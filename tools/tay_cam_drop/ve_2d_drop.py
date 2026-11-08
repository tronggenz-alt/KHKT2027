"""Bản vẽ 2D tay cầm VERA Drop, chiếu và cắt thẳng từ mô hình 3D trong mo_hinh_drop.py.

Chạy:  python tools/tay_cam_drop/ve_2d_drop.py
Ra: docs/tay-cam-drop/TCD-01..06.svg (rồi xuất PNG bằng tools/tay_cam_drop/xuat_png.sh)
"""
import json
import math
import os
import sys

import numpy as np

DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, DIR)
sys.path.insert(0, os.path.join(os.path.dirname(DIR), "tay_cam_v2"))
sys.path.insert(0, os.path.join(os.path.dirname(DIR), "ve_tay_cam"))
import mo_hinh_drop as mh  # noqa: E402
from ve_2d import Svg, fmt  # noqa: E402
from ve_2d_v2 import THEM_CSS, bb, cung_goc, to_mau, tu_truong  # noqa: E402

THU_MUC = mh.THU_MUC
R_CANH = [[1, 0, 0], [0, 0, 1], [0, -1, 0]]     # nhìn từ sườn phải: x sang phải, z lên
R_TRUOC = [[0, 1, 0], [0, 0, 1], [1, 0, 0]]     # nhìn từ trước: y sang phải, z lên
R_TREN = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]      # nhìn từ trên: x sang phải, y lên
R_DAY = [[1, 0, 0], [0, -1, 0], [0, 0, -1]]     # nhìn từ đáy: x sang phải, -y lên
MAU_LK = {"esp32": "#1d2b33", "pin": "#8a9aa3", "tp4056": "#2563a6", "ky023": "#30353a", "bmi160": "#7b4fd6",
          "hall": "#c0392b", "nam_cham": "#9aa3a8", "cong_tac": "#3b4a52"}
CSS_DROP = THEM_CSS + """
.trang{fill:#f4f7f8;stroke:var(--ink,#18272c);stroke-width:1.1;fill-rule:evenodd}
.nhat{fill:#dfe7e9;stroke:var(--ink-2,#5d6e72);stroke-width:.6;fill-rule:evenodd}
.chu{fill:#15191d;fill-rule:evenodd}
.day{fill:none;stroke:#b4471a;stroke-width:3;stroke-linecap:round;stroke-linejoin:round}
"""


def _ra_list(cs):
    return [[[round(float(x), 2), round(float(y), 2)] for x, y in poly] for poly in cs.to_polygons()]


def chieu(m, R):
    return _ra_list(m.transform(mh.bien_doi(np.array(R, float), [0, 0, 0])).project().simplify(0.02))


def cat_y(m, y):
    """Cắt tại mặt phẳng y; x sang phải, z lên."""
    return _ra_list(m.transform(mh.bien_doi(np.array(R_CANH, float), [0, 0, 0])).slice(-y).simplify(0.01))


def cat_z(m, z):
    """Cắt tại độ cao z; x sang phải, y lên."""
    return _ra_list(m.slice(z).simplify(0.01))


def guong(polys):
    return [[[-x, y] for x, y in p] for p in polys]


def so(v, nd=1):
    """Số kiểu Việt Nam: dấu phẩy thập phân, bỏ số 0 thừa."""
    t = f"{v:.{nd}f}".rstrip("0").rstrip(".") if nd else f"{v:.0f}"
    return t.replace(".", ",")


class Mo:
    """Gom các khối 3D một lần vì chiếu và cắt mất vài giây."""

    def __init__(self):
        self.ct = {x[0]: x[3] for x in mh.chi_tiet_in()}
        self.lk = {x[0]: x[3] for x in mh.khoi_linh_kien()}
        self.than = self.ct["than_phai"] + self.ct["than_trai"]
        self.tat_ca = self.than + self.ct["vong"] + self.ct["nap"] + self.ct["co"] + self.ct["thanh"] + self.ct["chen"] + self.lk["ky023"]
        self.chu = mh.chu_khoi(mh.SAU_CHU, -0.05)
        self.tay = mh.ban_tay()
        self.hb = self.tat_ca.bounding_box()
        d = mh.d_mat(mh.P_VONG) + mh.CAO_VONG
        self.nap_ngoai = self.ct["nap"].trim_by_plane(list(mh.n_mat), d)
        self.co_ngoai = self.ct["co"] ^ (mh.khoi() - mh.khoi(2.4))
        self.nam = self.lk["ky023"].trim_by_plane(list(mh.n_mat), mh.d_mat(mh.TAM_CAN))
        with open(os.path.join(THU_MUC, "thong_so.json"), encoding="utf-8") as fh:
            self.thong_so = json.load(fh)


def khung(s, so_ban, ten, phu):
    s.add(f"<style>{CSS_DROP}</style>")
    s.rect(6, 6, s.w - 12, s.h - 12, "paper", rx=4)
    s.rect(14, 14, s.w - 28, s.h - 28, "thin")
    w, h = 540, 74
    x, y = s.w - 14 - w, s.h - 14 - h
    s.rect(x, y, w, h, "ln")
    s.line(x, y + 30, x + 410, y + 30, "thin")
    s.line(x + 410, y, x + 410, y + h, "thin")
    s.line(x + 410, y + 30, x + w, y + 30, "thin")
    s.line(x + 410, y + 52, x + w, y + 52, "thin")
    s.text(x + 10, y + 21, ten, "h")
    s.text(x + 10, y + 47, phu, "ts")
    s.text(x + 10, y + 65, "Đơn vị: mm · VERA Lab, Lê Quý Đôn · 02/11/2026", "ts")
    s.text(x + 420, y + 13, "BẢN VẼ SỐ", "hs")
    s.text(x + 420, y + 27, so_ban, "tb")
    s.text(x + 420, y + 45, "Tay cầm VERA Drop", "ts")
    s.text(x + 420, y + 67, "Chiếu góc thứ nhất", "ts")


def ve_vo(s, mo, R, X, Y, nua_truoc=False):
    """Vẽ phần nhìn thấy từ ngoài: thân trắng, đầu đen, vòng xanh. nua_truoc: chỉ lấy nửa đầu quay về người nhìn."""
    nap, vong = mo.nap_ngoai, mo.ct["vong"]
    if nua_truoc:
        nap, vong = nap.trim_by_plane([1, 0, 0], 0.0), vong.trim_by_plane([1, 0, 0], 1.0)
    to_mau(s, chieu(mo.than, R), X, Y, "trang")
    to_mau(s, chieu(mo.ct["thanh"], R), X, Y, "toi")
    to_mau(s, chieu(mo.co_ngoai, R), X, Y, "toi")
    to_mau(s, chieu(mo.nam, R), X, Y, "toi")
    to_mau(s, chieu(nap, R), X, Y, "toi")
    to_mau(s, chieu(vong, R), X, Y, "hl")


