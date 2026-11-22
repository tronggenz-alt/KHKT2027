# Bảng Kê Dụng Cụ Sinh Học Tầng 2 (Khu Sinh Học VERA Lab)

Tài liệu này kê khai toàn bộ thiết bị, mô hình và dụng cụ tại Khu Sinh học Tầng 2, phân loại cầm được (`Grabbable`) và không cầm được theo quy tắc kỹ thuật nghiêm ngặt.

---

## 1. Luật Phân Loại

Tuân thủ tuyệt đối quy tắc phân loại hệ thống:

- **Không cầm (`Grabbable = false`):**
  - Tủ, máy cỡ lớn, đồ gắn tường, máy cắm điện cố định, máy có màn hình vận hành hoặc đã có `IDemoUsable` (kính hiển vi quang học, tủ an toàn sinh học cấp II, tủ ấm vi sinh, nồi hấp tiệt trùng, tủ lạnh 4 °C, tủ đông −20 °C, tủ âm sâu −80 °C, máy PCR, máy điện di gel, buồng chụp gel, máy ly tâm, máy đo pH, cân phân tích, bể ổn nhiệt, máy lắc vortex, máy quang phổ OD600...).
  - Bàn giải phẫu số `DigitalAnatomyTable`, mô hình bộ xương người trên giá, mô hình bán thân người, mô hình tế bào động vật/thực vật lớn, mô hình ADN xoắn kép lớn trên đế.
  - Cây trồng cố định trong giàn thủy canh NFT, cây cảnh cao đặt góc phòng (`kind = 4`).

- **Cầm được (`Grabbable = true`):**
  - Toàn bộ đồ dùng, dụng cụ nhỏ còn lại trên bàn học sinh, đảo làm việc, quầy thao tác và nhà kính.
  - Đĩa petri, giá ống nghiệm và từng ống ly tâm/nghiệm, micropipet, lọ/bình mẫu ngâm formalin, lọ thuốc nhuộm nhỏ giọt, khay dụng cụ inox, kẹp y tế, dao mổ, kim mũi mác, lam kính, hộp tiêu bản và từng tiêu bản, chậu cây nhỏ (môn, dương xỉ, xà lách, cây mầm đậu non), bình tia nước cất, cốc thủy tinh, chai hóa chất, bình tưới cây, giấy thấm, củ hành tím, mẫu vật gan/khoai tây, đũa thủy tinh.
  - **Cơ chế lồng nhau (`PlaceSocket`):**
    - Giá ống nghiệm (`kind = "gia_ong"`): cầm được cả giá mang đi; từng ống rút ra cầm riêng được (`kind = "ong_ly_tam"`), cắm lại vào đúng lỗ có `PlaceSocket`.
    - Hộp tiêu bản (`kind = "hop_tieu_ban"`): cầm được cả hộp; từng tiêu bản (`kind = "tieu_ban"`) rút ra cầm riêng, cắm lại vào đúng khe có `PlaceSocket`.
  - **Quy ước tọa độ:**
    - Gốc tọa độ pivot đặt ở đáy vật (`y = 0` trong hệ local, sai số < 2 mm).
    - `localScale` của root object luôn là `(1, 1, 1)`.
    - Vật phẳng / dài nằm ngang (`liesOnTable = true`): khai báo chính xác `lyingRadiusM` và `lengthM` để khi đặt lên mặt bàn (`PlaceSurface`) tiếp xúc phẳng hoàn hảo, không lún, không lơ lửng.

---

## 2. Bảng Kê Chi Tiết Dụng Cụ Cầm Được Tầng 2

