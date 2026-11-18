# Báo cáo [G2] Bàn tay người chân thật + mô hình người 3D (VERA Lab, Unity)

- **Nhánh:** `n18/antigravity`
- **Ngày thực hiện:** 18/11/2026
- **Commit mã nguồn:** `f5c3b81e`, `5742f762`
- **Phần cứng đo:** Apple M2 (5461 MB VRAM chia sẻ), RAM 8192 MB, 8 nhân CPU, màn Retina `3840x2486` (DPI 299).

---

## 1. Tóm tắt kết quả đạt được

1. **Khảo sát & chụp 6 tư thế TRƯỚC (`truoc_*.png`):**
   - Lưu 12 ảnh gốc tại `docs/ngay-18-11/anh/g2/truoc_1_xoe_*` đến `truoc_6_chi_tay_*` (2 góc: người chơi & góc nghiêng).
   - Xác định 6 nhóm lỗi: (a) trùng 3 bàn tay giả lập cùng lúc (tay khung cầu của `ArmRig` + `HandRig`); (b) ngón chỉ 2 đốt thẳng đều, thiếu móng tay, thiếu vòm mu tay & gò cơ gan tay; (c) hở cổ tay với ống tay áo blouse; (d) xuyên thành bình nón & pipet, hụt khóa buret; (e) vật liệu da dùng URP Lit trông như nhựa phẳng, chưa có tùy chọn găng nitrile xanh; (f) `LabPresence` dùng khối trụ/cầu đơn giản cho người khác và chưa có thân góc nhìn thứ nhất.
2. **Bàn tay giải phẫu 3 đốt + shader da + bảng tư thế + IK chống xuyên vật:**
   - Dựng 4 ngón 3 đốt với tỉ lệ giảm dần (`ProxLen > MidLen >= DistLen`), ngón cái 3 đoạn đối diện (`ThumbMetaLen`, `ThumbProxLen`, `ThumbDistLen`), 5 móng tay, vòm mu bàn tay và mô cái/mô út.
   - Bổ sung bảng tư thế `HandPoseTable` (`Xoe`, `Nghi`, `ChumCam`, `BinhNon`, `OngNghiem`, `Pipet`, `KhoaBuret`, `Kep`, `Que`, `ChiTay`) với thời gian chuyển mượt `0,15 s`, `ClampFinger` / `ClampThumb` giữ giới hạn sinh lý (cấm gập ngược) và `palmLocalOffset` xoay bàn tay sang cạnh cổ bình/thân pipet.
   - Viết shader `Khkt/DaNguoi` (`Lab/Shaders/DaNguoi.shader`) mô phỏng tán xạ dưới da nhẹ theo hướng wrap diffuse + rim sáng ấm.
   - Chế độ găng tay nitrile xanh mặc định **BẬT** (`HandRig.WearGloves`), lưu `PlayerPrefs`, nối liền cổ tay với ống tay áo blouse `ArmRig` (`gap = 0.00 mm`).
   - Ánh xạ 21 điểm MediaPipe trong `InAppHandTracker.MapLandmarksToHandRig` điều khiển trực tiếp khớp giải phẫu mới của `HandRig`.
   - Giải IK bề mặt (`SolveSurfaceContactIk`: raycast vuông góc trục dụng cụ + IK 2 khớp + tìm kiếm tọa độ giảm chấn + kiểm tra bề mặt ngay dưới đầu ngón): bình nón `pen=0, err=0.51 mm`, pipet `pen=0, err=0.45 mm`, khóa buret `pen=0, err=0.00 mm` (tất cả `<= 3 mm`, không xuyên vật).
3. **Mô hình người 3D (3 LOD) + thân người chơi + phòng học chung:**
   - `HumanoidAvatar`: dựng bằng mã, cao ~1,65 m, áo blouse trắng, quần, giày kín mũi, kính bảo hộ, 3 mức LOD chọn theo `HardwareProfile.Muc` (`Cao/SieuNet → 0`, `Vua → 1`, `Thap → 2`), phân biệt màu cổ áo/phù hiệu Giáo viên vs Học sinh và bảng tên trên đầu.
   - `LocalPlayerBody`: hiện thân áo blouse, quần, giày khi nhìn xuống, bật/tắt tức thì và lưu `PlayerPrefs`.
   - `LabPresence`: thay khối đơn giản của người chơi từ xa bằng `HumanoidAvatar` và thêm `SpawnSimulatedRemote` để chụp kiểm chứng phòng học chung trên 1 máy.
   - `AvatarSettingsHook`: gắn công tắc `Đeo găng tay nitrile xanh` (`ToggleDeoGangTay`) và `Hiện thân người góc nhìn thứ nhất` (`ToggleHienThanMinh`) vào `Tab Điều khiển` của `IntroScreen` lúc chạy mà không sửa `IntroScreen.cs`.

