# PROMPT – Phòng thí nghiệm Hoá học ảo (bản Web 3D + bản Unity 3D do admin bật/tắt)

> Dán nguyên file này cho Antigravity, hoặc gõ: "Đọc và làm theo PROMPT_PHONG_THI_NGHIEM_AO.md".
> Bối cảnh tổng: DAN_Y_HOA_HOC.md (phân hệ 14, giờ làm TRƯỚC các phần khác).

---

## 0. Việc phải làm trước khi viết code
1. Đọc **AGENTS.md** (luật chung), **CLAUDE.md** (sơ đồ dự án) và phần đầu **HANDOFF.md**.
2. Chạy `.venv/bin/python -m pytest -q` để ghi lại kết quả ban đầu (số test PASS). Cuối việc, toàn bộ test cũ vẫn phải PASS.
3. Tạo điểm phục hồi: `git add -A && git commit -m "chore: diem phuc hoi truoc Phong thi nghiem ao"`.
4. Thư mục dự án nằm trong iCloud nên đọc file khá chậm. **Không** `grep -r` toàn repo (`outputs/` có hơn 1.000 thư mục).
   Hãy grep đúng file, ví dụ `grep -n "cai-dat-chung" backend/quan_tri_web.py frontend/admin.js`, rồi đọc theo khoảng dòng bằng `sed -n`.
5. **Không sửa** vùng Phòng Lab Sinh học 3D (`frontend/biology-lab-3d*.html`, `frontend/sinh-hoc/`, `backend/sinh_hoc_ai.py`), vì vùng này chỉ Claude được ghi.
   Được phép ĐỌC `frontend/biology-lab-3d.html` để học cách nạp three@0.160.0 + gsap qua CDN và cách dừng render khi ẩn tab.
6. Không sửa `.env`. Không `git push`, không chạy `dua-len-web.command` khi người dùng chưa đồng ý.

## 1. Mục tiêu
Xây trang **`/hoa-hoc/phong-thi-nghiem`** gồm 5 thí nghiệm tương tác, chuẩn CT Hoá học GDPT 2018:

| id | Thí nghiệm | Lớp |
|---|---|---|
| `an-toan` | An toàn phòng thí nghiệm (bài nhập môn) | 10 |
| `chuan-do` | Chuẩn độ acid – base | 11 |
| `nhan-biet-ion` | Nhận biết ion trong dung dịch + thử màu ngọn lửa | 11–12 |
| `pin-galvani` | Pin Galvani (pin điện hoá) | 12 |
| `dien-phan` | Điện phân dung dịch | 12 |

Trang có **hai chế độ hiển thị**, dùng chung một lõi mô phỏng:
- **Bản Web 3D** (mặc định, ai cũng dùng được, chạy cả trên điện thoại): Three.js trong trình duyệt.
- **Bản Unity 3D** (chân thực hơn, nặng hơn): Unity WebGL. Chỉ hiện nút chuyển sang bản này khi **admin bật** công tắc cho mọi người.

## 2. Kiến trúc (BẮT BUỘC theo đúng hướng này)
```
frontend/hoa-hoc/phong-thi-nghiem/
  thi-nghiem/<id>.json   ← dữ liệu 5 thí nghiệm: dụng cụ, hoá chất, các bước, hiện tượng, phương trình, câu hỏi
  bang-ion.json          ← bảng ion × thuốc thử → hiện tượng (dùng cho nhan-biet-ion)
  tinh-toan.js           ← HÀM THUẦN, không DOM: pH, Faraday, E°pin… (UMD: chạy được cả trong trình duyệt lẫn Node)
  mo-phong.js            ← lõi trạng thái từng thí nghiệm (máy trạng thái), phát sự kiện "trang_thai"
  ve-web.js              ← renderer Three.js (bản Web 3D)
  cau-unity.js           ← cầu nối sang Unity (bản Unity 3D)
  README.md              ← cách sửa nội dung, thêm thí nghiệm, build Unity
frontend/phong-thi-nghiem.html  ← trang chính (khung giao diện + bảng điều khiển)
unity/PhongThiNghiemHoaHoc/     ← mã nguồn dự án Unity
frontend/unity-lab/Build/       ← nơi chứa bản build WebGL (có thể chưa có)
```
- **Lõi JS là nguồn sự thật duy nhất.** Mọi phép tính hoá học nằm trong `tinh-toan.js` và `mo-phong.js`.
  Hai renderer chỉ (a) vẽ theo trạng thái nhận được, (b) gửi thao tác của người dùng về lõi (ví dụ: mở khoá burette, nhỏ thuốc thử, đổi I).
  Unity **không** tự tính lại phần hoá học, để tránh hai nơi cho ra hai kết quả khác nhau.
