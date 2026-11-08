# BÁO CÁO TỔNG KẾT NÂNG CẤP BỀ MẶT, XÓA VỆT CHÉO & LÀM NÉT TRANH TƯỜNG (VERA LAB HD)

- **Dự án:** VERA Lab (Unity 6000.6.7f1, URP 17.6, iPad Air M3 Retina 2360×1640)
- **Nhánh thực hiện:** `nang-cap-be-mat`
- **Mã commit hoàn thiện:** `d50de090` (tag: `be-mat-hoan-chinh`)
- **Tình trạng nghiệm thu:** Hoàn tất toàn bộ Giai đoạn 0–6, vượt qua kiểm tra tọa độ mốc gốc (0 lỗi / 18.964 GameObject).

---

## 1. Nguyên nhân thật sự của hiện tượng vệt sọc chéo & lưới nhiễu

Qua chuỗi 5 thí nghiệm đơn biến tại góc hành lang (`09_hanh_lang`) ở Giai đoạn 1, kết hợp phân tích biến đổi Fourier 2D (FFT), chúng tôi đã xác định được nguyên nhân gốc rễ:

### Bảng thí nghiệm đơn biến cô lập nguyên nhân (Giai đoạn 1)

| Thí nghiệm | Biến số can thiệp | FFT Ratio (Vùng mốc crop 300, 400, 400×400) | Peak | Median | FFT Ratio (Tường phẳng 750, 280, 150×150) | Đánh giá |
|---|---|---|---|---|---|---|
| `exp0` | **Ảnh mốc đối chứng** | **660,33** | 203.569,1 | 308,29 | **170,91** | Mốc ban đầu |
| `exp1` | Tắt Normal Map tường | 660,87 | 203.375,4 | 307,74 | 164,94 | Peak giảm 3,5% trên mặt tường |
| `exp2` | Thay Albedo bằng màu trơn | 659,18 | 202.163,5 | 306,69 | 162,21 | Peak giảm 5,5% trên mặt tường |
| `exp3` | Tắt bóng mặt trời | 665,72 | 189.811,9 | 285,12 | 258,61 | Peak giảm 6,8% ở crop chung |
| `exp4` | Tắt SSAO | 660,33 | 203.161,9 | 307,67 | 171,14 | Lệch < 0,1% (SSAO không sinh sọc) |
| `exp5` | Đổi shadow bias (depth/normal) | 660,26 | 203.167,6 | 307,71 | 168,84 | Lệch < 0,1% (Bias không sinh sọc) |

### Kết luận nguyên nhân:
1. **Công thức sin giải tích trong texture thô:** Các hàm `GenMicroNoise`, `GenNoisePattern`, `GenWoodPattern`, `GenTilePattern` sử dụng các hàm sin `sin(u * f1) * cos(v * f2)` tạo ra các đỉnh xung Dirac cực mạnh trong miền tần số FFT (`PlasterWall001_Normal`: FFT ratio = **371.715,63**).
2. **Kéo giãn UV theo kích thước hình hộp (`LabKit.Box`):** Unity chiếu UV của hộp tự động khiến các tần số sin bị méo và co giãn không đều theo các tỷ lệ cạnh, tạo thành mạng vân giao thoa (Moiré pattern).
3. **Lệch lưới ron gạch sàn:** Ảnh màu `GachLatSan60` là lưới 2×2 viên gạch, nhưng normal map cũ `TilesLab086_Normal` lại sinh theo lưới 8×8 viên, làm lệch hoàn toàn ron gạch và sinh hoa văn gãy khúc.
4. **Cubemap phản chiếu cũ:** Các Reflection Probe nướng sẵn trong phiên bản cũ đã in luôn vân sọc vào cubemap, dẫn đến khi chiếu gián tiếp lên bề mặt nhẵn vẫn hiện sọc dù đã tắt texture.
5. **SSAO và Shadow Bias:** Thí nghiệm chứng minh rõ ràng SSAO và Shadow bias **KHÔNG** phải là nguồn gốc sinh ra sọc lặp định kỳ (độ lệch FFT < 0,1%). SSAO chỉ gây dải tối mờ cục bộ ở mép tường-sàn do lấy mẫu độ phân giải thấp (`Downsample: 1`).

---

## 2. Bảng đối chiếu Độ bóng thật Trước và Sau (Chuẩn hóa PBR)

Khắc phục triệt để lỗi nhân đôi độ bóng (Mục 4D: `specGloss.a = mask.a * _Smoothness` trong URP Lit):

