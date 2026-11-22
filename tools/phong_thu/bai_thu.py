"""Bài thử có chấm điểm cho tay cầm VERA: BMI160, joystick, 2 nút cảm ứng điện dung (không dùng cò Hall).

Bộ chính (Enter): 1 Kiểm tra cảm biến · 2 Trỏ menu · 3 Lấy chất · 4 Nhỏ pipet · 5 Rót chất.
Thêm (phím 6, 7): Lắc và khuấy · Vặn mở nắp. BaiDi chỉ dùng cho thu_that_tinh.py.

Mỗi chỉ số: (tên, giá trị, đơn vị, tiêu chí chữ, đạt?, cảm biến). Cảm biến là một trong mau.THU_TU_CAM_BIEN hoặc
"Chung" (thời gian cả bài). Tiêu chí lấy từ PDF tích hợp trang 8 nếu có, còn lại là mức đặt ra cho phòng thử.
"""
import math
import random

import numpy as np
import pygame

import mau as M
from canh import BAN_1, HOA_CHAT_MENU, TAM_VOI, BangMenu, Vat, lo_hoa_chat, luoi_gia_ong

MAU_COC = (150, 190, 215)


class BaiThu:
    ma = "?"
    ten = "?"
    cam_bien = ()
    mo_ta = ""
    huong_dan = []
    gioi_han_s = 90.0
    cam_pitch = -24.0

    def __init__(self):
        self.xong = False
        self.t0 = None
        self.chi_so = []
        self.buoc = ""
        self.tien_do = 0.0
        self.cc = None   # CuChi của bài (đếm chập chờn)

    def bat_dau(self, c, t):
        self.t0 = t
        c.cam_pitch = self.cam_pitch

    def cap_nhat(self, c, s, ct, cc, ev_canh, ev, t, dt):
        pass

    def ket_thuc(self, c, t):
        self.xong = True

    def het_gio(self, t):
        return self.t0 is not None and t - self.t0 > self.gioi_han_s

    def them(self, ten, gt, dv, tc, dat, cb="Chung"):
        self.chi_so.append((ten, gt, dv, tc, bool(dat), cb))

    def them_chap_chon(self, nut):
        n = getattr(self.cc, nut).chap_chon if self.cc else 0
        self.them(f"Cảm ứng {nut} chập chờn (tắt-bật < 0,12 s)", n, "lần", "0", n == 0, f"Cảm ứng {nut}")

    def ve(self, app):
        """Vẽ thêm lên màn hình (2D) khi bài cần."""


def _so(x, nd=2):
    return round(float(x), nd) if x is not None and np.isfinite(x) else float("nan")


