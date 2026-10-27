"""Xem và kiểm tra tay cầm không cần Unity.

Ví dụ:
    python tools/tay_cam_monitor.py --liet-ke
    python tools/tay_cam_monitor.py --port COM5
    python tools/tay_cam_monitor.py --port COM5 --ghi phien1.csv
    python tools/tay_cam_monitor.py --port COM5 --rung 200 --ms 300
    python tools/tay_cam_monitor.py --port COM5 --do-troi 300     # đo trôi yaw 5 phút, để yên tay cầm

Phím tắt khi đang chạy: không có; dừng bằng Ctrl+C.
"""

from __future__ import annotations

import argparse
import csv
import os
import sys
import time

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from khkt.fusion import Mahony  # noqa: E402
from khkt.protocol import BTN_RECENTER, FLAG_IMU_ERROR, PacketParser  # noqa: E402

try:
    import serial
    import serial.tools.list_ports
except ImportError:
    sys.exit("Thiếu pyserial: pip install pyserial")

CSV_COLS = ["t_ms", "seq", "ax", "ay", "az", "gx", "gy", "gz", "qw", "qx", "qy", "qz",
            "j1x", "j1y", "j2x", "j2y", "buttons"]


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--port", help="cổng COM của Arduino Mega, ví dụ COM5")
    ap.add_argument("--baud", type=int, default=115200)
    ap.add_argument("--liet-ke", action="store_true", help="liệt kê cổng COM rồi thoát")
    ap.add_argument("--ghi", help="ghi dữ liệu ra file CSV (cùng định dạng Unity)")
    ap.add_argument("--rung", type=int, help="thử rung mức 0-255")
    ap.add_argument("--ms", type=int, default=300, help="thời gian xung rung")
    ap.add_argument("--do-troi", type=float, metavar="GIAY", help="đo độ trôi yaw khi để yên trong N giây")
    ap.add_argument("--esp32", action="store_true",
                    help="tay cầm ESP32-CAM (đế nạp ESP32-CAM-MB): mở cổng không bật DTR/RTS để ESP32 không bị khởi động lại")
    args = ap.parse_args()

    if args.liet_ke or not args.port:
        for p in serial.tools.list_ports.comports():
            print(f"{p.device:8} {p.description}")
        return

    if args.esp32:
        ser = serial.Serial(baudrate=args.baud, timeout=0.02)
        ser.port = args.port
        ser.dtr = False  # đặt trước khi mở: DTR/RTS của đế MB nối vào chân EN và IO0 của ESP32
        ser.rts = False
        ser.open()
        time.sleep(0.2)
    else:
        ser = serial.Serial(args.port, args.baud, timeout=0.02)
        time.sleep(2.0)  # Mega tự khởi động lại khi mở cổng; chờ hiệu chuẩn con quay xong
    ser.write(b"I\n")
    if args.rung is not None:
        ser.write(f"P{args.rung},{args.ms}\n".encode())

    parser = PacketParser()
    fusion = Mahony()
    writer = None
    if args.ghi:
        f = open(args.ghi, "w", newline="", encoding="utf-8")
        writer = csv.writer(f)
        writer.writerow(CSV_COLS)

    last_t = None
    first = True
    n_window, t_window = 0, time.time()
    last_heartbeat = 0.0
    drift_start = None
    start_time = time.time()
    try:
        while True:
            now = time.time()
            if now - last_heartbeat > 0.4:
                ser.write(b"H\n")
                last_heartbeat = now
            for pkt in parser.feed(ser.read(256)):
                acc, gyr = pkt.accel_g, pkt.gyro_dps
                if first:
                    fusion.reset_from_accel(acc)
                    first = False
                dt = 0.01 if last_t is None else max(0.001, min(0.05, (pkt.t_ms - last_t) / 1000.0))
                last_t = pkt.t_ms
                fusion.update(gyr, acc, dt)
                if pkt.pressed(BTN_RECENTER):
                    fusion.reset_from_accel(acc)
                n_window += 1
                roll, pitch, yaw = fusion.euler_deg()
                if writer:
                    writer.writerow([pkt.t_ms, pkt.seq, *(f"{v:.5f}" for v in acc), *(f"{v:.3f}" for v in gyr),
                                     *(f"{v:.6f}" for v in fusion.q), *(f"{v:.3f}" for v in pkt.joy), pkt.buttons])
                if args.do_troi is not None:
                    if drift_start is None and now - start_time > 3:  # bỏ 3 s đầu cho bộ lọc ổn định
                        drift_start = (now, yaw)
                    if drift_start and now - drift_start[0] >= args.do_troi:
                        d = (yaw - drift_start[1] + 180) % 360 - 180
                        mins = (now - drift_start[0]) / 60
                        print(f"\nTrôi yaw: {d:+.2f}° trong {mins:.1f} phút = {d / mins:+.2f} °/phút")
                        return
            for m in parser.messages:
                print(f"\n[firmware] {m}")
            parser.messages.clear()
            if now - t_window >= 0.5 and n_window:
                hz = n_window / (now - t_window)
                n_window, t_window = 0, now
                roll, pitch, yaw = fusion.euler_deg()
                err = " LOI-IMU" if pkt.pressed(FLAG_IMU_ERROR) else ""
                print(f"\r{hz:5.1f} Hz  mat={parser.lost_packets:4d} crc={parser.crc_errors:3d}  "
                      f"roll={roll:+6.1f} pitch={pitch:+6.1f} yaw={yaw:+7.1f} nghieng={fusion.tilt_deg():5.1f}  "
                      f"j1=({pkt.joy[0]:+.2f},{pkt.joy[1]:+.2f}) j2=({pkt.joy[2]:+.2f},{pkt.joy[3]:+.2f}) "
                      f"nut={pkt.buttons:08b}{err}   ", end="", flush=True)
    except KeyboardInterrupt:
        pass
    finally:
        ser.write(b"R0\n")
        ser.close()
        if writer:
            f.close()
            print(f"\nĐã ghi {args.ghi}")


if __name__ == "__main__":
    main()
