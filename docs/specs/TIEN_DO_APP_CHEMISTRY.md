# TIẾN ĐỘ ĐÓNG GÓI APP MÁY TÍNH CHEMISTRY (macOS / Windows)

> Theo dõi thực hiện từng giai đoạn của `PROMPT_XUAT_APP_CHEMISTRY.md`.
> Cập nhật sau mỗi giai đoạn kèm mã commit.

## Danh sách công việc

- [x] **GĐ1. Khảo sát + Điểm phục hồi** (Hoàn thành - Commit `2da69c4` & `c022645`)
  - [x] Chạy baseline test (`pytest -q` 605/605 PASS, `node --test tests/js/` 48/48 PASS)
  - [x] Commit điểm phục hồi `chore: diem phuc hoi truoc xuat app CHEMISTRY`
  - [x] Khảo sát routes & lập bảng `desktop/DANH_SACH_TINH_NANG.md` (20 tính năng)
  - [x] Rà soát các yếu tố phụ thuộc máy (font, RDKit, GeoIP, STORAGE_DIR, COOKIE_SECURE, OAuth, sao lưu/tunnel)

- [x] **GĐ2. Kiến trúc app (Electron + Máy chủ Python PyInstaller)** (Hoàn thành)
  - [x] Khởi tạo thư mục `desktop/` và cấu hình `desktop/package.json` (Electron, electron-builder)
  - [x] Viết `desktop/main.js` (cửa sổ, menu, quản lý máy chủ, single instance, deep link)
  - [x] Viết `desktop/preload.js` (an toàn contextIsolation, window.chemistryApp)
  - [x] Viết `desktop/may_chu/khoi_dong.py` (chạy Uvicorn ở cổng trống 127.0.0.1, báo cổng ra stdout)
  - [x] Viết `desktop/may_chu/chemistry.spec` cho PyInstaller onedir
  - [x] Cập nhật `backend/config.py` đọc biến `CHE_DO_APP`, `CHE_DO_CUC_BO`, `STORAGE_DIR`, tắt sao lưu/tunnel
  - [x] Quản lý cấu hình khoá AI người dùng bằng safeStorage Keychain

- [x] **GĐ3. Hai chế độ: Trên máy / Trực tuyến** (Hoàn thành - 6/6 tests PASS)
  - [x] Viết `tools/tai_vendor.py` tải các thư viện CDN (Three.js, GSAP, MathJax, Tailwind...) về `frontend/vendor/`
  - [x] Tạo `frontend/vendor/PHIEN_BAN.json` có SHA-256 và kích thước
  - [x] Cập nhật hàm `_trang()` trong `backend/main.py` thay URL CDN bằng `vendor/` khi `CHE_DO_APP=1`
  - [x] Thêm các routes cấp và đổi mã đăng nhập: `POST /api/app/ma-dang-nhap`, `POST /api/app/doi-ma`
  - [x] Viết tests cho luồng đăng nhập app trong `tests/test_app_auth.py` (611/611 pytest PASS)

- [x] **GĐ4. Gộp app Unity "Phòng Lab Hoá 3D" vào trong** (Hoàn thành)
  - [x] Tích hợp `PhongLabHoa3D.app` vào `Contents/Resources/` của CHEMISTRY.app (sẵn sàng qua build script)
  - [x] Cập nhật trang phòng thí nghiệm: nút "Mở bằng ứng dụng" gọi `window.chemistryApp.moAppLab(tn)`
  - [x] Menu ứng dụng "Phòng Lab 3D" mở trực tiếp app Unity (Cmd+Shift+L)

- [x] **GĐ5. Hoàn thiện như app thật** (Hoàn thành)
  - [x] Menu hệ thống macOS đầy đủ (CHEMISTRY, Tệp, Đi tới, Chế độ, Cửa sổ, Trợ giúp, Bảng tuần hoàn)
  - [x] Cửa sổ Giới thiệu bản quyền tài nguyên (BodyParts3D, Sketchfab, PubChem, OFL fonts, RDKit, Three.js, Unity)
  - [x] Tạo icon app đa kích cỡ `assets/icon.icns` và `assets/icon.ico` (hình bình tam giác chuẩn Apple HIG)
  - [x] Ghi nhớ kích thước/vị trí cửa sổ, hộp thoại lưu file của hệ điều hành (will-download)
  - [x] Thông báo hệ thống khi giải chuỗi hoàn tất
  - [x] Chế độ Demo dùng Chrome máy (channel="chrome"), tự ẩn nếu không có
  - [x] Ghi nhật ký máy chủ vào `STORAGE_DIR/logs/may_chu.log`, xem nhật ký từ menu Trợ giúp

- [x] **GĐ6. Kịch bản Build + Ký** (Hoàn thành)
  - [x] Viết `desktop/build-mac.sh` tự động hoá: build ngoài iCloud tại `~/AppBuild/CHEMISTRY/`, PyInstaller onedir, nạp vendor, đóng gói Unity app, electron-builder arm64, codesign ad-hoc
  - [x] Viết `desktop/build-win.ps1` sẵn sàng cho Windows
  - [x] Tạo bản phân phối `CHEMISTRY-mac.dmg` (231 MB <= 900 MB) và `CHEMISTRY-mac.zip` (232 MB)
  - [x] Đã ký ad-hoc codesign và kiểm tra tính toàn vẹn ứng dụng

- [x] **GĐ7. Nghiệm thu bằng app thật (Playwright Electron / CDP)** (Hoàn thành)
  - [x] Viết `desktop/kiem_tra/kiem_tra_app.py` kiểm tra mọi trang trong `DANH_SACH_TINH_NANG.md`
  - [x] Chạy kiểm thử offline: 100% tài nguyên hiển thị trơn tru không lỗi CDN
  - [x] Đo đạc thời gian mở app: 4.31 giây (vượt chuẩn <= 8.0s), RAM rảnh: 352.66 MB
  - [x] Kiểm tra dọn dẹp: 0 tiến trình mồ côi sau khi đóng app
  - [x] Chụp và mở xem trực tiếp bằng mắt toàn bộ 14 ảnh nghiệm thu trong `outputs/app-chemistry/`
  - [x] Toàn bộ test suites cũ và mới PASS 100% (611/611 pytest, 48/48 node test)

- [x] **GĐ8. Bàn giao & Tài liệu** (Hoàn thành)
  - [x] Viết `desktop/README.md` (hướng dẫn cài đặt, mở lần đầu, chế độ trên máy/trực tuyến, quản lý khoá AI, gỡ cài đặt)
  - [x] Cập nhật `HANDOFF.md` và `CLAUDE.md`
  - [x] Báo cáo tổng kết và hoàn thành mục tiêu `PROMPT_XUAT_APP_CHEMISTRY.md`
