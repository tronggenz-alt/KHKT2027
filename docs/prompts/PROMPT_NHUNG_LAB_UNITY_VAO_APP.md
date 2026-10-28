# PROMPT – Đưa phòng lab Unity mới (PhongLabHoa3D) chạy THẲNG trong app CHEMISTRY; cần mượt hơn thì mở cửa sổ app riêng

> Gõ cho Antigravity: **"Đọc và làm theo PROMPT_NHUNG_LAB_UNITY_VAO_APP.md, chạy liên tục đến hết."**
> Phạm vi: **CHỈ app máy tính CHEMISTRY (Electron, thư mục `desktop/`)**. Trang web (`giai-chuoi-huu-co.onrender.com`, trình duyệt thường) **giữ nguyên** bản Three.js như hiện nay.
> Chỉ làm khi lượt Antigravity trước đã xong (xem mục 0.2).

---

## DÀN Ý NHANH
1. **Hiện tại (trong app):**
   - Trang Phòng thí nghiệm của app chạy bản **Three.js** (giống web).
   - Phòng lab Unity mới (toà nhà lớn, nội thất thật, nhân vật) chỉ có dạng `PhongLabHoa3D.app` riêng, mở qua nút "🖥 Mở bằng ứng dụng" (`chemistryApp.moAppLab`).
2. **Người dùng muốn:** vào Phòng thí nghiệm trong app là thấy **ngay phòng lab Unity mới**, không phải bản Three.js. Khi cần **mượt hơn** thì bấm nút để mở **cửa sổ app Unity riêng** (Metal, đa nhân).
3. **Cách làm:**
   - Build thêm bản **Unity WebGL** từ cùng dự án `PhongLabHoa3D` rồi **đóng gói vào trong app**.
   - App phục vụ các file build qua giao thức riêng `chemlab://` (đọc từ ổ đĩa, không tải mạng). Trang lab chạy bản này ngay trong cửa sổ app.
   - Lõi hoá học trên WebGL dùng `mo-phong.js` của trang (Jint + luồng riêng không chạy được trên WebGL).
4. **Máy yếu / lỗi:** tự về bản **Three.js nhẹ**. Các trường hợp: thiếu bản build, không có WebGL2, card đồ hoạ giả lập bằng phần mềm, máy ≤ 4 nhân hoặc ≤ 4 GB RAM, tải lỗi, fps quá thấp.
5. **Thứ tự làm:**
   1. Dọn trạng thái git và đồng bộ dự án Unity.
   2. Sửa Unity cho chạy được trên WebGL.
   3. Build WebGL.
   4. Sửa Electron (`chemlab://`).
   5. Sửa trang lab.
   6. Đóng gói app và nghiệm thu thật.

---

## 0. Việc phải làm trước khi viết code
### 0.1 Đọc
- **AGENTS.md**, **CLAUDE.md**, phần đầu **HANDOFF.md**, **TIEN_DO_APP_LAB.md**, **TIEN_DO_APP_CHEMISTRY.md**.
- `desktop/main.js` (mục 7 `moAppLabUnity`, `taoCuaSoChinh`, các `ipcMain.handle`), `desktop/preload.js`, `desktop/package.json` (`extraResources`), `desktop/build-mac.sh`.
- `frontend/hoa-hoc/phong-thi-nghiem/giao-dien.js`: hàm `batDau`, đoạn "Khởi tạo nút Mở bằng ứng dụng" (khoảng dòng 282).
- Dự án Unity: `Assets/Scripts/Loi/LoiHoaHoc.cs`, `GiaoDien/GiaoDienApp.cs`, `NhanVat/DieuKhienNhanVat.cs`, `Editor/BuildMayTinh.cs`, `Editor/DongBoDuLieu.cs`.

### 0.2 Trạng thái git: KHÔNG được làm mất việc dở
- Lúc viết prompt này (25/10) đang có thay đổi **chưa commit**:
  - `desktop/main.js`, `desktop/build-mac.sh`, `desktop/kiem_tra/kiem_tra_app.py`.
  - `frontend/hoa-hoc/phong-thi-nghiem/giao-dien.js`, `frontend/style.css`, `frontend/vendor/PHIEN_BAN.json`.
  - Unity: `CanhAnToan.cs`, `CanhNhanBiet.cs`, `ChiTietThuc.cs`, `VatLieuThuc.cs`, cùng các file mới `CauHinhDoHoa.cs`, `NgonLua.cs`, `ChatLuongDoHoa.cs`, `Shaders/`.
