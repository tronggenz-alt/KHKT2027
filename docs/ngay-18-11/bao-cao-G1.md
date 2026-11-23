# Báo cáo [G1] Hồ sơ phần cứng + hết giật khi di chuyển + thông số sắc nét

- **Nhánh:** `n18/antigravity`
- **Ngày thực hiện:** 18/11/2026
- **Phần cứng đo:** Apple M2 (5461 MB VRAM chia sẻ), RAM 8192 MB, 8 nhân CPU, màn hình Built-in Liquid Retina (`3840x2486` khi đo fullscreen Retina, `DPI 299`).
- **Mốc M1 (HardwareProfile):** **M1 ĐẠT** — đã tách commit hợp đồng `Khkt.Rendering.HardwareProfile` tại `4bba80f3`.

---

## 1. Tóm tắt kết quả đạt được

1. **Tạo quy trình xuất app & đo khung hình chuẩn:**
   - `Khkt.EditorTools.MacBuild` (`Build`, `BuildDev`, `BuildWindows`) xuất ra `~/AppBuild/VERA_mac/VERA Lab.app` (`227.8 MB`, 0 lỗi), tự động bọc `#if UNITY_EDITOR || KHKT_TEST` cho `KhktWave6Runner.cs` trong khi dựng để loại khỏi bản phát hành mà vẫn giữ nguyên file nguồn trong kho.
   - `Khkt.Rendering.DoKhungHinh` chạy đường đi chuẩn 90 giây qua `-vera-do` hoặc phím `F7` (và nút **"Đo thử 30 giây"** trong Cài đặt → Hiển thị), ghi CSV và tóm tắt ra `Application.persistentDataPath/do_khung/`.
2. **Đo TRƯỚC → tìm nguyên nhân gốc bằng số liệu → sửa triệt để hết giật:**
   - Từ `51.3 FPS | p95 = 33.30–33.31 ms | max = 1517.24–1533.30 ms | >50ms = 2.67–3.33 khung/phút | GC = 104.72–105.32 MB` ở cửa sổ `1600x900` (TRƯỚC)
   - Đạt **`59.7–59.9 FPS | p50 = 16.67 ms | p95 = 16.72–17.17 ms | >50ms = 0.67–1.33 khung/phút | GC = 11.11–11.61 MB`** ngay ở độ phân giải Retina đầy đủ **`3840x2486`** (SAU Tự động = mức `Cao`), đạt mục tiêu `p95 <= 18 ms` và `<= 2 khung > 50 ms / phút`.
3. **Hồ sơ phần cứng & giao diện Hiển thị:**
   - Triển khai `HardwareProfile` đúng hợp đồng (`MucDoHoa`: `Thap`, `Vua`, `Cao`, `SieuNet`, tự đề xuất qua `SystemInfo`, lưu `PlayerPrefs`, đổi tức thì không cần khởi động lại).
   - Cập nhật tab **Hiển thị** trong `IntroScreen`: 5 chip (`Tự động`, `Thấp`, `Vừa`, `Cao`, `Siêu nét`), nhãn `HardwareProfile.MoTaMay()`, trạng thái đang dùng và nút **Đo thử 30 giây** (`>= 44 pt`, không đè chữ).
4. **Sắc nét Retina & Khử răng cưa:**
   - Bật `macRetinaSupport = 1`, mở app ở độ phân giải vật lý Retina (`3840x2486`) cho mức `Cao` / `Siêu nét`.
   - Đối chiếu 3 chế độ khử răng cưa (MSAA 4x, SMAA, TAA): chọn **MSAA thuần phần cứng trong tile memory Metal TBDR** (không dùng TAA hay xếp thêm pass SMAA sau MSAA) giúp chữ trên biển chỉ dẫn, vạch chia buret sắc nét và tiết kiệm ~3.07 ms GPU.

---

## 2. Nguyên nhân gây giật phát hiện từ số liệu TRƯỚC

