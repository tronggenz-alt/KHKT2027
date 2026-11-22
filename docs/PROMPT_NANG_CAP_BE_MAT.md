# PROMPT: XÓA VỆT GẠCH CHÉO, LÀM NHẴN BỀ MẶT, SỬA PHẢN XẠ, LÀM NÉT TRANH TƯỜNG

Dự án VERA Lab, Unity 6000.6.7f1, URP 17.6, chạy trên iPad Air M3.
Làm chậm rãi, kỹ càng, từng giai đoạn một. Xong và đo đạt giai đoạn này mới sang giai đoạn sau. Không có giới hạn thời gian, không làm tắt để nhanh hơn.

## 1. Vấn đề người dùng thấy trên iPad

- Tường, trần, mặt bàn, cánh tủ có hoa văn lạ: đường gạch chéo, ô lưới, gợn sóng.
- Bề mặt trông xỉn và nhám, ánh sáng phản xạ không hợp lý.
- Tranh, chân dung, áp phích và chữ trên tường bị mờ.

Việc cần làm: bề mặt nhẵn sạch, phản xạ đúng với từng chất liệu, tranh tường sắc nét. **Chỉ đổi ngoại hình.**

## 2. Nơi làm việc

- Thư mục duy nhất: `~/VERALabHD`. Đây là bản chính sau khi gộp bản thường và bản HD. Nhánh `main`, commit `52b88faf`.
- Tạo nhánh `nang-cap-be-mat` từ `main`, làm trên nhánh đó. Commit file prompt này (`docs/PROMPT_NANG_CAP_BE_MAT.md`) làm commit đầu tiên.
- Không làm trong `.claude/worktrees` hay checkout `~/Documents/Antigravity`. Unity không đọc được dự án nằm dưới thư mục ẩn.
- Không push lên bất kỳ remote nào. Không xóa nhánh, tag hay thư mục cũ.

## 3. Luật không được phá

1. **Không đổi tọa độ:** vị trí, góc xoay, tỉ lệ, thứ tự cha con của mọi GameObject, kể cả đèn và camera. Không thêm hay bớt collider.
2. **Không đổi backend và logic:** không sửa `server/`, `data/`, `phong_sinh/`, `firmware/`, `contracts.md`, `Assets/Scripts/Simulation`, `Assets/Scripts/Interaction`, `Assets/Scripts/Input`. Không đổi kết quả thí nghiệm, màu dung dịch, nội dung màn hình máy, giao diện HUD.
3. **Giữ nguyên tên và GUID:** không đổi tên, không xóa, không tạo lại `.meta` của vật liệu và ảnh đang dùng. Code có thể tìm vật liệu theo tên.
4. **Sau mỗi giai đoạn phải chạy so tọa độ** và đạt **0 lỗi trên 18.964 đối tượng**, đối chiếu với `docs/do-hoa-hd/toa_do_goc.json`:
   `-executeMethod Khkt.EditorTools.HdCoordinateTool.CompareBatch`
5. **Không nới ngưỡng** trong `docs/do-hoa-hd/tinh-chinh/NGUONG.md`. Không sửa thước đo để đạt.
6. **Không báo cáo khống.** Mỗi số phải đến từ một lần đo có thật, ảnh phải là ảnh chụp thật. Chưa đo thì ghi "chưa đo". Tài liệu `docs/do-hoa-hd/nguon-tai-nguyen.md` đang ghi 100% vân lấy từ CC0, nhưng các bộ vân ở Mục 4A thật ra do công thức sin sinh ra. Phải sửa lại tài liệu cho đúng sự thật.
7. Thử 2 lần mà một giai đoạn vẫn không đạt thì **dừng lại và báo**, không đi tiếp và không che lỗi.

Mỗi giai đoạn: một commit riêng, một tag `be-mat-gN`, một mục trong `docs/do-hoa-hd/be-mat/nhat-ky.md` gồm: đã đổi gì, số đo, ảnh trước/sau, lỗi còn lại.

## 4. Những gì đã kiểm tra được

Đây là kết quả đọc code và xem ảnh vân, chưa thử sửa trong Unity. Giai đoạn 1 phải xác nhận lại bằng thí nghiệm.

### A. Nguyên nhân chính của vệt chéo: ảnh vân tự nó là hoa văn sin

