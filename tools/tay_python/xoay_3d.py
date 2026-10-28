"""Xoay mô hình 3D người bằng tay qua camera.

- Chụm ngón cái + ngón trỏ rồi kéo: xoay mô hình (kéo ngang = quay quanh trục dọc, kéo dọc = nghiêng).
- Kéo nhanh rồi thả: mô hình quay tiếp và chậm dần, như lướt quả địa cầu. Chụm lại lúc đang quay: dừng ngay.
- Chụm cả hai tay rồi kéo giãn / kéo lại gần: phóng to / thu nhỏ.
- Xoè cả hai bàn tay giữ 1 giây (hoặc phím r): về góc nhìn ban đầu.

Chạy:  source ~/hand3d-env/bin/activate && python ~/Downloads/hand3d/xoay_3d.py
Phím:  q / Esc = thoát · r = về góc đầu · v = ẩn/hiện hình camera
Nhận diện tay và chọn camera (không mở camera iPhone) dùng chung với xem_tay.py.
Vẽ 3D bằng numpy + OpenCV nên không phải cài thêm gì.
"""
import math
import sys
import time
from collections import deque

import cv2
import numpy as np

from xem_tay import (CAM, CHAM_BAT, CHAM_NHA, DEN, TRANG, XANH, chu, nhan_dien, tao_may_nhan_dien, ti_le_chum,
                     tim_camera, ve_tay)

RONG, CAO = 1280, 720                      # cửa sổ
TIEU_CU = (CAO / 2) / math.tan(math.radians(35) / 2)
KC_GOC = 3.9                               # khoảng cách từ mắt nhìn tới mô hình khi zoom = 1
TAM_Y = CAO / 2 - 20                       # đẩy hình lên một chút để thấy trọn bệ xoay
ZOOM_MIN, ZOOM_MAX = 0.45, 3.0
PITCH_MAX = math.radians(75)
GOC_DAU = (math.radians(-25), math.radians(8))   # quay nhẹ 3/4 và nhìn hơi từ trên xuống

DO_NHAY = 3.5             # rad xoay khi tay đi được một "chiều cao khung camera"
NGUONG_TRUOT = 0.008      # chụm rồi phải kéo quá đoạn này mới bắt đầu xoay (tránh rung khi vừa chụm; 0,015 → 0,008 cho đỡ trễ)
QT_NGUONG = 0.6           # thả tay khi đang quay nhanh hơn mức này (rad/s) thì lướt tiếp
QT_TAT = 0.6              # quán tính giảm còn 37 % sau mỗi chừng này giây
QT_MAX = 8.0
MAT_TAY_S = 0.15          # nhận diện hụt ngắn hơn mức này thì coi như tay vẫn còn
XOE_GIU_S = 1.0
VUNG = ((0.15, 0.85), (0.12, 0.82))   # vùng tay hay hoạt động trong khung camera → trải ra toàn cửa sổ

DA, AO, QUAN, GIAY, MAT_NGUOI = (165, 195, 235), (170, 120, 40), (80, 65, 55), (45, 42, 40), (40, 30, 30)
HUONG_SANG = np.array([-0.45, 0.6, 0.65]) / np.linalg.norm([-0.45, 0.6, 0.65])  # đèn trên-trái-trước, cố định theo mắt nhìn
XANH_DUONG = (230, 190, 60)


def kep(x, a, b):
    return a if x < a else b if x > b else x


# ============================================================================ nhận diện tay → con trỏ
class LocOneEuro:
    """Lọc rung: tay đứng yên thì lọc mạnh (hết rung), tay đi nhanh thì lọc nhẹ (không bị trễ)."""

    # 26/10/2026: (0,8 / 5 / 1) → (1 / 15 / 3): bắt kịp tay khi bắt đầu kéo sau 100 ms thay vì 200 ms,
    # trễ lúc kéo 8–25 ms thay vì 19–49 ms, vẫn lọc ~63 % độ rung (người dùng báo "bị trễ")
    def __init__(self, min_cutoff=1.0, beta=15.0, d_cutoff=3.0):
        self.mc, self.beta, self.dc = min_cutoff, beta, d_cutoff
        self.x = self.dx = self.t = None

    @staticmethod
    def _a(cutoff, te):
        return 1.0 / (1.0 + 1.0 / (2 * math.pi * cutoff * te))

    def loc(self, x, t):
        x = np.asarray(x, float)
        if self.x is None or t <= self.t:
            self.x, self.dx, self.t = x, np.zeros_like(x), t
            return x.copy()
        te, self.t = t - self.t, t
        self.dx = self.dx + self._a(self.dc, te) * ((x - self.x) / te - self.dx)
        self.x = self.x + self._a(self.mc + self.beta * float(np.linalg.norm(self.dx)), te) * (x - self.x)
        return self.x.copy()


