# Hợp đồng dữ liệu: Cơ chế 3D ⇄ Giấy → app VERA (Unity)

Python lo tính toán và xuất JSON; app Unity chỉ đọc JSON để hiển thị. Các trang `.html` trong thư mục này chỉ là bản
thử để xem nhanh (mở qua máy chủ tĩnh ở gốc kho, launch.json "co-che-3d": `/nguyen_mau/co_che_3d/xem_bai_demo.html`).

Từ 18/11 (C1): mã Python nằm ở `server/ai/co_che/` (lõi `co_che.py`, `dung_kich_ban_3d.py`, `hoc_ve/`); dữ liệu kín (kho mẫu,
báo cáo bộ đo, 8 bài demo) ở `$VERA_DU_LIEU_RIENG/co_che/` (mặc định `~/Developer/VERA_du_lieu_rieng`), không vào git.
Dựng tất cả: `python -m server.ai.co_che.dung_bai_demo` → `build/co_che/<ma>/<ma>.{bai,3d,giay}.json` (git bỏ qua).

## Luồng

```
cơ chế (JSON, định dạng co_che.py)      kiểm tra RDKit (co_che.kiem_tra)
        │
        ├─ dung_kich_ban_3d.py ──────→ <tên>.3d.json      (chế độ 3D)
        └─ hoc_ve/ve_theo_mau.py ────→ <tên>.giay.json    (chế độ giấy, vẽ theo kho mẫu + tư duy vẽ)

đáp án Word (ChemDraw) ─ hoc_ve/hoc_mau.py ─→ khung_mau_dap_an.json   (kho mẫu, dùng khi dựng .giay.json)
                       └ hoc_ve/bo_do.py ───→ bao_cao_bo_do.json      (CHỈ ADMIN)
```

App chỉ cần đọc `.3d.json` và `.giay.json`. Kho mẫu và bộ đo chạy ở phía Python (máy chủ VERA hoặc máy admin).

## 1. `<tên>.3d.json` – kịch bản 3D

| Trường | Kiểu | Ý nghĩa |
|---|---|---|
| `ten`, `giai_thich` | string | tên cơ chế, lời giải thích hướng phản ứng |
| `nguyen_tu` | `{id: {e}}` | mọi nguyên tử, kể cả H. `id` = `m<map>` (nguyên tử nặng) hoặc `H<map cha>_<k>`; `e` = ký hiệu nguyên tố |
| `trang_thai[]` | mảng | các khung hình chính, theo thứ tự |
| `trang_thai[].ten` | string | "Hai chất ban đầu", "Trung gian 1"… |
| `trang_thai[].toa_do` | `{id: [x,y,z]}` | toạ độ **Å**, hệ phải, tâm ≈ gốc. Unity (hệ trái): đổi dấu z |
| `trang_thai[].lien_ket` | `[[id,id,bậc]]` | bậc 1/2/3 |
| `trang_thai[].goc_tu_do` | `{id: số e}` | electron độc thân |
| `buoc[]` | mảng | `{tu, den, ten, ghi_chu, mui_ten[]}`: chuyển từ trạng thái `tu` sang `den` |
| `buoc[].mui_ten[]` | mảng | như co_che.py: `tu`/`den` = `{loai: cap_e|lien_ket|nguyen_tu, nguyen_tu: map|[map,map]}`, `kieu`: `day_du` (2 e) / `nua_dau` (1 e) |
| `loai`, `dieu_kien` | string | (18/11) loại cơ chế (SN2, E2, AE…), điều kiện chung |
| `trang_thai[].dien_tich` | `{id: số}` | (18/11) điện tích hình thức khác 0 (hiện ⊕/⊖ cạnh nguyên tử) |
| `buoc[].trang_thai_chuyen_tiep`, `buoc_cham`, `thuan_nghich` | bool | (18/11) bước đồng bộ có TS / giai đoạn chậm / cân bằng ⇌ |
| `buoc[].chuyen_h` | `[{tu, den}]` | (18/11) chuyển H⁺ rút gọn (map nguyên tử cho → nhận); H giữ nguyên id, chỉ dịch chỗ |
| `nhanh_sai[]` | mảng | (18/11, tuỳ bài) nhánh bị chặn – xem dưới |

Bước có `mui_ten` rỗng là bước "dàn cảnh": hai chất tiến lại gần, chất mới tham gia (H₂O, HSO₄⁻, Cl₂…), tách sản phẩm phụ.
Id chỉ có ở trạng thái `den` → nguyên tử hiện dần; chỉ có ở `tu` → mờ dần. Id `m<map>` cũng dùng cho H mang map riêng (`[H:6]`).

