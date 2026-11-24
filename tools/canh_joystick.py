"""Căn joystick tay cầm VERA: giữ cần ở từng hướng (giữa, tiến, lùi, trái, phải, nhấn) để ghi điện áp thật của 2 trục,
rồi suy ra tâm, biên, hướng gắn (JOY_DOI_XY, JOY_DAO_X, JOY_DAO_Y) và lưu kết quả ra JSON.

Chạy (từ thư mục dự án, tắt Serial Monitor / phòng thử trước vì chúng giữ cổng COM):
    python tools/canh_joystick.py            tự tìm cổng ESP32 (CH340 / CP210x)
    python tools/canh_joystick.py COM8

Cách làm: mỗi bước làm theo mũi tên trên màn hình.
  - Bước GIỮA: buông tay khỏi cần rồi bấm Space.
  - Các bước TIẾN / LÙI / TRÁI / PHẢI: đẩy cần hết cỡ theo hướng đó và giữ yên, chương trình tự ghi khi yên 1 giây.
  - Bước NHẤN: ấn cần xuống và giữ.
Phím: Space ghi bước hiện tại ngay · Backspace làm lại bước trước · A ghi kết quả vào tay cầm · Q / Esc thoát.
Ghi kết quả đã lưu mà không làm lại:  python tools/canh_joystick.py COM8 --ap  (tệp mới nhất, hoặc --ap <tệp.json>)
Lệnh H, B (ghi hướng, biên vào flash) có từ firmware bản 24/11/2026: bản cũ hơn thì nạp lại trước.
Kết quả: du_lieu/tay_cam/joystick_<giờ>.json (số đo từng bước và cấu hình suy ra).

Dữ liệu lấy từ gói thô 105 byte (lệnh r2, cần BMI160 chạy). Không có gói thô thì hỏi điện áp bằng lệnh J 10 lần/giây
(lệnh J đặt tâm trong RAM = số đo lúc đó; chương trình gửi J lại lúc cần ở giữa trước khi thoát để trả tâm đúng).
"""
import json
import os
import re
import struct
import sys
import time
from collections import deque

import cv2
import numpy as np
import serial
import serial.tools.list_ports
from PIL import Image, ImageDraw, ImageFont

GOC = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
THU_MUC_KQ = os.path.join(GOC, "du_lieu", "tay_cam")
W, H = 1000, 640
NEN, CHU, NHAT, XANH, VANG, DO, LAM = (22, 24, 28), (230, 232, 236), (140, 146, 156), (90, 200, 120), (240, 190, 70), (235, 90, 80), (90, 160, 240)

BUOC = [
    ("giua", "GIỮA", "Buông tay khỏi cần, để cần tự về giữa, rồi bấm Space", (0, 0)),
    ("tien", "TIẾN", "Đẩy cần hết cỡ về phía TRƯỚC (hướng mũi tay cầm, như đi tới) và giữ yên", (0, 1)),
    ("lui", "LÙI", "Kéo cần hết cỡ về phía SAU (về phía cổ tay, như đi lùi) và giữ yên", (0, -1)),
    ("trai", "TRÁI", "Gạt cần hết cỡ sang TRÁI và giữ yên", (-1, 0)),
    ("phai", "PHẢI", "Gạt cần hết cỡ sang PHẢI và giữ yên", (1, 0)),
    ("nhan", "NHẤN", "Ấn cần xuống (nút bấm) và giữ", (0, 0)),
]
GIU_S = 1.0          # yên chừng này giây thì tự ghi
LECH_TOI_THIEU = 500  # mV: đẩy lệch khỏi tâm ít hơn chừng này thì chưa tính là đẩy


def phong(co):
    for ten in ("segoeui.ttf", "arial.ttf"):
        p = os.path.join(os.environ.get("WINDIR", "C:/Windows"), "Fonts", ten)
        if os.path.exists(p):
            return ImageFont.truetype(p, co)
    return ImageFont.load_default()


F = {k: phong(v) for k, v in (("nho", 16), ("vua", 20), ("to", 28), ("rat_to", 44))}


def crc8(d):  # giống mang::crc8 trong firmware
    c = 0
    for x in d:
        c ^= x
        for _ in range(8):
            c = ((c << 1) ^ 0x07) & 0xFF if c & 0x80 else (c << 1) & 0xFF
    return c


