# Tiến độ: 4 mô phỏng Hóa lý trong Unity (Scripts/HoaLy)

Nhánh n18/tich-hop. Lần sau tiếp tục: đọc tệp này trước.

## Đã xong
- Bước 1 – mô hình C# thuần (namespace Khkt.HoaLy, không using UnityEngine), dịch từ CHEMISTRY hoa-ly/*/mo-hinh.js, nhiet-dong.js:
  - `Scripts/HoaLy/MoHinh/KhiLyTuongMoHinh.cs` (pV=nRT, v_rms/v_p/v_tb, Van der Waals, quá trình, Maxwell, Dalton, Charles; HopKhi2D: hộp hạt cấp sẵn mảng)
  - `TocDoMoHinh.cs` (Arrhenius, bậc 0/1/2, Van 't Hoff, xúc tác, erfc, phân bố năng lượng; đọc phan-ung.json)
  - `CanBangMoHinh.cs` (Kc(T), Kp↔Kc, Q, chiều, ICE, Euler, giải thích Le Chatelier; HeCanBang tổng quát đọc he-can-bang.json)
  - `DongCoNhietMoHinh.cs` (Otto, Diesel, nhiên liệu, Carnot, đường p–v, trạng thái theo góc 0–720°)
  - `JsonHoaLy.cs`, `CauHoiHoaLy.cs` (3 dạng câu hỏi, chấm trả lời ngắn, tính lại "tinh_bang")
  - `HoaLySimTests.cs`: 72/72 đạt (chạy ngoài Unity bằng mono, lệnh bên dưới)
- Dữ liệu: `Assets/Resources/HoaLy/*.json` (4 cau_hoi_*, khi, phan_ung, he_can_bang, nhien_lieu)

## Đang làm / còn lại (theo thứ tự)
2. Lớp vẽ `LuoiVe` (MaskableGraphic, một mesh động: hạt, đường, khung) + `HoaLyMoDun` (static Mo/Dong/DangMo, lớp phủ camera riêng, tab 4 mô phỏng, Esc/Đóng)
3. 4 trang: TrangKhiLyTuong (hạt + Maxwell), TrangTocDo (C–t + phân bố E), TrangCanBang (C–t + màu ống), TrangDongCo (p–v + pít-tông); bảng Câu hỏi nhanh
4. `Scripts/HoaLy/KhktHoaLyRunner.cs` + `Scripts/Editor/KhktHoaLyVerify.cs`; chạy batchmode, chép ảnh vào docs/ngay-18-11/anh/hoa-ly/, tự xem ảnh
5. Chép .meta do Unity sinh từ bản sao về repo; viết docs/ngay-18-11/bao-cao-hoa-ly.md; commit

## Lệnh kiểm thử
Test thuần (không cần Unity), từ `Phong thi nghiem ao/Assets/Scripts/HoaLy/MoHinh` (Main.cs gọi HoaLySimTests.RunAll với 8 tệp JSON trong Resources/HoaLy):
    B=/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge
    $B/bin/mono $B/lib/mono/4.5/csc.exe -langversion:9 -out:/tmp/t.exe *.cs Main.cs && $B/bin/mono /tmp/t.exe ../../../Resources/HoaLy
Unity (sau bước 4): `pgrep -fl -- -batchmode` rỗng rồi
    ~/AppBuild/VERA_claude/dong_bo_va_chay.sh ~/Developer/Antigravity KhktHoaLyVerify.Run hoa_ly
