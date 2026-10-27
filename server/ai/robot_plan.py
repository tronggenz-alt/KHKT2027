"""Cho mô hình ngôn ngữ "nhìn thấy" thế giới 3D của phòng lab ảo và điều khiển robot bằng kỹ năng.

Mô hình ngôn ngữ (Groq, DeepSeek, Gemini, Claude...) không biết gì về cảnh 3D. Vì vậy:
  1. Unity gửi ẢNH CHỤP CẢNH dạng chữ: các vật gần người dùng (mã vật, tên, loại, vị trí, đang chứa gì, ai đang cầm),
     vị trí người dùng, robot đang cầm gì.
  2. Mô hình chỉ được trả về KẾ HOẠCH gồm các KỸ NĂNG có sẵn, tham chiếu vật bằng MÃ VẬT trong ảnh chụp
     (không được đưa tọa độ, không điều khiển khớp tay chân).
  3. Máy chủ kiểm tra kế hoạch (kỹ năng có trong danh sách, mã vật có thật, lượng hợp lý, giới hạn an toàn),
     đổi tên hóa chất thành số thứ tự trong danh mục, tên thí nghiệm mẫu thành khóa.
  4. Unity thực hiện từng kỹ năng bằng thân robot (đi tới, với tay, cầm, rót...) và báo lỗi nếu không làm được.
Như robot thật: mô hình ngôn ngữ là "bộ não lập kế hoạch", còn thị giác (ảnh chụp cảnh) và vận động (kỹ năng)
do chương trình xác định đảm nhận.
"""

from __future__ import annotations

import unicodedata

SKILLS = {
    # di chuyển, cầm nắm
    "di_toi": "đi tới cạnh vật (doi_tuong)",
    "chi_vao": "chỉ tay vào vật để minh họa (doi_tuong)",
    "cam": "cầm vật lên và giữ (doi_tuong)",
    "dat": "đặt vật đang cầm lên chỗ (dich): mã chỗ đặt, mã vật chứa để đặt cạnh, hoặc 'ban_nguoi_dung'",
    "tra_ve": "trả vật (doi_tuong) về chỗ cất ban đầu (kệ, giá)",
    "lay_hoa_chat": "vào phòng hóa chất lấy lọ (hoa_chat: tên hoặc công thức) mang ra bàn người dùng",
    # chuyển chất
    "rot": "rót so_luong ml từ vật chứa (doi_tuong) vào vật chứa (dich)",
    "nho_giot": "nhỏ so_luong giọt từ lọ (doi_tuong) vào vật chứa (dich)",
    "xuc": "xúc so_luong thìa chất rắn từ lọ (doi_tuong) vào vật chứa (dich)",
    "hut_pipet": "dùng pipet bầu 25 ml lấy chính xác so_luong ml (mặc định 25) từ (doi_tuong) cho vào (dich)",
    "tia_nuoc": "tia so_luong ml nước cất vào vật chứa (dich): pha loãng, tráng",
    "suc_khi": "sục khí từ bình khí (doi_tuong, mã hoa_chat:...) vào dung dịch (dich) trong so_luong giây; khí độc robot tự đưa vào tủ hút",
    "do_bo": "đổ bỏ dung dịch trong (doi_tuong) vào cốc thải / thùng chất thải (so_luong ml, 0 là đổ hết)",
    # xử lý
    "lac": "lắc đều vật chứa (doi_tuong) trong so_luong giây",
    "khuay": "đặt (doi_tuong) lên máy khuấy từ, khuấy so_luong giây",
    "dun_nong": "đun (doi_tuong) tới so_luong °C (mặc định 70): ống nghiệm hơ trên đèn cồn, cốc đặt lên bếp; dich là mã bếp nếu muốn chọn",
    "ly_tam": "cho ống nghiệm (doi_tuong) vào máy ly tâm để kết tủa lắng xuống đáy",
    "dua_vao_tu_hut": "mang (doi_tuong) vào tủ hút và bật quạt hút",
    "doi": "chờ so_luong giây để quan sát hiện tượng",
    # đo, quan sát
    "quan_sat": "nhìn và nói rõ trong (doi_tuong) có gì: chất, pH, nhiệt độ, kết tủa, khí, hiện tượng vừa xảy ra",
    "do_ph": "đo pH dung dịch trong (doi_tuong) bằng máy đo pH",
    "do_nhiet_do": "đo nhiệt độ (doi_tuong) bằng nhiệt kế điện tử",
    "do_do_dan": "đo độ dẫn điện (doi_tuong)",
    "thu_quy": "nhúng giấy quỳ tím vào (doi_tuong)",
    "thu_giay_ph": "nhúng giấy chỉ thị pH vạn năng vào (doi_tuong)",
    "can": "cân vật (doi_tuong) trên cân điện tử",
    "can_chat_ran": "đặt (dich) lên cân, trừ bì rồi xúc chất rắn từ lọ (doi_tuong) tới đủ so_luong gam",
    "phan_tich": "đặt mẫu (doi_tuong) vào máy phân tích (dich: mã may_...; bỏ trống thì máy gần nhất) và đọc kết quả",
    # thiết bị, an toàn
    "bat_tat": "bật hoặc tắt thiết bị (doi_tuong): đèn cồn, bếp, máy khuấy, quạt tủ hút",
    "bat_tu_hut": "bật quạt tủ hút và quạt hút của phòng",
    "tat_lua": "tắt mọi nguồn lửa và bếp",
    "don_ban": "dọn bàn: đổ dung dịch thừa, trả dụng cụ, lau chỗ tràn",
    "rua_mat": "dẫn tới vòi rửa mắt khẩn cấp",
    # quy trình trọn gói
    "chuan_do": "làm mẫu trọn vẹn bài chuẩn độ đang chọn ở bàn 1: lắp buret, nạp, đọc V đầu, pipet mẫu, chỉ thị, chuẩn độ tới điểm cuối, tính nồng độ",
    "thi_nghiem_mau": "làm một thí nghiệm mẫu có sẵn (ghi khóa vào hoa_chat)",
    "noi": "nói một câu (loi) trong lúc làm, ví dụ giải thích bước đang minh họa",
}