# ---------------- TCD-01 hình chiếu ----------------

def tcd01(mo):
    s = Svg(1180, 820, 31)
    khung(s, "TCD-01", "TAY CẦM VERA DROP · HÌNH CHIẾU", "Chiếu từ mô hình 3D; thân 2 nửa, vách dày 1,8")
    k = 3.6
    hb = mo.hb
    a_eo, b_eo = mh.ab_tai(38)
    a_dau, b_dau = mh.ab_tai(82)
    a_day, b_day = mh.ab_tai(0)

    # hình chiếu đứng: nhìn từ sườn phải, thấy chữ khắc
    ox, oy = 500, 480
    X = lambda u: ox + u * k
    Y = lambda v: oy - v * k
    s.text(370, 46, "HÌNH CHIẾU ĐỨNG", "h")
    s.text(370, 62, "nhìn từ sườn phải, phía trước (cò) quay sang phải", "ts")
    ve_vo(s, mo, R_CANH, X, Y)
    to_mau(s, chieu(mo.chu, R_CANH), X, Y, "chu")
    s.dimh(X(hb[0]), X(hb[3]), Y(0) + 34, f"{so(hb[3] - hb[0], 0)} (kể cả thanh móc)", Y(0), Y(0) + 2)
    s.dimv(Y(hb[5]), Y(0), X(hb[3]) + 134, so(hb[5], 0), X(hb[3]), X(hb[3]), side=1)
    # góc ngửa của mặt đỉnh và vòng
    t, n = mh.t_mat, mh.n_mat
    p0 = mh.P_DINH
    s.line(X(p0[0] - 30 * t[0]), Y(p0[2] - 30 * t[2]), X(p0[0] + 34 * t[0]), Y(p0[2] + 34 * t[2]), "ctr")
    s.line(X(p0[0]), Y(p0[2]), X(p0[0] + 34), Y(p0[2]), "dim")
    cung_goc(s, X(p0[0]), Y(p0[2]), 112, 0, mh.NGHIENG, f"{so(mh.NGHIENG, 0)}°")
    s.leader(X(22), Y(77), X(22) + 44, Y(77) - 22, "vòng sáng, dày " + so(mh.CAO_VONG, 0))
    s.leader(X(22), Y(58), X(22) + 44, Y(58) + 6, "cò Hall", sub=f"rộng {so(2 * mh.Y_CO)}")
    s.leader(X(-1), Y(50), X(-1) - 118, Y(50) - 30, "chữ khắc sâu " + so(mh.SAU_CHU), anchor="end", sub=f"chữ hoa cao ≈ 3,2")
    s.leader(X(mh.X_THANH), Y(mh.Z_THANH), X(mh.X_THANH) - 40, Y(mh.Z_THANH) + 34, "thanh móc dây Ø4", anchor="end")

    # hình chiếu cạnh: nhìn từ trước
    ox2 = 165
    U = lambda u: ox2 + u * k
    s.text(40, 46, "HÌNH CHIẾU CẠNH", "h")
    s.text(40, 62, "nhìn từ phía trước", "ts")
    ve_vo(s, mo, R_TRUOC, U, Y, nua_truoc=True)
    s.line(U(0), Y(-3), U(0), Y(hb[5] + 3), "ctr")
    s.dimh(U(-b_dau), U(b_dau), Y(hb[5]) - 16, so(2 * b_dau, 0), Y(82), Y(82))
    s.dimh(U(-b_eo), U(b_eo), Y(0) + 34, f"{so(2 * b_eo)} (eo)", Y(38), Y(38))
    s.leader(U(0), Y(30), U(0) - 84, Y(30) + 60, "mặt ghép", anchor="end", sub="2 nửa thân")

    # hình chiếu bằng: nhìn từ trên, đặt dưới hình chiếu đứng
    oy3 = 652
    Yt = lambda v: oy3 - v * k
    s.text(370, 548, "HÌNH CHIẾU BẰNG", "h")
    s.text(370, 564, "nhìn từ trên", "ts")
    to_mau(s, chieu(mo.than, R_TREN), X, Yt, "trang")
    to_mau(s, chieu(mo.ct["thanh"], R_TREN), X, Yt, "toi")
    to_mau(s, chieu(mo.ct["vong"], R_TREN), X, Yt, "hl")
    to_mau(s, chieu(mo.nap_ngoai, R_TREN), X, Yt, "toi")
    to_mau(s, chieu(mo.ct["chen"], R_TREN), X, Yt, "nhat")
    tc = mh.TAM_CAN
    s.circle(X(tc[0]), Yt(tc[1]), 11.8 * k, "toi")
    to_mau(s, chieu(mo.nam, R_TREN), X, Yt, "toi")
    s.line(X(tc[0]) - 66, Yt(tc[1]), X(tc[0]) + 66, Yt(tc[1]), "ctr")
    s.leader(X(tc[0] + 11), Yt(10), X(tc[0] + 11) + 74, Yt(10) - 24, f"chén lõm Ø{so(2 * mh.R_CHEN, 0)}")
    s.dimv(Yt(mh.Y_TAI[1]), Yt(-mh.Y_TAI[1]), X(hb[0]) - 24, so(2 * mh.Y_TAI[1]), X(mh.X_THANH), X(mh.X_THANH))

    # nhìn từ đáy
    Xd = lambda u: ox2 + u * k
    Yd = lambda v: oy3 - v * k
    s.text(40, 548, "NHÌN TỪ ĐÁY", "h")
    s.text(40, 564, "mép trên hình là sườn phải", "ts")
    to_mau(s, chieu(mo.than, R_DAY), Xd, Yd, "trang")
    to_mau(s, chieu(mo.ct["thanh"], R_DAY), Xd, Yd, "toi")
    to_mau(s, chieu(mo.co_ngoai, R_DAY), Xd, Yd, "toi")
    cua = [(-4.0, mh.SAC["y"] - 1.6, 9.6, 3.8, "USB-C sạc"), (-4.0, mh.ESP["y"] + 3.1, 8.6, 3.8, "micro-USB nạp code"), (10.2, -3.0, 2.8, 2.8, "công tắc")]
    for x, y, w, h, _ in cua:
        s.rect(Xd(x - w / 2), Yd(-y + h / 2), w * k, h * k, "toi")
    for px, py, tx_, ten in ((Xd(-4), Yd(5.1), 62, "USB-C sạc"), (Xd(-4), Yd(-6.6), 170, "micro-USB nạp"), (Xd(10.2), Yd(3), 288, "công tắc")):
        s.circle(px, py, 1.8, "dot")
        s.line(px, py, tx_, 736, "thin")
        s.text(tx_, 750, ten, "t", "middle")

    # mặt cắt eo
    cx, cy, kk = 870, 200, 3.6
    s.text(760, 46, "MẶT CẮT TẠI EO", "h")
    s.text(760, 62, "z = 38, nơi 3 ngón ôm", "ts")
    Xe = lambda u: cx + u * kk
    Ye = lambda v: cy - v * kk
    to_mau(s, cat_z(mo.than, 38), Xe, Ye, "cutv")
    cxe = mh.cx(38)
    s.dimh(Xe(cxe - a_eo), Xe(cxe + a_eo), cy + 84, so(2 * a_eo), cy, cy)
    s.dimv(Ye(b_eo), Ye(-b_eo), cx - 104, so(2 * b_eo), cx, cx)
    chu_vi = math.pi * (3 * (a_eo + b_eo) - math.sqrt((3 * a_eo + b_eo) * (a_eo + 3 * b_eo)))
    s.text(760, 325, f"Chu vi eo ≈ {so(chu_vi, 0)} mm, tương đương trụ Ø{so(chu_vi / math.pi)}.", "ts")
    s.text(760, 341, "Với bàn tay dài 177 mm, trụ tối ưu ≈ Ø35 (docs/13). Eo lớn hơn", "ts")
    s.text(760, 357, "2,5 mm vì phải chứa ESP32 rộng 28 mm. Cần in thử để cầm.", "ts")

    gx, gy = 760, 400
    s.text(gx, gy, "THÔNG SỐ CHÍNH", "hs")
    nhua = sum(t_["gam_pla"] for t_ in mo.thong_so)
    rows = [("Dài × rộng × cao", f"{so(hb[3] - hb[0], 0)} × {so(hb[4] - hb[1], 0)} × {so(hb[5], 0)} mm, kể cả cần"),
            ("Đầu (lớn nhất)", f"{so(2 * a_dau)} × {so(2 * b_dau)} mm"),
            ("Eo", f"{so(2 * a_eo)} × {so(2 * b_eo)} mm"),
            ("Đáy phẳng", f"{so(2 * a_day)} × {so(2 * b_day)} mm, đứng được"),
            ("Mặt đỉnh và vòng sáng", f"ngửa {so(mh.NGHIENG, 0)}° về phía ngón cái"),
            ("Vách vỏ", f"{so(mh.VACH)} mm"),
            ("Số chi tiết in", f"{len(mo.thong_so)}, tổng {so(nhua, 0)} g nhựa PLA"),
            ("Khối lượng cả bộ", "≈ 85 g có pin, ước tính, chưa cân")]
    for i, (a_, b_) in enumerate(rows):
        s.text(gx, gy + 24 + i * 22, a_, "ts")
        s.text(gx + 150, gy + 24 + i * 22, b_, "t")
    return s


