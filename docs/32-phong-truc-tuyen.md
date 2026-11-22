# Phòng học trực tuyến trên trình duyệt

Giáo viên tạo phòng trên web, học sinh vào bằng mã 6 ký tự hoặc mã QR. Giáo viên quản lý trực tiếp và nhận trạng thái của từng trình duyệt **ngay lúc xảy ra** qua WebSocket, không phải tải lại hay đợi lượt hỏi định kỳ.

Khác với "Theo dõi lớp" (`/truc-tiep`): trang đó theo dõi **app Unity** qua `POST /app/nhip` (2 giây một lần). Phòng trực tuyến chạy hoàn toàn trên trình duyệt, nên học sinh dùng được điện thoại hoặc máy tính của trường mà không cần cài app.

## Cách dùng

1. Giáo viên mở **Phòng trực tuyến** (`/phong-truc-tuyen`, menu Buổi học) và bấm **Tạo phòng**. Chọn ai được vào: mọi học sinh có mã, hoặc chỉ một lớp. Có thể bật "Cho khách vào" cho buổi giới thiệu, hội thi.
2. Học sinh mở `verlab.pages.dev/phong-hoc`, nhập mã hoặc quét mã QR. Học sinh có tài khoản thì vào bằng tài khoản. Khách thì nhập họ tên, chỉ khi giáo viên cho phép.
3. Giáo viên thấy từng em và điều khiển màn hình của các em (bảng dưới).
4. **Đóng phòng**: mọi học sinh ra ngoài, bản tổng kết được lưu và xem lại được trong 30 ngày.

## Trạng thái giáo viên nhận được

| Trạng thái | Lấy từ đâu trong trình duyệt |
|---|---|
| Đang trong phòng / đã rời phòng | WebSocket mở hay đóng. Mất mạng đột ngột: sau 60 giây không thấy `ping` thì coi như đã rời |
| Rời trang (chuyển tab, thu nhỏ, khóa máy), số lần, tổng thời gian | `document.visibilityState` |
| Trang đang mở nhưng đang dùng cửa sổ khác | `document.hasFocus()`, sự kiện `focus` / `blur` |
| Không thao tác hơn 1 phút | Không có `pointer`, `key`, `wheel`, `touch`, `scroll` trong 60 giây |
| Việc đang làm | Đang chờ, đang xem nội dung chiếu, đang làm câu hỏi, đã trả lời (chọn gì), đang bị khóa |
| Thiết bị, độ trễ mạng | Loại máy và trình duyệt lấy từ User-Agent. Độ trễ là thời gian khứ hồi của `ping`/`pong` |
| Giơ tay, câu trả lời | Học sinh bấm |
| Máy đã hiện nội dung chiếu, máy đã hiện màn khóa | Trình duyệt báo lại sau khi đã vẽ xong |

Trang học sinh ghi rõ giáo viên thấy những gì. Không dùng camera, micro, và không đọc được nội dung của tab khác.

## Giáo viên điều khiển

- **Chiếu lên màn hình học sinh**: chữ (tiêu đề, nội dung) hoặc một trang bài giảng đã tải lên ở trang Trình chiếu. Khi đang chiếu một bài, lật trang là chiếu luôn trang mới. Học sinh chỉ tải được ảnh của trang đang chiếu.
- **Khóa màn hình** cả phòng hoặc từng em: màn che toàn trang kèm lời nhắn.
- **Câu hỏi nhanh**: 2 đến 6 lựa chọn, có thể đánh dấu đáp án. Số trả lời tăng ngay trên trang giáo viên. Có hai nút: **Dừng nhận**, và **Công bố** (học sinh thấy đáp án cùng tỉ lệ cả phòng).
- **Nhắn** cả phòng hoặc nhắn riêng. Nếu em đó đang mất mạng, tin được giữ lại và gửi khi em vào lại.
- **Hạ tay**, **Mời ra** (có thể cấm vào lại), **Cho vào lại**.
- **Chế độ kiểm tra**: rời trang thành cảnh báo màu vàng, trang học sinh hiện dòng báo trước.
- **Khóa phòng**: không nhận thêm người. Người đã vào vẫn nối lại được khi rớt mạng.

