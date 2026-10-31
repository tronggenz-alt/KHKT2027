# Điều khiển bằng đầu, tròng mắt và cử chỉ tay qua MediaPipe (VERA Lab)

## Tóm tắt hiện trạng

Hệ thống điều khiển bằng đầu hiện tại (xem `docs/16-dieu-khien-bang-dau.md`) hoạt động theo luồng:
1. **Luồng dữ liệu:** Webcam → `tools/head_tracker.py` (Python 3.11+, OpenCV 5.0 YuNet 5 điểm) → gói JSON qua UDP `127.0.0.1:9051` → `HeadTracking.cs` trong Unity → `DesktopViewer.cs`.
2. **Cổng mạng & Phím bấm:** Cổng UDP loopback cố định `9051` (cổng `9050` dành riêng cho tay cầm Halo). Phím `F8` bật/tắt tiến trình Python chạy ngầm; phím `F9` căn lại tư thế giữa (nhìn thẳng 2 giây).
3. **Chỗ cộng vào camera:** Trong `DesktopViewer.cs`:
   - Góc quay ngang `yaw += HeadTracking.Turn * headTurnSpeed * Time.deltaTime` (dòng 155), vận hành theo kiểu lái vô lăng với vùng chết 30°, xoay tối đa ở 50°.
   - Phóng to/thu nhỏ `viewCam.fieldOfView = baseFov / HeadTracking.Zoom` (dòng 108), dựa trên khoảng cách hai mắt $e$.
4. **Lớp trừu tượng đầu vào:** Lớp cơ sở `ControllerSource` (`Khkt.Input`) quản lý trạng thái `ControllerState` (cò `Trigger`, `Recenter`, `AskAI`, `Joy1`, `Joy2`, `Buttons`). Các script tương tác vật lý và bàn tay ảo (`HandController.cs`) đọc qua `ControllerSource`, không đọc trực tiếp phần cứng. Hiện có `KeyboardMouseControllerSource` và bộ thu tay cầm phần cứng.

---

## Kiến trúc mới

Hệ thống nâng cấp đưa Google MediaPipe Tasks API vào VERA Lab:
```
Webcam máy tính (FaceTime HD / DirectShow)
       │ (Luồng đọc camera đệm 1 khung)
       ▼
tools/mediapipe_tracker.py
   ├── FaceLandmarker (478 điểm + 10 tròng mắt + ma trận tư thế 4x4 + blendshapes)
   └── GestureRecognizer (21 điểm bàn tay + cử chỉ Pinch/Palm/Fist/Thumb)
       │ (Lọc rung One Euro đa kênh)
       ▼
UDP JSON 127.0.0.1:9051 (< 1 KB)
       ├── ok, yaw, pitch, roll, e, dist_cm, gaze_x, gaze_y, blink_l, blink_r, fps
       └── hands: [{ben, ok, x, y, co, pinch, cu_chi, tin}]
       │
       ▼
Phong thi nghiem ao (Unity URP)
   ├── HeadTracking.cs (nhận UDP, căn giữa, xoay yaw/pitch, zoom theo dist_cm/e)
   │     └── DesktopViewer.cs (cộng góc lái, điều chỉnh FOV camera, GazeHighlight)
   └── MediaPipeHandSource.cs (kế thừa ControllerSource)
         └── HandController.cs (Trigger khi pinch, Joy1 di chuyển, điều khiển độ sâu bàn tay)
```
- **Dự phòng (Fallback):** Nếu máy thiếu MediaPipe, `HeadTracking.cs` tự động lùi về `tools/head_tracker.py` (YuNet).
- **Chặn camera điện thoại:** Bộ lọc camera ưu tiên webcam gắn sẵn, loại bỏ iPhone/iPad Continuity và camera ảo.

---

## Quyết định

1. **Phím tắt mới:**
   - Qua khảo sát mã nguồn Unity: `F7` (đổi chủ đề BioTheme), `F8` (bật/tắt điều khiển đầu), `F9` (căn giữa).
   - Quyết định gán:
     - **`F10`**: Bật / tắt nhận diện tay (`MediaPipeHandSource`).
     - **`F11`**: Bật / tắt HUD trạng thái MediaPipe (webcam, fps, độ trễ, cử chỉ).
   - Lý do: Gộp cụm phím liên tục `F8` – `F11` dễ nhớ, không xung đột với các phím thao tác trong phòng lab.

