# PROMPT – Sửa lại & làm nốt 3 phần tối ưu: nút chuyển Hoá ⇄ Sinh · mục Cơ chế · ảnh sơ đồ chuỗi

> Dán nguyên file này cho Antigravity, hoặc gõ: "Đọc và làm theo PROMPT_TOI_UU_CHUYEN_MON_CO_CHE_CHUOI.md".
> Bối cảnh: commit `3ad555c` ("toi uu chuoi phan ung 5-6 chat, co che bat dong bo va segmented toggle hoa-sinh") mới làm **một phần**
> các yêu cầu trong ghi chú tay của người dùng, và có lỗi. File này liệt kê từng lỗi cần sửa cùng phần còn thiếu. Làm tiếp TRÊN commit đó, không quay lại bản cũ.

---

## DÀN Ý NHANH – người dùng yêu cầu gì, đã đạt chưa
| # | Ý người dùng (ghi chú tay) | Sau `3ad555c` | Việc phải làm |
|---|---|---|---|
| 1 | Nút chuyển Hoá học ⇄ Sinh học giống nút Chat ⇄ Code trong app Claude, có animation **mượt** (không cần quá đẹp) | Chỉ có ở trang Hoá. Bấm thì làm mờ trang rồi điều hướng cứng; trang Sinh vẫn là tab cũ. Bấm **Back** có thể ra **trang trắng** | Tách thành component dùng chung, chuyển cảnh liền mạch giữa hai trang, sửa lỗi Back, gửi đề xuất cho trang Sinh (mục 3) |
| 2a | Cơ chế: dựng hình **theo bố cục giống sơ đồ chuỗi**, mũi tên rõ hơn | Mới làm mũi tên dày/đậm hơn, bố cục chưa đổi | Mũi tên giữa các bước kiểu `.mui-chuoi`, xếp hàng theo bề rộng khung web, màu theo sáng/tối (mục 1.3) |
| 2b | Cơ chế: **xuất các bước trước** cho nhanh nhất, phần giải thích **chạy nền**, thêm **1 nút** để ai cần thì bấm xem | AI vẫn viết giải thích ngay trong lượt chính (không nhanh hơn). "Giải thích chi tiết" là **văn mẫu soạn sẵn** theo loại phản ứng, và luôn nhận danh sách bước rỗng | Lượt nhanh không có giải thích + giải thích bằng AI chạy nền thật + nút 4 trạng thái (mục 1.1–1.2) |
| 3① | Ảnh chất trong sơ đồ chuỗi: **đường viền hợp bố cục web** | Thẻ đẹp hơn, nhưng ảnh PNG nền trắng ở chế độ tối thành **hộp sẫm khác màu thẻ** | Ảnh SVG nền trong suốt, chỉ một lớp viền (mục 2) |
| 3② | Ảnh **không cần quá to, chỉ cần rõ**, **5–6 chất trên 1 hàng giống mẫu** | Ép ảnh xuống 48×94 px (điện thoại 38×78) → chất vừa/lớn **chữ quá nhỏ**. Ảnh PNG đáp án chưa sửa | Vẽ đúng cỡ nhỏ ngay từ đầu (không thu ảnh to), đạt 5–6 chất/hàng mà chữ vẫn rõ. Sửa cả ảnh PNG đáp án (mục 2) |

Thứ tự làm: **Cơ chế (mục 1)** → **Ảnh chuỗi (mục 2)** → **Nút chuyển (mục 3)** → kiểm tra (mục 4) → bàn giao (mục 5).

---

## 0. Việc phải làm trước khi viết code
1. Đọc **AGENTS.md**, **CLAUDE.md** (mục Cơ chế, Luồng giải chuỗi, Giao diện), phần đầu **HANDOFF.md**, rồi `git show 3ad555c --stat` và diff của commit đó.
2. Chạy `.venv/bin/python -m pytest -q` (lần gần nhất 593 PASS) và `node --test tests/js/` (25 PASS), ghi lại kết quả. Cuối việc, mọi test cũ vẫn phải PASS.
3. Tạo điểm phục hồi: `git add -A && git commit -m "chore: diem phuc hoi truoc khi sua toi uu chuoi, co che, nut chuyen mon"`.
4. Thư mục dự án nằm trong iCloud nên đọc file chậm. **Không** `grep -r` toàn repo (`outputs/` có hơn 1.000 thư mục). Grep đúng file rồi đọc theo khoảng dòng, ví dụ:
   `grep -n "giai_thich\|SCHEMA_CO_CHE\|SYSTEM_CO_CHE" backend/co_che.py`, sau đó `sed -n 555,640p backend/co_che.py`.
