# Tiến độ phát triển App Lab Hoá 3D (Unity)

> Theo sát kế hoạch trong `PROMPT_LAB_UNITY_TRON_GOI.md`. Chạy liên tục từ đầu đến cuối.

---

## PHẦN I – XUẤT PHÒNG LAB THÀNH APP RIÊNG (MỐC 1)

### [x] GĐ1. Chuẩn bị (Hoàn thành: 2026-10-24 13:08, commit: `ed7c3d4`)
- [x] Kiểm tra ổ cứng (`df -h ~` >= 15 GB trống - thực tế: 21 GB)
- [x] Chạy baseline test (pytest 593 passed, node test 30 passed)
- [x] Tạo điểm phục hồi git (`chore: diem phuc hoi truoc app lab Unity`)
- [x] Viết `tools/unity_dong_bo.sh` (`keo` / `day`)
- [x] Cập nhật `.gitignore` cho dự án `unity/PhongLabHoa3D/`
- [x] Tạo `TIEN_DO_APP_LAB.md`

### [x] GĐ2. Dự án Unity + Lõi hoá học (Jint) (Hoàn thành: 2026-10-24 13:19)
- [x] Khởi tạo dự án URP tại `~/UnityProjects/PhongLabHoa3D` bằng Unity 6000.6.7f1
- [x] Cài đặt các package cần thiết: URP, Cinemachine 3, Input System, AI Navigation, Burst, Collections, Test Framework
- [x] Cấu hình Metal, Graphics Jobs, Mono scripting backend
- [x] Tích hợp Jint DLL vào `Assets/Plugins/Jint/`
- [x] Viết `Assets/Scripts/Loi/LoiHoaHoc.cs` chạy JS (`tinh-toan.js`, `mo-phong.js`) trên worker thread riêng biệt (30Hz, hàng đợi lệnh an toàn luồng)
- [x] Viết menu Editor `Assets/Editor/DongBoDuLieu.cs` đồng bộ từ web vào `Assets/StreamingAssets/ptn/`
- [x] Viết EditMode tests kiểm tra tính toán hoá học (pH HCl, CH3COOH, điện phân Cu, pin Zn-Cu, phenolphthalein, sha256)
- [x] Chạy Unity EditMode tests và xác minh PASS (6/6 tests passed)
- [x] Đồng bộ `tools/unity_dong_bo.sh day`, cập nhật git

### [x] GĐ3. Phòng 22 × 16 m + 5 trạm thí nghiệm (Hoàn thành: 2026-10-24 13:26)
- [x] Chép script trạm từ `unity/PhongThiNghiemHoaHoc/` sang `Assets/Scripts/ThiNghiem/`
- [x] Chuyển đổi giao tiếp sang interface C# `ICauNoiLoi` nối trực tiếp `LoiHoaHoc`
- [x] Dựng phòng 22 × 16 m theo `ban-do.json` (module code tự động `DungPhongLab.cs`: sàn, tường, vách kính, cửa sổ, trần, biển khu, 5 bàn phenolic)
- [x] Bố trí 5 cảnh thí nghiệm cùng lúc trên 5 bàn (`QuanLyTram.cs`)
- [x] Cấu hình NavMesh phủ sàn phòng và kết nối 5 bàn (`NavMeshSurface` runtime bake)
- [x] Viết test `TestNavMeshVaPhongLab.cs` xác nhận NavMesh tới được mọi bàn (8/8 tests PASS)
- [x] Đồng bộ, chạy test, cập nhật git

### [x] GĐ4. Nhân vật + Điều khiển Genshin + Vào bàn (Hoàn thành: 2026-10-24 13:30)
- [x] Dựng nhân vật giáo sư blouse trắng bằng code (`DungNhanVat.cs`: người tỉ lệ 1.7m, blouse trắng dài qua gối, tóc hoa râm, kính bảo hộ, hoạt hình chân tay nhảy/đi/thở mượt mà)
- [x] Tương tác tủ đồ bảo hộ: đeo kính bảo hộ và găng tay cao su y tế
- [x] Điều khiển Input System (`DieuKhienNhanVat.cs`): WASD theo hướng camera, khóa chuột / Alt hiện chuột, Shift chạy nhanh, Ctrl đi chậm, Space nhảy, cuộn chuột zoom tay đòn
- [x] Chế độ camera: F chuyển đổi Ngôi 1 (mắt y=1.62m, ẩn đầu) ⇄ Ngôi 3 (qua vai phải), V toàn cảnh nhà búp bê, raycast chống xuyên tường
- [x] Cơ chế vào bàn: đứng trước bàn 0.6s hoặc nhấn E tự động di chuyển tới điểm dừng, quay mặt vào bàn, camera blend cận cảnh, phím Q rời bàn mượt mà kèm 3.5s cooldown
- [x] Viết test `TestNhanVatVaDieuKhien.cs` (11/11 EditMode tests PASS)
- [x] Đồng bộ, chạy test, cập nhật git

