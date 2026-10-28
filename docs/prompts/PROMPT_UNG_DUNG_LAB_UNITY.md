> ⛔ **ĐÃ GỘP vào `PROMPT_LAB_UNITY_TRON_GOI.md` (24/10/2026). KHÔNG chạy file này** – chỉ giữ lại để tham khảo.

# PROMPT – Ứng dụng máy tính "Phòng Lab Hoá 3D" bằng Unity: chạy ngoài trình duyệt cho mượt, đồ hoạ chân thật, toà nhà lab rộng nhiều phòng nối nhau, bảng tuần hoàn vẽ lại hoàn toàn

> Dán nguyên file này cho Antigravity, hoặc gõ: "Đọc và làm theo PROMPT_UNG_DUNG_LAB_UNITY.md".
> **Chạy SAU `PROMPT_APP_LAB_MAY_TINH.md`** (bước 1: xuất phòng lab hiện tại thành app riêng). Bước 1 đã làm phần **A** (lõi + Jint) và **H** (build + liên kết web). Ở đây chỉ kiểm tra lại hai phần đó rồi làm tiếp B–G, I trên cùng dự án `unity/PhongLabHoa3D`.
> Đây là **Giai đoạn 2 của phòng lab**. Việc lớn, người dùng cho phép **làm lâu nhưng phải kĩ**.
> Làm **từng giai đoạn A → I**. Mỗi giai đoạn: làm xong, kiểm chứng, commit, cập nhật HANDOFF.md rồi mới sang giai đoạn sau.
> Phiên làm việc bị ngắt giữa chừng: đọc HANDOFF.md để làm tiếp đúng chỗ.

---

## DÀN Ý NHANH
1. **Hết giật lag bằng app ngoài:** làm một **ứng dụng máy tính** (macOS trước, Windows sau) bằng Unity, chạy ngoài trình duyệt giống cách Genshin Impact chạy.
   App dùng thẳng card đồ hoạ (Metal / DirectX) và **nhiều nhân CPU**. Trang web có nút "Mở bằng ứng dụng". Bản Web 3D giữ lại cho điện thoại và máy yếu.
2. **Toà nhà lab rộng, trần cao, các phòng nối nhau:**
   - Sảnh trung tâm cao 2 tầng có giếng trời.
   - 7 phòng chức năng quanh hành lang, vách kính và cửa kính tự mở, đứng phòng này nhìn sang được phòng kia.
3. **Đồ hoạ chân thật:**
   - Bố cục: URP chất lượng cao, ánh sáng bake + probe phản chiếu, vật liệu PBR từ thư viện CC0 (Poly Haven, ambientCG).
   - Dụng cụ: dựng **đúng kích thước thật** theo tiêu chuẩn ISO; thuỷ tinh/dung dịch có khúc xạ.
   - Nhân vật: người thật có hoạt hình (Mixamo hoặc Starter Assets).
4. **Bảng tuần hoàn vẽ lại hoàn toàn từ dữ liệu thật:**
   - 118 nguyên tố, bố cục SGK Hoá 10 (CT 2018), dữ liệu PubChem đối chiếu RDKit.
   - Một bảng lớn có đèn nền trong sảnh, bấm vào xem từng nguyên tố.
   - Dùng chung cho cả bản web.
5. **Điều khiển kiểu Genshin:** chuột xoay camera, WASD, Shift chạy, Space nhảy, góc nhìn 1 / 3. Tới bàn là camera bay vào cận cảnh.
6. **Không nhân đôi phần hoá học:** app chạy chính `tinh-toan.js` + `mo-phong.js` của web, qua trình thông dịch JS **Jint** nhúng trong C#.

---

## 0. Hiểu đúng yêu cầu "dùng CPU như Genshin" (đọc kĩ trước khi thiết kế)
Người dùng muốn: hết giật, đồ hoạ đẹp như game cài trên máy, "dùng trực tiếp CPU". Thực tế kĩ thuật:
- **Hình 3D do GPU vẽ. CPU lo logic, vật lí, hoạt hình và chuẩn bị lệnh vẽ.**
  Trình duyệt (Three.js hay Unity WebGL) bị giới hạn: gần như một luồng CPU, WebGL chậm hơn Metal/DirectX, bộ nhớ hạn chế. Đó là nguồn gốc giật lag.
- App máy tính (Unity Standalone) thì:
  - Vẽ bằng **Metal** (macOS) / **DirectX 12–Vulkan** (Windows).
  - Bật **Graphics Jobs** + kết xuất đa luồng.
  - Chạy mô phỏng và hạt bằng **Job System / Burst** trên nhiều nhân.
  - Tải phòng bất đồng bộ.

  Đó chính là "dùng CPU + GPU như Genshin". Thiết kế phải tận dụng đủ các điểm này (mục G).
- Ở báo cáo cuối, giải thích ngắn điều này cho người dùng bằng lời dễ hiểu.

## 1. Việc phải làm trước khi viết code (bắt buộc)
1. Đọc **AGENTS.md**, **CLAUDE.md**, **HANDOFF.md**, `unity/PhongThiNghiemHoaHoc/README.md`.
   Đọc thêm `PROMPT_PHONG_LAB_KHONG_GIAN_MO.md` và `PROMPT_PHONG_LAB_DIEU_KHIEN_GAME.md` để nắm thí nghiệm, bàn, cách vào bàn.
