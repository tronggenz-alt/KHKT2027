# 28. Điều khiển phòng lab bằng thao tác bàn tay

Cập nhật: 10/11/2026.

## 1. Thao tác

| Thao tác | Nhãn | Việc làm |
|---|---|---|
| Nắm tay, lòng bàn tay úp hoặc ngửa (nắm ngang) | `nam_ngang` | Mở **danh sách dụng cụ** trên bàn: cốc, bình nón, pipet, buret, bình tia... |
| Nắm tay, lòng bàn tay quay ngang sang bên (nắm dọc) | `nam_doc` | Mở **danh sách hóa chất** trên bàn: lọ, chai hóa chất, lọ chỉ thị, bình khí |
| Chỉ giơ ngón trỏ | `tro` | Trượt lên / xuống để chọn dòng trong danh sách. Ngoài danh sách, ngón trỏ là con trỏ |
| Chụm đầu ngón cái với ngón trỏ | `chum` | Lấy vật đang chọn, hoặc chọn dòng trong danh sách máy / bàn. Trong bảng giao diện: bấm chuột trái (giữ để kéo) |
| Chỉ giơ ngón út | `ut` | Bật hoặc dùng máy trên bàn. Đang cầm dụng cụ thì là dùng (rót, nhỏ giọt, hút). Trỏ ra ngoài bàn thì mở danh sách đi tới bàn khác |
| Xòe cả bàn tay | `mo` | Đặt vật xuống (giữ 0,25 giây), đóng danh sách (giữ 0,4 giây) |

Hóa chất là vật có `ChemicalBottle`, hoặc vật thuộc loại `chai`, `lo_nho_giot`, `lo_rong`, `binh_khi`. Mọi vật khác là dụng cụ.

**Chụm khi chưa mở danh sách thì không cầm gì.** Lấy vật bao giờ cũng qua ba bước: nắm tay, ngón trỏ chọn, chụm để lấy.

## 2. Danh sách trượt và khu tương tác theo bàn

**Danh sách trượt** (`Demo/GestureControl.cs`) trượt ra ở cạnh phải màn hình, dưới khung xem webcam.
- **Nội dung:** liệt kê mọi vật đúng loại trên bàn đang đứng, xếp từ trái sang phải như trên bàn. Dòng chọn sẵn là vật đang trỏ gần nhất.
- **Mỗi dòng:** số thứ tự, tên vật, và một dòng phụ:
  - hóa chất: công thức kèm nồng độ;
  - dụng cụ: "đang chứa … ml" hoặc "trống".
- **Màu nhấn:** dụng cụ màu xanh dương, hóa chất màu cam, máy màu hồng.
- **Chọn dòng:** ngón trỏ trượt xuống thì dòng chọn đi xuống. Mỗi dòng ứng với 5,5% chiều cao màn hình, đo từ chỗ mở danh sách.
  - Chạm đầu hoặc cuối danh sách thì mốc tự dời, nên trượt ngược lại là đi ngay.
  - Danh sách dài hơn 8 dòng thì tự cuộn và hiện "▲ / ▼ còn …".
- **Số trên vật thật:** vật đang chọn và hai vật kề nó có số nổi phía trên, trùng với số trong danh sách.
- **Con trỏ không trượt khi chụm:** lúc chụm, nắm hay giơ ngón út, đầu ngón trỏ gập lại nên vị trí sẽ trôi. Vì vậy con trỏ chốt vị trí đầu ngón trỏ của khoảng 0,15 giây trước, rồi đi theo gốc ngón trỏ.

**Khu tương tác theo bàn** (`Demo/BenchZones.cs`). Mỗi trạm (`LabStation`) gắn với mặt bàn trước mặt nó. Khu gồm mặt bàn, nới rộng 12 cm và cao thêm 0,95 m để tính cả kệ phía trên. Khu được tính lúc chạy nên không cần dựng lại cảnh. Mặt bàn đang làm có viền sáng màu xanh ngọc.
- **Lấy:** danh sách chỉ gồm vật trên bàn này.
- **Đặt:** xòe tay.
  - Trỏ vào mặt bàn thì vật đặt đúng chỗ trỏ.
  - Trỏ ra ngoài bàn thì vật đặt vào chỗ trống gần nhất trên bàn đang làm, không rơi sang bàn khác.
- **Máy:** giơ ngón út để bật. Chỉ xét máy trên bàn đang làm; nếu có nhiều máy thì hiện danh sách máy.
- **Di chuyển:** giơ ngón út khi đang trỏ ra ngoài bàn thì hiện danh sách các bàn, bàn gần xếp trước. Chụm để đi tới bàn đã chọn.

Khi đi lại tự do (không đứng ở trạm nào), danh sách lấy các vật trong bán kính 0,8 m quanh chỗ trỏ.

**Khung xem webcam (F7)** ở góc trên bên phải: hiện ảnh đang đưa vào MediaPipe, các điểm mốc của hai tay, cử chỉ đang nhận, tỉ lệ chụm và nguồn nhận diện.

**Cử chỉ phải giữ ổn định 0,12 giây** mới kích hoạt.

## 3. Nguồn nhận diện tay

`Input/HandTrackerProcess.cs` tự chọn nguồn khi vào lab. F10 bật hoặc tắt nhận diện.

