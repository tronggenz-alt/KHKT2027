# Báo cáo: làm lại nhánh "Giải bài tập" – 19/11/2026

Khảo sát trước khi sửa: `khao-sat-giai-bai-tap.md` (bố cục CHEMISTRY từng mục + 14 lỗi của bản cũ).
Ảnh trước/sau: `anh/giai-bai-tap/truoc_*.png`, `anh/giai-bai-tap/sau_*.png` (1440×900, nền Trắng; trình phát 1920×1080).

## 1. Giao diện mới (bố cục CHEMISTRY, chủ đề VERA)

Tệp mới `Scripts/Menu/GiaiBaiTap/`:
- `KhungTrangGbt.cs` – khung trang chung: trang cuộn dọc lề cố định, **thanh phụ dính** (nút `‹ tên công cụ` + các
  **bước ngang** 01/02/03, bấm thì cuộn tới mục, bước đang xem tô màu nhấn), đầu trang (nhãn nhỏ, tiêu đề 64, mô tả),
  mục đánh số (số màu nhấn, tiêu đề 38, dòng phụ), thẻ tab lớn chọn-một, ô nhập nhiều dòng, chip, huy hiệu, nút `?`.
- `ManGiaiBaiTap.cs` – màn Giải bài tập (thay lớp cũ trong `ManChonLuong.cs`):
  - **Thanh điều hướng trên**: Trang chủ · Chuỗi phản ứng · Cơ chế · Mô hình 3D · Hóa lý; phải là Cài đặt, ← Màn chính.
  - **Trang chủ**: 3 thẻ lớn như CHEMISTRY (ô biểu tượng, nhãn "01 · …", tên, mô tả, hàng chip, mũi tên nhích khi rê):
    Chuỗi (trắng), Cơ chế (dải navy), Mô hình 3D (dải đen); thẻ ngang **Hóa lý**; chân trang.
  - **Chuỗi phản ứng**: `01 Nhập đề` 3 tab Văn bản / Hình ảnh / Chuỗi mẫu (ô nhiều dòng, Chèn →, Xoá; chọn ảnh + xem
    trước; 3 chuỗi mẫu bấm là giải) · `02 Tuỳ chọn giải` (Mức độ đề Tự động/Trung bình/HSG, Mức thông minh
    Nhanh/Cân bằng/Kỹ/Tối đa, công tắc Tra cứu tài liệu, nút `?` giải thích, thẻ "Lời giải") · nút lớn **Giải chuỗi →**
    + trạng thái · `03 Kết quả` (trạng thái trống như CHEMISTRY; có kết quả: huy hiệu kiểm chứng, **Mở tất cả / Thu gọn**,
    **Sao chép**, **Báo sai**, mỗi bước một thẻ gập/mở: phương trình, loại, điều kiện, chất tạo thành, nút
    **Xem cơ chế 3D ⇄ Giấy** khi có cơ chế mẫu).
  - **Cơ chế**: `01 Nhập đề` 3 tab Văn bản (gợi ý đề như CHEMISTRY) / Hình ảnh / **Bài mẫu** (lưới 8 bài, bấm mở ngay) ·
    `02 Kiểu cơ chế` (11 lựa chọn như ô "Loại cơ chế" của CHEMISTRY, gửi `loai` lên `/co-che/giai-de`) · **Vẽ cơ chế →** ·
    `03 Kết quả` (huy hiệu nguồn, tên, kiểu + điều kiện, **danh sách bước** bấm để nhảy tới bước trong trình phát, nút
    Mở 3D ⇄ Giấy, Giải thích chi tiết, Hỏi đáp, Báo sai).
  - **Mô hình 3D**: ô chất + 8 chip mẫu + **Dựng mô hình 3D →** → mô-đun 3D (`/co-che/mo-hinh-3d`).
  - **Hóa lý**: 4 thẻ ngang (Khí lý tưởng, Tốc độ, Cân bằng, Động cơ nhiệt) → `Khkt.HoaLy.HoaLyMoDun.Mo(ma)`.
  - **Hộp Báo sai** dùng chung (trang Chuỗi, trang Cơ chế, trình phát).
- Cài đặt Giải bài tập giữ đúng 2 mục Thông tin + API.

Trình phát Cơ chế 3D ⇄ Giấy (`CoCheMoDun.cs`):
- **Bảng bên** thay ô Giải thích cũ: 3 tab **Các bước** (bấm dòng → nhảy bước, dòng đang xem tô sáng) /
  **Giải thích** (chi tiết từ máy chủ, tải nền khi mở bài; không có máy chủ thì dùng lời soạn sẵn) /
  **Hỏi đáp** (giữ hội thoại, gửi kèm bài + các bước + lịch sử).
- Nút **Xuất ảnh PNG** (chụp tờ giấy gấp đôi độ phân giải màn hình → `~/Pictures/VERA Lab/co_che_<mã>_<giờ>.png`),
  **Báo sai**, nút `← Quay lại` (về đúng trang Giải bài tập). Nút trên nền giấy đổi màu để đọc được.
