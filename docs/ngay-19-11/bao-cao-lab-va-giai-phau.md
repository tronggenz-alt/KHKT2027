# Báo cáo 19/11: nút Về sảnh trong lab + giải phẫu bản web CHEMISTRY trong app

Nhánh `n18/tich-hop`. Commit: `b29befa9` (Về sảnh), `5379b632` (giải phẫu bản web) và commit kèm báo cáo này (plugin Mac ẩn nút dự phòng).

## 1. Nút "Về sảnh" trong phòng lab
- Menu Esc của lab (`IntroScreen`, trên iPad là nút ☰) có nút **"← Về sảnh"** cạnh "Cài đặt".
- Bấm → hộp hỏi: **Ở lại / Màn chọn ban đầu / Bảng điều khiển**. Đang làm dở bài (chuẩn độ đã xong ≥1 bước mà chưa xong
  hết, hoặc bài sinh học chưa có kết quả) thì hộp báo "Bài đang làm dở sẽ mất". Esc đóng hộp hỏi.
- `Scripts/Menu/VeSanh.cs`: đóng trang giải phẫu, thả con trỏ, rồi `GameSession.ReturnToMenu()` (UiCapture = 0, timeScale = 1,
  rời phòng mạng, nạp ManHinhChinh kiểu Single nên camera/UI của lab bị hủy theo cảnh).
- "Màn chọn ban đầu" bật cờ `GameSession.VeManChonKhiVe`; `MainMenu.Start` thấy cờ thì mở màn chọn 2 luồng thay vì vào
  thẳng bảng điều khiển (cờ tự xóa). "Bảng điều khiển" giữ hành vi cũ (HasIdentity → bảng điều khiển).
- Không sửa ManChonLuong, CoChe, HoaLy, MenuKit/GlassKit.
- **Chưa kiểm** bấm nút trong Play Mode hay app thật (chỉ biên dịch + KhktUiVerify toàn bộ đạt).

## 2. Giải phẫu: trả lại bản web BIO-LAB 3D của CHEMISTRY
- **Bản web**: `StreamingAssets/biology_lab` chính là CHEMISTRY v1.1.0 (`~/CHEMISTRY_xem/frontend`, commit 5e6066b7, bản mới
  nhất) đã chỉnh chạy ngoại tuyến (three/gsap/mathjax trong `vendor/`, đường dẫn tương đối) và có nút "← Về phòng Lab 3D".
  `sinh-hoc/` giống hệt bản gốc → giữ nguyên, không chép lại.
- **Mac (ưu tiên 1)**: plugin mới `Assets/Plugins/macOS/BioWebViewMac.bundle` (mã nguồn `Source~/BioWebViewMac.mm`, dựng bằng
  `Source~/build.sh`, arm64 + x86_64, chỉ bật cho Standalone OSX). WKWebView phủ kín cửa sổ app, cho phép file:// + ES module,
  nhận `webkit.messageHandlers.unity.postMessage('close')` và `veralab://close`; C# hỏi `_BioMac_ConsumeClosed()` mỗi khung hình.
  Nút gốc "← Về Lab" chỉ hiện khi trang không tải được (trang đã có nút riêng, tránh che giao diện).
- **Windows**: chưa làm WebView2 (cần SDK + plugin C++ dựng trên Windows). Thay vào đó `MayChuWebNoiBo` phục vụ
  `biology_lab` ở `http://127.0.0.1:<cổng ngẫu nhiên>` rồi mở trình duyệt mặc định (file:// không chạy được ES module trên
  Chrome/Edge), kèm lớp phủ "← Trở về Phòng Lab 3D" trong app. Chưa thử trên máy Windows thật.
- **iPad**: giữ nguyên WKWebView cũ (`Plugins/iOS/BioWebViewPlugin.mm`).
- **Lối vào**: thẻ "Giải phẫu" trên bảng điều khiển (`Khkt.Bio.GiaiPhau.GiaiPhauMoDun` → `BioWebViewBridge`), bàn giải phẫu số
  `DigitalAnatomyTable.Use()`, cổng sinh học (`BioPortalTransition` → `BioWebViewBridge.Open`). Mã G4 (`GiaiPhauMoDunImpl`,
  `GiaiPhauSanKhau3D`) vẫn trong kho nhưng không còn lối vào nào gọi.
- Khi trang mở: thả con trỏ và tăng `DesktopViewer.UiCapture` (lab không nhận phím/chuột); đóng thì trả lại.
- Editor batchmode (kiểm thử) chỉ đánh dấu mở, không bật trình duyệt.

## 3. Kiểm tra
| Hạng mục | Kết quả |
|---|---|
| Biên dịch Unity | 0 lỗi |
| `KhktN18Verify.RunG4` (viết lại cho bản web) | 9/9 đạt: đủ tệp web, trang có nút về + three.js ngoại tuyến, bundle Mac đúng nền tảng, Mo/Dong qua cầu nối, 5 lần mở/đóng không rò UiCapture, bàn giải phẫu số mở bản web, máy chủ 127.0.0.1 trả trang 200 và module JS `text/javascript` |
| `KhktUiVerify.Run` | 72/72 đạt (thẻ Giải phẫu mở và Dong() về bảng điều khiển) |
| `MacBuild.Build` | Succeeded, 0 lỗi → `~/AppBuild/VERA_mac/VERA Lab.app` (có `PlugIns/BioWebViewMac.bundle` và `StreamingAssets/biology_lab`) |
| Xem tận mắt plugin | App thử Cocoa nạp đúng bundle trong app đã xuất: trang hiện đầy đủ (mô hình người WebGL, Bio-Tutor, các cơ quan); bấm "← Về phòng Lab 3D" của trang → lớp phủ gỡ sạch, cờ đóng báo về C#. Ảnh: `anh/giai-phau-web-trong-app-mac.png` |

Chưa làm: mở chính app VERA Lab bấm qua giao diện tới thẻ Giải phẫu (cần đăng nhập/khách và điều khiển màn hình); WebView2 cho Windows.

Các tệp `Lab/Materials/*.mat` bị kiểm thử đổi đã lưu diff vào `~/Developer/Antigravity-luu-ban-cu/mat-truoc-checkout-19-11-giai-phau.diff` rồi trả về.
