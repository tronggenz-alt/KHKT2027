# PROMPT – ĐƯA BỘ ĐIỀU KHIỂN CỬ CHỈ TAY (BẢN PYTHON) VÀO PHÒNG SINH HỌC: XOAY, LƯỚT, PHÓNG TO MÔ HÌNH NGƯỜI

> Dán nguyên file này cho Antigravity (Gemini). Làm liên tục đến hết Giai đoạn 7, tự chủ theo AGENTS.md. Giai đoạn 8 là tuỳ chọn.
> Viết bởi Claude ngày 26/10/2026. Bản này **thay hoàn toàn** bản ngày 25/10 cùng tên (bản đó chưa ai chạy).
> **Luật đã đổi ngày 26/10/2026:** người dùng cho Gemini sửa trực tiếp trang Sinh học (xem `AGENTS.md`, `.agents/rules/phong_lab_3d_phoi_hop.md`).
> Vì vậy Gemini làm **trọn gói**, kể cả gắn bộ điều khiển vào `/sinh-hoc` và `/sinh-hoc-v2`. Không cần viết đề xuất chờ Claude.
> Trước khi viết, Claude đã:
> - viết và chạy thật bản Python trên máy người dùng: camera FaceTime HD, nhận cả hai tay, chụm/nhả đúng (`tools/tay_python/`);
> - kiểm bản Python bằng 15 tình huống tay giả, rồi sinh **dữ liệu vàng** 9 kịch bản / 514 khung (`tests/js/du_lieu/dieu_khien_tay_vang.json`);
> - đọc trang Sinh học 3D: camera, OrbitControls, vòng lặp, chọn bộ phận bằng tia, tab Hoá học, `window.BioLab`;
> - kiểm tra máy: máy chủ 127.0.0.1:8000 chạy, có Chrome và Safari, app CHEMISTRY đã có `NSCameraUsageDescription`, ổ còn khoảng 19 GB.

---

## 0. Trước khi làm (bắt buộc)

1. Đọc `AGENTS.md`, `CLAUDE.md` (mục "## Sinh học 3D"), phần cuối `HANDOFF.md`, `.agents/rules/phong_lab_3d_phoi_hop.md`, `tools/tay_python/README.md`.
2. **Đọc kỹ toàn bộ `tools/tay_python/xoay_3d.py` và `tools/tay_python/xem_tay.py`.** Đó là bản mẫu, là đặc tả chính xác nhất của việc phải làm.
3. **Sửa trang Sinh học theo quy tắc phối hợp** (`.agents/rules/phong_lab_3d_phoi_hop.md`):
   - Trước khi sửa `frontend/biology-lab-3d.html` / `-v2.html`: `git status` + `git log -3 -- <file>`. Nếu file đang có thay đổi **chưa commit** không phải của mình → dừng, báo người dùng, không sửa đè.
   - Đọc theo khoảng dòng, sửa đúng chỗ. Không định dạng lại cả file, không đổi tên hàm/biến đang dùng, chỉ **thêm** vào `window.BioLab`.
   - Việc chia hai phần: (a) bộ điều khiển dùng chung nằm ở `frontend/dieu-khien-tay/` (GĐ1–GĐ6); (b) gắn vào hai trang Sinh học (GĐ7).
4. Chạy `git status`.
   - Nếu còn thay đổi chưa commit của việc khác: **không** revert, **không** sửa đè.
   - Tạo điểm phục hồi: `git add -A && git commit -m "chore: diem phuc hoi truoc dieu khien tay sinh hoc"`. Lần commit này gồm luôn:
     - `tools/tay_python/`, `tests/js/du_lieu/dieu_khien_tay_vang.json` và file prompt này;
     - các file đổi luật ngày 26/10: `AGENTS.md`, `CLAUDE.md`, `.agents/rules/phong_lab_3d_phoi_hop.md`, `.agents/hang-doi/sinh-hoc/README.md`, `.githooks/commit-msg`.
5. Không sửa `.env`. Không `git push`. Không deploy.
6. **Không sửa `tools/tay_python/*.py` và không sửa `tests/js/du_lieu/dieu_khien_tay_vang.json`.** Test JS không khớp thì sửa code JS.
7. Máy chủ web: `http://127.0.0.1:8000`, chế độ cục bộ, không cần đăng nhập. Nếu đang tắt thì **bấm đúp `bat-tat.command` đúng MỘT lần**, hoặc tự chạy uvicorn ở cổng khác để kiểm thử.
8. Máy làm việc: Apple M2, RAM 8 GB, ổ còn khoảng 19 GB, thư mục dự án nằm trong iCloud.
   - Không `grep -r` toàn repo; chỉ grep theo từng file.
   - Mọi thứ tải về tạm thì để ở `~/AppBuild/`.
   - Playwright dùng `channel="chrome"` như `tools/kiem_tra_hoa_ly.py`, không tải trình duyệt mới.

---

## 1. Dàn ý (yêu cầu của người dùng)

