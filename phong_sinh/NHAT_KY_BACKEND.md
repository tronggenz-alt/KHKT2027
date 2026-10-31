# NHẬT KÝ VÀ KẾ HOẠCH BACKEND: DỤNG CỤ SINH HỌC TẦNG 2 (VERA LAB)

> Nhánh: `tang2/dung-cu-backend` (tách từ `origin/main` tại commit `aa5e17b8`)
> Thư mục làm việc: `/Users/trantrong/Documents/KHKT_backend`
> Ràng buộc: Làm chậm, kỹ, từng giai đoạn. Mọi thông số có nguồn URL https hoặc `"mo_phong": true`. Tuyệt đối không `git push`, không mở PR, chỉ commit trên máy. An toàn sinh học BSL-1.

---

## 1. Mốc kiểm thử ban đầu (Baseline - Giai đoạn B0)
- **Python Unittest** (`discover -s tests`): **109 / 109 test PASS** (0,41s).
- **C# ChemEngine** (`tests/ChemEngineTests.cs`): **140 / 140 test PASS** (140 đạt, 0 sai).
- **C# BioSimulation** (`tests/BioSimulationTests.cs`): **29 / 29 test PASS** (29 đạt, 0 lỗi).
- Môi trường:
  - macOS Darwin
  - Python 3.11.17 (`/Users/trantrong/Documents/Antigravity/.venv/bin/python`)
  - .NET SDK đi kèm Unity 6000.6.7f1 (`/Users/trantrong/Documents/6000.6.7f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet`)

---

## 2. Kế hoạch chi tiết 11 giai đoạn (B0 -> B10)

| Giai đoạn | Nội dung | Tiêu chí hoàn thành |
|---|---|---|
| **B0** | Khảo sát yêu cầu, lập kế hoạch chi tiết, ghi nhận mốc kiểm thử ban đầu vào nhật ký | `NHAT_KY_BACKEND.md` hoàn thành, commit B0 |
| **B1** | Viết `tests/test_dung_cu_sinh.py` (kiểm tra phân tích bảng markdown mục 2 hợp đồng 108 mã) và tạo `data/sinh/dung_cu.json` đủ 108 dụng cụ với đầy đủ thuộc tính, nguồn tham khảo | `test_dung_cu_sinh.py` PASS 100% cho mục dụng cụ |
| **B2** | Tạo các file dữ liệu sinh học còn lại: `tieu_ban.json`, `chung_vi_sinh.json`, `moi_truong.json`, `bai_thi_nghiem.json`, `sinh_pham_mac_dinh.json`; thêm 2 hồ sơ máy `gel_doc.json`, `may_do_od600.json` và bổ sung thông số cho 6 máy hiện có | Kiểm thử dữ liệu trong `test_dung_cu_sinh.py` và `test_du_lieu_may.py` PASS 100% |
| **B3** | Cập nhật `ChemEngine`: bổ sung chất xanh methylen, axit lactic, cồn 70%... và phản ứng catalase phân hủy H₂O₂ xúc tác sinh học | `ChemEngineTests.cs` đạt hết 140 bài cũ + bài test catalase mới |
| **B4** | C# Simulation đợt 1: `BioSampleData`, `Dilution`, `StreakPlate`, `ContaminationRules`, `GramStain` | Viết bài test mới trong `BioSimulationTests.cs`, biên dịch và chạy bằng `dotnet` đạt 100% |
| **B5** | C# Simulation đợt 2: `PipetteAccuracy`, `Centrifugation`, mở rộng `Pcr`, `Electrophoresis` | Viết bài test mới trong `BioSimulationTests.cs`, đạt 100% |
| **B6** | C# Simulation đợt 3: `Transpiration`, `Phototropism`, `DnaExtraction`, `Catalase`, mở rộng `Optics.Brightness`, `Fermentation` | Viết bài test mới trong `BioSimulationTests.cs`, toàn bộ test cũ và mới đạt 100% |
| **B7** | Xây dựng CSDL SQLite `server/lab/sinh_db.py`: 8 bảng mới, nạp mặc định từ JSON, kết nối an toàn luồng | Viết test kiểm tra CSDL và nạp mặc định đạt 100% |
| **B8** | Xây dựng API FastAPI `server/lab/sinh_api.py`, gắn router vào `server/ai/main.py`, phục vụ `/sinh`, xử lý batch `/su-kien`, xuất CSV UTF-8 BOM, viết `tests/test_sinh_web.py` | `test_sinh_web.py` đạt 100%, bảo đảm không lỗi 500, không lộ bí mật |
| **B9** | Tích hợp AI trong `server/ai/machines.py` và `server/ai/main.py`: từ khóa ALIASES mới, đưa SOP dụng cụ vào ngữ cảnh, kiểm tra an toàn BSL-1 từ chối tác nhân nguy hiểm | `test_ai_server.py` đạt hết, có bài kiểm tra từ khóa và ngữ cảnh |
| **B10** | Biên soạn tài liệu Backend `docs/21-dung-cu-sinh-hoc.md`, lập báo cáo bàn giao `phong_sinh/BAO_CAO_BAN_GIAO_BACKEND.md` | Hoàn thành, dừng chờ chủ dự án và Claude duyệt |

