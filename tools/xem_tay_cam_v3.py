"""Xem tay cầm VERA (firmware/tay_cam_vera) qua Wi-Fi: đọc gói v3 37 byte ở cổng UDP 4210.

Chạy:  python tools/xem_tay_cam_v3.py [--csv ghi.csv] [--cong 4210]
Phím:  z c J j a b g k n p x  gửi lệnh tới tay cầm (xem docs/28)
       d  đánh dấu yaw để đo trôi (hiện độ trôi và °/phút từ lúc đánh dấu)
       q  thoát
Lần đầu chạy Windows có thể hỏi tường lửa: cho phép Python nhận gói trên mạng đang dùng.
Không cần Unity. Tắt game trước (game cũng nghe cổng 4210).
"""
import argparse
import math
import socket
import struct
import sys
import time

DINH_DANG = struct.Struct("<2sBBI4h3h3h2hBBBBB")  # 37 byte
BIT = [(0x01, "A"), (0x02, "B"), (0x04, "nhan"), (0x08, "yen"), (0x10, "canlai"), (0x20, "hieuchuan"), (0x80, "LOI_IMU")]


def crc8(d):
    c = 0
    for x in d:
        c ^= x
        for _ in range(8):
            c = ((c << 1) ^ 0x07) & 0xFF if c & 0x80 else (c << 1) & 0xFF
    return c


def giai_ma(d):
    if len(d) != 37 or d[0] != 0xA5 or d[1] != 0x5C or crc8(d[2:36]) != d[36]:
        return None
    (_, ver, seq, t, qw, qx, qy, qz, gx, gy, gz, ax, ay, az, jx, jy, ma, mb, bit, nhiet, _) = DINH_DANG.unpack(d)
    q = [v / 16384 for v in (qw, qx, qy, qz)]
    return dict(seq=seq, t=t, q=q, gyro=(gx / 32.8, gy / 32.8, gz / 32.8), acc=(ax / 4096, ay / 4096, az / 4096),
                joy=(jx / 32767, jy / 32767), A=ma, B=mb, bit=bit, nhiet=nhiet - 40)


def goc(q):
    w, x, y, z = q
    yaw = math.degrees(math.atan2(2 * (w * z + x * y), 1 - 2 * (y * y + z * z)))
    pitch = math.degrees(math.asin(max(-1, min(1, 2 * (w * y - z * x)))))
    roll = math.degrees(math.atan2(2 * (w * x + y * z), 1 - 2 * (x * x + y * y)))
    return yaw, pitch, roll


def phim():
    try:
        import msvcrt
        if msvcrt.kbhit():
            return msvcrt.getwch()
    except ImportError:
        pass
    return None


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    p = argparse.ArgumentParser()
    p.add_argument("--cong", type=int, default=4210)
    p.add_argument("--cong-tay-cam", type=int, default=4211)
    p.add_argument("--csv")
    a = p.parse_args()

    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    s.bind(("0.0.0.0", a.cong))
    s.settimeout(0.05)
    print(f"Đang nghe UDP {a.cong}. Bật tay cầm (cùng Wi-Fi với máy này)...")
    csv = open(a.csv, "w", encoding="utf-8") if a.csv else None
    if csv:
        csv.write("t_pc,t_ms,seq,qw,qx,qy,qz,gx,gy,gz,ax,ay,az,joy_x,joy_y,A,B,bit,nhiet\n")

    tay_cam = None
    lan_nhip = 0
    so_goi = mat = goi_cu = 0
    seq_cu = None
    dem_tg = time.time()
    toc_do = 0.0
    ty_le_mat = 0.0
    cuoi = None
    moc = None  # (thời điểm, yaw)
    lan_ve = 0
    while True:
        try:
            d, nguon = s.recvfrom(256)
        except socket.timeout:
            d = None
        except ConnectionResetError:
            d = None
        bay_gio = time.time()
        if d:
            if tay_cam != nguon[0]:
                tay_cam = nguon[0]
                print(f"\nTay cầm: {tay_cam}")
            if d[:1] == b"#":
                print("\n" + d.decode("ascii", "replace").strip())
            elif d[:2] == b"\xa5\x5a":
                goi_cu += 1
            else:
                g = giai_ma(d)
                if g:
                    so_goi += 1
                    if seq_cu is not None:
                        mat += (g["seq"] - seq_cu - 1) % 256
                    seq_cu = g["seq"]
                    cuoi = g
                    if csv:
                        csv.write(f"{bay_gio:.4f},{g['t']},{g['seq']}," + ",".join(f"{v:.5f}" for v in g["q"]) + "," +
                                  ",".join(f"{v:.3f}" for v in g["gyro"] + g["acc"] + g["joy"]) +
                                  f",{g['A']},{g['B']},{g['bit']},{g['nhiet']}\n")
        if tay_cam and bay_gio - lan_nhip > 0.4:  # nhịp tim: tay cầm gửi thẳng tới máy này
            lan_nhip = bay_gio
            s.sendto(b"H\n", (tay_cam, a.cong_tay_cam))
        if bay_gio - dem_tg >= 1.0:
            toc_do = so_goi / (bay_gio - dem_tg)
            ty_le_mat = mat / max(1, so_goi + mat) * 100
            so_goi = mat = 0
            dem_tg = bay_gio
        k = phim()
        if k:
            if k == "q":
                break
            if k == "d" and cuoi:
                moc = (bay_gio, goc(cuoi["q"])[0])
                print("\nĐã đánh dấu yaw. Để yên hoặc dùng bình thường rồi đặt về đúng hướng cũ để đo trôi.")
            elif tay_cam and k in "zcJjabgknpx":
                s.sendto((k + "\n").encode(), (tay_cam, a.cong_tay_cam))
        if cuoi and bay_gio - lan_ve > 0.1:
            lan_ve = bay_gio
            y, pi, r = goc(cuoi["q"])
            bits = " ".join(t for b, t in BIT if cuoi["bit"] & b)
            troi = ""
            if moc:
                dy = (y - moc[1] + 180) % 360 - 180
                phut = max(1e-6, (bay_gio - moc[0]) / 60)
                troi = f" | trôi {dy:+6.2f}° ({dy / phut:+5.2f}°/ph)"
            mat_chu = f"{ty_le_mat:.1f}%"
            print(f"\r{toc_do:5.1f} Hz mất {mat_chu:>5s} | YPR {y:7.2f} {pi:6.2f} {r:7.2f} | J {cuoi['joy'][0]:+.2f} {cuoi['joy'][1]:+.2f}"
                  f" | A {cuoi['A']:3d} B {cuoi['B']:3d} | {cuoi['nhiet']:3d}C | {bits:20s}{troi}   ", end="", flush=True)
    if csv:
        csv.close()


if __name__ == "__main__":
    main()