### [x] GĐ5. Giao diện app cơ bản (UI Toolkit / OnGUI Glassmorphism) (Hoàn thành: 2026-10-24 13:37)
- [x] Bảng màu thống nhất theo web: Icy blue `#eef4fa`, Midnight Navy `#0f2b48`, Sky Blue `#38bdf8`
- [x] Menu chính: Vào phòng lab, Chọn thí nghiệm (5 trạm), Cài đặt, Giới thiệu (ghi công), Thoát
- [x] HUD khi di chuyển: tên khu (banner 1.6s), gợi ý phím (E, Q, F, V, Esc), phím tắt thu gọn, FPS counter
- [x] Bảng điều khiển thí nghiệm tại bàn: khớp đủ nút lệnh theo `BANG[id]` của web (chuẩn độ, nhận biết ion, pin Galvani, điện phân, an toàn)
- [x] Menu cài đặt: Đồ họa (Thấp/Vừa/Cao), Giới hạn FPS (30/60/120/Max), VSync, Độ nhạy chuột, FOV, Âm lượng (lưu & nạp PlayerPrefs)
- [x] Deep Link URL Scheme `phonglabhoa://mo?tn=<id>`
- [x] Viết test `TestGiaoDienApp.cs` (5/5 tests PASS, tổng cộng 16/16 EditMode tests PASS)
- [x] Đồng bộ, chạy test, cập nhật git

### [x] GĐ6. Đóng gói app + Nối với web (Hoàn thành: 2026-10-24 13:52)
- [x] Viết `Assets/Editor/BuildMayTinh.cs` build standalone macOS `PhongLabHoa3D.app` (Apple Silicon, 75MB, nén zip 28MB)
- [x] Ký ad-hoc codesign và tạo gói nén `PhongLabHoa3D-mac.zip`
- [x] Đăng ký deep link URL scheme `phonglabhoa://mo?tn=<id>` và `CFBundleDisplayName`
- [x] Web backend: cấu hình `app_settings` cho `ung_dung_lab`, API admin `GET/POST /admin/api/ung-dung-lab`, API người dùng `GET /api/phong-thi-nghiem/ung-dung`
- [x] Web frontend: Nút "🖥 Mở bằng ứng dụng" trên trang lab + hộp thoại hướng dẫn cài đặt
- [x] Viết bộ test `tests/test_ung_dung_lab.py` xác minh quyền, CSRF, ẩn/hiện link (2/2 tests PASS)
- [x] Đồng bộ, chạy test, cập nhật git

### [x] GĐ7. Tự kiểm tra bản 1 → ĐẠT MỐC 1 "APP CHẠY ĐƯỢC" (Hoàn thành: 2026-10-24 21:46)
- [x] Chạy lệnh tự kiểm tra: `PhongLabHoa3D.app/Contents/MacOS/PhongLabHoa3D -tu-kiem-tra outputs/app-lab/v1b/`
- [x] Tự động di chuyển qua 5 trạm, chụp ảnh góc 3, góc 1, cận cảnh, bản đồ, menu (9/9 ảnh sắc nét, đúng góc nhìn)
- [x] Chạy 4 kịch bản tự động (chuẩn độ chuyển hồng, nhận biết ion, pin Galvani 1.10V ngắt mạch 0V, điện phân 0.64g Cu) đều PASS 100%
- [x] Xuất `outputs/app-lab/v1b/bao-cao.json`: FPS trung bình 119.2 FPS, 1% low 110.0 FPS, RAM 149.4 MB, thời gian mở app 2.09s
- [x] Tự mở xem trực tiếp từng ảnh bằng mắt và mô tả chi tiết vào HANDOFF.md
- [x] Đánh dấu Mốc 1 hoàn thành, tiếp tục ngay sang Phần II (GĐ8)

---

