# Đề xuất hệ điều khiển cho môi trường thực tế ảo

Tài liệu nghiên cứu cho phần điều khiển của dự án *Phòng thí nghiệm ảo nhiều người*. Viết ngày 22/10/2026, dựa trên bản thiết kế cùng ngày. Mã nguồn đi kèm: `firmware/`, `Phong thi nghiem ao/Assets/Scripts/Input` và `Interaction`, `khkt/`.

## Tóm tắt

1. **Kính và tay cầm đều chỉ đo được HƯỚNG (3DoF), không đo được VỊ TRÍ.** Vài ý trong bản thiết kế ngầm cần vị trí: cúi người nhìn ngang vạch, với tay cầm dụng cụ, bàn tay "bóng ma". Cần thiết kế lại các thao tác này cho hợp 3DoF (mục 1, 3).
2. **Dùng đúng thứ IMU đo tốt.** Góc nghiêng (bám theo trọng lực) và vận tốc góc của MPU6050 rất ổn định: dùng cho các thao tác cần khéo (rót, vặn khóa buret, lắc bình). Góc quay ngang (yaw) bị trôi: chỉ dùng cho việc thô như chỉ tia chọn vật, kèm nút căn lại.
3. **Thiết kế "ngồi tại trạm thí nghiệm".** Học sinh ngồi, mọi dụng cụ trong tầm tay ảo; đổi trạm bằng dịch chuyển tức thời và xoay từng nấc 45°. Hợp với dây cáp, 3DoF, và giảm say.
4. **Động tác giống thật** là lý do dùng VR thay vì laptop: nghiêng để rót (có mô hình vật lý), **vặn cổ tay để mở khóa buret**, **lắc tròn để trộn**, kéo joystick để cúi đọc vạch. Ba động tác sau chưa có trong bản thiết kế.
5. **Rủi ro lớn nhất là độ trễ hình của PhoneVR.** Ghi chú phát hành PhoneVR v1.0 nêu độ trễ trung bình khoảng 150 ms, trong khi ngưỡng thường được nhắc để tránh say là khoảng 20 ms. Phải đo ngay tuần 1 và chuẩn bị phương án B: chạy ứng dụng thẳng trên điện thoại (mục 8).

## 1. Vấn đề phát hiện trong bản thiết kế hiện tại

| # | Vấn đề | Vì sao quan trọng | Đề xuất |
| --- | --- | --- | --- |
| 1 | Kính VR Box + PhoneVR chỉ gửi hướng đầu (3DoF) | "Học sinh phải tự cúi nhìn ngang vạch mặt khum" không làm được bằng cơ thể: cúi người thật thì camera ảo không hạ xuống | Mô hình cổ + cúi bằng joystick + kính lúp hiện số đọc có thị sai (mục 4.6). Nâng cấp: webcam theo dõi marker trên kính (mục 7) |
| 2 | Tay cầm chỉ có IMU, không biết vị trí tay | Không "với tay" chạm vật được; bàn tay bóng ma chỉ tái hiện được hướng | Tia chỉ + mô hình cánh tay (arm model) + chỗ đặt có sẵn trên bàn. Chỉ số khéo tay dùng góc và vận tốc góc, không dùng quỹ đạo tay |
| 3 | MPU6050 không có la bàn | Yaw trôi dần, tia chỉ lệch khỏi chỗ học sinh định chỉ | Nút căn lại; mọi thao tác chính xác dùng pitch/roll/vận tốc góc, không dùng yaw |
| 4 | Độ trễ stream PhoneVR khoảng 150 ms (theo ghi chú phát hành v1.0) | Quay đầu thấy hình chạy theo sau: nguyên nhân chính gây say | Đo tuần 1; phương án B chạy native Android (mục 8) |
| 5 | Bản thiết kế ghi "chế độ gamepad USB" cho Arduino Mega | Mega 2560 không tự làm thiết bị USB HID được; phải nạp lại firmware chip 16U2 (kiểu UnoJoy/HoodLoader2), rủi ro và mất cổng nạp | Dùng cổng COM và script Unity đọc trực tiếp (đã viết). Đằng nào cũng cần kênh hai chiều để gửi lệnh rung và đèn |
| 6 | MPU6050 ở tay cầm, Mega ở hộp thắt lưng: I2C đi qua cáp ~1 m | I2C vốn chỉ dành cho dây ngắn trên mạch; dây dài dễ treo bus | Firmware hạ I2C xuống 100 kHz, có timeout tự khởi động lại cảm biến, báo lỗi qua bit 7. Đi dây theo cặp xoắn của cáp LAN: SDA xoắn với GND, SCL xoắn với GND. Nâng cấp: vi điều khiển đặt ngay trong tay cầm (mục 7) |
| 7 | SteamVR chỉ nhận một kính trên một máy | Mỗi học sinh đeo kính cần một laptop có card rời riêng | Ghi rõ trong dự trù; phương án B chỉ cần một laptop làm máy chủ |
| 8 | Chưa thống nhất | Mục kiến trúc nói "hai tay cầm", phần cứng chỉ có một; tuần 1 "kiểm tra tay cầm Bluetooth" nhưng đã chọn gói chỉ có kính; ngân sách ghi lúc 250k, lúc 400k, lúc 500k | Chốt: một tay cầm tay phải; sửa kế hoạch tuần 1 và các con số ngân sách |

