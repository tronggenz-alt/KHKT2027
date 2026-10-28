"""Mã giao diện (nút "+K" trên trang chủ): ADMIN cấp mã → người dùng nhập → đổi theme (vd "Hoa anh đào").

- Mã KHÔNG nằm trong code frontend. Kiểm tra mã bằng HMAC-SHA256 (khoá suy từ khoá chính của app, như mã Claude); CSDL lưu thêm
  bản MÃ HOÁ (Fernet) của mã gốc để ADMIN xem lại / chép đưa cho người khác ở /admin → Mã giao diện (không bao giờ trả cho người thường,
  không ghi nhật ký). Mỗi mã gắn với một theme_id, có thể đặt hạn dùng và số lượt tối đa.
- Một tài khoản nhập lại cùng một mã không tốn thêm lượt. Nhập sai 5 lần / giờ (theo tài khoản; 15 theo IP) → khoá ô nhập 1 giờ.
- Mã mẫu SAKURA2026 (theme "sakura") được tạo MỘT lần lúc app khởi động (tao_ma_mau) để thử – admin thu hồi được như mã khác.
Danh sách theme hợp lệ: THEMES (giao diện tương ứng: frontend/themes/<id>.js).
"""
from __future__ import annotations

import base64
import hashlib
import hmac
import re
import secrets
import sqlite3
import time
from datetime import date

from . import db
from cryptography.fernet import Fernet, InvalidToken

from .bao_mat import LoiBaoMat, _suy_khoa
from .db import NguoiDung

THEMES = {"default": "Mặc định", "sakura": "Hoa anh đào"}
TIEN_TO = "GD"
BANG_CHU = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"
SAI_TOI_DA, SAI_TOI_DA_IP, CUA_SO, KHOA_GIAY = 5, 15, 3600, 3600
MA_MAU, THEME_MAU = "SAKURA2026", "sakura"
TEN_TRANG_THAI = {"con_hieu_luc": "Còn hiệu lực", "het_han": "Hết hạn", "het_luot": "Hết lượt", "thu_hoi": "Đã thu hồi"}


class LoiMaGiaoDien(Exception):
    def __init__(self, thong_diep: str, ma: str, status: int = 400, cho: int | None = None):
        super().__init__(thong_diep)
        self.ma, self.status, self.cho = ma, status, cho


def chuan_hoa(ma: str) -> str:
    """' sakura-2026 ' → 'SAKURA2026' (bỏ khoảng trắng / gạch, không phân biệt hoa thường)."""
    return re.sub(r"[\s\-_.]+", "", (ma or "").upper())


def bam(ma: str) -> str:
    return hmac.new(_suy_khoa(b"ma-giao-dien/v1"), chuan_hoa(ma).encode(), hashlib.sha256).hexdigest()


def _fernet() -> Fernet:
    return Fernet(base64.urlsafe_b64encode(_suy_khoa(b"ma-giao-dien-goc/v1")))


def ma_hoa_goc(ma: str) -> str:
    try:
        return _fernet().encrypt(ma.encode("utf-8")).decode("ascii")
    except LoiBaoMat:
        return ""  # chưa có khoá chính: vẫn tạo được mã, chỉ là admin không xem lại được


def giai_ma_goc(ban_ma: str) -> str:
    if not ban_ma:
        return ""
    try:
        return _fernet().decrypt(ban_ma.encode("ascii")).decode("utf-8")
    except (InvalidToken, ValueError, LoiBaoMat):
        return ""  # đổi khoá chính → không giải được (mã vẫn dùng được, admin chỉ thấy gợi ý)


def _goi_y(ma: str) -> str:
    t = chuan_hoa(ma)
    return f"{t[:3]}…{t[-2:]}" if len(t) > 6 else f"{t[:2]}…"


def sinh_ma() -> str:
    t = "".join(secrets.choice(BANG_CHU) for _ in range(8))
    return f"{TIEN_TO}-{t[:4]}-{t[4:]}"


