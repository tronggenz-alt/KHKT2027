"""Tài khoản giáo viên, học sinh: băm mật khẩu, phiên đăng nhập, lớp, giới hạn số lần sai.

Mật khẩu băm bằng Argon2id, không lưu dạng thô. Mã phiên là chuỗi ngẫu nhiên; CSDL chỉ giữ bản băm SHA-256 của mã,
nên lộ file CSDL cũng không dùng lại được phiên đang mở. Mỗi quyền kiểm tra ở server/lab/dang_nhap.py.
"""

from __future__ import annotations

import hashlib
import re
import secrets
import sqlite3
import time

from argon2 import PasswordHasher
from argon2.exceptions import Argon2Error, InvalidHashError

from . import db

VAI_TRO = ("giao_vien", "hoc_sinh")
HAN_PHIEN = {"web": 12 * 3600, "ung_dung": 14 * 86400}  # web tính từ lúc đăng nhập, ứng dụng từ lần dùng cuối
SAI_TOI_DA = 5
KHOA_GIAY = 300
_TEN_HOP_LE = re.compile(r"[a-z0-9._-]{3,32}")
_KY_TU_MA_LOP = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"  # bỏ O, 0, I, 1 để đọc cho cả lớp không nhầm
_KY_TU_MAT_KHAU_TAM = "abcdefghjkmnpqrstuvwxyz23456789"  # chữ thường bỏ thêm l, dễ nhầm với số 1

_bam = PasswordHasher()
_bam_gia: str | None = None


class LoiTaiKhoan(ValueError):
    """Dữ liệu tài khoản không hợp lệ; nội dung là lời báo tiếng Việt cho người dùng."""


class TenDaCo(LoiTaiKhoan):
    """Tên đăng nhập đã có người dùng."""


def chuan_ten(ten: str) -> str:
    return (ten or "").strip().lower()


def kiem_tra_mat_khau(mat_khau: str, ten: str = "") -> None:
    if len(mat_khau) < 8:
        raise LoiTaiKhoan("Mật khẩu cần ít nhất 8 ký tự")
    if len(mat_khau) > 128:
        raise LoiTaiKhoan("Mật khẩu dài quá 128 ký tự")
    if ten and mat_khau.lower() == ten:
        raise LoiTaiKhoan("Mật khẩu không được trùng tên đăng nhập")


def tao_nguoi_dung(ten: str, ho_ten: str, vai_tro: str, mat_khau: str, phai_doi: bool = False) -> dict:
    ten, ho_ten = chuan_ten(ten), (ho_ten or "").strip()
    if vai_tro not in VAI_TRO:
        raise LoiTaiKhoan("Vai trò không hợp lệ")
    if not _TEN_HOP_LE.fullmatch(ten):
        raise LoiTaiKhoan("Tên đăng nhập gồm 3 đến 32 ký tự: chữ không dấu, số, dấu chấm, gạch nối, gạch dưới")
    if not 2 <= len(ho_ten) <= 80:
        raise LoiTaiKhoan("Họ tên cần từ 2 đến 80 ký tự")
    kiem_tra_mat_khau(mat_khau, ten)
    try:
        cur = db.execute("INSERT INTO nguoi_dung (ten_dang_nhap, ho_ten, vai_tro, mat_khau_bam, phai_doi_mat_khau, tao_luc) VALUES (?,?,?,?,?,?)",
                         (ten, ho_ten, vai_tro, _bam.hash(mat_khau), int(phai_doi), time.time()))
    except sqlite3.IntegrityError:
        raise TenDaCo("Tên đăng nhập này đã có người dùng") from None
    return tim_theo_id(cur.lastrowid)


def tim_theo_ten(ten: str) -> dict | None:
    rows = db.query("SELECT * FROM nguoi_dung WHERE ten_dang_nhap=?", (chuan_ten(ten),))
    return rows[0] if rows else None


def tim_theo_id(nguoi_dung_id: int) -> dict | None:
    rows = db.query("SELECT * FROM nguoi_dung WHERE id=?", (nguoi_dung_id,))
    return rows[0] if rows else None


def _dung_mat_khau(bam: str, mat_khau: str) -> bool:
    try:
        return _bam.verify(bam, mat_khau)
    except (Argon2Error, InvalidHashError):
        return False


def xac_thuc(ten: str, mat_khau: str) -> dict | None:
    """Trả về người dùng nếu đúng. Tên không tồn tại vẫn băm một lượt để thời gian trả lời không lộ tên nào có thật."""
    global _bam_gia
    nd = tim_theo_ten(ten)
    if nd is None:
        _bam_gia = _bam_gia or _bam.hash(secrets.token_urlsafe(16))
        _dung_mat_khau(_bam_gia, mat_khau)
        return None
    if not _dung_mat_khau(nd["mat_khau_bam"], mat_khau):
        return None
    if _bam.check_needs_rehash(nd["mat_khau_bam"]):
        db.execute("UPDATE nguoi_dung SET mat_khau_bam=? WHERE id=?", (_bam.hash(mat_khau), nd["id"]))
    return nd


