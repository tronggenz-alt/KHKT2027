# KẾ HOẠCH NÂNG CẤP ĐỒ HỌA SIÊU THỰC VERA LAB (GIAI ĐOẠN 0 - 8)

> **Mục tiêu:** Nâng cấp chất lượng hình ảnh VERA Lab lên mức PBR siêu thực (phòng lab cao cấp, hiện đại, ngập tràn ánh sáng ban ngày, vật liệu chi tiết từ cự ly 30 cm) trên bản mẫu riêng **VERA Lab HD** (`com.trantrong.veralab.hd`), bảo đảm 100% logic thí nghiệm, khóa tuyệt đối vị trí/tỉ lệ/collider (0 lỗi so với mốc gốc), không ảnh hưởng đến app cũ và dừng lại ở Giai đoạn 8 để nghiệm thu trên iPad Air M3.

---

## 1. XÁC MINH HIỆN TRẠNG MÃ NGUỒN & HỆ THỐNG (MỤC 2 & 3)

| Hạng mục | Khảo sát thực tế mã nguồn | Kết luận |
|---|---|---|
| **Cơ chế dựng cảnh** | Dựng hoàn toàn bằng C# qua `LabSceneBuilder.Build()` (gồm cảnh lõi `PhongThiNghiem.unity` + 13 scene con trong `Assets/Scenes/Khu/` quản lý bằng `ZoneStreamer`). | **Khớp 100%**. Mọi thay đổi đồ họa phải nằm trong mã nguồn C# và tài nguyên import, không được chỉnh tay trên Scene lưu đệm. |
| **Pipeline URP hiện tại** | Dùng `Mobile_RPAsset.asset` + `Mobile_Renderer.asset`: Forward, Render Scale **0.8**, MSAA tắt, bóng 1024 (1 cascade), tắt Depth/Opaque Textures, không SSAO, không Lightmap. | **Khớp 100%**. Đây là nguyên nhân chính khiến không gian phẳng, thiếu bóng tiếp xúc, dụng cụ bay lơ lửng và thủy tinh không khúc xạ. |
| **Vật liệu hiện tại** | `Mat()` trong `LabKit.cs` chỉ gán BaseColor, Metallic, Smoothness và `_BaseMap`. Không có Normal map, Roughness map hay AO map. Vân tự sinh kích thước nhỏ (32–256 px). | **Khớp 100%**. Cần mở rộng bảng ánh xạ PBR texture trong partial class `LabSceneBuilder.Hd*.cs`. |
| **Hạt giống ngẫu nhiên** | `rnd = new System.Random(7)` ở đầu `LabSceneBuilder.Build()` chi phối cây sân thượng và màu dung dịch mẫu. | **Khớp 100%**. Tuyệt đối không chạm vào biến `rnd` này. Mọi sinh ngẫu nhiên cho chi tiết HD mới sẽ dùng bộ sinh độc lập. |
| **Đường dẫn Unity & Mac** | Unity 6000.6.7f1 tại `/Users/trantrong/Documents/6000.6.7f1/Unity.app`. Máy Mac M2, RAM 8 GB. Không mở được dự án dưới thư mục ẩn (`.gemini`, `.claude`). | **Khớp 100%**. Worktree được tạo tại thư mục người dùng không ẩn: `~/VERALabHD`. |
| **Thiết bị kiểm thử** | iPad Air M3, UDID `00008122-001669801186801C`, Team ID `29263U3K84`. | **Khớp 100%**. Cài đặt qua `xcrun devicectl`. |

---

## 2. PHÁT HIỆN THỰC TẾ & CẢNH BÁO ĐẶC BIỆT

1. **Dung lượng đĩa khả dụng dưới ngưỡng 8 GB (CẢNH BÁO QUAN TRỌNG):**
   - Hiện trạng lệnh `df -h /System/Volumes/Data`: **5.3 GiB khả dụng**.
   - Mục 3.3 quy định: *"Còn dưới 8 GB thì dừng và báo tôi, không tự xóa file ngoài những gì bạn tạo ra"*.
   - Khảo sát dung lượng: Thư mục derived data cũ của bản dựng trước tại `~/AppBuild/VERALab/dd-device` đang chiếm **3.6 GiB**. Nếu được dọn dẹp (theo gợi ý tại mục 3.3 *"xóa thư mục dd-* sau khi cài"*), đĩa sẽ có **~8.9 GiB trống**, bảo đảm an toàn để xuất bản Xcode cho VERA Lab HD mà không gặp nguy cơ tràn đĩa.
