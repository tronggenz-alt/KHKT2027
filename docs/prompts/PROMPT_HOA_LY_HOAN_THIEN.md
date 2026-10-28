# PROMPT – HOÀN THIỆN MỤC HOÁ LÝ: phóng to hình bản gốc + 3 mô phỏng còn lại + mục "Công thức Hoá lý" có nút "Chứng minh"

> Dán nguyên file này cho Antigravity (Gemini). Làm liên tục đến hết Giai đoạn 6, tự chủ theo AGENTS.md.
> Viết bởi Claude ngày 25/10/2026. Trước khi viết, Claude đã:
> - đọc mã nguồn Hoá lý (các commit 4532448 → ca5d165),
> - đo hình bằng trình duyệt thật,
> - tra cứu các mô hình 2D trên mạng,
> - tự tính bảng số chuẩn ở mục 11.
>
> Prompt này thay cho mục "VIỆC TIẾP THEO – Phát triển tiếp các mô phỏng" đang ghi trong HANDOFF.md.

---

## 0. Trước khi làm (bắt buộc)

1. Đọc `AGENTS.md`, `CLAUDE.md` (mục "## Hoá lý"), `HANDOFF.md` và `PROMPT_HOA_LY_DONG_CO_NHIET_2D.md`. Mọi quy ước của prompt cũ vẫn giữ nguyên, trừ những chỗ prompt này ghi đè.
2. Chạy `git status`.
   - Nếu còn thay đổi chưa commit của việc khác: **không** revert, **không** sửa đè.
   - Tạo điểm phục hồi: `git add -A && git commit -m "chore: diem phuc hoi truoc hoan thien hoa ly"`.
3. Không đụng vùng Phòng Lab Sinh học 3D. Không sửa `.env`. Không `git push`.
4. Xem ảnh hiện trạng trong `.agents/tham-chieu/hoa-ly-hien-trang/`:
   - `01-anh-nguoi-dung-hinh-nho.png`: ảnh người dùng gửi, kèm lời phàn nàn "hãy làm cho cái hình bự hơn".
   - `02-do-1280x800-hat-lech-ngoai-xi-lanh.png`: ảnh Claude chụp. **Hạt khí bị vẽ ra ngoài xi-lanh**, bên trái nắp.
   - `03-do-390x844.png`: bản di động.
5. Máy chủ web: `http://127.0.0.1:8000`, chế độ cục bộ, không cần đăng nhập.
   - Nếu máy chủ đang tắt (lúc 20:50 ngày 25/10 nó đã tắt) thì **bấm đúp `bat-tat.command` đúng MỘT lần** để bật lên.
   - Hoặc tự chạy uvicorn ở một cổng khác cho việc kiểm thử.
6. Máy làm việc: M2, RAM 8 GB, ổ đĩa gần đầy, thư mục dự án nằm trong iCloud.
   - Không `grep -r` toàn repo; chỉ grep theo từng file.
   - Không tải trình duyệt Playwright mới. Dùng `channel="chrome"` như `tools/kiem_tra_hoa_ly.py`, vì bản Chromium của Playwright trong máy đã lệch phiên bản.

---

## 1. Dàn ý (yêu cầu của người dùng)

| # | Người dùng nói | Cách hiểu để làm |
|---|---|---|
| 1 | "Hãy làm cho cái hình bự hơn" (ảnh 01) | **Giai đoạn 0:** phóng to hình của mô phỏng Động cơ nhiệt và sửa các lỗi đo được ở mục 2. Khung hình mới (`chung/`) là nền cho cả 3 mô phỏng mới. |
| 2 | "Nốt tất cả các dự án Hoá lý còn lại theo dạng mô phỏng giống bản gốc" | Làm đủ 3 thẻ đang "Sắp ra mắt" trong `danh-muc.json`: **Khí lý tưởng**, **Cân bằng hoá học & Le Chatelier**, **Tốc độ phản ứng & năng lượng hoạt hoá**. Mỗi cái có: hình SVG lớn (đồ thị + bộ dụng cụ), kéo chuột điều khiển, bảng điều khiển bên phải nhiều tab, thẻ "Trạng thái hiện tại", câu hỏi 3 dạng, sáng/tối, bản di động. |
| 3 | "Có mô hình 2D (có thể tra cứu trên mạng)" | Mô hình 2D bằng SVG + canvas hạt. Kết quả tra cứu ở mục 3. Không chép mã có giấy phép GPL hoặc NC. |
| 4 | "Thêm 1 mục có công thức Hoá lý" | **Hai lối vào, dùng chung MỘT nguồn dữ liệu** `cong-thuc.json`: (a) tab "Công thức" trong từng mô phỏng (gồm cả Động cơ nhiệt), (b) trang riêng `/hoa-hoc/hoa-ly/cong-thuc`, xuất hiện thành một thẻ trên trang Hoá lý. |
| 5 | "1 nút (Chứng minh) để học sinh nhấn vào có thể hiểu rõ công thức hơn" | Mỗi công thức có nút **Chứng minh**. Bấm vào thì mở hộp lời giải từng bước (lời giải thích + biểu thức MathJax), có hình minh hoạ cho các bước then chốt, rồi "Kiểm tra bằng số" và nút "Thử trong mô phỏng". Chi tiết ở mục 5. |

---

## 2. Hiện trạng Claude đã đo (Playwright, Chrome, 25/10/2026)

**Kích thước** (đơn vị px trên màn hình):

| Màn hình | Khung SVG | Đường chu trình p–V | Ghi chú |
|---|---|---|---|
| 1280×800 | 811×640 | **149×232** | Đồ thị chỉ chiếm khoảng 7 % diện tích khung. Chữ trục nhỏ cỡ 9,4 px. |
| 1440×900 | 811×640 | 149×232 | Bị chặn bởi `max-w-7xl` và `max-h-[640px]`. |
| 390×844 | 372×465 | **95×148** | Gần như không đọc được. |

**Nguyên nhân (trong `dong-co-nhiet/ve.js` và `trang.html`)**

1. `viewBox="0 0 620 680"` có dạng **dọc**, nhưng lại được đặt vào khung **ngang** 811×640 với `meet`. Kết quả là hình bị thu nhỏ còn 0,94 lần và thừa khoảng 113 px trống mỗi bên.
2. `S_px = 130` (hành trình pít-tông) bị viết cứng. Trục V của đồ thị khoá theo tỉ lệ của pít-tông, nên toàn bộ dải V từ 0 đến 447 cm³ chỉ dài khoảng 145 px, trong khi khung đồ thị rộng 490 px. Hơn 60 % khung bỏ trống.
3. Vùng đồ thị chỉ cao 250/680. Phía dưới hình còn một khoảng trống khoảng 80 px.

**Các lỗi khác đã thấy**

4. **Hạt khí vẽ ra ngoài xi-lanh** (ảnh 02, các hạt ở x ≈ 135–200 px, bên trái nắp). Lý do: canvas phủ `inset-0` theo kích thước khung, còn SVG bị co kiểu `meet` nên hai hệ toạ độ lệch nhau.
   HANDOFF ghi "đã kiểm tra trực quan bằng mắt 6 ảnh", nhưng lỗi này vẫn lọt. Từ nay phải dùng **assert bằng số** (mục 11.6).
5. Vạch chia trục là số lẻ (109, 87, 65, 44, 22). Nhãn "0" và "45" chồng lên nhau. Nhãn điểm "0" đè lên vạch 0.
6. Có nhiều màu viết cứng (`#ef4444`, `#64748b`, `#334155`, `#f59e0b`, `rgba(56,189,248,…)`) nên chế độ tối sai màu. Phải đổi sang token CSS.
7. `capNhat` gán `innerHTML` **ở mỗi khung hình** (`ve.js` khoảng dòng 510–612: van, nhãn, chấm trạng thái, pít-tông, bánh đà). Điều này vi phạm quy tắc "không tạo phần tử mới mỗi khung hình" ở mục 6.7 của prompt cũ.
8. Thanh truyền bị bánh đà che. Bánh đà to hơn mức cần thiết so với xi-lanh.
9. MathJax 3.2.2 đã có trong `frontend/vendor/mathjax.3.2.2.js`, nhưng **chưa có font CHTML offline**. Trong app máy tính, công thức sẽ hiện sai font (xem cách xử lý ở mục 5.4).

---

## 3. Tra cứu mô hình 2D (Claude, 25/10/2026) và quyết định

| Nguồn | Có gì | Giấy phép, cách dùng |
|---|---|---|
| PhET **Gas Properties** (phet.colorado.edu, repo `phetsims/gas-properties`) | Hộp khí có bơm, nắp kéo, bếp nung/làm lạnh, đồng hồ áp suất, giữ hằng số T/p/V, phân bố tốc độ | File HTML đã đóng gói: CC BY 4.0. **Mã nguồn riêng của mô phỏng: GPLv3** → chỉ học ý tưởng tương tác, **không chép mã** |
| PhET **Reactions & Rates** | Va chạm hạt, giản đồ năng lượng có thể kéo, bắn phân tử | Bản Java cũ (CC BY 3.0 hoặc GPL) → chỉ tham khảo ý tưởng |
| Concord Consortium **Next-Generation Molecular Workbench** (mw.concord.org, Lab framework MD2D) | Mô hình động lực học phân tử 2D trên HTML5 | Họ ghi là "free and open source" nhưng Claude **chưa xác minh được tệp LICENSE** → chỉ tham khảo |
| Thí nghiệm **ống tiêm NO₂/N₂O₄** (RSC Education, Purdue ChemEd demo 16.2, ILPI "P and V Effects on N₂O₄ Equilibrium") | Mô tả: nén pít-tông thì màu nâu **đậm lên ngay** (do nồng độ tăng), rồi **nhạt dần** khi cân bằng chuyển sang N₂O₄. Ngâm nước đá thì nhạt màu, nước nóng thì nâu đậm | Đây là mô tả thí nghiệm, **không có mô phỏng hạt mã nguồn mở nào** → tự dựng (cơ hội làm hay hơn) |
| LearnChemE (thermodynamics, kinetics, equilibrium) | Wolfram Player | CC BY-NC-SA → chỉ tham khảo cách trình bày |

**Quyết định**
- Tự dựng tất cả bằng **SVG + Canvas 2D + Pointer Events + JS thuần**, không thêm thư viện hay CDN mới.
- **Ngoại lệ duy nhất:** MathJax 3.2.2. Thư viện này đã có sẵn trong vendor và `_trang()` đã biết đổi URL CDN của nó sang bản offline. Phải dùng đúng URL `https://cdn.jsdelivr.net/npm/mathjax@3.2.2/es5/tex-chtml.js`.
- Gemini được phép tra cứu thêm trên mạng để lấy ý tưởng và số liệu. Nguồn số liệu nào dùng thì ghi vào trường `nguon` trong JSON, và thêm nguồn tham khảo vào `GHI_CONG.md`.

