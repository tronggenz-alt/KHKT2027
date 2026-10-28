> ⛔ **ĐÃ GỘP vào `PROMPT_LAB_UNITY_TRON_GOI.md` (24/10/2026). KHÔNG chạy file này** – chỉ giữ lại để tham khảo.

# PROMPT – BƯỚC 1: Xuất Phòng thí nghiệm Hoá ảo thành APP RIÊNG trên máy tính (Unity), làm đẹp phòng để sau

> Dán nguyên file này cho Antigravity, hoặc gõ: "Đọc và làm theo PROMPT_APP_LAB_MAY_TINH.md".
> Người dùng muốn **có app riêng chạy trên máy tính trước**. Việc chỉnh phòng thí nghiệm cho đẹp (toà nhà lớn, trần cao, đồ hoạ chân thật, bảng tuần hoàn mới, dụng cụ chuẩn kích thước) sẽ làm ở **bước sau**, theo `PROMPT_UNG_DUNG_LAB_UNITY.md`.
> Vì vậy bước này ưu tiên **chạy được, mượt, đúng kết quả, đóng gói thành app**, không cần đẹp. Nhưng phải dựng **đúng nền móng** để bước sau làm tiếp trên cùng dự án, không phải đập đi làm lại.

---

## DÀN Ý NHANH
1. Tạo dự án Unity mới `unity/PhongLabHoa3D` (app máy tính). Bản làm việc đặt ngoài iCloud.
2. App tự chạy **lõi hoá học của web** (`tinh-toan.js` + `mo-phong.js`) bằng **Jint** trên một luồng CPU riêng. Không phụ thuộc trang web, chạy được khi không có mạng.
3. Nội dung = **phòng thí nghiệm hiện tại**:
   - Dựng căn phòng 22 × 16 m từ `ban-do.json`, 5 trạm thí nghiệm.
   - Dụng cụ dùng lại 5 cảnh Unity đã có ở `unity/PhongThiNghiemHoaHoc/Assets/Scripts/Canh/`.
   - Thêm nhân vật đi lại (điều khiển kiểu Genshin cơ bản) và bảng điều khiển thí nghiệm trong app.
4. Đóng gói **`PhongLabHoa3D.app`** cho macOS. Trên web có nút "Mở bằng ứng dụng" (deep link `phonglabhoa://`).
5. Có chế độ **tự kiểm tra** (chụp ảnh + đo fps + chạy kịch bản 5 thí nghiệm) để chứng minh app chạy đúng.

---

## 0. Việc phải làm trước khi viết code
1. Đọc **AGENTS.md**, **CLAUDE.md**, **HANDOFF.md**, `unity/PhongThiNghiemHoaHoc/README.md`.
   Đọc **mục 0, 1, 2 của `PROMPT_UNG_DUNG_LAB_UNITY.md`** (kiến trúc chung phải theo đúng, để bước sau làm tiếp).
2. **Máy người dùng:**
   - Phần cứng: Apple M2, RAM 8 GB, ổ còn khoảng 21 GB (đã dùng 90%).
   - Unity **6000.6.7f1** đang nằm ở `/Users/trantrong/Documents/6000.6.7f1/Unity.app` (trong iCloud), có module **MacStandaloneSupport + WebGLSupport**.
   - Chạy `df -h ~`. **Dưới 15 GB trống thì DỪNG** và báo người dùng dọn ổ. Không tự xoá gì.
3. **Không để thư mục làm việc Unity trong iCloud:**
   - Bản làm việc ở `~/UnityProjects/PhongLabHoa3D` (chứa `Library/`, `Builds/`, `Assets/TaiNguyenNgoai/`).
   - Nguồn để commit ở `unity/PhongLabHoa3D/` gồm `Assets/` (trừ `TaiNguyenNgoai`), `Packages/`, `ProjectSettings/`.
   - Viết `tools/unity_dong_bo.sh keo|day` (rsync, loại trừ `Library Temp Logs Builds UserSettings Assets/TaiNguyenNgoai`). **Luôn `day` trước khi commit.**
4. Kết quả ban đầu của `.venv/bin/python -m pytest -q` và `node --test tests/js/`. Điểm phục hồi: `git add -A && git commit -m "chore: diem phuc hoi truoc app lab may tinh"`.
5. Giữ nguyên các phần sau:
   - Dự án WebGL cũ `unity/PhongThiNghiemHoaHoc/`: chỉ **chép** script sang dự án mới, không sửa bản gốc. Test `test_du_an_unity_co_ma_nguon_va_script_build` phải còn PASS.
   - Bản Web 3D, lõi `tinh-toan.js` / `mo-phong.js`: không sửa logic.
   - Luật cũ: không sửa vùng Lab Sinh, không sửa `.env`, không push/deploy khi chưa được đồng ý, không `grep -r` toàn repo.
