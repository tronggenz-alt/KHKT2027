"""Cảnh phòng thử và luật tương tác của tay cầm (bản đồ tương tác, PDF tích hợp trang 4).

Mọi luật nằm ở đây để Unity (G2) chép lại đúng: trỏ (có hỗ trợ ngắm, tầm với), cầm / thả (chống tuột tay khi cảm ứng
B chập chờn), kéo xa gần, xoay vật, rót, nhỏ pipet (hút / nhỏ từng giọt), lắc, khuấy, vặn nắp, chế độ tinh,
bảng menu trên tường, menu tròn, căn lại, đi lại (không xuyên bàn, kệ, tường), gợi ý khi ngón tay lơ lửng.
"""
import math
from collections import deque

import numpy as np

from ve3d import hop, ma_tran_truc, ma_tran_yaw, tam, tam_dung, tron_mau, tru

# ---------------------------------------------------------------- thông số (giống phần cài đặt Unity sẽ có)
TOC_DI = 1.2             # m/s, gạt hết cần
TOC_QUAY = 75.0          # °/s
TOC_KEO = 0.6            # m/s, chế độ TAY lên/xuống = xa/gần
TOC_XOAY_VAT = 120.0     # °/s, chế độ TAY trái/phải
DO_NHAY_TINH = 0.25      # chế độ tinh: giữ A khi đang cầm
DO_NHAY_PIPET = 0.35     # đầu pipet ở gần miệng lọ / ống: tự vào chế độ tinh
GOC_BAT_DAU_ROT = 35.0   # ° nghiêng (roll) mới bắt đầu chảy
LUU_LUONG_MAX = 40.0     # mL/s ở 90°
GIU_MENU = 0.6           # s giữ A để mở menu tròn (bằng ngưỡng "giữ" của nút)
VONG_VAN_MO = 720.0      # ° vặn để mở nắp
THOI_GIAN_TAN = 2.0      # s lắc để tan hết chất rắn
VONG_KHUAY_TAN = 3       # vòng khuấy để tan
BAN_KINH_BAT = 0.025     # m nới rộng vùng bắt khi trỏ (dễ trỏ vật nhỏ)
HO_TRO_NGAM = 4.0        # ° tia lệch khỏi vật chưa quá chừng này vẫn tính là trỏ trúng (vật gần tia nhất)
TAM_VOI = 1.7            # m, xa hơn thì phải đi lại gần mới cầm được
CHO_NHA_B = 0.10         # s, B phải tắt liền chừng này mới thả vật (cảm ứng chập chờn 1 khung không làm rơi)
CHO_BAT_B = 0.30         # s sau khi chạm B, tia mới quét tới vật vẫn cầm được
GIOT_ML = 0.05           # mL mỗi giọt pipet
PIPET_ML = 1.0           # sức chứa pipet
TOC_HUT = 1.2            # mL/s khi giữ A với đầu pipet trong lọ

CAM_SANG = (232, 236, 240)
HOA_CHAT_MENU = ["HCl 0,1 M", "NaOH 0,1 M", "Nước cất", "Phenolphtalein", "CuSO₄ 0,5 M", "AgNO₃ 0,1 M"]
# tên, màu dung dịch, màu nắp, màu thuỷ tinh lọ
HOA_CHAT = {
    "HCl 0,1 M": ((228, 236, 242), (220, 70, 70), (190, 215, 228)),
    "NaOH 0,1 M": ((228, 236, 242), (70, 110, 220), (190, 215, 228)),
    "Nước cất": ((215, 232, 245), (90, 170, 230), (190, 215, 228)),
    "Phenolphtalein": ((240, 240, 236), (230, 90, 170), (190, 215, 228)),
    "CuSO₄ 0,5 M": ((50, 120, 225), (240, 180, 60), (190, 215, 228)),
    "AgNO₃ 0,1 M": ((232, 232, 228), (60, 60, 66), (140, 92, 52)),
}
MAU_HONG = (236, 72, 160)   # phenolphtalein trong kiềm
LOAI_CHUA = ("coc", "ong_dong", "ong_nghiem", "binh", "lo")


def wrap(g):
    return (g + 180) % 360 - 180


