# Báo cáo C4: Đồ họa giao diện ngoài lab và độ sắc nét

Phiên "N18 Cơ chế" (Claude), nhánh `n18/co-che`, ngày 18/11/2026. **Kết quả: XONG C4.**

Trước khi làm, tôi đã gộp `n18/antigravity` mới nhất vào nhánh (commit `2efec0a7`, gồm G1 và bản sửa `CanhKhoiDong` cho batchmode).

## Kết quả kiểm thử (lần chạy cuối, trên mã đã gộp)

| Bộ kiểm thử | Kết quả |
|---|---|
| `KhktUiVerify.Run` (mới, giao diện ngoài lab) | **35/35 đạt, 0 lỗi runtime, 24 ảnh** |
| `KhktCoCheVerify.Run` (C3) | **46/46 đạt, 0 lỗi (vẫn đạt sau khi tắt hậu kỳ camera cơ chế), FPS propellane mức Cao ≈ 630 (batchmode 640×480)** |
| `KhktWave6Verify.Run` (gồm mục 7.7, 7.8) | **67/67 đạt, 0 lỗi runtime** |

`KhktUiVerify` kiểm tra:
- Phông Be Vietnam Pro Regular và SemiBold đủ chữ "Ặ ỷ ữ ợ ự ẫ" ở cỡ 15, 36 và 72.
- Màn chính:
  - CanvasScaler tham chiếu 1920×1080, co theo màn hình;
  - canvas bật `pixelPerfect`;
  - nền là ảnh lab thật 2880×1620;
  - logo 192 px cho khung 60 pt, không mipmap.
- Màn chờ có thanh tiến trình thật và mẹo an toàn.
- Bảng điều khiển, kiểm ở cả 3 nền:
  - mọi nút cao ≥ 44 pt;
  - không có nút nào chồng lên nhau;
  - không có lớp phủ bán trong suốt;
  - camera giao diện không bật hậu kỳ;
  - có đủ 5 thẻ mô-đun, mỗi thẻ có ảnh thu nhỏ.
- Thẻ "Cơ chế 3D" mở được `CoCheMoDun.Mo()`. Thẻ "Thực vật 3D" và "Giải phẫu" ghi "Sắp có", vì nhánh này chưa có các lớp đó.
- Tên app và tên cửa sổ là "VERA Lab".

## Đã làm

1. **Độ sắc nét.** Lỗi lớn nhất tìm được ở bước này: **cả giao diện bị xám đục**.
   - **Nguyên nhân:** `HardwareProfile.ApplyCameras` (G1) bật hậu kỳ cho *mọi* camera Base, kể cả camera màn chính và camera lớp phủ cơ chế, vốn chỉ vẽ giao diện. Tonemapping Neutral kéo màu trắng `#ffffff` xuống còn 208/255 (khoảng 81%). Màu tối thì không đổi (`#151517` vẫn ra 21), nên ở nền Đen gần như không thấy, còn ở nền Trắng thì cả màn xám. Ảnh Wave6 trước C4 cũng bị như vậy.
   - **Cách sửa:** `MainMenu` và `CoCheMoDun` tự tắt hậu kỳ cho camera giao diện của mình ở mỗi khung hình. Đo lại: trắng = **255**.
   - **Đề nghị cho Antigravity:** trong `HardwareProfile.ApplyCameras`, bỏ qua camera chỉ vẽ UI (ví dụ theo `cullingMask == UI`). Tệp này của Antigravity nên tôi không sửa.
   - Canvas màn chính bật `pixelPerfect` để viền 1 px thẳng hàng điểm ảnh. CanvasScaler giữ 1920×1080, `match = 0,5`.
   - Ảnh giao diện mới đều tắt mipmap, không nén (`textureCompression: 0`), không co về lũy thừa 2, và có kích thước 2x. Logo VERA cũ và logo trường cũng chuyển sang không nén.
   - Phông: mọi màn ngoài lab dùng `UnityEngine.UI.Text` với Be Vietnam Pro nhúng sẵn. Đây là phông chữ động, Unity dựng chữ đúng theo cỡ điểm ảnh của màn, nên phóng to không bị mờ. Dự án không dùng TextMeshPro: không có `TMPro`, chưa nhập TMP Essential Resources. Phần "atlas TMP" trong đề vì vậy không áp dụng. Đã kiểm đủ dấu ở 3 cỡ như trên.

2. **Màn chính.**
   - Khung minh họa dùng ảnh **dựng thật của phòng lab**: chụp bằng camera trong cảnh ở PlayMode sau khi lab dựng xong và đã ẩn giao diện (`KhktUiVerify.ChupLab`). Ảnh được cắt bỏ phần tay, làm mờ nhẹ (bán kính 1,2) và phủ 8% màu xanh thương hiệu, kích thước 2880×1620.
   - Logo VERA vẽ lại bằng nét vector ở 1024 px, xuất bản 192 px (`Resources/GiaoDien/LogoVera192.png`).
   - Thẻ "Phòng thí nghiệm" có ảnh lab thật dưới lớp màu nhấn 86%. Nút "Vào phòng thí nghiệm" nới lên 270×58. Nút "Phòng học chung" được đặt theo bề rộng nút này, cách 16 px. Bản đầu từng làm hai nút đè nhau; người dùng phát hiện và đã sửa, bộ kiểm thử nay có thêm kiểm tra nút chồng.