Về mục 8: chuẩn độ thật dùng **hai tay** (tay trái vặn khóa, tay phải lắc bình). Với một tay cầm, điều này lại ủng hộ chế độ **chia vai** của bản thiết kế: một bạn giữ khóa buret, một bạn lắc bình. Khi chơi một mình, bạn AI nhận vai còn lại.

## 2. Nguyên tắc thiết kế

1. **Ngồi tại trạm.** Mỗi trạm (bàn chuẩn độ, tủ hóa chất, khu an toàn có vòi rửa mắt và bình chữa cháy) được bố trí để mọi thứ nằm trong vòng tay ảo khi ngồi. Đi lại là ngoại lệ, không phải cách tương tác chính.
2. **Thao tác chính xác dùng đại lượng đo tốt.** Góc nghiêng so với phương thẳng đứng không trôi vì gia tốc kế luôn biết trọng lực; vận tốc góc của con quay đo trực tiếp, nhiễu thấp, 100 mẫu/giây.
3. **Động tác giống thật**, vì đây là điều laptop không làm được và là giả thuyết mà thực nghiệm ba nhóm kiểm chứng.
4. **Một tín hiệu, một nghĩa** cho rung và đèn (mục 5).
5. **Luôn có đường lùi.** Mọi thao tác làm được bằng chuột-bàn phím (`KeyboardMouseControllerSource`): để lập trình khi chưa có phần cứng, cho người không có kính, và cho nhóm laptop trong thực nghiệm.

## 3. Sơ đồ điều khiển đề xuất

Một tay cầm tay phải: cò, nút căn lại, nút hỏi AI, joystick 1 (chỉnh tinh), joystick 2 (di chuyển).