class Vat:
    """Vật cầm được. Gốc ở tâm đáy. Dung tích mL; the_tich là chất lỏng đang có."""

    def __init__(self, ten, loai, x, z, r, h, mau, suc_chua=0.0, the_tich=0.0, mau_dd=(70, 140, 220), y=0.9,
                 nhan=None, hoa_chat=None, mau_nap=(60, 60, 66)):
        self.ten, self.loai = ten, loai
        self.p = np.array([x, y, z], float)
        self.r, self.h = r, h
        self.mau = mau
        self.suc_chua, self.the_tich, self.mau_dd = suc_chua, the_tich, mau_dd
        self.nhan = nhan              # chữ hiện trên vật (nhãn lọ, số ống)
        self.hoa_chat = hoa_chat
        self.giot_chi_thi = 0         # số giọt phenolphtalein đã nhận
        self.mau_goc = mau_dd
        self.yaw = 0.0
        self.nghieng = (np.array([0, 0, 1.0]), 0.0)  # (trục, góc) khi đang cầm và lăn cổ tay
        self.chat_ran = 0.0    # 0..1 phần chất rắn chưa tan
        self.nap = None        # góc đã vặn (°) nếu có nắp vặn; None = không nắp
        self.mo_nap = False
        self.dang_cam = False
        self.ro = False        # vừa rơi xuống sàn
        self.h_mieng = h
        self.dong_lo = loai == "lo"   # lọ hoá chất đang đậy nắp (không rót, không hút được)
        kinh = loai in LOAI_CHUA or loai == "pipet"
        if loai == "binh":
            self.luoi = [tru(r, h * 0.62, mau, n=20, kinh=True, nap_tren=False),
                         tru(r * 0.36, h * 0.38, mau, n=14, kinh=True, nap_duoi=False, r_tren=r * 0.3)]
            self.luoi_dinh = [0.0, h * 0.62]
        elif loai == "dua":
            self.luoi = [tru(r, h, (200, 220, 230), n=6)]
            self.luoi_dinh = [0.0]
        elif loai == "lo":
            self.h_mieng = h * 0.86
            self.luoi = [tru(r, h * 0.66, mau, n=18, kinh=True, nap_tren=False),
                         tru(r, h * 0.12, mau, n=18, kinh=True, nap_tren=False, nap_duoi=False, r_tren=r * 0.5),
                         tru(r * 0.5, h * 0.08, mau, n=14, kinh=True, nap_tren=False, nap_duoi=False),
                         tru(r * 1.02, h * 0.26, (246, 246, 240), n=18, nap_tren=False, nap_duoi=False)]
            self.luoi_dinh = [0.0, h * 0.66, h * 0.78, h * 0.2]
            self.luoi_nap = tru(r * 0.56, h * 0.14, mau_nap, n=14)
        elif loai == "pipet":
            self.luoi = [tru(r * 0.3, h * 0.16, mau, n=8, kinh=True, nap_duoi=False, nap_tren=False, r_tren=r),
                         tru(r, h * 0.64, mau, n=8, kinh=True, nap_duoi=False, nap_tren=False),
                         tru(r * 2.3, h * 0.14, (214, 60, 60), n=12, r_tren=r * 1.8),
                         tru(r * 1.8, h * 0.06, (200, 52, 52), n=12, r_tren=r * 0.6)]
            self.luoi_dinh = [0.0, h * 0.16, h * 0.8, h * 0.94]
        else:
            self.luoi = [tru(r, h, mau, n=20, kinh=kinh, nap_tren=False)]
            self.luoi_dinh = [0.0]

    @property
    def mieng(self):
        """Bán kính miệng (bình nón, lọ: cổ hẹp)."""
        if self.loai == "binh":
            return self.r * 0.3
        if self.loai == "lo":
            return self.r * 0.5
        return self.r

    def R(self):
        truc, goc = self.nghieng
        return ma_tran_truc(truc, goc) @ ma_tran_yaw(self.yaw)

    def dinh(self):
        """Tâm miệng trên cùng (thế giới)."""
        return self.p + self.R() @ np.array([0, self.h_mieng, 0])

    def tam(self):
        return self.p + self.R() @ np.array([0, self.h / 2, 0])

    def muc_long(self):
        """(bán kính, chiều cao) khối chất lỏng bên trong, theo thể tích; None nếu rỗng."""
        if self.suc_chua <= 0 or self.the_tich <= 1e-3:
            return None
        k = min(1.0, self.the_tich / self.suc_chua)
        if self.loai == "pipet":
            return self.r * 0.7, max(0.004, self.h * 0.78 * k)
        h_day = {"binh": 0.62, "lo": 0.64}.get(self.loai, 0.95)
        return self.r * 0.9, max(0.002, self.h * h_day * k)