## PHẦN II – LÀM ĐẸP VÀ HOÀN THIỆN (MỐC 2)

### [x] GĐ8. Bảng tuần hoàn 118 nguyên tố vẽ lại hoàn toàn (dùng chung Web + App) (Hoàn thành: 2026-10-24 22:25)
- [x] Viết `tools/tao_bang_tuan_hoan.py` trích xuất PubChem và đối chiếu RDKit
- [x] Tạo `frontend/hoa-hoc/bang-tuan-hoan/nguyen-to.json` đầy đủ 118 nguyên tố, bố cục SGK Hoá 10
- [x] Viết test `tests/test_bang_tuan_hoan.py` (6/6 tests PASS)
- [x] Vẽ lại canvas bảng tuần hoàn chất lượng cao trên web trong `phong-mo.js` (2048×1152, 118 ô)
- [x] Xây dựng bảng tuần hoàn 3D 7x3.6m có đèn nền trong sảnh app Unity (`BangTuanHoan3D.cs`)
- [x] Tạo bảng tuần hoàn tương tác toàn màn hình (xem cấu hình e 3D, mô hình Bohr động, lọc nhóm, tô màu xu hướng)
- [x] Viết test `TestBangTuanHoan.cs` trong Unity (5/5 EditMode tests PASS, tổng 21/21 tests PASS)
- [x] Đồng bộ, chạy test hồi quy 601 pytest + 30 node tests PASS, cập nhật git

### [x] GĐ9. Toà nhà lab hiện đại: 36 × 30 m, trần cao 5m/8m, 7 phòng (Hoàn thành: 2026-10-24 22:28)
- [x] Thiết kế `Assets/DuLieu/toa-nha.json` (7 phòng nối nhau, sảnh 8m, giếng trời, vách kính, cửa trượt tự động)
- [x] Viết `DungToaNha.cs` dựng cảnh toà nhà module hoá và trần giật cấp (sảnh 8m, phòng 5m, hành lang 4m)
- [x] Tích hợp cửa trượt kính tự động `CuaTruotTuDong.cs` mở khi người chơi lại gần (< 2.2m)
- [x] Thiết lập ánh sáng giếng trời Skylight + Đèn LED sảnh/hành lang + Reflection Probes chống đen kính/inox
- [x] Dựng nội thất phòng thí nghiệm chi tiết (tủ hút khí độc Lý Sơn Sa Kỳ, trạm rửa mắt khẩn cấp, bàn phenolic)
- [x] Viết test `TestToaNha.cs` (4/4 tests PASS, tổng 25/25 Unity tests PASS)
- [x] Đồng bộ, chạy test, cập nhật git

### [x] GĐ10. Dụng cụ đúng kích thước thật + Hiệu ứng đồ họa cao cấp (Hoàn thành: 2026-10-24 22:32)
- [x] Tra cứu và dựng procedural dụng cụ theo chuẩn ISO (`DungCuISO.cs`: Burette ISO 385, Bình tam giác ISO 1773, Cốc ISO 3819, Ống nghiệm ISO 4142)
- [x] Vật liệu thuỷ tinh khúc xạ phản chiếu PBR URP + dung dịch đổi màu mượt mà + mặt cong khum meniscus
- [x] Hiệu ứng bọt khí sủi bọt điện phân / phản ứng kim loại tạo khí & kết tủa lắng đáy (`BotKhiThiNghiem.cs`)
- [x] Điểm nóng tương tác ①②③ trên dụng cụ với hiệu ứng nhịp thở pulse và tooltip hướng dẫn (`DiemNongTuongTac.cs`)
- [x] Viết test `TestDungCuISO.cs` (5/5 tests PASS, tổng 30/30 Unity tests PASS)
- [x] Đồng bộ, chạy test, cập nhật git

### [x] GĐ11. Nhân vật hoàn chỉnh + Giao diện nâng cao (Hoàn thành: 2026-10-24 22:45)
- [x] Hoàn thiện nhân vật giáo sư blouse trắng với rigging procedural tay chân, tương tác tủ đồ bảo hộ
- [x] Bổ sung tab Câu hỏi ôn thi THPT và tab Thông tin thí nghiệm tại bàn (`CauHoiOnThiTHPT.cs` và `GiaoDienApp.cs`)
- [x] Bản đồ tòa nhà tương tác (M) và chế độ xem toàn cảnh Dollhouse (V) tự ẩn trần nhà
- [x] Tích hợp âm thanh 3D procedural synth CC0 (`AmThanhLab.cs`: tiếng chân, rót nước, bọt khí, click công tắc)
- [x] Viết test `TestOnThiVaAmThanh.cs` (2/2 tests PASS, tổng 32/32 Unity tests PASS)
- [x] Đồng bộ, chạy test, cập nhật git

