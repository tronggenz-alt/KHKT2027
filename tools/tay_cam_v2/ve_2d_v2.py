"""Bản vẽ 2D tay cầm VR V2, chiếu và cắt thẳng từ mô hình 3D (docs/tay-cam-v2/hinh_chieu.json).

Chạy sau mo_hinh.py:  python tools/tay_cam_v2/ve_2d_v2.py
Ra: docs/tay-cam-v2/TC2-01..05.svg (rồi xuất PNG bằng tools/tay_cam_v2/xuat_png.sh)
"""
import json
import math
import os
import sys

DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(DIR), "ve_tay_cam"))
from ve_2d import Svg, fmt  # noqa: E402

GOC = os.path.dirname(os.path.dirname(DIR))
THU_MUC = os.path.join(GOC, "docs", "tay-cam-v2")

THEM_CSS = """
.vo{fill:var(--pvc,#dde5e3);stroke:var(--ink,#18272c);stroke-width:1.1;fill-rule:evenodd}
.toi{fill:#2a3238;stroke:var(--ink,#18272c);stroke-width:1;fill-rule:evenodd}
.hl{fill:var(--halo,#11b5d4);stroke:var(--ink,#18272c);stroke-width:1;fill-rule:evenodd}
.vien{fill:none;stroke:var(--ink,#18272c);stroke-width:1.6;stroke-linejoin:round}
.cutv{fill:url(#gach);stroke:var(--ink,#18272c);stroke-width:1.1;fill-rule:evenodd}
.lkf{stroke:var(--ink,#18272c);stroke-width:.8;fill-rule:evenodd}
.chart{fill:none;stroke:var(--dim,#0a6b8a);stroke-width:2.2}
.grid{fill:none;stroke:var(--rule,#c8d2d0);stroke-width:.7}
.tay{fill:var(--hand,#e9b98f);fill-opacity:.55;stroke:var(--hand-ln,#9c5f37);stroke-width:1.1}
.cam{fill:var(--ink-2,#5d6e72)}
.tia{fill:none;stroke:var(--halo,#11b5d4);stroke-width:1;stroke-dasharray:5 4}
.code{fill:var(--ink,#18272c);font:500 12px 'JetBrains Mono',ui-monospace,monospace}
"""
MAU_LK = {"esp32": "#1d2b33", "pin": "#8a9aa3", "tp4056": "#2563a6", "ky023": "#30353a", "bmi160": "#7b4fd6",
          "hall": "#c0392b", "nam_cham": "#9aa3a8"}


def doc():
    with open(os.path.join(THU_MUC, "hinh_chieu.json"), encoding="utf-8") as fh:
        return json.load(fh)


def duong(polys, X, Y):
    out = []
    for poly in polys:
        if len(poly) < 3:
            continue
        out.append("M" + " L".join(f"{fmt(X(p[0]))},{fmt(Y(p[1]))}" for p in poly) + " Z")
    return " ".join(out)


def bb(polys):
    xs = [p[0] for poly in polys for p in poly]
    ys = [p[1] for poly in polys for p in poly]
    return min(xs), min(ys), max(xs), max(ys)


def to_mau(s, polys, X, Y, cls, extra=""):
    d = duong(polys, X, Y)
    if d:
        s.path(d, cls, extra)


def khung2(s, so, ten, phu):
    s.add(f"<style>{THEM_CSS}</style>")
    s.rect(6, 6, s.w - 12, s.h - 12, "paper", rx=4)
    s.rect(14, 14, s.w - 28, s.h - 28, "thin")
    w, h = 540, 74
    x, y = s.w - 14 - w, s.h - 14 - h
    s.rect(x, y, w, h, "ln")
    s.line(x, y + 30, x + 410, y + 30, "thin")
    s.line(x + 410, y, x + 410, y + h, "thin")
    s.line(x + 410, y + 30, x + w, y + 30, "thin")
    s.line(x + 410, y + 52, x + w, y + 52, "thin")
    s.text(x + 10, y + 21, ten, "h")
    s.text(x + 10, y + 47, phu, "ts")
    s.text(x + 10, y + 65, "Đơn vị: mm · Dự án KHKT Phòng thí nghiệm ảo · 27/10/2026", "ts")
    s.text(x + 420, y + 13, "BẢN VẼ SỐ", "hs")
    s.text(x + 420, y + 27, so, "tb")
    s.text(x + 420, y + 45, "Tay cầm V2 · Halo", "ts")
    s.text(x + 420, y + 67, "Chiếu góc thứ nhất", "ts")


def cung_goc(s, cx, cy, r, a0, a1, nhan):
    """Cung đo góc (độ, chiều toán học, trục y giấy hướng xuống đã tính)."""
    p0 = (cx + r * math.cos(math.radians(a0)), cy - r * math.sin(math.radians(a0)))
    p1 = (cx + r * math.cos(math.radians(a1)), cy - r * math.sin(math.radians(a1)))
    lon = 1 if abs(a1 - a0) > 180 else 0
    quet = 0 if a1 > a0 else 1
    s.path(f"M{fmt(p0[0])},{fmt(p0[1])} A{fmt(r)},{fmt(r)} 0 {lon} {quet} {fmt(p1[0])},{fmt(p1[1])}", "dim")
    am = math.radians((a0 + a1) / 2)
    s.text(cx + (r + 14) * math.cos(am), cy - (r + 14) * math.sin(am) + 4, nhan, "dimt", "middle")


