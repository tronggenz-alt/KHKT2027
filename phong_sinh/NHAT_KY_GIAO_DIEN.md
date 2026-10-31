# Nhật ký triển khai Giao diện – Dụng cụ & Khu vực Tầng 2 (Khu Sinh học)

- **Nhánh làm việc:** `tang2/dung-cu-giao-dien` (tách từ `tang2/dung-cu-backend`, không `git push`, không mở PR)
- **Tài liệu gốc:** `phong_sinh/PROMPT_DUNG_CU_GIAO_DIEN.md` & `phong_sinh/HOP_DONG_GIAO_DIEN_BACKEND.md`

---

## 1. Kế hoạch tổng thể các giai đoạn (G0 → G14)

| Giai đoạn | Nội dung trọng tâm | Đầu ra & Kiểm chứng |
|---|---|---|
| **G0** | Rà soát toàn bộ dụng cụ hiện có, phím tắt, chụp ảnh hiện trạng 12 trạm (`20..31`) vào `docs/anh-tang2/truoc_dung_cu/` và 5 ảnh hành lang 2,2 m trước khi mở rộng vào `docs/anh-tang2/hanh_lang/`. | Ảnh `truoc_dung_cu/` (12 tấm), ảnh `01,03,05,07,09` hành lang 2,2 m, khởi tạo `phong_sinh/NHAT_KY_GIAO_DIEN.md`. |
| **G1** | Mở rộng hành lang Tầng 2 từ 2,2 m (`z: -2,2..0`) lên 3,6 m (`z: -3,6..0`). Cập nhật `ZoneMap.cs`, `LabSceneBuilder.Floor2.cs`, `Shell.cs`, `SpecialRooms.cs`, `bo_cuc_tang2.json`, `docs/20-tang-2-khu-sinh-hoc.md`. Giữ nguyên Tầng 1 và lõi thang. | AutoTest `tang2_hanh_lang` PASS, 10 ảnh đối chiếu trước/sau tại `docs/anh-tang2/hanh_lang/` đều > 80 KB. |
| **G2** | Xây dựng khung tương tác dùng chung `Assets/Scripts/Interaction/Bio/` (`BioKnob`, `BioButton`, `BioHinge`, `IBioTarget`, `BioTool`, `ProgressRing`, `BioFeedback`, `CloseUpView`), tích hợp `HandController.cs`, `AutoDoor.cs` (`interlockWith`), cập nhật `IsFloor2Dynamic`. | Biên dịch sạch 0 lỗi, tương tác nhặt/xoay/bấm/mở nắp/giữ chuột 3D mượt. |
| **G3** | Đồng bộ nhánh `tang2/dung-cu-backend` (sau khi Backend hoàn tất). Xây dựng `BioSample.cs`, `BioTransfer.cs` (cây quyết định chuyển mẫu 6 trường hợp + hậu quả thao tác sai), nạp tự động 83 mục từ `data/sinh/dung_cu.json` gắn `EquipmentInfo`. | 83/83 mục có `EquipmentInfo` đầy đủ trường, `BioTransfer` hoạt động đúng với các vật chứa. |
| **G4** | Cụm Kính hiển vi & Tiêu bản (Bàn 1–2, Trạm 20–21): nâng cấp `MicroscopeController` (4 vật kính 4×/10×/40×/100× dầu, 2 núm sơ/vi cấp, mâm trượt X–Y, vòng chắn sáng, giọt dầu soi, vỡ lamen ở 40×/100× khi vặn sơ cấp), `MicroscopeHud`, `SlidePrepBench` (lam kính, lamen 45°, kim mũi mác, giấy thấm, 5 thuốc nhuộm), `HandMagnifier` (`RenderTexture` phóng đại 3×). | Thí nghiệm TN01, TN02, TN03, TN13 chạy trọn vẹn trên bàn kính hiển vi. |
| **G5** | Cụm Sinh lý Thực vật & Quang hợp (Bàn 3, Trạm 22): `PhotosynthesisRig` nối `PhotosynthesisSim` (đèn LED trượt 5–50 cm, công tắc 3 màu Đỏ/Xanh/Trắng, thêm NaHCO₃, đếm bọt tự động), `OsmosisPotometerRig` nối `OsmosisSim` & `TranspirationSim` (3 cốc khoai tây, thẩm kế, túi nilon, chuông thủy tinh + nước vôi trong + nến, hạt nảy mầm đối chứng, bình tam giác rễ cây). | Thí nghiệm TN04, TN05, TN06, TN07, TN08 chạy trọn vẹn. |
| **G6** | Cụm Vi sinh & Tủ an toàn sinh học (Bàn 4, Trạm 23): `BioSafetyCabinet` (cửa trượt, luồng khí tầng, đèn UV khóa liên động với làm việc), `StreakPlateRig` nối `MicrobialGrowthModel` + `SterileTechniqueSim` (đèn cồn, que cấy nóng đỏ 800 °C → nguội 35 °C, ria 4 vùng trên đĩa Petri, tủ ấm 37 °C, lọ khuẩn mẫu, thùng rác sinh học). | Thí nghiệm TN09 (nuôi cấy & ria 4 vùng) và phát hiện lỗi thao tác vô trùng (TN09). |
| **G7** | Cụm Hóa sinh & Enzyme (Bàn 5–6, Trạm 24): `WaterBathController` (bể điều nhiệt 20–90 °C, 6 giếng), `EnzymeAssayRig` nối `EnzymeKinetics` (ống nghiệm cơ chất + enzyme + thuốc thử I₂/Benedict/Biuret, đồng hồ bấm giờ, máy khuấy từ sinh học, giá phơi, chổi rửa). | Thí nghiệm TN10 (catalase + H₂O₂ + tàn đóm đỏ bùng cháy) và TN11 (amylase + tinh bột + I₂ theo nhiệt độ/pH). |
| **G8** | Cụm Giải phẫu – Sinh lý người (Trạm 25–26): `AnatomyModelsInteraction` (bộ xương, mô hình tim mở nửa trước + nhịp đập theo BPM, thận + nephron, mắt + thấu kính, não + tai tách vùng, khay mổ ếch), nâng cấp `DigitalAnatomyTable` (thước đo 2 điểm mm, ghim nhãn, cắt lát phẳng X/Y/Z, câu đố "Tìm bộ phận"), `PhysiologyRig` nối `CardioRespSim` & `ReflexArcSim` (máy đo HA + ống nghe Korotkoff, SpO₂, phế dung kế VC/FEV₁, búa phản xạ gân bánh chè, bảng thị lực Snellen). | Thí nghiệm TN12 (huyết áp, nhịp tim, phế dung trước/sau vận động, phản xạ gân gối). |
| **G9** | Phòng Sinh học phân tử (Trạm 28–29): `MicropipetteController` (P20/P200/P1000, vòng chỉnh µL, 2 nấc piston, phóng đầu côn, hộp đầu côn 96 lỗ, cảnh báo hút quá tầm/nghiêng > 45°/quên đầu côn), `ThermalCyclerController` nối `PcrEngine` (màn hình `InstrumentScreen` biểu đồ nhiệt 3 bước × N chu kỳ), `GelBoxController` nối `GelElectrophoresisSim` (bể điện di + nguồn 50–150 V + bọt khí H₂/O₂ + vạch Bromophenol Blue), `UvTransilluminatorController` (nắp chắn cam an toàn, phát sáng vạch ADN EtBr/SYBR), `MicroCentrifugeController` nối `CentrifugeSafetySim` (kiểm tra đối xứng rotor 12 vị trí, khóa nắp khi quay, rung lắc khi lệch cân), `VortexMixerController`, `FreezerMinus80Controller` (−80 °C, sương lạnh, cảnh báo > 30 s), `DnaHelixInteractive`, `AirlockController` (khóa liên động 2 cửa + vòi rửa mắt khẩn cấp). | Thí nghiệm TN14 (PCR & điện di ADN có thang chuẩn 100–1000 bp) & kiểm tra an toàn ly tâm/pipet. |
| **G10** | Nhà kính Thực nghiệm & Khu phụ trợ (Trạm 27, 30, 31): `HydroponicSystemController` nối `HydroponicsSim` (bơm tuần hoàn, bút pH/EC, châm A+B/pH Down, hiện triệu chứng thiếu N/P/K/Fe/ngộ độc muối), `GreenhouseClimateController` (đèn quang chu kỳ 8h/12h/16h, phun sương hạ nhiệt/tăng ẩm, hộp hướng sáng có cửa sổ bên nối `TropismSim`, chậu cây đối chứng vs thí nghiệm, bình tưới), `AutoclaveController` nối `AutoclaveSim` (khóa nắp khi P > 105 kPa, chu trình 121 °C – 200 kPa – 15 phút, xả áp an toàn, băng chỉ thị nhiệt đổi màu), `CorridorShowcases` (tủ tiêu bản gõ số 1–6 xem tiêu bản mẫu trên HUD, ki-ốt tra cứu mở bảng 83 dụng cụ). | Thí nghiệm TN15 (thủy canh & hướng sáng) và vận hành nồi hấp tiệt trùng an toàn. |
| **G11** | Đồng bộ Unity ↔ Web (`Assets/Scripts/Demo/Bio/BioLabSync.cs`): gửi sự kiện nhật ký thao tác (`POST /api/bio/log`), gửi kết quả thí nghiệm (`POST /api/bio/experiment/run`), thăm dò nhà kính mỗi 5 s (`GET /api/bio/greenhouse/state`), hiển thị HUD trợ giảng AI khi thao tác sai (`POST /api/bio/assistant`). Chạy offline êm khi tắt server. | Khi server bật, thao tác 3D hiển thị tức thời trên web; khi server tắt không sinh lỗi Đỏ. |
| **G12** | Trang web Quản lý Phòng thí nghiệm Sinh học (`server/web/sinh.html` + thêm đúng 1 thẻ chuyển trang vào `server/web/index.html`): 5 thẻ chức năng (Sổ tay 83 dụng cụ + 15 quy trình, Bảng điều khiển thí nghiệm + Canvas 2D vẽ đồ thị/vạch điện di/bản đồ 8 khu, Trợ giảng Sinh học & Kiểm tra lỗi thao tác, Điều khiển Nhà kính & Tủ ấm từ xa, Nhật ký thực hành & Xuất báo cáo `.json`/in phiếu). | Mở `http://localhost:8000/sinh.html` đầy đủ 5 thẻ, không thư viện ngoài, số liệu định dạng dấu phẩy thập phân tiếng Việt. |
| **G13** | Tối ưu hiệu năng & Benchmark (`KhktBenchmark`): đảm bảo chỉ tối đa 1 `RenderTexture` phụ bật cùng lúc, `ParticleSystem` chỉ bật khi thiết bị chạy, không cấp phát rác mỗi khung hình. | `avg_fps >= 45`, `SetPass calls < 500`, `Triangles < 400.000`, `Mono heap delta < 20 MB`, `Console errors = 0`. |
| **G14** | Tài liệu (`docs/21-dung-cu-sinh-hoc.md` phần Giao diện, cập nhật `docs/20-tang-2-khu-sinh-hoc.md`), AutoTest (`tang2_dung_cu`, `tang2_hanh_lang`, `tang2_web`, `tang2`), chụp toàn bộ bộ ảnh `docs/anh-tang2/dung_cu/` (≥ 25 tấm) và `docs/anh-tang2/web/` (5 tấm), viết `phong_sinh/BAO_CAO_BAN_GIAO_GIAO_DIEN.md`. | Tất cả AutoTest PASS, ảnh > 80 KB, báo cáo bàn giao hoàn chỉnh. |