| # | Người dùng nói | Cách hiểu để làm |
|---|---|---|
| 1 | "Tích hợp phần điều khiển bằng cử chỉ tay của Python này vào để điều khiển xoay, thu phóng… mô hình người trong phòng Sinh học" | **Chuyển 1:1** phần điều khiển của bản Python (`doc_tay`, `LocOneEuro`, `Tay`, `DieuKhien`, `ti_le_chum`, `la_xoe`) sang JavaScript chạy **ngay trong trang** Sinh học. Áp kết quả lên camera OrbitControls của mô hình giải phẫu thật (BodyParts3D). Cảm giác điều khiển phải **giống hệt bản Python**. Điều này được kiểm bằng dữ liệu vàng (mục 2): sai số ≤ 1e-6 ở mọi khung. |
| 2 | (vì sao không chạy chính file Python trong app) | Người xem web trên Render không có Python, và máy chủ không có camera của họ. App máy tính vốn là Chromium, chạy được MediaPipe bản web. Bản web dùng **cùng file mô hình** `hand_landmarker.task` với bản Python, nên kết quả nhận diện như nhau, lại bớt được một chặng cầu nối. Bản Python vẫn giữ vai trò **bản mẫu và nguồn sự thật**. Ai muốn dùng đúng Python để điều khiển trang thì có Giai đoạn 8 (tuỳ chọn): Python gửi điểm tay sang trang qua WebSocket. |
| 3 | Các cử chỉ (y như bản Python) | • **Chụm ngón cái + trỏ rồi kéo**: xoay (ngang = quanh trục dọc, dọc = nghiêng).<br>• **Kéo nhanh rồi thả**: quay tiếp và chậm dần.<br>• **Chụm lại lúc đang quay**: dừng ngay.<br>• **Hai tay chụm rồi kéo giãn / kéo lại gần**: phóng to / thu nhỏ.<br>• **Xoè hai bàn tay giữ 1 giây**: về góc nhìn ban đầu. |
| 4 | "…" (thêm cho hợp phòng Sinh học) | • **Chạm nhẹ** (chụm rồi thả trong 0,3 s, không kéo) = chọn cơ quan dưới con trỏ, giống bấm chuột.<br>• Con trỏ vòng tròn theo đầu ngón, đầy dần khi chụm.<br>• Khung camera nhỏ có khung xương tay.<br>• Hướng dẫn cử chỉ bằng tiếng Việt có dấu. |
| 5 | "Chỉnh lại không kết nối với camera bên điện thoại" | **Không bao giờ mở camera iPhone/iPad** (Camera Liên tục). Ưu tiên camera FaceTime HD của Mac. Máy người dùng có cả "Camera FaceTime HD" và "Camera idc🎶" (iPhone15,2). |
| 6 | "Chỉ điều khiển cho sinh học thôi" | Chỉ `/sinh-hoc` và `/sinh-hoc-v2` có nút bật. Không gắn vào trang chủ, phòng thí nghiệm Hoá, Hoá lý hay lab Unity. Sang tab Hoá học thì camera tắt. |

---

## 2. Bản mẫu Python và dữ liệu vàng

**Bản mẫu** `tools/tay_python/` (đọc README ở đó):
- `xem_tay.py`: `ti_le_chum`, ngưỡng `CHAM_BAT = 0.25` / `CHAM_NHA = 0.40`, chọn camera bỏ qua iPhone, lật gương **trước** khi nhận diện, thu hình về rộng 640 px.
- `xoay_3d.py`: `LocOneEuro`, `Tay`, `la_xoe`, `doc_tay`, `goc_ngan`, `DieuKhien` cùng mọi hằng số (`DO_NHAY`, `NGUONG_TRUOT`, `QT_*`, `MAT_TAY_S`, `XOE_GIU_S`, `GOC_DAU`, `ZOOM_*`, `PITCH_MAX`, `VUNG`).

**Dữ liệu vàng** `tests/js/du_lieu/dieu_khien_tay_vang.json` (623 KB), sinh từ `tools/tay_python/tao_du_lieu_vang.py`:

```
{ "hang_so": {...các hằng số của bản Python...},
  "kich_ban": { "<tên>": {
      "vao": [ { "t", "dt", "tiLe", "tay": [ { "ben": "Left"|"Right", "diem": [[x,y,z]×21], "theGioi": [[x,y,z]×21] } ] } ],
      "ra":  [ { "t", "tay": [ { "ben", "conTro": [x,y], "cham", "tl", "xoe" } ],
                 "yaw", "pitch", "zoom", "cheDo", "wYaw", "wPitch", "veDau" } ] } } }
```

9 kịch bản: `keo_luot`, `bat_lai`, `keo_cham_tha`, `zoom_hai_tay`, `mat_tay`, `xoe_dat_lai`, `keo_doc`, `rung_tay`, `doi_tay`.

`diem` là toạ độ trên **khung đã lật gương** (giống Python: lật rồi mới nhận diện). `tiLe` = rộng/cao của khung camera.

---

## 3. Hiện trạng trang Sinh học (commit 8af5c157, ngày 26/10/2026; số dòng là "khoảng")

**`frontend/biology-lab-3d.html`** (một file, three@0.160.0 qua importmap CDN; app máy tính thay bằng `/static/vendor/three/...`):

| Dòng | Nội dung |
|---|---|
| 2842–2858 | `camera` (fov 40), `CAMERA_GOC`, `controls = new OrbitControls(...)`. `enableDamping`, `minDistance 0.45`, `maxDistance 10`, `maxPolarAngle 0.94π`. Sự kiện `"start"` dừng camera "giải thích từng bước". |
| 4440 | `chonGocNhin(ten)`. Không có tên hợp lệ thì bay về `CAMERA_GOC` bằng `bayToi`. |
| 4710–4762 | Chọn bộ phận bằng tia: `tiaChieu`, `timBoPhan(e)` nhận `{clientX, clientY}`. `pointerup` (dịch ≤ 5 px) gọi `chonCauTruc` / `chonBoPhan` + `xoaNhan` + `themNhan`. `pointermove` dò tia mỗi 80 ms để hiện chú thích `#goi-y-chuot`. |
| 5145 | `controls.addEventListener("end", ...)` cập nhật nhãn thông minh. |
| 5709–5736 | `vongLap()`, có `dt`. **`controls.update()` ở khoảng dòng 5718.** |
| 5737–5761 | `pauseRendering` / `resumeRendering`, `visibilitychange`. |
| ~6725 | `chonTab(hoa)`. Sang tab Hoá học thì gọi `pauseRendering()`. |
| 7059–7069 | `window.BioLab` (API + `trangThai` cho test). |

**`frontend/biology-lab-3d-v2.html`**: `controls` ở dòng 1270, chọn bằng tia ở 3592–3640, `window.BioLab` ở 4770.

**Khác**
- `backend/main.py` mount `/static` = `frontend/`; `.wasm` được trả đúng `application/wasm`.
- App `/Applications/CHEMISTRY.app` đã có `NSCameraUsageDescription` (chuỗi mặc định của Electron). `desktop/main.js` chưa có `setPermissionRequestHandler`.