class Tay:
    """Trạng thái một bàn tay giữa các khung: bộ lọc con trỏ, chụm có trễ, lúc thấy lần cuối."""

    def __init__(self):
        self.loc = LocOneEuro()
        self.cham = False
        self.dem = 0
        self.thay_luc = -1.0


def la_xoe(diem, ti_le):
    """4 đầu ngón (trỏ, giữa, áp út, út) đều xa cổ tay hơn khớp giữa ngón → bàn tay đang xoè."""
    c = diem[0]
    kc = lambda p: math.hypot((p.x - c.x) * ti_le, p.y - c.y)
    return all(kc(diem[dau]) > kc(diem[giua]) * 1.15 for dau, giua in ((8, 6), (12, 10), (16, 14), (20, 18)))


def doc_tay(kq, ti_le, t, cac_tay):
    """Kết quả MediaPipe → danh sách tay: con trỏ đã lọc (đơn vị chiều cao khung, x đã nhân tỉ lệ khung),
    đang chạm (chụm có trễ, phải đủ 2 khung liên tiếp mới đổi), tỉ lệ chụm, có đang xoè không."""
    ra = []
    for i, diem in enumerate(kq.hand_landmarks):
        ben = kq.handedness[i][0].category_name if kq.handedness and kq.handedness[i] else f"Tay{i}"
        tay = cac_tay.get(ben)
        if tay is None or t - tay.thay_luc > 0.3:  # tay mới hoặc mất lâu rồi mới thấy lại → bắt đầu lại từ đầu
            tay = cac_tay[ben] = Tay()
        tay.thay_luc = t
        the_gioi = kq.hand_world_landmarks[i] if kq.hand_world_landmarks else None
        tl = ti_le_chum(diem, the_gioi, ti_le)
        muon_cham = tl < CHAM_BAT if not tay.cham else tl <= CHAM_NHA
        tay.dem = tay.dem + 1 if muon_cham != tay.cham else 0
        if tay.dem >= 2:
            tay.cham, tay.dem = muon_cham, 0
        diem_chum = ((diem[4].x + diem[8].x) / 2 * ti_le, (diem[4].y + diem[8].y) / 2)
        ra.append({"ben": ben, "diem": diem, "con_tro": tay.loc.loc(diem_chum, t), "cham": tay.cham, "tl": tl,
                   "xoe": tl > 0.6 and la_xoe(diem, ti_le)})
    return ra


# ============================================================================ cử chỉ → góc quay
def goc_ngan(a):
    return (a + math.pi) % (2 * math.pi) - math.pi


