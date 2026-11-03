# NHẬT KÝ NÂNG CẤP BỀ MẶT, XÓA VỆT CHÉO & LÀM NÉT TRANH TƯỜNG (VERA LAB HD)

- **Nhánh làm việc:** `nang-cap-be-mat` (tách từ `main` tại commit `52b88faf`)
- **Dự án:** VERA Lab (Unity 6000.6.7f1, URP 17.6, iPad Air M3)
- **Thư mục:** `~/VERALabHD`

---

## Giai đoạn 0: Chuẩn bị, khóa mốc tọa độ và thiết lập thước đo FFT

### 1. Đã đổi gì
- Khởi tạo nhánh `nang-cap-be-mat` từ nhánh `main` (`52b88faf`).
- Đặt tag mốc gốc `be-mat-g0` tại commit đầu tiên chứa tài liệu đặc tả `docs/PROMPT_NANG_CAP_BE_MAT.md`.
- Chạy đối chiếu tọa độ tự động toàn diện qua `Khkt.EditorTools.HdCoordinateTool.CompareBatch`, xác nhận đạt mốc gốc **0 LỖI** trên toàn bộ 18.964 GameObject.
- Chụp đầy đủ bộ ảnh **TRƯỚC** (44 ảnh độ phân giải gốc iPad 2360×1640) bằng bài thử nghiệm `KHKT_TEST=do_hoa`, lưu toàn bộ vào `docs/do-hoa-hd/be-mat/truoc/`. Bộ ảnh bao gồm đầy đủ các góc máy bắt buộc:
  - Hành lang có cửa sổ và dãy chân dung (`09_hanh_lang.png`, `15_hanh_lang_lich_su.png`)
  - Cận bàn thí nghiệm có tủ gỗ (`03_ban_hoc_sinh.png`, `c04_canh_ban_inox.png`)
  - Tường video có bàn giáo viên (`13_tuong_video_gan.png`, `02_tuong_bang.png`)
  - Sàn phòng lab nhìn xiên (`c08_san_nhin_xien.png`)
  - Trần phòng lab (`18_tran_go_hang_ban.png`)
  - Nhà kính thực vật (`l08_nha_kinh.png`)
  - Hành lang tầng 2 (`l09_hanh_lang_t2.png`)
  - Phòng sinh học (`l03_sinh_toan_canh.png`, `l04_sinh_ban_hoc_sinh.png`)
- Xây dựng công cụ phân tích hoa văn lặp bằng biến đổi Fourier 2D `tools/do_hoa/do_soc.py` (tính tỉ số giữa đỉnh phổ cao nhất và trung vị phổ biên độ sau khi khử thành phần DC bán kính r = 5 và áp cửa sổ 2D Hanning).
- Chạy kiểm tra đối chứng (control benchmark) và chốt ngưỡng:
  - `PlasterWall001_Normal.png` : Ratio = **371.715,63** -> **CÓ SỌC** (ĐẠT mốc đối chứng)
  - `OakWood051_Normal.png`     : Ratio = **17.076,73**  -> **CÓ SỌC** (ĐẠT mốc đối chứng)
  - `Normal_Plaster.png`        : Ratio = **11,52**      -> **SẠCH** (ĐẠT mốc đối chứng)
  - **CHỐT NGƯỠNG PHÂN ĐỊNH:** Ngưỡng cố định là **30.0** (Ratio < 30.0 là SẠCH; Ratio >= 30.0 là CÓ SỌC). Ngưỡng này được khóa vĩnh viễn và không thay đổi trong suốt quá trình nâng cấp.
- Quét tự động toàn bộ 78 file texture trong `Assets/Lab/HD/Textures` và `Assets/Lab/Textures`, phát hiện 35 texture bị sọc bất thường do các hàm toán học sin tạo ra (`GenMicroNoise`, `GenNoisePattern`, `GenWoodPattern`, `GenBrushedMetal`, `GenTilePattern`), lập danh sách chi tiết các vật liệu đang bị ảnh hưởng và lưu tại `docs/do-hoa-hd/be-mat/danh_sach_van_soc.json`.

### 2. Số đo
- **So sánh tọa độ thế giới (CompareBatch):** 18.964/18.964 đối tượng. Độ lệch: 0.000 mm, 0.000°, 0.000%. Tổng số lỗi: **0 LỖI (ĐẠT)**.
- **Thước đo FFT trên ảnh vân đối chứng:**
  - `PlasterWall001_Normal.png`: 371.715,63 (Peak = 2.767.957,0; Median = 7,45)
  - `OakWood051_Normal.png`: 17.076,73 (Peak = 1.110.899,5; Median = 65,05)
  - `Normal_Plaster.png`: 11,52 (Peak = 22.441,0; Median = 1.947,84)
- **Thước đo FFT trên vùng cắt thực tế từ ảnh chụp render mốc trước (`truoc/`):**
  - Tường hành lang (`09_hanh_lang.png`, crop 400×400): **660,31** (Sọc nặng)
  - Trần phòng lab (`18_tran_go_hang_ban.png`, crop 400×400): **1.443,47** (Sọc nặng)
  - Mặt bàn học sinh (`03_ban_hoc_sinh.png`, crop 400×400): **985,79** (Sọc nặng)
- **Hiệu năng nền trên iPad Air M3 (mức Ultra):**
  - FPS trung bình: Trạm nhẹ đạt 68–127 FPS, trạm nặng (Kho hóa chất, Khu an toàn sinh học) đạt 35–54 FPS.
  - RAM sử dụng: Đỉnh 1,4–2,8 GB.

### 3. Ảnh trước / sau
- Toàn bộ 44 ảnh góc nhìn mốc ban đầu (2360×1640) đã được lưu trữ an toàn tại thư mục: `docs/do-hoa-hd/be-mat/truoc/`.

### 4. Lỗi còn lại
- 35 file texture bị dính hoa văn sọc tuần hoàn do công thức sin.
- Bề mặt tường, trần, bàn, cánh tủ trong ảnh chụp có tỉ số sọc cao (660–1440) và độ bóng bị nhân đôi khiến vật liệu xỉn màu.
- Sẽ thực hiện thí nghiệm tách nguyên nhân đơn biến ở Giai đoạn 1.
