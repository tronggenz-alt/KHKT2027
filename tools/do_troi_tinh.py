"""Đo trôi yaw khi để YÊN tay cầm VERA và đặc tính cảm biến, xuất bảng (CSV, Excel) và biểu đồ (PNG).

Ghi (tay cầm nằm yên suốt buổi, không cần bấm phím):
    python tools/ghi_tay_cam.py ghi --com COM8 --phut 15 --moc-moi 60 --im --ra du_lieu/tay_cam/troi_tinh.vera
Phân tích:
    python tools/do_troi_tinh.py du_lieu/tay_cam/troi_tinh.vera

Ra (thư mục <tên>_bao_cao/ cạnh tệp ghi):
    bang_troi_yaw.csv        trôi quanh trục đứng của từng thuật toán ở 1, 2, 5, 10, 15 phút, tốc độ trôi, đạt / chưa đạt
    bang_cam_bien.csv        độ lệch, nhiễu, ARW, bất ổn định độ lệch (Allan) từng trục con quay; gia tốc; nhiệt; lấy mẫu
    bang_allan.csv           độ lệch Allan theo τ
    bang_cam_ung_joystick.csv  nút cảm ứng (mức nền, nhiễu, số lần chạm ma), joystick (tâm, nhiễu)
    troi_tinh.xlsx           cả 4 bảng trong một tệp Excel, kèm biểu đồ
    hinh_*.png               biểu đồ

Trôi được tính bằng góc XOẮN quanh trục đứng (ghi_tay_cam.xoan_dung), không phải yaw Euler, nên đúng ở mọi tư thế đặt.
"""
import argparse
import csv
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ghi_tay_cam as gt  # noqa: E402

MUC_PHUT = (1, 2, 5, 10)
CHI_TIEU_10PH = 1.0  # PDF tích hợp trang 8: trôi yaw khi để yên < 1° / 10 phút


def allan(x, fs, so_tau=40):
    """Độ lệch Allan chồng lấp của chuỗi vận tốc góc x (°/s) lấy mẫu fs Hz. Trả về (tau s, sigma °/s)."""
    n = len(x)
    th = np.cumsum(np.r_[0.0, x]) / fs  # góc tích luỹ
    m_max = n // 9
    ms = np.unique(np.logspace(0, math.log10(max(m_max, 2)), so_tau).astype(int))
    taus, sig = [], []
    for m in ms:
        tau = m / fs
        d = th[2 * m:] - 2 * th[m:-m] + th[:-2 * m]
        if len(d) < 2:
            break
        taus.append(tau)
        sig.append(math.sqrt((d * d).sum() / (2 * tau * tau * len(d))))
    return np.array(taus), np.array(sig)


def dac_trung_allan(tau, sig):
    """ARW (°/√h) đọc ở τ = 1 s trên đường dốc −1/2; bất ổn định độ lệch (°/h) = đáy / 0,664."""
    arw = float(np.interp(0.0, np.log10(tau), np.log10(sig)))
    arw = 10 ** arw * 60  # σ(1 s) [°/s] × √3600 = °/√h
    k = int(np.argmin(sig))
    return arw, sig[k] / 0.664 * 3600, tau[k]


def ghi_csv(ten, dau, hang):
    with open(ten, "w", newline="", encoding="utf-8-sig") as f:  # có BOM để Excel đọc đúng tiếng Việt
        w = csv.writer(f)
        w.writerow(dau)
        w.writerows(hang)


def chinh(v, so=3):
    return round(float(v), so) if v is not None and np.isfinite(v) else ""


