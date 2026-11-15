# BÁO CÁO KẾT QUẢ LÀM LẠI GIAO DIỆN VERA LAB — NHÁNH `wave6-mac`

> [!IMPORTANT]
> - Đã **dừng toàn bộ chỉnh sửa** trong `~/AppBuild/VERALab_ui` (bản sao xuất iPad không nằm trong Git).
> - Toàn bộ công việc đã được đưa về dự án chính `/Users/trantrong/Developer/Antigravity` trên nhánh mới **`wave6-mac`** (tách từ `4f10ca11`). **Không commit và không push lên `main`**.
> - Tuân thủ nghiêm ngặt `AGENTS.md`: ưu tiên nền tảng **1. macOS trước** (chuột + bàn phím, chạy trong Unity Editor macOS) → **2. Windows thứ hai** (không dùng đường dẫn cứng `/tmp/...` hay API chỉ có trên Mac) → **3. iPad cuối cùng**.
> - File `Phong thi nghiem ao/Assets/Scripts/Demo/HandController.cs` được **giữ nguyên 100%** (0 dòng thay đổi), kèm bảng đối chiếu chi tiết ở **Mục 3** để duyệt trước khi gộp.

---

## 1. Danh sách các file đã thêm mới và cập nhật (37 script C# + 5 `.meta`)

### 1.1. File thêm mới (5 script C# + 5 `.meta`)
1. `Phong thi nghiem ao/Assets/Scripts/Demo/UiTheme.cs`: Quản lý 3 nền giao diện (`XanhMuc`, `Trang`, `Den`), cỡ chữ (`Bình thường`, `Lớn 115%`, `Rất lớn 130%`), cỡ bảng dụng cụ (`60%`, `75%`, `Kín màn hình`), và chế độ bấm dụng cụ (`Cầm ngay` / `Mở bảng`), lưu qua `PlayerPrefs` và phát sự kiện `Changed`.
2. `Phong thi nghiem ao/Assets/Scripts/Demo/ToolPreview.cs`: Camera phụ chụp trực tiếp vật thể 3D trong phòng lab ra `RenderTexture` 512×512 để hiển thị trong bảng chi tiết dụng cụ và bảng máy.
3. `Phong thi nghiem ao/Assets/Scripts/Demo/ToolPanel.cs`: Bảng chi tiết nổi 1660×940 cho Buret (soi mặt khum, thanh trượt độ mở khóa, 6 nút thao tác, đồ thị chuẩn độ trực tiếp), Chai hóa chất (thẻ nhãn, chọn bình nhận, thanh lượng rót, thẻ pha loãng $C_1 \cdot V_1 = C_2 \cdot V_2$, dự đoán trộn cô lập qua `ChemEngine`), Bình chứa và Dụng cụ chung. Đã tích hợp hàm nội bộ `ResolveGrabbable`, `PickTarget`, `ReturnTargetHome`, `TryReturnStationaryHome` gọi trực tiếp `HandController.Pick`/`ReturnHome` để không cần sửa `HandController.cs`.
4. `Phong thi nghiem ao/Assets/Scripts/Editor/KhktWave6Verify.cs`: Điểm vào `-executeMethod KhktWave6Verify.Run` mở cảnh `PhongThiNghiem.unity` và chạy kiểm thử tự động trong Unity Editor.
5. `Phong thi nghiem ao/Assets/Scripts/Demo/KhktWave6Runner.cs`: Bộ kiểm thử tự động PlayMode cho toàn bộ các mục giao diện, dùng `Path.Combine(Path.GetTempPath(), "vera_w6_shots")` tương thích đa nền tảng Mac & Windows.

### 1.2. File cập nhật theo nhóm chức năng (32 script C#)
- **Bộ dựng giao diện & Chuyển động**:
  - `Phong thi nghiem ao/Assets/Scripts/Demo/GlassKit.cs`: Hệ màu động theo `UiTheme`, nút chính/nút viền ≥ 44 pt, thanh trượt rãnh 6 px rõ nét, chip, ô số đo (`Readout`), dải báo (`Banner`). Đã khử toán tử `??` trên `UnityEngine.Object`.
  - `Phong thi nghiem ao/Assets/Scripts/Menu/Dashboard/MenuKit.cs`: Bảng màu màn hình chính động theo `UiTheme`.
  - `Phong thi nghiem ao/Assets/Scripts/Demo/GuiKit.cs`, `NutHieuUng.cs`, `UiMotion.cs` (bổ sung kiểm tra `cg != null` / `rt != null` trong `CoFade`, `CoPopup`, `CoDismiss` khi bảng dựng lại theo chủ đề).
