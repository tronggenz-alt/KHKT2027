# Tay cầm VR "VERA Arc"

Thiết kế ngày 03/11/2026, thay cho [VERA Drop](22-tay-cam-vera-drop.md). Vòng tròn nhỏ ngả ra sau có khắc logo và chữ VERA LAB, mặt tối bầu dục, pin vuông 9V nằm trong cán, thân gồm 2 nửa bắt vít. Chỉ dùng cho tay phải.

Kích thước cả bộ 81 × 71 × 123 mm. Khối lượng ước tính khoảng 137 g có pin (53 g nhựa), chưa cân thật.

Xem 3D: <https://claude.ai/artifact/FghkCUyNoxUUYSesivEQ7x>

Vòng chỉ để tạo dáng và mang logo. Theo dõi tay dùng MediaPipe ([docs/20](20-mediapipe.md)) nên không cần vòng để đo xa gần.

## Các file

- Gói gửi in: [`docs/tay-cam-arc/VERA_Arc_STL.zip`](tay-cam-arc/VERA_Arc_STL.zip) (8 file STL và hướng dẫn in).
- Mã nguồn: `tools/tay_cam_arc/`. Chạy theo thứ tự:
  1. `mo_hinh_arc.py <thư mục xem>`: dựng 3D, xuất STL.
  2. `kiem_va_cham.py`: kiểm va chạm, khe hở, hành trình cò, mỗi chi tiết phải là một khối liền.
  3. `dong_goi.py`: viết hướng dẫn in, kiểm từng STL, đóng gói zip.
  4. `trang.py <thư mục xem>`: tạo trang xem 3D.
  5. `xuat_web.py`: xuất `trang_gioi_thieu/mo_hinh/arc.bin` và `arc.json` cho trang giới thiệu, không ghi đè gói STL.
- Cần thư viện: `pip install manifold3d numpy matplotlib`. Chữ khắc dùng font Bahnschrift có sẵn trên Windows.
- Chưa có bản vẽ 2D.

## Chi tiết in 3D

| File | Số lượng | Chi tiết | Màu đề xuất | Kích thước (mm) | Nhựa | Hướng in |
| --- | --- | --- | --- | --- | --- | --- |
| `than.stl` | 1 | Thân, 2 nửa rời trong 1 file | Trắng | 163,2 × 105,3 × 34,6 | 33,2 g | Mặt ghép xuống bàn, đỡ bên trong |
| `vong.stl` | 1 | Vòng tròn có logo | Xanh ngọc huỳnh quang | 56,4 × 71 × 11 | 6,3 g | Dựng đứng trên mép sau, không cần vật đỡ |
| `day_tren.stl` | 1 | Đáy trên (mặt tối bầu dục) | Đen | 68,1 × 46,5 × 19,3 | 6,8 g | Mặt đỉnh xuống bàn |
| `day_duoi.stl` | 1 | Đáy dưới (nắp khoang pin) | Đen | 33,5 × 22,6 × 2,5 | 1,4 g | Nằm phẳng, gai khóa hướng lên |
| `co.stl` | 1 | Cò | Bạc hoặc xám | 13,8 × 25,7 × 15,6 | 3 g | Nằm nghiêng, đỡ dưới tay cò |
| `chen.stl` | 1 | Chén lục giác quanh joystick | Xanh ngọc huỳnh quang | 29,4 × 34 × 7,1 | 0,8 g | Mặt lớn xuống bàn |
| `thanh.stl` | 3 (2 dự phòng) | Ống thanh móc dây | Đen | 4 × 4 × 12,5 | 0,1 g mỗi cái | Dựng đứng |
| `nut_phu.stl` | 4 | Nắp nút phụ | Bạc hoặc xám | 8,6 × 8,6 × 5,1 | 0,9 g cả 4 | Mặt gờ xuống bàn |

Khối lượng nhựa tính theo PLA, chưa tính vật đỡ.

## Vật tư ngoài phần in

