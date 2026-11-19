"""Phòng thử tay cầm VERA: môi trường ảo chạy riêng (không cần Unity) để thử mọi tương tác trước khi đưa vào Unity.

Chạy (từ thư mục dự án; thêm --toan-man để chạy toàn màn hình):
    python tools/phong_thu/chay.py                     tay cầm ảo (chuột + bàn phím)
    python tools/phong_thu/chay.py --com COM8          tay cầm thật qua USB (firmware tay_cam_vera, gói thô r2)
    python tools/phong_thu/chay.py --wifi              tay cầm thật qua Wi-Fi (gói v3, cổng 4210)
    python tools/phong_thu/chay.py --phat tep.vera     phát lại một lần ghi
    python tools/phong_thu/tu_dong.py                  bot tự làm cả 7 bài (kiểm tra luật, chụp ảnh)

Phím: Enter làm lần lượt 7 bài · 1–7 làm một bài · Tab bảng số liệu · R căn lại tia · K căn trục gắn BMI160
      P đổi kiểu tia (vận tốc / tuyệt đối) · F bật/tắt lọc One Euro · N bật/tắt rung tay giả lập · Esc thoát
Tay cầm ảo: chuột xoay, Q/E lăn cổ tay, W A S D cần gạt, chuột trái/K = B, chuột phải/J = A, Space nhấn cần,
            giữ L lắc, giữ F ngón cái lơ lửng trên A.
Kết quả: du_lieu/phong_thu/phien_<giờ>/ (ket_qua.csv, tong_ket.xlsx, tong_ket.png, su_kien.csv, thong_ke_nguon.csv)
"""
import argparse
import csv
import math
import os
import sys
import time

import numpy as np

DAY = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, DAY)
os.environ.setdefault("PYGAME_HIDE_SUPPORT_PROMPT", "1")
import pygame  # noqa: E402

import bai_thu  # noqa: E402
import canh as cm  # noqa: E402
from nguon import NguonAo, NguonPhatLai, NguonThuCamBien, NguonUSB, NguonWifi, SuaTrucGan  # noqa: E402
from ve3d import BoVe, Camera, hop, ma_tran_yaw, tru  # noqa: E402
from xu_ly import ConTro, CuChi, dat_sang_tg, joystick, quay  # noqa: E402

GOC = os.path.dirname(os.path.dirname(DAY))
TEP_TRUC = os.path.join(GOC, "du_lieu", "tay_cam", "truc_gan.json")
W, H = 1280, 720
NEN = (24, 32, 40)
CHU = (236, 242, 245)
NHAT = (150, 168, 178)
NHAN = (56, 214, 196)     # #38d6c4, màu nhấn VERA
DO = (240, 96, 96)
VANG = (245, 196, 80)
XANH = (90, 200, 120)


def phong_chu(co, dam=False):
    for ten in ("Segoe UI", "Arial", "DejaVu Sans"):
        f = pygame.font.SysFont(ten, co, bold=dam)
        if f:
            return f
    return pygame.font.Font(None, co)


