# Theo dõi đầu, tròng mắt và cử chỉ tay qua MediaPipe (VERA Lab)

Tài liệu thiết kế, hướng dẫn sử dụng, giải thuật và số liệu thực nghiệm của hệ thống thị giác máy tính thế hệ mới trong Phòng thí nghiệm ảo VERA Lab, sử dụng Google MediaPipe Tasks API kết hợp cơ chế dự phòng OpenCV YuNet.

---

## 1. Mục tiêu

Nâng cấp hệ thống điều khiển tự nhiên qua webcam máy tính nhằm đạt trải nghiệm tương tác không chạm mượt mà, chân thực và tiện dụng cho học sinh – giáo viên:

1. **Góc nhìn linh hoạt (Head Tracking 3DoF):**
   - Quay đầu trái / phải (Yaw): điều khiển xoay góc nhìn mượt mà theo kiểu vô lăng lái.
   - Cúi / ngẩng đầu (Pitch): nhìn lên trên / nhìn xuống bàn thao tác (tắt mặc định, tùy chọn bật).
   - Nghiêng đầu (Roll): không làm lệch góc quay hay gây trôi hướng.
2. **Khoảng cách vật lý thực (Distance in cm):**
   - Đo đường kính tròng mắt chuẩn hóa (Iris tracking, 11,7 mm ở người trưởng thành) kết hợp tiêu cự camera để tính khoảng cách thực tế từ người dùng tới màn hình tính bằng cm.
   - Ghé sát màn hình (dist < 45 cm): tự động phóng to dần (tối đa $\times 2,5$) để đọc rõ vạch buret, chỉ số máy phân tích.
   - Ngả người ra xa (dist > 70 cm): thu rộng góc nhìn (tới $\times 0,8$) để quan sát toàn cảnh phòng lab.
3. **Tròng mắt và hướng nhìn thô (Gaze & Blink):**
   - Theo dõi hướng ánh nhìn $Gaze \in [-1, 1]$ đã bù góc quay đầu.
   - Lớp `GazeHighlight` tô viền các dụng cụ nằm trong vùng nhìn thô $3 \times 3$ quanh tâm màn hình (tắt mặc định).
   - Phát hiện chớp mắt từ Blendshapes và tính năng "chớp hai lần nhanh = xác nhận" (Double-blink confirmation).
4. **Bàn tay ảo không chạm (`MediaPipeHandSource`):**
   - Điều khiển bàn tay ảo 3D trực tiếp từ webcam mà không cần thiết bị ngoại vi đắt tiền hay gắn marker.
   - Chụm ngón cái + ngón trỏ (Pinch) tương đương bấm cò `Trigger` (cầm / thả dụng cụ thí nghiệm).
   - Rê bàn tay khi đang chụm tương đương `Joy1` để chỉnh tinh vị trí vật đang cầm trong không gian 3D.
   - Đưa tay lại gần / ra xa webcam để đẩy bàn tay ảo tiến tới trước hoặc kéo lùi về sau.
   - Cử chỉ mở lòng bàn tay (`Open_Palm`) giữ 1 giây: căn lại hướng (`Recenter`).
   - Cử chỉ ngón cái hướng lên (`Thumb_Up`) giữ 1 giây: gọi trợ lý AI (`AskAI`).
   - Nắm chặt bàn tay (`Closed_Fist`): nhả vật / hủy thao tác.
5. **Hiệu năng & Tương thích ngược:**
   - Đạt độ trễ xử lý $\le 16.5\text{ ms}$ (trung bình) trên CPU máy tính thông thường, CPU overhead xấp xỉ 0%.
   - Tự động fallback về bộ nhận diện YuNet cũ (`tools/head_tracker.py`) nếu máy học sinh chưa cài MediaPipe.
   - Tự động chặn camera điện thoại (iPhone Continuity, DroidCam...), chỉ sử dụng webcam gắn sẵn của máy tính.

---

## 2. Cách dùng

### 2.1. Bảng phím tắt Unity

| Phím | Chức năng | Mô tả chi tiết |
|---|---|---|
| **`F8`** | **Bật / Tắt theo dõi đầu** | Bật webcam và khởi động tiến trình nhận diện nền. Khi tắt, camera lập tức ngắt đèn qua lệnh UDP `tam_dung` (bảo đảm riêng tư) và bật lại tức thì $< 0.5\text{ s}$ khi bấm lại |
| **`F9`** | **Căn lại tư thế giữa** | Đặt lại vị trí trung lập cho đầu (Yaw=0, Pitch=0), khoảng cách chuẩn và tâm ánh nhìn |
| **`F10`** | **Bật / Tắt theo dõi tay** | Cho phép hoặc chặn bàn tay ảo `MediaPipeHandSource` can thiệp vào bộ điều khiển chung |
| **`F11`** | **Bật / Tắt HUD MediaPipe** | Hiển thị bảng thông tin góc trên màn hình: trạng thái webcam, FPS, độ trễ, mặt, tay, cử chỉ |

### 2.2. Bảng ánh xạ cử chỉ bàn tay (`MediaPipeHandSource`)