class DieuKhien:
    """Biến cử chỉ thành góc quay (yaw, pitch) và mức phóng to, có quán tính khi lướt."""

    def __init__(self):
        self.yaw, self.pitch, self.zoom = GOC_DAU[0], GOC_DAU[1], 1.0
        self.w_yaw = self.w_pitch = 0.0
        self.che_do = "cho"            # cho | keo | zoom | quan_tinh
        self.ve_dau = False
        self.ben_keo = self.goc_keo = self.cuoi = None
        self.da_truot = False
        self.lich_su = deque(maxlen=90)  # (t, yaw, pitch) lúc đang kéo → tính tốc độ khi thả
        self.kc_zoom = None
        self.mat_tay_tu = None
        self.xoe_tu, self.da_dat_lai = None, False

    def ve_goc_dau(self):
        self.w_yaw = self.w_pitch = 0.0
        self.ve_dau = True
        if self.che_do == "quan_tinh":
            self.che_do = "cho"

    def _bat_dau(self):  # chạm vào là "bắt" mô hình: dừng quán tính, dừng bay về
        self.w_yaw = self.w_pitch = 0.0
        self.ve_dau = False

    def _tha(self, t):
        self.che_do = "cho"
        if not self.da_truot or len(self.lich_su) < 2 or t - self.lich_su[-1][0] > 0.25:
            return
        t1 = self.lich_su[-1][0]
        mau = [m for m in self.lich_su if m[0] >= t1 - 0.12]
        (t0, y0, p0), (t1, y1, p1) = mau[0], mau[-1]
        if t1 - t0 < 0.03:
            return
        wy, wp = (y1 - y0) / (t1 - t0), (p1 - p0) / (t1 - t0)
        if math.hypot(wy, wp) > QT_NGUONG:
            self.w_yaw, self.w_pitch = kep(wy, -QT_MAX, QT_MAX), kep(wp, -QT_MAX, QT_MAX) * 0.5
            self.che_do = "quan_tinh"

    def cap_nhat(self, t, dt, ds_tay):
        dang_cham = [h for h in ds_tay if h["cham"]]

        # Xoè cả hai tay giữ 1 giây → về góc đầu (mỗi lần xoè chỉ một lần)
        if len(ds_tay) == 2 and all(h["xoe"] for h in ds_tay):
            self.xoe_tu = self.xoe_tu if self.xoe_tu is not None else t
            if not self.da_dat_lai and t - self.xoe_tu >= XOE_GIU_S:
                self.ve_goc_dau()
                self.da_dat_lai = True
        else:
            self.xoe_tu, self.da_dat_lai = None, False

        if len(dang_cham) >= 2:                                   # hai tay chụm → phóng to / thu nhỏ
            kc = float(np.hypot(*(dang_cham[0]["con_tro"] - dang_cham[1]["con_tro"])))
            if self.che_do != "zoom":
                self._bat_dau()
                self.che_do, self.kc_zoom = "zoom", kc
            elif kc > 1e-3 and self.kc_zoom > 1e-3:
                he_so = kep(kc / self.kc_zoom, 0.9, 1.1)
                if abs(he_so - 1) > 0.003:
                    self.zoom = kep(self.zoom * he_so, ZOOM_MIN, ZOOM_MAX)
                self.kc_zoom = kc
            self.mat_tay_tu = None
        elif len(dang_cham) == 1:                                 # một tay chụm → kéo để xoay
            h = dang_cham[0]
            p = h["con_tro"]
            if self.che_do != "keo" or self.ben_keo != h["ben"]:
                self._bat_dau()
                self.che_do, self.ben_keo = "keo", h["ben"]
                self.goc_keo, self.cuoi, self.da_truot = p.copy(), p.copy(), False
                self.lich_su.clear()
            else:
                if np.hypot(*(p - self.cuoi)) > 0.2:              # nhảy quá xa (vừa bám lại) → lấy mốc mới
                    self.cuoi = p.copy()
                if not self.da_truot and np.hypot(*(p - self.goc_keo)) > NGUONG_TRUOT:
                    self.da_truot, self.cuoi = True, p.copy()   # bắt đầu xoay từ đây nên không bị giật
                if self.da_truot and h["tl"] < (CHAM_BAT + CHAM_NHA) / 2:  # ngón đang mở ra để thả → bỏ phần dịch lệch đó
                    d = p - self.cuoi
                    self.yaw += d[0] * DO_NHAY
                    self.pitch = kep(self.pitch + d[1] * DO_NHAY, -PITCH_MAX, PITCH_MAX)
                    self.lich_su.append((t, self.yaw, self.pitch))
                self.cuoi = p.copy()
            self.mat_tay_tu = None
        elif self.che_do == "keo":
            if any(h["ben"] == self.ben_keo for h in ds_tay):    # tay vẫn còn, chỉ nhả ngón → thả (có thể lướt)
                self._tha(t)
            else:                                                 # mất tay: chờ chút, quá lâu thì huỷ, không lướt
                self.mat_tay_tu = self.mat_tay_tu if self.mat_tay_tu is not None else t
                if t - self.mat_tay_tu > MAT_TAY_S:
                    self.che_do, self.mat_tay_tu = "cho", None
        elif self.che_do == "zoom":                               # nhả cả hai tay (hoặc mất tay quá lâu) → thôi zoom
            self.mat_tay_tu = self.mat_tay_tu if self.mat_tay_tu is not None else t
            if ds_tay or t - self.mat_tay_tu > MAT_TAY_S:
                self.che_do, self.mat_tay_tu = "cho", None

        if self.che_do == "quan_tinh":
            self.yaw += self.w_yaw * dt
            p = self.pitch + self.w_pitch * dt
            self.pitch = kep(p, -PITCH_MAX, PITCH_MAX)
            if self.pitch != p:
                self.w_pitch = 0.0
            self.w_yaw *= math.exp(-dt / QT_TAT)
            self.w_pitch *= math.exp(-dt / (QT_TAT / 2))
            if math.hypot(self.w_yaw, self.w_pitch) < 0.05:
                self.che_do = "cho"

        if self.ve_dau:
            k = 1 - math.exp(-dt / 0.15)
            lech = goc_ngan(GOC_DAU[0] - self.yaw)                # quay về theo đường ngắn nhất
            self.yaw += lech * k
            self.pitch += (GOC_DAU[1] - self.pitch) * k
            self.zoom += (1.0 - self.zoom) * k
            if abs(lech) < 1e-3 and abs(GOC_DAU[1] - self.pitch) < 1e-3 and abs(1 - self.zoom) < 1e-3:
                self.ve_dau = False


