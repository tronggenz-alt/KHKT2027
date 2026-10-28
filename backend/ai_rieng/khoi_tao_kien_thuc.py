"""Nạp đầy đủ các kiến thức, quy tắc cốt lõi, tài liệu chuyên đề và bài mẫu về Đại cương Hoá hữu cơ vào kho tri thức AI Riêng."""
from __future__ import annotations

import json
import os
import sys

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "../..")))
from backend.ai_rieng import kho, xu_ly

# ================================================================ 25 QUY TẮC CỐT LÕI VỀ ĐẠI CƯƠNG HỮU CƠ
KIEN_THUC_COT_LOI = [
    {
        "tieu_de": "Quy tắc cộng Markovnikov (Anken & Ankin bất đối xứng)",
        "chu_de": "Hidrocacbon",
        "uu_tien": 5,
        "noi_dung": (
            "Khi cộng tác nhân bất đối xứng dạng HX (HCl, HBr, HI, H2O/H+) vào liên kết bội C=C hoặc C≡C bất đối xứng:\n"
            "- Sản phẩm chính: Nguyên tử H ưu tiên cộng vào nguyên tử cacbon có nhiều hiđro hơn (bậc C thấp hơn), "
            "còn phần mang điện âm X (halogen, nhóm OH) ưu tiên cộng vào nguyên tử cacbon có ít hiđro hơn (bậc C cao hơn).\n"
            "- Ví dụ: CH3-CH=CH2 + HBr → CH3-CH(Br)-CH3 (2-brompropan là sản phẩm chính).\n"
            "- Ngoại lệ (Hiệu ứng Kharasch / Peroxit): Khi cộng HBr vào anken có mặt peoxit hữu cơ (ROOR), "
            "phản ứng xảy ra theo cơ chế gốc tự do và tuân theo quy tắc phản Markovnikov: H cộng vào C ít H hơn, Br cộng vào C nhiều H hơn. "
            "Chỉ áp dụng với HBr, không xảy ra với HCl và HI."
        )
    },
    {
        "tieu_de": "Quy tắc tách Zaitsev (Zaytsev) tạo anken",
        "chu_de": "Dẫn xuất halogen, Ancol",
        "uu_tien": 5,
        "noi_dung": (
            "Khi tách phân tử HX từ dẫn xuất halogen (xúc tác KOH/ancol, đun nóng) hoặc tách nước H2O từ ancol (xúc tác H2SO4 đặc ở 170°C):\n"
            "- Sản phẩm chính: Nhóm halogen -X hoặc nhóm -OH ưu tiên tách cùng với nguyên tử H ở nguyên tử C liền kề có bậc cao hơn "
            "(nguyên tử C có ít H hơn) để tạo thành anken có nhiều nhóm thế hơn quanh nối đôi (anken bền hơn về mặt nhiệt động học).\n"
            "- Ví dụ: CH3-CH(OH)-CH2-CH3 (butan-2-ol) tách nước với H2SO4 đặc ở 170°C tạo but-2-en (CH3-CH=CH-CH3) là sản phẩm chính, "
            "but-1-en (CH2=CH-CH2-CH3) là sản phẩm phụ."
        )
    },
    {
        "tieu_de": "Quy tắc định hướng thế electrophin trên nhân thơm (Quy tắc Holleman)",
        "chu_de": "Aren, Benzen",
        "uu_tien": 5,
        "noi_dung": (
            "Khi vòng benzen đã có sẵn một nhóm thế, phản ứng thế nguyên tử H tiếp theo phụ thuộc vào bản chất của nhóm thế đó:\n"
            "1. Nhóm thế loại 1 (nhóm đẩy electron, hoạt hóa nhân thơm): -OH, -NH2, -OCH3, -CH3, -C2H5 và các gốc ankyl. "
            "Định hướng tác nhân thế tiếp theo vào các vị trí ortho (o-) và para (p-). Phản ứng xảy ra dễ dàng hơn so với benzen.\n"
            "2. Nhóm thế loại 2 (nhóm hút electron, khử hoạt nhân thơm): -NO2, -COOH, -CHO, -SO3H, -COCH3, -CN. "
            "Định hướng tác nhân thế vào vị trí meta (m-). Phản ứng xảy ra khó khăn hơn so với benzen.\n"
            "3. Halogen (-Cl, -Br): Là nhóm hút electron (do hiệu ứng cảm ứng âm -I) nhưng lại định hướng thế vào vị trí ortho/para "
            "(do hiệu ứng liên hợp dương +C của cặp e tự do)."
        )
    },
    {
        "tieu_de": "Cơ chế thế gốc tự do SR của Ankan",
        "chu_de": "Ankan",
        "uu_tien": 4,
        "noi_dung": (
            "Phản ứng halogen hoá ankan (với Cl2, Br2) xảy ra theo cơ chế thế gốc tự do SR dưới tác dụng của ánh sáng khuếch tán (askt) hoặc đun nóng:\n"
            "- Nguyên tử H đính ở nguyên tử cacbon bậc cao hơn (bậc 3 > bậc 2 > bậc 1) có năng lượng liên kết C-H nhỏ hơn nên dễ bị bứt ra và thế hơn.\n"
            "- Tính chọn lọc: Brom có tính chọn lọc rất cao, hầu như chỉ thế vào H ở C bậc cao (tỷ lệ sản phẩm thế bậc 3 có thể lên tới trên 99%). "
            "Clo có tính chọn lọc kém hơn, sinh ra hỗn hợp nhiều đồng phân dẫn xuất monoclo.\n"
            "- Ví dụ: (CH3)2CH-CH3 + Br2 (1:1, askt) → (CH3)3C-Br (2-brom-2-metylpropan) là sản phẩm chính vượt trội."
        )
    },
    {
        "tieu_de": "Phản ứng Kucherov (Hidrat hoá ankin)",
        "chu_de": "Ankin, Hợp chất cacbonyl",
        "uu_tien": 5,
        "noi_dung": (
            "Khi cho ankin tác dụng với nước (xúc tác HgSO4 trong môi trường H2SO4, đun nóng ở 80°C):\n"
            "- Với axetilen (CH≡CH): CH≡CH + H2O → [CH2=CH-OH] (enol không bền) → CH3-CHO (anđehit axetic).\n"
            "- Với các ankin khác (R-C≡CH hoặc R-C≡C-R'): Phản ứng cộng nước tuân theo quy tắc Markovnikov, tạo enol không bền rồi chuyển vị hỗ biến keto-enol "
            "thành xeton.\n"
            "Ví dụ: CH3-C≡CH + H2O → [CH3-C(OH)=CH2] → CH3-CO-CH3 (axeton)."
        )
    },
    {
        "tieu_de": "Oxi hoá không hoàn toàn ancol bằng CuO",
        "chu_de": "Ancol, Hợp chất cacbonyl",
        "uu_tien": 5,
        "noi_dung": (
            "Khi dẫn hơi ancol qua bột CuO màu đen nung nóng:\n"
            "1. Ancol bậc 1 bị oxi hoá thành anđehit: R-CH2OH + CuO →(t°) R-CHO + Cu↓ (màu đỏ) + H2O.\n"
            "2. Ancol bậc 2 bị oxi hoá thành xeton: R-CH(OH)-R' + CuO →(t°) R-CO-R' + Cu↓ + H2O.\n"
            "3. Ancol bậc 3 không có nguyên tử H ở nguyên tử C mang nhóm -OH nên không bị oxi hoá trong điều kiện này (CuO giữ nguyên màu đen).\n"
            "Phản ứng này là cơ sở quan trọng để điều chế anđehit/xeton và nhận biết bậc của ancol."
        )
    },
    {
        "tieu_de": "Phản ứng este hoá Fischer & Thủy phân este",
        "chu_de": "Este, Axit cacboxylic",
        "uu_tien": 5,
        "noi_dung": (
            "1. Phản ứng este hoá Fischer:\n"
            "R-COOH + R'-OH ⇌(H2SO4 đặc, t°) R-COO-R' + H2O. Là phản ứng thuận nghịch, H2SO4 đặc đóng vai trò vừa là chất xúc tác, "
            "vừa là chất hút nước làm chuyển dịch cân bằng theo chiều thuận.\n"
            "2. Phản ứng thủy phân este:\n"
            "- Trong môi trường axit: R-COO-R' + H2O ⇌(H+, t°) R-COOH + R'-OH (thuận nghịch).\n"
            "- Trong môi trường kiềm (xà phòng hoá): R-COO-R' + NaOH →(t°) R-COONa + R'-OH (phản ứng một chiều).\n"
            "- Este đặc biệt: Este phenyl R-COO-C6H5 phản ứng với NaOH theo tỉ lệ 1:2 tạo hai muối (R-COONa + C6H5ONa + H2O), không tạo ancol."
        )
    },
    {
        "tieu_de": "Phản ứng tráng bạc và phản ứng nhận biết nhóm chức",
        "chu_de": "Anđehit, Cacbohydrat",
        "uu_tien": 5,
        "noi_dung": (
            "1. Phản ứng tráng bạc (thuốc thử Tollens - [Ag(NH3)2]OH / AgNO3 trong NH3 dư, t°):\n"
            "- Nhóm anđehit (-CHO) bị oxi hoá tạo kết tủa Ag sáng bóng:\n"
            "  R-CHO + 2 AgNO3 + 3 NH3 + H2O →(t°) R-COONH4 + 2 Ag↓ + 2 NH4NO3.\n"
            "- Riêng fomanđehit (HCHO) phản ứng theo tỉ lệ 1 : 4 Ag: HCHO → 4 Ag↓ + (NH4)2CO3.\n"
            "- Axit fomic (HCOOH) và este fomat (HCOOR) cũng có phản ứng tráng bạc do chứa nhóm -CHO.\n"
            "2. Phản ứng với Cu(OH)2:\n"
            "- Ở nhiệt độ phòng: Các poliancol có ít nhất 2 nhóm -OH kề nhau (etylen glicol, glixerol, glucozơ...) hoà tan Cu(OH)2 tạo dung dịch phức màu xanh lam thẫm.\n"
            "- Khi đun nóng: Nhóm anđehit (-CHO) khử Cu(OH)2 trong môi trường kiềm tạo kết tủa đỏ gạch Cu2O↓."
        )
    },
    {
        "tieu_de": "Phản ứng thế nguyên tử H linh động của Ank-1-in",
        "chu_de": "Ankin",
        "uu_tien": 4,
        "noi_dung": (
            "Các ankin có liên kết ba ở đầu mạch (ank-1-in, dạng R-C≡CH hay HC≡CH):\n"
            "Do nguyên tử cacbon lai hoá sp có độ âm điện lớn, liên kết ≡C-H bị phân cực mạnh, nguyên tử H có tính axit yếu và có thể bị thế bởi ion kim loại nặng:\n"
            "R-C≡CH + AgNO3 + NH3 → R-C≡CAg↓ (kết tủa màu vàng nhạt) + NH4NO3.\n"
            "Với axetilen (HC≡CH): HC≡CH + 2 AgNO3 + 2 NH3 → AgC≡CAg↓ (bạc axetilua vàng nhạt) + 2 NH4NO3.\n"
            "Phản ứng này dùng để phân biệt ank-1-in với các ankin khác và các hidrocacbon không no khác."
        )
    },
    {
        "tieu_de": "Độ bất bão hoà k và bảo toàn liên kết trong hợp chất hữu cơ",
        "chu_de": "Đại cương hữu cơ",
        "uu_tien": 5,
        "noi_dung": (
            "Với hợp chất hữu cơ công thức CxHyOzNtXu (X là halogen):\n"
            "- Độ bất bão hoà: k = (2x + 2 + t - y - u) / 2 = số liên kết π + số vòng.\n"
            "- Ý nghĩa:\n"
            "  • k = 0: Hợp chất no, mạch hở (ankan, ancol no đơn hở, ete no đơn hở...).\n"
            "  • k = 1: Chứa 1 liên kết đôi C=C (anken) hoặc 1 liên kết C=O (anđehit, xeton, axit no đơn, este no đơn), hoặc 1 vòng no (xicloankan).\n"
            "  • k = 2: Chứa 1 liên kết ba C≡C (ankin, ankađien) hoặc 2 liên kết đôi, hoặc 1 liên kết đôi + 1 vòng.\n"
            "  • k = 4: Nhân benzen có k = 4 (gồm 1 vòng 6 cạnh + 3 liên kết π liên hợp).\n"
            "- Quy tắc hoá trị bắt buộc: Cacbon luôn có hoá trị IV (tạo 4 liên kết); Hiđro hoá trị I; Oxi hoá trị II; Nitơ hoá trị III; Halogen hoá trị I."
        )
    },
    {
        "tieu_de": "Thuyết cấu tạo hoá học Butlerov và Hiện tượng đồng phân",
        "chu_de": "Đại cương hữu cơ",
        "uu_tien": 5,
        "noi_dung": (
            "1. Thuyết cấu tạo hoá học:\n"
            "- Trong phân tử hợp chất hữu cơ, các nguyên tử liên kết với nhau theo đúng hoá trị và theo một trật tự nhất định gọi là cấu tạo hoá học. "
            "Thay đổi trật tự liên kết sẽ tạo ra chất mới.\n"
            "- Cacbon luôn có hoá trị IV. Nguyên tử C không những liên kết với nguyên tử của các nguyên tố khác mà còn liên kết trực tiếp với nhau tạo thành mạch cacbon (mạch không nhánh, mạch nhánh, mạch vòng).\n"
            "- Tính chất của các chất phụ thuộc vào thành phần phân tử và cấu tạo hoá học.\n"
            "2. Các loại đồng phân cấu tạo:\n"
            "- Đồng phân mạch cacbon: Mạch thẳng (butan) vs mạch nhánh (isobutan).\n"
            "- Đồng phân vị trí: Vị trí liên kết bội (but-1-en vs but-2-en), vị trí nhóm chức (propan-1-ol vs propan-2-ol), vị trí nhóm thế trên vòng (o-, m-, p-).\n"
            "- Đồng phân nhóm chức khác nhau: Ancol no đơn hở vs Ete no đơn hở (CnH2n+2O); Anđehit no đơn hở vs Xeton no đơn hở (CnH2nO); Axit cacboxylic no đơn hở vs Este no đơn hở (CnH2nO2)."
        )
    },
    {
        "tieu_de": "Đồng phân hình học cis-trans (E-Z) trong Hợp chất hữu cơ",
        "chu_de": "Đại cương hữu cơ",
        "uu_tien": 5,
        "noi_dung": (
            "1. Điều kiện cần và đủ để xuất hiện đồng phân hình học:\n"
            "- Phân tử phải chứa liên kết đôi C=C (hoặc vòng no) cản trở sự quay tự do quanh trục liên kết.\n"
            "- Mỗi nguyên tử cacbon ở nối đôi phải liên kết với hai nguyên tử hoặc nhóm nguyên tử khác nhau: dạng R1R2C=CR3R4 (với R1 ≠ R2 và R3 ≠ R4).\n"
            "- Ví dụ: but-2-en (CH3-CH=CH-CH3) có đồng phân hình học; but-1-en (CH2=CH-CH2-CH3) KHÔNG có đồng phân hình học vì C đầu mạch liên kết 2 nguyên tử H giống nhau.\n"
            "2. Quy ước gọi tên:\n"
            "- Dạng cis: Hai nhóm thế chính (hoặc có khối lượng phân tử lớn hơn) nằm cùng một phía so với mặt phẳng nối đôi.\n"
            "- Dạng trans: Hai nhóm thế chính nằm về hai phía khác nhau so với mặt phẳng nối đôi.\n"
            "- Tính chất vật lý: Đồng phân trans thường có cấu trúc đối xứng hơn nên xếp chặt khít hơn trong tinh thể → nhiệt độ nóng chảy cao hơn dạng cis. Dạng cis có mômen lưỡng cực lớn hơn dạng trans → nhiệt độ sôi thường cao hơn dạng trans."
        )
    },
    {
        "tieu_de": "Quy tắc xác định Bậc Cacbon, Bậc Ancol và Bậc Amin",
        "chu_de": "Đại cương hữu cơ",
        "uu_tien": 5,
        "noi_dung": (
            "1. Bậc của nguyên tử Cacbon:\n"
            "- Bậc của một nguyên tử C trong phân tử bằng số nguyên tử C khác liên kết trực tiếp với nó.\n"
            "- Cacbon bậc 1 (liên kết 1 C), bậc 2 (liên kết 2 C), bậc 3 (liên kết 3 C), bậc 4 (liên kết 4 C).\n"
            "2. Bậc của Ancol:\n"
            "- Bằng bậc của nguyên tử cacbon liên kết trực tiếp với nhóm -OH.\n"
            "- Ancol bậc 1: R-CH2-OH (vd: CH3-CH2-OH).\n"
            "- Ancol bậc 2: R-CH(OH)-R' (vd: CH3-CH(OH)-CH3).\n"
            "- Ancol bậc 3: R-C(OH)(R')-R'' (vd: (CH3)3C-OH).\n"
            "3. Bậc của Amin:\n"
            "- Bằng số nguyên tử hiđro trong phân tử amoniac NH3 bị thay thế bởi gốc hidrocacbon.\n"
            "- Amin bậc 1: R-NH2 (thay 1 H).\n"
            "- Amin bậc 2: R-NH-R' (thay 2 H).\n"
            "- Amin bậc 3: R-N(R')(R'') (thay 3 H).\n"
            "Lưu ý: Bậc amin KHÔNG phụ thuộc vào bậc của nguyên tử C mang nhóm chức (ví dụ (CH3)3C-NH2 là amin bậc 1 gắn vào C bậc 3)."
        )
    },
    {
        "tieu_de": "Hiệu ứng cảm ứng điện tử (±I) và Quy luật so sánh tính axit",
        "chu_de": "Axit cacboxylic, Đại cương hữu cơ",
        "uu_tien": 5,
        "noi_dung": (
            "1. Bản chất hiệu ứng cảm ứng (I):\n"
            "- Là sự dịch chuyển mật độ electron dọc theo trục liên kết σ do sự chênh lệch độ âm điện giữa các nguyên tử.\n"
            "- Hiệu ứng giảm rất nhanh theo khoảng cách mạch cacbon (qua 3 liên kết thì coi như triệt tiêu).\n"
            "2. Phân loại:\n"
            "- Hiệu ứng cảm ứng âm (-I, hút e): Các nhóm có độ âm điện lớn hơn H như -F > -Cl > -Br > -I > -OH > -NH2 > -NO2 > -COOH > -C6H5. Nhóm hút e làm tăng độ phân cực liên kết O-H trong nhóm -COOH, làm ion H+ dễ phân li → TĂNG LỰC AXIT.\n"
            "- Hiệu ứng cảm ứng dương (+I, đẩy e): Các gốc ankyl no: -C(CH3)3 > -CH(CH3)2 > -C2H5 > -CH3. Gốc ankyl càng lớn và phân nhánh càng đẩy e vào nhóm -COOH, làm giảm độ phân cực liên kết O-H → GIẢM LỰC AXIT.\n"
            "3. So sánh lực axit:\n"
            "- Axit fomic vs axit axetic: HCOOH > CH3COOH (vì CH3- gây +I làm giảm tính axit).\n"
            "- Dẫn xuất halogen của axit: CCl3COOH > CHCl2COOH > CH2ClCOOH > CH3COOH.\n"
            "- Khoảng cách: CH3-CH2-CH(Cl)-COOH > CH3-CH(Cl)-CH2-COOH > CH2(Cl)-CH2-CH2-COOH."
        )
    },
    {
        "tieu_de": "Hiệu ứng liên hợp điện tử (±C / ±R) trong hệ liên hợp và Aren",
        "chu_de": "Aren, Phenol, Đại cương hữu cơ",
        "uu_tien": 5,
        "noi_dung": (
            "1. Bản chất hiệu ứng liên hợp (C hoặc R):\n"
            "- Xuất hiện trong các hệ liên hợp có sự xen phủ bên của các obitan p: liên hợp π-π (chứa các liên kết đôi cách nhau 1 liên kết đơn như buta-1,3-đien) "
            "hoặc liên hợp p-π (cặp electron tự do trên dị tố liên hợp với hệ electron π của liên kết đôi hoặc nhân thơm).\n"
            "- Hiệu ứng liên hợp truyền đi xa trong toàn bộ hệ liên hợp phẳng mà không bị suy giảm nhanh như hiệu ứng cảm ứng.\n"
            "2. Phân loại:\n"
            "- Nhóm liên hợp dương (+C): Có cặp electron tự do (p-π) đẩy vào hệ liên hợp: -O⁻ > -NH2 > -OH > -OCH3 > -X (halogen). "
            "Trong phân tử phenol (C6H5OH), cặp e tự do trên O liên hợp với vòng benzen, làm giàu mật độ electron ở vị trí ortho và para, "
            "khiến phenol phản ứng thế brom/nitro dễ hơn benzen và làm tăng tính axit của nhóm -OH (phenol có tính axit, tác dụng được với NaOH).\n"
            "- Nhóm liên hợp âm (-C): Chứa liên kết bội mang dị tố hút e (C=O, C≡N, NO2): -NO2, -CN, -CHO, -COOH. Hút electron ra khỏi nhân thơm, định hướng thế meta."
        )
    },
    {
        "tieu_de": "Quy luật so sánh lực bazơ của các Amin",
        "chu_de": "Amin, Đại cương hữu cơ",
        "uu_tien": 5,
        "noi_dung": (
            "1. Yếu tố quyết định lực bazơ của amin:\n"
            "- Khả năng nhận proton H+ phụ thuộc vào mật độ electron trên nguyên tử nitơ (cặp e chưa liên kết) và hiệu ứng hydrat hoá (dung môi nước) của cation amoni sinh ra.\n"
            "2. Quy luật tổng quát trong dung dịch nước:\n"
            "Amin no bậc 2 > Amin no bậc 1 > NH3 > Amin thơm (Anilin C6H5NH2).\n"
            "Ví dụ: (CH3)2NH > CH3NH2 > NH3 > C6H5NH2.\n"
            "3. Giải thích:\n"
            "- Các gốc ankyl (-CH3, -C2H5) có hiệu ứng cảm ứng đẩy electron (+I) làm tăng mật độ electron trên nguyên tử N → tăng lực bazơ so với NH3.\n"
            "- Gốc phenyl (-C6H5) có hiệu ứng liên hợp hút e (+C) giải tỏa cặp e của N vào vòng benzen → giảm mạnh mật độ e trên N → lực bazơ yếu hơn NH3 nhiều lần.\n"
            "- Tính chất: Amin no (metylamin, đimetylamin...) làm xanh quỳ tím và hồng phenolphtalein; còn anilin KHÔNG làm đổi màu quỳ tím."
        )
    },
    {
        "tieu_de": "Phản ứng thế nucleophin (SN) của Dẫn xuất halogen",
        "chu_de": "Dẫn xuất halogen, Ancol",
        "uu_tien": 4,
        "noi_dung": (
            "Liên kết C-X (X = Cl, Br, I) phân cực về phía halogen (Cδ+ - Xδ-), nguyên tử cacbon mang điện tích dương là trung tâm của các phản ứng thế nucleophin:\n"
            "1. Phản ứng thủy phân thế -X bằng -OH (tạo ancol):\n"
            "- Tác nhân: Dung dịch kiềm loãng trong nước (NaOH hoặc KOH, đun nóng nhẹ).\n"
            "- Phương trình: R-X + NaOH →(H2O, t°) R-OH + NaX.\n"
            "- Ví dụ: C2H5Br + NaOH → C2H5OH + NaBr.\n"
            "2. Phản ứng phân biệt với phản ứng tách anken:\n"
            "- Nếu dùng môi trường ancol (KOH / C2H5OH, đun nóng): Xảy ra phản ứng TÁCH HX tạo anken (quy tắc Zaitsev).\n"
            "- Muốn điều chế ancol: Bắt buộc dùng dung dịch kiềm trong nước."
        )
    },
    {
        "tieu_de": "Phản ứng cộng nucleophin (AN) vào nhóm Cacbonyl C=O",
        "chu_de": "Hợp chất cacbonyl, Anđehit",
        "uu_tien": 4,
        "noi_dung": (
            "Liên kết đôi C=O trong anđehit và xeton phân cực mạnh về phía O (Cδ+ = Oδ-). Nguyên tử C mang điện dương dễ bị tác nhân nucleophin (Nu⁻) tấn công:\n"
            "1. Cộng axit xianhiđric (HCN):\n"
            "R-CHO + HCN → R-CH(OH)-CN (xianohidrin).\n"
            "Sau đó thủy phân nhóm -CN trong môi trường axit sinh ra α-hidroxi axit: R-CH(OH)-COOH.\n"
            "2. Cộng natri bisunfit (NaHSO3):\n"
            "R-CHO + NaHSO3 → R-CH(OH)-SO3Na↓ (tinh thể kết tinh, dùng để tách và tinh chế anđehit).\n"
            "3. Cộng thuốc thử Grignard (R'MgX):\n"
            "- HCHO + R'MgX →(sau thủy phân) Ancol bậc 1 (R'-CH2OH).\n"
            "- RCHO + R'MgX →(sau thủy phân) Ancol bậc 2 (R-CH(OH)-R').\n"
            "- RCOR'' + R'MgX →(sau thủy phân) Ancol bậc 3."
        )
    },
    {
        "tieu_de": "Phản ứng vôi tôi xút (Phản ứng Dumas - Decacboxyl hoá)",
        "chu_de": "Ankan, Axit cacboxylic",
        "uu_tien": 5,
        "noi_dung": (
            "1. Điều kiện phản ứng:\n"
            "Nung muối natri của axit cacboxylic ở trạng thái khan với hỗn hợp vôi tôi xút (gồm NaOH rắn và CaO rắn theo tỉ lệ khối lượng ~ 1 : 1,25):\n"
            "CaO có vai trò làm xốp hỗn hợp, ngăn không cho NaOH ăn mòn thuỷ tinh ở nhiệt độ cao và hút ẩm.\n"
            "2. Phương trình hoá học tổng quát:\n"
            "R-(COONa)n + n NaOH →(CaO, t°) R-Hn + n Na2CO3.\n"
            "3. Ứng dụng trong chuỗi phản ứng:\n"
            "- Điều chế metan: CH3COONa + NaOH →(CaO, t°) CH4↑ + Na2CO3.\n"
            "- Giảm mạch cacbon: Chuyển muối natri axetat (2 C) thành ankan metan (1 C).\n"
            "- Điều chế benzen: C6H5COONa + NaOH →(CaO, t°) C6H6 + Na2CO3.\n"
            "- Với muối đinatri của axit oxalic: (COONa)2 + 2 NaOH →(CaO, t°) H2↑ + 2 Na2CO3."
        )
    },
    {
        "tieu_de": "Phản ứng tạo ete (140°C) vs Tách nước tạo anken (170°C) của Ancol",
        "chu_de": "Ancol, Hidrocacbon",
        "uu_tien": 5,
        "noi_dung": (
            "Dưới tác dụng xúc tác của axit H2SO4 đặc, nhiệt độ quyết định hướng phản ứng của ancol no đơn chức:\n"
            "1. Ở 140°C: Tách nước liên phân tử tạo ETE:\n"
            "2 R-OH →(H2SO4 đặc, 140°C) R-O-R + H2O.\n"
            "- Nếu đun nóng hỗn hợp gồm n ancol khác nhau ở 140°C, số ete tối đa thu được là: N = n(n + 1) / 2.\n"
            "- Ví dụ: Hỗn hợp CH3OH và C2H5OH tạo 3 ete: CH3OCH3, C2H5OC2H5, CH3OC2H5.\n"
            "2. Ở 170°C: Tách nước nội phân tử tạo ANKEN:\n"
            "CnH2n+1OH →(H2SO4 đặc, 170°C) CnH2n + H2O (điều kiện n ≥ 2).\n"
            "- Tuân theo quy tắc Zaitsev nếu là ancol bất đối xứng.\n"
            "- Riêng CH3OH chỉ tạo ete ở 140°C, không có phản ứng tách nước tạo anken vì chỉ có 1 C."
        )
    },
    {
        "tieu_de": "Phản ứng oxi hoá mạch nhánh của Aren bằng KMnO4",
        "chu_de": "Aren, Benzen",
        "uu_tien": 4,
        "noi_dung": (
            "1. Tính trơ của vòng benzen:\n"
            "Benzen không phản ứng và không làm mất màu dung dịch KMnO4 ở cả nhiệt độ phòng và khi đun nóng.\n"
            "2. Oxi hoá các ankylbenzen:\n"
            "Các ankylbenzen (toluen, etylbenzen, cumen...) làm mất màu dung dịch thuốc tím KMnO4 khi đun nóng:\n"
            "- Mọi nguyên tử cacbon no ở vị trí α (liên kết trực tiếp với vòng benzen và còn liên kết C-H) đều bị oxi hoá cắt đứt mạch cacbon, "
            "tạo thành nhóm muối cacboxylat -COOK gắn vào vòng benzen.\n"
            "- Phương trình với toluen:\n"
            "  C6H5-CH3 + 2 KMnO4 →(t°) C6H5-COOK + 2 MnO2↓ (nâu đen) + KOH + H2O.\n"
            "  Sau đó axit hoá bằng HCl: C6H5-COOK + HCl → C6H5-COOH (axit benzoic kết tủa trắng) + KCl.\n"
            "- Ứng dụng: Dùng nhận biết benzen (không mất màu KMnO4 khi đun) và toluen (làm mất màu KMnO4 khi đun nóng)."
        )
    },
    {
        "tieu_de": "Quy tắc nhận biết các chất hữu cơ bằng phản ứng hoá học",
        "chu_de": "Đại cương hữu cơ",
        "uu_tien": 5,
        "noi_dung": (
            "Bảng tóm tắt các thuốc thử kinh điển dùng nhận biết chất trong chuỗi hữu cơ:\n"
            "1. Quỳ tím:\n"
            "- Đổi màu đỏ: Axit cacboxylic (RCOOH), axit sunfonic.\n"
            "- Đổi màu xanh: Amin no (R-NH2, R2NH...), dung dịch amoniac.\n"
            "- Không đổi màu: Ancol, phenol, anilin, este, glucozơ.\n"
            "2. Kim loại kiềm (Na):\n"
            "- Sủi bọt khí H2: Mọi chất có H linh động (-OH ancol, -OH phenol, -COOH axit).\n"
            "3. Dung dịch NaHCO3:\n"
            "- Sủi bọt khí CO2: Chỉ có axit cacboxylic RCOOH (RCOOH + NaHCO3 → RCOONa + CO2↑ + H2O).\n"
            "- Phenol và ancol không phản ứng.\n"
            "4. Nước Brom (dung dịch Br2):\n"
            "- Mất màu dung dịch: Hợp chất có nối đôi C=C (anken, đien, stiren), nối ba C≡C, anđehit (-CHO), glucozơ.\n"
            "- Xuất hiện kết tủa trắng: Phenol (kết tủa 2,4,6-tribromphenol) và Anilin (kết tủa 2,4,6-tribromanilin).\n"
            "5. Phức đồng Cu(OH)2:\n"
            "- Tạo dung dịch xanh lam đậm ở t° thường: Ancol đa chức có ≥ 2 nhóm -OH kề nhau (glixerol, etylen glicol, glucozơ, saccarozơ).\n"
            "- Tạo kết tủa đỏ gạch Cu2O khi đun nóng: Hợp chất có nhóm anđehit (-CHO), glucozơ."
        )
    },
    {
        "tieu_de": "Phản ứng trùng hợp và phản ứng trùng ngưng tạo Polime",
        "chu_de": "Polime, Đại cương hữu cơ",
        "uu_tien": 4,
        "noi_dung": (
            "1. Phản ứng trùng hợp:\n"
            "- Là quá trình kết hợp nhiều phân tử nhỏ (monome) giống nhau hay tương tự nhau thành phân tử rất lớn (polime).\n"
            "- Điều kiện cấu tạo của monome: Phân tử phải có liên kết bội (nối đôi C=C như etilen, vinyl clorua, buta-1,3-đien, isopren) "
            "hoặc vòng kém bền (caprolactam).\n"
            "- Ví dụ: n CH2=CH-Cl →(xt, t°, p) -(-CH2-CH(Cl)-)-n (poli(vinyl clorua) - PVC).\n"
            "2. Phản ứng trùng ngưng:\n"
            "- Là quá trình kết hợp nhiều monome thành polime đồng thời giải phóng các phân tử nhỏ khác (thường là H2O).\n"
            "- Điều kiện cấu tạo: Monome phải có ít nhất hai nhóm chức có khả năng phản ứng với nhau (như -COOH với -OH; -COOH với -NH2; -NH2 với -COOH).\n"
            "- Ví dụ: n H2N-[CH2]5-COOH →(t°) -(-NH-[CH2]5-CO-)-n (nilon-6) + n H2O."
        )
    },
    {
        "tieu_de": "Đặc trưng của este không no và este của phenol",
        "chu_de": "Este",
        "uu_tien": 5,
        "noi_dung": (
            "Khi thủy phân este trong môi trường kiềm (dung dịch NaOH, t°):\n"
            "1. Este thông thường: R-COO-R' + NaOH → R-COONa + R'-OH (tạo muối và ancol).\n"
            "2. Este có liên kết C=C đính trực tiếp vào nguyên tử O (este vinyl):\n"
            "- Dạng R-COO-CH=CH-R' + NaOH → R-COONa + R'-CH2-CHO (sinh ra anđehit do ancol không bền chuyển vị enol-xeto).\n"
            "  Ví dụ: CH3COOCH=CH2 + NaOH → CH3COONa + CH3CHO (anđehit axetic).\n"
            "- Dạng R-COO-C(CH3)=CH2 + NaOH → R-COONa + CH3-CO-CH3 (sinh ra xeton axeton).\n"
            "3. Este của phenol (dạng R-COO-C6H5):\n"
            "- Tác dụng với NaOH theo tỉ lệ mol 1 : 2:\n"
            "  R-COO-C6H5 + 2 NaOH →(t°) R-COONa + C6H5-ONa + H2O.\n"
            "- Sản phẩm sinh ra hai muối và nước, KHÔNG tạo ancol."
        )
    },
    {
        "tieu_de": "Quy luật tăng - giảm mạch cacbon trong Chuỗi phản ứng",
        "chu_de": "Đại cương hữu cơ",
        "uu_tien": 4,
        "noi_dung": (
            "Trong giải chuỗi phản ứng hoá hữu cơ, số nguyên tử cacbon có thể được điều chỉnh theo các quy luật sau:\n"
            "1. Phản ứng giữ nguyên mạch C: Oxi hoá ancol thành anđehit/axit, cộng H2/Br2/HX vào anken/ankin, este hoá và thuỷ phân este.\n"
            "2. Phản ứng tăng mạch C:\n"
            "- Từ C1 lên C2: Nung metan 1500°C làm lạnh nhanh: 2 CH4 → C2H2 + 3 H2.\n"
            "- Từ C2 lên C4: Đime hoá axetilen (xúc tác CuCl, NH4Cl, t°): 2 CH≡CH → CH≡C-CH=CH2 (vinylaxetilen).\n"
            "- Phản ứng Grignard: R-MgX + HCHO/R'CHO → ancol có mạch C dài hơn.\n"
            "- Phản ứng Wurtz: 2 R-X + 2 Na → R-R + 2 NaX (tăng gấp đôi mạch C).\n"
            "3. Phản ứng giảm mạch C:\n"
            "- Phản ứng cracking ankan: CnH2n+2 →(xt, t°) CxH2x+2 + CyH2y (x + y = n).\n"
            "- Phản ứng vôi tôi xút: R-COONa + NaOH →(CaO, t°) R-H + Na2CO3 (giảm đúng 1 nguyên tử C).\n"
            "- Oxi hoá bẻ gãy mạch nối đôi bằng KMnO4 đậm đặc / Ozon phân."
        )
    }
]

