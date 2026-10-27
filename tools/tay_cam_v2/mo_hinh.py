"""Tay cầm VR V2: ESP32 38 chân, BMI160, joystick KY-023, cò analog bằng cảm biến Hall tuyến tính, vòng halo cho OpenCV.

Chạy:  python tools/tay_cam_v2/mo_hinh.py [thư mục xem]
Ra:
  docs/tay-cam-v2/in/*.stl          chi tiết in 3D, đã xoay đúng hướng in (đáy ở z = 0)
  docs/tay-cam-v2/hinh_chieu.json   đường bao các hình chiếu để vẽ bản 2D
  <thư mục xem>/*.stl + canh.json   cả bộ ở tư thế lắp, cho trang xem 3D

Hệ trục tay cầm (mm): X chĩa ra trước, Y sang trái, Z lên trên. Gốc ở đỉnh cán cầm.
Cần: pip install manifold3d numpy
"""
import json
import math
import os
import struct
import sys

import numpy as np
from manifold3d import CrossSection, Manifold

GOC = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
THU_MUC = os.path.join(GOC, "docs", "tay-cam-v2")
SEG = 64

VACH = 2.2                      # bề dày vỏ
NGHIENG_CAN = 20                # cán cầm ngả ra sau 20° so với phương thẳng đứng
NGHIENG_MAT = 15                # mặt joystick ngửa về phía ngón cái 15°
d = np.array([-math.sin(math.radians(NGHIENG_CAN)), 0, -math.cos(math.radians(NGHIENG_CAN))])   # trục cán, hướng xuống
e_a = np.array([math.cos(math.radians(NGHIENG_CAN)), 0, -math.sin(math.radians(NGHIENG_CAN))])   # hướng ra trước, vuông góc trục cán
R_CAN = np.column_stack([e_a, [0, -1, 0], d])      # khung cán: x -> e_a, z -> d

# mặt cắt cán cầm theo chiều dài s: (s, nửa sâu a, nửa rộng b). Giữa cán 37 × 33 (tối ưu ≈ Ø35 cho tay 177 mm)
TRAM_CAN = [(-6, 19.0, 17.0), (15, 19.5, 17.2), (45, 18.5, 16.5), (75, 17.8, 16.0), (100, 17.6, 15.9), (112, 18.4, 16.6), (117, 17.0, 15.4)]
S_DAY = 112                     # mặt cắt đáy cán (lắp nắp đáy)
DAU = {"tam": (14, 0, 14), "ban_truc": (36, 21, 15.5)}
MUI = {"tam": (42, 0, 11), "ban_truc": (11, 12, 9.5)}  # mũi thuôn, đầu mũi ở x = 53, lọt vào giữa vòng halo
MAT = {"diem": (10, 0, 25.0)}
n_mat = np.array([-math.sin(math.radians(NGHIENG_MAT)), 0, math.cos(math.radians(NGHIENG_MAT))])
t_mat = np.array([math.cos(math.radians(NGHIENG_MAT)), 0, math.sin(math.radians(NGHIENG_MAT))])
TAM_CAN_JOY = np.array(MAT["diem"]) - 6 * t_mat           # tâm cần joystick trên mặt
CHOT_CO = np.array([27.0, 0, 4.0])                        # trục quay cò
GOC_CHAN_CO = 11.0              # góc bóp hết (vấu chạm vách Hall)
TAM_OP = (40, 27, 1.6)          # tấm ốp mặt joystick (dài × rộng × dày)
HALO = {"x0": 53, "x1": 58, "R": 35, "r": 29, "tam_z": 11, "x_tai": 26}   # vòng Ø70, bản 6 mm; 2 tay đỡ bắt vào hai bên đầu ở x = 26

LINH_KIEN = {
    "esp32": (55, 28, 1.6), "bmi160": (13, 18, 1.6), "ky023": (34, 26, 1.6),
    "hall": (4.0, 3.0, 1.5), "nam_cham": (6, 3), "pin": (40, 25, 9), "tp4056": (25, 18, 5),
}
S_ESP = (10, 65)                # ESP32 dọc cán từ s = 10 tới 65
S_PIN = (65.6, 105.6)           # pin LiPo 40 × 25 × 9 (902540, ≈ 1000 mAh), cách ESP32 0,6 mm
S_SAC = 106.3                   # mặt trên mạch sạc TP4056 (dày 5), cách pin 0,7 mm, cổng USB-C chạm nắp đáy


