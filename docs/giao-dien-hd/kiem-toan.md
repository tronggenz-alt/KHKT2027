# KIỂM TOÁN CHI TIẾT GIAO DIỆN HIỆN TRẠNG (VERA LAB HD)

Bảng kiểm toán toàn diện từng màn hình và hệ thống giao diện trong VERA Lab, lập danh sách các khiếm khuyết hiện có, kích thước thực tế so với chuẩn Apple HIG (44 pt), giải pháp khắc phục và mức độ ưu tiên xử lý.

---

## 1. MÀN HÌNH TOUCH HUD (`TouchHud.cs`)

| Thành phần nút | Kích thước hiện tại | Kích thước quy đổi (pt trên iPad) | Lỗi hiện có | Cách sửa | Độ ưu tiên |
|---|---|---|---|---|---|
| **Nút Tay chính (`MainActionButton`)** | 104 × 104 units | 71.0 × 71.0 pt | Đạt kích thước (>44 pt), nhưng không có hiệu ứng co giãn nhấn mượt; nhãn "Chạm / Giữ" hơi nhỏ (11 pt). | Thêm `NutHieuUng`, co giãn 0.96 khi ngón tay chạm, âm thanh `button_press`, giữ nguyên logic đổi màu trạng thái. | **Cao** |
| **Nút Trả về (`returnBtn`)** | 104 × 48 units | 71.0 × 32.8 pt | **Cao chỉ 32.8 pt** (< 44 pt chuẩn Apple). Chứa phím tắt PC `"Trả về (Q)"`. | Tăng chiều cao lên 65 units (44.4 pt). Đổi nhãn thành biểu tượng Undo + chữ `"Trả về"`. Bỏ `(Q)` trên iPad. | **Rất cao** |
| **Nút Lắc bình (`swirlBtn`)** | 88 × 52 units | 60.1 × 35.5 pt | **Cao 35.5 pt** (< 44 pt). Chứa phím tắt `"Lắc (F)"`. Nút ẩn hiện đột ngột không hiệu ứng mờ dần. | Mở rộng hitbox lên 65 units chiều cao. Đổi nhãn thành biểu tượng Lắc + `"Lắc"`. Hiệu ứng Fade 120 ms khi hiện. | **Rất cao** |
| **Nút Chậm / Nhanh (`shiftBtn`)** | 88 × 52 units | 60.1 × 35.5 pt | **Cao 35.5 pt** (< 44 pt). Nhãn `"Chậm"` không rõ trạng thái bật/tắt. Không có âm toggle. | Tăng chiều cao lên 65 units. Thêm chỉ báo đèn chấm LED xanh khi bật Chậm. Tích hợp âm `switch_toggle`. | **Cao** |
| **Nút Ngồi / Đứng (`sitBtn`)** | 88 × 48 units | 60.1 × 32.8 pt | **Cao 32.8 pt** (< 44 pt). Nhãn `"Ngồi (C)"` nhắc phím PC. | Tăng chiều cao lên 65 units. Đổi nhãn thành biểu tượng Tư thế + `"Ngồi / Đứng"`. | **Cao** |
| **Nút Tầm mắt Lên / Xuống** | 72 × 52 units | 49.2 × 35.5 pt | **Cao 35.5 pt** (< 44 pt). Ký tự `▲ ▼` phụ thuộc phông hệ thống, nút quá sát nhau dễ bấm nhầm. | Mở rộng chiều cao lên 65 units, tăng khoảng cách giữa 2 nút lên 12 pt. Dùng icon mũi tên chuẩn Lucide. | **Cao** |
| **Cụm điều khiển Buret** | 88 × 44 units (Mở/Đóng), 92 × 44 units (Khóa) | **Cao 30.1 pt** (< 44 pt). Nhãn `"Khóa (Space)"` nhắc phím cứng. Nút quá thấp so với ngón cái. | Tăng chiều cao nút lên 65 units (44.4 pt). Bỏ `(Space)`. Nút Mở/Đóng có thanh đo vạch độ mở buret trực quan. | **Rất cao** |
| **Bảng xoay lắp ráp (`FabPanel`)** | 92 × 44 units | **Cao 30.1 pt** (< 44 pt). Ký hiệu `⟲ ⟳` vẽ bằng font Segoe UI có thể lỗi hiển thị trên iOS. | Nâng kích thước nút lên 96 × 65 units. Nhúng icon vector xoay trái/phải sắc nét. | **Trung bình** |
| **Nút Ghi số đọc (`recordBtn`)** | 130 × 48 units | **Cao 32.8 pt** (< 44 pt). Nhãn `"Ghi số (Enter)"`. | Tăng chiều cao lên 65 units. Đổi thành `"Ghi số đọc"` + icon bút ghi. | **Trung bình** |
| **Nút Menu góc trên (`menuBtn`)** | 96 × 44 units | **Cao 30.1 pt** (< 44 pt). Quá nhỏ và sát góc màn hình. | Mở rộng lên 110 × 65 units, bo góc 16 pt, lùi vào trong Safe Area chuẩn 20 pt. | **Cao** |
| **Các nút chọn Trạm (`StationPanel`)** | 230 × 40 units | **Cao 27.3 pt** (< 44 pt). Chiều cao hàng quá thấp (40 units) khiến ngón tay dễ chạm đè 2 trạm cùng lúc. | Tăng chiều cao mỗi hàng lên 64 units (43.7 pt), khoảng cách giữa các hàng 8 units. | **Rất cao** |

---

