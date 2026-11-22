# Phòng lab kiểu trường quốc tế, tải khu cộng dồn và tối ưu đồ họa

Tài liệu này mô tả ba việc làm sau giai đoạn A (thiết kế lại phong cách công nghệ cao):

1. Đổi phong cách sang **phòng lab trường quốc tế**: bớt trắng xóa, đồ họa đẹp hơn nhưng không gây nhiễu hạt.
2. **Cửa tự động đóng kín, tải / gỡ từng khu khi đi qua cửa, giữ nguyên hiện trạng** (Level Streaming / Additive Scene
   Loading kết hợp State Persistence). Lối vào phòng hóa chất làm lại với vách kính mờ và cửa kính trượt.
3. **Giai đoạn B**: thử 4 kỹ thuật tối ưu (occlusion culling + cổng, HLOD, impostor, cửa sổ ảo), đo từng kỹ thuật bằng bản
   .exe trên laptop Iris Xe, kỹ thuật nào không nhanh hơn thì tắt.

Toàn bộ cảnh vẫn dựng bằng code: menu **KHKT > Dựng phòng thí nghiệm** (`Assets/Scripts/Editor/LabBuilder/`).

Ảnh chụp 18 góc của cấu hình cuối, kèm ảnh cửa đang mở và ống nghiệm giữ nguyên sau khi gỡ / tải lại khu:
[anh-demo/lab-quoc-te/tong_hop.png](anh-demo/lab-quoc-te/tong_hop.png) (từng ảnh nằm trong `anh-demo/lab-quoc-te/goc/`).
Bảng số đo FPS nằm trong `anh-demo/lab-quoc-te/so_do_fps/`.

## 1. Phong cách phòng lab trường quốc tế

| Hạng mục | Trước (giai đoạn A) | Bây giờ |
|---|---|---|
| Tường, trần | Trắng lạnh, trần phát sáng mạnh | Trắng ấm, dịu hơn; trần sáng vừa |
| Tủ, bàn | Tủ trắng, tay nắm xanh | Cánh tủ vân gỗ sồi, tay nắm đen; mặt bàn Trespa xám nhạt |
| Màu nhận diện | Xanh dương neon khắp nơi | Mỗi khu một màu: lab xanh ngọc, kho hổ phách, phòng máy xanh cốm, phòng robot cam san hô |
| Tường bảng | Mảng mạch điện xanh cuộn chạy | Ốp lam gỗ sồi tiêu âm sau tường video; tên phòng song ngữ |
| Trần | Đèn dải sát trần | Tấm trần gỗ treo trên từng hàng bàn, đèn dải dưới tấm |
| Tường | Dải mạch điện phát sáng | Dải sọc 4 màu chạy ngang tường (supergraphic) |
| Sàn | Vạch xanh mảnh | Lối đi vinyl xám xanh viền xanh ngọc |
| Ghế | Một màu xanh | Nhiều màu (xanh ngọc, cam, xanh cốm, vàng, xanh trời) |
| Thêm mới | | Tường triển lãm dự án học sinh (bảng ghim, 7 poster), tủ khay nhiều màu, kệ sổ thí nghiệm, mô hình phân tử que – bi (H₂O, CO₂, CH₄, benzen), biển song ngữ Việt – Anh |
| Ánh sáng, hậu kỳ | Hơi lạnh, phơi sáng cao | Ấm (~4300 K), tương phản và bão hòa nhẹ; không film grain, không quang sai màu; khử răng cưa SMAA |

**Không nhiễu hạt:** mọi ảnh vân sinh bằng hàm tuần hoàn (lặp liền mạch), tần số thấp, tương phản nhẹ, có mipmap; vật liệu
sàn bớt bóng (đỡ lóa lấm tấm); không thêm hiệu ứng nào tạo hạt. SSAO giữ chế độ làm mờ cao như giai đoạn A.

## 2. Cửa tự động và các khu

Tòa nhà chia thành 5 khu, mỗi khu một cảnh riêng chứa phần tĩnh (tủ, kệ, bàn, trang trí, đèn):