- Chạy `git status`. Nếu vẫn còn thay đổi chưa commit:
  - Đọc `HANDOFF.md`, `TIEN_DO_APP_LAB.md` và `PROMPT_SUA_APP_LAB_FPS_CHAN_THAT.md` để biết đó là việc gì.
  - Chạy test, rồi **commit nguyên trạng** với thông điệp `chore: luu viec dang do truoc khi nhung lab Unity vao app`.
  - **Không** `git checkout -- …`, không `git stash`, không `git reset`.
  - Nếu test cũ đang FAIL vì việc dở đó thì ghi vào HANDOFF.md và **dừng lại báo người dùng**, không tự sửa việc của prompt khác.
- Chạy baseline và ghi lại số PASS:
  - `.venv/bin/python -m pytest -q` (gần nhất 611 PASS).
  - `node --test tests/js/` (48 PASS).
  - Unity EditMode (37 PASS).
- Commit điểm phục hồi: `chore: diem phuc hoi truoc khi nhung lab Unity vao app`.

### 0.3 Hai bản dự án Unity – đồng bộ đúng chiều
- **Bản làm việc thật** nằm NGOÀI iCloud: `~/UnityProjects/PhongLabHoa3D` (có `Library/` 1,1 GB).
- **Bản lưu trong repo:** `unity/PhongLabHoa3D`. Đồng bộ bằng `tools/unity_dong_bo.sh keo` (repo → ngoài) hoặc `day` (ngoài → repo).
- Hiện **repo đang MỚI HƠN** bản ngoài: repo có `NgonLua.cs`, `ChatLuongDoHoa.cs`, `CauHinhDoHoa.cs`, `Shaders/`, và 4 file khác nội dung. Vì vậy:
  - Chạy `diff -rq` (bỏ qua Library / Temp / Logs / Builds / UserSettings / TaiNguyenNgoai) để xác nhận.
  - Rồi chạy **`keo` trước**.
  - **Tuyệt đối không chạy `day` khi chưa `keo`**, vì sẽ ghi đè mất phần mới trong repo.
- Sửa code ở bản ngoài, mở Unity bằng bản ngoài. Xong mỗi bước thì `day` rồi commit.

### 0.4 Máy và luật chung
- Máy: Apple M2, **8 GB RAM**, ổ đĩa còn khoảng **16 GB** (93 % đầy).
  - Trước khi build WebGL, kiểm tra `df -h ~` còn **≥ 8 GB**. Thiếu thì dừng, báo người dùng dọn ổ; không tự xoá gì.
  - Khi build Unity, đóng app CHEMISTRY và trình duyệt nặng.
- Unity 6000.6.7f1: `~/Documents/6000.6.7f1/Unity.app` (**nằm trong iCloud**), đã có module **WebGLSupport** (`~/Documents/6000.6.7f1/PlaybackEngines/WebGLSupport`).
  - Không di chuyển, không cài lại Unity.
  - Unity báo thiếu file vì iCloud chưa tải về máy → báo người dùng.
- Build bằng dòng lệnh, chạy **nền**, ghi log:
  `~/Documents/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath ~/UnityProjects/PhongLabHoa3D -executeMethod PhongThiNghiem.Editor.BuildMayTinh.WebGLAppBatch -logFile logs/unity-webgl.log`.
  Lần đầu build IL2CPP WebGL có thể mất 15–40 phút.
- Không sửa `.env`. Không `git push`. Không deploy web (`dua-len-web.command`) khi người dùng chưa đồng ý.
- **Không sửa** vùng Lab Sinh học 3D (`frontend/biology-lab-3d*.html`, `frontend/sinh-hoc/`, `backend/sinh_hoc_ai.py`).
- Không commit file build: `outputs/` và `Builds/` đã nằm trong `.gitignore`.
- Thư mục dự án nằm trong iCloud: không `grep -r` toàn repo. Grep đúng file.

