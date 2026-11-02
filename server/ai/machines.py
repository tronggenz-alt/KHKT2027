"""Quy trình vận hành (SOP) các máy phân tích, phụ trợ (data/may/*.json) để AI trợ lý và robot hướng dẫn đúng tài liệu của phòng lab.

Câu hỏi của học sinh nhắc tới máy nào (theo tên, mã, từ khóa) thì tóm tắt nguyên lý, quy trình, lỗi hay gặp, an toàn của máy đó
được đưa vào ngữ cảnh gửi mô hình. Không có máy nào khớp thì không thêm gì.
"""

from __future__ import annotations

import json
import re
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MAY_DIR = ROOT / "data" / "may"

# từ khóa (không dấu, chữ thường) → mã máy hoặc mã dụng cụ sinh học
ALIASES: dict[str, list[str]] = {
    "uv_vis": ["uv-vis", "uv vis", "uvvis", "quang pho hap thu phan tu", "quang pho kha kien", "tu ngoai kha kien", "lambert", "beer"],
    "aas": ["aas", "hap thu nguyen tu", "ngon lua axetilen"],
    "ftir": ["ftir", "hong ngoai", "atr", "giao thoa"],
    "gc_ms": ["gc-ms", "gc ms", "gcms", "sac ky khi", "khoi pho"],
    "hplc": ["hplc", "sac ky long"],
    "chuan_do_dien_the": ["chuan do dien the", "chuan do tu dong", "dien cuc ph", "titrator"],
    "xrd": ["xrd", "nhieu xa tia x", "nhieu xa"],
    "sem": ["sem", "hien vi dien tu", "eds"],
    "nmr": ["nmr", "cong huong tu"],
    "nhiet_luong": ["bom nhiet luong", "nhiet luong", "nhiet dot chay"],
    "tu_say": ["tu say", "do am", "chat kho"],
    "lo_nung": ["lo nung", "tro tong so", "nung voi"],
    "may_cat_nuoc": ["may cat nuoc", "nuoc cat", "do dan dien"],
    "noi_hap": ["noi hap", "tiet trung", "autoclave", "noi hap tiet trung", "hap tiet trung"],
    "kinh_hien_vi": ["kinh hien vi", "microscope", "vat kinh", "thi kinh", "tieu ban", "soi tieu ban", "do phong dai", "soi kinh", "oc vi cap", "oc so cap"],
    "tu_an_toan_sinh_hoc": ["tu an toan sinh hoc", "tu thao tac vo trung", "biosafety cabinet", "bsc", "tu bsc", "dong khi laminar", "tu vo trung", "loc hepa"],
    "may_pcr": ["may pcr", "luan nhiet", "thermal cycler", "khuech dai gen", "chu ky nhiet", "pcr", "bien tinh", "gan moi"],
    "be_dien_di": ["dien di", "be dien di", "dien di gel", "electrophoresis", "agarose", "ban gel", "tae"],
    "tu_am_vi_sinh": ["tu am", "tu am vi sinh", "incubator", "u vi khuan", "u vi sinh", "dia petri", "37 do"],
    "gel_doc": ["gel doc", "chup gel", "buong chup gel", "buong toi uv", "gel documentation", "anh gel", "soi gel", "doc gel", "transilluminator"],
    "may_do_od600": ["od600", "od 600", "do quang", "mat do quang", "may do od600", "may quang pho od600", "so mau vi sinh", "spectrophotometer 600", "duong cong sinh truong"],
    # Dụng cụ sinh học tầng 2 (data/sinh/dung_cu.json)
    "micropipet_p200": ["micropipet", "micropipette", "pipet tu dong", "hut mau", "nac 1", "nac 2", "p200"],
    "micropipet_p10": ["micropipet p10", "pipet p10", "p10"],
    "micropipet_p1000": ["micropipet p1000", "pipet p1000", "p1000"],
    "hop_dau_con": ["dau con", "hop dau con", "tip pipet", "thay dau con"],
    "que_cay_vong": ["que cay", "que cay vong", "cay ria", "dot que cay", "vong cay", "inoculating loop"],
    "que_cay_thang": ["que cay thang", "cay dam", "stab"],
    "que_trang": ["que trang", "trang dia", "cell spreader"],
    "dao_nhuom_gram": ["nhuom gram", "dao nhuom gram", "gram stain", "tim ket tinh", "crystal violet", "safranin", "tay con", "gram duong", "gram am"],
    "may_ly_tam_de_ban": ["ly tam", "may ly tam", "centrifuge", "rcf", "can bang rotor", "ong doi dien", "ly tam de ban"],
    "may_ly_tam_mini": ["ly tam mini", "microcentrifuge", "spin down"],
    "may_vortex": ["vortex", "may vortex", "may lac vortex", "lac vortex", "tron vortex"],
    "may_dem_khuan_lac": ["dem khuan lac", "may dem khuan lac", "colony counter", "cfu"],
    "coi_chay_su": ["coi chay", "coi chay su", "tach chiet adn", "nghien mau", "ket tua adn"],
    "tram_potometer": ["potometer", "tram potometer", "thoat hoi nuoc", "do thoat hoi nuoc", "bot khi mao quan"],
    "tram_quang_hop": ["tram quang hop", "rong duoi cho", "elodea", "dem bot khi quang hop", "que dom"],
    "hop_huong_sang": ["huong sang", "hop huong sang", "phototropism", "moc vong"],
    "gian_thuy_canh_nft": ["thuy canh", "gian thuy canh", "nft"],
    "binh_len_men": ["binh len men", "len men ruou", "bong bay co2", "nam men"],
    "bo_lam_sua_chua": ["lam sua chua", "len men lactic", "hu lam sua chua"],
    "ban_giai_phau_so": ["ban giai phau", "ban giai phau so", "giai phau 3d"],
    "tu_thao_tac_pcr": ["tu thao tac pcr", "tu pcr", "pcr hood"],
    "bo_xu_ly_tran": ["bo xu ly tran", "tran do sinh hoc", "spill kit", "vo dia petri"],
    "lo_dau_soi": ["dau soi", "dau soi kinh", "vat kinh 100x", "vat kinh dau"],
}