# ============================================================================ mô hình 3D + bộ vẽ
def _luoi_cau(ngang, doc):
    """Lưới cầu đơn vị: (đỉnh, mặt tam giác quay ra ngoài)."""
    v = [(math.cos(la) * math.cos(lo), math.sin(la), math.cos(la) * math.sin(lo))
         for la in np.linspace(math.pi / 2, -math.pi / 2, doc + 1) for lo in np.linspace(0, 2 * math.pi, ngang, endpoint=False)]
    f = []
    for i in range(doc):
        for j in range(ngang):
            a, b = i * ngang + j, i * ngang + (j + 1) % ngang
            c, d = a + ngang, b + ngang
            f += [(a, b, c), (b, d, c)]
    v, f = np.array(v), np.array(f)
    n = np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]])
    ngoai = np.einsum("ij,ij->i", n, v[f].mean(axis=1))
    f[ngoai < 0] = f[ngoai < 0][:, ::-1]  # lật mặt nào đang quay vào trong
    return v, f


_CAU = {}


def khoi_elip(tam, ban_kinh, mau, ngang=20, doc=14, R=None):
    v, f = _CAU.setdefault((ngang, doc), _luoi_cau(ngang, doc))
    v = v * np.asarray(ban_kinh, float)
    if R is not None:
        v = v @ R.T
    return v + np.asarray(tam, float), f, mau


def khoi_doan(a, b, r, mau, ngang=18, doc=12):
    """Khối thuôn nối hai khớp (cánh tay, cẳng chân…): hình cầu kéo dài theo trục a → b."""
    a, b = np.asarray(a, float), np.asarray(b, float)
    dai = float(np.linalg.norm(b - a))
    y = (b - a) / dai
    x = np.cross(y, [0, 0, 1.0])
    if np.linalg.norm(x) < 1e-6:
        x = np.cross(y, [1.0, 0, 0])
    x /= np.linalg.norm(x)
    R = np.column_stack([x, y, np.cross(x, y)])
    return khoi_elip((a + b) / 2, (r, dai / 2 + r * 0.6, r), mau, ngang, doc, R)


def gop(cac_khoi):
    """Gộp nhiều khối thành một lưới: (đỉnh, mặt, màu từng mặt)."""
    V, F, C, dem = [], [], [], 0
    for v, f, mau in cac_khoi:
        V.append(v)
        F.append(f + dem)
        C.append(np.tile(mau, (len(f), 1)))
        dem += len(v)
    return np.vstack(V), np.vstack(F), np.vstack(C).astype(float)


