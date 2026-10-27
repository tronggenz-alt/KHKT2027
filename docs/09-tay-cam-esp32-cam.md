# Tay cầm joystick → Arduino Mega → ESP32-CAM → Wi-Fi → game

Đường đi của tín hiệu:

```
Mạch joystick ──A0, A1, D2──▶ Arduino Mega ──D22 → IO13 (UART 9600)──▶ ESP32-CAM ──Wi-Fi UDP 4210──▶ Laptop (game Unity)
                              đọc analog 0–1023                          gửi dòng chữ "X,Y,B"
```

Mega đọc joystick, nên tránh được giới hạn của ESP32-CAM: các chân analog của ESP32-CAM thuộc ADC2, mà ADC2 không đọc được khi Wi-Fi bật. ESP32 chỉ nhận chữ từ Mega rồi phát lên Wi-Fi.

| Phần | Ở đâu |
| --- | --- |
| Code Mega | `mega_joystick_opendrain.ino` của bạn: đọc A0, A1, nút D2; gửi `X,Y,B\n` khoảng 25 lần/giây ra D22, 9600 baud, kiểu "open-drain" (không bao giờ xuất 5 V) |
| Code ESP32 | `esp32s3_udp.ino` của bạn (board **AI Thinker ESP32-CAM**): đọc UART ở IO13, phát mỗi dòng thành một gói UDP **broadcast** tới cổng **4210**; 1 giây không có dữ liệu từ Mega thì gửi `NO_MEGA_DATA` |
| Game nhận gói | `Assets/Scripts/Input/UdpControllerSource.cs` + `XybDecoder.cs`. Tự có trong cảnh: nếu cảnh chưa có thì game tự tạo khi chạy, không cần dựng lại cảnh |
| Cử động nhân vật | `Assets/Scripts/Input/PadInput.cs` (2 chế độ ĐI / TAY), `DesktopViewer.cs` (đi, quay, ngồi), `HandController.cs` (cầm, rót) |
| Xem gói / giả lập | `python tools/tay_cam_udp.py nghe` (tắt game trước) · `python tools/tay_cam_udp.py gia-lap --mega` |

> **Trạng thái (26/10/2026):**
> - **Laptop nhận được gói thật** từ ESP32 (192.168.0.106): khoảng 80–110 gói/giây; tâm cần khi để yên khoảng (387–418, 362–396), mỗi lần bật lệch một chút.
> - **Bài thử tự động trong Unity** gửi đúng dạng chữ `X,Y,B`. Mọi cử động ở mục 3 đều chạy đúng (số đo ở mục 6).
> - **Unity nhận được mạch thật** (27/10/2026), sau khi đổi luật tường lửa của Unity Editor sang Allow ở mạng Public (mục 4):
>   - 55–109 gói/giây;
>   - tự lấy tâm (362, 339);
>   - để yên cần 10 giây thì nhân vật không trôi, không quay.
> - Chưa thử: đẩy cần thật để đi / quay, vì cần người cầm cần.

## 1. Bảng đấu nối

| Từ | Tới | Ghi chú |
| --- | --- | --- |
| Joystick **VRx** | Mega **A0** | Trục trái / phải |
| Joystick **VRy** | Mega **A1** | Trục lên / xuống |
| Joystick **SW** | Mega **D2** | Nút nhấn cần; Mega bật điện trở kéo lên bên trong, nhấn là nối GND |
| Joystick **+5V** | Mega **5V** (hoặc 3.3V) | Cấp 3,3 V thì giá trị chỉ chạy tới khoảng 675 thay vì 1023; game tự học biên độ nên vẫn đẩy hết cỡ được |
| Joystick **GND** | Mega **GND** | |
| Mega **D22** | ESP32-CAM **IO13** | Nối thẳng, **không cần điện trở**: Mega chỉ kéo xuống GND hoặc thả nổi, ESP32 tự kéo lên 3,3 V |
| Mega **GND** | ESP32-CAM **GND** | **Bắt buộc nối chung GND**, thiếu là ESP32 báo `NO_MEGA_DATA` |
| Mega USB | Laptop | Lấy nguồn; cổng Serial in `X=.. Y=.. B=..` để xem bằng Serial Monitor |
| ESP32-CAM (trên đế ESP32-CAM-MB) USB | Laptop | Lấy nguồn; nạp code; xem IP và số gói/giây bằng Serial Monitor 115200 |

Trên ESP32-CAM **không dùng** IO0, IO12, IO2, IO16. IO13 là chân an toàn.

## 2. Cài đặt (một lần)

