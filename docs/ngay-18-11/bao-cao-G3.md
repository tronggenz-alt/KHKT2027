# Báo cáo [G3] Mô hình 3D thực vật: cấu trúc, chức năng, chuyển động dạy học theo thời gian thực

- **Nhánh:** `n18/antigravity`
- **Ngày thực hiện:** 18/11/2026
- **Commit mã nguồn:** `75e9be14`, `5f32e6fa`
- **Phần cứng đo:** Apple M2 (5461 MB VRAM chia sẻ), RAM 8192 MB, 8 nhân CPU.

---

## 1. Tóm tắt kết quả đạt được

1. **Nội dung khoa học & mô phỏng C# thuần (chương trình Sinh 10–11 GDPT 2018):**
   - Lưu đầy đủ thông số có nguồn trích dẫn (`"nguon"`) trong `data/sinh/thuc_vat.json`: 17 thẻ kiến thức bộ phận, 5 bước bài giảng, 5 câu hỏi trắc nghiệm và thông số sinh lý cây đậu hai lá mầm C3.
   - Viết `Khkt.Simulation.Bio.ThucVat.ThucVatMoPhong` (không `using UnityEngine`) gọi trực tiếp các hàm dùng chung từ `Photosynthesis`, `Transpiration`, `Phototropism` để không mâu thuẫn số liệu với các trạm trong nhà kính.
   - Viết bộ unit test C# thuần `ThucVatSimTests.RunAll` (**12/12 đạt, 0 lỗi**) và giữ nguyên 100% `BioSimulationTests` (**114/114 đạt, 0 lỗi** qua `tools/chay_kiem_thu_hoa.sh`).
2. **Mô hình 3D cây hai lá mầm + 3 lát cắt giải phẫu + chuyển động thời gian thực:**
   - Dựng bằng mã trong `CayHaiLaMam3D.cs` trên layer riêng `28` tại sân khấu tách biệt `(0, -450, 0)`:
     - **Tổng thể cây đậu:** Hệ rễ cọc (rễ chính, chóp rễ, miền lông hút 28 sợi, rễ bên), thân chia 7 đốt uốn cong động theo hướng sáng (`GocUonThanDo`) & vươn dài trong tối (`HeSoVuonDaiTrongToi`), lá cây hai lá mầm gân hình mạng gắn shader `"Khkt/Bio/LaCay"` đung đưa theo thanh trượt Gió, hoa đậu mẫu 5.
     - **3 lát cắt phóng to (bấm/nhấp đúp hoặc chọn thẻ/thanh trên để mở):**
       1. `Lát cắt Rễ`: Lông hút, mô mềm vỏ, nội bì có **đai Caspari** thấm suberin chặn gian bào, mạch gỗ hình sao, mạch rây xen kẽ.
       2. `Lát cắt Thân`: Biểu bì, vỏ, vòng 8 bó mạch hở xếp đúng từ ngoài vào trong (**mạch rây ngoài → tầng sinh mạch → mạch gỗ trong**).
       3. `Lát cắt Lá`: Lớp cutin bóng, biểu bì trên/dưới, mô giậu giàu lục lạp, mô xốp có khoang khí, gân lá (gỗ trên/rây dưới), **khí khổng với 2 tế bào hạt đậu** trương mở/xẹp khép khe khí khổng thời gian thực theo ánh sáng và ABA khi thiếu nước đất.
     - **Dòng chảy & hệ hạt:** Shader cuộn UV `Khkt/Bio/LaCayMach` trên ống mạch gỗ (đi lên theo thoát hơi nước) và mạch rây (đi xuống theo quang hợp), cùng bể hạt pre-allocated (nước, đường, CO₂, O₂, auxin dồn về phía khuất sáng) giới hạn chặt `<= HardwareProfile.SoHatToiDa` và 0 cấp phát mỗi khung.
