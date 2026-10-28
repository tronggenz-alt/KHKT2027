# Đề xuất: Sửa lỗi chuyển tab từ Sinh học sang Hoá học
- Người đề xuất: Gemini
- Người dùng yêu cầu: "hãy fix lại lỗi chuyển từ sinh học sang hóa học k dc"

## Mục tiêu
- Khi người dùng ở trang `/sinh-hoc` hoặc `/sinh-hoc-v2` bấm vào nút tab "Hoá học" trên thanh tiêu đề, trình duyệt chuyển ngay về trang Hoá học (`/`).
- Bổ sung CSS cho `.tab-hoa-hoc` và ẩn `#bo-cuc` để hỗ trợ chế độ mở trực tiếp file offline (`file://`).

## File & vị trí
- `frontend/biology-lab-3d.html`:
  - Thêm CSS `.tab-hoa-hoc` và `#khung-hoa-hoc`
  - Nút `#tab-btn-hoa`: thêm sự kiện onclick chuyển trang khi giao thức http/https
  - Hàm `chonTab(hoa)`: chuyển hướng sang `/` khi `hoa === true` trên máy chủ web, ẩn `#bo-cuc`
- `frontend/biology-lab-3d-v2.html`:
  - Nút `#tab-btn-hoa`: thêm onclick điều hướng
  - Hàm `chonTab(hoa)`: thêm kiểm tra chuyển hướng sang `/`

## Cách kiểm tra
- Chạy kiểm thử Playwright với Google Chrome: mở `http://127.0.0.1:8000/sinh-hoc` và `http://127.0.0.1:8000/sinh-hoc-v2`, bấm `#tab-btn-hoa`, kiểm tra URL chuyển về `http://127.0.0.1:8000/`.
- Chạy `pytest -q tests/test_sinh_hoc.py tests/test_sinh_hoc_v2.py`: 10/10 test pass.

---
**Trạng thái**: ĐÃ ÁP DỤNG
- Kết quả: Đã sửa đồng bộ trong `biology-lab-3d.html` và `biology-lab-3d-v2.html`.