---

## 1. Unity: chạy được trên WebGL (bản app macOS vẫn y như cũ)
Mọi chỗ khác nhau giữa hai nền tảng bọc bằng `#if UNITY_WEBGL && !UNITY_EDITOR`. Bản macOS phải chạy **giống hệt** trước.

### 1.1 Đọc dữ liệu: bỏ `File.ReadAllText` trên WebGL
- Trên WebGL, `Application.streamingAssetsPath` là một URL nên `File.ReadAllText` sẽ lỗi. Các chỗ đang dùng:
  - `LoiHoaHoc.NapFileNguon` (+ dòng khoảng 120).
  - `BangTuanHoan3D` (khoảng 122–136).
  - `DungToaNha` (khoảng 134–143).
  - `DungPhongLab` (khoảng 58–67).
- Tạo `Assets/Scripts/HeThong/DocTaiNguyen.cs`, hàm `string DocChuoi(string duongDanTuongDoi)`:
  - Đọc từ **`Resources`** dạng `TextAsset` (đồng bộ, chạy được mọi nền tảng).
  - Sửa `Editor/DongBoDuLieu.cs` để chép thêm các file JSON (`toa-nha`, `ban-do`, `nguyen-to`, `bang-ion`, `TAI_NGUYEN`, `thi-nghiem/*.json`) vào `Assets/Resources/ptn/`.
  - Hai file JS lõi chỉ bản macOS (Jint) cần, nên giữ ở StreamingAssets.
- Đổi 4 chỗ trên sang `DocTaiNguyen.DocChuoi(...)`.

### 1.2 Lõi hoá học: một giao diện, hai cách chạy
- Tách giao diện `ILoiHoaHoc` từ các hàm công khai đang có của `LoiHoaHoc`:
  - Hàm: `Tao(id, cheDo)`, `ThaoTac`, `ThaoTacDongBo`, `BatDauMoPhong`, `TamDungMoPhong`, `TrangThai()`, `SoTay()`, `CapNhatLuongChinh()`, `Dispose`.
  - Sự kiện: `OnTrangThai`, `OnSoTay`, `OnHieuUng`.
  - Mọi script dùng lõi phải đi qua giao diện này.
- **macOS:** `LoiHoaHoc` hiện có (Jint + luồng `LuongMoPhongLoiHoaHoc`), không đổi hành vi.
- **WebGL:** class mới `LoiHoaHocTrinhDuyet : ILoiHoaHoc`, gọi lõi `mo-phong.js` **của trang** qua `Assets/Plugins/WebGL/CauNoiTrang.jslib`:
  - Unity → trang: `PTN_Tao(id, cheDo)`, `PTN_ThaoTac(id, lenh, json)`, `PTN_TrangThai(id)` (trả **chuỗi JSON đồng bộ**), `PTN_SoTay(id)`, `PTN_ChoChay(bool)` (trang chỉ cho thời gian chạy khi đang tại bàn).
  - Unity → trang (sự kiện): `PTN_BaoSuKien(ten, json)`. Sự kiện gồm `san_sang`, `tram` (đang ở trạm nào / tại bàn hay không), `fps` (mỗi 2 s), `mo_cua_so_muot`, `thoat`, `loi`.
  - Trang → Unity: `SendMessage("CauNoiTrang", "NhanTrangThai" | "NhanSoTay" | "NhanHieuUng" | "MoThiNghiem" | "DoiGiaoDien", json)`. Cần GameObject `CauNoiTrang` được tạo bằng code lúc khởi động.
- Chọn cài đặt lúc chạy bằng `#if`. Trong Editor vẫn dùng Jint như cũ để test EditMode chạy được.
- `Jint.dll` / `Esprima.dll`: trong Plugin Import Settings **bỏ nền tảng WebGL** để build WebGL không kéo Jint vào. Code dùng `using Jint` bọc `#if !UNITY_WEBGL || UNITY_EDITOR`.

