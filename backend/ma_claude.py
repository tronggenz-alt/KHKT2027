"""Mã kích hoạt Claude: mặc định KHÔNG ai dùng được Claude (kể cả đề HSG), trừ admin và tài khoản đã nhập mã hợp lệ.

- Admin tạo mã (trang /admin, `admin_cli.py code create`): số lượt dùng Claude, ngày hết hạn (hết ngày đó), số tài khoản được
  dùng chung, ghi chú. Mã dạng CLD-XXXX-XXXX (8 ký tự ngẫu nhiên từ 32 ký tự dễ đọc, ~10^12 khả năng), CHỈ hiện đúng 1 lần
  lúc tạo; CSDL lưu HMAC-SHA256 (khoá suy từ khoá chính của app) + 4 ký tự cuối để admin nhận ra.
- Người dùng nhập mã trong Cài đặt → gắn mã vào tài khoản (mỗi tài khoản 1 mã). Nhập sai 5 lần trong 1 giờ → khoá ô nhập 1 giờ
  (theo tài khoản; thêm giới hạn theo IP để chống dò bằng nhiều tài khoản).
- Lượt: khi bài giải thật sự dùng Claude thì GIỮ 1 lượt (nguyên tử, không vượt tổng), giải thành công → trừ hẳn, lỗi → trả lại.
  Mọi kiểm tra ở backend; frontend chỉ gửi mong muốn "dùng Claude".
"""
from __future__ import annotations

import hmac
import hashlib
import logging
import re
import secrets
import threading
import time
from dataclasses import dataclass, field
from datetime import date

from . import db, tai_khoan
from .bao_mat import _suy_khoa
from .db import NguoiDung

log = logging.getLogger(__name__)

BANG_CHU = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"  # bỏ 0/O, 1/I – dễ đọc, dễ gõ
TIEN_TO = "CLD"
SAI_TOI_DA = 5            # nhập sai 5 lần trong 1 giờ → khoá 1 giờ (theo tài khoản)
SAI_TOI_DA_IP = 15        # theo IP (nhiều tài khoản cùng dò)
CUA_SO = 3600
KHOA_GIAY = 3600


class LoiMaClaude(Exception):
    def __init__(self, thong_diep: str, ma: str = "ma_claude_sai", status: int = 400, cho: int | None = None):
        super().__init__(thong_diep)
        self.ma, self.status, self.cho = ma, status, cho


# ---------------------------------------------------------------- mã
def chuan_hoa(ma: str) -> str:
    """'cld-7kq2 m9xa' → '7KQ2M9XA' (bỏ tiền tố CLD, dấu gạch, khoảng trắng; không phân biệt hoa thường)."""
    t = re.sub(r"[\s\-_.]+", "", (ma or "").upper())
    return t[len(TIEN_TO):] if t.startswith(TIEN_TO) and len(t) == len(TIEN_TO) + 8 else t


def bam(ma: str) -> str:
    return hmac.new(_suy_khoa(b"ma-claude/v1"), chuan_hoa(ma).encode(), hashlib.sha256).hexdigest()


def sinh_ma() -> str:
    t = "".join(secrets.choice(BANG_CHU) for _ in range(8))
    return f"{TIEN_TO}-{t[:4]}-{t[4:]}"


def _hom_nay() -> str:
    return date.today().isoformat()


def tao_ma(so_luot: int, het_han: str, so_tai_khoan: int = 1, ghi_chu: str = "", nguoi_tao: int | None = None) -> tuple[str, int]:
    """Tạo mã mới → (mã gốc – chỉ trả về đúng lần này, id)."""
    if not 1 <= int(so_luot) <= 100000:
        raise LoiMaClaude("Số lượt phải từ 1 đến 100000.", "so_luot_sai")
    if not 1 <= int(so_tai_khoan) <= 1000:
        raise LoiMaClaude("Số tài khoản dùng chung phải từ 1 đến 1000.", "so_tai_khoan_sai")
    try:
        han = date.fromisoformat(het_han)
    except (TypeError, ValueError) as e:
        raise LoiMaClaude("Ngày hết hạn không hợp lệ (YYYY-MM-DD).", "het_han_sai") from e
    if han < date.today():
        raise LoiMaClaude("Ngày hết hạn đã qua.", "het_han_sai")
    for _ in range(5):
        ma = sinh_ma()
        try:
            with db.ket_noi() as con:
                ma_id = con.execute(
                    "INSERT INTO claude_codes (code_hash, hint, uses_total, expires, max_accounts, note, created, created_by) "
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    (bam(ma), ma[-4:], int(so_luot), han.isoformat(), int(so_tai_khoan), (ghi_chu or "").strip()[:200],
                     time.time(), nguoi_tao)).lastrowid
            return ma, ma_id
        except Exception as e:  # noqa: BLE001 – trùng băm (gần như không thể) → sinh lại
            if "UNIQUE" not in str(e):
                raise
    raise LoiMaClaude("Không sinh được mã, thử lại.", "loi")


