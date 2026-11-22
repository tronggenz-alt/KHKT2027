"""Ghi số liệu tay cầm VERA (firmware/tay_cam_vera) và vẽ biểu đồ so sánh hiệu quả các thuật toán.

GHI (tay cầm phát dữ liệu thô 200 Hz: con quay, gia tốc, nhiệt độ, cảm ứng, joystick, hướng tính trên mạch):
    python tools/ghi_tay_cam.py ghi --com COM8          qua cáp USB (không mất mẫu, nên dùng khi đo trôi)
    python tools/ghi_tay_cam.py ghi --wifi              qua Wi-Fi (cổng 4210, tắt game trước)
  Tệp ghi mặc định: du_lieu/tay_cam/phien_<ngày giờ>.vera. Ghi thêm: --ra ten.vera
  Phím khi ghi:  m  đánh dấu "tay cầm đang nằm đúng dấu trên bàn"   q  dừng và lưu
                 z c p g n j a b k  gửi lệnh tới tay cầm (docs/28)

PHÂN TÍCH (mặc định chỉ xuất kết quả Yaw):
    python tools/ghi_tay_cam.py phan_tich du_lieu/tay_cam/phien_....vera [--khong-hien]
  Ra cạnh tệp ghi:
    <tên>_troi_yaw.png       biểu đồ Yaw và sai số tại các mốc
    <tên>_yaw_du_lieu.csv    Yaw theo từng mẫu thời gian của tất cả thuật toán
    <tên>_yaw_thong_ke.csv   thống kê toàn phiên và sai số tại các mốc
    <tên>_moc.csv            bảng sai số tại các mốc (khi có ít nhất 2 mốc)

CÁCH ĐO TRÔI YAW (docs/28 mục 9):
  1. Dán một miếng băng dính làm dấu trên bàn; đặt tay cầm áp sát dấu, đúng hướng. Bắt đầu ghi, để yên 5 giây.
  2. Bấm m (mốc 0). Cầm lên dùng bình thường (quay, nghiêng, rót, ngắm...).
  3. Mỗi lần đặt tay cầm lại đúng dấu, chờ 2 giây cho yên rồi bấm m. Càng nhiều mốc càng tốt (cách nhau 1-2 phút).
  Hướng thật ở mọi mốc giống hệt mốc 0, nên chênh lệch yaw ở mốc chính là sai số trôi thật của từng thuật toán.
  Không gõ z, c và không giữ A + B trong lúc đo (sẽ đặt lại hướng giữa chừng).
"""
import argparse
import math
import os
import re
import struct
import sys
import time

import numpy as np

MA_TEP = b"VERAGHI1\n"
LOAI_THO, LOAI_CHU, LOAI_MOC = 1, 2, 3
DAU_BAN_GHI = struct.Struct("<BHd")
DAU_GOI = struct.Struct("<2sBBHIhBB4hIIIIhhhhBB")  # 48 byte
MAU = struct.Struct("<6hh")                         # 14 byte
DO_DAI_THO = 105
GYR_LSB, ACC_LSB = 32.8, 4096.0
D2R = math.pi / 180
BIT_TEN = [(0x01, "A"), (0x02, "B"), (0x04, "nhan"), (0x08, "yen"), (0x10, "canlai"), (0x20, "hieuchuan"), (0x80, "LOI_IMU")]


def crc8(d):
    c = 0
    for x in d:
        c ^= x
        for _ in range(8):
            c = ((c << 1) ^ 0x07) & 0xFF if c & 0x80 else (c << 1) & 0xFF
    return c


def goi_hop_le(d):
    return len(d) == DO_DAI_THO and d[0] == 0xA5 and d[1] == 0x5D and crc8(d[2:104]) == d[104]


def giai_ma_dau(d):
    (_, ver, n, chi_so, t_ms, nhiet, bit, che_do, qw, qx, qy, qz, tho_a, tho_b, nen_a, nen_b,
     mv_x, mv_y, jx, jy, muc_a, muc_b) = DAU_GOI.unpack_from(d, 0)
    return dict(n=n, chi_so=chi_so, t_ms=t_ms, nhiet=nhiet / 100, bit=bit, che_do=che_do,
                q=(qw / 16384, qx / 16384, qy / 16384, qz / 16384), tho_a=tho_a, tho_b=tho_b, nen_a=nen_a, nen_b=nen_b,
                mv=(mv_x, mv_y), joy=(jx / 32767, jy / 32767), muc_a=muc_a, muc_b=muc_b)


def yaw_q(w, x, y, z):
    return math.degrees(math.atan2(2 * (w * z + x * y), 1 - 2 * (y * y + z * z)))


