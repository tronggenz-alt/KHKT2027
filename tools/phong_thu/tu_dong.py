"""Bot tự làm cả 7 bài trong phòng thử, chạy không cần màn hình: kiểm tra luật tương tác chạy đúng từ đầu đến cuối,
chụp ảnh từng bài, xuất bảng như khi người thật làm.

    python tools/phong_thu/tu_dong.py [--ra thư_mục] [--khong-rung]
    python tools/phong_thu/tu_dong.py --cam-bien du_lieu/tay_cam/<ghi_tĩnh>.vera [--joy-that <khung.csv>]

--cam-bien: con quay, gia tốc kế = chuyển động của bot + chuỗi số đo THẬT của mạch nằm yên (độ lệch lúc bật, nhiễu, trôi,
nhiệt), chạy qua thuật toán hướng F1 của firmware từng mẫu 200 Hz (f1_truc_tuyen.py); mức cảm ứng lúc không chạm cũng lấy
từ tệp ghi. --joy-that: cộng joystick thật lúc buông (cột "joy x/y thô" của khung.csv do thu_that_tinh.py xuất), để thấy
ảnh hưởng của joystick đang đấu nối. Chuyển động vẫn là của bot (tổng hợp), không phải tay người.

Bot điều khiển một tay cầm ảo có run tay và nhiễu giả lập (nguon.NguonAo), đi qua đúng đường xử lý như tay cầm thật:
quaternion -> ConTro (One Euro) -> CuChi -> TuongTac. Số đo của bot chỉ chứng minh luật chạy được; khả năng
dùng thật phải đo bằng người cầm tay cầm thật (chay.py --com COMx).
"""
import argparse
import math
import os
import sys
import time

os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
DAY = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, DAY)

import numpy as np  # noqa: E402

import bai_thu  # noqa: E402
import chay  # noqa: E402
from f1_truc_tuyen import F1  # noqa: E402
from nguon import NguonAo, TrangThai, tu_bit, van_toc_tu_q  # noqa: E402
from xu_ly import quay  # noqa: E402

DT = 1 / 60


class NguonBot(NguonAo):
    ten = "Bot tự thử · tay cầm ảo có run tay"

    def __init__(self, rung=True):
        super().__init__()
        self.rung = rung
        self.nut = {"A": False, "B": False, "can": False}
        self.jx = self.jy = 0.0
        self.lac = 0.0
        self.muc_a = 0

    def cap_nhat_bot(self, dt):
        self._pha += dt
        ry = rp = 0.0
        if self.rung:  # run sinh lý 8–12 Hz khoảng 0,05° và lắc lư chậm khoảng 0,08°
            ry = 0.05 * math.sin(2 * math.pi * 9 * self._pha) + 0.08 * math.sin(2 * math.pi * 0.4 * self._pha)
            rp = 0.04 * math.sin(2 * math.pi * 10.3 * self._pha + 1) + 0.06 * math.sin(2 * math.pi * 0.31 * self._pha)
        q = self.q_tu_goc(self.yaw + ry, -(self.pitch + rp), self.roll)  # pitch dương = ngẩng mũi
        gyro = van_toc_tu_q(self.q_cu, q, dt) if self.q_cu else (0.0, 0.0, 0.0)
        self.q_cu = q
        acc = (self.lac * math.sin(2 * math.pi * 6 * self._pha), 0.0, 1.0)
        a, b = self.nut["A"], self.nut["B"]
        s = TrangThai(q=q, gyro=gyro, acc=acc, joy=(self.jx, self.jy), muc_a=255 if a else self.muc_a, muc_b=255 if b else 0,
                      cham_a=a, cham_b=b, nhan_can=self.nut["can"])
        self._dat(s)