---

## 4. Các file sẽ tạo

```
frontend/biology-lab-3d.html        ← SỬA (GĐ7): nút, nạp lười, nối vào vòng lặp / chọn bộ phận / tab Hoá học, API test
frontend/biology-lab-3d-v2.html     ← SỬA (GĐ7): như trên
frontend/dieu-khien-tay/            ← MỚI. Chỉ trang Sinh học nạp.
  cu-chi.js        UMD, hàm thuần: bản chuyển 1:1 của bản Python + phần mở rộng "chạm nhẹ". Test bằng node.
  ap-orbit.js      ES module: đổi yaw/pitch/zoom của bộ điều khiển → quay camera OrbitControls. Nhận THREE qua tham số.
  chon-camera.js   UMD, hàm thuần: chọn camera Mac, bỏ iPhone. Test bằng node.
  theo-doi-tay.js  ES module: camera + MediaPipe + vòng nhận diện + bảng camera + con trỏ.
  dieu-khien-tay.css   mọi class có tiền tố .dkt-
  demo.html        trang thử độc lập: người nộm three.js + OrbitControls cấu hình y hệt trang Sinh học
  du-lieu-thu/<kịch bản>.json   phần "vao" tách từ dữ liệu vàng, để demo phát lại
  README.md
frontend/vendor/mediapipe/
  tasks-vision-1.1.0/  vision_bundle.mjs, LICENSE, wasm/* (chỉ những file thật sự được tải)
  hand_landmarker.task (đúng file bản Python đang dùng, 7 819 105 byte)
tools/tach_du_lieu_thu_tay.mjs     tách "vao" từ dữ liệu vàng → du-lieu-thu/
tools/kiem_tra_dieu_khien_tay.py   Playwright nghiệm thu
desktop/quyen_camera.js            hàm thuần: có cho phép camera không
tests/js/dieu_khien_tay_vang.test.mjs, dieu_khien_tay_mo_rong.test.mjs, dieu_khien_tay_ap_orbit.test.mjs,
         dieu_khien_tay_chon_camera.test.mjs, quyen_camera.test.mjs
tests/test_dieu_khien_tay.py
tests/test_sinh_hoc.py, tests/test_sinh_hoc_v2.py   ← THÊM test cho nút và API điều khiển tay
tools/kiem_tra_tay_sinh_hoc.py     Playwright trên chính trang /sinh-hoc và /sinh-hoc-v2 (GĐ7)
```

- **Đường dẫn:** trong module, mọi đường dẫn vendor và CSS tính từ `import.meta.url`. Không viết cứng domain, không dùng CDN.
- **Nạp lười:** chỉ `import()` module, wasm (~10 MB) và mô hình (7,8 MB) khi người dùng bấm bật lần đầu. Trang không chậm thêm khi không dùng tính năng này.

---

## 5. Đặc tả chuyển sang JavaScript

### 5.1. `cu-chi.js` – bản chuyển 1:1

**Bắt buộc:**
- Xuất `HANG_SO` (đúng giá trị của `hang_so` trong dữ liệu vàng) và các hàm/lớp: `gocNgan`, `LocOneEuro`, `tiLeChum`, `laXoe`, `docTay`, `DieuKhien`.
- Logic, thứ tự phép tính và điều kiện **giống từng dòng** bản Python. Tên đổi sang camelCase, chú thích tiếng Việt như bản gốc.
- Đầu vào của `docTay(kq, tiLe, t, cacTay)` có dạng `{ hand_landmarks, hand_world_landmarks, handedness }` giống MediaPipe. Điểm có `.x .y .z`; `handedness[i][0].categoryName` (bản JS) hoặc `.category_name`. Viết một hàm đổi từ khung trong dữ liệu vàng sang dạng này.
- `con_tro` là mảng 2 số `[x·tiLe, y]`. Phép trừ véc-tơ, `hypot`, `norm` viết tay.

**Những chỗ dễ sai** (đối chiếu kỹ với Python):
1. **Chụm có trễ, phải đủ 2 khung liên tiếp:** `muonCham = !tay.cham ? tl < CHAM_BAT : tl <= CHAM_NHA`; biến đếm `dem` tăng khi `muonCham !== tay.cham`, ngược lại về 0; `dem >= 2` thì đổi.
2. **Tay mới hoặc mất > 0,3 s** thì tạo `Tay` mới (bộ lọc mới, `cham = false`).
3. **One Euro:** `te = t − tTruoc`. Lần đầu, hoặc khi `t <= tTruoc`, trả nguyên giá trị. `dx` là véc-tơ 2 chiều; tần số cắt = `minCutoff + beta·‖dx‖`.
4. **`tiLeChum`:** có `theGioi` thì dùng khoảng cách 3D; không có thì 2D với x nhân `tiLe`. Cỡ tay ≤ 1e-6 thì trả 9.
5. **`laXoe`:** 4 cặp (8,6), (12,10), (16,14), (20,18), hệ số 1,15. `xoe = tl > 0.6 && laXoe(...)`.
6. **Kéo:**
   - Vượt `NGUONG_TRUOT` mới bắt đầu xoay, và đặt lại mốc tại đó.
   - Nhảy > 0,2 thì đặt lại mốc.
   - **Chỉ áp chuyển động khi `tl < (CHAM_BAT + CHAM_NHA)/2`.**
   - `lichSu` chỉ thêm mẫu khi có áp chuyển động, giữ tối đa 90 mẫu.
