---
mon: hoa
lop: 0
loai: tra_cuu
chu_de: Nhận biết anion trong dung dịch bằng thuốc thử
tu_khoa: [nhận biết anion, gốc axit, AgNO3, BaCl2, clorua, chloride, sunfat, sulfate, cacbonat, carbonate, nitrat, nitrate, photphat, sunfua, halide, nước vôi trong]
nguon:
  - Chương trình GDPT 2018 môn Hóa học (Thông tư 32/2018/TT-BGDĐT) – Hóa học 10 (ion halide), Hóa học 11 (sulfate, ammonium, nitrate), Hóa học 12 (nhận biết ion)
  - Vogel's Textbook of Macro and Semimicro Qualitative Inorganic Analysis (G. Svehla), các phản ứng định tính anion
  - LibreTexts Chemistry – Qualitative Analysis of Anions, https://chem.libretexts.org
  - Dữ liệu hóa chất của dự án VERA Lab, data/chem/hoa_chat.json
---

# Nhận biết anion trong dung dịch

## Nguyên tắc nhận biết anion và hai thuốc thử chính AgNO3, BaCl2

Nhận biết anion (gốc acid) dựa vào phản ứng tạo kết tủa có màu riêng, tạo khí có tính chất đặc trưng, hoặc phản ứng oxi hóa – khử làm đổi màu. Hai thuốc thử dùng nhiều nhất:

- Dung dịch silver nitrate (bạc nitrat) AgNO3: tạo kết tủa với Cl-, Br-, I-, PO4 3-, S2-, CO3 2-; màu kết tủa giúp phân biệt.
- Dung dịch barium chloride (bari clorua) BaCl2: tạo kết tủa trắng với SO4 2-, SO3 2-, CO3 2-, PO4 3-; chỉ BaSO4 không tan trong acid mạnh.

Thuốc thử thứ ba là acid mạnh loãng (HCl, H2SO4): làm thoát khí từ CO3 2-, HCO3-, SO3 2-, S2-, S2O3 2-; khí được nhận biết tiếp bằng nước vôi trong, nước bromine hoặc giấy tẩm muối lead.

Thứ tự hợp lí khi có nhiều anion: thử khí bằng acid trước, sau đó thử SO4 2- bằng BaCl2 trong môi trường acid, cuối cùng thử halide bằng AgNO3 trong môi trường HNO3 loãng (để CO3 2-, PO4 3- không gây nhiễu). Trong VERA Lab có Dung dịch AgNO3 0.02M và Dung dịch BaCl2 0.2M ở kệ dung dịch pha sẵn. Muối silver và barium độc, phải đeo găng tay và đổ chất thải vào bình chứa chất thải.

## Nhận biết ion halide Cl-, Br-, I- và F- bằng silver nitrate

Ion halide (chloride, bromide, iodide) được nhận biết bằng dung dịch silver nitrate AgNO3 trong môi trường HNO3 loãng; mỗi ion cho kết tủa màu khác nhau:

- Cl- (chloride, clorua): kết tủa trắng AgCl, tan trong dung dịch NH3; để ngoài sáng hóa tím xám rồi đen.
- Br- (bromide, bromua): kết tủa vàng nhạt AgBr, ít tan trong NH3.
- I- (iodide, iotua): kết tủa vàng đậm AgI, không tan trong NH3.
- F- (fluoride, florua): không có kết tủa vì AgF tan; nhận biết F- bằng ion Ca2+ tạo kết tủa trắng CaF2.

Ag+ + Cl- → AgCl↓ (trắng)
Ag+ + Br- → AgBr↓ (vàng nhạt)
Ag+ + I- → AgI↓ (vàng)

Có thể kiểm tra thêm bằng nước chlorine: chlorine oxi hóa Br- thành Br2 (dung dịch vàng cam) và I- thành I2 (dung dịch nâu; thêm hồ tinh bột hóa xanh tím). Thí nghiệm này cũng chứng minh tính oxi hóa giảm dần Cl2 > Br2 > I2 ở Hóa học 10.

Cl2 + 2KBr → 2KCl + Br2
Cl2 + 2KI → 2KCl + I2

## Nhận biết ion SO4 2-, SO3 2- và S2O3 2-

