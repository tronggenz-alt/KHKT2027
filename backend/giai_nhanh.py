"""Giải chuỗi NHANH, kết quả dạng CHUỖI gọn (A1 —[phản ứng, điều kiện]→ A2 …, "Lưu ý", đáp án) – mục tiêu 1–2 phút, ít token.

- MỘT lần gọi AI gộp Phân tích + Tra cứu + Giải (mô hình tự suy nghĩ nội bộ rồi trả JSON gọn SCHEMA_GON), streaming: bước nào xong là
  hiện ngay. Không sinh phương trình đầy đủ – chỉ sinh khi người dùng bấm "Xem phương trình chi tiết" (phuong_trinh_chi_tiet).
- Thanh "mức độ thông minh": Nhanh (Flash-Lite, không suy nghĩ thêm, bỏ kiểm tra) · Cân bằng (Flash, suy nghĩ ít, kiểm tra nhanh) ·
  Kỹ (Pro hoặc Claude nếu được phép, suy nghĩ vừa, có kiểm tra) · Tối đa (mô hình mạnh nhất được phép, suy nghĩ nhiều, kiểm tra kỹ).
  Tên mô hình + ngân sách token ở .env (config.py: GEMINI_MODEL_*, CLAUDE_MODEL(_MANH), THINKING_*, MAX_TOKENS_*, CLAUDE_EFFORT_*).
- Kiểm tra: RDKit trước (SMILES hợp lệ, đúng công thức/CTPT đề cho, các bước nối đúng chất, nguyên tố mới phải đến từ tác nhân) –
  chỉ gọi AI sửa khi RDKit báo lỗi (Cân bằng: mô hình rẻ; Kỹ: chính mô hình giải); Tối đa: luôn nhờ AI rà soát thêm một lượt.
- Cache: cùng đề (chuẩn hoá chữ + băm ảnh) + cùng mức + cùng loại mô hình → trả kết quả đã lưu trong CACHE_GIAI_NGAY ngày (bảng giai_cache).
"""
from __future__ import annotations

import hashlib
import json
import logging
import re
import time
import unicodedata
from dataclasses import dataclass
from typing import Callable

from pydantic import ValidationError

from . import db, do_kho, parser, validator
from .config import settings
from .llm import LoiAI, PhienHoiThoai, doc_json, mo_ta_mo_hinh
from .schemas import Buoc, ChatPhanUng, LoiGiai, LoiGiaiGon, NutSoDo

log = logging.getLogger(__name__)

MUC_TM = ("nhanh", "can_bang", "ky", "toi_da")
TEN_MUC_TM = {"nhanh": "Nhanh", "can_bang": "Cân bằng", "ky": "Kỹ", "toi_da": "Tối đa"}
MUC_THEO_DO_KHO = {"de": "nhanh", "trung_binh": "can_bang", "hsg": "ky"}
UOC_TINH = {"nhanh": "~20–40 giây", "can_bang": "~30–60 giây", "ky": "~1–2 phút", "toi_da": "~2–4 phút"}

