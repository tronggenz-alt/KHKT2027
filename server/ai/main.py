"""Máy chủ AI và web quản lý cho phòng thí nghiệm ảo.

    pip install -r requirements.txt          (thêm `pip install anthropic` để dùng Claude)
    uvicorn server.ai.main:app --host 127.0.0.1 --port 8000
    mở http://127.0.0.1:8000                 web quản lý: kho hóa chất, thiết bị, nhật ký, mạng lưới khóa AI

Unity gửi ngữ cảnh do bộ máy hóa học tính (chất trong bình, pH, nhiệt độ, hiện tượng vừa xảy ra). AI chỉ
DIỄN GIẢI, trò chuyện và lập kế hoạch cho robot; AI không quyết định phản ứng. Khóa API chỉ nằm trên máy chủ.

Điểm cuối:
    GET  /suc-khoe              máy chủ có AI không, nhà cung cấp, mô hình
    POST /chu-thich             giải thích một hiện tượng định tính (1-2 câu)
    POST /robot/ke-hoach        robot hình người: ảnh chụp cảnh + câu hỏi → lời đáp + kế hoạch kỹ năng đã kiểm tra
    POST /tro-ly                (bản cũ) trả lời + danh sách hành động đơn giản
    POST /hoi, GET /the-thong-tin/{id}   thẻ thông tin phản ứng soạn sẵn
    /api/...                    web quản lý (server/lab/api.py)

Chọn nhà cung cấp: xem server/ai/providers/__init__.py (mạng lưới khóa Groq/DeepSeek/Gemini → Claude → OpenAI).
"""

from __future__ import annotations

import hashlib
import json
import re
import unicodedata
from pathlib import Path

from fastapi import FastAPI, HTTPException
from fastapi.responses import FileResponse
from pydantic import BaseModel, Field

from server.lab import api as lab_api
from server.lab import db as lab_db
from server.lab import keypool

from . import machines, providers
from .robot_plan import PLAN_SCHEMA, SYSTEM_PLAN, format_scene, validate

ROOT = Path(__file__).resolve().parents[2]
REACTIONS = {r["id"]: r for r in json.loads((ROOT / "data/reactions/phan_ung.json").read_text(encoding="utf-8"))["phan_ung"]}
_chem_path = ROOT / "data/chem/hoa_chat.json"
CHEMICALS = json.loads(_chem_path.read_text(encoding="utf-8"))["hoa_chat"] if _chem_path.exists() else []

SYSTEM_QA = (
    "Bạn là trợ giảng hóa học cho học sinh THPT Việt Nam trong phòng thí nghiệm ảo. "
    "Chỉ giải thích dựa trên NGỮ CẢNH được cung cấp (đó là kết quả mô phỏng đã kiểm chứng); "
    "không tự đặt ra phản ứng hay số liệu mới. Nếu ngữ cảnh không đủ, nói rõ là chưa đủ thông tin. "
    "Trả lời ngắn gọn (tối đa 4 câu), dễ đọc thành tiếng, dùng số liệu của chính học sinh khi có."
)

SYSTEM_ANNOTATE = (
    "Bạn là trợ giảng hóa học THPT Việt Nam. Phòng thí nghiệm ảo vừa ghi nhận một hiện tượng; dữ kiện (tên hiện tượng, "
    "phương trình, thành phần dung dịch, pH, nhiệt độ) do bộ máy mô phỏng tính, coi là đúng. "
    "Viết 1-2 câu tiếng Việt (tối đa 45 từ) giải thích BẢN CHẤT vì sao có hiện tượng này trong đúng bình đó, "
    "theo kiến thức sách giáo khoa; thêm một lưu ý an toàn ngắn nếu chất độc, ăn mòn hoặc dễ cháy. "
    "Không lặp lại nguyên văn tiêu đề, không bịa thêm phản ứng, không dùng định dạng markdown."
)

ACTIONS = ["tim_hoa_chat", "mang_hoa_chat", "don_ban", "bat_tu_hut", "tat_lua", "rua_mat", "mo_tra_cuu"]

