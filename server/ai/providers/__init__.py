"""Chọn nhà cung cấp AI đám mây.

Thứ tự mặc định: mạng lưới khóa (Groq, DeepSeek, Gemini nhập trên web quản lý) → Claude (khóa Anthropic)
→ API kiểu OpenAI (LLM_BASE_URL). Nhà cung cấp đầu lỗi (mọi khóa đang nghỉ, mạng...) thì tự chuyển sang nhà kế tiếp.
AI_PROVIDER=mang_luoi_khoa | claude | openai_compat để chỉ dùng một nhà.
Không có nhà cung cấp nào: máy chủ vẫn chạy, trả thẻ thông tin soạn sẵn; ứng dụng Unity tự hiểu lệnh cơ bản.
"""

from __future__ import annotations

import os
from types import ModuleType

from server.lab import keypool

from . import claude, openai_compat

_ALL = {"mang_luoi_khoa": keypool, "claude": claude, "openai_compat": openai_compat}


class Chain:
    """Gọi lần lượt các nhà cung cấp đang dùng được; NAME là nhà vừa trả lời (hoặc nhà đầu tiên)."""

    def __init__(self, items: list[ModuleType]):
        self.items = items
        self.NAME = items[0].NAME

    def model_name(self) -> str:
        return self.items[0].model_name()

    async def text(self, system: str, user: str, max_tokens: int = 400) -> str:
        return await self._try("text", system, user, max_tokens)

    async def structured(self, system: str, user: str, schema: dict, max_tokens: int = 800) -> dict:
        return await self._try("structured", system, user, schema, max_tokens)

    async def _try(self, fn: str, *args):
        error: Exception | None = None
        for p in self.items:
            try:
                out = await getattr(p, fn)(*args)
            except Exception as e:  # khóa hết hạn mức, mạng, từ chối: thử nhà kế tiếp
                error = e
                continue
            self.NAME = p.NAME
            return out
        raise error or RuntimeError("không có nhà cung cấp")


def pick() -> Chain | None:
    want = os.environ.get("AI_PROVIDER", "").lower().replace("anthropic", "claude")
    if want in _ALL:
        return Chain([_ALL[want]]) if _ALL[want].available() else None
    ready = [p for p in (keypool, claude, openai_compat) if p.available()]
    return Chain(ready) if ready else None