- **Bảng máy, Kính hiển vi & Thiết bị sinh học**:
  - `Phong thi nghiem ao/Assets/Scripts/Demo/MachinePanel.cs`: Bảng nổi 1660×940 cho 10 máy phòng lab, hiển thị sơ đồ/ảnh 3D, đổi đơn vị `vòng/phút` ↔ `×g` (RCF), chọn chương trình dựng sẵn và lưu chương trình mới (`Lưu mới`).
  - `Phong thi nghiem ao/Assets/Scripts/Demo/Bio/CentrifugeLoad.cs`: Tích hợp thông số máy ly tâm với `MachinePanel`.
  - `Phong thi nghiem ao/Assets/Scripts/Demo/Bio/MicroscopeUI.cs`: Bảng nổi 1660×940, thị trường tròn đen hoàn toàn với 4 nút góc (`Kéo`, `Thước`, `Chú thích`, `Chụp`), thư viện ảnh đã chụp (`Xem ảnh đã chụp`) và lưu vào báo cáo (`MicroscopeReportStore`).
- **Màn hình máy phân tích (14 chương trình)**:
  - `Phong thi nghiem ao/Assets/Scripts/Demo/Instruments/InstrumentScreen.cs`: Đổi thành bảng nổi trên phòng lab (giữ camera phòng lab bật), tự động đổi màu nền đồ thị theo `UiTheme`, nút `Hỏi trợ lý` và ưu tiên phím `Esc` (`UiBridge.LastCloseFrame != Time.frameCount`).
  - `SchemKit.cs` và 12 tệp chương trình máy trong `Phong thi nghiem ao/Assets/Scripts/Demo/Instruments/`: `AasProgram.cs`, `CalorimeterProgram.cs`, `DistillerProgram.cs`, `FtirProgram.cs`, `GcMsProgram.cs`, `HplcProgram.cs`, `NmrProgram.cs`, `SemProgram.cs`, `TitratorProgram.cs`, `UvVisProgram.cs`, `WeighingProgram.cs`, `XrdProgram.cs`.
- **Màn hình ngoài Lab, Cài đặt, HUD & Thông báo**:
  - `Phong thi nghiem ao/Assets/Scripts/Menu/MainMenu.cs`, `Dashboard/DashboardShell.cs`, `Dashboard/PageOverview.cs`: Màn hình đăng nhập 2 cột, vào thẳng `DashboardShell` khi bấm `Dùng thử không cần tài khoản`, thẻ `Bài đang làm dở` trung thực, trang `Bài tập` (6 bài thực hành VERA) và `Cải thiện` (3 kỹ năng cốt lõi).
  - `Phong thi nghiem ao/Assets/Scripts/Demo/IntroScreen.cs`: Lưới 2×2 thẻ chế độ trong phòng lab và bảng Cài đặt 5 mục (`Giao diện`, `Hiển thị`, `Âm thanh`, `Điều khiển`, `Trợ năng`) kèm hộp thoại xác nhận khi đổi nền.
  - `ModernUI.cs`, `TouchHud.cs`, `LabToasts.cs`, `RobotChatUI.cs`, `ChemSearchUI.cs`, `DemoShowcase.cs`, `FloatingCalloutGroup.cs`, `ReactionAnnotator.cs`, `Assembly/Fabrication/FabricationUI.cs`.

---

## 2. Kết quả chạy thử thực tế trên Mac (Unity 6 `6000.6.7f1`)

Đã chạy kiểm thử tự động trên dự án chính `/Users/trantrong/Developer/Antigravity/Phong thi nghiem ao` bằng lệnh:
```bash
"/Users/trantrong/AppBuild/UnityEditor/Unity.app/Contents/MacOS/Unity" \
  -batchmode -buildTarget StandaloneOSX \
  -projectPath "/Users/trantrong/Developer/Antigravity/Phong thi nghiem ao" \
  -executeMethod KhktWave6Verify.Run \
  -logFile "${TMPDIR%/}/vera_antigravity_w6.log"
```

