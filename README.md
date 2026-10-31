# VERA Lab: phòng thí nghiệm thực tế ảo Hóa – Sinh tích hợp AI.

**VERA Lab** (Virtual Experiment & Reality-based AI Lab) là hệ thống trải nghiệm phòng thí nghiệm thực tế ảo Hóa – Sinh, tích hợp AI đám mây / cục bộ, dùng trong dạy học. Đây là dự án Khoa học kỹ thuật của **Nguyễn Ngọc Bảo Trọng** và **Trần Đức Trọng**.

Thành phần chính:

- **Phòng lab hóa học ảo 3D** (Unity 6, URP):
  - 720 hóa chất xếp kệ, bộ máy hóa học tính phản ứng thật;
  - chuẩn độ, phòng máy phân tích, lắp ráp dụng cụ tự do kèm mạng dòng chảy;
  - robot trợ lý theo kế hoạch AI.
- **Tay cầm tự chế V2 "Halo":**
  - ESP32 DevKitC 38 chân + cảm biến chuyển động BMI160 + joystick KY-023 + cò analog bằng cảm biến Hall SS49E;
  - vòng halo huỳnh quang để webcam (OpenCV) đo xa gần và vị trí tay;
  - vỏ in 3D. Xem [docs/15](docs/15-tay-cam-v2-halo.md).
- **Điều khiển góc nhìn bằng đầu qua webcam** (OpenCV, thử nghiệm). Xem [docs/16](docs/16-dieu-khien-bang-dau.md).
- **Web quản lý:** kho hóa chất, thiết bị, nhật ký; mạng lưới khóa AI.

> **Phần cứng đã thay:** các bản tay cầm cũ (Arduino Mega + MPU6050; joystick → Mega → ESP32-CAM; bản V1 ống PVC) không còn dùng. Tài liệu và firmware của chúng vẫn giữ trong kho để tham khảo. Mỗi tài liệu cũ có ghi chú ở đầu.

## Tài liệu

> **Thành viên mới (tầng 2 lab sinh học, máy chủ nhiều người):** đọc [contracts.md](contracts.md) trước khi viết code.

### Đang dùng

