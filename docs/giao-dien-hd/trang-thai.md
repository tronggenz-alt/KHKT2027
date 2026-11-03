# TRẠNG THÁI TIẾN ĐỘ: NÂNG CẤP GIAO DIỆN IPAD HIỆN ĐẠI (VERA LAB HD)

- **Thời gian bắt đầu:** 2026-11-03 00:39
- **Nhánh làm việc:** `giao-dien-hd` (khởi tạo từ mốc commit hoàn tất đồ họa `ebf7d32b`)
- **Mục tiêu:** Toàn bộ giao diện iPad đạt chuẩn Apple HIG (nút >= 44x44 pt, safe area), phong cách kính mờ hiện đại, hiệu ứng chuyển động mượt mà, phản hồi âm thanh/hình ảnh rõ ràng, không phụ thuộc phím cứng PC.
- **Cam kết cứng (Mục 0):**
  - Tuyệt đối KHÔNG xuất app, KHÔNG chạy Xcode, KHÔNG chạy simulator hay cài đặt lên iPad đêm nay.
  - Tuyệt đối KHÔNG sửa backend, mô phỏng hóa học/sinh học, server, firmware, contracts.md.
  - Mọi thao tác kiểm thử giao diện thực hiện trong Unity Editor với `KHKT_CAM_UNG=1` ở 2360 × 1640 và 2732 × 2048.

---

## 1. TIẾN ĐỘ CÁC GIAI ĐOẠN

- [/] **Giai đoạn 0: Khóa hiện trạng (Kiểm kê nút `nut_goc.json`, ảnh gốc, commit nền)** [Đang tiến hành]
- [ ] **Giai đoạn 1: Tham khảo (`tham-khao.md`) và kiểm toán giao diện (`kiem-toan.md`)**
- [ ] **Giai đoạn 2: Hệ thiết kế `GlassKit`, nhúng phông `Be Vietnam Pro`, icon, kính mờ URP**
- [ ] **Giai đoạn 3: Bộ hiệu ứng nút `NutHieuUng`, `UiMotion`, phản hồi âm thanh `BioFeedback`**
- [ ] **Giai đoạn 4: HUD cảm ứng iPad (Touch target >= 44 pt, Safe Area, ngón cái, loại bỏ phím PC)**
- [ ] **Giai đoạn 5: Màn hình đầu, menu, bảng cài đặt (âm thanh, giảm chuyển động, giảm trong suốt)**
- [ ] **Giai đoạn 6: Bảng trong phòng lab (`ModernUI`, tra cứu hóa chất, robot AI, kiosk dụng cụ)**
- [ ] **Giai đoạn 7: Màn hình 14 máy phân tích và giao diện Tầng 2 (kính hiển vi, ghép gen)**
- [ ] **Giai đoạn 8: Hoàn thiện, chống tràn chữ, tối ưu canvas, đo FPS và GC**
- [ ] **Giai đoạn 9: Bàn giao (`viec-sang-mai.md` và `bao-cao-sang.md`)**
