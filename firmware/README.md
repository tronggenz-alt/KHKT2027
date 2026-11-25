# Firmware tay cầm

**Bản hiện tại: `tay_cam_vera/`**: ESP32 DevKit V1 + joystick KY-023 + BMI160 + 2 nút cảm ứng điện dung (GPIO13, 14). Gửi gói v3 và gói cũ 29 byte qua Wi-Fi UDP 4210, có chống trôi yaw. Xem [docs/28-firmware-tay-cam-vera.md](../docs/28-firmware-tay-cam-vera.md).

| Thư mục | Dùng để |
| --- | --- |
| `tay_cam_vera/` | Firmware tay cầm hiện tại |
| `tay_cam_bmi160/` | Bản chỉ có BMI160, in quaternion qua USB (so sánh độ trôi với bản mới) |
| `thu_bmi160/` | Thử đọc BMI160 |

Các thư mục dưới đây là firmware của các bản đã thay, giữ để tham khảo:

| Thư mục | Bản tay cầm cũ |
| --- | --- |
| `tay_cam_mega/` | Arduino Mega + MPU6050, 2 joystick, 3 nút, rung, đèn (docs/01, 02) |
| `joystick_qua_mega/` | Joystick → Arduino Mega → ESP32 gửi UDP "X,Y,B" (docs/09) |
| `tay_cam_esp32cam/` | ESP32-CAM + 1 mạch joystick (docs/09) |