---

## 2. Các tệp đã sửa / tạo mới (trong phạm vi G2)

| Tệp | Nội dung |
|---|---|
| `Phong thi nghiem ao/Assets/Lab/Shaders/DaNguoi.shader` (+ `.meta`) | Shader da người nhẹ (`Khkt/DaNguoi`) với wrap diffuse, subsurface color và viền sáng ấm |
| `Phong thi nghiem ao/Assets/Scripts/Avatar/HandPoseTable.cs` (+ `.meta`) | Bảng 10 tư thế dụng cụ, giới hạn góc khớp sinh lý, `palmLocalOffset`, chuyển mượt `0.15f` giây |
| `Phong thi nghiem ao/Assets/Scripts/Demo/HandRig.cs` | Cấu trúc 3 đốt/ngón, ngón cái 3 đoạn, 5 móng tay, vòm mu/gan tay, `WearGloves`, `WristAttach`, `SolveSurfaceContactIk`, `ApplyMediaPipeLandmarks` |
| `Phong thi nghiem ao/Assets/Scripts/Demo/ArmRig.cs` | Gỡ bàn tay khung cầu trùng lặp, nối sát cẳng tay áo vào `WristAttach`, tôn trọng trạng thái găng/da qua `HandRig.SetRenderEnabled` |
| `Phong thi nghiem ao/Assets/Scripts/Demo/HandController.cs` | Chỉ thêm hook `TargetContactObject` và `SetTargetPose` (giữ nguyên toàn bộ luồng cầm/thả và cổng an toàn) |
| `Phong thi nghiem ao/Assets/Scripts/Input/InAppHandTracker.cs` | Thêm `MapLandmarksToHandRig` ánh xạ 21 điểm MediaPipe vào `HandRig` |
| `Phong thi nghiem ao/Assets/Scripts/Avatar/HumanoidAvatar.cs` (+ `.meta`) | Mô hình người 3D cao ~1,65 m dựng bằng mã với 3 mức LOD theo `HardwareProfile` |
| `Phong thi nghiem ao/Assets/Scripts/Avatar/LocalPlayerBody.cs` (+ `.meta`) | Thân người chơi góc nhìn thứ nhất (`Visible` lưu `PlayerPrefs`) |
| `Phong thi nghiem ao/Assets/Scripts/Avatar/AvatarSettingsHook.cs` (+ `.meta`) | Tự gắn 2 toggle G2 vào `IntroScreen` → `Tab Điều khiển` trong `intro.ui.Canvas` lúc chạy |
| `Phong thi nghiem ao/Assets/Scripts/Network/LabPresence.cs` | Thay avatar khối đơn giản bằng `HumanoidAvatar` + thêm `SpawnSimulatedRemote` |
| `Phong thi nghiem ao/Assets/Scripts/Editor/KhktN18Verify.cs` (+ `.meta`) | Bộ kiểm thử tự động nhóm `"G2"` (`ChupG2Truoc`, `RunG2`) |

---

## 3. Kết quả kiểm thử & đối chiếu hiệu năng

| Kiểm thử | Lệnh | Kết quả |
|---|---|---|
| Kiểm thử G2 tự động | `Unity -batchmode -buildTarget StandaloneOSX -executeMethod Khkt.EditorTools.KhktN18Verify.RunG2` | `passes=16 failures=0 runtimeErrors=0` (`[N18_G2_RESULT] ALL PASSED`, thoát `0`). Chi tiết IK: Bình nón `pen=0, err=0.51mm`, Pipet `pen=0, err=0.45mm`, Khóa buret `pen=0, err=0.00mm`, cổ tay `gap=0.00mm`, MediaPipe `mcp=44.3 pip=34.6`, LOD `0/1/2`. |
| Xuất app Mac | `Unity -batchmode -quit -buildTarget StandaloneOSX -executeMethod Khkt.EditorTools.MacBuild.Build` | Thoát `0` (`BUILD_EXIT=0`), cập nhật `~/AppBuild/VERA_mac/VERA Lab.app`. |
| Đo 90s SAU G2 | `"~/AppBuild/VERA_mac/VERA Lab.app/Contents/MacOS/Phong thi nghiem ao" -vera-do -vera-muc=tudong -vera-nhan=g2_sau` | `3840x2486` Retina (mức `Cao`): **59.7 FPS TB**, `ms TB=16.77`, `p50=16.67 ms`, `p95=16.97 ms`, `p99=17.64 ms`, `max=100.05 ms`, `>33.3ms=13`, `>50ms=2 (1.33 khung/phút)`, `GC=10.98 MB (2.09 KB/khung, 5 Gen0)`. So với G1 SAU (`59.7–59.9 FPS`): độ sụt ~0–0,3% (đạt yêu cầu ≤ 5%). |
| Hồi quy Wave 6 | `Unity -batchmode -buildTarget StandaloneOSX -executeMethod KhktWave6Verify.Run` | `[W6_SUMMARY] passes=67 failures=0 runtimeErrors=0 startupSearchIgnored=0 shots=12` (`[W6_RESULT] ALL PASSED`, thoát `0`). |

