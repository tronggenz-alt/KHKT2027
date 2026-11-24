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

## 5. Giải phẫu trong app và bàn mổ tự do (24/11)

### 5.1. Mô hình giải phẫu người 3D mở ngay trong app

Thẻ Giải phẫu và bàn giải phẫu số trong lab trước đây mở trang web BIO-LAB 3D riêng. Nay cả hai mở khung xem 3D trong app (`GiaiPhauMoDunImpl`): lưới BodyParts3D, bóc lớp da → cơ → xương và nội tạng, mặt phẳng cắt, chọn 15 cơ quan, tách cơ quan xoay riêng, tim đập, phổi thở, nhu động, câu hỏi chỉ cơ quan.

- Cầu nối `Khkt.Bio.GiaiPhau.GiaiPhauMoDun` gọi `GiaiPhauMoDunImpl` thay vì `BioWebViewBridge`.
- Giao diện dùng màu `GlassKit`, đổi chủ đề Sáng/Tối khi đang mở thì dựng lại ngay.
- Bàn giải phẫu số: bấm mở khung xem và chọn sẵn cơ quan đang trỏ. Con lăn chỉ xoay mô hình trên mặt bàn (trước đây vừa xoay vừa nhảy cơ quan). Mã cơ quan sửa cho khớp danh mục (`da_day`, `ruot_non`…).
- Cổng sinh học vẫn mở bản web BIO-LAB đầy đủ, vì bản web còn nhiều phần khác ngoài giải phẫu.

### 5.2. Mổ ếch tự do

Mở từ nút "Mổ ếch tự do" trong khung xem giải phẫu. Bài chỉ làm trên ếch đồng (không mổ người). Không có bước quy định: người học tự chọn dụng cụ, tự kéo đường rạch. Mọi thứ thấy được (vạt mở, máu chảy, tim đập, phổi phồng, ruột nhu động, chân co) là kết quả tính từ trạng thái trong `Simulation/Bio/GiaiPhau/MoTuDoMoHinh.cs`.

| Thành phần | Vai trò |
|---|---|
| `MoTuDoMoHinh.cs` | Lớp mô dạng lưới ô, vết rạch là cạnh bị cắt; mạch, thần kinh, cơ quan, dịch; sinh lý tim, phổi, ruột, thuốc |
| `BanMoSanKhau.cs` | Vẽ mẫu từ trạng thái: thân ếch một khối theo dáng thật, lớp mô có độ dày ở mép rạch, vạt lật quanh mép còn dính, vũng máu ở chỗ trũng, giọt máu phụt theo tâm thu |
| `BanMoLuoi.cs`, `BanMoVatLieu.cs` | Lưới hữu cơ liền (gan 3 thùy, tim 1 thất 2 nhĩ, hành động mạch, phổi túi, thể mỡ), ống quét liên tục cho ruột, mạch, thần kinh; vật liệu URP Lit có vân sinh theo thủ tục (da ếch lấm tấm, cơ bụng có vách gân ngang, tiểu thùy gan, màng bụng có sắc tố đen) |
| `BanMoTuDo.cs` | Camera (chuột phải xoay), dụng cụ, bảng theo dõi mẫu, ký đồ co bóp tim, nhật ký |

**Luật dụng cụ:**
- Phải hủy não, tủy qua lỗ chẩm và ghim 4 chi lên khay sáp trước khi mổ.
- Dao chỉ cắt lớp trên cùng nơi lưỡi chạm; không cắt xương. Kéo phải luồn từ mép đã rạch hoặc mép tự do; đai vai, xương ức của ếch mỏng nên kéo cắt được.
- Vạt chỉ lật được khi đã rạch rời các phía và chỉ còn dính theo một mép; mảnh rạch vòng thì gắp bỏ được. Vạt chưa ghim tự rơi về sau vài giây.
- Mạch nằm dưới đường rạch thì đứt: rạch cơ bụng đúng đường giữa đứt tĩnh mạch bụng (nên rạch lệch). Động mạch phụt theo nhịp tim, tĩnh mạch trào đều. Kẹp cầm máu thì ngừng; kẹp cuống trước khi cắt thì không chảy.
- Cơ quan nằm sau cơ quan khác (túi mật sau gan, dây tọa sau ruột) phải vén ra trước.