---

## 2. Rà soát toàn bộ phím tắt (Đảm bảo không xung đột)

| Phím / Chuột | Chức năng hiện có trong dự án | Nơi xử lý | Tương tác Sinh học (Tầng 2) |
|---|---|---|---|
| `W, A, S, D` + `Shift` | Di chuyển / chạy nhanh | `DesktopViewer`, `KeyboardMouseControllerSource` | Giữ nguyên |
| `C` | Ngồi / Đứng | `DesktopViewer` | Giữ nguyên |
| `Z / X` | Hạ / Nâng tầm mắt | `DesktopViewer` | Giữ nguyên |
| `← → / PageUp PageDown` | Quay ngang / Ngẩng cúi camera | `DesktopViewer`, `KeyboardMouseControllerSource` | Giữ nguyên |
| `1..9, 0` | Dịch chuyển nhanh đến trạm Tầng 1 | `DesktopViewer` | Giữ nguyên (Tầng 2 dùng menu trạm `20..31` hoặc đi bộ/thang máy) |
| `Tab / Alt` | Bật/tắt chế độ nhìn chuột / hiện con trỏ | `DesktopViewer` | Giữ nguyên |
| `Chuột trái (Click)` | Nhặt / Đặt vật (`Grabbable`), bấm nút (`BioButton`), gạt nắp (`BioHinge`), chọn vật phẩm | `HandController` | Tích hợp ưu tiên `BioButton` → `BioHinge` → `BioTool` / `Grabbable` |
| `Giữ Chuột trái` | Nhỏ giọt từ pipet/bình tia | `HandController` | Tác động liên tục dụng cụ đang cầm (`BioTool`) lên `IBioTarget` + hiện `ProgressRing` |
| `Cuộn chuột (Scroll)` | Chưa gắn trên vật thể 3D | `HandController` | Xoay núm vặn (`BioKnob`) khi tia ngắm trỏ vào núm, hoặc chỉnh µL khi đang cầm Micropipette |
| `Chuột phải (Click)` | Xem thẻ thông tin thiết bị (`EquipmentInfo`) | `HandController` | Giữ nguyên; hiển thị thông tin 83 dụng cụ + thông báo trợ giảng khi thao tác sai |
| `F` | Lắc bình chuẩn độ / thao tác phụ | `HandController` | Phóng đầu côn Micropipette khi hướng vào thùng rác sinh học (`thung_rac_sinh_hoc`) |
| `Q` | Trả vật đang cầm về vị trí gốc | `HandController` | Giữ nguyên |
| `R / E` | Xoay chi tiết lắp ráp / làm mới lượt chuẩn độ | `HandController`, `FabricationWorkspace` | Giữ nguyên |
| `B` | Mở tra cứu hóa chất (`ChemSearchUI`) | `ChemSearchUI` | Giữ nguyên |
| `T` | Mở hội thoại trợ lý AI (`RobotChatUI`) | `RobotChatUI` | Giữ nguyên |
| `H` | Bật/tắt hướng dẫn phím tắt | `HandController` | Giữ nguyên |
| `G` | Bật/tắt đồ thị | `HandController` | Giữ nguyên |
| `Enter / NumpadEnter` | Ghi số đo / Gửi tin nhắn | `HandController`, `ChemSearchUI`, `RobotChatUI` | Giữ nguyên |
| `Esc` | Đóng bảng UI / `InstrumentScreen` | `IntroScreen`, `InstrumentScreen`, `ChemSearchUI`, `RobotChatUI` | Đóng thêm góc nhìn cận cảnh `CloseUpView` |
| `F7` | Đổi chủ đề màu Tầng 2 | `BioThemeSwitcher` | Giữ nguyên |
| `F8 / F9` | Bật/tắt & Cân chỉnh theo dõi đầu | `HeadTracking` | Giữ nguyên |

