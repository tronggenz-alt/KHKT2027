---
mon: sinh
lop: 12
loai: chuyen_de
chu_de: Chuyên đề 12.1 – Sinh học phân tử
tu_khoa: [sinh học phân tử, PCR, phản ứng chuỗi polymerase, mồi, Taq polymerase, điện di, gel agarose, giải trình tự DNA, phương pháp Sanger, giải trình tự thế hệ mới, NGS, DNA tái tổ hợp, enzyme cắt giới hạn, ligase, vector plasmid, chỉnh sửa gene, CRISPR-Cas9, tin sinh học, hệ gene người, xét nghiệm di truyền, DNA pháp y, sinh vật biến đổi gene]
nguon:
  - Chương trình GDPT 2018 môn Sinh học (Thông tư 32/2018/TT-BGDĐT), chuyên đề học tập lớp 12
  - Danh mục bài của SGK Chuyên đề học tập Sinh học 12 (Kết nối tri thức, Chân trời sáng tạo, Cánh diều)
  - Mullis K. B., Faloona F. A. (1987), Methods in Enzymology 155:335 – phản ứng PCR
  - Sanger F., Nicklen S., Coulson A. R. (1977), PNAS 74:5463 – giải trình tự bằng dideoxynucleotide
  - Jinek M., Charpentier E., Doudna J. A. và cs. (2012), Science 337:816 – hệ thống CRISPR-Cas9
  - National Human Genome Research Institute (genome.gov) – Human Genome Project, các kĩ thuật sinh học phân tử
---

# Chuyên đề: Sinh học phân tử

## Sinh học phân tử: đối tượng nghiên cứu và các thành tựu nền tảng

Sinh học phân tử là ngành khoa học nghiên cứu các hiện tượng sống ở **cấp độ phân tử**, tập trung vào cấu trúc, chức năng và sự tương tác của các đại phân tử sinh học – đặc biệt là **DNA, RNA và protein** – trong quá trình lưu giữ, truyền đạt và biểu hiện thông tin di truyền.

**Học thuyết trung tâm:** thông tin di truyền truyền từ DNA → RNA → protein (tái bản, phiên mã, dịch mã). Ngoại lệ: một số virus có quá trình phiên mã ngược RNA → DNA (HIV) hoặc tái bản RNA → RNA.

**Các thành tựu nền tảng:**
- 1953: James Watson và Francis Crick đề xuất mô hình chuỗi xoắn kép DNA, dựa trên dữ liệu nhiễu xạ tia X của Rosalind Franklin.
- 1958: thí nghiệm Meselson – Stahl chứng minh DNA tái bản theo nguyên tắc bán bảo toàn.
- Thập niên 1960: giải mã bảng mã di truyền; Jacob và Monod mô tả operon lac.
- Thập niên 1970: phát hiện enzyme cắt giới hạn, tạo DNA tái tổ hợp; phát minh phương pháp giải trình tự DNA (Sanger, 1977).
- 1983–1985: Kary Mullis phát minh kĩ thuật PCR.
- 2003: hoàn thành Dự án Hệ gene người, xác định trình tự khoảng 3 tỉ cặp nucleotide của hệ gene người đơn bội, với khoảng 20 nghìn gene mã hóa protein.
- 2012: phát triển công cụ chỉnh sửa gene CRISPR-Cas9 (giải Nobel Hóa học 2020 cho Emmanuelle Charpentier và Jennifer Doudna).

**Các nhóm kĩ thuật chính của sinh học phân tử:** tách chiết DNA, RNA, protein; nhân bản DNA (PCR); phân tách và phát hiện phân tử (điện di, lai phân tử); xác định trình tự (giải trình tự); tạo DNA tái tổ hợp và chuyển gene; chỉnh sửa gene; phân tích dữ liệu bằng tin sinh học.

**Ứng dụng:** chẩn đoán bệnh di truyền, bệnh truyền nhiễm; y học cá thể hóa; sản xuất thuốc, vaccine; chọn giống cây trồng, vật nuôi; pháp y; nghiên cứu tiến hóa và bảo tồn.

## Kĩ thuật PCR: nguyên lí và các bước của phản ứng chuỗi polymerase

**PCR (Polymerase Chain Reaction – phản ứng chuỗi polymerase)** là kĩ thuật **nhân bản nhanh một đoạn DNA đặc hiệu** trong ống nghiệm, từ một lượng DNA khuôn rất nhỏ tạo ra hàng triệu đến hàng tỉ bản sao chỉ trong vài giờ.