---

## 4. Kiến trúc chung: khung "giống bản gốc" cho mọi mô phỏng

### 4.1 Thư mục dùng chung `frontend/hoa-hoc/hoa-ly/chung/`

Mỗi file viết theo dạng UMD giống `tinh-toan.js`. Phần nào không đụng DOM thì phải chạy được trong Node để test.

| File | Vai trò |
|---|---|
| `dinh-dang.js` (`HLDinhDang`) | Định dạng số kiểu Việt Nam (dấu phẩy thập phân, `·10⁻³`, đơn vị). Hàm `vachDep(min, max, soVach)` chia vạch theo quy tắc 1–2–5 cho trục tuyến tính và trục log. |
| `ngau-nhien.js` (`HLNgauNhien`) | Bộ sinh số ngẫu nhiên **có seed** (mulberry32): `deu()`, `chuan()`, `maxwell3D(m, kT)` (độ lớn vận tốc), `poisson(λ)`. Test và mô phỏng hạt đều phải tái lập được kết quả. |
| `hat.js` (`HLHat`) | Động cơ hạt 2D: nhiều loại hạt (khối lượng, bán kính, màu là token), va chạm thành (thành có thể di chuyển), va chạm hạt–hạt đàn hồi (lưới ô, tuỳ chọn), đếm xung lực lên thành, đếm va chạm theo năng lượng pháp tuyến, hàm gọi lại `khiVaCham(a, b, E_n)`. **Lõi không đụng DOM.** |
| `ve-hat.js` (`HLVeHat`) | Vẽ hạt lên **một canvas đặt chồng khít lên SVG**: lấy `svg.getScreenCTM()` để quy đổi toạ độ viewBox ra px, tính lại khi `resize`, và **cắt (clip) theo vùng chứa khí**. Đây là cách sửa lỗi 4. |
| `do-thi.js` (`HLDoThi`) | Đồ thị SVG: trục tuyến tính/log, vạch đẹp, **tự tránh nhãn chồng nhau** (bỏ nhãn cách nhau < 28 px; nhãn điểm tự né vạch trục), lưới, đường chu trình/quá trình, tô diện tích, bóng ma, **chấm trạng thái kéo được** (chiếu điểm lên đường, có cửa sổ liên tục như `tienDoTuDiemPV`), chú thích. Tạo phần tử một lần, sau đó chỉ cập nhật thuộc tính. |
| `keo.js` (`HLKeo`) | Pointer Events dùng chung: `setPointerCapture`, kéo theo góc (bánh xe, núm vặn), kéo theo trục, quán tính khi thả, `touch-action: none`, tay nắm ≥ 44 px trên di động, bàn phím (`role="slider"`, các phím ←/→ và Shift). |
| `bang-mau.js` (`HLMau`) | Đọc token bằng `getComputedStyle` và tự cập nhật khi đổi `data-theme`. `mauNhietDo(T, Tmin, Tmax)` dùng `color-mix`. `tronMau([{mau, trongSo}])` để trộn màu dung dịch hoặc khí theo nồng độ (dùng cho NO₂). **Cấm viết cứng mã hex trong mọi file `ve*.js`.** |
| `khung-trang.js` (`HLKhung`) | Dựng khung trang, chi tiết ở mục 4.2. |
| `cong-thuc.js` (`HLCongThuc`) | Tab Công thức, hộp Chứng minh, nạp MathJax (mục 5). |
| `cau-hoi.js` (`HLCauHoi`) | Bộ hiển thị câu hỏi 3 dạng TN THPT. Tách từ `dong-co-nhiet/giao-dien.js`, đọc `cau-hoi.json` của từng mô phỏng. |

**`khung-trang.js` (`HLKhung`) – khung trang dùng chung**
- Thanh trên: logo `chemistry:` + breadcrumb "Hoá học › Hoá lý › {ten}". Tên lấy từ `danh-muc.json` theo `id`.
- Nút sáng/tối, cùng khoá localStorage `giai-chuoi-giao-dien`.
- Nút xuất ảnh PNG/SVG, nút sao chép link chia sẻ.
- Bố cục 2 cột co giãn. Thêm nút **"Thu gọn bảng"** (ẩn cột phải để hình chiếm toàn bộ chiều ngang) và nút **"Toàn màn hình"** (Fullscreen API; khi bật, bảng điều khiển thành lớp nổi có thể ẩn/hiện).
- Bố cục tab bên phải.
- Ngăn kéo đáy trên di động.
- Hook `window.HoaLy` chuẩn hoá (mục 4.3).

### 4.2 Khuôn một trang mô phỏng (giống bản gốc, nhưng hình to)

```
frontend/hoa-hoc/hoa-ly/<id>/
  trang.html          ← chỉ có khung HTML + <script> (thứ tự: chung/* → mô hình → vẽ → giao diện)
  mo-hinh.js          ← UMD thuần = NGUỒN SỰ THẬT của mọi con số (window.HL_<Id>)
  ve.js               ← vẽ SVG riêng của mô phỏng (dùng HLDoThi, HLMau)
  giao-dien.js        ← nối điều khiển + hook window.HoaLy
  cau-hoi.json        ← 9 câu, 3 dạng
  <du-lieu>.json      ← số liệu (khí, hệ cân bằng, phản ứng…) có trường `nguon`
```

**Quy tắc kích thước hình (áp dụng cho MỌI mô phỏng, kể cả Động cơ nhiệt)**
- `max-w-7xl` → `max-w-[1600px]`. Cột hình chiếm `lg:col-span-8 xl:col-span-9`, cột bảng điều khiển chiếm phần còn lại (tối thiểu 360 px).
- **Khung hình không cố định tỉ lệ.** Chiều cao là `clamp(560px, calc(100vh - 210px), 960px)` trên desktop và `min(78vh, 720px)` trên di động.
- Dùng `ResizeObserver` để đặt **viewBox = đúng kích thước px của khung** (1 đơn vị SVG = 1 px CSS). Bố cục bên trong tính lại theo W×H thật. Chữ có cỡ thật:
  - nhãn trục ≥ 12 px,
  - tên trục 13 px,
  - nhãn điểm 13 px đậm,
  - nét đồ thị chính 2,5 px.
- Đồ thị chiếm **55–60 %** chiều cao khung; phần dụng cụ chiếm phần còn lại. Không được để trống quá 24 px ở bất kỳ mép nào.

### 4.3 Hook kiểm thử chuẩn (mọi mô phỏng)

```js
window.HoaLy = {
  id,                       // "khi-ly-tuong" …
  trangThai(),              // object số liệu hiện tại (mỗi mô phỏng ghi rõ các trường trong mục của nó)
  datThamSo(obj), datLai(), choi(), dung(), buoc(ms),
  toaDo: { <tayNam>(): {x, y} },   // toạ độ client của từng tay nắm kéo được, để Playwright kéo đúng chỗ
  boCuc(),                  // {khung:{w,h}, doThi:{x,y,w,h}, duong:{w,h}, vungKhi:{x,y,w,h}, coChu:{truc, nhanDiem}}
  hat()                     // [{x, y, loai}] theo toạ độ client (để kiểm hạt nằm trong vùng chứa)
};
```

### 4.4 Route chung (thay cho route riêng từng trang)

Trong `backend/main.py`, giữ nguyên `/hoa-hoc/hoa-ly` và thêm một route đặt **sau** các route cụ thể:

```python
@app.get("/hoa-hoc/hoa-ly/{ma}")
def trang_hoa_ly_mo_phong(request: Request, ma: str):
    """Trang mô phỏng / thư viện Hoá lý theo frontend/hoa-hoc/hoa-ly/danh-muc.json (chỉ mục trang_thai = "co")."""
```

- `ma` phải khớp `^[a-z0-9-]{2,40}$`, **có trong** `danh-muc.json`, `trang_thai == "co"`, và file `frontend/hoa-hoc/hoa-ly/<ma>/trang.html` phải tồn tại. Thiếu bất kỳ điều kiện nào → 404.
- Phải dùng `_trang_can_dang_nhap`.
- Route cũ `/hoa-hoc/hoa-ly/dong-co-nhiet` vẫn phải chạy. Có thể bỏ route riêng nếu route chung đã bao được, và test phải chứng minh điều đó.

### 4.5 `danh-muc.json`

- Đổi `trang_thai` của 3 mục sang `"co"` **khi giai đoạn tương ứng đã xong**.
- Thêm mục `{"id": "cong-thuc", "loai": "thu-vien", "ten": "Công thức Hoá lý", "bieu_tuong": "∑", "phu_de": "Công thức · chứng minh từng bước", …}`.
- Trang `trung-tam.js` hiển thị thẻ thư viện có kiểu riêng, đặt ở đầu lưới.
- Tên mọi mục vẫn **chỉ nằm trong** `danh-muc.json` (luật cũ). Test kiểm cả 5 trang.
- Mục `dong-co-nhiet` vẫn giữ `ten_tam: true`, vì người dùng sẽ tự đặt tên.

---

## 5. Mục "Công thức Hoá lý" và nút "Chứng minh"

### 5.1 Dữ liệu: `frontend/hoa-hoc/hoa-ly/cong-thuc.json` (nguồn duy nhất)

```json
{
  "phien_ban": 1,
  "chu_de": [{"id": "khi", "ten": "Khí lý tưởng"}, {"id": "nhiet-dong", "ten": "Nhiệt động học"},
             {"id": "nhiet-hoa", "ten": "Nhiệt hoá học"}, {"id": "dong-hoc", "ten": "Tốc độ phản ứng"},
             {"id": "can-bang", "ten": "Cân bằng hoá học"}],
  "cong_thuc": [{
    "id": "pt-trang-thai", "chu_de": "khi", "ten": "Phương trình trạng thái khí lý tưởng",
    "latex": "pV = nRT",
    "ky_hieu": [{"k": "p", "y": "áp suất", "dv": "Pa"}, …],
    "dieu_kien": "Khí loãng, áp suất thấp, nhiệt độ không quá thấp",
    "lop": "Vật lí 12 · Hoá 10", "muc_do": "sgk",
    "mo_phong": [{"id": "khi-ly-tuong", "query": "giu=T"}],
    "vi_du_so": {"ham": "HL_KhiLyTuong.theTich", "vao": {"n": 1, "T": 298.15, "p_bar": 1}, "ra": 24.79, "dv": "L", "sai_so": 0.01},
    "chung_minh": {
      "loai": "chung-minh | suy-luan-mo-hinh | dinh-luat-thuc-nghiem",
      "can_toan": "khong | dao-ham | tich-phan",
      "buoc": [{"loi": "…", "latex": "…", "to_mau": ["V'"], "hinh": "id-hinh-minh-hoa (tuỳ chọn)"}],
      "y_chinh": "1–2 câu chốt lại",
      "cach_2": {"ten": "…", "buoc": [ … ]}
    }
  }]
}
```

