"""Cấu hình Gemini. Tên mô hình KHÔNG viết cứng ở đây: GEMINI_MODEL_LITE / _FLASH / _PRO trong .env (backend/config.py)."""
from __future__ import annotations

from ....config import GEMINI_MODEL_DU_PHONG, settings

MO_HINH_KIEM_TRA_KEY = ["gemini-flash-lite-latest"]  # mô hình nhẹ, hạn mức miễn phí rộng → gọi thử key ít tốn lượt


def mo_hinh_gemini(bac: str) -> str:
    """Bậc (lite | flash | pro | claude→pro) → tên mô hình Gemini trong .env."""
    return {"lite": settings.gemini_model_lite, "pro": settings.gemini_model_hsg}.get(bac, settings.gemini_model)


__all__ = ["GEMINI_MODEL_DU_PHONG", "MO_HINH_KIEM_TRA_KEY", "mo_hinh_gemini"]