# ---------------- tiện ích hình học ----------------

def tam_op():
    """Tâm tấm ốp: nằm trên mặt joystick, lệch về trước 3 mm so với tâm cần."""
    return TAM_CAN_JOY + 3 * t_mat - 0.8 * n_mat


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


def elipsoit(tam, ban_truc, R=None, n=SEG):
    m = Manifold.sphere(1, n).scale(list(ban_truc))
    if R is not None:
        m = m.transform(bien_doi(R, [0, 0, 0]))
    return m.translate(list(tam))


def cuc(sx, sy, sz, x=0, y=0, z=0):
    return Manifold.cube([sx, sy, sz], True).translate([x, y, z])


def tru_y(r, y0, y1, x, z, n=40):
    """Trụ nằm dọc trục Y."""
    return Manifold.cylinder(y1 - y0, r, r, n).rotate([-90, 0, 0]).translate([x, y0, z])


def tru_x(r, x0, x1, y, z, n=48):
    return Manifold.cylinder(x1 - x0, r, r, n).rotate([0, 90, 0]).translate([x0, y, z])


def theo_can(m, s0=0.0, du=0.0, dv=0.0):
    """Đặt khối dựng trong khung cán (z = dọc cán) vào vị trí s0 trên cán."""
    return m.transform(bien_doi(R_CAN, s0 * d + du * e_a + np.array([0, dv, 0])))


def elip_cs(a, b, n=SEG):
    return CrossSection([[[a * math.cos(2 * math.pi * i / n), b * math.sin(2 * math.pi * i / n)] for i in range(n)]])


def ab_tai(s):
    for (s0, a0, b0), (s1, a1, b1) in zip(TRAM_CAN, TRAM_CAN[1:]):
        if s0 <= s <= s1:
            k = (s - s0) / (s1 - s0)
            return a0 + k * (a1 - a0), b0 + k * (b1 - b0)
    return TRAM_CAN[-1][1:]


# ---------------- vỏ ----------------

def khoi_ngoai(lui=0.0):
    """Khối đặc bên ngoài; lui > 0 cho khối bên trong (dùng để khoét rỗng)."""
    dia = []
    for s, a, b in TRAM_CAN:
        dia.append(elipsoit(s * d, (a - lui, b - lui, 3.0), R_CAN))
    can = Manifold.batch_hull(dia)
    dau = elipsoit(DAU["tam"], np.array(DAU["ban_truc"]) - lui)
    mui = elipsoit(MUI["tam"], np.array(MUI["ban_truc"]) - lui)
    co = Manifold.batch_hull([dau, mui] + dia[:2])
    khoi = co + can
    # mặt joystick phẳng ngửa 15°; khối trong lùi vào đúng bề dày vỏ
    khoi = khoi.trim_by_plane(list(-n_mat), -(float(np.dot(n_mat, MAT["diem"])) - lui))
    # đáy cán phẳng; khối trong xuyên qua đáy để đáy để hở (nắp đáy đậy lại)
    khoi = khoi.trim_by_plane(list(-d), -(S_DAY + (5 if lui else 0)))
    return khoi