Ion sulfate SO4 2- (sunfat) được nhận biết bằng dung dịch chứa Ba2+ (BaCl2 hoặc Ba(NO3)2): tạo kết tủa trắng BaSO4 không tan trong acid HCl hoặc HNO3 dư. Đây là điểm phân biệt SO4 2- với CO3 2- và SO3 2-.

Ba2+ + SO4 2- → BaSO4↓

Ion sulfite SO3 2- (sunfit) cũng tạo kết tủa trắng BaSO3 với Ba2+, nhưng kết tủa tan trong HCl. Khi cho acid vào muối sulfite, khí sulfur dioxide SO2 thoát ra có mùi hắc, làm mất màu nước bromine và dung dịch thuốc tím:

SO3 2- + 2H+ → SO2↑ + H2O
SO2 + Br2 + 2H2O → H2SO4 + 2HBr

Ion thiosulfate S2O3 2- (thiosunfat) khi gặp acid tạo kết tủa sulfur màu vàng nhạt làm dung dịch vẩn đục, đồng thời có SO2 thoát ra:

S2O3 2- + 2H+ → S↓ + SO2↑ + H2O

Phản ứng của sodium thiosulfate với HCl dùng trong thí nghiệm tốc độ phản ứng ở Hóa học 10: thời gian xuất hiện vẩn đục ngắn hơn khi nồng độ hoặc nhiệt độ cao hơn. Khí SO2 độc, làm các thí nghiệm này với lượng nhỏ và trong tủ hút khí độc.

## Nhận biết ion sulfide S2-

Ion sulfide S2- (sunfua) được nhận biết bằng dung dịch muối lead(II) Pb2+ (ví dụ lead(II) acetate hoặc lead(II) nitrate) hoặc copper(II) Cu2+: tạo kết tủa màu đen PbS hoặc CuS.

Pb2+ + S2- → PbS↓ (đen)
Cu2+ + S2- → CuS↓ (đen)

Khi cho acid mạnh vào muối sulfide tan, khí hydrogen sulfide H2S thoát ra có mùi trứng thối; khí này làm giấy lọc tẩm dung dịch lead(II) acetate hóa đen.

S2- + 2H+ → H2S↑

An toàn: H2S là khí rất độc, ở nồng độ cao làm tê liệt khứu giác nên không còn ngửi thấy mùi. Chỉ làm với lượng rất nhỏ trong tủ hút khí độc, không ngửi trực tiếp. Muối của lead độc, thu gom riêng. Kết tủa CuS và PbS không tan trong acid loãng, nên phản ứng Cu2+ + H2S → CuS + 2H+ xảy ra được dù H2S là acid yếu; đây là ví dụ về phản ứng trao đổi tạo chất kết tủa rất ít tan.

## Nhận biết ion CO3 2- và HCO3- (carbonate và hydrogencarbonate)

Ion carbonate CO3 2- (cacbonat) được nhận biết bằng acid mạnh loãng: sủi bọt khí carbon dioxide CO2 không màu, không mùi; dẫn khí qua nước vôi trong Ca(OH)2 thấy vẩn đục trắng.

CO3 2- + 2H+ → CO2↑ + H2O
CO2 + Ca(OH)2 → CaCO3↓ + H2O

Với dung dịch BaCl2 hoặc CaCl2, CO3 2- tạo kết tủa trắng BaCO3, CaCO3, kết tủa tan trong acid và sủi bọt khí.

Ion hydrogencarbonate HCO3- (hiđrocacbonat) cũng sủi bọt CO2 với acid, nhưng không tạo kết tủa với BaCl2 hoặc CaCl2 ở nhiệt độ thường vì Ba(HCO3)2, Ca(HCO3)2 tan. Khi đun nóng dung dịch HCO3- có Ca2+, kết tủa CaCO3 xuất hiện:

Ca(HCO3)2 → CaCO3↓ + CO2↑ + H2O (khi đun nóng)

Phản ứng này giải thích cặn trong ấm đun nước và cách làm mềm nước cứng tạm thời. Phân biệt khí CO2 với SO2: cả hai đều làm đục nước vôi trong, nhưng chỉ SO2 làm mất màu nước bromine và có mùi hắc.

## Nhận biết ion NO3- và PO4 3- (nitrate và phosphate)

