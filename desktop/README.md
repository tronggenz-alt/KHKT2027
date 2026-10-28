# Hướng dẫn sử dụng & Đóng gói Ứng dụng Máy tính CHEMISTRY

Ứng dụng máy tính độc lập (Desktop Application) cho hệ thống **CHEMISTRY** trên **macOS** (Apple Silicon) và **Windows**, bao gồm toàn bộ chức năng Hoá học & Sinh học 3D, phòng thí nghiệm ảo Web 3D và app Unity "Phòng Lab Hoá 3D" đóng gói sẵn bên trong.

---

## 1. Vị trí tệp cài đặt đã đóng gói

- **Thư mục xuất xưởng**: `~/AppBuild/CHEMISTRY/dist/`
  - `CHEMISTRY.app`: Ứng dụng macOS nguyên khối (chứa sẵn máy chủ backend Python và Unity Lab).
  - `CHEMISTRY-mac.dmg`: Bộ cài đặt đĩa ảnh macOS (**231 MB**, dung lượng tối ưu $\le 900$ MB).
  - `CHEMISTRY-mac.zip`: Bản nén di động macOS (**232 MB**).

---

## 2. Cài đặt và Mở ứng dụng lần đầu

### Trên macOS
1. Nhấp đúp vào tệp `CHEMISTRY-mac.dmg`.
2. Kéo biểu tượng **CHEMISTRY** vào thư mục **Applications** (Ứng dụng).
3. **Mở lần đầu (quan trọng do bản build ký tự do ad-hoc)**:
   - Nhấp chuột phải (hoặc giữ phím `Control` và bấm) vào `CHEMISTRY.app` trong thư mục Applications.
   - Chọn **Mở** (Open).
   - Tại hộp thoại cảnh báo của macOS Gatekeeper, bấm **Mở** (Open).
   - Các lần khởi chạy sau, bạn chỉ cần nhấp đúp bình thường hoặc mở từ Launchpad/Spotlight.

---

## 3. Hai chế độ vận hành

Ứng dụng hỗ trợ chuyển đổi linh hoạt ngay từ thanh công cụ hoặc menu **Chế độ**:

### A. Chế độ "Trên máy" (Mặc định - Offline hoàn chỉnh)
- Máy chủ Python cục bộ tự khởi động ngầm tại cổng ngẫu nhiên trong khoảng `50000–65000`.
- Toàn bộ thư viện đồ hoạ (Three.js, GSAP, Tailwind CSS, MathJax) được tải từ thư mục `vendor/` cục bộ. Bạn có thể sử dụng giải chuỗi, tìm cấu tạo, mô hình 3D, Lab Sinh học 3D và phòng lab ảo ngay cả khi không có kết nối Internet.
- **Dữ liệu riêng tư**: CSDL SQLite và lịch sử lưu độc lập tại `~/Library/Application Support/CHEMISTRY/`.

### B. Chế độ "Trực tuyến" (Đồng bộ tài khoản & AI dùng chung)
- Khi chuyển sang chế độ Trực tuyến, app kết nối tới máy chủ web (mặc định `https://giai-chuoi-huu-co.onrender.com` hoặc tên miền tuỳ chỉnh).
- **Đăng nhập một lần an toàn qua trình duyệt**:
  1. Trong app, chọn menu **Tài khoản** -> **Đăng nhập qua Trình duyệt**.
  2. Trình duyệt hệ thống (Chrome/Safari) tự động mở trang web. Sau khi bạn đăng nhập tài khoản (Email hoặc Google OAuth), hệ thống sinh một mã đăng nhập một lần (hiệu lực 5 phút).
  3. Trình duyệt tự động gọi deep-link `chemistry://dang-nhap?ma=...` để truyền phiên đăng nhập an toàn vào app máy tính mà không cần gõ lại mật khẩu.

---

## 4. Quản lý khoá AI an toàn (Chế độ Trên máy)

Trong chế độ Trên máy, các tính năng nâng cao (Chat trợ lý hoá học, giải thích hình ảnh Sinh học, cơ chế AI) sử dụng khoá cá nhân của bạn:
1. Mở menu **CHEMISTRY** -> **Cài đặt ứng dụng** (hoặc nhấn `Cmd+,`).
2. Nhập khoá API của bạn (Google Gemini, Anthropic Claude, hoặc OpenAI).
3. Bấm **Lưu khoá**.
4. Toàn bộ khoá được mã hoá bằng phần cứng Apple qua chuẩn `safeStorage` (Keychain trên macOS / DPAPI trên Windows). Tuyệt đối không lưu plaintext hay đưa vào mã nguồn.

---

## 5. Khởi chạy Phòng Lab Hoá 3D (Bản Unity)