SYSTEM_GIAI_GON = """Bạn là giáo viên Hoá hữu cơ giỏi ở Việt Nam (chương trình GDPT 2018, cả đề học sinh giỏi). Đọc đề chuỗi phản ứng (chữ hoặc ảnh),
TỰ SUY NGHĨ NỘI BỘ (phân tích đề, nhớ dữ kiện về các chất, giải) rồi CHỈ trả về MỘT JSON GỌN đúng schema – không văn xuôi,
KHÔNG viết phương trình phản ứng.

{"chuoi":[{"ki_hieu","ctct","ten","smiles"}], "buoc":[{"tu","den","phan_ung","dieu_kien"}], "luu_y":["..."], "dap_an":"..."}
- chuoi: MỌI chất của chuỗi theo thứ tự xuất hiện, mỗi kí hiệu một lần. ki_hieu giữ đúng như đề viết (CH4, X, A1, (C6H10O5)n);
  ctct: công thức cấu tạo thu gọn kiểu SGK, thể hiện rõ vị trí nhóm chức (CH3–CHBr–CH3, CH3COOC2H5); ten: tên ngắn theo SGK 2018
  (IUPAC: ethanol, acetaldehyde, acetic acid…; đề dùng tên cũ thì ghi kèm trong ngoặc); smiles: hợp lệ với RDKit, có lập thể khi
  lập thể quyết định đáp án (@, @@, /, \\). Polime: SMILES một mắt xích, hai đầu nối là * (tinh bột: *OC1C(CO)OC(*)C(O)C1O).
- buoc: mỗi mũi tên của đề là một phần tử, đúng thứ tự; tu/den là ki_hieu trong chuoi. phan_ung: tên/loại phản ứng thật ngắn
  ("cộng HBr (Markovnikov)", "tách nước (Zaitsev)", "oxi hoá bằng CuO", "chuyển vị pinacol", "ester hoá", "trùng hợp");
  dieu_kien: tác nhân + điều kiện ngắn như ghi trên mũi tên ("H2SO4 đặc, 170°C", "CuO, t°", "1) O3; 2) Zn/H2O"); đề đã ghi trên mũi
  tên thì giữ nguyên, chỉ bổ sung phần thiếu; "t°" cho đun nóng. Tác nhân nhiều giai đoạn trên một mũi tên vẫn là MỘT bước.
- Viết hết chuoi trước rồi tới buoc (app hiện dần từng bước cho học sinh).
- luu_y: tối đa 5 gạch đầu dòng rất ngắn: chuyển vị, quy tắc Markovnikov/Zaitsev, lập thể, sản phẩm phụ, điểm dễ nhầm; không có thì [].
- dap_an: một dòng tóm tắt đáp án (vd "X: CH3CHO (acetaldehyde); Y: CH3COOH (acetic acid)").
- Dạng đề cho chất đầu + tác nhân, hỏi cấu tạo sản phẩm: suy ra sản phẩm CHÍNH theo quy tắc (Markovnikov khi cộng HX/H2O vào alkene
  bất đối xứng; Zaitsev khi tách; định hướng thế vòng benzen; ancol bậc I → aldehyde, bậc II → ketone khi oxi hoá CuO; thế X bằng OH
  khi đun với NaOH loãng; tách HX bằng KOH/ethanol…).
- Đề cho CTPT (vd "X (C3H6O)") thì cấu tạo phải đúng CTPT đó. App kiểm tra tự động bằng RDKit: SMILES hợp lệ, đúng công thức/CTPT
  đề cho, tu/den có trong chuoi, nguyên tố mới ở chất sau phải đến từ tác nhân của bước đó.
- [MODULE: BIỆN LUẬN CHẤT HỮU CƠ]: Khi đề có chất ẩn / chuỗi suy luận cấu tạo, tư duy và ghi luu_y ngắn gọn theo ký hiệu `->`,
  không văn mẫu, đi thẳng vào bản chất theo 4 bước:
  1. Δ & Nhóm chức: Tính Δ = (2C + 2 + N − H − X)/2. Dịch nhanh: [Dữ kiện] -> [Cấu tạo/Nhóm chức/Phản ứng].
  2. Truy vết sơ đồ: Biện luận từng nấc (Chất A -> Chất B). Chỉ nêu tác nhân, sự biến đổi khung C/nhóm chức và cơ chế trọng tâm (đóng/mở vòng, chuyển vị, cắt mạch).
  3. Tổng hợp CTCT: Liệt kê rõ CTCT thu gọn của tất cả các chất chưa biết.
  4. Kiểm chứng (Bắt buộc): Check nhanh kết quả với [CTPT, Δ, số mol/khối lượng, lập thể/đồng phân]. Nếu lệch dữ kiện -> tự sửa lại cấu trúc trước khi chốt."""

