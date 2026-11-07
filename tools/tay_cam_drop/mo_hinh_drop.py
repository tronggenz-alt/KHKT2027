"""Tay cầm VR "VERA Drop": dáng giọt nước, thân 2 nửa bắt vít, một vòng sáng quanh cổ.

Chạy:  python tools/tay_cam_drop/mo_hinh_drop.py [thư mục xem]
Ra:
  docs/tay-cam-drop/in/*.stl        chi tiết in 3D, đã xoay đúng hướng in (đáy ở z = 0)
  docs/tay-cam-drop/thong_so.json   kích thước, khối lượng
  <thư mục xem>/*.stl + canh.json   cả bộ ở tư thế lắp, cho trang xem 3D

Hệ trục (mm): z dọc thân từ đáy lên, x ra trước (phía ngón trỏ), y sang trái. Gốc ở tâm mặt đáy.
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
THU_MUC = os.path.join(GOC, "docs", "tay-cam-drop")
SEG = 96

VACH = 1.8              # bề dày vỏ
# dáng thân: (z, nửa sâu a theo x, nửa rộng b theo y). Eo 43 × 31 mm ≈ Ø37 cho bàn tay dài 177 mm; đầu 53 × 42 ôm mạch joystick
DANG = [(0, 15.5, 11.5), (1, 17.0, 12.7), (3, 19.0, 13.9), (8, 20.8, 15.0), (20, 21.4, 15.6), (38, 21.6, 15.7), (52, 22.6, 16.4),
        (64, 24.6, 18.2), (74, 26.0, 20.3), (82, 26.6, 21.1), (89, 25.8, 20.4), (95, 23.6, 18.4), (99, 20.6, 16.4), (102, 16.0, 13.0), (104, 9.0, 7.0)]
NGHIENG = 10.0          # mặt joystick và vòng sáng ngửa về phía ngón cái
n_mat = np.array([-math.sin(math.radians(NGHIENG)), 0, math.cos(math.radians(NGHIENG))])
t_mat = np.array([math.cos(math.radians(NGHIENG)), 0, math.sin(math.radians(NGHIENG))])
R_MAT = np.column_stack([t_mat, [0, 1, 0], n_mat])
P_VONG = np.array([0, 0, 72.0])         # mặt dưới vòng sáng đi qua điểm này
P_DINH = np.array([0, 0, 98.5])         # mặt đỉnh đi qua điểm này
CAO_VONG = 4.0
TAM_CAN = P_DINH                        # tâm cần joystick trên mặt đỉnh
R_CHEN = 15.0                           # bán kính chén lõm quanh cần
KY_SAU = 20.5                           # tâm mạch KY-023 nằm dưới mặt đỉnh chừng này

# linh kiện (x, y, z theo hệ thân)
ESP = {"x": (-18.0, 10.0), "y": 3.5, "z": (5.5, 60.5)}      # DevKitC 55 × 28, cổng micro-USB quay xuống đáy, chân header cắt ngắn về phía -y
PIN = {"x": (-14.0, 11.0), "y": (-9.4, -0.4), "z": (27.5, 67.5)}     # LiPo 40 × 25 × 9
SAC = {"x": (-13.0, 5.0), "y": -3.5, "z": (2.2, 27.2)}      # TP4056 USB-C đặt đứng, cổng quay xuống đáy
# cò Hall
CHOT = np.array([18.8, 0, 72.5])
Y_CO = 7.8
Z_CO = (48.5, 71.0)
Z_NC, Y_NC = 64.0, 3.0      # tâm nam châm và Hall
X_HALL = 12.0               # mặt đế Hall
X_NC = 18.5                 # mặt nam châm lúc nhả: khe 6,5 mm
GOC_BOP = 15.5              # góc bóp hết: vấu tì vào vách Hall, mép nam châm còn cách Hall ≈ 3,5 mm
# 4 vít M2×10 ghép 2 nửa thân (x, z, bán kính trụ): đầu vít ở nửa trái (+y), ren ăn vào nửa phải (-y)
VIT = [(14.5, 11.0, 3.2), (14.5, 40.0, 3.2), (-21.0, 30.0, 2.4), (-20.6, 59.0, 2.4)]
Y_VAI = 3.5                 # mặt tì đầu vít
Z_VIT_NAP = 65.0            # 2 vít M2×6 giữ nắp, bắt từ lưng thân
# thanh móc dây: ống Ø4 giữa 2 tai ở lưng đáy, lõi là vít M2×16 nên cũng ghép luôn 2 nửa
X_THANH, Z_THANH = -26.5, 4.0
R_THANH, R_TAI = 2.0, 3.2
Y_TAI = (5.5, 9.2)
Y_VAI_THANH = 7.4
# chữ khắc ở sườn phải (-y), đọc từ đáy lên
CHU = "VERA LAB - Le Quy Don"
CO_CHU, EP_CHU = 4.4, 0.9   # cỡ chữ (chữ hoa cao ≈ 3,2 mm), hệ số ép ngang
Z_CHU, X_CHU = 7.0, 0.2     # đầu dòng, đường chân chữ
SAU_CHU = 0.6


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


def cx(z):
    """Tâm thân lệch theo chiều cao: lưng ôm tay ở eo, đầu đổ nhẹ ra trước (uốn chữ S)."""
    t = min(max((z - 50.0) / 34.0, 0.0), 1.0)
    return -1.5 + 4.0 * t * t * (3 - 2 * t)


@functools.lru_cache(maxsize=None)
def khoi(lui=0.0):
    """Khối đặc của thân; lui > 0 cho khối bên trong."""
    tang = []
    for r0, r1 in zip(TRAM, TRAM[1:]):
        a0, b0, a1, b1 = r0[1] - lui, r0[2] - lui, r1[1] - lui, r1[2] - lui
        h = r1[0] - r0[0] + 0.001
        tang.append(Manifold.extrude(elip(a0, b0), h, 0, 0, (a1 / a0, b1 / b0)).translate([0, 0, r0[0]]))
    return Manifold.batch_boolean(tang, OpType.Add).warp(lambda v: (v[0] + cx(v[2]), v[1], v[2]))


def d_mat(p):
    return float(np.dot(n_mat, p))


def o_co(mo=0.0, r=6.0):
    """Ô cò nhìn từ trước: chữ nhật bo góc, mo > 0 cho khe trên thân."""
    y, z0, z1 = Y_CO + mo - r, Z_CO[0] - mo + r, Z_CO[1] + mo - r
    return Manifold.batch_hull([tru_x(r, 12, 40, sy * y, z) for sy in (-1, 1) for z in (z0, z1)])


# ---------------- chi tiết in ----------------

def nua(m, phia):
    """Giữ phần thuộc một nửa: phia = 1 nửa trái (+y), -1 nửa phải (-y)."""
    return m.trim_by_plane([0, float(phia), 0], 0.0)


def chu_khoi(sau_trong, cao_ngoai):
    """Khối chữ uốn theo mặt sườn phải, từ sâu sau_trong dưới mặt tới cao_ngoai trên mặt."""
    duong = TextPath((0, 0), CHU, size=CO_CHU, prop=FontProperties(family="DejaVu Sans", weight="bold"))
    vien = [(np.asarray(q)[:-1] * [EP_CHU, 1.0]).tolist() for q in duong.to_polygons() if len(q) > 3]
    mat = CrossSection(vien, FillRule.EvenOdd)

    def uon(v):
        x, z = X_CHU - v[1], Z_CHU + v[0]
        a, b = ab_tai(z)
        rong = b * math.sqrt(max(1 - ((x - cx(z)) / a) ** 2, 0.0))
        return (x, -(rong - sau_trong + v[2] * (sau_trong + cao_ngoai)), z)

    return Manifold.extrude(mat, 1.0).warp(uon)


def than_nua(phia):
    """Một nửa thân, tách theo mặt y = 0: mở ra lắp mạch, khép lại bằng 4 vít và lõi thanh móc."""
    t = khoi() - khoi(VACH).trim_by_plane([0, 0, 1], VACH)
    # 2 gờ đỡ mép ESP32, chỉ ở phía module để không đè 2 hàng chân header; mạch ép vào gờ bằng băng xốp giữa mạch và pin
    yb = ESP["y"] + 1.6
    t = t + (hop(-26, -15.5, yb, yb + 2.0, 16, 46) ^ khoi(0.5)) + (hop(8.5, 26, yb, yb + 2.0, 16, 47) ^ khoi(0.5))
    # vách đế Hall, hốc TO-92, khe luồn 3 chân, chỗ tựa lò xo
    vach = hop(8, 12, 0, 22, 61, 70.5) ^ khoi(0.5)
    vach = vach - cuc(1.9, 4.4, 3.4, X_HALL - 0.85, Y_NC, Z_NC) - cuc(4, 3.6, 1.6, 9.5, Y_NC, Z_NC)
    vach = vach - tru_x(2.2, X_HALL - 1.5, X_HALL + 1, Y_NC, 68.3)
    t = t + vach
    # tai trục cò hai bên
    for y0, y1 in ((Y_CO + 0.5, Y_CO + 2.5), (-Y_CO - 2.5, -Y_CO - 0.5)):
        t = t + (hop(15.5, 40, y0, y1, 68, 77) ^ khoi(0.5))
    # trụ vít nối vào vách gần nhất
    for x, z, r in VIT:
        x0, x1 = (x, 30) if x > 0 else (-30, x)
        t = t + ((tru_y(r, -8, Y_VAI, x, z) + hop(x0, x1, -8, Y_VAI, z - r, z + r)) ^ khoi(0.5))
    t = nua(t, phia)
    # tai giữ thanh móc dây; bên trong thân vẫn sạch
    y0, y1 = (Y_TAI[0], Y_TAI[1]) if phia > 0 else (-Y_TAI[1], -Y_TAI[0])
    tai = Manifold.batch_hull([tru_y(R_TAI, y0, y1, X_THANH, Z_THANH), hop(-24, -14, y0, y1, 1, 8)])
    t = t + (tai - khoi(VACH).trim_by_plane([0, 0, 1], VACH))
    # khe cò ở mặt trước
    t = t - o_co(0.5)
    t = t - tru_y(1.1, -Y_CO - 3, Y_CO + 3, CHOT[0], CHOT[2])
    # cổng ở đáy: USB-C (sạc), micro-USB (nạp code), công tắc nguồn
    t = t - cuc(9.6, 3.8, 6, -4.0, SAC["y"] - 1.6, 0.8)
    t = t - cuc(8.6, 3.8, 6, -4.0, ESP["y"] + 3.1, 0.8)
    t = t - cuc(2.8, 2.8, 8, 10.2, -3.0, 1.5)
    # 2 lỗ vít M2 giữ nắp, ở lưng thân, có hốc nông cho đầu vít
    for y in (-8, 8):
        t = t - tru_x(1.15, -30, -14, y, Z_VIT_NAP) - tru_x(2.1, -30, -21.5, y, Z_VIT_NAP)
    # lỗ vít ghép: nửa trái có hốc đầu vít và lỗ thoát, nửa phải có lỗ mồi cho vít tự ren
    if phia > 0:
        for x, z, _ in VIT:
            t = t - tru_y(2.1, Y_VAI, 30, x, z) - tru_y(1.1, -0.1, Y_VAI + 0.1, x, z)
        t = t - tru_y(2.1, Y_VAI_THANH, 12, X_THANH, Z_THANH) - tru_y(1.15, Y_TAI[0] - 0.1, Y_VAI_THANH + 0.1, X_THANH, Z_THANH)
        t = t - tru_y(R_THANH + 0.15, Y_TAI[0] - 0.1, Y_TAI[0] + 0.8, X_THANH, Z_THANH)
    else:
        for x, z, _ in VIT:
            t = t - tru_y(0.85, -7, 0.1, x, z)
        t = t - tru_y(0.85, -12, -Y_TAI[0] + 0.1, X_THANH, Z_THANH)
        t = t - tru_y(R_THANH + 0.15, -Y_TAI[0] - 0.8, -Y_TAI[0] + 0.1, X_THANH, Z_THANH)
        t = t - chu_khoi(SAU_CHU, 1.5)
    return t.trim_by_plane(list(-n_mat), -d_mat(P_VONG))


def thanh():
    """Thanh móc dây: ống Ø4 lồng vào 2 tai, lõi vít M2×16 chịu lực."""
    l = Y_TAI[0] + 0.75
    return tru_y(R_THANH, -l, l, X_THANH, Z_THANH) - tru_y(1.15, -l - 1, l + 1, X_THANH, Z_THANH)


def vong():
    """Vòng sáng: in nhựa trong hoặc huỳnh quang."""
    d = d_mat(P_VONG)
    v = khoi() - khoi(VACH)
    return v.trim_by_plane(list(n_mat), d).trim_by_plane(list(-n_mat), -(d + CAO_VONG))


def nap():
    """Nắp đầu: giữ joystick, có gờ lồng xuống thân."""
    d, dd = d_mat(P_VONG), d_mat(P_DINH)
    ng = khoi().trim_by_plane(list(n_mat), d + CAO_VONG).trim_by_plane(list(-n_mat), -dd)
    tr = khoi(VACH).trim_by_plane(list(-n_mat), -(dd - VACH))
    c = ng - tr
    # gờ lồng xuyên qua vòng sáng, xuống thân 3,5 mm (chừa khe 0,25)
    go = (khoi(VACH + 0.25) - khoi(VACH + 1.7)).trim_by_plane(list(n_mat), d - 3.0).trim_by_plane(list(-n_mat), -(d + CAO_VONG + 0.5))
    # vành nối gờ với thành nắp (giữa 2 thứ có khe 0,25 để lọt vòng sáng), nằm ngay dưới mạch KY-023
    noi = (khoi(1.0) - khoi(VACH + 1.7)).trim_by_plane(list(n_mat), d + CAO_VONG).trim_by_plane(list(-n_mat), -(d + CAO_VONG + 0.8))
    go = go + noi - hop(7.5, 40, -30, 30, 50, 90)
    c = c + go
    # 2 tai bắt vít ở lưng
    for y in (-8, 8):
        c = c + (hop(-26, -15.5, y - 3.5, y + 3.5, 62.5, 69.5) ^ khoi(VACH + 0.25))
    for y in (-8, 8):
        c = c - tru_x(0.95, -30, -15, y, Z_VIT_NAP)
    # lỗ cần Ø24, 4 trụ đỡ mạch KY-023
    hoc = Manifold.revolve(CrossSection([[[0, -7.5], [11.8, -7.5], [R_CHEN + 0.2, -1.5], [R_CHEN + 0.2, 2], [0, 2]]]), 96)
    c = c - hoc.transform(bien_doi(R_MAT, TAM_CAN))
    tam_ky = TAM_CAN - KY_SAU * n_mat
    tru = None
    for sx in (-15, 15):
        for sy in (-11, 11):
            k = tru_z(1.7, 0.8, KY_SAU + 1.0, sx, sy, 20).transform(bien_doi(R_MAT, tam_ky))
            tru = k if tru is None else tru + k
    return c + (tru ^ ng)


def chen():
    """Chén lõm tối màu quanh cần joystick: cần nằm gọn trong chén, nhìn từ xa chỉ thấy một vòng tròn."""
    r = R_CHEN
    return Manifold.revolve(CrossSection([[[11.6, -7.5], [r, -1.5], [r, 0], [r - 1.0, 0], [11.6, -5.4]]]), 96).transform(bien_doi(R_MAT, TAM_CAN))


def co():
    """Cò Hall: mảng vỏ đi theo đúng đường cong của thân, quay quanh trục Ø2."""
    c = (khoi() - khoi(2.4)) ^ o_co()
    # gân nối lên trục
    c = c + (hop(CHOT[0] - 2.6, 24.5, -Y_CO, Y_CO, 66, 70.8) ^ khoi()) + hop(CHOT[0] - 2.3, 21.0, -Y_CO, Y_CO, 70.5, CHOT[2])
    c = c + tru_y(2.3, -Y_CO, Y_CO, CHOT[0], CHOT[2])
    c = c - tru_y(1.1, -Y_CO - 1, Y_CO + 1, CHOT[0], CHOT[2])
    # bệ và hốc nam châm Ø6 × 3 (mặt phẳng hướng về Hall)
    a = ab_tai(Z_NC)[0] + cx(Z_NC)
    c = c + tru_x(4.8, X_NC, a - 1, Y_NC, Z_NC)
    # bệ lò xo; vấu chặn tì vào mặt vách Hall ở góc bóp hết
    c = c + tru_x(2.9, 19.5, a, Y_NC, 68.3)
    c = c + hop(X_VAU, 23, 6.3, Y_CO, 61.2, 63.8)
    c = c - tru_x(2.2, 19, 21.5, Y_NC, 68.3)
    c = c - tru_x(3.1, X_NC - 1, X_NC + 3.05, Y_NC, Z_NC)
    # 3 rãnh bám ngón rất nông
    for z in (52, 56, 60):
        an = ab_tai(z)[0] + cx(z)
        c = c - cuc(1.2, 2 * Y_CO + 2, 1.0, an - 0.1, 0, z)
    return c ^ (khoi(2.4) + o_co())


X_VAU = 14.88


def khoi_linh_kien():
    e, p, s = ESP, PIN, SAC
    xc = (e["x"][0] + e["x"][1]) / 2
    zc = (e["z"][0] + e["z"][1]) / 2
    esp = (hop(e["x"][0], e["x"][1], e["y"], e["y"] + 1.6, e["z"][0], e["z"][1])
           + hop(xc - 9, xc + 9, e["y"] + 1.6, e["y"] + 4.8, e["z"][1] - 25.5, e["z"][1])          # module WROOM ở đầu trên
           + hop(xc - 4, xc + 4, e["y"] + 1.6, e["y"] + 4.6, e["z"][0] - 1.0, e["z"][0] + 5)       # cổng micro-USB ở đầu dưới
           + hop(e["x"][0] + 0.2, e["x"][0] + 2.7, e["y"] - 2.5, e["y"], zc - 24, zc + 24)
           + hop(e["x"][1] - 2.7, e["x"][1] - 0.2, e["y"] - 2.5, e["y"], zc - 24, zc + 24))
    pin = hop(p["x"][0], p["x"][1], p["y"][0], p["y"][1], p["z"][0], p["z"][1])
    sac = (hop(s["x"][0], s["x"][1], s["y"], s["y"] + 1.6, s["z"][0], s["z"][1])
           + hop(-8.5, 0.5, s["y"] - 3.2, s["y"], s["z"][0] - 0.2, s["z"][0] + 7.5))
    ct = hop(6.0, 14.4, -4.8, -1.2, 3.2, 6.7) + hop(9.2, 11.2, -4.0, -2.0, 0.5, 3.2)
    bmi = hop(xc - 6.5, xc + 6.5, e["y"] + 4.8, e["y"] + 6.4, zc - 8, zc + 10)
    tam_ky = TAM_CAN - KY_SAU * n_mat
    ky = (cuc(34, 26, 1.6) + cuc(16, 16, 11, 0, 0, 6.3) + tru_z(2, 10, 24, 0, 0, 24) + tru_z(10, 21, 26, 0, 0, 48)).transform(bien_doi(R_MAT, tam_ky))
    hall = cuc(1.5, 4, 3, X_HALL - 0.75, Y_NC, Z_NC) + cuc(6, 2.6, 0.5, X_HALL - 4, Y_NC, Z_NC)
    nc = tru_x(3, X_NC, X_NC + 3, Y_NC, Z_NC)
    vit = None
    for x, z, _ in VIT:
        k = tru_y(0.8, -6.5, Y_VAI, x, z) + tru_y(1.9, Y_VAI, Y_VAI + 1.4, x, z)
        vit = k if vit is None else vit + k
    vit = vit + tru_y(0.8, -8.7, Y_VAI_THANH, X_THANH, Z_THANH) + tru_y(1.9, Y_VAI_THANH, Y_VAI_THANH + 1.4, X_THANH, Z_THANH)
    for y in (-8, 8):
        vit = vit + tru_x(0.8, -21.5, -15.5, y, Z_VIT_NAP) + tru_x(1.9, -22.9, -21.5, y, Z_VIT_NAP)
    return [("esp32", "ESP32 DevKitC 38 chân", "#1d2b33", esp), ("pin", "Pin LiPo 1000 mAh", "#7a8a94", pin), ("tp4056", "Mạch sạc TP4056 USB-C", "#2563a6", sac),
            ("cong_tac", "Công tắc trượt", "#3b4a52", ct), ("bmi160", "BMI160", "#7b4fd6", bmi), ("ky023", "Joystick KY-023", "#26292c", ky),
            ("hall", "Hall SS49E", "#c0392b", hall), ("nam_cham", "Nam châm Ø6×3", "#9aa3a8", nc),
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
    """Tay phải cầm kiểu báng súng: lòng tay ôm lưng và sườn phải (-y), 4 ngón vòng qua trước sang trái."""
    tam_ky = TAM_CAN + 5.5 * n_mat
    long_tay = Manifold.batch_hull([Manifold.sphere(1, 32).scale([17, 11, 36]).translate([-20, -21, 36]),
                                    Manifold.sphere(1, 32).scale([12, 9, 26]).translate([0, -25, 34]),
                                    Manifold.sphere(1, 32).scale([14, 12, 22]).translate([-44, -20, 16])])
    cang = _xuong((-44, -20, 16, 25), (-160, -18, -55, 32))
    m = long_tay + cang
    for z, r, d in ((62, 8.5, 1.0), (45, 8.5, 1.05), (29, 8.0, 1.0), (14, 7.0, 0.9)):
        an, bn = ab_tai(z)
        m = m + _xuong((4, -bn - r + 2, z, r + 0.5), (an * 0.72 + r - 1, -bn * 0.75 - 2, z, r),
                       (an + r - 1, 0, z, r), (an * 0.8, (bn + r - 1) * 0.95, z, r - 0.5), (an * 0.25 * d, bn + r - 1, z, r - 1))
    dinh = tam_ky + 8 * n_mat
    m = m + _xuong((-30, -16, 62, 13), (-28, -12, 86, 10.5), (dinh[0] - 12, dinh[1] - 2, dinh[2] + 1, 9.5), (dinh[0] + 2, dinh[1], dinh[2], 8.5))
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
        fh.write(b"VERA Drop".ljust(80, b" "))
        fh.write(struct.pack("<I", len(f)))
        rec = np.zeros(len(f), dtype=[("n", "<f4", 3), ("v", "<f4", (3, 3)), ("a", "<u2")])
        rec["n"], rec["v"] = n, tri
        fh.write(rec.tobytes())


def dat_len_ban(m):
    bb = m.bounding_box()
    return m.translate([-(bb[0] + bb[3]) / 2, -(bb[1] + bb[4]) / 2, -bb[2]])


def chi_tiet_in():
    """(mã, tên, màu, khối ở tư thế lắp, ma trận về hướng in, hướng tách rời, cách in)."""
    Rv = R_MAT.T
    return [
        ("than_phai", "Thân, nửa phải (có chữ)", "#eef2f4", than_nua(-1), quay("x", -90), [0, -36, 0], "mặt ghép xuống bàn, đỡ bên trong"),
        ("than_trai", "Thân, nửa trái (có hốc vít)", "#eef2f4", than_nua(1), quay("x", 90), [0, 44, 0], "mặt ghép xuống bàn, đỡ bên trong"),
        ("vong", "Vòng sáng", "#18c8e6", vong(), Rv, list(26 * n_mat), "phẳng, nằm trên bàn"),
        ("nap", "Nắp đầu", "#15191d", nap(), quay("x", 180) @ Rv, list(52 * n_mat), "mặt đỉnh xuống bàn"),
        ("chen", "Chén lõm joystick", "#3a444b", chen(), Rv, list(76 * n_mat), "mặt lớn xuống bàn"),
        ("co", "Cò Hall", "#1b2329", co(), quay("x", 90), [46, 0, -6], "nằm nghiêng, mặt bên xuống bàn"),
        ("thanh", "Thanh móc dây", "#1b2329", thanh(), quay("x", 90), [-34, 0, -8], "dựng đứng"),
    ]


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    xem = sys.argv[1] if len(sys.argv) > 1 else None
    os.makedirs(os.path.join(THU_MUC, "in"), exist_ok=True)
    tt = []
    for ma, ten, mau, m, R, tach, cach in chi_tiet_in():
        in_m = dat_len_ban(m.transform(bien_doi(R, [0, 0, 0])))
        ghi_stl(in_m, os.path.join(THU_MUC, "in", ma + ".stl"))
        bb = in_m.bounding_box()
        kt = [round(bb[3] - bb[0], 1), round(bb[4] - bb[1], 1), round(bb[5] - bb[2], 1)]
        v = in_m.volume() / 1000
        tt.append({"ma": ma, "ten": ten, "mau": mau, "tach": [round(float(x), 1) for x in tach], "cach_in": cach, "kich_thuoc": kt,
                   "cm3": round(v, 1), "gam_pla": round(v * 1.24 * 0.92, 1), "loi": in_m.status().name})
        if xem:
            ghi_stl(m, os.path.join(xem, ma + ".stl"))
        print(f"{ma:6s} {kt} mm {v:6.1f} cm3 ≈ {v * 1.24 * 0.92:5.1f} g  {in_m.num_tri():6d} tam giác {in_m.status().name}")
    tach_lk = {"esp32": [0, 14, 0], "pin": [0, -16, 0], "tp4056": [0, -8, 0], "cong_tac": [0, -8, 0], "bmi160": [0, 24, 0],
               "ky023": list(36 * n_mat), "hall": [0, 44, 0], "nam_cham": [36, 8, -6], "vit": [0, 66, 0]}
    if xem:
        for ma, ten, mau, m in khoi_linh_kien():
            ghi_stl(m, os.path.join(xem, ma + ".stl"))
            tt.append({"ma": ma, "ten": ten, "mau": mau, "tach": [round(float(x), 1) for x in tach_lk[ma]], "linh_kien": True})
        ghi_stl(chu_khoi(SAU_CHU, -0.05), os.path.join(xem, "chu.stl"))
        tt.append({"ma": "chu", "ten": "Chữ khắc (tô màu khi xem)", "mau": "#15191d", "tach": [0, -36, 0], "chi_xem": True})
        ghi_stl(ban_tay(), os.path.join(xem, "ban_tay.stl"))
        with open(os.path.join(xem, "canh.json"), "w", encoding="utf-8") as fh:
            json.dump({"chi_tiet": tt}, fh, ensure_ascii=False, indent=1)
    with open(os.path.join(THU_MUC, "thong_so.json"), "w", encoding="utf-8") as fh:
        json.dump([t for t in tt if not t.get("linh_kien") and not t.get("chi_xem")], fh, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
