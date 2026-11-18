# Tiến độ: Cơ chế từ đề bài mới

Lần sau tiếp tục thì đọc tệp này trước. Báo cáo chi tiết: `bao-cao-co-che-tu-de.md`.

## Đã xong
1. Máy chủ: `server/ai/co_che/giai_de.py`, `POST /co-che/giai-de` (`api.py`), `nap_env` (`may_chu.py`). Đã thêm Gemini (REST) làm dự phòng khi Claude không có khóa hoặc bị 401/403.
2. `tests/test_co_che_giai_de.py`: 14/14 đạt, `tests/test_co_che_api.py` vẫn đạt.
3. App: bảng "Đề mới" (`CoCheMoDun.cs`), hộp chọn ảnh (`ChonTepHeThong.cs`: Mac, Windows, Editor; iPad để sau), `MayChuCoChe.GiaiDe`, phần kiểm thử trong `KhktCoCheRunner.cs`.
4. `KhktCoCheVerify.Run`: **52/52 đạt**, 0 lỗi runtime, FPS 665. Sau khi chạy, `.mat` và ảnh g2..g4 không bị đổi.
5. Commit đầu: 076d786f. Test Gemini giả lập: 18/18 đạt (commit sau).

## Đang làm / còn lại (theo thứ tự)
1. ~~Test giả lập Gemini~~ (xong).
2. Thử khóa Gemini bằng một lệnh nhỏ (không in giá trị khóa).
3. Thử thật 1 đề chữ và 1 ảnh đề (ảnh tự tạo ở scratchpad, `anh_de_thu.png`: Câu 2, (CH3)3CBr + H2O).
   - Khóa Claude trong `.env` đang bị 401. Phiên chính đã báo người dùng thay khóa; phiên này không sửa `.env`.
4. Điền kết quả vào `bao-cao-co-che-tu-de.md`, chỗ đang ghi KET_QUA_UNITY và KET_QUA_THU_THAT.
5. Xóa `StreamingAssets/CoChe/mau_kiem_thu.bin` (tệp này git bỏ qua), rồi commit.

## Lệnh kiểm thử
```bash
.venv/bin/python -m unittest tests.test_co_che_giai_de tests.test_co_che_api
VERA_GHI_GOI_MAU="$PWD/Phong thi nghiem ao/Assets/StreamingAssets/CoChe/mau_kiem_thu.bin" .venv/bin/python -m unittest tests.test_co_che_dong_goi
~/AppBuild/VERA_claude/dong_bo_va_chay.sh ~/Developer/Antigravity KhktCoCheVerify.Run co_che_giai_de
python -m server.ai.co_che.may_chu   # rồi POST http://127.0.0.1:8010/co-che/giai-de
```
