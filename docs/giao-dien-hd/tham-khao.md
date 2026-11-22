# TÀI LIỆU THAM KHẢO THIẾT KẾ GIAO DIỆN IPAD (VERA LAB HD)

Tài liệu tổng hợp 16 nguồn tham khảo từ 6 nhóm thiết kế và kỹ thuật chuyên sâu theo yêu cầu của Mục 4 trong đề bài. Mỗi tài liệu ghi rõ liên kết, điều cốt lõi học được và ứng dụng cụ thể vào màn hình nào của VERA Lab.

---

## 1. NỀN TẢNG APPLE (HUMAN INTERFACE GUIDELINES & WWDC)

1. **Apple HIG — Buttons & Touch Targets**
   - *Liên kết:* https://developer.apple.com/design/human-interface-guidelines/buttons
   - *Điều học được:* Vùng chạm tối thiểu trên iPadOS là 44 × 44 pt để tránh chạm trượt ngón tay cái. Nút nhấn cần có phản hồi thị giác ngay lập tức khi ngón tay vừa chạm (`OnPointerDown`), không chờ đến khi nhấc ngón (`OnPointerUp`).
   - *Áp dụng vào:* Toàn bộ nút trong `TouchHud` (nút Tay chính, nút Trả về, Lắc bình, Mắt lên/xuống, bảng Buret) và `GlassKit.MakeButton`.

2. **Apple HIG — Spatial Layout & Safe Area**
   - *Liên kết:* https://developer.apple.com/design/human-interface-guidelines/layout
   - *Điều học được:* Khoảng cách lề an toàn (Safe Area) bảo vệ giao diện khỏi cạnh bo cong của iPad, Home Indicator và Dynamic Island/Camera notch. Tối ưu khu vực ngón cái (Thumb Zone) ở 2 góc dưới màn hình 11" và 12.9".
   - *Áp dụng vào:* `TouchHud.ApplySafeArea`, di chuyển các nút thao tác nhanh về góc dưới hai bên, giữ thông thoáng 60% diện tích trung tâm màn hình để nhìn thao tác 3D.

3. **Apple HIG — Materials & Visual Depth (Materials in iPadOS)**
   - *Liên kết:* https://developer.apple.com/design/human-interface-guidelines/materials
   - *Điều học được:* Kính mờ (Frosted Glass / Materials) phân cấp giao diện thành 3 lớp độ dày: Ultra Thin (lớp phủ menu), Thin (thẻ dụng cụ / thông báo), Regular/Thick (bảng cài đặt / màn hình máy). Khi nền 3D quá sáng, kính cần lớp tối (tint) để giữ độ tương phản chữ.
   - *Áp dụng vào:* `GlassKit.Glass` (vật liệu shader `Khkt/KinhMo`) phân tầng kính mờ tối (Dark Tint) cho môi trường phòng lab mới có độ rọi cao.

4. **WWDC23 — Designing with motion and micro-interactions**
   - *Liên kết:* https://developer.apple.com/videos/play/wwdc2023/10156/
   - *Điều học được:* Chuyển động giao diện cảm ứng cần ngắn (120–220 ms), dùng đường cong giảm tốc (Cubic Ease Out / Spring nhẹ). Nút nhấn co lại nhẹ 4–5% khi giữ ngón tay và bật nảy lại khi nhả.
   - *Áp dụng vào:* Module chuyển động `UiMotion.cs` và `NutHieuUng.cs`.

---

## 2. HỆ THỐNG THIẾT KẾ HIỆN ĐẠI (DESIGN SYSTEMS)

5. **Material Design 3 — State Layers & Interaction States**
   - *Liên kết:* https://m3.material.io/foundations/interaction-states
   - *Điều học được:* Tách biệt trạng thái tương tác (Hover, Pressed, Focused, Dragged) thành một lớp bán trong suốt đè lên trên (`State Layer`) thay vì đổi màu trực tiếp nền nút. Giúp tránh xung đột giữa màu logic (đang rót/chuẩn độ) và màu hiệu ứng nhấn.
   - *Áp dụng vào:* Quyết định 2: Tạo lớp con `HighlightOverlay` trong `NutHieuUng`, giữ nguyên `Image.color` của `mainActionBg` và `shiftBg`.

