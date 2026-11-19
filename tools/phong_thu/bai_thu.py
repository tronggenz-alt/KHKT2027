"""7 bài thử có chấm điểm cho từng nhóm tương tác. Mỗi bài dựng vật của mình, theo dõi sự kiện, ra bảng chỉ số.

Mỗi chỉ số: (tên, giá trị, đơn vị, tiêu chí chữ, đạt?). Tiêu chí lấy từ PDF tích hợp trang 8 nếu có,
còn lại là mức đặt ra cho phòng thử (ghi rõ trong docs/29).
"""
import math
import random

import numpy as np

from canh import HOA_CHAT_MENU, Vat

MAU_COC = (150, 190, 215)


class BaiThu:
    ma = "?"
    ten = "?"
    huong_dan = []
    gioi_han_s = 90.0

    def __init__(self):
        self.xong = False
        self.t0 = None
        self.chi_so = []
        self.buoc = ""

    def bat_dau(self, c, t):
        self.t0 = t

    def cap_nhat(self, c, ct, ev_canh, ev, t, dt):
        pass

    def ket_thuc(self, c, t):
        self.xong = True

    def het_gio(self, t):
        return self.t0 is not None and t - self.t0 > self.gioi_han_s

    def them(self, ten, gt, dv, tc, dat):
        self.chi_so.append((ten, gt, dv, tc, bool(dat)))


class BaiTro(BaiThu):
    ma, ten = "1", "Trỏ và chọn"
    huong_dan = ["Hướng tia vào bia giữa, giữ yên 3 giây (đo rung tia).",
                 "Sau đó bia nào sáng thì trỏ vào và NHẤP A.", "Lệch tia: giữ A + B 1 giây để căn lại."]
    gioi_han_s = 60

    def bat_dau(self, c, t):
        super().bat_dau(c, t)
        c.bia = [(x, y, 0.12) for y in (2.0, 1.6, 1.2) for x in (-1.0, 0.0, 1.0)]
        rng = random.Random(7)
        self.thu_tu = [0, 2, 6, 8, 1, 7, 3, 5]
        rng.shuffle(self.thu_tu)
        self.pha = "giu_yen"
        self.mau_giu = []
        self.t_pha = t
        self.trung, self.truot, self.t_moi = 0, 0, []
        self.chi = 0
        c.bia_sang = 4
        self.buoc = "Giữ tia yên trên bia giữa"

    def _bia_trung(self, c, ct):
        P = c.cat_tuong(c.tay(), c.huong_tia(*ct.ra))
        if P is None:
            return None
        for i, (x, y, r) in enumerate(c.bia):
            if math.hypot(P[0] - x, P[1] - y) <= r:
                return i
        return None

    def cap_nhat(self, c, ct, ev_canh, ev, t, dt):
        b = self._bia_trung(c, ct)
        if self.pha == "giu_yen":
            if b == 4:
                self.mau_giu.append(ct.ra.copy())
                self.buoc = f"Giữ yên: {len(self.mau_giu) * dt:.1f} / 3,0 s"
                if len(self.mau_giu) * dt >= 3.0:
                    a = np.array(self.mau_giu[int(0.5 / dt):])  # bỏ 0,5 s đầu (vừa ngắm tới)
                    self.rung = float(np.sqrt(a.var(0).sum()))
                    self.pha = "tro"
                    self.t_pha = t
                    c.bia_sang = self.thu_tu[0]
            else:
                self.mau_giu = []
            return
        if "A_nhap" in ev:
            if b == c.bia_sang:
                self.trung += 1
                self.t_moi.append(t - self.t_pha)
                self.chi += 1
                self.t_pha = t
                if self.chi >= len(self.thu_tu):
                    self.ket_thuc(c, t)
                    return
                c.bia_sang = self.thu_tu[self.chi]
            else:
                self.truot += 1
        self.buoc = f"Bia {self.chi + 1}/{len(self.thu_tu)}: trỏ vào bia sáng, nhấp A"

    def ket_thuc(self, c, t):
        super().ket_thuc(c, t)
        c.bia_sang = None
        rung = getattr(self, "rung", float("nan"))
        n = self.trung + self.truot
        ty = self.trung / n * 100 if n else 0.0
        tb = float(np.mean(self.t_moi)) if self.t_moi else float("nan")
        self.them("Rung tia khi cầm yên (RMS)", round(rung, 3), "°", "< 0,15° (PDF tr.8)", rung < 0.15)
        self.them("Tỉ lệ nhấp trúng", round(ty, 1), "%", "≥ 90%", ty >= 90 and self.trung == len(self.thu_tu))
        self.them("Thời gian trung bình mỗi bia", round(tb, 2), "s", "< 2,0 s", tb < 2.0)