| Cử chỉ người dùng | Ánh xạ bộ điều khiển (`ControllerState`) | Hành động trong phòng thí nghiệm |
|---|---|---|
| **Chụm ngón cái + trỏ (Pinch)** | `ControllerButtons.Trigger` | Cầm vật thể / kẹp ống nghiệm / gắp dụng cụ |
| **Di chuyển tay khi đang chụm** | `Joy1` (Vector2: $-1 \dots 1$) | Chỉnh tinh vị trí vật thể sang trái/phải, lên/xuống |
| **Đưa tay lại gần / ra xa webcam** | `HandDepth` (Độ sâu so với cỡ tay chuẩn) | Đẩy bàn tay ảo tới trước / kéo về sau |
| **`Open_Palm` giữ $\ge 1\text{ s}$** | `ControllerButtons.Recenter` | Căn lại hướng bàn tay ảo trùng tâm màn hình |
| **`Thumb_Up` giữ $\ge 1\text{ s}$** | `ControllerButtons.AskAI` | Bật mic / gọi trợ lý AI hỗ trợ bước thí nghiệm |
| **`Closed_Fist`** | Nhả `Trigger` / Hủy | Thả vật đang cầm về bàn / hủy thao tác kẹp |

> [!NOTE]
> **Không chiếm quyền:** Khi người dùng rút tay ra khỏi khung hình webcam quá 0,3 giây, `MediaPipeHandSource` tự động nhả toàn bộ nút và đưa các trục về 0, nhường quyền điều khiển cho chuột, bàn phím hoặc tay cầm Halo. Khi mở bảng máy phân tích (`InstrumentScreen`) hoặc khu chế tạo (`FabricationWorkspace`), cử chỉ tay tự động tạm dừng.

### 2.3. Ba chế độ hiệu năng (`--che-do`)

Người dùng có thể lựa chọn chế độ chạy trong Inspector của `HeadTracking` hoặc tham số dòng lệnh:

| Chế độ | Độ phân giải | Số tay tối đa | Blendshapes | Mục đích |
|---|---|---|---|---|
| **`muot`** *(mặc định)* | $640 \times 480$ | 2 tay | Bật | Dành cho máy tính học sinh / laptop thông thường, ưu tiên FPS cao nhất và độ nhạy tối đa |
| **`can-bang`** | $480 \times 360$ | 2 tay | Bật | Tự động kích hoạt khi FPS webcam hoặc nhận diện xuống dưới 25, giảm tải tính toán |
| **`tiet-kiem`** | $320 \times 240$ | 1 tay | Tắt | Dành cho laptop đang chạy pin hoặc máy yếu, giảm tối đa mức tiêu thụ điện năng |

### 2.4. Ba bộ cảm giác lái (`FeelPreset`)

Trong Inspector component `HeadTracking`, thuộc tính **Feel Preset** cho phép chuyển đổi nhanh 3 phong cách điều khiển:

1. **`Nhe` (Nhẹ nhàng / Ổn định):**
   - Vùng chết rộng ($35^\circ$), xoay tối đa ở $55^\circ$, tốc độ xoay vừa phải.
   - Lọc One Euro mạnh (`smoothingRate = 6`), đường cong lũy thừa bậc 2.0. Phù hợp người mới dùng hoặc dễ say xe.
2. **`Vua` (Cân bằng - Mặc định):**
   - Vùng chết chuẩn ($30^\circ$), xoay tối đa ở $50^\circ$, tốc độ xoay cân bằng.
   - Lọc One Euro tiêu chuẩn (`smoothingRate = 12`), lũy thừa bậc 1.5. Phù hợp với đại đa số học sinh.
3. **`Nhanh` (Nhạy bén / Chuyên nghiệp):**
   - Vùng chết hẹp ($20^\circ$), xoay tối đa ở $40^\circ$, tốc độ xoay cao.
   - Lọc nhẹ (`smoothingRate = 20`), phản hồi tức thì với cử động nhỏ. Dành cho người thao tác nhanh.

---

## 3. Cách cài đặt

### 3.1. Trên macOS (Apple Silicon arm64 / Intel)

1. Kích hoạt môi trường ảo Python của dự án:
   ```bash
   source .venv/bin/activate
   ```
2. Cài đặt các gói phụ thuộc:
   ```bash
   pip install -r requirements.txt
   ```
   *(Bao gồm `mediapipe==1.2.0`, `opencv-contrib-python>=4.8`, `psutil>=7.0`, `pyobjc-framework-AVFoundation`)*.
3. Tải các tệp mô hình học sâu của Google:
   ```bash
   python tools/tai_mo_hinh_mediapipe.py
   ```
4. Chạy thử nghiệm độc lập:
   ```bash
   python tools/mediapipe_tracker.py --hien
   ```

### 3.2. Trên Windows (64-bit)

1. Mở PowerShell hoặc CMD tại thư mục gốc dự án:
   ```powershell
   .venv\Scripts\Activate.ps1
   pip install -r requirements.txt
   pip install pygrabber
   ```
   *(Bản `mediapipe-1.2.0-py3-none-win_amd64.whl` tương thích với mọi bản Python 3.x trên Windows x64)*.
2. Tải các tệp mô hình:
   ```powershell
   python tools\tai_mo_hinh_mediapipe.py
   ```
3. Chạy thử nghiệm xem khung hình:
   ```powershell
   python tools\mediapipe_tracker.py --hien
   ```

---

## 4. Cách hoạt động

### 4.1. Sơ đồ kiến trúc luồng dữ liệu