6. **Fluent 2 Design System — Touch Target Padding vs Visual Size**
   - *Liên kết:* https://fluent2.microsoft.com/
   - *Điều học được:* Kích thước thị giác (Visual Size) của nút có thể nhỏ hơn để thanh thoát, nhưng vùng nhận chạm (Hitbox Size) mở rộng tối thiểu 44×44 pt bằng invisible padding (`raycastTarget` trong suốt).
   - *Áp dụng vào:* Nút mắt lên/xuống (72x52), nút điều khiển buret (44x44) và các nút điều hướng máy phân tích.

7. **Carbon Design System — Data Visualization & Scientific Color Tokens**
   - *Liên kết:* https://carbondesignsystem.com/data-visualization/color-palettes/
   - *Điều học được:* Đồ thị khoa học cần bảng màu phân biệt rõ rệt (Categorical Colors) đạt chuẩn tương phản WCAG AA, không dùng màu neon gây mỏi mắt trên màn hình Liquid Retina.
   - *Áp dụng vào:* Đồ thị pH chuẩn độ trong `ModernUI` và màn hình 14 máy phân tích `InstrumentScreen`.

---

## 3. GIAO DIỆN GAME DI ĐỘNG & 3D (GAME UI DATABASE)

8. **Genshin Impact / Honkai Star Rail Touch HUD Layout Analysis**
   - *Liên kết:* https://www.gameuidatabase.com/gameData.php?id=83
   - *Điều học được:* Bố trí cụm nút hành động chính (Primary Action) hình tròn lớn nổi bật ở góc phải dưới, các nút kỹ năng phụ bố trí dạng cung tròn bán nguyệt theo tầm xoay ngón cái. Không đặt chữ phím PC cứng lên nút cảm ứng trên iPad.
   - *Áp dụng vào:* Tái cấu trúc cụm nút tay chính trong `TouchHud`: nút Tay trung tâm hình tròn Ø 88 pt, các nút Trả về, Lắc bình, Tầm mắt, Chậm/Nhanh bố trí theo đường cong công thái học ngón cái.

9. **The Legend of Zelda: Tears of the Kingdom — Contextual Interaction Prompts**
   - *Liên kết:* https://interfaceingame.com/games/the-legend-of-zelda-tears-of-the-kingdom/
   - *Điều học được:* Thông tin ngữ cảnh (Contextual Prompts) chỉ xuất hiện khi hướng tầm nhìn vào vật thể có thể tương tác, tự động ẩn khi không cần thiết để trả lại không gian tối đa cho thế giới 3D.
   - *Áp dụng vào:* Thanh gợi ý tương tác `ModernUI` và các nhãn HUD ngữ cảnh.

---

## 4. PHẦN MỀM KHOA HỌC & THIẾT BỊ PHÂN TÍCH THỰC TẾ

10. **Agilent OpenLab CDS Interface & Chromatography Systems**
    - *Liên kết:* https://www.agilent.com/en/products/software-informatics/openlab-software-suite
    - *Điều học được:* Giao diện máy sắc ký/quang phổ hiện đại tổ chức theo 3 vùng: Thanh trạng thái thiết bị (đèn LED Ready/Run/Error), Khung tham số phương pháp, và Đồ thị tín hiệu thời gian thực có lưới chia độ chuẩn.
    - *Áp dụng vào:* Màn hình máy sắc ký HPLC, GC-MS và quang phổ UV-Vis trong `InstrumentScreen` và các `*Program.cs`.

11. **Thermo Scientific Chromeleon Chromatography Data System**
    - *Liên kết:* https://www.thermofisher.com/order/catalog/product/CHROMELEON7
    - *Điều học được:* Các nút điều khiển máy phân tích (Bắt đầu chạy, Dừng khẩn cấp, Hiệu chuẩn, Xuất báo cáo) có kích thước lớn, màu sắc rõ ràng (Xanh lục/Đỏ/Xanh dương), chữ hiển thị tiếng Việt ngắn gọn không bị tràn khung.
    - *Áp dụng vào:* 14 chương trình điều khiển máy trong phòng phân tích và phòng robot.