class TachLuong:
    """Tách luồng byte USB thành gói thô (A5 5D) và dòng chữ '#...'."""

    def __init__(self):
        self.b = bytearray()

    def nap(self, du_lieu):
        self.b += du_lieu
        ra = []
        while self.b:
            if self.b[0] == 0xA5:
                if len(self.b) < 2:
                    break
                if self.b[1] == 0x5D:
                    if len(self.b) < DO_DAI_THO:
                        break
                    goi = bytes(self.b[:DO_DAI_THO])
                    if goi_hop_le(goi):
                        ra.append(("tho", goi))
                        del self.b[:DO_DAI_THO]
                        continue
                del self.b[:1]
            elif self.b[0] == ord("#"):
                k = self.b.find(b"\n")
                if k < 0:
                    if len(self.b) > 200:
                        del self.b[:1]
                    break
                ra.append(("chu", self.b[:k].decode("ascii", "replace").strip()))
                del self.b[:k + 1]
            else:
                del self.b[:1]
        return ra


# ====================================================================== GHI
def phim():
    try:
        import msvcrt
        if msvcrt.kbhit():
            return msvcrt.getwch()
    except ImportError:
        pass
    return None


def ghi(args):
    thu_muc = os.path.dirname(args.ra) if args.ra else os.path.join("du_lieu", "tay_cam")
    if thu_muc:
        os.makedirs(thu_muc, exist_ok=True)
    ten = args.ra or os.path.join(thu_muc, time.strftime("phien_%Y%m%d_%H%M%S.vera"))
    tep = open(ten, "wb")
    tep.write(MA_TEP)

    def luu(loai, du_lieu):
        tep.write(DAU_BAN_GHI.pack(loai, len(du_lieu), time.time()) + du_lieu)

    cong = None
    udp = None
    tay_cam = None
    if args.com:
        import serial
        cong = serial.Serial(args.com, 921600, timeout=0)
        print(f"Đã mở {args.com}. ESP32 khởi động lại khi mở cổng: ĐỂ YÊN tay cầm trên bàn 5 giây...")
        time.sleep(4.5)
        cong.reset_input_buffer()
        cong.write(b"r2\np\n")
    else:
        import socket
        udp = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        udp.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        udp.bind(("0.0.0.0", args.cong))
        udp.settimeout(0.02)
        print(f"Đang nghe Wi-Fi UDP {args.cong}. Bật tay cầm cùng mạng với máy này...")

    def gui(lenh):
        if cong:
            cong.write(lenh)
        elif tay_cam:
            udp.sendto(lenh, (tay_cam, args.cong_tay_cam))

    print(__doc__.split("CÁCH ĐO TRÔI YAW")[1])
    print(f"Ghi vào {ten}\n")
    tach = TachLuong()
    so_mau = mat = so_moc = 0
    chi_so_cu = None
    cuoi = None
    lan_nhip = lan_ve = lan_luu = 0
    lan_tho = time.time()
    bat_dau = time.time()
    try:
        while True:
            muc = []
            if cong:
                d = cong.read(4096)
                if d:
                    muc = tach.nap(d)
                else:
                    time.sleep(0.005)
            else:
                try:
                    d, nguon = udp.recvfrom(512)
                    if tay_cam != nguon[0]:
                        tay_cam = nguon[0]
                        print(f"\nTay cầm: {tay_cam}. Bật phát dữ liệu thô...")
                        gui(b"r1\np\n")
                    if goi_hop_le(d):
                        muc = [("tho", d)]
                    elif d[:1] == b"#":
                        muc = [("chu", d.decode("ascii", "replace").strip())]
                except (OSError, ValueError):
                    pass
            bay_gio = time.time()
            for loai, v in muc:
                if loai == "tho":
                    luu(LOAI_THO, v)
                    h = giai_ma_dau(v)
                    if chi_so_cu is not None:
                        mat += (h["chi_so"] - chi_so_cu - h["n"]) % 65536
                    chi_so_cu = h["chi_so"]
                    so_mau += h["n"]
                    cuoi = h
                    lan_tho = bay_gio
                else:
                    luu(LOAI_CHU, v.encode("utf-8"))
                    print("\n" + v)
            if udp and tay_cam and bay_gio - lan_nhip > 0.4:
                lan_nhip = bay_gio
                gui(b"H\n")
            if bay_gio - lan_tho > 2.5 and (cong or tay_cam):
                lan_tho = bay_gio
                gui(b"r2\n" if cong else b"r1\n")
            k = phim()
            if k == "q":
                break
            if k == "m":
                if cuoi is None:
                    print("\nChưa có dữ liệu, chưa đánh dấu được.")
                else:
                    luu(LOAI_MOC, struct.pack("<H", so_moc))
                    yen = "" if cuoi["bit"] & 0x08 else "  (CHÚ Ý: tay cầm chưa yên, nên chờ 2 giây rồi bấm lại)"
                    print(f"\nMốc {so_moc} lúc {bay_gio - bat_dau:.0f} s, yaw trên mạch {yaw_q(*cuoi['q']):+.2f}°{yen}")
                    so_moc += 1
            elif k and k in "zcpgnjabk":
                gui((k + "\n").encode())
            if bay_gio - lan_luu > 1:
                lan_luu = bay_gio
                tep.flush()
            if cuoi and bay_gio - lan_ve > 0.2:
                lan_ve = bay_gio
                tg = bay_gio - bat_dau
                bits = " ".join(t for b, t in BIT_TEN if cuoi["bit"] & b)
                print(f"\r{tg:6.0f} s | {so_mau / max(tg, 1e-3):5.0f} mẫu/s | mất {mat:4d} | yaw {yaw_q(*cuoi['q']):+8.2f}° | "
                      f"{cuoi['nhiet']:5.1f} C | A {cuoi['muc_a']:3d} B {cuoi['muc_b']:3d} | J {cuoi['joy'][0]:+.2f} {cuoi['joy'][1]:+.2f}"
                      f" | mốc {so_moc} | {bits:14s}", end="", flush=True)
    finally:
        gui(b"r0\n")
        if cong:
            cong.write(b"s2\n")
        tep.close()
        print(f"\n\nĐã lưu {ten}: {so_mau} mẫu, mất {mat}, {so_moc} mốc.")
        print(f"Phân tích:  python tools/ghi_tay_cam.py phan_tich \"{ten}\"")


