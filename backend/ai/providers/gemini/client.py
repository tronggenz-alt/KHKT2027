"""Client Gemini (google-genai): key của chủ app (GEMINI_API_KEY trong .env) hoặc key Gemini RIÊNG của người dùng (BYOK)."""
from __future__ import annotations

from collections import OrderedDict
from typing import Any

from ....config import settings

_gemini_client = None
# Key riêng của người dùng: client tách theo từng key (mã băm), không dùng chung với key của chủ app
_client_ca_nhan: OrderedDict[str, Any] = OrderedDict()


def _tao_client_gemini(api_key: str):
    from google import genai
    from google.genai import types

    # Gemini trả cả khối (không đo được "im lặng" như streaming) → giới hạn TỔNG thời gian mỗi lần gọi (AI_TIMEOUT_DAI_GIAY)
    return genai.Client(api_key=api_key, http_options=types.HttpOptions(timeout=int(settings.ai_timeout_dai_giay) * 1000))


def _gemini():
    """Client Gemini bằng key của chủ app (.env)."""
    global _gemini_client
    if _gemini_client is None:
        _gemini_client = _tao_client_gemini(settings.gemini_api_key)
    return _gemini_client


def _gemini_ca_nhan(khoa):
    c = _client_ca_nhan.get(khoa.ma)
    if c is None:
        c = _client_ca_nhan[khoa.ma] = _tao_client_gemini(khoa.gemini_api_key)
        while len(_client_ca_nhan) > 32:
            _client_ca_nhan.popitem(last=False)
    _client_ca_nhan.move_to_end(khoa.ma)
    return c


def san_sang(khoa) -> bool:
    """Gemini dùng được cho yêu cầu có key `khoa`: key riêng của người dùng, hoặc key chủ app (GEMINI_API_KEY + GEMINI_FALLBACK)."""
    if khoa.nguon != "chu_app":
        return khoa.nguon == "ca_nhan" and bool(khoa.gemini_api_key)
    return settings.gemini_fallback and bool(settings.gemini_api_key)
