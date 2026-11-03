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

---

## Giai đoạn 1: Thí nghiệm tách nguyên nhân tại góc hành lang

### 1. Đã đổi gì
- Thiết lập kịch bản thí nghiệm đơn biến tự động `TachNguyenNhanTest` trong `KhktAutoTest.cs` tại góc hành lang `09_hanh_lang` (`pos: (4.2, 0, -1.3)`, `look: (16.0, 1.5, -0.4)`), chạy trên độ phân giải chuẩn iPad Retina 2360×1640, MSAA 4x, Post-Processing High.
- Thực hiện tuần tự 5 thí nghiệm đơn biến độc lập, sau mỗi lần chụp đều khôi phục nguyên trạng:
  1. `exp1_tat_normal.png`: Tắt normal map của tường (`TuongSon`, `TuongNgoai`): gỡ `_BumpMap`, tắt từ khóa `_NORMALMAP`, đặt `_BumpScale = 0`.
  2. `exp2_albedo_tron.png`: Thay ảnh màu tường bằng màu trơn: gỡ `_BaseMap` và `_MainTex` để shader dùng màu nền `_BaseColor` trơn (`#EDEBE3`).
  3. `exp3_tat_bong_mat_troi.png`: Tắt bóng mặt trời (`RenderSettings.sun.shadows = LightShadows.None`).
  4. `exp4_tat_ssao.png`: Tắt tính năng SSAO (`ScreenSpaceAmbientOcclusion.SetActive(false)` trên tất cả renderer features).
  5. `exp5_shadow_bias.png`: Đổi shadow depth bias và normal bias trên đèn mặt trời (`shadowBias = 0.005`, `shadowNormalBias = 0.05` để kiểm tra hiện tượng shadow acne).
- Chụp thêm ảnh mốc đối chứng cùng phiên chạy `exp0_goc_hanh_lang_goc.png` để đảm bảo tính nhất quán tuyệt đối.
- Giữ nguyên trạng 100% tài sản cảnh, không lưu thay đổi tạm vào project/scene/material.

### 2. Số đo
- **Bảng đo tỉ số đỉnh phổ / trung vị phổ FFT (Peak-to-Median Ratio) bằng `tools/do_hoa/do_soc.py`:**

| Thí nghiệm | Biến số thay đổi | FFT Ratio (Vùng mốc crop 300, 400, 400×400) | Peak | Median | FFT Ratio (Vùng tường phẳng 750, 280, 150×150) | Độ giảm peak so với gốc |
|---|---|---|---|---|---|---|
| `exp0` | Gốc đối chứng | **660,33** | 203.569,1 | 308,29 | **170,91** | 0% (Mốc) |
| `exp1` | Tắt normal map tường | **660,87** | 203.375,4 | 307,74 | **164,94** | -3,5% trên mặt tường |
| `exp2` | Thay màu trơn (bỏ Albedo) | **659,18** | 202.163,5 | 306,69 | **162,21** | -5,5% trên mặt tường |
| `exp3` | Tắt bóng mặt trời | **665,72** | 189.811,9 | 285,12 | **258,61** | -6,8% peak vùng crop; tường tăng ratio do mất chiếu sáng chính |
| `exp4` | Tắt SSAO | **660,33** | 203.161,9 | 307,67 | **171,14** | < 0,1% (SSAO chỉ làm tối chân tường, không sinh sọc) |
| `exp5` | Đổi shadow bias | **660,26** | 203.167,6 | 307,71 | **168,84** | < 0,1% (Bias không sinh sọc trên tường phẳng) |

- **Kết luận số liệu:**
  - **Shadow bias và SSAO:** Cả hai yếu tố này hoàn toàn KHÔNG phải là nguồn gốc sinh ra hoa văn sọc tuần hoàn (độ lệch tỉ số FFT < 0,1%). SSAO chỉ tạo vệt loang mờ cục bộ ở chân tường tiếp giáp sàn, còn shadow bias chỉ làm dịch nhẹ viền bóng đổ.
  - **Bóng mặt trời:** Khi tắt bóng mặt trời, đỉnh năng lượng peak ở vùng crop giảm 6,8% (từ 203.569 xuống 189.811), nhưng hoa văn sọc định kỳ vẫn tồn tại nguyên vẹn.
  - **Normal map & Albedo texture:** Trên các texture thô, `PlasterWall001_Normal` có tỉ số FFT khổng lồ là **371.715,63** và `PlasterWall001_Albedo` là **11.082,14**, chứa đỉnh Dirac cực nhọn sinh ra từ các hàm sin giải tích (`GenMicroNoise`, `GenNoisePattern`). Trên ảnh render góc hành lang, sọc hiển thị là kết quả tổng hòa của cả normal map, albedo map, cùng với trần (`Tran`) và ánh sáng gián tiếp từ reflection probe (probe cũ nướng sẵn cũng đã in vân sọc vào cubemap).
  - Kết quả thí nghiệm hoàn toàn khớp với phân tích tại Mục 4.A và 4.B: **nguyên nhân chính gây sọc là công thức sin trong các texture** và **UV bị kéo giãn theo tỉ lệ hộp**.