- Bảng điều khiển, số liệu, sổ tay thí nghiệm và câu hỏi là HTML chung cho cả hai chế độ. Chỉ khung vẽ ở giữa được đổi qua lại.

## 3. Đồ hoạ bản Web 3D – "không thô, hợp giao diện web"
- three@0.160.0 (cùng phiên bản Lab Sinh), nạp qua importmap CDN; gsap cho chuyển động. Không tải model nặng: dựng **procedural**
  (`LatheGeometry` cho cốc, bình tam giác, burette, ống nghiệm; `TubeGeometry` cho dây dẫn, cầu muối).
- Renderer: `antialias`, `SRGBColorSpace`, `ACESFilmicToneMapping`, môi trường phản chiếu `PMREMGenerator` + `RoomEnvironment`,
  bóng mềm `PCFSoftShadowMap`, `setPixelRatio(min(devicePixelRatio, 2))` (điện thoại ≤ 1.5).
- Thuỷ tinh: `MeshPhysicalMaterial` (`transmission: 1`, `roughness ≈ 0.05`, `ior 1.5`, `thickness`).
  Dung dịch là mesh riêng có mặt khum (meniscus), đổi màu mượt bằng gsap theo pH, nồng độ hoặc phản ứng.
- Hiệu ứng: kết tủa (hạt `InstancedMesh` lắng dần), bọt khí (instanced, nổi lên rồi vỡ), lớp kim loại bám điện cực,
  ngọn lửa đèn cồn (sprite cộng sáng hoặc shader nhỏ) đổi màu khi thử ngọn lửa, dòng electron chạy trên dây (chấm sáng).
- **Màu theo giao diện web:** đọc biến CSS ở `:root` / `[data-theme="toi"]` bằng `getComputedStyle`, không gán cứng.
  Sáng: nền Icy blue `#eef4fa`, điểm nhấn Midnight Navy `#0f2b48`. Tối: nền `#0f2438`, điểm nhấn Sky Blue `#38bdf8`.
  Đổi sáng/tối thì cảnh 3D đổi theo ngay, không cần tải lại trang.
- Bàn thí nghiệm tối giản, mặt mờ, bo góc mềm. Nhãn dụng cụ hiển thị bằng `CSS2DRenderer` với font của web.
- Camera: `OrbitControls` có giới hạn góc và khoảng cách, mỗi thí nghiệm có sẵn góc nhìn đẹp, chuyển góc mượt.
- Hiệu năng: ≥ 50 fps trên máy tính, ≥ 30 fps trên điện thoại. **Dừng render khi tab ẩn hoặc canvas ra khỏi màn hình**.
  Có lựa chọn Chất lượng **Cao / Thấp** (tự chọn theo thiết bị; Thấp = tắt bóng, giảm số hạt).
- Mọi thao tác kéo/xoay đều phải có **nút bấm thay thế** (bàn phím và màn hình cảm ứng đều dùng được). Số liệu luôn hiện bằng chữ trong bảng.

## 4. Nội dung 5 thí nghiệm
Mỗi thí nghiệm có hai chế độ: **Hướng dẫn** (từng bước, có gợi ý) và **Tự do**. Có **Sổ tay thí nghiệm** tự ghi lại thao tác và hiện tượng,
và **câu hỏi cuối bài** theo 3 dạng thi TN THPT: 4 lựa chọn (3–5 câu), Đúng/Sai 4 ý (1–2 câu), Trả lời ngắn (1–2 câu).
UI dùng tên gọi IUPAC theo CT 2018 (ethanoic acid / acetic acid, sodium hydroxide…), dấu phẩy thập phân ("0,64 g").
Thể tích khí đo ở đkc 25 °C, 1 bar: **24,79 L/mol**.

