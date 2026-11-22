"""Xử lý tín hiệu phía máy tính (giống phần G2 sẽ làm trong Unity): tia trỏ, lọc One Euro, joystick, cử chỉ.

Hệ trục:
  - tay cầm / mặt đất (firmware): X trước, Y trái, Z lên, thuận tay phải;
  - thế giới của phòng thử (giống Unity): x phải, y lên, z tới trước. Đổi: (x, y, z)_đất -> (-y, z, x).
"""
import math
from collections import deque

import numpy as np


def quay(q, v):
    """Quay véc-tơ v (hệ tay cầm) bằng q (tay cầm -> mặt đất)."""
    w, x, y, z = q
    vx, vy, vz = v
    tx = 2 * (y * vz - z * vy)
    ty = 2 * (z * vx - x * vz)
    tz = 2 * (x * vy - y * vx)
    return (vx + w * tx + y * tz - z * ty, vy + w * ty + z * tx - x * tz, vz + w * tz + x * ty - y * tx)


def dat_sang_tg(v):
    return np.array([-v[1], v[2], v[0]], float)


def goc_q(q):
    """yaw, pitch, roll (độ) theo thứ tự ZYX trong hệ mặt đất."""
    w, x, y, z = q
    yaw = math.degrees(math.atan2(2 * (w * z + x * y), 1 - 2 * (y * y + z * z)))
    pitch = math.degrees(math.asin(max(-1.0, min(1.0, 2 * (w * y - z * x)))))
    roll = math.degrees(math.atan2(2 * (w * x + y * z), 1 - 2 * (x * x + y * y)))
    return yaw, pitch, roll


class OneEuro:
    """Bộ lọc One Euro (Casiez, Roussel, Vogel, CHI 2012): đứng yên lọc mạnh (hết run), chuyển động nhanh lọc nhẹ (ít trễ)."""

    def __init__(self, fc_min=1.0, beta=0.05, fc_dao_ham=1.0):
        self.fc_min, self.beta, self.fc_d = fc_min, beta, fc_dao_ham
        self.x = None
        self.dx = 0.0

    @staticmethod
    def _a(fc, dt):
        tau = 1.0 / (2 * math.pi * fc)
        return 1.0 / (1.0 + tau / dt)

    def __call__(self, x, dt):
        x = np.asarray(x, float)
        if self.x is None or dt <= 0:
            self.x = x.copy()
            self.dx = np.zeros_like(x)
            return self.x
        dx = (x - self.x) / dt
        self.dx = self.dx + self._a(self.fc_d, dt) * (dx - self.dx)
        fc = self.fc_min + self.beta * np.abs(self.dx)
        a = 1.0 / (1.0 + 1.0 / (2 * math.pi * fc * dt))
        self.x = self.x + a * (x - self.x)
        return self.x


def joystick(jx, jy, trong=0.08, ngoai=0.05, mu=1.6):
    """Vùng chết tròn (trong 8%, ngoài 5%) rồi đường cong expo: gạt nhẹ chậm và tinh, gạt hết nhanh."""
    r = math.hypot(jx, jy)
    if r < trong:
        return 0.0, 0.0
    r2 = min(1.0, (r - trong) / (1 - trong - ngoai))
    k = r2 ** mu / r
    return jx * k, jy * k


