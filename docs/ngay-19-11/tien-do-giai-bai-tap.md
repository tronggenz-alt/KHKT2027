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
- Khoanh vùng lỗi khung 3D trong bảng lớn (19/11 tối): điểm ảnh giữa khung đo được màu quầng electron (0,58; 0,47; 0,25) –
  phần **không cần đèn** (quầng, electron Unlit) có vẽ, còn **nguyên tử Lit ra đen** → gần như chắc là **thiếu ánh sáng**
  ở cảnh menu (cảnh lab có đèn nên trình phát Cơ chế không bị). Đèn riêng `Đèn 3D bảng` (cullingMask lớp 5) có thể bị URP
  bỏ qua (chỉ 1 đèn chính / không hỗ trợ culling mask). Hướng sửa lần sau, chọn một:
  1. Trong `Xem3DNho` dùng vật liệu nguyên tử không cần đèn (Unlit có tô bóng giả) hoặc bật Emission = 35% màu nguyên tử;
  2. Hoặc đặt `RenderSettings.ambientLight` sáng khi bảng mở và trả lại khi đóng.
  Rồi chạy KhktUiVerify (kiểm "Khung 3D trong bảng có hình phân tử" phải > 2000 điểm khác nền) và xem ảnh co_che_bang_lon_3d.
- Đã sửa (cho chạy tới 85%): khung 3D trong bảng lớn – RenderTexture 1600 theo tỉ lệ, căn khoảng cách camera, đèn chính riêng + tự phát sáng. Còn: xem trong app thật bằng chuột.

## Còn lại (20/11) – thiết kế để làm tiếp
1. **Nhiều AI cùng giải (nấc Tối đa)**: chạy song song Claude + Gemini pro (và Gemini flash nếu còn giờ) trong cùng giới hạn
   3 phút; mỗi lời giải qua RDKit (`kiem_tra_co_che`); chọn bản đạt kiểm tra có điểm cao nhất (ít bước sửa, đủ mũi tên, khớp
   sản phẩm); nếu hai bản đều đạt mà khác nhau → hỏi AI thứ ba làm trọng tài (≤ 30 s). Viết trong `giai_de.giai_de` bằng
   `asyncio.gather` + `asyncio.wait_for(…, muc["cho"])`, test giả lập 2 AI.
2. **"Tự luyện" bố cục tờ giấy cho rõ nhất**: không phải huấn luyện mô hình mà là **tối ưu bố cục có chấm điểm**, chạy lặp:
   điểm phạt = chữ chồng chữ, mũi tên cắt chữ/cắt nét, đầu mũi tên quá gần chữ, khoảng cách giữa các chất không đều, khung
   giai đoạn tràn; biến số = khoảng cách ngang giữa chất, hướng cong mũi tên (lên/xuống), độ cong, vị trí điều kiện trên/dưới
   mũi tên phản ứng. Chạy ở máy chủ khi dựng `so_do_giay` (thử ~200 phương án, giữ phương án ít điểm phạt nhất) và một bài kiểm
   thử đo điểm phạt trước/sau trên 8 bài mẫu + đề mới. Có thể chạy định kỳ trên kho đề đã giải để chỉnh trọng số phạt.
