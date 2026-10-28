"""Bước 3 – Giải chuỗi: Claude trả JSON theo schema (structured outputs), kiểm tra bằng pydantic."""
from __future__ import annotations

import json
import logging
from typing import Callable

from pydantic import ValidationError

from .config import settings
from .llm import LoiAI, PhienHoiThoai, doc_json, mo_ta_mo_hinh
from .research import KetQuaTraCuu
from .schemas import SCHEMA_LOI_GIAI, LoiGiai, PhanTichDe

log = logging.getLogger(__name__)

SYSTEM_GIAI = """Bạn là giáo viên Hoá giỏi ở Việt Nam. Giải chuỗi theo chương trình THPT, gọi tên theo SGK Việt Nam, ghi rõ điều kiện. Ưu tiên dữ liệu tra cứu. Nhiều cách thì chọn cách phổ biến trong SGK, nêu cách khác trong ghi_chu.

Lời giải được kiểm tra tự động bằng RDKit (SMILES hợp lệ, cân bằng nguyên tố và điện tích từng phương trình), vì vậy hãy tuân thủ:
- so_do: mọi chất của chuỗi, mỗi kí hiệu một lần theo thứ tự xuất hiện; ki_hieu giữ đúng như đề viết (vd "CH3CHO", "X", "(C6H10O5)n"); cong_thuc là công thức cấu tạo thu gọn kiểu SGK (vd CH3COOC2H5); ten là tên chất.
- cac_buoc: mỗi mũi tên là một bước, so bắt đầu từ 1; chat_dau/chat_sau là ki_hieu trong so_do. Chất đầu phải nằm trong chat_tham_gia, chất sau phải nằm trong san_pham với cùng SMILES như trong so_do.
- phuong_trinh: phương trình đã cân bằng, công thức thu gọn kiểu SGK, hệ số đứng trước, mũi tên "→" (phản ứng thuận nghịch dùng "⇌"); không ghi điều kiện trong phuong_trinh.
- chat_tham_gia/san_pham: liệt kê đủ mọi chất trong phương trình, kể cả chất vô cơ (H2O, H2, O2, CO2...), với he_so đúng như phương trình. Tác nhân có tham gia phản ứng (HBr, NaOH, CuO, Br2, AgNO3 + NH3...) phải có trong chat_tham_gia; chỉ chất xúc tác (Ni, Pt, H2SO4 đặc, H+, HgSO4...) thì không đưa vào.
- SMILES chuẩn, ưu tiên lấy từ PubChem. H2 là [H][H]; O2 là O=O; H2O là O; hợp chất ion viết dạng ion (NaOH là [Na+].[OH-]; CH3COONa là CC(=O)[O-].[Na+]); kim loại viết [Ag], [Cu]; hiđro nguyên tử là [H] (vd khử bằng Fe/HCl dùng 6[H]).
- Polime (tinh bột, xenlulozơ, PE...): SMILES của một mắt xích với hai đầu nối là * (vd tinh bột: *OC1C(CO)OC(*)C(O)C1O), he_so tính theo một mắt xích; phuong_trinh vẫn ghi dạng (C6H10O5)n + nH2O → nC6H12O6.
- dieu_kien: ngắn gọn như ghi trên mũi tên (vd "1500°C, làm lạnh nhanh", "H2SO4 đặc, t°", "Ni, t°"); dùng "t°" cho đun nóng. Nếu đề đã ghi tác nhân/điều kiện trên mũi tên (dieu_kien_de_cho) thì giữ nguyên như đề, chỉ bổ sung điều kiện còn thiếu.
- loai_phan_ung: vd "nhiệt phân", "cộng nước (hydrate hoá)", "cộng hydrogen", "oxi hoá", "ester hoá", "thế".
- giai_thich: 1–3 câu ngắn cho học sinh.
- chat_an: mỗi kí hiệu chưa biết trong đề (X, Y, A...) kèm ten, smiles, cong_thuc; đề không có chất ẩn thì để mảng rỗng.
- Dạng đề cho chất đầu + tác nhân trên mũi tên, hỏi cấu tạo sản phẩm: suy ra cấu tạo sản phẩm CHÍNH từ cấu tạo chất trước và tác nhân, theo quy tắc SGK (Markovnikov khi cộng HX/H2O vào alkene bất đối xứng; Zaitsev khi tách HX/H2O; định hướng thế ở vòng benzen; alcohol bậc I → aldehyde, bậc II → ketone khi oxi hoá bằng CuO; thế halogen bằng OH khi đun với NaOH loãng; tách HX khi đun với KOH/ethanol...). Ghi tên sản phẩm phụ (nếu có) trong giai_thich. cong_thuc của chất ẩn phải là công thức cấu tạo thu gọn thể hiện rõ vị trí nhóm chức (vd CH3–CHBr–CH3, CH3–CO–CH3), không viết CTPT.
- Nếu đề cho CTPT của chất (ctpt_de_cho) thì cấu tạo phải có đúng CTPT đó; RDKit sẽ so CTPT của SMILES với đề.
- nguon_tham_khao: các URL đã dùng trong dữ liệu tra cứu (có thể thêm "SGK Hoá học 11").
- Tên chất: theo SGK Hoá học hiện hành (Chương trình GDPT 2018, danh pháp IUPAC: methane, acetylene, acetaldehyde, ethanol, acetic acid, ethyl acetate...). Nếu đề dùng tên kiểu cũ thì ghi kèm tên đó trong ngoặc."""