# ---------------- TC2-01 ba hình chiếu ----------------

def tc01(h):
    s = Svg(1180, 820, 21)
    khung2(s, "TC2-01", "TAY CẦM VR HALO · BA HÌNH CHIẾU", "Chiếu từ mô hình 3D; vỏ 2 nửa PETG/PLA, dày 2,2")
    k = 2.5
    ph = h["phan"]
    tp = h["tham_so"]
    # hình chiếu đứng (nhìn từ phải)
    ox, oy = 470, 215
    X = lambda u: ox + u * k
    Y = lambda v: oy - v * k
    s.text(330, 46, "HÌNH CHIẾU ĐỨNG", "h")
    s.text(330, 62, "nhìn từ phía phải, mũi tay cầm hướng sang phải", "ts")
    to_mau(s, ph["vo_trai"]["canh"], X, Y, "vo")
    for ma, cls in (("tam_op", "toi"), ("co", "toi"), ("nap_day", "toi"), ("halo", "hl"), ("dai_sang", "hl")):
        to_mau(s, ph[ma]["canh"], X, Y, cls)
    for ma, polys in h["linh_kien_canh"].items():
        to_mau(s, polys, X, Y, "hid")
    to_mau(s, h["tong"]["canh"], X, Y, "vien")
    x0, z0, x1, z1 = bb(h["tong"]["canh"])
    s.dimh(X(x0), X(x1), Y(z0) + 34, f"{x1 - x0:.0f}", Y(z0), Y(z0) + 2)
    s.dimv(Y(z1), Y(z0), X(x1) + 30, f"{z1 - z0:.0f}", X(x1), X(x1), side=1)
    # góc cán 20° và mặt joystick 15°
    g = math.radians(tp["nghieng_can"])
    s.line(X(0), Y(0), X(-108 * math.sin(g)), Y(-108 * math.cos(g)), "ctr")
    s.line(X(0), Y(0), X(0), Y(-75), "dim")
    cung_goc(s, X(0), Y(0), 150, -90, -90 - tp["nghieng_can"], f"{tp['nghieng_can']}°")
    j = tp["tam_can_joy"]
    gm = math.radians(tp["nghieng_mat"])
    s.line(X(j[0] - 30 * math.cos(gm)), Y(j[2] - 30 * math.sin(gm)), X(j[0] + 32 * math.cos(gm)), Y(j[2] + 32 * math.sin(gm)), "ctr")
    s.line(X(j[0]), Y(j[2]), X(j[0] - 40), Y(j[2]), "dim")
    cung_goc(s, X(j[0]), Y(j[2]), 80, 180, 180 + tp["nghieng_mat"], f"{tp['nghieng_mat']}°")
    s.leader(X(56), Y(40), X(56) + 30, Y(40) - 30, "vòng halo Ø70")
    s.leader(X(31), Y(-22), X(31) + 34, Y(-22) + 38, "cò Hall")

    # hình chiếu cạnh (nhìn từ trước) đặt bên trái
    ox2 = 150
    U = lambda u: ox2 + u * k
    s.text(40, 46, "HÌNH CHIẾU CẠNH", "h")
    s.text(40, 62, "nhìn từ phía trước", "ts")
    to_mau(s, ph["vo_trai"]["truoc"], U, Y, "vo")
    to_mau(s, ph["vo_phai"]["truoc"], U, Y, "vo")
    for ma, cls in (("tam_op", "toi"), ("nap_day", "toi"), ("co", "toi"), ("halo", "hl")):
        to_mau(s, ph[ma]["truoc"], U, Y, cls)
    to_mau(s, h["tong"]["truoc"], U, Y, "vien")
    a0, b0, a1, b1 = bb(ph["halo"]["truoc"])
    s.dimh(U(a0), U(a1), Y(b1) - 18, f"Ø{a1 - a0:.0f}", Y(b1), Y(b1))
    v0, _, v1, _ = bb(ph["vo_trai"]["truoc"] + ph["vo_phai"]["truoc"])
    s.dimh(U(v0), U(v1), Y(-114) + 34, f"{v1 - v0:.0f} (đầu)", Y(-60), Y(-60))
    s.dimh(U(-16.5), U(16.5), Y(-60), "33", None, None, text_dy=-4)
    s.line(U(-16.5), Y(-60) - 6, U(-16.5), Y(-60) + 6, "dim")
    s.line(U(16.5), Y(-60) - 6, U(16.5), Y(-60) + 6, "dim")

    # hình chiếu bằng (nhìn từ trên) đặt dưới hình chiếu đứng
    oy3 = 690
    Yt = lambda v: oy3 - v * k
    s.text(40, 600, "HÌNH CHIẾU BẰNG", "h")
    s.text(40, 616, "nhìn từ trên xuống (dưới hình chiếu đứng)", "ts")
    to_mau(s, ph["vo_trai"]["tren"], X, Yt, "vo")
    to_mau(s, ph["vo_phai"]["tren"], X, Yt, "vo")
    for ma, cls in (("nap_day", "toi"), ("tam_op", "toi"), ("halo", "hl")):
        to_mau(s, ph[ma]["tren"], X, Yt, cls)
    to_mau(s, h["tong"]["tren"], X, Yt, "vien")
    s.circle(X(j[0]), Yt(0), 11.5 * k, "thin")
    s.leader(X(j[0]) + 8, Yt(0) - 26, X(j[0]) - 40, Yt(0) - 78 + 30, "lỗ cần Ø23", anchor="end")

    # mặt cắt B–B giữa cán
    cx, cy, kk = 930, 300, 5.0
    s.text(800, 120, "MẶT CẮT B–B", "h")
    s.text(800, 136, "vuông góc trục cán, cách đỉnh cán 45 mm", "ts")
    a, b, t = 18.5, 16.5, 2.2
    s.add(f'<ellipse cx="{cx}" cy="{cy}" rx="{a * kk}" ry="{b * kk}" class="cutv"/>')
    s.add(f'<ellipse cx="{cx}" cy="{cy}" rx="{(a - t) * kk}" ry="{(b - t) * kk}" class="paper" style="stroke:var(--ink,#18272c);stroke-width:1.1"/>')
    for sx in (-1, 1):
        s.rect(cx + sx * 13.2 * kk - (0 if sx > 0 else 4.4 * kk), cy - 3.5 * kk, 4.4 * kk if sx > 0 else 4.4 * kk, 7 * kk, "cutv")
    s.rect(cx - 14 * kk, cy - 0.8 * kk, 28 * kk, 1.6 * kk, "lkf", extra='style="fill:#1d2b33"')
    s.rect(cx - 9 * kk, cy + 0.8 * kk, 18 * kk, 3.2 * kk, "lkf", extra='style="fill:#55626a"')
    s.add(f'<ellipse cx="{cx}" cy="{cy - (b - 0.6) * kk}" rx="{2 * kk}" ry="{0.6 * kk}" class="hl"/>')
    s.dimh(cx - a * kk, cx + a * kk, cy + b * kk + 30, "37", cy, cy)
    s.dimv(cy - b * kk, cy + b * kk, cx - a * kk - 30, "33", cx, cx)
    s.leader(cx - (a - 1) * kk, cy - 6 * kk, cx - a * kk - 14, cy - b * kk - 16, "vỏ 2,2", anchor="end")
    s.leader(cx + 13.5 * kk, cy - 2 * kk, cx + a * kk + 16, cy - 40, "rãnh kẹp ESP32", anchor="start")
    s.leader(cx + 4 * kk, cy + 2.4 * kk, cx + a * kk + 16, cy + 34, "module ESP32", anchor="start", sub="(BMI160 dán trên)")
    s.leader(cx, cy - (b - 0.6) * kk, cx + 30, cy - b * kk - 30, "dải màu", anchor="start")
    s.text(800, 470, "Cán 37 × 33 mm (≈ Ø35) là tối ưu cho bàn tay dài 177 mm:", "ts")
    s.text(800, 486, "đường kính = 19,7% chiều dài bàn tay (Kong và Lowe, 2005).", "ts")
    gx, gy = 800, 540
    s.text(gx, gy, "THÔNG SỐ CHÍNH", "hs")
    for i, (a_, b_) in enumerate([("Dài × cao × rộng", f"{x1 - x0:.0f} × {z1 - z0:.0f} × 70 mm"), ("Cán cầm", "37 × 33, dài 112, ngả 20°"),
                                  ("Mặt joystick", "ngửa 15°, tấm ốp 40 × 27"), ("Vòng halo", "Ø70, bản 6, cách mũi 3 mm"),
                                  ("Khối lượng in", f"{sum(t_['gam_pla'] for t_ in h['thong_so'])} g nhựa (PLA)")]):
        s.text(gx, gy + 22 + i * 20, a_, "ts")
        s.text(gx + 130, gy + 22 + i * 20, b_, "t")
    return s


