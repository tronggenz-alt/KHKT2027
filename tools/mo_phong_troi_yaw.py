"""Mô phỏng 3D trôi yaw của BMI160: so sánh Madgwick, Mahony và các kỹ thuật bù sai số của firmware/tay_cam_vera.

Chạy:  python tools/mo_phong_troi_yaw.py [số_lần=24] [ảnh_ra.png]
       TROI_CHI="M1,R2" python tools/mo_phong_troi_yaw.py 8     (chỉ chạy vài cấu hình)
       TROI_THEM="R3|{'loc':'mahony','ki':0.05}" python ...      (thử thêm cấu hình)
       TROI_LY_TUONG=1 python tools/mo_phong_troi_yaw.py 4      (kiểm tra: con quay hoàn hảo)
Bản 1 trục cũ (tìm ra ZARU có hại): tools/mo_phong_troi_yaw_1truc.py.

Mô hình (100 mẫu/giây, phiên 10 phút, mỗi lần chạy một tay cầm và một người dùng ngẫu nhiên):
  con quay đo = (1 + sai_tỉ_lệ) * ω_thật + độ_lệch_lúc_bật + hệ_số_nhiệt * (T - T0) + bước_ngẫu_nhiên + nhiễu
  gia tốc đo  = trọng lực trong hệ tay cầm + gia tốc tay + nhiễu
Thông số theo datasheet BMI160 (Bosch BST-BMI160-DS000): nhiễu 0,008 °/s/√Hz, độ lệch lúc bật tới ±3 °/s, sai tỉ lệ ±1%,
độ lệch đổi theo nhiệt tối đa ±0,05 °/s/K (mô phỏng: độ lệch chuẩn 0,02 mỗi trục), lượng tử 1/32,8 °/s.
Mạch nóng lên 8 °C, hằng số thời gian 4 phút (ESP32 bật Wi-Fi). Độ lệch trôi ngẫu nhiên 0,0002 °/s/√s (TROI_RW).

Chuyển động thật: quay 20-120° ở 60-200 °/s, ngắm yên, run tay 8-12 Hz, lắc lư chậm, nghiêng cổ tay lang thang ±12°,
thỉnh thoảng nghiêng rót 40-70° trong 2-4 giây.
  Kịch bản 1 "có đặt xuống bàn": cứ 60-120 giây đặt tay cầm xuống bàn 5-15 giây.
  Kịch bản 2 "cầm liên tục": cầm suốt 10 phút, không có lần nằm yên nào sau lúc bật (khó nhất).
Yaw: hướng mũi tay cầm trên mặt phẳng ngang. Là mô phỏng, không thay cho đo trên mạch thật (docs/28).
"""
import math
import os
import sys
import numpy as np

HZ = 100
DT = 1.0 / HZ
THOI_GIAN = 600.0
LSB = 1 / 32.8
D2R = math.pi / 180
BUOC_NGAU_NHIEN = float(os.environ.get("TROI_RW", "0.0002"))
# mật độ nhiễu trắng con quay °/s/√Hz: datasheet 0,008; đo trên mạch thật 11/11/2026 (ARW 0,36–0,45 °/√h) ≈ 0,0067–0,0076
NHIEU_MAT_DO = float(os.environ.get("TROI_NHIEU", "0.008"))


# ---------------------------------------------------------------- quaternion (cột cuối = w, x, y, z)
def qnhan(a, b):
    w1, x1, y1, z1 = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    w2, x2, y2, z2 = b[..., 0], b[..., 1], b[..., 2], b[..., 3]
    return np.stack([w1 * w2 - x1 * x2 - y1 * y2 - z1 * z2,
                     w1 * x2 + x1 * w2 + y1 * z2 - z1 * y2,
                     w1 * y2 - x1 * z2 + y1 * w2 + z1 * x2,
                     w1 * z2 + x1 * y2 - y1 * x2 + z1 * w2], -1)