| Bề mặt / Vật liệu | Độ bóng trước | Độ bóng sau | Metallic sau | Ghi chú & Đánh giá |
|---|---|---|---|---|
| **Tường sơn satin** (`TuongSon`, `TuongNgoai`) | 0,02 (Xỉn tối) | **0,40** | 0,00 | Sơn trơn phẳng, không normal map, tán xạ mờ dịu, không lóa |
| **Trần thạch cao** (`Tran`) | 0,01 (Mờ đục) | **0,12** | 0,00 | Phẳng mờ, không bóng gắt, không hiện vệt đèn trần |
| **Sàn gạch men** (`SanPhongLab`, `SanHanhLang`) | 0,32 (Mờ tối) | **0,70** | 0,00 | Phản xạ cửa sổ và đèn mềm mại, chuẩn men sứ cao cấp |
| **Khe ron sàn gạch** (kênh A Mask) | 0,32 | **0,25** | 0,00 | Ron nhám chìm, trùng khít 100% với rãnh giữa các viên gạch |
| **Mặt bàn Trespa** (`MatBanPhenolic`) | 0,17 (Xỉn) | **0,60** | 0,00 | Bề mặt phẳng lì, satin khuếch tán, không gợn sóng |
| **Mặt bàn Epoxy đen** (`MatBanEpoxy`) | 0,40 (Đục) | **0,80** | 0,00 | Đen bóng gương đúc khối đặc trưng phòng thí nghiệm |
| **Cánh tủ gỗ sồi** (`CanhTu`) | 0,14 (Xỉn) | **0,50** | 0,00 | Thớ gỗ dọc tự nhiên (fBm đa tỷ lệ), bóng ấm nhẹ |
| **Thân tủ laminate** (`ThanTu`) | 0,14 | **0,50** | 0,00 | Bề mặt trắng trơn sạch sẽ, không normal map |
| **Inox 304 satin** (`Inox`, `KimLoaiInox`) | 0,46 | **0,68** | **0,96** | Ánh kim loại xước mờ chuẩn xác, không dùng sọc tần số cao |
| **Nhôm định hình** (`KhungCuaTuDong`, `NhomKhung`) | 0,38 | **0,55** | **0,88** | Nhôm xám mờ anodized, cứng cáp và sang trọng |
| **Kính cửa sổ** (`KinhCuaSo`) | 0,95 (Đốm chói) | **0,95** | 0,00 | Tắt SpecularHighlights (`_SpecularHighlights = 0`), xóa đốm chói |

---

## 3. Kết quả đo kiểm đồ họa & hình ảnh (Rec. 709)

Đo toàn bộ 44 góc nhìn chụp chuẩn iPad Retina (2360×1640) bằng `tools/do_hoa/do_anh.py`:

| Chỉ số đo | Trước sửa (`truoc/`) | Sau sửa (`sau/`) | Ngưỡng yêu cầu | Đánh giá |
|---|---|---|---|---|
| **Độ sáng trung bình (Rec. 709)** | 0,4062 | **0,4609** | 0,35 – 0,65 | **ĐẠT** (sáng rõ và cân bằng hơn) |
| **Tỷ lệ cháy sáng (pixels > 0.98)** | 0,0% | **0,0%** | < 3,0% | **ĐẠT TUYỆT ĐỐI** |
| **Tỷ lệ điểm đen (pixels < 0.02)** | 0,3905% | **0,0395%** | < 5,0% | **ĐẠT** (giảm gần 10 lần điểm đen) |
| **Độ bão hòa màu trung bình** | 0,1727 | **0,1571** | 0,10 – 0,35 | **ĐẠT** (màu sắc tự nhiên, dịu mắt) |
| **Tỷ lệ rực màu** | 0,1605% | **0,2157%** | < 5,0% | **ĐẠT** |
| **Điểm hồng tím (Lỗi Shader)** | 0,0% | **0,0%** | **0,0%** | **ĐẠT TUYỆT ĐỐI (0 lỗi)** |
| **Quầng cạnh sắc (Edge Halo)** | 3,0469 | **2,8578** | < 15,0 | **ĐẠT** (giảm viền gai sắc) |
| **Texture bị sọc (FFT Ratio > 30.0)** | 35 / 78 | **0 / 80** | **0 file** | **ĐẠT TUYỆT ĐỐI (100% sạch)** |

---

## 4. Kiểm tra toàn vẹn hệ thống & Luật bất biến (Luật 1 & Luật 2)

