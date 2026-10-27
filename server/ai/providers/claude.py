"""Nhà cung cấp AI: Claude (Anthropic) qua SDK chính thức `anthropic`.

    pip install anthropic

Khóa API chỉ đặt trên máy chủ (biến môi trường ANTHROPIC_API_KEY, hoặc đăng nhập bằng `ant auth login`),
không bao giờ đưa vào ứng dụng Unity.
Biến môi trường tùy chọn:
    CLAUDE_MODEL   mặc định claude-opus-5-5
    CLAUDE_EFFORT  low | medium | high (mặc định low: trả lời nhanh cho lớp học)
"""

from __future__ import annotations

import json
import os

NAME = "claude"
DEFAULT_MODEL = "claude-opus-5-5"

_client = None


def available() -> bool:
    if os.environ.get("AI_PROVIDER", "").lower() not in ("", "claude", "anthropic"):
        return False
    try:
        import anthropic  # noqa: F401
    except ImportError:
        return False
    return bool(os.environ.get("ANTHROPIC_API_KEY") or os.environ.get("ANTHROPIC_AUTH_TOKEN")
                or os.environ.get("AI_PROVIDER", "").lower() in ("claude", "anthropic"))


def model_name() -> str:
    return os.environ.get("CLAUDE_MODEL", DEFAULT_MODEL)


def _get_client():
    global _client
    if _client is None:
        import anthropic
        _client = anthropic.AsyncAnthropic(timeout=30.0, max_retries=2)
    return _client


async def _create(system: str, user: str, max_tokens: int, output_format: dict | None = None):
    """Một lần gọi Messages API. Bật dự phòng phía máy chủ (server-side fallback) khi mô hình từ chối."""
    output_config: dict = {"effort": os.environ.get("CLAUDE_EFFORT", "low")}
    if output_format is not None:
        output_config["format"] = output_format
    response = await _get_client().beta.messages.create(
        model=model_name(),
        max_tokens=max_tokens,
        system=[{"type": "text", "text": system, "cache_control": {"type": "ephemeral"}}],
        messages=[{"role": "user", "content": user}],
        output_config=output_config,
        betas=["server-side-fallback-2026-07-01"],
        fallbacks="default",
    )
    if response.stop_reason == "refusal":
        raise RuntimeError("Mô hình từ chối trả lời yêu cầu này")
    return response


def _text_of(response) -> str:
    return "".join(b.text for b in response.content if b.type == "text").strip()


async def text(system: str, user: str, max_tokens: int = 400) -> str:
    return _text_of(await _create(system, user, max_tokens))


async def structured(system: str, user: str, schema: dict, max_tokens: int = 800) -> dict:
    response = await _create(system, user, max_tokens, {"type": "json_schema", "schema": schema})
    if response.stop_reason == "max_tokens":
        raise RuntimeError("Câu trả lời bị cắt (max_tokens)")
    return json.loads(_text_of(response))