def q_euler(psi, theta, phi):
    cy, sy = np.cos(psi / 2), np.sin(psi / 2)
    cp, sp = np.cos(theta / 2), np.sin(theta / 2)
    cr, sr = np.cos(phi / 2), np.sin(phi / 2)
    return np.stack([cr * cp * cy + sr * sp * sy, sr * cp * cy - cr * sp * sy,
                     cr * sp * cy + sr * cp * sy, cr * cp * sy - sr * sp * cy], -1)


def yaw_cua(q):
    return np.arctan2(2 * (q[..., 0] * q[..., 3] + q[..., 1] * q[..., 2]), 1 - 2 * (q[..., 2] ** 2 + q[..., 3] ** 2))


def loc_thap(x, tau):
    a = DT / (tau + DT)
    y = np.empty_like(x)
    acc = x[0]
    for i in range(len(x)):
        acc += a * (x[i] - acc)
        y[i] = acc
    return y


# ---------------------------------------------------------------- tạo một phiên
def tao_phien(rng, co_dat_ban):
    n = int(THOI_GIAN * HZ)
    t = np.arange(n) * DT
    w = np.zeros(n)
    tren_ban = np.zeros(n, bool)
    tren_ban[: 3 * HZ] = True
    i = 3 * HZ
    lan_dat = i + int(rng.uniform(60, 120) * HZ)
    while i < n:
        if co_dat_ban and i >= lan_dat:
            d = int(rng.uniform(5, 15) * HZ)
            tren_ban[i:i + d] = True
            i += d
            lan_dat = i + int(rng.uniform(60, 120) * HZ)
        elif rng.random() < 0.55:
            i += int(rng.uniform(2, 6) * HZ)
        else:
            goc = rng.uniform(20, 120) * rng.choice([-1, 1])
            dinh = rng.uniform(60, 200)
            d = max(int(abs(goc) / dinh * 2 * HZ), 5)
            hinh = np.sin(np.linspace(0, np.pi, d)) ** 2
            hinh *= goc / (hinh.sum() * DT)
            e = min(i + d, n)
            w[i:e] += hinh[: e - i]
            i = e
    cam = (~tren_ban).astype(float)
    cam_muot = loc_thap(cam, 0.3)

    def song(f0, f1, rms, k=4):
        s = np.zeros(n)
        for _ in range(k):
            s += np.sin(2 * np.pi * rng.uniform(f0, f1) * t + rng.uniform(0, 6.3))
        return s * rms / math.sqrt(k / 2)

    w += (song(8, 12, 0.6) + song(0.2, 1.0, 0.8)) * cam
    psi = np.cumsum(w) * DT * D2R
    theta = loc_thap(rng.normal(0, 1, n), 2.0)
    theta *= 12 * D2R / (theta.std() + 1e-9)
    phi = loc_thap(rng.normal(0, 1, n), 2.0)
    phi *= 12 * D2R / (phi.std() + 1e-9)
    rot = np.zeros(n)
    i = int(rng.uniform(10, 30) * HZ)
    while i < n:
        d = int(rng.uniform(2, 4) * HZ)
        if not tren_ban[i:i + d].any():
            rot[i:i + d] = rng.uniform(40, 70) * rng.choice([-1, 1]) * D2R
        i += d + int(rng.uniform(20, 40) * HZ)
    rot = loc_thap(loc_thap(rot, 0.25), 0.25)
    theta = (theta + song(8, 12, 0.3) * D2R) * cam_muot
    phi = (phi + rot + song(8, 12, 0.3) * D2R) * cam_muot
    q = q_euler(psi, theta, phi)
    dq = np.gradient(q, DT, axis=0)
    qc = q * np.array([1, -1, -1, -1])
    omega = 2 * qnhan(qc, dq)[:, 1:] / D2R  # °/s, hệ tay cầm
    trong_luc = np.stack([2 * (q[:, 1] * q[:, 3] - q[:, 0] * q[:, 2]), 2 * (q[:, 2] * q[:, 3] + q[:, 0] * q[:, 1]),
                          1 - 2 * (q[:, 1] ** 2 + q[:, 2] ** 2)], -1)
    wn = np.linalg.norm(omega, axis=1)
    tay = np.stack([loc_thap(rng.normal(0, 1, n), 0.05) for _ in range(3)], -1)
    # gia tốc tay: run tay vài mg + hướng tâm ω²r với r ≈ 0,1 m (200 °/s -> khoảng 0,12 g)
    tay *= ((0.003 + 0.1 * (wn * D2R) ** 2 / 9.81) * cam)[:, None] * 1.7
    acc = trong_luc + tay + rng.normal(0, 0.004, (n, 3))
    nhiet = 26 + 8 * (1 - np.exp(-t / 240))
    he_so = np.clip(rng.normal(0, 0.02, 3), -0.05, 0.05)
    sai = rng.normal(0, 0.01, 3)
    lech0 = rng.uniform(-3, 3, 3)
    buoc = np.cumsum(rng.normal(0, BUOC_NGAU_NHIEN * math.sqrt(DT), (n, 3)), axis=0)
    gyro = (1 + sai) * omega + lech0 + he_so * (nhiet - nhiet[0])[:, None] + buoc
    gyro += rng.normal(0, NHIEU_MAT_DO * math.sqrt(40), (n, 3))
    gyro = np.round(gyro / LSB) * LSB
    if os.environ.get("TROI_LY_TUONG"):  # kiểm tra bộ mô phỏng: con quay hoàn hảo thì sai lệch phải gần 0
        gyro, he_so, sai = omega.copy(), np.zeros(3), np.zeros(3)
    return dict(gyro=gyro, acc=acc, nhiet=nhiet + rng.normal(0, 0.1, n), he_so=he_so, sai=sai, q=q, yaw=yaw_cua(q))


