"""Mục Cơ chế nhận ĐỀ BÀI mới: đề gõ chữ hoặc ảnh đề → AI (Claude) viết cơ chế → RDKit kiểm → dựng 3D + sơ đồ giấy.

Viết lại từ app CHEMISTRY (backend/co_che.py: SCHEMA_CO_CHE, SYSTEM_CO_CHE, sinh_bang_ai; backend/co_che_anh.py: đọc ảnh đề),
bỏ phần đăng nhập/kế hoạch. AI cho cả chữ và ảnh: Claude trước (ANTHROPIC_API_KEY, mô hình CLAUDE_MODEL_CO_CHE/CLAUDE_MODEL);
Claude không có khóa hoặc khóa bị từ chối (401/403) → Gemini qua REST (GEMINI_API_KEY, GEMINI_MODEL_CO_CHE hoặc
gemini-pro-latest → gemini-flash-latest), như CHEMISTRY gốc vẫn dùng Gemini làm chính. Cả hai hỏng → LoiGiaiDe 503 nêu cả hai lý do.

    giai_de(van_ban="CH3CH2Br + NaOH loãng", anh=None) → dict   (async)

Luồng:
  1. Có ảnh → kiểm tra ảnh (dung lượng, định dạng, kích thước, độ sáng; HEIC → JPG bằng sips trên macOS) → Claude đọc ảnh ra
     đề + phản ứng (SMILES) theo SCHEMA_DOC_ANH; SMILES RDKit không đọc được → gửi lại cho Claude sửa (SO_LAN_DOC_LAI lần).
  2. Claude viết cơ chế theo SCHEMA_CO_CHE → co_che.kiem_tra (RDKit mô phỏng từng mũi tên) → lỗi gửi lại cho Claude sửa
     (SO_LAN_SUA lần, cùng một hội thoại). Vẫn lỗi → LoiGiaiDe 422 kèm danh sách lỗi (không dựng 3D cơ chế sai).
  3. Đạt → dung_kich_ban_3d.dung (→ .3d.json) và hoc_ve.ve_theo_mau.tao_so_do (→ .giay.json), như dung_bai_demo.
Kết quả là JSON THƯỜNG (không mã hóa) cùng khung với gói .verapack: {"phien_ban", "ma", "bai", "kich_ban_3d", "so_do_giay"}
+ "co_che", "doc_anh", "thong_tin" — app mở bằng BaiCoChe.TuJson như 8 bài mẫu.

Kiểm thử thay hàm gọi AI bằng `goi_ai=` (không gọi mạng): async (system, messages, schema) -> str (JSON).
"""
from __future__ import annotations

import asyncio
import base64
import hashlib
import io
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
from collections import OrderedDict
from pathlib import Path
from typing import Awaitable, Callable

from rdkit import Chem

from . import co_che as cc_mod
from . import dung_kich_ban_3d as d3
from .hoc_ve import ve_theo_mau

LOAI_CO_CHE = cc_mod.LOAI_CO_CHE
KIEU_MUI_TEN = cc_mod.KIEU_MUI_TEN
SO_LAN_SUA = 3                      # cơ chế sai → nhờ AI sửa tối đa 3 lần
SO_LAN_DOC_LAI = 2                  # SMILES đọc từ ảnh sai → nhờ AI đọc lại tối đa 2 lần
ANH_TOI_DA = 8 * 1024 * 1024        # byte mỗi ảnh (sau khi giải base64)
CANH_NHO_NHAT = 200                 # px
CANH_LON_NHAT = 1600                # px – ảnh lớn hơn được thu nhỏ trước khi gửi (đủ nét, ít token)
DO_SANG_TOI_THIEU = 28
VAN_BAN_TOI_DA = 4000
CHO_MAC_DINH = 300.0                # giây cho cả lượt giải (đọc ảnh + viết + sửa + dựng 3D/giấy)

# Mức tư duy (thanh trượt ở app, 20/11): 0 Nhanh · 1 Cân bằng · 2 Kỹ · 3 Tối đa (đề HSG quốc gia).
# Mỗi mức: effort của Claude, thứ tự mô hình Gemini (pro nghĩ kỹ hơn, flash nhanh hơn), thời gian tối đa cho MỘT cơ chế (≤ 3 phút).
MUC_TU_DUY = {
    0: {"ten": "nhanh", "effort": "low", "gemini": ["gemini-flash-latest", "gemini-flash-lite-latest"], "cho": 60.0},
    1: {"ten": "can_bang", "effort": "medium", "gemini": ["gemini-pro-latest", "gemini-flash-latest"], "cho": 120.0},
    2: {"ten": "ky", "effort": "high", "gemini": ["gemini-pro-latest", "gemini-flash-latest"], "cho": 150.0},
    3: {"ten": "toi_da", "effort": "high", "gemini": ["gemini-pro-latest", "gemini-flash-latest"], "cho": 180.0},
}
import contextvars as _cv  # noqa: E402
muc_hien_tai: _cv.ContextVar[int] = _cv.ContextVar("muc_tu_duy", default=1)


def muc_tu_duy(muc) -> dict:
    try:
        return MUC_TU_DUY[max(0, min(3, int(muc)))]
    except (TypeError, ValueError):
        return MUC_TU_DUY[1]
CHO_MOI_LAN_GOI = 180.0             # giây cho một lần gọi Claude
CHO_GEMINI = 90.0                   # giây cho một lần gọi một mô hình Gemini (quá thì thử mô hình sau)
VAN_DE_ANH = ("", "khong_phai_de_hoa", "mo", "toi", "bi_cat", "khac")

GoiAI = Callable[[str, list, dict], Awaitable[str]]


class LoiGiaiDe(RuntimeError):
    """Lỗi báo cho người dùng (tiếng Việt) + mã HTTP + danh sách chi tiết (vd lỗi RDKit còn lại)."""

    def __init__(self, thong_diep: str, ma: str = "giai_de", status: int = 422, chi_tiet: list[str] | None = None):
        super().__init__(thong_diep)
        self.thong_diep, self.ma, self.status, self.chi_tiet = thong_diep, ma, status, chi_tiet or []


