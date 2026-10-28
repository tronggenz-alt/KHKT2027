# PROMPT TRỌN GÓI – App máy tính "Phòng Lab Hoá 3D" (Unity): xuất app → làm đẹp → hoàn thiện. CHẠY LIÊN TỤC TỪ ĐẦU ĐẾN CUỐI

> Dán nguyên file này cho Antigravity, hoặc gõ: **"Đọc và làm theo PROMPT_LAB_UNITY_TRON_GOI.md, chạy liên tục đến hết."**
> Bị ngắt giữa chừng thì gõ: **"Làm tiếp PROMPT_LAB_UNITY_TRON_GOI.md"**. Gemini đọc `TIEN_DO_APP_LAB.md` + `HANDOFF.md` rồi làm tiếp đúng chỗ.
> File này **gộp và thay thế** `PROMPT_APP_LAB_MAY_TINH.md` + `PROMPT_UNG_DUNG_LAB_UNITY.md`. Không chạy hai file cũ nữa.

---

## CÁCH CHẠY LIÊN TỤC (luật vận hành, đọc trước tiên)
1. Làm **lần lượt 12 giai đoạn** dưới đây. **Không dừng hỏi người dùng giữa các giai đoạn.** Xong giai đoạn nào thì làm ngay giai đoạn sau.
2. Ngay khi bắt đầu, tạo **`TIEN_DO_APP_LAB.md`** ở thư mục gốc repo:
   - Chép danh sách 12 giai đoạn và các mục con dạng checkbox `- [ ]`.
   - Xong mục nào thì đánh `- [x]`, kèm ngày giờ và mã commit.
   - Đây là "bản đồ" để làm tiếp khi phiên bị ngắt.
3. **Cuối mỗi giai đoạn**, đúng thứ tự:
   1. `tools/unity_dong_bo.sh day` (từ GĐ2 trở đi).
   2. Chạy test liên quan.
   3. `git add -A && git commit -m "app-lab GĐx: …"`.
   4. Cập nhật `HANDOFF.md` + `TIEN_DO_APP_LAB.md`.
   5. **Làm tiếp giai đoạn sau.**
4. **Chỉ được DỪNG khi:**
   - (a) ổ đĩa còn dưới 15 GB trống;
   - (b) cần xoá dữ liệu/file của người dùng;
   - (c) cần `git push` hoặc deploy;
   - (d) cần API key hay thông tin người dùng chưa cung cấp.

   Mọi việc cần **đăng nhập tài khoản của người dùng** (Sketchfab, Mixamo, Unity Asset Store, Apple Developer) thì **KHÔNG dừng**: dùng phương án dự phòng, ghi vào mục "VIỆC NGƯỜI DÙNG CẦN LÀM" trong HANDOFF.md, rồi làm tiếp.
5. **Gặp lỗi:**
   - Tự đọc log, sửa, chạy lại, tối đa 3 lần cho mỗi vấn đề.
   - Vẫn hỏng thì ghi vào "LỖI CÒN TỒN" (lỗi gì, đã thử gì, hướng xử lý), chuyển sang giai đoạn không phụ thuộc vào nó, và **quay lại xử lí trước khi kết thúc**.
6. **Không được báo "xong" khi chưa kiểm chứng bằng mắt.** Mọi ảnh chụp kiểm tra phải được tự mở ra xem, và ghi vào HANDOFF.md một dòng mô tả thật ảnh đó đang thấy gì.
   Bài học cũ: ảnh "đã kiểm chứng" của bản web từng chỉ là màu nền, nhân vật không hề hiện.
7. Làm **lâu cũng được nhưng phải kĩ**. Có **2 mốc**:
   - **Mốc 1 "APP CHẠY ĐƯỢC"** (sau GĐ7): người dùng đã có app riêng dùng được.
   - **Mốc 2 "APP HOÀN CHỈNH"** (sau GĐ12).

   Đạt Mốc 1 vẫn **làm tiếp luôn** Phần II, không chờ.

---

## DÀN Ý
**Phần I – Xuất phòng lab hiện tại thành app riêng (chạy được trước, chưa cần đẹp)**
- GĐ1 Chuẩn bị máy, thư mục làm việc ngoài iCloud, điểm phục hồi.
- GĐ2 Dự án Unity mới + lõi hoá học (chạy `mo-phong.js` bằng Jint trên luồng riêng) + test.
- GĐ3 Phòng 22 × 16 m + 5 trạm (dùng lại cảnh Unity cũ).
- GĐ4 Nhân vật (giáo sư mặc blouse trắng, có dự phòng) + điều khiển kiểu Genshin + vào/rời bàn.
- GĐ5 Giao diện app cơ bản (menu, HUD, bảng điều khiển thí nghiệm, cài đặt).
- GĐ6 Đóng gói `PhongLabHoa3D.app` + deep link `phonglabhoa://` + nút "Mở bằng ứng dụng" trên web + admin + test.
- GĐ7 Tự kiểm tra bản 1 → **Mốc 1**.

**Phần II – Làm đẹp và hoàn thiện (trên cùng dự án)**
- GĐ8 Bảng tuần hoàn 118 nguyên tố vẽ lại hoàn toàn (dữ liệu thật, dùng chung web + app).
- GĐ9 Toà nhà lab rộng, trần cao, 7 phòng nối nhau, ánh sáng bake, vật liệu PBR thật.
- GĐ10 Dụng cụ đúng kích thước thật, thuỷ tinh/dung dịch khúc xạ, hiệu ứng.
- GĐ11 Nhân vật thật hoàn chỉnh + giao diện đầy đủ (câu hỏi, bảng tuần hoàn tương tác, âm thanh).
- GĐ12 Hiệu năng đa nhân CPU/GPU + build lại + tự kiểm tra cuối + tài liệu → **Mốc 2**.

