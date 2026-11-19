# Tiến độ "Giải bài tập" – 19/11/2026

Đã làm:
- Khảo sát: `docs/ngay-19-11/khao-sat-giai-bai-tap.md`, ảnh trước `docs/ngay-19-11/anh/giai-bai-tap/truoc_*.png`.
- Máy chủ: `server/ai/co_che/hoi_dap.py` + `api.py` – `/co-che/giai-thich-chi-tiet`, `/co-che/hoi-dap`, `/co-che/bao-sai`,
  ảnh cho `/co-che/giai-chuoi`. Test: `tests/test_co_che_hoi_dap.py` (đạt).
- Unity: `Scripts/Menu/GiaiBaiTap/KhungTrangGbt.cs` (khung trang cuộn, thanh phụ bước ngang, thẻ tab…),
  `Scripts/Menu/GiaiBaiTap/ManGiaiBaiTap.cs` (5 trang + hộp Báo sai); bỏ lớp ManGiaiBaiTap cũ trong ManChonLuong.cs.
  CoCheMoDun: bảng bên (Các bước / Giải thích chi tiết / Hỏi đáp), Xuất ảnh PNG, Báo sai, MoVoiBai/MoMau/DamBaoCo.
- Kiểm thử: KhktUiRunner.KiemGiaiBaiTap viết lại; KhktCoCheRunner thêm máy chủ giả + kiểm bảng bên, xuất ảnh, báo sai.

Xong 19/11: KhktUiVerify 66/66, KhktCoCheVerify 59/59, KhktHoaLyVerify 59/59, 76 test Python, app Mac xuất được; báo cáo `bao-cao-giai-bai-tap.md`.
Việc tiếp theo (nếu làm): nối tuỳ chọn mức độ/thông minh/tra cứu vào phần AI của giai_chuoi.py; chụp app thật từng trang khi đã cấp quyền ghi màn hình.
Chạy: `KHKT_UI_SHOTS=<thư mục> ~/AppBuild/VERA_claude/dong_bo_va_chay.sh ~/Developer/Antigravity KhktUiVerify.Run sau_ui`

## TẠM DỪNG 19/11 (luồng đáp án kiểu trợ lý AI) – hạn mức 5 giờ 76%

- Xong: tham khảo + thiết kế `docs/ngay-19-11/thiet-ke-luong-dap-an.md` (nguyên tắc, bố cục, chuyển động, kế hoạch mã 7 bước).
- Dở: chưa viết mã.
- Còn lại (theo mục 4 của tệp thiết kế): DongThoiGian.cs + KhoiDapAn, BangLon.cs (75% giữa màn, nền mờ, Esc), ChupHinh.cs
  (chụp giấy/3D thành ảnh), gắn vào 3 trang (ô đề dính đáy, tuỳ chọn gập), Cơ chế "Mở lớn" chứa khung 3D ⇄ giấy, kiểm thử
  KhktUiRunner + KhktCoCheVerify, xuất app Mac, báo cáo, commit. Không sửa MainMenu.cs (phiên khác đang làm lab/giải phẫu).

- 19/11 (làm tiếp sau TẠM DỪNG): đã viết xong luồng đáp án (DapAn.cs, gắn 3 trang, kiểm thử 72/72 + 66/66, app Mac). Xem báo cáo mục 8–9.

## TẠM DỪNG tối 19/11 (hạn mức 64%, mốc 70%)
- Xong: 3D kiểu CHEMISTRY, ô đề dính đáy, phím Enter/⌘↵, 3D nhúng trong bảng lớn Cơ chế (logic + kiểm thử 77/77), app Mac xuất.
- Dở: khung 3D trong bảng lớn hiện tối trong ảnh kiểm thử – kiểm `Xem3DNho` (khoảng cách camera, hiển thị RawImage), xem trong app thật bằng chuột.