- Chữ trên nút không còn in đậm cỡ nhỏ; ảnh kiểm thử dựng lại chữ theo cỡ ảnh (hết nhòe chữ trong ảnh chụp).
- Hàm mới: `DamBaoCo()`, `MoVoiBai(bai, tab, buoc)`, `MoMau(ma)`, `GhiDeDaGiai(bai)`, `MoBangBen(i)`, `XuatAnhGiay()`.

## 2. Máy chủ (`server/ai/co_che/hoi_dap.py`, `api.py`)

| API | Việc |
|---|---|
| `POST /co-che/giai-thich-chi-tiet` | tổng quan + từng bước (giữ đúng số bước) + lưu ý; tương đương `/api/co-che/giai-thich` của CHEMISTRY |
| `POST /co-che/hoi-dap` | hỏi đáp theo bài đang xem, có lịch sử (tương đương `/api/co-che/chat`) |
| `POST /co-che/bao-sai` | ghi `data/bao_sai/<co_che|chuoi|mo_hinh_3d>.jsonl` (đã thêm vào .gitignore) |
| `POST /co-che/giai-chuoi` | thêm `anh_base64` (AI chép chuỗi trong ảnh rồi giải), `muc_do`, `muc_thong_minh`, `tra_cuu` |

AI: Claude trước, Gemini dự phòng (dùng lại `giai_de.goi_ai_mac_dinh`). Không có AI → giải thích theo ghi chú đã kiểm chứng,
hỏi đáp qua RAG tại máy, cuối cùng là tóm tắt theo bài; kết quả ghi rõ `nguon` để app báo.
Thử thật với máy chủ đang chạy (khóa Claude vẫn 401): `/co-che/hoi-dap` trả lời bằng gemini-flash-lite-latest sau 13 s;
`/co-che/giai-thich-chi-tiet` trả đủ bước sau ~105 s (Gemini pro chậm – app tải nền nên không chặn người dùng).

## 3. Kiểm tra

- Python: 76 test `tests.test_co_che_*` đạt (mới: `tests/test_co_che_hoi_dap.py` – giữ đúng số bước, thiếu khóa,
  lịch sử hỏi đáp, ghi báo sai + chặn `nguon` lạ, đọc chuỗi từ ảnh, `/co-che/giai-chuoi` bằng ảnh).
- Unity (bản sao `~/AppBuild/VERA_claude`, batchmode có đồ họa):
  - KhktUiVerify **66/66** – viết lại phần Giải bài tập: điều hướng 5 mục, 3 thẻ + Hóa lý, trang Chuỗi (tab, bước ngang,
    tuỳ chọn), giải chuỗi mẫu 4 bước, Mở tất cả → nút Xem cơ chế, Báo sai, trang Cơ chế 8 bài mẫu + 11 kiểu,
    Vẽ cơ chế gửi đúng `loai`, kết quả có danh sách bước, Giải thích chi tiết mở trình phát ở tab Giải thích,
    Mô hình 3D, Hóa lý 4 mô phỏng, Cài đặt Thông tin + API.
  - KhktCoCheVerify **59/59** – thêm: bảng bên đủ dòng bước, bấm dòng nhảy bước, giải thích chi tiết, xuất PNG,
    hộp báo sai + gửi.
  - KhktHoaLyVerify **59/59**.
- App Mac: xem mục 4.

## 4. App Mac thật

- `Khkt.EditorTools.MacBuild.Build` thành công (0 lỗi, 247,5 MB) → `~/AppBuild/VERA_mac/VERA Lab.app`.
- System Events không bấm chuột được vào Unity, nên thêm phím tắt (cũng tiện cho người dùng Mac):
  màn đầu **G** = Giải bài tập, **L** = Phòng Lab; trong Giải bài tập **1–5** chuyển trang, **Esc** về Trang chủ
  (không bắt phím khi đang gõ vào ô chữ hoặc khi lớp phủ 3D/Báo sai đang mở).
- Mở app, gửi phím G: vào đúng trang chủ Giải bài tập mới – ảnh `anh/giai-bai-tap/app_mac_trang_chu.png`
  (màn Retina, thu còn 1600 rộng). Ngay sau đó macOS bật hộp thoại xin quyền ghi màn hình cho Claude và giữ tiêu điểm
  phím, nên các phím 2, 3 không tới app; tôi không bấm vào hộp thoại đó. Ảnh các trang còn lại lấy từ KhktUiVerify /
  KhktCoCheVerify (cùng mã, kết xuất 1440×900 và 1920×1080).

## 5. Còn lại / giới hạn

- CHEMISTRY có "Mở bằng ChemDraw" và tải CDXML: VERA chưa có ChemDraw nên chưa làm; thay bằng Sao chép lời giải và
  Xuất ảnh PNG sơ đồ giấy.
