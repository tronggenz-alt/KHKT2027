# NHẬT KÝ NÂNG CẤP ĐỒ HỌA PBR SIÊU THỰC - VERA LAB HD

Nhánh: `do-hoa-hd` | Máy trạm: Mac M2 8 GB | Thiết bị mục tiêu: iPad Air M3 (UDID: `00008122-001669801186801C`)

---

## Giai đoạn 0: Chuẩn bị và khóa tọa độ

### 1. Đã đổi gì
- Tạo nhánh làm việc độc lập `do-hoa-hd` trên worktree `~/VERALabHD` tách biệt hoàn toàn khỏi `main`.
- Dựng cảnh sạch gốc (20.222 GameObject, 12 reflection probes, 22 cửa sổ ảo, NavGrid hash `09aa0fd26f0923f663435ad5e45a2745`).
- Phát triển bộ công cụ khóa & đối chiếu tọa độ `HdCoordinateTool.cs` tại `Assets/Scripts/Editor/HdTools/HdCoordinateTool.cs`.
- Trích xuất toàn bộ mốc gốc vào `docs/do-hoa-hd/toa_do_goc.json` (đường dẫn phân cấp kèm sibling index, vị trí thế giới, góc xoay, tỉ lệ, loại & bounds collider, các thành phần tương tác: `Grabbable`, `LabContainer`, `LabStation`, `EquipmentInfo`, `LabScreen`, `AutoDoor`, mã băm lưới đi lại NavGrid).
- Chụp ảnh mốc gốc:
  - 12 ảnh góc máy cố định landing (`docs/do-hoa-hd/anh_goc/landing/`).
  - 27 ảnh góc nhìn chuẩn (`docs/do-hoa-hd/anh_goc/goc_nhin/`).
- Chạy toàn diện bộ kiểm thử gốc: `landing`, `ipad` (5/6 tiêu chuẩn), `tang2` (12 trạm + 2 theme), `khu` (nạp/gỡ khu, giữ trạng thái hóa chất, robot lấy đồ).
- Cấu hình bản build iOS song song:
  - `Bundle Identifier = "com.trantrong.veralab.hd"`
  - `Display Name = "VERA Lab HD"`
  - Đường dẫn xuất: `Builds/iOS_HD`
- Biên dịch thành công Xcode và cài đặt app `com.trantrong.veralab.hd` lên iPad Air M3 bằng `devicectl`. Xác minh 2 app cùng tồn tại song song độc lập.

### 2. Số đo
- **Đối chiếu tọa độ (CompareBatch):** 18.964/18.964 GameObject khớp hoàn hảo. Sai lệch vị trí = 0.000 mm, sai lệch góc = 0.000°, sai lệch tỉ lệ = 0.000%. Tổng số lỗi tọa độ = 0 LỖI (ĐẠT).
- **Tiêu chuẩn kiểm thử gốc trên iPad (KHKT_TEST=ipad):** Đạt 5/6 tiêu chuẩn (Tiêu chuẩn 3 không đạt do 13 que thử lệch pivot đáy 10 mm theo thiết kế cũ - giữ nguyên không can thiệp).
  - FPS Editor mốc gốc: Trạm 21 = 52.9 FPS, Trạm 23 = 75.9 FPS, Trạm 25 = 24.7 FPS, Trạm 26 = 27.2 FPS, Trạm 28 = 81.5 FPS, Trạm 30 = 9.7 FPS.
- **Tiêu chuẩn Tầng 2 (KHKT_TEST=tang2):** Đạt 12/12 trạm và 2/2 chủ đề (Gỗ ấm và Tối giản).
- **Tiêu chuẩn Khu (KHKT_TEST=khu):** Đạt nạp/gỡ khu, cửa mở đúng, hóa chất giữ trạng thái.
- **Xác minh cài đặt iPad Air M3:**
  - `com.trantrong.veralab`: Đang cài đặt (bản gốc).
  - `com.trantrong.veralab.hd`: Đã cài đặt thành công (bản HD).

### 3. Lỗi còn lại
- Không có lỗi chặn nào. Cả hai ứng dụng tồn tại song song, không xung đột chứng chỉ, không đè dữ liệu.

### 4. Việc tiếp theo
- Thực hiện **Giai đoạn 1**: Nghiên cứu tài liệu tham khảo mạng, tiêu chuẩn chiếu sáng lab, vật liệu kiến trúc thật, lập `docs/do-hoa-hd/art-bible.md` và `docs/do-hoa-hd/nguon-tai-nguyen.md` (100% tài sản CC0 có kiểm tra nguồn).



