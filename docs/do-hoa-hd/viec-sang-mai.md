# KẾ HOẠCH & HƯỚNG DẪN KIỂM TRA SÁNG MAI TRÊN IPAD (VERA LAB HD)

Tài liệu này tổng hợp toàn bộ các bước xuất ứng dụng và checklist kiểm thử thực tế trên iPad dành cho buổi sáng mai.
Đêm nay tuân thủ nghiêm ngặt **Mục 0: Chế độ chạy đêm**, tuyệt đối KHÔNG xuất app, KHÔNG chạy Xcode, KHÔNG chạy Simulator hay devicectl.

---

## 1. CÁC BƯỚC XUẤT VÀ CÀI ĐẶT APP LÊN IPAD (SÁNG MAI)

### Bước 1: Kiểm tra cấu hình dự án
- Thư mục dự án: `~/VERALabHD/Phong thi nghiem ao`
- Nhánh làm việc: `do-hoa-hd` (hoặc `giao-dien-hd`)
- Đã cấu hình sẵn trong `Assets/Scripts/Editor/IosBuild.cs`:
  - `DisplayName`: **"VERA Lab HD"** (cài song song, KHÔNG ghi đè lên bản cũ "VERA Lab")
  - `BundleId`: **"com.trantrong.veralab.hd"**
  - Thư mục xuất riêng: `~/AppBuild/VERALabHD`

### Bước 2: Xuất Xcode Project từ Unity
Mở Terminal và thực thi lệnh xuất bản iOS:
```bash
/Users/trantrong/Documents/6000.6.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath "$HOME/VERALabHD/Phong thi nghiem ao" \
  -executeMethod IosBuild.BuildIosProject \
  -logFile "$HOME/VERALabHD/logs/ios_export.log"
```
*(Quá trình xuất mất khoảng 3–5 phút; theo dõi log tại `logs/ios_export.log`)*

### Bước 3: Mở Xcode & Cài đặt lên iPad
1. Cắm cáp iPad vào máy Mac (iPad Air M3).
2. Mở dự án Xcode vừa xuất:
   ```bash
   open "$HOME/AppBuild/VERALabHD/Unity-iPhone.xcodeproj"
   ```
3. Trong Xcode:
   - Chọn Signing & Capabilities -> Chọn Team cá nhân (Apple Development).
   - Đảm bảo Bundle Identifier là `com.trantrong.veralab.hd`.
   - Chọn thiết bị đích: Tên iPad của bạn.
   - Nhấn **Cmd + R** (Product -> Run) để build và cài đặt lên iPad.

---

## 2. DANH SÁCH KIỂM TRA TỰ THỬ TRÊN IPAD (CHECKLIST 20 PHÚT)

Sau khi cài đặt xong, khởi chạy app **VERA Lab HD** trên iPad và thực hiện checklist sau:

### 2.1. Kiểm tra khởi động & Giao diện Touch
- [ ] Icon app hiển thị đúng tên **"VERA Lab HD"** trên màn hình chính iPad (nằm cạnh app "VERA Lab" cũ).
- [ ] Mở app mượt mà, không bị crash trong 5 giây đầu.
- [ ] Joystick ảo và các nút cảm ứng HUD hiển thị rõ ràng, touch target đạt chuẩn Apple (>= 44x44 pt).

### 2.2. Đi dạo & Đánh giá đồ họa tổng thể
1. **Phòng Thí nghiệm Hóa học (Tầng 1):**
   - [ ] Trần thạch cao sáng rõ (độ sáng đo đạt 0.55–0.75), không còn tình trạng trần tối đen u ám.
   - [ ] Vệt nắng mặt trời xiên từ cửa sổ sắc nét, bóng đổ 4 tầng mềm mại dưới chân bàn và ghế.
   - [ ] Dụng cụ trên bàn (ống nghiệm, buret, bình tam giác) có bóng tiếp xúc chân thực (SSAO), không còn bay lơ lửng.
   - [ ] Mặt bàn chống hóa chất Phenolic/Epoxy có độ bóng mờ, viền bo nẹp inox phản xạ ánh sáng.
2. **Nhà kính Thực vật & Khu sinh học:**
   - [ ] Cây xanh có gân lá, độ xuyên sáng (SSS) khi nhìn ngược nắng.
   - [ ] Chậu đất nung terracotta có vân xốp nhẹ, rãnh thoát nước và ống tưới chân thực.
3. **Thang máy & Tầng 2:**
   - [ ] Bước vào cabin thang máy, bấm nút chuyển tầng mượt mà (< 300 ms không giật hình).
   - [ ] Hành lang Tầng 2 sáng tự nhiên, sàn gạch men bóng nhẹ phản chiếu ánh sáng môi trường.

### 2.3. Thử nghiệm thao tác dụng cụ khoa học
- [ ] Cầm ống nghiệm, buret bằng tay chạm: viền miệng thủy tinh sáng bóng, vạch chia milimet sắc nét.
- [ ] Rót dung dịch hoặc thực hiện chuẩn độ: màu dung dịch chỉ thị và mặt khum chất lỏng hiển thị chính xác.
- [ ] Thử nghiệm bọt khí rong đuôi chó trong nhà kính hoạt động bình thường.

### 2.4. Đo đạc hiệu năng thực tế (Mục tiêu Mục 7)
- [ ] Chạm 3 ngón tay lên màn hình (hoặc nhấn phím F8 nếu có bàn phím ngoài) để bật **HD Performance HUD**:
  - **FPS tại mức Ultra:** Duy trì >= 30 FPS ổn định tại phòng nặng nhất (Kho hóa chất / Phòng máy).
  - **1% Low FPS:** Không tụt dưới 24 FPS khi quay góc nhìn nhanh.
  - **RAM (Memory):** Đỉnh RAM dưới 3.5 GB (theo dõi qua Xcode Instruments hoặc HUD).
  - **Nhiệt độ (Thermal):** Chơi liên tục 20 phút, iPad không bị quá nhiệt dẫn đến hạ xung hay văng app.
- [ ] Thử chuyển đổi mức đồ họa giữa **Ultra**, **Cao**, và **Cân bằng** xem độ mượt tăng lên tương ứng.
