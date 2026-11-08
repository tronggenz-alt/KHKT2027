"""Lệnh chạy trên máy chủ; web không có đường nào tạo được tài khoản giáo viên.

    python -m server.lab.quan_tri tao-giao-vien gv.an "Nguyễn Văn An"    hỏi mật khẩu, không hiện trên màn hình
    python -m server.lab.quan_tri dat-lai-mat-khau gv.an                 khi giáo viên quên mật khẩu; web không có đường nào làm việc này
    python -m server.lab.quan_tri nap-mau                                nạp tài khoản thử từ data/tai_khoan_mau.json

Repo công khai nên tệp mẫu không có mật khẩu. Lệnh nạp sinh mật khẩu và mã lớp ngẫu nhiên, ghi vào data/tai_khoan_mau_mat_khau.json (git bỏ qua).
"""

from __future__ import annotations

import argparse
import getpass
import json
import os
import secrets
import sys
from pathlib import Path

from . import db, tai_khoan

MAU = Path(__file__).resolve().parents[2] / "data" / "tai_khoan_mau.json"
TEP_MAT_KHAU = MAU.with_name("tai_khoan_mau_mat_khau.json")


def _khop_tep_mat_khau(ten: str, ho_ten: str, vai_tro: str, mat_khau: dict) -> dict:
    """Tạo tài khoản thử, hoặc đặt lại mật khẩu nếu không còn khớp với tệp (tệp bị xóa, người thử đã đổi mật khẩu)."""
    nd = tai_khoan.tim_theo_ten(ten)
    if nd and ten in mat_khau and tai_khoan.dung_mat_khau_cua(nd, mat_khau[ten]):
        return nd
    mat_khau[ten] = secrets.token_urlsafe(12)
    if nd is None:
        return tai_khoan.tao_nguoi_dung(ten, ho_ten, vai_tro, mat_khau[ten])
    tai_khoan.dat_mat_khau(nd["id"], mat_khau[ten])
    tai_khoan.huy_phien_khac(nd["id"])
    return nd


def nap_mau(duong_dan: Path = MAU, tep_mat_khau: Path = TEP_MAT_KHAU) -> list[dict]:
    """Nạp tài khoản thử, chạy lại nhiều lần không tạo trùng. Trả về danh sách lớp kèm mã lớp."""
    if os.environ.get("KHKT_MO_MANG") == "1":
        raise tai_khoan.LoiTaiKhoan("Không nạp tài khoản thử khi máy chủ mở ra mạng (KHKT_MO_MANG=1)")
    mau = json.loads(duong_dan.read_text(encoding="utf-8"))
    mat_khau = json.loads(tep_mat_khau.read_text(encoding="utf-8"))["mat_khau"] if tep_mat_khau.exists() else {}
    nguoi = {}
    for vai_tro, ds in (("giao_vien", mau["giao_vien"]), ("hoc_sinh", mau["hoc_sinh"])):
        for u in ds:
            nguoi[u["ten_dang_nhap"]] = _khop_tep_mat_khau(u["ten_dang_nhap"], u["ho_ten"], vai_tro, mat_khau)
    lop = []
    for l in mau["lop"]:
        giao_vien_id = nguoi[l["giao_vien"]]["id"]
        co = db.query("SELECT * FROM lop WHERE ten=? AND giao_vien_id=?", (l["ten"], giao_vien_id))
        hang = co[0] if co else tai_khoan.tao_lop(l["ten"], l["nam_hoc"], giao_vien_id)
        for ten in l["hoc_sinh"]:
            tai_khoan.them_vao_lop(hang["id"], nguoi[ten]["id"])
        lop.append(hang)
    tep_mat_khau.write_text(json.dumps({"_ghi_chu": "Tài khoản THỬ trên máy này, không đưa lên git.", "mat_khau": mat_khau, "ma_lop": {l["ten"]: l["ma_lop"] for l in lop}},
                                       ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return lop


def _hoi_mat_khau() -> str:
    mat_khau = getpass.getpass("Mật khẩu: ")
    if mat_khau != getpass.getpass("Nhập lại mật khẩu: "):
        raise tai_khoan.LoiTaiKhoan("Hai lần nhập không khớp")
    return mat_khau


def dat_lai_mat_khau(nd: dict, mat_khau: str) -> None:
    tai_khoan.kiem_tra_mat_khau(mat_khau, nd["ten_dang_nhap"])
    tai_khoan.dat_mat_khau(nd["id"], mat_khau)
    tai_khoan.huy_phien_khac(nd["id"])


def main() -> int:
    for luong in (sys.stdout, sys.stderr):  # màn hình Windows mặc định cp1252, không in được tiếng Việt
        luong.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description="Quản lý tài khoản VERA Lab trên máy chủ")
    lenh = ap.add_subparsers(dest="lenh", required=True)
    gv = lenh.add_parser("tao-giao-vien")
    gv.add_argument("ten_dang_nhap")
    gv.add_argument("ho_ten")
    lenh.add_parser("dat-lai-mat-khau").add_argument("ten_dang_nhap")
    lenh.add_parser("nap-mau")
    args = ap.parse_args()
    try:
        if args.lenh == "tao-giao-vien":
            nd = tai_khoan.tao_nguoi_dung(args.ten_dang_nhap, args.ho_ten, "giao_vien", _hoi_mat_khau())
            print(f"Đã tạo giáo viên {nd['ten_dang_nhap']} ({nd['ho_ten']})")
        elif args.lenh == "dat-lai-mat-khau":
            nd = tai_khoan.tim_theo_ten(args.ten_dang_nhap)
            if nd is None:
                raise tai_khoan.LoiTaiKhoan("Không có tài khoản này")
            dat_lai_mat_khau(nd, _hoi_mat_khau())
            print(f"Đã đặt lại mật khẩu của {nd['ten_dang_nhap']}, các phiên đang mở đã bị hủy")
        else:
            for l in nap_mau():
                print(f"Lớp {l['ten']}: mã lớp {l['ma_lop']}")
            print(f"Đã nạp tài khoản thử; mật khẩu nằm trong {TEP_MAT_KHAU.relative_to(MAU.parents[1])}")
    except tai_khoan.LoiTaiKhoan as e:
        print(f"Lỗi: {e}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