| Thao tác | Cách làm | Đại lượng dùng | Script |
| --- | --- | --- | --- |
| Chỉ, chọn vật | Tia phát ra từ bàn tay ảo | Hướng (cả yaw) | `ControllerPoseDriver` + XRI Ray Interactor |
| Cầm / thả | Bóp cò: vật về tay. Thả cò: vật về chỗ đặt gần nhất trên bàn | Nút | XRI Grab + Socket |
| Rót từ cốc | Cầm cốc, nghiêng cổ tay | Góc nghiêng | `PourableContainer` |
| Mở khóa buret | Chỉ vào khóa, giữ cò, **vặn cổ tay**; joystick 1 lên/xuống để chỉnh rất tinh. Thả cò thì khóa giữ nguyên độ mở | Góc xoay quanh trục tay cầm | `BuretStopcock` |
| Lắc bình nón | Cầm bình, **lắc tròn** tay cầm | Vận tốc góc | `SwirlDetector` → `TitrationFlask` |
| Cúi đọc vạch | Kéo joystick 1 xuống (khi không vặn khóa); cúi đầu thì mắt cũng hạ theo mô hình cổ | Joystick + hướng đầu | `EyeHeightController`, `MeniscusReading` |
| Đổi trạm | Đẩy joystick 2 tới: hiện vòng cung, thả ra để dịch chuyển | Joystick | XRI Teleportation |
| Quay người | Gạt joystick 2 trái/phải: xoay 45° | Joystick | XRI Snap Turn |
| Căn lại | Nhìn thẳng, chĩa tay cầm về trước, bấm nút căn lại | Hướng | `ControllerPoseDriver.Recenter` |
| Hỏi AI | Chỉ tia vào vật, giữ nút hỏi AI và nói | Nút + vật đang chỉ | `server/ai` |

Người thuận tay trái: bỏ tích `rightHand` trong `ControllerPoseDriver`.

## 4. Chi tiết từng cơ chế

### 4.1. Tính hướng tay cầm

Firmware gửi gia tốc và vận tốc góc thô 100 lần/giây; PC chạy **bộ lọc Mahony**. Bộ lọc tích phân con quay (nhanh, mượt nhưng trôi) rồi kéo dần về hướng trọng lực đo bằng gia tốc kế. Gia tốc kế chỉ được tin khi độ lớn gần 1 g, tức là khi tay không giật mạnh.

- Pitch và roll: không trôi, sai số cỡ vài phần mười độ khi cầm yên. Đã kiểm thử: đặt nghiêng 30°, bộ lọc hội tụ về 30 ± 0,5°.
- Yaw: không có mốc. Tài liệu cộng đồng xác nhận yaw của MPU6050 chỉ là tương đối so với lúc khởi động và trôi dần; tốc độ trôi phụ thuộc từng con chip và nhiệt độ, nên **phải tự đo**: `python tools/tay_cam_monitor.py --port COM5 --do-troi 300`.
- Bias con quay được đo lúc khởi động (để yên tay cầm 2 giây). Nếu tay cầm bị lắc trong lúc đo, firmware đo lại tối đa 3 lần.

Vì sao không dùng bộ xử lý DMP trong MPU6050: DMP cho sẵn quaternion nhưng cần thư viện lớn và firmware nhị phân của hãng. Gửi dữ liệu thô vừa đơn giản hơn, vừa giữ được dữ liệu cần để đo run tay.

### 4.2. Mô hình cánh tay (arm model)

Tay cầm 3DoF của Google Daydream giải bài toán tương tự bằng mô hình cánh tay: vị trí tay được suy ra từ hướng, qua các khớp ảo vai, khuỷu, cổ tay. Bản đơn giản trong `ControllerPoseDriver`:

```
khuỷu = vị trí đầu + xoay_thân × (0,2; -0,45; 0,15) m   (+ nâng nhẹ khi chĩa lên)
tay   = khuỷu + hướng_tay_cầm × (0; 0; 0,3) m
```

Thân xoay theo đầu một cách chậm. Kết quả: chĩa tay cầm sang trái thì bàn tay ảo đi sang trái, đủ tự nhiên cho tia chỉ và cho dụng cụ đang cầm. Không dùng được để "chạm" chính xác một điểm trong không gian, nên mọi tương tác chọn vật đều qua tia.

### 4.3. Rót theo góc nghiêng

Mô hình vật lý đơn giản cho cốc hình trụ bán kính r, cao h, mực chất lỏng H:

```
bắt đầu chảy khi  tan θ ≥ (h − H) / r
lưu lượng         Q = k · e^1,5    với e = H + r·tanθ − h   (công thức đập tràn)
```

