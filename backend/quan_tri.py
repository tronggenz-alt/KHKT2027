"""Quản trị tài khoản – chạy trên máy chạy app (người có quyền vào máy = chủ app), không có API web.

    .venv/bin/python -m backend.quan_tri ds
    .venv/bin/python -m backend.quan_tri tao-admin <email> [--ten "Tên"] [--mat-khau-ngau-nhien]
    .venv/bin/python -m backend.quan_tri cap-admin <email> | bo-admin <email>
    .venv/bin/python -m backend.quan_tri tao-ki-cuu <email> [--ten "Tên"] [--mat-khau-ngau-nhien]
    .venv/bin/python -m backend.quan_tri cap-ki-cuu <email> | bo-ki-cuu <email>
    .venv/bin/python -m backend.quan_tri dat-mat-khau <email> [--mat-khau-ngau-nhien]
Không có tham số → menu (quan-tri.command). Mật khẩu nhập ẩn (getpass); --mat-khau-ngau-nhien in mật khẩu mới MỘT lần.
"""
from __future__ import annotations

import argparse
import getpass
import secrets
import sys

from . import db
from .bao_mat import MAT_KHAU_TOI_THIEU, bam_mat_khau


class LoiQuanTri(Exception):
    pass


def _tim(email: str) -> list[db.NguoiDung]:
    email = email.strip().lower()
    return [u for u in db.danh_sach_nguoi_dung() if (u.email or "").lower() == email]


def _mot(email: str, chi_email: bool = False) -> db.NguoiDung:
    ds = [u for u in _tim(email) if not chi_email or u.dang_nhap_bang == "email"]
    if not ds:
        raise LoiQuanTri(f"Không có tài khoản {'email + mật khẩu ' if chi_email else ''}nào với email {email}.")
    if len(ds) > 1:  # cùng email có cả tài khoản Google và tài khoản mật khẩu → cấp cho cả hai là đúng ý chủ app
        return ds
    return ds[0]


def _nhap_mat_khau(ngau_nhien: bool) -> tuple[str, bool]:
    if ngau_nhien:
        return secrets.token_urlsafe(12), True
    mk = getpass.getpass(f"Mật khẩu (≥ {MAT_KHAU_TOI_THIEU} ký tự, không hiện khi gõ): ")
    if len(mk) < MAT_KHAU_TOI_THIEU:
        raise LoiQuanTri(f"Mật khẩu cần ít nhất {MAT_KHAU_TOI_THIEU} ký tự.")
    if getpass.getpass("Nhập lại mật khẩu: ") != mk:
        raise LoiQuanTri("Hai mật khẩu chưa khớp.")
    return mk, False


def _vai_tro(u: db.NguoiDung) -> str:
    return "admin" if u.la_admin or u.cuc_bo else "kì cựu" if u.la_ki_cuu else ""


def danh_sach() -> str:
    dong = [f"{'ID':>3}  {'Đăng nhập':<9} {'Vai trò':<7} {'Dùng thử':<8} {'Key':<4} Email / tên"]
    for u in db.danh_sach_nguoi_dung():
        dong.append(f"{u.id:>3}  {u.dang_nhap_bang:<9} {_vai_tro(u):<7} {u.luot_da_dung:<8} "
                    f"{'có' if u.co_key else '':<4} {u.email or '-'} ({u.ten})")
    return "\n".join(dong)


def tao_admin(email: str, ten: str = "", mat_khau: str | None = None, ki_cuu: bool = False) -> tuple[db.NguoiDung, bool]:
    """Tạo tài khoản email + mật khẩu có quyền admin – hoặc vai trò kì cựu nếu ki_cuu=True (đã có thì chỉ cấp quyền).
    Trả (người dùng, mới tạo?)."""
    dat = db.dat_ki_cuu if ki_cuu else db.dat_admin
    email = email.strip().lower()
    if "@" not in email:
        raise LoiQuanTri("Email không hợp lệ.")
    cu = db.lay_theo_sub(db.SUB_EMAIL + email)
    if cu is not None:
        dat(cu.id, True)
        return db.lay_nguoi_dung(cu.id), False
    if not mat_khau or len(mat_khau) < MAT_KHAU_TOI_THIEU:
        raise LoiQuanTri(f"Mật khẩu cần ít nhất {MAT_KHAU_TOI_THIEU} ký tự.")
    u = db.tao_tai_khoan_email(email, ten.strip() or email.split("@")[0], bam_mat_khau(mat_khau))
    dat(u.id, True)
    return db.lay_nguoi_dung(u.id), True


def dat_quyen(email: str, bat: bool, ki_cuu: bool = False) -> list[db.NguoiDung]:
    kq = _mot(email)
    ds = kq if isinstance(kq, list) else [kq]
    for u in ds:
        (db.dat_ki_cuu if ki_cuu else db.dat_admin)(u.id, bat)
    return [db.lay_nguoi_dung(u.id) for u in ds]


