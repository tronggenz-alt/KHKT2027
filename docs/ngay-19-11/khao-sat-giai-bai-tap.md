# Khảo sát nhánh "Giải bài tập" – 19/11/2026

Mục đích: lấy bố cục app web CHEMISTRY (`~/CHEMISTRY_xem/frontend`, nhánh chemistry, chỉ đọc) làm khuôn cho
nhánh "Giải bài tập" của VERA Lab (Unity), đổi chủ đề sang VERA (phông Be Vietnam Pro, màu nhấn xanh VERA, 3 nền
Trắng/Đen/Xanh mực), và liệt kê lỗi của bản Unity hiện tại.

Cách xem CHEMISTRY: trang dùng đường dẫn `/static/...`, nên phục vụ một thư mục tạm có `static → frontend` và
các tệp `*.html` (máy chủ tĩnh cổng 8766), mở ở khung 1440×900. Không có backend nên các API trả lỗi (toast góc phải).

## 1. Bố cục CHEMISTRY (1440×900)

### Khung chung
- **Thanh điều hướng trên** (cao ~64): chữ "chemistry:" bên trái, viên chọn Hóa học / Sinh học, giữa là các mục
  `Chuỗi phản ứng · Tìm cấu tạo · Cơ chế · Mô hình 3D · Phòng TN ảo · Hóa lý · Tài liệu HSG`, phải là "Đăng nhập"
  và nút đen "Bắt đầu giải". Mục đang mở in đậm.
- **Thanh phụ dính** dưới thanh trên (viên nang trắng viền tóc): nút `‹ <tên công cụ>` + các bước ngang
  (`buoc-ngang`): `01 Nhập đề · 02 Tuỳ chọn · 03 Kết quả`, bước đang cuộn tới tô màu nhấn.
- **Đầu trang công cụ**: nhãn chữ đơn cách viết hoa nhỏ ("CÔNG CỤ · GIẢI CHUỖI PHẢN ỨNG"), tiêu đề rất lớn (~52 px,
  đậm, chữ sát), đoạn mô tả 16 px có chữ đậm/nghiêng nhấn ý.
