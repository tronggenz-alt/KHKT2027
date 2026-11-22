# Tay cầm VR "VERA Drop"

> Bản này đã được thay bằng [VERA Arc](24-tay-cam-vera-arc.md).

Thiết kế ngày 02/11/2026, thay cho bản V2 "Halo" ở [docs/15](15-tay-cam-v2-halo.md). Dáng giọt nước, nhỏ gọn, thân trắng, đầu đen, một vòng quanh cổ. Thân gồm 2 nửa bắt vít để mở ra lắp mạch.

Kích thước cả bộ 59 × 42 × 106 mm, kể cả cần joystick và thanh móc dây. Khối lượng ước tính khoảng 85 g có pin, chưa cân thật.

Xem 3D: <https://claude.ai/artifact/Efcr62HioxoWEL7qehCgz6>

## Các file

- Chi tiết in 3D: [`docs/tay-cam-drop/in/`](tay-cam-drop/in/) (7 file STL, đã xoay đúng hướng in).
- Bản vẽ: [`docs/tay-cam-drop/TCD-01..06`](tay-cam-drop/), mỗi bản có cả `.svg` và `.png`.
- Mã nguồn: `tools/tay_cam_drop/`. Chạy theo thứ tự:
  1. `mo_hinh_drop.py <thư mục xem>`: dựng 3D, xuất STL.
  2. `kiem_va_cham.py`: kiểm va chạm, khe hở, hành trình cò, và mỗi chi tiết phải là một khối liền.
  3. `ve_2d_drop.py`: vẽ bản 2D.
  4. `xuat_png.sh`: xuất PNG.
  5. `trang.py <thư mục xem>`: tạo trang xem 3D.
- Cần thư viện: `pip install manifold3d numpy matplotlib`.

| Bản vẽ | Nội dung |
| --- | --- |
| [TCD-01](tay-cam-drop/TCD-01.png) | Hình chiếu, kích thước, mặt cắt eo |
| [TCD-02](tay-cam-drop/TCD-02.png) | Bố trí linh kiện từng nửa thân, bảng xếp chồng theo chiều y |
| [TCD-03](tay-cam-drop/TCD-03.png) | Mặt ghép 2 nửa, vít, thanh móc dây, thứ tự lắp |
| [TCD-04](tay-cam-drop/TCD-04.png) | Cò Hall, đường cong điện áp ước tính |
| [TCD-05](tay-cam-drop/TCD-05.png) | Đấu nối ESP32 và nguồn |
| [TCD-06](tay-cam-drop/TCD-06.png) | Cách cầm, luồn dây đeo, mốc cho OpenCV |

## Chi tiết in 3D

| File | Chi tiết | Kích thước in (mm) | Nhựa | Hướng in |
| --- | --- | --- | --- | --- |
| `than_phai.stl` | Nửa thân phải, có chữ khắc | 58,1 × 77 × 20 | 9,5 g | Mặt ghép úp xuống, cần vật đỡ |
| `than_trai.stl` | Nửa thân trái, có hốc đầu vít | 58,1 × 77 × 20 | 10,4 g | Mặt ghép úp xuống, cần vật đỡ |
| `nap.stl` | Nắp đầu, màu đen | 54,9 × 42,2 × 32,8 | 8,6 g | Mặt đỉnh úp xuống |
| `vong.stl` | Vòng quanh cổ | 55,1 × 41,3 × 4 | 1,2 g | Nằm phẳng |
| `chen.stl` | Chén lõm quanh joystick | 30 × 30 × 7,5 | 0,8 g | Mặt lớn úp xuống |
| `co.stl` | Cò Hall, màu đen | 11,9 × 26,3 × 15,6 | 1,7 g | Nằm nghiêng |
| `thanh.stl` | Ống thanh móc dây, màu đen | 4 × 4 × 12,5 | 0,1 g | Dựng đứng |

Khối lượng nhựa tính theo PLA, chưa tính vật đỡ.

## Vật tư ngoài phần in

| Vật tư | Số lượng | Dùng cho |
| --- | --- | --- |
| ESP32 DevKitC 38 chân | 1 | Xử lý, Wi-Fi |
| BMI160 | 1 | Chuyển động cổ tay |
| Joystick KY-023 | 1 | Đi, quay |
| Hall SS49E, nam châm N35 Ø6 × 3, lò xo nén Ø4 dài ≈ 12 | mỗi thứ 1 | Cò analog |
| Pin LiPo 1000 mAh 40 × 25 × 9, TP4056 USB-C có bảo vệ, công tắc trượt | mỗi thứ 1 | Nguồn |
| Mạch hạ áp 3,3 V từ 500 mA (ví dụ AP2112K), 2 tụ 10 µF | 1 bộ | Cấp 3V3 cho ESP32 |
| 2 điện trở 100k, tụ 100 nF | 1 bộ | Đo pin, lọc tín hiệu Hall |
| Vít tự ren M2 × 10 | 4 | V1 đến V4, ghép 2 nửa thân |
| Vít tự ren M2 × 16 | 1 | V5, xuyên thanh móc |
| Vít tự ren M2 × 6 | 2 | Giữ nắp |
| Thanh thép Ø2 × 20 | 1 | Trục cò |
| Dây đeo cổ tay, băng xốp 1 mm, dây điện mảnh | | Lắp ráp |