SYSTEM_ASSIST = (
    "Bạn là robot trợ lý hình người trong phòng thí nghiệm hóa học ảo của một trường THPT Việt Nam (300 m², phòng hóa chất 720 chất "
    "xếp theo nhóm trên 27 kệ). Bạn trò chuyện bằng tiếng Việt, thân thiện, ngắn gọn (tối đa 4 câu), "
    "dựa trên NGỮ CẢNH thí nghiệm đi kèm (do mô phỏng tính, coi là đúng). Bạn hướng dẫn thao tác đúng, giải thích hiện tượng, "
    "nhắc an toàn, và có thể tự làm các hành động sau cho học sinh:\n"
    "- tim_hoa_chat: đi tới kệ chỉ vị trí một hóa chất (ghi tên hoặc công thức vào hoa_chat)\n"
    "- mang_hoa_chat: lấy lọ hóa chất trong kho mang ra bàn học sinh đang đứng (ghi hoa_chat)\n"
    "- don_ban: đổ dung dịch thừa, trả dụng cụ về chỗ, lau chỗ tràn\n"
    "- bat_tu_hut: bật quạt tủ hút và quạt hút (khi có khí độc)\n"
    "- tat_lua: tắt mọi đèn cồn, đèn Bunsen, bếp\n"
    "- rua_mat: dẫn tới vòi rửa mắt khẩn cấp\n"
    "- mo_tra_cuu: mở bảng tra cứu hóa chất\n"
    "Chỉ thêm hành động khi học sinh yêu cầu hoặc khi tình huống nguy hiểm cần xử lý ngay. "
    "Giữ đúng phạm vi thí nghiệm trường phổ thông: không hướng dẫn điều chế chất nổ, chất độc hay vũ khí; "
    "nếu được hỏi những điều đó, từ chối nhẹ nhàng và gợi ý thí nghiệm an toàn tương ứng. "
    "Khi có QUY TRÌNH MÁY đi kèm (tài liệu vận hành của phòng lab), hướng dẫn đúng thứ tự các bước và nhắc lỗi hay gặp, an toàn của máy đó; "
    "không tự đặt thêm bước hay thông số. Không dùng markdown."
)

ASSIST_SCHEMA = {
    "type": "object",
    "properties": {
        "tra_loi": {"type": "string"},
        "hanh_dong": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {"loai": {"type": "string", "enum": ACTIONS}, "hoa_chat": {"type": "string"}},
                "required": ["loai", "hoa_chat"],
                "additionalProperties": False,
            },
        },
    },
    "required": ["tra_loi", "hanh_dong"],
    "additionalProperties": False,
}

app = FastAPI(title="KHKT - AI phòng thí nghiệm ảo")
app.include_router(lab_api.router)
_cache: dict[str, str] = {}
WEB_INDEX = ROOT / "server/web/index.html"


@app.get("/", include_in_schema=False)
def web_quan_ly() -> FileResponse:
    return FileResponse(WEB_INDEX, media_type="text/html; charset=utf-8")


# ---------------------------------------------------------------------------
# Tra hóa chất theo tên / công thức (để biến "CuSO4" của AI thành số thứ tự trong danh mục)
# ---------------------------------------------------------------------------

def fold(s: str) -> str:
    s = (s or "").replace("đ", "d").replace("Đ", "D")
    s = "".join(c for c in unicodedata.normalize("NFD", s) if unicodedata.category(c) != "Mn")
    return s.lower().translate(str.maketrans("₀₁₂₃₄₅₆₇₈₉", "0123456789")).strip()