7. **Thả (`_tha`):** lấy các mẫu trong 0,12 s cuối tính từ **mẫu cuối cùng**. Mẫu cuối cũ hơn 0,25 s thì không lướt. Tốc độ trên `QT_NGUONG` thì lướt; `wPitch` nhân 0,5; cả hai kẹp ±`QT_MAX`.
8. **Quán tính:** `wYaw *= exp(−dt/QT_TAT)`, `wPitch *= exp(−dt/(QT_TAT/2))`. Chạm biên pitch thì `wPitch = 0`. Tổng tốc độ < 0,05 thì về `"cho"`.
9. **Zoom:** hệ số mỗi khung kẹp [0,9; 1,1], bỏ qua khi |hệ số − 1| ≤ 0,003, rồi kẹp vào `[ZOOM_MIN, ZOOM_MAX]`.
10. **Mất tay:** khi đang kéo mà tay đó biến mất thì chờ `MAT_TAY_S` rồi huỷ (không lướt). Đang zoom mà nhả hết thì về `"cho"` (xem nhánh `elif self.che_do == "zoom"`).
11. **Xoè 2 tay ≥ `XOE_GIU_S`:** gọi `veGocDau()` đúng một lần cho mỗi lần xoè.

**Hai tuỳ chọn cho trang** (mặc định giống Python, để test vàng chạy nguyên trạng):
- `new DieuKhien({ veGocDauNoiBo: true })`. Đặt `false` thì `veGocDau()` **không** tự chạy hoạt cảnh: nó chỉ dừng quán tính rồi phát sự kiện `dat_lai`, để trang gọi `chonGocNhin()` của chính nó.
- `khiSuKien(suKien)`: callback, mặc định không làm gì. Các sự kiện:
  - `bat_dau` khi bắt đầu kéo hoặc zoom; `ket_thuc` khi thả hoặc hết quán tính;
  - `dat_lai`;
  - `cham_chon {x, y}`: **phần mở rộng, không có trong Python.** Phát khi một lần kéo kết thúc qua đường "tay vẫn còn, chỉ nhả ngón", với `da_truot` còn `false` và thời gian chụm ≤ 0,3 s. `x, y` = con trỏ lúc bắt đầu chụm, đổi ra toạ độ 0..1 của khung 3D bằng `VUNG` (giống hàm `ra_cua_so`).

  Các sự kiện này **không được làm đổi** bất kỳ con số nào trong dữ liệu vàng.

### 5.2. `ap-orbit.js` – áp lên camera OrbitControls

```js
export function taoBoApOrbit({ THREE, camera, controls, dieuKhien, doMuot = 0.03 })
  → { capNhat(dt), dongBo() }
```

- **Đổi dấu.** Python xoay **mô hình**, trang quay **camera** quanh `controls.target`. Hai cách này tương đương với:
  - `θ_camera −= Δyaw`: Python yaw tăng thì mặt trước mô hình quay sang phải người xem, tức camera đi vòng sang trái.
  - `φ_camera −= Δpitch`: Python pitch tăng thì thấy từ trên xuống nhiều hơn, tức camera lên cao.
  - `bán kính *= zoomTrước / zoomSau`.

  Trong đó Δ = giá trị của `dieuKhien` bây giờ trừ giá trị lần `capNhat` trước.
- **Mượt.** Nhận diện chạy khoảng 30 Hz, màn hình vẽ 60 Hz. Cộng dồn Δ vào hàng chờ; mỗi khung vẽ áp phần `k = 1 − exp(−dt/doMuot)` của hàng chờ. Dùng `THREE.Spherical`, kẹp theo `minPolarAngle`/`maxPolarAngle`/`minDistance`/`maxDistance` của controls, không lăn camera.
- **`capNhat(dt)` gọi TRƯỚC `controls.update()`.**
- **Báo cho trang:** khi `dieuKhien` phát `bat_dau` thì `controls.dispatchEvent({type:"start"})`; khi phát `ket_thuc` thì `{type:"end"}`. Đánh dấu là "do tay" để phân biệt.
- **Chuột cắt ngang:** controls phát `"start"` không phải do tay (người dùng kéo chuột) thì dừng quán tính của `dieuKhien` (`wYaw = wPitch = 0`, `cheDo = "cho"`).
- **`dongBo()`:** gọi khi trang tự bay camera (ví dụ `chonGocNhin`, mở cơ quan). Xoá hàng chờ và lấy lại mốc, để không cộng dồn lệch.

### 5.3. `chon-camera.js` – không bao giờ mở camera iPhone

`chonCamera(dsThietBi)` nhận mảng `{deviceId, label}` (từ `enumerateDevices`, `kind === "videoinput"`) và trả về `deviceId` hoặc `null`:
1. Bỏ mọi thiết bị có nhãn khớp `/iphone|ipad|continuity|liên tục|desk view|camera bàn/i`.
2. Trong số còn lại, ưu tiên nhãn chứa `facetime`, rồi `built-in` / `tích hợp`, rồi thiết bị đầu tiên.
3. Không còn thiết bị nào thì trả `null`.

Trong `theo-doi-tay.js`:
- Nếu `enumerateDevices` chưa có nhãn (chưa cấp quyền): gọi `getUserMedia({video:true})` một lần để xin quyền, dừng ngay mọi track, rồi liệt kê lại.
- Sau đó mở bằng `deviceId: { exact: id }`.
- Trả `null` thì báo "Chỉ thấy camera điện thoại. Bộ điều khiển tay chỉ dùng camera của máy tính." **Không** rơi về camera mặc định.
- Nghe `devicechange`: camera đang dùng biến mất thì chọn lại theo đúng luật trên.

**Lưu ý nhãn:** nhãn của camera iPhone có thể **không** chứa chữ "iPhone". Máy người dùng hiện "Camera idc🎶". Vì thế ưu tiên FaceTime là then chốt. Test phải có ca đúng như máy người dùng: `["Camera idc🎶", "Camera FaceTime HD"]` → chọn FaceTime.

### 5.4. `theo-doi-tay.js` – camera, MediaPipe, giao diện

```js
export async function taoDieuKhienTay({ khung3D, vungBang = khung3D, dieuKhien, nguon = "camera", tanSoToiDa = 30 })
  → { bat(), tat(), huy(), get trangThai() }
// nguon: "camera" | { phatLai: "<url json>" } | { websocket: "ws://127.0.0.1:<cổng>" }
// trangThai: { bat, dangTai, loi, uyQuyen, tanSoNhanDien, msNhanDien, soTay, cheDo, camera, nguon }
```