### 1.3 Không dùng luồng trên WebGL
- `VatLieuThuc.cs` dùng `Task.Run`. Trên WebGL phải chạy tuần tự **rải qua nhiều khung hình** (coroutine), để màn hình không đứng hình lúc dựng vật liệu.
- Tìm thêm `Thread`, `Task.Run`, `Parallel`, `System.Diagnostics.Process` trong `Assets/Scripts` và xử lí hết.
- `TuKiemTraApp`: tắt trên WebGL (dùng `Screen.SetResolution`, `Application.Quit`).

### 1.4 Hành vi trong cửa sổ app
- `WebGLInput.captureAllKeyboardInput = false`: Unity chỉ nhận phím khi canvas đang có focus. Bấm vào canvas thì focus.
- **Khoá chuột** (`DieuKhienNhanVat.KhoaChuot`): trên WebGL chỉ khoá khi người dùng bấm vào canvas.
  Lần **Esc** đầu tiên trình duyệt dùng để thả chuột. Unity phải hiểu việc mất khoá là "thả chuột", không mở menu và không đẩy nhân vật ra khỏi bàn.
  Ra khỏi bàn hoặc mở menu dùng **Esc lần hai** hoặc phím **Tab**. Ghi rõ trong HUD gợi ý phím.
- `GiaoDienApp`:
  - Mục **"Thoát"** gọi `Application.Quit()`, không chạy trên WebGL. Đổi thành gửi sự kiện `thoat` cho trang (trang tắt Unity rồi về trang chủ app).
  - Thêm mục **"⚡ Mở cửa sổ mượt"**, gửi `mo_cua_so_muot`.
  - Ẩn các tuỳ chọn độ phân giải / VSync / giới hạn FPS không áp dụng được trên WebGL.
- Deep link `phonglabhoa://` không dùng trên WebGL. Thí nghiệm ban đầu do trang gửi qua `MoThiNghiem`.

### 1.5 Chất lượng hình cho WebGL (`ChatLuongDoHoa` / `CauHinhDoHoa`)
- Thêm mức **"WebGL"**, tự chọn khi chạy WebGL:
  - Bóng: chỉ đèn chính, 1 cascade, khoảng cách 20 m, bóng cứng.
  - Không SSAO, bloom nhẹ hoặc tắt. MSAA 2x hoặc FXAA. Render scale tự giảm 1,0 → 0,8 nếu fps < 40.
  - Hạt hiệu ứng giảm một nửa. Texture tối đa 1K. LOD bias thấp.
- Bản macOS **giữ nguyên** mức hiện có.

### 1.6 Build WebGL – `Editor/BuildMayTinh.cs`
- Thêm `WebGLApp()` (menu "Phòng Lab/Build WebGL cho app CHEMISTRY") và `WebGLAppBatch()`, theo khuôn của `BuildMacThucThi`, cùng scene `Assets/Scenes/PhongLab.unity` và cùng cấu hình URP.
- Xuất ra `~/UnityProjects/PhongLabHoa3D/Builds/WebGL/lab/`. Tên file cố định: `lab.loader.js`, `lab.data`, `lab.framework.js`, `lab.wasm`.
- Thiết lập:
  - `PlayerSettings.WebGL.compressionFormat = Disabled`: file đọc từ ổ đĩa trong app, không qua mạng nên không cần nén, mở nhanh hơn.
  - `decompressionFallback = false`, `dataCaching = false`, `nameFilesAsHashes = false`, `exceptionSupport = ExplicitlyThrownExceptionsOnly`.
  - Bộ nhớ: khởi đầu 256 MB, tăng dần (Geometric), tối đa 2048 MB.
  - IL2CPP code generation **OptimizeSpeed**, Managed Stripping **Medium**, kèm `Assets/link.xml` giữ các kiểu dùng `JsonUtility` / phản chiếu.
  - Template tối giản (không logo Unity), vì trang tự vẽ màn hình tải.
- Build xong:
  - Chép `Builds/WebGL/lab/` sang **`outputs/app-lab/webgl/`**, giống cách `.app` đang được lấy từ `outputs/app-lab/`.
  - Ghi dung lượng (MB) vào TIEN_DO_APP_LAB.md. Mục tiêu ≤ 120 MB chưa nén; lớn hơn thì ghi lí do.
