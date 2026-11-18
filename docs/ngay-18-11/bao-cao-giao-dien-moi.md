# Báo cáo: làm mới giao diện ngoài lab (18–19/11)

Nguyên tắc thiết kế rút ra từ CHEMISTRY: `thiet-ke-giao-dien-moi.md`. Tiến độ từng bước: `tien-do-giao-dien-moi.md`.
Ảnh trước/sau (1440×900, 3 nền): `anh/giao-dien-moi/truoc_*.png`, `anh/giao-dien-moi/sau_*.png`.

## Vì sao trước đây trông "nhựa"
- Thẻ là mảng tô phẳng trên nền phẳng, bóng một lớp đều; nút phụ và ô nhập làm viền bằng cách chồng hai mảng tô
  (viền dày, đứt ở mép trái — thấy rõ ở ô nhập và nút "Đổi" trong ảnh `truoc_dang_nhap_Trang`).
- Rê chuột chỉ phóng to 1–2 %, thẻ không "nổi lên"; ảnh bo góc bằng Mask stencil nên góc răng cưa.

## Đã làm
**Hệ thống chung** (`Scripts/Menu/Dashboard/MenuKit.cs`, `MenuAnim.cs`):
- Sprite bo góc vẽ một lần ở 2x cho Retina (khối đặc, viền tóc 1,25 điểm, phần ngoài góc để bo góc ảnh), bán kính tính bằng
  điểm: nút 12, thẻ 20, thẻ lớn 28, viên nang.
- Thẻ: bóng hai lớp (rơi 30 điểm + sát 4 điểm), thân, viền tóc, ánh mép trên ở nền tối. `MakeCardClickable`: rê chuột thẻ nhấc
  4–6 điểm, bóng đậm và rơi xa hơn, viền chuyển màu nhấn (như thẻ lớn CHEMISTRY).
- Nút: Bóng / Mặt / Viền / Sáng / Chữ; nút chính có dải sáng dọc và viền sáng mép trên; rê thì lớp sáng hiện, nhấn co 0,97.
- Lò xo (`MenuHover`, `MenuSwitch`, `MenuFocusRing`): tích phân bán ẩn, bước con 1/240 s theo `Time.unscaledDeltaTime`
  (không phụ thuộc FPS), không cấp phát mỗi khung, tự ngừng khi đứng yên. Giảm chuyển động → nhảy thẳng trạng thái cuối.
- `MenuGradient`: dải màu theo góc kiểu CSS bằng màu đỉnh lưới (giữ mép khử răng cưa của sprite).
- `Backdrop`: nền có chiều sâu (dải dọc rất nhẹ + hai quầng màu nhấn ≤ 10 % + vân chấm), ảnh đục nên không thành lớp phủ làm xám.
- `ImageFrame`: ảnh phủ kín khung, bốn góc phủ màu thẻ (thay Mask stencil), viền tóc.
- `TextField` (viền chuyển màu nhấn + quầng 3 điểm khi gõ), `Tabs` (thanh phân đoạn / danh sách dọc có vệt chọn trượt),
  `Switch` (công tắc viên nang, núm có bóng), `Tag`, `Eyebrow`, `TextShadow`.
- `MenuAnim`: thêm đường cong out-quint (≈ `cubic-bezier(.22,1,.36,1)`) và away, `Pop` cho hộp thoại (0,96 → 1),
  `Leave` cho rời màn (mờ + chìm 200 ms), `Enter` mặc định 0,45 s; số đếm chỉ tạo chuỗi khi con số đổi.
- Không dùng `Outline` hay chồng mảng tô để làm viền ở bất kỳ màn nào đã đổi.

**Các màn**:
- Màn chọn 2 luồng: nhãn chào, tiêu đề 60 đậm có bóng chữ, hai thẻ lớn dải màu chéo (navy / ngọc lục bảo) không ảnh, chữ trắng 48,
  nút "Vào ngay →" trắng có mũi tên nhích khi rê; các phần hiện lần lượt.
- Giải bài tập: thanh trên (logo, vạch, nhãn), thẻ công cụ có ảnh bo góc lồng, nút chính có mũi tên, hàng Hóa lý thẻ nhỏ bấm cả thẻ.
  Cài đặt Giải bài tập: hộp thoại nở ra, thanh phân đoạn Thông tin/API, ô nhập mới.
