# PROMPT – Mục HOÁ LÝ + Mô phỏng 2D "Động cơ nhiệt 4 kỳ" điều khiển bằng kéo chuột

> Dán nguyên file này cho Antigravity (Gemini). Làm liên tục đến hết 5 giai đoạn, tự chủ theo AGENTS.md.
> Tên hiển thị của mô phỏng là **TÊN TẠM** ("Động cơ nhiệt 4 kỳ"), người dùng sẽ đặt tên chính thức sau, nên tên phải đổi được ở **một chỗ duy nhất** (mục 4.2).
> Viết bởi Claude ngày 25/10/2026, sau khi xem video tham chiếu, đọc cấu trúc app và tự tính bảng số chuẩn ở mục 9.

---

## 0. Trước khi làm (bắt buộc)

1. Đọc `AGENTS.md`, `CLAUDE.md`, `HANDOFF.md`, `DAN_Y_HOA_HOC.md`.
2. **Không chạy song song** với `PROMPT_NHAN_VAT_VAO_APP_ANIMATE_UI.md`, vì cả hai cùng sửa `frontend/index.html`, `frontend/style.css` và `backend/main.py`.
   Lúc viết prompt này, `git status` vẫn còn thay đổi chưa commit của việc nhân vật (`index.html`, `app.js`, `cai-dat.*`, `main.py`, `frontend/nhan-vat/`, `chuyen-dong.js`…).
   - Nếu việc nhân vật **đang chạy**: chờ nó commit xong rồi mới làm.
   - Nếu việc nhân vật **đã xong nhưng chưa commit**: tạo điểm phục hồi như thường lệ: `git add -A && git commit -m "chore: diem phuc hoi truoc muc hoa ly"`.
   - **Không** revert hay sửa đè bất kỳ dòng nào của việc khác.
3. Không đụng vùng Phòng Lab Sinh học 3D (chỉ Claude được ghi).
4. Xem ảnh tham chiếu trong `.agents/tham-chieu/hoa-ly-dong-co-nhiet/` (4 ảnh ghép khung hình). Video gốc nằm ở `~/Downloads/snaptik.vn_7698415536446721300.mp4`; nếu không mở được video thì đọc mô tả ở mục 2 là đủ.
5. Máy làm việc: Apple M2, RAM 8 GB, ổ đĩa gần đầy. Không cài thêm phần mềm nặng. Thư mục dự án nằm trong iCloud nên đọc file chậm: dùng `grep`/`sed -n` theo file cụ thể, không `grep -r` toàn repo.

---

## 1. Dàn ý (yêu cầu của người dùng)

| # | Yêu cầu | Cách hiểu để làm |
|---|---|---|
| 1 | "Tạo 1 mục riêng gọi là Hoá lý" | Trang trung tâm mới `/hoa-hoc/hoa-ly`, có lối vào từ trang chủ. Đây là nơi chứa các mô phỏng Hoá lý (sau này thêm khí lý tưởng, cân bằng, động học…). |
| 2 | "Cái này cũng là 1 mục riêng trong Hoá lý" | Mô phỏng đầu tiên có trang riêng `/hoa-hoc/hoa-ly/dong-co-nhiet`, xuất hiện thành một thẻ trong trang Hoá lý. |
| 3 | "Tìm mô hình 2D thôi" | **2D** (SVG), không dùng Three.js/Unity. Kết quả tìm mô hình có sẵn ở mục 3: không có cái nào dùng lại được về giấy phép, nên tự dựng. |
| 4 | "Dựng lại theo chuyển động của chuột mà mình kéo" | Người dùng **kéo** bánh đà, pít-tông, chấm trạng thái trên đồ thị hoặc thanh thời gian, và toàn bộ mô hình chạy theo tay (mục 6). |
| 5 | "Dựng lại đầy đủ hơn, thêm vài tính năng" | Có số liệu thật, van, bugi, công/nhiệt, đồ thị phụ, nhiên liệu, định luật I, quán tính, đề máy… (mục 8). |
| 6 | "Sử dụng chủ đề giống với app" | Dùng hệ thiết kế Awesomic của app (token màu trong `style.css`, font Be Vietnam Pro, sáng/tối dùng chung khoá). Chi tiết ở mục 7. |
| 7 | "Tên tôi sẽ đặt sau" | Tên chỉ nằm trong `danh-muc.json`, không viết cứng ở chỗ nào khác (mục 4.2). |

---

## 2. Video tham chiếu: mô tả chính xác

Video 550×650 px, dài 32 s, chạy 2 vòng chu trình. Kiểu hình là đồ thị matplotlib nét đen trên nền trắng.

**Nửa trên: đồ thị p–V** (tiêu đề "4-stroke Otto cycle")
- Trục đứng là p, trục ngang là V. Có 5 điểm đánh số:
  - 0 = (V nhỏ, p thấp)
  - 1 = (V lớn, p thấp)
  - 2 = (V nhỏ, p vừa)
  - 3 = (V nhỏ, p cao nhất)
  - 4 = (V lớn, p hơi cao hơn điểm 1)
- Màu các đoạn: 2→3 **đỏ** (cấp nhiệt), 4→1 **xanh dương** (thải nhiệt), các đoạn còn lại màu đen. Đoạn 0–1 là một đường nằm ngang dùng chung cho cả nạp lẫn xả.
- **Chấm đỏ** là trạng thái hiện tại. Bên phải đồ thị có chữ tên quá trình: "Adiabatic compression", "Adiabatic expansion", "Exhaust the gas", "Intake the fuel".

**Nửa dưới: cơ cấu máy**
- Xi-lanh nằm ngang, nắp ở bên trái. Pít-tông là một vạch đen dày. Cán pít-tông nối với thanh truyền, thanh truyền nối với chốt khuỷu nằm trên **bánh đà tròn màu xám có 4 lỗ trắng**.
- Bánh đà quay **theo chiều kim đồng hồ** trên màn hình. Điểm chết trên (ĐCT) là lúc chốt khuỷu ở vị trí 9 giờ.

**Chi tiết quan trọng nhất cần giữ:** một **đường chấm thẳng đứng** nối chấm đỏ trên đồ thị xuống đúng mặt pít-tông. Nghĩa là **trục V của đồ thị thẳng hàng với vị trí pít-tông**: V = 0 trùng với nắp xi-lanh.

**Màu khí theo nhiệt độ**
- Khí nạp vào màu xanh tím nhạt.
- Khi nén, khí chuyển dần sang trắng rồi hồng.
- Ngay sau đánh lửa, khí đỏ đậm.
- Khi giãn nở, màu nhạt dần.
- Khi xả, khí lại xanh nhạt.

**Nhãn dưới xi-lanh**
- "d'Q = 0" trong các quá trình đoạn nhiệt.
- "p = const." khi nạp và khi xả.
- Mũi tên ← ở nắp khi xả, mũi tên → khi nạp.

**Thứ tự các quá trình**
1. Nén đoạn nhiệt 1→2.
2. Đánh lửa tức thời 2→3, pít-tông đứng yên ở ĐCT.
3. Giãn nở đoạn nhiệt 3→4.
4. Thải nhiệt tức thời 4→1, pít-tông ở ĐCD.
5. Xả 1→0.
6. Nạp 0→1.