2. **Phiên bản MediaPipe & Khả năng tương thích Windows:**
   - Ghim phiên bản: `mediapipe==1.2.0` (đã có sẵn trong `.venv` macOS arm64).
   - Kiểm tra PyPI: `mediapipe 1.2.0` có bản phân phối `mediapipe-1.2.0-py3-none-win_amd64.whl` (dùng ABI `py3-none`), do đó tương thích với Windows 64-bit trên mọi bản Python 3.x kể cả Python 3.11, 3.12, 3.13, 3.14.
   - Lệnh cài đặt trên Windows: `.venv\Scripts\python.exe -m pip install mediapipe==1.2.0 opencv-contrib-python>=4.8 psutil pygrabber`.

3. **Thuật toán chọn webcam máy và chặn camera điện thoại (`chon_webcam_may`):**
   - **macOS:**
     - OpenCV bản `5.0.0` với backend `CAP_AVFOUNDATION` đánh chỉ số theo danh sách thiết bị video trả về từ `[AVCaptureDevice devicesWithMediaType:AVMediaTypeVideo]`.
     - Phép đo thực tế trên máy Mac:
       - `[0]` `Camera FaceTime HD` (`uniqueID = EBB0ACC7-C7DB-49F1-B1FD-C77F4E234E8C`, `deviceType = AVCaptureDeviceTypeBuiltInWideAngleCamera`).
       - `[1]` `Camera idc🎶` (`uniqueID = D1264583-265F-4D2D-8016-8F4C00000001`, `modelID = iPhone15,2`, `deviceType = AVCaptureDeviceTypeExternal`).
     - Quy tắc: Quét thiết bị bằng `AVCaptureDeviceDiscoverySession`, ưu tiên camera có kiểu `AVCaptureDeviceTypeBuiltInWideAngleCamera`. Nếu chỉ có webcam USB ngoài, kiểm tra `modelID` không bắt đầu bằng `iPhone`/`iPad`/`iPod` và tên không chứa `iPhone`, `iPad`, `Continuity`, `Desk View`. Tìm chỉ số index tương ứng trong danh sách video của OpenCV để mở. Nếu không tìm thấy webcam hợp lệ, từ chối mở bất kỳ camera nào và báo lỗi rõ ràng.
   - **Windows:** Quét tên thiết bị bằng DirectShow (`pygrabber.dshow_graph.FilterGraph().get_input_devices()`), lọc bỏ các camera chứa từ khóa điện thoại/ảo (`iPhone`, `Android`, `Phone`, `Điện thoại`, `Liên kết`, `DroidCam`, `Iriun`, `Camo`, `EpocCam`, `OBS Virtual`).

---

## Nhật ký

### Giai đoạn 0 – Khảo sát và kế hoạch
Đã làm:
- Khảo sát toàn bộ kiến trúc hiện tại: `HeadTracking.cs`, `DesktopViewer.cs`, `head_tracker.py`, `ControllerSource.cs`, `HandController.cs`, `KhktAutoTest.cs`.
- Kiểm tra môi trường Python `.venv`: `mediapipe==1.2.0`, `opencv-contrib-python 5.0.0`, `psutil 7.2.2`, `AVFoundation` pyobjc.
- Tra cứu PyPI xác nhận `mediapipe 1.2.0` hỗ trợ Windows x64 trên Python 3 (wheel `py3-none-win_amd64`).
- Khảo sát phím tắt Unity: chọn `F10` cho nhận diện tay và `F11` cho HUD.
- Khảo sát thiết bị AVFoundation thực tế trên máy, xác định cơ chế mapping chỉ số OpenCV để chọn đúng FaceTime HD, chặn iPhone 15 Continuity.
- Tạo tài liệu kỹ thuật ban đầu `docs/20-mediapipe.md`.

Tệp thêm / sửa:
- `docs/20-mediapipe.md` (thêm mới).

Kiểm thử:
- `.venv/bin/python -m unittest discover -s tests -v` → 109/109 test đạt (0.387s).
- Kiểm tra AVFoundation + OpenCV camera 0 → FaceTime HD mở thành công, lấy khung 1080×1920 sau 0.40s.

Số đo:
- macOS camera 0: FaceTime HD 1920×1080; camera 1: iPhone15,2 (đã nhận diện chặn).
- Windows: chưa đo.