### 4.1 `an-toan` – An toàn phòng thí nghiệm
- Cảnh "tìm lỗi": một bàn thí nghiệm có 8 lỗi để bấm phát hiện. Ví dụ: không đeo kính bảo hộ; lọ hoá chất không nhãn;
  đổ nước vào H₂SO₄ đặc; ngửi trực tiếp miệng ống nghiệm; hướng miệng ống nghiệm đang đun về phía người;
  ăn uống trong phòng thí nghiệm; dùng tay cầm hoá chất; để đèn cồn cháy không người trông.
- Thẻ 9 biểu tượng cảnh báo **GHS**: tự vẽ bằng SVG, không chép ảnh có bản quyền. Có trò ghép biểu tượng ↔ ý nghĩa.
- Xử lí sự cố: bỏng acid hoặc base, đổ vỡ thuỷ ngân, cháy cồn. Bài này gắn huy hiệu "Nhập môn" và khuyến khích làm trước, nhưng không khoá các bài khác.

### 4.2 `chuan-do` – Chuẩn độ acid – base
- Burette chứa NaOH 0,100 M; bình tam giác chứa 20,00 mL acid; có hai bài: **HCl** (mạnh – mạnh) và **CH₃COOH** (yếu – mạnh, Ka = 1,75·10⁻⁵).
- Thao tác: mở khoá chảy nhanh / chảy từng giọt / đóng; lắc bình; chọn chất chỉ thị:
  phenolphthalein (8,2–10: không màu → hồng), methyl orange (3,1–4,4: đỏ → vàng).
  Màu dung dịch tính theo pH thật và khoảng đổi màu của chỉ thị.
- Đồ thị pH – V(NaOH) vẽ trực tiếp (SVG/Canvas, cùng phong cách web), đánh dấu điểm tương đương.
  Phải đọc vạch burette trên mặt khum (có phóng to).
- pH tính bằng **cân bằng điện tích giải số** (chia đôi trên [H⁺]), không dùng công thức gần đúng từng đoạn.
- Chế độ **Ôn thi**: nồng độ acid ẩn ngẫu nhiên, học sinh chuẩn độ rồi nhập C_M, hệ thống chấm sai số.

### 4.3 `nhan-biet-ion` – Nhận biết ion
- Giá có 4–6 ống nghiệm không nhãn (ngẫu nhiên). Lọ thuốc thử gồm NaOH, NH₃, AgNO₃, BaCl₂, HCl, H₂SO₄, KSCN và giấy quỳ.
  Nhỏ từng giọt hoặc nhỏ dư (dư có thể làm tan kết tủa).
- `bang-ion.json` tối thiểu có:
  - Cl⁻ → AgCl trắng; Br⁻ → AgBr vàng nhạt; I⁻ → AgI vàng; SO₄²⁻ → BaSO₄ trắng; CO₃²⁻ + HCl → khí CO₂.
  - Fe³⁺ → Fe(OH)₃ nâu đỏ, với SCN⁻ → đỏ máu; Fe²⁺ → Fe(OH)₂ trắng xanh, để ngoài không khí hoá nâu đỏ.
  - Cu²⁺ → Cu(OH)₂ xanh lam, tan trong NH₃ dư thành xanh lam thẫm.
  - Al³⁺ → Al(OH)₃ keo trắng, tan trong NaOH dư; Mg²⁺ → Mg(OH)₂ trắng; NH₄⁺ + NaOH, đun → NH₃ làm quỳ ẩm hoá xanh.
  - Mỗi dòng có phương trình ion thu gọn.
- Thử màu ngọn lửa: Na (vàng), K (tím nhạt), Li (đỏ tía), Ca (đỏ cam), Ba (lục vàng), Cu (lục lam).
- Chế độ trò chơi: xác định các ống với ít lượt thử nhất, có tính điểm.

### 4.4 `pin-galvani` – Pin Galvani
- Hai cốc điện cực, cầu muối KNO₃, vôn kế. Có thể chọn cặp kim loại trong: Mg (−2,37), Zn (−0,763), Fe (−0,44), Ni (−0,257),
  Sn (−0,137), Pb (−0,126), Cu (+0,340), Ag (+0,799) (V, so với H₂).