# ================================================================ TÀI LIỆU CHUYÊN ĐỀ
TAI_LIEU_CHUYEN_DE = {
    "ten": "Cẩm nang Toàn diện: Đại cương Hoá học Hữu cơ & Chuỗi phản ứng",
    "chu_de": "Đại cương hữu cơ",
    "co_loi_giai": True,
    "noi_dung": """CHUYÊN ĐỀ: ĐẠI CƯƠNG HOÁ HỌC HỮU CƠ & QUY TẮC GIẢI CHUỖI PHẢN ỨNG

Phần 1: Cấu tạo phân tử hợp chất hữu cơ & Sự lai hoá của Cacbon
Nguyên tử cacbon ở trạng thái kích thích có cấu hình 1s2 2s1 2px1 2py1 2pz1, có 4 electron độc thân và luôn thể hiện hoá trị IV trong hợp chất hữu cơ.
- Lai hoá sp3: 1 obitan s lai hoá với 3 obitan p tạo 4 obitan lai hoá hướng về 4 đỉnh của hình tứ diện đều, góc liên kết 109,5°. Đặc trưng cho liên kết đơn C-C trong ankan, xicloankan, ancol no.
- Lai hoá sp2: 1 obitan s lai hoá với 2 obitan p tạo 3 obitan lai hoá nằm trong một mặt phẳng, góc liên kết 120°. Obitan p còn lại tạo liên kết π. Đặc trưng cho anken, aren, anđehit, axit.
- Lai hoá sp: 1 obitan s lai hoá với 1 obitan p tạo 2 obitan lai hoá nằm thẳng hàng, góc liên kết 180°. Hai obitan p còn lại tạo 2 liên kết π. Đặc trưng cho ankin (C≡C), anlen.

Phần 2: Mối quan hệ chuyển hoá giữa các loại nhóm chức
Sơ đồ chuyển hoá tổng quát kinh điển:
Ankan →(cracking / đun nóng) Anken →(+ H2O / H+) Ancol →(+ CuO, t°) Anđehit →(+ O2 / xt) Axit cacboxylic →(+ Ancol / H2SO4 đặc) Este.
- Từ Metan (CH4): CH4 →(1500°C) C2H2 →(+ H2, Pd/PbCO3) C2H4 →(+ H2O) C2H5OH →(men giấm) CH3COOH.
- Chuỗi điều chế Axeton: C3H8 → C3H6 →(+ H2O/H+, sản phẩm chính Markovnikov) CH3-CH(OH)-CH3 →(+ CuO, t°) CH3-CO-CH3.
- Chuỗi Benzen: C6H6 →(+ Br2/Fe) C6H5Br →(+ NaOH đặc, 300°C, 200atm) C6H5ONa →(+ CO2 + H2O) C6H5OH (Phenol).

Phần 3: Các quy tắc chọn sản phẩm chính trong phản ứng hữu cơ
- Quy tắc Markovnikov: Áp dụng cho phản ứng cộng electrophin (AE) của HX vào liên kết bội bất đối xứng. H vào C nhiều H hơn, X vào C ít H hơn.
- Quy tắc Zaitsev: Áp dụng cho phản ứng tách (E) phân tử HX hoặc H2O từ dẫn xuất halogen hoặc ancol. Nhóm -X hoặc -OH ưu tiên tách cùng H ở C bậc cao hơn lân cận để tạo anken bền hơn có nhiều nhóm thế hơn quanh nối đôi.
- Quy tắc Holleman: Định hướng thế electrophin (SE) trên vòng benzen. Nhóm hoạt hoá đẩy e (-OH, -NH2, -CH3) định hướng ortho/para; nhóm hút e (-NO2, -COOH, -CHO) định hướng meta.
- Cơ chế gốc tự do SR: Thế halogen vào ankan ưu tiên bứt H ở C bậc cao (bậc 3 > 2 > 1). Brom có tính chọn lọc sản phẩm cao nhất.

Phần 4: Phương pháp bảo toàn và nhận biết cấu tạo
- Độ bất bão hoà k = số pi + số vòng = (2C + 2 - H + N - X) / 2.
- Bảo toàn liên kết pi: n_pi = k * n_X = n_Br2 phản ứng (hoặc n_H2 phản ứng).
- Nhận biết anđehit: Tráng bạc AgNO3/NH3 sinh Ag sáng bóng, hoặc đun Cu(OH)2/NaOH sinh kết tủa đỏ gạch Cu2O.
- Nhận biết phenol và anilin: Nhỏ nước brom thấy kết tủa trắng 2,4,6-tribromphenol và 2,4,6-tribromanilin.
- Nhận biết ancol đa chức kề nhau (glixerol, etylen glicol): Hoà tan kết tủa Cu(OH)2 tạo dung dịch xanh lam đậm trong suốt.
"""
}

