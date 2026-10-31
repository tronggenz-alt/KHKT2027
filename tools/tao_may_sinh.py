"""Tạo hồ sơ máy mới (gel_doc, may_do_od600) và bổ sung cho 6 máy hiện có."""

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DATA_MAY = ROOT / "data" / "may"


def tao_gel_doc():
    doc = {
        "ma": "gel_doc",
        "ten": "Hệ thống chụp ảnh và tài liệu hóa gel điện di (Gel Doc)",
        "may_mau": "Bio-Rad Gel Doc EZ System, buồng tối tích hợp cảm biến CCD 1,4 megapixel, nguồn sáng UV 302 nm và ánh sáng xanh Blue LED 470 nm",
        "nguyen_ly": [
            "Phát hiện huỳnh quang của các chất nhuộm axit nucleic (như Ethidium Bromide, GelGreen, GelRed, SYBR Safe) liên kết xen vào giữa các cặp bazơ của chuỗi xoắn kép DNA/RNA khi được kích thích bởi bức xạ tử ngoại UV (~302 nm) hoặc ánh sáng xanh lam (~470 nm).",
            "Chất huỳnh quang hấp thụ photon năng lượng cao và tái phát xạ photon năng lượng thấp hơn (bước sóng dài hơn trong vùng khả kiến: xanh lục ~530 nm hoặc cam đỏ ~605 nm), được ghi lại qua thấu kính quang học có kính lọc tương ứng tới cảm biến hình ảnh số.",
            "Cường độ tín hiệu huỳnh quang của từng vạch DNA trên ảnh tỷ lệ tuyến tính với khối lượng DNA trong vạch (ng), cho phép định lượng nồng độ và xác định kích thước đoạn DNA bằng cách so sánh vị trí di chuyển với thang chuẩn DNA Ladder (log10 bp)."
        ],
        "cong_thuc": [
            "I_fluor = k * m_DNA",
            "d_migration = -a * log10(bp) + b"
        ],
        "bo_phan": [
            {
                "ma": "buong_toi",
                "ten": "Buồng tối quang học kín",
                "chuc_nang": "Ngăn ánh sáng tạp từ môi trường xung quanh lọt vào làm giảm tỷ lệ tín hiệu trên nhiễu (SNR)."
            },
            {
                "ma": "khay_mau_uv_blue",
                "ten": "Khay trượt chứa gel UV/Blue LED",
                "chuc_nang": "Bề mặt truyền xạ tia tử ngoại UV 302 nm và ánh sáng xanh 470 nm kích thích huỳnh quang."
            },
            {
                "ma": "camera_ccd",
                "ten": "Camera kỹ thuật số độ nhạy cao 1,4 MP",
                "chuc_nang": "Thu nhận photon phát xạ từ vạch gel qua kính lọc quang học 530 nm / 605 nm."
            },
            {
                "ma": "khoa_lien_dong_uv",
                "ten": "Khóa an toàn liên động cửa buồng",
                "chuc_nang": "Tự động ngắt nguồn phát tia UV ngay khi mở cửa để bảo vệ mắt và da người dùng."
            },
            {
                "ma": "man_hinh_dieu_khien",
                "ten": "Màn hình giao diện vận hành",
                "chuc_nang": "Hiển thị ảnh thời gian thực, điều chỉnh thời gian phơi sáng và xuất ảnh phân tích."
            }
        ],
        "thong_so": [
            {
                "ten": "Bước sóng kích thích UV trung",
                "gia_tri": "302 nm",
                "loai": "tiêu chuẩn",
                "nguon": "https://www.bio-rad.com/en-us/product/gel-doc-ez-system"
            },
            {
                "ten": "Bước sóng kích thích Blue LED",
                "gia_tri": "470 nm",
                "loai": "tiêu chuẩn",
                "nguon": "https://www.bio-rad.com/en-us/product/gel-doc-ez-system"
            },
            {
                "ten": "Độ phân giải cảm biến camera",
                "gia_tri": "1,4 megapixel (1392 x 1040 pixel)",
                "loai": "tiêu chuẩn",
                "nguon": "https://www.bio-rad.com/en-us/product/gel-doc-ez-system"
            },
            {
                "ten": "Mô phỏng cường độ huỳnh quang vạch DNA",
                "gia_tri": "Cường độ tỷ lệ khối lượng DNA (ng) và bão hòa ở 50 ng",
                "loai": "mô phỏng",
                "mo_phong": True
            }
        ],
        "quy_trinh": [
            {
                "ma": "chuan_bi",
                "ten": "Bật nguồn buồng chụp",
                "viec": "Bật công tắc nguồn Gel Doc, lau sạch khay chứa gel bằng khăn mềm thấm cồn 70% hoặc nước cất.",
                "ly_do": "Đảm bảo buồng chụp sạch nền, tránh bụi bẩn phát huỳnh quang giả trên ảnh.",
                "loi": "Khay gel dính bẩn: xuất hiện vệt sáng tạp làm sai lệch phân tích vạch."
            },
            {
                "ma": "dat_gel",
                "ten": "Đặt khay gel vào buồng",
                "viec": "Chuyển gel agarose sau điện di vào giữa khay chụp, đẩy khay vào buồng và đóng kín cửa.",
                "ly_do": "Mẫu nằm đúng tâm quang trường của camera và kích hoạt công tắc an toàn liên động.",
                "loi": "Cửa đóng chưa khít: khóa liên động không cho phép bật đèn phát tia UV."
            },
            {
                "ma": "chon_che_do",
                "ten": "Chọn nguồn sáng và bộ lọc",
                "viec": "Chọn chế độ UV 302 nm (cho EtBr, GelRed) hoặc Blue LED 470 nm (cho GelGreen, SYBR Safe).",
                "ly_do": "Khớp bước sóng kích thích tối ưu với chất nhuộm huỳnh quang đã nạp trong gel.",
                "loi": "Chọn sai bộ lọc: tín hiệu huỳnh quang bị suy giảm mạnh hoặc không thấy vạch."
            },
            {
                "ma": "chup_anh",
                "ten": "Chỉnh phơi sáng và chụp ảnh",
                "viec": "Điều chỉnh thời gian phơi sáng (exposure) để vạch rõ nét, không bị lóa trắng bão hòa, nhấn Capture.",
                "ly_do": "Thu nhận ảnh số có dải động tối ưu để đối chiếu với thang chuẩn DNA Ladder.",
                "loi": "Thời gian phơi sáng quá ngắn khiến vạch mờ, hoặc quá dài làm bão hòa không định lượng được."
            }
        ],
        "an_toan": [
            "Tia cực tím UV-B (302 nm) có thể gây bỏng giác mạc và tổn thương da nghiêm trọng; không bao giờ vô hiệu hóa khóa liên động.",
            "Khi cần cắt gel trực tiếp dưới ánh sáng UV, bắt buộc phải đeo kính bảo hộ chống tia cực tím chuyên dụng và tấm chắn UV.",
            "Thuốc nhuộm Ethidium Bromide là chất xen cặp gây đột biến gen mạnh; phải mang găng tay nitrile khi thao tác."
        ],
        "thuat_ngu": [
            {
                "tu": "DNA Ladder",
                "nghia": "Thang chuẩn phân tử chứa các đoạn DNA có kích thước xác định (bp) dùng làm mốc so sánh."
            },
            {
                "tu": "Khóa liên động (Interlock)",
                "nghia": "Cơ chế an toàn tự động ngắt điện nguồn đèn UV khi mở cửa buồng chụp."
            },
            {
                "tu": "GelGreen",
                "nghia": "Thuốc nhuộm axit nucleic an toàn huỳnh quang xanh lục, kích thích tối ưu bằng ánh sáng xanh 470 nm."
            }
        ],
        "cau_hoi_on_tap": [
            "Nguyên lý cơ bản giúp phát hiện vạch DNA trên gel agarose trong buồng chụp Gel Doc là gì?",
            "Tại sao vạch DNA chứa nhiều bản sao hơn lại cho tín hiệu huỳnh quang sáng hơn?",
            "Tác dụng của khóa an toàn liên động cửa buồng đối với nguồn sáng cực tím UV là gì?"
        ]
    }
    p = DATA_MAY / "gel_doc.json"
    p.write_text(json.dumps(doc, ensure_ascii=False, indent=2), encoding="utf-8")
    print("Created data/may/gel_doc.json")