3. **Bảng điều khiển: 5 thẻ mô-đun có ảnh thu nhỏ.** Thay hai thẻ "tầng" cũ bằng Phòng Hóa, Phòng Sinh, Cơ chế 3D, Thực vật 3D, Giải phẫu.
   - Ảnh thu nhỏ bo góc 10 px, cắt giữa cho vừa khung.
   - Ảnh Phòng Hóa và Phòng Sinh chụp từ lab thật. Ảnh Cơ chế 3D là hình 3D SN2 của C3.
   - Ảnh Thực vật 3D và Giải phẫu là hình vẽ đơn giản (lá cây, trái tim), vì hai mô-đun này chưa có, không phải ảnh chụp từ mô-đun.
   - Thẻ gọi `Mo()` bằng cách tìm lớp theo tên trong mọi assembly đã nạp, nên biên dịch không lỗi dù nhánh kia chưa gộp.
   - Chữ trên thẻ tự co lại khi thẻ hẹp, không tràn khung.

4. **Màn chờ.**
   - Thanh tiến trình thật (`AsyncOperation.progress`) đã có sẵn và được giữ nguyên.
   - Thêm "MẸO AN TOÀN PHÒNG THÍ NGHIỆM": 8 mẹo, đổi mỗi 5 giây khi đang nạp cảnh.

5. **Biểu tượng app và tên.**
   - `Assets/Lab/Logo/IconVeraLab1024.png`: lục giác và chữ V vẽ lại ở 4096 px rồi thu về 1024, nền xanh chuyển màu, bo góc kiểu macOS.
   - `VeraLab.ico` có các cỡ 16 tới 256 cho Windows.
   - `KhktUiVerify.DatTenVaBieuTuong` đặt `PlayerSettings.productName = "VERA Lab"` và biểu tượng Standalone đủ 8 cỡ (Mac và Windows dùng chung).
   - ProjectSettings chỉ đổi tên và biểu tượng.
   - **Lưu ý:** đổi `productName` làm bản Mac lưu PlayerPrefs ở tệp mới, nên người dùng cũ mất cài đặt đã lưu (nền, phiên đăng nhập) một lần.

6. **Giữ đúng 3 nền của UiTheme, nút ≥ 44 pt, hoạt cảnh nhẹ theo MenuAnim.** Không đổi bảng màu.

## Ảnh trước / sau (`docs/ngay-18-11/anh/c4/`)

- **Ảnh "trước":** bộ chụp riêng của C4 không ra được ảnh "trước". Lý do: bộ chụp được gắn vào cảnh ManHinhChinh, nên mỗi lần cảnh nạp lại Unity dựng thêm một bản và các bản giẫm lên nhau. Lỗi đã sửa, nhưng theo thống nhất với phiên kế hoạch, tôi dùng ảnh Wave6 chụp trên `n18/co-che` *trước khi sửa giao diện C4* làm ảnh "trước" (`truoc_step7_*`).
- **Ảnh "sau":** 4 màn (đăng nhập, cài đặt, màn chờ, bảng điều khiển) × 3 nền × 2 độ phân giải (1440×900 và 2880×1800). Các tệp `sau_*` thu về 1440 px. Ảnh đặt cạnh nhau: `so_sanh_*.png`.
- **Lưu ý khi chụp:** Unity batchmode có màn 640×480. Đổi sang cỡ chụp mà không ép chữ dựng lại thì chữ trong ảnh 2880 bị nhòe. App thật không bị lỗi này, vì chữ được dựng đúng theo màn ngay từ đầu. Bộ chụp nay ép dựng lại chữ trước khi chụp.

| Màn | Ảnh |
|---|---|
| Trước → sau (bảng điều khiển; ảnh "trước" là trang Cải thiện của Wave6 – trang Tổng quan không có ảnh trước) | `so_sanh_bang_dieu_khien_Trang.png` |
| Đăng nhập, 3 nền | `sau_dang_nhap_{Trang,Den,XanhMuc}_1440x900.png` |
| Cài đặt, 3 nền | `sau_cai_dat_{Trang,Den,XanhMuc}_1440x900.png` |
| Màn chờ có mẹo an toàn, 3 nền | `sau_man_cho_{Trang,Den,XanhMuc}_1440x900.png` |
| Bảng điều khiển, 3 nền | `sau_bang_dieu_khien_{Trang,Den,XanhMuc}_1440x900.png` |
| Cắt 100% từ ảnh 2880×1800 (độ nét chữ trên Retina) | `sau_dang_nhap_Trang_2880_cat100.png` |
| Ảnh "trước" còn lại (Wave6) | `truoc_step7_dashboard_*.png` |

Đo màu nền Trắng: trước khi tắt hậu kỳ, trắng = 208/255; sau = **255/255**, màu `#f5f5f7` = (245, 245, 247) đúng như khai báo.

## Còn chưa ổn / ghi chú

- Màn **Cài đặt** (có sẵn, không thuộc phần tôi sửa): khối nội dung bên phải đi sát mép phải màn hình. Ở màn đăng nhập, nút "Đổi" máy chủ bị viền mép trái che một chút. Cả hai là bố cục cũ, nên chỉnh tiếp ở lượt sau.
- Ảnh thu nhỏ Thực vật 3D và Giải phẫu là hình vẽ tạm. Khi mô-đun G3/G4 xong nên thay bằng ảnh chụp từ mô-đun.
- Chưa chụp được trên màn Retina thật của bản dựng Mac; số liệu ở trên lấy từ Editor batchmode, ảnh dựng ở 2880×1800.
- `HardwareProfile` nên bỏ qua camera chỉ vẽ UI (xem mục 1).