2. **Quy tắc phân cấp đối tượng khi so tọa độ:**
   - Trong cảnh có nhiều đối tượng con trùng tên cùng cha (ví dụ các vòi nước, kính bảo hộ).
   - Công cụ `HdCoordinateTool.cs` đã được thiết kế dùng đường dẫn phân cấp tuyệt đối kèm chỉ số anh em `[siblingIndex]` ở mọi cấp cha-con, loại trừ hoàn toàn nguy cơ trùng lặp từ điển.
   - Kết quả chạy đối chiếu trên toàn bộ **18.964 đối tượng** đạt **0 LỖI TUYỆT ĐỐI**.
3. **13 dụng cụ que tầng 2 lệch pivot đáy 10 mm:**
   - Trong bài kiểm thử `KHKT_TEST=ipad`, 13 dụng cụ que (que cấy, que gạt, kim mũi giáo...) đặt xiên có điểm pivot đáy lệch 10 mm, khiến tiêu chuẩn 3 hiện báo không đạt và tổng điểm kiểm thử iPad là **5/6 ĐẠT**. Đây là mốc gốc chuẩn của nhánh `claude/xcode-app-export-310c80`, không được tự ý sửa pivot của 13 que này để tôn trọng Luật 1.1.

---

## 3. KẾ HOẠCH THỰC HIỆN TỪ GIAI ĐOẠN 0 ĐẾN GIAI ĐOẠN 8

### Giai đoạn 0 — Chuẩn bị, Khóa Tọa Độ & Bản Thử Cài Đặt Ban Đầu
- [x] Tạo worktree sạch `~/VERALabHD` từ commit `e07c84aa` (`claude/xcode-app-export-310c80`).
- [x] Sao chép APFS clone thư mục `Library` từ `~/AppBuild/VERALab/Project/Library`.
- [x] Dựng cảnh gốc sạch: 20.222 đối tượng, 12 reflection probes, 22 cửa sổ ảo, NavGrid data hash `09aa0fd2...`.
- [x] Viết công cụ `HdCoordinateTool.cs` chụp và so khớp mốc gốc `toa_do_goc.json` (sai số vị trí $\le 1$ mm, góc $\le 0.01^\circ$, tỉ lệ $\le 0.1\%$).
- [x] Chạy nghiệm thu so tọa độ mốc gốc: **0 LỖI (ĐẠT)** trên 18.964 đối tượng kiểm tra.
- [ ] Chụp bộ ảnh mốc gốc ban đầu qua `KHKT_TEST=landing` (15 ảnh) và `KHKT_TEST=goc_nhin` (12 góc).
- [ ] Ghi nhận điểm kiểm thử gốc: `KHKT_TEST=ipad` (5/6), `tang2`, `khu`.
- [ ] Cập nhật `IosBuild.cs`: Đổi Display Name thành `VERA Lab HD`, Bundle ID thành `com.trantrong.veralab.hd`, thư mục xuất `Builds/iOS_HD`.
- [ ] Báo cáo dung lượng đĩa và xin phép dọn `~/AppBuild/VERALab/dd-device` để build thử VERA Lab HD lên iPad.

### Giai đoạn 1 — Nghiên Cứu Mỹ Thuật & Lập Art Bible
- [ ] Thu thập tài liệu tham khảo: Phòng lab hiện đại (Harvard, MIT, tiêu chuẩn phòng thí nghiệm chuẩn quốc tế), nhà kính công nghệ cao, kiến trúc ngoại thất trường học.
- [ ] Viết `docs/do-hoa-hd/art-bible.md`: Bảng màu, vật liệu chi tiết (gỗ tự nhiên ấm, compact laminate chống axit, inox 304 xước satin, kính quang học), tiêu chuẩn ánh sáng 500 lux, 3 chi tiết kiến trúc bổ sung cho từng phòng trong 13 khu.
- [ ] Lập `docs/do-hoa-hd/nguon-tai-nguyen.md`: 100% tài nguyên CC0 từ Poly Haven, ambientCG (kèm URL, tác giả, giấy phép).

### Giai đoạn 2 — Cải Tổ Pipeline Kết Xuất & Quản Lý Hiệu Năng
- [ ] Nâng cấp URP Asset: Forward+, render scale 1.0, HDR, 4 cascade shadows với soft shadows.
- [ ] Bật Depth Texture & Opaque Texture phục vụ khúc xạ thủy tinh.
- [ ] Kích hoạt Screen Space Ambient Occlusion (SSAO) cho chiều sâu góc cạnh và tiếp xúc sàn/bàn.
- [ ] Tinh chỉnh cự ly culling và đổ bóng: Cho phép vật dụng nhỏ từ 6 cm đổ bóng, không làm bay lơ lửng trên bàn; tầm nhìn vật nhỏ đạt 30 m.
- [ ] Triển khai 3 mức đồ họa trong cấu hình: **Ultra** (mặc định), **Cao**, **Cân bằng**.
- [ ] Thêm bảng HUD đo lường thời gian thực: FPS (trung bình & 1% low), RAM, Draw Calls.