def vo_nguyen():
    ngoai = khoi_ngoai()
    trong = khoi_ngoai(VACH)
    vo = ngoai - trong
    # hốc lõm ở đáy cán để nắp đáy ôm vào (sâu 2 mm, dài 10 mm)
    a, b = ab_tai(S_DAY - 5)
    cat = Manifold.extrude(elip_cs(80, 80) - elip_cs(a - 2.0, b - 2.0), 11)
    vo = vo - theo_can(cat, S_DAY - 10)
    # lỗ cần joystick Ø24 trên mặt
    lo = Manifold.cylinder(30, 12, 12, 64).transform(bien_doi(np.column_stack([t_mat, [0, 1, 0], n_mat]), TAM_CAN_JOY - 15 * n_mat))
    vo = vo - lo
    # khe cho cò ở mặt trước dưới đầu
    vo = vo - cuc(19, 13, 38, 27.5, 0, -9)
    # vách đỡ cảm biến Hall (ngang cổ, trước mạch ESP32), có hốc giữ TO-92; cũng là chặn hành trình cò
    vach = cuc(4, 36, 12, 15, 0, -9) ^ ngoai
    vo = vo + (vach - cuc(1.9, 4.4, 3.4, 16.2, 0, -10) - cuc(3.6, 3.6, 1.6, 13.5, 0, -10))     # hốc TO-92 + khe luồn 3 chân
    # trụ chốt cò hai bên (lỗ Ø3,1 cho vít M3)
    for y0, y1 in ((6.6, 20), (-20, -6.6)):
        vo = vo + (tru_y(4.2, y0, y1, CHOT_CO[0], CHOT_CO[2]) ^ ngoai)
    vo = vo - tru_y(1.55, -30, 30, CHOT_CO[0], CHOT_CO[2])
    # gờ đỡ mạch KY-023 (song song mặt joystick, 13 mm dưới mặt)
    tam_ky = TAM_CAN_JOY - 13.0 * n_mat
    Rm = np.column_stack([t_mat, [0, 1, 0], n_mat])
    for y in (-15.5, 15.5):
        go = Manifold.cube([30, 9, 3], True).transform(bien_doi(Rm, tam_ky + np.array([0, y, 0]) - 2.3 * n_mat))
        vo = vo + (go ^ ngoai)
    # rãnh kẹp mạch ESP32 (hai mép dọc cán)
    for du in (-15.4, 15.4):
        ray = theo_can(Manifold.cube([4.4, 7, S_ESP[1] - S_ESP[0]], True).translate([0, 0, (S_ESP[1] - S_ESP[0]) / 2]), S_ESP[0], du)
        ray = (ray ^ ngoai) - theo_can(Manifold.cube([2.4, 2.0, 80], True).translate([0, 0, 40]), S_ESP[0] - 5, du - 1.2 * np.sign(du))   # rãnh 13,0–15,4: mép mạch (±14) ăn sâu 1 mm
        vo = vo + ray
    # trụ vít ghép hai nửa (đầu sau, đầu trước) + trụ bắt halo ở mũi
    tru_vit = [(-13, 1), (36, 18)]
    for x, z in tru_vit:
        vo = vo + (tru_y(3.6, -25, 25, x, z) ^ ngoai)
    # trụ trong thành hai bên đầu để bắt tay đỡ halo (vít M3 tự ren, lỗ Ø2,5)
    for y0, y1 in ((7, 22), (-22, -7)):
        vo = vo + (tru_y(3.6, y0, y1, HALO["x_tai"], HALO["tam_z"]) ^ ngoai)
    vo = vo - tru_y(1.25, -30, 30, HALO["x_tai"], HALO["tam_z"])
    # hốc chìm 1,6 mm trên mặt joystick cho tấm ốp tối màu
    vo = vo - Manifold.cube([TAM_OP[0] + 0.4, TAM_OP[1] + 0.4, 3.2], True).transform(bien_doi(np.column_stack([t_mat, [0, 1, 0], n_mat]), tam_op()))
    # rãnh dọc lưng cán cho dải màu huỳnh quang (in rời, dán vào): rộng 4, sâu 1,2
    vung = theo_can(Manifold.cube([17, 4, 84]).translate([-30, -2, 0]), 12)
    dai = (vung ^ ngoai) - khoi_ngoai(1.2)
    vo = vo - dai
    return vo, tru_vit, dai


def tach_doi(vo, tru_vit):
    trai = vo.trim_by_plane([0, 1, 0], 0.0)
    phai = vo.trim_by_plane([0, -1, 0], 0.0)
    for x, z in tru_vit:
        trai = trai - tru_y(0.95, 0, 30, x, z)                               # lỗ mồi vít M2 tự ren
        phai = phai - tru_y(1.15, -30, 0, x, z) - tru_y(2.3, -30, -9, x, z)  # lỗ xuyên + lỗ chìm đầu vít
    return trai, phai


