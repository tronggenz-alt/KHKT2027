# PROMPT – Đóng gói TOÀN BỘ web CHEMISTRY thành MỘT app máy tính (macOS trước, Windows sau)

> Gõ cho Antigravity: **"Đọc và làm theo PROMPT_XUAT_APP_CHEMISTRY.md, chạy liên tục đến hết."**
> Bị ngắt thì gõ: **"Làm tiếp PROMPT_XUAT_APP_CHEMISTRY.md"**. Đọc `TIEN_DO_APP_CHEMISTRY.md` + `HANDOFF.md` rồi làm tiếp đúng chỗ.

## MỤC TIÊU
Một app duy nhất **"CHEMISTRY.app"**. Mở lên là có **mọi thứ người dùng đã làm**:
- Hoá học: giải chuỗi phản ứng, tìm cấu tạo chất, cơ chế phản ứng, mô hình 3D phân tử.
- Sinh học: Lab Sinh học 3D (bản v1 và v2), kiến thức Sinh học, lộ trình học.
- Phòng thí nghiệm Hoá ảo: bản Web 3D, và **app Unity "Phòng Lab Hoá 3D" đóng gói sẵn bên trong**.
- Bảng tuần hoàn, AI Riêng, Cài đặt, Lịch sử.
- Trang Admin: tổng quan, chi phí AI, sơ đồ hệ thống, thứ tự AI, mã/key Claude, email rác, lộ trình…

App chạy được **ngay trên máy, không cần mạng** (trừ các tính năng phải gọi AI), và có thêm **chế độ trực tuyến** để dùng tài khoản trên web.

---

## CÁCH CHẠY LIÊN TỤC
- Làm lần lượt **GĐ1 → GĐ8**, không dừng hỏi giữa chừng.
- Tạo **`TIEN_DO_APP_CHEMISTRY.md`** với checkbox từng mục. Xong mục nào đánh dấu kèm mã commit.
- **Cuối mỗi giai đoạn:** chạy test → `git commit` → cập nhật `HANDOFF.md` + tiến độ → **làm tiếp ngay**.
- **Chỉ dừng khi:**
  - ổ đĩa < 15 GB;
  - cần xoá dữ liệu/file của người dùng;
  - cần push/deploy;
  - cần API key hay mật khẩu (Apple Developer, tài khoản…).

  Việc cần người dùng làm thì dùng phương án dự phòng, ghi vào "VIỆC NGƯỜI DÙNG CẦN LÀM" rồi làm tiếp.
- Lỗi thì tự sửa tối đa 3 lần. Vẫn hỏng thì ghi "LỖI CÒN TỒN", làm phần khác, quay lại trước khi kết thúc.
- **Kiểm chứng bằng mắt:** mọi ảnh chụp kiểm tra phải tự mở xem, và ghi vào HANDOFF một dòng mô tả thật ảnh đó.
  Bài học cũ: ảnh "đã kiểm chứng" từng trống trơn.

## LUẬT CHUNG (giữ nguyên như AGENTS.md)
- Không sửa `.env`. **Không đóng gói `.env`, `data/`, `outputs/`, `logs/`, `.git`, `.venv` của dự án vào app.**
  App phát cho người khác tuyệt đối không được chứa key của chủ app.
- Không sửa vùng Lab Sinh 3D (`frontend/biology-lab-3d*.html`, `frontend/sinh-hoc/`, `backend/sinh_hoc_ai.py`). Chỉ **đóng gói nguyên trạng**.
  Cần sửa vùng này thì gửi đề xuất vào `.agents/hang-doi/sinh-hoc/` cho Claude.