**Thành phần phản ứng PCR:**
- **DNA khuôn** chứa đoạn cần nhân.
- **Cặp mồi (primer):** hai đoạn DNA ngắn (khoảng 18–25 nucleotide) bổ sung với hai đầu của đoạn cần nhân, quyết định tính đặc hiệu.
- **DNA polymerase chịu nhiệt** (Taq polymerase, tách từ vi khuẩn suối nước nóng Thermus aquaticus).
- **Bốn loại nucleotide tự do** (dATP, dTTP, dGTP, dCTP).
- **Dung dịch đệm** có ion Mg2+.

**Một chu kì PCR gồm ba bước**, thực hiện trong máy luân nhiệt:
1. **Biến tính (khoảng 94–95 °C):** hai mạch DNA khuôn tách rời do liên kết hydrogen bị phá vỡ.
2. **Gắn mồi (khoảng 50–65 °C):** mồi bắt cặp bổ sung với đầu 3' của mỗi mạch khuôn.
3. **Kéo dài (khoảng 72 °C):** Taq polymerase gắn nucleotide vào đầu 3'-OH của mồi, tổng hợp mạch mới theo chiều 5' → 3'.

Lặp lại 25–35 chu kì. Về lí thuyết, **số bản sao tăng theo cấp số nhân: sau n chu kì, từ 1 phân tử DNA thu được 2^n phân tử**. Ví dụ sau 30 chu kì: 2^30 ≈ 1,07 × 10^9 bản sao.

**Vì sao cần DNA polymerase chịu nhiệt:** ở bước biến tính 94–95 °C, polymerase thông thường bị biến tính; Taq polymerase chịu được nhiệt độ cao nên không phải bổ sung enzyme sau mỗi chu kì, giúp tự động hóa.

**Biến thể:** **RT-PCR** – phiên mã ngược RNA thành cDNA rồi PCR, dùng phát hiện virus RNA (SARS-CoV-2, HIV); **Real-time PCR (qPCR)** – theo dõi lượng sản phẩm theo thời gian thực bằng tín hiệu huỳnh quang, cho phép định lượng (đo tải lượng virus).

**Ứng dụng:** chẩn đoán bệnh truyền nhiễm, phát hiện đột biến gây bệnh, xác định huyết thống, pháp y, kiểm tra thực phẩm biến đổi gene, nghiên cứu tiến hóa từ mẫu DNA cổ.

## Điện di DNA trên gel agarose và lai phân tử

**Điện di** là kĩ thuật **phân tách các phân tử theo kích thước** (và điện tích) dưới tác dụng của điện trường khi chúng di chuyển trong một môi trường gel.

**Nguyên lí điện di DNA:** DNA mang điện tích âm (do các nhóm phosphate) nên trong điện trường sẽ di chuyển từ cực âm về **cực dương**. Gel agarose có mạng lưới lỗ xốp; phân tử DNA **càng nhỏ di chuyển càng nhanh và càng xa**, phân tử lớn di chuyển chậm. Vì mọi phân tử DNA có tỉ lệ điện tích trên khối lượng gần như nhau, sự phân tách chủ yếu dựa vào kích thước.

**Các bước điện di:**
1. Đổ gel agarose (thường 0,8–2%) có giếng ở một đầu, đặt vào bể điện di chứa dung dịch đệm, giếng ở phía cực âm.
2. Trộn mẫu DNA với dung dịch nạp mẫu (có màu và chất làm nặng), nạp vào giếng bằng micropipette. Nạp thang DNA chuẩn (marker, chứa các đoạn DNA đã biết kích thước) vào một giếng riêng.
3. Chạy điện di với hiệu điện thế phù hợp đến khi vạch màu di chuyển gần hết gel.
4. Nhuộm DNA bằng chất phát huỳnh quang và quan sát dưới đèn UV hoặc đèn xanh; mỗi băng (vạch) tương ứng với các đoạn DNA cùng kích thước.
5. So sánh vị trí băng với thang chuẩn để ước lượng kích thước (tính bằng cặp base, bp).

**Ứng dụng:** kiểm tra sản phẩm PCR, kiểm tra DNA tách chiết, phân tích kết quả cắt bằng enzyme giới hạn, lập **dấu vân tay DNA** (DNA fingerprint) trong pháp y và xác định huyết thống. Protein cũng được điện di trên gel polyacrylamide (SDS-PAGE).