def dung_nguoi_nom():
    """Người nộm cao ~1,8 m, mặt (mắt, mũi) quay về phía người xem, đứng trên một bệ xoay."""
    than = [khoi_elip((0, 0.78, 0), (0.10, 0.125, 0.11), DA, 28, 20),                       # đầu
            khoi_elip((0, 0.765, 0.108), (0.016, 0.025, 0.022), DA, 10, 8),                  # mũi
            khoi_doan((0, 0.60, 0), (0, 0.68, 0), 0.048, DA),                               # cổ
            khoi_elip((0, 0.45, 0), (0.19, 0.17, 0.115), AO, 32, 22),                       # ngực
            khoi_elip((0, 0.27, 0), (0.155, 0.13, 0.10), AO, 28, 18),                       # bụng
            khoi_elip((0, 0.10, 0), (0.17, 0.10, 0.11), QUAN, 28, 18)]                      # hông
    for s in (-1, 1):
        than += [khoi_elip((s * 0.035, 0.80, 0.097), (0.014, 0.014, 0.012), MAT_NGUOI, 10, 8),  # mắt
                 khoi_elip((s * 0.215, 0.555, 0), (0.065, 0.06, 0.065), AO, 18, 12),            # vai
                 khoi_doan((s * 0.225, 0.54, 0), (s * 0.27, 0.28, -0.01), 0.048, AO),          # cánh tay
                 khoi_elip((s * 0.27, 0.28, -0.01), (0.043,) * 3, DA, 14, 10),                  # khuỷu
                 khoi_doan((s * 0.27, 0.28, -0.01), (s * 0.30, 0.04, 0.03), 0.038, DA),        # cẳng tay
                 khoi_elip((s * 0.305, -0.04, 0.035), (0.03, 0.065, 0.045), DA, 14, 10),        # bàn tay
                 khoi_doan((s * 0.09, 0.05, 0), (s * 0.10, -0.40, 0.01), 0.072, QUAN),         # đùi
                 khoi_elip((s * 0.10, -0.40, 0.01), (0.056,) * 3, QUAN, 14, 10),                # gối
                 khoi_doan((s * 0.10, -0.40, 0.01), (s * 0.10, -0.83, -0.01), 0.052, QUAN),    # cẳng chân
                 khoi_elip((s * 0.10, -0.875, 0.045), (0.045, 0.032, 0.11), GIAY, 18, 12)]      # bàn chân
    # Bệ xoay: các múi xen màu + một múi màu cam chỉ hướng mặt trước → nhìn là biết đang quay bao nhiêu
    y0, r, n = -0.91, 0.55, 36
    goc = np.linspace(0, 2 * math.pi, n + 1)
    v = [(0, y0, 0)] + [(r * math.cos(g), y0, r * math.sin(g)) for g in goc[:-1]]
    f = np.array([(0, 1 + (j + 1) % n, 1 + j) for j in range(n)])
    truoc = int(round((math.pi / 2) / (2 * math.pi) * n)) % n  # múi ở hướng +z (mặt trước)
    mau = np.array([(0, 140, 255) if j in (truoc, (truoc - 1) % n) else (70, 68, 66) if j % 2 else (58, 56, 55)
                    for j in range(n)], float)
    san = (np.array(v, float), f, mau)
    return gop(than), san


def ma_tran_xoay(yaw, pitch):
    cy, sy, cp, sp = math.cos(yaw), math.sin(yaw), math.cos(pitch), math.sin(pitch)
    ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    rx = np.array([[1, 0, 0], [0, cp, -sp], [0, sp, cp]])
    return rx @ ry


def ve_luoi(anh, V, F, C, R, kc, chieu_sang=True):
    """Vẽ lưới kiểu "hoạ sĩ": bỏ mặt quay lưng, tô từ xa đến gần, đổ bóng theo hướng đèn."""
    v = V @ R.T
    zc = kc - v[:, 2]                                     # mắt nhìn đặt ở (0, 0, kc), nhìn về gốc toạ độ
    if np.any(zc < 0.05):
        return
    a, b, c = v[F[:, 0]], v[F[:, 1]], v[F[:, 2]]
    n = np.cross(b - a, c - a)
    do_dai = np.linalg.norm(n, axis=1)
    thay = (np.einsum("ij,ij->i", n, np.array([0, 0, kc]) - (a + b + c) / 3) > 0) & (do_dai > 1e-12)
    if not thay.any():
        return
    Ft, Ct = F[thay], C[thay]
    if chieu_sang:
        sang = 0.32 + 0.68 * np.clip((n[thay] / do_dai[thay, None]) @ HUONG_SANG, 0, None)
        Ct = Ct * sang[:, None]
    mau = np.clip(Ct, 0, 255).astype(np.int32).tolist()
    xy = np.stack([RONG / 2 + TIEU_CU * v[:, 0] / zc, TAM_Y - TIEU_CU * v[:, 1] / zc], axis=1)
    tam_giac = np.round(xy[Ft] * 16).astype(np.int32)   # tọa độ ×16 (shift = 4) để cạnh mịn hơn
    for i in np.argsort(-zc[Ft].mean(axis=1)):
        cv2.fillConvexPoly(anh, tam_giac[i], mau[i], cv2.LINE_8, 4)


