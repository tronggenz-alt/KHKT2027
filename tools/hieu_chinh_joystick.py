"""Hiệu chỉnh hướng joystick của tay cầm VERA qua USB (lệnh h của firmware tay_cam_vera).

Chạy: python tools/hieu_chinh_joystick.py COM8
Làm theo dòng in ra: đẩy cần LÊN (về phía mũi tay cầm) hết cỡ và giữ, buông, đẩy sang PHẢI hết cỡ và giữ.
Kết quả lưu trong flash của ESP32 (không cần nạp lại), in ra doi_xy / dao_x / dao_y.
"""
import sys
import time

import serial

cong = sys.argv[1] if len(sys.argv) > 1 else "COM8"
s = serial.Serial()
s.port, s.baudrate, s.timeout = cong, 921600, 0.05
s.dtr = s.rts = False   # không làm ESP32 khởi động lại
s.open()
time.sleep(0.3)
s.write(b"r0\n")
time.sleep(0.1)
s.write(b"s0\n")
time.sleep(0.2)
s.reset_input_buffer()
s.write(b"h\n")
print("Đã gửi lệnh h. Làm theo hướng dẫn:")
t0 = time.time()
buf = b""
xong = False
while time.time() - t0 < 45 and not xong:
    buf += s.read(4096)
    *dong, buf = buf.split(b"\n")
    for d in dong:
        d = d.decode(errors="ignore").strip()
        if "HUONG_JOY" in d:
            print(" ", d.lstrip("#"), flush=True)
            if "xong" in d or "het gio" in d:
                xong = True
s.write(b"p\n")
time.sleep(0.5)
for d in s.read(8192).decode(errors="ignore").splitlines():
    if d.startswith("#JOY"):
        print(" ", d.lstrip("#"))
s.close()
