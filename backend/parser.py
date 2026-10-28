"""Bước 1 – Phân tích đề: tách chuỗi thành các bước (cục bộ + nhờ AI, đọc được cả ảnh đề)."""
from __future__ import annotations

import base64
import io
import re
from typing import Callable

from PIL import Image
from pydantic import ValidationError

from .llm import LoiAI, PhienHoiThoai, doc_json
from .schemas import SCHEMA_PHAN_TICH, PhanTichDe

# Các kiểu mũi tên người dùng hay gõ; có thể kèm tác nhân/điều kiện:
#   →[HBr]   →[NaOH, t°]   -(CuO, t°)->   -[HBr]->   --HBr-->   và số thứ tự bước (1)
_MUI_TEN = r"(?:-{1,2}>|={1,2}>|→|⟶|➝|➔|⇒|⟹|⇌|⇄|─>|—>|–>)"
_ARROW = re.compile(
    r"\s*(?:"
    r"(?<![^\s])-{1,3}\s*(?:\([^()]*\)|\[[^\]]*\])\s*-{1,3}>"   # -(tác nhân)->  -[tác nhân]->
    r"|(?<![^\s])-{2,3}(?=[^\s>-])[^<>→\n]*?[^\s-]-{2,3}>"        # --tác nhân-->
    rf"|{_MUI_TEN}(?:\s*\[[^\]]*\])?"                            # → hoặc →[tác nhân]
    r")\s*(?:\(\s*\d+\s*\))?\s*"
)

SYSTEM_PHAN_TICH = """Bạn là giáo viên Hoá học THPT ở Việt Nam. Nhiệm vụ: đọc đề "chuỗi phản ứng hữu cơ" (văn bản hoặc ảnh) và tách cấu trúc, CHƯA giải.
- de_bai: chép lại nguyên văn đề (nếu là ảnh thì chép chữ trong ảnh; giữ mũi tên →; tác nhân trên mũi tên ghi dạng →[tác nhân]).
- cac_chat: mỗi chất xuất hiện trong chuỗi đúng 1 lần, theo thứ tự xuất hiện; ki_hieu giữ đúng như đề viết (vd "CH3CHO", "X", "(C6H10O5)n") nhưng KHÔNG kèm CTPT trong ngoặc; la_chat_an=true nếu là kí hiệu chưa biết (X, Y, A, B...); ten_goi_y là tên tiếng Việt theo SGK nếu đã biết (để trống nếu là chất ẩn); ten_tieng_anh là tên tiếng Anh để tra PubChem (để trống nếu chưa biết).
- ctpt_de_cho: công thức phân tử đề cho kèm chất ẩn, vd "C (C3H6O)" hoặc "C: C3H6O" → ki_hieu "C", ctpt_de_cho "C3H6O"; đề không cho thì để trống.
- cac_buoc: mỗi mũi tên là một bước, đánh số từ 1; chat_dau/chat_sau dùng đúng ki_hieu ở cac_chat; dieu_kien_de_cho chép đúng tác nhân và điều kiện đề ghi trên/dưới mũi tên (vd "HBr", "NaOH, t°", "+ H2O, H+", "AgNO3/NH3, t°"), đề không ghi thì để trống.
- Cách gõ tác nhân trên mũi tên trong văn bản: "→[HBr]", "-(HBr)->", "--HBr-->", hoặc "A + HBr → B". Tác nhân (HBr, NaOH, CuO, Br2, H2...) KHÔNG phải là một chất của chuỗi, trừ khi đề dùng kí hiệu riêng cho nó.
- Chuỗi có nhánh thì mỗi mũi tên vẫn là một bước riêng.
- ghi_chu: ghi những điểm mơ hồ của đề. Nếu nội dung không phải chuỗi phản ứng hoá học thì để cac_buoc rỗng và giải thích trong ghi_chu."""


def chuan_hoa(text: str) -> str:
    """Chuẩn hoá chữ số chỉ số dưới Unicode (CH₃ → CH3) và khoảng trắng."""
    sub = str.maketrans("₀₁₂₃₄₅₆₇₈₉", "0123456789")
    return re.sub(r"[ \t]+", " ", text.translate(sub)).strip()