| STT | Tên dụng cụ | Phân loại (`kind`) | Kích thước (m) | Khối lượng (g) | `gripHeightM` | `liesOnTable` | `lyingRadiusM` (m) | `lengthM` (m) | Vị trí xuất hiện |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Đĩa petri có thạch | `dia_petri` | Ø0,09 × 0,015 | 45 | 0,015 | false | — | — | Bàn sinh học 1-9, Tủ an toàn, Đảo vi sinh |
| 2 | Giá ống ly tâm (nhựa) | `gia_ong` | 0,05 × 0,04 × 0,17 | 170 | 0,04 | false | — | 0,17 | Tủ an toàn, Quầy chuẩn bị, Đảo tách ADN |
| 3 | Ống ly tâm 1,5 ml / 15 ml | `ong_ly_tam` | Ø0,017 × 0,085 | 15 | 0,04 | true | 0,0085 | 0,085 | Cắm trong giá ống `TubeRack` |
| 4 | Micropipet (P20 / P200 / P1000) | `pipet` | Ø0,028 × 0,25 | 95 | 0,15 | true | 0,014 | 0,25 | Giá micropipet `PipetteStation` |
| 5 | Hộp đầu côn (vàng / xanh) | `hop_dau_con` | 0,08 × 0,065 × 0,12 | 80 | 0,035 | false | — | — | Giá micropipet `PipetteStation` |
| 6 | Bình mẫu ngâm formalin (cá) | `binh_ngam` | Ø0,14 × 0,24 | 450 | 0,12 | false | — | — | Tủ mẫu ngâm, Tường triển lãm |
| 7 | Bình mẫu ngâm formalin (ếch) | `binh_ngam` | Ø0,14 × 0,24 | 450 | 0,12 | false | — | — | Tủ mẫu ngâm, Tường triển lãm |
| 8 | Bình mẫu ngâm formalin (rắn) | `binh_ngam` | Ø0,14 × 0,24 | 450 | 0,12 | false | — | — | Tủ mẫu ngâm, Tường triển lãm |
| 9 | Bình mẫu ngâm formalin (tim) | `binh_ngam` | Ø0,14 × 0,24 | 450 | 0,12 | false | — | — | Tủ mẫu ngâm, Tường triển lãm |
| 10 | Bình mẫu ngâm formalin (cây non) | `binh_ngam` | Ø0,14 × 0,24 | 450 | 0,12 | false | — | — | Tủ mẫu ngâm, Tường triển lãm |
| 11 | Chai thuốc nhuộm nhỏ giọt (xanh methylen) | `lo_nhuom` | Ø0,04 × 0,09 | 65 | 0,045 | false | — | — | Giá thuốc thử bàn 1-9, Đảo vi sinh |
| 12 | Chai thuốc nhuộm nhỏ giọt (Lugol) | `lo_nhuom` | Ø0,04 × 0,09 | 65 | 0,045 | false | — | — | Giá thuốc thử bàn 1-9, Đảo vi sinh |
| 13 | Chai thuốc nhuộm nhỏ giọt (tím tinh thể) | `lo_nhuom` | Ø0,04 × 0,09 | 65 | 0,045 | false | — | — | Giá thuốc thử bàn 1-9, Đảo vi sinh |
| 14 | Chai thuốc nhuộm nhỏ giọt (Safranin) | `lo_nhuom` | Ø0,04 × 0,09 | 65 | 0,045 | false | — | — | Giá thuốc thử bàn 1-9, Đảo vi sinh |
| 15 | Khay inox dụng cụ | `khay_dung_cu` | 0,18 × 0,012 × 0,26 | 250 | 0,01 | false | — | — | Bàn sinh học 1-9 |
| 16 | Kẹp y tế inox | `kep` | 0,008 × 0,004 × 0,14 | 25 | 0,01 | true | 0,003 | 0,14 | Trong khay dụng cụ |
| 17 | Dao mổ cán kim loại | `dao_mo` | 0,01 × 0,004 × 0,15 | 30 | 0,01 | true | 0,0035 | 0,15 | Trong khay dụng cụ |
| 18 | Kim mũi mác | `kim_mui_mac` | 0,006 × 0,004 × 0,13 | 15 | 0,01 | true | 0,0025 | 0,13 | Trong khay dụng cụ |
| 19 | Lam kính hiển vi (rời) | `lam_kinh` | 0,026 × 0,0012 × 0,076 | 5 | 0,01 | true | 0,001 | 0,076 | Trong khay dụng cụ bàn 1-9 |
| 20 | Hộp tiêu bản cố định | `hop_tieu_ban` | 0,1 × 0,034 × 0,2 | 120 | 0,02 | false | — | — | Bàn sinh học 1-9 |
| 21 | Tiêu bản cố định (10 loại bài học) | `tieu_ban` | 0,076 × 0,026 × 0,0012 | 5 | 0,01 | true | 0,0012 | 0,076 | Cắm trong khe `SlideBox` |
| 22 | Bình tia nước cất | `binh_tia` | Ø0,072 × 0,2 | 120 | 0,1 | false | — | — | Bàn sinh học 1-9 |
| 23 | Cốc đong 100 ml (co nguyên sinh) | `coc` | Ø0,052 × 0,07 | 75 | 0,04 | false | — | — | Bàn sinh học 1 |
| 24 | Củ hành tím thí nghiệm | `hanh_tim` | Ø0,07 × 0,06 | 60 | 0,03 | false | — | — | Bàn sinh học 1 |
| 25 | Tập giấy thấm | `giay_tham` | 0,1 × 0,008 × 0,1 | 15 | 0,005 | true | 0,004 | 0,1 | Bàn sinh học 1-9 |
| 26 | Chai hóa chất dung tích 500 ml | `chai_hoa_chat` | Ø0,076 × 0,2 | 300 | 0,09 | false | — | — | Bàn sinh học 1, 4; Đảo tách ADN |
| 27 | Miếng gan thí nghiệm catalase | `mau_vat` | 0,04 × 0,02 × 0,05 | 20 | 0,01 | false | — | — | Bàn sinh học 4 |
| 28 | Lát khoai tây thí nghiệm catalase | `mau_vat` | 0,05 × 0,012 × 0,05 | 15 | 0,006 | false | — | — | Bàn sinh học 4 |
| 29 | Cốc dịch nghiền chuối 250 ml | `coc` | Ø0,07 × 0,1 | 130 | 0,05 | false | — | — | Đảo tách chiết ADN |
| 30 | Đũa thủy tinh khuấy sợi ADN | `dua_thuy_tinh` | Ø0,006 × 0,2 | 18 | 0,01 | true | 0,003 | 0,2 | Đảo tách chiết ADN |
| 31 | Bình tưới cây có vòi | `binh_tuoi` | Ø0,16 × 0,24 | 220 | 0,12 | false | — | — | Nhà kính thực vật |
| 32 | Chậu cây nhỏ (loại 0, 1, 2, 3) | `chau_cay` | Ø0,14..0,22 × 0,07..0,16 | 200–350 | 0,05..0,08 | false | — | — | Bàn trồng cây di động, bàn sang chậu |