# ---------------------------------------------------------------- admin: tạo / xem / thu hồi
def tao_ma(theme_id: str, ma: str = "", so_luot: int | None = None, het_han: str = "", ghi_chu: str = "",
           admin_id: int | None = None) -> tuple[str, int]:
    """→ (mã gốc – CHỈ trả lần này, id). ma trống → sinh ngẫu nhiên GD-XXXX-XXXX. het_han = 'YYYY-MM-DD' (dùng được hết ngày đó)."""
    if theme_id not in THEMES or theme_id == "default":
        raise ValueError("Theme không hợp lệ.")
    ma = (ma or "").strip() or sinh_ma()
    if not re.fullmatch(r"[A-Za-z0-9\-_. ]{4,40}", ma) or len(chuan_hoa(ma)) < 4:
        raise ValueError("Mã chỉ gồm chữ, số, dấu gạch – từ 4 đến 40 ký tự.")
    if so_luot is not None and not 1 <= so_luot <= 1_000_000:
        raise ValueError("Số lượt phải từ 1 đến 1.000.000 (để trống = không giới hạn).")
    if het_han:
        try:
            date.fromisoformat(het_han)
        except ValueError as e:
            raise ValueError("Hạn dùng phải có dạng YYYY-MM-DD.") from e
    try:
        with db.ket_noi() as con:
            ma_id = con.execute("INSERT INTO theme_codes (code_hash, code_enc, hint, theme_id, max_uses, expires, note, created, admin_id) "
                                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)", (bam(ma), ma_hoa_goc(ma), _goi_y(ma), theme_id, so_luot, het_han or None,
                                                                      (ghi_chu or "").strip()[:200], time.time(), admin_id)).lastrowid
    except sqlite3.IntegrityError as e:
        raise ValueError("Mã này đã tồn tại – hãy chọn mã khác.") from e
    return ma, ma_id


def _trang_thai(r) -> str:
    if r["revoked_at"]:
        return "thu_hoi"
    if r["expires"] and r["expires"] < date.today().isoformat():
        return "het_han"
    if r["max_uses"] is not None and r["used"] >= r["max_uses"]:
        return "het_luot"
    return "con_hieu_luc"


def danh_sach() -> list[dict]:
    with db.ket_noi() as con:
        rows = con.execute("SELECT c.*, u.email AS admin_email FROM theme_codes c LEFT JOIN users u ON u.id = c.admin_id "
                           "ORDER BY c.id DESC").fetchall()
    kq = []
    for r in rows:
        tt = _trang_thai(r)
        kq.append({"id": r["id"], "ma": giai_ma_goc(r["code_enc"]), "goi_y": r["hint"], "theme_id": r["theme_id"], "ten_theme": THEMES.get(r["theme_id"], r["theme_id"]),
                   "da_dung": r["used"], "toi_da": r["max_uses"], "het_han": r["expires"] or "", "ghi_chu": r["note"],
                   "trang_thai": tt, "ten_trang_thai": TEN_TRANG_THAI[tt], "admin": r["admin_email"] or "(dòng lệnh / mẫu)",
                   "tao_luc": time.strftime("%Y-%m-%d %H:%M", time.localtime(r["created"]))})
    return kq


def thu_hoi(ma_id: int) -> bool:
    with db.ket_noi() as con:
        return con.execute("UPDATE theme_codes SET revoked_at = ? WHERE id = ? AND revoked_at IS NULL", (time.time(), ma_id)).rowcount == 1


def tim(chuoi: str) -> dict | None:
    """'#3' / '3' / mã gốc → dòng mã (cho dòng lệnh)."""
    s = (chuoi or "").strip().lstrip("#")
    with db.ket_noi() as con:
        r = (con.execute("SELECT * FROM theme_codes WHERE id = ?", (int(s),)).fetchone() if s.isdigit()
             else con.execute("SELECT * FROM theme_codes WHERE code_hash = ?", (bam(s),)).fetchone())
    return dict(r) if r else None


