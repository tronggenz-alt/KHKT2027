"""Ảnh so sánh cỡ VERA Band 2 gốc và bản phóng: hình bao nhìn ngang, nhìn trước, kèm linh kiện cỡ thật trong bản phóng.

Chạy: python tools/tay_cam_band2/so_sanh_co.py <thư mục xem bản gốc> <thư mục xem bản phóng> <ảnh ra.png> [tỉ lệ]
(thư mục xem là tham số thứ nhất của mo_hinh_band2.py, chạy với VERA_TI_LE=1.0 và VERA_TI_LE=1.3)
"""
import os
import struct
import sys

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
import numpy as np  # noqa: E402

VO = ("than_phai", "than_trai", "nap", "chen", "nap_pin", "co", "tam_mu_tay")
LINH_KIEN = {"esp32": "#1d6fa5", "ky023": "#333", "pin9v": "#c9862b", "ha_ap": "#2563a6", "bmi160": "#7b4fd6"}


def doc_stl(p):
    with open(p, "rb") as f:
        f.read(80)
        n = struct.unpack("<I", f.read(4))[0]
        rec = np.frombuffer(f.read(n * 50), dtype=[("n", "<f4", 3), ("v", "<f4", (3, 3)), ("a", "<u2")])
    return rec["v"].reshape(-1, 3)


def diem(thu_muc, ten):
    p = os.path.join(thu_muc, ten + ".stl")
    return doc_stl(p) if os.path.exists(p) else np.zeros((0, 3))


def main():
    goc, phong, ra = sys.argv[1], sys.argv[2], sys.argv[3]
    k = float(sys.argv[4]) if len(sys.argv) > 4 else 1.3
    a = np.vstack([diem(goc, t) for t in VO])
    b = np.vstack([diem(phong, t) for t in VO])
    fig, ax = plt.subplots(1, 2, figsize=(12, 7.5))
    for i, (u, ten_u) in enumerate(((0, "x (mm, ra trước)"), (1, "y (mm)"))):
        ax[i].scatter(b[:, u], b[:, 2], s=0.3, c="#9fd3e6", alpha=0.35, label=f"Bản phóng ×{k:g}")
        ax[i].scatter(a[:, u], a[:, 2], s=0.3, c="#555", alpha=0.25, label="Band 2 gốc")
        for lk, mau in LINH_KIEN.items():
            p = diem(phong, lk)
            if len(p):   # khung bao của linh kiện, cỡ thật
                lo, hi = p.min(axis=0), p.max(axis=0)
                ax[i].add_patch(plt.Rectangle((lo[u], lo[2]), hi[u] - lo[u], hi[2] - lo[2], fill=False, ec=mau, lw=1.8))
        ax[i].set_aspect("equal")
        ax[i].set_xlabel(ten_u)
        ax[i].set_ylabel("z (mm, dọc cán)")
        ax[i].grid(alpha=0.25)
    ax[0].set_title("Nhìn ngang (phía ngón cái ở trên)")
    ax[1].set_title("Nhìn từ trước")
    ha = [plt.Line2D([], [], marker="o", ls="", c="#555", label="Band 2 gốc"),
          plt.Line2D([], [], marker="o", ls="", c="#9fd3e6", label=f"Bản phóng ×{k:g}")]
    ha += [plt.Line2D([], [], marker="o", ls="", c=m, label=t) for t, m in
           (("ESP32 (cỡ thật)", "#1d6fa5"), ("Joystick KY-023", "#333"), ("Pin 9V", "#c9862b"), ("Hạ áp MP1584", "#2563a6"), ("BMI160", "#7b4fd6"))]
    fig.legend(handles=ha, loc="lower center", ncol=4, frameon=False)
    sa, sb = np.ptp(a, axis=0), np.ptp(b, axis=0)
    fig.suptitle(f"VERA Band 2: gốc {sa[0]:.0f} × {sa[1]:.0f} × {sa[2]:.0f} mm  →  ×{k:g}: {sb[0]:.0f} × {sb[1]:.0f} × {sb[2]:.0f} mm (vỏ, không tính linh kiện)")
    fig.tight_layout(rect=(0, 0.07, 1, 0.95))
    fig.savefig(ra, dpi=130)
    print("đã ghi", ra)


if __name__ == "__main__":
    main()