`Assets/Scripts/Editor/LabBuilder/LabKit.HdMaterials.cs`, các hàm cuối file, sinh "nhiễu" bằng công thức sin nên ra hoa văn đều đặn:

| Hàm | Công thức | Kết quả nhìn thấy |
|---|---|---|
| `GenMicroNoise` | `sin(u*f*π) * cos(v*f*π)` | Lưới chéo. `PlasterWall001_Normal.png` nhìn rõ các sọc chéo đều |
| `GenNoisePattern` | `sin(u*50)*cos(v*50) + sin(u*110+v*90)*0.5` | Sọc chéo trong ảnh màu |
| `GenWoodPattern`, `GenWoodHeight` | `sin(u*20 + sin(v*40)*2)` | Gợn zigzag trên cánh tủ |
| `GenBrushedMetal`, `GenBrushedNormalHeight` | `sin(v*800)` | Sọc đều trên inox, dễ gây moiré |
| `GenTilePattern` | `sin(u*120)*cos(v*120)` | Hoa văn trên gạch |

Các hệ số như 50, 110, 20 không phải bội số của 2π nên vân **không liền mép** khi lặp.

Vật liệu đang dính các bộ này: `TuongSon`, `Tran`, `TuongNgoai`, `ThanTu` (Plaster), `MatBanPhenolic` (Trespa), `CanhTu` (Oak normal), `SanPhongLab`, `SanHanhLang` và 2 vật liệu sàn khác (Tiles normal + mask), và các vật liệu inox, nhôm, epoxy, terracotta. Phải quét lại toàn bộ `Assets/Lab/Materials` để có danh sách đủ.

Ngược lại, `Normal_Plaster.png` do `tools/hd_textures/generate_pbr_textures.py` sinh ra nhìn đúng là nhiễu, không có sọc.

**Có 3 đường cùng ghi đè vật liệu:** `LabKit.HdMaterials.cs`, `Assets/Scripts/Editor/HdTools/HdMaterialEnhancer.cs` và `tools/hd_textures/generate_pbr_textures.py`. Đường chạy sau thắng. Hiện `TuongSon.mat` đang dùng bộ Plaster sin dù `HdMaterialEnhancer` định gán `Normal_Plaster`. Phải gom về **một nguồn duy nhất**, để dựng lại cảnh không bao giờ sinh lại vân cũ. `CreateOrLoadPbrSet` chỉ sinh khi file chưa tồn tại, nên xóa PNG cũ là chưa đủ: hàm sin vẫn còn và sẽ sinh lại.

### B. Vân bị kéo giãn theo kích thước hộp

`LabKit.Box` dùng `CreatePrimitive(Cube)` rồi đổi tỉ lệ, UV mỗi mặt luôn là 0..1. `TuongSon` có tiling 1×1, nên một ảnh bị kéo trải theo cả mảng tường, hộp dài ngắn khác nhau thì vân giãn khác nhau. Vì thế cùng một vật liệu mà vệt ở mỗi nơi một khác.

### C. Sàn: ảnh màu và normal lệch lưới

`SanPhongLab` có tiling 16,67×12,5. Ảnh màu `GachLatSan60` khớp với tiling đó, nhưng `TilesLab086_Normal` có **8 viên gạch trong một ảnh**, nên lưới ron của normal dày gấp 8 lần và không trùng ron thật.

### D. Độ bóng đang bị nhân hai lần

Cả 10 vật liệu chính đều bật `_METALLICSPECGLOSSMAP`. URP tính `specGloss.a *= _Smoothness` (`LitInput.hlsl` dòng 142), tức độ bóng thật = kênh A của mask × `_Smoothness`. Tính theo code sinh mask:

| Vật liệu | Mask A | `_Smoothness` | Độ bóng thật |
|---|---|---|---|
| `TuongSon` | 0,12 | 0,18 | **0,02** |
| `Tran` | 0,12 | 0,08 | **0,01** |
| `CanhTu` | 0,38 | 0,36 | **0,14** |
| `MatBanPhenolic` | 0,40 | 0,42 | **0,17** |
| `SanPhongLab` | 0,58 | 0,55 | **0,32** |
| `MatBanEpoxy` | 0,65 | 0,62 | **0,40** |
| `Inox` | 0,68 | 0,68 | **0,46** |

