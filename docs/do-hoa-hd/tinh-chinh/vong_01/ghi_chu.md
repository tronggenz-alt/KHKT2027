# Nhật ký Vòng 1: Tinh chỉnh Hậu kỳ URP (HauKy.asset) - Giảm quầng mờ, hạ bão hòa nhân tạo, cân bằng trắng chuẩn lab

## 1. Lỗi cụ thể quan sát được ở bản nền (vong_00 / hd-tot-nhat)
- `09_hanh_lang.png`: Độ bão hòa cao quá mức (sat = 0.4338, % điểm rực = 18.74%), viền cửa và đèn bị quầng mờ (halo = 5.27).
- `l02_mat_ngoai_tay.png`: Tỉ lệ điểm rực lên tới 34.07% (vượt quá mức thẩm mỹ kiềm chế của ảnh kiến trúc hiện đại), quầng mép kính/tường = 4.91.
- `17_cua_phong_may.png`: Mức quầng sáng halo cao nhất toàn bộ dữ liệu (halo = 5.45), vùng tối bị bết đen (pct_den = 4.95%).
- `c04_canh_ban_inox.png`: Độ sáng hơi thấp (do_sang = 0.4142), bóng dưới mặt bàn bị bết đen (3.38%).
- Toàn bộ cảnh mang ám vàng nhẹ do `WhiteBalance.temperature` = 6, không đạt chuẩn ánh sáng trắng trung tính phòng sạch 5500K.

## 2. Giả thuyết & Mục tiêu
- **Giả thuyết**: 
  - Trong `HauKy.asset`, `saturation = 14` đẩy bão hòa nhân tạo lên toàn bộ cảnh; hạ xuống `2` sẽ giúp màu sắc trung tính, thanh thoát theo phong cách kiến trúc châu Âu.
  - `Bloom` (intensity = 0.25, scatter = 0.6, threshold = 1.1) khuếch tán ánh sáng quá mạnh làm mờ mép viền thiết bị; hạ intensity xuống `0.08`, scatter `0.40`, tăng threshold lên `1.30` sẽ dập tắt quầng viền (halo), giữ viền sắc gọn mà vẫn giữ lấp lánh phản xạ kim loại/thủy tinh.
  - `contrast = 14` dìm vùng tối xuống đen; hạ xuống `10` sẽ mở nhẹ shadow giúp `c04` và `17` giảm điểm bết đen.
  - `WhiteBalance.temperature = 6` hạ về `0` để khử ám vàng, đưa màu trắng về chuẩn D65 trung tính.
- **Mong đợi**:
  - Halo trung bình giảm từ ~3.30 xuống < 3.00.
  - % điểm rực giảm rõ ở `09_hanh_lang` và `l02_mat_ngoai_tay`.
  - % điểm đen ở các góc tối giảm.
  - Không vi phạm ngưỡng NGUONG.md.
  - Tọa độ 0 lỗi.

## 3. Các thay đổi thực hiện
- Sửa `Phong thi nghiem ao/Assets/Lab/HauKy.asset`:
  - `ColorAdjustments`:
    - `saturation`: 14 -> 2
    - `contrast`: 14 -> 10
  - `Bloom`:
    - `intensity`: 0.25 -> 0.08
    - `scatter`: 0.6 -> 0.4
    - `threshold`: 1.1 -> 1.3
  - `WhiteBalance`:
    - `temperature`: 6 -> 0

## 4. Kết quả đo
(Sẽ cập nhật sau khi chạy kiểm thử)

## 5. Quyết định
(Sẽ quyết định dựa trên so sánh ảnh và số đo)