### Giai đoạn 3 — Hệ Thống Vật Liệu PBR & World-Aligned UV
- [ ] Xây dựng mô đun `LabSceneBuilder.HdMaterials.cs` mở rộng hàm `Mat()`.
- [ ] Áp dụng bộ vân PBR (Albedo 1K-2K, Normal, Roughness, AO) cho ~40 vật liệu diện tích lớn: gạch men sàn lab, tường vữa mịn, trần acoustic, mặt bàn compact laminate/epoxy, khung thép, kính phòng.
- [ ] Áp dụng World-Aligned / Triplanar cho các khối kéo dãn để triệt tiêu hiện tượng giãn vân.

### Giai đoạn 4 — Nghệ Thuật Ánh Sáng Mẫu (Phòng Lab & Nhà Kính)
- [ ] Nâng độ sáng tổng thể đạt tiêu chuẩn: Tường/trần 0.55–0.75, Mặt bàn 0.60–0.80, tỷ lệ cháy sáng < 3%, tối nghẽn < 2%.
- [ ] Nâng cấp trần sáng với máng đèn dải tán xạ mềm, bóng đổ chân thực.
- [ ] Thiết lập vệt nắng đổ qua khung cửa kính nhà kính và phòng lab.
- [ ] Nướng Lightmap Baked Indirect và nâng độ phân giải Reflection Probes lên 512 px.

### Giai đoạn 5 — Kiến Trúc & Chi Tiết Không Gian Nhóm `HD_*`
- [ ] Bổ sung các chi tiết kiến trúc chân thực (phào chỉ, len chân tường, nẹp kính, gioăng cao su cửa, hộp kỹ thuật) đặt trong các GameObject cha có tiền tố `HD_*` và **không có Collider**.
- [ ] Nâng cấp ngoại thất tòa nhà: vữa sơn chất lượng cao, gờ cửa, máng thoát nước, thảm cỏ và sân trường.
- [ ] Cập nhật bầu trời HDRI sạch sẽ, chụp lại 22 cửa sổ ảo với độ nét cao.

### Giai đoạn 6 — Hệ Thống Cây Siêu Thực
- [ ] Nâng cấp mô hình cây trồng nhà kính và cây trang trí lab: lá dẹt có độ dày, vân gân lá rõ nét, normal map, hiệu ứng ánh sáng xuyên lá (subsurface scattering giả lập).
- [ ] Đất chậu có độ xốp và texture mùn, chậu đất nung có vết ố khoáng nhẹ.
- [ ] Bổ sung pass `DepthOnly` và `DepthNormals` cho `LaCay.shader` để nhận SSAO chính xác.
- [ ] Bảo đảm 100% cơ chế tương tác thí nghiệm (cây rong đuôi chó quang hợp thoát bọt, lá soi khí khổng).

### Giai đoạn 7 — Nâng Cấp Dụng Cụ, Thiết Bị & Mặt Khum Thủy Tinh
- [ ] Nâng cấp vật liệu thủy tinh: Khúc xạ quang học, phản xạ Fresnel theo góc nhìn, miệng ống nghiệm/cốc đong có viền cong sáng, vạch chia chia độ sắc nét không vỡ hạt.
- [ ] Mặt khum dung dịch (meniscus) và màu sắc phản ứng tuân thủ tuyệt đối màu tính toán hóa học.
- [ ] Kim loại inox/nhôm: xước chải xước dọc thân dụng cụ, phản chiếu mềm mại.
- [ ] Bề mặt máy móc: khe tản nhiệt, công tắc, nhãn cảnh báo, màn hình LED/OLED phát sáng nhẹ.

### Giai đoạn 8 — ⛔ BẢN MẪU TRÊN IPAD & DỪNG LẠI CHỜ DUYỆT
- [ ] Chạy kiểm tra tọa độ mốc gốc: **Bắt buộc 0 lỗi**.
- [ ] Chạy toàn bộ kiểm thử tự động (`ipad`, `tang2`, `khu`): Điểm số không được thấp hơn ban đầu.
- [ ] Gỡ script kiểm thử, xuất bản `com.trantrong.veralab.hd` sang Xcode và cài đặt lên iPad Air M3.
- [ ] Xác minh thực tế trên iPad: App khởi động thành công, chạy song song với app cũ, không có shader hồng.
- [ ] Đo đạc số liệu hiệu năng thực: FPS (trung bình & 1% min), đỉnh RAM, nhiệt độ qua 20 phút chạy liên tục.
- [ ] Viết báo cáo `docs/do-hoa-hd/bao-cao-ban-mau.md` kèm ảnh đối chiếu trước/sau và checklist tiếng Việt cho chủ dự án.
- [ ] **DỪNG LẠI TUYỆT ĐỐI VÀ CHỜ DUYỆT.**

