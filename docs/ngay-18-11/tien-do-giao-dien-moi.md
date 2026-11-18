# Tiến độ: làm mới giao diện ngoài lab (phiên "giao diện mới")

Đọc tệp này trước khi làm tiếp. Nhánh `n18/tich-hop`, chỉ `git add` từng tệp của việc này.

## Đã xong
- Phân tích CHEMISTRY (chạy ở trình duyệt, máy chủ tĩnh có đường `/static`) → `docs/ngay-18-11/thiet-ke-giao-dien-moi.md`.
- Ảnh "trước" (1440×900, 3 nền) → `docs/ngay-18-11/anh/giao-dien-moi/truoc_*.png`.
- Hệ thống chung:
  - `Scripts/Menu/Dashboard/MenuKit.cs` viết lại: sprite bo góc 2x (Round/RingSprite/CornerCap), bóng 2 lớp
    (ShadowSprite), viền tóc, ánh mép (Sheen), MenuGradient (dải màu theo góc), Backdrop (nền có chiều sâu, ảnh đục +
    vân chấm), Card/Surface/MakeCardClickable (nhấc 5 điểm khi rê), ImageFrame (bo góc ảnh không răng cưa),
    MakeButton mới (Bóng/Mặt/Viền/Sáng/Chữ, lò xo), Input (viền + quầng khi gõ), Tabs (vệt chọn trượt), Switch.
    MenuHover chạy bằng lò xo, bước con 1/240 s, không cấp phát. Bán kính tính bằng điểm (RadiusButton 12, Card 20, Large 28; ≤1 là viên nang).
  - `MenuAnim.cs`: thêm Ease.OutQuint/Away, Pop (hộp thoại), Leave (rời màn), Enter mặc định 0,45 s out-quint, Count chỉ tạo chuỗi khi số đổi.
  - `PageOverview.cs`: nút Phòng học chung đổi màu qua `MenuKit.Face(b)` (nút mới không còn Image ở gốc).
  - `KhktUiRunner.cs`: hàm chụp đợi `MenuAnim.Busy` hết (tối đa 3 s).

## Đang làm
- Kiểm biên dịch hệ thống chung bằng KhktUiVerify.Run.

## Còn lại (theo thứ tự)
1. ManChonLuong.cs: màn chọn 2 luồng (Backdrop, tiêu đề 60 đậm có bóng, 2 thẻ lớn dải màu navy/ngọc lục bảo, nhấc khi rê, mũi tên nhích), màn Giải bài tập (thẻ công cụ có ImageFrame, hàng Hóa lý), Cài đặt Giải bài tập (Pop, Tabs, Input).
2. MainMenu.cs: nền Backdrop, bỏ mảng tô cột trái/phải, mỗi màn trong thẻ Surface, MakeField → MenuKit.Input, nút Cài đặt góc dưới trái màn đăng nhập, cài đặt dùng Tabs + Switch, thẻ xem trước giao diện dùng viền tóc, màn chờ.
3. DashboardShell.cs + PageOverview.cs: nền Backdrop, thanh bên viền tóc, vệt chọn, thẻ mô-đun ImageFrame.
4. Chạy kiểm thử, xem ảnh, chép ảnh "sau" vào docs, viết `bao-cao-giao-dien-moi.md`, commit.

## Lệnh kiểm thử
```bash
export KHKT_UI_SHOTS=<thư mục ảnh>; ~/AppBuild/VERA_claude/dong_bo_va_chay.sh ~/Developer/Antigravity KhktUiVerify.Run ui_gd_moi
```
Kết quả: dòng `[UI_SUMMARY]` / `[UI_RESULT]` trong `$TMPDIR/ui_gd_moi.log`. Sau đó nếu `Lab/Materials/*.mat` hay
`docs/ngay-18-11/anh/g2..g4` bị đổi thì `git checkout --` chúng.