- Tuỳ chọn "Mức độ đề / Mức thông minh / Tra cứu" được gửi lên máy chủ và trả lại trong kết quả, nhưng `giai_chuoi.giai`
  chưa dùng chúng để đổi cách giải (bảng đã kiểm chứng giải như nhau) – cần nối vào phần AI giải bước mới.
- Chọn ảnh đề trên iPad chưa có (nút ghi "sắp có"), theo thứ tự ưu tiên Mac > Windows > iPad.
- Khóa Claude trong .env vẫn 401; mọi tính năng AI đang chạy bằng Gemini.

## 6. Bổ sung chiều 19/11: Cơ chế – giấy trước, khung 3D nhỏ ⇄ toàn màn hình, góc nhìn tự chọn

Yêu cầu: chỉnh chuyển động/góc camera cho dễ thấy electron; thanh tốc độ kéo liên tục; khi giải bài hiện **bản giấy trước**,
nút **Xem 3D** bật **khung nhỏ phía dưới** chỉ có 3D + thanh điều khiển, nút **phóng to** ra toàn màn hình (cũng chỉ 3D +
điều khiển, có nút thu nhỏ), nút **Quay lại bảng giấy**; áp dụng cho 8 bài mẫu và đề mới.

Đã làm (`CoChe/TrinhPhat3D.cs`, `CoChe/CoCheMoDun.cs`):
- **Góc nhìn tự chọn mỗi bước** (`TinhGocNhin`): chấm điểm 160 hướng nhìn rải đều trên mặt cầu – mũi tên electron phải nằm
  ngang mặt nhìn (thấy trọn đường bay), nguyên tử tham gia không bị nguyên tử khác che và không chồng nhau, ưu tiên gần góc
  đang nhìn và hơi nhìn từ trên xuống. Camera xoay mượt tới góc đó (giảm dần theo hàm mũ, ~1 s), đồng thời tự căn **tâm và
  khoảng cách** ôm các chất của bước. Kéo chuột / cuộn thì tắt tự động tới bước sau.
- **Electron chậm hơn**: một bước 4,2 s ở 1× (trước 2,6 s), electron bay ≈ 2,3 s.
- **Thanh tốc độ** kéo liên tục 0,25× – 3× (hiện số, ví dụ 1.00×) thay cho nút 3 mức.
- **Luồng giấy trước**: mở bài (mẫu hoặc đề mới, từ trang Giải bài tập hay phím M) là tờ giấy; nút **▶ Xem 3D** ở góc dưới
  phải tờ giấy. Bấm: tờ giấy thu lên phía trên (0,6×), **khung 3D 1040×450** trượt hiện ở giữa đáy màn: đầu khung ghi
  "Bước n/N tên bước", nút **Phóng to** và **Quay lại bảng giấy**; thanh dưới có ‹ Phát/Dừng ›, thanh tiến trình theo bước
  (bấm đoạn để nhảy bước), **Tốc độ**, Ẩn H. **Phóng to** giãn khung ra toàn màn hình trong 0,3 s (nút đổi thành **Thu nhỏ**).
  Đề bài, bảng bên, khung bước cũ không hiện trong khung 3D.
- Kỹ thuật: camera 3D riêng vẽ vào RenderTexture bằng cỡ màn, chỉ vẽ đúng phần ô xem (`Camera.rect` + `RawImage.uvRect`) nên
  nét 1:1 và đổi khung nhỏ ⇄ toàn màn không cấp phát lại; vẫn một camera gốc URP cho giao diện, nhãn nguyên tử (Br, ⊕ ⊖) đè
  đúng chỗ trong khung; kéo xoay chỉ nhận khi chuột ở trong ô xem.
- Phím tắt (Mac): **V** = Xem 3D ⇄ bảng giấy, **F** = phóng to ⇄ thu nhỏ, Space phát/dừng, ← → chuyển bước.
- Nút "Bài khác ▾" chuyển lên hàng nút phải (đọc được trên nền giấy); bấm Báo sai khi đang xem 3D thì về giấy rồi mở hộp.

Kiểm tra:
- KhktCoCheVerify **66/66** – thêm: mở bài là giấy (khung 3D đóng); nút Xem 3D bật khung nhỏ ở đáy (rect ~0,54 × 0,30 màn);
  Phóng to → ô 3D phủ bề ngang màn; Thu nhỏ về khung; thanh tốc độ 0,25×–3×; góc tự chọn – mũi tên không bị nhìn dọc trục
  (≥ 80% mũi tên); Quay lại bảng giấy tắt khung và camera 3D. KhktUiVerify **66/66** (không đổi).
- App Mac xuất lại thành công; mở app thật, phím M → tờ giấy (Thế gốc tự do CH₄ + Cl₂), V → khung 3D nhỏ dưới tờ giấy,
  Space phát, F → toàn màn hình: `anh/giai-bai-tap/app_mac_co_che_*.png` (góc trên phải che thông báo hệ thống).
  Ảnh kiểm thử: `sau_trinh_phat_luong_giay_truoc.png`, `sau_trinh_phat_khung_3d_nho.png`, `sau_trinh_phat_khung_3d_toan_man.png`.