| Khu | Phạm vi (m) | Cảnh |
|---|---|---|
| Phòng thí nghiệm | x 0–20, z 0–15 | `Assets/Scenes/Khu/Khu_PhongLab.unity` |
| Phòng hóa chất | x 20–27, z 0–15 | `Khu_KhoHoaChat.unity` |
| Hành lang | x 0–27, z −2,2–0 | `Khu_HanhLang.unity` |
| Phòng máy phân tích | x 0–13,5, z −9 – −2,2 | `Khu_PhongMay.unity` |
| Phòng tự động hóa & robot | x 13,5–27, z −9 – −2,2 | `Khu_PhongRobot.unity` |

Cảnh lõi `Assets/Scenes/PhongThiNghiem.unity` giữ vỏ tòa nhà (sàn, tường, trần, cửa), hiên và sân, người xem, giao diện,
robot, cùng **mọi đồ tương tác** (lọ, dụng cụ, máy phân tích, chỗ đặt, công tắc, đèn bật tắt được).

**Cửa trượt tự động hai cánh** ở mọi lối giữa các khu: lab – hành lang, lab – kho, kho – hành lang, hành lang – phòng máy,
hành lang – phòng robot, lab – hiên. Người xem hoặc robot tới gần (1,9 m) thì cửa mở, nhưng **chỉ mở khi khu bên kia đã tải
xong**. Đèn trên khung cửa báo trạng thái:

| Màu đèn | Ý nghĩa |
|---|---|
| Xanh dịu | Cửa đóng |
| Hổ phách | Đang tải khu bên kia |
| Xanh lá | Cửa mở |

Robot đứng chờ cửa mở, không đi xuyên qua cánh.

**Kính giữa các khu đổi sang kính mờ** (có dải phim màu), để khi khu bên kia đã gỡ thì không nhìn thấy khoảng trống. Đoạn
tường nam phòng lab giữa cửa và khu thiết bị phân tích thành tường triển lãm dự án.

**Lối vào phòng hóa chất**: vách kính mờ khung nhôm sơn tĩnh điện, dải phim hổ phách, cửa kính mờ trượt tự động, biển phát
sáng song ngữ "PHÒNG HÓA CHẤT · CHEMICAL STORE", trụ quẹt thẻ, 4 biểu tượng cảnh báo GHS, kệ đồ bảo hộ cạnh cửa, thảm cửa
viền vàng hai phía.

## 3. Tải khu cộng dồn và lưu hiện trạng

`ZoneStreamer` (cảnh lõi) giữ đúng các khu đang cần, tải bằng `SceneManager.LoadSceneAsync(..., Additive)`:

- khu người xem đang đứng; đứng ngoài hiên thì cả phòng lab và phòng hóa chất (nhìn qua cửa sổ thấy được);
- khu hai bên mọi cửa trong vòng 4,5 m quanh người xem (tải trước, nên tới cửa là mở ngay);
- khu robot đang đứng và các khu trên đường robot sắp đi;
- khu đang có việc chưa xong (máy phân tích đang chạy), khu vừa được yêu cầu (dịch chuyển tới trạm).

Khu không còn cần quá 2,5 giây thì gỡ hẳn khỏi bộ nhớ (`UnloadSceneAsync`).

**Lưu hiện trạng.** Đồ tương tác của mỗi khu nằm dưới một gốc `ZoneRoot` ở cảnh lõi:

- **Gỡ khu:** nhánh đó "ngủ", tức là tắt hẳn: không vẽ, không chạy script, mô phỏng hóa học tạm dừng.
- **Tải lại:** đồ thức dậy y nguyên: vị trí, dung dịch trong bình, nhiệt độ, máy đang bật.
- **Đồ đã mang sang khu khác hoặc đang cầm:** được chuyển sang gốc của khu chứa nó trước khi khu cũ ngủ.
- **Đồ đang ngủ được cầm hộ từ xa** (tra cứu, robot): được đánh thức về khu đang đứng.

Lưới đi của robot được nướng sẵn khi dựng cảnh (`NavGridData`), vì lúc chạy các khu ở xa đã gỡ. Dịch chuyển tới một trạm ở
khu chưa tải thì có màn mờ ngắn "Đang tải …" (khoảng 0,1–0,3 s).

**Kiểm thử tự động** (bài `khu` trong bản sao project):

