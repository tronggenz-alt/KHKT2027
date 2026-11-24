# Tay cầm VERA trong app chính (G2)

Cập nhật 12/11/2026. Tay cầm VERA (firmware `firmware/tay_cam_vera`, docs/28) điều khiển app Unity. Tia trỏ đi theo hướng tay cầm, có hút mục tiêu. Hai nút cảm ứng dùng theo bản đồ tương tác trong `docs/tay-cam-tich-hop`.

## 1. Nối tay cầm

| Đường nối | Gói | Cách nhận |
| --- | --- | --- |
| USB (cổng COM) | gói thô `A5 5D`, 105 byte, 50 gói/s | `SerialControllerSource` tự dò cổng và gửi lệnh `r2`. Nếu tay cầm im quá 2 s thì gửi lại |
| Wi-Fi (UDP 4210) | gói v3 `A5 5C`, 37 byte, 100 gói/s | `UdpControllerSource`. Khi đã có gói v3 thì bỏ gói cũ 29 byte firmware gửi kèm |

- Hướng tay cầm do mạch tính sẵn (có chống trôi yaw và bù nhiệt), nên máy tính không chạy Mahony.
- Mở cổng USB làm ESP32 khởi động lại. Để tay cầm nằm yên 3 giây cho mạch cân con quay.
- Mã liên quan:
  - `Input/VeraPacket.cs`: giải mã gói.
  - `PacketParser`: nhận cả 3 loại gói `5A`, `5C`, `5D`.
  - `ControllerPackets.FromVera`: đổi mẫu VERA sang `ControllerState`. Trạng thái có thêm `Vera`, `TouchA/B`, `Flags`, `TempC`.

## 2. Tia trỏ (`Input/VeraAim.cs`, `Input/VeraPointer.cs`)

- **Nguồn gốc:** chép 1:1 từ phòng thử (`tools/phong_thu/xu_ly.py`, `ConTro`).
- **Yaw:** tia cộng dồn độ đổi hướng của mũi tay cầm và bị kẹp trong khung nhìn, giống con trỏ chuột chạm mép màn hình. Nhờ vậy yaw có trôi cũng không làm lệch tia mãi.
- **Pitch:** lấy trực tiếp từ hướng tay cầm (gia tốc kế giữ đúng).
- **Làm mượt:** lọc One Euro (fc 1 Hz, β 0,05). Đứng yên thì hết run tay, lia nhanh thì không trễ.
- **Bù trễ:** dự đoán thêm 12 ms cộng với tuổi của mẫu cuối.
- **Gốc tia:** đi từ mắt người chơi theo hướng thân.
- **Ưu tiên tia:** tia tay cầm thắng tia bàn tay webcam khi người chơi đang dùng tay cầm. Động chuột thì chuột lấy lại quyền trỏ.
- **Chế độ tinh:** đang cầm vật mà giữ A thì tia đi chậm 4 lần.
- **Căn lại:** giữ A + B 1 giây (khi không cầm vật) để tia về giữa.
- **F6, căn trục gắn BMI160:**
  1. Đặt tay cầm nằm ngang và để yên.
  2. Chúc mũi xuống khoảng 45° và để yên.
  
  Kết quả lưu trong PlayerPrefs (`vera_truc_gan`). Nên làm một lần với mỗi tay cầm, vì module BMI160 của mạch đang gắn nghiêng (lúc nằm yên đo được chúc 77°, lăn 40°).

**Kiểm chứng:** bài Unity `tay_cam_vera` phát lại tệp ghi thật 10/11 (45 784 gói, 15 phút cầm tay) qua bộ giải mã và tia C#. `tools/phong_thu/so_sanh_unity.py` so kết quả với bản Python: yaw lệch tối đa 0,0016°, pitch và roll lệch tối đa 0,0001°.

## 3. Hút mục tiêu (`Demo/AimAssist.cs`)

Không cần trỏ trúng 100% mới thao tác được:

1. **Trúng thẳng** một vật làm được việc gì đó: vật đó luôn thắng.
2. **Không trúng thẳng:** tìm vật làm được việc trong nón 5° quanh tia (mức "Vừa", lưu `vera_hut_do` trong PlayerPrefs, 0 = tắt).
   - Nón được cộng thêm kích thước nhìn thấy của vật.
   - Vật được nhận khi tia nắn lại tới được nó (không bị che) và `HandController` xác nhận làm được việc (cầm, đặt, rót...).
   - Bàn, tường, sàn không bao giờ là mục tiêu hút.