5. **Không sửa** vùng Lab Sinh học 3D: `frontend/biology-lab-3d*.html`, `frontend/sinh-hoc/`, `backend/sinh_hoc_ai.py`. Chỉ Claude được ghi vùng này. Phần trang Sinh của mục 3 phải gửi **đề xuất** (xem 3.3).
6. Không sửa `.env`, không in API key. Không `git push`, không chạy `dua-len-web.command` khi người dùng chưa đồng ý.
7. Test gọi AI phải **monkeypatch ở module provider** (`backend/ai/providers/gemini` / `claude_chat`…), **không** monkeypatch ở `llm`. Xem các test sẵn có trong `tests/test_co_che.py`.

---

## 1. Mục 05 Cơ chế
### 1.1 Lỗi của `3ad555c` (đọc để hiểu vì sao phải làm lại)
- `backend/co_che.py` → `giai_thich_chi_tiet_co_che()` là **văn mẫu cố định** theo loại phản ứng (SN1/SN2/ADE…), không do AI viết cho phản ứng cụ thể.
  Khoá `"ADE"` / `"ADN"` không khớp `LOAI_CO_CHE` (`"AE"` / `"AN"`). Nhánh mặc định là câu chung chung ("Trung tâm phản ứng tích điện trái dấu").
  Ở mức HSG, kiểu nội dung này là sai hoặc thừa. → **Xoá hàm này** cùng `_CACHE_GIAI_THICH`.
- `frontend/app.js` → `taiGiaiThichCoCheNen` đọc `d.co_che?.cac_buoc`, nhưng JSON trả về là `co_che.buoc` → **luôn gửi danh sách rỗng**,
  nên khung giải thích chỉ hiện lại `ghi_chu` cũ.
- Lượt AI chính **vẫn phải viết** `ghi_chu` + `giai_thich`: `SYSTEM_CO_CHE` mục 4 và `required` của `SCHEMA_CO_CHE` vẫn còn hai trường này.
  → Tốc độ **không hề nhanh hơn**. Đây đúng là điều người dùng muốn bỏ.
- `POST /api/co-che/giai-thich` dùng `gioi_han("co_che")` (10 lần/phút, 80 lần/giờ) → mỗi lần vẽ cơ chế tiêu **2 lượt** của giới hạn đắt.
  Cache là dict toàn cục không giới hạn kích thước (rò bộ nhớ), khoá dễ trùng, không gắn người dùng.
- Không có test nào cho phần mới (số test trước và sau commit như nhau: 593).

### 1.2 Làm đúng: "vẽ trước – giải thích chạy nền – bấm nút mới xem"
**A. Lượt nhanh (chỉ cấu trúc, để ra hình nhanh nhất)**
- Tạo `SCHEMA_CO_CHE_NHANH`: bỏ `giai_thich` (cấp cơ chế) và `ghi_chu` (cấp bước) khỏi `properties` lẫn `required`. Giữ nguyên mọi trường cấu trúc:
  `ten, loai, dieu_kien, nhan_hien_thi, buoc{chat, mui_ten, trang_thai_chuyen_tiep, buoc_cham, thuan_nghich, nhan_trung_gian, cong_huong, chuyen_h, dieu_kien}, san_pham`.