---

## 0. Hiểu đúng yêu cầu "chạy app ngoài, dùng CPU như Genshin"
Người dùng muốn hết giật lag, đồ hoạ đẹp như game cài trên máy.

**Hình 3D do GPU vẽ. CPU lo logic, vật lí, hoạt hình và chuẩn bị lệnh vẽ.**
Trình duyệt (Three.js / Unity WebGL) gần như chỉ dùng một luồng CPU, WebGL chậm hơn Metal/DirectX: đó là nguồn gốc giật lag.

App máy tính Unity khắc phục bằng cách:
- vẽ bằng **Metal** (macOS) / **DirectX 12 – Vulkan** (Windows);
- bật **Graphics Jobs** và kết xuất đa luồng;
- chạy mô phỏng trên **luồng riêng**, hạt bằng **Job System + Burst** trên nhiều nhân;
- tải phòng bất đồng bộ.

Thiết kế phải tận dụng đủ các điểm này. Báo cáo cuối giải thích ngắn điều này cho người dùng bằng lời dễ hiểu.

## 1. Bối cảnh + luật chung
- **Máy người dùng** (Claude kiểm tra 24/10/2026):
  - Phần cứng: Apple **M2** (CPU 8 nhân, GPU 10 nhân), **RAM 8 GB**, ổ còn khoảng **21 GB** (đã dùng 90%).
  - Unity **6000.6.7f1** ở `/Users/trantrong/Documents/6000.6.7f1/Unity.app`, **nằm trong iCloud**. Có module **MacStandaloneSupport + WebGLSupport**, chưa có module Windows.
- **Luật cũ vẫn giữ:**
  - Đọc **AGENTS.md**, **CLAUDE.md**, **HANDOFF.md** trước.
  - Không sửa vùng Lab Sinh (`frontend/biology-lab-3d*.html`, `frontend/sinh-hoc/`, `backend/sinh_hoc_ai.py`).
  - Không sửa `.env`. Không push/deploy khi chưa được đồng ý.
  - Không `grep -r` toàn repo (iCloud chậm, `outputs/` rất lớn).
  - **Không sửa logic** `tinh-toan.js` / `mo-phong.js` / `thi-nghiem/*.json` / `bang-ion.json`.
- **Giữ nguyên các bản đang chạy:**
  - Bản Web 3D giữ cho điện thoại và máy yếu.
  - Dự án Unity WebGL cũ `unity/PhongThiNghiemHoaHoc/`: chỉ **chép** script sang dự án mới, không sửa bản gốc. Test `test_du_an_unity_co_ma_nguon_va_script_build` phải còn PASS.
- **Không đăng nhập tài khoản thay người dùng.**
- **Bản quyền:**
  - Chỉ dùng CC0 (Poly Haven, ambientCG, Kenney, Freesound CC0), CC BY (có ghi công), font OFL.
  - Mixamo: dùng trong sản phẩm, không phát tán file gốc.
  - Unity Starter Assets (Unity Companion License), Jint (BSD-2), dữ liệu PubChem (phạm vi công cộng).
  - **Không** dùng model .max của Hòa Phát, ảnh của Lý Sơn Sa Kỳ, logo hãng dụng cụ (chỉ xem để học hình dáng).

## 2. Kiến trúc (theo đúng từ GĐ2, để Phần II làm tiếp không phải đập đi)
```
frontend/hoa-hoc/phong-thi-nghiem/      ← NGUỒN DỮ LIỆU + LÕI HOÁ HỌC DUY NHẤT (web và app dùng chung)
  tinh-toan.js, mo-phong.js, thi-nghiem/*.json, bang-ion.json, ban-do.json
frontend/hoa-hoc/bang-tuan-hoan/nguyen-to.json   ← GĐ8: 118 nguyên tố
unity/PhongLabHoa3D/                    ← nguồn commit (Assets trừ TaiNguyenNgoai, Packages, ProjectSettings)
  Assets/Scripts/Loi/        lõi JS qua Jint, luồng mô phỏng
  Assets/Scripts/ToaNha/     dựng phòng (GĐ3) → toà nhà (GĐ9), cửa tự động, đèn phòng
  Assets/Scripts/NhanVat/    điều khiển kiểu Genshin, Cinemachine, hoạt hình
  Assets/Scripts/ThiNghiem/  5 trạm, dụng cụ, hiệu ứng
  Assets/Scripts/GiaoDien/   UI Toolkit
  Assets/Scripts/HeThong/    chất lượng đồ hoạ, deep link, tự kiểm tra, đo hiệu năng
  Assets/Editor/             DongBoDuLieu, DungToaNha, BakeAnhSang, BuildMayTinh
  Assets/DuLieu/toa-nha.json (GĐ9)
  Assets/StreamingAssets/ptn/  BẢN SAO dữ liệu web (chép bằng "Đồng bộ dữ liệu từ web" trước khi build)
  Assets/TaiNguyenNgoai/     texture/HDRI/model/nhân vật tải về – KHÔNG commit (.gitignore)
  TAI_NGUYEN.json (URL + giấy phép + sha256), GHI_CONG.md, THAM_KHAO.md, README.md
~/UnityProjects/PhongLabHoa3D           ← BẢN LÀM VIỆC ngoài iCloud (Library, Builds, TaiNguyenNgoai)
tools/unity_dong_bo.sh keo|day          tools/tai_tai_nguyen_unity.py      tools/tao_bang_tuan_hoan.py
```
- **Lõi hoá học là một:** app chạy chính `tinh-toan.js` + `mo-phong.js` qua **Jint**. Không viết lại phép tính bằng C#.
  Scripting backend **Mono** (Jint dùng reflection).