---

## 4. Ảnh đối chiếu TRƯỚC / SAU (trong `docs/ngay-18-11/anh/g2/`)

- **Tư thế 1 — Xòe:** TRƯỚC ([người chơi](anh/g2/truoc_1_xoe_goc_nguoi_choi.png), [nghiêng](anh/g2/truoc_1_xoe_goc_nghieng.png)) → SAU ([người chơi](anh/g2/sau_1_xoe_goc_nguoi_choi.png), [nghiêng](anh/g2/sau_1_xoe_goc_nghieng.png))
- **Tư thế 2 — Chụm cầm:** TRƯỚC ([người chơi](anh/g2/truoc_2_chum_cam_goc_nguoi_choi.png), [nghiêng](anh/g2/truoc_2_chum_cam_goc_nghieng.png)) → SAU ([người chơi](anh/g2/sau_2_chum_cam_goc_nguoi_choi.png), [nghiêng](anh/g2/sau_2_chum_cam_goc_nghieng.png))
- **Tư thế 3 — Cầm bình nón:** TRƯỚC ([người chơi](anh/g2/truoc_3_binh_non_goc_nguoi_choi.png), [nghiêng](anh/g2/truoc_3_binh_non_goc_nghieng.png)) → SAU ([người chơi](anh/g2/sau_3_binh_non_goc_nguoi_choi.png), [nghiêng](anh/g2/sau_3_binh_non_goc_nghieng.png))
- **Tư thế 4 — Cầm pipet:** TRƯỚC ([người chơi](anh/g2/truoc_4_pipet_goc_nguoi_choi.png), [nghiêng](anh/g2/truoc_4_pipet_goc_nghieng.png)) → SAU ([người chơi](anh/g2/sau_4_pipet_goc_nguoi_choi.png), [nghiêng](anh/g2/sau_4_pipet_goc_nghieng.png))
- **Tư thế 5 — Vặn khóa buret:** TRƯỚC ([người chơi](anh/g2/truoc_5_khoa_buret_goc_nguoi_choi.png), [nghiêng](anh/g2/truoc_5_khoa_buret_goc_nghieng.png)) → SAU ([người chơi](anh/g2/sau_5_khoa_buret_goc_nguoi_choi.png), [nghiêng](anh/g2/sau_5_khoa_buret_goc_nghieng.png))
- **Tư thế 6 — Chỉ tay:** TRƯỚC ([người chơi](anh/g2/truoc_6_chi_tay_goc_nguoi_choi.png), [nghiêng](anh/g2/truoc_6_chi_tay_goc_nghieng.png)) → SAU ([người chơi](anh/g2/sau_6_chi_tay_goc_nguoi_choi.png), [nghiêng](anh/g2/sau_6_chi_tay_goc_nghieng.png))
- **Phòng học chung giả lập 2 người (Giáo viên & Học sinh):** [sau_7_phong_hoc_chung.png](anh/g2/sau_7_phong_hoc_chung.png)
- **Thân người chơi góc nhìn thứ nhất (nhìn xuống):** [sau_8_than_minh.png](anh/g2/sau_8_than_minh.png)
- **Da người thật khi tắt găng nitrile (`Khkt/DaNguoi`):** [sau_9_da_khong_gang.png](anh/g2/sau_9_da_khong_gang.png)

---

## 5. Phát hiện ngoài phạm vi & việc còn lại

- `IntroScreen.cs` nằm ngoài phạm vi G2 nên không chỉnh trực tiếp; 2 nút bật/tắt găng tay nitrile và thân người chơi được gắn lúc chạy qua `AvatarSettingsHook.cs`. Nếu muốn dồn vào hàm dựng giao diện tĩnh của `IntroScreen` sau này có thể gộp lúc tích hợp.
- PlayMode trong Unity Editor vẫn làm thay đổi các tệp `.mat` trong `Phong thi nghiem ao/Assets/Lab/Materials/` như `moc-goc.md` đã ghi nhận; đã khôi phục sạch bằng `git checkout -- "Phong thi nghiem ao/Assets/Lab/Materials/"`.
- Chưa có module Windows trên máy này nên kiểm thử bản dựng tại G2 chỉ chạy trên `StandaloneOSX`.
