"""Viết hướng dẫn in, đóng gói STL và kiểm từng file trong gói.

Chạy sau mo_hinh_arc.py:  python tools/tay_cam_arc/dong_goi.py
Ra: docs/tay-cam-arc/in/HUONG_DAN_IN.txt, docs/tay-cam-arc/VERA_Arc_STL.zip
"""
import json
import os
import struct
import sys
import zipfile
from collections import Counter

import numpy as np

GOC = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
THU_MUC = os.path.join(GOC, "docs", "tay-cam-arc")
BAN_IN = 200    # kích thước bàn in nhỏ phổ biến, mm
SO_KHOI = {"than": 2}


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
    dong = ["TAY CẦM VERA ARC - HƯỚNG DẪN IN 3D", "",
            "Đơn vị trong file STL: mm. In đúng tỉ lệ 100%. Các file đã xoay sẵn đúng hướng in.", "",
            "File            Số lượng  Chi tiết                        Màu đề xuất             Kích thước (mm)      Hướng in"]
    for c in goi:
        kt = " x ".join(f"{v:g}".replace(".", ",") for v in c["kich_thuoc"])
        dong.append(f"{c['file'] + '.stl':16s}{c['so_luong']:<10d}{c['ten']:32s}{c['mau']:24s}{kt:21s}{c['cach_in']}")
    dong += ["", "Nhờ bên in lưu ý:",
             "- than.stl có 2 nửa thân rời nhau trong cùng 1 file, cần in cả 2. Giữ mặt ghép phẳng.",
             "- Vật liệu đề xuất: PLA hoặc PETG, lớp 0,16 mm, 3 lớp vách, độ đặc 20%.",
             "- 2 nửa thân cần vật đỡ bên trong phần đầu. Giữ mặt ngoài sạch.",
             "- vong.stl in dựng đứng, không cần vật đỡ. Mặt ngoài có logo và chữ VERA LAB khắc sâu 0,6 mm, chữ hoa cao khoảng 4,4 mm, nên chọn lớp mỏng để chữ rõ.",
             "- Lỗ nhỏ: lỗ mồi vít Ø1,7, lỗ qua vít Ø2,2-2,3, lỗ trục cò Ø2,2, cổng nút Ø7. Giữ đúng kích thước, không tự khoét rộng.",
             "- Ray của day_duoi.stl chỉ có khe 0,2 mm với thân. Nếu máy in sai số lớn, báo lại trước khi in."]
    with open(os.path.join(THU_MUC, "in", "HUONG_DAN_IN.txt"), "w", encoding="utf-8-sig") as fh:
        fh.write("\n".join(dong) + "\n")
    loi = 0
    for c in goi:
        tg = doc_stl(os.path.join(THU_MUC, "in", c["file"] + ".stl"))
        kin, so = kiem(tg)
        nho, lon = tg.reshape(-1, 3).min(axis=0), tg.reshape(-1, 3).max(axis=0)
        tot = kin and so == SO_KHOI.get(c["file"], 1) and abs(nho[2]) < 0.01 and max(lon[0] - nho[0], lon[1] - nho[1]) <= BAN_IN
        loi += not tot
        print(f"{c['file']:10s} {len(tg):6d} tam giác, kín {kin}, {so} khối, đáy z {nho[2]:.3f}, cỡ {np.round(lon - nho, 1)}  {'ổn' if tot else 'LỖI'}")
    ra = os.path.join(THU_MUC, "VERA_Arc_STL.zip")
    with zipfile.ZipFile(ra, "w", zipfile.ZIP_DEFLATED) as z:
        for c in goi:
            z.write(os.path.join(THU_MUC, "in", c["file"] + ".stl"), c["file"] + ".stl")
        z.write(os.path.join(THU_MUC, "in", "HUONG_DAN_IN.txt"), "HUONG_DAN_IN.txt")
    with zipfile.ZipFile(ra) as z:
        assert z.testzip() is None
        print([i.filename for i in z.infolist()])
    print(os.path.getsize(ra) // 1024, "KB,", "có lỗi" if loi else "cả gói ổn")


if __name__ == "__main__":
    main()