**Kết luận rà soát phím:** Toàn bộ thao tác dụng cụ sinh học dùng Chuột trái (click/giữ/kéo), Cuộn chuột (trên `BioKnob` hoặc Micropipette đang cầm), Chuột phải (tra cứu `EquipmentInfo`), `Esc` (thoát `CloseUpView`), và `F` (phóng đầu côn vào thùng rác sinh học khi cầm Micropipette). Không đè bất kỳ phím tắt nào hiện có.

---

## 3. Rà soát 83 dụng cụ trong `HOP_DONG_GIAO_DIEN_BACKEND.md` (Mục 2)

| STT | Mã định danh (`id`) | Khu vực | Hiện trạng trước G0 | Script nâng cấp (G3–G10) |
|---|---|---|---|---|
| 1 | `kinh_hien_vi_quang_hoc` | Bàn 1–2 (Trạm 20–21) | Có `MicroscopeController` cơ bản | Nâng cấp `MicroscopeController` + `MicroscopeHud` (4 vật kính 4×/10×/40×/100×, 2 núm sơ/vi cấp `BioKnob`, mâm X–Y, chắn sáng, dầu soi, vỡ lamen) |
| 2 | `kinh_hien_vi_hai_mat_digital` | Bàn GV (Trạm 20) | Mô hình tĩnh | `MicroscopeController` (chế độ giáo viên + truyền hình ảnh lên màn hình phụ) |
| 3 | `hop_tieu_ban_mau` | Bàn 1–2 | Mô hình tĩnh | `SlideBoxSource` (lấy ra 6 loại tiêu bản cố định `BioSample`) |
| 4 | `lam_kinh` | Bàn 1–2 | Mô hình tĩnh | `SlidePrepBench` + `BioSample` + `Grabbable` |
| 5 | `lamen` | Bàn 1–2 | Mô hình tĩnh | `SlidePrepBench` (`BioHinge` hạ góc 45° tránh bọt khí) |
| 6 | `lo_thuoc_nhuom_i2` | Bàn 1–2 | Mô hình tĩnh | `BioTool` (`StainDropper`: Iốt Lugol) + `BioTransfer` |
| 7 | `lo_thuoc_nhuom_xanh_methylen` | Bàn 1–2 | Mô hình tĩnh | `BioTool` (`StainDropper`: Xanh methylen) + `BioTransfer` |
| 8 | `lo_thuoc_nhuom_orcein` | Bàn 1–2 | Mô hình tĩnh | `BioTool` (`StainDropper`: Orcein axetic) + `BioTransfer` |
| 9 | `lo_thuoc_nhuom_sudan_iii` | Bàn 1–2 | Mô hình tĩnh | `BioTool` (`StainDropper`: Sudan III) + `BioTransfer` |
| 10 | `lo_dau_soi_immersion` | Bàn 1–2 | Mô hình tĩnh | `BioTool` (`ImmersionOilDropper` cho vật kính 100×) |
| 11 | `kim_mui_mac` | Bàn 1–2 | Mô hình tĩnh | `BioTool` (`MountingNeedle`: dàn phẳng biểu bì hành, ép nhẹ lamen) |
| 12 | `dao_mo_sinh_hoc` | Bàn 1–2, Giải phẫu | Mô hình tĩnh | `BioTool` (`Scalpel`: tách biểu bì hành, cắt lát mỏng) |
| 13 | `nhip_sinh_hoc` | Bàn 1–2, Giải phẫu | Mô hình tĩnh | `BioTool` (`Forceps`: gắp mẫu biểu bì hành/lá rong mái chèo) |
| 14 | `giay_tham_sinh_hoc` | Bàn 1–2 | Mô hình tĩnh | `BioTool` (`BlottingPaper`: hút thuốc nhuộm thừa ở mép lamen) |
| 15 | `kinh_lup_cam_tay` | Bàn 1–2 | Mô hình tĩnh | `HandMagnifier` (`Grabbable` + `Camera` phụ phóng đại 3× khi cầm) |
| 16 | `mau_cu_hanh_tay` | Bàn 1–2 | Mô hình tĩnh | `BioSampleSource` (tách biểu bì hành tây `hanh_tay_bieu_bi`) |
| 17 | `mau_rong_mai_cheo` | Bàn 1–2, Bàn 3 | Mô hình tĩnh | `BioSampleSource` (lá rong mái chèo *Elodea* `la_rong_mai_cheo`) |
| 18 | `coc_rong_elodea_quang_hop` | Bàn 3 (Trạm 22) | Có `ElodeaBubbles` tĩnh | `PhotosynthesisRig` nối `PhotosynthesisSim` (tốc độ bọt phụ thuộc I, T, CO₂, λ) |
| 19 | `den_led_quang_hop` | Bàn 3 (Trạm 22) | Mô hình tĩnh | `PhotosynthesisRig` (kéo trượt 5–50 cm, nút đổi màu Đỏ 660 nm / Xanh 450 nm / Trắng) |
| 20 | `lo_nahco3_2pct` | Bàn 3 (Trạm 22) | Mô hình tĩnh | `BioTool` (bổ sung NaHCO₃ tăng nồng độ CO₂ hòa tan trong cốc Elodea) |
| 21 | `bo_3_coc_tham_thau_khoai_tay` | Bàn 3 (Trạm 22) | Mô hình tĩnh | `OsmosisPotometerRig` nối `OsmosisSim` (3 cốc: nước cất, NaCl 0,9%, NaCl 5% + cân khối lượng/độ cứng) |
| 22 | `tham_ke_potometer` | Bàn 3 (Trạm 22) | Mô hình tĩnh | `OsmosisPotometerRig` nối `TranspirationSim` (bọt khí trong ống mao quản dịch chuyển + quạt gió/đèn) |
| 23 | `chau_cay_boc_tui_nilon` | Bàn 3 (Trạm 22) | Mô hình tĩnh | `OsmosisPotometerRig` (hạt sương ngưng tụ bên trong túi + thử giấy CoCl₂ đổi Xanh → Hồng) |
| 24 | `chuong_thuy_tinh_ho_hap` | Bàn 3 (Trạm 22) | Mô hình tĩnh | `OsmosisPotometerRig` (nước vôi trong Ca(OH)₂ vẩn đục CaCO₃ + nến tắt) |
| 25 | `binh_hat_nay_mam_doi_chung` | Bàn 3 (Trạm 22) | Mô hình tĩnh | `OsmosisPotometerRig` (2 bình nhiệt kế: hạt sống tăng lên ~29,8 °C, hạt luộc chín giữ ~25,0 °C) |
| 26 | `binh_tam_giac_re_cay_khoang` | Bàn 3 (Trạm 22) | Mô hình tĩnh | `OsmosisPotometerRig` (so sánh bình đủ khoáng lá xanh vs thiếu N lá vàng nhạt) |
| 27 | `tu_an_toan_sinh_hoc_cap_2` | Bàn 4 (Trạm 23) | Mô hình tĩnh | `BioSafetyCabinet` (cửa kính trượt `BioHinge`, công tắc quạt laminar + đèn chiếu + đèn UV khóa liên động) |
| 28 | `den_con_sinh_hoc` | Bàn 4 (Trạm 23) | Mô hình tĩnh | `StreakPlateRig` (bật/tắt ngọn lửa, vùng vô trùng bán kính 15 cm, nung đỏ que cấy) |
| 29 | `que_cay_vi_sinh` | Bàn 4 (Trạm 23) | Mô hình tĩnh | `StreakPlateRig` (`Grabbable` + nhiệt độ đầu que 25–800 °C + lấy giống + ria 4 vùng) |
| 30 | `dia_petri_thach_dinh_duong` | Bàn 4 (Trạm 23) | Có `BacterialColonyAnimation` | `StreakPlateRig` nối `MicrobialGrowthModel` + `SterileTechniqueSim` (ria 4 vùng, ủ ấm, mọc khuẩn lạc hoặc mốc tạp nhiễm) |
| 31 | `lo_giong_vi_khuan_mau` | Bàn 4 (Trạm 23) | Mô hình tĩnh | `StreakPlateRig` (lọ *E. coli* K-12, *B. subtilis*, *S. cerevisiae* có nắp mở/đóng cạnh đèn cồn) |
| 32 | `tu_am_vi_sinh` | Bàn 4 (Trạm 23) | Mô hình tĩnh | `StreakPlateRig` (cửa mở `BioHinge`, chỉnh nhiệt 20–60 °C `BioKnob`, nút tua nhanh thời gian ủ 0–48 h) |
| 33 | `thung_rac_sinh_hoc` | Bàn 4, Phòng SHPT | Mô hình tĩnh | `BioWasteBin` (nhận đầu côn đã dùng, giấy thấm, đĩa Petri bỏ) |
| 34 | `be_dieu_nhiet_water_bath` | Bàn 5–6 (Trạm 24) | Mô hình tĩnh | `WaterBathController` (nắp `BioHinge`, núm nhiệt 20–90 °C `BioKnob`, 6 giếng đặt ống nghiệm, màn LED) |
| 35 | `gia_ong_nghiem_sinh_hoc` | Bàn 5–6 (Trạm 24) | Mô hình tĩnh | `EnzymeAssayRig` (giá 6 ống nghiệm có `PlaceSocket`, nhãn tự động, nhận mẫu qua `BioTransfer`) |
| 36 | `bo_thuoc_thu_sinh_hoa` | Bàn 5–6 (Trạm 24) | Mô hình tĩnh | `EnzymeAssayRig` (5 lọ: Tinh bột 1%, Amylase/Catalase, H₂O₂ 3%, Benedict, Biuret + que đóm thử O₂) |
| 37 | `dong_ho_bam_gio_sinh_hoc` | Bàn 5–6 (Trạm 24) | Mô hình tĩnh | `EnzymeAssayRig` (Chạy/Dừng/Đặt lại, hiển thị mm:ss,d) |
| 38 | `may_khuay_tu_sinh_hoc` | Bàn 5–6 (Trạm 24) | Mô hình tĩnh | `EnzymeAssayRig` (núm RPM `BioKnob` + cá từ quay trong cốc) |
| 39 | `gia_phoi_dung_cu_sinh_hoc` | Bồn rửa (Trạm 24) | Mô hình tĩnh | `LabSinkAccessory` (úp ống nghiệm/cốc sau khi tráng rửa) |
| 40 | `choi_rua_ong_nghiem` | Bồn rửa (Trạm 24) | Mô hình tĩnh | `BioTool` (làm sạch ống nghiệm đã dùng tại bồn rửa) |
| 41 | `bo_xuong_nguoi_170cm` | Trạm 25 | Mô hình tĩnh | `AnatomyModelsInteraction` (click từng vùng xương: hộp sọ, cột sống, lồng ngực, xương chậu, chi trên/dưới) |
| 42 | `mo_hinh_tim_nguoi` | Trạm 25 | Mô hình tĩnh | `AnatomyModelsInteraction` (`BioHinge` mở nửa trước tim + co bóp theo BPM từ `CardioRespSim`) |
| 43 | `mo_hinh_than_nephron` | Trạm 25 | Mô hình tĩnh | `AnatomyModelsInteraction` (chỉ vị trí cầu thận, ống lượn gần, quai Henle, ống góp + luồng lọc) |
| 44 | `mo_hinh_mat_nguoi` | Trạm 25 | Mô hình tĩnh | `AnatomyModelsInteraction` (thay thấu kính phân kỳ/hội tụ minh họa tật cận thị/viễn thị) |
| 45 | `mo_hinh_nao_va_tai` | Trạm 25 | Mô hình tĩnh | `AnatomyModelsInteraction` (tách 2 bán cầu đại não, tiểu não, thân não; ốc tai & 3 ống bán khuyên) |
| 46 | `bo_dung_cu_mo_ech` | Trạm 25 | Mô hình tĩnh | `AnatomyModelsInteraction` (khay mổ sáp + mô hình ếch giải phẫu các cơ quan nội tạng) |
| 47 | `ban_giai_phau_ao_65inch` | Trạm 26 | Có `DigitalAnatomyTable` | Nâng cấp `DigitalAnatomyTable` (thước đo 2 điểm mm, ghim nhãn, cắt lát phẳng X/Y/Z, câu đố "Tìm bộ phận") |
| 48 | `may_do_huyet_ap_ong_nghe` | Trạm 25–26 | Mô hình tĩnh | `PhysiologyRig` nối `CardioRespSim` (bơm bao khí 180 mmHg, xả van 2–3 mmHg/s, âm Korotkoff chốt tâm thu/tâm trương) |
| 49 | `may_do_spo2_nhip_tim` | Trạm 25–26 | Mô hình tĩnh | `PhysiologyRig` nối `CardioRespSim` (hiển thị SpO₂ % và BPM theo mức gắng sức) |
| 50 | `phe_dung_ke_spirometer` | Trạm 25–26 | Mô hình tĩnh | `PhysiologyRig` nối `CardioRespSim` (đo TV, IRV, ERV, VC và FEV₁/FVC) |
| 51 | `bua_phan_xa_than_kinh` | Trạm 25–26 | Mô hình tĩnh | `PhysiologyRig` nối `ReflexArcSim` (gõ gân bánh chè → đá cẳng chân sau ~35 ms + hiện sơ đồ cung phản xạ 5 khâu) |
| 52 | `bang_thi_luc_snellen` | Trạm 25 | Mô hình tĩnh | `PhysiologyRig` (đứng cách 5 m đọc hàng chữ, chấm thị lực 1/10 → 10/10) |
| 53 | `micropipet_bo_3_cay` | Phòng SHPT (Trạm 28–29) | Mô hình tĩnh | `MicropipetteController` (P20: 2–20 µL, P200: 20–200 µL, P1000: 100–1000 µL; chỉnh µL, gắn/phóng đầu côn, 2 nấc piston) |
| 54 | `hop_dau_con_pipet` | Phòng SHPT (Trạm 28–29) | Mô hình tĩnh | `TipBoxController` (`BioHinge` mở nắp + 96 đầu côn vơi dần khi gắn vào Micropipette) |
| 55 | `may_pcr_thermal_cycler` | Phòng SHPT (Trạm 28–29) | Mô hình tĩnh | `ThermalCyclerController` nối `PcrEngine` (nắp nhiệt `BioHinge`, khay 96 giếng, màn hình `InstrumentScreen` biểu đồ nhiệt) |
| 56 | `ong_pcr_02ml_va_gia` | Phòng SHPT (Trạm 28–29) | Mô hình tĩnh | `PcrTubeRack` (ống 0,2 mL nhận khuôn ADN + mồi + Master Mix qua Micropipette, đặt vào máy PCR) |
| 57 | `bo_dien_di_gel_agarose` | Phòng SHPT (Trạm 28–29) | Có `GelMigrationAnimation` | `GelBoxController` nối `GelElectrophoresisSim` (8 giếng nạp mẫu + nguồn 50–150 V + bọt khí điện cực + vạch chỉ thị) |
| 58 | `ban_soi_uv_transilluminator` | Phòng SHPT (Trạm 28–29) | Mô hình tĩnh | `UvTransilluminatorController` (nắp chắn cam an toàn `BioHinge` + phát sáng vạch ADN huỳnh quang theo bp) |
| 59 | `may_ly_tam_vi_luong` | Phòng SHPT (Trạm 28–29) | Mô hình tĩnh | `MicroCentrifugeController` nối `CentrifugeSafetySim` (rotor 12 vị trí kiểm tra đối xứng, khóa nắp khi quay, báo lỗi lệch cân) |
| 60 | `ong_eppendorf_15ml` | Phòng SHPT (Trạm 28–29) | Mô hình tĩnh | `EppendorfTube` (`Grabbable`, đặt vào rotor ly tâm hoặc máy Vortex, phân lớp cặn/dịch nổi sau ly tâm) |
| 61 | `may_lac_vortex` | Phòng SHPT (Trạm 28–29) | Mô hình tĩnh | `VortexMixerController` (ấn ống Eppendorf xuống mâm cao su → rung tròn 2800 RPM + xoáy chất lỏng) |
| 62 | `tu_lanh_sau_am_80` | Phòng SHPT (Trạm 28) | Mô hình tĩnh | `FreezerMinus80Controller` (cửa mở `BioHinge` + làn sương lạnh + màn LED −80,0 °C + còi báo khi mở > 30 s) |
| 63 | `mo_hinh_xoan_kep_adn` | Phòng SHPT (Trạm 29) | Mô hình tĩnh | `DnaHelixInteractive` (xoay 360°, chỉ cặp base A–T 2 liên kết H, G–X 3 liên kết H, chiều 5'→3') |
| 64 | `buong_dem_airlock_uv` | Lối vào SHPT (Trạm 28) | Có 2 cửa `AutoDoor` | `AirlockController` + `AutoDoor.interlockWith` (khóa liên động 2 cửa không mở cùng lúc + đèn báo xanh/đỏ) |
| 65 | `voi_rua_mat_khan_cap` | Phòng SHPT (Trạm 28) | Mô hình tĩnh | `EmergencyEyewashController` (gạt cần → 2 tia nước phun vòm để sơ cứu khi bắn hóa chất/mẫu) |
| 66 | `gian_thuy_canh_nft` | Nhà kính (Trạm 30) | Mô hình tĩnh | `HydroponicSystemController` nối `HydroponicsSim` (3 tầng máng NFT, dòng chảy dinh dưỡng, cây lớn & đổi màu lá theo pH/EC) |
| 67 | `bon_pha_dinh_duong_ab` | Nhà kính (Trạm 30) | Mô hình tĩnh | `HydroponicSystemController` (bồn 50 L + bơm tuần hoàn + châm dung dịch A, B, pH Down, nước sạch) |
| 68 | `but_do_ph_ec_thuy_canh` | Nhà kính (Trạm 30) | Mô hình tĩnh | `HydroponicMeter` (nhúng vào bồn NFT đọc trực tiếp pH 5,8–6,2 và EC mS/cm) |
| 69 | `he_thong_den_quang_chu_ky` | Nhà kính (Trạm 30) | Mô hình tĩnh | `GreenhouseClimateController` (chuyển chế độ ngày ngắn 8h / trung tính 12h / ngày dài 16h, đổi phổ R:B) |
| 70 | `he_thong_phun_suong_nha_kinh` | Nhà kính (Trạm 30) | Mô hình tĩnh | `GreenhouseClimateController` (bật/tắt 4 béc phun sương → hạ nhiệt 2–3 °C, tăng ẩm lên 80–85%) |
| 71 | `hop_thi_nghiem_huong_sang` | Nhà kính (Trạm 30) | Mô hình tĩnh | `PhototropismBoxController` nối `TropismSim` (mở cửa sổ trái/phải/trên → ngọn cây uốn cong về phía sáng nhờ auxin) |
| 72 | `day_chau_cay_doi_chung` | Nhà kính (Trạm 30) | Mô hình tĩnh | `GreenhousePotRow` (so sánh trực quan lô đối chứng vs lô thiếu N, thiếu P, thiếu Fe, thiếu sáng) |
| 73 | `binh_tuoi_cay_nha_kinh` | Nhà kính (Trạm 30) | Mô hình tĩnh | `BioTool` (`WateringCan`: cầm tưới cho các chậu cây đất tăng độ ẩm giá thể) |
| 74 | `noi_hap_tiet_trung_autoclave` | Khu chuẩn bị (Trạm 31) | Mô hình tĩnh | `AutoclaveController` nối `AutoclaveSim` (khóa nắp an toàn khi P > 105 kPa, chu trình 121 °C – 200 kPa – 15 phút, van xả áp) |
| 75 | `may_cat_nuoc_1_lan` | Khu chuẩn bị (Trạm 31) | Mô hình tĩnh | `WaterDistillerBioController` (bật đun → nhỏ giọt nước cất vào bình chứa, cung cấp nước cất cho toàn tầng) |
| 76 | `can_dien_tu_sinh_hoc` | Khu chuẩn bị (Trạm 31) | Mô hình tĩnh | `BioBalanceScale` (nút Bật/Tắt, Trừ bì `Tare`, cân mẫu khoai tây thẩm thấu hoặc bột thạch agar chính xác 0,01 g) |
| 77 | `tu_bao_quan_mau_4_do` | Khu chuẩn bị (Trạm 31) | Mô hình tĩnh | `Fridge4CController` (cửa mở `BioHinge`, bảo quản enzyme/thuốc thử ở 4,0 °C tránh biến tính) |
| 78 | `bang_phan_loai_rac_sinh_hoc` | Khu chuẩn bị (Trạm 31) | Mô hình tĩnh | `WasteSortingGuide` (tương tác phân loại 3 nhóm rác: sinh hoạt xanh, lây nhiễm vàng, sắc nhọn hộp cứng) |
| 79 | `tu_trung_bay_tieu_ban_hanh_lang` | Hành lang (Trạm 27) | Mô hình tĩnh | `CorridorShowcases` (bấm phím 1–6 xem nhanh 6 tiêu bản mẫu có chú thích giải phẫu trên HUD) |
| 80 | `tram_tra_cuu_dung_cu_hanh_lang` | Hành lang (Trạm 27) | Mô hình tĩnh | `CorridorShowcases` (ki-ốt cảm ứng mở bảng tra cứu 83 dụng cụ + chỉ đường đến trạm tương ứng) |
| 81 | `ghe_bang_cho_hanh_lang` | Hành lang (Trạm 27) | Mô hình tĩnh | Giữ nguyên + dịch chuyển theo hành lang mới `z = -3,42` (gắn `EquipmentInfo`) |
| 82 | `ke_tu_do_ca_nhan_locker` | Hành lang (Trạm 27) | Mô hình tĩnh | `LockerCabinetController` (`BioHinge` mở cánh tủ lấy áo blouse/kính bảo hộ, dịch chuyển theo hành lang `z = -3,42`) |
| 83 | `binh_chua_chay_hanh_lang_t2` | Hành lang (Trạm 27) | Mô hình tĩnh | Giữ nguyên + dịch chuyển theo hành lang mới `z = -3,45` (gắn `EquipmentInfo`) |