- **Web đang chạy phải giữ nguyên hành vi:** bản trên máy chủ Render và bản chạy bằng `tu-chay.command`. Mọi thay đổi backend phải bật/tắt theo biến môi trường của app, mặc định tắt.
- Không `grep -r` toàn repo (iCloud chậm, `outputs/` rất lớn).
- **Máy người dùng:** Apple M2, RAM 8 GB, ổ đĩa đang chật. Kiểm `df -h ~` trước mỗi bước build lớn.
  Làm việc build ngoài iCloud: `~/AppBuild/CHEMISTRY/` (giống cách đang làm với Unity).

---

## GĐ1. Khảo sát + điểm phục hồi
1. Đọc AGENTS.md, CLAUDE.md (mục Route, Giao diện, Sinh học, Phòng thí nghiệm Hoá ảo), HANDOFF.md.
   Ghi lại kết quả `pytest -q` và `node --test tests/js/`. Commit điểm phục hồi `chore: diem phuc hoi truoc xuat app CHEMISTRY`.
2. **Lập bảng mọi trang/tính năng** cần có trong app vào `desktop/DANH_SACH_TINH_NANG.md`: route, file, cần đăng nhập/admin, có gọi AI không.
   Lấy từ `backend/main.py`, `auth.py`, `quan_tri_web.py` và `frontend/*.html`. Bảng này là "danh sách nghiệm thu" ở GĐ7.
3. Kiểm những thứ phụ thuộc máy:
   - **Font** (`config.py` FONT_*).
   - **Playwright/Chrome** cho chế độ Demo.
   - **RDKit**, **maxminddb**, cơ sở dữ liệu GeoIP nếu có.
   - Thư mục ghi dữ liệu (`STORAGE_DIR`).
   - Cookie `COOKIE_SECURE`.
   - Đăng nhập Google OAuth.
   - Cloudflare tunnel và sao lưu GitHub (`sao_luu.py`): trong app phải **tắt**.

## GĐ2. Kiến trúc app (Electron + máy chủ Python đóng gói sẵn)
```
desktop/
  package.json            electron, electron-builder (khoá phiên bản chính xác)
  main.js                 cửa sổ, menu, khởi động/tắt máy chủ, chế độ Trên máy / Trực tuyến, deep link
  preload.js              cầu nối an toàn (contextIsolation) – chỉ lộ vài hàm: moAppLab(), cheDo(), phienBan()
  may_chu/khoi_dong.py    chạy uvicorn backend.main:app ở cổng trống 127.0.0.1, ghi cổng ra stdout
  may_chu/chemistry.spec  PyInstaller (onedir) – gói backend + frontend + config + knowledge
  build-mac.sh            build toàn bộ: PyInstaller → copy app Unity → electron-builder → ký ad-hoc → zip/dmg
  README.md
```
- **Vỏ app: Electron.** Dùng Chromium giống hệt Chrome nơi web đã được test, nên Three.js, WebGL và pointer lock chạy giống web.
  Bảo mật:
  - `contextIsolation: true`, `nodeIntegration: false`, `sandbox: true`.
  - Chỉ cho điều hướng trong `http://127.0.0.1:<cổng>` (Trên máy) hoặc tên miền web (Trực tuyến). Link ngoài mở bằng trình duyệt mặc định.
- **Máy chủ Python đóng gói bằng PyInstaller:**
  - Gói kèm RDKit, Pillow, fonttools…; bỏ `pytest`, `rich` khỏi bản đóng gói.
  - Mở app thì `main.js` chạy máy chủ, chờ `/api/suc-khoe` trả OK (màn hình chờ có logo + thanh tiến độ), rồi mới tải trang.
  - Đóng app thì tắt hẳn máy chủ, không để tiến trình mồ côi.
  - Mở app lần 2 thì dùng lại cửa sổ cũ (single instance).
- **Dữ liệu người dùng** ở `~/Library/Application Support/CHEMISTRY/` (Windows: `%APPDATA%\CHEMISTRY`), đặt qua `STORAGE_DIR`. Trong đó có `data/app.db`, `outputs/`, `logs/`.
  Menu **"Nhập dữ liệu từ dự án…"**: **chép** (không di chuyển) `data/app.db` của thư mục dự án vào đây, có hỏi xác nhận.