- Sau một lần xuất app, 14 tệp `Lab/Materials/*.mat` trong kho chính bị đổi: đã lưu `git diff --binary` vào
  `~/Developer/Antigravity-luu-ban-cu/mat_sau_build_co_che_3d_*.diff` rồi `git checkout --`.

Còn lại: ở bước "chất mới tham gia", khung nhìn ôm cả chất sắp bay vào nên lúc đầu phân tử đang có lệch về một phía
(đúng chỗ dành cho chất bay tới, nhưng có thể cần chỉnh nếu thấy khó nhìn).

## 7. Bổ sung tối 19/11: bo góc vừa phải, công tắc gọn, chỉnh tỉ lệ

Góp ý: bo tròn quá (công tắc viên thuốc, mục bước nền tròn) nhìn xấu; bo vừa thôi và chỉnh tỉ lệ bố cục.
- Bán kính riêng cho nhánh Giải bài tập (`TrangCuon.BoNho/BoNut/BoThe/BoTheLon` = 8/12/16/20 đơn vị canvas 1920 rộng,
  ≈ 6/9/12/15 px ở màn 1440): nút, chip, mục điều hướng, chip loại cơ chế = 12; mục bước ngang và nút quay lại = 10;
  thanh phụ = 14; thẻ = 16; thẻ công cụ lớn và hộp Báo sai = 20; huy hiệu = 8. Không còn dạng viên thuốc tròn hẳn.
- Công tắc gọn `TrangCuon.CongTac`: rãnh 42×24 bo 12, núm 18 (trước 52×30 và 24), núm chạy ±9; MenuSwitch thêm `travel`
  (mặc định 11 như cũ, các màn khác không đổi).
- Tỉ lệ: tiêu đề trang 64 → 54, tiêu đề mục 38 → 32, đầu trang/mục thấp lại; thẻ Tuỳ chọn 236 → 200, công tắc sát nhãn;
  nút `?` 22.
- Phím 1–5 / V / F chỉ bị chặn khi ô nhập ĐANG HIỆN có con trỏ (trước đó ô đăng nhập đã ẩn vẫn có thể chặn phím).
- Kiểm tra: KhktUiVerify 66/66; ảnh `sau_*.png` cập nhật; app Mac xuất lại, trang chủ Giải bài tập thật:
  `app_mac_trang_chu_bo_vua.png`. Gửi phím bằng System Events vào app lần này không ổn định (lúc nhận, lúc không), nên
  các trang trong app thật chưa chụp lại được; ảnh các trang lấy từ KhktUiVerify (cùng mã).

Chụp lại app thật (bản có bo góc mới): kích hoạt cửa sổ app rồi gửi **mã phím** (key code) thay vì ký tự – bộ gõ tiếng Việt
đã nuốt phím số khi gửi ký tự. Đủ 5 trang: `app_mac_that_1_chu.png`, `_2_chuoi`, `_3_co_che`, `_4_3d`, `_5_hoa_ly`.

## 8. Luồng đáp án kiểu trợ lý AI (19/11, theo `thiet-ke-luong-dap-an.md`)

Tệp mới `Scripts/Menu/GiaiBaiTap/DapAn.cs`:
- **DongThoiGian** – mục "Kết quả" của mỗi trang thành dòng thời gian: mỗi lần giải là một **khối** (nhãn "Lần giải n",
  bong bóng câu hỏi có ảnh đề thu nhỏ, thẻ đáp án). Khối mới nối xuống cuối, khối cũ giữ làm lịch sử (tối đa 20). Chưa giải
  lần nào thì hiện thẻ trống có hướng dẫn.
- **KhoiDapAn** – đang giải: **khung xương** 3 thanh nhấp nháy + dòng **tiến trình từng bước** (✓ xong, ● đang làm, mờ = chưa,
  đếm giây). Có đáp án: **hình** đứng đầu → kết luận một dòng + huy hiệu kiểm chứng → tóm tắt → hàng nút → chip gợi ý tiếp;
  các phần hiện lần lượt cách 60 ms. **Hỏi tiếp** mở ô hỏi ngay dưới thẻ (`/co-che/hoi-dap`, gửi kèm đề và các bước).
- **Trượt mượt**: bấm Giải → trượt tới khối mới; đáp án về → trượt tới đầu hình, **trừ khi người dùng đã tự cuộn đi**
  (0,45 s, ease-out bậc 3, theo `unscaledDeltaTime`, không cấp phát mỗi khung).
- **BangLon** – bảng 75% × 80% giữa màn, nền mờ, đóng bằng ✕ / Esc / bấm nền, mở: mờ dần + phóng nhẹ.
- **HinhDapAn** – hình đáp án vẽ bằng uGUI (nét, không cần máy chủ vẽ): **chuỗi** = ô chất – mũi tên có điều kiện – ô chất,
  tự xuống dòng (bảng lớn thêm tên chất); **3D tĩnh** = bóng–que chiếu từ góc đẹp, xa vẽ trước gần sau, màu CPK, ánh sáng;
  **giấy** = SoDoGiay véc-tơ trên nền giấy.