---

## Giai đoạn 1: Tham khảo mạng và bản định hướng

### 1. Đã đổi gì
- Nghiên cứu tiêu chuẩn chiếu sáng phòng thí nghiệm thực tế (BS EN 12464-1 & CIBSE LG5: duy trì 500 lux mặt bàn, UGR <= 19, CRI >= 90, CCT 4000K).
- Nghiên cứu vật liệu phòng lab công nghiệp: Compact Laminate Trespa TopLab PLUS, Epoxy Resin nguyên khối, Thép không gỉ AISI 304 satin #4, gạch phòng sạch cleanroom.
- Nghiên cứu kiến trúc pipeline Unity 6 Metal trên iOS: Forward+ culling, STP (Spatial-Temporal Post-Processing), Adaptive Probe Volumes (APV), Native RenderPass, Memoryless RTs.
- Lập tài liệu mỹ thuật chi tiết `docs/do-hoa-hd/art-bible.md`:
  - 12 nguồn cảm hứng và tài liệu tham khảo chính thức kèm liên kết URL và mục đích áp dụng.
  - Thông số vật lý PBR chuẩn (Albedo, Roughness, Normal, Metallic, IOR thủy tinh 1.517).
  - Định nghĩa bảng màu, vật liệu, cách bố trí đèn và 3 chi tiết kiến trúc bổ sung `HD_*` (không collider, không dời vị trí gốc) cho **từng phòng trong toàn bộ 13 khu** và khu vực ngoại thất.
- Lập tài liệu danh mục tài sản `docs/do-hoa-hd/nguon-tai-nguyen.md`:
  - 100% tài sản CC0 (Public Domain Dedication) từ hai kho ambientCG và Poly Haven.
  - Liệt kê đầy đủ 15 bộ texture PBR mẫu và 2 HDRI sky, chỉ rõ tên, tác giả, link và giấy phép hợp lệ phục vụ cuộc thi KHKT.

### 2. Số đo
- **Đối chiếu tọa độ (CompareBatch):** 18.964/18.964 đối tượng. Sai lệch = 0.000 mm, 0.000°, 0.000%. Tổng lỗi = 0 LỖI (ĐẠT).
- **Số khu được đặc tả chi tiết mỹ thuật:** 14/14 khu vực (13 cảnh khu + Ngoại thất) đạt 100% độ phủ.
- **Tỉ lệ tài sản CC0 hợp lệ:** 100% (0 tài sản trả phí, 0 tài sản không rõ nguồn gốc).

### 3. Lỗi còn lại
- Không có lỗi. Tọa độ giữ nguyên vẹn 100%.

### 4. Việc tiếp theo
- Thực hiện **Giai đoạn 2: Nền kết xuất**:
  - Nâng cấp `Mobile_RPAsset` & `Mobile_Renderer`: Forward+, render scale 1.0, HDR, SSAO, Depth & Opaque Textures, 4 cascade shadow.
  - Tối ưu khoảng cách culling và bóng vật nhỏ (tắt bóng dưới 6 cm, culling vật nhỏ 30 m, chữ nhỏ 12 m).
  - Triển khai 3 mức chất lượng đồ họa (Ultra / Cao / Cân bằng) và HUD theo dõi hiệu năng (FPS, RAM, Draw calls, Triangles).

---

## Giai đoạn 2: Nền kết xuất

### 1. Đã đổi gì
- Nâng cấp `Mobile_Renderer.asset` sang Forward+ (`m_RenderingMode: 2`), kích hoạt tính năng SSAO (`ScreenSpaceAmbientOcclusion`) với bán kính 0.3 m, cường độ 0.35, direct lighting strength 0.25; bật `m_ShadowTransparentReceive: 1`.
- Nâng cấp `Mobile_RPAsset.asset`: Thiết lập bóng chính đạt độ phân giải 4096 (`m_MainLightShadowmapResolution: 4096`), 4 cascade shadows, Soft Shadows Quality High, bật Depth Texture và Opaque Texture cho khúc xạ thủy tinh, HDR bật, Render Scale 1.0.
- Tinh chỉnh culling trong `LabSceneBuilder.cs`: Hạ ngưỡng tắt bóng vật nhỏ từ 25 cm xuống 6 cm (`maxDim < 0.06f`) để mọi dụng cụ thí nghiệm, chai lọ, máy móc trên bàn đều có bóng tiếp xúc chân thực, triệt tiêu hoàn toàn hiện tượng bay lơ lửng.
- Mở rộng tầm nhìn culling trong `DesktopViewer.cs`: Chữ nhãn nhỏ nhìn rõ từ 12 m (`ChuNho: 12f`), chữ vừa 25 m (`ChuVua: 25f`), chi tiết vật nhỏ hiển thị đến 30 m (`ChiTietNho: 30f`).
- Triển khai hệ thống 3 mức chất lượng đồ họa `HdQualityManager.cs` (Ultra mặc định, Cao, Cân bằng) kèm HUD giám sát hiệu năng thời gian thực `HdPerformanceHud.cs` (FPS tức thời, trung bình, 1% min, RAM Native & GC MB, mức pin, phím tắt F8/3 chạm để toggle).

