# Gemini (Google) – `backend/ai/providers/gemini/`

- `client.py`: client google-genai – key của chủ app `GEMINI_API_KEY` (.env) hoặc key Gemini **riêng** của người dùng (BYOK, Cài đặt → 02).
  Người dùng có key riêng → CHỈ dùng key đó (không dùng key máy chủ, không chuyển sang ChatGPT của máy chủ).
- `chat.py`: `_goi_gemini()` (streaming khi cần, structured output, ngân sách suy nghĩ, thử lại khi quá tải, lùi mô hình khi hết hạn mức)
  + `GeminiProvider` (giao diện `AIProvider`) + `kiem_tra_key_gemini()` (Cài đặt) + `kiem_tra_suc_khoe()` (`models.get`, không tốn token).
- `config.py`: `mo_hinh_gemini(bac)` – bậc lite / flash / pro → tên mô hình trong .env.

| Biến (.env) | Ý nghĩa |
|---|---|
| `GEMINI_API_KEY` | key của chủ app |
| `GEMINI_MODEL_LITE` / `GEMINI_MODEL_FLASH` / `GEMINI_MODEL_PRO` | mô hình theo bậc (Dễ / Trung bình / HSG) |
| `GEMINI_MODEL_DU_PHONG` (config.py) | danh sách mô hình lùi dần khi hết hạn mức |
| `AI_TIMEOUT_DAI_GIAY` | tổng thời gian tối đa mỗi lần gọi Gemini (mặc định 300 giây) |
