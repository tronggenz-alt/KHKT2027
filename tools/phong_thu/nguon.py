"""Nguồn dữ liệu tay cầm cho phòng thử: tay cầm thật (Wi-Fi gói v3, USB gói thô), phát lại tệp .vera, tay cầm ảo.

Mọi nguồn trả về cùng một TrangThai: hướng (quaternion hệ tay cầm -> mặt đất, X trước, Y trái, Z lên),
vận tốc góc, gia tốc, joystick, mức và trạng thái 2 nút cảm ứng, nhấn cần.
"""
import json
import math
import os
import socket
import struct
import sys
import threading
import time
from dataclasses import dataclass, replace

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import ghi_tay_cam as gt  # noqa: E402  (giải mã gói thô, đọc tệp .vera)


@dataclass
class TrangThai:
    t: float = 0.0
    q: tuple = (1.0, 0.0, 0.0, 0.0)
    gyro: tuple = (0.0, 0.0, 0.0)   # °/s, hệ tay cầm
    acc: tuple = (0.0, 0.0, 1.0)    # g
    joy: tuple = (0.0, 0.0)         # -1..1 (đã hiệu chuẩn trên mạch, chưa có vùng chết)
    muc_a: int = 0
    muc_b: int = 0
    cham_a: bool = False
    cham_b: bool = False
    nhan_can: bool = False
    yen: bool = False
    loi_imu: bool = False
    nhiet: float = 0.0
    co: bool = False                # đã có dữ liệu
    bop: float = 0.0                # cò Hall 0..1 (chỉ nguồn thu_cam_bien có)
    hall_mv: int = 0


def tu_bit(s, bit):
    return replace(s, cham_a=bool(bit & 1), cham_b=bool(bit & 2), nhan_can=bool(bit & 4), yen=bool(bit & 8),
                   loi_imu=bool(bit & 0x80))


def qnhan(a, b):
    w1, x1, y1, z1 = a
    w2, x2, y2, z2 = b
    return (w1 * w2 - x1 * x2 - y1 * y2 - z1 * z2, w1 * x2 + x1 * w2 + y1 * z2 - z1 * y2,
            w1 * y2 - x1 * z2 + y1 * w2 + z1 * x2, w1 * z2 + x1 * y2 - y1 * x2 + z1 * w2)


def van_toc_tu_q(q_cu, q, dt):
    """Vận tốc góc hệ tay cầm (°/s) từ hai hướng liên tiếp: ω = 2 log(q_cũ* ⊗ q) / dt."""
    if dt <= 0:
        return (0.0, 0.0, 0.0)
    d = qnhan((q_cu[0], -q_cu[1], -q_cu[2], -q_cu[3]), q)
    if d[0] < 0:
        d = tuple(-v for v in d)
    s = math.sqrt(d[1] ** 2 + d[2] ** 2 + d[3] ** 2)
    if s < 1e-9:
        return (0.0, 0.0, 0.0)
    goc = 2 * math.atan2(s, d[0])
    k = math.degrees(goc) / (s * dt)
    return (d[1] * k, d[2] * k, d[3] * k)