class BaiCamDat(BaiThu):
    ma, ten = "2", "Cầm và đặt"
    huong_dan = ["Trỏ vào ống nghiệm, GIỮ B để cầm, buông B để thả.",
                 "Đặt 3 ống vào 3 ô tròn trên bàn (ô nào cũng được).",
                 "Nhấn cần sang chế độ TAY: gạt lên/xuống kéo xa/gần.", "Giữ A khi đang cầm: chế độ tinh ×0,25."]
    gioi_han_s = 90

    def bat_dau(self, c, t):
        super().bat_dau(c, t)
        c.vat = [Vat(f"Ống {i + 1}", "ong_nghiem", -0.6 + 0.12 * i, 0.75, 0.011, 0.15, (180, 205, 220), 15, 8,
                     mau_dd=m) for i, m in enumerate([(220, 70, 70), (70, 170, 90), (70, 110, 220)])]
        c.o_dat = [(0.25 + 0.2 * i, 0.9, 0.03, f"Ô {i + 1}") for i in range(3)]
        self.sai = {}
        self.buoc = "Cầm 3 ống đặt vào 3 ô"

    def cap_nhat(self, c, ct, ev_canh, ev, t, dt):
        for loai, gt in ev_canh:
            if loai == "tha":
                v = next(v for v in c.vat if v.ten == gt)
                kc = min(math.hypot(v.p[0] - x, v.p[2] - z) for x, z, _, _ in c.o_dat)
                if kc < 0.08:
                    self.sai[v.ten] = kc
                elif v.ten in self.sai:
                    del self.sai[v.ten]
        dung = [k for k, e in self.sai.items() if e <= 0.03]
        self.buoc = f"Đã đặt đúng ô: {len(dung)}/3 (sai số ≤ 3 cm)"
        if len(dung) == 3 and c.tuong_tac.cam is None:
            self.ket_thuc(c, t)

    def ket_thuc(self, c, t):
        super().ket_thuc(c, t)
        dung = [e for e in self.sai.values() if e <= 0.03]
        tg = t - self.t0
        tb = float(np.mean(list(self.sai.values()))) * 100 if self.sai else float("nan")
        self.them("Số vật đặt đúng ô", len(dung), "/3", "3/3, sai số ≤ 3 cm", len(dung) == 3)
        self.them("Sai số đặt trung bình", round(tb, 2), "cm", "≤ 3 cm", tb <= 3)
        self.them("Số lần rơi xuống sàn", c.tuong_tac.so_lan_roi, "lần", "0", c.tuong_tac.so_lan_roi == 0)
        self.them("Thời gian", round(tg, 1), "s", "< 45 s", tg < 45)


class BaiRot(BaiThu):
    ma, ten = "3", "Rót đúng 50 mL"
    huong_dan = ["Cầm cốc (GIỮ B), đưa miệng cốc tới sát bên trái ống đong.",
                 "Lăn cổ tay sang phải quá 35° để rót; nghiêng ít = nhỏ giọt.",
                 "Gần đủ thì giữ A (chế độ tinh). Đủ 50 mL thì dựng cốc, đặt xuống bàn."]
    gioi_han_s = 90

    def bat_dau(self, c, t):
        super().bat_dau(c, t)
        c.vat = [Vat("Cốc 250 mL", "coc", -0.25, 0.8, 0.036, 0.095, MAU_COC, 250, 150, mau_dd=(70, 120, 225)),
                 Vat("Ống đong 100 mL", "ong_dong", 0.15, 0.85, 0.016, 0.26, MAU_COC, 100, 0)]
        c.tuong_tac.tran = 0.0
        self.da_rot = False
        self.buoc = "Rót 50 mL vào ống đong"

    def cap_nhat(self, c, ct, ev_canh, ev, t, dt):
        od = c.vat[1]
        if any(e[0] == "rot" for e in ev_canh):
            self.da_rot = True
        self.buoc = f"Ống đong: {od.the_tich:5.1f} mL · tràn ra ngoài {c.tuong_tac.tran:4.1f} mL"
        if self.da_rot and any(e == ("tha", "Cốc 250 mL") for e in ev_canh) and od.the_tich > 5:
            self.ket_thuc(c, t)

    def ket_thuc(self, c, t):
        super().ket_thuc(c, t)
        v = c.vat[1].the_tich
        tran = c.tuong_tac.tran
        self.them("Thể tích rót được", round(v, 2), "mL", "50 ± 2 mL", abs(v - 50) <= 2)
        self.them("Lượng tràn ra ngoài", round(tran, 2), "mL", "< 2 mL", tran < 2)
        self.them("Thời gian", round(t - self.t0, 1), "s", "< 45 s", t - self.t0 < 45)