# Mô hình AI hay viết tên hóa chất tiếng Anh ("oxalic acid", "copper sulfate"): đổi từng từ sang cách gọi trong danh mục
_EN_VI = {
    "acid": "axit", "sodium": "natri", "potassium": "kali", "copper": "dong", "iron": "sat", "silver": "bac", "barium": "bari",
    "calcium": "canxi", "magnesium": "magie", "zinc": "kem", "aluminium": "nhom", "aluminum": "nhom", "ammonium": "amoni",
    "lead": "chi", "manganese": "mangan", "chromium": "crom", "nickel": "niken", "cobalt": "coban", "lithium": "liti",
    "hydroxide": "hidroxit", "chloride": "clorua", "sulfate": "sunfat", "sulphate": "sunfat", "sulfite": "sunfit",
    "sulfide": "sunfua", "nitrate": "nitrat", "nitrite": "nitrit", "carbonate": "cacbonat", "bicarbonate": "hidrocacbonat",
    "permanganate": "pemanganat", "dichromate": "dicromat", "chromate": "cromat", "oxide": "oxit", "peroxide": "peoxit",
    "iodide": "iodua", "bromide": "bromua", "fluoride": "florua", "phosphate": "photphat", "acetate": "axetat",
    "oxalate": "oxalat", "thiocyanate": "thioxianat", "thiosulfate": "thiosunfat", "hydrogen": "hidro", "water": "nuoc",
    "sulfuric": "sunfuric", "sulphuric": "sunfuric", "hydrochloric": "clohidric", "phosphoric": "photphoric",
    "acetic": "axetic", "formic": "fomic", "carbonic": "cacbonic", "glucose": "glucozo", "fructose": "fructozo",
    "sucrose": "saccarozo", "starch": "tinh bot", "ethanol": "etanol", "methanol": "metanol", "glycerol": "glixerol",
    "phenolphthalein": "phenolphtalein", "ammonia": "amoniac", "iodine": "iot", "chlorine": "clo", "bromine": "brom",
    "litmus": "quy", "indicator": "chi thi", "hidroxyt": "hidroxit", "hydroxit": "hidroxit", "sulfat": "sunfat",
    "clohydric": "clohidric", "hiđroxit": "hidroxit",
}
# Từ chỉ dạng / nồng độ, không dùng để so tên
_STOP = {"dung", "dich", "dd", "solution", "loang", "dilute", "aqueous", "lo", "chai", "ong", "cua", "va", "the"}


def _words(text: str) -> list[str]:
    out = []
    for w in re.findall(r"[a-z]+", fold(text)):
        out.extend(_EN_VI.get(w, w).split())
    return [w for w in out if w not in _STOP]


def formula_key(f: str) -> str:
    """Số nguyên tử mỗi nguyên tố, bỏ nước kết tinh và điện tích: (COOH)2 và H2C2O4 cùng ra 'C2H2O4'."""
    f = re.split(r"[.·*]", (f or "").translate(str.maketrans("₀₁₂₃₄₅₆₇₈₉", "0123456789")))[0]
    f = re.sub(r"[\^]?\d*[+\-]+$", "", f.strip())
    if not re.fullmatch(r"(?:[A-Z][a-z]?\d*|[()\[\]]\d*)+", f or "-"):
        return ""
    stack = [{}]
    for el, n, br, bn in re.findall(r"([A-Z][a-z]?)(\d*)|([()\[\]])(\d*)", f):
        if el:
            stack[-1][el] = stack[-1].get(el, 0) + int(n or 1)
        elif br in "([":
            stack.append({})
        elif len(stack) > 1:
            inner = stack.pop()
            for k, v in inner.items():
                stack[-1][k] = stack[-1].get(k, 0) + v * int(bn or 1)
    counts = stack[0]
    order = sorted(counts, key=lambda e: (e != "C", e != "H", e))  # kiểu Hill: C, H rồi theo chữ cái
    return "".join(e + (str(counts[e]) if counts[e] > 1 else "") for e in order)


