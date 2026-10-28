"""Cấu hình Claude. Tên mô hình KHÔNG viết cứng ở đây: CLAUDE_MODEL / CLAUDE_MODEL_MANH trong .env (backend/config.py)."""
from __future__ import annotations

import anthropic

from ....config import settings

# Các mô hình Claude hỗ trợ tham số fallbacks="default" (beta, chỉ trên Claude API) – dự phòng phía máy chủ Anthropic
FALLBACK_MODELS = {"claude-sonnet-5-5", "claude-opus-5-5", "claude-opus-5", "claude-fable-5-1"}
FALLBACK_BETA = "server-side-fallback-2026-07-01"
NGUONG_CACHE_SYSTEM = 1500  # system dài từ chừng này ký tự → prompt caching (cache_control)


def thoi_gian_cho() -> anthropic.Timeout:
    """Mọi lượt gọi Claude đều streaming → read = số giây tối đa KHÔNG nhận được dữ liệu (AI_TIMEOUT_GIAY, mặc định 30)."""
    # dùng Timeout của chính SDK (anthropic mới chạy trên httpx2 – từ chối httpx.Timeout)
    return anthropic.Timeout(900.0, connect=10.0, read=float(settings.ai_timeout_giay), write=30.0, pool=10.0)