Đây là lý do mọi thứ xỉn, kể cả các mặt được ghi là "bóng". Khi có mask thì `_Metallic` cũng bị bỏ qua, metallic lấy từ kênh R. Hãy đọc kênh A thật của các PNG để xác nhận bảng này.

### E. Những nghi vấn cần đo, chưa kết luận

Nền hiện tại trong `Assets/Settings/Mobile_RPAsset.asset` và `Mobile_Renderer.asset`:

- Bóng: 4096, 4 tầng, 45 m, depth bias 1, normal bias 1.
- `m_MSAA: 1`, tức **không khử răng cưa**.
- SSAO: Intensity 0,35, Radius 0,3, `Downsample: 1`, `BlurQuality: 0`. Có thể là nguyên nhân của dải mờ loang lổ ở chân tường trong ảnh.
- Đầu ảnh hành lang có hai vệt sáng hình bình hành trên tường, cần xác định là nắng thật hay nắng lọt.
- Có một đốm chói trên kính cửa sổ gần mép trên màn hình, cần xác định nguyên nhân.
- Ảnh chụp từ iPad rộng 2000 px trong khi màn hình 2360 px: kiểm tra xem app có đang chạy dưới độ phân giải gốc không.
- Probe đã nướng cũng chứa vân sọc cũ, nên **phải nướng lại** sau khi đổi vân.

### F. Tranh tường mờ vì ảnh quá nhỏ

| Ảnh | Hiện tại | Tối thiểu cần có |
|---|---|---|
| `Poster0..6` | 128×176 | 1024×1408 |
| `ChanDungMau` (một bóng người dùng chung cho mọi nhà khoa học) | 160×200 | 1024×1280 cho từng người |
| `T2_ApPhichTeBao`, `ChuKy`, `CaySuSong`, `ADN` | 256×352 | 1024×1408 |
| `BangTuanHoan` | 456×288 | 1824×1152 |
| `GHS0..3`, `T2_BieuTuongBiohazard` | 128×128 | 512×512 |
| `CoVietNam` | 300×200 | 1200×800 |
| `SocMau` | 32×128 | 128×512 |
| `BanGhim`, `LamGoSoi`, `VanGoSoi`, `VanGoTran` | 128–256 | 1024 |
| `NenManHinh` | 320×180 | 1280×720 |
| `T2_AnhGel`, `T2_ManGiaiPhau` | 128×96, 128×256 | gấp 4 lần |

Giữ đúng tỉ lệ khung để tranh không bị méo trên tấm đang có.

Bẫy cần tránh:

- **`LabKit.Tex` ghi đè PNG mỗi lần dựng cảnh.** Thay ảnh bằng tay thì lần dựng sau sẽ mất. Phải sửa để nguồn ảnh nét nằm trong code hoặc được nạp ưu tiên.
- `HdMaterialEnhancer.ConfigureTextureImporters` ép `maxTextureSize` về 1024 trong `Assets/Lab/HD/Textures`.
- `LabKit.Label` dùng `TextMesh` với `fontSize = 64`, chữ nhỏ trên thẻ tên bị mờ. Có thể tăng `fontSize` và giảm `characterSize` theo cùng tỉ lệ để giữ nguyên kích thước thật. Vị trí chữ không được đổi.

## 5. Các giai đoạn

### Giai đoạn 0: Chuẩn bị, chưa sửa gì

1. Tạo nhánh, tag `be-mat-g0`. Chạy so tọa độ để có mốc 0 lỗi.
2. Chụp ảnh **trước** bằng `KHKT_TEST=do_hoa` (2360×1640) tại các góc cố định. Bắt buộc có 3 góc giống ảnh người dùng: hành lang có cửa sổ và dãy chân dung, cận bàn thí nghiệm có tủ gỗ, tường video có bàn giáo viên. Thêm: sàn phòng lab nhìn xiên, trần, nhà kính, hành lang tầng 2, phòng sinh học. Lưu ở `docs/do-hoa-hd/be-mat/truoc/`. Các lần sau chụp đúng các góc này.
3. Viết `tools/do_hoa/do_soc.py`, phát hiện hoa văn lặp bằng FFT: tỉ số giữa đỉnh phổ cao nhất và trung vị phổ, cho cả ảnh vân lẫn vùng cắt trên ảnh chụp.
4. **Đối chứng:** thước đo phải báo có sọc với `PlasterWall001_Normal.png` và `OakWood051_Normal.png`, báo sạch với `Normal_Plaster.png`. Chốt ngưỡng từ đối chứng này và ghi vào nhật ký **trước khi** sửa, sau đó không đổi nữa.
5. Quét tất cả ảnh trong `Assets/Lab/HD/Textures` và `Assets/Lab/Textures`, lập danh sách vân nào bị sọc, vật liệu nào đang dùng. Ảnh có chủ đích (poster, sọc trang trí, cờ, vạch buret) ghi riêng là ngoại lệ.
6. Ghi số đo nền trên iPad nếu có: FPS trung bình, 1% thấp, RAM.