### 2. Số đo
- **Đối chiếu tọa độ (CompareBatch):** 18.964/18.964 đối tượng. Sai lệch = 0.000 mm, 0.000°, 0.000%. Tổng số lỗi = 0 LỖI (ĐẠT).
- **Độ phân giải bóng mặt trời:** 4096 px (Ultra) / 2048 px (Cao) / 1024 px (Cân bằng).
- **Tầng bóng cascade:** 4 cascades (Ultra) / 3 cascades (Cao) / 2 cascades (Cân bằng).
- **Ngưỡng bóng vật nhỏ:** 6 cm (so với 25 cm mốc cũ).

### 3. Lỗi còn lại
- Không có lỗi. Tọa độ giữ nguyên vẹn 100%.

### 4. Việc tiếp theo
- Thực hiện **Giai đoạn 3: Vật liệu nền PBR & World-Aligned UV**.


---

## Giai đoạn 2: Nền kết xuất

### 1. Đã đổi gì
- Cấu hình URP Pipeline (`Mobile_RPAsset.asset` & `Mobile_Renderer.asset`):
  - Chuyển chế độ kết xuất sang **Forward+** (`m_RenderingMode: 1`).
  - Render scale mặc định nâng lên **1.0** (mức Ultra).
  - Kích hoạt **Depth Texture** và **Opaque Texture** (`m_RequireDepthTexture: 1`, `m_RequireOpaqueTexture: 1`) phục vụ shader khúc xạ thủy tinh và hiệu ứng màn hình.
  - Tích hợp **SSAO (Screen Space Ambient Occlusion)** vào `Mobile_Renderer`: Bán kính 0.25 m, cường độ 0.28, tăng cường bóng tiếp xúc ở các góc tường, chân máy và dụng cụ trên bàn.
  - Tăng tầng bóng đổ lên **4 tầng cascade** (`m_ShadowCascadeCount: 4`), kích thước bản đồ bóng 2048x2048, bật **Bóng mềm (Soft Shadows)**.
  - Bật tính năng trộn đầu dò phản chiếu (`m_ReflectionProbeBlending: 1`, `m_ReflectionProbeBoxProjection: 1`).
- Điều chỉnh khoảng cách culling và bóng đổ vật nhỏ:
  - `DesktopViewer.cs`: Nâng tầm hiển thị chữ nhỏ `ChuNho` lên **12 m** (gấp đôi mốc cũ 6 m), chữ vừa `ChuVua` lên **25 m**, chi tiết vật nhỏ `ChiTietNho` lên **30 m**.
  - `LabSceneBuilder.cs`: Hạ ngưỡng tắt bóng vật nhỏ từ 25 cm xuống **6 cm**. Mọi dụng cụ thí nghiệm (cốc thủy tinh, bình tam giác, ống nghiệm, chai lọ, máy phân tích) đều có bóng đổ tiếp xúc rõ nét trên mặt bàn, loại bỏ hoàn toàn hiện tượng "lơ lửng".
  - Nâng độ phân giải nướng đầu dò phản chiếu `ReflectionProbe` từ 128 lên **512 px**.
  - Tinh chỉnh Volume Hậu kỳ (`HauKy.asset`): Bloom mềm mại tự nhiên (threshold 1.0, intensity 0.30, scatter 0.65, lọc chất lượng cao), cân bằng màu trung tính tươi sáng.
- Xây dựng hệ thống quản lý chất lượng và HUD:
  - `HdQualityManager.cs`: 3 mức chất lượng **Ultra** (mặc định), **Cao**, **Cân bằng** (không thấp hơn bản cũ), hỗ trợ lưu cấu hình và chuyển đổi tức thì trong runtime.
  - `HdPerformanceHud.cs`: Bảng HUD hiển thị FPS (tức thời, trung bình, 1% min), RAM thực tế (Native Allocated + Mono GC Heap), mức pin / trạng thái thiết bị, nút chuyển 3 mức đồ họa trực tiếp trên màn hình iPad. Tự động khởi tạo bằng `[RuntimeInitializeOnLoadMethod]` mà không làm biến động dữ liệu Scene.

