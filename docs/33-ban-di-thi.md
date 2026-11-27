# Bản đi thi (26/11/2026)

Bản `.exe` dùng khi đi thi. Thư mục `VERA_Lab` được nén thành một tệp `.zip`. Bản này gộp 5 thay đổi so với `main` e936d8f.

## 1. Dụng cụ nhỏ to hơn 35%

- Ống nghiệm, giá ống nghiệm, cốc, bình nón, bình cầu, ống nhỏ giọt, bình tia, pipet, nhiệt kế, đũa thủy tinh, đĩa petri, bát sứ, đèn cồn và kẹp được phóng to ×1,35 khi cảnh nạp. Mã nằm ở `Interaction/DungCuPhongTo.cs`, gọi từ `Grabbable.Awake`.
- Gốc dụng cụ ở đáy, nên dụng cụ vẫn đứng đúng trên mặt bàn. Dung tích và hóa học không đổi vì `capacityMl` không phụ thuộc kích thước. Độ cao điểm cầm, chiều dài và bán kính khi nằm được phóng theo, nên tay ảo và robot cầm đúng chỗ.
- Đặt vào ổ vừa khít cỡ thật thì về cỡ gốc, lấy ra lại to. Các ổ đó gồm máy ly tâm, máy đo quang, máy phân tích, đầu dò, cân và ổ dưới buret. Đầu buret chỉ cao hơn miệng bình nón 5 mm, nên bình to sẽ đâm vào buret.
- Không phóng:
  - lọ hóa chất, vì kệ kho xếp sát nhau;
  - buret, phễu, ống ly tâm, cuvet, tiêu bản;
  - bộ phận lắp ráp (`ApparatusPart`).
- Đổi hệ số bằng biến môi trường `KHKT_PHONG_TO`, ví dụ `1.3`. Đặt `1` là tắt.

## 2. Bàn thí nghiệm đen tiêu chuẩn

- Công cụ `Editor/KhktBanDen.cs` (menu **KHKT → Đổi bàn thí nghiệm sang đen**) đổi mọi bàn thí nghiệm trong 12 cảnh của bản build:
  - mặt bàn "Mặt bàn…" và "Mặt làm việc" sang epoxy đen bóng mờ (`BanDen_MatBan`);
  - tủ dưới, cánh tủ, ngăn kéo, chân bàn sang đen than (`BanDen_Than`).
- Mặt bàn gỗ (bàn giáo viên, bàn trang trí) giữ nguyên.
- Trình dựng cảnh (`LabSceneBuilder`) cũng đổi màu mặt bàn sang đen. Nếu dựng lại cảnh, chạy lại `KhktBanDen.Run` để thân bàn cũng đen.

## 3. Webcam: hướng đầu và nắm / thả bằng tay trái

Webcam chạy ngay trong ứng dụng (Unity Inference Engine), không gọi Python. Lý do: máy thi có thể bật Smart App Control, chặn Python, OpenCV và MediaPipe, và bản `.exe` không kèm `tools/`. Webcam tự bật sau khi vào lab 1,5 giây.

| Việc | Cách làm |
| --- | --- |
| Xoay góc nhìn | Quay đầu quá khoảng 30° về một phía. Góc nhìn xoay liên tục về phía đó, nhìn thẳng thì dừng |
| Phóng to / nhìn rộng | Ghé gần webcam để phóng to, ngả ra xa để nhìn rộng |
| Căn hướng đầu | Lúc webcam bật, nhìn thẳng màn hình 2 giây. **F12** để căn lại |
| Cầm vật | Tay cầm VERA (BMI160) trỏ vào dụng cụ, **nắm chặt tay trái** trước webcam |
| Thả / đặt vật | **Xòe tay trái**. Đang trỏ chỗ đặt thì đặt vào đó, không thì trả về chỗ cũ |
| Bật / tắt | **F10** bật hoặc tắt webcam. **F8** chỉ bật hoặc tắt phần xoay theo đầu |

- **Mặt:**
  - Dùng YuNet (`face_detection_yunet_2023mar`), mã ở `Input/YuNetFace.cs`, mô hình ở `Lab/MoHinh/Resources/face_yunet.onnx`.
  - Cách giải mã đối chiếu với `cv2.FaceDetectorYN` trên 6 ảnh chân dung: trùng đến 0,1 px.
  - Tư thế đầu tính như `tools/head_tracker.py` (ảnh 320 px), lọc One Euro, rồi đưa vào `HeadTracking.Feed`. Vì vậy ngưỡng 30° / 50° và mức phóng giữ như cũ.
