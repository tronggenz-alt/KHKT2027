# BẢN ĐỊNH HƯỚNG MỸ THUẬT (ART BIBLE) - VERA LAB HD

Dự án: **VERA Lab HD** (`com.trantrong.veralab.hd`)  
Nền tảng mục tiêu: **iPad Air M3 (Metal, Forward+, URP Unity 6000.6.7f1)**  
Nguyên tắc cốt lõi: **PBR Siêu thực - Chuẩn mực khoa học, Tinh tế, Sáng đẹp** (Không cel-shading, không viền hoạt hình, giữ nguyên 100% tọa độ và logic thí nghiệm).

---

## 1. TÀI LIỆU THAM KHẢO VÀ NGUỒN CẢM HỨNG

| STT | Tài liệu / Nguồn cảm hứng | Đường dẫn / Nguồn | Áp dụng gì, ở đâu |
|:---:|---|---|---|
| 1 | **Tiêu chuẩn chiếu sáng Lab EN 12464-1 & CIBSE LG5** | [CIBSE LG5 Educational Lighting Guide](https://www.cibse.org) | Độ rọi mặt bàn 500 lux, $U_0 \ge 0.6$, UGR $\le 19$, CCT 4000K, CRI $\ge 90$ cho toàn bộ các phòng thí nghiệm và chuẩn độ hóa sinh. |
| 2 | **Vật liệu bàn thí nghiệm hiện đại (Trespa TopLab PLUS)** | [Trespa TopLab Scientific Solutions](https://www.trespa.com) | Mặt bàn Compact Laminate màu xám đá chống hóa chất, gờ vát R2 bo viền mịn cho trạm hóa học và sinh học. |
| 3 | **Mặt bàn Epoxy Resin nguyên khối kháng axit** | [Durcon Epoxy Worktops Specification](https://www.durcon.com) | Mặt bàn đen nhám/satin đúc nguyên khối, chống ăn mòn hóa chất cực mạnh trong tủ hút khí độc và trạm tổng hợp. |
| 4 | **Thép không gỉ AISI 304 / 316 Satin Finish #4** | [Specialty Steel Industry of NA (SSINA)](https://www.ssina.com) | Khung bàn, bồn rửa chịu acid, tủ an toàn sinh học BSC, vòi rửa mắt khẩn cấp, giá đỡ thiết bị. |
| 5 | **Phòng thí nghiệm Stanford Bio-X Clark Center** | [Foster + Partners Stanford Bio-X](https://www.fosterandpartners.com) | Thiết kế vách kính trong suốt phân chia không gian, tràn ngập ánh sáng tự nhiên, trần kỹ thuật hở tinh gọn và hiện đại. |
| 6 | **Phòng sạch sinh học BSL-2 theo tiêu chuẩn CDC/NIH** | [CDC BMBL 6th Edition](https://www.cdc.gov) | Bề mặt sàn Vinyl bo chân tường coved base, tường kháng khuẩn không khe nứt, ron silicone vô trùng tại Phòng Vi sinh và Sinh học Phân tử. |
| 7 | **Nhà kính nghiên cứu nhiệt đới Đại học Wageningen** | [Wageningen University Greenhouses](https://www.wur.nl) | Khung nhôm định hình thanh mảnh, kính tán xạ ánh sáng (diffuse glass), hệ thống tưới nhỏ giọt, chậu ươm gốm terracotta sạch sẽ. |
| 8 | **Kiến trúc trường học công nghệ cao ETH Zurich** | [ETH Zurich Campus Architecture](https://ethz.ch) | Ngoại thất vữa xi măng sơn chống thấm trắng xám mịn, lam che nắng gỗ sồi thanh thoát, sân cỏ tự nhiên có bờ bao bê tông sạch. |
| 9 | **Tài liệu Unity 6: URP Forward+ & Metal Optimization** | [Unity 6 Universal Render Pipeline Manual](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/rendering-paths.html) | Thiết lập Forward+ culling, Native RenderPass, Memoryless RTs, giảm tải băng thông bộ nhớ trên GPU Apple Silicon M3. |
| 10 | **Tài liệu Unity 6: Spatial-Temporal Post-Processing (STP)** | [Unity 6 STP Graphics Reference](https://unity.com) | Khử răng cưa thời gian siêu mẫu, giữ nét các chi tiết mảnh như ống sinh hàn, kim tiêm, que cấy, cạnh bàn trên màn hình Retina 264 ppi. |
| 11 | **Tài liệu Unity 6: Adaptive Probe Volumes (APV)** | [Unity 6 Adaptive Probe Volumes](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/probevolumes.html) | Ánh sáng gián tiếp động cho nhân vật và dụng cụ di động khi chuyển giữa các vùng sáng/tối trong phòng. |
| 12 | **Kỹ thuật thẩm mỹ không gian Genshin Impact** | [Genshin Impact Lighting Analysis (GDC)](https://www.gdcvault.com) | Kỹ thuật cân bằng phơi sáng: phòng tràn ngập ánh sáng mềm, đèn có quầng glow dịu nhẹ, không có góc chết tối tăm, phân định chiều sâu rõ rệt. |

---

## 2. THÔNG SỐ VẬT LÝ VÀ CHỈ SỐ MÀU SẮC CHUẨN

- **Mặt bàn thí nghiệm (Lab Worktop):**
  - Albedo: Trespa Gray (`#3E4246`), Epoxy Jet Black (`#1E2022`), Hoặc Men sứ trắng ngà (`#E8ECEE`).
  - Roughness: `0.25 - 0.40` (bề mặt satin mịn, phản xạ nhòe tinh tế, không bóng gương chói gắt).
  - Normal map: Vi hạt siêu mịn (micro-texture 0.5 mm), chống cảm giác phẳng nhựa nhân tạo.
- **Tường & Trần:**
  - Albedo: Trắng vô trùng có sắc ấm nhẹ (`#F4F5F6`), trần thạch cao tán xạ cao (`#FFFFFF`).
  - Roughness: `0.85 - 0.95` (tán xạ Lambertian hoàn hảo, nhận gián tiếp GI mượt mà).
  - Độ sáng ảnh đo mục tiêu: Trần đạt **0.55 – 0.75**; Mặt bàn đạt **0.60 – 0.80**.
- **Thủy tinh Borosilicate 3.3:**
  - IOR: `1.517` (khúc xạ quang học chuẩn của kính Pyrex/Schott Duran).
  - Cạnh vát miệng cốc/ống đong: Phản xạ Fresnel tăng vọt khi góc nhìn tiếp tuyến ($R_0 \approx 0.04$, mép viền sáng lấp lánh).
  - Bổ sung đốm phản chiếu mềm và vạch chia độ in men trắng/xanh nét mịn.
- **Inox 304 Satin:**
  - Metallic: `0.92 - 0.98`.
  - Roughness: `0.22 - 0.35` với anisotropic scratch chải xước dọc theo trục thiết bị.

---

## 3. THIẾT KẾ ĐẶC TẢ CHI TIẾT CHO 13 KHU VÀ NGOẠI THẤT

> [!IMPORTANT]
> Toàn bộ các chi tiết kiến trúc thêm vào đều nằm trong GameObject `HD_*`, là các khối trang trí thuần túy (KHÔNG có Collider, KHÔNG thay đổi vị trí, góc xoay, tỉ lệ của bất kỳ vật thể nào từ bản gốc).

### 1. Khu 1: Phòng Thí nghiệm Hóa học Chính (`phong_lab`)
- **Bảng màu:** Trắng thạch cao chủ đạo (`#F4F5F6`), Bàn đá nhân tạo xám đá (`#4A5056`), Tủ hút xanh lam kỹ thuật nhạt (`#3A6D8C`), Điểm nhấn gỗ ấm sồi Nga ở kệ lưu trữ.
- **Vật liệu chính:** Epoxy resin satin, Kính an toàn nhiều lớp, Inox 304 satin, Ống dẫn khí đồng mạ crom.
- **Cách đặt đèn:**
  - Đèn trần: 4 dải máng LED hộp dài 2.4 m chạy song song với dãy bàn thí nghiệm, CCT 4000K, phát sáng khuếch tán mềm.
  - Đèn trần kỹ thuật: Đèn hắt khe trần gián tiếp (cove lighting) làm sáng đều tấm trần thạch cao (đạt độ sáng 0.65).
  - Đèn tủ hút: Dải LED chống nổ bên trong tủ tạo điểm nhấn sáng 700 lux cho bình chưng cất.
- **3 Chi tiết kiến trúc `HD_*` thêm vào:**
  1. `HD_NepTran_PhongLab`: Phào chỉ viền trần thạch cao âm đèn LED và hộp kỹ thuật thông gió.
  2. `HD_LenChanTuong_PhongLab`: Len chân tường inox 304 cao 10 cm bo tròn chân góc vệ sinh kiểu phòng lab.
  3. `HD_KenhDienAmBan_PhongLab`: Cột cấp điện và khí kỹ thuật thả trần inox mờ cắm vào giữa các dãy bàn.

### 2. Khu 2: Nhà Kính Thực Vật (`nha_kinh`)
- **Bảng màu:** Xanh diệp lục tươi sáng (`#4E8B42`), Khung thép sơn trắng tĩnh điện (`#FAFAFA`), Đất ẩm dinh dưỡng nâu đen (`#2E231B`), Gạch gốm nung terracotta (`#C66847`).
- **Vật liệu chính:** Kính nhà kính mờ tán xạ cao (diffuse glass 85%), Nhôm định hình phủ fluorocarbon trắng, Đất mùn hữu cơ, Inox thanh treo đèn quang hợp.
- **Cách đặt đèn:**
  - Nguồn sáng chính: Ánh sáng mặt trời tự nhiên trực tiếp (Directional Light) tạo vệt nắng xiên đổ bóng rõ nét trên sàn sỏi và luống cây.
  - Đèn quang hợp bổ sung: Dải đèn LED sinh học tím hồng/vàng ấm (CCT 3000K + Deep Red 660nm) treo trên giàn khung trần.
- **3 Chi tiết kiến trúc `HD_*` thêm vào:**
  1. `HD_NepKhungKinh_NhaKinh`: Nẹp gioăng cao su đen và gờ nẹp nhôm chữ T gia cố viền kính trần và vách.
  2. `HD_HeThongTuoi_NhaKinh`: Ống dẫn tưới nhỏ giọt PE đen mờ chạy men theo đáy các bồn trồng cây.
  3. `HD_MaoThoatNuoc_NhaKinh`: Rãnh thoát nước mặt bê tông mài có vỉ gang xám chống trơn trượt quanh sàn.

### 3. Khu 3: Kho Hóa Chất Chuyên Dụng (`kho_hoa_chat`)
- **Bảng màu:** Xám bê tông công nghiệp sạch (`#95999E`), Kệ thép phủ sơn tĩnh điện chống hóa chất màu vàng cảnh báo an toàn (`#D4A017`), Đèn led tín hiệu xanh/đỏ.
- **Vật liệu chính:** Sàn sơn sàn Polyurethane tự san phẳng chống tràn đổ hóa chất, Thép cuộn nguội dập lỗ, Tủ an toàn chống cháy nổ sơn sần.
- **Cách đặt đèn:**
  - Đèn máng chống nổ chụp thủy tinh bảo vệ IP65, phân bố dọc theo các lối đi giữa kệ chai hóa chất.
  - Chiếu điểm nhẹ lên bảng chỉ dẫn an toàn MSDS và vòi sen tắm khẩn cấp.
- **3 Chi tiết kiến trúc `HD_*` thêm vào:**
  1. `HD_GoChongTran_KhoHoaChat`: Gờ dốc cao su chống tràn hóa chất (spill containment berm) gắn tại ngưỡng cửa.
  2. `HD_KhungGiaKe_KhoHoaChat`: Thanh giằng kim loại gia cố chịu động đất ở mặt sau kệ hóa chất.
  3. `HD_OngHutKhiSan_KhoHoaChat`: Miệng van thông gió áp suất âm hút khí nặng sát sàn inox mờ.

### 4. Khu 4: Hành Lang Tầng 1 (`hanh_lang`)
- **Bảng màu:** Trắng ngà thanh lịch (`#F8F9FA`), Ốp tường lam gỗ sồi ấm áp (`#C8A882`), Khung cửa nhôm xám ghi đen (`#2C2F33`).
- **Vật liệu chính:** Gạch Porcelain bóng mờ chống trơn 600x600, Kính Low-E trong vắt kết nối tầm nhìn vào phòng thí nghiệm, Gỗ tiêu âm rãnh hẹp.
- **Cách đặt đèn:**
  - Đèn rãnh LED âm trần chạy dọc tim hành lang nối liền liên tục, kết hợp đèn spotlight chiếu rọi các bức tranh chân dung nhà khoa học.
- **3 Chi tiết kiến trúc `HD_*` thêm vào:**
  1. `HD_KhungVachKinh_HanhLang`: Khung nẹp nhôm mảnh định hình vách kính ngăn hành lang nhìn vào phòng lab.
  2. `HD_PhaoChiGo_HanhLang`: Nẹp bo viền gỗ và đường chỉ ron inox phân tách mảng ốp gỗ tiêu âm.
  3. `HD_BangChiDan_HanhLang`: Khung biển chỉ dẫn phòng thí nghiệm mica trong suốt gắn chốt inox nổi cách tường 2 cm.

### 5. Khu 5: Phòng Máy Phân Tích Hiện Đại (`phong_may`)
- **Bảng màu:** Trắng phòng sạch (`#FFFFFF`), Xám máy kỹ thuật số (`#5A626A`), Đèn nền LED xanh cyan dịu mắt (`#00A8CC`) ở chân máy SEM/XRD.
- **Vật liệu chính:** Sàn Vinyl đồng nhất chống tĩnh điện (ESD Vinyl), Nhôm anodized mờ, Màn hình kính tinh thể đen bóng phản xạ dịu.
- **Cách đặt đèn:**
  - Đèn panel LED tấm mỏng âm trần khuếch tán đều UGR < 16, không tạo bóng phản xạ khó chịu trên màn hình máy phân tích.
- **3 Chi tiết kiến trúc `HD_*` thêm vào:**
  1. `HD_HopCapSan_PhongMay`: Nắp máng cáp ngầm kỹ thuật âm sàn bằng nhôm sọc gân chống trượt.
  2. `HD_OngDapKhiXa_PhongMay`: Ống chụp hút khí thải cục bộ linh hoạt bằng nhôm co giãn thả từ trần xuống đầu máy phân tích.
  3. `HD_VienDenLedChanMay_PhongMay`: Khe hắt ánh sáng LED mềm ở gầm bàn và chân đế các cụm máy phân tích lớn.

### 6. Khu 6: Phòng Robot & Chế Tạo Thử Nghiệm (`phong_robot`)
- **Bảng màu:** Xám titan (`#484D52`), Vàng an toàn công nghiệp (`#E6AF2E`), Đen carbon mờ (`#1A1C1E`).
- **Vật liệu chính:** Thép dập lỗ, Bề mặt bàn lắp ráp nhôm rãnh chữ T (T-slot extrusions), Tấm polycarbonate trong chắn mảnh văng.
- **Cách đặt đèn:**
  - Đèn trần High-bay LED công nghiệp hiện đại, kết hợp đèn chiếu điểm tay robot cánh tay cơ khí.
- **3 Chi tiết kiến trúc `HD_*` thêm vào:**
  1. `HD_VachKinhBaoVe_PhongRobot`: Vách ngăn tấm trong suốt viền nhôm ngăn cách khu vực vận hành tay máy.
  2. `HD_GiaTreoDungCu_PhongRobot`: Khung pegboard kim loại treo dụng cụ siết ốc, cờ lê chuẩn xác trên tường.
  3. `HD_SanVachKeKe_PhongRobot`: Đường line vạch sơn epoxy kẻ sàn phân chia ranh giới di chuyển an toàn cho robot.

### 7. Khu 7: Phòng Sinh Học Đại Cương T2 (`phong_sinh`)
- **Bảng màu:** Trắng sứ sáng (`#F0F3F4`), Xanh lá xô thơm mộc mạc dịu mắt (`#87A987`), Mặt bàn phenolic resin trắng ngà.
- **Vật liệu chính:** Phenolic resin kháng bám vi sinh, Gỗ dán laminate ép nhiệt màu be, Kính quang học thị kính kính hiển vi.
- **Cách đặt đèn:**
  - Hệ thống đèn chiếu sáng daylight 4500K chiếu rọi thẳng tâm từng bàn thực hành kính hiển vi và mổ phẫu tích.
- **3 Chi tiết kiến trúc `HD_*` thêm vào:**
  1. `HD_GiaKinhHienVi_PhongSinh`: Khung nẹp kim loại cố định ổ cắm điện tích hợp từng bàn kính hiển vi.
  2. `HD_HopRuaMat_PhongSinh`: Chậu rửa chuyên dụng vòi cổ ngỗng bằng đồng mạ satin và giá treo khay sấy lam kính.
  3. `HD_HopKyThuatTran_PhongSinh`: Miệng gió khuếch tán khí tươi trần hình vuông sơn trắng trang nhã.

### 8. Khu 8: Phòng Vi Sinh Vô Trùng T2 (`phong_vi_sinh`)
- **Bảng màu:** Trắng vô trùng tuyệt đối (`#FAFAFC`), Điểm xuyết đèn UV khử khuẩn tím nhạt (`#6A0DAD`) khi phòng tắt đèn.
- **Vật liệu chính:** Tường tấm Panel Cleanroom PU cách nhiệt không gờ mép, Sàn vinyl hàn nhiệt nguyên khối, Kính cường lực vát cạnh.
- **Cách đặt đèn:**
  - Đèn panel phòng sạch phẳng tuyệt đối không bám bụi, ánh sáng đồng nhất 600 lux.
- **3 Chi tiết kiến trúc `HD_*` thêm vào:**
  1. `HD_CovedWall_PhongViSinh`: Góc lượn bo tròn chân tường và trần (coved corner) đúng chuẩn BSL-2 loại bỏ hoàn toàn góc chết 90 độ.
  2. `HD_DongHoApSuat_PhongViSinh`: Đồng hồ đo chênh lệch áp suất vi sai Magnehelic gắn âm cạnh cửa ra vào.
  3. `HD_CuaMoTuDong_PhongViSinh`: Nút ấn khuỷu tay mở cửa vô trùng bằng inox 316.

### 9. Khu 9: Kho Sinh Phẩm & Lạnh Âm Sâu T2 (`kho_sinh_pham`)
- **Bảng màu:** Xám bạc kim loại lạnh (`#C0C7CD`), Xanh băng tuyết (`#A5D8FF`), Trắng vỏ tủ lạnh âm sâu.
- **Vật liệu chính:** Inox 304 dập gân, Ron cao su silicon giữ nhiệt, Màn hình LED kỹ thuật số hiển thị âm 80°C.
- **Cách đặt đèn:**
  - Đèn LED thanh mỏng ánh sáng trắng lạnh 5000K, bật sáng rõ các ngăn kéo lưu trữ mẫu gen.
- **3 Chi tiết kiến trúc `HD_*` thêm vào:**
  1. `HD_OngGenLanh_KhoSinhPham`: Ống bảo ôn điều hòa lạnh bọc màng bạc sáng chạy trên cao.
  2. `HD_CanhBaoNhiet_KhoSinhPham`: Đèn tháp tín hiệu 3 màu báo động nhiệt độ tủ âm sâu.
  3. `HD_VachCachNhiet_KhoSinhPham`: Khung nẹp kim loại gia cố chân đế tải trọng nặng cho tủ đông âm sâu.

### 10. Khu 10: Phòng Sinh Học Phân Tử & PCR T2 (`phong_sinh_phan_tu`)
- **Bảng màu:** Trắng xanh hiện đại (`#EBF4F6`), Mặt bàn đá thạch anh đúc cao cấp xám nhạt (`#D2D7DF`), Inox satin.
- **Vật liệu chính:** Tủ an toàn sinh học cấp II Biobase inox 304, Buồng điện di trong suốt acrylic quang học, Màn hình chụp gel UV.
- **Cách đặt đèn:**
  - Đèn LED âm trần kết hợp đèn rọi cục bộ trên buồng PCR tránh ô nhiễm mẫu chéo.
- **3 Chi tiết kiến trúc `HD_*` thêm vào:**
  1. `HD_GiaTreoPipet_SinhPhanTu`: Giá đỡ pipet tự động xoay bằng acrylic gắn viền bàn.
  2. `HD_NapKhungChongSoc_SinhPhanTu`: Gioăng nẹp giảm chấn cho nắp máy ly tâm và máy Real-time PCR.
  3. `HD_MaoTranHapThu_SinhPhanTu`: Trần thạch cao tiêu âm đục lỗ tròn micro giảm tiếng ồn động cơ máy quay li tâm.

### 11. Khu 11: Hành Lang Tầng 2 (`hanh_lang_t2`)
- **Bảng màu (Chủ đề Gỗ ấm & Tối giản):**
  - Theme Gỗ ấm: Màu gỗ dẻ gai Châu Âu (`#B8977E`) kết hợp tường kem mịn.
  - Theme Tối giản: Màu bê tông sáng hiện đại (`#CFD3D7`) viền LED âm trần đen mờ.
- **Vật liệu chính:** Sàn gạch khổ lớn chống lóa, Vách kính toàn cảnh nhìn xuống thông tầng và sân trong.
- **Cách đặt đèn:**
  - Đèn LED thanh rãnh chìm và đèn hắt chân tường ban đêm dẫn hướng tinh tế.
- **3 Chi tiết kiến trúc `HD_*` thêm vào:**
  1. `HD_TayVinLanCan_HanhLangT2`: Tay vịn lan can gỗ bo tròn mịn gắn trên trụ kẹp kính inox không mối hàn.
  2. `HD_KheTranAmDen_HanhLangT2`: Khe thạch cao âm đèn LED gián tiếp uốn lượn mềm mại theo luồng giao thông.
  3. `HD_ThungRacPhanLoai_HanhLangT2`: Bộ thùng rác phân loại 3 ngăn âm tường inox mờ.

### 12. Khu 12: Hành Lang Phía Đông T2 (`hanh_lang_dong`)
- **Bảng màu:** Trắng ánh ban mai (`#FCFBF7`), Lam nhạt mát mẻ đón nắng sớm, Kính trong suốt mở ra không gian ngoài trời.
- **Vật liệu chính:** Nhôm anodized màu cát sa mạc, Kính cản nhiệt hai lớp hộp có khí trơ Argon.
- **Cách đặt đèn:**
  - Kết hợp ánh sáng trời tự nhiên qua cửa sổ vách kính và đèn downlight rọi điểm 3500K.
- **3 Chi tiết kiến trúc `HD_*` thêm vào:**
  1. `HD_RemKinhChongChoi_HanhLangDong`: Hộp rèm cuốn che nắng mỏng thả nhẹ phía mép trên cửa kính.
  2. `HD_GheNghiCho_HanhLangDong`: Băng ghế nghỉ chân dài ốp gỗ sồi gắn chìm vào vách tường.
  3. `HD_KhayCayXanh_HanhLangDong`: Khay bồn cây xanh nội thất mini đặt men chân tường mang thiên nhiên vào phòng.

### 13. Khu 13: Lõi Thang Máy & Thang Bộ (`loi_thang`)
- **Bảng màu:** Xám đá hoa cương granite (`#3D4043`), Inox gương xước kết hợp viền ánh vàng đồng thanh lịch (`#B39268`).
- **Vật liệu chính:** Cửa tầng thang máy inox mạ titan xước, Bậc thang bộ đá granite chống trượt mài cạnh vát, Kính giếng thang.
- **Cách đặt đèn:**
  - Cabin thang máy: Trần xuyên sáng Barrisol ánh sáng tự nhiên rực rỡ, không gây chói.
  - Thang bộ: Đèn LED bậc thang âm mũi bậc chiếu sáng từng bước chân an toàn.
- **3 Chi tiết kiến trúc `HD_*` thêm vào:**
  1. `HD_KhungVienCuaThang_LoiThang`: Khung phào inox ốp viền bao quanh toàn bộ cửa tầng thang máy.
  2. `HD_DenBaoTang_LoiThang`: Bảng hiển thị số tầng LED ma trận sắc nét và nút bấm cảm ứng kính đen.
  3. `HD_TayVinThangBo_LoiThang`: Tay vịn thang bộ liên tục hình elip bằng gỗ tự nhiên gắn chốt cùm inox vào tường.

### 14. Ngoại Thất Tòa Nhà VERA Lab (`mat_ngoai`)
- **Bảng màu:** Trắng ngà vữa sơn ngoại thất cao cấp (`#F5F5F0`), Xám than lam che nắng (`#343A40`), Xanh ngọc kính phản quang nhẹ (`#A9CCE3`), Xanh cỏ tự nhiên (`#457B3B`).
- **Vật liệu chính:** Sơn vữa silicat chịu thời tiết, Lam nhôm chắn nắng kiến trúc (brise-soleil), Cỏ mượt và sỏi tự nhiên rải bờ hiên.
- **Cách đặt đèn & Ánh sáng:**
  - Bầu trời HDRI ban ngày độ phân giải cao, mặt trời ở góc 52 độ tạo bóng đổ sắc nét và vệt sáng chân thực.
  - Đèn hắt ngoại thất ban đêm âm sàn chiếu sáng cột và logo trường VERA Lab.
- **3 Chi tiết kiến trúc `HD_*` thêm vào:**
  1. `HD_GoMaiVaMangNuoc_MatNgoai`: Gờ viền mái nhôm hộp và hệ thống máng thu nước mưa âm tinh tế.
  2. `HD_BaoTuongChongTham_MatNgoai`: Chân tường ốp đá granite xám đậm cao 60 cm ngăn nước văng và rêu mốc.
  3. `HD_VienKinhVaCuaSoAo_MatNgoai`: Khung nẹp cửa sổ thật và tái tạo kết cấu nướng cửa sổ ảo (cubemap/virtual window) sắc nét chống bể vỡ hình.