**Thiếu sót của video, bản mới phải khắc phục**
- Chỉ xem được, không tương tác.
- Không có số liệu và đơn vị.
- Không có van và bugi.
- Không thấy được công và nhiệt.
- Chữ bằng tiếng Anh.
- Đánh lửa tức thời nên không quan sát kịp.

---

## 3. Kết quả "tìm mô hình 2D" và quyết định

Claude đã tìm (25/10/2026):

| Nguồn | Là gì | Dùng được không |
|---|---|---|
| LearnChemE "Otto Cycle" (learncheme.com/simulations/thermodynamics/thermo-1/otto-cycle/) | Mô phỏng chạy bằng Wolfram Player, có đồ thị p–V, T–s, công thức η, tỉ số nén | Giấy phép **CC BY-NC-SA** → chỉ tham khảo cách trình bày, **không chép** |
| UNLV PHYS 115 `ottoCycle2D` | Trang JS có thanh trượt góc quay, độ dài tay quay và thanh truyền, kèm đồ thị P–V | Không ghi giấy phép → chỉ tham khảo |
| Wikimedia Commons `4-Stroke-Engine.gif` (UtzOnBike) | Ảnh động mô hình 3D chiếu xuống 2D, không có đồ thị p–V. Trang thảo luận chê là không có thể tích buồng cháy ở ĐCT | Bài học rút ra: **luôn có Vc > 0** |
| Wikimedia Commons `P-V_Otto_cycle.svg` | Đồ thị tĩnh | Chỉ tham khảo |
| Video người dùng gửi (TikTok tải lại) | Hoạt ảnh tham chiếu | Chỉ dùng làm mẫu thị giác |

Không có thư viện JS mã nguồn mở (MIT…) nào làm sẵn cơ cấu tay quay kèm đồ thị p–V mà kéo được.

**Quyết định**
- Tự dựng bằng **SVG + Pointer Events + JS thuần**, **không thêm thư viện hay CDN mới**.
- Lý do: app máy tính chạy offline, và `_trang()` trong `backend/main.py` chỉ thay được các CDN đã liệt kê sẵn (Tailwind 3.4.16, gsap, three, MathJax).
- Mọi hình đều vẽ bằng code. Không tải ảnh hay asset ngoài.
- Lớp hạt khí (mục 8, P2) dùng một `<canvas>` nhỏ đặt chồng lên xi-lanh.

---

## 4. Kiến trúc file và route

### 4.1 File mới / file sửa

```
frontend/hoa-ly.html                                  ← trang trung tâm "Hoá lý" (lưới thẻ)
frontend/hoa-hoc/hoa-ly/danh-muc.json                 ← danh mục mô phỏng = NGUỒN DUY NHẤT của tên hiển thị
frontend/hoa-hoc/hoa-ly/trung-tam.js                  ← đọc danh-muc.json → vẽ thẻ
frontend/hoa-hoc/hoa-ly/dong-co-nhiet/
  trang.html          ← trang mô phỏng
  nhiet-dong.js       ← UMD thuần (window.HLNhietDong): Otto, Diesel, entropy, định luật I, nhiên liệu, Wiebe
  co-cau.js           ← UMD thuần (window.HLCoCau): tay quay–thanh truyền thuận/nghịch, ds/dθ
  tien-do.js          ← UMD thuần (window.HLTienDo): tham số chu trình t, ánh xạ góc ⇄ t ⇄ điểm p–V, quy tắc liên tục
  dong-luc.js         ← UMD thuần (window.HLDongLuc): quán tính bánh đà, chế độ đề máy
  ve.js               ← vẽ SVG: đồ thị, xi-lanh, bánh đà, van, bugi, nhãn (màu lấy từ token CSS)
  tuong-tac.js        ← Pointer Events: kéo bánh đà / pít-tông / chấm đồ thị / thanh thời gian + phím tắt
  giao-dien.js        ← bảng điều khiển, bảng trạng thái, tab đồ thị phụ, tab Học, xuất ảnh, link chia sẻ
  hat-khi.js          ← (P2) canvas hạt khí
  nhien-lieu.json     ← nhiên liệu: công thức, ΔfH°, trạng thái, nguồn số liệu
  cau-hoi.json        ← câu hỏi 3 dạng TN THPT
backend/main.py       ← THÊM 2 route (đặt cạnh route phòng thí nghiệm)
frontend/index.html   ← THÊM lối vào Hoá lý (banner + 1 link menu), không sửa gì khác
frontend/style.css    ← THÊM class tiền tố `hl-` Ở CUỐI FILE (quy ước CLAUDE.md)
tests/js/hoa_ly_nhiet_dong.test.mjs · hoa_ly_co_cau.test.mjs · hoa_ly_tien_do.test.mjs · hoa_ly_dong_luc.test.mjs
tests/test_hoa_ly.py  ← route, JSON, lối vào trang chủ, chế độ app, câu hỏi
tools/kiem_tra_hoa_ly.py ← kiểm tra trình duyệt thật bằng Playwright (có assert số) + ảnh chụp vào outputs/hoa-ly/
```

Các module UMD viết theo đúng mẫu `frontend/hoa-hoc/phong-thi-nghiem/tinh-toan.js`:
- Trên trình duyệt gắn vào `window.X`; trong Node dùng `require`.
- Không chạm DOM.
- Có hàm `dinhDang` dùng dấu phẩy thập phân.

**Nguồn sự thật duy nhất** cho mọi con số là `nhiet-dong.js`, `co-cau.js` và `tien-do.js`. Các file vẽ và giao diện chỉ hiển thị, không tự tính lại.

### 4.2 Route và tên hiển thị

```python
@app.get("/hoa-hoc/hoa-ly")
def trang_hoa_ly(request: Request):
    """Mục Hoá lý: lưới mô phỏng đọc từ frontend/hoa-hoc/hoa-ly/danh-muc.json."""
    return _trang_can_dang_nhap(request, "hoa-ly.html")

@app.get("/hoa-hoc/hoa-ly/dong-co-nhiet")
def trang_hoa_ly_dong_co_nhiet(request: Request):
    """Mô phỏng 2D động cơ nhiệt 4 kỳ (chu trình Otto/Diesel), điều khiển bằng kéo chuột."""
    return _trang_can_dang_nhap(request, "hoa-hoc/hoa-ly/dong-co-nhiet/trang.html")
```

- `_trang()` chỉ gắn `?v=<băm>` cho chuỗi dạng `"/static/…/x.js|css"` nằm **trong ngoặc kép**. Vì vậy mọi `<script src>` và `<link href>` phải viết đúng dạng đó.
- Slug `dong-co-nhiet` là cố định, **không đổi theo tên hiển thị**.

`danh-muc.json`:

```json
{
  "phien_ban": 1,
  "ten_muc": "Hoá lý",
  "mo_ta_muc": "Mô phỏng tương tác các quy luật nhiệt động học, khí, cân bằng và tốc độ phản ứng.",
  "mo_phong": [
    { "id": "dong-co-nhiet", "ten": "Động cơ nhiệt 4 kỳ", "ten_tam": true,
      "phu_de": "Chu trình Otto · kéo chuột điều khiển", "mo_ta": "…",
      "duong_dan": "/hoa-hoc/hoa-ly/dong-co-nhiet", "bieu_tuong": "⚙️",
      "nhan": ["Nhiệt động học", "Định luật I", "Mở rộng 10–12 · HSG"], "trang_thai": "co" },
    { "id": "khi-ly-tuong", "ten": "Khí lý tưởng pV = nRT", "trang_thai": "sap-ra-mat" },
    { "id": "can-bang-le-chatelier", "ten": "Cân bằng hoá học & Le Chatelier", "trang_thai": "sap-ra-mat" },
    { "id": "toc-do-phan-ung", "ten": "Tốc độ phản ứng & năng lượng hoạt hoá", "trang_thai": "sap-ra-mat" }
  ]
}
```

**Quy tắc tên (để người dùng đổi tên ở một chỗ)**
- Trang mô phỏng `fetch("/static/hoa-hoc/hoa-ly/danh-muc.json", {cache: "no-cache"})` rồi lấy `ten` để đặt `<title>`, tiêu đề và đường dẫn breadcrumb.
- HTML chỉ có chữ dự phòng chung là "Mô phỏng · Hoá lý".
- Test phải kiểm rằng `trang.html`, `hoa-ly.html` và `index.html` **không chứa** chuỗi "Động cơ nhiệt 4 kỳ".
- Không hiện huy hiệu "tên tạm" trên giao diện. Cờ `ten_tam` chỉ để code biết.

### 4.3 Lối vào từ trang chủ (`frontend/index.html`)

- **Banner thứ 3:** đặt ngay sau banner "Phòng thí nghiệm Hoá học ảo", **sao chép đúng cấu trúc** `.banner-lab-sinh-hoc > a.the-lab-ngang` với:
  - `href="/hoa-hoc/hoa-ly"`
  - Biểu tượng 🌡️
  - Hai nhãn: "HOÁ LÝ" và "MÔ PHỎNG 2D · KÉO THẢ"
  - Tiêu đề: "Hoá lý"
  - Mô tả: "Mô phỏng nhiệt động học tương tác: động cơ nhiệt 4 kỳ, đồ thị p–V và T–s, định luật I…"
  - Nút: "Vào Hoá lý"
- **Menu desktop:** trong `nav.nav-awesomic`, sau link "Phòng TN ảo ⚗️", thêm `<a href="/hoa-hoc/hoa-ly" class="nav-link-awe">Hoá lý</a>`.
- Không đổi bất kỳ thứ gì khác trên trang chủ.
- Ghi chú cho sau này: khi làm trang chủ 3 nhóm (DAN_Y, Giai đoạn 2), mục Hoá lý thuộc nhóm "Đại cương & Hoá lí".

---

## 5. Mô hình vật lý (chu trình chuẩn không khí, khí lý tưởng)

### 5.1 Thông số mặc định (tất cả chỉnh được, trừ R)

| Ký hiệu | Ý nghĩa | Mặc định | Khoảng |
|---|---|---|---|
| B | đường kính xi-lanh | 80 mm | 50–120 |
| S | hành trình pít-tông | 80 mm | 50–120 |
| L | chiều dài thanh truyền | 140 mm | ≥ 2,5·(S/2) |
| r | tỉ số nén | 10 | 4–14 (Otto), 12–22 (Diesel) |
| γ | chỉ số đoạn nhiệt cp/cv | 1,4 | 1,25–1,67 |
| R | hằng số khí riêng (không khí) | 0,287 kJ/(kg·K) | cố định |
| T₁ | nhiệt độ khí nạp | 300 K | 250–350 |
| p₁ | áp suất nạp | 1 bar = 100 kPa | 0,5–2 (có tăng áp) |
| q_in | nhiệt cấp cho 1 kg khí | 1500 kJ/kg | 200–2500, hoặc tính từ nhiên liệu (5.4) |
| n | tốc độ quay (chỉ dùng để tính công suất khi kéo tay) | 1200 vòng/phút | 300–6000 |

### 5.2 Công thức

**Hình học xi-lanh**

```
A  = πB²/4
Vd = A·S
Vc = Vd/(r − 1)
V1 = Vc + Vd
```

**Cơ cấu tay quay – thanh truyền** (a = S/2; θ = φ mod 360°, đo từ ĐCT)
- Quãng dịch của pít-tông tính từ ĐCT:
  `s(θ) = a(1 − cos θ) + L − √(L² − a² sin² θ)`
- Thể tích: `V(θ) = Vc + A·s(θ)`
- Nghịch đảo (cho trước s, tìm θ ∈ [0°, 180°]):
  - `d = a + L − s`
  - `cos θ = (d² + a² − L²)/(2ad)`
- Đạo hàm: `ds/dθ = a sin θ + a² sin θ cos θ / √(L² − a² sin² θ)`

**Chu trình Otto**

```
cv = R/(γ − 1)          cp = γ·cv
T2 = T1·r^(γ−1)         p2 = p1·r^γ
T3 = T2 + q_in/cv       p3 = p2·T3/T2
T4 = T3·r^(1−γ)         p4 = p1·T4/T1
q_out = cv(T4 − T1)     w = q_in − q_out
η = 1 − r^(1−γ)         MEP = w/(v1 − v2)        η_Carnot = 1 − T1/T3
```

- Trên các đường đoạn nhiệt: `p·V^γ = hằng`, `T·V^(γ−1) = hằng`.
- Trong lúc cấp nhiệt đẳng tích, gọi f ∈ [0, 1] là phần nhiệt đã cấp: `T = T2 + f·q_in/cv`.
- Nạp và xả (lý tưởng): p = p₁ và T = T₁. Khối lượng khí trong xi-lanh `m = p·V/(R·T)` thay đổi theo V; giao diện ghi rõ "khí đi vào / đi ra".
- Entropy, lấy trạng thái 1 làm gốc: `s − s1 = cv·ln(T/T1) + R·ln(v/v1)`.
- Mỗi chu trình:
  - `m = p1·V1/(R·T1)`
  - `W = m·w`
  - Công suất một xi-lanh: `P = W·(n/2)/60` (máy 4 kỳ: 1 chu trình ứng với 2 vòng quay).

**Chu trình Diesel (P3):** thêm tỉ số ngắt rc.

```
T3 = T2·rc      p3 = p2      V3 = rc·V2
3→4 giãn đoạn nhiệt về V1
q_in = cp(T3 − T2)
η = 1 − r^(1−γ)·(rc^γ − 1)/(γ(rc − 1))
```

Ở Diesel, quá trình 2→3 xảy ra **trong lúc pít-tông chạy**, nên kéo bánh đà sẽ đi qua đoạn này một cách liên tục.

### 5.3 Định luật I: quy ước dấu theo SGK Vật lí 12 (GDPT 2018)

- **ΔU = A + Q**
  - A > 0 khi khí **nhận** công.
  - Q > 0 khi khí **nhận** nhiệt.
