"""Tay cầm VR "VERA Arc": vòng tròn lớn phía trên đầu, mặt tối hình bầu dục, pin vuông 9V nằm trong cán, thân 2 nửa bắt vít.

Chạy:  python tools/tay_cam_arc/mo_hinh_arc.py [thư mục xem]
Ra:
  docs/tay-cam-arc/in/*.stl        chi tiết in 3D, đã xoay đúng hướng in (đáy ở z = 0)
  docs/tay-cam-arc/thong_so.json   kích thước, khối lượng
  <thư mục xem>/*.stl + canh.json  cả bộ ở tư thế lắp, cho trang xem 3D

Hệ trục (mm): z dọc cán từ đáy lên, x ra trước (phía ngón trỏ), y sang trái. Tay phải: mu bàn tay ở phía -y.
Cần: pip install manifold3d numpy matplotlib
"""
import functools
import json
import math
import os
import struct
import sys

import numpy as np
from manifold3d import CrossSection, FillRule, Manifold, OpType
from matplotlib.font_manager import FontProperties
from matplotlib.textpath import TextPath

GOC = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
THU_MUC = os.path.join(GOC, "docs", "tay-cam-arc")
SEG = 96

VACH = 1.8              # bề dày vỏ
# dáng thân: (z, nửa sâu a theo x, nửa rộng b theo y). Cán ≈ 40 × 29 mm (≈ Ø35, docs/13), vừa đủ ôm pin vuông; đầu to ra để đặt ESP32 nằm ngang
DANG = [(0, 18.2, 13.0), (1.5, 19.2, 13.8), (6, 19.7, 14.3), (20, 20.0, 14.5), (36, 20.5, 14.9), (46, 23.5, 16.4), (54, 30.0, 21.0),
        (60, 36.0, 25.0), (66, 38.5, 26.2), (74, 37.0, 26.4), (84, 33.5, 26.0), (94, 31.0, 25.0), (104, 29.0, 23.0), (114, 26.0, 20.0)]
NGHIENG = 20.0          # mặt joystick ngửa về phía ngón cái
n_mat = np.array([-math.sin(math.radians(NGHIENG)), 0, math.cos(math.radians(NGHIENG))])
t_mat = np.array([math.cos(math.radians(NGHIENG)), 0, math.sin(math.radians(NGHIENG))])
R_MAT = np.column_stack([t_mat, [0, 1, 0], n_mat])
P_DINH = np.array([8.0, 0, 96.0])       # mặt đỉnh đi qua điểm này
TAM_CAN = P_DINH - 5.0 * t_mat          # tâm cần joystick, giữa mặt bầu dục
KY_SAU = 20.5                           # tâm mạch KY-023 nằm dưới mặt đỉnh chừng này
R_CHEN = 17.0                           # chén lõm lục giác quanh cần, bán kính đỉnh
LUN_MAT = 0.4                           # mặt tối lún so với vành trắng
DAY_MAT = 2.0                           # bề dày mặt tối

# linh kiện (x, y, z theo hệ thân)
ESP = {"x": (-22.5, 32.5), "z": 63.0}   # DevKitC 55 × 28 nằm ngang trong đầu, cổng micro-USB quay ra lưng, chân header cắt ngắn xuống dưới
HA_AP = {"x": (-26.0, -9.0), "y": (-11.0, 11.0), "z": (69.0, 73.0)}     # MP1584 22 × 17, nằm trên ESP32, sau mạch joystick
# khoang pin vuông 9V (PP3 48,5 × 26,5 × 17,5) dựng đứng trong cán, kẹp pin ở trên
BAY_XC = -1.0
BAY_X = 13.75
BAY_Y = 9.25
Z_SAN = 2.4             # mặt trên sàn cán
Z_PIN = 2.25            # mặt dưới pin, tựa nắp đáy (2 mm) qua lớp xốp mỏng
DOOR_X0 = BAY_XC - 14.2
# cò Hall: tay cò hở ra ngoài như ảnh mẫu, quay quanh trục Ø2 nằm dưới đầu
CHOT = np.array([25.0, 0, 57.0])
Y_CO = 7.8
Z_CUA = (33.0, 59.0)    # ô cửa trên thân
Z_NC, Y_NC = 46.0, 3.0  # tâm nam châm và Hall
X_VACH = (13.0, 15.2)   # vách ngăn giữa pin và cò, mặt trước đỡ Hall
X_HALL = 15.1           # mặt Hall hướng về nam châm
X_NC = 21.6             # mặt nam châm lúc nhả: khe 6,5 mm
GOC_BOP = 13.0          # góc bóp hết: vấu tì vào vách, nam châm còn cách Hall ≈ 3,5 mm
X_VAU = 18.71
Z_LO_XO = 38.0
# 6 vít M2×10 ghép 2 nửa thân (x, z, bán kính trụ): đầu vít ở nửa trái (+y), ren ăn vào nửa phải (-y)
VIT = [(15.4, 12.0, 2.4), (15.4, 28.0, 2.4), (-17.7, 14.0, 2.4), (-17.8, 40.0, 2.4), (-24.0, 57.0, 2.4), (-29.5, 72.8, 2.4), (37.0, 67.0, 2.4)]
Y_VAI = 3.5             # mặt tì đầu vít
# thanh móc dây: ống Ø4 giữa 2 tai ở lưng đáy cán, lõi là vít M2×16 nên cũng ghép luôn 2 nửa
X_THANH, Z_THANH = -24.2, 6.0
R_THANH, R_TAI = 2.0, 3.2
Y_TAI = (5.5, 9.2)
Y_VAI_THANH = 7.4
# vòng tròn nhỏ để tạo dáng và mang logo (theo dõi tay dùng MediaPipe); mặt phẳng vòng ngả ra sau, 2 đầu bắt vào 2 sườn đầu tay cầm
BETA = 45.0             # góc ngả của mặt phẳng vòng so với phương đứng
P_NEO = np.array([5.0, 0, 80.0])        # điểm giữa 2 đầu vòng
Y_NEO = 27.5            # nửa khoảng cách 2 đầu vòng
R_VONG = 34.0           # bán kính giữa bản vòng
VONG_DAY, VONG_RONG = 3.0, 11.0         # bề dày, bản rộng
DAY_LUG = 4.2           # phần tai đỡ sau mép vòng
# chữ và logo khắc trên đỉnh vòng, logo lấy từ trang giới thiệu (lục giác + chữ V)
CHU = "VERA LAB"
PHONG = r"C:\Windows\Fonts\bahnschrift.ttf"
CO_CHU = 6.2            # cỡ chữ (chữ hoa cao ≈ 4,4 mm)
CAO_LOGO = 8.6
SAU_CHU = 0.6
# 4 cổng mở rộng Ø7: lắp nút bấm 6 × 6 kèm nắp in 3D, hoặc bỏ nắp để luồn dây cảm biến. P1, P2 trên mặt tối, P3, P4 ở mũi đầu
T_CONG, Y_CONG = 16.3, 9.5      # P1, P2 trong hệ mặt đỉnh
Y_MUI, Z_MUI = 11.0, 77.0       # P3, P4 trên thân