**Lai phân tử:** dựa trên nguyên tắc bổ sung, một **mẫu dò (probe)** là đoạn DNA hoặc RNA ngắn đã đánh dấu (phóng xạ, huỳnh quang) sẽ bắt cặp với trình tự bổ sung trong mẫu, giúp phát hiện sự có mặt của gene cụ thể. Ví dụ: kĩ thuật Southern blot (phát hiện DNA), Northern blot (RNA), lai huỳnh quang tại chỗ FISH (phát hiện bất thường NST), chip DNA (microarray) phân tích biểu hiện hàng nghìn gene cùng lúc.

## Giải trình tự DNA: phương pháp Sanger và giải trình tự thế hệ mới

**Giải trình tự DNA** là xác định **thứ tự sắp xếp các nucleotide** (A, T, G, C) trên một đoạn phân tử DNA.

**Phương pháp Sanger (phương pháp kết thúc chuỗi, 1977):**
- Dựa trên quá trình tổng hợp DNA nhân tạo từ mạch khuôn với mồi, DNA polymerase, các nucleotide thường (dNTP) và một lượng nhỏ **dideoxynucleotide (ddNTP)**.
- ddNTP thiếu nhóm 3'-OH, nên khi được gắn vào mạch đang tổng hợp, **mạch dừng kéo dài** tại vị trí đó.
- Kết quả tạo ra tập hợp các đoạn DNA có độ dài khác nhau, mỗi đoạn kết thúc bằng một ddNTP. Trong kĩ thuật hiện đại, mỗi loại ddNTP (ddA, ddT, ddG, ddC) được gắn chất huỳnh quang màu khác nhau.
- Điện di mao quản phân tách các đoạn hơn kém nhau một nucleotide; đầu đọc laser ghi lại màu của từng đoạn theo thứ tự kích thước, từ đó đọc ra trình tự.
- Phương pháp Sanger chính xác cao, đọc được đoạn khoảng 700–1000 nucleotide, vẫn được dùng để kiểm tra trình tự gene, xác nhận đột biến.

**Giải trình tự thế hệ mới (NGS – Next Generation Sequencing):** giải trình tự **song song hàng triệu đến hàng tỉ đoạn DNA ngắn** cùng lúc, sau đó dùng máy tính ghép nối các đoạn đọc thành trình tự hoàn chỉnh. NGS giúp giải trình tự toàn bộ hệ gene người chỉ trong vài ngày với chi phí giảm mạnh (từ khoảng 3 tỉ USD cho dự án hệ gene người đầu tiên xuống còn khoảng vài trăm đến một nghìn USD). Công nghệ **giải trình tự đoạn dài** (thế hệ ba) đọc được đoạn hàng chục nghìn nucleotide, giúp lắp ráp hệ gene chính xác hơn.

**Ứng dụng:** xác định hệ gene của các loài; chẩn đoán bệnh di truyền hiếm; sàng lọc trước sinh không xâm lấn (phân tích DNA tự do của thai nhi trong máu mẹ); phân tích đột biến trong khối u để chọn thuốc điều trị ung thư; giám sát biến chủng virus (SARS-CoV-2); nghiên cứu hệ vi sinh vật đường ruột (metagenomics); nghiên cứu nguồn gốc loài người.

## DNA tái tổ hợp và chuyển gene: enzyme giới hạn, ligase và vector

**DNA tái tổ hợp** là phân tử DNA được tạo ra bằng cách **nối đoạn DNA từ các nguồn khác nhau**, thường là gene cần chuyển nối với vector.

**Công cụ chính:**
- **Enzyme cắt giới hạn (restriction enzyme):** do vi khuẩn tạo ra để chống virus, nhận biết và cắt DNA tại các trình tự đặc hiệu (thường 4–8 cặp nucleotide, có tính đối xứng đảo ngược). Ví dụ EcoRI nhận biết trình tự GAATTC và cắt so le tạo **đầu dính**, giúp hai đoạn DNA được cắt cùng enzyme dễ bắt cặp với nhau.
- **DNA ligase:** nối các đoạn DNA bằng liên kết phosphodiester.
- **Vector (thể truyền):** phân tử DNA có khả năng tự tái bản trong tế bào chủ, mang gene cần chuyển vào tế bào. Phổ biến là **plasmid** (DNA vòng nhỏ ngoài NST của vi khuẩn), virus đã biến đổi, NST nhân tạo. Vector thường mang **gene đánh dấu** (gene kháng kháng sinh, gene phát huỳnh quang) để chọn lọc tế bào nhận.