def _trang_thai(r) -> str:
    if r["revoked"]:
        return "thu_hoi"
    if r["expires"] < _hom_nay():
        return "het_han"
    if r["uses_used"] + r["uses_held"] >= r["uses_total"]:
        return "het_luot"
    return "con_hieu_luc"


TEN_TRANG_THAI = {"con_hieu_luc": "còn hiệu lực", "het_han": "hết hạn", "het_luot": "hết lượt", "thu_hoi": "đã thu hồi"}


def _dong(r, nguoi: list[dict]) -> dict:
    tt = _trang_thai(r)
    return {"id": r["id"], "goi_y": f"{TIEN_TO}-…-{r['hint']}", "tong_luot": r["uses_total"], "da_dung": r["uses_used"],
            "dang_giu": r["uses_held"], "con_lai": max(0, r["uses_total"] - r["uses_used"] - r["uses_held"]),
            "het_han": r["expires"], "so_tai_khoan": r["max_accounts"], "ghi_chu": r["note"],
            "tao_luc": time.strftime("%Y-%m-%d %H:%M", time.localtime(r["created"])),
            "thu_hoi_luc": time.strftime("%Y-%m-%d %H:%M", time.localtime(r["revoked"])) if r["revoked"] else "",
            "trang_thai": tt, "ten_trang_thai": TEN_TRANG_THAI[tt], "nguoi_dung": nguoi}


def danh_sach() -> list[dict]:
    with db.ket_noi() as con:
        rows = con.execute("SELECT * FROM claude_codes ORDER BY id DESC").fetchall()
        gan = con.execute("SELECT c.code_id, u.id, u.name, u.email, c.attached FROM claude_code_users c JOIN users u ON u.id = c.user_id "
                          "ORDER BY c.attached").fetchall()
        dung = {(r["code_id"], r["user_id"]): r["n"] for r in con.execute(
            "SELECT code_id, user_id, COUNT(DISTINCT job_id) AS n FROM ai_usage WHERE provider = 'claude' AND ok = 1 AND code_id IS NOT NULL "
            "GROUP BY code_id, user_id")}
    theo_ma: dict[int, list[dict]] = {}
    for g in gan:
        theo_ma.setdefault(g["code_id"], []).append({"id": g["id"], "ten": g["name"], "email": g["email"],
                                                    "gan_luc": time.strftime("%Y-%m-%d %H:%M", time.localtime(g["attached"])),
                                                    "so_bai": dung.get((g["code_id"], g["id"]), 0)})
    return [_dong(r, theo_ma.get(r["id"], [])) for r in rows]


def tim(chuoi: str) -> dict | None:
    """Theo id ('12' / '#12') hoặc mã gốc."""
    s = (chuoi or "").strip().lstrip("#")
    with db.ket_noi() as con:
        r = (con.execute("SELECT id FROM claude_codes WHERE id = ?", (int(s),)).fetchone() if s.isdigit()
             else con.execute("SELECT id FROM claude_codes WHERE code_hash = ?", (bam(s),)).fetchone())
    if r is None:
        return None
    return next((x for x in danh_sach() if x["id"] == r["id"]), None)


def thu_hoi(ma_id: int) -> dict:
    with db.ket_noi() as con:
        n = con.execute("UPDATE claude_codes SET revoked = ? WHERE id = ? AND revoked IS NULL", (time.time(), ma_id)).rowcount
    if not n and tim(str(ma_id)) is None:
        raise LoiMaClaude("Không có mã này.", "khong_co", 404)
    return tim(str(ma_id))


def them_luot(ma_id: int, so: int) -> dict:
    if not 1 <= int(so) <= 100000:
        raise LoiMaClaude("Số lượt cộng thêm phải từ 1 đến 100000.", "so_luot_sai")
    with db.ket_noi() as con:
        n = con.execute("UPDATE claude_codes SET uses_total = uses_total + ? WHERE id = ?", (int(so), ma_id)).rowcount
    if not n:
        raise LoiMaClaude("Không có mã này.", "khong_co", 404)
    return tim(str(ma_id))