1. **MediaPipe C# trong game** (`Input/PluginHandTracker.cs`). Dùng MediaPipeUnityPlugin v0.16.3 (homuler, MIT), không gọi Python.
   - Mã chỉ biên dịch khi có ký hiệu `KHKT_MEDIAPIPE_PLUGIN` và gói `com.github.homuler.mediapipe`.
   - Hiện chỉ bản sao kiểm thử `D:\KHKT_kiem_thu\proj` (nơi dựng .exe) có gói và cờ này. Gói nằm ở `D:\KHKT_kiem_thu	ai_ve\mediapipe_unity`, 290 MB, nên không đưa vào git.
   - **Cách chạy:**
     - ảnh webcam thu về 320 × 240 và lật như gương trên GPU;
     - đọc về bằng `AsyncGPUReadback`;
     - chép sang bộ nhớ riêng, MediaPipe tự giải phóng qua hàm xóa. `Image` không chép điểm ảnh, nên dùng bộ nhớ tạm sẽ làm sập ứng dụng;
     - HandLandmarker chạy chế độ LIVE_STREAM, kết quả về qua callback;
     - mô hình được nạp ở luồng phụ.
   - **Số đo trên máy này** (bài thử `mp_cs`):
     - 17 ms mỗi khung, khoảng 22 kết quả mỗi giây (giới hạn bởi webcam);
     - sau khi bật xong, không khung game nào quá 50 ms (chậm nhất 31 ms);
     - lúc bật có một lần khựng khoảng 1,2 giây, do Unity mở webcam trên luồng chính.
   - **Lưu ý:**
     - Smart App Control không chặn `mediapipe_c.dll` trên máy này;
     - trên Windows, plugin chỉ chạy CPU và tác giả ghi là thử nghiệm;
     - nạp hỏng thì ứng dụng tự chuyển sang tracker Python.
2. **Tracker Python** `tools/tay_tracker.py`, dùng khi bản dựng không có plugin hoặc plugin nạp hỏng. Cách làm theo `hand_rects.py`:
   - camera chạy luồng riêng và chỉ giữ khung mới nhất;
   - chỉ chạy HandLandmarker, chế độ VIDEO, trên ảnh 320 px đã lật;
   - lọc 1€.

   Tracker xử lý khoảng 11 ms mỗi khung. Nó gửi UDP tới cổng 9054, mỗi gói có 21 điểm 3D, 21 điểm ảnh và nhãn thao tác. Unity tìm Python ở luồng phụ nên game không khựng lúc vào lab.
3. **Dự phòng** `Input/InAppHandTracker.cs` (Inference Engine) khi không chạy được Python.

Khi F8 (điều khiển bằng đầu) đang chạy và giữ webcam, tracker tay tạm dừng. Tracker của F8 vẫn gửi tay về cùng cổng.

Cả hai bản nhận diện thao tác dùng cùng ngưỡng: `tools/tay_thao_tac.py` (Python) và `Input/HandGesture.cs` (C#).
- **Ngón duỗi:** khoảng cách cổ tay tới đầu ngón lớn hơn 1,12 lần khoảng cách cổ tay tới khớp PIP.
- **Chụm:** khoảng cách đầu ngón cái tới đầu ngón trỏ chia cỡ lòng bàn tay dưới 0,28 thì bật, trên 0,40 thì tắt.
- **Nắm ngang hay nắm dọc:** xét thành phần đứng của pháp tuyến lòng bàn tay; lớn hơn 0,55 là nắm ngang.
- **Bỏ phiếu:** lấy nhãn xuất hiện nhiều nhất trong 5 khung gần nhất.
- **Gán tay trái, phải:** theo vị trí cổ tay cũ (nhớ 1,5 giây), không dùng nhãn trái, phải của mô hình.

## 4. Hai bàn tay, không bao giờ ba

`Demo/ArmRig.cs` luôn hiện đúng hai bàn tay:
- Tay thấy trên webcam thì bám theo tay thật.
- Tay không thấy thì về tư thế nghỉ, trượt mềm vì bộ lọc không bị đặt lại.
- Khi đang cầm vật, bàn tay ảo ôm đúng vật. Lúc cầm, vật được kéo về trong tầm với 0,6 m.
- Găng tay cũ của chuột chỉ hiện khi không có bàn tay ảo nào với tới vật. Lúc đó cánh tay phải ẩn đi.

## 5. Kiểm thử

- **Python:** `python -m unittest tests.test_tay_thao_tac`, gồm tư thế, nắm dọc / ngang, ngưỡng trễ chụm, bỏ phiếu, gán bên.
- **Unity:** `chay.ps1 -Mode thao_tac` trong bản sao project, đạt 19/19. Bài thử gồm:
  - nắm ngang mở danh sách dụng cụ, nắm dọc mở danh sách hóa chất, danh sách chỉ có đúng loại;
  - ngón trỏ trượt thì dòng chọn đi theo, chụm thì lấy đúng vật đã chọn;
  - xòe tay khi trỏ ra ngoài bàn thì vật vẫn nằm trên bàn đang làm;
  - xòe tay đóng danh sách;
  - chụm khi chưa mở danh sách thì không cầm gì;
  - ngón út bật máy trên bàn;
  - ngón út trỏ ra ngoài bàn thì hiện danh sách bàn, chụm thì chuyển bàn;
  - suốt bài thử không lúc nào quá hai bàn tay;
  - mất tay thì trả lại điều khiển chuột.
- **Plugin:** `chay.ps1 -Mode mp_cs` kiểm tra nạp `mediapipe_c.dll`, đo tốc độ và độ giật khi bật webcam.
- Ảnh chụp nằm ở `D:\KHKT_kiem_thu\ket_qua\thao_tac`.
