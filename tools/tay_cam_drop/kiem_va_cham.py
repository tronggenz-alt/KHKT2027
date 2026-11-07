"""Kiểm va chạm và khe hở của tay cầm VERA Drop.

Chạy:  python tools/tay_cam_drop/kiem_va_cham.py
In ra: thể tích chồng lấn (mm³), khe hở nhỏ nhất (mm) của từng cặp; quét cò từ 0° tới góc chặn.
"""
import itertools
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mo_hinh_drop as mh  # noqa: E402

KHE_TOI_THIEU = 0.3

# cặp được thiết kế để chạm nhau (kẹp, tựa, dán, lồng)
DUOC_CHAM = {
    frozenset({"than"}): "2 nửa thân áp vào nhau ở mặt ghép",
    frozenset({"than", "thanh"}): "thanh móc lồng vào 2 tai",
    frozenset({"than", "vit"}): "vít ghép, đầu vít tì trong hốc",
    frozenset({"nap", "vit"}): "vít giữ nắp ăn vào tai nắp",
    frozenset({"thanh", "vit"}): "lõi vít đi trong thanh móc",
    frozenset({"than", "vong"}): "vòng sáng đặt lên miệng thân",
    frozenset({"vong", "nap"}): "nắp đặt lên vòng sáng, gờ lồng xuyên qua vòng",
    frozenset({"than", "nap"}): "gờ nắp lồng vào miệng thân",
    frozenset({"than", "esp32"}): "ESP32 kẹp trong 2 rãnh",
    frozenset({"than", "pin"}): "pin dán băng keo xốp vào thành",
    frozenset({"than", "tp4056"}): "mạch sạc tựa đáy, cổng ra lỗ đáy",
    frozenset({"than", "cong_tac"}): "công tắc gắn ở đáy",
    frozenset({"than", "hall"}): "SS49E nằm trong hốc vách",
    frozenset({"nap", "ky023"}): "mạch KY-023 tựa 4 trụ, cần đi qua lỗ nắp",
    frozenset({"co", "nam_cham"}): "nam châm dán trong hốc cò",
    frozenset({"than", "co"}): "cò quay giữa 2 tai trục",
    frozenset({"esp32", "bmi160"}): "BMI160 dán trên module ESP32",
    frozenset({"nap", "pin"}): "gờ nắp sát đỉnh pin, cách bằng băng xốp",
    frozenset({"nap", "chen"}): "chén lõm đặt trong hốc nắp",
    frozenset({"chen", "ky023"}): "cần joystick nằm trong chén",
}


def dung():
    ct = {ma: m for ma, _, _, m, _, _, _ in mh.chi_tiet_in()}
    for ma, _, _, m in mh.khoi_linh_kien():
        ct[ma] = m
    return ct


def goc_ma(ma):
    return "than" if ma.startswith("than_") else ma


def kiem_cap(a, b):
    return (a ^ b).volume(), a.min_gap(b, 12.0)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ct = dung()
    loi = []
    print(f"{'cặp':26s} {'chồng (mm³)':>12s} {'khe (mm)':>9s}  kết luận")
    for a, b in itertools.combinations(ct, 2):
        v, khe = kiem_cap(ct[a], ct[b])
        if v < 1e-3 and khe >= 3.0:
            continue
        cap = frozenset({goc_ma(a), goc_ma(b)})
        if cap in DUOC_CHAM:
            kl = "được phép: " + DUOC_CHAM[cap]
            if v > 0.05:
                kl = "LẤN VÀO NHAU dù được phép chạm"
                loi.append((a, b, round(v, 3)))
        elif v > 1e-3:
            kl = "VA CHẠM"
            loi.append((a, b, round(v, 3)))
        elif khe < KHE_TOI_THIEU:
            kl = "QUÁ SÁT"
            loi.append((a, b, round(khe, 3)))
        else:
            kl = "ổn"
        print(f"{a + ' ↔ ' + b:26s} {v:12.3f} {khe:9.2f}  {kl}")

    print("\nMỗi chi tiết in phải là một khối liền:")
    for ma, _, _, m, _, _, _ in mh.chi_tiet_in():
        so = len(m.decompose())
        print(f"  {ma:10s} {so} khối")
        if so != 1:
            loi.append((ma, "rời thành khối", so))

    print("\nQuét cò (góc, chồng với thân, khe với thân, khe với ESP32, khe nam châm ↔ Hall):")
    c, nc = ct["co"], ct["nam_cham"]
    than = ct["than_phai"] + ct["than_trai"]
    for g in np.linspace(0, mh.GOC_BOP, 7):
        cg = mh.quay_co(c, g)
        v, khe = kiem_cap(cg, than)
        _, khe_e = kiem_cap(cg, ct["esp32"])
        _, khe_nc = kiem_cap(mh.quay_co(nc, g), ct["hall"])
        print(f"  {g:5.1f}°: chồng {v:7.3f} mm³, khe thân {khe:5.2f}, khe ESP32 {khe_e:5.2f}, nam châm ↔ Hall {khe_nc:5.2f} mm")
        if v > (0.2 if g >= mh.GOC_BOP else 0.01):
            loi.append((f"co@{g:.1f}°", "than", round(v, 3)))
        if khe_e < KHE_TOI_THIEU:
            loi.append((f"co@{g:.1f}°", "esp32", round(khe_e, 3)))
    v, _ = kiem_cap(mh.quay_co(c, mh.GOC_BOP + 1.0), than)
    print(f"  quá góc chặn 1°: chồng {v:.3f} mm³ ({'vấu đã chạm, đúng' if v > 0 else 'CHƯA chạm: chỉnh vấu'})")
    if v <= 0:
        loi.append(("vấu cò", "chưa chạm", 0))
    print("\nKẾT QUẢ:", "không có va chạm ngoài ý muốn" if not loi else f"{len(loi)} chỗ cần sửa")
    for x in loi:
        print("  -", x)


if __name__ == "__main__":
    main()
