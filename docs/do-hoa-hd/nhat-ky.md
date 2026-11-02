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
