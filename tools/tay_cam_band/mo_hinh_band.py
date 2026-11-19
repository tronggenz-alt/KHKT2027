"""Tay cầm VR "VERA Band": đeo vào tay bằng quai qua mu bàn tay, thả lỏng tay vẫn không rơi. Mặt tối bầu dục, pin vuông 9V trong cán, thân 2 nửa bắt vít.

Chạy:  python tools/tay_cam_band/mo_hinh_band.py [thư mục xem]
Ra:
  docs/tay-cam-band/in/*.stl        chi tiết in 3D, đã xoay đúng hướng in (đáy ở z = 0)
  docs/tay-cam-band/thong_so.json   kích thước, khối lượng
  <thư mục xem>/*.stl + canh.json   cả bộ ở tư thế lắp, cho trang xem 3D

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
from manifold3d import CrossSection, FillRule, JoinType, Manifold, OpType
from matplotlib.font_manager import FontProperties
from matplotlib.textpath import TextPath

GOC = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
THU_MUC = os.path.join(GOC, "docs", "tay-cam-band")
SEG = 96

VACH = 1.5              # bề dày vỏ (≈ 4 đường in với đầu 0,4 mm)
DU_RONG = 0.5           # nới thân ra mỗi bên, cùng với vách mỏng đi 0,3 thì ruột rộng thêm 0,8 mm quanh linh kiện
Z_DAY = -14.0          # đáy cán: cán dài thêm 14 mm so với bản Arc để đủ chỗ cho 3 ngón giữa, áp út, út của tay người lớn
# dáng thân: (z, nửa sâu a theo x, nửa rộng b theo y). Cán ≈ 40 × 29 mm (≈ Ø35, docs/13), vừa đủ ôm pin vuông; đầu to ra để đặt ESP32 nằm ngang
DANG = [(Z_DAY, 18.2, 13.0), (Z_DAY + 1.5, 19.2, 13.8), (Z_DAY + 6, 19.7, 14.3), (20, 20.0, 14.5), (36, 20.5, 14.9), (46, 23.5, 16.4), (54, 30.0, 21.0),
        (60, 36.0, 25.0), (66, 38.5, 26.2), (74, 37.0, 26.4), (84, 33.5, 26.0), (94, 31.0, 25.0), (104, 29.0, 23.0), (114, 26.0, 20.0)]
DANG = [(z, a + DU_RONG, b + DU_RONG) for z, a, b in DANG]
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
BAY_X = 14.25           # lỗ lọt pin dư 1 mm mỗi bên
BAY_Y = 9.75
Z_SAN = Z_DAY + 2.4     # mặt trên sàn cán
Z_PIN = Z_DAY + 2.25    # mặt dưới pin, tựa nắp đáy (2 mm) qua lớp xốp mỏng
DOOR_X0 = BAY_XC - BAY_X - 0.7
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
VIT = [(15.4, -2.0, 2.4), (15.4, 28.0, 2.4), (-17.7, 0.0, 2.4), (-17.8, 40.0, 2.4), (-24.0, 57.0, 2.4), (-29.5, 72.8, 2.4), (37.0, 67.0, 2.4)]
Y_VAI = 3.5             # mặt tì đầu vít
# quai đeo: băng gai dính 20 mm chạy dọc mu bàn tay. Đầu trên luồn qua khuyên ở sườn đầu, đầu dưới luồn qua khe ở đế phình rồi gập ngược dán lại để chỉnh độ chặt
RONG_QUAI, DAY_QUAI = 20.0, 1.4
KHUYEN_X = (-13.0, 9.0)         # lòng khuyên trên, rộng hơn quai 2 mm
KHUYEN_Z = (68.5, 74.5)
KHUYEN_Y = -32.0                # tâm thanh ngang của khuyên
KHE_X = (-16.5, 4.5)            # khe dưới, xuyên dọc đế phình
KHE_Y = (-22.5, -19.3)
# tấm mu tay: tấm cong nằm trên quai, khắc logo và chữ, mặt trong dán xốp
TAM_X, TAM_Z = (-20.0, 12.0), (6.0, 46.0)
TAM_R, TAM_DAY = 70.0, 2.4
TAM_TAM = (-4.0, 21.0)          # trục cong của tấm (x, y), song song trục z; mặt trong ở y = -49, ngay trên mu bàn tay
TAM_KHE_Z = ((9.0, 12.0), (40.0, 43.0))
CAO_LOGO_TAM, CO_CHU_TAM = 12.0, 5.2
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


# ---------------- quai và tấm mu tay ----------------

@functools.lru_cache(maxsize=None)
def phinh(lui=0.0):
    """Đế phình về phía mu bàn tay: chứa khe luồn quai dưới và đỡ mép bàn tay; lui > 0 cho khối bên trong."""
    zt = Z_DAY + 17.0
    a, b = ab_tai(zt)
    tren = Manifold.extrude(elip(a - lui, b - lui), 0.01).translate([cx(zt), 0, zt - lui])
    day = [Manifold.extrude(elip(21.5 - v - lui, 20.5 - v - lui), 0.01).translate([-4.0, -7.0, Z_DAY + z + lui]) for z, v in ((0.0, 1.0), (1.0, 0.0), (5.0, 0.0))]
    return Manifold.batch_hull(day + [tren])


def khe_duoi():
    """Khe dọc xuyên đế phình cho quai đi xuống rồi gập ngược lên."""
    return hop(KHE_X[0], KHE_X[1], KHE_Y[0], KHE_Y[1], Z_DAY - 1.0, Z_DAY + 17.5)


def khuyen_tren():
    """Khuyên trên ở sườn đầu, phía mu bàn tay: quai luồn giữa thân và thanh ngang."""
    x0, x1 = KHUYEN_X
    z0, z1 = KHUYEN_Z
    m = Manifold.batch_hull([tru_x(1.6, x0 - 3.5, x1 + 3.5, KHUYEN_Y, z) for z in (z0 + 1.6, z1 - 1.6)])
    for xa, xb in ((x0 - 3.5, x0), (x1, x1 + 3.5)):
        m = m + hop(xa, xb, KHUYEN_Y - 1.6, -16.0, z0, z1)
    return m


def _uon_tam(r_mat, sau, cao):
    """Hàm uốn một khối phẳng (u theo x, v theo z, w là bề dày) lên mặt cong của tấm mu tay."""
    cx_t, cy_t = TAM_TAM
    zc = (TAM_Z[0] + TAM_Z[1]) / 2

    def uon(v):
        goc = v[0] / r_mat
        r = r_mat - sau + v[2] * (sau + cao)
        return (cx_t + r * math.sin(goc), cy_t - r * math.cos(goc), zc + v[1])

    return uon


def nhan_doc_2d():
    """Logo ở trên, chữ VERA LAB ở dưới, căn giữa quanh gốc."""
    duong = TextPath((0, 0), CHU, size=CO_CHU_TAM, prop=FontProperties(fname=PHONG))
    vien = [np.asarray(q)[:-1].tolist() for q in duong.to_polygons() if len(q) > 3]
    chu = CrossSection(vien, FillRule.EvenOdd)
    bb = chu.bounds()
    chu = chu.translate((-(bb[0] + bb[2]) / 2, -bb[3] - 2.0))
    logo = _logo_2d(CAO_LOGO_TAM)
    lb = logo.bounds()
    logo = logo.translate((0, -lb[1] + 2.0))
    m = chu + logo
    b2 = m.bounds()
    return m.translate((0, -(b2[1] + b2[3]) / 2))


def khoi_chu_tam(sau, cao):
    m = Manifold.extrude(nhan_doc_2d(), 1.0).refine_to_length(1.2)
    return m.warp(_uon_tam(TAM_R + TAM_DAY, sau, cao))


def tam_mu_tay():
    """Tấm cong ôm mu bàn tay, in dựng đứng; 2 khe cho quai luồn dưới tấm."""
    cx_t, cy_t = TAM_TAM
    goc = math.asin(((TAM_X[1] - TAM_X[0]) / 2 + 4) / TAM_R)
    vanh = []
    for r, gs in ((TAM_R, np.linspace(-goc, goc, 64)), (TAM_R + TAM_DAY, np.linspace(goc, -goc, 64))):
        vanh += [[cx_t + r * math.sin(g), cy_t - r * math.cos(g)] for g in gs]
    vo = Manifold.extrude(CrossSection([vanh], FillRule.EvenOdd), TAM_Z[1] - TAM_Z[0] + 10).translate([0, 0, TAM_Z[0] - 5])
    r = 5.0
    w, h = TAM_X[1] - TAM_X[0], TAM_Z[1] - TAM_Z[0]
    mat = CrossSection.square([w - 2 * r, h - 2 * r], True).offset(r, JoinType.Round, 2.0, 32)
    mat = mat.translate(((TAM_X[0] + TAM_X[1]) / 2, (TAM_Z[0] + TAM_Z[1]) / 2))
    khung = Manifold.extrude(mat, 40).rotate([90, 0, 0]).translate([0, -16, 0])
    m = vo ^ khung
    for z0, z1 in TAM_KHE_Z:
        m = m - hop(KHE_X[0] + 1.5, KHE_X[1] + 1.5, TAM_TAM[1] - TAM_R - 20, TAM_TAM[1] - TAM_R + 20, z0, z1)
    return m - khoi_chu_tam(SAU_CHU, 1.5)


def duong_quai():
    """Các điểm trên đường giữa của quai: từ khuyên trên xuống dưới tấm mu tay, qua khe đế phình rồi gập ngược."""
    d = Z_DAY
    diem = [(-2.0, -28.2, 63.0), (-2.0, -28.2, 72.5), (-2.0, -32.0, 76.4), (-2.0, -35.6, 72.5), (-3.0, -47.6, 48.0), (-4.0, -47.6, 4.0),
            (-6.0, -36.0, d + 9.0), (-6.0, -20.9, d + 6.0), (-6.0, -20.9, d - 0.8), (-6.0, -24.0, d - 1.8), (-6.0, -28.6, d + 1.0), (-6.0, -29.6, d + 9.0)]
    return diem


def quai():
    """Quai băng gai dính, chỉ để xem."""
    diem = duong_quai()
    m = None
    for p, q in zip(diem, diem[1:]):
        k = Manifold.batch_hull([cuc(RONG_QUAI, DAY_QUAI, DAY_QUAI, *p), cuc(RONG_QUAI, DAY_QUAI, DAY_QUAI, *q)])
        m = k if m is None else m + k
    return m


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
    return Manifold.batch_hull([hop(x0, x1, -10.0 - tang, 10.0 + tang, Z_DAY, Z_DAY + 0.01), hop(x0, x1, -11.3 - tang, 11.3 + tang, Z_DAY + 1.99 + tang * 0.75, Z_DAY + 2.0 + tang * 0.75)])


def _gai_cua(r):
    x = BAY_XC + 16.3
    return Manifold.sphere(r, 24).translate([x, 5.0, Z_DAY + 1.45]) + Manifold.sphere(r, 24).translate([x, -5.0, Z_DAY + 1.45])


def _tab_sau(y):
    return hop_mat(-60, -20, y - 3.5, y + 3.5, -8.6, -LUN_MAT - DAY_MAT + 0.01)


N_VIT_MAT = -5.6        # độ sâu 2 vít giữ mặt tối
Y_VIT_MAT = 9.0


def than_nua(phia):
    """Một nửa thân, tách theo mặt y = 0: mở ra lắp mạch, khép lại bằng 7 vít."""
    # ruột rỗng: trong cán, đầu, và cả đế phình (chừa vách quanh khe quai)
    vach_khe = hop(KHE_X[0] - VACH, KHE_X[1] + VACH, KHE_Y[0] - VACH, KHE_Y[1] + VACH, Z_DAY - 2, Z_DAY + 30)
    t = khoi() + phinh() - (khoi(VACH) + (phinh(VACH) - vach_khe)).trim_by_plane([0, 0, 1], Z_SAN)
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
    t = t + (hop(BAY_XC + BAY_X + 0.5, 60, -12, 12, Z_DAY + 2.0, Z_DAY + 4.2) ^ khoi(0.5))
    # trụ vít nối vào vách gần nhất
    for x, z, r in VIT:
        x0, x1 = (x, 60) if x > 0 else (-60, x)
        y0 = -5.2 if z > 70 else -8    # vít ở lưng đầu nằm dưới tai mặt tối, chỉ dài M2×8
        t = t + ((tru_y(r, y0, Y_VAI, x, z) + hop(x0, x1, y0, Y_VAI, z - r, z + r)) ^ khoi(0.5))
    if phia < 0:
        t = t + (khuyen_tren() - khoi(VACH))
    ma_mui = "P3" if phia > 0 else "P4"
    for ma, R, o in _vi_tri_cong():
        if ma == ma_mui:
            t = t + (_dat(_cong_de(), R, o) ^ khoi(0.5))
    t = nua(t, phia)
    # lỗ lọt pin ở sàn, rãnh trượt nắp pin, hốc khóa nắp
    t = t - hop(BAY_XC - BAY_X, BAY_XC + BAY_X, -BAY_Y, BAY_Y, Z_DAY - 1, Z_SAN + 0.1)
    t = t - _lang_tru_cua(DOOR_X0 - 0.2, 60, 0.2) - hop(DOOR_X0 - 0.2, 60, -10.2, 10.2, Z_DAY - 3, Z_DAY + 0.01) - _gai_cua(1.1)
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
    else:
        for x, z, _ in VIT:
            t = t - tru_y(0.85, -_sau_moi(x, z), 0.1, x, z)
    t = t - khe_duoi()
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
        m = m - cuc(1.4, 16, 1.2, x, 0, Z_DAY)
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
    for y in (-Y_VIT_MAT, Y_VIT_MAT):
        tn = mep_t(y, N_VIT_MAT, -1)
        vit = vit + tru_mat(0.8, tn + 0.5, tn + 7.0, y, N_VIT_MAT) + tru_mat(1.9, tn - 0.9, tn + 0.5, y, N_VIT_MAT)
    tac = None
    for _, R, o in _vi_tri_cong():
        k = _dat(cuc(6, 6, 3.5, 0, 0, -6.95) + Manifold.cylinder(1.25, 1.75, 1.75, 24).translate([0, 0, -5.2]), R, o)
        tac = k if tac is None else tac + k
    return [("quai", "Quai băng gai dính 20 mm", "#2b3136", quai()), ("nut6x6", "Nút bấm 6 × 6", "#26292c", tac), ("esp32", "ESP32 DevKitC 38 chân", "#1d2b33", esp), ("pin9v", "Pin vuông 9V", "#c9862b", pin), ("kep_pin", "Kẹp pin 9V", "#26292c", kep),
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
    long_tay = Manifold.batch_hull([Manifold.sphere(1, 32).scale([17, 17, 34]).translate([-20, -27, 22]),
                                    Manifold.sphere(1, 32).scale([12, 13, 28]).translate([0, -28, 18]),
                                    Manifold.sphere(1, 32).scale([14, 16, 20]).translate([-42, -26, 2])])
    cang = _xuong((-42, -19, 2, 25), (-160, -18, -67, 32))
    m = long_tay + cang
    for z, r in ((28, 8.5), (12, 8.2), (-3.5, 7.5)):
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
        fh.write(b"VERA Band".ljust(80, b" "))
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
        ("tam_mu_tay", "Tấm mu tay có logo", "#18c8e6", tam_mu_tay(), np.eye(3), [0, -30, 0], "dựng đứng trên cạnh dưới, nên có brim"),
        ("nap_pin", "Nắp khoang pin", "#15191d", nap_pin(), np.eye(3), [10, 0, -30], "nằm phẳng, gai khóa hướng lên"),
        ("nut_phu", "Nắp nút phụ (in 4 cái)", "#aab4ba", nut_phu(), np.eye(3), [34, 0, 40], "in 4 cái, mặt gờ xuống bàn"),
    ]


# khối lượng linh kiện (g), chưa cân thật: pin theo số hãng công bố, còn lại ước tính
LINH_KIEN_G = {"quai": 6, "nut6x6": 2, "esp32": 10, "pin9v": 46, "kep_pin": 3, "ha_ap": 3, "cong_tac": 1, "bmi160": 1, "ky023": 10, "hall": 1, "nam_cham": 1, "vit": 6}
# hướng tách rời của linh kiện khi xem 3D
TACH_LK = {"quai": [0, -30, 0], "nut6x6": [22, 0, 26], "esp32": [0, 10, 18], "pin9v": [0, 0, -74], "kep_pin": [0, 0, -18], "ha_ap": [-30, 0, 30], "cong_tac": [24, 0, -18], "bmi160": [0, 10, 26],
           "ky023": list(30 * n_mat), "hall": [18, 34, -4], "nam_cham": [42, 8, -8], "vit": [0, 72, 0]}


# gói gửi in: (file, chi tiết trong chi_tiet_in, tên, số lượng, màu đề xuất)
GOI_IN = [("than", (("than_phai", 1), ("than_trai", 1)), "Thân, 2 nửa", "Trắng"),
          ("day_tren", (("nap", 1),), "Đáy trên (mặt tối bầu dục)", "Đen"),
          ("day_duoi", (("nap_pin", 1),), "Đáy dưới (nắp khoang pin)", "Đen"),
          ("chi_tiet_xanh", (("tam_mu_tay", 1), ("chen", 1)), "Tấm mu tay có logo, chén lục giác", "Xanh ngọc huỳnh quang"),
          ("chi_tiet_bac", (("co", 1), ("nut_phu", 4)), "Cò, 4 nắp nút phụ", "Bạc hoặc xám")]


def xep_hang(khoi_list, khe=8.0):
    """Đặt các chi tiết thành một hàng trên bàn in, cách nhau `khe` mm, rồi căn giữa."""
    m, x = None, 0.0
    for k in khoi_list:
        bb = k.bounding_box()
        k = k.translate([x - bb[0], -(bb[1] + bb[4]) / 2, -bb[2]])
        x += bb[3] - bb[0] + khe
        m = k if m is None else m + k
    return dat_len_ban(m)


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
    goi_in = []
    for file, nguon, ten, mau in GOI_IN:
        m = xep_hang([khoi_in[ma] for ma, so in nguon for _ in range(so)], 10.0 if file == "than" else 8.0)
        ghi_stl(m, os.path.join(THU_MUC, "in", file + ".stl"))
        bb = m.bounding_box()
        kt = [round(bb[3] - bb[0], 1), round(bb[4] - bb[1], 1), round(bb[5] - bb[2], 1)]
        g = sum(gam[ma] for ma, _ in nguon)
        goi_in.append({"file": file, "ten": ten, "mau": mau, "kich_thuoc": kt, "gam_pla": round(g, 1),
                       "chi_tiet": [{"ma": ma, "so": so, "cach_in": cach[ma]} for ma, so in nguon], "so_khoi": sum(so for _, so in nguon)})
        print(f"{file:14s} {kt} mm ≈ {g:5.1f} g  {m.num_tri():6d} tam giác {m.status().name}")
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
    if xem:
        ghi_stl(khoi_chu_tam(SAU_CHU, -0.05), os.path.join(xem, "chu.stl"))
        tt.append({"ma": "chu", "ten": "Logo và chữ khắc (tô màu khi xem)", "mau": "#15191d", "tach": [0, -30, 0], "chi_xem": True})
        ghi_stl(ban_tay(), os.path.join(xem, "ban_tay.stl"))
        with open(os.path.join(xem, "canh.json"), "w", encoding="utf-8") as fh:
            json.dump({"chi_tiet": tt, "goi_in": goi_in, "kich_thuoc": kt, "nhua_g": round(nhua), "tong_g": round(tong), "rong_quai": RONG_QUAI}, fh, ensure_ascii=False, indent=1)
    with open(os.path.join(THU_MUC, "thong_so.json"), "w", encoding="utf-8") as fh:
        json.dump(goi_in, fh, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