- Build lại cả **bản macOS** (`BuildMacBatch`) để chắc chắn không hỏng, rồi chép `.app` vào `outputs/app-lab/` như trước.

---

## 2. Electron: phục vụ bản WebGL từ trong app (`desktop/`)
### 2.1 Giao thức `chemlab://`
- File mới **`desktop/lab_webgl.js`** (CommonJS, hàm thuần để test được):
  - `thuMucLabWebGL({ resourcesPath, dirname })`: chọn thư mục theo thứ tự `process.resourcesPath/lab-webgl` (bản đóng gói) → `../outputs/app-lab/webgl` (chạy dev).
  - `giaiDuongDan(goc, urlPath)`: giải đường dẫn, **chặn `..` và đường dẫn tuyệt đối**. Trả `null` nếu ra ngoài thư mục gốc.
  - `kieuMime(ten)`: `.wasm` → `application/wasm`, `.js` → `text/javascript`, `.data` → `application/octet-stream`, `.json` → `application/json`.
  - `thongTinBuild(goc)`: `{ coSan, tep: { loader, data, framework, wasm }, kichThuocMB }`. Chỉ `coSan = true` khi đủ cả 4 file.
- `desktop/main.js`:
  - **Trước** `app.whenReady`: `protocol.registerSchemesAsPrivileged([{ scheme: "chemlab", privileges: { standard: true, secure: true, supportFetchAPI: true, corsEnabled: true, stream: true } }])`.
  - Sau khi ready: `protocol.handle("chemlab", ...)` đọc file qua `lab_webgl.js`, trả `Response` có `Content-Type` đúng và `Access-Control-Allow-Origin: *`. Không có file thì trả 404.
    Cần CORS vì trang được nạp từ `http://127.0.0.1:<port>` (chế độ Trên máy) hoặc từ web thật (chế độ Trực tuyến).
  - `ipcMain.handle("lab:webgl", () => thongTinBuild(...))` trả về thêm `goc: "chemlab://lab/"`.
  - Không đổi `moAppLabUnity` (đang có thay đổi riêng, xem 0.2). Chỉ cần nó trả lỗi rõ ràng khi thiếu app.
- `desktop/preload.js`: thêm `labWebGL: () => ipcRenderer.invoke("lab:webgl")` vào `chemistryApp`. Giữ `contextIsolation`, `sandbox`.
- `desktop/package.json` → `extraResources`: thêm `{ "from": "may_chu/dist/lab-webgl", "to": "lab-webgl" }`.
- `desktop/build-mac.sh`: chép `outputs/app-lab/webgl/` vào `$BUILD_DIR/desktop/may_chu/dist/lab-webgl/`, đặt cạnh đoạn chép `PhongLabHoa3D.app`. Thiếu thì in cảnh báo, không dừng build.
  `desktop/build-win.ps1` sửa tương tự (chưa có module Windows thì chỉ sửa script).

### 2.2 Lưu ý chế độ Trực tuyến
- Ở chế độ Trực tuyến, trang lab tải từ web thật nên **chỉ có code mới khi web đã được deploy**. Trước khi deploy, app ở chế độ này vẫn chạy bản Three.js cũ, và như vậy là chấp nhận được.
- **Không tự deploy.** Ghi trong báo cáo: cần người dùng đồng ý deploy thì chế độ Trực tuyến mới có lab Unity.

---

## 3. Trang Phòng thí nghiệm: chọn bản Unity trong app, web giữ nguyên
### 3.1 Hàm chọn chế độ (thuần, test được)
- File UMD mới `frontend/hoa-hoc/phong-thi-nghiem/chon-che-do-lab.js` (giống kiểu `tinh-toan.js`), hàm
  `chonCheDo({ laApp, build, webgl2, rendererGpu, soNhan, ramGB, uuTien })` trả về `"unity"` hoặc `"nhe"` kèm `lyDo`:
  - Không phải app, hoặc app chưa có hàm `labWebGL` → `"nhe"`, tức đúng hành vi web hiện nay.
  - `build.coSan` sai, không có WebGL2, hoặc `rendererGpu` chứa `SwiftShader` / `llvmpipe` / `Software` → `"nhe"`.
  - `soNhan ≤ 4` hoặc `ramGB ≤ 4` → `"nhe"`. Thiếu số liệu thì không tính là yếu.
  - Người dùng đã chọn `uuTien` (localStorage `ptn-ban-lab` = `"unity"` | `"nhe"`) → theo lựa chọn đó, nhưng `"unity"` vẫn cần có build và WebGL2.
  - Còn lại → `"unity"`.

