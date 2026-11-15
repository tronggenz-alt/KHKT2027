# BÁO CÁO KẾT QUẢ LÀM LẠI GIAO DIỆN VERA LAB — NHÁNH `wave6-mac`

> [!IMPORTANT]
> - Đã **dừng toàn bộ chỉnh sửa** trong `~/AppBuild/VERALab_ui` (bản sao xuất iPad không nằm trong Git).
> - Toàn bộ công việc đã được đưa về dự án chính `/Users/trantrong/Developer/Antigravity` trên nhánh mới **`wave6-mac`** (tách từ `4f10ca11`). **Không commit và không push lên `main`**.
> - Tuân thủ nghiêm ngặt `AGENTS.md`: ưu tiên nền tảng **1. macOS trước** (chuột + bàn phím, chạy trong Unity Editor macOS) → **2. Windows thứ hai** (không dùng đường dẫn cứng `/tmp/...` hay API chỉ có trên Mac) → **3. iPad cuối cùng**.
> - Đã gộp đầy đủ **cả 3 cụm thay đổi (1 + 2 + 3)** vào `Phong thi nghiem ao/Assets/Scripts/Demo/HandController.cs` theo phê duyệt và đạt **67 / 67 PASS (`failures=0, runtimeErrors=0`)** trên Unity 6 macOS.

---

## 1. Danh sách các file đã thêm mới và cập nhật (38 script C# + 5 `.meta`)

### 1.1. File thêm mới (5 script C# + 5 `.meta`)
1. `Phong thi nghiem ao/Assets/Scripts/Demo/UiTheme.cs`: Quản lý 3 nền giao diện (`XanhMuc`, `Trang`, `Den`), cỡ chữ (`Bình thường`, `Lớn 115%`, `Rất lớn 130%`), cỡ bảng dụng cụ (`60%`, `75%`, `Kín màn hình`), và chế độ bấm dụng cụ (`Cầm ngay` / `Mở bảng`), lưu qua `PlayerPrefs` và phát sự kiện `Changed`.
2. `Phong thi nghiem ao/Assets/Scripts/Demo/ToolPreview.cs`: Camera phụ chụp trực tiếp vật thể 3D trong phòng lab ra `RenderTexture` 512×512 để hiển thị trong bảng chi tiết dụng cụ và bảng máy.
3. `Phong thi nghiem ao/Assets/Scripts/Demo/ToolPanel.cs`: Bảng chi tiết nổi 1660×940 cho Buret (soi mặt khum, thanh trượt độ mở khóa, 6 nút thao tác, đồ thị chuẩn độ trực tiếp), Chai hóa chất (thẻ nhãn, chọn bình nhận, thanh lượng rót, thẻ pha loãng $C_1 \cdot V_1 = C_2 \cdot V_2$, dự đoán trộn cô lập qua `ChemEngine`), Bình chứa và Dụng cụ chung. Kết nối đồng bộ với `HandController.PickFromPanel` và `HandController.ReturnToHomeFromPanel`.
4. `Phong thi nghiem ao/Assets/Scripts/Editor/KhktWave6Verify.cs`: Điểm vào `-executeMethod KhktWave6Verify.Run` mở cảnh `PhongThiNghiem.unity` và chạy kiểm thử tự động trong Unity Editor.
5. `Phong thi nghiem ao/Assets/Scripts/Demo/KhktWave6Runner.cs`: Bộ kiểm thử tự động PlayMode cho toàn bộ các mục giao diện, dùng `Path.Combine(Path.GetTempPath(), "vera_w6_shots")` tương thích đa nền tảng Mac & Windows.

### 1.2. File cập nhật theo nhóm chức năng (33 script C#)
- **Điều khiển tay & Tương tác 3D**:
  - `Phong thi nghiem ao/Assets/Scripts/Demo/HandController.cs`: Đã gộp cả 3 cụm thay đổi đã duyệt:
    - **Cụm 1**: Mở `ToolPanel` bằng nhấn giữ 0,4 giây (ở chế độ `Cầm ngay`) hoặc bấm đơn (ở chế độ `Mở bảng`), chặn click xuyên qua phần tử UI (`pointerOnUi`), cập nhật gợi ý phím (`Hints`).
    - **Cụm 2**: Hai hàm `PickFromPanel(GameObject)` và `ReturnToHomeFromPanel(GameObject)` để `ToolPanel` gọi đồng bộ và làm sạch trạng thái cảnh báo an toàn khi cầm vật.
    - **Cụm 3**: Cổng cảnh báo an toàn `TryGateUnsafeAction` và `EvaluateSafetyRisk` chặn 4 nhóm thao tác nguy hiểm (sục khí độc ngoài tủ hút, đổ nước/dung dịch loãng vào $\text{H}_2\text{SO}_4$ đặc > 6 M, trộn sinh khí độc $\text{Cl}_2$/$\text{HCN}$ ngoài tủ hút, cho kim loại kiềm/chất rắn phản ứng mạnh tiếp xúc với nước) và hiển thị hộp thoại `ModernUI.ShowSafetyWarning`.
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
| **Số mục đạt (`passes`)** | **67 / 67** | **`[W6_RESULT] ALL PASSED`** |
| **Số mục chưa đạt (`failures`)** | **0 / 67** | Đạt tuyệt đối 100% các bài kiểm thử |
| **Ảnh chụp nghiệm thu (`shots`)** | **12 / 12** | Lưu tại `${TMPDIR}/vera_w6_shots/` |