---

## 4. Nhật ký thực hiện theo từng giai đoạn

### Giai đoạn G0 – Khởi tạo, rà soát & chụp ảnh trước khi sửa (ĐÃ XONG)
- Đọc 100% `PROMPT_DUNG_CU_GIAO_DIEN.md`, `HOP_DONG_GIAO_DIEN_BACKEND.md`, và toàn bộ mã nguồn Tầng 1 + Tầng 2.
- Chạy `KHKT_TEST=truoc_g0` chụp 12 ảnh hiện trạng trạm `20..31` vào `docs/anh-tang2/truoc_dung_cu/` và 5 ảnh hành lang 2,2 m trước khi mở rộng (`01, 03, 05, 07, 09`) vào `docs/anh-tang2/hanh_lang/`.
- Commit cục bộ: `8c4bdc5` (`docs(tang2): G0 ra soat 83 dung cu, phim tat va chup anh hien trang truoc khi sua`).

### Giai đoạn G1 – Mở rộng hành lang Tầng 2 từ 2,2 m lên 3,6 m (ĐÃ XONG)
- Cập nhật `Assets/Scripts/Rendering/ZoneMap.cs`: thêm hằng số `F2CorridorZ = -3.6f`, cập nhật khu `8` (`hanh_lang_t2`: `z ∈ [-3.6, 0]`), khu `9` (`phong_sinh_phan_tu`: `z ∈ [-9.0, -3.6]`), khu `10` (`nha_kinh`: `z ∈ [-9.0, -3.6]`). Giữ nguyên Tầng 1 (`0..4`) và lõi thang (`12`).
- Cập nhật `LabSceneBuilder.Floor2.cs` và `LabSceneBuilder.Floor2.Shell.cs`: dời tường nam hành lang Tầng 2 sang `z = -3.6`, xây kín tường đầu đông đoạn mở rộng `z ∈ [-3.6, -2.2]` tại `x = 27.0`, cập nhật tấm sàn, trần, mái, mái kính hai dốc của nhà kính và đầu dò phản chiếu.
- Cập nhật `LabSceneBuilder.Floor2.SpecialRooms.cs`:
  - Bố trí 2 dãy đèn trần hành lang tại `z = -1.1` và `z = -2.5`, đèn điểm tại `z = -1.8`.
  - Dời các vật dụng sát tường nam hành lang (`Ghế băng chờ 1 & 2`, `Tủ trưng bày tiêu bản`, `Kệ tủ đồ cá nhân`, `Bình chữa cháy`, `Trạm tra cứu dụng cụ`) về `z ≈ -3.32 .. -3.45`, chừa lối đi thông thủy rộng ~2,9 m.
  - Dời toàn bộ nội thất Phòng Sinh học phân tử và Nhà kính vào trong vùng `z ∈ [-8.8, -3.8]`, bổ sung `Vòi rửa mắt khẩn cấp` và `Mô hình xoắn kép ADN` tại Phòng Sinh học phân tử.