- `SYSTEM_CO_CHE`:
  - Bỏ mục 4 (ghi_chu) và gạch đầu dòng "giai_thich". Thêm câu: "KHÔNG viết lời giải thích; chỉ trả cấu trúc cơ chế."
  - Phần lập thể: giữ yêu cầu ghi đủ `@/@@`, `/\` trong SMILES. Bỏ vế "nêu rõ … trong ghi_chu", vì phần đó chuyển sang lượt giải thích.
- `tu_json` đã đọc bằng `.get`, nên thiếu hai trường vẫn chạy. Kiểm tra lại và thêm test. Vòng sửa bằng RDKit (`SO_LAN_SUA`) **giữ nguyên**.
- `effort` giữ `"high"`. Chỉ được thử `"medium"` nếu đo thấy tỉ lệ qua `kiem_tra` **không giảm** (xem phần đo bên dưới), và phải ghi số đo vào HANDOFF.md.
- **Đo tốc độ** (chỉ làm khi máy có key; KHÔNG chạy trong pytest): viết `tools/do_toc_do_co_che.py`, chạy 5 phản ứng, mỗi phản ứng 1 lần với bản cũ và bản mới:
  - SN2: CH₃CH₂Br + OH⁻
  - SN1: (CH₃)₃CBr + H₂O
  - E2: 2-bromobutane + EtO⁻
  - AE: propene + HBr
  - SEAr: nitro hoá benzene

  Ghi vào HANDOFF.md: thời gian trung vị, số token đầu ra, tỉ lệ qua `kiem_tra`. Mục tiêu: **thời gian trung vị giảm ≥ 25 %**, tỉ lệ qua kiểm tra không giảm.

**B. Giải thích bằng AI, chạy nền** – module mới `backend/giai_thich_co_che.py`
- `bat_dau(cc_json: dict, de_bai: str, user_id: int) -> str` trả về mã `ma`. Các bước:
  - `bam` = sha256 của JSON cơ chế chuẩn hoá (`buoc` + `san_pham`, sắp khoá).
  - Đã có kết quả cho `bam` trong 7 ngày (của bất kì ai) → ghi một dòng cho user này, trạng thái `xong`, trả về ngay.
  - Chưa có → chạy nền bằng `backend/ai_rieng/song_song.py` → `gui_nen(...)` (hàm này giữ ContextVar).
- Trong luồng nền:
  - Bọc bằng `dung_khoa(tai_khoan.chon_khoa_3d(u))` + `dung_ke_hoach(KeHoachAI(loai="co_che", user_id=..., buoc="giai_thich_co_che", bac="flash"))`, để chi phí ghi đúng vào `ai_usage`.
  - **Không dùng Claude** (`claude=None`), để không tiêu lượt mã Claude của người dùng.
  - Gọi `PhienHoiThoai(SYSTEM_GIAI_THICH, output_schema=SCHEMA_GIAI_THICH, bac="flash", max_tokens≈6000, effort thấp)`.
  - Đầu vào là cơ chế **đã qua kiểm tra**: SMILES bỏ atom map cho dễ đọc, mỗi mũi tên viết thành chữ (ví dụ "cặp e của O(1) → C(3)"), kèm đề bài.
  - Đầu ra:
    `{"tong_quan": "2–4 câu: kiểu cơ chế, vì sao chọn hướng đó, sản phẩm chính/phụ", "buoc": [{"so": 1, "tieu_de": "≤ 60 kí tự", "dien_bien": "1–2 câu", "electron": "…", "lap_the": "" }]}`.
    Số phần tử `buoc` phải **bằng** số bước của cơ chế. Lệch thì nhờ AI sửa 1 lần; vẫn lệch thì cắt bớt hoặc bù phần tử rỗng.
  - Quá 90 s hoặc lỗi → trạng thái `loi`, kèm câu báo thân thiện bằng tiếng Việt.
- CSDL: thêm bảng `co_che_giai_thich(ma TEXT PRIMARY KEY, bam TEXT, user_id INTEGER, trang_thai TEXT, ket_qua TEXT, loi TEXT, tao_luc TEXT)`
  trong `backend/db.py`, theo đúng cách các bảng khác được tạo (xem `giai_cache`). Có index theo `bam`. Dọn bản ghi quá 7 ngày theo mẫu dọn cache sẵn có.
- API (`backend/main.py`):
  - `/api/co-che`: vẽ xong thì **tự gọi** `bat_dau(...)` (không chờ) và trả thêm `ma_giai_thich`. Bỏ trường `ghi_chu` cũ trong response, hoặc để rỗng.
  - `GET /api/co-che/giai-thich/{ma}` → `{trang_thai: "dang_chay" | "xong" | "loi", tong_quan?, buoc?, loi?}`. `ma` không thuộc người đang đăng nhập → **404**.
  - `POST /api/co-che/giai-thich/{ma}/thu-lai` (CSRF) → chạy lại khi đang `loi`.
  - Giới hạn tần suất: thêm khoá riêng `"co_che_gt"` vào `GIOI_HAN` trong `backend/bao_mat.py`, ví dụ `[(40, 60), (400, 3600)]` (frontend hỏi lại định kì). **Không** dùng `gioi_han("co_che")`.
  - **Xoá** `POST /api/co-che/giai-thich` cũ và class `CoCheGiaiThichVao`.

**C. Frontend (`frontend/app.js`, `index.html`, CSS)**
- Nút **"Giải thích từng bước"**, mặc định khung đóng, có 4 trạng thái:
  1. "Đang chuẩn bị giải thích…" kèm vòng quay nhỏ. Vẫn bấm được: bấm thì mở khung chờ, có kết quả là tự hiện.
  2. "Giải thích từng bước ▾" khi đã sẵn sàng.
  3. "Ẩn giải thích ▴" khi đang mở.
  4. "Không tạo được giải thích – Thử lại" khi lỗi.
- Hỏi trạng thái mỗi 1,5 s. Dừng khi `xong`/`loi`, khi rời mục 05, hoặc khi vẽ cơ chế mới.
  Dùng `AbortController` và so `ma`, để kết quả của cơ chế cũ **không đè** lên cơ chế mới.
- Khung giải thích gồm `tong_quan` rồi các thẻ bước. Số bước khớp số "(1)", "(2)"… trên hình.
  Trường nào rỗng thì **không hiện** (bỏ các chữ thay thế giả như "Theo trật tự bền vững", "Trung tâm ái nhân / ái điện tử").
- Mọi chữ do AI viết đều phải qua hàm escape sẵn có (`esc` / `ctHTML`), không chèn `innerHTML` thô.

### 1.3 Bố cục hình cơ chế "giống sơ đồ chuỗi" + mũi tên rõ (`backend/ve_co_che.py`)
- Mũi tên phản ứng giữa các bước vẽ giống `.mui-chuoi` của mục 03:
  - **Phía trên** mũi tên: số bước `(1)`, `(2)`… và nhãn ngắn (điều kiện riêng của bước, hoặc "chậm" nếu `buoc_cham`).
  - **Phía dưới**: `nhan_trung_gian`, chữ nhỏ nghiêng.
  - Thân mũi tên dài tối thiểu khoảng 2,2 đơn vị liên kết, đầu tam giác rõ. Bước thuận nghịch dùng ⇌.
- Mũi tên electron: giữ nét 1,8 và đầu to như `3ad555c`. Thêm điều kiện **không đè lên chữ nguyên tử**: dời điểm đầu/cuối ra khỏi vòng bán kính nhãn.
  Nếu mũi tên cắt qua nhãn khác thì đổi chiều cong.
- Xếp hàng theo **bề rộng khung web**:
  - Frontend gửi `rong_khung` (px, kẹp trong 480–1400) trong body `/api/co-che`. Backend dùng giá trị này thay cho `RONG_TOI_DA`, mặc định vẫn là 1080.
  - Xuống hàng thì **hàng mới bắt đầu bằng mũi tên của bước kế tiếp**, không để mũi tên treo ở cuối hàng trên.
- Màu theo sáng/tối, **không dùng `filter: invert`** cho hình cơ chế:
  - Nét và chữ chính dùng `currentColor`. Mũi tên electron dùng `style="stroke:var(--cc-mui-e,#b91c1c);fill:…"` (có màu dự phòng, để file SVG tải về vẫn đúng màu).
  - Màu nguyên tử (O đỏ, N lam…) chọn tông đọc được trên cả hai nền.
  - Bỏ dòng `:root[data-theme="toi"] .khung-co-che svg { filter: invert(...) }` trong `style.css`.
  - Định nghĩa `--cc-mui-e` cho sáng và tối ở cuối `style.css`.
- `taiPngCoChe` đang vẽ nền trắng, và `currentColor` trong SVG đứng riêng mặc định là đen, nên sẽ đúng. Kiểm tra lại "Tải PNG", "Tải SVG", "Tải CDXML" vẫn ra file đúng.
- `xuat_cdxml` vẫn phải chạy với hình học mới (chạy lại test cũ trong `tests/test_co_che.py`).

### 1.4 Test bắt buộc – `tests/test_co_che.py` (thêm) và `tests/test_giai_thich_co_che.py` (mới)
- Lượt nhanh: schema gửi cho AI không còn `ghi_chu`/`giai_thich`. JSON thiếu hai trường vẫn `tu_json` + `kiem_tra` + `ve` được.
- `/api/co-che` (AI giả) trả `ma_giai_thich`. `GET` lần đầu là `dang_chay`; sau `cho_nen_xong()` là `xong` và có đủ số bước.
- Người dùng khác gọi `GET` cùng `ma` → 404. Chưa đăng nhập → 401.
- Cùng cơ chế vẽ lần hai → dùng lại kết quả, **không gọi AI giải thích lần nữa** (đếm số lần gọi provider giả).
- AI giải thích ném lỗi → `loi`; gọi `thu-lai` → `xong`.
- Gọi `GET` nhiều lần không làm giảm lượt `gioi_han("co_che")`.
- SVG có chuỗi `(1)` ở mũi tên phản ứng, có `currentColor`, không còn bộ lọc invert trong CSS.

---

## 2. Mục 03 – ảnh chất trong sơ đồ chuỗi + ảnh PNG đáp án
### 2.1 Hiện trạng sau `3ad555c`
- Ảnh chất là PNG vẽ bằng `renderer.ve_mol` (BOND_PX 40, chữ nguyên tử 22 px, **nền trắng**, `_cat_vien` chừa lề 14 px).
  CSS ép ảnh xuống `max-height: 48px; max-width: 94px` (điện thoại 38×78) → chất vừa/lớn bị thu còn khoảng 30–45 % → chữ nguyên tử chỉ còn khoảng 7–10 px, khó đọc.
- Chế độ tối dùng `invert(0.92) hue-rotate(180deg)` trên PNG nền trắng → thành **hộp sẫm** khác màu nền thẻ `var(--giay-noi)`, nhìn như viền chồng viền.
- `backend/image_export.py` → `_ve_so_do` (ảnh PNG đáp án 1920 px) **chưa sửa**: liên kết 40 px, mũi tên rộng ≥ 170 px, nên chỉ được khoảng 3–4 chất/hàng.

### 2.2 Làm đúng
- **SVG nền trong suốt, vẽ đúng cỡ nhỏ ngay từ đầu** (không vẽ to rồi thu):
  - `backend/renderer.py` thêm `ve_svg_phan_tu(smiles, bond_px=22, cong_thuc="", mau=None) -> str | None`, dùng `rdMolDraw2D.MolDraw2DSVG`.
    Cùng các bước chuẩn bị như `ve_mol` (`chuan_bi_mol`, hướng theo chất mẫu, viết tắt, bảng màu nguyên tử). `fixedFontSize` khoảng 12–13 px.
  - **Không tô nền** (`clearBackground = False`, không có `<rect>` trắng phủ cả hình). Cắt khung sát hình vẽ (viewBox theo bbox + lề khoảng 4 px).
- `backend/pipeline.py` (chỗ `luu_anh_phan_tu`, khoảng dòng 133): lưu thêm `chat/<ten>.svg` và cho `anh[smiles]` trỏ tới **.svg**. Giữ `.png` cho ZIP và cho tải xuống.
  Kết quả cũ trong `giai_cache` (chỉ có .png) vẫn phải hiển thị bình thường.
- `GET /api/tep/...`: với file `.svg` trả `Content-Type: image/svg+xml`, thêm `Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'` và `X-Content-Type-Options: nosniff`.
- CSS (thêm hoặc sửa ở cuối `style.css`; giữ các class đang dùng):
  - **Chỉ một lớp viền** là thẻ `.nut-chuoi`: `border-radius: var(--r)` hoặc 10 px, `1px solid var(--chi)`, nền `var(--giay-noi)`. Ảnh không có nền hay viền riêng.
  - Vùng ảnh **cao đều**: desktop 60 px, điện thoại 46 px, `object-fit: contain`, căn giữa dọc, để hàng thẳng.
  - Ảnh hiển thị **gần đúng cỡ gốc**: `max-width` khoảng 150 px (điện thoại 110 px). Không cho tỉ lệ hiển thị `clientWidth / naturalWidth` < 0,7.
    Chất rất lớn được chiếm thẻ rộng hơn và tự xuống hàng. **Rõ quan trọng hơn số chất/hàng.**
  - Chế độ tối: `filter: invert(1) hue-rotate(180deg)` áp lên ảnh SVG **trong suốt**. Nền trong suốt nên không còn hộp sẫm; màu O/N vẫn đỏ/lam.
  - `.mui-chuoi` gọn: rộng khoảng 64–90 px, chữ tác nhân/điều kiện 0,72 rem và tự xuống dòng, số bước `(1)` ở trên.