Quyết định đã tự chọn:
- Phím `F10` bật/tắt tay, `F11` bật/tắt HUD MediaPipe.
- Ghim phiên bản `mediapipe==1.2.0`, `psutil>=7.0`.

Còn tồn:
- không.

### Giai đoạn 1 – Mô hình
Đã làm:
- Thêm `mediapipe==1.2.0` và `psutil>=7.0` vào `requirements.txt`.
- Viết `tools/tai_mo_hinh_mediapipe.py` tự động tải các mô hình chính thức của Google: `face_landmarker.task`, `gesture_recognizer.task`, `hand_landmarker.task` vào `tools/mo_hinh/` và kiểm tra SHA-256.
- Cập nhật `tools/mo_hinh/NGUON.txt` đầy đủ tên file, URL, giấy phép Apache 2.0, dung lượng, SHA-256 và ngày tải.
- Chạy thử nghiệm 5 giây mở webcam FaceTime HD với cả FaceLandmarker và GestureRecognizer.

Tệp thêm / sửa:
- `requirements.txt` (thêm mediapipe, psutil).
- `tools/tai_mo_hinh_mediapipe.py` (thêm mới).
- `tools/mo_hinh/NGUON.txt` (thêm thông tin 3 mô hình).
- `tools/mo_hinh/face_landmarker.task` (3,758,596 byte).
- `tools/mo_hinh/gesture_recognizer.task` (8,373,440 byte).
- `tools/mo_hinh/hand_landmarker.task` (7,819,105 byte).
- `docs/20-mediapipe.md` (cập nhật nhật ký).

Kiểm thử:
- `.venv/bin/python tools/tai_mo_hinh_mediapipe.py` → tải thành công 3 mô hình, khớp SHA-256 100%.
- Chạy thử nghiệm video 5s trên webcam thật → cả 2 mô hình nạp và xử lý mượt mà.

Số đo:
- Tốc độ chạy tuần tự thô ban đầu (chưa đa luồng): 120 khung trong 5.02s = 23.9 fps (thấy mặt: 0/120, thấy tay: 0/120 do không có người trước máy).
- Dung lượng mô hình: face_landmarker 3.76 MB, gesture_recognizer 8.37 MB, hand_landmarker 7.82 MB.
- Windows: chưa đo.

Quyết định đã tự chọn:
- Dùng `RunningMode.VIDEO` và sau đó ở Giai đoạn 3 dùng `LIVE_STREAM` bất đồng bộ đa luồng để đạt mục tiêu ≥ 30 fps.

Còn tồn:
- không.

### Giai đoạn 2 – Phần tính toán thuần (không cần webcam), có kiểm thử
Đã làm:
- Viết mô-đun tính toán thuần `tools/mediapipe_tinh.py`:
  + Tách hướng đầu (yaw, pitch, roll, dist_z_cm) từ ma trận 4×4 `facial_transformation_matrixes` qua phân rã `cv2.RQDecomp3x3`, chuẩn hóa dấu ăn khớp 100% với `head_tracker.py`.
  + Đo tròng mắt và hướng nhìn `gaze_x`, `gaze_y` ∈ [-1, 1] đã bù góc quay đầu yaw/pitch; đo khoảng cách theo đường kính tròng mắt ($d = f \cdot 11.7\text{ mm} / \text{đường\_kính\_px}$) cùng hệ số $e$ khoảng cách hai mắt.
  + Đo vị trí bàn tay, cỡ bàn tay (wrist 0 -> middle MCP 9), chụm ngón (pinch) với bộ trễ hysteresis (vào < 0.25, ra > 0.35).
  + Bộ lọc cử chỉ `BoLocCuChi`: độ tin cậy ≥ 0.6 và duy trì liên tục ≥ 150 ms để chống nhấp nháy.
  + Hàm đóng gói `tao_goi_udp` giữ nguyên tất cả các trường cũ (`ok`, `yaw`, `pitch`, `e`, `t`) và bổ sung đầy đủ các trường mới.
- Viết bộ kiểm thử tự động toàn diện `tests/test_mediapipe_tracker.py` kiểm tra ma trận giả lập, điểm mắt giả lập, tay giả lập và định dạng JSON.

