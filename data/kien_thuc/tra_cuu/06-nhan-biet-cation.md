---
mon: hoa
lop: 0
loai: tra_cuu
chu_de: Nhận biết cation trong dung dịch bằng thuốc thử và màu ngọn lửa
tu_khoa: [nhận biết cation, nhận biết ion kim loại, thuốc thử, NaOH, kết tủa, NH4+, Fe3+, Fe2+, Cu2+, Al3+, Ba2+, Ag+, thử màu ngọn lửa, hydroxide lưỡng tính]
nguon:
  - Chương trình GDPT 2018 môn Hóa học (Thông tư 32/2018/TT-BGDĐT) – Hóa học 12, nhận biết một số ion trong dung dịch
  - Vogel's Textbook of Macro and Semimicro Qualitative Inorganic Analysis (G. Svehla), các phản ứng định tính cation
  - LibreTexts Chemistry – Qualitative Analysis of Cations, https://chem.libretexts.org
  - Dữ liệu hóa chất của dự án VERA Lab, data/chem/hoa_chat.json (nhóm Thuốc thử, Dung dịch pha sẵn)
---

# Nhận biết cation trong dung dịch

## Nguyên tắc nhận biết cation và cách làm an toàn

Nhận biết cation là dùng thuốc thử tạo ra dấu hiệu dễ thấy với một ion: kết tủa có màu riêng, kết tủa tan trong thuốc thử dư, dung dịch đổi màu, khí có mùi hoặc màu ngọn lửa đặc trưng. Thuốc thử chung hay dùng nhất là dung dịch sodium hydroxide (natri hiđroxit) NaOH loãng, vì phần lớn ion kim loại tạo hydroxide kết tủa có màu khác nhau. Thuốc thử thứ hai là dung dịch ammonia NH3, dùng để phân biệt ion tạo phức tan (Cu2+, Zn2+, Ag+, Ni2+).

Quy trình chung: lấy khoảng 1–2 mL dung dịch cần thử vào ống nghiệm sạch, nhỏ từng giọt thuốc thử, lắc nhẹ, quan sát; sau đó nhỏ tiếp đến dư để xem kết tủa có tan không. Mỗi lần thử dùng một mẫu mới, không đổ thuốc thử trở lại lọ.

Trong VERA Lab, thuốc thử nằm ở kệ "Dung dịch pha sẵn" (Dung dịch NaOH 0.5M, Dung dịch NH3 2M, Dung dịch BaCl2 0.2M, Dung dịch AgNO3 0.02M) và tủ "Thuốc thử". An toàn: đeo kính bảo hộ và găng tay; muối của barium, lead, silver, chromium độc; dung dịch NaOH làm bỏng da và mắt. Chất thải chứa kim loại nặng đổ vào bình chứa chất thải riêng, không đổ xuống bồn rửa.

## Nhận biết ion ammonium NH4+ và ion kim loại kiềm bằng màu ngọn lửa

Ion ammonium NH4+ (amoni) được nhận biết bằng dung dịch kiềm đun nóng nhẹ: khí ammonia NH3 thoát ra có mùi khai và làm giấy quỳ tím ẩm hóa xanh.

NH4+ + OH- → NH3↑ + H2O

Ion kim loại kiềm (Li+, Na+, K+) hầu như không tạo kết tủa với thuốc thử thông thường, nên được nhận biết bằng thử màu ngọn lửa: nhúng dây platinum hoặc nichrome sạch (đã rửa bằng dung dịch HCl và đốt đến khi không còn màu) vào dung dịch muối, rồi đưa vào phần ngọn lửa không màu của đèn khí. Màu đặc trưng:

- Li+: đỏ tía (đỏ son)
- Na+: vàng tươi, rất mạnh và che lấp các màu khác
- K+: tím nhạt (tím hồng), nên nhìn qua kính cobalt xanh để lọc màu vàng của sodium
- Ca2+: đỏ cam (đỏ gạch)
- Sr2+: đỏ thẫm
- Ba2+: lục vàng (vàng lục)
- Cu2+: xanh lục (lục lam)

Màu ngọn lửa sinh ra do electron bị kích thích bởi nhiệt rồi trở về mức năng lượng thấp và phát ánh sáng có bước sóng xác định. Nguyên lí này cũng được dùng trong máy quang phổ hấp thụ nguyên tử ngọn lửa (AAS) của VERA Lab để định lượng kim loại.

## Nhận biết ion Ba2+, Ca2+ và Mg2+ (kim loại nhóm IIA)

Ion barium Ba2+ được nhận biết bằng dung dịch chứa SO4 2- (dung dịch H2SO4 loãng hoặc Na2SO4): tạo kết tủa trắng BaSO4, không tan trong acid mạnh dư.