### Tổng hợp kết quả (`[W6_SUMMARY]`)
| Chỉ số | Kết quả | Ghi chú |
|---|---|---|
| **Lỗi biên dịch C# (`error CS`)** | **0** | Biên dịch sạch toàn bộ dự án |
| **Lỗi Runtime (`runtimeErrors`)** | **0** | Không có ngoại lệ `NullReferenceException` / `MissingReferenceException` |
| **Số mục đạt (`passes`)** | **61 / 67** | Đạt 100% các mục ngoài `HandController.cs` |
| **Số mục chưa đạt (`failures`)** | **6 / 67** | Cả 6 mục đều thuộc các nhánh code nằm trong `HandController.cs` đang giữ nguyên chờ duyệt |
| **Ảnh chụp nghiệm thu (`shots`)** | **12 / 12** | Lưu tại `${TMPDIR}/vera_w6_shots/` |

### Chi tiết 61 mục PASS
- **Cài đặt `IntroScreen` (Mục 7.8)**: Mở/đóng bảng Cài đặt, hiển thị chip `Cỡ chữ` & `Cỡ bảng dụng cụ` ≥ 44 pt (`hFont=44, hPanel=44`), cập nhật `UiTheme`, không chồng lấn.
- **Chuẩn độ & Thẻ chấm điểm 4 tiêu chí (Mục 7.5)**: Ngắm bình nón (`Act.Pick`), bấm nhanh cầm bình, đặt dưới buret (`Act.Swirl`), giữ để lắc, nút `(i)` trên thẻ trỏ mở `ToolPanel`, ghi nhận `V đầu = 0.3 mL`, hoàn thành chuẩn độ (`sai số = 2.21%`), hiện thẻ kết quả 4 tiêu chí (`rows=4`).
- **`ToolPanel` Chai hóa chất & Buret (Mục 7.1, 7.2, 7.6, 7.8)**:
  - Hiển thị 3 dòng dự đoán khi trộn (`textFields=6`), chạy mô phỏng `ChemEngine` trên bản sao cô lập không làm biến đổi bình thật.
  - Tính công thức pha loãng chuẩn xác: `Lấy V₁ = 62,5 mL gốc + V_nước = 187,5 mL nước cất → 250 mL dung dịch 0,025 M`; nút `Áp dụng V₁` cập nhật thanh trượt lượng rót (`25 → 62 mL`).
  - Nút `Hỏi trợ lý` mở `RobotChatUI` nổi lên trên `ToolPanel` (`tpSib=18, chatSib=19`); nhấn `Esc` đóng `RobotChatUI` trước mà vẫn giữ `ToolPanel` mở.
  - Co giãn `ToolPanel` ở mức `60%` (`scale=0.880, 1460.4×827.0`) và `Kín màn hình` (`scale=1.120, 1859.6×1053.0 ≤ 1920×1080`), đóng sạch với `UiCapture=0`.
