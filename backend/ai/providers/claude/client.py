"""Client Claude (Anthropic SDK): key máy chủ (ANTHROPIC_API_KEY trong .env) hoặc API key Claude RIÊNG của người dùng."""
from __future__ import annotations

import hashlib
from collections import OrderedDict

import anthropic

from ....config import settings
from ...types import LoiAI
from .config import thoi_gian_cho

_claude_client: anthropic.Anthropic | None = None
_client_claude_rieng: OrderedDict[str, anthropic.Anthropic] = OrderedDict()


def _claude() -> anthropic.Anthropic:
    global _claude_client
    if not settings.api_key:
        raise LoiAI("Chưa có ANTHROPIC_API_KEY trong file .env.", "thieu_key")
    if _claude_client is None:
        _claude_client = anthropic.Anthropic(api_key=settings.api_key, timeout=thoi_gian_cho(), max_retries=2)
    return _claude_client


def van_tay(key: str) -> str:
    return hashlib.sha256(key.encode()).hexdigest()[:24]


def claude_cua(api_key: str | None) -> anthropic.Anthropic:
    """Client Claude: key máy chủ (.env) hoặc key RIÊNG của người dùng (cache tối đa 32 client)."""
    if not api_key:
        return _claude()
    ma = van_tay(api_key)
    c = _client_claude_rieng.get(ma)
    if c is None:
        c = _client_claude_rieng[ma] = anthropic.Anthropic(api_key=api_key, timeout=thoi_gian_cho(), max_retries=2)
        while len(_client_claude_rieng) > 32:
            _client_claude_rieng.popitem(last=False)
    _client_claude_rieng.move_to_end(ma)
    return c