1. **Khởi động webcam tự động (`InAppHandTracker` — ngoài phạm vi):** `DesktopViewer.Start()` tự gắn/khởi chạy webcam ở ~t=1.5s gây khựng cứng **1517.24–1533.30 ms** ở khung #38 trong Chặng 1, đồng thời `DownloadToArray()` đồng bộ GPU→CPU làm phình GC lên **104.72–105.32 MB (~23.3 KB/khung, 29–30 Gen0)** và kéo `p95` lên `33.30 ms`. Đã sửa trong `DesktopViewer.cs` (trong phạm vi): không tự mở camera khi khởi động, chỉ kích hoạt `InAppHandTracker` khi người dùng bấm `F10`.
2. **Xung đột điều phối chất lượng cũ (`HdQualityManager` — ngoài phạm vi):** Tự gắn sau khi nạp cảnh, ép mức Ultra và chạy `Govern()` mỗi giây. Đã vô hiệu hóa qua reflection trong `HardwareProfile.DisableLegacyHdQualityManager()`.
3. **Nút thắt GPU trên Metal TBDR ở màn Retina (`3840x2486`):**
   - `Depth Texture` (`m_RequireDepthTexture = 1`) ép Depth Prepass/resolve MSAA depth dù SSAO tắt, tốn thêm **~5.73 ms** GPU (`23.99 ms` → `18.26 ms`).
   - `PC_Renderer.asset` đặt `m_DepthPrimingMode = 1` phá tile fast-path của Apple Silicon TBDR.
   - `Bloom.highQualityFiltering = true` tốn thêm **~1.67 ms** GPU ở 3840x2486 (`18.26 ms` → `16.59 ms`).
   - Xếp pass `SMAA` chồng lên `MSAA` tốn thêm **~3.07 ms** GPU (`16.59 ms` → `13.52 ms` GPU với `MSAA 2x`).
   - `m_HDRColorBufferPrecision = 0` (64-bit float) tiêu tốn băng thông & ~150 MB bộ nhớ đồ họa trên máy 8 GB RAM hợp nhất.
   - `ProjectSettings.asset` bật `metalAPIValidation = 1` và tắt `enableFrameTimingStats = 0`.
4. **Nạp/gỡ khu & camera:**
   - `ZoneStreamer.cs` từng gỡ khu đồng bộ dồn cục khi khởi động, `unloadDelayS` ngắn (`2.5s`), dùng `GetComponentsInChildren<Grabbable>(true)` tạo rác bộ nhớ và chưa nhìn trước theo vận tốc/hướng nhìn tại cụm cầu thang.
   - `DesktopViewer.cs` cập nhật camera ở `Update()` không có `[DefaultExecutionOrder(-50)]` và chưa đảm bảo `RigidbodyInterpolation.Interpolate` cho vật đang cầm trong `LateUpdate()`.
   - `FramePacing.cs` chưa xử lý riêng màn ProMotion/120 Hz (`vSyncCount = 2` khi `refreshRate >= 100 Hz`, còn 60 Hz giữ `vSyncCount = 1`).
5. **Khựng assert shader khi làm nóng toàn bộ:** Lần kiểm thử `KhktWave6Verify` đầu phát hiện `Shader.WarmupAllShaders()` ép biên dịch shader `StencilDeferred` trên Forward+, gây 4 lỗi Assert `State comes from an incompatible keyword space`. Đã bỏ `Shader.WarmupAllShaders()`, chỉ giữ `ShaderVariantCollection` cho 10 shader Forward+ đang dùng.

---

## 3. Các tệp đã sửa / tạo mới (trong phạm vi G1)