- `muc_do` nhận một trong ba giá trị:
  - `sgk`: có trong chương trình GDPT 2018,
  - `mo-rong`: HSG hoặc chuyên đề,
  - `dai-hoc`.
- Giao diện hiện nhãn theo `muc_do`. Lọc mặc định chỉ hiện `sgk` và `mo-rong`.
- **Định luật hoặc tiên đề** (ví dụ định luật I) thì để `loai: "dinh-luat-thuc-nghiem"`. Nút vẫn ghi "Chứng minh", nhưng nội dung là "Vì sao tin được?": giải thích ý nghĩa + cách kiểm chứng bằng mô phỏng, **không giả vờ chứng minh**.

### 5.2 Giao diện

**Tab "Công thức" trong mỗi mô phỏng**
- Liệt kê các công thức có `mo_phong` chứa id của mô phỏng đó.
- Thẻ công thức gồm:
  - tên,
  - biểu thức lớn (MathJax),
  - bảng ký hiệu và đơn vị,
  - điều kiện áp dụng,
  - nhãn mức độ,
  - một dòng **giá trị đang chạy trong mô phỏng**, nếu có (ví dụ p·V = 2,48 kJ = nRT ✓).
- Các nút: **Chứng minh** (nổi bật, màu `--nhan`) · **Ví dụ số** · **Thử trong mô phỏng**.

**Trang `/hoa-hoc/hoa-ly/cong-thuc` (thư mục `cong-thuc/trang.html`)**
- Thanh tìm kiếm: tìm cả tên, ký hiệu và nội dung bỏ dấu, ví dụ gõ "arrhenius" hoặc "dang nhiet".
- Bộ lọc theo chủ đề và mức độ. Mục lục bên trái.
- Lưới thẻ công thức giống tab ở trên.
- Liên kết thẳng tới một công thức: `#pt-trang-thai`. Có thêm `?chung-minh=pt-trang-thai` để mở luôn hộp chứng minh.

**Hộp "Chứng minh"**
- Trên desktop: ngăn kéo bên phải rộng 560 px. Trên di động: tấm phủ toàn màn hình.
- Có bẫy focus, phím Esc để đóng, `role="dialog"`, `aria-modal`.
- Đầu hộp: tên công thức + công thức đích. Một dòng nhắc "Ta cần chứng minh …".
- **Hiện từng bước**: "Bước 2/7". Mỗi bước gồm:
  - lời giải thích (vì sao làm bước này),
  - biểu thức (phần vừa thay đổi tô màu `--nhan` qua `\color{}`),
  - hình minh hoạ SVG nhỏ (tuỳ chọn).
- Các nút: **‹ Bước trước · Bước tiếp ›**, "Hiện tất cả", "Ý chính". Phím ←/→ để chuyển bước.
- **Thanh tiến độ** các bước. Nhớ bước đang đọc theo từng công thức (localStorage, bọc try/catch).
- Bước cuối:
  - ô **"Kiểm tra bằng số"**: thay số mẫu vào hai vế, tính bằng **đúng hàm của `mo-hinh.js`**, hiện "Vế trái = … · Vế phải = … ✓",
  - nút **"Thử trong mô phỏng"**: mở mô phỏng với tham số `query` và tự làm nổi bật đại lượng liên quan.
- Nếu có `cach_2` thì hiện thêm tab "Cách 2".
- Có `can_toan` = `tich-phan` hoặc `dao-ham` thì hiện nhãn "Cần kiến thức đạo hàm/tích phân (Toán 12)". Nếu có thì kèm luôn cách không dùng giải tích (xem D5).

**Hình minh hoạ trong chứng minh** (`chung/hinh-chung-minh.js`): vẽ bằng SVG thuần, có hoạt ảnh ngắn, tôn trọng `prefers-reduced-motion`. Tối thiểu có các hình:
- `phan-tu-dap-thanh`: một phân tử đập vào thành, mũi tên Δp = 2mvₓ,
- `pit-tong-dx`: pít-tông dịch dx, công δA = p·dV,
- `trang-thai-trung-gian`: đường đi 1 → 1' → 2 trên đồ thị p–V,
- `chu-trinh-hess`: chu trình Hess đi qua đơn chất,
- `do-thi-nang-luong`: đồ thị Ea, ΔH và đường có xúc tác,
- `phan-ra-bac-1`: cứ mỗi t½ còn lại một nửa,
- `nen-thay-doi-Q`: nén lại thì Q nhân s^Δn.

### 5.3 Danh sách công thức BẮT BUỘC và khung chứng minh do Claude viết

Gemini viết lời giải thích tiếng Việt cho từng bước, nhưng **phải theo đúng khung dưới đây**. Không bỏ bước, không đổi lập luận. Dấu → là chuyển sang bước sau.

**A. Khí lý tưởng** (mô phỏng `khi-ly-tuong`)

- **K1 – pV = nRT.**
  1. Boyle (T không đổi): p₁V₁ = p₂V′.
  2. Charles (p không đổi): V′/T₁ = V₂/T₂.
  3. Khử V′ → p₁V₁/T₁ = p₂V₂/T₂, nên pV/T là hằng số với lượng khí cố định.
  4. Avogadro: ở cùng p, T thì V tỉ lệ với n → hằng số = n·R.
  5. Tính R từ đkc (25 °C, 1 bar, V_m = 24,79 L/mol) → R = 8,314 J/(mol·K).
  - Hình: `trang-thai-trung-gian`. `loai: chung-minh`, có kèm ghi chú: Boyle và Charles là định luật thực nghiệm.
- **K2 – p = ⅓·μ·m·⟨v²⟩** (μ = N/V, SGK Vật lí 12).
  1. Xét 1 phân tử trong hộp lập phương cạnh ℓ.
  2. Va chạm đàn hồi với thành: Δpₓ = 2mvₓ.
  3. Thời gian giữa hai lần đập vào cùng một thành: Δt = 2ℓ/vₓ.
  4. Lực trung bình F₁ = mvₓ²/ℓ.
  5. N phân tử: F = (Nm/ℓ)·⟨vₓ²⟩.
  6. Chuyển động hỗn loạn đều mọi hướng: ⟨vₓ²⟩ = ⟨v²⟩/3.
  7. p = F/ℓ² = Nm⟨v²⟩/(3V).
  - Hình: `phan-tu-dap-thanh`. "Thử trong mô phỏng" bật chế độ **Theo dõi 1 phân tử**.
- **K3 – W̄đ = (3/2)kT.**
  1. K2 viết lại: pV = ⅓Nm⟨v²⟩.
  2. K1 viết theo số phân tử: pV = NkT, với k = R/N_A.
  3. So sánh hai vế → ½m⟨v²⟩ = (3/2)kT.
- **K4 – v_rms = √(3RT/M).**
  1. Từ K3: ⟨v²⟩ = 3kT/m.
  2. Nhân cả tử và mẫu với N_A: kN_A = R, mN_A = M.
  3. Lấy căn.
  - Thêm (mở rộng): v_p = √(2RT/M), v̄ = √(8RT/(πM)), kèm ghi chú "từ phân bố Maxwell, không chứng minh ở mức THPT".
- **K5 – Dalton p = Σpᵢ, pᵢ = xᵢp.**
  1. Áp dụng K2 cho từng loại phân tử, mỗi loại đập vào thành độc lập.
  2. Áp suất là tổng xung lực → cộng lại.
  3. pᵢ = (nᵢ/V)RT.
  4. Chia cho p → xᵢ.
- **K6 – Các đẳng quá trình** (Boyle, Charles, Gay-Lussac): mỗi định luật là pV = nRT với hai đại lượng giữ không đổi. Thêm **độ không tuyệt đối**: trên đồ thị V–t(°C), kéo dài đường thẳng tới V = 0 → t = −273,15 °C.
- **K7 – Công đẳng nhiệt A′ = nRT·ln(V₂/V₁)** (`mo-rong`, `can_toan: tich-phan`).
  1. δA′ = p·dV (lấy từ N2).
  2. p = nRT/V.
  3. Tích phân với T không đổi.
- **K8 – Van der Waals** (`dai-hoc`, `loai: suy-luan-mo-hinh`).
  1. Phân tử có thể tích riêng → thể tích dùng được là V − nb.
  2. Lực hút giữa các phân tử làm giảm số cú đập và lực đập, mức giảm tỉ lệ với (n/V)² → cộng a·n²/V² vào p.

**B. Nhiệt động học** (gắn với `dong-co-nhiet`; N2, N3, N4, N5 gắn thêm với `khi-ly-tuong`)

- **N1 – ΔU = A + Q** (SGK Vật lí 12; `dinh-luat-thuc-nghiem`): đây là định luật bảo toàn năng lượng. Nêu quy ước dấu. Kiểm chứng bằng thẻ "Định luật I" đang chạy trong Động cơ nhiệt.
- **N2 – Công của khí A′ = ∫p dV = diện tích dưới đường p–V.**
  1. Pít-tông diện tích S dịch đi dx → δA′ = F·dx = pS·dx = p·dV.
  2. Cộng (tích phân) các đoạn nhỏ.
  3. Mỗi dải p·dV là một hình chữ nhật mảnh trên đồ thị → tổng là diện tích.
  - Hình: `pit-tong-dx`. Có cách không dùng tích phân: "cộng các hình chữ nhật".
- **N3 – U = (f/2)nRT, Cv = (f/2)R.**
  1. Khí đơn nguyên tử: U = N·(3/2)kT, lấy từ K3.
  2. → Cv = (3/2)R.
  3. Mở rộng: mỗi bậc tự do góp ½kT; khí lưỡng nguyên tử có f = 5.
- **N4 – Mayer: Cp − Cv = R.**
  1. Đẳng áp: Q = ΔU + A′.
  2. ΔU = nCvΔT; A′ = pΔV = nRΔT.
  3. Chia cho nΔT.
- **N5 – Poisson: TV^(γ−1) = hằng, pV^γ = hằng** (`mo-rong`, `can_toan: tich-phan`).
  1. Đoạn nhiệt: δQ = 0 → dU = −p dV.
  2. nCv dT = −(nRT/V)dV.
  3. dT/T = −(R/Cv)·dV/V = −(γ−1)·dV/V.
  4. Tích phân → ln T + (γ−1)·ln V = hằng.
  5. Thay T = pV/(nR) → pV^γ = hằng.
- **N6 – Hiệu suất động cơ nhiệt η = A′/Q₁ = 1 − Q₂/Q₁.**
  1. Sau một chu trình khí trở về trạng thái đầu → ΔU = 0.
  2. → A′ = Q₁ − Q₂.
