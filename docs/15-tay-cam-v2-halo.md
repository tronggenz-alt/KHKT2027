# Tay cầm VR V2 "Halo"

Thiết kế ngày 27/10/2026, thay cho bản V1 ở [docs/13](13-ban-ve-tay-cam-vr.md) (ống PVC + hộp thắt lưng).

## Bộ mạch

| Linh kiện | Ghi chú |
| --- | --- |
| ESP32 DevKitC 38 chân | Gửi dữ liệu qua Wi-Fi |
| BMI160 | Đo chuyển động cổ tay: vặn, gập, lắc |
| Joystick KY-023 | Đi tới, lùi, quay trái, quay phải |
| Cảm biến Hall tuyến tính SS49E | Làm **cò analog** |

Không còn Arduino Mega và cảm biến siêu âm. Phần dịch chuyển xa gần và phóng to thu nhỏ do OpenCV đảm nhận, đo qua vòng halo.

Hình dáng tay cầm:

- Cán cầm ngả 20°, mặt cắt 37 × 33 mm (≈ Ø35, tối ưu cho bàn tay dài 177 mm, xem docs/13).
- Đầu tay cầm có mặt joystick ngửa 15° về phía ngón cái.
- Cò nằm dưới ngón trỏ.
- Vòng halo Ø70 ôm quanh mũi tay cầm.
- Dải màu huỳnh quang chạy dọc lưng cán.
- Kích thước tổng thể 120 × 160 × 70 mm, khoảng 135 g kể cả mạch, chưa tính pin.

Các file:

- Chi tiết in 3D: [`docs/tay-cam-v2/in/`](tay-cam-v2/in/) (7 file STL, đã xoay đúng hướng in).
- Bản vẽ: [`docs/tay-cam-v2/TC2-01..05`](tay-cam-v2/), mỗi bản có cả `.svg` và `.png`.
- Mã nguồn: `tools/tay_cam_v2/`. Chạy theo thứ tự:
  1. `mo_hinh.py`: dựng 3D, xuất STL và dữ liệu hình chiếu.
  2. `ve_2d_v2.py`: vẽ bản 2D.
  3. `xuat_png.sh`: xuất PNG.
  4. `trang.py`: tạo trang xem 3D.
- Cần thư viện: `pip install manifold3d numpy`.

## Chi tiết in 3D

| File | Chi tiết | Kích thước in (mm) | Nhựa | Hướng in |
| --- | --- | --- | --- | --- |
| `vo_trai.stl`, `vo_phai.stl` | Vỏ hai nửa | 105 × 131 × 21 | 26 g mỗi nửa | Mặt ghép úp xuống bàn |
| `co.stl` | Cò Hall | 15 × 36 × 12 | 5 g | Nằm nghiêng |
| `halo.stl` | Vòng halo + 2 tay đỡ | 70 × 70 × 38 | 10 g | Mặt trước xuống bàn |
| `tam_op.stl` | Tấm ốp mặt joystick | 40 × 27 × 1,6 | 1 g | Phẳng |
| `nap_day.stl` | Nắp đáy, cửa USB-C, tai dây đeo | 42 × 33 × 12,5 | 6 g | Đáy xuống bàn |
| `dai_sang.stl` | Dải màu lưng cán | 4 × 84 × 3 | 1 g | Nằm |

**Cách in:**

- Nhựa PETG hoặc PLA, lớp 0,2 mm, 3 lớp vách, độ đặc 25%. Tổng khoảng 75 g nhựa.
- In vòng halo và dải lưng cán bằng **nhựa màu huỳnh quang** (xanh ngọc hoặc vàng chanh). Màu càng nổi thì OpenCV lọc HSV càng dễ.
- In cò, tấm ốp và nắp đáy bằng màu tối.

**Vật tư lắp ráp:**

- 2 vít M2 × 10 tự ren để ghép hai nửa vỏ.
- 1 vít M3 × 16 làm chốt cò.
- 2 vít M3 × 12 bắt vòng halo vào hai bên đầu.
- Nam châm neodymium N35 Ø6 × 3.
- Lò xo nén Ø4 × 8.
- Dây đeo cổ tay.

## Đấu nối (TC2-04)

| Linh kiện | Chân | ESP32 |
| --- | --- | --- |
| KY-023 | +5V, GND | 3V3, GND (cấp 3,3 V để tín hiệu không vượt mức chịu của ESP32) |
| | VRx, VRy, SW | GPIO34, GPIO35, GPIO27 (INPUT_PULLUP) |
| SS49E | VCC, GND, OUT | 3V3, GND, GPIO32 (thêm tụ 100 nF từ OUT xuống GND) |
| BMI160 | VIN, GND, SDA, SCL | 3V3, GND, GPIO21, GPIO22 (địa chỉ 0x68) |
| Pin (tính sau) | chia áp 100k/100k | GPIO33 |

- Mọi tín hiệu analog dùng chân **ADC1**, vì ADC2 không đọc được khi Wi-Fi đang bật.
- GPIO34/35 là chân chỉ vào, không có điện trở kéo trong. Đọc analog thì không cần kéo.

## Cò analog bằng cảm biến Hall (TC2-03)

**Cấu tạo:**

