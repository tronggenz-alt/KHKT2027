---
mon: hoa
lop: 10
loai: chuyen_de
chu_de: Chuyên đề 10.1 Cơ sở hóa học (phần 2) – năng lượng hoạt hóa, phương trình Arrhenius, entropy và năng lượng tự do Gibbs
tu_khoa: [chuyên đề 10.1, năng lượng hoạt hóa, Ea, phương trình Arrhenius, hằng số tốc độ, chất xúc tác, entropy, độ mất trật tự, năng lượng tự do Gibbs, ΔG, ΔG = ΔH - TΔS, chiều hướng phản ứng, phản ứng tự xảy ra]
nguon:
  - Chương trình GDPT 2018 môn Hóa học (Thông tư 32/2018/TT-BGDĐT), Chuyên đề 10.1 Cơ sở hóa học
  - Chuyên đề học tập Hóa học 10 (Kết nối tri thức) – mục lục Bài 3 Năng lượng hoạt hóa, Bài 4 Entropy và biến thiên năng lượng tự do Gibbs
  - LibreTexts Chemistry, Reference Table T1 Standard Thermodynamic Quantities (ΔfH°, S°) – https://chem.libretexts.org/Bookshelves/Ancillary_Materials/Reference/Reference_Tables/Thermodynamics_Tables/T1%3A_Standard_Thermodynamic_Quantities
  - IUPAC Gold Book – activation energy, Arrhenius equation, Gibbs energy – https://goldbook.iupac.org/
---

# Chuyên đề Cơ sở hóa học: năng lượng hoạt hóa, entropy và năng lượng tự do Gibbs

## Năng lượng hoạt hóa của phản ứng hóa học

**Năng lượng hoạt hóa** (kí hiệu Ea, đơn vị J/mol hoặc kJ/mol) là năng lượng tối thiểu mà các hạt chất phản ứng cần có khi va chạm để phản ứng xảy ra. Có thể hình dung đó là "con dốc" hay "hàng rào năng lượng" mà hệ phải vượt qua để chuyển từ chất đầu thành sản phẩm.

**Giản đồ năng lượng:** trục tung là năng lượng, trục hoành là tiến trình phản ứng. Từ mức chất đầu, đường cong đi lên một đỉnh (trạng thái chuyển tiếp) rồi đi xuống mức sản phẩm. Chiều cao từ mức chất đầu đến đỉnh là Ea; chênh lệch giữa mức sản phẩm và mức chất đầu là ΔrH. Một phản ứng tỏa nhiệt (ΔrH < 0) vẫn có thể có Ea lớn, nên vẫn cần mồi lửa hoặc đun nóng ban đầu.

**Ảnh hưởng của Ea tới tốc độ:** ở cùng nhiệt độ, phản ứng có Ea càng nhỏ thì càng nhiều hạt đủ năng lượng vượt rào, số va chạm hiệu quả càng nhiều, tốc độ phản ứng càng lớn. Ví dụ phản ứng trung hòa acid – base hay tạo kết tủa AgCl có Ea rất nhỏ, xảy ra gần như tức thời; còn đốt cháy giấy có Ea lớn nên giấy để trong không khí không tự cháy.

**Liên hệ với nhiệt độ:** khi tăng nhiệt độ, năng lượng trung bình của các hạt tăng, tỉ lệ hạt có năng lượng lớn hơn hoặc bằng Ea tăng mạnh, nên tốc độ phản ứng tăng nhanh. Đây là lí do sâu xa của quy tắc Van't Hoff ở chương tốc độ phản ứng.

**Ví dụ đời sống:** que diêm cần cọ xát (cung cấp nhiệt vượt Ea) mới cháy; than đá ở nhiệt độ thường không cháy dù phản ứng với O2 tỏa rất nhiều nhiệt; xăng chỉ bắt cháy khi có tia lửa điện trong động cơ.