# ====================================================================== 1. kiểm tra cảm biến
class BaiCamBien(BaiThu):
    ma, ten = "1", "Kiểm tra cảm biến"
    cam_bien = ("BMI160", "Joystick", "Cảm ứng A", "Cảm ứng B")
    mo_ta = "để yên, lăn cổ tay, gạt cần 4 hướng, chạm A / B theo nhịp"
    huong_dan = ["Làm theo dòng chữ vàng. Mỗi bước có giới hạn thời gian riêng,",
                 "bước nào không làm được thì tự sang bước sau và ghi là chưa đạt."]
    gioi_han_s = 150
    cam_pitch = -20
    HUONG = [("LÊN", (0.0, 1.0)), ("PHẢI", (1.0, 0.0)), ("XUỐNG", (0.0, -1.0)), ("TRÁI", (-1.0, 0.0))]
    NHIP, CHU_KY, CUA_SO, CHO_NHIP = 6, 1.0, 0.45, 1.5
    BUOC = ["yen", "lan_phai", "lan_trai", "joy", "nut", "nhip_A", "nhip_B"]

    def bat_dau(self, c, t):
        super().bat_dau(c, t)
        c.vat = []
        c.khoa_di = True
        self.yen = {"tia": [], "w": [], "g": [], "a": [], "ma": [], "mb": []}
        self.so_lan_dong = 0
        self.roll_max, self.roll_min = -999.0, 999.0
        self.joy = []           # mỗi hướng: (tên, đọc được là hướng nào, độ lớn lớn nhất, lệch tâm khi thả)
        self.i_joy = 0
        self.joy_pha = "gat"
        self.joy_mag = 0.0
        self.joy_doc = None
        self.t_vuot = None
        self.joy_du = []
        self.nut = False
        self.nhip = {}          # "A"/"B": dict
        self._sang("yen", t)

    def _sang(self, pha, t):
        self.pha = pha
        self.t_pha = t
        self.tien_do = self.BUOC.index(pha) / len(self.BUOC) if pha in self.BUOC else 1.0
        if pha in ("nhip_A", "nhip_B"):
            self.nhip[pha[-1]] = {"trung": [0] * self.NHIP, "thua": 0, "nham": 0, "muc": [], "nen": 0}

    def cap_nhat(self, c, s, ct, cc, ev_canh, ev, t, dt):
        self.cc = cc
        tp = t - self.t_pha
        w = math.sqrt(sum(g * g for g in s.gyro))
        if self.pha == "yen":
            if tp > 1.0:
                if w > 8.0 and tp < 20:
                    self.so_lan_dong += 1
                    self.t_pha = t
                    self.yen = {k: [] for k in self.yen}
                else:
                    y = self.yen
                    y["tia"].append(ct.ra.copy())
                    y["w"].append(w)
                    y["g"].append(s.gyro)
                    y["a"].append(math.sqrt(sum(a * a for a in s.acc)))
                    y["ma"].append(s.muc_a)
                    y["mb"].append(s.muc_b)
            n = len(self.yen["w"]) * dt
            self.buoc = f"Để tay cầm thật YÊN (đặt lên bàn càng tốt): {min(n, 3):.1f} / 3,0 s"
            if self.so_lan_dong and n < 0.5:
                self.buoc = "Tay cầm đang cử động: để yên lại"
            if n >= 3.0 or tp > 25:
                self._sang("lan_phai", t)
        elif self.pha in ("lan_phai", "lan_trai"):
            self.roll_max = max(self.roll_max, ct.roll)
            self.roll_min = min(self.roll_min, ct.roll)
            phai = self.pha == "lan_phai"
            self.buoc = f"Lăn cổ tay sang {'PHẢI' if phai else 'TRÁI'} hết cỡ (≥ 55°): đang {ct.roll:+.0f}°"
            if (phai and ct.roll >= 55) or (not phai and ct.roll <= -55) or tp > 10:
                self._sang("lan_trai" if phai else "joy", t)
        elif self.pha == "joy":
            self._joy(s, t, tp)
        elif self.pha == "nut":
            self.buoc = "NHẤN cần xuống (bấm nút của joystick)"
            if "can_nhan" in ev:
                self.nut = True
            if self.nut or tp > 8:
                self._sang("nhip_A", t)
        else:
            nut = self.pha[-1]
            khac = "B" if nut == "A" else "A"
            d = self.nhip[nut]
            if getattr(s, "cham_" + nut.lower()):
                d["muc"].append(getattr(s, "muc_" + nut.lower()))
            if nut + "_bam" in ev:
                k = round((tp - self.CHO_NHIP) / self.CHU_KY)
                if 0 <= k < self.NHIP and abs(tp - self.CHO_NHIP - k * self.CHU_KY) <= self.CUA_SO and d["trung"][k] == 0:
                    d["trung"][k] = 1
                else:
                    d["thua"] += 1
            if khac + "_bam" in ev:
                d["nham"] += 1
            con = max(0, self.NHIP - int(max(0.0, tp - self.CHO_NHIP + self.CUA_SO) // self.CHU_KY))
            self.buoc = f"CHẠM {nut} đúng lúc vòng tròn sáng · còn {con} nhịp"
            if tp > self.CHO_NHIP + self.NHIP * self.CHU_KY:
                if nut == "A":
                    self._sang("nhip_B", t)
                else:
                    self.ket_thuc(c, t)

    def _joy(self, s, t, tp):
        ten, (ex, ey) = self.HUONG[self.i_joy]
        jx, jy = s.joy
        r = math.hypot(jx, jy)
        if self.joy_pha == "gat":
            self.buoc = f"Gạt cần {ten} hết cỡ ({self.i_joy + 1}/4) rồi thả ra"
            self.joy_mag = max(self.joy_mag, r)
            if r >= 0.85:
                self.t_vuot = self.t_vuot or t
                if t - self.t_vuot >= 0.15 and self.joy_doc is None:
                    goc = math.degrees(math.atan2(jx, jy)) % 360
                    self.joy_doc = ["LÊN", "PHẢI", "XUỐNG", "TRÁI"][int(((goc + 45) % 360) // 90)]
            else:
                self.t_vuot = None
            if (self.joy_doc is not None and r < 0.2) or tp > 8:
                self.joy_pha = "tha"
                self.t_tha = t
                self.joy_du = []
        else:
            self.buoc = "Thả tay khỏi cần (đo lệch tâm)"
            if 0.15 <= t - self.t_tha <= 0.65:
                self.joy_du.append(r)
            if t - self.t_tha > 0.65:
                self.joy.append((ten, self.joy_doc, self.joy_mag, max(self.joy_du) if self.joy_du else float("nan")))
                self.i_joy += 1
                self.joy_pha, self.joy_mag, self.joy_doc, self.t_vuot = "gat", 0.0, None, None
                self.t_pha = t
                if self.i_joy >= 4:
                    self._sang("nut", t)

    def ket_thuc(self, c, t):
        super().ket_thuc(c, t)
        c.khoa_di = False
        y = self.yen
        if len(y["tia"]) > 10:
            a = np.array(y["tia"])
            rung = float(np.sqrt(a.var(0).sum()))
            # độ lệch con quay = độ lớn của TRUNG BÌNH véc-tơ ω (rung tay dao động quanh 0 nên tự triệt)
            w_tb, a_tb = float(np.linalg.norm(np.mean(y["g"], axis=0))), float(np.mean(y["a"]))
        else:
            rung = w_tb = a_tb = float("nan")
        self.them("Rung tia khi để yên (RMS)", _so(rung, 3), "°", "< 0,15° (PDF tr.8)", rung < 0.15, "BMI160")
        self.them("Độ lệch con quay khi yên |ω̄|", _so(w_tb), "°/s", "< 1 °/s", w_tb < 1.0, "BMI160")
        self.them("Gia tốc kế khi yên |a|", _so(a_tb, 3), "g", "1,00 ± 0,05 g", abs(a_tb - 1) <= 0.05, "BMI160")
        lp = self.roll_max if self.roll_max > -999 else float("nan")
        lt = self.roll_min if self.roll_min < 999 else float("nan")
        self.them("Lăn cổ tay đo được: phải / trái", f"{lp:+.0f}° / {lt:+.0f}°", "", "≥ 55° cả hai phía",
                  lp >= 55 and lt <= -55, "BMI160")
        dung = [j for j in self.joy if j[1] == j[0]]
        sai = [f"{j[0]}→{j[1] or 'không thấy'}" for j in self.joy if j[1] != j[0]] + [h for h, _ in self.HUONG[len(self.joy):]]
        self.them("Đúng chiều 4 hướng", f"{len(dung)}/4" + (" · " + ", ".join(sai) if sai else ""), "", "4/4",
                  len(dung) == 4, "Joystick")
        tam = min((j[2] for j in self.joy), default=0.0) if len(self.joy) == 4 else 0.0
        self.them("Tầm gạt nhỏ nhất trong 4 hướng", _so(tam), "", "≥ 0,90", tam >= 0.9, "Joystick")
        du = max((j[3] for j in self.joy if np.isfinite(j[3])), default=float("nan"))
        self.them("Lệch tâm lớn nhất khi thả cần", _so(du, 3), "", "< 0,08 (vùng chết)", du < 0.08, "Joystick")
        self.them("Nút nhấn cần", "có" if self.nut else "chưa", "", "có", self.nut, "Joystick")
        for nut in "AB":
            d = self.nhip.get(nut, {"trung": [0] * self.NHIP, "thua": 0, "nham": 0, "muc": []})
            nen = max(y["m" + nut.lower()], default=float("nan"))
            muc = float(np.mean(d["muc"])) if d["muc"] else float("nan")
            cb = "Cảm ứng " + nut
            self.them(f"Chạm {nut} nhận đúng nhịp", f"{sum(d['trung'])}/{self.NHIP}", "", f"{self.NHIP}/{self.NHIP}",
                      sum(d["trung"]) == self.NHIP, cb)
            self.them(f"Chạm {nut} thừa (ngoài nhịp / 2 lần một nhịp)", d["thua"], "lần", "0", d["thua"] == 0, cb)
            self.them(f"Mức tín hiệu {nut} khi chạm (trung bình)", _so(muc, 0), "/255", "≥ 160", muc >= 160, cb)
            self.them(f"Mức nhiễu {nut} khi không chạm (lớn nhất)", _so(nen, 0), "/255", "< 38 (ngưỡng lơ lửng)", nen < 38, cb)
            self.them(f"Chạm {nut} mà {'B' if nut == 'A' else 'A'} cũng báo chạm", d["nham"], "lần", "0", d["nham"] == 0, cb)
        self.them_chap_chon("A")
        self.them_chap_chon("B")

    # ---------- vẽ
    def ve(self, app):
        m = app.man
        cx, cy = app.tam_canh
        cy += 30
        t = app.t
        tp = t - self.t_pha
        if self.pha == "yen":
            n = min(1.0, len(self.yen["w"]) * (1 / 60) / 3.0)
            pygame.draw.circle(m, M.VIEN, (cx, cy), 62, 8)
            if n > 0:
                pygame.draw.arc(m, M.NHAN, (cx - 62, cy - 62, 124, 124), math.pi / 2 - 2 * math.pi * n, math.pi / 2, 8)
            app.chu("YÊN", (cx, cy), "to", M.CHU, giua=True)
        elif self.pha in ("lan_phai", "lan_trai"):
            R = 80
            pygame.draw.circle(m, M.O, (cx, cy), R + 6)
            pygame.draw.circle(m, M.VIEN, (cx, cy), R + 6, 2)
            for g in (-55, 55):
                a = math.radians(g - 90)
                pygame.draw.line(m, M.VANG, (cx, cy), (cx + R * math.cos(a), cy + R * math.sin(a)), 2)
            a = math.radians(app.ct.roll - 90)
            pygame.draw.line(m, M.NHAN, (cx, cy), (cx + (R - 4) * math.cos(a), cy + (R - 4) * math.sin(a)), 6)
            pygame.draw.circle(m, M.NHAN, (cx, cy), 8)
            app.chu(f"{app.ct.roll:+.0f}°", (cx, cy + R + 26), "to", M.CHU, giua=True)
        elif self.pha == "joy" and self.i_joy < 4:
            R = 74
            pygame.draw.circle(m, M.O, (cx, cy), R + 10)
            pygame.draw.circle(m, M.VIEN, (cx, cy), R + 10, 2)
            pygame.draw.circle(m, M.MO, (cx, cy), int(R * 0.08), 1)
            _, (ex, ey) = self.HUONG[self.i_joy]
            if self.joy_pha == "gat":
                dx, dy = ex * R, -ey * R
                pygame.draw.line(m, M.VANG, (cx, cy), (cx + dx * 0.8, cy + dy * 0.8), 4)
                pygame.draw.circle(m, M.VANG, (int(cx + dx), int(cy + dy)), 12, 3)
            jx, jy = app.s.joy
            pygame.draw.circle(m, M.NHAN, (int(cx + jx * R), int(cy - jy * R)), 11)
        elif self.pha == "nut":
            nhan = app.s.nhan_can
            pygame.draw.circle(m, M.VANG if nhan else M.O, (cx, cy), 46)
            pygame.draw.circle(m, M.VANG, (cx, cy), 46, 3)
            app.chu("NHẤN", (cx, cy), "dam", M.NEN if nhan else M.CHU, giua=True)
        elif self.pha in ("nhip_A", "nhip_B"):
            nut = self.pha[-1]
            mau_nut = M.CAM_BIEN["Cảm ứng " + nut]
            k = round((tp - self.CHO_NHIP) / self.CHU_KY)
            lech = tp - self.CHO_NHIP - k * self.CHU_KY
            dang_cham = getattr(app.s, "cham_" + nut.lower())
            trong = 0 <= k < self.NHIP and abs(lech) <= self.CUA_SO
            if 0 <= k < self.NHIP or tp < self.CHO_NHIP:
                pha = ((tp - self.CHO_NHIP) % self.CHU_KY) / self.CHU_KY
                r_ngoai = int(40 + 60 * (1 - pha)) if tp >= self.CHO_NHIP - self.CHU_KY else 100
                pygame.draw.circle(m, M.VIEN, (cx, cy), max(42, r_ngoai), 2)
            pygame.draw.circle(m, mau_nut if trong else M.O, (cx, cy), 40)
            pygame.draw.circle(m, mau_nut, (cx, cy), 40, 3)
            if dang_cham:
                pygame.draw.circle(m, M.CHU, (cx, cy), 46, 3)
            app.chu(nut, (cx, cy), "tieu_de", M.NEN if trong else M.CHU, giua=True)
            d = self.nhip[nut]
            for i in range(self.NHIP):
                x = cx + (i - (self.NHIP - 1) / 2) * 26
                da_qua = tp > self.CHO_NHIP + i * self.CHU_KY + self.CUA_SO
                mau_i = M.XANH if d["trung"][i] else (M.DO if da_qua else M.VIEN)
                pygame.draw.circle(m, mau_i, (int(x), cy + 122), 8)


# ====================================================================== 2. trỏ menu
class BaiTroMenu(BaiThu):
    ma, ten = "2", "Trỏ menu"
    cam_bien = ("BMI160", "Cảm ứng A", "Joystick")
    mo_ta = "trỏ ô trên bảng + chạm A; menu tròn bằng cần gạt"
    huong_dan = ["Bảng trên tường: trỏ tia vào ô có tên được yêu cầu, CHẠM A để chọn (6 lần).",
                 "Sau đó menu tròn: GIỮ A 0,6 s cho menu hiện, gạt cần tới mục, NHẢ A (3 lần)."]
    gioi_han_s = 90
    cam_pitch = -4

    def bat_dau(self, c, t):
        super().bat_dau(c, t)
        c.vat = []
        c.bang_menu = BangMenu(HOA_CHAT_MENU)
        rng = random.Random(5)
        self.thu_tu = rng.sample(range(len(HOA_CHAT_MENU)), len(HOA_CHAT_MENU))
        self.yeu_cau_tron = rng.sample(HOA_CHAT_MENU, 3)
        self.k = 0
        self.pha = "bang"
        self.dung_ngay = self.sai = self.truot = 0
        self.lan_dau = True
        self.t_moi, self.rung, self.t_tron = [], [], []
        self.dung_tron = self.huy_tron = 0
        self.t_moc = t
        c.bang_menu.dich = self.thu_tu[0]

    def cap_nhat(self, c, s, ct, cc, ev_canh, ev, t, dt):
        self.cc = cc
        bm = c.bang_menu
        if self.pha == "bang":
            dich = self.thu_tu[self.k]
            if "A_bam" in ev:
                n = max(5, int(0.4 / max(dt, 1e-3)))
                a = np.array(list(ct.lich_su)[-n:])
                self.rung.append(float(np.sqrt(a.var(0).sum())) if len(a) >= 5 else float("nan"))
                i = bm.tro
                if i == dich:
                    bm.nhay[i] = (t, True)
                    self.dung_ngay += self.lan_dau
                    self.t_moi.append(t - self.t_moc)
                    self.k += 1
                    self.lan_dau = True
                    self.t_moc = t
                    if self.k >= len(self.thu_tu):
                        self.pha = "tron"
                        bm.dich = None
                        c.cho_menu = True
                    else:
                        bm.dich = self.thu_tu[self.k]
                else:
                    self.lan_dau = False
                    if i is None:
                        self.truot += 1
                    else:
                        self.sai += 1
                        bm.nhay[i] = (t, False)
            if self.pha == "bang":
                self.buoc = f"Ô {self.k + 1}/6: trỏ vào « {HOA_CHAT_MENU[self.thu_tu[self.k]]} », chạm A"
            self.tien_do = self.k / 9
            return
        j = self.dung_tron + self.huy_tron
        for loai, gt in ev_canh:
            if loai == "menu_chon" and j < 3:
                if gt == self.yeu_cau_tron[j]:
                    self.dung_tron += 1
                else:
                    self.huy_tron += 1
                self.t_tron.append(t - self.t_moc)
                self.t_moc = t
                j += 1
        if j >= 3:
            self.ket_thuc(c, t)
            return
        self.tien_do = (6 + j) / 9
        self.buoc = f"Menu tròn {j + 1}/3: giữ A, gạt cần tới « {self.yeu_cau_tron[j]} », nhả A"

    def ket_thuc(self, c, t):
        super().ket_thuc(c, t)
        c.cho_menu = False
        tb = float(np.mean(self.t_moi)) if self.t_moi else float("nan")
        rung = float(np.nanmean(self.rung)) if self.rung and not all(np.isnan(self.rung)) else float("nan")
        self.them("Chọn đúng ô ngay lần chạm đầu", f"{self.dung_ngay}/6", "", "≥ 5/6", self.dung_ngay >= 5 and self.k >= 6, "BMI160")
        self.them("Thời gian trỏ trung bình mỗi ô", _so(tb), "s", "< 2,5 s", tb < 2.5, "BMI160")
        self.them("Rung tia 0,4 s trước khi chạm A (TB)", _so(rung, 3), "°", "< 0,30°", rung < 0.3, "BMI160")
        self.them("Chạm A trượt khỏi bảng / nhầm ô", f"{self.truot} / {self.sai}", "lần", "tổng ≤ 1",
                  self.truot + self.sai <= 1, "Cảm ứng A")
        self.them_chap_chon("A")
        tt = float(np.mean(self.t_tron)) if self.t_tron else float("nan")
        self.them("Menu tròn chọn đúng mục", f"{self.dung_tron}/3", "", "3/3", self.dung_tron == 3, "Joystick")
        self.them("Thời gian chọn menu tròn (TB)", _so(tt), "s", "< 3 s (gồm 0,6 s giữ A)", tt < 3, "Joystick")
        c.bang_menu = None


# ====================================================================== 3. lấy chất
class BaiLayChat(BaiThu):
    ma, ten = "3", "Lấy chất"
    cam_bien = ("Joystick", "BMI160", "Cảm ứng B")
    mo_ta = "đi tới kệ, lấy đúng lọ, đặt vào ô trên bàn"
    huong_dan = ["Kệ hoá chất ở bên TRÁI: gạt cần trái để xoay người, gạt lên để đi tới kệ.",
                 "Trỏ vào lọ được yêu cầu, GIỮ B để cầm; mang tới ô vàng trên bàn rồi buông B.",
                 "Nhấn cần: đổi ĐI ↔ TAY (TAY: gạt lên/xuống kéo vật xa/gần)."]
    gioi_han_s = 120
    cam_pitch = -16

    def bat_dau(self, c, t):
        super().bat_dau(c, t)
        c.vat = []
        for i, ten in enumerate(HOA_CHAT_MENU):
            tang = i // 3
            c.vat.append(lo_hoa_chat(ten, -2.44, 0.6 + 0.4 * (i % 3), y=(0.80, 1.20)[tang]))
        c.o_dat = [(BAN_1.x0 + 0.25, 0.78, 0.04, "Ô 1"), (BAN_1.x0 + 0.5, 0.78, 0.04, "Ô 2")]
        rng = random.Random(11)
        self.yeu_cau = rng.sample(HOA_CHAT_MENU, 2)
        self.k = 0
        self.t_toi = None
        self.lan_dau = True
        self.dung_ngay = 0
        self.nham = 0
        self.sai_so = []
        c.o_sang = 0

    def cap_nhat(self, c, s, ct, cc, ev_canh, ev, t, dt):
        self.cc = cc
        o = c.tay()
        if self.t_toi is None and any(np.linalg.norm(v.tam() - o) <= TAM_VOI for v in c.vat if not v.dang_cam):
            self.t_toi = t - self.t0
        ten = self.yeu_cau[self.k]
        for loai, gt in ev_canh:
            if loai == "cam":
                if gt == ten:
                    self.dung_ngay += self.lan_dau
                else:
                    self.nham += 1
                    self.lan_dau = False
                    c.tuong_tac.bao(t, f"Nhầm lọ: cần « {ten} »")
            elif loai == "tha" and gt == ten:
                v = next(v for v in c.vat if v.ten == gt)
                x, z, _, _ = c.o_dat[self.k]
                kc = math.hypot(v.p[0] - x, v.p[2] - z)
                if kc <= 0.08 and abs(v.p[1] - BAN_1.y) < 0.01:
                    self.sai_so.append(kc)
                    self.k += 1
                    self.lan_dau = True
                    if self.k >= len(self.yeu_cau):
                        c.o_sang = None
                        self.ket_thuc(c, t)
                        return
                    c.o_sang = self.k
        ten = self.yeu_cau[self.k]
        self.nhan_can = ten
        self.tien_do = self.k / 2
        cam = c.tuong_tac.cam
        if cam is not None and cam.ten == ten:
            self.buoc = f"Lọ {self.k + 1}/2: mang « {ten} » tới {c.o_dat[self.k][3]} (vòng vàng), buông B"
        else:
            self.buoc = f"Lọ {self.k + 1}/2: lấy lọ « {ten} » trên kệ bên trái"

    def ket_thuc(self, c, t):
        super().ket_thuc(c, t)
        tt = self.t_toi if self.t_toi is not None else float("nan")
        ss = float(np.mean(self.sai_so)) * 100 if self.sai_so else float("nan")
        self.them("Thời gian đi tới kệ (vào tầm với)", _so(tt, 1), "s", "< 12 s", tt < 12, "Joystick")
        self.them("Đặt được lọ vào ô", f"{len(self.sai_so)}/2", "", "2/2", len(self.sai_so) == 2, "Joystick")
        self.them("Cầm đúng lọ ngay lần đầu", f"{self.dung_ngay}/2", "", "2/2", self.dung_ngay == 2, "BMI160")
        self.them("Sai số đặt vào ô (TB)", _so(ss), "cm", "≤ 3 cm", ss <= 3, "BMI160")
        self.them("Số lần làm rơi", c.tuong_tac.so_lan_roi, "lần", "0", c.tuong_tac.so_lan_roi == 0, "Cảm ứng B")
        self.them_chap_chon("B")
        self.them("Thời gian cả bài", _so(t - self.t0, 1), "s", "< 90 s", t - self.t0 < 90)


# ====================================================================== 4. nhỏ pipet
class BaiNhoPipet(BaiThu):
    ma, ten = "4", "Nhỏ pipet"
    cam_bien = ("BMI160", "Cảm ứng A", "Cảm ứng B", "Joystick")
    mo_ta = "hút phenolphtalein, nhỏ đúng 5 giọt vào ống 2"
    huong_dan = ["GIỮ B cầm pipet. Đưa đầu pipet vào lọ phenolphtalein, GIỮ A để hút.",
                 "Đưa đầu pipet lên trên miệng ống số 2: mỗi lần CHẠM A là 1 giọt, nhỏ đúng 5 giọt.",
                 "Gần miệng lọ / ống tia tự chậm lại. Nhấn cần → TAY: gạt lên/xuống chỉnh xa/gần.",
                 "Xong thì buông B đặt pipet xuống bàn."]
    gioi_han_s = 90
    cam_pitch = -32
    SO_GIOT = 5

    def bat_dau(self, c, t):
        super().bat_dau(c, t)
        lo = lo_hoa_chat("Phenolphtalein", -0.3, 0.8, r=0.028, h=0.11, the_tich=60)
        lo.dong_lo = False
        pipet = Vat("Pipet", "pipet", -0.52, 0.8, 0.0042, 0.16, (205, 228, 238), 1.0, 0.0)
        ong = [Vat(f"Ống {i}", "ong_nghiem", 0.02 + 0.11 * (i - 1), 0.84, 0.012, 0.15, (190, 212, 226), 15, 6,
                   mau_dd=(222, 232, 240), nhan=str(i), hoa_chat="NaOH 0,1 M") for i in (1, 2, 3)]
        c.vat = [lo, pipet] + ong
        c.trang_tri = luoi_gia_ong(0.02, 0.24, 0.84)
        self.dung = self.ngoai = self.tuot = self.rong = 0
        self.lech = []
        self.hut = False
        self.da_cam = False
        self.nhan_can = "2"

    def cap_nhat(self, c, s, ct, cc, ev_canh, ev, t, dt):
        self.cc = cc
        for loai, gt in ev_canh:
            if loai == "cam" and gt == "Pipet":
                self.da_cam = True
            elif loai == "hut":
                self.hut = True
            elif loai == "giot_rong":
                self.rong += 1
            elif loai == "giot":
                dich, lech, gan = gt
                if dich == "Ống 2":
                    self.dung += 1
                    self.lech.append(lech)
                else:
                    self.ngoai += 1
                    c.tuong_tac.bao(t, "Giọt rơi ra ngoài" if dich is None else f"Giọt rơi vào {dich}, không phải Ống 2")
            elif loai == "tha" and gt == "Pipet":
                if self.dung + self.ngoai > 0:
                    self.ket_thuc(c, t)
                    return
                self.tuot += 1
        p = c.vat[1]
        self.tien_do = min(1.0, (0.2 if self.hut else 0) + 0.8 * self.dung / self.SO_GIOT)
        if not self.da_cam:
            self.buoc = "Trỏ vào pipet (bên trái), giữ B để cầm"
        elif p.the_tich < 0.04 and self.dung < self.SO_GIOT:
            self.buoc = "Đưa đầu pipet vào lọ phenolphtalein, GIỮ A để hút"
        else:
            self.buoc = (f"Ống 2: {self.dung}/{self.SO_GIOT} giọt · rơi chỗ khác: {self.ngoai}"
                         f" · pipet còn {p.the_tich:.2f} mL" + (" · đủ rồi: buông B" if self.dung >= self.SO_GIOT else ""))

    def ket_thuc(self, c, t):
        super().ket_thuc(c, t)
        lech = float(np.mean(self.lech)) * 1000 if self.lech else float("nan")
        self.them(f"Số giọt vào ống 2 (mỗi chạm A = 1 giọt)", self.dung, "giọt", f"đúng {self.SO_GIOT}",
                  self.dung == self.SO_GIOT, "Cảm ứng A")
        self.them("Hút được dung dịch (giữ A)", "có" if self.hut else "chưa", "", "có", self.hut, "Cảm ứng A")
        self.them("Giọt rơi ra ngoài / vào ống khác", self.ngoai, "giọt", "0", self.ngoai == 0, "BMI160")
        self.them("Lệch đầu pipet khỏi tâm ống lúc nhỏ (TB)", _so(lech, 1), "mm", "≤ 5 mm", lech <= 5, "BMI160")
        self.them("Nhả pipet giữa chừng", self.tuot, "lần", "0", self.tuot == 0, "Cảm ứng B")
        self.them_chap_chon("B")
        self.them("Thời gian cả bài", _so(t - self.t0, 1), "s", "< 60 s", t - self.t0 < 60)


# ====================================================================== 5. rót chất
class BaiRot(BaiThu):
    ma, ten = "5", "Rót chất"
    cam_bien = ("BMI160", "Cảm ứng B", "Cảm ứng A")
    mo_ta = "rót đúng 50 mL CuSO₄ vào ống đong bằng lăn cổ tay"
    huong_dan = ["Cầm cốc (GIỮ B), đưa miệng cốc tới sát bên trái ống đong.",
                 "Lăn cổ tay sang phải quá 35° để rót; nghiêng ít = nhỏ giọt.",
                 "Gần đủ thì giữ A (chế độ tinh). Đủ 50 mL thì dựng cốc, đặt xuống bàn."]
    gioi_han_s = 90
    cam_pitch = -26

    def bat_dau(self, c, t):
        super().bat_dau(c, t)
        c.vat = [Vat("Cốc 250 mL", "coc", -0.25, 0.8, 0.036, 0.095, MAU_COC, 250, 150, mau_dd=(50, 120, 225), nhan="CuSO₄"),
                 Vat("Ống đong 100 mL", "ong_dong", 0.15, 0.85, 0.016, 0.26, MAU_COC, 100, 0, nhan="Ống đong")]
        c.tuong_tac.tran = 0.0
        self.da_rot = False
        self.tuot = 0

    def cap_nhat(self, c, s, ct, cc, ev_canh, ev, t, dt):
        self.cc = cc
        od = c.vat[1]
        if any(e[0] == "rot" for e in ev_canh):
            self.da_rot = True
        self.tien_do = min(1.0, od.the_tich / 50)
        self.buoc = f"Ống đong: {od.the_tich:5.1f} / 50 mL · tràn ra ngoài {c.tuong_tac.tran:4.1f} mL"
        if any(e == ("tha", "Cốc 250 mL") for e in ev_canh):
            if self.da_rot and od.the_tich > 5:
                self.ket_thuc(c, t)
            elif self.da_rot:
                self.tuot += 1

    def ket_thuc(self, c, t):
        super().ket_thuc(c, t)
        v = c.vat[1].the_tich
        tran = c.tuong_tac.tran
        self.them("Thể tích rót được", round(v, 2), "mL", "50 ± 2 mL", abs(v - 50) <= 2, "BMI160")
        self.them("Lượng tràn ra ngoài", round(tran, 2), "mL", "< 2 mL", tran < 2, "BMI160")
        self.them("Số lần làm rơi", c.tuong_tac.so_lan_roi, "lần", "0", c.tuong_tac.so_lan_roi == 0, "Cảm ứng B")
        self.them_chap_chon("B")
        self.them("Thời gian cả bài", round(t - self.t0, 1), "s", "< 45 s", t - self.t0 < 45)


# ====================================================================== 6, 7. thêm
class BaiLacKhuay(BaiThu):
    ma, ten = "6", "Lắc và khuấy"
    cam_bien = ("BMI160", "Cảm ứng B")
    mo_ta = "lắc ống cho tan (gia tốc kế), khuấy 3 vòng (con quay)"
    huong_dan = ["Cầm ống nghiệm có chất rắn trắng, LẮC NHANH cho tan hết (cần khoảng 2 giây lắc).",
                 "Đặt ống xuống, cầm đũa thuỷ tinh, đưa đầu đũa vào cốc.",
                 "Vẽ vòng tròn đều bằng cổ tay: khuấy đủ 3 vòng."]
    gioi_han_s = 90

    def bat_dau(self, c, t):
        super().bat_dau(c, t)
        o = Vat("Ống nghiệm", "ong_nghiem", -0.5, 0.75, 0.011, 0.15, (180, 205, 220), 15, 8, mau_dd=(225, 228, 232))
        o.chat_ran = 1.0
        coc = Vat("Cốc 100 mL", "coc", 0.3, 0.85, 0.03, 0.07, MAU_COC, 100, 60, mau_dd=(240, 200, 90))
        coc.chat_ran = 1.0
        dua = Vat("Đũa thuỷ tinh", "dua", -0.15, 0.95, 0.004, 0.2, (200, 220, 230))
        c.vat = [o, coc, dua]
        self.t_lac = 0.0
        self.t_tan_ong = self.t_tan_coc = None

    def cap_nhat(self, c, s, ct, cc, ev_canh, ev, t, dt):
        self.cc = cc
        if c.tuong_tac.dang_lac:
            self.t_lac += dt
        for loai, gt in ev_canh:
            if loai == "tan" and gt == "Ống nghiệm":
                self.t_tan_ong = t - self.t0
            if loai == "tan" and gt == "Cốc 100 mL":
                self.t_tan_coc = t - self.t0
        o, coc = c.vat[0], c.vat[1]
        vong = round((1 - coc.chat_ran) * 3)
        self.tien_do = (1 - o.chat_ran) * 0.5 + vong / 6
        self.buoc = f"Ống nghiệm: còn {o.chat_ran * 100:3.0f}% rắn · cốc: khuấy {vong}/3 vòng"
        if self.t_tan_ong is not None and self.t_tan_coc is not None:
            self.ket_thuc(c, t)

    def ket_thuc(self, c, t):
        super().ket_thuc(c, t)
        o, coc = c.vat[0], c.vat[1]
        self.them("Ống nghiệm tan hết khi lắc", "có" if o.chat_ran == 0 else "chưa", "", "có", o.chat_ran == 0, "BMI160")
        self.them("Thời gian nhận là đang lắc", round(self.t_lac, 2), "s", "≈ 2 s (đủ để tan)", self.t_lac >= 1.9, "BMI160")
        self.them("Số vòng khuấy nhận được", round((1 - coc.chat_ran) * 3), "vòng", "3", coc.chat_ran == 0, "BMI160")
        self.them_chap_chon("B")
        self.them("Thời gian cả bài", round(t - self.t0, 1), "s", "< 40 s", t - self.t0 < 40)


class BaiVan(BaiThu):
    ma, ten = "7", "Vặn mở nắp"
    cam_bien = ("BMI160", "Cảm ứng B")
    mo_ta = "giữ B trên nắp, xoay cổ tay 2 vòng"
    huong_dan = ["Trỏ vào NẮP bình, giữ B, xoay cổ tay NGƯỢC chiều kim đồng hồ.",
                 "Cổ tay hết tầm thì buông B, xoay về, giữ B vặn tiếp (như vặn thật).", "Mở đủ 2 vòng là nắp rời."]
    gioi_han_s = 60

    def bat_dau(self, c, t):
        super().bat_dau(c, t)
        b = Vat("Bình nón", "binh", 0.0, 0.85, 0.045, 0.16, MAU_COC, 250, 100, mau_dd=(160, 90, 200))
        b.nap = 0.0
        c.vat = [b]

    def cap_nhat(self, c, s, ct, cc, ev_canh, ev, t, dt):
        self.cc = cc
        b = c.vat[0]
        self.tien_do = min(1.0, b.nap / 720)
        self.buoc = f"Đã vặn {b.nap:4.0f}° / 720° · lần cầm nắp: {c.tuong_tac.so_lan_van}"
        if b.mo_nap:
            self.ket_thuc(c, t)

    def ket_thuc(self, c, t):
        super().ket_thuc(c, t)
        b = c.vat[0]
        self.them("Mở được nắp", "có" if b.mo_nap else "chưa", "", "có", b.mo_nap, "BMI160")
        self.them("Số lần cầm nắp", c.tuong_tac.so_lan_van, "lần", "≤ 6 (≈ 120°/lần)", c.tuong_tac.so_lan_van <= 6, "Cảm ứng B")
        self.them_chap_chon("B")
        self.them("Thời gian cả bài", round(t - self.t0, 1), "s", "< 25 s", t - self.t0 < 25)


class BaiDi(BaiThu):
    """Đi tới bàn 2 (chỉ dùng cho thu_that_tinh.py: tay cầm nằm yên mà người tự đi là joystick lỗi)."""
    ma, ten = "D", "Đi lại"
    cam_bien = ("Joystick",)
    huong_dan = ["Chế độ ĐI: gạt cần lên để đi, trái/phải để xoay người.", "Đi tới vòng tròn xanh trước bàn 2."]
    gioi_han_s = 60

    def bat_dau(self, c, t):
        super().bat_dau(c, t)
        c.vat = []
        c.moc_san = (3.5, 0.15)
        c.tuong_tac.che_do = "DI"

    def _kc(self, c):
        return math.hypot(c.cam_vi_tri[0] - c.moc_san[0], c.cam_vi_tri[2] - c.moc_san[1])

    def cap_nhat(self, c, s, ct, cc, ev_canh, ev, t, dt):
        kc = self._kc(c)
        lech = abs(((c.cam_yaw + 180) % 360) - 180)
        self.buoc = f"Còn {kc:4.2f} m · lệch hướng bàn {lech:3.0f}°"
        if kc < 0.35 and lech < 30:
            self.ket_thuc(c, t)

    def ket_thuc(self, c, t):
        super().ket_thuc(c, t)
        kc = self._kc(c)
        self.them("Tới đúng chỗ", round(kc, 2), "m", "< 0,35 m", kc < 0.35, "Joystick")
        self.them("Thời gian", round(t - self.t0, 1), "s", "< 20 s", t - self.t0 < 20)
        c.moc_san = None


TAT_CA = [BaiCamBien, BaiTroMenu, BaiLayChat, BaiNhoPipet, BaiRot, BaiLacKhuay, BaiVan]
BO_CHINH = TAT_CA[:5]