- Trong từng quá trình: `ΔU = m·cv·ΔT` và `A = −∫p dV`.
- Bảng "Đang diễn ra" hiện ΔU, A, Q **tính từ đầu quá trình hiện tại đến thời điểm đang xem**, và phải thoả ΔU = A + Q (sai số ≤ 1e-6).
- Tab Học nói rõ:
  - Quy ước này khác với một số giáo trình Hoá lý đại học (ΔU = Q + W với W là công khí nhận, cùng nghĩa với A).
  - Khác với giáo trình viết ΔU = Q − W (W là công khí sinh ra).

### 5.4 Nhiên liệu → q_in (cầu nối với Hoá 10 "Năng lượng hoá học")

`nhien-lieu.json` gồm các nhiên liệu sau. Mỗi mục có công thức, M (theo nguyên tử khối SGK: C 12, H 1, O 16, N 14), ΔfH°₂₉₈ (kJ/mol) và `nguon`:

| Nhiên liệu | ΔfH° |
|---|---|
| isooctane C₈H₁₈(l) | −259,2 |
| ethanol C₂H₅OH(l) | −277,7 |
| methane CH₄(g) | −74,8 |
| hydrogen H₂(g) | 0 |

Dùng chung: CO₂(g) −393,5 · H₂O(g) −241,8 · H₂O(l) −285,8.

Đối chiếu các giá trị với bảng phụ lục SGK Hoá 10 (KNTT). Nếu SGK ghi khác (ví dụ CH₄ −74,6 hoặc −74,9) thì dùng số của SGK, ghi `nguon` và để test dung sai ±0,5 %.

**Cách tính (không viết cứng LHV, AFR)**
- Tự cân bằng phương trình cháy với không khí, coi không khí = O₂ + 3,76 N₂. Ví dụ:
  `C₈H₁₈ + 12,5 (O₂ + 3,76 N₂) → 8 CO₂ + 9 H₂O + 47 N₂`
- `ΔcH° = ΣΔfH°(sản phẩm) − ΣΔfH°(chất đầu)`, với nước ở thể **hơi** → ra nhiệt trị thấp (LHV). Tab Học cho xem thêm HHV (nước lỏng) để so sánh.
- `AFR = n(O₂)·(32 + 3,76·28) / M(nhiên liệu)`
- Hệ số dư không khí λ (0,8–1,3) và hệ số nhiệt hiệu dụng k (mặc định 0,55, khoảng 0,3–1; nghĩa là phần nhiệt cháy thực sự làm nóng khí, đã trừ tổn thất):
  `q_in = k·LHV·min(1, λ)/(λ·AFR + 1)`
- Khi T₃ > 3000 K: hiện cảnh báo nhẹ "Cao hơn động cơ thật (~2500–2800 K) vì mô hình bỏ qua phân li và truyền nhiệt qua thành".

### 5.5 Chu trình gần thực tế: cháy hữu hạn theo hàm Wiebe (P3, chỉ để minh hoạ)

- Phần nhiên liệu đã cháy:
  `x_b(φ) = 1 − exp(−a·((φ − φ0)/Δφ)^(m+1))`, với a = 5, m = 2, φ0 = 350°, Δφ = 60° (chỉnh được).
- Tích phân số theo φ:
  `dp/dφ = −γ·(p/V)·dV/dφ + (γ − 1)/V · dQ/dφ`, với `Q = m·q_in·x_b`.
- Van xả mở ở 520°; áp suất giảm mềm về p₁ trước 560°.
- Tính trước một bảng 1441 điểm mỗi khi đổi thông số, không tính lại trong từng khung hình.
- Hiệu suất lấy từ `∮p dV`. Đồ thị vẽ chu trình bo tròn, kèm chu trình lý tưởng dạng nét đứt để so sánh.

---

## 6. Tương tác kéo chuột (phần lõi, làm kỹ nhất)

### 6.1 Trạng thái chuẩn duy nhất

**Tiến độ chu trình** `t ∈ [0, 6)`:
- **Phần nguyên** của t là số thứ tự đoạn.
- **Phần lẻ** của t là tiến độ bên trong đoạn đó.
- Ngoài ra có biến đếm `vong` (số chu trình đã chạy) để cộng dồn liên tục, không bị quấn khi qua 6.

| Đoạn | Quá trình | Góc trục khuỷu φ | Ghi chú |
|---|---|---|---|
| 0 | Nạp 0→1 (p = p₁) | 0°→180° | van nạp mở, mũi tên → |
| 1 | Nén đoạn nhiệt 1→2 | 180°→360° | Q = 0 |
| 2 | Đánh lửa – cháy đẳng tích 2→3 | giữ 360° | phần lẻ = f (phần nhiệt đã cấp), bugi lóe |
| 3 | Giãn nở sinh công 3→4 | 360°→540° | Q = 0 |
| 4 | Mở van xả – thải nhiệt đẳng tích 4→1 | giữ 540° | phần lẻ = phần nhiệt đã thải |
| 5 | Xả 1→0 (p = p₁) | 540°→720° | van xả mở, mũi tên ← |

- Với các đoạn 0, 1, 3, 5: phần lẻ ánh xạ **tuyến tính theo φ** trong 180° của đoạn.
- **Trạng thái ban đầu và nút "Đặt lại":** t = 1 (điểm 1, bắt đầu nén), giống video.
- `tien-do.js` cung cấp các hàm thuần sau:
  - `tuTienDo(t, thamSo)` → `{phi, V, p, T, doan, f, ky}`
  - `buocGoc(t, dPhi, thamSo)` → `{t, suKien: []}`. Sự kiện gồm `danh_lua`, `danh_lua_nguoc`, `mo_van_xa`, `mo_van_xa_nguoc`, `het_chu_trinh`.
  - `tienDoTuDiemPV(V, p, tHienTai, thamSo, toaDo)`
  - `gocTuTheTich(V, phiHienTai, thamSo)`

### 6.2 Bốn tay nắm kéo

**1. Bánh đà (mặc định, như video)**
- Bấm vào **bất kỳ đâu trên bánh đà** (không bắt buộc trúng chốt khuỷu) rồi kéo vòng quanh tâm.
- Lấy góc chuột `ψ = atan2(...)`, tính `Δ = wrap(ψ − ψ_trước)` về khoảng (−180°, 180°], rồi `φ += Δ` cộng dồn liên tục.
- **Chiều thuận là chiều kim đồng hồ.** Kéo ngược chiều thì chu trình lùi lại (chữ "tua ngược" hiện mờ ở góc).
- Chốt khuỷu sáng lên khi rê chuột qua. Con trỏ là `grab`/`grabbing`.

**2. Pít-tông / cán pít-tông (kéo ngang)**
- Đổi vị trí x của chuột ra V (cùng tỉ lệ với trục V của đồ thị), rồi dùng `gocTuTheTich` tìm θ **trong nửa vòng hiện tại**.
- Kéo ngược hướng hành trình khi chưa tới điểm chết: pít-tông lùi trong cùng nửa vòng.
- Khi pít-tông đã chạm điểm chết (cách ≤ 0,5 % hành trình) mà người dùng kéo quay lại: **sang nửa vòng kế tiếp, luôn theo chiều tiến**, giống bánh đà có quán tính. Muốn lùi chu trình thì dùng bánh đà, đồ thị hoặc phím ←.