- **"Giống mẫu"**: nếu có ảnh mẫu của người dùng trong `docs/mau/` thì bám theo ảnh đó. Chưa có thì theo kiểu sơ đồ chuỗi trong đề thi/SGK:
  chất → mũi tên có số (1), tác nhân ở trên, điều kiện ở dưới → chất…
- **Mục tiêu đo được** (desktop 1440×900):
  - Chuỗi 6 chất nhỏ–vừa (ví dụ C₂H₅OH → CH₃CHO → CH₃COOH → CH₃COOC₂H₅ …) nằm **≥ 5 chất trên 1 hàng**.
  - Chuỗi có vòng benzene, hoặc chất 10–12 nguyên tử nặng, vẫn **đọc rõ chữ nguyên tử**.
  - Điện thoại 390×844: 2–3 chất/hàng, **không tràn ngang trang**.
- **Ảnh PNG đáp án** (`image_export._ve_so_do`):
  - Liên kết trong sơ đồ khoảng 28 px (thay 40), cỡ chữ điều kiện 22 → 18, mũi tên tối thiểu 170 → 120 px.
  - Mỗi chất đặt trong thẻ bo góc 10 px, viền `MAU_VIEN` 2 px, giống thẻ trên web.
  - Mục tiêu 5–6 chất nhỏ–vừa trên một hàng ở khổ 1920 px, chữ vẫn rõ khi in A4. Giữ bố cục "rắn lượn" sẵn có.
  - Tách phần xếp hàng thành hàm thuần (ví dụ `_xep_hang_so_do(muc, rong)` trả danh sách hàng) để test được.
