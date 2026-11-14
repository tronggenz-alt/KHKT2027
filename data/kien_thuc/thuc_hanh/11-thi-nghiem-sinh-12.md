---
mon: sinh
lop: 0
loai: thuc_hanh
chu_de: Quy trình và kết quả mong đợi của các thí nghiệm thực hành Sinh học 12
tu_khoa: [thực hành sinh 12, tách chiết DNA, chuối, dâu tây, cồn lạnh, đột biến nhiễm sắc thể, PCR, điện di, ghép gen, DNA tái tổ hợp, pUC19, GFP, khuẩn lạc xanh trắng, ampicillin, X-gal, công nghệ gen]
nguon:
  - Chương trình GDPT 2018 môn Sinh học (Thông tư 32/2018/TT-BGDĐT) – Sinh học 12
  - Sinh học 12 Kết nối tri thức – Bài 6 Thực hành tách chiết DNA, Bài 14 Thực hành quan sát một số dạng đột biến nhiễm sắc thể, tra cứu qua https://loigiaihay.com/giai-sinh-12-bai-6-trang-32-33-34-ket-noi-tri-thuc-a157114.html và https://loigiaihay.com/giai-sinh-12-bai-14-trang-75-76-ket-noi-tri-thuc-a158137.html
  - Addgene Protocols – Bacterial transformation; Blue-white screening, https://www.addgene.org/protocols/
  - Dữ liệu dự án VERA Lab, data/sinh/bai_thi_nghiem.json, data/may/tram_ghep_gen.json, data/may/may_pcr.json, data/may/be_dien_di.json
---

# Thí nghiệm thực hành Sinh học 12: quy trình và kết quả mong đợi

## Danh sách bài thực hành Sinh học 12 và thí nghiệm tương ứng trong VERA Lab

Sinh học 12 (GDPT 2018) tập trung vào di truyền, tiến hóa và sinh thái. Các nội dung thực hành chính:

- Thực hành tách chiết DNA (Sinh học 12 Kết nối tri thức, Bài 6; Chân trời sáng tạo đặt ở Bài 2) → VERA Lab: "Tách chiết ADN thô từ mô thực vật (chuối / dâu tây)". Dữ liệu cũ của VERA Lab ghi bài này thuộc Sinh học 10; theo sách hiện hành nó thuộc Sinh học 12.
- Thực hành quan sát một số dạng đột biến nhiễm sắc thể (Kết nối tri thức, Bài 14) trên tiêu bản cố định hoặc ảnh chụp.
- Thực hành và tìm hiểu về công nghệ gen (lí thuyết công nghệ DNA tái tổ hợp, PCR): VERA Lab có hai bài mô phỏng nâng cao "Khuếch đại gen bằng PCR và phân tích sản phẩm qua điện di gel agarose" và "Ghép gen: tạo ADN tái tổ hợp pUC19 – GFP và chọn dòng khuẩn lạc xanh / trắng".
- Thực hành về di truyền học quần thể, thống kê lai giống (gieo đồng xu, mô hình hạt màu) và điều tra di truyền người (phả hệ) – hoạt động không dùng hóa chất.
- Thực hành sinh thái: điều tra quần thể, quần xã ngoài thực địa (đếm mật độ bằng ô tiêu chuẩn).

Các bài công nghệ gen trong VERA Lab chỉ dùng plasmid, enzyme thương mại và chủng E. coli phòng thí nghiệm cấp BSL-1; không có thao tác với sinh vật gây bệnh.

## Thực hành tách chiết DNA từ chuối hoặc dâu tây

Thí nghiệm tách chiết DNA thô từ mô thực vật (Sinh học 12; VERA Lab: "Tách chiết ADN thô từ mô thực vật"):

1. Nghiền: cho khoảng 50 g chuối chín hoặc dâu tây vào cối sứ, nghiền nhuyễn để phá vỡ thành tế bào.
2. Phá màng: thêm dung dịch li giải gồm nước rửa chén pha loãng (chất tẩy rửa phá màng lipid của tế bào và nhân) và muối ăn NaCl (ion Na+ trung hòa điện tích âm của DNA, giúp DNA kết tụ), khuấy nhẹ 5–10 phút, không khuấy mạnh làm đứt sợi DNA.
3. Lọc: lọc hỗn hợp qua phễu có giấy lọc hoặc vải màn vào ống nghiệm, thu dịch lọc chứa DNA.
4. Kết tủa DNA: nghiêng ống, rót từ từ theo thành ống một lượng cồn ethanol 90–96 % lạnh (để trong tủ đông trước) bằng thể tích dịch lọc, để cồn tạo lớp riêng phía trên; không lắc.
5. Để yên vài phút, quan sát mặt phân cách hai lớp.