- Vôn kế hiện E°pin = E°(cathode) − E°(anode). Có thanh trượt nồng độ, tính theo Nernst E = E° − (0,0592/n)·lg Q (ghi rõ là phần mở rộng).
- Hoạt hình: electron đi trên dây từ anode sang cathode, ion di chuyển trong cầu muối; anode mòn dần, cathode dày lên.
  Rút cầu muối thì vôn kế về 0 V, kèm câu hỏi "vì sao?".

### 4.5 `dien-phan` – Điện phân dung dịch
- Bài: CuSO₄ điện cực trơ (graphite); NaCl có màng ngăn (nhỏ phenolphthalein ở cathode, quỳ ẩm ở anode);
  CuSO₄ với anode Cu (anode tan – tinh luyện đồng).
- Điều khiển I (A) và t (s), tua nhanh thời gian. Tính n(e) = I·t / F với F = 96 485 C/mol,
  ra khối lượng kim loại bám, thể tích khí (đkc) và biến thiên pH.
- Hiện thứ tự điện phân ở từng điện cực (Cu²⁺ trước H₂O ở cathode; Cl⁻ trước H₂O ở anode; SO₄²⁻, NO₃⁻ không bị điện phân).
  Màu xanh của Cu²⁺ nhạt dần theo nồng độ thật.

## 5. Bản Unity 3D + công tắc admin
### 5.1 Công tắc (backend)
- Lưu trong `app_settings` khoá `"phong_thi_nghiem"` = `{"unity_bat": false}` qua `db.cai_dat()` / `db.dat_cai_dat()`. Mặc định TẮT.
- Làm theo đúng mẫu công tắc admin sẵn có `"hoat_anh_cai_dat"` / route `cai-dat-chung` trong `backend/quan_tri_web.py` + `frontend/admin.js`:
  - `GET  /admin/api/phong-thi-nghiem` → `{unity_bat, unity_san_sang, dung_luong_build}` (chỉ admin: `can_admin`).
  - `POST /admin/api/phong-thi-nghiem` `{unity_bat: bool}` (chỉ admin + CSRF). Ghi nhật kí hoạt động nếu `nhat_ky` có sự kiện phù hợp.
  - `GET  /api/phong-thi-nghiem/cau-hinh` (người đã đăng nhập) → `{unity_bat, unity_san_sang, la_admin}`.
- `unity_san_sang` = có đủ file loader, data, framework, wasm trong `frontend/unity-lab/Build/`.
  Đường dẫn đặt thành biến module để test có thể monkeypatch.

### 5.2 Giao diện admin
- Thêm một thẻ **"Phòng thí nghiệm"** vào lưới `/admin` (`.the-nho`, có `tomTat()` hiện "Unity: Bật/Tắt · Build: có/chưa").
- Khối mở ra có công tắc **"Cho mọi người dùng bản 3D Unity"**, trạng thái build, dung lượng, và đường dẫn `unity/README.md` khi chưa có build.
  Khi chưa có build: công tắc vẫn bật được nhưng có cảnh báo "Người dùng sẽ chưa thấy nút vì chưa có bản build".

### 5.3 Nút trên trang Phòng thí nghiệm
- Nút chuyển **"Web 3D ⇄ Unity 3D"** ở thanh công cụ của khung vẽ:
  - Người dùng thường chỉ thấy khi `unity_bat && unity_san_sang`.
  - Admin luôn thấy khi `unity_san_sang`; nếu đang tắt cho mọi người thì nút có nhãn "(chỉ admin – đang tắt)".
- Trên điện thoại: hiện cảnh báo "Bản Unity nặng, nên dùng máy tính" trước khi tải. Mặc định luôn mở bản Web 3D.
- Chuyển qua lại không mất tiến trình: lõi `mo-phong.js` giữ trạng thái, renderer mới nhận lại trạng thái hiện tại.
  Tải Unity có thanh tiến trình theo phong cách web. Tải lỗi thì quay về Web 3D kèm thông báo.

