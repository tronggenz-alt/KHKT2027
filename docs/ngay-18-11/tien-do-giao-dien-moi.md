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

Commit ab8d18f9: hệ thống chung + ảnh trước + nguyên tắc (KhktUiVerify 54/54 đạt).

Commit (bước 2): ManChonLuong, MainMenu, DashboardShell, PageOverview, MenuKit (TextField) + ảnh "sau" — KhktUiVerify 54/54 đạt
(chạy từ bản kiểm riêng `scratchpad/wt/dong_bo.sh` vì tệp Hóa lý chưa commit của phiên khác đang làm dở, không biên dịch).

Bước 3 (commit cuối): bỏ số mờ 01/02, kiểm lần cuối 54/54 đạt, ảnh sau lần cuối, báo cáo `bao-cao-giao-dien-moi.md`.

## Còn lại
1. Dựng app Mac (`Khkt.EditorTools.MacBuild.Build`, ra ~/AppBuild/VERA_mac) và xem độ nét chữ trên Retina: hai lần dựng
   bị dừng từ bên ngoài (mã 144), chưa làm được.
2. Khi phiên Hóa lý commit xong: chạy lại KhktUiVerify từ thư mục chính (không cần bản kiểm riêng).

## Lệnh kiểm thử
```bash
export KHKT_UI_SHOTS=<thư mục ảnh>; ~/AppBuild/VERA_claude/dong_bo_va_chay.sh ~/Developer/Antigravity KhktUiVerify.Run ui_gd_moi
```
Kết quả: dòng `[UI_SUMMARY]` / `[UI_RESULT]` trong `$TMPDIR/ui_gd_moi.log`. Sau đó nếu `Lab/Materials/*.mat` hay
`docs/ngay-18-11/anh/g2..g4` bị đổi thì `git checkout --` chúng.
