"""Tiện ích kiểm thử: mở phiên giáo viên cho các bài kiểm thử không nhắm vào phần đăng nhập."""

import time

from server.lab import dang_nhap, db, tai_khoan


def dang_nhap_giao_vien(client) -> None:
    """Thêm thẳng một giáo viên vào CSDL đang dùng (không băm mật khẩu để khỏi tốn thời gian) và gắn cookie phiên vào client."""
    cur = db.execute("INSERT INTO nguoi_dung (ten_dang_nhap, ho_ten, vai_tro, mat_khau_bam, tao_luc) VALUES (?,?,?,?,?)",
                     ("gv.kiemthu", "Giáo viên kiểm thử", "giao_vien", "khong-phai-ma-bam", time.time()))
    client.cookies.set(dang_nhap.COOKIE, tai_khoan.tao_phien(cur.lastrowid)["ma_phien"])
