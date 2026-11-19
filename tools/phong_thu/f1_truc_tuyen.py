"""Thuật toán hướng F1 của firmware/tay_cam_vera (huong.h) chạy TỪNG MẪU, để phòng thử đưa số đo thật vào đúng
đường xử lý của mạch. Chép 1:1 từ tools/ghi_tay_cam.chay_lai (cấu hình "F1", một hàng); kiểm bằng
    python tools/phong_thu/f1_truc_tuyen.py du_lieu/tay_cam/<tệp>.vera
(hai cách chạy phải cho cùng yaw tới sai số làm tròn).
"""
import math
import os
import sys

import numpy as np

D2R = math.pi / 180


def _qnhan(a, b):
    w1, x1, y1, z1 = a
    w2, x2, y2, z2 = b
    return np.array([w1 * w2 - x1 * x2 - y1 * y2 - z1 * z2, w1 * x2 + x1 * w2 + y1 * z2 - z1 * y2,
                     w1 * y2 - x1 * z2 + y1 * w2 + z1 * x2, w1 * z2 + x1 * y2 - y1 * x2 + z1 * w2])


class F1:
    """khoi_dong(G, A, T): 2 giây đầu nằm yên (firmware cân lúc bật). buoc(g, a, T, dt) -> q (w, x, y, z), tay cầm -> đất."""

    def __init__(self, he_so=(0.0, 0.0, 0.0), do_bang_n=False, ti_le=1.0):
        self.a_ema, self.toc_hoc, self.n_yen, self.beta = 0.025, 0.01, 100, 0.033
        self.lo, self.hi, self.kep, self.vc = 0.85, 1.15, 1000.0, 0.5
        self.w_nhiet = 0.1 if do_bang_n else 0.3
        self.he_so = np.array(he_so, float)
        self.ti_le = ti_le

    def khoi_dong(self, G, A, T):
        G = np.clip(np.asarray(G, float), -self.kep, self.kep)
        self.lech = G.mean(0)
        self.tb = self.lech.copy()
        self.ps = np.ones(3)
        self.dem = 0
        self.yen = False
        self.bd = 0.0
        self.moc_lech = self.lech.copy()
        self.moc_T = float(T)
        a0 = np.asarray(A, float).mean(0)
        phi = math.atan2(a0[1], a0[2])
        th = math.atan2(-a0[0], math.hypot(a0[1], a0[2]))
        self.q = np.array([math.cos(phi / 2) * math.cos(th / 2), math.sin(phi / 2) * math.cos(th / 2),
                           math.cos(phi / 2) * math.sin(th / 2), -math.sin(phi / 2) * math.sin(th / 2)])
        self.t = 0.0
        self.g_hieu_chinh = np.zeros(3)

    def buoc(self, g, a, T, dt):
        self.t += dt
        g = np.clip(np.asarray(g, float), -self.kep, self.kep)
        a = np.asarray(a, float)
        an = float(np.sqrt(a @ a))
        self.tb += self.a_ema * (g - self.tb)
        d = g - self.tb
        self.ps += self.a_ema * (d * d - self.ps)
        yen_nay = math.sqrt(self.ps.sum()) < 0.6 and abs(an - 1) < 0.06
        self.dem = self.dem + 1 if yen_nay else 0
        yen_moi = self.dem > self.n_yen
        if yen_moi and not self.yen:
            self.bd = self.t
        if not yen_moi and self.yen and self.t - self.bd > 1.0:
            dT = T - self.moc_T
            if abs(dT) >= 0.5:
                dd = np.clip((self.lech - self.moc_lech) / dT, -0.05, 0.05)
                self.he_so = (1 - self.w_nhiet) * self.he_so + self.w_nhiet * dd
            self.moc_lech = self.lech.copy()
            self.moc_T = T
        self.yen = yen_moi
        c = g - self.lech - self.he_so * (T - self.moc_T)
        c[2] *= self.ti_le
        if self.yen:
            self.lech = self.lech + self.toc_hoc * (self.tb - self.lech)
            c = np.zeros(3)
        q0, q1, q2, q3 = self.q
        ez = np.array([2 * (q1 * q3 - q0 * q2), 2 * (q2 * q3 + q0 * q1), 1 - 2 * (q1 * q1 + q2 * q2)])
        wz = float(c @ ez)
        giu = min(1.0, max(0.0, (abs(wz) - self.vc) / self.vc))
        c = c - wz * (1 - giu) * ez
        self.g_hieu_chinh = c
        r = c * D2R
        if an > 0:
            ax, ay, az = a / an
        else:
            ax, ay, az = a
        s0 = 4 * q0 * q2 * q2 + 2 * q2 * ax + 4 * q0 * q1 * q1 - 2 * q1 * ay
        s1 = 4 * q1 * q3 * q3 - 2 * q3 * ax + 4 * q0 * q0 * q1 - 2 * q0 * ay - 4 * q1 + 8 * q1 ** 3 + 8 * q1 * q2 * q2 + 4 * q1 * az
        s2 = 4 * q0 * q0 * q2 + 2 * q0 * ax + 4 * q2 * q3 * q3 - 2 * q3 * ay - 4 * q2 + 8 * q2 * q1 * q1 + 8 * q2 ** 3 + 4 * q2 * az
        s3 = 4 * q1 * q1 * q3 - 2 * q1 * ax + 4 * q2 * q2 * q3 - 2 * q2 * ay
        s = np.array([s0, s1, s2, s3])
        s /= max(float(np.linalg.norm(s)), 1e-12)
        b = 0.1 if self.t < 2.0 else self.beta
        qd = 0.5 * _qnhan(self.q, (0.0, r[0], r[1], r[2]))
        if self.lo < an < self.hi:
            qd = qd - b * s
        self.q = self.q + qd * dt
        self.q /= np.linalg.norm(self.q)
        return tuple(self.q)


def _kiem(tep):
    """So với ghi_tay_cam.chay_lai trên một tệp ghi thật."""
    sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    import ghi_tay_cam as gt
    du = gt.doc_tep(tep)
    ts = gt.doc_thong_so(du["chu"])
    cfg = [c for c in gt.cau_hinh(ts) if c[0] == "F1"]
    k0 = 400
    n = min(len(du["G"]), 200 * 120)
    du2 = dict(du, G=du["G"][:n], A=du["A"][:n], DT=du["DT"][:n], T=du["T"][:n])
    yaw_lo, _ = gt.chay_lai(du2, [c[3] for c in cfg], k0)
    f = F1(ts["he_so"], ts["do_bang_n"], ts["ti_le"])
    f.khoi_dong(du["G"][:k0], du["A"][:k0], du["T"][k0])
    yaw = np.zeros(n)
    q_goc = None
    for k in range(k0, n):
        q = np.array(f.buoc(du["G"][k], du["A"][k], du["T"][k], du["DT"][k]))
        if q_goc is None:
            q_goc = q.copy()
        dw = q @ q_goc
        dz = -q[0] * q_goc[3] - q[1] * q_goc[2] + q[2] * q_goc[1] + q[3] * q_goc[0]
        yaw[k] = math.degrees(2 * math.atan2(dz, dw))
    yaw[:k0] = yaw[k0]
    yaw = np.degrees(np.unwrap(np.radians(yaw)))
    print(f"{n - k0} mẫu: lệch lớn nhất giữa F1 từng mẫu và chay_lai = {np.abs(yaw - yaw_lo[0]).max():.2e}°")


if __name__ == "__main__":
    _kiem(sys.argv[1])
