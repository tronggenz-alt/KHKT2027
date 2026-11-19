"""Cảnh phòng thử và luật tương tác của tay cầm (bản đồ tương tác, PDF tích hợp trang 4).

Mọi luật nằm ở đây để Unity (G2) chép lại đúng: trỏ, cầm / thả, kéo xa gần, xoay vật, rót, lắc, khuấy, vặn nắp,
chế độ tinh, menu tròn, căn lại, đi lại, nhấp đúp hỏi AI, vuốt giữa 2 nút, gợi ý khi ngón tay lơ lửng.
"""
import math
from collections import deque

import numpy as np

from ve3d import hop, ma_tran_truc, ma_tran_yaw, tru

# ---------------------------------------------------------------- thông số (giống phần cài đặt Unity sẽ có)
TOC_DI = 1.2             # m/s, gạt hết cần
TOC_QUAY = 75.0          # °/s
TOC_KEO = 0.6            # m/s, chế độ TAY lên/xuống = xa/gần
TOC_XOAY_VAT = 120.0     # °/s, chế độ TAY trái/phải
DO_NHAY_TINH = 0.25      # chế độ tinh: giữ A khi đang cầm
GOC_BAT_DAU_ROT = 35.0   # ° nghiêng (roll) mới bắt đầu chảy
LUU_LUONG_MAX = 40.0     # mL/s ở 90°
GIU_MENU = 0.5           # s giữ A để mở menu tròn
VONG_VAN_MO = 720.0      # ° vặn để mở nắp
THOI_GIAN_TAN = 2.0      # s lắc để tan hết chất rắn
VONG_KHUAY_TAN = 3       # vòng khuấy để tan
BAN_KINH_BAT = 0.025     # m nới rộng vùng bắt khi trỏ (dễ trỏ vật nhỏ)

CAM_SANG = (232, 236, 240)
MAU_BAN = (64, 86, 104)
MAU_MAT_BAN = (210, 218, 224)
HOA_CHAT_MENU = ["HCl 0,1 M", "NaOH 0,1 M", "Nước cất", "Phenolphtalein", "CuSO₄ 0,5 M", "AgNO₃ 0,1 M"]


def wrap(g):
    return (g + 180) % 360 - 180


class Vat:
    """Vật cầm được. Gốc ở tâm đáy. Dung tích mL; the_tich là chất lỏng đang có."""

    def __init__(self, ten, loai, x, z, r, h, mau, suc_chua=0.0, the_tich=0.0, mau_dd=(70, 140, 220), y=0.9):
        self.ten, self.loai = ten, loai
        self.p = np.array([x, y, z], float)
        self.r, self.h = r, h
        self.mau = mau
        self.suc_chua, self.the_tich, self.mau_dd = suc_chua, the_tich, mau_dd
        self.yaw = 0.0
        self.nghieng = (np.array([0, 0, 1.0]), 0.0)  # (trục, góc) khi đang cầm và lăn cổ tay
        self.chat_ran = 0.0    # 0..1 phần chất rắn chưa tan
        self.nap = None        # góc đã vặn (°) nếu có nắp; None = không nắp
        self.mo_nap = False
        self.dang_cam = False
        self.ro = False        # vừa rơi xuống sàn
        kinh = loai in ("coc", "ong_dong", "ong_nghiem", "binh")
        if loai == "binh":
            self.luoi = [tru(r, h * 0.62, mau, kinh=True, nap_tren=False), tru(r * 0.36, h * 0.38, mau, kinh=True, nap_duoi=False, r_tren=r * 0.3)]
            self.luoi_dinh = [0.0, h * 0.62]
        elif loai == "dua":
            self.luoi = [tru(r, h, (200, 220, 230), n=6)]
            self.luoi_dinh = [0.0]
        elif loai == "lo":
            self.luoi = [tru(r, h * 0.8, mau, n=12), tru(r * 0.45, h * 0.2, (40, 40, 48), n=10)]
            self.luoi_dinh = [0.0, h * 0.8]
        else:
            self.luoi = [tru(r, h, mau, kinh=kinh, nap_tren=False)]
            self.luoi_dinh = [0.0]

    @property
    def mieng(self):
        """Bán kính miệng (bình nón: cổ hẹp)."""
        return self.r * 0.3 if self.loai == "binh" else self.r

    def R(self):
        truc, goc = self.nghieng
        return ma_tran_truc(truc, goc) @ ma_tran_yaw(self.yaw)

    def dinh(self):
        """Tâm miệng trên cùng (thế giới)."""
        return self.p + self.R() @ np.array([0, self.h, 0])

    def tam(self):
        return self.p + self.R() @ np.array([0, self.h / 2, 0])


