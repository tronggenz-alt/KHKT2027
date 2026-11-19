"""Đo mất gói của tay cầm VERA qua USB (gói thô r2, mã đồng bộ A5 5D) và ghi các dòng thông báo '#'.

Chạy: python tools/do_mat_goi_usb.py COM8 40      (cầm tay cầm lên, lắc trong lúc đo)
In ra: số gói mỗi giây, khoảng hở lớn nhất giữa hai gói, các lần hở > 50 ms, mọi dòng '#' (lỗi I2C, khởi động lại...).
"""
import sys
import time

import serial

cong = sys.argv[1] if len(sys.argv) > 1 else "COM8"
giay = float(sys.argv[2]) if len(sys.argv) > 2 else 40.0

s = serial.Serial()
s.port, s.baudrate, s.timeout = cong, 921600, 0.005
s.dtr = s.rts = False   # không giữ DTR/RTS: ESP32 không bị khởi động lại
s.open()
time.sleep(0.3)
s.write(b"r2\n")
t0 = time.time()
lan_goi_cuoi = None
so_goi = 0
ho = []           # (thời điểm, độ dài hở ms)
dong_chu = b""
buf = b""
thong_bao = []
while time.time() - t0 < giay:
    buf += s.read(4096)
    now = time.time()
    # tách gói nhị phân A5 5D; phần chữ (dòng '#') nằm xen giữa
    while True:
        i = buf.find(b"\xA5\x5D")
        if i < 0:
            dong_chu += buf[:-1]
            buf = buf[-1:]
            break
        dong_chu += buf[:i]
        if len(buf) - i < 105:
            buf = buf[i:]
            break
        buf = buf[i + 105:]
        so_goi += 1
        if lan_goi_cuoi is not None:
            d = (now - lan_goi_cuoi) * 1000
            if d > 50:
                ho.append((now - t0, d))
        lan_goi_cuoi = now
    while b"\n" in dong_chu:
        dong, dong_chu = dong_chu.split(b"\n", 1)
        d = dong.decode(errors="ignore").strip()
        k = d.find("#")
        if k >= 0:
            thong_bao.append(f"{now - t0:6.2f}s {d[k:]}")
    if time.time() - t0 > 3 and lan_goi_cuoi and now - lan_goi_cuoi > 2:
        s.write(b"r2\n")   # ESP32 khởi động lại thì quên r2: xin lại
        lan_goi_cuoi = now
        thong_bao.append(f"{now - t0:6.2f}s (không có gói 2 giây: gửi lại r2)")
s.write(b"r0\n")
s.close()
print(f"Gói nhận: {so_goi} trong {giay:.0f} s = {so_goi / giay:.1f} gói/s (mong đợi 100 gói/s: 200 mẫu/s, 2 mẫu/gói)")
print(f"Số lần hở > 50 ms: {len(ho)}; hở lớn nhất: {max([h for _, h in ho], default=0):.0f} ms")
for t, h in ho[:30]:
    print(f"  hở {h:6.0f} ms lúc {t:6.2f} s")
print("Thông báo từ tay cầm:")
for d in thong_bao[:60]:
    print(" ", d)
