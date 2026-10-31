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