# ---------------- TCD-02 mặt cắt và bố trí ----------------

def _tam(polys):
    x0, y0, x1, y1 = bb(polys)
    return (x0 + x1) / 2, (y0 + y1) / 2


def tcd02(mo):
    s = Svg(1180, 820, 32)
    khung(s, "TCD-02", "TAY CẦM VERA DROP · BỐ TRÍ LINH KIỆN", "Cắt dọc từng nửa thân và cắt ngang tại z = 40")
    k = 4.0
    oy = 500
    Y = lambda v: oy - v * k

    def nua(ox, tieu_de, phu, y_cat, than, cac_lk):
        X = lambda u: ox + u * k
        s.text(ox - 100, 46, tieu_de, "h")
        s.text(ox - 100, 62, phu, "ts")
        for ma in cac_lk:
            to_mau(s, chieu(mo.lk[ma], R_CANH), X, Y, "lkf", f'style="fill:{MAU_LK[ma]};fill-opacity:.92"')
        to_mau(s, cat_y(than, y_cat), X, Y, "cutv")
        to_mau(s, cat_y(mo.ct["nap"], y_cat), X, Y, "toi")
        to_mau(s, cat_y(mo.ct["vong"], y_cat), X, Y, "hl")
        to_mau(s, cat_y(mo.ct["co"], y_cat), X, Y, "toi")
        to_mau(s, cat_y(mo.ct["chen"], y_cat), X, Y, "nhat")
        return X

    Xt = nua(150, "A–A · NỬA TRÁI", "cắt tại y = +1, nhìn từ sườn phải", 1.0, mo.ct["than_trai"], ["ky023", "esp32", "bmi160", "hall"])
    Xp = nua(430, "B–B · NỬA PHẢI", "cắt tại y = −1, nhìn từ sườn phải", -1.0, mo.ct["than_phai"], ["ky023", "pin", "tp4056", "cong_tac"])

    tam_lk = {m: _tam(chieu(mo.lk[m], R_CANH)) for m in mo.lk if m != "vit"}
    can = lambda m: (tam_lk[m][0], tam_lk[m][1])
    muc = [(1, "ESP32 DevKitC 38 chân, cổng micro-USB xuống đáy", Xt, can("esp32")[0], 15),
           (2, "BMI160, dán trên vỏ module ESP32", Xt, *can("bmi160")),
           (3, "Hall SS49E, trong hốc vách đế", Xt, *can("hall")),
           (4, "Joystick KY-023, bắt vào 4 trụ của nắp", Xt, *can("ky023")),
           (5, "Pin LiPo 1000 mAh 40 × 25 × 9", Xp, *can("pin")),
           (6, "Mạch sạc TP4056 USB-C, cổng xuống đáy", Xp, *can("tp4056")),
           (7, "Công tắc trượt, cần ló qua lỗ đáy", Xp, *can("cong_tac")),
           (8, "Cò Hall, trục Ø2 qua 2 tai", Xp, 22.0, 58.0)]
    for n, ten, X_, px, py in muc:
        x, y = X_(px), Y(py)
        s.circle(x, y, 8.5, "ball")
        s.text(x, y + 4, str(n), "ballt", "middle")
        cot = 0 if n <= 4 else 1
        hang = n - 1 if n <= 4 else n - 5
        lx = 44 + cot * 330
        ly = 556 + hang * 24
        s.circle(lx, ly - 4, 8.5, "ball")
        s.text(lx, ly, str(n), "ballt", "middle")
        s.text(lx + 14, ly, ten, "ts")
    s.text(40, 672, "Phần gạch chéo là vỏ thân tại mặt cắt, chỗ nhô vào trong là trụ vít. Màu đậm là linh kiện chiếu từ phía sau mặt cắt.", "ts")
    s.text(40, 690, "Dây từ joystick và Hall đi dọc lưng ESP32 xuống. Gờ nắp lồng xuống thân để khóa miệng 2 nửa.", "ts")

    # cắt ngang
    cx, cy, kk, z = 880, 200, 6.0, 40.0
    s.text(700, 46, "C–C · CẮT NGANG TẠI z = 40", "h")
    s.text(700, 62, "nhìn từ trên; thứ tự xếp chồng theo chiều y", "ts")
    Xc = lambda u: cx + u * kk
    Yc = lambda v: cy - v * kk
    to_mau(s, cat_z(mo.than, z), Xc, Yc, "cutv")
    for ma in ("esp32", "bmi160", "pin"):
        to_mau(s, cat_z(mo.lk[ma], z), Xc, Yc, "lkf", f'style="fill:{MAU_LK[ma]}"')
    to_mau(s, cat_z(mo.lk["vit"], z), Xc, Yc, "lkf", 'style="fill:#aab4ba"')
    s.leader(Xc(-4), Yc(9.1), Xc(-4) - 56, Yc(9.1) - 48, "BMI160", anchor="end")
    s.leader(Xc(-4), Yc(4.3), Xc(-4) + 130, Yc(4.3) - 96, "ESP32", anchor="start")
    s.leader(Xc(-2), Yc(-5), Xc(-2) - 40, Yc(-5) + 74, "pin", anchor="end")
    s.leader(Xc(13), Yc(6.1), Xc(13) + 56, Yc(6.1) - 50, "gờ đỡ", anchor="start")
    s.leader(Xc(14.5), Yc(-2), Xc(14.5) + 58, Yc(-2) + 40, "trụ vít V2", anchor="start")

    # bảng xếp chồng tính từ mô hình
    tx, ty = 700, 350
    e, p = mh.ESP, mh.PIN
    yb = e["y"] + 1.6
    ychong = [("Vách nửa phải, mặt trong", -mh.ab_tai(z)[1] + mh.VACH), ("Pin, mặt ngoài", p["y"][0]), ("Pin, mặt trong", p["y"][1]),
              ("Chân header cắt ngắn, đầu chân", e["y"] - 2.5), ("Mạch ESP32, mặt dưới", e["y"]), ("Mạch ESP32, mặt trên", yb),
              ("Module WROOM, mặt trên", e["y"] + 4.8), ("BMI160, mặt trên", e["y"] + 6.4), ("Vách nửa trái, mặt trong", mh.ab_tai(z)[1] - mh.VACH)]
    s.text(tx, ty, "XẾP CHỒNG THEO CHIỀU Y TẠI z = 40", "hs")
    s.text(tx + 290, ty, "y (mm)", "hs", "end")
    s.text(tx + 410, ty, "khe tới lớp sau", "hs", "end")
    for i, (ten, y) in enumerate(ychong):
        yy = ty + 22 + i * 20
        s.text(tx, yy, ten, "ts")
        s.text(tx + 290, yy, f"{y:+.1f}".replace(".", ",").replace("-", "−"), "dimt", "end")
        if i + 1 < len(ychong):
            khe = ychong[i + 1][1] - y
            lop_khoi = i in (1, 3, 4, 5, 6)
            s.text(tx + 410, yy, "liền khối" if lop_khoi else so(khe), "dimt", "end")
    s.text(700, ty + 22 + 9 * 20 + 2, "Khe pin ↔ chân header dùng để lót xốp 1 mm; xốp này ép ESP32 vào gờ.", "ts")
    return s


