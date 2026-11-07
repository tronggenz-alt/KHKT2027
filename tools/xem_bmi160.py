"""Xem BMI160 xoay trong 3D (cửa sổ OpenCV) và đo độ ổn định.

Chạy: python tools/xem_bmi160.py [COM8]
Phím: R đặt lại hướng gốc, Q thoát. Góc trôi và độ rung tính trên 10 giây gần nhất (để cảm biến yên mới đúng).
"""
import math
import sys
import time
from collections import deque

import cv2
import numpy as np
import serial
import serial.tools.list_ports


def chon_cong() -> str:
    if len(sys.argv) > 1:
        return sys.argv[1]
    for p in serial.tools.list_ports.comports():
        if "CH340" in p.description or "CP210" in p.description:
            return p.device
    raise SystemExit("Không thấy cổng ESP32, ghi rõ: python tools/xem_bmi160.py COM8")


def ma_tran(q):
    w, x, y, z = q
    return np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - w * z), 2 * (x * z + w * y)],
        [2 * (x * y + w * z), 1 - 2 * (x * x + z * z), 2 * (y * z - w * x)],
        [2 * (x * z - w * y), 2 * (y * z + w * x), 1 - 2 * (x * x + y * y)],
    ])


def nhan(a, b):
    w1, x1, y1, z1 = a
    w2, x2, y2, z2 = b
    return (w1 * w2 - x1 * x2 - y1 * y2 - z1 * z2, w1 * x2 + x1 * w2 + y1 * z2 - z1 * y2,
            w1 * y2 - x1 * z2 + y1 * w2 + z1 * x2, w1 * z2 + x1 * y2 - y1 * x2 + z1 * w2)


def euler(q):
    w, x, y, z = q
    roll = math.degrees(math.atan2(2 * (w * x + y * z), 1 - 2 * (x * x + y * y)))
    pitch = math.degrees(math.asin(max(-1, min(1, 2 * (w * y - z * x)))))
    yaw = math.degrees(math.atan2(2 * (w * z + x * y), 1 - 2 * (y * y + z * z)))
    return roll, pitch, yaw


# hộp dẹt cỡ tấm mạch, trục x đỏ, y xanh lá, z xanh dương
DINH = np.array([[sx, sy, sz] for sx in (-1.6, 1.6) for sy in (-1, 1) for sz in (-0.25, 0.25)])
CANH = [(0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7)]


def chieu(p):
    # cảm biến nằm trên bàn: z hướng lên; nhìn chéo từ phía trước
    v = np.array([p[0], -p[2], p[1]]) * 110
    v = np.array([v[0] * 0.87 - v[2] * 0.5, v[1] + v[2] * 0.35, 0])
    return int(400 + v[0]), int(300 + v[1])


