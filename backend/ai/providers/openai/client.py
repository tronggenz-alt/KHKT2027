"""Client ChatGPT (SDK chính thức `openai`) – key OPENAI_API_KEY trong .env, CHỈ dùng ở server (không bao giờ gửi frontend / ghi log)."""
from __future__ import annotations

import openai

from ....config import settings
from ...types import LoiAI
from .config import thoi_gian_cho

_client: openai.OpenAI | None = None


def lay_client() -> openai.OpenAI:
    global _client
    if not settings.openai_api_key:
        raise LoiAI("Chưa có OPENAI_API_KEY trong file .env.", "thieu_key")
    if _client is None:
        _client = openai.OpenAI(api_key=settings.openai_api_key, timeout=thoi_gian_cho(), max_retries=1)
    return _client
