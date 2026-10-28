"""Kiểu dùng chung cho mọi nhà cung cấp AI (Claude, Gemini, ChatGPT).

AIProvider: giao diện chung – tính năng (Giải chuỗi, Mô hình 3D, Cơ chế) chỉ dùng llm.PhienHoiThoai, phiên gọi AI qua giao diện này
nên không cần biết đang gọi AI nào. `phien` là llm.PhienHoiThoai (lịch sử trung lập `_lich_su`, system, schema, ảnh, streaming…).
"""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Protocol, runtime_checkable


class LoiAI(Exception):
    """Lỗi khi gọi AI; thông điệp là tiếng Việt để hiện cho người dùng. `loai` dùng để quyết định chuyển nhà cung cấp:
    thieu_key | key | quyen | model | het_tien | gioi_han | qua_tai | mang | timeout | yeu_cau | tu_choi | khac."""

    def __init__(self, thong_diep: str, loai: str = "khac"):
        super().__init__(thong_diep)
        self.loai = loai


@dataclass
class GoiCongCu:
    id: str
    ten: str
    tham_so: dict


@dataclass
class PhanHoi:
    text: str
    cong_cu: list[GoiCongCu] = field(default_factory=list)
    nha_cung_cap: str = "claude"
    model: str = ""
    bi_cat: bool = False  # bị cắt do vượt max_tokens
    tokens_in: int = 0
    tokens_out: int = 0    # đầu ra tính tiền (Gemini / ChatGPT: kể cả phần suy nghĩ)
    tokens_think: int = 0


def ds_anh(anh: Any) -> list[tuple[str, str]]:
    """Một ảnh (mime, base64) hoặc danh sách ảnh → danh sách."""
    if not anh:
        return []
    return list(anh) if isinstance(anh, list) else [anh]


def phang_tro_ly(luot: dict) -> str:
    """Lượt trợ lý (trung lập) → văn bản; lời gọi công cụ ghi thành chữ (dựng lại lịch sử khi đổi nhà cung cấp giữa phiên)."""
    import json
    text = luot["text"]
    for gc in luot.get("cong_cu") or []:
        text += f"\n[Đã gọi công cụ {gc.ten} với tham số {json.dumps(gc.tham_so, ensure_ascii=False)}]"
    return text.strip() or "(đã gọi công cụ)"


def phang_cong_cu(luot: dict) -> str:
    import json
    return "\n".join(f"Kết quả công cụ {gc.ten}: {json.dumps(r, ensure_ascii=False)[:6000]}" for gc, r in luot["ket_qua"])


@runtime_checkable
class AIProvider(Protocol):
    """Giao diện chung của một nhà cung cấp AI."""

    ten: str            # "claude" | "gemini" | "openai" (khoá trong routing.json / ai_usage.provider)
    ten_hien_thi: str   # "Claude" | "Gemini" | "ChatGPT"

    def co_key(self) -> bool:
        """Máy chủ đã cấu hình key (+ model nếu cần) cho nhà cung cấp này chưa."""

    def ly_do_bo_qua(self, phien: Any) -> str | None:
        """None = dùng được cho phiên này; ngược lại là lý do (tiếng Việt) để router bỏ qua (chưa có key, không có quyền…)."""

    def mo_hinh(self, phien: Any) -> str:
        """Tên mô hình sẽ dùng cho phiên (ghi chi phí khi lỗi)."""

    def chat(self, phien: Any, luot: dict) -> PhanHoi:
        """Gửi lượt mới nhất (đã nằm cuối phien._lich_su) kèm toàn bộ ngữ cảnh; trả PhanHoi hoặc ném LoiAI."""

    def kiem_tra_suc_khoe(self, model: str) -> None:
        """Một yêu cầu rất nhẹ (không sinh token) để biết key / mô hình còn dùng được; lỗi → ném exception."""
