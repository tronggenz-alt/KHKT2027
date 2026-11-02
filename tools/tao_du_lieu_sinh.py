"""Tạo 5 file dữ liệu sinh học data/sinh/*.json:
- tieu_ban.json
- chung_vi_sinh.json
- moi_truong.json
- sinh_pham_mac_dinh.json
- bai_thi_nghiem.json
"""

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DATA_SINH = ROOT / "data" / "sinh"


def tao_tieu_ban():
    data = {
        "phien_ban": 1,
        "tieu_ban": [
            # Tự làm (tu_lam)
            {
                "ma": "bieu_bi_hanh",
                "ten": "Biểu bì vảy hành tím không nhuộm",
                "loai": "tu_lam",
                "nhuom": "khong",
                "kich_thuoc_te_bao_um": [150, 300],
                "bo_phan_chu_thich": [
                    {"ten": "Vách tế bào (Cell wall)", "x": 0.25, "y": 0.35},
                    {"ten": "Không bào trung tâm chứa sắc tố anthocyanin", "x": 0.52, "y": 0.48},
                    {"ten": "Nhân tế bào (Nucleus)", "x": 0.78, "y": 0.32}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/4-3-eukaryotic-cells"
            },
            {
                "ma": "bieu_bi_hanh_xanh",
                "ten": "Biểu bì vảy hành nhuộm xanh methylen",
                "loai": "tu_lam",
                "nhuom": "xanh_methylen",
                "kich_thuoc_te_bao_um": [150, 300],
                "bo_phan_chu_thich": [
                    {"ten": "Nhân tế bào bắt màu xanh đậm", "x": 0.45, "y": 0.40},
                    {"ten": "Hạt nhân (Nucleolus)", "x": 0.48, "y": 0.42},
                    {"ten": "Tế bào chất (Cytoplasm)", "x": 0.30, "y": 0.55}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/4-3-eukaryotic-cells"
            },
            {
                "ma": "thai_lai_tia_binh_thuong",
                "ten": "Biểu bì lá thài lài tía bình thường (trương nước)",
                "loai": "tu_lam",
                "nhuom": "khong",
                "kich_thuoc_te_bao_um": [40, 90],
                "bo_phan_chu_thich": [
                    {"ten": "Tế bào biểu bì chứa không bào màu tím", "x": 0.35, "y": 0.45},
                    {"ten": "Khí khổng gồm 2 tế bào hình hạt đậu", "x": 0.65, "y": 0.50},
                    {"ten": "Lục lạp trong tế bào khí khổng", "x": 0.62, "y": 0.44}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/30-2-stems"
            },
            {
                "ma": "thai_lai_tia_co_nguyen_sinh",
                "ten": "Biểu bì lá thài lài tía co nguyên sinh (trong NaCl 10%)",
                "loai": "tu_lam",
                "nhuom": "khong",
                "kich_thuoc_te_bao_um": [40, 90],
                "bo_phan_chu_thich": [
                    {"ten": "Khối chất nguyên sinh co cụm tách khỏi vách", "x": 0.50, "y": 0.50},
                    {"ten": "Khoảng trống giữa vách và màng sinh chất", "x": 0.28, "y": 0.38},
                    {"ten": "Vách tế bào cellulose giữ nguyên hình dạng", "x": 0.20, "y": 0.25}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/5-2-passive-transport"
            },
            {
                "ma": "thai_lai_tia_phan_co",
                "ten": "Biểu bì lá thài lài tía phản co nguyên sinh (sau khi nhỏ nước cất)",
                "loai": "tu_lam",
                "nhuom": "khong",
                "kich_thuoc_te_bao_um": [40, 90],
                "bo_phan_chu_thich": [
                    {"ten": "Khối nguyên sinh chất hút nước nở căng áp sát vách", "x": 0.48, "y": 0.48},
                    {"ten": "Không bào tím phục hồi thể tích ban đầu", "x": 0.55, "y": 0.52}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/5-2-passive-transport"
            },
            {
                "ma": "khi_khong_mo",
                "ten": "Khí khổng mở (ở ngoài sáng)",
                "loai": "tu_lam",
                "nhuom": "khong",
                "kich_thuoc_te_bao_um": [30, 50],
                "bo_phan_chu_thich": [
                    {"ten": "Lỗ khí mở rộng (Stomatal pore)", "x": 0.50, "y": 0.50},
                    {"ten": "Vách trong dày của tế bào hạt đậu cong lõm", "x": 0.45, "y": 0.42},
                    {"ten": "Vách ngoài mỏng dãn dài ra ngoài", "x": 0.32, "y": 0.55}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/30-2-stems"
            },
            {
                "ma": "khi_khong_dong",
                "ten": "Khí khổng đóng (trong bóng tối hoặc thiếu nước)",
                "loai": "tu_lam",
                "nhuom": "khong",
                "kich_thuoc_te_bao_um": [30, 50],
                "bo_phan_chu_thich": [
                    {"ten": "Khe khí khổng khép kín", "x": 0.50, "y": 0.50},
                    {"ten": "Tế bào hình hạt đậu mất nước xẹp thẳng", "x": 0.42, "y": 0.45}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/30-2-stems"
            },
            {
                "ma": "niem_mac_mieng",
                "ten": "Tế bào niêm mạc miệng người nhuộm xanh methylen",
                "loai": "tu_lam",
                "nhuom": "xanh_methylen",
                "kich_thuoc_te_bao_um": [50, 75],
                "bo_phan_chu_thich": [
                    {"ten": "Nhân tế bào biểu mô dẹt bắt màu xanh thẫm", "x": 0.50, "y": 0.48},
                    {"ten": "Màng sinh chất mềm dẻo không có vách", "x": 0.28, "y": 0.35},
                    {"ten": "Tế bào chất bắt màu xanh nhạt", "x": 0.65, "y": 0.58}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/4-2-prokaryotic-cells"
            },
            {
                "ma": "nam_men",
                "ten": "Nấm men bánh mì (Saccharomyces cerevisiae) nảy chồi",
                "loai": "tu_lam",
                "nhuom": "xanh_methylen",
                "kich_thuoc_te_bao_um": [5, 10],
                "bo_phan_chu_thich": [
                    {"ten": "Tế bào nấm men hình cầu / bầu dục", "x": 0.40, "y": 0.45},
                    {"ten": "Chồi non đang mọc (Budding)", "x": 0.58, "y": 0.38},
                    {"ten": "Sẹo nảy chồi (Bud scar)", "x": 0.35, "y": 0.52}
                ],
                "nguon": "https://www.atcc.org/products/204508"
            },
            {
                "ma": "vi_khuan_sua_chua",
                "ten": "Vi khuẩn sữa chua (Lactobacillus & Streptococcus)",
                "loai": "tu_lam",
                "nhuom": "gram",
                "kich_thuoc_te_bao_um": [0.8, 3.5],
                "bo_phan_chu_thich": [
                    {"ten": "Trực khuẩn Lactobacillus bulgaricus màu tím Gram+", "x": 0.35, "y": 0.45},
                    {"ten": "Chuỗi liên cầu Streptococcus thermophilus Gram+", "x": 0.68, "y": 0.52}
                ],
                "nguon": "https://openstax.org/books/microbiology/pages/22-2-microbial-diseases-of-the-digestive-system"
            },
            {
                "ma": "e_coli_gram",
                "ten": "Escherichia coli K-12 nhuộm Gram (Gram âm)",
                "loai": "tu_lam",
                "nhuom": "gram",
                "kich_thuoc_te_bao_um": [1.0, 2.5],
                "bo_phan_chu_thich": [
                    {"ten": "Trực khuẩn ngắn bắt màu hồng đỏ Safranin", "x": 0.50, "y": 0.48},
                    {"ten": "Vách tế bào peptidoglycan mỏng kèm màng ngoài", "x": 0.45, "y": 0.52}
                ],
                "nguon": "https://openstax.org/books/microbiology/pages/3-3-unique-characteristics-of-prokaryotic-cells"
            },
            {
                "ma": "b_subtilis_gram",
                "ten": "Bacillus subtilis nhuộm Gram (Gram dương)",
                "loai": "tu_lam",
                "nhuom": "gram",
                "kich_thuoc_te_bao_um": [2.0, 5.0],
                "bo_phan_chu_thich": [
                    {"ten": "Trực khuẩn lớn bắt màu tím Crystal Violet đậm", "x": 0.40, "y": 0.45},
                    {"ten": "Nội bào tử chịu nhiệt hình bầu dục sáng ở giữa", "x": 0.42, "y": 0.48}
                ],
                "nguon": "https://openstax.org/books/microbiology/pages/3-3-unique-characteristics-of-prokaryotic-cells"
            },

            # Tiêu bản cố định (co_dinh)
            {
                "ma": "pho_nang",
                "ten": "Phế nang và mao mạch phổi (Alveoli)",
                "loai": "co_dinh",
                "nhuom": "he",
                "kich_thuoc_te_bao_um": [10, 30],
                "bo_phan_chu_thich": [
                    {"ten": "Tế bào biểu mô phế nang dẹt (Pneumocyte I)", "x": 0.35, "y": 0.42},
                    {"ten": "Lòng phế nang chứa khí trao đổi O2/CO2", "x": 0.50, "y": 0.50},
                    {"ten": "Mao mạch chứa hồng cầu đơn hàng", "x": 0.72, "y": 0.65}
                ],
                "nguon": "https://openstax.org/books/anatomy-and-physiology-2e/pages/22-2-the-lungs"
            },
            {
                "ma": "nephron",
                "ten": "Cầu thận và ống lượn thận (Nephron)",
                "loai": "co_dinh",
                "nhuom": "he",
                "kich_thuoc_te_bao_um": [15, 45],
                "bo_phan_chu_thich": [
                    {"ten": "Búi mao mạch Malpighi (Glomerulus)", "x": 0.48, "y": 0.48},
                    {"ten": "Bao Bowman hứng dịch lọc cầu thận", "x": 0.30, "y": 0.35},
                    {"ten": "Biểu mô ống lượn gần có vi nhung mao", "x": 0.75, "y": 0.60}
                ],
                "nguon": "https://openstax.org/books/anatomy-and-physiology-2e/pages/25-3-physiology-of-urine-formation"
            },
            {
                "ma": "synapse",
                "ten": "Khớp thần kinh hóa học (Chemical Synapse)",
                "loai": "co_dinh",
                "nhuom": "he",
                "kich_thuoc_te_bao_um": [0.5, 2.0],
                "bo_phan_chu_thich": [
                    {"ten": "Chùy tiền synapse chứa bóng chất dẫn truyền", "x": 0.45, "y": 0.32},
                    {"ten": "Khe synapse rộng 20 – 40 nm", "x": 0.50, "y": 0.50},
                    {"ten": "Màng sau synapse chứa thụ thể tiếp nhận", "x": 0.55, "y": 0.68}
                ],
                "nguon": "https://openstax.org/books/anatomy-and-physiology-2e/pages/12-5-communication-between-neurons"
            },
            {
                "ma": "sarcomere",
                "ten": "Đơn vị co cơ vân (Sarcomere)",
                "loai": "co_dinh",
                "nhuom": "he",
                "kich_thuoc_te_bao_um": [2.0, 3.0],
                "bo_phan_chu_thich": [
                    {"ten": "Vạch Z giới hạn hai đầu sarcomere", "x": 0.20, "y": 0.50},
                    {"ten": "Băng tối A chứa sợi dày myosin xen sợi actin", "x": 0.50, "y": 0.48},
                    {"ten": "Băng sáng I chỉ chứa sợi mỏng actin", "x": 0.78, "y": 0.52}
                ],
                "nguon": "https://openstax.org/books/anatomy-and-physiology-2e/pages/10-2-skeletal-muscle"
            },
            {
                "ma": "te_bao_vien",
                "ten": "Tế bào viền niêm mạc dạ dày tiết HCl (Parietal cell)",
                "loai": "co_dinh",
                "nhuom": "he",
                "kich_thuoc_te_bao_um": [20, 35],
                "bo_phan_chu_thich": [
                    {"ten": "Tế bào viền hình tháp bắt màu axit mạnh (hồng)", "x": 0.52, "y": 0.45},
                    {"ten": "Hệ thống vi quản nội bào bơm proton H+/K+ ATPase", "x": 0.42, "y": 0.52},
                    {"ten": "Tế bào chính tiết pepsinogen (bắt màu kiềm)", "x": 0.75, "y": 0.60}
                ],
                "nguon": "https://openstax.org/books/anatomy-and-physiology-2e/pages/23-4-the-stomach"
            },
            {
                "ma": "tieu_thuy_gan",
                "ten": "Tiểu thùy gan hình lục giác (Hepatic lobule)",
                "loai": "co_dinh",
                "nhuom": "he",
                "kich_thuoc_te_bao_um": [20, 30],
                "bo_phan_chu_thich": [
                    {"ten": "Tĩnh mạch trung tâm tiểu thùy", "x": 0.50, "y": 0.50},
                    {"ten": "Dây tế bào gan xếp hình nan hoa", "x": 0.38, "y": 0.38},
                    {"ten": "Khoảng cửa chứa bộ ba cửa (TM cửa, ĐM gan, ống mật)", "x": 0.82, "y": 0.20}
                ],
                "nguon": "https://openstax.org/books/anatomy-and-physiology-2e/pages/23-6-accessory-organs-in-digestion"
            },
            {
                "ma": "phet_mau_nguoi",
                "ten": "Phết máu ngoại vi người nhuộm Giemsa",
                "loai": "co_dinh",
                "nhuom": "he",
                "kich_thuoc_te_bao_um": [7.0, 15.0],
                "bo_phan_chu_thich": [
                    {"ten": "Hồng cầu hình đĩa lõm hai mặt không nhân", "x": 0.35, "y": 0.45},
                    {"ten": "Bạch cầu trung tính nhân nhiều thùy", "x": 0.62, "y": 0.50},
                    {"ten": "Bạch cầu lympho nhân to tròn chiếm gần hết tế bào", "x": 0.48, "y": 0.72},
                    {"ten": "Tiểu cầu nhỏ li ti đông máu", "x": 0.25, "y": 0.60}
                ],
                "nguon": "https://openstax.org/books/anatomy-and-physiology-2e/pages/18-2-production-of-the-formed-elements"
            },
            {
                "ma": "re_hanh_nguyen_phan",
                "ten": "Mô phân sinh rễ hành các kỳ nguyên phân",
                "loai": "co_dinh",
                "nhuom": "he",
                "kich_thuoc_te_bao_um": [20, 45],
                "bo_phan_chu_thich": [
                    {"ten": "Kỳ đầu: NST đóng xoắn thấy rõ sợi", "x": 0.25, "y": 0.35},
                    {"ten": "Kỳ giữa: NST co ngắn cực đại xếp 1 hàng ở mặt phẳng xích đạo", "x": 0.50, "y": 0.45},
                    {"ten": "Kỳ sau: các cromatit tách nhau về 2 cực", "x": 0.75, "y": 0.55},
                    {"ten": "Kỳ cuối: vách ngăn tế bào mới hình thành", "x": 0.40, "y": 0.75}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/10-2-the-cell-cycle"
            }
        ]
    }
    f = DATA_SINH / "tieu_ban.json"
    f.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Created {f} with {len(data['tieu_ban'])} slides.")


def tao_chung_vi_sinh():
    data = {
        "phien_ban": 1,
        "he_so_nhiem": {
            "mo_ta": "Các hệ số xác suất nhiễm mẫu môi trường trong ContaminationRules (C# thuần)",
            "mo_phong": True,
            "thoi_gian_mo_nap_moi_giay": 0.002,
            "trong_tu_an_toan_chay_tot": 0.02,
            "kinh_cao_hon_20cm": 4.5,
            "luoi_hut_bi_chan": 3.0,
            "chua_rua_tay": 2.5,
            "khong_bao_ho": 1.8,
            "mat_ban_chua_sat_khuan": 3.0,
            "dung_cu_chua_tiet_trung": 15.0,
            "gan_ngon_lua_den_con": 0.15
        },
        "chung_vi_sinh": [
            {
                "ma": "e_coli_k12",
                "ten_khoa_hoc": "Escherichia coli strain K-12 substr. MG1655",
                "ten": "Vi khuẩn E. coli K-12",
                "cap_an_toan": "BSL-1",
                "gram": "am",
                "hinh_dang": "que",
                "thoi_gian_the_he_phut": 20.0,
                "nhiet_do_toi_uu_c": 37.0,
                "khuan_lac": {
                    "mau": [245, 240, 225],
                    "be_mat": "bong",
                    "bo": "tron",
                    "duong_kinh_24h_mm": 1.8
                },
                "nguon": "https://www.atcc.org/products/25922"
            },
            {
                "ma": "s_cerevisiae",
                "ten_khoa_hoc": "Saccharomyces cerevisiae",
                "ten": "Nấm men bánh mì / bia",
                "cap_an_toan": "BSL-1",
                "gram": "khong_ap_dung",
                "hinh_dang": "nam_men",
                "thoi_gian_the_he_phut": 90.0,
                "nhiet_do_toi_uu_c": 30.0,
                "khuan_lac": {
                    "mau": [255, 250, 240],
                    "be_mat": "loi",
                    "bo": "tron",
                    "duong_kinh_24h_mm": 2.2
                },
                "nguon": "https://www.atcc.org/products/204508"
            },
            {
                "ma": "l_bulgaricus",
                "ten_khoa_hoc": "Lactobacillus delbrueckii subsp. bulgaricus",
                "ten": "Vi khuẩn lên men sữa chua Bulgaricus",
                "cap_an_toan": "BSL-1",
                "gram": "duong",
                "hinh_dang": "que",
                "thoi_gian_the_he_phut": 55.0,
                "nhiet_do_toi_uu_c": 42.0,
                "khuan_lac": {
                    "mau": [240, 235, 215],
                    "be_mat": "kho_nhan",
                    "bo": "khong_deu",
                    "duong_kinh_24h_mm": 1.0
                },
                "nguon": "https://www.atcc.org/products/11842"
            },
            {
                "ma": "s_thermophilus",
                "ten_khoa_hoc": "Streptococcus thermophilus",
                "ten": "Liên cầu khuẩn sữa chua chịu nhiệt",
                "cap_an_toan": "BSL-1",
                "gram": "duong",
                "hinh_dang": "cau",
                "thoi_gian_the_he_phut": 35.0,
                "nhiet_do_toi_uu_c": 42.0,
                "khuan_lac": {
                    "mau": [250, 245, 230],
                    "be_mat": "bong",
                    "bo": "tron",
                    "duong_kinh_24h_mm": 1.2
                },
                "nguon": "https://www.atcc.org/products/19258"
            },
            {
                "ma": "b_subtilis_168",
                "ten_khoa_hoc": "Bacillus subtilis subsp. subtilis strain 168",
                "ten": "Trực khuẩn cỏ Bacillus subtilis",
                "cap_an_toan": "BSL-1",
                "gram": "duong",
                "hinh_dang": "que",
                "thoi_gian_the_he_phut": 26.0,
                "nhiet_do_toi_uu_c": 37.0,
                "khuan_lac": {
                    "mau": [235, 230, 210],
                    "be_mat": "kho_nhan",
                    "bo": "khong_deu",
                    "duong_kinh_24h_mm": 3.0
                },
                "nguon": "https://www.atcc.org/products/23857"
            },
            # 2 chủng tạp nhiễm môi trường (BSL-1)
            {
                "ma": "tap_nam_moc",
                "ten_khoa_hoc": "Aspergillus sp. (environmental isolate BSL-1)",
                "ten": "Bào tử nấm mốc không khí",
                "cap_an_toan": "BSL-1",
                "gram": "khong_ap_dung",
                "hinh_dang": "nam_men",
                "thoi_gian_the_he_phut": 180.0,
                "nhiet_do_toi_uu_c": 28.0,
                "khuan_lac": {
                    "mau": [60, 90, 60],
                    "be_mat": "kho_nhan",
                    "bo": "khong_deu",
                    "duong_kinh_24h_mm": 4.5
                },
                "mo_phong": True
            },
            {
                "ma": "tap_bacillus_khong_khi",
                "ten_khoa_hoc": "Bacillus megaterium (environmental isolate BSL-1)",
                "ten": "Trực khuẩn bụi không khí",
                "cap_an_toan": "BSL-1",
                "gram": "duong",
                "hinh_dang": "que",
                "thoi_gian_the_he_phut": 45.0,
                "nhiet_do_toi_uu_c": 30.0,
                "khuan_lac": {
                    "mau": [220, 210, 180],
                    "be_mat": "kho_nhan",
                    "bo": "khong_deu",
                    "duong_kinh_24h_mm": 2.8
                },
                "mo_phong": True
            }
        ]
    }
    f = DATA_SINH / "chung_vi_sinh.json"
    f.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Created {f} with {len(data['chung_vi_sinh'])} strains.")


def tao_moi_truong():
    data = {
        "phien_ban": 1,
        "moi_truong": [
            {
                "ma": "pca",
                "ten": "Thạch đếm khuẩn lạc PCA (Plate Count Agar)",
                "loai": "thach",
                "thanh_phan": [
                    {"chat": "Tryptone", "g_l": 5.0},
                    {"chat": "Cao men (Yeast extract)", "g_l": 2.5},
                    {"chat": "Glucose", "g_l": 1.0},
                    {"chat": "Thạch agar", "g_l": 15.0}
                ],
                "mau": [245, 235, 195],
                "do_trong": 0.85,
                "ph": 7.0,
                "nguon": "https://www.fda.gov/food/laboratory-methods-food/bam-media-m124-plate-count-agar-standard-methods"
            },
            {
                "ma": "thach_dinh_duong",
                "ten": "Thạch dinh dưỡng NA (Nutrient Agar)",
                "loai": "thach",
                "thanh_phan": [
                    {"chat": "Peptone", "g_l": 5.0},
                    {"chat": "Cao thịt (Beef extract)", "g_l": 3.0},
                    {"chat": "NaCl", "g_l": 5.0},
                    {"chat": "Thạch agar", "g_l": 15.0}
                ],
                "mau": [235, 220, 175],
                "do_trong": 0.80,
                "ph": 6.8,
                "nguon": "https://www.fda.gov/food/laboratory-methods-food/bam-media-m112-nutrient-agar"
            },
            {
                "ma": "lb_agar",
                "ten": "Thạch Luria-Bertani (LB Agar)",
                "loai": "thach",
                "thanh_phan": [
                    {"chat": "Tryptone", "g_l": 10.0},
                    {"chat": "Cao men", "g_l": 5.0},
                    {"chat": "NaCl", "g_l": 10.0},
                    {"chat": "Thạch agar", "g_l": 15.0}
                ],
                "mau": [240, 225, 180],
                "do_trong": 0.82,
                "ph": 7.0,
                "nguon": "https://cshprotocols.cshlp.org/content/2006/1/pdb.rec8141"
            },
            {
                "ma": "sabouraud",
                "ten": "Thạch Sabouraud Dextrose Agar (SDA cho nấm men)",
                "loai": "thach",
                "thanh_phan": [
                    {"chat": "Mycological peptone", "g_l": 10.0},
                    {"chat": "Dextrose (Glucose)", "g_l": 40.0},
                    {"chat": "Thạch agar", "g_l": 15.0}
                ],
                "mau": [250, 235, 170],
                "do_trong": 0.88,
                "ph": 5.6,
                "nguon": "https://www.fda.gov/food/laboratory-methods-food/bam-media-m136-sabouraud-dextrose-agar"
            },
            {
                "ma": "canh_lb",
                "ten": "Canh thang dinh dưỡng Luria-Bertani (LB Broth)",
                "loai": "long",
                "thanh_phan": [
                    {"chat": "Tryptone", "g_l": 10.0},
                    {"chat": "Cao men", "g_l": 5.0},
                    {"chat": "NaCl", "g_l": 10.0}
                ],
                "mau": [230, 215, 150],
                "do_trong": 0.95,
                "ph": 7.0,
                "nguon": "https://cshprotocols.cshlp.org/content/2006/1/pdb.rec8141"
            },
            {
                "ma": "nuoc_muoi_sinh_ly",
                "ten": "Dung dịch nước muối sinh lý vô trùng (NaCl 0,9%)",
                "loai": "long",
                "thanh_phan": [
                    {"chat": "NaCl", "g_l": 9.0}
                ],
                "mau": [250, 250, 255],
                "do_trong": 1.0,
                "ph": 7.0,
                "nguon": "https://www.pharmacopeia.cn/"
            }
        ]
    }
    f = DATA_SINH / "moi_truong.json"
    f.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Created {f} with {len(data['moi_truong'])} media.")


def tao_sinh_pham():
    data = {
        "phien_ban": 1,
        "sinh_pham": [
            # Mẫu thực vật / động vật tươi
            {
                "id": "sp_hanh_tim",
                "ten": "Củ hành tím tươi (nguyên củ làm tiêu bản tế bào)",
                "loai": "mau",
                "tu": "ke",
                "ngan": "Khay mẫu tươi K01",
                "so_luong": 20,
                "don_vi": "củ",
                "so_lo": "M2026-HT01",
                "han_dung_ngay": 14,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_la_thai_lai",
                "ten": "Lá thài lài tía tươi (Tradescantia zebrina)",
                "loai": "mau",
                "tu": "ke",
                "ngan": "Khay mẫu tươi K01",
                "so_luong": 30,
                "don_vi": "lá",
                "so_lo": "M2026-TL02",
                "han_dung_ngay": 3,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_rong_duoi_cho",
                "ten": "Cành rong đuôi chó khỏe (Elodea canadensis)",
                "loai": "mau",
                "tu": "ke",
                "ngan": "Bể thủy sinh nhà kính",
                "so_luong": 15,
                "don_vi": "cành",
                "so_lo": "M2026-EL01",
                "han_dung_ngay": 7,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_khoai_tay",
                "ten": "Củ khoai tây tươi (nguồn enzyme catalase thực vật)",
                "loai": "mau",
                "tu": "ke",
                "ngan": "Khay mẫu tươi K02",
                "so_luong": 10,
                "don_vi": "củ",
                "so_lo": "M2026-KT01",
                "han_dung_ngay": 25,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_gan_ga",
                "ten": "Gan gà tươi bảo quản lạnh (nguồn enzyme catalase động vật)",
                "loai": "mau",
                "tu": "tu_mat_4",
                "ngan": "Ngăn mát số 1",
                "so_luong": 500,
                "don_vi": "g",
                "so_lo": "M2026-GG03",
                "han_dung_ngay": 4,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_chuoi_chin",
                "ten": "Chuối chín tươi (nguồn mô tách chiết ADN)",
                "loai": "mau",
                "tu": "ke",
                "ngan": "Khay mẫu tươi K02",
                "so_luong": 12,
                "don_vi": "quả",
                "so_lo": "M2026-CH01",
                "han_dung_ngay": 6,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_sua_tuoi",
                "ten": "Sữa tươi không đường thanh trùng (nguyên liệu làm sữa chua)",
                "loai": "mau",
                "tu": "tu_mat_4",
                "ngan": "Ngăn mát số 2",
                "so_luong": 4,
                "don_vi": "hộp 1L",
                "so_lo": "M2026-ST01",
                "han_dung_ngay": 10,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_sua_chua_cai",
                "ten": "Sữa chua cái men sống (L. bulgaricus & S. thermophilus)",
                "loai": "giong",
                "tu": "tu_mat_4",
                "ngan": "Ngăn mát số 2",
                "so_luong": 6,
                "don_vi": "hộp",
                "so_lo": "M2026-SC01",
                "han_dung_ngay": 12,
                "cap_an_toan": "BSL-1",
                "ma_chung": "l_bulgaricus"
            },
            {
                "id": "sp_men_banh_mi",
                "ten": "Men khô làm bánh mì bánh ngọt (S. cerevisiae hoạt hóa)",
                "loai": "giong",
                "tu": "tu_mat_4",
                "ngan": "Ngăn mát số 2",
                "so_luong": 10,
                "don_vi": "gói 50g",
                "so_lo": "M2026-MK01",
                "han_dung_ngay": 180,
                "cap_an_toan": "BSL-1",
                "ma_chung": "s_cerevisiae"
            },

            # Giống vi sinh thuần chủng BSL-1
            {
                "id": "sp_giong_e_coli",
                "ten": "Ống giống E. coli K-12 thạch nghiêng",
                "loai": "chung_vi_sinh",
                "tu": "tu_mat_4",
                "ngan": "Hộp giống vi sinh 01",
                "so_luong": 5,
                "don_vi": "ống",
                "so_lo": "G2026-EC01",
                "han_dung_ngay": 60,
                "cap_an_toan": "BSL-1",
                "ma_chung": "e_coli_k12"
            },
            {
                "id": "sp_giong_b_subtilis",
                "ten": "Ống giống Bacillus subtilis 168 thạch nghiêng",
                "loai": "chung_vi_sinh",
                "tu": "tu_mat_4",
                "ngan": "Hộp giống vi sinh 01",
                "so_luong": 5,
                "don_vi": "ống",
                "so_lo": "G2026-BS01",
                "han_dung_ngay": 90,
                "cap_an_toan": "BSL-1",
                "ma_chung": "b_subtilis_168"
            },

            # Hóa chất sinh học và thuốc thử
            {
                "id": "sp_xanh_methylen_1",
                "ten": "Dung dịch xanh methylen 1% trong nước",
                "loai": "hoa_chat_sinh",
                "tu": "ke",
                "ngan": "Kệ thuốc thử K15",
                "so_luong": 500,
                "don_vi": "mL",
                "so_lo": "HC2026-MB01",
                "han_dung_ngay": 720,
                "cap_an_toan": "BSL-1",
                "ma_chat": "xanh_methylen"
            },
            {
                "id": "sp_lugol",
                "ten": "Thuốc thử Lugol (I2/KI nhận biết tinh bột và cố định)",
                "loai": "hoa_chat_sinh",
                "tu": "ke",
                "ngan": "Kệ thuốc thử K15",
                "so_luong": 500,
                "don_vi": "mL",
                "so_lo": "HC2026-LG01",
                "han_dung_ngay": 365,
                "cap_an_toan": "BSL-1",
                "ma_chat": "I2"
            },
            {
                "id": "sp_nacl_10",
                "ten": "Dung dịch Natri clorua NaCl 10% (gây co nguyên sinh)",
                "loai": "hoa_chat_sinh",
                "tu": "ke",
                "ngan": "Kệ dung dịch muối",
                "so_luong": 1000,
                "don_vi": "mL",
                "so_lo": "HC2026-NC10",
                "han_dung_ngay": 720,
                "cap_an_toan": "BSL-1",
                "ma_chat": "NaCl"
            },
            {
                "id": "sp_nacl_sinh_ly",
                "ten": "Dung dịch NaCl 0,9% sinh lý vô trùng",
                "loai": "hoa_chat_sinh",
                "tu": "ke",
                "ngan": "Kệ dung dịch muối",
                "so_luong": 2000,
                "don_vi": "mL",
                "so_lo": "HC2026-NC09",
                "han_dung_ngay": 180,
                "cap_an_toan": "BSL-1",
                "ma_chat": "NaCl"
            },
            {
                "id": "sp_h2o2_3",
                "ten": "Dung dịch hiđro peoxit H2O2 3% (nước oxy già thử catalase)",
                "loai": "hoa_chat_sinh",
                "tu": "tu_mat_4",
                "ngan": "Ngăn mát chai nâu",
                "so_luong": 1000,
                "don_vi": "mL",
                "so_lo": "HC2026-HP03",
                "han_dung_ngay": 90,
                "cap_an_toan": "BSL-1",
                "ma_chat": "H2O2"
            },
            {
                "id": "sp_con_70",
                "ten": "Cồn Ethanol 70% v/v khử trùng phòng lab",
                "loai": "hoa_chat_sinh",
                "tu": "ke",
                "ngan": "Tủ dung môi dễ cháy",
                "so_luong": 5000,
                "don_vi": "mL",
                "so_lo": "HC2026-ET70",
                "han_dung_ngay": 720,
                "cap_an_toan": "BSL-1",
                "ma_chat": "C2H5OH"
            },
            {
                "id": "sp_con_96_lanh",
                "ten": "Cồn Ethanol 96% giữ lạnh -20 °C (kết tủa ADN)",
                "loai": "hoa_chat_sinh",
                "tu": "tu_dong_am20",
                "ngan": "Ngăn đông hóa chất",
                "so_luong": 1000,
                "don_vi": "mL",
                "so_lo": "HC2026-ET96",
                "han_dung_ngay": 365,
                "cap_an_toan": "BSL-1",
                "ma_chat": "C2H5OH"
            },
            {
                "id": "sp_bo_nhuom_gram",
                "ten": "Bộ kit thuốc nhuộm Gram 4 chai (Crystal Violet, Lugol, Cồn tẩy, Safranin)",
                "loai": "kit",
                "tu": "ke",
                "ngan": "Đảo nhuộm Gram",
                "so_luong": 4,
                "don_vi": "bộ",
                "so_lo": "KT2026-GR01",
                "han_dung_ngay": 365,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_dau_soi_kinh",
                "ten": "Dầu soi kính hiển vi quang học tổng hợp n = 1,515",
                "loai": "hoa_chat_sinh",
                "tu": "ke",
                "ngan": "Khay kính hiển vi",
                "so_luong": 200,
                "don_vi": "mL",
                "so_lo": "HC2026-DS01",
                "han_dung_ngay": 720,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_duong_glucose",
                "ten": "Đường Glucose D-Dextrose tinh khiết",
                "loai": "hoa_chat_sinh",
                "tu": "ke",
                "ngan": "Kệ hóa chất hữu cơ",
                "so_luong": 1000,
                "don_vi": "g",
                "so_lo": "HC2026-GL01",
                "han_dung_ngay": 720,
                "cap_an_toan": "BSL-1",
                "ma_chat": "hc:578"
            },
            {
                "id": "sp_nahco3",
                "ten": "Muối Natri hiđrocacbonat NaHCO3 (cung cấp CO2 quang hợp)",
                "loai": "hoa_chat_sinh",
                "tu": "ke",
                "ngan": "Kệ muối vô cơ",
                "so_luong": 1000,
                "don_vi": "g",
                "so_lo": "HC2026-BC01",
                "han_dung_ngay": 720,
                "cap_an_toan": "BSL-1",
                "ma_chat": "NaHCO3"
            },

            # Hóa chất sinh học phân tử & PCR
            {
                "id": "sp_kit_pcr_mastermix",
                "ten": "Taq 2X PCR MasterMix (Taq polymerase, dNTPs, đệm MgCl2)",
                "loai": "kit",
                "tu": "tu_dong_am20",
                "ngan": "Hộp enzyme PCR",
                "so_luong": 5,
                "don_vi": "ống 1,25 mL",
                "so_lo": "KT2026-MM01",
                "han_dung_ngay": 180,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_cap_moi_pcr",
                "ten": "Cặp mồi PCR kiểm tra gen giữ nhà thực vật rbcL (10 µM)",
                "loai": "kit",
                "tu": "tu_dong_am20",
                "ngan": "Hộp mồi PCR",
                "so_luong": 4,
                "don_vi": "ống 500 µL",
                "so_lo": "KT2026-PR01",
                "han_dung_ngay": 365,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_nuoc_khong_nuclease",
                "ten": "Nước siêu tinh khiết không nuclease (Nuclease-free water)",
                "loai": "hoa_chat_sinh",
                "tu": "tu_dong_am20",
                "ngan": "Hộp nước sinh học",
                "so_luong": 10,
                "don_vi": "lọ 50 mL",
                "so_lo": "HC2026-NW01",
                "han_dung_ngay": 720,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_dna_ladder_100bp",
                "ten": "Thang chuẩn ADN 100 bp DNA Ladder (100 – 1500 bp)",
                "loai": "kit",
                "tu": "tu_dong_am20",
                "ngan": "Hộp thang chuẩn ADN",
                "so_luong": 5,
                "don_vi": "ống 500 µL",
                "so_lo": "KT2026-LD01",
                "han_dung_ngay": 365,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_dem_nap_mau_6x",
                "ten": "Đệm nạp mẫu điện di 6X Loading Dye (Bromophenol blue / Xylene cyanol)",
                "loai": "hoa_chat_sinh",
                "tu": "tu_mat_4",
                "ngan": "Ngăn mát điện di",
                "so_luong": 5,
                "don_vi": "lọ 5 mL",
                "so_lo": "HC2026-LD06",
                "han_dung_ngay": 365,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_agarose_bot",
                "ten": "Bột Agarose điện di độ phân giải cao (Molecular Biology Grade)",
                "loai": "hoa_chat_sinh",
                "tu": "ke",
                "ngan": "Kệ hóa chất SHPT",
                "so_luong": 500,
                "don_vi": "g",
                "so_lo": "HC2026-AG01",
                "han_dung_ngay": 1080,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_dem_tae_50x",
                "ten": "Dung dịch đệm điện di TAE 50X đậm đặc (Tris-Acetate-EDTA)",
                "loai": "hoa_chat_sinh",
                "tu": "ke",
                "ngan": "Kệ đệm điện di",
                "so_luong": 1000,
                "don_vi": "mL",
                "so_lo": "HC2026-TAE50",
                "han_dung_ngay": 720,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_nhuom_adn_gelgreen",
                "ten": "Thuốc nhuộm ADN GelGreen 10.000X an toàn huỳnh quang xanh",
                "loai": "hoa_chat_sinh",
                "tu": "tu_mat_4",
                "ngan": "Hộp thuốc nhuộm cản quang",
                "so_luong": 2,
                "don_vi": "ống 500 µL",
                "so_lo": "HC2026-GG01",
                "han_dung_ngay": 365,
                "cap_an_toan": "BSL-1"
            },

            # Môi trường pha sẵn
            {
                "id": "sp_dia_thach_pca",
                "ten": "Đĩa petri thạch PCA đổ sẵn vô trùng",
                "loai": "moi_truong",
                "tu": "tu_mat_4",
                "ngan": "Ngăn đĩa thạch đổ sẵn",
                "so_luong": 50,
                "don_vi": "đĩa",
                "so_lo": "MT2026-PCA01",
                "han_dung_ngay": 30,  # sắp hết hạn để test cảnh báo
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_dia_thach_lb",
                "ten": "Đĩa petri thạch LB đổ sẵn vô trùng",
                "loai": "moi_truong",
                "tu": "tu_mat_4",
                "ngan": "Ngăn đĩa thạch đổ sẵn",
                "so_luong": 50,
                "don_vi": "đĩa",
                "so_lo": "MT2026-LB01",
                "han_dung_ngay": 45,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_ong_canh_lb_10ml",
                "ten": "Ống canh thang LB 10 mL vô trùng",
                "loai": "moi_truong",
                "tu": "tu_mat_4",
                "ngan": "Giá canh thang",
                "so_luong": 40,
                "don_vi": "ống",
                "so_lo": "MT2026-CT01",
                "han_dung_ngay": 20,  # sắp hết hạn
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_nuoc_rua_tay_sat_khuan",
                "ten": "Dung dịch xà phòng bọt sát khuẩn vòi cảm ứng",
                "loai": "hoa_chat_sinh",
                "tu": "ke",
                "ngan": "Kho dự phòng vệ sinh",
                "so_luong": 5,
                "don_vi": "can 2L",
                "so_lo": "HC2026-ST01",
                "han_dung_ngay": 720,
                "cap_an_toan": "BSL-1"
            },
            # Sinh phẩm hết hạn (để web có dữ liệu cảnh báo)
            {
                "id": "sp_kit_ly_giai_het_han",
                "ten": "Kit ly giải màng tế bào SDS-Lysis (lô cũ quá hạn)",
                "loai": "kit",
                "tu": "ke",
                "ngan": "Góc vật tư chờ hủy",
                "so_luong": 1,
                "don_vi": "hộp",
                "so_lo": "KT2024-LY99",
                "han_dung_ngay": -15,  # ĐÃ QUÁ HẠN 15 NGÀY
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_plasmid_puc19",
                "ten": "Plasmid pUC19 (thể truyền 2686 bp, ampR, lacZα) 100 ng/µL",
                "loai": "kit",
                "tu": "tu_dong_am20",
                "ngan": "Hộp ghép gen",
                "so_luong": 4,
                "don_vi": "ống 50 µL",
                "so_lo": "GG2026-PU19",
                "han_dung_ngay": 365,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_enzyme_ecori",
                "ten": "Enzyme cắt giới hạn EcoRI (G^AATTC) 20 U/µL",
                "loai": "kit",
                "tu": "tu_dong_am20",
                "ngan": "Hộp ghép gen",
                "so_luong": 2,
                "don_vi": "ống 100 µL",
                "so_lo": "GG2026-ECO1",
                "han_dung_ngay": 365,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_enzyme_bamhi",
                "ten": "Enzyme cắt giới hạn BamHI (G^GATCC) 20 U/µL",
                "loai": "kit",
                "tu": "tu_dong_am20",
                "ngan": "Hộp ghép gen",
                "so_luong": 2,
                "don_vi": "ống 100 µL",
                "so_lo": "GG2026-BAM1",
                "han_dung_ngay": 365,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_ligaza_t4",
                "ten": "ADN ligaza T4 kèm đệm 10× (ATP, Mg²⁺)",
                "loai": "kit",
                "tu": "tu_dong_am20",
                "ngan": "Hộp ghép gen",
                "so_luong": 2,
                "don_vi": "bộ",
                "so_lo": "GG2026-LIG4",
                "han_dung_ngay": 365,
                "cap_an_toan": "BSL-1"
            },
            {
                "id": "sp_te_bao_kha_bien_dh5a",
                "ten": "Tế bào E. coli DH5α khả biến (chủng không gây bệnh)",
                "loai": "chung_vi_sinh",
                "tu": "tu_dong_am20",
                "ngan": "Hộp ghép gen",
                "so_luong": 10,
                "don_vi": "ống 50 µL",
                "so_lo": "GG2026-DH5A",
                "han_dung_ngay": 180,
                "cap_an_toan": "BSL-1",
                "ma_chung": "e_coli_k12"
            },
            {
                "id": "sp_dia_lb_amp_xgal",
                "ten": "Đĩa thạch LB + ampicillin 100 µg/mL + X-gal/IPTG",
                "loai": "moi_truong",
                "tu": "tu_mat_4",
                "ngan": "Ngăn đĩa thạch",
                "so_luong": 20,
                "don_vi": "đĩa",
                "so_lo": "GG2026-AXG",
                "han_dung_ngay": 30,
                "cap_an_toan": "BSL-1"
            }
        ]
    }
    f = DATA_SINH / "sinh_pham_mac_dinh.json"
    f.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Created {f} with {len(data['sinh_pham'])} default biological supplies.")


def tao_bai_thi_nghiem():
    data = {
        "phien_ban": 1,
        "bai_thi_nghiem": [
            {
                "ma": "te_bao_hanh",
                "ten": "Quan sát tế bào biểu bì vảy hành dưới kính hiển vi",
                "lop": 10,
                "sgk": "Sinh học 10 Kết nối tri thức - Bài 4: Thực hành quan sát tế bào",
                "muc_tieu": "Làm được tiêu bản tạm thời tế bào biểu bì vảy hành; quan sát, vẽ và phân biệt được vách tế bào, không bào chứa sắc tố và nhân tế bào ở độ phóng đại 100x và 400x.",
                "dung_cu": ["kinh_hien_vi", "lam_kinh", "lamen", "kim_mui_mac", "kep_panh", "dao_lam", "ong_nho_giot", "giay_tham"],
                "sinh_pham": ["sp_hanh_tim", "sp_xanh_methylen_1"],
                "buoc": [
                    {
                        "so": 1,
                        "mo_ta": "Dùng dao lam rạch nhẹ một ô vuông nhỏ 5x5 mm ở mặt trong vảy hành tím, dùng kim mũi mác hoặc panh bóc nhẹ lớp biểu bì mỏng.",
                        "dieu_kien": {"loai": "dat_vat", "ma": "kim_mui_mac", "vao": "dia_dong_ho"}
                    },
                    {
                        "so": 2,
                        "mo_ta": "Nhỏ 1 giọt nước cất (hoặc dung dịch xanh methylen) lên giữa lam kính sạch.",
                        "dieu_kien": {"loai": "the_tich", "ma": "ong_nho_giot", "ml_min": 0.03, "ml_max": 0.06}
                    },
                    {
                        "so": 3,
                        "mo_ta": "Đặt lớp biểu bì vào giọt nước, dàn phẳng không để quăn mép.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "lam_kinh", "ten": "co_mau", "gia_tri": 1}
                    },
                    {
                        "so": 4,
                        "mo_ta": "Đậy lamen nghiêng góc 45° từ từ hạ xuống tránh tạo bọt khí, dùng giấy thấm hút dịch tràn ngoài mép.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "lam_kinh", "ten": "da_day_lamen", "gia_tri": 1}
                    },
                    {
                        "so": 5,
                        "mo_ta": "Đặt tiêu bản lên bàn kính hiển vi, lấy nét ở vật kính 10x rồi chuyển sang vật kính 40x quan sát cấu trúc tế bào.",
                        "dieu_kien": {"loai": "vat_kinh", "gia_tri": 40}
                    }
                ],
                "ket_qua_mong_doi": "Thấy rõ các tế bào biểu bì hình đa giác xếp sát nhau như bờ tường gạch, vách tế bào rõ nét; không bào lớn chứa sắc tố tím (anthocyanin); tế bào nhuộm xanh methylen thấy rõ nhân bắt màu xanh thẫm.",
                "cau_hoi": [
                    {"hoi": "Tại sao cần đậy lamen nghiêng góc 45° rồi từ từ hạ xuống?", "dap_an": "Để bọt khí thoát hết ra ngoài theo mép lamen, không bị kẹt lại trong giọt dịch gây cản trở quan sát hiển vi."},
                    {"hoi": "Thành phần nào của tế bào biểu bì hành giữ cho tế bào có hình dạng cố định?", "dap_an": "Vách tế bào (thành tế bào) cấu tạo từ sợi cellulose chịu lực tốt."},
                    {"hoi": "Tác dụng của phẩm nhuộm xanh methylen trong bài thực hành này là gì?", "dap_an": "Xanh methylen là phẩm nhuộm kiềm tính liên kết đặc hiệu với axit nucleic trong nhân tế bào, làm nhân bắt màu xanh đậm nổi bật."}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/4-3-eukaryotic-cells"
            },
            {
                "ma": "khi_khong",
                "ten": "Quan sát khí khổng ở biểu bì lá cây",
                "lop": 11,
                "sgk": "Sinh học 11 Cánh diều - Bài 3: Thực hành trao đổi nước và khoáng ở thực vật",
                "muc_tieu": "Bóc được lớp biểu bì mặt dưới lá thài lài tía; quan sát và mô tả được cấu trúc đóng mở của tế bào hạt đậu tạo nên khí khổng.",
                "dung_cu": ["kinh_hien_vi", "lam_kinh", "lamen", "kep_panh", "kim_mui_mac", "ong_nho_giot", "keo_nho"],
                "sinh_pham": ["sp_la_thai_lai"],
                "buoc": [
                    {
                        "so": 1,
                        "mo_ta": "Dùng panh xé chéo một mảnh nhỏ ở mặt dưới của phiến lá thài lài tía để lộ lớp màng biểu bì mỏng màu tím nhạt.",
                        "dieu_kien": {"loai": "dat_vat", "ma": "kep_panh", "vao": "dia_dong_ho"}
                    },
                    {
                        "so": 2,
                        "mo_ta": "Cắt lấy đoạn màng biểu bì khoảng 3x3 mm, đặt lên lam kính đã nhỏ sẵn giọt nước cất và đậy lamen.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "lam_kinh", "ten": "da_day_lamen", "gia_tri": 1}
                    },
                    {
                        "so": 3,
                        "mo_ta": "Quan sát ở vật kính 10x sau đó chuyển sang 40x để đếm số lượng khí khổng mở và đóng trong vi trường.",
                        "dieu_kien": {"loai": "vat_kinh", "gia_tri": 40}
                    }
                ],
                "ket_qua_mong_doi": "Quan sát thấy các tế bào khí khổng hình đôi hạt đậu úp vào nhau chứa nhiều hạt lục lạp xanh lục, ở giữa là khe khí khổng; tế bào biểu bì xung quanh không có lục lạp.",
                "cau_hoi": [
                    {"hoi": "Tại sao khí khổng phân bố tập trung chủ yếu ở mặt dưới lá cây trên cạn?", "dap_an": "Để giảm bớt sự thoát hơi nước quá mức do ánh nắng mặt trời trực tiếp chiếu vào mặt trên."},
                    {"hoi": "Cơ chế nào làm cho khí khổng mở ra khi tế bào hạt đậu no nước?", "dap_an": "Thành trong của tế bào hạt đậu dày và kém đàn hồi, thành ngoài mỏng và dãn dài; khi no nước áp suất trương tăng làm thành ngoài căng ra kéo thành trong cong theo mở khe khí."},
                    {"hoi": "Tế bào biểu bì thông thường có chứa lục lạp quang hợp không?", "dap_an": "Không, trong lớp biểu bì chỉ có hai tế bào hình hạt đậu của khí khổng là chứa lục lạp."}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/30-2-stems"
            },
            {
                "ma": "co_nguyen_sinh",
                "ten": "Thí nghiệm co và phản co nguyên sinh ở tế bào thực vật",
                "lop": 10,
                "sgk": "Sinh học 10 Chân trời sáng tạo - Bài 10: Thực hành thẩm thấu và co nguyên sinh",
                "muc_tieu": "Chứng minh hiện tượng thẩm thấu qua màng sinh chất tế bào sống; quan sát được tế bào co nguyên sinh khi ngâm dung dịch ưu trương và phản co nguyên sinh khi chuyển sang nhược trương.",
                "dung_cu": ["kinh_hien_vi", "lam_kinh", "lamen", "ong_nho_giot", "giay_tham", "kep_panh", "kim_mui_mac"],
                "sinh_pham": ["sp_la_thai_lai", "sp_nacl_10"],
                "buoc": [
                    {
                        "so": 1,
                        "mo_ta": "Làm tiêu bản biểu bì lá thài lài tía trong nước cất, quan sát tế bào no nước ban đầu ở vật kính 40x.",
                        "dieu_kien": {"loai": "vat_kinh", "gia_tri": 40}
                    },
                    {
                        "so": 2,
                        "mo_ta": "Nhỏ 1 giọt dung dịch NaCl 10% (hoặc sucrose 0,6M) vào một mép lamen, ở mép đối diện dùng giấy thấm hút nước để kéo dung dịch ưu trương qua mẫu.",
                        "dieu_kien": {"loai": "the_tich", "ma": "ong_nho_giot", "ml_min": 0.04, "ml_max": 0.08}
                    },
                    {
                        "so": 3,
                        "mo_ta": "Quan sát sự co lại của khối chất nguyên sinh và không bào tím tách khỏi vách tế bào (co nguyên sinh góc và co nguyên sinh lõm).",
                        "dieu_kien": {"loai": "trang_thai", "ma": "kinh_hien_vi", "ten": "co_nguyen_sinh", "gia_tri": 1}
                    },
                    {
                        "so": 4,
                        "mo_ta": "Nhỏ nước cất vào mép lamen và hút kiệt dung dịch muối bằng giấy thấm để nước nhược trương đi vào, quan sát tế bào phản co nguyên sinh.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "kinh_hien_vi", "ten": "phan_co", "gia_tri": 1}
                    }
                ],
                "ket_qua_mong_doi": "Trong dung dịch NaCl 10% (ưu trương), nước từ trong không bào thẩm thấu ra ngoài, chất nguyên sinh tím co lại thành khối tròn nhỏ ở giữa; khi thay bằng nước cất (nhược trương), nước thẩm thấu ngược vào trong làm chất nguyên sinh nở to áp sát vách như ban đầu.",
                "cau_hoi": [
                    {"hoi": "Nếu tế bào đã chết (ví dụ bị luộc sôi) thì có xảy ra hiện tượng co nguyên sinh không?", "dap_an": "Không, vì khi tế bào chết màng sinh chất bị phá hủy tính thấm chọn lọc bán thấm, nước và chất tan tự do khuếch tán."},
                    {"hoi": "Dung dịch ưu trương là dung dịch có nồng độ chất tan như thế nào so với dịch bào?", "dap_an": "Dung dịch ưu trương có nồng độ chất tan cao hơn nồng độ chất tan trong dịch bào (thế nước thấp hơn thế nước tế bào)."},
                    {"hoi": "Hiện tượng phản co nguyên sinh chứng minh tính chất nào của màng tế bào sống?", "dap_an": "Chứng minh màng sinh chất có tính thấm chọn lọc linh động và tế bào vẫn duy trì được sức sống sau khi co."}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/5-2-passive-transport"
            },
            {
                "ma": "catalase",
                "ten": "Nghiên cứu hoạt tính của enzyme catalase trong mô sống",
                "lop": 10,
                "sgk": "Sinh học 10 Kết nối tri thức - Bài 14: Thực hành một số thí nghiệm về enzyme",
                "muc_tieu": "Chứng minh sự có mặt và vai trò xúc tác phân hủy H2O2 thành H2O và O2 của enzyme catalase; so sánh hoạt tính ở củ khoai tây và gan tươi, ảnh hưởng của nhiệt độ cao (luộc sôi).",
                "dung_cu": ["ong_nghiem", "gia_ong_nghiem", "ong_dong", "kep_panh", "dao_lam", "que_dom", "be_on_nhiet"],
                "sinh_pham": ["sp_khoai_tay", "sp_gan_ga", "sp_h2o2_3"],
                "buoc": [
                    {
                        "so": 1,
                        "mo_ta": "Chuẩn bị 4 ống nghiệm: Ống 1 (lát khoai tây sống), Ống 2 (lát khoai tây chín luộc sôi), Ống 3 (mẩu gan gà tươi), Ống 4 (mẩu gan gà chín luộc sôi).",
                        "dieu_kien": {"loai": "dat_vat", "ma": "ong_nghiem", "vao": "gia_ong_nghiem"}
                    },
                    {
                        "so": 2,
                        "mo_ta": "Rót vào mỗi ống nghiệm 2 mL dung dịch H2O2 3%.",
                        "dieu_kien": {"loai": "the_tich", "ma": "ong_dong", "ml_min": 1.8, "ml_max": 2.2}
                    },
                    {
                        "so": 3,
                        "mo_ta": "Quan sát tốc độ sủi bọt khí O2 trong từng ống, đưa que đóm còn tàn đỏ vào miệng ống nghiệm để thử khí O2.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "que_dom", "ten": "bung_chay", "gia_tri": 1}
                    }
                ],
                "ket_qua_mong_doi": "Ống 3 (gan tươi) sủi bọt trắng xóa cực mạnh, que đóm tàn đỏ bùng cháy thành ngọn lửa; Ống 1 (khoai sống) sủi bọt vừa phải; Ống 2 và 4 (luộc chín) hoàn toàn không có bọt khí.",
                "cau_hoi": [
                    {"hoi": "Phương trình hóa học của phản ứng do enzyme catalase xúc tác là gì?", "dap_an": "2H2O2 --(Catalase)--> 2H2O + O2 (sủi bọt khí O2 làm bùng cháy tàn đóm đỏ)."},
                    {"hoi": "Tại sao ở các ống nghiệm chứa mô đã luộc chín lại không có bọt khí sinh ra?", "dap_an": "Nhiệt độ cao (100 °C) làm enzyme catalase (bản chất là protein) bị biến tính nhiệt vĩnh viễn, mất hoàn toàn hoạt tính xúc tác."},
                    {"hoi": "Tại sao hoạt tính catalase ở gan động vật lại mạnh hơn nhiều so với mô củ khoai tây?", "dap_an": "Gan là cơ quan chuyển hóa và giải độc trung tâm của động vật, tạo ra nhiều gốc oxy hóa nên chứa mật độ enzyme catalase cao gấp nhiều lần thực vật."}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/6-5-enzymes"
            },
            {
                "ma": "len_men_ruou",
                "ten": "Lên men ethylic (lên men rượu) ở nấm men",
                "lop": 10,
                "sgk": "Sinh học 10 Cánh diều - Bài 17: Thực hành một số phương pháp phân tích vi sinh vật",
                "muc_tieu": "Chứng minh quá trình lên men kỵ khí đường glucose của nấm men sinh khí CO2 và ethanol; đo lường thể tích CO2 làm phồng bóng bay.",
                "dung_cu": ["binh_len_men", "ong_dong", "can_phan_tich", "be_on_nhiet", "dong_ho_bam_gio"],
                "sinh_pham": ["sp_men_banh_mi", "sp_duong_glucose"],
                "buoc": [
                    {
                        "so": 1,
                        "mo_ta": "Hòa tan 10 g đường glucose trong 100 mL nước ấm 35 °C vào bình tam giác 250 mL.",
                        "dieu_kien": {"loai": "the_tich", "ma": "binh_len_men", "ml_min": 90.0, "ml_max": 110.0}
                    },
                    {
                        "so": 2,
                        "mo_ta": "Cho 5 g men khô làm bánh mì vào bình, lắc đều tạo huyền phù nấm men.",
                        "dieu_kien": {"loai": "nhiet_do", "ma": "binh_len_men", "c_min": 30.0, "c_max": 37.0}
                    },
                    {
                        "so": 3,
                        "mo_ta": "Lồng miệng bóng bay cao su trùm kín cổ bình tam giác, buộc chặt dây cao su.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "binh_len_men", "ten": "da_buoc_bong", "gia_tri": 1}
                    },
                    {
                        "so": 4,
                        "mo_ta": "Đặt bình vào bể ổn nhiệt hoặc tủ ấm 35 °C trong 60 – 90 phút và theo dõi đường kính phồng của bóng bay.",
                        "dieu_kien": {"loai": "thoi_gian_u", "gio_min": 1.0}
                    }
                ],
                "ket_qua_mong_doi": "Bình xuất hiện nhiều bọt khí li ti dâng lên, bóng bay phồng căng tròn với đường kính > 15 cm do tích tụ khí CO2; dung dịch có mùi cồn thơm đặc trưng của ethanol.",
                "cau_hoi": [
                    {"hoi": "Phương trình hóa học tổng quát của quá trình lên men rượu là gì?", "dap_an": "C6H12O6 --(Nấm men)--> 2C2H5OH + 2CO2 + năng lượng (ATP)."},
                    {"hoi": "Nếu đun bình lên men ở 80 °C thì bóng bay có phồng không, vì sao?", "dap_an": "Không, vì nhiệt độ 80 °C làm chết tế bào nấm men và biến tính các enzyme lên men đường."},
                    {"hoi": "Làm thế nào để nhận biết khí tích tụ trong bóng bay chính là CO2?", "dap_an": "Dẫn khí trong bóng sục vào ống nghiệm chứa nước vôi trong Ca(OH)2, nước vôi bị vẩn đục trắng CaCO3."}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/7-5-metabolism-without-oxygen"
            },
            {
                "ma": "len_men_lactic",
                "ten": "Lên men lactic và làm sữa chua",
                "lop": 10,
                "sgk": "Sinh học 10 Kết nối tri thức - Bài 20: Thực hành lên men vi sinh vật",
                "muc_tieu": "Nắm vững nguyên lý lên men lactic đồng hình; làm được sữa chua thơm ngon đặc quánh; đo sự sụt giảm pH do tích tụ axit lactic.",
                "dung_cu": ["bo_lam_sua_chua", "may_do_ph", "ong_dong", "dua_thuy_tinh", "tu_am", "nhiet_ke"],
                "sinh_pham": ["sp_sua_tuoi", "sp_sua_chua_cai"],
                "buoc": [
                    {
                        "so": 1,
                        "mo_ta": "Làm ấm sữa tươi thanh trùng đến 40 – 42 °C (kiểm tra bằng nhiệt kế).",
                        "dieu_kien": {"loai": "nhiet_do", "ma": "nhiet_ke", "c_min": 38.0, "c_max": 43.0}
                    },
                    {
                        "so": 2,
                        "mo_ta": "Cho sữa chua cái vào theo tỷ lệ 1:10, dùng đũa thủy tinh khuấy nhẹ phân tán đều men vi khuẩn.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "bo_lam_sua_chua", "ten": "da_tron_men", "gia_tri": 1}
                    },
                    {
                        "so": 3,
                        "mo_ta": "Rót sữa vào các hũ thủy tinh, đậy nắp kín, đo pH ban đầu (~6,6).",
                        "dieu_kien": {"loai": "the_tich", "ma": "bo_lam_sua_chua", "ml_min": 100.0, "ml_max": 150.0}
                    },
                    {
                        "so": 4,
                        "mo_ta": "Xếp vào tủ ấm 40 – 42 °C trong 6 – 8 giờ.",
                        "dieu_kien": {"loai": "thoi_gian_u", "gio_min": 6.0}
                    }
                ],
                "ket_qua_mong_doi": "Sữa đông tụ tạo thành khối gel sánh mịn đặc quánh màu trắng ngà, vị chua thanh dịu mát; đo pH giảm từ 6,6 xuống khoảng 4,2 – 4,5.",
                "cau_hoi": [
                    {"hoi": "Tại sao sữa từ dạng lỏng lại chuyển thành dạng đặc quánh (đông tụ)?", "dap_an": "Vi khuẩn lactic biến đường lactose thành axit lactic làm giảm pH môi trường xuống dưới 4,6 (điểm đẳng điện pI của protein casein), làm casein kết tủa đông tụ."},
                    {"hoi": "Hai loài vi khuẩn chủ yếu tham gia vào quá trình lên men sữa chua là gì?", "dap_an": "Lactobacillus delbrueckii subsp. bulgaricus và Streptococcus thermophilus."},
                    {"hoi": "Tại sao cần giữ nhiệt độ ủ ấm ở mức 40 – 42 °C mà không để nguội ở nhiệt độ phòng?", "dap_an": "40 – 42 °C là nhiệt độ tối ưu cho hai chủng vi khuẩn lactic phát triển nhanh, ức chế vi khuẩn tạp nhiễm và thúc đẩy quá trình lên men đạt chuẩn."}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/7-5-metabolism-without-oxygen"
            },
            {
                "ma": "quang_hop",
                "ten": "Thí nghiệm quang hợp thải khí O2 ở cành rong đuôi chó",
                "lop": 11,
                "sgk": "Sinh học 11 Kết nối tri thức - Bài 5: Thực hành quang hợp ở thực vật",
                "muc_tieu": "Chứng minh thực vật thải khí O2 trong quá trình quang hợp dưới tác dụng của ánh sáng; khảo sát mối quan hệ giữa cường độ sáng và tốc độ tạo bọt khí.",
                "dung_cu": ["tram_quang_hop", "ong_nghiem", "coc_co_mo", "keo_nho", "que_dom", "dong_ho_bam_gio"],
                "sinh_pham": ["sp_rong_duoi_cho", "sp_nahco3"],
                "buoc": [
                    {
                        "so": 1,
                        "mo_ta": "Cắt vát cuống cành rong đuôi chó dưới nước, cắm ngược cuống hướng lên trên vào ống nghiệm chứa dung dịch NaHCO3 0,5%.",
                        "dieu_kien": {"loai": "dat_vat", "ma": "tram_quang_hop", "vao": "tram_quang_hop"}
                    },
                    {
                        "so": 2,
                        "mo_ta": "Bật đèn chiếu sáng cách ống nghiệm 10 cm, chờ 3 phút cho dòng bọt khí ổn định.",
                        "dieu_kien": {"loai": "nhiet_do", "ma": "tram_quang_hop", "c_min": 20.0, "c_max": 30.0}
                    },
                    {
                        "so": 3,
                        "mo_ta": "Đếm số bọt khí O2 thoát ra trong 1 phút ở các khoảng cách 10 cm, 20 cm và 30 cm.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "tram_quang_hop", "ten": "da_dem_bot", "gia_tri": 1}
                    },
                    {
                        "so": 4,
                        "mo_ta": "Dùng que đóm còn tàn đỏ đưa vào miệng ống nghiệm hứng khí để xác nhận khí O2.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "que_dom", "ten": "bung_chay", "gia_tri": 1}
                    }
                ],
                "ket_qua_mong_doi": "Bọt khí O2 li ti nổi lên liên tục từ vết cắt cành rong; khoảng cách đèn càng gần (cường độ sáng càng mạnh) thì số bọt khí thoát ra trong mỗi phút càng nhiều; khí tích tụ làm tàn đóm đỏ bùng cháy.",
                "cau_hoi": [
                    {"hoi": "Vai trò của dung dịch NaHCO3 0,5% trong thí nghiệm này là gì?", "dap_an": "NaHCO3 phân li tạo ion HCO3- giải phóng khí CO2 hòa tan cung cấp nguyên liệu cho pha tối quang hợp."},
                    {"hoi": "Oxy sinh ra trong quang hợp có nguồn gốc từ phân tử nào?", "dap_an": "Từ phân tử nước (H2O) qua quá trình quang phân ly nước ở pha sáng quang hợp tại màng thylakoid."},
                    {"hoi": "Khi đưa đèn ra quá xa (ví dụ 100 cm), tại sao không còn thấy bọt khí thoát ra?", "dap_an": "Khi cường độ sáng thấp hơn điểm bù ánh sáng, tốc độ quang hợp nhỏ hơn hoặc bằng tốc độ hô hấp, lượng O2 sinh ra không đủ để tích tụ thành bọt khí."}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/8-1-overview-of-photosynthesis"
            },
            {
                "ma": "thoat_hoi_nuoc",
                "ten": "Đo tốc độ thoát hơi nước của lá bằng potometer",
                "lop": 11,
                "sgk": "Sinh học 11 Cánh diều - Bài 3: Thực hành trao đổi nước và khoáng ở thực vật",
                "muc_tieu": "Đo tốc độ hút nước và thoát hơi nước của cành cây bằng áp kế bọt khí potometer; khảo sát ảnh hưởng của quạt gió và ánh sáng.",
                "dung_cu": ["tram_potometer", "keo_nho", "dong_ho_bam_gio", "binh_tia_nuoc"],
                "sinh_pham": ["sp_la_thai_lai"],
                "buoc": [
                    {
                        "so": 1,
                        "mo_ta": "Cắt cành cây tươi dưới nước, cắm khít chặt vào ống cao su potometer có chứa đầy nước cất không bọt khí.",
                        "dieu_kien": {"loai": "dat_vat", "ma": "tram_potometer", "vao": "tram_potometer"}
                    },
                    {
                        "so": 2,
                        "mo_ta": "Nhấc nhẹ đầu ống mao quản ra khỏi cốc nước 2 giây rồi nhúng lại để đón 1 bọt khí nhỏ làm mốc đo.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "tram_potometer", "ten": "co_bot_khi", "gia_tri": 1}
                    },
                    {
                        "so": 3,
                        "mo_ta": "Bấm giờ đo quãng đường bọt khí di chuyển trên thước chia vạch (mm/phút) ở điều kiện tĩnh và khi bật quạt gió.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "tram_potometer", "ten": "da_do_gio", "gia_tri": 1}
                    }
                ],
                "ket_qua_mong_doi": "Bọt khí di chuyển đều đặn dọc theo ống mao quản về phía cành cây; khi bật quạt gió hoặc chiếu đèn sáng, tốc độ dịch chuyển của bọt khí tăng lên rõ rệt (tăng 2 – 3 lần).",
                "cau_hoi": [
                    {"hoi": "Động lực chính kéo dòng nước từ dưới lên trong cây là gì?", "dap_an": "Động lực đầu trên do lực hút của quá trình thoát hơi nước qua khí khổng ở lá cây."},
                    {"hoi": "Tại sao gió làm tăng tốc độ thoát hơi nước của cây?", "dap_an": "Gió thổi bay lớp không khí ẩm bão hòa sát bề mặt lá, làm tăng độ dốc thế nước giữa khoang dưới khí khổng và khí quyển."},
                    {"hoi": "Tại sao phải cắt cành cây ở dưới mặt nước trước khi cắm vào máy đo?", "dap_an": "Để ngăn không khí lọt vào các mạch gỗ (hiện tượng tắc mạch do bóng khí - embolism) làm gián đoạn dòng vận chuyển nước."}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/30-5-transport-of-water-and-solutes-in-plants"
            },
            {
                "ma": "huong_sang",
                "ten": "Thí nghiệm tính hướng sáng của ngọn cây mầm",
                "lop": 11,
                "sgk": "Sinh học 11 Chân trời sáng tạo - Bài 14: Thực hành cảm ứng ở sinh vật",
                "muc_tieu": "Chứng minh tính hướng sáng dương của thân mầm hạt đậu; phân tích vai trò phân bố bất đối xứng của hormone auxin.",
                "dung_cu": ["hop_huong_sang", "binh_tuoi", "dong_ho_bam_gio"],
                "sinh_pham": ["sp_chuoi_chin"],
                "buoc": [
                    {
                        "so": 1,
                        "mo_ta": "Chuẩn bị 2 chậu cây mầm đậu xanh 3 ngày tuổi mọc thẳng đứng đồng đều.",
                        "dieu_kien": {"loai": "dat_vat", "ma": "hop_huong_sang", "vao": "hop_huong_sang"}
                    },
                    {
                        "so": 2,
                        "mo_ta": "Đặt Chậu 1 vào hộp có khe chiếu sáng đơn hướng một bên thành; Chậu 2 để ngoài ánh sáng tán xạ tự nhiên đều 4 phía (đối chứng).",
                        "dieu_kien": {"loai": "trang_thai", "ma": "hop_huong_sang", "ten": "da_chieu_sang", "gia_tri": 1}
                    },
                    {
                        "so": 3,
                        "mo_ta": "Tưới nước giữ ẩm nhẹ nhàng hàng ngày, quan sát hướng sinh trưởng của ngọn thân mầm sau 24 và 48 giờ.",
                        "dieu_kien": {"loai": "thoi_gian_u", "gio_min": 24.0}
                    }
                ],
                "ket_qua_mong_doi": "Thân mầm trong hộp tối uốn cong rõ rệt về phía khe hở có ánh sáng (hướng sáng dương); chậu đối chứng thân mầm tiếp tục mọc thẳng đứng.",
                "cau_hoi": [
                    {"hoi": "Hormone thực vật nào chịu trách nhiệm chính gây ra hiện tượng hướng sáng ở ngọn cây?", "dap_an": "Auxin (chủ yếu là AIA - Axit indol axetic)."},
                    {"hoi": "Tại sao ngọn cây lại uốn cong về phía có nguồn sáng đơn hướng?", "dap_an": "Dưới tác dụng của ánh sáng, auxin vận chuyển chủ động dịch chuyển về phía tối; phía tối có nhiều auxin hơn kích thích tế bào dãn dài nhanh hơn phía sáng đẩy ngọn uốn về phía sáng."},
                    {"hoi": "Bộ phận nào của cây có tính hướng sáng âm (tránh xa ánh sáng)?", "dap_an": "Rễ cây (hướng đất dương, hướng sáng âm)."}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/30-6-plant-sensory-systems-and-responses"
            },
            {
                "ma": "tach_chiet_adn",
                "ten": "Tách chiết ADN thô từ mô thực vật (chuối / dâu tây)",
                "lop": 10,
                "sgk": "Sinh học 10 Kết nối tri thức - Bài 6: Thực hành tách chiết axit nucleic",
                "muc_tieu": "Nắm vững các bước phá vỡ màng tế bào, kết tủa protein và thu nhận sợi ADN thô kết tủa trong cồn lạnh.",
                "dung_cu": ["coi_chay_su", "coc_co_mo", "pheu_loc", "ong_nghiem", "dua_thuy_tinh", "can_phan_tich"],
                "sinh_pham": ["sp_chuoi_chin", "sp_con_96_lanh", "sp_nacl_10"],
                "buoc": [
                    {
                        "so": 1,
                        "mo_ta": "Cắt 50 g chuối chín cho vào cối sứ, nghiền nhuyễn với 50 mL dung dịch ly giải (nước cất + 5 mL nước rửa chén + 2 g muối NaCl).",
                        "dieu_kien": {"loai": "the_tich", "ma": "coc_co_mo", "ml_min": 45.0, "ml_max": 55.0}
                    },
                    {
                        "so": 2,
                        "mo_ta": "Lọc hỗn hợp nghiền qua phễu có giấy lọc thu lấy phần dịch chiết trong suốt vào ống nghiệm.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "pheu_loc", "ten": "da_loc", "gia_tri": 1}
                    },
                    {
                        "so": 3,
                        "mo_ta": "Nghiêng ống nghiệm 45°, rót từ từ 10 mL cồn 96% lạnh (-20 °C) chảy dọc theo thành ống để tạo lớp cồn nổi trên mặt dịch chiết.",
                        "dieu_kien": {"loai": "the_tich", "ma": "ong_nghiem", "ml_min": 8.0, "ml_max": 12.0}
                    },
                    {
                        "so": 4,
                        "mo_ta": "Quan sát các sợi ADN màu trắng đục kết tủa xuất hiện tại ranh giới phân cách giữa lớp cồn và lớp dịch chiết; dùng que thủy tinh cuộn vớt sợi ADN.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "dua_thuy_tinh", "ten": "vot_dna", "gia_tri": 1}
                    }
                ],
                "ket_qua_mong_doi": "Tại mặt phân cách hai pha xuất hiện lớp kết tủa nhầy dạng sợi màu trắng đục của các phân tử ADN cuộn lại với nhau; dùng đũa thủy tinh có thể xoay và vớt búi sợi ADN lên.",
                "cau_hoi": [
                    {"hoi": "Vai trò của nước rửa chén (chất tẩy rửa) trong thí nghiệm tách chiết ADN là gì?", "dap_an": "Hòa tan lớp kép photpholipit của màng sinh chất và màng nhân, phá vỡ tế bào giải phóng ADN vào dịch chiết."},
                    {"hoi": "Tại sao phải thêm muối NaCl vào dung dịch ly giải?", "dap_an": "Ion Na+ liên kết với nhóm photphat tích điện âm của khung ADN, trung hòa điện tích giúp các phân tử ADN dễ kết tụ lại với nhau."},
                    {"hoi": "Tại sao phải dùng cồn lạnh (-20 °C) để kết tủa ADN?", "dap_an": "ADN không tan trong cồn lạnh nồng độ cao (độ tan giảm cực mạnh ở nhiệt độ âm), đồng thời nhiệt độ lạnh ức chế hoạt tính enzyme DNase phá hủy ADN."}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/10-1-cloning-and-genetic-engineering"
            },
            {
                "ma": "dem_vi_sinh",
                "ten": "Đếm vi sinh vật bằng phương pháp pha loãng và trải đĩa",
                "lop": 10,
                "sgk": "Sinh học 10 Cánh diều - Bài 17: Thực hành một số phương pháp phân tích vi sinh vật",
                "muc_tieu": "Thực hiện thành thạo kỹ thuật pha loãng bậc 10; trang đều mẫu vi sinh lên đĩa thạch; đếm khuẩn lạc và tính mật độ CFU/mL của dịch vi sinh vật gốc.",
                "dung_cu": ["bo_ong_pha_loang", "micropipet_p1000", "micropipet_p200", "hop_dau_con", "que_trang", "den_con", "tu_am", "may_dem_khuan_lac"],
                "sinh_pham": ["sp_dia_thach_pca", "sp_nacl_sinh_ly", "sp_giong_e_coli"],
                "buoc": [
                    {
                        "so": 1,
                        "mo_ta": "Hút 1,0 mL dịch huyền phù vi khuẩn vào ống 1 (9 mL nước muối sinh lý), lắc vortex đều được nồng độ pha loãng 10^-1.",
                        "dieu_kien": {"loai": "the_tich", "ma": "micropipet_p1000", "ml_min": 0.95, "ml_max": 1.05}
                    },
                    {
                        "so": 2,
                        "mo_ta": "Thay đầu côn, hút 1,0 mL từ ống 10^-1 sang ống tiếp theo tạo chuỗi nồng độ từ 10^-1 đến 10^-6.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "bo_ong_pha_loang", "ten": "pha_loang_xong", "gia_tri": 1}
                    },
                    {
                        "so": 3,
                        "mo_ta": "Dùng pipet hút 0,1 mL từ các nấc 10^-4, 10^-5, 10^-6 nhỏ lên bề mặt đĩa thạch PCA vô trùng.",
                        "dieu_kien": {"loai": "the_tich", "ma": "micropipet_p200", "ml_min": 0.09, "ml_max": 0.11}
                    },
                    {
                        "so": 4,
                        "mo_ta": "Dùng que trang thủy tinh tiệt trùng trang đều dịch mẫu khắp mặt đĩa; úp ngược đĩa ủ ở 37 °C trong 24 giờ.",
                        "dieu_kien": {"loai": "thoi_gian_u", "gio_min": 24.0}
                    },
                    {
                        "so": 5,
                        "mo_ta": "Đếm số khuẩn lạc trên các đĩa có số lượng từ 30 – 300 khuẩn lạc và tính nồng độ CFU/mL.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "may_dem_khuan_lac", "ten": "da_dem_xong", "gia_tri": 1}
                    }
                ],
                "ket_qua_mong_doi": "Đĩa 10^-4 khuẩn lạc dày đặc chồng lấn; đĩa 10^-5 đếm được khoảng 120 khuẩn lạc tròn bờ đều màu trắng đục; đĩa 10^-6 đếm được khoảng 12 khuẩn lạc; tính được nồng độ vi khuẩn ban đầu ~ 1,2 x 10^8 CFU/mL.",
                "cau_hoi": [
                    {"hoi": "Công thức tính mật độ vi sinh vật CFU/mL từ số đếm khuẩn lạc là gì?", "dap_an": "CFU/mL = (Số khuẩn lạc trung bình) / (Thể tích cấy mL x Hệ số pha loãng)."},
                    {"hoi": "Tại sao khoảng đếm khuẩn lạc tin cậy chuẩn thống kê quốc tế là từ 30 đến 300 khuẩn lạc/đĩa?", "dap_an": "Dưới 30 khuẩn lạc sai số ngẫu nhiên quá lớn; trên 300 khuẩn lạc các khuẩn lạc dính chồng lấn vào nhau và cạnh tranh dinh dưỡng không đếm chính xác được."},
                    {"hoi": "Tại sao phải thay đầu côn micropipet mới ở mỗi nấc pha loãng liên tiếp?", "dap_an": "Nếu không thay đầu côn, lượng vi khuẩn bám dính ở thành trong và ngoài đầu côn sẽ làm nồng độ ống sau bị thừa nghiêm trọng, sai số có thể gấp 10 – 100 lần."}
                ],
                "nguon": "https://www.iso.org/standard/53612.html"
            },
            {
                "ma": "cay_ria",
                "ten": "Kỹ thuật cấy ria phân lập đơn khuẩn lạc vi sinh vật",
                "lop": 10,
                "sgk": "Sinh học 10 Kết nối tri thức - Bài 18: Nuôi cấy và sinh trưởng của vi sinh vật",
                "muc_tieu": "Thực hiện thành thạo kỹ thuật khử trùng que cấy trên ngọn lửa đèn cồn; cấy ria 4 vùng phân lập thành công các khuẩn lạc đơn lẻ thuần khiết.",
                "dung_cu": ["que_cay_vong", "den_con", "dia_petri", "tu_am", "but_ghi_nhan"],
                "sinh_pham": ["sp_dia_thach_lb", "sp_giong_e_coli"],
                "buoc": [
                    {
                        "so": 1,
                        "mo_ta": "Đốt đỏ tiệt trùng que cấy vòng từ chuôi đến đầu vòng trên ngọn lửa đèn cồn, để nguội 10 giây trong vùng vô trùng.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "que_cay_vong", "ten": "da_dot_tiet_trung", "gia_tri": 1}
                    },
                    {
                        "so": 2,
                        "mo_ta": "Chạm nhẹ đầu que cấy lấy một lượng nhỏ vi khuẩn, ria các đường dích dắc sít nhau ở vùng 1 (chiếm 1/4 đĩa).",
                        "dieu_kien": {"loai": "trang_thai", "ma": "que_cay_vong", "ten": "ria_vung_1", "gia_tri": 1}
                    },
                    {
                        "so": 3,
                        "mo_ta": "Đốt đỏ que cấy, để nguội, xoay đĩa 90°, lướt que cấy qua mép vùng 1 hai lần rồi ria tiếp các nét ở vùng 2.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "que_cay_vong", "ten": "ria_vung_2", "gia_tri": 1}
                    },
                    {
                        "so": 4,
                        "mo_ta": "Lặp lại tương tự cho vùng 3 và vùng 4 (ở vùng 4 các nét ria thưa rộng và không chạm lại vùng 1).",
                        "dieu_kien": {"loai": "trang_thai", "ma": "que_cay_vong", "ten": "ria_vung_4", "gia_tri": 1}
                    },
                    {
                        "so": 5,
                        "mo_ta": "Đốt tiệt trùng que cấy, úp ngược đĩa petri, ủ ở 37 °C trong 24 giờ.",
                        "dieu_kien": {"loai": "thoi_gian_u", "gio_min": 24.0}
                    }
                ],
                "ket_qua_mong_doi": "Ở vùng 1 vi khuẩn mọc dày đặc thành thảm; vùng 2 và 3 mật độ giảm dần; ở vùng 4 xuất hiện các khuẩn lạc đơn lẻ tách rời hoàn toàn có hình tròn bóng đều, kích thước 1,5 – 2,0 mm.",
                "cau_hoi": [
                    {"hoi": "Mục đích quan trọng nhất của kỹ thuật cấy ria phân lập là gì?", "dap_an": "Làm loãng dần mật độ tế bào vi khuẩn trên mặt thạch để thu nhận được các khuẩn lạc đơn lẻ thuần khiết bắt nguồn từ 1 tế bào duy nhất."},
                    {"hoi": "Tại sao phải đốt đỏ que cấy và để nguội giữa các lần ria chuyển vùng?", "dap_an": "Đốt đỏ để tiêu diệt hết lượng vi khuẩn còn thừa bám trên que cấy; để nguội nhằm tránh làm chết vi khuẩn ở vùng trước khi kéo sang vùng sau."},
                    {"hoi": "Nếu ở vùng 4 người thao tác vô tình kéo que cấy quẹt chạm lại vào vùng 1 thì kết quả sẽ ra sao?", "dap_an": "Vùng 4 sẽ bị nhiễm lại mật độ vi khuẩn cực cao từ vùng 1, không thu được khuẩn lạc đơn lẻ."}
                ],
                "nguon": "https://openstax.org/books/microbiology/pages/2-4-staining-microscopic-specimens"
            },
            {
                "ma": "nhuom_gram",
                "ten": "Kỹ thuật nhuộm Gram phân biệt vi khuẩn",
                "lop": 10,
                "sgk": "Sinh học 10 Chân trời sáng tạo - Bài 17: Thực hành một số phương pháp nghiên cứu vi sinh vật",
                "muc_tieu": "Nắm vững nguyên lý và các bước kỹ thuật nhuộm Gram; phân biệt được vi khuẩn Gram dương (bắt màu tím) và Gram âm (bắt màu hồng).",
                "dung_cu": ["dao_nhuom_gram", "lam_kinh", "que_cay_vong", "den_con", "kinh_hien_vi", "giay_tham", "lo_dau_soi"],
                "sinh_pham": ["sp_bo_nhuom_gram", "sp_giong_e_coli", "sp_giong_b_subtilis", "sp_dau_soi_kinh"],
                "buoc": [
                    {
                        "so": 1,
                        "mo_ta": "Làm vết bôi mỏng hỗn hợp vi khuẩn E. coli và B. subtilis lên lam kính, để khô tự nhiên rồi cố định nhiệt lướt qua ngọn lửa đèn cồn 3 lần.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "lam_kinh", "ten": "da_co_dinh", "gia_tri": 1}
                    },
                    {
                        "so": 2,
                        "mo_ta": "Nhỏ dung dịch tinh thể tím Crystal Violet phủ kín vết bôi trong 60 giây, rửa nhẹ dưới vòi nước cất.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "dao_nhuom_gram", "ten": "da_nhuom_tim", "gia_tri": 1}
                    },
                    {
                        "so": 3,
                        "mo_ta": "Phủ dung dịch Lugol (I2/KI) trong 60 giây làm chất gắn màu tạo phức hợp tím-iodine, rửa nước.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "dao_nhuom_gram", "ten": "da_nhuom_lugol", "gia_tri": 1}
                    },
                    {
                        "so": 4,
                        "mo_ta": "Tẩy màu bằng cồn 96% nghiêng lam kính trong 15 – 30 giây cho tới khi dòng cồn chảy xuống không còn màu tím, rửa nước ngay lập tức.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "dao_nhuom_gram", "ten": "da_tay_con", "gia_tri": 1}
                    },
                    {
                        "so": 5,
                        "mo_ta": "Nhuộm bổ sung bằng dung dịch Safranin trong 60 giây, rửa sạch nước, thấm khô tiêu bản.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "dao_nhuom_gram", "ten": "da_nhuom_safranin", "gia_tri": 1}
                    },
                    {
                        "so": 6,
                        "mo_ta": "Nhỏ 1 giọt dầu soi, quan sát dưới kính hiển vi ở vật kính 100x dầu.",
                        "dieu_kien": {"loai": "vat_kinh", "gia_tri": 100}
                    }
                ],
                "ket_qua_mong_doi": "Trực khuẩn Bacillus subtilis giữ được màu tím thẫm (Gram dương); trực khuẩn E. coli bị tẩy cồn mất màu tím và bắt màu hồng đỏ của Safranin (Gram âm).",
                "cau_hoi": [
                    {"hoi": "Sự khác biệt căn bản về cấu trúc vách tế bào giữa vi khuẩn Gram dương và Gram âm là gì?", "dap_an": "Vi khuẩn Gram dương có vách peptidoglycan rất dày (20 – 80 nm) giữ chặt phức hợp tím-iod; vi khuẩn Gram âm có vách peptidoglycan mỏng (2 – 7 nm) và có màng ngoài lipopolysaccharide dễ bị cồn hòa tan."},
                    {"hoi": "Bước nào là bước nhạy cảm và quan trọng nhất quyết định sự thành bại của kỹ thuật nhuộm Gram?", "dap_an": "Bước tẩy cồn 96% (nếu tẩy quá ngắn Gram âm vẫn tím; nếu tẩy quá lâu cồn hòa tan cả vách Gram dương biến thành hồng)."},
                    {"hoi": "Tại sao phải cố định vết bôi tế bào bằng nhiệt trước khi nhuộm?", "dap_an": "Để giết chết vi khuẩn, làm đông tụ protein giúp tế bào gắn chặt vào phiến kính không bị dòng nước rửa trôi đi."}
                ],
                "nguon": "https://www.cdc.gov/labtraining/docs/job-aids/gram-stain/Gram_Stain_Job_Aid.pdf"
            },
            {
                "ma": "pcr_dien_di",
                "ten": "Khuếch đại gen bằng PCR và phân tích sản phẩm qua điện di gel agarose",
                "lop": 12,
                "sgk": "Sinh học 12 Kết nối tri thức - Bài 4: Công nghệ gen và ứng dụng",
                "muc_tieu": "Nắm vững nguyên lý phản ứng chuỗi polymerase PCR và kỹ thuật điện di gel agarose; vận hành máy PCR và đọc kích thước đoạn ADN trên gel doc.",
                "dung_cu": ["micropipet_p10", "micropipet_p200", "hop_dau_con", "ong_pcr", "gia_ong_pcr", "hop_da", "may_ly_tam_mini", "may_pcr", "be_dien_di", "nguon_dien_di", "khay_duc_gel", "gel_doc"],
                "sinh_pham": ["sp_kit_pcr_mastermix", "sp_cap_moi_pcr", "sp_nuoc_khong_nuclease", "sp_dna_ladder_100bp", "sp_agarose_bot", "sp_dem_tae_50x", "sp_nhuom_adn_gelgreen", "sp_dem_nap_mau_6x"],
                "buoc": [
                    {
                        "so": 1,
                        "mo_ta": "Trong tủ thao tác PCR, pha mỗi ống 20 µL trên đá bằng micropipet P10 (mỗi thuốc thử một đầu côn mới): 10 µL Master Mix 2X + 1 µL cặp mồi GFP + 1 µL ADN khuôn + 8 µL nước. Ống đối chứng âm thay ADN khuôn bằng nước.",
                        "dieu_kien": {"loai": "the_tich", "ma": "micropipet_p10", "ml_min": 0.001, "ml_max": 0.015}
                    },
                    {
                        "so": 2,
                        "mo_ta": "Ly tâm spindown nhẹ, đặt ống vào máy PCR, cài chu trình: 95 °C (3 phút) -> [95 °C (30s) -> 55 °C (30s) -> 72 °C (45s)] x 30 chu kỳ -> 72 °C (5 phút).",
                        "dieu_kien": {"loai": "trang_thai", "ma": "may_pcr", "ten": "chay_chu_trinh", "gia_tri": 1}
                    },
                    {
                        "so": 3,
                        "mo_ta": "Đúc gel agarose 1,0% trong đệm 1X TAE có bổ sung thuốc nhuộm GelGreen, cắm lược 8 giếng.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "khay_duc_gel", "ten": "gel_da_dong", "gia_tri": 1}
                    },
                    {
                        "so": 4,
                        "mo_ta": "Đặt gel vào bể điện di ngập đệm TAE, tra 5 µL DNA Ladder vào giếng 1 và 10 µL sản phẩm PCR vào các giếng tiếp theo.",
                        "dieu_kien": {"loai": "the_tich", "ma": "micropipet_p10", "ml_min": 0.005, "ml_max": 0.01}
                    },
                    {
                        "so": 5,
                        "mo_ta": "Chạy điện di ở điện áp 100V trong 45 phút cho tới khi vạch màu chỉ thị chạy được 2/3 bản gel.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "nguon_dien_di", "ten": "dien_di_xong", "gia_tri": 1}
                    },
                    {
                        "so": 6,
                        "mo_ta": "Chuyển gel sang buồng chụp Gel Doc, bật ánh sáng xanh Blue LED 470 nm chụp ảnh và đối chiếu kích thước vạch.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "gel_doc", "ten": "chup_anh_xong", "gia_tri": 1}
                    }
                ],
                "ket_qua_mong_doi": "Trên ảnh chụp gel huỳnh quang xuất hiện 1 vạch ADN sáng rõ ở khoảng 740 bp (gen GFP 720 bp + hai đuôi mồi mang vị trí cắt EcoRI / BamHI), nằm giữa vạch 700 và 800 bp của thang chuẩn; giếng đối chứng âm không có vạch.",
                "cau_hoi": [
                    {"hoi": "Ba giai đoạn cơ bản của một chu kỳ nhiệt trong phản ứng PCR là gì?", "dap_an": "Biến tính (Denaturation, 94 – 95 °C), Gắn mồi (Annealing, 50 – 60 °C) và Kéo dài (Extension, 72 °C)."},
                    {"hoi": "Tại sao trong điện di gel agarose, các đoạn ADN luôn di chuyển từ cực âm sang cực dương?", "dap_an": "Bộ khung photphat của phân tử ADN chứa nhiều gốc photphat PO4 3- mang điện tích âm ở pH trung tính, nên bị hút về cực dương trong điện trường."},
                    {"hoi": "Mối quan hệ giữa kích thước phân tử ADN (bp) và tốc độ di chuyển trên gel agarose như thế nào?", "dap_an": "Đoạn ADN càng nhỏ càng dễ len lỏi qua mạng lưới gel xốp nên di chuyển nhanh hơn; tốc độ di chuyển tỉ lệ nghịch với log10 của số cặp bazơ (bp)."}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/10-1-cloning-and-genetic-engineering"
            },
            {
                "ma": "duong_cong_od600",
                "ten": "Đo quang phổ OD600 và dựng đường cong sinh trưởng vi khuẩn",
                "lop": 10,
                "sgk": "Sinh học 10 Kết nối tri thức - Bài 18: Nuôi cấy và sinh trưởng của vi sinh vật",
                "muc_tieu": "Nắm vững nguyên lý đo mật độ quang học vi sinh; xác định được 4 pha sinh trưởng của quần thể vi khuẩn E. coli và tính thời gian thế hệ (g).",
                "dung_cu": ["may_quang_pho_od600", "cuvet", "tu_am", "micropipet_p1000", "dong_ho_bam_gio"],
                "sinh_pham": ["sp_ong_canh_lb_10ml", "sp_giong_e_coli"],
                "buoc": [
                    {
                        "so": 1,
                        "mo_ta": "Cấy 100 µL vi khuẩn E. coli vào ống chứa 10 mL canh thang LB vô trùng, lắc đều.",
                        "dieu_kien": {"loai": "the_tich", "ma": "micropipet_p1000", "ml_min": 0.09, "ml_max": 0.11}
                    },
                    {
                        "so": 2,
                        "mo_ta": "Đặt ống vào máy lắc ở 37 °C, nuôi cấy liên tục.",
                        "dieu_kien": {"loai": "nhiet_do", "ma": "tu_am", "c_min": 36.0, "c_max": 38.0}
                    },
                    {
                        "so": 3,
                        "mo_ta": "Chuẩn zero máy quang phổ ở bước sóng 600 nm bằng cuvet chứa môi trường LB trắng (Blank).",
                        "dieu_kien": {"loai": "trang_thai", "ma": "may_quang_pho_od600", "ten": "chuan_blank", "gia_tri": 1}
                    },
                    {
                        "so": 4,
                        "mo_ta": "Cứ sau mỗi 30 phút, hút 1 mL dịch nuôi cấy cho vào cuvet đo giá trị OD600 trong suốt 4 – 6 giờ.",
                        "dieu_kien": {"loai": "thoi_gian_u", "gio_min": 4.0}
                    },
                    {
                        "so": 5,
                        "mo_ta": "Vẽ đồ thị logarit OD600 theo thời gian và xác định thời gian thế hệ của vi khuẩn trong pha lũy thừa.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "may_quang_pho_od600", "ten": "da_ve_do_thi", "gia_tri": 1}
                    }
                ],
                "ket_qua_mong_doi": "Đồ thị đường cong sinh trưởng hiển thị rõ 4 pha: Pha tiềm phát (Lag, 0 – 30 phút, OD tăng chậm), Pha lũy thừa (Log, 30 – 180 phút, OD tăng nhanh theo cấp số nhân), Pha cân bằng (Stationary, 180 – 300 phút, OD đạt cực đại ~1,5 – 2,0), Pha suy vong.",
                "cau_hoi": [
                    {"hoi": "Ý nghĩa sinh học của pha tiềm phát (Lag phase) là gì?", "dap_an": "Vi khuẩn đang thích ứng với môi trường mới, tổng hợp các enzyme cần thiết và chuẩn bị nguyên liệu nhân đôi ADN, chưa phân chia số lượng."},
                    {"hoi": "Trong pha nào của đường cong sinh trưởng thì tốc độ sinh trưởng của quần thể đạt cực đại?", "dap_an": "Pha lũy thừa (pha logarit)."},
                    {"hoi": "Thời gian thế hệ (g) của E. coli K-12 trong điều kiện tối ưu 37 °C là khoảng bao nhiêu phút?", "dap_an": "Khoảng 20 phút."}
                ],
                "nguon": "https://openstax.org/books/microbiology/pages/9-1-how-microbes-grow"
            },
            {
                "ma": "ghep_gen_adn_tai_to_hop",
                "ten": "Ghép gen: tạo ADN tái tổ hợp pUC19 – GFP và chọn dòng khuẩn lạc xanh / trắng",
                "lop": 12,
                "sgk": "Sinh học 12 Kết nối tri thức - Bài 4: Công nghệ gen và ứng dụng (quy trình tạo ADN tái tổ hợp, chuyển gen vào tế bào nhận)",
                "muc_tieu": "Thực hiện được quy trình tạo ADN tái tổ hợp: cắt thể truyền và gen bằng cùng enzyme cắt giới hạn, nối bằng ligaza, biến nạp vào E. coli và chọn dòng bằng ampicillin + X-gal; kiểm tra lại bằng cắt enzyme và điện di.",
                "dung_cu": ["micropipet_p10", "hop_dau_con", "ong_eppendorf", "hop_da", "khoi_gia_nhiet_kho", "be_on_nhiet", "dia_petri", "tu_am", "be_dien_di", "nguon_dien_di", "gel_doc"],
                "sinh_pham": ["sp_plasmid_puc19", "sp_enzyme_ecori", "sp_enzyme_bamhi", "sp_ligaza_t4", "sp_te_bao_kha_bien_dh5a", "sp_dia_lb_amp_xgal", "sp_dna_ladder_100bp"],
                "buoc": [
                    {
                        "so": 1,
                        "mo_ta": "Cắt plasmid pUC19 và sản phẩm PCR gen GFP bằng cùng cặp enzyme EcoRI + BamHI, ủ 37 °C 1 giờ: khung plasmid 2665 bp, gen 726 bp có hai đầu dính khác nhau.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "tram_ghep_gen", "ten": "cat_gioi_han", "gia_tri": 1}
                    },
                    {
                        "so": 2,
                        "mo_ta": "Trộn gen : khung theo tỉ lệ 3 : 1, thêm ADN ligaza T4 và đệm (ATP, Mg2+), ủ 16 °C: tạo plasmid tái tổ hợp 3391 bp.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "tram_ghep_gen", "ten": "noi_ligaza", "gia_tri": 1}
                    },
                    {
                        "so": 3,
                        "mo_ta": "Biến nạp vào tế bào E. coli khả biến giữ trên đá: sốc nhiệt 42 °C trong 45 giây, đặt lại vào đá, thêm LB và lắc 37 °C 1 giờ.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "tram_ghep_gen", "ten": "bien_nap", "gia_tri": 1}
                    },
                    {
                        "so": 4,
                        "mo_ta": "Trải lên đĩa LB + ampicillin + X-gal/IPTG, ủ 37 °C qua đêm; đếm khuẩn lạc trắng (mang gen ghép làm hỏng lacZα) và xanh (plasmid không có gen).",
                        "dieu_kien": {"loai": "thoi_gian_u", "gio_min": 16.0}
                    },
                    {
                        "so": 5,
                        "mo_ta": "Chọn khuẩn lạc trắng, tách plasmid, cắt kiểm tra bằng EcoRI + BamHI rồi điện di cạnh thang chuẩn 100 bp.",
                        "dieu_kien": {"loai": "trang_thai", "ma": "gel_doc", "ten": "chup_anh_xong", "gia_tri": 1}
                    }
                ],
                "ket_qua_mong_doi": "Đĩa có nhiều khuẩn lạc trắng, rất ít khuẩn lạc xanh. Plasmid của khuẩn lạc trắng cắt bằng EcoRI + BamHI cho 2 băng: khoảng 2665 bp (khung pUC19) và 726 bp (gen GFP). Bỏ ligaza thì không có khuẩn lạc; cắt plasmid bằng một enzyme thì phần lớn khuẩn lạc xanh; đĩa thiếu ampicillin thì vi khuẩn mọc kín.",
                "cau_hoi": [
                    {"hoi": "Vì sao phải cắt thể truyền và gen cần chuyển bằng cùng một loại enzyme cắt giới hạn?", "dap_an": "Để hai phân tử có các đầu dính bổ sung với nhau, nhờ đó ADN ligaza nối được gen vào thể truyền tạo ADN tái tổ hợp."},
                    {"hoi": "Vai trò của gen kháng ampicillin và gen lacZα trên plasmid pUC19 là gì?", "dap_an": "Gen kháng ampicillin giúp chọn tế bào đã nhận plasmid (chỉ chúng sống trên đĩa có ampicillin); vùng cắt nằm trong lacZα nên khi gen được ghép vào, lacZα hỏng, khuẩn lạc không phân giải X-gal và có màu trắng."},
                    {"hoi": "Tại sao dùng hai enzyme khác nhau (EcoRI và BamHI) lại cho nhiều khuẩn lạc trắng hơn dùng một enzyme?", "dap_an": "Hai đầu khung plasmid khác nhau nên không tự nối lại được; chỉ khi có gen chèn vào mới đóng vòng, đồng thời gen được ghép đúng chiều."}
                ],
                "nguon": "https://openstax.org/books/biology-2e/pages/17-1-biotechnology"
            }
        ]
    }
    f = DATA_SINH / "bai_thi_nghiem.json"
    f.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Created {f} with {len(data['bai_thi_nghiem'])} experiment protocols.")


if __name__ == "__main__":
    DATA_SINH.mkdir(parents=True, exist_ok=True)
    tao_tieu_ban()
    tao_chung_vi_sinh()
    tao_moi_truong()
    tao_sinh_pham()
    tao_bai_thi_nghiem()