Ion nitrate NO3- (nitrat) không tạo kết tủa với thuốc thử thông thường vì hầu hết muối nitrate tan. Cách nhận biết trong chương trình THPT: thêm vụn copper (đồng) và dung dịch H2SO4 loãng, đun nhẹ. Dung dịch chuyển dần sang màu xanh lam (Cu2+), có khí không màu NO thoát ra, khí này hóa nâu đỏ (NO2) khi gặp không khí ở miệng ống nghiệm.

3Cu + 8H+ + 2NO3- → 3Cu2+ + 2NO↑ + 4H2O
2NO + O2 → 2NO2 (nâu đỏ)

NO và NO2 là khí độc, chỉ làm với lượng rất nhỏ trong tủ hút khí độc. Trong phòng thí nghiệm còn dùng thuốc thử diphenylamine trong H2SO4 đặc (có ở tủ Thuốc thử của VERA Lab), cho màu xanh lam với nitrate; thuốc thử này có acid đặc nên do giáo viên thao tác.

Ion phosphate PO4 3- (photphat) được nhận biết bằng dung dịch AgNO3: tạo kết tủa vàng Ag3PO4, kết tủa tan trong HNO3 loãng (khác AgI không tan).

3Ag+ + PO4 3- → Ag3PO4↓ (vàng)

Thuốc thử ammonium molybdate trong HNO3 cho kết tủa vàng với phosphate, dùng ở phân tích phân bón.

## Bảng tóm tắt nhận biết anion thường gặp

Bảng tóm tắt thuốc thử và dấu hiệu nhận biết các anion thường gặp trong chương trình Hóa học THPT:

| Anion | Thuốc thử | Dấu hiệu |
|---|---|---|
| Cl- | AgNO3 (môi trường HNO3) | Kết tủa trắng AgCl, tan trong NH3 |
| Br- | AgNO3 | Kết tủa vàng nhạt AgBr |
| I- | AgNO3; hoặc nước chlorine + hồ tinh bột | Kết tủa vàng AgI; hoặc màu xanh tím |
| SO4 2- | BaCl2 | Kết tủa trắng BaSO4, không tan trong acid |
| CO3 2- | Acid loãng, khí dẫn vào nước vôi trong | Sủi bọt không mùi, nước vôi vẩn đục |
| SO3 2- | Acid loãng, khí dẫn vào nước bromine | Khí mùi hắc, làm mất màu nước bromine |
| S2- | Pb2+ hoặc Cu2+ | Kết tủa đen PbS, CuS |
| PO4 3- | AgNO3 | Kết tủa vàng Ag3PO4, tan trong HNO3 |

Ion OH- nhận biết bằng chất chỉ thị: quỳ tím hóa xanh, phenolphthalein hóa hồng. Ion NO3- nhận biết bằng Cu và H2SO4 loãng (khí hóa nâu trong không khí).

## Hỏi đáp nhanh: nhận biết anion

- Nhận biết ion clorua (Cl-) bằng gì? Dung dịch AgNO3, tạo kết tủa trắng AgCl.
- Phân biệt Cl-, Br-, I- thế nào? Với AgNO3: AgCl trắng, AgBr vàng nhạt, AgI vàng.
- Thuốc thử của ion sunfat (SO4 2-)? Dung dịch BaCl2, tạo kết tủa trắng BaSO4 không tan trong acid.
- Muối cacbonat gặp acid có hiện tượng gì? Sủi bọt khí CO2 không mùi, làm đục nước vôi trong.
- Làm sao phân biệt CO2 và SO2? SO2 mùi hắc, làm mất màu nước bromine; CO2 không làm mất màu.
- Nhận biết ion nitrate bằng gì? Cu và H2SO4 loãng, đun nhẹ: dung dịch xanh, khí không màu hóa nâu đỏ trong không khí.
- Ion sunfua nhận biết thế nào? Dung dịch muối Pb2+ hoặc Cu2+ cho kết tủa đen.
- Ag3PO4 màu gì? Vàng, tan trong HNO3.
- Vì sao thử halide trong môi trường HNO3? Để CO3 2-, PO4 3- không tạo kết tủa với Ag+ gây nhầm.
- F- có tạo kết tủa với AgNO3 không? Không, AgF tan; dùng Ca2+ tạo CaF2 trắng.