2. **Máy của người dùng** (Claude kiểm tra 24/10/2026): Apple **M2**, CPU 8 nhân, GPU 10 nhân, **RAM 8 GB**, ổ đĩa **còn khoảng 21 GB (đã dùng 90%)**.
   Unity **6000.6.7f1** có module **MacStandaloneSupport + WebGLSupport**, đang nằm ở `/Users/trantrong/Documents/6000.6.7f1/Unity.app`.
   Thư mục này nằm trong **iCloud** và có thể bị iCloud dọn khi ổ đầy. Chưa có module Windows.
   - Chạy `df -h ~`. **Còn dưới 15 GB thì DỪNG** và báo người dùng giải phóng ổ. Không tự xoá gì của người dùng.
   - Dùng RAM tiết kiệm: texture tối đa 2K, bake ánh sáng từng phòng, đóng Unity khi chạy test Python.
3. **Không làm việc với Unity trong iCloud:**
   - Bản làm việc đặt ở `~/UnityProjects/PhongLabHoa3D` (ngoài iCloud). `Library/`, `Builds/`, tài nguyên tải về đều nằm đó.
   - **Nguồn chuẩn** để commit vẫn ở repo: `unity/PhongLabHoa3D/` gồm `Assets/` (trừ tài nguyên ngoài), `Packages/`, `ProjectSettings/`.
   - Viết `tools/unity_dong_bo.sh` với 2 lệnh `keo` (repo → bản làm việc) và `day` (bản làm việc → repo, rsync loại trừ `Library Temp Logs Builds UserSettings Assets/TaiNguyenNgoai`).
     **Luôn `day` trước khi commit.**
4. Kết quả ban đầu của `pytest -q` và `node --test tests/js/`. Điểm phục hồi: `git add -A && git commit -m "chore: diem phuc hoi truoc ung dung lab Unity"`.
5. Giữ nguyên các luật cũ:
   - Không sửa vùng Lab Sinh, không sửa `.env`.
   - Không push/deploy khi chưa được đồng ý.
   - Không `grep -r` toàn repo.
   - Không sửa logic `tinh-toan.js` / `mo-phong.js`.
6. **Dự án Unity WebGL cũ `unity/PhongThiNghiemHoaHoc/` giữ nguyên** (test `test_du_an_unity_co_ma_nguon_va_script_build` phải còn PASS).
   App máy tính là **dự án Unity mới, riêng**, để bản WebGL nhẹ không bị kéo theo tài nguyên nặng.
7. **Không đăng nhập tài khoản thay người dùng** (Adobe/Mixamo, Unity Asset Store, Apple Developer).
   Việc cần đăng nhập thì ghi vào danh sách "VIỆC NGƯỜI DÙNG CẦN LÀM" (mục 12), dùng phương án dự phòng rồi làm tiếp phần khác.

## 2. Kiến trúc tổng
```
frontend/hoa-hoc/phong-thi-nghiem/      ← NGUỒN DỮ LIỆU + LÕI HOÁ HỌC DUY NHẤT (web và app dùng chung)
  tinh-toan.js, mo-phong.js, thi-nghiem/*.json, bang-ion.json
frontend/hoa-hoc/bang-tuan-hoan/
  nguyen-to.json                        ← MỚI: 118 nguyên tố (mục B)
  README.md                             ← nguồn dữ liệu, cách cập nhật
unity/PhongLabHoa3D/                    ← MỚI: app máy tính (Unity 6000.6.7f1, URP)
  Assets/Scripts/Loi/                   ← chạy JS lõi bằng Jint, luồng mô phỏng riêng
  Assets/Scripts/ToaNha/                ← dựng toà nhà từ toa-nha.json (editor) + cửa tự động, đèn phòng
  Assets/Scripts/NhanVat/               ← điều khiển kiểu Genshin, Cinemachine, hoạt hình
  Assets/Scripts/ThiNghiem/             ← 5 trạm thí nghiệm, dụng cụ, hiệu ứng
  Assets/Scripts/GiaoDien/              ← UI Toolkit: bảng thí nghiệm, sổ tay, câu hỏi, bảng tuần hoàn, cài đặt, bản đồ
  Assets/Scripts/HeThong/               ← chất lượng đồ hoạ, deep link, tự kiểm tra, nhật kí hiệu năng
  Assets/Editor/                        ← DongBoDuLieu, DungToaNha, BakeAnhSang, BuildMayTinh
  Assets/DuLieu/toa-nha.json            ← sơ đồ toà nhà (mục C)
  Assets/StreamingAssets/ptn/           ← BẢN SAO dữ liệu web, chép bằng Editor "Đồng bộ dữ liệu từ web" trước khi build
  Assets/TaiNguyenNgoai/                ← texture/HDRI/model CC0, Mixamo… (KHÔNG commit, xem mục 11)
  TAI_NGUYEN.json, GHI_CONG.md, THAM_KHAO.md, README.md
tools/tai_tai_nguyen_unity.py           ← tải lại tài nguyên ngoài theo TAI_NGUYEN.json (URL + giấy phép + sha256)
backend/phong_thi_nghiem.py, quan_tri_web.py, admin.js, giao-dien.js ← nút "Mở bằng ứng dụng" + cài đặt link tải (mục H)
```
- **Lõi hoá học là một:** app nạp `tinh-toan.js` + `mo-phong.js` + JSON qua **Jint** (gói NuGet, giấy phép BSD-2, hỗ trợ .NET Standard 2.0, chạy được trên Mono của Unity).
  Bản sao ở `StreamingAssets` phải giống hệt bản gốc: test so sha256 (mục I).
