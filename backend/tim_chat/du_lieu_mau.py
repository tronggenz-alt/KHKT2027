"""Bộ 10 đề bài mẫu tìm cấu tạo chất rải rác dữ kiện (chuẩn THPT & HSG)."""
from __future__ import annotations

from typing import Any, Dict, List

DE_MAU_TIM_CHAT: List[Dict[str, Any]] = [
    {
        "id": "bai_01",
        "tieu_de": "Este đa chức C6H8O6 (Đề HSG / Tốt nghiệp THPT)",
        "de_bai": "Hợp chất hữu cơ no, mạch hở E có CTPT C6H8O6. Cho các phản ứng sau theo đúng tỉ lệ mol:\n(1) E + 3NaOH -> X + Y + Z + H2O (đun nóng)\n(2) X + H2SO4 -> T + Na2SO4\n(3) Y + HCl -> F + NaCl\nBiết MZ < MY < MX. T và F là các hợp chất hữu cơ.",
        "cau_hoi": "Xác định công thức cấu tạo của E, X, Y, Z, T, F.",
        "trac_nghiem": "A. E là HOOC-CH2-COO-CH2-CH2-OOCH\nB. E là CH3-OOC-COO-CH2-CH2-OOCH\nC. E là (COOCH3)2\nD. E là C3H5(OOCH)3",
        "dap_an_dung": "A",
        "chat_mong_doi": {
            "E": "HOOC-CH2-COO-CH2-CH2-OOCH",
            "X": "NaOOC-CH2-COONa",
            "Y": "HCOONa",
            "Z": "HO-CH2-CH2-OH",
            "T": "HOOC-CH2-COOH",
            "F": "HCOOH",
        },
    },
    {
        "id": "bai_02",
        "tieu_de": "Đốt cháy và thủy phân este đơn chức mạch hở",
        "de_bai": "Đốt cháy hoàn toàn 4,4 gam một este no, đơn chức, mạch hở X thu được 4,48 lít khí CO2 (ở đktc) và 3,6 gam nước. Mặt khác, cho 4,4 gam X tác dụng vừa đủ với dung dịch NaOH, đun nóng, thu được 4,1 gam muối Y và một ancol Z. Khối lượng mol của ancol Z đo được bằng 46 g/mol.",
        "cau_hoi": "Xác định công thức cấu tạo và tên gọi của este X, muối Y và ancol Z.",
        "trac_nghiem": "A. Etyl axetat (CH3COOC2H5)\nB. Metyl propionat (C2H5COOCH3)\nC. Propyl fomat (HCOOC3H7)\nD. Etyl fomat (HCOOC2H5)",
        "dap_an_dung": "A",
        "chat_mong_doi": {
            "X": "CH3COOC2H5",
            "Y": "CH3COONa",
            "Z": "C2H5OH",
        },
    },
    {
        "id": "bai_03",
        "tieu_de": "Este không no C4H6O2 tráng bạc 2 lần",
        "de_bai": "Hợp chất hữu cơ X mạch hở có CTPT C4H6O2. Cho X tác dụng với dung dịch NaOH đun nóng sinh ra muối Y và chất hữu cơ Z. Cho Z tác dụng với lượng dư dung dịch AgNO3 trong NH3 thu được kết tủa Ag. Cho muối Y tác dụng với dung dịch H2SO4 loãng thu được axit cacboxylic T cũng có khả năng tham gia phản ứng tráng bạc.",
        "cau_hoi": "Biện luận xác định công thức cấu tạo của X, Y, Z, T.",
        "trac_nghiem": "A. HCOOCH=CH-CH3\nB. CH3COOCH=CH2\nC. CH2=CH-COOCH3\nD. HCOOCH2-CH=CH2",
        "dap_an_dung": "A",
        "chat_mong_doi": {
            "X": "HCOOCH=CH-CH3",
            "Y": "HCOONa",
            "Z": "CH3CH2CHO",
            "T": "HCOOH",
        },
    },
    {
        "id": "bai_04",
        "tieu_de": "Este hai chức C4H6O4 tạo từ một axit và một ancol",
        "de_bai": "Este hai chức mạch hở X có CTPT C4H6O4. Cho X tác dụng hoàn toàn với dung dịch NaOH đun nóng, thu được muối của một axit cacboxylic Y duy nhất và một ancol Z. Axit Y có khả năng tham gia phản ứng tráng bạc.",
        "cau_hoi": "Xác định công thức cấu tạo của X, Y và Z.",
        "trac_nghiem": "A. (HCOO)2C2H4 (etylen đifomat)\nB. CH3OOC-COOCH3 (đimetyl oxalat)\nC. CH2(COOCH3)2\nD. HOOC-CH2-COOCH3",
        "dap_an_dung": "A",
        "chat_mong_doi": {
            "X": "(HCOO)2C2H4",
            "Y": "HCOOH",
            "Z": "C2H4(OH)2",
        },
    },
    {
        "id": "bai_05",
        "tieu_de": "Amino axit lưỡng tính từ thành phần % khối lượng",
        "de_bai": "Hợp chất hữu cơ X chứa các nguyên tố C, H, O, N có thành phần phần trăm khối lượng: %C = 32,00%, %H = 6,67%, %N = 18,67%, còn lại là oxi. Phân tử khối của X bằng 75. Chất X có tính lưỡng tính, tác dụng được với cả dung dịch HCl và dung dịch NaOH.",
        "cau_hoi": "Xác định CTPT và CTCT của chất X.",
        "trac_nghiem": "A. H2N-CH2-COOH (Glyxin)\nB. CH3-CH(NH2)-COOH (Alanin)\nC. H2N-CH2-CH2-COOH\nD. CH3-NH-CH2-COOH",
        "dap_an_dung": "A",
        "chat_mong_doi": {
            "X": "H2N-CH2-COOH",
        },
    },
    {
        "id": "bai_06",
        "tieu_de": "Đồng phân đơn chức C3H6O2 không tráng bạc",
        "de_bai": "Hợp chất hữu cơ X đơn chức có CTPT C3H6O2. Biết X phản ứng được với dung dịch NaOH khi đun nóng, nhưng không phản ứng với kim loại Na ở điều kiện thường và không có phản ứng tráng bạc với thuốc thử Tollens.",
        "cau_hoi": "Xác định công thức cấu tạo của X.",
        "trac_nghiem": "A. CH3COOCH3 (Metyl axetat)\nB. HCOOC2H5 (Etyl fomat)\nC. C2H5COOH (Axit propionic)\nD. HO-CH2-CH2-CHO",
        "dap_an_dung": "A",
        "chat_mong_doi": {
            "X": "CH3COOCH3",
        },
    },
    {
        "id": "bai_07",
        "tieu_de": "Ancol thơm C7H8O không tan trong kiềm",
        "de_bai": "Hợp chất thơm X có CTPT C7H8O. Chất X tác dụng được với kim loại Na giải phóng khí H2, nhưng không tác dụng với dung dịch NaOH loãng. Khi đun nóng X với CuO ở nhiệt độ cao thu được anđehit Y có mùi hạnh nhân đặc trưng.",
        "cau_hoi": "Xác định công thức cấu tạo của X và Y.",
        "trac_nghiem": "A. C6H5-CH2-OH (Ancol benzylic)\nB. 2-metylphenol (o-crezol)\nC. 4-metylphenol (p-crezol)\nD. C6H5-O-CH3 (Anisol)",
        "dap_an_dung": "A",
        "chat_mong_doi": {
            "X": "C6H5CH2OH",
            "Y": "C6H5CHO",
        },
    },
    {
        "id": "bai_08",
        "tieu_de": "Este đa chức C7H12O4 tạo 2 ancol cùng C",
        "de_bai": "Hợp chất hữu cơ no, mạch hở X có CTPT C7H12O4. Thủy phân hoàn toàn X trong dung dịch NaOH đun nóng, thu được muối natri của axit cacboxylic hai chức Y và hai ancol đơn chức no Z, T có cùng số nguyên tử cacbon trong phân tử. Biết khối lượng mol MZ < MT.",
        "cau_hoi": "Xác định công thức cấu tạo của X, Y, Z, T.",
        "trac_nghiem": "A. CH3-CH2-OOC-CH2-COO-CH(CH3)2\nB. CH3-OOC-CH2-CH2-COO-C3H7\nC. C2H5-OOC-COO-C4H9\nD. CH3-OOC-(CH2)3-COOCH3",
        "dap_an_dung": "A",
        "chat_mong_doi": {
            "X": "C2H5OOC-CH2-COOCH(CH3)2",
            "Y": "CH2(COOH)2",
            "Z": "C2H5OH",
            "T": "CH3CH(OH)CH3",
        },
    },
    {
        "id": "bai_09",
        "tieu_de": "Este của alpha-amino axit tác dụng NaOH",
        "de_bai": "Cho 0,1 mol chất hữu cơ X (phân tử chứa C, H, O, N) phản ứng hoàn toàn với dung dịch NaOH vừa đủ, đun nóng, thu được 4,6 gam ancol etylic no đơn chức Y và dung dịch chứa 9,7 gam muối natri của một alpha-amino axit Z có mạch cacbon không phân nhánh.",
        "cau_hoi": "Xác định CTPT và CTCT của chất X.",
        "trac_nghiem": "A. H2N-CH2-COOC2H5 (Etyl glyxinat)\nB. H2N-CH(CH3)-COOCH3\nC. CH3-NH-CH2-COOC2H5\nD. H2N-(CH2)2-COOC2H5",
        "dap_an_dung": "A",
        "chat_mong_doi": {
            "X": "H2N-CH2-COOC2H5",
            "Y": "C2H5OH",
            "Z": "H2N-CH2-COOH",
        },
    },
    {
        "id": "bai_10",
        "tieu_de": "Muối amoni của axit cacboxylic đơn chức",
        "de_bai": "Hợp chất hữu cơ X có CTPT C3H9O2N. Cho X tác dụng hoàn toàn với dung dịch NaOH đun nóng, thu được dung dịch chứa muối natri hữu cơ Y và giải phóng một chất khí Z có mùi khai, làm xanh giấy quỳ tím ẩm. Biết tỉ khối của Z so với H2 bằng 15,5.",
        "cau_hoi": "Xác định công thức cấu tạo của X, Y và Z.",
        "trac_nghiem": "A. CH3COONH3CH3\nB. HCOONH3CH2CH3\nC. C2H5COONH4\nD. HCOONH2(CH3)2",
        "dap_an_dung": "A",
        "chat_mong_doi": {
            "X": "CH3COONH3CH3",
            "Y": "CH3COONa",
            "Z": "CH3NH2",
        },
    },
]