def find_chemical(query: str) -> dict | None:
    q = fold(query)
    if not q:
        return None
    qkey = formula_key(query.strip())
    qwords = set(_words(query))
    want_solid = bool({"ran", "tinh", "bot", "solid", "crystal", "powder"} & set(re.findall(r"[a-z]+", q)))
    best, best_score = None, 0
    for c in CHEMICALS:
        name, formula = c.get("ten_khong_dau") or fold(c["ten"]), (c.get("cong_thuc") or "").lower()
        score = 0
        if formula and formula == q:
            score = 900
        elif qkey and len(qkey) > 2 and qkey in {formula_key(c.get("cong_thuc") or "")} | {formula_key(t) for t in re.findall(r"[A-Z][A-Za-z0-9()]*", c["ten"])}:
            score = 880  # cùng công thức, viết kiểu khác (H2C2O4 = (COOH)2) hoặc công thức nằm trong tên
        elif name == q:
            score = 850
        elif qwords and qwords == set(_words(name)):
            score = 820  # cùng các từ, khác thứ tự hoặc tiếng Anh: "oxalic acid" = "axit oxalic"
        elif name.startswith(q):
            score = 700
        elif qwords and qwords <= set(_words(name)):
            score = 600
        elif q in name or (formula and q in formula and len(q) >= 3):
            score = 400
        # cùng điểm thì ưu tiên dung dịch (rót được), trừ khi hỏi chất rắn
        prefer = (c.get("trang_thai") == "ran") if want_solid else (c.get("trang_thai") == "dd")
        score -= (0 if prefer else 3) + len(name) / 100 + c["stt"] / 10000
        if score > best_score:
            best, best_score = c, score
    return best if best_score > 0 else None


# ---------------------------------------------------------------------------
# Mô hình dữ liệu
# ---------------------------------------------------------------------------

class NguCanh(BaseModel):
    phan_ung_id: str | None = None
    chat_trong_coc: dict[str, float] = Field(default_factory=dict, description="chất -> số mol hoặc ml")
    ph: float | None = None
    nhiet_do_c: float | None = None
    hien_tuong: list[str] = Field(default_factory=list)
    so_lieu: dict[str, float] = Field(default_factory=dict, description="ví dụ v_buret_ml, v_tuong_duong_ml")
    ky_nang: dict[str, float] = Field(default_factory=dict, description="chỉ số khéo tay từ khkt.skill")


class CauHoi(BaseModel):
    cau_hoi: str
    ngu_canh: NguCanh


class TraLoi(BaseModel):
    tra_loi: str
    nguon: str  # "the_thong_tin" | "llm" | "cache"


class ChuThichYeuCau(BaseModel):
    tieu_de: str
    phuong_trinh: str = ""
    chi_tiet: str = ""
    muc_do: str = "Info"
    loai: str = ""
    thanh_phan: list[str] = Field(default_factory=list)
    ph: float | None = None
    nhiet_do: float | None = None
    ngu_canh: str = ""


class ChuThich(BaseModel):
    giai_thich: str
    nguon: str


class TroLyYeuCau(BaseModel):
    cau_hoi: str
    ngu_canh: str = ""
    lich_su: list[str] = Field(default_factory=list)


class HanhDong(BaseModel):
    loai: str
    stt: int = 0
    ghi_chu: str = ""


class TroLyTraLoi(BaseModel):
    tra_loi: str
    hanh_dong: list[HanhDong] = Field(default_factory=list)
    nguon: str


class VatTrongCanh(BaseModel):
    id: str
    ten: str = ""
    loai: str = ""
    chua: str = ""
    khoang_cach: float = 0


class AnhChupCanh(BaseModel):
    khu_vuc: str = ""
    nguoi_dang_cam: str = ""
    robot_dang_cam: str = ""
    che_do: str = ""
    vat: list[VatTrongCanh] = Field(default_factory=list)
    su_kien: list[str] = Field(default_factory=list)


class KeHoachYeuCau(BaseModel):
    cau_hoi: str
    the_gioi: AnhChupCanh = Field(default_factory=AnhChupCanh)
    lich_su: list[str] = Field(default_factory=list)


class BuocKeHoach(BaseModel):
    stt: int = 0
    ky_nang: str
    doi_tuong: str = ""
    dich: str = ""
    so_luong: float = 0
    loi: str = ""


