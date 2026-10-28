"""Gọi Claude (Anthropic SDK, streaming) + ClaudeProvider (giao diện AIProvider dùng chung)."""
from __future__ import annotations

import json
import logging
from typing import Any, Callable

import anthropic

from ....config import settings
from ...types import GoiCongCu, LoiAI, PhanHoi, ds_anh, phang_cong_cu, phang_tro_ly
from . import client as _c
from .config import FALLBACK_BETA, FALLBACK_MODELS, NGUONG_CACHE_SYSTEM

log = logging.getLogger(__name__)


def _claude_stream(use_fallback: bool, kwargs: dict[str, Any], api_key: str | None = None,
                   khi_co_chu: Callable[[str | None], None] | None = None):
    """khi_co_chu: nhận từng đoạn chữ ngay khi Claude viết ra (None = bắt đầu lại từ đầu, vd khi thử lại)."""
    client = _c.claude_cua(api_key)
    luong = (client.beta.messages.stream(betas=[FALLBACK_BETA], fallbacks="default", **kwargs) if use_fallback
             else client.messages.stream(**kwargs))
    with luong as stream:
        if khi_co_chu is not None:
            khi_co_chu(None)
            for doan in stream.text_stream:
                khi_co_chu(doan)
        return stream.get_final_message()


def goi_claude(
    *,
    system: str,
    messages: list[dict[str, Any]],
    max_tokens: int = 32000,
    effort: str | None = None,
    tools: list[dict[str, Any]] | None = None,
    output_schema: dict[str, Any] | None = None,
    model: str = "",
    api_key: str | None = None,
    khi_co_chu: Callable[[str | None], None] | None = None,
):
    """Một lượt gọi Claude (streaming để tránh timeout); trả về message cuối hoặc ném LoiAI.
    model: mô hình riêng cho lượt này (vd Claude mạnh nhất cho cơ chế HSG); trống = CLAUDE_MODEL.
    api_key: key Claude RIÊNG của người dùng (trống = key máy chủ ANTHROPIC_API_KEY); thông báo lỗi nói rõ là key của họ."""
    rieng = bool(api_key)
    model = model or settings.model
    # Prompt caching: phần hướng dẫn cố định (system dài) đặt đầu + cache_control → các lần gọi sau đọc từ cache (rẻ, nhanh)
    he_thong: Any = ([{"type": "text", "text": system, "cache_control": {"type": "ephemeral"}}] if len(system) >= NGUONG_CACHE_SYSTEM
                     else system)
    kwargs: dict[str, Any] = {"model": model, "max_tokens": max_tokens, "system": he_thong, "messages": messages}
    if tools:
        kwargs["tools"] = tools
    output_config: dict[str, Any] = {}
    if effort:
        output_config["effort"] = effort
    if output_schema:
        output_config["format"] = {"type": "json_schema", "schema": output_schema}
    if output_config:
        kwargs["output_config"] = output_config

    use_fallback = settings.fallback and model in FALLBACK_MODELS
    try:
        try:
            msg = _claude_stream(use_fallback, kwargs, api_key, khi_co_chu)
        except anthropic.BadRequestError as e:
            # Mô hình/tài khoản không nhận tham số dự phòng hoặc effort: thử lại không có chúng
            text = str(e).lower()
            if use_fallback and "fallback" in text:
                msg = _claude_stream(False, kwargs, api_key, khi_co_chu)
            elif "effort" in text and "output_config" in kwargs:
                kwargs["output_config"].pop("effort", None)
                msg = _claude_stream(use_fallback, kwargs, api_key, khi_co_chu)
            else:
                raise
    except anthropic.AuthenticationError as e:
        raise LoiAI("API key Claude của bạn không hợp lệ hoặc đã bị thu hồi – vào Cài đặt → API key Claude để nhập lại." if rieng
                    else "API key Claude không hợp lệ hoặc đã bị thu hồi (ANTHROPIC_API_KEY trong .env).", "key") from e
    except anthropic.PermissionDeniedError as e:
        raise LoiAI(f"API key Claude{' của bạn' if rieng else ''} không có quyền dùng mô hình {model}.", "quyen") from e
    except anthropic.NotFoundError as e:
        raise LoiAI(f"Key Claude của bạn không dùng được mô hình '{model}'." if rieng
                    else f"Không tìm thấy mô hình Claude '{model}' (CLAUDE_MODEL trong .env).", "model") from e
    except anthropic.RateLimitError as e:
        raise LoiAI("Claude: đã vượt giới hạn số lần gọi, vui lòng đợi một lát.", "gioi_han") from e
    except anthropic.BadRequestError as e:
        if "credit balance" in str(e).lower():
            raise LoiAI("Tài khoản Anthropic của bạn đã hết tín dụng (nạp thêm tại platform.claude.com)." if rieng
                        else "Tài khoản Anthropic đã hết tín dụng (cần nạp thêm tại platform.claude.com).", "het_tien") from e
        raise LoiAI(f"Yêu cầu gửi tới Claude không hợp lệ: {getattr(e, 'message', e)}", "yeu_cau") from e
    except anthropic.APITimeoutError as e:
        raise LoiAI(f"Claude quá {settings.ai_timeout_giay} giây không phản hồi.", "timeout") from e
    except anthropic.APIConnectionError as e:
        raise LoiAI("Không kết nối được tới Claude (kiểm tra mạng Internet).", "mang") from e
    except anthropic.APIStatusError as e:
        if e.status_code >= 500:
            raise LoiAI("Máy chủ Claude đang quá tải hoặc gặp lỗi.", "qua_tai") from e
        raise LoiAI(f"Lỗi khi gọi Claude ({e.status_code}): {getattr(e, 'message', e)}", "khac") from e

    usage = getattr(msg, "usage", None)
    log.info("Claude %s stop=%s in=%s out=%s", getattr(msg, "model", model), msg.stop_reason,
             getattr(usage, "input_tokens", "?"), getattr(usage, "output_tokens", "?"))
    if msg.stop_reason == "refusal":
        raise LoiAI("Claude từ chối xử lý yêu cầu này.", "tu_choi")
    return msg


