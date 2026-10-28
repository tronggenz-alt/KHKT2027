"""Key Claude dùng chung: người dùng (không phải admin) nhập đúng key do chủ app đặt (CLAUDE_ACCESS_KEY trong .env) thì được giải bài
bằng Claude của máy chủ (ANTHROPIC_API_KEY); không nhập thì KHÔNG dùng được Claude. Song song với mã kích hoạt Claude (ma_claude.py):
có một trong hai là dùng được Claude.

- Key chỉ nằm trong .env: không viết trong code, không gửi xuống frontend, không ghi log. So sánh bằng hmac.compare_digest trên
  dấu vân tay HMAC-SHA256 (khoá suy từ khoá chính của app) → thời gian so sánh không phụ thuộc nội dung / độ dài key nhập.
- Mỗi tài khoản đã kích hoạt lưu dấu vân tay của key LÚC KÍCH HOẠT. Chủ app đổi CLAUDE_ACCESS_KEY + khởi động lại → vân tay khác
  → mọi tài khoản kích hoạt bằng key cũ tự mất quyền, phải nhập key mới.
- Nhập sai 5 lần trong 1 giờ (theo tài khoản HOẶC theo IP) → khoá ô nhập 1 giờ (chống dò key).
- Mỗi người tối đa CLAUDE_DAILY_LIMIT bài Claude mỗi ngày: giữ 1 lượt khi bài thật sự gọi Claude (nguyên tử), xong → chốt, lỗi → trả.
- Admin thu hồi quyền của từng người (người đó không tự kích hoạt lại được cho tới khi admin mở lại).
"""
from __future__ import annotations

import hashlib
import hmac
import logging
import threading
import time
from dataclasses import dataclass, field
from datetime import date

from . import claude_rieng, db, ma_claude, tai_khoan
from .bao_mat import _suy_khoa
from .config import settings
from .db import NguoiDung

log = logging.getLogger(__name__)

SAI_TOI_DA = 5      # sai 5 lần trong 1 giờ → khoá 1 giờ (theo tài khoản)
SAI_TOI_DA_IP = 5   # và theo IP
CUA_SO = ma_claude.CUA_SO
KHOA_GIAY = ma_claude.KHOA_GIAY
GIU_LAN_SAI_NGAY = 30  # giữ lịch sử nhập sai 30 ngày (admin xem)

TEN_TRANG_THAI = {"da_mo": "đã mở Claude", "chua_mo": "chưa mở", "key_cu": "key cũ – cần nhập key mới",
                  "bi_thu_hoi": "admin đã thu hồi", "tat": "chủ app chưa bật Key Claude"}


class LoiKeyClaude(Exception):
    def __init__(self, thong_diep: str, ma: str = "key_claude_sai", status: int = 400, cho: int | None = None):
        super().__init__(thong_diep)
        self.ma, self.status, self.cho = ma, status, cho


def bat() -> bool:
    """Chủ app đã đặt CLAUDE_ACCESS_KEY chưa (trống = tắt tính năng)."""
    return bool(settings.claude_access_key)


def gioi_han_ngay() -> int:
    return settings.claude_daily_limit


def van_tay(key: str) -> str:
    """Dấu vân tay HMAC-SHA256 của một key (không đảo ngược được nếu không có khoá chính của app)."""
    return hmac.new(_suy_khoa(b"key-claude/v1"), (key or "").strip().encode("utf-8"), hashlib.sha256).hexdigest()


def van_tay_hien_tai() -> str:
    return van_tay(settings.claude_access_key) if bat() else ""


def _dung_key(nhap: str) -> bool:
    hien_tai = van_tay_hien_tai()
    return bool(hien_tai) and hmac.compare_digest(van_tay(nhap), hien_tai)


def _hom_nay() -> str:
    return date.today().isoformat()


def _gio(ts: float | None) -> str:
    return time.strftime("%Y-%m-%d %H:%M", time.localtime(ts)) if ts else ""


# ---------------------------------------------------------------- trạng thái một tài khoản
def _dong(uid: int):
    with db.ket_noi() as con:
        return con.execute("SELECT * FROM claude_key_users WHERE user_id = ?", (uid,)).fetchone()


