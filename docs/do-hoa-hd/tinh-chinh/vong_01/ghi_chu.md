# Nhật ký Vòng 1: Tinh chỉnh Hậu kỳ URP (HauKy.asset) - BỎ

## 1. Lỗi cụ thể quan sát được ở bản nền (vong_00 / hd-tot-nhat)
- `09_hanh_lang.png`: Độ bão hòa cao quá mức (sat = 0.4338, % điểm rực = 18.74%), viền cửa và đèn bị quầng mờ (halo = 5.27).
- `l02_mat_ngoai_tay.png`: Tỉ lệ điểm rực lên tới 34.07% (vượt quá mức thẩm mỹ kiềm chế của ảnh kiến trúc hiện đại), quầng mép kính/tường = 4.91.
- `17_cua_phong_may.png`: Mức quầng sáng halo cao nhất toàn bộ dữ liệu (halo = 5.45), vùng tối bị bết đen (pct_den = 4.95%).
- `c04_canh_ban_inox.png`: Độ sáng hơi thấp (do_sang = 0.4142), bóng dưới mặt bàn bị bết đen (3.38%).
- Toàn bộ cảnh mang ám vàng nhẹ do `WhiteBalance.temperature` = 6.

## 2. Giả thuyết & Mục tiêu
- Sửa trong `HauKy.asset`:
  - `saturation`: 14 -> 2
  - `contrast`: 14 -> 10
  - `Bloom`: intensity 0.25 -> 0.08, scatter 0.6 -> 0.4, threshold 1.1 -> 1.3
  - `WhiteBalance.temperature`: 6 -> 0

## 3. Các thay đổi thực hiện
- Sửa file `Phong thi nghiem ao/Assets/Lab/HauKy.asset`.
- Chạy kiểm thử: `KhktPlayVerify.RunNoBuild` với `KHKT_TEST=do_hoa`.

## 4. Kết quả đo
- **Độ sáng TB**: 0.5121 (nền: 0.5117)
- **% Cháy TB**: 0.0% (nền: 0.0%)
- **% Đen TB**: 0.754% (nền: 0.779%) -> Cải thiện nhẹ.
- **Bão hòa TB**: 0.1603 (nền: 0.2021) -> Giảm bão hòa tốt.
- **% Rực TB**: 2.377% (nền: 2.913%) -> Cải thiện tốt.
- **Độ nét TB**: 147.4959 (nền: 157.5095) -> **THẤP HƠN NGƯỠNG KHÓA 150.0!**
- **Quầng cạnh TB**: 3.4254 (nền: 3.303) -> Tăng nhẹ ở mép trời ngoại thất.
- **Tọa độ**: 0 lỗi trên 18.964 vật thể.
- **Lỗi kiểm chứng `kiem_chung.py`**:
  - `[TRUNG BÌNH] LỖI ĐỘ NÉT THẤP: 147.4959 < 150.0`
  - `[l01_mat_ngoai.png] LỖI QUẦNG CẠNH ĐƠN LẺ NẶNG: 8.786 > 7.0`
  - `[l02_mat_ngoai_tay.png] LỖI QUẦNG CẠNH ĐƠN LẺ NẶNG: 20.725 > 7.0`
  - `NullReferenceException` và `ZBinningJob InvalidOperationException` trong `unity.log` khi không rebuild cảnh.

## 5. Quyết định
- **KẾT QUẢ: BỎ (REJECT)**.
- **Lý do**:
  1. Hạ contrast từ 14 xuống 10 làm gradient viền yếu đi, kéo tụt độ nét trung bình toàn viện xuống 147.50 (< 150.0), vi phạm nghiêm trọng tiêu chí không được làm giảm độ nét so với mốc nền.
  2. Việc chỉ sửa hậu kỳ qua `RunNoBuild` mà không đồng bộ tái tạo URP Forward+ ZBinning gây exception trong Unity log.
  3. Mép đường viền chân trời ngoại thất bị vọt lố gradient khi bloom threshold bị nâng lên 1.30.
- **Hành động hoàn tác**: Đã hoàn tác `HauKy.asset` về nguyên trạng `v00`. Tag `hd-tot-nhat` giữ nguyên tại `hd-tinh-chinh-v00`.
- **Dọn dẹp**: Đã xóa toàn bộ ảnh PNG gốc trong `vong_01/`, giữ lại 44 ảnh ghép so sánh `so_sanh/*.jpg`, `so_do.json`, `manifest.json`, `so_toado.log`, `unity.log` để tiết kiệm dung lượng đĩa.