**MediaPipe**
- Gói `@mediapipe/tasks-vision` **1.1.0**. Bản 1.1.1 mới ra 14/10/2026, chưa đủ 2 tuần lúc Claude kiểm.
- **Đọc `vision.d.ts` trong gói** để dùng đúng API: `FilesetResolver.forVisionTasks`, `HandLandmarker.createFromOptions`, `detectForVideo`. Nếu 1.1.0 không chạy được thì lùi về 0.10.35 và ghi lý do vào HANDOFF.
- Tuỳ chọn **giống bản Python**: `runningMode: "VIDEO"`, `numHands: 2`, ba ngưỡng tin cậy = 0,6.
- `delegate: "GPU"` trước, lỗi thì `"CPU"`.

**Giống Python (quan trọng để cảm giác y hệt)**
- Mỗi khung vẽ video lên một canvas rộng **640 px**, **lật gương** bằng `ctx.scale(-1, 1)`, rồi `detectForVideo(canvas, ms)`. Python lật trước khi nhận diện, nên nhãn tay trái/phải và chiều x mới khớp.
- `ms` phải tăng dần, giống `ms = max(int(t*1000), ms_truoc+1)`.
- Mỗi kết quả → `docTay` → `dieuKhien.capNhat(t, dt, dsTay)`, với `dt` kẹp [0,001; 0,1] như Python.

**Camera và vòng nhận diện**
- `getUserMedia({ video: { deviceId: {exact}, width: {ideal: 1280}, height: {ideal: 720}, frameRate: {ideal: 30} }, audio: false })`.
- Gọi nhận diện tối đa `tanSoToiDa` lần/giây, bằng `requestVideoFrameCallback`. Lần trước chưa xong thì bỏ khung. `document.hidden` thì dừng.

**Giao diện** (chuyển từ `ve_giao_dien` / `ve_con_tro` của Python, nhưng chữ là tiếng Việt có dấu)
- **Con trỏ** đè lên `khung3D` (`pointer-events: none`), vị trí qua `VUNG`:
  - vòng tròn trắng, phần xanh đầy theo `(0,7 − tl)/(0,7 − CHAM_BAT)`;
  - đang chạm thì thành chấm cam đặc + vòng ngoài; đang zoom thì màu xanh dương, có đường nối hai con trỏ.
- **Bảng camera** góc dưới phải: rộng 320 px (di động 160 px), video lật gương + khung xương (đường nối như `NOI` trong `xem_tay.py`), thu nhỏ được.
- **Chip trạng thái** gồm: "Chờ", "Đang xoay", "Đang lướt", "Phóng to / thu nhỏ", "Về góc đầu", "Đang mở camera…", "Đang tải mô hình…", và các thông báo lỗi.
- **Vòng tiến độ** khi đang xoè hai tay.
- **Thẻ "Cách dùng"** gồm 5 dòng như `HUONG_DAN` của Python, viết có dấu. Có thêm dòng: "Chạm nhẹ: chọn cơ quan".
- Dòng chữ nhỏ: "Hình camera chỉ xử lý trên máy này, không gửi đi, không lưu."
- **Thông báo lỗi** từng trường hợp:

  | Lỗi | Câu báo |
  |---|---|
  | `NotAllowedError` | hướng dẫn vào Cài đặt hệ thống → Quyền riêng tư & Bảo mật → Camera |
  | `NotFoundError` | "Không tìm thấy camera." |
  | `NotReadableError` | camera đang bị app khác dùng |
  | chỉ có camera điện thoại | câu ở mục 5.3 |
  | `file:` | yêu cầu mở qua máy chủ |
  | mô hình tải lỗi | "Không tải được mô hình nhận diện tay." |

- Không tạo phần tử DOM mới mỗi khung. Nền bảng tối cố định để đọc được cả khi trang dùng nền trắng. Có `prefers-reduced-motion`.

**Quyền riêng tư**
- Chỉ bật khi người dùng bấm, không tự bật lúc tải trang.
- Không gửi khung hình hay điểm tay đi đâu, không lưu, không dùng micro.
- `tat()` / `huy()` / `pagehide` → `track.stop()` cho mọi track.

---

## 6. Các giai đoạn

Mỗi giai đoạn: làm xong → chạy test liên quan → cập nhật HANDOFF.md → commit `feat(dieu-khien-tay): GDn …`.

### GĐ0 – Đọc và tạo điểm phục hồi
Làm mục 0.

### GĐ1 – Vendor MediaPipe ngoại tuyến
1. Tải và giải nén gói:
   `mkdir -p ~/AppBuild/tmp-mediapipe && cd ~/AppBuild/tmp-mediapipe && npm pack @mediapipe/tasks-vision@1.1.0 && tar xzf mediapipe-tasks-vision-1.1.0.tgz`
2. Chép vào `frontend/vendor/mediapipe/tasks-vision-1.1.0/`: `vision_bundle.mjs`, `LICENSE`, và **chỉ những file `wasm/` mà `FilesetResolver` thật sự tải**. Xem tab Network khi chạy demo; đừng đoán.
3. Chép mô hình **đúng file bản Python đang dùng**: `cp ~/Downloads/hand_landmarker.task frontend/vendor/mediapipe/`. Nếu không có thì tải:
   `curl -L -o frontend/vendor/mediapipe/hand_landmarker.task https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task`

   Phải đúng **7 819 105 byte**.
4. Ghi từng file vào `frontend/vendor/PHIEN_BAN.json` (`ten`, `url`, `phien_ban`, `sha256`, `kich_thuoc_bytes`).
5. Thêm mục "Thư viện phần mềm" cuối `GHI_CONG.md`: MediaPipe Tasks Vision + Hand Landmarker © Google, Apache-2.0.