# ---------------- các chi tiết rời ----------------

def co():
    """Cò: lá dày 12, quay quanh chốt M3, nam châm Ø6×3 ở mặt sau đối diện cảm biến Hall."""
    bien = [(CHOT_CO[0] - 5, CHOT_CO[2] + 4), (CHOT_CO[0] + 5, CHOT_CO[2] + 4), (35.5, -6), (34.5, -18), (30, -27),
            (25.5, -25), (27, -17), (22.5, -8), (21, 0)]
    dt = sum(x0 * z1 - x1 * z0 for (x0, z0), (x1, z1) in zip(bien, bien[1:] + bien[:1]))
    if dt < 0:
        bien = bien[::-1]
    cs = CrossSection([[[x, z] for x, z in bien]])
    la = Manifold.extrude(cs, 12).translate([0, 0, -6]).transform(bien_doi(np.array([[1, 0, 0], [0, 0, -1], [0, 1, 0]]), [0, 0, 0]))
    la = la + tru_y(5.2, -6, 6, CHOT_CO[0], CHOT_CO[2])
    la = la - tru_y(1.65, -10, 10, CHOT_CO[0], CHOT_CO[2])
    # hốc nam châm ở mặt sau (hướng -X), đối diện cảm biến (mặt ở x = 17,15): khe 6,5 mm lúc nhả, ≈ 3,4 mm lúc bóp hết
    la = la - tru_x(3.1, 20.0, 26.6, 0, -10)
    # chừa khe ≥ 0,4 mm với vách Hall suốt hành trình 0–11°: khoét phần cò quét qua vách (vách phồng thêm 0,6)
    vach = cuc(5.2, 37, 21.6, 15, 0, -13.2)        # vách Hall + thành cổ ngay dưới vách
    for g in np.arange(0.0, GOC_CHAN_CO + 0.01, 0.25):
        R = quay("y", -g)
        la = la - vach.transform(bien_doi(R, CHOT_CO - R @ CHOT_CO))
    # vấu chặn chạm vách Hall khi bóp hết (góc 11°, khe nam châm còn 3,3 mm); hốc lò xo nén Ø4 × 8
    la = la + cuc(3.01, 4, 2.5, 22.01, 0, -13.75)
    la = la - tru_x(2.2, 20.0, 24.5, 0, -4.5)
    # rãnh bám ngón
    for z in (-12, -16, -20):
        la = la - cuc(2, 14, 1.2, 35.0 + (z + 12) * 0.12, 0, z)
    return la


def halo():
    """Vòng halo Ø70 bản 6 mm ôm quanh mũi, 2 tay đỡ thuôn bắt vít vào hai bên đầu.
    Mặt trước phẳng để dán băng màu huỳnh quang / dải LED cho OpenCV."""
    x0, x1, R, r, zc, xt = HALO["x0"], HALO["x1"], HALO["R"], HALO["r"], HALO["tam_z"], HALO["x_tai"]
    bo = 1.6
    mc = CrossSection([[[r + bo, 0], [R - bo, 0], [R, bo], [R, x1 - x0], [r, x1 - x0], [r, bo]]])     # vát mép sau
    vong = Manifold.revolve(mc, 160).transform(np.array([[0, 0, 1, x0], [1, 0, 0, 0], [0, 1, 0, zc]], float))
    h = vong
    ngoai = khoi_ngoai()
    for sy in (-1, 1):
        cau = [((x0 + x1) / 2, sy * (r + 3), zc, 2.9), (44, sy * 27.5, zc, 3.2), (xt + 2, sy * 21.5, zc, 4.2)]
        tay = None
        for (a, b) in zip(cau, cau[1:]):
            seg = Manifold.batch_hull([Manifold.sphere(a[3], 32).translate(list(a[:3])), Manifold.sphere(b[3], 32).translate(list(b[:3]))])
            tay = seg if tay is None else tay + seg
        de = tru_y(6.0, sy * 17 if sy > 0 else -24, 24 if sy > 0 else -17, xt, zc)
        h = h + ((tay + de) - ngoai)
        # lỗ vít M3 xuyên + lỗ chìm đầu vít
        h = h - tru_y(1.7, -40, 40, xt, zc)
        h = h - (tru_y(3.0, 22.5, 40, xt, zc) if sy > 0 else tru_y(3.0, -40, -22.5, xt, zc))
    return h