- Test (`tests/test_renderer.py` + test mới):
  - `ve_svg_phan_tu` trả chuỗi bắt đầu `<svg` / `<?xml`, không có `<rect` nền trắng phủ cả hình, SMILES sai → None.
  - Pipeline (AI giả) lưu cả `.svg` lẫn `.png`, và `anh[...]` kết thúc bằng `.svg`.
  - `/api/tep` trả file `.svg` với đúng 3 header nêu trên.
  - `_xep_hang_so_do` với 6 chất nhỏ ở khổ 1920 px → **1 hàng**.

---

## 3. Nút chuyển Hoá học ⇄ Sinh học (kiểu Chat ⇄ Code của Claude)
### 3.1 Hiện trạng sau `3ad555c`
- `.segmented-pill-toggle` chỉ có trong header `frontend/index.html`. Mã nằm trong `app.js` → `khoiTaoTogglePhanHe`.
- Bấm "Sinh học": đặt `opacity: 0` + `translate3d(-20px,0,0)` thẳng lên `<main>`, chờ 200 ms rồi `location.href = "/sinh-hoc"`.
  Trang Sinh có header khác hẳn (tab cũ "Hoá học | Sinh học 3D", dòng khoảng 1793 của `biology-lab-3d.html`, chuyển cứng bằng `location.href='/'`).
  → Chuyển cảnh không liền mạch như Chat ⇄ Code.