**Quy trình tạo sinh vật chuyển gene (ví dụ vi khuẩn sản xuất insulin người):**
1. Tách gene mã hóa insulin người (hoặc tổng hợp cDNA từ mRNA) và tách plasmid.
2. Cắt gene và plasmid bằng cùng một enzyme giới hạn tạo đầu dính tương thích.
3. Nối gene vào plasmid bằng DNA ligase tạo plasmid tái tổ hợp.
4. Đưa plasmid tái tổ hợp vào vi khuẩn E. coli (biến nạp bằng sốc nhiệt với CaCl2 hoặc xung điện).
5. Nuôi trên môi trường có kháng sinh để chọn lọc vi khuẩn mang plasmid; kiểm tra bằng PCR, giải trình tự.
6. Nuôi vi khuẩn chuyển gene quy mô lớn, thu và tinh sạch insulin.

Insulin tái tổ hợp được cấp phép năm 1982, là thuốc đầu tiên sản xuất bằng công nghệ DNA tái tổ hợp.

**Thành tựu khác:** hormone tăng trưởng, yếu tố đông máu, interferon, vaccine viêm gan B tái tổ hợp; cây trồng biến đổi gene (ngô, bông kháng sâu mang gene Bt; lúa vàng giàu tiền chất vitamin A); động vật chuyển gene sản xuất protein dược phẩm trong sữa.

## Chỉnh sửa gene bằng CRISPR-Cas9 và liệu pháp gene

**CRISPR-Cas9** là công cụ chỉnh sửa gene dựa trên **cơ chế miễn dịch tự nhiên của vi khuẩn** chống lại virus. Vi khuẩn lưu giữ các đoạn DNA của virus đã xâm nhiễm trong vùng CRISPR; khi virus tái nhiễm, RNA phiên mã từ vùng này dẫn đường cho enzyme Cas cắt DNA virus.

**Thành phần của hệ thống chỉnh sửa gene:**
- **Enzyme Cas9:** "chiếc kéo phân tử" cắt cả hai mạch DNA.
- **RNA dẫn đường (gRNA):** khoảng 20 nucleotide được thiết kế bổ sung với trình tự đích trên DNA, dẫn Cas9 đến đúng vị trí cần cắt (cần có trình tự PAM ngắn bên cạnh).

**Cơ chế chỉnh sửa:** Cas9 cắt đứt hai mạch DNA tại vị trí đích. Tế bào sửa chữa vết cắt theo hai cách:
- **Nối hai đầu không tương đồng:** dễ gây thêm hoặc mất vài nucleotide, làm **bất hoạt gene** (knock-out).
- **Sửa chữa theo khuôn tương đồng:** nếu cung cấp đoạn DNA khuôn mong muốn, tế bào dùng khuôn để sửa, giúp **thay thế, sửa đột biến** hoặc chèn gene mới.

**Ưu điểm:** chính xác, đơn giản, rẻ, nhanh hơn nhiều so với các phương pháp trước, áp dụng được trên hầu hết các loài.

**Ứng dụng:**
- **Y học:** liệu pháp điều trị bệnh hồng cầu hình liềm và β-thalassemia bằng chỉnh sửa tế bào gốc tạo máu của bệnh nhân (được cấp phép lần đầu năm 2023); nghiên cứu điều trị ung thư, bệnh di truyền về mắt, gan.
- **Nông nghiệp:** tạo giống cây chống chịu hạn, mặn, sâu bệnh; giống lúa, cà chua cải thiện chất lượng mà không cần đưa gene ngoại lai.
- **Nghiên cứu:** tạo mô hình bệnh, xác định chức năng gene.

**Liệu pháp gene** là chữa bệnh bằng cách đưa gene lành vào tế bào bệnh nhân (thường dùng vector virus đã loại gene gây bệnh) hoặc sửa gene đột biến. Liệu pháp gene trên **tế bào soma** không di truyền sang đời sau và được chấp nhận trong khuôn khổ thử nghiệm lâm sàng. Chỉnh sửa gene trên **tế bào sinh dục hoặc phôi người** có thể di truyền cho thế hệ sau, gây tranh cãi lớn về đạo đức và bị cấm ở nhiều nước.