- **Phương án dự phòng:** chỉ khi Jint không chạy được `mo-phong.js` thì mới port sang C#, kèm bộ véc-tơ kiểm thử chạy cả trên Node và C# để so từng số. Ghi lí do vào HANDOFF.md.

---

# PHẦN I – XUẤT APP

## GĐ1. Chuẩn bị
1. Chạy `df -h ~`. **Dưới 15 GB thì DỪNG** và báo người dùng dọn ổ.
2. Ghi lại kết quả ban đầu của `.venv/bin/python -m pytest -q` và `node --test tests/js/`.
   Tạo điểm phục hồi: `git add -A && git commit -m "chore: diem phuc hoi truoc app lab Unity"`.
3. Tạo `TIEN_DO_APP_LAB.md` (luật vận hành mục 2).
4. Viết `tools/unity_dong_bo.sh`:
   - `keo` = repo → `~/UnityProjects/PhongLabHoa3D`.
   - `day` = ngược lại, rsync loại trừ `Library Temp Logs Builds UserSettings Assets/TaiNguyenNgoai`.
5. Thêm vào `.gitignore`: `unity/PhongLabHoa3D/{Library,Temp,Logs,Builds,UserSettings}/` và `unity/PhongLabHoa3D/Assets/TaiNguyenNgoai/`.

## GĐ2. Dự án Unity + lõi hoá học
1. Tạo `~/UnityProjects/PhongLabHoa3D` bằng Unity 6000.6.7f1, template **URP**.
   Gói: `render-pipelines.universal`, `cinemachine` 3.x, `inputsystem`, `ai.navigation`, `burst`, `collections`, `shadergraph`, `test-framework`.
   Mono, Graphics API **Metal**, **Graphics Jobs bật**.
2. **Jint** (NuGet) + phụ thuộc vào `Assets/Plugins/Jint/`, chỉ bật cho Editor + Standalone.
   `Loi/LoiHoaHoc.cs`:
   - Chạy `tinh-toan.js` rồi `mo-phong.js`, giả lập `module.exports` cho kiểu UMD.
   - Các hàm `Tao(id)`, `ThaoTac(lenh, thamSoJson)`, `CapNhat(dt)`, `TrangThai()`. Nhận sự kiện `trang_thai`, `so_tay`, `hieu_ung`.
3. **Luồng mô phỏng riêng:**
   - Mỗi luồng một `Engine`, `CapNhat` 30 lần/giây.
   - Hàng đợi lệnh vào thread-safe, ảnh chụp trạng thái mới nhất trả ra luồng chính. Luồng chính không bao giờ chờ JS.
   - Chỉ thí nghiệm đang làm mới chạy thời gian. Rời bàn thì tạm dừng.
4. Editor menu **"Phòng Lab → Đồng bộ dữ liệu từ web"**: chép các file lõi + JSON vào `Assets/StreamingAssets/ptn/`. Build tự gọi bước này trước.
5. **Test EditMode**, giá trị khớp `tests/js/`:
   - pH HCl 0,1 M (V = 0 / 10 / 19,9 / 20 / 20,1 / 30 mL → 1,00 / 1,48 / 3,60 / 7,00 / 10,40 / 12,30).
   - CH₃COOH (V = 0 / 10 / 20 mL → 2,88 / 4,76 / 8,73).
   - Điện phân 2 A 965 s → 0,64 g.
   - Pin Zn – Cu 1,103 V.
   - Phenolphthalein chuyển hồng ở V ≈ 20,0 mL.
   - Bản sao `StreamingAssets/ptn` trùng sha256 với `frontend/`.

   Chạy: `Unity -batchmode -projectPath ~/UnityProjects/PhongLabHoa3D -runTests -testPlatform EditMode -testResults logs/unity-editmode.xml`.

## GĐ3. Phòng 22 × 16 m + 5 trạm (dùng lại cái đã có)
1. **Chép** từ `unity/PhongThiNghiemHoaHoc/Assets/Scripts/` sang `Assets/Scripts/ThiNghiem/`: `LabHinh.cs`, `LabVatLieu.cs`, `LabTrangThai.cs`, `Canh/*.cs`.
   - Thay đường gửi thao tác qua `.jslib` (`LabBridge.GuiThaoTac` / `PTN_GuiSuKien`) bằng interface C# `ICauNoiLoi` nối thẳng vào `LoiHoaHoc`.
   - Giữ API cảnh `Dung(goc, …, tt)` / `CapNhat(json)` / `HieuUng(e)` / `Tick(dt)`.
2. **Phòng dựng bằng code từ `ban-do.json`:** 22 × 16 m, 5 khu, dải màu sàn, tường, cửa sổ, trần, biển tên khu, 5 bàn.
   - Đặt **5 cảnh cùng lúc** lên 5 bàn theo `tam`, `huong`, `cao_ban`. Ánh sáng realtime đơn giản.
   - Viết bộ dựng **dạng module** (đọc dữ liệu → dựng), để GĐ9 chỉ cần thay dữ liệu và bộ dựng bằng toà nhà lớn mà không đụng tới trạm, nhân vật, giao diện.
   - **Bỏ bảng tuần hoàn giả** của bản web (ô màu sai, không có kí hiệu). Để trống tường; GĐ8 sẽ vẽ bảng thật.
3. **NavMesh** phủ sàn, mọi bàn tới được. Có test.