- Nam châm Ø6 × 3 dán ở mặt sau cò.
- SS49E nằm trong vách đỡ đối diện. Vách này cũng là chặn hành trình cò.
- Cò quay quanh vít M3. Bóp cò thì nam châm lại gần cảm biến: khe hở 6,5 mm lúc nhả, khoảng 3,8 mm (tính ở tâm nam châm) lúc bóp hết. Cò quay 11° thì vấu chặn tì vào vách.

**Ước tính tín hiệu** (mô hình nam châm trụ N35, SS49E ≈ 0,92 mV/G ở 3,3 V):

- Điện áp ra tăng từ khoảng 1,87 V lên 2,22 V (236 G lên 618 G).
- ESP32 đọc được khoảng 2466 → 2931, tức khoảng 465 mức. Vẫn đủ cho thao tác tinh.
- Khe hở được chọn để SS49E chưa bão hòa (bão hòa ở khoảng ±1000 G).
- Lật mặt nam châm thì điện áp giảm thay vì tăng. Phần mềm xử lý được cả hai chiều.

**Phần mềm:**

1. Lúc khởi động, đọc mức nhả cò (min).
2. Người dùng bóp hết một lần để lấy mức max.
3. Tính % = (đọc − min) / (max − min).
4. Đường cong không thẳng, nên dùng bảng tra 16 điểm để bóp đều tay thì % tăng đều.

**Dùng trong game:**

- Bóp quá 60% là cầm vật.
- Phần % liên tục dùng để bóp quả bóp pipet và mở khóa buret nhiều hay ít.

## Vòng halo cho OpenCV (TC2-05)

1. Webcam lọc màu huỳnh quang theo HSV, rồi gọi `cv2.fitEllipse` để lấy tâm (u, v) và hai trục của elip.
2. **Khoảng cách:** Z = f · D / d, với D = 70 mm là đường kính thật của halo, d là trục lớn của elip trên ảnh (px), f là tiêu cự webcam (px).
3. **Ví dụ:** f ≈ 600 px, d = 84 px thì Z = 500 mm.
4. **Vị trí ngang, dọc:** X = (u − c_x) · Z / f và Y = (v − c_y) · Z / f.
5. **Độ nghiêng halo:** cos φ = trục nhỏ / trục lớn. Đối chiếu với BMI160.

Trục lớn của elip không đổi khi halo nghiêng, nên khoảng cách vẫn đo đúng khi cổ tay xoay. Dải màu lưng cán là điểm màu thứ hai, cho OpenCV bắt khi tay quay lưng về camera.

## Kiểm kích thước và va chạm

Script `tools/tay_cam_v2/kiem_va_cham.py` kiểm từng cặp chi tiết và linh kiện: tính thể tích chồng lấn và khe hở nhỏ nhất. Nó cũng quét cò từ 0° tới 11°.

| Vị trí | Khe hở |
| --- | --- |
| ESP32 trong rãnh kẹp | 0,2 mm (mép mạch ăn sâu 1 mm vào rãnh) |
| ESP32 ↔ pin | 0,6 mm |
| Pin ↔ mạch sạc | 0,7 mm |
| SS49E trong hốc vách | 0,2 mm, có khe luồn 3 chân |
| Tấm ốp ↔ hốc chìm | 0,2 mm |
| Cò ↔ thành khe | 0,5 mm |
| Cò ↔ vách Hall suốt hành trình | ≥ 0,3 mm |

- Các cặp dán hoặc tựa lên nhau, như BMI160 trên module ESP32 và nam châm trong hốc cò, chạm mặt nhưng không lấn vào nhau.
- Cò chỉ chạm vỏ ở đúng 11°, khi vấu chặn tì vào vách Hall.
- Kết quả hiện tại: không có va chạm ngoài ý muốn.

Khi đổi kích thước linh kiện, chạy lại script trước khi in.

## Cần kiểm trước khi in

- **ESP32 nằm dọc trong cán, giữa hai rãnh kẹp.**
  - Phải tháo chân header, hoặc mua loại chưa hàn chân, rồi hàn dây thẳng.
  - Bản vẽ giả định mạch 55 × 28 mm. Đo mạch thật rồi sửa `LINH_KIEN` trong `mo_hinh.py`.
- **KY-023:** bẻ thẳng chân rồi hàn dây, vì đầu tay cầm không đủ chỗ cắm dupont.
- **Khoang pin** dành cho LiPo 40 × 25 × 9 (≈ 1000 mAh) và mạch sạc TP4056 USB-C 25 × 18. Phần nguồn (hạ áp 3,3 V hay tăng áp 5 V) chốt sau.
- **In thử riêng cò và một nửa vỏ trước** để thử khe nam châm và độ nặng tay của lò xo.
- **Đường cong điện áp của cò** là ước tính theo mô hình. Phải đo thật: ghi giá trị ADC ở 5 vị trí cò.

## Nguồn

- Đường kính tay nắm tối ưu: Kong & Lowe (2005), https://stacks.cdc.gov/view/cdc/197408
- Số đo bàn tay người Việt: Imrhan, Nguyen & Nguyen (1993), trích trong https://openaccess-api.cms-conferences.org/articles/download/978-1-4951-2105-0_32
- Lỗ chân và sơ đồ chân ESP32 DevKitC lấy theo tài liệu Espressif ESP32-DevKitC V4. Đối chiếu lại với mạch thật khi có hàng.