Kết quả mong đợi: tại mặt phân cách xuất hiện lớp chất nhầy dạng sợi màu trắng đục; dùng đũa thủy tinh xoay nhẹ có thể cuộn và vớt búi sợi DNA lên. DNA không tan trong cồn nên kết tủa; cồn lạnh làm kết tủa nhanh và hạn chế enzyme phân giải DNA. Có thể thêm nước ép dứa (chứa enzyme protease bromelain) để loại protein bám DNA.

Lưu ý: sản phẩm là DNA thô lẫn RNA và protein; muốn tinh sạch phải dùng kit chiết và kiểm tra bằng máy đo nồng độ DNA (máy quang phổ OD600 / ADN đo tỉ số hấp thụ A260/A280). Ethanol dễ cháy, xa đèn cồn.

## Thực hành quan sát một số dạng đột biến nhiễm sắc thể

Thực hành quan sát đột biến nhiễm sắc thể (Sinh học 12) dùng tiêu bản cố định, ảnh chụp hiển vi hoặc ảnh bộ nhiễm sắc thể (karyotype), vì gây đột biến trong phòng học là không an toàn.

Nội dung quan sát và cách nhận biết:
- Đột biến lệch bội ở người: so sánh bộ nhiễm sắc thể bình thường (2n = 46) với bộ nhiễm sắc thể của người mắc hội chứng Down (3 nhiễm sắc thể số 21, tổng 47), hội chứng Turner (chỉ một nhiễm sắc thể X, kí hiệu 45,XO), hội chứng Klinefelter (47,XXY).
- Đột biến đa bội ở thực vật: tế bào và cơ quan to hơn (khí khổng, hạt phấn lớn hơn) so với dạng lưỡng bội; ví dụ dưa hấu tam bội không hạt.
- Đột biến cấu trúc: quan sát ảnh nhiễm sắc thể bị mất đoạn, lặp đoạn, đảo đoạn, chuyển đoạn (thường ở kì giữa, khi nhiễm sắc thể co xoắn cực đại).

Cách làm: quan sát tiêu bản ở vật kính 10x tìm tế bào ở kì giữa, chuyển 40x hoặc 100x dầu; đếm số nhiễm sắc thể; so sánh với bộ bình thường; vẽ và ghi nhận xét.

Kĩ thuật dùng kính hiển vi và vật kính dầu như ở tài liệu kính hiển vi. Học sinh cần tôn trọng người mang đột biến khi thảo luận về hội chứng di truyền ở người.

## Thí nghiệm PCR và điện di gel agarose trong VERA Lab

Bài "Khuếch đại gen bằng PCR và phân tích sản phẩm qua điện di gel agarose" (VERA Lab, liên hệ Sinh học 12 phần công nghệ gen) nhân bản gene GFP (protein phát huỳnh quang xanh) rồi kiểm tra bằng điện di.

Tóm tắt các bước (thông số chi tiết trong tài liệu máy PCR, bể điện di và Gel Doc):
1. Pha phản ứng PCR trên hộp đá trong ống PCR 0,2 mL: PCR master mix, cặp mồi đặc hiệu gene GFP, DNA khuôn, nước không nuclease; một ống đối chứng âm thay DNA khuôn bằng nước. Dùng micropipet P10, P200 và thay đầu côn mỗi lần hút.
2. Ly tâm mini nhanh, đặt ống vào máy PCR, chạy khoảng 30 chu kì (biến tính – gắn mồi – kéo dài).
3. Đúc gel agarose có thuốc nhuộm GelGreen; nạp thang DNA 100 bp và các mẫu PCR đã trộn đệm nạp mẫu.
4. Chạy điện di, chụp ảnh gel bằng buồng chụp gel.