## GĐ4. Nhân vật + điều khiển kiểu Genshin + vào bàn
1. **Nhân vật: giáo sư mặc áo blouse trắng** (người dùng chọn). Nguồn miễn phí, giấy phép **CC BY** (dùng được, phải ghi công):
   - https://sketchfab.com/3d-models/modern-scientist-in-a-lab-coat-e835a3b126a4463583727dbac2fedecc ("Modern Scientist in a Lab Coat", tác giả restore50, khoảng 215k tam giác, **chưa có khung xương**, mô hình do AI tạo).
   - https://sketchfab.com/3d-models/casual-scientist-in-a-lab-coat-59db594edf914c62827150c307e162e4 (cùng tác giả).
   - **Quy trình:** người dùng tải model và gắn khung xương bằng **Mixamo** (Upload Character), tải kèm các động tác, để file ở **`~/Downloads/Mixamo/`**.
     Có file ở đó thì:
     - chuyển vào `Assets/TaiNguyenNgoai/NhanVat/`, import rig **Humanoid**;
     - giảm lưới còn ≤ 60k tam giác (LOD), sửa vật liệu sang URP;
     - ghi công trong `GHI_CONG.md` và màn hình Giới thiệu: *"Modern Scientist in a Lab Coat" by restore50, CC BY 4.0, Sketchfab*.
   - **Chưa có file:** dựng **nhân vật tạm bằng code**: người tỉ lệ thật 1,7 m, áo blouse trắng dài qua gối, kính, tóc hoa râm (dáng "giáo sư").
     Hoạt hình đi/chạy/nhảy bằng code, để mọi thứ vẫn chạy và test được. Gói thành một prefab `NhanVat`; sau này thay model thật chỉ cần đổi prefab.
   - Giữ tương tác tủ đồ bảo hộ: nhấn E để đeo kính bảo hộ + găng tay. Áo blouse đã mặc sẵn.
2. **Điều khiển** (Input System, hỗ trợ cả tay cầm):
   - Bấm vào khung → **khoá chuột, di chuột để xoay camera**; giữ **Alt** để hiện chuột.
   - WASD đi theo hướng camera, nhân vật xoay mặt mượt, có gia tốc/phanh. **Shift** chạy, **Ctrl** đi chậm, **Space** nhảy, lăn chuột để thu/phóng.
   - **F** góc nhìn 1 ⇄ 3, **V** toàn cảnh, **M** bản đồ, **E** tương tác/vào bàn, **Q** rời bàn, **Esc** menu.
   - Bỏ qua phím khi đang gõ trong ô nhập.
3. **Camera (Cinemachine 3):**
   - Góc 3: `CinemachineOrbitalFollow` + `CinemachineRotationComposer` + **`CinemachineDeoccluder`** (không xuyên tường). Camera bắt đầu **sau lưng** nhân vật; đi lâu thì tự xoay về sau lưng (tắt được).
   - Góc 1: camera ở mắt, ẩn đầu bằng layer.
4. **Vào bàn:**
   1. Đứng trước bàn 0,6 s hoặc nhấn E.
   2. Nhả chuột → nhân vật tới chỗ đứng, quay mặt vào bàn.
   3. **Blend camera 1 s** vào cận cảnh (bàn chiếm 75–85% bề ngang).
   4. Bật Depth of Field lấy nét vào dụng cụ, làm tối nhẹ xung quanh.
   5. Bảng thí nghiệm hiện ra.

   Q để quay lại sau lưng nhân vật. Bước qua ranh giới khu thì hiện thẻ tên khu kiểu game 1,6 s.

## GĐ5. Giao diện app cơ bản (UI Toolkit)
- Màu theo web: sáng = Icy blue `#eef4fa` + Midnight Navy `#0f2b48`; tối = `#0f2438` + Sky Blue `#38bdf8`. Font **Be Vietnam Pro** + **JetBrains Mono** (OFL, được nhúng).
- **Menu chính:** Vào phòng lab · Chọn thí nghiệm (dịch chuyển thẳng tới bàn) · Cài đặt · Giới thiệu (ghi công) · Thoát.
- **HUD khi đi:** tên khu, bản đồ nhỏ, gợi ý "E – Vào bàn …", phím tắt thu gọn.
- **Bảng thí nghiệm khi tại bàn:**
  - **Điều khiển:** làm lại đủ nút theo `BANG[id]` trong `frontend/hoa-hoc/phong-thi-nghiem/giao-dien.js`, gửi đúng lệnh `thaoTac` như web.
  - Bước hướng dẫn, Số liệu, Sổ tay tự ghi. Chế độ Hướng dẫn / Tự do.
- **Cài đặt:**
  - Đồ hoạ Thấp / Vừa / Cao / Tự động, độ phân giải, giới hạn fps, VSync, hiện fps.
  - Độ nhạy chuột, đảo trục Y, FOV.
  - Lưu bằng `PlayerPrefs`.

## GĐ6. Đóng gói app + nối với web
1. **`Assets/Editor/BuildMayTinh.cs` → `BuildMayTinh.Mac()`:**
   - Build ra `~/UnityProjects/PhongLabHoa3D/Builds/macOS/PhongLabHoa3D.app` (Apple Silicon + Intel), tên hiển thị "Phòng Lab Hoá 3D", icon bình tam giác như web.
   - Ký ad-hoc `codesign --force --deep -s -`. Nén `PhongLabHoa3D-mac.zip`.
   - Có `BuildMayTinh.Windows()`, chỉ chạy khi đã có module Windows; chưa có thì báo, không lỗi.
   - Ghi lệnh build batchmode vào README.
2. **Deep link `phonglabhoa://mo?tn=<id>`:**
   - Khai báo Supported URL schemes (macOS), nhận bằng `Application.deepLinkActivated` / `Application.absoluteURL`.
   - Windows: lần chạy đầu ghi khoá `HKCU\Software\Classes\phonglabhoa`.
   - `tn` hợp lệ thì mở thẳng bàn đó; tham số lạ bỏ qua. Có test cho parser.