| Tài liệu | Nội dung |
| --- | --- |
| [docs/15-tay-cam-v2-halo.md](docs/15-tay-cam-v2-halo.md) | **Tay cầm V2 "Halo"**: ESP32 38 chân + BMI160 + joystick + cò analog Hall; vòng halo cho OpenCV đo xa gần; 7 file STL in 3D, 5 bản vẽ 2D, sơ đồ đấu nối |
| [docs/16-dieu-khien-bang-dau.md](docs/16-dieu-khien-bang-dau.md) | **Điều khiển góc nhìn bằng đầu qua webcam** (F8): OpenCV + YuNet nhận diện mặt; quay đầu để xoay (kiểu lái), ghé gần để phóng to, ngả xa để nhìn rộng |
| [docs/05-unity.md](docs/05-unity.md) | Cảnh phòng thí nghiệm (mặt bằng, phím xem thử), cài đặt Unity |
| [docs/06-phong-lab-moi-hoa-chat-ai.md](docs/06-phong-lab-moi-hoa-chat-ai.md) | Lab 300 m², 720 hóa chất xếp kệ và tra cứu, bộ máy hóa học, chuẩn độ tự chọn, AI đám mây và robot trợ lý |
| [docs/07-web-quan-ly-robot-phong-moi.md](docs/07-web-quan-ly-robot-phong-moi.md) | Web quản lý (kho, thiết bị, nhật ký), mạng lưới khóa Groq / DeepSeek / Gemini, robot hình người theo kế hoạch AI, phòng máy phân tích và phòng robot |
| [docs/08-tai-lieu-tham-khao-thiet-bi.md](docs/08-tai-lieu-tham-khao-thiet-bi.md) | Tài liệu, datasheet, tiêu chuẩn (ưu tiên tiếng Việt) về nguyên lý và cách dùng máy phân tích, thiết bị phòng lab |
| [docs/10-lab-quoc-te-tai-khu-toi-uu.md](docs/10-lab-quoc-te-tai-khu-toi-uu.md) | Phong cách lab trường quốc tế, cửa tự động, tải / gỡ từng khu khi đi qua cửa mà vẫn giữ hiện trạng; tối ưu đồ họa (occlusion culling, HLOD, impostor, cửa sổ ảo) kèm số đo FPS |
| [docs/11-lap-rap-tu-do.md](docs/11-lap-rap-tu-do.md) | Lắp ráp dụng cụ bằng khớp nối chuẩn: khoảng 40 bộ phận cho chưng cất, hồi lưu, điều chế – thu khí, nhiệt phân, lọc, chiết, cô cạn; hệ kín / hở, trọng tâm và đổ |
| [docs/12-khu-che-tao.md](docs/12-khu-che-tao.md) | Khu chế tạo dụng cụ: ngăn kính trong phòng lab, giao diện lắp ráp 3D riêng, gia công ra bàn thí nghiệm |
| [docs/19-may-phan-tich-dot-3-4.md](docs/19-may-phan-tich-dot-3-4.md) | **Máy phân tích đợt 3 – 4:** XRD (thẻ chuẩn Handbook of Mineralogy, Bragg, Scherrer), SEM-EDS (ảnh tinh thể, phổ tia X), ¹H-NMR 60 MHz (FID, tách vạch, qNMR), bom nhiệt lượng (hiệu chuẩn axit benzoic), tủ sấy, lò nung, máy cất nước, nồi hấp (IAPWS-IF97); AI trợ lý đọc quy trình máy; trang Hướng dẫn máy trên web |
| [docs/18-may-phan-tich-dot-2.md](docs/18-may-phan-tich-dot-2.md) | **Máy phân tích đợt 2: FTIR, GC-MS, HPLC, chuẩn độ điện thế** có màn hình vận hành riêng: giao thoa đồ và trừ phổ nước (FTIR), phổ khối và chỉ số lưu NIST, định lượng metanol trong rượu (GC-MS), 3 phương pháp axit hữu cơ / đường / axit benzoic (HPLC), hiệu chuẩn điện cực, hệ số chuẩn KHP, đồ thị Gran (chuẩn độ) |
| [docs/17-may-phan-tich-dot-1.md](docs/17-may-phan-tich-dot-1.md) | **Máy phân tích đợt 1: màn hình vận hành UV-Vis và AAS** như phần mềm máy thật: quy trình (Hướng dẫn / Thực hành), sơ đồ bên trong máy có hoạt cảnh, đường chuẩn, pha loãng, tách hỗn hợp bằng phổ; số liệu có nguồn (NIST, sổ tay Agilent, Shimadzu) |
| [docs/14-mang-dong-chay.md](docs/14-mang-dong-chay.md) | Mạng dòng chảy: bộ dụng cụ chạy như thật (chưng cất theo nhiệt độ sôi, hồi lưu, dẫn – rửa – thu khí, áp suất hệ kín, lọc, chiết, nhiệt phân, cô cạn); hình ảnh giọt, dòng, bọt khí, hơi |
| [docs/trinh-bay/VERA_Lab_trinh_bay.pdf](docs/trinh-bay/VERA_Lab_trinh_bay.pdf) | Bản trình bày VERA Lab (16 trang PDF). Dựng lại bằng `tools/trinh_bay/lam_trinh_bay.py` |
| [docs/anh-phong-lab/](docs/anh-phong-lab/), [docs/anh-demo/](docs/anh-demo/) | Ảnh chụp phòng lab và các bài demo |

### Tài liệu cũ (phần cứng đã thay, giữ để tham khảo)

| Tài liệu | Nội dung |
| --- | --- |
| [docs/01-de-xuat-dieu-khien.md](docs/01-de-xuat-dieu-khien.md) | Nghiên cứu, đề xuất hệ điều khiển ban đầu (Arduino Mega + MPU6050) |
| [docs/02-giao-thuc-tay-cam.md](docs/02-giao-thuc-tay-cam.md) | Gói tin nhị phân Mega → PC |
| [docs/03-kien-truc.md](docs/03-kien-truc.md) | Kiến trúc phần mềm ban đầu (phần tay cầm theo bản Mega) |
| [docs/04-kiem-thu-tuan-1.md](docs/04-kiem-thu-tuan-1.md) | Danh sách đo kiểm tuần 1 (độ trễ, trôi góc, say) |
| [docs/09-tay-cam-esp32-cam.md](docs/09-tay-cam-esp32-cam.md) | Tay cầm joystick → Arduino Mega → ESP32-CAM → Wi-Fi |
| [docs/13-ban-ve-tay-cam-vr.md](docs/13-ban-ve-tay-cam-vr.md) | Bản vẽ tay cầm V1 (ống PVC + hộp thắt lưng). Số đo bàn tay vẫn dùng cho V2 |

