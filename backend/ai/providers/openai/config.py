"""Cấu hình ChatGPT. Tên mô hình KHÔNG viết cứng: OPENAI_MODEL trong .env (xem README.md cùng thư mục)."""
from __future__ import annotations

import openai

from ....config import settings

MUC_SUY_NGHI = {"low", "medium", "high", "xhigh", "max"}  # reasoning.effort mà các mô hình GPT-6 nhận


def mo_hinh() -> str:
    return settings.openai_model


def nhiet_do() -> float | None:
    """OPENAI_TEMPERATURE (trống = mặc định của mô hình – mô hình suy luận không nhận nhiệt độ, app tự bỏ nếu bị từ chối)."""
    return settings.openai_temperature


def tran_token(yeu_cau: int) -> int:
    """max_output_tokens: theo yêu cầu (như Claude / Gemini), có trần OPENAI_MAX_TOKENS nếu đặt."""
    return min(yeu_cau, settings.openai_max_tokens) if settings.openai_max_tokens > 0 else yeu_cau


def muc_suy_nghi(effort: str | None) -> str | None:
    return effort if effort in MUC_SUY_NGHI else None


def thoi_gian_cho() -> openai.Timeout:
    """Mọi lượt gọi đều streaming → read = số giây tối đa KHÔNG nhận được dữ liệu (AI_TIMEOUT_GIAY, mặc định 30)."""
    # dùng Timeout của chính SDK (như Claude: SDK anthropic mới chạy trên httpx2, từ chối httpx.Timeout)
    return openai.Timeout(900.0, connect=10.0, read=float(settings.ai_timeout_giay), write=30.0, pool=10.0)
