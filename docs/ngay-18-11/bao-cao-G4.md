# Báo cáo [G4] — Giải phẫu người 3D ngay trong ứng dụng (Mac/Windows)

**Nhánh:** `n18/antigravity`  
**Ngày hoàn thành:** 18/11/2026  
**Ghi nhận bản quyền mô hình giải phẫu:** `BodyParts3D © The Database Center for Life Science · CC BY-SA 2.1 JP / CC BY 4.0`

---

## 1. Mục tiêu đã đạt
1. Đưa toàn bộ mô-đun **Giải phẫu người 3D** vào chạy trực tiếp bằng C#/Unity bên trong ứng dụng trên **macOS** và **Windows**, **không gọi `Application.OpenURL` mở trình duyệt ngoài**; đồng thời **giữ nguyên** luồng `WKWebView` trên iPad (`#if UNITY_IOS && !UNITY_EDITOR`).
2. Tận dụng lại trực tiếp 3 lưới BodyParts3D đã có sẵn trên Bàn giải phẫu số (`BP3D2_xuong.asset`, `BP3D2_co.asset`, `BP3D2_da.asset`, không commit các tệp `.asset` này theo `.gitignore`) và giải mã theo yêu cầu từ `StreamingAssets/biology_lab/sinh-hoc/bp3d.bin.gz` cho hệ mạch máu ngoại vi (`mach_do`, `mach_xanh`) cũng như giảm mật độ lưới theo `HardwareProfile.Muc` (`Thấp` đơn giản hóa cụm đỉnh, `Vừa/Cao/Siêu nét` giữ đầy đủ lưới chuẩn), giúp **mở lần đầu chỉ mất `0.188 s`** (`<= 3 s`).
3. Cung cấp đầy đủ tính năng dạy và học sinh học tương tác trên sân khấu Layer `29`:
   - Bật/tắt độc lập **8 hệ cơ quan** (`xương`, `cơ`, `tiêu hóa`, `tuần hoàn`, `hô hấp`, `thần kinh`, `bài tiết`, `nội tiết`) và **lớp da**.
   - Thanh trượt **Bóc lớp (`0%–100%`)** làm mờ mượt từ ngoài vào trong (`Da → Cơ → Xương mờ & Nội tạng`).
   - **Mặt phẳng cắt giải phẫu 3 chiều** (`Cắt dọc X`, `Cắt ngang Y`, `Cắt trán Z` + thanh trượt vị trí `-1..+1`) dùng shader URP `Khkt/Bio/MatCatGiaiPhau` hiển thị màu tiết diện mặt cắt (`_CrossSectionColor`) cùng khung viền cắt mảnh không che tầm nhìn.
   - Nhấp chọn **15 cơ quan** đọc trực tiếp **Thẻ kiến thức tiếng Việt** từ `StreamingAssets/biology_lab/sinh-hoc/kien-thuc/*.json` (`danh-muc.json` và 15 tệp chi tiết).
   - Nút **Tách ra / Đưa về cơ thể** đưa riêng cơ quan đang chọn vào giữa sân khấu để xoay 360° quan sát.
   - Hoạt ảnh sinh lý thời gian thực: **tim đập 2 pha (`60–120 lần/phút`)**, **dòng máu đỏ (giàu O₂) & xanh (nghèo O₂)** chảy qua tâm nhĩ – tâm thất – động/tĩnh mạch, **hai lá phổi phồng xẹp cùng cơ hoành**, và **sóng nhu động** trên dạ dày/ruột non/ruột già.
   - Chế độ **Câu hỏi nhanh 5 câu (chỉ cơ quan trên mô hình 3D)** chấm điểm tức thì kèm giải thích tiếng Việt.

---

## 2. Danh sách tệp đã tạo / sửa (trong phạm vi G4)

