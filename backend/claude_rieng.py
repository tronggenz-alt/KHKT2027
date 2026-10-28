"""API key Claude RIÊNG của người dùng (Cài đặt → API key Claude): có key riêng thì dùng được Claude không cần Key Claude chung / mã Claude,
gọi bằng key của họ – KHÔNG trừ lượt Claude, KHÔNG tốn tiền máy chủ (ai_usage.key_source = "claude_rieng" = "key người dùng").

- CHỈ tài khoản được admin BẬT (users.allow_own_claude_key – /admin, `admin_cli.py ownkey on`) mới thấy / lưu / dùng; admin luôn được.
  Người dùng có công tắc "Dùng key của tôi". Admin TẮT → key vô hiệu ngay, bản mã hoá giữ 30 ngày (bật lại dùng tiếp), quá hạn thì xoá hẳn.

- Lưu: gọi thử 1 yêu cầu rất nhỏ (max_tokens 1) → hợp lệ thì mã hoá Fernet (bao_mat.ma_hoa_key_claude, khoá USER_KEY_SECRET) rồi lưu;
  chỉ trả 4 ký tự cuối, không bao giờ trả key về frontend, không ghi key vào log.
- Key lỗi lúc giải (hết tiền, bị thu hồi) → llm báo người dùng + chuyển Gemini, KHÔNG tự dùng key Claude máy chủ.
"""
from __future__ import annotations

import logging
import time
from dataclasses import dataclass, field

import anthropic

from . import db, tai_khoan
from .bao_mat import LoiBaoMat, bon_ky_tu_cuoi, giai_ma_key_claude, ma_hoa_key_claude
from .config import settings
from .db import NguoiDung

log = logging.getLogger(__name__)


class LoiKeyRieng(Exception):
    def __init__(self, thong_diep: str, ma: str = "key_claude_rieng_sai", status: int = 400):
        super().__init__(thong_diep)
        self.ma, self.status = ma, status


def chuan_hoa(key: str) -> str:
    k = "".join((key or "").split()).strip("\"'")
    if not k.startswith("sk-ant-") or k.startswith("sk-ant-admin") or len(k) < 30:
        raise LoiKeyRieng("Đây không phải API key Claude – key đúng có dạng sk-ant-api03-… (tạo ở platform.claude.com → API keys).")
    return k


def kiem_tra(key: str) -> dict:
    """Gọi thử Claude (max_tokens 1). → {"hop_le": bool, "thong_diep", "canh_bao"}. Lỗi mạng / máy chủ quá tải → LoiKeyRieng (chưa lưu)."""
    client = anthropic.Anthropic(api_key=key, timeout=30.0, max_retries=0)
    try:
        client.messages.create(model=settings.model, max_tokens=1, messages=[{"role": "user", "content": "Hi"}])
    except anthropic.AuthenticationError:
        return {"hop_le": False, "thong_diep": "Key không hợp lệ hoặc đã bị thu hồi."}
    except anthropic.PermissionDeniedError:
        return {"hop_le": False, "thong_diep": f"Key không có quyền dùng mô hình {settings.model}."}
    except anthropic.NotFoundError:
        return {"hop_le": False, "thong_diep": f"Tài khoản của key không dùng được mô hình {settings.model}."}
    except anthropic.RateLimitError:
        return {"hop_le": True, "canh_bao": "Key hợp lệ nhưng đang bị giới hạn tần suất – lát nữa giải bài sẽ dùng được."}
    except anthropic.BadRequestError as e:
        if "credit balance" in str(e).lower():
            return {"hop_le": True, "canh_bao": "Key hợp lệ nhưng tài khoản Anthropic của bạn đã HẾT TÍN DỤNG – Claude sẽ lỗi và tự chuyển Gemini "
                                                "cho tới khi bạn nạp tiền tại platform.claude.com."}
        return {"hop_le": False, "thong_diep": f"Claude từ chối yêu cầu thử: {getattr(e, 'message', e)}"[:300]}
    except anthropic.APIConnectionError as e:
        raise LoiKeyRieng("Không kết nối được tới Claude để kiểm tra key – thử lại sau.", "khong_kiem_tra_duoc", 503) from e
    except anthropic.APIStatusError as e:
        raise LoiKeyRieng(f"Máy chủ Claude đang lỗi ({e.status_code}) – chưa kiểm tra được key, thử lại sau.", "khong_kiem_tra_duoc", 503) from e
    return {"hop_le": True}


GIU_KHI_TAT_NGAY = 30
_KHONG_PHEP = "Tài khoản chưa được admin cho phép dùng API key Claude riêng."


def _cot(uid: int):
    with db.ket_noi() as con:
        return con.execute("SELECT id, claude_key_enc, claude_key_last4, claude_key_checked, allow_own_claude_key, allow_own_claude_key_at, "
                           "allow_own_claude_key_by, own_claude_key_off_at, use_own_claude_key, claude_key_error, own_claude_key_req_at "
                           "FROM users WHERE id = ?", (uid,)).fetchone()