Theo từng mục:
- **Chuỗi**: khối có hình chuỗi; nút Mở lớn / Giải thích (bảng lớn: hình chuỗi lớn + từng bước, bước có cơ chế mẫu có nút
  Xem cơ chế), Hỏi tiếp, Xuất PNG, Báo sai; chip "Xem cơ chế: A → B", "Vì sao … cần …?".
- **Cơ chế**: vẽ đề mới hoặc mở bài mẫu đều thành khối có sơ đồ giấy; Mở lớn = bảng lớn (tờ giấy lớn + nút **Xem 3D ⇄ Giấy**
  mở khung 3D/thanh tốc độ/phóng toàn màn đã làm ở mục 6 + danh sách bước bấm để nhảy bước); Giải thích (trình phát, tab Giải
  thích), Hỏi tiếp, Xuất PNG, Báo sai; chip "Xem electron chạy trong 3D".
- **Mô hình 3D**: Dựng → khối có ảnh 3D tĩnh, công thức, M, số nguyên tử, SMILES, nguồn; Mở lớn = khung xoay 3D (MoHinh3DMoDun).
- **Xuất PNG** chung: chụp đúng vùng hình bằng camera của canvas ở 2× độ phân giải → `~/Pictures/VERA Lab/`.

Kiểm tra: KhktUiVerify viết lại phần Giải bài tập – giải chuỗi có hình 5 ô chất, giải lần 2 thêm khối dưới + trang tự trượt,
Mở lớn chuỗi có nút Xem cơ chế, đóng bảng, hỏi tiếp dưới thẻ (máy chủ giả), báo sai; cơ chế có sơ đồ giấy, Mở lớn có Xem 3D,
Giải thích mở tab Giải thích; 3D có 9 nguyên tử ethanol, Mở lớn mở khung xoay. Kết quả xem mục 9.

Giới hạn: bảng lớn của Cơ chế chứa tờ giấy + nút mở khung 3D (khung 3D vẫn là lớp phủ của trình phát), chưa nhúng 3D thẳng vào
bảng 75%; ô đề vẫn ở trên trang (thiết kế cho phép "trên hoặc dưới"), chưa dính đáy.

## 9. Kết quả kiểm tra mục 8

- KhktUiVerify **72/72**, KhktCoCheVerify **66/66**; app Mac xuất thành công.
- Ảnh: `anh/giai-bai-tap/dap_an_*.png` (khối đáp án chuỗi, lịch sử 2 lần giải, bảng lớn chuỗi, hỏi tiếp, khối cơ chế có sơ đồ
  giấy, bảng lớn cơ chế, khối 3D tĩnh).
- App thật: không có phím tắt để bấm "Giải" (cần chuột; System Events không bấm chuột được vào Unity) nên chưa chụp khối đáp án
  trong app thật; mã giống hệt bản kiểm thử.

## 10. Chỉnh chu thêm (tối 19/11)

