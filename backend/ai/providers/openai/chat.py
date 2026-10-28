"""Gọi ChatGPT (OpenAI Responses API, streaming) + OpenAIProvider (giao diện AIProvider dùng chung).

Hỗ trợ: system (instructions), lịch sử nhiều lượt, ẢNH (input_image dạng data URL), JSON theo schema (text.format json_schema),
mức suy nghĩ (reasoning.effort), streaming từng đoạn chữ. Chưa hỗ trợ công cụ (tools) → router bỏ qua ChatGPT cho phiên có tools.
"""
from __future__ import annotations

import logging
from typing import Any, Callable

import httpx
import openai

from ....config import settings
from ...types import LoiAI, PhanHoi, ds_anh, phang_cong_cu, phang_tro_ly
from . import client as _c
from .config import muc_suy_nghi, nhiet_do, tran_token

log = logging.getLogger(__name__)


def lich_su_sang_input(lich_su: list[dict]) -> list[dict]:
    """Lịch sử trung lập của phiên → `input` của Responses API (ChatGPT không giữ trạng thái: gửi lại cả lịch sử mỗi lượt)."""
    ds: list[dict] = []
    for luot in lich_su:
        if luot["vai"] == "user":
            noi_dung: list[dict] = [{"type": "input_text", "text": luot["text"] or "(trống)"}]
            noi_dung += [{"type": "input_image", "image_url": f"data:{mt};base64,{data}"} for mt, data in ds_anh(luot.get("anh"))]
            ds.append({"role": "user", "content": noi_dung})
        elif luot["vai"] == "assistant":
            ds.append({"role": "assistant", "content": phang_tro_ly(luot)})
        else:
            ds.append({"role": "user", "content": phang_cong_cu(luot)})
    return ds


def _doi_loi(e: Exception, model: str) -> LoiAI:
    """Lỗi SDK OpenAI → LoiAI tiếng Việt (loai quyết định router có chuyển AI kế tiếp không – luôn chuyển, nhưng ghi đúng lý do)."""
    t = str(e).lower()
    if isinstance(e, openai.AuthenticationError):
        return LoiAI("OPENAI_API_KEY không hợp lệ hoặc đã bị thu hồi.", "key")
    if isinstance(e, openai.PermissionDeniedError):
        return LoiAI(f"Key ChatGPT không có quyền dùng mô hình {model}.", "quyen")
    if isinstance(e, openai.NotFoundError):
        return LoiAI(f"Không tìm thấy mô hình ChatGPT '{model}' (OPENAI_MODEL trong .env).", "model")
    if isinstance(e, openai.RateLimitError):
        if "insufficient_quota" in t or "billing" in t:
            return LoiAI("Tài khoản OpenAI đã hết tín dụng / hạn mức (nạp thêm tại platform.openai.com).", "het_tien")
        return LoiAI("ChatGPT: vượt giới hạn số lần gọi, vui lòng đợi một lát.", "gioi_han")
    if isinstance(e, (openai.APITimeoutError, httpx.TimeoutException)):
        return LoiAI(f"ChatGPT quá {settings.ai_timeout_giay} giây không phản hồi.", "timeout")
    if isinstance(e, (openai.APIConnectionError, httpx.HTTPError)):
        return LoiAI("Không kết nối được tới ChatGPT (kiểm tra mạng Internet).", "mang")
    if isinstance(e, openai.BadRequestError):
        return LoiAI(f"Yêu cầu gửi tới ChatGPT không hợp lệ: {getattr(e, 'message', e)}"[:300], "yeu_cau")
    if isinstance(e, openai.APIStatusError) and e.status_code >= 500:
        return LoiAI("Máy chủ ChatGPT đang quá tải hoặc gặp lỗi.", "qua_tai")
    return LoiAI(f"Lỗi khi gọi ChatGPT: {e.__class__.__name__}", "khac")


def _chay(kwargs: dict[str, Any], khi_co_chu: Callable[[str | None], None] | None) -> tuple[str, Any]:
    luong = _c.lay_client().responses.create(**kwargs)
    if khi_co_chu is not None:
        khi_co_chu(None)
    chu: list[str] = []
    cuoi = None
    for ev in luong:
        loai = getattr(ev, "type", "")
        if loai == "response.output_text.delta":
            chu.append(ev.delta)
            if khi_co_chu is not None:
                khi_co_chu(ev.delta)
        elif loai in ("response.completed", "response.incomplete"):
            cuoi = ev.response
        elif loai == "response.failed":
            loi = getattr(getattr(ev, "response", None), "error", None)
            raise LoiAI(f"ChatGPT báo lỗi: {getattr(loi, 'message', '') or 'không rõ'}"[:300], "qua_tai")
        elif loai == "error":
            raise LoiAI(f"ChatGPT báo lỗi: {getattr(ev, 'message', '') or 'không rõ'}"[:300], "khac")
    if cuoi is None:
        raise LoiAI("ChatGPT ngắt kết nối giữa chừng.", "mang")
    return "".join(chu) or (getattr(cuoi, "output_text", "") or ""), cuoi


