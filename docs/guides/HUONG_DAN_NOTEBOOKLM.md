# Hướng dẫn Kết nối và Quản lý Google NotebookLM Đa Tài Khoản

Tài liệu hướng dẫn quản trị viên cách kết nối, quản lý nhiều tài khoản Google NotebookLM, trực quan hóa trên sơ đồ tư duy (Mindmap) và liên kết kho tri thức với AI Riêng (Gia sư Hoá Hữu Cơ).

---

## 1. Giới thiệu & Cơ chế Hoạt động

- **Google NotebookLM** là công cụ nghiên cứu tài liệu AI của Google (dựa trên mô hình Gemini 1.5 Pro). NotebookLM cho phép tải lên tài liệu học tập (SGK, bài tập, đề thi, PDF, bài báo khoa học, video YouTube) để hỏi đáp có trích dẫn nguồn chính xác.
- Do NotebookLM bản thông thường chưa cung cấp API chính thức mở rộng ra bên ngoài, hệ thống sử dụng cơ chế RPC bảo mật (`batchexecute`) chạy **hoàn toàn ở phía máy chủ (Server-side)** qua cookie phiên đăng nhập.
- **Bảo mật tuyệt đối:** Cookie và token xác thực được mã hóa bằng chuẩn **AES (Fernet HKDF-SHA256)** trước khi lưu vào CSDL (`notebooklm_accounts`). Cookie **không bao giờ** được gửi về phía trình duyệt hay in ra log.
- Toàn bộ logic giao tiếp được bọc độc lập trong module `services/notebooklm/` để sẵn sàng nâng cấp lên NotebookLM Enterprise API chính thức trong tương lai mà không cần thay đổi giao diện hay các module khác.

---

## 2. Cách Lấy Cookie Google Thủ Công