def _trang_thai_dong(r) -> str:
    if r is None:
        return "chua_mo"
    if r["revoked"]:
        return "bi_thu_hoi"
    if not bat():
        return "tat"
    return "da_mo" if hmac.compare_digest(r["fingerprint"], van_tay_hien_tai()) else "key_cu"


def da_dung_hom_nay(uid: int) -> int:
    """Số bài Claude hôm nay (đã xong + đang giữ)."""
    with db.ket_noi() as con:
        return con.execute("SELECT COUNT(*) FROM claude_key_uses WHERE user_id = ? AND day = ?", (uid, _hom_nay())).fetchone()[0]


def trang_thai(u: NguoiDung | None, ip: str = "") -> dict:
    """Cho /api/toi và Cài đặt → Key Claude. KHÔNG BAO GIỜ chứa key."""
    if u is None:
        return {"bat": bat(), "da_mo": False, "la_admin": False, "trang_thai": "chua_mo"}
    if tai_khoan.la_chu_app(u):
        return {"bat": bat(), "da_mo": True, "la_admin": True, "trang_thai": "da_mo"}
    r = _dong(u.id)
    tt = _trang_thai_dong(r)
    kq = {"bat": bat(), "da_mo": tt == "da_mo", "la_admin": False, "trang_thai": tt, "ten_trang_thai": TEN_TRANG_THAI[tt],
          "kich_hoat_luc": _gio(r["activated"]) if r is not None else "", "khoa_nhap_giay": int(khoa_nhap(u.id, ip))}
    if tt == "da_mo":
        dung = da_dung_hom_nay(u.id)
        kq.update(hom_nay=dung, gioi_han=gioi_han_ngay(), con_hom_nay=max(0, gioi_han_ngay() - dung))
    return kq


# ---------------------------------------------------------------- nhập key (chống dò)
def khoa_nhap(uid: int | None, ip: str = "") -> float:
    """> 0: số giây ô nhập key còn bị khoá (5 lần sai/giờ theo tài khoản hoặc theo IP)."""
    bay_gio = time.time()
    with db.ket_noi() as con:
        theo_tk = [r[0] for r in con.execute("SELECT ts FROM claude_key_fails WHERE user_id = ? AND ts >= ?",
                                             (uid, bay_gio - CUA_SO - KHOA_GIAY))] if uid is not None else []
        theo_ip = [r[0] for r in con.execute("SELECT ts FROM claude_key_fails WHERE ip = ? AND ip <> '' AND ts >= ?",
                                             (ip, bay_gio - CUA_SO - KHOA_GIAY))] if ip else []
    den = max(ma_claude._khoa_den(theo_tk, SAI_TOI_DA), ma_claude._khoa_den(theo_ip, SAI_TOI_DA_IP))
    return max(0.0, den - bay_gio)


def _con_thu(uid: int, ip: str) -> int:
    tu = time.time() - CUA_SO
    with db.ket_noi() as con:
        tk = con.execute("SELECT COUNT(*) FROM claude_key_fails WHERE user_id = ? AND ts >= ?", (uid, tu)).fetchone()[0]
        theo_ip = con.execute("SELECT COUNT(*) FROM claude_key_fails WHERE ip = ? AND ip <> '' AND ts >= ?", (ip, tu)).fetchone()[0] if ip else 0
    return max(0, min(SAI_TOI_DA - tk, SAI_TOI_DA_IP - theo_ip))


def _ghi_sai(uid: int, ip: str) -> None:
    with db.ket_noi() as con:
        con.execute("INSERT INTO claude_key_fails (user_id, ip, ts) VALUES (?, ?, ?)", (uid, ip or "", time.time()))
        con.execute("DELETE FROM claude_key_fails WHERE ts < ?", (time.time() - GIU_LAN_SAI_NGAY * 86400,))


