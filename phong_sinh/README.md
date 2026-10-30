# phong_sinh: hồ sơ dựng tầng 2, khu sinh học

Thư mục này chứa mọi thứ cần để dựng tầng 2 (khu sinh học) của VERA Lab. Mã Unity **không** đặt ở đây mà theo đúng `contracts.md` (xem `PROMPT_ANTIGRAVITY.md` mục 2).

| Tệp | Nội dung |
|---|---|
| `PROMPT_ANTIGRAVITY.md` | Prompt đầy đủ cho Antigravity: bố cục, đồ họa, chủ đề Quốc tế / Tối giản, hoạt cảnh, liên kết app cũ, các giai đoạn, kiểm thử |
| `bo_cuc_tang2.json` | Tọa độ từng phòng, cửa, cửa sổ, đồ đạc, lõi thang, khu `ZoneMap` 5..12, trạm 20..31 (đo từ bản vẽ, 21,4 px = 1 m) |
| `mat_bang_tang2.png` | Bản đồ quy hoạch 2 tầng (trang 13 bản trình bày) |
| `tu_app_cu/` | Tài nguyên lấy từ phòng sinh học 3D của app cũ (web CHEMISTRY, `/sinh-hoc`) |

## tu_app_cu

| Tệp | Nguồn trong app cũ |
|---|---|
| `bp3d.bin.gz` | `frontend/sinh-hoc/bp3d.bin.gz`: giải phẫu BodyParts3D đã giảm đa giác |
| `dung_bp3d_tham_khao.py` | `tools/dung_bp3d.py`: mô tả định dạng nhị phân của `bp3d.bin.gz` |
| `kien_thuc/` | `frontend/sinh-hoc/kien-thuc/`: 15 cơ quan, danh mục, thuật ngữ SGK |
| `vi_mo_tham_khao.js` | `frontend/biology-lab-3d.html` mục 16: 6 cảnh hiển vi |
| `phong_lab_cu_tham_khao.js` | `frontend/biology-lab-3d.html` mục 3: phòng hologram cũ (chỉ tham khảo ý tưởng) |

## Ghi công

BodyParts3D, © The Database Center for Life Science, licensed under CC Attribution 4.0 International
(https://dbarchive.biosciencedbc.jp/en/bodyparts3d/lic.html). Khi hiển thị trong phòng phải có dòng ghi công này.
