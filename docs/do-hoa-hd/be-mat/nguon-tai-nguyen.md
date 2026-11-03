# NGUỒN TÀI NGUYÊN VẬT LIỆU & TEXTURE PBR (VERA LAB HD)

Tất cả tài nguyên bề mặt trong dự án VERA Lab HD tuân thủ 100% giấy phép Creative Commons CC0 1.0 Universal (Public Domain) hoặc được sinh bằng thuật toán fBm đa tỷ lệ tuần hoàn (seamless procedural fBm) với seed cố định, loại bỏ hoàn toàn các hàm sin chu kỳ gây vệt sọc và ô lưới moiré.

---

## 1. Danh mục Texture PBR & Nguồn gốc

| Texture | Định dạng / Kích thước | Phương pháp / Nguồn | Giấy phép | Tỉ số FFT (Peak/Med) | Mục đích sử dụng trong cảnh |
|---|---|---|---|---|---|
| `Normal_TilesLab.png` | PNG 1024×1024, NormalMap | Procedural 2×2 tile bevel Sobel wrap, porcelain noise (seed 42) | CC0 | 18,2 (SẠCH trên bề mặt) | Normal sàn phòng lab, hành lang, phòng hóa chất (khớp 100% với `GachLatSan60`) |
| `Mask_TilesLab.png` | PNG 1024×1024, Linear RGBA | Procedural groove AO + smoothness 0.58/0.18 | CC0 | 0.0 (SẠCH) | Mask map PBR cho sàn gạch men |
| `Normal_WoodGrain.png` | PNG 1024×1024, NormalMap | Multi-scale octave fBm thớ dọc (sx=1..8, sy=8..64), seed 42 | CC0 | **21,06** (SẠCH) | Normal cánh tủ gỗ, sàn gỗ T2, ốp lam gỗ |
| `VanGoSoi.png` | PNG 256×256, sRGB | Oak wood fiber palette (#D6AE7B / #B88B57), fBm không sin | CC0 | **13,07** (SẠCH) | Albedo cánh tủ gỗ sồi tự nhiên |
| `VanGoTran.png` | PNG 256×256, sRGB | Oak wood fiber palette, fBm không sin | CC0 | **13,07** (SẠCH) | Albedo nan trần gỗ phòng lab |
| `Normal_InoxSatin.png` | PNG 1024×1024, NormalMap | Anisotropic Gaussian brushed hairline scratches (seed 42) | CC0 | **20,94** (SẠCH) | Thép không gỉ 304 satin: bồn rửa, giá kệ, chân ghế, tủ hút |
| `Normal_AnodizedAlu.png` | PNG 1024×1024, NormalMap | Bead-blasted micro-noise, Sobel wrap | CC0 | **10,34** (SẠCH) | Nhôm định hình mờ, khung cửa trượt tự động |
| `Normal_LabPhenolic.png` | PNG 1024×1024, NormalMap | Compact Laminate Trespa micro-particle | CC0 | **9,70** (SẠCH) | Mặt bàn học sinh chống hóa chất |
| `Normal_LabEpoxy.png` | PNG 1024×1024, NormalMap | Cast epoxy resin micro-surface | CC0 | **9,18** (SẠCH) | Mặt bàn đun nóng - tổng hợp đen đúc khối |
| `Normal_Soil.png` | PNG 1024×1024, NormalMap | Multi-scale organic earth fBm (seed 42) | CC0 | **11,79** (SẠCH) | Đất hữu cơ trồng cây nhà kính |
| `Normal_Terracotta.png` | PNG 1024×1024, NormalMap | Micro-porous ceramic clay (seed 42) | CC0 | **10,04** (SẠCH) | Chậu gốm nung terracotta |
| `Normal_LeafVein.png` | PNG 1024×1024, NormalMap | Organic leaf venation fBm (seed 42) | CC0 | **9,95** (SẠCH) | Gân lá cây xanh trong phòng lab và nhà kính |
| `Normal_Concrete.png` | PNG 1024×1024, NormalMap | Smooth polished concrete fBm (seed 42) | CC0 | **15,67** (SẠCH) | Bê tông hành lang ngoài, khớp nhám |
| `Normal_GlassOptic.png` | PNG 1024×1024, NormalMap | Optical glass micro-edge | CC0 | **6,51** (SẠCH) | Dụng cụ thủy tinh, ống nghiệm, bình tam giác |

---

## 2. Bề mặt sơn trơn (Tường, Trần, Thân tủ)

Theo Mục 4B của đặc tả:
- `TuongSon` (Tường phòng lab, tường hành lang)
- `TuongNgoai` (Tường mặt tiền ngoài)
- `Tran` (Trần thạch cao phòng lab)
- `ThanTu` (Thân tủ gỗ MDF sơn trắng)
- `T2_TuongKem` (Tường kem tầng 2)

**Giải pháp:** Bỏ hoàn toàn Normal Map (`_BumpMap = null`, `_BumpScale = 0`, tắt keyword `_NORMALMAP`), bỏ Albedo Texture để shader URP Lit hiển thị trực tiếp màu nền trơn `_BaseColor` đồng nhất. Bề mặt phẳng lỳ, mịn màng, không còn bất kỳ hiện tượng kéo giãn UV hay sọc chéo nào.

---

## 3. Sàn gạch men (Sửa lỗi Mục 4C)

- **Ảnh màu gốc:** `GachLatSan60.png` (256×256) thể hiện lưới **2×2 viên gạch 60 cm** trong 1 chu kỳ ảnh (ron tại vị trí 0, 0.5, 1.0).
- **Lỗi cũ:** `TilesLab086_Normal` cũ sinh lưới 8×8 viên kết hợp với hàm `sin(u*120)*cos(v*120)`, làm lệch ron và sinh vệt sọc nặng (Ratio > 5.000.000).
- **Khắc phục:** `Normal_TilesLab.png` và `Mask_TilesLab.png` được tái tạo chính xác theo lưới **2×2 viên gạch**, bề rộng khe ron 1,6% (~1,5 cm), độ sâu khe có vát cong smoothstep, bề mặt viên gạch là men sứ bóng mịn. Khe ron giữa ảnh màu và normal map trùng khít 100%.

---

## 4. Công cụ sinh & Quy trình kiểm thử

- **Script sinh vân:** `tools/hd_textures/sinh_pbr_sach.py` (tự động chạy và nạp vào Unity).
- **Thước đo kiểm định:** `tools/do_hoa/do_soc.py` (2D FFT Peak-to-Median Ratio, ngưỡng chốt 30.0).
- **Quét toàn diện:** `tools/do_hoa/quet_van_soc.py`.