def ve_canh(anh, nguoi, san, yaw, pitch, zoom):
    R = ma_tran_xoay(yaw, pitch)
    kc = KC_GOC / zoom
    ve_luoi(anh, *san, R, kc, chieu_sang=False)  # sàn luôn nằm dưới người nên vẽ trước
    # bóng mờ dưới chân (chỉ khi đang nhìn thấy mặt trên của bệ)
    g = np.linspace(0, 2 * math.pi, 40, endpoint=False)
    bong = np.stack([0.28 * np.cos(g), np.full_like(g, -0.905), 0.2 * np.sin(g)], 1) @ R.T
    zc = kc - bong[:, 2]
    if np.all(zc > 0.05) and 0.91 + kc * math.sin(pitch) > 0:
        pts = np.stack([RONG / 2 + TIEU_CU * bong[:, 0] / zc, TAM_Y - TIEU_CU * bong[:, 1] / zc], 1)
        x0, y0 = np.maximum(np.floor(pts.min(0)).astype(int) - 2, 0)
        x1, y1 = np.minimum(np.ceil(pts.max(0)).astype(int) + 2, (RONG, CAO))
        if x1 > x0 and y1 > y0:
            vung = anh[y0:y1, x0:x1]
            lop = vung.copy()
            cv2.fillPoly(lop, [np.round((pts - (x0, y0)) * 16).astype(np.int32)], (25, 25, 25), cv2.LINE_AA, 4)
            cv2.addWeighted(lop, 0.55, vung, 0.45, 0, vung)
    ve_luoi(anh, *nguoi, R, kc)


def tao_nen():
    t = np.linspace(0, 1, CAO)[:, None, None]
    tren, duoi = np.array([58, 44, 36], float), np.array([24, 20, 18], float)
    return np.broadcast_to(tren * (1 - t) + duoi * t, (CAO, RONG, 3)).astype(np.uint8).copy()


# ============================================================================ giao diện
def ra_cua_so(con_tro, ti_le):
    """Con trỏ (đơn vị khung camera) → toạ độ cửa sổ, trải vùng tay hay hoạt động ra cả cửa sổ."""
    x, y = con_tro[0] / ti_le, con_tro[1]
    (x0, x1), (y0, y1) = VUNG
    return (round(kep((x - x0) / (x1 - x0), 0, 1) * RONG), round(kep((y - y0) / (y1 - y0), 0, 1) * CAO))


def ve_con_tro(anh, ds_tay, dk, ti_le):
    for h in ds_tay:
        p = ra_cua_so(h["con_tro"], ti_le)
        mau = XANH_DUONG if dk.che_do == "zoom" and h["cham"] else CAM
        if h["cham"]:
            cv2.circle(anh, p, 13, mau, -1, cv2.LINE_AA)
            cv2.circle(anh, p, 20, mau, 2, cv2.LINE_AA)
        else:
            do_chum = kep((0.7 - h["tl"]) / (0.7 - CHAM_BAT), 0, 1)
            cv2.circle(anh, p, 20, TRANG, 1, cv2.LINE_AA)
            cv2.ellipse(anh, p, (20, 20), -90, 0, 360 * do_chum, XANH, 3, cv2.LINE_AA)
    if dk.che_do == "zoom":
        cham = [ra_cua_so(h["con_tro"], ti_le) for h in ds_tay if h["cham"]]
        if len(cham) == 2:
            cv2.line(anh, cham[0], cham[1], XANH_DUONG, 2, cv2.LINE_AA)


TEN_CHE_DO = {"cho": "CHO", "keo": "DANG XOAY", "zoom": "PHONG TO / THU NHO", "quan_tinh": "LUOT (QUAN TINH)"}
HUONG_DAN = ["Chum ngon cai + ngon tro roi keo: xoay", "Keo nhanh roi tha: quay tiep, cham lai de dung",
             "Chum ca 2 tay roi keo gian: phong to", "Xoe 2 ban tay 1 giay / phim r: ve goc dau",
             "q: thoat   v: an/hien camera"]