def chon_cong(cong=None):
    if cong:
        return cong
    for p in serial.tools.list_ports.comports():
        if "CH340" in p.description or "CP210" in p.description:
            return p.device
    raise SystemExit("Không thấy cổng ESP32, ghi rõ: python tools/canh_joystick.py COM8")


class Nguon:
    """Đọc điện áp 2 trục joystick (mV, chưa đổi hướng) và trạng thái nút nhấn từ tay cầm."""

    def __init__(self, cong):
        s = serial.Serial()
        s.port, s.baudrate, s.timeout = cong, 921600, 0
        s.dtr = s.rts = False  # mở cổng không làm ESP32 khởi động lại
        s.open()
        self.s = s
        self.bo_dem = b""
        self.mau = deque(maxlen=400)  # (t, x, y, nhan)
        self.kieu = "goi_tho"
        self.nhan = None
        self.lan_j = 0.0
        self.thong_bao = deque(maxlen=6)
        self.gui("s0")
        time.sleep(0.2)
        self.s.reset_input_buffer()
        self.gui("r2")
        self.bat_dau = time.time()
        self.lan_goi = 0.0

    def gui(self, lenh):
        self.s.write((lenh + "\n").encode())

    def doc(self):
        self.bo_dem += self.s.read(self.s.in_waiting or 1)
        now = time.time()
        while True:
            i = self.bo_dem.find(b"\xA5\x5D")
            j = self.bo_dem.find(b"\n")
            if i < 0 and j < 0:
                self.bo_dem = self.bo_dem[-104:]
                break
            if i >= 0 and (j < 0 or i < j):  # gói thô đứng trước
                if len(self.bo_dem) < i + 105:
                    self.bo_dem = self.bo_dem[i:]
                    break
                g = self.bo_dem[i:i + 105]
                if g[2] != 1 or crc8(g[2:104]) != g[104]:  # không phải gói thật: bỏ 1 byte, tìm mã đồng bộ tiếp
                    self.bo_dem = self.bo_dem[i + 1:]
                    continue
                self.bo_dem = self.bo_dem[i + 105:]
                x, y = struct.unpack_from("<2h", g, 38)
                self.nhan = bool(g[12] & 0x04)
                self.mau.append((now, float(x), float(y), self.nhan))
                self.lan_goi = now
            else:
                dong = self.bo_dem[:j].decode("utf-8", "replace").strip()
                self.bo_dem = self.bo_dem[j + 1:]
                m = re.search(r"#JOY tam=(-?\d+),(-?\d+) mV$", dong)
                if m and self.kieu == "lenh_J":
                    self.mau.append((now, float(m.group(1)), float(m.group(2)), None))
                elif dong.startswith("#") and not dong.startswith("#JOY tam="):
                    self.thong_bao.append(dong[:90])
        # không có gói thô (BMI160 lỗi): chuyển sang hỏi điện áp bằng lệnh J
        if self.kieu == "goi_tho" and now - self.bat_dau > 2.0 and now - self.lan_goi > 1.5:
            self.kieu = "lenh_J"
            self.gui("r0")
            self.thong_bao.append("Không có gói thô (BMI160 lỗi?): đọc điện áp bằng lệnh J, 10 lần/giây")
        if self.kieu == "lenh_J" and now - self.lan_j > 0.1:
            self.gui("J")
            self.lan_j = now

    def gan_day(self, giay):
        t = time.time()
        return [m for m in self.mau if t - m[0] <= giay]

    def dong(self):
        try:
            self.gui("r0")
            time.sleep(0.1)
            self.gui("s2")
            self.s.close()
        except Exception:
            pass


