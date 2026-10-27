# Kiến trúc phần mềm

## Luồng dữ liệu (phương án A: stream từ PC)

```
 Tay cầm (MPU6050, 2 joystick, 3 nút, LED, motor)
        │ I2C + dây nút (cáp LAN ~1 m)
 Arduino Mega ── firmware/tay_cam_mega ── 100 gói/s, 29 byte, CRC-8
        │ USB (COM, 115200)                         ▲ lệnh R/P/L/C/H
        ▼                                           │
 ┌──────────────────────── Laptop: Unity ───────────┴───────────────────────┐
 │ SerialControllerSource (luồng riêng: giải mã + lọc Mahony)                │
 │        │ ControllerSource.SampleReceived (100 Hz)                         │
 │        ├─► ControllerPoseDriver (arm model, căn lại) ─► bàn tay ảo         │
 │        ├─► KhktControllerFeeder ─► Input System ─► XR Interaction Toolkit  │
 │        ├─► SwirlDetector, BuretStopcock, PourableContainer                 │
 │        └─► MotionRecorder ─► sessions/*.csv ─► khkt/skill.py               │
 │ Mô phỏng: TitrationModel/TitrationFlask (công thức) + data/reactions       │
 │ Đồng bộ nhiều người (Photon Fusion) ◄──► máy khác                          │
 │ Hỏi AI ── HTTP ──► server/ai (FastAPI) ──► LLM (tùy chọn)                  │
 └──────────────┬────────────────────────────────────────────────────────────┘
                │ SteamVR → PhoneVR/ALVR (USB tethering hoặc wifi 5 GHz)
                ▼
 Điện thoại trong kính VR Box: hiển thị + gửi hướng đầu (3DoF)
```

Phương án B (chạy native trên điện thoại) giữ nguyên mọi khối trừ: Unity build Android với Cardboard XR Plugin, và tay cầm vào điện thoại bằng một lớp nguồn mới kế thừa `ControllerSource`.

## Các quyết định thiết kế

| Quyết định | Lý do | Thay thế đã cân nhắc |
| --- | --- | --- |
| Firmware gửi dữ liệu thô, PC tính hướng | Firmware không cần thư viện; giữ dữ liệu thô để đo run tay; dễ đổi bộ lọc | DMP trên MPU6050 (cần thư viện, firmware hãng); lọc trên Mega |
| Gói nhị phân có CRC | 25% băng thông thay vì gần 100% của CSV; bỏ được gói hỏng do nhiễu | Dòng chữ CSV |
| Đọc COM trên luồng riêng, xử lý từng mẫu | Không làm giật khung hình; bộ lọc chạy đúng dt của từng mẫu | Đọc trong Update (mất mẫu, dt sai) |
| Lớp trừu tượng `ControllerSource` | Đổi tay cầm thật / chuột-bàn phím / bản không dây sau này mà không sửa phần tương tác | Gọi thẳng SerialPort ở mọi nơi |
| Thiết bị Input System `KhktController` | XRI dùng được tay cầm tự chế bằng binding thường | Viết driver SteamVR (C++, khó); viết interactor riêng |
| Công thức khoa học có hai bản: Python (kiểm thử) và C# (chạy) | Kiểm thử được ngoài Unity; vẽ đường cong lý thuyết cho báo cáo | Chỉ C# (khó kiểm thử khi chưa có Unity) |
| pH theo bảo toàn điện tích | Một công thức cho trước/tại/sau điểm tương đương, axit mạnh và yếu | Công thức từng đoạn |
| LLM chỉ giải thích, không quyết định phản ứng | Kết quả luôn đúng theo cơ sở dữ liệu đã đối chiếu SGK | – |

## Đồng bộ nhiều người (chưa làm)

Gợi ý khi làm tuần 8 với Photon Fusion (Shared Mode):

- **Quyền sở hữu theo dụng cụ:** ai đang cầm/vặn dụng cụ nào thì máy người đó tính dụng cụ ấy (độ mở khóa, thể tích đã chảy) và gửi trạng thái. Bình nón do người giữ vai "lắc bình" sở hữu.
- **Chỉ đồng bộ trạng thái nhỏ:** độ mở khóa, thể tích, số mol đã trộn/chưa trộn, nhiệt độ, tư thế dụng cụ. Không đồng bộ dữ liệu IMU 100 Hz (chỉ ghi file tại máy).
- Giọt rơi gửi dạng sự kiện có số thứ tự để không mất và không đếm hai lần.

## Lưu trữ

- `sessions/*.csv` trong `Application.persistentDataPath`: một file mỗi lượt, cột mô tả trong `MotionRecorder.cs`.
- Thao tác chuẩn của giáo viên: ghi như học sinh với `sessionName = "chuan"`, chép vào thư mục dữ liệu chung để `khkt/skill.py` dùng làm mốc.
