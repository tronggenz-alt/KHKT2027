"""So tia trỏ C# (Unity, VeraAimCore + OneEuro2) với bản gốc Python (xu_ly.ConTro) trên cùng một tệp ghi .vera.

    python tools/phong_thu/so_sanh_unity.py <tệp.vera> <vera_phat_lai.csv do bài Unity tay_cam_vera xuất>

Đạt khi số gói bằng nhau và sai lệch yaw / pitch / roll (thô và sau lọc) dưới 0,01° (khác biệt float32 với float64).
"""
import csv
import math
import os
import sys

import numpy as np

DAY = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, DAY)
sys.path.insert(0, os.path.dirname(DAY))
import ghi_tay_cam as gt  # noqa: E402
from nguon import TrangThai  # noqa: E402
from xu_ly import ConTro  # noqa: E402


def main():
    tep, csv_unity = sys.argv[1], sys.argv[2]
    d = open(tep, "rb").read()
    i = len(gt.MA_TEP)
    ct = ConTro()
    ra = []
    t_cu = None
    while i + gt.DAU_BAN_GHI.size <= len(d):
        loai, n, _ = gt.DAU_BAN_GHI.unpack_from(d, i)
        i += gt.DAU_BAN_GHI.size
        v = d[i:i + n]
        i += n
        if loai != gt.LOAI_THO or not gt.goi_hop_le(v):
            continue
        h = gt.giai_ma_dau(v)
        dt = 0.0 if t_cu is None else min(0.1, max(0.001, (h["t_ms"] - t_cu) / 1000))
        t_cu = h["t_ms"]
        ct.cap_nhat(TrangThai(q=h["q"]), dt)
        ra.append((h["chi_so"], ct.yaw, ct.pitch, ct.roll, ct.ra[0], ct.ra[1]))
    P = np.array(ra, float)
    with open(csv_unity, encoding="utf-8") as f:
        U = np.array([[float(r[k]) for k in ("seq", "yaw", "pitch", "roll", "yaw_loc", "pitch_loc")] for r in csv.DictReader(f)])
    print(f"Python: {len(P)} gói · Unity: {len(U)} gói")
    n = min(len(P), len(U))
    ten = ["chỉ số", "yaw thô", "pitch thô", "roll", "yaw lọc", "pitch lọc"]
    dat = len(P) == len(U)
    for k in range(1, 6):
        e = np.abs(P[:n, k] - U[:n, k])
        if k == 3:
            e = np.abs((P[:n, k] - U[:n, k] + 180) % 360 - 180)
        print(f"  {ten[k]:10s} lệch lớn nhất {e.max():.5f}°  trung bình {e.mean():.6f}°")
        dat &= e.max() < 0.01 or (k == 3 and np.percentile(e, 99.9) < 0.01)
    print("KẾT QUẢ:", "ĐẠT" if dat else "CHƯA ĐẠT")


if __name__ == "__main__":
    main()