### 5.4 Dự án Unity (`unity/PhongThiNghiemHoaHoc/`)
- Unity 6 LTS, URP, nền tảng WebGL. Viết toàn bộ C# + `README.md` hướng dẫn build.
- Cảnh chỉ có một GameObject `LabBridge`. Mọi dụng cụ được **dựng bằng code lúc chạy** từ dữ liệu JSON gửi sang,
  nên có thể build bằng dòng lệnh mà không cần chỉnh cảnh bằng tay.
- Cầu nối:
  - JS → Unity: `unityInstance.SendMessage("LabBridge", "NhanLenh", json)`; lệnh gồm `mo_thi_nghiem`, `trang_thai`, `doi_giao_dien` (màu sáng/tối).
  - Unity → JS: `Assets/Plugins/WebGL/LabBridge.jslib` gọi `window.PhongLabUnity.nhan(json)`; sự kiện gồm `san_sang`, `thao_tac`, `loi`.
- Chất lượng hình: vật liệu thuỷ tinh URP trong suốt có khúc xạ giả, dung dịch đổi màu theo trạng thái, hạt kết tủa và bọt khí (VFX/Particle),
  ánh sáng mềm, bảng màu bám theo web như mục 3.
- `Assets/Editor/BuildWebGL.cs`: hàm `BuildWebGL.Build()` xuất ra `../../frontend/unity-lab/Build/`.
  Thiết lập: Compression **Gzip** + **Decompression Fallback = ON** (để máy chủ không cần header đặc biệt), Strip Engine Code,
  Code Optimization = Size. Mục tiêu dung lượng build **≤ 25 MB**.
- Build bằng dòng lệnh nếu máy đã cài Unity:
  `"/Applications/Unity/Hub/Editor/<phiên bản>/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath unity/PhongThiNghiemHoaHoc -executeMethod BuildWebGL.Build -logFile logs/unity-build.log`
  Kiểm tra trước bằng `ls /Applications/Unity/Hub/Editor`. **Nếu chưa cài Unity hoặc thiếu WebGL Build Support:** KHÔNG tự cài.
  Vẫn hoàn thành mọi phần còn lại (công tắc báo "chưa có build"), rồi báo người dùng các bước cài Unity Hub → Unity 6 LTS + WebGL Build Support.

## 6. Route, điều hướng, giao diện chung
- `GET /hoa-hoc/phong-thi-nghiem` → `_trang_can_dang_nhap(request, "phong-thi-nghiem.html")`; hỗ trợ `?tn=<id>` để mở thẳng một thí nghiệm.
  Dùng `_trang()` để JS/CSS được gắn `?v=` (đường dẫn `/static/...` đúng định dạng regex trong `_trang`).
- Đường vào trang (rút kinh nghiệm lỗi "không thấy Sinh học 3D trên điện thoại" ngày 22/10): phải có ở **cả 4 chỗ**:
  nav desktop `.nav-awesomic`, nút pill trên header di động (`lg:hidden`), banner trên trang chủ (cạnh banner Lab Sinh học)
  và liên kết trong sidebar `aside.cot-ben`.
- Bố cục: desktop = danh sách 5 thí nghiệm (trái) · khung 3D (giữa) · bảng điều khiển + sổ tay (phải).
  Điện thoại = khung 3D ở trên, bảng điều khiển dạng bottom sheet kéo lên.
- CSS mới chỉ THÊM vào cuối `frontend/style.css` (class tiền tố `ptn-`), dùng biến màu sẵn có, chạy đúng cả sáng lẫn tối.
- Hiển thị chữ từ JSON bằng `textContent` (không `innerHTML` với dữ liệu), để chống chèn mã.

## 7. Test (bắt buộc)
`tests/test_phong_thi_nghiem.py`:
- **Dữ liệu:** 5 file thí nghiệm đủ trường (`id, ten, lop, muc_tieu, dung_cu, hoa_chat, buoc, an_toan, cau_hoi`); câu hỏi 3 dạng hợp lệ
  (4 lựa chọn khác nhau, đáp án A–D; Đúng/Sai đủ 4 ý; Trả lời ngắn có đáp số); `bang-ion.json` có đủ các ion ở mục 4.3.