Hệ quả giống thật: **cốc càng vơi càng phải nghiêng nhiều**, và tốc độ chảy tăng rất nhanh khi nghiêng quá tay. Với cốc 100 ml chứa 50 ml (`khkt/pour.py`): bắt đầu chảy ở khoảng 61°, nghiêng thêm 1° chảy khoảng 3 ml/s, thêm 5° khoảng 40 ml/s. Độ nhạy này cao, vì vậy:

- Phải **hiệu chỉnh k** bằng cách quay video rót cốc thật và đo thời gian rót hết (docs/04).
- Có vùng chết 0,5 mm: phần vượt miệng cốc nhỏ hơn thế coi như bị sức căng bề mặt giữ lại, để rung tay nhẹ không làm nhỏ giọt liên tục.
- Rót ra ngoài bình nhận thì tính là **đổ ra bàn** (sự kiện `Spilled`), nối vào hệ thống an toàn.

### 4.4. Khóa buret: vặn cổ tay

Ở phòng lab thật, khóa buret được vặn bằng các ngón tay. Động tác gần nhất với tay cầm là **xoay cổ tay quanh trục tay cầm**: góc này đo bằng cách tách phần xoay quanh trục dọc (phân tách swing-twist) của hướng hiện tại so với lúc bắt đầu bóp cò, không phụ thuộc yaw nên không bị trôi.

- Độ mở = góc vặn / 90°, kẹp trong 0..1. Lưu lượng = 0,6 ml/s × độ mở². Bình phương làm cho đoạn gần đóng rất "chậm", dễ nhỏ từng giọt.
- Joystick 1 cộng thêm từ từ, dành cho chỉnh rất tinh và cho người khó xoay cổ tay.
- Mỗi giọt 0,05 ml, có sai số ngẫu nhiên ±0,004 ml (độ lệch chuẩn, có hạt giống cố định để lặp lại được). Dưới khoảng 4 giọt/s thì **mỗi giọt rung một nhịp**: học sinh "đếm giọt bằng tay".
- Thả cò thì khóa **giữ nguyên** độ mở như khóa thật: quên khóa là một lỗi được ghi nhận.

### 4.5. Lắc tròn bình nón

Lắc tròn làm vận tốc góc quanh hai trục ngang dao động đều. `SwirlDetector` lấy độ lệch chuẩn của chúng trong 0,75 s: dưới 30 °/s coi như không lắc, từ 150 °/s là lắc mạnh nhất.

NaOH nhỏ vào nằm trong vùng "chưa trộn" và chỉ hòa vào cả bình khi lắc (hoặc tự khuếch tán chậm). Vì vậy gần điểm tương đương sẽ thấy **vệt hồng chỗ giọt rơi rồi tan khi lắc**, đúng như thật; không lắc thì dễ dừng sai điểm cuối. Đây là một lỗi thao tác thật mà bản laptop khó tái hiện.

### 4.6. Đọc vạch và sai số thị sai

Vạch in ở thành ngoài ống (bán kính r ≈ 7 mm), mặt khum ở giữa. Mắt cao hơn mặt khum Δy, cách trục buret D thì tia nhìn cắt thành ống cao hơn Δy·r/D:

```
sai số (ml) = − Δy · r / D × (ml trên mỗi mét thang chia)
```

Ví dụ: mắt cao hơn 10 cm, cách 30 cm thì đọc nhỏ hơn khoảng 0,22 ml, cùng cỡ sai số thị sai thường gặp.

Màn hình điện thoại trong kính không đủ nét để thấy vạch 0,1 ml, nên khi nhìn vào buret sẽ hiện **kính lúp** với số đọc đã tính thị sai theo vị trí mắt ảo (`MeniscusReading`). Học sinh vẫn phải tự hạ tầm mắt cho ngang vạch: chế độ luyện tập có gợi ý "mắt cao hơn vạch 6 cm: cúi xuống", chế độ kiểm tra thì không.