### [x] GĐ12. Hiệu năng tối đa + Build hoàn chỉnh v2 → ĐẠT MỐC 2 "APP HOÀN CHỈNH" (Hoàn thành: 2026-10-24 23:08)
- [x] Cấu hình tối ưu Forward+, GPU Resident Drawer, SRP Batcher, STP upscaling
- [x] Occlusion Culling và mipmap streaming
- [x] Đo kiểm hiệu năng Profiler trên Apple M2: FPS trung bình 117.8 FPS, 1% low 62.7 FPS, RAM 166.1 MB, mở app 2.33s (Vượt xa mục tiêu: >= 60 FPS, RAM <= 2.5GB, khởi động <= 8s)
- [x] Đóng gói build cuối v2 `PhongLabHoa3D.app` (Apple Silicon 78MB, zip phân phối 29MB) và chạy `-tu-kiem-tra outputs/app-lab/v2/`: 4/4 kịch bản hoá học đạt `tat_ca_dat: true`
- [x] Mở xem và kiểm chứng toàn bộ 9/9 ảnh chụp v2 (01-09) chuẩn xác từng chi tiết PBR
- [x] Cập nhật tài liệu `README.md`, `HANDOFF.md`, `CLAUDE.md`
- [x] Viết báo cáo tổng kết chi tiết cho người dùng

---
## PHẦN III – NHÚNG PHÒNG LAB UNITY TRỰC TIẾP VÀO APP CHEMISTRY (MỐC 3)
> Theo chuẩn `PROMPT_NHUNG_LAB_UNITY_VAO_APP.md` – Hoàn thành 100% (2026-10-25).

### [x] GĐ0. Khảo sát & Kế hoạch
- [x] Tạo `tools/kiem_tra_app_lab_unity.sh`
- [x] Kiểm tra dung lượng và tài nguyên app

### [x] GĐ1. Cầu nối Unity WebGL ⇄ Trang web
- [x] Tạo `unity/PhongLabHoa3D/Assets/Plugins/WebGL/CauNoiTrang.jslib`
- [x] Tạo `unity/PhongLabHoa3D/Assets/Scripts/Loi/PTNLoiTrang.cs`
- [x] Tạo `frontend/hoa-hoc/phong-thi-nghiem/chon-che-do-lab.js` và `lab-unity-app.js`

### [x] GĐ2. Build Unity WebGL chuẩn App
- [x] Thêm target WebGL vào `BuildMayTinh.cs`
- [x] Build WebGL tại `outputs/app-lab/webgl/` (Tổng 43.3 MB: `lab.loader.js` 20K, `lab.framework.js` 324K, `lab.data` 10M, `lab.wasm` 33M)

### [x] GĐ3. Tích hợp vào Electron App CHEMISTRY
- [x] Đăng ký scheme an toàn `chemlab://` trong `desktop/main.js`
- [x] Cung cấp API `window.chemistryApp.labWebGL()` qua `desktop/preload.js`
- [x] Cập nhật `desktop/package.json` và `desktop/build-mac.sh` đóng gói `lab-webgl` và `PhongLabHoa3D.app`

### [x] GĐ4. Giao diện & Trải nghiệm người dùng
- [x] Nút "⚡ Mở cửa sổ mượt" giải phóng Unity WebGL (~94-220MB RAM) và mở app native
- [x] Nút "Chạy lại trong app" và nút "Bản nhẹ" (Three.js) ⇄ "Bản 3D đầy đủ" (Unity)
- [x] Đồng bộ giao diện sáng / tối qua `DoiGiaoDien`
- [x] Bảo toàn nhập liệu bàn phím HTML (`WebGLInput.captureAllKeyboardInput = false`)

