"""Bộ vẽ 3D tối giản cho phòng thử (pygame, không cần OpenGL): lưới đa giác, chiếu phối cảnh, sắp theo độ sâu, tô phẳng.

Hệ thế giới giống Unity: x phải, y lên, z tới trước (mét). Góc yaw camera dương = quay sang TRÁI.
"""
import math

import numpy as np
import pygame


def mau_sang(mau, k):
    return tuple(max(0, min(255, int(c * k))) for c in mau)


class Luoi:
    """Lưới: đỉnh (n, 3) trong hệ vật, mặt là danh sách chỉ số đỉnh (đa giác lồi, ngược chiều kim đồng hồ nhìn từ ngoài)."""

    def __init__(self, dinh, mat, mau, kinh=False):
        self.dinh = np.asarray(dinh, float)
        self.mat = [tuple(m) for m in mat]
        self.mau = mau
        self.kinh = kinh  # thuỷ tinh: chỉ vẽ viền


def hop(sx, sy, sz, mau, day=False):
    """Hộp kích thước sx, sy, sz; gốc ở tâm đáy nếu day=True, ngược lại ở tâm."""
    x, z = sx / 2, sz / 2
    y0, y1 = (0, sy) if day else (-sy / 2, sy / 2)
    d = [(-x, y0, -z), (x, y0, -z), (x, y1, -z), (-x, y1, -z), (-x, y0, z), (x, y0, z), (x, y1, z), (-x, y1, z)]
    m = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 4, 7, 3), (1, 2, 6, 5), (3, 7, 6, 2), (0, 1, 5, 4)]
    return Luoi(d, m, mau)


def tru(r, h, mau, n=16, nap_tren=True, nap_duoi=True, kinh=False, r_tren=None):
    """Hình trụ (hoặc nón cụt nếu r_tren khác r), gốc ở tâm đáy, trục y."""
    r2 = r if r_tren is None else r_tren
    d = []
    for i in range(n):
        a = 2 * math.pi * i / n
        d.append((r * math.cos(a), 0, r * math.sin(a)))
    for i in range(n):
        a = 2 * math.pi * i / n
        d.append((r2 * math.cos(a), h, r2 * math.sin(a)))
    m = [(i, (i + 1) % n, n + (i + 1) % n, n + i)[::-1] for i in range(n)]
    if nap_duoi:
        m.append(tuple(range(n)))
    if nap_tren:
        m.append(tuple(range(2 * n - 1, n - 1, -1)))
    return Luoi(d, m, mau, kinh)


def ma_tran_yaw(yaw_do):
    a = math.radians(yaw_do)
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, -s], [0, 1, 0], [s, 0, c]])  # yaw dương: trục z (trước) quay về −x (trái)


def ma_tran_truc(truc, goc_do):
    """Quay goc_do quanh véc-tơ đơn vị truc (Rodrigues)."""
    k = np.asarray(truc, float)
    k = k / max(np.linalg.norm(k), 1e-12)
    a = math.radians(goc_do)
    K = np.array([[0, -k[2], k[1]], [k[2], 0, -k[0]], [-k[1], k[0], 0]])
    return np.eye(3) + math.sin(a) * K + (1 - math.cos(a)) * K @ K


class Camera:
    def __init__(self, w, h, fov=70.0):
        self.w, self.h = w, h
        self.f = (h / 2) / math.tan(math.radians(fov) / 2)
        self.vi_tri = np.array([0.0, 1.6, 0.0])
        self.yaw = 0.0
        self.pitch = -18.0

    def truc(self):
        """Ma trận 3x3: hàng là trục phải, lên, trước của camera trong thế giới."""
        R = ma_tran_yaw(self.yaw)
        p = math.radians(self.pitch)
        truoc = R @ np.array([0, math.sin(p), math.cos(p)])
        len_ = R @ np.array([0, math.cos(p), -math.sin(p)])
        phai = np.cross(len_, truoc)
        return np.stack([phai, len_, truoc])

    def chieu(self, P):
        """Chiếu điểm thế giới (n, 3) -> (n, 2) màn hình và độ sâu (n,)."""
        M = self.truc()
        c = (np.atleast_2d(P) - self.vi_tri) @ M.T
        z = c[:, 2]
        zz = np.maximum(z, 1e-3)
        x = self.w / 2 + self.f * c[:, 0] / zz
        y = self.h / 2 - self.f * c[:, 1] / zz
        return np.stack([x, y], 1), z


class BoVe:
    """Gom mặt của mọi vật trong khung hình, sắp xa trước gần sau, rồi vẽ."""
    ANH_SANG = np.array([0.35, 0.85, -0.4]) / np.linalg.norm([0.35, 0.85, -0.4])

    def __init__(self, cam):
        self.cam = cam
        self.mat = []

    def them(self, luoi, R=None, T=(0, 0, 0), mau=None, vien=None):
        R = np.eye(3) if R is None else R
        W = luoi.dinh @ R.T + np.asarray(T, float)
        S, z = self.cam.chieu(W)
        mau = mau or luoi.mau
        for m in luoi.mat:
            idx = list(m)
            zm = z[idx]
            if (zm < 0.05).any():
                continue
            p = W[idx]
            n = np.cross(p[1] - p[0], p[2] - p[0])
            nn = np.linalg.norm(n)
            if nn < 1e-12:
                continue
            n /= nn
            huong_nhin = p[0] - self.cam.vi_tri
            mat_truoc = n @ huong_nhin < 0
            if not mat_truoc and not luoi.kinh:
                continue
            k = 0.55 + 0.45 * max(0.0, float(n @ self.ANH_SANG))
            self.mat.append((float(zm.mean()), S[idx], mau_sang(mau, k), luoi.kinh, mat_truoc, vien))

    def ve(self, man):
        self.mat.sort(key=lambda x: -x[0])
        for _, pts, mau, kinh, truoc, vien in self.mat:
            pts = [(float(a), float(b)) for a, b in pts]
            if kinh:
                if truoc:
                    pygame.draw.polygon(man, mau, pts, 1)
                continue
            pygame.draw.polygon(man, mau, pts)
            if vien:
                pygame.draw.polygon(man, vien, pts, 2)
        self.mat = []

    def duong(self, man, A, B, mau, day=2):
        S, z = self.cam.chieu(np.array([A, B], float))
        if (z > 0.05).all():
            pygame.draw.line(man, mau, S[0], S[1], day)
            return True
        return False

    def diem(self, P):
        S, z = self.cam.chieu(np.array([P], float))
        return (float(S[0, 0]), float(S[0, 1])) if z[0] > 0.05 else None