- **Lỗi:** quay lại bằng nút **Back** (trình duyệt khôi phục trang từ bfcache) → `<main>` vẫn `opacity: 0` → **trang trắng**.
- Không có `prefers-reduced-motion`. Không có đề xuất nào cho trang Sinh trong `.agents/hang-doi/sinh-hoc/`.

### 3.2 Làm đúng
- Tách thành component dùng chung, không phụ thuộc `app.js`: `frontend/chuyen-mon.js` (IIFE, `window.ChuyenMon`) + `frontend/chuyen-mon.css`.
  Tách file CSS riêng để trang Sinh nạp được mà không cần cả `style.css`. Màu lấy từ biến CSS của trang, có màu dự phòng.
  Gỡ `khoiTaoTogglePhanHe` + CSS `.segmented-pill-*` cũ, hoặc đổi chúng thành lớp mỏng gọi component.
- API: `ChuyenMon.gan(phanTu, { hienTai: "hoa" | "sinh", urlHoa: "/", urlSinh: "/sinh-hoc", truocKhiRoi: () => {} })`.
  Giữ `role="tablist"` + 2 `role="tab"`, `aria-selected`, phím ←/→, Enter/Space, vòng focus rõ.
- Chuyển cảnh (**mượt quan trọng hơn đẹp**):
  1. Viên nền trượt sang bằng `transform: translate3d(...)` 240 ms, `cubic-bezier(0.22, 1, 0.36, 1)`, có `will-change`.
  2. Trình duyệt hỗ trợ View Transitions khi điều hướng giữa hai trang (Chrome/Edge ≥ 126, Safari ≥ 18.2):
     - Khai báo `@view-transition { navigation: auto; }` ở **cả hai trang**.
     - Nút đặt `view-transition-name: chuyen-mon` để **đứng yên** khi chuyển.
     - Phần còn lại mờ chéo + trượt nhẹ 12 px theo hướng chuyển (`::view-transition-old(root)` / `::view-transition-new(root)`, 260 ms).
     - Điều hướng ngay khi viên trượt gần xong (khoảng 180 ms).
  3. Trình duyệt không hỗ trợ: lớp phủ màu nền trang mờ lên 160 ms rồi điều hướng. Trang mới bắt đầu có lớp phủ, mờ đi sau `DOMContentLoaded`.
  4. **Không** đặt style trực tiếp lên `<main>` nữa. Nghe `pageshow` (khi `event.persisted`) để xoá lớp phủ và trả viên nền về đúng vị trí. Đây là cách sửa lỗi trang trắng khi Back.
  5. `prefers-reduced-motion: reduce` → không trượt, không mờ, chuyển ngay.
  6. Hover/focus vào lựa chọn còn lại → thêm `<link rel="prefetch">` tới trang kia (chỉ một lần).
  7. Trang Sinh trước khi rời: gọi `pauseRendering()` qua `truocKhiRoi`.