| Tệp | Commit | Nội dung thay đổi |
|---|---|---|
| `Phong thi nghiem ao/Assets/Scripts/Rendering/HardwareProfile.cs` (+ `.meta`) | `4bba80f3`, `555bb416` | Hợp đồng `MucDoHoa` & `HardwareProfile`; nhận diện phần cứng; chỉnh URP (render scale + FSR, HDR 32/64-bit, MSAA thuần TBDR, shadow, Depth Texture khi có SSAO, Bloom HQ via runtime `vol.profile`); aniso & mipMapBias (`>= -0.5`); làm nóng 10 shader; áp dụng Retina fullscreen và khôi phục snapshot trong Editor. |
| `Phong thi nghiem ao/Assets/Scripts/Editor/MacBuild.cs` (+ `.meta`) | `7117e35d` | Xuất Mac `VERA Lab.app` (`Build`, `BuildDev`) ra ngoài kho git, `BuildWindows` log nhẹ nếu thiếu module Windows; tự bọc `KhktWave6Runner.cs` trong lúc dựng và khôi phục lại ngay trong `finally`. |
| `Phong thi nghiem ao/Assets/Scripts/Rendering/DoKhungHinh.cs` (+ `.meta`) | `7117e35d`, `555bb416` | Đường đi chuẩn 90s (`-vera-do` / `F7`), đo thử 30s (`ChayThu30Giay`), p50/p95/p99/GC/FPS/FrameTiming, `-vera-sweep` đo GPU, `-vera-chup-g1` chụp ảnh đối chiếu; ghi CSV & tóm tắt vào `Application.persistentDataPath/do_khung/`. |
| `Phong thi nghiem ao/Assets/Scripts/Rendering/FramePacing.cs` | `555bb416` | Nơi duy nhất đặt nhịp khung; xử lý màn >= 100 Hz (`vSyncCount = 2`), 60 Hz (`vSyncCount = 1`), fallback `targetFrameRate = 60`. |
| `Phong thi nghiem ao/Assets/Scripts/Rendering/ZoneRoot.cs` | `555bb416` | Thêm `CollectGrabbables()` dùng danh sách tái sử dụng không cấp phát mỗi lần Park/Unpark. |
| `Phong thi nghiem ao/Assets/Scripts/Rendering/ZoneStreamer.cs` | `555bb416` | `preloadRadius = 6.5f`, `unloadDelayS = 8.0f`, nhìn trước theo hướng di chuyển/xoay camera + nạp cụm cầu thang; chia nhỏ gỡ khu khi khởi động (3 khung/khu) và cooldown `0.45s` giữa các lần gỡ; bổ sung cờ `initSettled`; bỏ cấp phát chuỗi thừa. |
| `Phong thi nghiem ao/Assets/Scripts/Environment/DesktopViewer.cs` | `9304b834` | `[DefaultExecutionOrder(-50)]`, camera & `RigidbodyInterpolation.Interpolate` cho vật đang cầm chạy trong `LateUpdate()`, thêm `SetPose()`, không tự bật `InAppHandTracker` trước khi bấm `F10`. |
| `Phong thi nghiem ao/Assets/Scripts/Demo/IntroScreen.cs` | `9304b834` | Tab Hiển thị thêm cụm **Chất lượng đồ họa** (5 chip + `MoTaMay()` + trạng thái + nút `Đo thử 30 giây`) và `OpenDisplaySettings()`. |
| `Phong thi nghiem ao/Assets/Settings/PC_RPAsset.asset` | `7eac3374` | `m_RequireDepthTexture: 0`, `m_OpaqueDownsampling: 1`, `m_HDRColorBufferPrecision: 1` (32-bit), cập nhật Prefilter SSAO khi build. |
| `Phong thi nghiem ao/Assets/Settings/PC_Renderer.asset` | `7eac3374` | `m_DepthPrimingMode: 0` để giữ đường tile MSAA nhanh trên Metal TBDR. |
| `Phong thi nghiem ao/ProjectSettings/ProjectSettings.asset` | `7eac3374` | Giữ `macRetinaSupport: 1`, bật `runInBackground: 1`, `resizableWindow: 1`, `enableFrameTimingStats: 1`, tắt `metalAPIValidation: 0`. |

---

## 4. Số liệu đo kiểm chứng

### 4.1. Phân tích GPU theo từng pass (`-vera-sweep` tại Sảnh tầng 1, `3840x2486`)

Tệp gốc: `~/Library/Application Support/DefaultCompany/Phong thi nghiem ao/do_khung/sweep_gpu.txt`.