def duoc_phep(u: NguoiDung | None) -> bool:
    """Admin luôn được; người khác cần admin bật (users.allow_own_claude_key)."""
    if u is None:
        return False
    if tai_khoan.la_chu_app(u):
        return True
    r = _cot(u.id)
    return bool(r and r["allow_own_claude_key"])


def _kiem_phep(u: NguoiDung) -> None:
    if not duoc_phep(u):
        raise LoiKeyRieng(_KHONG_PHEP, "khong_duoc_phep", 403)


def luu(u: NguoiDung, key: str, kiem=None) -> dict:
    _kiem_phep(u)
    k = chuan_hoa(key)
    kq = (kiem or kiem_tra)(k)
    if not kq.get("hop_le"):
        raise LoiKeyRieng(kq.get("thong_diep") or "Key không hợp lệ.")
    try:
        db.dat_key_claude(u.id, ma_hoa_key_claude(k), bon_ky_tu_cuoi(k))
    except LoiBaoMat as e:
        raise LoiKeyRieng(str(e), "chua_co_khoa_ma_hoa", 503) from e
    xoa_loi(u.id)
    return {**trang_thai(u), "canh_bao": kq.get("canh_bao", "")}


def xoa(u: NguoiDung) -> None:
    _kiem_phep(u)
    db.dat_key_claude(u.id, None)
    xoa_loi(u.id)


def dat_dung(u: NguoiDung, bat: bool) -> dict:
    """Công tắc "Dùng key của tôi" (tắt → giải theo quy tắc bình thường: Key Claude chung / Gemini)."""
    _kiem_phep(u)
    with db.ket_noi() as con:
        con.execute("UPDATE users SET use_own_claude_key = ? WHERE id = ?", (int(bool(bat)), u.id))
    return trang_thai(u)


def yeu_cau(u: NguoiDung) -> bool:
    """Người dùng chưa được bật bấm "Gửi yêu cầu cho admin". → True nếu là yêu cầu MỚI (đã có yêu cầu đang chờ thì không ghi lại)."""
    if duoc_phep(u):
        raise LoiKeyRieng("Tài khoản đã được bật dùng API key Claude riêng.", "da_duoc_bat", 400)
    with db.ket_noi() as con:
        return con.execute("UPDATE users SET own_claude_key_req_at = ? WHERE id = ? AND own_claude_key_req_at IS NULL",
                           (time.time(), u.id)).rowcount == 1


def tu_choi(uid: int) -> bool:
    """Admin từ chối yêu cầu (xoá yêu cầu đang chờ; người dùng gửi lại được). → True nếu có yêu cầu."""
    with db.ket_noi() as con:
        return con.execute("UPDATE users SET own_claude_key_req_at = NULL WHERE id = ? AND own_claude_key_req_at IS NOT NULL",
                           (uid,)).rowcount == 1


def ghi_loi(uid: int | None, loi: str) -> None:
    """Lỗi tài khoản khi gọi bằng key riêng (hết tiền / key bị thu hồi…) → admin thấy "Key lỗi"."""
    if uid:
        with db.ket_noi() as con:
            con.execute("UPDATE users SET claude_key_error = ? WHERE id = ?", ((loi or "lỗi")[:200], uid))


def xoa_loi(uid: int | None) -> None:
    if uid:
        with db.ket_noi() as con:
            con.execute("UPDATE users SET claude_key_error = '' WHERE id = ? AND claude_key_error <> ''", (uid,))


def trang_thai(u: NguoiDung | None) -> dict:
    """Cho /api/toi và Cài đặt – KHÔNG có key, chỉ 4 ký tự cuối. Chưa được bật → chỉ {"duoc_phep": False} (ẩn hẳn mục)."""
    if u is None:
        return {"duoc_phep": False, "co": False, "cuoi": ""}
    if not duoc_phep(u):  # chỉ cho biết đã gửi yêu cầu chưa – không có gì về key
        r = _cot(u.id)
        yc = r["own_claude_key_req_at"] if r is not None else None
        return {"duoc_phep": False, "co": False, "cuoi": "", "da_yeu_cau": bool(yc), "yeu_cau_luc": _gio(yc)}
    r = _cot(u.id)
    enc, luc = r["claude_key_enc"], r["claude_key_checked"]
    return {"duoc_phep": True, "co": bool(enc), "cuoi": (r["claude_key_last4"] or "") if enc else "", "dung": bool(r["use_own_claude_key"]),
            "loi": r["claude_key_error"] if enc else "",
            "kiem_tra_luc": time.strftime("%Y-%m-%d %H:%M", time.localtime(luc)) if enc and luc else ""}


def lay_key(u: NguoiDung) -> str:
    """Key đã giải mã để GỌI CLAUDE – "" nếu chưa được admin bật, người dùng tắt "Dùng key của tôi", chưa có key / không giải mã được."""
    if not duoc_phep(u):
        return ""
    r = _cot(u.id)
    enc = r["claude_key_enc"] if r is not None and r["use_own_claude_key"] else None
    if not enc:
        return ""
    try:
        return giai_ma_key_claude(enc)
    except LoiBaoMat:
        log.warning("Không giải mã được key Claude riêng của tài khoản #%s", u.id)
        return ""