- Scripting backend **Mono** cho bản máy tính (Jint dùng reflection, an toàn hơn IL2CPP).
- **Phương án dự phòng chỉ khi Jint không chạy được** `mo-phong.js` (ví dụ cú pháp không hỗ trợ): port sang C#, kèm **bộ véc-tơ kiểm thử chung**.
  Chạy cùng kịch bản trên Node và trên C#, so từng số. Ghi rõ lí do trong HANDOFF.md.

## A. Dự án Unity mới + lõi hoá học (làm đầu tiên)
1. Tạo `unity/PhongLabHoa3D` bằng Unity 6000.6.7f1, template **URP**.
   Gói cần có: `com.unity.render-pipelines.universal`, `com.unity.cinemachine` (3.x), `com.unity.inputsystem`, `com.unity.ai.navigation`, `com.unity.burst`, `com.unity.collections`, `com.unity.test-framework`, `com.unity.shadergraph`.
2. Thêm Jint + phụ thuộc vào `Assets/Plugins/Jint/`, chỉ bật cho Editor + Standalone.
   `Loi/LoiHoaHoc.cs`:
   - Tạo `Engine`, gán `module = { exports: {} }` cho kiểu UMD, chạy `tinh-toan.js` rồi `mo-phong.js`.
   - Hàm `Tao(idThiNghiem)`, `ThaoTac(lenh, thamSo)`, `CapNhat(dt)`, `TrangThai()` trả JSON. Nghe sự kiện `trang_thai`, `so_tay`, `hieu_ung`.
3. **Luồng mô phỏng riêng:** một `Thread` (mỗi luồng một `Engine`) chạy `CapNhat` 30 lần/giây.
   Hàng đợi lệnh vào (thread-safe), ảnh chụp trạng thái ra luồng chính. Luồng chính chỉ đọc ảnh chụp mới nhất, không bao giờ chờ JS.
4. **Test EditMode** (`Assets/Tests/EditMode/LoiHoaHocTests.cs`), giá trị khớp với `tests/js/`:
   - pH HCl 0,1 M (V = 0 / 10 / 19,9 / 20 / 20,1 / 30 mL → 1,00 / 1,48 / 3,60 / 7,00 / 10,40 / 12,30).
   - CH₃COOH (V = 0 / 10 / 20 mL → 2,88 / 4,76 / 8,73).
   - Điện phân 2 A 965 s → 0,64 g Cu.
   - Pin Zn – Cu 1,103 V.
   - Chuẩn độ với phenolphthalein chuyển màu ở V ≈ 20,0 mL.

   Chạy: `Unity -batchmode -projectPath ~/UnityProjects/PhongLabHoa3D -runTests -testPlatform EditMode -testResults logs/unity-editmode.xml`.

## B. Bảng tuần hoàn – vẽ lại hoàn toàn (dùng chung web + app)
Bảng hiện tại (`phong-mo.js` hàm `_taoBangTuanHoan`) là **giả**: chỉ có ô màu `(col + r) % 7`, không có kí hiệu, không có họ lanthanide/actinide. Thay hoàn toàn.

1. **Dữ liệu `frontend/hoa-hoc/bang-tuan-hoan/nguyen-to.json`** (118 nguyên tố). Script tạo: `tools/tao_bang_tuan_hoan.py`.
   - Nguồn chính: PubChem `https://pubchem.ncbi.nlm.nih.gov/rest/pug/periodictable/JSON` (phạm vi công cộng; đã kiểm tra trả 118 dòng).
     Có các cột AtomicNumber, Symbol, Name, AtomicMass, ElectronConfiguration, Electronegativity, AtomicRadius, IonizationEnergy, OxidationStates, StandardState, MeltingPoint, BoilingPoint, Density, GroupBlock.
   - Lưu sẵn file gốc vào `tools/du_lieu/pubchem_periodictable.json` để chạy lại không cần mạng.
   - **Đối chiếu RDKit** (`.venv`): kí hiệu phải trùng `Chem.GetPeriodicTable().GetElementSymbol(z)`.
     Nguyên tử khối lệch ≤ 0,01 với Z ≤ 92 (nguyên tố phóng xạ nặng cho phép ghi số khối đồng vị bền nhất trong ngoặc vuông, ví dụ [226]).
   - Mỗi phần tử gồm:
     - Định danh: `z`, `ki_hieu`, `ten` (tên IUPAC theo CT 2018, ví dụ "Sodium"), `ten_viet` (tên quen dùng khi có: natri, sắt, đồng, bạc, vàng, thuỷ ngân, chì, thiếc, kẽm…).
     - Vị trí: `nguyen_tu_khoi` (hiển thị 2 chữ số thập phân, dấu phẩy), `chu_ky`, `nhom` (1–18), `nhom_ab` ("IA" … "VIIIA", "IB" … "VIIIB"), `khoi` (s/p/d/f), `loai`.
     - `loai` nhận một trong: kim loại kiềm, kim loại kiềm thổ, kim loại chuyển tiếp, kim loại nhóm p, á kim, phi kim, halogen, khí hiếm, lanthanide, actinide.
     - Tính chất: `cau_hinh_e` (dạng rút gọn có chỉ số trên, ví dụ "[Ar]3d⁶4s²", theo thứ tự lớp như SGK), `do_am_dien` (Pauling), `ban_kinh_pm`, `nang_luong_ion_hoa_1_eV`, `so_oxi_hoa`, `trang_thai_25C`, `t_nong_chay_K`, `t_soi_K`, `khoi_luong_rieng`.
   - **Bố cục theo SGK Hoá học 10 (CT GDPT 2018):** nhóm A/B kèm số 1–18; họ lanthanide và actinide tách thành 2 hàng dưới, có dấu * và **.
     Trước khi chốt, mở ảnh bảng tuần hoàn trong SGK Hoá 10 (sách điện tử hoặc ảnh chính thống trên mạng) để kiểm vị trí La/Ac và nhãn nhóm. Ghi nguồn đã đối chiếu vào `README.md`.
   - Màu theo `loai` lấy từ một bảng màu dịu, đủ tương phản cho cả nền sáng và tối (kiểm WCAG ≥ 4,5:1 cho chữ trên ô).