3. **Dính có trễ:** đã hút vào vật thì giữ tới khi lệch quá 1,6 lần nón (8°), nên run tay không làm mục tiêu nhảy qua lại.
4. **Theo tốc độ lia:** dưới 60 °/s nón đủ 5°; trên 180 °/s nón còn 35% (1,75°). Lia nhanh thì lướt qua các vật, chậm lại gần vật nào thì hút vào vật đó.
5. **Đang trỏ mặt bàn đặt được:** chỉ hút sang ô hay bình bên cạnh khi lệch dưới 2°, để người chơi cố ý đặt xuống bàn thì không bị hút đi chỗ khác.
6. **Hai tia riêng:** bàn tay và vật đang cầm đi theo tia thật; tia nắn chỉ dùng để chọn chỗ trúng. Nhờ vậy vật đang cầm không bị giật.

Chỉ bật cho tay cầm (VERA, hoặc tay cầm chơi game trỏ giữa màn hình), không bật cho con trỏ chuột tự do.

## 4. Nút

| Thao tác | Tay không | Đang cầm vật |
| --- | --- | --- |
| Chạm / giữ B | cầm vật đang trỏ | giữ để tiếp tục cầm |
| Buông B | | đặt vào chỗ đang trỏ (ô, mặt bàn, khớp lắp); không trỏ chỗ đặt thì trả về chỗ cũ |
| Nhấp A | bấm nút, van, núm thiết bị | dùng 1 lần (nhỏ 1 giọt, múc 1 thìa, nhúng giấy thử) |
| Giữ A | | dùng liên tục (rót, hút, xịt) + tia chậm (chế độ tinh) |
| Lăn cổ tay quá 35° | | rót, khi đang trỏ vào bình nhận |
| Giữ A + B 1 giây | căn tia về giữa | |
| Nhấp đúp A (không trỏ vào gì) | bật / tắt trợ giúp | |
| Cần gạt | ↑ ↓ đi, ← → quay; đẩy hết lên 1 s thì chạy | |
| Nhấn cần | lắc bình | |

**Bảo vệ cần gạt:**
- Khi tay cầm nằm yên mà một trục lệch quá 0,12 trong 1 s, trục đó bị tạm bỏ và màn hình báo "dây lỏng?".
- Trục về tâm 2 s liền thì được dùng lại.
- Lý do: dây VRy thật đang chập chờn, đo được làm nhân vật tự đi 8 m trong 3 phút.

## 5. Kiểm thử

Chạy trong bản sao kiểm thử:

```powershell
$env:KHKT_VERA = "D:\KHKT 11 du phong\du_lieu\tay_cam\phien_20261110_202414.vera"
D:\KHKT_kiem_thu\chay.ps1 -Mode tay_cam_vera
python tools/phong_thu/so_sanh_unity.py du_lieu/tay_cam/phien_20261110_202414.vera D:/KHKT_kiem_thu/ket_qua/tay_cam_vera/vera_phat_lai.csv
```

Bài này gồm:
1. Giải mã gói v3 dựng đúng bố cục `mang.h`, chen chữ, gói hỏng CRC và gói bị cắt đôi.
2. Phát lại tệp ghi thật.
3. Chạy đầu-cuối: gói v3 qua UDP loopback, rồi tia, hút, cầm, chế độ tinh, đặt và bảo vệ cần gạt trong cảnh lab.

**Chưa thử trên phần cứng:**
- Gói v3 qua Wi-Fi (firmware chưa đặt tên Wi-Fi).
- Một lần chạy app thật với người cầm tay cầm qua USB.


## Trang "Tay cầm" ở sảnh chính (19/11/2026)

Sau khi đăng nhập hoặc dùng thử, thanh bên có mục **Tay cầm** (`Menu/Dashboard/PageController.cs`). Mục này dùng để thử tay cầm trước khi vào lab.