YEU_CAU_HSG = """ĐỀ KHÓ (HSG tỉnh/quốc gia) – suy luận kĩ theo [MODULE: BIỆN LUẬN CHẤT HỮU CƠ]:
Trình bày ngắn gọn bằng ký hiệu `->`, không văn mẫu, đi thẳng vào bản chất theo 4 bước:
1. Δ & Nhóm chức: Tính độ bất bão hoà Δ = (2C + 2 + N − H − X)/2 cho chất ẩn có CTPT; Dịch nhanh: [Dữ kiện] -> [Cấu tạo/Nhóm chức/Phản ứng].
2. Truy vết sơ đồ: Lập luận xuôi và ngược theo tác nhân, kiểm số C qua từng bước (Chất A -> Chất B); chỉ nêu tác nhân, sự biến đổi khung C/nhóm chức và cơ chế trọng tâm (Grignard, aldol/croton, Claisen, Cannizzaro, Diels–Alder, ozon phân, KMnO4 loãng lạnh/đặc nóng, HIO4, pinacol, Hofmann, Wittig, Friedel–Crafts, Clemmensen/Wolff–Kishner, LiAlH4/NaBH4, haloform, decarboxyl hoá, đóng/mở vòng, chuyển vị, cắt mạch).
3. Tổng hợp CTCT: Liệt kê rõ CTCT thu gọn của tất cả các chất chưa biết.
4. Kiểm chứng (Bắt buộc): Check nhanh kết quả với [CTPT, Δ, số mol/khối lượng, lập thể/đồng phân]. Nếu lệch dữ kiện -> tự sửa lại cấu trúc trước khi chốt. Lập thể quyết định đáp án thì ghi trong SMILES (@, @@, /, \\) và nêu trong luu_y."""

_CHUOI_SCHEMA = {"type": "object", "properties": {"ki_hieu": {"type": "string"}, "ctct": {"type": "string"}, "ten": {"type": "string"},
                                                   "smiles": {"type": "string"}},
                 "required": ["ki_hieu", "ctct", "ten", "smiles"], "additionalProperties": False}
_BUOC_SCHEMA = {"type": "object", "properties": {"tu": {"type": "string"}, "den": {"type": "string"}, "phan_ung": {"type": "string"},
                                                  "dieu_kien": {"type": "string"}},
                "required": ["tu", "den", "phan_ung", "dieu_kien"], "additionalProperties": False}
SCHEMA_GON = {
    "type": "object",
    "properties": {"chuoi": {"type": "array", "items": _CHUOI_SCHEMA}, "buoc": {"type": "array", "items": _BUOC_SCHEMA},
                   "luu_y": {"type": "array", "items": {"type": "string"}}, "dap_an": {"type": "string"}},
    "required": ["chuoi", "buoc", "luu_y", "dap_an"],
    "additionalProperties": False,
}
# Có tài liệu tra cứu (literature.py): mỗi bước ghi căn cứ – "[n]" (bài n) hoặc "suy luận"
_BUOC_TL = {**_BUOC_SCHEMA, "properties": {**_BUOC_SCHEMA["properties"], "can_cu": {"type": "string"}},
            "required": [*_BUOC_SCHEMA["required"], "can_cu"]}
SCHEMA_GON_TL = {**SCHEMA_GON, "properties": {**SCHEMA_GON["properties"], "buoc": {"type": "array", "items": _BUOC_TL}}}
# AI Riêng: mỗi bước ghi căn cứ = nhãn mục kho ([Q2], [M1]…) + bước chưa được dạy / chỗ kho mâu thuẫn hiểu biết chung
SCHEMA_AI_RIENG = {**SCHEMA_GON_TL, "properties": {**SCHEMA_GON_TL["properties"], "chua_duoc_day": {"type": "array", "items": {"type": "string"}},
                                                   "mau_thuan": {"type": "array", "items": {"type": "string"}}},
                   "required": [*SCHEMA_GON_TL["required"], "chua_duoc_day", "mau_thuan"]}
YEU_CAU_TAI_LIEU = """YÊU CẦU KHI CÓ TÀI LIỆU: dựa vào tài liệu dưới đây khi có thông tin liên quan (ưu tiên hơn trí nhớ); mỗi bước ghi can_cu:
"[n]" nếu bước đó lấy từ bài số n (nhiều bài: "[1], [3]"), "suy luận" nếu là suy luận của bạn (tài liệu không nói tới). Trong luu_y có
thể dẫn [n]. Không bịa nội dung không có trong tài liệu."""


# ---------------------------------------------------------------- mức độ thông minh
@dataclass
class CauHinhMuc:
    muc: str
    bac: str             # lite | flash | pro | claude
    suy_nghi: int        # ngân sách suy nghĩ Gemini
    effort: str          # Claude
    max_tokens: int
    kiem_tra: str        # bo | re | manh | ky
    claude_model: str = ""