# ================================================================ schema + lời dặn AI (từ CHEMISTRY)
_SO_NGUYEN = {"type": "array", "items": {"type": "integer"}}
SCHEMA_CO_CHE = {
    "type": "object",
    "properties": {
        "ten": {"type": "string"},
        "loai": {"type": "string"},
        "dieu_kien": {"type": "string"},
        "giai_thich": {"type": "string"},
        "nhan_hien_thi": {"type": "array", "items": {"type": "object", "properties": {"map": {"type": "integer"}, "nhan": {"type": "string"}},
                                                     "required": ["map", "nhan"], "additionalProperties": False}},
        "buoc": {"type": "array", "items": {"type": "object", "properties": {
            "chat": {"type": "array", "items": {"type": "string"}},
            "mui_ten": {"type": "array", "items": {"type": "object", "properties": {
                "tu": {"type": "object", "properties": {"loai": {"type": "string", "enum": ["cap_e", "lien_ket"]}, "nguyen_tu": _SO_NGUYEN},
                       "required": ["loai", "nguyen_tu"], "additionalProperties": False},
                "den": {"type": "object", "properties": {"loai": {"type": "string", "enum": ["nguyen_tu", "lien_ket"]}, "nguyen_tu": _SO_NGUYEN},
                        "required": ["loai", "nguyen_tu"], "additionalProperties": False},
                "kieu": {"type": "string", "enum": list(KIEU_MUI_TEN)}},
                "required": ["tu", "den", "kieu"], "additionalProperties": False}},
            "chuyen_h": {"type": "array", "items": {"type": "object", "properties": {"tu": {"type": "integer"}, "den": {"type": "integer"}},
                                                    "required": ["tu", "den"], "additionalProperties": False}},
            "trang_thai_chuyen_tiep": {"type": "boolean"},
            "buoc_cham": {"type": "boolean"},
            "thuan_nghich": {"type": "boolean"},
            "nhan_trung_gian": {"type": "string"},
            "cong_huong": {"type": "array", "items": {"type": "string"}},
            "dieu_kien": {"type": "string"},
            "ghi_chu": {"type": "string"}},
            "required": ["chat", "mui_ten", "chuyen_h", "trang_thai_chuyen_tiep", "buoc_cham", "thuan_nghich", "nhan_trung_gian",
                         "cong_huong", "dieu_kien", "ghi_chu"], "additionalProperties": False}},
        "san_pham": {"type": "array", "items": {"type": "string"}},
        # hướng tấn công KHÔNG thể xảy ra (bị màn chắn chặn trong phim 3D): dich = [map nguyên tử tấn công, map nguyên tử đích]
        "nhanh_sai": {"type": "array", "items": {"type": "object", "properties": {
            "tu_buoc": {"type": "integer"}, "dich": _SO_NGUYEN, "ten": {"type": "string"}, "ly_do": {"type": "string"},
            "kieu_chan": {"type": "string"}},
            "required": ["tu_buoc", "dich", "ten", "ly_do", "kieu_chan"], "additionalProperties": False}},
    },
    "required": ["ten", "loai", "dieu_kien", "giai_thich", "nhan_hien_thi", "buoc", "san_pham", "nhanh_sai"],
    "additionalProperties": False,
}

SYSTEM_CO_CHE = """Bạn là chuyên gia hoá hữu cơ, ra đề và chấm thi HỌC SINH GIỎI QUỐC GIA. Viết CƠ CHẾ phản ứng dạng JSON theo schema.
App VERA Lab dựng cơ chế thành phim 3D và sơ đồ giấy cho học sinh THPT Việt Nam (chương trình GDPT 2018).

YÊU CẦU NỘI DUNG:
- Đủ TỪNG BƯỚC CƠ BẢN (elementary step), KHÔNG gộp bước: proton hoá, tấn công, tách, chuyển proton, chuyển vị… mỗi cái một bước
  (chuyển proton đơn giản giữa hai nguyên tử dùng "chuyen_h" thay cho mũi tên, app vẽ rút gọn "~H⁺").
- Ghi rõ chất trung gian ("nhan_trung_gian": tên/kiểu chất trung gian TẠO RA sau bước đó, vd "cacbocation bậc 3", "ion arenium",
  "carbanion enolat"); "trang_thai_chuyen_tiep": true cho bước đồng bộ cần vẽ TS (SN2, E2, pericyclic…); "buoc_cham": true cho bước
  quyết định tốc độ; "thuan_nghich": true cho cân bằng (⇌).
- Hoá học lập thể: SMILES phải ghi đủ @/@@ và /\\ khi chất có lập thể; nêu rõ nghịch chuyển/giữ cấu hình/raxemic hoá, anti-periplanar,
  syn/anti, endo/exo… trong ghi_chu.
- Cấu trúc cộng hưởng quan trọng của chất trung gian: "cong_huong" (SMILES CÓ map, CÙNG bộ map, cùng số H, cùng tổng điện tích với
  tiểu phân đó trong "chat" của bước – chỉ dời electron).
- Chuyển vị (1,2-H, 1,2-ankyl, Wagner–Meerwein, pinacol, sigmatropic…) là bước riêng, có mũi tên.
- "giai_thich": giải thích NGẮN hướng phản ứng – vì sao chọn vị trí đó (bền cacbocation, Markovnikov/Zaitsev/Hofmann, định hướng thế,
  khống chế động học/nhiệt động…), sản phẩm chính/phụ.
- "ten": tên ngắn của cơ chế kèm phương trình gọn, vd "SN1: (CH₃)₃CBr + H₂O → (CH₃)₃COH + HBr".
- Tác nhân dạng ion (NaOH → [OH-], NaOCH3 → C[O-]); bỏ ion khán giả (Na⁺, K⁺).

QUY TẮC DỮ LIỆU (app kiểm tra bằng RDKit và mô phỏng từng mũi tên):
1. "chat" của mỗi bước = SMILES của MỌI tiểu phân trước khi đẩy electron ở bước đó. MỌI nguyên tử nặng có atom map duy nhất trong bước
   và GIỮ NGUYÊN số map qua các bước. Viết số H trong ngoặc ([CH2:3], [OH-:1], [CH3:5] gốc tự do). H chuyển chỗ bằng mũi tên phải là
   nguyên tử riêng có map [H:7]. Nhóm thế chung (R, Ar, B:, Nu⁻, E⁺) có thể dùng nguyên tử giả [*:9] và khai báo "nhan_hien_thi".
2. Mũi tên: kieu "day_du" = 1 CẶP e; "nua_dau" = 1 e (gốc tự do: phân cắt đồng li cần 2 mũi tên nửa đầu, cộng gốc vào liên kết π cần 3);
   "vong" = cặp e trong phản ứng pericyclic (vẽ vòng khép kín).
   tu {loai: cap_e, nguyen_tu: [A]} (cặp e hoặc e độc thân của A) hoặc {loai: lien_ket, nguyen_tu: [A, B]}.
   den {loai: nguyen_tu, nguyen_tu: [B]} hoặc {loai: lien_ket, nguyen_tu: [X, Y]} (liên kết ĐANG HÌNH THÀNH/tăng bậc giữa X và Y).
   cap_e A → nguyen_tu B: tạo liên kết A–B. lien_ket [A,B] → nguyen_tu B: electron về B. lien_ket [A,B] → lien_ket [B,C]: thành liên kết B–C.
   Đích là nguyên tử NGOÀI liên kết nguồn thì phải dùng den loai lien_ket.
3. Áp TOÀN BỘ mũi tên (và chuyen_h) của bước i lên "chat" bước i phải ra ĐÚNG các chất (cùng map, cùng điện tích, cùng số H) trong
   "chat" bước i+1; bước cuối ra "san_pham" (san_pham cũng CÓ map). Bước sau được thêm chất mới (map mới) và bỏ sản phẩm phụ đã tách ra.
   Tâm lập thể không tham gia phản ứng phải giữ nguyên @/@@.
4. ghi_chu: 1–2 câu tiếng Việt cho mỗi bước. "dieu_kien" của bước: chỉ ghi khi khác điều kiện chung.
5. Đề có NHIỀU phản ứng: chỉ viết cơ chế cho phản ứng người dùng chỉ định, không chỉ định thì phản ứng ĐẦU TIÊN cần vẽ cơ chế.
6. "nhanh_sai": 0–2 HƯỚNG TẤN CÔNG KHÔNG THỂ xảy ra mà học sinh hay nhầm (vd SN2 vào C bậc III bị cản trở không gian;
   nucleophin tấn công phía trước thay vì phía sau; cộng ngược Markovnikov; tấn công vào C không mang điện tích dương).
   tu_buoc = số thứ tự bước (bắt đầu từ 1) mà tại đó học sinh có thể nhầm; dich = [map nguyên tử tấn công, map nguyên tử bị
   tấn công SAI]; ten ngắn (≤ 8 từ), ly_do 1 câu; kieu_chan = "can_tro_khong_gian" | "sai_huong" | "khong_ben". Không có thì []."""

