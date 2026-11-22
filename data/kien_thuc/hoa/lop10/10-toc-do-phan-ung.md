---
mon: hoa
lop: 10
loai: ly_thuyet
chu_de: Tốc độ phản ứng hóa học
tu_khoa: [tốc độ phản ứng, tốc độ trung bình, định luật tác dụng khối lượng, hằng số tốc độ, phương trình tốc độ, nồng độ, nhiệt độ, áp suất, diện tích bề mặt, chất xúc tác, hệ số nhiệt độ Van't Hoff, gamma, va chạm hiệu quả]
nguon:
  - Chương trình GDPT 2018 môn Hóa học (Thông tư 32/2018/TT-BGDĐT), Lớp 10 – Tốc độ phản ứng hóa học
  - IUPAC Gold Book – rate of reaction, rate constant – https://goldbook.iupac.org/
  - SGK Hóa học 10 (Kết nối tri thức, Chân trời sáng tạo, Cánh diều) – đối chiếu mục lục và phạm vi kiến thức
---

# Tốc độ phản ứng hóa học

## Khái niệm tốc độ phản ứng và cách tính tốc độ trung bình

**Tốc độ phản ứng hóa học** là đại lượng đặc trưng cho sự biến thiên nồng độ của chất phản ứng hoặc sản phẩm trong một đơn vị thời gian. Có phản ứng xảy ra gần như tức thời (nổ, kết tủa AgCl), có phản ứng rất chậm (gỉ sắt, lên men, hình thành dầu mỏ).

Đơn vị tốc độ thường dùng: mol/(L·s), viết tắt M/s; cũng có thể dùng mol/(L·phút) hoặc mol/(L·giờ).

**Tốc độ trung bình** của phản ứng tổng quát aA + bB → cC + dD trong khoảng thời gian Δt = t2 – t1:

v = -(1/a)·ΔC(A)/Δt = -(1/b)·ΔC(B)/Δt = (1/c)·ΔC(C)/Δt = (1/d)·ΔC(D)/Δt

trong đó ΔC = C(t2) – C(t1). Dấu trừ đứng trước số hạng của chất đầu vì nồng độ chất đầu giảm (ΔC âm), giúp tốc độ luôn dương. Chia cho hệ số để tốc độ tính theo chất nào cũng cho cùng một giá trị.

**Ví dụ có lời giải:** Phân hủy hydrogen peroxide: 2H2O2 → 2H2O + O2. Nồng độ H2O2 giảm từ 0,50 M xuống 0,30 M sau 40 giây.
- ΔC(H2O2) = 0,30 – 0,50 = -0,20 M.
- v = -(1/2) × (-0,20) / 40 = 2,5 × 10^-3 M/s.
- Tốc độ tạo O2 cũng là 2,5 × 10^-3 M/s; tốc độ tiêu hao H2O2 là 5,0 × 10^-3 M/s (gấp đôi do hệ số 2).

**Theo dõi tốc độ trong thực nghiệm:** đo thể tích khí thoát ra theo thời gian (dùng xi lanh hoặc ống đong úp ngược), đo khối lượng giảm trên cân, đo sự thay đổi màu hoặc độ đục. Tốc độ thường giảm dần theo thời gian vì nồng độ chất phản ứng giảm.

**Lỗi hay gặp:** quên chia cho hệ số tỉ lượng; quên đổi phút ra giây; lấy nồng độ đầu trừ nồng độ cuối nhưng vẫn thêm dấu trừ làm tốc độ âm.

## Định luật tác dụng khối lượng và hằng số tốc độ phản ứng

**Định luật tác dụng khối lượng** (C. M. Guldberg và P. Waage, 1864): ở nhiệt độ không đổi, tốc độ của một **phản ứng đơn giản** tỉ lệ thuận với tích nồng độ các chất phản ứng với số mũ bằng hệ số tỉ lượng tương ứng.

Với phản ứng đơn giản aA + bB → sản phẩm:  **v = k · C(A)^a · C(B)^b**

trong đó k là **hằng số tốc độ phản ứng**.

**Ý nghĩa của hằng số tốc độ k:** k chính là tốc độ phản ứng khi nồng độ mỗi chất phản ứng bằng 1 mol/L. k chỉ phụ thuộc bản chất phản ứng, nhiệt độ và chất xúc tác, không phụ thuộc nồng độ. k càng lớn, phản ứng xảy ra càng nhanh.

**Lưu ý quan trọng:** định luật chỉ đúng cho phản ứng đơn giản (xảy ra qua một giai đoạn). Với phản ứng phức tạp, số mũ trong phương trình tốc độ phải xác định bằng thực nghiệm, không được tùy tiện lấy theo hệ số phương trình.

**Ví dụ 1:** Phản ứng 2NO(g) + O2(g) → 2NO2(g) có phương trình tốc độ v = k · C(NO)^2 · C(O2). Nếu tăng nồng độ NO lên 2 lần, giữ nguyên O2: v mới = k · (2C(NO))^2 · C(O2) = 4v → tốc độ tăng 4 lần. Nếu tăng nồng độ O2 lên 3 lần: tốc độ tăng 3 lần.

**Ví dụ 2 – Tăng áp suất:** với phản ứng trên, nếu nén hỗn hợp để áp suất tăng 2 lần (thể tích giảm một nửa) thì nồng độ mỗi khí tăng 2 lần: v mới = k · (2)^2 · 2 = 8 lần v cũ.

**Ví dụ 3 – Tính k:** Phản ứng đơn giản H2(g) + I2(g) → 2HI(g) có v = k · C(H2) · C(I2). Khi C(H2) = 0,10 M, C(I2) = 0,20 M, tốc độ đo được 1,0 × 10^-5 M/s. Suy ra k = 1,0 × 10^-5 / (0,10 × 0,20) = 5,0 × 10^-4 L/(mol·s).

## Ảnh hưởng của nồng độ và áp suất đến tốc độ phản ứng

Các yếu tố ảnh hưởng đến tốc độ phản ứng được giải thích bằng **thuyết va chạm**: phản ứng chỉ xảy ra khi các hạt chất phản ứng va chạm với nhau đủ năng lượng và đúng hướng (va chạm hiệu quả). Yếu tố nào làm tăng số va chạm hiệu quả trong một đơn vị thời gian sẽ làm tăng tốc độ phản ứng.

**Ảnh hưởng của nồng độ:** tăng nồng độ chất phản ứng thì tốc độ phản ứng tăng. Giải thích: nồng độ cao, số hạt trong một đơn vị thể tích nhiều hơn, tần số va chạm tăng, số va chạm hiệu quả tăng.

*Thí nghiệm:* cho cùng một lượng mảnh kẽm (hoặc đá vôi) vào hai ống nghiệm chứa dung dịch HCl 1 M và HCl 0,1 M. Ống HCl 1 M sủi bọt khí mạnh hơn: Zn + 2HCl → ZnCl2 + H2. Một thí nghiệm khác dùng sodium thiosulfate: Na2S2O3 + H2SO4 → Na2SO4 + S + SO2 + H2O; đo thời gian dung dịch vẩn đục (do S) đủ để che khuất dấu chữ X đặt dưới cốc. Nồng độ Na2S2O3 càng lớn, thời gian càng ngắn.

**Ảnh hưởng của áp suất (chỉ với phản ứng có chất khí):** tăng áp suất thì nồng độ chất khí tăng, tốc độ phản ứng tăng. Ví dụ trong công nghiệp tổng hợp NH3 (N2 + 3H2 ⇌ 2NH3), người ta dùng áp suất cao vài trăm atm. Với phản ứng chỉ có chất rắn và chất lỏng, áp suất hầu như không ảnh hưởng.

**Liên hệ thực tế:** than cháy mạnh hơn trong oxygen nguyên chất so với trong không khí (nồng độ O2 cao hơn khoảng 5 lần); bệnh nhân được thở oxygen nồng độ cao để tăng tốc độ trao đổi khí; để thực phẩm trong túi hút chân không giảm nồng độ O2 nên chậm bị oxi hóa.

**An toàn:** không để dầu mỡ tiếp xúc với van bình oxygen nén vì nồng độ O2 cao làm tăng tốc độ cháy, dễ gây cháy nổ.

## Ảnh hưởng của nhiệt độ và hệ số nhiệt độ Van't Hoff

**Ảnh hưởng của nhiệt độ:** tăng nhiệt độ thì tốc độ phản ứng tăng (với hầu hết các phản ứng). Giải thích: nhiệt độ cao làm các hạt chuyển động nhanh hơn, va chạm nhiều hơn, và quan trọng hơn, tỉ lệ hạt có đủ năng lượng để va chạm hiệu quả tăng mạnh.

**Quy tắc Van't Hoff:** với nhiều phản ứng, khi nhiệt độ tăng thêm 10 °C thì tốc độ phản ứng tăng từ 2 đến 4 lần. Số lần tăng này gọi là **hệ số nhiệt độ Van't Hoff**, kí hiệu γ (gamma):

v2 / v1 = γ^((T2 – T1)/10)

trong đó v1, v2 là tốc độ ở nhiệt độ T1, T2.

**Ý nghĩa của γ:** cho biết tốc độ phản ứng tăng bao nhiêu lần khi nhiệt độ tăng 10 độ; γ càng lớn, tốc độ phản ứng càng nhạy cảm với nhiệt độ.

**Ví dụ 1:** Phản ứng có γ = 2. Tăng nhiệt độ từ 20 °C lên 50 °C thì tốc độ tăng 2^((50 – 20)/10) = 2^3 = 8 lần.

**Ví dụ 2 (bài toán thời gian):** Ở 30 °C phản ứng hoàn thành trong 40 phút, γ = 2. Ở 60 °C cần bao lâu? Tốc độ tăng 2^3 = 8 lần nên thời gian giảm 8 lần: 40 / 8 = 5 phút.

**Ví dụ 3 (tìm γ):** Tăng nhiệt độ từ 25 °C lên 45 °C, tốc độ tăng 9 lần → γ^2 = 9 → γ = 3.

**Liên hệ thực tế:** nấu bằng nồi áp suất (nước sôi ở khoảng 115–120 °C) chín nhanh hơn nồi thường; bảo quản thực phẩm trong tủ lạnh làm chậm phản ứng gây hư hỏng; ủ men, muối dưa nhanh hơn khi trời ấm; bệnh nhân sốt cao có tốc độ trao đổi chất tăng.

**Lưu ý:** quy tắc Van't Hoff chỉ là gần đúng trong khoảng nhiệt độ không quá rộng; phản ứng có enzyme thì quá nóng enzyme bị biến tính, tốc độ giảm.

## Ảnh hưởng của diện tích bề mặt tiếp xúc đến tốc độ phản ứng

**Ảnh hưởng của diện tích bề mặt:** với phản ứng có chất rắn tham gia, tăng diện tích bề mặt tiếp xúc (chia nhỏ chất rắn) thì tốc độ phản ứng tăng. Giải thích: phản ứng chỉ xảy ra ở bề mặt chất rắn; chia nhỏ chất rắn làm tăng tổng diện tích bề mặt, nhiều hạt hơn tiếp xúc với chất phản ứng khác, số va chạm hiệu quả tăng.

**Ví dụ tính diện tích:** một khối lập phương cạnh 1 cm có diện tích bề mặt 6 cm2. Cắt thành 1000 khối nhỏ cạnh 1 mm thì tổng diện tích là 1000 × 6 × 0,01 = 60 cm2, tăng 10 lần.

**Thí nghiệm:** cho cùng khối lượng đá vôi dạng viên và dạng bột vào hai cốc chứa cùng thể tích dung dịch HCl cùng nồng độ:
CaCO3 + 2HCl → CaCl2 + CO2 + H2O.
Cốc chứa bột đá vôi sủi bọt khí mãnh liệt hơn và kết thúc sớm hơn; tổng thể tích khí cuối cùng như nhau nếu acid dư.

**Liên hệ thực tế:**
- Nhai kĩ thức ăn giúp tiêu hóa nhanh hơn (tăng diện tích tiếp xúc với enzyme).
- Củi chẻ nhỏ cháy nhanh hơn khúc gỗ to; than được nghiền thành bột trong nhà máy nhiệt điện để cháy nhanh và hoàn toàn.
- Thuốc dạng bột, viên sủi tan và tác dụng nhanh hơn viên nén.
- Trong sản xuất, quặng được nghiền nhỏ trước khi nung hoặc hòa tách.

**Cảnh báo an toàn – nổ bụi:** bụi rất mịn của chất cháy được (bột mì, bột đường, bụi than, bột nhôm) lơ lửng trong không khí có diện tích bề mặt cực lớn, nếu gặp tia lửa có thể cháy nhanh đến mức gây nổ. Các nhà máy xay xát, mỏ than, xưởng gỗ phải kiểm soát bụi, thông gió, cấm lửa.

## Ảnh hưởng của chất xúc tác đến tốc độ phản ứng

**Chất xúc tác** là chất làm tăng tốc độ phản ứng nhưng **không bị tiêu hao** (vẫn còn nguyên về khối lượng và bản chất hóa học) sau khi phản ứng kết thúc.

**Giải thích:** chất xúc tác tạo ra một con đường phản ứng mới có năng lượng hoạt hóa thấp hơn, nên ở cùng nhiệt độ có nhiều va chạm hiệu quả hơn. Chất xúc tác có tham gia vào các giai đoạn trung gian nhưng được tái tạo lại ở cuối. Chất xúc tác không làm thay đổi ΔrH của phản ứng.

**Thí nghiệm:** nhỏ dung dịch hydrogen peroxide (nước oxy già) vào hai ống nghiệm. Ống thứ nhất để nguyên, chỉ có vài bọt khí rất chậm. Ống thứ hai thêm một ít bột manganese dioxide MnO2 màu đen: sủi bọt mạnh, khí O2 làm que đóm còn tàn đỏ bùng cháy.
2H2O2 → 2H2O + O2 (xúc tác MnO2).
Sau phản ứng lọc lại được MnO2 với khối lượng gần như không đổi.

**Ví dụ chất xúc tác trong công nghiệp và đời sống:**
- Sắt (Fe) trong tổng hợp ammonia N2 + 3H2 ⇌ 2NH3.
- Vanadium(V) oxide V2O5 trong oxi hóa SO2 thành SO3 khi sản xuất sulfuric acid: 2SO2 + O2 ⇌ 2SO3.
- Platinum, palladium, rhodium trong bộ chuyển đổi xúc tác của ô tô, chuyển CO, NO thành CO2, N2 ít độc hơn.
- **Enzyme** là chất xúc tác sinh học: amylase trong nước bọt thủy phân tinh bột; catalase trong gan, khoai tây phân hủy H2O2 (thả lát khoai tây sống vào H2O2 thấy sủi bọt).
- Men (nấm men) trong làm bánh mì, nấu rượu, sữa chua.

**Chất ức chế** là chất làm giảm tốc độ phản ứng, ví dụ chất bảo quản thực phẩm, chất chống oxi hóa trong dầu ăn, chất ức chế ăn mòn kim loại.

## Vận dụng kiến thức tốc độ phản ứng trong đời sống và sản xuất

Kiến thức về tốc độ phản ứng giúp giải thích và điều khiển nhiều quá trình trong đời sống và sản xuất. Nguyên tắc chung: muốn phản ứng có lợi xảy ra nhanh thì tăng nồng độ, nhiệt độ, áp suất (với khí), diện tích bề mặt, dùng xúc tác; muốn phản ứng có hại xảy ra chậm thì làm ngược lại.

**Làm phản ứng nhanh hơn:**
- Nhóm bếp than: quạt gió (tăng O2), chẻ nhỏ củi, dùng than tổ ong có nhiều lỗ (tăng diện tích tiếp xúc với không khí).
- Nấu ăn: thái nhỏ thực phẩm, dùng nồi áp suất.
- Công nghiệp: nghiền quặng, dùng nhiệt độ và áp suất thích hợp, dùng xúc tác để tiết kiệm năng lượng và thời gian.
- Hòa tan đường nhanh hơn: dùng đường kính mịn, nước nóng, khuấy đều.

**Làm phản ứng chậm lại:**
- Bảo quản thực phẩm: để tủ lạnh (giảm nhiệt độ), hút chân không hoặc bơm khí N2 vào bao bì (giảm O2), thêm chất bảo quản cho phép.
- Chống gỉ kim loại: sơn phủ, bôi dầu để cách li O2 và hơi nước.
- Dập lửa: phủ chăn ướt, cát hoặc dùng bình CO2 để giảm nồng độ O2 và hạ nhiệt độ.
- Bảo quản thuốc: để nơi khô, mát, tránh ánh sáng.

**Ví dụ phân tích:** Vì sao viên nén đá vôi trong lò nung cần đập nhỏ vừa phải? Đập nhỏ tăng diện tích tiếp xúc giúp nhiệt truyền và CO2 thoát ra nhanh; nhưng quá nhỏ thì lớp đá vôi bít kín, cản không khí lưu thông trong lò. Người kĩ sư phải cân bằng các yếu tố.

**Lỗi học sinh hay mắc về tốc độ phản ứng:**
- Cho rằng chất xúc tác không tham gia phản ứng (thực ra có tham gia nhưng được tái tạo).
- Cho rằng áp suất ảnh hưởng đến mọi phản ứng (chỉ khi có chất khí).
- Cho rằng tăng diện tích bề mặt làm tăng lượng sản phẩm (chỉ làm nhanh hơn, lượng sản phẩm phụ thuộc lượng chất phản ứng).
- Áp dụng định luật tác dụng khối lượng cho phản ứng phức tạp.

## Hỏi đáp nhanh: Tốc độ phản ứng hóa học

**Hỏi:** Tốc độ phản ứng là gì?
**Đáp:** Đại lượng đặc trưng cho sự biến thiên nồng độ chất phản ứng hoặc sản phẩm trong một đơn vị thời gian.

**Hỏi:** Những yếu tố nào ảnh hưởng đến tốc độ phản ứng?
**Đáp:** Nồng độ, nhiệt độ, áp suất (với chất khí), diện tích bề mặt tiếp xúc và chất xúc tác.

**Hỏi:** Viết biểu thức định luật tác dụng khối lượng cho aA + bB → sản phẩm?
**Đáp:** v = k · C(A)^a · C(B)^b, chỉ đúng cho phản ứng đơn giản.

**Hỏi:** Hằng số tốc độ k có ý nghĩa gì?
**Đáp:** Là tốc độ phản ứng khi nồng độ các chất phản ứng đều bằng 1 M; phụ thuộc bản chất phản ứng, nhiệt độ và xúc tác.

**Hỏi:** Hệ số Van't Hoff γ = 3, tăng nhiệt độ thêm 20 °C thì tốc độ tăng mấy lần?
**Đáp:** 3^2 = 9 lần.

**Hỏi:** Vì sao bột đá vôi tan trong HCl nhanh hơn viên đá vôi?
**Đáp:** Bột có diện tích bề mặt tiếp xúc lớn hơn nên số va chạm hiệu quả nhiều hơn.

**Hỏi:** Chất xúc tác là gì?
**Đáp:** Chất làm tăng tốc độ phản ứng mà không bị tiêu hao sau phản ứng, ví dụ MnO2 khi phân hủy H2O2.

**Hỏi:** Vì sao bảo quản thực phẩm trong tủ lạnh?
**Đáp:** Nhiệt độ thấp làm giảm tốc độ các phản ứng và hoạt động của vi sinh vật gây hỏng.