2. **Test `tests/test_bang_tuan_hoan.py`:**
   - Đủ 118, Z từ 1 đến 118 không trùng, kí hiệu khớp RDKit, nguyên tử khối đúng sai số.
   - Kiểm vị trí mẫu:
     - H (chu kì 1, IA), He (1, VIIIA), Na (3, IA), Fe (4, VIIIB), Cu (4, IB), Br (4, VIIA), I (5, VIIA).
     - Hg ở trạng thái lỏng, Br ở trạng thái lỏng.
     - Ce–Lu ở hàng lanthanide, Th–Lr ở hàng actinide.
   - Không có ô nào trùng toạ độ trên lưới. Mọi `cau_hinh_e` khớp tổng số electron = Z.
3. **Bản web:** `_taoBangTuanHoan` vẽ lại từ `nguyen-to.json`.
   - Canvas 2048×1152: tiêu đề, nhãn nhóm/chu kì, ô có Z, kí hiệu, tên, nguyên tử khối, màu theo loại, chú thích màu.
   - Đọc được khi đứng cách 2 m. Vẽ sau `document.fonts.ready`.
4. **Bản app:**
   - **Bảng lớn 7 × 3,6 m có đèn nền** trên tường sảnh trung tâm: mỗi ô là mesh + TextMeshPro, không phải một ảnh mờ.
   - Mỗi phòng có một poster nhỏ.
   - Lại gần nhấn **E** → mở **bảng tương tác toàn màn hình** (UI Toolkit):
     - Bấm một ô để mở thẻ chi tiết gồm cấu hình e, **mô hình nguyên tử lớp electron 3D** quay chậm, các tính chất, vị trí (ô/chu kì/nhóm) và loại nguyên tố.
     - Lọc theo khối s/p/d/f và theo loại.
     - Chế độ tô màu xu hướng (độ âm điện, bán kính, năng lượng ion hoá) theo nội dung Hoá 10 chương 2.

## C. Toà nhà lab: rộng hơn, trần cao hơn, các phòng nối nhau
1. **`Assets/DuLieu/toa-nha.json`** (đơn vị mét; y = 0 là sàn): kích thước toà, danh sách phòng (hình chữ nhật, cao trần), tường, vách kính, cửa (vị trí, rộng, kiểu tự động/khoá), cửa sổ, giếng trời, nội thất, trạm thí nghiệm. Sơ đồ đề xuất khoảng **36 × 30 m**:
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
   - **Trần:** phòng lab cao **5 m**. Có máng cáp, ống gió, ống hút khí độc **lộ trần** như lab hiện đại. Sảnh trung tâm cao **8 m**, giếng trời kính.
   - **Liên kết giữa các phòng:**
     - Vách kính từ sàn tới 2,6 m giữa hành lang và mọi phòng; có cửa sổ kính giữa hai phòng cạnh nhau. Đứng ở đâu cũng nhìn xuyên qua được.
     - **Cửa kính trượt tự mở** khi nhân vật tới gần 2 m.
     - Biển hướng dẫn treo trần ở hành lang ("← P. Định lượng · P. Điện hoá →").
   - Lối đi ≥ 1,8 m. Mọi trạm tới được: kiểm bằng **NavMesh** (gói AI Navigation), có test.
2. **Dựng bằng Editor script** `DungToaNha.cs` từ `toa-nha.json` ra cảnh `Assets/Scenes/ToaNhaLab.unity`.
   - Dùng bộ module (tường, cột, cửa, khung kính, ô trần, máng cáp) để tái tạo được.
   - Sau đó **bake ánh sáng** bằng `BakeAnhSang.cs` (batchmode): Progressive GPU Lightmapper, lightmap ≤ 2048, ≤ 8 atlas, bake từng phòng nếu thiếu RAM.
   - Cảnh tĩnh dùng lightmap; vật động dùng **Adaptive Probe Volumes**.
   - **Reflection probe dạng hộp** cho từng phòng để sàn bóng, kính, inox phản chiếu đúng.