---

## 3. Nhật ký thực hiện từng giai đoạn

### Giai đoạn B0 (2026-10-31)
- Đã đọc kỹ `PROMPT_DUNG_CU_BACKEND.md`, `HOP_DONG_GIAO_DIEN_BACKEND.md`, `contracts.md`, `PROMPT_DUNG_CU_GIAO_DIEN.md`.
- Đã khảo sát hiện trạng mã nguồn: các lớp `Simulation/Bio`, `Simulation/Chem`, `server/lab`, `server/ai`.
- Đã chạy kiểm thử baseline: 109 test Python, 140 test Chem C#, 29 test Bio C# đều đạt 100%.
- Đã khởi tạo nhật ký và kế hoạch tổng thể.

### Giai đoạn B1 (2026-10-31)
- Đã viết bộ kiểm thử `tests/test_dung_cu_sinh.py` (chạy TDD đỏ trước khi có file dữ liệu).
- Đã tạo 2 hồ sơ máy phân tích mới `data/may/gel_doc.json` và `data/may/may_do_od600.json`, bổ sung các thông số còn thiếu cho 6 máy hiện có (`kinh_hien_vi`, `tu_an_toan_sinh_hoc`, `tu_am_vi_sinh`, `noi_hap`, `may_pcr`, `be_dien_di`) mà không xóa khóa cũ.
- Đồng bộ dữ liệu máy sang Unity (`Phong thi nghiem ao/Assets/Lab/Data/May/`) qua `tools/chep_du_lieu_may.py`. Bộ kiểm thử `tests/test_du_lieu_may.py` đạt 8/8 test PASS.
- Đã xây dựng `data/sinh/dung_cu.json` với đúng 108 bản ghi khớp 100% mục 2 hợp đồng, đầy đủ trường, thông số có nguồn URL https thật hoặc `"mo_phong": true`.

### Giai đoạn B2 (2026-10-31)
- Đã tạo generator `tools/tao_du_lieu_sinh.py` và sinh 5 file dữ liệu sinh học trong `data/sinh/`:
  - `tieu_ban.json`: 20 tiêu bản (12 tự làm, 8 cố định; đầy đủ 6 tiêu bản từ app cũ).
  - `chung_vi_sinh.json`: 5 chủng BSL-1 giảng dạy + 2 tạp nhiễm BSL-1, đầy đủ thông số khuẩn lạc, hình thái tế bào, hệ số nhiễm (`he_so_nhiem`).
  - `moi_truong.json`: 6 môi trường (PCA, NA, LB agar, Sabouraud, LB lỏng, nước muối sinh lý) với thành phần, điều kiện hấp vô trùng và hạn dùng.
  - `sinh_pham_mac_dinh.json`: 35 mặt hàng sinh phẩm khởi tạo (mẫu tươi, hóa chất, enzyme, kit PCR, ladder, thuốc nhuộm; có mẫu cận date `sp_la_thai_lai` 3 ngày và hết hạn `sp_kit_ly_giai_het_han` để test cảnh báo).
  - `bai_thi_nghiem.json`: 15 bài thí nghiệm chuẩn SGK (10, 11, 12), đầy đủ mục tiêu, mã dụng cụ, sinh phẩm, các bước chi tiết với điều kiện máy đọc được, câu hỏi và đáp án.
- Bộ kiểm thử `tests/test_dung_cu_sinh.py` đạt 8/8 test PASS (không còn test nào bị skip).
- Toàn bộ kiểm thử Python `unittest discover -s tests` đạt 117/117 test PASS.