# Từ khóa nhận diện yêu cầu mất an toàn sinh học (vượt cấp BSL-1)
UNSAFE_BIO_KEYWORDS: list[str] = [
    "bsl 2", "bsl 3", "bsl 4", "bsl2", "bsl3", "bsl4",
    "cap an toan 2", "cap an toan 3", "cap an toan 4",
    "tang doc luc", "tang cuong doc luc", "bien doi doc luc",
    "doc to sinh hoc", "vu khi sinh hoc", "tao mam benh",
    "tao virus", "nuoi virus", "cay virus", "khang khang sinh", "tao chung khang thuoc",
    "tac nhan gay benh", "vi khuan gay benh", "virus gay benh", "nam gay benh",
    "nuoi cay mam benh", "phan lap tu benh pham", "phan lap tu mau", "phan lap tu dom", "phan lap tu mu",
    "bacillus anthracis", "vi khuan than", "benh than", "anthrax",
    "vibrio cholerae", "vi khuan ta", "phay khuan ta",
    "mycobacterium tuberculosis", "vi khuan lao", "truc khuan lao",
    "yersinia pestis", "dich hach",
    "salmonella", "thuong han",
    "staphylococcus aureus", "tu cau vang", "mrsa",
    "clostridium botulinum", "doc to botulinum", "clostridium tetani", "uon van",
    "shigella", "truc khuan ly", "klebsiella pneumoniae", "pseudomonas aeruginosa",
    "ebola", "marburg", "sars cov", "mers cov", "h5n1", "cum gia cam", "virus dai", "dau mua",
]

BSL1_REFUSAL_MESSAGE = (
    "Phòng thí nghiệm trường học chỉ thực hiện các thí nghiệm an toàn sinh học cấp 1 (BSL-1); "
    "mình không thể hướng dẫn nuôi cấy, phân lập từ bệnh phẩm, tạo mới hay làm tăng độc lực các tác nhân gây bệnh nguy hiểm (BSL-2/3/4). "
    "Bạn có thể thực hành kỹ thuật vi sinh và sinh học phân tử tương đương trên các chủng dạy học an toàn BSL-1 như: "
    "cấy ria hoặc đo OD600 với E. coli K-12, nhuộm Gram vi khuẩn sữa chua (Lactobacillus bulgaricus, Streptococcus thermophilus), "
    "quan sát Bacillus subtilis hoặc lên men nấm men bánh mì (Saccharomyces cerevisiae)."
)

BSL1_SAFETY_CONTEXT = (
    "AN TOÀN SINH HỌC (BẮT BUỘC - CHỈ BSL-1):\n"
    f"{BSL1_REFUSAL_MESSAGE}\n"
    "Hãy từ chối nhẹ nhàng yêu cầu nguy hiểm này và gợi ý học sinh thực hiện các bài thí nghiệm BSL-1 an toàn ở trên."
)


def strip(s: str) -> str:
    """Bỏ dấu, chữ thường, mọi ký tự không phải chữ / số thành dấu cách."""
    s = unicodedata.normalize("NFD", s.lower()).replace("đ", "d")
    s = "".join(c for c in s if unicodedata.category(c) != "Mn")
    return re.sub(r"[^a-z0-9]+", " ", s).strip()