- **Mục đánh số**: số đơn cách màu nhấn ("01") ở trên, tiêu đề mục ~32 px, dòng phụ 14 px xám.
- Nền xám rất nhạt (#f4f4f5), thẻ trắng bo ~16, viền tóc 1 px, bóng rất nhẹ; nút chính màu đen chữ trắng có mũi tên →.
- Chân trang: "CHEMISTRY – chạy trên máy của bạn…" trái, các nguồn (PubChem · Wikipedia · RDKit · ChemDraw) phải.
- Chuyển động (`chuyen-dong.js`, `chuyen-canh.js`): các khối hiện lần lượt trượt lên 12–20 px, ~400 ms, cách nhau
  ~50 ms; đổi trang mờ chéo ~200 ms; thẻ rê chuột nhấc 2–4 px.

### Trang chủ chọn công cụ
- Hàng 4 thẻ lớn cao ~200 bằng nhau: **Giải chuỗi** (trắng), **Tìm cấu tạo** (xanh lá đậm), **Cơ chế** (xanh navy),
  **Mô hình 3D** (đen). Mỗi thẻ: ô biểu tượng vuông bo trái trên, nhãn đơn cách "01 · CƠ BẢN ĐẾN NÂNG CAO" phải trên,
  tên 20 px đậm, mô tả 2 dòng, hàng chip đơn cách nhỏ (A → B → C, ChemDraw CDXML, Từng nấc…).
- Dưới là các thẻ ngang rộng hết khung: Phòng Lab Sinh học 3D, Phòng thí nghiệm Hóa học ảo, **Hóa lý** – mỗi thẻ có ô
  biểu tượng, nhãn nhỏ, tên, một dòng mô tả và nút đen viên nang bên phải.

### Chuỗi phản ứng
1. `01 Nhập đề bài` – 3 thẻ tab ngang: **Văn bản** (gõ chuỗi), **Hình ảnh** (tải/kéo-thả/dán ảnh đề), **Chuỗi mẫu**
   (5 chuỗi có sẵn). Thẻ đang chọn viền đậm + nền nhạt. Dưới là thẻ nhập: nhãn "CHUỖI PHẢN ỨNG", nút `Chèn →` và
   `Chèn →[tác nhân]`, ô nhiều dòng phông đơn cách với ví dụ mờ.
2. `02 Tuỳ chọn giải` – thẻ trái: **Mức độ đề** (thanh phân đoạn Tự động / Trung bình / HSG, nút `?`),
   **Mức thông minh** (thanh trượt Nhanh / Cân bằng / Kỹ / Tối đa, ô "Theo độ khó"), công tắc **Tra cứu tài liệu khoa học**,
   công tắc Hiện Chrome. Thẻ phải: **Lời giải** (Tự động / Gemini / Demo). Dải gập "VÍ DỤ & CÁCH HOẠT ĐỘNG".
   Nút lớn đen **Giải chuỗi →** + dòng trạng thái xám "Chưa nhập chuỗi".
3. `03 Kết quả` – thẻ trống có biểu tượng lục giác, "Chưa có kết quả" và hướng dẫn. Khi có kết quả: thanh công cụ
   (Mở bằng ChemDraw, Báo sai, Xem phương trình chi tiết, Mở tất cả), mỗi bước là một thẻ gập được.

### Cơ chế (05)
- Ô "PHẢN ỨNG" nhiều dòng, công tắc tra cứu tài liệu, vùng **Tải ảnh đề** (kéo-thả/dán, ≤ 4 ảnh), hàng chip mẫu
  (CH3CH2Br + OH⁻, propen + HBr, 2-bromopropan + KOH/etanol, este hóa, benzen + HNO3/H2SO4, CH3CHO + HCN),
  ô chọn **Loại cơ chế** (Tự động, SN2, SN1, E2, E1, AE, SEAr, AN, Este hóa, Thủy phân este, Khác) và nút **Vẽ cơ chế**.
- Kết quả: huy hiệu nguồn (Mẫu chuẩn / AI tạo, cần kiểm tra), sơ đồ SVG, cảnh báo, nút **Xem giải thích chi tiết**
  (tải nền `/api/co-che/giai-thich`, hiện tổng quan + từng bước), tải PNG/SVG/CDXML, copy JSON, hỏi đáp
  `/api/co-che/chat`.

### Mô hình 3D (04)
- 2 thẻ tab: Văn bản (tên Việt/Anh, công thức, SMILES) và Hình ảnh; ô "CHẤT CẦN DỰNG 3D" + chip mẫu (ethanol, axit
  axetic, benzen, glucozơ, CH3COOC2H5, aspirin, caffeine, SMILES); nút **Dựng mô hình 3D →**. Kết quả: khung 3D có
  2 tab (bóng-que / đặc), đặt lại góc nhìn, toàn màn hình.

### Hóa lý
- Thẻ ngang ở trang chủ mở trang riêng 4 mô phỏng (Khí lý tưởng, Tốc độ, Cân bằng, Động cơ nhiệt).

## 2. Lỗi của bản Unity hiện tại (ảnh trong `anh/giai-bai-tap/truoc_*.png`)

Trang Giải bài tập (`truoc_trang_giai_bai_tap.png`):
1. Không có thanh điều hướng giữa các công cụ: muốn sang công cụ khác phải về trang rồi bấm lại.
2. Thẻ công cụ dùng ảnh chụp nền tối cắt dở (ảnh Chuỗi bị cắt chữ, ảnh Cơ chế chỉ thấy một nửa phân tử) – trông
   như ảnh lỗi; không giống thẻ lớn của CHEMISTRY.
3. Hóa lý là 4 ô nhỏ rời, không có trang riêng; nửa dưới màn trống.

Chuỗi phản ứng (`truoc_chuoi.png`):
4. Bảng chuỗi nổi đè lên cảnh 3D của mô-đun Cơ chế: phân tử và các nút của trình phát lộ ra quanh bảng.
5. Không có 3 tab nhập (chỉ một ô chữ), không có tuỳ chọn giải, không có trạng thái trống; chip mẫu chữ bé, cắt chữ.
6. Kết quả không gập/mở được, không có Báo sai, không có thanh công cụ kết quả.

Cơ chế (`truoc_co_che_3d.png`, `truoc_co_che_giay.png`):
7. Giao diện "kính tối" riêng, không theo chủ đề VERA (phông, màu, bo góc khác hẳn phần còn lại).
8. Tiêu đề trái trên bị nhòe/phát sáng; nút "Phát" chữ xanh trên nền xanh gần như không đọc được.
9. Không có trang nhập đề kiểu CHEMISTRY (chọn loại cơ chế, ảnh, mẫu) – "Đề mới" là bảng nổi nhỏ.
10. Không có danh sách bước để bấm nhảy tới bước; giải thích chỉ là ô chữ dài; hỏi AI chỉ một câu, không giữ hội thoại.
11. Không xuất được ảnh sơ đồ giấy, không có Báo sai.
12. Nút "Đóng" đưa thẳng ra ngoài, không về đúng trang Giải bài tập.

Chung:
13. Cài đặt máy chủ nằm hai nơi (bảng Cơ chế và Cài đặt Giải bài tập).
14. Hệ thống không bấm chuột được vào app Unity bằng System Events → ảnh "sau" chụp bằng KhktUiVerify (Unity
    batchmode có đồ họa, 1440×900) và app Mac thật (chỉ gửi phím).

## 3. Hướng làm

- Một khung trang chung cho cả nhánh (thanh điều hướng trên, thanh phụ có bước ngang, cuộn dọc, mục đánh số), dùng
  MenuKit/UiTheme nên theo đủ 3 nền VERA; nút chính màu nhấn VERA thay cho màu đen của CHEMISTRY.
- Trang chủ: hàng thẻ lớn dải màu (Chuỗi trắng, Cơ chế navy, Mô hình 3D đen) + thẻ ngang Hóa lý.
- Chuỗi: đúng 3 mục 01/02/03 như trên; ảnh đề → máy chủ đọc chuỗi bằng AI (Claude trước, Gemini sau).
- Cơ chế: trang nhập đề (Văn bản / Hình ảnh / Bài mẫu, loại cơ chế, tra cứu) → trình phát 3D ⇄ giấy có cột "Các bước",
  Giải thích chi tiết, Hỏi đáp (giữ hội thoại), Xuất ảnh PNG sơ đồ giấy, Báo sai.
- Máy chủ: thêm `/co-che/giai-thich-chi-tiet`, `/co-che/hoi-dap`, `/co-che/bao-sai`, ảnh cho `/co-che/giai-chuoi`.
