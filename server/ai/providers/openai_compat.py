"""Nhà cung cấp AI kiểu OpenAI (vLLM, Ollama, dịch vụ tương thích) - giữ nguyên cách gọi cũ của dự án.

Biến môi trường:
    LLM_BASE_URL  ví dụ http://localhost:11434/v1
    LLM_MODEL     tên mô hình
    LLM_API_KEY   (nếu dịch vụ cần) - chỉ để trên máy chủ
"""

from __future__ import annotations

import json
import os

import httpx

NAME = "openai_compat"


def available() -> bool:
    return bool(os.environ.get("LLM_BASE_URL"))


def model_name() -> str:
    return os.environ.get("LLM_MODEL", "")


async def _chat(system: str, user: str, max_tokens: int, json_mode: bool = False) -> str:
    base_url = os.environ["LLM_BASE_URL"].rstrip("/")
    headers = {}
    if os.environ.get("LLM_API_KEY"):
        headers["Authorization"] = f"Bearer {os.environ['LLM_API_KEY']}"
    body = {
        "model": model_name(),
        "messages": [{"role": "system", "content": system}, {"role": "user", "content": user}],
        "max_tokens": max_tokens,
        "temperature": 0.3,
    }
    if json_mode:
        body["response_format"] = {"type": "json_object"}
    async with httpx.AsyncClient(timeout=20) as client:
        r = await client.post(f"{base_url}/chat/completions", json=body, headers=headers)
        r.raise_for_status()
        return r.json()["choices"][0]["message"]["content"].strip()


async def text(system: str, user: str, max_tokens: int = 400) -> str:
    return await _chat(system, user, max_tokens)


async def structured(system: str, user: str, schema: dict, max_tokens: int = 800) -> dict:
    hint = "\n\nTrả lời DUY NHẤT bằng một đối tượng JSON theo lược đồ: " + json.dumps(schema, ensure_ascii=False)
    return json.loads(await _chat(system + hint, user, max_tokens, json_mode=True))