- Cập nhật tọa độ trạm `22..30` trong `LabSceneBuilder.cs`, `phong_sinh/bo_cuc_tang2.json` và `docs/20-tang-2-khu-sinh-hoc.md`.
- Kiểm thử tự động `KHKT_TEST=tang2_hanh_lang`: PASS, chụp đủ 5 ảnh sau mở rộng (`02, 04, 06, 08, 10`) trong `docs/anh-tang2/hanh_lang/`.
- Commit cục bộ: `7f3709e` (`feat(tang2): G1 mo rong hanh lang tang 2 tu 2,2 m len 3,6 m va chup 10 anh doi chieu`).

### Giai đoạn G2 – Khung tương tác dùng chung `Assets/Scripts/Interaction/Bio/` (ĐANG BIÊN DỊCH)
- Tạo đầy đủ các lớp nền tảng trong `Assets/Scripts/Interaction/Bio/`:
  - `BioFeedback.cs`: tổng hợp âm thanh toán học (`AudioClip.Create` cho click, nút bấm, thành công, cảnh báo, vỡ lamen, nhịp Korotkoff, bọt khí), hiển thị Toast và ghi nhận lỗi thao tác khoa học (`RecordMistake`).
  - `BioKnob.cs`: núm vặn cơ học 3D xoay bằng con lăn chuột hoặc click, nội suy góc mượt, định dạng dấu phẩy thập phân tiếng Việt.
  - `BioButton.cs`: nút bấm cơ học / công tắc giữ trạng thái với hoạt cảnh lún 4 mm theo `Easing.StdDecel` và đèn báo `MaterialPropertyBlock`.
  - `BioHinge.cs`: nắp bản lề / cửa mở / cửa kính trượt theo `Easing.EmphDecel`, hỗ trợ khóa an toàn (`isLocked`) kèm giải thích khoa học khi cố mở lúc nguy hiểm.
  - `IBioTarget.cs` & `BioTool.cs` & `ProgressRing.cs`: giao diện tác động dụng cụ cầm tay lên mục tiêu (tức thì hoặc giữ chuột kèm thanh tiến trình `ProgressRing`).
  - `CloseUpView.cs`: chế độ góc nhìn cận cảnh khi thao tác tỉ mỉ, thoát bằng `Esc`.
  - `BioSample.cs` & `BioTransfer.cs`: quản lý trạng thái mẫu sinh học và cây quyết định chuyển mẫu 6 trường hợp.
- Cập nhật `AutoDoor.cs` (thêm `interlockWith` cho buồng đệm Airlock) và `HandController.cs` (tích hợp `BioButton`, `BioHinge`, `BioKnob`, `IBioTarget`).