- **Tay trái:**
  - Lấy tay ở bên trái khung hình đã lật như gương. Thấy 2 tay thì lấy cổ tay bên trái hơn; thấy 1 tay thì chỉ nhận nếu tay đó ở nửa trái, vì tay phải đang cầm tay cầm VERA.
  - Không dùng nhãn trái/phải của mô hình, vì chưa kiểm chứng trên webcam thật.
  - Nắm (`nam_doc` / `nam_ngang` của `HandGesture`) giữ 0,12 giây thì tính là nắm. Xòe 0,15 giây, hoặc mất tay 0,6 giây, thì tính là thả.
- **Chạm kẹt:** nếu tay cầm báo chạm A hoặc B liên tục ngay từ lúc nối mà chưa từng nhả, game bỏ qua chạm đó tới khi nhả và báo trên màn hình. Lỗi này đo được ngày 25/11, khi dây chạm B chập GND.

## 4. Ô khóa API ở Cài đặt → Trợ lý AI

- Có các phần:
  - chọn nhà cung cấp: Gemini, Groq, Claude, OpenAI;
  - ô dán khóa;
  - nút **Lưu khóa**, **Kiểm tra** (gọi thử nhà cung cấp) và **Xóa khóa**.
- Dùng được cả khi vào **Dùng thử**, không cần đăng nhập. Khóa lưu thành **khóa chung của máy này** (`ApiKeyStore.DeviceOwner`), mã hóa bằng DPAPI.
- Tài khoản đã đăng nhập mà chưa có khóa riêng thì dùng khóa chung đó.
- Khóa đã lưu không bao giờ hiện lại, chỉ hiện dạng che.

## 5. Bài demo cho giám khảo (F2)

- 3 bài nổi bật đứng đầu danh sách:
  - **Phản ứng đổi màu glucozơ + Cu(OH)₂**: kết tủa xanh, chuyển phức xanh lam thẫm, đun nóng ra Cu₂O đỏ gạch. Robot làm mẫu được.
  - **Trợ lý AI ra lệnh cho robot bằng tiếng Việt**: gửi sẵn lệnh "làm thí nghiệm nhận biết Fe³⁺ bằng KSCN rồi giải thích". AI lập kế hoạch, robot làm thật trong lab.
  - **Mổ ếch tự do**: tới bàn giải phẫu số thì bàn mổ ếch tự mở.
- Bài nào cũng có nút **Hỏi trợ lý AI** (**F5**): mở khung trò chuyện và gửi câu hỏi về đúng bài đó.
- Không có mạng hoặc khóa AI thì robot vẫn hiểu các lệnh thường dùng, và nút **Robot làm mẫu** vẫn chạy.

## Kiểm thử

- Bài thử tự động trong Unity (tạm, không đưa vào bản build): phóng to dụng cụ, ổ máy về cỡ gốc, mặt bàn đen, YuNet C# so với OpenCV, khóa API khách, bảng demo, và tay cầm VERA giả trỏ vật rồi tay trái nắm / xòe.
- Kết quả ghi ở phần cuối báo cáo phiên.
- **Chưa thử với webcam và người thật:** độ nhạy cử chỉ nắm / xòe và hướng đầu cần thử trước buổi thi.

## 6. Nói tiếng Việt với trợ lý AI (27/11/2026)

- **Tay cầm VERA:** tay rảnh, không trỏ vào dụng cụ nào, **giữ nút chạm A 3 giây**. Trong lúc giữ, góc dưới bên phải hiện thanh đếm. Đủ 3 giây thì khung trợ lý mở và bắt đầu thu âm. **Giữ tiếp để nói, thả A là gửi cho AI.**
  - Tay đang cầm vật: giữ A vẫn là dùng vật (rót, nhỏ giọt). Đang trỏ vào thiết bị: A vẫn là bấm thiết bị.
  - A báo chạm liên tục từ lúc nối (dây chập) thì bị bỏ qua tới khi thả.