- Ứng dụng đã đóng gói sẵn bản Unity `PhongLabHoa3D.app` bên trong `Contents/Resources/`.
- Để mở:
  - Bấm nút **"Mở bằng ứng dụng"** tại góc trên trang Phòng thí nghiệm 3D Web.
  - Hoặc chọn từ thanh Menu hệ thống: **Phòng Lab 3D** -> **Mở Phòng Lab Hoá 3D** (Phím tắt: `Cmd+Shift+L`).

---

## 6. Hướng dẫn đóng gói lại (Build from source)

### Yêu cầu môi trường
- macOS 12+ (Apple Silicon khuyên dùng)
- Python 3.10+ kèm virtual environment `.venv`
- Node.js 18+ và npm

### Lệnh đóng gói
Chạy kịch bản tự động duy nhất tại thư mục gốc của dự án:
```bash
bash desktop/build-mac.sh
```
Kịch bản sẽ tự động:
1. Tải vendor offline và sinh bộ icon Apple HIG (`.icns`, `.ico`).
2. Chạy PyInstaller onedir đóng gói backend Python và dữ liệu tĩnh.
3. Đưa app Unity vào tài nguyên ứng dụng.
4. Chạy `electron-builder` xuất bản DMG và ZIP.
5. Ký ad-hoc codesign tự động.

### Kiểm thử & Nghiệm thu tự động
Chạy kịch bản nghiệm thu 14 tính năng qua Playwright CDP:
```bash
./.venv/bin/python3 desktop/kiem_tra/kiem_tra_app.py
```

---

## 6b. Tự cập nhật

App tự hỏi máy chủ web bản mới nhất (`GET /api/app/phien-ban`, đọc `config/phien_ban_app.json`) 12 giây sau khi mở (tối đa 6 giờ / lần) và khi chọn
**Trợ giúp ▸ Kiểm tra cập nhật…**. Có bản mới → hộp thoại (ghi chú, dung lượng) → **Tải về và cài** → app tải vào thư mục Downloads,
**kiểm tra kích thước + SHA-256** (sai là xoá file, không mở) → mở file `.dmg` (macOS) / `.exe` (Windows) để bạn cài.

App **không tự thay chính nó**: macOS chỉ cho việc đó với app ký bằng chứng chỉ Apple Developer, mà bản này ký ad-hoc.
Chế độ "Trực tuyến" tự có giao diện mới khi web đổi; còn app (kể cả "Phòng Lab Hoá 3D" Unity nằm bên trong) chỉ đổi khi cài bản mới.

### Phát hành một bản cập nhật
1. Tăng `version` trong `desktop/package.json` (vd `1.0.1`), chạy `bash desktop/build-mac.sh` → `~/AppBuild/CHEMISTRY/dist/CHEMISTRY-mac.dmg`.
2. Tải `.dmg` lên nơi **công khai** thuộc danh sách cho phép: `github.com` (Releases của repo công khai) hoặc `giai-chuoi-huu-co.onrender.com`.
   (Muốn nơi khác: sửa `HOST_HOP_LE` trong `desktop/cap_nhat.js` **và** `HOST_TAI_VE_HOP_LE` trong `backend/cap_nhat_app.py`.)
3. Tạo bản phát hành (tự tính SHA-256 + kích thước):
   ```bash
   .venv/bin/python tools/phat_hanh_app.py --phien-ban 1.0.1 \
     --mac-arm64 ~/AppBuild/CHEMISTRY/dist/CHEMISTRY-mac.dmg \
     --url-mac-arm64 https://github.com/<tài-khoản>/<repo-công-khai>/releases/download/v1.0.1/CHEMISTRY-mac.dmg \
     --ghi-chu "Sửa lỗi phòng thí nghiệm"
   ```
   Thêm `--bat-buoc-tu 1.0.0` nếu mọi bản cũ hơn 1.0.0 buộc phải cập nhật.
4. Commit `config/phien_ban_app.json`, đưa web lên (`./dua-len-web.command`). Từ lúc đó app đang chạy sẽ thấy bản mới.

Không có file / sai dữ liệu → máy chủ trả `phien_ban: null` và app im lặng. Kiểm thử: `tests/test_cap_nhat_app.py`, `tests/js/cap_nhat_*.test.mjs`.

---

## 7. Gỡ cài đặt hoàn toàn

Để xoá sạch ứng dụng khỏi máy tính:
1. Kéo `CHEMISTRY.app` từ thư mục Applications vào Thùng rác.
2. Xoá thư mục dữ liệu và cấu hình cục bộ:
   ```bash
   rm -rf ~/Library/Application\ Support/CHEMISTRY
   ```