- **N7 – η_Otto = 1 − 1/r^(γ−1).**
  1. Q₁ = nCv(T₃ − T₂), Q₂ = nCv(T₄ − T₁).
  2. Hai đoạn đoạn nhiệt (dùng N5): T₂ = T₁·r^(γ−1), T₃ = T₄·r^(γ−1).
  3. → T₃ − T₂ = r^(γ−1)·(T₄ − T₁).
  4. η = 1 − (T₄ − T₁)/(T₃ − T₂) = 1 − r^(1−γ).
  - **Ví dụ số:** r = 8, γ = 1,4 → η = 0,5647.
- **N8 – Carnot η_max = 1 − T_lạnh/T_nóng** (`mo-rong`).
  1. Nêu kết quả.
  2. Chứng minh Otto **kém** Carnot: η_Otto = 1 − T₁/T₂ (từ N7) < 1 − T₁/T₃, vì T₂ < T₃.

**C. Nhiệt hoá học** (gắn với tab Nhiên liệu của `dong-co-nhiet`)

- **H1 – ΔrH° = ΣΔfH°(sản phẩm) − ΣΔfH°(chất đầu)** (SGK Hoá 10).
  1. Phân huỷ chất đầu thành đơn chất: cần −ΣΔfH°(chất đầu).
  2. Tạo sản phẩm từ chính các đơn chất đó: ΣΔfH°(sản phẩm).
  3. Enthalpy là hàm trạng thái → hai đường đi cho cùng kết quả (định luật Hess).
  - Hình: `chu-trinh-hess`. **Ví dụ số:** isooctane ΔcH° = −5065,0 kJ/mol.
- **H2 – ΔrH° = ΣE_b(chất đầu) − ΣE_b(sản phẩm)** (SGK Hoá 10, phản ứng ở thể khí).
  1. Phá hết liên kết của chất đầu → tốn năng lượng.
  2. Tạo liên kết của sản phẩm → toả năng lượng.
  3. Cộng hai phần lại.
- **H3 – LHV = −ΔcH°/M, AFR = n(O₂)·(32 + 3,76·28)/M.** Viết phương trình cháy với không khí = O₂ + 3,76 N₂ rồi tính khối lượng. **Ví dụ số:** isooctane LHV = 44,43 MJ/kg, AFR = 15,05.

**D. Tốc độ phản ứng** (mô phỏng `toc-do-phan-ung`)

- **D1 – Tốc độ trung bình** v̄ = −(1/a)·ΔC_A/Δt = (1/c)·ΔC_C/Δt. Chứng minh kết quả như nhau với mọi chất: theo hệ số tỉ lượng, ΔC_A/(−a) = ΔC_C/c.
- **D2 – v = k·C_A^a·C_B^b** (SGK Hoá 10; `suy-luan-mo-hinh` cho phản ứng sơ cấp).
  1. Phản ứng xảy ra khi A gặp B.
  2. Số va chạm A–B trong một đơn vị thể tích mỗi giây tỉ lệ với C_A·C_B (gấp đôi A thì gấp đôi số cặp).
  3. Chỉ một phần va chạm là hiệu quả, phần này dồn vào k.
  4. Ghi chú: với phản ứng phức tạp thì bậc phải xác định bằng thực nghiệm.
  - "Thử trong mô phỏng": tăng gấp đôi số hạt A rồi xem bộ đếm va chạm.
- **D3 – Van't Hoff: v₂/v₁ = γ^((T₂−T₁)/10)** (SGK Hoá 10; `mo-rong` khi suy ra từ Arrhenius).
  1. Từ D4: ln(k₂/k₁) = (Ea/R)·(T₂ − T₁)/(T₁T₂).
  2. Khi ΔT nhỏ: T₁T₂ ≈ T².
  3. → γ = e^(10·Ea/(RT²)).
  4. Với Ea ≈ 51 kJ/mol ở 25 °C thì γ ≈ 2. Đây là lý do có "quy tắc γ = 2–4".
- **D4 – Arrhenius k = A·e^(−Ea/(RT)), ln(k₂/k₁) = −(Ea/R)·(1/T₂ − 1/T₁).**
  1. e^(−Ea/RT) là tỉ lệ va chạm có năng lượng (theo phương nối tâm) ≥ Ea. Trong mô hình 2D, tỉ lệ này **đúng chính xác** (`suy-luan-mo-hinh`, `mo-rong`).
  2. Lấy ln hai vế.
  3. Viết ở hai nhiệt độ rồi trừ cho nhau.
  - "Kiểm tra bằng số" dùng bộ đếm va chạm của mô phỏng hạt.
- **D5 – Bậc 1: ln(C₀/C) = kt, t½ = ln2/k.**
  - Cách 1 (`tich-phan`): −dC/dt = kC → dC/C = −k·dt → tích phân.
  - **Cách 2 (không giải tích):**
    1. Trong mỗi khoảng thời gian nhỏ bằng nhau, cùng một **tỉ lệ** chất bị mất đi.
    2. → C giảm theo cấp số nhân.
    3. Thời gian để còn một nửa luôn bằng nhau.
  - Hình: `phan-ra-bac-1`.
- **D6 – Bậc 2** (A + A): 1/C − 1/C₀ = kt, t½ = 1/(kC₀). **Bậc 0:** C = C₀ − kt, t½ = C₀/(2k). Cả hai đều `tich-phan`, có kèm hình đường thẳng hoá.
- **D7 – Xúc tác: k_xt/k = e^(ΔEa/(RT))** (cùng A). Lấy tỉ số hai biểu thức Arrhenius. **Ví dụ số:** giảm Ea 20 kJ/mol ở 298,15 K → tăng 3190 lần.

**E. Cân bằng hoá học** (mô phỏng `can-bang-le-chatelier`)

- **C1 – Kc = [C]^c·[D]^d / ([A]^a·[B]^b)** (SGK Hoá 11).
  1. v_t = k_t·[A]^a[B]^b; v_n = k_n·[C]^c[D]^d (phản ứng sơ cấp).
  2. Ở trạng thái cân bằng: v_t = v_n.
  3. → Kc = k_t/k_n, là hằng số khi T không đổi.
  4. Ghi chú: biểu thức Kc đúng cho mọi phản ứng (nhiệt động học), cách chứng minh qua tốc độ chỉ là trường hợp minh hoạ.
- **C2 – Q so với K quyết định chiều.**
  1. Với cùng mô hình: v_t/v_n = Kc/Q.
  2. Q < K → v_t > v_n → chuyển dịch theo chiều thuận.
  3. Q > K → chiều nghịch.
  4. Q = K → đang cân bằng.
- **C3 – Ảnh hưởng của áp suất** (nén thể tích lại s lần).
  1. Mọi nồng độ khí nhân s.
  2. → Q′ = s^Δn·K, với Δn = (c+d) − (a+b) (chỉ tính chất khí).
  3. Δn < 0 → Q′ < K → chiều thuận, tức chiều giảm số mol khí.
  4. Δn = 0 → không chuyển dịch.
  - Hình: `nen-thay-doi-Q`. **Ví dụ số:** 2NO₂ ⇌ N₂O₄ (mục 11.3).
- **C4 – Ảnh hưởng của nhiệt độ: ln(K₂/K₁) = −(ΔH°/R)·(1/T₂ − 1/T₁).**
  - **Cách 1 (chỉ dùng kiến thức tốc độ):**
    1. K = k_t/k_n = (A_t/A_n)·e^(−(Ea_t − Ea_n)/(RT)).
    2. Ea_t − Ea_n = ΔH (đọc từ đồ thị năng lượng).
    3. → ln K = hằng − ΔH/(RT).
    4. Viết ở hai nhiệt độ rồi trừ.
    5. Kết luận: thu nhiệt (ΔH > 0) thì T tăng làm K tăng.
  - **Cách 2 (đại học):** ΔG° = −RT·ln K = ΔH° − TΔS°.
- **C5 – Kp = Kc·(RT)^Δn.**
  1. pᵢ = Cᵢ·RT.
  2. Thay vào biểu thức Kp rồi gom các thừa số (RT).
- **C6 – Bảng ICE cho A ⇌ 2B:** Kc = 4x²/(C₀ − x) → x = (−K + √(K² + 16·K·C₀))/8.
- **C7 – Xúc tác không làm chuyển dịch cân bằng.**
  1. Xúc tác làm giảm Ea_t và Ea_n **cùng một lượng** ΔEa.
  2. k_t và k_n cùng nhân e^(ΔEa/RT).
  3. K = k_t/k_n không đổi. Chỉ làm **nhanh tới** cân bằng hơn.

**Tổng cộng ít nhất 30 công thức.** Mỗi công thức có `vi_du_so`, trừ những công thức thuần định tính (N1, K8 là `suy-luan-mo-hinh`). Test 11.5 sẽ kiểm.

### 5.4 MathJax chạy offline

- Cấu hình giống `biology-lab-3d.html` (`startup.typeset: false`), thêm `chtml.fontURL: "/static/vendor/mathjax-fonts/woff-v2"`.
- **Tải các font** `output/chtml/fonts/woff-v2/*.woff` của MathJax 3.2.2 qua `tools/tai_vendor.py`, khai báo trong `frontend/vendor/PHIEN_BAN.json`, lưu vào `frontend/vendor/mathjax-fonts/woff-v2/` (khoảng 300 KB). Dùng cho cả web lẫn app.
- Chỉ gọi `MathJax.typesetPromise([el])` cho phần tử vừa chèn vào. Không typeset lại toàn trang.
- Khi MathJax chưa tải xong, hiện tạm biểu thức dạng chữ thường (dạng dự phòng `latex_text`, ví dụ "pV = nRT").

---

## 6. Giai đoạn 0: phóng to hình Động cơ nhiệt và sửa lỗi (làm TRƯỚC)

1. Chuyển `dong-co-nhiet` sang khung chung ở mục 4.2: viewBox động, khung không cố định tỉ lệ, `max-w-[1600px]`, các nút thu gọn bảng và toàn màn hình. Giữ nguyên toàn bộ tính năng và các test cũ.
2. **Bỏ ràng buộc "trục V thẳng hàng tuyệt đối với pít-tông" làm mặc định.** Thay bằng **"phễu phóng to"**:
   - Đồ thị p–V trải hết chiều ngang khung (trừ lề). V từ 0 đến V₁·1,08 trải trên toàn bộ trục.
   - Ngay trên xi-lanh có một **thước V nhỏ** (từ nắp tới ĐCD).
   - Hai đường mờ nét đứt nối đầu–cuối thước V với đầu–cuối trục V của đồ thị, tạo thành hình phễu.
   - Đường chấm của trạng thái hiện tại đi: chấm đỏ → trục V của đồ thị → đường gấp khúc trong phễu → mặt pít-tông. Mối liên hệ V ↔ pít-tông vẫn nhìn thấy được.
   - Có nút chuyển **"Thẳng hàng như video"** (bật lại cách cũ). Trạng thái nút lưu vào link chia sẻ (`&canh=thang`).
