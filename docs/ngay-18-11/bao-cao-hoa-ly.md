# Báo cáo: 4 mô phỏng Hóa lý trong Unity (18–19/11/2026)

Hàng **Hóa lý** của màn Giải bài tập giờ mở được 4 mô phỏng, viết lại từ app web CHEMISTRY
(`frontend/hoa-hoc/hoa-ly/`). MainMenu gọi `Khkt.HoaLy.HoaLyMoDun.Mo(ma)` qua reflection như cũ,
nên không phải sửa `Scripts/Menu`.

| Mã | Mô phỏng | Phần vẽ |
|---|---|---|
| `khi_ly_tuong` | Khí lý tưởng, Van der Waals | Bình có pít-tông, hạt chuyển động theo phân bố Maxwell (màu theo tốc độ); cột đếm tốc độ hạt chồng lên đường Maxwell lý thuyết; vạch v_p, v_tb, v_rms |
| `toc_do` | Tốc độ phản ứng | Đồ thị C–t chạy theo con trỏ thời gian, so với mốc 298 K; phân bố năng lượng có phần E ≥ Ea tô màu và đuôi phóng to 10ⁿ |
| `can_bang` | Cân bằng – Le Chatelier | Đồ thị nồng độ cửa sổ 40 s có mốc tác động; ống nghiệm đổi màu; thang Q–K (log) có mũi tên chiều dịch; lời giải thích |
| `dong_co_nhiet` | Động cơ nhiệt (Otto, Diesel) | Xi-lanh – pít-tông – trục khuỷu chạy 0–720°; chu trình p–v có chấm trạng thái và diện tích công; thanh q_in = w + q_out và η so với η Carnot |

Mỗi mô phỏng có thanh trượt tham số, ô số liệu sống, và mục **Câu hỏi nhanh**: 9 câu mỗi mô phỏng,
lấy từ `cau-hoi.json`, đủ ba dạng (trắc nghiệm, đúng/sai 4 ý, trả lời ngắn có chấm số).

## Cấu trúc

- `Scripts/HoaLy/MoHinh/`: phần tính, C# thuần, không dùng UnityEngine.
  - `KhiLyTuongMoHinh.cs`, `TocDoMoHinh.cs`, `CanBangMoHinh.cs`: dịch từ các tệp `mo-hinh.js`.
  - `DongCoNhietMoHinh.cs`: dịch từ `nhiet-dong.js`.
  - `HopKhi2D`: hộp hạt, mảng cấp sẵn.
  - `HeCanBang`: hệ cân bằng tổng quát đọc từ `he-can-bang.json`. Nó giải Q(ξ) = K bằng chia đôi rồi tiến về cân bằng theo hàm mũ.
  - `CauHoiHoaLy.cs`, `JsonHoaLy.cs`.
  - `HoaLySimTests.cs`: 72 kiểm tra.
- `Scripts/HoaLy/`: phần giao diện.
  - `HoaLyMoDun` (static `Mo`/`Dong`/`DangMo`) và `HoaLyManHinh`: lớp phủ có camera riêng và canvas 1920×1080, 4 tab, cột Điều khiển / Câu hỏi nhanh, nút Đóng và phím Esc. Khi mở, lớp phủ tắt các camera khác và tăng `DesktopViewer.UiCapture`, rồi trả lại khi đóng. Nó cũng đóng lớp phủ Cơ chế và Mô hình 3D nếu đang mở.
  - `LuoiVe`: một `MaskableGraphic` vẽ cả trang bằng một mesh động (hạt, đoạn thẳng, dải tô, tứ giác). Bộ đệm cấp sẵn, không cấp phát mỗi khung.
  - `DoThi`: khung đồ thị. Chuỗi số trục chỉ tạo lại khi vạch chia đổi.
  - `TrangKhiLyTuong`, `TrangTocDo`, `TrangCanBang`, `TrangDongCo`, `BangCauHoi`.
