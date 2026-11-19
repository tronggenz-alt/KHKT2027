# Cơ chế từ đề bài mới (18/11/2026)

Trước đây mục **Cơ chế** chỉ mở được 8 bài mẫu đã đóng gói. Bây giờ người dùng có thể gõ đề hoặc chọn ảnh đề. Máy chủ phân tích và sinh cơ chế,
rồi app hiện cơ chế đó ở hai chế độ 3D ⇄ Giấy, giống 8 bài mẫu.

## Luồng

```
App (bảng "Đề mới")                              Máy chủ cơ chế (cổng 8010)
  gõ đề / Chọn ảnh đề… / Phân tích  ── POST /co-che/giai-de ──▶  giai_de.py
                                                                    1. ảnh: kiểm tra → Claude đọc ảnh → SMILES (RDKit kiểm, nhờ đọc lại ≤ 2 lần)
                                                                    2. Claude viết cơ chế (SCHEMA_CO_CHE) → co_che.kiem_tra
                                                                       sai → gửi lỗi RDKit cho Claude sửa (≤ 3 lần, cùng hội thoại)
                                                                    3. đạt → dung_kich_ban_3d.dung + ve_theo_mau.tao_so_do
  BaiCoChe.TuJson → MoBai(b)        ◀── JSON thường {phien_ban, ma, bai, kich_ban_3d, so_do_giay, co_che, doc_anh, thong_tin}
```

Kết quả dùng cùng khung với gói `.verapack` nên app mở bằng chính `BaiCoChe.TuJson`. Không sửa `DuLieuCoChe.cs` hay `SoDoGiay.cs`.
Nếu cơ chế vẫn sai sau 3 lần sửa, máy chủ **không dựng 3D** mà trả lỗi 422 kèm danh sách lỗi RDKit. Thông điệp này hiện nguyên trong app.

## Máy chủ (`server/ai/co_che`)

| Tệp | Thay đổi |
|---|---|
| `giai_de.py` (mới) | Viết lại từ CHEMISTRY `backend/co_che.py` (SCHEMA_CO_CHE, SYSTEM_CO_CHE, vòng sửa lỗi) và `backend/co_che_anh.py` (đọc ảnh đề). Chỉ dùng Claude trước (`ANTHROPIC_API_KEY`, mô hình `CLAUDE_MODEL_CO_CHE` hoặc `CLAUDE_MODEL`, structured output). Claude không có khóa hoặc bị 401/403 thì chuyển sang **Gemini qua REST** (`GEMINI_API_KEY`, ảnh gửi dạng `inline_data`), lần lượt thử `GEMINI_MODEL_CO_CHE`, gemini-pro-latest, gemini-flash-latest, gemini-flash-lite-latest. Mô hình nào trả 429, quá tải hoặc quá 90 s thì bị bỏ qua 10 phút. Cả hai nhà cung cấp hỏng thì trả 503, nêu lý do của cả hai. Trước khi gửi, RDKit đánh map sẵn cho chất đầu (`smiles_co_map`) để AI viết bước 1 đúng khung. Tên cơ chế được làm gọn để dễ đọc (bỏ số map). Ảnh: tối đa 8 MB, JPG/PNG/WEBP/GIF/HEIC (HEIC đổi sang JPG bằng `sips` trên macOS); cạnh nhỏ hơn 200 px hoặc ảnh quá tối thì báo lỗi; cạnh dài hơn 1600 px thì thu nhỏ. Đề gõ toàn SMILES (`CCBr.[OH-]>>CCO`) được tách thẳng thành chất tham gia và sản phẩm. Kết quả lưu đệm trong bộ nhớ (32 đề) để gửi lại cùng đề thì không tốn lượt gọi AI. |
| `api.py` | `POST /co-che/giai-de` nhận JSON (`van_ban`, `anh_base64` có thể kèm tiền tố `data:`, `loai`) hoặc multipart (`van_ban`, `loai`, tệp `anh`). Thân yêu cầu quá lớn → 413. Chờ tối đa 300 s (`VERA_GIAI_DE_CHO`), quá thì trả 504. Lỗi có dạng `{"detail": {"thong_diep", "ma", "chi_tiet"}}`. Đã cập nhật docstring đầu tệp. |
| `may_chu.py` | `nap_env()` tự đọc `.env` ở gốc kho, không cần python-dotenv. Chỉ nạp `ANTHROPIC_API_KEY`, `ANTHROPIC_AUTH_TOKEN`, `CLAUDE_MODEL`, `CLAUDE_MODEL_CO_CHE`, `CLAUDE_EFFORT_CO_CHE`, `VERA_GIAI_DE_CHO`. Biến đã có trong môi trường thì giữ nguyên. Khi chạy kiểm thử (unittest/pytest, `VERA_KHONG_NAP_ENV=1`) thì không nạp, để test không bao giờ thấy khóa thật. |