**3. Chấm trạng thái trên đồ thị p–V (và T–s)**
- Chiếu chuột lên điểm gần nhất của đường chu trình, tính theo khoảng cách **trên màn hình**.
- **Chỉ xét trong cửa sổ `t ± 0,75` đoạn quanh t hiện tại.** Lý do: đường nạp và đường xả **trùng nhau**, nếu không giới hạn thì chấm sẽ nhảy nhầm từ kỳ xả sang kỳ nạp.
- Kéo chấm cho phép **đi chậm qua 2→3 và 4→1**: pít-tông đứng yên, màu khí đổi dần, bugi sáng theo f.

**4. Thanh thời gian 0–720° (dưới hình)**
- Có 4 dải màu cho 4 kỳ và 2 vạch sự kiện (đánh lửa 360°, mở van xả 540°).
- Kéo hoặc bấm để tua. Hai đoạn đẳng tích hiện thành hai "nấc" hẹp có thể bấm vào.

### 6.3 Qua ĐCT / ĐCD khi kéo bánh đà hoặc pít-tông (chế độ lý tưởng)

- `buocGoc` cho t **nhảy qua** đoạn 2 (hoặc đoạn 4) và phát sự kiện.
- Bên vẽ chạy một **hoạt ảnh nối 250 ms**:
  - chấm đỏ chạy dọc đoạn thẳng đứng 2→3,
  - khí đổi sang đỏ,
  - bugi lóe màu `--ho-phach`,
  - nhãn "Đánh lửa!" nổi lên rồi mờ đi.
- Đi lùi thì chạy hoạt ảnh ngược lại.
- Hoạt ảnh **không chặn** thao tác kéo tiếp theo.

### 6.4 Quán tính khi thả (bật/tắt, mặc định bật)

- Lúc thả, lấy ω bằng trung bình dφ/dt của 80 ms cuối.
- Sau đó `dω/dt = −c·ω − ma_sat·sign(ω)`.
- Dừng hẳn khi |ω| < 2°/s.
- Bấm vào bánh đà trong lúc nó đang quay thì bắt lại ngay (như dùng tay giữ bánh đà).

### 6.5 Chế độ "Đề máy" – động cơ tự chạy (P2, file `dong-luc.js`)

**Phương trình chuyển động**
```
I·dω/dt = τ_khí − τ_ma sát − τ_tải
τ_khí   = (p − p_atm)·A·ds/dθ        (SI: Pa, m², m/rad)
τ_ma sát = c0 + c1·ω
```
- Mặc định I = 0,05 kg·m² (khoảng 0,02–0,2). Thanh trượt "Tải" 0–60 N·m. Với ca B, mô-men khí trung bình ≈ 468 J / 4π ≈ 37 N·m, nên tải lớn hơn mức này thì máy chậm dần rồi tắt.
- Khí được coi là tựa tĩnh, nên p là hàm của φ đã có sẵn trong `tien-do`.

**Thời gian thật và thời gian hiển thị**
- Mô phỏng tính theo **thời gian thật**. Hiển thị được làm chậm theo hệ số "Chậm ×k" (mặc định ×20; 1200 vòng/phút ↔ 1 vòng/s trên màn hình).
- Số vòng/phút ghi trên giao diện là số **thật**.
- Mỗi khung hình chia thành nhiều bước nhỏ, dt ≤ 1e-4 s, dùng Euler bán ẩn.
- Giới hạn tối đa 6000 vòng/phút.

**Cách người dùng đề máy:** quay mạnh bánh đà rồi thả.
- Nếu đủ năng lượng vượt kỳ nén, máy tự nổ và tăng tốc tới khi mô-men cân bằng. Năng lượng cần là ∫(p − p_atm)dV = m·cv·(T2 − T1) − p_atm·Vd ≈ 169 − 40 ≈ 129 J, tức ≈ 690 vòng/phút với I = 0,05.
- Nếu không đủ, máy dừng ở kỳ nén và **dội ngược nhẹ**.
- Hiện đồng hồ vòng/phút, công suất, và dòng chữ "Máy đã nổ" / "Chưa đủ đà".

### 6.6 Phím và cảm ứng

**Phím tắt**
- ← / → : 1°
- Shift + ← / → : 10°
- Space : chạy / dừng
- 1–4 : nhảy tới đầu kỳ 1–4
- R : đặt lại
- G : ghim chu trình (bóng ma)

Mỗi tay nắm kéo có `tabindex="0"`, `role="slider"`, `aria-valuenow` và `aria-label` tiếng Việt.

**Cảm ứng**
- Vùng kéo đặt `touch-action: none`. Dùng `setPointerCapture`.
- Tay nắm tối thiểu 44 px trên di động.

### 6.7 Hiệu năng

- Chỉ chạy `requestAnimationFrame` khi đang có chuyển động; dừng hẳn khi tab bị ẩn (`visibilitychange`).
- **Không tạo phần tử SVG mới trong mỗi khung hình**: tạo một lần, sau đó chỉ đổi thuộc tính.
- Đường chu trình chỉ tính lại khi đổi thông số.
- Mục tiêu: 60 khung/s trên M2 8 GB, trang dùng dưới 80 MB.

---

## 7. Giao diện: dùng chủ đề của app

**Phần `<head>`**
- Sao chép nguyên khối đầu của `frontend/phong-thi-nghiem.html` (dòng 1–40):
  - script chọn sáng/tối từ localStorage `"giai-chuoi-giao-dien"`
  - font Be Vietnam Pro + JetBrains Mono
  - Tailwind CDN **3.4.16** với `tailwind.config` màu `var(--…)`
  - `<link rel="stylesheet" href="/static/style.css">`
- Không thêm CDN nào khác. Tailwind sẽ tự được thay bằng bản offline khi chạy trong app máy tính.

**Thanh trên (theo kiểu app)**
- Logo `chemistry:` dẫn về `/`.
- Breadcrumb "Hoá học › Hoá lý › {ten}".
- Nút sáng/tối dùng cùng biểu tượng và cùng khoá localStorage với trang chủ (đổi ở đây thì trang chủ cũng đổi theo). Sao chép logic nút `#nut-che-do` hiện có.

**Token màu (lấy bằng `getComputedStyle`, đổi tự động khi chuyển sáng/tối)**

