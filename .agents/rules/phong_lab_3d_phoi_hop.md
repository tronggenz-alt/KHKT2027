# PHÒNG LAB SINH HỌC 3D – GEMINI VÀ CLAUDE ĐỀU SỬA TRỰC TIẾP, NHƯNG PHẢI PHỐI HỢP

Người dùng quyết định (26/10/2026): **bỏ luật "chỉ Claude được ghi"** (đặt ngày 17/10/2026). Gemini được sửa trực tiếp vùng dưới đây.
Luật cũ có là vì Gemini và Claude từng cùng sửa một file Phòng Lab 3D, gây xung đột. Các bước dưới đây để chuyện đó không lặp lại.

## Vùng áp dụng
- `frontend/biology-lab-3d.html` (bản v2 đã xoá ngày 26/10/2026)
- `frontend/sinh-hoc/` (mọi file: module giải thích từng bước, dữ liệu quy trình, dữ liệu 3D)
- `backend/sinh_hoc_ai.py`

## Trước khi sửa
1. `git status` và `git log -3 -- <file>` với từng file định sửa.
2. File đang có thay đổi **chưa commit** mà không phải của mình (của AI khác hoặc người dùng) → **dừng, báo người dùng**. Không sửa đè, không revert.
3. Tạo điểm phục hồi: `git add -A && git commit -m "chore: diem phuc hoi truoc <viec>"`.

## Trong khi sửa
- Các file này rất dài (`biology-lab-3d.html` ~7000 dòng): đọc theo khoảng dòng, sửa đúng chỗ. Không định dạng lại cả file, không đổi tên hàm hay biến đang dùng.
- Giữ nguyên API công khai `window.BioLab` (test và trang Hoá học nhúng đang dùng); chỉ thêm, không bớt.
- Không đổi hành vi chuột / cảm ứng / phím tắt hiện có nếu việc đang làm không yêu cầu.

## Sau khi sửa
1. Chạy `.venv/bin/python -m pytest -q tests/test_sinh_hoc*.py tests/test_kien_thuc_sinh_hoc.py` (và test riêng của việc đang làm).
2. Mở trang thật (`/sinh-hoc`) kiểm tra không lỗi console, mô hình vẫn hiện, FPS không tụt.
3. **Commit ngay**, không để thay đổi dở dang qua lượt làm việc sau. Ghi vào HANDOFF.md file nào, dòng nào đã sửa.

## Hàng đợi đề xuất
`.agents/hang-doi/sinh-hoc/` không còn bắt buộc. Vẫn dùng được khi muốn nhờ AI khác xem trước thay vì tự sửa
(mẫu trong README ở đó). Đề xuất cũ đã xử lý nằm ở `da-xu-ly/`.

Chốt chặn git `.githooks/commit-msg` đã tắt cùng ngày 26/10/2026 (lịch sử git của file đó còn bản cũ nếu muốn bật lại).