class SuaTrucGan:
    """Sửa lệch gắn BMI160 (PDF trang 8): đặt tay cầm nằm ngang, rồi chúc mũi xuống khoảng 45°.

    Bước 1 cho trục Z tay cầm (hướng lên) trong hệ chip, bước 2 cho trục X (mũi). M = [X Y Z] (cột, hệ chip) đổi
    véc-tơ hệ tay cầm sang hệ chip, nên q_tay_cầm = q_chip ⊗ q(M), ω_tay_cầm = Mᵀ ω_chip. Lưu JSON để dùng lại.
    """

    def __init__(self, tep=None):
        self.tep = tep
        self.M = np.eye(3)
        self.qM = (1.0, 0.0, 0.0, 0.0)
        self.z_chip = None
        if tep and os.path.exists(tep):
            with open(tep, encoding="utf-8") as f:
                self.dat(np.array(json.load(f)["M"], float))

    def dat(self, M):
        self.M = M
        self.qM = q_tu_ma_tran(M)

    def buoc_nam_ngang(self, acc_tb):
        self.z_chip = np.asarray(acc_tb, float) / np.linalg.norm(acc_tb)

    def buoc_chuc_mui(self, acc_tb):
        """Trả về góc chúc mũi đo được (độ); < 20° thì chưa đủ để xác định trục X."""
        u = np.asarray(acc_tb, float) / np.linalg.norm(acc_tb)
        z = self.z_chip
        ngang = u - (u @ z) * z
        goc = math.degrees(math.atan2(np.linalg.norm(ngang), u @ z))
        if goc < 20:
            return goc
        x = -ngang / np.linalg.norm(ngang)  # mũi chúc xuống: hướng lên lệch về phía ĐUÔI
        y = np.cross(z, x)
        self.dat(np.stack([x, y, z], 1))
        if self.tep:
            os.makedirs(os.path.dirname(self.tep), exist_ok=True)
            with open(self.tep, "w", encoding="utf-8") as f:
                json.dump({"M": self.M.tolist(), "ghi_chu": "cột = trục X, Y, Z tay cầm trong hệ chip BMI160"}, f, indent=1)
        return goc

    def ap(self, s):
        if np.allclose(self.M, np.eye(3)):
            return s
        Mt = self.M.T
        return replace(s, q=qnhan(s.q, self.qM), gyro=tuple(Mt @ np.asarray(s.gyro)), acc=tuple(Mt @ np.asarray(s.acc)))


def q_tu_ma_tran(M):
    """Quaternion (w, x, y, z) của ma trận quay 3x3 (Shepperd)."""
    t = np.trace(M)
    if t > 0:
        s = math.sqrt(t + 1) * 2
        q = (0.25 * s, (M[2, 1] - M[1, 2]) / s, (M[0, 2] - M[2, 0]) / s, (M[1, 0] - M[0, 1]) / s)
    elif M[0, 0] > M[1, 1] and M[0, 0] > M[2, 2]:
        s = math.sqrt(1 + M[0, 0] - M[1, 1] - M[2, 2]) * 2
        q = ((M[2, 1] - M[1, 2]) / s, 0.25 * s, (M[0, 1] + M[1, 0]) / s, (M[0, 2] + M[2, 0]) / s)
    elif M[1, 1] > M[2, 2]:
        s = math.sqrt(1 + M[1, 1] - M[0, 0] - M[2, 2]) * 2
        q = ((M[0, 2] - M[2, 0]) / s, (M[0, 1] + M[1, 0]) / s, 0.25 * s, (M[1, 2] + M[2, 1]) / s)
    else:
        s = math.sqrt(1 + M[2, 2] - M[0, 0] - M[1, 1]) * 2
        q = ((M[1, 0] - M[0, 1]) / s, (M[0, 2] + M[2, 0]) / s, (M[1, 2] + M[2, 1]) / s, 0.25 * s)
    n = math.sqrt(sum(v * v for v in q))
    return tuple(v / n for v in q)


class Nguon:
    ten = "?"

    def __init__(self):
        self.s = TrangThai()
        self.khoa = threading.Lock()
        self.dem = 0
        self.mat = 0
        self.tan_so = 0.0
        self.ty_le_mat = 0.0
        self.lan_cuoi = 0.0
        self.thong_bao = []
        self._t_dem = time.time()
        self._chay = True

    def lay(self):
        with self.khoa:
            return self.s

    def _dat(self, s):
        bay_gio = time.time()
        with self.khoa:
            self.s = replace(s, t=bay_gio, co=True)
            self.dem += 1
            self.lan_cuoi = bay_gio
            if bay_gio - self._t_dem >= 1.0:
                self.tan_so = self.dem / (bay_gio - self._t_dem)
                self.ty_le_mat = self.mat / max(1, self.dem + self.mat) * 100
                self.dem = self.mat = 0
                self._t_dem = bay_gio

    def _bao(self, chu):
        self.thong_bao = (self.thong_bao + [chu])[-6:]

    def gui(self, lenh):
        pass

    def dung(self):
        self._chay = False

    def tre_ms(self):
        return (time.time() - self.lan_cuoi) * 1000 if self.lan_cuoi else float("inf")