3. Kích thước cơ cấu tính theo khung thật:
   - a_px (bán kính tay quay) lớn nhất có thể, với điều kiện `X_nắp + Vc_px + cán + L_px + a_px + R_bánh_đà + lề ≤ W`.
   - R_bánh_đà = 1,2·a_px.
   - Thứ tự vẽ: **bánh đà → tay quay → thanh truyền → pít-tông** (thanh truyền nằm trên cùng, không bị che).
4. Vạch chia đẹp dùng `HLDinhDang.vachDep` (ví dụ p: 0, 20, 40…; V: 0, 100, 200…). Nhãn Vc và V₁ đặt **trong thước V của xi-lanh**, không nhét vào trục đồ thị. Nhãn điểm 0–4 tự né nhau và né vạch trục.
5. Nhãn quá trình ("Kỳ 2 · Nén / Nén đoạn nhiệt") chuyển lên **dải đầu đồ thị**, không đặt bên trong vùng vẽ.
6. Hạt khí: chuyển sang `HLVeHat`, cắt theo `vungKhi`. Test 11.6 phải chứng minh **100 % hạt nằm trong vùng chứa khí**.
7. Bỏ toàn bộ mã hex viết cứng trong `ve.js`. Bỏ việc gán `innerHTML` trong `capNhat`: tạo phần tử một lần, mỗi khung hình chỉ `setAttribute`.
8. Thêm tab **"Công thức"** (sau khi Giai đoạn 1 xong) với N1–N8 và H1–H3.

**Tiêu chí nghiệm thu GĐ0** (đo bằng `HoaLy.boCuc()` trong Playwright)

| Màn hình | Đường chu trình p–V | Yêu cầu khác |
|---|---|---|
| 1280×800 | ≥ **560×300** px (hiện 149×232) | chữ trục ≥ 12 px |
| 1440×900 | ≥ 640×340 | – |
| 390×844 | ≥ 320×200 (hiện 95×148) | không có cuộn ngang |

- Thu gọn bảng ở 1280×800 → đường chu trình rộng ≥ 900 px.
- Ở mọi cỡ màn hình: không có khoảng trống > 24 px ở bất kỳ mép nào của khung hình.
- Ở mọi cỡ màn hình: 100 % hạt nằm trong `vungKhi`.
- Toàn bộ test cũ của Động cơ nhiệt vẫn PASS.

---

## 7. Mô phỏng 1: KHÍ LÝ TƯỞNG (`/hoa-hoc/hoa-ly/khi-ly-tuong`)

### 7.1 Mô hình (`khi-ly-tuong/mo-hinh.js` → `window.HL_KhiLyTuong`), nguồn sự thật

**Trạng thái** `{n (mol), V (L), T (K), khi, giu: "T" | "p" | "V" | "doan-nhiet" | "tu-do"}`. Tính ra p = nRT/V với R = 8,314 J/(mol·K). Đổi đơn vị bar / kPa / atm.

**Danh sách khí** (`khi.json`, có trường `nguon`):

| Khí | M (g/mol) | γ |
|---|---|---|
| He | 4,003 | 5/3 |
| Ne | 20,18 | 5/3 |
| Ar | 39,95 | 5/3 |
| H₂ | 2,016 | 7/5 |
| N₂ | 28,01 | 7/5 |
| O₂ | 32,00 | 7/5 |
| CO₂ | 44,01 | ≈1,29 |
| Không khí | 28,96 | 1,40 |

Mỗi khí kèm a, b Van der Waals. Ví dụ CO₂: a = 3,640 L²·bar/mol², b = 0,04267 L/mol.

**Các hàm**
- `apSuat`, `theTich`, `vRms`, `vP`, `vTb`, `dongNangTb`, `noiNang`
- `quaTrinh(tuTrangThai, denGiaTri, giu)`: đi theo đường đẳng nhiệt / đẳng áp / đẳng tích / đoạn nhiệt. Trả về {Q, A, ΔU} theo quy ước SGK, ΔU = A + Q.
- `vanDerWaals`
- `phanBoMaxwell(v)` (3D)
- `hopKhi([...])`: hỗn hợp khí, tính áp suất riêng phần.

**Giới hạn:** V 0,5–50 L; T 50–1500 K; n 0,01–5 mol. Khi p > 100 bar thì cảnh báo và gợi ý bật Van der Waals.

**Trạng thái mặc định (và khi bấm Đặt lại):** N₂, n = 1 mol, T = 298,15 K, V = 24,79 L (p = 1 bar), giữ T.

### 7.2 Mô hình hạt (chỉ để minh hoạ, không làm thay đổi số liệu)

- Số hạt hiển thị N_ht = clamp(round(n·120), 12, 400). Ghi chú "1 hạt ≈ … phân tử".
- Độ lớn vận tốc lấy từ `maxwell3D` theo T, hướng ngẫu nhiên trong mặt phẳng. **Mặc định không cho hạt va chạm nhau** nên phân bố tốc độ giữ đúng Maxwell 3D. Khi T đổi, mọi vận tốc nhân √(T_mới/T_cũ).
- Va chạm với thành là phản xạ gương. Pít-tông di chuyển được.
- **"Áp suất đo từ va chạm":** tổng xung lực lên thành chia cho (Δt × chu vi), nhân một hệ số hiệu chỉnh chọn sao cho kỳ vọng = nRT/V.
  - Đồng hồ có **2 kim**: kim lý thuyết và kim đo (dao động quanh kim lý thuyết).
  - Test: trung bình trong 4 s mô phỏng lệch ≤ 5 %.
- **"Theo dõi 1 phân tử":** tô sáng một hạt kèm vệt đuôi, đếm số lần nó đập thành phải, hiện Δp = 2mvₓ mỗi lần đập. Dùng cho phần chứng minh K2.

### 7.3 Hình và thao tác kéo (giống bản gốc)

**Nửa trên: đồ thị**, có các tab:
- p–V (vẽ sẵn họ đường đẳng nhiệt mờ),
- V–T,
- p–T,
- Phân bố tốc độ (histogram tốc độ hạt so với đường Maxwell, đánh dấu v_p, v̄, v_rms).

Trục V của p–V nối với pít-tông bằng **phễu phóng to** (giống GĐ0).

**Nửa dưới: bình khí nằm ngang** gồm:
- **pít-tông kéo ngang ở bên phải**,
- bơm tay ở góc trên trái (tay bơm kéo lên xuống, mỗi lần đẩy +0,05 mol),
- van xả ở góc dưới trái (kéo để mở, khí thoát ra theo tốc độ),
- bếp ở đáy bình (núm kéo: lên = nung có ngọn lửa, xuống = làm lạnh có nước đá),
- nhiệt kế (kéo cột thuỷ ngân để đặt T khi giữ V hoặc giữ T bằng bể ổn nhiệt),
- đồng hồ áp suất.

**Bộ chọn "Giữ cố định":**
- **Nhiệt độ (bể ổn nhiệt):** kéo pít-tông → đẳng nhiệt; mũi tên nhiệt đi qua thành bình; hiện Q.
- **Áp suất:** pít-tông tự trượt khi nung hoặc làm lạnh; kéo trực tiếp thì bị khoá, có biểu tượng ổ khoá.
- **Thể tích:** pít-tông bị khoá.
- **Cách nhiệt (đoạn nhiệt):** kéo pít-tông thì T đổi theo N5; vỏ bình đổi màu.
- **Tự do:** đổi gì cũng được, đường đi tự do.

**Chấm trạng thái trên đồ thị kéo được**, chạy dọc **đường của quá trình đang giữ** đi qua trạng thái hiện tại.

### 7.4 Tính năng

**P1**
- Mọi thao tác kéo ở 7.3.
- Bảng trạng thái: n, N, p, V, T (K và °C), U, v_rms, W̄đ.
- Đồng hồ 2 kim.
- Tab Thông số / Số liệu / Công thức / Học & Thi.
- Ghim bóng ma, xuất ảnh, link chia sẻ.

**P2**
- Thí nghiệm **"Tìm độ không tuyệt đối"**: giữ p, nút "Ghi điểm" ở nhiều T, khớp đường thẳng V–t(°C), kéo dài tới V = 0 → hiện −273,15 °C ± sai số.
- Hỗn hợp 2 khí, mỗi loại một màu; cột áp suất riêng phần xếp chồng (Dalton).
- Theo dõi 1 phân tử.

**P3**
- Bật Van der Waals với CO₂: vẽ hai đường đẳng nhiệt (khí thật và khí lý tưởng) cạnh nhau, hiện hệ số nén Z.

### 7.5 `HoaLy.trangThai()`

Trả về `{n, V_L, T_K, p_bar, giu, khi, N_hat, p_do_bar, U_J}`. Các tay nắm có toạ độ: `pitTong`, `tayBom`, `nutBep`, `nhietKe`, `chamDoThi`.

---

## 8. Mô phỏng 2: CÂN BẰNG HOÁ HỌC & LE CHATELIER (`/hoa-hoc/hoa-ly/can-bang-le-chatelier`)

### 8.1 Hệ phản ứng (`he-can-bang.json`)

Mỗi hệ có các trường:
- `phuong_trinh`, `chat` [{ct, he_so, pha, mau (token hoặc màu trộn), do_dam}],
- `dH_kJ`, `K_ref`, `T_ref`, `loai_K: "Kc"`, `Ea_t_kJ`, `A_t`, `nguon`.

Ưu tiên các thí nghiệm có trong SGK Hoá 11 (KNTT / CTST / Cánh Diều), đối chiếu và ghi rõ nguồn.

1. **2NO₂(g) ⇌ N₂O₄(g)** (mặc định). ΔrH° ≈ −57,2 kJ (SGK có thể ghi −58); NO₂ màu nâu đỏ, N₂O₄ không màu; bình là **ống tiêm nằm ngang có pít-tông** giống bản gốc.
   - K_ref: dùng số trong SGK nếu có. Nếu không, dùng Kc(N₂O₄ ⇌ 2NO₂, 25 °C) ≈ 4,61·10⁻³ mol/L, tức Kc(chiều dime hoá) ≈ 217 L/mol, và ghi nguồn.