**Nhánh sai** `nhanh_sai[]`: `{tu_trang_thai, ten, ly_do, kieu_chan, nguyen_tu_them: {id: {e}}, toa_do: {id: [x,y,z]},
mui_ten[], man_chan: {tam: [x,y,z], phap_tuyen: [x,y,z], ban_kinh}}`. Cách phát: từ trạng thái `tu_trang_thai`, các nguyên
tử dịch tới `toa_do` (chất tấn công tiến lại), hạt electron theo `mui_ten` bay tới thì dừng ở màn chắn; màn chắn (đĩa tròn
bán trong, tâm `tam`, vuông góc `phap_tuyen`) **chỉ hiện khi đang phát nhánh sai**; hiện `ly_do`; rồi lùi về trạng thái gốc.
`kieu_chan`: `can_tro_khong_gian` (SN2 với dẫn xuất bậc ba), `khong_anti_periplanar` (E2 với H gauche).

Cách phát (đã chạy đúng trong bản thử `hsg_propellane.html`):
- Nội suy toạ độ giữa `tu` → `den` (electron đi trước, nguyên tử dịch từ ~35% thời gian bước).
- Liên kết có trong cả hai trạng thái → đổi bậc mờ dần; chỉ có ở `den` → mọc dần; chỉ ở `tu` → mờ dần.
- Mỗi mũi tên → 1 hoặc 2 hạt electron bay theo đường cong từ nguồn (giữa liên kết / chấm electron) tới đích.
- Nút "Ẩn H": thu nhỏ mọi nguyên tử `H…` và liên kết tới chúng.

## 2. `<tên>.giay.json` – sơ đồ trên giấy (tĩnh, kiểu đáp án)

Toạ độ 2D theo **độ dài liên kết = 1**, trục y hướng lên. Đáp án dùng 14,4 pt / liên kết (chuẩn ChemDraw).
Sơ đồ dài tự xuống hàng (bề rộng ~26 đơn vị), nên `y` có thể âm nhiều hàng; dùng `khung` để co giãn vừa khung nhìn.

| Trường | Kiểu | Ý nghĩa |
|---|---|---|
| `khung` | `[x0,x1,y0,y1]` | khung bao |
| `nt` | `{id: [x,y,"nhãn"?]}` | nút; không nhãn = C khung. Nhãn dùng chữ số chỉ số dưới Unicode (CO₂Me) |
| `lk` | `[[a,b,kiểu?,phía?]]` | kiểu 1/2/3 hoặc `"sau"` (nét phía sau, vẽ dưới, nét trước có khe che); `phía` = điểm để đặt nét đôi thứ hai; (18/11) `"dam"` nêm đặc / `"gach"` nêm gạch cho lập thể, đầu hẹp ở `a` |
| `goc` | `[[x,y]]` | chấm electron độc thân |
| `mt` | `[{tu,qua,den,kieu?}]` | mũi tên cong: đường Bézier bậc 2; (18/11) `kieu` `nua_dau` = một ngạnh (1 e), `day_du`/`vong` = hai ngạnh (2 e) |
| `pu` | `[{x1,x2,y,tren?,duoi?,thuan_nghich?}]` | mũi tên phản ứng, chữ trên/dưới (`duoi: "chậm"` = giai đoạn chậm); `thuan_nghich` → vẽ ⇌ |
| `dt` | `[[x,y,"⁺"]]` | (18/11) điện tích của C không có nhãn (cacbocation, ion arenium); dị tố ghi điện tích ngay trong nhãn (OH⁻, O⁺) |
| `chu` | `[[x,y,"+"]]` | (18/11) chữ thường khác: dấu "+" giữa các chất cùng trạng thái |
| `ten` | `[[x,y,"A1"]]` | nhãn chất |

Quy tắc vẽ nhãn, khe che, ngạnh mũi tên: xem [QUY_UOC_VE.md](QUY_UOC_VE.md) và `ve_giay.js` (bản thử web) – bản Unity
phải vẽ giống hệt.

## 3. `bao_cao_bo_do.json` – CHỈ ADMIN

Kết quả bộ đo "độ giống đáp án". Khi gắn vào VERA: thêm vai trò `quan_tri` (hiện chỉ có `giao_vien`, `hoc_sinh`), máy chủ
chỉ trả file này cho tài khoản `quan_tri`; app chỉ hiện mục báo cáo khi vai trò là `quan_tri`. Học sinh và giáo viên
không thấy điểm chấm, chỉ thấy hình.

## Khi gắn vào app (C2–C3 ngày 18/11)

1. Chép `.3d.json`, `.giay.json` vào `StreamingAssets/CoChe/` (hoặc tải từ máy chủ VERA).
2. Unity: lớp dữ liệu đọc hai file trên; trình phát 3D (cầu + trụ, hạt electron); vẽ sơ đồ giấy (LineRenderer / UI);
   nút chuyển 3D ⇄ Giấy; nút Ẩn H; ô "Giải thích".
3. Dựng Unity ở thư mục chính, không dựng trong `.claude/worktrees`.

## 4. Gói `.verapack` (C2, 18/11)