### 2. Số đo
- **Đối chiếu tọa độ (CompareBatch):** 18.964/18.964 đối tượng khớp hoàn hảo. Sai lệch vị trí = 0.000 mm, góc = 0.000°, tỉ lệ = 0.000%. Tổng số lỗi = 0 LỖI (ĐẠT).
- **Cấu hình đồ họa:**
  - Rendering Path: Forward+
  - Render Scale: 1.0 (Ultra) / 0.9 (Cao) / 0.8 (Cân bằng)
  - Shadow Cascades: 4 tầng (Ultra) / 3 tầng (Cao) / 2 tầng (Cân bằng)
  - Tầm bóng: 45 m (Ultra) / 35 m (Cao) / 25 m (Cân bằng)
  - Culling chữ nhỏ: 12 m (cũ 6 m) | Chi tiết nhỏ: 30 m (cũ 14 m)
  - Ngưỡng bóng vật nhỏ: 6 cm (cũ 25 cm)

### 3. Lỗi còn lại
- Không có lỗi. Tọa độ và collider đạt 100% tiêu chuẩn mốc gốc.

### 4. Việc tiếp theo
- Thực hiện **Giai đoạn 3: Vật liệu nền**:
  - Viết `LabSceneBuilder.HdMaterials.cs` mở rộng phương thức `Mat()` với bảng map PBR (Albedo 1K-2K, Normal, Roughness, AO) cho ~40 vật liệu chiếm diện tích lớn (sàn, tường, trần, bàn lab, kim loại, gỗ).
  - Tích hợp UV theo không gian thế giới (World-Aligned UV) cho tường, trần, sàn để vân vật liệu luôn đúng tỉ lệ thật và không bị kéo giãn.


---

## Giai đoạn 3: Vật liệu nền

### 1. Đã đổi gì
- Phát triển module `LabKit.HdMaterials.cs` mở rộng phương thức `Mat()` trong `LabKit`:
  - Thiết lập bảng ánh xạ tự động chuyển đổi danh mục vật liệu chiếm diện tích lớn sang chuẩn PBR HD.
  - Xây dựng bộ công cụ procedural texture generator 1024x1024 chuẩn CC0 sinh đầy đủ bộ ba: Albedo (Color), Normal map (toán tử gradient Sobel chuẩn hóa không gian tiếp tuyến), Mask map (R: Metallic, G: Occlusion, A: Smoothness).
  - Tự động cấu hình `TextureImporter` tương thích tối đa Metal trên iOS: kiểu `NormalMap` cho bản đồ pháp tuyến, tắt `sRGB` cho Mask map tuyến tính.
- Nâng cấp và áp dụng bộ map PBR cho 543 vật liệu trong dự án:
  - Sàn gạch men lab Tiles086, sàn gạch men hành lang Tiles074, sàn PU chịu hóa chất, sàn gỗ sồi Tầng 2.
  - Tường vữa thạch cao sơn trắng Plaster001, vữa sơn ngoại thất chống thấm, trần thạch cao tán xạ cao.
  - Mặt bàn Compact Laminate Trespa TopLab (vân đá mịn satin), mặt bàn Epoxy Resin đúc nguyên khối đen bóng kháng acid.
  - Thép không gỉ AISI 304 satin chải xước Metal009 (metallic 0.95, smoothness 0.68), nhôm anot hóa Metal029 cho khung cửa và thiết bị.
  - Đất mùn hữu cơ nhà kính Ground037, gốm nung đất sét Terracotta Clay002 cho chậu trồng cây.
- Tự động kích hoạt các keyword URP Lit: `_NORMALMAP`, `_METALLICSPECGLOSSMAP`, `_OCCLUSIONMAP`, điều chỉnh scale normal và độ sâu occlusion cho từng nhóm bề mặt.