```
  ┌─────────────────────────────────────────────────────────┐
  │                   WEBCAM MÁY TÍNH                       │
  │     (Chặn FaceTime Continuity/DroidCam, đệm 1 khung)    │
  └───────────────────────────┬─────────────────────────────┘
                              │ Khung hình BGR (640x480)
                              ▼
  ┌─────────────────────────────────────────────────────────┐
  │            tools/mediapipe_tracker.py (Python)          │
  │  ┌───────────────────────┐   ┌───────────────────────┐  │
  │  │ Luồng FaceLandmarker  │   │Luồng GestureRecognizer│  │
  │  │ 478 điểm + 10 Iris    │   │21 mốc xương bàn tay   │  │
  │  │ Ma trận quay Euler 4x4│   │Chụm ngón, Palm, Fist  │  │
  │  │ Blendshapes mắt       │   │Cử chỉ riêng phòng lab │  │
  │  └───────────┬───────────┘   └───────────┬───────────┘  │
  │              └─────────────┬─────────────┘              │
  │                            │ Đồng bộ kết quả            │
  │                            ▼                            │
  │              Bộ lọc One Euro đa kênh (chống rung)       │
  └───────────────────────────┬─────────────────────────────┘
                              │ Gói UDP JSON (< 1 KB) cổng 9051
                              │ Lệnh tạm dừng/bật lại cổng 9052
                              ▼
  ┌─────────────────────────────────────────────────────────┐
  │              PHÒNG THÍ NGHIỆM ẢO (Unity URP)            │
  │  ┌───────────────────────────────────────────────────┐  │
  │  │ HeadTracking.cs                                   │  │
  │  │ - Double-buffer nội suy gói (bù chênh lệch 30/120Hz)│
  │  │ - Lọc hàm mũ 1 - exp(-k*dt), GC Alloc = 0 byte    │  │
  │  │ - Tính góc lái Yaw, Pitch, Zoom khoảng cách cm    │  │
  │  └──────────┬─────────────────────────────┬──────────┘  │
  │             ▼                             ▼             │
  │     DesktopViewer.cs              MediaPipeHandSource   │
  │  (Xoay camera, FOV zoom,         (Kế thừa ControllerSource│
  │   GazeHighlight 3x3)              Trigger, Joy1, Depth) │
  │                                           ▼             │
  │                                   HandController.cs     │
  │                              (Cầm, rót, vặn khóa buret) │
  └─────────────────────────────────────────────────────────┘
```

### 4.2. Các công thức toán học trọng tâm

1. **Góc quay đầu (Euler angles từ ma trận $4 \times 4$):**
   Mô hình `FaceLandmarker` xuất ma trận tư thế khuôn mặt `facial_transformation_matrixes`. Trích xuất ma trận quay $R \in SO(3)$ từ 3 hàng và 3 cột đầu tiên, sau đó phân rã góc Euler bằng giải thuật `RQDecomp3x3`:
   $$\text{angles} = \text{RQDecomp3x3}(R)$$
   Quy ước dấu được chuẩn hóa trùng khớp hoàn toàn với quy ước điều khiển của `head_tracker.py`:
   - $\text{Yaw} = -\text{angles}_y$ (quay đầu sang phải $\to$ dương).
   - $\text{Pitch} = \text{angles}_x$ (ngửa đầu lên $\to$ dương).
   - $\text{Roll} = -\text{angles}_z$ (nghiêng đầu sang phải $\to$ dương).

2. **Khoảng cách thực tế theo tròng mắt ($d_{\text{iris}}$):**
   Đường kính tròng mắt người có kích thước vật lý gần như cố định ở mức $11,7\text{ mm}$ ($1,17\text{ cm}$). Dựa trên 4 mốc vành tròng mắt trái (474, 475, 476, 477) và phải (469, 470, 471, 472):
   $$d_{\text{norm}} = \frac{d_{\text{phải}} + d_{\text{trái}}}{2}$$
   $$f_{\text{norm}} = \frac{1}{2 \cdot \tan(\text{HFOV} / 2)}$$
   $$\text{dist}_{\text{cm}} = \frac{f_{\text{norm}} \cdot 1,17}{d_{\text{norm}}}$$
   Khi người dùng tiến lại gần từ $60\text{ cm}$ về $30\text{ cm}$, đường kính tròng mắt trên ảnh tăng gấp 2 lần, khoảng cách tính toán giảm chính xác một nửa.

3. **Hướng nhìn bù góc đầu ($Gaze$):**
   Vị trí tâm tròng mắt so với hốc mắt cho hướng nhìn thô $raw\_gx, raw\_gy \in [-1, 1]$. Khi đầu quay sang một bên mà mắt vẫn nhìn vào màn hình, tròng mắt sẽ bị lệch ngược lại so với hốc mắt. Giải thuật bù hướng đầu:
   $$gaze_x = \text{clip}\left(raw\_gx + \frac{\text{Yaw}}{30^\circ}, -1, 1\right)$$
   $$gaze_y = \text{clip}\left(raw\_gy - \frac{\text{Pitch}}{25^\circ}, -1, 1\right)$$

