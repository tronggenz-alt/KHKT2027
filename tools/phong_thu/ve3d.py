"""Bộ vẽ 3D tối giản cho phòng thử (pygame, không cần OpenGL).

Lưới đa giác -> cắt theo mặt phẳng gần (mặt to như sàn, tường không bị mất khi một góc nằm sau camera) -> chiếu phối
cảnh -> sắp xa trước gần sau theo từng lớp (lớp 0: phòng; lớp 1: đồ vật, luôn vẽ đè lên phòng) -> tô phẳng có ánh
sáng, sương xa, kính trong suốt, viền khử răng cưa.

Hệ thế giới giống Unity: x phải, y lên, z tới trước (mét). Góc yaw camera dương = quay sang TRÁI.
"""
import math

import numpy as np
import pygame
import pygame.gfxdraw

GAN = 0.05          # mặt phẳng gần (m)
GIOI_HAN_AA = 6000  # gfxdraw dùng số 16 bit: bỏ viền mượt khi toạ độ quá lớn


def mau_sang(mau, k):
    return tuple(max(0, min(255, int(c * k))) for c in mau)


def tron_mau(a, b, u):
    """Trộn màu a -> b theo u (0..1)."""
    u = max(0.0, min(1.0, u))
    return tuple(int(x + (y - x) * u) for x, y in zip(a, b))


class Luoi:
    """Lưới: đỉnh (n, 3) trong hệ vật, mặt là danh sách chỉ số đỉnh (đa giác lồi, ngược chiều kim đồng hồ nhìn từ ngoài).

    kinh: thuỷ tinh (tô trong suốt, thấy cả mặt sau). he_so: hệ số sáng riêng từng mặt (ví dụ mặt nước sáng hơn).
    """

    def __init__(self, dinh, mat, mau, kinh=False, hai_mat=False, he_so=None, mau_mat=None):
        self.dinh = np.asarray(dinh, float)
        self.mat = [np.array(m, int) for m in mat]
        self.mau = mau
        self.kinh = kinh
        self.hai_mat = hai_mat or kinh
        self.he_so = np.ones(len(self.mat)) if he_so is None else np.asarray(he_so, float)
        self.mau_mat = None if mau_mat is None else np.asarray(mau_mat, float)   # màu riêng từng mặt (lưới gộp)
        self.i0 = np.array([m[0] for m in self.mat])
        self.i1 = np.array([m[1] for m in self.mat])
        self.i2 = np.array([m[2] for m in self.mat])
        # chỉ số đỉnh của mọi mặt nối liền + vị trí đầu mỗi mặt: tính min / max / tổng theo mặt bằng reduceat
        self.phang = np.concatenate(self.mat)
        self.so = np.array([len(m) for m in self.mat])
        self.dau = np.concatenate([[0], np.cumsum(self.so)[:-1]])


def gop(ds):
    """Gộp nhiều (lưới, R, T) không trong suốt thành một lưới trong hệ thế giới (phòng tĩnh: một lần gọi vẽ)."""
    dinh, mat, mau, hs = [], [], [], []
    n = 0
    for luoi, R, T in ds:
        R = np.eye(3) if R is None else R
        dinh.append(luoi.dinh @ R.T + np.asarray(T, float))
        mat += [m + n for m in luoi.mat]
        mau += [luoi.mau] * len(luoi.mat)
        hs.append(luoi.he_so)
        n += len(luoi.dinh)
    return Luoi(np.concatenate(dinh), mat, (200, 200, 200), he_so=np.concatenate(hs), mau_mat=mau)


def hop(sx, sy, sz, mau, day=False):
    """Hộp kích thước sx, sy, sz; gốc ở tâm đáy nếu day=True, ngược lại ở tâm."""
    x, z = sx / 2, sz / 2
    y0, y1 = (0, sy) if day else (-sy / 2, sy / 2)
    d = [(-x, y0, -z), (x, y0, -z), (x, y1, -z), (-x, y1, -z), (-x, y0, z), (x, y0, z), (x, y1, z), (-x, y1, z)]
    m = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 4, 7, 3), (1, 2, 6, 5), (3, 7, 6, 2), (0, 1, 5, 4)]
    return Luoi(d, m, mau)


def tam(sx, sz, mau, he_so=1.0):
    """Tấm phẳng nằm ngang (mặt hướng lên), gốc ở tâm."""
    x, z = sx / 2, sz / 2
    return Luoi([(-x, 0, -z), (-x, 0, z), (x, 0, z), (x, 0, -z)], [(0, 1, 2, 3)], mau, he_so=[he_so])