def don_giu_khi_khoi_dong() -> None:
    """Khởi động lại app thì không còn bài nào đang giải → bỏ mọi lượt đang giữ (tránh "kẹt" lượt khi app tắt giữa chừng)."""
    try:
        with db.ket_noi() as con:
            con.execute("UPDATE claude_codes SET uses_held = 0 WHERE uses_held <> 0")
    except Exception:  # noqa: BLE001
        log.exception("Không dọn được lượt mã Claude đang giữ")


# ---------------------------------------------------------------- người dùng nhập mã (chống dò)
def _khoa_den(moc: list[float], toi_da: int) -> float:
    """Thời điểm hết khoá: lần sai thứ `toi_da` trong một cửa sổ 1 giờ → khoá 1 giờ kể từ lần đó."""
    moc = sorted(moc)
    den = 0.0
    for i in range(toi_da - 1, len(moc)):
        if moc[i] - moc[i - toi_da + 1] <= CUA_SO:
            den = max(den, moc[i] + KHOA_GIAY)
    return den


def khoa_nhap(uid: int | None, ip: str = "") -> float:
    """> 0: số giây còn bị khoá ô nhập mã."""
    bay_gio = time.time()
    with db.ket_noi() as con:
        theo_tk = [r[0] for r in con.execute("SELECT ts FROM claude_code_fails WHERE user_id = ? AND ts >= ?",
                                             (uid, bay_gio - CUA_SO - KHOA_GIAY))] if uid is not None else []
        theo_ip = [r[0] for r in con.execute("SELECT ts FROM claude_code_fails WHERE ip = ? AND ip <> '' AND ts >= ?",
                                             (ip, bay_gio - CUA_SO - KHOA_GIAY))] if ip else []
    den = max(_khoa_den(theo_tk, SAI_TOI_DA), _khoa_den(theo_ip, SAI_TOI_DA_IP))
    return max(0.0, den - bay_gio)


def _ghi_sai(uid: int, ip: str) -> None:
    with db.ket_noi() as con:
        con.execute("INSERT INTO claude_code_fails (user_id, ip, ts) VALUES (?, ?, ?)", (uid, ip or "", time.time()))
        con.execute("DELETE FROM claude_code_fails WHERE ts < ?", (time.time() - 2 * 86400,))


def gan_ma(u: NguoiDung, ma: str, ip: str = "") -> dict:
    """Người dùng nhập mã → gắn vào tài khoản. Sai → ghi lần sai (5 lần/giờ → khoá 1 giờ)."""
    cho = khoa_nhap(u.id, ip)
    if cho > 0:
        raise LoiMaClaude(f"Bạn đã nhập sai mã quá nhiều lần – ô nhập bị khoá, thử lại sau {int(cho // 60) + 1} phút.",
                          "ma_claude_bi_khoa", 429, int(cho) + 1)
    t = chuan_hoa(ma)
    with db.ket_noi() as con:
        r = con.execute("SELECT * FROM claude_codes WHERE code_hash = ?", (bam(t),)).fetchone() if len(t) == 8 else None
        if r is not None:
            dang_dung = con.execute("SELECT COUNT(*) FROM claude_code_users WHERE code_id = ? AND user_id <> ?", (r["id"], u.id)).fetchone()[0]
    if r is None:
        _ghi_sai(u.id, ip)
        con_lai = SAI_TOI_DA - _so_sai_gan_day(u.id)
        if khoa_nhap(u.id, ip) > 0:
            raise LoiMaClaude("Mã không đúng. Bạn đã nhập sai 5 lần – ô nhập bị khoá 1 giờ.", "ma_claude_bi_khoa", 429, KHOA_GIAY)
        raise LoiMaClaude(f"Mã không đúng (còn {max(0, con_lai)} lần thử trước khi bị khoá 1 giờ).", "ma_claude_sai")
    tt = _trang_thai(r)
    if tt != "con_hieu_luc":
        raise LoiMaClaude(f"Mã này {TEN_TRANG_THAI[tt]} – hãy xin admin mã khác.", f"ma_claude_{tt}")
    if dang_dung >= r["max_accounts"]:
        raise LoiMaClaude("Mã này đã đủ số tài khoản được dùng chung.", "ma_claude_du_tai_khoan")
    with db.ket_noi() as con:
        con.execute("DELETE FROM claude_code_users WHERE user_id = ?", (u.id,))
        con.execute("INSERT INTO claude_code_users (code_id, user_id, attached) VALUES (?, ?, ?)", (r["id"], u.id, time.time()))
    return trang_thai(u)


