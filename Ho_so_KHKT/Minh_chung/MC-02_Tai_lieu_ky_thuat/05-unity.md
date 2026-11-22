# Phần Unity

Project: `Phong thi nghiem ao/` (Unity 6000.6.7f1, URP). Script nằm ở `Phong thi nghiem ao/Assets/Scripts/`. Đã biên dịch bằng chính trình biên dịch và tham số của project (cả nhánh .NET Standard lẫn nhánh cổng COM .NET Framework): không lỗi, không cảnh báo. Cảnh phòng lab và chế độ demo đã chạy thử tự động trong Unity (vào Play, làm trọn một lượt chuẩn độ, chụp ảnh). Tay cầm thật và kính VR chưa gắn vào cảnh.

## Chạy demo

1. Unity Hub → mở project **Phong thi nghiem ao** (Unity 6000.6.7f1). Cảnh `PhongThiNghiem` tự mở; nếu không: cửa sổ Project → `Assets/Scenes` → bấm đúp `PhongThiNghiem`.
2. Bấm **▶ Play**, bấm chuột vào khung Game. Người xem ngồi ở bàn 1; bàn tay ảo (găng xanh) đi theo con trỏ chuột.

Giao diện ít chữ: thanh 7 bước ở trên, một dòng gợi ý thao tác dạng phím ở dưới (chỉ hiện việc làm được ngay lúc đó), card kính mờ nổi cạnh dụng cụ đang trỏ. Bấm **H** để xem bảng phím đầy đủ.

| Thao tác | Việc |
| --- | --- |
| Trỏ chuột vào dụng cụ | Dụng cụ sáng viền, card hiện tên, trạng thái (đang chứa gì, bao nhiêu), công dụng |
| Nhấn chuột trái | Cầm dụng cụ lên / đặt vào chỗ sáng vòng (kẹp buret, ô dưới buret) / đặt xuống mặt bàn |
| Giữ chuột trái (đang cầm chai, cốc) | Rót vào vật đang trỏ |
| Giữ chuột trái (cầm pipet, trỏ cốc HCl) | Hút; nhả là ngừng. **Shift + giữ**: xả bớt về vạch |
| Giữ chuột trái (cầm pipet, trỏ bình nón) | Xả vào bình |
| Nhấn chuột trái (cầm lọ phenolphtalein) | Nhỏ 1 giọt vào vật đang trỏ |
| Giữ chuột trái trên bình nón dưới buret | Lắc bình (nhấn nhanh là cầm lên) |
| Lăn chuột trên khóa buret, ↑ ↓, Space | Mở / đóng khóa, tinh chỉnh, khóa hẳn |
| Q | Trả dụng cụ đang cầm về chỗ cũ |
| Z / X | Hạ / nâng tầm mắt cho ngang mặt khum |
| Enter | Ghi số đọc buret (lần 1: V đầu, lần 2: V cuối) |
| Chuột phải + rê, W A S D, C, 1 – 7 | Nhìn, đi, ngồi/đứng, đến các trạm |
| G / R / H | Đồ thị pH / làm lượt mới / bảng phím |

### Bài chuẩn độ HCl bằng NaOH 0,1 M (tự làm từ đầu)

Lúc bắt đầu, buret và pipet nằm trên giá đỡ, bình nón rỗng, cốc HCl có **nồng độ ngẫu nhiên 0,08–0,12 M** mỗi lượt. Mọi thao tác chạy trên một mô hình chất lỏng chung (số mol HCl, NaOH, nước, chất chỉ thị trong từng dụng cụ), nên làm sai sẽ ra hậu quả đúng logic.

