# Hợp đồng kỹ thuật cho người tham gia mới (tầng 2: lab sinh học, phòng robot, máy chủ nhiều người)

Tài liệu này dành cho Claude Code của thành viên mới. Đọc hết trước khi viết code. **"Phải"** là bắt buộc; **"Không"** là cấm.

## 1. Nhiệm vụ

1. **Lab sinh học tầng 2.** Dựng bằng code, cùng phong cách tầng 1, có thí nghiệm chạy được.
2. **Phòng robot tầng 1: bàn lập trình khối lệnh** (mục 5). Đây là phần duy nhất còn lại của kế hoạch máy phân tích.
3. **Máy chủ nhiều người.** Nhiều học sinh cùng vào một phòng lab, thấy nhau và làm thí nghiệm chung.
4. **Chỉ góc nhìn thứ nhất (FPS).**
   - Không làm góc nhìn thứ ba, camera bay tự do, camera khán giả hay bản đồ nhìn từ trên để chơi.
   - Mọi thao tác đều qua mắt và tay của chính người chơi.
   - Ngoại lệ duy nhất đã có sẵn: màn hình vận hành máy toàn màn hình (`InstrumentScreen`) và giao diện lắp ráp (`FabricationWorkspace`). Chúng là giao diện 2D của máy, không phải góc nhìn.

## 2. Môi trường (máy Mac)

- **Unity:**
  - đúng **6000.6.7f1**, URP 17.6.0, Input System 1.20.1;
  - không nâng cấp Unity hoặc gói nếu chưa thống nhất;
  - cài thêm mô-đun Mac Build Support (Mono) qua Unity Hub để build cho macOS.