def cau_hinh_muc(muc: str, dung_claude: bool) -> CauHinhMuc:
    s = settings
    if muc == "nhanh":
        return CauHinhMuc(muc, "lite", s.thinking_nhanh, "low", s.max_tokens_nhanh, "bo")
    if muc == "can_bang":
        return CauHinhMuc(muc, "flash", s.thinking_can_bang, "low", s.max_tokens_can_bang, "re")
    if muc == "toi_da":
        return CauHinhMuc(muc, "claude" if dung_claude else "pro", s.thinking_toi_da, s.claude_effort_toi_da, s.max_tokens_toi_da, "ky",
                          s.claude_model_manh if dung_claude else "")
    return CauHinhMuc("ky", "claude" if dung_claude else "pro", s.thinking_ky, s.claude_effort_ky, s.max_tokens_ky, "manh")


def chon_muc(yeu_cau: str, muc_do: str, quyen, bat_claude: bool) -> tuple[str, bool, str]:
    """(mức người dùng chọn – "" = tự động, mức độ đề, quyền Claude, người dùng bật "Dùng Claude") → (mức, dùng Claude?, thông báo).
    Tự động: Dễ → Nhanh, Trung bình → Cân bằng, HSG → Kỹ (bật "Dùng Claude" và có quyền → ít nhất Kỹ để giải bằng Claude).
    Tối đa chỉ dành cho người có quyền Claude (key chung / key riêng / mã) hoặc admin; người khác → Kỹ."""
    tb = ""
    muc = yeu_cau if yeu_cau in MUC_TM else MUC_THEO_DO_KHO.get(muc_do, "can_bang")
    if not yeu_cau and quyen is not None and bat_claude and MUC_TM.index(muc) < MUC_TM.index("ky"):
        muc = "ky"
    if muc == "toi_da" and quyen is None:
        muc, tb = "ky", "Mức Tối đa chỉ dành cho người có quyền Claude (Key Claude, key riêng, mã Claude) – dùng mức Kỹ."
    claude = (quyen is not None and muc in ("ky", "toi_da")
              and (bool(getattr(quyen, "moi_muc", False)) or muc_do == "hsg"))
    return muc, claude, tb


# ---------------------------------------------------------------- cache (7 ngày)
def chuan_de(text: str) -> str:
    t = unicodedata.normalize("NFC", parser.chuan_hoa(text or ""))
    t = re.sub(r"\s*(?:-+>|=+>|⟶|→)\s*", " → ", t)
    return re.sub(r"\s+", " ", t).strip()


def khoa_cache(de_chuan: str, anh: bytes | None, muc: str, nha_cung_cap: str) -> str:
    h = hashlib.sha256(de_chuan.encode())
    if anh:
        h.update(b"|anh|" + hashlib.sha256(anh).digest())
    h.update(f"|{muc}|{nha_cung_cap}".encode())
    return h.hexdigest()


def doc_cache(khoa: str) -> dict | None:
    if settings.cache_giai_ngay <= 0:
        return None
    with db.ket_noi() as con:
        r = con.execute("SELECT data FROM giai_cache WHERE key = ? AND created >= ?",
                        (khoa, time.time() - settings.cache_giai_ngay * 86400)).fetchone()
    return json.loads(r[0]) if r else None


def ghi_cache(khoa: str, muc: str, du_lieu: dict) -> None:
    if settings.cache_giai_ngay <= 0:
        return
    with db.ket_noi() as con:
        con.execute("INSERT OR REPLACE INTO giai_cache (key, created, level, data) VALUES (?, ?, ?, ?)",
                    (khoa, time.time(), muc, json.dumps(du_lieu, ensure_ascii=False)))
        con.execute("DELETE FROM giai_cache WHERE created < ?", (time.time() - max(1, settings.cache_giai_ngay) * 86400,))


# ---------------------------------------------------------------- streaming: đọc dần JSON để hiện từng chất / bước
def _mang_xong(buf: str, ten: str) -> list[dict]:
    """Các object ĐÃ VIẾT XONG trong mảng `ten` của JSON đang được stream (chưa đủ thì bỏ qua)."""
    i = buf.find(f'"{ten}"')
    j = buf.find("[", i) if i >= 0 else -1
    if j < 0:
        return []
    dec, kq, k = json.JSONDecoder(), [], j + 1
    while True:
        while k < len(buf) and buf[k] in " \n\r\t,":
            k += 1
        if k >= len(buf) or buf[k] != "{":
            return kq
        try:
            obj, k = dec.raw_decode(buf, k)
        except ValueError:
            return kq
        if isinstance(obj, dict):
            kq.append(obj)