- Dữ liệu: `Assets/Resources/HoaLy/*.json`, gồm 4 tệp câu hỏi, `khi`, `phan_ung`, `he_can_bang`, `nhien_lieu`.
- Giao diện dùng GlassKit (màu theo chủ đề sáng/tối/VERA, phông Be Vietnam Pro), có hiệu ứng:
  - khi mở: mờ dần và trượt lên;
  - khi đổi tab: trang trượt vào, gạch dưới tab chạy theo;
  - trục đồ thị, pít-tông và nồng độ đổi mượt.
- Số hạt = n/5 × `HardwareProfile.SoHatToiDa`. Ở các mức Thấp, Vừa, Cao, Siêu nét, giới hạn lần lượt là 200, 500, 1500, 3000 hạt.

## Kiểm thử

```bash
~/AppBuild/VERA_claude/dong_bo_va_chay.sh ~/Developer/Antigravity KhktHoaLyVerify.Run hoa_ly
```

`Scripts/Editor/KhktHoaLyVerify.cs` mở cảnh trống rồi vào PlayMode. `KhktHoaLyRunner` chạy các kiểm tra và thoát; mã 0 là đạt.

**Kết quả: 59/59 đạt, 0 lỗi lúc chạy, 13 ảnh.** Các kiểm tra chính:

- **Test C# thuần:** 72/72 đạt. Mô hình C# tính lại đúng cả 12/12 đáp số dạng III (`tinh_bang`) của CHEMISTRY.
- **Mở và đóng lớp phủ:**
  - Mở qua reflection đúng như MainMenu.
  - Cả 4 mã chọn đúng tab; mã lạ thì về tab đầu.
  - Khi mở, khóa điều khiển lab và tắt camera cảnh. `Dong()` trả lại cả hai; mở lại sau khi đóng vẫn được.
- **Khí lý tưởng:**
  - Kéo thanh trượt thì p = nRT/V đúng.
  - Boyle: V ×2 thì p ÷2.
  - T ×2 thì v_rms đo trên hạt ×√2 (1,4142). v_rms đo khớp √(3RT/M) trong khoảng 1 %.
  - Số hạt ≤ SoHatToiDa ở cả 4 mức đồ họa.
  - CO₂ nén còn 0,5 L cho Z = 0,801 < 1.
  - Cấp phát trung bình 1,6 KB/khung, chủ yếu do Text của Unity (xem ghi chú bên dưới).
- **Tốc độ phản ứng:**
  - k = A·e^(−Ea/RT).
  - Tăng 10 K thì k ×1,9243 (Arrhenius).
  - Xúc tác tăng k đúng bằng exp(ΔEa/RT).
  - Tại t½ nồng độ còn C₀/2.
- **Cân bằng:**
  - Hệ N₂O₄ tự về Q = K, [NO₂] = 0,01247 so với 0,01248 theo bảng ICE.
  - Đun nóng hệ tỏa nhiệt: K giảm, hệ dịch nghịch thật, NO₂ tăng.
  - Nén ×2: Q = K/2, hệ dịch thuận.
  - Thêm NO₂: dịch thuận.
  - H₂ + I₂ ⇌ 2HI (Δn = 0): nén không làm dịch.
  - Haber: nén làm NH₃ tăng hơn mức chỉ do nồng độ ×2. Tăng T thì K giảm, hệ dịch nghịch.
  - Pha loãng Fe³⁺ + SCN⁻: dịch nghịch.
  - Xúc tác không làm đổi K.
- **Động cơ nhiệt:**
  - Otto r = 10: η = 60,2 %.
  - Quét r = 4–22 và q_in = 200–2500: η luôn ≤ η Carnot. Diesel quét rc = 1,1–3,5 cũng luôn ≤ Carnot.
  - Diện tích chu trình p–v (tích phân số) bằng w.
  - Cùng r thì Diesel kém Otto.
  - Chọn isooctane thì q_in tính từ LHV 44,43 MJ/kg.
- **Câu hỏi nhanh:** cả 4 bộ đều chấm đúng 9/9 câu trả lời đúng và nhận ra 9/9 câu trả lời sai.