# ---------------- TC2-02 mặt cắt dọc + bố trí ----------------

def tc02(h):
    s = Svg(1180, 820, 22)
    khung2(s, "TC2-02", "TAY CẦM VR HALO · MẶT CẮT DỌC", "Cắt theo mặt ghép hai nửa vỏ (y = 0)")
    k = 3.7
    ox, oy = 290, 220
    X = lambda u: ox + u * k
    Y = lambda v: oy - v * k
    s.text(40, 46, "MẶT CẮT A–A", "h")
    s.text(40, 62, "theo mặt ghép hai nửa vỏ, nhìn từ phía phải", "ts")
    c = h["cat_y0"]
    for ma, polys in h["linh_kien_canh"].items():
        to_mau(s, polys, X, Y, "lkf", f'style="fill:{MAU_LK[ma]};fill-opacity:.9"')
    to_mau(s, c["vo"], X, Y, "cutv")
    for ma, cls in (("co", "toi"), ("nap_day", "toi"), ("tam_op", "toi"), ("halo", "hl"), ("dai_sang", "hl")):
        to_mau(s, c[ma], X, Y, cls)
    tp = h["tham_so"]
    cc = tp["chot_co"]
    s.circle(X(cc[0]), Y(cc[2]), 1.6 * k, "ball")
    lk = {m: bb(p) for m, p in h["linh_kien_canh"].items()}
    tam = lambda m: (X((lk[m][0] + lk[m][2]) / 2), Y((lk[m][1] + lk[m][3]) / 2))
    muc = [
        (1, "Vỏ hai nửa, ghép 2 vít M2 × 10", X(-24.5), Y(-12)),
        (2, "Tấm ốp mặt joystick (in màu tối)", X(14), Y(24.4)),
        (3, "Joystick KY-023 (bẻ thẳng chân, hàn dây)", *tam("ky023")),
        (4, "Cò Hall, quay quanh vít M3", X(32), Y(-12)),
        (5, "Nam châm Ø6 × 3 dán ở mặt sau cò", *tam("nam_cham")),
        (6, "Cảm biến Hall SS49E trong vách đỡ", *tam("hall")),
        (7, "ESP32 DevKit 38 chân (không hàn header)", *tam("esp32")),
        (8, "BMI160 dán trên vỏ module ESP32", *tam("bmi160")),
        (9, "Pin LiPo 40 × 25 × 9 (tính sau)", *tam("pin")),
        (10, "Mạch sạc TP4056 USB-C", *tam("tp4056")),
        (11, "Nắp đáy ôm hai nửa vỏ, có tai dây đeo", X(-36), Y(-110)),
        (12, "Vòng halo Ø70, 2 vít M3 vào hai bên đầu", X(56), Y(40)),
        (13, "Dải màu huỳnh quang dán rãnh lưng cán", X(-37.6), Y(-50)),
    ]
    lx = 640
    for i, (n, ten, px, py) in enumerate(muc):
        ty = 120 + i * 40
        s.circle(px, py, 2, "dot")
        s.path(f"M{fmt(px)},{fmt(py)} L{fmt(lx - 40)},{fmt(ty)} L{fmt(lx - 12)},{fmt(ty)}", "thin")
        s.circle(lx, ty, 10, "ball")
        s.text(lx, ty + 4, str(n), "ballt", "middle")
        s.text(lx + 18, ty + 4, ten, "t")
    s.leader(X(cc[0]), Y(cc[2]), X(cc[0]) + 40, Y(cc[2]) - 120, "chốt cò")
    s.text(40, 700, "Vách Hall đồng thời là chặn hành trình cò. Rãnh kẹp ESP32 nằm ở hai mép mạch (xem mặt cắt B–B, TC2-01).", "ts")
    s.text(40, 718, "Dây đi phía sau vách Hall từ đầu xuống cán; đáy cán để hở cho mạch sạc, nắp đáy đậy lại và kẹp hai nửa vỏ.", "ts")
    return s


