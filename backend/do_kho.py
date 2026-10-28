"""Phân loại độ khó đề TRƯỚC khi giải, bằng mô hình RẺ NHẤT (bậc lite = Gemini Flash-Lite), để chọn mô hình giải:
Dễ → Flash-Lite · Trung bình → Flash · HSG → Pro (hoặc Claude nếu tài khoản có mã Claude hợp lệ và bật "Dùng Claude").
Đã mở Claude bằng Key Claude (hoặc admin) + bật "Dùng Claude" → giải bằng Claude ở mọi mức.
Đề có ảnh: phân loại SAU bước đọc ảnh (dùng đề đã chép ra chữ). Người dùng được nâng mức thủ công (mức tối thiểu).
"""
from __future__ import annotations

import json
import logging
from typing import Callable

from pydantic import BaseModel, ValidationError

from .llm import LoiAI, PhienHoiThoai, doc_json

log = logging.getLogger(__name__)

MUC_DO = ("de", "trung_binh", "hsg")
TEN_MUC = {"de": "Dễ", "trung_binh": "Trung bình", "hsg": "HSG"}
BAC_THEO_MUC = {"de": "lite", "trung_binh": "flash", "hsg": "pro"}
THU_TU_BAC = ("lite", "flash", "pro", "claude")

SYSTEM_PHAN_LOAI = """Bạn phân loại ĐỘ KHÓ của một đề Hoá hữu cơ (chương trình THPT Việt Nam) để chọn mô hình AI giải phù hợp. KHÔNG giải đề.
Trả JSON {"muc_do": "de" | "trung_binh" | "hsg", "ly_do": "..."} – ly_do 1 câu ngắn tiếng Việt.
- de: chuỗi phản ứng trong SGK (các chất đã cho tên/công thức rõ, phản ứng cơ bản: cộng, thế, tách, oxi hoá, ester hoá, thuỷ phân…),
  gọi tên, viết phương trình cơ bản.
- trung_binh: bài tập tổng hợp, có chất ẩn X, Y cần biện luận công thức cấu tạo thông thường, cơ chế phản ứng cơ bản (SN1/SN2/E1/E2,
  cộng electrophile), dùng CTPT để suy luận đơn giản.
- hsg: đề học sinh giỏi tỉnh/quốc gia: cơ chế nhiều bước, chuyển vị (pinacol, Wagner–Meerwein, Beckmann…), lập thể phức tạp (R/S,
  E/Z, cộng anti/syn, endo/exo), phản ứng có tên nâng cao (Grignard, aldol/Claisen, Diels–Alder, Wittig, ozon phân…), tổng hợp hữu cơ
  nhiều giai đoạn, nhiều chất ẩn phải tính độ bất bão hoà và lập luận xuôi ngược.
Đang phân vân giữa hai mức thì chọn mức CAO hơn."""

SCHEMA_PHAN_LOAI = {
    "type": "object",
    "properties": {"muc_do": {"type": "string", "enum": list(MUC_DO)}, "ly_do": {"type": "string"}},
    "required": ["muc_do", "ly_do"],
    "additionalProperties": False,
}


class _PhanLoai(BaseModel):
    muc_do: str
    ly_do: str = ""


def cao_hon(a: str, b: str) -> str:
    """Mức cao hơn trong hai mức ("" = không có)."""
    ds = [m for m in (a, b) if m in MUC_DO]
    return max(ds, key=MUC_DO.index) if ds else ""


def bac_tiep(bac: str, co_claude: bool) -> str | None:
    """Bậc cao hơn một bậc (lite → flash → pro → claude); chỉ lên Claude khi có quyền Claude. None = đã cao nhất."""
    i = THU_TU_BAC.index(bac) if bac in THU_TU_BAC else 1
    if i + 1 >= len(THU_TU_BAC):
        return None
    tiep = THU_TU_BAC[i + 1]
    return None if tiep == "claude" and not co_claude else tiep


def bac_giai(muc_do: str, co_claude: bool, moi_muc: bool = False) -> str:
    """Bậc mô hình để GIẢI theo mức độ: có quyền Claude + (hsg, hoặc quyền dùng cho mọi mức – Key Claude / admin) → claude."""
    return "claude" if co_claude and (muc_do == "hsg" or moi_muc) else BAC_THEO_MUC.get(muc_do, "flash")


def phan_loai(noi_dung: str, bao: Callable[[str], None] | None = None, anh: tuple[str, str] | None = None) -> dict:
    """→ {"muc_do", "ly_do", "loi": bool}. Lỗi (mạng, JSON hỏng...) → mức "trung_binh" (an toàn, không chặn bài giải)."""
    try:
        phien = PhienHoiThoai(SYSTEM_PHAN_LOAI, output_schema=SCHEMA_PHAN_LOAI, effort="low", max_tokens=1200, bao=bao, bac="lite")
        ph = phien.gui(text=f"Đề cần phân loại:\n{noi_dung[:6000]}" + ("\n(Đề nằm trong ảnh đính kèm.)" if anh else ""), anh=anh)
        kq = _PhanLoai.model_validate(doc_json(ph.text))
        if kq.muc_do not in MUC_DO:
            raise ValueError(f"mức lạ: {kq.muc_do}")
        return {"muc_do": kq.muc_do, "ly_do": kq.ly_do.strip()[:300], "loi": False}
    except (LoiAI, ValueError, ValidationError) as e:
        log.warning("Không phân loại được độ khó: %s", e)
        return {"muc_do": "trung_binh", "ly_do": "Không phân loại tự động được – dùng mức Trung bình.", "loi": True}


def mo_ta_de(de) -> str:
    """Đề đã phân tích (PhanTichDe) → văn bản gọn để phân loại (đề gốc + cấu trúc chuỗi)."""
    cau_truc = {
        "so_buoc": len(de.cac_buoc),
        "chat_an": [c.ki_hieu + (f" ({c.ctpt_de_cho})" if c.ctpt_de_cho else "") for c in de.cac_chat if c.la_chat_an],
        "tac_nhan": [b.dieu_kien_de_cho for b in de.cac_buoc if b.dieu_kien_de_cho],
    }
    return f"{de.de_bai}\n\nCấu trúc: {json.dumps(cau_truc, ensure_ascii=False)}" + (f"\nGhi chú: {de.ghi_chu}" if de.ghi_chu else "")
