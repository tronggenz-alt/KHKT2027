"""Tầng AI của app: 3 nhà cung cấp (Claude, Gemini, ChatGPT) cùng giao diện AIProvider + router chọn thứ tự / dự phòng.

Tính năng (Giải chuỗi, Mô hình 3D, Cơ chế) chỉ dùng `llm.PhienHoiThoai` – phiên đi theo chuỗi AI của router nên không cần biết
đang gọi AI nào. Thêm nhà cung cấp mới: tạo providers/<ten>/ (client.py, chat.py, config.py) có lớp theo AIProvider,
đăng ký vào NHA_CUNG_CAP dưới đây và thêm tên vào router.CAC_AI + routing.json.
"""
from .providers.claude import ClaudeProvider
from .providers.gemini import GeminiProvider
from .providers.openai import OpenAIProvider
from .types import AIProvider, GoiCongCu, LoiAI, PhanHoi

NHA_CUNG_CAP: dict[str, AIProvider] = {"claude": ClaudeProvider(), "gemini": GeminiProvider(), "openai": OpenAIProvider()}

__all__ = ["AIProvider", "GoiCongCu", "LoiAI", "NHA_CUNG_CAP", "PhanHoi"]