def tao_may_do_od600():
    doc = {
        "ma": "may_do_od600",
        "ten": "Máy quang phổ đo mật độ quang vi sinh và nồng độ ADN (OD600 / UV-Vis Spectrophotometer)",
        "may_mau": "Eppendorf BioSpectrometer basic, dải bước sóng 200 - 830 nm, đo cuvet 1 cm tiêu chuẩn và micro-cuvet",
        "nguyen_ly": [
            "Đo mật độ quang OD600 (Optical Density tại bước sóng 600 nm): Ở 600 nm, tế bào vi sinh vật làm tán xạ ánh sáng truyền qua theo định luật tán xạ Rayleigh / Mie; mật độ tán xạ tỷ lệ thuận với sinh khối tế bào trong dịch huyền phù.",
            "Đo độ hấp thụ quang học của axit nucleic tại 260 nm (A260) theo định luật Beer-Lambert: A = epsilon * c * l, trong đó 1 đơn vị A260 tương đương 50 µg/mL DNA chuỗi kép (dsDNA).",
            "Đánh giá độ tinh sạch của chế phẩm ADN qua tỷ số A260/A280 (chuẩn tinh sạch 1,8 - 2,0; tỷ số < 1,8 cảnh báo mẫu còn lẫn protein hoặc phenol tồn dư)."
        ],
        "cong_thuc": [
            "OD600 = -log10(I / I0)",
            "C_dsDNA (µg/mL) = A260 * 50",
            "Độ tinh sạch = A260 / A280"
        ],
        "bo_phan": [
            {
                "ma": "nguon_sang_xenon",
                "ten": "Đèn flash Xenon phổ rộng",
                "chuc_nang": "Phát chùm sáng liên tục từ vùng tử ngoại đến khả kiến (200 - 830 nm) không cần làm nóng lâu."
            },
            {
                "ma": "he_don_sac",
                "ten": "Cách tử nhiễu xạ lõm",
                "chuc_nang": "Tách chùm sáng thành các bước sóng đơn sắc với độ rộng phổ hẹp 5 nm."
            },
            {
                "ma": "ngan_cuvet",
                "ten": "Ngăn đo cuvet có nắp chắn sáng",
                "chuc_nang": "Giữ cuvet chuẩn 1 cm hoặc micro-cuvet đúng phương truyền quang học."
            },
            {
                "ma": "detector_diode",
                "ten": "Mảng diode silicon nhạy quang",
                "chuc_nang": "Chuyển đổi cường độ photon truyền qua thành tín hiệu điện số hóa."
            },
            {
                "ma": "man_hinh_dieu_khien",
                "ten": "Màn hình màu tích hợp phím chức năng",
                "chuc_nang": "Hiển thị giá trị mật độ quang OD, tính nồng độ ADN và vẽ đường cong sinh trưởng."
            }
        ],
        "thong_so": [
            {
                "ten": "Dải bước sóng quang phổ",
                "gia_tri": "200 – 830 nm",
                "loai": "tiêu chuẩn",
                "nguon": "https://www.eppendorf.com/us-en/eShop-Products/Cell-Analysis/Spectrophotometry/BioSpectrometer-basic-p-PF-24750"
            },
            {
                "ten": "Độ chính xác bước sóng",
                "gia_tri": "±1 nm",
                "loai": "tiêu chuẩn",
                "nguon": "https://www.eppendorf.com/us-en/eShop-Products/Cell-Analysis/Spectrophotometry/BioSpectrometer-basic-p-PF-24750"
            },
            {
                "ten": "Dải đo trắc quang",
                "gia_tri": "0 – 3,0 A",
                "loai": "tiêu chuẩn",
                "nguon": "https://www.eppendorf.com/us-en/eShop-Products/Cell-Analysis/Spectrophotometry/BioSpectrometer-basic-p-PF-24750"
            },
            {
                "ten": "Mô phỏng mật độ quang OD600 vi sinh",
                "gia_tri": "1 OD600 ≈ 8 * 10^8 tế bào/mL (E. coli K-12)",
                "loai": "mô phỏng",
                "mo_phong": True
            }
        ],
        "quy_trinh": [
            {
                "ma": "bat_may",
                "ten": "Khởi động và chọn phương pháp",
                "viec": "Bật công tắc nguồn, chọn chế độ đo OD600 (vi sinh) hoặc Absorbance 260/280 (axit nucleic).",
                "ly_do": "Máy tự kiểm chuẩn bước sóng và nạp thông số đo tương ứng.",
                "loi": "Không chờ máy hoàn tất tự kiểm: số đo có thể bị trôi zero."
            },
            {
                "ma": "do_blank",
                "ten": "Đo mẫu trắng (Blank Zero)",
                "viec": "Cho môi trường nuôi cấy chưa cấy khuẩn (hoặc nước cất/đệm TE) vào cuvet, đặt vào ngăn và bấm Blank.",
                "ly_do": "Thiết lập chuẩn zero để trừ độ hấp thụ và phản xạ nền của dung môi và thành cuvet.",
                "loi": "Quên đo mẫu trắng hoặc dùng sai dung môi trắng: kết quả OD bị sai lệch lớn."
            },
            {
                "ma": "chuan_bi_mau",
                "ten": "Nạp mẫu vào cuvet sạch",
                "viec": "Hút 1,0 mL dịch huyền phù vi sinh vào cuvet 1 cm; dùng giấy lau kính lau sạch hai mặt truyền sáng.",
                "ly_do": "Vân tay hoặc giọt nước bám ngoài thành cuvet gây tán xạ ánh sáng giả tạo.",
                "loi": "Chạm ngón tay vào mặt truyền quang: làm tăng giá trị OD ảo."
            },
            {
                "ma": "do_mau",
                "ten": "Đo và ghi nhận kết quả",
                "viec": "Đặt cuvet đúng chiều chùm sáng, đóng kín nắp buồng đo và bấm nút Sample / Measure.",
                "ly_do": "Đảm bảo buồng đo kín quang học, không bị ánh sáng ngoài lọt vào cảm biến.",
                "loi": "Để hở nắp buồng đo: ánh sáng phòng làm bão hòa cảm biến quang đi-ốt."
            }
        ],
        "an_toan": [
            "Luôn đậy kín nắp buồng đo khi phát chùm sáng để tránh ánh sáng tạp bên ngoài lọt vào detector.",
            "Chỉ cầm vào hai mặt nhám bên hông cuvet, không chạm vào mặt quang học truyền sáng.",
            "Dịch nuôi cấy vi sinh sau khi đo phải được thu gom vào thùng rác nguy hại sinh học để hấp tiệt trùng."
        ],
        "thuat_ngu": [
            {
                "tu": "OD600",
                "nghia": "Mật độ quang tại bước sóng 600 nm, đại lượng đo mức độ tán xạ ánh sáng của huyền phù vi sinh vật."
            },
            {
                "tu": "Blank (Mẫu trắng)",
                "nghia": "Mẫu đối chứng chỉ chứa dung môi hoặc môi trường không tế bào, dùng để trừ tín hiệu nền."
            },
            {
                "tu": "Tỷ số A260/A280",
                "nghia": "Tỷ lệ độ hấp thụ ở 260 nm và 280 nm dùng để đánh giá mức độ sạch của mẫu DNA/RNA."
            }
        ],
        "cau_hoi_on_tap": [
            "Tại sao bước sóng 600 nm lại được sử dụng phổ biến để theo dõi sinh trưởng vi khuẩn thay vì các bước sóng vùng tử ngoại?",
            "Mục đích của việc đo mẫu trắng (Blank) trước khi đo mẫu vi sinh là gì?",
            "Nếu một mẫu ADN có tỷ số A260/A280 đạt 1,85 thì mẫu đó được đánh giá như thế nào?"
        ]
    }
    p = DATA_MAY / "may_do_od600.json"
    p.write_text(json.dumps(doc, ensure_ascii=False, indent=2), encoding="utf-8")
    print("Created data/may/may_do_od600.json")