Khi thiếu khóa hoặc khóa hỏng, máy chủ trả **503** với lời báo rõ ràng, ví dụ "Máy chủ cơ chế chưa có khóa Claude (ANTHROPIC_API_KEY trong .env ở gốc kho)…"
hoặc "Khóa Claude (ANTHROPIC_API_KEY) không hợp lệ.". 8 bài mẫu vẫn mở được bình thường.

## App (`Scripts/CoChe`)

- **`CoCheMoDun.cs`**: thêm bảng **"Đề mới"**. Mở từ nút "＋ Đề mới (gõ / ảnh)" ở đầu trang, từ nút đầu tiên trong "Bài khác ▾",
  hoặc gọi `CoCheMoDun.MoDeMoi()`. Bảng gồm:
  - ô đề nhiều dòng;
  - nút "Chọn ảnh đề…", tên và dung lượng ảnh đã chọn, nút "Bỏ ảnh";
  - nút "Phân tích";
  - 3 đề mẫu;
  - dòng trạng thái đếm giây khi đang chờ;
  - các chip "Đề đã giải" để mở lại đề đã làm trong phiên.

  Khi giải xong, bài mở như một bài mẫu: phim 3D, nút gạt Giấy, ô Giải thích lấy ghi chú từng bước. Trong lúc bảng mở, phím ← → và Space
  không điều khiển phim 3D. "Bài khác ▾" vẫn liệt kê đủ 8 bài mẫu (danh sách cao thêm 60 px).
- **`ChonTepHeThong.cs`** (mới): hộp chọn ảnh của hệ điều hành, không cần plugin, chạy ở luồng nền nên khung hình không bị treo.
  - **Mac:** `osascript` "choose file of type public.image".
  - **Windows:** PowerShell `OpenFileDialog` (`-STA`, lệnh mã hóa base64).
  - **Editor:** `EditorUtility.OpenFilePanelWithFilters`.
  - **iPad:** chưa hỗ trợ, nút hiện "Ảnh đề (sắp có)", người dùng gõ đề bằng chữ. Sau này cần plugin PHPicker hoặc UIImagePickerController.
  - Ảnh PNG/JPG lớn hơn 7 MB được nén lại thành JPG trước khi gửi.
- **`MayChuCoChe.cs`**: thêm `GiaiDe(...)` (POST JSON, chờ 330 s). Khi lỗi, `LoiTuMayChu` đọc `detail.thong_diep` và tối đa 4 dòng `chi_tiet`.
- **`KhktCoCheRunner.cs`**: thêm phần kiểm thử "Đề mới" với máy chủ giả và hộp chọn ảnh giả (xem bên dưới).

## Kiểm thử