3. **Nội thất chân thật** (tham khảo hình dáng ở noithathoaphat.com và banthinghiemlysonsaky.vn; **không tải file .max**, dựng lại):
   - Bàn thí nghiệm Hoá – Sinh: mặt phenolic xanh lam có gờ, tủ dưới vân gỗ, khung thép xám, chậu inox + vòi cổ ngỗng, giá thuốc thử 2 tầng.
   - Tủ hút khí độc có cửa kính kéo + đèn trong, tủ hoá chất 2 cánh có nhãn GHS, tủ an toàn sinh học.
   - Bàn tường, ghế đẩu, bồn rửa, giá phơi dụng cụ, tủ sấy, bàn giáo viên, màn hình tương tác.
   - Trạm rửa mắt/sen khẩn cấp, bình chữa cháy, tủ sơ cứu, thùng rác hoá chất.
4. **Vật liệu PBR thật:** texture **CC0** từ Poly Haven / ambientCG (sàn vinyl, gỗ, thép sơn, inox xước, gạch men, bê tông trần), HDRI CC0 cho bầu trời ngoài cửa sổ.
   Mọi tài nguyên ghi vào `TAI_NGUYEN.json` (URL, giấy phép, sha256) và `GHI_CONG.md`.
5. **Bàn trưng bày:** chỉ bàn chính của mỗi thí nghiệm là trạm làm được (viền sáng nhẹ). Bàn phụ có dụng cụ tĩnh, lại gần thì hiện "Bàn trưng bày – thí nghiệm ở bàn có viền sáng".

## D. Nhân vật + điều khiển kiểu Genshin Impact
1. **Nhân vật người thật** (không chibi), cao 1,65–1,75 m, có 2 lựa chọn nam/nữ.
   - Trang phục thường; tới tủ đồ bảo hộ nhấn E thì **mặc áo blouse trắng + kính bảo hộ + găng tay** (đổi lưới/vật liệu).
   - Nguồn ưu tiên:
     1. **Mixamo** (miễn phí, dùng được trong game; không phát tán file gốc riêng lẻ). Người dùng tự đăng nhập và tải theo danh sách ở mục 12.
     2. **Unity Starter Assets – Third Person** (Asset Store, miễn phí; người dùng thêm vào tài khoản).
     3. **Dự phòng khi chưa có hai nguồn trên:** người giản lược dựng bằng code, để vẫn chạy và test được.

     Đặt file vào `Assets/TaiNguyenNgoai/NhanVat/`, rig **Humanoid**.
   - Hoạt hình: đứng, đi, chạy, chạy nước rút, nhảy (bắt đầu/lơ lửng/tiếp đất), quay tại chỗ, với tay thao tác.
     Blend tree theo tốc độ, chân không trượt, **Foot IK** trên sàn.
2. **Điều khiển** (Input System, có hỗ trợ tay cầm):
   - **Chuột xoay camera**, khoá con trỏ (`Cursor.lockState = Locked`); giữ **Alt** để hiện chuột.
   - WASD đi theo hướng camera, nhân vật xoay mặt mượt theo hướng chạy. **Shift** chạy nước rút, **Ctrl** đi chậm, **Space** nhảy.
   - Lăn chuột để thu/phóng. **F**: góc nhìn 1 ⇄ 3. **V**: toàn cảnh (cắt trần, nhìn chéo từ trên). **M**: bản đồ toà nhà. **E**: tương tác. **Q**: rời bàn. **Esc**: menu.
   - Bấm sàn khi đang hiện chuột: tự đi bằng NavMesh.
   - Bỏ qua phím khi đang gõ chữ trong ô nhập của UI.
3. **Camera (Cinemachine 3):**
   - Góc 3: `CinemachineOrbitalFollow` + `CinemachineRotationComposer` + **`CinemachineDeoccluder`** (không xuyên tường, tự co lại). Tự xoay về sau lưng khi đi lâu (tắt được).
   - Góc 1: camera ở mắt, ẩn đầu bằng layer.
   - Độ nhạy chuột, đảo trục Y, FOV 60–90 chỉnh trong Cài đặt.
4. **Tới bàn = vào phòng thí nghiệm đó:**
   - Đứng trước bàn 0,6 s (hoặc nhấn E): nhả chuột → nhân vật tới chỗ đứng, quay mặt vào bàn → **blend Cinemachine 1 s** sang camera cận cảnh của trạm.
   - Đồng thời bật **Depth of Field** lấy nét vào dụng cụ + làm tối nhẹ xung quanh (Volume) → bảng thí nghiệm trượt ra.
   - Q để rời bàn, camera blend ngược về sau lưng nhân vật.
   - Bước qua cửa phòng: thẻ tên phòng kiểu game hiện 1,6 s.