def tam_op_mat():
    """Tấm ốp mặt joystick (in màu tối), lỗ cần Ø23, rãnh trang trí quanh lỗ."""
    L, W, T = TAM_OP
    cs = CrossSection([chu_nhat_bo(L, W, 8)])
    m = Manifold.extrude(cs, T).translate([0, 0, -T / 2])
    lech = float(np.dot(TAM_CAN_JOY - tam_op(), t_mat))
    m = m - Manifold.cylinder(4, 11.5, 11.5, 64).translate([lech, 0, -2])
    m = m - (Manifold.cylinder(1, 14.6, 14.6, 64).translate([lech, 0, T / 2 - 0.5]) - Manifold.cylinder(2, 13.8, 13.8, 64).translate([lech, 0, T / 2 - 1]))
    return m.transform(bien_doi(np.column_stack([t_mat, [0, 1, 0], n_mat]), tam_op()))


def chu_nhat_bo(w, h, r, n=10):
    pts = []
    for cx, cy, a0 in ((w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270)):
        for i in range(n + 1):
            t = math.radians(a0 + 90 * i / n)
            pts.append([cx + r * math.cos(t), cy + r * math.sin(t)])
    return pts


def nap_day():
    """Nắp đáy dạng cổ áo ôm đáy cán: giữ hai nửa vỏ, có cửa USB-C của mạch sạc và lỗ dây đeo cổ tay."""
    a, b = ab_tai(S_DAY - 5)
    ong = Manifold.extrude(elip_cs(a + 0.3, b + 0.3) - elip_cs(a - 2.0 + 0.25, b - 2.0 + 0.25), 10)
    day = Manifold.extrude(elip_cs(a + 0.3, b + 0.3), 2.5).translate([0, 0, 10])
    n = ong + day
    n = n - cuc(10, 4, 6, 0, 0, 11)                         # cửa USB-C 9,5 × 3,6
    n = n - cuc(2.2, 5, 6, -(a - 4), 0, 11)                # khe công tắc trượt
    tai = cuc(8, 10, 6, -a - 2, 0, 9.5) - cuc(3, 12, 3, -a - 2.5, 0, 10)
    n = n + tai
    return n                                              # dựng trong khung cán, z = 0 tại s = S_DAY - 10


# ---------------- linh kiện (để xem và vẽ) ----------------

def khoi_linh_kien():
    lk = []
    L, W, T = LINH_KIEN["esp32"]
    esp = theo_can(Manifold.cube([W, 1.6, L], True).translate([0, 0, L / 2]) + Manifold.cube([18, 3.2, 25.5], True).translate([0, -2.4, 13]), S_ESP[0])
    lk.append(("esp32", "ESP32 DevKit 38 chân", "#1d2b33", esp))
    pin = theo_can(Manifold.cube([25, 9, 40], True).translate([0, 0, 20]), S_PIN[0])
    lk.append(("pin", "Pin LiPo 1000 mAh (tính sau)", "#7a8a94", pin))
    sac = theo_can(Manifold.cube([25, 18, 5], True).translate([0, 0, 2.5]), S_SAC)
    lk.append(("tp4056", "Mạch sạc TP4056 USB-C", "#2563a6", sac))
    tam_ky = TAM_CAN_JOY - 13.0 * n_mat
    Rm = np.column_stack([t_mat, [0, 1, 0], n_mat])
    ky = (Manifold.cube([34, 26, 1.6], True) + Manifold.cube([16, 16, 11], True).translate([0, 0, 6.3])
          + Manifold.cylinder(14, 2, 2, 24).translate([0, 0, 10]) + Manifold.cylinder(5, 10, 10, 48).translate([0, 0, 21]))
    ky = ky.transform(bien_doi(Rm, tam_ky))
    lk.append(("ky023", "Joystick KY-023", "#26292c", ky))
    lk.append(("bmi160", "BMI160 (dán trên vỏ module ESP32)", "#7b4fd6", theo_can(Manifold.cube([13, 1.6, 18], True).translate([0, -4.8, 9]), S_ESP[0] + 6)))
    lk.append(("hall", "Hall SS49E", "#c0392b", cuc(1.5, 4, 3, 16.4, 0, -10) + cuc(6, 0.5, 0.5, 12.9, 0, -10)))
    lk.append(("nam_cham", "Nam châm Ø6×3", "#9aa3a8", tru_x(3, 23.6, 26.6, 0, -10)))
    return lk


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
        fh.write(b"KHKT tay cam VR V2".ljust(80, b" "))
        fh.write(struct.pack("<I", len(f)))
        rec = np.zeros(len(f), dtype=[("n", "<f4", 3), ("v", "<f4", (3, 3)), ("a", "<u2")])
        rec["n"], rec["v"] = n, tri
        fh.write(rec.tobytes())


