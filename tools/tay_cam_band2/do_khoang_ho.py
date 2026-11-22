"""Đo khoảng hở giữa từng linh kiện và vỏ thân VERA Band (bỏ qua chỗ tựa có chủ ý như trụ đỡ, gờ, vách Hall).

Chạy:  python tools/tay_cam_band/do_khoang_ho.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mo_hinh_band2 as mh  # noqa: E402

LINH_KIEN = ("pin9v", "kep_pin", "esp32", "bmi160", "ha_ap", "ky023", "cong_tac", "nut6x6")


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    vo = mh.khoi() - mh.khoi(mh.VACH)
    lk = {ma: m for ma, _, _, m in mh.khoi_linh_kien()}
    print(f"Vách {mh.VACH} mm. Khoảng hở nhỏ nhất tới mặt trong vỏ (mm):")
    for ma in LINH_KIEN:
        print(f"  {ma:9s} {lk[ma].min_gap(vo, 10.0):5.2f}")
    pin = lk["pin9v"]
    lo = mh.hop(mh.BAY_XC - mh.BAY_X, mh.BAY_XC + mh.BAY_X, -mh.BAY_Y, mh.BAY_Y, mh.Z_DAY - 1, mh.Z_SAN + 0.1)
    bb = pin.bounding_box()
    print(f"  lỗ lọt pin {2 * mh.BAY_X:.1f} × {2 * mh.BAY_Y:.1f}, pin {bb[3] - bb[0]:.1f} × {bb[4] - bb[1]:.1f}: dư {mh.BAY_X - (bb[3] - bb[0]) / 2:.2f} mỗi bên theo x, {mh.BAY_Y - (bb[4] - bb[1]) / 2:.2f} theo y")


if __name__ == "__main__":
    main()