| Tệp | Vai trò |
| :--- | :--- |
| [MatCatGiaiPhau.shader](file:///Users/trantrong/Developer/Antigravity/Phong%20th%20nghiem%20ao/Assets/Lab/Shaders/MatCatGiaiPhau.shader) | Shader URP cắt mặt phẳng 3 chiều (`IS_FRONT_VFACE`), màu tiết diện giải phẫu, `_Opacity` bóc lớp, `_Highlight` viền chọn, sóng nhu động `_WaveAmp` |
| [MatCatGiaiPhauMat.mat](file:///Users/trantrong/Developer/Antigravity/Phong%20thi%20nghiem%20ao/Assets/Scripts/Demo/Bio/GiaiPhau/Resources/MatCatGiaiPhauMat.mat) | Vật liệu tham chiếu nhỏ trong `Resources/` để giữ shader `Khkt/Bio/MatCatGiaiPhau` khi xuất bản dựng Standalone |
| [Bp3dReader.cs](file:///Users/trantrong/Developer/Antigravity/Phong%20thi%20nghiem%20ao/Assets/Scripts/Demo/Bio/GiaiPhau/Bp3dReader.cs) | Tái sử dụng trước `BP3D2_xuong`/`co`/`da` trong cảnh; đọc `bp3d.bin.gz` cho mạch máu/dự phòng (`UInt32`), giảm lưới ở mức `Thap`, nạp 15 hồ sơ cơ quan `.json` |
| [GiaiPhauSanKhau3D.cs](file:///Users/trantrong/Developer/Antigravity/Phong%20thi%20nghiem%20ao/Assets/Scripts/Demo/Bio/GiaiPhau/GiaiPhauSanKhau3D.cs) | Sân khấu Layer `29` `(0, -520, 0)`: 8 hệ cơ quan + Da, Bóc lớp, Mặt cắt 3D, Tách riêng cơ quan, Tim đập 60–120 bpm, Dòng máu, Phổi thở, Nhu động ruột, giải phóng vật liệu động khi đóng |
| [GiaiPhauMoDun.cs](file:///Users/trantrong/Developer/Antigravity/Phong%20thi%20nghiem%20ao/Assets/Scripts/Demo/Bio/GiaiPhau/GiaiPhauMoDun.cs) | `GiaiPhauMoDunImpl` & UI tiếng Việt (`1920x1080`, `matchWidthOrHeight = 0f`), tái sử dụng `CameraQuyDao`, Thẻ kiến thức, Câu hỏi nhanh 5 câu, ghi nhận bản quyền BodyParts3D |
| [Khkt.Bio.GiaiPhau.Bridge.asmdef](file:///Users/trantrong/Developer/Antigravity/Phong%20thi%20nghiem%20ao/Assets/Scripts/Demo/Bio/GiaiPhau/Bridge/Khkt.Bio.GiaiPhau.Bridge.asmdef), [Bridge/GiaiPhauMoDun.cs](file:///Users/trantrong/Developer/Antigravity/Phong%20thi%20nghiem%20ao/Assets/Scripts/Demo/Bio/GiaiPhau/Bridge/GiaiPhauMoDun.cs), [link.xml](file:///Users/trantrong/Developer/Antigravity/Phong%20thi%20nghiem%20ao/Assets/Scripts/Demo/Bio/GiaiPhau/Bridge/link.xml) | Cầu nối đúng hợp đồng `Khkt.Bio.GiaiPhau.GiaiPhauMoDun` (`Mo()`, `Dong()`, `DangMo`) tách assembly tránh xung đột `MenuKit.Bio` trong `MainMenu.cs` |
| [BioWebViewBridge.cs](file:///Users/trantrong/Developer/Antigravity/Phong%20thi%20nghiem%20ao/Assets/Scripts/Demo/Bio/Portal/BioWebViewBridge.cs) | Trên Mac/Windows/Editor mở `GiaiPhauMoDunImpl.Mo(...)` trong ứng dụng (thay `Application.OpenURL`); giữ nguyên `_OpenBioWebView` trên iOS |
| [DigitalAnatomyTable.cs](file:///Users/trantrong/Developer/Antigravity/Phong%20thi%20nghiem%20ao/Assets/Scripts/Demo/Bio/DigitalAnatomyTable.cs) | Khi bấm vào Bàn giải phẫu số trên Mac/Windows/Editor (`#if UNITY_STANDALONE \|\| UNITY_EDITOR`) mở thẳng mô-đun Giải phẫu 3D |
| [KhktN18Verify.cs](file:///Users/trantrong/Developer/Antigravity/Phong%20thi%20nghiem%20ao/Assets/Scripts/Editor/KhktN18Verify.cs) | Bổ sung nhóm kiểm thử `"G4"` (`RunG4()` và gọi trong `Run()`) + chụp 6 ảnh nghiệm thu |

*(Lưu ý: giữ nguyên `BioPortalTransition.cs` không cần sửa vì đã gọi qua `BioWebViewBridge.Open(() => OnReturnFromBioLab())`.)*

---

## 3. Lệnh kiểm thử và kết quả bằng số

Tất cả các phép đo dưới đây được chạy **sau lần sửa mã nguồn cuối cùng** trên nhánh `n18/antigravity` (`commit 9aba2764`):

1. **Kiểm thử tổng hợp N18 (`KhktN18Verify.Run` — G2 + G3 + G4):**
   ```bash
   /Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/trantrong/Developer/Antigravity/Phong thi nghiem ao" -executeMethod Khkt.EditorTools.KhktN18Verify.Run -logFile /tmp/vera_n18_all_verify.log
   ```
   - **G2:** `passes=16 failures=0 runtimeErrors=0` (`[N18_G2_RESULT] ALL PASSED`)
   - **G3:** `passes=10` (cộng dồn `26`) `failures=0 runtimeErrors=0` (`[N18_G3_RESULT] ALL PASSED`, `~61.8 FPS`)
   - **G4:** `passes=14` (cộng dồn **`40/40 PASS`**), `failures=0`, `runtimeErrors=0` (`[N18_G4_RESULT] ALL PASSED`):
     - Thời gian mở lần đầu `GiaiPhauMoDun.Mo()`: **`0.188 s`** (yêu cầu `<= 3.0 s`).
     - Chênh lệch bộ nhớ qua **5 chu kỳ mở/đóng liên tiếp** (`Profiler.GetTotalAllocatedMemoryLong()`): **`-1.07 MB`** (không rò rỉ bộ nhớ).
     - Nhịp khung trong Editor PlayMode mức **`HardwareProfile.Cao`**: **`~61.4 FPS`**.
     - Lối vào `BioWebViewBridge` & `DigitalAnatomyTable`: **ĐẠT** (mở/đóng ngay trong app, không mở trình duyệt ngoài).
     - Bật/tắt đủ **8 hệ cơ quan**: **ĐẠT**; **Bóc lớp `0..1`**: **ĐẠT**; **Mặt cắt dọc/ngang/trán**: **ĐẠT**; chọn 3+ cơ quan (`tim`, `phoi`, `nao`) & **Tách ra**: **ĐẠT**; đổi nhịp tim `60 -> 120 lần/phút`: **ĐẠT**; **Câu hỏi nhanh 5 câu**: **`5/5`**.

2. **Kiểm thử hồi quy Wave 6 (`KhktWave6Verify.Run`):**
   ```bash
   /Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/trantrong/Developer/Antigravity/Phong thi nghiem ao" -executeMethod KhktWave6Verify.Run -logFile /tmp/vera_w6_g4_check.log
   ```
   - Kết quả: **`[W6_SUMMARY] passes=67 failures=0 runtimeErrors=0 startupSearchIgnored=0 shots=12`** → **`[W6_RESULT] ALL PASSED`**.

3. **Xuất bản dựng macOS (`Khkt.EditorTools.MacBuild.Build`):**
   ```bash
   /Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "/Users/trantrong/Developer/Antigravity/Phong thi nghiem ao" -executeMethod Khkt.EditorTools.MacBuild.Build -logFile /tmp/vera_macbuild_g4.log
   ```
   - Kết quả: **`Succeeded, lỗi: 0, thời gian: 47.1s, dung lượng: 228.0 MB → ~/AppBuild/VERA_mac/VERA Lab.app`**.

---

## 4. Ảnh nghiệm thu G4

1. [01_tong_quan_he_co_xuong.png](file:///Users/trantrong/Developer/Antigravity/docs/ngay-18-11/anh/g4/01_tong_quan_he_co_xuong.png) — Toàn thân BodyParts3D nhìn chính diện, bóc lớp 36% lộ hệ cơ, xương sọ và mạch máu chi.
2. [02_boc_lop_noi_tang_tuan_hoan.png](file:///Users/trantrong/Developer/Antigravity/docs/ngay-18-11/anh/g4/02_boc_lop_noi_tang_tuan_hoan.png) — Bóc lớp 78% làm mờ khung xương sườn để quan sát tim, phổi, cơ hoành, gan, túi mật, dạ dày, ruột non/già, thận và bàng quang.
3. [03_mat_cat_doc_co_the.png](file:///Users/trantrong/Developer/Antigravity/docs/ngay-18-11/anh/g4/03_mat_cat_doc_co_the.png) — Mặt cắt dọc (`Sagittal X`) dùng shader `MatCatGiaiPhau` kèm khung cắt mảnh không che khuất giải phẫu bên trong.
4. [04_chon_tim_va_the_kien_thuc.png](file:///Users/trantrong/Developer/Antigravity/docs/ngay-18-11/anh/g4/04_chon_tim_va_the_kien_thuc.png) — Chọn cơ quan Tim trên mô hình 3D với viền sáng nổi bật và Thẻ kiến thức tiếng Việt đầy đủ.
5. [05_tach_rieng_co_quan_tim_120bpm.png](file:///Users/trantrong/Developer/Antigravity/docs/ngay-18-11/anh/g4/05_tach_rieng_co_quan_tim_120bpm.png) — Chế độ `Tách ra` cô lập Tim 4 ngăn + cuống động mạch chủ/động mạch phổi và dòng máu ở nhịp `120 lần/phút`.
6. [06_cau_hoi_nhanh_chi_co_quan.png](file:///Users/trantrong/Developer/Antigravity/docs/ngay-18-11/anh/g4/06_cau_hoi_nhanh_chi_co_quan.png) — Chế độ Câu hỏi nhanh 5 câu bấm trực tiếp cơ quan trên mô hình 3D (`Điểm hiện tại: 1/5`).

---

## 5. Việc còn lại / Bàn giao gộp nhánh
- **Hoàn thành đủ chuỗi `G1 → G2 → G3 → G4`** trên nhánh `n18/antigravity` trong `/Users/trantrong/Developer/Antigravity`.
- Chưa dựng bản Windows trên máy Mac này do môi trường chỉ cài module `StandaloneOSX`; logic đọc `bp3d.bin.gz` / `StreamingAssets` hoàn toàn dùng `Path.Combine` đa nền tảng sẵn sàng biên dịch cho Windows.
- Không có tiến trình Unity nào đang mở; toàn bộ file nhạy cảm không được thêm vào git. Nhánh đã sẵn sàng cho bước rà soát và gộp của Claude.