class Ban:
    def __init__(self, x0, x1, z0, z1, y=0.9):
        self.x0, self.x1, self.z0, self.z1, self.y = x0, x1, z0, z1, y

    def chua(self, x, z, le=0.0):
        return self.x0 - le <= x <= self.x1 + le and self.z0 - le <= z <= self.z1 + le


class BangMenu:
    """Bảng chọn trên tường (bài trỏ menu): các ô chữ nhật ở mặt phẳng z; trỏ tia vào ô rồi chạm A."""

    def __init__(self, muc, cx=0.0, cy=1.55, z=2.62, w=0.66, h=0.26, khe=0.09, cot=3):
        self.z = z
        self.o = []
        hang = math.ceil(len(muc) / cot)
        for i, ten in enumerate(muc):
            c, r = i % cot, i // cot
            self.o.append((ten, cx + (c - (cot - 1) / 2) * (w + khe), cy + ((hang - 1) / 2 - r) * (h + khe), w, h))
        self.tro = None       # ô đang trỏ
        self.dich = None      # ô cần chọn (tô viền vàng)
        self.nhay = {}        # ô -> (t, đúng?) vừa chọn, để nháy màu

    def cat(self, o, d):
        """(chỉ số ô trúng hoặc None, điểm trên mặt bảng hoặc None)."""
        if d[2] <= 1e-6:
            return None, None
        t = (self.z - o[2]) / d[2]
        if t <= 0:
            return None, None
        P = o + t * d
        for i, (_, x, y, w, h) in enumerate(self.o):
            if abs(P[0] - x) <= w / 2 and abs(P[1] - y) <= h / 2:
                return i, P
        return None, P


# ---------------------------------------------------------------- bố cục phòng
BAN_1 = Ban(-1.3, 1.3, 0.55, 1.15)
BAN_2 = Ban(2.7, 4.3, 0.55, 1.15)
KE = (-2.6, -2.28, 0.35, 1.65)            # x0, x1, z0, z1 của kệ hoá chất sát tường trái
TANG_KE = (0.80, 1.20)                    # mặt trên 2 tầng kệ
TUONG = (-2.6, 5.0, -2.0, 2.7)            # x0, x1, z0, z1
BANG = (0.0, 1.62, 2.66, 2.7, 0.92)       # x, y, z, rộng, cao của bảng trên tường sau


class Canh:
    def __init__(self):
        self.ban = [BAN_1, BAN_2]
        x0, x1, z0, z1 = KE
        self.mat_dat = [(b.x0, b.x1, b.z0, b.z1, b.y) for b in self.ban] + [(x0, x1, z0, z1, y) for y in TANG_KE]
        self.vat_can = [(b.x0, b.x1, b.z0, b.z1) for b in self.ban] + [KE]
        self.dat_lai()

    def dat_lai(self):
        self.vat, self.o_dat, self.bia, self.bia_sang, self.moc_san = [], [], [], None, None
        self.bang_menu = None
        self.trang_tri = []       # lưới trang trí của bài: (lưới, R, T)
        self.cho_menu = False     # bài nào cần menu tròn mới bật
        self.khoa_di = False      # bài kiểm tra cảm biến: gạt cần không làm người đi
        self.o_sang = None        # ô đặt đang cần (vẽ nổi bật)
        self.cam_pitch = -24.0    # góc cúi camera mà bài muốn
        self.mat_tuong_z = 2.6
        self.cam_vi_tri = np.array([0.0, 1.6, 0.0])
        self.cam_yaw = 0.0
        self.s = None
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

    def mat_duoi(self, x, z, y_tu=9.0):
        """Mặt đỡ cao nhất (bàn, tầng kệ) dưới điểm (x, z) mà không cao hơn y_tu + 5 cm; 0 là sàn."""
        tot = 0.0
        for x0, x1, z0, z1, y in self.mat_dat:
            if x0 <= x <= x1 and z0 <= z <= z1 and y <= y_tu + 0.05:
                tot = max(tot, y)
        return tot

    def di_duoc(self, x, z):
        """Người (bán kính 0,3 m) đứng được ở (x, z): không xuyên bàn, kệ, tường."""
        le = 0.3
        tx0, tx1, tz0, tz1 = TUONG
        if not (tx0 + le <= x <= tx1 - le and tz0 + le <= z <= tz1 - le):
            return False
        return not any(x0 - le <= x <= x1 + le and z0 - le <= z <= z1 + le for x0, x1, z0, z1 in self.vat_can)

    def cat_vat(self, o, d, bo_qua=None):
        """Tia cắt vật gần nhất: (t, vật, 'nap' hoặc 'than') hoặc None.
        Không cắt trúng vật nào thì lấy vật có tâm / miệng lệch khỏi tia ít nhất nếu dưới HO_TRO_NGAM độ."""
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
        if tot is not None:
            return tot
        gan = None
        for v in self.vat:
            if v is bo_qua or v.dang_cam:
                continue
            for phan, P in (("than", v.tam()), ("nap" if v.nap is not None and not v.mo_nap else "than", v.dinh())):
                u = P - o
                L = float(np.linalg.norm(u))
                if L < 1e-6:
                    continue
                g = math.degrees(math.acos(max(-1.0, min(1.0, float(u @ d) / L))))
                if g <= HO_TRO_NGAM and (gan is None or g < gan[0]):
                    gan = (g, L, v, phan)
        return (gan[1], gan[2], gan[3]) if gan else None

    def cat_ban(self, o, d):
        """Điểm tia chạm mặt bàn / tầng kệ / sàn."""
        tot = None
        if d[1] < -1e-6:
            for x0, x1, z0, z1, y in self.mat_dat:
                t = (y - o[1]) / d[1]
                q = o + t * d
                if t > 0 and x0 <= q[0] <= x1 and z0 <= q[2] <= z1 and (tot is None or t < tot):
                    tot = t
            if tot is None:
                tot = -o[1] / d[1]
        return tot

    def cat_tuong(self, o, d):
        """Điểm tia chạm tường gần nhất (sau, trái, phải)."""
        tx0, tx1, _, tz1 = TUONG
        ts = []
        if d[2] > 1e-6:
            ts.append((tz1 - 0.04 - o[2]) / d[2])
        if d[0] < -1e-6:
            ts.append((tx0 - o[0]) / d[0])
        if d[0] > 1e-6:
            ts.append((tx1 - o[0]) / d[0])
        ts = [t for t in ts if t > 0]
        return o + min(ts) * d if ts else None