- **3D kiểu CHEMISTRY (3Dmol/PubChem)**: ảnh 3D trong thẻ đáp án nay là `Anh3DXoay` – nền sáng, bóng–que cầu 0,27 × bán kính
  van der Waals, que 0,14 Å **hai nửa màu theo nguyên tử**, bảng màu CHEMISTRY (C #2c3038, O #e63946, N #3a86ff…), H trắng có
  viền, nhãn nguyên tố (trừ H) trên nền giấy mờ, **tự xoay quanh trục đứng 24°/s** (không cấp phát mỗi khung). Khung xoay 3D
  (Mở lớn) cũng chuyển sang nền sáng, kiểu CHEMISTRY, tự xoay (`TrinhPhat3D.KieuChemistry`, `TuXoay`; trình phát Cơ chế giữ kiểu cũ).
- **Ô đề dính đáy** ở Chuỗi, Cơ chế, Mô hình 3D (như ô chat): gõ + Enter hoặc nút chính là giải, khung cuộn chừa chỗ.
- **Phím Giải**: Enter khi không gõ / ở ô một dòng; ⌘ hoặc Ctrl + Enter ở ô nhiều dòng.
- **3D ngay trong bảng lớn Cơ chế** (`Xem3DNho`): nút Giấy | 3D, ‹ Phát/Dừng ›, thanh tốc độ 0,25×–3×, Toàn màn hình (mở
  trình phát ở đúng bước), bấm bước → 3D tới bước đó; đóng bảng thì giải phóng camera và RenderTexture.
- Chip gợi ý không còn gãy 2 dòng.
- Kiểm tra: KhktUiVerify **77/77** (thêm: ô đề dính đáy thêm khối, 3D trong bảng vẽ nguyên tử và phát, đóng bảng giải phóng,
  ảnh 3D kiểu CHEMISTRY); app Mac xuất được.
- **Chưa xong (TẠM DỪNG vì hạn mức 64%, mốc dừng 70%)**: ảnh kiểm thử của khung 3D trong bảng lớn vẫn gần như tối (đo được
  phân tử ở RenderTexture nhưng rất ít điểm ảnh, ảnh chụp menu không thấy rõ). Cần kiểm tiếp: khoảng cách camera/khung hình của
  `Xem3DNho` và hiển thị RawImage; xem trong app thật bằng chuột.

### 10b. Sửa khung 3D trong bảng lớn (làm tiếp, người dùng cho chạy tới 85%)
- Nguyên nhân khung "đen": RenderTexture lấy theo điểm ảnh màn lúc tạo (màn batchmode 640×480 → 315×341, quá thô) và camera
  đứng quá xa vì công thức căn khung nhân thêm khi khung gần vuông → phân tử chỉ vài chục điểm ảnh.
- Sửa: RenderTexture rộng cố định 1600 theo đúng tỉ lệ khung (MSAA 4×); khoảng cách camera căn theo chiều hẹp của khung
  (`max(1, 1/tỉ lệ)`, áp cho cả trình phát Cơ chế nên phân tử trong khung nhỏ/toàn màn cũng to hơn); thêm đèn riêng làm đèn
  chính (`RenderSettings.sun`) khi bảng mở và nguyên tử tự phát sáng nhẹ 30% để không bao giờ tối đen; đóng bảng trả đèn cũ.
- Kiểm: KhktUiVerify 77/77; nội dung RenderTexture khi vẽ: `anh/giai-bai-tap/dap_an_co_che_bang_lon_3d_noi_dung.png`
  (thấy rõ CH₃Br, OH⁻, electron). Ảnh chụp menu ở batchmode vẫn thấy khung tối vì ở batchmode camera không tự vẽ theo vòng lặp
  (cùng lý do các kiểm thử Cơ chế phải gọi Render() thủ công); app thật dùng cùng cách với khung 3D nhỏ của Cơ chế đã hiện đúng.
  Chưa xem được bảng lớn trong app thật vì phải bấm chuột.

## 11. Theo góp ý khi test app (20/11)

- **3D hiện ngay dưới đáp án Cơ chế**: khối đáp án = tờ giấy → khung 3D tự phát (‹ Phát/Dừng ›, thanh tốc độ, kéo để xoay) →
  **Lời giải** bằng chữ (theo giai đoạn: Khơi mào / Phát triển mạch…, mỗi bước "n. tên – ghi chú"). Chỉ khối mới nhất giữ trình
  phát chạy; khối cũ có nút "Xem lại 3D" (không tốn nhiều camera khi có lịch sử dài).
- **Sơ đồ giấy**: bỏ tiêu đề có gạch lượn sóng trong hình (lời giải đưa xuống phần chữ); chữ có chỉ số dưới/trên (H₃C, Br⁻)
  dùng phông của app vì phông Times của máy thiếu ký tự chỉ số → trước đây lệch.
- **Ít nút**: Cơ chế = Hỏi tiếp · Xem 3D toàn màn hình; Chuỗi = Xem chi tiết từng bước · Hỏi tiếp; Mô hình 3D = Xoay 3D toàn màn
  hình. "Xuất ảnh · Báo sai" thành liên kết chữ nhỏ bên phải. Bỏ chip câu hỏi, bỏ "Giải thích" trùng lặp.
- **Hết chồng đè**: khối "Lần giải" từng đè lên ô nhập (trang Mô hình 3D) vì hiệu ứng trượt giữ vị trí cũ khi trang xếp lại →
  khối và thẻ chỉ còn hiệu ứng mờ dần.
- **Bo góc vừa phải toàn app**: dạng viên thuốc (RadiusPill) nay bo 12, thẻ 16, thẻ lớn 20 (MenuKit, dùng chung mọi màn).
- **Thanh kéo**: rãnh mảnh 4 điểm + núm tròn đều 18 điểm có viền màu nhấn (trước bị kéo thành ellipse).
- **3D kiểu CHEMISTRY**: mặt phẳng phân tử (vd benzen) quay chính diện, lắc ±40° thay vì xoay hết vòng (không bao giờ nhìn
  ngang mép), cầu nhỏ hơn; camera 3D căn theo khung chữ nhật của phân tử sau khi xoay (khung dẹt không còn đứng quá xa).
- Kiểm: KhktUiVerify 76/76, KhktCoCheVerify 66/66, app Mac xuất được.
- **Chưa làm** (cần sửa phía máy chủ dựng sơ đồ `so_do_giay`): công thức dài vẽ dạng khung (đường gấp khúc, giữ N, O, nhóm
  như NO₂) và chỉnh vị trí đầu/đuôi mũi tên cong cho khỏi đè chữ.

## 12. Góp ý test lần 2 (20/11)
- Núm thanh tốc độ **tròn đều 20×20** (Slider của Unity kéo neo dọc của núm theo vùng chứa → vùng núm đặt cao 0), có viền màu
  nhấn, mặt trắng, bóng nhẹ.
- Đáp án Cơ chế chỉ còn **lời giải trên giấy như bài thi** (sơ đồ + mũi tên), bỏ tóm tắt và phần chữ; nút **▶ Xem 3D** mở khu 3D
  ngay dưới (bấm lại "▲ Ẩn 3D" để đóng); khung 3D **bo góc 14** (Mask theo hình bo); Toàn màn hình nằm trong thanh điều khiển 3D.
  Tờ giấy trong trình phát cũng bỏ tiêu đề gạch lượn sóng.
- **Electron**: hạt nhỏ hơn (0,12), hai hạt của cặp cách xa hơn (0,3 Å) → phân biệt rõ 1 hay 2 e; quầng vàng nhỏ riêng từng hạt
  (1,7× thay 3×, mờ hơn); vệt đuôi mảnh; cặp xoay chậm (2,6 thay 7,5 rad/s); đường bay tăng–giảm tốc dạng sin (êm hơn).
  Ảnh: `anh/giai-bai-tap/electron_moi_hai_hat.png`.
- Kiểm: KhktUiVerify 77/77, KhktCoCheVerify 66/66, app Mac xuất được.

## 13. Electron đơn giản, mũi tên cong làm lại, dạng khung (20/11)
- **Electron**: chỉ còn chấm vàng nhỏ bay theo đường cong từ chỗ cho đến chỗ nhận (cặp e = 2 chấm cố định hai bên đường bay,
  tách theo mặt màn hình nên không bao giờ chồng nhau); bỏ xoay, quầng sáng, vệt đuôi.
- **Mũi tên cong** (`SoDoGiay.MuiTenCong`): độ cong tối thiểu 35% chiều dài (mũi tên ngắn không còn thành móc), chừa hở 5 điểm ở
  đuôi và 7 điểm ở đầu để không đè chữ, thân trơn 40 đoạn, đầu **tam giác đặc theo tiếp tuyến cuối**, mũi tên 1 e chỉ có nửa
  đầu phía ngoài cung. Ảnh: `anh/giai-bai-tap/mui_ten_va_electron_moi.png`.
- **Dạng khung cho công thức dài**: mạch có ≥ 5 nguyên tử C viết rõ (C, CH, CH₂, CH₃…) thì ẩn chữ C và mạch ngang thành đường
  gấp khúc; dị nguyên tố (O, N, Br…) và nhóm (NO₂, COOH, C₆H₅…) giữ nguyên; mũi tên, cặp e, điện tích dời theo nguyên tử. Chỉ
  đổi trong máy vẽ, không đổi dữ liệu bài. 8 bài mẫu hiện có đều dưới 5 C nên vẫn vẽ đầy đủ (đúng yêu cầu: ngắn thì vẽ full).
- Kiểm: KhktCoCheVerify 66/66, KhktUiVerify 77/77, app Mac xuất được.
- Còn lại: vị trí đầu mũi tên do máy chủ đặt (ví dụ Br⁻ → C⁺ ở bài cộng HBr chạm gần liên kết C–CH₃) – cần chỉnh ở dữ liệu
  `so_do_giay` của bài.

## 14. Phóng to ảnh, nhãn nguyên tử, Ẩn H, electron Lewis (20/11)
- **Phóng to 90%**: góc trên phải mỗi hình đáp án có nhãn "⤢ Phóng to"; bấm nhãn hoặc bấm thẳng vào hình → bảng chiếm 90% màn
  (nền mờ, ✕ / Esc / bấm nền để đóng). Cơ chế: tờ giấy lớn; Chuỗi: hình chuỗi 1,7× (cuộn được); 3D: ảnh 3D lớn. Ảnh `phong_to_90.png`.
- **Nhãn ký hiệu trên mọi nguyên tử** (C, H, O, Br… kèm ⊕/⊖) ở khung 3D đáp án và trình phát.
- **Ẩn H là ẩn hẳn**: không còn quả cầu trắng thu nhỏ, liên kết tới H cũng ẩn.
- **Nút "Hiện e"** (khung 3D đáp án và trình phát): electron không liên kết như cấu trúc Lewis – số e = hoá trị − tổng bậc liên
  kết − điện tích; cặp = 2 chấm, e lẻ (gốc tự do) = 1 chấm; đặt theo hướng xa các liên kết nhất. Ảnh `khung3d_lewis_an_h.png`.
- Kiểm: KhktUiVerify 80/80, KhktCoCheVerify 66/66, app Mac xuất được.

## 15. Thanh mức tư duy cho Cơ chế (20/11)
- Trang Cơ chế, mục 02: thanh **Mức tư duy** 4 nấc – Nhanh (≤ 1 phút) · Cân bằng (≤ 2 phút) · Kỹ (≤ 2,5 phút) · **Tối đa · HSG
  quốc gia (≤ 3 phút)**. Gửi `muc_tu_duy` (0–3) lên `/co-che/giai-de`.
- Máy chủ (`giai_de.MUC_TU_DUY`): mỗi nấc đổi effort của Claude (low/medium/high), thứ tự mô hình Gemini (Nhanh: flash trước;
  Cân bằng/Kỹ/Tối đa: pro trước) và **giới hạn thời gian một cơ chế** (60/120/150/180 s, không vượt 3 phút). App chờ thêm 30 s.
- Kiểm: Python 41/41 (giữ đúng hành vi cũ ở nấc Cân bằng), KhktUiVerify 81/81, app Mac xuất được.

## 16. Hướng sai (màn chắn) và bỏ dạng vừa giấy vừa 3D (20/11)
- **Hướng sai**: nút "✕ Hướng sai" (khung 3D dưới đáp án và trình phát toàn màn; chỉ hiện khi bài có nhánh sai): electron bay về
  hướng không thể (vd SN2 vào C bậc III) và bị **đĩa màn chắn đỏ** chặn lại; dải đỏ "✕ Không thể: tên – lý do". Bấm tiếp để qua
  nhánh sai khác. Ảnh `huong_sai_man_chan.png`.
- **Đề mới cũng có hướng sai**: khuôn trả lời của AI (`giai_de.SCHEMA_CO_CHE`) thêm `nhanh_sai` (0–2 hướng học sinh hay nhầm:
  cản trở không gian, tấn công phía trước, ngược Markovnikov…); máy dựng 3D bỏ qua nhánh AI viết lỗi, không hỏng cả bài.
- **Trình phát**: bỏ dạng vừa giấy vừa 3D – "Xem 3D" vào thẳng 3D toàn màn hình, "Thu nhỏ" về hẳn bảng giấy (bỏ nút trùng
  "Quay lại bảng giấy").
- Kiểm: Python 42/42, KhktCoCheVerify, KhktUiVerify đạt (xem mục kết quả commit), app Mac xuất được.

## 17. Thu nhỏ về trang đáp án; 3D từ PubChem, kéo xoay được (20/11)
- Mở "Toàn màn hình" từ khối đáp án Cơ chế → bấm **Thu nhỏ** là đóng hẳn trình phát, về lại trang đáp án (khung 3D dưới lời
  giải nhảy tới đúng bước vừa xem). Mở trình phát từ chỗ khác (phím M) thì Thu nhỏ vẫn về bảng giấy.
- **Mô hình 3D lấy cấu dạng chuẩn của PubChem** (`record_type=3d`, như trình xem 3Dmol của web CHEMISTRY cũ); không có mạng/không có
  bản 3D thì mới tự dựng bằng RDKit (ETKDG + MMFF). Kết quả có `nguon_3d` = `pubchem_3d` | `rdkit`; lưu đệm theo SMILES.
- Ảnh 3D trong thẻ đáp án **kéo chuột để xoay** (mọi hướng), **cuộn để phóng to**; thả tay 3 giây thì tự lắc chậm lại.
- Kiểm: Python mô hình 3D đạt; KhktUiVerify đạt; app Mac xuất được.

## 18. Màn chắn hướng sai là quả cầu (theo bản demo đã duyệt, 20/11)
- Thay đĩa dẹt bằng **quả cầu chắn** như `nguyen_mau/co_che_3d/sn2_sn1.html`: mặt cầu đỏ trong suốt bán kính 2,3 Å đặt tại
  nguyên tử bị tấn công (bao luôn các nhóm che chắn), phủ **lưới khung đa diện** (hình cầu 20 mặt chia 1 lần, 120 cạnh); độ đậm
  tăng khi electron chạm tới (0,06 → 0,18 mặt, 0,3 → 0,75 lưới), nhịp nhẹ. Ảnh `huong_sai_qua_cau_chan.png`.

## 19. Hướng sai tự phát khi Phát; nền 3D đen, màu nổi (20/11)
- Bỏ nút "Hướng sai": khi **Phát** tới bước có hướng sai, phim **tự phát hướng sai trước** (electron bay tới, bị quả cầu chắn
  chặn), xong **tự đi tiếp hướng đúng** của cùng bước. Xem lại bước (‹ ›, bấm dòng bước) thì phát lại cả hướng sai.
- **Nền 3D đen** (#040508) ở mọi khung 3D (dưới đáp án, trình phát, Mô hình 3D – cả ảnh trong thẻ lẫn khung xoay lớn).
- **Màu nổi trên nền đen**: C xám sáng (#949EAD thay xám đậm chìm nền), O/N/Br/Cl tươi hơn, mọi nguyên tử phát sáng nhẹ 12%.
  Ảnh `nen_den_mau_noi.png`.
- Kiểm: KhktCoCheVerify 66/66 (thêm: phát tới bước có hướng sai tự phát hướng sai rồi đi tiếp hướng đúng), KhktUiVerify 82/82,
  app Mac xuất được.