# ---------------- TCD-03 hai nửa thân, vít, thanh móc ----------------

def tcd03(mo):
    s = Svg(1180, 820, 33)
    khung(s, "TCD-03", "HAI NỬA THÂN · VÍT GHÉP · THANH MÓC DÂY", "Mở thân ra lắp mạch, khép lại bằng 5 vít từ nửa trái")
    k = 4.2
    oy = 440
    Y = lambda v: oy - v * k
    vit = [(f"V{i + 1}", x, z) for i, (x, z, _) in enumerate(mh.VIT)] + [("V5", mh.X_THANH, mh.Z_THANH)]

    def mat(ox, tieu_de, phu, ma, dau, y_gan, y_sau, nhan):
        U = lambda u: ox + u * k * dau
        s.text(ox - 104, 46, tieu_de, "h")
        s.text(ox - 104, 62, phu, "ts")
        to_mau(s, chieu(mo.ct[ma], R_CANH), U, Y, "nhat")
        to_mau(s, cat_y(mo.ct[ma], y_sau), U, Y, "trang")
        to_mau(s, cat_y(mo.ct[ma], y_gan), U, Y, "cutv")
        for ten, x, z in vit:
            s.circle(U(x), Y(z), 8, "ball")
            s.text(U(x), Y(z) + 3.5, ten, "ballt", "middle", extra='style="font-size:8.5px"')
        s.text(ox, oy + 22, nhan, "ts", "middle")

    mat(150, "NỬA PHẢI, MẶT GHÉP", "nhìn từ giữa ra; trụ có lỗ mồi Ø1,7", "than_phai", -1, -0.05, -4.0, "chữ khắc ở mặt ngoài nửa này")
    mat(430, "NỬA TRÁI, MẶT GHÉP", "nhìn từ giữa ra; hốc đầu vít Ø4,2", "than_trai", 1, 0.05, 6.0, "gờ đỡ ESP32 và vách Hall ở nửa này")

    # chi tiết thanh móc
    cx, cy, kk = 800, 190, 6.5
    s.text(650, 46, "THANH MÓC DÂY, CẮT TẠI z = 4", "h")
    s.text(650, 62, "nhìn từ trên; ống Ø4 lồng vào 2 tai, lõi là vít M2 × 16", "ts")
    z = mh.Z_THANH
    Xc = lambda u: cx + (u - mh.X_THANH) * kk
    Yc = lambda v: cy - v * kk
    s.add('<clipPath id="kc33"><rect x="650" y="76" width="300" height="230"/></clipPath><g clip-path="url(#kc33)">')
    to_mau(s, cat_z(mo.than, z), Xc, Yc, "cutv")
    to_mau(s, cat_z(mo.ct["thanh"], z), Xc, Yc, "toi")
    to_mau(s, cat_z(mo.lk["vit"], z), Xc, Yc, "lkf", 'style="fill:#aab4ba"')
    s.add("</g>")
    s.rect(650, 76, 300, 230, "thin")
    s.dimv(Yc(mh.Y_TAI[0]), Yc(-mh.Y_TAI[0]), Xc(mh.X_THANH) + 54, f"{so(2 * mh.Y_TAI[0], 0)}", Xc(mh.X_THANH) + 16, Xc(mh.X_THANH) + 16, side=1)
    s.leader(Xc(mh.X_THANH), Yc(0), 980, 120, "ống Ø4 in rời", anchor="start")
    s.leader(Xc(mh.X_THANH), Yc(7.2), 980, 150, "tai nửa trái, dày 3,7", anchor="start")
    s.leader(Xc(mh.X_THANH), Yc(-7.5), 980, 250, "tai nửa phải, lỗ mồi Ø1,7", anchor="start")
    s.leader(Xc(mh.X_THANH + 3.3), Yc(2.6), 980, 190, "khe luồn dây ≈ 2,8", anchor="start")
    s.text(650, 322, "Lõi vít thép chịu lực nên ống không phải đỡ tải một mình. Tai hở ở phía đáy và sau để luồn dây.", "ts")

    # bảng vít
    tx, ty = 650, 352
    s.text(tx, ty, "VÍT", "hs")
    hang = [("V1, V2", "2 cái M2 × 10 tự ren", "trước thân, z = 11 và 40"), ("V3, V4", "2 cái M2 × 10 tự ren", "lưng thân, z = 30 và 59"),
            ("V5", "1 cái M2 × 16 tự ren", "xuyên thanh móc, z = 4"), ("Nắp", "2 cái M2 × 6 tự ren", "lưng thân, z = 65, y = ±8"),
            ("Trục cò", "1 đoạn thép Ø2 × 20", "giữa 2 tai, kẹp khi khép thân")]
    for i, (a, b, c) in enumerate(hang):
        yy = ty + 22 + i * 20
        s.text(tx, yy, a, "tb")
        s.text(tx + 70, yy, b, "t")
        s.text(tx + 240, yy, c, "ts")

    # bảng in
    ty2 = 490
    s.text(tx, ty2, "FILE IN 3D", "hs")
    s.text(tx + 190, ty2, "KÍCH THƯỚC", "hs")
    s.text(tx + 300, ty2, "NHỰA", "hs")
    s.text(tx + 345, ty2, "HƯỚNG IN", "hs")
    for i, t_ in enumerate(mo.thong_so):
        yy = ty2 + 20 + i * 20
        s.text(tx, yy, t_["ma"] + ".stl", "code")
        s.text(tx + 190, yy, " × ".join(so(v) for v in t_["kich_thuoc"]), "ts")
        s.text(tx + 300, yy, so(t_["gam_pla"]) + " g", "ts")
        s.text(tx + 345, yy, t_["cach_in"].split(",")[0], "ts")
    s.text(tx, ty2 + 20 + 7 * 20 + 2, "2 nửa thân và 2 tai móc cần vật đỡ khi in. Mặt ngoài nằm phía trên nên giữ được sạch.", "ts")

    # thứ tự lắp
    bx, by = 40, 498
    s.text(bx, by, "THỨ TỰ LẮP", "hs")
    buoc = ["Đo mạch thật, cắt chân header ESP32 còn ≈ 2,5 mm, hàn dây theo TCD-05.",
            "Nửa trái: đặt ESP32 lên 2 gờ, dán BMI160 lên module, đặt SS49E vào hốc vách.",
            "Nửa phải: dán pin bằng băng xốp, đặt TP4056 và công tắc vào cửa đáy.",
            "Đặt cò, trục Ø2, lò xo và ống thanh móc vào nửa trái. Luồn dây gọn.",
            "Khép nửa phải, lật lại, siết V1 đến V5 từ nửa trái. Thử bóp cò trước khi siết hẳn.",
            "Đặt vòng sáng. Bắt KY-023 vào 4 trụ nắp, đặt chén lõm, cắm dây.",
            "Úp nắp, gờ nắp lồng vào miệng thân, siết 2 vít M2 × 6 ở lưng."]
    for i, b in enumerate(buoc):
        yy = by + 24 + i * 22
        s.circle(bx + 8, yy - 4, 8.5, "ball")
        s.text(bx + 8, yy, str(i + 1), "ballt", "middle")
        s.text(bx + 24, yy, b, "ts")
    return s