def tam_dung(sx, sy, mau, hai_mat=False):
    """Tấm đứng trong mặt phẳng x-y, mặt hướng về −z (về phía người đứng xem), gốc ở tâm."""
    x, y = sx / 2, sy / 2
    return Luoi([(-x, -y, 0), (-x, y, 0), (x, y, 0), (x, -y, 0)], [(0, 1, 2, 3)], mau, hai_mat=hai_mat)


def tru(r, h, mau, n=16, nap_tren=True, nap_duoi=True, kinh=False, r_tren=None, sang_nap=1.0):
    """Hình trụ (hoặc nón cụt nếu r_tren khác r), gốc ở tâm đáy, trục y. sang_nap: hệ số sáng nắp trên."""
    r2 = r if r_tren is None else r_tren
    d = []
    for i in range(n):
        a = 2 * math.pi * i / n
        d.append((r * math.cos(a), 0, r * math.sin(a)))
    for i in range(n):
        a = 2 * math.pi * i / n
        d.append((r2 * math.cos(a), h, r2 * math.sin(a)))
    m = [(i, (i + 1) % n, n + (i + 1) % n, n + i)[::-1] for i in range(n)]
    hs = [1.0] * n
    if nap_duoi:
        m.append(tuple(range(n)))
        hs.append(1.0)
    if nap_tren:
        m.append(tuple(range(2 * n - 1, n - 1, -1)))
        hs.append(sang_nap)
    return Luoi(d, m, mau, kinh, he_so=hs)


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
    """cx, cy: tâm chiếu trên màn hình (dời sang phải khi bên trái có bảng điều khiển)."""

    def __init__(self, w, h, fov=66.0, cx=None, cy=None):
        self.w, self.h = w, h
        self.cx = w / 2 if cx is None else cx
        self.cy = h / 2 if cy is None else cy
        self.f = (h / 2) / math.tan(math.radians(fov) / 2)
        self.vi_tri = np.array([0.0, 1.6, 0.0])
        self.yaw = 0.0
        self.pitch = -18.0

    def truc(self):
        """Ma trận 3x3: hàng là trục phải, lên, trước của camera trong thế giới (nhớ lại theo yaw, pitch)."""
        khoa = (self.yaw, self.pitch)
        if getattr(self, "_khoa", None) != khoa:
            R = ma_tran_yaw(self.yaw)
            p = math.radians(self.pitch)
            truoc = R @ np.array([0, math.sin(p), math.cos(p)])
            len_ = R @ np.array([0, math.cos(p), -math.sin(p)])
            phai = np.cross(len_, truoc)
            self._M = np.stack([phai, len_, truoc])
            self._khoa = khoa
        return self._M

    def doi(self, P):
        """Điểm thế giới (n, 3) -> hệ camera (n, 3)."""
        return (np.atleast_2d(P) - self.vi_tri) @ self.truc().T

    def chieu_c(self, C):
        """Hệ camera (n, 3) -> màn hình (n, 2)."""
        zz = np.maximum(C[:, 2], 1e-3)
        return np.stack([self.cx + self.f * C[:, 0] / zz, self.cy - self.f * C[:, 1] / zz], 1)

    def chieu(self, P):
        """Chiếu điểm thế giới (n, 3) -> (n, 2) màn hình và độ sâu (n,)."""
        C = self.doi(P)
        return self.chieu_c(C), C[:, 2]


def cat_gan(P):
    """Cắt đa giác (hệ camera) theo mặt phẳng z = GAN (Sutherland–Hodgman một mặt)."""
    ra = []
    n = len(P)
    for i in range(n):
        a, b = P[i], P[(i + 1) % n]
        trong_a, trong_b = a[2] >= GAN, b[2] >= GAN
        if trong_a:
            ra.append(a)
        if trong_a != trong_b:
            u = (GAN - a[2]) / (b[2] - a[2])
            ra.append(a + (b - a) * u)
    return np.array(ra) if len(ra) >= 3 else None


