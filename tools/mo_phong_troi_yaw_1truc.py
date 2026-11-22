"""Mô phỏng trôi yaw của BMI160 và đo các thuật toán chống trôi trong firmware/tay_cam_vera/huong.h.

Chạy:  python tools/mo_phong_troi_yaw_1truc.py [số_lần=40] [ảnh_ra.png]

Mô hình 1 trục (quay quanh trục đứng, tay cầm gần nằm ngang), 100 mẫu/giây, phiên 10 phút:
  con quay đo = (1 + sai_tỉ_lệ) * vận_tốc_thật + độ_lệch_lúc_bật + hệ_số_nhiệt * (T - T0) + bước_ngẫu_nhiên + nhiễu
Thông số lấy theo datasheet BMI160 (Bosch BST-BMI160-DS000): nhiễu 0,008 °/s/√Hz, sai tỉ lệ ±1%,
độ lệch đổi theo nhiệt tối đa ±0,05 °/s/K (mô phỏng lấy ngẫu nhiên, độ lệch chuẩn 0,02), lượng tử 1/32,8 °/s.
Mạch nóng lên 8 °C với hằng số thời gian 4 phút (ESP32 bật Wi-Fi). Độ lệch trôi ngẫu nhiên 0,0002 °/s/√s (đổi bằng TROI_RW).

Chuyển động: run tay 8-12 Hz, lắc lư chậm, các lần quay 20-120° ở 60-200 °/s, lúc ngắm yên.
Kịch bản 1 "có đặt xuống bàn": cứ khoảng 90 giây đặt tay cầm xuống bàn 5-15 giây.
Kịch bản 2 "cầm liên tục": cầm tay suốt 10 phút (khó nhất: không có lần nằm yên nào để học độ lệch).

Đây là mô phỏng, không thay cho đo trên mạch thật (docs/28 mục Đo trôi yaw).
"""
import math
import sys
import numpy as np

HZ = 100
DT = 1.0 / HZ
THOI_GIAN = 600.0
LSB = 1 / 32.8
# Độ lệch trôi ngẫu nhiên (°/s/√s). 0,0002 ứng với độ lệch đổi khoảng 0,005 °/s sau 10 phút, cỡ "bias instability"
# vài °/giờ của con quay MEMS tiêu dùng. Đổi bằng biến môi trường TROI_RW để thử trường hợp xấu hơn.
import os
BUOC_NGAU_NHIEN = float(os.environ.get("TROI_RW", "0.0002"))


def tao_chuyen_dong(rng, co_dat_ban):
    """Trả về (vận tốc thật °/s, độ lệch |a| khỏi 1 g, cờ đang trên bàn) cho một phiên."""
    n = int(THOI_GIAN * HZ)
    w = np.zeros(n)
    tren_ban = np.zeros(n, bool)
    i = 0
    tren_ban[: 3 * HZ] = True  # lúc bật: đặt trên bàn 3 giây
    i = 3 * HZ
    lan_dat_ban = i + int(rng.uniform(60, 120) * HZ)
    while i < n:
        if co_dat_ban and i >= lan_dat_ban:
            d = int(rng.uniform(5, 15) * HZ)
            tren_ban[i:i + d] = True
            i += d
            lan_dat_ban = i + int(rng.uniform(60, 120) * HZ)
            continue
        if rng.random() < 0.55:  # ngắm yên 2-6 giây (chỉ run và lắc lư)
            i += int(rng.uniform(2, 6) * HZ)
        else:  # quay một góc rồi dừng
            goc = rng.uniform(20, 120) * rng.choice([-1, 1])
            dinh = rng.uniform(60, 200)
            d = max(int(abs(goc) / dinh * 2 * HZ), 5)
            hinh = np.sin(np.linspace(0, np.pi, d)) ** 2
            hinh *= goc / (hinh.sum() * DT)
            e = min(i + d, n)
            w[i:e] += hinh[: e - i]
            i = e
    cam = ~tren_ban
    # run tay 8-12 Hz, 0,6 °/s hiệu dụng
    t = np.arange(n) * DT
    run = np.zeros(n)
    for _ in range(4):
        run += np.sin(2 * np.pi * rng.uniform(8, 12) * t + rng.uniform(0, 6.3))
    run *= 0.6 / np.sqrt(2)
    # lắc lư chậm 0,2-1 Hz, 0,8 °/s hiệu dụng
    lu = np.zeros(n)
    for _ in range(4):
        lu += np.sin(2 * np.pi * rng.uniform(0.2, 1.0) * t + rng.uniform(0, 6.3))
    lu *= 0.8 / np.sqrt(2)
    w += (run + lu) * cam
    lech_a = np.where(cam, 0.02 + 0.0015 * np.abs(w), 0.0) + rng.normal(0, 0.004, n)
    return w, np.abs(lech_a), tren_ban