def kich_hoat(u: NguoiDung, nhap: str, ip: str = "") -> dict:
    """Người dùng nhập key → mở Claude cho tài khoản. Sai → LoiKeyClaude (ma "key_claude_sai"; lần sai thứ 5 → "key_claude_bi_khoa")."""
    if tai_khoan.la_chu_app(u):
        return trang_thai(u, ip)
    if not bat():
        raise LoiKeyClaude("Chủ app chưa bật Key Claude (chưa đặt CLAUDE_ACCESS_KEY) – chưa mở khoá Claude bằng key được.", "key_claude_tat")
    r = _dong(u.id)
    if r is not None and r["revoked"]:  # kiểm tra TRƯỚC khi so key: không để lộ key đúng/sai
        raise LoiKeyClaude("Admin đã thu hồi quyền Claude của tài khoản này – hãy liên hệ admin.", "key_claude_bi_thu_hoi", 403)
    cho = khoa_nhap(u.id, ip)
    if cho > 0:
        raise LoiKeyClaude(f"Bạn đã nhập sai key quá nhiều lần – ô nhập bị khoá, thử lại sau {int(cho // 60) + 1} phút.",
                           "key_claude_bi_khoa", 429, int(cho) + 1)
    if not _dung_key((nhap or "")[:200]):
        _ghi_sai(u.id, ip)
        if khoa_nhap(u.id, ip) > 0:
            raise LoiKeyClaude("Key không đúng. Bạn đã nhập sai 5 lần – ô nhập bị khoá 1 giờ.", "key_claude_bi_khoa", 429, KHOA_GIAY)
        raise LoiKeyClaude(f"Key không đúng (còn {_con_thu(u.id, ip)} lần thử trước khi bị khoá 1 giờ).")
    with db.ket_noi() as con:
        con.execute("INSERT INTO claude_key_users (user_id, activated, fingerprint) VALUES (?, ?, ?) "
                    "ON CONFLICT(user_id) DO UPDATE SET activated = excluded.activated, fingerprint = excluded.fingerprint, "
                    "revoked = NULL, revoked_by = NULL", (u.id, time.time(), van_tay_hien_tai()))
    return trang_thai(u, ip)


def go_key(u: NguoiDung) -> None:
    """Người dùng tự tắt Claude (gỡ key). Đã bị admin thu hồi thì giữ nguyên dấu thu hồi."""
    with db.ket_noi() as con:
        con.execute("DELETE FROM claude_key_users WHERE user_id = ? AND revoked IS NULL", (u.id,))


# ---------------------------------------------------------------- admin
def thu_hoi(uid: int, admin_id: int | None = None) -> bool:
    """Admin thu hồi quyền Claude (bằng key) của một người: mất quyền ngay, không tự kích hoạt lại được. False = đã thu hồi từ trước."""
    with db.ket_noi() as con:
        n = con.execute("UPDATE claude_key_users SET revoked = ?, revoked_by = ? WHERE user_id = ? AND revoked IS NULL",
                        (time.time(), admin_id, uid)).rowcount
        if n == 0 and con.execute("SELECT 1 FROM claude_key_users WHERE user_id = ?", (uid,)).fetchone() is None:
            con.execute("INSERT INTO claude_key_users (user_id, activated, fingerprint, revoked, revoked_by) VALUES (?, 0, '', ?, ?)",
                        (uid, time.time(), admin_id))  # chưa từng kích hoạt: chặn trước
            n = 1
    return n == 1


def mo_lai(uid: int) -> bool:
    """Admin bỏ thu hồi: người đó nhập key lại được (không tự mở Claude ngay)."""
    with db.ket_noi() as con:
        return con.execute("DELETE FROM claude_key_users WHERE user_id = ? AND revoked IS NOT NULL", (uid,)).rowcount == 1