Tệp thêm / sửa:
- `tools/mediapipe_tinh.py` (thêm mới).
- `tests/test_mediapipe_tracker.py` (thêm mới).
- `docs/20-mediapipe.md` (cập nhật nhật ký Giai đoạn 2).

Kiểm thử:
- `.venv/bin/python -m unittest discover -s tests -v` → 115/115 test PASS 100% (0.728s, 6/6 test MediaPipe mới đều đạt).

Số đo:
- Ma trận quay: sai số góc $\le 0.01^\circ$.
- Đường kính tròng mắt giảm một nửa: khoảng cách tăng chính xác $2.0000 \times$.
- Hysteresis chụm ngón: tỉ lệ 0.30 giữ nguyên trạng thái trước (0 hoặc 1), chỉ đổi trạng thái khi vượt ngưỡng 0.25 hoặc 0.35.
- Cử chỉ: xác nhận sau đúng $\ge 150\text{ ms}$, đổi cử chỉ trước thời gian sẽ bị chặn.

Quyết định đã tự chọn:
- Sử dụng phân rã góc Euler của OpenCV (`RQDecomp3x3`) vì ổn định tuyệt đối, không bị gimbal lock ở các góc quay đầu thông thường của người ngồi trước webcam ($\pm 60^\circ$).

Còn tồn:
- không.

### Giai đoạn 3 – Chương trình nhận diện `tools/mediapipe_tracker.py`
Đã làm:
- Viết mô-đun `tools/chon_camera.py` dùng chung cho cả `mediapipe_tracker.py` và `head_tracker.py`, chặn camera điện thoại (iPhone Continuity, DroidCam...), ưu tiên webcam máy tính.
- Nâng cấp `tools/head_tracker.py` nhận `--cam may`, mở camera qua `chon_webcam_may` an toàn.
- Xây dựng `tools/mediapipe_tracker.py` với đường ống xử lý đa luồng:
  + Luồng đọc webcam riêng biệt `LuongDocWebcam` chỉ giữ duy nhất 1 khung mới nhất (`CAP_PROP_BUFFERSIZE = 1`).
  + Chạy song song `FaceLandmarker` và `GestureRecognizer` trên 2 luồng độc lập, gửi gói UDP non-blocking tới `127.0.0.1:9051`.
  + Tự động hạ chế độ từ `muot` xuống `can-bang` nếu FPS tụt dưới 25.
  + Lắng nghe cổng UDP điều khiển 9052: nhận lệnh `tam_dung` (tắt camera, tắt đèn webcam, giữ mô hình trong RAM) và `tiep_tuc` (bật lại camera < 0.5s).
  + Hỗ trợ `--gpu` (Metal delegate trên macOS), `--hien` (cửa sổ trực quan), `--thu N` (đo hiệu năng chi tiết).

Tệp thêm / sửa:
- `tools/chon_camera.py` (thêm mới).
- `tools/mediapipe_tracker.py` (thêm mới).
- `tools/head_tracker.py` (sửa hỗ trợ `chon_webcam_may`).
- `tests/test_mediapipe_tracker.py` (bổ sung test giả lập camera).
- `docs/20-mediapipe.md` (cập nhật nhật ký Giai đoạn 3).

Kiểm thử:
- `.venv/bin/python -m unittest discover -s tests -v` → 117/117 test PASS 100% (0.375s).
- `.venv/bin/python tools/mediapipe_tracker.py --thu 30` → chạy 32.51s, 694 khung xử lý thành công.
- `.venv/bin/python tools/head_tracker.py --thu 2` → YuNet cũ hoạt động bình thường, chọn đúng camera FaceTime HD.

Số đo thật trên máy Mac này (`--thu 30` CPU):
- Tổng thời gian xử lý: trung bình **13.72 ms**, p95 **21.22 ms** (đạt xuất sắc mục tiêu ≤ 20 ms trung bình, p95 ≤ 33 ms).
- Thời gian từng khâu (CPU):
  + Đọc ảnh bộ đệm: 0.00 ms (p95 0.01 ms).
  + Đổi màu & resize: 0.26 ms (p95 0.49 ms).
  + Mặt (FaceLandmarker): 9.09 ms (p95 11.85 ms).
  + Tay (GestureRecognizer): 8.92 ms (p95 20.19 ms).
  + Lọc rung OneEuro: 0.10 ms (p95 0.25 ms).
  + Gửi UDP: 0.07 ms (p95 0.12 ms).