def bo_sung_may_cu():
    # 1. kinh_hien_vi
    p_khv = DATA_MAY / "kinh_hien_vi.json"
    if p_khv.exists():
        khv = json.loads(p_khv.read_text(encoding="utf-8"))
        ma_bp = {b["ma"] for b in khv["bo_phan"]}
        if "num_dich_xy" not in ma_bp:
            khv["bo_phan"].append({
                "ma": "num_dich_xy",
                "ten": "Núm điều khiển dịch chuyển sa trượt X - Y",
                "chuc_nang": "Dịch chuyển lam kính theo trục ngang (X) và dọc (Y) để rà quét tiêu bản một cách có hệ thống."
            })
        ten_ts = {t["ten"] for t in khv["thong_so"]}
        if "Khoảng dịch chuyển sa trượt X - Y" not in ten_ts:
            khv["thong_so"].append({
                "ten": "Khoảng dịch chuyển sa trượt X - Y",
                "gia_tri": "76 x 30 mm",
                "loai": "tiêu chuẩn",
                "nguon": "https://www.olympus-lifescience.com/en/microscopes/upright/cx23/"
            })
        p_khv.write_text(json.dumps(khv, ensure_ascii=False, indent=2), encoding="utf-8")

    # 2. tu_an_toan_sinh_hoc
    p_bsc = DATA_MAY / "tu_an_toan_sinh_hoc.json"
    if p_bsc.exists():
        bsc = json.loads(p_bsc.read_text(encoding="utf-8"))
        ma_bp = {b["ma"] for b in bsc["bo_phan"]}
        if "vach_kinh_20cm" not in ma_bp:
            bsc["bo_phan"].append({
                "ma": "vach_kinh_20cm",
                "ten": "Vạch đánh dấu chiều cao kính an toàn 20 cm",
                "chuc_nang": "Giới hạn độ mở kính trượt ở đúng 20 cm (8 inch) để duy trì tốc độ và lưu lượng khí bảo vệ."
            })
        ten_ts = {t["ten"] for t in bsc["thong_so"]}
        if "Chiều cao kính mở an toàn khi làm việc" not in ten_ts:
            bsc["thong_so"].append({
                "ten": "Chiều cao kính mở an toàn khi làm việc",
                "gia_tri": "20 cm (8 inch)",
                "loai": "tiêu chuẩn",
                "nguon": "https://www.thermofisher.com/order/catalog/product/1389"
            })
        if "Thời gian chạy quạt đuổi khí trước khi làm việc" not in ten_ts:
            bsc["thong_so"].append({
                "ten": "Thời gian chạy quạt đuổi khí trước khi làm việc",
                "gia_tri": "5 phút thanh lọc khí tồn (Purge)",
                "loai": "tiêu chuẩn",
                "nguon": "https://www.thermofisher.com/order/catalog/product/1389"
            })
        p_bsc.write_text(json.dumps(bsc, ensure_ascii=False, indent=2), encoding="utf-8")

    # 3. tu_am_vi_sinh
    p_am = DATA_MAY / "tu_am_vi_sinh.json"
    if p_am.exists():
        am = json.loads(p_am.read_text(encoding="utf-8"))
        ten_ts = {t["ten"] for t in am["thong_so"]}
        if "Thể tích buồng nuôi cấy" not in ten_ts:
            am["thong_so"].append({
                "ten": "Thể tích buồng nuôi cấy",
                "gia_tri": "53 lít",
                "loai": "tiêu chuẩn",
                "nguon": "https://www.memmert.com/products/incubators/incubator/IN55/"
            })
        p_am.write_text(json.dumps(am, ensure_ascii=False, indent=2), encoding="utf-8")

    # 4. noi_hap
    p_nh = DATA_MAY / "noi_hap.json"
    if p_nh.exists():
        nh = json.loads(p_nh.read_text(encoding="utf-8"))
        ten_ts = {t["ten"] for t in nh["thong_so"]}
        if "Chu trình tiệt trùng tiêu chuẩn" not in ten_ts:
            nh["thong_so"].append({
                "ten": "Chu trình tiệt trùng tiêu chuẩn",
                "gia_tri": "121 °C trong 15 – 20 phút ở áp suất 1,05 bar",
                "loai": "tiêu chuẩn",
                "nguon": "https://www.cdc.gov/infection-control/hcp/disinfection-sterilization/steam-sterilization.html"
            })
        p_nh.write_text(json.dumps(nh, ensure_ascii=False, indent=2), encoding="utf-8")

    # 5. may_pcr
    p_pcr = DATA_MAY / "may_pcr.json"
    if p_pcr.exists():
        pcr = json.loads(p_pcr.read_text(encoding="utf-8"))
        ten_ts = {t["ten"] for t in pcr["thong_so"]}
        if "Định dạng khay mẫu luân nhiệt" not in ten_ts:
            pcr["thong_so"].append({
                "ten": "Định dạng khay mẫu luân nhiệt",
                "gia_tri": "Khay 96 giếng cho ống 0,2 mL đơn hoặc dải 8 ống",
                "loai": "tiêu chuẩn",
                "nguon": "https://www.bio-rad.com/en-us/product/t100-thermal-cycler"
            })
        p_pcr.write_text(json.dumps(pcr, ensure_ascii=False, indent=2), encoding="utf-8")

    # 6. be_dien_di
    p_dd = DATA_MAY / "be_dien_di.json"
    if p_dd.exists():
        dd = json.loads(p_dd.read_text(encoding="utf-8"))
        ten_ts = {t["ten"] for t in dd["thong_so"]}
        if "Số lượng giếng mẫu tối đa trên khay" not in ten_ts:
            dd["thong_so"].append({
                "ten": "Số lượng giếng mẫu tối đa trên khay",
                "gia_tri": "15 giếng / khay lược",
                "loai": "tiêu chuẩn",
                "nguon": "https://www.bio-rad.com/en-us/product/sub-cell-gt-agarose-gel-electrophoresis-systems"
            })
        p_dd.write_text(json.dumps(dd, ensure_ascii=False, indent=2), encoding="utf-8")

    print("Updated 6 existing machine profiles.")


if __name__ == "__main__":
    tao_gel_doc()
    tao_may_do_od600()
    bo_sung_may_cu()
