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

---

## Còn tồn

- Chưa có (Giai đoạn 0 hoàn thành sạch sẽ).
