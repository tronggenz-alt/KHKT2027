---
mon: sinh
lop: 0
loai: thuc_hanh
chu_de: Kĩ thuật dùng micropipette, máy li tâm và điện di DNA trên gel agarose
tu_khoa: [micropipette, micropipet, đầu côn, nấc thứ nhất, nấc thứ hai, máy li tâm, máy ly tâm, cân bằng ống, điện di, gel agarose, thang DNA, đệm TAE, giếng gel, cực dương, Gel Doc, PCR]
nguon:
  - Addgene Protocols – Agarose Gel Electrophoresis, https://www.addgene.org/protocols/gel-electrophoresis/
  - Gilson – Guide to Pipetting (hướng dẫn kĩ thuật hút xả micropipette)
  - OpenStax Biology 2e, mục 17.1 Biotechnology, https://openstax.org/books/biology-2e/pages/17-1-biotechnology
  - Dữ liệu dự án VERA Lab, data/sinh/dung_cu.json, data/may/be_dien_di.json, data/may/gel_doc.json, data/may/may_pcr.json
---

# Micropipette, máy li tâm và điện di DNA

## Micropipette: các loại và cách chọn đúng dải thể tích

Micropipette (micropipet) là dụng cụ hút chính xác thể tích rất nhỏ, đơn vị microlit (1 µL = 0,001 mL). Phòng công nghệ gen của VERA Lab có ba loại:

- Micropipet P10: 0,5–10 µL, dùng đầu côn trắng (loại nhỏ) – nạp mẫu DNA, enzyme cắt giới hạn.
- Micropipet P200: 20–200 µL, dùng đầu côn vàng – pha phản ứng PCR, trải đĩa 100 µL.
- Micropipet P1000: 100–1 000 µL, dùng đầu côn xanh – pha loãng mẫu vi sinh, đo OD600.

Nguyên tắc chọn: dùng micropipette có dải nhỏ nhất chứa thể tích cần hút, vì độ chính xác tốt nhất ở khoảng giữa và gần đầu trên của dải; ví dụ hút 8 µL dùng P10 chứ không dùng P200. Không vặn núm chỉnh vượt quá giới hạn trên hoặc dưới của dải, sẽ làm hỏng cơ cấu bên trong.

Đặt thể tích: xoay núm điều chỉnh đến khi cửa sổ hiển thị đúng số (đọc theo hướng dẫn trên thân pipette, ví dụ P200 hiển thị "100" là 100 µL). Mỗi lần hút mẫu khác nhau phải thay đầu côn mới để tránh nhiễm chéo; đầu côn đã dùng bỏ vào thùng rác đầu côn. Hộp đầu côn được hấp tiệt trùng trước khi dùng cho nuôi cấy và PCR.

## Kĩ thuật hút và xả micropipette với hai nấc

Micropipette có pít-tông với hai nấc ấn: nấc thứ nhất ứng với đúng thể tích cài đặt; nấc thứ hai (ấn sâu hơn) để đẩy hết giọt còn sót.

Kĩ thuật hút xả chuẩn (forward pipetting):
1. Lắp đầu côn chắc chắn bằng cách ấn nhẹ thân pipette vào đầu côn trong hộp.
2. Ấn pít-tông xuống nấc thứ nhất và giữ.
3. Nhúng đầu côn thẳng đứng vào dung dịch khoảng 2–3 mm (P10, P200) hoặc 3–6 mm (P1000).
4. Thả pít-tông từ từ, đều tay để dung dịch được hút lên; chờ khoảng 1 giây rồi rút đầu côn ra.
5. Đặt đầu côn chạm thành trong ống nhận, hơi nghiêng; ấn pít-tông chậm xuống nấc thứ nhất, chờ 1 giây, rồi ấn tiếp xuống nấc thứ hai để đẩy hết.
6. Giữ pít-tông ở nấc hai, rút đầu côn ra dọc theo thành ống, rồi mới thả pít-tông.
7. Bấm nút đẩy để thải đầu côn vào thùng rác.

Lỗi thường gặp: ấn quá nấc thứ hai trước khi hút (hút thừa thể tích); thả pít-tông đột ngột (tạo bọt khí, dung dịch bắn lên trong thân pipette); nhúng đầu côn quá sâu (dung dịch bám ngoài đầu côn); đặt pipette nằm ngang khi đầu côn còn chứa dung dịch (chất lỏng chảy vào thân, làm nhiễm và hỏng pipette). Khi nạp mẫu vào giếng gel thì chỉ ấn đến nấc thứ nhất để không tạo bọt khí đẩy mẫu ra khỏi giếng.