Ảnh (1920×1080, tôi đã tự xem từng ảnh) nằm trong `docs/ngay-18-11/anh/hoa-ly/`:
`khi_ly_tuong`, `khi_ly_tuong_nong`, `toc_do`, `toc_do_zn_xuc_tac`, `can_bang_dun_nong`, `can_bang_haber`,
`can_bang_fe_scn`, `dong_co_otto`, `dong_co_diesel`, `cau_hoi_*` (4 ảnh).

Lần chạy đầu: 54 đạt, 5 sai. Đã sửa các lỗi sau:

- **Cân bằng chưa kịp hội tụ.** Trong batchmode, dt bị kẹp ở 0,05 s nên thời gian mô phỏng chậm hơn thời gian thực. Runner nay đợi theo thời gian mô phỏng và tua nhanh ×3.
- **Cấp phát 3,7 KB/khung.** Số trục tạo chuỗi mới mỗi khung khi trục co giãn mượt. Nay chỉ tạo khi vạch chia đổi.
- **Lỗi nhìn thấy trên ảnh:**
  - chú thích đè nhãn trục;
  - thiếu số trục;
  - công thức hiện "NO2" thay vì NO₂, Fe³⁺;
  - hai đường trùng nhau che nhau;
  - trục khuỷu đè xi-lanh;
  - phần đuôi E ≥ Ea không thấy được.

## Ghi chú, giới hạn, việc còn lại

- **Phần chưa chuyển:** chỉ chuyển phần lõi của từng mô phỏng. Chưa có các phần sau của bản web:
  - cháy hữu hạn Wiebe, cơ cấu trục khuỷu chi tiết (`co-cau.js`, `dong-luc.js`);
  - thư viện công thức `cong-thuc.json` và các hình chứng minh;
  - các quá trình đẳng nhiệt, đẳng áp từng bước của trang khí (mô hình C# đã có `QuaTrinh`, chỉ chưa có nút).
- **Hộp hạt là 2D, bỏ qua va chạm giữa các hạt** (đúng giả thiết khí lý tưởng). Vận tốc được lấy mẫu 3D để biểu đồ tốc độ khớp Maxwell 3D.
- **Diện tích bề mặt Zn** (viên ×1, lá mỏng ×6, bột ×40) là hệ số minh họa nhân vào A, không lấy từ số liệu đo.
- **Mô phỏng cân bằng tiến về nghiệm cân bằng theo hàm mũ** (τ = 1,6 s, xúc tác τ/5), không tích phân động học thật cho mọi hệ. `BuocTichPhan` của bản JS vẫn được dịch và test hội tụ đúng nghiệm ICE.
- **Cấp phát còn ~1,6 KB/khung.** Phần vẽ hạt và đồ thị không cấp phát, phần còn lại do Unity dựng lại Text/canvas (không có khung nào bằng 0). Nếu cần về 0 thì phải bỏ hẳn việc đổi chữ trong khung.
- **Script `~/AppBuild/VERA_claude/dong_bo_va_chay.sh`** đợi bằng `pgrep -f "Unity.*-batchmode"`. Mẫu này khớp cả dòng lệnh shell của các phiên Claude khác, nên có thể đợi mãi. Nên đổi sang `pgrep -f "Contents/MacOS/Unity -batchmode"`. Script cũng rsync **trước** khi đợi, nên ghi đè lên bản sao mà Unity khác đang chạy.
- **Thứ tự nền tảng:** đã kiểm trên Mac. Mã không dùng API hay đường dẫn riêng của Mac (Windows chạy được). Giao diện bấm bằng chuột/cảm ứng như phần còn lại của app (iPad sau cùng).

## Commit (nhánh n18/tich-hop)

- `111dd800`: mô hình C# thuần và test, dữ liệu JSON
- `fa9574b0`: lớp phủ, 4 trang, câu hỏi nhanh, runner
- `f69b44f2`: các sửa sau lần chạy Unity đầu
- `faef9f7c`: các tệp .meta
- (commit cuối): đuôi phân bố năng lượng, ảnh, báo cáo, tiến độ