# ---------------- tiện ích ----------------

def bien_doi(R, t):
    m = np.zeros((3, 4))
    m[:, :3] = R
    m[:, 3] = t
    return m


def quay(truc, do):
    t = math.radians(do)
    c, s = math.cos(t), math.sin(t)
    if truc == "x":
        return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
    if truc == "y":
        return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


def cuc(sx, sy, sz, x=0.0, y=0.0, z=0.0):
    return Manifold.cube([sx, sy, sz], True).translate([x, y, z])


def hop(x0, x1, y0, y1, z0, z1):
    return Manifold.cube([x1 - x0, y1 - y0, z1 - z0]).translate([x0, y0, z0])


def tru_x(r, x0, x1, y, z, n=40):
    return Manifold.cylinder(x1 - x0, r, r, n).rotate([0, 90, 0]).translate([x0, y, z])


def tru_y(r, y0, y1, x, z, n=40):
    return Manifold.cylinder(y1 - y0, r, r, n).rotate([-90, 0, 0]).translate([x, y0, z])


def tru_z(r, z0, z1, x, y, n=40):
    return Manifold.cylinder(z1 - z0, r, r, n).translate([x, y, z0])


def elip(a, b, n=SEG):
    return CrossSection([[[a * math.cos(2 * math.pi * i / n), b * math.sin(2 * math.pi * i / n)] for i in range(n)]])


def duong_cong(ds, buoc=8):
    """Nội suy Catmull-Rom qua các trạm (z, a, b) để thân trơn, không gãy khúc."""
    p = [np.array(d, float) for d in ds]
    p = [2 * p[0] - p[1]] + p + [2 * p[-1] - p[-2]]
    ra = []
    for i in range(1, len(p) - 2):
        for k in range(buoc):
            t = k / buoc
            ra.append(0.5 * (2 * p[i] + (p[i + 1] - p[i - 1]) * t + (2 * p[i - 1] - 5 * p[i] + 4 * p[i + 1] - p[i + 2]) * t * t
                             + (3 * p[i] - p[i - 1] - 3 * p[i + 1] + p[i + 2]) * t ** 3))
    ra.append(p[-2])
    return ra


TRAM = duong_cong(DANG)


def ab_tai(z):
    zs = [r[0] for r in TRAM]
    return float(np.interp(z, zs, [r[1] for r in TRAM])), float(np.interp(z, zs, [r[2] for r in TRAM]))