6. **Không đăng nhập tài khoản thay người dùng** (Adobe/Mixamo, Sketchfab, Unity Asset Store, Apple Developer). Thiếu thứ gì cần đăng nhập thì dùng phương án dự phòng và ghi vào mục "VIỆC NGƯỜI DÙNG CẦN LÀM".

## 1. Dự án + lõi hoá học (làm đầu tiên)
1. Tạo `~/UnityProjects/PhongLabHoa3D` bằng Unity 6000.6.7f1, template **URP**.
   Gói: `render-pipelines.universal`, `cinemachine` 3.x, `inputsystem`, `ai.navigation`, `test-framework`, `burst`, `collections`.
   Scripting backend **Mono**, Graphics API **Metal**, **Graphics Jobs bật**.
2. **Jint** (NuGet, BSD-2) + phụ thuộc vào `Assets/Plugins/Jint/`, chỉ bật cho Editor + Standalone.
   `Assets/Scripts/Loi/LoiHoaHoc.cs`:
   - Chạy `tinh-toan.js` rồi `mo-phong.js`, giả lập `module.exports` cho kiểu UMD.
   - Các hàm `Tao(id)`, `ThaoTac(lenh, thamSoJson)`, `CapNhat(dt)`, `TrangThai()`. Nhận sự kiện `trang_thai`, `so_tay`, `hieu_ung`.
3. **Luồng mô phỏng riêng** (dùng nhân CPU khác, luồng chính không bao giờ chờ JS):
   - Mỗi luồng một `Engine`, `CapNhat` 30 lần/giây.
   - Hàng đợi lệnh vào thread-safe, ảnh chụp trạng thái mới nhất trả ra luồng chính.
   - Chỉ thí nghiệm đang làm mới chạy thời gian. Rời bàn thì tạm dừng, giống bản web.
4. **Đồng bộ dữ liệu từ web:** Editor menu "Phòng Lab → Đồng bộ dữ liệu từ web" chép `frontend/hoa-hoc/phong-thi-nghiem/{tinh-toan.js, mo-phong.js, bang-ion.json, ban-do.json, thi-nghiem/*.json}` vào `Assets/StreamingAssets/ptn/`.
   Build tự gọi bước này trước. Có test so sha256 với bản gốc.
5. **Phương án dự phòng:** chỉ khi Jint không chạy được `mo-phong.js` thì mới port sang C#, kèm bộ véc-tơ kiểm thử chạy song song trên Node và C# để so từng số. Ghi lí do vào HANDOFF.md.
6. **Test EditMode** (`Assets/Tests/EditMode/`), giá trị khớp `tests/js/`:
   - pH HCl 0,1 M (V = 0 / 10 / 19,9 / 20 / 20,1 / 30 mL → 1,00 / 1,48 / 3,60 / 7,00 / 10,40 / 12,30).
   - CH₃COOH (V = 0 / 10 / 20 mL → 2,88 / 4,76 / 8,73).
   - Điện phân 2 A 965 s → 0,64 g.
   - Pin Zn – Cu 1,103 V.
   - Chuẩn độ HCl + phenolphthalein chuyển hồng ở V ≈ 20,0 mL.

   Chạy: `Unity -batchmode -projectPath ~/UnityProjects/PhongLabHoa3D -runTests -testPlatform EditMode -testResults logs/unity-editmode.xml`.

## 2. Phòng + 5 trạm (dùng lại cái đã có, chưa cần đẹp)
1. **Chép** từ `unity/PhongThiNghiemHoaHoc/Assets/Scripts/` sang `Assets/Scripts/ThiNghiem/`: `LabHinh.cs`, `LabVatLieu.cs`, `LabTrangThai.cs`, `Canh/*.cs`.
   - Thay đường gửi thao tác qua `.jslib` (`LabBridge.GuiThaoTac` / `PTN_GuiSuKien`) bằng một interface C# `ICauNoiLoi`, nối thẳng vào `LoiHoaHoc`.
   - Giữ API cảnh `Dung(goc, …, tt)` / `CapNhat(json)` / `HieuUng(e)` / `Tick(dt)`.