### GĐ2 – `cu-chi.js` + test vàng (viết test trước)
- **`tests/js/dieu_khien_tay_vang.test.mjs`**
  - Với **mỗi** kịch bản: tạo `DieuKhien` mới và `cacTay = {}`. Cho từng khung `vao` qua `docTay` + `capNhat`.
  - So **mọi** trường của `ra` với tolerance 1e-6: `yaw`, `pitch`, `zoom`, `wYaw`, `wPitch`, `cheDo`, `veDau`, và từng tay: `ben`, `conTro`, `cham`, `tl`, `xoe`.
  - Sai thì báo **tên kịch bản + số khung + trường lệch + hai giá trị**.
  - Kiểm `HANG_SO` khớp `hang_so`.
  - **Phải đạt đủ 9/9 kịch bản.**
- **`tests/js/dieu_khien_tay_mo_rong.test.mjs`**, ít nhất 7 ca:
  1. Chạm nhẹ 0,2 s không kéo → đúng một `cham_chon`, toạ độ đúng theo `VUNG`.
  2. Chụm 0,6 s không kéo → không có `cham_chon`.
  3. Chụm rồi kéo → không có `cham_chon`.
  4. `bat_dau` / `ket_thuc` đúng thứ tự, mỗi thứ một lần, cho một lần kéo có lướt.
  5. `veGocDauNoiBo: false` → xoè hai tay 1 giây chỉ phát `dat_lai`; yaw, pitch, zoom không tự đổi.
  6. Bật các sự kiện mà chạy lại 9 kịch bản vàng → số liệu vẫn khớp.
  7. Đầu vào hỏng (mảng rỗng, thiếu điểm, NaN) → không ném lỗi.

### GĐ3 – `ap-orbit.js` + `chon-camera.js` + test
- **`tests/js/dieu_khien_tay_ap_orbit.test.mjs`**: import THREE từ `frontend/vendor/three/three.module.js`, dùng controls giả `{target, min/maxDistance, min/maxPolarAngle, addEventListener, dispatchEvent}`. Ít nhất 7 ca:
  1. yaw +0,5 rad → góc phương vị camera giảm 0,5 (±1 %) sau khi hội tụ.
  2. pitch +0,3 → góc cực giảm 0,3.
  3. zoom 1 → 2 → khoảng cách giảm một nửa.
  4. Kẹp min/max cả góc cực lẫn khoảng cách.
  5. Mượt: một bước Δ lớn được áp dần qua nhiều khung vẽ, không vượt quá.
  6. `"start"` từ chuột → quán tính của `dieuKhien` dừng.
  7. `dongBo()` sau khi trang tự bay camera → không có cú giật.
- **`tests/js/dieu_khien_tay_chon_camera.test.mjs`**, ít nhất 6 ca:
  - `["Camera idc🎶", "Camera FaceTime HD"]` → FaceTime;
  - `["iPhone Camera", "FaceTime HD Camera"]` → FaceTime;
  - chỉ có iPhone → `null`;
  - nhãn rỗng;
  - webcam USB + iPhone → webcam;
  - nhãn tiếng Việt "Camera Liên tục" → bỏ.

### GĐ4 – `theo-doi-tay.js` + CSS
Làm theo mục 5.4.

### GĐ5 – Demo + nghiệm thu trình duyệt + pytest
**`demo.html`** mở ở `http://127.0.0.1:8000/static/dieu-khien-tay/demo.html`
- importmap trỏ `/static/vendor/three/...`.
- Người nộm dựng bằng các khối elip **cùng kích thước và màu như `dung_nguoi_nom()` của Python**: áo xanh, quần xám, có mắt mũi, bệ xoay có múi cam chỉ mặt trước. Thêm vài "cơ quan" (tim, 2 lá phổi) để thử chạm nhẹ.
- OrbitControls cấu hình y hệt trang Sinh học.
- `cham_chon` thì tô sáng bộ phận và hiện tên.
- `?phat=<kịch bản>` phát lại `du-lieu-thu/<kịch bản>.json`, sinh bằng `tools/tach_du_lieu_thu_tay.mjs`.
- Góc trên hiện: FPS, Hz và ms nhận diện, GPU/CPU, tên camera.

**`tools/kiem_tra_dieu_khien_tay.py`**
- Playwright, `channel="chrome"`, `headless=False`, cờ `--use-fake-device-for-media-stream --use-fake-ui-for-media-stream`.
- Ghi **mọi con số** vào `outputs/dieu-khien-tay/ket-qua.json` và ảnh `01..06.png`. **Không được** ghi "đã kiểm tra bằng mắt" thay cho số.

| Mã | Kịch bản | Đạt khi |
|---|---|---|
| K1 | Camera giả + mô hình thật | bật trong ≤ 15 s; sau 5 s Hz ≥ 15 (GPU) hoặc ≥ 10 (CPU); không lỗi console |
| K2 | Mạng | không request nào ra ngoài `127.0.0.1` |
| K3 | Tắt | mọi track `ended` |
| K4 | `?phat=keo_luot` | góc phương vị camera giảm ≥ 60° khi kéo; vẫn quay tiếp sau khi thả; dừng trong < 4 s |
| K5 | `?phat=bat_lai` | quay dừng trong ≤ 2 khung nhận diện sau khi chụm lại |
| K6 | `?phat=zoom_hai_tay` | khoảng cách camera nhỏ nhất ≈ khoảng cách đầu / 1,5 (±5 %) |
| K7 | `?phat=xoe_dat_lai` | về góc đầu (sai số < 2°) |
| K8 | `?phat=rung_tay` | camera đổi < 0,5° |
| K9 | FPS khi tắt và khi bật camera giả | khi bật ≥ 0,85 × khi tắt, hoặc ≥ 50 |
| K10 | 390×844 | bảng camera che ≤ 25 % khung 3D |