Ba2+ + SO4 2- → BaSO4↓ (trắng)

Ba2+ cũng tạo kết tủa vàng BaCrO4 với ion chromate CrO4 2-, và cho ngọn lửa màu lục vàng.

Ion calcium Ca2+ được nhận biết bằng dung dịch carbonate (Na2CO3) tạo kết tủa trắng CaCO3, kết tủa này tan trong acid và sủi bọt khí CO2. Thuốc thử đặc trưng hơn là ion oxalate C2O4 2- (từ ammonium oxalate), tạo kết tủa trắng CaC2O4. Ngọn lửa calcium màu đỏ cam.

Ca2+ + CO3 2- → CaCO3↓ (trắng)

Ion magnesium Mg2+ tạo kết tủa trắng Mg(OH)2 với dung dịch NaOH; kết tủa không tan khi thêm NaOH dư (khác Al(OH)3, Zn(OH)2), nhưng tan trong acid và trong dung dịch muối ammonium đậm đặc.

Mg2+ + 2OH- → Mg(OH)2↓ (trắng)

Lưu ý: CaSO4 ít tan nên dung dịch Ca2+ đặc cũng có thể cho vẩn đục với SO4 2-; cần thử thêm bằng oxalate hoặc màu ngọn lửa để phân biệt với Ba2+.

## Nhận biết ion Al3+, Zn2+ và Cr3+ (hydroxide lưỡng tính)

Ion aluminium Al3+, zinc Zn2+ và chromium(III) Cr3+ có chung đặc điểm: tạo hydroxide kết tủa với NaOH, rồi kết tủa tan khi NaOH dư vì các hydroxide này lưỡng tính.

- Al3+: kết tủa keo trắng Al(OH)3, tan trong NaOH dư tạo dung dịch không màu chứa [Al(OH)4]- (cách viết cũ AlO2-). Al(OH)3 không tan trong dung dịch NH3 dư.
- Zn2+: kết tủa trắng Zn(OH)2, tan trong NaOH dư tạo [Zn(OH)4]2- (cách viết cũ ZnO2 2-); khác Al(OH)3 ở chỗ Zn(OH)2 tan cả trong NH3 dư tạo phức [Zn(NH3)4]2+ không màu.
- Cr3+: kết tủa lục xám Cr(OH)3, tan trong NaOH dư tạo dung dịch xanh lục chứa [Cr(OH)4]- (cách viết cũ CrO2-).

Phương trình minh họa với Al3+:

Al3+ + 3OH- → Al(OH)3↓
Al(OH)3 + OH- → [Al(OH)4]-

Lỗi hay gặp: nhỏ NaOH quá nhanh hoặc dư ngay từ đầu thì không thấy kết tủa xuất hiện, dẫn tới kết luận sai là "không có phản ứng". Vì vậy phải nhỏ từng giọt, quan sát kết tủa, rồi mới nhỏ dư.

## Nhận biết ion Fe2+ và Fe3+ (iron(II) và iron(III))

Ion iron(II) Fe2+ (sắt(II)) và iron(III) Fe3+ (sắt(III)) đều nhận biết được bằng dung dịch NaOH nhưng cho kết tủa khác màu.

- Fe2+: kết tủa trắng hơi xanh Fe(OH)2; để ngoài không khí kết tủa chuyển dần sang nâu đỏ do bị oxygen oxi hóa thành Fe(OH)3.
  Fe2+ + 2OH- → Fe(OH)2↓
  4Fe(OH)2 + O2 + 2H2O → 4Fe(OH)3
- Fe3+: kết tủa nâu đỏ Fe(OH)3 ngay lập tức.
  Fe3+ + 3OH- → Fe(OH)3↓

Thuốc thử đặc trưng cho Fe3+ là ion thiocyanate SCN- (từ KSCN hoặc NH4SCN): dung dịch chuyển màu đỏ máu do tạo phức [Fe(SCN)]2+, rất nhạy, phát hiện được lượng vết. Fe2+ không cho màu này.

Fe2+ có tính khử nên làm mất màu tím của dung dịch potassium permanganate KMnO4 trong môi trường acid (H2SO4 loãng):

5Fe2+ + MnO4- + 8H+ → 5Fe3+ + Mn2+ + 4H2O

Dung dịch Fe2+ để lâu bị oxi hóa một phần thành Fe3+, nên dung dịch FeSO4 trong VERA Lab cần pha mới hoặc bảo quản với một ít acid và đinh sắt. Màu dung dịch: Fe2+ lục nhạt, Fe3+ vàng nâu.

## Nhận biết ion Cu2+, Ni2+, Mn2+, Ag+ và Pb2+