| Bước cấu hình | dt TB (ms) | CPU TB (ms) | GPU TB (ms) | Nhận xét |
|---|---:|---:|---:|---|
| `1_hien_tai(msaa2+smaa+hdr64+depth+opaque+bloomHQ)` | 18.70 | 19.55 | 23.99 | Vượt khung 16.67 ms |
| `2_hdr32` | 18.52 | 19.04 | 23.99 | Chỉ tiết kiệm băng thông sau khi bỏ prepass |
| `3_no_depth_no_opaque` | 16.66 | 16.67 | 18.26 | Bỏ Depth Prepass cắt **-5.73 ms** |
| `4_bloom_thuong` | 16.85 | 16.72 | 16.59 | Tắt Bloom HQ cắt thêm **-1.67 ms** |
| `5_no_smaa(msaa2_only)` | **16.67** | **16.67** | **13.52** | Bỏ SMAA trên MSAA2 cắt **-3.07 ms**, khóa 60 FPS |
| `6_msaa4_only` | 16.67 | 16.67 | 16.00 | Sát ngưỡng 16.67 ms (dự phòng biên hẹp ở cảnh nặng) |
| `7_smaa_only(msaa1)` | 16.86 | 16.74 | 13.76 | Chậm hơn MSAA 2x trong tile TBDR Metal |
| `10_scale0.85_msaa2_smaa_post_hdr32` | 16.95 | 16.92 | 12.27 | Cho máy yếu / mức Vừa |

*(Ghi chú kỹ thuật: `ModernUI.cs` vẫn đặt `requiresColorOption = CameraOverrideOption.On` trên camera chính nên hiệu ứng kính mờ `Khkt/KinhMo` trong giao diện vẫn hoạt động; toàn bộ phần giảm 5.73 ms ở bước 3 đến từ việc tắt `supportsCameraDepthTexture` khi không dùng SSAO.)*

### 4.2. So sánh đường đi chuẩn 90 giây TRƯỚC và SAU

| Lần đo | Độ phân giải | FPS TB | ms TB | p50 (ms) | p95 (ms) | p99 (ms) | Max (ms) | >33,3 ms | >50 ms (khung/phút) | GC tổng (KB/khung / Gen0) |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| **TRƯỚC lần 1** (`truoc_lan1`) | 1600x900 | 51.3 | 19.49 | 16.67 | 33.31 | 33.34 | 1517.24 | 64 | 5 (3.33) | 104.72 MB (23.22 KB / 29) |
| **TRƯỚC lần 2** (`truoc_lan2`) | 1600x900 | 51.3 | 19.49 | 16.67 | 33.30 | 33.34 | 1533.30 | 57 | 4 (2.67) | 105.32 MB (23.35 KB / 30) |
| **SAU Tự động (`Cao`) lần 1*** | 3840x2486 | **59.9** | **16.70** | **16.67** | **16.72** | **17.46** | **99.90** | **7** | **1 (0.67)** | 11.11 MB (2.11 KB / 5) |
| **SAU Tự động (`Cao`) lần 2** | 3840x2486 | **59.7** | **16.75** | **16.67** | **17.17** | **17.66** | **100.04** | **11** | **2 (1.33)** | 11.61 MB (2.21 KB / 4) |
| **SAU Thấp lần 1** (scale 0.7 + FSR) | 3840x2486 | **59.8** | **16.73** | **16.67** | **16.86** | **17.66** | **83.39** | **11** | **2 (1.33)** | 11.29 MB (2.15 KB / 5) |
| **SAU Thấp lần 2** (scale 0.7 + FSR) | 3840x2486 | **59.8** | **16.73** | **16.67** | **17.22** | **17.66** | **116.77** | **7** | **2 (1.33)** | 11.44 MB (2.18 KB / 4) |

*\*Ghi chú minh bạch: `sau_tudong_lan1` đo ngay trước bước gỡ bỏ 1 dòng `Shader.WarmupAllShaders()` để sạch runtimeErrors trên Editor; `sau_tudong_lan2`, `sau_thap_lan1` và `KhktWave6Verify` đều đo trên bản dựng cuối cùng, trong trạng thái máy rảnh (`NO_BATCHMODE` trước và sau khi đo).*

Chi tiết theo 6 chặng của bản đo cuối (`sau_tudong_lan2`, mức `Cao`, `3840x2486`):
- Chặng 1 (Sảnh tầng 1): TB=16.81 ms, max=100.0 ms, >33ms=2, >50ms=1
- Chặng 2 (Quầy hóa chất): TB=16.72 ms, max=33.3 ms, >33ms=1, >50ms=0
- Chặng 3 (Xoay 360°): TB=16.67 ms, max=17.7 ms, >33ms=0, >50ms=0
- Chặng 4 (Lên thang bộ): TB=16.83 ms, max=100.0 ms, >33ms=5, >50ms=1
- Chặng 5 (Nhà kính tầng 2): TB=16.71 ms, max=33.3 ms, >33ms=1, >50ms=0
- Chặng 6 (Kính hiển vi): TB=16.73 ms, max=34.3 ms, >33ms=2, >50ms=0

