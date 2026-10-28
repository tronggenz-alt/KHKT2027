# ChatGPT (OpenAI) – `backend/ai/providers/openai/`

AI thứ 3 của app (mặc định: dự phòng sau Claude → Gemini). Dùng SDK chính thức `openai` (Python), **Responses API**, streaming.

| File | Nội dung |
|---|---|
| `client.py` | khởi tạo SDK, đọc `OPENAI_API_KEY` từ `.env` (chỉ ở server – không bao giờ gửi frontend / ghi log) |
| `chat.py` | `goi_openai()`: system + lịch sử + **ảnh** (data URL) + JSON theo schema + mức suy nghĩ + streaming; `OpenAIProvider` (giao diện `AIProvider`); `kiem_tra_suc_khoe()` |
| `config.py` | tên mô hình, nhiệt độ, trần token, thời gian chờ – đều đọc từ `.env` |

## Cấu hình

Nháy đúp **`cau-hinh-openai.command`** (thư mục gốc) → dán key (gõ ẩn) → chọn mô hình → script gọi thử rồi ghi `.env` và khởi động lại app.
Hoặc tự thêm vào `.env`:

```
OPENAI_API_KEY=sk-...
OPENAI_MODEL=gpt-6.1-sol
# tuỳ chọn:
OPENAI_TEMPERATURE=        # trống = mặc định của mô hình (mô hình suy luận không nhận nhiệt độ – app tự bỏ nếu bị từ chối)
OPENAI_MAX_TOKENS=         # trần token đầu ra (trống = theo từng yêu cầu)
AI_TIMEOUT_GIAY=30         # quá N giây không nhận được dữ liệu → chuyển AI kế tiếp
```

Thiếu key **hoặc** `OPENAI_MODEL` → router tự bỏ qua ChatGPT, app vẫn chạy bình thường.

## Chọn / đổi mô hình

Tên mô hình **không** viết cứng trong code – chỉ đổi `OPENAI_MODEL` trong `.env` rồi khởi động lại app.
Theo tài liệu OpenAI (10/2026, developers.openai.com/api/docs/models) – cả ba đều nhận **ảnh** và chạy qua Responses API:

| Mô hình | Ghi chú |
|---|---|
| `gpt-6.1-sol` | cân bằng – **gợi ý** cho app này (giải chuỗi, đọc ảnh đề, cơ chế) |
| `gpt-6-astra` | mạnh nhất, đắt hơn |
| `gpt-6-luna` | nhỏ, rẻ, nhanh (khối lượng lớn) |

## Giới hạn hiện tại

- Không dùng ChatGPT cho phiên có công cụ (tools – chỉ luồng cũ / demo dùng) → router bỏ qua.
- Người dùng đang giải bằng **key Gemini riêng** của họ → không dùng ChatGPT của máy chủ (giữ nguyên tắc BYOK).
- Chi phí ước tính: `chi_phi_ai.BANG_GIA` (giá gpt-6.1-sol theo trang theo dõi giá công khai – ước tính).
