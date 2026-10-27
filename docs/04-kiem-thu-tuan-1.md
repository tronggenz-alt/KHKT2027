# Đo kiểm tuần 1–2

Mục tiêu: biết sớm phần cứng có dùng được không, và có số liệu thật cho báo cáo. Ghi kết quả vào bảng cuối tài liệu.

## 1. Tay cầm (chưa cần Unity)

1. Nạp `firmware/tay_cam_mega/tay_cam_mega.ino`, mở Serial Monitor 115200: thấy dòng `#KHKT-TAYCAM v1 imu=OK` (lẫn ký tự lạ của gói nhị phân là bình thường). Đóng Serial Monitor.
2. Chạy công cụ xem:

   ```bash
   python tools/tay_cam_monitor.py --port COM5
   ```

   **Đạt:** khoảng 100 Hz; `mat` và `crc` gần như không tăng khi lắc mạnh và uốn cáp; tay cầm nằm ngang thì `nghieng` khoảng 0; chúc đầu xuống thì `pitch` dương.
3. Thử rung: `--rung 255 --ms 300` rồi `--rung 120 --ms 60`. Ghi lại mức thấp nhất và xung ngắn nhất còn cảm nhận được (với nguồn 5 V từ USB và với adapter 7–9 V).
4. Trôi yaw: để yên tay cầm trên bàn, chạy `--do-troi 300`. Làm 3 lần. **Ghi °/phút.**
5. Rút cáp USB khi đang rung: motor phải dừng trong khoảng 2 giây, đèn xanh nháy chậm.
6. Nếu thấy `LOI-IMU`: kiểm tra cặp xoắn SDA/GND và SCL/GND trong cáp LAN, rút ngắn cáp.

## 2. Hiển thị và độ trễ

1. PhoneVR + SteamVR với cảnh Unity đơn giản (một căn phòng, một bàn). Kiểm tra kết nối cáp USB tethering và wifi 5 GHz.
2. **Đo độ trễ khi quay đầu:** đặt kính (có điện thoại) trên một mặt quay được, quay video chậm (240 khung/s nếu điện thoại quay phim có chế độ này) cả kính lẫn màn hình điện thoại. Xoay nhanh rồi dừng đột ngột; đếm số khung từ lúc kính dừng tới lúc hình trên màn dừng. Độ trễ ≈ số khung ÷ 240 giây. Đo 10 lần, lấy trung vị.
3. Làm lại bước 2 với bản build Android Cardboard của cùng cảnh (phương án B), nếu nhóm quyết định thử.
4. Cho 3–5 bạn đeo mỗi phương án 5 phút; điền bảng hỏi SSQ trước và sau.

## 3. Hiệu chỉnh mô hình bằng số liệu thật

1. **Rót:** đổ 50 ml nước vào cốc 100 ml thật, quay video từ bên cạnh khi nghiêng chậm. Ghi góc bắt đầu chảy và thời gian rót hết khi giữ một góc cố định. So với `khkt/pour.py` (dự đoán bắt đầu chảy ở khoảng 61°), chỉnh `weir_coef` cho khớp.
2. **Giọt buret:** cho 100 giọt từ buret thật vào ống đong, đo thể tích, tính thể tích trung bình mỗi giọt và độ lệch. Cập nhật `dropMl`, `dropJitterMl`.
3. **Đường cong chuẩn độ:** nếu trường có máy đo pH, đo pH theo thể tích NaOH và đặt cạnh `khkt.titration.curve(...)`.

## Bảng kết quả

| Phép đo | Lần 1 | Lần 2 | Lần 3 | Ghi chú |
| --- | --- | --- | --- | --- |
| Tần số gói (Hz) | | | | |
| Mất gói / lỗi CRC sau 5 phút | | | | |
| Trôi yaw (°/phút) | | | | |
| Mức rung thấp nhất cảm nhận được (0–255) | | | | |
| Độ trễ quay đầu, phương án A (ms) | | | | |
| Độ trễ quay đầu, phương án B (ms) | | | | |
| SSQ trung bình, A / B | | | | |
| Góc bắt đầu rót, cốc thật 50 ml (°) | | | | |
| Thể tích một giọt buret thật (ml) | | | | |