# ---------------- TC2-03 cò Hall ----------------

def tu_truong(khe, Br=1.2, R=3.0, L=3.0):
    """Từ trường trên trục nam châm trụ (gauss) ở khoảng cách khe (mm) tính từ mặt cực."""
    z = khe
    return Br / 2 * ((z + L) / math.hypot(R, z + L) - z / math.hypot(R, z)) * 1e4


def quay_cw(p, tam, goc):
    t = math.radians(goc)
    x, z = p[0] - tam[0], p[1] - tam[1]
    return (tam[0] + x * math.cos(t) + z * math.sin(t), tam[1] - x * math.sin(t) + z * math.cos(t))


def tc03(h):
    s = Svg(1180, 820, 23)
    khung2(s, "TC2-03", "CÒ ANALOG BẰNG CẢM BIẾN HALL", "Bóp cò → nam châm lại gần SS49E → điện áp ra tăng")
    k = 8.0
    ox, oy = 30 - 4 * k, 230
    X = lambda u: ox + u * k
    Y = lambda v: oy - v * k
    s.text(40, 46, "CHI TIẾT CÒ (MẶT CẮT A–A, PHÓNG TO 3:1)", "h")
    s.text(40, 62, "nét liền: nhả cò · nét đứt: bóp hết (cò quay 11° tới khi vấu chạm vách)", "ts")
    s.add(f'<clipPath id="kc23"><rect x="30" y="80" width="420" height="440"/></clipPath><g clip-path="url(#kc23)">')
    c = h["cat_y0"]
    to_mau(s, c["vo"], X, Y, "cutv")
    to_mau(s, c["co"], X, Y, "toi")
    tp = h["tham_so"]
    piv = (tp["chot_co"][0], tp["chot_co"][2])
    GOC_MAX = 11.0
    for poly in c["co"]:
        q = [quay_cw(p, piv, GOC_MAX) for p in poly]
        s.path("M" + " L".join(f"{fmt(X(a))},{fmt(Y(b))}" for a, b in q) + " Z", "hid")
    s.rect(X(23.6), Y(-7), 3 * k, 6 * k, "lkf", extra='style="fill:#9aa3a8"')
    s.rect(X(15.65), Y(-8.5), 1.5 * k, 3 * k, "lkf", extra='style="fill:#c0392b"')
    for dy in (-0.9, 0, 0.9):
        s.line(X(15.65), Y(-10 + dy), X(9), Y(-10 + dy), "ln", 'style="stroke:#c0392b;stroke-width:1.2"')
    zz = "M" + " L".join(f"{fmt(X(17 + i * 0.5))},{fmt(Y(-4.5 + (1.6 if i % 2 else -1.6)))}" for i in range(11))
    s.path(zz, "ln", 'style="stroke-width:1.2"')
    s.circle(X(piv[0]), Y(piv[1]), 1.6 * k, "ball")
    s.add("</g>")
    s.rect(30, 80, 420, 440, "thin")
    # kích thước khe
    s.dimh(X(17.15), X(23.6), Y(-7) - 40, "6,5 lúc nhả", Y(-8.5), Y(-7))
    mag_full = quay_cw((23.6, -10), piv, GOC_MAX)
    s.dimh(X(17.15), X(mag_full[0]), Y(-13) + 70, f"{mag_full[0] - 17.15:.1f} lúc bóp hết".replace(".", ","), Y(-11.5), Y(-13))
    s.leader(X(25), Y(-4), X(25) + 70, Y(-4) - 90, "nam châm N35 Ø6 × 3")
    s.leader(X(16.4), Y(-11.5), X(16.4) - 50, Y(-11.5) + 180, "SS49E (mặt có chữ hướng về nam châm)", anchor="start")
    s.leader(X(19), Y(-4.5), X(19) - 10, Y(-4.5) - 120, "lò xo nén Ø4 × 8", anchor="start")
    s.leader(X(piv[0]), Y(piv[1]), X(piv[0]) + 60, Y(piv[1]) - 60, "chốt M3")
    s.leader(X(34), Y(-15), X(34) + 30, Y(-15) + 60, "chỗ đặt ngón trỏ")

    # đồ thị điện áp ra theo độ bóp
    gx, gy, gw, gh = 560, 110, 520, 330
    s.text(gx, 96, "ĐIỆN ÁP RA SS49E THEO ĐỘ BÓP CÒ (ƯỚC TÍNH)", "hs")
    vmin, vmax = 1.6, 2.8
    PX = lambda p: gx + p / 100 * gw
    PY = lambda v: gy + gh - (v - vmin) / (vmax - vmin) * gh
    for v in (1.6, 1.8, 2.0, 2.2, 2.4, 2.6, 2.8):
        s.line(gx, PY(v), gx + gw, PY(v), "grid")
        s.text(gx - 8, PY(v) + 4, f"{v:.1f}".replace(".", ","), "dimt", "end")
    for p in range(0, 101, 20):
        s.line(PX(p), gy, PX(p), gy + gh, "grid")
        s.text(PX(p), gy + gh + 18, f"{p}%", "dimt", "middle")
    s.text(gx + gw / 2, gy + gh + 40, "độ bóp cò (0% = nhả, 100% = vấu chạm vách)", "ts", "middle")
    s.text(gx - 40, gy + gh / 2, "điện áp ra (V)", "ts", "middle", rot=-90)
    pts = []
    for i in range(0, 101):
        g = GOC_MAX * i / 100
        mx = quay_cw((23.6, -10), piv, g)[0]
        khe = mx - 17.15 + 0.5          # phần tử cảm biến nằm sâu ≈ 0,5 mm trong vỏ TO-92
        B = tu_truong(khe)
        v = min(3.3 / 2 + 0.00092 * B, 3.1)
        pts.append((i, v, khe, B))
    s.path("M" + " L".join(f"{fmt(PX(p))},{fmt(PY(v))}" for p, v, _, _ in pts), "chart")
    for p in (0, 100):
        _, v, khe, B = pts[p]
        s.circle(PX(p), PY(v), 4, "dot")
        s.text(PX(p) + (8 if p == 0 else -8), PY(v) + (22 if p == 0 else -12), f"{v:.2f} V · {B:.0f} G · khe {khe:.1f} mm".replace(".", ","), "ts", "start" if p == 0 else "end")
    adc = lambda v: v / 3.1 * 4095
    s.text(gx, gy + gh + 72, f"ESP32 (ADC 12 bit, 11 dB) đọc ≈ {adc(pts[0][1]):.0f} → {adc(pts[-1][1]):.0f}: khoảng {adc(pts[-1][1]) - adc(pts[0][1]):.0f} mức, đủ cho thao tác tinh.", "t")
    notes = [
        "Mô hình: nam châm trụ N35 (Br ≈ 1,2 T); SS49E ≈ 1,4 mV/G ở 5 V, tức ≈ 0,92 mV/G ở 3,3 V.",
        "Giữ khe ≥ 3 mm để SS49E chưa bão hòa (≈ ±1000 G). Lật mặt nam châm thì điện áp giảm thay vì tăng.",
        "Phần mềm: đọc mức nhả (min) lúc khởi động, bóp hết một lần lấy max; % = (đọc − min) / (max − min).",
        "Đường cong không thẳng: dùng bảng tra 16 điểm để bóp đều tay thì % tăng đều.",
        "Dùng làm: cầm vật khi > 60%; bóp quả bóp pipet, mở khóa buret theo % liên tục.",
    ]
    for i, n in enumerate(notes):
        s.text(gx, gy + gh + 104 + i * 18, n, "ts")
    return s