# ====================================================================== ĐỌC TỆP
def doc_tep(ten):
    with open(ten, "rb") as f:
        d = f.read()
    if not d.startswith(MA_TEP):
        raise SystemExit("Không phải tệp ghi tay cầm VERA")
    i = len(MA_TEP)
    goi, chu, moc = [], [], []
    while i + DAU_BAN_GHI.size <= len(d):
        loai, n, t = DAU_BAN_GHI.unpack_from(d, i)
        i += DAU_BAN_GHI.size
        if i + n > len(d):
            break  # bản ghi cuối chưa ghi xong (đang ghi dở)
        v = d[i:i + n]
        i += n
        if loai == LOAI_THO and goi_hop_le(v):
            goi.append((t, v))
        elif loai == LOAI_CHU:
            chu.append((t, v.decode("utf-8", "replace")))
        elif loai == LOAI_MOC:
            moc.append(t)
    if not goi:
        raise SystemExit("Tệp chưa có gói dữ liệu thô nào")

    # dựng chuỗi mẫu liên tục theo chỉ số mẫu; mẫu bị mất thì giữ số đo trước đó
    dau = [giai_ma_dau(v) for _, v in goi]
    # chỉ số mẫu 16 bit quay vòng sau 65536 mẫu (5,5 phút): cộng dồn để có chỉ số tuyệt đối
    vt_goi = [0]
    for h0, h1 in zip(dau, dau[1:]):
        vt_goi.append(vt_goi[-1] + (h1["chi_so"] - h0["chi_so"]) % 65536)
    n_tong = vt_goi[-1] + dau[-1]["n"]
    G = np.zeros((n_tong, 3))
    A = np.zeros((n_tong, 3))
    DT = np.full(n_tong, 0.005)
    co = np.zeros(n_tong, bool)
    vi_tri = []
    for (t, v), h, p in zip(goi, dau, vt_goi):
        for j in range(h["n"]):
            gx, gy, gz, ax, ay, az, dt = MAU.unpack_from(v, 48 + 14 * j)
            if p + j < n_tong:
                G[p + j] = (gx / GYR_LSB, gy / GYR_LSB, gz / GYR_LSB)
                A[p + j] = (ax / ACC_LSB, ay / ACC_LSB, az / ACC_LSB)
                DT[p + j] = (dt + 32768) * 1e-6
                co[p + j] = True
        vi_tri.append(min(p + h["n"] - 1, n_tong - 1))
    mat = int((~co).sum())
    for k in range(1, n_tong):
        if not co[k]:
            G[k], A[k] = G[k - 1], A[k - 1]
    DT = np.where((DT <= 0) | (DT > 0.05), 0.005, DT)
    t_mau = np.cumsum(DT) - DT[0]
    vi_tri = np.array(vi_tri)
    t_goi = np.array([t for t, _ in goi])
    lo = dict(vi_tri=vi_tri, t_pc=t_goi, nhiet=np.array([h["nhiet"] for h in dau]), bit=np.array([h["bit"] for h in dau]),
              che_do=np.array([h["che_do"] for h in dau]), yaw_mach=np.array([yaw_q(*h["q"]) for h in dau]),
              tho_a=np.array([h["tho_a"] for h in dau], float), tho_b=np.array([h["tho_b"] for h in dau], float),
              nen_a=np.array([h["nen_a"] for h in dau], float), nen_b=np.array([h["nen_b"] for h in dau], float),
              mv=np.array([h["mv"] for h in dau], float), joy=np.array([h["joy"] for h in dau]),
              muc_a=np.array([h["muc_a"] for h in dau]), muc_b=np.array([h["muc_b"] for h in dau]))
    # mốc: gói cuối cùng nhận được trước lúc bấm m
    moc_mau = []
    for t in moc:
        j = np.searchsorted(t_goi, t) - 1
        if j >= 0:
            moc_mau.append(int(vi_tri[j]))
    nhiet_mau = np.interp(np.arange(n_tong), vi_tri, lo["nhiet"])
    return dict(G=G, A=A, DT=DT, t=t_mau, T=nhiet_mau, lo=lo, moc=moc_mau, chu=chu, mat=mat)