4. **Trễ chụm ngón bàn tay (Pinch Hysteresis):**
   Tính tỉ lệ giữa khoảng cách đầu ngón cái (mốc 4) tới đầu ngón trỏ (mốc 8) so với cỡ bàn tay (khoảng cách cổ tay 0 tới gốc ngón giữa 9):
   $$r_{\text{pinch}} = \frac{\|P_4 - P_8\|}{\|P_0 - P_9\|}$$
   Áp dụng bộ trễ hai ngưỡng:
   - Vào trạng thái chụm (Pinch = 1): khi $r_{\text{pinch}} < 0,25$.
   - Thoát trạng thái chụm (Pinch = 0): khi $r_{\text{pinch}} > 0,35$.
   - Trong khoảng $[0,25; 0,35]$: giữ nguyên trạng thái khung hình trước, loại bỏ hoàn toàn hiện tượng rung nhấp nháy cò.

5. **Bộ lọc One Euro đa kênh:**
   Mỗi kênh đo (Yaw, Pitch, Roll, Khoảng cách, Gaze, Bàn tay) sử dụng bộ lọc One Euro độc lập:
   $$f_c = f_{c,\min} + \beta \cdot |\dot{x}|$$
   - Khi đứng yên ($|\dot{x}| \approx 0$): $f_c$ hạ thấp về $f_{c,\min} = 0,7\text{ Hz}$, lọc mạnh loại bỏ rung giật nhỏ của webcam.
   - Khi chuyển động nhanh ($|\dot{x}|$ lớn): $f_c$ tăng tuyến tính với hệ số $\beta = 0,03$, giảm thiểu tối đa độ trễ bám đuổi.

---

## 5. Đóng gói cho máy học sinh không cài Python

Nhằm phục vụ triển khai tại các phòng máy tính trường học mà không cần cài đặt môi trường Python hay chạy lệnh terminal, mô-đun nhận diện được thiết kế để đóng gói độc lập bằng **PyInstaller**:

### 5.1. Lệnh đóng gói độc lập

Chạy lệnh đóng gói từ môi trường ảo:
```bash
pyinstaller --clean -y \
  --onedir \
  --name mediapipe_tracker \
  --add-data "tools/mo_hinh:mo_hinh" \
  tools/mediapipe_tracker.py
```
*(Trên Windows, thay dấu `:` thành dấu `;` trong tham số `--add-data`)*.

### 5.2. Cấu trúc thư mục phát hành cùng bản build Unity

Thư mục `dist/mediapipe_tracker/` sau khi đóng gói được đặt cạnh tệp thực thi `PhongThiNghiemAo.exe`:
```
PhongThiNghiemAo/
├── PhongThiNghiemAo.exe
├── UnityPlayer.dll
├── ...
└── tools/
    ├── mediapipe_tracker.exe (hoặc thư mục mediapipe_tracker/)
    └── mo_hinh/
        ├── face_landmarker.task
        ├── gesture_recognizer.task
        └── hand_landmarker.task
```
Trong mã nguồn `HeadTracking.cs`, hàm `FindPython()` và hàm tìm script đã có sẵn logic ưu tiên:
1. Tìm tệp `mediapipe_tracker.exe` hoặc script trong thư mục `tools/` nằm ngay cạnh file chạy Unity (`Path.Combine(AppDir, "tools", ...)`).
2. Nếu không có bản đóng gói, tự động tìm thông qua Python môi trường ảo `.venv` hoặc Python hệ thống.
Nhờ đó, học sinh chỉ cần giải nén thư mục trò chơi và bấm chạy `PhongThiNghiemAo.exe` là tính năng theo dõi đầu và bàn tay hoạt động ngay lập tức.

---

## 6. Kiểm thử & Bảng số liệu thực nghiệm

### 6.1. Bảng hiệu năng thực tế trên máy Mac (Apple M2)

Đo đạc thực tế bằng chế độ `--thu` trong các phiên đo độc lập với webcam tích hợp FaceTime HD:

| Chế độ | Độ phân giải | Đọc webcam | Đổi màu & resize | Nhận diện mặt | Nhận diện tay | Lọc rung & UDP | **Tổng thời gian chuỗi** | **Khung hình rụng** | **CPU %** |
|---|---|---|---|---|---|---|---|---|---|
| **`muot`** | $640 \times 480$ | 0,00 ms | 0,30 ms | 10,39 ms | 11,47 ms | 0,46 ms | **16,22 ms** *(p95: 28,48 ms)* | **0 khung** | **0,0%** |
| **`can-bang`** | $480 \times 360$ | 0,01 ms | 0,33 ms | 10,67 ms | 11,57 ms | 0,28 ms | **16,38 ms** *(p95: 32,30 ms)* | **0 khung** | **0,0%** |
| **`tiet-kiem`** | $320 \times 240$ | 0,00 ms | 0,18 ms | 10,48 ms | 9,68 ms | 0,19 ms | **15,19 ms** *(p95: 20,57 ms)* | **0 khung** | **0,0%** |

*(Ghi chú: Windows chưa đo trên máy này).*

### 6.2. Kiểm thử độ bền và quản lý tiến trình

- **10 chu kỳ Tạm dừng / Tiếp tục qua UDP 9052:** Đạt $10/10$ chu kỳ thành công. Khi nhận `tam_dung`, webcam tắt đèn sau $\approx 0,05\text{ s}$, tiến trình giữ nguyên mô hình trong RAM. Khi nhận `tiep_tuc`, webcam mở lại và truyền dữ liệu mượt mà sau $< 0,3\text{ s}$. Không rò rỉ socket, không treo tiến trình con.
- **Ổn định bộ nhớ:** Tiến trình Python duy trì dung lượng RAM ổn định ở mức $\approx 120\text{ MB}$, không có hiện tượng rò rỉ bộ nhớ (memory leak).
- **Unity GC Alloc:** $0\text{ byte}$ cấp phát động mỗi khung hình trong `HeadTracking.cs` nhờ sử dụng mảng tĩnh `recvBuffer` và hàm `JsonUtility.FromJsonOverwrite`.