# ---------------------------------------------------------------- bộ lọc, chạy song song mọi lần (mảng R hàng)
def chay(phien, cfg, seed):
    """Các bước của firmware (huong.h) đổi sang 100 Hz. Trả về sai lệch yaw (°), mỗi 0,1 giây."""
    rng = np.random.default_rng(seed)
    G = np.stack([p["gyro"] for p in phien])
    A = np.stack([p["acc"] for p in phien])
    T = np.stack([p["nhiet"] for p in phien])
    R, n, _ = G.shape
    k0 = int(cfg.get("can_s", 1) * HZ)
    lech = G[:, :k0].mean(1) if cfg.get("can", True) else np.zeros((R, 3))  # bù sai số tĩnh
    tb = lech.copy()
    ps = np.ones((R, 3))
    dem_yen = np.zeros(R)
    yen = np.zeros(R, bool)
    bd_yen = np.zeros(R)
    moc_lech = lech.copy()
    moc_nhiet = T[:, k0].copy()
    he_so = np.zeros((R, 3))
    if cfg.get("nhiet_luu"):
        he_so = np.stack([p["he_so"] for p in phien]) * (1 + rng.normal(0, cfg.get("nhiet_sai", 0.1), (R, 3)))
    ti_le = np.ones(R)
    if cfg.get("ti_le"):
        ti_le = 1 / (1 + np.array([p["sai"][2] for p in phien])) * (1 + rng.normal(0, 0.002, R))
    q = np.stack([p["q"][k0] for p in phien])  # bắt đầu đúng hướng thật (yaw đặt về 0 lúc cân xong)
    e_tp = np.zeros((R, 3))
    loc = cfg.get("loc", "madgwick")
    beta, kp, ki = cfg.get("beta", 0.033), cfg.get("kp", 1.0), cfg.get("ki", 0.0)
    vc = cfg.get("vung_chet_dps", 0.5) if cfg.get("vung_chet") else 0.0
    yaw_that = np.stack([p["yaw"] for p in phien])
    ra = []
    for k in range(k0, n):
        g, a, tk = G[:, k], A[:, k], T[:, k]
        tb += 0.05 * (g - tb)
        d = g - tb
        ps += 0.05 * (d * d - ps)
        an = np.linalg.norm(a, axis=1)
        yen_nay = (np.sqrt(ps.sum(1)) < 0.6) & (np.abs(an - 1) < 0.06)
        dem_yen = np.where(yen_nay, dem_yen + 1, 0)
        yen_moi = bool(cfg.get("yen", False)) & (dem_yen > HZ // 2)
        bd_yen = np.where(yen_moi & ~yen, k, bd_yen)
        het = ~yen_moi & yen & (k - bd_yen > HZ)
        if cfg.get("nhiet") and het.any():
            dT = tk - moc_nhiet
            hoc = het & (np.abs(dT) >= 0.5)
            dd = np.clip((lech - moc_lech) / np.where(np.abs(dT) < 1e-6, 1, dT)[:, None], -0.05, 0.05)
            he_so = np.where(hoc[:, None], 0.7 * he_so + 0.3 * dd, he_so)
            moc_lech = np.where(het[:, None], lech, moc_lech)
            moc_nhiet = np.where(het, tk, moc_nhiet)
        yen = yen_moi
        bu = he_so * (tk - moc_nhiet)[:, None] if cfg.get("nhiet") else 0.0
        c = g - lech - bu
        c[:, 2] *= ti_le
        lech = np.where(yen[:, None], lech + 0.02 * (tb - lech), lech)
        c = np.where(yen[:, None], 0.0, c)
        q0, q1, q2, q3 = q[:, 0], q[:, 1], q[:, 2], q[:, 3]
        ez = np.stack([2 * (q1 * q3 - q0 * q2), 2 * (q2 * q3 + q0 * q1), 1 - 2 * (q1 * q1 + q2 * q2)], -1)
        if vc > 0:
            wz = (c * ez).sum(1)
            giu = np.clip((np.abs(wz) - vc) / vc, 0, 1)
            c = c - (wz * (1 - giu))[:, None] * ez
        r = c * D2R
        hop_le = (an > 0.85) & (an < 1.15)
        a_n = a / an[:, None]
        if loc == "mahony":
            e = np.where(hop_le[:, None], np.cross(a_n, ez), 0.0)  # trọng lực đo × trọng lực ước lượng
            if ki > 0:
                e_tp = np.where(yen[:, None], e_tp, e_tp + ki * e * DT)
            r = r + kp * e + np.where(yen[:, None], 0.0, e_tp)
            qd = 0.5 * qnhan(q, np.concatenate([np.zeros((R, 1)), r], 1))
        else:
            ax, ay, az = a_n[:, 0], a_n[:, 1], a_n[:, 2]
            s0 = 4 * q0 * q2 * q2 + 2 * q2 * ax + 4 * q0 * q1 * q1 - 2 * q1 * ay
            s1 = 4 * q1 * q3 * q3 - 2 * q3 * ax + 4 * q0 * q0 * q1 - 2 * q0 * ay - 4 * q1 + 8 * q1 ** 3 + 8 * q1 * q2 * q2 + 4 * q1 * az
            s2 = 4 * q0 * q0 * q2 + 2 * q0 * ax + 4 * q2 * q3 * q3 - 2 * q3 * ay - 4 * q2 + 8 * q2 * q1 * q1 + 8 * q2 ** 3 + 4 * q2 * az
            s3 = 4 * q1 * q1 * q3 - 2 * q1 * ax + 4 * q2 * q2 * q3 - 2 * q2 * ay
            s = np.stack([s0, s1, s2, s3], -1)
            s = s / np.maximum(np.linalg.norm(s, axis=1), 1e-12)[:, None]
            qd = 0.5 * qnhan(q, np.concatenate([np.zeros((R, 1)), r], 1)) - np.where(hop_le[:, None], beta * s, 0.0)
        q = q + qd * DT
        q /= np.linalg.norm(q, axis=1)[:, None]
        if k % 10 == 0:
            ra.append(yaw_cua(q) - yaw_that[:, k] + yaw_that[:, k0] - yaw_cua(np.stack([p["q"][k0] for p in phien])))
    loi = np.array(ra).T / D2R
    return (loi + 180) % 360 - 180


BU_TINH = dict(can=True)
MOI = dict(BU_TINH, yen=True, nhiet=True, nhiet_luu=True, can_s=2, vung_chet=True, ti_le=True)
CAU_HINH = [
    ("M0", "Madgwick, không bù sai số tĩnh", dict(can=False)),
    ("M1", "Madgwick + bù sai số tĩnh (cân 1 s lúc bật)", dict(BU_TINH)),
    ("R1", "Mahony Ki = 0 + bù sai số tĩnh", dict(BU_TINH, loc="mahony")),
    ("R2", "Mahony Ki = 0,02 + bù sai số tĩnh", dict(BU_TINH, loc="mahony", ki=0.02)),
    ("M2", "M1 + học lại khi nằm yên (firmware cũ)", dict(BU_TINH, yen=True)),
    ("F1", "Firmware mới, Madgwick", dict(MOI)),
    ("F2", "Firmware mới, Mahony Ki = 0,02", dict(MOI, loc="mahony", ki=0.02)),
]


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    so_lan = int(sys.argv[1]) if len(sys.argv) > 1 else 24
    anh = sys.argv[2] if len(sys.argv) > 2 else None
    chi = os.environ.get("TROI_CHI")
    cau_hinh = [c for c in CAU_HINH if not chi or c[0] in chi.split(",")]
    for muc in filter(None, os.environ.get("TROI_THEM", "").split(";")):
        ten, d = muc.split("|")
        goc = MOI if ten.startswith("F") else BU_TINH
        cau_hinh.append((ten, d, dict(goc, **eval(d))))
    ve = {}
    kich_ban = (("Kịch bản 1: có đặt xuống bàn", True), ("Kịch bản 2: cầm liên tục 10 phút", False))
    for kb, co_ban in kich_ban:
        phien = [tao_phien(np.random.default_rng(1000 + r), co_ban) for r in range(so_lan)]
        print(f"\n{kb} ({so_lan} lần). Sai lệch yaw sau 10 phút:")
        print(f"  {'':4s}{'cấu hình':46s}{'TB':>8s}{'P90':>8s}{'TB cả phiên':>13s}")
        goc = None
        for ma, ten, cfg in cau_hinh:
            loi = chay(phien, cfg, 7)
            cuoi = np.abs(loi[:, -1])
            if ma == "M1":
                goc = cuoi.mean()
            so_sanh = f"   giảm {(1 - cuoi.mean() / goc) * 100:3.0f}% so với M1" if goc and ma != "M1" else ""
            print(f"  {ma:4s}{ten[:46]:46s}{cuoi.mean():7.2f}°{np.percentile(cuoi, 90):7.2f}°{np.abs(loi).mean():12.2f}°{so_sanh}",
                  flush=True)
            ve[(kb, ma)] = (ten, loi[0])
    if anh:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
        fig, truc = plt.subplots(1, 2, figsize=(13, 4.6))
        mau = {"M1": "#c2452b", "R1": "#d98b12", "R2": "#b06d00", "M2": "#7d9196", "F1": "#0e7c86", "F2": "#6b5bd6"}
        for ax, (kb, _) in zip(truc, kich_ban):
            for ma, _, _ in cau_hinh:
                if ma not in mau or (kb, ma) not in ve:
                    continue
                ten, y = ve[(kb, ma)]
                ax.plot(np.arange(len(y)) * 0.1 / 60, y, color=mau[ma], lw=2.2 if ma in ("M1", "F1", "F2") else 1.1,
                        label=f"{ma} {ten}")
            ax.set_title(kb + " (lần chạy 1)")
            ax.set_xlabel("phút")
            ax.set_ylabel("sai lệch yaw (°)")
            ax.axhline(0, color="#10242b", lw=0.6)
            ax.grid(alpha=0.25)
        truc[0].legend(fontsize=7.5)
        fig.tight_layout()
        fig.savefig(anh, dpi=130)
        print("\nĐã lưu", anh)


if __name__ == "__main__":
    main()