Nếu máy thiếu `numpy`, `PIL` thì dùng venv riêng trong dự án, không cài vào Python hệ thống.

### Giai đoạn 1: Thí nghiệm tách nguyên nhân

Tại góc hành lang, mỗi lần chỉ đổi đúng một thứ rồi chụp lại, không commit thay đổi tạm:

1. Tắt normal map của tường.
2. Thay ảnh màu tường bằng màu trơn.
3. Tắt bóng mặt trời.
4. Tắt SSAO.
5. Đổi depth/normal bias.

Lập bảng: mỗi thay đổi làm giảm vệt bao nhiêu theo `do_soc.py`. Kết luận nguyên nhân bằng số. Nếu kết quả trái với Mục 4 thì tin kết quả đo và ghi rõ.

### Giai đoạn 2: Thay toàn bộ vân bị sọc

1. Gom về một nguồn sinh vân duy nhất, loại hẳn các hàm sin ở Mục 4A và chuỗi ghi đè chồng chéo.
2. Ưu tiên vân thật từ ambientCG hoặc Poly Haven (CC0) cho gỗ, gạch, kim loại, có ghi URL và giấy phép. Không tải được thì sinh bằng fBm nhiều tầng tuần hoàn trên lưới nguyên, seed cố định, gỗ có domain warp và thớ dọc thật sự. Ghi đúng nguồn của từng bộ vào `nguon-tai-nguyen.md`.
3. Mọi vân phải liền mép và lọt ngưỡng `do_soc.py`.
4. Xử lý kéo giãn (Mục 4B), chọn theo thứ tự đơn giản trước:
   - Bề mặt sơn trơn (tường, trần, thân tủ) **bỏ normal map**, màu gần như đồng nhất, chỉ biến thiên rất nhẹ ở tần số thấp.
   - Bề mặt cần vân mà bị giãn thì dùng UV theo mét bằng shader thế giới, hoặc tính tiling đúng. Shader mới phải đủ 4 pass (ForwardLit, ShadowCaster, DepthOnly, DepthNormals) và tương thích SRP Batcher. Không thay mesh nếu làm so tọa độ báo lỗi.
5. Sửa sàn (Mục 4C): normal và mask phải cùng lưới với `GachLatSan60`, ron trùng khít.

### Giai đoạn 3: Độ bóng và phản xạ theo chất liệu

1. Sửa lỗi nhân đôi (Mục 4D): một phía là giá trị thật, phía kia bằng 1. Viết bài kiểm tra in **độ bóng thật** của mỗi vật liệu.
2. Mục tiêu độ bóng thật, tinh chỉnh quanh các mức này bằng mắt và đo cháy sáng:

| Bề mặt | Độ bóng thật | Metallic | Ghi chú |
|---|---|---|---|
| Tường sơn satin | 0,35–0,45 | 0 | Nhẵn, ánh mờ nhẹ, không giống gương |
| Trần | 0,10–0,15 | 0 | Mờ, không có vệt đèn |
| Sàn gạch men | 0,65–0,75 | 0 | Thấy phản chiếu mềm của cửa sổ và đèn. Ron 0,20–0,30 |
| Mặt bàn Trespa | 0,55–0,65 | 0 | Phẳng, không gợn |
| Mặt bàn epoxy | 0,75–0,85 | 0 | Bóng rõ, phẳng |
| Cánh tủ gỗ phủ sơn | 0,45–0,55 | 0 | Thớ gỗ dọc, tinh tế |
| Thân tủ laminate | 0,45–0,55 | 0 | Trơn |
| Inox xước mờ | 0,60–0,75 | 0,9–1 | Không dùng sọc tần số cao gây moiré |
| Nhôm anodized | 0,50–0,60 | 0,8–0,9 | |
| Kính, thủy tinh | Giữ nguyên | | Không đụng trừ khi có lỗi |