class ConTro:
    """Tia trỏ từ tay. Hai kiểu để so sánh:
      'van_toc'  : yaw của tia cộng dồn ĐỘ ĐỔI HƯỚNG MŨI tay cầm giữa 2 mẫu liền nhau, bị kẹp trong ±GIOI_HAN_YAW (như
                   con trỏ chuột chạm mép màn hình: quay quá mép thì tự "kéo" lại, nên yaw trôi không làm lệch tia mãi);
                   pitch lấy thẳng từ hướng (gia tốc kế giữ đúng, không trôi).
                   KHÔNG cộng dồn thành phần đứng của vận tốc góc: khi mũi chúc xuống 38° mà lăn cổ tay 70° để rót,
                   thành phần đó làm tia lệch 43° dù mũi không đổi hướng (đo bằng phòng thử 11/11/2026).
      'tuyet_doi': tia đi đúng theo hướng mũi tay cầm (yaw so với lúc căn lại).
    Chế độ tinh (do_nhay < 1): tia đi chậm lại quanh điểm đang trỏ; thôi chế độ tinh thì phần bù pitch tắt dần 0,3 s.
    """
    GIOI_HAN_YAW = 70.0

    def __init__(self):
        self.kieu = "van_toc"
        self.loc = True
        self.yaw = 0.0
        self.pitch = 0.0
        self.yaw0 = 0.0
        self.mo = OneEuro(1.0, 0.05)
        self.ra = np.zeros(2)
        self.roll = 0.0
        self.lich_su = deque(maxlen=60)
        self.can_lai_lan_toi = True
        self.do_nhay = 1.0
        self._huong_cu = None
        self._bu_pitch = 0.0
        self._pitch_tho_cu = None

    def can_lai(self):
        self.can_lai_lan_toi = True

    def cap_nhat(self, s, dt):
        f = quay(s.q, (1, 0, 0))
        yaw_tuyet = math.degrees(math.atan2(f[1], f[0]))
        pitch = math.degrees(math.asin(max(-1.0, min(1.0, f[2]))))
        if self.can_lai_lan_toi:
            self.yaw0 = yaw_tuyet
            self.yaw = 0.0
            self._huong_cu = yaw_tuyet
            self._bu_pitch = 0.0
            self.mo = OneEuro(1.0, 0.05)
            self.can_lai_lan_toi = False
        if self.kieu == "van_toc":
            d = (yaw_tuyet - self._huong_cu + 180) % 360 - 180 if self._huong_cu is not None else 0.0
            if abs(pitch) > 80:  # mũi gần thẳng đứng: hướng ngang không xác định, giữ nguyên tia
                d = 0.0
            self.yaw = max(-self.GIOI_HAN_YAW, min(self.GIOI_HAN_YAW, self.yaw + d * self.do_nhay))
        else:
            self.yaw = (yaw_tuyet - self.yaw0 + 180) % 360 - 180
        self._huong_cu = yaw_tuyet
        # pitch: chế độ tinh thì chỉ đi do_nhay phần thay đổi; phần bù tắt dần khi về độ nhạy 1
        if self._pitch_tho_cu is not None:
            self._bu_pitch -= (pitch - self._pitch_tho_cu) * (1 - self.do_nhay)
        if self.do_nhay >= 1.0:
            self._bu_pitch *= max(0.0, 1 - dt / 0.3)
        self._pitch_tho_cu = pitch
        self.pitch = max(-89.0, min(89.0, pitch + self._bu_pitch))
        tho = np.array([self.yaw, self.pitch])
        self.ra = self.mo(tho, dt) if self.loc else tho
        # roll quanh trục trỏ: dương = xoay theo chiều kim đồng hồ khi nhìn dọc tia
        u = quay(s.q, (0, 0, 1))
        fz = np.array(f)
        len_ = np.array([0.0, 0.0, 1.0]) - fz * fz[2]
        n = np.linalg.norm(len_)
        if n > 1e-6:
            len_ /= n
            uu = np.array(u)
            self.roll = math.degrees(math.atan2(np.dot(np.cross(len_, uu), fz), np.dot(len_, uu)))
        self.lich_su.append(self.ra.copy())

    def huong_tg(self):
        """Hướng tia trong thế giới phòng thử."""
        yaw, pitch = np.radians(self.ra)
        f = (math.cos(pitch) * math.cos(yaw), math.cos(pitch) * math.sin(yaw), math.sin(pitch))
        return dat_sang_tg(f)

    def run_tia(self, so_khung=30):
        """Độ rung của tia (°, độ lệch chuẩn) trong vài khung gần nhất."""
        if len(self.lich_su) < 5:
            return 0.0
        a = np.array(list(self.lich_su)[-so_khung:])
        return float(np.sqrt(a.var(0).sum()))