| Vật tư | Số lượng | Dùng cho |
| --- | --- | --- |
| ESP32 DevKitC 38 chân | 1 | Xử lý, Wi-Fi |
| BMI160 | 1 | Chuyển động cổ tay |
| Joystick KY-023 | 1 | Đi, quay |
| Hall SS49E, nam châm N35 Ø6 × 3, lò xo nén Ø4 dài ≈ 12 | mỗi thứ 1 | Cò analog |
| Pin vuông 9V, kẹp pin 9V | mỗi thứ 1 | Nguồn |
| Mạch hạ áp MP1584, công tắc trượt | mỗi thứ 1 | Hạ 9 V xuống 3,3 V, bật tắt |
| Nút bấm 6 × 6 mm cao 5 mm | 4 | 4 cổng mở rộng |
| Điện trở 330k, 82k, 10k; tụ 100 nF | mỗi thứ 1 | Đo pin, kéo lên cho P4, lọc tín hiệu Hall |
| Vít tự ren M2 × 10 | 6 | Ghép 2 nửa thân |
| Vít tự ren M2 × 8 | 3 | 1 ở lưng đầu, 2 giữ vòng |
| Vít tự ren M2 × 16 | 1 | Xuyên thanh móc |
| Vít tự ren M2 × 6 | 2 | Giữ đáy trên |
| Thanh thép Ø2 × 20 | 1 | Trục cò |
| Dây đeo cổ tay, băng xốp 1 mm, dây điện mảnh | | Lắp ráp |

## Thứ tự lắp

1. Đo ESP32, KY-023, pin và kẹp pin. Cắt chân header ESP32 còn khoảng 2,5 mm.
2. Chỉnh biến trở MP1584 ra đúng 3,3 V, đo bằng đồng hồ trước khi nối mạch. Hàn dây theo mục Đấu nối.
3. Mở 2 nửa thân. Đặt ESP32 lên 4 trụ trong đầu, cổng micro-USB quay ra lưng. Dán BMI160 lên module, dán MP1584 phía trên ESP32 bằng băng xốp.
4. Đặt SS49E vào hốc vách ở nửa trái, công tắc vào bụng đầu. Đặt nắp nút và nút 6 × 6 vào cổng P3, P4.
5. Dán nam châm vào cò. Đặt cò, trục Ø2, lò xo và ống thanh móc. Luồn dây gọn.
6. Khép 2 nửa, bắt vít từ nửa trái: 6 vít M2 × 10, 1 vít M2 × 8 ở lưng đầu, 1 vít M2 × 16 xuyên thanh móc. Thử bóp cò trước khi siết hẳn.
7. Đặt 2 đầu vòng vào hốc ở 2 tai sườn đầu, bắt 2 vít M2 × 8 từ phía sau.
8. Bắt KY-023 vào 4 trụ của đáy trên. Đặt chén lục giác, nắp nút và nút 6 × 6 vào P1, P2. Luồn móc phía trước xuống dưới gờ thân, hạ đáy trên xuống, siết 2 vít M2 × 6 ở lưng đầu.
9. Cắm kẹp vào pin, đẩy cả hai vào cán từ đáy, rồi trượt đáy dưới vào ray từ phía trước.

Thay pin: đẩy đáy dưới về phía trước để mở. Nạp code: gạt công tắc tắt nguồn trước, rồi cắm cáp vào cổng ở lưng đầu.

## Đấu nối

| Chân ESP32 | Nối tới |
| --- | --- |
| GPIO34, GPIO35 | VRx, VRy joystick |
| GPIO27 | SW joystick |
| GPIO32 | OUT Hall SS49E |
| GPIO21, GPIO22 | SDA, SCL BMI160 |
| GPIO33 | Điện áp pin qua chia áp 330k/82k |
| 3V3, GND | Đầu ra MP1584 |

Nguồn: cực dương pin qua công tắc vào IN+ của MP1584, cực âm pin vào IN−, OUT+ vào chân 3V3, OUT− vào GND.