- Đăng nhập / Đăng ký / Đổi mật khẩu / Phòng học chung / Màn chờ: nền chiều sâu, mỗi màn trong một thẻ nổi, ô nhập mới;
  nút **Cài đặt ở góc dưới trái** màn đăng nhập (theo bản phác). Màn chờ: thanh tiến độ viên nang dải màu, phần trăm lớn.
- Cài đặt app: đặt giữa cả màn (cột phải chỉ ~950 điểm ở màn 16:10 nên trước đó tràn), danh sách mục có vệt chọn trượt,
  thẻ xem trước giao diện viền tóc, công tắc lò xo, cỡ chữ / cỡ bảng là thanh phân đoạn.
- Bảng điều khiển: nền chiều sâu, thanh bên viền tóc, vệt chọn có vạch màu nhấn trượt out-quint, thẻ phòng thí nghiệm dải màu
  (bỏ ảnh dưới lớp màu che bằng Mask), thẻ mô-đun ảnh bo góc khử răng cưa và bấm được cả thẻ.

**Kiểm thử**: `KhktUiRunner` đợi `MenuAnim.Busy` hết (tối đa 3 s) trước khi chụp. KhktUiVerify: **54/54 đạt**, 0 lỗi lúc chạy, 3 nền.

## Ghi chú
- Phạm vi: chỉ `Scripts/Menu/**` (gồm `KhktUiRunner.cs`) và tài liệu. Không sửa GlassKit/UiTheme: hai tệp này dùng chung cho
  bảng trong lab (vát góc kiểu máy đo ở nền Xanh mực); MenuKit có bộ hình riêng nên giao diện ngoài lab đổi mà lab không bị ảnh hưởng.
- Lúc kiểm thử, thư mục chính có tệp Hóa lý chưa commit của phiên khác đang làm dở (thiếu `TrangTocDo`, `TrangCanBang`,
  `TrangDongCo`) nên cả dự án không biên dịch khi đồng bộ từ thư mục chính. Tôi chạy kiểm thử từ bản sao riêng ở scratchpad
  (bỏ các tệp đó). Khi phiên Hóa lý xong, cần chạy lại KhktUiVerify từ thư mục chính.
- Ảnh batchmode dựng ở màn ảo 640×480 nên chữ nhỏ có thể hơi nhòe; độ nét đánh giá trên app Mac (mục dưới).
- Biểu tượng thanh bên và ô chỉ số dùng phông Segoe MDL2 (chỉ có trên Windows); trên Mac các ô biểu tượng trống như trước.

## Độ nét trên app Mac (19/11)
- Dựng từ **mã đã commit** (`git archive HEAD` sang bản kiểm sạch, `Khkt.EditorTools.MacBuild.Build`): Succeeded, 0 lỗi,
  403 s, 247 MB → `~/AppBuild/VERA_mac/VERA Lab.app`. (Hai lần dựng trước đó bị dừng từ bên ngoài, mã 144.)
- Mở app trên màn Liquid Retina 2880×1864, chụp ở độ phân giải gốc: `anh/giao-dien-moi/app_mac_man_chon.png` (thu còn
  1600 rộng) và `app_mac_the_lon_retina_goc.png` (cắt 1:1 điểm ảnh gốc).
- Kết quả: chữ nét ở mọi cỡ, kể cả nhãn 12–13 điểm và dấu tiếng Việt; viền tóc và góc bo của thẻ, nút "Vào ngay" mịn,
  không răng cưa; bóng thẻ mềm, không bị cắt; nền dải màu không thành bậc.
- Chuyển động: ảnh chụp cách nhau 1 giây nên chỉ thấy trạng thái cuối (các phần hiện lần lượt xong trong ~0,75 s). Hiệu ứng
  rê chuột và nhấn chưa được xem tận mắt trên app thật trong phiên này (không điều khiển chuột); chuyển động đã kiểm qua
  KhktUiVerify (đợi `MenuAnim.Busy`, không còn nhóm mờ dở khi chụp).
- Không xóa cài đặt đã lưu của app; app đóng bằng lệnh thoát bình thường.

## Commit
- `ab8d18f9`: hệ thống chung MenuKit/MenuAnim, ảnh trước, nguyên tắc thiết kế.
- `9c6ce2ba`: áp lên mọi màn, ảnh sau.
- `cc987299`: bỏ số mờ trên thẻ lớn, ảnh sau lần cuối, báo cáo.
- Commit cuối: kết quả app Mac, ảnh app thật.