- **Kết nối:**
  - tự dò cổng USB (`SerialControllerSource`, thử 115200 rồi 921600 baud) và Wi-Fi (`UdpControllerSource`, cổng 4210);
  - đèn xanh khi đang nhận dữ liệu, vàng khi lỗi cảm biến hướng, đỏ khi chưa thấy tay cầm;
  - hiện số gói/giây, nhiệt độ chip, dòng `#` mới nhất của firmware;
  - nút **Căn joystick**, **Căn trục BMI160**, **Cân con quay** (lệnh `c`), **Đặt hướng về 0** (lệnh `z`). Xem mục 24/11/2026 bên dưới.
- **Hướng tay cầm:** mô hình 3D quay theo tay cầm thật (camera riêng vẽ vào RenderTexture). Có góc xoay / chúc / nghiêng so với lúc đặt về 0.
- **Joystick và nút:** chấm theo cần gạt, đèn nhấn cần, thanh mức nút cảm ứng A, B, có vạch ngưỡng bấm 50%.
- **Thao tác thử:** 13 ô tự đánh dấu khi làm đúng. Gồm đẩy cần 4 hướng, nhấn cần, chạm A, chạm B, chúc mũi lên / xuống 30°, nghiêng trái / phải 30°, xoay trái / phải 45°.
- **Số đo:** con quay (°/s), gia tốc (g), mức cảm ứng, cờ trạng thái.

Nguồn tay cầm chỉ được tạo khi mở trang và bị huỷ khi rời trang, nên vào lab không bị giữ cổng COM hay UDP. Nếu một chương trình khác đang nghe cổng 4210 (ví dụ `xem_tay_cam_v3.py`), trang không nhận được tay cầm Wi-Fi; tắt chương trình đó trước. Tương tự, Serial Monitor hay phòng thử Python đang mở COM thì trang không đọc được USB. Bài thử `man_hinh_chinh` có 6 kiểm tra cho trang này (đạt 83/83 trên bản đã commit cộng với trang mới).


## Sảnh: thanh tay cầm, mô hình thật, căn joystick và trục BMI160 (24/11/2026)

### Kết nối dùng chung cho cả sảnh
- `Menu/Dashboard/ControllerLink.cs` gắn vào khung sảnh. Khi sảnh hiện, nó mở nguồn USB (tự dò cổng) và Wi-Fi (cổng 4210). Khi rời sảnh (`DashboardShell.Hide`), nó trả cổng ngay để lab mở được tay cầm.
- **Thanh góc trên bên phải** (`Thanh tay cầm`):
  - đèn xanh khi đã kết nối, vàng khi lỗi cảm biến hướng, đỏ khi chưa kết nối;
  - có dòng chữ trạng thái và ảnh 3D nhỏ quay theo tay cầm;
  - bấm vào thì mở trang Tay cầm.
- Thanh thông báo lỗi của trang Tổng quan dịch sang trái để nhường chỗ.

### Mô hình 3D thật
- `tools/tay_cam_band2/xuat_unity.py` dựng lại VERA Band 2 bản in ×1.3 trong thư mục tạm, bằng bản sao `mo_hinh_band2.py`, nên không ghi đè tệp in trong docs.
  - Chỉ lấy phần nhìn thấy: vỏ 2 nửa, mặt tối, chén, cò, tấm mu tay, chữ, nắp pin, nắp nút, quai. Tổng khoảng 90 nghìn tam giác.
  - Ghi ra `Assets/Lab/MoHinh/Resources/TayCam/VeraBand2.bytes` (1,1 MB): toạ độ int16, pháp tuyến tách ở cạnh gấp > 35°.
- `Input/VeraModel.cs` đọc tệp này.
  - Hệ trục mô hình trùng hệ tay cầm của firmware (x mũi, y trái, z dọc cán), đổi sang Unity bằng `Frames.ToUnity`.
  - Cần joystick do code vẽ và nghiêng theo cần thật (tối đa 25°). Mặt tối sáng cam khi chạm A, cò sáng xanh khi chạm B.
- Hướng hiển thị là `Frames.ToUnity(q_mạch ⊗ Mount)`, trừ yaw lúc "Đặt hướng về 0". Đây cũng là công thức tia trỏ trong lab dùng.
- Máy quay đặt sau lưng người cầm, nên mô hình quay cùng chiều tay.