# ---------------- TCD-04 cò Hall ----------------

def quay_cw(p, tam, goc):
    t = math.radians(goc)
    x, z = p[0] - tam[0], p[1] - tam[1]
    return (tam[0] + x * math.cos(t) + z * math.sin(t), tam[1] - x * math.sin(t) + z * math.cos(t))


def tcd04(mo):
    s = Svg(1180, 820, 34)
    khung(s, "TCD-04", "CÒ ANALOG BẰNG CẢM BIẾN HALL", "Bóp cò → nam châm lại gần SS49E → điện áp ra tăng")
    k = 10.0
    x0, z1 = 4.0, 80.0
    X = lambda u: 40 + (u - x0) * k
    Y = lambda v: 84 + (z1 - v) * k
    y = mh.Y_NC
    piv = (float(mh.CHOT[0]), float(mh.CHOT[2]))
    s.text(40, 46, "CHI TIẾT CÒ, CẮT TẠI y = 3 (QUA TÂM NAM CHÂM)", "h")
    s.text(40, 62, f"nét liền: nhả cò · nét đứt: bóp hết, cò quay {so(mh.GOC_BOP)}° tới khi vấu chạm vách", "ts")
    s.add('<clipPath id="kc34"><rect x="40" y="80" width="440" height="440"/></clipPath><g clip-path="url(#kc34)">')
    to_mau(s, cat_y(mo.ct["than_trai"], y), X, Y, "cutv")
    to_mau(s, cat_y(mo.ct["co"], y), X, Y, "toi")
    for poly in cat_y(mo.ct["co"], y):
        q = [quay_cw(p, piv, mh.GOC_BOP) for p in poly]
        s.path("M" + " L".join(f"{fmt(X(a))},{fmt(Y(b))}" for a, b in q) + " Z", "hid")
    to_mau(s, cat_y(mo.lk["nam_cham"], y), X, Y, "lkf", 'style="fill:#9aa3a8"')
    to_mau(s, cat_y(mo.lk["hall"], y), X, Y, "lkf", 'style="fill:#c0392b"')
    # lò xo giữa vách và cò
    zl = 68.3
    n = 13
    xa, xb = mh.X_HALL - 1.5, 21.5
    zz = "M" + " L".join(f"{fmt(X(xa + (xb - xa) * i / n))},{fmt(Y(zl + (0 if i in (0, n) else (1.7 if i % 2 else -1.7))))}" for i in range(n + 1))
    s.path(zz, "ln", 'style="stroke-width:1.2"')
    s.circle(X(piv[0]), Y(piv[1]), 1.0 * k, "ball")
    s.add("</g>")
    s.rect(40, 80, 440, 440, "thin")
    xh, xn = mh.X_HALL, mh.X_NC
    s.dimh(X(xh), X(xn), Y(mh.Z_NC) + 4, "6,5")
    s.leader(X(xn + 1.5), Y(mh.Z_NC + 1), 520, 120, "nam châm N35 Ø6 × 3", anchor="start")
    s.leader(X(xh - 0.7), Y(mh.Z_NC - 1), 200, 534, "SS49E, mặt có chữ hướng về nam châm", anchor="middle")
    s.leader(X(15), Y(zl), 520, 150, "lò xo nén Ø4, dài ≈ 12", anchor="start")
    s.leader(X(piv[0]), Y(piv[1]), 520, 180, "trục thép Ø2", anchor="start")
    s.leader(X(20.8), Y(54), 520, 210, "chỗ đặt ngón trỏ", anchor="start")

    # đồ thị tính từ hình học thật
    gx, gy, gw, gh = 580, 250, 500, 240
    s.text(gx, gy - 14, "ĐIỆN ÁP RA SS49E THEO ĐỘ BÓP CÒ (ƯỚC TÍNH)", "hs")
    pts = []
    for i in range(0, 101):
        g = mh.GOC_BOP * i / 100
        mx = quay_cw((xn, mh.Z_NC), piv, g)[0]
        khe = mx - xh + 0.5                 # phần tử cảm biến nằm sâu ≈ 0,5 mm trong vỏ TO-92
        B = tu_truong(khe)
        pts.append((i, 3.3 / 2 + 0.00092 * B, khe, B))
    vmin, vmax = 1.6, 2.0
    PX = lambda p: gx + p / 100 * gw
    PY = lambda v: gy + gh - (v - vmin) / (vmax - vmin) * gh
    for v in (1.6, 1.7, 1.8, 1.9, 2.0):
        s.line(gx, PY(v), gx + gw, PY(v), "grid")
        s.text(gx - 8, PY(v) + 4, f"{v:.1f}".replace(".", ","), "dimt", "end")
    for p in range(0, 101, 20):
        s.line(PX(p), gy, PX(p), gy + gh, "grid")
        s.text(PX(p), gy + gh + 18, f"{p}%", "dimt", "middle")
    s.text(gx + gw / 2, gy + gh + 38, "độ bóp cò (0% = nhả, 100% = vấu chạm vách)", "ts", "middle")
    s.text(gx - 40, gy + gh / 2, "điện áp ra (V)", "ts", "middle", rot=-90)
    s.path("M" + " L".join(f"{fmt(PX(p))},{fmt(PY(v))}" for p, v, _, _ in pts), "chart")
    for p in (0, 100):
        _, v, khe, B = pts[p]
        s.circle(PX(p), PY(v), 4, "dot")
        s.text(PX(p) + (8 if p == 0 else -8), PY(v) + (20 if p == 0 else -12), f"{so(v, 2)} V · {so(B, 0)} G · khe {so(khe)} mm", "ts", "start" if p == 0 else "end")
    adc = lambda v: v / 3.1 * 4095
    d0, d1 = adc(pts[0][1]), adc(pts[-1][1])
    ghi = [f"ESP32 (ADC 12 bit, 11 dB) đọc ≈ {d0:.0f} → {d1:.0f}, tức khoảng {d1 - d0:.0f} mức.",
           "Lấy trung bình 8 mẫu để giảm nhiễu ADC, rồi đổi ra 0–100%.",
           "Mô hình: nam châm trụ N35 (Br ≈ 1,2 T); SS49E ≈ 0,92 mV/G ở 3,3 V. Đây là ước tính, chưa đo."]
    for i, g in enumerate(ghi):
        s.text(gx, gy + gh + 62 + i * 18, g, "ts" if i else "t")
    s.text(40, 640, "KHI LẮP VÀ LẬP TRÌNH", "hs")
    s.text(40, 662, "Lúc khởi động đọc mức nhả làm min, bóp hết một lần lấy max: % = (đọc − min) / (max − min). Lật nam châm thì điện áp giảm.", "ts")
    s.text(40, 680, "Khe hẹp nhất còn 3,58 mm nên SS49E chưa bão hòa. Nếu tầm đọc quá hẹp khi đo thật, có thể dùng nam châm dày hơn.", "ts")
    return s