SYSTEM_DOC_ANH = """Bạn đọc ẢNH ĐỀ Hoá hữu cơ (chương trình THPT Việt Nam, kể cả đề HSG tỉnh/quốc gia) để app vẽ CƠ CHẾ phản ứng. KHÔNG giải đề.
Trả về DUY NHẤT một JSON đúng schema:
- de_bai: chép lại TOÀN VĂN đề (chữ in và chữ viết tay), giữ đánh số câu; công thức viết dạng chữ (CH3CH2Br, C6H5-CH=CH2…).
- phan_ung: mỗi phản ứng / câu hỏi cần vẽ cơ chế là một phần tử:
  so_cau ("Câu 1", "2a"…, không đánh số thì "1", "2"… theo thứ tự); chat_dau: SMILES các chất phản ứng VÀ tác nhân dạng tiểu phân thật
  sự phản ứng (NaOH → [OH-]; NaOCH3 → C[O-]; HBr → Br; Br2 → BrBr; H2O → O); tac_nhan_dieu_kien: chữ ghi trên/dưới mũi tên (xúc tác,
  dung môi, nhiệt độ, as, H+…); san_pham: SMILES sản phẩm đề đã cho – sản phẩm đề BẮT TÌM (chưa biết, X, Y…) ghi chuỗi rỗng "";
  yeu_cau: đề yêu cầu gì ("vẽ cơ chế", "giải thích sản phẩm chính", "so sánh tốc độ"…);
  goi_y_loai: mã loại cơ chế ĐÚNG về hoá học trong danh sách {DS_LOAI}; không thuộc danh sách / không chắc → "".
- khong_chac_chan: những chỗ đọc KHÔNG rõ – ghi rõ câu nào, chất nào, KÈM SMILES đã đoán và lý do.
- chat_luong_anh: doc_duoc (đọc được nội dung hoá học không); van_de: "" | "khong_phai_de_hoa" | "mo" | "toi" | "bi_cat" | "khac";
  mo_ta: MỘT câu tiếng Việt cụ thể cho người dùng (để trống nếu ảnh tốt).
Đọc công thức cấu tạo: dạng khung (mỗi đỉnh/đầu mút là C, đủ H), thu gọn, nêm đậm/nêm gạch (ghi lập thể @/@@), liên kết đôi E/Z (/ \\),
Newman, ghế, Fischer, Haworth (chuyển thành SMILES có lập thể đúng); điện tích, ion; mũi tên phản ứng (→, ⇌) và điều kiện trên mũi tên.
SMILES phải hợp lệ với RDKit (đúng hoá trị, ion có ngoặc vuông: [Na+], [OH-], [NH4+]). Không bịa chất không có trong ảnh."""
SYSTEM_DOC_ANH = SYSTEM_DOC_ANH.replace("{DS_LOAI}", "; ".join(f"{k} = {v}" for k, v in LOAI_CO_CHE.items()))