- Vị trí: **cùng chỗ, cùng kích thước** trên cả hai trang (header, ngay sau logo), để khi chuyển trang nút đứng yên đúng chỗ.
  Màn < 360 px: chỉ icon + chữ ngắn "Hoá" / "Sinh".
- Bên Hoá: gắn component ở `frontend/index.html`. Vẫn giữ banner và liên kết Sinh học 3D trong trang (lối vào cho điện thoại, rút kinh nghiệm lỗi ngày 22/10).

### 3.3 Trang Sinh – CHỈ GỬI ĐỀ XUẤT, không sửa
- Tạo `.agents/hang-doi/sinh-hoc/<NAMTHANGNGAY-GIOPHUT>-gemini-nut-chuyen-mon.md` theo **đúng mẫu** trong `.agents/hang-doi/sinh-hoc/README.md`
  (Mục tiêu / File & vị trí / Code đề xuất / Cách kiểm tra), cho cả `frontend/biology-lab-3d.html` (nav `tab-chinh` khoảng dòng 1793, `chonTab` ở mục "20. TAB HOÁ HỌC ↔ SINH HỌC" khoảng dòng 6714)
  và `frontend/biology-lab-3d-v2.html` (khoảng dòng 334 và 4730). Nội dung đề xuất:
  - Nạp `chuyen-mon.css/js`.
  - Thay `<nav class="tab-chinh">` bằng component với `hienTai: "sinh"`, `truocKhiRoi: pauseRendering`.
  - Thêm `@view-transition`.
  - Giữ nguyên chế độ mở bằng `file://` (tab Hoá học hiển thị trong iframe như `chonTab` hiện có).
- Trong báo cáo cuối, nói rõ với người dùng: phía trang Sinh **cần nhờ Claude áp dụng đề xuất** thì chuyển cảnh mới liền mạch hai chiều.

### 3.4 Test – `tests/test_chuyen_mon.py`
- Trang chủ nạp `chuyen-mon.css` + `chuyen-mon.js`, có phần tử gắn component và `@view-transition`.
- `chuyen-mon.js` có xử lý `pageshow`, `prefers-reduced-motion`, và không đặt `style.opacity` lên `<main>`.
- File đề xuất có trong `.agents/hang-doi/sinh-hoc/` và đủ 4 mục của mẫu.

---

