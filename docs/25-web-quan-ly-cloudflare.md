# 25. Web quản lý chạy cố định trên verlab.pages.dev

Trang giới thiệu và web quản lý (đăng nhập giáo viên, học sinh; lớp; kết quả; trình chiếu; theo dõi lớp; buổi học; kho Hóa; phòng Sinh; trợ lý AI) cùng chạy trên Cloudflare, **gói miễn phí**, không cần bật máy tính hay tunnel. Nút **Đăng nhập** trên trang giới thiệu dẫn tới `/quan-ly`.

Bản FastAPI (`server/`) vẫn giữ nguyên để chạy cục bộ, làm phương án dự phòng.

## Cấu trúc

| Thành phần | Nơi chạy | Mã |
|---|---|---|
| Trang giới thiệu, giao diện web (`server/web/`) | Tệp tĩnh Cloudflare Pages | `cloudflare/lenh.mjs xay` gộp vào `cloudflare/dist/` |
| API (`/auth`, `/api`, `/app`, `/lab`, AI) | Pages Functions, TypeScript | `cloudflare/src/` |
| Cơ sở dữ liệu | D1 `verlab-db` (SQLite của Cloudflare) | `cloudflare/csdl/luoc_do.sql` |

Đường dẫn, mã lỗi và dữ liệu trả về giữ đúng như bản FastAPI. Bộ kiểm thử qua HTTP: `python cloudflare/kiem_thu.py <địa chỉ>`.

## Khác với bản FastAPI (do giới hạn của gói miễn phí)

1. **Băm mật khẩu bằng PBKDF2-SHA256, 100.000 vòng, muối 16 byte** (bản FastAPI dùng Argon2id). Workers không chạy được Argon2 và chỉ cho tối đa 100.000 vòng PBKDF2, thấp hơn mức OWASP khuyên (600.000). Mật khẩu tạm do máy sinh (10 ký tự ngẫu nhiên, bắt buộc đổi ở lần đăng nhập đầu) chỉ băm 200 vòng, để tạo cả lớp 200 học sinh trong một lượt gọi. Khi đổi mật khẩu, máy chủ băm lại đủ 100.000 vòng.
2. **PDF bài giảng được chuyển thành ảnh 1600×900 ngay trên trình duyệt của giáo viên** (`server/web/tinh/chuyen_trang.js`, dùng pdf.js), rồi ảnh được lưu trong D1. Máy chủ chỉ nhận ảnh JPEG hoặc PNG.
3. **Không có lối riêng 127.0.0.1 cho Unity**: trên mạng, mọi lời gọi của app phải đăng nhập (Bearer).
4. **Nhịp app lưu trong bảng `nhip` của D1**, vì mỗi lượt gọi có thể chạy trên một máy khác nên không giữ được trong bộ nhớ. Nếu không có gì đổi, máy chủ chỉ ghi 4 giây một lần.
5. Khóa API của mạng lưới khóa AI nằm trong D1 và được nhập trên web, như bản FastAPI. Khóa Claude, nếu dùng, đặt bằng `wrangler pages secret put ANTHROPIC_API_KEY`. Không khóa nào nằm trong git.

## Hạn mức miễn phí và sức chứa

| Hạn mức (tài liệu Cloudflare, tra ngày 2026-11-09) | Ý nghĩa |
|---|---|
| 100.000 lượt gọi Functions mỗi ngày, đặt lại lúc 07:00 giờ Việt Nam | Tệp tĩnh không tính. Mỗi nhịp app (2 giây) là một lượt. |
| D1: 100.000 dòng ghi, 5 triệu dòng đọc mỗi ngày; tối đa 500 MB | Mỗi nhịp có thay đổi ghi một dòng |
| 10 ms CPU mỗi lượt gọi | Xem số đo bên dưới |

Ước tính: một lớp 30 máy học 45 phút, mỗi máy gửi nhịp 2 giây, tốn khoảng 40.000 lượt gọi. Như vậy mỗi ngày đủ khoảng 2 tiết đầy lớp. Con số này là phép tính từ hạn mức, chưa đo với 30 máy thật.

## Số đo (2026-11-09)

- Băm PBKDF2 100.000 vòng mất 64 ms CPU trên laptop của nhóm (Node 24). Trên Cloudflare thật, 8/8 lần đăng nhập liên tiếp đều thành công, không bị lỗi vượt CPU. Mình chưa đọc được số ms CPU chính xác từ log của Cloudflare. Nếu sau này gặp lỗi 1102 (vượt CPU), hạ `VONG` trong `cloudflare/src/tai_khoan.ts`.
- Thời gian một lần đăng nhập đo từ máy ở Việt Nam: 396–975 ms, phần lớn là thời gian chờ D1.
- `kiem_thu.py`: đạt 68/68 bài trên `wrangler pages dev`, 69/69 trên bản xem trước trên Cloudflare (dữ liệu thử đã xóa sau đó), 30/30 bộ nhẹ trên https://verlab.pages.dev.
- Chưa thử trên điện thoại thật. Chưa thử với app Unity trỏ vào verlab.pages.dev.

## Thao tác

```bash
cd cloudflare
npm install
npm run dang                                   # dựng và đăng lên verlab.pages.dev
node lenh.mjs tao-giao-vien gv.ten "Họ Tên" --remote   # mật khẩu tạm in ra một lần, phải đổi ở lần đăng nhập đầu
node lenh.mjs csdl --remote                    # tạo bảng và dữ liệu ban đầu (chạy lại vẫn an toàn)
```

`wrangler` dùng tài khoản Cloudflare của thành viên đang giữ dự án `verlab`. Tài khoản thử trên mạng là gv.thu1, gv.thu2, hs.thu1–3; mật khẩu nằm trong `data/tai_khoan_mau_mat_khau.json` (git bỏ qua). Mã lớp thử đã đóng.