- **Python 3.11+:** `pip install -r requirements.txt`. Máy chủ AI / web là FastAPI (`server/ai/main.py`).
- **Kiểm thử bộ máy hóa học (C#, không cần Unity):** `tools/chay_kiem_thu_hoa.ps1` là PowerShell.
  - Trên Mac: cài `pwsh`, hoặc chạy `dotnet` biên dịch `Assets/Scripts/Simulation/Chem/*.cs` + `tests/ChemEngineTests.cs` (xem lệnh trong file .ps1).
  - Phải đặt biến `KHKT_ROOT` trỏ về gốc dự án.
- **Đường dẫn:**
  - dùng `Path.Combine` / `Application.dataPath`; không viết cứng `C:\`, `D:\`, dấu `\`;
  - tên tệp, thư mục mới viết không dấu, không cách (`phong_sinh`, `may_pcr`).

## 3. Kiến trúc phải tuân theo

| Phần | Vị trí | Quy tắc |
|---|---|---|
| Cảnh 3D | `Assets/Scripts/Editor/LabBuilder/` | **Toàn bộ cảnh dựng bằng code** (menu KHKT → Dựng phòng thí nghiệm). Không chỉnh tay trong Scene rồi lưu. Tầng 2 viết trong file mới `LabSceneBuilder.Floor2.cs` (+ file con nếu dài), dùng lại `LabKit` (`Box`, `Lathe`, `Label`…), vật liệu và bảng màu trong `LabSceneBuilder.Style.cs` |
| Khu tải / gỡ | `Rendering/ZoneMap.cs`, `ZoneStreamer.cs` | Mỗi phòng tầng 2 là **một khu mới, thêm vào cuối** `ZoneMap.Zones`. **Không đổi chỉ số 0 – 4** của tầng 1. `ZoneMap.At` hiện chỉ xét x, z: phải thêm điều kiện độ cao (y ≥ 3,6 → tầng 2) mà không làm sai tầng 1 |
| Người xem | `Environment/DesktopViewer.cs`, `Demo/HandController.cs` | Một bộ camera góc nhìn thứ nhất duy nhất cho người chơi cục bộ. Trạm dịch chuyển `LabStation`: tầng 2 dùng số thứ tự **từ 20 trở lên** |
| Tương tác | `Interaction/*` | Dùng lại `Grabbable`, `PlaceSocket`, `PlaceSurface`, `LabContainer`, `Pipette`, `Centrifuge`, `HeatSource`… Vật bấm được thì cài `IDemoUsable` (`Use()`, `UseHint`) |
| Mô phỏng hóa học | `Simulation/Chem/` (`ChemEngine`, `Mix`, `SpeciesRegistry`) | Dung dịch, pH, phản ứng hóa học (H₂O₂ + catalase, thuốc thử…) **phải đi qua bộ máy này**, không tự tính song song |
| Mô phỏng sinh học | **mới**: `Simulation/Bio/` (namespace `Khkt.Simulation.Bio`) | C# thuần, **không phụ thuộc UnityEngine**, để kiểm thử ngoài Unity như `Chem` |
| Máy có màn hình vận hành | `Demo/Instruments/` | Kế thừa `InstrumentProgram`, mở bằng `InstrumentScreen.Open`. Có quy trình Hướng dẫn / Thực hành; sơ đồ bên trong vẽ bằng `Painter`. Hồ sơ máy là JSON trong `data/may/<ma>.json`, **đúng khóa** như các máy hiện có (`ma`, `ten`, `may_mau`, `nguyen_ly`, `bo_phan`, `thong_so`, `quy_trinh`, `an_toan`, `thuat_ngu`, `cau_hoi_on_tap`). Chép sang Unity bằng `tools/chep_du_lieu_may.py` |
| Đầu vào | `Input/` (`ControllerState`, `ControllerSource`, `KeyboardMouseControllerSource`) | Mọi điều khiển đi qua lớp trừu tượng này, để chuột – bàn phím và tay cầm dùng chung. Không đọc phím trực tiếp rải rác (trừ phím tắt giao diện) |
| AI | `server/ai/` | Thêm máy tầng 2 vào `data/may` thì `server/ai/machines.py` tự đưa quy trình vào ngữ cảnh AI. Thêm từ khóa vào `ALIASES` nếu cần. Khóa API chỉ ở máy chủ |
| Web | `server/web/index.html`, `server/lab/` | Giữ phong cách sẵn có; thêm tab, không viết lại |

**Tọa độ** (mét, trục y hướng lên):
- tầng 1 nằm trong x 0 → 27, z −9 → 15; trần cao 3,6 m (`H` trong `LabSceneBuilder.cs`);
- **tầng 2 đặt sàn ở y = 3,8**, cùng khung nhà;
- cầu thang và lỗ sàn: đề xuất vị trí trước khi sửa cảnh tầng 1;
- không đặt gì ở y < 0: chỗ đó dành cho màn hình máy (y −500) và xưởng lắp ráp ảo.

## 4. Lab sinh học tầng 2: yêu cầu

**Phòng** (mỗi phòng một khu):
- phòng thí nghiệm sinh học chung (bàn học sinh, kính hiển vi);
- phòng vi sinh, nuôi cấy (tủ an toàn sinh học, tủ ấm, nồi hấp);
- phòng sinh học phân tử (PCR, điện di);
- kho mẫu, hóa chất sinh học (tủ lạnh 4 °C, tủ đông −20 °C);
- hành lang tầng 2 nối cầu thang.

**Thiết bị tối thiểu:**

| Thiết bị | Ghi chú |
|---|---|
| Kính hiển vi quang học | Vật kính 4×, 10×, 40×, 100× (dầu); lấy nét thô / tinh; ảnh tiêu bản vẽ bằng code |
| Tủ an toàn sinh học cấp II | Luồng khí; sai thao tác thì nhiễm mẫu |
| Tủ ấm 37 °C | Khuẩn lạc mọc theo thời gian |
| Nồi hấp | Dùng lại mô hình `AutoclaveProgram` / `SteamTable` |
| Máy ly tâm | Dùng lại `Centrifuge` |
| Máy PCR | Chu kỳ nhiệt |
| Điện di gel, bàn soi UV / chụp gel | |
| Máy đo OD600 | Dùng lại khung UV-Vis |
| Pipet tự động | Dùng lại `Pipette` |
| Cân, máy đo pH, bể ổn nhiệt, đèn cồn, que cấy, đĩa petri | |

**Thí nghiệm theo chương trình Sinh học 10 – 12 (Việt Nam):**
- quan sát tế bào hành, khí khổng;
- co và phản co nguyên sinh;
- enzyme catalase phân hủy H₂O₂ (qua `ChemEngine`);
- lên men rượu, lên men lactic;
- quang hợp thải O₂;
- tách chiết DNA;
- nuôi cấy và đếm vi sinh vật.

**Quy tắc nội dung:**
- **Số liệu phải có nguồn:** ghi URL trong JSON. Phần ước lượng ghi `"mo_phong": true`. Ưu tiên nguồn tiếng Việt (SGK, TCVN, giáo trình đại học).
- **An toàn sinh học:** không hướng dẫn nuôi, tạo tác nhân gây bệnh. Vi sinh vật chỉ dùng chủng an toàn dạy học (E. coli K-12, nấm men…). AI phải từ chối nhẹ nhàng các yêu cầu nguy hiểm.

## 5. Phòng robot: bàn lập trình khối lệnh

**Vị trí:** khu `phong_robot` có sẵn (x 13,5 → 27, z −9 → −2,2). Cobot, máy hút nhả, máy in 3D giữ trưng bày, chỉ thêm thẻ nguyên lý (cobot: ISO/TS 15066).

**Giao diện:**
- bấm vào bàn thì mở giao diện kéo thả khối toàn màn hình, giống `InstrumentScreen` / `FabricationWorkspace`;
- dùng `DesktopViewer.UiCapture` để thả con trỏ, thoát thì về đúng góc nhìn thứ nhất.

**Khối:**
- mỗi kỹ năng robot có sẵn là một khối: `di_toi`, `cam`, `lay_hoa_chat`, `rot`, `lac`, `doi`, `dun_nong`, `khuay`, `do_ph`, `can`, `ly_tam`, `phan_tich`… (danh sách đầy đủ trong `SKILLS` của `server/ai/robot_plan.py` và `switch` trong `Demo/LabRobot.Skills.cs`);
- khối điều khiển: lặp N lần, chờ, nếu – thì theo số đo cảm biến (ví dụ pH < 7);
- ô tham số liệt kê vật có thật gần đó, lấy mã từ `RobotWorld` (`ong_nghiem_1`, `hoa_chat:253`…).

**Chạy:**
- chương trình khối chuyển thành `List<RobotStep>` (`ky_nang`, `doi_tuong`, `dich`, `so_luong`) rồi gọi `LabRobot.ExecutePlan`. Không viết bộ chạy robot thứ hai;
- trước khi chạy phải qua bộ kiểm tra an toàn giống `validate()` trong `robot_plan.py`, **viết lại bằng C#** để chạy khi không có mạng;
- khối đang chạy được tô sáng; lỗi hiện ngay trên khối hỏng;
- lưu / mở chương trình ra JSON.

**Khối lặp, điều kiện:** mở rộng tối thiểu `ExecutePlan` (hoặc bộ chạy bọc ngoài gọi từng bước). Không đổi tên kỹ năng hiện có vì AI và web đang dùng.

**Kiểm thử:** thêm chế độ `KHKT_TEST=robot_khoi`: dựng một chương trình (lấy HCl, rót, đo pH, nếu pH < 7 thì báo) và chạy hết; chương trình sai (vật không tồn tại, rót vào lọ hóa chất gốc) phải bị chặn.

## 6. Máy chủ nhiều người

- **Không phá chế độ một người:** chế độ mặc định vẫn chạy offline. Nhiều người là tùy chọn: kết nối khi có máy chủ.
- **Đề xuất kiến trúc:**
  - Netcode for GameObjects + Unity Transport;
  - máy chủ chuyên dụng chạy bản build headless (`-batchmode -nographics`), có quyền quyết định (server-authoritative): **chỉ máy chủ chạy `ChemEngine` / `Bio`**;
  - máy khách gửi ý định (cầm, rót, bấm máy), nhận lại trạng thái.
- **Đồng bộ:**
  - vị trí, quay của vật đang cầm và avatar: 10 – 20 lần/giây;
  - trạng thái dung dịch (`LabContainer`: thể tích, thành phần rút gọn, nhiệt độ, màu): khi đổi, tối đa 5 lần/giây;
  - mỗi vật chỉ một người cầm tại một thời điểm (khóa sở hữu).
- **Mỗi người là một avatar góc nhìn thứ nhất:**
  - người khác thấy thân và tay;
  - tên học sinh nổi trên đầu;
  - camera chỉ của người cục bộ.
- **Robot trợ lý và AI:** chạy phía máy chủ; trả lời đúng người hỏi.
- **Tài khoản, phòng:** dùng / mở rộng FastAPI hiện có (`server/lab`). Không lưu mật khẩu dạng thô; khóa API không bao giờ gửi xuống máy khách.
- **Tải / gỡ khu (`ZoneStreamer`):** chỉ phía máy khách theo vị trí người chơi đó. Máy chủ giữ toàn bộ trạng thái thế giới (`WorldState`).

## 7. Phần cứng (tương thích)

- **Tay cầm hiện tại là V2 "Halo"** ([docs/15](docs/15-tay-cam-v2-halo.md)): ESP32 DevKitC 38 chân + BMI160 + joystick KY-023 + cò Hall SS49E + vòng halo cho webcam (OpenCV).
  - Firmware chưa viết.
  - Không dùng tài liệu tay cầm cũ (Mega, MPU6050, ESP32-CAM).
- **Mọi thiết bị đầu vào mới:** viết một `ControllerSource` mới, xuất ra `ControllerState`.
- **Cổng mạng đã dùng:**
  - UDP 9051 (điều khiển bằng đầu qua webcam, `Input/HeadTracking.cs` + `tools/head_tracker.py`, chỉ localhost);
  - UDP 4210 (tay cầm cũ);
  - HTTP 8000 (máy chủ AI / web).
  - Máy chủ nhiều người dùng cổng khác, ghi vào tài liệu.
- **macOS:**
  - cổng nối tiếp là `/dev/cu.*` (`SerialControllerSource` đang dò theo tên COM của Windows, cần thêm nhánh cho Mac);
  - webcam cần cấp quyền Camera cho Unity / Terminal.

## 8. Quy ước code và tài liệu

- **Tiếng Việt** cho giao diện, chú thích code, tài liệu, thông báo. Số thập phân dùng dấu phẩy khi hiển thị (`CultureInfo("vi-VN")`).
- **Namespace** `Khkt.*`. Theo cách đặt tên, mật độ chú thích của code xung quanh.
- **Không sửa hành vi tầng 1** (hóa học, 14 máy phân tích, lắp ráp, robot). Cần sửa file dùng chung (`ZoneMap`, `DesktopViewer`, `HandController`, `LabContainer`, `LabSceneBuilder.cs`) thì sửa tối thiểu và ghi rõ trong PR.
- **Hiệu năng:** máy mục tiêu là laptop Intel Iris Xe, ≥ 60 FPS ở phòng chính.
  - Dùng lại tải / gỡ khu, occlusion, HLOD sẵn có.
  - Màn hình, texture động chỉ vẽ lại khi đổi.
- **Tài liệu:** mỗi phần lớn có một file `docs/NN-ten.md` (số tiếp theo sau số lớn nhất hiện có). Ghi nguồn số liệu, kết quả kiểm thử, ảnh chụp.

## 9. Kiểm thử bắt buộc trước khi gộp

| Kiểm thử | Lệnh / vị trí |
|---|---|
| Python | `python -m unittest discover -s tests` (hiện 105+ bài, phải đạt hết) |
| Bộ máy hóa học / sinh học (C#) | `tests/ChemEngineTests.cs` (hiện 140 bài). Thêm bài cho `Simulation/Bio` theo cùng kiểu (`Check`, `Near`) |
| Dữ liệu máy | `tests/test_du_lieu_may.py`: mọi `data/may/*.json` đủ khóa, mọi thông số có `nguon` https hoặc `mo_phong` |
| Unity tự động | `tools/kiem_thu_unity/KhktAutoTest.cs`: thêm chế độ (ví dụ `KHKT_TEST=tang2`) làm như học sinh, ghi log `[KHKT-PLAY]`, chụp ảnh |
| Hồi quy | Các chế độ cũ `may`, `may2`, `may3`, `may4`, `khu` và bài đầy đủ vẫn phải đạt |

## 10. Git

- Kho: `https://github.com/tronggenz-alt/KHKT2027` (riêng tư), nhánh chính `main`.
- **Làm trên nhánh riêng** (`tang2/...`, `robot/...`, `mang/...`), mở PR, không đẩy thẳng `main`.
- **Build xong là đẩy lên GitHub ngay (bắt buộc).** Mỗi khi xong một phần, ví dụ một phòng, một thiết bị hoặc một tính năng mạng, và kiểm thử ở mục 9 đã đạt:
  1. Commit ngay.
  2. `git push` lên kho dự án, vào nhánh đang làm.
  3. Mở PR hoặc cập nhật PR đang có. PR ghi phần đã làm, kết quả kiểm thử và ảnh chụp.

  Không để việc đã xong nằm lại trên máy Mac qua ngày.
- **Lấy thay đổi mới của tầng 1:** `git fetch origin` rồi `git merge origin/main` vào nhánh của mình. Không dùng rebase trên nhánh đã đẩy. Có xung đột ở file dùng chung thì giữ hành vi tầng 1 và ghi rõ trong PR.

**An toàn kho (bắt buộc):**
- **Cấm** `git push --force` / `--force-with-lease`, `git reset --hard` trên nhánh đã đẩy, xóa nhánh của người khác, sửa lịch sử `main`, đổi cài đặt kho GitHub.
- **Không commit file cảnh sinh ra:** `Assets/Scenes/**/*.unity`, `OcclusionCullingData.asset`. Cảnh dựng lại từ code sau khi gộp, nên commit vào chỉ gây xung đột khổng lồ. Vẫn phải commit file `.meta` của script / dữ liệu mới.
- Mỗi PR nhỏ, một chủ đề. Không gom định dạng lại hay đổi tên hàng loạt file tầng 1.
- Lỡ đẩy khóa API hay mật khẩu: báo chủ dự án **ngay**. Khóa đó coi như lộ và phải thu hồi. Xóa commit không đủ.
- Trên Mac đặt `git config core.autocrlf input` để không đổi kiểu xuống dòng của cả kho.
- Claude Code bên kia chỉ được chạy `git push` vào nhánh của mình. Không tự gộp PR.
- Không đưa lên kho: khóa API, `.env`, `Library/`, `Temp/`, bản build, tệp lớn hơn 50 MB.
- Ảnh trong tài liệu chỉ dùng ảnh tự chụp từ dự án hoặc ảnh phạm vi công cộng.