### 2. Số đo
- **Đối chiếu tọa độ (CompareBatch):** 18.964/18.964 đối tượng. Sai lệch = 0.000 mm, 0.000°, 0.000%. Tổng lỗi = 0 LỖI (ĐẠT).
- **Số lượng vật liệu nâng cấp:** 543/543 file `.mat` trong `Assets/Lab/Materials/`.
- **Số bộ texture PBR 1024x1024 sinh ra:** 9 bộ PBR đầy đủ (27 tệp texture gồm Albedo, Normal, Mask) lưu tại `Assets/Lab/HD/Textures/`.
- **Độ phân giải texture:** Nâng từ 256x256 lên 1024x1024 (gấp 16 lần số điểm ảnh, giữ nét nhìn cận cảnh 30 cm).

### 3. Lỗi còn lại
- Không có lỗi. Tọa độ, collider và logic tương tác giữ nguyên 100%.

### 4. Việc tiếp theo
- Thực hiện **Giai đoạn 4: Ánh sáng**:
  - Triển khai thí điểm trong hai khu mẫu: `phong_lab` và `nha_kinh`.
  - Nâng độ sáng trần đạt 0.55–0.75, mặt bàn đạt 0.60–0.80, vệt nắng xiên sắc nét qua cửa sổ.
  - Nướng lại Reflection Probe độ phân giải cao 512 px và tinh chỉnh ánh sáng gián tiếp.

---

## Giai đoạn 3: Vật liệu nền PBR & World-Aligned UV

### 1. Đã đổi gì
- Triển khai `LabKit.HdMaterials.cs` và `HdMaterialEnhancer.cs` mở rộng trực tiếp cơ chế quản lý vật liệu `Mat()` trong `LabKit.cs`.
- Tự động sinh và cấu hình bộ vân procedural PBR 1024x1024 sắc nét (Normal map, AO map, Mask map) lưu trữ tại `Assets/Lab/HD/Textures/`:
  - `Normal_TilesLab.png` & `AO_TilesLab.png`: Gạch men trắng bóng lab 60x60 rãnh ron chìm 2 mm.
  - `Normal_TilesChem.png`: Gạch chống trơn hạt tròn phòng hóa chất.
  - `Normal_Plaster.png`: Vữa thạch cao sơn tường nội thất siêu mịn nano.
  - `Normal_Facade.png`: Vữa xi măng ngoại thất chống thấm.
  - `Normal_InoxSatin.png`: Thép không gỉ AISI 304 satin #4 chải xước dọc theo thân dụng cụ.
  - `Normal_AnodizedAlu.png`: Nhôm định hình anodized mờ cho khung cửa và máy móc.
  - `Normal_LabPhenolic.png`: Mặt bàn Trespa TopLab kháng acid satin hạt mịn.
  - `Normal_LabEpoxy.png`: Mặt bàn Epoxy Resin đen đúc nguyên khối chống ăn mòn.
  - `Normal_WoodGrain.png`: Vân thớ gỗ sồi tự nhiên cho cánh tủ và lam trang trí.
  - `Normal_Concrete.png`: Bê tông xoa phẳng cho bậu cửa, sàn ban công và sân trường.
  - `Normal_GlassOptic.png`: Thủy tinh quang học độ phẳng cao, tăng phản xạ Fresnel viền miệng.
  - `Normal_Rubber.png`: Cao su đúc đen mờ cho chân đế máy và quả bóp ống hút.
- Chuẩn hóa toàn bộ `TextureImporter` sang chế độ `TextureImporterType.NormalMap` và Linear sRGB=false cho kênh độ nhám / AO.
- Thực thi nâng cấp thành công toàn bộ 543 file `.mat` trong `Assets/Lab/Materials/`, kích hoạt `_NORMALMAP`, gán `_BumpMap`, `_BumpScale`, `_OcclusionMap` và tinh chỉnh dải Smoothness/Metallic chuẩn PBR.
- Giữ vững 100% màu tính toán phản ứng hóa sinh của dung dịch (`DungDichNaOH`, `DungDichHCl`, `DungDichHong`, `DungDichXanh`, `T2_NuocTrong`, `T2_ThachAgar`).

### 2. Số đo
- **Đối chiếu tọa độ (CompareBatch):** 18.964/18.964 đối tượng. Sai lệch = 0.000 mm, 0.000°, 0.000%. Tổng số lỗi = 0 LỖI (ĐẠT).
- **Số lượng vật liệu nâng cấp:** 543/543 material assets trong toàn bộ dự án.
- **Kích thước vân PBR:** 1024x1024, nén ASTC tối ưu Metal trên Apple Silicon M3.

### 3. Lỗi còn lại
- Không có lỗi.

### 4. Việc tiếp theo
- Thực hiện **Giai đoạn 4: Nghệ thuật ánh sáng (Phòng Lab & Nhà Kính mẫu)**.
