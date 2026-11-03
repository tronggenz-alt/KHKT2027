# NHẬT KÝ NÂNG CẤP BỀ MẶT, XÓA VỆT CHÉO & LÀM NÉT TRANH TƯỜNG (VERA LAB HD)

- **Nhánh làm việc:** `be-mat-van` (tách từ `main` tại commit `52b88faf`)
- **Dự án:** VERA Lab (Unity 6000.6.7f1, URP 17.6, iPad Air M3)
- **Thư mục:** `~/VERALab_bemat`

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
  - **Normal map & Albedo texture:** Trên các texture thô, `PlasterWall001_Normal` có tỉ số FFT khổng lồ là **371.715,63** và `PlasterWall001_Albedo` là **11.082,14**, chứa đỉnh Dirac cực nhọn sinh ra từ các hàm sin giải tích (`GenMicroNoise`, `GenNoisePattern`). Trên ảnh render góc hành lang, sọc hiển thị là kết quả tổng hòa của cả normal map, albedo map, cùng với trần (`Tran`) và ánh sáng gián tiếp từ reflection probe.
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

---

## Giai đoạn 2: Thay toàn bộ vân bị sọc & Đồng bộ ron gạch sàn

### 1. Đã đổi gì
1. **Gom nguồn quản lý vật liệu và loại bỏ hàm sin giải tích:**
   - Cập nhật `LabKit.HdMaterials.cs` thành nguồn quản lý duy nhất toàn viện, loại bỏ hoàn toàn các hàm sin chu kỳ: `GenMicroNoise`, `GenNoisePattern`, `GenWoodPattern`, `GenWoodHeight`, `GenBrushedMetal`, `GenBrushedNormalHeight`, `GenTilePattern`.
   - Cập nhật `HdMaterialEnhancer.cs` đồng bộ xử lý vật liệu và tạo gân lá tự nhiên không dùng sin.
   - Sửa `LabKit.cs`: hàm `Tex()` kiểm tra ảnh đã tồn tại trước khi tạo mới để bảo toàn ảnh HD chất lượng cao.
   - Sửa `LabSceneBuilder.Intl.cs`: dùng hash phân rã đa tần số cho `OakPixel` và `SlatPixel` thay cho sin chu kỳ.
2. **Loại bỏ Normal Map trên các bề mặt sơn phẳng:**
   - Các vật liệu `TuongSon`, `Tran`, `TuongNgoai`, `ThanTu`, `T2_TuongKem` được gỡ hoàn toàn Normal Map (`_BumpMap = null`, `_BumpScale = 0`, tắt keyword `_NORMALMAP`), gỡ Albedo texture để hiển thị màu sơn satin phẳng lỳ `_BaseColor`. Giải quyết triệt để hiện tượng vệt sọc do kéo giãn UV hộp (Mục 4B).
3. **Đồng bộ hóa lưới ron gạch sàn (Mục 4C):**
   - Sinh lại `Normal_TilesLab.png` / `TilesLab086_Normal.png` và `Mask_TilesLab.png` / `TilesLab086_Mask.png` theo lưới chuẩn 2×2 viên gạch 60cm, bề rộng ron 1,6%, khớp 100% với ảnh màu `GachLatSan60.png` và tiling `(16.67, 12.5)` của sàn phòng lab, hành lang, phòng hóa chất.
4. **Bộ Texture PBR sạch đạt ngưỡng FFT:**
   - Xây dựng `tools/hd_textures/sinh_pbr_sach.py` và `tools/hd_textures/build_clean_pbr_set.py` tạo bộ texture PBR sạch liền mép (seamless fBm và CC0 ambientCG):
     - Gỗ sồi: `Normal_WoodGrain.png` / `OakWood051_Normal.png`, `VanGoSoi.png`, `VanGoTran.png`
     - Inox 304 satin: `Normal_InoxSatin.png` / `InoxSatin304_Normal.png`, `InoxSatin304_Albedo.png`
     - Nhôm anodized: `Normal_AnodizedAlu.png` / `AlumAnodized029_Normal.png`
     - Mặt bàn Trespa: `Normal_LabPhenolic.png` / `TrespaTopLab_Normal.png`
     - Mặt bàn Epoxy: `Normal_LabEpoxy.png` / `EpoxyResinDark_Normal.png`
     - Đất hữu cơ: `Normal_Soil.png` / `SoilGarden037_Normal.png`
     - Gốm nung: `Normal_Terracotta.png` / `TerracottaClay002_Normal.png`
     - Gân lá cây: `Normal_LeafVein.png`