def tao_con_quay(rng, w):
    n = len(w)
    t = np.arange(n) * DT
    nhiet = 26 + 8 * (1 - np.exp(-t / 240))
    he_so = np.clip(rng.normal(0, 0.02), -0.05, 0.05)
    sai_ti_le = rng.normal(0, 0.01)
    lech0 = rng.uniform(-3, 3)
    buoc = np.cumsum(rng.normal(0, BUOC_NGAU_NHIEN * np.sqrt(DT), n))
    nhieu = rng.normal(0, 0.008 * np.sqrt(40), n)
    do = (1 + sai_ti_le) * w + lech0 + he_so * (nhiet - nhiet[0]) + buoc + nhieu
    do = np.round(do / LSB) * LSB
    nhiet_do = nhiet + rng.normal(0, 0.1, n)
    return do, nhiet_do, he_so, sai_ti_le


CAU_HINH = [
    ("A0 chỉ cân lúc bật", dict()),
    ("A1 firmware cũ (cân + học khi nằm yên)", dict(yen=True)),
    ("A2 + bù nhiệt học trong phiên", dict(yen=True, nhiet=True)),
    ("A3 + hệ số nhiệt đo bằng lệnh n", dict(yen=True, nhiet=True, nhiet_luu=True, nhiet_sai=0.1)),
    ("A4 + cân lúc bật 2 giây", dict(yen=True, nhiet=True, nhiet_luu=True, nhiet_sai=0.1, can_s=2)),
    ("A5 + vùng chết mềm 0,5 °/s", dict(yen=True, nhiet=True, nhiet_luu=True, nhiet_sai=0.1, can_s=2, vung_chet=True)),
    ("A6 + hiệu chuẩn tỉ lệ (lệnh g) = firmware mới",
     dict(yen=True, nhiet=True, nhiet_luu=True, nhiet_sai=0.1, can_s=2, vung_chet=True, ti_le=True)),
    ("(đã thử, bỏ) A6 + ZARU lúc cầm tay",
     dict(yen=True, nhiet=True, nhiet_luu=True, nhiet_sai=0.1, can_s=2, vung_chet=True, ti_le=True, zaru=True)),
]