### 3.2 Module `frontend/hoa-hoc/phong-thi-nghiem/lab-unity-app.js` (ES module)
- Thêm vào importmap của `phong-thi-nghiem.html` là `ptn/lab-unity-app`.
- `giao-dien.js` → `batDau()`:
  - Nạp dữ liệu như cũ, rồi gọi `chonCheDo`.
  - Ra `"nhe"` → chạy **đúng luồng Three.js hiện tại** (không đổi gì).
  - Ra `"unity"` → gọi `LabUnityApp.mo(...)`.
- **Bố cục chế độ Unity:**
  - Ẩn cột trái, cột phải, thanh nút camera, nút Unity web cũ. `#ptn-khung` chiếm hết vùng dưới header (Unity tự có giao diện đầy đủ: bảng thí nghiệm, sổ tay, câu hỏi, bảng tuần hoàn).
  - Header có:
    - Tên "Phòng thí nghiệm 3D".
    - Nút **"⚡ Mở cửa sổ mượt"**.
    - Nút **"Bản nhẹ"**: chuyển sang Three.js và nhớ lựa chọn. Bên Three.js có nút ngược lại **"Bản 3D đầy đủ"** khi điều kiện cho phép.
- **Tải:**
  - Chèn `<script src="chemlab://lab/lab.loader.js">`, rồi `createUnityInstance(canvas, { dataUrl, frameworkUrl, codeUrl, productName, devicePixelRatio: Math.min(devicePixelRatio, 1.5) }, onProgress)`.
  - Hiện tiến độ trên màn tải `#ptn-dang-tai` sẵn có (thanh `.ptn-thanh-tai`).
- **Cầu nối lõi** `window.PTNLoiTrang`:
  - Dùng `M.tao(...)` của `mo-phong.js` và dữ liệu thí nghiệm + `bang-ion` mà trang đã tải. Cài các hàm jslib gọi tới.
  - Chỉ chạy `capNhat(dt)` theo `requestAnimationFrame` khi Unity báo đang tại bàn.
  - Nếu làm được, dùng **chung** các máy trạng thái `U.mp` với `giao-dien.js`, để chuyển "Bản nhẹ" ⇄ Unity không mất tiến trình. Không bắt buộc, nhưng phải ghi rõ trong HANDOFF.
- **Sự kiện từ Unity:**
  - `san_sang`: có `?tn=<id>` thì gửi `MoThiNghiem`. Gửi `DoiGiaoDien` theo sáng/tối. Ẩn màn tải.
  - `tram`: cập nhật `?tn=` trên URL (`history.replaceState`) và biến trạm hiện tại.
  - `fps`: trung bình 10 s < 25 → toast gợi ý (không tự chuyển giữa lúc đang làm thí nghiệm): "Máy đang chậm – thử **Mở cửa sổ mượt** hoặc **Bản nhẹ**".
  - `mo_cua_so_muot` → giống nút ⚡. `thoat` → tắt Unity, về trang chủ app.
- **Nút "⚡ Mở cửa sổ mượt":**
  1. `await unityInstance.Quit()` để giải phóng RAM/GPU (máy chỉ 8 GB, không chạy song song hai bản).
  2. Thay khung bằng thẻ "Phòng lab đang mở ở cửa sổ riêng (mượt hơn)" kèm nút "Chạy lại trong app".
  3. `await chemistryApp.moAppLab(tram)`. Lỗi (thiếu app) → toast + chạy lại bản trong app.
- **Lỗi khi tải** (thiếu file, lỗi biên dịch wasm, hết bộ nhớ, `createUnityInstance` reject):
  - `Quit` nếu đã tạo, rồi **tự về bản Three.js**.
  - Toast nêu lí do ngắn, ghi `console.error` đầy đủ.