Hạ tầm mắt bằng joystick là di chuyển camera nhân tạo, nên làm **chậm** (0,2 m/s), **chỉ theo phương thẳng đứng**, tối đa 35 cm.

### 4.7. Di chuyển và chống say

- Mặc định: **dịch chuyển tức thời + xoay từng nấc 45°**, tắt đi liên tục. Hướng dẫn thiết kế của Meta và tài liệu XR Interaction Toolkit đều xếp các cách này vào nhóm dễ chịu nhất; một nghiên cứu năm 2025 (90 người) ghi nhận kỹ thuật rời rạc như dịch chuyển và xoay nấc giảm say so với di chuyển liên tục, và xoay ảo gây khó chịu nhiều hơn tịnh tiến.
- Đi liên tục bằng joystick chỉ là tùy chọn, có thu hẹp khung nhìn (vignette) khi đang đi.
- Mỗi lượt đeo kính 10–15 phút như bản thiết kế. Đo mức say bằng bảng hỏi SSQ (Simulator Sickness Questionnaire) trước và sau mỗi lượt.

## 5. Ngôn ngữ rung và đèn

Motor DC gắn ốc lệch tâm cần khoảng 30–60 ms mới quay đủ nhanh, nên không có xung nào ngắn hơn 45 ms. Bảng dưới khớp với `HapticFeedback.cs`.

| Ý nghĩa | Rung | Đèn |
| --- | --- | --- |
| Cầm được vật | 1 nhịp nhẹ 60 ms | – |
| Một giọt rơi (khi nhỏ từng giọt) | 1 nhịp 45 ms | – |
| Sắp tới điểm tương đương (chỉ chế độ luyện tập) | 2 nhịp nhẹ | Xanh |
| Thao tác sai (quên tráng, rót tràn, quên khóa) | Mạnh 0,6 s | Đỏ |
| Nguy hiểm (cháy, bắn hóa chất) | 3 nhịp mạnh ngắt quãng | Đỏ |
| Chạm vật nóng | Liên tục, mạnh theo nhiệt độ | – |
| Mất kết nối với PC | Tắt | Xanh nháy chậm (firmware tự làm) |

Gợi ý "sắp tới điểm tương đương" làm lộ đáp án, nên **tắt ở chế độ kiểm tra và trong thực nghiệm đo kỹ năng**.

## 6. Đo độ khéo tay: IMU đo được gì

| Chỉ số | Cách tính (`khkt/skill.py`) | Ghi chú |
| --- | --- | --- |
| Độ run tay | RMS vận tốc góc trong dải 4–12 Hz, trung vị qua các cửa sổ 2 s | Run sinh lý thường được mô tả ở khoảng 8–12 Hz (có tài liệu ghi 5–12 hoặc 6–12 Hz). Khối lượng thiết bị làm thay đổi phổ run, nên **chỉ so các lần đo trên cùng một tay cầm** |
| Tốc độ 2 ml cuối | ml/s trong 2 ml trước điểm tương đương | Từ thể tích buret |
| Có giảm tốc không | Tốc độ 2 ml cuối / tốc độ trước đó | Nhỏ hơn 1 là có giảm tốc |
| Số giọt vượt | (V cuối − V tương đương) / 0,05 ml | Âm là dừng sớm |
| Độ giống thao tác chuẩn | DTW giữa đường "độ mở khóa theo thể tích đã chảy" của học sinh và giáo viên | So theo **thể tích** thay vì theo thời gian: người nhanh người chậm vẫn so được "ở 2 ml cuối mở khóa bao nhiêu" |

**Bóng ma giáo viên** làm được dù không có vị trí tay: phát lại hướng tay cầm đã ghi của giáo viên qua cùng mô hình cánh tay, bàn tay mờ sẽ chuyển động hợp lý cạnh tay học sinh. Nên đồng bộ bóng ma theo thể tích đã chảy (giáo viên ở thể tích nào thì hiện tư thế lúc đó), không theo đồng hồ.