def _muot(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


def cx(z):
    """Tâm thân lệch theo chiều cao: cán thẳng, đầu đổ ra trước để ESP32 không đội lên chỗ khe ngón cái."""
    return -1.0 + 6.0 * _muot((z - 38.0) / 26.0)


@functools.lru_cache(maxsize=None)
def khoi(lui=0.0):
    """Khối đặc của thân; lui > 0 cho khối bên trong."""
    tang = []
    for r0, r1 in zip(TRAM, TRAM[1:]):
        a0, b0, a1, b1 = r0[1] - lui, r0[2] - lui, r1[1] - lui, r1[2] - lui
        h = r1[0] - r0[0] + 0.001
        tang.append(Manifold.extrude(elip(a0, b0), h, 0, 0, (a1 / a0, b1 / b0)).translate([0, 0, r0[0]]))
    return Manifold.batch_boolean(tang, OpType.Add).refine_to_length(6.0).warp(lambda v: (v[0] + cx(v[2]), v[1], v[2]))


def d_mat(p):
    return float(np.dot(n_mat, p))


DD = d_mat(P_DINH)


def hop_mat(t0, t1, y0, y1, n0, n1):
    """Hộp trong hệ mặt đỉnh: t theo mặt, n vuông góc mặt, gốc ở P_DINH."""
    return hop(t0, t1, y0, y1, n0, n1).transform(bien_doi(R_MAT, P_DINH))


def diem_mat(t, y, n):
    return P_DINH + t * t_mat + y * np.array([0, 1.0, 0]) + n * n_mat


def tru_mat(r, t0, t1, y, n, k=40):
    """Trụ dọc theo t trong hệ mặt đỉnh."""
    return Manifold.cylinder(t1 - t0, r, r, k).rotate([0, 90, 0]).translate([t0, y, n]).transform(bien_doi(R_MAT, P_DINH))


def mep_t(y, n, huong):
    """Tìm t của mặt ngoài thân theo một hướng, tại độ sâu n dưới mặt đỉnh."""
    t = 0.0
    while abs(t) < 70:
        p = diem_mat(t, y, n)
        a, b = ab_tai(float(p[2]))
        if ((p[0] - cx(float(p[2]))) / a) ** 2 + (y / b) ** 2 > 1:
            return t
        t += 0.1 * huong
    return t


def o_cua(mo=0.0, r=6.0):
    """Ô cửa cò trên thân, nhìn từ trước: chữ nhật bo góc."""
    y, z0, z1 = Y_CO + mo - r, Z_CUA[0] - mo + r, Z_CUA[1] + mo - r
    return Manifold.batch_hull([tru_x(r, X_VACH[1] + 0.2, 34.5, sy * y, z) for sy in (-1, 1) for z in (z0, z1)])


# ---------------- vòng tròn ----------------

_b = math.radians(BETA)
w_vong = np.array([-math.sin(_b), 0, math.cos(_b)])      # hướng lên đỉnh vòng
N_VONG = np.array([math.cos(_b), 0, math.sin(_b)])       # pháp tuyến mặt phẳng vòng, ra trước
# hệ riêng của vòng: x về đỉnh, y sang -y (mu bàn tay), z theo pháp tuyến
R_V = np.column_stack([w_vong, [0, -1, 0], N_VONG])
H_VONG = math.sqrt(R_VONG ** 2 - Y_NEO ** 2)
TAM_VONG = P_NEO + H_VONG * w_vong
THETA = math.pi - math.asin(Y_NEO / R_VONG)              # nửa góc mở: từ đỉnh tới đầu vòng
R_NGOAI = R_VONG + VONG_DAY / 2
R_TRONG = R_VONG - VONG_DAY / 2
Z0_V, Z1_V = -VONG_RONG / 2, VONG_RONG / 2
R_KHOI = R_VONG - 5.0                                     # 2 đầu vòng dày lên phía trong để ăn vít
GOC_KHOI = 14.0 / R_VONG


def _xoay_vong(ri, ro, z0, z1, goc0, goc1, vat=0.0):
    """Mảnh vành khuyên quanh tâm vòng, từ goc0 tới goc1 (radian, tính từ đỉnh, dương về phía -y)."""
    if vat > 0:
        mc = [[ri + vat, z0], [ro - vat, z0], [ro, z0 + vat], [ro, z1 - vat], [ro - vat, z1], [ri + vat, z1], [ri, z1 - vat], [ri, z0 + vat]]
    else:
        mc = [[ri, z0], [ro, z0], [ro, z1], [ri, z1]]
    m = Manifold.revolve(CrossSection([mc]), 320, math.degrees(goc1 - goc0)).rotate([0, 0, math.degrees(goc0)])
    return m.transform(bien_doi(R_V, TAM_VONG))


def _tru_v(r, z0, z1, goc, rc):
    """Trụ song song trục vòng, đặt tại góc goc, bán kính rc."""
    return Manifold.cylinder(z1 - z0, r, r, 40).translate([rc * math.cos(goc), rc * math.sin(goc), z0]).transform(bien_doi(R_V, TAM_VONG))


def _goc_vit(phia):
    """Góc đặt vít của một đầu vòng; phia = 1 đầu bên trái (+y), -1 đầu bên phải (-y)."""
    return -phia * (THETA - 7.5 / R_VONG)


def _diem_dau(phia):
    return P_NEO + np.array([0, phia * Y_NEO, 0])


def tai_vong(phia):
    """Tai đỡ đầu vòng, mọc từ sườn đầu tay cầm: mép sau vòng tựa vào đây, có hốc giữ, bắt 1 vít từ phía sau."""
    t0, t1 = Z0_V - DAY_LUG, Z0_V + 1.2
    g = _goc_vit(phia)
    rc = (R_KHOI + R_NGOAI) / 2
    tam = (t0 + t1) / 2
    patch = cuc(16, 2, t1 - t0).transform(bien_doi(R_V, _diem_dau(phia) + np.array([0, -phia * 6.0, 0]) + tam * N_VONG))
    m = Manifold.batch_hull([_tru_v(5.0, t0, t1, g, rc), patch])
    dau = (-THETA - 0.02, -THETA + GOC_KHOI + 0.02) if phia > 0 else (THETA - GOC_KHOI - 0.02, THETA + 0.02)
    m = m - _xoay_vong(R_KHOI - 0.2, R_NGOAI + 0.2, Z0_V - 0.15, 14, *dau)
    return m


def lo_tai_vong(phia):
    g, t0 = _goc_vit(phia), Z0_V - DAY_LUG
    rc = (R_KHOI + R_NGOAI) / 2
    return _tru_v(1.15, t0 - 1, Z0_V + 0.1, g, rc) + _tru_v(2.1, t0 - 1, t0 + 1.2, g, rc)


def _logo_2d(cao):
    """Logo trang giới thiệu (viewBox 40 × 40): lục giác nét 2,6 và chữ V nét 3,6. Gốc ở tâm logo."""
    k = cao / 37.6
    luc = [(20, 2.5), (35.2, 11.2), (35.2, 28.8), (20, 37.5), (4.8, 28.8), (4.8, 11.2)]
    ngoai = [[(x - 20) * k * 1.09, (20 - y) * k * 1.09] for x, y in luc]
    trong = [[(x - 20) * k * 0.91, (20 - y) * k * 0.91] for x, y in luc]
    vanh = CrossSection([ngoai, trong], FillRule.EvenOdd)
    r = 1.8 * k
    v0, v1, v2 = [((x - 20) * k, (20 - y) * k) for x, y in ((12.5, 13), (20, 28), (27.5, 13))]
    tron = lambda p: CrossSection.circle(r, 20).translate(p)
    return vanh + CrossSection.batch_hull([tron(v0), tron(v1)]) + CrossSection.batch_hull([tron(v1), tron(v2)])


def nhan_2d():
    """Logo và chữ VERA LAB thành một dòng, căn giữa quanh gốc."""
    duong = TextPath((0, 0), CHU, size=CO_CHU, prop=FontProperties(fname=PHONG))
    vien = [np.asarray(q)[:-1].tolist() for q in duong.to_polygons() if len(q) > 3]
    chu = CrossSection(vien, FillRule.EvenOdd)
    bb = chu.bounds()
    logo = _logo_2d(CAO_LOGO)
    lb = logo.bounds()
    gian = 3.0
    rong_logo = lb[2] - lb[0]
    rong_chu = bb[2] - bb[0]
    tong = rong_logo + gian + rong_chu
    logo = logo.translate((-tong / 2 + rong_logo / 2, 0))
    chu = chu.translate((-tong / 2 + rong_logo + gian - bb[0], -(bb[1] + bb[3]) / 2))
    return logo + chu


def khoi_nhan(sau, cao):
    """Khối nhãn uốn theo mặt ngoài vòng, từ sâu `sau` dưới mặt tới `cao` trên mặt."""
    m = Manifold.extrude(nhan_2d(), 1.0).refine_to_length(1.5)

    def uon(v):
        goc = v[0] / R_NGOAI
        r = R_NGOAI - sau + v[2] * (sau + cao)
        return (r * math.cos(goc), r * math.sin(goc), v[1])

    return m.warp(uon).transform(bien_doi(R_V, TAM_VONG))


def vong():
    """Vòng tròn: in dựng đứng trên mép sau nên không cần vật đỡ, nhựa huỳnh quang để webcam lọc màu."""
    m = _xoay_vong(R_TRONG, R_NGOAI, Z0_V, Z1_V, -THETA, THETA, 0.8)
    for dau in ((-THETA, -THETA + GOC_KHOI), (THETA - GOC_KHOI, THETA)):
        m = m + _xoay_vong(R_KHOI, R_NGOAI, Z0_V, Z1_V, *dau, 0.8)
    for phia in (1, -1):
        m = m - _tru_v(0.85, Z0_V - 0.1, Z0_V + 5.0, _goc_vit(phia), (R_KHOI + R_NGOAI) / 2)
    # đầu vòng ôm sát sườn đầu tay cầm
    return m - khoi_nhan(SAU_CHU, 1.5) - khoi(-0.25)


# ---------------- cổng mở rộng ----------------

def _R_mui(phia):
    """Hệ của cổng ở mũi đầu: z ra ngoài theo pháp tuyến mặt thân."""
    a, b = ab_tai(Z_MUI)
    x = a * math.sqrt(1 - (Y_MUI / b) ** 2)
    u = np.array([x / a ** 2, phia * Y_MUI / b ** 2, 0.0])
    u = u / np.linalg.norm(u)
    v = np.array([0, 0, 1.0])
    return np.column_stack([np.cross(v, u), v, u])


def _goc_mui(phia):
    a, b = ab_tai(Z_MUI)
    return np.array([cx(Z_MUI) + a * math.sqrt(1 - (Y_MUI / b) ** 2), phia * Y_MUI, Z_MUI])


def _vi_tri_cong():
    """(mã cổng, R, gốc ở mặt ngoài) của cả 4 cổng."""
    return [("P1", R_MAT, diem_mat(T_CONG, Y_CONG, -LUN_MAT)), ("P2", R_MAT, diem_mat(T_CONG, -Y_CONG, -LUN_MAT)),
            ("P3", _R_mui(1), _goc_mui(1)), ("P4", _R_mui(-1), _goc_mui(-1))]


def _dat(m, R, o):
    return m.transform(bien_doi(R, o))


def _cong_de():
    return Manifold.cylinder(9.5, 6.0, 6.0, 40).translate([0, 0, -9.5])


def _cong_khoet():
    """Lỗ Ø7 qua vách, buồng gờ nắp Ø9, ô vuông giữ nút 6 × 6 mở phía dưới cho chân và dây."""
    return (Manifold.cylinder(6.5, 3.5, 3.5, 40).translate([0, 0, -2.5]) + Manifold.cylinder(1.4, 4.5, 4.5, 40).translate([0, 0, -3.9])
            + cuc(6.3, 6.3, 6.2, 0, 0, -7.0))


def nut_don():
    """Nắp nút phụ, gốc ở mặt ngoài vách: đầu nhô 1,4 mm, gờ giữ dưới vách."""
    return Manifold.batch_boolean([Manifold.cylinder(3.5, 3.3, 3.3, 40).translate([0, 0, -2.8]), Manifold.cylinder(0.71, 3.3, 2.5, 40).translate([0, 0, 0.69]),
                                   Manifold.cylinder(1.1, 4.3, 4.3, 40).translate([0, 0, -3.7])], OpType.Add)


def nut_phu():
    m = None
    for _, R, o in _vi_tri_cong():
        k = _dat(nut_don(), R, o)
        m = k if m is None else m + k
    return m


# ---------------- chi tiết in ----------------

def nua(m, phia):
    """Giữ phần thuộc một nửa: phia = 1 nửa trái (+y), -1 nửa phải (-y)."""
    return m.trim_by_plane([0, float(phia), 0], 0.0)


def _lang_tru_cua(x0, x1, tang):
    """Lăng trụ thang của nắp pin: trên rộng hơn dưới để nắp không rơi."""
    return Manifold.batch_hull([hop(x0, x1, -10.0 - tang, 10.0 + tang, 0.0, 0.01), hop(x0, x1, -11.3 - tang, 11.3 + tang, 1.99 + tang * 0.75, 2.0 + tang * 0.75)])


def _gai_cua(r):
    x = BAY_XC + 16.3
    return Manifold.sphere(r, 24).translate([x, 5.0, 1.45]) + Manifold.sphere(r, 24).translate([x, -5.0, 1.45])


def _tab_sau(y):
    return hop_mat(-60, -20, y - 3.5, y + 3.5, -8.6, -LUN_MAT - DAY_MAT + 0.01)


N_VIT_MAT = -5.6        # độ sâu 2 vít giữ mặt tối
Y_VIT_MAT = 9.0


def than_nua(phia):
    """Một nửa thân, tách theo mặt y = 0: mở ra lắp mạch, khép lại bằng 7 vít và lõi thanh móc."""
    t = khoi() - khoi(VACH).trim_by_plane([0, 0, 1], Z_SAN)
    # gờ đỡ mặt tối, chạy quanh miệng thân
    go = (khoi(VACH - 0.1) - khoi(VACH + 1.6)).trim_by_plane(list(-n_mat), -(DD - LUN_MAT - DAY_MAT)).trim_by_plane(list(n_mat), DD - LUN_MAT - DAY_MAT - 1.8)
    for y in (-Y_VIT_MAT, Y_VIT_MAT):
        go = go - hop_mat(-60, -20, y - 3.8, y + 3.8, -11.0, 0.5)
    for ma, R, o in _vi_tri_cong():
        if ma in ("P1", "P2"):
            go = go - _dat(Manifold.cylinder(14.0, 6.3, 6.3, 40).translate([0, 0, -13.0]), R, o)
    t = t + go
    # 4 trụ đỡ mặt dưới ESP32 (giữa 2 hàng chân), 4 vấu chặn mép trên mạch
    zb = ESP["z"]
    for sy in (-1, 1):
        t = t + (hop(-22.0, -17.0, sy * 7 - 2, sy * 7 + 2, 40, zb) ^ khoi(0.5))
        y0, y1 = (8.5, 13.0) if sy > 0 else (-13.0, -8.5)
        t = t + (hop(29.5, 32.5, y0, y1, 40, zb) ^ khoi(0.5))
    for x in (-6.0, 18.0):
        for sy in (-1, 1):
            y0, y1 = (13.0, 40.0) if sy > 0 else (-40.0, -13.0)
            t = t + (hop(x - 3, x + 3, y0, y1, zb + 1.8, zb + 4.0) ^ khoi(0.5))
    # vách ngăn pin với cò, hốc Hall, khe luồn chân, chỗ tựa lò xo
    vach = hop(X_VACH[0], X_VACH[1], -11, 11, 30, 60) ^ khoi(0.5)
    vach = vach - cuc(1.9, 4.4, 3.4, X_HALL - 0.85, Y_NC, Z_NC) - cuc(3, 3.6, 14, X_HALL - 0.85, Y_NC, Z_NC + 8)
    vach = vach - tru_x(2.2, X_VACH[1] - 1.5, X_VACH[1] + 1, 0, Z_LO_XO)
    t = t + vach
    # tai trục cò hai bên
    for y0, y1 in ((Y_CO + 0.5, Y_CO + 2.5), (-Y_CO - 2.5, -Y_CO - 0.5)):
        t = t + (hop(19, 31, y0, y1, 44, 61) ^ khoi(0.5))
    # khối mũi sàn, chứa hốc khóa nắp pin
    t = t + (hop(BAY_XC + 14.2, 60, -12, 12, 2.0, 4.2) ^ khoi(0.5))
    # trụ vít nối vào vách gần nhất
    for x, z, r in VIT:
        x0, x1 = (x, 60) if x > 0 else (-60, x)
        y0 = -5.2 if z > 70 else -8    # vít ở lưng đầu nằm dưới tai mặt tối, chỉ dài M2×8
        t = t + ((tru_y(r, y0, Y_VAI, x, z) + hop(x0, x1, y0, Y_VAI, z - r, z + r)) ^ khoi(0.5))
    t = t + (tai_vong(phia) - khoi(VACH))
    ma_mui = "P3" if phia > 0 else "P4"
    for ma, R, o in _vi_tri_cong():
        if ma == ma_mui:
            t = t + (_dat(_cong_de(), R, o) ^ khoi(0.5))
    t = nua(t, phia)
    # tai giữ thanh móc dây, ở lưng đáy cán
    y0, y1 = (Y_TAI[0], Y_TAI[1]) if phia > 0 else (-Y_TAI[1], -Y_TAI[0])
    tai = Manifold.batch_hull([tru_y(R_TAI, y0, y1, X_THANH, Z_THANH), hop(-18, -15, y0, y1, 2.5, 10)])
    t = t + (tai - khoi(VACH))
    # lỗ lọt pin ở sàn, rãnh trượt nắp pin, hốc khóa nắp
    t = t - hop(BAY_XC - BAY_X, BAY_XC + BAY_X, -BAY_Y, BAY_Y, -1, Z_SAN + 0.1)
    t = t - _lang_tru_cua(DOOR_X0 - 0.2, 60, 0.2) - hop(DOOR_X0 - 0.2, 60, -10.2, 10.2, -3, 0.01) - _gai_cua(1.1)
    # cổng micro-USB ở lưng đầu
    t = t - hop(-60, ESP["x"][0] - 0.5, -6.5, 6.5, zb - 0.5, zb + 7.0)
    # khe công tắc nguồn ở bụng đầu, trước cò
    t = t - hop(34.2, 36.8, -2.8, 2.8, 48, 59.6)
    # ô cửa và lỗ trục cò
    t = t - o_cua(0.5)
    t = t - tru_y(1.1, -Y_CO - 3, Y_CO + 3, CHOT[0], CHOT[2])
    # 2 lỗ vít M2 giữ mặt tối, ở lưng đầu, có hốc nông cho đầu vít
    for y in (-Y_VIT_MAT, Y_VIT_MAT):
        tn = mep_t(y, N_VIT_MAT, -1)
        t = t - tru_mat(1.15, tn - 2, tn + 6, y, N_VIT_MAT) - tru_mat(2.1, tn - 2, tn + 0.5, y, N_VIT_MAT)
    # lỗ vít ghép: nửa trái có hốc đầu vít và lỗ thoát, nửa phải có lỗ mồi cho vít tự ren
    if phia > 0:
        for x, z, _ in VIT:
            t = t - tru_y(2.1, Y_VAI, 40, x, z) - tru_y(1.1, -0.1, Y_VAI + 0.1, x, z)
        t = t - tru_y(2.1, Y_VAI_THANH, 12, X_THANH, Z_THANH) - tru_y(1.15, Y_TAI[0] - 0.1, Y_VAI_THANH + 0.1, X_THANH, Z_THANH)
        t = t - tru_y(R_THANH + 0.15, Y_TAI[0] - 0.1, Y_TAI[0] + 0.8, X_THANH, Z_THANH)
    else:
        for x, z, _ in VIT:
            t = t - tru_y(0.85, -_sau_moi(x, z), 0.1, x, z)
        t = t - tru_y(0.85, -12, -Y_TAI[0] + 0.1, X_THANH, Z_THANH)
        t = t - tru_y(R_THANH + 0.15, -Y_TAI[0] - 0.8, -Y_TAI[0] + 0.1, X_THANH, Z_THANH)
    t = t - lo_tai_vong(phia)
    for ma, R, o in _vi_tri_cong():
        if ma == ma_mui:
            t = t - _dat(_cong_khoet(), R, o)
    return t.trim_by_plane(list(-n_mat), -DD)


def _sau_moi(x, z):
    """Lỗ mồi dài tối đa 7 mm, chừa 1 mm tới mặt ngoài."""
    if z > 70:
        return 4.5
    a, b = ab_tai(z)
    return min(7.0, b * math.sqrt(max(1 - ((x - cx(z)) / a) ** 2, 0.0)) - 1.0)


def thanh():
    """Thanh móc dây: ống Ø4 lồng vào 2 tai, lõi vít M2×16 chịu lực."""
    l = Y_TAI[0] + 0.75
    return tru_y(R_THANH, -l, l, X_THANH, Z_THANH) - tru_y(1.15, -l - 1, l + 1, X_THANH, Z_THANH)


def _hex(ri_ro_z):
    """Khối xoay 6 cạnh quanh tâm cần, đỉnh lục giác hướng theo trục t như logo."""
    return Manifold.revolve(CrossSection([ri_ro_z]), 6).rotate([0, 0, 90]).transform(bien_doi(R_MAT, TAM_CAN))


def nap():
    """Mặt tối bầu dục: giữ joystick, lún trong vành trắng, móc phía trước và 2 vít phía sau."""
    d1, d0 = DD - LUN_MAT, DD - LUN_MAT - DAY_MAT
    c = khoi(VACH + 0.2).trim_by_plane(list(-n_mat), -d1).trim_by_plane(list(n_mat), d0)
    # 2 tai bắt vít ở lưng
    for y in (-Y_VIT_MAT, Y_VIT_MAT):
        c = c + (_tab_sau(y) ^ khoi(VACH + 0.25))
        tn = mep_t(y, N_VIT_MAT, -1)
        c = c - tru_mat(0.95, tn - 2, tn + 12, y, N_VIT_MAT)
    # móc phía trước, luồn dưới gờ thân
    tt = mep_t(0, -LUN_MAT - DAY_MAT - 1.0, 1)
    c = c + (hop_mat(tt - 10.0, tt - 6.5, -3, 3, -7.0, -LUN_MAT - DAY_MAT + 0.01) ^ khoi(VACH + 0.25))
    c = c + (hop_mat(tt - 10.0, tt - 2.0, -3, 3, -7.0, -5.0) ^ khoi(VACH + 0.25))
    # lỗ lục giác cho chén, 4 trụ đỡ mạch KY-023
    c = c - _hex([[0, -8.0], [12.5, -8.0], [R_CHEN + 0.2, -1.5], [R_CHEN + 0.2, 2], [0, 2]])
    tam_ky = TAM_CAN - KY_SAU * n_mat
    tru = None
    for sx in (-15, 15):
        for sy in (-11, 11):
            k = tru_z(1.7, 0.8, KY_SAU - LUN_MAT - 0.5, sx, sy, 20).transform(bien_doi(R_MAT, tam_ky))
            tru = k if tru is None else tru + k
    c = c + tru
    # 2 cổng mở rộng cho ngón cái
    for ma, R, o in _vi_tri_cong():
        if ma in ("P1", "P2"):
            c = c + (_dat(_cong_de(), R, o) ^ khoi(VACH + 0.25))
    for ma, R, o in _vi_tri_cong():
        if ma in ("P1", "P2"):
            c = c - _dat(_cong_khoet(), R, o)
    return c


def chen():
    """Chén lõm lục giác màu nhấn quanh cần joystick, lấy hình từ logo."""
    return _hex([[12.5, -7.5], [R_CHEN, -1.5], [R_CHEN, -LUN_MAT], [R_CHEN - 1.0, -LUN_MAT], [12.5, -5.4]])


def co():
    """Cò Hall: tay cò hở ra ngoài thân, quay quanh trục Ø2."""
    r = 3.0
    y = Y_CO - r
    dem = Manifold.batch_hull([Manifold.sphere(r, 28).translate([x, sy * y, z]) for x, z in ((26.5, 37.5), (29.5, 49.0)) for sy in (-1, 1)])
    c = dem + Manifold.batch_hull([tru_y(3.2, -Y_CO, Y_CO, CHOT[0], CHOT[2]), hop(23.5, 31.0, -Y_CO, Y_CO, 47.0, 50.0)])
    # bệ và hốc nam châm Ø6 × 3 (mặt phẳng hướng về Hall)
    c = c + tru_x(4.8, X_NC, 27.0, Y_NC, Z_NC)
    # bệ lò xo; vấu chặn tì vào vách ở góc bóp hết
    c = c + tru_x(2.9, 21.5, 27.0, 0, Z_LO_XO)
    c = c + hop(X_VAU, 25.0, -7.8, -4.5, 40.7, 43.3)
    c = c - tru_x(2.2, 21, 23.5, 0, Z_LO_XO)
    c = c - tru_x(3.1, X_NC - 1, X_NC + 3.05, Y_NC, Z_NC)
    return c - tru_y(1.1, -Y_CO - 1, Y_CO + 1, CHOT[0], CHOT[2])


def nap_pin():
    """Nắp khoang pin ở đáy cán: trượt theo ray về phía trước, 2 gai nhỏ khóa nắp, không cần vít."""
    m = _lang_tru_cua(DOOR_X0, 60, 0.0) ^ khoi()
    for x in (6.0, 9.5, 13.0):
        m = m - cuc(1.4, 16, 1.2, x, 0, 0)
    return m + _gai_cua(1.0)


def khoi_linh_kien():
    e, h = ESP, HA_AP
    x0, x1, zb = e["x"][0], e["x"][1], e["z"]
    esp = (hop(x0, x1, -14, 14, zb, zb + 1.6)
           + hop(x1 - 25.5, x1, -9, 9, zb + 1.6, zb + 4.8)                   # module WROOM ở đầu trước
           + hop(x0 - 1.0, x0 + 5, -4, 4, zb + 1.6, zb + 4.6)                # cổng micro-USB ở lưng
           + hop(x0 + 3.5, x1 - 3.5, -13.8, -11.3, zb - 2.5, zb)
           + hop(x0 + 3.5, x1 - 3.5, 11.3, 13.8, zb - 2.5, zb))
    pin = Manifold.batch_hull([tru_z(2.0, Z_PIN, Z_PIN + 48.5, BAY_XC + sx * 11.25, sy * 6.75, 20) for sx in (-1, 1) for sy in (-1, 1)])
    kep = hop(BAY_XC - 12.5, BAY_XC + 12.5, -8.0, 8.0, Z_PIN + 48.5, Z_PIN + 57.0)
    ha_ap = hop(h["x"][0], h["x"][1], h["y"][0], h["y"][1], h["z"][0], h["z"][0] + 1.6) + hop(h["x"][0] + 2, h["x"][1] - 2, h["y"][0] + 2, h["y"][1] - 2, h["z"][0] + 1.6, h["z"][1])
    ct = hop(33.4, 37.6, -4.2, 4.2, 59.5, 63.0) + hop(34.5, 36.5, -1.0, 1.0, 56.1, 59.5)
    bmi = hop(x1 - 21, x1 - 3, -6.5, 6.5, zb + 4.8, zb + 6.4)
    tam_ky = TAM_CAN - KY_SAU * n_mat
    ky = (cuc(34, 26, 1.6) + cuc(16, 16, 11, 0, 0, 6.3) + tru_z(2, 10, 24, 0, 0, 24) + tru_z(10, 21, 26, 0, 0, 48)).transform(bien_doi(R_MAT, tam_ky))
    hall = cuc(1.5, 4, 3, X_HALL - 0.75, Y_NC, Z_NC) + cuc(1.0, 2.6, 6, X_HALL - 0.8, Y_NC, Z_NC + 4.0)
    nc = tru_x(3, X_NC, X_NC + 3, Y_NC, Z_NC)
    vit = None
    for x, z, _ in VIT:
        k = tru_y(0.8, -_sau_moi(x, z) + 0.5, Y_VAI, x, z) + tru_y(1.9, Y_VAI, Y_VAI + 1.4, x, z)
        vit = k if vit is None else vit + k
    vit = vit + tru_y(0.8, -8.7, Y_VAI_THANH, X_THANH, Z_THANH) + tru_y(1.9, Y_VAI_THANH, Y_VAI_THANH + 1.4, X_THANH, Z_THANH)
    for y in (-Y_VIT_MAT, Y_VIT_MAT):
        tn = mep_t(y, N_VIT_MAT, -1)
        vit = vit + tru_mat(0.8, tn + 0.5, tn + 7.0, y, N_VIT_MAT) + tru_mat(1.9, tn - 0.9, tn + 0.5, y, N_VIT_MAT)
    for phia in (1, -1):
        g, t0 = _goc_vit(phia), Z0_V - DAY_LUG
        rc = (R_KHOI + R_NGOAI) / 2
        vit = vit + _tru_v(0.8, t0 + 1.2, Z0_V + 3.6, g, rc) + _tru_v(1.9, t0 - 0.2, t0 + 1.2, g, rc)
    tac = None
    for _, R, o in _vi_tri_cong():
        k = _dat(cuc(6, 6, 3.5, 0, 0, -6.95) + Manifold.cylinder(1.25, 1.75, 1.75, 24).translate([0, 0, -5.2]), R, o)
        tac = k if tac is None else tac + k
    return [("nut6x6", "Nút bấm 6 × 6", "#26292c", tac), ("esp32", "ESP32 DevKitC 38 chân", "#1d2b33", esp), ("pin9v", "Pin vuông 9V", "#c9862b", pin), ("kep_pin", "Kẹp pin 9V", "#26292c", kep),
            ("ha_ap", "Mạch hạ áp MP1584", "#2563a6", ha_ap), ("cong_tac", "Công tắc trượt", "#3b4a52", ct), ("bmi160", "BMI160", "#7b4fd6", bmi),
            ("ky023", "Joystick KY-023", "#26292c", ky), ("hall", "Hall SS49E", "#c0392b", hall), ("nam_cham", "Nam châm Ø6×3", "#9aa3a8", nc),
            ("vit", "Vít M2", "#aab4ba", vit)]


def quay_co(m, goc):
    R = quay("y", goc)
    return m.transform(bien_doi(R, CHOT - R @ CHOT))


# ---------------- bàn tay (chỉ để xem cỡ) ----------------

def _xuong(*diem):
    m = None
    for a, b in zip(diem, diem[1:]):
        s = Manifold.batch_hull([Manifold.sphere(a[3], 24).translate(list(a[:3])), Manifold.sphere(b[3], 24).translate(list(b[:3]))])
        m = s if m is None else m + s
    return m


def ban_tay():
    """Tay phải cầm kiểu báng súng: lòng tay ôm lưng và sườn phải (-y), ngón trỏ trên cò, ngón cái lên joystick."""
    dinh = TAM_CAN + 7.0 * n_mat
    long_tay = Manifold.batch_hull([Manifold.sphere(1, 32).scale([17, 11, 30]).translate([-20, -20, 28]),
                                    Manifold.sphere(1, 32).scale([12, 9, 24]).translate([0, -24, 26]),
                                    Manifold.sphere(1, 32).scale([14, 12, 20]).translate([-42, -19, 14])])
    cang = _xuong((-42, -19, 14, 25), (-160, -18, -55, 32))
    m = long_tay + cang
    for z, r in ((31, 8.5), (17.5, 8.0), (6.5, 6.5)):
        an, bn = ab_tai(z)
        c = cx(z)
        m = m + _xuong((4, -bn - r + 2, z, r + 0.5), (c + an * 0.72 + r - 1, -bn * 0.75 - 2, z, r),
                       (c + an + r - 1, 0, z, r), (c + an * 0.8, (bn + r - 1) * 0.95, z, r - 0.5), (c + an * 0.25, bn + r - 1, z, r - 1))
    # ngón trỏ: gác lên tay cò
    m = m + _xuong((4, -24, 44, 9), (24, -17, 45, 8.5), (38, -1, 44, 8), (31, 12, 42, 7))
    m = m + _xuong((-30, -16, 56, 13), (-27, -13, 80, 10.5), (dinh[0] - 15, dinh[1] - 3, dinh[2] + 2, 9.5), (dinh[0] + 2, dinh[1], dinh[2] + 1, 8.5))
    return m


# ---------------- xuất ----------------

def ghi_stl(m, duong):
    mesh = m.to_mesh()
    v = np.asarray(mesh.vert_properties)[:, :3].astype(np.float32)
    f = np.asarray(mesh.tri_verts)
    tri = v[f]
    n = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
    ln = np.linalg.norm(n, axis=1, keepdims=True)
    n = (n / np.where(ln == 0, 1, ln)).astype(np.float32)
    with open(duong, "wb") as fh:
        fh.write(b"VERA Arc".ljust(80, b" "))
        fh.write(struct.pack("<I", len(f)))
        rec = np.zeros(len(f), dtype=[("n", "<f4", 3), ("v", "<f4", (3, 3)), ("a", "<u2")])
        rec["n"], rec["v"] = n, tri
        fh.write(rec.tobytes())


def dat_len_ban(m):
    bb = m.bounding_box()
    return m.translate([-(bb[0] + bb[3]) / 2, -(bb[1] + bb[4]) / 2, -bb[2]])


def trong_tam(m):
    """Trọng tâm khối đặc, tính từ lưới tam giác."""
    mesh = m.to_mesh()
    v = np.asarray(mesh.vert_properties)[:, :3]
    t = v[np.asarray(mesh.tri_verts)]
    w = np.einsum("ij,ij->i", t[:, 0], np.cross(t[:, 1], t[:, 2]))
    return (t.sum(axis=1) / 4 * w[:, None]).sum(axis=0) / w.sum()


def chi_tiet_in():
    """(mã, tên, màu, khối ở tư thế lắp, ma trận về hướng in, hướng tách rời, cách in)."""
    Rv = R_MAT.T
    return [
        ("than_phai", "Thân, nửa phải", "#eef2f4", than_nua(-1), quay("x", -90), [0, -40, 0], "mặt ghép xuống bàn, đỡ bên trong"),
        ("than_trai", "Thân, nửa trái (có hốc vít)", "#eef2f4", than_nua(1), quay("x", 90), [0, 48, 0], "mặt ghép xuống bàn, đỡ bên trong"),
        ("nap", "Mặt tối bầu dục", "#15191d", nap(), quay("x", 180) @ Rv, list(46 * n_mat), "mặt đỉnh xuống bàn"),
        ("chen", "Chén lục giác", "#18c8e6", chen(), quay("x", 180) @ Rv, list(72 * n_mat), "mặt lớn xuống bàn"),
        ("co", "Cò Hall", "#aab4ba", co(), quay("x", 90), [52, 0, -8], "nằm nghiêng, mặt bên xuống bàn, đỡ dưới tay cò"),
        ("thanh", "Thanh móc dây", "#1b2329", thanh(), quay("x", 90), [-34, 0, -6], "dựng đứng"),
        ("vong", "Vòng tròn có logo", "#18c8e6", vong(), R_V.T, list(46 * w_vong), "dựng đứng trên mép sau, không cần vật đỡ"),
        ("nap_pin", "Nắp khoang pin", "#15191d", nap_pin(), np.eye(3), [10, 0, -30], "nằm phẳng, gai khóa hướng lên"),
        ("nut_phu", "Nắp nút phụ (in 4 cái)", "#aab4ba", nut_phu(), np.eye(3), [34, 0, 40], "in 4 cái, mặt gờ xuống bàn"),
    ]


# khối lượng linh kiện (g), chưa cân thật: pin theo số hãng công bố, còn lại ước tính
LINH_KIEN_G = {"nut6x6": 2, "esp32": 10, "pin9v": 46, "kep_pin": 3, "ha_ap": 3, "cong_tac": 1, "bmi160": 1, "ky023": 10, "hall": 1, "nam_cham": 1, "vit": 6}
# hướng tách rời của linh kiện khi xem 3D
TACH_LK = {"nut6x6": [22, 0, 26], "esp32": [0, 10, 18], "pin9v": [0, 0, -74], "kep_pin": [0, 0, -18], "ha_ap": [-30, 0, 30], "cong_tac": [24, 0, -18], "bmi160": [0, 10, 26],
           "ky023": list(30 * n_mat), "hall": [18, 34, -4], "nam_cham": [42, 8, -8], "vit": [0, 72, 0]}


# gói gửi in: (file, chi tiết trong chi_tiet_in, tên, số lượng, màu đề xuất)
GOI_IN = [("than", ("than_phai", "than_trai"), "Thân, gồm 2 nửa trong 1 file", 1, "Trắng"),
          ("vong", ("vong",), "Vòng tròn có logo", 1, "Xanh ngọc huỳnh quang"),
          ("day_tren", ("nap",), "Đáy trên (mặt tối bầu dục)", 1, "Đen"),
          ("day_duoi", ("nap_pin",), "Đáy dưới (nắp khoang pin)", 1, "Đen"),
          ("co", ("co",), "Cò", 1, "Bạc hoặc xám"),
          ("chen", ("chen",), "Chén lục giác quanh joystick", 1, "Xanh ngọc huỳnh quang"),
          ("thanh", ("thanh",), "Ống thanh móc dây", 3, "Đen"),
          ("nut_phu", ("nut_phu",), "Nắp nút phụ", 4, "Bạc hoặc xám")]


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    xem = sys.argv[1] if len(sys.argv) > 1 else None
    os.makedirs(os.path.join(THU_MUC, "in"), exist_ok=True)
    tt = []
    ca_bo = None
    tong, mo = 0.0, np.zeros(3)
    khoi_in, gam, cach = {}, {}, {}
    for ma, ten, mau, m, R, tach, cach_in in chi_tiet_in():
        ca_bo = m if ca_bo is None else ca_bo + m
        in_m = dat_len_ban(nut_don() if ma == "nut_phu" else m.transform(bien_doi(R, [0, 0, 0])))
        v = in_m.volume() / 1000
        if ma == "nut_phu":
            v *= 4
        g = v * 1.24 * 0.92
        tong += g
        mo += g * trong_tam(m)
        khoi_in[ma], gam[ma], cach[ma] = in_m, g, cach_in
        tt.append({"ma": ma, "ten": ten, "mau": mau, "tach": [round(float(x), 1) for x in tach]})
        if xem:
            ghi_stl(m, os.path.join(xem, ma + ".stl"))
    # 2 nửa thân đặt cạnh nhau trên bàn in, cách 10 mm, cùng nằm trong 1 file
    bb = khoi_in["than_phai"].bounding_box()
    khoi_in["than"] = dat_len_ban(khoi_in["than_phai"].translate([-(bb[3] - bb[0]) / 2 - 5, 0, 0]) + khoi_in["than_trai"].translate([(bb[3] - bb[0]) / 2 + 5, 0, 0]))
    goi_in = []
    for file, nguon, ten, so, mau in GOI_IN:
        m = khoi_in["than"] if file == "than" else khoi_in[nguon[0]]
        ghi_stl(m, os.path.join(THU_MUC, "in", file + ".stl"))
        bb = m.bounding_box()
        kt = [round(bb[3] - bb[0], 1), round(bb[4] - bb[1], 1), round(bb[5] - bb[2], 1)]
        g = sum(gam[n] for n in nguon)
        goi_in.append({"file": file, "ten": ten, "so_luong": so, "mau": mau, "kich_thuoc": kt, "gam_pla": round(g, 1), "cach_in": cach[nguon[0]], "loi": m.status().name})
        print(f"{file:10s} {kt} mm ≈ {g:5.1f} g  x{so}  {m.num_tri():6d} tam giác {m.status().name}")
    nhua = tong
    for ma, ten, mau, m in khoi_linh_kien():
        ca_bo = ca_bo + m
        tong += LINH_KIEN_G[ma]
        mo += LINH_KIEN_G[ma] * trong_tam(m)
        if xem:
            ghi_stl(m, os.path.join(xem, ma + ".stl"))
            tt.append({"ma": ma, "ten": ten, "mau": mau, "tach": [round(float(x), 1) for x in TACH_LK[ma]], "linh_kien": True})
    bb = ca_bo.bounding_box()
    kt = [round(bb[3] - bb[0]), round(bb[4] - bb[1]), round(bb[5] - bb[2])]
    tam = mo / tong
    print(f"cả bộ {kt} mm, nhựa {nhua:.0f} g, tổng ≈ {tong:.0f} g, trọng tâm x {tam[0]:.1f} y {tam[1]:.1f} z {tam[2]:.1f}")
    print(f"vòng: Ø{2 * R_NGOAI:.1f} ngoài, góc mở {2 * math.degrees(THETA):.0f}°")
    if xem:
        ghi_stl(khoi_nhan(SAU_CHU, -0.05), os.path.join(xem, "chu.stl"))
        tt.append({"ma": "chu", "ten": "Logo và chữ khắc (tô màu khi xem)", "mau": "#15191d", "tach": [round(float(x), 1) for x in 46 * w_vong], "chi_xem": True})
        ghi_stl(ban_tay(), os.path.join(xem, "ban_tay.stl"))
        with open(os.path.join(xem, "canh.json"), "w", encoding="utf-8") as fh:
            json.dump({"chi_tiet": tt, "goi_in": goi_in, "kich_thuoc": kt, "nhua_g": round(nhua), "tong_g": round(tong), "d_vong": round(2 * R_NGOAI), "goc_vong": round(2 * math.degrees(THETA))}, fh, ensure_ascii=False, indent=1)
    with open(os.path.join(THU_MUC, "thong_so.json"), "w", encoding="utf-8") as fh:
        json.dump(goi_in, fh, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