## Máy li tâm: nguyên lí, cân bằng ống và an toàn

Máy li tâm (máy ly tâm) quay mẫu với tốc độ cao, tạo lực li tâm làm các thành phần nặng hơn lắng xuống đáy ống nhanh hơn nhiều so với để lắng tự nhiên. Lực li tâm thường ghi theo đơn vị × g (số lần gia tốc trọng trường), phụ thuộc tốc độ quay (vòng/phút, rpm) và bán kính rôto.

VERA Lab có Máy ly tâm để bàn (cho ống li tâm 15 mL, 50 mL: thu tế bào nấm men, vi khuẩn, tách cặn khi tách chiết DNA) và Máy ly tâm mini (cho ống eppendorf 1,5 mL và ống PCR 0,2 mL: gom dung dịch xuống đáy ống trước khi chạy PCR, trộn nhanh).

Quy tắc an toàn khi dùng máy li tâm:
1. Cân bằng: các ống phải đặt đối xứng qua tâm rôto và có khối lượng (thể tích) bằng nhau. Nếu chỉ có một mẫu, đặt một ống chứa nước cùng thể tích ở vị trí đối diện.
2. Đậy kín nắp ống; không đổ đầy quá vạch giới hạn của ống.
3. Đóng nắp máy trước khi bật; không mở nắp khi rôto đang quay; chờ rôto dừng hẳn.
4. Nếu máy rung mạnh hoặc kêu bất thường: tắt ngay, kiểm tra cân bằng.
5. Ống bị vỡ hoặc rò mẫu vi sinh trong máy: chờ vài phút cho giọt bắn lắng xuống rồi mới mở, lau khử trùng rôto.

Sau li tâm, phần chất rắn lắng ở đáy là cặn (pellet), phần chất lỏng phía trên là dịch nổi (supernatant).

## Nguyên lí điện di DNA trên gel agarose

Điện di trên gel agarose là kĩ thuật tách các đoạn DNA theo kích thước. Phân tử DNA mang điện tích âm do các nhóm phosphate, nên trong điện trường nó di chuyển từ cực âm (màu đen) về cực dương (màu đỏ). Gel agarose có cấu trúc dạng lưới lỗ: đoạn DNA ngắn luồn qua dễ nên đi xa, đoạn dài đi chậm nên ở gần giếng.

Các thành phần:
- Gel agarose nồng độ khoảng 0,8–2 % (gel 1 % tách tốt đoạn khoảng 0,5–10 kb); gel đặc hơn tách đoạn ngắn tốt hơn.
- Đệm điện di TAE (Tris – acetate – EDTA) dẫn điện và giữ pH ổn định; ngập gel 3–5 mm.
- Thang DNA chuẩn (DNA ladder, ví dụ thang 100 bp) gồm các đoạn đã biết kích thước, chạy song song để so sánh.
- Đệm nạp mẫu (loading dye 6x) chứa glycerol làm mẫu nặng chìm xuống giếng và chất màu theo dõi quá trình chạy.
- Thuốc nhuộm DNA phát huỳnh quang (GelGreen trong VERA Lab) giúp thấy vạch DNA khi chiếu ánh sáng xanh hoặc tia UV.

Kích thước đoạn DNA của mẫu được xác định bằng cách so vị trí vạch của mẫu với các vạch của thang chuẩn. Ví dụ trong bài PCR của VERA Lab, sản phẩm khuếch đại gene GFP cho một vạch khoảng 740 bp, nằm giữa vạch 700 và 800 bp của thang.

## Quy trình điện di gel agarose tóm tắt và an toàn

Tóm tắt quy trình điện di trên Hệ thống điện di gel agarose của VERA Lab (các bước chi tiết và thông số trong tài liệu bể điện di và máy chụp gel):