def goi_openai(*, system: str, input: list[dict], max_tokens: int, effort: str | None = None, output_schema: dict | None = None,
               model: str = "", khi_co_chu: Callable[[str | None], None] | None = None) -> PhanHoi:
    """Một lượt gọi ChatGPT (streaming); trả PhanHoi hoặc ném LoiAI."""
    model = model or settings.openai_model
    if not model:
        raise LoiAI("Chưa đặt OPENAI_MODEL trong file .env.", "thieu_key")
    kwargs: dict[str, Any] = {"model": model, "instructions": system, "input": input, "max_output_tokens": tran_token(max_tokens), "stream": True}
    if output_schema:
        kwargs["text"] = {"format": {"type": "json_schema", "name": "ket_qua", "schema": output_schema, "strict": False}}
    if (muc := muc_suy_nghi(effort)) is not None:
        kwargs["reasoning"] = {"effort": muc}
    if (t := nhiet_do()) is not None:
        kwargs["temperature"] = t
    for _ in range(3):  # mô hình không nhận một tham số (reasoning / temperature) → bỏ tham số đó, gọi lại
        try:
            text, cuoi = _chay(kwargs, khi_co_chu)
            break
        except LoiAI:
            raise
        except openai.BadRequestError as e:
            bo = next((k for k in ("reasoning", "temperature") if k in kwargs and k in str(e).lower()), None)
            if bo is None:
                raise _doi_loi(e, model) from e
            log.info("ChatGPT %s không nhận tham số '%s' – gọi lại không có", model, bo)
            kwargs.pop(bo)
        except (openai.OpenAIError, httpx.HTTPError) as e:
            raise _doi_loi(e, model) from e
    else:
        raise LoiAI("ChatGPT từ chối các tham số yêu cầu.", "yeu_cau")
    chi_tiet = getattr(cuoi, "incomplete_details", None)
    if getattr(chi_tiet, "reason", "") == "content_filter":
        raise LoiAI("ChatGPT từ chối xử lý yêu cầu này.", "tu_choi")
    u = getattr(cuoi, "usage", None)
    vao, ra = int(getattr(u, "input_tokens", 0) or 0), int(getattr(u, "output_tokens", 0) or 0)
    nghi = int(getattr(getattr(u, "output_tokens_details", None), "reasoning_tokens", 0) or 0)
    log.info("ChatGPT %s status=%s in=%s out=%s think=%s", getattr(cuoi, "model", model), getattr(cuoi, "status", "?"), vao, ra, nghi)
    return PhanHoi(text.strip(), [], "openai", getattr(cuoi, "model", "") or model,
                   getattr(cuoi, "status", "") == "incomplete" and getattr(chi_tiet, "reason", "") == "max_output_tokens",
                   tokens_in=vao, tokens_out=ra, tokens_think=nghi)


def kiem_tra_suc_khoe(model: str) -> None:
    """Request rất nhẹ: chỉ hỏi thông tin mô hình (không sinh token, không tốn tiền); lỗi → exception."""
    openai.OpenAI(api_key=settings.openai_api_key, timeout=8.0, max_retries=0).models.retrieve(model or settings.openai_model)


class OpenAIProvider:
    """AIProvider cho ChatGPT – chỉ dùng key của MÁY CHỦ (OPENAI_API_KEY). Người dùng đang dùng key Gemini riêng thì không dùng
    ChatGPT của máy chủ (giữ đúng nguyên tắc BYOK)."""

    ten = "openai"
    ten_hien_thi = "ChatGPT"

    def co_key(self) -> bool:
        return bool(settings.openai_api_key and settings.openai_model)

    def ly_do_bo_qua(self, phien) -> str | None:
        if not settings.openai_api_key:
            return "chưa có OPENAI_API_KEY"
        if not settings.openai_model:
            return "chưa đặt OPENAI_MODEL"
        if phien.khoa.nguon != "chu_app":
            return "đang dùng key riêng của người dùng – không dùng ChatGPT của máy chủ"
        if phien.tools:
            return "ChatGPT chưa hỗ trợ công cụ (tools) trong app"
        return None

    def mo_hinh(self, phien) -> str:
        return settings.openai_model

    def kiem_tra_suc_khoe(self, model: str = "") -> None:
        kiem_tra_suc_khoe(model)

    def chat(self, phien, luot: dict) -> PhanHoi:
        ph = goi_openai(system=phien.system, input=lich_su_sang_input(phien._lich_su), max_tokens=phien.max_tokens, effort=phien.effort,
                        output_schema=phien.output_schema, **({"khi_co_chu": phien._khi_co_chu} if phien._khi_co_chu else {}))
        phien._lich_su.append({"vai": "assistant", "text": ph.text, "cong_cu": []})
        return ph