def main():
    p = argparse.ArgumentParser(description="Đo trôi yaw khi để yên và đặc tính cảm biến tay cầm VERA")
    p.add_argument("tep")
    p.add_argument("--khong-hien", action="store_true")
    args = p.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    du = gt.doc_tep(args.tep)
    ts = gt.doc_thong_so(du["chu"])
    lo = du["lo"]
    G, A, T, t = du["G"], du["A"], du["T"], du["t"]
    n = len(G)
    fs = 1.0 / np.median(du["DT"])
    k0 = min(400, n // 4)
    thu_muc = os.path.splitext(args.tep)[0] + "_bao_cao"
    os.makedirs(thu_muc, exist_ok=True)
    print(f"{args.tep}: {n} mẫu, {t[-1] / 60:.2f} phút, {fs:.1f} Hz, mất {du['mat']} mẫu")

    # ---------------- 1. trôi quanh trục đứng của từng thuật toán ----------------
    cfgs = gt.cau_hinh(ts)
    print("Chạy lại các thuật toán trên cùng chuỗi số đo...")
    yaw, lech_z = gt.chay_lai(du, [c[3] for c in cfgs], k0)
    j0 = min(int(np.searchsorted(lo["vi_tri"], k0)), len(lo["q"]) - 1)
    yaw_fw = np.interp(np.arange(n), lo["vi_tri"], gt.xoan_dung(lo["q"], lo["q"][j0]))

    # tích phân thô quanh trục đứng (không bù gì, và chỉ bù độ lệch 2 s đầu): mốc so sánh
    g_len = A[:k0].mean(0)
    g_len /= np.linalg.norm(g_len)          # hướng "lên" trong hệ chip (gia tốc kế đo phản lực)
    w_dung = G @ g_len                      # vận tốc góc quanh trục đứng, °/s
    tho = np.cumsum(w_dung * du["DT"])
    tho -= tho[k0]
    bu_tinh = np.cumsum((w_dung - w_dung[:k0].mean()) * du["DT"])
    bu_tinh -= bu_tinh[k0]

    hang_tt = [("T0", "Tích phân thô, không bù độ lệch", "#9a3412", tho),
               ("T1", "Tích phân, chỉ bù độ lệch 2 s đầu", "#c2410c", bu_tinh)]
    hang_tt += [(ma, ten, mau, yaw[i] - yaw[i][k0]) for i, (ma, ten, mau, _) in enumerate(cfgs)]
    hang_tt.append(("FW", "Firmware trên mạch (hướng gửi về)", "#10242b", yaw_fw - yaw_fw[k0]))

    t_rel = t - t[k0]
    bang_troi = []
    dau_troi = ["Mã", "Thuật toán"] + [f"Trôi sau {m} phút (°)" for m in MUC_PHUT] + \
               ["|Trôi| lớn nhất (°)", "Tốc độ trôi (°/phút)", f"Đạt < {CHI_TIEU_10PH:g}°/10 phút"]
    for ma, ten, mau, y in hang_tt:
        o = [ma, ten]
        for m in MUC_PHUT:
            k = np.searchsorted(t_rel, m * 60)
            o.append(chinh(y[k], 3) if k < n else "")
        o.append(chinh(y[-1], 3))
        o.append(chinh(np.abs(y[k0:]).max(), 3))
        doc = np.polyfit(t_rel[k0:] / 60, y[k0:], 1)[0]
        o.append(chinh(doc, 4))
        k10 = np.searchsorted(t_rel, 600)
        if k10 < n:
            o.append("Đạt" if np.abs(y[k0:k10 + 1]).max() < CHI_TIEU_10PH else "Chưa đạt")
        else:  # ghi chưa đủ 10 phút: suy theo tốc độ trôi
            o.append(("Đạt" if abs(doc) * 10 < CHI_TIEU_10PH else "Chưa đạt") + " (suy theo tốc độ)")
        bang_troi.append(o)
    ghi_csv(os.path.join(thu_muc, "bang_troi_yaw.csv"), dau_troi, bang_troi)

    # ---------------- 2. đặc tính con quay (Allan), gia tốc, nhiệt, lấy mẫu ----------------
    truc = [("Con quay X chip", G[:, 0]), ("Con quay Y chip", G[:, 1]), ("Con quay Z chip", G[:, 2]),
            ("Con quay quanh trục ĐỨNG", w_dung)]
    allan_kq = []
    bang_cb = []
    dau_cb = ["Đại lượng", "Giá trị", "Đơn vị", "Ghi chú"]
    for ten, x in truc:
        tau, sig = allan(x, fs)
        arw, bi, tau_bi = dac_trung_allan(tau, sig)
        allan_kq.append((ten, tau, sig))
        bang_cb += [
            [f"{ten}: độ lệch trung bình", chinh(x.mean(), 4), "°/s", "trước khi firmware trừ độ lệch"],
            [f"{ten}: nhiễu (độ lệch chuẩn 200 Hz)", chinh(x.std(), 4), "°/s", ""],
            [f"{ten}: ARW (bước ngẫu nhiên góc)", chinh(arw, 3), "°/√h", "Allan ở τ = 1 s"],
            [f"{ten}: bất ổn định độ lệch", chinh(bi, 2), "°/h", f"đáy Allan / 0,664, ở τ = {tau_bi:.0f} s"],
        ]
    a_tb = A.mean(0)
    pitch_a = math.degrees(math.atan2(-a_tb[0], math.hypot(a_tb[1], a_tb[2])))
    roll_a = math.degrees(math.atan2(a_tb[1], a_tb[2]))
    bang_cb += [
        ["Gia tốc trung bình X, Y, Z chip", f"{a_tb[0]:+.4f}, {a_tb[1]:+.4f}, {a_tb[2]:+.4f}", "g", "nằm ngang đúng trục thì ≈ 0, 0, +1"],
        ["|a| trung bình", chinh(np.linalg.norm(a_tb), 4), "g", "lệch 1 g: sai tỉ lệ gia tốc kế"],
        ["Nhiễu gia tốc (độ lệch chuẩn |a|)", chinh(np.linalg.norm(A, axis=1).std() * 1000, 2), "mg", ""],
        ["Pitch, roll suy từ gia tốc", f"{pitch_a:+.1f}, {roll_a:+.1f}", "°", "pitch gần ±90°: module đang dựng đứng"],
        ["Nhiệt độ BMI160 đầu → cuối", f"{T[0]:.2f} → {T[-1]:.2f}", "°C", f"tăng {T[-1] - T[0]:+.2f} °C"],
        ["Tần số lấy mẫu thực", chinh(fs, 2), "Hz", "mục tiêu 200"],
        ["Dao động chu kỳ lấy mẫu (độ lệch chuẩn)", chinh(du["DT"].std() * 1000, 3), "ms", ""],
        ["Mẫu bị mất", du["mat"], "mẫu", f"trên {n}"],
        ["Hệ số nhiệt firmware đang dùng", ", ".join(f"{v:.4f}" for v in ts["he_so"]), "°/s/°C",
         "chưa đo bằng lệnh n" if not ts["do_bang_n"] else "đo bằng lệnh n"],
        ["Tỉ lệ con quay Z (lệnh g)", chinh(ts["ti_le"], 4), "", "1.0000 = chưa hiệu chuẩn"],
    ]
    # độ lệch trục đứng theo nhiệt: trung bình từng phút
    phut = (t // 60).astype(int)
    w_ph = np.array([w_dung[phut == m].mean() for m in np.unique(phut)])
    T_ph = np.array([T[phut == m].mean() for m in np.unique(phut)])
    if T_ph.max() - T_ph.min() > 0.3:
        hs = np.polyfit(T_ph, w_ph, 1)[0]
        bang_cb.append(["Độ lệch trục đứng đổi theo nhiệt", chinh(hs, 4), "°/s/°C", "hồi quy theo trung bình từng phút"])
    else:
        bang_cb.append(["Độ lệch trục đứng đổi theo nhiệt", "", "°/s/°C",
                        f"nhiệt chỉ đổi {T_ph.max() - T_ph.min():.2f} °C, chưa đủ để đo"])
    ghi_csv(os.path.join(thu_muc, "bang_cam_bien.csv"), dau_cb, bang_cb)
    ghi_csv(os.path.join(thu_muc, "bang_allan.csv"), ["τ (s)"] + [a[0] + " σ (°/s)" for a in allan_kq],
            [[chinh(allan_kq[0][1][i], 4)] + [chinh(a[2][i], 6) if i < len(a[2]) else "" for a in allan_kq]
             for i in range(len(allan_kq[0][1]))])

    # ---------------- 3. cảm ứng, joystick khi để yên ----------------
    bit = lo["bit"]
    bang_cj = []
    for nut, tho_n, nen_n, muc_n, b in (("A", lo["tho_a"], lo["nen_a"], lo["muc_a"], 1), ("B", lo["tho_b"], lo["nen_b"], lo["muc_b"], 2)):
        cham = (bit & b) > 0
        so_lan = int((np.diff(cham.astype(int)) == 1).sum() + (1 if cham[0] else 0))
        bang_cj += [
            [f"Nút {nut}: số đo thô trung bình", chinh(tho_n.mean(), 1), "đơn vị chạm", ""],
            [f"Nút {nut}: nhiễu số đo thô (độ lệch chuẩn)", chinh(tho_n.std(), 2), "đơn vị chạm",
             f"= {tho_n.std() / tho_n.mean() * 100:.2f}% mức nền"],
            [f"Nút {nut}: mức nền trôi đầu → cuối", f"{nen_n[0]:.0f} → {nen_n[-1]:.0f}", "đơn vị chạm", ""],
            [f"Nút {nut}: mức 0–255 lớn nhất khi không chạm", int(muc_n.max()), "", "ngưỡng lơ lửng 38, chạm 128"],
            [f"Nút {nut}: số lần CHẠM MA", so_lan, "lần", f"trong {t[-1] / 60:.1f} phút (mục tiêu 0)"],
        ]
    joy, mv = lo["joy"], lo["mv"]
    nhan = (bit & 4) > 0
    bang_cj += [
        ["Joystick X ra khi buông: trung bình", chinh(joy[:, 0].mean(), 4), "−1..1", "phải ≈ 0"],
        ["Joystick Y ra khi buông: trung bình", chinh(joy[:, 1].mean(), 4), "−1..1", "phải ≈ 0"],
        ["Joystick X, Y: nhiễu (độ lệch chuẩn)", f"{joy[:, 0].std():.4f}, {joy[:, 1].std():.4f}", "−1..1", ""],
        ["Joystick điện áp X, Y trung bình", f"{mv[:, 0].mean():.0f}, {mv[:, 1].mean():.0f}", "mV", "KY-023 cấp 3V3: tâm ≈ 1650"],
        ["Joystick sau vùng chết 8% có về 0 không", "Có" if np.hypot(joy[:, 0], joy[:, 1]).max() < 0.08 else "KHÔNG",
         "", "lớn nhất " + f"{np.hypot(joy[:, 0], joy[:, 1]).max():.3f}"],
        ["Nhấn cần ma", int((np.diff(nhan.astype(int)) == 1).sum()), "lần", "mục tiêu 0"],
    ]
    ghi_csv(os.path.join(thu_muc, "bang_cam_ung_joystick.csv"), ["Đại lượng", "Giá trị", "Đơn vị", "Ghi chú"], bang_cj)

    # ---------------- 4. biểu đồ ----------------
    ph = t_rel / 60
    fig, (a1, a2) = plt.subplots(2, 1, figsize=(13, 9))
    for ma, ten, mau, y in hang_tt:
        if ma.startswith("F-"):
            continue
        a1.plot(ph[k0:], y[k0:], color=mau, lw=2.2 if ma in ("F1", "FW", "M1", "T1") else 1.0,
                ls="--" if ma in ("FW", "T0") else "-", label=f"{ma} {ten}")
    a1.set_title("Trôi quanh trục đứng khi tay cầm nằm YÊN (đo thật, cùng một lần ghi)")
    a1.set_ylabel("độ (°)")
    a1.grid(alpha=0.25)
    a1.legend(fontsize=8, ncol=2)
    for ma, ten, mau, y in hang_tt:
        if ma in ("F1", "FW", "C0", "F-n", "F-v", "F-g"):
            a2.plot(ph[k0:], y[k0:], color=mau, lw=2 if ma in ("F1", "FW") else 1, ls="--" if ma == "FW" else "-", label=f"{ma} {ten}")
    a2.axhspan(-CHI_TIEU_10PH, CHI_TIEU_10PH, color="#16a34a", alpha=0.08, label=f"chỉ tiêu ±{CHI_TIEU_10PH:g}° / 10 phút")
    a2.set_title("Phóng to: firmware mới, firmware cũ, bản bỏ từng kỹ thuật")
    a2.set_xlabel("phút")
    a2.set_ylabel("độ (°)")
    a2.grid(alpha=0.25)
    a2.legend(fontsize=8, ncol=2)
    fig.tight_layout()
    fig.savefig(os.path.join(thu_muc, "hinh_troi_yaw.png"), dpi=120)

    fig, ax = plt.subplots(figsize=(9, 6))
    for (ten, tau, sig), mau in zip(allan_kq, ("#c2452b", "#0e7c86", "#6b5bd6", "#10242b")):
        ax.loglog(tau, sig * 3600, color=mau, lw=2.2 if "ĐỨNG" in ten else 1.2, label=ten)
    ax.set_xlabel("τ (s)")
    ax.set_ylabel("độ lệch Allan (°/h)")
    ax.set_title("Độ lệch Allan con quay BMI160 (dốc −½: nhiễu trắng; đáy: bất ổn định độ lệch)")
    ax.grid(alpha=0.3, which="both")
    ax.legend()
    fig.tight_layout()
    fig.savefig(os.path.join(thu_muc, "hinh_allan.png"), dpi=120)

    fig, (b1, b2) = plt.subplots(2, 1, figsize=(13, 7), sharex=True)
    b1.plot(t / 60, T, color="#d98b12")
    b1.set_ylabel("nhiệt BMI160 (°C)")
    b1.grid(alpha=0.25)
    w_loc = np.convolve(w_dung, np.ones(1000) / 1000, mode="same")
    b2.plot(t / 60, w_loc, color="#6b5bd6", lw=1, label="vận tốc góc quanh trục đứng, trung bình 5 s (thô)")
    i_f1 = [c[0] for c in cfgs].index("F1")
    lz = lech_z[i_f1]
    b2.axhline(w_dung[:k0].mean(), color="#c2452b", ls="--", lw=1, label="độ lệch đo 2 s đầu")
    b2.set_ylabel("°/s")
    b2.set_xlabel("phút")
    b2.grid(alpha=0.25)
    b2.legend(fontsize=8)
    b1.set_title("Nhiệt độ và độ lệch con quay quanh trục đứng theo thời gian")
    fig.tight_layout()
    fig.savefig(os.path.join(thu_muc, "hinh_nhiet_do_lech.png"), dpi=120)
    del lz

    # ---------------- 5. Excel ----------------
    try:
        from openpyxl import Workbook
        from openpyxl.styles import Alignment, Font, PatternFill
        wb = Workbook()
        tieu_de = Font(bold=True, color="FFFFFF")
        nen = PatternFill("solid", fgColor="0E7C86")
        for i, (ten_trang, dau, hang) in enumerate((("Trôi yaw", dau_troi, bang_troi), ("Cảm biến", dau_cb, bang_cb),
                                                   ("Cảm ứng + joystick", ["Đại lượng", "Giá trị", "Đơn vị", "Ghi chú"], bang_cj))):
            ws = wb.active if i == 0 else wb.create_sheet()
            ws.title = ten_trang
            ws.append(dau)
            for h in hang:
                ws.append(list(h))
            for c in ws[1]:
                c.font, c.fill = tieu_de, nen
                c.alignment = Alignment(wrap_text=True, vertical="center")
            for cot in ws.columns:
                ws.column_dimensions[cot[0].column_letter].width = min(48, max(10, max(len(str(c.value or "")) for c in cot) + 2))
            ws.freeze_panes = "A2"
        try:
            from openpyxl.drawing.image import Image
            for ten_anh in ("hinh_troi_yaw.png", "hinh_allan.png", "hinh_nhiet_do_lech.png"):
                ws = wb.create_sheet(ten_anh[5:-4].replace("_", " "))
                ws.add_image(Image(os.path.join(thu_muc, ten_anh)), "A1")
        except ImportError:
            pass
        wb.save(os.path.join(thu_muc, "troi_tinh.xlsx"))
    except ImportError:
        print("Chưa có openpyxl: bỏ qua tệp Excel, vẫn có CSV")

    # ---------------- 6. in bảng ----------------
    print(f"\nBẢNG 1. Trôi quanh trục đứng khi để yên (chỉ tiêu < {CHI_TIEU_10PH:g}° / 10 phút)")
    print(" | ".join(dau_troi))
    for h in bang_troi:
        print(" | ".join(str(v) for v in h))
    print("\nBẢNG 2. Đặc tính cảm biến")
    for h in bang_cb:
        print(" | ".join(str(v) for v in h))
    print("\nBẢNG 3. Cảm ứng và joystick khi không chạm")
    for h in bang_cj:
        print(" | ".join(str(v) for v in h))
    print(f"\nĐã lưu bảng và hình vào {thu_muc}")


if __name__ == "__main__":
    main()
