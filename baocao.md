# Báo cáo dự án VERA Lab

**VERA Lab** (Virtual Experiment & Reality-based AI Lab): hệ thống trải nghiệm phòng thí nghiệm thực tế ảo Hóa – Sinh tích hợp AI trong dạy học.

**Tác giả:** Nguyễn Ngọc Bảo Trọng, Trần Đức Trọng. Lĩnh vực: Khoa học kỹ thuật.

## 1. Giới thiệu

Thực hành là phần cốt lõi của môn Hóa và Sinh, nhưng nhiều trường khó tổ chức đầy đủ:
- hóa chất đắt hoặc độc, thí nghiệm có nguy cơ gây tai nạn;
- máy phân tích như GC-MS, NMR, SEM, PCR thường không có ở trường phổ thông;
- học sinh không được phép làm sai, nên ít khi thấy hậu quả của một thao tác sai.

VERA Lab là tòa nhà thí nghiệm ảo 3D hai tầng, đi lại và thao tác bằng **góc nhìn thứ nhất**. Tầng 1 là khu Hóa học, tầng 2 là khu Sinh học. Kết quả thí nghiệm do mô hình tính toán sinh ra chứ không phát lại đoạn phim dựng sẵn, nên làm sai sẽ ra kết quả sai, giống ngoài đời.

**Mục tiêu:**
1. Cho học sinh thực hành an toàn, lặp lại không giới hạn, không tốn hóa chất.
2. Cho học sinh làm quen quy trình vận hành của máy phân tích hiện đại.
3. Có trợ lý AI và robot hướng dẫn đúng theo quy trình của từng thiết bị.
4. Điều khiển tự nhiên hơn chuột – bàn phím, bằng webcam và tay cầm tự chế giá rẻ.

## 2. Ứng dụng

| Đối tượng | Cách dùng |
|---|---|
| Học sinh THPT | Làm thí nghiệm theo chương trình trước và sau giờ học thật; luyện đến khi thành thạo, thử cả thao tác sai |
| Giáo viên | Minh họa thí nghiệm nguy hiểm hoặc thiếu thiết bị; quản lý kho và nhật ký qua web |
| Trường thiếu phòng thí nghiệm | Bổ sung trải nghiệm máy phân tích và kỹ thuật sinh học phân tử mà trường chưa có |
| Học sinh giỏi, định hướng STEM | Tìm hiểu nguyên lý máy, đọc phổ, dựng đường chuẩn, tự lắp bộ dụng cụ |

VERA Lab bổ sung cho thực hành thật, không thay thế kỹ năng cầm nắm ngoài đời.

## 3. Tính năng

### Tầng 1: Hóa học
- **720 hóa chất** xếp trên kệ, có tra cứu và dẫn tới đúng vị trí.
- **Thí nghiệm định tính và chuẩn độ**: tự chọn hóa chất, chất phản ứng theo tính toán.
- **14 máy có màn hình vận hành** như phần mềm máy thật, có chế độ Hướng dẫn và Thực hành, sơ đồ bên trong máy và đồ thị: UV-Vis, AAS, FTIR, GC-MS, HPLC, chuẩn độ điện thế, XRD, SEM-EDS, ¹H-NMR, bom nhiệt lượng, tủ sấy, lò nung, máy cất nước, nồi hấp.
- **Khu chế tạo dụng cụ**: lắp khoảng 40 loại bộ phận bằng khớp nối chuẩn trong giao diện 3D riêng, rồi đưa bộ dụng cụ ra bàn thí nghiệm.
- **Mạng dòng chảy**: bộ tự lắp chạy được, gồm chưng cất, hồi lưu, dẫn và thu khí, lọc, chiết, cô cạn; hệ kín có áp suất tăng.

### Tầng 2: Sinh học
- 6 khu chức năng (sinh học chung, vi sinh, sinh học phân tử, kho sinh phẩm, nhà kính, hành lang), nối với tầng 1 bằng cầu thang và thang máy.
- **108 dụng cụ** có hồ sơ công dụng và thông số.
- **16 bài thí nghiệm** theo chương trình GDPT 2018, gồm: quan sát tế bào hành, khí khổng, co nguyên sinh, catalase, lên men, quang hợp, tách chiết ADN, nhuộm Gram, cấy ria, đếm vi sinh, PCR – điện di, đường cong OD600, ghép gen ADN tái tổ hợp.
- **An toàn sinh học**: chỉ dùng chủng dạy học cấp BSL-1. AI từ chối các yêu cầu vượt cấp này.

