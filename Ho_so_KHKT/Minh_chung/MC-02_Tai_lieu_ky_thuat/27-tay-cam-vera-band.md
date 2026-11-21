# Tay cầm VR "VERA Band"

Thiết kế ngày 09/11/2026, thay cho [VERA Arc](24-tay-cam-vera-arc.md). Tay cầm đeo vào tay bằng quai qua mu bàn tay: thả lỏng các ngón, tay cầm vẫn nằm trong lòng bàn tay. Chỉ dùng cho tay phải.

Giữ nguyên phần điện tử, đấu nối, 4 cổng mở rộng GPIO, cò Hall, mặt tối và chén lục giác của VERA Arc (xem [docs/24](24-tay-cam-vera-arc.md)). Phần đổi:

- Bỏ vòng tròn. Logo và chữ VERA LAB khắc trên tấm mu tay.
- Quai băng gai dính 20 mm: đầu trên luồn qua khuyên ở sườn đầu, đầu dưới luồn qua khe ở đế phình rồi gập ngược dán lại để chỉnh độ chặt.
- Cán dài thêm 14 mm cho đủ chỗ 3 ngón giữa, áp út, út của tay người lớn.
- Bỏ thanh móc dây cổ tay vì quai đã giữ tay cầm.
- Ruột rộng hơn để dễ đặt linh kiện: vách 1,5 mm, thân nới ra 0,5 mm mỗi bên, đế phình rỗng ruột. Linh kiện cách mặt trong vỏ ít nhất: pin 1 mm, ESP32 1,8 mm, mạch hạ áp 1,8 mm, mạch joystick 5 mm. Lỗ lọt pin 28,5 × 19,5 mm, dư 1 mm mỗi bên (đo bằng `do_khoang_ho.py`).

Kích thước cả bộ 78 × 78 × 122 mm, kể cả quai. Khối lượng ước tính khoảng 141 g có pin và quai (51 g nhựa), chưa cân thật.

Xem 3D: <https://claude.ai/artifact/2hv9Ykzdohf8bgpFX1dF7g>

## Gói gửi tiệm in

[`docs/tay-cam-band/VERA_Band_STL.zip`](tay-cam-band/VERA_Band_STL.zip) gồm 5 file STL, gộp theo màu nhựa, và 1 file kích thước:

| File | Chi tiết trong file | Màu | Nhựa |
| --- | --- | --- | --- |
| `than.stl` | 2 nửa thân | Trắng | 34,7 g |
| `day_tren.stl` | Đáy trên (mặt tối bầu dục) | Đen | 7,4 g |
| `day_duoi.stl` | Đáy dưới (nắp khoang pin) | Đen | 1,5 g |
| `chi_tiet_xanh.stl` | Tấm mu tay có logo, chén lục giác | Xanh ngọc huỳnh quang | 3,9 g |
| `chi_tiet_bac.stl` | Cò, 4 nắp nút phụ | Bạc hoặc xám | 3,9 g |
| `VERA_Band_kich_thuoc.pdf` | Hình chiếu 1:1, kích thước từng phần, đối chiếu cỡ tay, hướng in | | |

In PDF đúng 100% thì hình bằng kích thước thật; trang 1 có thước 50 mm để kiểm.

## Đối chiếu với cỡ tay người Việt trưởng thành

| Hạng mục | Tay cầm | Số đo bàn tay | Kết luận |
| --- | --- | --- | --- |
| Đường kính cán | Eo 40,8 × 29,8 mm, tương đương Ø35,5 | Tối ưu 19,7% chiều dài bàn tay: Ø30,1 (nữ P5) đến Ø38,8 (nam P95) | Giữa dải, tay nhỏ thấy hơi to |
| Vùng nắm cho 3 ngón dưới | 47 mm | 3 ngón: nữ TB 45,8, nữ P95 51,8, nam P95 ≈ 54,7 | Vừa tay trung bình; tay to thì ngón út tì lên đế phình |
| Quai | Đường quai trên mô hình 145 mm | Độ dày bàn tay: nữ P5 32,2 đến nam P95 ≈ 42,5 | Cần 137–158 mm, cắt 240 mm để còn phần gập dán và khâu |
| Tấm mu tay | 32 × 40 mm | Bề rộng gan bàn tay nữ P5 68,4 mm | Không cấn mép bàn tay |

Nguồn số đo:

- Tay nam: Imrhan, Nguyen & Nguyen (1993), trích trong Zheng và cs., AHFE 2018, <https://openaccess-api.cms-conferences.org/articles/download/978-1-4951-2105-0_32>.
- Tay nữ: Bùi Văn Huấn, Đỗ Thị Hoa Ngà, *Nghiên cứu xây dựng hệ thống cỡ số bàn tay nữ sinh viên Hưng Yên phục vụ thiết kế găng tay da*, Tạp chí Khoa học và Công nghệ 140 (2020) 25–30, đo 270 nữ sinh viên, <https://storage.googleapis.com/jst-journals/articles/140/005-19024.pdf>.
- Đường kính tay nắm tối ưu: Kong & Lowe (2005), <https://stacks.cdc.gov/view/cdc/197408>.

P5, P95 tính bằng trung bình ∓ 1,645 độ lệch chuẩn. Chưa có số đo độ dày bàn tay và bề rộng ngón tay của nam người Việt, nên số có dấu ≈ được ước tính từ tay nữ theo tỉ lệ bề rộng bàn tay nam/nữ.

## Mã nguồn

`tools/tay_cam_band/`, chạy theo thứ tự:

1. `mo_hinh_band.py <thư mục xem>`: dựng 3D, xuất 5 file STL.
2. `kiem_va_cham.py`: kiểm va chạm, hành trình cò, khe quai ở đế và ở tấm mu tay.
3. `ban_kich_thuoc.py`: tạo file kích thước HTML và PDF (in bằng Edge chạy ẩn).
4. `dong_goi.py`: kiểm từng STL, nén gói gửi in.
5. `trang.py <thư mục xem>`: tạo trang xem 3D.

## Vật tư thêm so với VERA Arc

- Quai băng gai dính 2 mặt rộng 20 mm, dài 240 mm.
- Xốp mỏng 1–2 mm dán mặt trong tấm mu tay.
- Bỏ vít M2 × 16 và 2 vít M2 × 8 giữ vòng. Vít ghép thân vẫn 6 cái M2 × 10 và 1 cái M2 × 8.

## Đeo vào tay

1. Khâu hoặc dán gập đầu quai thành vòng qua thanh ngang của khuyên trên.
2. Luồn quai xuống dưới tấm mu tay qua 2 khe của tấm.
3. Đưa tay phải vào giữa tay cầm và quai, cầm như bình thường.
4. Luồn đầu dưới quai qua khe ở đế từ trên xuống, gập ngược lên rồi dán gai cho vừa tay.

## Chưa xác minh

- **Độ thoải mái** chưa thử trên tay thật. Khuyên trên chỉ cách gốc ngón cái mô phỏng 0,42 mm, tay to có thể chạm.
- **Bàn tay mô phỏng** chỉ gần đúng, nên số đo quai là ước tính. Cắt dư rồi tỉa sau.
- **Tấm mu tay** in PLA cứng; nếu xưởng có TPU thì in tấm này bằng TPU sẽ êm hơn.
- Các mục chưa xác minh của VERA Arc vẫn còn: kích thước linh kiện, ray nắp pin, thời lượng pin, MediaPipe khi đang đeo.