## E. Dụng cụ trên bàn chân thật như ngoài đời + 5 thí nghiệm
1. **Tra cứu trên mạng** hình dáng và kích thước thật. Ghi link nguồn vào `THAM_KHAO.md`, chỉ học hình dáng; **không sao chép logo/thương hiệu**.
   Dựng procedural đúng kích thước (lathe/sweep trong C#, đủ độ chia đa giác, có độ dày thành kính thật).
   Kích thước tham khảo **phải kiểm lại theo tiêu chuẩn** trước khi dựng:

   | Dụng cụ | Kích thước tham khảo |
   |---|---|
   | Burette 50 mL hạng A, khoá PTFE (ISO 385) | dài tổng ~750 mm, vạch 0–50 mL chia 0,1 mL, có vạch số |
   | Bình tam giác cổ hẹp 250 mL (ISO 1773) | Ø đáy ~85 mm, cao ~145 mm, cổ Ø ~34 mm |
   | Cốc thuỷ tinh thấp 250 mL (ISO 3819) | Ø ~70 mm, cao ~95 mm, có mỏ và vạch |
   | Ống nghiệm | 16 × 160 mm; giá ống nghiệm |
   | Khác | giá đỡ + kẹp burette (đế ~200 × 130 mm, cọc ~600 mm), đèn cồn ~150 mL, dây Pt/nichrome thử lửa, lọ nhỏ giọt có nhãn, bình tia nước |
   | Điện hoá | thanh Zn/Cu 100 × 20 × 1 mm, thanh than chì Ø 6 × 150 mm, cầu muối chữ U, vôn kế/đồng hồ vạn năng, nguồn DC 0–30 V, dây dẫn có kẹp cá sấu |

2. **Shader Graph:**
   - **Thuỷ tinh:** khúc xạ bằng Scene Color + lệch pháp tuyến, fresnel, hấp thụ theo độ dày, cạnh kính ánh xanh nhẹ. Bật Opaque Texture trong URP.
   - **Dung dịch:** mặt khum, mức theo tham số, màu đổi mượt theo trạng thái lõi, tán xạ nhẹ.
   - **Kim loại:** PBR có vết xước. Vạch chia và nhãn dùng decal.
   - Thứ tự vẽ đúng giữa mặt sau kính → dung dịch → mặt trước kính.
3. **Hiệu ứng:** dùng VFX Graph (máy tính) hoặc Particle System.
   - Bọt khí nổi và vỡ, kết tủa lắng (mô phỏng hạt bằng Job + Burst), lớp kim loại bám dày dần trên cathode.
   - Ngọn lửa đèn cồn có đèn nhấp nháy và đổi màu khi thử lửa, electron chạy trên dây.
4. **5 trạm:** `an-toan`, `chuan-do`, `nhan-biet-ion`, `pin-galvani`, `dien-phan`.
   - Thao tác giống bản web (cùng lệnh `thaoTac` của `mo-phong.js`). Tham khảo `unity/PhongThiNghiemHoaHoc/Assets/Scripts/Canh/*.cs` để biết ánh xạ trạng thái → hình, nhưng dựng lại ở chất lượng cao.
   - Có **điểm nóng đánh số** trên dụng cụ, bấm để thao tác.

## F. Giao diện trong app (UI Toolkit, phong cách giống web)
- Màu theo web: sáng = Icy blue `#eef4fa` + Midnight Navy `#0f2b48`; tối = `#0f2438` + Sky Blue `#38bdf8`. Font **Be Vietnam Pro** + **JetBrains Mono** (giấy phép OFL, được nhúng).
- **Màn hình:**
  - Menu chính: Vào toà nhà · Chọn thí nghiệm · Bảng tuần hoàn · Cài đặt · Thoát.
  - HUD khi đi: tên phòng, bản đồ nhỏ, gợi ý phím E. Bản đồ toà nhà M: bấm phòng để tự đi hoặc dịch chuyển nhanh.
  - **Bảng thí nghiệm** khi tại bàn: các tab Điều khiển / Sổ tay / Câu hỏi / Thông tin.
    Tab Điều khiển làm lại từng nút như `BANG[id]` trong `giao-dien.js`. Câu hỏi 3 dạng lấy từ JSON, có chấm điểm.
  - Bảng tuần hoàn tương tác (mục B).
- **Cài đặt:**
  - Đồ hoạ: Thấp / Vừa / Cao / Rất cao / Tự động, độ phân giải, tỉ lệ dựng hình (STP), giới hạn fps, VSync, hiện fps.
  - Điều khiển: độ nhạy, đảo Y, FOV. Âm thanh. Ngôn ngữ chỉ tiếng Việt.

  Lưu vào `PlayerPrefs`.
- Âm thanh (nên có): bước chân, tiếng phòng, tiếng thuỷ tinh, cửa mở; chỉ dùng nguồn **CC0** (Kenney, Freesound bản CC0), ghi công.

## G. Hiệu năng + dùng đa nhân CPU/GPU (lí do làm app)
- **Player Settings:**
  - Graphics API Metal (macOS) / DX12 + Vulkan (Windows).
  - **Graphics Jobs bật**, đa luồng kết xuất.
  - Mono, .NET Standard 2.1, Incremental GC.
- **URP:**
  - Forward+, **GPU Resident Drawer** + **GPU Occlusion Culling**, SRP Batcher, SSAO, MSAA 4× hoặc TAA.
  - **STP** (upscale) cho máy yếu, Bloom nhẹ, tonemapping ACES, color grading cho căn phòng sáng sạch.
- **Tổ chức cảnh:**
  - **Occlusion culling bake** theo phòng. LOD Group cho đồ nhiều chi tiết.
  - **Tải từng phòng bất đồng bộ** (cảnh additive hoặc Addressables).
  - Mipmap streaming cho texture. Vật lặp dùng instancing.
- **CPU đa nhân:** lõi hoá học ở luồng riêng (mục A). Hạt kết tủa và bọt khí bằng **Job System + Burst**. Hoạt hình nhân vật bằng Animator (Unity tự đa luồng).
- **Tự chọn chất lượng** theo `SystemInfo` (GPU, VRAM, số nhân). Mục tiêu đo bằng Profiler và ghi vào HANDOFF.md:

  | Máy | Chế độ | Mục tiêu |
  |---|---|---|
  | MacBook M2 8 GB (máy người dùng), 1920×1200 | Cao | **≥ 60 fps**, 1% low ≥ 45; luồng chính ≤ 8 ms; RAM app ≤ 2,5 GB |
  | Cùng máy | Rất cao | ≥ 45 fps |
  | Laptop Windows card tích hợp, 1080p | Thấp + STP 67% | ≥ 30 fps |

- Mở app lần đầu ≤ 15 s; vào phòng khác không khựng (khung dài nhất < 50 ms khi đi qua cửa).
- Kích thước bản build macOS ≤ 1 GB.

## H. Build app + liên kết với trang web
1. **`Assets/Editor/BuildMayTinh.cs`:**
   - `BuildMayTinh.Mac()` ra `~/UnityProjects/PhongLabHoa3D/Builds/macOS/PhongLabHoa3D.app` (Apple Silicon + Intel). Có `BuildMayTinh.Windows()` (chỉ chạy khi đã cài module Windows).
   - Trước khi build tự gọi "Đồng bộ dữ liệu từ web" và dựng/bake lại nếu `toa-nha.json` đổi.
   - Ký **ad-hoc** (`codesign --force --deep -s -`). Ghi hướng dẫn mở lần đầu (chuột phải → Mở) vào README.
     Notarize cần tài khoản Apple Developer: ghi vào việc người dùng quyết định.
2. **Deep link** `phonglabhoa://mo?tn=<id>`:
   - macOS: Player Settings → Supported URL schemes, nhận bằng `Application.deepLinkActivated` / `Application.absoluteURL`.
   - Windows: lần chạy đầu ghi khoá `HKCU\Software\Classes\phonglabhoa`, đọc tham số dòng lệnh.
   - Nhận `tn` hợp lệ (1 trong 5 id) thì xuất hiện ngay ở trạm đó. Tham số lạ bỏ qua (có test parser).
3. **Web:**
   - **Admin:** `app_settings` khoá `"ung_dung_lab"` = `{bat: false, url_mac: "", url_win: "", phien_ban: "", ghi_chu: ""}`.
     - `GET/POST /admin/api/ung-dung-lab` (chỉ admin + CSRF, ghi nhật kí).
     - Khối cài đặt nằm trong thẻ "Phòng thí nghiệm" ở `/admin`, theo đúng mẫu công tắc Unity đã có trong `backend/phong_thi_nghiem.py`.
   - **API người dùng:** `GET /api/phong-thi-nghiem/ung-dung` (đã đăng nhập) → `{bat, url_mac, url_win, phien_ban}`. Chỉ lộ link khi `bat`.
   - **Trang lab:** nút "🖥 Mở bằng ứng dụng (mượt hơn, đồ hoạ cao)" hiện khi `bat` và máy là macOS/Windows.
     - Bấm thì thử mở `phonglabhoa://mo?tn=<id>`.
     - Sau 1,5 s trang vẫn còn hiển thị (không mất focus) thì hiện hộp "Chưa cài ứng dụng?": link tải theo hệ điều hành + 3 bước cài.
   - **Bản tải không để trong repo** (quá nặng). Đề xuất GitHub Releases hoặc Google Drive; **người dùng chọn**, admin dán link.
   - Bản Web 3D và bản Unity WebGL cũ giữ nguyên.
4. **Test Python** `tests/test_ung_dung_lab.py`: mặc định tắt; học sinh gọi API admin → 403; thiếu CSRF → 403; chưa đăng nhập → 401; tắt thì không lộ URL; admin bật thì học sinh thấy.

## I. Tự kiểm tra tự động + kiểm chứng bằng mắt
1. **Chế độ tự kiểm tra trong app:** `PhongLabHoa3D.app/Contents/MacOS/PhongLabHoa3D -tu-kiem-tra <thư mục ra>`.
   - Đi theo lộ trình cố định: cửa chính → sảnh → từng phòng → vào từng trạm.
   - Thực hiện kịch bản thí nghiệm qua lõi:
     - chuẩn độ HCl đến khi chuyển hồng, V ≈ 20,0 mL;
     - nhận biết 4 ống;
     - pin Zn – Cu 1,10 V, rút cầu muối về 0 V;
     - điện phân 2 A 965 s → 0,64 g.
   - Chụp PNG ở các điểm nhìn cố định: toàn cảnh, sảnh có bảng tuần hoàn, từng phòng, từng trạm cận cảnh, góc 1, góc 3, bảng tuần hoàn tương tác.
   - Ghi `bao-cao.json`: fps trung bình, 1% low, ms luồng chính/kết xuất, draw call, RAM, thời gian tải, kết quả từng kịch bản (đạt/không). Xong thì tự thoát.
2. **Test Unity EditMode:**
   - Lõi hoá học (mục A).
   - `toa-nha.json` hợp lệ: phòng không chồng nhau, mọi cửa nằm trên tường.
   - NavMesh nối được mọi trạm từ cửa chính.
   - Bản sao `StreamingAssets/ptn` trùng sha256 với `frontend/`.
   - Bảng tuần hoàn nạp đủ 118.
   - Parser deep link đúng.
3. **Gemini phải tự mở từng ảnh ra xem.** Ghi vào HANDOFF.md một dòng mô tả thật mỗi ảnh. Ảnh không thể hiện đúng thứ cần thấy thì phần đó **chưa xong**.
   Bài học từ lần trước: ảnh "toàn cảnh" từng chỉ là màu nền.
4. Cuối cùng: `pytest -q`, `node --test tests/js/`, test EditMode Unity đều PASS.

## 10. Tài nguyên ngoài + bản quyền
- **Chỉ dùng:**
  - CC0: Poly Haven, ambientCG, Kenney, Freesound CC0.
  - Font OFL.
  - Mixamo (theo điều khoản Adobe: dùng trong sản phẩm, không phát tán file gốc).
  - Unity Starter Assets (Unity Companion License).
  - Jint (BSD-2).
  - Dữ liệu PubChem (phạm vi công cộng).
- **Không** dùng model/ảnh có bản quyền (model .max của Hòa Phát, ảnh của Lý Sơn Sa Kỳ, logo hãng dụng cụ).
- `Assets/TaiNguyenNgoai/` **không commit** (thêm vào `.gitignore`). `TAI_NGUYEN.json` liệt kê URL + giấy phép + sha256. `tools/tai_tai_nguyen_unity.py` tải lại được (Mixamo thì in hướng dẫn tải tay).

## 11. Commit theo giai đoạn
`A lõi + Jint + test` → `B bảng tuần hoàn (data + web + app)` → `C toà nhà + bake` → `D nhân vật + điều khiển` → `E dụng cụ + 5 trạm` → `F giao diện` → `G hiệu năng` → `H build + liên kết web + test` → `I tự kiểm tra + tài liệu`.
- Mỗi giai đoạn: `tools/unity_dong_bo.sh day` → test → commit → cập nhật HANDOFF.md (ĐÃ XONG / ĐANG LÀM / VIỆC TIẾP THEO / LỖI CÒN TỒN / CÁCH CHẠY).
- Sửa đoạn "## Phòng thí nghiệm Hoá ảo" trong **CLAUDE.md**: thêm app máy tính, `nguyen-to.json`, route mới, khoá `app_settings` mới, test mới.
- **Chưa push hoặc deploy.**

## 12. VIỆC NGƯỜI DÙNG CẦN LÀM (Gemini ghi lại, nhắc người dùng; không tự làm thay)
1. **Giải phóng ổ đĩa** để còn ≥ 20 GB trống (hiện khoảng 21 GB, đã dùng 90%; Unity + bake + build có thể chiếm 8–12 GB).
2. (Nên làm) **Cài lại Unity 6000.6.7f1 bằng Unity Hub vào `/Applications/Unity/Hub/Editor`** thay vì thư mục Tài liệu trong iCloud, kèm module Mac Build Support.
   (Tuỳ chọn) thêm **Windows Build Support (Mono)** nếu muốn bản Windows.
3. **Mixamo** (đăng nhập Adobe ID miễn phí): tải 1 nhân vật nam + 1 nữ dáng người thật, *FBX for Unity, With Skin*.
   Tải thêm các hoạt hình *Idle, Walking, Running, Fast Run, Jump, Falling Idle, Landing, Left/Right Turn, Reaching* ở dạng *Without Skin, 30 fps*.
   Đặt vào `~/UnityProjects/PhongLabHoa3D/Assets/TaiNguyenNgoai/NhanVat/`.
4. (Tuỳ chọn) Thêm **Starter Assets – Third Person** (Unity Asset Store, miễn phí) vào tài khoản Unity.
5. Chọn nơi để file tải app (GitHub Releases / Google Drive), rồi dán link trong `/admin`. Quyết định có mua Apple Developer để notarize hay không.

## 13. Điều kiện "XONG"
- [ ] App macOS mở được bằng nút trên web (deep link) và bằng tay. Chạy mượt đạt mục tiêu mục G trên máy M2 của người dùng, có số đo Profiler.
- [ ] Toà nhà khoảng 36 × 30 m: phòng lab trần 5 m, sảnh 8 m có giếng trời, 7 phòng nối nhau bằng hành lang, vách kính và cửa kính tự mở. Ánh sáng bake, phản chiếu đúng, vật liệu PBR thật.
- [ ] Nhân vật người thật có hoạt hình mượt (hoặc đã có dự phòng + danh sách việc người dùng cần tải). Điều khiển kiểu Genshin đủ phím, góc 1/3, toàn cảnh, không xuyên tường.
- [ ] Dụng cụ đúng kích thước thật, thuỷ tinh/dung dịch có khúc xạ. 5 thí nghiệm làm trọn trong app với **cùng kết quả số** như bản web.
- [ ] Bảng tuần hoàn 118 nguyên tố đúng bố cục SGK, dữ liệu đã đối chiếu RDKit. Có bảng lớn trong sảnh, bảng tương tác trong app, poster web vẽ lại thật.
- [ ] Web: nút mở app, admin dán link tải, API có test. Bản Web 3D và Unity WebGL cũ vẫn chạy, test cũ PASS.
- [ ] Ảnh tự kiểm tra đã được mở xem và mô tả đúng. `bao-cao.json` đạt hết.
- [ ] HANDOFF.md, CLAUDE.md, các README cập nhật. Commit đủ 9 giai đoạn. **Chưa push hoặc deploy.**
