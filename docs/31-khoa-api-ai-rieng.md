# Khóa API AI riêng của từng tài khoản

Mỗi giáo viên hoặc học sinh có thể tự nhập khóa API của một nhà cung cấp AI đám mây trong app VERA Lab.
Khi đó robot và phần chú thích hiện tượng trong lab gọi thẳng nhà cung cấp, không đi qua máy chủ của trường
(`server/` FastAPI hay `cloudflare/` Pages Functions). Tài khoản chưa có khóa vẫn dùng AI của máy chủ như cũ.

## Dùng thế nào

1. Đăng nhập, ở sảnh chính chọn mục **Trợ lý AI** trên thanh bên.
2. Chọn nhà cung cấp: **Google Gemini** (mặc định), **Groq**, **Anthropic Claude** hoặc **OpenAI**.
3. Ô **Mô hình** có thể để trống:
   - Claude dùng `claude-opus-5-5`;
   - Gemini dùng `gemini-flash-latest`;
   - Groq dùng `openai/gpt-oss-120b`;
   - OpenAI tự chọn mô hình trò chuyện mạnh nhất mà khóa được dùng.
4. Dán khóa vào ô **Khóa API**. Ô này che ký tự.
5. Các nút:
   - **Kiểm tra khóa** chỉ hỏi danh sách mô hình nên không tốn lượt gọi;
   - **Lưu khóa** lưu khóa đã mã hóa;
   - **Xóa khóa** quay về dùng AI của máy chủ.

Khóa đã lưu không bao giờ hiện lại trên giao diện mà chỉ hiện ở dạng che (`sk-ant-…ab12`). Chi phí gọi AI được tính vào tài khoản của chủ khóa.

## Lưu trữ và bảo mật

- Khóa lưu ở `persistentDataPath/khoa_ai/<băm>.bin`, mỗi tệp ứng với một cặp (địa chỉ máy chủ, mã tài khoản), nên cùng một máy có thể giữ khóa riêng cho nhiều người.
- **Windows** mã hóa bằng DPAPI (`CryptProtectData`, phạm vi người dùng Windows), kèm entropy là máy chủ và mã tài khoản. Tài khoản Windows khác, hoặc tệp chép sang tài khoản app khác, đều không giải được.
- **macOS và máy khác** mã hóa AES-CBC kèm HMAC, với khóa sinh từ `SystemInfo.deviceUniqueIdentifier`, tên người dùng và mã tài khoản. Cách này chỉ che khóa và không chắc bằng DPAPI; giao diện có ghi rõ điều này.
- Khi đăng xuất, bản đã giải mã bị xóa khỏi bộ nhớ (`GameSession.SignOut` gọi `ApiKeyStore.Forget`).
- Khóa chỉ đi trong header HTTPS: `x-api-key` với Claude, `Authorization: Bearer` với OpenAI, Gemini và Groq. Khóa không bao giờ nằm trong URL, log, nhật ký gửi lên máy chủ hay tệp của dự án.
- Thông báo lỗi của nhà cung cấp được che phần khóa trước khi hiện ra.

## Luồng gọi

`AiClient.Plan` và `AiClient.Annotate` tự chọn đường đi:

- **Có khóa riêng** (`AiClient.Direct`): dùng `DirectAi` để gọi nhà cung cấp, rồi `RobotPlanLocal` làm phần việc mà máy chủ vẫn làm:
  - lời dặn hệ thống và khuôn JSON của kế hoạch;
  - ảnh chụp cảnh và quy trình máy đọc từ `StreamingAssets/data/may` và `data/sinh/dung_cu.json`;
  - chặn yêu cầu sinh học nguy hiểm (chỉ cho cấp an toàn BSL-1);
  - kiểm tra từng bước (`Validate`), tra hóa chất (`FindChemical`) để điền stt;
  - giới hạn số lượng, tối đa 20 bước.

  Câu hỏi và kế hoạch (không kèm khóa) vẫn được gửi vào nhật ký của giáo viên qua `/api/nhat-ky` khi máy chủ còn kết nối được.