---

## 5. KHẢ NĂNG TIẾP CẬN & CÔNG THÁI HỌC (ACCESSIBILITY & ERGONOMICS)

12. **WCAG 2.2 Guidelines — Target Size (Minimum 2.5.8) & Contrast (1.4.3)**
    - *Liên kết:* https://www.w3.org/WAI/WCAG22/quickref/
    - *Điều học được:* Độ tương phản tối thiểu 4.5:1 cho chữ thông thường và 3:1 cho chữ lớn/nút bấm trên nền kính mờ. Kích thước mục tiêu chạm tối thiểu 24×24 CSS pixels và khuyến nghị 44×44 pt cho màn hình cảm ứng di động.
    - *Áp dụng vào:* Thang màu token `GlassKit` (Ink #F5F7FF, Dim #C7D6ED, Accent #61DBFF, Good #73F29E, Warn #FFC759, Bad #FF7373).

13. **Nielsen Norman Group — Mobile & Tablet Touch Ergonomics**
    - *Liên kết:* https://www.nngroup.com/articles/touch-target-size/
    - *Điều học được:* Tỷ lệ bấm nhầm giảm 85% khi khoảng cách giữa tâm 2 nút bấm liền kề cách nhau tối thiểu 8–12 pt. Người dùng cầm iPad bằng 2 tay sẽ thao tác chủ yếu bằng 2 ngón tay cái, vùng trung tâm trên và góc trên là khu vực khó với tới nhất.
    - *Áp dụng vào:* Khoảng cách giữa các nút trong `TouchHud` tăng lên 12–16 pt, nút Menu đưa vào góc trên có kích thước đủ lớn để dễ chạm khi cần.

---

## 6. KỸ THUẬT TỐI ƯU HÓA UGUI TRÊN UNITY & METAL

14. **Unity Manual — Optimizing Unity UI (Canvas Separation & Draw Calls)**
    - *Liên kết:* https://docs.unity3d.com/Manual/Optimizing-GUI.html
    - *Điều học được:* Mỗi khi một phần tử Graphic (Text/Image) thay đổi thuộc tính (đổi màu, di chuyển), toàn bộ Canvas chứa phần tử đó sẽ phải dựng lại lưới (re-batching). Tách các phần tử chuyển động (Joystick, thanh tiến trình, nút bấm động) sang Canvas con (Sub-Canvas) riêng biệt để giữ FPS ổn định 60–120 FPS trên chip Apple Silicon M3.
    - *Áp dụng vào:* Tách phần joystick và các hiệu ứng co giãn nút sang Sub-Canvas riêng biệt, giữ các panel tĩnh không bị re-batch.

15. **Unity Input System — Touchscreen & Virtual Controller Best Practices**
    - *Liên kết:* https://docs.unity3d.com/Packages/com.unity.inputsystem@1.7/manual/Touch.html
    - *Điều học được:* Lắng nghe sự kiện cảm ứng đa điểm qua `IPointerDownHandler`, `IPointerUpHandler`, `IPointerExitHandler` đảm bảo giải phóng trạng thái nhấn ngay cả khi ngón tay trượt khỏi nút hoặc khi bị ngắt bởi thông báo hệ thống iOS.
    - *Áp dụng vào:* Thiết kế `NutHieuUng.cs` đảm bảo 100% nút giữ nhả an toàn trong mọi tình huống.

16. **iOS Human Interface — Dynamic Type & Vietnamese Typographic Legibility**
    - *Liên kết:* https://developer.apple.com/design/human-interface-guidelines/typography
    - *Điều học được:* Chữ tiếng Việt có dấu thanh và dấu mũ kép (ế, ể, ộ, ử) đòi hỏi khoảng cách dòng (line spacing) tối thiểu 1.15–1.20 để dấu không bị chạm vào đáy dòng trên hoặc bị cắt cụt ở mép khung chứa.
    - *Áp dụng vào:* Tích hợp phông `Be Vietnam Pro` (SIL OFL) với lineSpacing 1.18 trong `GlassKit.Label`.