## 2. MÀN HÌNH ĐẦU & CÀI ĐẶT (`IntroScreen.cs`)

| Thành phần | Lỗi hiện có | Cách sửa | Độ ưu tiên |
|---|---|---|---|
| **Tiêu đề & Thương hiệu** | Tên cũ: `"Phòng thí nghiệm Hóa học ảo"`. Mô tả cũ: `"720 hóa chất và 80 dụng cụ"`. Chưa phản ánh toàn bộ quy mô tòa nhà 2 tầng Hóa - Sinh VERA Lab. | Đổi thành: **"VERA Lab — Phòng Thí nghiệm Hóa học & Sinh học Ảo"**. Cập nhật số liệu chuẩn theo `baocao.md`: 2 tầng, 13 khu chuyên sâu, 720 hóa chất, 108 dụng cụ sinh học, 14 máy phân tích hiện đại. | **Rất cao** |
| **Dòng hướng dẫn phím cứng** | Hiển thị: `"Rê chuột nhìn · WASD đi · Chuột trái cầm, dùng · B tra cứu · T trợ lý AI · Esc menu"`. Gây khó hiểu cho học sinh dùng iPad. | Khi `PadInput.UsingTouch = true`: hiển thị: `"Vuốt nửa trái để đi · Vuốt nửa phải để xoay góc nhìn · Nút tròn để cầm và thao tác · Nút Menu góc trên để chuyển khu"`. | **Rất cao** |
| **Nút Cài đặt & Tiếp tục** | Kích thước 150 × 44 units (cao 30.1 pt < 44 pt). | Mở rộng lên 160 × 65 units, bo góc 16 pt, chữ đậm rõ ràng. | **Cao** |
| **Bảng Cài đặt hệ thống** | Chỉ có ô nhập URL máy chủ AI. Thiếu các tùy chọn điều chỉnh trải nghiệm cảm ứng trên iPad. | Bổ sung các công tắc tùy chọn: **Giảm chuyển động** (Reduced Motion), **Giảm độ trong suốt kính**, **Âm thanh giao diện** (Bật/Tắt), **Độ nhạy xoay góc nhìn**. | **Rất cao** |

---

## 3. BẢNG GIAO DIỆN PHÒNG THÍ NGHIỆM (`ModernUI.cs`, `ChemSearchUI.cs`, `RobotChatUI.cs`)

| Màn hình | Lỗi hiện có | Cách sửa | Độ ưu tiên |
|---|---|---|---|
| **Thanh gợi ý thao tác (`PromptBar`)** | Nhắc phím chuột máy tính: `"Nhấn Chuột trái để rót"`, `"Nhấn Q để trả về"`. Chữ bị nhòe khi đi qua hậu kỳ bloom. | Chuyển sang canvas Overlay hoặc cấu hình shader kính mờ độc lập không bị ảnh hưởng bởi render scale. Thay văn bản theo thiết bị: `"Chạm nút TAY để rót"`, `"Chạm nút Trả về"`. | **Cao** |
| **Thẻ thông tin dụng cụ & Kính lúp** | Nút đóng `✕` quá nhỏ (32 × 32 units = 21.8 pt), học sinh rất khó bấm tắt trên iPad. | Mở rộng nút đóng lên tối thiểu 65 × 65 units, vùng chạm an toàn. | **Cao** |
| **Bảng tra cứu hóa chất (`ChemSearchUI`)** | Ô nhập tìm kiếm và danh sách hóa chất có thanh cuộn nhỏ, khó vuốt bằng ngón tay. | Mở rộng chiều cao các mục kết quả lên tối thiểu 65 units, thanh cuộn rộng 24 units dễ kéo chạm. | **Trung bình** |
| **Khung chat robot AI (`RobotChatUI`)** | Nút gửi tin nhắn nhỏ, khi bàn phím ảo iOS hiện lên có nguy cơ che mất ô nhập chat. | Đảm bảo nút gửi >= 65 units. Tính toán offset tự động theo `TouchScreenKeyboard.area` để đẩy khung nhập lên trên bàn phím iOS. | **Cao** |

---

## 4. MÀN HÌNH 14 MÁY PHÂN TÍCH & TẦNG 2 (`InstrumentScreen.cs`, Tầng 2)

| Màn hình | Lỗi hiện có | Cách sửa | Độ ưu tiên |
|---|---|---|---|
| **Màn hình 14 máy (`InstrumentScreen` + `*Program`)** | Nút thoát góc trên ghi `"Thoát (Esc)"` nhỏ (36 units). Sơ đồ nguyên lý và đồ thị có chữ nhỏ (12 pt). | Tăng nút đóng thành nút màu đỏ bo tròn kích thước 65 × 65 units với biểu tượng `✕` rõ nét. Thang chữ thông số máy tối thiểu 15 pt. Giữ nguyên 100% dữ liệu mô phỏng backend. | **Cao** |
| **Kính hiển vi quang học (`MicroscopeController`)** | Một số nút chỉnh tiêu cự thô/tinh và đổi vật kính còn vẽ bằng `OnGUI` cũ, kích thước không chuẩn trên Retina iPad. | Chuyển các nút điều khiển kính sang uGUI với `GlassKit`, nút xoay ốc vi cấp/thô có phản hồi chạm mượt và âm thanh nảy nấc cơ học. | **Cao** |
| **Bàn ghép gen & Sinh học phân tử** | Nút thao tác pipet, giếng điện di ADN có kích thước nhỏ. | Nâng chuẩn nút lên >= 65 units, bố trí vừa vặn tầm tay ngón cái. | **Trung bình** |