class NguonWifi(Nguon):
    """Gói v3 37 byte (A5 5C) ở cổng UDP 4210; gửi nhịp tim H để tay cầm gửi thẳng tới máy này."""
    ten = "Tay cầm thật · Wi-Fi"
    DD = struct.Struct("<2sBBI4h3h3h2hBBBBB")

    def __init__(self, cong=4210, cong_tay_cam=4211):
        super().__init__()
        self.cong_tay_cam = cong_tay_cam
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.sock.bind(("0.0.0.0", cong))
        self.sock.settimeout(0.05)
        self.tay_cam = None
        self.seq = None
        threading.Thread(target=self._vong, daemon=True).start()

    def gui(self, lenh):
        if self.tay_cam:
            self.sock.sendto(lenh.encode(), (self.tay_cam, self.cong_tay_cam))

    def _vong(self):
        lan_nhip = 0
        while self._chay:
            try:
                d, nguon = self.sock.recvfrom(256)
            except (socket.timeout, OSError):
                d = None
            if d:
                if self.tay_cam != nguon[0]:
                    self.tay_cam = nguon[0]
                    self._bao(f"Tay cầm {self.tay_cam}")
                if d[:1] == b"#":
                    self._bao(d.decode("ascii", "replace").strip())
                elif len(d) == 37 and d[:2] == b"\xa5\x5c" and gt.crc8(d[2:36]) == d[36]:
                    (_, _, seq, _, qw, qx, qy, qz, gx, gy, gz, ax, ay, az, jx, jy, ma, mb, bit, nhiet, _) = self.DD.unpack(d)
                    if self.seq is not None:
                        self.mat += (seq - self.seq - 1) % 256
                    self.seq = seq
                    s = TrangThai(q=(qw / 16384, qx / 16384, qy / 16384, qz / 16384), gyro=(gx / 32.8, gy / 32.8, gz / 32.8),
                                  acc=(ax / 4096, ay / 4096, az / 4096), joy=(jx / 32767, jy / 32767), muc_a=ma, muc_b=mb,
                                  nhiet=nhiet - 40)
                    self._dat(tu_bit(s, bit))
            if self.tay_cam and time.time() - lan_nhip > 0.4:
                lan_nhip = time.time()
                self.gui("H\n")


