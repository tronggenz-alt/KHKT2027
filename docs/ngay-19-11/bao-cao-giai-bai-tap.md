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