- **Hộp thoại cảnh báo an toàn (Mục 7.5)**: `ModernUI.ShowSafetyWarning` chặn tương tác (`UiCapture > 0`), nút `Đổi cách làm` hủy thao tác an toàn, nút `Vẫn làm` thực thi callback và trả `UiCapture=0`.
- **14/14 chương trình máy phân tích (`InstrumentScreen`)**: Cả 14 lớp (`HplcProgram`, `UvVisProgram`, `DistillerProgram`, `AutoclaveProgram`, `AasProgram`, `CalorimeterProgram`, `SemProgram`, `XrdProgram`, `TitratorProgram`, `FtirProgram`, `DryingOvenProgram`, `FurnaceProgram`, `NmrProgram`, `GcMsProgram`) mở dưới dạng bảng nổi (`scale=0.865`), camera phòng lab vẫn bật (`cam=True`), đủ 5 vùng, cỡ chữ hiệu dụng ≥ 12 pt; nút `Hỏi trợ lý` + phím `Esc` đóng `RobotChatUI` trước mà không đóng `InstrumentScreen`.
- **Bảng máy ly tâm (`MachinePanel` — Mục 7.3)**: Mở `CentrifugeLoad`, hiển thị `×g` trực tiếp, nút đổi đơn vị `vòng/phút ↔ ×g` quy đổi thanh trượt (`rpm=6000 ↔ xg=2616`), áp dụng chương trình dựng sẵn `Tủa ADN` và nút `Lưu mới` tạo chip `Tùy chỉnh 1`.
- **Kính hiển vi (`MicroscopeUI` — Mục 7.4)**: Nạp tiêu bản `bieu_bi_hanh` ở `400×`, bấm `Chụp ảnh` tăng bộ đếm `Ảnh đã chụp (1)`, bấm `Thêm vào báo cáo` lưu vào `MicroscopeReportStore` (`count=1`), mở lớp phủ thư viện ảnh với thumbnail `Texture2D`.
- **Màn hình chính & Dashboard (`ManHinhChinh` — Mục 7.7)**: Mở cảnh `ManHinhChinh`, bấm `Dùng thử` vào thẳng `DashboardShell`, hiển thị thẻ `Bài đang làm dở` trung thực, trang `Bài tập` có 6 thẻ bài tập VERA (nút ≥ 44 pt), trang `Cải thiện` có thông báo trống trung thực và 3 thẻ rèn kỹ năng cốt lõi.

### Chi tiết 6 mục FAIL (đều do chưa gộp `HandController.cs` theo yêu cầu)
1. `[W6_FAIL] Mode 0 0.4s hold opens ToolPanel | dt=0.85s`
2. `[W6_FAIL] Mode 1 single click opens bottle ToolPanel`
3. `[W6_FAIL] Mode 1 Cam len picks bottle`
4. `[W6_FAIL] Mode 1 single click opens pipette ToolPanel`
5. `[W6_FAIL] Mode 1 Cam len picks pipette`
6. `[W6_FAIL] 7.5 HandController TryGateUnsafeAction blocked water into conc H2SO4`

---

## 3. So sánh chi tiết `HandController.cs` (Chờ duyệt)

File `Phong thi nghiem ao/Assets/Scripts/Demo/HandController.cs` trong dự án chính hiện có **1087 dòng** (nguyên bản). Bản trong `~/AppBuild/VERALab_ui` có **1492 dòng** (+405 dòng), chia làm **3 cụm thay đổi độc lập**:

### Cụm 1 — Mở `ToolPanel` bằng nhấn giữ 0,4 giây hoặc theo cài đặt `Khi bấm dụng cụ`, chặn click xuyên bảng UI
- **Vị trí**: Dòng 71–85, 110–166, 167–231 (`Act.Pick`, `Act.Swirl`, `Act.Stopcock`), 299–303, và 1357–1381 (`Hints`).
- **Nội dung**:
  - Thêm biến trạng thái nhấn giữ: `inspectHoldTarget`, `inspectHoldTimer`, `inspectHoldOpened`, `inspectProgressShown` và các hàm `IsOpenPanelOnClick` (đọc `PlayerPrefs` `KHKT_BAM_DUNG_CU`), `GetInspectTarget()`, `CancelInspectHold()`.
  - Trong `Update()`:
    - Mở rộng cờ `uiOpen`: `DesktopViewer.UiCapture > 0 || ToolPanel.IsOpen || MachinePanel.IsOpen || ModernUI.IsSafetyModalOpen`.
    - Thêm `pointerOnUi`: chặn nhấn chuột trái vào thế giới 3D khi con trỏ đang nằm trên phần tử uGUI (`EventSystem.current.IsPointerOverGameObject()`), giúp bấm nút `(i)` hoặc nút trên HUD không bị cầm nhầm vật 3D phía sau.
    - Ở chế độ `Cầm ngay` (`Mode 0`): bấm nhả nhanh (< 0,4s) là cầm dụng cụ lên như cũ; nhấn giữ ≥ 0,4s trên dụng cụ hoặc khóa buret sẽ hiện vòng tiến trình `LabToasts.Progress("Mở chi tiết", ...)` và mở `ToolPanel.OpenObject(...)`.
    - Ở chế độ `Mở bảng` (`Mode 1`): bấm chuột đơn vào dụng cụ hoặc khóa buret sẽ mở `ToolPanel.OpenObject(...)` (sau đó bấm `Cầm lên` trong bảng để cầm).
  - Cập nhật gợi ý phím (`Hints`) ở góc dưới theo chế độ đang chọn.

