# Firmware tay cầm CŨ (giữ để tham khảo)

Tay cầm hiện tại là **V2 "Halo"**: ESP32 DevKitC 38 chân + BMI160 + joystick KY-023 + cò analog bằng cảm biến Hall SS49E. Xem [docs/15-tay-cam-v2-halo.md](../docs/15-tay-cam-v2-halo.md). Firmware cho V2 **chưa viết** (đang đặt linh kiện).

Các thư mục dưới đây là firmware của các bản đã thay:

| Thư mục | Bản tay cầm cũ |
| --- | --- |
| `tay_cam_mega/` | Arduino Mega + MPU6050, 2 joystick, 3 nút, rung, đèn (docs/01, 02) |
| `joystick_qua_mega/` | Joystick → Arduino Mega → ESP32 gửi UDP "X,Y,B" (docs/09) |
| `tay_cam_esp32cam/` | ESP32-CAM + 1 mạch joystick (docs/09) |