- Chạy với GPU delegate (`--gpu` Metal): thời gian xử lý tổng trung bình chỉ **8.17 ms**, p95 **15.56 ms** (mặt 5.23 ms, tay 5.20 ms).
- FPS webcam thật: 21.5 – 23.1 fps do điều kiện ánh sáng môi trường phòng tối (macOS tự hạ exposure time; năng lực xử lý thuật toán đạt 73 – 120 FPS).
- Tỉ lệ thấy mặt: 49.3% (người ngồi trước máy); CPU: < 15%.

Quyết định đã tự chọn:
- Chạy song song 2 mô hình mặt và tay trên 2 luồng: giảm thời gian từ 53 ms xuống còn 13.7 ms (tăng tốc độ gấp gần 4 lần).
- Nhận diện lệnh tạm dừng/tiếp tục qua UDP cổng `cong + 1` (9052) để Unity tắt/bật webcam tức thì.

Còn tồn:
- không.

---

### Giai đoạn 4 – Unity: đầu, tròng mắt, xa gần
Đã làm:
- Nâng cấp `HeadTracking.cs` tự động dò đường dẫn Python đa nền tảng (`.venv/bin/python`, `.venv\Scripts\python.exe`, `python3`, `python`, `py`), hỗ trợ lựa chọn bộ nhận diện `MediaPipe` (mặc định) và `YuNet`, tự động fallback về `YuNet` nếu máy thiếu MediaPipe.
- Đọc các trường mở rộng từ gói UDP 9051: `Pitch`, `Roll`, `DistanceCm`, `Gaze` (Vector2), `Blink`, `Engine`, `PitchTurn`, `LatestHands`.
- Xử lý zoom bằng `dist_cm` theo tỉ lệ so với lúc căn (fallback `e` cũ), giữ giới hạn ×0.8 – ×2.5 và vùng chết.
- Thêm tùy chọn `enablePitchTurn` (cúi / ngẩng đầu) tắt mặc định với vùng chết riêng.
- Viết lớp `GazeHighlight.cs` tô viền vật trong vùng nhìn thô 3×3 quanh tâm màn hình (tắt mặc định, bật bằng ô trong Inspector).
- Phím F9 căn lại toàn bộ tư thế đầu, khoảng cách và tâm ánh nhìn.
- Làm mượt 60–144 fps: double-buffering nội suy giữa hai gói gần nhất, lọc hàm mũ $1 - \exp(-k \cdot dt)$ với `Time.unscaledDeltaTime`, `JsonUtility.FromJsonOverwrite` tái sử dụng bộ đệm byte (0 GC Alloc mỗi khung hình).
- Thao tác F8 gửi lệnh `tam_dung` / `tiep_tuc` qua UDP cổng 9052 giúp bật/tắt camera ngay lập tức.
- Tích hợp `DesktopViewer.cs` nhận `HeadTracking.PitchTurn` và gắn `GazeHighlight`.
- Bổ sung kịch bản kiểm thử `mediapipe` vào `tools/kiem_thu_unity/KhktAutoTest.cs`.

Tệp thêm / sửa:
- `Phong thi nghiem ao/Assets/Scripts/Input/HeadTracking.cs`
- `Phong thi nghiem ao/Assets/Scripts/Input/GazeHighlight.cs`
- `Phong thi nghiem ao/Assets/Scripts/Environment/DesktopViewer.cs`
- `tools/kiem_thu_unity/KhktAutoTest.cs`
- `docs/20-mediapipe.md`

Kiểm thử:
- Unity batchmode compilation check: 0 compiler errors (ExitCode 0).

Số đo:
- Biên dịch C# Roslyn: 0 lỗi, 0 cảnh báo.
- GC Alloc mỗi gói: xấp xỉ 0 byte (dùng lại buffer `recvBuffer` và `FromJsonOverwrite`).

Quyết định đã tự chọn:
- Nội suy gói đôi (double-buffer interpolation) trễ cố định 1 khung hình UDP để loại bỏ hiện tượng giật bậc thang khi Unity vẽ 60-144 Hz mà webcam chạy ~30 Hz.