3. **Web:**
   - **Admin:** `app_settings` khoá `"ung_dung_lab"` = `{bat: false, url_mac: "", url_win: "", phien_ban: ""}`.
     - `GET/POST /admin/api/ung-dung-lab`: chỉ admin, CSRF, ghi nhật kí.
     - Khối cài đặt nằm trong thẻ "Phòng thí nghiệm" ở `/admin`, theo mẫu công tắc Unity trong `backend/phong_thi_nghiem.py`.
   - **API người dùng:** `GET /api/phong-thi-nghiem/ung-dung` (đã đăng nhập) → `{bat, url_mac, url_win, phien_ban}`. Chỉ lộ link khi `bat`.
   - **Trang lab:** nút "🖥 Mở bằng ứng dụng (mượt hơn)" hiện khi `bat` và máy là macOS/Windows.
     - Bấm thì thử mở `phonglabhoa://mo?tn=<id>`.
     - Sau 1,5 s trang vẫn còn hiển thị thì hiện hộp "Chưa cài ứng dụng?": link tải + 3 bước cài (giải nén → kéo vào Applications → lần đầu chuột phải chọn Mở).
   - File app **không để trong repo**. Người dùng chọn GitHub Releases / Google Drive rồi dán link ở `/admin`.
   - **Test** `tests/test_ung_dung_lab.py`:
     - Mặc định tắt.
     - Học sinh gọi API admin → 403; chưa đăng nhập → 401; thiếu CSRF → 403.
     - Đang tắt thì không lộ URL; admin bật thì học sinh thấy.

## GĐ7. Tự kiểm tra bản 1 → MỐC 1 "APP CHẠY ĐƯỢC"
1. Chế độ tự kiểm tra: `PhongLabHoa3D.app/Contents/MacOS/PhongLabHoa3D -tu-kiem-tra outputs/app-lab/v1/`. App tự làm lần lượt:
   1. Mở ở cửa, chụp góc 3 (**phải thấy nhân vật**).
   2. Đi tới từng bàn bằng NavMesh, vào bàn, chụp cận cảnh.
   3. Chạy kịch bản qua lõi:
      - chuẩn độ HCl chuyển hồng, V ≈ 20,0 mL;
      - nhận biết đúng 4 ống;
      - pin Zn – Cu 1,10 V, rút cầu muối về 0 V;
      - điện phân 2 A 965 s → 0,64 g.
   4. Chụp góc 1, bản đồ, menu.
   5. Ghi `bao-cao.json` (fps trung bình, 1% low, ms luồng chính, RAM, thời gian mở, kết quả từng kịch bản) rồi tự thoát.
2. Thử `open "phonglabhoa://mo?tn=dien-phan"` → app mở đúng bàn điện phân.
3. **Mục tiêu Mốc 1** trên máy M2 (1920×1200, chất lượng Cao):
   - ≥ 60 fps; RAM ≤ 1,5 GB; mở app ≤ 10 s; bản build ≤ 400 MB.
   - Mọi kịch bản đạt. pytest + node + EditMode PASS.
4. Tự mở xem từng ảnh, mô tả vào HANDOFF.md. Commit, đánh dấu Mốc 1 trong `TIEN_DO_APP_LAB.md` → **làm tiếp GĐ8 ngay**.

---

# PHẦN II – LÀM ĐẸP VÀ HOÀN THIỆN

## GĐ8. Bảng tuần hoàn – vẽ lại hoàn toàn (dùng chung web + app)
1. **Dữ liệu `frontend/hoa-hoc/bang-tuan-hoan/nguyen-to.json`** (118 nguyên tố), tạo bằng `tools/tao_bang_tuan_hoan.py`:
   - **Nguồn:** PubChem `https://pubchem.ncbi.nlm.nih.gov/rest/pug/periodictable/JSON` (phạm vi công cộng, đã kiểm tra trả 118 dòng).
     Lưu bản gốc vào `tools/du_lieu/pubchem_periodictable.json` để chạy lại không cần mạng.
   - **Đối chiếu RDKit** trong `.venv`:
     - kí hiệu khớp `Chem.GetPeriodicTable().GetElementSymbol(z)`;
     - nguyên tử khối lệch ≤ 0,01 với Z ≤ 92;
     - nguyên tố phóng xạ nặng ghi số khối trong ngoặc vuông, ví dụ [226].
   - **Trường dữ liệu:**
     - Định danh: `z`, `ki_hieu`, `ten` (IUPAC theo CT 2018, ví dụ "Sodium"), `ten_viet` (natri, sắt, đồng, bạc, vàng, thuỷ ngân, chì, thiếc, kẽm…).
     - Vị trí: `nguyen_tu_khoi` (2 chữ số thập phân, dấu phẩy), `chu_ky`, `nhom` (1–18), `nhom_ab` ("IA" … "VIIIA", "IB" … "VIIIB"), `khoi` (s/p/d/f).
     - `loai`: kim loại kiềm, kiềm thổ, chuyển tiếp, kim loại nhóm p, á kim, phi kim, halogen, khí hiếm, lanthanide, actinide.
     - Tính chất: `cau_hinh_e` (rút gọn, chỉ số trên, ví dụ "[Ar]3d⁶4s²"), `do_am_dien`, `ban_kinh_pm`, `nang_luong_ion_hoa_1_eV`, `so_oxi_hoa`, `trang_thai_25C`, `t_nong_chay_K`, `t_soi_K`, `khoi_luong_rieng`.
   - **Bố cục theo SGK Hoá học 10 (CT GDPT 2018):** nhóm A/B kèm số 1–18; lanthanide và actinide là 2 hàng dưới, có dấu * và **.
     Mở ảnh bảng tuần hoàn trong SGK Hoá 10 để kiểm vị trí La/Ac và nhãn nhóm trước khi chốt. Ghi nguồn đã đối chiếu vào `frontend/hoa-hoc/bang-tuan-hoan/README.md`.
   - Màu theo `loai`: dịu, chữ trên ô đạt WCAG ≥ 4,5:1 cả nền sáng lẫn tối.
