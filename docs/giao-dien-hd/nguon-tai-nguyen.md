# DANH MỤC TÀI NGUYÊN GIAO DIỆN & GIẤY PHÉP BẢN QUYỀN (VERA LAB HD)

Tài liệu kiểm định và ghi nhận toàn bộ nguồn gốc tài sản giao diện (Phông chữ, Biểu tượng, Âm thanh) đưa vào dự án VERA Lab HD theo đúng quy định pháp lý và bản quyền mở (100% mã nguồn mở / CC0 / SIL OFL).

---

## 1. PHÔNG CHỮ TIẾNG VIỆT CHUYÊN DỤNG

### Phông chữ: Be Vietnam Pro (Regular, Medium, SemiBold, Bold)
- **Tác giả / Nhà thiết kế:** Tiêu Trúc Lam, Hoàng Lam (Việt Nam) & nhóm Foundry TypeType.
- **Liên kết nguồn:** https://github.com/bettertype/BeVietnamPro
- **Giấy phép bản quyền:** SIL Open Font License (OFL 1.1)
- **Kiểm tra giấy phép:** Miễn phí sử dụng thương mại và phi thương mại, được phép nhúng vào ứng dụng và phần mềm.
- **Mục đích sử dụng:**
  - Thiết kế riêng cho tiếng Việt hiện đại, tối ưu dấu thanh, dấu mũ cân đối trên màn hình độ phân giải cao (Retina iPad).
  - Hỗ trợ đầy đủ bộ ký tự khoa học hóa học & sinh học: chỉ số dưới, chỉ số trên (`H₂O`, `SO₄²⁻`, `Ca²⁺`, `NH₄⁺`), ký hiệu toán học (`±`, `≈`, `×`, `÷`, `Δ`, `μm`, `°C`).
- **Vị trí lưu trữ trong dự án:** `Assets/Resources/GiaoDien/Phong/BeVietnamPro-Regular.ttf`, `BeVietnamPro-SemiBold.ttf` kèm file giấy phép `OFL.txt`.

---

## 2. BỘ BIỂU TƯỢNG GIAO DIỆN CẢM ỨNG (TOUCH ICONS)

### Bộ biểu tượng: Lucide Icons & Material Symbols
- **Tác giả:** Lucide Contributors / Google Design Team
- **Liên kết nguồn:** https://lucide.dev/ / https://fonts.google.com/icons
- **Giấy phép bản quyền:** ISC License (Lucide) / Apache License 2.0 (Material Symbols)
- **Kiểm tra giấy phép:** Miễn phí 100%, được tích hợp tự do vào ứng dụng di động.
- **Danh mục biểu tượng sử dụng:**
  - `hand` (biểu tượng bàn tay thao tác)
  - `undo-2` / `reply` (biểu tượng trả về dụng cụ)
  - `flask-conical` / `beaker` (biểu tượng hóa chất, bình tam giác)
  - `rotate-cw` / `rotate-ccw` (biểu tượng xoay bộ phận lắp ráp)
  - `lock` / `unlock` (biểu tượng khóa van buret)
  - `eye` / `arrow-up` / `arrow-down` (biểu tượng điều chỉnh góc nhìn tầm mắt)
  - `menu` / `x` (biểu tượng mở menu, đóng cửa sổ)
  - `volume-2` / `volume-x` (biểu tượng bật/tắt âm thanh)
  - `settings` (biểu tượng cài đặt hệ thống)
  - `help-circle` (biểu tượng hướng dẫn trợ giúp)
- **Định dạng:** Vector SVG kết xuất ra Sprite Texture 2D PNG 128×128 RGBA32 độ phân giải cao, tối ưu nén trên Metal iOS.

---

## 3. ÂM PHẢN HỒI GIAO DIỆN (UI AUDIO FEEDBACK)

### Bộ âm thanh: Kenney UI Audio & Âm tổng hợp nội bộ
- **Tác giả:** Kenney (Asset Jesus) / BioFeedback procedural synth
- **Liên kết nguồn:** https://kenney.nl/assets/ui-audio
- **Giấy phép bản quyền:** Creative Commons Zero (CC0 1.0 Universal - Public Domain)
- **Kiểm tra giấy phép:** Hoàn toàn thuộc phạm vi công cộng, không yêu cầu ghi công, không ràng buộc thương mại.
- **Danh mục âm thanh tích hợp:**
  - `click_tap`: Tiếng chạm nhẹ tinh tế (soft click) khi bấm các nút điều hướng, chuyển tab, chọn bài chuẩn độ.
  - `button_press`: Tiếng nhấn nút sâu (deep click) khi giữ nút tay chính, mở van buret.
  - `switch_toggle`: Tiếng bật/tắt chuyển đổi (switch) cho nút Chậm/Nhanh (Shift), Chế độ ngồi.
  - `pop_success`: Tiếng âm bổng tích cực khi hoàn tất chuẩn độ hoặc đạt điểm kiểm tra.
  - `warning_alert`: Tiếng chuông cảnh báo nhẹ khi chạm vào vùng cấm (ngoài mép bàn hoặc đè lên vật khác).