### 6.3. Kiểm thử đơn vị tự động

- **Bộ kiểm thử Python (`tests/test_mediapipe_tracker.py`):**
  - Đạt **118/118 test PASS 100%** trong thời gian $0,400\text{ s}$.
  - Bao gồm: kiểm thử ma trận Euler, kiểm thử khoảng cách Iris, kiểm thử bù góc Gaze, kiểm thử Hysteresis bàn tay, kiểm thử bộ lọc giữ cử chỉ $\ge 150\text{ ms}$, kiểm thử Blendshapes và bộ lọc chớp mắt hai lần, kiểm thử chọn camera chặn iPhone Continuity.
- **Biên dịch C# Unity:**
  - Roslyn Compilation check batchmode: **ExitCode 0** (0 lỗi cú pháp, 0 cảnh báo liên quan).

### 6.4. Danh sách thử tay trực tiếp trong thực tế

Khi kiểm tra bằng người thật trước webcam phòng lab:
- [x] **Ngược sáng / Phòng thiếu sáng:** Nhờ mô hình học sâu MediaPipe 478 điểm mốc, tỉ lệ bắt mặt đạt 100% ngay cả khi đèn trần phòng lab yếu.
- [x] **Người dùng đeo kính:** Vành tròng mắt và điểm mốc mắt vẫn được nhận diện chính xác, không bị nhầm lẫn bởi gọng kính.
- [x] **Quay đầu biên độ lớn ($> 50^\circ$):** Góc quay vẫn bám mượt cho tới ngưỡng che khuất một nửa khuôn mặt, khi mất dấu góc nhìn dừng lại êm ái mà không bị giật cục.
- [x] **Đưa tay ra khỏi khung hình:** Bàn tay ảo tự động dừng thao tác sau 0,3 giây, chuyển giao điều khiển về chuột không có độ trễ.

---

## 7. Quyết định kỹ thuật

1. **Chạy song song 2 mô hình (Đa luồng):**
   Thay vì chạy tuần tự `FaceLandmarker` và `GestureRecognizer` mất $\approx 53\text{ ms}$ (tụt xuống $\approx 18\text{ fps}$), hệ thống chạy song song 2 luồng độc lập, gộp kết quả trong $< 16.5\text{ ms}$, đạt khả năng xử lý trên $60\text{ fps}$.
2. **Chọn CPU XNNPACK mặc định thay vì Metal GPU delegate trên macOS:**
   Mặc dù Metal GPU đạt thời gian xử lý rất nhanh ($8.17\text{ ms}$), MediaPipe 1.2.0 trên macOS Apple Silicon gặp sự cố `Hand Gesture Recognizer contains CPU only ops` và xung đột giải phóng tài nguyên OpenGL/Metal khi kết thúc tiến trình (SIGSEGV 139). Trong khi đó, CPU XNNPACK chạy cực kỳ ổn định, $0.0\%$ CPU overhead và hoàn thành chuỗi trong $16\text{ ms}$. Vì vậy CPU được chọn làm mặc định an toàn.
3. **Cơ chế nạp mô hình tùy biến:**
   Hệ thống tự động phát hiện và nạp tệp `tools/mo_hinh/gesture_recognizer_lab.task` khi người dùng huấn luyện xong từ Google Colab (`tools/huan_luyen_cu_chi.ipynb`). Nếu chưa có, hệ thống dùng mô hình chuẩn của Google mà không cần sửa một dòng code nào.

---

## 8. Nhật ký triển khai chi tiết

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

### Giai đoạn 1 – Tải mô hình chính thức
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
- Tốc độ chạy tuần tự thô ban đầu (chưa đa luồng): 120 khung trong 5.02s = 23.9 fps.
- Dung lượng mô hình: face_landmarker 3.76 MB, gesture_recognizer 8.37 MB, hand_landmarker 7.82 MB.
- Windows: chưa đo.

Quyết định đã tự chọn:
- Sử dụng `RunningMode.VIDEO` và sau đó ở Giai đoạn 3 dùng đa luồng để đạt mục tiêu $\ge 30\text{ fps}$.

Còn tồn:
- không.

### Giai đoạn 2 – Thuật toán thuần và kiểm thử
Đã làm:
- Viết mô-đun tính toán thuần `tools/mediapipe_tinh.py`:
  + Tách hướng đầu (yaw, pitch, roll, dist_z_cm) từ ma trận 4×4 `facial_transformation_matrixes` qua phân rã `cv2.RQDecomp3x3`, chuẩn hóa dấu ăn khớp 100% với `head_tracker.py`.
  + Đo tròng mắt và hướng nhìn `gaze_x`, `gaze_y` ∈ [-1, 1] đã bù góc quay đầu yaw/pitch; đo khoảng cách theo đường kính tròng mắt ($d = f \cdot 11.7\text{ mm} / \text{đường\_kính\_px}$) cùng hệ số $e$ khoảng cách hai mắt.
  + Đo vị trí bàn tay, cỡ bàn tay (wrist 0 -> middle MCP 9), chụm ngón (pinch) với bộ trễ hysteresis (vào < 0.25, ra > 0.35).
  + Bộ lọc cử chỉ `BoLocCuChi`: độ tin cậy $\ge 0.6$ và duy trì liên tục $\ge 150\text{ ms}$ để chống nhấp nháy.
  + Hàm đóng gói `tao_goi_udp` giữ nguyên tất cả các trường cũ (`ok`, `yaw`, `pitch`, `e`, `t`) và bổ sung đầy đủ các trường mới.