### Cụm 2 — Hai hàm phụ trợ gọi từ `ToolPanel` (`PickFromPanel` và `ReturnToHomeFromPanel`)
- **Vị trí**: Dòng 704–755 trong bản `AppBuild`.
- **Nội dung**:
  - `public void PickFromPanel(GameObject go)`: Tìm `Grabbable` qua `ApparatusPart.WorldGrab`, nếu tay đang cầm vật khác thì gọi `ReturnHome()`, sau đó gọi `Pick(g)`.
  - `public bool ReturnToHomeFromPanel(GameObject go)`: Nếu vật đang cầm trên tay thì gọi `ReturnHome()`, nếu vật đang đặt trên bàn/giá thì kiểm tra `CanPutDown(g)` + khóa buret rồi đưa về `HomeSocket` hoặc `HomePosition`.
- **Ghi chú**: Hiện tại `ToolPanel.cs` đã có sẵn hàm nội bộ tương đương (`PickTarget` / `ReturnTargetHome` / `TryReturnStationaryHome`), nhưng nếu gộp Cụm 2 vào `HandController.cs` thì luồng xóa `confirmedSafety` khi cầm lại vật sẽ đồng bộ hoàn toàn.

### Cụm 3 — Cổng cảnh báo an toàn trước thao tác nguy hiểm (`TryGateUnsafeAction` — Mục 7.5)
- **Vị trí**: Dòng 32–40, 239–295 (`Act.Pour`, `Act.PipetteDrain`, `Act.Drop`, `Act.Spray`, `Act.Scoop`, `Act.Bubble`), 764–765, và 1016–1191.
- **Nội dung**:
  - Thêm `confirmedSafety` (`HashSet<(GameObject heldObj, LabContainer target, string riskKey)>`) và danh sách `WaterReactiveIds` (`K(s)`, `Na(s)`, `Li(s)`, `Ba(s)`, `Ca(s)`, `CaC2(s)`, `Al4C3(s)`, `NaH(s)`, `LiAlH4(s)`, `NaBH4(s)`).
  - Hàm `TryGateUnsafeAction(...)` và `EvaluateSafetyRisk(...)` kiểm tra 4 tình huống nguy hiểm trước khi thực hiện thao tác:
    1. Sục bình khí độc ngoài tủ hút (`khi_doc_bubble`).
    2. Đổ nước hoặc dung dịch loãng vào axit $\text{H}_2\text{SO}_4$ đặc (> 6 M) gây sôi bắn (`ban_be_h2so4`).
    3. Trộn hóa chất sinh khí cực độc $\text{Cl}_2$ hoặc $\text{HCN}$ ngoài tủ hút (`khi_doc_cl2`, `khi_doc_hcn`).
    4. Xúc kim loại kiềm / chất rắn phản ứng mãnh liệt với nước hoặc xịt nước vào bình chứa chúng (`chay_no_nuoc`).
  - Khi phát hiện nguy cơ, mở hộp thoại `ModernUI.ShowSafetyWarning(...)` với 2 nút `Đổi cách làm` (hủy thao tác và nhắc cách làm an toàn) hoặc `Vẫn làm` (ghi nhớ vào `confirmedSafety` cho vật đang cầm và cho phép tiếp tục).

---

## 4. Việc còn dở & Bước tiếp theo
1. **Duyệt `HandController.cs`**: Chọn các cụm muốn gộp trong 3 cụm của `HandController.cs` ở Mục 3 để đạt **67/67 PASS**.
2. **Thử nghiệm tương tác trực tiếp trên Mac và Windows**: Nhánh `wave6-mac` đã sẵn sàng để mở trực tiếp trong Unity Editor trên macOS (điều khiển chuột + bàn phím) hoặc chuyển sang Windows kiểm thử tiếp theo đúng thứ tự trong `AGENTS.md`.
