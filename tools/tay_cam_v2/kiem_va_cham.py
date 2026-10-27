"""Kiểm va chạm và khe hở giữa các chi tiết, linh kiện của tay cầm V2.

Chạy:  python tools/tay_cam_v2/kiem_va_cham.py
In ra: thể tích chồng lấn (mm³) và khe hở nhỏ nhất (mm) của từng cặp, và quét cò từ 0° tới góc chặn.
Cặp được phép chạm (dán lên nhau, nằm trong rãnh) thì ghi rõ "được phép".
"""
import itertools
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mo_hinh as mh  # noqa: E402

KHE_TOI_THIEU = 0.3        # mm: khe lắp ráp tối thiểu giữa hai chi tiết không dính nhau
GOC_CHAN = 11.0            # độ: cò quay tới khi vấu chạm vách Hall

# các cặp được thiết kế để chạm nhau (dán, kẹp, tựa)
DUOC_CHAM = {
    frozenset({"bmi160", "esp32"}): "BMI160 dán trên vỏ module ESP32",
    frozenset({"esp32", "vo"}): "ESP32 nằm trong rãnh kẹp của vỏ",
    frozenset({"ky023", "vo"}): "mạch KY-023 tựa trên gờ đỡ",
    frozenset({"hall", "vo"}): "SS49E nằm trong hốc của vách",
    frozenset({"nam_cham", "co"}): "nam châm dán trong hốc của cò",
    frozenset({"pin", "vo"}): "pin dán băng keo xốp vào thành cán",
    frozenset({"tp4056", "vo"}): "mạch sạc tựa vào thành cán",
    frozenset({"tp4056", "nap_day"}): "cổng USB-C tựa vào nắp đáy",
    frozenset({"tam_op", "vo"}): "tấm ốp nằm trong hốc chìm",
    frozenset({"tam_op", "ky023"}): "cần joystick xuyên qua lỗ tấm ốp",
    frozenset({"halo", "vo"}): "đế tay đỡ halo ôm theo mặt vỏ",
    frozenset({"dai_sang", "vo"}): "dải màu nằm trong rãnh",
    frozenset({"nap_day", "vo"}): "nắp đáy ôm hốc lõm của vỏ",
}


def dung_chi_tiet():
    vo, tru_vit, dai = mh.vo_nguyen()
    trai, phai = mh.tach_doi(vo, tru_vit)
    ct = {"vo": trai + phai, "co": mh.co(), "halo": mh.halo(), "tam_op": mh.tam_op_mat(),
          "nap_day": mh.theo_can(mh.nap_day(), mh.S_DAY - 10), "dai_sang": dai}
    for ma, _, _, m in mh.khoi_linh_kien():
        ct[ma] = m
    return ct


def kiem_cap(a, b):
    giao = a ^ b
    v = giao.volume()
    try:
        khe = a.min_gap(b, 12.0)
    except Exception:
        khe = float("nan")
    return v, khe


def quay_co(m, goc):
    """Quay cò quanh trục chốt (song song Y), chiều bóp: phần dưới đi về phía sau."""
    c = mh.CHOT_CO
    R = mh.quay("y", goc)
    t = c - R @ c
    return m.transform(mh.bien_doi(R, t))


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ct = dung_chi_tiet()
    loi = []
    print(f"{'cặp':34s} {'chồng (mm³)':>12s} {'khe (mm)':>9s}  kết luận")
    for a, b in itertools.combinations(ct, 2):
        v, khe = kiem_cap(ct[a], ct[b])
        if v < 1e-3 and (khe != khe or khe >= 12.0):
            continue
        cap = frozenset({a, b})
        if cap in DUOC_CHAM:
            kl = "được phép: " + DUOC_CHAM[cap]
        elif v > 1e-3:
            kl = "VA CHẠM"
            loi.append((a, b, v))
        elif khe < KHE_TOI_THIEU:
            kl = "QUÁ SÁT"
            loi.append((a, b, khe))
        else:
            kl = "ổn"
        print(f"{a + ' ↔ ' + b:34s} {v:12.3f} {khe:9.2f}  {kl}")

    print("\nQuét cò (chồng lấn với vỏ, khe tới vách Hall):")
    nam = ct["nam_cham"]
    hall = ct["hall"]
    for g in np.linspace(0, GOC_CHAN, 12):
        co_g = quay_co(ct["co"], g)
        v, khe = kiem_cap(co_g, ct["vo"])
        nam_g = quay_co(nam, g)
        _, khe_nc = kiem_cap(nam_g, hall)
        kl = "VA CHẠM" if v > 0.05 else "ổn"
        if v > 0.05:
            loi.append(("co@%.1f°" % g, "vo", v))
        print(f"  góc {g:5.1f}°: chồng với vỏ {v:7.3f} mm³, khe vỏ {khe:5.2f} mm, khe nam châm ↔ Hall {khe_nc:5.2f} mm  {kl}")
    co_max = quay_co(ct["co"], GOC_CHAN + 0.8)
    v, _ = kiem_cap(co_max, ct["vo"])
    print(f"  quá góc chặn 0,8°: chồng {v:.3f} mm³ ({'vấu đã chạm vách, đúng' if v > 0 else 'CHƯA chạm: chỉnh vấu'})")

    print("\nKẾT QUẢ:", "không có va chạm ngoài ý muốn" if not loi else f"{len(loi)} chỗ cần sửa")
    for x in loi:
        print("  -", x)
    return loi


if __name__ == "__main__":
    main()
