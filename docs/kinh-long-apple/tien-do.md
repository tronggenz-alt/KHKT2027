# Tiến Độ Liquid Glass Apple (iOS 26/27 & macOS 27) — VERA Lab

> Kho mã nguồn chính: `/Users/trantrong/Developer/Antigravity` (nhánh `main`)  
> Tài liệu đặc tả: [`PROMPT_LIQUID_GLASS_ANTIGRAVITY.md`](./PROMPT_LIQUID_GLASS_ANTIGRAVITY.md)  
> Thư mục ảnh đối chiếu (ngoài git): `~/AppBuild/kinh_long_so_sanh/{lab_truoc,lab_sau,apple_wwdc}`

---

## P0 — Tài liệu Apple, chụp mốc Phòng Lab & ranh giới màn hình

**Trạng thái:** Hoàn thành

### 1. Tư liệu Apple đã thu thập & phân tích
- Transcript chính thức & phân tích tham chiếu lưu tạm tại `/tmp/`:
  - `/tmp/wwdc25_219_transcript.txt` (WWDC25 #219 — Meet Liquid Glass)
  - `/tmp/wwdc26_289_transcript.txt` (WWDC26 #289)
  - `/tmp/apple_sources_summary.txt` (tổng hợp WWDC25 #356, #323, WWDC26 #269, #289, kube.io)
- Trích xuất 18 khung hình từ `wwdc2025-219_hd.mp4` (`4a_152s.png` .. `4k_731s.png`) và 4 khung hình từ `wwdc2026-289_hd.mp4` (`289_0_866.0.png` .. `289_3_890.0.png`) tại `~/AppBuild/kinh_long_so_sanh/apple_wwdc/`.
- Quan sát quang học cốt lõi từ 9 khung hình WWDC25 #219 đã đối chiếu bằng mắt:
  - **`4a`**: Dải sáng viền mềm (smoothstep-warped soft band) uốn theo mép cong trên đỉnh nút, không phải nét stroke đều.
  - **`4b`**: Viên nang Dark bên trong gần như trong suốt, chỉ giữ viền sáng (rim highlight) cực mảnh và sắc trên nền tối.
  - **`4c` (`4c_37`, `4c_65`)**: Ngưng tụ vật chất — ở `0.37s` nút Play phóng đại và làm mờ mạnh biểu tượng bên dưới rồi nét dần lại ở `0.65s`.
  - **`4f`**: Núm công tắc nhấc lên thành thấu kính lip-profile bóng mượt, phóng đại viên thuốc xanh lá phía dưới.
  - **`4g`**: Núm thanh trượt bẻ cong và phóng đại mép thanh xanh lam xuyên qua thấu kính.
  - **`4h_302` / `4j`**: Kính Regular trên chữ 17pt làm mờ dòng chữ thành vệt xám mềm kết hợp bóng đổ tối dịu phía dưới để giữ độ đọc.
  - **`4k`**: Viên nang Play dạng Clear khúc xạ sắc nét thân xanh/vàng ở rìa kính kèm tối dịu cục bộ và ánh sáng tràn (light spill) nhẹ.

### 2. Chụp mốc Phòng Lab trước khi sửa (`lab_truoc`)
- Lệnh đã chạy: `KHKT_RA=~/AppBuild/kinh_long_so_sanh/lab_truoc ~/AppBuild/VERA_claude/dong_bo_va_chay.sh /Users/trantrong/Developer/Antigravity KhktUiVerify.ChupLab chup_lab_truoc`
- Kết quả: thoát mã `0`, `0` lỗi C# / Exception; sinh 3 ảnh tại `~/AppBuild/kinh_long_so_sanh/lab_truoc/`:
  - `lab_gan.png` (`2.0M`)
  - `lab_phai.png` (`1.8M`)
  - `lab_rong.png` (`2.0M`)
- Trạng thái `PlayerPrefs` đã kiểm tra sạch: `KHKT_KINH_LONG=1`, `KHKT_GIAM_TRONG_SUOT=0`, `KHKT_GIAM_CHUYEN_DONG=0`, `KHKT_TANG_TUONG_PHAN=0`.

### 3. Bảng ranh giới Trong Lab vs. Ngoài Lab

| Màn hình / Mô-đun | Phạm vi | Quy ước áp dụng |
|---|---|---|
| `ManChonLuong`, `CaiDatGiaiBaiTap` | Ngoài Lab | Dùng toàn bộ vật liệu Kính Lỏng Apple hai tầng không màu, chuyển động lò xo/gel |
| `MainMenu` (Đăng nhập, Đăng ký, Đổi MK, Cài đặt, Phòng, Đang tải) | Ngoài Lab | Dùng Kính Lỏng Apple; tab Hiển thị thêm thanh kéo `"Độ trong của kính"` (`KHKT_DO_TRONG`) |
| `DashboardShell`, `PageOverview`, `PageController`, `PageAiKey`, `ControllerCalPanel`, `MenuKit`, `MenuAnim` | Ngoài Lab | Thanh điều hướng kính tầng trên, thẻ nội dung/điều khiển theo phân tầng kính-trên-nền, xóa mọi nhuộm màu/Gradient/Ring cũ |
| `ManGiaiBaiTap`, `KhungTrangGbt`, `DapAn`, `KhungMoHinh3D` | Ngoài Lab | Tờ đề (`SoDoGiay`), CTCT 2D, khung 3D giữ nguyên làm Nội dung đặc; nút/thanh điều hướng nổi dùng Kính Lỏng (`Nút Toàn màn hình 3D` dùng Clear + tối dịu 35%) |
| `CoCheMoDun`, `MoHinh3DMoDun`, `HoaLyMoDun` | Thích ứng theo ngữ cảnh | Khi mở ngoài Lab: dùng `VungKinhApple` hai tầng; khi mở trong Lab: giữ nguyên giao diện & vật liệu Phòng Lab |
| `KinhMo.shader`, `GlassKit.Configure` trong Lab (`ModernUI`, `TouchHud`, `InstrumentScreen`, `RobotChatUI`, `ChemSearchUI`, `ReactionAnnotator`, `BioPortal`, `IntroScreen`, `DemoShowcase`, `TrackingViewer`) | Trong Lab (CẤM ĐỤNG) | Giữ nguyên pixel (`lab_truoc` == `lab_sau`) |
| `VeSanh.cs` | Trong Lab (không UI) | Không liên quan UI, giữ nguyên |

---

## Nhật ký các pha tiếp theo
- **P1 — Hệ thống 2 tầng Canvas/RenderGraph, Layer 14 `KinhDieuHuong`, `KinhLongThapMo.shader`, `KinhLongBlurFeature` & `KinhLongNen`:** **Hoàn thành**
  - Khai báo Layer `14: KinhDieuHuong` trong `ProjectSettings/TagManager.asset`.
  - Tạo `VungKinhApple.cs`, `KinhLongHaiTang.cs` (`KinhLongLop`, `KinhLongCameraNen`, `DongBoCanvasDieuHuong`, `HaiTangCanvas`, `KinhGuongDieuHuong`), `KinhLongThapMo.shader` (Copy + Dual Kawase 5-tap Downsample + 8-tap Tent Upsample), `KinhLongBlurFeature.cs` (RenderGraph URP 17 trên một camera duy nhất, tách Layer 14 khỏi pass vẽ mặc định của `PC_Renderer.asset` & `Mobile_Renderer.asset`, tạo `_KL_NoiDung` & `_KL_Mo1..4`, cache 0 ms khi đứng yên + `AsyncGPUReadback` lưới sáng 24×14, sau đó vẽ riêng `RendererList` Layer 14 đè lên).
  - Nâng cấp `KinhLongNen.cs` lên nền mịn 1920×1080 kèm tháp mờ tính sẵn 1 lần `_KL_NenMo1..4` + lưới sáng CPU 24×14.
  - Đã kiểm tra PlayMode `KhktKinhLongSetup.KiemTraP1Batch` → `[P1_PASS]` (`Center=0.949,0.451,0.110`, `Nav=0.110,0.851,0.451`, `NoiDungAtNav=0.949,0.451,0.110` tách sạch Layer 14, `NoiDung=1280x720`, `Mo1=640x360`, `Mo4=80x45`, `IdleSkip=True`).
- **P2**: `Khkt/KinhLongApple`, LUT Snell 128x1, `Khkt/BoGocAnh`, `KinhHinh` & đăng ký AlwaysIncludedShaders/WarmUp. *(Đang thực hiện)*
- **P3**: Dựng `KinhLongPhongThu` (bộ trường hợp a–g @ 2880x1800) & hiệu chỉnh quang học tĩnh.
- **P4**: Thích ứng Sáng/Tối, bóng đổ thích ứng, ánh sáng môi trường & tối dịu khi mất tiêu điểm.
- **P5**: Chuyển động lỏng (ngưng tụ Hermite, rọi con trỏ, nảy macOS/iPad, kéo gel & biến hình công tắc/thanh trượt).
- **P6**: `HopKinh` (`sminPoly`), giọt chọn không màu, bùng popover & viền cuộn.
- **P7**: Phủ khắp mọi màn hình ngoài Lab & xóa sạch mọi nhuộm màu trên kính.
- **P8**: Trợ năng & thanh trượt `"Độ trong của kính"` (`KHKT_DO_TRONG`).
- **P9**: Kiểm định đủ 10 tiêu chí §12.2, đối chiếu `lab_truoc` vs `lab_sau` & build ứng dụng Mac thật.