class TuongTac:
    """Máy trạng thái tương tác. cap_nhat nhận trạng thái tay cầm, con trỏ, sự kiện cử chỉ; trả về sự kiện cảnh."""

    def __init__(self, canh):
        self.c = canh
        self.che_do = "DI"
        self.cam = None              # vật đang cầm
        self.kc = 0.5                # khoảng cách giữ (m)
        self.tam_cam = None          # tâm vật đang cầm (đi theo điểm cuối tia)
        self.tro = None              # (t, vật, phần) đang trỏ
        self.tro_xa = False          # vật đang trỏ ở xa quá tầm với
        self.diem_tro = None         # điểm tia chạm
        self.van = None              # vật đang vặn nắp
        self.roll_van = None
        self.so_lan_van = 0
        self.menu_mo = False
        self.menu_chon = None
        self.t_a_bam = None
        self.t_b_tat = None
        self.tinh = False
        self.dong_chay = []          # dòng rót để vẽ: (điểm đầu, điểm cuối, lưu lượng)
        self.giot = []               # giọt pipet đang rơi: [vị trí, vận tốc rơi, y chạm, màu]
        self.tran = 0.0              # mL rót ra ngoài
        self.thong_bao = deque(maxlen=4)
        self.goi_y = None
        self.so_lan_doi_che_do = 0
        self.so_lan_roi = 0
        self.khuay_vong = 0
        self.dang_lac = False
        self.dang_hut = False
        self._t_bao_xa = -9.0

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
        # ---- nhấp đúp A: hỏi AI (không khi đang cầm pipet: A là nhỏ giọt)
        if "A_nhap_dup" in ev and self.cam is None:
            self.bao(t, "Hỏi trợ lý AI (giọng nói)…")
            ra.append(("hoi_ai", None))
        if "vuot_AB" in ev:
            ra.append(("vuot", "tiep"))
        if "vuot_BA" in ev:
            ra.append(("vuot", "lui"))

        # ---- đi lại / kéo, xoay vật
        if self.che_do == "DI" and not self.menu_mo and not c.khoa_di:
            c.cam_yaw -= jx * TOC_QUAY * dt
            buoc = ma_tran_yaw(c.cam_yaw) @ np.array([0, 0, jy * TOC_DI * dt])
            moi = c.cam_vi_tri + buoc
            if c.di_duoc(moi[0], moi[2]):
                c.cam_vi_tri = moi
            elif c.di_duoc(moi[0], c.cam_vi_tri[2]):   # trượt dọc vật cản thay vì đứng khựng
                c.cam_vi_tri = np.array([moi[0], moi[1], c.cam_vi_tri[2]])
            elif c.di_duoc(c.cam_vi_tri[0], moi[2]):
                c.cam_vi_tri = np.array([c.cam_vi_tri[0], moi[1], moi[2]])
        elif self.che_do == "TAY" and self.cam is not None:
            self.kc = float(np.clip(self.kc + jy * TOC_KEO * dt, 0.2, 1.4))
            if self.cam.loai != "pipet":
                self.cam.yaw += jx * TOC_XOAY_VAT * dt

        o = c.tay()
        d = c.huong_tia(*ct.ra)
        # ---- chế độ tinh: đang cầm + giữ A; riêng pipet (A là nhỏ giọt) thì tự bật khi đầu pipet ở gần miệng lọ / ống
        if self.cam is not None and self.cam.loai == "pipet":
            self.tinh = self._gan_mieng(self.cam.p) is not None
            ct.do_nhay = DO_NHAY_PIPET if self.tinh else 1.0
        else:
            self.tinh = self.cam is not None and s.cham_a
            ct.do_nhay = DO_NHAY_TINH if self.tinh else 1.0

        self.tro = c.cat_vat(o, d, bo_qua=self.cam) if self.van is None else self.tro
        self.tro_xa = self.tro is not None and self.tro[0] > TAM_VOI and self.cam is None
        tb = c.cat_ban(o, d)
        tw = c.cat_tuong(o, d)
        tw = float(np.linalg.norm(tw - o)) if tw is not None else None
        ts = [x for x in ((self.tro[0] if self.tro else None), tb, tw) if x is not None]
        bm = c.bang_menu
        if bm is not None:
            bm.tro, P = bm.cat(o, d)
            if P is not None:
                ts.append(float(np.linalg.norm(P - o)))
        self.diem_tro = o + (min(ts) if ts else 3.0) * d
        if self.cam is not None and self.tam_cam is not None:
            self.diem_tro = self.tam_cam.copy()   # đang cầm: tia dừng ở vật

        # ---- menu tròn: giữ A khi tay không cầm gì (chỉ bài có dùng), gạt cần chọn, nhả A để chọn
        if "A_bam" in ev:
            self.t_a_bam = t
        if (c.cho_menu and self.cam is None and s.cham_a and not s.cham_b and self.t_a_bam is not None
                and t - self.t_a_bam >= GIU_MENU and not self.menu_mo):
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
        # ---- cầm: chạm B (hoặc đang chạm B vừa xong mà tia mới quét tới vật)
        elif (s.cham_b and t - cu_chi.B.t_bam <= CHO_BAT_B and self.cam is None and not self.menu_mo
              and not s.cham_a and self.tro):
            _, v, phan = self.tro
            if self.tro_xa:
                if t - self._t_bao_xa > 2.0:
                    self._t_bao_xa = t
                    self.bao(t, f"{v.ten} ở xa quá ({self.tro[0]:.1f} m): gạt cần đi lại gần")
                    ra.append(("xa_qua", v.ten))
            elif phan == "nap":
                self.van = v
                self.roll_van = ct.roll
                self.so_lan_van += 1
                ra.append(("bat_dau_van", v.ten))
            elif v.loai == "binh" and v.nap is not None and not v.mo_nap:
                if "B_bam" in ev:
                    self.bao(t, "Bình đang đậy nắp: trỏ vào nắp để vặn")
            else:
                self.cam = v
                v.dang_cam = True
                self.kc = float(np.linalg.norm(v.tam() - o))
                self.tam_cam = v.tam()
                self.t_b_tat = None
                ra.append(("cam", v.ten))
        # ---- thả: buông B (phải tắt liền CHO_NHA_B giây, tránh rơi vật khi cảm ứng chập chờn)
        if self.cam is not None:
            if s.cham_b:
                self.t_b_tat = None
            else:
                self.t_b_tat = t if self.t_b_tat is None else self.t_b_tat
                if t - self.t_b_tat >= CHO_NHA_B:
                    ra += self._tha(t)

        # ---- vật đang cầm đi theo tia, nghiêng theo cổ tay
        self.dong_chay = []
        self.dang_lac = False
        self.dang_hut = False
        if self.cam is not None:
            v = self.cam
            ngang = np.array([d[0], 0, d[2]])
            ngang /= max(np.linalg.norm(ngang), 1e-9)
            # roll dương = lăn cổ tay sang PHẢI thì miệng vật phải đổ sang phải (cùng phía vòi trong _rot).
            # Rodrigues quanh trục tới trước với góc dương đưa đỉnh sang −x (trái), nên phải đổi dấu (sửa 11/11/2026).
            # Pipet luôn dựng đứng (đầu nhỏ giọt chĩa xuống).
            v.nghieng = (ngang, 0.0 if v.loai == "pipet" else float(np.clip(-ct.roll, -95, 95)))
            # tay nắm ở TÂM vật: nghiêng quanh tâm, tâm đi theo điểm cuối tia
            muc_tieu = o + d * self.kc
            self.tam_cam = muc_tieu if self.tam_cam is None else self.tam_cam + (muc_tieu - self.tam_cam) * min(1.0, dt * 25)
            v.p = self.tam_cam - v.R() @ np.array([0, v.h / 2, 0])
            if v.loai == "pipet":
                ra += self._pipet(v, s, ev, t, dt)
            else:
                ra += self._rot(v, ct, t, dt)
                ra += self._lac_khuay(v, cu_chi, t, dt)
                if "A_nhap" in ev:
                    ra.append(("dung", v.ten))
        self._roi_giot(dt)
        # ---- gợi ý khi ngón cái lơ lửng trên A
        self.goi_y = None
        if s.muc_a >= 38 and not s.cham_a:
            if self.cam is not None and self.cam.loai == "pipet":
                self.goi_y = "A: nhỏ 1 giọt · giữ A trong lọ: hút"
            elif self.cam is not None:
                self.goi_y = "A: dùng vật · giữ: chế độ tinh"
            else:
                self.goi_y = "A: chọn" + (" · giữ: menu tròn" if c.cho_menu else "")
        return ra

    def _tha(self, t):
        c = self.c
        v = self.cam
        v.dang_cam = False
        tam_v = v.tam()
        v.nghieng = (np.array([0, 0, 1.0]), 0.0)
        v.p = tam_v - np.array([0, v.h / 2, 0])
        self.tam_cam = None
        self.t_b_tat = None
        y = c.mat_duoi(v.p[0], v.p[2], v.p[1])
        v.p[1] = y
        if y == 0.0:
            v.ro = True
            self.so_lan_roi += 1
            self.bao(t, f"{v.ten} rơi xuống sàn!")
        self.cam = None
        return [("tha", v.ten)]

    def _gan_mieng(self, dau, ban_kinh=0.05, cao=0.12):
        """Vật chứa có miệng gần đầu pipet nhất (ngang ≤ ban_kinh, đầu không cao hơn miệng quá 'cao'): (vật, khoảng ngang)."""
        tot = None
        for u in self.c.vat:
            if u is self.cam or u.loai not in LOAI_CHUA:
                continue
            m = u.dinh()
            kc = math.hypot(dau[0] - m[0], dau[2] - m[2])
            if kc <= ban_kinh and u.p[1] - 0.01 <= dau[1] <= m[1] + cao and (tot is None or kc < tot[1]):
                tot = (u, kc)
        return tot

    def _pipet(self, v, s, ev, t, dt):
        """Pipet: đầu trong lọ + giữ A = hút; ra ngoài, mỗi lần chạm A = 1 giọt rơi thẳng xuống."""
        ra = []
        dau = v.p.copy()
        gan = self._gan_mieng(dau)
        trong_lo = (gan is not None and gan[0].loai == "lo" and not gan[0].dong_lo and gan[1] <= gan[0].mieng + 0.012
                    and dau[1] <= gan[0].dinh()[1] + 0.015)
        if trong_lo and s.cham_a and gan[0].the_tich > 0 and v.the_tich < v.suc_chua:
            lo = gan[0]
            dv = min(TOC_HUT * dt, lo.the_tich, v.suc_chua - v.the_tich)
            lo.the_tich -= dv
            if v.the_tich < 1e-6:
                v.hoa_chat, v.mau_dd = lo.hoa_chat, lo.mau_dd
            v.the_tich += dv
            self.dang_hut = True
            if v.the_tich >= v.suc_chua - 1e-6 and dv > 0:
                ra.append(("hut_day", v.ten))
            ra.append(("hut", (lo.ten, dv)))
        elif "A_bam" in ev and not trong_lo:
            if v.the_tich < GIOT_ML * 0.5:
                self.bao(t, "Pipet rỗng: đưa đầu pipet vào lọ, giữ A để hút")
                ra.append(("giot_rong", None))
            else:
                dv = min(GIOT_ML, v.the_tich)
                v.the_tich -= dv
                dich = None
                for u in self.c.vat:
                    if u is v or u.loai not in LOAI_CHUA:
                        continue
                    m = u.dinh()
                    if math.hypot(dau[0] - m[0], dau[2] - m[2]) <= u.mieng * 0.85 and dau[1] >= u.p[1]:
                        dich = u
                        break
                gan_nhat = self._gan_mieng(dau, 0.3, 1.0)
                lech = gan_nhat[1] if gan_nhat else float("nan")
                if dich is not None:
                    y_cham = dich.p[1] + (dich.muc_long() or (0, 0.002))[1]
                    vao = min(dv, max(0.0, dich.suc_chua - dich.the_tich))
                    dich.the_tich += vao
                    if v.hoa_chat == "Phenolphtalein" and dich.hoa_chat == "NaOH 0,1 M":
                        dich.giot_chi_thi += 1
                        dich.mau_dd = tron_mau(dich.mau_goc, MAU_HONG, 0.35 + 0.13 * dich.giot_chi_thi)
                else:
                    y_cham = self.c.mat_duoi(dau[0], dau[2], dau[1])
                self.giot.append([dau.copy(), 0.0, y_cham, v.mau_dd])
                ra.append(("giot", (dich.ten if dich else None, lech, gan_nhat[0].ten if gan_nhat else None)))
        return ra

    def _roi_giot(self, dt):
        con = []
        for g in self.giot:
            g[1] += 9.8 * dt
            g[0][1] -= g[1] * dt
            if g[0][1] > g[2]:
                con.append(g)
        self.giot = con

    def _rot(self, v, ct, t, dt):
        ra = []
        g = abs(ct.roll)
        if v.loai not in LOAI_CHUA or v.the_tich <= 0 or g < GOC_BAT_DAU_ROT:
            return ra
        if (v.loai == "binh" and v.nap is not None and not v.mo_nap) or v.dong_lo:
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
            if u is v or u.loai not in LOAI_CHUA:
                continue
            if (u.loai == "binh" and u.nap is not None and not u.mo_nap) or u.dong_lo:
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
            day = np.array([voi[0], nhan.p[1] + (nhan.muc_long() or (0, 0.0))[1], voi[2]])
        else:
            self.tran += dv
            day = np.array([voi[0], self.c.mat_duoi(voi[0], voi[2], voi[1]), voi[2]])
        self.dong_chay.append((voi, day, q, v.mau_dd))
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
MAU_SAN = ((176, 184, 190), (190, 197, 202))
MAU_TUONG = (226, 229, 230)
MAU_TUONG_BEN = (214, 219, 221)
MAU_MAT_BAN = (58, 66, 76)
MAU_TU = (226, 230, 234)
MAU_CHAN_TU = (70, 80, 90)
MAU_GO = (170, 128, 88)