- **Trang:** chưa đăng nhập vào `/hoa-hoc/phong-thi-nghiem` → 303 sang `/dang-nhap`; đã đăng nhập → 200.
- **Công tắc:**
  - Mặc định `unity_bat = false`.
  - Học sinh gọi API admin → 403; chưa đăng nhập → 401; thiếu CSRF → 403.
  - Admin bật xong thì `/api/phong-thi-nghiem/cau-hinh` của học sinh thấy `unity_bat = true`.
  - `unity_san_sang` đúng theo việc thư mục build có hay không (dùng `tmp_path` + monkeypatch).
- **Tính toán:** nếu `shutil.which("node")` có thì chạy `node --test tests/js/` qua subprocess, không có thì `skip`.
  Các giá trị phải khớp (sai số ±0,02):

  | Trường hợp | Kết quả |
  |---|---|
  | HCl 0,1 M 20 mL + NaOH 0,1 M, V = 0 / 10 / 19,9 / 20 / 20,1 / 30 mL | pH 1,00 / 1,48 / 3,60 / 7,00 / 10,40 / 12,30 |
  | CH₃COOH 0,1 M 20 mL, V = 0 / 10 / 20 mL | pH 2,88 / 4,76 / 8,73 |
  | CuSO₄, I = 2 A, t = 965 s | m(Cu) = 0,64 g; V(O₂) = 0,124 L (đkc) |
  | Pin Zn – Cu | E°pin = 1,103 V |

- Dùng fixture sẵn có trong `tests/conftest.py` (`client`, `dang_nhap`, `db.dat_admin`), giống `tests/test_lo_trinh.py`.
- Cuối cùng chạy lại toàn bộ `.venv/bin/python -m pytest -q`: tất cả PASS.

## 8. Kiểm tra bằng trình duyệt thật trước khi báo xong
- Desktop 1440×900 và điện thoại 390×844, cả chế độ Sáng lẫn Tối: chụp ảnh từng thí nghiệm. Console không có lỗi.
- Làm trọn một lượt mỗi thí nghiệm:
  - Chuẩn độ HCl đến khi chuyển màu hồng: V ≈ 20,0 mL.
  - Nhận biết đúng 4 ống.
  - Pin Zn – Cu hiện 1,10 V; rút cầu muối về 0 V.
  - Điện phân CuSO₄ 2 A trong 965 s ra 0,64 g.
- Admin: bật công tắc → tài khoản học sinh thấy nút Unity (nếu đã có build); tắt → nút mất. Chưa có build → không ai thấy nút, trang admin báo rõ.
- Đo fps bằng Performance của DevTools và ghi vào HANDOFF.md.

## 9. Bàn giao
- Cập nhật **HANDOFF.md** (ĐÃ XONG / ĐANG LÀM / VIỆC TIẾP THEO / LỖI CÒN TỒN / CÁCH CHẠY).
  Thêm một đoạn ngắn "## Phòng thí nghiệm Hoá ảo" vào sơ đồ trong **CLAUDE.md**: file, route, khoá app_settings, test.
- Commit theo từng phần lớn (lõi + dữ liệu → Web 3D → admin + Unity → test). Commit cuối có thông điệp rõ ràng.
- Báo cáo ngắn: đã làm gì, file nào, cách chạy thử, ảnh chụp, Unity đã build được chưa (chưa thì người dùng cần cài gì).

## 10. Điều kiện "XONG"
- [ ] 5 thí nghiệm chạy trọn ở bản Web 3D, đẹp và đúng màu web ở cả sáng/tối, desktop lẫn điện thoại.
- [ ] Kết quả tính toán khớp bảng ở mục 7.
- [ ] Công tắc admin hoạt động: lưu bền (có trong `app.db`, nên cũng được sao lưu), áp dụng cho mọi người, không lộ cho học sinh khi tắt.
- [ ] Dự án Unity có đủ mã nguồn + script build + README; nếu máy có Unity thì đã có bản build ≤ 25 MB chạy được trong trang.
- [ ] Trang vào được từ 4 chỗ (nav, pill di động, banner trang chủ, sidebar).
- [ ] Toàn bộ test PASS; HANDOFF.md và CLAUDE.md đã cập nhật; đã commit; CHƯA push hoặc deploy.