- **Không có khóa:** đi qua máy chủ như trước.

Luật của giáo viên luôn thắng khóa riêng:
- giáo viên tắt AI hoặc đang ở chế độ kiểm tra thì app không gọi nhà cung cấp;
- hóa chất bị khóa thì kế hoạch dừng từ bước đầu tiên dùng tới chất đó (`ClassRules`).

Nếu khóa sai hoặc hết hạn mức, khung chat robot báo lỗi rồi làm theo lệnh cơ bản, không lặng lẽ chuyển sang máy chủ. Dòng trạng thái của khung chat cũng hiện "Khóa AI riêng lỗi".

Cách gọi từng nhà cung cấp:
- **Claude:** Messages API, khuôn JSON qua `output_config.format`, `effort: low`, `fallbacks: "default"` với tiêu đề beta `server-side-fallback-2026-07-01`. Gặp lỗi 400 thì thử lại dạng đơn giản: bỏ các tham số đó và dặn JSON bằng lời.
- **OpenAI:** `chat/completions` với `response_format` json_schema chặt và `max_completion_tokens`.
- **Gemini:** cổng tương thích OpenAI (`/v1beta/openai`), giống mạng lưới khóa trên máy chủ. Thử lần lượt: json_schema kèm `reasoning_effort: low`, rồi json_schema, rồi chỉ dặn bằng lời.
- **Groq:** `https://api.groq.com/openai/v1/chat/completions` (tương thích OpenAI). Thử json_schema chặt; mô hình không nhận thì chuyển `json_object`, rồi chỉ dặn bằng lời. Với gpt-oss gửi kèm `reasoning_effort: low`.

## Tệp mã

| Tệp | Việc |
|---|---|
| `Assets/Scripts/Menu/ApiKeyStore.cs` | Lưu, đọc, xóa và mã hóa khóa theo tài khoản |
| `Assets/Scripts/Demo/Ai/DirectAi.cs` | Gọi nhà cung cấp, kiểm tra khóa, tự chọn mô hình, đổi lỗi thành câu dễ hiểu |
| `Assets/Scripts/Demo/Ai/RobotPlanLocal.cs` | Bản C# của `robot_plan.ts` cùng phần tra hóa chất, quy trình máy, BSL-1 trong `ai.ts` |
| `Assets/Scripts/Demo/AiClient.cs` | Chọn đường: khóa riêng hay máy chủ |
| `Assets/Scripts/Menu/Dashboard/PageAiKey.cs` | Trang "Trợ lý AI" ở sảnh |

**Giữ trùng với máy chủ:** sửa kỹ năng, thí nghiệm mẫu hay giới hạn trong `cloudflare/src/robot_plan.ts` thì phải sửa cả `RobotPlanLocal.cs`. Bài thử `khoa_ai` so hai bên và báo lệch.

## Kiểm thử

```powershell
& "D:\KHKT_kiem_thu\chay.ps1" -Mode khoa_ai
```

Bài thử dùng khóa giả và một nhà cung cấp giả chạy trên `127.0.0.1`, nên không gọi mạng thật. Nội dung kiểm tra:
- mã hóa: tệp không chứa khóa ở dạng chữ thường;
- tách tài khoản và tách máy chủ; tệp chép sang tài khoản khác không giải được; xóa khóa;
- tra hóa chất và kiểm tra kế hoạch;
- so danh sách kỹ năng với `robot_plan.ts`;
- gọi thẳng Claude, OpenAI, Gemini, Groq; khóa sai; luật giáo viên;
- các nút trên trang ở sảnh (có ảnh chụp `khoa_ai_trang_sanh.png`);
- khóa không xuất hiện trong log Unity.

Gọi bằng khóa thật thì người dùng phải tự thử: nhập khóa ở sảnh, bấm Kiểm tra khóa, rồi vào lab nhấn T để hỏi robot.
