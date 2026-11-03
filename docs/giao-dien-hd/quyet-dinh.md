# NHẬT KÝ QUYẾT ĐỊNH THIẾT KẾ GIAO DIỆN IPAD (VERA LAB HD)

Tài liệu ghi lại các quyết định kỹ thuật và thiết kế theo Quy tắc 0.1 ("Khi phân vân: chọn phương án an toàn nhất và hoàn tác được, ghi vào quyet-dinh.md").

---

### Quyết định 1: Giữ nguyên `UnityEngine.UI.Text` thay vì chuyển sang TextMeshPro
- **Bối cảnh:** Prompt nêu rõ "Giữ `UnityEngine.UI.Text`, không đổi toàn bộ sang TextMeshPro vì quá rủi ro cho một đêm".
- **Lý do:** Khối lượng code giao diện uGUI trong dự án hiện trải rộng qua hàng chục file (`TouchHud`, `ModernUI`, `IntroScreen`, `ChemSearchUI`, `RobotChatUI`, `FabricationUI`, `SearchKiosk`, `ToolKiosk`, `InstrumentScreen` + 14 `*Program`, `MicroscopeController`, `MolecularTools`, `GeneCloningStation`). Đổi sang TMP sẽ gây vỡ layout và rủi ro compile.
- **Giải pháp:** Sử dụng phông `Be Vietnam Pro` dạng TrueType (.ttf) nạp qua `Font.CreateDynamicFontFromOSFont` hoặc Resources, cấu hình `font.fontSize` và căn dòng pixel-perfect.

### Quyết định 2: Tách biệt hoàn toàn lớp hiệu ứng bấm `NutHieuUng` khỏi `Image.color` của nút gốc
- **Bối cảnh:** Mã nguồn hiện tại đổi màu trực tiếp `mainActionBg.color` và `shiftBg.color` để báo trạng thái thao tác (rót, mở nắp, chuẩn độ, xoay). Nếu hiệu ứng nhấn đè lên `Image.color`, hai bên sẽ triệt tiêu hoặc làm mất trạng thái logic.
- **Lý do:** Tuân thủ Luật 1.3: "Không đổi RectTransform gốc nhận chạm... Không ghi đè Image.color của nút gốc. Code đang đặt màu trực tiếp để báo trạng thái. Hiệu ứng nhấn đặt ở một lớp phủ riêng".
- **Giải pháp:** `NutHieuUng` sinh một GameObject con `Visual` hoặc lớp phủ `HighlightOverlay` có `raycastTarget = false`, scale 0.96 và tăng độ sáng nhẹ khi ngón tay chạm, nhả tức thì khi `OnPointerUp` hoặc `OnPointerExit`.

### Quyết định 3: Không dùng Haptic Feedback rung trên iPad
- **Bối cảnh:** `HapticFeedback.cs` chỉ hỗ trợ bộ rung tay cầm VR. iPad không trang bị Taptic Engine như iPhone.
- **Giải pháp:** Phản hồi xúc giác trên iPad hoàn toàn thông qua phản hồi hình ảnh (state layer / scale nhẹ) và âm thanh click ngắn, tinh tế tích hợp từ `BioFeedback` (`PlayButton`, `PlayClick`, `PlayPop`).