2. **Test `tests/test_bang_tuan_hoan.py`:**
   - Đủ 118, Z từ 1 đến 118 không trùng, kí hiệu khớp RDKit, nguyên tử khối đúng sai số.
   - Kiểm vị trí mẫu:
     - H (chu kì 1, IA), He (1, VIIIA), Na (3, IA), Fe (4, VIIIB), Cu (4, IB), Br (4, VIIA), I (5, VIIA).
     - Hg ở trạng thái lỏng, Br ở trạng thái lỏng.
     - Ce–Lu ở hàng lanthanide, Th–Lr ở hàng actinide.
   - Không có ô trùng toạ độ trên lưới. Số electron trong `cau_hinh_e` bằng Z.
3. **Web:** viết lại `_taoBangTuanHoan` trong `phong-mo.js` từ `nguyen-to.json`.
   - Canvas 2048×1152: tiêu đề, nhãn nhóm/chu kì, ô có Z, kí hiệu, tên, nguyên tử khối, màu theo loại, chú thích.
   - Vẽ sau `document.fonts.ready`.
4. **App:**
   - **Bảng lớn 7 × 3,6 m có đèn nền** ở sảnh (mỗi ô là mesh + TextMeshPro, không phải ảnh mờ) + poster nhỏ mỗi phòng.
   - Nhấn **E** → bảng tương tác toàn màn hình:
     - Bấm một ô để mở thẻ chi tiết gồm cấu hình e, **mô hình lớp electron 3D** quay chậm, các tính chất, vị trí, loại nguyên tố.
     - Lọc theo khối/loại.
     - Tô màu xu hướng (độ âm điện, bán kính, năng lượng ion hoá) theo Hoá 10 chương 2.

## GĐ9. Toà nhà lab: rộng hơn, trần cao hơn, các phòng nối nhau
1. **`Assets/DuLieu/toa-nha.json`** (mét): kích thước, phòng (hình chữ nhật, cao trần), tường, vách kính, cửa (vị trí, rộng, kiểu tự động/khoá), cửa sổ, giếng trời, nội thất, trạm. Sơ đồ đề xuất khoảng **36 × 30 m**:
```
z=0  ┌──────────────── cửa sổ lớn (tường Bắc) ───────────────────────────────┐
     │ P. ĐỊNH TÍNH 12×10   │ KHO HOÁ CHẤT 6×10 │ P. ĐIỆN HOÁ 14×10             │
     │ nhan-biet-ion        │ cửa khoá, nhìn    │ pin-galvani · dien-phan        │
     │ 2 tủ hút, giá thuốc  │ qua kính          │ nguồn DC, bàn đo               │
     ├────[cửa kính]────────┴────[cửa]──────────┴────[cửa kính]────────────────┤
     │   HÀNH LANG – SẢNH TRUNG TÂM rộng 6 m, TRẦN 8 m (2 tầng), GIẾNG TRỜI     │
     │   BẢNG TUẦN HOÀN LỚN 7×3,6 m có đèn nền ở tường giữa                     │
     ├────[cửa kính]────────┬────[cửa]──────────┬────[cửa kính]────────────────┤
     │ P. ĐỊNH LƯỢNG 12×10  │ P. CHUẨN BỊ 6×10  │ P. GIẢNG – THỰC HÀNH 14×10     │
     │ chuan-do (1 bàn chính│ chậu rửa, giá phơi│ bàn giáo viên, bảng tương tác, │
     │ + 2–3 bàn trưng bày) │ tủ sấy            │ ghế học sinh                   │
     ├──────────────────────┴───────────┬──────┴──────────────────────────────┤
     │ SẢNH VÀO + KHU AN TOÀN: an-toan · tủ đồ bảo hộ · vòi rửa mắt/sen khẩn cấp │
     │ · tường GHS · bình chữa cháy                       [ CỬA CHÍNH ]          │
z=30 └──────────────────────────────────────────────────────────────────────────┘
```
   - **Trần:** phòng lab cao **5 m**, lộ máng cáp, ống gió, ống hút khí độc. Sảnh **8 m**, có giếng trời kính.
   - **Liên kết giữa các phòng:**
     - Vách kính từ sàn tới 2,6 m giữa hành lang và mọi phòng; cửa sổ kính giữa hai phòng cạnh nhau. Đứng đâu cũng nhìn xuyên qua được.
     - **Cửa kính trượt tự mở** khi tới gần 2 m.
     - Biển chỉ đường treo trần.
   - Lối đi ≥ 1,8 m. NavMesh nối mọi trạm, có test.
2. **Dựng bằng Editor script** `DungToaNha.cs` ra cảnh `Assets/Scenes/ToaNhaLab.unity` (bộ module tường/cột/cửa/khung kính/ô trần/máng cáp).
   - **Bake ánh sáng** bằng `BakeAnhSang.cs` (batchmode): Progressive GPU Lightmapper, lightmap ≤ 2048, ≤ 8 atlas, bake từng phòng nếu thiếu RAM.
   - Vật động dùng **Adaptive Probe Volumes**. **Reflection probe dạng hộp** cho từng phòng.
   - Thay phòng 22 × 16 m của GĐ3 bằng toà nhà này. 5 trạm, nhân vật, giao diện giữ nguyên API.
