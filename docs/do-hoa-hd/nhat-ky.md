# NHẬT KÝ NÂNG CẤP ĐỒ HỌA PBR SIÊU THỰC - VERA LAB HD

Nhánh: `do-hoa-hd` | Máy trạm: Mac M2 8 GB | Thiết bị mục tiêu: iPad Air M3 (UDID: `00008122-001669801186801C`)

---

## Giai đoạn 0: Chuẩn bị và khóa tọa độ

### 1. Đã đổi gì
- Tạo nhánh làm việc độc lập `do-hoa-hd` trên worktree `~/VERALabHD` tách biệt hoàn toàn khỏi `main`.
- Dựng cảnh sạch gốc (20.222 GameObject, 12 reflection probes, 22 cửa sổ ảo, NavGrid hash `09aa0fd26f0923f663435ad5e45a2745`).
- Phát triển bộ công cụ khóa & đối chiếu tọa độ `HdCoordinateTool.cs` tại `Assets/Scripts/Editor/HdTools/HdCoordinateTool.cs`.
- Trích xuất toàn bộ mốc gốc vào `docs/do-hoa-hd/toa_do_goc.json` (đường dẫn phân cấp kèm sibling index, vị trí thế giới, góc xoay, tỉ lệ, loại & bounds collider, các thành phần tương tác: `Grabbable`, `LabContainer`, `LabStation`, `EquipmentInfo`, `LabScreen`, `AutoDoor`, mã băm lưới đi lại NavGrid).
- Chụp ảnh mốc gốc:
  - 12 ảnh góc máy cố định landing (`docs/do-hoa-hd/anh_goc/landing/`).
  - 27 ảnh góc nhìn chuẩn (`docs/do-hoa-hd/anh_goc/goc_nhin/`).
- Chạy toàn diện bộ kiểm thử gốc: `landing`, `ipad` (5/6 tiêu chuẩn), `tang2` (12 trạm + 2 theme), `khu` (nạp/gỡ khu, giữ trạng thái hóa chất, robot lấy đồ).
- Cấu hình bản build iOS song song:
  - `Bundle Identifier = "com.trantrong.veralab.hd"`
  - `Display Name = "VERA Lab HD"`
  - Đường dẫn xuất: `Builds/iOS_HD`
- Biên dịch thành công Xcode và cài đặt app `com.trantrong.veralab.hd` lên iPad Air M3 bằng `devicectl`. Xác minh 2 app cùng tồn tại song song độc lập.

### 2. Số đo
- **Đối chiếu tọa độ (CompareBatch):** 18.964/18.964 GameObject khớp hoàn hảo. Sai lệch vị trí = 0.000 mm, sai lệch góc = 0.000°, sai lệch tỉ lệ = 0.000%. Tổng số lỗi tọa độ = 0 LỖI (ĐẠT).
- **Tiêu chuẩn kiểm thử gốc trên iPad (KHKT_TEST=ipad):** Đạt 5/6 tiêu chuẩn (Tiêu chuẩn 3 không đạt do 13 que thử lệch pivot đáy 10 mm theo thiết kế cũ - giữ nguyên không can thiệp).
  - FPS Editor mốc gốc: Trạm 21 = 52.9 FPS, Trạm 23 = 75.9 FPS, Trạm 25 = 24.7 FPS, Trạm 26 = 27.2 FPS, Trạm 28 = 81.5 FPS, Trạm 30 = 9.7 FPS.
- **Tiêu chuẩn Tầng 2 (KHKT_TEST=tang2):** Đạt 12/12 trạm và 2/2 chủ đề (Gỗ ấm và Tối giản).
- **Tiêu chuẩn Khu (KHKT_TEST=khu):** Đạt nạp/gỡ khu, cửa mở đúng, hóa chất giữ trạng thái.
- **Xác minh cài đặt iPad Air M3:**
  - `com.trantrong.veralab`: Đang cài đặt (bản gốc).
  - `com.trantrong.veralab.hd`: Đã cài đặt thành công (bản HD).

### 3. Lỗi còn lại
- Không có lỗi chặn nào. Cả hai ứng dụng tồn tại song song, không xung đột chứng chỉ, không đè dữ liệu.

### 4. Việc tiếp theo
- Thực hiện **Giai đoạn 1**: Nghiên cứu tài liệu tham khảo mạng, tiêu chuẩn chiếu sáng lab, vật liệu kiến trúc thật, lập `docs/do-hoa-hd/art-bible.md` và `docs/do-hoa-hd/nguon-tai-nguyen.md` (100% tài sản CC0 có kiểm tra nguồn).
