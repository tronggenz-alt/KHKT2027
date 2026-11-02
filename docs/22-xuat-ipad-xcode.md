# Hướng dẫn Xuất Xcode và Chạy VERA Lab trên iPad

Tài liệu hướng dẫn triển khai ứng dụng phòng thí nghiệm ảo (VERA Lab) lên iPad qua Xcode.

## 1. Yêu cầu môi trường

- **Máy Mac** đã cài đặt:
  - Unity **6000.6.7f1** kèm mô-đun **iOS Build Support**.
  - Xcode 15 hoặc 16 mới nhất kèm iOS SDK.
  - Apple Developer Account (miễn phí hoặc trả phí) đã đăng nhập trong Xcode (`Settings -> Accounts`).
- **Thiết bị:** iPad chạy iPadOS 16.0 trở lên.
- Cáp kết nối USB-C / Lightning từ iPad sang Mac (hoặc kết nối qua Wi-Fi sau khi bật Connect via Network).

## 2. Xuất dự án Xcode từ Unity

Unity không đọc được dự án nằm dưới thư mục ẩn (như `.claude/worktrees`): hãy dựng từ thư mục thường, ví dụ bản sao ở `~/AppBuild/VERALab`. Không dùng cờ `-nographics` khi dựng cảnh, vì bước nướng phản chiếu cần GPU.

Script `Assets/Scripts/Editor/IosBuild.cs` đặt sẵn Bundle Identifier `com.trantrong.veralab`, nhóm ký, hướng ngang và tên hiển thị VERA Lab, rồi xuất toàn bộ cảnh đang bật ra `Builds/iOS`.

### Cách 1: Qua Menu Unity Editor
Mở dự án trong Unity Editor 6000.6.7f1, chuyển nền tảng sang iOS, rồi chọn **KHKT → Xuất iPad (Xcode)**.

### Cách 2: Qua Dòng lệnh (Batchmode)
```bash
/Users/trantrong/Documents/6000.6.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "<thư mục dự án>" -buildTarget iOS \
  -executeMethod Khkt.EditorTools.IosBuild.Device \
  -logFile logs/unity_ios_device.log
```

## 3. Mở và Chạy trên Xcode

1. Mở thư mục Xcode vừa xuất:
   ```bash
   open "Phong thi nghiem ao/Builds/iOS/Unity-iPhone.xcodeproj"
   ```
2. Trong thanh điều hướng bên trái Xcode:
   - Chọn mục **Unity-iPhone** (dự án gốc).
   - Vào thẻ **Signing & Capabilities**.
   - Tại mục **Signing**:
     - Tích chọn **Automatically manage signing**.
     - Chọn **Team** của bạn (Personal Team của Apple ID cá nhân là đủ).
     - Bundle Identifier sẽ tự động điền: `com.trantrong.veralab` (nếu trùng có thể đổi hậu tố ngắn).
3. Kết nối iPad vào máy Mac:
   - Trên thanh công cụ trên cùng của Xcode, chọn thiết bị đích là iPad của bạn (thay vì "Any iOS Device").
   - Nếu là lần đầu cắm iPad: bấm "Tin cậy máy tính này" (Trust This Computer) trên iPad và nhập passcode.
   - Bật **Developer Mode** trên iPad (Cài đặt -> Quyền riêng tư & Bảo mật -> Chế độ nhà phát triển -> Bật và khởi động lại iPad).
4. Nhấn nút **Play / Run** (hoặc phím tắt `Cmd + R`) để biên dịch và cài đặt lên iPad.

## 4. Cấu hình Máy chủ AI trên iPad (Nếu có)

- Ứng dụng hoạt động hoàn toàn độc lập ngoại tuyến.
- Nếu muốn dùng tính năng trợ lý AI / nhận diện hiện tượng:
  1. Khởi động máy chủ AI trên Mac: `python -m server.ai.main` (chạy trên cổng 8000).
  2. Đảm bảo iPad và Mac cùng chung mạng Wi-Fi.
  3. Mở menu trong app VERA Lab trên iPad (nút Menu góc trái hoặc phím Esc) -> chọn **Cài đặt**.
  4. Nhập địa chỉ IP của máy Mac (ví dụ: `http://192.168.1.15:8000`) và bấm **Lưu cài đặt**.
  5. Nếu không có máy chủ, ứng dụng thông báo nhẹ nhàng và toàn bộ các thí nghiệm Hóa học & Sinh học vẫn hoạt động bình thường.