3. Giữ hợp lý vật lý: mặt nhìn xiên phản xạ mạnh hơn nhìn thẳng (Fresnel của URP Lit), không thêm mẹo tăng phản xạ giả. Không lắp thêm SSR đắt tiền trên iPad.
4. Nướng lại toàn bộ probe sau khi có vân và độ bóng mới. Kiểm tra `boxProjection`, vùng probe khớp phòng, không có đường nối phản xạ khi đi qua cửa.
5. Được đổi cường độ, màu, tầm chiếu của đèn; **không được đổi vị trí, góc xoay của đèn**. Không thêm đèn. Vệt phản chiếu của đèn trần trên sàn phải khớp vị trí đèn thật.
6. Làm rõ hai vệt sáng và đốm chói ở Mục 4E. Nếu là lỗi hiển thị thì sửa bằng vật liệu hoặc cài đặt, không dời vật.
7. Đo `do_anh.py`: cháy sáng và các ngưỡng trong `NGUONG.md` vẫn phải đạt.

### Giai đoạn 4: Bóng, SSAO, khử răng cưa, độ nét toàn cảnh

Mỗi thay đổi đo riêng FPS và RAM, giữ nếu đáng giá.

1. Hạ bias từng bước đến khi bóng bám chân vật mà không có sọc acne.
2. Tinh chỉnh SSAO để hết dải loang ở chân tường: thử `Downsample: 0`, `BlurQuality` cao hơn, Radius nhỏ hơn.
3. Thử MSAA 4x; nếu quá nặng thì dùng SMAA.
4. Xác nhận render đúng độ phân giải gốc trên iPad ở mức Ultra.
5. Anisotropic 16 cho sàn, tường, tranh; kiểm tra chế độ `ForceEnable` trong `HdQualityManager` có tác dụng thật.
6. Giữ 3 mức Ultra, Cao, Cân bằng hoạt động đúng. Đảm bảo đổi mức không làm hỏng các thay đổi này.

### Giai đoạn 5: Làm nét tranh và chi tiết trên tường

1. Vẽ lại mọi ảnh ở Mục 4F theo độ phân giải tối thiểu, giữ nguyên tên file và `.meta`.
2. **Chữ trong ảnh** render sạch bằng Be Vietnam Pro (`Assets/Resources/GiaoDien/Phong/`), đủ dấu tiếng Việt, kiểm tra chính tả từng tấm. Giữ đúng tiêu đề đang có trong code.
3. **Nội dung khoa học phải đúng:** bảng tuần hoàn đúng ô và đúng khối, cấu trúc ADN và tế bào đúng, chu kỳ tế bào đúng thứ tự, biểu tượng GHS đúng hình chuẩn. Tự rà và ghi nhật ký.
4. **Chân dung:** danh sách trong `Chemists` ở `LabSceneBuilder.Style.cs:228` và phần sinh học. Hiện mọi người dùng chung một bóng đen. Ưu tiên ảnh hoặc tranh **phạm vi công cộng** đã kiểm chứng đúng người, có ghi nguồn. Không xác minh được thì dùng minh họa trung tính, tuyệt đối không gán ảnh sai người và không dùng ảnh AI giả làm ảnh thật. Việc này có thể cần thêm vật liệu cho từng người: thêm được, nhưng tấm ảnh phải giữ nguyên vị trí, kích thước.
5. Thiết lập nhập ảnh cho nhóm này: mipmap, Trilinear, Aniso 16, Clamp, ASTC 4×4 trên iOS, không nén mạnh. Sau khi đổi, đọc kích thước thật trong file `.meta` và xem lại `HdMaterialEnhancer` có ép nhỏ đi không.
6. Tăng độ nét `TextMesh` (Mục 4F) và kiểm tra `Assets/Lab/Shaders/ChuTrongCanh.shader`. Chữ ở thẻ tên phải đọc được khi đứng cách 1,5 m. Kiểm tra `Label` không đổi vị trí.
7. Tấm tranh nên có độ bóng thật khoảng 0,30–0,40 như giấy ép plastic và không bị lóa đến mức mất chữ.
8. Sửa để dựng lại cảnh **không** ghi đè ảnh nét bằng ảnh 128 px (Mục 4F).