class KeHoach(BaseModel):
    tra_loi: str
    ke_hoach: list[BuocKeHoach] = Field(default_factory=list)
    bo_qua: list[str] = Field(default_factory=list)
    nguon: str


# ---------------------------------------------------------------------------
# Điểm cuối
# ---------------------------------------------------------------------------

@app.get("/suc-khoe")
def suc_khoe() -> dict:
    p = providers.pick()
    return {"ok": p is not None, "nha_cung_cap": p.NAME if p else "khong", "mo_hinh": p.model_name() if p else ""}


@app.get("/may")
def danh_sach_may() -> list[dict]:
    """Các máy có hồ sơ vận hành (data/may): mã, tên, máy mẫu, số bước quy trình."""
    return machines.listing()


@app.get("/may/{ma}")
def ho_so_may(ma: str) -> dict:
    d = machines.MACHINES.get(ma)
    if d is None:
        raise HTTPException(status_code=404, detail="Không có máy này")
    return d


@app.get("/the-thong-tin/{phan_ung_id}")
def the_thong_tin(phan_ung_id: str) -> dict:
    r = REACTIONS.get(phan_ung_id)
    return {"the_thong_tin": r["the_thong_tin"] if r else None, "an_toan": r["an_toan"] if r else []}


@app.post("/hoi", response_model=TraLoi)
async def hoi(req: CauHoi) -> TraLoi:
    key = hashlib.sha256(req.model_dump_json().encode()).hexdigest()
    if key in _cache:
        return TraLoi(tra_loi=_cache[key], nguon="cache")
    reaction = REACTIONS.get(req.ngu_canh.phan_ung_id or "")
    card = reaction["the_thong_tin"] if reaction else "Chưa có thẻ thông tin cho thí nghiệm này."
    p = providers.pick()
    if p is None:
        return TraLoi(tra_loi=card, nguon="the_thong_tin")
    context = req.ngu_canh.model_dump(exclude_none=True)
    if reaction:
        context["phuong_trinh"] = reaction["phuong_trinh"]
        context["the_thong_tin"] = reaction["the_thong_tin"]
    try:
        text = await p.text(SYSTEM_QA, f"NGỮ CẢNH: {json.dumps(context, ensure_ascii=False)}\n\nCÂU HỎI: {req.cau_hoi}", 300)
    except Exception:
        return TraLoi(tra_loi=card, nguon="the_thong_tin")  # AI lỗi/chậm: vẫn có thẻ soạn sẵn
    _cache[key] = text
    return TraLoi(tra_loi=text, nguon="llm")


@app.post("/chu-thich", response_model=ChuThich)
async def chu_thich(req: ChuThichYeuCau) -> ChuThich:
    fact = req.model_dump(exclude_none=True)
    key = hashlib.sha256(json.dumps([req.tieu_de, req.phuong_trinh, sorted(req.thanh_phan)], ensure_ascii=False).encode()).hexdigest()
    if key in _cache:
        return ChuThich(giai_thich=_cache[key], nguon="cache")
    p = providers.pick()
    if p is None:
        return ChuThich(giai_thich=req.chi_tiet, nguon="the_thong_tin")
    try:
        text = await p.text(SYSTEM_ANNOTATE, "DỮ KIỆN: " + json.dumps(fact, ensure_ascii=False), 300)
    except Exception:
        return ChuThich(giai_thich="", nguon="loi")
    text = re.sub(r"\s+", " ", text).strip()
    _cache[key] = text
    return ChuThich(giai_thich=text, nguon="llm")


def ai_error(e: Exception) -> str:
    """Lời báo khi mọi nhà cung cấp đều lỗi; với mạng lưới khóa thì nêu lý do từng khóa (không lộ khóa)."""
    if isinstance(e, keypool.AllKeysFailed):
        return f"Mình chưa kết nối được AI: {str(e)[:240]}. Xem trang Mạng lưới khóa AI trên web quản lý."
    return f"Mình chưa kết nối được AI ({type(e).__name__})."