2. **Căn phòng** dựng bằng code từ `ban-do.json` (giống bản web: 22 × 16 m, 5 khu, đồ nội thất dạng hộp):
   - Sàn có dải màu khu, tường, cửa sổ, trần, biển tên khu, 5 bàn.
   - Đặt 5 cảnh lên đúng 5 bàn **cùng lúc**, mỗi cảnh một `Transform` theo `tam`, `huong`, `cao_ban`.
   - Ánh sáng realtime đơn giản (1 nắng + đèn môi trường) là đủ. Làm đẹp để bước sau.
   - **Bảng tuần hoàn giả hiện tại thì BỎ** (không chép bảng ô màu sai). Để trống chỗ trên tường; bước sau sẽ vẽ bảng thật.
3. **NavMesh** (AI Navigation) phủ sàn, mọi bàn tới được. Có test.

## 3. Nhân vật + điều khiển kiểu Genshin (bản cơ bản)
1. **Nhân vật:**
   - Người dùng muốn **một giáo sư mặc áo blouse trắng**. Nguồn miễn phí đã tìm được (giấy phép **CC BY**: dùng được, phải ghi tên tác giả trong app):
     - https://sketchfab.com/3d-models/modern-scientist-in-a-lab-coat-e835a3b126a4463583727dbac2fedecc ("Modern Scientist in a Lab Coat", tác giả restore50, khoảng 215k tam giác, **chưa có khung xương**).
     - https://sketchfab.com/3d-models/casual-scientist-in-a-lab-coat-59db594edf914c62827150c307e162e4 ("Casual Scientist In A Lab Coat", cùng tác giả).
   - Người dùng tự tải (cần tài khoản Sketchfab), sau đó **gắn khung xương miễn phí bằng Mixamo** (Upload Character → đặt điểm cằm/cổ tay/khuỷu/gối/hông) và tải kèm các động tác.
     File để ở **`~/Downloads/Mixamo/`**. Có file ở đó thì chuyển vào `Assets/TaiNguyenNgoai/NhanVat/`.
     - Import rig **Humanoid**.
     - Giảm còn ≤ 60k tam giác bằng LOD/giảm lưới nếu cần.
     - Ghi công vào `GHI_CONG.md` và màn hình "Giới thiệu" trong app: *"Modern Scientist in a Lab Coat" by restore50, CC BY 4.0, Sketchfab*.
   - **Chưa có file:** dùng nhân vật tạm dựng bằng code (thân, đầu, tay chân, áo blouse trắng, kính) có hoạt hình đi bằng code, để vẫn chạy và test được. Khi người dùng thêm file thì chỉ cần đổi một prefab.
2. **Điều khiển** (Input System):
   - Bấm vào khung → **khoá chuột, di chuột để xoay camera**; giữ **Alt** để hiện chuột.
   - WASD đi theo hướng camera, nhân vật xoay mặt mượt. **Shift** chạy, **Space** nhảy, lăn chuột để thu/phóng, **F** góc nhìn 1 ⇄ 3.
   - **E** vào bàn, **Q** rời bàn, **M** bản đồ, **Esc** menu.
   - Bỏ qua phím khi đang gõ trong ô nhập.
3. **Camera (Cinemachine 3):** góc 3 dùng `CinemachineOrbitalFollow` + `CinemachineRotationComposer` + `CinemachineDeoccluder` (không xuyên tường). Góc 1 đặt ở mắt.
4. **Vào bàn:** đứng trước bàn 0,6 s hoặc nhấn E → nhả chuột → nhân vật tới chỗ đứng → **blend camera 1 s** vào cận cảnh bàn → bảng thí nghiệm hiện ra. Q để quay lại.

## 4. Giao diện trong app (UI Toolkit, đủ dùng, chưa cần cầu kì)
- Màu theo web: sáng = Icy blue `#eef4fa` + Midnight Navy `#0f2b48`; tối = `#0f2438` + Sky Blue `#38bdf8`. Font **Be Vietnam Pro** + **JetBrains Mono** (OFL, được nhúng).
- **Menu chính:** Vào phòng lab · Chọn thí nghiệm (dịch chuyển thẳng tới bàn) · Cài đặt · Giới thiệu (ghi công) · Thoát.
- **HUD khi đi:** tên khu, bản đồ nhỏ, gợi ý "E – Vào bàn …".
- **Bảng thí nghiệm khi tại bàn:**
  - **Điều khiển:** làm lại đủ nút của từng thí nghiệm theo `BANG[id]` trong `frontend/hoa-hoc/phong-thi-nghiem/giao-dien.js`, gửi đúng lệnh `thaoTac` như web.
  - **Bước hướng dẫn**, **Số liệu** (V, pH, E, m…), **Sổ tay** tự ghi.
  - Tab Câu hỏi để bước sau.
  - Chế độ Hướng dẫn / Tự do.