## Tin sinh học và ứng dụng sinh học phân tử trong y học, pháp y, nông nghiệp

**Tin sinh học (bioinformatics)** là ngành kết hợp sinh học, tin học và thống kê để **lưu trữ, phân tích và giải thích dữ liệu sinh học**, đặc biệt là dữ liệu trình tự DNA, RNA, protein khổng lồ do giải trình tự tạo ra.

**Công cụ và cơ sở dữ liệu:** ngân hàng gene GenBank (NCBI), cơ sở dữ liệu protein UniProt, cấu trúc protein PDB; công cụ BLAST so sánh trình tự với cơ sở dữ liệu để tìm gene tương đồng; phần mềm lắp ráp hệ gene, dự đoán gene, xây dựng cây phát sinh chủng loại; trí tuệ nhân tạo dự đoán cấu trúc không gian của protein từ trình tự amino acid (AlphaFold).

**Ứng dụng của sinh học phân tử:**
- **Y học:** chẩn đoán bệnh truyền nhiễm bằng PCR, RT-PCR; sàng lọc sơ sinh và sàng lọc trước sinh; phát hiện gene gây bệnh (BRCA1, BRCA2 liên quan ung thư vú); **y học cá thể hóa** – chọn thuốc và liều dựa trên kiểu gene của người bệnh; sản xuất thuốc và vaccine tái tổ hợp, vaccine mRNA.
- **Pháp y và xác định huyết thống:** phân tích các vùng DNA lặp lại ngắn (STR) có độ đa hình cao để lập hồ sơ DNA, nhận dạng tội phạm từ mẫu máu, tóc, nước bọt; xác định danh tính hài cốt liệt sĩ; xác định quan hệ cha – con.
- **Nông nghiệp:** chọn giống nhờ chỉ thị phân tử (MAS) giúp chọn cây mang gene mong muốn ngay ở giai đoạn cây con; tạo cây trồng, vật nuôi biến đổi gene; chẩn đoán bệnh cây, bệnh vật nuôi.
- **Bảo tồn và tiến hóa:** mã vạch DNA nhận dạng loài, phát hiện buôn bán động vật hoang dã trái phép; nghiên cứu quan hệ họ hàng giữa các loài, nguồn gốc và di cư của loài người.
- **An toàn thực phẩm:** phát hiện vi khuẩn gây bệnh, kiểm tra gian lận (thịt pha trộn), xác định thực phẩm biến đổi gene.

**Vấn đề đạo đức:** bảo mật thông tin di truyền cá nhân; chống phân biệt đối xử dựa trên gene (bảo hiểm, việc làm); quản lí an toàn sinh học đối với sinh vật biến đổi gene; giới hạn chỉnh sửa gene người.

## Thực hành tách chiết DNA và mô phỏng các kĩ thuật sinh học phân tử

**Thực hành tách chiết DNA từ chuối, dâu tây hoặc hành tây:**
1. Nghiền khoảng 50 g mẫu với 100 mL **dung dịch tách chiết** gồm nước, một thìa nước rửa chén (phá màng tế bào và màng nhân do chứa chất tẩy phá lớp lipid) và một ít muối ăn (giúp DNA kết tụ, trung hòa điện tích âm của DNA).
2. Lọc hỗn hợp qua vải màn hoặc giấy lọc, thu dịch lọc vào ống nghiệm.
3. Có thể thêm vài giọt nước ép dứa hoặc dịch đu đủ (chứa protease) để phân giải protein liên kết với DNA.
4. Nghiêng ống nghiệm, rót nhẹ theo thành ống một lượng **ethanol lạnh** (hoặc cồn 90°) bằng thể tích dịch lọc, tạo lớp riêng phía trên.
5. Sau vài phút, ở mặt phân cách xuất hiện các **sợi trắng đục** – đó là DNA kết tủa (DNA không tan trong ethanol). Dùng que tre quấn nhẹ để lấy DNA.

**Mô phỏng và thực hành khác:**
- **Mô phỏng PCR** bằng mô hình giấy hoặc phần mềm: vẽ sơ đồ số bản sao qua các chu kì, tính 2^n.
- **Điện di phẩm màu thực phẩm** trên gel agarose (nếu trường có bộ điện di): quan sát sự phân tách các chất màu, hiểu nguyên lí điện di.
- **Tra cứu dữ liệu tin sinh học:** dùng cơ sở dữ liệu NCBI để tìm trình tự gene insulin người, dùng BLAST so sánh trình tự với các loài khác; đọc bảng mã di truyền dịch một đoạn mRNA thành chuỗi amino acid.
- **Đọc kết quả điện di giả định:** xác định huyết thống hoặc nghi phạm dựa trên hình ảnh các băng DNA.