class NguonUSB(Nguon):
    """Gói thô 105 byte (A5 5D, lệnh r2) qua cổng COM: có hướng tính trên mạch, gia tốc, cảm ứng, joystick."""
    ten = "Tay cầm thật · USB"

    def __init__(self, com):
        super().__init__()
        import serial
        self.com = com
        self.cong = serial.Serial(com, 921600, timeout=0)
        self.ten = f"Tay cầm thật · USB {com}"
        self.tach = gt.TachLuong()
        self.q_cu = None
        self.t_cu = None
        self.chi_so = None
        self.so_lan_mat_noi = 0
        self._bao("Mở cổng làm ESP32 khởi động lại: để yên tay cầm 3 giây")
        threading.Thread(target=self._vong, daemon=True).start()

    def gui(self, lenh):
        try:
            if self.cong:
                self.cong.write(lenh.encode())
        except Exception:  # cáp lỏng / rút ra: vòng đọc sẽ tự nối lại
            pass

    def _noi_lai(self):
        """Cổng mất (cáp lỏng, rút ra): đóng, chờ cổng xuất hiện lại rồi mở lại, không làm sập phòng thử."""
        import serial
        self.so_lan_mat_noi += 1
        self._bao(f"MẤT KẾT NỐI USB {self.com} (lần {self.so_lan_mat_noi}): kiểm tra cáp, đang chờ cắm lại...")
        try:
            self.cong.close()
        except Exception:
            pass
        self.cong = None
        while self._chay:
            time.sleep(1.0)
            try:
                self.cong = serial.Serial(self.com, 921600, timeout=0)
            except Exception:
                continue
            self.tach = gt.TachLuong()
            self.q_cu = self.t_cu = self.chi_so = None
            self._bao(f"Đã nối lại {self.com}: để yên tay cầm 3 giây (ESP32 khởi động lại)")
            return time.time()
        return time.time()

    def _vong(self):
        bat_dau = time.time()
        lan_gui = 0
        while self._chay:
            try:
                d = self.cong.read(4096)
            except Exception:
                bat_dau = self._noi_lai()
                continue
            if not d:
                time.sleep(0.003)
            for loai, v in self.tach.nap(d) if d else []:
                if loai == "chu":
                    self._bao(v)
                    continue
                h = gt.giai_ma_dau(v)
                if self.chi_so is not None:
                    self.mat += ((h["chi_so"] - self.chi_so - h["n"]) % 65536) // max(1, h["n"])
                self.chi_so = h["chi_so"]
                ax, ay, az = struct.unpack_from("<3h", v, 48 + 14 * 3 + 6)
                q = h["q"]
                t_mach = h["t_ms"] / 1000
                gyro = van_toc_tu_q(self.q_cu, q, t_mach - self.t_cu) if self.q_cu else (0.0, 0.0, 0.0)
                self.q_cu, self.t_cu = q, t_mach
                s = TrangThai(q=q, gyro=gyro, acc=(ax / 4096, ay / 4096, az / 4096), joy=h["joy"], muc_a=h["muc_a"],
                              muc_b=h["muc_b"], nhiet=h["nhiet"])
                self._dat(tu_bit(s, h["bit"]))
            if time.time() - bat_dau > 3 and time.time() - self.lan_cuoi > 2 and time.time() - lan_gui > 2:
                lan_gui = time.time()
                self.gui("r2\n")


class NguonThuCamBien(Nguon):
    """Sketch firmware/thu_cam_bien (921600 baud, dòng 'D,...' 100 lần/giây): hướng tính trên mạch theo trục chip,
    gia tốc, con quay, joystick mV, cò Hall mV, số đo cảm ứng thô. Tâm joystick, mức nền cảm ứng và mốc nhả cò
    lấy trong 1 giây đầu (lúc đó đừng chạm vào tay cầm); hành trình cò tự nới theo lần bóp sâu nhất."""
    ten = "Tay cầm thật · sketch thử cảm biến"
    TY_LE_CHAM = 0.10      # mức thay đổi khi chạm thật / mức nền (giống CHAM_TY_LE_MAC_DINH của firmware)

    def __init__(self, com, dao_x=False, dao_y=False, doi_xy=False):
        super().__init__()
        import serial
        self.com = com
        self.cong = serial.Serial(com, 921600, timeout=0.05)
        self.ten = f"Tay cầm thật · thử cảm biến {com}"
        self.dao = (dao_x, dao_y, doi_xy)
        self.mau_dau = []
        self.goc = None             # (tâm jx, tâm jy, nền A, nền B, nhả cò)
        self.hanh_trinh = 300.0     # mV, nới dần khi bóp sâu hơn
        self.cham = [False, False]
        self._bao("Mở cổng làm ESP32 khởi động lại: để yên tay cầm, không chạm, nhả cò 3 giây")
        threading.Thread(target=self._vong, daemon=True).start()

    def gui(self, lenh):
        try:
            self.cong.write(lenh.strip()[:1].encode())   # sketch thử nhận lệnh 1 ký tự: k, z, i
        except Exception:
            pass

    def _vong(self):
        du = b""
        while self._chay:
            try:
                d = self.cong.read(4096)
            except Exception:
                self._bao(f"MẤT KẾT NỐI {self.com}")
                time.sleep(1.0)
                continue
            du += d
            while b"\n" in du:
                dong, du = du.split(b"\n", 1)
                self._xu_ly(dong.decode("ascii", "replace").strip())
            if len(du) > 8000:
                du = b""

    def _xu_ly(self, dong):
        if dong.startswith("#"):
            self._bao(dong[1:].strip())
            return
        p = dong.split(",")
        if len(p) != 20 or p[0] != "D":
            return
        try:
            v = [float(x) for x in p[1:]]
        except ValueError:
            return
        q, acc, gyro = tuple(v[1:5]), tuple(v[5:8]), tuple(v[8:11])
        jx, jy, sw, hall, ta, tb, nhiet, imu = v[11:19]
        if self.goc is None:            # 1 giây đầu: lấy tâm và mức nền
            self.mau_dau.append((jx, jy, ta, tb, hall))
            if len(self.mau_dau) >= 100:
                m = np.median(np.array(self.mau_dau), axis=0)
                self.goc = tuple(float(x) for x in m)
                self._bao(f"Đã lấy mốc: joystick {m[0]:.0f}/{m[1]:.0f} mV, cảm ứng {m[2]:.0f}/{m[3]:.0f}, cò nhả {m[4]:.0f} mV")
            return
        cx, cy, nen_a, nen_b, nha = self.goc
        x = max(-1.0, min(1.0, (jx - cx) / 1500))
        y = max(-1.0, min(1.0, (jy - cy) / 1500))
        if self.dao[2]:
            x, y = y, x
        if self.dao[0]:
            x = -x
        if self.dao[1]:
            y = -y
        muc = []
        for i, (tho, nen) in enumerate(((ta, nen_a), (tb, nen_b))):
            m = abs(tho - nen) / max(1.0, nen * self.TY_LE_CHAM)
            self.cham[i] = m > 0.35 if self.cham[i] else m > 0.5          # ngưỡng có trễ giống firmware
            muc.append(int(max(0.0, min(1.0, m)) * 255))
        lech = abs(hall - nha)
        self.hanh_trinh = max(self.hanh_trinh, lech)
        bop = lech / self.hanh_trinh
        bop = 0.0 if bop < 0.03 else min(1.0, bop)
        yen = math.sqrt(sum(g * g for g in gyro)) < 2.0
        bit = (1 if self.cham[0] else 0) | (2 if self.cham[1] else 0) | (4 if sw else 0) | (8 if yen else 0) | (0 if imu else 0x80)
        s = TrangThai(q=q, gyro=gyro, acc=acc, joy=(x, y), muc_a=muc[0], muc_b=muc[1], nhiet=nhiet, bop=bop, hall_mv=int(hall))
        self._dat(tu_bit(s, bit))