def resolve_actions(raw: list[dict]) -> list[HanhDong]:
    out = []
    for a in raw or []:
        loai = a.get("loai", "")
        if loai not in ACTIONS:
            continue
        stt = 0
        if loai in ("tim_hoa_chat", "mang_hoa_chat"):
            c = find_chemical(a.get("hoa_chat", ""))
            if c is None:
                continue
            stt = c["stt"]
        out.append(HanhDong(loai=loai, stt=stt, ghi_chu=a.get("hoa_chat", "")))
    return out


@app.post("/tro-ly", response_model=TroLyTraLoi)
async def tro_ly(req: TroLyYeuCau) -> TroLyTraLoi:
    p = providers.pick()
    if p is None:
        return TroLyTraLoi(tra_loi="Máy chủ chưa cấu hình mô hình AI; robot dùng lệnh cơ bản.", nguon="khong")
    history = "\n".join(req.lich_su[-8:])
    sop = machines.context(req.cau_hoi, req.ngu_canh)
    user = f"NGỮ CẢNH THÍ NGHIỆM:\n{req.ngu_canh}\n\n" + (f"QUY TRÌNH MÁY (tài liệu của phòng lab, coi là đúng):\n{sop}\n\n" if sop else "") + f"HỘI THOẠI GẦN ĐÂY:\n{history}\n\nHỌC SINH: {req.cau_hoi}"
    try:
        data = await p.structured(SYSTEM_ASSIST, user, ASSIST_SCHEMA, 1500)
    except Exception as e:  # mạng, khóa API, từ chối: Unity tự chuyển sang hiểu lệnh tại chỗ
        return TroLyTraLoi(tra_loi=ai_error(e), nguon="loi")
    return TroLyTraLoi(tra_loi=data.get("tra_loi", ""), hanh_dong=resolve_actions(data.get("hanh_dong", [])), nguon=p.NAME)


@app.post("/robot/ke-hoach", response_model=KeHoach)
async def robot_ke_hoach(req: KeHoachYeuCau) -> KeHoach:
    """Mô hình lập kế hoạch trên ảnh chụp cảnh; máy chủ kiểm tra từng bước trước khi gửi cho robot."""
    p = providers.pick()
    if p is None:
        return KeHoach(tra_loi="Máy chủ chưa cấu hình mô hình AI; robot dùng lệnh cơ bản.", nguon="khong")
    scene = req.the_gioi.model_dump()
    history = "\n".join(req.lich_su[-8:])
    sop = machines.context(req.cau_hoi)
    user = f"ẢNH CHỤP CẢNH:\n{format_scene(scene)}\n\n" + (f"QUY TRÌNH MÁY (tài liệu của phòng lab):\n{sop}\n\n" if sop else "") + f"HỘI THOẠI GẦN ĐÂY:\n{history}\n\nHỌC SINH: {req.cau_hoi}"
    try:
        data = await p.structured(SYSTEM_PLAN, user, PLAN_SCHEMA, 4000)  # 12 bước kèm lời giải thích cần ~2000-3000 token
    except Exception as e:  # mọi nhà cung cấp đều lỗi: Unity tự chuyển sang hiểu lệnh tại chỗ
        return KeHoach(tra_loi=ai_error(e), nguon="loi")
    steps, dropped = validate(data.get("ke_hoach") or [], {v.id for v in req.the_gioi.vat}, find_chemical)
    try:
        summary = " → ".join(s["ky_nang"] + (f"({s['doi_tuong']})" if s["doi_tuong"] else "") for s in steps)
        lab_db.log("robot", "Lệnh robot: " + req.cau_hoi[:200], (summary or "chỉ trả lời") + ("\nBỏ: " + "; ".join(dropped) if dropped else ""),
                   "Warning" if dropped else "Info", p.NAME)
    except Exception:
        pass  # nhật ký không được làm hỏng lệnh robot
    return KeHoach(tra_loi=str(data.get("tra_loi") or ""), ke_hoach=[BuocKeHoach(**s) for s in steps], bo_qua=dropped, nguon=p.NAME)