def suy_ra(kq):
    """Từ số đo từng bước suy ra hướng gắn và biên. Trả về (dict cấu hình, danh sách lỗi/ghi chú)."""
    ghi_chu = []
    tam = np.array(kq["giua"]["tb"])
    d = {k: np.array(kq[k]["tb"]) - tam for k in ("tien", "lui", "trai", "phai")}
    truc_tien = int(np.argmax(np.abs(d["tien"])))
    truc_phai = int(np.argmax(np.abs(d["phai"])))
    dau_tien = 1 if d["tien"][truc_tien] > 0 else -1
    dau_phai = 1 if d["phai"][truc_phai] > 0 else -1
    if truc_tien == truc_phai:
        ghi_chu.append("LỖI: tiến và phải cùng một trục mạch, có thể đẩy sai hướng hoặc 1 trục không đổi điện áp")
    if np.sign(d["lui"][truc_tien]) != -dau_tien:
        ghi_chu.append("LỖI: lùi không ngược chiều tiến trên cùng trục")
    if np.sign(d["trai"][truc_phai]) != -dau_phai:
        ghi_chu.append("LỖI: trái không ngược chiều phải trên cùng trục")
    for k in ("tien", "lui", "trai", "phai"):
        chinh = abs(d[k]).max()
        phu = abs(d[k]).min()
        if chinh < LECH_TOI_THIEU:
            ghi_chu.append(f"Bước {k}: chỉ lệch {chinh:.0f} mV, chưa đẩy hết cỡ")
        if phu > 0.35 * chinh:
            ghi_chu.append(f"Bước {k}: trục kia cũng lệch {phu:.0f} mV (đẩy chéo?)")
    # biên theo trục MẠCH (firmware lưu am/duong theo trục mạch, trước khi đổi hướng)
    am, duong = [0.0, 0.0], [0.0, 0.0]
    for k in ("tien", "lui", "trai", "phai"):
        for i in range(2):
            if d[k][i] < 0:
                am[i] = max(am[i], -d[k][i])
            else:
                duong[i] = max(duong[i], d[k][i])
    for i, ten in enumerate("XY"):
        if tam[i] < 400 or tam[i] > 2900:
            ghi_chu.append(f"Tâm trục {ten} = {tam[i]:.0f} mV, quá lệch (thường 1400–1900): kiểm tra dây VR{ten.lower()}, GND, 3V3")
    cau_hinh = {
        "JOY_DOI_XY": 1 if truc_phai == 1 else 0,  # x của game lấy từ trục Y mạch thì hoán đổi
        "JOY_DAO_X": 1 if dau_phai < 0 else 0,
        "JOY_DAO_Y": 1 if dau_tien < 0 else 0,
        "tam_mV": [round(float(v)) for v in tam],
        "am_mV": [round(v - 30) for v in am],      # chừa 30 mV như lệnh j của firmware
        "duong_mV": [round(v - 30) for v in duong],
        "tien_la_truc": "XY"[truc_tien] + ("+" if dau_tien > 0 else "-"),
        "phai_la_truc": "XY"[truc_phai] + ("+" if dau_phai > 0 else "-"),
    }
    if "nhan" in kq:
        n = kq["nhan"].get("ty_le_nhan")
        if n is None:
            ghi_chu.append("Nút nhấn: không đo được (chế độ lệnh J)")
        elif n < 0.8:
            ghi_chu.append(f"Nút nhấn: chỉ {n * 100:.0f}% mẫu thấy nhấn, kiểm tra dây SW (GPIO27) và GND")
    return cau_hinh, ghi_chu


def lenh_ap_dung(cau_hinh):
    """Lệnh firmware (từ bản 24/11/2026) ghi hướng và biên vào flash, không cần nạp lại."""
    return [f"H{cau_hinh['JOY_DOI_XY']},{cau_hinh['JOY_DAO_X']},{cau_hinh['JOY_DAO_Y']}",
            "B" + ",".join(str(v) for v in cau_hinh["am_mV"] + cau_hinh["duong_mV"])]


def gui_ap_dung(nguon, cau_hinh):
    nguon.thong_bao.clear()
    for l in lenh_ap_dung(cau_hinh):
        nguon.gui(l)
    return time.time()


def ket_qua_ap_dung(nguon, t_gui):
    """None = đang chờ; chuỗi = kết quả."""
    tb = list(nguon.thong_bao)
    h = any(t.startswith("#JOYH") and "da luu" in t for t in tb)
    b = any(t.startswith("#JOYB") and "da luu" in t for t in tb)
    sai = [t for t in tb if t.startswith(("#JOYH", "#JOYB")) and "sai" in t]
    if h and b:
        return "Đã ghi hướng và biên vào flash tay cầm"
    if sai:
        return "Tay cầm từ chối: " + sai[0]
    if time.time() - t_gui > 2.0:
        return "Tay cầm không trả lời lệnh H/B: nạp firmware bản 24/11/2026 rồi chạy lại với --ap"
    return None