---

## 3. Danh Mục Thiết Bị Cố Định (Không Cầm Được)

| Tên thiết bị | Vị trí | Lý do không gắn `Grabbable` theo luật |
|---|---|---|
| Kính hiển vi quang học hai mắt | Bàn sinh học 1–9, Đảo vi sinh | Thiết bị quang học tương tác có `MicroscopeController` / `IDemoUsable` |
| Tủ an toàn sinh học cấp II | Tường bắc lab sinh học, phòng vi sinh | Thiết bị cố định lớn (1,5 m), hệ thống quạt lọc HEPA |
| Tủ ấm vi sinh 37 °C | Khu tủ ấm, phòng vi sinh | Thiết bị điện cố định có buồng nhiệt |
| Nồi hấp tiệt trùng đứng | Khu tủ ấm | Thiết bị áp suất cao đứng sàn cố định |
| Máy ly tâm để bàn | Khu tủ ấm, phòng vi sinh, phòng sinh học phân tử | Máy quay tốc độ cao cố định |
| Bể ổn nhiệt (water bath) | Khu tủ ấm, phòng sinh học phân tử | Thiết bị gia nhiệt cách thủy để bàn |
| Cân phân tích lồng kính | Khu tủ ấm, phòng vi sinh | Thiết bị đo lường chính xác chống rung |
| Máy đo pH để bàn | Khu tủ ấm | Thiết bị đo điện hóa có giá đỡ điện cực |
| Máy lắc vortex | Khu tủ ấm, phòng vi sinh, tiền PCR | Thiết bị rung lắc để bàn cố định |
| Máy PCR (luân nhiệt) | Quầy hậu PCR | Thiết bị luân nhiệt chu kỳ có màn hình cảm ứng |
| Bể điện di gel agarose + nguồn | Quầy hậu PCR | Thiết bị điện cực cao áp 100 V |
| Buồng chụp gel (Gel doc) | Quầy hậu PCR | Buồng tối camera soi huỳnh quang |
| Máy đo quang phổ OD600 | Đảo tách chiết ADN | Thiết bị quang phổ phân tích |
| Bàn giải phẫu số BodyParts3D | Khu giải phẫu số | Màn hình cảm ứng lớn 2,2 m có `DigitalAnatomyTable` |
| Mô hình bộ xương người trên giá | Khu giải phẫu số | Mô hình giải phẫu đứng cao 1,7 m trên giá bánh xe |
| Mô hình bán thân người | Khu giải phẫu số, kho sinh phẩm | Mô hình giải phẫu bán thân lớn trên bục |
| Mô hình ADN xoắn kép | Tường triển lãm, kho sinh phẩm | Mô hình dạy học cố định trên đế |
| Mô hình tế bào động vật / thực vật | Tường triển lãm, kho sinh phẩm | Mô hình dạy học cố định trên đế |
| Giàn thủy canh NFT 3 tầng | Nhà kính thực vật | Giàn khung nhôm 5 m có bơm tuần hoàn |
| Cây cảnh cao trang trí (`kind = 4`) | Các góc phòng và hành lang | Cây chậu lớn cao 1,2–1,5 m đứng sàn |
| Tủ lạnh 4 °C / Tủ đông −20 °C / −80 °C | Kho sinh phẩm, phòng vi sinh | Tủ bảo quản lạnh dung tích lớn |
| Pass-box xuyên tường | Vách ngăn lab và phòng vi sinh | Hộp trung chuyển mẫu gắn tường có khóa liên động |