def danh_sach() -> list[dict]:
    """Ai đã mở Claude bằng key (kể cả key cũ / bị thu hồi) + ai nhập sai key trong 30 ngày: kích hoạt lúc nào,
    số bài Claude hôm nay / tổng, số lần nhập sai, đang bị khoá không."""
    hom_nay, bay_gio = _hom_nay(), time.time()
    with db.ket_noi() as con:
        mo = {r["user_id"]: r for r in con.execute("SELECT * FROM claude_key_users")}
        sai = {r["user_id"]: (r["n"], r["n24"], r["cuoi"]) for r in con.execute(
            "SELECT user_id, COUNT(*) AS n, SUM(ts >= ?) AS n24, MAX(ts) AS cuoi FROM claude_key_fails WHERE user_id IS NOT NULL "
            "GROUP BY user_id", (bay_gio - 86400,))}
        dung = {r["user_id"]: (r["hom_nay"], r["tong"]) for r in con.execute(
            "SELECT user_id, SUM(day = ?) AS hom_nay, COUNT(*) AS tong FROM claude_key_uses WHERE done = 1 GROUP BY user_id", (hom_nay,))}
        ids = sorted(set(mo) | set(sai))
        nguoi = {r["id"]: r for r in con.execute(
            f"SELECT id, name, email FROM users WHERE id IN ({','.join('?' * len(ids))})", ids)} if ids else {}
    kq = []
    for uid in ids:
        r, n = mo.get(uid), nguoi.get(uid)
        tt = _trang_thai_dong(r)
        s = sai.get(uid, (0, 0, None))
        d = dung.get(uid, (0, 0))
        kq.append({"id": uid, "ten": n["name"] if n else "", "email": n["email"] if n else "(đã xoá)", "trang_thai": tt,
                   "ten_trang_thai": TEN_TRANG_THAI[tt], "kich_hoat_luc": _gio(r["activated"]) if r is not None else "",
                   "thu_hoi_luc": _gio(r["revoked"]) if r is not None else "", "hom_nay": int(d[0] or 0), "tong": int(d[1] or 0),
                   "gioi_han": gioi_han_ngay(), "sai_30_ngay": int(s[0] or 0), "sai_24h": int(s[1] or 0), "sai_cuoi": _gio(s[2]),
                   "khoa_nhap_giay": int(khoa_nhap(uid))})
    thu_tu = {"da_mo": 0, "key_cu": 1, "bi_thu_hoi": 2, "tat": 3, "chua_mo": 4}
    return sorted(kq, key=lambda x: (thu_tu[x["trang_thai"]], -x["tong"], x["id"]))


def tom_tat() -> dict:
    """Thông tin cấu hình cho admin (KHÔNG có key): bật chưa, giới hạn/ngày."""
    return {"bat": bat(), "gioi_han_ngay": gioi_han_ngay()}


def don_khi_khoi_dong() -> None:
    """Khởi động: bỏ các lượt đang giữ (app tắt giữa chừng); ghi log số tài khoản còn dùng key cũ (đã mất quyền) – không ghi key."""
    try:
        with db.ket_noi() as con:
            con.execute("DELETE FROM claude_key_uses WHERE done = 0")
            ds = con.execute("SELECT fingerprint FROM claude_key_users WHERE revoked IS NULL").fetchall()
        if not bat():
            log.info("Key Claude: tắt (chưa đặt CLAUDE_ACCESS_KEY)")
            return
        hien_tai = van_tay_hien_tai()
        cu = sum(1 for r in ds if not hmac.compare_digest(r[0], hien_tai))
        log.info("Key Claude: bật, tối đa %d bài/người/ngày; %d tài khoản đang mở Claude%s", gioi_han_ngay(), len(ds) - cu,
                 f"; {cu} tài khoản kích hoạt bằng key cũ đã mất quyền" if cu else "")
    except Exception:  # noqa: BLE001
        log.exception("Không dọn được lượt Key Claude đang giữ")