# Thư viện thí nghiệm mẫu: khóa phải trùng RobotDemos.cs trong Unity (tests/test_lab_web.py đối chiếu)
TEMPLATES = {
    "nhan_biet_cu2": "Nhận biết ion Cu²⁺ bằng NaOH (kết tủa xanh lam)",
    "nhan_biet_cl": "Nhận biết ion Cl⁻ bằng AgNO₃ (kết tủa trắng)",
    "nhan_biet_so4": "Nhận biết ion SO₄²⁻ bằng BaCl₂ (kết tủa trắng)",
    "nhan_biet_fe3": "Nhận biết ion Fe³⁺ bằng KSCN (đỏ máu)",
    "chi_thi_axit_bazo": "Phenolphtalein trong bazơ và phản ứng trung hòa",
    "kim_loai_axit": "Kẽm tác dụng với axit clohiđric (sủi bọt H₂)",
    "do_ph_dung_dich": "So sánh pH của axit, bazơ và nước cất bằng máy đo pH",
    "ly_tam_ket_tua": "Tạo kết tủa Cu(OH)₂ rồi ly tâm tách rắn - lỏng",
    "glucozo_cu_oh2": "Glucozơ với Cu(OH)₂: phức xanh lam, đun ra Cu₂O đỏ gạch",
    "phan_tich_nuoc": "Phân tích kim loại trong nước giếng và nước thải bằng AAS",
    "chuan_do": "Chuẩn độ axit - bazơ trọn vẹn",
}

NEEDS_OBJECT = {"di_toi", "chi_vao", "cam", "tra_ve", "rot", "nho_giot", "xuc", "hut_pipet", "suc_khi", "do_bo", "lac", "khuay",
                "dun_nong", "ly_tam", "dua_vao_tu_hut", "quan_sat", "do_ph", "do_nhiet_do", "do_do_dan", "thu_quy", "thu_giay_ph",
                "can", "can_chat_ran", "phan_tich", "bat_tat"}