def chay(do, nhiet, lech_a, he_so_that, sai_ti_le, rng, cfg):
    """Chạy đúng các bước của huong.h (đổi hằng số sang 100 Hz). Trả về yaw ước lượng theo thời gian."""
    n = len(do)
    b0 = float(np.mean(do[: int(cfg.get("can_s", 1) * HZ)]))  # cân lúc bật (trên bàn)
    lech, lech_goc = b0, b0
    tb, ps = b0, 1.0
    dem_yen, yen, bat_dau_yen = 0, False, 0
    moc_lech, moc_nhiet = b0, float(nhiet[HZ])
    he_so = 0.0
    if cfg.get("nhiet_luu"):
        he_so = he_so_that * (1 + rng.normal(0, cfg.get("nhiet_sai", 0.3)))  # học từ trước, sai 30% (lệnh n: 10%)
    ti_le = 1.0
    if cfg.get("ti_le"):
        ti_le = 1 / (1 + sai_ti_le) * (1 + rng.normal(0, 0.002))  # lệnh g: còn sai khoảng 0,2%
    z_tong = z_tong2 = 0.0
    z_n = 0
    psi = np.zeros(n)
    p = 0.0
    do = do.tolist()
    nhiet = nhiet.tolist()
    lech_a = lech_a.tolist()
    k0 = int(cfg.get("can_s", 1) * HZ)
    for k in range(k0, n):
        g = do[k]
        tb += 0.05 * (g - tb)
        d = g - tb
        ps += 0.05 * (d * d - ps)
        yen_nay = math.sqrt(3 * ps) < 0.6 and lech_a[k] < 0.06
        dem_yen = dem_yen + 1 if yen_nay else 0
        yen_moi = cfg.get("yen", False) and dem_yen > HZ // 2
        if yen_moi and not yen:
            bat_dau_yen = k
        if not yen_moi and yen and k - bat_dau_yen > HZ and cfg.get("nhiet"):
            if abs(nhiet[k] - moc_nhiet) >= 0.5:
                dd = min(0.05, max(-0.05, (lech - moc_lech) / (nhiet[k] - moc_nhiet)))
                he_so = 0.7 * he_so + 0.3 * dd
            moc_lech, moc_nhiet = lech, nhiet[k]
        yen = yen_moi
        bu = he_so * (nhiet[k] - moc_nhiet) if cfg.get("nhiet") else 0.0
        c = (g - lech - bu) * ti_le
        if yen:
            lech += 0.02 * (tb - lech)
            lech_goc = lech
            c = 0.0
            z_tong = z_tong2 = 0.0
            z_n = 0
        else:
            if cfg.get("zaru"):
                if lech_a[k] < 0.1:
                    z_tong += c
                    z_tong2 += c * c
                    z_n += 1
                    if z_n >= int(cfg.get("zaru_cua_so", 1.0) * HZ):
                        m = z_tong / z_n
                        s2 = z_tong2 / z_n - m * m
                        # 1 trục: run tay trên trục đứng ít hơn tổng 3 trục, giữ đúng ngưỡng 1,5 °/s mỗi trục
                        if s2 < cfg.get("zaru_rung", 1.5) ** 2 and abs(m) < cfg.get("zaru_tb", 0.6):
                            lech = min(lech_goc + 1.0, max(lech_goc - 1.0, lech + cfg.get("zaru_he_so", 0.1) * m / ti_le))
                        z_tong = z_tong2 = 0.0
                        z_n = 0
                else:
                    z_tong = z_tong2 = 0.0
                    z_n = 0
            if cfg.get("vung_chet"):
                aw = abs(c)
                vc = cfg.get("vung_chet_dps", 0.5)
                if aw < 2 * vc:
                    c *= 0.0 if aw <= vc else (aw - vc) / vc
        p += c * DT
        psi[k] = p
    return psi


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    so_lan = int(sys.argv[1]) if len(sys.argv) > 1 else 40
    anh = sys.argv[2] if len(sys.argv) > 2 else None
    ket_qua = {}
    mau_ve = {}
    for kb, co_ban in (("Kịch bản 1: có đặt xuống bàn", True), ("Kịch bản 2: cầm liên tục 10 phút", False)):
        loi_cuoi = {ten: [] for ten, _ in CAU_HINH}
        loi_tb = {ten: [] for ten, _ in CAU_HINH}
        for r in range(so_lan):
            rng = np.random.default_rng(1000 + r)
            w, lech_a, _ = tao_chuyen_dong(rng, co_ban)
            do, nhiet, he_so, sai = tao_con_quay(rng, w)
            that = np.cumsum(w) * DT
            that -= that[HZ - 1]
            for ten, cfg in CAU_HINH:
                rng_cfg = np.random.default_rng(5000 + r)
                psi = chay(do, nhiet, lech_a, he_so, sai, rng_cfg, cfg)
                loi = psi[HZ:] - that[HZ:]
                loi_cuoi[ten].append(abs(loi[-1]))
                loi_tb[ten].append(np.mean(np.abs(loi)))
                if r == 0:
                    mau_ve[(kb, ten)] = loi
        ket_qua[kb] = (loi_cuoi, loi_tb)

    for kb, (loi_cuoi, loi_tb) in ket_qua.items():
        goc0 = np.mean(loi_cuoi[CAU_HINH[0][0]])
        goc1 = np.mean(loi_cuoi[CAU_HINH[1][0]])
        print(f"\n{kb} ({so_lan} lần, sai lệch yaw sau 10 phút)")
        print(f"{'cấu hình':52s} {'TB cuối':>8s} {'P90 cuối':>9s} {'TB cả phiên':>12s} {'giảm so A0':>11s} {'giảm so A1':>11s}")
        for ten, _ in CAU_HINH:
            c = np.array(loi_cuoi[ten])
            print(f"{ten:52s} {c.mean():7.2f}° {np.percentile(c, 90):8.2f}° {np.mean(loi_tb[ten]):11.2f}° "
                  f"{(1 - c.mean() / goc0) * 100:10.0f}% {(1 - c.mean() / goc1) * 100:10.0f}%")

    if anh:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
        fig, truc = plt.subplots(1, 2, figsize=(13, 4.6), sharey=False)
        t = np.arange(HZ, int(THOI_GIAN * HZ)) * DT / 60
        mau = ["#c2452b", "#d98b12", "#9fb6ba", "#7d9196", "#6b5bd6", "#11a8c8", "#0e7c86", "#b06d00"]
        for ax, kb in zip(truc, ket_qua):
            for (ten, _), m in zip(CAU_HINH, mau):
                ax.plot(t, mau_ve[(kb, ten)], color=m, lw=2.2 if ten.startswith(("A0", "A1", "A6")) else 1, ls="--" if ten.startswith("(") else "-", label=ten)
            ax.set_title(kb + " (lần chạy 1)")
            ax.set_xlabel("phút")
            ax.set_ylabel("sai lệch yaw (°)")
            ax.axhline(0, color="#10242b", lw=0.6)
            ax.grid(alpha=0.25)
        truc[0].legend(fontsize=7.5, loc="best")
        fig.tight_layout()
        fig.savefig(anh, dpi=130)
        print("\nĐã lưu", anh)


if __name__ == "__main__":
    main()