class NguonPhatLai(Nguon):
    """Phát lại tệp .vera (tools/ghi_tay_cam.py) đúng nhịp thời gian đã ghi, lặp lại khi hết."""

    def __init__(self, tep, toc_do=1.0):
        super().__init__()
        self.ten = f"Phát lại · {os.path.basename(tep)}"
        with open(tep, "rb") as f:
            d = f.read()
        i = len(gt.MA_TEP)
        self.goi = []
        while i + gt.DAU_BAN_GHI.size <= len(d):
            loai, n, t = gt.DAU_BAN_GHI.unpack_from(d, i)
            i += gt.DAU_BAN_GHI.size
            v = d[i:i + n]
            i += n
            if loai == gt.LOAI_THO and len(v) == n and gt.goi_hop_le(v):
                self.goi.append((t, v))
        if not self.goi:
            raise SystemExit("Tệp không có gói dữ liệu thô")
        self.toc_do = toc_do
        threading.Thread(target=self._vong, daemon=True).start()

    def _vong(self):
        while self._chay:
            t0 = self.goi[0][0]
            bat_dau = time.time()
            q_cu = t_cu = None
            for t, v in self.goi:
                if not self._chay:
                    return
                cho = (t - t0) / self.toc_do - (time.time() - bat_dau)
                if cho > 0:
                    time.sleep(cho)
                h = gt.giai_ma_dau(v)
                ax, ay, az = struct.unpack_from("<3h", v, 48 + 14 * 3 + 6)
                t_mach = h["t_ms"] / 1000
                gyro = van_toc_tu_q(q_cu, h["q"], (t_mach - t_cu) / self.toc_do * self.toc_do) if q_cu else (0, 0, 0)
                q_cu, t_cu = h["q"], t_mach
                s = TrangThai(q=h["q"], gyro=gyro, acc=(ax / 4096, ay / 4096, az / 4096), joy=h["joy"], muc_a=h["muc_a"],
                              muc_b=h["muc_b"], nhiet=h["nhiet"])
                self._dat(tu_bit(s, h["bit"]))
            self._bao("Hết tệp, phát lại từ đầu")


