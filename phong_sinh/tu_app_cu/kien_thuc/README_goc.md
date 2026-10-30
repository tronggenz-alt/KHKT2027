# Kiến thức Phòng Lab Sinh học 3D

Mỗi cơ quan là **một file JSON** trong thư mục này. Trang `/sinh-hoc` đọc file khi học sinh bấm vào cơ quan trên mô hình 3D. AI dùng cùng nội dung này làm căn cứ để trả lời.

## Sửa nội dung
Mở file của cơ quan cần sửa (ví dụ `tim.json`) rồi sửa chữ trong dấu ngoặc kép. Giữ nguyên tên các mục:

| Mục | Ý nghĩa |
|---|---|
| `vi_tri` | Vị trí |
| `cau_tao` | Cấu tạo |
| `chuc_nang` | Chức năng |
| `co_che` | Cơ chế hoạt động |
| `lien_he` | Liên hệ thực tế / bệnh thường gặp |
| `diem_thi` | Điểm hay ra thi THPT |
| `trac_nghiem` | 3–5 câu: `cau`, 4 `lua_chon`, `dap_an` (A/B/C/D), `giai_thich` |
| `goi_y_hoi` | 2–4 câu gợi ý hỏi AI (kết thúc bằng dấu ?) |
| `mo_rong` | (không bắt buộc) kiến thức ngoài chương trình THPT, hiện trong ô "Mở rộng" thu gọn |
| `can_duyet` | `true` = chưa được giáo viên duyệt; duyệt xong đổi thành `false` |

## Thêm cơ quan mới
1. Chép `tim.json` thành file mới (ví dụ `mat.json`), sửa `id` và nội dung.
2. Thêm một dòng vào `danh-muc.json`: `id`, `ten`, `icon`, `he`, `tep` và `bo_phan` (mã bộ phận 3D sẽ mở cơ quan này).

## Thuật ngữ
`thuat-ngu.json` ghi cách viết chuẩn theo SGK (ví dụ "neuron", không viết "nơron"). Thêm từ cần thống nhất vào đây.

## Kiểm tra sau khi sửa
```
.venv/bin/python -m pytest -q tests/test_kien_thuc_sinh_hoc.py
```
Test báo lỗi nếu file JSON sai cú pháp, thiếu mục, đáp án không hợp lệ, mã bộ phận không có trên mô hình, hoặc dùng thuật ngữ sai.