### [x] GĐ5. Nghiệm thu thực tế trong App Máy tính (CDP)
- [x] Viết `desktop/kiem_tra/kiem_tra_lab_unity.py`
- [x] Khởi chạy `CHEMISTRY.app`, đo kiểm 8 kịch bản tự động: **PASS 100% (8/8)**
  - Tải nhanh: 4.2s (yêu cầu < 10s)
  - Đồ họa rực rỡ: Độ lệch chuẩn 74.81 (yêu cầu > 12.0), 8020 màu (yêu cầu >= 20)
  - Chuẩn độ HCl: V = 20.05 mL chuyển hồng chính xác
  - FPS mượt mà: 60 FPS trung bình
  - RAM giải phóng: Giảm 93.7 MB khi mở cửa sổ riêng
  - Chuyển đổi qua lại Bản nhẹ ⇄ Bản đầy đủ trơn tru
  - Không nuốt phím HTML, đồng bộ theme sáng/tối
  - Fallback mượt mà về Three.js khi thiếu build WebGL
- [x] Xuất báo cáo `outputs/app-chemistry/lab-unity/KET_QUA_NGHIEM_THU_LAB_UNITY.md`

---

---
## PHẦN IV – TỰ TẢI CHUYỂN ĐỘNG MIXAMO & ĐƯA NHÂN VẬT VÀO APP (26/10/2026)
> Theo chuẩn `PROMPT_TAI_VA_DUA_NHAN_VAT_VAO_APP.md`.

### [x] GĐ1. Chuẩn bị mô hình nhân vật (Bước A)
- [x] Kiểm tra ổ cứng (`df -h ~` trống 18 GB > 15 GB).
- [x] Tạo `~/Downloads/MoHinh3D/researcher-60k.glb` (gộp 1 lưới duy nhất, 57.956 tam giác, scale 0.896 đúng 1,70 m, gót chân y=0, mặt hướng +Z, đầy đủ 4 texture 2k).
- [x] Xuất `~/Downloads/MoHinh3D/researcher-cho-mixamo.zip` (11 MB, gồm `researcher.obj`, `researcher.mtl`, và textures).
- [x] Lưu 4 texture 2k gốc vào `~/Downloads/MoHinh3D/textures/`.
- [x] Render kiểm chứng 3 góc nhìn tại `outputs/nhan-vat/` (chính diện, góc nghiêng 3/4, cận cảnh mặt): mô hình sắc nét, cân đối, đúng tỉ lệ, không lỗi lưới.

### [x] GĐ2. Chuyển động giao diện Animate UI & Vendor Motion / Draco (Bước F)
- [x] Vendor ngoại tuyến `motion.12.4.7.js` vào `frontend/vendor/motion/`.
- [x] Vendor ngoại tuyến `DRACOLoader.js` và `draco_decoder.wasm/js` vào `frontend/vendor/three/draco/`.
- [x] Cập nhật `tools/tai_vendor.py` và `frontend/vendor/PHIEN_BAN.json` (SHA256 chuẩn xác).
- [x] Viết `frontend/chuyen-dong.js` cung cấp tokens (spring, ease, transition, scale, blur) và các hàm: `hienRa`, `anDi`, `nhanNut`, `truotVao`, `chiBaoTab`.
- [x] Tôn trọng `prefers-reduced-motion` và bảo đảm hiệu năng.

### [x] GĐ3. Trợ lý Web 3D & Mở rộng Bảng trạng thái Unity (Bước D & E)
- [x] Viết `frontend/nhan-vat/tro-ly.js`: widget 220x300 px góc dưới phải, tải offline, bong bóng lời thoại Animate UI, tự động lùi về idle an toàn.
- [x] Đồng bộ manifest 47 động tác vào `frontend/nhan-vat/` và `StreamingAssets/ptn/`.
- [x] Mở rộng `BangTrangThaiNhanVat.cs` với 26 sự kiện mới (thao tác bàn thí nghiệm, di chuyển, ngồi, đứng chờ).
- [x] Cập nhật `TestBangTrangThaiNhanVat.cs` (44/44 Unity tests PASS, 166/166 Node tests PASS, 611/611 Pytest PASS).

### [ ] GĐ4. Tải chuyển động Mixamo & Gắn xương (Bước B & C)
- [ ] Chờ người dùng đăng nhập Mixamo (Adobe) trên cửa sổ Chrome đang mở.
- [ ] Tải `researcher-cho-mixamo.zip` lên Mixamo, gắn 5 điểm khớp (cằm, 2 cổ tay, 2 khuỷu tay, 2 đầu gối, háng) với Skeleton LOD 65.
- [ ] Tải 47 chuyển động về `~/Downloads/MoHinh3D/mixamo/`.