3. **Camera quỹ đạo dùng chung & giao diện dạy học:**
   - `CameraQuyDao.cs`: tái sử dụng được cho cả G3 và G4 (kéo xoay, cuộn phóng to/thu nhỏ, bấm chọn thẻ kiến thức, nhấp đúp phóng to/lấy nét, `Esc` để đóng).
   - `ThucVatMoDun` (`Khkt.Bio.ThucVat.ThucVatMoDun` với `Mo()`, `Dong()`, `DangMo`): 3 chế độ (`Bài giảng` 5 bước có lời dẫn, `Tự do`, `Câu hỏi` 5 câu chấm điểm ngay), 7 thanh trượt thời gian thực (Ánh sáng, CO₂, Nhiệt độ, Độ ẩm không khí, Độ ẩm đất, Gió, Góc đèn hướng sáng), tua nhanh ngày/đêm (`×1`, `×60`, `×600`), công tắc chú thích 3D và thẻ kiến thức.
   - `BanMoHinhThucVat.cs`: tự gắn `"Bàn mô hình thực vật"` vào Nhà kính thực vật (tầng 2) lúc chạy khi khu `ZoneMap.Greenhouse` được nạp; tương tác bấm mở thẳng `ThucVatMoDun`.

---

## 2. Xử lý xung đột tên `MenuKit.Bio` ngoài phạm vi

- **Phát hiện:** `Assets/Scripts/Menu/MainMenu.cs` (ngoài phạm vi G3, thuộc phạm vi C4) nằm trong `namespace Khkt.Menu`, khai báo `using static Khkt.Menu.Dashboard.MenuKit;` và gọi rút gọn `Bio` ở các dòng `205`, `366`, `674`. Khi định nghĩa `namespace Khkt.Bio.ThucVat` trực tiếp trong `Assembly-CSharp`, C# ưu tiên giải phân tên `Khkt.Bio` trong namespace cha `Khkt`, gây lỗi biên dịch `CS0118` tại `MainMenu.cs`.
- **Cách xử lý đúng Luật chung (không sửa `MainMenu.cs` ngoài phạm vi):**
  - Đặt phần thực thi trong `namespace Khkt.Demo.Bio.ThucVat` (`ThucVatMoDunImpl`).
  - Đặt lớp hợp đồng đúng tên `Khkt.Bio.ThucVat.ThucVatMoDun` (`Mo()`, `Dong()`, `DangMo`) vào assembly riêng `Assets/Scripts/Demo/Bio/ThucVat/Bridge/Khkt.Bio.ThucVat.Bridge.asmdef` với `"autoReferenced": false` và `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]` (kèm `link.xml` chống strip).
  - Nhờ đó `Assembly-CSharp` (`MainMenu.cs`) không tham chiếu trực tiếp assembly này nên biên dịch sạch 100%, còn C4 và bài thử kiểm tra qua `AppDomain.CurrentDomain.GetAssemblies()` đều tìm thấy và gọi được `Khkt.Bio.ThucVat.ThucVatMoDun`.
  - *(Kiến nghị ngoài phạm vi để nhóm C4 xử lý ở đợt gộp: đổi 3 chỗ `Bio` tại `MainMenu.cs:205, 366, 674` thành `MenuKit.Bio` tương tự các tệp trong `Menu/Dashboard/` để đồng bộ với G3 và G4).*

---

## 3. Các tệp đã sửa / tạo mới (trong phạm vi G3)

| Tệp | Commit | Nội dung |
|---|---|---|
| `data/sinh/thuc_vat.json` | `75e9be14` | Dữ liệu thông số sinh lý có `"nguon"`, 17 bộ phận, 5 bước bài giảng, 5 câu hỏi |
| `Phong thi nghiem ao/Assets/Scripts/Simulation/Bio/ThucVat/ThucVatMoPhong.cs` (+ `.meta`) | `75e9be14` | Mô phỏng C# thuần dùng chung `Photosynthesis`, `Transpiration`, `Phototropism` |
| `Phong thi nghiem ao/Assets/Scripts/Simulation/Bio/ThucVat/ThucVatSimTests.cs` (+ `.meta`) | `75e9be14` | 12 kiểm thử đơn vị C# thuần về chiều sinh lý & dữ liệu giáo khoa |
| `Phong thi nghiem ao/Assets/Lab/Shaders/LaCayMach.shader` (+ `.meta`) | `5f32e6fa` | Shader cuộn UV dòng nước mạch gỗ và dòng đường mạch rây |
| `Phong thi nghiem ao/Assets/Scripts/Demo/Bio/ThucVat/CameraQuyDao.cs` (+ `.meta`) | `5f32e6fa` | Camera quỹ đạo dùng chung (chuột xoay, cuộn zoom, nhấp đúp lấy nét, Esc) |
| `Phong thi nghiem ao/Assets/Scripts/Demo/Bio/ThucVat/CayHaiLaMam3D.cs` (+ `.meta`) | `5f32e6fa` | Cây 3D, 3 lát cắt phóng to, khí khổng động, hệ hạt `<= SoHatToiDa` |
| `Phong thi nghiem ao/Assets/Scripts/Demo/Bio/ThucVat/ThucVatMoDun.cs` (+ `.meta`) | `5f32e6fa` | Sân khấu riêng Layer 28, giao diện thanh trượt, 3 chế độ dạy học, thẻ kiến thức |
| `Phong thi nghiem ao/Assets/Scripts/Demo/Bio/ThucVat/BanMoHinhThucVat.cs` (+ `.meta`) | `5f32e6fa` | Bàn mô hình thực vật tự gắn khi khu `ZoneMap.Greenhouse` nạp |
| `Phong thi nghiem ao/Assets/Scripts/Demo/Bio/ThucVat/Bridge/*` | `5f32e6fa` | Assembly cầu nối `Khkt.Bio.ThucVat.ThucVatMoDun` (`autoReferenced = false`) + `link.xml` |
| `Phong thi nghiem ao/Assets/Scripts/Editor/KhktN18Verify.cs` | `5f32e6fa` | Bổ sung nhóm `"G3"` (`RunG3()`) và chuỗi `Run()` gồm G2 + G3 |