3. **Nội thất chân thật** (học hình dáng ở noithathoaphat.com/thu-vien-3d/noi-that-phong-thi-nghiem và banthinghiemlysonsaky.vn; dựng lại, **không tải file .max**):
   - Bàn thí nghiệm Hoá – Sinh: mặt phenolic xanh lam có gờ, tủ dưới vân gỗ, khung thép xám, chậu inox + vòi cổ ngỗng, giá thuốc thử 2 tầng.
   - Tủ hút khí độc có cửa kính kéo + đèn trong, tủ hoá chất 2 cánh có nhãn GHS, tủ an toàn sinh học.
   - Bàn tường, ghế đẩu, bồn rửa, giá phơi, tủ sấy, bàn giáo viên, màn hình tương tác.
   - Trạm rửa mắt/sen khẩn cấp, bình chữa cháy, tủ sơ cứu.
   - **Bàn trưng bày:** chỉ bàn chính là trạm (viền sáng); bàn phụ lại gần thì hiện "Bàn trưng bày – thí nghiệm ở bàn có viền sáng".
4. **Vật liệu PBR thật:** texture **CC0** (Poly Haven / ambientCG; tối đa 2K vì máy có 8 GB RAM) và HDRI CC0 cho bầu trời.
   Ghi vào `TAI_NGUYEN.json` + `GHI_CONG.md`. `tools/tai_tai_nguyen_unity.py` tải lại được.