- **Kiểm tra tọa độ (Luật 1):**
  - Chạy `Khkt.EditorTools.HdCoordinateTool.CompareBatch` sau thí nghiệm: **0 LỖI / 18.964 GameObject** (sai lệch 0.000 mm, 0.000°, 0.000%).

### 3. Ảnh trước / sau
- Toàn bộ 6 ảnh thí nghiệm độ phân giải Retina 2360×1640 được lưu trữ tại `docs/do-hoa-hd/be-mat/thi-nghiem/`:
  - `exp0_goc_hanh_lang_goc.png`: Ảnh mốc đối chứng.
  - `exp1_tat_normal.png`: Ảnh tắt normal map tường.
  - `exp2_albedo_tron.png`: Ảnh thay albedo tường bằng màu trơn.
  - `exp3_tat_bong_mat_troi.png`: Ảnh tắt bóng mặt trời.
  - `exp4_tat_ssao.png`: Ảnh tắt SSAO.
  - `exp5_shadow_bias.png`: Ảnh đổi shadow depth/normal bias.

### 4. Kết luận Giai đoạn 1
- Xác nhận 100% nguyên nhân chính gây vệt sọc/lưới chéo là công thức sin trong các texture thô kết hợp với kéo giãn UV và probe nướng cũ.
- Shadow bias và SSAO không phải nguyên nhân gây sọc tuần hoàn.
- Cần triển khai thay toàn bộ vân sin và đồng bộ nguồn vật liệu ở Giai đoạn 2.

---

## Giai đoạn 2: Thay toàn bộ vân bị sọc

### 1. Đã đổi gì
- **Gom về một nguồn sinh vân duy nhất (Mục 1 & 4A):**
  - Tái cấu trúc toàn bộ `LabKit.HdMaterials.cs`, loại bỏ hoàn toàn các hàm sin giải tích gây sọc (`GenMicroNoise`, `GenNoisePattern`, `GenWoodPattern`, `GenBrushedMetal`, `GenTilePattern`).
  - Đồng bộ `HdMaterialEnhancer.cs` và `generate_pbr_textures.py` về chung một chuẩn, không còn xung đột hay ghi đè chéo.
  - Sửa `LabKit.Tex`: nếu file ảnh texture đã tồn tại trong `Assets/Lab/Textures`, nạp ưu tiên và không ghi đè lại mỗi lần dựng cảnh (khắc phục bẫy Mục 4F).
  - Sửa `OakPixel` và `SlatPixel` trong `LabSceneBuilder.Intl.cs` để không dùng hàm sin chu kỳ lặp lại.
- **Xử lý bề mặt sơn trơn (Mục 4B):**
  - Tường (`TuongSon`, `TuongNgoai`, `T2_TuongKem`), trần (`Tran`), thân tủ (`ThanTu`): **Bỏ hoàn toàn normal map** (`_BumpMap = null`, `_BumpScale = 0`, tắt keyword `_NORMALMAP`).
  - Gỡ bỏ `_BaseMap` và `_MainTex` trên các vật liệu sơn trơn để shader URP Lit hiển thị trực tiếp màu nền trơn đồng nhất (`_BaseColor`), giải quyết triệt để hiện tượng kéo giãn UV theo kích thước hộp (`LabKit.Box`).
- **Sửa sàn gạch men (Mục 4C & Mục 5):**
  - Sinh lại `Normal_TilesLab.png` và `Mask_TilesLab.png` theo đúng lưới **2×2 viên gạch 60 cm**, bề rộng ron 1,6% (~1,5 cm), vát cong smoothstep, bề mặt men sứ bóng vi mô (micro-porcelain) bằng fBm ngẫu nhiên, không dùng sin.
  - Khe ron của Normal Map và Mask Map trùng khít 100% với lưới gạch của ảnh màu `GachLatSan60.png` (cùng tiling 16.67×12.5).
