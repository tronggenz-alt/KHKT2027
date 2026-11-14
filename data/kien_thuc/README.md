# Kho kiến thức RAG – Hóa học và Sinh học THPT (VERA Lab)

Thư mục này là "cuốn sách" mà máy chủ AI tra cứu trước khi trả lời học sinh (RAG). Mô hình chính là
VERA-Hoa-32B (Qwen2.5-32B), cửa sổ ngữ cảnh 8.192 token, nên mỗi lần chỉ đọc được khoảng 5 đoạn.
Vì vậy tài liệu phải chia thành các đoạn ngắn, mỗi đoạn tự đứng được.

Dựng lại kho sau khi sửa tài liệu:

    .venv/bin/python tools/xay_kho_rag.py            # cần Ollama + bge-m3; không có thì chỉ dựng chỉ mục từ khóa
    .venv/bin/python tools/xay_kho_rag.py --kiem-tra # chấm khả năng tìm đúng tài liệu bằng các câu trong _kiem_tra/

## Cấu trúc

    hoa/lop10/  hoa/lop11/  hoa/lop12/     lý thuyết + chuyên đề học tập Hóa học (GDPT 2018)
    sinh/lop10/ sinh/lop11/ sinh/lop12/    lý thuyết + chuyên đề học tập Sinh học (GDPT 2018)
    tra_cuu/                               bảng tra: nhận biết ion và khí, màu chất, tính tan, dãy điện hóa, hằng số...
    thuc_hanh/                             kỹ thuật và an toàn phòng thí nghiệm Hóa, Sinh; gắn với dụng cụ của dự án
    _kiem_tra/                             câu hỏi kiểm tra khả năng tìm tài liệu (JSON)

Ngoài các file `.md` ở đây, công cụ dựng kho còn tự đọc dữ liệu có cấu trúc của dự án: `data/chem/*.json`
(720 hóa chất, thiết bị, nhiệt độ sôi), `data/reactions/phan_ung.json`, `data/may/*.json`, `data/sinh/*.json`.

## Quy cách một file

Tên file: `NN-ten-khong-dau.md` (NN là số thứ tự chương, `cd-...` cho chuyên đề), ví dụ `04-phan-ung-oxi-hoa-khu.md`.

```markdown
---
mon: hoa                      # hoa | sinh
lop: 10                       # 10 | 11 | 12 | 0 (dùng chung, cho tra_cuu và thuc_hanh)
loai: ly_thuyet               # ly_thuyet | chuyen_de | tra_cuu | thuc_hanh
chu_de: Phản ứng oxi hóa – khử
tu_khoa: [số oxi hóa, chất khử, chất oxi hóa, cân bằng electron]
nguon:
  - Chương trình GDPT 2018 môn Hóa học (Thông tư 32/2018/TT-BGDĐT)
  - https://...
---

# Phản ứng oxi hóa – khử

## Số oxi hóa: định nghĩa và quy tắc xác định
(150–450 từ, tự đứng được)

## Phản ứng oxi hóa – khử: chất khử, chất oxi hóa, quá trình khử và oxi hóa
...
```

Quy tắc viết:

1. **Mỗi mục `##` là một đoạn RAG.** Dài 150–450 từ. Tiêu đề `##` phải nêu rõ chủ đề (không viết "Ví dụ" trơn,
   mà viết "Ví dụ cân bằng phản ứng oxi hóa – khử bằng phương pháp thăng bằng electron").
   Câu đầu đoạn nhắc lại chủ đề; không dùng "như trên", "ở mục trước".
2. **Đúng và đủ chương trình GDPT 2018** (sách Kết nối tri thức, Chân trời sáng tạo, Cánh diều có cùng yêu cầu cần đạt).
   Mỗi yêu cầu cần đạt của chương trình phải có ít nhất một đoạn trả lời được.
3. **Tự viết bằng lời của dự án**, không chép nguyên văn sách giáo khoa hay trang web.
4. **Tên chất:** Hóa học dùng tên IUPAC theo chương trình mới, kèm tên cũ trong ngoặc ở lần đầu
   (sodium hydroxide (natri hiđroxit)). Công thức viết chữ số thường: H2SO4, Fe2(SO4)3, ion Fe3+, SO4 2-.
5. **Số liệu phải kiểm chứng** (nguyên tử khối, hằng số, nhiệt độ, pH...) bằng nguồn tin cậy; ghi nguồn ở `nguon`.
6. **Mỗi chương nên có:** khái niệm, quy tắc/công thức, phương trình đã cân bằng, hiện tượng thí nghiệm,
   ví dụ có lời giải từng bước, lỗi học sinh hay mắc, liên hệ thực tế và an toàn. Cuối file có mục
   `## Hỏi đáp nhanh: <chủ đề>` gồm 5–10 cặp hỏi–đáp ngắn.
7. Không dùng bảng Markdown quá 8 dòng trong một đoạn (mô hình đọc bảng dài kém); bảng dài thì tách đoạn.
8. Không hướng dẫn điều chế chất nổ, chất độc, vũ khí, hay thao tác với tác nhân sinh học ngoài BSL-1.

## Câu hỏi kiểm tra (`_kiem_tra/*.json`)

```json
[
  {
    "cau_hoi": "Chất khử là gì?",
    "dap_an": "Chất khử là chất nhường electron, số oxi hóa tăng.",
    "tu_khoa": ["nhường electron", "số oxi hóa tăng"],
    "tep": "hoa/lop10/04-phan-ung-oxi-hoa-khu.md"
  }
]
```

`tep` là file chứa câu trả lời; công cụ kiểm tra đo xem file đó có nằm trong 5 đoạn tìm được hay không.