class NguonAo(Nguon):
    """Tay cầm ảo bằng chuột và bàn phím (chương trình chính gọi cap_nhat mỗi khung hình).

    Chuột: xoay tay cầm (yaw, pitch). Q / E: lăn cổ tay (roll). W A S D: joystick. Chuột trái hoặc K: nút B (cầm).
    Chuột phải hoặc J: nút A. Phím cách: nhấn cần. Giữ L: lắc. Giữ F: chạm hờ A (lơ lửng).
    Có rung tay và nhiễu giả lập (tắt bằng phím N) để bài thử gần với tay cầm thật.
    """
    ten = "Tay cầm ảo · chuột + bàn phím"

    def __init__(self):
        super().__init__()
        self.yaw = self.pitch = self.roll = 0.0  # độ, hệ mặt đất
        self.q_cu = None
        self.rung = True
        self._pha = 0.0

    @staticmethod
    def q_tu_goc(yaw, pitch, roll):
        y, p, r = math.radians(yaw) / 2, math.radians(pitch) / 2, math.radians(roll) / 2
        cy, sy, cp, sp, cr, sr = math.cos(y), math.sin(y), math.cos(p), math.sin(p), math.cos(r), math.sin(r)
        return (cr * cp * cy + sr * sp * sy, sr * cp * cy - cr * sp * sy, cr * sp * cy + sr * cp * sy, cr * cp * sy - sr * sp * cy)

    def cap_nhat(self, dt, chuot_dx, chuot_dy, phim, nut_chuot):
        """phim: tập ký tự đang giữ; nut_chuot: (trái, phải)."""
        self.yaw -= chuot_dx * 0.12
        self.pitch = max(-80.0, min(80.0, self.pitch - chuot_dy * 0.12))
        if "q" in phim:
            self.roll -= 120 * dt
        if "e" in phim:
            self.roll += 120 * dt
        if "q" not in phim and "e" not in phim:
            self.roll *= max(0.0, 1 - 3 * dt)
        self._pha += dt
        ry = rp = 0.0
        if self.rung:  # run tay 9 Hz khoảng 0,15° và lắc lư chậm
            ry = 0.15 * math.sin(2 * math.pi * 9 * self._pha) + 0.4 * math.sin(2 * math.pi * 0.4 * self._pha)
            rp = 0.12 * math.sin(2 * math.pi * 10.3 * self._pha + 1) + 0.3 * math.sin(2 * math.pi * 0.31 * self._pha)
        # self.pitch dương = ngẩng mũi lên (giống tia); q_tu_goc theo ZYX nên pitch dương là chúc mũi xuống
        q = self.q_tu_goc(self.yaw + ry, -(self.pitch + rp), self.roll)
        gyro = van_toc_tu_q(self.q_cu, q, dt) if self.q_cu else (0.0, 0.0, 0.0)
        self.q_cu = q
        jx = (1 if "d" in phim else 0) - (1 if "a" in phim else 0)
        jy = (1 if "w" in phim else 0) - (1 if "s" in phim else 0)
        lac = "l" in phim
        acc = (1.6 * math.sin(2 * math.pi * 6 * self._pha), 0.0, 1.0) if lac else (0.0, 0.0, 1.0)
        b = nut_chuot[0] or "k" in phim
        a = nut_chuot[1] or "j" in phim
        muc_a = 255 if a else (90 if "f" in phim else 0)
        s = TrangThai(q=q, gyro=gyro, acc=acc, joy=(jx, jy), muc_a=muc_a, muc_b=255 if b else 0, cham_a=a, cham_b=b,
                      nhan_can=" " in phim)
        self._dat(s)

    def gui(self, lenh):
        if lenh.strip() == "z":
            self.yaw = 0.0