---

## 4. Kết quả kiểm thử & hiệu năng

| Kiểm thử | Lệnh | Kết quả |
|---|---|---|
| Unit test Bio toàn hệ | `bash tools/chay_kiem_thu_hoa.sh tests/BioSimulationTests.cs` | **114 đạt, 0 lỗi** |
| Unit test ThucVat thuần C# | `.NET csc` trên `ThucVatSimTests.RunAll` (và chạy lại trong `RunG3`) | **12 đạt, 0 lỗi** |
| Kiểm thử tự động G3 | `Unity -batchmode -buildTarget StandaloneOSX -executeMethod Khkt.EditorTools.KhktN18Verify.RunG3` | `[N18_G3_SUMMARY] passes=10 failures=0 runtimeErrors=0` (`[N18_G3_RESULT] ALL PASSED`, thoát `0`). Quang hợp: `low=0.71, mid=25.81, sat=35.69, night=-2.66`; Độ ẩm & ABA: `rh25=2.44, rh88=0.78, turgorDry=0.18, wet=1.00`; Hướng sáng: `bend=47.6deg`. |
| Nhịp khung khi mở mô-đun (`Cao`) | Đo 45 khung trong `KhktN18Verify.RunG3` (`HardwareProfile.Dat(MucDoHoa.Cao)`) | `0.733 s` cho 45 khung → **~61.4 FPS** (ngay cả trong Editor PlayMode). |
| Hồi quy Wave 6 | `Unity -batchmode -buildTarget StandaloneOSX -executeMethod KhktWave6Verify.Run` | `[W6_SUMMARY] passes=67 failures=0 runtimeErrors=0 startupSearchIgnored=0 shots=12` (`[W6_RESULT] ALL PASSED`, thoát `0`). |

---

## 5. Ảnh nghiệm thu (trong `docs/ngay-18-11/anh/g3/`)

1. **Tổng thể cây đậu hai lá mầm & chế độ Bài giảng:** [g3_1_tong_the_cay_hai_la_mam.png](anh/g3/g3_1_tong_the_cay_hai_la_mam.png)
2. **Lát cắt Rễ & đai Caspari chặn gian bào:** [g3_2_lat_cat_re_caspari.png](anh/g3/g3_2_lat_cat_re_caspari.png)
3. **Lát cắt Thân bó mạch hở (rây ngoài / tầng sinh mạch / gỗ trong):** [g3_3_lat_cat_than_bo_mach.png](anh/g3/g3_3_lat_cat_than_bo_mach.png)
4. **Lát cắt Lá & khí khổng 2 tế bào hạt đậu mở:** [g3_4_lat_cat_la_khi_khong.png](anh/g3/g3_4_lat_cat_la_khi_khong.png)
5. **Quang hợp, dòng mạch & uốn cong hướng sáng + phân bố auxin phía tối:** [g3_5_quang_hop_va_huong_sang.png](anh/g3/g3_5_quang_hop_va_huong_sang.png)
6. **Lối vào Bàn mô hình 3D Thực vật tại Nhà kính tầng 2:** [g3_6_ban_mo_hinh_nha_kinh.png](anh/g3/g3_6_ban_mo_hinh_nha_kinh.png)