- Chia áp 330k/82k biến 9 V thành khoảng 1,8 V. Giữ 100k/100k như bản cũ thì ra 4,5 V, vượt mức chịu của chân ESP32.
- Tắt công tắc trước khi cắm cáp USB. Nếu không, MP1584 và mạch ổn áp trên DevKit sẽ cùng cấp 3,3 V.

## 4 cổng mở rộng

| Cổng | Vị trí | Chân ESP32 | Dùng được cho |
| --- | --- | --- | --- |
| P1 | Mặt tối, phía trái, ngón cái | GPIO13 | Nút bấm, cảm ứng chạm, cảm biến tín hiệu số |
| P2 | Mặt tối, phía phải, ngón cái | GPIO14 | Nút bấm, cảm ứng chạm, cảm biến tín hiệu số |
| P3 | Mũi đầu, phía trái | GPIO4 | Nút bấm, cảm ứng chạm, cảm biến tín hiệu số |
| P4 | Mũi đầu, phía phải | GPIO36 | Cảm biến analog; làm nút thì thêm điện trở kéo lên 10k |

- Mỗi cổng là một lỗ Ø7 mm, phía sau có ô giữ nút 6 × 6. Muốn gắn cảm biến thì bỏ nắp nút, luồn dây qua lỗ.
- Mọi chân chỉ chịu tối đa 3,3 V. Cảm biến ra 5 V phải qua chia áp.
- GPIO13, 14 và 4 thuộc ADC2 nên không đọc được analog khi bật Wi-Fi. Chỉ P4 đọc được.
- Firmware và Unity chưa đọc 4 cổng này.

## Kiểm tra trên máy

`kiem_va_cham.py` và `dong_goi.py` chạy ngày 03/11/2026, kết quả không có va chạm ngoài ý muốn:

| Mục | Kết quả |
| --- | --- |
| Mỗi file STL | Lưới kín, đáy ở z = 0, vừa bàn in 200 × 200 mm |
| `than.stl` | 2 khối rời, đúng 2 nửa thân |
| Khe nam châm ↔ Hall | 6,5 mm lúc nhả, 3,46 mm lúc bóp hết |
| Góc bóp cò | 13°, vấu chặn tì vào vách |
| Khe cò ↔ ESP32 | 2,8 mm |

Đây là kiểm tra hình học trên mô hình, chưa phải kiểm tra trên bản in.

## Chưa xác minh

- **Kích thước linh kiện là số giả định:** ESP32 55 × 28 mm, KY-023 34 × 26 mm và khoảng cách lỗ, kẹp pin cao 8,5 mm. Cán chỉ dư khoảng 0,25 mm mỗi bên quanh pin, nên phải đo trước khi in.
- **Khối lượng 137 g** là ước tính. Riêng pin 46 g lấy theo số hãng công bố.
- **Thời lượng pin** chưa đo. Pin kiềm 9V dung lượng nhỏ, ước tính vài giờ khi bật Wi-Fi.
- **Độ thoải mái** chưa thử. Đuôi đầu nhô ra sau trên khe ngón cái có thể cấn tay. Ngón trỏ có thể phải rời cò mới bấm được P3, P4.
- **Khả năng in** chưa thử: ray đáy dưới khe 0,2 mm, gai khóa, móc đáy trên, khe lọt nắp nút. Chữ hoa cao 4,4 mm, sâu 0,6 mm, in xong nên tô màu.
- **Mặt ghép phẳng, không có chốt định vị**, nên 2 nửa có thể lệch nhẹ, ảnh hưởng tới ray đáy dưới.
- **Cò:** độ nặng tay của lò xo và đường cong điện áp phải đo thật. Đầu ngón chỉ đi được khoảng 4 mm.
- **MediaPipe** khi tay đang cầm tay cầm chưa thử. Thân và vòng có thể che ngón tay trước webcam.
- **Sơ đồ chân** chưa đối chiếu với mạch thật.
- **Vòng** chỉ nổi màu nhờ nhựa huỳnh quang, không tự sáng.