1. **Nạp code ESP32:** điền `WIFI_SSID`, `WIFI_PASS` trong `esp32s3_udp.ino`, chọn board **AI Thinker ESP32-CAM**, nạp. ESP32 chỉ vào được Wi-Fi **2,4 GHz**, và laptop phải ở **cùng Wi-Fi đó**.
2. **Nạp code Mega:** nạp `mega_joystick_opendrain.ino` (board Arduino Mega 2560).
3. **Kiểm tra laptop nhận được gói:** tắt game rồi chạy
   ```bash
   python tools/tay_cam_udp.py nghe
   ```
   Phải thấy khoảng 80–110 gói/giây, X, Y đổi khi đẩy cần, `nut=NHAN` khi nhấn cần.
4. **Mở tường lửa cho Unity** (mục 4).
5. **Mở Unity, bấm Play**, để yên cần khoảng nửa giây. Góc trên màn hình báo "Đã nối tay cầm (192.168.x.x:4210)".

## 3. Cử động nhân vật bằng 1 cần + 1 nút

Mạch joystick chỉ có 2 trục và 1 nút (nhấn cần), nên game chia 2 chế độ. Thanh gợi ý dưới màn hình luôn ghi đang ở chế độ nào.

**Chế độ ĐI** (lúc mới nối):

| Thao tác | Nhân vật |
| --- | --- |
| Đẩy cần **lên** | Tiến tới (đẩy nhẹ đi chậm, đẩy mạnh đi nhanh) |
| Kéo cần **xuống** | Lùi |
| Đẩy cần **sang phải / trái** | Quay mặt sang phải / trái (giữ hết cỡ thì quay nhanh dần) |
| Đẩy **chéo** | Vừa đi vừa rẽ |
| Đẩy lên **hết cỡ quá 1 giây** | Chạy nhanh |
| **Giữ nút** + đẩy cần phải / trái | Bước ngang sang phải / trái (mặt không quay) |
| **Giữ nút** + đẩy cần lên / xuống | Ngẩng lên / cúi xuống nhìn |
| **Nhấn nút 1 lần** | Sang chế độ TAY |
| **Nhấn nút 2 lần** liên tiếp | Ngồi / đứng |
| **Giữ nút 1 giây** (không đẩy cần) | Dịch chuyển sang khu kế tiếp (bàn chuẩn độ, bàn định tính, phòng hóa chất...) |

**Chế độ TAY** (thao tác dụng cụ):

| Thao tác | Nhân vật |
| --- | --- |
| Đẩy cần | Ngắm bằng dấu chấm giữa màn hình: xoay, ngẩng / cúi (chậm hơn lúc đi cho dễ trúng). Trỏ vào khóa buret thì ↑ mở, ↓ đóng |
| **Nhấn nút** khi trỏ vào dụng cụ | Cầm lên / đặt xuống / dùng máy |
| **Giữ nút** khi trỏ vào vật chứa | Rót, hút pipet, xả, lắc bình nón |
| **Nhấn nút** khi trỏ vào chỗ trống | Về chế độ ĐI (dụng cụ đang cầm vẫn giữ trên tay) |

Bàn phím vẫn dùng được song song: Q trả dụng cụ về chỗ, Enter ghi số đọc buret, H bảng trợ giúp. Bảng trợ giúp có dòng trạng thái tay cầm: địa chỉ ESP32, số gói/giây, tâm cần, chế độ.

**Chiều cần ngược?** Ví dụ đẩy lên mà nhân vật lùi, hoặc đẩy phải mà quay trái.
1. Trong Unity, chọn đối tượng có component **UdpControllerSource**: `Hệ thống demo` khi đã dựng lại cảnh, hoặc `Tay cầm Wi-Fi (tự tạo)` khi game tự tạo.
2. Tích **Invert Joy2 Y** hoặc **Invert Joy2 X**.
3. Nếu mạch gắn xoay ngang (đẩy lên mà nhân vật quay) thì tích **Swap Joy2 Axes**.

**Tâm cần:** game lấy tâm theo các gói đầu tiên sau khi nối, nên đừng chạm cần lúc bấm Play. Biên độ thì game tự học: lần đầu đẩy hết cỡ, từ đó đẩy hết cỡ là tốc độ tối đa.

## 4. Mở tường lửa cho Unity (bắt buộc)

Windows đang có luật **chặn** gói đến của "Unity 6000.6.7f1 Editor" khi Wi-Fi là mạng **Công cộng (Public)**. Wi-Fi của laptop hiện đang là Public. Python nhận được gói nhưng Unity thì không.