def dat_mat_khau(nguoi_dung_id: int, mat_khau: str, phai_doi: bool = False) -> None:
    db.execute("UPDATE nguoi_dung SET mat_khau_bam=?, phai_doi_mat_khau=? WHERE id=?", (_bam.hash(mat_khau), int(phai_doi), nguoi_dung_id))


def dung_mat_khau_cua(nd: dict, mat_khau: str) -> bool:
    return _dung_mat_khau(nd["mat_khau_bam"], mat_khau)


def cong_khai(nd: dict) -> dict:
    """Phần được gửi ra ngoài; không bao giờ có mat_khau_bam."""
    if nd["vai_tro"] == "giao_vien":
        lop = db.query("SELECT id, ten, nam_hoc FROM lop WHERE giao_vien_id=? ORDER BY id", (nd["id"],))
    else:
        lop = db.query("SELECT l.id, l.ten, l.nam_hoc FROM lop l JOIN thanh_vien_lop t ON t.lop_id=l.id WHERE t.hoc_sinh_id=? ORDER BY l.id", (nd["id"],))
    return {"id": nd["id"], "ten_dang_nhap": nd["ten_dang_nhap"], "ho_ten": nd["ho_ten"], "vai_tro": nd["vai_tro"],
            "phai_doi_mat_khau": bool(nd["phai_doi_mat_khau"]), "lop": lop}


# ---------------------------------------------------------------------------
# Phiên đăng nhập
# ---------------------------------------------------------------------------

def _bam_ma(ma: str) -> str:
    return hashlib.sha256(ma.encode()).hexdigest()


def tao_phien(nguoi_dung_id: int, kieu: str = "web") -> dict:
    now = time.time()
    db.execute("DELETE FROM phien WHERE het_han<?", (now,))
    ma = secrets.token_urlsafe(32)
    het_han = now + HAN_PHIEN[kieu]
    db.execute("INSERT INTO phien (ma_bam, nguoi_dung_id, kieu, tao_luc, het_han) VALUES (?,?,?,?,?)", (_bam_ma(ma), nguoi_dung_id, kieu, now, het_han))
    return {"ma_phien": ma, "het_han": het_han}


def doc_phien(ma: str) -> dict | None:
    """Người dùng của mã phiên còn hạn. Phiên ứng dụng được gia hạn khi dùng (tối đa 5 phút ghi một lần)."""
    if not ma:
        return None
    bam, now = _bam_ma(ma), time.time()
    rows = db.query("SELECT * FROM phien WHERE ma_bam=? AND het_han>?", (bam, now))
    if not rows:
        return None
    if rows[0]["kieu"] == "ung_dung" and now + HAN_PHIEN["ung_dung"] - rows[0]["het_han"] > 300:
        db.execute("UPDATE phien SET het_han=? WHERE ma_bam=?", (now + HAN_PHIEN["ung_dung"], bam))
    return tim_theo_id(rows[0]["nguoi_dung_id"])


def huy_phien(ma: str) -> None:
    db.execute("DELETE FROM phien WHERE ma_bam=?", (_bam_ma(ma),))


def huy_phien_khac(nguoi_dung_id: int, giu_ma: str = "") -> None:
    db.execute("DELETE FROM phien WHERE nguoi_dung_id=? AND ma_bam<>?", (nguoi_dung_id, _bam_ma(giu_ma) if giu_ma else ""))


# ---------------------------------------------------------------------------
# Giới hạn số lần sai (theo cặp tên đăng nhập + địa chỉ IP, hoặc mã lớp + IP)
# ---------------------------------------------------------------------------

def _khoa_sai(muc: str, ip: str) -> str:
    return hashlib.sha256(f"{muc}|{ip}".encode()).hexdigest()  # không lưu chữ người dùng gõ: có khi là mật khẩu gõ nhầm ô


def con_bi_khoa(muc: str, ip: str) -> int:
    """Số giây còn phải chờ; 0 là được thử."""
    rows = db.query("SELECT khoa_den FROM gioi_han_sai WHERE khoa=?", (_khoa_sai(muc, ip),))
    return max(0, int(rows[0]["khoa_den"] - time.time()) + 1) if rows and rows[0]["khoa_den"] > time.time() else 0


def ghi_sai(muc: str, ip: str, toi_da: int = SAI_TOI_DA) -> None:
    now, khoa = time.time(), _khoa_sai(muc, ip)
    db.execute("DELETE FROM gioi_han_sai WHERE lan_cuoi<? AND khoa_den<?", (now - 86400, now))
    rows = db.query("SELECT so_lan, lan_cuoi FROM gioi_han_sai WHERE khoa=?", (khoa,))
    so_lan = rows[0]["so_lan"] + 1 if rows and now - rows[0]["lan_cuoi"] < KHOA_GIAY else 1
    khoa_den = now + KHOA_GIAY if so_lan >= toi_da else 0
    db.execute("INSERT OR REPLACE INTO gioi_han_sai (khoa, so_lan, lan_cuoi, khoa_den) VALUES (?,?,?,?)", (khoa, 0 if khoa_den else so_lan, now, khoa_den))