## GĐ10. Dụng cụ đúng kích thước thật + hiệu ứng (nâng cấp 5 trạm)
1. **Tra cứu trên mạng** hình dáng và kích thước thật; ghi link vào `THAM_KHAO.md`; không sao chép logo/thương hiệu.
   Dựng procedural (lathe/sweep C#, thành kính có độ dày). **Kiểm lại theo tiêu chuẩn** trước khi dựng:

   | Dụng cụ | Kích thước tham khảo |
   |---|---|
   | Burette 50 mL hạng A, khoá PTFE (ISO 385) | dài tổng ~750 mm, vạch 0–50 mL chia 0,1 mL |
   | Bình tam giác cổ hẹp 250 mL (ISO 1773) | Ø đáy ~85 mm, cao ~145 mm, cổ Ø ~34 mm |
   | Cốc thuỷ tinh thấp 250 mL (ISO 3819) | Ø ~70 mm, cao ~95 mm, có mỏ và vạch |
   | Ống nghiệm | 16 × 160 mm; giá ống nghiệm |
   | Khác | giá đỡ + kẹp burette (đế ~200 × 130 mm, cọc ~600 mm), đèn cồn ~150 mL, dây Pt/nichrome thử lửa, lọ nhỏ giọt có nhãn, bình tia nước |
   | Điện hoá | thanh Zn/Cu 100 × 20 × 1 mm, thanh than chì Ø 6 × 150 mm, cầu muối chữ U, đồng hồ vạn năng, nguồn DC 0–30 V, dây có kẹp cá sấu |

2. **Shader Graph:**
   - **Thuỷ tinh:** khúc xạ bằng Scene Color + lệch pháp tuyến, fresnel, hấp thụ theo độ dày, cạnh ánh xanh nhẹ. Bật Opaque Texture.
   - **Dung dịch:** mặt khum, mức theo tham số, màu đổi mượt theo trạng thái lõi.
   - **Kim loại:** PBR có vết xước. Vạch chia, nhãn dùng decal.
   - Thứ tự vẽ: mặt sau kính → dung dịch → mặt trước kính.
3. **Hiệu ứng** (VFX Graph hoặc Particle System):
   - Bọt khí nổi và vỡ; kết tủa lắng (hạt chạy bằng **Job + Burst**); lớp kim loại bám dày dần.
   - Ngọn lửa đèn cồn có đèn nhấp nháy, đổi màu khi thử lửa; electron chạy trên dây.
4. **Điểm nóng đánh số** (①②③…) trên dụng cụ, bấm để thao tác. Kết quả số vẫn đúng như GĐ2 (chạy lại test).

## GĐ11. Nhân vật hoàn chỉnh + giao diện đầy đủ
1. Có file Mixamo/Sketchfab ở `~/Downloads/Mixamo/` thì **thay nhân vật tạm bằng giáo sư thật**:
   - Blend tree đứng/đi/chạy/chạy nhanh, nhảy (bắt đầu/lơ lửng/tiếp đất), quay tại chỗ, với tay khi thao tác.
   - **Foot IK**, chân không trượt.
   - Không có file thì nâng cấp nhân vật tạm (tỉ lệ, áo blouse có nếp, hoạt hình mượt) và giữ mục này trong "VIỆC NGƯỜI DÙNG CẦN LÀM".
2. **Giao diện đầy đủ:**
   - Tab **Câu hỏi** (3 dạng thi TN THPT lấy từ JSON, có chấm), tab **Thông tin**.
   - **Bảng tuần hoàn tương tác** (GĐ8).
   - **Bản đồ toà nhà** (M): bấm phòng để tự đi hoặc dịch chuyển nhanh. Toàn cảnh (V) kiểu nhà búp bê: ẩn trần, nhìn chéo từ trên.
   - Cài đặt thêm tỉ lệ dựng hình (STP), mức Rất cao, âm lượng.
3. **Âm thanh** (CC0 – Kenney, Freesound CC0, có ghi công): bước chân, tiếng phòng, cửa trượt, tiếng thuỷ tinh, bọt khí.

## GĐ12. Hiệu năng đa nhân CPU/GPU + build lại + tự kiểm tra cuối → MỐC 2
1. **Player/URP:**
   - Graphics Jobs, Metal / DX12 + Vulkan, Forward+.
   - **GPU Resident Drawer** + **GPU Occlusion Culling**, SRP Batcher, SSAO, MSAA 4× hoặc TAA, **STP** cho máy yếu.
   - Bloom nhẹ, tonemapping ACES.
2. **Tổ chức cảnh:**
   - **Occlusion culling bake** theo phòng, LOD Group.
   - **Tải từng phòng bất đồng bộ**, mipmap streaming, instancing.
   - Lõi hoá học ở luồng riêng, hạt bằng Job + Burst.
3. **Tự chọn chất lượng** theo `SystemInfo`. **Mục tiêu** (đo bằng Profiler, ghi vào HANDOFF.md):

   | Máy | Chế độ | Mục tiêu |
   |---|---|---|
   | M2 8 GB, 1920×1200 | Cao | **≥ 60 fps**, 1% low ≥ 45; luồng chính ≤ 8 ms; RAM ≤ 2,5 GB |
   | Cùng máy | Rất cao | ≥ 45 fps |
   | Laptop Windows card tích hợp, 1080p | Thấp + STP 67% | ≥ 30 fps |

   - Đi qua cửa không khựng (khung dài nhất < 50 ms). Mở app ≤ 15 s. Bản build ≤ 1 GB.
4. **Build lại** `.app` + zip. Chạy **tự kiểm tra cuối** `-tu-kiem-tra outputs/app-lab/v2/`:
   - Như GĐ7, thêm ảnh: toàn cảnh nhà búp bê, sảnh có bảng tuần hoàn, từng phòng, bảng tuần hoàn tương tác, góc 1/góc 3.
   - **Tự mở xem từng ảnh**, mô tả vào HANDOFF.md.
5. **Test cuối:**
   - EditMode: lõi, `toa-nha.json` hợp lệ, NavMesh nối mọi trạm, sha256 bản sao, 118 nguyên tố, deep link.
   - `pytest -q` + `node --test tests/js/` PASS hết.
6. **Tài liệu:**
   - `unity/PhongLabHoa3D/README.md`: mở dự án, đồng bộ, tải tài nguyên, build, tự kiểm tra, deep link, ghi công.
   - Cập nhật **HANDOFF.md**. Sửa đoạn "## Phòng thí nghiệm Hoá ảo" trong **CLAUDE.md**: app máy tính, `nguyen-to.json`, khoá `ung_dung_lab`, route và test mới.
7. **Báo cáo cuối cho người dùng** (ngắn, dễ hiểu):
   - Đã làm gì; cách mở app; số fps; ảnh chụp.
   - Giải thích vì sao app mượt hơn web (CPU/GPU).
   - **VIỆC NGƯỜI DÙNG CẦN LÀM** còn lại.
   - **Chưa push hoặc deploy.**

---

## VIỆC NGƯỜI DÙNG CẦN LÀM (ghi vào HANDOFF.md, nhắc trong báo cáo; Gemini không làm thay)
1. Dọn ổ để còn ≥ 20 GB trống (Unity + bake + build có thể chiếm 8–12 GB).
2. (Nên làm) Cài lại Unity 6000.6.7f1 bằng Unity Hub vào `/Applications/Unity/Hub/Editor`, ra khỏi iCloud.
   (Tuỳ chọn) thêm **Windows Build Support (Mono)** nếu muốn bản Windows.
3. **Nhân vật giáo sư:**
   1. Đăng nhập Sketchfab, tải model ở GĐ4 (FBX).
   2. Vào mixamo.com → **Upload Character** → đặt điểm khớp → tải *FBX for Unity, With Skin, T-pose*.
   3. Tải động tác *Idle, Walking, Running, Fast Run, Jump, Falling Idle, Landing, Left/Right Turn, Reaching*: chọn *Without Skin, 30 fps*; đi và chạy tích **In Place**.
   4. Để hết vào `~/Downloads/Mixamo/`, rồi gõ cho Gemini "Làm tiếp PROMPT_LAB_UNITY_TRON_GOI.md – đã có nhân vật".
4. Chọn nơi để file cài app (GitHub Releases / Google Drive), dán link ở `/admin`. Quyết định có mua Apple Developer để notarize hay không.

## ĐIỀU KIỆN XONG
**Mốc 1 – APP CHẠY ĐƯỢC (sau GĐ7)**
- [ ] `PhongLabHoa3D.app` mở được bằng tay và bằng nút trên web, chạy không cần mạng.
- [ ] Phòng 22 × 16 m, 5 bàn, nhân vật (giáo sư hoặc nhân vật tạm mặc blouse), xoay camera bằng chuột, vào/rời bàn mượt.
- [ ] 5 thí nghiệm cho **cùng kết quả số** như bản web. `bao-cao.json` v1 đạt. Ảnh đã được xem và mô tả đúng.

**Mốc 2 – APP HOÀN CHỈNH (sau GĐ12)**
- [ ] Toà nhà khoảng 36 × 30 m: phòng lab trần 5 m, sảnh 8 m có giếng trời, 7 phòng nối nhau bằng hành lang, vách kính, cửa tự mở. Ánh sáng bake, phản chiếu đúng, vật liệu PBR thật.
- [ ] Bảng tuần hoàn 118 nguyên tố đúng bố cục SGK, đối chiếu RDKit. Có bảng lớn ở sảnh, bảng tương tác trong app, poster web vẽ lại.
- [ ] Dụng cụ đúng kích thước thật, thuỷ tinh/dung dịch khúc xạ, hiệu ứng thật. 5 thí nghiệm vẫn đúng số.
- [ ] Nhân vật giáo sư thật (hoặc đã ghi rõ việc người dùng cần tải). Điều khiển kiểu Genshin đủ phím, góc 1/3, toàn cảnh, không xuyên tường.
- [ ] Đạt bảng hiệu năng GĐ12. `bao-cao.json` v2 đạt. Ảnh đã được xem và mô tả đúng.
- [ ] Web: nút mở app + admin link tải + test. Bản Web 3D và Unity WebGL cũ vẫn chạy. Mọi test PASS.
- [ ] `TIEN_DO_APP_LAB.md` đánh dấu hết. HANDOFF.md, CLAUDE.md, README cập nhật. Commit đủ các giai đoạn. **Chưa push hoặc deploy.**