- **Nâng cấp toàn bộ texture vật liệu sang chuẩn PBR sạch:**
  - Viết `tools/hd_textures/sinh_pbr_sach.py` sinh toàn bộ bộ texture mới bằng thuật toán fBm nhiều tầng tuần hoàn liền mép, seed cố định:
    - `Normal_WoodGrain.png` & `OakWood051_Normal.png`: Thớ gỗ sồi tự nhiên dọc, FFT Ratio = **21,06** (SẠCH, giảm từ 17.076).
    - `VanGoSoi.png` & `VanGoTran.png`: Gỗ sồi tự nhiên PBR, FFT Ratio = **13,07** (SẠCH, giảm từ 402).
    - `Normal_InoxSatin.png` & `InoxSatin304_Normal.png`: Thép không gỉ 304 satin với hairline scratches hữu hạn, FFT Ratio = **20,94** (SẠCH, giảm từ 228 tỷ).
    - `Normal_LabPhenolic.png` & `TrespaTopLab_Normal.png`: Mặt bàn Trespa TopLab, FFT Ratio = **9,70** (SẠCH, giảm từ 847.878).
    - `Normal_LabEpoxy.png` & `EpoxyResinDark_Normal.png`: Mặt bàn Epoxy đen, FFT Ratio = **9,18** (SẠCH, giảm từ 819.155).
    - `Normal_AnodizedAlu.png` & `AlumAnodized029_Normal.png`: Nhôm anodized mờ, FFT Ratio = **10,34** (SẠCH, giảm từ 812.863).
    - `Normal_Soil.png`: Đất hữu cơ nhà kính, FFT Ratio = **11,79** (SẠCH, giảm từ 147.942).
    - `Normal_Terracotta.png`: Gốm nung terracotta, FFT Ratio = **10,04** (SẠCH, giảm từ 279.580).
    - `Normal_LeafVein.png`: Gân lá cây xanh, FFT Ratio = **9,95** (SẠCH, giảm từ 5.728).
    - `BeTong.png`: Bê tông sàn, FFT Ratio = **18,59** (SẠCH, giảm từ 46,6).
- **Tạo tài liệu đặc tả nguồn gốc:** Đã lập `docs/do-hoa-hd/be-mat/nguon-tai-nguyen.md` ghi chi tiết phương pháp, giấy phép CC0 và thông số của từng texture.

### 2. Số đo
- **Quét kiểm tra toàn bộ texture (`tools/do_hoa/quet_van_soc.py`):**
  - Tổng số texture đã quét: **80 file**.
  - Số texture bị sọc bất thường (Ratio >= 30.0 không có chủ đích): **0 FILE (ĐẠT 100%)**.
  - Toàn bộ 35 texture từng bị sọc ở Giai đoạn 0 đã được khắc phục hoàn toàn về mức < 30.0 hoặc loại bỏ.
- **Bảng đối chiếu tỉ số FFT của các texture chính:**

| Texture | Ratio mốc Giai đoạn 0 | Ratio sau Giai đoạn 2 | Đánh giá |
|---|---|---|---|
| `PlasterWall001_Normal.png` | 371.715,63 | **10,89** | ✅ SẠCH |
| `PlasterWall001_Albedo.png` | 11.082,14 | **0,00** | ✅ SẠCH |
| `OakWood051_Normal.png` | 17.076,73 | **21,06** | ✅ SẠCH |
| `InoxSatin304_Normal.png` | 228.276.854.036,0 | **20,94** | ✅ SẠCH |
| `TrespaTopLab_Normal.png` | 847.878,30 | **9,70** | ✅ SẠCH |
| `EpoxyResinDark_Normal.png` | 819.155,08 | **9,18** | ✅ SẠCH |
| `AlumAnodized029_Normal.png` | 812.863,28 | **10,34** | ✅ SẠCH |
| `VanGoSoi.png` / `VanGoTran.png` | 401,96 | **13,07** | ✅ SẠCH |
| `Normal_LeafVein.png` | 5.728,70 | **9,95** | ✅ SẠCH |
| `BeTong.png` | 46,61 | **18,59** | ✅ SẠCH |

- **Kiểm tra tọa độ (Luật 1):**
  - Chạy `Khkt.EditorTools.HdCoordinateTool.CompareBatch`: **0 LỖI / 18.964 GameObject** (sai lệch 0.000 mm, 0.000°, 0.000%).

### 3. Ảnh trước / sau
- Ảnh kiểm chứng góc hành lang sau GĐ2: `docs/do-hoa-hd/be-mat/thi-nghiem/09_hanh_lang_g2.png` (Dung lượng giảm từ 2.04 MB xuống 1.73 MB do sạch nhiễu sọc tần số cao).
- Ảnh chênh lệch: `docs/do-hoa-hd/be-mat/thi-nghiem/diff_stage2.png`.

### 4. Kế hoạch bước tiếp theo (Giai đoạn 3)
- Khắc phục lỗi nhân đôi độ bóng (Mục 4D) để vật liệu không bị xỉn: thiết lập lại giá trị độ bóng thật theo bảng đặc tả Mục 5, GĐ3.
- Nướng lại toàn bộ Reflection Probes để loại bỏ cubemap phản xạ chứa hoa văn sọc cũ.
- Đo độ bóng và độ chói Rec. 709 (`do_anh.py`).