**`tests/test_dieu_khien_tay.py`**
- File vendor tồn tại, sha256 khớp, mô hình đúng 7 819 105 byte.
- Module không chứa `cdn.`, `jsdelivr`, `unpkg`, `googleapis`.
- **Chỉ Sinh học:** `index.html`, `app.js`, `trang-chu.js`, `phong-thi-nghiem.html`, `hoa-ly.html` không chứa `dieu-khien-tay`.
- Demo trả 200, `.wasm` trả `application/wasm`.
- Dữ liệu vàng tồn tại và đủ 9 kịch bản.

### GĐ6 – Quyền camera cho app máy tính (chỉ Sinh học)
- `desktop/quyen_camera.js` (CommonJS, thuần): `choPhepCamera({ urlYeuCau, gocMayChu, loaiQuyen, kieuMedia })` trả `true` **chỉ khi** đủ cả 4 điều kiện:
  - `loaiQuyen === "media"`;
  - chỉ có `"video"`;
  - cùng origin máy chủ cục bộ của app;
  - đường dẫn bắt đầu bằng `/sinh-hoc` hoặc `/static/dieu-khien-tay/`.
- `desktop/main.js`: `session.setPermissionRequestHandler`.
  - "media" thì hỏi `choPhepCamera`; được thì trên macOS gọi `systemPreferences.askForMediaAccess("camera")`.
  - **Mọi loại quyền khác giữ nguyên hành vi hiện tại** (đang cho phép vì chưa có handler). Đặc biệt không làm hỏng pointer lock, toàn màn hình, clipboard của phòng thí nghiệm Hoá.
- `desktop/package.json` → `build.mac.extendInfo.NSCameraUsageDescription`: "CHEMISTRY dùng camera để bạn xoay mô hình giải phẫu 3D bằng cử chỉ tay. Hình ảnh chỉ xử lý trên máy, không gửi đi." Giữ nguyên `CFBundleURLTypes`.
- `tests/js/quyen_camera.test.mjs`, ít nhất 7 ca.
- **Không** build lại DMG. Ghi vào HANDOFF.

### GĐ7 – Gắn thẳng vào phòng Sinh học (`/sinh-hoc` rồi `/sinh-hoc-v2`) + bàn giao
Làm theo mục 0.3: kiểm `git status` / `git log -3` của từng file, tạo điểm phục hồi `chore: diem phuc hoi truoc gan tay vao sinh hoc`, đọc theo khoảng dòng (mục 3).

**1. Sửa `frontend/biology-lab-3d.html`:**
- **Nút "Điều khiển bằng tay"** trong thanh công cụ của khung 3D: icon SVG bàn tay, `aria-pressed`, `title`. Chọn chỗ hợp với các nút sẵn có. Phím tắt `H` chỉ khi file chưa dùng phím đó (kiểm mọi `keydown` trong file).
- **Nạp lười:** bấm lần đầu mới `await import("/static/dieu-khien-tay/theo-doi-tay.js")` (cùng `ap-orbit.js`, `cu-chi.js`). Lúc mở trang **không** được có request nào tới `/static/dieu-khien-tay/` hay `/static/vendor/mediapipe/`.
- **Tạo bộ điều khiển:** `new DieuKhien({ veGocDauNoiBo: false, khiSuKien })`, `taoBoApOrbit({ THREE, camera, controls, dieuKhien })`, `taoDieuKhienTay({ khung3D: <khung chứa canvas>, dieuKhien })`.
- **Một dòng trong `vongLap()`** ngay trước `controls.update()` (khoảng dòng 5718): `boApOrbit?.capNhat(dt);`
- **`bat_dau`** → `gsap.killTweensOf(camera.position); gsap.killTweensOf(controls.target);`
- **`cham_chon {x,y}`**
  - Đổi ra `clientX/clientY` theo `renderer.domElement.getBoundingClientRect()`.
  - **Tách** thân hàm `pointerup` hiện có (khoảng dòng 4729–4738) thành hàm `chonTaiDiem(clientX, clientY)`. Chuột và tay cùng gọi hàm này. Hành vi chuột phải giữ y nguyên: vẫn chỉ chọn khi dịch ≤ 5 px và không ở `cheDoViMo`.
- **`dat_lai`** → `chonGocNhin()`: tên không có trong `GOC_NHIN` thì bay về `CAMERA_GOC`.
- **`boApOrbit.dongBo()`:** gọi sau **mọi** cú bay camera của trang (`bayToi`, mở/đóng cơ quan, vào/ra vi mô, `chonGocNhin`), khi tween xong (`onComplete`). Nhờ vậy tay và camera không cộng dồn lệch.
- **Tắt camera:**
  - `chonTab(true)` (sang Hoá học) → `tat()` hẳn;
  - `pauseRendering()` / trang ẩn → tạm tắt, khi hiện lại thì tự bật lại nếu người dùng đang bật;
  - `pagehide` → `huy()`.
- **Ẩn nút** khi `location.protocol === "file:"` hoặc không có `navigator.mediaDevices`.
- **API cho test**, chỉ **thêm** vào `window.BioLab`:
  - `dieuKhienTay: { bat(nguon?), tat(), get trangThai() }`. `bat({ phatLai: url })` phát lại một kịch bản thay cho camera.
  - `gocCamera`: getter `{ phuongVi, cuc, khoangCach }` (độ, độ, đơn vị cảnh).
  - `trangThai.tay` (bật/tắt, chế độ, camera).

**2. Làm tương tự cho `frontend/biology-lab-3d-v2.html`:**
- `controls` ở dòng 1270, chọn bằng tia ở 3592–3640, `window.BioLab` ở 4770.
- v2 có thêm chuột phải "bóc lớp": tay **không** thay thế việc đó.

**3. Test**
- **`tests/test_sinh_hoc.py` / `tests/test_sinh_hoc_v2.py`** (thêm, không sửa test cũ):
  - trang có nút điều khiển tay;
  - có `import("/static/dieu-khien-tay/`;
  - `BioLab` có `dieuKhienTay` và `gocCamera`;
  - không nạp MediaPipe lúc đầu.