class UngDung:
    def __init__(self, nguon, thu_muc_ra, bot=None, hien=True, toan_man=False):
        pygame.init()
        # toàn màn hình: giữ khung vẽ 1280×720, SDL phóng to đúng tỉ lệ (SCALED) nên chữ, bảng không lệch
        co = (pygame.FULLSCREEN | pygame.SCALED) if toan_man else 0
        self.man = pygame.display.set_mode((W, H), co) if hien else pygame.Surface((W, H))
        if hien:
            pygame.display.set_caption("VERA Lab · Phòng thử tay cầm")
        self.f = {k: phong_chu(v) for k, v in (("nho", 15), ("vua", 18), ("to", 24))}
        self.f["dam"] = phong_chu(18, True)
        self.f["tieu_de"] = phong_chu(30, True)
        self.nguon = nguon
        self.bot = bot
        self.sua_truc = SuaTrucGan(TEP_TRUC if not isinstance(nguon, NguonAo) else None)
        self.cam = Camera(W, H)
        self.cam.pitch = -24
        self.ve = BoVe(self.cam)
        self.phong = cm.luoi_phong()
        self.canh = cm.Canh()
        self.ct = ConTro()
        self.cu_chi = CuChi()
        self.t = 0.0
        self.bai = None
        self.hang_doi = []
        self.ket_qua = []           # (mã bài, tên bài, chỉ số...)
        self.su_kien = []
        self.thong_ke = []           # (t, tần số, % mất, trễ ms) mỗi giây
        self.bang = False
        self.man_hinh = "chao"      # chao | bai | ket_qua_bai | tong_ket | can_truc
        self.t_man = 0.0
        self.can_truc = None
        self.thu_muc_ra = thu_muc_ra
        self.chuot_khoa = False
        self.lan_tk = 0.0
        self.chay = True
        self.s = None

    # ------------------------------------------------------------------ vòng chính
    def bat_dau_bai(self, lop):
        self.canh.dat_lai()
        self.cu_chi = CuChi()
        self.ct.can_lai()
        self.bai = lop()
        self.bai.bat_dau(self.canh, self.t)
        self.man_hinh = "bai"
        self.su_kien.append((round(self.t, 3), "bat_dau_bai", self.bai.ten))

    def lam_tat_ca(self):
        self.ket_qua = []
        self.hang_doi = list(bai_thu.TAT_CA)
        self.bat_dau_bai(self.hang_doi.pop(0))

    def buoc(self, dt, su_kien_pg=()):
        """Một khung hình. su_kien_pg: sự kiện pygame (bỏ trống khi bot chạy)."""
        self.t += dt
        phim = set()
        chuot = (0, 0)
        nut = (False, False)
        for e in su_kien_pg:
            if e.type == pygame.QUIT:
                self.chay = False
            elif e.type == pygame.KEYDOWN:
                self._phim(e)
        if isinstance(self.nguon, NguonAo) and self.bot is None:
            k = pygame.key.get_pressed()
            for ch in "qeswadkjlf":
                if k[getattr(pygame, "K_" + ch)]:
                    phim.add(ch)
            if k[pygame.K_SPACE]:
                phim.add(" ")
            chuot = pygame.mouse.get_rel() if self.chuot_khoa else (0, 0)
            b = pygame.mouse.get_pressed()
            nut = (b[0], b[2])
            self.nguon.cap_nhat(dt, chuot[0], chuot[1], phim, nut)
        elif self.bot is not None:
            self.bot.buoc(self, dt)

        s = self.sua_truc.ap(self.nguon.lay())
        self.s = s
        if self.man_hinh == "can_truc":
            self._buoc_can_truc(s)
        joy = joystick(*s.joy)
        self.ct.cap_nhat(s, dt)
        ev = self.cu_chi.cap_nhat(s, self.ct, self.t, dt)
        ev_canh = []
        if self.man_hinh == "bai":
            ev_canh = self.canh.tuong_tac.cap_nhat(s, joy, self.ct, self.cu_chi, ev, self.t, dt)
            self.bai.cap_nhat(self.canh, self.ct, ev_canh, ev, self.t, dt)
            if self.bai.het_gio(self.t) and not self.bai.xong:
                self.su_kien.append((round(self.t, 3), "het_gio", self.bai.ten))
                self.bai.ket_thuc(self.canh, self.t)
            if self.bai.xong:
                for cs in self.bai.chi_so:
                    self.ket_qua.append((self.bai.ma, self.bai.ten) + cs)
                self.man_hinh = "ket_qua_bai"
                self.t_man = self.t
        elif self.man_hinh == "ket_qua_bai" and (self.t - self.t_man > 3.0 or "vuot_AB" in ev):
            if self.hang_doi:
                self.bat_dau_bai(self.hang_doi.pop(0))
            else:
                self.man_hinh = "tong_ket"
                self.xuat()
        for x in ev:
            self.su_kien.append((round(self.t, 3), x, ""))
        for loai, gt in ev_canh:
            if loai not in ("rot", "lac"):
                self.su_kien.append((round(self.t, 3), loai, str(gt)))
        if self.t - self.lan_tk >= 1.0:
            self.lan_tk = self.t
            self.thong_ke.append((round(self.t, 1), round(self.nguon.tan_so, 1), round(self.nguon.ty_le_mat, 2),
                                  round(min(self.nguon.tre_ms(), 9999), 1), round(self.ct.run_tia(), 3)))
        self.ve_khung()

    def _phim(self, e):
        k = e.key
        if k == pygame.K_ESCAPE:
            if self.chuot_khoa:
                self._khoa_chuot(False)
            else:
                self.chay = False
        elif k == pygame.K_RETURN:
            self.lam_tat_ca()
        elif pygame.K_1 <= k <= pygame.K_7:
            self.hang_doi = []
            self.ket_qua = []
            self.bat_dau_bai(bai_thu.TAT_CA[k - pygame.K_1])
        elif k == pygame.K_TAB:
            self.bang = not self.bang
        elif k == pygame.K_r:
            self.ct.can_lai()
        elif k == pygame.K_p:
            self.ct.kieu = "tuyet_doi" if self.ct.kieu == "van_toc" else "van_toc"
            self.ct.can_lai()
        elif k == pygame.K_F5:
            self.ct.loc = not self.ct.loc
        elif k == pygame.K_n and isinstance(self.nguon, NguonAo):
            self.nguon.rung = not self.nguon.rung
        elif k == pygame.K_k and not isinstance(self.nguon, NguonAo):
            self.man_hinh = "can_truc"
            self.can_truc = {"buoc": 1, "mau": [], "t": self.t}
        elif k == pygame.K_m and isinstance(self.nguon, NguonAo):
            self._khoa_chuot(not self.chuot_khoa)

    def _khoa_chuot(self, bat):
        self.chuot_khoa = bat
        pygame.event.set_grab(bat)
        pygame.mouse.set_visible(not bat)
        pygame.mouse.get_rel()

    def _buoc_can_truc(self, s):
        ct = self.can_truc
        ct["mau"].append(s.acc)
        if len(ct["mau"]) > 60:
            ct["mau"].pop(0)
        a = np.array(ct["mau"])
        yen = len(a) >= 60 and np.linalg.norm(a.std(0)) < 0.01
        if yen and self.t - ct["t"] > 2.0:
            if ct["buoc"] == 1:
                self.sua_truc.buoc_nam_ngang(a.mean(0))
                ct.update(buoc=2, mau=[], t=self.t)
            else:
                goc = self.sua_truc.buoc_chuc_mui(a.mean(0))
                if goc >= 20:
                    ct["xong"] = goc
                    self.su_kien.append((round(self.t, 3), "can_truc_gan", f"{goc:.0f}"))
                    self.man_hinh = "chao"
                    self.ct.can_lai()

    # ------------------------------------------------------------------ vẽ
    def ve_khung(self):
        m = self.man
        m.fill(NEN)
        c = self.canh
        self.cam.vi_tri = c.cam_vi_tri
        self.cam.yaw = c.cam_yaw
        for luoi, R, T in self.phong:
            self.ve.them(luoi, R, T)
        for v in c.vat:
            R = v.R()
            for luoi, dy in zip(v.luoi, v.luoi_dinh):
                self.ve.them(luoi, R, v.p + R @ np.array([0, dy, 0]))
            if v.suc_chua > 0 and v.the_tich > 0.05:
                k = min(1.0, v.the_tich / v.suc_chua)
                hh = v.h * k * (0.62 if v.loai == "binh" else 0.97)
                self.ve.them(tru(v.r * 0.9, max(hh, 0.002), v.mau_dd, n=12), R, v.p + R @ np.array([0, 0.002, 0]))
            if v.chat_ran > 0:
                self.ve.them(tru(v.r * 0.85, 0.012 + 0.02 * v.chat_ran, (245, 245, 240), n=10), R, v.p + R @ np.array([0, 0.002, 0]))
            if v.nap is not None and not v.mo_nap:
                Rn = R @ ma_tran_yaw(-v.nap)
                self.ve.them(hop(0.03, 0.028, 0.03, (210, 70, 60), day=True), Rn, v.p + R @ np.array([0, v.h, 0]), vien=(120, 30, 30))
        self._tay(c)
        self.ve.ve(m)
        self._dau_tren_ban(c)
        if self.man_hinh in ("bai", "ket_qua_bai") or self.man_hinh == "chao":
            self._tia(c)
        self._hud()
        if self.canh.tuong_tac.menu_mo:
            self._menu_tron()
        if self.man_hinh == "chao":
            self._man_chao()
        elif self.man_hinh == "ket_qua_bai":
            self._bang_ket_qua(self.bai.ten, self.bai.chi_so, nho=True)
        elif self.man_hinh == "tong_ket":
            self._tong_ket()
        elif self.man_hinh == "can_truc":
            self._man_can_truc()
        if self.bang:
            self._bang_so_lieu()
        if isinstance(self.man, pygame.Surface) and pygame.display.get_surface() is self.man:
            pygame.display.flip()

    def _tay(self, c):
        """Mô hình tay cầm nhỏ ở gốc tia, xoay đúng theo hướng tay cầm (để nhìn thấy lăn cổ tay)."""
        s = self.s
        if s is None:
            return
        o = c.tay()
        R = np.stack([dat_sang_tg(quay(s.q, e)) for e in ((0, -1, 0), (0, 0, 1), (1, 0, 0))], 1)
        # bỏ phần yaw tuyệt đối của tay cầm, dùng yaw tia để mô hình luôn chỉ theo tia
        f = c.huong_tia(*self.ct.ra)
        g = math.atan2(-R[0, 2], R[2, 2]) - math.atan2(-f[0], f[2])
        R = ma_tran_yaw(math.degrees(-g)) @ R if np.isfinite(g) else R
        self.ve.them(hop(0.035, 0.03, 0.12, (240, 244, 246)), R, o)
        self.ve.them(hop(0.02, 0.012, 0.02, (40, 40, 48)), R, o + R @ np.array([0, 0.02, 0.02]))

    def _dau_tren_ban(self, c):
        m = self.man
        for x, z, r, ten in c.o_dat:
            pts = [self.ve.diem((x + r * math.cos(a), 0.901, z + r * math.sin(a))) for a in np.linspace(0, 2 * math.pi, 20)]
            if all(pts):
                pygame.draw.polygon(m, VANG, pts, 2)
        if c.moc_san:
            x, z = c.moc_san
            pts = [self.ve.diem((x + 0.35 * math.cos(a), 0.01, z + 0.35 * math.sin(a))) for a in np.linspace(0, 2 * math.pi, 28)]
            if all(pts):
                pygame.draw.polygon(m, XANH, pts, 3)
        for i, (x, y, r) in enumerate(c.bia):
            p = self.ve.diem((x, y, c.mat_tuong_z - 0.04))
            if p:
                _, z = self.cam.chieu(np.array([[x, y, c.mat_tuong_z]]))
                rr = max(3, int(self.cam.f * r / max(z[0], 0.1)))
                sang = i == c.bia_sang
                pygame.draw.circle(m, NHAN if sang else (70, 90, 100), p, rr, 0 if sang else 2)
                pygame.draw.circle(m, CHU, p, max(2, rr // 4))
        for dau, cuoi, q in c.tuong_tac.dong_chay:
            if q < 2.5:  # nhỏ giọt
                for k in range(4):
                    a = (self.t * 3 + k / 4) % 1.0
                    p = self.ve.diem(dau + (cuoi - dau) * a)
                    if p:
                        pygame.draw.circle(m, (120, 170, 240), p, 3)
            else:
                self.ve.duong(m, dau, cuoi, (110, 160, 240), max(2, int(q / 5)))

    def _tia(self, c):
        m = self.man
        tt = c.tuong_tac
        o = c.tay()
        d = c.huong_tia(*self.ct.ra)
        if tt.diem_tro is None:
            return
        mau = VANG if tt.tinh else (NHAN if tt.tro else (140, 160, 170))
        self.ve.duong(m, o, tt.diem_tro, mau, 2)
        p = self.ve.diem(tt.diem_tro)
        if p:
            pygame.draw.circle(m, mau, p, 6, 2)
            if tt.tro and tt.cam is None:
                _, v, phan = tt.tro
                chu = f"{v.ten}" + (" · nắp: giữ B + xoay cổ tay" if phan == "nap" else " · giữ B để cầm")
                self._chu(chu, (p[0] + 12, p[1] - 22), self.f["nho"], CHU, nen=True)
            if tt.goi_y:
                self._chu(tt.goi_y, (p[0] + 12, p[1] + 10), self.f["nho"], VANG, nen=True)
        del d

    def _chu(self, chu, xy, f, mau=CHU, nen=False, giua=False):
        anh = f.render(chu, True, mau)
        r = anh.get_rect()
        if giua:
            r.center = xy
        else:
            r.topleft = xy
        if nen:
            khung = r.inflate(10, 6)
            s = pygame.Surface(khung.size, pygame.SRCALPHA)
            s.fill((10, 16, 20, 190))
            self.man.blit(s, khung)
        self.man.blit(anh, r)
        return r

    def _khung(self, rect, alpha=200):
        s = pygame.Surface(rect.size, pygame.SRCALPHA)
        pygame.draw.rect(s, (12, 18, 24, alpha), s.get_rect(), border_radius=10)
        pygame.draw.rect(s, (56, 214, 196, 90), s.get_rect(), 1, border_radius=10)
        self.man.blit(s, rect)

    def _hud(self):
        s = self.s
        if s is None:
            return
        m = self.man
        n = self.nguon
        tt = self.canh.tuong_tac
        co_hall = isinstance(n, NguonThuCamBien)
        r = pygame.Rect(12, 12, 300, 266 if co_hall else 236)
        self._khung(r)
        y = 20
        self._chu(n.ten, (24, y), self.f["dam"], NHAN)
        y += 26
        tre = n.tre_ms()
        mau_tre = XANH if tre < 30 else (VANG if tre < 80 else DO)
        self._chu(f"{n.tan_so:5.0f} gói/s · mất {n.ty_le_mat:4.1f}% · trễ {min(tre, 999):3.0f} ms", (24, y), self.f["nho"], mau_tre)
        y += 22
        self._chu(f"Chế độ {'ĐI' if tt.che_do == 'DI' else 'TAY'} · tia {'vận tốc' if self.ct.kieu == 'van_toc' else 'tuyệt đối'}"
                  f"{' · TINH ×0,25' if tt.tinh else ''}", (24, y), self.f["nho"], VANG if tt.tinh else CHU)
        y += 24
        for ten, muc, cham in (("A", s.muc_a, s.cham_a), ("B", s.muc_b, s.cham_b)):
            self._chu(ten, (24, y - 2), self.f["dam"], CHU)
            khung = pygame.Rect(44, y, 150, 12)
            pygame.draw.rect(m, (40, 52, 60), khung, border_radius=4)
            pygame.draw.rect(m, NHAN if cham else (110, 140, 150), (44, y, int(150 * muc / 255), 12), border_radius=4)
            for nguong in (38, 89, 128):
                xx = 44 + int(150 * nguong / 255)
                pygame.draw.line(m, (200, 210, 215), (xx, y - 2), (xx, y + 13), 1)
            self._chu("CHẠM" if cham else ("lơ lửng" if muc >= 38 else ""), (200, y - 3), self.f["nho"], NHAN if cham else NHAT)
            y += 22
        # joystick
        cx, cy = 60, y + 30
        pygame.draw.circle(m, (40, 52, 60), (cx, cy), 26)
        pygame.draw.circle(m, (70, 90, 100), (cx, cy), int(26 * 0.08), 1)
        pygame.draw.circle(m, VANG if s.nhan_can else NHAN, (cx + int(s.joy[0] * 24), cy - int(s.joy[1] * 24)), 6)
        self._chu(f"J {s.joy[0]:+.2f} {s.joy[1]:+.2f}", (cx - 30, cy + 30), self.f["nho"], NHAT)
        # thước roll
        rx, ry = 200, y + 30
        pygame.draw.circle(m, (40, 52, 60), (rx, ry), 28, 2)
        for g in (-cm.GOC_BAT_DAU_ROT, cm.GOC_BAT_DAU_ROT):
            a = math.radians(g - 90)
            pygame.draw.line(m, VANG, (rx, ry), (rx + 28 * math.cos(a), ry + 28 * math.sin(a)), 1)
        a = math.radians(self.ct.roll - 90)
        pygame.draw.line(m, NHAN, (rx, ry), (rx + 26 * math.cos(a), ry + 26 * math.sin(a)), 3)
        self._chu(f"lăn {self.ct.roll:+4.0f}°", (rx - 28, ry + 30), self.f["nho"], NHAT)
        # cò Hall (chỉ sketch thử cảm biến có)
        if co_hall:
            yc = ry + 56
            self._chu("Cò", (24, yc - 3), self.f["dam"], CHU)
            pygame.draw.rect(m, (40, 52, 60), (60, yc, 150, 12), border_radius=4)
            pygame.draw.rect(m, VANG if s.bop > 0 else (110, 140, 150), (60, yc, int(150 * s.bop), 12), border_radius=4)
            self._chu(f"{s.bop * 100:3.0f}% · {s.hall_mv} mV", (216, yc - 3), self.f["nho"], NHAT)
        # bài đang làm
        if self.bai and self.man_hinh in ("bai", "ket_qua_bai"):
            r = pygame.Rect(W - 452, 12, 440, 40 + 22 * len(self.bai.huong_dan) + 34)
            self._khung(r)
            self._chu(f"Bài {self.bai.ma}/7 · {self.bai.ten}", (r.x + 14, r.y + 10), self.f["dam"], NHAN)
            yy = r.y + 38
            for dong in self.bai.huong_dan:
                self._chu("• " + dong, (r.x + 14, yy), self.f["nho"], CHU)
                yy += 22
            self._chu(self.bai.buoc, (r.x + 14, yy + 6), self.f["vua"], VANG)
            con = self.bai.gioi_han_s - (self.t - self.bai.t0)
            self._chu(f"{con:3.0f} s", (r.right - 54, r.y + 10), self.f["nho"], NHAT)
        # thông báo
        yy = H - 40
        for t, chu in reversed(list(tt.thong_bao)):
            if self.t - t < 2.5:
                self._chu(chu, (W // 2, yy), self.f["vua"], CHU, nen=True, giua=True)
                yy -= 32

    def _menu_tron(self):
        m = self.man
        tt = self.canh.tuong_tac
        cx, cy, R = W // 2, H // 2, 170
        s = pygame.Surface((W, H), pygame.SRCALPHA)
        pygame.draw.circle(s, (8, 12, 16, 200), (cx, cy), R + 30)
        m.blit(s, (0, 0))
        n = len(cm.HOA_CHAT_MENU)
        for i, ten in enumerate(cm.HOA_CHAT_MENU):
            a = math.radians(i * 360 / n - 90)
            p = (cx + R * 0.72 * math.cos(a), cy + R * 0.72 * math.sin(a))
            chon = i == tt.menu_chon
            pygame.draw.circle(m, NHAN if chon else (50, 66, 76), p, 46)
            self._chu(ten, p, self.f["nho"], NEN if chon else CHU, giua=True)
        self._chu("gạt cần chọn · nhả A", (cx, cy), self.f["vua"], NHAT, giua=True)

    def _man_chao(self):
        r = pygame.Rect(W // 2 - 360, 150, 720, 420)
        self._khung(r, 225)
        self._chu("Phòng thử tay cầm VERA", (r.centerx, r.y + 40), self.f["tieu_de"], NHAN, giua=True)
        dong = ["Thử mọi tương tác của tay cầm trước khi đưa vào Unity.",
                "", "Enter: làm lần lượt 7 bài, cuối cùng ra bảng khả thi / cần chỉnh",
                "1–7: làm một bài   ·   Tab: số liệu tay cầm   ·   R: căn lại tia",
                "K: căn trục gắn BMI160 (tay cầm thật)   ·   P: đổi kiểu tia   ·   Esc: thoát"]
        if isinstance(self.nguon, NguonAo):
            dong += ["", "Tay cầm ảo: M khoá chuột rồi xoay chuột · Q/E lăn cổ tay · WASD cần gạt",
                     "chuột trái / K = B (cầm) · chuột phải / J = A · Space nhấn cần · giữ L lắc · giữ F lơ lửng"]
        elif np.allclose(self.sua_truc.M, np.eye(3)):
            dong += ["", "CHƯA căn trục gắn BMI160: bấm K (đặt nằm ngang, rồi chúc mũi xuống 45°)"]
        y = r.y + 90
        for d in dong:
            self._chu(d, (r.centerx, y), self.f["vua"], VANG if "CHƯA" in d else CHU, giua=True)
            y += 30

    def _man_can_truc(self):
        r = pygame.Rect(W // 2 - 330, 200, 660, 220)
        self._khung(r, 230)
        b = self.can_truc["buoc"]
        self._chu("Căn trục gắn BMI160", (r.centerx, r.y + 36), self.f["tieu_de"], NHAN, giua=True)
        chu = ("Bước 1/2: đặt tay cầm NẰM NGANG trên bàn, mũi hướng ra trước, để yên" if b == 1 else
               "Bước 2/2: CHÚC MŨI tay cầm xuống khoảng 45° và giữ yên")
        self._chu(chu, (r.centerx, r.y + 100), self.f["vua"], CHU, giua=True)
        self._chu("tự ghi khi yên 2 giây", (r.centerx, r.y + 140), self.f["nho"], NHAT, giua=True)

    def _bang_ket_qua(self, ten, chi_so, nho=False):
        r = pygame.Rect(W // 2 - 400, 170, 800, 80 + 34 * len(chi_so))
        self._khung(r, 235)
        dat = all(c[4] for c in chi_so)
        self._chu(f"{ten}: {'ĐẠT' if dat else 'CẦN CHỈNH'}", (r.x + 20, r.y + 16), self.f["to"], XANH if dat else VANG)
        y = r.y + 60
        for cs in chi_so:
            ten_cs, gt, dv, tc, d = cs
            self._chu(ten_cs, (r.x + 20, y), self.f["vua"], CHU)
            self._chu(f"{gt} {dv}", (r.x + 420, y), self.f["dam"], CHU)
            self._chu(tc, (r.x + 560, y), self.f["nho"], NHAT)
            self._chu("✓" if d else "✗", (r.right - 30, y), self.f["dam"], XANH if d else DO)
            y += 34
        if nho:
            self._chu("vuốt A → B hoặc chờ 3 giây để sang bài sau", (r.centerx, r.bottom - 18), self.f["nho"], NHAT, giua=True)

    def _tong_ket(self):
        r = pygame.Rect(40, 40, W - 80, H - 80)
        self._khung(r, 240)
        dat = sum(1 for k in self.ket_qua if k[6])
        self._chu(f"Tổng kết: {dat}/{len(self.ket_qua)} chỉ số đạt · "
                  f"{'KHẢ THI' if dat == len(self.ket_qua) else 'CẦN CHỈNH các dòng đỏ'}", (r.x + 20, r.y + 14), self.f["to"],
                  XANH if dat == len(self.ket_qua) else VANG)
        y = r.y + 56
        for ma, ten_bai, ten, gt, dv, tc, d in self.ket_qua:
            self._chu(f"{ma}. {ten_bai}", (r.x + 20, y), self.f["nho"], NHAT)
            self._chu(ten, (r.x + 230, y), self.f["nho"], CHU)
            self._chu(f"{gt} {dv}", (r.x + 590, y), self.f["nho"], CHU)
            self._chu(tc, (r.x + 730, y), self.f["nho"], NHAT)
            self._chu("đạt" if d else "chưa", (r.right - 60, y), self.f["nho"], XANH if d else DO)
            y += 21
        try:
            noi = os.path.relpath(self.thu_muc_ra, GOC)
        except ValueError:  # khác ổ đĩa
            noi = self.thu_muc_ra
        self._chu(f"Đã lưu bảng vào {noi}", (r.x + 20, r.bottom - 28), self.f["nho"], NHAN)

    def _bang_so_lieu(self):
        s = self.s
        r = pygame.Rect(12, H - 210, 420, 198)
        self._khung(r, 230)
        q = s.q
        dong = [f"q  {q[0]:+.3f} {q[1]:+.3f} {q[2]:+.3f} {q[3]:+.3f}",
                f"ω  {s.gyro[0]:+7.1f} {s.gyro[1]:+7.1f} {s.gyro[2]:+7.1f} °/s",
                f"a  {s.acc[0]:+.2f} {s.acc[1]:+.2f} {s.acc[2]:+.2f} g   |a| {math.sqrt(sum(v * v for v in s.acc)):.2f}",
                f"tia yaw {self.ct.ra[0]:+6.1f}°  pitch {self.ct.ra[1]:+6.1f}°  lăn {self.ct.roll:+6.1f}°",
                f"rung tia {self.ct.run_tia():.3f}° · lọc {'bật' if self.ct.loc else 'tắt'} · {s.nhiet:.1f} °C"
                f"{' · yên' if s.yen else ''}{' · LỖI IMU' if s.loi_imu else ''}",
                f"lắc {self.cu_chi.nang_luong_lac:.2f} g · khuấy {self.cu_chi.vong} vòng"]
        y = r.y + 12
        for d in dong:
            self._chu(d, (r.x + 14, y), self.f["nho"], CHU)
            y += 30

    # ------------------------------------------------------------------ xuất bảng
    def xuat(self):
        os.makedirs(self.thu_muc_ra, exist_ok=True)
        dau = ["Bài", "Tên bài", "Chỉ số", "Giá trị", "Đơn vị", "Tiêu chí", "Đạt"]
        hang = [list(k[:6]) + ["Đạt" if k[6] else "Chưa đạt"] for k in self.ket_qua]
        with open(os.path.join(self.thu_muc_ra, "ket_qua.csv"), "w", newline="", encoding="utf-8-sig") as f:
            w = csv.writer(f)
            w.writerow(dau)
            w.writerows(hang)
        with open(os.path.join(self.thu_muc_ra, "su_kien.csv"), "w", newline="", encoding="utf-8-sig") as f:
            w = csv.writer(f)
            w.writerow(["t (s)", "sự kiện", "chi tiết"])
            w.writerows(self.su_kien)
        dau_tk = ["t (s)", "gói/s", "mất (%)", "trễ gói cuối (ms)", "rung tia (°)"]
        with open(os.path.join(self.thu_muc_ra, "thong_ke_nguon.csv"), "w", newline="", encoding="utf-8-sig") as f:
            w = csv.writer(f)
            w.writerow(dau_tk)
            w.writerows(self.thong_ke)
        # bảng theo bài (đạt / tổng) để vẽ
        bai = []
        for ma, ten, *_ in self.ket_qua:
            if (ma, ten) not in bai:
                bai.append((ma, ten))
        tong = [(ma, ten, sum(1 for k in self.ket_qua if k[0] == ma and k[6]), sum(1 for k in self.ket_qua if k[0] == ma))
                for ma, ten in bai]
        try:
            import matplotlib
            matplotlib.use("Agg")
            import matplotlib.pyplot as plt
            fig = plt.figure(figsize=(14, 3 + 0.36 * len(hang)))
            gs = fig.add_gridspec(1, 2, width_ratios=[3.2, 1])
            ax = fig.add_subplot(gs[0])
            ax.axis("off")
            tb = ax.table(cellText=[h[1:] for h in hang], colLabels=dau[1:], loc="upper center", cellLoc="left",
                          colWidths=[0.17, 0.3, 0.1, 0.07, 0.22, 0.09])
            tb.auto_set_font_size(False)
            tb.set_fontsize(8.5)
            tb.scale(1, 1.35)
            for (i, j), o in tb.get_celld().items():
                if i == 0:
                    o.set_facecolor("#0e7c86")
                    o.set_text_props(color="white", weight="bold")
                elif j == 5:
                    o.set_facecolor("#dcfce7" if hang[i - 1][6] == "Đạt" else "#fee2e2")
            ax.set_title(f"Phòng thử tay cầm VERA · nguồn: {self.nguon.ten} · {time.strftime('%d/%m/%Y %H:%M')}", fontsize=11)
            ax2 = fig.add_subplot(gs[1])
            ten_b = [f"{ma}. {ten}" for ma, ten, _, _ in tong][::-1]
            ax2.barh(ten_b, [d / n * 100 for _, _, d, n in tong][::-1],
                     color=["#16a34a" if d == n else "#d97706" for _, _, d, n in tong][::-1])
            ax2.set_xlim(0, 100)
            ax2.set_xlabel("% chỉ số đạt")
            ax2.tick_params(axis="y", labelsize=8)
            fig.tight_layout()
            fig.savefig(os.path.join(self.thu_muc_ra, "tong_ket.png"), dpi=130)
            plt.close(fig)
        except ImportError:
            pass
        try:
            from openpyxl import Workbook
            from openpyxl.styles import Font, PatternFill
            wb = Workbook()
            for i, (ten_trang, d, hh) in enumerate((("Kết quả", dau, hang), ("Theo bài", ["Bài", "Tên bài", "Đạt", "Tổng"], tong),
                                                    ("Nguồn", dau_tk, self.thong_ke), ("Sự kiện", ["t (s)", "sự kiện", "chi tiết"], self.su_kien))):
                ws = wb.active if i == 0 else wb.create_sheet()
                ws.title = ten_trang
                ws.append(d)
                for h in hh:
                    ws.append(list(h))
                for o in ws[1]:
                    o.font = Font(bold=True, color="FFFFFF")
                    o.fill = PatternFill("solid", fgColor="0E7C86")
                for cot in ws.columns:
                    ws.column_dimensions[cot[0].column_letter].width = min(46, max(8, max(len(str(o.value or "")) for o in cot) + 2))
                if ten_trang == "Kết quả":
                    for row in ws.iter_rows(min_row=2):
                        row[6].fill = PatternFill("solid", fgColor="DCFCE7" if row[6].value == "Đạt" else "FEE2E2")
            wb.save(os.path.join(self.thu_muc_ra, "tong_ket.xlsx"))
        except ImportError:
            pass
        self.su_kien.append((round(self.t, 3), "xuat", self.thu_muc_ra))

    def chup(self, ten):
        os.makedirs(self.thu_muc_ra, exist_ok=True)
        pygame.image.save(self.man, os.path.join(self.thu_muc_ra, ten))


def main():
    p = argparse.ArgumentParser(description="Phòng thử tay cầm VERA")
    p.add_argument("--com")
    p.add_argument("--thu", metavar="COM", help="tay cầm đang nạp sketch firmware/thu_cam_bien (có cả cò Hall)")
    p.add_argument("--dao-x", action="store_true", help="đảo chiều trục X của cần gạt (dùng với --thu)")
    p.add_argument("--dao-y", action="store_true", help="đảo chiều trục Y của cần gạt (dùng với --thu)")
    p.add_argument("--doi-xy", action="store_true", help="hoán đổi X và Y của cần gạt (dùng với --thu)")
    p.add_argument("--wifi", action="store_true")
    p.add_argument("--phat")
    p.add_argument("--toan-man", action="store_true", help="chạy toàn màn hình")
    args = p.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")
    if args.thu:
        nguon = NguonThuCamBien(args.thu, args.dao_x, args.dao_y, args.doi_xy)
    elif args.com:
        nguon = NguonUSB(args.com)
    elif args.wifi:
        nguon = NguonWifi()
    elif args.phat:
        nguon = NguonPhatLai(args.phat)
    else:
        nguon = NguonAo()
    ra = os.path.join(GOC, "du_lieu", "phong_thu", time.strftime("phien_%Y%m%d_%H%M%S"))
    app = UngDung(nguon, ra, toan_man=args.toan_man)
    dong_ho = pygame.time.Clock()
    while app.chay:
        dt = dong_ho.tick(60) / 1000
        app.buoc(min(dt, 0.05), pygame.event.get())
    if app.ket_qua and app.man_hinh != "tong_ket":
        app.xuat()
    if isinstance(nguon, NguonUSB):
        nguon.gui("r0\n")
    nguon.dung()
    pygame.quit()
    if app.ket_qua:
        print(f"Đã lưu kết quả vào {ra}")


if __name__ == "__main__":
    main()