2. **H₂(g) + I₂(g) ⇌ 2HI(g)**: Δn = 0, nên **áp suất không làm chuyển dịch** (điểm dạy học quan trọng). I₂ màu tím. Kc ở khoảng 430–450 °C cỡ 50–64; lấy số SGK.
3. **N₂(g) + 3H₂(g) ⇌ 2NH₃(g)** (Haber): ΔH ≈ −91,8 kJ; có nút **xúc tác Fe**; có nút chọn sẵn "Điều kiện công nghiệp" (450 °C, 200 bar).
4. (P2) **Fe³⁺(aq) + SCN⁻(aq) ⇌ [Fe(SCN)]²⁺(aq)**, màu đỏ máu, chỉ có ảnh hưởng nồng độ, bình là ống nghiệm. Nếu SGK dùng thí nghiệm khác (ví dụ CrO₄²⁻/Cr₂O₇²⁻) thì thêm thí nghiệm đó.

### 8.2 Mô hình (`can-bang-le-chatelier/mo-hinh.js` → `HL_CanBang`)

- **Tốc độ:** r_t = k_t·Π C^ν, r_n = k_n·Π C^ν (coi là phản ứng sơ cấp).
  - k_t = A_t·e^(−Ea_t/RT).
  - k_n = k_t/Kc(T), với Kc(T) tính theo van 't Hoff từ K_ref (dùng C4).
- **Tích phân** bằng RK4 có bước thích nghi. Nếu hệ bị cứng thì lùi về Euler ẩn. Thời gian hiển thị có hệ số co giãn, ghi rõ trên giao diện.
- **Các hàm**
  - `Kc(T)`, `Kp(T)`, `Q(C)`, `chieu(Q, K)`
  - `canBangTuBanDau(C0, T)`: giải bảng ICE bằng phương pháp số (Newton có chặn hoặc chia đôi). Kết quả phải khớp nghiệm giải tích của C6 trong test.
  - `nhieu(loai, giaTri)`, với loai ∈ {`the_tich`, `nhiet_do`, `them_chat`, `bot_chat`, `xuc_tac`, `khi_tro_V`, `khi_tro_p`}. Trả về trạng thái tức thời + `giaiThich` (mảng câu tiếng Việt sinh **từ số liệu**, không viết sẵn).
- **Khí trơ** (P2):
  - Giữ V: không chuyển dịch.
  - Giữ p: V tăng → giống giảm áp suất.

### 8.3 Hình và thao tác kéo

**Nửa dưới: bình phản ứng**
- **Ống tiêm có pít-tông kéo ngang.** Với chất khí, đổi V thì nồng độ đổi tức thì theo 1/V. Màu nâu **đậm lên ngay**, rồi nhạt dần khi hệ trở về cân bằng (đúng như mô tả của Purdue/RSC).
- Hạt minh hoạ: NO₂ là chấm nâu, N₂O₄ là cặp chấm. Số hạt tỉ lệ với số mol.
  - Mỗi khung hình, số "phản ứng" giữa các hạt = số lần mô hình yêu cầu (lấy mẫu Poisson theo r_t, r_n). Chọn những cặp hạt đang ở gần nhau và loé sáng nhẹ khi chúng phản ứng.
  - Nền khí trộn màu theo nồng độ (`tronMau`).
- **Chậu nước dưới ống tiêm:** kéo núm từ "nước đá" tới "nước nóng" để đặt T. Có nhiệt kế.
- **Bình chứa chất có van:** kéo ống nhỏ giọt hoặc bình khí vào miệng ống tiêm để **thêm chất**. Van rút để **bớt sản phẩm**.
- **Viên xúc tác:** kéo vào bình để bật, kéo ra để tắt.
- **Cột nồng độ kéo được** (bên cạnh bình): kéo trực tiếp chiều cao cột của một chất để đổi nồng độ tức thì (thêm hoặc bớt), thả ra thì các cột tự trôi về cân bằng.

**Nửa trên: đồ thị**, có các tab:
- **C–t**: mỗi chất một đường, đánh dấu sự kiện ① ② … tại các lần tác động.
- **v–t**: v_t và v_n tiến về nhau. Đây là hình kinh điển trong SGK.
- **Q/K**: kim trên thang log, mũi tên "→ thuận / ← nghịch / ⇌ cân bằng".
- **ln K – 1/T**: đồ thị van 't Hoff, hệ số góc = −ΔH/R.

**Thẻ "Le Chatelier vừa xảy ra"** (bên phải): sau mỗi lần tác động, hiện các câu trong `giaiThich`. Ví dụ:

> Bạn giảm V còn ½ → mọi nồng độ khí × 2 → Q = 2^Δn·K = 0,5·K < K → chuyển dịch theo chiều thuận (tạo N₂O₄, giảm số mol khí) → màu nâu đậm lên ngay rồi nhạt dần.

Kèm nút "Xem chứng minh C3".

### 8.4 Tính năng

**P1**
- Hệ 1, 2, 3.
- Mọi thao tác kéo ở 8.3.
- Bảng ICE tự động; hiện Kc, Kp, Q, độ chuyển hoá α.
- Tab Thông số / Số liệu / Công thức / Học & Thi.

**P2**
- Chế độ **"Dự đoán trước"**: trước mỗi tác động, học sinh chọn thuận / nghịch / không đổi. Mô phỏng chạy rồi chấm điểm.
- Hệ 4.
- Khí trơ.
- Haber: đồ thị hiệu suất NH₃ theo T và p.

**P3**
- Tab **G–ξ** (`dai-hoc`): đường cong G theo độ tiến phản ứng ξ, có **quả bóng kéo được**; thả ra thì bóng lăn về đáy, đáy chính là trạng thái cân bằng.

### 8.5 `HoaLy.trangThai()`

Trả về `{he, T_K, V_L, C: {ct: mol/L}, Q, Kc, chieu, alpha, xuc_tac, t_s}`. Các tay nắm có toạ độ: `pitTong`, `nutNhietDo`, `ongNhoGiot`, `vienXucTac`, `cot_<ct>`.

---

## 9. Mô phỏng 3: TỐC ĐỘ PHẢN ỨNG & NĂNG LƯỢNG HOẠT HOÁ (`/hoa-hoc/hoa-ly/toc-do-phan-ung`)

### 9.1 Mô hình (`toc-do-phan-ung/mo-hinh.js` → `HL_TocDo`)

- **Định luật tốc độ:** v = k·C_A^a·C_B^b. Bậc a, b chọn được: 0, 1, 2.
- **Arrhenius:** k = A·e^(−Ea/RT).
- **Xúc tác:** Ea_xt = Ea − ΔEa.
- **Các hàm:** C(t) cho bậc 0/1/2 (nghiệm giải tích; tổng quát thì dùng RK4), `tBanPhan`, `heSoVantHoff(Ea, T)`, `khopArrhenius([{T, k}])` (hồi quy ln k theo 1/T → Ea, A, R²).
- **Phản ứng mẫu** (`phan-ung.json`, có trường `nguon`):
  1. A + B → C + D (tổng quát, mặc định).
  2. H₂O₂ → H₂O + ½O₂, có xúc tác MnO₂ hoặc KI (thí nghiệm SGK).
  3. Zn + HCl hoặc CaCO₃ + HCl: phản ứng dị thể, **diện tích bề mặt** (P2).
  4. 2HI → H₂ + I₂ (Ea ≈ 184 kJ/mol; dùng để so sánh Ea cao và thấp).
- Các hằng số động học lấy theo SGK hoặc tài liệu, có trường `nguon`. Nếu không có thì đánh dấu `"minh_hoa": true` và giao diện ghi "số liệu minh hoạ".

### 9.2 Mô hình hạt (có ý nghĩa vật lý thật)

- Đĩa 2D va chạm đàn hồi. Vận tốc Maxwell 2D theo kT.
- Khi hai hạt A–B va chạm: E_n = ½μ·v_n² (μ là khối lượng rút gọn, v_n là thành phần vận tốc tương đối theo phương nối tâm).
  - Nếu E_n ≥ E₀ (thêm hệ số định hướng P, chỉnh được) → phản ứng: A, B đổi thành C, D. Phần năng lượng ΔH cộng vào hoặc trừ đi động năng của sản phẩm (toả nhiệt thì hạt chạy nhanh hơn, có quầng sáng).
  - Nếu không đủ → nảy ra như bình thường.
- **Đơn vị rút gọn:** kT_sim = T/T_ref; E₀_sim = Ea/(R·T_ref). Như vậy tỉ lệ va chạm hiệu quả **lý thuyết = e^(−Ea/RT)**. Với tiêu chí theo phương nối tâm, kết quả này **đúng chính xác** cho phân bố Maxwell 2D (dòng va chạm có trọng số v_n).
  - Test 11.4 kiểm tỉ lệ đo được: x = 2 → e⁻² = 0,1353 ± 10 % (≥ 5000 va chạm, có seed).
- **Bộ đếm:** va chạm/s, va chạm hiệu quả/s, tỉ lệ đo được so với e^(−Ea/RT).
- **Số liệu chính** (C–t, k) lấy từ mô hình ODE ở 9.1. Đường C–t của hạt vẽ chồng lên làm đối chứng, có nhãn "mô hình hạt (ngẫu nhiên)".

### 9.3 Hình và thao tác kéo

**Nửa trên: đồ thị**, có các tab:
- **Giản đồ năng lượng**
  - Mức chất đầu, đỉnh trạng thái chuyển tiếp, mức sản phẩm.
  - **Kéo tay nắm đỉnh** để đổi Ea; **kéo mức sản phẩm** để đổi ΔH. Có đường xúc tác nét đứt.
  - **Quả bóng phân tử kéo được:** kéo lên sườn đồi. Thả khi chưa qua đỉnh thì bóng lăn về, dao động tắt dần, hiện chữ "va chạm không hiệu quả". Qua đỉnh thì bóng lăn xuống phía sản phẩm, toả ΔH, hiện "hiệu quả".
- **Maxwell–Boltzmann (năng lượng)**
  - Tô phần đuôi có E ≥ Ea; **kéo vạch Ea**.
  - Kéo nhiệt kế thì đường cong dẹt ra và phần tô lớn lên.
  - Hiện tỉ lệ chính xác `erfc(√x) + 2√(x/π)·e^(−x)` (3D) và thừa số Arrhenius e^(−x).
- **C–t**: đường C–t; kéo con trỏ thời gian để đọc C và tốc độ tức thời (tiếp tuyến).
- **ln k – 1/T**: các điểm từ tab Thí nghiệm, đường khớp, Ea suy ra.

**Nửa dưới: buồng phản ứng**
- Thành phải kéo được để đổi V, tức đổi nồng độ.
- Ống nhỏ giọt: thêm A hoặc B.
- Nhiệt kế kéo được: T.
- Tấm xúc tác: kéo vào hoặc kéo ra.
- (P2) Khối rắn: kéo "búa" để đập nhỏ, diện tích bề mặt tăng.