**Sinh lý:** tim mô phỏng nút xoang → nhĩ → trễ nhĩ thất → thất, có thời kỳ trơ. Kích thích lúc thất đang co thì không đáp ứng; kích thích cuối tâm trương gây ngoại tâm thu và nghỉ bù. Kích thích dây X làm tim chậm, có thể ngừng rồi thoát ức chế; atropin chặn tác dụng này. Adrenalin tăng nhịp, giảm nhu động; acetylcholin ngược lại, liều cao làm tim ngừng thì tâm trương, rửa Ringer thì đập lại. Ếch biến nhiệt nên Ringer lạnh làm tim chậm. Không nhỏ Ringer thì mô khô, tim chậm dần. Hủy não thì ngừng thở bằng thềm miệng; thổi qua thanh quản phổi phồng rồi xẹp dần. Tim cắt rời vẫn tự đập. Phản xạ tủy với HCl 0,3 % còn khi chỉ hủy não, mất khi hủy tủy hoặc cắt dây tọa.

**Giới hạn:** lớp mô là lưới ô nên đường rạch có dạng bậc thang theo ô. Cơ quan dựng theo thủ tục, chưa phải lưới quét từ mẫu thật. Số liệu ở mức dạy học, đúng chiều thay đổi, gần đúng về độ lớn. Mã mô hình còn nhánh dữ liệu người (không mở được từ app), có thể xóa nếu không dùng lại.

### 5.3. Dụng cụ lab Sinh: bỏ thao tác dựng sẵn

Bấm trực tiếp lên các dụng cụ dưới đây trước đây trả kết quả có sẵn, không phụ thuộc thứ gì trên bàn. Nay kết quả lấy từ trạng thái thật quanh dụng cụ (`BioGanNhat`).

| Dụng cụ | Trước | Nay |
|---|---|---|
| Đồng hồ bấm giờ | Bấm là nhảy lên 60,0 s, không đếm | Đếm thời gian thật: bắt đầu, dừng, đặt lại |
| Nhiệt kế | Luôn đọc 37 °C | Đọc bình đang nhúng, không thì nhiệt độ vùng hoặc phòng |
| Que đóm | Luôn bùng cháy (gan gà 37 °C) | Thử O₂ thật của bình gần nhất |
| Bình lên men, hũ sữa chua, ống canh khuẩn | Mỗi lần bấm tua 2–3 giờ | Thời gian, nhiệt độ theo chỗ đặt (bàn, tủ ấm, tủ lạnh); đun nóng thì men, khuẩn chết hẳn |
| Kẹp, dao lam, kéo, đĩa đồng hồ | Bấm là có biểu bì, đã rạch, đã cắt | Chỉ hướng dẫn; việc bóc, rạch, cắt làm khi trỏ dụng cụ vào khay mẫu |
| Túi hấp hủy | Bấm là đổi sọc đen | Chỉ đổi khi nằm trong nồi hấp đã chạy xong chu trình |
| Đũa thủy tinh | Bấm là có ADN | Chỉ quấn khi bình gần có ADN kết tủa thật |
| Que trang | Bấm là đã khử trùng | Cần đèn cồn đang cháy ở gần; trang lên đĩa Petri thật |
| Dãy ống pha loãng | Luôn từ 1,25·10⁸ | Lấy mật độ của ống canh khuẩn gốc ở gần |
| Phễu lọc, cối chày | Phễu tự có dịch lọc chứa ADN; cối tự đổ đệm | Phễu cần dịch nghiền trong cối; không có đệm thì dịch lọc không có ADN tự do; thêm đệm là thao tác riêng |
| Trạm tách ADN chuối (phòng SHPT) | Bấm 4 lần đi hết 4 bước in sẵn, luôn ra ống ADN | Chỉ thu ADN từ cốc ở gần đã thật sự tách ra sợi ADN theo luật lab Sinh tự do |

Còn lại: bộ sang chậu (3 bước bấm) vẫn là kịch bản; chuỗi tách ADN cũ (cối, phễu, đũa) vẫn trùng với luồng tách ADN trong `BioVessel`. Nên gộp hẳn vào `BioVessel` ở lần sau.

### 5.4. Kiểm thử

- `Khkt.EditorTools.KhktGiaiPhauVerify.Run`: mô hình ếch, sinh lý; mở khung xem, bàn mổ qua tia chuột thật; dụng cụ đã sửa; chụp ảnh vào `docs/anh-giai-phau-tu-do/`.
- `Khkt.EditorTools.KhktN18Verify.RunG4`: kiểm thử G4 viết lại cho khung xem trong app.