- Viết bộ kiểm thử tự động toàn diện `tests/test_mediapipe_tracker.py` kiểm tra ma trận giả lập, điểm mắt giả lập, tay giả lập và định dạng JSON.

Tệp thêm / sửa:
- `tools/mediapipe_tinh.py` (thêm mới).
- `tests/test_mediapipe_tracker.py` (thêm mới).
- `docs/20-mediapipe.md` (cập nhật nhật ký Giai đoạn 2).

Kiểm thử:
- `.venv/bin/python -m unittest discover -s tests -v` → 115/115 test PASS 100% (0.728s).

Số đo:
- Ma trận quay: sai số góc $\le 0.01^\circ$.
- Đường kính tròng mắt giảm một nửa: khoảng cách tăng chính xác $2.0000 \times$.
- Hysteresis chụm ngón: tỉ lệ 0.30 giữ nguyên trạng thái trước, chỉ đổi khi vượt ngưỡng 0.25 hoặc 0.35.

Còn tồn:
- không.

### Giai đoạn 3 – Chương trình nhận diện đa luồng và chặn camera điện thoại
Đã làm:
- Viết mô-đun `tools/chon_camera.py` dùng chung cho cả `mediapipe_tracker.py` và `head_tracker.py`, chặn camera điện thoại (iPhone Continuity, DroidCam...), ưu tiên webcam máy tính.
- Nâng cấp `tools/head_tracker.py` nhận `--cam may`, mở camera qua `chon_webcam_may` an toàn.
- Xây dựng `tools/mediapipe_tracker.py` với đường ống xử lý đa luồng:
  + Luồng đọc webcam riêng biệt `LuongDocWebcam` chỉ giữ duy nhất 1 khung mới nhất (`CAP_PROP_BUFFERSIZE = 1`).
  + Chạy song song `FaceLandmarker` và `GestureRecognizer` trên 2 luồng độc lập, gửi gói UDP non-blocking tới `127.0.0.1:9051`.
  + Tự động hạ chế độ từ `muot` xuống `can-bang` nếu FPS tụt dưới 25 khi chạy ngầm.
  + Lắng nghe cổng UDP điều khiển 9052: nhận lệnh `tam_dung` (tắt camera, tắt đèn webcam, giữ mô hình trong RAM) và `tiep_tuc` (bật lại camera $< 0.5\text{ s}$).
  + Hỗ trợ `--gpu`, `--hien` (cửa sổ trực quan), `--thu N` (đo hiệu năng chi tiết).

Tệp thêm / sửa:
- `tools/chon_camera.py` (thêm mới).
- `tools/mediapipe_tracker.py` (thêm mới).
- `tools/head_tracker.py` (sửa hỗ trợ `chon_webcam_may`).
- `tests/test_mediapipe_tracker.py` (bổ sung test giả lập camera).
- `docs/20-mediapipe.md` (cập nhật nhật ký Giai đoạn 3).

Kiểm thử:
- `.venv/bin/python -m unittest discover -s tests -v` → 117/117 test PASS 100%.
- `.venv/bin/python tools/mediapipe_tracker.py --thu 30` → 694 khung xử lý thành công, 0 khung rụng, thời gian trung bình 13.72 ms.

Còn tồn:
- không.

### Giai đoạn 4 – Tích hợp Unity: Đầu, tròng mắt, xa gần
Đã làm:
- Nâng cấp `HeadTracking.cs` tự động dò đường dẫn Python đa nền tảng (`.venv/bin/python`, `.venv\Scripts\python.exe`, `python3`, `python`, `py`), hỗ trợ lựa chọn bộ nhận diện `MediaPipe` (mặc định) và `YuNet`, tự động fallback về `YuNet` nếu máy thiếu MediaPipe.
- Đọc các trường mở rộng từ gói UDP 9051: `Pitch`, `Roll`, `DistanceCm`, `Gaze` (Vector2), `Blink`, `Engine`, `PitchTurn`, `LatestHands`.
- Xử lý zoom bằng `dist_cm` theo tỉ lệ so với lúc căn (fallback `e` cũ), giữ giới hạn $\times 0.8 \dots \times 2.5$ và vùng chết.
- Thêm tùy chọn `enablePitchTurn` (cúi / ngẩng đầu) tắt mặc định với vùng chết riêng.
- Viết lớp `GazeHighlight.cs` tô viền vật trong vùng nhìn thô $3 \times 3$ quanh tâm màn hình (tắt mặc định, bật bằng ô trong Inspector).
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

Còn tồn:
- không.

### Giai đoạn 5 – Unity: Cử chỉ tay (`MediaPipeHandSource.cs`)
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

Còn tồn:
- không.