1. **Lắp buret:** cầm buret, đặt vào kẹp trên giá sắt (vòng sáng chỉ chỗ đặt).
2. **Nạp NaOH:** cầm chai NaOH, trỏ vào miệng buret, giữ để rót tới trên vạch 0. Buret chưa lắp lên giá thì không rót được. Rót quá đầy thì tràn.
3. **Chỉnh về vạch 0 và đọc V đầu:** đặt cốc thải dưới buret, mở khóa xả tới gần vạch 0, khóa lại, cất cốc thải. Hạ/nâng mắt (Z/X) cho ngang mặt khum; kính lúp đặt trên đường mắt → mặt khum nên có đúng sai số thị sai. Nhấn Enter. Nếu xả vào bình nón thay vì cốc thải, NaOH lẫn vào trước khi đọc V đầu và bị ghi lỗi.
4. **Lấy 25,00 ml HCl:** cầm pipet bầu, trỏ cốc HCl, giữ để hút quá vạch một chút (card báo "trên vạch … mm"), Shift + giữ để xả bớt cho mặt khum chạm **vòng vạch trắng**. Trỏ bình nón, giữ để xả hết. Pipet còn dung dịch thì không đặt xuống được.
5. **Chỉ thị:** cầm lọ phenolphtalein, trỏ bình nón, nhấn 2–3 lần. Quên bước này thì đến điểm tương đương vẫn không đổi màu.
6. **Chuẩn độ:** đặt bình nón lên ô dưới buret, mở khóa, giữ chuột trên bình để lắc. Gần điểm tương đương nhỏ từng giọt; hồng nhạt bền 30 giây thì khóa.
7. **Đọc V cuối** (Enter). Card kết quả: C(HCl) tính được so với thật, sai số %, đồ thị pH, và các nguồn sai số cụ thể (pipet lấy thiếu/thừa bao nhiêu ml, nhỏ quá mấy giọt, đọc lệch mắt, lắc chưa đều, quên chỉ thị, NaOH lẫn vào trước V đầu).

Kết quả chạy thử tự động cả quy trình bằng bàn tay ảo: pipet lấy 24,98 ml, điểm tương đương 25,77 ml, đo được 25,90 ml, sai số 0,39 %. Ảnh từng bước: [docs/anh-demo/](anh-demo/).

## Cảnh phòng thí nghiệm

Cảnh `Assets/Scenes/PhongThiNghiem.unity` được **dựng bằng code** (`Assets/Scripts/Editor/LabBuilder/`), không cần mô hình ngoài. Ảnh chụp từ cảnh thật: [docs/anh-phong-lab/](anh-phong-lab/).

**Dựng lại:** menu **KHKT → Dựng phòng thí nghiệm**. Lệnh này ghi đè cảnh trên, nên mọi chỉnh tay trong cảnh sẽ mất; muốn đổi bố cục thì sửa code builder rồi dựng lại, hoặc chỉnh tay xong thì đừng dựng lại nữa.

### Mặt bằng (tầng 2, gốc tọa độ ở góc tây nam phòng lab, trần cao 3,4 m)

```
 z=9,8 ┌───────────────────────── BAN CÔNG (lan can, chậu cây, ghế, đèn) ───────────────────────┐
 z=8   ├─cửa sổ──cửa sổ──cửa sổ──[cửa kính lùa]─────────┬────cửa sổ─────────────────────────────┤
       │ bảng tuần hoàn                                 │  quạt hút          kệ "dung dịch chuẩn" │
       │ BẢNG     bàn GV      bàn 2   bàn 4   bàn 6    cửa                   kệ "oxit - kim loại"│
       │ TRẮNG    (bồn rửa,   (3 ghế đẩu mỗi bàn,  khu an toàn  bàn pha chế    kệ "chất chỉ thị"  │
       │          laptop)      bồn rửa, giá hóa chất) (vòi rửa mắt, (cân)     kệ "bazơ"          │
       │          bàn 1★ bàn 3   bàn 5    bình cứu hỏa, tủ dụng cụ thủy tinh  kệ "muối"          │
       │ tủ hút                       bồn rửa chung + giá treo dụng cụ                            │
 z=0   └─cửa sổ──────cửa sổ──[giá treo áo]─[cửa ra vào]─┴──tủ axit──tủ dễ cháy──────────────────┘
      x=0                                              x=12                                  x=16
                   PHÒNG THÍ NGHIỆM 12 × 8 m                      PHÒNG HÓA CHẤT 4 × 8 m
```

★ Bàn 1 có sẵn bộ chuẩn độ (giá sắt, buret 50 ml, bình nón 250 ml trên gạch men trắng, cốc NaOH, bình tia). Các đối tượng mang tên cố định (`Buret`, `Khóa buret`, `Bình nón`, `Cốc NaOH`) để gắn `BuretStopcock`, `TitrationFlask`... ở bước sau.

| Khu | Có gì |
| --- | --- |
| Phòng thí nghiệm | 6 bàn thí nghiệm mặt epoxy đen (bồn rửa inox, vòi nước, giá hóa chất, giá ống nghiệm, chỗ để chân khi ngồi), 18 ghế đẩu, bàn giáo viên + ghế, tủ hút khí độc, bảng trắng, bảng tuần hoàn, đồng hồ, 10 máng đèn tuýp, 4 quạt trần quay được, bồn rửa chung + giá treo dụng cụ thủy tinh, giá treo áo blouse, khu an toàn (vòi rửa mắt, 2 bình chữa cháy, hộp y tế, xô cát), thùng chất thải hóa học |
| Phòng hóa chất | 5 kệ sắt theo nhóm (muối, bazơ, chất chỉ thị, oxit - kim loại, dung dịch chuẩn), tủ axit có ống thông gió, tủ chất dễ cháy, tủ kính dụng cụ thủy tinh, bàn pha chế có bồn rửa và cân, quạt hút, bình chữa cháy |
| Ban công | Lan can (có va chạm, không rơi xuống được), cột, chậu cây, ghế băng, đèn ốp trần, cửa kính lùa vào lớp |
| Ngoài trời | Sân trường, cỏ, cây, cột cờ đỏ sao vàng, dãy nhà đối diện, mặt ngoài tòa nhà màu vàng |