- **`tools/kiem_tra_tay_sinh_hoc.py`:** Playwright `channel="chrome"`, `headless=False`, camera giả. Ghi `outputs/dieu-khien-tay/sinh-hoc.json` + ảnh. Chạy cho **cả hai trang**:

| Mã | Kịch bản | Đạt khi |
|---|---|---|
| K11 | Mở trang, chưa bật tay | không lỗi console; không request MediaPipe; `BioLab.fps` như trước khi sửa (±10 %) |
| K12 | `BioLab.dieuKhienTay.bat({phatLai: ".../keo_luot.json"})` | `gocCamera.phuongVi` giảm ≥ 60° khi kéo, quay tiếp sau khi thả, dừng trong < 4 s |
| K13 | `zoom_hai_tay` | `khoangCach` nhỏ nhất ≈ ban đầu / 1,5 (±5 %), không dưới `minDistance` |
| K14 | Chạm nhẹ ở giữa khung 3D, rồi bấm chuột đúng điểm đó (sau khi đặt lại) | cấu trúc được chọn **giống hệt** nhau |
| K15 | Đang bật tay rồi sang tab Hoá học | mọi video track `ended`, `trangThai.tay.bat = false` |
| K16 | FPS khi bật camera giả (đo 5 s) | ≥ 0,85 × lúc tắt, hoặc ≥ 45 (dưới 45 trang sẽ tự hạ chất lượng) |
| K17 | Chuột sau khi gắn | kéo chuột vẫn xoay, cuộn vẫn zoom, bấm vẫn chọn như cũ |

**4. Bàn giao**
- Thêm vào `CLAUDE.md`:
  - một dòng ở mục "## Sinh học 3D": nút tay, `chonTaiDiem`, `boApOrbit`, `BioLab.dieuKhienTay`, `gocCamera`;
  - một mục ngắn `## Điều khiển bằng tay (chỉ Sinh học)` ở cuối (3–5 dòng): module, bản mẫu Python, dữ liệu vàng, demo, test.
- Cập nhật HANDOFF.md: file nào, khoảng dòng nào đã sửa trong hai trang Sinh học.
- Chạy toàn bộ:
  - `.venv/bin/python -m pytest -q`
  - `node --test tests/js/`
  - `.venv/bin/python tools/kiem_tra_dieu_khien_tay.py`
  - `.venv/bin/python tools/kiem_tra_tay_sinh_hoc.py`
- Commit: `feat(sinh-hoc): dieu khien mo hinh 3D bang cu chi tay`.

### GĐ8 (tuỳ chọn) – Dùng đúng Python để điều khiển trang
Chỉ làm khi GĐ0–7 xong và test xanh.
- `tools/tay_python/gui_tay_ws.py`:
  - **import** `tim_camera`, `tao_may_nhan_dien`, `nhan_dien` từ `xem_tay.py` (không chép lại code).
  - Lật gương, nhận diện, rồi gửi mỗi khung qua `ws://127.0.0.1:8765` (chỉ nghe 127.0.0.1). Định dạng giống `vao` của dữ liệu vàng: `t`, `tiLe`, `tay[{ben, diem, theGioi}]`.
  - Chạy bằng `~/hand3d-env/bin/python`. Cài `websockets` vào `~/hand3d-env`, **không** vào `.venv`, **không** vào `requirements.txt`.
- Module nhận nguồn `{ websocket }`: bỏ qua camera trình duyệt, đưa khung thẳng vào `docTay` → `DieuKhien`, nên cảm giác giống hệt nguồn camera trình duyệt.
- Demo `?nguon=ws` dùng nguồn này. Có nút "Đo 5 giây" ghi Hz, độ rung con trỏ và độ trễ của hai nguồn vào `outputs/dieu-khien-tay/so-sanh-python.json`. Phần đo với tay thật để người dùng làm.

---

## 7. Không được làm
- Sửa đè thay đổi chưa commit của AI khác trong các file Sinh học; định dạng lại cả file trang Sinh học; bớt hay đổi tên thứ gì trong `window.BioLab`.
- Sửa bản mẫu Python hay dữ liệu vàng.
- Gắn module vào trang nào ngoài Sinh học.
- Mở camera iPhone/iPad, hoặc rơi về camera mặc định khi không chọn được camera của máy tính.
- Dùng CDN cho MediaPipe, gửi khung hình hoặc điểm tay ra ngoài máy, tự bật camera lúc tải trang, dùng micro.
- Thêm thư viện ngoài MediaPipe Tasks Vision.
- Đổi hành vi chuột hoặc cảm ứng hiện có của trang Sinh học.
- Build lại DMG, `git push`, sửa `.env`.

---

## 8. Báo cáo cuối (ngắn gọn, theo AGENTS.md)
1. Danh sách file mới/sửa và các commit.
2. Kết quả **test vàng 9/9**, test mở rộng, test áp OrbitControls, test chọn camera, test quyền camera, pytest.
3. Bảng K1–K10 (demo) và K11–K17 (cả `/sinh-hoc` lẫn `/sinh-hoc-v2`) kèm **số đo thật** từ `ket-qua.json` và `sinh-hoc.json`.
4. Khoảng dòng đã sửa trong hai trang Sinh học.
5. Hướng dẫn người dùng thử bằng tay thật **trong phòng Sinh học**:
   1. Mở `http://127.0.0.1:8000/sinh-hoc`, bấm nút "Điều khiển bằng tay", cho phép camera. Kiểm tra chip ghi đúng "Camera FaceTime HD", không phải iPhone.
   2. Chụm rồi kéo ngang: mô hình giải phẫu quay theo tay, cảm giác giống bản Python `tools/tay_python/xoay_3d.py`.
   3. Kéo nhanh rồi thả: quay tiếp rồi chậm dần. Chụm lại: dừng.
   4. Hai tay chụm kéo giãn: phóng to. Xoè hai tay 1 giây: về góc đầu.
   5. Chạm nhẹ vào tim: tim được chọn, bảng thông tin mở như khi bấm chuột.
   6. Sang tab Hoá học: đèn camera tắt.