def ap_dung_tep(cong, tep):
    if not tep or tep == "moi_nhat":
        ds = sorted(f for f in os.listdir(THU_MUC_KQ) if f.startswith("joystick_") and f.endswith(".json"))
        if not ds:
            raise SystemExit("Chưa có tệp kết quả trong du_lieu/tay_cam")
        tep = os.path.join(THU_MUC_KQ, ds[-1])
    with open(tep, encoding="utf-8") as f:
        d = json.load(f)
    if any(g.startswith("LỖI") for g in d["ghi_chu"]):
        raise SystemExit(f"{tep} có lỗi suy luận, không ghi: " + "; ".join(d["ghi_chu"]))
    nguon = Nguon(chon_cong(cong))
    nguon.gui("r0")
    time.sleep(0.2)
    t = gui_ap_dung(nguon, d["cau_hinh_suy_ra"])
    print("Tệp:", tep)
    print("Gửi:", " ".join(lenh_ap_dung(d["cau_hinh_suy_ra"])))
    kq = None
    while kq is None:
        nguon.kieu = "xong"  # chỉ đọc thông báo
        nguon.doc()
        kq = ket_qua_ap_dung(nguon, t)
        time.sleep(0.01)
    print(kq)
    for l in nguon.thong_bao:
        print(" ", l)
    nguon.dong()


def ve_chu(anh, xy, chu, co="vua", mau=CHU, giua=False):
    d = ImageDraw.Draw(anh)
    f = F[co]
    if giua:
        w = d.textlength(chu, font=f)
        xy = (xy[0] - w / 2, xy[1])
    d.text(xy, chu, font=f, fill=mau)