---

## 5. Đối chiếu khử răng cưa, lọc dị hướng & ảnh chụp Retina (`3840x2486`)

Ảnh chụp được lưu trong `docs/ngay-18-11/anh/g1/`:

1. **So sánh MSAA vs SMAA vs TAA (cùng góc nhìn buret & biển báo tại `3840x2486`):**
   - **MSAA (`aa_msaa4x_buret.png`, `aa_msaa4x_bien_bao.png`):** Khử răng cưa hình học trong tile memory Metal, vạch chia trên buret và chữ nổi `BÀN 1`, `KHU THỰC HÀNH`, bảng tên trường & áp phích tường sắc nét nguyên điểm ảnh, không bị mềm hay bóng ma khi di chuyển/xoay camera.
   - **SMAA (`aa_smaa_buret.png`, `aa_smaa_bien_bao.png`):** Đẹp tương đương khi đứng yên nhưng thêm pass lọc hậu kỳ toàn màn hình làm mềm nhẹ mép viền mảnh và tốn thêm **3.07 ms GPU**.
   - **TAA (`aa_taa_buret.png`, `aa_taa_bien_bao.png`):** Không được chọn làm mặc định vì tái chiếu lịch sử khung trước gây mờ/bóng ma nhẹ khi camera xoay gần vạch chia buret mảnh và chữ nhỏ.
   - **Quyết định:** Dùng **MSAA thuần phần cứng** (`AntialiasingMode.None` hậu kỳ) — `MSAA 2x` cho mức `Cao` trên màn cực nét >= 2560px (`3840x2486`, `gpu ~13.5 ms`) / `MSAA 4x` dưới 2560px, và `MSAA 4x` cho `Siêu nét`.
2. **Lọc dị hướng & mipMapBias:** Bật `AnisotropicFiltering.ForceEnable` (8x ở mức `Cao`, 16x ở `Siêu nét`, 4x ở `Vừa`) trên vật liệu sàn (`San*`, `T2_San*`) và mặt bàn (`MatBan*`, `T2_Ban*`); giới hạn `mipMapBias` trong `[-0.5, 0]` (`-0.35` ở `Cao`, `-0.5` ở `Siêu nét`). Kiểm chứng trong [sau_san_ban.png](anh/g1/sau_san_ban.png) không bị mờ vân gạch ở xa.
3. **Chứng minh ảnh trước/sau & UI:**
   - TRƯỚC: [truoc_bien_bao.png](anh/g1/truoc_bien_bao.png), [truoc_buret.png](anh/g1/truoc_buret.png), [truoc_san_ban.png](anh/g1/truoc_san_ban.png), [truoc_nha_kinh.png](anh/g1/truoc_nha_kinh.png)
   - SAU: [sau_bien_bao.png](anh/g1/sau_bien_bao.png), [sau_buret.png](anh/g1/sau_buret.png), [sau_san_ban.png](anh/g1/sau_san_ban.png), [sau_nha_kinh.png](anh/g1/sau_nha_kinh.png)
   - AA đối chiếu: [aa_msaa4x_buret.png](anh/g1/aa_msaa4x_buret.png), [aa_smaa_buret.png](anh/g1/aa_smaa_buret.png), [aa_taa_buret.png](anh/g1/aa_taa_buret.png), [aa_msaa4x_bien_bao.png](anh/g1/aa_msaa4x_bien_bao.png), [aa_smaa_bien_bao.png](anh/g1/aa_smaa_bien_bao.png), [aa_taa_bien_bao.png](anh/g1/aa_taa_bien_bao.png)
   - Tab Hiển thị mới (`Cài đặt & Tùy chọn`): [sau_cai_dat_hien_thi.png](anh/g1/sau_cai_dat_hien_thi.png) — hiện đủ 5 chip chất lượng, dòng gợi ý phần cứng `Apple M2, 8 GB → đề xuất Cao` và nút `Đo thử 30 giây`, không đè chữ hay tràn lề.