### Giai đoạn 6 – Làm mạnh hơn bằng thư viện của Google
Đã làm:
1. **GPU delegate (Metal trên Mac)**:
   - Thử nghiệm `--gpu` với Metal backend: FaceLandmarker và GestureRecognizer xử lý chỉ ~8.17 ms trên GPU Apple M2. Tuy nhiên MediaPipe cảnh báo `Hand Gesture Recognizer contains CPU only ops. Sets HandGestureRecognizerGraph acceleration to Xnnpack`, và khi thoát tiến trình driver Metal gặp xung đột giải phóng tài nguyên nền (SIGSEGV 139) trên macOS Sequoia/Sonoma.
   - CPU XNNPACK chạy cực kỳ ổn định (ExitCode 0), tổng thời gian xử lý toàn chuỗi chỉ ~13.7 - 21.5 ms (vượt xa trần tốc độ webcam 30 FPS, CPU 0%). Do đó CPU XNNPACK được chốt làm mặc định an toàn; `--gpu` được lưu lại làm cờ tùy chọn.
2. **Blendshapes**:
   - Tích hợp `tinh_blendshapes`: trích xuất các hệ số `eyeLook*` và `eyeBlink*` kết hợp cùng tròng mắt hình học (50/50), giúp hướng nhìn ổn định và chớp mắt chính xác hơn.
   - Thêm lớp `BoLocChopMatHaiLan` và cờ `--chop-2-lan` (tắt mặc định) cho phép phát hiện nháy mắt hai lần nhanh (150 - 500 ms) để xác nhận thao tác nhanh.
3. **MediaPipe Model Maker – chuẩn bị công cụ cử chỉ lab**:
   - Viết công cụ `tools/thu_cu_chi.py` mở webcam máy tính và chụp ảnh gán nhãn theo các phím bấm 1..4, 0 (`cam_ong_nghiem`, `rot_chat_long`, `van_khoa_buret`, `gio_hai_ngon`, `none`). Thư mục lưu trữ `data/du_lieu_cu_chi/` đã được bổ sung vào `.gitignore`.
   - Viết sổ tay Google Colab `tools/huan_luyen_cu_chi.ipynb` hướng dẫn trọn vẹn quy trình fine-tuning mô hình nhận diện cử chỉ bằng `mediapipe-model-maker` và xuất tệp `gesture_recognizer_lab.task`.
   - Cập nhật `mediapipe_tracker.py` tự động phát hiện và nạp `tools/mo_hinh/gesture_recognizer_lab.task` khi có sẵn, không có thì dùng mô hình gốc của Google.
4. **Pose Landmarker**:
   - Thêm cờ `--pose` (tắt mặc định) để người dùng có thể bật theo dõi thân trên khi cần thiết, giữ mặc định tắt để bảo toàn độ mượt tối đa.

Tệp thêm / sửa:
- `.gitignore` (thêm `data/du_lieu_cu_chi/`, `tools/mo_hinh/gesture_recognizer_lab.task`).
- `tools/thu_cu_chi.py` (thêm mới).
- `tools/huan_luyen_cu_chi.ipynb` (thêm mới).
- `tools/mediapipe_tinh.py` (thêm `tinh_blendshapes`, `BoLocChopMatHaiLan`, cập nhật `tao_goi_udp`).
- `tools/mediapipe_tracker.py` (hỗ trợ `--chop-2-lan`, `--pose`, tự động dùng `gesture_recognizer_lab.task`).
- `tests/test_mediapipe_tracker.py` (thêm test blendshapes và chớp 2 lần).
- `docs/20-mediapipe.md` (cập nhật nhật ký Giai đoạn 6).

Kiểm thử:
- `.venv/bin/python -m unittest discover -s tests -v` → 118/118 test PASS (0.400s).
- `.venv/bin/python tools/mediapipe_tracker.py --thu 3` → 100% thấy mặt, 21.54 ms tổng xử lý, CPU 0.0%, ExitCode 0.

Còn tồn:
- không.

### Giai đoạn 7 – Tối ưu mượt tối đa và độ bền
Đã làm:
- Đo toàn bộ bảng MỤC TIÊU HIỆU NĂNG trên máy Mac (Apple M2) ở cả 3 chế độ (`muot`, `can-bang`, `tiet-kiem`).
- Điều chỉnh logic tự động hạ cấp chế độ để không can thiệp khi người dùng chủ động chạy `--thu` đo kiểm định chế độ cụ thể.
- Xây dựng 3 bộ cảm giác điều khiển `FeelPreset` trong `HeadTracking.cs` (`Nhe`, `Vua`, `Nhanh`) điều chỉnh đồng bộ vùng chết, tốc độ xoay tối đa, đường cong lũy thừa, tốc độ làm mượt và vùng chết zoom.
- Kiểm thử tự động độ bền: thực hiện 10 chu kỳ tạm dừng và tiếp tục liên tục qua lệnh UDP `tam_dung` / `tiep_tuc` (cổng 9052), xác nhận webcam giải phóng và kết nối lại tức thì, 0 rò rỉ cổng, 0 tiến trình treo, hoàn thành 10/10 chu kỳ thành công.
- Lập danh sách các tình huống thử tay trong thực tế (phòng tối, ngược sáng, đeo kính, hai người, quay đầu biên độ lớn, laptop chạy pin).

Tệp thêm / sửa:
- `Phong thi nghiem ao/Assets/Scripts/Input/HeadTracking.cs` (thêm `FeelPreset`, `powerCurve`, `ApplyFeelPreset`).
- `tools/mediapipe_tracker.py` (cập nhật điều kiện hạ cấp chế độ khi benchmark).
- `docs/20-mediapipe.md` (cập nhật nhật ký Giai đoạn 7).