# ---------------- TCD-05 đấu nối ----------------

def tcd05(mo):
    s = Svg(1180, 820, 35)
    khung(s, "TCD-05", "SƠ ĐỒ ĐẤU NỐI", "ESP32 DevKitC 38 chân · mọi cảm biến chạy 3,3 V")
    s.text(40, 46, "SƠ ĐỒ ĐẤU NỐI", "h")
    s.text(40, 62, "chỉ dùng chân ADC1 cho tín hiệu analog (ADC2 không đọc được khi bật Wi-Fi)", "ts")
    bx, by, bw = 470, 100, 230
    trai = ["3V3", "EN", "VP 36", "VN 39", "34", "35", "32", "33", "25", "26", "27", "14", "12", "GND", "13", "SD2", "SD3", "CMD", "5V"]
    phai = ["GND", "23", "22", "TX0", "RX0", "21", "GND", "19", "18", "5", "17", "16", "4", "0", "2", "15", "SD1", "SD0", "CLK"]
    step = 26
    bh = 66 + step * 18 + 30
    s.rect(bx, by, bw, bh, "mod", rx=6)
    s.rect(bx + 50, by + 6, bw - 100, 46, "lkf", extra='style="fill:#55626a"')
    s.text(bx + bw / 2, by + 34, "ESP32-WROOM-32", "ts", "middle", extra='style="fill:#e6eaec"')
    s.rect(bx + bw / 2 - 16, by + bh - 14, 32, 18, "kim")
    s.text(bx + bw / 2, by + bh - 24, "USB", "ts", "middle")
    pin = {}
    for i, p in enumerate(trai):
        y = by + 66 + i * step
        s.circle(bx + 12, y, 4, "ln")
        s.text(bx + 22, y + 4, p, "code")
        pin["L" + p] = (bx, y)
    for i, p in enumerate(phai):
        y = by + 66 + i * step
        s.circle(bx + bw - 12, y, 4, "ln")
        s.text(bx + bw - 22, y + 4, p, "code", "end")
        pin["R" + p] = (bx + bw, y)
    dung = {"L3V3": "#e0362c", "L34": "#f08c00", "L35": "#8d5a2b", "L32": "#c0392b", "L33": "#7f8c8d", "L27": "#8e44ad",
            "LGND": None, "R22": "#2e9e44", "R21": "#d9a400", "RGND": None}
    for kk, mau in dung.items():
        x, y = pin[kk]
        s.circle(x + (12 if kk[0] == "L" else -12), y, 4.5, "dot" if mau is None else "ln", "" if mau is None else f'style="fill:{mau};stroke:{mau}"')

    def day(pts, mau, w=2.2, net=False):
        d = "M" + " L".join(f"{fmt(x)},{fmt(y)}" for x, y in pts)
        if mau:
            s.path(d, "wire", f'stroke="{mau}" stroke-width="{w}"' + (' stroke-dasharray="6 4"' if net else ""))
        else:
            s.path(d, "wire gnd", f'stroke-width="{w}"' + (' stroke-dasharray="6 4"' if net else ""))

    def mod(x, y, w, h_, ten, chan):
        s.rect(x, y, w, h_, "mod", rx=5)
        s.rect(x, y, w, 22, "modh", rx=5)
        s.text(x + w / 2, y + 16, ten, "tb", "middle")
        out = {}
        for i, c_ in enumerate(chan):
            yy = y + 42 + i * 22
            s.text(x + w - 8, yy + 4, c_, "ts", "end")
            s.circle(x + w, yy, 3, "dot")
            out[c_] = (x + w, yy)
        return out

    ky = mod(60, 110, 180, 140, "Joystick KY-023", ["+5V (nối 3V3)", "GND", "VRx", "VRy", "SW"])
    hl = mod(60, 290, 180, 110, "Hall SS49E (cò)", ["1 VCC", "2 GND", "3 OUT"])
    s.rect(250, 357, 12, 16, "mod", rx=2)
    s.text(270, 369, "100 nF", "ts")
    bm = mod(60, 440, 180, 132, "BMI160", ["VIN", "GND", "SDA", "SCL"])
    x3, y3 = pin["L3V3"]
    rail3 = 330
    day([(x3, y3), (rail3, y3)], "#e0362c", 2.6)
    day([(rail3, ky["+5V (nối 3V3)"][1]), (rail3, bm["VIN"][1])], "#e0362c", 2.6)
    for p in (ky["+5V (nối 3V3)"], hl["1 VCC"], bm["VIN"]):
        day([p, (rail3, p[1])], "#e0362c")
        s.circle(rail3, p[1], 3, "dot")
    xg, yg = pin["LGND"]
    railg = 350
    day([(xg, yg), (railg, yg), (railg, ky["GND"][1])], None, 2.6)
    for p in (ky["GND"], hl["2 GND"], bm["GND"]):
        day([p, (railg, p[1])], None)
        s.circle(railg, p[1], 3, "dot")
    day([(256, hl["2 GND"][1]), (256, 357)], None, 1.4)
    day([(256, 373), (256, hl["3 OUT"][1])], "#c0392b", 1.4)
    for src, dst, mau, xo in ((ky["VRx"], "L34", "#f08c00", 380), (ky["VRy"], "L35", "#8d5a2b", 392), (ky["SW"], "L27", "#8e44ad", 404),
                              (hl["3 OUT"], "L32", "#c0392b", 416)):
        day([src, (xo, src[1]), (xo, pin[dst][1]), pin[dst]], mau)
    for src, dst, mau, xo in ((bm["SDA"], "R21", "#d9a400", 760), (bm["SCL"], "R22", "#2e9e44", 772)):
        day([src, (300, src[1]), (300, 696 + (xo - 760)), (xo, 696 + (xo - 760)), (xo, pin[dst][1]), pin[dst]], mau)
    s.text(530, 724, "I2C: SDA → GPIO21, SCL → GPIO22 (địa chỉ 0x68)", "ts", "middle")

    # nguồn
    px_, py_ = 820, 100
    s.text(px_, py_, "NGUỒN", "hs")
    y0 = py_ + 14

    def hop_(x, y, w, t1, t2=""):
        s.rect(x, y, w, 36, "mod", rx=4)
        s.text(x + 10, y + 16, t1, "tb")
        s.text(x + 10, y + 30, t2, "ts")

    def mui(x, y1, y2):
        s.line(x, y1, x, y2, "dim", f'marker-end="url(#m{s.ma})"')

    hop_(px_, y0, 150, "Cổng USB-C ở đáy", "sạc")
    hop_(px_ + 170, y0, 150, "Pin LiPo 1000 mAh", "40 × 25 × 9")
    hop_(px_, y0 + 56, 320, "TP4056 có bảo vệ", "USB-C vào; B+, B− nối pin; OUT+, OUT− ra tải")
    hop_(px_, y0 + 112, 320, "Công tắc trượt ở đáy", "cắt dây OUT+")
    hop_(px_, y0 + 168, 320, "Hạ áp 3,3 V ≥ 500 mA (AP2112K), 2 tụ 10 µF", "OUT → chân 3V3 của ESP32")
    mui(px_ + 75, y0 + 36, y0 + 56)
    mui(px_ + 245, y0 + 36, y0 + 56)
    mui(px_ + 160, y0 + 92, y0 + 112)
    mui(px_ + 160, y0 + 148, y0 + 168)
    ty0 = y0 + 216
    s.text(px_, ty0, "Không cắm pin vào chân 5V: mạch hạ áp trên DevKit", "tw")
    s.text(px_, ty0 + 16, "cần điện áp cao hơn pin.", "tw")
    s.text(px_, ty0 + 32, "HT7333 chỉ cho 250 mA, không đủ khi bật Wi-Fi.", "tw")
    s.text(px_, ty0 + 50, "Mạch hạ áp chưa có hốc riêng trong mô hình,", "ts")
    s.text(px_, ty0 + 66, "gói co nhiệt đi cùng dây.", "ts")
    s.text(px_, ty0 + 82, "Đo pin: chia áp 100k/100k từ sau công tắc về GPIO33.", "ts")
    day([pin["L33"], (430, pin["L33"][1]), (430, 86), (px_ - 20, 86), (px_ - 20, y0 + 130), (px_, y0 + 130)], "#7f8c8d", 1.6, net=True)
    day([pin["L3V3"], (446, pin["L3V3"][1]), (446, 78), (px_ - 8, 78), (px_ - 8, y0 + 186), (px_, y0 + 186)], "#e0362c", 1.6)

    # bảng chân
    tx, ty = 820, 450
    s.text(tx, ty, "BẢNG CHÂN", "hs")
    rows = [("GPIO34", "VRx joystick", "ADC1"), ("GPIO35", "VRy joystick", "ADC1"), ("GPIO27", "SW joystick", "INPUT_PULLUP"),
            ("GPIO32", "OUT Hall SS49E", "ADC1"), ("GPIO21", "SDA BMI160", "I2C"), ("GPIO22", "SCL BMI160", "I2C"),
            ("GPIO33", "điện áp pin", "ADC1"), ("3V3, GND", "nguồn cảm biến", "")]
    for i, (a, b, c_) in enumerate(rows):
        y = ty + 24 + i * 22
        s.text(tx, y, a, "code")
        s.text(tx + 90, y, b, "t")
        s.text(tx + 230, y, c_, "ts")
    return s