_CHUOI_DS = {"type": "array", "items": {"type": "string"}}
SCHEMA_DOC_ANH = {
    "type": "object",
    "properties": {
        "de_bai": {"type": "string"},
        "phan_ung": {"type": "array", "items": {
            "type": "object",
            "properties": {"so_cau": {"type": "string"}, "chat_dau": _CHUOI_DS, "tac_nhan_dieu_kien": {"type": "string"}, "san_pham": _CHUOI_DS,
                           "yeu_cau": {"type": "string"}, "goi_y_loai": {"type": "string", "enum": ["", *LOAI_CO_CHE]}},
            "required": ["so_cau", "chat_dau", "tac_nhan_dieu_kien", "san_pham", "yeu_cau", "goi_y_loai"], "additionalProperties": False}},
        "khong_chac_chan": _CHUOI_DS,
        "chat_luong_anh": {"type": "object", "properties": {
            "doc_duoc": {"type": "boolean"}, "van_de": {"type": "string", "enum": list(VAN_DE_ANH)}, "mo_ta": {"type": "string"}},
            "required": ["doc_duoc", "van_de", "mo_ta"], "additionalProperties": False},
    },
    "required": ["de_bai", "phan_ung", "khong_chac_chan", "chat_luong_anh"],
    "additionalProperties": False,
}


# ================================================================ gọi Claude
def co_khoa_ai() -> bool:
    return bool(os.environ.get("ANTHROPIC_API_KEY") or os.environ.get("ANTHROPIC_AUTH_TOKEN"))


def ten_mo_hinh() -> str:
    return os.environ.get("CLAUDE_MODEL_CO_CHE") or os.environ.get("CLAUDE_MODEL") or "claude-opus-5-5"


_client = None


def _lay_client():
    global _client
    if _client is None:
        try:
            import anthropic
        except ImportError as e:
            raise LoiGiaiDe("Máy chủ chưa cài thư viện anthropic (pip install anthropic).", "thieu_thu_vien", 503) from e
        _client = anthropic.AsyncAnthropic(timeout=CHO_MOI_LAN_GOI, max_retries=1)
    return _client


async def goi_claude(system: str, messages: list, schema: dict) -> str:
    """Một lượt Messages API, trả chữ JSON (structured output theo schema). Lỗi mạng/khóa → LoiGiaiDe."""
    if not co_khoa_ai():
        raise LoiGiaiDe("Máy chủ cơ chế chưa có khóa Claude (ANTHROPIC_API_KEY trong .env ở gốc kho) – chưa giải được đề mới. "
                        "8 bài mẫu vẫn mở được.", "thieu_key", 503)
    try:
        import anthropic
    except ImportError as e:
        raise LoiGiaiDe("Máy chủ chưa cài thư viện anthropic (pip install anthropic).", "thieu_thu_vien", 503) from e
    try:
        r = await _lay_client().beta.messages.create(
            model=ten_mo_hinh(), max_tokens=16000,
            system=[{"type": "text", "text": system, "cache_control": {"type": "ephemeral"}}],
            messages=messages,
            output_config={"effort": os.environ.get("CLAUDE_EFFORT_CO_CHE") or muc_tu_duy(muc_hien_tai.get())["effort"],
                           "format": {"type": "json_schema", "schema": schema}},
        )
    except anthropic.AuthenticationError as e:
        raise LoiGiaiDe("Khóa Claude (ANTHROPIC_API_KEY) không hợp lệ.", "sai_key", 503) from e
    except anthropic.RateLimitError as e:
        raise LoiGiaiDe("Claude đang quá tải hoặc hết hạn mức – thử lại sau ít phút.", "qua_tai", 503) from e
    except anthropic.APITimeoutError as e:
        raise LoiGiaiDe("Claude trả lời quá lâu – thử lại.", "qua_gio", 504) from e
    except anthropic.APIError as e:
        raise LoiGiaiDe(f"Gọi Claude lỗi: {getattr(e, 'message', e)}", "loi_ai", 502) from e
    if r.stop_reason == "refusal":
        raise LoiGiaiDe("Claude từ chối đề này.", "tu_choi", 422)
    if r.stop_reason == "max_tokens":
        raise LoiGiaiDe("Cơ chế quá dài, câu trả lời của Claude bị cắt – hãy tách đề thành từng phản ứng.", "bi_cat", 422)
    return "".join(b.text for b in r.content if b.type == "text").strip()


# ---------------------------------------------------------------- Gemini (REST, không thêm thư viện)
GEMINI_URL = "https://generativelanguage.googleapis.com/v1beta/models/{m}:generateContent"


def co_khoa_gemini() -> bool:
    return bool(os.environ.get("GEMINI_API_KEY") or os.environ.get("GOOGLE_API_KEY"))


GEMINI_TAM_BO_GIAY = 600.0          # mô hình vừa 429/quá giờ/quá tải → bỏ qua 10 phút (khỏi chờ lại ở mỗi lượt sửa)
_gemini_tam_bo: dict[str, float] = {}


def ds_mo_hinh_gemini() -> list[str]:
    ds = [os.environ.get("GEMINI_MODEL_CO_CHE") or "", *muc_tu_duy(muc_hien_tai.get())["gemini"],
          "gemini-pro-latest", "gemini-flash-latest", "gemini-flash-lite-latest"]
    ds = [m for i, m in enumerate(ds) if m and m not in ds[:i]]
    con = [m for m in ds if _gemini_tam_bo.get(m, 0) <= time.time()]
    return con or ds


def _schema_gemini(sc):
    """JSON Schema → bản Gemini nhận được (bỏ additionalProperties)."""
    if isinstance(sc, dict):
        return {k: _schema_gemini(v) for k, v in sc.items() if k != "additionalProperties"}
    if isinstance(sc, list):
        return [_schema_gemini(x) for x in sc]
    return sc


def _noi_dung_gemini(messages: list) -> list[dict]:
    """Tin nhắn kiểu Anthropic (chữ hoặc danh sách khối text/image base64) → contents của Gemini (ảnh = inline_data)."""
    out = []
    for m in messages:
        c = m["content"]
        parts = []
        for b in ([{"type": "text", "text": c}] if isinstance(c, str) else c):
            if b.get("type") == "image":
                parts.append({"inline_data": {"mime_type": b["source"]["media_type"], "data": b["source"]["data"]}})
            else:
                parts.append({"text": b.get("text", "")})
        out.append({"role": "model" if m["role"] == "assistant" else "user", "parts": parts})
    return out


def _ssl_ctx():
    import ssl
    try:
        import certifi
        return ssl.create_default_context(cafile=certifi.where())
    except ImportError:
        return ssl.create_default_context()