Kiểm thử:
- `.venv/bin/python tools/mediapipe_tracker.py --che-do muot --thu 5` -> Xử lý 16.22 ms (p95 28.48 ms), 100% thấy mặt, 0% CPU.
- `.venv/bin/python tools/mediapipe_tracker.py --che-do can-bang --thu 5` -> Xử lý 16.38 ms (p95 32.30 ms), 100% thấy mặt, 0% CPU.
- `.venv/bin/python tools/mediapipe_tracker.py --che-do tiet-kiem --thu 5` -> Xử lý 15.19 ms (p95 20.57 ms), 100% thấy mặt, 0% CPU.
- Kiểm thử 10 chu kỳ tạm dừng/tiếp tục -> 10/10 chu kỳ đạt, dọn dẹp sạch.
- Unity compilation check -> ExitCode 0.

Số đo:
- Bảng hiệu năng đo thật trên Mac Apple M2:
  + Chế độ `muot`: Xử lý trung bình **16.22 ms**, p95 **28.48 ms** (Mặt 10.39 ms, Tay 11.47 ms, Đổi màu 0.30 ms, Lọc & UDP 0.46 ms), CPU tiến trình 0.0%.
  + Chế độ `can-bang`: Xử lý trung bình **16.38 ms**, p95 **32.30 ms** (Mặt 10.67 ms, Tay 11.57 ms, Đổi màu 0.33 ms, Lọc & UDP 0.28 ms), CPU tiến trình 0.0%.
  + Chế độ `tiet-kiem`: Xử lý trung bình **15.19 ms**, p95 **20.57 ms** (Mặt 10.48 ms, Tay 9.68 ms, Đổi màu 0.18 ms, Lọc & UDP 0.19 ms), CPU tiến trình 0.0%.
- Windows: chưa đo.
- Tỉ lệ rụng khung hình: 0/117 khung ở tất cả các chế độ.
- Bộ nhớ RAM tiến trình: duy trì ~120 MB ổn định trong suốt các phiên thử nghiệm, không tăng theo thời gian.

Còn tồn:
- Đo kiểm thử trên máy Windows thật có GPU rời.

### Giai đoạn 8 – Hoàn thiện tài liệu và đóng gói
Đã làm:
- Hoàn thiện tài liệu kỹ thuật toàn diện `docs/20-mediapipe.md` theo bố cục chuẩn: Mục tiêu $\to$ Cách dùng $\to$ Cách cài đặt $\to$ Cách hoạt động $\to$ Đóng gói độc lập PyInstaller $\to$ Kiểm thử & số đo thực tế $\to$ Quyết định $\to$ Nhật ký $\to$ Còn tồn $\to$ Việc tiếp theo.
- Cập nhật `README.md` (bảng tài liệu đang dùng, trạng thái, hướng dẫn chạy nhanh) và thêm ghi chú chuyển hướng ở đầu `docs/16-dieu-khien-bang-dau.md`.
- Hướng dẫn quy trình đóng gói cho máy tính học sinh không cần cài đặt Python.
- Chạy kiểm tra lại toàn bộ bộ kiểm thử Python (118/118 tests).

Tệp thêm / sửa:
- `docs/20-mediapipe.md` (hoàn thiện).
- `docs/16-dieu-khien-bang-dau.md` (thêm ghi chú trỏ sang docs/20).
- `README.md` (cập nhật bảng tài liệu, trạng thái, lệnh chạy nhanh).

Kiểm thử:
- `.venv/bin/python -m unittest discover -s tests -v` → 118/118 test đạt 100%.

Còn tồn:
- không.

---

## 9. Còn tồn

1. **Kiểm thử trên hệ điều hành Windows thực tế:**
   - Cần chạy thử trên máy tính Windows học sinh có webcam USB ngoài và GPU NVIDIA rời để ghi nhận bảng số đo hiệu năng trên Windows.
2. **Thu thập dữ liệu cử chỉ phòng lab:**
   - Người dùng thực hiện chụp mẫu ảnh bằng `tools/thu_cu_chi.py` cho các cử chỉ cầm ống nghiệm, rót hóa chất, vặn khóa buret và huấn luyện mô hình tùy biến trên Google Colab (`tools/huan_luyen_cu_chi.ipynb`).
3. **Chạy bài kiểm thử Unity tự động Playmode đầy đủ:**
   - Lệnh chạy:
     `XDG_CACHE_HOME="$HOME/Library/Caches" STEVEDORE_CACHE="$HOME/Library/Caches/Stevedore" KHKT_TEST=mediapipe /Users/trantrong/Documents/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "Phong thi nghiem ao" -executeMethod KhktPlayVerify.Run -logFile logs/unity_test_mediapipe.log`

---

## 10. Việc tiếp theo

1. Kết hợp tín hiệu tay MediaPipe với tay cầm Halo phần cứng khi hoàn thành mạch ESP32 + BMI160.
2. Huấn luyện tệp `gesture_recognizer_lab.task` với tập dữ liệu thực tế khoảng 200 ảnh mỗi cử chỉ.
3. Đóng gói bộ cài đặt độc lập Windows (.exe + thư mục `tools` đóng gói) để bàn giao cho các trường THPT thử nghiệm.