### Giai đoạn 6: Dựng lại cảnh và kiểm tra toàn bộ

1. Chạy `Khkt.EditorTools.LabSceneBuilder.Build`. **Không dùng `-nographics`**, vì nướng phản chiếu và cảnh ngoài cửa sổ cần GPU.
2. So tọa độ: 0 lỗi trên 18.964 đối tượng.
3. Chụp lại đúng các góc ở Giai đoạn 0 vào `docs/do-hoa-hd/be-mat/sau/`, ghép ảnh trước/sau.
4. Chạy `do_soc.py`, `do_anh.py`, `kiem_chung.py`, và các bài `KHKT_TEST=ipad`, `tang2`, `khu` nếu có sẵn. So với kết quả trước khi sửa. Không được có bài nào tệ đi.
5. Quét cả 13 khu, tìm vật liệu hồng (thiếu shader), chữ sai dấu, vân còn sọc.
6. **Tự xem ảnh bằng mắt** ở cả 8 góc. Số đạt mà nhìn vẫn thấy sọc thì chưa đạt.

### Giai đoạn 7: Xuất lên iPad

1. Chép dự án sang `~/AppBuild/VERALabHD` bằng `cp -cR`, chỉ khi còn đủ đĩa (hiện còn khoảng 12 GB, nếu dưới 8 GB thì dừng và báo). Gỡ `Assets/Scripts/AutoTest` và `KhktPlayVerify.cs` khỏi bản sao trước khi xuất.
2. Xuất bằng `Khkt.EditorTools.IosBuild.Device` (ra `Builds/iOS`). Lệnh `IosBuild.BuildIosProject` trong `viec-sang-mai.md` **không tồn tại**.
3. Dựng Xcode với `DEBUG_INFORMATION_FORMAT=dwarf GCC_GENERATE_DEBUGGING_SYMBOLS=NO`, nhóm ký `29263U3K84`. Cài bằng `xcrun devicectl device install app` lên iPad UDID `00008122-001669801186801C`. Xóa `dd-*` sau khi cài.
4. App giờ tên **VERA Lab**, bundle `com.trantrong.veralab`, sẽ thay app VERA Lab cũ. Không tự gỡ app nào trên iPad. Nhắc người dùng tự xóa biểu tượng "VERA Lab HD" cũ.
5. Đo trên iPad ở mức Ultra: FPS trung bình phải giữ 30, 1% thấp không dưới 28, RAM tăng phải được ghi rõ. Không đổi `targetFrameRate`. Nếu tụt FPS thì giảm theo thứ tự: MSAA, SSAO, độ phân giải bóng, không giảm vân tường và tranh trước.

### Giai đoạn 8: Bàn giao

1. Sau khi mọi thứ đạt, gộp fast-forward `nang-cap-be-mat` vào `main`. Không push.
2. Viết `docs/do-hoa-hd/be-mat/bao-cao.md`:
   - Nguyên nhân thật của vệt chéo, kèm bảng thí nghiệm Giai đoạn 1.
   - Bảng độ bóng thật trước và sau.
   - Ảnh trước/sau.
   - FPS, RAM trên iPad.
   - Danh sách nguồn ảnh và giấy phép.
   - Những gì chưa đạt hoặc chưa kiểm được, ghi thẳng.

## 6. Điều kiện hoàn thành

- [ ] Không còn đường gạch chéo, lưới, gợn sóng trên tường, trần, mặt bàn, tủ, kể cả nhìn xiên. `do_soc.py` đạt ngưỡng đã chốt trước.
- [ ] Không còn hàm sinh vân bằng sin. Dựng lại cảnh vẫn sạch.
- [ ] Độ bóng thật trong khoảng ở Giai đoạn 3. Sàn và mặt bàn thấy phản xạ hợp lý, tường và trần không lóa.
- [ ] Tranh, chân dung, áp phích, chữ trên tường đọc rõ ở khoảng cách 1,5 m, nội dung đúng.
- [ ] So tọa độ 0 lỗi trên 18.964 đối tượng. Backend và logic không đổi: `git diff` không chạm các thư mục ở Luật 2.
- [ ] Bài kiểm thử không tệ đi. Ngưỡng `NGUONG.md` đạt và không bị sửa.
- [ ] iPad giữ 30 FPS ở Ultra, người dùng tự kiểm tra lại được.