1. Đúc gel: cân agarose (ví dụ 1 g cho 100 mL đệm TAE 1x), đun trong lò vi sóng nhỏ đến tan trong, để nguội khoảng 55–60 °C, thêm thuốc nhuộm, đổ vào khay đúc gel đã cài lược; chờ khoảng 20–30 phút cho gel đông, rút lược.
2. Đặt gel vào bể sao cho giếng ở phía cực âm (đen); đổ đệm TAE ngập gel.
3. Trộn mẫu DNA với đệm nạp mẫu; dùng micropipet P10 nạp từ từ vào từng giếng (đầu côn đặt ngay trên miệng giếng, không chọc thủng đáy giếng); nạp thang DNA vào một giếng riêng.
4. Đậy nắp an toàn, nối dây đúng cực (đen với đen, đỏ với đỏ), bật nguồn khoảng 100–120 V trong 30–45 phút; thấy bọt khí li ti ở hai điện cực là có dòng điện.
5. Dừng khi vạch màu chạy được khoảng 2/3 gel; tắt nguồn rồi mới mở nắp.
6. Chuyển gel vào buồng chụp gel (Gel Doc), chụp ảnh, phân tích vạch.

An toàn: bể điện di dùng điện áp cao, chỉ mở nắp khi đã tắt nguồn; không chạm vào đệm khi đang chạy. Agarose nóng gây bỏng. Đeo găng tay khi cầm gel có thuốc nhuộm DNA. Ánh sáng UV gây tổn thương mắt và da: dùng tấm chắn, kính chống UV; buồng Gel Doc dùng đèn xanh hoặc UV trong buồng kín.

Lỗi hay gặp: đặt giếng ở phía cực dương làm DNA chạy ra khỏi gel; quên thuốc nhuộm; nạp mẫu tạo bọt khí làm mẫu tràn khỏi giếng.

## PCR: khuếch đại DNA bằng máy luân nhiệt

PCR (phản ứng chuỗi polymerase) là kĩ thuật nhân bản một đoạn DNA xác định thành hàng triệu bản sao trong ống nghiệm, thực hiện trong máy luân nhiệt PCR. Mỗi chu kì gồm ba bước:

- Biến tính (khoảng 94–95 °C): hai mạch DNA khuôn tách rời.
- Gắn mồi (khoảng 50–65 °C, tùy cặp mồi): hai đoạn mồi ngắn bắt cặp bổ sung với hai đầu đoạn cần nhân.
- Kéo dài (72 °C): enzyme Taq polymerase chịu nhiệt tổng hợp mạch mới nối tiếp mồi.

Sau n chu kì, lí thuyết số bản sao tăng 2^n lần; 30 chu kì cho khoảng 10^9 bản sao. Thành phần phản ứng: DNA khuôn, cặp mồi, hỗn hợp PCR master mix (chứa Taq polymerase, 4 loại dNTP, Mg2+, đệm), nước không nuclease.

Kĩ thuật trong VERA Lab (bài "Khuếch đại gen bằng PCR và phân tích sản phẩm qua điện di gel agarose"): pha phản ứng trên hộp đá bằng micropipet P10 và P200 trong ống PCR 0,2 mL, ly tâm mini nhanh để gom dung dịch, đặt ống vào máy PCR, chạy chương trình nhiệt; sản phẩm được kiểm tra bằng điện di. Luôn có ống đối chứng âm (thay DNA khuôn bằng nước) để phát hiện nhiễm bẩn; đối chứng âm đúng thì không có vạch. Thao tác pha PCR trong tủ thao tác PCR, thay đầu côn cho mỗi lần hút. Quy trình nhiệt cụ thể nằm trong tài liệu máy PCR.

## Hỏi đáp nhanh: micropipette, li tâm và điện di

- Hút 5 µL dùng micropipet nào? P10 (0,5–10 µL).
- Nấc thứ nhất và nấc thứ hai của micropipette khác nhau gì? Nấc 1 ứng với thể tích cài đặt khi hút; nấc 2 để đẩy hết dung dịch khi xả.
- Vì sao phải thay đầu côn? Tránh nhiễm chéo giữa các mẫu.
- Vì sao phải cân bằng ống khi li tâm? Để rôto không rung, tránh hỏng máy và vỡ ống.
- DNA chạy về cực nào khi điện di? Về cực dương (đỏ), vì DNA tích điện âm.
- Đoạn DNA ngắn đi xa hay gần giếng? Đi xa hơn.
- Thang DNA dùng để làm gì? So sánh để xác định kích thước đoạn DNA của mẫu.
- PCR gồm mấy bước mỗi chu kì? Ba bước: biến tính, gắn mồi, kéo dài.
- Enzyme dùng trong PCR là gì? Taq polymerase chịu nhiệt.
- Đối chứng âm trong PCR là gì? Ống thay DNA khuôn bằng nước, đúng thì không có vạch.
