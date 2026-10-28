# HƯỚNG DẪN DẠY VÀ VẬN HÀNH "AI RIÊNG – GIA SƯ HOÁ HỮU CƠ"

Tài liệu hướng dẫn giáo viên / quản trị viên app **CHEMISTRY** xây dựng kho tri thức riêng và huấn luyện trợ lý AI giải chuỗi phản ứng mà **không cần fine-tune mô hình**.

---

## 1. Nguyên lý hoạt động: Cơ chế Giải 3 Tầng

Hệ thống AI Riêng vận hành theo quy trình 3 tầng tối ưu chi phí và độ chính xác:

```
[ Đề chuỗi phản ứng ]
         │
         ▼
┌─────────────────────────────────┐
│ Tầng 1: Thư viện lời giải       │ ──(Khớp)──► Trả kết quả tức thì (0 token, 0 s,
│ (Đã duyệt trong kho)            │             không trừ lượt dùng thử)
└─────────────────────────────────┘
         │ (Chưa có trong thư viện)
         ▼
┌─────────────────────────────────┐
│ Tầng 2: Suy luận từ kho riêng   │ ──► Tìm kiếm lai (Vector + FTS5):
│ (Mô hình rẻ: Flash / Lite)       │     • Đoạn trích tài liệu liên quan [T1, T2]
└─────────────────────────────────┘     • Quy tắc & kiến thức chuyên sâu [Q1, Q2]
         │                               • Các mục đã dạy sửa sai [S1, S2]
         ▼
┌─────────────────────────────────┐
│ Kiểm tra tự động bằng RDKit     │ ──(Đạt)──► Xuất ảnh PNG, CDXML, SDF
│ (SMILES, hoá trị, cân bằng)     │
└─────────────────────────────────┘
         │ (Nếu phát hiện lỗi cấu tạo)
         ▼
┌─────────────────────────────────┐
│ Tầng 3: Tự động nâng mô hình    │ ──► Dùng mô hình mạnh (Pro/Claude) cùng lỗi
│ để sửa bài                      │     từ RDKit để hoàn thiện lời giải chuẩn
└─────────────────────────────────┘
```

---

## 2. Cách nạp tài liệu vào kho (Tab 02)

### Các định dạng hỗ trợ
- **File tài liệu**: `.pdf`, `.docx`, `.txt`, `.xlsx`, hình ảnh đề bài (`.png`, `.jpg`).
- **Link bài viết**: URL Wikipedia tiếng Việt/Anh, LibreTexts hoặc các trang tài liệu mở.
- **Dán văn bản trực tiếp**: Sao chép nội dung từ giáo trình, bài giảng.

### Tự động trích xuất bài mẫu
Khi tải lên tài liệu là **tập đề thi có đáp án** hoặc **sách giải bài tập**, hãy tích chọn:
> ☑ **Tài liệu có đề + lời giải (tự trích bài mẫu)**

Hệ thống sẽ chạy tiến trình nền:
1. Đọc từng trang và tách thành các đoạn nội dung nhỏ (~800–1.500 từ).
2. Phát hiện các chuỗi phản ứng và lời giải có sẵn trong tài liệu.
3. Tạo các bản ghi **Bài mẫu chờ duyệt** trong Thư viện lời giải.

### Kiểm tra & biên tập đoạn trích
- Bấm **"Xem đoạn"** tại mỗi tài liệu để kiểm tra các phần đã tách.
- Có thể trực tiếp chỉnh sửa câu chữ hoặc xoá các đoạn thông tin không liên quan (mục lục, lời nói đầu...).

---

## 3. Cách soạn và dạy Quy tắc Kiến thức (Tab 03)

Đây là những quy luật hoá học cốt lõi bạn muốn AI luôn tuân thủ khi giải.

### Cấu trúc một quy tắc chuẩn
* **Tiêu đề**: Ngắn gọn, nêu rõ phản ứng hoặc nhóm chất (vd: *Cộng HBr vào anken bất đối xứng*).
* **Nội dung**: Nêu rõ quy tắc, điều kiện và sản phẩm chính/phụ:
  > *Theo quy tắc Markovnikov: Khi cộng HX (HBr, HCl, H2O) vào liên kết đôi C=C bất đối xứng, H ưu tiên cộng vào nguyên tử C bậc thấp hơn (nhiều H hơn), còn X cộng vào C bậc cao hơn.*
* **Chủ đề**: Phân loại theo mảng (vd: *Hidrocacbon*, *Ancol - Phenol*, *Este - Lipit*...).
* **Mức ưu tiên (1–5)**:
  - `5` (Rất cao): Các quy tắc quan trọng nhất, bắt buộc ưu tiên trước (Markovnikov, Zaitsev, định hướng thế vòng benzen).
  - `3` (Bình thường): Các phản ứng thông dụng.
  - `1` (Thấp): Ngoại lệ hiếm gặp.

### Nhập hàng loạt từ Excel / CSV
Bấm **"Nhập Excel / CSV"**, chuẩn bị file với các cột tiêu đề:
`tieu_de` | `noi_dung` | `chu_de` | `uu_tien`

---

## 4. Quản lý Thư viện lời giải (Tab 04)