### Chi tiết toàn bộ 67 mục PASS
- **Cài đặt `IntroScreen` (Mục 7.8)**: Mở/đóng bảng Cài đặt, hiển thị chip `Cỡ chữ` & `Cỡ bảng dụng cụ` ≥ 44 pt (`hFont=44, hPanel=44`), cập nhật `UiTheme`, không chồng lấn.
- **Tương tác `HandController` ở cả 2 chế độ (`Mode 0` & `Mode 1`)**:
  - `Mode 0 (Cầm ngay)`: Ngắm bình nón (`Act.Pick`), bấm nhanh cầm bình, đặt dưới buret (`Act.Swirl`), giữ để lắc, nút `(i)` trên thẻ trỏ mở `ToolPanel`, nhấn giữ 0,4 giây mở `ToolPanel` (`dt=0.42s`).
  - `Mode 1 (Mở bảng)`: Bấm chuột đơn vào lọ hóa chất mở `ToolPanel`, bấm `Cầm lên` trong bảng để cầm lọ; bấm chuột đơn vào pipet mở `ToolPanel`, bấm `Cầm lên` trong bảng để cầm pipet.
- **Chuẩn độ & Thẻ chấm điểm 4 tiêu chí (Mục 7.5)**: Ghi nhận `V đầu = 0.3 mL`, hoàn thành chuẩn độ (`err=3.00%`), hiện thẻ kết quả 4 tiêu chí (`rows=4`).
- **`ToolPanel` Chai hóa chất & Buret (Mục 7.1, 7.2, 7.6, 7.8)**:
  - Hiển thị 3 dòng dự đoán khi trộn (`textFields=6`), chạy mô phỏng `ChemEngine` trên bản sao cô lập không làm biến đổi bình thật.
  - Tính công thức pha loãng chuẩn xác: `Lấy V₁ = 62,5 mL gốc + V_nước = 187,5 mL nước cất → 250 mL dung dịch 0,025 M`; nút `Áp dụng V₁` cập nhật thanh trượt lượng rót (`before=5, after=62`).
  - Nút `Hỏi trợ lý` mở `RobotChatUI` nổi lên trên `ToolPanel` (`tpSib=18, chatSib=19`); nhấn `Esc` đóng `RobotChatUI` trước mà vẫn giữ `ToolPanel` mở.
  - Co giãn `ToolPanel` ở mức `60%` (`scale=0.880, size=1460.4x827.0`) và `Kín màn hình` (`scale=1.120, size=1859.6x1053.0 ≤ 1920×1080`), đóng sạch với `UiCapture=0`.
- **Cổng cảnh báo an toàn trước thao tác nguy hiểm (Mục 7.5)**: `HandController.TryGateUnsafeAction` chặn thao tác đổ nước vào $\text{H}_2\text{SO}_4$ đặc, mở hộp thoại `ModernUI.ShowSafetyWarning` (`UiCapture > 0`), nút `Đổi cách làm` hủy thao tác an toàn, nút `Vẫn làm` thực thi `onProceed` và giải phóng `UiCapture=0`.
- **14/14 chương trình máy phân tích (`InstrumentScreen`)**: Cả 14 lớp (`HplcProgram`, `UvVisProgram`, `DistillerProgram`, `AutoclaveProgram`, `AasProgram`, `CalorimeterProgram`, `SemProgram`, `XrdProgram`, `TitratorProgram`, `FtirProgram`, `DryingOvenProgram`, `FurnaceProgram`, `NmrProgram`, `GcMsProgram`) mở dưới dạng bảng nổi (`scale=0.865`), camera phòng lab vẫn bật (`cam=True`), đủ 5 vùng, cỡ chữ hiệu dụng ≥ 12 pt; nút `Hỏi trợ lý` + phím `Esc` đóng `RobotChatUI` trước mà không đóng `InstrumentScreen`.
- **Bảng máy ly tâm (`MachinePanel` — Mục 7.3)**: Mở `CentrifugeLoad`, hiển thị `×g` trực tiếp, nút đổi đơn vị `vòng/phút ↔ ×g` quy đổi thanh trượt (`rpm=6000, xg=2616`), áp dụng chương trình dựng sẵn `Tủa ADN` và nút `Lưu mới` tạo chip `Tùy chỉnh 1`.
- **Kính hiển vi (`MicroscopeUI` — Mục 7.4)**: Nạp tiêu bản `bieu_bi_hanh` ở `400×`, bấm `Chụp ảnh` tăng bộ đếm `Ảnh đã chụp (1)`, bấm `Thêm vào báo cáo` lưu vào `MicroscopeReportStore` (`count=1`), mở lớp phủ thư viện ảnh với thumbnail `Texture2D`.
- **Màn hình chính & Dashboard (`ManHinhChinh` — Mục 7.7)**: Mở cảnh `ManHinhChinh`, bấm `Dùng thử` vào thẳng `DashboardShell`, hiển thị thẻ `Bài đang làm dở` trung thực (`hits=2`), trang `Bài tập` có 6 thẻ bài tập VERA (`cards=6`, nút ≥ 44 pt), trang `Cải thiện` có thông báo trống trung thực và 3 thẻ rèn kỹ năng cốt lõi (`skills=3`).

---

## 3. Trạng thái nhánh Git & Bước tiếp theo
- Toàn bộ thay đổi đã được commit gọn gàng trên nhánh **`wave6-mac`** tại dự án chính `/Users/trantrong/Developer/Antigravity` (**không commit, không push lên `main`**).
- Dự án trên nhánh `wave6-mac` đã đạt **67/67 PASS** trên macOS và sẵn sàng để dùng trực tiếp trong Unity Editor trên Mac hoặc kiểm thử tiếp trên Windows và iPad theo lộ trình trong `AGENTS.md`.