## Thứ tự lắp

1. Đo mạch thật, cắt chân header ESP32 còn khoảng 2,5 mm, hàn dây theo TCD-05.
2. Nửa trái: đặt ESP32 lên 2 gờ, dán BMI160 lên module, đặt SS49E vào hốc vách.
3. Nửa phải: dán pin bằng băng xốp, đặt TP4056 và công tắc vào cửa đáy.
4. Đặt cò, trục Ø2, lò xo và ống thanh móc vào nửa trái. Luồn dây gọn.
5. Khép nửa phải, lật lại, siết V1 đến V5 từ nửa trái. Thử bóp cò trước khi siết hẳn.
6. Đặt vòng lên miệng thân. Bắt KY-023 vào 4 trụ của nắp, đặt chén lõm, cắm dây.
7. Úp nắp, gờ nắp lồng vào miệng thân, siết 2 vít M2 × 6 ở lưng.

## Đấu nối

Chân ESP32 giữ nguyên như bản V2:

| Chân | Nối tới |
| --- | --- |
| GPIO34, GPIO35 | VRx, VRy joystick |
| GPIO27 | SW joystick |
| GPIO32 | OUT Hall SS49E |
| GPIO21, GPIO22 | SDA, SCL BMI160 |
| GPIO33 | Điện áp pin qua chia áp 100k/100k |

Mọi tín hiệu analog dùng chân ADC1, vì ADC2 không đọc được khi Wi-Fi đang bật.

Nguồn: pin nối B+, B− của TP4056; OUT+ qua công tắc vào mạch hạ áp 3,3 V; đầu ra nối chân 3V3 của ESP32.

- Không cắm pin vào chân 5V: mạch hạ áp trên DevKit cần điện áp cao hơn pin.
- Đổi so với bản V2: bản vẽ TC2-04 ghi HT7333, nhưng con này chỉ cho 250 mA, không đủ khi ESP32 bật Wi-Fi. Bản này dùng mạch từ 500 mA.

## Kiểm tra trên máy

`kiem_va_cham.py` chạy ngày 02/11/2026 trên mô hình, kết quả không có va chạm ngoài ý muốn:

| Mục | Kết quả |
| --- | --- |
| Mỗi chi tiết in | Một khối liền, lưới kín |
| Khe thân ↔ cò suốt hành trình | ≥ 0,44 mm, chỉ chạm ở vấu chặn khi bóp hết |
| Khe cò ↔ ESP32 lúc bóp hết | 0,89 mm |
| Khe nam châm ↔ Hall | 6,5 mm lúc nhả, 3,58 mm lúc bóp hết |
| Khe vít ↔ ESP32 | 0,7 mm |

Đây là kiểm tra hình học trên mô hình, chưa phải kiểm tra trên bản in.

## Chưa xác minh

- **Kích thước linh kiện là số giả định:** ESP32 55 × 28 mm, KY-023 34 × 26 mm và khoảng cách lỗ của nó. Đo mạch thật rồi sửa trong `mo_hinh_drop.py`, vì nhiều chỗ chỉ hở dưới 0,5 mm.
- **Khối lượng 85 g** là ước tính.
- **Độ thoải mái** chưa thử. Eo tương đương trụ Ø37,5, lớn hơn mức tối ưu Ø35 cho bàn tay dài 177 mm ([docs/13](13-ban-ve-tay-cam-vr.md)). Cần xem 2 tai móc có cấn gót tay không.
- **Khả năng in** chưa thử trên máy thật. 2 nửa thân và 2 tai móc cần vật đỡ. Chữ hoa cao 3,2 mm, sâu 0,6 mm, in xong nên tô bút sơn.
- **Mặt ghép phẳng, không có gờ khóa**, nên 2 nửa có thể lệch nhẹ và thấy khe ghép.
- **Cò:** đường cong điện áp là ước tính, phải đo ADC thật. Độ nặng tay của lò xo chưa thử.
- **Vòng sáng** chỉ phát sáng trên hình. Muốn sáng thật phải thêm dải LED, mô hình chưa có chỗ.
- **OpenCV:** vòng bầu dục 55 × 41 mm nhỏ hơn halo tròn Ø70 của V2, nên đường kính trên ảnh đổi theo hướng xoay. Cần lấy hướng từ BMI160 để bù, và chưa thử với webcam thật.
- **Mạch hạ áp** chưa có hốc riêng trong mô hình, tạm gói co nhiệt đi cùng dây.