### Căn joystick (`Input/VeraJoyCal.cs`, bảng `ControllerCalPanel`)
- **Các bước:** giữa, tiến, lùi, trái, phải, nhấn (giống `tools/canh_joystick.py`).
  - Bước giữa gửi lệnh `J` để firmware đặt tâm ngay tại chỗ buông tay.
  - Các bước hướng tự ghi khi cần lệch hơn 0,2 và giữ yên 0,8 giây.
  - Không thấy nút nhấn thì bấm "Bỏ qua bước".
- **Cách tính:** làm trên số joystick firmware gửi lên, nên chạy với mọi bản firmware, cả USB lẫn Wi-Fi.
  - Tìm trục và chiều của tiến, phải.
  - Lấy độ khuếch đại riêng cho từng nửa trục, để bù biên sai đang lưu trong flash: biên quá rộng làm đẩy hết cần chỉ ra khoảng 0,5.
  - Lưu ở PlayerPrefs `vera_joy_cal`.
  - Áp dụng trong `ControllerPackets.FromVera`: `Joy2` là số đã căn, `JoyRaw` là số firmware gốc.
- **Báo lỗi khi:** tiến và phải cùng trục, một trục không đổi (dây lỏng), hoặc đi ngược chiều. Đẩy chéo hay tâm lệch thì chỉ nhắc.
- Đổi hướng hoặc biên trong flash tay cầm (lệnh `h`, `j`, `H`, `B`) thì phải căn lại.

### Căn trục BMI160
- **Hai bước:**
  1. Cầm tự nhiên: cán dựng đứng, mũi chĩa thẳng ra trước.
  2. Chúc mũi xuống khoảng 45°.
- Tính bằng `VeraMount.Compute` và lưu ở `vera_truc_gan`, chung với phím F6 trong lab. Lời nhắc F6 đã đổi sang tư thế cầm tự nhiên, thay cho "nằm ngang".
- Căn xong, mô hình đứng thẳng khi bạn cầm tự nhiên, và tia trỏ trong lab đi theo mũi tay cầm.

### Kiểm thử
- Bài `man_hinh_chinh` (`tools/kiem_thu_unity/KhktMenuTest.cs`) kiểm:
  - thanh góc;
  - mô hình đọc từ tệp, đúng cỡ (cao 150–175 mm);
  - nút căn, mở và đóng 2 bảng căn;
  - phần tính căn joystick theo số giống tay cầm thật: tiến = y+, phải = x−, đẩy hết ra ±1;
  - báo lỗi khi cùng trục hoặc trục chết;
  - tìm lại đúng trục khi cảm biến gắn lệch bất kỳ;
  - rời sảnh thì trả cổng COM, về lại thì mở lại.
- Trước khi chạy, chép `KhktMenuTest.cs` vào `D:\KHKT_kiem_thu\proj\Assets\Scripts\AutoTest\`, và chép `VeraBand2.bytes` vào `proj\Assets\Lab\MoHinh\Resources\TayCam\`, vì `chay.ps1` không chép hai thứ này.

### Chạy với tay cầm thật (24/11/2026)
- Đặt `KHKT_TAY_CAM_THAT=1` khi chạy bài `man_hinh_chinh`. Bài này chờ sảnh nhận tay cầm (tối đa 30 giây), ghi 6 giây số đo rồi chụp `tay_cam_that.png`.
- **Đo với tay cầm thật qua USB COM8:**
  - nhận được sau 11 giây, 100 gói/s, không khung nào lỗi cảm biến, |a| = 1,05 g;
  - thanh góc báo "Đã kết nối · USB COM8";
  - mô hình nằm nghiêng 85° vì chưa căn trục BMI160;
  - tổng 105/105.
- Gốc yaw lấy sau 0,5 giây có dữ liệu liên tục, vì gói đầu tiên sau khi mở cổng chưa có hướng thật.
- **Nhánh `toi-uu/lab-sinh-tu-do` cần bản tạm tắt `ControllerTestRoom.cs`** (lấy từ commit `f1d9e6d`) mới biên dịch được, vì phần partial `Solution` chưa có.
- **Bản `.exe` chơi được:** `KhktPlayVerify.BuildPlay` với `KHKT_BUILD=D:\KHKT_kiem_thu\ban_tay_cam`.
- **Firmware mới chưa nạp được:** Smart App Control của Windows chặn trình biên dịch ESP32, và máy không có Arduino IDE. Game không cần bản mới, vì phần căn joystick và căn trục làm ở phía game.