Nhận biết các ion kim loại nặng Cu2+, Ni2+, Mn2+, Ag+, Pb2+ thường gặp trong phòng thí nghiệm:

- Cu2+ (đồng(II)): dung dịch màu xanh lam; với NaOH tạo kết tủa xanh lam Cu(OH)2; với NH3 nhỏ dư, kết tủa tan tạo phức [Cu(NH3)4]2+ màu xanh lam thẫm (xanh đậm). Ngọn lửa màu xanh lục.
- Ni2+ (niken(II)): dung dịch màu lục; với NaOH tạo kết tủa lục nhạt Ni(OH)2; tan trong NH3 dư tạo phức màu xanh tím.
- Mn2+ (mangan(II)): dung dịch gần như không màu (hồng rất nhạt); với NaOH tạo kết tủa trắng Mn(OH)2, hóa nâu dần trong không khí.
- Ag+ (bạc): với dung dịch Cl- tạo kết tủa trắng AgCl, kết tủa tan trong NH3 dư và hóa xám đen khi để ngoài ánh sáng. Ag+ + Cl- → AgCl↓.
- Pb2+ (chì(II)): với dung dịch I- tạo kết tủa vàng tươi PbI2; với S2- hoặc H2S tạo kết tủa đen PbS; với Cl- tạo kết tủa trắng PbCl2 tan được trong nước nóng.

Cu2+ + 2OH- → Cu(OH)2↓ (xanh lam)
Cu(OH)2 + 4NH3 → [Cu(NH3)4]2+ + 2OH-

Ion Ag+ và Pb2+ độc; chất thải chứa chúng phải thu gom vào bình chứa chất thải kim loại nặng.

## Bảng tóm tắt nhận biết cation bằng dung dịch NaOH

Bảng tóm tắt hiện tượng khi nhỏ dung dịch sodium hydroxide NaOH vào dung dịch chứa các cation thường gặp, từ từ đến dư:

| Cation | Hiện tượng khi nhỏ NaOH | Khi NaOH dư |
|---|---|---|
| NH4+ | Đun nhẹ có khí mùi khai, quỳ ẩm hóa xanh | – |
| Mg2+ | Kết tủa trắng Mg(OH)2 | Không tan |
| Al3+ | Kết tủa keo trắng Al(OH)3 | Tan, dung dịch không màu |
| Zn2+ | Kết tủa trắng Zn(OH)2 | Tan, dung dịch không màu |
| Fe2+ | Kết tủa trắng xanh, hóa nâu đỏ trong không khí | Không tan |
| Fe3+ | Kết tủa nâu đỏ Fe(OH)3 | Không tan |
| Cu2+ | Kết tủa xanh lam Cu(OH)2 | Không tan (tan trong NH3 dư) |
| Cr3+ | Kết tủa lục xám Cr(OH)3 | Tan, dung dịch xanh lục |

Dựa vào bảng này, chỉ với một thuốc thử NaOH có thể phân biệt nhiều dung dịch muối mất nhãn. Riêng Al3+ và Zn2+ giống nhau với NaOH, cần thêm dung dịch NH3 dư: Zn(OH)2 tan, Al(OH)3 không tan. Ion Na+, K+ không tạo kết tủa, phải dùng màu ngọn lửa.

## Hỏi đáp nhanh: nhận biết cation

- Nhận biết ion NH4+ bằng gì? Dung dịch kiềm đun nhẹ, có khí NH3 mùi khai làm quỳ tím ẩm hóa xanh.
- Thuốc thử đặc trưng cho Fe3+? Dung dịch thiocyanate (KSCN), tạo màu đỏ máu.
- Fe(OH)2 có màu gì? Trắng hơi xanh, để ngoài không khí hóa nâu đỏ thành Fe(OH)3.
- Cu(OH)2 tan trong NH3 dư cho màu gì? Xanh lam thẫm của phức [Cu(NH3)4]2+.
- Phân biệt Al3+ và Mg2+ thế nào? Nhỏ NaOH dư: Al(OH)3 tan, Mg(OH)2 không tan.
- Nhận biết Ba2+ bằng gì? Ion SO4 2-, tạo kết tủa trắng BaSO4 không tan trong acid.
- Màu ngọn lửa của sodium và potassium? Sodium vàng tươi, potassium tím nhạt (nhìn qua kính cobalt xanh).
- Ion Ag+ nhận biết thế nào? Thêm Cl-, có kết tủa trắng AgCl tan trong NH3, hóa đen ngoài sáng.
- PbI2 có màu gì? Vàng tươi.
- Vì sao phải nhỏ NaOH từng giọt? Để thấy kết tủa trước khi nó tan trong NaOH dư (với Al3+, Zn2+, Cr3+).