| Thành phần | Token |
|---|---|
| Nền | `--giay` |
| Thẻ | `--the-lon-nen`, viền `--chi`, bo `--r-lg` |
| Đường chu trình | `--muc` |
| 2→3 (cấp nhiệt) | `--gach` |
| 4→1 (thải nhiệt) | `--lam` |
| Lưới | `--chi` |
| Điểm nhấn, tay nắm | `--nhan` (xanh đen #0f2b48 ở chế độ sáng, #38bdf8 ở chế độ tối) |
| Tia lửa bugi | `--ho-phach` |
| Chữ phụ | `--lang` |
| Số | `--mono` |

**Thang màu nhiệt độ** `mauNhietDo(T)`:
- Đặt `u = ln(T/T1)/ln(T3/T1)`, giới hạn trong [0, 1].
- Nội suy bằng `color-mix(in oklab, …)`: `--lam` (u = 0) → `--the-lon-nen` (u ≈ 0,3) → `--gach` (u = 1).
- Hai chế độ sáng và tối đều phải đọc rõ.

**Bố cục desktop (≥ 1024 px)**
- **Cột trái (~62 %):** một thẻ chứa SVG chính, gồm:
  - đồ thị p–V ở trên,
  - xi-lanh + bánh đà ở dưới,
  - đường chấm nối giữa hai phần,
  - thanh thời gian ở đáy.
  - Bên trong xi-lanh còn có: nắp xi-lanh, **van nạp** (trái trên) và **van xả** (trái dưới) mở/đóng đúng kỳ, **bugi** ở giữa nắp, nhãn "ĐCT"/"ĐCD", thước V.
- **Cột phải (~38 %):**
  - Thẻ "Đang diễn ra" ở trên cùng: kỳ, quá trình, φ, p, V, T, ΔU / A / Q.
  - Bên dưới là 4 tab: **Thông số · Số liệu · Nhiên liệu · Học**.

**Bố cục di động (< 768 px)**
- Xếp dọc. SVG rộng hết màn hình.
- Bảng điều khiển thành **ngăn kéo đáy** giống phòng thí nghiệm ảo.
- Không được có cuộn ngang.

**Chữ hiển thị (tiếng Việt, đơn vị rõ, dấu phẩy thập phân)**

| Kỳ | Quá trình |
|---|---|
| Kỳ 1 · Nạp | Nạp hỗn hợp nhiên liệu – không khí (p = const) |
| Kỳ 2 · Nén | Nén đoạn nhiệt (Q = 0) |
| Đánh lửa | Cháy đẳng tích (V = const) |
| Kỳ 3 · Nổ – sinh công | Giãn nở đoạn nhiệt (Q = 0) |
| Mở van xả | Thải nhiệt đẳng tích |
| Kỳ 4 · Xả | Đẩy khí thải ra ngoài (p = const) |

- Nhãn trục: `p (bar)`, `V (cm³)`. Các điểm vẫn đánh số 0–4 như video.

**CSS:** class mới đều có tiền tố `hl-`, đặt cuối `style.css`, có phần tương ứng cho `:root[data-theme="toi"]`. Không sửa class cũ.

---

## 8. Tính năng (theo mức ưu tiên)

### P1 – bắt buộc (bản dựng lại "đầy đủ hơn video")

1. Xi-lanh, bánh đà, thanh truyền và đồ thị p–V thẳng hàng trục V; màu khí theo T; van và bugi.
2. Bốn cách kéo ở mục 6.2, kèm quy tắc liên tục ở 6.1 và 6.3.
3. Nút chạy/dừng; tốc độ 0,25×–4× (mặc định 1 chu trình / 8 s); nút "kỳ trước / kỳ sau"; nút "đặt lại".
4. Tab **Thông số:** thanh trượt r, γ, T₁, p₁, q_in, B, S, L.
   - Đổi r thì **buồng cháy co giãn thấy được** (ĐCT dịch chuyển, nắp xi-lanh đứng yên).
5. Tab **Số liệu:**
   - Bảng trạng thái 0–4 (p bar, V cm³, T K và °C).
   - Q_in, Q_out, W (J/chu trình và kJ/kg), η, η_Carnot, MEP, công suất ở n vòng/phút.
6. Thẻ "Đang diễn ra" có ΔU = A + Q cập nhật trực tiếp.
7. Sáng/tối, di động, phím tắt, cảm ứng.

### P2 – nên có

8. **Đồ thị phụ** dạng tab: T–s (s tương đối) và p–φ (0–720°). Chấm trạng thái đồng bộ; trên T–s cũng kéo được.
9. **Hiện công:**
   - Tô diện tích bên trong chu trình (= công có ích).
   - Tuỳ chọn tô diện tích dưới đường của quá trình đang diễn ra: màu xanh lục khi khí sinh công, màu đỏ khi khí nhận công.
10. **Trục p tuyến tính / logarit.** Ở dạng log–log, đường đoạn nhiệt thành đường thẳng có hệ số góc −γ; hiện nhãn hệ số góc.
11. **Ghim chu trình (bóng ma):** giữ tối đa 2 chu trình cũ (nét đứt, kèm nhãn r, γ, η) để so sánh sau khi đổi thông số.
12. **Tab Nhiên liệu:**
    - Chọn nhiên liệu, xem phương trình cháy đã cân bằng, các bước tính ΔcH° → LHV → AFR → q_in.
    - Thanh trượt λ và k; nút "Dùng q_in này".
13. **Quán tính bánh đà** (6.4) và **chế độ Đề máy** (6.5).
14. **Hạt khí** (`hat-khi.js`, canvas): khoảng 120 hạt; tốc độ tỉ lệ √T; mật độ tỉ lệ m/V.
    - Khi nạp, hạt đi vào qua van nạp; khi xả, hạt đi ra qua van xả.
    - Tự tắt khi bật `prefers-reduced-motion` hoặc khi FPS < 40.
15. **Xuất ảnh PNG/SVG** của hình chính.
16. **Link chia sẻ:** thông số nằm trong query, ví dụ `?r=8&g=1.4&q=1000&nl=isooctane&t=1.5`.
    - Mở link là khôi phục đúng.
    - Không đưa dữ liệu cá nhân vào URL.
17. **Tab Học:**
    - Giải thích ngắn từng quá trình (đồng bộ với đoạn đang xem).
    - Liên hệ Hoá 10 (enthalpy, nhiệt cháy) và Vật lí 12 (định luật I nhiệt động lực học).
    - Ghi chú về quy ước dấu.
    - **9 câu hỏi** trong `cau-hoi.json`, chia 3 dạng TN THPT mới, mỗi dạng 3 câu:
      - Phần I: 4 lựa chọn.
      - Phần II: Đúng/Sai 4 ý.
      - Phần III: Trả lời ngắn.
    - Câu nào có đáp án là số thì thêm khoá `"tinh_bang": {"ham": "otto", "tham_so": {...}, "truong": "T2", "lam_tron": 0}` để test tự tính lại.

### P3 – mở rộng (làm sau cùng)

18. **Chọn chu trình Otto | Diesel** (5.2), có thanh trượt rc.
19. **Chế độ gần thực tế (Wiebe)** (5.5), vẽ chồng lên chu trình lý tưởng.

---

## 9. Kiểm thử: bảng số chuẩn do Claude tự tính

Quy ước: R = 0,287; γ = 1,4; cv = 0,7175 kJ/(kg·K); đơn vị kPa, K, kJ/kg.

### 9.1 `tests/js/hoa_ly_nhiet_dong.test.mjs`

**Otto – ca A** (r = 8, T₁ = 300, p₁ = 100, q_in = 1000)

| Đại lượng | Giá trị chuẩn |
|---|---|
| T₂ | 689,22 |
| p₂ | 1837,92 |
| T₃ | 2082,95 |
| p₃ | 5554,53 |
| T₄ | 906,66 |
| p₄ | 302,22 |
| q_out | 435,28 |
| w | 564,72 |
| η | 0,5647 |
| MEP | 749,59 |
| η_Carnot | 0,8560 |
| s₃ − s₂ | 0,7935 kJ/(kg·K) |

**Otto – ca B** (r = 10, q_in = 1500, còn lại như ca A)

| Đại lượng | Giá trị chuẩn |
|---|---|
| T₂ | 753,57 |
| p₂ | 2511,89 |
| T₃ | 2844,16 |
| p₃ | 9480,53 |
| T₄ | 1132,28 |
| p₄ | 377,43 |
| w | 902,84 |
| η | 0,6019 |
| MEP | 1165,10 |

**Diesel** (r = 18, rc = 2, T₁ = 300, p₁ = 100)

| Đại lượng | Giá trị chuẩn |
|---|---|
| T₂ | 953,30 |
| p₂ | 5719,81 |
| T₃ | 1906,60 |
| T₄ | 791,70 |
| q_in | 957,59 |
| q_out | 352,80 |
| η | 0,6316 |

η tính từ q và η tính bằng công thức đóng phải trùng nhau.

**Nhiên liệu** (nguyên tử khối SGK, H₂O ở thể hơi, k = 0,55, λ = 1)

| Nhiên liệu | ΔcH° (kJ/mol) | LHV (MJ/kg) | AFR | q_in (kJ/kg) |
|---|---|---|---|---|
| isooctane | −5065,0 | 44,43 | 15,05 | 1522 |
| ethanol | −1234,7 | 26,84 | 8,95 | 1483 |
| methane | −802,3 | 50,14 | 17,16 | 1519 |
| hydrogen | −241,8 | 120,90 | 34,32 | 1883 |

**Kiểm tra tổng quát**
- Định luật I: ở mọi t, ΔU = A + Q của quá trình hiện tại (sai số 1e-6).
- Tổng ∮ trên một chu trình: W = Q_in − Q_out.
- **Wiebe** (P3): đặt φ0 = 359,5°, Δφ = 1° và cho van xả mở tức thời ở 540°; khi đó η phải lệch η Otto lý tưởng ≤ 1 %. Phần tính Wiebe cần V(φ), nên `nhiet-dong.js` được phép dùng `co-cau.js`.

Dung sai: ±0,05 % cho số tính bằng công thức; ±0,5 % cho số liên quan đến số liệu nhiên liệu.

### 9.2 `tests/js/hoa_ly_co_cau.test.mjs`

Thông số: B = 80, S = 80, L = 140 mm, r = 10.
- Vd = 402,124 cm³; Vc = 44,680 cm³.
- V(φ) theo cm³:

| φ | 0° | 30° | 60° | 90° | 120° | 180° |
|---|---|---|---|---|---|---|
| V | 44,680 | 78,835 | 167,094 | 275,077 | 368,156 | 446,804 |

- Tính tuần hoàn và đối xứng: V(φ + 360°) = V(φ) và V(360° − θ) = V(θ).
- V(90°) > Vc + Vd/2 = 245,74 (do thanh truyền nghiêng). Test phải ghi chú lý do này.
- Nghịch đảo ∘ thuận = đồng nhất với θ ∈ [0°, 180°], bước 1° (sai số 1e-9).
- V tăng đơn điệu trên (0°, 180°) và giảm đơn điệu trên (180°, 360°).
- Khối khí mỗi chu trình (r = 10, T₁ = 300, p₁ = 100 kPa): m = 0,5189 g.

### 9.3 `tests/js/hoa_ly_tien_do.test.mjs`

**Các điểm mốc của `tuTienDo`**
- t = 1 → trạng thái 1 tại φ = 180°.
- t = 2 → trạng thái 2 tại φ = 360°.
- t = 2,5 → φ = 360° và T = T₂ + 0,5·q_in/cv.
- t = 3 → trạng thái 3.
- t = 5 → trạng thái 1 tại φ = 540°.
- t = 6 → tương đương t = 0, `vong` + 1.

**`buocGoc`**
- Từ t = 1,9, cộng Δφ = +30° → t ∈ (3; 3,2), có sự kiện `danh_lua`.
- Từ t = 3,1, cộng Δφ = −30° → t ∈ (1; 2), có sự kiện `danh_lua_nguoc`.
- Quay 720° → `vong` + 1, có sự kiện `het_chu_trinh`.

**Chống nhảy nhầm trên đồ thị**
- Đang ở t = 5,5 (giữa kỳ xả), kéo chấm về phía V lớn → t phải nằm trong [5; 5,5), **không bao giờ** rơi vào [0; 1).

**`gocTuTheTich`**
- Ra φ nằm trong đúng nửa vòng hiện tại.
- Áp dụng đúng quy tắc khi pít-tông ở điểm chết (6.2 mục 2).

### 9.4 `tests/js/hoa_ly_dong_luc.test.mjs`

- **Không nhiên liệu** (q_in = 0), ma sát = 0, tải = 0, ω₀ lớn: sau 1 chu trình ω quay về giá trị ban đầu, sai số ±0,5 % (hệ bảo toàn).
- **Có nhiên liệu**, ma sát = 0, tải = 0: động năng tăng thêm m·w mỗi chu trình, sai số ±2 %. Với ca B và hình học mặc định là ≈ 468 J/chu trình.
- **Có ma sát và tải:** tốc độ hội tụ về một giá trị ổn định và không vượt 6000 vòng/phút.

### 9.5 `tests/test_hoa_ly.py`

1. `/hoa-hoc/hoa-ly` và `/hoa-hoc/hoa-ly/dong-co-nhiet`:
   - Chưa đăng nhập → chuyển sang `/dang-nhap?tiep=…`.
   - Đã đăng nhập (dùng fixture có sẵn) → trả về 200.
   - Mọi `/static/…js|css` trong HTML đều đã có `?v=`.
2. `danh-muc.json`:
   - Đúng schema; `id` không trùng.
   - Mục có `trang_thai: "co"` phải có `duong_dan` khớp một route thật.
   - Tên tạm **không** xuất hiện trong `trang.html`, `hoa-ly.html`, `index.html`.
3. `index.html` có `href="/hoa-hoc/hoa-ly"` cả ở banner lẫn menu.
4. Đặt `settings.che_do_app = True` (monkeypatch): HTML trả về dùng `/static/vendor/tailwind.3.4.16.js`, không còn `cdn.tailwindcss.com`, và **không có** URL bên ngoài nào ngoài Google Fonts.
5. `nhien-lieu.json` và `cau-hoi.json`:
   - Đủ trường bắt buộc.
   - Mỗi câu có đáp án, giải thích, dạng ∈ {I, II, III}, mỗi dạng 3 câu.
   - Câu có `tinh_bang` thì gọi `node -e` với `nhiet-dong.js` để tính lại và so khớp. Nếu máy không có node thì `skip`.
6. **Toàn bộ test cũ vẫn PASS:** `.venv/bin/python -m pytest -q` và `node --test tests/js/`.

### 9.6 Kiểm tra trình duyệt thật: `tools/kiem_tra_hoa_ly.py`

Lần trước, ảnh chụp mà Gemini báo "đã kiểm" thực ra trắng hoặc sai, nên **bắt buộc dùng assert số**, không chỉ chụp ảnh.

**Cách chạy**
- Dùng Playwright đã có trong `.venv` (tham khảo `desktop/kiem_tra/kiem_tra_app.py`).
- Máy chủ cục bộ `http://127.0.0.1:8000`, chế độ cục bộ (không cần đăng nhập).

**Hook cho test.** Trang mô phỏng phải cung cấp `window.HoaLy`:

```js
window.HoaLy = {
  trangThai(),   // {phi, t, vong, doan, ky, p_bar, V_cm3, T_K, rpm, cheDo, quanTinh, thamSo, soLanDanhLua}
  datTienDo(t), datGoc(phi), datThamSo(obj), choi(), dung(),
  toaDo: { tamBanhDa(), chotKhuyu(), matPiston(), diemPV() }   // toạ độ client {x,y} để Playwright kéo đúng chỗ
};
```

**Các kịch bản** (mỗi kịch bản PASS/FAIL kèm con số thật in ra):

1. `/hoa-hoc/hoa-ly` hiện đủ thẻ theo `danh-muc.json`. Bấm thẻ "co" → vào trang mô phỏng; `<title>` chứa `ten` lấy từ JSON.
2. Trạng thái đầu: t = 1, φ = 180°.
3. Kéo bánh đà theo cung tròn 90° chiều kim đồng hồ (≥ 20 bước `mouse.move`) → φ = 270° ± 3°; V = HLCoCau.V(φ) ± 0,5 %; p·V^γ = p₁·V₁^γ ± 1 %.
4. Quay tiếp qua ĐCT → `doan` = 3, `soLanDanhLua` + 1, p ≈ p₃·(V₂/V)^γ.
5. Kéo pít-tông tới V = Vc + 0,5·Vd → V đúng ± 0,5 %, φ nằm trong nửa vòng hiện tại.
6. `datTienDo(2)` rồi kéo chấm p–V đi lên dọc đoạn 2→3 → φ giữ 360°, `doan` = 2, T tăng tuyến tính theo f.
7. `datTienDo(5.5)`, kéo chấm về phía V lớn → `doan` ∈ {4, 5}, không bao giờ = 0.
8. Bật quán tính, vung bánh đà rồi thả → 1 s sau φ vẫn tăng. Tắt quán tính → đứng yên.
9. Đổi r = 8 → ô η hiện "56,5 %" và ô T₂ hiện "689 K".
10. Chế độ tối (`localStorage "giai-chuoi-giao-dien" = "toi"`, tải lại trang) → `getComputedStyle(document.body).backgroundColor` = `rgb(9, 9, 11)`; màu nét chu trình khác màu nền.
11. Màn hình 390×844, giả lập cảm ứng → `scrollWidth ≤ innerWidth`; kéo bằng pointer kiểu touch vẫn làm φ thay đổi.
12. (P2) Đề máy: vung mạnh rồi thả, chờ 3 s → rpm > 800 và tăng; tải = 50 N·m → máy chậm dần rồi dừng.
13. Trong toàn bộ quá trình, console không có lỗi.

**Ảnh chụp:** lưu vào `outputs/hoa-ly/` gồm: sáng, tối, di động, ngay sau đánh lửa, tab Nhiên liệu.
- **Tự mở xem từng ảnh** và mô tả vào HANDOFF.md.
- Ảnh trắng hoặc thiếu hình = FAIL, phải sửa.

---

## 10. Giai đoạn và tiêu chí nghiệm thu

Mỗi giai đoạn xong: chạy test → cập nhật HANDOFF.md → `git add -A && git commit`.

| GĐ | Việc | Xong khi |
|---|---|---|
| 1 | Khung **mục Hoá lý**: 2 route, `hoa-ly.html` + `trung-tam.js` + `danh-muc.json`, banner + link menu ở trang chủ, `trang.html` khung rỗng đúng chủ đề | Test 9.5 mục 1–4 PASS; trang chủ vẫn như cũ ngoài phần thêm |
| 2 | Lõi tính toán thuần: `nhiet-dong.js`, `co-cau.js`, `tien-do.js` (+ Diesel, nhiên liệu) | Test 9.1–9.3 PASS |
| 3 | Trang mô phỏng **P1** (vẽ SVG, 4 cách kéo, thanh thời gian, chạy/dừng, bảng thông số và số liệu, sáng/tối, di động) | Kịch bản 9.6 số 1–11 và 13 PASS kèm ảnh |
| 4 | **P2** (T–s, p–φ, công, log–log, bóng ma, nhiên liệu, quán tính + đề máy, hạt khí, xuất ảnh, link, tab Học + câu hỏi) | Test 9.4, 9.5 mục 5 và kịch bản 12 PASS |
| 5 | **P3** (Diesel, Wiebe) + rà lại di động/tối + thử trong **app máy tính** (`CHE_DO_APP=1`) | Toàn bộ test PASS; mở được trong app CHEMISTRY mà không cần mạng (trừ font) |

---

## 11. Bàn giao

- **HANDOFF.md:** ĐÃ XONG / ĐANG LÀM / VIỆC TIẾP THEO / LỖI CÒN TỒN / CÁCH CHẠY, kèm số test PASS và đường dẫn ảnh.
- **CLAUDE.md:** thêm một đoạn ngắn "## Hoá lý (/hoa-hoc/hoa-ly)" vào sơ đồ dự án, gồm: file, hook `window.HoaLy`, nguồn sự thật, tên nằm trong `danh-muc.json`, test.
- **DAN_Y_HOA_HOC.md:** đánh dấu dòng Hoá lý là đã làm.
- **Báo cáo cuối cho người dùng (ngắn):**
  - Đã làm gì, file nào.
  - Mở thử ở đâu: `http://127.0.0.1:8000/hoa-hoc/hoa-ly`.
  - **Cách đổi tên:** sửa `"ten"` của `dong-co-nhiet` trong `frontend/hoa-hoc/hoa-ly/danh-muc.json`.
  - Liệt kê **trung thực** những gì chưa kiểm được.

---

## 12. KHÔNG làm

- Không dùng 3D, Three.js hay Unity cho mô phỏng này. Không thêm thư viện, CDN hay asset ngoài.
- Không chép mã hoặc hình từ LearnChemE, UNLV hay video gốc. Chỉ học ý tưởng.
- Không viết cứng tên mô phỏng ở ngoài `danh-muc.json`.
- Không sửa kiến trúc trang chủ: chỉ thêm 1 banner và 1 link menu.
- Không sửa class CSS cũ. Không sửa `.env`. Không `git push` hay deploy.
- Không đụng vùng Phòng Lab Sinh học 3D. Không đụng file của việc nhân vật trợ lý.
- Không ghi "đã kiểm tra" khi chưa chạy `tools/kiem_tra_hoa_ly.py` và chưa tự mở xem ảnh.