def doc_thong_so(chu):
    """Lấy hệ số nhiệt, tỉ lệ Z, thông số cảm ứng từ các dòng '#' (lệnh p lúc bắt đầu ghi)."""
    ts = dict(he_so=np.zeros(3), do_bang_n=False, co_he_so=False, ti_le=1.0, cham={}, nguong=(0.5, 0.35, 0.15))
    for _, s in chu:
        m = re.search(r"heso_nhiet=([-\d.]+),([-\d.]+),([-\d.]+) tile_z=([\d.]+) nhiet:(.*)", s)
        if m:
            ts["he_so"] = np.array([float(m.group(i)) for i in (1, 2, 3)])
            ts["ti_le"] = float(m.group(4))
            ts["do_bang_n"] = "do bang n" in m.group(5)
            ts["co_he_so"] = "chua co" not in m.group(5)
        for nut in "AB":
            m = re.search(nut + r" tho=\d+ nen=[\d.]+ tyle=([\d.]+) chieu=(-?\d)", s)
            if m:
                ts["cham"][nut] = (float(m.group(1)), int(m.group(2)))
        m = re.search(r"NGUONG bam=(\d+) nha=(\d+) lolung=(\d+)", s)
        if m:
            ts["nguong"] = tuple(int(m.group(i)) / 100 for i in (1, 2, 3))
    return ts


# ====================================================================== CHẠY LẠI THUẬT TOÁN
def cau_hinh(ts):
    moi = dict(yen=True, a_ema=0.025, toc_hoc=0.01, n_yen=100, beta=0.033, beta_dau=True, lo=0.85, hi=1.15, kep=1000,
               nhiet=True, he_so=ts["he_so"], w_nhiet=0.1 if ts["do_bang_n"] else 0.3, ti_le=ts["ti_le"], vc=0.5,
               mahony=False, kp=1.0, ki=0.0)
    tinh = dict(yen=False, a_ema=0.025, toc_hoc=0.0, n_yen=10 ** 9, beta=0.033, beta_dau=False, lo=0.85, hi=1.15, kep=1000,
                nhiet=False, he_so=np.zeros(3), w_nhiet=0.0, ti_le=1.0, vc=0.0, mahony=False, kp=1.0, ki=0.0)
    return [
        ("M1", "Madgwick + bù sai số tĩnh", "#c2452b", dict(tinh)),
        ("R1", "Mahony Ki=0 + bù sai số tĩnh", "#d98b12", dict(tinh, mahony=True)),
        ("R2", "Mahony Ki=0,02 + bù sai số tĩnh", "#b06d00", dict(tinh, mahony=True, ki=0.02)),
        ("C0", "Firmware cũ (tay_cam_bmi160)", "#7d9196", dict(tinh, yen=True, toc_hoc=0.01, n_yen=100, beta=0.08, lo=0.5, hi=1.5, kep=500)),
        ("F1", "Firmware mới (chạy lại trên máy tính)", "#0e7c86", dict(moi)),
        ("F-n", "Firmware mới, bỏ bù nhiệt", "#6fb7bd", dict(moi, nhiet=False)),
        ("F-v", "Firmware mới, bỏ vùng chết", "#8f82e3", dict(moi, vc=0.0)),
        ("F-g", "Firmware mới, bỏ tỉ lệ Z (lệnh g)", "#b3aaf0", dict(moi, ti_le=1.0)),
    ]