# ---------------- TC2-04 đấu nối ----------------

def tc04(h):
    s = Svg(1180, 820, 24)
    khung2(s, "TC2-04", "SƠ ĐỒ ĐẤU NỐI ESP32", "ESP32 DevKitC 38 chân · mọi cảm biến chạy 3,3 V")
    s.text(40, 46, "SƠ ĐỒ ĐẤU NỐI", "h")
    s.text(40, 62, "chỉ dùng chân ADC1 cho tín hiệu analog (ADC2 không đọc được khi bật Wi-Fi)", "ts")
    bx, by, bw = 470, 110, 230
    trai = ["3V3", "EN", "VP 36", "VN 39", "34", "35", "32", "33", "25", "26", "27", "14", "12", "GND", "13", "SD2", "SD3", "CMD", "5V"]
    phai = ["GND", "23", "22", "TX0", "RX0", "21", "GND", "19", "18", "5", "17", "16", "4", "0", "2", "15", "SD1", "SD0", "CLK"]
    step = 26
    bh = 66 + step * 18 + 30
    s.rect(bx, by, bw, bh, "mod", rx=6)
    s.rect(bx + 50, by + 6, bw - 100, 46, "lkf", extra='style="fill:#55626a"')
    s.text(bx + bw / 2, by + 34, "ESP32-WROOM-32", "ts", "middle", extra='style="fill:#e6eaec"')
    s.rect(bx + bw / 2 - 16, by + bh - 14, 32, 18, "kim")
    s.text(bx + bw / 2, by + bh - 24, "USB", "ts", "middle")
    pin = {}
    for i, p in enumerate(trai):
        y = by + 66 + i * step
        s.circle(bx + 12, y, 4, "ln")
        s.text(bx + 22, y + 4, p, "code")
        pin["L" + p] = (bx, y)
    for i, p in enumerate(phai):
        y = by + 66 + i * step
        s.circle(bx + bw - 12, y, 4, "ln")
        s.text(bx + bw - 22, y + 4, p, "code", "end")
        pin["R" + p] = (bx + bw, y)
    dung = {"L3V3": "#e0362c", "L34": "#f08c00", "L35": "#8d5a2b", "L32": "#c0392b", "L33": "#7f8c8d", "L27": "#8e44ad",
            "LGND": None, "R22": "#2e9e44", "R21": "#d9a400", "RGND": None}
    for kk, mau in dung.items():
        x, y = pin[kk]
        s.circle(x + (12 if kk[0] == "L" else -12), y, 4.5, "dot" if mau is None else "ln", "" if mau is None else f'style="fill:{mau};stroke:{mau}"')

    def day(pts, mau, w=2.2, net=False):
        d = "M" + " L".join(f"{fmt(x)},{fmt(y)}" for x, y in pts)
        if mau:
            s.path(d, "wire", f'stroke="{mau}" stroke-width="{w}"' + (' stroke-dasharray="6 4"' if net else ""))
        else:
            s.path(d, "wire gnd", f'stroke-width="{w}"' + (' stroke-dasharray="6 4"' if net else ""))

    def mod(x, y, w, h_, ten, chan, ben="phai"):
        s.rect(x, y, w, h_, "mod", rx=5)
        s.rect(x, y, w, 22, "modh", rx=5)
        s.text(x + w / 2, y + 16, ten, "tb", "middle")
        out = {}
        for i, c_ in enumerate(chan):
            yy = y + 42 + i * 22
            if ben == "phai":
                s.text(x + w - 8, yy + 4, c_, "ts", "end")
                s.circle(x + w, yy, 3, "dot")
                out[c_] = (x + w, yy)
            else:
                s.text(x + 8, yy + 4, c_, "ts")
                s.circle(x, yy, 3, "dot")
                out[c_] = (x, yy)
        return out

    ky = mod(60, 120, 180, 140, "Joystick KY-023", ["+5V (nối 3V3)", "GND", "VRx", "VRy", "SW"])
    hl = mod(60, 300, 180, 110, "Hall SS49E (cò)", ["1 VCC", "2 GND", "3 OUT"])
    s.rect(250, 365, 12, 24, "mod", rx=2)
    s.text(270, 381, "100 nF", "ts")
    bm = mod(60, 450, 180, 132, "BMI160", ["VIN", "GND", "SDA", "SCL"])
    x3, y3 = pin["L3V3"]
    rail3 = 330
    day([(x3, y3), (rail3, y3), (rail3, bm["VIN"][1])], "#e0362c", 2.6)
    for p in (ky["+5V (nối 3V3)"], hl["1 VCC"], bm["VIN"]):
        day([p, (rail3, p[1])], "#e0362c")
        s.circle(rail3, p[1], 3, "dot")
    xg, yg = pin["LGND"]
    railg = 350
    day([(xg, yg), (railg, yg), (railg, ky["GND"][1])], None, 2.6)
    for p in (ky["GND"], hl["2 GND"], bm["GND"]):
        day([p, (railg, p[1])], None)
        s.circle(railg, p[1], 3, "dot")
    day([(256, 365), (256, hl["3 OUT"][1])], "#c0392b", 1.4)
    day([(256, 389), (256, 400), (railg, 400)], None, 1.4)
    for src, dst, mau, xo in ((ky["VRx"], "L34", "#f08c00", 380), (ky["VRy"], "L35", "#8d5a2b", 392), (ky["SW"], "L27", "#8e44ad", 404),
                              (hl["3 OUT"], "L32", "#c0392b", 416)):
        day([src, (xo, src[1]), (xo, pin[dst][1]), pin[dst]], mau)
    for src, dst, mau, xo in ((bm["SDA"], "R21", "#d9a400", 760), (bm["SCL"], "R22", "#2e9e44", 772)):
        day([src, (300, src[1]), (300, 700 + (xo - 760)), (xo, 700 + (xo - 760)), (xo, pin[dst][1]), pin[dst]], mau)
    s.text(530, 730, "I2C: SDA → GPIO21, SCL → GPIO22 (địa chỉ 0x68)", "ts", "middle")
    # nguồn (tính sau)
    px_, py_ = 820, 110
    s.rect(px_, py_, 320, 230, "box", rx=8)
    s.text(px_ + 12, py_ + 20, "NGUỒN · TÍNH SAU", "hs")
    for i, (t1, t2) in enumerate([("Pin LiPo 1S 1000 mAh", "40 × 25 × 9, nằm trong cán"), ("TP4056 USB-C có bảo vệ", "sạc qua cửa ở nắp đáy"),
                                  ("Công tắc trượt", "khe ở nắp đáy"), ("Hạ áp 3,3 V (HT7333)", "→ chân 3V3, hoặc tăng áp 5 V → chân 5V"),
                                  ("Đo pin: chia áp 100k/100k", "→ GPIO33")]):
        s.text(px_ + 14, py_ + 48 + i * 36, t1, "t")
        s.text(px_ + 14, py_ + 64 + i * 36, t2, "ts")
    day([pin["L33"], (430, pin["L33"][1]), (430, 96), (px_ + 160, 96), (px_ + 160, py_)], "#7f8c8d", 1.6, net=True)
    # bảng chân
    tx, ty = 820, 380
    s.text(tx, ty, "BẢNG CHÂN", "hs")
    rows = [("GPIO34", "VRx joystick", "ADC1"), ("GPIO35", "VRy joystick", "ADC1"), ("GPIO27", "SW joystick", "INPUT_PULLUP"),
            ("GPIO32", "OUT Hall SS49E", "ADC1"), ("GPIO21", "SDA BMI160", "I2C"), ("GPIO22", "SCL BMI160", "I2C"),
            ("GPIO33", "điện áp pin", "ADC1, sau"), ("3V3, GND", "nguồn cảm biến", "")]
    for i, (a, b, c_) in enumerate(rows):
        y = ty + 24 + i * 22
        s.text(tx, y, a, "code")
        s.text(tx + 90, y, b, "t")
        s.text(tx + 230, y, c_, "ts")
    s.text(tx, ty + 24 + 8 * 22 + 8, "Gói UDP giữ dạng docs/02: j2x j2y = joystick, nút bit 4,", "ts")
    s.text(tx, ty + 24 + 8 * 22 + 24, "thêm trường cò 0–1023 và dữ liệu BMI160.", "ts")
    return s