def main():
    cong = chon_cong()
    # mở cổng mà không giữ DTR/RTS: giữ hai chân này làm ESP32 kẹt ở trạng thái khởi động lại
    s = serial.Serial()
    s.port, s.baudrate, s.timeout = cong, 115200, 0.05
    s.dtr = s.rts = False
    s.open()
    s.rts = True  # nhấn EN một lần để chương trình chạy lại từ đầu, kể cả bước cân con quay
    time.sleep(0.1)
    s.rts = False
    print("Đang đọc", cong, "- giữ cảm biến yên 2 giây lúc ESP32 khởi động")
    bat_dau, co_du_lieu = time.time(), False
    bo_dem = b""
    goc = (1, 0, 0, 0)
    q = (1, 0, 0, 0)
    lich_su = deque()
    dem, t0 = 0, time.time()
    hz = 0.0
    hoi_phuc = 0
    while True:
        # đọc phần đã có trong bộ đệm rồi tự tách dòng: readlines() chờ tới khi dòng dữ liệu im lặng,
        # mà firmware gửi 100 lần/giây nên nó không bao giờ trả về
        bo_dem += s.read(s.in_waiting or 1)
        *dong_moi, bo_dem = bo_dem.split(b"\n")
        if dong_moi:
            co_du_lieu = True
        elif not co_du_lieu and time.time() - bat_dau > 4:
            print("Chưa nhận được gì sau 4 giây: kiểm tra đã nạp thu_bmi160.ino, đã đóng Serial Monitor, đúng cổng COM của ESP32.")
            bat_dau = time.time() + 6
        for dong in dong_moi:
            d = dong.decode(errors="ignore").strip()
            if d.startswith("q ") or d.startswith("Q,"):
                # "q w x y z" (thu_bmi160.ino) hoặc "Q,w,x,y,z,số lần hồi phục" (ESP32_BMI160_Cube.ino)
                phan = d.replace(",", " ").split()
                try:
                    q = tuple(float(v) for v in phan[1:5])
                    if len(phan) > 5:
                        hoi_phuc = int(phan[5])
                except ValueError:
                    continue
                dem += 1
                lich_su.append((time.time(), euler(nhan((goc[0], -goc[1], -goc[2], -goc[3]), q))))
            elif d:
                print(d)
        now = time.time()
        while lich_su and now - lich_su[0][0] > 10:
            lich_su.popleft()
        if now - t0 >= 1:
            hz, dem, t0 = dem / (now - t0), 0, now

        rel = nhan((goc[0], -goc[1], -goc[2], -goc[3]), q)
        r = ma_tran(rel)
        img = np.full((600, 800, 3), 24, np.uint8)
        pts = [chieu(r @ v) for v in DINH]
        for a, b in CANH:
            cv2.line(img, pts[a], pts[b], (230, 230, 230), 2, cv2.LINE_AA)
        for truc, mau in ((0, (60, 60, 240)), (1, (60, 200, 60)), (2, (240, 140, 40))):
            e = np.zeros(3)
            e[truc] = 2.4
            cv2.arrowedLine(img, chieu(np.zeros(3)), chieu(r @ e), mau, 3, cv2.LINE_AA, tipLength=0.08)
        roll, pitch, yaw = euler(rel)
        dong_chu = [f"Roll {roll:+7.1f}  Pitch {pitch:+7.1f}  Yaw {yaw:+7.1f} do", f"Nhan {hz:4.0f} goi/giay   Hoi phuc cam bien: {hoi_phuc} lan"]
        if len(lich_su) > 50:
            a = np.array([v for _, v in lich_su])
            tg = lich_su[-1][0] - lich_su[0][0]
            rung = a.std(axis=0)
            troi = (a[-1, 2] - a[0, 2]) / max(tg, 1e-3) * 60
            dong_chu.append(f"10 s: rung roll/pitch/yaw {rung[0]:.2f}/{rung[1]:.2f}/{rung[2]:.2f} do")
            dong_chu.append(f"Troi yaw {troi:+.1f} do/phut (de yen moi dung)")
        for i, t in enumerate(dong_chu):
            cv2.putText(img, t, (16, 30 + 28 * i), cv2.FONT_HERSHEY_SIMPLEX, 0.65, (220, 220, 220), 1, cv2.LINE_AA)
        cv2.putText(img, "R: goc tren may  Z: zero tren ESP32  C: can lai gyro  Q: thoat", (16, 580), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (150, 150, 150), 1, cv2.LINE_AA)
        cv2.imshow("BMI160 - VERA Lab", img)
        k = cv2.waitKey(15) & 0xFF
        if k in (ord("q"), 27):
            break
        if k == ord("z"):
            s.write(b"z")  # firmware đặt lại hướng về 0
        if k == ord("c"):
            s.write(b"c")  # firmware cân lại con quay: để cảm biến yên
            lich_su.clear()
        if k == ord("r"):
            goc = q
            lich_su.clear()
    s.close()


if __name__ == "__main__":
    main()
