# VERA Lab: tiến độ và hiện trạng toàn dự án

Cập nhật: 09/11/2026. Đây là bản tóm tắt để ai mở dự án (người hay AI) cũng nắm được ngay. Chi tiết nằm trong `docs/` và `contracts.md`.

## 1. Dự án

- **Tên:** VERA Lab (Virtual Experiment & Reality-based AI Lab). Hệ thống trải nghiệm phòng thí nghiệm thực tế ảo Hóa – Sinh tích hợp AI.
- **Nhóm:** Nguyễn Ngọc Bảo Trọng, Trần Đức Trọng (THPT Chuyên Lê Quý Đôn). Dự thi KHKT. Lộ trình: Hóa → Sinh → Vật lý, chạy trên Windows và Mac.
- **Kho:** `github.com/tronggenz-alt/KHKT2027`, đang **công khai**. Nhánh chính `main`, nhánh làm việc `toi-uu/lab-sinh-tu-do`.
- **Unity:** 6000.6.7f1, URP. Cảnh dựng bằng code qua menu **KHKT > Dựng phòng thí nghiệm**. Sửa code dựng cảnh xong phải dựng lại cảnh.

## 2. Đã xong

### Phòng thí nghiệm (Unity)
- **Lab Hóa tầng 1:** khoảng 720 hóa chất, bộ máy phản ứng (pH, kết tủa, khí, oxi hóa – khử, màu), chuẩn độ, robot AI làm mẫu và xử lý sự cố.
- **Máy phân tích:** 14 máy (UV-Vis, AAS, XRD, NMR...) chạy theo SOP thật. Xem docs/17–19.
- **Khu chế tạo dụng cụ:** lắp ráp tự do bằng khớp nối, gia công ra bàn.
- **Lab Sinh tầng 2:** thao tác tự do, sinh phẩm là vật thật, kết quả tính theo thành phần. Xem docs/23.
- **Tối ưu đồ họa:** giảm 28–35% thời gian mỗi khung hình, nạp và gỡ cảnh theo khu.
- **Màn hình chính** (cảnh `ManHinhChinh`): đăng nhập, tạo tài khoản bằng mã lớp, dùng thử, cài đặt máy chủ (mặc định `https://verlab.pages.dev`), một nút "Vào phòng thí nghiệm". Xem docs/25-man-hinh-chinh.
- **Phòng học chung:** Unity Relay (mã 6 ký tự) hoặc LAN. Thấy nhau, cầm chung một vật có phân xử. Chưa đồng bộ hóa chất. Xem docs/26.
- **Lớp học từ web** (qua `POST /app/nhip`): trình chiếu lên tường video, theo dõi trực tiếp, thông báo, "Tất cả chú ý", chế độ Kiểm tra, khóa hóa chất, lệnh robot.

### Điều khiển bằng cơ thể
- **Nhận diện tay ngay trong Unity** (`Input/InAppHandTracker.cs`):
  - mô hình MediaPipe chạy bằng Inference Engine, nằm ở `Assets/Lab/MoHinh/Resources`;
  - tự bật khi vào lab, F10 bật/tắt;
  - hai tay hiện sẵn (`Demo/ArmRig.cs`), có lọc 1€ và vùng chết mềm;
  - trỏ vào vật rồi chụm ngón để cầm.
- **Tracker Python** (`tools/mediapipe_tracker.py`): mặt, đầu, mắt (F8), Pose. Bảng F9 mô phỏng tracking. Đã sửa dấu yaw. **Có thể bị Smart App Control chặn**, xem mục 4.
- **Tay cầm ESP32 + BMI160** (SDA 21, SCL 22):
  - firmware `firmware/tay_cam_bmi160` chống trôi khi để yên;
  - xem 3D bằng `python tools/xem_bmi160.py COM8`.

