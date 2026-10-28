# Đưa app lên web (Render – gói MIỄN PHÍ) – hướng dẫn ngắn

Web chạy trên máy chủ Render (Singapore), **không cần bật máy Mac**, **0 đồng**.

| Repo GitHub (đều riêng tư) | Chứa gì |
|---|---|
| `tyrt07112/giai-chuoi-huu-co` | Code (Render lấy từ đây) |
| `tyrt07112/giai-chuoi-du-lieu` | Bản sao lưu dữ liệu tự động. **Đừng sửa tay.** |

Gói miễn phí có ba giới hạn:
- **Web ngủ sau 15 phút không ai dùng.** Người mở trang lúc đó phải chờ khoảng 1 phút.
- **Máy chủ yếu hơn** Mac nên vẽ hình chậm hơn một chút.
- **Không có ổ đĩa lưu.** App tự sao lưu tài khoản, lịch sử và tài liệu AI Riêng sang repo dữ liệu mỗi 2 phút và trước khi ngủ. Khi web thức dậy, app tự khôi phục.
  - Ảnh của các bài giải cũ không được lưu. Lời giải vẫn còn nguyên trong lịch sử.

Key AI (Claude, Gemini…) **chỉ** nằm ở mục *Environment* trên Render và trong file `.env` trên máy bạn.
Chúng **không bao giờ** lên GitHub: `dua-len-web.command` tự kiểm tra và dừng nếu thấy key trong code.

## Lần sau sửa app: đưa bản mới lên web

1. Lưu thay đổi bằng một commit (AI làm cho bạn; hoặc tự gõ `git commit -am "nội dung"`).
2. Nháy đúp **`dua-len-web.command`** (hoặc gõ `./dua-len-web.command` trong Terminal ở thư mục app).
3. Đợi 5–10 phút. Theo dõi trên dashboard.render.com → dịch vụ `giai-chuoi-huu-co` → **Events** (xong khi hiện *Deploy live*).

Chỉ bản **đã commit** được đưa lên. Script đẩy một "ảnh chụp" code sang GitHub, không kèm lịch sử cũ trên máy.

## Đổi key / cấu hình

dashboard.render.com → dịch vụ → **Environment** → sửa giá trị → **Save Changes**. Render tự khởi động lại app.

- **Đừng đổi `APP_SECRET_KEY`.** Đổi nó thì mọi người bị đăng xuất và key riêng đã lưu phải nhập lại.
- Token sao lưu (`SAO_LUU_GITHUB_TOKEN`) hết hạn thì tạo token mới (xem mục bên dưới) rồi dán vào Environment.

## Gửi email mã xác thực (Brevo – bắt buộc với gói Free)

Render Free **chặn gửi email kiểu SMTP (Gmail)**, nên app gửi mã xác thực qua **Brevo** (miễn phí 300 email/ngày):
1. Đăng ký tại brevo.com (gói Free).
2. Vào **Senders, Domains & Dedicated IPs → Senders → Add a sender**, nhập Gmail của bạn và bấm link xác minh Brevo gửi về Gmail.
3. Vào **SMTP & API → API Keys → Generate a new API key** (đặt tên `render`), rồi chép key `xkeysib-…`.
4. Trên Render → **Environment** → **Add Environment Variable**: đặt `BREVO_API_KEY` = key vừa chép (`EMAIL_FROM` = đúng Gmail đã xác minh) → **Save Changes**.

## Tài khoản admin

Đặt `CAP_ADMIN_EMAIL` = email của bạn trong Environment. Sau đó **đăng ký** trên web bằng đúng email đó. Nhập mã xác thực gửi về email xong là tài khoản thành admin.

## Tạo token sao lưu (khi cần làm lại)

1. github.com → ảnh đại diện → **Settings** → **Developer settings** → **Personal access tokens** → **Fine-grained tokens** → **Generate new token**.
2. Đặt tên `render-sao-luu`, chọn thời hạn (Expiration) dài nhất bạn muốn.
3. **Repository access** → *Only select repositories* → chọn `giai-chuoi-du-lieu`.
4. **Permissions** → **Contents**: *Read and write*.
5. Bấm **Generate token**, chép token rồi dán vào biến `SAO_LUU_GITHUB_TOKEN` trên Render. Không gửi token cho ai.

## Khác với khi chạy trên Mac

- Nút "Mở bằng ChemDraw" không có trên web vì ChemDraw chỉ có trên máy bạn. Người dùng vẫn tải được file `.cdxml`.
- Chế độ Demo bỏ bước tra Wikipedia bằng Chrome. Giải bằng AI vẫn tra cứu bình thường.
- Dữ liệu trên web tách khỏi dữ liệu trên máy Mac.

## Muốn nâng cấp sau này

Đổi `plan: free` thành `plan: starter` trong `render.yaml` và thêm ổ đĩa `/var/data` (khoảng 7 USD/tháng). Web hết ngủ, dữ liệu nằm ngay trên ổ đĩa.

## Các file liên quan (cho AI / người sửa code)

- `Dockerfile`, `.dockerignore`: đóng gói app.
- `render.yaml`: cấu hình dịch vụ Render.
- `backend/sao_luu.py`: sao lưu và khôi phục dữ liệu qua repo GitHub.
- `dua-len-web.command`: đưa code lên.
- Biến môi trường riêng cho máy chủ: `STORAGE_DIR`, `CHE_DO_CUC_BO=0`, `TRUSTED_PROXIES`, `APP_SECRET_KEY`, `SAO_LUU_*`, `CAP_ADMIN_EMAIL`.