## Kiến trúc

```
Trình duyệt GV ──WSS /api/phong/{id}/ket-noi──┐
                                              ├─ Pages Functions (kiểm đăng nhập, quyền, Origin) ─ binding PHONG ─► Durable Object PhongTrucTuyen
Trình duyệt HS ──WSS /api/vao-phong/ket-noi───┘                                                                      (mỗi phòng một đối tượng)
                                              D1: bảng phong_truc_tuyen (mã, cài đặt, bản tổng kết khi đóng)
```

- `cloudflare/phong/phong.ts`: Durable Object nằm trong Worker riêng `verlab-phong`, vì Pages Functions không tự định nghĩa được Durable Object. Worker này không có đường dẫn công khai (`workers_dev = false`), chỉ Pages gọi được vào qua binding.
- `cloudflare/src/phong_truc_tuyen.ts`: các API và việc chuyển tiếp WebSocket. Functions kiểm tra quyền rồi gửi người gọi sang Durable Object trong header `x-vera-nguoi`.
- Dùng WebSocket Hibernation: phòng "ngủ" khi không có tin, và `ping` được trả `pong` tự động mà không đánh thức phòng. Trạng thái nằm trong bộ nhớ SQLite của Durable Object, nên mỗi lần học sinh đổi trạng thái không tốn một lần ghi D1. Đủ dùng trong gói miễn phí.
- Mỗi người chỉ một kết nối: mở thêm tab thì tab cũ dừng, và việc chuyển tab không bị tính là rời phòng.
- An toàn:
  - mã phòng 6 ký tự (bỏ I, L, O, 0, 1); nhập sai 20 lần thì khóa theo IP;
  - WebSocket kiểm `Origin` để chặn trang khác dùng cookie của học sinh;
  - khách được nhận diện bằng mã khách ngẫu nhiên 144 bit do Durable Object cấp;
  - một trình duyệt gửi quá 40 tin trong 10 giây thì bị ngắt.

## Kiểm thử

```
cd cloudflare/phong && npx wrangler dev --port 8790      # cửa sổ 1
cd cloudflare && node lenh.mjs xay && npx wrangler pages dev --port 8788   # cửa sổ 2
node cloudflare/kiem_thu_phong.mjs http://127.0.0.1:8788
```

Ngày 21/11/2026, chạy cục bộ:

- `kiem_thu_phong.mjs` đạt **66/66**. Bộ này gồm quyền, rời tab, chiếu, khóa, câu hỏi, giơ tay, khách, mời ra, khóa phòng, đóng phòng và tổng kết. Từ lúc học sinh rời tab tới lúc trang giáo viên nhận được sự kiện mất **9 ms** (đo trên cùng một máy, chưa tính mạng Internet).
- `kiem_thu.py` (API cũ) vẫn đạt 68/68.
- Đã thử bằng Chrome thật: 1 giáo viên, 2 học sinh có tài khoản và 1 khách ở khung 390 px. Cả trang giáo viên lẫn trang học sinh đều không tràn ngang ở 390 px.

**Chưa đo:** trên verlab.pages.dev thật, với điện thoại thật và với cả lớp 30–40 em.

## Đăng lên mạng

Đăng Worker trước, rồi tạo bảng, rồi mới đăng Pages:

```
cd cloudflare/phong && npx wrangler deploy
cd .. && npx wrangler d1 execute verlab-db --remote --file csdl/luoc_do.sql   # chỉ CREATE ... IF NOT EXISTS, chạy lại vẫn an toàn
npx wrangler pages deploy --branch main --commit-dirty=true
node kiem_thu_phong.mjs https://verlab.pages.dev
```