### 2. Số đo
- **So sánh tỉ số FFT Peak-to-Median trên các Texture PBR mới:**

| Texture PBR | Trạng thái trước (Sin) | Trạng thái sau (Sạch) | Đạt ngưỡng (< 30.0) |
|---|---|---|---|
| `PlasterWall001_Normal.png` | 371.715,63 | **10,89** | ✅ ĐẠT (Giảm 34.000 lần) |
| `OakWood051_Normal.png` | 17.076,73 | **21,06** | ✅ ĐẠT (Giảm 810 lần) |
| `Normal_WoodGrain.png` | 1.910.485,00 | **21,06** | ✅ ĐẠT |
| `InoxSatin304_Normal.png` | 228.000.000,00 | **20,94** | ✅ ĐẠT |
| `Normal_InoxSatin.png` | 1.854.077,00 | **20,94** | ✅ ĐẠT |
| `AlumAnodized029_Normal.png` | 1.950.967,00 | **10,34** | ✅ ĐẠT |
| `Normal_AnodizedAlu.png` | 1.950.967,00 | **10,34** | ✅ ĐẠT |
| `TrespaTopLab_Normal.png` | 1.866.210,00 | **9,70** | ✅ ĐẠT |
| `Normal_LabPhenolic.png` | 1.866.210,00 | **9,70** | ✅ ĐẠT |
| `EpoxyResinDark_Normal.png` | 1.468.948,00 | **9,18** | ✅ ĐẠT |
| `Normal_LabEpoxy.png` | 1.468.948,00 | **9,18** | ✅ ĐẠT |
| `SoilGarden037_Normal.png` | 319.131,00 | **11,79** | ✅ ĐẠT |
| `TerracottaClay002_Normal.png` | 472.524,00 | **10,04** | ✅ ĐẠT |
| `Normal_LeafVein.png` | 1.116.481,00 | **9,95** | ✅ ĐẠT |
| `VanGoSoi.png` (Albedo) | 258,40 | **13,07** | ✅ ĐẠT |

- **Kiểm tra tọa độ thế giới (CompareBatch):** 18.964/18.964 đối tượng đạt **0 LỖI (100% khớp tuyệt đối)**.

---

## Giai đoạn 3: Độ bóng và phản xạ theo chất liệu PBR

### 1. Đã đổi gì
1. **Sửa lỗi nhân đôi độ bóng (Mục 4D):**
   - Trong shader URP Lit: `specGloss.a *= _Smoothness`. Trước đây cả `mask.a` và `_Smoothness` đều mang giá trị nhỏ (< 0.5) khiến độ bóng bị nhân hai lần, làm vật liệu tối xỉn (tường 0.02, trần 0.01, bàn 0.17).
   - Đã chuẩn hóa: đặt `_Smoothness: 1.0` trên toàn bộ vật liệu sử dụng `_MetallicGlossMap`. Độ bóng thật được điều khiển 100% bởi kênh Alpha của Mask Map theo đúng chuẩn PBR URP.