def dat_mat_khau(email: str, mat_khau: str) -> db.NguoiDung:
    u = _mot(email, chi_email=True)
    if len(mat_khau) < MAT_KHAU_TOI_THIEU:
        raise LoiQuanTri(f"Mật khẩu cần ít nhất {MAT_KHAU_TOI_THIEU} ký tự.")
    db.dat_mat_khau(u.id, bam_mat_khau(mat_khau))
    return u


def _in_mat_khau(mk: str, ngau_nhien: bool) -> None:
    if ngau_nhien:
        print(f"  Mật khẩu: {mk}   ← chỉ hiện một lần, hãy lưu lại (đổi được trong Cài đặt → Tài khoản)")


def chay(args: list[str]) -> int:
    ap = argparse.ArgumentParser(prog="quan_tri", description="Quản trị tài khoản CHEMISTRY")
    sub = ap.add_subparsers(dest="lenh")
    sub.add_parser("ds", help="liệt kê tài khoản")
    p = sub.add_parser("tao-admin", help="tạo tài khoản admin (email + mật khẩu)")
    p.add_argument("email"); p.add_argument("--ten", default=""); p.add_argument("--mat-khau-ngau-nhien", action="store_true")
    p = sub.add_parser("tao-ki-cuu", help="tạo tài khoản kì cựu (giải không giới hạn bằng key chủ app, không có quyền quản trị)")
    p.add_argument("email"); p.add_argument("--ten", default=""); p.add_argument("--mat-khau-ngau-nhien", action="store_true")
    for ten in ("cap-admin", "bo-admin", "cap-ki-cuu", "bo-ki-cuu"):
        sub.add_parser(ten).add_argument("email")
    p = sub.add_parser("dat-mat-khau", help="đặt lại mật khẩu (khi người dùng quên)")
    p.add_argument("email"); p.add_argument("--mat-khau-ngau-nhien", action="store_true")
    a = ap.parse_args(args)
    try:
        if a.lenh in (None, "ds"):
            print(danh_sach())
        elif a.lenh in ("tao-admin", "tao-ki-cuu"):
            ki_cuu = a.lenh == "tao-ki-cuu"
            quyen = "kì cựu" if ki_cuu else "admin"
            if db.lay_theo_sub(db.SUB_EMAIL + a.email.strip().lower()):
                u, _ = tao_admin(a.email, ki_cuu=ki_cuu)
                print(f"Tài khoản {u.email} đã có – đã cấp quyền {quyen} (mật khẩu giữ nguyên).")
            else:
                mk, nn = _nhap_mat_khau(a.mat_khau_ngau_nhien)
                u, _ = tao_admin(a.email, a.ten, mk, ki_cuu=ki_cuu)
                print(f"Đã tạo tài khoản {quyen} #{u.id}: {u.email} ({u.ten})")
                _in_mat_khau(mk, nn)
        elif a.lenh in ("cap-admin", "bo-admin"):
            for u in dat_quyen(a.email, a.lenh == "cap-admin"):
                print(f"#{u.id} {u.email} ({u.dang_nhap_bang}): admin = {'có' if u.la_admin else 'không'}")
        elif a.lenh in ("cap-ki-cuu", "bo-ki-cuu"):
            for u in dat_quyen(a.email, a.lenh == "cap-ki-cuu", ki_cuu=True):
                print(f"#{u.id} {u.email} ({u.dang_nhap_bang}): kì cựu = {'có' if u.la_ki_cuu else 'không'}")
        elif a.lenh == "dat-mat-khau":
            mk, nn = _nhap_mat_khau(a.mat_khau_ngau_nhien)
            u = dat_mat_khau(a.email, mk)
            print(f"Đã đặt mật khẩu mới cho {u.email}.")
            _in_mat_khau(mk, nn)
    except LoiQuanTri as e:
        print("LỖI:", e, file=sys.stderr)
        return 1
    return 0


def menu() -> int:
    while True:
        print("\n===== Quản trị tài khoản =====")
        print(danh_sach())
        print("\n 1) Tạo tài khoản admin   2) Cấp quyền admin   3) Bỏ quyền admin   4) Đặt lại mật khẩu")
        print(" 5) Tạo tài khoản kì cựu   6) Cấp quyền kì cựu   7) Bỏ quyền kì cựu   0) Thoát")
        chon = input("Chọn: ").strip()
        if chon in ("0", "q", ""):
            return 0
        if chon not in ("1", "2", "3", "4", "5", "6", "7"):
            continue
        email = input("Email: ").strip()
        lenh = {"1": ["tao-admin", email], "2": ["cap-admin", email], "3": ["bo-admin", email], "4": ["dat-mat-khau", email],
                "5": ["tao-ki-cuu", email], "6": ["cap-ki-cuu", email], "7": ["bo-ki-cuu", email]}[chon]
        if chon in ("1", "5") and not db.lay_theo_sub(db.SUB_EMAIL + email.lower()):
            ten = input("Tên hiển thị (Enter để bỏ qua): ").strip()
            if ten:
                lenh += ["--ten", ten]
        chay(lenh)


if __name__ == "__main__":
    sys.exit(menu() if len(sys.argv) == 1 else chay(sys.argv[1:]))
