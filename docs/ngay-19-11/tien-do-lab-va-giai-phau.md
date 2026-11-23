# Tiến độ: nút Về sảnh + giải phẫu bản web cũ (19/11) — ĐÃ XONG, xem bao-cao-lab-va-giai-phau.md

Hạn mức 5 giờ đã dùng 76%, dừng theo luật chừa 15%. Nhánh `n18/tich-hop`.

## Đã xong (việc 1: nút Về sảnh)
- `Scripts/Menu/VeSanh.cs` (mới): `VeSanh.DangLamDo()` (chuẩn độ đã làm ≥1 bước mà chưa xong, hoặc bài sinh học chưa có kết quả)
  và `VeSanh.Ve(veManChon)`: đóng web giải phẫu (`BioWebViewBridge.Close`), đóng mô-đun G4 nếu đang mở,
  thả con trỏ, rồi `GameSession.ReturnToMenu()` (UiCapture = 0, timeScale = 1, rời phòng mạng, nạp ManHinhChinh kiểu Single nên camera lab bị hủy).
- `GameSession.VeManChonKhiVe` (mới) + `MainMenu.Start`: cờ bật thì mở màn chọn 2 luồng thay vì bảng điều khiển; cờ tự xóa.
- `Scripts/Demo/IntroScreen.cs` (menu Esc / ☰ trên iPad): nút "← Về sảnh" cạnh "Cài đặt"; hộp hỏi
  "Ở lại / Màn chọn ban đầu / Bảng điều khiển" (chữ cảnh báo mất bài khi đang làm dở); Esc đóng hộp hỏi.
- Không sửa ManChonLuong, CoChe, HoaLy, MenuKit/GlassKit.

Kiểm tra: Unity batchmode biên dịch 0 lỗi. Chưa chạy thử trong Play Mode / app Mac.

## Đã tìm hiểu (việc 2: giải phẫu)
- Bản web trong `StreamingAssets/biology_lab` = CHEMISTRY v1.1.0 (`~/CHEMISTRY_xem/frontend`, commit 5e6066b7, bản mới nhất)
  + chỉnh chạy ngoại tuyến (three/gsap/mathjax trong vendor/, đường dẫn tương đối, nút "← Về phòng Lab 3D" gọi
  `webkit.messageHandlers.unity.postMessage('close')` / `veralab://close`). `sinh-hoc/` giống hệt. → Giữ bản StreamingAssets, không cần chép lại.
- Trang dùng ES module + fetch file:// → Chrome/Edge mở file:// sẽ hỏng; Windows cần máy chủ HTTP nội bộ (TcpListener 127.0.0.1) hoặc WebView2.

## Còn lại
1. Plugin Mac: `Assets/Plugins/macOS/Source~/BioWebViewMac.mm` (WKWebView phủ contentView của cửa sổ Unity, nút NSButton "← Về Lab",
   handler `unity` + chặn `veralab://`), API C `_BioMac_Open/_Close/_IsOpen/_ConsumeClosed` (C# hỏi mỗi khung hình vì Mac không có UnitySendMessage).
   Dựng: `clang++ -fobjc-arc -bundle -arch arm64 -arch x86_64 -framework Cocoa -framework WebKit` → `Assets/Plugins/macOS/BioWebViewMac.bundle` (cần Info.plist).
   (Bản nháp lần trước bị người dùng từ chối ghi, chưa có tệp.)
2. `BioWebViewBridge.Open`: Mac standalone → `_BioMac_Open`; Windows → máy chủ HTTP nội bộ + `Application.OpenURL` + lớp phủ "← Trở về";
   Editor batchmode → chỉ đánh dấu mở (cho kiểm thử); bỏ gọi `GiaiPhauMoDunImpl`. Khi mở: UiCapture++ và thả con trỏ; khi đóng thì trả lại.
3. `Khkt.Bio.GiaiPhau.GiaiPhauMoDun` (Bridge, asmdef riêng): Mo/Dong/DangMo gọi `Khkt.Demo.Bio.Portal.BioWebViewBridge` qua reflection.
   `DigitalAnatomyTable.Use()` gọi `BioWebViewBridge.Open()` thay cho G4. Giữ mã G4, không dùng.
4. Sửa `KhktN18Verify` G4 (kiểm cầu nối mở bản web, không còn kiểm sân khấu 3D) và `KhktUiVerify` thẻ Giải phẫu (DangMo qua bridge).
5. Biên dịch, kiểm thử, `Khkt.EditorTools.MacBuild.Build`, mở app xem tận mắt, báo cáo `bao-cao-lab-va-giai-phau.md`.