App không đọc `.3d.json`/`.giay.json` rời nữa mà đọc gói mã hóa `StreamingAssets/CoChe/<ma>.verapack` (hoặc tải từ máy chủ).
Tạo gói: `python -m server.ai.co_che.dong_goi --unity` (chép gói vào `Assets/StreamingAssets/CoChe/`, khóa vào
`Assets/Resources/CoChe/khoa.bytes`; cả hai git bỏ qua). Mã Python: `server/ai/co_che/dong_goi.py`.

```
"VERA1" (5 byte ASCII) | phiên bản (1 byte = 0x01) | salt (16) | IV (16) | bản mã AES-256-CBC, PKCS#7 (n·16) | HMAC-SHA256 (32)
khóa mã = HMACSHA256(khóa gốc 32 byte, ASCII "VERA-ma" ‖ salt)
khóa ký = HMACSHA256(khóa gốc 32 byte, ASCII "VERA-ky" ‖ salt)
HMAC = HMACSHA256(khóa ký, mọi byte từ đầu gói tới hết bản mã)
bản rõ = gzip(UTF-8 JSON) – JSON: {"phien_ban":1, "ma", "bai", "kich_ban_3d", "so_do_giay", "_d"}
```

Giải: kiểm độ dài và "VERA1" → tính HMAC, so bằng hàm thời gian hằng (CryptographicOperations.FixedTimeEquals hoặc tự viết
vòng XOR) → sai thì từ chối, KHÔNG giải mã → Aes.Create() (CBC, PKCS7) → GZipStream → JSON. Tất cả trong bộ nhớ.
`bai` = nội dung `bai.json` (đề, mục tiêu, các bước, 3 câu hỏi có đáp án, lỗi hay gặp, nguồn); `_d` = dấu ẩn (bỏ qua).
Gói thử cố định cho test C#: chạy `VERA_GHI_GOI_MAU=<tệp> python -m unittest tests.test_co_che_dong_goi` → khóa = byte 0..31,
salt = 0..15, IV = 16..31, JSON `{"ma":"mau","x":"Ặ ỷ ữ"}`.

## 5. Máy chủ cơ chế (C2, 18/11)

- Chạy: `python -m server.ai.co_che.may_chu` → **`http://127.0.0.1:8010`** (đổi bằng `VERA_CO_CHE_HOST`, `VERA_CO_CHE_PORT`).
  Cổng 8000 để cho app CHEMISTRY (LaunchAgent `vn.giaichuoihuuco.app` chạy `--port 8000`) và máy chủ lab `server/ai/main.py`.
- App lưu URL trong PlayerPrefs, mặc định `http://127.0.0.1:8010`. iPad/Windows trong LAN: chạy máy chủ với `VERA_CO_CHE_HOST=0.0.0.0`.

| Điểm cuối | Vào | Ra |
|---|---|---|
| `GET /co-che/suc-khoe` | – | `{ok, mo_hinh, muc, ram_gb, so_bai, rag:{so_doan, vector}}` |
| `GET /co-che/bai` | – | `[{ma, ten, loai, lop, de, so_buoc, nhanh_sai}]` |
| `GET /co-che/bai/{ma}` | – | gói `.verapack` (application/octet-stream); 404 nếu không có |
| `GET /co-che/chuoi-mau` | – | 3 chuỗi mẫu (giải được khi không có mạng) |
| `POST /co-che/giai-thich` | `{"cau_hoi", "ma_bai"?}` | `{tra_loi, nguon:[{so, tieu_de, nguon}], nha_cung_cap: tai_may|<trực tuyến>|mau, mo_hinh, kiem_chung, giay}` |
| `POST /co-che/giai-chuoi` | `{"chuoi": "C2H4 -> C2H5OH -> CH3CHO -> CH3COOH"}` | `{chat:[{ki_hieu, smiles, ten, ctct, cong_thuc, kiem_chung}], buoc:[{tu, den, phan_ung, dieu_kien, loai, ma_co_che?, khop_mau?, kiem_chung, goi?}], da_kiem_chung, ai}` |

`kiem_chung`: `da_kiem_chung` (bảng đã kiểm chứng) | `chua_kiem_chung` (do AI đưa ra – app ghi rõ "chưa kiểm chứng") |
`mau` (không có AI, trả lời soạn sẵn) | `mau_do_ai_truot` (AI đề xuất cơ chế trượt RDKit, đã thay bằng mẫu).
`ma_co_che` có giá trị → bước đó có cơ chế mẫu: app mở trình phát với gói `/co-che/bai/{ma_co_che}`; `khop_mau`:
`dung_mau` (đúng phản ứng của bài) | `cung_loai` (cùng kiểu cơ chế, khác chất – app ghi "cơ chế tương tự").
`/co-che/giai-thich` với mô hình 3B tại máy: 4–13 giây; app nên hiện "Đang suy nghĩ…" và đặt thời gian chờ ≥ 120 s.