- **Cài đặt:** chất lượng Thấp / Vừa / Cao / Tự động, độ phân giải, giới hạn fps, VSync, độ nhạy chuột, đảo trục Y, hiện fps. Lưu bằng `PlayerPrefs`.

## 5. Hiệu năng (lí do làm app)
- URP: Forward+, SRP Batcher, **GPU Resident Drawer**, MSAA 4×. Chất lượng Thấp bật **STP** để giảm tải.
- Lõi hoá học ở luồng riêng. Hạt (bọt khí, kết tủa) giới hạn số lượng theo mức chất lượng.
- **Mục tiêu trên máy người dùng (M2 8 GB, 1920×1200, chất lượng Cao):**
  - ≥ 60 fps, 1% low ≥ 45.
  - Luồng chính ≤ 8 ms.
  - RAM app ≤ 1,5 GB.
  - Mở app ≤ 10 s.
  - Bản build ≤ 400 MB.

## 6. Đóng gói app + nối với web
1. **`Assets/Editor/BuildMayTinh.cs`** → `BuildMayTinh.Mac()`:
   - Build ra `~/UnityProjects/PhongLabHoa3D/Builds/macOS/PhongLabHoa3D.app` (Apple Silicon + Intel), tên hiển thị "Phòng Lab Hoá 3D", icon lấy từ biểu tượng bình tam giác của web.
   - Ký ad-hoc: `codesign --force --deep -s - PhongLabHoa3D.app`.
   - Nén thành `PhongLabHoa3D-mac.zip` cạnh đó.
   - Chạy build bằng dòng lệnh `-batchmode -executeMethod BuildMayTinh.Mac`. Ghi lệnh vào README.
   - Có thêm `BuildMayTinh.Windows()`, nhưng chỉ chạy khi đã cài module Windows; chưa có thì báo, không lỗi.
2. **Deep link `phonglabhoa://mo?tn=<id>`:**
   - Khai báo ở Player Settings → macOS → Supported URL schemes. Nhận bằng `Application.deepLinkActivated` / `Application.absoluteURL`.
   - `tn` hợp lệ (1 trong 5 id) thì mở thẳng bàn đó. Tham số lạ bỏ qua. Có test cho parser.
3. **Web** (giữ nguyên bản Web 3D):
   - **Admin:** `app_settings` khoá `"ung_dung_lab"` = `{bat: false, url_mac: "", url_win: "", phien_ban: ""}`.
     - `GET/POST /admin/api/ung-dung-lab`: chỉ admin, CSRF, ghi nhật kí.
     - Khối cài đặt đặt trong thẻ "Phòng thí nghiệm" ở `/admin`, làm theo mẫu công tắc Unity có sẵn trong `backend/phong_thi_nghiem.py`.
   - **API người dùng:** `GET /api/phong-thi-nghiem/ung-dung` (đã đăng nhập) → `{bat, url_mac, url_win, phien_ban}`. Chỉ lộ link khi `bat`.
   - **Trang lab:** nút "🖥 Mở bằng ứng dụng (mượt hơn)" hiện khi `bat` và máy là macOS/Windows.
     - Bấm thì thử mở `phonglabhoa://mo?tn=<id đang mở>`.
     - Sau 1,5 s trang vẫn còn hiển thị thì hiện hộp "Chưa cài ứng dụng?": link tải + 3 bước cài (giải nén → kéo vào Applications → lần đầu chuột phải chọn Mở).
   - File app **không để trong repo** (nặng). Người dùng chọn nơi để (GitHub Releases / Google Drive) rồi dán link ở `/admin`.
   - **Test** `tests/test_ung_dung_lab.py`:
     - Mặc định tắt.
     - Học sinh gọi API admin → 403; chưa đăng nhập → 401; thiếu CSRF → 403.
     - Đang tắt thì không lộ URL; admin bật thì học sinh thấy.

## 7. Tự kiểm tra + kiểm chứng bằng mắt (bắt buộc)
1. Chạy `PhongLabHoa3D.app/Contents/MacOS/PhongLabHoa3D -tu-kiem-tra outputs/app-lab/`. App tự làm lần lượt:
   1. Mở phòng ở cửa, chụp ảnh góc 3 (**phải thấy nhân vật**).
   2. Đi tới từng bàn bằng NavMesh, vào bàn, chụp cận cảnh.
   3. Chạy kịch bản qua lõi:
      - chuẩn độ HCl chuyển hồng, V ≈ 20,0 mL;
      - nhận biết đúng 4 ống;
      - pin Zn – Cu 1,10 V, rút cầu muối về 0 V;
      - điện phân 2 A 965 s → 0,64 g.
   4. Chụp góc 1, bản đồ, menu.
   5. Ghi `bao-cao.json` (fps trung bình, 1% low, ms luồng chính, RAM, thời gian mở app, kết quả từng kịch bản) rồi tự thoát.