def dat_len_ban(m):
    bb = m.bounding_box()
    return m.translate([-(bb[0] + bb[3]) / 2, -(bb[1] + bb[4]) / 2, -bb[2]])


def duong_bao(m, nhin):
    """Đường bao hình chiếu: nhin = 'truoc' (theo -X), 'canh' (theo +Y, X sang phải), 'tren' (theo -Z)."""
    if nhin == "canh":      # nhìn từ phía phải: trục ngang = X, trục đứng = Z
        mm = m.transform(bien_doi(np.array([[1, 0, 0], [0, 0, 1], [0, -1, 0]], float), [0, 0, 0]))
    elif nhin == "truoc":   # nhìn từ phía trước: ngang = -Y (trái người xem là phải tay cầm), đứng = Z
        mm = m.transform(bien_doi(np.array([[0, -1, 0], [0, 0, 1], [-1, 0, 0]], float), [0, 0, 0]))
    else:                   # nhìn từ trên: ngang = X, đứng = Y
        mm = m
    return [[[round(x, 2), round(y, 2)] for x, y in poly] for poly in mm.project().to_polygons()]


def mat_cat(m, y=0.0):
    """Mặt cắt tại mặt phẳng y: ngang = X, đứng = Z."""
    mm = m.transform(bien_doi(np.array([[1, 0, 0], [0, 0, 1], [0, -1, 0]], float), [0, 0, 0]))
    return [[[round(a, 2), round(b, 2)] for a, b in poly] for poly in mm.slice(-y).to_polygons()]