def tao_ma_mau() -> bool:
    """Tạo mã mẫu SAKURA2026 MỘT lần (cờ app_settings) – để chủ app thử theme Hoa anh đào. True nếu vừa tạo."""
    if db.cai_dat("ma_giao_dien_mau", "") == "1":
        with db.ket_noi() as con:  # mã mẫu tạo trước khi có cột code_enc → điền để admin xem / chép được
            con.execute("UPDATE theme_codes SET code_enc = ? WHERE code_hash = ? AND code_enc = ''", (ma_hoa_goc(MA_MAU), bam(MA_MAU)))
        return False
    try:
        tao_ma(THEME_MAU, MA_MAU, so_luot=None, het_han="", ghi_chu="Mã mẫu (seed) – thu hồi khi không cần")
    except ValueError:
        pass  # đã có (vd tạo tay trước đó)
    db.dat_cai_dat("ma_giao_dien_mau", "1")
    return True


# ---------------------------------------------------------------- người dùng nhập mã (POST /api/theme/redeem)
def _khoa_den(moc: list[float], toi_da: int) -> float:
    moc, den = sorted(moc), 0.0
    for i in range(toi_da - 1, len(moc)):
        if moc[i] - moc[i - toi_da + 1] <= CUA_SO:
            den = max(den, moc[i] + KHOA_GIAY)
    return den


def khoa_nhap(uid: int, ip: str = "") -> float:
    bay_gio = time.time()
    with db.ket_noi() as con:
        tk = [r[0] for r in con.execute("SELECT ts FROM theme_code_fails WHERE user_id = ? AND ts >= ?", (uid, bay_gio - CUA_SO - KHOA_GIAY))]
        theo_ip = [r[0] for r in con.execute("SELECT ts FROM theme_code_fails WHERE ip = ? AND ip <> '' AND ts >= ?",
                                             (ip, bay_gio - CUA_SO - KHOA_GIAY))] if ip else []
    return max(0.0, max(_khoa_den(tk, SAI_TOI_DA), _khoa_den(theo_ip, SAI_TOI_DA_IP)) - bay_gio)


def doi_ma(u: NguoiDung, ma: str, ip: str = "") -> dict:
    """Mã hợp lệ → {theme_id, ten_theme}. Sai / hết hạn / hết lượt / thu hồi → LoiMaGiaoDien (thông báo tiếng Việt)."""
    cho = khoa_nhap(u.id, ip)
    if cho > 0:
        raise LoiMaGiaoDien(f"Nhập sai quá nhiều lần – thử lại sau {int(cho // 60) + 1} phút.", "bi_khoa", 429, int(cho) + 1)
    if not chuan_hoa(ma):
        raise LoiMaGiaoDien("Hãy nhập mã giao diện.", "trong")
    with db.ket_noi() as con:
        r = con.execute("SELECT * FROM theme_codes WHERE code_hash = ?", (bam(ma),)).fetchone()
        if r is None:
            con.execute("INSERT INTO theme_code_fails (user_id, ip, ts) VALUES (?, ?, ?)", (u.id, ip or "", time.time()))
            con.execute("DELETE FROM theme_code_fails WHERE ts < ?", (time.time() - 2 * 86400,))
            loi = LoiMaGiaoDien("Mã không đúng – kiểm tra lại (không phân biệt hoa thường).", "sai")
        else:
            da_dung = con.execute("SELECT 1 FROM theme_code_uses WHERE code_id = ? AND user_id = ?", (r["id"], u.id)).fetchone() is not None
            tt = _trang_thai(r)
            if tt == "thu_hoi":
                loi = LoiMaGiaoDien("Mã này đã bị admin thu hồi.", "thu_hoi")
            elif tt == "het_han":
                loi = LoiMaGiaoDien(f"Mã này đã hết hạn (hạn dùng {r['expires']}).", "het_han")
            elif tt == "het_luot" and not da_dung:
                loi = LoiMaGiaoDien("Mã này đã hết lượt dùng.", "het_luot")
            else:
                loi = None
                if not da_dung:  # mỗi tài khoản chỉ tính 1 lượt cho mỗi mã
                    con.execute("INSERT INTO theme_code_uses (code_id, user_id, ts, ip) VALUES (?, ?, ?, ?)", (r["id"], u.id, time.time(), ip or ""))
                    con.execute("UPDATE theme_codes SET used = used + 1 WHERE id = ?", (r["id"],))
    if loi is not None:
        raise loi
    return {"theme_id": r["theme_id"], "ten_theme": THEMES.get(r["theme_id"], r["theme_id"])}