**Tab "Thí nghiệm"**
- Nút "Chạy thí nghiệm ở T này" (chạy nhanh ngầm, đo k) → thêm một dòng vào bảng T – k – ln k – 1/T.
- Sau ≥ 3 dòng: khớp Arrhenius → Ea đo được so với Ea đặt.
- Hiện hệ số Van't Hoff γ.

### 9.4 Tính năng

**P1**
- Phản ứng mẫu 1 và 2.
- Mọi thao tác kéo ở 9.3; bộ đếm va chạm; bảng thí nghiệm.
- Tab Thông số / Số liệu / Thí nghiệm / Công thức / Học & Thi.

**P2**
- **Tìm bậc phản ứng**: vẽ C, ln C, 1/C theo t; đường nào thẳng thì đó là bậc.
- Phản ứng mẫu 3 (diện tích bề mặt).
- Hệ số định hướng P.

**P3**
- Phản ứng thuận nghịch (bật được), nối sang mô phỏng Cân bằng: giải thích **K = k_t/k_n** ngay trên giản đồ năng lượng (Ea_t − Ea_n = ΔH).

### 9.5 `HoaLy.trangThai()`

Trả về `{pu, T_K, Ea_kJ, dH_kJ, xuc_tac, C: {...}, k, v, vaCham, vaChamHieuQua, tiLeDo, tiLeLyThuyet}`. Các tay nắm có toạ độ: `dinhEa`, `mucSanPham`, `qua_bong`, `vachEa`, `nhietKe`, `thanhPhai`, `tamXucTac`.

---

## 10. Phần chung cho cả 3 mô phỏng (giống bản gốc)

- Nút chạy/dừng; tốc độ 0,25×–4×; đặt lại; ghim bóng ma; phím tắt (Space, R, G, ←/→, 1–4 để chuyển tab đồ thị).
- Kéo bằng chuột và cảm ứng; sáng/tối; bản di động có ngăn kéo đáy; toàn màn hình; thu gọn bảng.
- Thẻ **"Trạng thái hiện tại"** luôn nằm trên cùng cột phải, hiện các số chính kèm đơn vị, dấu phẩy thập phân.
- **Học & Thi:** 9 câu (3 dạng × 3). Câu có đáp án là số thì có `tinh_bang` để test tự tính lại (giống `dong-co-nhiet`).
- **Hiệu năng:**
  - Chỉ chạy rAF khi đang có chuyển động; dừng khi tab bị ẩn.
  - ≤ 400 hạt; va chạm hạt–hạt dùng lưới ô.
  - 60 khung/s trên M2.
  - Tắt hạt khi bật `prefers-reduced-motion` hoặc khi FPS < 40 (hiện thông báo).
- **Link chia sẻ:** toàn bộ tham số nằm trong query. Không đưa dữ liệu cá nhân vào URL.
- CSS mới dùng tiền tố `hl-`, đặt ở cuối `style.css`. Không sửa class cũ.

---

## 11. Kiểm thử: bảng số chuẩn do Claude tự tính

R = 8,314462618 J/(mol·K), R = 0,0831446 L·bar/(mol·K). Dung sai ±0,1 % nếu không ghi khác.

### 11.1 `tests/js/hoa_ly_chung.test.mjs`

- `vachDep(0, 109, 5)` → [0, 20, 40, 60, 80, 100] (hoặc có thêm 120 nếu hàm chọn bao trọn khoảng). Mọi vạch là bội của 1, 2 hoặc 5 × 10ⁿ.
- `ngau-nhien` cùng seed → cùng dãy số. Trung bình `maxwell3D(v²)` = 3kT/m ± 2 % (N = 20 000).
- Va chạm hạt–hạt bảo toàn động lượng và năng lượng (1e-9).
- Hạt không bao giờ ra khỏi thành, kể cả khi thành di chuyển nhanh.

### 11.2 `tests/js/hoa_ly_khi_ly_tuong.test.mjs`

**Thể tích mol**
- n = 1, T = 298,15 K, p = 1 bar → **V = 24,790 L**.
- 273,15 K, 1 atm → **22,414 L**.

**Vận tốc ở 300 K** (m/s)

| Khí | v_rms | v_p | v̄ |
|---|---|---|---|
| N₂ | 516,8 | 422,0 | 476,2 |
| O₂ | 483,6 | 394,8 | 445,5 |
| He | 1367,2 | 1116,3 | 1259,7 |
| H₂ | 1926,6 | 1573,1 | 1775,0 |
| CO₂ | 412,4 | – | – |
| Ar | 432,8 | – | – |

**Các giá trị khác**
- W̄đ(300 K) = **6,2129·10⁻²¹ J**.
- Kiểm K2: 1 mol N₂ ở 300 K trong V = nRT/p (p = 1 bar): ⅓·(N/V)·m·v_rms² = **100 000 Pa** (sai số 1e-6).
- Đẳng nhiệt: n = 1, T = 300 K, V 10 → 20 L: **A′ = 1728,94 J**, Q = A′, ΔU = 0.
- Đoạn nhiệt từ 300 K, V giảm còn ½: γ = 1,4 → T = **395,85 K**, p × **2,6390**; γ = 5/3 → T = **476,22 K**.
- Van der Waals CO₂, n = 1, T = 300 K:

| V | p lý tưởng | p Van der Waals | Z |
|---|---|---|---|
| 0,5 L | 49,887 bar | **39,981 bar** | 0,8014 |
| 0,2 L | 124,717 bar | **67,542 bar** | 0,5416 |
| 1,0 L | 24,943 bar | **22,415 bar** | 0,8986 |

- Charles (p = 1 bar, n = 1): khớp hai điểm 0 °C và 100 °C rồi kéo dài → cắt trục ở **−273,15 °C**.
- Định luật I trong mọi quá trình: ΔU = A + Q (1e-9).

### 11.3 `tests/js/hoa_ly_can_bang.test.mjs`

Hệ thử viết theo chiều phân li, A ⇌ 2B (N₂O₄ ⇌ 2NO₂), Kc_ref = **4,61·10⁻³** mol/L ở 298,15 K, ΔH = **+57,2** kJ/mol. Số liệu nằm ngay trong test, không phụ thuộc file JSON.

**Kc theo van 't Hoff**

| T | Kc |
|---|---|
| 273,15 K | **5,5787·10⁻⁴** |
| 323,15 K | **2,7476·10⁻²** |
| 373,15 K | **0,47625** |

**Cân bằng ban đầu:** [A]₀ = 0,0400 M → x = **0,006238**, [N₂O₄] = **0,033762**, [NO₂] = **0,012476**, α = **0,1559**, Q = K.

**Nén còn ½ thể tích:**
- Ngay sau khi nén: Q = **9,22·10⁻³** = 2·K > K → chuyển dịch theo **chiều nghịch của phân li**, tức chiều tạo N₂O₄. Đây là chiều thuận của phản ứng 2NO₂ ⇌ N₂O₄ trong giao diện.
- Cân bằng mới: [N₂O₄] = **0,070957**, [NO₂] = **0,018086**.
- [NO₂] mới (0,0181) vẫn **lớn hơn** trước khi nén (0,0125) → màu vẫn đậm hơn lúc đầu, nhưng nhạt hơn ngay sau khi nén (0,0250). Test phải kiểm đủ chuỗi 0,0125 < 0,0181 < 0,0250.

**Các kiểm tra khác**
- Kp = Kc·(RT)^Δn ở 298,15 K = **0,11428** (bar).
- Bảng nhiệt động (để đối chiếu, không bắt buộc dùng): ΔfH NO₂ = 33,2, N₂O₄ = 9,16; S° NO₂ = 240,1, N₂O₄ = 304,29 → ΔH = 57,24 kJ, ΔS = 175,91 J/K → K°(298,15) = 0,1447 (bar).
- Haber với ΔH = −91,8 kJ, ΔS = −198,1 J/K: K°(298,15) = **5,435·10⁵**; K°(723,15) = **1,92·10⁻⁴**. Đây là kết quả khi coi ΔH, ΔS không đổi; giao diện phải ghi rõ giả định này.
- H₂ + I₂ ⇌ 2HI: nén hay giãn đều cho **Q′ = Q** (Δn = 0).
- Xúc tác: k_t và k_n cùng nhân e^(ΔEa/RT), K không đổi (1e-12). Thời gian đạt 99 % cân bằng giảm đúng bằng hệ số đó.
- Mô hình ODE xuất phát từ mọi trạng thái ban đầu đều hội tụ về đúng nghiệm `canBangTuBanDau` (1e-6).

### 11.4 `tests/js/hoa_ly_toc_do.test.mjs`

Ea = 50 kJ/mol, A = 1,0·10¹⁰.

**Tỉ số tốc độ và hệ số Van't Hoff**
- k(308,15)/k(298,15) = **1,9243**; k(348,15)/k(298,15) = **18,114**.
- γ xấp xỉ theo D3: e^(10Ea/RT²) ở 298,15 K = **1,9670**.
- Ea để γ = 2 ở 298,15 K = **51,23 kJ/mol**.

**Hằng số tốc độ k**

| T | 298,15 K | 308,15 K | 318,15 K | 328,15 K |
|---|---|---|---|---|
| k | 17,393 | 33,469 | 61,807 | 109,95 |

Khớp Arrhenius bằng 4 điểm này phải ra lại **Ea = 50,00 kJ/mol** (1e-6) và **A = 1,0·10¹⁰**.

**Xúc tác:** giảm 20 kJ/mol ở 298,15 K → × **3190,4**.

**Theo bậc phản ứng**
- Bậc 1, t½ = 30 s → k = **0,023105 s⁻¹**; C(60 s)/C₀ = **0,25**.
- Bậc 2, C₀ = 0,1 M, k = 0,5 L/(mol·s): C(20 s) = **0,05 M**, t½ = **20 s**.
- Bậc 0: C = C₀ − kt, chặn ở 0.

**Phân bố năng lượng:** x = Ea/RT (298,15 K) = **20,170**; e^(−x) = **1,7393·10⁻⁹**; tỉ lệ 3D = **9,028·10⁻⁹**.

**Mô hình hạt** (seed cố định, ≥ 5000 va chạm A–B, E₀/kT = 2): tỉ lệ va chạm có E_n ≥ E₀ = **0,1353 ± 10 %**. Tăng gấp đôi số hạt A → số va chạm A–B/s tăng 2 lần ± 10 %.

### 11.5 `tests/test_hoa_ly.py` (bổ sung)

1. **Route chung** `/hoa-hoc/hoa-ly/{ma}`:
   - 200 cho 5 mã có `trang_thai == "co"`.
   - 404 cho mã không có, mã `sap-ra-mat`, mã chứa `..` hoặc `/` hay ký tự lạ.
   - Chưa đăng nhập → chuyển sang trang đăng nhập.
   - Mọi `/static/…js|css` trong HTML có `?v=`.