- **Biến môi trường khi app chạy máy chủ:**
  - `CHE_DO_APP=1` (mới; backend dùng để bật các nhánh riêng của app).
  - `CHE_DO_CUC_BO=1`: người dùng app là chủ dữ liệu trên máy mình.
  - `COOKIE_SECURE=0` (http://127.0.0.1).
  - Tắt sao lưu GitHub, tunnel, các tác vụ nền chỉ dành cho máy chủ.

  Thêm vào `backend/config.py` theo đúng mẫu đọc biến sẵn có. Mặc định **tắt**, nên web không đổi.
- **Key AI không đóng gói.** Màn hình **Cài đặt app → Khoá AI**: người dùng tự dán key Gemini/Claude/OpenAI.
  - Lưu bằng `safeStorage` của Electron (Keychain), truyền cho máy chủ qua biến môi trường khi khởi động.
  - Chưa có key thì các tính năng AI hiện thông báo rõ: "Thêm key trong Cài đặt app" hoặc "Chuyển sang chế độ Trực tuyến".
  - Tính năng Key Gemini cá nhân (BYOK) sẵn có vẫn dùng được.

## GĐ3. Hai chế độ: Trên máy / Trực tuyến
- **Trên máy (mặc định):** mọi trang chạy từ máy chủ cục bộ, không cần mạng (trừ gọi AI, tra tài liệu khoa học, tải CDN).
- **Không phụ thuộc CDN khi Trên máy:** web đang tải three.js, gsap, Tailwind, MathJax, 3Dmol, font… qua CDN.
  - Khi `CHE_DO_APP=1`, `_trang()` thay URL CDN bằng bản **đã tải sẵn trong `frontend/vendor/`**, đúng phiên bản đang dùng.
  - Viết `tools/tai_vendor.py` để tải và ghi `vendor/PHIEN_BAN.json` có sha256.
  - Web trên máy chủ vẫn dùng CDN như cũ.
  - Riêng file Lab Sinh 3D: gửi đề xuất cho Claude nếu cần đổi đường dẫn trong file đó. Không làm kịp thì để CDN cho riêng trang này và ghi chú.
- **Trực tuyến:** cửa sổ tải tên miền web thật (lấy từ cài đặt app, mặc định địa chỉ Render/tên miền trong `HUONG_DAN_DUA_LEN_WEB.md`), dùng tài khoản và AI của máy chủ.
  - **Đăng nhập Google không chạy trong cửa sổ nhúng** (Google chặn).
  - Mở trang đăng nhập bằng **trình duyệt mặc định**. Đăng nhập xong, web cấp **mã một lần** (sống 2 phút, dùng 1 lần, gắn với user) và chuyển về `chemistry://dang-nhap?ma=...`.
  - App đổi mã lấy cookie phiên.
  - Thêm route `POST /api/app/ma-dang-nhap` và `POST /api/app/doi-ma`, kèm test: hết hạn, dùng lại, sai mã, rate limit.
- Menu **Chế độ** đổi qua lại; nhớ lựa chọn.

## GĐ4. Gộp app Unity "Phòng Lab Hoá 3D" vào trong
- `build-mac.sh` chép `PhongLabHoa3D.app` (bản build mới nhất ở `~/UnityProjects/PhongLabHoa3D/Builds/macOS/`) vào `CHEMISTRY.app/Contents/Resources/PhongLabHoa3D.app`.
- Trên trang Phòng thí nghiệm (khi `CHE_DO_APP=1`), nút **"Mở bằng ứng dụng"** gọi `window.chemistryApp.moAppLab(tn)` → `main.js` mở app Unity đi kèm với tham số `-tn <id>` (hoặc deep link `phonglabhoa://mo?tn=`).
  Không cần tải thêm gì.
- Menu **Phòng Lab 3D** mở thẳng app Unity.

## GĐ5. Hoàn thiện như app thật
- **Menu macOS:** CHEMISTRY (Giới thiệu, Cài đặt app ⌘,, Thoát) · Tệp (Nhập dữ liệu từ dự án, Mở thư mục dữ liệu) · Đi tới (Trang chủ, Giải chuỗi, Cơ chế, 3D, Sinh học 3D, Phòng thí nghiệm, Bảng tuần hoàn, Admin) · Chế độ · Cửa sổ · Trợ giúp.
- **Giới thiệu:** phiên bản, ghi công tài nguyên (BodyParts3D CC BY, mô hình Sketchfab CC BY, PubChem, font OFL…). Lấy từ các file `GHI_CONG.md` sẵn có.
- **Icon app:** bình tam giác theo logo web, đủ cỡ `.icns` (và `.ico` cho Windows).
- **Cửa sổ:** nhớ kích thước/vị trí; mở link ngoài bằng trình duyệt.
- **Tải file** (ZIP kết quả, PNG, CDXML, .mol): hộp lưu file của hệ điều hành.
- **Thông báo hệ thống** khi một lượt giải chuỗi chạy nền xong (nếu cửa sổ không ở trước).
- **Chế độ Demo cần Chrome:**
  - Có Google Chrome trên máy thì dùng `channel="chrome"` của Playwright.
  - Không có thì ẩn Demo và ghi lí do. **Không** đóng gói trình duyệt Playwright (quá nặng).
- **Nhật kí** của máy chủ ghi vào thư mục dữ liệu, menu Trợ giúp → "Mở nhật kí".

## GĐ6. Build + ký
- `desktop/build-mac.sh` (chạy được lại từ đầu):
  1. Tạo venv build riêng ngoài iCloud.
  2. PyInstaller.
  3. Tải vendor.
  4. Chép frontend/config/knowledge.
  5. Chép app Unity.
  6. electron-builder (arm64 + x64 hoặc universal).
  7. Ký ad-hoc `codesign --force --deep -s -`.
  8. Ra `CHEMISTRY-<phiên bản>-mac.dmg` và `.zip`.
- Ghi dung lượng từng phần vào HANDOFF. Mục tiêu: dmg ≤ 900 MB (đã gồm app Unity). Nặng hơn thì cắt bớt: bỏ test, tài liệu, file `.pyc` thừa, gói Python không dùng.
- Ghi vào README cách mở lần đầu: chuột phải → Mở, vì chưa notarize. Notarize cần tài khoản Apple Developer (99 USD/năm), **người dùng quyết định**.
- Có `build-win.ps1` cho Windows, chạy trên máy Windows sau, kèm hướng dẫn. Chưa build được trên Mac thì ghi rõ.
- **Không đưa file cài vào repo.** Để ở `~/AppBuild/CHEMISTRY/dist/`.

## GĐ7. Nghiệm thu bằng app thật (bắt buộc)
1. **Script tự động** `desktop/kiem_tra/kiem_tra_app.py` dùng **Playwright cho Electron** (`playwright._impl… electron.launch` / `_electron.launch`) mở `CHEMISTRY.app` đã build.
   - Đi qua **mọi trang** trong `DANH_SACH_TINH_NANG.md` ở chế độ Trên máy, ở cả sáng và tối.
   - Mỗi trang: chụp ảnh, đọc console (không có lỗi), kiểm tra phần tử chính có hiện.
   - Các tính năng AI: chạy với **AI giả** (bật bằng biến môi trường chỉ dành cho test, giống conftest đang làm) để không tốn tiền và không cần key.
   - Ghi ảnh vào `outputs/app-chemistry/`.
2. **Danh sách nghiệm thu tối thiểu:**
   - Trang chủ; Giải chuỗi ra sơ đồ 5–6 chất/hàng + tải ZIP; Tìm cấu tạo; Cơ chế có nút Giải thích; 3D phân tử xoay được.
   - Sinh học 3D v1 và v2 hiện cơ thể và xoay được; nút chuyển Hoá ⇄ Sinh.
   - Phòng thí nghiệm Web 3D làm trọn chuẩn độ V ≈ 20,0 mL; nút "Mở bằng ứng dụng" mở được app Unity đi kèm.
   - Bảng tuần hoàn; Cài đặt; Lịch sử; AI Riêng.
   - Admin: các thẻ mở được.
3. **Tắt mạng** (tắt Wi-Fi hoặc chặn bằng biến test) rồi chạy lại: mọi trang không gọi AI vẫn hiện đủ, không vỡ giao diện vì thiếu CDN.
4. **Chế độ Trực tuyến:** mở được web thật. Luồng đổi mã đăng nhập chạy đúng: test tự động phía backend, còn phần trình duyệt mô tả thao tác tay trong README.
5. **Đo:**
   - Thời gian mở app tới trang chủ: mục tiêu ≤ 8 s.
   - RAM khi rảnh.
   - Kiểm tra không còn tiến trình Python sau khi thoát.
6. **Tự mở xem từng ảnh**, mô tả vào HANDOFF. Ảnh nào sai thì sửa rồi chạy lại.
7. `pytest -q`, `node --test tests/js/`, test EditMode Unity: PASS hết. **Web trên máy chủ không đổi hành vi** (test cũ PASS).

## GĐ8. Bàn giao
- `desktop/README.md`: cách build, cài, mở lần đầu, nhập dữ liệu, thêm key AI, chế độ Trực tuyến, gỡ cài đặt.
- Cập nhật **HANDOFF.md** và **CLAUDE.md** (mục mới "## App máy tính CHEMISTRY": thư mục `desktop/`, biến `CHE_DO_APP`, route `/api/app/*`, cách build, test).
- Commit theo giai đoạn. **Chưa push/deploy, chưa đưa file cài lên mạng.**
- **Báo cáo ngắn cho người dùng:** file cài ở đâu, dung lượng, cách mở, tính năng nào cần key AI, việc người dùng cần làm.

## VIỆC NGƯỜI DÙNG CẦN LÀM (ghi vào HANDOFF, không tự làm thay)
1. Dọn ổ đĩa nếu còn dưới 20 GB: build Electron + PyInstaller + Unity cần khoảng 5–8 GB tạm.
2. Thêm key AI vào **Cài đặt app** sau khi cài, hoặc dùng chế độ Trực tuyến.
3. Muốn đăng nhập Google trong chế độ Trực tuyến: thêm `chemistry://dang-nhap` vào cấu hình OAuth nếu Google Cloud Console yêu cầu. Gemini ghi đúng các bước.
4. Quyết định có mua Apple Developer để notarize (để người khác mở app không bị cảnh báo) và nơi đặt file cài (GitHub Releases / Google Drive).

## ĐIỀU KIỆN XONG
- [ ] Có `CHEMISTRY.app` (dmg/zip). Mở là vào trang chủ, có đủ mọi tính năng trong `DANH_SACH_TINH_NANG.md`, kể cả Lab Sinh 3D, Phòng thí nghiệm và app Unity đi kèm.
- [ ] Chạy được khi không có mạng (trừ AI). Không chứa `.env` hay dữ liệu của chủ app. Key AI lưu trong Keychain.
- [ ] Có chế độ Trực tuyến, đăng nhập qua trình duyệt bằng mã một lần, có test.
- [ ] Script nghiệm thu PASS. Ảnh đã được mở xem và mô tả đúng. Mọi test cũ PASS. Web trên máy chủ không bị ảnh hưởng.
- [ ] README, HANDOFF, CLAUDE.md cập nhật. Đã commit. Chưa push hoặc deploy.