# ---------------------------------------------------------------- quyền dùng Claude cho MỘT yêu cầu (giữ / chốt / trả lượt ngày)
@dataclass
class QuyenKey:
    """Cùng giao diện với ma_claude.QuyenClaude. moi_muc: bật "Dùng Claude" thì giải bằng Claude ở MỌI mức độ đề."""

    user_id: int
    loai: str = ""
    job_id: str = ""
    la_admin: bool = False
    ma_id: int | None = None
    moi_muc: bool = True
    nguon: str = "key"
    ly_do_het: str = "hôm nay đã hết lượt Claude"
    _giu_id: int | None = field(default=None, repr=False)
    _khoa: threading.Lock = field(default_factory=threading.Lock, repr=False)

    def giu(self) -> bool:
        """Giữ 1 lượt hôm nay (một lần cho cả yêu cầu; nguyên tử – không vượt CLAUDE_DAILY_LIMIT)."""
        with self._khoa:
            if self._giu_id is not None:
                return True
            with db.ket_noi() as con:
                cur = con.execute(
                    "INSERT INTO claude_key_uses (user_id, day, ts, kind, job_id, done) SELECT ?, ?, ?, ?, ?, 0 "
                    "WHERE (SELECT COUNT(*) FROM claude_key_uses WHERE user_id = ? AND day = ?) < ?",
                    (self.user_id, _hom_nay(), time.time(), self.loai, self.job_id, self.user_id, _hom_nay(), gioi_han_ngay()))
                self._giu_id = cur.lastrowid if cur.rowcount == 1 else None
            return self._giu_id is not None

    def chot(self) -> bool:
        with self._khoa:
            if self._giu_id is None:
                return False
            with db.ket_noi() as con:
                con.execute("UPDATE claude_key_uses SET done = 1, job_id = ? WHERE id = ?", (self.job_id, self._giu_id))
            self._giu_id = None
            return True

    def tra(self) -> None:
        with self._khoa:
            if self._giu_id is None:
                return
            with db.ket_noi() as con:
                con.execute("DELETE FROM claude_key_uses WHERE id = ? AND done = 0", (self._giu_id,))
            self._giu_id = None


def chon_quyen(u: NguoiDung) -> tuple[object | None, str]:
    """Người dùng XIN dùng Claude → (quyền | None, thông báo). Thứ tự: API key Claude RIÊNG (admin đã bật cho tài khoản + người dùng bật
    "Dùng key của tôi"; không tốn tiền máy chủ) → admin (key máy chủ) → Key Claude chung (còn lượt hôm nay) → mã Claude.
    Không có quyền → None + lý do: yêu cầu vẫn chạy, bằng Gemini (backend từ chối Claude, không tin frontend)."""
    # key riêng (admin đã bật + người dùng bật "Dùng key của tôi") đứng TRƯỚC – kể cả admin: không tốn tiền máy chủ
    if key_rieng := claude_rieng.lay_key(u):
        return claude_rieng.QuyenRieng(api_key=key_rieng), ""
    if tai_khoan.la_chu_app(u):
        return ma_claude.QuyenClaude(la_admin=True, moi_muc=True, nguon="admin"), ""
    tt = _trang_thai_dong(_dong(u.id))
    q_ma = ma_claude.quyen(u)
    if tt == "da_mo":
        if da_dung_hom_nay(u.id) < gioi_han_ngay():
            return QuyenKey(user_id=u.id), ""
        if q_ma is not None:
            return q_ma, f"Hôm nay đã hết lượt Claude của Key Claude ({gioi_han_ngay()} bài/ngày) – dùng mã Claude (chỉ cho bài HSG)."
        return None, "Hôm nay đã hết lượt Claude, đang dùng Gemini."
    if q_ma is not None:
        return q_ma, ""
    if tt == "key_cu":
        return None, "Key Claude đã được đổi – hãy nhập key mới trong Cài đặt → Key Claude. Bài này dùng Gemini."
    if tt == "bi_thu_hoi":
        return None, "Admin đã thu hồi quyền Claude của tài khoản này – bài này dùng Gemini."
    tt_ma = ma_claude.trang_thai(u)
    if tt_ma.get("co_ma"):
        return None, f"Mã Claude {tt_ma.get('ten_trang_thai', 'không dùng được')} – không dùng Claude, bài giải bằng Gemini."
    log.info("Từ chối Claude cho tài khoản #%s (chưa mở Claude) → Gemini", u.id)
    return None, "Tài khoản chưa mở Claude (chưa nhập Key Claude hoặc mã Claude) – từ chối dùng Claude, bài giải bằng Gemini."