class Ban:
    def __init__(self, x0, x1, z0, z1, y=0.9):
        self.x0, self.x1, self.z0, self.z1, self.y = x0, x1, z0, z1, y

    def chua(self, x, z, le=0.0):
        return self.x0 - le <= x <= self.x1 + le and self.z0 - le <= z <= self.z1 + le


class Canh:
    def __init__(self):
        self.ban = [Ban(-1.3, 1.3, 0.55, 1.15), Ban(2.7, 4.3, 0.55, 1.15)]
        self.vat = []
        self.o_dat = []          # ô đánh dấu trên bàn: (x, z, bán kính, tên)
        self.bia = []            # bia trên tường (bài trỏ): (x, y, bán kính)
        self.bia_sang = None     # chỉ số bia đang cần trỏ
        self.moc_san = None      # (x, z) điểm cần đi tới (bài đi lại)
        self.mat_tuong_z = 2.6
        self.cam_vi_tri = np.array([0.0, 1.6, 0.0])
        self.cam_yaw = 0.0
        self.tuong_tac = TuongTac(self)

    def dat_lai(self):
        self.vat, self.o_dat, self.bia, self.bia_sang, self.moc_san = [], [], [], None, None
        self.cam_vi_tri = np.array([0.0, 1.6, 0.0])
        self.cam_yaw = 0.0
        self.tuong_tac = TuongTac(self)

    # ---------- hình học ----------
    def tay(self):
        """Gốc tia: bàn tay phải trước người."""
        return self.cam_vi_tri + ma_tran_yaw(self.cam_yaw) @ np.array([0.22, -0.35, 0.35])

    def huong_tia(self, yaw_tia, pitch_tia):
        p = math.radians(pitch_tia)
        R = ma_tran_yaw(self.cam_yaw + yaw_tia)
        return R @ np.array([0, math.sin(p), math.cos(p)])

    def huong_toi(self, P):
        """yaw, pitch tia (so với người) để tia từ tay đi qua điểm P. Dùng cho bot tự thử."""
        d = np.asarray(P, float) - self.tay()
        d = ma_tran_yaw(-self.cam_yaw) @ d
        return math.degrees(math.atan2(-d[0], d[2])), math.degrees(math.atan2(d[1], math.hypot(d[0], d[2])))

    def mat_duoi(self, x, z):
        for b in self.ban:
            if b.chua(x, z):
                return b.y
        return 0.0

    def cat_vat(self, o, d, bo_qua=None):
        """Tia cắt vật gần nhất: (t, vật, 'nap' hoặc 'than') hoặc None."""
        tot = None
        for v in self.vat:
            if v is bo_qua or v.dang_cam:
                continue
            r = v.r + BAN_KINH_BAT
            c = v.p
            oo = o - c
            a = d[0] ** 2 + d[2] ** 2
            b = 2 * (oo[0] * d[0] + oo[2] * d[2])
            cc = oo[0] ** 2 + oo[2] ** 2 - r * r
            ts = []
            if a > 1e-9:
                disc = b * b - 4 * a * cc
                if disc >= 0:
                    for t in ((-b - math.sqrt(disc)) / (2 * a), (-b + math.sqrt(disc)) / (2 * a)):
                        y = oo[1] + t * d[1]
                        if t > 0 and -0.01 <= y <= v.h + 0.02:
                            ts.append(t)
            if abs(d[1]) > 1e-6:
                t = (v.h - oo[1]) / d[1]
                q = oo + t * d
                if t > 0 and q[0] ** 2 + q[2] ** 2 <= r * r:
                    ts.append(t)
            if ts:
                t = min(ts)
                y = oo[1] + t * d[1]
                phan = "nap" if v.nap is not None and not v.mo_nap and y > v.h * 0.8 else "than"
                if tot is None or t < tot[0]:
                    tot = (t, v, phan)
        return tot

    def cat_ban(self, o, d):
        """Điểm tia chạm mặt bàn / sàn."""
        tot = None
        for b in self.ban:
            if d[1] < -1e-6:
                t = (b.y - o[1]) / d[1]
                q = o + t * d
                if t > 0 and b.chua(q[0], q[2]) and (tot is None or t < tot):
                    tot = t
        if tot is None and d[1] < -1e-6:
            tot = -o[1] / d[1]
        return tot

    def cat_tuong(self, o, d):
        if d[2] <= 1e-6:
            return None
        t = (self.mat_tuong_z - o[2]) / d[2]
        return o + t * d if t > 0 else None