### AI và robot
- **Trợ lý AI** trả lời theo đúng quy trình của từng máy, lấy từ hồ sơ máy.
- **Robot hình người** nhận yêu cầu bằng lời, AI lập kế hoạch từng bước, hệ thống kiểm tra an toàn rồi mới cho robot làm.
- **Mạng lưới khóa AI**: nhiều nhà cung cấp, tự chuyển sang khóa khác khi lỗi. Khóa chỉ nằm ở máy chủ.

### Điều khiển và quản lý
- **Webcam**: quay đầu để xoay góc nhìn, ghé gần để phóng to, theo dõi tròng mắt, cử chỉ tay bằng MediaPipe; tự chuyển về YuNet nếu thiếu MediaPipe.
- **Tay cầm tự chế V2 "Halo"**: đã có thiết kế 3D, bản vẽ và sơ đồ đấu nối.
- **Web quản lý**: kho hóa chất, thiết bị, nhật ký, hướng dẫn máy, trang quản lý khu sinh học.

## 4. Nguyên lý hoạt động

### Mô phỏng hóa học
Mỗi dung dịch lưu thành phần chất theo mol. `ChemEngine` tính cân bằng, động học, màu và bay hơi, nên kết quả phụ thuộc vào đúng lượng và thứ tự học sinh đã làm.

### Mô phỏng máy phân tích
Mỗi máy tính tín hiệu từ thành phần mẫu thật trong ống của học sinh, theo nguyên lý vật lý của máy:

| Máy | Nguyên lý dùng trong mô hình |
|---|---|
| UV-Vis, AAS | Định luật Beer – Lambert, đường chuẩn, tách hỗn hợp bằng phổ |
| FTIR | Giao thoa đồ, biến đổi Fourier, trừ phổ nước |
| GC-MS, HPLC | Thời gian lưu, chỉ số lưu, phổ khối tham chiếu NIST |
| XRD | Định luật Bragg, công thức Scherrer, so khớp thẻ chuẩn khoáng vật |
| SEM-EDS | Phổ tia X, định lượng bằng khớp toàn phổ không âm |
| ¹H-NMR | Tín hiệu FID, biến đổi Fourier, tách vạch, định lượng qNMR |
| Bom nhiệt lượng | Hiệu chuẩn bằng axit benzoic, hiệu chỉnh Regnault – Pfaundler |
| Nồi hấp | Bảng hơi nước IAPWS-IF97, mô hình giảm bậc log vi sinh |

Thông số có nguồn dẫn; phần ước lượng được đánh dấu `mo_phong` trong dữ liệu.

**Ví dụ kết quả đo được từ bài kiểm thử tự động:**

| Thí nghiệm | Kết quả mô phỏng | Giá trị đối chiếu |
|---|---|---|
| Nhiệt đốt cháy glucozơ | 15570 J/g | 15569 J/g (NIST) |
| Chuẩn độ giấm | 0,8301 mol/L | 0,8300 mol/L (đáp án) |
| Metanol trong rượu bằng GC-MS | 1602 mg/L | 1602 mg/L (đáp án) |
| Nung đá vôi 900 °C | còn 56,0 % | 56,0 % (lý thuyết CaO) |
| Nồi hấp đúng quy trình | 120,8 °C, giảm 8,5 bậc log | đạt tiệt trùng |
| Nồi hấp không đuổi khí | đồng hồ 1,05 bar nhưng chỉ 94 °C | không tiệt trùng |

Các số này chứng tỏ mô hình tính đúng so với đáp án và giá trị tham chiếu, chưa phải là đánh giá hiệu quả dạy học.

### Mô phỏng sinh học
Thư viện `Khkt.Simulation.Bio` gồm các mô hình quang học hiển vi, thẩm thấu, tăng trưởng khuẩn lạc, lên men, quang hợp, thoát hơi nước, PCR, điện di, nhuộm Gram, nhiễm chéo và ghép gen.

### Tương tác và tối ưu đồ họa
- **Cảnh dựng hoàn toàn bằng code**: dễ sửa, dễ kiểm thử lại.
- **Tải / gỡ từng khu**: mỗi phòng là một cảnh riêng, có lưu trạng thái; quay lại thì dung dịch vẫn như cũ.
- Dùng occlusion culling và cửa sổ ảo; mọi kỹ thuật đều được đo, kỹ thuật không làm nhanh hơn thì bỏ.

### Điều khiển bằng webcam
Python đọc webcam, MediaPipe tìm 478 điểm trên mặt và các điểm bàn tay, lọc rung bằng bộ lọc One Euro, rồi gửi qua UDP cổng 9051 sang Unity. Unity đổi các số này thành góc nhìn, độ phóng và cử chỉ.