NEEDS_DEST = {"rot", "nho_giot", "xuc", "hut_pipet", "tia_nuoc", "suc_khi", "can_chat_ran"}
OPTIONAL_DEST = {"phan_tich", "dun_nong"}  # có thì phải là mã thật, không có thì robot chọn máy / bếp gần nhất
# (mặc định, tối đa) cho so_luong; tối thiểu luôn là 0
LIMITS = {"rot": (2, 100), "nho_giot": (1, 20), "xuc": (1, 5), "hut_pipet": (25, 25), "tia_nuoc": (5, 100), "suc_khi": (5, 30),
          "lac": (4, 30), "khuay": (15, 120), "doi": (5, 60), "dun_nong": (70, 100), "can_chat_ran": (1, 20), "do_bo": (0, 1000)}

PLAN_SCHEMA = {
    "type": "object",
    "properties": {
        "tra_loi": {"type": "string"},
        "ke_hoach": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "ky_nang": {"type": "string", "enum": list(SKILLS)},
                    "doi_tuong": {"type": "string"},
                    "dich": {"type": "string"},
                    "so_luong": {"type": "number"},
                    "hoa_chat": {"type": "string"},
                    "loi": {"type": "string"},
                },
                "required": ["ky_nang", "doi_tuong", "dich", "so_luong", "hoa_chat", "loi"],
                "additionalProperties": False,
            },
        },
    },
    "required": ["tra_loi", "ke_hoach"],
    "additionalProperties": False,
}

MAX_STEPS = 20

SYSTEM_PLAN = (
    "Bạn là bộ não của một robot trợ lý hình người trong phòng thí nghiệm hóa học ảo của trường THPT Việt Nam. "
    "Bạn KHÔNG nhìn thấy cảnh 3D; bạn chỉ biết ẢNH CHỤP CẢNH dạng chữ đi kèm (danh sách vật với mã vật, tên, loại, "
    "khoảng cách tới người dùng, đang chứa gì). Hãy trả lời học sinh bằng tiếng Việt ngắn gọn (tối đa 3 câu) và, nếu cần "
    "hành động, lập KẾ HOẠCH là danh sách kỹ năng theo đúng thứ tự thực hiện. Kỹ năng có sẵn:\n"
    + "\n".join(f"- {k}: {v}" for k, v in SKILLS.items()) + "\n"
    "Thí nghiệm mẫu có sẵn (kỹ năng thi_nghiem_mau, ghi khóa vào hoa_chat):\n"
    + "\n".join(f"- {k}: {v}" for k, v in TEMPLATES.items()) + "\n"
    "Quy tắc:\n"
    "- Học sinh xin làm mẫu cả một thí nghiệm có trong danh sách mẫu thì dùng MỘT bước thi_nghiem_mau (hoặc chuan_do), "
    "không tự chia nhỏ. Thí nghiệm khác thì tự ghép các kỹ năng.\n"
    "- Robot tự đi lấy dụng cụ cần cho kỹ năng (máy đo pH, nhiệt kế, máy đo độ dẫn, giấy quỳ, giấy pH, pipet, bình tia, "
    "cân, máy ly tâm, máy khuấy từ, bếp, đèn cồn, tủ hút) và dùng xong tự cất: không cần mã vật cho các dụng cụ đó.\n"
    "- doi_tuong và dich PHẢI là mã vật có trong ảnh chụp cảnh (hoặc 'ban_nguoi_dung' cho dich của dat); trường nào "
    "không dùng thì để chuỗi rỗng, so_luong không dùng thì để 0.\n"
    "- Hóa chất chưa có trên bàn thì dùng lay_hoa_chat trước (ghi tên TIẾNG VIỆT theo danh pháp trong SGK như "
    "'axit oxalic', 'kali pemanganat', 'axit sunfuric', hoặc công thức như H2C2O4), sau đó gọi lọ đó bằng mã "
    "'hoa_chat:<đúng chuỗi đã ghi ở hoa_chat>'.\n"
    "- Nếu phản ứng cần môi trường hay chất xúc tác (ví dụ KMnO4 oxi hóa axit oxalic cần H2SO4 loãng) thì lấy thêm chất đó "
    "và cho vào đúng thứ tự.\n"
    "- Lời giải thích (noi, tra_loi) phải đúng hóa học: nêu hiện tượng thật sẽ thấy (ví dụ dung dịch thuốc tím mất màu, "
    "sủi bọt khí CO2), không đoán bừa; nếu không chắc thì để robot quan_sat rồi nói theo hiện tượng quan sát được.\n"
    "- Dùng ống nghiệm hoặc cốc đang trống trong ảnh chụp cảnh cho phản ứng mới. Rót lượng nhỏ (ống nghiệm 1-5 ml, cốc 10-50 ml).\n"
    "- Sau khi trộn nên lac rồi quan_sat để học sinh thấy hiện tượng; xen kỹ năng noi để giải thích từng bước.\n"
    "- An toàn trường phổ thông: chất sinh khí độc làm trong tủ hút; không pha nước vào axit đặc; không đun chất dễ cháy "
    "trên lửa trần; dùng dung dịch loãng, lượng nhỏ.\n"
    "- CÁC THÍ NGHIỆM TRONG SGK HÓA 10-12 ĐỀU ĐƯỢC LÀM, không được từ chối, ví dụ: KMnO4 với axit oxalic / H2O2 / FeSO4 "
    "(môi trường H2SO4 loãng), kim loại với axit loãng, nhận biết ion, trung hòa, chuẩn độ, điện phân dung dịch, "
    "glucozơ với Cu(OH)2, tốc độ phản ứng, cân bằng hóa học. Chỉ từ chối (ke_hoach rỗng, giải thích nhẹ nhàng) khi yêu cầu "
    "chế tạo chất nổ, pháo, chất độc / ma túy, hoặc tạo lượng lớn khí độc ngoài tủ hút.\n"
    f"- Câu hỏi chỉ cần trả lời thì ke_hoach rỗng. Tối đa {MAX_STEPS} bước."
)