def xoa_sai(muc: str, ip: str) -> None:
    db.execute("DELETE FROM gioi_han_sai WHERE khoa=?", (_khoa_sai(muc, ip),))


# ---------------------------------------------------------------------------
# Lớp
# ---------------------------------------------------------------------------

def tao_lop(ten: str, nam_hoc: str, giao_vien_id: int) -> dict:
    ten = (ten or "").strip()
    if not 1 <= len(ten) <= 60:
        raise LoiTaiKhoan("Tên lớp cần từ 1 đến 60 ký tự")
    while True:
        ma = "".join(secrets.choice(_KY_TU_MA_LOP) for _ in range(6))
        if not db.query("SELECT 1 FROM lop WHERE ma_lop=?", (ma,)):
            break
    cur = db.execute("INSERT INTO lop (ten, nam_hoc, giao_vien_id, ma_lop, tao_luc) VALUES (?,?,?,?,?)", (ten, (nam_hoc or "").strip(), giao_vien_id, ma, time.time()))
    return db.query("SELECT * FROM lop WHERE id=?", (cur.lastrowid,))[0]


def them_vao_lop(lop_id: int, hoc_sinh_id: int) -> None:
    db.execute("INSERT OR IGNORE INTO thanh_vien_lop (lop_id, hoc_sinh_id, them_luc) VALUES (?,?,?)", (lop_id, hoc_sinh_id, time.time()))


def tim_lop_theo_ma(ma_lop: str) -> dict | None:
    """Lớp đang mở nhận học sinh theo mã; lớp đã đóng mã coi như không có."""
    rows = db.query("SELECT * FROM lop WHERE ma_lop=? AND ma_lop_mo=1", ((ma_lop or "").strip().upper(),))
    return rows[0] if rows else None


def sinh_mat_khau_tam() -> str:
    """Mật khẩu tạm giáo viên phát cho học sinh: dễ đọc, dễ gõ; học sinh phải đổi ở lần đăng nhập đầu."""
    return "".join(secrets.choice(_KY_TU_MAT_KHAU_TAM) for _ in range(10))


def lop_cua_giao_vien(lop_id: int, giao_vien_id: int) -> dict | None:
    rows = db.query("SELECT * FROM lop WHERE id=? AND giao_vien_id=?", (lop_id, giao_vien_id))
    return rows[0] if rows else None


def ds_lop_cua_giao_vien(giao_vien_id: int) -> list[dict]:
    return db.query("""SELECT l.id, l.ten, l.nam_hoc, l.ma_lop, l.ma_lop_mo, COUNT(t.hoc_sinh_id) AS so_hoc_sinh
                       FROM lop l LEFT JOIN thanh_vien_lop t ON t.lop_id=l.id
                       WHERE l.giao_vien_id=? GROUP BY l.id ORDER BY l.id""", (giao_vien_id,))


def sua_lop(lop_id: int, ten: str | None = None, nam_hoc: str | None = None, ma_lop_mo: bool | None = None) -> None:
    if ten is not None:
        ten = ten.strip()
        if not 1 <= len(ten) <= 60:
            raise LoiTaiKhoan("Tên lớp cần từ 1 đến 60 ký tự")
        db.execute("UPDATE lop SET ten=? WHERE id=?", (ten, lop_id))
    if nam_hoc is not None:
        db.execute("UPDATE lop SET nam_hoc=? WHERE id=?", (nam_hoc.strip(), lop_id))
    if ma_lop_mo is not None:
        db.execute("UPDATE lop SET ma_lop_mo=? WHERE id=?", (int(ma_lop_mo), lop_id))


def hoc_sinh_cua_lop(lop_id: int) -> list[dict]:
    return db.query("""SELECT n.id, n.ten_dang_nhap, n.ho_ten, n.phai_doi_mat_khau FROM nguoi_dung n
                       JOIN thanh_vien_lop t ON t.hoc_sinh_id=n.id WHERE t.lop_id=? ORDER BY n.ho_ten""", (lop_id,))


def co_trong_lop(lop_id: int, hoc_sinh_id: int) -> bool:
    return bool(db.query("SELECT 1 FROM thanh_vien_lop WHERE lop_id=? AND hoc_sinh_id=?", (lop_id, hoc_sinh_id)))


def go_khoi_lop(lop_id: int, hoc_sinh_id: int) -> None:
    db.execute("DELETE FROM thanh_vien_lop WHERE lop_id=? AND hoc_sinh_id=?", (lop_id, hoc_sinh_id))


def hoc_sinh_cua_giao_vien(giao_vien_id: int) -> set[int]:
    """Mọi học sinh trong các lớp giáo viên này dạy: phạm vi kết quả giáo viên được xem, được chấm."""
    return {r["hoc_sinh_id"] for r in db.query("SELECT t.hoc_sinh_id FROM thanh_vien_lop t JOIN lop l ON l.id=t.lop_id WHERE l.giao_vien_id=?", (giao_vien_id,))}