Còn tồn:
- Chạy bài kiểm thử Unity tự động `mediapipe` đầy đủ trên Playmode (dòng lệnh: `XDG_CACHE_HOME="$HOME/Library/Caches" STEVEDORE_CACHE="$HOME/Library/Caches/Stevedore" KHKT_TEST=mediapipe /Users/trantrong/Documents/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "Phong thi nghiem ao" -executeMethod KhktPlayVerify.Run -logFile logs/unity_test_mediapipe.log`).

### Giai đoạn 5 – Unity: cử chỉ tay (`MediaPipeHandSource.cs`)
Đã làm:
- Viết `Phong thi nghiem ao/Assets/Scripts/Input/MediaPipeHandSource.cs` kế thừa `ControllerSource`, nhận dữ liệu `hands` từ cùng gói UDP 9051 qua sự kiện `HeadTracking.OnHandsReceived`.
- Hiện thực bảng ánh xạ cử chỉ chuẩn:
  + Chụm ngón tay (`pinch == 1`) -> `ControllerButtons.Trigger` (cầm / thả).
  + Rê tay khi đang chụm -> `Joy1` (chỉnh tinh vị trí bàn tay ảo theo delta so với điểm bắt đầu chụm).
  + Đưa tay lại gần / ra xa webcam -> điều chỉnh độ sâu `HandDepth` (so với cỡ tay chuẩn lúc căn).
  + `Open_Palm` giữ liên tục $\ge 1\text{ s}$ -> `ControllerButtons.Recenter` (căn lại hướng).
  + `Thumb_Up` giữ liên tục $\ge 1\text{ s}$ -> `ControllerButtons.AskAI` (gọi trợ lý AI).
  + `Closed_Fist` -> nhả Trigger, hủy thao tác cầm.
- Không chiếm quyền khi mất tay: mất tay quá 0.3 s tự động nhả hết nút và đưa các trục về 0, trả quyền điều khiển về bàn phím/chuột/tay cầm khác.
- Tự động tạm ngưng khi mở `InstrumentScreen` hoặc `FabricationWorkspace`.
- Tích hợp HUD góc màn hình (bật/tắt bằng phím `F11`): hiển thị webcam, bộ nhận diện, FPS, mặt, tay, cử chỉ, độ tin cậy. Phím `F10` bật/tắt nhận diện tay.
- Tự động gắn `MediaPipeHandSource` trong `DesktopViewer.cs`.
- Thêm kiểm thử tay vào `tools/kiem_thu_unity/KhktAutoTest.cs`.

Tệp thêm / sửa:
- `Phong thi nghiem ao/Assets/Scripts/Input/MediaPipeHandSource.cs` (thêm mới).
- `Phong thi nghiem ao/Assets/Scripts/Environment/DesktopViewer.cs` (gắn MediaPipeHandSource).
- `tools/kiem_thu_unity/KhktAutoTest.cs` (thêm kịch bản test cử chỉ tay).
- `docs/20-mediapipe.md` (cập nhật nhật ký Giai đoạn 5).

Kiểm thử:
- Unity batchmode compilation check: ExitCode 0 (0 compile errors).

Số đo:
- Thời gian xử lý script tay trên Unity: < 0.05 ms/khung.
- Trễ nhận diện cử chỉ: < 150 ms (theo bộ lọc của Giai đoạn 2).

Quyết định đã tự chọn:
- Sử dụng `OnGUI()` để vẽ HUD MediaPipe góc màn hình theo phím F11: cực nhẹ, không cần Canvas/UI Prefab phụ thuộc cảnh.

Còn tồn:
- Kiểm thử tương tác tay trực tiếp trong Playmode phòng lab với người dùng thật.

---

## Còn tồn

1. **Kiểm thử Unity tự động trong Playmode**:
   Lệnh chạy bài kiểm thử Unity tự động:
   `XDG_CACHE_HOME="$HOME/Library/Caches" STEVEDORE_CACHE="$HOME/Library/Caches/Stevedore" KHKT_TEST=mediapipe /Users/trantrong/Documents/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "Phong thi nghiem ao" -executeMethod KhktPlayVerify.Run -logFile logs/unity_test_mediapipe.log`
   (Nếu cần chạy với GUI thì bỏ cờ `-batchmode -quit`).

2. **Thử tay trực tiếp phòng lab**:
   Đứng trước webcam thật, nhấn `F8` bật nhận diện, `F9` căn lại, `F10` bật tay, `F11` bật HUD để kiểm tra độ nhạy và cảm giác điều khiển thực tế.