# ================================================================ 4 BÀI MẪU CHUẨN KINH ĐIỂN
BAI_MAU_CHUAN = [
    {
        "de": "CH4 → C2H2 → C2H4 → C2H5OH → CH3CHO → CH3COOH → CH3COOC2H5",
        "chu_de": "Chuỗi hidrocacbon, Este",
        "loi_giai": {
            "chuoi": [
                {"ki_hieu": "CH4", "ctct": "CH4", "smiles": "C", "ten": "Metan"},
                {"ki_hieu": "C2H2", "ctct": "CH≡CH", "smiles": "C#C", "ten": "Axetilen"},
                {"ki_hieu": "C2H4", "ctct": "CH2=CH2", "smiles": "C=C", "ten": "Etilen"},
                {"ki_hieu": "C2H5OH", "ctct": "CH3-CH2-OH", "smiles": "CCO", "ten": "Etanol"},
                {"ki_hieu": "CH3CHO", "ctct": "CH3-CH=O", "smiles": "CC=O", "ten": "Axetanđehit"},
                {"ki_hieu": "CH3COOH", "ctct": "CH3-COOH", "smiles": "CC(=O)O", "ten": "Axit axetic"},
                {"ki_hieu": "CH3COOC2H5", "ctct": "CH3-COO-CH2-CH3", "smiles": "CCOC(=O)C", "ten": "Etyl axetat"}
            ],
            "buoc": [
                {"tu": "CH4", "den": "C2H2", "phan_ung": "Nhiệt phân metan ở 1500°C làm lạnh nhanh", "dieu_kien": "1500°C, làm lạnh nhanh", "phuong_trinh": "2 CH4 →(1500°C, lln) C2H2 + 3 H2"},
                {"tu": "C2H2", "den": "C2H4", "phan_ung": "Hiđro hoá chọn lọc ankin thành anken", "dieu_kien": "Pd/PbCO3, t°", "phuong_trinh": "CH≡CH + H2 →(Pd/PbCO3, t°) CH2=CH2"},
                {"tu": "C2H4", "den": "C2H5OH", "phan_ung": "Hiđrat hoá etilen trong môi trường axit", "dieu_kien": "H2SO4 loãng, t°", "phuong_trinh": "CH2=CH2 + H2O →(H+, t°) CH3-CH2-OH"},
                {"tu": "C2H5OH", "den": "CH3CHO", "phan_ung": "Oxi hoá ancol bậc 1 bằng CuO đun nóng", "dieu_kien": "CuO, t°", "phuong_trinh": "CH3-CH2-OH + CuO →(t°) CH3-CHO + Cu + H2O"},
                {"tu": "CH3CHO", "den": "CH3COOH", "phan_ung": "Oxi hoá anđehit bằng oxi không khí có xúc tác", "dieu_kien": "Mn2+, t°", "phuong_trinh": "2 CH3-CHO + O2 →(Mn2+, t°) 2 CH3-COOH"},
                {"tu": "CH3COOH", "den": "CH3COOC2H5", "phan_ung": "Phản ứng este hoá Fischer với etanol", "dieu_kien": "H2SO4 đặc, t°", "phuong_trinh": "CH3-COOH + C2H5-OH ⇌(H2SO4 đặc, t°) CH3-COO-C2H5 + H2O"}
            ],
            "luu_y": [
                "Bước 2 bắt buộc dùng xúc tác Lindlar (Pd/PbCO3) để dừng lại ở anken etilen, nếu dùng Ni/t° sẽ bị hiđro hoá hoàn toàn về etan C2H6.",
                "Bước cuối là phản ứng thuận nghịch hai chiều có H2SO4 đặc làm xúc tác và hút ẩm."
            ],
            "dap_an": "Chuỗi chuyển hoá ankan kinh điển qua ankin, anken, ancol, anđehit, axit và este etyl axetat."
        }
    },
    {
        "de": "CaC2 → C2H2 → C4H4 → C4H6 → Cao su Buna",
        "chu_de": "Ankin, Polime",
        "loi_giai": {
            "chuoi": [
                {"ki_hieu": "CaC2", "ctct": "CaC2", "smiles": "[Ca+2].[C-]#C", "ten": "Canxi cacbua (đất đèn)"},
                {"ki_hieu": "C2H2", "ctct": "CH≡CH", "smiles": "C#C", "ten": "Axetilen"},
                {"ki_hieu": "C4H4", "ctct": "CH≡C-CH=CH2", "smiles": "C#CC=C", "ten": "Vinylaxetilen"},
                {"ki_hieu": "C4H6", "ctct": "CH2=CH-CH=CH2", "smiles": "C=CC=C", "ten": "Buta-1,3-đien"},
                {"ki_hieu": "Cao su Buna", "ctct": "-(-CH2-CH=CH-CH2-)-n", "smiles": "C=C", "ten": "Polibutadien"}
            ],
            "buoc": [
                {"tu": "CaC2", "den": "C2H2", "phan_ung": "Thuỷ phân đất đèn trong nước", "dieu_kien": "H2O, nhiệt độ phòng", "phuong_trinh": "CaC2 + 2 H2O → CH≡CH + Ca(OH)2"},
                {"tu": "C2H2", "den": "C4H4", "phan_ung": "Đime hoá axetilen", "dieu_kien": "CuCl, NH4Cl, 100°C", "phuong_trinh": "2 CH≡CH →(CuCl, NH4Cl, 100°C) CH≡C-CH=CH2"},
                {"tu": "C4H4", "den": "C4H6", "phan_ung": "Hiđro hoá có chọn lọc liên kết ba của vinylaxetilen", "dieu_kien": "Pd/PbCO3, t°", "phuong_trinh": "CH≡C-CH=CH2 + H2 →(Pd/PbCO3, t°) CH2=CH-CH=CH2"},
                {"tu": "C4H6", "den": "Cao su Buna", "phan_ung": "Trùng hợp buta-1,3-đien theo kiểu cộng 1,4", "dieu_kien": "Na, t°, p", "phuong_trinh": "n CH2=CH-CH=CH2 →(Na, t°, p) -(-CH2-CH=CH-CH2-)-n"}
            ],
            "luu_y": [
                "Phản ứng trùng hợp buta-1,3-đien dùng xúc tác kim loại Na tạo polime có tên gọi thương mại là Cao su Buna."
            ],
            "dap_an": "Chuỗi điều chế cao su buna nhân tạo từ nguyên liệu đất đèn CaC2."
        }
    },
    {
        "de": "CH3-CH2-CH2-OH → CH3-CH=CH2 → CH3-CH(Br)-CH3 → CH3-CH(OH)-CH3",
        "chu_de": "Ancol, Anken, Dẫn xuất halogen",
        "loi_giai": {
            "chuoi": [
                {"ki_hieu": "CH3-CH2-CH2-OH", "ctct": "CH3-CH2-CH2-OH", "smiles": "CCCO", "ten": "Propan-1-ol"},
                {"ki_hieu": "CH3-CH=CH2", "ctct": "CH3-CH=CH2", "smiles": "CC=C", "ten": "Propen"},
                {"ki_hieu": "CH3-CH(Br)-CH3", "ctct": "CH3-CH(Br)-CH3", "smiles": "CC(Br)C", "ten": "2-brompropan"},
                {"ki_hieu": "CH3-CH(OH)-CH3", "ctct": "CH3-CH(OH)-CH3", "smiles": "CC(O)C", "ten": "Propan-2-ol"}
            ],
            "buoc": [
                {"tu": "CH3-CH2-CH2-OH", "den": "CH3-CH=CH2", "phan_ung": "Tách nước ancol propan-1-ol tạo anken", "dieu_kien": "H2SO4 đặc, 170°C", "phuong_trinh": "CH3-CH2-CH2-OH →(H2SO4 đặc, 170°C) CH3-CH=CH2 + H2O"},
                {"tu": "CH3-CH=CH2", "den": "CH3-CH(Br)-CH3", "phan_ung": "Cộng HBr vào propen theo quy tắc Markovnikov", "dieu_kien": "Khí HBr", "phuong_trinh": "CH3-CH=CH2 + HBr → CH3-CH(Br)-CH3"},
                {"tu": "CH3-CH(Br)-CH3", "den": "CH3-CH(OH)-CH3", "phan_ung": "Thuỷ phân dẫn xuất halogen trong dung dịch kiềm", "dieu_kien": "NaOH loãng, t°", "phuong_trinh": "CH3-CH(Br)-CH3 + NaOH →(t°) CH3-CH(OH)-CH3 + NaBr"}
            ],
            "luu_y": [
                "Đây là phương pháp kinh điển chuyển đổi ancol bậc 1 (propan-1-ol) thành ancol bậc 2 (propan-2-ol) bằng cách tách nước tạo anken rồi cộng lại theo quy tắc Markovnikov."
            ],
            "dap_an": "Chuỗi chuyển đổi vị trí nhóm chức -OH từ đầu mạch (bậc 1) vào giữa mạch (bậc 2)."
        }
    },
    {
        "de": "C6H6 → C6H5-CH3 → C6H5-COOH → C6H5-COOCH3",
        "chu_de": "Aren, Axit cacboxylic, Este",
        "loi_giai": {
            "chuoi": [
                {"ki_hieu": "C6H6", "ctct": "C6H6", "smiles": "c1ccccc1", "ten": "Benzen"},
                {"ki_hieu": "C6H5-CH3", "ctct": "C6H5-CH3", "smiles": "Cc1ccccc1", "ten": "Toluen"},
                {"ki_hieu": "C6H5-COOH", "ctct": "C6H5-COOH", "smiles": "O=C(O)c1ccccc1", "ten": "Axit benzoic"},
                {"ki_hieu": "C6H5-COOCH3", "ctct": "C6H5-COO-CH3", "smiles": "COC(=O)c1ccccc1", "ten": "Metyl benzoat"}
            ],
            "buoc": [
                {"tu": "C6H6", "den": "C6H5-CH3", "phan_ung": "Ankyl hoá Friedel-Crafts benzen với metyl clorua", "dieu_kien": "AlCl3 khan, t°", "phuong_trinh": "C6H6 + CH3Cl →(AlCl3, t°) C6H5-CH3 + HCl"},
                {"tu": "C6H5-CH3", "den": "C6H5-COOH", "phan_ung": "Oxi hoá mạch nhánh aren bằng dung dịch KMnO4 đun nóng", "dieu_kien": "KMnO4, t°; sau đó + HCl", "phuong_trinh": "C6H5-CH3 + 2 KMnO4 →(t°) C6H5-COOK + 2 MnO2↓ + KOH + H2O; C6H5-COOK + HCl → C6H5-COOH + KCl"},
                {"tu": "C6H5-COOH", "den": "C6H5-COOCH3", "phan_ung": "Este hoá axit benzoic với metanol", "dieu_kien": "H2SO4 đặc, t°", "phuong_trinh": "C6H5-COOH + CH3OH ⇌(H2SO4 đặc, t°) C6H5-COOCH3 + H2O"}
            ],
            "luu_y": [
                "Benzen không bị oxi hoá bởi KMnO4, nhưng nhóm metyl trên toluen thì bị oxi hoá hoàn toàn về nhóm -COOH."
            ],
            "dap_an": "Chuỗi chuyển hoá nhân thơm từ benzen thành este thơm metyl benzoat."
        }
    }
]