### Lưu ý kỹ thuật

- **Kích thước theo thật**: bàn cao 0,8 m, ghế 0,5 m, tầm mắt ngồi 1,25 m, đứng 1,6 m, trần 3,4 m. Trong VR, sai tỉ lệ là thứ học sinh nhận ra ngay.
- **Chữ trên biển** dùng shader riêng `Khkt/ChuTrongCanh` (`Assets/Lab/Shaders`) để bị tường che như vật thật; shader chữ mặc định của Unity vẽ xuyên tường.
- **Hiệu năng**: khoảng 1.900 đối tượng, 1.700 renderer, 15 đèn điểm (không đổ bóng) + mặt trời có bóng; vật tĩnh đã đánh dấu static để gộp lô vẽ. Đủ nhẹ cho laptop có card rời. Nếu sau này chạy thẳng trên điện thoại (phương án B), cần giảm đèn điểm, tắt bóng mặt trời và gộp lưới (mesh combine).
- Vật liệu, lưới tròn xoay và ảnh vân sinh ra nằm ở `Assets/Lab/{Materials,Meshes,Textures}`; dựng lại sẽ ghi đè.

## Cài đặt project còn thiếu

1. Package Manager: cài **XR Plugin Management**, **OpenXR**, **XR Interaction Toolkit 3.x** (kèm sample *Starter Assets*). Input System đã có sẵn.
2. Project Settings:
   - *XR Plug-in Management → Windows*: bật **OpenXR**. SteamVR (chạy cùng PhoneVR hoặc VRidge) là OpenXR runtime.
   - *Player → Other Settings → Api Compatibility Level*: đổi từ .NET Standard sang **.NET Framework** để có `System.IO.Ports` cho cổng COM. Chưa đổi thì `SerialControllerSource` chỉ báo lỗi trong Console, phần còn lại vẫn chạy.
   - *Player → Active Input Handling*: đang là **Input System Package**, giữ nguyên.
3. Chưa có kính và tay cầm? Vẫn lập trình được: dùng `KeyboardMouseControllerSource` thay cho `SerialControllerSource` và XR Device Simulator của XRI.

## Dựng cảnh tối thiểu

```
XR Origin (XR Rig)
└─ Camera Offset              ← EyeHeightController
   ├─ Main Camera             (Tracked Pose Driver: chỉ lấy rotation, vì kính 3DoF)
   └─ Tay phải                ← ControllerPoseDriver, XR Ray Interactor (hoặc Near-Far Interactor)
Hệ thống tay cầm              ← SerialControllerSource (hoặc KeyboardMouseControllerSource),
                                KhktControllerFeeder, HapticFeedback, MotionRecorder
Bàn chuẩn độ
├─ Buret                      ← BuretStopcock, MeniscusReading
│  └─ Khóa                    ← XR Simple Interactable: Select Entered → BuretStopcock.BeginTurn,
│                                                        Select Exited  → BuretStopcock.EndTurn
└─ Bình nón                   ← TitrationFlask, SwirlDetector, HeldTool, XR Grab Interactable
                                (Select Entered → HeldTool.Grab, Select Exited → HeldTool.Release),
                                Collider để nhận giọt
```

## Nối tay cầm với XR Interaction Toolkit

`KhktControllerFeeder` tạo một thiết bị Input System tên **KhktController**. Trong Input Actions của XRI (ví dụ *XRI Default Input Actions* của Starter Assets), thêm binding:

| Action (XRI Right Interaction / Locomotion) | Binding |
| --- | --- |
| Select | `<KhktController>/trigger` |
| Activate | `<KhktController>/fineClick` |
| Teleport Mode / Move | `<KhktController>/move` |
| Turn (Snap Turn) | `<KhktController>/move/x` |

Vị trí và hướng tay do `ControllerPoseDriver` đặt trực tiếp lên Transform, nên **tắt** Tracked Pose Driver trên GameObject tay.