# ---------------- TC2-05 cầm tay + OpenCV ----------------

def tc05(h):
    s = Svg(1180, 820, 25)
    khung2(s, "TC2-05", "CÁCH CẦM VÀ ĐO VỊ TRÍ BẰNG OPENCV", "Vòng halo là mốc cho webcam: đường kính ảnh → khoảng cách")
    k = 2.2
    ox, oy = 320, 200
    X = lambda u: ox + u * k
    Y = lambda v: oy - v * k
    s.text(40, 46, "A · CẦM TRÊN TAY", "h")
    s.text(40, 62, "tay phải, nhìn từ phía phải", "ts")
    ph = h["phan"]
    to_mau(s, ph["vo_trai"]["canh"], X, Y, "vo")
    for ma, cls in (("tam_op", "toi"), ("co", "toi"), ("nap_day", "toi"), ("halo", "hl"), ("dai_sang", "hl")):
        to_mau(s, ph[ma]["canh"], X, Y, cls)
    to_mau(s, h["tong"]["canh"], X, Y, "vien")
    g = math.radians(20)
    dd = (-math.sin(g), -math.cos(g))
    ea = (math.cos(g), -math.sin(g))
    P = lambda s_, u: (s_ * dd[0] + u * ea[0], s_ * dd[1] + u * ea[1])

    def bao(pts, cls="tay"):
        s.path("M" + " L".join(f"{fmt(X(a))},{fmt(Y(b))}" for a, b in pts) + " Z", cls)

    # lòng bàn tay + cổ tay phía sau cán
    bao([P(-2, -15), P(100, -17), P(108, -26), (-70, -135), (-105, -112), (-40, -8)])

    def ngon(pts, w):
        d = "M" + " L".join(f"{fmt(X(a))},{fmt(Y(b))}" for a, b in pts)
        s.path(d, "", f'fill="none" stroke="var(--hand-ln,#9c5f37)" stroke-width="{fmt(w * k + 2)}" stroke-linecap="round" stroke-linejoin="round" opacity=".7"')
        s.path(d, "", f'fill="none" stroke="var(--hand,#e9b98f)" stroke-width="{fmt(w * k)}" stroke-linecap="round" stroke-linejoin="round" opacity=".85"')

    for s_ in (44, 63, 82):              # giữa, áp út, út ôm phía trước cán
        ngon([P(s_, -6), P(s_, 14), P(s_ + 4, 22)], 15)
    ngon([P(6, -8), (25, -11), (33, -17)], 14)              # ngón trỏ trên cò
    j = h["tham_so"]["tam_can_joy"]
    ngon([(-30, 6), (-12, 26), (j[0] + 2, j[2] + 14)], 17)   # ngón cái trên joystick
    s.leader(X(j[0] + 2), Y(j[2] + 16), X(j[0]) + 70, Y(j[2] + 16) - 40, "Ngón cái: joystick (đi, quay)")
    s.leader(X(34), Y(-16), X(34) + 90, Y(-16) + 10, "Ngón trỏ: cò Hall 0–100%")
    s.leader(*[f(v) for f, v in zip((X, Y), P(64, 22))], X(P(64, 22)[0]) + 120, Y(P(64, 22)[1]) + 40, "3 ngón ôm cán 37 × 33")
    s.leader(X(-40), Y(-113), X(-40) + 60, Y(-113) + 50, "dây đeo cổ tay ở nắp đáy")
    s.leader(X(57), Y(40), X(57) + 40, Y(40) - 20, "vòng halo hướng về webcam")

    # B · OpenCV
    s.text(620, 46, "B · OPENCV ĐO XA GẦN VÀ VỊ TRÍ", "h")
    s.text(620, 62, "webcam đặt trước người dùng, nhìn thấy vòng halo", "ts")
    cx, cy = 660, 260
    s.rect(cx - 24, cy - 18, 40, 36, "cam", rx=6)
    s.circle(cx + 22, cy, 10, "cam")
    s.text(cx - 4, cy + 44, "webcam", "ts", "middle")
    s.text(cx - 4, cy + 60, "tiêu cự f (px)", "ts", "middle")
    hx = 1000
    s.add(f'<ellipse cx="{hx}" cy="{cy}" rx="16" ry="62" class="hl" style="fill-opacity:.25"/>')
    s.add(f'<ellipse cx="{hx}" cy="{cy}" rx="12" ry="50" class="paper" style="stroke:var(--ink,#18272c);stroke-width:1"/>')
    s.line(cx + 32, cy, hx, cy - 62, "tia")
    s.line(cx + 32, cy, hx, cy + 62, "tia")
    s.line(cx + 32, cy, hx, cy, "ctr")
    s.dimh(cx + 32, hx, cy + 100, "Z (khoảng cách cần tìm)", cy + 10, cy + 70)
    s.dimv(cy - 62, cy + 62, hx + 40, "D = 70 mm", hx, hx, side=1)
    # ảnh trên cảm biến
    ix, iy = 700, 470
    s.rect(ix, iy, 220, 150, "mod", rx=4)
    s.text(ix + 110, iy - 8, "ảnh webcam", "ts", "middle")
    s.add(f'<ellipse cx="{ix + 150}" cy="{iy + 62}" rx="34" ry="22" transform="rotate(-25 {ix + 150} {iy + 62})" class="hl" style="fill-opacity:.25"/>')
    s.line(ix + 150 - 31, iy + 62 + 14.5, ix + 150 + 31, iy + 62 - 14.5, "dim", s.ar())
    s.text(ix + 110, iy + 136, "d (px) = trục lớn của elip", "ts", "middle")
    s.circle(ix + 150, iy + 62, 2.5, "dot")
    s.text(ix + 150, iy + 50 - 22, "(u, v)", "ts", "middle")
    ft = [("Z = f · D / d", "khoảng cách → xa gần, phóng to thu nhỏ"),
          ("X = (u − c_x) · Z / f", "dịch trái phải"), ("Y = (v − c_y) · Z / f", "dịch lên xuống"),
          ("cos φ = trục nhỏ / trục lớn", "độ nghiêng halo, đối chiếu với BMI160")]
    for i, (a, b) in enumerate(ft):
        s.text(950, 480 + i * 40, a, "code")
        s.text(950, 496 + i * 40, b, "ts")
    s.text(620, 664, "Ví dụ: f ≈ 600 px, halo rộng 84 px trên ảnh → Z = 600 · 70 / 84 = 500 mm.", "t")
    s.text(620, 684, "Trục lớn elip không đổi khi halo nghiêng, nên đo khoảng cách vẫn đúng khi cổ tay xoay.", "ts")
    s.text(620, 700, "Lọc màu HSV theo màu huỳnh quang của halo → cv2.fitEllipse cho tâm (u, v) và hai trục.", "ts")
    return s


def tat_ca(standalone=True):
    h = doc()
    return [("TC2-01", "Ba hình chiếu", tc01(h).svg(standalone)), ("TC2-02", "Mặt cắt dọc, bố trí", tc02(h).svg(standalone)),
            ("TC2-03", "Cò Hall", tc03(h).svg(standalone)), ("TC2-04", "Đấu nối ESP32", tc04(h).svg(standalone)),
            ("TC2-05", "Cầm tay, OpenCV", tc05(h).svg(standalone))]


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    for so, ten, svg in tat_ca(True):
        p = os.path.join(THU_MUC, so + ".svg")
        with open(p, "w", encoding="utf-8") as fh:
            fh.write(svg)
        print(p)