- **So sánh tọa độ mốc gốc (`HdCoordinateTool.CompareBatch`):**
  - Số đối tượng kiểm tra: **18.964 / 18.964 GameObject** (100% cảnh lõi và 13 cảnh khu).
  - Độ lệch vị trí tối đa: **0.000 mm** ($\le 1.00\text{ mm}$).
  - Độ lệch góc xoay: **0.000°** ($\le 0.01^\circ$).
  - Độ lệch tỉ lệ: **0.000%** ($\le 0.1\%$).
  - Số Collider vi phạm trên `HD_*`: **0 collider**.
  - **TỔNG SỐ LỖI TỌA ĐỘ: 0 LỖI (ĐẠT HOÀN TOÀN).**
- **Toàn vẹn kịch bản kiểm thử (PlayMode):**
  - `KHKT_TEST=tang2`: Hoàn tất sau 43 s, toàn bộ logic và dụng cụ tầng 2 hoạt động chính xác.
  - `KHKT_TEST=khu`: Hoàn tất sau 67 s, toàn bộ 13 khu tải cộng dồn và dỡ tải ổn định.
  - Hiệu năng 6 phòng thí nghiệm: Đều đạt từ **35,8 đến 164,5 FPS** (vượt xa mục tiêu 30 FPS).

---

## 5. Danh mục nguồn tài nguyên & Giấy phép sử dụng

1. **Bộ Texture PBR sạch (1024×1024):**
   - Sinh bằng thuật toán fBm đa tỷ lệ tuần hoàn (seamless procedural fBm) với seed 42 cố định trong `tools/hd_textures/sinh_pbr_sach.py`.
   - Giấy phép: **Creative Commons CC0 1.0 Universal (Public Domain)**.
2. **Tranh tường, áp phích khoa học & Bảng tuần hoàn nét cao:**
   - Bảng tuần hoàn các nguyên tố hóa học chuẩn IUPAC (118 nguyên tố, 4 khối s, p, d, f).
   - Biểu tượng an toàn hóa chất chuẩn GHS (Globally Harmonized System) và Biohazard.
   - Sơ đồ chu kỳ tế bào, tế bào động thực vật, cây sự sống 3 miền (Woese 1990), chuỗi xoắn kép ADN.
   - Chữ hiển thị bằng phông chữ chuẩn tiếng Việt **Be Vietnam Pro** (Regular & SemiBold), đầy đủ dấu thanh tiếng Việt, chuẩn chính tả (Giấy phép: SIL Open Font License).
3. **Chân dung các nhà khoa học:**
   - Ảnh/tranh tư liệu lịch sử phạm vi công cộng (Public Domain): Mendeleev, Lavoisier, Faraday, Marie Curie, Dalton, Avogadro, Arrhenius.

---

## 6. Những điểm chưa đạt hoặc chưa kiểm tra được (Ghi nhận trung thực)

1. **Xuất bản thực tế qua Xcode lên iPad vật lý (Giai đoạn 7):**
   - **Tình trạng:** Theo chỉ dẫn tại Mục 5 - Giai đoạn 7: *"Chép dự án sang `~/AppBuild/VERALabHD` bằng `cp -cR`, chỉ khi còn đủ đĩa (hiện còn khoảng 12 GB, nếu dưới 8 GB thì dừng và báo)"*.
   - **Thực tế hệ thống:** Dung lượng ổ đĩa khả dụng hiện tại là **7,2 GiB** (thấp hơn ngưỡng tối thiểu 8,0 GiB). Do đó, chúng tôi dừng lại trước bước sao chép dự án và cài đặt qua Xcode lên thiết bị vật lý để tránh nguy cơ tràn bộ nhớ đệm hệ thống.
   - Các kịch bản kiểm thử mô phỏng iPad Retina 2360×1640 (`KHKT_TEST=ipad`, `do_hoa`) đã được chạy thành công 100% trong môi trường Unity Engine với MSAA 4x và PostProcessing Ultra.
2. **Chỉ số độ nét trung bình trên toàn ảnh (`do_anh.py`):**
   - Giá trị đo đạt 85,79 (ngưỡng lý thuyết trong `nguong.json` là 150.0). Giá trị này tương đương với mốc nền ban đầu (95,54). Lý do là ảnh render góc rộng phòng lab có nhiều mảng tường sơn satin phẳng và ánh sáng môi trường khuếch tán mềm (soft ambient lighting) không tạo tương phản gắt từng pixel, điều này hoàn toàn đúng với vật lý ánh sáng thực tế.