## 4. Kiểm tra bằng trình duyệt thật trước khi báo xong
- Desktop 1440×900 và điện thoại 390×844, cả Sáng lẫn Tối. Console không lỗi. Ảnh chụp lưu vào `outputs/anh-chup-toi-uu-ui/`.
- **Chuỗi**: dùng chế độ Demo hoặc kết quả đã lưu, xem 3 chuỗi (6 chất nhỏ; có benzene; có chất 10–12 nguyên tử nặng).
  - Đếm số chất/hàng (so `offsetTop`) và ghi lại.
  - Phóng to ảnh chụp để xác nhận chữ nguyên tử đọc được.
  - Chế độ tối không còn hộp sẫm quanh ảnh.
  - Tải ảnh PNG đáp án và chụp lại.
- **Cơ chế**: vẽ SN2 và AE.
  - Hình hiện trước; nút chuyển "Đang chuẩn bị…" → "Giải thích từng bước"; mở ra đủ số bước, khớp số trên hình.
  - Vẽ cơ chế khác khi giải thích cũ còn đang chạy → không bị đè.
  - Đổi sáng/tối: hình đổi màu đúng, không dùng invert.
  - Tải PNG/SVG/CDXML vẫn được.
- **Nút chuyển**:
  - Hoá → Sinh → nút **Back** → trang Hoá hiển thị đầy đủ (không trắng).
  - Bật giả lập `prefers-reduced-motion` trong DevTools (Rendering) → chuyển ngay.
  - Ghi lại cảm nhận độ mượt. Có thể chụp 3 khung giữa lúc chuyển hoặc đo trong tab Performance: không có khung nào > 50 ms trong lúc trượt.

## 5. Bàn giao
- Commit theo từng phần:
  1. Cơ chế, gồm backend + test.
  2. Cơ chế, gồm frontend + bố cục hình.
  3. Ảnh chuỗi + PNG đáp án.
  4. Nút chuyển + đề xuất cho trang Sinh.
  5. Tài liệu.
- Cập nhật **HANDOFF.md**: ĐÃ XONG / ĐANG LÀM / VIỆC TIẾP THEO / LỖI CÒN TỒN / CÁCH CHẠY. Ghi rõ đã sửa từng lỗi nào của `3ad555c`, kèm bảng tốc độ cơ chế trước/sau.
- Cập nhật **CLAUDE.md**:
  - Mục Cơ chế: `giai_thich_co_che.py`, bảng `co_che_giai_thich`, endpoint mới, giới hạn `co_che_gt`.
  - Mục Giao diện: `chuyen-mon.js/.css`.
  - Mục Luồng giải chuỗi: ảnh `.svg` trong `chat/`.
- Báo cáo ngắn: đã làm gì, file nào, cách chạy thử, ảnh chụp, số đo tốc độ, và việc cần Claude áp dụng ở trang Sinh.

## 6. Điều kiện "XONG"
- [ ] Cơ chế: lượt AI chính không còn viết giải thích, có số đo nhanh hơn (hoặc ghi rõ lí do nếu chưa đo được vì thiếu key).
- [ ] Cơ chế: giải thích do AI viết riêng cho từng phản ứng, chạy nền; nút 4 trạng thái hoạt động; đã xoá văn mẫu cũ.
- [ ] Cơ chế: test quyền (404 với người khác), cache và thử lại đều có.
- [ ] Hình cơ chế có mũi tên giữa các bước kiểu sơ đồ chuỗi `(1)`, `(2)`…, mũi tên electron không đè chữ, xếp hàng theo bề rộng khung, đổi màu sáng/tối không dùng invert.
- [ ] Sơ đồ chuỗi: ảnh SVG nền trong suốt, một lớp viền, đạt ≥ 5 chất/hàng với chất nhỏ–vừa ở desktop mà chữ vẫn rõ, điện thoại không tràn.
- [ ] Ảnh PNG đáp án đã gọn theo cùng kiểu.
- [ ] Nút Hoá ⇄ Sinh là component dùng chung, chuyển mượt (View Transitions + dự phòng), không còn lỗi trang trắng khi Back, có reduced-motion.
- [ ] Đề xuất cho trang Sinh đã nằm trong hàng đợi.
- [ ] Toàn bộ pytest + `node --test` PASS. HANDOFF.md và CLAUDE.md đã cập nhật. Đã commit. CHƯA push hoặc deploy.