| Bước | Kết quả |
|---|---|
| Đi bộ thật qua cửa lab → hành lang → phòng máy | Lúc băng qua, cửa đã mở hẳn (1,00), khu bên kia đã tải, không phải đứng chờ |
| Ở phòng máy, chờ robot đi theo ra khỏi lab | Phòng lab được gỡ sau 13 s (khi robot đã rời đi); ống nghiệm ngủ |
| Quay lại bàn 2 | Ống nghiệm vẫn 4,07 ml SO₄²⁻ 50,1 mM, Na⁺ 49,9 mM, Cu²⁺ 25,1 mM, Cu(OH)₂↓, đúng vị trí: **giữ nguyên** |
| Cầm lọ FeCl₃ từ kho sang lab, chờ kho bị gỡ | Lọ vẫn hiện, đã chuyển sang gốc phòng lab |
| Robot đi lấy HCl ở kho (đang gỡ) | Kho tự tải khi robot tới, lọ đặt trước mặt người xem (0,69 m) |
| Thời gian tải một khu | 20–290 ms (phòng lab nặng nhất) |

Bài kiểm thử đầy đủ (chuẩn độ, định tính, tra cứu, robot, 6 máy phân tích, robot tự chuẩn độ) vẫn qua như trước.

**Trong Unity Editor:** sau khi dựng, cảnh lõi và 5 cảnh khu đều mở sẵn (chỉnh sửa nhiều cảnh). Mở lại sau này: menu
**KHKT > Mở cảnh lõi + tất cả các khu**. Chỉ mở cảnh lõi rồi bấm Play cũng được: `ZoneStreamer` tự tải các khu (các cảnh đã
có trong Build Settings).

## 4. Giai đoạn B: 4 kỹ thuật tối ưu và số đo

| Kỹ thuật | Làm gì | Mã |
|---|---|---|
| B1. Occlusion culling + cổng | Nướng chung cảnh lõi + 5 cảnh khu thành một bộ dữ liệu dùng chung. Mỗi cửa tự động có `OcclusionPortal`: cửa đóng thì che phần phía sau. Kính trong suốt bỏ cờ vật che | `LabSceneBuilder.Zones.cs` (BakeOcclusion), `AutoDoor.cs` |
| B2. HLOD | Mỗi kệ hóa chất / tủ dụng cụ: ở xa thay ~30 lọ bằng một lưới gộp theo vật liệu; lọ bị cầm đi tự loại khỏi lưới gộp | `ClusterHlod.cs` |
| B3. Impostor thời gian thực | Cây ngoài sân được chụp từ 8 hướng lúc chạy; ở xa hiện tấm phẳng quay về người xem, chọn ảnh theo hướng nhìn | `ImpostorBillboard.cs` |
| B4. Cửa sổ ảo | Chụp sẵn cảnh ngoài phía bắc (hiên, sân) và phía nam thành ảnh toàn cảnh; cửa sổ ngoài hiện ảnh đó (hiệu chỉnh thị sai theo khối hộp, shader `Khkt/CuaSoAo`), hiên và sân thật tắt hẳn khi ở trong nhà, tới gần cửa hiên thì bật lại | `VirtualWindowSet.cs`, `Assets/Lab/Shaders/CuaSoAo.shader` |

**Cách đo:** bản .exe 1920×1080, laptop Intel i5-1334U + Iris Xe, cắm sạc. Mỗi bản chạy 10 vị trí (5 s mỗi vị trí, có đi bộ
dọc phòng lab), 3 vòng xen kẽ giữa các bản. Có ghi thêm thời gian CPU và GPU mỗi khung hình.

| Bản | FPS tổng | FPS 1% thấp | CPU ms | GPU ms | SetPass | Tam giác (nghìn) |
|---|---|---|---|---|---|---|
| Giai đoạn A | 61,9 | 40,4 | – | – | 116 | 110 |
| Nền: phong cách mới + tải khu | 58,8 | 37,2 | 7,0 | 17,4 | 116 | 108 |
| + B1 occlusion | 62,4 | 39,8 | 7,6 | 16,3 | **78** | **75** |
| + B2 HLOD | 62,4 | 39,3 | 7,5 | 16,3 | 78 | 75 |
| + B3 impostor | 62,7 | 40,1 | 7,6 | 16,3 | 79 | 72 |
| + B4 cửa sổ ảo | **64,4** | **43,8** | 7,3 | **15,7** | 79 | 67 |

Nhận xét:

- **Máy nghẽn ở GPU** (GPU 16–17 ms, CPU khoảng 7 ms mỗi khung hình), nên kỹ thuật nào chỉ bớt lệnh vẽ mà không bớt điểm ảnh
  phải tô thì ít tác dụng.