## Cấu trúc thư mục

```
Phong thi nghiem ao/        Project Unity 6 (URP)
  Assets/Scripts/
    Simulation/            Bộ máy hóa học (cân bằng, động học, màu, bay hơi), danh mục hóa chất
    Interaction/           Vật chứa, rót, buret, nguồn nhiệt, cân, máy ly tâm
    Assembly/              Lắp ráp bằng khớp nối, khu chế tạo, mạng dòng chảy (Flow/)
    Demo/                  Bàn tay ảo, giao diện, robot trợ lý, máy phân tích, kịch bản chuẩn độ
    Input/                 Tay cầm (UDP / serial), bàn phím – chuột, điều khiển bằng đầu (HeadTracking)
    Rendering/             Tải / gỡ từng khu, lưu trạng thái, tối ưu đồ họa
    Environment/           Người xem (DesktopViewer), cửa tự động, trạm dịch chuyển
    Editor/LabBuilder/     Dựng toàn bộ cảnh bằng code (menu KHKT)
data/chem/                 Danh mục hóa chất, nhiệt độ sôi
server/                    Web quản lý (web/, lab/), máy chủ AI (ai/)
tools/                     head_tracker.py (webcam), tay_cam_v2/ (dựng 3D, bản vẽ tay cầm Halo),
                           trinh_bay/, kiem_thu_unity/ (bài chạy tự động Unity), mo_hinh/ (mô hình YuNet)
khkt/                      Thư viện Python: giao thức, bộ lọc hướng, mô hình chuẩn độ và rót, chỉ số khéo tay
tests/                     Kiểm thử Python và kiểm thử bộ máy hóa học (C#)
firmware/                  Firmware các bản tay cầm CŨ (Mega, ESP32-CAM), giữ để tham khảo
```

## Chạy nhanh

```bash
pip install -r requirements.txt
```

```bash
python -m unittest discover -s tests -v
```

Unity: mở thư mục `Phong thi nghiem ao` bằng Unity 6000.6.7f1, chạy menu **KHKT → Dựng phòng thí nghiệm**, rồi Play. Xem [docs/05-unity.md](docs/05-unity.md).

Điều khiển bằng đầu qua webcam: trong Unity bấm **F8**. Cần Python, `opencv-python` và webcam. Thử riêng chương trình nhận diện:

```bash
python tools/head_tracker.py --hien
```

## Trạng thái

| Phần | Trạng thái |
| --- | --- |
| Phòng lab hóa học 3D | Chạy được bằng chuột – bàn phím: chuẩn độ, thí nghiệm định tính, tra cứu 720 hóa chất, robot trợ lý, lắp ráp và gia công bộ dụng cụ, mạng dòng chảy. Có bài chạy tự động kiểm tra từng phần |
| Phòng máy phân tích | Cả 14 máy (UV-Vis, AAS, FTIR, GC-MS, HPLC, chuẩn độ điện thế, XRD, SEM-EDS, NMR, bom nhiệt lượng, tủ sấy, lò nung, máy cất nước, nồi hấp) có màn hình vận hành như máy thật, quy trình Hướng dẫn / Thực hành, phân tích chuyên sâu (docs/17 – 19). AI trợ lý hướng dẫn theo đúng quy trình máy |
| Tay cầm V2 "Halo" | Đã có thiết kế 3D, bản vẽ, sơ đồ đấu nối. Đang đặt linh kiện, **firmware cho ESP32 + BMI160 chưa viết** |
| Điều khiển bằng đầu (webcam) | Thử nghiệm, chạy được (F8) |
| Bản chạy | Windows (.exe). Bản macOS cần cài thêm mô-đun Mac Build Support cho Unity |
| Chưa làm | Phòng sinh học tầng 2, hệ thống học tập cho giáo viên – học sinh, nhiều người cùng làm, AI cục bộ |