def load() -> dict[str, dict]:
    docs = {}
    for f in sorted(MAY_DIR.glob("*.json")):
        try:
            d = json.loads(f.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            continue
        if "quy_trinh" in d:
            docs[d["ma"]] = d
    return docs


MACHINES = load()

DUNG_CU_PATH = ROOT / "data" / "sinh" / "dung_cu.json"


def load_bio_tools() -> dict[str, dict]:
    if not DUNG_CU_PATH.exists():
        return {}
    try:
        data = json.loads(DUNG_CU_PATH.read_text(encoding="utf-8"))
        return {d["ma"]: d for d in data.get("dung_cu", []) if "ma" in d}
    except Exception:
        return {}


BIO_TOOLS = load_bio_tools()


def find_bio_tools(text: str, limit: int = 2) -> list[dict]:
    """Các dụng cụ sinh học (data/sinh/dung_cu.json) được nhắc tới trong câu hỏi."""
    t = " " + strip(text or "") + " "
    hits: list[tuple[int, str]] = []
    for ma, d in BIO_TOOLS.items():
        may_ma = d.get("may_man_hinh") or ""
        words = ALIASES.get(ma, []) + (ALIASES.get(may_ma, []) if may_ma and may_ma != ma else [])
        keys = [strip(k) for k in words] + [
            strip(d.get("ten", "")),
            strip(d.get("ten_tieng_anh", "")),
            ma.replace("_", " "),
        ]
        best = max((len(k) for k in keys if k and len(k) >= 3 and f" {k} " in t), default=0)
        if best:
            hits.append((best, ma))
    hits.sort(reverse=True)
    return [BIO_TOOLS[ma] for _, ma in hits[:limit]]


def tool_summary(d: dict) -> str:
    """Tóm tắt gọn một dụng cụ sinh học cho ngữ cảnh mô hình (khoảng 1500 ký tự)."""
    lines = [f"DỤNG CỤ: {d['ten']} ({d.get('phan_loai', '')})"]
    if d.get("cong_dung"):
        lines.append(f"Công dụng: {d['cong_dung']}")
    if d.get("cach_dung"):
        lines.append("Cách dùng:\n" + "\n".join(f"- {s}" for s in d["cach_dung"][:6]))
    if d.get("loi_thuong_gap"):
        lines.append("Lỗi thường gặp: " + "; ".join(d["loi_thuong_gap"][:4]))
    if d.get("an_toan"):
        lines.append("An toàn: " + "; ".join(d["an_toan"][:4]))
    return "\n".join(lines)[:1500]


def is_unsafe_bio(text: str) -> bool:
    """Kiểm tra câu hỏi có yêu cầu nuôi cấy, phân lập, tạo mới hoặc tăng độc lực tác nhân gây bệnh (vượt BSL-1)."""
    t = " " + strip(text or "") + " "
    for kw in UNSAFE_BIO_KEYWORDS:
        k = strip(kw)
        if k and f" {k} " in t:
            return True
    return False


def bio_safety_refusal(text: str) -> str | None:
    """Trả về lời từ chối nhẹ nhàng kèm gợi ý thí nghiệm BSL-1 an toàn nếu câu hỏi vi phạm an toàn sinh học."""
    if is_unsafe_bio(text):
        return BSL1_REFUSAL_MESSAGE
    return None


def find(text: str, limit: int = 2) -> list[dict]:
    """Các máy được nhắc tới trong câu (khớp từ khóa dài trước để 'nhiet luong' không lẫn với từ ngắn)."""
    t = " " + strip(text or "") + " "
    hits: list[tuple[int, str]] = []
    for ma, words in ALIASES.items():
        if ma not in MACHINES:
            continue
        keys = [strip(k) for k in words] + [strip(MACHINES[ma]["ten"]), ma.replace("_", " ")]
        best = max((len(k) for k in keys if k and f" {k} " in t), default=0)
        if best:
            hits.append((best, ma))
    hits.sort(reverse=True)
    return [MACHINES[ma] for _, ma in hits[:limit]]


def summary(d: dict) -> str:
    """Tóm tắt gọn một máy cho ngữ cảnh mô hình (khoảng 1500 ký tự)."""
    lines = [f"MÁY: {d['ten']} (thông số theo {d['may_mau'].split('(')[0].strip()})"]
    lines.append("Nguyên lý: " + " ".join(d["nguyen_ly"][:3]))
    steps = []
    for i, s in enumerate(d["quy_trinh"], 1):
        loi = f" (lỗi hay gặp: {s['loi']})" if s.get("loi") else ""
        steps.append(f"{i}. {s['ten']}: {s['viec']}{loi}")
    lines.append("Quy trình:\n" + "\n".join(steps))
    lines.append("An toàn: " + " ".join(d["an_toan"]))
    return "\n".join(lines)


def context(*texts: str) -> str:
    combined = " ".join(t for t in texts if t)
    parts = []
    if is_unsafe_bio(combined):
        parts.append(BSL1_SAFETY_CONTEXT)
    docs = find(combined)
    tools = find_bio_tools(combined)
    if docs:
        parts.append("\n\n".join(summary(d) for d in docs))
    if tools:
        parts.append("\n\n".join(tool_summary(d) for d in tools))
    return "\n\n".join(parts)


def listing() -> list[dict]:
    return [{"ma": d["ma"], "ten": d["ten"], "may_mau": d["may_mau"], "so_buoc": len(d["quy_trinh"])} for d in MACHINES.values()]
