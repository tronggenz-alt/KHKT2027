# Mốc gốc ngày 18/11/2026 (P0)

Ghi lúc 12:35 ngày 18/11/2026 trên MacBook M2, RAM 8 GB, macOS 27.2.1. Cuối ngày (C5) so kết quả với mốc này.

## Tóm tắt

| Mục | Kết quả |
|---|---|
| Ổ trống | 12,2 GB sau khi chạy mốc Wave6 (đầu ngày 14,8 GB) |
| RAM trống | Khoảng 30% theo `memory_pressure`. Swap đã dùng 1,8/3 GB |
| Mốc Wave6 (`KhktWave6Verify.Run`) | **67/67 PASS**, 0 FAIL, 0 lỗi runtime, 0 `error CS`, 12 ảnh |
| Test Python (`python -m unittest discover -s tests`) | 381 test: **204 đạt**, 77 FAIL, 100 ERROR |
| rdkit | 2026.09.2, cài mới vào `.venv` (Python 3.15.0) hôm nay |
| ollama | 0.40.16, `ollama serve` chạy thường trực qua Homebrew. Mô hình có sẵn: `bge-m3:latest` (1,2 GB) |
| Cổng 8000 | Trống, không tiến trình nào giữ |
| LaunchAgent `vn.giaichuoihuuco.app` | Lỗi lặp lại: mã thoát 3, đã khởi động lại 3.957 lần. Xem mục 5 |

## 1. Git

- `main`, `n18/antigravity` và `n18/co-che` cùng ở `d5defe05`. Thư mục chính `~/Developer/Antigravity` đang ở nhánh `n18/antigravity`. Chưa đẩy lên GitHub.
- Đầu ngày thư mục chính có 53 tệp `Assets/Lab/Materials/*.mat` bị sửa lúc 17/11 22:39. Trong đó 27 vật liệu mất `_EMISSION` và có `_SrcBlend` đổi từ 5 thành 1, nên màn hình, đèn LED, đèn trạng thái không còn phát sáng. Các thay đổi này đã được bỏ, không commit. Nếu hôm nay `.mat` lại tự đổi sau khi bấm Play, thì nghi có mã đang ghi thẳng vào vật liệu dùng chung (`sharedMaterial`).
- Còn 5 mục chưa theo dõi, git cũng không bỏ qua chúng: `tai_khoan_admin.txt`, `data/email_tam_thoi.txt`, `data/ai_rieng/`, `data/geoip/`, `phong_sinh/PROMPT_CONG_SINH_HOC_3D.md`. Kho đang công khai, **không được commit** `tai_khoan_admin.txt` và `data/email_tam_thoi.txt`.

## 2. Mốc Wave6 trên bản sao của Claude

- Bản sao: `~/AppBuild/VERA_claude`, tạo bằng `cp -cR` (bản sao APFS, tốn khoảng 20 MB lúc tạo). Đồng bộ từ commit `d5defe05`.
- Sau lần chạy đầu, Unity ghi lại khoảng 0,7 GB trong `Library/` của bản sao. Swap cũng tăng từ 3 lên 4 GB. Kết quả: ổ còn 12,2 GB, đầu ngày là 14,8 GB.
- Lệnh: `~/AppBuild/VERA_claude/dong_bo_va_chay.sh <worktree> KhktWave6Verify.Run w6_moc_goc_18-11`
- Kết quả: `[W6_SUMMARY] passes=67 failures=0 runtimeErrors=0 startupSearchIgnored=0 shots=12`, `[W6_RESULT] ALL PASSED`. Unity thoát mã 0. Chạy từ 12:27:39 tới 12:30:35 (3 phút). Log: `$TMPDIR/w6_moc_goc_18-11.log`, ảnh ở `$TMPDIR/vera_w6_shots/`.

Script `dong_bo_va_chay.sh <thư mục worktree> <Lớp.Hàm> [tên log]`:
- rsync `Assets/` (có `--delete`), `ProjectSettings/`, `Packages/`, `data/`. So bằng nội dung (`-c`) nên tệp giống nhau không bị ghi lại, đỡ tốn ổ.
- Tệp trong `Assets/` mà git bỏ qua (6 tệp `Lab/Tang2/GiaiPhau/BP3D2_*`) được giữ lại. rsync sẽ in cảnh báo `GiaiPhau: not empty, cannot delete`, cảnh báo này vô hại.
- Đang có Unity batchmode khác thì đợi, cứ 2 phút xem lại. Log ở `$TMPDIR/<tên log>.log`.

