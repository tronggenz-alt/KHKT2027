# Thiết kế giao diện mới ngoài lab (18/11)

Nguồn tham khảo: app web CHEMISTRY (`~/CHEMISTRY_xem/frontend`: style.css, chuyen-dong.js, chuyen-canh.js,
giao-dien.js, themes/) chạy thử trong trình duyệt tích hợp ở 1440×900, cùng lối thiết kế quen thuộc của các app
hiện đại cùng loại (Linear, Vercel, Raycast, Notion, Apple Settings): nền trung tính, viền tóc 1 px, bóng rất
mềm, một màu nhấn, chữ đậm có thứ bậc rõ, chuyển động ngắn và có quán tính.

## Vì sao giao diện cũ trông "nhựa"

- Thẻ là mảng tô đặc trên nền đặc, gần như không có viền; bóng là một lớp mờ đều, đậm ở nền tối nên thẻ trông như
  miếng nhựa dán lên.
- Nút phụ làm viền bằng cách chồng hai mảng tô (mảng màu viền + mảng nền nhỏ hơn 3 px): viền dày, đục, góc không đều.
- Nền trang phẳng một màu, không có chiều sâu.
- Rê chuột chỉ phóng to 1–2 %: thiếu cảm giác thẻ "nổi lên".
- Chuyển màn chỉ mờ cả khối; thẻ không hiện lần lượt.

## Nguyên tắc rút ra

### Màu
- Nền trung tính kiểu zinc: Trắng `#f4f4f5` (thẻ `#ffffff`), Đen `#09090b` (thẻ `#18181b`), Xanh mực giữ hệ màu
  VERA nhưng sâu hơn (`#07101d`, thẻ `#0e1a2c`).
- Viền tóc 1 px: Trắng `rgba(0,0,0,.08)`, Đen `rgba(255,255,255,.08)`, Xanh mực `rgba(120,190,255,.14)`.
  Viền là nét chính để tách khối, không phải bóng.
- Một màu nhấn duy nhất cho thao tác; màu xanh lá/hổ phách chỉ để phân loại (vạch, nhãn), không tô cả thẻ.
- Thẻ màu đậm (kiểu thẻ lớn của CHEMISTRY) dùng dải màu chéo 145° từ màu đậm tới gần đen, chữ trắng.

### Bán kính
- Thẻ lớn 28 px, thẻ thường 18 px, nút/ô nhập 12 px, chip và nhãn dạng viên nang 999 px.
- Ảnh trong thẻ bo theo thẻ trừ đi phần đệm (bán kính lồng nhau: trong = ngoài − đệm).

### Bóng nhiều lớp
- Hai lớp: lớp sát (ambient, lệch 1–2 px, mờ 4 px, đậm 4–6 %) + lớp rơi (lệch 8–20 px, mờ 24–40 px, đậm 6–12 %).
- Rê chuột: lớp rơi tăng lên ~2×, thẻ nhấc lên 4–6 px (CHEMISTRY: `translateY(-5px)`,
  `0 20px 40px -15px rgba(0,0,0,.12)`), viền chuyển sang màu nhấn.
- Nền tối: bóng gần như không thấy, nên thay bằng viền sáng hơn + ánh sáng mép trên (1 px trắng 4–6 %).

### Nền có chiều sâu
- Gradient rất nhẹ từ trên xuống và một quầng màu nhấn mờ ở góc (độ đậm ≤ 6 %), thêm lưới chấm/vân rất mờ.
- Phải là ảnh đục (alpha = 1) để không thành lớp phủ bán trong suốt làm xám giao diện.

### Khoảng cách
- Lưới 8 px: 8 / 12 / 16 / 24 / 32 / 48 / 64. Đệm thẻ lớn 32–40 px, thẻ thường 24 px.
- Lề trang 64–96 px ở 1920×1080.

### Chữ
- Be Vietnam Pro. Tiêu đề màn 44–64 px đậm, chữ hơi khít; tiêu đề thẻ 22–30 px đậm; thân 15–17 px thường;
  nhãn phụ ("eyebrow") 12–13 px đậm, IN HOA, giãn chữ, màu nhạt hoặc màu nhấn.
- Ba mức màu chữ: mực (chính), mờ (phụ), câm (chú thích). Không dùng quá 3 cỡ trong một thẻ.

### Chuyển động (token theo chuyen-dong.js)
| Việc | Thời lượng | Đường cong |
|---|---|---|
| Nhấn nút | 80–100 ms xuống 0,97, nhả bằng lò xo | lò xo cứng (stiffness 550, damping 30) |
| Rê chuột thẻ | 220–280 ms | `cubic-bezier(.22,1,.36,1)` (out-quint) |
| Hiện thẻ lần lượt | 450 ms, trễ 50–70 ms/thẻ, trượt 10–16 px | out-quint |
| Chuyển màn | vào 350–420 ms trượt + mờ, ra 200 ms | vào `soft (.215,.61,.355,1)`, ra `away (.33,.17,.67,.5)` |
| Hộp thoại | 400 ms, từ 0,92 → 1 | soft + mờ 200 ms |
| Vệt chọn tab | lò xo selection (300/30) | |
| Số đếm | 700–900 ms | out-cubic |

- Giảm chuyển động: nhảy thẳng tới trạng thái cuối (giữ nguyên chức năng).
- Chỉ đổi vị trí, tỉ lệ, alpha, màu: không dựng lại lưới/văn bản trong lúc chạy.

## Áp vào Unity (uGUI, dựng bằng mã)

- Bóng hai lớp = hai Image 9-slice dùng sprite bóng mềm tự tạo (một lần), đặt sau thân thẻ.
- Viền tóc = sprite chỉ có đường viền 1,5 px khớp bán kính (`GlassKit.Border`), đặt trên thân; không dùng
  `Outline` (vẽ lại cả ô bốn lần, làm xám) và không chồng hai mảng tô.
- Ánh sáng mép trên (nền tối) = sprite viền chỉ sáng ở nửa trên.
- Lò xo: tích phân bán ẩn theo `Time.unscaledDeltaTime`, bước con cố định để không phụ thuộc FPS.
- Mọi chuyển động chạy qua một `MenuAnim` duy nhất; không cấp phát trong `Update`.