class NguonBotThat(NguonBot):
    """Bot + số đo thật của mạch: hướng do F1 ước lượng từ con quay / gia tốc giả lập có cộng nhiễu thật."""

    def __init__(self, tep_cam_bien, tep_joy=None, rung=True):
        super().__init__(rung)
        sys.path.insert(0, os.path.dirname(DAY))
        import csv
        import ghi_tay_cam as gt
        du = gt.doc_tep(tep_cam_bien)
        ts = gt.doc_thong_so(du["chu"])
        self.G, self.A, self.T = du["G"], du["A"], du["T"]
        self.A_tb = self.A.mean(0)
        lo = du["lo"]
        self.muc_nen = np.stack([lo["muc_a"], lo["muc_b"]], 1)  # mức cảm ứng thật lúc không chạm (theo gói)
        self.f1 = F1(ts["he_so"], ts["do_bang_n"], ts["ti_le"])
        self.k = 400
        self.k_goi = 0
        self.tich = 0.0
        self.q_truoc = None
        self.joy = None
        if tep_joy:
            with open(tep_joy, encoding="utf-8-sig") as f:
                hang = list(csv.DictReader(f))
            self.joy = np.array([[float(h["joy x thô"]), float(h["joy y thô"])] for h in hang])
        self.k_joy = 0
        self.ten = "Bot · cảm biến thật" + (" · joystick thật" if tep_joy else "")
        self.sai = []  # (t, sai yaw °, sai pitch °) giữa hướng F1 và hướng thật của bot
        self.goc0 = None

    @staticmethod
    def _huong(q):
        f = quay(q, (1, 0, 0))
        return math.degrees(math.atan2(f[1], f[0])), math.degrees(math.asin(max(-1.0, min(1.0, f[2]))))

    def _dat(self, s):
        q_that = np.array(s.q)
        if self.q_truoc is None:
            self.q_truoc = q_that
            n0 = 400
            trong = np.array(quay(tuple(q_that * [1, -1, -1, -1]), (0, 0, 1)))
            self.f1.khoi_dong(self.G[:n0], trong + self.A[:n0] - self.A_tb, self.T[n0])
        w = np.array(van_toc_tu_q(tuple(self.q_truoc), tuple(q_that), DT))
        self.tich += DT * 200
        n = int(self.tich)
        self.tich -= n
        a = np.array(s.acc)
        for i in range(n):
            u = (i + 1) / n
            qi = self.q_truoc * (1 - u) + q_that * u
            qi /= np.linalg.norm(qi)
            k = self.k % len(self.G)
            self.k += 1
            a = np.array(quay(tuple(qi * [1, -1, -1, -1]), (0, 0, 1))) + np.array([s.acc[0], 0, 0]) + self.A[k] - self.A_tb
            q_uoc = self.f1.buoc(w + self.G[k], a, self.T[k], 0.005)
        self.q_truoc = q_that
        if n == 0:
            q_uoc = tuple(self.f1.q)
        # sai lệch hướng mũi: F1 so với thật (yaw tương đối lúc đầu, vì F1 không biết hướng bắc)
        yt, pt = self._huong(tuple(q_that))
        yu, pu = self._huong(q_uoc)
        if self.goc0 is None:
            self.goc0 = yu - yt
        self.sai.append((self._pha, (yu - yt - self.goc0 + 180) % 360 - 180, pu - pt))
        kg = (self.k // 4) % len(self.muc_nen)
        muc_a = s.muc_a if s.muc_a else int(self.muc_nen[kg, 0])
        muc_b = s.muc_b if s.muc_b else int(self.muc_nen[kg, 1])
        joy = s.joy
        if self.joy is not None:
            j = self.joy[self.k_joy % len(self.joy)]
            self.k_joy += 1
            joy = (float(np.clip(joy[0] + j[0], -1, 1)), float(np.clip(joy[1] + j[1], -1, 1)))
        s2 = TrangThai(q=q_uoc, gyro=tuple(self.f1.g_hieu_chinh), acc=tuple(a), joy=joy, muc_a=muc_a, muc_b=muc_b,
                       cham_a=s.cham_a, cham_b=s.cham_b, nhan_can=s.nhan_can, yen=self.f1.yen, nhiet=float(self.T[self.k % len(self.T)]))
        super()._dat(s2)


def min_jerk(a, b, u):
    u = min(1.0, max(0.0, u))
    return a + (b - a) * (10 * u ** 3 - 15 * u ** 4 + 6 * u ** 5)


class Bot:
    """Mỗi bài là một generator: mỗi lần yield là một khung hình."""

    def __init__(self, ng):
        self.ng = ng
        self.kich_ban = None
        self.anh = []  # (thời điểm cần chụp, tên)

    def tha_het(self):
        self.ng.nut = {"A": False, "B": False, "can": False}
        self.ng.jx = self.ng.jy = self.ng.lac = 0.0
        self.ng.muc_a = 0

    def buoc(self, app, dt):
        if self.kich_ban is not None:
            try:
                next(self.kich_ban)
            except StopIteration:
                self.kich_ban = None
        self.ng.cap_nhat_bot(dt)

    # ---------- động tác ----------
    def cho(self, s):
        for _ in range(max(1, int(s / DT))):
            yield

    def ngam(self, app, P, s=0.5, lap=True):
        """Xoay cổ tay để tia đi qua điểm P (đường cong min-jerk), có chỉnh lại theo điểm tia thật như người nhìn."""
        y0, p0 = self.ng.yaw, self.ng.pitch
        for i in range(int(s / DT)):
            yd, pd = app.canh.huong_toi(P)
            yd += self.ng.yaw - app.ct.yaw  # tia cộng dồn đổi hướng: bù phần đã lệch so với tay cầm
            self.ng.yaw = min_jerk(y0, yd, (i + 1) * DT / s)
            self.ng.pitch = min_jerk(p0, pd, (i + 1) * DT / s)
            yield
        if lap:
            for _ in range(int(0.25 / DT)):
                yd, pd = app.canh.huong_toi(P)
                self.ng.yaw += (yd - app.ct.ra[0]) * 0.25
                self.ng.pitch += (pd - app.ct.ra[1]) * 0.25
                yield

    def lan(self, goc, s=0.4):
        r0 = self.ng.roll
        for i in range(int(s / DT)):
            self.ng.roll = min_jerk(r0, goc, (i + 1) * DT / s)
            yield

    def nhap(self, nut, s=0.1):
        self.ng.nut[nut] = True
        yield from self.cho(s)
        self.ng.nut[nut] = False
        yield from self.cho(0.08)

    def gat(self, jx, jy, s):
        self.ng.jx, self.ng.jy = jx, jy
        yield from self.cho(s)
        self.ng.jx = self.ng.jy = 0.0

    def dua_vat_toi(self, app, P, s=0.7, giu_tay=False):
        """Đang cầm: ngắm tâm vật tới P, rồi dùng chế độ TAY chỉnh xa / gần cho đúng khoảng cách.
        giu_tay=True: ở lại chế độ TAY (khi rót cần gạt cần chỉnh xa / gần theo dòng chảy)."""
        yield from self.ngam(app, P, s)
        tt = app.canh.tuong_tac
        if tt.che_do != "TAY":
            yield from self.nhap("can")
            self.ng.nut["can"] = False
        for _ in range(int(2.0 / DT)):
            loi = np.linalg.norm(np.asarray(P) - app.canh.tay()) - tt.kc
            if abs(loi) < 0.004:
                break
            self.ng.jy = float(np.clip(loi * 8, -1, 1))
            yd, pd = app.canh.huong_toi(P)
            self.ng.yaw += (yd - app.ct.ra[0]) * 0.2
            self.ng.pitch += (pd - app.ct.ra[1]) * 0.2
            yield
        self.ng.jy = 0.0
        for _ in range(int(0.4 / DT)):
            yd, pd = app.canh.huong_toi(P)
            self.ng.yaw += (yd - app.ct.ra[0]) * 0.2
            self.ng.pitch += (pd - app.ct.ra[1]) * 0.2
            yield
        if not giu_tay:
            yield from self.nhap("can")  # về chế độ ĐI

    # ---------- 7 bài ----------
    def bai_1(self, app):
        c = app.canh
        x, y, _ = c.bia[4]
        yield from self.ngam(app, (x, y, c.mat_tuong_z), 0.8)
        yield from self.cho(3.4)
        self.anh.append("1_tro.png")
        for _ in range(8):
            i = c.bia_sang
            if i is None:
                break
            x, y, _ = c.bia[i]
            yield from self.ngam(app, (x, y, c.mat_tuong_z), 0.55)
            yield from self.nhap("A")
            yield from self.cho(0.1)

    def bai_2(self, app):
        c = app.canh
        for i, v in enumerate(list(c.vat)):
            yield from self.ngam(app, v.tam(), 0.6)
            self.ng.nut["B"] = True
            yield from self.cho(0.2)
            x, z, _, _ = c.o_dat[i]
            yield from self.dua_vat_toi(app, (x, 0.9 + v.h / 2 + 0.003, z))
            if i == 1:
                self.anh.append("2_cam_dat.png")
            self.ng.nut["B"] = False
            yield from self.cho(0.3)

    def bai_3(self, app):
        c = app.canh
        coc, od = c.vat
        yield from self.ngam(app, coc.tam(), 0.6)
        self.ng.nut["B"] = True
        yield from self.cho(0.2)
        goc = 62.0
        g = math.radians(goc)
        dinh = od.dinh()
        # nghiêng quanh tâm: miệng lệch sang phải h/2·sin g, vòi thêm r·cos(g/2); hạ (h/2)(1 − cos g)
        lech = coc.h / 2 * math.sin(g) + coc.r * math.cos(g / 2)
        # miệng lệch theo hướng "phải" của TIA (vuông góc với tia trên mặt ngang, như canh._rot), không phải trục x:
        # tia đi chéo nên lệch theo x làm vòi sai 2,9 cm theo chiều sâu, rót hết ra ngoài (sửa 11/11/2026)
        ngang = np.array(dinh, float) - c.tay()
        ngang[1] = 0.0
        ngang /= max(np.linalg.norm(ngang), 1e-9)
        phai = np.cross(np.array([0, 1.0, 0]), ngang)
        dich = np.array(dinh, float) - phai * lech + np.array([0, 0.05 - coc.h / 2 * math.cos(g), 0])
        yield from self.dua_vat_toi(app, dich, giu_tay=True)
        tt = app.canh.tuong_tac

        def nhin_dong_chay():
            """Nhìn dòng chảy, chỉnh cho rơi đúng miệng ống đong (cả lúc nhỏ giọt ở chế độ tinh):
            lệch ngang với tia thì xoay cổ tay, lệch dọc tia thì gạt cần đẩy ra / kéo vào (chế độ TAY)."""
            self.ng.jy = 0.0
            if tt.dong_chay and tt.tam_cam is not None:
                voi = tt.dong_chay[0][0]
                loi = od.dinh() - voi
                loi[1] = 0.0
                tia = tt.tam_cam - app.canh.tay()
                tia[1] = 0.0
                tia /= max(np.linalg.norm(tia), 1e-9)
                doc = float(loi @ tia)
                yd, pd = app.canh.huong_toi(tt.tam_cam + loi - tia * doc)
                self.ng.yaw += (yd - app.ct.ra[0]) * 0.3 / max(app.ct.do_nhay, 0.25)
                if tt.che_do == "TAY":
                    self.ng.jy = float(np.clip(doc * 8, -1, 1))

        anh = False
        yield from self.lan(37, 0.35)  # nghiêng vừa qua ngưỡng chảy: nhỏ giọt, nhìn dòng để ngắm trước
        for _ in range(int(0.6 / DT)):
            nhin_dong_chay()
            yield
        yield from self.lan(goc, 0.4)
        for _ in range(int(20 / DT)):
            nhin_dong_chay()
            if not anh and od.the_tich > 20:
                self.anh.append("3_rot.png")
                anh = True
            if od.the_tich >= 43:
                break
            yield
        self.ng.nut["A"] = True  # chế độ tinh, nghiêng ít để nhỏ giọt
        yield from self.lan(41, 0.3)
        for _ in range(int(20 / DT)):
            nhin_dong_chay()
            if od.the_tich >= 49.6:
                break
            yield
        self.ng.jy = 0.0
        self.ng.nut["A"] = False
        yield from self.lan(0, 0.25)
        yield from self.cho(0.3)
        yield from self.dua_vat_toi(app, (-0.45, 0.9 + coc.h / 2 + 0.005, 0.8), 0.6)
        self.ng.nut["B"] = False
        yield from self.cho(0.3)

    def bai_4(self, app):
        c = app.canh
        ong, coc, dua = c.vat
        yield from self.ngam(app, ong.tam(), 0.6)
        self.ng.nut["B"] = True
        yield from self.cho(0.2)
        yield from self.ngam(app, ong.tam() + np.array([0, 0.15, 0]), 0.5)
        self.ng.lac = 2.2
        yield from self.cho(1.0)
        self.anh.append("4_lac.png")
        yield from self.cho(1.6)
        self.ng.lac = 0.0
        yield from self.cho(0.3)
        yield from self.dua_vat_toi(app, (-0.5, 0.9 + ong.h / 2 + 0.003, 0.75), 0.5)
        self.ng.nut["B"] = False
        yield from self.cho(0.3)
        yield from self.ngam(app, dua.tam(), 0.6)
        self.ng.nut["B"] = True
        yield from self.cho(0.2)
        P = coc.p + np.array([0, dua.h / 2 + 0.02, 0])
        yield from self.dua_vat_toi(app, P, 0.7)
        yd0, pd0 = self.ng.yaw, self.ng.pitch
        for i in range(int(4.0 / DT)):  # vẽ vòng tròn bán kính 2°, 1 vòng / giây
            a = 2 * math.pi * i * DT
            self.ng.yaw = yd0 + 1.6 * math.cos(a)
            self.ng.pitch = pd0 + 1.6 * math.sin(a)
            if i == int(1.6 / DT):
                self.anh.append("4_khuay.png")
            if coc.chat_ran == 0:
                break
            yield
        self.ng.yaw, self.ng.pitch = yd0, pd0
        yield from self.cho(0.3)
        self.ng.nut["B"] = False
        yield from self.cho(0.3)

    def bai_5(self, app):
        b = app.canh.vat[0]
        nap = b.p + np.array([0, b.h + 0.012, 0])
        yield from self.ngam(app, nap, 0.6)
        for lan in range(8):
            if b.mo_nap:
                break
            self.ng.nut["B"] = True
            yield from self.cho(0.12)
            yield from self.lan(-70, 0.45)
            if lan == 2:
                self.anh.append("5_van.png")
            self.ng.nut["B"] = False
            yield from self.cho(0.1)
            yield from self.lan(70, 0.4)
            yield from self.cho(0.1)
            yield from self.ngam(app, nap, 0.2, lap=False)
        yield from self.lan(0, 0.3)

    def bai_6(self, app):
        c = app.canh
        mx, mz = c.moc_san
        for _ in range(int(15 / DT)):
            d = np.array([mx, 0, mz]) - c.cam_vi_tri
            kc = math.hypot(d[0], d[2])
            if kc < 0.15:
                break
            muon = math.degrees(math.atan2(-d[0], d[2]))
            lech = (muon - c.cam_yaw + 180) % 360 - 180
            self.ng.jx = float(np.clip(-lech / 25, -1, 1))
            self.ng.jy = float(np.clip(kc * 1.5, 0, 1)) if abs(lech) < 20 else 0.0
            if kc < 2.0 and "6_di.png" not in self.anh:
                self.anh.append("6_di.png")
            yield
        for _ in range(int(5 / DT)):
            lech = (0 - c.cam_yaw + 180) % 360 - 180
            if abs(lech) < 5:
                break
            self.ng.jx, self.ng.jy = float(np.clip(-lech / 25, -1, 1)), 0.0
            yield
        self.ng.jx = self.ng.jy = 0.0
        yield from self.cho(0.3)

    def bai_7(self, app):
        b = app.bai
        n = len(chay.cm.HOA_CHAT_MENU)
        for k in range(3):
            i = chay.cm.HOA_CHAT_MENU.index(b.yeu_cau[k])
            self.ng.nut["A"] = True
            yield from self.cho(0.6)
            a = math.radians(i * 360 / n)
            self.ng.jx, self.ng.jy = 0.95 * math.sin(a), 0.95 * math.cos(a)
            yield from self.cho(0.35)
            if k == 1:
                self.anh.append("7_menu.png")
            self.ng.nut["A"] = False
            yield from self.cho(0.05)
            self.ng.jx = self.ng.jy = 0.0
            yield from self.cho(0.4)
        yield from self.nhap("A", 0.1)
        yield from self.nhap("A", 0.1)
        yield from self.cho(0.5)
        self.ng.muc_a = 90  # ngón cái lơ lửng: hiện gợi ý
        yield from self.cho(0.4)
        self.ng.muc_a = 0
        self.ng.nut["A"] = True
        yield from self.cho(0.15)
        self.ng.nut["A"] = False
        yield from self.cho(0.1)
        yield from self.nhap("B", 0.15)
        yield from self.cho(0.5)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--ra", default=None)
    p.add_argument("--khong-rung", action="store_true")
    p.add_argument("--cam-bien", help="tệp ghi .vera tay cầm nằm yên: nhiễu, độ lệch, nhiệt thật đưa qua F1")
    p.add_argument("--joy-that", help="khung.csv của thu_that_tinh.py: cộng joystick thật lúc buông")
    args = p.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")
    ra = args.ra or os.path.join(chay.GOC, "du_lieu", "phong_thu", time.strftime("bot_%Y%m%d_%H%M%S"))
    ng = NguonBotThat(args.cam_bien, args.joy_that, rung=not args.khong_rung) if args.cam_bien else NguonBot(rung=not args.khong_rung)
    bot = Bot(ng)
    app = chay.UngDung(ng, ra, bot=bot, hien=False)
    for _ in range(30):
        app.buoc(DT)
    app.chup("0_man_chao.png")
    app.lam_tat_ca()
    bai_cu = None
    so_khung = 0
    while app.man_hinh != "tong_ket" and so_khung < 60 * 900:
        if app.man_hinh == "bai" and app.bai is not bai_cu:
            bai_cu = app.bai
            bot.kich_ban = getattr(bot, "bai_" + app.bai.ma)(app)
            print(f"Bài {app.bai.ma}: {app.bai.ten}", flush=True)
        if app.man_hinh == "ket_qua_bai" and bot.kich_ban is not None:
            bot.kich_ban = None
            bot.tha_het()
        app.buoc(DT)
        so_khung += 1
        while bot.anh:
            app.chup(bot.anh.pop(0))
        if app.man_hinh == "ket_qua_bai" and app.t - app.t_man < DT * 1.5:
            app.chup(f"{app.bai.ma}_ket_qua.png")
    app.chup("8_tong_ket.png")
    print()
    for ma, ten_bai, ten, gt, dv, tc, d in app.ket_qua:
        print(f"{ma} | {ten_bai} | {ten} | {gt} {dv} | {tc} | {'Đạt' if d else 'CHƯA'}")
    if isinstance(ng, NguonBotThat) and ng.sai:
        S = np.array(ng.sai)
        np.savetxt(os.path.join(ra, "sai_huong_f1.csv"), S, delimiter=",", fmt="%.4f",
                   header="t (s),sai yaw F1 - thật (°),sai pitch F1 - thật (°)", comments="", encoding="utf-8")
        print(f"\nHướng F1 so với hướng thật của bot ({len(S)} khung, {S[-1, 0]:.0f} s): "
              f"|sai yaw| TB {np.abs(S[:, 1]).mean():.2f}°, lớn nhất {np.abs(S[:, 1]).max():.2f}°, cuối {S[-1, 1]:+.2f}° · "
              f"|sai pitch| TB {np.abs(S[:, 2]).mean():.2f}°, lớn nhất {np.abs(S[:, 2]).max():.2f}°")
    print(f"\nĐã lưu ảnh và bảng vào {ra}")


if __name__ == "__main__":
    main()