### Web và máy chủ
- **Landing page** `trang_gioi_thieu/`, bản tab sáng, trên verlab.pages.dev. Đăng lại bằng wrangler từ `D:\KHKT_kiem_thu\cf_verlab`, máy này đang đăng nhập Cloudflare bằng tài khoản của Đức Trọng.
- **Web quản lý giáo viên, học sinh:**
  - chạy cố định trên `https://verlab.pages.dev` (Cloudflare Pages Functions + D1, mã ở `cloudflare/`);
  - bản FastAPI cục bộ `server/` vẫn giữ (cổng 8000).
  - Xem docs/25-web-quan-ly-cloudflare.
- **Giao diện web "Phòng điều hành"** (tối, màu nhấn #38d6c4): làm theo 6 giai đoạn, xong giai đoạn 1.

### Tay cầm VR (phần cứng)
- **VERA Arc** (vòng qua bàn tay, pin 9V): có gói in STL. Xem docs/24-tay-cam-vera-arc.
- **VERA Band** (quai đeo trên bàn tay, không cần cầm): đã duyệt dáng, đang làm gói in.
- VERA Drop, V2 Halo là bản cũ.

## 3. Đang dở và ý tưởng chờ duyệt

| Việc | Trạng thái |
|---|---|
| Pose trong Unity (khuỷu, vai thật) | Đã tải mô hình `pose_*.onnx`, chưa nối. Hiện khuỷu suy ra bằng IK |
| Đo nhận diện tay bằng webcam thật | Chưa đo. Game ghi `[TayUnity] ... khung/giây` vào Player.log mỗi 5 giây |
| Phòng học chung đồng bộ dung dịch, phản ứng | Chưa làm. Máy mở phòng sẽ tính, máy khác chỉ nhận |
| Phủ phản ứng hóa học (148/242 phản ứng chuẩn) | Kế hoạch 6 giai đoạn chờ duyệt |
| Phòng robot, bàn lập trình khối lệnh | Tạm dừng |
| Giao diện web giai đoạn 2–6 | Đang làm ở phiên thiết kế web |
| Gói in VERA Band | Đang làm |
| Bản macOS | Tạm dừng |

## 4. Lưu ý quan trọng

- **Smart App Control (Windows 11)** chặn `libmediapipe.dll` của MediaPipe Python. Vì vậy nhận diện tay đã chuyển vào Unity. F8 (đầu) vẫn dùng Python nên có thể không chạy trên máy bật Smart App Control.
- **Mật khẩu tài khoản thử** nằm ở `data/tai_khoan_mau_mat_khau.json` (git bỏ qua). Không chép mật khẩu vào tệp được commit, vì kho đang công khai.
- **Nhiều phiên AI dùng chung một thư mục:** chỉ stage tệp của mình theo tên. Cấm `git add -A` và force push.
- **Kiểm thử Unity** chạy trong bản sao `D:\KHKT_kiem_thu` (`chay.ps1 -Mode <tên>`). Các bài: `man_hinh_chinh`, `canh_tay`, `trinh_chieu`, `sinh_tu_do`, `tracking_f9`... Mã bài thử ở `tools/kiem_thu_unity`. Trước khi dựng project thật, kiểm tra `Temp/UnityLockfile`.
- **Kiểm thử Python:** `python -m unittest discover -s tests`.

## 5. Chạy thử

- **Bản .exe:** `D:\KHKT_kiem_thu\ban_do\vera\PhongThiNghiemAo.exe`, hoặc gói mang đi `D:\KHKT_kiem_thu\ban_do\VERA_Lab.zip`. Bản dựng không có trong git.
- **Phím:**

| Phím | Tác dụng |
|---|---|
| F8 | Điều khiển bằng đầu |
| F9 | Mô phỏng tracking |
| F10 | Nhận diện tay |
| F11 | Bảng trạng thái MediaPipe |
| F12 | Căn lại tư thế đầu |
| Esc | Tạm dừng, về màn hình chính |
| B | Tra cứu hóa chất |
| T | Trợ lý robot |
| H | Trợ giúp |