class BoVe:
    """Gom mặt của mọi vật trong khung hình theo lớp, sắp xa trước gần sau, rồi vẽ."""
    ANH_SANG = np.array([0.35, 0.85, -0.4]) / np.linalg.norm([0.35, 0.85, -0.4])

    def __init__(self, cam, mau_suong=(200, 208, 214), suong=(4.0, 14.0, 0.45)):
        self.cam = cam
        self.lop = {}
        self.mau_suong = mau_suong
        self.suong = suong  # (bắt đầu, hết, mức lớn nhất)

    def them(self, luoi, R=None, T=(0, 0, 0), mau=None, vien=None, lop=1, alpha=None, sang=None):
        """alpha: tô trong suốt (0..255) cả lưới; sang: hệ số sáng cố định (đèn, màn hình), bỏ qua ánh sáng.

        Mọi phép theo mặt (pháp tuyến, mặt trước / sau, độ sâu, khung bao, màu, sương) tính một lượt bằng numpy;
        vòng lặp Python chỉ còn ở các mặt thật sự hiện ra."""
        cam = self.cam
        W = luoi.dinh if R is None else luoi.dinh @ R.T
        W = W + np.asarray(T, float)
        C = (W - cam.vi_tri) @ cam.truc().T
        z = C[:, 2]
        if z.max() < GAN:
            return
        S = cam.chieu_c(C)
        a = W[luoi.i0]
        b = W[luoi.i1] - a
        c = W[luoi.i2] - a
        n = np.stack([b[:, 1] * c[:, 2] - b[:, 2] * c[:, 1], b[:, 2] * c[:, 0] - b[:, 0] * c[:, 2],
                      b[:, 0] * c[:, 1] - b[:, 1] * c[:, 0]], 1)
        nn = np.sqrt((n * n).sum(1))
        co = nn > 1e-12
        n = n / np.where(co, nn, 1.0)[:, None]
        truoc = ((a - cam.vi_tri) * n).sum(1) < 0
        f, d = luoi.phang, luoi.dau
        zf = z[f]
        zmin = np.minimum.reduceat(zf, d)
        zmax = np.maximum.reduceat(zf, d)
        sau = np.add.reduceat(zf, d) / luoi.so
        sx, sy = S[f, 0], S[f, 1]
        hien = (co & (truoc | luoi.hai_mat) & (zmax >= GAN)
                & (np.maximum.reduceat(sx, d) >= 0) & (np.minimum.reduceat(sx, d) <= cam.w)
                & (np.maximum.reduceat(sy, d) >= 0) & (np.minimum.reduceat(sy, d) <= cam.h))
        if not hien.any():
            return
        if sang is not None:
            k = np.full(len(n), float(sang))
        else:
            k = 0.56 + 0.40 * np.clip(n @ self.ANH_SANG, 0, None) + 0.07 * n[:, 1]
        k = k * luoi.he_so
        if not luoi.kinh:
            k = np.where(truoc, k, k * 0.8)
        if mau is None and luoi.mau_mat is not None:
            goc = luoi.mau_mat
        else:
            goc = np.asarray(mau or luoi.mau, float)[None, :]
        col = goc * k[:, None]
        s0, s1, smax = self.suong
        u = np.clip(smax * (sau - s0) / (s1 - s0), 0, smax)[:, None]
        col = np.clip(col + (np.asarray(self.mau_suong, float) - col) * u, 0, 255).astype(int)
        ds = self.lop.setdefault(lop, [])
        for j in np.nonzero(hien)[0]:
            idx = luoi.mat[j]
            mau_m = tuple(col[j])
            if zmin[j] < GAN:
                Cc = cat_gan(C[idx])
                if Cc is None:
                    continue
                pts = cam.chieu_c(Cc)
                sj = float(Cc[:, 2].mean())
            else:
                pts = S[idx]
                sj = float(sau[j])
            if luoi.kinh:
                aj = (54 if truoc[j] else 30) if alpha is None else alpha
                vien_m = (*tron_mau(mau_m, (255, 255, 255), 0.55), 150 if truoc[j] else 70)
                ds.append((sj, pts, mau_m, aj, vien_m))
            else:
                ds.append((sj, pts, mau_m, alpha, vien))

    def ve(self, man, chi_lop=None):
        """Vẽ rồi xoá các lớp (tất cả, hoặc chỉ chi_lop)."""
        for lop in sorted(self.lop):
            if chi_lop is not None and lop != chi_lop:
                continue
            ds = self.lop[lop]
            ds.sort(key=lambda x: -x[0])
            # mặt trong suốt liền nhau vẽ chung lên một lớp alpha, dán xuống khi gặp mặt đục kế tiếp (ít tấm vẽ, nhanh)
            mo = self._lop_mo(man)
            ban = None
            for _, pts, mau, alpha, vien in ds:
                if alpha is None:
                    if ban is not None:
                        man.blit(mo, ban, ban)
                        mo.fill((0, 0, 0, 0), ban)
                        ban = None
                    ve_da_giac(man, pts, mau)
                    if vien is not None:
                        pygame.draw.polygon(man, vien, _nguyen(pts), 2)
                    continue
                if np.abs(pts).max() > GIOI_HAN_AA:
                    continue
                p = _nguyen(pts)
                r = pygame.draw.polygon(mo, (*mau, alpha), p)
                if vien is not None and len(vien) == 4:
                    r = r.union(pygame.draw.aalines(mo, vien, True, p))
                ban = r if ban is None else ban.union(r)
            if ban is not None:
                man.blit(mo, ban, ban)
                mo.fill((0, 0, 0, 0), ban)
            ds.clear()

    def _lop_mo(self, man):
        kt = man.get_size()
        if getattr(self, "_mo", None) is None or self._mo.get_size() != kt:
            self._mo = pygame.Surface(kt, pygame.SRCALPHA)
        return self._mo

    def duong(self, man, A, B, mau, day=2):
        """Đoạn thẳng 3D (cắt theo mặt phẳng gần). Trả về 2 điểm màn hình hoặc None."""
        C = self.cam.doi(np.array([A, B], float))
        if C[0, 2] < GAN and C[1, 2] < GAN:
            return None
        if C[0, 2] < GAN or C[1, 2] < GAN:
            a, b = (C[0], C[1]) if C[0, 2] >= GAN else (C[1], C[0])
            u = (GAN - a[2]) / (b[2] - a[2])
            C = np.array([a, a + (b - a) * u])
        S = self.cam.chieu_c(C)
        if np.abs(S).max() < GIOI_HAN_AA:
            pygame.draw.line(man, mau, S[0], S[1], day)
        return S

    def diem(self, P):
        S, z = self.cam.chieu(np.array([P], float))
        return (float(S[0, 0]), float(S[0, 1])) if z[0] > GAN else None

    def vong(self, P, r, n=24):
        """Điểm màn hình của vòng tròn nằm ngang bán kính r quanh P; None nếu có điểm khuất sau camera."""
        a = np.linspace(0, 2 * math.pi, n, endpoint=False)
        Q = np.stack([P[0] + r * np.cos(a), np.full(n, P[1]), P[2] + r * np.sin(a)], 1)
        S, z = self.cam.chieu(Q)
        return S if (z > GAN).all() and np.abs(S).max() < GIOI_HAN_AA else None