class DocDan:
    """Nhận từng đoạn chữ (llm khi_co_chu) → gọi khi_moi(chuoi, buoc) mỗi khi có thêm chất / bước viết xong."""

    def __init__(self, khi_moi: Callable[[list[dict], list[dict]], None]):
        self.buf, self.khi_moi, self.da = "", khi_moi, (0, 0)

    def nhan(self, doan: str | None) -> None:
        if doan is None:  # mô hình bắt đầu lại (thử lại / đổi mô hình)
            self.buf, self.da = "", (0, 0)
            return
        self.buf += doan
        chuoi, buoc = _mang_xong(self.buf, "chuoi"), _mang_xong(self.buf, "buoc")
        if (len(chuoi), len(buoc)) != self.da:
            self.da = (len(chuoi), len(buoc))
            try:
                self.khi_moi(chuoi, buoc)
            except Exception:  # noqa: BLE001 – hiển thị dần lỗi không được làm hỏng bài giải
                log.exception("Lỗi khi hiện dần lời giải")


# ---------------------------------------------------------------- giải (một lần gọi) + kiểm tra RDKit + sửa
def _doc_gon(text: str) -> LoiGiaiGon:
    try:
        gon = LoiGiaiGon.model_validate(doc_json(text))
    except (ValueError, ValidationError) as e:
        raise LoiAI("AI trả lời giải không đúng định dạng JSON.", "dinh_dang") from e
    if not gon.chuoi or not gon.buoc:
        raise LoiAI("AI không trả về chuỗi phản ứng nào.", "dinh_dang")
    return gon


def giai(de: str, anh: tuple[str, str] | None, cfg: CauHinhMuc, muc_do: str = "", bao: Callable[[str], None] | None = None,
         khi_moi: Callable[[list[dict], list[dict]], None] | None = None, tai_lieu: str = "", kho_rieng: str = "",
         system_them: str = "") -> tuple[LoiGiaiGon, PhienHoiThoai, str]:
    """Một lần gọi AI (streaming) → (lời giải gọn, phiên – để sửa tiếp, tên mô hình).
    tai_lieu: đoạn trích bài báo (literature.tra_cuu → khoi_prompt) – đặt ở lượt người dùng (phần system cố định vẫn được cache).
    kho_rieng: khối KHO KIẾN THỨC RIÊNG của AI Riêng (ai_rieng/tim.py) + system_them: system prompt của AI Riêng (nối sau system chung)."""
    schema = SCHEMA_AI_RIENG if kho_rieng else SCHEMA_GON_TL if tai_lieu else SCHEMA_GON
    phien = PhienHoiThoai(SYSTEM_GIAI_GON + ("\n\n" + system_them if system_them else ""), output_schema=schema, effort=cfg.effort,
                          max_tokens=cfg.max_tokens, bao=bao, bac=cfg.bac, suy_nghi=cfg.suy_nghi, claude_model=cfg.claude_model)
    yeu_cau = ("Đề (văn bản):\n" + de.strip()) if de.strip() else "Đề nằm trong ảnh đính kèm."
    if de.strip() and anh:
        yeu_cau += "\n(Ảnh đính kèm là đề gốc; văn bản ở trên là phần bổ sung.)"
    if muc_do == "hsg" or cfg.muc in ("ky", "toi_da"):
        yeu_cau += "\n\n" + YEU_CAU_HSG
    if tai_lieu:
        yeu_cau += "\n\nTÀI LIỆU THAM KHẢO (đoạn trích hợp pháp; [n] = số thứ tự bài):\n" + tai_lieu + "\n\n" + YEU_CAU_TAI_LIEU
    if kho_rieng:
        yeu_cau += "\n\n" + kho_rieng
    doc_dan = DocDan(khi_moi) if khi_moi else None
    ph = phien.gui(text=yeu_cau, anh=anh, khi_co_chu=doc_dan.nhan if doc_dan else None)
    try:
        gon = _doc_gon(ph.text)
    except LoiAI:
        if bao:
            bao("Kết quả chưa đúng định dạng → nhờ AI trả lại JSON")
        ph = phien.gui(text="Câu trả lời trước không phải JSON hợp lệ theo schema (hoặc bị cắt). Hãy trả lại DUY NHẤT JSON gọn đó.",
                       khi_co_chu=doc_dan.nhan if doc_dan else None)
        gon = _doc_gon(ph.text)
    return gon, phien, _mo_ta(ph)


