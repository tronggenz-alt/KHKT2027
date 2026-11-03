# Tối ưu đồ họa và lab Sinh thao tác tự do

## 1. Tối ưu đồ họa

Đo bằng bản .exe trên laptop Intel Iris Xe, cửa sổ 1920 × 1080, xen kẽ bản gốc và bản mới. Bản gốc là commit `ad0aa42`.

| Lượt đo | Bản gốc | Bản mới | Thời gian mỗi khung |
|---|---|---|---|
| Máy nguội, 2 vòng | 60,1 FPS | 82,0 FPS | giảm 28 % |
| Máy đã nóng, 2 vòng | 34,6 FPS | 53,2 FPS | giảm 35 % |

Khung giật trên 33 ms giảm từ khoảng 720 xuống khoảng 44 mỗi lượt khi máy nóng. Khung 1 % chậm nhất khi máy nguội từ 26,7 lên 46,0 FPS.

**Đã làm:**

| Thay đổi | Vì sao |
|---|---|
| Đèn trần gắn theo khu (`ZoneStreamer.CollectLights`) | 66 đèn thời gian thực luôn bật, kể cả ở tầng không đứng tới |
| Tầm đèn × 0,55, bù ánh sáng nền khoảng 4 % | Đèn chiếm khoảng 5 ms GPU; phần sáng ở xa gần như đều nên ánh sáng nền thay được |
| Cửa chỉ phản ứng với người, robot cùng tầng (`AutoDoor.OnSameFloor`) | Đứng dưới cửa tầng 2 từng làm tải cả các khu tầng 2 |
| Depth priming, lọc bóng mềm mức thấp, SMAA Medium | Giảm vẽ chồng và chi phí toàn màn hình |
| Giới hạn 60 FPS (`FramePacing`) | Không giới hạn thì GPU chạy hết công suất, máy nóng, tự hạ xung |
| Sửa `ElodeaBubbles` ném lỗi mỗi khung hình trong bản .exe | Shader tìm theo tên không có trong bản dựng |

Bài đo tắt giới hạn 60 FPS để so được, nên trong game FPS sẽ dừng ở 60.

**Đã thử rồi bỏ vì đổi hình ảnh:**
- Hạ bậc SSAO: tường, kệ nổi hạt sạn.
- Khoảng cách bóng 18 m: nắng xuyên tường xa thành vệt vàng.

So ảnh 50 góc với trước khi tối ưu: độ sáng trung bình lệch khoảng −1 %, sai khác điểm ảnh khoảng 2,4 %. Mép vệt nắng hơi sắc hơn.

## 2. Lab Sinh tự do

Trước đây nhiều dụng cụ chỉ cầm lên được, hoặc bấm là chạy một đoạn có sẵn (bể ổn nhiệt bấm là tăng bộ đếm, lam kính bấm là đổi nhãn theo danh sách). Sinh phẩm không có vật thật.

**Cách làm mới:** trong mỗi bình có gì do người học đưa vào, kết quả do luật tính theo thành phần, nhiệt độ và thời gian. Không có bước quy định sẵn.

| Thành phần | Vai trò |
|---|---|
| `Simulation/Bio/BioMix.cs` | Chất trong bình và luật `BioRules` (C# thuần, không cần Unity) |
| `Interaction/Bio/BioVessel.cs` | Cốc, ống, chai, khay mẫu: nhận rót, hút, nhỏ, gắp, thái, đo |
| `BioDropper`, `BioDispenser`, `BioDrain` | Ống nhỏ giọt, hộp lấy thêm dụng cụ, bồn rửa |
| `BioThermalZone`, `BioLabZones`, `BioHotPlate` | Bình đặt vào bể, tủ ấm, tủ lạnh, bếp thì đổi nhiệt độ thật |
| `BioFloor2InteractiveTools.FreeLab.cs` | Giá sinh phẩm, bộ dụng cụ trên từng bàn |

**Trong phòng có:** hai giá sinh phẩm (phòng Sinh và kho), 56 chai và khay; hộp cấp ống nghiệm, cốc, ống nhỏ giọt, lam kính trên từng bàn.

**Hiện tượng tính được:**
- Catalase sủi bọt, tàn đóm bùng cháy; mô đã đun không phản ứng dù nguội lại.
- Lugol gặp tinh bột hóa xanh tím, đun mất màu, nguội hiện lại.
- Men sinh CO₂ theo nhiệt độ, chết khi đun; sữa chua đông ở 37–45 °C.
- Tách ADN: phải có xà phòng, muối, cồn lạnh; thiếu thì không ra sợi.
- Vi khuẩn làm đục canh thang ở 37 °C; cồn và nhiệt diệt; bồn rửa từ chối canh khuẩn sống.
- Agarose đun tan, nguội thành gel.
- Làm tiêu bản: chưa rạch thì không bóc được biểu bì; nhỏ thuốc thật từ chai; lam tự làm đặt được lên kính hiển vi.

**Sửa lỗi kèm theo:**
- Tia trỏ của bàn tay từng dừng ở thân người chơi khi cúi nhìn vật gần, nên không chọn được dụng cụ. Đã sửa ở `HandController.Raycast`.
- Dữ liệu sinh đọc từ thư mục ngoài dự án nên bản .exe không có. Đã thêm bản sao trong `StreamingAssets/data`. Khi sửa `data/sinh` hoặc `data/may` phải chép lại.

## 3. Giới hạn hiện tại

- Quá trình dài được tua nhanh: ngoài bàn 1 giây bằng 1 phút, trong tủ ấm 1 giây bằng 30 phút.
- Luật tính ở mức dạy học, đúng hiện tượng, gần đúng về số.
- Các máy PCR, điện di, ghép gen vẫn dùng luồng riêng (`MolMix`), chưa nối với `BioVessel`. Thuốc thử trong giá có mã PCR chỉ rót được, chưa nạp được vào máy.
- Đồ cố định (vòi nước, màn hình, bình chữa cháy, vòi rửa mắt) chỉ đọc được thông tin.
- Chưa có người thật chơi thử; kiểm thử dùng bàn tay tự động.

## 4. Kiểm thử

- `KHKT_TEST=sinh_tu_do`: 34 mục, đi qua bàn tay thật (tia trỏ, giữ chuột), có cả đảo thứ tự và thao tác sai.
- `KHKT_TEST=khu_tang`: tải khu theo tầng.
- `KHKT_TEST=kiem_ke_sinh`: mỗi mã dụng cụ có vật gì, thao tác được bằng cách nào.
- `tests/BioSimulationTests.cs`: thêm 14 bài cho `BioMix`.
- Đo: `KhktBenchmark` có thêm cột khung giật và chế độ `-khkt-phan-tich` bật, tắt từng bước vẽ.