- **B1 giữ lại.** Lệnh vẽ giảm một phần ba (116 → 78), tam giác 108 → 75 nghìn, FPS +6%. Rõ nhất ở khu thiết bị phân tích
  (60 → 71) và bàn chuẩn độ (70 → 79).
- **B2 tắt mặc định.** Không nhanh hơn (62,4 → 62,4): các lọ không phải chỗ nghẽn, và khoảng nhìn trong phòng ngắn nên lưới gộp
  ít khi được dùng. Bật lại: biến môi trường `KHKT_HLOD=1` khi dựng.
- **B3 tắt mặc định.** Không nhanh hơn trong sai số (62,4 → 62,7). Khi đã có B4, cây ngoài sân tắt hẳn lúc ở trong nhà nên
  impostor chỉ còn tác dụng ngoài hiên. Bật lại: `KHKT_IMPOSTOR=1`.
- **B4 giữ lại.** FPS +2,7%, FPS 1% thấp +9%, góc toàn cảnh 42 → 49.
- **Bản nền chậm hơn giai đoạn A khoảng 5%.** Phần hậu kỳ mới không phải nguyên nhân (mục 5). Mức chênh này cỡ mức trôi giữa
  các lần đo: cùng một cấu hình, lần đo này được 64,4, lần sau được 66,3.

## 5. Hậu kỳ: chọn khử răng cưa và bloom

Cùng cấu hình B1 + B4, chỉ đổi phần hậu kỳ, đo 3 vòng xen kẽ:

| Phương án | FPS tổng | FPS 1% thấp | GPU ms |
|---|---|---|---|
| SMAA cao + bloom lọc chất lượng cao | 66,3 | 44,6 | 15,18 |
| SMAA vừa + bloom thường | 67,0 | 44,8 | 15,08 |
| FXAA + bloom thường | 66,9 | 44,3 | 15,07 |

Ba phương án chênh nhau trong sai số (0,1 ms GPU), nên chọn **SMAA cao + bloom lọc chất lượng cao**. SMAA cho cạnh sắc, ít
răng cưa và không làm mờ như FXAA. Bloom lọc chất lượng cao giữ viền sáng của dải LED mịn, không nhấp nháy theo điểm ảnh.
Không dùng TAA (dễ bóng ma, mờ) và film grain.

**Cấu hình cuối cùng:** B1 + B4 bật, B2 + B3 tắt, SMAA cao, bloom chất lượng cao, **khoảng 66 FPS** (giai đoạn A: 61,9).

## 6. Tệp mã chính

| Tệp | Vai trò |
|---|---|
| `Assets/Scripts/Rendering/ZoneMap.cs` | Bảng 5 khu: phạm vi, tên cảnh |
| `Assets/Scripts/Rendering/ZoneRoot.cs` | Gốc đồ tương tác của một khu; `FindAll<T>()` tìm cả đồ đang ngủ |
| `Assets/Scripts/Rendering/ZoneStreamer.cs` | Tải / gỡ khu, cho đồ ngủ / thức, chuyển đồ sang khu mới, màn mờ khi dịch chuyển |
| `Assets/Scripts/Rendering/AutoDoor.cs` | Cửa trượt tự động, đèn trạng thái, cổng che khuất, robot chờ cửa |
| `Assets/Scripts/Rendering/ClusterHlod.cs` | B2 HLOD cho cụm vật cầm được |
| `Assets/Scripts/Rendering/ImpostorBillboard.cs` | B3 impostor thời gian thực |
| `Assets/Scripts/Rendering/VirtualWindowSet.cs`, `Assets/Lab/Shaders/CuaSoAo.shader` | B4 cửa sổ ảo |
| `Assets/Scripts/Demo/NavGridData.cs` | Lưới đi robot nướng sẵn |
| `Assets/Scripts/Editor/LabBuilder/LabSceneBuilder.Zones.cs` | Dựng cửa tự động, lối vào kho, tách khu ra cảnh riêng, nướng occlusion, gắn B2–B4 |
| `Assets/Scripts/Editor/LabBuilder/LabSceneBuilder.Intl.cs` | Phong cách trường quốc tế: ảnh vân, tấm trần, tường triển lãm, tủ khay, mô hình phân tử, lối đi, mảng màu cửa |