2. Thử deep link: `open "phonglabhoa://mo?tn=dien-phan"` → app mở đúng bàn điện phân.
3. **Tự mở từng ảnh ra xem.** Ghi vào HANDOFF.md một dòng mô tả thật mỗi ảnh. Ảnh nào không thấy đúng thứ cần thấy thì phần đó **chưa xong**.
   Lần trước ảnh "toàn cảnh" của bản web chỉ là màu nền; không được lặp lại.
4. Cuối cùng: `pytest -q`, `node --test tests/js/`, test EditMode Unity đều PASS.

## 8. Bàn giao
- Commit theo phần:
  1. Dự án + Jint + test.
  2. Phòng + 5 trạm.
  3. Nhân vật + điều khiển.
  4. Giao diện.
  5. Build + deep link + web + test.
  6. Tự kiểm tra + tài liệu.

  Mỗi lần: `tools/unity_dong_bo.sh day` → test → commit.
- `unity/PhongLabHoa3D/README.md`: cách mở dự án, đồng bộ, build, chạy tự kiểm tra, deep link.
- Cập nhật **HANDOFF.md** (ĐÃ XONG / ĐANG LÀM / VIỆC TIẾP THEO / LỖI CÒN TỒN / CÁCH CHẠY + số fps + mô tả ảnh).
  Sửa đoạn "## Phòng thí nghiệm Hoá ảo" trong **CLAUDE.md**: app máy tính, khoá `app_settings` `ung_dung_lab`, route mới, test mới.
- Ghi rõ trong HANDOFF: **bước tiếp theo** là `PROMPT_UNG_DUNG_LAB_UNITY.md` (toà nhà nhiều phòng trần cao, đồ hoạ chân thật, bảng tuần hoàn 118 nguyên tố, dụng cụ đúng kích thước).
  Các phần A (lõi + Jint) và H (build + liên kết web) của prompt đó **đã làm ở bước này**, chỉ cần kiểm tra lại.
- **Chưa push hoặc deploy.**

## 9. VIỆC NGƯỜI DÙNG CẦN LÀM (nhắc trong báo cáo; Gemini không tự làm thay)
1. Dọn ổ để còn ≥ 20 GB trống.
2. (Nên làm) Cài lại Unity 6000.6.7f1 bằng Unity Hub vào `/Applications/Unity/Hub/Editor`, ra khỏi iCloud.
3. Nhân vật giáo sư:
   1. Đăng nhập Sketchfab, tải model ở mục 3 (định dạng FBX hoặc OBJ).
   2. Đăng nhập mixamo.com → **Upload Character** → đặt điểm khớp → tải bản *FBX for Unity, With Skin, T-pose*.
   3. Tải các động tác *Idle, Walking, Running, Jump, Falling Idle, Landing*: chọn *Without Skin, 30 fps*; riêng đi và chạy tích **In Place**.
   4. Để tất cả vào `~/Downloads/Mixamo/`.
4. Chọn nơi để file cài app (GitHub Releases hoặc Google Drive) rồi dán link ở `/admin`.

## 10. Điều kiện "XONG"
- [ ] Có `PhongLabHoa3D.app` mở được bằng tay và bằng nút trên web (deep link), chạy không cần mạng.
- [ ] Trong app có phòng 22 × 16 m, 5 bàn, nhân vật (giáo sư nếu người dùng đã tải, nếu chưa thì nhân vật tạm mặc blouse), đi lại, xoay camera bằng chuột, vào/rời bàn mượt.
- [ ] Làm trọn 5 thí nghiệm trong app với **cùng kết quả số** như bản web. Test EditMode PASS.
- [ ] Đạt mục tiêu hiệu năng mục 5, có `bao-cao.json`. Ảnh tự kiểm tra đã được mở xem và mô tả đúng.
- [ ] Web có nút mở app + cài đặt link tải trong admin + test. Bản Web 3D và Unity WebGL cũ vẫn chạy, mọi test cũ PASS.
- [ ] Tài liệu + HANDOFF + CLAUDE.md cập nhật. Đã commit. **Chưa push hoặc deploy.**
