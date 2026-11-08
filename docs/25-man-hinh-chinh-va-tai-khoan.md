# Màn hình chính và tài khoản trong app

Mở app không còn vào thẳng phòng lab. Cảnh đầu tiên là `Assets/Scenes/ManHinhChinh.unity`, nhẹ, chỉ có camera và `MainMenu`. Bấm "Vào phòng thí nghiệm" mới tải lab, kèm thanh tiến độ. Không có bước chọn chế độ: vào lab là dùng được mọi tính năng.

Giao diện theo trang giới thiệu: nền sáng, màu nhấn `#0b63d1`, bo góc nhỏ, logo lục giác chữ V.

## Các màn hình

| Màn hình | Việc làm được |
|---|---|
| Đăng nhập | Đăng nhập, sang Tạo tài khoản, Dùng thử không cần tài khoản |
| Tạo tài khoản | Đăng ký bằng mã lớp do giáo viên cấp |
| Đổi mật khẩu | Bắt buộc khi tài khoản có `phai_doi_mat_khau` |
| Cài đặt | Địa chỉ máy chủ, kiểm tra kết nối |
| Trang chính | Vào phòng thí nghiệm, Đăng xuất |

Phím Esc trong lab mở bảng tạm dừng: Tiếp tục, Về màn hình chính.

## Mã nguồn

| Tệp | Vai trò |
|---|---|
| `Scripts/Menu/MainMenu.cs` | Giao diện, dựng lúc chạy |
| `Scripts/Menu/AccountClient.cs` | Gọi `/auth/*`, lưu mã phiên, xử lý hết phiên |
| `Scripts/Menu/GameSession.cs` | Người dùng hiện tại, địa chỉ máy chủ, về màn hình chính |
| `Editor/LabBuilder/LabSceneBuilder.Menu.cs` | Dựng cảnh, menu **KHKT > Dựng màn hình chính** |

`AiClient` và `BioLabSync` gửi kèm mã phiên và dùng địa chỉ trong Cài đặt. `BioLabSync` vẫn ưu tiên biến môi trường `KHKT_SERVER_URL`.

## Hành vi cần biết

- **Mật khẩu:** không lưu trên máy.
- **Mã phiên:** lưu ở `phien_dang_nhap.txt` trong `Application.persistentDataPath`, chưa mã hóa. Hết hạn sau 14 ngày không dùng.
- **Máy chủ trả 401 khi đang trong lab:** app bỏ mã phiên và chơi tiếp ở chế độ dùng thử.
- **Dùng thử:** không cần mạng. Các lời gọi AI, nhật ký chỉ chạy khi máy chủ ở `127.0.0.1` hoặc `localhost` trên chính máy đó.
- **Bài chuẩn độ tự chọn có chấm điểm:** đã bỏ lối vào cùng các chế độ. `LabProcedure` vẫn còn, robot vẫn làm mẫu được, bộ chuẩn độ vẫn nằm trên bàn.

## Kiểm thử

Chạy trong bản sao ở ổ D với `KHKT_TEST=man_hinh_chinh` (`tools/kiem_thu_unity/KhktMenuTest.cs`):

- Mặc định dùng máy chủ giả đúng hợp đồng API, đi hết luồng, vào lab ba lần liên tiếp, đếm điểm ảnh để chắc giao diện thật sự vẽ ra.
- Có thêm `KHKT_MENU_TK=<tệp>` (dòng 1 tên, dòng 2 mật khẩu) thì thử với máy chủ thật ở cổng 8000. Mỗi lần chạy tạo một tài khoản `thu.unity.*` trong CSDL.
- Lệnh `KhktPlayVerify.BuildMenuTest` dựng bản .exe chạy cùng bài thử này.

Phím bấm và nhập chữ bằng tay chưa được tự động kiểm tra: bài thử gọi trực tiếp vào nút và ô nhập.
