# HỆ THỐNG THIẾT KẾ GIAO DIỆN "GLASSKIT PRO" (VERA LAB HD)

Tài liệu quy chuẩn toàn bộ hệ thống thiết kế giao diện kính mờ hiện đại dành cho phiên bản iPad của VERA Lab, kế thừa và mở rộng từ `GlassKit.cs`, tối ưu hóa cho màn hình cảm ứng Liquid Retina và chip Apple Silicon.

---

## 1. BẢNG MÀU TOKEN (COLOR TOKENS)

| Token Name | Mã Hex | Giá trị RGBA (0–1) | Ý nghĩa & Vị trí sử dụng |
|---|---|---|---|
| **`Ink`** | `#F5F7FF` | `(0.96, 0.97, 1.00, 0.97)` | Chữ tiêu đề chính, nhãn nút hành động, số đo khoa học có độ tương phản cao nhất. |
| **`Dim`** | `#C7D6ED` | `(0.78, 0.84, 0.93, 0.75)` | Chữ phụ, mô tả hướng dẫn, đơn vị đo, nhãn thứ cấp. |
| **`Muted`** | `#8A9BB5` | `(0.54, 0.61, 0.71, 0.60)` | Chữ chú thích rất nhỏ, văn bản vô hiệu hóa (disabled). |
| **`Accent`** | `#38DBFF` | `(0.22, 0.86, 1.00, 1.00)` | Màu nhấn ngọc lam (Cyan Neon) đồng bộ với dải viền trang trí tòa nhà, dùng cho nút chính, con trỏ nhập liệu, tab đang chọn. |
| **`Good`** | `#5CE68A` | `(0.36, 0.90, 0.54, 1.00)` | Trạng thái thành công, đạt tiêu chuẩn, phản ứng hóa học hoàn tất, pH trung tính an toàn. |
| **`Warn`** | `#FFC759` | `(1.00, 0.78, 0.35, 1.00)` | Cảnh báo nồng độ, nhiệt độ cao, hóa chất độc hại, pin yếu. |
| **`Bad`** | `#FF5C5C` | `(1.00, 0.36, 0.36, 1.00)` | Lỗi, nút dừng khẩn cấp, ngoài phạm vi đo, vi phạm an toàn phòng lab. |
| **`GlassBase`** | `#0F1A2E` | `(0.06, 0.10, 0.18, 0.55)` | Nền kính mờ tối (Dark Glass) lọc ánh sáng phòng lab, giữ độ rõ nét chữ tuyệt đối. |
| **`GlassSurface`**| `#1B2A45` | `(0.11, 0.16, 0.27, 0.75)` | Mặt nổi của thẻ dụng cụ, menu nhanh, thanh trạm. |
| **`GlassBorder`** | `#FFFFFF` | `(1.00, 1.00, 1.00, 0.35)` | Đường viền phản chiếu ánh sáng kính mờ bo góc. |

---

## 2. THANG ĐO KÍCH THƯỚC VÙNG CHẠM CẢM ỨNG (TOUCH TARGET SCALE)

Quy chuẩn kích thước theo điểm ảnh logic (Points - pt) trên iPad:

| Phân cấp nút | Kích thước trên Canvas (Units) | Kích thước trên iPad Air M3 (pt) | Kích thước trên iPad Pro 12.9" (pt) | Trường hợp sử dụng |
|---|---|---|---|---|
| **Action Lớn (Hero / Primary)** | 104 × 104 | **71.0 × 71.0 pt** | **85.4 × 85.4 pt** | Nút Tay chính (`MainActionButton`) dùng ngón cái thao tác liên tục. |
| **Nút Chuẩn (Standard Touch)** | 88 × 65 | **60.1 × 44.4 pt** | **72.3 × 53.4 pt** | Nút Trả về, Lắc bình, Shift, Ngồi, Menu, Nút buret, Nút đóng cửa sổ. |
| **Nút Hàng Danh Sách (List Row)**| 240 × 65 | **164.0 × 44.4 pt**| **197.1 × 53.4 pt**| Các mục chọn trạm thí nghiệm, danh sách hóa chất, chọn bài chuẩn độ. |
| **Nút Nhỏ / Icon Nút (Icon Only)**| 65 × 65 | **44.4 × 44.4 pt** | **53.4 × 53.4 pt** | Nút đóng `✕`, nút phóng to, nút trợ giúp, nút âm thanh. |