def _fold(s: str) -> str:
    s = (s or "").replace("đ", "d").replace("Đ", "D")
    # NFKD: bỏ dấu và đổi chữ số nhỏ (Cu²⁺ → cu2+)
    return "".join(c for c in unicodedata.normalize("NFKD", s) if unicodedata.category(c) != "Mn").lower().strip()


def find_template(name: str) -> str | None:
    """Khóa thí nghiệm mẫu từ khóa hoặc tên (có dấu hay không)."""
    q = _fold(name).replace(" ", "_")
    if q in TEMPLATES:
        return q
    words = [w for w in _fold(name).replace("_", " ").split() if len(w) > 1]
    best, best_hits = None, 0
    for k, title in TEMPLATES.items():
        hay = _fold(title) + " " + k.replace("_", " ")
        hits = sum(1 for w in words if w in hay)
        if hits > best_hits:
            best, best_hits = k, hits
    return best if words and best_hits >= max(1, (len(words) + 1) // 2) else None


def validate(plan: list[dict], object_ids: set[str], find_chemical) -> tuple[list[dict], list[str]]:
    """Giữ các bước hợp lệ; trả (kế hoạch đã chuẩn hóa, danh sách lý do bỏ bước)."""
    out, dropped = [], []
    fetched: dict[str, int] = {}

    def known(v: str) -> bool:
        return v in object_ids or v.startswith("hoa_chat:")

    for i, s in enumerate(plan[:MAX_STEPS]):
        k = s.get("ky_nang", "")
        if k not in SKILLS:
            dropped.append(f"bước {i + 1}: kỹ năng lạ '{k}'")
            continue
        step = {"ky_nang": k, "doi_tuong": str(s.get("doi_tuong") or ""), "dich": str(s.get("dich") or ""),
                "so_luong": float(s.get("so_luong") or 0), "stt": 0, "loi": str(s.get("loi") or "")[:300]}
        if k == "thi_nghiem_mau":
            key = find_template(str(s.get("hoa_chat") or "") or step["doi_tuong"])
            if key is None:
                dropped.append(f"bước {i + 1}: không có thí nghiệm mẫu '{s.get('hoa_chat') or step['doi_tuong']}'")
                continue
            out.append({**step, "doi_tuong": key, "dich": "", "so_luong": 0})
            continue
        if k == "lay_hoa_chat":
            c = find_chemical(s.get("hoa_chat", ""))
            if c is None:
                dropped.append(f"bước {i + 1}: không tìm thấy hóa chất '{s.get('hoa_chat', '')}'")
                continue
            step["stt"] = c["stt"]
            step["doi_tuong"] = f"hoa_chat:{c['stt']}"
            fetched[str(s.get("hoa_chat", "")).strip().lower()] = c["stt"]
            fetched[str(c["stt"])] = c["stt"]
        missing = ""
        for field in ("doi_tuong", "dich"):
            v = step[field]
            if v.startswith("hoa_chat:"):
                key = v.split(":", 1)[1].strip().lower()
                if key in fetched:
                    step[field] = f"hoa_chat:{fetched[key]}"
                elif key.isdigit():
                    step[field] = f"hoa_chat:{key}"
                else:
                    c = find_chemical(key)
                    step[field] = f"hoa_chat:{c['stt']}" if c else ""
                    missing = missing or ("" if c else key)
        if missing:
            dropped.append(f"bước {i + 1}: hóa chất '{missing}' không có trong danh mục")
            continue
        if k == "tia_nuoc" and not step["dich"] and known(step["doi_tuong"]):
            step["dich"] = step["doi_tuong"]  # mô hình hay ghi bình nhận vào doi_tuong
        if k in NEEDS_OBJECT and not known(step["doi_tuong"]):
            dropped.append(f"bước {i + 1}: mã vật '{step['doi_tuong']}' không có trong cảnh")
            continue
        if k in NEEDS_DEST and not known(step["dich"]):
            dropped.append(f"bước {i + 1}: nơi nhận '{step['dich']}' không có trong cảnh")
            continue
        if k in OPTIONAL_DEST and step["dich"] and step["dich"] not in object_ids:
            step["dich"] = ""  # mã máy/bếp lạ: để robot chọn cái gần nhất
        if k in LIMITS:
            default, limit = LIMITS[k]
            step["so_luong"] = max(0.0, min(step["so_luong"] or float(default), float(limit)))
        if k == "dat" and not (step["dich"] in object_ids or step["dich"] in ("ban_nguoi_dung", "")):
            step["dich"] = "ban_nguoi_dung"
        out.append(step)
    if len(plan) > MAX_STEPS:
        dropped.append(f"bỏ {len(plan) - MAX_STEPS} bước sau bước {MAX_STEPS}")
    return out, dropped


def format_scene(scene: dict) -> str:
    """Ảnh chụp cảnh (JSON từ Unity) thành văn bản gọn cho mô hình."""
    lines = []
    if scene.get("khu_vuc"):
        lines.append(f"Người dùng đang ở: {scene['khu_vuc']}.")
    if scene.get("nguoi_dang_cam"):
        lines.append(f"Người dùng đang cầm: {scene['nguoi_dang_cam']}.")
    if scene.get("robot_dang_cam"):
        lines.append(f"Robot đang cầm: {scene['robot_dang_cam']}.")
    if scene.get("che_do"):
        lines.append(f"Chế độ: {scene['che_do']}.")
    lines.append("Các vật gần người dùng (mã | tên | loại | cách người dùng | đang chứa):")
    for v in scene.get("vat", [])[:60]:
        lines.append(f"- {v.get('id')} | {v.get('ten')} | {v.get('loai')} | {v.get('khoang_cach', 0):.1f} m | {v.get('chua', '')}")
    if scene.get("su_kien"):
        lines.append("Hiện tượng gần đây: " + "; ".join(scene["su_kien"][-5:]))
    return "\n".join(lines)