- **Bài mẫu Đã duyệt**: Sẽ được Tầng 1 sử dụng để trả kết quả ngay lập tức khi học sinh nhập đề trùng khớp.
- **Bài mẫu Chờ duyệt**: Được trích xuất tự động từ tài liệu tải lên hoặc lưu từ Sân tập. Bấm **"Duyệt"** sau khi kiểm tra để đưa vào vận hành.
- **Kiểm tra RDKit**: Mọi bài mẫu đều được RDKit tự động thẩm định tính hợp lệ của cấu trúc hoá học.

---

## 5. Dạy sửa sai & Xử lý phản ánh từ học sinh (Tab 05)

Khi AI trả lời sai một phản ứng:
1. **Hàng chờ báo sai**: Xem các đề bài mà học sinh bấm nút **"Báo sai"** trên giao diện giải.
2. Bấm **"Dạy sửa"**:
   - Nhập đáp án đúng chuẩn (công thức, chất, điều kiện).
   - Nhập lý do (để AI hiểu vì sao cách giải cũ là sai).
3. Sau khi lưu, mục sửa sai này sẽ được gán độ ưu tiên cao nhất trong prompt của AI Riêng khi gặp các đề bài tương tự.

---

## 6. Sân tập thử nghiệm (Tab 06)

Nơi giáo viên kiểm tra năng lực của AI Riêng trước khi mở cho học sinh:
- **Bắt buộc suy luận từ kho**: Đánh dấu để AI không dùng bài mẫu có sẵn trong thư viện, buộc phải tự ráp nối kiến thức đã học.
- **Giải so sánh song song**: Bật để so sánh trực tiếp kết quả của **AI Riêng** (có kho kiến thức) với **AI thông thường** (chỉ có trí nhớ gốc).
- Đánh giá ngay sau khi giải:
  - Bấm **"Đúng → Lưu thư viện"** để đưa vào bộ bài mẫu chuẩn.
  - Bấm **"Sai → Dạy sửa"** để mở form nạp quy tắc sửa sai tức thì.

---

## 7. Bộ đề kiểm tra & Chấm tự động (Tab 07)

Dùng để đánh giá "năng lực học tập" của AI qua thời gian:
1. Nhập danh sách câu hỏi kiểm tra kèm đáp án chuẩn (vd: `A: C2H5OH; B: CH3COOH`).
2. Bấm **"Chạy chấm toàn bộ"**:
   - Hệ thống tự động cho AI giải toàn bộ các câu trong đề.
   - Thẩm định đáp án bằng so khớp phân tử RDKit (chuẩn hoá SMILES, không phụ thuộc cách viết chữ).
3. Xem bảng điểm tổng kết (tỉ lệ % đúng) và phân tích chi tiết theo từng chủ đề.

---

## 8. Sao lưu và Phục hồi (Tab 01)

- **Tải sao lưu**: Tải file `.json` chứa toàn bộ tài liệu, đoạn trích, quy tắc, bài mẫu và lịch sử sửa sai. Khuyến nghị sao lưu định kỳ.
- **Phục hồi**: Tải file sao lưu lên để gộp dữ liệu hoặc chuyển giao kho kiến thức sang máy chủ khác.

---

## 9. Chuẩn Biện Luận Chất Hữu Cơ 4 Bước (`->`)

Khi soạn thảo **Quy tắc (Tab 03)**, **Bài mẫu (Tab 04)**, hoặc **Dạy sửa (Tab 05)** cho dạng bài tìm chất ẩn ($X, Y, Z, A, B...$) hoặc chuỗi chuyển hóa phức tạp, giáo viên cần áp dụng cú pháp rút gọn bằng ký hiệu `->`, không dùng văn mẫu rườm rà, bám sát 4 bước:

1. **$\Delta$ & Nhóm chức**:
   - Tính độ bất bão hoà $\Delta = \frac{2C + 2 + N - H - X}{2}$.
   - Dịch nhanh dữ kiện theo cú pháp: `[Dữ kiện] -> [Cấu tạo/Nhóm chức/Phản ứng]`.
   - *Ví dụ*: `X (C4H6O2) -> Δ = 2 (1 C=O + 1 C=C)`. `X + NaOH -> 1 muối + 1 anđehit -> X: RCOOCH=CH-R'`.

2. **Truy vết sơ đồ**:
   - Biện luận từng nấc theo sơ đồ: `Chất A -> Chất B`.
   - Chỉ nêu tác nhân, sự biến đổi khung C/nhóm chức và cơ chế trọng tâm (đóng/mở vòng, chuyển vị, cắt mạch decarboxyl/ozon phân, cộng/tách Markovnikov/Zaitsev).

3. **Tổng hợp CTCT**:
   - Liệt kê rõ CTCT thu gọn của tất cả các chất chưa biết trong đề bài (thể hiện rõ liên kết đôi/ba, vị trí nhóm chức, mạch phân nhánh).

4. **Kiểm chứng (Bắt buộc)**:
   - Check nhanh kết quả đối chiếu: `[CTPT, Δ, số mol/khối lượng, lập thể/đồng phân]`.
   - Nếu phát hiện mâu thuẫn dữ kiện -> tự động điều chỉnh và hoàn thiện lại CTCT trước khi kết luận.