- `pagehide` / chuyển trang trong app → `Quit()`.
- Hook cho test: `window.PhongLab.app = { cheDo(), unitySanSang(), tram(), fpsTrungBinh(), thaoTac(id, lenh, json), trangThai(id) }`.
- Chữ lấy từ dữ liệu: `textContent`, không `innerHTML`. CSS mới thêm ở **cuối** `style.css`, tiền tố `ptn-app-`.
- **Web không đổi:** trình duyệt thường không có `window.chemistryApp`, nên mọi thứ chạy như trước. Nút "🖥 Mở bằng ứng dụng" trên web giữ nguyên, mở qua deep link.

---

## 4. Test bắt buộc
- **Node** (`tests/js/`):
  - `lab_chon_che_do.test.mjs`: bảng quyết định của `chonCheDo` gồm không phải app, thiếu build, không WebGL2, SwiftShader, 4 nhân, 4 GB, ưu tiên `nhe`, ưu tiên `unity` khi thiếu build, máy tốt.
  - `desktop_lab_webgl.test.mjs` (`createRequire` nạp `desktop/lab_webgl.js`): chặn `../`, `%2e%2e/`, đường dẫn tuyệt đối; MIME đúng; `thongTinBuild` chỉ `coSan` khi đủ 4 file (dùng thư mục tạm).
- **Pytest:** `tests/test_lab_unity_app.py`:
  - Trang lab có `ptn/lab-unity-app` trong importmap và nạp `chon-che-do-lab.js`.
  - `lab-unity-app.js` chỉ chạy khi có `chemistryApp.labWebGL`.
  - Có xử lý `Quit` trước `moAppLab` và có nhánh tự về Three.js khi lỗi.
  - Test cũ vẫn phải PASS, đặc biệt `test_du_an_unity_co_ma_nguon_va_script_build` (dự án Unity WebGL cũ `unity/PhongThiNghiemHoaHoc` **không được sửa**) và `'"NhanLenh"' in cau-unity.js`.
- **Unity EditMode** – `Assets/Tests/Editor/TestWebGLTuongThich.cs`:
  - Mọi file JSON trong `Assets/Resources/ptn/` đọc được bằng `DocTaiNguyen`.
  - Quét `Assets/Scripts`: `File.ReadAllText`, `new Thread`, `Task.Run`, `Application.Quit` chỉ xuất hiện trong các file được phép, và đều nằm trong khối `#if` loại trừ WebGL.
  - `BuildMayTinh` có `WebGLApp` / `WebGLAppBatch`.
  - `LoiHoaHoc` và `LoiHoaHocTrinhDuyet` đều cài `ILoiHoaHoc`.
- Cuối cùng chạy lại toàn bộ: pytest + `node --test tests/js/` + Unity EditMode, tất cả PASS.

## 5. Nghiệm thu THẬT trong app đã đóng gói (bắt buộc; ảnh phải tự mở ra xem)
> Các lần trước, ảnh "đã kiểm tra" từng bị **trắng hoặc sai** mà vẫn báo đạt. Lần này mỗi ảnh phải kèm số đo chứng minh nó không trống.