# ---------------------------------------------------------------- admin bật / tắt quyền dùng key riêng
def _gio(ts) -> str:
    return time.strftime("%Y-%m-%d %H:%M", time.localtime(ts)) if ts else ""


def _trang_thai_admin(r, la_admin: bool, email_admin: dict) -> dict:
    enc = r["claude_key_enc"]
    tt = "loi" if enc and r["claude_key_error"] else "da_nhap" if enc else "chua_nhap"
    xoa_sau = None
    if not la_admin and not r["allow_own_claude_key"] and enc and r["own_claude_key_off_at"]:
        xoa_sau = max(0, GIU_KHI_TAT_NGAY - int((time.time() - r["own_claude_key_off_at"]) // 86400))
    return {"bat": la_admin or bool(r["allow_own_claude_key"]), "la_admin": la_admin, "trang_thai": tt,
            "ten_trang_thai": {"loi": "Key lỗi", "da_nhap": f"Đã nhập key (…{r['claude_key_last4'] or ''})", "chua_nhap": "Chưa nhập key"}[tt],
            "cuoi": (r["claude_key_last4"] or "") if enc else "", "loi": r["claude_key_error"] if enc else "", "dung": bool(r["use_own_claude_key"]),
            "bat_luc": _gio(r["allow_own_claude_key_at"]), "bat_boi": email_admin.get(r["allow_own_claude_key_by"], ""),
            "tat_luc": _gio(r["own_claude_key_off_at"]) if not r["allow_own_claude_key"] else "", "xoa_sau_ngay": xoa_sau,
            "yeu_cau_luc": _gio(r["own_claude_key_req_at"]) if not la_admin and not r["allow_own_claude_key"] else ""}


def danh_sach_admin() -> dict[int, dict]:
    """{uid: trạng thái key riêng} cho bảng người dùng ở /admin (một truy vấn)."""
    don_het_han()
    with db.ket_noi() as con:
        rows = con.execute("SELECT id, email, claude_key_enc, claude_key_last4, allow_own_claude_key, allow_own_claude_key_at, "
                           "allow_own_claude_key_by, own_claude_key_off_at, use_own_claude_key, claude_key_error, own_claude_key_req_at "
                           "FROM users").fetchall()
    email = {r["id"]: r["email"] for r in rows}
    kq = {}
    for r in rows:
        u = db.lay_nguoi_dung(r["id"])
        kq[r["id"]] = _trang_thai_admin(r, bool(u and tai_khoan.la_chu_app(u)), email)
    return kq


def trang_thai_admin(uid: int) -> dict | None:
    r, u = _cot(uid), db.lay_nguoi_dung(uid)
    if r is None or u is None:
        return None
    ad = db.lay_nguoi_dung(r["allow_own_claude_key_by"]) if r["allow_own_claude_key_by"] else None
    return _trang_thai_admin(r, tai_khoan.la_chu_app(u), {ad.id: ad.email} if ad else {})


def cap_quyen(uid: int, bat: bool, admin_id: int | None = None) -> bool:
    """Admin bật / tắt. Tắt: key vô hiệu ngay, bản mã hoá giữ GIU_KHI_TAT_NGAY ngày. → True nếu trạng thái thực sự đổi."""
    with db.ket_noi() as con:
        if bat:
            n = con.execute("UPDATE users SET allow_own_claude_key = 1, allow_own_claude_key_at = ?, allow_own_claude_key_by = ?, "
                            "own_claude_key_off_at = NULL, own_claude_key_req_at = NULL WHERE id = ? AND allow_own_claude_key = 0",
                            (time.time(), admin_id, uid)).rowcount
        else:
            n = con.execute("UPDATE users SET allow_own_claude_key = 0, own_claude_key_off_at = ? WHERE id = ? AND allow_own_claude_key = 1",
                            (time.time(), uid)).rowcount
    return n == 1


def don_het_han() -> int:
    """Xoá hẳn key mã hoá của tài khoản đã bị admin TẮT quá GIU_KHI_TAT_NGAY ngày (khởi động app + mỗi lần admin xem danh sách)."""
    with db.ket_noi() as con:
        return con.execute("UPDATE users SET claude_key_enc = NULL, claude_key_last4 = NULL, claude_key_checked = NULL, claude_key_error = '' "
                           "WHERE allow_own_claude_key = 0 AND own_claude_key_off_at IS NOT NULL AND own_claude_key_off_at < ? "
                           "AND claude_key_enc IS NOT NULL", (time.time() - GIU_KHI_TAT_NGAY * 86400,)).rowcount


@dataclass
class QuyenRieng:
    """Quyền Claude bằng key RIÊNG (cùng giao diện ma_claude.QuyenClaude / key_claude.QuyenKey): không giữ / trừ lượt nào."""

    api_key: str = field(default="", repr=False)
    la_admin: bool = False
    ma_id: int | None = None
    moi_muc: bool = True
    nguon: str = "key_rieng"
    ly_do_het: str = ""

    def giu(self) -> bool:
        return True

    def chot(self) -> bool:
        return False

    def tra(self) -> None:
        return None
