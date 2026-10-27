# Giao thức tay cầm (Arduino Mega ↔ PC)

Phiên bản 1. Cổng COM, **115200 baud**, 8N1. Firmware: `firmware/tay_cam_mega/`. Bộ giải mã tham chiếu: `khkt/protocol.py` (Python) và `Phong thi nghiem ao/Assets/Scripts/Input/PacketParser.cs` (C#).

## Vì sao gửi dữ liệu thô dạng nhị phân

- **Thô:** Mega chỉ gửi gia tốc và vận tốc góc đã trừ sai lệch (bias); phần tính hướng (bộ lọc Mahony) chạy trên PC. Firmware không cần thư viện ngoài, và dữ liệu thô là thứ cần để đo độ run tay.
- **Nhị phân:** 29 byte × 100 gói/giây = 2,9 KB/s, khoảng 25% băng thông 115200 baud. Dạng chữ CSV cần khoảng 80 byte/gói, gần chạm trần, dễ trễ và mất gói.
- **Có mã kiểm tra CRC-8:** dây cáp LAN dài dễ nhiễu; gói hỏng bị bỏ thay vì cho số sai vào mô phỏng.

## Hệ trục tay cầm

Firmware xuất dữ liệu theo **hệ trục tay cầm** (hệ thuận tay phải), không theo cách gắn chip:

| Trục | Hướng khi cầm tay cầm như cầm đèn pin |
| --- | --- |
| X | Dọc ống PVC, hướng ra đầu tay cầm (phía trước) |
| Y | Sang trái |
| Z | Lên trên |

Nếu gắn MPU6050 lệch trục, sửa macro `TRUC_X/Y/Z` trong firmware, không sửa phía PC. Kiểm tra: đặt tay cầm nằm ngang yên trên bàn, `az` phải xấp xỉ +1 g; chúc đầu tay cầm xuống, `ax` tiến về −1 g.

## Gói dữ liệu Mega → PC (29 byte, little-endian)

| Byte | Kiểu | Trường | Ý nghĩa |
| --- | --- | --- | --- |
| 0–1 | 2 × u8 | `A5 5A` | Mã đồng bộ |
| 2 | u8 | `seq` | Số thứ tự, tăng 1 mỗi gói, quay vòng 255 → 0 (phát hiện mất gói) |
| 3–6 | u32 | `t_ms` | `millis()` lúc đọc cảm biến |
| 7–12 | 3 × i16 | `ax ay az` | Gia tốc, thang ±2 g, **16384 LSB/g** |
| 13–18 | 3 × i16 | `gx gy gz` | Vận tốc góc đã trừ bias, thang ±500 °/s, **65,5 LSB/(°/s)** |
| 19–26 | 4 × u16 | `j1x j1y j2x j2y` | Joystick 0–1023 (joystick 1 = chỉnh tinh, joystick 2 = di chuyển) |
| 27 | u8 | `nut` | Bit nút, 1 = đang bấm (xem bảng dưới) |
| 28 | u8 | `crc` | CRC-8 (đa thức 0x07, giá trị đầu 0x00) tính trên byte 2–27 |

Bit của `nut`:

| Bit | Nút |
| --- | --- |
| 0 | Cò (cầm/thả) |
| 1 | Căn lại (recenter) |
| 2 | Hỏi AI (giữ để nói) |
| 3 | Nhấn joystick 1 |
| 4 | Nhấn joystick 2 |
| 6 | Tay cầm chỉ có 1 cần (ESP32-CAM + 1 mạch joystick): cần ở `j2x j2y`, nút ở bit 4; game dùng 2 chế độ ĐI / TAY ([09](09-tay-cam-esp32-cam.md)) |
| 7 | Lỗi đọc MPU6050 (dây I2C lỏng hoặc quá dài), hoặc tay cầm không có MPU6050 |

Ngoài gói nhị phân, firmware có thể gửi **dòng chữ bắt đầu bằng `#`** (thông báo khởi động, kết quả lệnh `I`). Bộ giải mã bỏ qua mọi byte nằm ngoài gói hợp lệ và gom các dòng `#` thành thông báo.

## Lệnh PC → Mega (dòng chữ ASCII, kết thúc bằng `\n`)

| Lệnh | Ví dụ | Tác dụng |
| --- | --- | --- |
| `R<mức>` | `R120` | Rung liên tục mức 0–255 (0 = tắt) |
| `P<mức>,<ms>` | `P255,300` | Xung rung mức 0–255 trong ms (tối đa 2000), xong trở về mức `R` |
| `L<xanh>,<đỏ>` | `L0,1` | Bật/tắt đèn xanh và đỏ (0 hoặc 1) |
| `C` | `C` | Hiệu chuẩn lại con quay: **để yên tay cầm khoảng 2 giây**; trong lúc này không có gói dữ liệu, xong thì trả về dòng `#CAL bias=...` |
| `H` | `H` | Nhịp tim: PC báo vẫn sống, không làm gì khác |
| `I` | `I` | Firmware trả về dòng `#INFO ...` |

**An toàn:** nếu quá 2 giây không nhận được byte nào từ PC (Unity treo, rút cáp), firmware tự tắt motor và đèn, rồi nháy đèn xanh chậm báo "chưa kết nối". Phía PC phải gửi `H` ít nhất mỗi 0,5 giây.

## Bản không dây (ESP32-CAM, Wi-Fi UDP)

Firmware `firmware/tay_cam_esp32cam/`; đấu nối và cách nạp ở [09-tay-cam-esp32-cam.md](09-tay-cam-esp32-cam.md). Gói 29 byte, bit nút và lệnh **giữ nguyên**; chỉ đổi đường truyền:

| | Bản dây (Mega) | Bản không dây (ESP32-CAM) |
| --- | --- | --- |
| Đường truyền | Cổng COM 115200 baud | UDP qua Wi-Fi |
| Tay cầm → PC | Luồng byte liên tục | **Mỗi gói UDP chứa đúng một gói 29 byte**, gửi tới cổng **4210** của máy tính |
| PC → tay cầm | Dòng chữ qua COM | Dòng chữ ASCII, có thể nhiều dòng trong một gói UDP, gửi tới cổng **4211** (đúng địa chỉ và cổng mà gói dữ liệu gửi đi) |
| Dòng `#...` | Xen giữa các gói | Gửi thành gói UDP riêng, chỉ dùng ký tự ASCII |
| Tìm nhau | Chọn cổng COM | Tay cầm **phát quảng bá** tới cổng 4210 cho tới khi nghe thấy máy tính, sau đó gửi thẳng tới máy tính đó |
| An toàn | 2 s không nghe PC: tắt rung, đèn | 2 s: tắt rung; **5 s: quên máy tính, quay lại phát quảng bá** (game tắt, đổi máy) |

Lệnh thêm cho bản không dây:

| Lệnh | Tác dụng |
| --- | --- |
| `J` | Lấy vị trí nghỉ hiện tại của hai joystick làm tâm (buông tay khỏi cần), trả về `#JOY tam=...` |

Bản không dây đọc joystick bằng ADS1115 rồi đổi về thang 0–1023 (tâm 512), nên phía PC không phải phân biệt hai loại tay cầm. Không có MPU6050 thì bit 7 (lỗi IMU) luôn bật: game bỏ qua hướng tay cầm, joystick và nút vẫn dùng bình thường. Mỗi gói UDP nặng 29 byte, 100 gói/s, tức khoảng 3 KB/s, quá nhỏ so với băng thông Wi-Fi. Độ trễ thường vài ms vì firmware tắt chế độ tiết kiệm điện Wi-Fi (`WiFi.setSleep(false)`).

## Cảm giác rung: lưu ý về motor

Motor DC gắn ốc lệch tâm cần khoảng 30–60 ms để quay đủ nhanh, nên xung ngắn hơn khoảng 40 ms gần như không cảm nhận được. Bảng mẫu rung gợi ý nằm trong [01-de-xuat-dieu-khien.md](01-de-xuat-dieu-khien.md#5-ngôn-ngữ-rung-và-đèn).