Đây là cài đặt bảo mật của máy nên bạn tự làm. Chọn **một** trong hai cách:

- **Cách 1, cho phép Unity qua tường lửa:**
  1. Mở **Windows Security**.
  2. Vào **Firewall & network protection**, chọn **Allow an app through firewall**.
  3. Bấm **Change settings**.
  4. Tìm **Unity 6000.6.7f1 Editor** (có thể có 2 dòng), tích cả cột **Private** và **Public**, rồi bấm OK.
- **Cách 2, đổi Wi-Fi sang mạng Riêng tư:**
  1. Vào **Settings > Network & internet > Wi-Fi**, chọn tên Wi-Fi đang dùng.
  2. Ở **Network profile type**, chọn **Private network**.
  3. Làm thêm cách 1 cho cột Private nếu Unity vẫn chưa nhận được gói.

Khi xuất bản `.exe`, lần đầu chạy Windows sẽ hỏi: tích cả **Private** và **Public**, rồi bấm **Allow**.

## 5. Gỡ lỗi

| Hiện tượng | Nguyên nhân | Cách xử lý |
| --- | --- | --- |
| `tay_cam_udp.py nghe` thấy gói, nhưng game báo "đang chờ trên cổng UDP 4210" | Tường lửa chặn Unity | Mục 4 |
| Bảng trợ giúp báo "ESP32 không nhận được dữ liệu từ Mega" | Lỏng dây D22 → IO13, thiếu GND chung, Mega chưa chạy | Kiểm tra dây; mở Serial Monitor của Mega xem có in `X=.. Y=..` không |
| Không thấy gói nào | Khác Wi-Fi; Wi-Fi 5 GHz; ESP32 chưa vào được mạng | Mở Serial Monitor ESP32 (115200) xem dòng `DA KET NOI. IP ESP32: ...`; laptop và ESP32 cùng Wi-Fi 2,4 GHz |
| Mạng chặn gói broadcast (Wi-Fi trường, điểm phát có cách ly thiết bị) | Gói `255.255.255.255` không tới laptop | Trong code ESP32 đổi `TARGET_IP` thành IP của laptop (xem bằng lệnh `ipconfig`) |
| Bảng trợ giúp báo "không mở được cổng UDP 4210" | `tay_cam_udp.py nghe` hoặc một cửa sổ game khác đang mở cổng | Tắt chương trình kia |
| Nhân vật tự trôi khi để yên cần | Đang chạm cần lúc bấm Play nên lấy tâm sai | Dừng Play, buông cần, bấm Play lại |
| Đẩy hết cỡ mà đi chậm | Game chưa học biên độ | Đẩy cần hết cỡ một lần theo mỗi hướng |
| Đi / quay ngược chiều | Cách gắn mạch | Tích Invert / Swap ở mục 3 |

## 6. Kiểm thử

**Không cần mạch:** mở game, rồi chạy lệnh dưới. Lệnh này gửi dòng `X,Y,B` giống ESP32 thật; điều khiển bằng W A S D, Space là nhấn nút, B là bật / tắt giữ nút.
```bash
python tools/tay_cam_udp.py gia-lap --mega
```

**Kiểm thử tự động trong Unity** gửi dòng `X,Y,B` qua UDP. Kết quả đo được:

| Thao tác | Kết quả |
| --- | --- |
| Lùi 1 s | 1,41 m |
| Tiến 0,6 s | 0,85 m |
| Quay phải / trái 0,5 s | +46° / −61° |
| Đẩy chéo 0,5 s | Đi 0,66 m, quay 71° |
| Lên hết cỡ 1,4 s | Chạy nhanh bật |
| Giữ nút + phải 0,8 s | Bước ngang 1,12 m, mặt không quay |
| Giữ nút + lên 0,6 s | Ngẩng 32° |
| Nhấn 2 lần | Đứng → ngồi |
| Nhấn 1 lần | Sang TAY; nhấn tiếp cầm được bình nón |
| Nhấn vào chỗ trống | Về ĐI, vẫn cầm bình |
| Giữ nút 1,3 s | Sang khu kế tiếp |
| Gói `NO_MEGA_DATA` | Hiện lời nhắc kiểm tra dây |

**Unity với mạch thật** (bài nghe 10 giây ở cổng 4210): đã nối ESP32 192.168.0.106, nhận 55–109 gói/giây, tâm (362, 339), không trôi khi để yên cần.

Sau này gắn thêm cảm biến (MPU6050...) thì chuyển sang gói nhị phân 29 byte ([docs/02](02-giao-thuc-tay-cam.md)). Game đã đọc được cả hai dạng gói.
