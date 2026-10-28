# Claude (Anthropic) – `backend/ai/providers/claude/`

- `client.py`: client Anthropic SDK – key máy chủ `ANTHROPIC_API_KEY` (.env) hoặc API key Claude **riêng** của người dùng (claude_rieng.py).
- `chat.py`: `goi_claude()` (streaming, prompt caching, structured output, đổi lỗi SDK → `LoiAI` tiếng Việt) + `ClaudeProvider`
  (giao diện `AIProvider` dùng chung) + `kiem_tra_suc_khoe()` (chỉ `models.retrieve`, không tốn token).
- `config.py`: mô hình hỗ trợ dự phòng phía máy chủ Anthropic, ngưỡng prompt caching, thời gian chờ.

Cấu hình (.env – đặt bằng `cau-hinh-claude.command`):

| Biến | Ý nghĩa |
|---|---|
| `ANTHROPIC_API_KEY` | key máy chủ |
| `CLAUDE_MODEL` | mô hình mặc định (Giải chuỗi mức Kỹ, đọc ảnh đề) |
| `CLAUDE_MODEL_MANH` | mô hình mạnh nhất (mức Tối đa, viết cơ chế) |
| `AI_TIMEOUT_GIAY` | quá N giây không nhận được dữ liệu → quá thời gian, router chuyển AI kế tiếp (mặc định 30) |

Claude chỉ chạy cho tài khoản có quyền Claude (admin, Key Claude, mã Claude, API key riêng) – kiểm tra ở `llm.PhienHoiThoai`.
Đổi mô hình: sửa `CLAUDE_MODEL` / `CLAUDE_MODEL_MANH` trong `.env` rồi khởi động lại app.