YEU_CAU_HSG = """ĐỀ MỨC HỌC SINH GIỎI (tỉnh/quốc gia) – suy luận kĩ trước khi trả lời:
- Với mỗi chất ẩn có CTPT: tính độ bất bão hoà k = (2C + 2 + N − H − X)/2, kết hợp tác nhân của bước trước/sau và chất cuối (nếu đề cho) để lập luận xuôi và ngược; kiểm tra lại số C qua từng bước (bước nào tăng/giảm mạch C).
- Vận dụng phản ứng nâng cao khi phù hợp: Grignard (RMgX + carbonyl/CO2/epoxide/ester rồi H3O+), aldol/croton hoá, Claisen, Cannizzaro, Diels–Alder, ozon phân (O3 rồi Zn/H2O hoặc H2O2), oxi hoá KMnO4 (loãng lạnh → diol; đặc nóng → cắt mạch), HIO4, chuyển vị pinacol, Hofmann, Wittig, Friedel–Crafts, khử Clemmensen/Wolff–Kishner, LiAlH4/NaBH4, haloform, decarboxyl hoá, nitrile → acid/amine, ester hoá/thuỷ phân/xà phòng hoá.
- Tác nhân nhiều giai đoạn trên một mũi tên (vd "1) O3; 2) Zn/H2O") là MỘT bước: viết phương trình tổng đã cân bằng.
- Khi lập thể quyết định đáp án (cộng anti Br2, cis/trans, R/S, endo/exo) thì ghi lập thể trong SMILES (@, @@, /, \\) và nêu trong giai_thich.
- giai_thich: 2–4 câu nêu quy tắc/cơ chế dẫn tới cấu tạo đã chọn; cách hiểu khác của đề ghi trong ghi_chu."""