# ---------------- TCD-06 cầm tay, dây đeo, OpenCV ----------------

def tcd06(mo):
    s = Svg(1180, 820, 36)
    khung(s, "TCD-06", "CÁCH CẦM · DÂY ĐEO · MỐC CHO OPENCV", "Bàn tay chỉ là hình mô phỏng để so cỡ, độ thoải mái chưa thử")
    k = 2.6
    ox, oy = 320, 420
    X = lambda u: ox + u * k
    Y = lambda v: oy - v * k
    s.text(40, 46, "A · CẦM TRÊN TAY PHẢI", "h")
    s.text(40, 62, "nhìn từ sườn phải, bàn tay vẽ trong suốt", "ts")
    ve_vo(s, mo, R_CANH, X, Y)
    s.add('<clipPath id="kc36"><rect x="30" y="76" width="540" height="480"/></clipPath><g clip-path="url(#kc36)">')
    to_mau(s, chieu(mo.tay, R_CANH), X, Y, "tay")
    # dây đeo từ thanh móc vòng qua cổ tay
    bx_, bz = X(mh.X_THANH), Y(mh.Z_THANH)
    s.path(f"M{fmt(bx_)},{fmt(bz)} C{fmt(bx_ - 20)},{fmt(bz + 70)} {fmt(bx_ - 90)},{fmt(bz + 110)} {fmt(bx_ - 130)},{fmt(bz + 90)}", "day")
    s.add("</g>")
    tc = mh.TAM_CAN
    s.leader(X(tc[0]), Y(tc[2] + 6), X(tc[0]) + 96, Y(tc[2] + 6) - 12, "Ngón cái: joystick (đi, quay)")
    s.leader(X(26), Y(58), X(26) + 60, Y(58) - 6, "Ngón trỏ: cò 0–100%")
    s.leader(X(22), Y(25), X(22) + 80, Y(25) + 8, "3 ngón ôm eo thân")
    s.leader(bx_ - 90, bz + 100, 230, 545, "dây đeo quàng cổ tay", anchor="start")
    s.text(40, 580, "Buông tay thì tay cầm treo ở cổ tay, không rơi. Nhờ vậy không phải nắm chặt liên tục,", "ts")
    s.text(40, 596, "đây là cách chính để đỡ mỏi. Thanh móc nằm dưới gót bàn tay: khi in thử cần xem", "ts")
    s.text(40, 612, "2 tai móc có cấn gót tay không.", "ts")

    # B · luồn dây
    cx, cy, kk = 730, 180, 9.0
    s.text(620, 46, "B · LUỒN DÂY ĐEO", "h")
    s.text(620, 62, "cắt tại y = 0 phóng to; nút thòng lọng quanh thanh móc", "ts")
    Xb = lambda u: cx + (u - mh.X_THANH) * kk
    Yb = lambda v: cy + 60 - (v - mh.Z_THANH) * kk
    s.add('<clipPath id="kc36b"><rect x="620" y="76" width="280" height="240"/></clipPath><g clip-path="url(#kc36b)">')
    to_mau(s, chieu(mo.ct["than_phai"], R_CANH), Xb, Yb, "nhat")
    to_mau(s, cat_y(mo.than, 0.05), Xb, Yb, "cutv")
    to_mau(s, cat_y(mo.ct["thanh"], 0.05), Xb, Yb, "toi")
    tx_, ty_ = Xb(mh.X_THANH), Yb(mh.Z_THANH)
    s.path(f"M{fmt(tx_ + 30)},{fmt(ty_ + 80)} C{fmt(tx_ + 30)},{fmt(ty_ + 20)} {fmt(tx_ + 26)},{fmt(ty_ - 26)} {fmt(tx_)},{fmt(ty_ - 26)} "
           f"C{fmt(tx_ - 26)},{fmt(ty_ - 26)} {fmt(tx_ - 30)},{fmt(ty_ + 20)} {fmt(tx_ - 30)},{fmt(ty_ + 80)}", "day")
    s.add("</g>")
    s.rect(620, 76, 280, 240, "thin")
    s.leader(tx_, ty_, 920, 110, "thanh Ø4", anchor="start")
    s.leader(tx_ + 26, ty_ - 8, 920, 140, "khe ≈ 2,8", anchor="start", sub="dây dù hoặc dây vải mỏng")
    s.text(910, 200, "1. Gấp đôi đầu dây,", "ts")
    s.text(910, 216, "    luồn qua khe.", "ts")
    s.text(910, 232, "2. Xỏ đầu dây còn lại", "ts")
    s.text(910, 248, "    qua vòng rồi kéo chặt.", "ts")

    # C · OpenCV
    cx2, cy2, k2 = 720, 450, 2.6
    s.text(620, 346, "C · VÒNG SÁNG LÀM MỐC CHO OPENCV", "h")
    s.text(620, 362, "nhìn vuông góc mặt vòng; in nhựa màu nổi để lọc màu", "ts")
    v = next(t_ for t_ in mo.thong_so if t_["ma"] == "vong")["kich_thuoc"]
    s.add(f'<ellipse cx="{cx2}" cy="{cy2}" rx="{v[0] / 2 * k2}" ry="{v[1] / 2 * k2}" class="hl"/>')
    s.add(f'<ellipse cx="{cx2}" cy="{cy2}" rx="{(v[0] / 2 - mh.VACH) * k2}" ry="{(v[1] / 2 - mh.VACH) * k2}" class="paper" style="stroke:var(--ink,#18272c);stroke-width:1"/>')
    s.dimh(cx2 - v[0] / 2 * k2, cx2 + v[0] / 2 * k2, cy2 + v[1] / 2 * k2 + 22, f"D ≈ {so(v[0], 0)}", cy2, cy2)
    s.dimv(cy2 - v[1] / 2 * k2, cy2 + v[1] / 2 * k2, cx2 + v[0] / 2 * k2 + 22, so(v[1], 0), cx2, cx2, side=1)
    ft = [("Z = f · D / d", "khoảng cách: xa gần, phóng to thu nhỏ"), ("X = (u − c_x) · Z / f", "dịch trái phải"), ("Y = (v − c_y) · Z / f", "dịch lên xuống")]
    for i, (a, b) in enumerate(ft):
        s.text(860, 412 + i * 34, a, "code")
        s.text(860, 428 + i * 34, b, "ts")
    s.text(620, 560, "Ví dụ: f ≈ 600 px, vòng rộng 66 px trên ảnh → Z = 600 · 55 / 66 = 500 mm.", "t")
    s.text(620, 582, "Khác halo tròn Ø70 của bản V2: vòng này bầu dục và nhỏ hơn,", "tw")
    s.text(620, 598, "nên D trên ảnh đổi theo hướng xoay.", "tw")
    s.text(620, 616, "Cần lấy hướng từ BMI160 để bù. Lòng bàn tay có thể che phần sau vòng.", "ts")
    s.text(620, 632, "Chưa thử với webcam thật.", "ts")
    return s


def tat_ca():
    mo = Mo()
    return [(ten, f(mo).svg(True)) for ten, f in (("TCD-01", tcd01), ("TCD-02", tcd02), ("TCD-03", tcd03), ("TCD-04", tcd04), ("TCD-05", tcd05), ("TCD-06", tcd06))]


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    for ten, svg in tat_ca():
        p = os.path.join(THU_MUC, ten + ".svg")
        with open(p, "w", encoding="utf-8") as fh:
            fh.write(svg)
        print(p, f"{len(svg) // 1024} KB")