# ================================================================ HÀM NẠP TỔNG THỂ
def nap_tat_ca():
    print("=" * 60)
    print("BẮT ĐẦU NẠP TRỌN BỘ KIẾN THỨC ĐẠI CƯƠNG HOÁ HỮU CƠ CHO AI RIÊNG")
    print("=" * 60)

    # 1. Nạp quy tắc cốt lõi
    print("\n--- 1. NẠP 25 QUY TẮC CỐT LÕI ---")
    dem_kt = 0
    ds_hien_tai = {d["tieu_de"]: d["id"] for d in kho.ds_kien_thuc()}
    for kt in KIEN_THUC_COT_LOI:
        if kt["tieu_de"] in ds_hien_tai:
            # Cập nhật nội dung đầy đủ mới nhất
            kho.sua_kien_thuc(
                kid=ds_hien_tai[kt["tieu_de"]],
                tieu_de=kt["tieu_de"],
                noi_dung=kt["noi_dung"],
                chu_de=kt["chu_de"],
                uu_tien=kt["uu_tien"],
                bat=True
            )
            print(f"✓ Đã cập nhật quy tắc #{ds_hien_tai[kt['tieu_de']]}: {kt['tieu_de']}")
        else:
            kid = kho.tao_kien_thuc(
                tieu_de=kt["tieu_de"],
                noi_dung=kt["noi_dung"],
                chu_de=kt["chu_de"],
                uu_tien=kt["uu_tien"],
                bat=True
            )
            print(f"+ Đã tạo mới quy tắc #{kid}: {kt['tieu_de']}")
            dem_kt += 1

    # 2. Nạp tài liệu chuyên đề
    print("\n--- 2. NẠP TÀI LIỆU CHUYÊN ĐỀ ĐẠI CƯƠNG ---")
    ds_tl = {t["ten"]: t["id"] for t in kho.ds_tai_lieu()}
    ten_tl = TAI_LIEU_CHUYEN_DE["ten"]
    if ten_tl in ds_tl:
        print(f"✓ Tài liệu «{ten_tl}» đã tồn tại (ID: {ds_tl[ten_tl]}).")
    else:
        du_lieu = TAI_LIEU_CHUYEN_DE["noi_dung"].encode("utf-8")
        tid = kho.tao_tai_lieu(
            ten=ten_tl,
            loai="van_ban",
            nguon="chuyen_de_dai_cuong.txt",
            tep="",
            chu_de=TAI_LIEU_CHUYEN_DE["chu_de"],
            co_loi_giai=TAI_LIEU_CHUYEN_DE["co_loi_giai"],
            kich_thuoc=len(du_lieu)
        )
        # Lưu file tạm và đánh thức xử lý
        p = xu_ly.thu_muc_tep() / f"{tid}.txt"
        p.write_bytes(du_lieu)
        kho.cap_nhat_tai_lieu(tid, tep=str(p))
        print(f"+ Đã tạo tài liệu chuyên đề #{tid}: {ten_tl}")

    # 3. Nạp bài mẫu kinh điển vào thư viện
    print("\n--- 3. NẠP BÀI MẪU VÀO THƯ VIỆN LỜI GIẢI ---")
    dem_bm = 0
    with kho.db.ket_noi() as con:
        ds_de_khoa = {r[0] for r in con.execute("SELECT de_khoa FROM ar_bai_mau").fetchall()}

    for bm in BAI_MAU_CHUAN:
        khoa = kho.khoa_de(bm["de"])
        if khoa in ds_de_khoa:
            print(f"✓ Đã có bài mẫu: {bm['de']}")
            continue
        bid = kho.tao_bai_mau(
            de=bm["de"],
            loi_giai=bm["loi_giai"],
            chu_de=bm["chu_de"],
            trang_thai="duyet",
            nguon="tay",
            rdkit_ok=True
        )
        print(f"+ Đã thêm bài mẫu #{bid}: {bm['de']}")
        dem_bm += 1

    # 4. Đánh thức tiến trình nền để tách đoạn và nhúng vector
    print("\n--- 4. ĐÁNH THỨC WORKER TÁCH ĐOẠN & NHÚNG TRI THỨC ---")
    xu_ly.xu_ly_het()
    print("✓ Toàn bộ tài liệu và quy tắc đã được xử lý xong và đồng bộ vào CSDL Vector / FTS5!")

    print("\n" + "=" * 60)
    print("HOÀN TẤT DẠY ĐẦY ĐỦ KIẾN THỨC ĐẠI CƯƠNG HỮU CƠ CHO AI RIÊNG!")
    print("=" * 60)

if __name__ == "__main__":
    nap_tat_ca()
