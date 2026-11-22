"""Đóng gói gửi tiệm in VERA Band: 5 file STL và file kích thước PDF, kiểm từng STL trước khi nén.

Chạy sau mo_hinh_band.py và ban_kich_thuoc.py:  python tools/tay_cam_band/dong_goi.py
Ra: docs/tay-cam-band/VERA_Band_STL.zip
"""
import json
import os
import struct
import sys
import zipfile
from collections import Counter

import numpy as np

GOC = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
THU_MUC = os.path.join(GOC, "docs", "tay-cam-band")
BAN_IN = 200    # kích thước bàn in nhỏ phổ biến, mm



def doc_stl(duong):
    with open(duong, "rb") as fh:
        fh.read(80)
        n = struct.unpack("<I", fh.read(4))[0]
        rec = np.frombuffer(fh.read(), dtype=[("n", "<f4", 3), ("v", "<f4", (3, 3)), ("a", "<u2")], count=n)
    return rec["v"]


def kiem(tam_giac):
    """Trả về (lưới kín, số khối rời) khi ghép đỉnh theo tọa độ."""
    _, chi_so = np.unique(np.round(tam_giac.reshape(-1, 3), 3), axis=0, return_inverse=True)
    f = chi_so.reshape(-1, 3)
    canh = Counter()
    for a, b, c in f.tolist():
        for u, v in ((a, b), (b, c), (c, a)):
            canh[(u, v)] += 1
    kin = all(canh.get((v, u), 0) == so for (u, v), so in canh.items())
    cha = list(range(int(f.max()) + 1))

    def goc(x):
        while cha[x] != x:
            cha[x] = cha[cha[x]]
            x = cha[x]
        return x

    for a, b, c in f.tolist():
        cha[goc(a)] = goc(b)
        cha[goc(b)] = goc(c)
    return kin, len({goc(x) for x in np.unique(f).tolist()})


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    with open(os.path.join(THU_MUC, "thong_so.json"), encoding="utf-8") as fh:
        goi = json.load(fh)
    pdf = os.path.join(THU_MUC, "VERA_Band_kich_thuoc.pdf")
    loi = 0
    for c in goi:
        tg = doc_stl(os.path.join(THU_MUC, "in", c["file"] + ".stl"))
        kin, so = kiem(tg)
        nho, lon = tg.reshape(-1, 3).min(axis=0), tg.reshape(-1, 3).max(axis=0)
        tot = kin and so == c["so_khoi"] and abs(nho[2]) < 0.01 and max(lon[0] - nho[0], lon[1] - nho[1]) <= BAN_IN
        loi += not tot
        print(f"{c['file']:14s} {len(tg):6d} tam giác, kín {kin}, {so}/{c['so_khoi']} khối, đáy z {nho[2]:.3f}, cỡ {np.round(lon - nho, 1)}  {'ổn' if tot else 'LỖI'}")
    ra = os.path.join(THU_MUC, "VERA_Band_STL.zip")
    with zipfile.ZipFile(ra, "w", zipfile.ZIP_DEFLATED) as z:
        for c in goi:
            z.write(os.path.join(THU_MUC, "in", c["file"] + ".stl"), c["file"] + ".stl")
        z.write(pdf, os.path.basename(pdf))
    with zipfile.ZipFile(ra) as z:
        assert z.testzip() is None
        print([i.filename for i in z.infolist()])
    print(os.path.getsize(ra) // 1024, "KB,", "có lỗi" if loi else "cả gói ổn")


if __name__ == "__main__":
    main()