def _mo_ta(ph) -> str:
    return mo_ta_mo_hinh(ph)  # Gemini (…) / Claude (…) / ChatGPT (…)


_TEN_NGUYEN_TO = {"brom": "Br", "clo": "Cl", "chlor": "Cl", "iot": "I", "iod": "I", "flo": "F", "fluor": "F", "nước": "O", "oxi": "O",
                  "oxy": "O", "ozon": "O", "nitơ": "N", "amoni": "N", "amin": "N", "nitr": "N", "lưu huỳnh": "S", "sulf": "S", "natri": "Na",
                  "kali": "K", "magie": "Mg", "kẽm": "Zn", "đồng": "Cu", "bạc": "Ag", "photpho": "P", "phosph": "P", "xianua": "N", "cyan": "N"}


def _nguyen_to_trong_chu(text: str) -> set[str]:
    """Nguyên tố có thể đến từ tác nhân ghi bằng chữ ("HBr", "H2SO4 đặc", "nước brom", "1) O3; 2) Zn/H2O")."""
    t = text or ""
    co = set(re.findall(r"[A-Z][a-z]?", t))
    thuong = t.lower()
    co |= {v for k, v in _TEN_NGUYEN_TO.items() if k in thuong}
    return co


def _ctpt_de_cho(de: str) -> dict[str, str]:
    """"X (C3H6O)", "A1 (C4H8)" trong đề → {kí hiệu: CTPT}."""
    return {m.group(1): m.group(2) for m in re.finditer(r"\b([A-Z][A-Za-z0-9']{0,3})\s*\(\s*(C\d*H\d*[A-Za-z0-9]*)\s*\)", de or "")}


def kiem_tra(gon: LoiGiaiGon, de: str = "") -> tuple[list[str], list[str], dict[int, list[str]]]:
    """RDKit (không gọi AI) → (lỗi cần sửa, cảnh báo nhẹ, lỗi theo bước)."""
    loi: list[str] = []
    canh_bao: list[str] = []
    theo_buoc: dict[int, list[str]] = {}
    nguyen_to: dict[str, set[str]] = {}
    ctpt = _ctpt_de_cho(de)
    for c in gon.chuoi:
        mol, tin = validator.mol_tu_smiles(c.smiles)
        if mol is None:
            loi.append(f"{c.ki_hieu}: {tin} (RDKit)")
            continue
        dem = validator.dem_nguyen_to(mol)[0]
        nguyen_to[c.ki_hieu] = set(dem)
        can = validator.cong_thuc_tu_chu(ctpt[c.ki_hieu]) if c.ki_hieu in ctpt else (
            None if re.fullmatch(r"[A-Z][a-z]?[0-9']*", c.ki_hieu.strip()) else validator.cong_thuc_tu_chu(c.ki_hieu))
        if can and "*" not in c.smiles and validator.cong_thuc_hill(can) != validator.cong_thuc_hill(dem):
            loi.append(f"{c.ki_hieu}: SMILES {c.smiles} có công thức {validator.cong_thuc_hill(dem)}, khác "
                       f"{'CTPT đề cho' if c.ki_hieu in ctpt else 'công thức đề viết'} {validator.cong_thuc_hill(can)}")
            continue
        # CTCT AI ghi và SMILES phải là CÙNG một chất (vd ghi C6H5CH2OH nhưng SMILES là OCCc1ccccc1 → C7H8O ≠ C8H10O; RDKit
        # không bắt được vì kí hiệu "B" không có công thức) – chỉ so khi CTCT đọc được thành công thức
        ct = validator.cong_thuc_tu_chu(c.ctct) if c.ctct and "*" not in c.smiles else None
        if ct and validator.cong_thuc_hill(ct) != validator.cong_thuc_hill(dem):
            loi.append(f"{c.ki_hieu}: CTCT {c.ctct} ({validator.cong_thuc_hill(ct)}) và SMILES {c.smiles} ({validator.cong_thuc_hill(dem)}) "
                       "không cùng một chất – sửa cho khớp")
    ki_hieu = {c.ki_hieu for c in gon.chuoi}
    for i, b in enumerate(gon.buoc):
        if b.tu not in ki_hieu or b.den not in ki_hieu:
            theo_buoc.setdefault(i, []).append(f"Bước {i + 1}: “{b.tu} → {b.den}” dùng kí hiệu không có trong danh sách chất")
        elif b.tu in nguyen_to and b.den in nguyen_to:
            moi = nguyen_to[b.den] - nguyen_to[b.tu] - _nguyen_to_trong_chu(f"{b.phan_ung} {b.dieu_kien}") - {"H"}
            if moi:
                canh_bao.append(f"Bước {i + 1} ({b.tu} → {b.den}): nguyên tố {', '.join(sorted(moi))} không thấy ở chất trước hay tác nhân "
                                f"“{b.dieu_kien or b.phan_ung}”")
    phan = parser.tach_chuoi(de) if de and "→" in chuan_de(de) else []
    if len(phan) >= 3 and len(gon.buoc) < len(phan) - 1:
        loi.append(f"Đề có {len(phan) - 1} mũi tên nhưng lời giải chỉ có {len(gon.buoc)} bước")
    # Đề là chuỗi chữ thẳng (A → B → C, mỗi chất một "từ") → từng bước phải nối đúng cặp chất theo thứ tự của đề
    don_gian = [validator._khoa_ki_hieu(x) for x in phan]
    if len(phan) == len(gon.buoc) + 1 >= 3 and all(re.fullmatch(r"[A-Z0-9()'≡=]+", x or "") for x in don_gian):
        for i, b in enumerate(gon.buoc):
            tu, den = validator._khoa_ki_hieu(b.tu), validator._khoa_ki_hieu(b.den)
            if (tu, den) != (don_gian[i], don_gian[i + 1]):
                theo_buoc.setdefault(i, []).append(f"Bước {i + 1}: đề là {phan[i]} → {phan[i + 1]} nhưng lời giải ghi {b.tu} → {b.den}")
    return loi + [x for v in theo_buoc.values() for x in v], canh_bao, theo_buoc