## 3. Test Python

Mọi lỗi nằm trong 5 tệp test máy chủ: `test_dang_nhap` (45 FAIL, 8 ERROR), `test_lop_hoc` (32 FAIL), `test_truc_tiep` (33 ERROR), `test_buoi_hoc` (31 ERROR), `test_trinh_chieu` (28 ERROR).
Các test khác đều đạt.

- Lỗi chung: đăng nhập trả về `401 {"detail":"Cần đăng nhập"}`, nên các test sau không lấy được `ma_phien` (92 lỗi `KeyError: 'ma_phien'`).
- Có lỗi `'_IncludedRouter' object has no attribute 'path'`. `.venv` đang có FastAPI 0.162.4 và Starlette 1.10.0, trong khi `requirements.txt` chỉ ghi `fastapi>=0.110`. Nghi lỗi do thư viện đã lên phiên bản mới, chưa xác minh.

## 4. Script canh gác ngủ

- `~/AppBuild/canh_gac_ngu.sh` chạy nền từ 12:18, log ở `~/AppBuild/canh_gac_ngu.log`. Tắt bằng `pkill -f canh_gac_ngu.sh`.
- Script kiểm tra 5 phút một lần. Nó chỉ cho máy ngủ khi bạn đã không chạm phím/chuột ≥ 15 phút và có một trong ba điều: (a) có tệp `~/AppBuild/XONG_NGAY_18-11`; (b) 90 phút không có tệp nào đổi và không có Unity/xcodebuild/ollama; (c) đã qua 19/11 01:00. Trước khi ngủ, script báo một thông báo, đợi 2 phút, và hủy nếu bạn chạm máy trong lúc đó.
- "Unity" chỉ tính Unity Editor, không tính Unity Hub. "ollama" chỉ tính khi đang chạy mô hình (tiến trình runner), vì `ollama serve` chạy thường trực.
- `~/Developer/VERA_du_lieu_rieng` chưa có. Script bỏ qua thư mục này cho tới khi nó được tạo.
- Lưu ý: lúc 12:2x, thời gian không chạm máy luôn là 0 giây vì hệ thống nhận tín hiệu liên tục từ một đầu thu USB (chuột). Nếu chuột vẫn gửi tín hiệu khi không ai dùng, máy sẽ không tự ngủ. Xem dòng "nghỉ tay" trong log.

## 5. Cổng 8000 và LaunchAgent

- `lsof -i :8000`: không có gì.
- `vn.giaichuoihuuco.app`: lỗi lặp lại (KeepAlive, cứ 10 giây khởi động lại một lần). Log `~/Documents/Antigravity/logs/tu-chay-app.log` (17 MB) báo `Could not import module "backend.main"`. `~/Documents/Antigravity` đang ở nhánh `mediapipe`, không có `backend/main.py`.
- `vn.giaichuoihuuco.tunnel` vẫn chạy.
- Chỉ xem, không sửa.

## 6. Ổ đĩa: những thứ dọn được (CHƯA xóa, phải hỏi trước)

Ổ còn 12,2 GB, sát ngưỡng 12 GB. Khi xuống dưới ngưỡng thì hỏi người dùng trước khi dọn:

| Thứ | Dung lượng | Ghi chú |
|---|---|---|
| `~/Library/Caches/Google` | 1,3 GB | Cache Chrome/Google |
| `.claude/worktrees/antigryvity-app-update-b35dd8` | 875 MB | Đã gộp vào main, sạch. Phải xác nhận phiên Claude dùng nó đã xong |
| `~/Library/Caches/Homebrew` | 39 MB | |
| `~/Library/Caches/pip` | 29 MB | |
| uv | không có cache | |

Không được dọn:
- `.claude/worktrees/one-day-completion-plan-7f5a50`: chứa `docs/ngay-18-11/KE_HOACH_1_NGAY.md`, tệp này chưa commit.
- `~/.gemini/antigravity/worktrees/Antigravity/tai_xuong_va_ket_hop`: chưa gộp vào main.
- Library/ và Builds/ của Unity, `~/AppBuild/VERALab_ui`, cache Unity, `/Applications/Unity`, `~/Documents/Antigravity`, `~/Developer/Antigravity-luu-ban-cu`, `~/CHEMISTRY_xem`.