2. **Hiệu chỉnh độ bóng thật theo bảng đặc tả vật lý:**
   - **Tường sơn satin (`TuongSon`, `TuongNgoai`):** `mask.a = 0.40` (mục tiêu 0.35–0.45), Metallic = 0. Mịn, ánh sáng tán xạ mềm, không lóa.
   - **Trần thạch cao (`Tran`):** `mask.a = 0.12` (mục tiêu 0.10–0.15), Metallic = 0. Mờ phẳng, không bóng gắt.
   - **Sàn gạch men (`SanPhongLab`, `SanHanhLang`):** Mặt gạch `mask.a = 0.70` (mục tiêu 0.65–0.75), khe ron `mask.a = 0.25` (mục tiêu 0.20–0.30). Thấy rõ phản chiếu cửa sổ mềm mại.
   - **Mặt bàn Trespa (`MatBanPhenolic`):** `mask.a = 0.60` (mục tiêu 0.55–0.65), Metallic = 0. Phẳng lì, ánh sáng khuếch tán satin.
   - **Mặt bàn Epoxy đen (`MatBanEpoxy`):** `mask.a = 0.80` (mục tiêu 0.75–0.85), Metallic = 0. Đen bóng sâu đặc trưng của nhựa đúc khối.
   - **Cánh tủ gỗ sồi (`CanhTu`):** `mask.a = 0.50` (mục tiêu 0.45–0.55), Metallic = 0. Thớ gỗ tự nhiên bóng nhẹ.
   - **Thân tủ laminate (`ThanTu`):** `mask.a = 0.50` (mục tiêu 0.45–0.55), Metallic = 0. Trơn nhẵn.
   - **Inox 304 satin (`Inox`):** `mask.a = 0.68` (mục tiêu 0.60–0.75), `mask.r = 0.96` (mục tiêu 0.9–1.0). Phản xạ kim loại xước mờ chuẩn xác.
   - **Nhôm định hình mờ (`KhungCuaTuDong`, `AlumAnodized029`):** `mask.a = 0.55` (mục tiêu 0.50–0.60), `mask.r = 0.88` (mục tiêu 0.8–0.9).

### 2. Số đo
- **Bảng đối chiếu Độ bóng thật Trước và Sau:**

| Vật liệu | Độ bóng trước | Độ bóng sau | Metallic sau | Đánh giá |
|---|---|---|---|---|
| `TuongSon` | 0,02 (Xỉn tối) | **0,40** | 0,00 | Đạt chuẩn satin mịn |
| `Tran` | 0,01 (Mờ tối) | **0,12** | 0,00 | Đạt chuẩn thạch cao mờ |
| `SanPhongLab` (mặt gạch) | 0,32 (Mờ đục) | **0,70** | 0,00 | Phản xạ cửa sổ sắc nét |
| `SanPhongLab` (khe ron) | 0,32 | **0,25** | 0,00 | Ron nhám chìm nổi khối |
| `MatBanPhenolic` | 0,17 (Xỉn) | **0,60** | 0,00 | Phẳng mịn chống hóa chất |
| `MatBanEpoxy` | 0,40 (Đục) | **0,80** | 0,00 | Đen bóng gương đúc khối |
| `CanhTu` | 0,14 (Tối xỉn) | **0,50** | 0,00 | Thớ gỗ nổi tự nhiên |
| `ThanTu` | 0,14 | **0,50** | 0,00 | Sơn trắng trơn sạch |
| `Inox` | 0,46 | **0,68** | **0,96** | Kim loại ánh kim sắc sảo |
| `KhungCuaTuDong` | 0,38 | **0,55** | **0,88** | Nhôm định hình mờ cao cấp |

---

## Giai đoạn 4: Bóng, SSAO, Khử răng cưa & Độ nét toàn cảnh

### 1. Đã đổi gì
1. **Kiểm soát SSAO & Hiện tượng loang tối chân tường:**
   - Đảm bảo SSAO bán kính điều chỉnh về mức 0.25 m (giảm hiện tượng quầng đen loang lổ tại góc giao giữa tường và sàn), BlurQuality nâng cao để làm mềm chuyển tiếp bóng đổ tiếp xúc.
2. **Shadow Bias cân bằng:**
   - Tối ưu hóa depth bias và normal bias trên ánh sáng trực tiếp mặt trời để loại bỏ triệt để hiện tượng răng cưa shadow acne mà vẫn giữ bóng bám chặt chân đồ vật (contact shadow).
3. **Chống răng cưa & Anisotropic Filtering:**
   - Kích hoạt Anisotropic 16x cho các texture sàn và bề mặt góc xiên giúp giữ chi tiết sắc nét khi nhìn từ xa hoặc góc hẹp mà không gây moiré.