def _nguyen(pts):
    return np.rint(pts).astype(np.int32).tolist()


def ve_da_giac(man, pts, mau):
    p = _nguyen(pts)
    pygame.draw.polygon(man, mau, p)
    if np.abs(pts).max() < GIOI_HAN_AA:
        pygame.gfxdraw.aapolygon(man, p, mau)


def _tam_ve(man, pts):
    x0, y0 = np.floor(pts.min(0)).astype(int)
    x1, y1 = np.ceil(pts.max(0)).astype(int)
    x0, y0 = max(x0, -2), max(y0, -2)
    x1, y1 = min(x1, man.get_width() + 2), min(y1, man.get_height() + 2)
    if x1 <= x0 or y1 <= y0:
        return None, None
    return pygame.Surface((x1 - x0 + 2, y1 - y0 + 2), pygame.SRCALPHA), (x0, y0)


def ve_da_giac_mo(man, pts, mau, alpha):
    """Đa giác trong suốt: vẽ lên tấm nhỏ bằng khung bao rồi dán (trộn alpha)."""
    tv, goc = _tam_ve(man, pts)
    if tv is None:
        return
    p = [(x - goc[0], y - goc[1]) for x, y in _nguyen(pts)]
    pygame.draw.polygon(tv, (*mau, alpha), p)
    man.blit(tv, goc)


def ve_vien_mo(man, pts, mau_a):
    """Viền mảnh trong suốt (mép thuỷ tinh)."""
    tv, goc = _tam_ve(man, pts)
    if tv is None:
        return
    p = [(x - goc[0], y - goc[1]) for x, y in _nguyen(pts)]
    pygame.draw.aalines(tv, mau_a, True, p)
    man.blit(tv, goc)


def ve_elip_mo(man, S, mau, alpha):
    """Đa giác mờ từ các điểm màn hình S (bóng đổ, vòng đánh dấu)."""
    if S is not None:
        ve_da_giac_mo(man, np.asarray(S, float), mau, alpha)