### Cách 1: Lấy trực tiếp từ Trình duyệt (Chrome / Edge / Firefox) — Khuyến nghị
1. Mở trình duyệt và đăng nhập tài khoản Google của bạn tại [https://notebooklm.google.com](https://notebooklm.google.com).
2. Nhấn phím `F12` (hoặc chuột phải chọn **Kiểm tra / Inspect**) để mở Developer Tools.
3. Chuyển sang thẻ **Network (Mạng)**.
4. Tải lại trang NotebookLM (`F5` hoặc `Ctrl+R` / `Cmd+R`).
5. Trong danh sách các yêu cầu mạng, nhấp vào dòng đầu tiên (thường là `notebooklm.google.com` hoặc `projects`).
6. Ở khung bên phải, tìm mục **Request Headers (Tiêu đề yêu cầu)**.
7. Tìm dòng `Cookie:` (hoặc `cookie:`), bôi đen và **sao chép toàn bộ giá trị** phía sau (bắt đầu bằng `SID=...; HSID=...; __Secure-1PSID=...`).
8. Dán chuỗi này vào ô nhập Cookie trong trang Quản trị của app.

*(Mẹo: Bạn cũng có thể chuột phải vào dòng yêu cầu mạng → chọn **Copy** → **Copy as cURL** rồi dán thẳng vào ô nhập. Hệ thống sẽ tự động bóc tách cookie chính xác!)*

### Cách 2: Dùng Tiện ích Mở rộng (Cookie-Editor hoặc EditThisCookie)
1. Cài đặt tiện ích mở rộng [Cookie-Editor](https://cookie-editor.com/) hoặc [EditThisCookie](https://www.editthiscookie.com/) trên Chrome/Firefox.
2. Truy cập [https://notebooklm.google.com](https://notebooklm.google.com).
3. Nhấp vào biểu tượng tiện ích Cookie trên thanh công cụ trình duyệt.
4. Bấm nút **Export (Xuất)** → chọn **Export as JSON** (hoặc **Header String**).
5. Dán đoạn JSON vừa sao chép vào form thêm tài khoản trong app. App sẽ tự động phân tích và trích xuất các cookie cần thiết.

---

## 3. Quản lý Nhiều Tài khoản trong Admin

Truy cập trang Quản trị: `http://localhost:8000/admin` → Nhấp vào thẻ **NotebookLM – Tài khoản**.

### Các chức năng chính:
- **Thêm tài khoản mới:** Bấm nút **"+ Thêm tài khoản Google"**, đặt tên gợi nhớ (ví dụ: *Google Thầy Giáo*, *Google Nghiên Cứu Hoá 12*), dán cookie và lưu. Hệ thống tự động kiểm tra kết nối với Google, đọc email, tên hiển thị và ảnh đại diện Google.
- **Tài khoản Mặc định:** Nhấn nút **"★ Mặc định"** để đặt tài khoản chính được ưu tiên khi tra cứu hoặc giải bài. Thao tác hoàn thành tức thì không cần tải lại trang.
- **Đồng bộ lại:** Bấm **"↻ Đồng bộ"** để tải danh sách các notebook mới nhất, các nguồn tài liệu (PDF, Web, YouTube) và các ghi chú vào bộ nhớ đệm (cache) SQLite.
- **Cập nhật Cookie (Đăng nhập lại) / Đổi tên:** Nhấn nút **"✏ Sửa"** để cập nhật cookie mới khi cookie cũ hết hạn hoặc đổi tên gợi nhớ.
- **Xóa tài khoản:** Nhấn nút **"✕"** (hệ thống sẽ yêu cầu xác nhận để tránh bấm nhầm). Toàn bộ cookie mã hóa và cache của tài khoản sẽ được dọn dẹp sạch sẽ.

---

## 4. Xử lý khi Cookie Hết Hạn

- **Cơ chế cảnh báo tự động:** Hệ thống có cơ chế kiểm tra định kỳ sức khỏe kết nối. Khi Google thu hồi cookie hoặc phiên đăng nhập hết hạn, trạng thái tài khoản sẽ tự động chuyển thành **"Cần đăng nhập lại"** kèm cảnh báo màu vàng/đỏ nổi bật trên Admin và Mindmap, **tuyệt đối không làm sập ứng dụng**.
- **Cách xử lý:**
  1. Mở tab mới trên trình duyệt, truy cập lại [https://notebooklm.google.com](https://notebooklm.google.com).
  2. Lấy cookie mới theo **Mục 2** ở trên.
  3. Vào `/admin` → Thẻ **NotebookLM – Tài khoản** → Bấm nút **"✏ Sửa"** tại tài khoản bị hết hạn → Dán cookie mới → Bấm **"Lưu & Kết nối"**. Tài khoản sẽ ngay lập tức chuyển lại trạng thái **"Đang hoạt động"**.

---

## 5. Sử dụng trên Sơ đồ Tư duy (Mindmap)

Truy cập: `http://localhost:8000/admin/mindmap`.

- **Nhánh NotebookLM:** Nối trực tiếp từ nút gốc app với màu xanh nhận diện Google `#4285F4`.
- **Bộ chọn tài khoản linh hoạt:** Ở góc trái trên của Mindmap, bạn có thể chọn tài khoản từ dropdown hoặc chọn **"-- Hiện tất cả tài khoản --"**. Khi chuyển đổi, cây nhánh NotebookLM sẽ tự động tải lại mà không cần load lại toàn bộ trang.
- **Cấu trúc nhánh:**
  - Ở chế độ 1 tài khoản: `NotebookLM` → Các `Notebook` → Các `Nguồn tài liệu` (icon PDF đỏ, Web xanh, YouTube đỏ, Text ghi chú).
  - Ở chế độ "Tất cả tài khoản": `NotebookLM` → Các `Tài khoản Google` (mỗi tài khoản một màu riêng biệt) → Các `Notebook` → `Nguồn & Ghi chú`.
- **Bảng Drawer Chi tiết bên phải:** Nhấp chuột vào bất kỳ nút Notebook nào trên sơ đồ để mở bảng điều khiển:
  - Xem tóm tắt notebook, số nguồn tài liệu, số ghi chú.
  - Nút **"Mở trên Google NotebookLM ↗"** để truy cập trực tiếp notebook trên web của Google.
  - **Khung Hỏi đáp Trực tiếp:** Đặt câu hỏi trực tiếp với notebook ngay trên giao diện mindmap để kiểm tra dữ liệu mà không cần rời khỏi app.
  - Nút **"Dùng làm tài liệu cho AI Riêng"**: Nhập nội dung vào kho tri thức của AI Riêng.

---

## 6. Liên kết với AI Riêng (Gia sư Hoá Hữu Cơ)

1. **Nhập Notebook thành tài liệu AI Riêng:**
   - Trong drawer chi tiết notebook trên Mindmap, bấm **"Dùng làm tài liệu cho AI Riêng"**.
   - Toàn bộ tiêu đề nguồn, mô tả và ghi chú sẽ được tạo thành tài liệu tri thức trong bảng `ar_tai_lieu` và phân tách thành các đoạn trong `ar_doan`, tự động gắn nguồn trích dẫn `NotebookLM · <Tên tài khoản> (<Email>) · ID: <notebook_id>`.
   - Hệ thống tự động kích hoạt tính năng nhúng vector nền để AI Riêng tìm kiếm ngữ nghĩa khi giải chuỗi.
2. **Hỏi thêm NotebookLM khi giải chuỗi phản ứng:**
   - Khi giải chuỗi ở chế độ AI Riêng (qua pipeline hoặc API), bạn có thể bật tùy chọn `"hoi_notebooklm": true` và chỉ định `"nlm_notebook_id"`.
   - AI Riêng sẽ tự động tra cứu phản ứng, chất phản ứng, điều kiện xúc tác từ NotebookLM và đưa vào khối căn cứ hỗ trợ mô hình giải đúng đắn và chuẩn xác nhất.