### AI và robot
Máy chủ FastAPI đưa cảnh hiện tại và quy trình máy vào câu hỏi. AI trả về kế hoạch dạng cấu trúc, gồm các kỹ năng robot có sẵn. Bộ kiểm tra loại bước sai hoặc mất an toàn trước khi robot thực hiện.

## 5. Công nghệ

| Lớp | Công nghệ |
|---|---|
| Đồ họa 3D | Unity 6 (6000.6.7f1), URP 17.6, Input System |
| Mô phỏng | C# thuần, chạy và kiểm thử được ngoài Unity |
| Thị giác máy tính | Python, OpenCV, MediaPipe 1.2.0, YuNet |
| Máy chủ | Python, FastAPI, SQLite |
| AI | Groq, DeepSeek, Gemini, Claude (tùy chọn) qua lớp nhà cung cấp chung |
| Web | HTML, CSS, JavaScript |
| Phần cứng | ESP32 DevKitC, BMI160, joystick KY-023, cảm biến Hall SS49E, vỏ in 3D |
| Kiểm thử | Python `unittest`, bài C# riêng, bài Unity tự động chụp ảnh và ghi log |
| Nền tảng | Windows, macOS |

## 6. Điểm mới

1. **Kết quả tính từ mẫu của chính học sinh.** Cùng một máy, mẫu pha sai hoặc không pha loãng sẽ ra kết quả sai và có cảnh báo tương ứng.
2. **Thao tác sai có hậu quả đúng nguyên lý.** Ví dụ: nồi hấp không đuổi khí thì đồng hồ vẫn báo đủ áp nhưng không tiệt trùng; bỏ ADN ligaza thì không có khuẩn lạc trắng.
3. **Hóa học và Sinh học dùng chung một bộ máy.** Phản ứng catalase ở tầng 2 chạy trên cùng bộ máy hóa học của tầng 1.
4. **Máy phân tích vận hành theo quy trình thật**, kèm sơ đồ bên trong máy, không chỉ bấm nút ra đáp số.
5. **AI bị ràng buộc bởi quy trình và bộ kiểm tra**: robot chỉ làm các bước đã qua kiểm tra an toàn.

*Các điểm trên là đặc điểm của hệ thống. Nhóm chưa khảo sát đối chiếu có hệ thống với các phần mềm thí nghiệm ảo khác.*

## 7. Tính sáng tạo

- **Tự do thật sự trong lắp ráp**: học sinh tự chọn bộ phận và tự nối; bộ lắp chạy theo cách đã nối, kể cả khi nối sai.
- **Điều khiển bằng cơ thể với thiết bị rẻ**: chỉ cần webcam thường là quay đầu nhìn được, thêm tay cầm tự chế thay cho kính VR đắt tiền.
- **Giao diện riêng khi cần tập trung**: màn hình máy và khu chế tạo tạm tắt cảnh 3D, vừa dễ nhìn vừa nhẹ máy.
- **Tối ưu bằng số đo**: giữ hay bỏ từng kỹ thuật đồ họa dựa trên FPS đo trên laptop Intel Iris Xe.
- **Kiểm thử như học sinh thật**: bài tự động thao tác đúng và cố ý thao tác sai, rồi đối chiếu kết quả.

## 8. Trạng thái và hạn chế

**Kết quả kiểm thử mới nhất:** 149 bài Python đạt; 147 bài mô phỏng C# đạt; bài Unity `phong_adn` (30 mục), `khu` và bài đầy đủ đều chạy xong không lỗi biên dịch.

**Chưa làm:**
- máy chủ nhiều người cùng vào một phòng;
- bàn lập trình khối lệnh ở phòng robot;
- firmware tay cầm V2 "Halo" (đang đặt linh kiện);
- AI cục bộ; hệ thống học tập cho giáo viên – học sinh;
- khảo sát hiệu quả dạy học trên học sinh thật.

**Hạn chế đã biết:**
- Mô phỏng ở mức giáo dục: đúng định tính và gần đúng định lượng trong phạm vi các bài, không thay cho phần mềm nghiên cứu.
- Mức khoảng 66 FPS được đo trên Intel Iris Xe khi mới có tầng 1; **chưa đo lại trên cả tòa nhà hai tầng**.
- Theo dõi bằng MediaPipe trên Windows mới thử 10 giây: xử lý trung bình 15,5 ms mỗi khung, 25,5 khung/giây, CPU khoảng 69 %. Lần thử đó không nhận được mặt nào trong khung hình, nên **độ chính xác trên Windows chưa được kiểm chứng**.
- Chi tiết kỹ thuật và ảnh chụp nằm trong thư mục [docs/](docs/).