Kết quả mong đợi: giếng mẫu có một vạch DNA sáng rõ khoảng 740 bp (gene GFP 720 bp cộng hai đuôi mồi mang vị trí cắt EcoRI và BamHI), nằm giữa vạch 700 và 800 bp của thang chuẩn; giếng đối chứng âm không có vạch. Nếu đối chứng âm có vạch là bị nhiễm DNA lạ; nếu không có vạch nào là PCR thất bại (thiếu thành phần, nhiệt độ gắn mồi sai).

Ứng dụng của PCR: xét nghiệm bệnh, giám định pháp y, xác định quan hệ huyết thống, kiểm tra sinh vật biến đổi gene.

## Thí nghiệm ghép gen tạo DNA tái tổ hợp pUC19 – GFP và chọn dòng xanh trắng

Bài "Ghép gen: tạo ADN tái tổ hợp pUC19 – GFP và chọn dòng khuẩn lạc xanh / trắng" tại Trạm ghép gen của VERA Lab minh họa quy trình công nghệ DNA tái tổ hợp ở Sinh học 12 (các bước chi tiết trong tài liệu trạm ghép gen):

1. Cắt: cắt plasmid pUC19 (thể truyền) và đoạn gene GFP bằng cùng hai enzyme cắt giới hạn EcoRI và BamHI, tạo đầu dính bổ sung.
2. Nối: trộn plasmid và gene GFP đã cắt với DNA ligase T4 để nối thành DNA tái tổ hợp.
3. Biến nạp: đưa hỗn hợp nối vào tế bào E. coli DH5α khả biến bằng sốc nhiệt (giữ đá, 42 °C khoảng 45 giây, lại giữ đá), nuôi phục hồi trong canh LB.
4. Chọn dòng: trải lên đĩa LB có ampicillin và X-gal, ủ 37 °C qua đêm.

Kết quả mong đợi: chỉ vi khuẩn nhận plasmid (mang gene kháng ampicillin) mới mọc. Khuẩn lạc xanh mang plasmid không có đoạn chèn (gene lacZα còn nguyên tạo enzyme phân giải X-gal thành sản phẩm màu xanh); khuẩn lạc trắng mang plasmid tái tổ hợp (gene GFP chèn vào giữa làm hỏng lacZα). Đĩa có nhiều khuẩn lạc trắng, ít khuẩn lạc xanh. Cắt plasmid tách từ khuẩn lạc trắng bằng EcoRI + BamHI rồi điện di thấy hai băng khoảng 2 665 bp (khung pUC19) và 726 bp (gene GFP).

Đối chứng: bỏ ligase thì không có khuẩn lạc; đĩa thiếu ampicillin thì vi khuẩn mọc kín đĩa. An toàn: chủng E. coli DH5α cấp BSL-1; mọi đĩa, ống phải hấp hủy sau thí nghiệm.

## Hỏi đáp nhanh: thí nghiệm thực hành Sinh học 12

- Tách chiết DNA dùng nước rửa chén để làm gì? Phá màng lipid của tế bào và màng nhân, giải phóng DNA.
- Vì sao thêm muối NaCl khi tách chiết DNA? Ion Na+ trung hòa điện tích âm của DNA, giúp DNA kết tụ.
- Vì sao dùng cồn lạnh? DNA không tan trong cồn nên kết tủa thành sợi trắng; lạnh giúp kết tủa nhanh và bảo vệ DNA.
- DNA tách từ chuối trông thế nào? Sợi nhầy màu trắng đục ở mặt phân cách giữa cồn và dịch lọc.
- Bài thực hành tách chiết DNA thuộc lớp mấy theo sách hiện hành? Lớp 12 (Kết nối tri thức Bài 6).
- Hội chứng Down có bao nhiêu nhiễm sắc thể? 47, thừa một nhiễm sắc thể số 21.
- Sản phẩm PCR gene GFP trong VERA Lab dài khoảng bao nhiêu? Khoảng 740 bp.
- Khuẩn lạc trắng trên đĩa X-gal nghĩa là gì? Vi khuẩn mang plasmid tái tổ hợp có gene chèn.
- Enzyme nào nối gene vào plasmid? DNA ligase (ligase T4).
- Vì sao đĩa có ampicillin? Để chỉ vi khuẩn nhận plasmid mang gene kháng ampicillin mới mọc.