def main(cong=None):
    nguon = Nguon(chon_cong(cong))
    ap = None  # (thời điểm gửi, kết quả) của phím A
    buoc = 0
    kq = {}
    yen_tu = None
    xong = None
    cv2.namedWindow("Can joystick - VERA Lab")
    while True:
        nguon.doc()
        now = time.time()
        gd = nguon.gan_day(0.6)
        hien = np.array([m[1:3] for m in gd]) if gd else None
        x, y = (hien[-1] if hien is not None else (np.nan, np.nan))
        tam = np.array(kq["giua"]["tb"]) if "giua" in kq else None

        # tự ghi
        ghi_ngay = False
        if buoc < len(BUOC) and hien is not None and len(hien) >= 3:
            ma = BUOC[buoc][0]
            on = float(np.linalg.norm(hien.std(0))) < 30
            dat = on
            if ma in ("tien", "lui", "trai", "phai"):
                dat = on and tam is not None and float(np.abs(hien[-1] - tam).max()) > LECH_TOI_THIEU
            elif ma == "nhan":
                dat = nguon.nhan is True or nguon.kieu == "lenh_J"
                if nguon.kieu == "lenh_J":
                    dat = False  # không đọc được nút: bấm Space để bỏ qua
            elif ma == "giua":
                dat = False      # bước giữa luôn bấm Space
            if dat:
                yen_tu = yen_tu or now
                if now - yen_tu >= GIU_S:
                    ghi_ngay = True
            else:
                yen_tu = None

        k = cv2.waitKey(15) & 0xFF
        if k in (ord("q"), 27):
            break
        if k == 8 and buoc > 0:  # Backspace: làm lại bước trước
            buoc -= 1
            kq.pop(BUOC[buoc][0], None)
            xong = None
            ap = None
            yen_tu = None
        if k in (ord("a"), ord("A")) and xong and not any(g.startswith("LỖI") for g in xong[1]) and ap is None:
            nguon.gui("r0")  # tạm dừng gói thô để đọc rõ dòng xác nhận
            ap = [gui_ap_dung(nguon, xong[0]), None]
        if ap and ap[1] is None:
            ap[1] = ket_qua_ap_dung(nguon, ap[0])
            if ap[1] is not None:
                print(ap[1])
        if k == 32 and buoc < len(BUOC):
            ghi_ngay = True

        if ghi_ngay and buoc < len(BUOC):
            ma = BUOC[buoc][0]
            m = nguon.gan_day(GIU_S)
            if len(m) >= 3:
                a = np.array([v[1:3] for v in m])
                muc = {"tb": a.mean(0).round(1).tolist(), "lech_chuan": a.std(0).round(1).tolist(), "so_mau": len(m)}
                if ma == "nhan":
                    nn = [v[3] for v in m if v[3] is not None]
                    muc["ty_le_nhan"] = (sum(nn) / len(nn)) if nn else None
                kq[ma] = muc
                buoc += 1
                yen_tu = None
                if buoc == len(BUOC):
                    cau_hinh, ghi_chu = suy_ra(kq)
                    os.makedirs(THU_MUC_KQ, exist_ok=True)
                    tep = os.path.join(THU_MUC_KQ, time.strftime("joystick_%Y%m%d_%H%M%S.json"))
                    with open(tep, "w", encoding="utf-8") as f:
                        json.dump({"thoi_gian": time.strftime("%Y-%m-%d %H:%M:%S"), "nguon": nguon.kieu, "so_do": kq,
                                   "cau_hinh_suy_ra": cau_hinh, "ghi_chu": ghi_chu}, f, ensure_ascii=False, indent=1)
                    xong = (cau_hinh, ghi_chu, tep)
                    print("Đã lưu", tep)
                    print(json.dumps(cau_hinh, ensure_ascii=False))
                    for g in ghi_chu:
                        print(" -", g)

        # ---------------- vẽ
        anh = Image.new("RGB", (W, H), NEN)
        dr = ImageDraw.Draw(anh)
        ve_chu(anh, (24, 16), "Căn joystick tay cầm VERA", "to")
        ve_chu(anh, (24, 54), f"Nguồn: {'gói thô 100 Hz' if nguon.kieu == 'goi_tho' else 'lệnh J 10 Hz'} · "
               f"X {x:5.0f} mV  Y {y:5.0f} mV" + (f" · nút {'NHẤN' if nguon.nhan else 'thả'}" if nguon.nhan is not None else ""),
               "nho", NHAT)
        # mặt joystick: điện áp thô quanh tâm (chưa đổi hướng)
        cx, cy, r = 250, 360, 190
        dr.rectangle((cx - r, cy - r, cx + r, cy + r), outline=(70, 74, 82), width=2)
        dr.line((cx - r, cy, cx + r, cy), fill=(50, 54, 60))
        dr.line((cx, cy - r, cx, cy + r), fill=(50, 54, 60))
        ve_chu(anh, (cx - r, cy + r + 6), "điện áp thô: ngang = trục X mạch, dọc = trục Y mạch (0–3300 mV)", "nho", NHAT)
        if not np.isnan(x):
            px = cx - r + x / 3300 * 2 * r
            py = cy + r - y / 3300 * 2 * r
            dr.ellipse((px - 10, py - 10, px + 10, py + 10), fill=VANG)
        for ma, muc in kq.items():
            if ma == "nhan":
                continue
            px = cx - r + muc["tb"][0] / 3300 * 2 * r
            py = cy + r - muc["tb"][1] / 3300 * 2 * r
            dr.ellipse((px - 5, py - 5, px + 5, py + 5), fill=XANH)
            ve_chu(anh, (px + 8, py - 10), dict((b[0], b[1]) for b in BUOC)[ma], "nho", XANH)

        # danh sách bước
        x0 = 500
        y0 = 110
        for i, (ma, ten, _, _) in enumerate(BUOC):
            mau = XANH if ma in kq else (VANG if i == buoc else NHAT)
            dau = "✓" if ma in kq else ("▶" if i == buoc else "·")
            gt = ""
            if ma in kq:
                t = kq[ma]["tb"]
                gt = f"X {t[0]:.0f}  Y {t[1]:.0f} mV"
                if ma == "nhan":
                    n = kq[ma].get("ty_le_nhan")
                    gt = "không đo được" if n is None else f"nhấn {n * 100:.0f}% mẫu"
            ve_chu(anh, (x0, y0 + 34 * i), f"{dau} {ten}", "vua", mau)
            ve_chu(anh, (x0 + 120, y0 + 34 * i + 3), gt, "nho", NHAT)

        if buoc < len(BUOC):
            ma, ten, huong_dan, (hx, hy) = BUOC[buoc]
            ve_chu(anh, (x0, 330), f"Bước {buoc + 1}/{len(BUOC)}: {ten}", "rat_to", VANG)
            ve_chu(anh, (x0, 390), huong_dan, "nho", CHU)
            if hx or hy:  # mũi tên hướng đẩy, nhìn tay cầm từ trên xuống, mũi hướng lên màn hình
                ax0, ay0 = 880, 330
                dr.line((ax0, ay0, ax0 + hx * 60, ay0 - hy * 60), fill=VANG, width=8)
                dr.ellipse((ax0 + hx * 60 - 12, ay0 - hy * 60 - 12, ax0 + hx * 60 + 12, ay0 - hy * 60 + 12), fill=VANG)
            tien_do = 0.0 if yen_tu is None else min(1.0, (now - yen_tu) / GIU_S)
            dr.rectangle((x0, 425, x0 + 460, 437), outline=(70, 74, 82))
            dr.rectangle((x0, 425, x0 + int(460 * tien_do), 437), fill=XANH)
            if ma in ("tien", "lui", "trai", "phai") and tam is not None and hien is not None:
                lech = float(np.abs(hien[-1] - tam).max())
                ve_chu(anh, (x0, 444), f"lệch khỏi tâm {lech:.0f} mV (cần > {LECH_TOI_THIEU})", "nho",
                       XANH if lech > LECH_TOI_THIEU else NHAT)
            if ma == "nhan" and nguon.kieu == "lenh_J":
                ve_chu(anh, (x0, 444), "Chế độ lệnh J không đọc được nút: bấm Space để bỏ qua", "nho", NHAT)
        elif xong:
            cau_hinh, ghi_chu, tep = xong
            ve_chu(anh, (x0, 320), "Xong. Cấu hình suy ra:", "vua", XANH)
            dong = [f"tiến = trục {cau_hinh['tien_la_truc']}, phải = trục {cau_hinh['phai_la_truc']}",
                    f"JOY_DOI_XY {cau_hinh['JOY_DOI_XY']}  JOY_DAO_X {cau_hinh['JOY_DAO_X']}  JOY_DAO_Y {cau_hinh['JOY_DAO_Y']}",
                    f"tâm {cau_hinh['tam_mV']}  âm {cau_hinh['am_mV']}  dương {cau_hinh['duong_mV']} mV"]
            for i, t in enumerate(dong + ghi_chu[:4]):
                ve_chu(anh, (x0, 352 + 24 * i), t, "nho", DO if t.startswith("LỖI") else CHU)
            y_cuoi = 352 + 24 * (len(dong) + min(4, len(ghi_chu))) + 8
            ve_chu(anh, (x0, y_cuoi), "Đã lưu: " + os.path.relpath(tep, GOC), "nho", NHAT)
            if any(g.startswith("LỖI") for g in ghi_chu):
                ve_chu(anh, (x0, y_cuoi + 28), "Có lỗi: Backspace làm lại các bước, chưa ghi xuống tay cầm", "vua", DO)
            elif ap is None:
                ve_chu(anh, (x0, y_cuoi + 28), "Bấm A: ghi hướng và biên vào tay cầm", "vua", VANG)
            else:
                ve_chu(anh, (x0, y_cuoi + 28), ap[1] or "Đang ghi...", "nho", XANH if ap[1] and ap[1].startswith("Đã") else VANG)

        for i, t in enumerate(list(nguon.thong_bao)[-3:]):
            ve_chu(anh, (24, H - 72 + 20 * i), t, "nho", NHAT)
        ve_chu(anh, (500, H - 32), "Space: ghi ngay · Backspace: làm lại bước trước · Q: thoát", "nho", NHAT)
        cv2.imshow("Can joystick - VERA Lab", cv2.cvtColor(np.asarray(anh), cv2.COLOR_RGB2BGR))

    # lệnh J đã đặt tâm trong RAM theo số đo; trả tâm về giữa nếu cần đang buông
    if nguon.kieu == "lenh_J" and "giua" in kq:
        t0 = time.time()
        while time.time() - t0 < 3:
            nguon.doc()
            gd = nguon.gan_day(0.3)
            if gd and np.abs(np.array(gd[-1][1:3]) - np.array(kq["giua"]["tb"])).max() < 150:
                nguon.kieu = "xong"
                nguon.gui("J")
                break
    nguon.dong()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    import argparse
    ts = argparse.ArgumentParser(description="Căn joystick tay cầm VERA")
    ts.add_argument("cong", nargs="?", help="cổng COM, ví dụ COM8 (bỏ trống: tự tìm)")
    ts.add_argument("--ap", nargs="?", const="moi_nhat", metavar="TEP_JSON",
                    help="không làm lại các bước: ghi kết quả đã lưu (mặc định tệp mới nhất) vào tay cầm")
    tham_so = ts.parse_args()
    if tham_so.ap:
        ap_dung_tep(tham_so.cong, tham_so.ap)
    else:
        main(tham_so.cong)