2. Tên của 5 mục không bị viết cứng trong HTML (luật cũ, mở rộng cho cả 5 mục).
3. **`cong-thuc.json`:**
   - Đúng schema; `id` không trùng.
   - Có ≥ 30 công thức; mỗi chủ đề có ≥ 4 công thức.
   - Mỗi mô phỏng có ≥ 5 công thức trỏ tới nó.
   - Mọi `mo_phong[].id` tồn tại trong `danh-muc.json`.
   - Mỗi `chung_minh.buoc` có ≥ 3 bước, trừ `dinh-luat-thuc-nghiem`.
   - Có đủ các id bắt buộc ở mục 5.3: K1–K8, N1–N8, H1–H3, D1–D7, C1–C7, đặt theo `id` tự chọn nhưng có trường `ma: "K1"`.
4. **`vi_du_so`:** gọi `node -e` với đúng `ham` của `mo-hinh.js` để tính lại và so `ra` ± `sai_so`. Không có node thì `skip`.
5. Các file `khi.json`, `he-can-bang.json`, `phan-ung.json`, `cau-hoi.json` của 3 mô phỏng: đủ trường bắt buộc, có `nguon`, mỗi dạng câu hỏi đủ 3 câu, câu có `tinh_bang` thì tính lại khớp.
6. Chế độ app (`che_do_app = True`): 5 trang không còn CDN nào ngoài Google Fonts. MathJax trỏ tới `/static/vendor/mathjax.3.2.2.js`. Font MathJax tồn tại trong `frontend/vendor/mathjax-fonts/woff-v2/`.
7. **Toàn bộ test cũ PASS:** `.venv/bin/python -m pytest -q` và `node --test tests/js/`.

### 11.6 Kiểm tra trình duyệt thật: `tools/kiem_tra_hoa_ly.py` (viết lại cho cả 5 trang)

- Dùng `channel="chrome"`, máy chủ 127.0.0.1:8000.
- Mỗi kịch bản in PASS/FAIL kèm **con số thật**.
- Ảnh chụp lưu vào `outputs/hoa-ly/`. Chụp ảnh chỉ là phần phụ, **mọi kết luận phải dựa trên assert số**.

**GĐ0 – Động cơ nhiệt**
1. Các kích thước ở mục 6, đo ở 1280×800, 1440×900, 390×844, và khi thu gọn bảng.
2. 100 % `HoaLy.hat()` nằm trong `boCuc().vungKhi`.
3. Chữ trục ≥ 12 px.
4. Chuyển sang chế độ "Thẳng hàng như video" → |x chấm đỏ − x mặt pít-tông| ≤ 2 px.
5. Chạy lại 13 kịch bản cũ.

**Khí lý tưởng**
1. Giữ T, kéo pít-tông cho V từ 24,79 L xuống 12,40 L → p = 2,000 bar ± 1 %, T không đổi.
2. Cách nhiệt: từ trạng thái mặc định, kéo cho V giảm còn ½ với N₂ → T = 298,15·2^0,4 = 393,41 K ± 1 %.
3. Giữ p, nung tới 596,3 K → V tăng gấp đôi ± 1 %; khi giữ p thì pít-tông không kéo được.
4. Bơm 5 lần → n = 1,25 mol (n₀ = 1). Số hạt tăng.
5. `p_do_bar` trung bình trong 4 s lệch ≤ 5 % so với `p_bar`.
6. Kéo chấm trên p–V khi đang giữ T → chấm luôn nằm trên đúng đường đẳng nhiệt (pV = hằng ± 0,5 %).
7. Tab Phân bố tốc độ: đỉnh histogram ở v_p ± 15 %.
8. 100 % hạt nằm trong bình; kích thước đồ thị ≥ 560×300 ở 1280×800.

**Cân bằng (hệ 2NO₂ ⇌ N₂O₄)**
1. Chờ cân bằng → Q/K = 1 ± 1 %.
2. Kéo pít-tông cho V còn ½ → ngay lập tức [NO₂] × 2. Sau đó [NO₂] giảm dần, nhưng ở cân bằng mới vẫn lớn hơn giá trị ban đầu. Chiều chuyển dịch = tạo N₂O₄. Độ đậm màu nền giảm dần sau lúc nén.
3. Kéo núm nhiệt về "nước đá" (0 °C) → Kc đổi theo van 't Hoff; [NO₂] giảm, màu nhạt đi.
4. Hệ H₂ + I₂: nén V → chiều = "không đổi"; Q trước và sau bằng nhau.
5. Haber: bật xúc tác → Kc không đổi; thời gian đạt cân bằng giảm.
6. Kéo cột NO₂ lên → có sự kiện mới trên đồ thị C–t; thẻ Le Chatelier có ≥ 3 câu, chứa đúng chữ "nghịch" hoặc "thuận" theo số liệu.

**Tốc độ phản ứng**
1. Kéo đỉnh Ea từ 50 lên 70 kJ/mol → k giảm đúng theo e^(−ΔEa/RT) ± 0,5 %.
2. Kéo bóng lên 80 % độ cao đỉnh rồi thả → bóng quay về phía chất đầu. Kéo qua đỉnh rồi thả → bóng sang phía sản phẩm.
3. Kéo nhiệt kế từ 298 K lên 308 K → k × 1,924 ± 1 %.
4. Bật xúc tác → đường nét đứt xuất hiện; k tăng đúng hệ số.
5. Tab Thí nghiệm chạy ở 4 nhiệt độ → Ea khớp ra = Ea đặt ± 2 %.
6. Bộ đếm: sau 10 s, `tiLeDo` / `tiLeLyThuyet` trong khoảng 0,85–1,15.

**Công thức**
1. Trang `/hoa-hoc/hoa-ly/cong-thuc` hiện ≥ 30 thẻ.
2. Tìm "dang nhiet" (không dấu) → có K7.
3. Bấm "Chứng minh" của N7 → hộp mở, có "Bước 1/…". Bấm "Bước tiếp" tới cuối → ô "Kiểm tra bằng số" hiện η = 0,5647 ✓.
4. Esc đóng hộp, focus quay về nút vừa bấm.
5. Trên trang không có phần tử `mjx-merror` (lỗi MathJax).
6. `?chung-minh=K2` mở luôn hộp của K2. "Thử trong mô phỏng" mở Khí lý tưởng với chế độ "Theo dõi 1 phân tử".

**Chung cho 5 trang**
1. Chế độ tối: nền `rgb(9, 9, 11)`; không có phần tử SVG nào dùng mã hex viết cứng. Quét thuộc tính `fill`/`stroke` có dạng `#` trong SVG; được phép nếu là `url(#…)`.
2. Ở 390×844: không có cuộn ngang; kéo bằng cảm ứng làm trạng thái thay đổi.
3. Console không có lỗi.

---

## 12. Giai đoạn và tiêu chí nghiệm thu

Mỗi giai đoạn xong: chạy test → cập nhật HANDOFF.md → `git add -A && git commit`.

| GĐ | Việc | Xong khi |
|---|---|---|
| 0 | Thư mục `chung/` (dinh-dang, ngau-nhien, hat, ve-hat, do-thi, keo, bang-mau, khung-trang, cau-hoi) + **phóng to hình Động cơ nhiệt** + sửa lỗi ở mục 2 | 11.1 PASS; kịch bản GĐ0 ở 11.6 PASS; test cũ PASS |
| 1 | `cong-thuc.json` (≥ 30 công thức theo 5.3), `chung/cong-thuc.js` + `hinh-chung-minh.js`, font MathJax offline, trang `/cong-thuc`, tab Công thức trong Động cơ nhiệt, thẻ thư viện trên trang Hoá lý | 11.5 mục 3, 4, 6 PASS; kịch bản Công thức PASS |
| 2 | **Khí lý tưởng**, P1 → P2 | 11.2 PASS; kịch bản Khí lý tưởng PASS; `danh-muc` của mục này chuyển sang `"co"` |
| 3 | **Cân bằng & Le Chatelier**, P1 → P2 | 11.3 PASS; kịch bản Cân bằng PASS; `"co"` |
| 4 | **Tốc độ phản ứng & Ea**, P1 → P2 | 11.4 PASS; kịch bản Tốc độ PASS; `"co"` |
| 5 | Các phần P3 (Van der Waals, G–ξ, thuận nghịch nối sang Cân bằng) + rà lại di động, tối, hiệu năng + thử trong **app máy tính** (`CHE_DO_APP=1`, không có mạng) | Toàn bộ 11.x PASS; 5 trang chạy offline được, công thức hiển thị đúng font |
| 6 | Tài liệu: HANDOFF, CLAUDE.md (sơ đồ `chung/` + 3 mô phỏng + route chung + cong-thuc.json), DAN_Y_HOA_HOC.md, GHI_CONG.md | Báo cáo cuối (mục 13) |

---

## 13. Bàn giao

- **HANDOFF.md** theo mẫu AGENTS.md, ghi **đúng các con số đo được** (kích thước hình trước và sau, số test PASS, kết quả 11.6). Không ghi kiểu "đã kiểm tra bằng mắt".
- **Báo cáo ngắn cho người dùng:**
  - Đã làm gì.
  - Các trang mở thử:
    - `http://127.0.0.1:8000/hoa-hoc/hoa-ly`
    - `…/khi-ly-tuong`
    - `…/can-bang-le-chatelier`
    - `…/toc-do-phan-ung`
    - `…/cong-thuc`
  - Cách đổi tên: sửa `ten` trong `danh-muc.json`.
  - Cách thêm công thức: thêm một mục vào `cong-thuc.json`.
  - Liệt kê **trung thực** những gì chưa làm được hoặc chưa kiểm được.

---

## 14. KHÔNG làm

- Không dùng 3D, Three.js hay Unity. Không thêm thư viện hay CDN mới (ngoại lệ duy nhất là MathJax 3.2.2 đã có sẵn và font của nó).
- Không chép mã của PhET (GPLv3), Concord (giấy phép chưa rõ) hay LearnChemE (NC). Chỉ học ý tưởng. Nguồn tham khảo ghi vào GHI_CONG.md.
- Không viết sẵn chữ "giải thích Le Chatelier" một cách cứng nhắc. Mọi kết luận phải sinh từ Q, K và Δn của mô hình.
- Không tự đổi khung chứng minh ở mục 5.3, không "chứng minh" định luật thực nghiệm. Thấy sai thì ghi vào HANDOFF ở mục "LỖI CÒN TỒN" để Claude xem lại.
- Không viết cứng tên mô phỏng ngoài `danh-muc.json`. Không viết cứng màu hex trong file vẽ.
- Không sửa class CSS cũ. Không đụng vùng Phòng Lab Sinh học. Không sửa `.env`. Không push hay deploy.
- Không báo "đã kiểm tra" khi chưa chạy `tools/kiem_tra_hoa_ly.py` với assert số.