def sua(phien: PhienHoiThoai | None, gon: LoiGiaiGon, loi: list[str], cfg: CauHinhMuc, ra_soat: bool = False,
        bao: Callable[[str], None] | None = None) -> tuple[LoiGiaiGon, str]:
    """Nhờ AI sửa / rà soát. Cân bằng ("re") → mô hình rẻ (Flash-Lite) phiên mới; Kỹ / Tối đa → chính phiên đã giải (đủ ngữ cảnh)."""
    noi_dung = ("Rà soát kĩ lời giải của bạn (cấu tạo, sản phẩm chính, lập thể, chuyển vị, điều kiện). Đúng thì trả lại nguyên JSON; "
                "sai thì trả JSON đã sửa." if ra_soat else "RDKit báo lỗi trong lời giải:\n- " + "\n- ".join(loi)
                + "\nHãy sửa và trả lại TOÀN BỘ JSON gọn (cùng schema).")
    if ra_soat and loi:
        noi_dung += "\nRDKit cũng báo:\n- " + "\n- ".join(loi)
    if cfg.kiem_tra == "re" or phien is None:
        co_can_cu = any(b.can_cu for b in gon.buoc)
        phien = PhienHoiThoai(SYSTEM_GIAI_GON, output_schema=SCHEMA_GON_TL if co_can_cu else SCHEMA_GON, effort="low",
                              max_tokens=settings.max_tokens_can_bang, bao=bao, bac="lite", suy_nghi=settings.thinking_nhanh)
        noi_dung = "Lời giải cần kiểm tra:\n" + json.dumps(gon.model_dump(), ensure_ascii=False) + "\n\n" + noi_dung
    ph = phien.gui(text=noi_dung)
    return _doc_gon(ph.text), _mo_ta(ph)