## 7. Lộ trình nâng cấp theo tầng

| Tầng | Thêm gì | Được gì | Rủi ro | Trạng thái |
| --- | --- | --- | --- | --- |
| 0 | IMU 3DoF + dây (bản thiết kế hiện tại) | Đủ cho toàn bộ sơ đồ mục 3 | Trôi yaw, độ trễ stream | Đã viết code |
| 1 | Webcam laptop + 2 marker ArUco in giấy (trên kính, trên đầu tay cầm) | **Vị trí thật** của đầu và tay: cúi người thật để đọc vạch, đo được quỹ đạo tay | Mất dấu khi marker quay lệch quá nhiều hoặc nhòe do chuyển động nhanh; cần đủ sáng | Bản thử `tools/webcam_tracker.py`: trên ảnh giả lập, marker 5 cm ở 60 cm cho sai số khoảng 5 mm. **Chưa thử với webcam thật** |
| 2 | Vi điều khiển có Bluetooth/wifi (ví dụ ESP32) đặt ngay trong tay cầm, pin sạc | Không dây; hết cảnh I2C qua cáp dài | Thêm chi phí (cần hỏi giá), độ trễ không dây | Giao thức giữ nguyên, chỉ thêm một lớp nguồn mới kế thừa `ControllerSource` |
| 3 | IMU có la bàn và bộ hợp nhất cảm biến trên chip (ví dụ dòng BNO) | Bớt trôi yaw | Giá cao hơn nhiều MPU6050, la bàn nhiễu gần motor và sắt thép | Chỉ làm nếu tầng 0 trôi quá mức chấp nhận |

Đã cân nhắc và **không chọn** làm phương án chính: theo dõi bàn tay không cầm gì bằng webcam (kiểu MediaPipe). Học sinh mất cảm giác cầm nắm và rung, kém chính xác hơn cho thao tác nhỏ giọt, trái với luận điểm "luyện tay".

Ghi chú cho tầng 1: hướng xoay vẫn lấy từ IMU (nhanh, mượt), webcam chỉ bổ sung vị trí. Vị trí được lọc bằng bộ lọc **One Euro** (đứng yên thì lọc mạnh cho hết rung, di chuyển nhanh thì lọc nhẹ cho ít trễ). Đã viết sẵn phía Unity: `UdpPoseReceiver`, và hai script `ControllerPoseDriver`, `EyeHeightController` tự dùng vị trí webcam khi có.

## 8. Hai phương án hiển thị

| | A. Stream từ PC (bản thiết kế) | B. Chạy thẳng trên điện thoại |
| --- | --- | --- |
| Cách làm | Unity trên laptop → SteamVR → PhoneVR/ALVR → điện thoại | Build Android bằng Google Cardboard XR Plugin cho Unity; laptop chỉ làm máy chủ đồng bộ và AI |
| Độ trễ khi quay đầu | Phụ thuộc mã hóa, mạng, giải mã; PhoneVR v1.0 nêu khoảng 150 ms | Hướng đầu tính ngay trên điện thoại |
| Nhiều người đeo kính | Mỗi người một laptop card rời | Một laptop cho cả nhóm |
| Đồ họa | Mạnh (card rời) | Yếu hơn; phòng lab ít đa giác vẫn ổn |
| Nối tay cầm | Cổng COM vào laptop (đã làm) | Mega → điện thoại qua cáp OTG serial, hoặc Bluetooth (HC-05 hay ESP32) |
| Độ ổn định phần mềm | PhoneVR còn là bản beta, chỉ hỗ trợ ALVR v20.8.0 | Cardboard SDK ổn định, nhưng chỉ hỗ trợ sẵn một nút bấm; tay cầm phải tự nối |

