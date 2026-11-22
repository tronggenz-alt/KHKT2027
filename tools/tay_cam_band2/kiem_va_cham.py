"""Kiểm va chạm và khe hở của tay cầm VERA Band.

Chạy:  python tools/tay_cam_band/kiem_va_cham.py
In ra: thể tích chồng lấn (mm³), khe hở nhỏ nhất (mm) của từng cặp; quét cò từ 0° tới góc chặn; khe bàn tay mô phỏng.
"""
import itertools
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mo_hinh_band2 as mh  # noqa: E402

KHE_TOI_THIEU = 0.3


def quay_co_k(m, goc):
    """Xoay cò quanh trục đã phóng theo hệ số K của mô hình."""
    R = mh.quay("y", goc)
    p = mh.CHOT * mh.K
    return m.transform(mh.bien_doi(R, p - R @ p))

# cặp được thiết kế để chạm nhau (kẹp, tựa, dán, lồng)
DUOC_CHAM = {
    frozenset({"than"}): "2 nửa thân áp vào nhau ở mặt ghép",
    frozenset({"than", "vit"}): "vít ghép, đầu vít tì trong hốc",
    frozenset({"nap", "vit"}): "vít giữ mặt tối ăn vào tai",
    frozenset({"than", "nap"}): "mặt tối tựa gờ thân, móc dưới gờ",
    frozenset({"than", "esp32"}): "ESP32 tựa 4 trụ, vấu chặn mép trên",
    frozenset({"than", "cong_tac"}): "công tắc gắn ở bụng đầu",
    frozenset({"than", "hall"}): "SS49E nằm trong hốc vách",
    frozenset({"than", "pin9v"}): "pin tựa thành cán",
    frozenset({"than", "kep_pin"}): "kẹp pin nằm trên pin",
    frozenset({"nap_pin", "pin9v"}): "pin tựa lên nắp khoang",
    frozenset({"pin9v", "kep_pin"}): "kẹp cắm vào cực pin",
    frozenset({"than", "nap_pin"}): "nắp pin trượt trong ray, gai khóa vào hốc",
    frozenset({"nap", "ky023"}): "mạch KY-023 tựa 4 trụ, cần đi qua lỗ mặt",
    frozenset({"co", "nam_cham"}): "nam châm dán trong hốc cò",
    frozenset({"than", "co"}): "cò quay giữa 2 tai trục",
    frozenset({"esp32", "bmi160"}): "BMI160 dán trên module ESP32",
    frozenset({"nap", "chen"}): "chén lục giác đặt trong hốc mặt",
    frozenset({"chen", "ky023"}): "cần joystick nằm trong chén",
    frozenset({"esp32", "ha_ap"}): "MP1584 dán băng xốp phía trên ESP32",
    frozenset({"than", "nut6x6"}): "nút 6 × 6 nằm trong ô của cổng mũi đầu",
    frozenset({"nap", "nut6x6"}): "nút 6 × 6 nằm trong ô của cổng trên mặt",
    frozenset({"nut_phu", "nut6x6"}): "nắp nút tì lên cần nút bấm, chừa hành trình",
    frozenset({"than", "nut_phu"}): "nắp nút lọt lỗ Ø7, gờ giữ dưới vách",
    frozenset({"nap", "nut_phu"}): "nắp nút lọt lỗ Ø7, gờ giữ dưới vách",
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
        if "quai" in (a, b):
            continue    # quai là băng vải mềm, hình chỉ để xem nên không kiểm va chạm
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
        if so != (4 if ma == "nut_phu" else 1):
            loi.append((ma, "rời thành khối", so))

    print("\nQuét cò (góc, chồng với thân, khe với thân, khe với ESP32, khe nam châm ↔ Hall):")
    c, nc = ct["co"], ct["nam_cham"]
    than = ct["than_phai"] + ct["than_trai"]
    for g in np.linspace(0, mh.GOC_BOP, 7):
        cg = quay_co_k(c, g)
        v, khe = kiem_cap(cg, than)
        _, khe_e = kiem_cap(cg, ct["esp32"])
        _, khe_nc = kiem_cap(quay_co_k(nc, g), ct["hall"])
        print(f"  {g:5.1f}°: chồng {v:7.3f} mm³, khe thân {khe:5.2f}, khe ESP32 {khe_e:5.2f}, nam châm ↔ Hall {khe_nc:5.2f} mm")
        if v > (0.2 if g >= mh.GOC_BOP else 0.01):
            loi.append((f"co@{g:.1f}°", "than", round(v, 3)))
        if khe_e < KHE_TOI_THIEU:
            loi.append((f"co@{g:.1f}°", "esp32", round(khe_e, 3)))
    v, _ = kiem_cap(quay_co_k(c, mh.GOC_BOP + 1.0), than)
    print(f"  quá góc chặn 1°: chồng {v:.3f} mm³ ({'vấu đã chạm, đúng' if v > 0 else 'CHƯA chạm: chỉnh vấu'})")
    if v <= 0:
        loi.append(("vấu cò", "chưa chạm", 0))

    print("\nBàn tay mô phỏng (khe mm; bàn tay là hình gần đúng, chồng với thân là chỗ tay ôm):")
    tay = mh.ban_tay()
    for ma in ("tam_mu_tay", "nap", "than_phai", "nap_pin"):
        v, khe = kiem_cap(tay, ct[ma])
        print(f"  tay ↔ {ma:10s} chồng {v:9.1f} mm³, khe {khe:5.2f}")
    v, khe = kiem_cap(tay, ct["tam_mu_tay"])
    if v > 0 or khe < 2.0:
        loi.append(("tay", "tam_mu_tay", round(khe, 2)))
    v, khe = kiem_cap(tay, mh.khuyen_tren())
    print(f"  tay ↔ khuyên trên chồng {v:9.1f} mm³, khe {khe:5.2f}")
    if v > 0:
        loi.append(("tay", "khuyên trên", round(v, 1)))
    print("\nKhe dưới phải là lỗ thông, quai 20 × 1,4 lọt qua:")
    # 2 góc mái khe vát VAT_KHE: phần rộng đủ 20,4 mm cao 1,9 mm (quai dày 1,4), phần sát mái hẹp lại theo góc vát
    ruot = (mh.hop(mh.KHE_X[0] + 0.3, mh.KHE_X[1] - 0.3, mh.KHE_Y[0] + mh.VAT_KHE, mh.KHE_Y[1] - 0.3, mh.Z_DAY - 2, mh.Z_DAY + mh.CAO_PHINH + 1.5)
            + mh.hop(mh.KHE_X[0] + mh.VAT_KHE + 0.3, mh.KHE_X[1] - mh.VAT_KHE - 0.3, mh.KHE_Y[0] + 0.3, mh.KHE_Y[1] - 0.3, mh.Z_DAY - 2, mh.Z_DAY + mh.CAO_PHINH + 1.5)).scale([mh.K, mh.K, mh.K])   # khe đã phóng theo K
    for ma in ("than_phai", "than_trai"):
        v, _ = kiem_cap(ruot, ct[ma])
        print(f"  khe ↔ {ma}: chồng {v:.3f} mm³")
        if v > 0.01:
            loi.append(("khe dưới", ma, round(v, 3)))
    print("\n2 khe của tấm mu tay phải thông, quai 20 × 1,4 lọt qua:")
    for z0, z1 in mh.TAM_KHE_Z:
        ruot = mh.hop(mh.KHE_X[0] + 1.8, mh.KHE_X[1] + 1.2, -90, 0, z0 + 0.3, z1 - 0.3).scale([mh.K, mh.K, mh.K])
        v, _ = kiem_cap(ruot, ct["tam_mu_tay"])
        print(f"  khe z {z0:g}–{z1:g}: chồng {v:.3f} mm³")
        if v > 0.01:
            loi.append(("khe tấm mu tay", z0, round(v, 3)))

    print("\nKẾT QUẢ:", "không có va chạm ngoài ý muốn" if not loi else f"{len(loi)} chỗ cần sửa")
    for x in loi:
        print("  -", x)


if __name__ == "__main__":
    main()
