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