class BaiLacKhuay(BaiThu):
    ma, ten = "4", "Lắc và khuấy"
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
        self.buoc = "Lắc ống nghiệm"

    def cap_nhat(self, c, ct, ev_canh, ev, t, dt):
        if c.tuong_tac.dang_lac:
            self.t_lac += dt
        for loai, gt in ev_canh:
            if loai == "tan" and gt == "Ống nghiệm":
                self.t_tan_ong = t - self.t0
            if loai == "tan" and gt == "Cốc 100 mL":
                self.t_tan_coc = t - self.t0
        o, coc = c.vat[0], c.vat[1]
        self.buoc = f"Ống nghiệm: còn {o.chat_ran * 100:3.0f}% rắn · cốc: khuấy {round((1 - coc.chat_ran) * 3)}/3 vòng"
        if self.t_tan_ong is not None and self.t_tan_coc is not None:
            self.ket_thuc(c, t)

    def ket_thuc(self, c, t):
        super().ket_thuc(c, t)
        o, coc = c.vat[0], c.vat[1]
        self.them("Ống nghiệm tan hết khi lắc", "có" if o.chat_ran == 0 else "chưa", "", "có", o.chat_ran == 0)
        self.them("Thời gian nhận là đang lắc", round(self.t_lac, 2), "s", "≈ 2 s (đủ để tan)", self.t_lac >= 1.9)
        self.them("Số vòng khuấy nhận được", round((1 - coc.chat_ran) * 3), "vòng", "3", coc.chat_ran == 0)
        self.them("Thời gian", round(t - self.t0, 1), "s", "< 40 s", t - self.t0 < 40)


class BaiVan(BaiThu):
    ma, ten = "5", "Vặn mở nắp"
    huong_dan = ["Trỏ vào NẮP bình, giữ B, xoay cổ tay NGƯỢC chiều kim đồng hồ.",
                 "Cổ tay hết tầm thì buông B, xoay về, giữ B vặn tiếp (như vặn thật).", "Mở đủ 2 vòng là nắp rời."]
    gioi_han_s = 60

    def bat_dau(self, c, t):
        super().bat_dau(c, t)
        b = Vat("Bình nón", "binh", 0.0, 0.85, 0.045, 0.16, MAU_COC, 250, 100, mau_dd=(160, 90, 200))
        b.nap = 0.0
        c.vat = [b]
        self.buoc = "Vặn mở nắp bình"

    def cap_nhat(self, c, ct, ev_canh, ev, t, dt):
        b = c.vat[0]
        self.buoc = f"Đã vặn {b.nap:4.0f}° / 720° · lần cầm nắp: {c.tuong_tac.so_lan_van}"
        if b.mo_nap:
            self.ket_thuc(c, t)

    def ket_thuc(self, c, t):
        super().ket_thuc(c, t)
        b = c.vat[0]
        self.them("Mở được nắp", "có" if b.mo_nap else "chưa", "", "có", b.mo_nap)
        self.them("Số lần cầm nắp", c.tuong_tac.so_lan_van, "lần", "≤ 6 (≈ 120°/lần)", c.tuong_tac.so_lan_van <= 6)
        self.them("Thời gian", round(t - self.t0, 1), "s", "< 25 s", t - self.t0 < 25)