def kiem_tra_suc_khoe(model: str) -> None:
    """Kiểm tra mô hình và số dư tín dụng bằng cách gọi thử 1 token; lỗi → exception."""
    c = anthropic.Anthropic(api_key=settings.api_key, timeout=8.0, max_retries=0)
    m = model or settings.model
    c.models.retrieve(m)
    c.messages.create(
        model=m,
        max_tokens=1,
        messages=[{"role": "user", "content": "1"}],
    )


class ClaudeProvider:
    """AIProvider cho Claude. Chỉ dùng được khi phiên đã được cấp quyền Claude (admin / Key Claude / mã Claude / API key riêng,
    kiểm tra ở llm.PhienHoiThoai) – người dùng thường không bao giờ tốn Claude của máy chủ."""

    ten = "claude"
    ten_hien_thi = "Claude"

    def co_key(self) -> bool:
        return bool(settings.api_key)

    def ly_do_bo_qua(self, phien) -> str | None:
        return None if phien.claude_duoc_dung else (phien.loi_claude or "tài khoản chưa mở Claude")

    def mo_hinh(self, phien) -> str:
        return phien.claude_model or settings.model

    def kiem_tra_suc_khoe(self, model: str = "") -> None:
        kiem_tra_suc_khoe(model)

    @staticmethod
    def _tu_luot(luot: dict, ban_dia: bool) -> dict:
        """Lượt trung lập → message Claude. ban_dia=False (dựng lại khi đổi sang Claude giữa phiên): công cụ ghi thành chữ."""
        vai = luot["vai"]
        if vai == "assistant":
            return {"role": "assistant", "content": phang_tro_ly(luot)}
        if vai == "tool":
            if not ban_dia:
                return {"role": "user", "content": phang_cong_cu(luot)}
            return {"role": "user", "content": [{"type": "tool_result", "tool_use_id": gc.id, "content": json.dumps(r, ensure_ascii=False)[:6000],
                                                 "is_error": not r.get("ok", True)} for gc, r in luot["ket_qua"]]}
        content: list[dict] = [{"type": "image", "source": {"type": "base64", "media_type": mt, "data": data}} for mt, data in ds_anh(luot.get("anh"))]
        content.append({"type": "text", "text": luot["text"]})
        return {"role": "user", "content": content}

    def chat(self, phien, luot: dict) -> PhanHoi:
        if phien._claude_msgs is None:  # lượt đầu / chuyển sang Claude giữa phiên → dựng lại từ lịch sử trung lập (đã có lượt này)
            phien._claude_msgs = [self._tu_luot(x, ban_dia=False) for x in phien._lich_su]
        else:
            phien._claude_msgs.append(self._tu_luot(luot, ban_dia=True))
        msg = goi_claude(
            system=phien.system,
            messages=phien._claude_msgs,
            max_tokens=phien.max_tokens,
            effort=phien.effort,
            tools=phien.tools,
            output_schema=phien.output_schema,
            model=phien.claude_model,
            **({"api_key": phien.key_claude_rieng} if phien.key_claude_rieng else {}),
            **({"khi_co_chu": phien._khi_co_chu} if phien._khi_co_chu else {}),
        )
        phien._claude_msgs.append({"role": "assistant", "content": msg.content})
        text = "".join(b.text for b in msg.content if getattr(b, "type", "") == "text").strip()
        cong_cu = [GoiCongCu(id=b.id, ten=b.name, tham_so=dict(b.input or {})) for b in msg.content if getattr(b, "type", "") == "tool_use"]
        phien._lich_su.append({"vai": "assistant", "text": text, "cong_cu": cong_cu})
        u = getattr(msg, "usage", None)
        vao = sum(int(getattr(u, k, 0) or 0) for k in ("input_tokens", "cache_creation_input_tokens", "cache_read_input_tokens"))
        return PhanHoi(text, cong_cu, "claude", getattr(msg, "model", phien.claude_model or settings.model), msg.stop_reason == "max_tokens",
                       tokens_in=vao, tokens_out=int(getattr(u, "output_tokens", 0) or 0))