def _post_gemini(mo_hinh: str, than: dict, cho: float) -> tuple[int, dict]:
    khoa = os.environ.get("GEMINI_API_KEY") or os.environ.get("GOOGLE_API_KEY") or ""
    req = urllib.request.Request(GEMINI_URL.format(m=mo_hinh), data=json.dumps(than).encode("utf-8"), method="POST",
                                 headers={"Content-Type": "application/json", "x-goog-api-key": khoa})
    try:
        with urllib.request.urlopen(req, timeout=cho, context=_ssl_ctx()) as r:
            return r.status, json.loads(r.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        try:
            return e.code, json.loads(e.read().decode("utf-8"))
        except ValueError:
            return e.code, {}


async def goi_gemini(system: str, messages: list, schema: dict) -> str:
    if not co_khoa_gemini():
        raise LoiGiaiDe("Máy chủ chưa có khóa Gemini (GEMINI_API_KEY).", "thieu_key_gemini", 503)
    than = {"systemInstruction": {"parts": [{"text": system}]}, "contents": _noi_dung_gemini(messages),
            "generationConfig": {"responseMimeType": "application/json", "responseJsonSchema": _schema_gemini(schema),
                                 "maxOutputTokens": 32000, "temperature": 0.2}}
    loi_cuoi = ""
    for mo_hinh in ds_mo_hinh_gemini():
        try:
            ma, d = await asyncio.to_thread(_post_gemini, mo_hinh, than, CHO_GEMINI)
        except (TimeoutError, urllib.error.URLError, OSError) as e:
            if isinstance(e, TimeoutError) or "timed out" in str(e):      # mô hình này treo → thử mô hình sau
                loi_cuoi += f"{mo_hinh}: quá {CHO_GEMINI:.0f} s; "
                _gemini_tam_bo[mo_hinh] = time.time() + GEMINI_TAM_BO_GIAY
                continue
            raise LoiGiaiDe(f"Không gọi được Gemini: {e}", "loi_ai", 502) from e
        thong_bao = ((d.get("error") or {}).get("message") or "")[:200]
        if ma in (401, 403) or (ma == 400 and "API key" in thong_bao):
            raise LoiGiaiDe("Khóa Gemini (GEMINI_API_KEY) không hợp lệ.", "sai_key_gemini", 503)
        if ma in (404, 429, 500, 503):                       # không có mô hình này / hết hạn mức / quá tải → mô hình sau
            loi_cuoi += f"{mo_hinh}: {ma} {thong_bao[:90]}; "
            if ma != 404:
                _gemini_tam_bo[mo_hinh] = time.time() + GEMINI_TAM_BO_GIAY
            continue
        if ma != 200:
            raise LoiGiaiDe(f"Gemini lỗi {ma}: {thong_bao}", "loi_ai", 502)
        ung = (d.get("candidates") or [{}])[0]
        if ung.get("finishReason") == "MAX_TOKENS":
            raise LoiGiaiDe("Cơ chế quá dài, câu trả lời của Gemini bị cắt – hãy tách đề thành từng phản ứng.", "bi_cat", 422)
        chu = "".join(p.get("text", "") for p in (ung.get("content") or {}).get("parts", []) if not p.get("thought"))
        if not chu.strip():
            raise LoiGiaiDe(f"Gemini không trả lời ({ung.get('finishReason') or 'rỗng'}).", "tu_choi", 422)
        _da_dung.append(mo_hinh)
        return chu.strip()
    raise LoiGiaiDe(f"Gemini quá tải hoặc hết hạn mức ({loi_cuoi.rstrip('; ')}) – thử lại sau.", "qua_tai", 503)


# ---------------------------------------------------------------- chọn nhà cung cấp: Claude → Gemini
_da_dung: list[str] = []          # mô hình đã trả lời trong lượt giải hiện tại (ghi vào thong_tin)
_claude_hong = ""                 # Claude bị từ chối khóa ở lần trước → bỏ qua tới khi máy chủ khởi động lại


async def goi_ai_mac_dinh(system: str, messages: list, schema: dict) -> str:
    global _claude_hong
    if not _claude_hong:
        try:
            chu = await goi_claude(system, messages, schema)
            _da_dung.append(ten_mo_hinh())
            return chu
        except LoiGiaiDe as e:
            if e.ma not in ("thieu_key", "sai_key"):
                raise
            _claude_hong = e.thong_diep
    try:
        return await goi_gemini(system, messages, schema)
    except LoiGiaiDe as e:
        if e.ma in ("thieu_key_gemini", "sai_key_gemini"):
            raise LoiGiaiDe(f"Chưa có AI dùng được: Claude – {_claude_hong} Gemini – {e.thong_diep} "
                            "Hãy đặt khóa đúng trong .env ở gốc kho rồi khởi động lại máy chủ. 8 bài mẫu vẫn mở được.",
                            "thieu_key", 503) from e
        raise


def doc_json(chu: str) -> dict:
    """JSON trong câu trả lời (chịu được ```json … ``` và chữ thừa quanh)."""
    t = (chu or "").strip()
    m = re.search(r"```(?:json)?\s*(.*?)```", t, re.S)
    if m:
        t = m.group(1).strip()
    try:
        d = json.loads(t)
    except ValueError:
        a, b = t.find("{"), t.rfind("}")
        if a < 0 or b <= a:
            raise ValueError("không có JSON") from None
        d = json.loads(t[a:b + 1])
    if not isinstance(d, dict):
        raise ValueError("JSON không phải đối tượng")
    return d


class HoiThoai:
    """Hội thoại nhiều lượt với AI (giữ lời trước để AI sửa đúng chỗ)."""

    def __init__(self, system: str, schema: dict, goi_ai: GoiAI):
        self.system, self.schema, self.goi_ai = system, schema, goi_ai
        self.messages: list = []
        self.so_lan_goi = 0

    async def gui(self, noi_dung) -> str:
        self.messages.append({"role": "user", "content": noi_dung})
        tra = await self.goi_ai(self.system, list(self.messages), self.schema)
        self.so_lan_goi += 1
        self.messages.append({"role": "assistant", "content": tra or "{}"})
        return tra


# ================================================================ ảnh đề
_HEIC = (b"heic", b"heix", b"hevc", b"hevx", b"heim", b"heis", b"mif1", b"msf1", b"heif")
_KIEU_ANH = {"JPEG": "image/jpeg", "MPO": "image/jpeg", "PNG": "image/png", "WEBP": "image/webp", "GIF": "image/gif"}


def _heic_sang_jpg(data: bytes) -> bytes:
    if sys.platform != "darwin" or not shutil.which("sips"):
        raise LoiGiaiDe("Máy chủ chưa đọc được ảnh HEIC – hãy chọn ảnh JPG/PNG.", "heic_loi", 415)
    with tempfile.TemporaryDirectory(prefix="co_che_anh_") as d:
        vao, ra = Path(d) / "anh.heic", Path(d) / "anh.jpg"
        vao.write_bytes(data)
        try:
            subprocess.run(["sips", "-s", "format", "jpeg", str(vao), "--out", str(ra)], capture_output=True, timeout=60, check=True)
            return ra.read_bytes()
        except (subprocess.SubprocessError, OSError) as e:
            raise LoiGiaiDe("Không chuyển được ảnh HEIC sang JPG – hãy chọn ảnh JPG/PNG.", "heic_loi", 415) from e


def chuan_bi_anh(data: bytes) -> tuple[str, str]:
    """Kiểm tra ảnh đề → (media_type, base64) để gửi Claude. Lỗi → LoiGiaiDe (400/413/415/422)."""
    if not data:
        raise LoiGiaiDe("Ảnh đề rỗng.", "anh_rong", 400)
    if len(data) > ANH_TOI_DA:
        raise LoiGiaiDe(f"Ảnh quá lớn ({len(data) / 1024 / 1024:.1f} MB) – tối đa {ANH_TOI_DA // 1024 // 1024} MB.", "anh_qua_lon", 413)
    if data[4:8] == b"ftyp" and data[8:12] in _HEIC:
        data = _heic_sang_jpg(data)
    try:
        from PIL import Image, ImageStat, UnidentifiedImageError
    except ImportError:                                    # không có Pillow: chỉ nhận dạng theo chữ ký tệp
        for dau, kieu in ((b"\xff\xd8\xff", "image/jpeg"), (b"\x89PNG", "image/png"), (b"GIF8", "image/gif")):
            if data.startswith(dau):
                return kieu, base64.b64encode(data).decode("ascii")
        raise LoiGiaiDe("Chỉ nhận ảnh JPG, PNG, WEBP, HEIC.", "sai_dinh_dang", 415) from None
    try:
        img = Image.open(io.BytesIO(data))
        img.load()
    except (UnidentifiedImageError, OSError, Image.DecompressionBombError) as e:
        raise LoiGiaiDe("Tệp không phải ảnh hoặc định dạng không hỗ trợ – chỉ nhận JPG, PNG, WEBP, HEIC.", "sai_dinh_dang", 415) from e
    if img.format not in _KIEU_ANH:
        raise LoiGiaiDe(f"Định dạng {img.format} không hỗ trợ – chỉ nhận JPG, PNG, WEBP, HEIC.", "sai_dinh_dang", 415)
    if min(img.size) < CANH_NHO_NHAT:
        raise LoiGiaiDe(f"Ảnh quá nhỏ ({img.size[0]}×{img.size[1]} px) – không đủ nét để đọc công thức, hãy chụp gần hơn.", "anh_qua_nho")
    xam = img.convert("L")
    xam.thumbnail((256, 256))
    if ImageStat.Stat(xam).mean[0] < DO_SANG_TOI_THIEU:
        raise LoiGiaiDe("Ảnh quá tối – hãy chụp lại nơi đủ sáng.", "anh_qua_toi")
    if max(img.size) > CANH_LON_NHAT or img.format not in ("JPEG", "PNG"):
        img = img.convert("RGB")
        img.thumbnail((CANH_LON_NHAT, CANH_LON_NHAT))
        buf = io.BytesIO()
        img.save(buf, "JPEG", quality=90)
        return "image/jpeg", base64.b64encode(buf.getvalue()).decode("ascii")
    return _KIEU_ANH[img.format], base64.b64encode(data).decode("ascii")


def _smiles_sai_trong_anh(kq: dict) -> list[str]:
    sai = []
    for p in kq.get("phan_ung") or []:
        for vai, ds in (("chất đầu", p.get("chat_dau") or []), ("sản phẩm", p.get("san_pham") or [])):
            for i, s in enumerate(ds, 1):
                if (s or "").strip() and Chem.MolFromSmiles(s.strip()) is None:
                    sai.append(f"{p.get('so_cau') or '?'} – {vai} {i}: {s.strip()}")
    return sai


async def doc_anh_de(kieu: str, b64: str, van_ban: str, goi_ai: GoiAI) -> tuple[dict, int]:
    """Claude đọc ảnh đề → (kết quả theo SCHEMA_DOC_ANH, số lần gọi). Ảnh không phải đề Hoá/không đọc được → LoiGiaiDe."""
    ht = HoiThoai(SYSTEM_DOC_ANH, SCHEMA_DOC_ANH, goi_ai)
    yc = "Đọc ảnh đề đính kèm và trả JSON."
    if van_ban.strip():
        yc += f"\nNgười dùng ghi thêm: {van_ban.strip()[:600]}"
    noi_dung = [{"type": "image", "source": {"type": "base64", "media_type": kieu, "data": b64}}, {"type": "text", "text": yc}]
    try:
        kq = doc_json(await ht.gui(noi_dung))
    except ValueError:
        kq = doc_json(await ht.gui("Câu trả lời trước không phải JSON hợp lệ theo schema. Hãy trả lại DUY NHẤT JSON đó."))
    for lan in range(1, SO_LAN_DOC_LAI + 1):
        sai = _smiles_sai_trong_anh(kq)
        if not sai:
            break
        try:
            kq = doc_json(await ht.gui("RDKit KHÔNG đọc được các SMILES sau (sai hoá trị / cú pháp):\n- " + "\n- ".join(sai)
                                       + "\nHãy nhìn lại ảnh, sửa các SMILES đó và trả lại TOÀN BỘ JSON (cùng schema)."))
        except ValueError:
            break
    cl = kq.get("chat_luong_anh") or {}
    pu = [p for p in kq.get("phan_ung") or [] if any((s or "").strip() for s in p.get("chat_dau") or [])]
    if cl.get("van_de") == "khong_phai_de_hoa" or not (pu or (kq.get("de_bai") or "").strip()):
        raise LoiGiaiDe(cl.get("mo_ta") or "Ảnh không phải đề Hoá học (không thấy phản ứng hay công thức nào) – hãy chọn đúng ảnh đề.",
                        "anh_khong_lien_quan")
    if not pu and not (kq.get("de_bai") or "").strip():
        raise LoiGiaiDe((cl.get("mo_ta") or "Không đọc được phản ứng nào trong ảnh").rstrip(".") + " — hãy chụp gần và rõ hơn.",
                        "anh_khong_doc_duoc")
    kq["phan_ung"] = pu
    return kq, ht.so_lan_goi


# ================================================================ AI viết cơ chế + RDKit kiểm + nhờ sửa
def kiem_tra_co_che(d: dict) -> list[str]:
    try:
        return cc_mod.kiem_tra(cc_mod.tu_json(d, "ai"))
    except cc_mod.LoiCoChe as e:
        return [e.thong_diep]
    except (KeyError, TypeError, ValueError, IndexError) as e:
        return [f"Cấu trúc JSON sai: {e}"]


def ten_de_doc(ten: str) -> str:
    """Tên cơ chế cho người đọc: bỏ số map ([CH3:1] → [CH3]) và ngoặc của nguyên tử trung hoà đơn giản ([CH3] → CH3)."""
    ten = re.sub(r":\d+\]", "]", ten)
    return re.sub(r"\[([A-Z][a-z]?(?:H\d?)?)\]", r"\1", ten)


def smiles_co_map(ds: list[str]) -> list[str]:
    """Chất đầu → SMILES có atom map liên tiếp cho MỌI nguyên tử nặng, số H ghi trong ngoặc ([CH3:1]) – gợi ý cho AI viết bước 1
    đúng khung (mô hình nhỏ hay viết sai mạch cacbon / thiếu H khi tự đánh map)."""
    out, so = [], 1
    for x in ds:
        m = Chem.MolFromSmiles(x)
        if m is None:
            continue
        for a in m.GetAtoms():
            if a.GetAtomicNum() > 1:
                a.SetAtomMapNum(so)
                so += 1
        out.append(Chem.MolToSmiles(m, allHsExplicit=True))
    return out


async def sinh_co_che(van_ban: str, chat: list[str], san_pham: list[str], dieu_kien: str, goi_y_loai: str,
                      goi_ai: GoiAI) -> tuple[dict, list[str], int]:
    """AI viết cơ chế; RDKit kiểm; sai thì gửi lỗi cụ thể cho AI sửa (tối đa SO_LAN_SUA lần).
    → (cơ chế cuối, lỗi còn lại [rỗng = đạt], số lần đã nhờ sửa)."""
    yc = f"Phản ứng / đề bài: {van_ban}"
    if chat:
        yc += f"\nChất tham gia (SMILES): {', '.join(chat)}"
        co_map = smiles_co_map(chat)
        if co_map:
            yc += f"\n\"chat\" của bước 1 nên dùng đúng các SMILES đã đánh map này (giữ map, số H): {', '.join(co_map)}"
    if san_pham:
        yc += f"\nSản phẩm (SMILES): {', '.join(san_pham)}"
    if dieu_kien:
        yc += f"\nĐiều kiện: {dieu_kien}"
    if goi_y_loai:
        yc += f"\nGợi ý về kiểu cơ chế: {LOAI_CO_CHE.get(goi_y_loai, goi_y_loai)}"
    ht = HoiThoai(SYSTEM_CO_CHE, SCHEMA_CO_CHE, goi_ai)
    tra = await ht.gui(yc)
    cc: dict | None = None
    loi: list[str] = []
    so_lan_sua = 0
    for lan in range(SO_LAN_SUA + 1):
        try:
            cc = doc_json(tra)
            loi = kiem_tra_co_che(cc)
        except ValueError as e:
            loi = [f"JSON không đúng định dạng: {e}"]
        if not loi or lan == SO_LAN_SUA:
            break
        so_lan_sua += 1
        tra = await ht.gui("Cơ chế có lỗi khi mô phỏng bằng RDKit, hãy sửa và trả lại TOÀN BỘ JSON:\n- " + "\n- ".join(loi[:15]))
    if cc is None:
        raise LoiGiaiDe("AI chưa viết được cơ chế đúng định dạng.", "dinh_dang", 422, loi[:5])
    return cc, loi, so_lan_sua


# ---------------------------------------------------------------- tách SMILES gõ thẳng (từ CHEMISTRY)
_SMILES_RO = re.compile(r"[\[\]=#@()\\/]|^[BCNOSPFIcnosp][a-zA-Z0-9]*$")
_KHAN_GIA = {"[Na+]", "[K+]", "[Li+]", "[Cs+]", "[Ca+2]", "[Mg+2]"}
_KIEM = {"Li", "Na", "K", "Cs"}


def doc_smiles_truc_tiep(van_ban: str) -> tuple[list[str], list[str]] | None:
    """'CCBr.[OH-]>>CCO.[Br-]' hoặc 'CCBr + [OH-] -> CCO + [Br-]' toàn SMILES → (chất, sản phẩm); không phải SMILES → None."""
    t = van_ban.strip()
    if not t:
        return None
    if ">>" in t:
        trai, phai = t.split(">>", 1)
    else:
        phan = re.split(r"\s*(?:→|->|=>|⟶)\s*", t, maxsplit=1)
        trai, phai = phan[0], (phan[1] if len(phan) > 1 else "")

    def tach(s):
        return [x.strip() for x in re.split(r"\s+\+\s+|\.", s.strip()) if x.strip()] if s.strip() else []

    chat, sp = tach(trai), tach(phai)
    for x in chat + sp:
        if " " in x or Chem.MolFromSmiles(x) is None or not (_SMILES_RO.search(x) or not re.search(r"\d", x)):
            return None
    return (chat, sp) if chat else None


def lam_sach(ds: list[str]) -> list[str]:
    """Tách SMILES nhiều mảnh, bỏ ion khán giả, NaOH/KOH cộng hoá trị → [OH-]; bỏ SMILES sai/trùng."""
    out: list[str] = []
    for s in ds:
        for x in (s or "").split("."):
            x = x.strip()
            m = Chem.MolFromSmiles(x) if x else None
            if m is None:
                continue
            chuan = Chem.MolToSmiles(m)
            if chuan in _KHAN_GIA:
                continue
            if m.GetNumAtoms() == 2 and {a.GetSymbol() for a in m.GetAtoms()} & _KIEM and any(a.GetSymbol() == "O" for a in m.GetAtoms()):
                chuan = "[OH-]"
            if chuan not in out:
                out.append(chuan)
    return out


# ================================================================ dựng 3D + giấy, gói kết quả
def dung_ket_qua(co_che: dict, de: str, ma: str) -> dict:
    """Cơ chế đã qua kiểm tra → khung gói bài như .verapack (bai, kich_ban_3d, so_do_giay), JSON thường."""
    kb = d3.dung(co_che)
    sd = ve_theo_mau.tao_so_do(co_che)
    bai = {"ma": ma, "de": de, "lop": 11, "loai": co_che.get("loai", ""), "dieu_kien": co_che.get("dieu_kien", ""),
           "muc_tieu": [], "cau_hoi": [], "loi_hay_gap": [],
           "cac_buoc": [{"ten": (b.get("nhan_trung_gian") and f"Bước {k} → {b['nhan_trung_gian']}") or f"Bước {k}",
                         "giai_thich": b.get("ghi_chu", "")} for k, b in enumerate(co_che["buoc"], 1)]}
    return {"phien_ban": 1, "ma": ma, "bai": bai, "kich_ban_3d": kb, "so_do_giay": sd}


_CACHE: "OrderedDict[str, dict]" = OrderedDict()
_CACHE_TOI_DA = 32


def _khoa_cache(van_ban: str, anh: bytes | None, loai: str) -> str:
    h = hashlib.sha256()
    h.update(van_ban.strip().encode("utf-8"))
    h.update(b"\0" + (anh or b"") + b"\0" + loai.encode())
    return h.hexdigest()


async def giai_de(van_ban: str = "", anh: bytes | None = None, loai: str = "", goi_ai: GoiAI | None = None,
                  dung_cache: bool = True) -> dict:
    """Điểm vào: đề chữ và/hoặc ảnh đề (+ gợi ý loại cơ chế, không bắt buộc) → bài cơ chế mở được như 8 bài mẫu."""
    van_ban = (van_ban or "").strip()[:VAN_BAN_TOI_DA]
    loai = (loai or "").strip().upper()
    loai = loai if loai in LOAI_CO_CHE else ""
    if not van_ban and not anh:
        raise LoiGiaiDe("Hãy gõ đề hoặc chọn ảnh đề.", "thieu_de", 400)
    khoa = _khoa_cache(van_ban, anh, loai)
    if dung_cache and goi_ai is None and khoa in _CACHE:
        kq = json.loads(json.dumps(_CACHE[khoa]))
        kq["thong_tin"]["tu_bo_nho_dem"] = True
        return kq
    goi = goi_ai or goi_ai_mac_dinh
    _da_dung.clear()
    t0 = time.time()
    so_lan_goi = 0
    doc_anh: dict | None = None
    chat: list[str] = []
    san_pham: list[str] = []
    dieu_kien, de = "", van_ban

    if anh:
        kieu, b64 = chuan_bi_anh(anh)
        doc_anh, n = await doc_anh_de(kieu, b64, van_ban, goi)
        so_lan_goi += n
        pu = (doc_anh.get("phan_ung") or [{}])[0]
        chat = lam_sach(pu.get("chat_dau") or [])
        san_pham = lam_sach([s for s in pu.get("san_pham") or [] if (s or "").strip()])
        dieu_kien = pu.get("tac_nhan_dieu_kien") or ""
        loai = loai or (pu.get("goi_y_loai") or "")
        de = (doc_anh.get("de_bai") or "").strip() or van_ban
        if van_ban:
            de = f"{de}\n(Ghi chú: {van_ban})" if de != van_ban else de
    else:
        truc_tiep = doc_smiles_truc_tiep(van_ban)
        if truc_tiep:
            chat, san_pham = lam_sach(truc_tiep[0]), lam_sach(truc_tiep[1])

    yeu_cau = de if not doc_anh else (de + (f"\nPhản ứng cần vẽ: {pu.get('so_cau', '')} – {pu.get('yeu_cau', '')}" if pu else ""))
    co_che, loi, so_lan_sua = await sinh_co_che(yeu_cau, chat, san_pham, dieu_kien, loai, goi)
    so_lan_goi += 1 + so_lan_sua
    if loi:
        raise LoiGiaiDe(f"AI đã sửa {so_lan_sua} lần nhưng cơ chế vẫn chưa nhất quán khi mô phỏng bằng RDKit – chưa dựng 3D. "
                        "Hãy ghi rõ chất tham gia/sản phẩm hoặc thử lại.", "co_che_sai", 422, loi[:8])
    co_che["ten"] = ten_de_doc(str(co_che.get("ten") or "Cơ chế"))
    ma = "de_" + khoa[:10]
    try:
        kq = await asyncio.to_thread(dung_ket_qua, co_che, de, ma)
    except Exception as e:  # noqa: BLE001 – dựng 3D/giấy hỏng không làm sập máy chủ
        raise LoiGiaiDe(f"Cơ chế đúng nhưng chưa dựng được hình 3D/sơ đồ giấy: {e}", "dung_hinh", 422) from e
    kq["co_che"] = co_che
    kq["doc_anh"] = doc_anh
    kq["thong_tin"] = {"mo_hinh": ", ".join(dict.fromkeys(_da_dung)) if goi_ai is None else "gia_lap", "so_lan_goi_ai": so_lan_goi,
                       "so_lan_sua": so_lan_sua, "kiem_tra": "đạt", "giay": round(time.time() - t0, 1), "tu_bo_nho_dem": False}
    if goi_ai is None:
        _CACHE[khoa] = kq
        while len(_CACHE) > _CACHE_TOI_DA:
            _CACHE.popitem(last=False)
    return kq