Locomotion nên dùng: **Teleportation Provider** + **Snap Turn Provider** (45°), tắt Continuous Move theo mặc định, bật **Tunneling Vignette** nếu có. Lý do: [docs/01-de-xuat-dieu-khien.md](../docs/01-de-xuat-dieu-khien.md).

## Script

| Thư mục | Script | Việc |
| --- | --- | --- |
| Input | `ControllerSource` | Lớp cha chung cho mọi nguồn tay cầm; sự kiện từng mẫu 100 Hz |
| | `SerialControllerSource` | Đọc COM trên luồng riêng, lọc Mahony, nhịp tim, tự kết nối lại |
| | `KeyboardMouseControllerSource` | Giả lập tay cầm, cũng là "chế độ laptop" |
| | `PacketParser`, `MahonyFilter`, `ControllerState` | Giải mã gói, tính hướng, đổi hệ trục |
| | `ControllerPoseDriver` | Arm model + căn lại yaw; vị trí webcam nếu có |
| | `KhktController`, `KhktControllerFeeder` | Thiết bị Input System cho XRI |
| | `HapticFeedback` | Bảng mẫu rung/đèn |
| | `UdpPoseReceiver` | (Thử nghiệm) nhận vị trí từ `tools/webcam_tracker.py` |
| Interaction | `PourableContainer` | Rót theo góc nghiêng |
| | `BuretStopcock` | Vặn cổ tay mở khóa, nhỏ từng giọt, rung mỗi giọt |
| | `SwirlDetector` | Nhận biết lắc tròn bình nón |
| | `EyeHeightController` | Mô hình cổ + cúi xuống bằng joystick |
| | `MeniscusReading` | Số đọc buret có sai số thị sai theo vị trí mắt |
| Simulation | `TitrationModel`, `TitrationFlask` | pH, chỉ thị, trộn, nhiệt |
| Recording | `MotionRecorder` | CSV 100 Hz cho `khkt/skill.py` |
| Environment | `CeilingFan` | Quay cánh quạt trần, quạt hút (bật/tắt, tốc độ) |
| | `LabStation` | Đánh dấu trạm dịch chuyển (sau này làm Teleportation Anchor) |
| | `DesktopViewer` | Đi xem cảnh bằng chuột-bàn phím |
| | `WorldTextMaterial` | Gắn ảnh phông chữ cho vật liệu chữ dùng chung |
| | `LiquidFill` | Khối chất lỏng trong bình tròn xoay, mực dâng đúng hình dạng bình |
| | `LabContainer` | Vật chứa dung dịch (bình, cốc, chai, buret, pipet): thêm, lấy, trộn chậm khi chưa lắc, màu chỉ thị, nhiệt |
| | `Pipette`, `Dropper`, `WashBottle` | Pipet bầu (hút, xả, vạch định mức), lọ nhỏ giọt chỉ thị, bình tia nước cất |
| | `Grabbable`, `PlaceSocket`, `PlaceSurface`, `StopcockHandle` | Cầm/đặt dụng cụ, chỗ đặt định vị (kẹp buret, ô dưới buret), mặt bàn, vùng vặn khóa |
| Simulation | `Solution` | Thành phần dung dịch (thể tích, mol HCl, mol NaOH, giọt chỉ thị), pH, độ hồng |
| Demo | `LabProcedure` | Quy trình 7 bước theo trạng thái thật của dụng cụ, ghi số đọc, kết quả và nguồn sai số |
| | `HandController`, `HandRig` | Bàn tay ảo găng xanh theo chuột: cầm, đặt, rót, hút, xả, nhỏ giọt, lắc, gợi ý thao tác |
| | `ModernUI`, `GlassKit` | Giao diện kính mờ: thanh bước, gợi ý phím, card dụng cụ, kính lúp, số liệu, kết quả, đồ thị |
| | `HoverOutline` | Viền sáng dụng cụ đang trỏ (shader `Khkt/VienSang`) |
| | `LoupeCamera` | Camera kính lúp trên đường mắt → mặt khum (tái hiện thị sai) |
| | `BuretVisual`, `FlaskSwirlVisual` | Cột dung dịch, mặt khum, giọt rơi, dòng chảy; bình nón lắc tròn |
| | `EquipmentInfo`, `ToggleEffect`, `FlameFlicker` | Thẻ thông tin dụng cụ; bật/tắt lửa, nước, đèn, quạt |
| Editor/LabBuilder | `LabSceneBuilder` (+ `.Rooms`, `.Demo`), `LabKit` | Dựng toàn bộ cảnh phòng thí nghiệm và gắn demo (menu KHKT) |