**Lỗi hay gặp:** nhầm Ea với ΔrH. Ea quyết định phản ứng nhanh hay chậm; ΔrH cho biết phản ứng tỏa hay thu nhiệt bao nhiêu. Hai đại lượng độc lập nhau.

## Phương trình Arrhenius: ảnh hưởng của năng lượng hoạt hóa và nhiệt độ

**Phương trình Arrhenius** mô tả sự phụ thuộc của hằng số tốc độ k vào nhiệt độ và năng lượng hoạt hóa:

**k = A · e^(-Ea/(R·T))**

trong đó A là hằng số đặc trưng cho phản ứng (thừa số tần số), e là cơ số logarit tự nhiên (khoảng 2,718), Ea là năng lượng hoạt hóa (J/mol), R = 8,314 J/(mol·K) là hằng số khí, T là nhiệt độ tuyệt đối (K).

**Nhận xét từ phương trình:**
- Ea càng lớn thì e^(-Ea/RT) càng nhỏ → k càng nhỏ → phản ứng càng chậm.
- T càng lớn thì Ea/RT càng nhỏ → k càng lớn → phản ứng càng nhanh.

**Dạng so sánh hai nhiệt độ:** ln(k2/k1) = (Ea/R) × (1/T1 – 1/T2).

**Ví dụ 1:** Phản ứng có Ea = 50 kJ/mol. Tăng nhiệt độ từ 300 K lên 310 K, k tăng bao nhiêu lần?
- 1/300 – 1/310 = 10/93 000 ≈ 1,075 × 10^-4 K^-1.
- Ea/R = 50 000 / 8,314 ≈ 6014 K.
- ln(k2/k1) ≈ 6014 × 1,075 × 10^-4 ≈ 0,647 → k2/k1 ≈ e^0,647 ≈ 1,9 lần (phù hợp quy tắc Van't Hoff γ khoảng 2).

**Ví dụ 2 – vai trò chất xúc tác:** Ở 298 K, chất xúc tác làm Ea giảm từ 75 kJ/mol xuống 50 kJ/mol (giả sử A không đổi).
- k(xt)/k = e^((75 000 – 50 000)/(8,314 × 298)) = e^(25 000/2477,6) ≈ e^10,09 ≈ 2,4 × 10^4.
- Tốc độ tăng khoảng 24 000 lần – cho thấy chất xúc tác hiệu quả hơn rất nhiều so với chỉ tăng nhiệt độ vài độ.

**Lưu ý tính toán:** luôn đổi °C sang K (T = t + 273), đổi kJ sang J khi dùng R = 8,314 J/(mol·K).

## Vai trò của chất xúc tác theo năng lượng hoạt hóa

**Chất xúc tác** làm tăng tốc độ phản ứng bằng cách tạo ra con đường phản ứng mới (cơ chế khác) có **năng lượng hoạt hóa thấp hơn**. Trên giản đồ năng lượng, đường có xúc tác có đỉnh thấp hơn đường không xúc tác, nhưng mức năng lượng chất đầu và sản phẩm giữ nguyên.

**Hệ quả quan trọng:**
- Chất xúc tác **không làm thay đổi ΔrH** của phản ứng và không làm thay đổi trạng thái cân bằng; nó chỉ giúp phản ứng (cả chiều thuận lẫn chiều nghịch) đạt cân bằng nhanh hơn.
- Chất xúc tác tham gia vào các giai đoạn trung gian nhưng được tái tạo, nên lượng dùng nhỏ và dùng được lâu (có thể bị "ngộ độc" bởi tạp chất).
- Chất xúc tác có tính chọn lọc: mỗi chất xúc tác thường chỉ thúc đẩy một số phản ứng nhất định.

**Ví dụ:**
- Phân hủy H2O2 thành H2O và O2: không xúc tác Ea khoảng 75 kJ/mol; với enzyme catalase Ea giảm còn khoảng vài chục kJ/mol hoặc thấp hơn, phản ứng xảy ra cực nhanh (giá trị cụ thể khác nhau tùy tài liệu).
- Tổng hợp NH3 dùng xúc tác sắt; sản xuất H2SO4 dùng V2O5 trong giai đoạn oxi hóa SO2 thành SO3.
- Bộ chuyển đổi xúc tác trên ô tô (Pt, Pd, Rh) chuyển CO, NO, hydrocarbon chưa cháy thành CO2, N2, H2O.
- Enzyme trong cơ thể giúp các phản ứng sinh hóa xảy ra nhanh ở 37 °C, điều mà không có enzyme thì cần nhiệt độ rất cao.

**Ý nghĩa kinh tế và môi trường:** xúc tác giúp phản ứng xảy ra ở nhiệt độ thấp hơn, tiết kiệm năng lượng, giảm sản phẩm phụ; là nền tảng của "hóa học xanh". Phần lớn quy trình sản xuất hóa chất hiện đại đều dùng chất xúc tác.

**Lỗi hay gặp:** cho rằng chất xúc tác làm tăng hiệu suất (lượng sản phẩm) của phản ứng thuận nghịch – thực ra chỉ rút ngắn thời gian đạt cân bằng.

## Entropy: đại lượng đặc trưng cho độ mất trật tự của hệ

**Entropy** (kí hiệu S, đơn vị J/(mol·K)) là đại lượng đặc trưng cho **độ mất trật tự** (mức độ hỗn loạn, số cách sắp xếp có thể có) của các hạt trong hệ. Hệ càng hỗn loạn thì entropy càng lớn. Entropy chuẩn của một chất kí hiệu S°298.

**Quy luật định tính:**
- **Trạng thái:** S(khí) > S(lỏng) > S(rắn) của cùng một chất. Ví dụ S° của H2O(l) khoảng 70 J/(mol·K), của H2O(g) khoảng 189 J/(mol·K). Nước đá tan, nước bay hơi đều làm tăng entropy.
- **Phân tử phức tạp hơn** (nhiều nguyên tử hơn) thường có entropy lớn hơn: S° của CO2(g) khoảng 214 > CO(g) khoảng 198 J/(mol·K).
- **Hòa tan chất rắn vào nước** thường làm tăng entropy.
- **Nhiệt độ tăng** làm entropy tăng.

**Biến thiên entropy của phản ứng:** ΔrS° = Σ S°(sản phẩm) – Σ S°(chất đầu) (nhân hệ số).

**Dự đoán dấu ΔrS nhanh theo số mol khí:** phản ứng làm tăng số mol khí thường có ΔrS > 0; làm giảm số mol khí thường có ΔrS < 0.
- CaCO3(s) → CaO(s) + CO2(g): tạo khí → ΔrS > 0.
- N2(g) + 3H2(g) → 2NH3(g): 4 mol khí → 2 mol khí → ΔrS < 0.

**Ví dụ tính:** CaCO3(s) → CaO(s) + CO2(g), với S°: CaCO3 91,7; CaO 38,1; CO2 213,8 J/(mol·K).
- ΔrS° = (38,1 + 213,8) – 91,7 = +160,2 J/K.

**Lưu ý:** khác với enthalpy tạo thành, entropy chuẩn của đơn chất **không bằng 0** (ví dụ S° của O2(g) khoảng 205, H2(g) khoảng 131 J/(mol·K)).

## Biến thiên năng lượng tự do Gibbs và chiều hướng của phản ứng

Để dự đoán một phản ứng có **tự xảy ra** (tự diễn biến) ở điều kiện đã cho hay không, người ta dựa vào **biến thiên năng lượng tự do Gibbs** ΔrG của phản ứng:

**ΔrG° = ΔrH° – T·ΔrS°**

(chú ý đổi ΔrS từ J/K sang kJ/K khi ΔrH tính bằng kJ; T tính bằng K).

**Ý nghĩa của dấu ΔrG:**
- **ΔrG < 0:** phản ứng tự xảy ra theo chiều thuận ở điều kiện đó.
- **ΔrG > 0:** phản ứng không tự xảy ra theo chiều thuận (chiều nghịch thuận lợi).
- **ΔrG = 0:** hệ ở trạng thái cân bằng.
Trị tuyệt đối ΔrG âm càng lớn, xu hướng xảy ra theo chiều thuận càng mạnh. Tuy nhiên ΔrG < 0 không cho biết phản ứng nhanh hay chậm (tốc độ do Ea quyết định).

**Phân tích theo dấu ΔH và ΔS:**
- ΔH < 0, ΔS > 0: ΔG luôn âm, phản ứng tự xảy ra ở mọi nhiệt độ.
- ΔH > 0, ΔS < 0: ΔG luôn dương, không tự xảy ra.
- ΔH < 0, ΔS < 0: thuận lợi ở nhiệt độ thấp.
- ΔH > 0, ΔS > 0: thuận lợi ở nhiệt độ cao.

**Ví dụ 1 – Nhiệt phân CaCO3:** ΔrH° = +179,2 kJ; ΔrS° = +160,2 J/K = 0,1602 kJ/K.
- Ở 298 K: ΔrG° = 179,2 – 298 × 0,1602 ≈ 179,2 – 47,7 = +131,5 kJ > 0 → không tự xảy ra ở nhiệt độ phòng (đá vôi bền).
- Nhiệt độ để ΔrG = 0: T = 179,2 / 0,1602 ≈ 1119 K (khoảng 846 °C). Trên nhiệt độ này phản ứng thuận lợi, phù hợp thực tế lò nung vôi hoạt động trên 800–900 °C (ước tính gần đúng vì ΔH, ΔS thay đổi ít theo nhiệt độ).

**Ví dụ 2 – Đốt hydrogen:** 2H2(g) + O2(g) → 2H2O(l). ΔrH° = -571,6 kJ; S°: H2 130,7; O2 205,2; H2O(l) 70,0.
- ΔrS° = 2 × 70,0 – (2 × 130,7 + 205,2) = -326,6 J/K.
- ΔrG° = -571,6 – 298 × (-0,3266) ≈ -571,6 + 97,3 = -474,3 kJ < 0 → tự xảy ra. Tuy vậy hỗn hợp H2 và O2 để yên không phản ứng vì Ea lớn; cần tia lửa.

## Hỏi đáp nhanh: Năng lượng hoạt hóa, entropy và Gibbs

**Hỏi:** Năng lượng hoạt hóa là gì?
**Đáp:** Năng lượng tối thiểu các hạt cần có khi va chạm để phản ứng xảy ra; Ea càng nhỏ phản ứng càng nhanh.

**Hỏi:** Viết phương trình Arrhenius?
**Đáp:** k = A · e^(-Ea/RT); k tăng khi T tăng hoặc Ea giảm.

**Hỏi:** Chất xúc tác tăng tốc độ phản ứng bằng cách nào?
**Đáp:** Tạo con đường phản ứng có năng lượng hoạt hóa thấp hơn; không làm thay đổi ΔrH.

**Hỏi:** Entropy là gì?
**Đáp:** Đại lượng đặc trưng cho độ mất trật tự của hệ; khí có entropy lớn hơn lỏng, lỏng lớn hơn rắn.

**Hỏi:** Công thức tính biến thiên năng lượng tự do Gibbs?
**Đáp:** ΔrG° = ΔrH° – T·ΔrS°.

**Hỏi:** ΔrG âm có nghĩa gì?
**Đáp:** Phản ứng tự xảy ra theo chiều thuận ở điều kiện đang xét.

**Hỏi:** Vì sao đá vôi không tự phân hủy ở nhiệt độ phòng?
**Đáp:** Vì ΔrG° ở 298 K khoảng +131 kJ > 0; chỉ trên khoảng 850 °C phản ứng mới thuận lợi.

**Hỏi:** ΔG âm thì phản ứng có chắc chắn xảy ra nhanh không?
**Đáp:** Không; ΔG chỉ cho biết chiều hướng, tốc độ còn phụ thuộc năng lượng hoạt hóa.