def main():
    thu_muc_xem = sys.argv[1] if len(sys.argv) > 1 else None
    os.makedirs(os.path.join(THU_MUC, "in"), exist_ok=True)
    vo, tru_vit, dai = vo_nguyen()
    trai, phai = tach_doi(vo, tru_vit)
    c = co()
    h = halo()
    op = tam_op_mat()
    nd = theo_can(nap_day(), S_DAY - 10)

    chi_tiet = [  # mã, tên, màu, khối ở tư thế lắp, ma trận xoay về hướng in, tách rời, cách in
        ("vo_trai", "Vỏ nửa trái", "#eef2f4", trai, quay("x", 90), [0, 34, 0], "mặt ghép úp xuống bàn"),
        ("vo_phai", "Vỏ nửa phải", "#eef2f4", phai, quay("x", -90), [0, -34, 0], "mặt ghép úp xuống bàn"),
        ("co", "Cò Hall", "#1c2329", c, quay("x", 90), [26, 0, -18], "nằm nghiêng, mặt bên xuống bàn"),
        ("halo", "Vòng halo", "#18c8e6", h, quay("y", 90), [44, 0, 0], "mặt trước xuống bàn, 2 tay đỡ dựng lên"),
        ("tam_op", "Tấm ốp mặt joystick", "#1c2329", op, None, list(26 * n_mat), "mặt phẳng xuống bàn"),
        ("nap_day", "Nắp đáy USB-C", "#1c2329", nd, None, list(28 * d), "đáy xuống bàn"),
        ("dai_sang", "Dải màu lưng cán", "#18c8e6", dai, np.vstack([[0, 1, 0], d, -e_a]), list(-26 * e_a), "nằm, mặt cong lên"),
    ]
    thong_tin = []
    for ma, ten, mau, m, R, tach, cach in chi_tiet:
        if ma == "nap_day":
            in_m = dat_len_ban(nap_day().mirror([0, 0, 1]))
        elif ma == "tam_op":
            in_m = dat_len_ban(op.transform(bien_doi(np.column_stack([t_mat, [0, 1, 0], n_mat]).T, -np.column_stack([t_mat, [0, 1, 0], n_mat]).T @ tam_op())))
        else:
            in_m = dat_len_ban(m.transform(bien_doi(R, [0, 0, 0])))
        ghi_stl(in_m, os.path.join(THU_MUC, "in", ma + ".stl"))
        bb = in_m.bounding_box()
        kt = [round(bb[3] - bb[0], 1), round(bb[4] - bb[1], 1), round(bb[5] - bb[2], 1)]
        tt = in_m.volume() / 1000
        thong_tin.append({"ma": ma, "ten": ten, "mau": mau, "tach": [round(v, 1) for v in tach], "cach_in": cach,
                          "kich_thuoc": kt, "cm3": round(tt, 1), "gam_pla": round(tt * 1.24), "loi": in_m.status().name})
        if thu_muc_xem:
            ghi_stl(m, os.path.join(thu_muc_xem, ma + ".stl"))
        print(f"{ma:9s} {kt} mm {tt:6.1f} cm3 {in_m.num_tri():6d} tam giác {in_m.status().name}")

    lk = khoi_linh_kien()
    tach_lk = {"esp32": list(-30 * e_a), "pin": list(-30 * e_a + 18 * d), "tp4056": list(40 * d), "ky023": list(30 * n_mat),
               "bmi160": [0, -26, 0], "hall": [-14, 0, -8], "nam_cham": [34, 0, -18]}
    if thu_muc_xem:
        for ma, ten, mau, m in lk:
            ghi_stl(m, os.path.join(thu_muc_xem, ma + ".stl"))
            thong_tin.append({"ma": ma, "ten": ten, "mau": mau, "tach": [round(v, 1) for v in tach_lk[ma]], "linh_kien": True})
        with open(os.path.join(thu_muc_xem, "canh.json"), "w", encoding="utf-8") as fh:
            json.dump({"chi_tiet": thong_tin}, fh, ensure_ascii=False, indent=1)

    # dữ liệu hình chiếu cho bản vẽ 2D
    lap = trai + phai + c + h + nd + op + dai
    hc = {"tong": {v: duong_bao(lap, v) for v in ("canh", "truoc", "tren")},
          "phan": {ma: {v: duong_bao(m, v) for v in ("canh", "truoc", "tren")} for ma, _, _, m, _, _, _ in chi_tiet},
          "cat_y0": {"vo": mat_cat(trai + phai, 0.01), "co": mat_cat(c, 0.01), "halo": mat_cat(h, 0.01), "nap_day": mat_cat(nd, 0.01),
                     "tam_op": mat_cat(op, 0.01), "dai_sang": mat_cat(dai, 0.01)},
          "linh_kien_canh": {ma: duong_bao(m, "canh") for ma, _, _, m in lk},
          "bb": [round(v, 1) for v in lap.bounding_box()],
          "thong_so": thong_tin[:7],
          "tham_so": {"nghieng_can": NGHIENG_CAN, "nghieng_mat": NGHIENG_MAT, "tram_can": TRAM_CAN,
                      "chot_co": CHOT_CO.tolist(), "halo": HALO, "tam_can_joy": TAM_CAN_JOY.tolist(), "s_day": S_DAY}}
    with open(os.path.join(THU_MUC, "hinh_chieu.json"), "w", encoding="utf-8") as fh:
        json.dump(hc, fh, ensure_ascii=False)
    print("bao ngoài:", hc["bb"])


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