def _tao_yeu_cau(de: PhanTichDe, tra_cuu: KetQuaTraCuu, hsg: bool = False, loi_truoc: list[str] | None = None) -> str:
    cau_truc = {
        "cac_chat": [c.model_dump() for c in de.cac_chat],
        "cac_buoc": [b.model_dump() for b in de.cac_buoc],
    }
    return (
        f"ĐỀ BÀI:\n{de.de_bai}\n\n"
        f"CẤU TRÚC CHUỖI ĐÃ PHÂN TÍCH:\n{json.dumps(cau_truc, ensure_ascii=False)}\n\n"
        f"DỮ LIỆU TRA CỨU:\n{tra_cuu.tom_tat()}\n\n"
        + (f"{YEU_CAU_HSG}\n\n" if hsg else "")
        + ("LỜI GIẢI TRƯỚC (của mô hình khác) BỊ KIỂM TRA TỰ ĐỘNG BẰNG RDKit PHÁT HIỆN CÁC LỖI SAU – tránh lặp lại:\n"
           + "\n".join(f"- {x}" for x in loi_truoc) + "\n\n" if loi_truoc else "")
        + "Hãy giải chuỗi và trả về JSON đúng schema."
    )


def _gui_va_doc(phien: PhienHoiThoai, noi_dung: str) -> tuple[LoiGiai, str]:
    """Gửi một lượt; nếu JSON sai schema thì yêu cầu trả lại đúng 1 lần (lịch sử chỉ nối thêm).

    Trả về (lời giải, tên mô hình đã trả lời).
    """
    for lan in range(2):
        ph = phien.gui(text=noi_dung)
        try:
            if ph.bi_cat:
                raise ValueError("câu trả lời bị cắt do vượt giới hạn độ dài")
            return LoiGiai.model_validate(doc_json(ph.text)), mo_ta_mo_hinh(ph)
        except (ValueError, ValidationError) as e:
            log.warning("Lời giải sai định dạng (lần %d): %s", lan + 1, e)
            if lan == 1:
                raise LoiAI("AI trả về lời giải không đúng định dạng sau 2 lần thử. Vui lòng bấm Giải lại.", "dinh_dang") from e
            noi_dung = f"JSON vừa rồi không hợp lệ ({str(e)[:500]}). Hãy trả lại TOÀN BỘ lời giải đúng schema."
    raise AssertionError("unreachable")


def tao_phien(bao: Callable[[str], None] | None = None, hsg: bool = False) -> PhienHoiThoai:
    """Mô hình theo bậc của kế hoạch (pipeline đặt theo độ khó: lite / flash / pro / claude)."""
    if hsg:  # đề khó: suy nghĩ sâu hơn
        effort = settings.effort if settings.effort in ("xhigh", "max") else "high"
        return PhienHoiThoai(SYSTEM_GIAI, output_schema=SCHEMA_LOI_GIAI, effort=effort, max_tokens=64000, bao=bao)
    return PhienHoiThoai(SYSTEM_GIAI, output_schema=SCHEMA_LOI_GIAI, effort=settings.effort, max_tokens=32000, bao=bao)


def giai(de: PhanTichDe, tra_cuu: KetQuaTraCuu, bao: Callable[[str], None] | None = None,
         hsg: bool = False, loi_truoc: list[str] | None = None) -> tuple[LoiGiai, PhienHoiThoai, str]:
    """Trả về (lời giải, phiên hội thoại để dùng tiếp cho vòng sửa lỗi, tên mô hình).
    loi_truoc: lỗi RDKit của lời giải trước (khi giải lại bằng mô hình cao hơn)."""
    phien = tao_phien(bao, hsg)
    sol, mo_hinh = _gui_va_doc(phien, _tao_yeu_cau(de, tra_cuu, hsg, loi_truoc))
    return sol, phien, mo_hinh


def sua_loi(phien: PhienHoiThoai, loi: list[str]) -> tuple[LoiGiai, str]:
    danh_sach = "\n".join(f"- {x}" for x in loi)
    noi_dung = (
        "Kiểm tra tự động bằng RDKit phát hiện các lỗi sau:\n"
        f"{danh_sach}\n\n"
        "Hãy sửa (SMILES, hệ số, danh sách chất tham gia/sản phẩm hoặc phương trình) và trả lại TOÀN BỘ lời giải đúng schema."
    )
    return _gui_va_doc(phien, noi_dung)