def sang_loi_giai(gon: LoiGiaiGon, theo_buoc: dict[int, list[str]] | None = None) -> LoiGiai:
    """Kết quả gọn → LoiGiai nội bộ (để vẽ chất, ảnh đáp án, .cdxml, .sdf và nút Cơ chế / 3D dùng lại như cũ)."""
    theo_buoc = theo_buoc or {}
    chat = {c.ki_hieu: c for c in gon.chuoi}

    def cpu(kh: str) -> list[ChatPhanUng]:
        c = chat.get(kh)
        return [ChatPhanUng(ten=c.ten or c.ki_hieu, smiles=c.smiles)] if c and c.smiles else []

    so_do = [NutSoDo(ki_hieu=c.ki_hieu, cong_thuc=c.ctct, ten=c.ten, smiles=c.smiles) for c in gon.chuoi]
    for b in gon.buoc:  # bước dùng kí hiệu không có trong danh sách chất (đã bị RDKit đánh dấu) → chất giữ chỗ để vẫn vẽ được sơ đồ
        for kh in (b.tu, b.den):
            if kh not in chat:
                chat[kh] = None
                so_do.append(NutSoDo(ki_hieu=kh))
    return LoiGiai(
        so_do=so_do,
        cac_buoc=[Buoc(so=i + 1, chat_dau=b.tu, chat_sau=b.den, chat_tham_gia=cpu(b.tu), san_pham=cpu(b.den), dieu_kien=b.dieu_kien,
                       loai_phan_ung=b.phan_ung, can_kiem_tra=bool(theo_buoc.get(i)), loi=theo_buoc.get(i, []))
                  for i, b in enumerate(gon.buoc)],
        luu_y=gon.luu_y[:6], dap_an=gon.dap_an,
    )


def gon_tu_loi_giai(sol: LoiGiai) -> dict:
    """LoiGiai cũ (chế độ demo, bài lưu trước đây) → dạng chuỗi gọn để giao diện hiển thị thống nhất."""
    return {"chuoi": [{"ki_hieu": n.ki_hieu, "ctct": n.cong_thuc, "ten": n.ten, "smiles": n.smiles} for n in sol.so_do],
            "buoc": [{"tu": b.chat_dau, "den": b.chat_sau, "phan_ung": b.loai_phan_ung, "dieu_kien": b.dieu_kien} for b in sol.cac_buoc],
            "luu_y": sol.luu_y or ([sol.ghi_chu] if sol.ghi_chu else []), "dap_an": sol.dap_an}


# ---------------------------------------------------------------- "Xem phương trình chi tiết" (chỉ gọi AI khi người dùng bấm)
SYSTEM_PT = """Bạn là giáo viên Hoá. Cho lời giải chuỗi dạng gọn (JSON), viết PHƯƠNG TRÌNH đã cân bằng cho từng bước, công thức cấu tạo thu
gọn kiểu SGK, hệ số đứng trước, "→" (thuận nghịch "⇌"), điều kiện ghi sau trong ngoặc. Polime: (C6H10O5)n + nH2O → nC6H12O6.
Trả DUY NHẤT JSON {"phuong_trinh":[{"buoc":1,"phuong_trinh":"...","ghi_chu":""}]} – ghi_chu ≤ 1 câu (sản phẩm phụ, cơ chế), không văn xuôi."""
SCHEMA_PT = {"type": "object", "properties": {"phuong_trinh": {"type": "array", "items": {
    "type": "object", "properties": {"buoc": {"type": "integer"}, "phuong_trinh": {"type": "string"}, "ghi_chu": {"type": "string"}},
    "required": ["buoc", "phuong_trinh", "ghi_chu"], "additionalProperties": False}}},
    "required": ["phuong_trinh"], "additionalProperties": False}


def phuong_trinh_chi_tiet(gon: dict, de: str = "") -> tuple[list[dict], str]:
    phien = PhienHoiThoai(SYSTEM_PT, output_schema=SCHEMA_PT, effort="low", max_tokens=8000, bac="flash", suy_nghi=settings.thinking_can_bang)
    ph = phien.gui(text=(f"Đề: {de.strip()[:1500]}\n\n" if de.strip() else "") + "Lời giải gọn:\n" + json.dumps(gon, ensure_ascii=False))
    try:
        ds = doc_json(ph.text).get("phuong_trinh") or []
    except (ValueError, AttributeError) as e:
        raise LoiAI("AI trả phương trình không đúng định dạng – thử lại.", "dinh_dang") from e
    return [{"buoc": int(x.get("buoc") or i + 1), "phuong_trinh": str(x.get("phuong_trinh", "")), "ghi_chu": str(x.get("ghi_chu", ""))}
            for i, x in enumerate(ds) if isinstance(x, dict)], _mo_ta(ph)