> **Nguyên tắc cứng:** Tuyệt đối không có bất kỳ nút nào có chiều rộng hoặc chiều cao dưới **65 units** trên Canvas tham chiếu (đảm bảo luôn >= **44 pt** trên mọi dòng iPad).

---

## 3. THANG CHỮ & ĐỒ HỌA CHỮ (TYPOGRAPHY)

Sử dụng phông chữ **Be Vietnam Pro**:

| Cấp bậc chữ | Kích cỡ (Font Size) | Trọng lượng (Weight) | Khoảng cách dòng | Mục đích sử dụng |
|---|---|---|---|---|
| **Display / Tiêu đề lớn** | 36 – 44 | Bold | 1.15 | Tên app trên màn hình đầu, tiêu đề bài chuẩn độ lớn. |
| **Heading 1 / Tiêu đề bảng** | 24 – 28 | SemiBold | 1.18 | Tiêu đề bảng cài đặt, tiêu đề máy phân tích, thẻ dụng cụ. |
| **Heading 2 / Nhãn nhóm** | 18 – 20 | SemiBold | 1.18 | Nhãn các nhóm chức năng, tên nút bấm lớn. |
| **Body / Chữ thân** | 16 – 17 | Regular / Medium | 1.20 | Mô tả thao tác, văn bản gợi ý, bước thực hành, thông số máy. |
| **Caption / Chú thích** | 14 – 15 | Regular | 1.20 | Đơn vị đo, nhãn phụ, chỉ số dưới hóa học (`ml`, `pH`, `SO₄²⁻`). |

---

## 4. QUY CHUẨN BO GÓC & KHOẢNG CÁCH (SHAPES & SPACING)

- **Bo góc (Corner Radius):**
  - Khung bảng lớn (Panel/Window): **24 pt** (bo tròn mềm mại hiện đại).
  - Nút bấm chuẩn & Thẻ (Button/Card): **16 pt**.
  - Chip nhãn / Ô nhập (Input/Chip): **10 pt**.
  - Nút tròn thao tác (Circle Button): **50% (Hoàn toàn tròn)**.
- **Khoảng cách (Spacing Grid):** Bội số của 8 pt (8, 16, 24, 32, 48 pt). Khoảng cách tối thiểu giữa 2 nút bấm liền kề là **12 pt** để triệt tiêu nguy cơ chạm nhầm.

---

## 5. BỘ CHUYỂN ĐỘNG & HIỆU ỨNG NHẤN (MOTION SPECIFICATION)

1. **Hiệu ứng chạm ngón tay (`NutHieuUng.cs`):**
   - Khi chạm (`PointerDown`): Co kích thước xuống **0.95**, lớp phủ `HighlightOverlay` tăng độ sáng 20%, phát âm click nhẹ. Thời gian đáp ứng: **80 ms** (Cubic Ease Out).
   - Khi nhả (`PointerUp`): Bật nảy về **1.00** với hiệu ứng đàn hồi nhẹ (Over-shoot 1.02 rồi về 1.00 trong **160 ms**).
   - Khi ngón tay trượt ra ngoài (`PointerExit`): Tự động hồi phục về **1.00** mượt mà, giải phóng trạng thái giữ ngay lập tức.
2. **Hiệu ứng mở/đóng cửa sổ:**
   - Mở bảng (Open Window): Fade alpha 0 -> 1 kèm Scale 0.95 -> 1.00 trong **180 ms** (Ease Out Quint).
   - Đóng bảng (Close Window): Fade alpha 1 -> 0 trong **120 ms**.
3. **Chế độ Giảm chuyển động (Reduced Motion):** Khi bật tùy chọn này trong Cài đặt, loại bỏ hoàn toàn hiệu ứng scale, chỉ giữ chuyển đổi alpha nhanh (60 ms) để chống say tàu xe/chóng mặt.