def ve_giao_dien(anh, khung, ds_tay, dk, t, fps, ms_nd, hien_cam):
    """Con trỏ tay, hình camera nhỏ góc phải, dòng trạng thái và hướng dẫn."""
    ti_le = khung.shape[1] / khung.shape[0]
    ve_con_tro(anh, ds_tay, dk, ti_le)
    if hien_cam:
        nho = cv2.resize(khung, (320, round(320 / ti_le)))
        for h in ds_tay:
            ve_tay(nho, h["diem"], CAM if h["cham"] else XANH, False)
        y0 = CAO - nho.shape[0] - 12
        anh[y0:y0 + nho.shape[0], RONG - 332:RONG - 12] = nho
        cv2.rectangle(anh, (RONG - 333, y0 - 1), (RONG - 12, y0 + nho.shape[0]), TRANG, 1)
    chu(anh, f"{'VE GOC DAU' if dk.ve_dau else TEN_CHE_DO[dk.che_do]}   |   FPS {fps:.0f}   nhan dien {ms_nd:.0f} ms   "
             f"goc {math.degrees(goc_ngan(dk.yaw)):+.0f} / {math.degrees(dk.pitch):+.0f}   zoom x{dk.zoom:.2f}", 14, 30)
    for i, dong in enumerate(HUONG_DAN):
        chu(anh, dong, 14, CAO - 22 - 28 * (len(HUONG_DAN) - 1 - i), co=0.5)
    if dk.xoe_tu is not None and not dk.da_dat_lai:  # vòng tiến độ khi đang xoè hai tay
        cv2.ellipse(anh, (RONG // 2, 60), (22, 22), -90, 0, 360 * min(1, (t - dk.xoe_tu) / XOE_GIU_S), XANH, 4, cv2.LINE_AA)


def main():
    may = tao_may_nhan_dien()
    so_cam, ten_cam, cam = tim_camera()
    if cam is None:
        sys.exit("Không mở được camera của Mac. Kiểm tra quyền camera của app terminal đang dùng "
                 "(Cài đặt hệ thống → Quyền riêng tư & Bảo mật → Camera), hoặc chạy trong app Terminal của Mac.")
    print(f"Đang dùng: {ten_cam} (số {so_cam}). Phím: q = thoát, r = về góc đầu, v = ẩn/hiện camera.")

    nguoi, san = dung_nguoi_nom()
    nen = tao_nen()
    dk = DieuKhien()
    cac_tay, hien_cam = {}, True
    cua_so = "Xoay mo hinh 3D bang tay (q: thoat)"
    cv2.namedWindow(cua_so, cv2.WINDOW_NORMAL)
    cv2.resizeWindow(cua_so, RONG, CAO)
    bat_dau = time.perf_counter()
    t_truoc, ms_truoc, fps = 0.0, -1, 0.0
    try:
        while True:
            ok, khung = cam.read()
            if not ok:
                print("Mất hình từ camera, đang thử mở lại…")
                cam.release()
                so_cam, ten_cam, cam = tim_camera()
                if cam is None:
                    sys.exit("Không mở lại được camera của Mac.")
                continue
            khung = cv2.flip(khung, 1)  # lật gương để tay phải hiện bên phải
            t = time.perf_counter() - bat_dau
            ms = max(int(t * 1000), ms_truoc + 1)
            ms_truoc = ms
            kq, ms_nd = nhan_dien(may, khung, ms)
            ti_le = khung.shape[1] / khung.shape[0]
            ds_tay = doc_tay(kq, ti_le, t, cac_tay)
            dt = kep(t - t_truoc, 1e-3, 0.1)
            t_truoc = t
            fps = 0.9 * fps + 0.1 / dt
            dk.cap_nhat(t, dt, ds_tay)

            anh = nen.copy()
            ve_canh(anh, nguoi, san, dk.yaw, dk.pitch, dk.zoom)
            ve_giao_dien(anh, khung, ds_tay, dk, t, fps, ms_nd, hien_cam)
            cv2.imshow(cua_so, anh)
            phim = cv2.waitKey(1) & 0xFF
            if phim in (ord("q"), 27) or cv2.getWindowProperty(cua_so, cv2.WND_PROP_VISIBLE) < 1:
                break
            if phim == ord("r"):
                dk.ve_goc_dau()
            if phim == ord("v"):
                hien_cam = not hien_cam
    finally:
        cam.release()
        may.close()
        cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