- `tests/test_co_che_giai_de.py`: **18/18 đạt** (có 4 test chọn Claude → Gemini), không gọi mạng. AI giả trả JSON đúng hoặc sai theo kịch bản. Các phần đã kiểm:
  - đề đúng ngay lần đầu ra 3D và giấy;
  - vòng sửa lỗi: lần 1 không phải JSON, lần 2 sai RDKit, lần 3 đúng; mỗi lần sửa gửi lại đủ hội thoại cùng lỗi cụ thể;
  - sai mãi thì trả 422 kèm chi tiết, đúng 4 lần gọi;
  - đề gõ bằng SMILES, bỏ ion khán giả Na⁺;
  - ảnh đề: SMILES hỏng được nhờ đọc lại, rồi viết cơ chế;
  - ảnh không phải đề Hoá;
  - kiểm tra ảnh: rỗng, sai định dạng, quá nhỏ, quá tối, quá lớn (413), thu nhỏ;
  - thiếu khóa thì báo 503;
  - API: JSON chữ, base64, multipart, các lỗi 400/413/422, quá giờ 504;
  - `nap_env` chỉ nạp các biến cần.
- `tests/test_co_che_api.py` vẫn đạt.
- `KhktCoCheVerify.Run` (19/11, chạy trên bản sạch HEAD + `DanhDauBuoc`, kết quả đề là kết quả ảnh đề THẬT qua `KHKT_CO_CHE_DE_JSON`): **53/53 đạt**, 0 lỗi runtime, FPS propellane 693.
  Phần Đề mới gồm các kiểm tra:
  - bảng mở thì phim 3D không nhận phím;
  - chọn ảnh (hộp chọn giả) gắn được ảnh;
  - ảnh được gửi kèm dạng base64;
  - kết quả mở như một bài (SN1, 4 bước, sơ đồ giấy 21 nét);
  - lỗi 422 hiện thông điệp kèm chi tiết.

  Ảnh chụp: `de_moi_ket_qua.png` (3D, khung "BƯỚC CHẬM"), `de_moi_giay.png` (sơ đồ giấy SN1 có mũi tên cong, chậm/nhanh), `bang_de_moi.png`, `de_moi_loi.png`.

## Thử thật

Khóa Claude trong `.env` bị Anthropic trả 401 (invalid x-api-key), nên lần thử thật chạy bằng Gemini. gemini-pro và gemini-flash lúc trả 429 (hết hạn mức), lúc treo; **gemini-flash-lite-latest** trả lời được.

| Đề | Kết quả |
|---|---|
| Chữ: "Viết cơ chế phản ứng cộng HBr vào propen…" | **Đạt.** 2 bước (H⁺ cộng vào C1 tạo cacbocation bậc 2, rồi Br⁻ tấn công), giải thích đúng Markovnikov. AI tự sửa 3 lần, mất 38,7 s. |
| Ảnh (tự tạo: Câu 2, (CH3)3C-Br + H2O, đun nóng) lần 1 | Ảnh đọc đúng hết: đề, SMILES `CC(C)(C)Br`, `O`, gợi ý SN1, mất 10 s. Nhưng flash-lite viết sai khung C, vẫn sai sau 3 lần sửa → **422** kèm chi tiết, không dựng 3D sai. |
| Cùng ảnh, sau khi thêm `smiles_co_map` | **Đạt.** SN1 3 bước (C–Br đứt là bước chậm → H₂O tấn công → tách H⁺), mất 24,6 s, sơ đồ giấy đúng. |

Gọi qua HTTP: `curl -F van_ban= -F anh=@anh_de_thu.png http://127.0.0.1:8010/co-che/giai-de` trả 200. Lỗi quá giờ 504 và lỗi khóa 503 cũng đã thử qua HTTP.

## Còn lại

- Thay khóa Claude trong `.env`. Hiện chạy bằng Gemini flash-lite (mô hình yếu nhất, hay phải sửa 3 lần). Có Claude hoặc gemini-pro thì cơ chế khó (mức HSG) sẽ tốt hơn.

- iPad: chưa chọn được ảnh đề (cần plugin). Gõ đề vẫn dùng được nếu iPad kết nối được máy chủ trong mạng LAN.
- Máy chủ chưa gửi tiến trình từng bước về app. App chỉ đếm giây và ghi các giai đoạn đang chạy.
- Đề có nhiều phản ứng: hiện chỉ vẽ phản ứng đầu tiên, hoặc phản ứng người dùng ghi trong ô đề, vd "chỉ câu 2".