def _so_sai_gan_day(uid: int) -> int:
    with db.ket_noi() as con:
        return con.execute("SELECT COUNT(*) FROM claude_code_fails WHERE user_id = ? AND ts >= ?", (uid, time.time() - CUA_SO)).fetchone()[0]


def go_ma(u: NguoiDung) -> None:
    with db.ket_noi() as con:
        con.execute("DELETE FROM claude_code_users WHERE user_id = ?", (u.id,))


def _ma_cua(uid: int):
    with db.ket_noi() as con:
        return con.execute("SELECT k.* FROM claude_code_users c JOIN claude_codes k ON k.id = c.code_id WHERE c.user_id = ?",
                           (uid,)).fetchone()


def trang_thai(u: NguoiDung | None, ip: str = "") -> dict:
    """Cho /api/toi và Cài đặt → Mã Claude: dùng được Claude không, còn bao nhiêu lượt, hết hạn ngày nào."""
    if u is None:
        return {"co_the_dung": False, "la_admin": False, "co_ma": False}
    if tai_khoan.la_chu_app(u):
        return {"co_the_dung": True, "la_admin": True, "co_ma": False}
    r = _ma_cua(u.id)
    kq = {"co_the_dung": False, "la_admin": False, "co_ma": r is not None, "khoa_nhap_giay": int(khoa_nhap(u.id, ip))}
    if r is not None:
        tt = _trang_thai(r)
        kq.update(co_the_dung=tt == "con_hieu_luc", trang_thai=tt, ten_trang_thai=TEN_TRANG_THAI[tt], goi_y=f"{TIEN_TO}-…-{r['hint']}",
                  con_luot=max(0, r["uses_total"] - r["uses_used"] - r["uses_held"]), tong_luot=r["uses_total"], het_han=r["expires"])
    return kq


# ---------------------------------------------------------------- quyền dùng Claude cho MỘT yêu cầu (giữ / trừ / trả lượt)
@dataclass
class QuyenClaude:
    la_admin: bool = False
    ma_id: int | None = None
    moi_muc: bool = False      # True (admin): bật "Dùng Claude" là giải bằng Claude ở mọi mức; mã Claude: chỉ bài HSG
    nguon: str = "ma"
    ly_do_het: str = "mã Claude đã hết lượt hoặc hết hạn"
    _da_giu: bool = field(default=False, repr=False)
    _khoa: threading.Lock = field(default_factory=threading.Lock, repr=False)

    def giu(self) -> bool:
        """Giữ 1 lượt cho yêu cầu này (một lần; nguyên tử – không vượt tổng lượt, mã phải còn hạn, chưa thu hồi)."""
        if self.la_admin:
            return True
        with self._khoa:
            if self._da_giu:
                return True
            with db.ket_noi() as con:
                n = con.execute("UPDATE claude_codes SET uses_held = uses_held + 1 WHERE id = ? AND revoked IS NULL AND expires >= ? "
                                "AND uses_used + uses_held < uses_total", (self.ma_id, _hom_nay())).rowcount
            self._da_giu = n == 1
            return self._da_giu

    def chot(self) -> bool:
        """Bài giải bằng Claude thành công → trừ hẳn lượt đã giữ. Trả True nếu đã trừ."""
        with self._khoa:
            if self.la_admin or not self._da_giu:
                return False
            with db.ket_noi() as con:
                con.execute("UPDATE claude_codes SET uses_used = uses_used + 1, uses_held = MAX(0, uses_held - 1) WHERE id = ?",
                            (self.ma_id,))
            self._da_giu = False
            return True

    def tra(self) -> None:
        """Bài lỗi / không dùng Claude → trả lượt đã giữ."""
        with self._khoa:
            if self.la_admin or not self._da_giu:
                return
            with db.ket_noi() as con:
                con.execute("UPDATE claude_codes SET uses_held = MAX(0, uses_held - 1) WHERE id = ?", (self.ma_id,))
            self._da_giu = False


def quyen(u: NguoiDung | None) -> QuyenClaude | None:
    """Quyền dùng Claude của tài khoản (None = không được): admin luôn được; người khác cần mã còn hiệu lực + còn lượt."""
    if u is None:
        return None
    if tai_khoan.la_chu_app(u):
        return QuyenClaude(la_admin=True, moi_muc=True, nguon="admin")
    r = _ma_cua(u.id)
    if r is None or _trang_thai(r) != "con_hieu_luc":
        return None
    return QuyenClaude(ma_id=r["id"])