- **Nút Mic** trên khung trợ lý (hoặc phím **V** khi khung mở): bấm để bắt đầu nói, nút đổi thành đỏ "Dừng", bấm lần nữa để gửi.
- **Chuyển giọng nói thành chữ:** dùng AI của Google (Gemini, mô hình `gemini-flash-latest`) bằng khóa API ở Cài đặt → Trợ lý AI. Khóa Groq và OpenAI dùng Whisper (tiếng Việt). Khóa Claude không nhận âm thanh, game báo rõ.
  - Ghi 16 kHz mono, tối đa 30 giây. Âm nhỏ được tăng âm tự động; lời nhắc nêu thuật ngữ hóa học (NaOH, HCl, CuSO₄, ống nghiệm, buret...).
  - Chữ nhận được hiện thành tin "Bạn" trong khung trò chuyện và gửi cho AI như người dùng gõ.
  - Lỗi (không có micro, nói quá ngắn, không nghe thấy tiếng, hết hạn mức, mất mạng) hiện ở dòng trạng thái của khung, tự tắt sau 8 giây. Khóa không bao giờ nằm trong địa chỉ hay thông báo lỗi.
- **Cần:** micro (laptop có sẵn), quyền micro cho ứng dụng (Windows → Quyền riêng tư → Micro), mạng, và một khóa Gemini miễn phí lấy ở aistudio.google.com.
- **Đã kiểm (20/20):** khuôn WAV, yêu cầu Gemini và Whisper tới máy chủ giả đúng khuôn, Claude báo lỗi rõ, mất mạng không lộ khóa, giữ A 1,5 / 2,5 giây chưa nghe, quá 3 giây thì nghe, thả A thì gửi, chạm A ngắn không kích hoạt, nút Mic bấm hai lần. Yêu cầu gọi tới máy chủ Google thật bằng khóa giả được Google trả "API key not valid", tức khuôn yêu cầu đúng.
- **Chưa thử:** nói thật vào micro và nhận chữ thật (cần khóa Gemini thật của bạn).

## 7. Sửa lỗi hai tay và lỗi lag khi nhấn E vào phòng giải phẫu (27/11/2026)

- **Tay phải bị méo khi chụm tay trái:**
  - Nguyên nhân: bàn tay trái theo dõi bằng webcam bị dùng làm "người lái" của tay phải ảo (`ArmRig.Drive`): điểm ngắm của tay phải đặt vào lòng bàn tay trái, và chụm tay trái được coi là giữ chuột. Ngoài ra nhãn trái/phải của mô hình nhảy qua lại khi chụm.
  - Sửa: khi có tay cầm VERA, tay phải (kể cả tia trỏ, cầm, thả) chỉ do tay cầm điều khiển; webcam chỉ lo tay trái (cánh tay trái ảo và nắm / xòe để cầm / thả). Bên tay xác định theo vị trí trong khung hình đã lật như gương, không theo nhãn của mô hình.
  - Kiểm thử trong Unity: chụm tay trái, rồi chụm / mở liên tục 8 lần, rồi tay trái rời khung: tay phải lệch 0,0 cm, 0,0°; không còn bị "lái". 5/5 đạt.
- **Lag khi nhấn E ở cổng Phòng Sinh học 3D (mở phòng giải phẫu):**
  - Đo trước khi sửa: lần nhấn E đầu tiên đứng hình khoảng 1,3 s. Gỡ nén `bp3d.bin.gz` và giải mã 115 nghìn đỉnh lớp xương mất khoảng 490 ms trên luồng chính, dựng sân khấu 142 ms, rồi một khung hình vẽ đầu tiên 664 ms. Lần thứ hai chỉ 25 ms.
  - Sửa 1: gỡ nén và giải mã đỉnh chạy ở luồng nền ngay sau khi vào khu sinh học (4 giây sau khi cổng nạp, `Bp3dReader.CoPrewarm`); luồng chính chỉ dựng Mesh chia theo khung hình. Đo: nạp trước 0,3 s, khung hình tệ nhất 25 ms.
  - Sửa 2: màn hình "Đang tải mô hình giải phẫu 3D" phủ kín màn hình, hiện trước khi dựng phòng, che khoảng khựng của khung vẽ đầu tiên (`GiaiPhauDangTai`). Sau khi tắt màn hình tải, khung hình tệ nhất 17 ms (trước là 664 ms).