### Giai đoạn B3 (2026-10-31)
- Cập nhật `Species.cs`: bổ sung các chất sinh học tầng 2 (`xanh_methylen`, `tim_ket_tinh`, `safranin`, `catalase`, `catalase_bat_hoat`) và hệ thống bí danh (`axit_lactic` → `Lac`, `con_70` / `con_96` / `etanol` → `C2H5OH`, `mb` → `xanh_methylen`).
- Cập nhật `ChemKinetics.cs`: bổ sung hàm `CatalaseActivity(tempC, boiled)` và tích hợp xúc tác sinh học catalase phân hủy H₂O₂ giải phóng O₂ (tối ưu ở 37 °C, giảm mạnh ở 4 °C, biến tính mất hoạt tính khi đun >= 65 °C).
- Cập nhật `data/reactions/phan_ung.json`: thêm phản ứng `h2o2_catalase`.
- Cập nhật `tests/ChemEngineTests.cs`: đạt **147 / 147 test PASS** (140 bài cũ + 7 bài kiểm thử mới).

### Giai đoạn B4 (2026-10-31)
- Đã triển khai 5 lớp C# thuần (không phụ thuộc `UnityEngine`, không cấp phát rác mỗi khung hình, tất định theo `seed`) kèm file `.meta` trong `Phong thi nghiem ao/Assets/Scripts/Simulation/Bio/`:
  - `BioSampleData.cs`: cấu trúc `DnaBand`, `Contaminant`, lớp `BioSampleData` với `CopyFrom` và `MixInto` (chia mật độ theo thể tích, chuyển chủng ít tế bào hơn vào `contaminants`, gộp băng ADN cùng `bp`).
  - `Dilution.cs`: `CfuPerMl`, `IsCountable` (30..300 theo ISO 4833-1), `CarryOverMl`, `SampleColonies` (Poisson tất định).
  - `StreakPlate.cs`: `StreakStroke`, `ColonySpot`, `Colonies` (mô phỏng cấy ria 4 vùng tách khuẩn lạc đơn, giảm mật độ khi đốt que cấy, phụ thuộc nhiệt độ và thời gian ủ).
  - `ContaminationRules.cs`: `AsepticContext`, `Probability`, `Roll` (khớp bảng hệ số `he_so_nhiem` trong `chung_vi_sinh.json`).
  - `GramStain.cs`: `GramResult`, `Result` (đánh giá quy trình nhuộm Gram 4 bước theo chuẩn ASM).
- Cập nhật `tests/BioSimulationTests.cs`: đạt **49 / 49 test PASS** (29 bài cũ + 20 bài kiểm thử mới).

### Giai đoạn B5 (2026-10-31)
- Đã triển khai 2 lớp C# mới kèm `.meta` và mở rộng 2 lớp hiện có trong `Phong thi nghiem ao/Assets/Scripts/Simulation/Bio/`:
  - `PipetteAccuracy.cs`: `ActualUl` (sai số ISO 8655-2, lỗi bấm nấc 2 khi hút dư 12%, lỗi không ngập đầu côn, lỗi nghiêng > 20°), `ResidualFraction` (còn sót ~3,5% ở nấc 1, ≈ 0 ở nấc 2).
  - `Centrifugation.cs`: `Rcf`, `Rpm` ($RCF = 1,118 \times 10^{-5} \cdot r \cdot rpm^2$), `IsBalanced` kiểm tra vị trí đối xứng và chênh khối lượng, trả `reasonVi` tiếng Việt rõ ràng ("Ống ở lỗ 3 chưa có ống đối diện ở lỗ 9.").
  - Mở rộng `Pcr.cs`: thêm `PcrSetup` và `Pcr.Run` (kiểm tra đủ thành phần, ảnh hưởng của $T_a$ so với $T_m$, sinh băng phụ khi $T_a < T_m - 10$, mờ/mất băng khi $T_a > T_m$, nắp nhiệt, thời gian để ấm enzyme, nhiễm chéo sản phẩm ở đối chứng âm).
  - Mở rộng `Electrophoresis.cs`: thêm `BandPositionCm` (hỗ trợ cả cắm ngược cực trả về vị trí âm) và `LeftGel`.
- Cập nhật `tests/BioSimulationTests.cs`: đạt **66 / 66 test PASS**.




- Kết quả kiểm thử: `tests/test_dung_cu_sinh.py` đạt 3/3 test dụng cụ, toàn bộ test suite Python đạt 117/117 test (0,60s).