class CuChiNut:
    """Bộ máy trạng thái 1 nút cảm ứng: nhấp (< 350 ms), nhấp đúp (2 lần nhả cách < 500 ms), giữ (≥ 600 ms), lơ lửng,
    chập chờn (nhả rồi chạm lại trong < 120 ms: tay không làm nhanh thế được, là tín hiệu cảm ứng nhảy).

    Ngưỡng cũ 250 / 500 ms quá chặt với nút cảm ứng điện dung: phiên thật 18/11/2026 có nhiều lần chạm 250–500 ms
    không thành "nhấp" mà thành "giữ" (mở nhầm menu tròn).
    """
    NHAP = 0.35
    GIU = 0.6
    DUP = 0.5   # giữa 2 lần nhả
    CHAP_CHON = 0.12

    def __init__(self, ten):
        self.ten = ten
        self.bam = False
        self.t_bam = 0.0
        self.t_nha = -9.0
        self.da_giu = False
        self.t_nhap_cuoi = -9.0
        self.chap_chon = 0
        self.lo_lung = False

    def cap_nhat(self, bam, muc, t):
        su_kien = []
        if bam and not self.bam:
            if t - self.t_nha < self.CHAP_CHON:
                self.chap_chon += 1
                su_kien.append(self.ten + "_chap_chon")
            self.t_bam = t
            self.da_giu = False
            su_kien.append(self.ten + "_bam")
        elif not bam and self.bam:
            su_kien.append(self.ten + "_nha")
            self.t_nha = t
            if t - self.t_bam < self.NHAP:
                su_kien.append(self.ten + "_nhap")
                if t - self.t_nhap_cuoi < self.DUP:
                    su_kien.append(self.ten + "_nhap_dup")
                self.t_nhap_cuoi = t
        if bam and not self.da_giu and t - self.t_bam >= self.GIU:
            self.da_giu = True
            su_kien.append(self.ten + "_giu")
        self.bam = bam
        self.lo_lung = (not bam) and muc >= 38  # 15% của 255
        return su_kien


class CuChi:
    """Gom cử chỉ: 2 nút, A + B giữ 1 s (căn lại), lắc (gia tốc), khuấy (tia vẽ vòng tròn), vặn (lăn cổ tay)."""

    def __init__(self):
        self.A = CuChiNut("A")
        self.B = CuChiNut("B")
        self.t_ca_hai = None
        self.da_can = False
        self.nang_luong_lac = 0.0
        self.lac = False
        self.tam = None
        self.goc_khuay = 0.0
        self.goc_cu = None
        self.vong = 0
        self.roll_cu = None
        self.vat_xoay = 0.0
        self.nhan_cu = False

    def cap_nhat(self, s, con_tro, t, dt):
        ev = self.A.cap_nhat(s.cham_a, s.muc_a, t) + self.B.cap_nhat(s.cham_b, s.muc_b, t)
        if s.nhan_can and not self.nhan_cu:
            ev.append("can_nhan")
        self.nhan_cu = s.nhan_can
        if s.cham_a and s.cham_b:
            if self.t_ca_hai is None:
                self.t_ca_hai = t
            if not self.da_can and t - self.t_ca_hai >= 1.0:
                self.da_can = True
                ev.append("can_lai")
        else:
            self.t_ca_hai = None
            self.da_can = False
        # vuốt giữa 2 nút: nhả A rồi chạm B trong 300 ms = bước tiếp; nhả B rồi chạm A = lùi lại (PDF trang 9)
        if "A_nha" in ev:
            self.t_nha_a = t
        if "B_nha" in ev:
            self.t_nha_b = t
        if "B_bam" in ev and t - getattr(self, "t_nha_a", -9) < 0.3:
            ev.append("vuot_AB")
        if "A_bam" in ev and t - getattr(self, "t_nha_b", -9) < 0.3:
            ev.append("vuot_BA")
        # lắc: |a| dao động quá 0,8 g trong 0,3 s gần nhất (PDF trang 4)
        an = math.sqrt(sum(v * v for v in s.acc))
        if not hasattr(self, "cua_so_a"):
            self.cua_so_a = deque()
        self.cua_so_a.append((t, an))
        while self.cua_so_a and t - self.cua_so_a[0][0] > 0.3:
            self.cua_so_a.popleft()
        bien = max(a for _, a in self.cua_so_a) - min(a for _, a in self.cua_so_a)
        self.nang_luong_lac = bien
        self.lac = bien > 0.8
        # khuấy: góc quay của tia quanh tâm trượt (0,7 s); đủ 360° là một vòng
        p = con_tro.ra
        self.tam = p.copy() if self.tam is None else self.tam + min(1.0, dt / 0.7) * (p - self.tam)
        d = p - self.tam
        if np.hypot(*d) > 1.2:
            g = math.degrees(math.atan2(d[1], d[0]))
            if self.goc_cu is not None:
                dg = (g - self.goc_cu + 180) % 360 - 180
                self.goc_khuay += dg
                if abs(self.goc_khuay) >= 360:
                    self.vong += 1
                    self.goc_khuay = 0.0
                    ev.append("khuay_vong")
            self.goc_cu = g
        else:
            self.goc_cu = None
        # vặn: cộng dồn roll quanh trục trỏ
        if self.roll_cu is not None:
            self.vat_xoay = (con_tro.roll - self.roll_cu + 180) % 360 - 180
        self.roll_cu = con_tro.roll
        return ev