class BaiDi(BaiThu):
    ma, ten = "6", "Đi lại"
    huong_dan = ["Chế độ ĐI: gạt cần lên để đi, trái/phải để xoay người.",
                 "Đi tới vòng tròn xanh trước bàn 2 và quay mặt vào bàn.", "Nhấn cần để đổi ĐI ↔ TAY."]
    gioi_han_s = 60

    def bat_dau(self, c, t):
        super().bat_dau(c, t)
        c.vat = []
        c.moc_san = (3.5, 0.15)
        c.tuong_tac.che_do = "DI"
        self.buoc = "Đi tới bàn 2"

    def _kc(self, c):
        return math.hypot(c.cam_vi_tri[0] - c.moc_san[0], c.cam_vi_tri[2] - c.moc_san[1])

    def cap_nhat(self, c, ct, ev_canh, ev, t, dt):
        kc = self._kc(c)
        lech = abs(((c.cam_yaw + 180) % 360) - 180)
        self.buoc = f"Còn {kc:4.2f} m · lệch hướng bàn {lech:3.0f}°"
        if kc < 0.35 and lech < 30:
            self.ket_thuc(c, t)

    def ket_thuc(self, c, t):
        super().ket_thuc(c, t)
        kc = self._kc(c)
        self.them("Tới đúng chỗ", round(kc, 2), "m", "< 0,35 m, mặt hướng bàn ±30°", kc < 0.35)
        self.them("Thời gian", round(t - self.t0, 1), "s", "< 20 s", t - self.t0 < 20)
        c.moc_san = None


class BaiMenu(BaiThu):
    ma, ten = "7", "Menu tròn, hỏi AI, vuốt"
    huong_dan = ["GIỮ A 0,5 s mở menu tròn, gạt cần về mục được yêu cầu, NHẢ A để chọn (3 lần).",
                 "Sau đó NHẤP ĐÚP A để hỏi AI.", "Cuối cùng VUỐT: nhả A rồi chạm B ngay (< 0,3 s)."]
    gioi_han_s = 60

    def bat_dau(self, c, t):
        super().bat_dau(c, t)
        c.vat = []
        rng = random.Random(3)
        self.yeu_cau = rng.sample(HOA_CHAT_MENU, 3)
        self.dung, self.sai, self.t_chon = 0, 0, []
        self.t_moc = t
        self.ai = self.vuot = False
        self.buoc = f"Chọn: {self.yeu_cau[0]}"

    def cap_nhat(self, c, ct, ev_canh, ev, t, dt):
        i = self.dung + self.sai
        for loai, gt in ev_canh:
            if loai == "menu_chon" and i < 3:
                if gt == self.yeu_cau[i]:
                    self.dung += 1
                else:
                    self.sai += 1
                self.t_chon.append(t - self.t_moc)
                self.t_moc = t
                i += 1
            elif loai == "hoi_ai" and i >= 3:
                self.ai = True
            elif loai == "vuot" and gt == "tiep" and self.ai:
                self.vuot = True
        if i < 3:
            self.buoc = f"Lần {i + 1}/3 · chọn: {self.yeu_cau[i]}"
        elif not self.ai:
            self.buoc = "Nhấp đúp A để hỏi AI"
        elif not self.vuot:
            self.buoc = "Vuốt: nhả A rồi chạm B ngay"
        else:
            self.ket_thuc(c, t)

    def ket_thuc(self, c, t):
        super().ket_thuc(c, t)
        tb = float(np.mean(self.t_chon)) if self.t_chon else float("nan")
        self.them("Chọn đúng mục menu", self.dung, "/3", "3/3", self.dung == 3)
        self.them("Thời gian chọn trung bình", round(tb, 2), "s", "< 3 s (gồm 0,5 s giữ)", tb < 3)
        self.them("Nhấp đúp hỏi AI nhận được", "có" if self.ai else "chưa", "", "có", self.ai)
        self.them("Vuốt A → B nhận được", "có" if self.vuot else "chưa", "", "có", self.vuot)


TAT_CA = [BaiTro, BaiCamDat, BaiRot, BaiLacKhuay, BaiVan, BaiDi, BaiMenu]