def _tu_ban(b, so_canh):
    """Bàn thí nghiệm: mặt đá tối, tủ trắng có cánh và tay nắm."""
    ra = []
    cx, cz = (b.x0 + b.x1) / 2, (b.z0 + b.z1) / 2
    rong, sau = b.x1 - b.x0, b.z1 - b.z0
    ra.append((hop(rong + 0.04, 0.04, sau + 0.04, MAU_MAT_BAN, day=True), None, (cx, b.y - 0.04, cz)))
    ra.append((hop(rong - 0.06, 0.78, sau - 0.1, MAU_TU, day=True), None, (cx, 0.08, cz)))
    ra.append((hop(rong - 0.12, 0.08, sau - 0.16, MAU_CHAN_TU, day=True), None, (cx, 0.0, cz)))
    w = (rong - 0.1) / so_canh
    z_mat = b.z0 + 0.05 - 0.006
    for i in range(so_canh):
        x = b.x0 + 0.05 + w * (i + 0.5)
        ra.append((hop(w - 0.03, 0.68, 0.012, (238, 241, 244)), None, (x, 0.47, z_mat)))
        ra.append((hop(0.012, 0.12, 0.02, (120, 130, 140)), None, (x + w * 0.32 * (1 if i % 2 else -1), 0.66, z_mat - 0.012)))
    return ra