---

## 6. Lệnh kiểm thử đã chạy và kết quả

| Kiểm thử | Lệnh | Kết quả |
|---|---|---|
| Xuất app Mac | `Unity -batchmode -quit -executeMethod Khkt.EditorTools.MacBuild.Build` | Thoát `0`, **Succeeded**, 0 lỗi, `227.8 MB` → `~/AppBuild/VERA_mac/VERA Lab.app` |
| Kiểm tra loại script test | `grep -a "KhktWave6Runner" ".../Assembly-CSharp.dll"` | Không có dấu vết `KhktWave6Runner` trong bản xuất (`KhktWave6Runner.cs` sạch trong repo) |
| Kiểm tra nhánh Windows | `Unity -batchmode -quit -executeMethod Khkt.EditorTools.MacBuild.BuildWindows` | Thoát `0`, log `[MAC-BUILD] Module StandaloneWindows64 chưa được cài trên Unity này; bỏ qua xuất Windows.` |
| Kiểm thử hồi quy Wave6 | `Unity -batchmode -buildTarget StandaloneOSX -executeMethod KhktWave6Verify.Run` | `[W6_SUMMARY] passes=67 failures=0 runtimeErrors=0 startupSearchIgnored=0 shots=12` (`[W6_RESULT] ALL PASSED`, thoát `0`) |
| Đo 90s SAU (`Tự động`, `Thấp`) & GPU sweep | `"~/AppBuild/VERA_mac/VERA Lab.app/Contents/MacOS/Phong thi nghiem ao" -vera-do ...` | Xem mục 4 (`59.7–59.9 FPS`, `p95 = 16.72–17.17 ms`, `<= 1.33 khung > 50 ms / phút`) |

---

## 7. Phát hiện ngoài phạm vi G1 (ghi báo cáo, không tự sửa)

1. **`Assets/Lab/HD/Scripts/HdQualityManager.cs`:** Vẫn tự gắn vào scene qua `RuntimeInitializeOnLoadMethod` và chạy `Govern()`. Hiện đã được `HardwareProfile.DisableLegacyHdQualityManager()` vô hiệu hóa khi khởi động; nên gỡ hoặc gộp hẳn về `HardwareProfile` trong đợt dọn sau.
2. **`Assets/Scripts/Input/InAppHandTracker.cs`:** Khi bật `F10`, webcam khởi động đồng bộ gây khựng ~1,5s và `DownloadToArray()` đọc GPU→CPU mỗi khung tạo ~100 MB GC / 90s. Trong G1 đã dừng tự bật trong `DesktopViewer.Start()` (chỉ bật khi bấm `F10`); tối ưu nội bộ `InAppHandTracker` thuộc phạm vi riêng của phần tracking tay.
3. **Trùng phím `F7`:** `DoKhungHinh` dùng `F7` theo yêu cầu G1, nhưng `Assets/Scripts/Environment/Tang2/BioThemeSwitcher.cs` (dòng 55) và `Assets/Scripts/Demo/TouchHud.cs` (dòng 450) cũng bắt phím `F7`. Nên chuyển `DoKhungHinh` sang tổ hợp khác (ví dụ `F8` hoặc `Ctrl+Shift+D`) nếu cần tránh xung đột khi bấm phím trực tiếp ở tầng 2.
4. **PlayMode làm bẩn 27 tệp `.mat` trong `Assets/Lab/Materials/`:** Như `moc-goc.md` đã ghi nhận, khi chạy `KhktWave6Verify.Run` trong Unity Editor PlayMode, có code ghi thẳng vào `sharedMaterial` làm mất từ khóa `_EMISSION` của 27 vật liệu (như `DenTrangThaiXanh.mat`). Đã khôi phục sạch bằng `git checkout -- "Phong thi nghiem ao/Assets/Lab/Materials/"` để không commit nhầm.

---

## 8. Việc còn lại

- Chưa build thực tế ra `.exe` vì máy hiện tại chưa cài module `StandaloneWindows64` (hàm `MacBuild.BuildWindows` đã sẵn sàng và kiểm tra an toàn khi thiếu module).
- Cần quyết định phân bổ lại phím `F7` ngoài phạm vi G1 ở trên nếu dùng bàn phím tại tầng 2.
