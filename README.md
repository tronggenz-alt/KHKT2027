# Phòng thí nghiệm ảo nhiều người (KHKT)

Khung dự án cho phòng lab hóa học ảo: kính VR Box + điện thoại, tay cầm tự chế (Arduino Mega + MPU6050), nhiều người cùng làm, đo độ khéo tay, AI theo ngữ cảnh. Bản thiết kế gốc: `Bản thiết kế dự án KHKT Phòng thí nghiệm ảo nhiều người.md`.

## Đọc gì trước

| Tài liệu | Nội dung |
| --- | --- |
| [docs/01-de-xuat-dieu-khien.md](docs/01-de-xuat-dieu-khien.md) | **Nghiên cứu và đề xuất hệ điều khiển** (đọc đầu tiên) |
| [docs/02-giao-thuc-tay-cam.md](docs/02-giao-thuc-tay-cam.md) | Gói tin nhị phân Mega → PC, lệnh PC → Mega |
| [docs/03-kien-truc.md](docs/03-kien-truc.md) | Kiến trúc phần mềm, các module, quyết định thiết kế |
| [docs/04-kiem-thu-tuan-1.md](docs/04-kiem-thu-tuan-1.md) | Danh sách đo kiểm tuần 1 (độ trễ, trôi góc, say) |
| [docs/05-unity.md](docs/05-unity.md) | Cảnh phòng thí nghiệm (mặt bằng, phím xem thử), cài đặt Unity, nối tay cầm với XR Interaction Toolkit |
| [docs/06-phong-lab-moi-hoa-chat-ai.md](docs/06-phong-lab-moi-hoa-chat-ai.md) | Lab 300 m², 720 hóa chất xếp kệ và tra cứu, bộ máy hóa học, chuẩn độ tự chọn, AI đám mây và robot trợ lý |
| [docs/07-web-quan-ly-robot-phong-moi.md](docs/07-web-quan-ly-robot-phong-moi.md) | Web quản lý (kho, thiết bị, nhật ký), mạng lưới khóa Groq/DeepSeek/Gemini, robot hình người theo kế hoạch AI, phòng máy phân tích và phòng robot |
| [docs/08-tai-lieu-tham-khao-thiet-bi.md](docs/08-tai-lieu-tham-khao-thiet-bi.md) | Tài liệu, datasheet, tiêu chuẩn (ưu tiên tiếng Việt) về nguyên lý và cách dùng các máy phân tích, thiết bị phòng lab |
| [docs/09-tay-cam-esp32-cam.md](docs/09-tay-cam-esp32-cam.md) | **Tay cầm joystick → Arduino Mega → ESP32-CAM → Wi-Fi**: bảng đấu nối, mở tường lửa, đủ cử động nhân vật bằng 1 cần + 1 nút (chế độ ĐI / TAY) |
| [docs/10-lab-quoc-te-tai-khu-toi-uu.md](docs/10-lab-quoc-te-tai-khu-toi-uu.md) | **Phong cách lab trường quốc tế, cửa tự động, tải / gỡ từng khu khi đi qua cửa mà vẫn giữ hiện trạng**, lối vào phòng hóa chất kính mờ; giai đoạn B: occlusion culling + cổng, HLOD, impostor, cửa sổ ảo, kèm số đo FPS từng kỹ thuật |
| [docs/11-lap-rap-tu-do.md](docs/11-lap-rap-tu-do.md) | **Lắp ráp dụng cụ bằng khớp nối chuẩn** (giai đoạn C1 + C2): khoảng 40 bộ phận cho nhiều loại thí nghiệm (chưng cất, hồi lưu, điều chế – thu khí, nhiệt phân, lọc, chiết, cô cạn), hệ kín / hở, trọng tâm và đổ |
| [docs/12-khu-che-tao.md](docs/12-khu-che-tao.md) | **Khu chế tạo dụng cụ**: ngăn kính trong phòng lab, giao diện lắp ráp 3D riêng (bảng bộ phận có ảnh, bảng kiểm tra, hoàn tác), gia công ra bàn thí nghiệm |
| [docs/13-ban-ve-tay-cam-vr.md](docs/13-ban-ve-tay-cam-vr.md) | **Bản vẽ tay cầm VR (V1)**: số đo bàn tay → ống Ø34, 5 bản vẽ 2D (ba hình chiếu, bố trí trong, hộp thắt lưng, sơ đồ đấu nối 19 lõi, cách cầm và đeo), dự trù chi phí |
| [docs/14-mang-dong-chay.md](docs/14-mang-dong-chay.md) | **Mạng dòng chảy** (giai đoạn C3 + C4): bộ dụng cụ đã gia công chạy như thật: chưng cất (nhiệt độ sôi theo thành phần, nhiệt kế), hồi lưu, dẫn – rửa – thu khí, áp suất hệ kín, lọc / lọc hút, chiết, nhiệt phân, cô cạn; hình ảnh giọt, dòng, bọt khí, hơi, giọt ngưng, mực nước ống úp |
| [docs/15-tay-cam-v2-halo.md](docs/15-tay-cam-v2-halo.md) | **Tay cầm VR V2 "Halo"** (thay V1): ESP32 38 chân + BMI160 + joystick + cò analog bằng cảm biến Hall; vòng halo cho OpenCV đo xa gần; 7 file STL in 3D, 5 bản vẽ 2D, sơ đồ đấu nối |
| [docs/16-dieu-khien-bang-dau.md](docs/16-dieu-khien-bang-dau.md) | **Điều khiển góc nhìn bằng đầu qua webcam** (thử nghiệm, F8): OpenCV + YuNet nhận diện mặt, quay đầu để xoay (kiểu lái), ghé gần để phóng to, ngả xa để nhìn rộng; dùng song song với chuột, tay cầm |
| [docs/trinh-bay/VERA_Lab_trinh_bay.pdf](docs/trinh-bay/VERA_Lab_trinh_bay.pdf) | **Bản trình bày VERA Lab** (16 trang PDF): tên, mô tả, kiến trúc, tiến độ, tính năng Hóa, tay cầm Halo, quy hoạch 2 tầng Hóa – Sinh, LMS, nhiều người, AI cục bộ, lộ trình. Dựng lại bằng `tools/trinh_bay/lam_trinh_bay.py` |
| [docs/anh-phong-lab/](docs/anh-phong-lab/), [docs/anh-demo/](docs/anh-demo/) | Ảnh chụp cảnh phòng lab và từng bước demo chuẩn độ |