- Chạy `desktop/build-mac.sh` → `~/AppBuild/CHEMISTRY/dist/CHEMISTRY.app`. Mở bằng Playwright qua CDP như `desktop/kiem_tra/kiem_tra_app.py`, và **thêm kịch bản** vào file này:
  1. Chế độ **Trên máy** → vào Phòng thí nghiệm.
     - `PhongLab.app.cheDo() === "unity"`, chờ `unitySanSang()` (giới hạn 60 s) và ghi thời gian tải.
     - Chụp khung lab. Tính **độ lệch chuẩn độ sáng điểm ảnh > 12** và **≥ 20 màu khác nhau**, để chứng minh ảnh không trắng/đen trơn.
     - Lưu số đo cạnh ảnh trong `outputs/app-chemistry/lab-unity/`.
  2. `?tn=chuan-do` → Unity mở đúng trạm. Qua `PhongLab.app.thaoTac` chuẩn độ HCl tới khi chuyển hồng: V ≈ 20,0 mL (đọc từ `trangThai`).
  3. Đo `fpsTrungBinh()` trong 15 s khi đi lại và khi tại bàn. Mục tiêu **≥ 45 fps** ở cửa sổ 1440×900 trên M2. Thấp hơn thì ghi số thật, **không làm tròn cho đẹp**.
  4. Bấm **"⚡ Mở cửa sổ mượt"** → có tiến trình `PhongLabHoa3D` chạy, Unity trong app đã `Quit`. Đo RAM các tiến trình CHEMISTRY trước/sau. Rồi bấm "Chạy lại trong app" → chạy lại được.
  5. Đổi tên tạm thư mục `lab-webgl` trong `.app` (sửa trên bản copy) → trang tự về Three.js, không màn hình đen. Đổi lại tên cũ.
  6. Bấm "Bản nhẹ" → Three.js chạy và nhớ lựa chọn khi mở lại. Bấm "Bản 3D đầy đủ" → về Unity.
  7. Gõ chữ vào ô nhập của HTML (ví dụ ô tìm ở trang khác) → Unity không nuốt phím. Esc lần đầu chỉ thả chuột, không mất trạm.
  8. Chế độ sáng/tối: Unity đổi tông theo `DoiGiaoDien`.
  9. Đóng app → không còn tiến trình mồ côi (kiểm tra như kịch bản sẵn có).
- **Web không đổi:** mở `http://127.0.0.1:8000/hoa-hoc/phong-thi-nghiem` bằng Chrome thường → vẫn là bản Three.js như trước, console không lỗi.

## 6. Bàn giao
- Commit theo phần:
  1. Unity tương thích WebGL + test.
  2. Build WebGL + chất lượng WebGL.
  3. Electron `chemlab://` + test.
  4. Trang lab + test.
  5. Đóng gói + nghiệm thu + tài liệu.

  Mỗi lần sửa Unity: chạy `tools/unity_dong_bo.sh day` trước khi commit.
- Cập nhật:
  - **TIEN_DO_APP_LAB.md**: dung lượng build WebGL, thời gian tải, fps.
  - **TIEN_DO_APP_CHEMISTRY.md**.
  - **HANDOFF.md**: ĐÃ XONG / ĐANG LÀM / VIỆC TIẾP THEO / LỖI CÒN TỒN / CÁCH CHẠY + bảng số đo mục 5.
  - **CLAUDE.md**: thêm đoạn ngắn về `chemlab://`, `lab-unity-app.js`, `chon-che-do-lab.js`, `ILoiHoaHoc` / `LoiHoaHocTrinhDuyet`, cách build WebGL.
  - **desktop/README.md**: cách build và cách app chọn bản lab.
- Báo cáo ngắn cho người dùng: app giờ mở lab Unity ngay trong cửa sổ, khi nào tự về bản nhẹ, nút "Mở cửa sổ mượt" làm gì, các số đo thật, chế độ Trực tuyến cần deploy web, việc còn lại.

## 7. Điều kiện "XONG"
- [ ] Trong CHEMISTRY.app (chế độ Trên máy), vào Phòng thí nghiệm là chạy **phòng lab Unity mới** ngay trong cửa sổ. Có ảnh và số đo chứng minh không trống.
- [ ] 5 thí nghiệm làm được trong bản nhúng, kết quả tính toán khớp lõi `mo-phong.js` (chuẩn độ V ≈ 20,0 mL).
- [ ] "⚡ Mở cửa sổ mượt" mở `PhongLabHoa3D.app` đúng trạm và giải phóng bản nhúng. "Chạy lại trong app" hoạt động.
- [ ] Máy yếu, thiếu build hoặc lỗi tải → tự về Three.js. Nút chuyển qua lại "Bản nhẹ" / "Bản 3D đầy đủ" có nhớ lựa chọn.
- [ ] Bản app macOS `PhongLabHoa3D.app` vẫn chạy như trước. Web trong trình duyệt **không đổi**.
- [ ] Mọi test PASS (pytest, node, Unity EditMode). Tài liệu đã cập nhật. Đã commit. **CHƯA push, CHƯA deploy.**
