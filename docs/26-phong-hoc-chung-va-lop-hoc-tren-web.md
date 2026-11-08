# Phòng học chung (Relay) và điều khiển từ web

## Phòng học chung

Từ màn hình chính, bấm **Phòng học chung**:

- **Mở phòng qua Internet:** nối qua Unity Relay, tạo mã 6 ký tự cho các máy khác nhập.
- **Mở phòng trong mạng LAN:** máy mở phòng hiện địa chỉ IP, máy khác nhập IP đó. Windows có thể hỏi cho phép qua tường lửa, người dùng tự bấm cho phép.
- **Vào phòng:** nhập mã phòng hoặc địa chỉ IP.

Tài khoản học sinh chỉ vào phòng được; giáo viên và chế độ dùng thử mở được phòng.

Mỗi máy tự tải lab. Qua mạng chỉ có tin nhắn (`Network/LabPresence.cs`):

- **Vị trí:** đầu và tay của từng người, 15 lần mỗi giây, hiện thành hình người có tên.
- **Vật đang cầm:** máy mở phòng quyết định ai được cầm. Người khác đang cầm thì vật bị khóa, người đến sau bị từ chối.
- **Người vào muộn:** nhận đúng chỗ các vật đã bị dời.

**Chưa đồng bộ:** dung dịch, phản ứng, chỗ đặt trong giá, máy phân tích, và vật sinh ra lúc chạy.

**Gói đã thêm:** Netcode for GameObjects 2.13.3, Multiplayer Services 2.4.1. Project đã liên kết Unity Cloud (`trong2010`), nên Relay chạy được mà không cần cấu hình thêm.

**Băng thông đo được:** với 2 người và 1 vật đang cầm, máy mở phòng gửi khoảng 0,9 đến 1,3 kB/s và nhận khoảng 0,8 kB/s. Con số này chỉ tính phần dữ liệu, chưa tính phần đầu gói mạng.

**Kiểm thử:** `KhktNetTest`, ba tiến trình .exe thật, qua LAN và qua Relay thật, đạt 31/31.

## Lớp học trên web

`Demo/PresentationClient.cs` gọi `POST /app/nhip` khoảng 2 giây một lần. Chỉ chạy khi đã đăng nhập, không đi qua Relay.

| Chức năng | Cách làm |
|---|---|
| Trình chiếu | Tải ảnh slide kèm mã phiên, hiện lên màn hình có mã khớp (`tuong_video`, `t2_tuong_video`...). Lúc chiếu không tự chuyển trang và không bấm sang trang được. Ảnh cũ bị xóa khỏi bộ nhớ |
| Theo dõi trực tiếp | Gửi tầng, x, z, vật đang cầm, bước chuẩn độ đang làm |
| Thông báo | Hiện lần lượt từng cái; hiện xong mới báo đã nhận |
| Tất cả chú ý | Khóa đi lại và thao tác, hiện lớp phủ. Mất máy chủ quá 15 giây thì tự mở khóa |
| Kiểm tra | Ẩn gợi ý và chú thích, khóa trò chuyện robot (T), hiện đồng hồ đếm ngược theo giờ máy chủ |
| Khóa hóa chất | Không cầm được, robot không lấy được (`Demo/ClassRules.cs`) |
| Lệnh robot | Chạy `LabRobot.ExecutePlan`, kể cả lúc đang Kiểm tra |

**Kiểm thử:** `KhktSlideTest` với `tools/kiem_thu_unity/giao_vien_thu.py`, dùng API thật, đạt 36/36. Độ trễ từ lúc giáo viên bấm tới lúc màn hình trong app hiện xong là 0,3 đến 1,5 giây.
