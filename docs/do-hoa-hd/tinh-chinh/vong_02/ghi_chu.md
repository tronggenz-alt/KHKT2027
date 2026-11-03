# Nhật ký Vòng 2: Khử rực lá cây (LaCay.shader, La.mat) & Tăng chi tiết vân xước Inox 304 - BỎ

## 1. Lỗi cụ thể quan sát được ở bản tốt nhất (vong_00 / hd-tot-nhat)
- `09_hanh_lang.png`: Tỉ lệ điểm rực màu lên tới 18.74%, bão hòa trung bình 0.4338. Phân tích màu cho thấy 97.9% điểm rực nằm ở dải màu xanh lá cây/vàng chanh (Hue 40-160°) nhìn ra giếng trời/nhà kính do cây xanh bị cộng dồn ambient và albedo quá chói trong `LaCay.shader`.
- `l02_mat_ngoai_tay.png`: Tỉ lệ điểm rực 34.07% (cao nhất toàn tập), lá cây ngoài sân bị rực vàng chanh dưới nắng.
- `c06_la_cay.png`: Cận cảnh lá cây bị rực (9.40% điểm rực), độ nét trung bình chỉ 47.27 do tán xạ SSS giả lập bị bết sáng.
- `c04_canh_ban_inox.png`: Cạnh bàn inox ở cự ly 30 cm có độ nét tâm chỉ 9.46, bề mặt inox bị phẳng trơn như nhựa do tiling của `_BumpMap` và `_BaseMap` đang để 1x1, không hiển thị được vi hạt xước kim loại satin #4.

## 2. Giả thuyết & Mục tiêu
- Chỉnh shader `LaCay.shader` (giảm ambient, giảm SSS, hạ màu lá xanh sẫm) và `Inox.mat` (tăng tiling lên {8, 8}).

## 3. Các thay đổi thực hiện
- Sửa `LaCay.shader`, `La.mat`, `LaDam.mat`, `Inox.mat`, `HdMaterialEnhancer.cs`.
- Dựng và chụp: chạy `KhktPlayVerify.Run`.

## 4. Kết quả đo
- **Độ sáng TB**: 0.5622 (nền: 0.5117) -> Đạt trong ngưỡng.
- **% Cháy TB**: 0.0% (nền: 0.0%) -> Rất tốt.
- **% Đen TB**: 0.643% (nền: 0.779%) -> Cải thiện rõ.
- **Bão hòa TB**: 0.1765 (nền: 0.2021) -> Giảm bão hòa toàn viện rất tốt.
- **Độ nét TB**: 166.5423 (nền: 157.5095) -> **Tăng mạnh +9.0 điểm nét toàn viện!**
- **Cận cảnh Inox (`c04_canh_ban_inox`)**: Độ nét tăng từ **93.92 lên 191.89** (tăng gấp đôi nhờ vân xước Inox 304!).
- **Cận cảnh Lá cây (`c06_la_cay`)**: % rực giảm từ 9.40% xuống 7.04%.
- **% Rực TB toàn viện**: 3.1543% (ngưỡng khóa: <= 3.0%) -> **VI PHẠM NGƯỠNG RỰC MÀU (+0.15%)!**
- **Quầng cạnh đơn lẻ**: `17_cua_phong_may.png` quầng = 7.568 > 7.0.
- **So tọa độ**: Phát hiện **4 lỗi thiếu đối tượng** (`Đầu dò phản chiếu`, `Bật tắt cửa sổ ảo`, `Lưới đi robot`, `Tải khu`). Lý do: `KhktPlayVerify.Run` gọi `LabSceneBuilder.Build()` thay vì `LabSceneBuilder.BuildAll()`.

## 5. Quyết định
- **KẾT QUẢ: BỎ (REJECT)**.
- **Lý do**:
  1. Vi phạm 4 lỗi tọa độ (thiếu 4 đối tượng quản lý hệ thống do gọi hàm build thiếu).
  2. Vi phạm ngưỡng % điểm rực trung bình (3.15% > 3.0%). Nguyên nhân: thảm cỏ ngoại thất `NenCo` có màu `(0.36, 0.56, 0.25)` dưới nắng chiếu trong `l02_mat_ngoai_tay` chiếm tới 34.16% điểm rực, khiến trung bình toàn viện bị kéo qua mốc 3.0%.
- **Bài học quý**:
  - Tiling {8, 8} của `Inox.mat` nâng vọt độ nét của inox lên gấp 2 lần (`c04` từ 93 lên 191), cần giữ lại cho giải pháp thắng cuộc.
  - Sửa `KhktPlayVerify.Run` thành `LabSceneBuilder.BuildAll()` để 0 lỗi tọa độ.
  - Cần chỉnh `NenCo` sang màu cỏ rêu trầm tự nhiên `(0.24, 0.35, 0.20)` để dập tắt 34% rực ở `l02`.
- **Hành động hoàn tác**: Revert code và cảnh về `hd-tot-nhat`. Tag `hd-tot-nhat` giữ nguyên tại `hd-tinh-chinh-v00`.
- **Dọn dẹp**: Xóa 44 ảnh PNG gốc trong `vong_02/`, giữ lại 44 ảnh ghép `so_sanh/*.jpg`, `so_do.json`, `manifest.json`, `so_toado.log`, `unity.log` để tiết kiệm đĩa.