def tach_chuoi(text: str) -> list[str]:
    """Tách chuỗi đơn giản theo mũi tên (dùng để kiểm tra nhanh ô nhập trước khi gọi AI)."""
    t = chuan_hoa(text)
    parts = [p.strip(" ,;.") for p in _ARROW.split(t)]
    return [p for p in parts if p]


def tach_tac_nhan(text: str) -> list[str]:
    """Tác nhân/điều kiện ghi trên từng mũi tên, theo thứ tự (chuỗi rỗng nếu mũi tên không ghi gì)."""
    out = []
    for m in _ARROW.finditer(chuan_hoa(text)):
        x = re.sub(r"\(\s*\d+\s*\)\s*$", "", m.group().strip())  # bỏ số thứ tự bước "(1)"
        x = re.sub(r"-*" + _MUI_TEN + r"$", "", x.strip())
        x = re.sub(r"^(?:" + _MUI_TEN + r"|-+)", "", x.strip())
        out.append(x.strip(" -[]()"))
    return out


def chuan_bi_anh(data: bytes, dinh_dang: str = "PNG") -> tuple[str, str]:
    """Thu nhỏ ảnh đề (cạnh dài ≤ 1600px) và trả về (media_type, base64). dinh_dang "JPEG": ảnh chụp (nhẹ hơn PNG nhiều)."""
    try:
        img = Image.open(io.BytesIO(data))
        img.load()
    except Exception as e:  # noqa: BLE001
        raise LoiAI("Không đọc được ảnh đề. Hãy dùng ảnh PNG hoặc JPG.") from e
    if img.mode not in ("RGB", "L"):
        bg = Image.new("RGB", img.size, "white")
        bg.paste(img, mask=img.convert("RGBA").split()[-1])
        img = bg
    img.thumbnail((1600, 1600))
    buf = io.BytesIO()
    if dinh_dang == "JPEG":
        img.convert("RGB").save(buf, "JPEG", quality=90, optimize=True)
        return "image/jpeg", base64.standard_b64encode(buf.getvalue()).decode()
    img.convert("RGB").save(buf, "PNG", optimize=True)
    return "image/png", base64.standard_b64encode(buf.getvalue()).decode()


def phan_tich_de(text: str, anh: bytes | None = None, bao: Callable[[str], None] | None = None) -> PhanTichDe:
    yeu_cau = "Phân tích đề chuỗi phản ứng sau."
    if text.strip():
        yeu_cau += f"\n\nĐề (văn bản):\n{chuan_hoa(text)}"
    anh_b64 = None
    if anh:
        anh_b64 = chuan_bi_anh(anh)
        yeu_cau += "\n\nĐề nằm trong ảnh đính kèm" + (" (văn bản ở trên là phần bổ sung)." if text.strip() else ".")

    # Đề chữ: tách cấu trúc là việc nhẹ → bậc lite; đề ảnh cần đọc chữ/công thức trong ảnh → bậc flash.
    # JSON hỏng → thử lại một lần ở bậc cao hơn.
    cac_bac = ["flash", "pro"] if anh_b64 else ["lite", "flash"]
    for i, bac in enumerate(cac_bac):
        phien = PhienHoiThoai(SYSTEM_PHAN_TICH, output_schema=SCHEMA_PHAN_TICH, effort="low", max_tokens=8000, bao=bao, bac=bac)
        ph = phien.gui(text=yeu_cau, anh=anh_b64)
        try:
            kq = PhanTichDe.model_validate(doc_json(ph.text))
            break
        except (ValueError, ValidationError) as e:
            if i == len(cac_bac) - 1:
                raise LoiAI("AI trả về kết quả phân tích đề không đúng định dạng. Vui lòng thử lại.") from e
            if bao:
                bao(f"Kết quả đọc đề không đúng định dạng → đọc lại bằng mô hình cao hơn ({cac_bac[i + 1]}).")
    if not kq.cac_buoc:
        raise LoiAI("Không nhận ra chuỗi phản ứng trong đề. " + (kq.ghi_chu or "Hãy nhập lại, ví dụ: CH4 → C2H2 → CH3CHO"))
    return kq
