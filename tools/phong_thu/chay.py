"""Phòng thử tay cầm VERA: môi trường 3D chạy riêng (không cần Unity) để thử BMI160, joystick và 2 nút cảm ứng
bằng các thao tác thí nghiệm thật: trỏ menu, lấy chất, nhỏ pipet, rót chất (cò Hall không dùng ở đây).

Chạy (từ thư mục dự án; thêm --toan-man để chạy toàn màn hình, F11 đổi qua lại):
    python tools/phong_thu/chay.py                     tay cầm ảo (chuột + bàn phím)
    python tools/phong_thu/chay.py --thu COM8          tay cầm thật, sketch firmware/thu_cam_bien (dòng D,... 100 Hz)
    python tools/phong_thu/chay.py --com COM8          tay cầm thật, firmware tay_cam_vera qua USB (gói thô r2)
    python tools/phong_thu/chay.py --wifi              tay cầm thật qua Wi-Fi (gói v3, cổng 4210)
    python tools/phong_thu/chay.py --phat tep.vera     phát lại một lần ghi
    python tools/phong_thu/tu_dong.py                  bot tự làm cả 7 bài (kiểm tra luật, chụp ảnh)

Phím: Enter làm bộ chính (bài 1–5) · 1–7 làm một bài · Esc dừng bài / thoát · Tab bảng số liệu · R căn lại tia
      K căn trục gắn BMI160 (tay cầm thật) · P đổi kiểu tia · F5 bật/tắt lọc One Euro · N bật/tắt rung tay giả lập
Tay cầm ảo: M khoá chuột rồi xoay chuột, Q/E lăn cổ tay, W A S D cần gạt, chuột trái / K = B, chuột phải / J = A,
            Space nhấn cần, giữ L lắc, giữ F ngón cái lơ lửng trên A.
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
import mau as M  # noqa: E402
from nguon import NguonAo, NguonPhatLai, NguonThuCamBien, NguonUSB, NguonWifi, SuaTrucGan  # noqa: E402
from ve3d import BoVe, Camera, gop, hop, ma_tran_yaw, tam_dung, tron_mau, tru, ve_elip_mo  # noqa: E402
from xu_ly import ConTro, CuChi, dat_sang_tg, joystick, quay  # noqa: E402

GOC = os.path.dirname(os.path.dirname(DAY))
TEP_TRUC = os.path.join(GOC, "du_lieu", "tay_cam", "truc_gan.json")
W, H = 1280, 720
BEN = 296                       # bề rộng bảng cảm biến bên trái
NEN, CHU, NHAT, NHAN, DO, VANG, XANH = M.NEN, M.CHU, M.NHAT, M.NHAN, M.DO, M.VANG, M.XANH
CHO_KET_QUA = 6.0               # s hiện bảng kết quả một bài rồi tự sang bài sau


def phong_chu(co, dam=False):
    for ten in ("Segoe UI", "Arial", "DejaVu Sans"):
        f = pygame.font.SysFont(ten, co, bold=dam)
        if f:
            return f
    return pygame.font.Font(None, co)


class UngDung:
    def __init__(self, nguon, thu_muc_ra, bot=None, hien=True, toan_man=False):
        pygame.init()
        if hien:
            co = (pygame.FULLSCREEN | pygame.SCALED) if toan_man else (pygame.SCALED | pygame.RESIZABLE)
            self.man = pygame.display.set_mode((W, H), co)
            pygame.display.set_caption("VERA Lab · Phòng thử tay cầm")
        else:
            self.man = pygame.Surface((W, H))
        self.hien = hien
        self.f = {k: phong_chu(v) for k, v in (("rat_nho", 13), ("nho", 15), ("vua", 18), ("to", 24))}
        self.f["dam_nho"] = phong_chu(15, True)
        self.f["dam"] = phong_chu(18, True)
        self.f["tieu_de"] = phong_chu(30, True)
        self._chu_cache = {}
        self.nguon = nguon
        self.bot = bot
        self.sua_truc = SuaTrucGan(TEP_TRUC if not isinstance(nguon, NguonAo) else None)
        self.cam = Camera(W, H, cx=BEN + (W - BEN) / 2)
        self.cam.pitch = -24
        self.ve = BoVe(self.cam)
        self.phong = [(gop(cm.luoi_phong()), None, (0, 0, 0))]   # cả phòng tĩnh: một lưới
        self.nen = self._tao_nen()
        self.canh = cm.Canh()
        self.ct = ConTro()
        self.cu_chi = CuChi()
        self.t = 0.0
        self.bai = None
        self.hang_doi = []
        self.so_bai_hang = 0
        self.ket_qua = []           # (mã bài, tên bài, tên chỉ số, giá trị, đơn vị, tiêu chí, đạt, cảm biến)
        self.su_kien = []
        self.thong_ke = []          # (t, tần số, % mất, trễ ms, rung tia) mỗi giây
        self.bang = False
        self.man_hinh = "chao"      # chao | bai | ket_qua_bai | tong_ket | can_truc
        self.t_man = 0.0
        self.can_truc = None
        self.thu_muc_ra = thu_muc_ra
        self.chuot_khoa = False
        self.lan_tk = 0.0
        self.chay = True
        self.s = None
        self.tam_canh = (int(BEN + (W - BEN) / 2), H // 2)

    # ------------------------------------------------------------------ vòng chính
    def bat_dau_bai(self, lop):
        self.canh.dat_lai()
        self.cu_chi = CuChi()
        self.ct.can_lai()
        self.bai = lop()
        self.bai.bat_dau(self.canh, self.t)
        self.man_hinh = "bai"
        self.su_kien.append((round(self.t, 3), "bat_dau_bai", self.bai.ten))
        if self.hien and self.bot is None and isinstance(self.nguon, NguonAo):
            self._khoa_chuot(True)

    def lam_bo(self, ds):
        self.ket_qua = []
        self.hang_doi = list(ds)
        self.so_bai_hang = len(ds)
        self.bat_dau_bai(self.hang_doi.pop(0))

    def lam_tat_ca(self):
        self.lam_bo(bai_thu.TAT_CA)

    def _sang_bai_sau(self):
        if self.hang_doi:
            self.bat_dau_bai(self.hang_doi.pop(0))
        else:
            self.man_hinh = "tong_ket"
            self._khoa_chuot(False)
            self.xuat()

    def buoc(self, dt, su_kien_pg=()):
        """Một khung hình. su_kien_pg: sự kiện pygame (bỏ trống khi bot chạy)."""
        self.t += dt
        for e in su_kien_pg:
            if e.type == pygame.QUIT:
                self.chay = False
            elif e.type == pygame.KEYDOWN:
                self._phim(e)
            elif e.type == pygame.MOUSEBUTTONDOWN and isinstance(self.nguon, NguonAo) and not self.chuot_khoa \
                    and self.man_hinh == "bai":
                self._khoa_chuot(True)
        if isinstance(self.nguon, NguonAo) and self.bot is None:
            k = pygame.key.get_pressed()
            phim = {ch for ch in "qeswadkjlf" if k[getattr(pygame, "K_" + ch)]}
            if k[pygame.K_SPACE]:
                phim.add(" ")
            chuot = pygame.mouse.get_rel() if self.chuot_khoa else (0, 0)
            b = pygame.mouse.get_pressed()
            nut = (b[0], b[2]) if self.chuot_khoa else (False, False)
            self.nguon.cap_nhat(dt, chuot[0], chuot[1], phim, nut)
        elif self.bot is not None:
            self.bot.buoc(self, dt)

        s = self.sua_truc.ap(self.nguon.lay())
        self.s = s
        self.canh.s = s
        if self.man_hinh == "can_truc":
            self._buoc_can_truc(s)
        joy = joystick(*s.joy)
        self.ct.cap_nhat(s, dt)
        ev = self.cu_chi.cap_nhat(s, self.ct, self.t, dt)
        ev_canh = []
        if self.man_hinh == "bai":
            ev_canh = self.canh.tuong_tac.cap_nhat(s, joy, self.ct, self.cu_chi, ev, self.t, dt)
            self.bai.cap_nhat(self.canh, s, self.ct, self.cu_chi, ev_canh, ev, self.t, dt)
            if self.bai.het_gio(self.t) and not self.bai.xong:
                self.su_kien.append((round(self.t, 3), "het_gio", self.bai.ten))
                self.bai.ket_thuc(self.canh, self.t)
            if self.bai.xong:
                for cs in self.bai.chi_so:
                    self.ket_qua.append((self.bai.ma, self.bai.ten) + cs)
                self.man_hinh = "ket_qua_bai"
                self.t_man = self.t
        elif self.man_hinh == "ket_qua_bai" and (self.t - self.t_man > CHO_KET_QUA or "vuot_AB" in ev):
            self._sang_bai_sau()
        dang_khuay = self.canh.tuong_tac.cam is not None and self.canh.tuong_tac.cam.loai == "dua"
        for x in ev:
            if x != "khuay_vong" or dang_khuay:
                self.su_kien.append((round(self.t, 3), x, ""))
        for loai, gt in ev_canh:
            if loai not in ("rot", "lac", "hut"):
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
            elif self.man_hinh in ("bai", "ket_qua_bai", "tong_ket", "can_truc"):
                if self.man_hinh != "tong_ket" and self.ket_qua:
                    self.xuat()
                self.man_hinh = "chao"
                self.hang_doi = []
                self.bai = None
                self.canh.dat_lai()
            else:
                self.chay = False
        elif k in (pygame.K_RETURN, pygame.K_KP_ENTER):
            if self.man_hinh == "ket_qua_bai":
                self._sang_bai_sau()
            elif self.man_hinh != "bai":
                self.lam_bo(bai_thu.BO_CHINH)
        elif pygame.K_1 <= k <= pygame.K_7:
            self.hang_doi = []
            self.ket_qua = []
            self.so_bai_hang = 1
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
        elif k == pygame.K_F11 and self.hien:
            pygame.display.toggle_fullscreen()
        elif k == pygame.K_n and isinstance(self.nguon, NguonAo):
            self.nguon.rung = not self.nguon.rung
        elif k == pygame.K_k and not isinstance(self.nguon, NguonAo):
            self.man_hinh = "can_truc"
            self.can_truc = {"buoc": 1, "mau": [], "t": self.t}
        elif k == pygame.K_m and isinstance(self.nguon, NguonAo):
            self._khoa_chuot(not self.chuot_khoa)

    def _khoa_chuot(self, bat):
        if not self.hien:
            return
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

    # ------------------------------------------------------------------ tiện ích vẽ 2D
    def chu(self, chu, xy, co="nho", mau=CHU, nen=False, giua=False, phai=False):
        khoa = (chu, co, mau)
        anh = self._chu_cache.get(khoa)
        if anh is None:
            if len(self._chu_cache) > 800:
                self._chu_cache.clear()
            anh = self.f[co].render(chu, True, mau)
            self._chu_cache[khoa] = anh
        r = anh.get_rect()
        if giua:
            r.center = (int(xy[0]), int(xy[1]))
        elif phai:
            r.topright = (int(xy[0]), int(xy[1]))
        else:
            r.topleft = (int(xy[0]), int(xy[1]))
        if nen:
            khung = r.inflate(14, 8)
            s = pygame.Surface(khung.size, pygame.SRCALPHA)
            pygame.draw.rect(s, (*NEN, 205), s.get_rect(), border_radius=6)
            self.man.blit(s, khung)
        self.man.blit(anh, r)
        return r

    def khung(self, rect, alpha=215, vien=(*NHAN, 70), bo=12):
        s = pygame.Surface(rect.size, pygame.SRCALPHA)
        pygame.draw.rect(s, (*NEN, alpha), s.get_rect(), border_radius=bo)
        if vien:
            pygame.draw.rect(s, vien, s.get_rect(), 1, border_radius=bo)
        self.man.blit(s, rect)

    def chip(self, chu, xy, mau, co="rat_nho"):
        """Nhãn nhỏ bo tròn (tên cảm biến)."""
        anh = self.f[co].render(chu, True, mau)
        r = anh.get_rect(topleft=(int(xy[0]) + 7, int(xy[1]) + 2))
        k = r.inflate(14, 4)
        s = pygame.Surface(k.size, pygame.SRCALPHA)
        pygame.draw.rect(s, (*mau, 38), s.get_rect(), border_radius=9)
        pygame.draw.rect(s, (*mau, 150), s.get_rect(), 1, border_radius=9)
        self.man.blit(s, k)
        self.man.blit(anh, r)
        return k

    def thanh(self, x, y, w, h, u, mau, nen=(40, 52, 60)):
        pygame.draw.rect(self.man, nen, (x, y, w, h), border_radius=h // 2)
        u = max(0.0, min(1.0, u))
        if u > 0:
            pygame.draw.rect(self.man, mau, (x, y, max(h, int(w * u)), h), border_radius=h // 2)

    def _tao_nen(self):
        s = pygame.Surface((W, H))
        for y in range(H):
            s.fill(tron_mau((34, 44, 54), (190, 198, 204), y / H), (0, y, W, 1))
        return s

    # ------------------------------------------------------------------ vẽ khung hình
    def ve_khung(self):
        m = self.man
        m.blit(self.nen, (0, 0))
        c = self.canh
        self.cam.vi_tri = c.cam_vi_tri
        self.cam.yaw = c.cam_yaw
        self.cam.pitch += (c.cam_pitch - self.cam.pitch) * 0.08
        for luoi, R, T in self.phong:
            self.ve.them(luoi, R, T, lop=0)
        self.ve.ve(m, 0)
        self._dau_tren_ban(c)
        self._bong(c)
        self._vat(c)
        self._bang_menu_3d(c)
        self._tay(c)
        self.ve.ve(m)
        self._chu_bang_menu(c)
        self._nhan_vat(c)
        self._dong_chay(c)
        if self.man_hinh in ("bai", "ket_qua_bai", "chao"):
            self._tia(c)
        self._ben_trai()
        self._the_bai()
        if self.bai and self.man_hinh == "bai":
            self.bai.ve(self)
        self._thong_bao()
        if c.tuong_tac.menu_mo:
            self._menu_tron()
        if self.man_hinh == "chao":
            self._man_chao()
        elif self.man_hinh == "ket_qua_bai":
            self._bang_ket_qua(self.bai, nho=True)
        elif self.man_hinh == "tong_ket":
            self._tong_ket()
        elif self.man_hinh == "can_truc":
            self._man_can_truc()
        if self.bang:
            self._bang_so_lieu()
        if self.hien:
            pygame.display.flip()

    def _vat(self, c):
        for luoi, R, T in c.trang_tri:
            self.ve.them(luoi, R, T)
        for v in c.vat:
            R = v.R()
            for luoi, dy in zip(v.luoi, v.luoi_dinh):
                self.ve.them(luoi, R, v.p + R @ np.array([0, dy, 0]))
            if v.loai == "lo" and v.dong_lo:
                self.ve.them(v.luoi_nap, R, v.p + R @ np.array([0, v.h_mieng, 0]))
            ml = v.muc_long()
            if ml is not None:
                r, h = ml
                trong = sum(v.mau_dd) > 640       # dung dịch không màu: trong hơn
                y0 = 0.002 if v.loai != "pipet" else 0.004
                self.ve.them(tru(r, h, v.mau_dd, n=14 if v.loai != "pipet" else 6, sang_nap=1.18), R,
                             v.p + R @ np.array([0, y0, 0]), alpha=110 if trong else 215)
            if v.chat_ran > 0:
                self.ve.them(tru(v.r * 0.85, 0.012 + 0.02 * v.chat_ran, (245, 245, 240), n=10), R, v.p + R @ np.array([0, 0.002, 0]))
            if v.nap is not None and not v.mo_nap:
                Rn = R @ ma_tran_yaw(-v.nap)
                self.ve.them(hop(0.03, 0.028, 0.03, (210, 70, 60), day=True), Rn, v.p + R @ np.array([0, v.h, 0]), vien=(120, 30, 30))
        for p, _, _, mau in c.tuong_tac.giot:
            self.ve.them(hop(0.004, 0.006, 0.004, mau), None, p)

    def _bong(self, c):
        """Bóng mềm dưới mỗi vật (vật đang cầm: bóng mờ dần theo độ cao, giúp đặt đúng chỗ)."""
        for v in c.vat:
            y = c.mat_duoi(v.p[0], v.p[2], v.p[1])
            cao = v.p[1] - y
            if cao > 0.6:
                continue
            S = self.ve.vong((v.p[0], y + 0.002, v.p[2]), v.r * (1.35 + cao * 2), 16)
            ve_elip_mo(self.man, S, (20, 26, 30), int(90 * (1 - cao / 0.6)))

    def _dau_tren_ban(self, c):
        m = self.man
        for i, (x, z, r, ten) in enumerate(c.o_dat):
            sang = i == c.o_sang
            S = self.ve.vong((x, cm.BAN_1.y + 0.001, z), r, 28)
            if S is not None:
                k = 0.5 + 0.5 * math.sin(self.t * 5) if sang else 0
                ve_elip_mo(m, S, VANG if sang else (200, 200, 200), int(40 + 50 * k))
                pygame.draw.aalines(m, VANG if sang else NHAT, True, [tuple(p) for p in S])
        if c.moc_san:
            x, z = c.moc_san
            S = self.ve.vong((x, 0.01, z), 0.35, 32)
            if S is not None:
                pygame.draw.polygon(m, XANH, [tuple(p) for p in S], 3)

    def _bang_menu_3d(self, c):
        bm = c.bang_menu
        if bm is None:
            return
        for i, (ten, x, y, w, h) in enumerate(bm.o):
            mau = (48, 66, 78)
            nh = bm.nhay.get(i)
            if nh and self.t - nh[0] < 0.6:
                mau = XANH if nh[1] else DO
            elif i == bm.tro:
                mau = (64, 150, 142)
            self.ve.them(tam_dung(w, h, mau), None, (x, y, bm.z), sang=1.0 if i != bm.tro else 1.1)

    def _chu_bang_menu(self, c):
        bm = c.bang_menu
        if bm is None:
            return
        for i, (ten, x, y, w, h) in enumerate(bm.o):
            p = self.ve.diem((x, y, bm.z))
            if p is None:
                continue
            if i == bm.dich:
                goc = [self.ve.diem((x + sx * w / 2, y + sy * h / 2, bm.z - 0.005)) for sx, sy in ((-1, -1), (-1, 1), (1, 1), (1, -1))]
                if all(goc):
                    pygame.draw.lines(self.man, VANG, True, goc, 3)
            self.chu(ten, p, "dam", CHU, giua=True)

    def _nhan_vat(self, c):
        yc = getattr(self.bai, "nhan_can", None) if self.bai else None
        for v in c.vat:
            if not v.nhan:
                continue
            P = v.p + np.array([0, v.h + 0.035, 0])
            if np.linalg.norm(P - c.cam_vi_tri) > 3.2:
                continue
            p = self.ve.diem(P)
            if p:
                self.chu(v.nhan, p, "rat_nho", VANG if v.nhan == yc else CHU, nen=True, giua=True)

    def _dong_chay(self, c):
        m = self.man
        for dau, cuoi, q, mau in c.tuong_tac.dong_chay:
            if q < 2.5:  # nhỏ giọt
                for k in range(4):
                    a = (self.t * 3 + k / 4) % 1.0
                    p = self.ve.diem(dau + (cuoi - dau) * a)
                    if p:
                        pygame.draw.circle(m, mau, p, 3)
            else:
                self.ve.duong(m, dau, cuoi, mau, max(2, int(q / 5)))

    def _tay(self, c):
        """Mô hình tay cầm nhỏ ở gốc tia, xoay đúng theo hướng tay cầm (để nhìn thấy lăn cổ tay)."""
        s = self.s
        if s is None:
            return
        o = c.tay()
        R = np.stack([dat_sang_tg(quay(s.q, e)) for e in ((0, -1, 0), (0, 0, 1), (1, 0, 0))], 1)
        f = c.huong_tia(*self.ct.ra)
        g = math.atan2(-R[0, 2], R[2, 2]) - math.atan2(-f[0], f[2])
        R = ma_tran_yaw(math.degrees(-g)) @ R if np.isfinite(g) else R
        o = o - c.huong_tia(*self.ct.ra) * 0.03
        self.ve.them(hop(0.022, 0.019, 0.075, (240, 244, 246)), R, o)
        self.ve.them(hop(0.024, 0.021, 0.01, NHAN), R, o + R @ np.array([0, 0, 0.022]))
        self.ve.them(tru(0.0055, 0.006, (36, 40, 46), n=10), R, o + R @ np.array([0, 0.0095, 0.008]))

    def _tia(self, c):
        m = self.man
        tt = c.tuong_tac
        if tt.diem_tro is None:
            return
        o = c.tay() + c.huong_tia(*self.ct.ra) * 0.05
        tro_bang = c.bang_menu is not None and c.bang_menu.tro is not None
        mau = VANG if tt.tinh else (DO if tt.tro_xa else (NHAN if tt.tro or tro_bang else (180, 196, 204)))
        self.ve.duong(m, o, tt.diem_tro, tron_mau(mau, (20, 30, 36), 0.5), 5)
        self.ve.duong(m, o, tt.diem_tro, mau, 2)
        p = self.ve.diem(tt.diem_tro)
        if p:
            pygame.draw.circle(m, mau, p, 7, 2)
            pygame.draw.circle(m, CHU, p, 2)
            if tt.tro and tt.cam is None:
                _, v, phan = tt.tro
                if tt.tro_xa:
                    chu = f"{v.ten} · xa quá {tt.tro[0]:.1f} m: đi lại gần"
                else:
                    chu = f"{v.ten}" + (" · nắp: giữ B + xoay cổ tay" if phan == "nap" else " · giữ B để cầm")
                self.chu(chu, (p[0] + 14, p[1] - 26), "nho", DO if tt.tro_xa else CHU, nen=True)
            if tt.goi_y:
                self.chu(tt.goi_y, (p[0] + 14, p[1] + 12), "nho", VANG, nen=True)

    # ------------------------------------------------------------------ bảng cảm biến bên trái
    def _ben_trai(self):
        s = self.s
        if s is None:
            return
        m = self.man
        n = self.nguon
        tt = self.canh.tuong_tac
        dung = set(self.bai.cam_bien) if self.bai and self.man_hinh in ("bai", "ket_qua_bai") else set()
        s_nen = pygame.Surface((BEN, H), pygame.SRCALPHA)
        s_nen.fill((*NEN, 236))
        m.blit(s_nen, (0, 0))
        pygame.draw.line(m, M.VIEN, (BEN - 1, 0), (BEN - 1, H))
        x0, y = 16, 14
        self.chu("VERA", (x0, y), "dam", NHAN)
        self.chu("· Phòng thử tay cầm", (x0 + 52, y + 1), "vua", CHU)
        y += 28
        self.chu(n.ten, (x0, y), "rat_nho", NHAT)
        y += 18
        tre = n.tre_ms()
        mau_tre = XANH if tre < 30 else (VANG if tre < 80 else DO)
        self.chu(f"{n.tan_so:4.0f} gói/s · mất {n.ty_le_mat:4.1f}% · trễ {min(tre, 999):3.0f} ms", (x0, y), "rat_nho", mau_tre)
        y += 24
        xt = x0 + 102

        def muc(ten, cao):
            nonlocal y
            bat = ten in dung
            mau = M.CAM_BIEN[ten]
            rr = pygame.Rect(8, y, BEN - 16, cao)
            pygame.draw.rect(m, M.O, rr, border_radius=10)
            pygame.draw.rect(m, tron_mau(M.O, mau, 0.6 if bat else 0.15), rr, 1, border_radius=10)
            if bat:
                pygame.draw.rect(m, mau, (8, y + 10, 3, cao - 20), border_radius=2)
                self.chu("đang thử", (BEN - 20, y + 9), "rat_nho", mau, phai=True)
            self.chu(ten, (x0 + 4, y + 7), "dam_nho", mau if bat else CHU)
            yy = y + 30
            y += cao + 8
            return yy

        # BMI160: chân trời ảo + số
        yy = muc("BMI160", 128)
        cx, cy, R = x0 + 46, yy + 44, 40
        pygame.draw.circle(m, (30, 40, 48), (cx, cy), R)
        a = math.radians(self.ct.roll)
        dp = max(-R, min(R, self.ct.ra[1] * R / 60))
        ux, uy = math.cos(a), math.sin(a)
        pygame.draw.line(m, M.CAM_BIEN["BMI160"], (cx - ux * R - uy * dp, cy - uy * R + ux * dp),
                         (cx + ux * R - uy * dp, cy + uy * R + ux * dp), 3)
        pygame.draw.circle(m, M.VIEN, (cx, cy), R, 2)
        pygame.draw.line(m, CHU, (cx - 10, cy), (cx + 10, cy), 1)
        wn = math.sqrt(sum(g * g for g in s.gyro))
        an = math.sqrt(sum(v * v for v in s.acc))
        self.chu(f"yaw    {self.ct.ra[0]:+6.1f}°", (xt, yy), "rat_nho", CHU)
        self.chu(f"pitch  {self.ct.ra[1]:+6.1f}°", (xt, yy + 17), "rat_nho", CHU)
        self.chu(f"lăn     {self.ct.roll:+6.1f}°", (xt, yy + 34), "rat_nho", CHU)
        self.chu(f"|ω| {wn:6.1f} °/s", (xt, yy + 51), "rat_nho", NHAT)
        self.chu(f"|a|  {an:5.2f} g" + ("  LẮC" if self.cu_chi.lac else ""), (xt, yy + 68), "rat_nho",
                 VANG if self.cu_chi.lac else NHAT)
        if s.loi_imu:
            self.chu("LỖI IMU", (xt + 100, yy), "dam_nho", DO)
        # Joystick
        yy = muc("Joystick", 104)
        cx, cy, R = x0 + 46, yy + 34, 32
        pygame.draw.circle(m, (30, 40, 48), (cx, cy), R)
        pygame.draw.circle(m, M.VIEN, (cx, cy), R, 2)
        pygame.draw.circle(m, M.MO, (cx, cy), max(2, int(R * 0.08)), 1)
        pygame.draw.circle(m, VANG if s.nhan_can else M.CAM_BIEN["Joystick"],
                           (int(cx + s.joy[0] * (R - 6)), int(cy - s.joy[1] * (R - 6))), 7)
        self.chu(f"x {s.joy[0]:+.2f}   y {s.joy[1]:+.2f}", (xt, yy), "rat_nho", CHU)
        self.chu("nút: " + ("NHẤN" if s.nhan_can else "nhả"), (xt, yy + 18), "rat_nho", VANG if s.nhan_can else NHAT)
        self.chu("chế độ " + ("ĐI" if tt.che_do == "DI" else "TAY") + (" (khoá đi)" if self.canh.khoa_di else ""),
                 (xt, yy + 36), "rat_nho", CHU)
        if tt.tinh:
            self.chu("TINH: tia chậm lại", (xt, yy + 54), "rat_nho", VANG)
        # Cảm ứng
        for nut, muc_n, cham in (("A", s.muc_a, s.cham_a), ("B", s.muc_b, s.cham_b)):
            ten = "Cảm ứng " + nut
            yy = muc(ten, 62)
            mau = M.CAM_BIEN[ten]
            self.thanh(x0 + 4, yy + 4, 170, 12, muc_n / 255, mau if cham else (110, 140, 150))
            for nguong in (38, 128):
                xx = x0 + 4 + int(170 * nguong / 255)
                pygame.draw.line(m, (210, 218, 222), (xx, yy), (xx, yy + 19), 1)
            nhan = "CHẠM" if cham else ("lơ lửng" if muc_n >= 38 else "")
            self.chu(nhan, (x0 + 184, yy + 1), "dam_nho" if cham else "rat_nho", mau if cham else NHAT)
            self.chu(f"{muc_n:3d}", (BEN - 20, yy + 1), "rat_nho", NHAT, phai=True)
            cc = getattr(self.cu_chi, nut).chap_chon
            if cc:
                self.chu(f"chập chờn {cc}", (BEN - 20, yy - 23), "rat_nho", VANG, phai=True)
        # phím
        yk = H - 62
        for d in ("Enter: bộ chính 1–5 · 1–7: một bài · Esc: dừng",
                  "Tab: số liệu · R: căn lại tia · F11: toàn màn hình",
                  ("M: khoá chuột · Q/E lăn · WASD cần · chuột trái B, phải A" if isinstance(n, NguonAo)
                   else "K: căn trục gắn BMI160 · P: kiểu tia")):
            self.chu(d, (x0, yk), "rat_nho", M.MO)
            yk += 18

    # ------------------------------------------------------------------ thẻ bài, thông báo
    def _the_bai(self):
        b = self.bai
        if not b or self.man_hinh != "bai":
            return
        w = 740
        x = BEN + (W - BEN - w) // 2
        r = pygame.Rect(x, 12, w, 98 + 19 * len(b.huong_dan))
        self.khung(r, 222)
        so = f"Bài {b.ma}" + (f" · {self.so_bai_hang - len(self.hang_doi)}/{self.so_bai_hang}" if self.so_bai_hang > 1 else "")
        rr = self.chu(so, (r.x + 16, r.y + 13), "dam", NHAT)
        rr = self.chu(b.ten, (rr.right + 10, r.y + 9), "to", NHAN)
        xc = rr.right + 14
        for cb in b.cam_bien:
            xc = self.chip(cb, (xc, r.y + 15), M.CAM_BIEN[cb]).right + 6
        con = b.gioi_han_s - (self.t - b.t0)
        self.chu(f"{max(0, con):3.0f} s", (r.right - 16, r.y + 13), "dam", VANG if con < 15 else NHAT, phai=True)
        self.chu(b.buoc, (r.x + 16, r.y + 44), "vua", VANG)
        self.thanh(r.x + 16, r.y + 74, w - 32, 5, b.tien_do, NHAN)
        yy = r.y + 86
        for dong in b.huong_dan:
            self.chu("• " + dong, (r.x + 16, yy), "rat_nho", CHU)
            yy += 19

    def _thong_bao(self):
        yy = H - 46
        for t, chu in reversed(list(self.canh.tuong_tac.thong_bao)):
            if self.t - t < 2.5:
                self.chu(chu, (self.tam_canh[0], yy), "vua", CHU, nen=True, giua=True)
                yy -= 36

    def _menu_tron(self):
        m = self.man
        tt = self.canh.tuong_tac
        (cx, cy), R = self.tam_canh, 170
        s = pygame.Surface((W, H), pygame.SRCALPHA)
        pygame.draw.circle(s, (*NEN, 215), (cx, cy), R + 36)
        m.blit(s, (0, 0))
        pygame.draw.circle(m, NHAN, (cx, cy), R + 36, 2)
        n = len(cm.HOA_CHAT_MENU)
        for i, ten in enumerate(cm.HOA_CHAT_MENU):
            a = math.radians(i * 360 / n - 90)
            p = (cx + R * 0.74 * math.cos(a), cy + R * 0.74 * math.sin(a))
            chon = i == tt.menu_chon
            pygame.draw.circle(m, NHAN if chon else (48, 64, 74), p, 48)
            self.chu(ten, p, "dam_nho" if chon else "nho", NEN if chon else CHU, giua=True)
        jx, jy = joystick(*self.s.joy)
        pygame.draw.circle(m, VANG, (int(cx + jx * 40), int(cy - jy * 40)), 6)
        self.chu("gạt cần chọn · nhả A", (cx, cy + 22), "nho", NHAT, giua=True)

    # ------------------------------------------------------------------ màn hình chào, kết quả, tổng kết
    def _man_chao(self):
        w, h = 800, 520
        r = pygame.Rect(BEN + (W - BEN - w) // 2, (H - h) // 2, w, h)
        self.khung(r, 232)
        self.chu("Phòng thử tay cầm VERA", (r.x + 28, r.y + 20), "tieu_de", NHAN)
        self.chu("BMI160 · joystick · 2 nút cảm ứng, thử bằng thao tác thí nghiệm thật", (r.x + 30, r.y + 62), "nho", NHAT)
        y = r.y + 100
        for lop in bai_thu.TAT_CA:
            chinh = lop in bai_thu.BO_CHINH
            pygame.draw.circle(self.man, NHAN if chinh else M.VIEN, (r.x + 44, y + 13), 14)
            self.chu(lop.ma, (r.x + 44, y + 13), "dam_nho", NEN if chinh else CHU, giua=True)
            self.chu(lop.ten + ("" if chinh else "  (thêm)"), (r.x + 70, y + 2), "dam", CHU if chinh else NHAT)
            self.chu(lop.mo_ta, (r.x + 70, y + 24), "rat_nho", NHAT)
            xc = r.x + 450
            for cb in lop.cam_bien:
                xc = self.chip(cb, (xc, y + 6), M.CAM_BIEN[cb]).right + 5
            y += 50
        y += 6
        self.chu("Enter: làm bộ chính (bài 1–5), cuối cùng ra bảng theo từng cảm biến   ·   1–7: một bài",
                 (r.centerx, y), "nho", CHU, giua=True)
        if isinstance(self.nguon, NguonAo):
            self.chu("Tay cầm ảo: bài bắt đầu sẽ khoá chuột (Esc để nhả) · Q/E lăn cổ tay · WASD cần gạt · Space nhấn cần",
                     (r.centerx, y + 24), "rat_nho", NHAT, giua=True)
        elif np.allclose(self.sua_truc.M, np.eye(3)):
            self.chu("CHƯA căn trục gắn BMI160: bấm K (đặt nằm ngang, rồi chúc mũi xuống 45°)", (r.centerx, y + 24), "nho", VANG, giua=True)

    def _man_can_truc(self):
        r = pygame.Rect(BEN + (W - BEN - 660) // 2, 220, 660, 200)
        self.khung(r, 235)
        b = self.can_truc["buoc"]
        self.chu("Căn trục gắn BMI160", (r.centerx, r.y + 36), "tieu_de", NHAN, giua=True)
        chu = ("Bước 1/2: đặt tay cầm NẰM NGANG trên bàn, mũi hướng ra trước, để yên" if b == 1 else
               "Bước 2/2: CHÚC MŨI tay cầm xuống khoảng 45° và giữ yên")
        self.chu(chu, (r.centerx, r.y + 100), "vua", CHU, giua=True)
        self.chu("tự ghi khi yên 2 giây · Esc: huỷ", (r.centerx, r.y + 140), "nho", NHAT, giua=True)

    def _bang_ket_qua(self, bai, nho=False):
        cs = bai.chi_so
        dong = 26
        w = 920
        cao = 92 + dong * len(cs)
        r = pygame.Rect(BEN + (W - BEN - w) // 2, max(12, (H - cao) // 2), w, cao)
        self.khung(r, 240)
        dat = all(c[4] for c in cs)
        so_dat = sum(1 for c in cs if c[4])
        self.chu(f"Bài {bai.ma} · {bai.ten}", (r.x + 22, r.y + 14), "to", CHU)
        self.chu(f"{'ĐẠT' if dat else 'CẦN CHỈNH'} · {so_dat}/{len(cs)} chỉ số", (r.right - 22, r.y + 18), "dam",
                 XANH if dat else VANG, phai=True)
        y = r.y + 54
        for ten_cs, gt, dv, tc, d, cb in cs:
            self.chip(cb, (r.x + 18, y + 1), M.CAM_BIEN[cb])
            self.chu(ten_cs, (r.x + 118, y), "nho", CHU)
            self.chu(f"{gt} {dv}".strip(), (r.x + 520, y), "dam_nho", CHU)
            self.chu(tc, (r.x + 700, y + 1), "rat_nho", NHAT)
            self.chu("✓" if d else "✗", (r.right - 26, y - 2), "dam", XANH if d else DO)
            y += dong
        if nho:
            con = max(0, CHO_KET_QUA - (self.t - self.t_man))
            self.chu(f"Enter / vuốt A → B: sang bài sau · tự sang sau {con:.0f} s", (r.centerx, r.bottom - 18), "rat_nho", NHAT, giua=True)

    def tong_cam_bien(self):
        """{cảm biến: (đạt, tổng)} gồm cả 'Chung'."""
        ra = {}
        for k in self.ket_qua:
            d, n = ra.get(k[7], (0, 0))
            ra[k[7]] = (d + bool(k[6]), n + 1)
        return ra

    def _tong_ket(self):
        r = pygame.Rect(BEN + 20, 24, W - BEN - 40, H - 48)
        self.khung(r, 240)
        dat = sum(1 for k in self.ket_qua if k[6])
        n = len(self.ket_qua)
        self.chu("Tổng kết theo cảm biến", (r.x + 22, r.y + 14), "to", CHU)
        self.chu(f"{dat}/{n} chỉ số đạt · {'KHẢ THI' if dat == n else 'CẦN CHỈNH các mục đỏ'}", (r.right - 22, r.y + 18),
                 "dam", XANH if dat == n else VANG, phai=True)
        tk = self.tong_cam_bien()
        wc = (r.w - 44 - 3 * 12) // 4
        for i, cb in enumerate(M.THU_TU_CAM_BIEN):
            x = r.x + 22 + i * (wc + 12)
            rr = pygame.Rect(x, r.y + 54, wc, 92)
            mau = M.CAM_BIEN[cb]
            self.khung(rr, 255, (*mau, 140), 10)
            self.chu(cb, (x + 14, rr.y + 10), "dam", mau)
            d, tong = tk.get(cb, (0, 0))
            self.chu(f"{d}/{tong}" if tong else "–", (x + 14, rr.y + 34), "tieu_de", CHU)
            if tong:
                self.chu("đạt hết" if d == tong else f"{tong - d} mục chưa đạt", (x + 104, rr.y + 48), "rat_nho",
                         XANH if d == tong else VANG)
                self.thanh(x + 14, rr.y + 78, wc - 28, 5, d / tong, XANH if d == tong else VANG)
        y = r.y + 164
        bai = []
        for k in self.ket_qua:
            if (k[0], k[1]) not in bai:
                bai.append((k[0], k[1]))
        for ma, ten in bai:
            hang = [k for k in self.ket_qua if k[0] == ma]
            d = sum(1 for k in hang if k[6])
            self.chu(f"{ma}. {ten}", (r.x + 22, y), "dam_nho", CHU)
            self.chu(f"{d}/{len(hang)}", (r.x + 200, y), "dam_nho", XANH if d == len(hang) else VANG)
            hong = [f"{k[2]} ({k[3]} {k[4]})".replace(" )", ")") for k in hang if not k[6]]
            chu = "đạt hết" if not hong else "chưa: " + " · ".join(hong)
            while self.f["rat_nho"].size(chu)[0] > r.w - 290 and len(chu) > 20:
                chu = chu[:-8] + "…"
            self.chu(chu, (r.x + 250, y + 1), "rat_nho", NHAT if not hong else DO)
            y += 30
        noi = os.path.relpath(self.thu_muc_ra, GOC) if os.path.abspath(self.thu_muc_ra).startswith(os.path.abspath(GOC))             else os.path.basename(os.path.normpath(self.thu_muc_ra))
        self.chu(f"Đã lưu bảng chi tiết vào {noi} · Esc: về màn hình đầu", (r.x + 22, r.bottom - 30), "nho", NHAN)

    def _bang_so_lieu(self):
        s = self.s
        r = pygame.Rect(W - 432, H - 210, 420, 198)
        self.khung(r, 230)
        q = s.q
        dong = [f"q  {q[0]:+.3f} {q[1]:+.3f} {q[2]:+.3f} {q[3]:+.3f}",
                f"ω  {s.gyro[0]:+7.1f} {s.gyro[1]:+7.1f} {s.gyro[2]:+7.1f} °/s",
                f"a  {s.acc[0]:+.2f} {s.acc[1]:+.2f} {s.acc[2]:+.2f} g   |a| {math.sqrt(sum(v * v for v in s.acc)):.2f}",
                f"tia yaw {self.ct.ra[0]:+6.1f}°  pitch {self.ct.ra[1]:+6.1f}°  lăn {self.ct.roll:+6.1f}°",
                f"rung tia {self.ct.run_tia():.3f}° · lọc {'bật' if self.ct.loc else 'tắt'} · {s.nhiet:.1f} °C"
                f"{' · yên' if s.yen else ''}{' · LỖI IMU' if s.loi_imu else ''}",
                f"lắc {self.cu_chi.nang_luong_lac:.2f} g · khuấy {self.cu_chi.vong} vòng · kiểu tia {self.ct.kieu}"]
        y = r.y + 12
        for d in dong:
            self.chu(d, (r.x + 14, y), "nho", CHU)
            y += 30

    # ------------------------------------------------------------------ xuất bảng
    def xuat(self):
        os.makedirs(self.thu_muc_ra, exist_ok=True)
        dau = ["Bài", "Tên bài", "Cảm biến", "Chỉ số", "Giá trị", "Đơn vị", "Tiêu chí", "Đạt"]
        hang = [[k[0], k[1], k[7], k[2], k[3], k[4], k[5], "Đạt" if k[6] else "Chưa đạt"] for k in self.ket_qua]
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
        tk = self.tong_cam_bien()
        theo_cb = [(cb, *tk[cb]) for cb in M.THU_TU_CAM_BIEN + ["Chung"] if cb in tk]
        try:
            import matplotlib
            matplotlib.use("Agg")
            import matplotlib.pyplot as plt
            fig = plt.figure(figsize=(15, 3 + 0.34 * len(hang)))
            gs = fig.add_gridspec(1, 2, width_ratios=[3.4, 1])
            ax = fig.add_subplot(gs[0])
            ax.axis("off")
            if hang:
                tb = ax.table(cellText=[h[1:] for h in hang], colLabels=dau[1:], loc="upper center", cellLoc="left",
                              colWidths=[0.13, 0.08, 0.3, 0.1, 0.05, 0.18, 0.07])
                tb.auto_set_font_size(False)
                tb.set_fontsize(8)
                tb.scale(1, 1.3)
                for (i, j), o in tb.get_celld().items():
                    if i == 0:
                        o.set_facecolor("#0e7c86")
                        o.set_text_props(color="white", weight="bold")
                    elif j == 6:
                        o.set_facecolor("#dcfce7" if hang[i - 1][7] == "Đạt" else "#fee2e2")
            ax.set_title(f"Phòng thử tay cầm VERA · nguồn: {self.nguon.ten} · {time.strftime('%d/%m/%Y %H:%M')}", fontsize=11)
            ax2 = fig.add_subplot(gs[1])
            ten_b = [cb for cb, _, _ in theo_cb][::-1]
            ax2.barh(ten_b, [d / n * 100 for _, d, n in theo_cb][::-1],
                     color=["#%02x%02x%02x" % M.CAM_BIEN[cb] for cb, _, _ in theo_cb][::-1])
            for i, (cb, d, n) in enumerate(theo_cb[::-1]):
                ax2.text(2, i, f"{d}/{n}", va="center", fontsize=9, color="black", weight="bold")
            ax2.set_xlim(0, 100)
            ax2.set_xlabel("% chỉ số đạt theo cảm biến")
            fig.tight_layout()
            fig.savefig(os.path.join(self.thu_muc_ra, "tong_ket.png"), dpi=130)
            plt.close(fig)
        except ImportError:
            pass
        try:
            from openpyxl import Workbook
            from openpyxl.styles import Font, PatternFill
            wb = Workbook()
            trang = (("Kết quả", dau, hang), ("Theo cảm biến", ["Cảm biến", "Đạt", "Tổng"], theo_cb),
                     ("Nguồn", dau_tk, self.thong_ke), ("Sự kiện", ["t (s)", "sự kiện", "chi tiết"], self.su_kien))
            for i, (ten_trang, d, hh) in enumerate(trang):
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
                        row[7].fill = PatternFill("solid", fgColor="DCFCE7" if row[7].value == "Đạt" else "FEE2E2")
            wb.save(os.path.join(self.thu_muc_ra, "tong_ket.xlsx"))
        except ImportError:
            pass
        self.su_kien.append((round(self.t, 3), "xuat", self.thu_muc_ra))

    def chup(self, ten):
        os.makedirs(self.thu_muc_ra, exist_ok=True)
        pygame.image.save(self.man, os.path.join(self.thu_muc_ra, ten))


def main():
    p = argparse.ArgumentParser(description="Phòng thử tay cầm VERA")
    p.add_argument("--com", help="firmware tay_cam_vera qua USB")
    p.add_argument("--thu", metavar="COM", help="tay cầm đang nạp sketch firmware/thu_cam_bien")
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