def qnhan(a, b):
    w1, x1, y1, z1 = a[:, 0], a[:, 1], a[:, 2], a[:, 3]
    w2, x2, y2, z2 = b[:, 0], b[:, 1], b[:, 2], b[:, 3]
    return np.stack([w1 * w2 - x1 * x2 - y1 * y2 - z1 * z2, w1 * x2 + x1 * w2 + y1 * z2 - z1 * y2,
                     w1 * y2 - x1 * z2 + y1 * w2 + z1 * x2, w1 * z2 + x1 * y2 - y1 * x2 + z1 * w2], -1)


def chay_lai(du, cfgs, k0):
    """Mỗi hàng một thuật toán, cùng một chuỗi số đo. Trả về yaw (°) từng mẫu và độ lệch Z ước lượng."""
    G, A, DT, T = du["G"], du["A"], du["DT"], du["T"]
    n = len(G)
    R = len(cfgs)
    P = lambda k: np.array([c[k] for c in cfgs], float)
    co_yen, a_ema, toc, n_yen, beta, beta_dau = P("yen") > 0, P("a_ema")[:, None], P("toc_hoc")[:, None], P("n_yen"), P("beta"), P("beta_dau") > 0
    lo, hi, kep, co_nhiet, w_nhiet = P("lo"), P("hi"), P("kep")[:, None], P("nhiet") > 0, P("w_nhiet")[:, None]
    ti_le, vc, mahony, kp, ki = P("ti_le"), P("vc"), P("mahony") > 0, P("kp")[:, None], P("ki")[:, None]
    he_so = np.stack([np.asarray(c["he_so"], float) for c in cfgs])

    g0 = np.clip(G[:k0], -kep[:, None, 0:1], kep[:, None, 0:1])
    lech = g0.mean(1)
    tb = lech.copy()
    ps = np.ones((R, 3))
    dem = np.zeros(R)
    yen = np.zeros(R, bool)
    bd = np.zeros(R)
    moc_lech = lech.copy()
    moc_T = np.full(R, T[k0])
    a0 = A[:k0].mean(0)
    phi = math.atan2(a0[1], a0[2])
    th = math.atan2(-a0[0], math.hypot(a0[1], a0[2]))
    q = np.tile([math.cos(phi / 2) * math.cos(th / 2), math.sin(phi / 2) * math.cos(th / 2),
                 math.cos(phi / 2) * math.sin(th / 2), -math.sin(phi / 2) * math.sin(th / 2)], (R, 1))
    e_tp = np.zeros((R, 3))
    yaw = np.zeros((R, n))
    lech_z = np.zeros((R, n))
    t = 0.0
    buoc_in = max(1, (n - k0) // 10)
    for k in range(k0, n):
        if (k - k0) % buoc_in == 0:
            print(f"\r  {(k - k0) * 100 // max(1, n - k0):3d}%", end="", flush=True)
        dt = DT[k]
        t += dt
        g = np.clip(np.broadcast_to(G[k], (R, 3)), -kep, kep)
        a = A[k]
        an = float(np.sqrt(a @ a))
        tb += a_ema * (g - tb)
        d = g - tb
        ps += a_ema * (d * d - ps)
        yen_nay = (np.sqrt(ps.sum(1)) < 0.6) & (abs(an - 1) < 0.06)
        dem = np.where(yen_nay, dem + 1, 0)
        yen_moi = co_yen & (dem > n_yen)
        bd = np.where(yen_moi & ~yen, t, bd)
        het = ~yen_moi & yen & (t - bd > 1.0) & co_nhiet
        if het.any():
            dT = T[k] - moc_T
            hoc = het & (np.abs(dT) >= 0.5)
            dd = np.clip((lech - moc_lech) / np.where(np.abs(dT) < 1e-6, 1, dT)[:, None], -0.05, 0.05)
            he_so = np.where(hoc[:, None], (1 - w_nhiet) * he_so + w_nhiet * dd, he_so)
            moc_lech = np.where(het[:, None], lech, moc_lech)
            moc_T = np.where(het, T[k], moc_T)
        yen = yen_moi
        c = g - lech - np.where(co_nhiet[:, None], he_so * (T[k] - moc_T)[:, None], 0.0)
        c[:, 2] *= ti_le
        lech = np.where(yen[:, None], lech + toc * (tb - lech), lech)
        c = np.where(yen[:, None], 0.0, c)
        q0, q1, q2, q3 = q[:, 0], q[:, 1], q[:, 2], q[:, 3]
        ez = np.stack([2 * (q1 * q3 - q0 * q2), 2 * (q2 * q3 + q0 * q1), 1 - 2 * (q1 * q1 + q2 * q2)], -1)
        wz = (c * ez).sum(1)
        giu = np.where(vc > 0, np.clip((np.abs(wz) - vc) / np.where(vc > 0, vc, 1), 0, 1), 1.0)
        c = c - (wz * (1 - giu))[:, None] * ez
        r = c * D2R
        hop = (an > lo) & (an < hi)
        an1 = a / an if an > 0 else a
        ax, ay, az = an1
        # Madgwick
        s0 = 4 * q0 * q2 * q2 + 2 * q2 * ax + 4 * q0 * q1 * q1 - 2 * q1 * ay
        s1 = 4 * q1 * q3 * q3 - 2 * q3 * ax + 4 * q0 * q0 * q1 - 2 * q0 * ay - 4 * q1 + 8 * q1 ** 3 + 8 * q1 * q2 * q2 + 4 * q1 * az
        s2 = 4 * q0 * q0 * q2 + 2 * q0 * ax + 4 * q2 * q3 * q3 - 2 * q3 * ay - 4 * q2 + 8 * q2 * q1 * q1 + 8 * q2 ** 3 + 4 * q2 * az
        s3 = 4 * q1 * q1 * q3 - 2 * q1 * ax + 4 * q2 * q2 * q3 - 2 * q2 * ay
        s = np.stack([s0, s1, s2, s3], -1)
        s /= np.maximum(np.linalg.norm(s, axis=1), 1e-12)[:, None]
        b = np.where(beta_dau & (t < 2.0), 0.1, beta)[:, None]
        z4 = np.zeros((R, 1))
        qd_m = 0.5 * qnhan(q, np.concatenate([z4, r], 1)) - np.where(hop[:, None], b * s, 0.0)
        # Mahony
        e = np.where(hop[:, None], np.cross(an1, ez), 0.0)
        e_tp = np.where(yen[:, None], e_tp, e_tp + ki * e * dt)
        rr = r + kp * e + np.where(yen[:, None], 0.0, e_tp)
        qd_r = 0.5 * qnhan(q, np.concatenate([z4, rr], 1))
        q = q + np.where(mahony[:, None], qd_r, qd_m) * dt
        q /= np.linalg.norm(q, axis=1)[:, None]
        yaw[:, k] = np.degrees(np.arctan2(2 * (q[:, 0] * q[:, 3] + q[:, 1] * q[:, 2]), 1 - 2 * (q[:, 2] ** 2 + q[:, 3] ** 2)))
        lech_z[:, k] = lech[:, 2]
    print("\r  100%")
    yaw[:, :k0] = yaw[:, k0:k0 + 1]
    return np.degrees(np.unwrap(np.radians(yaw), axis=1)), lech_z


def boc(x):
    return (x + 180) % 360 - 180


# ====================================================================== PHÂN TÍCH, VẼ
def phan_tich(args):
    import matplotlib
    if args.khong_hien:
        matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    du = doc_tep(args.tep)
    ts = doc_thong_so(du["chu"])
    lo = du["lo"]
    n = len(du["G"])
    k0 = min(400, n // 4)  # 2 giây đầu: bù sai số tĩnh (tay cầm phải nằm yên)
    t = du["t"]
    goc = os.path.splitext(args.tep)[0]
    sys.stdout.reconfigure(encoding="utf-8")
    print(f"{args.tep}: {n} mẫu ({t[-1] / 60:.1f} phút), mất {du['mat']} mẫu, {len(du['moc'])} mốc")
    print(f"Hệ số nhiệt {ts['he_so']} ({'đo bằng n' if ts['do_bang_n'] else 'tự học' if ts['co_he_so'] else 'chưa có'}), "
          f"tỉ lệ Z {ts['ti_le']:.4f}")
    rung_dau = np.sqrt(du["G"][:k0].var(0).sum())
    if rung_dau > 1.0:
        print(f"CHÚ Ý: 2 giây đầu tay cầm không yên (dao động {rung_dau:.2f} °/s): bù sai số tĩnh sẽ kém chính xác")
    if (lo["bit"] & 0x10).any():
        print("CHÚ Ý: có lúc đặt lại yaw trên mạch (z hoặc giữ A + B): sai số của đường 'trên mạch' sau đó không còn đúng")

    cfgs = cau_hinh(ts)
    print("Đang chạy lại các thuật toán...", flush=True)
    yaw, lech_z = chay_lai(du, [c[3] for c in cfgs], k0)
    yaw_mach = np.interp(np.arange(n), lo["vi_tri"], np.degrees(np.unwrap(np.radians(lo["yaw_mach"]))))

    moc = [m for m in du["moc"] if m >= k0]
    hang = [(ma, ten, mau, yaw[i]) for i, (ma, ten, mau, _) in enumerate(cfgs)] + [("FW", "Firmware trên mạch", "#10242b", yaw_mach)]
    loi = {}
    if len(moc) >= 2:
        for ma, ten, mau, y in hang:
            loi[ma] = boc(y[moc] - y[moc[0]])
        goc_m1 = np.mean(np.abs(loi["M1"][1:]))
        print(f"\nSai lệch yaw ở các mốc (so với mốc 0), {len(moc) - 1} mốc:")
        print(f"  {'':4s}{'thuật toán':40s}{'TB |sai|':>9s}{'lớn nhất':>10s}{'mốc cuối':>10s}{'so với M1':>11s}")
        with open(goc + "_moc.csv", "w", encoding="utf-8") as f:
            f.write("ma,ten," + ",".join(f"moc{j}_{t[m] / 60:.2f}ph" for j, m in enumerate(moc)) + "\n")
            for ma, ten, mau, y in hang:
                e = np.abs(loi[ma][1:])
                so = "" if ma == "M1" else f"{(e.mean() / max(goc_m1, 1e-9) - 1) * 100:+.0f}%"
                print(f"  {ma:4s}{ten:40s}{e.mean():8.2f}°{e.max():9.2f}°{loi[ma][-1]:+9.2f}°{so:>11s}")
                f.write(f"{ma},{ten}," + ",".join(f"{v:.3f}" for v in loi[ma]) + "\n")
        print(f"(Đã lưu {goc}_moc.csv)")
    else:
        print("\nChưa đủ 2 mốc (bấm m khi tay cầm nằm đúng dấu) nên chưa tính được sai số trôi thật. Vẫn vẽ yaw để so sánh.")

    ph = t / 60
    # ---------- Hình 1: trôi yaw ----------
    fig = plt.figure(figsize=(14, 9))
    gs = fig.add_gridspec(2, 2, height_ratios=[1.25, 1], width_ratios=[2.2, 1])
    ax1 = fig.add_subplot(gs[0, :])
    for ma, ten, mau, y in hang:
        if ma.startswith("F-"):
            continue
        ax1.plot(ph, y - y[k0], color=mau, lw=2.2 if ma in ("F1", "FW", "M1") else 1.1, ls="--" if ma == "FW" else "-", label=f"{ma} {ten}")
    for j, m in enumerate(moc):
        ax1.axvline(ph[m], color="#9fb6ba", lw=0.8, ls=":")
        ax1.text(ph[m], ax1.get_ylim()[1], f" m{j}", fontsize=8, va="top", color="#5a6d72")
    ax1.set_title("Yaw của từng thuật toán trên CÙNG một lần ghi (vạch chấm: lúc tay cầm nằm đúng dấu)")
    ax1.set_xlabel("phút")
    ax1.set_ylabel("yaw so với lúc đầu (°)")
    ax1.grid(alpha=0.25)
    ax1.legend(fontsize=8, ncol=2)
    ax2 = fig.add_subplot(gs[1, 0])
    ax3 = fig.add_subplot(gs[1, 1])
    if loi:
        for ma, ten, mau, y in hang:
            ax2.plot(ph[moc], loi[ma], marker="o", color=mau, lw=2 if ma in ("F1", "FW", "M1") else 1, ms=4,
                     ls="--" if ma == "FW" else "-", label=ma)
        ax2.axhline(0, color="#10242b", lw=0.6)
        ax2.set_title("Sai số trôi THẬT ở các mốc (hướng thật = mốc 0)")
        ax2.set_xlabel("phút")
        ax2.set_ylabel("sai lệch yaw (°)")
        ax2.grid(alpha=0.25)
        ax2.legend(fontsize=7.5, ncol=3)
        ma_s = [h[0] for h in hang]
        tb = [np.mean(np.abs(loi[m][1:])) for m in ma_s]
        cot = ax3.barh(ma_s, tb, color=[h[2] for h in hang])
        for c, v, m in zip(cot, tb, ma_s):
            ax3.text(v, c.get_y() + c.get_height() / 2, f" {v:.2f}°" + ("" if m == "M1" else f"  ({(1 - v / max(tb[0], 1e-9)) * 100:+.0f}%)"),
                     va="center", fontsize=8)
        ax3.invert_yaxis()
        ax3.set_title("TB |sai số| ở các mốc (% so với M1)")
        ax3.set_xlim(0, max(tb) * 1.55)
    else:
        ax2.text(0.5, 0.5, "Cần ít nhất 2 mốc (phím m)", ha="center", va="center", transform=ax2.transAxes)
        ax3.axis("off")
    fig.tight_layout()
    tep_bieu_do = goc + "_troi_yaw.png"
    fig.savefig(tep_bieu_do, dpi=160, bbox_inches="tight")
    plt.close(fig)

    # ---------- Chỉ xuất dữ liệu và thống kê Yaw ----------
    # CSV theo từng mẫu: các cột Yaw là góc tương đối so với mẫu k0.
    # Bao gồm cả các biến thể F-n/F-v/F-g dù biểu đồ chính lược bớt chúng để dễ đọc.
    cac_yaw = [(ma, ten, y) for ma, ten, mau, y in hang]
    tep_du_lieu = goc + "_yaw_du_lieu.csv"
    with open(tep_du_lieu, "w", newline="", encoding="utf-8-sig") as f:
        import csv
        w = csv.writer(f)
        w.writerow(["thoi_gian_s", "thoi_gian_phut"] + [f"{ma}_yaw_tuong_doi_deg" for ma, _, _ in cac_yaw])
        for i in range(n):
            w.writerow([f"{t[i]:.6f}", f"{ph[i]:.6f}"] + [f"{(y[i] - y[k0]):.6f}" for _, _, y in cac_yaw])

    # CSV thống kê. Các thống kê toàn phiên bao gồm cả lúc người dùng xoay/cầm tay cầm,
    # vì vậy KHÔNG được diễn giải chúng như nhiễu đứng yên. Sai số drift thực được
    # tính riêng tại các mốc đặt tay cầm về đúng dấu trên bàn.
    tep_thong_ke = goc + "_yaw_thong_ke.csv"
    thoi_gian_moc = ph[moc] if len(moc) else np.array([], dtype=float)
    with open(tep_thong_ke, "w", newline="", encoding="utf-8-sig") as f:
        import csv
        w = csv.writer(f)
        w.writerow([
            "ma_thuat_toan", "ten_thuat_toan", "so_mau", "thoi_luong_phut",
            "yaw_tb_tuong_doi_toan_phien_deg", "yaw_std_toan_phien_deg",
            "yaw_rms_tuong_doi_toan_phien_deg", "yaw_min_tuong_doi_toan_phien_deg",
            "yaw_max_tuong_doi_toan_phien_deg", "yaw_peak_to_peak_toan_phien_deg",
            "so_moc_tinh_sai_so", "sai_so_tuyet_doi_tb_tai_moc_deg",
            "sai_so_tuyet_doi_lon_nhat_tai_moc_deg", "sai_so_tai_moc_cuoi_deg",
            "do_doc_sai_so_tai_moc_deg_moi_phut"
        ])
        for ma, ten, y in cac_yaw:
            yr = y[k0:] - y[k0]
            mean_abs_marker = max_abs_marker = final_marker = marker_slope = ""
            so_moc_tinh = max(0, len(moc) - 1)
            if ma in loi and so_moc_tinh:
                e = loi[ma][1:]
                mean_abs_marker = f"{np.mean(np.abs(e)):.6f}"
                max_abs_marker = f"{np.max(np.abs(e)):.6f}"
                final_marker = f"{loi[ma][-1]:.6f}"
                if len(thoi_gian_moc) >= 2 and np.ptp(thoi_gian_moc) > 0:
                    marker_slope = f"{np.polyfit(thoi_gian_moc, loi[ma], 1)[0]:.6f}"
            w.writerow([
                ma, ten, n, f"{t[-1] / 60:.6f}",
                f"{np.mean(yr):.6f}", f"{np.std(yr):.6f}",
                f"{np.sqrt(np.mean(yr ** 2)):.6f}", f"{np.min(yr):.6f}",
                f"{np.max(yr):.6f}", f"{np.ptp(yr):.6f}",
                so_moc_tinh, mean_abs_marker, max_abs_marker, final_marker, marker_slope
            ])

    print(f"\nĐã lưu phân tích Yaw:")
    print(f"  {tep_bieu_do}")
    print(f"  {tep_du_lieu}")
    print(f"  {tep_thong_ke}")
    if len(moc) >= 2:
        print(f"  {goc}_moc.csv")
    print("Lưu ý: thống kê toàn phiên có cả chuyển động; đánh giá drift thực bằng các mốc đặt đúng dấu.")
    if not args.khong_hien:
        plt.show()
    return


def main():
    p = argparse.ArgumentParser(description="Ghi và phân tích tay cầm VERA")
    sub = p.add_subparsers(dest="lenh", required=True)
    g = sub.add_parser("ghi")
    g.add_argument("--com", help="cổng USB, ví dụ COM8 (không ghi thì dùng Wi-Fi)")
    g.add_argument("--wifi", action="store_true")
    g.add_argument("--cong", type=int, default=4210)
    g.add_argument("--cong-tay-cam", type=int, default=4211)
    g.add_argument("--ra")
    a = sub.add_parser("phan_tich")
    a.add_argument("tep")
    a.add_argument("--khong-hien", action="store_true", help="chỉ lưu ảnh, không mở cửa sổ")
    args = p.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")
    if args.lenh == "ghi":
        ghi(args)
    else:
        phan_tich(args)


if __name__ == "__main__":
    main()