**Đề xuất:** giữ A làm hướng chính như bản thiết kế, nhưng tuần 1 dựng cùng một cảnh đơn giản cho cả A và B, đo độ trễ và cho 3–5 bạn thử (SSQ). Code đã tách lớp `ControllerSource` nên phần tương tác dùng chung được cho cả hai. Nếu A gây say rõ rệt hơn B thì chuyển sang B, và đây cũng là một kết quả nghiên cứu đáng đưa vào báo cáo.

## 9. Kiểm chứng hệ điều khiển (đưa vào báo cáo KHKT)

Chi tiết trong [04-kiem-thu-tuan-1.md](04-kiem-thu-tuan-1.md).

- Độ trễ hình khi quay đầu (quay video chậm cả đầu và màn hình) cho phương án A và B.
- Độ trôi yaw (°/phút) và tỉ lệ mất gói, lỗi CRC qua cáp thật.
- Độ chính xác rót: yêu cầu rót đúng 10 ml, đo sai số trên 10 lần, so người mới với người đã luyện.
- Mức say (SSQ) và độ dễ dùng (thang SUS) sau mỗi lượt.
- Đối chiếu mô hình rót và đường cong chuẩn độ với số liệu đo thật.

## 10. Việc nhóm cần quyết định

1. Chốt một tay cầm (tay phải) và sửa các chỗ "hai tay cầm", "tay cầm Bluetooth" trong bản thiết kế?
2. Tuần 1 có dựng song song phương án B để so sánh độ trễ không?
3. Có thử tầng 1 (webcam + marker) ở vòng trường, hay để dành vòng tỉnh?
4. Thao tác chuẩn của giáo viên ghi lúc nào để làm bóng ma và mốc so sánh?

## Nguồn tham khảo

- PhoneVR: [README](https://github.com/ShootingKing-AM/PhoneVR/blob/master/README.md) (chỉ hỗ trợ ALVR v20.8.0), [ghi chú phát hành](https://github.com/PhoneVR-Developers/PhoneVR/releases) (v1.0.0-beta: độ trễ trung bình khoảng 150 ms; bản mới nhất v2.0.0-beta)
- ALVR: [bảng tương thích](https://github.com/alvr-org/ALVR)
- VRidge: [FAQ của RiftCat](https://riftcat.com/faq)
- Google Cardboard cho Unity: [hướng dẫn nhanh](https://developers.google.com/cardboard/develop/unity/quickstart?hl=vi)
- Mô hình cánh tay: [Google VR – Arm models](https://developers.google.com/vr/elements/arm-model)
- Yaw MPU6050 không có mốc tuyệt đối: [diễn đàn Arduino – DMP of MPU6050](https://forum.arduino.cc/t/dmp-of-mpu6050-doubts/1251963)
- Đọc cổng COM trong Unity: [Ardity](https://github.com/dwilches/Ardity) (cần API Compatibility .NET Framework, đọc trên luồng riêng)
- Chống say: [Meta – Locomotion comfort](https://developers.meta.com/horizon/design/locomotion-comfort-usability/), [Unity Learn – Reducing motion sickness with XRI](https://learn.unity.com/tutorial/reducing-motion-sickness-with-xri-toolkit), [arXiv 2502.15227](https://www.arxiv.org/pdf/2502.15227)
- Ngưỡng độ trễ khoảng 20 ms (quy tắc kinh nghiệm của ngành, không phải ngưỡng cứng): [Unity glossary – motion-to-photon latency](https://unity.com/glossary/motion-to-photon-latency)
- Run tay sinh lý và ảnh hưởng của khối lượng cảm biến: [Scientific Reports 2022, PMC9547012](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC9547012/)
- Theo dõi marker ArUco bằng một webcam và giới hạn của nó: [poster UW CSE 493V](https://courses.cs.washington.edu/courses/cse493v/23sp/public/poster_05.pdf), [Devpost cv-fps](https://devpost.com/software/cv-fps)