**An toàn:** dùng kính bảo hộ, tránh xa lửa khi dùng ethanol; nếu có đèn UV phải dùng tấm chắn và kính chống UV; chất nhuộm DNA cần găng tay; không tiến hành thí nghiệm chuyển gene, nuôi cấy vi khuẩn biến đổi gene trong phòng thí nghiệm trường học.

## Những nhầm lẫn thường gặp về sinh học phân tử

**1. "PCR tạo ra toàn bộ hệ gene."** Sai. PCR chỉ nhân bản đoạn DNA nằm giữa hai mồi đã thiết kế.

**2. "Sau n chu kì PCR thu được 2n bản sao."** Sai. Số bản sao tăng theo cấp số nhân 2^n (lí thuyết), không phải 2n.

**3. "Trong điện di, DNA lớn chạy nhanh hơn."** Sai. Đoạn DNA nhỏ chạy nhanh và xa hơn; DNA di chuyển về cực dương vì mang điện tích âm.

**4. "ddNTP giúp kéo dài mạch DNA."** Ngược lại, ddNTP thiếu nhóm 3'-OH nên làm dừng kéo dài mạch – cơ sở của phương pháp Sanger.

**5. "Enzyme cắt giới hạn cắt DNA ở vị trí ngẫu nhiên."** Sai. Mỗi enzyme nhận biết và cắt tại trình tự đặc hiệu.

**6. "CRISPR-Cas9 tự tìm gene bệnh."** Cas9 được RNA dẫn đường (do con người thiết kế) đưa đến đúng trình tự đích.

**7. "Mọi liệu pháp gene đều di truyền sang con cái."** Liệu pháp gene trên tế bào soma không di truyền; chỉ chỉnh sửa tế bào sinh dục hoặc phôi mới di truyền.

**8. "Sợi trắng tách từ chuối là một phân tử DNA nhìn bằng mắt thường."** Đó là khối hàng triệu phân tử DNA kết tụ lại, không phải một phân tử.

## Hỏi đáp nhanh: Sinh học phân tử

Hỏi: PCR gồm những bước nào trong một chu kì?
Đáp: Biến tính khoảng 94–95 °C, gắn mồi khoảng 50–65 °C, kéo dài khoảng 72 °C.

Hỏi: Vì sao PCR dùng Taq polymerase?
Đáp: Vì enzyme này chịu nhiệt, không bị biến tính ở bước 94–95 °C.

Hỏi: Sau 30 chu kì PCR, từ 1 phân tử DNA thu được bao nhiêu bản sao theo lí thuyết?
Đáp: 2^30, khoảng 1,07 tỉ bản sao.

Hỏi: Trong điện di gel agarose, DNA di chuyển về cực nào và đoạn nào đi xa hơn?
Đáp: Về cực dương; đoạn ngắn đi nhanh và xa hơn.

Hỏi: Nguyên lí phương pháp giải trình tự Sanger là gì?
Đáp: Dùng ddNTP thiếu nhóm 3'-OH làm dừng tổng hợp mạch, tạo các đoạn dài ngắn khác nhau rồi điện di để đọc trình tự.

Hỏi: DNA tái tổ hợp được tạo nhờ những enzyme nào?
Đáp: Enzyme cắt giới hạn để cắt và DNA ligase để nối gene vào vector.

Hỏi: CRISPR-Cas9 gồm những thành phần chính nào?
Đáp: Enzyme Cas9 cắt DNA và RNA dẫn đường gRNA đưa Cas9 đến trình tự đích.

Hỏi: Tin sinh học dùng để làm gì?
Đáp: Lưu trữ, phân tích, so sánh dữ liệu trình tự DNA, RNA, protein bằng máy tính, ví dụ dùng BLAST tìm gene tương đồng.

Hỏi: Khi tách chiết DNA từ chuối, nước rửa chén và ethanol lạnh có vai trò gì?
Đáp: Nước rửa chén phá màng tế bào và màng nhân; ethanol lạnh làm DNA kết tủa thành sợi trắng.