def luoi_phong():
    """Danh sách (lưới, R, T) của phòng: sàn lát gạch, tường, trần có đèn, 2 bàn thí nghiệm, kệ hoá chất, bảng, cửa sổ."""
    ra = []
    tx0, tx1, tz0, tz1 = TUONG
    o = 0.6
    nx, nz = int(round((tx1 - tx0) / o)), int(round((tz1 - tz0) / o))
    for i in range(nx):
        for k in range(nz):
            ra.append((tam(o, o, MAU_SAN[(i + k) % 2]), None, (tx0 + o * (i + 0.5), 0.0, tz0 + o * (k + 0.5))))
    cx, cz = (tx0 + tx1) / 2, (tz0 + tz1) / 2
    ra.append((hop(tx1 - tx0, 3.0, 0.05, MAU_TUONG), None, (cx, 1.5, tz1 + 0.025)))                 # tường sau
    ra.append((hop(0.05, 3.0, tz1 - tz0, MAU_TUONG_BEN), None, (tx0 - 0.025, 1.5, cz)))             # tường trái
    ra.append((hop(0.05, 3.0, tz1 - tz0, MAU_TUONG_BEN), None, (tx1 + 0.025, 1.5, cz)))             # tường phải
    ra.append((hop(tx1 - tx0, 0.05, tz1 - tz0, (238, 240, 242)), None, (cx, 3.025, cz)))             # trần
    ra.append((hop(tx1 - tx0, 0.1, 0.012, (150, 160, 168)), None, (cx, 0.05, tz1 - 0.006)))          # chân tường
    for x in (-1.2, 1.2, 3.6):
        for z in (0.2, 1.8):
            ra.append((hop(0.9, 0.02, 0.3, (252, 252, 246)), None, (x, 2.99, z)))                  # đèn trần
    # ốp gạch men sau bàn 1 và bàn 2
    for b in (BAN_1, BAN_2):
        ra.append((hop(b.x1 - b.x0 + 0.04, 0.42, 0.012, (204, 228, 226)), None, ((b.x0 + b.x1) / 2, b.y + 0.21, tz1 - 0.006)))
    for b, n in ((BAN_1, 4), (BAN_2, 3)):
        ra += _tu_ban(b, n)
    # bảng tối trên tường sau (bảng menu của bài trỏ menu), viền màu nhấn
    bx, by, bz, bw, bh = BANG
    ra.append((hop(bw + 0.08, bh + 0.08, 0.02, (56, 214, 196)), None, (bx, by, bz + 0.012)))
    ra.append((hop(bw, bh, 0.03, (40, 58, 70)), None, (bx, by, bz)))
    # cửa sổ trên tường sau, phía bàn 2
    ra.append((hop(1.9, 1.2, 0.02, (110, 120, 128)), None, (3.5, 1.95, tz1 - 0.01)))
    for i in range(3):
        ra.append((hop(0.56, 1.08, 0.012, (178, 214, 238)), None, (2.9 + 0.6 * i, 1.95, tz1 - 0.022)))
    # kệ hoá chất sát tường trái
    x0, x1, z0, z1 = KE
    xc, zc = (x0 + x1) / 2, (z0 + z1) / 2
    for z in (z0 + 0.015, z1 - 0.015):
        for x in (x0 + 0.015, x1 - 0.015):
            ra.append((hop(0.03, 1.62, 0.03, MAU_GO), None, (x, 0.81, z)))
    for y in (0.12,) + TANG_KE + (1.6,):
        ra.append((hop(x1 - x0, 0.025, z1 - z0, MAU_GO, day=True), None, (xc, y - 0.025, zc)))
    ra.append((hop(0.01, 1.6, z1 - z0, (150, 112, 78)), None, (x0 + 0.005, 0.8, zc)))
    return ra


def luoi_gia_ong(x0, x1, z, y=0.9):
    """Giá ống nghiệm (trang trí): đế, 2 cột, thanh sau."""
    xc = (x0 + x1) / 2
    w = x1 - x0 + 0.06
    mau = (228, 232, 236)
    return [(hop(w, 0.012, 0.07, mau, day=True), None, (xc, y, z)),
            (hop(0.012, 0.08, 0.07, mau, day=True), None, (x0 - 0.03, y, z)),
            (hop(0.012, 0.08, 0.07, mau, day=True), None, (x1 + 0.03, y, z)),
            (hop(w, 0.012, 0.012, mau, day=True), None, (xc, y + 0.068, z + 0.03))]


def lo_hoa_chat(ten, x, z, y=0.9, r=0.03, h=0.13, the_tich=80.0):
    """Lọ hoá chất có nhãn và nắp màu theo HOA_CHAT."""
    mau_dd, mau_nap, mau_kinh = HOA_CHAT[ten]
    return Vat(ten, "lo", x, z, r, h, mau_kinh, 100.0, the_tich, mau_dd=mau_dd, y=y, nhan=ten, hoa_chat=ten, mau_nap=mau_nap)