class TuongTac:
    """Máy trạng thái tương tác. cap_nhat nhận trạng thái tay cầm, con trỏ, sự kiện cử chỉ; trả về sự kiện cảnh."""

    def __init__(self, canh):
        self.c = canh
        self.che_do = "DI"
        self.cam = None              # vật đang cầm
        self.kc = 0.5                # khoảng cách giữ (m)
        self.tam_cam = None          # tâm vật đang cầm (đi theo điểm cuối tia)
        self.tro = None              # (t, vật, phần) đang trỏ
        self.diem_tro = None         # điểm tia chạm
        self.van = None              # vật đang vặn nắp
        self.roll_van = None
        self.so_lan_van = 0
        self.menu_mo = False
        self.menu_chon = None
        self.t_a_bam = None
        self.tinh = False
        self.dong_chay = []          # giọt / dòng để vẽ: (điểm đầu, điểm cuối, lưu lượng)
        self.tran = 0.0              # mL rót ra ngoài
        self.thong_bao = deque(maxlen=4)
        self.goi_y = None
        self.so_lan_doi_che_do = 0
        self.so_lan_roi = 0
        self.khuay_vong = 0
        self.dang_lac = False

    def bao(self, t, chu):
        self.thong_bao.append((t, chu))

    def cap_nhat(self, s, joy, ct, cu_chi, ev, t, dt):
        """s: TrangThai tay cầm; joy: joystick sau vùng chết; ct: ConTro; cu_chi: CuChi; ev: sự kiện cử chỉ."""
        c = self.c
        ra = []
        jx, jy = joy
        # ---- nhấn cần: đổi ĐI / TAY
        if "can_nhan" in ev:
            self.che_do = "TAY" if self.che_do == "DI" else "DI"
            self.so_lan_doi_che_do += 1
            self.bao(t, f"Chế độ {'TAY: kéo xa/gần, xoay vật' if self.che_do == 'TAY' else 'ĐI: đi lại, xoay người'}")
            ra.append(("doi_che_do", self.che_do))
        # ---- căn lại A + B 1 giây
        if "can_lai" in ev:
            ct.can_lai()
            self.bao(t, "Đã căn lại: tia về giữa")
            ra.append(("can_lai", None))
        # ---- nhấp đúp A: hỏi AI
        if "A_nhap_dup" in ev and self.cam is None:
            self.bao(t, "Hỏi trợ lý AI (giọng nói)…")
            ra.append(("hoi_ai", None))
        if "vuot_AB" in ev:
            self.bao(t, "Vuốt A → B: bước tiếp theo")
            ra.append(("vuot", "tiep"))
        if "vuot_BA" in ev:
            self.bao(t, "Vuốt B → A: lùi lại")
            ra.append(("vuot", "lui"))

        # ---- đi lại / kéo, xoay vật
        if self.che_do == "DI" and not self.menu_mo:
            c.cam_yaw -= jx * TOC_QUAY * dt
            buoc = ma_tran_yaw(c.cam_yaw) @ np.array([0, 0, jy * TOC_DI * dt])
            moi = c.cam_vi_tri + buoc
            if not any(b.chua(moi[0], moi[2], 0.3) for b in c.ban):  # không đi xuyên bàn
                c.cam_vi_tri = moi
        elif self.che_do == "TAY" and self.cam is not None:
            self.kc = float(np.clip(self.kc + jy * TOC_KEO * dt, 0.2, 1.4))
            self.cam.yaw += jx * TOC_XOAY_VAT * dt

        # ---- chế độ tinh: đang cầm, giữ A
        self.tinh = self.cam is not None and s.cham_a
        ct.do_nhay = DO_NHAY_TINH if self.tinh else 1.0

        o = c.tay()
        d = c.huong_tia(*ct.ra)
        self.tro = c.cat_vat(o, d, bo_qua=self.cam) if self.van is None else self.tro
        tb = c.cat_ban(o, d)
        tw = c.cat_tuong(o, d)
        tw = float(np.linalg.norm(tw - o)) if tw is not None else None
        ts = [x for x in ((self.tro[0] if self.tro else None), tb, tw) if x is not None]
        self.diem_tro = o + (min(ts) if ts else 3.0) * d

        # ---- menu tròn: giữ A 0,5 s khi tay không cầm gì, gạt cần chọn, nhả A để chọn
        if "A_bam" in ev:
            self.t_a_bam = t
        if self.cam is None and s.cham_a and not s.cham_b and self.t_a_bam is not None and t - self.t_a_bam >= GIU_MENU and not self.menu_mo:
            self.menu_mo = True
            self.menu_chon = None
            ra.append(("menu_mo", None))
        if self.menu_mo:
            r = math.hypot(jx, jy)
            if r > 0.5:
                goc = math.degrees(math.atan2(jx, jy)) % 360  # 0° = gạt lên, theo chiều kim đồng hồ
                n = len(HOA_CHAT_MENU)
                moi = int(((goc + 180 / n) % 360) // (360 / n))
                if self.menu_chon is None:
                    self.menu_chon = moi
                else:  # trễ 10°: phải gạt quá mép ô 10° mới đổi
                    tam_cu = self.menu_chon * 360 / n
                    if abs(wrap(goc - tam_cu)) > 180 / n + 10:
                        self.menu_chon = moi
            if not s.cham_a:
                self.menu_mo = False
                if self.menu_chon is not None:
                    self.bao(t, f"Đã chọn: {HOA_CHAT_MENU[self.menu_chon]}")
                    ra.append(("menu_chon", HOA_CHAT_MENU[self.menu_chon]))
                else:
                    ra.append(("menu_huy", None))
            if s.cham_b:  # chạm thêm B: huỷ menu, nhường cho căn lại
                self.menu_mo = False

        # ---- vặn nắp: trỏ vào nắp, giữ B, xoay cổ tay quanh trục mũi
        if self.van is not None:
            if not s.cham_b:
                self.van = None
                self.roll_van = None
            else:
                v = self.van
                dr = wrap(ct.roll - self.roll_van)
                self.roll_van = ct.roll
                v.nap = max(0.0, v.nap - dr)  # vặn ngược chiều kim đồng hồ (roll âm) là mở
                if v.nap >= VONG_VAN_MO and not v.mo_nap:
                    v.mo_nap = True
                    self.van = None
                    self.bao(t, f"Đã mở nắp {v.ten}")
                    ra.append(("mo_nap", v.ten))
        # ---- cầm: chạm B
        elif "B_bam" in ev and self.cam is None and not self.menu_mo and not s.cham_a:
            if self.tro:
                _, v, phan = self.tro
                if phan == "nap":
                    self.van = v
                    self.roll_van = ct.roll
                    self.so_lan_van += 1
                    ra.append(("bat_dau_van", v.ten))
                elif v.loai == "binh" and v.nap is not None and not v.mo_nap:
                    self.bao(t, "Bình đang đậy nắp: trỏ vào nắp để vặn")
                else:
                    self.cam = v
                    v.dang_cam = True
                    self.kc = float(np.linalg.norm(v.tam() - o))
                    self.tam_cam = v.tam()
                    ra.append(("cam", v.ten))
        # ---- thả: buông B
        if self.cam is not None and not s.cham_b:
            v = self.cam
            v.dang_cam = False
            tam = v.tam()
            v.nghieng = (np.array([0, 0, 1.0]), 0.0)
            v.p = tam - np.array([0, v.h / 2, 0])
            self.tam_cam = None
            y = c.mat_duoi(v.p[0], v.p[2])
            v.p[1] = y
            if y == 0.0:
                v.ro = True
                self.so_lan_roi += 1
                self.bao(t, f"{v.ten} rơi xuống sàn!")
            self.cam = None
            ra.append(("tha", v.ten))

        # ---- vật đang cầm đi theo tia, nghiêng theo cổ tay
        self.dong_chay = []
        self.dang_lac = False
        if self.cam is not None:
            v = self.cam
            ngang = np.array([d[0], 0, d[2]])
            ngang /= max(np.linalg.norm(ngang), 1e-9)
            # roll dương = lăn cổ tay sang PHẢI thì miệng vật phải đổ sang phải (cùng phía vòi trong _rot).
            # Rodrigues quanh trục tới trước với góc dương đưa đỉnh sang −x (trái), nên phải đổi dấu (sửa 11/11/2026:
            # trước đó cốc đổ sang trái mà vòi nằm ở mép phải, mép đang ở trên cao)
            v.nghieng = (ngang, float(np.clip(-ct.roll, -95, 95)))
            # tay nắm ở TÂM vật: nghiêng quanh tâm, tâm đi theo điểm cuối tia
            self.tam_cam = getattr(self, "tam_cam", None)
            muc_tieu = o + d * self.kc
            self.tam_cam = muc_tieu if self.tam_cam is None else self.tam_cam + (muc_tieu - self.tam_cam) * min(1.0, dt * 25)
            v.p = self.tam_cam - v.R() @ np.array([0, v.h / 2, 0])
            ra += self._rot(v, ct, t, dt)
            ra += self._lac_khuay(v, cu_chi, t, dt)
            if "A_nhap" in ev:
                self.bao(t, f"Dùng: {v.ten}")
                ra.append(("dung", v.ten))
        # ---- gợi ý khi ngón cái lơ lửng trên A
        self.goi_y = None
        if s.muc_a >= 38 and not s.cham_a:
            self.goi_y = ("A: dùng vật · giữ: chế độ tinh" if self.cam else "A: nhấp = dùng · giữ = menu · đúp = hỏi AI")
        return ra

    def _rot(self, v, ct, t, dt):
        ra = []
        g = abs(ct.roll)
        if v.loai not in ("coc", "ong_dong", "ong_nghiem", "binh") or v.the_tich <= 0 or g < GOC_BAT_DAU_ROT:
            return ra
        if v.loai == "binh" and v.nap is not None and not v.mo_nap:
            return ra
        q = LUU_LUONG_MAX * ((min(g, 90) - GOC_BAT_DAU_ROT) / (90 - GOC_BAT_DAU_ROT)) ** 1.6
        dv = min(v.the_tich, q * dt)
        v.the_tich -= dv
        # vòi: mép miệng phía nghiêng, hạ theo góc
        truc = v.nghieng[0]
        phai = np.cross(np.array([0, 1.0, 0]), truc)
        voi = v.dinh() + phai * math.copysign(v.mieng, ct.roll) * math.cos(math.radians(g) * 0.5)
        nhan = None
        for u in self.c.vat:
            if u is v or u.loai not in ("coc", "ong_dong", "ong_nghiem", "binh"):
                continue
            if u.loai == "binh" and u.nap is not None and not u.mo_nap:
                continue
            dinh_u = u.dinh()
            if math.hypot(voi[0] - dinh_u[0], voi[2] - dinh_u[2]) < u.mieng + 0.008 and dinh_u[1] - 0.03 < voi[1] < dinh_u[1] + 0.3:
                nhan = u
                break
        if nhan is not None:
            vao = min(dv, max(0.0, nhan.suc_chua - nhan.the_tich))
            nhan.the_tich += vao
            nhan.mau_dd = v.mau_dd
            self.tran += dv - vao
            day = np.array([voi[0], nhan.dinh()[1] - nhan.h * 0.3, voi[2]])
        else:
            self.tran += dv
            day = np.array([voi[0], self.c.mat_duoi(voi[0], voi[2]), voi[2]])
        self.dong_chay.append((voi, day, q))
        ra.append(("rot", (v.ten, nhan.ten if nhan else None, dv)))
        return ra

    def _lac_khuay(self, v, cu_chi, t, dt):
        ra = []
        if v.loai == "ong_nghiem" and cu_chi.lac:
            self.dang_lac = True
            if v.chat_ran > 0:
                v.chat_ran = max(0.0, v.chat_ran - dt / THOI_GIAN_TAN)
                if v.chat_ran == 0:
                    self.bao(t, f"Chất rắn trong {v.ten} đã tan hết")
                    ra.append(("tan", v.ten))
            ra.append(("lac", v.ten))
        if v.loai == "dua":
            dau = v.p
            for u in self.c.vat:
                if u.loai == "coc" and math.hypot(dau[0] - u.p[0], dau[2] - u.p[2]) < u.r and dau[1] < u.p[1] + u.h:
                    if cu_chi.vong > self.khuay_vong:
                        self.khuay_vong = cu_chi.vong
                        ra.append(("khuay_vong", u.ten))
                        if u.chat_ran > 0:
                            u.chat_ran = max(0.0, u.chat_ran - 1 / VONG_KHUAY_TAN)
                            if u.chat_ran <= 1e-9:
                                u.chat_ran = 0.0
                                self.bao(t, f"Chất rắn trong {u.ten} đã tan hết")
                                ra.append(("tan", u.ten))
                    break
            else:
                self.khuay_vong = cu_chi.vong
        return ra


# ---------------------------------------------------------------- vẽ cảnh tĩnh
def luoi_phong():
    """Danh sách (lưới, R, T) của phòng: sàn, tường, 2 bàn thí nghiệm."""
    ra = [(hop(12, 0.02, 12, (196, 204, 210)), None, (1.5, -0.01, 2.0)),
          (hop(12, 3.0, 0.1, (226, 232, 236)), None, (1.5, 1.5, 2.7)),
          (hop(0.1, 3.0, 8, (214, 222, 228)), None, (-2.6, 1.5, 1.0))]
    for b in (Ban(-1.3, 1.3, 0.55, 1.15), Ban(2.7, 4.3, 0.55, 1.15)):
        cx, cz = (b.x0 + b.x1) / 2, (b.z0 + b.z1) / 2
        ra.append((hop(b.x1 - b.x0, 0.04, b.z1 - b.z0, MAU_MAT_BAN), None, (cx, b.y - 0.02, cz)))
        ra.append((hop(b.x1 - b.x0 - 0.06, 0.82, b.z1 - b.z0 - 0.06, MAU_BAN, day=True), None, (cx, 0.0, cz)))
    ra.append((hop(3.2, 1.1, 0.03, (34, 52, 64)), None, (0.0, 1.6, 2.64)))  # bảng bia trên tường
    return ra