## Cấu trúc thư mục

```
firmware/tay_cam_mega/     Firmware Arduino Mega: đọc IMU thô, joystick, nút; rung + đèn
firmware/tay_cam_esp32cam/ Firmware tay cầm ESP32-CAM + 1 mạch joystick (USB hoặc Bluetooth, cùng gói tin)
Phong thi nghiem ao/        Project Unity 6 (URP)
  Assets/Scripts/
    Input/                 Nguồn tay cầm (serial, bàn phím-chuột), bộ lọc hướng, arm model
    Interaction/           Rót theo góc nghiêng, khóa buret, lắc bình, cúi đọc vạch
    Simulation/            Mô hình chuẩn độ (công thức pH)
    Recording/             Ghi chuyển động 100 Hz ra CSV
    Environment/           Quạt quay, trạm dịch chuyển, xem cảnh bằng chuột-bàn phím
    Demo/                  Chế độ demo: kịch bản chuẩn độ, giao diện, kính lúp, thẻ dụng cụ
    Editor/LabBuilder/     Dựng toàn bộ cảnh phòng thí nghiệm bằng code (menu KHKT)
  Scenes/PhongThiNghiem.unity  Cảnh phòng lab + phòng hóa chất + ban công
khkt/                      Thư viện Python: giao thức, bộ lọc hướng, mô hình chuẩn độ và rót, chỉ số khéo tay
tests/                     Kiểm thử cho khkt/ (21 bài)
tools/                     Công cụ: xem dữ liệu tay cầm, theo dõi bằng webcam (thử nghiệm)
server/ai/                 Máy chủ AI theo ngữ cảnh (FastAPI, khung)
data/reactions/            Cơ sở dữ liệu phản ứng (mẫu định dạng)
```

## Chạy nhanh

```bash
pip install -r requirements.txt
```

```bash
python -m unittest discover -s tests -v
```

```bash
python tools/tay_cam_monitor.py --port COM5
```

Nạp firmware: mở `firmware/tay_cam_mega/tay_cam_mega.ino` bằng Arduino IDE, chọn board *Arduino Mega 2560*, nạp. Firmware không cần thư viện ngoài (chỉ `Wire`).

Tay cầm joystick → Mega → ESP32-CAM (Wi-Fi): xem [docs/09-tay-cam-esp32-cam.md](docs/09-tay-cam-esp32-cam.md). Thử game bằng tay cầm giả lập (mở game trước):

```bash
python tools/tay_cam_udp.py gia-lap --mega
```

Unity: xem [docs/05-unity.md](docs/05-unity.md).

## Trạng thái

Đây là **khung**: firmware, giao thức, mô hình chuẩn độ và phân tích kỹ năng đã viết đầy đủ và có kiểm thử Python; script Unity đã biên dịch sạch bằng trình biên dịch của Unity nhưng **chưa chạy thử trong cảnh**; `tools/webcam_tracker.py` mới thử trên ảnh giả lập, chưa thử với webcam thật. Cảnh phòng lab 3D đã dựng, có chế độ demo chạy bằng chuột-bàn phím: bài chuẩn độ làm được trọn vẹn, mọi dụng cụ có thẻ thông tin (xem [docs/05-unity.md](docs/05-unity.md#chạy-demo)). Chưa gắn tay cầm thật và kính VR; đồng bộ nhiều người (Photon) và bạn AI chưa làm.
