# Prompt cho Antigravity: dựng tầng 2 – khu sinh học (VERA Lab / KHKT2027)

> Dán toàn bộ file này vào Antigravity. Làm **lâu, kỹ, từng giai đoạn**. Không vội gộp nhiều giai đoạn vào một lần build.

---

## 0. Vai trò và mục tiêu

Bạn là kỹ sư Unity kiêm họa sĩ môi trường 3D của dự án **VERA Lab** (kho `https://github.com/tronggenz-alt/KHKT2027`, Unity **6000.6.7f1**, URP 17.6.0, Input System 1.20.1).

Nhiệm vụ: dựng **tầng 2 – khu sinh học**. Yêu cầu:

- **Đúng bố cục** bản đồ quy hoạch trang 13 (`phong_sinh/mat_bang_tang2.png`). Tọa độ đã đo sẵn trong `phong_sinh/bo_cuc_tang2.json`.
- **Bàn ghế, đồ đạc như một phòng thí nghiệm sinh học trường THPT thật**: kính hiển vi, tủ an toàn sinh học, tủ ấm, nồi hấp, PCR, điện di, nhà kính.
- **Đồ họa sắc nét, chân thực**, ít nhất bằng phòng hóa học tầng 1 (xem `docs/anh-phong-lab/*.png`), vượt tầng 1 ở độ chi tiết vật liệu.
- **Hai chủ đề:** "Quốc tế" (đồng bộ tầng 1) và **"Tối giản"** (mới, mục 6). Đổi chủ đề có hoạt cảnh.
- **Hoạt cảnh đẹp, nhẹ** (mục 7).
- **Liên kết với phòng sinh học 3D của app cũ** (mục 5): giải phẫu BodyParts3D, 6 cảnh hiển vi, 15 cơ quan.

Máy mục tiêu là laptop **Intel Iris Xe, ≥ 60 FPS** ở phòng chính. "Siêu thực" ở đây là **chi tiết hình học, vật liệu, ánh sáng nướng sẵn, phản chiếu đúng phòng**. Không phải ray tracing hay texture 4K.

---

## 1. Đọc trước khi viết code (bắt buộc, theo thứ tự)

1. `contracts.md`: **luật của dự án, ưu tiên cao hơn prompt này** nếu mâu thuẫn. Đọc hết mục 2, 3, 4, 8, 9, 10.
2. `phong_sinh/README.md`, `phong_sinh/bo_cuc_tang2.json`, `phong_sinh/mat_bang_tang2.png`.
3. Mã dựng cảnh tầng 1, để học **phong cách và mật độ chi tiết**:
   - `Phong thi nghiem ao/Assets/Scripts/Editor/LabBuilder/LabKit.cs`: `Box`, `Cyl`, `Ball`, `Lathe`, `Panel`, `Label`, `Mat`, `Tex`, `NoShadow`, `MarkStatic`.
   - `LabSceneBuilder.cs`: `Build()`, `BuildShell`, `Wall` / `Win` / `WinF` / `Auto`, `BuildLighting`, `BuildPostVolume`, `BakeReflectionProbes`, `OptimizeRenderers`, `Station`.
   - `LabSceneBuilder.Style.cs`, `LabSceneBuilder.Intl.cs`: ảnh vân sinh bằng hàm tuần hoàn, màu khu, biển song ngữ, tấm trần gỗ.
   - `LabSceneBuilder.Rooms.cs`: `StudentBench`, `Stool`, `FumeHood`, `SafetyStation`, `WallSinkCounter`.
   - `LabSceneBuilder.Equipment.cs`, `LabSceneBuilder.Wing.cs`: máy móc, phòng máy.
   - `LabSceneBuilder.Zones.cs`: cửa tự động, tách khu, occlusion, HLOD, impostor, cửa sổ ảo.
4. `Rendering/ZoneMap.cs`, `ZoneStreamer.cs`, `ZoneRoot.cs`, `AutoDoor.cs`.
5. `Environment/DesktopViewer.cs`, `LabStation.cs`. `Demo/HandController.cs`, `EquipmentInfo.cs`, `LabScreen.cs`, `Demo/Instruments/InstrumentScreen.cs`, `Painter.cs`, `DistillerProgram.cs` (có `AutoclaveProgram`).
6. `Interaction/*`: `Grabbable`, `PlaceSocket`, `PlaceSurface`, `LabContainer`, `Pipette`, `Centrifuge`, `HeatSource`, `BalanceScale`, `Spectrophotometer`.
7. `tools/kiem_thu_unity/KhktAutoTest.cs` (cách viết chế độ `KHKT_TEST`).
8. Tài nguyên app cũ trong `phong_sinh/tu_app_cu/` (mục 5).

Sau khi đọc, **viết ra một kế hoạch ngắn** (file, hàm, thứ tự). Chỗ nào prompt này mâu thuẫn với code thật thì **theo code thật và ghi lại**.

---

## 2. Nơi đặt file (tương thích KHKT2027)

| Nội dung | Vị trí |
|---|---|
| Hồ sơ thiết kế tầng 2 (prompt, bố cục, ảnh, tài nguyên app cũ) | `phong_sinh/` (đã có) |
| Mã dựng cảnh tầng 2 | `Assets/Scripts/Editor/LabBuilder/Tang2/LabSceneBuilder.Floor2.cs` (gọi từ `Build()`) và các file con `LabSceneBuilder.Floor2.Shell.cs`, `.BioLab.cs`, `.Micro.cs`, `.Store.cs`, `.Molecular.cs`, `.Greenhouse.cs`, `.Stairs.cs`, `.Theme.cs`, `.Props.cs`. Vẫn là `partial class LabSceneBuilder`, namespace `Khkt.EditorTools` |
| Tiện ích dựng hình mới (hộp bo cạnh, normal map, lá cây) | thêm vào `LabKit.cs`. Hoặc `LabKit.Extra.cs` nếu đổi `LabKit` sang `partial` (sửa tối thiểu, ghi trong PR) |
| Mô phỏng sinh học (C# thuần, **không** `using UnityEngine`) | `Assets/Scripts/Simulation/Bio/` (namespace `Khkt.Simulation.Bio`) |
| Thành phần chạy lúc chơi (chủ đề, hoạt cảnh, kính hiển vi, thang máy) | `Assets/Scripts/Environment/Tang2/`, `Assets/Scripts/Demo/Bio/`, `Assets/Scripts/Demo/Instruments/` (máy có màn hình) |
| Shader mới | `Assets/Lab/Shaders/` (`LaCay.shader`: lá cây đung đưa; `TieuBan.shader`: ảnh tiêu bản mờ theo tiêu cự) |
| Asset sinh ra (vật liệu, lưới, ảnh vân) | `Assets/Lab/Tang2/...`, tên bắt đầu `T2_` để không đè asset tầng 1 |
| Hồ sơ máy | `data/may/<ma>.json`, đúng khóa như máy hiện có |
| Dữ liệu sinh học (tiêu bản, chủng an toàn, thông số có nguồn) | `data/sinh/*.json` |
| Tài liệu | `docs/20-tang-2-khu-sinh-hoc.md`, ảnh chụp ở `docs/anh-tang2/` |

Tên tệp, thư mục mới **không dấu, không cách**. Giao diện, chú thích, tài liệu bằng **tiếng Việt**.

---

## 3. Bố cục tầng 2 (theo bản vẽ)

Tọa độ thế giới: x hướng đông, z hướng bắc, y hướng lên. **Sàn tầng 2 ở y = 3,8**, trần tầng 2 ở y = 7,4. Tạo nhóm gốc `"Tầng 2"` đặt tại `(0, 3.8, 0)` rồi **gọi lại đúng các hàm tầng 1** (`Wall`, `Win`, `WinF`, `Auto`, `LedPanel`, `RoomLights`, `Stool`…) với nhóm này làm cha. Các hàm đó dùng tọa độ cục bộ nên không phải viết lại.

```
 z=15 ┌──────────────── PHÒNG TN SINH HỌC (0..20 × 0..15) ───────────┬── PHÒNG VI SINH ──┬─ HÀNH ─┐
      │  [BSC 6.6]        [BSC 11.6]      [rửa 14.2–16.6]            │ BSC  BSC          │  LANG  │
      │ B                                              ┌giải phẫu┐   │ đảo nhuộm Gram    │  ĐÔNG  │
      │ Ả   ▪bàn7  ▪bàn8  ▪bàn9   (z 12.4)             │17.2–19.9│   │ tủ ấm      ┌đệm┐ ←cửa  │
      │ N   ▪bàn4  ▪bàn5  ▪bàn6   (z 7.5)   pass-box→  └z 9–13.5─┘   │            └───┘   │ 27..30 │
 z=7.5│ G                                              ┌tủ ấm,hấp┐   ├─── KHO SINH PHẨM ─┼────────┤
      │     ▪bàn1  ▪bàn2  ▪bàn3   (z 2.6)              │ z 3–7.5 │   │ tủ 4°C  tủ −20°C  │ THANG  │
      │ [an toàn]                      cửa→kho         └─────────┘   │ kệ mẫu, mô hình ←cửa│ bộ+máy │
 z=0  └─cửa 1.6–3.2──────────── HÀNH LANG TẦNG 2 (z −2.2..0) ───────┴───────────────────┤ 27..30 │
z=−2.2├──── SINH HỌC PHÂN TỬ (0..13.5) ─cửa 5.9–7.5┬──── NHÀ KÍNH THỰC VẬT (13.5..27) cửa 19–20.6 ─┘
      │ tiền PCR │ tách chiết ADN │ PCR · điện di │ mái kính · bàn trồng cây · thủy canh · quang hợp
z=−9  └──────────┴────────────────┴───────────────┴──────────────────────────────────────────────
      x=0                                         x=13.5                                       x=27
```

Chi tiết từng phòng (kích thước, cửa, cửa sổ, đồ đạc) nằm trong `bo_cuc_tang2.json` → `phong`. Tóm tắt:

1. **Phòng thí nghiệm sinh học** (300 m², x 0..20, z 0..15). Bố cục **giống phòng hóa tầng 1** để học sinh quen:
   - **Bảng:** tường tây, học sinh nhìn về phía tây. Khu giáo viên x 0..3.
   - **9 bàn học sinh** tại x {5,2; 8,6; 12,0} × z {2,6; 7,5; 12,4}.
     - Kích thước: dài 2,6 m, sâu 0,9 m, mặt cao 0,8 m.
     - Mỗi bàn: **3 kính hiển vi**, 3 ghế đẩu, bồn rửa đầu bàn, giá sau có ổ điện, đèn bàn LED.
     - Viết **`BioBench`** mới dựa trên `StudentBench`. Không sửa `StudentBench`.
   - **2 tủ an toàn sinh học cấp II** sát tường bắc, tâm x 6,6 và 11,6.
   - **Khu giải phẫu** x 17,2..19,9, z 9..13,5: liên kết app cũ.
   - **Khu tủ ấm, nồi hấp** x 17,2..19,9, z 3..7,5.
   - **Khu an toàn** ở góc tây nam.
2. **Phòng vi sinh** (x 20..27, z 7,5..15).
   - Vùng sạch, có **buồng đệm** (x 25,2..27, z 7,5..10). Cửa vào từ hành lang đông.
   - 2 tủ an toàn sinh học, đảo nhuộm Gram, tủ ấm 37 °C.
   - Đèn UV diệt khuẩn trên trần (tắt, có biển cảnh báo).
   - Pass-box thông sang phòng lab.
3. **Kho sinh phẩm** (x 20..27, z 0..7,5).
   - Tủ lạnh 4 °C hai cánh kính, tủ đông −20 °C, mỗi tủ có màn LED nhiệt độ.
   - Kệ inox, mẫu ngâm, mô hình.
   - Không có cửa sổ (không có tường ngoài).
4. **Hành lang tầng 2** (z −2,2..0), giống hành lang tầng 1. **Hành lang đông** (x 27..30, z 6..15): tầng 1 bên dưới để trống, chỉ có cột.
5. **Phòng sinh học phân tử** (x 0..13,5, z −9..−2,2). Luồng một chiều: tiền PCR → tách chiết ADN → hậu PCR (máy PCR, điện di, buồng chụp gel).
6. **Nhà kính thực vật** (x 13,5..27, z −9..−2,2).
   - Mái kính hai dốc. Vách kính phía nam và phía đông.
   - Bàn trồng cây di động, giá thủy canh.
   - Trạm thí nghiệm: quang hợp, thoát hơi nước, hướng sáng.
7. **Lõi thang bộ + thang máy** (x 27..30, z −2,2..6) theo bản vẽ (*THANG bộ + máy*). Chi tiết ở `bo_cuc_tang2.json` → `loi_thang`.
   - **Thang bộ:** một vế thẳng, 22 bậc × 17,3 cm. Bậc chỉ để nhìn; dốc ẩn để `CharacterController` đi êm.
   - **Thang máy:** đóng cửa, mờ màn hình, đổi tầng, mở cửa.
   - Đặt lõi **ngoài khung tầng 1**, nên **không khoét lỗ sàn** trong phòng tầng 1.

### Thay đổi tầng 1 (tối thiểu, ghi rõ trong PR)

Theo `contracts.md`: *"cầu thang và lỗ sàn: đề xuất vị trí trước khi sửa cảnh tầng 1"*. Vị trí đã theo bản đồ quy hoạch.

**Mở PR nháp ghi đề xuất trước, rồi mới sửa file tầng 1.** Chỉ được sửa:
- **Trần:** `Trần` mỏng lại còn y 3,6..3,7. Mặt dưới giữ ở 3,6. Sàn tầng 2 là hộp y 3,7..3,8, nên không trùng mặt (z-fighting).
- **Mái:** `Mái` và `Diềm mái` dời lên đỉnh tầng 2, chừa khoảng mái kính của nhà kính.
- **Cửa:** thêm một cửa tự động ở `Tường đông dãy nam` (x = 27, z −1,9..−0,3) vào lõi thang.
- **`ZoneMap`:**
  - thêm trường `floor` (0 = tầng 1, 1 = tầng 2, −1 = cả hai; 5 khu cũ mặc định 0);
  - `At(p)` xét `p.y >= 3.6f` để biết đang ở tầng 2;
  - thêm 8 khu mới **vào cuối** (chỉ số 5..12);
  - **không đổi chỉ số 0..4**;
  - `ZoneStreamer` / `SplitIntoZones` đọc thêm `floor`.
- **Đầu dò phản chiếu:** thêm vào `BakeReflectionProbes` một đầu dò cho mỗi phòng tầng 2.
- **Trạm:** thêm trạm dịch chuyển **20..31** (danh sách trong JSON; y thế giới = 3,8).

Mọi hành vi tầng 1 (hóa học, 14 máy, lắp ráp, robot) giữ nguyên. Chạy lại các chế độ kiểm thử cũ để chứng minh.

---

## 4. Đồ họa: sắc nét, chân thực, vẫn ≥ 60 FPS trên Iris Xe

Giữ toàn bộ nền tảng tầng 1:
- URP Forward+ (`PC_Renderer` đã có SSAO);
- hậu kỳ `HauKy.asset`: Neutral tonemapping, bloom nhẹ, cân bằng trắng ấm;
- SMAA cao;
- đầu dò phản chiếu nướng sẵn chiếu hộp;
- lớp `ChiTietNho` / `ChuNho` / `ChuVua` để thôi vẽ khi ở xa;
- tải / gỡ khu, occlusion, HLOD, impostor.

**Không** thêm film grain, vignette, quang sai màu, TAA.

Nâng chất lượng so với tầng 1 bằng các kỹ thuật rẻ sau:

1. **Bo cạnh mọi đồ nội thất và vỏ máy** (`RoundBox(parent, name, center, size, radius, mat)` trong LabKit):
   - lưới hộp bo cạnh sinh bằng code, 2–3 đoạn bo, lưu cache theo kích thước làm tròn tới mm;
   - bán kính: R 6 mm cho mặt bàn, tủ; R 2–3 mm cho vỏ máy;
   - cạnh bo bắt điểm sáng đèn: khác biệt lớn nhất giữa "khối hộp game" và "đồ thật".
2. **Normal map sinh bằng code** (`TexN(name, w, h, heightFunc)`):
   - chuyển ảnh độ cao thành normal, đặt `TextureImporter.textureType = NormalMap`, có mipmap, tối đa 512 px;
   - dùng cho: ron gạch, vân gỗ sồi, inox xước dọc, sơn tĩnh điện sần nhẹ, cao su, bê tông nhà kính, lá cây.
   - Cường độ normal thấp (0,2–0,5) để không lấm tấm hạt khi nhìn xa.
3. **Độ bóng thay đổi theo vùng:**
   - mặt bàn phenolic mờ 0,4;
   - vỏ máy nhựa 0,5;
   - inox 0,75 + metallic 0,9;
   - kính 0,95;
   - mặt màn hình 0,85.
   - Thêm một lớp vệt mờ rất nhẹ (vân tay, vệt lau) vào ảnh smoothness của mặt bàn và kính tủ (kênh alpha của metallic map).
4. **Ánh sáng:**
   - lưới đèn rọi góc rộng như `RoomLights`;
   - đèn hắt ấm 3000 K giấu dưới tủ treo;
   - nhà kính nhận nắng thật qua mái kính: kính `NoShadow`, khung nhôm đổ bóng. Bóng nắng lên sàn và lá là điểm nhấn.
   - Mỗi phòng một đầu dò phản chiếu (128 px, HDR, chiếu hộp).
5. **Kính:**
   - dụng cụ thủy tinh, tủ kính, vách kính dùng lại `labGlass` / `cabinetGlass` / `windowGlass`;
   - dụng cụ dày (bình, cốc) vẽ **hai lượt mặt trong / mặt ngoài** như app cũ (`LabHinh` / `HaiLuot`);
   - chất lỏng ôm sát lòng bình.
6. **Vật chủ đạo dựng thật kỹ (vật nhìn gần):**
   - **Kính hiển vi quang học:**
     - đế có đèn LED (phát sáng);
     - thân cong (Lathe + Box bo cạnh);
     - đầu quan sát hai mắt nghiêng 30°;
     - mâm xoay 4 vật kính có **vòng màu chuẩn**: 4× đỏ, 10× vàng, 40× xanh nhạt, 100× trắng (dầu);
     - bàn kính có bộ dịch mẫu và kẹp lam;
     - tụ quang có màn chắn sáng;
     - núm chỉnh thô / tinh đồng trục có khía.
     - Một lưới dùng chung cho 27 + 2 máy.
   - **Tủ an toàn sinh học cấp II:** kính trượt nghiêng, lưới hút phía trước, đèn trong tủ, bảng điều khiển có màn LED tốc độ gió, chân đỡ.
   - **Tủ ấm:** cửa ngoài kín + cửa trong kính, kệ lưới, màn số 37,0 °C.
   - **Nồi hấp đứng:** nắp vô lăng, đồng hồ áp suất kim, van xả.
   - **Máy PCR:** nắp gia nhiệt, khối 96 giếng, màn cảm ứng.
   - **Bể điện di:** gel trong, lược, điện cực đỏ / đen, nguồn có màn số.
   - **Buồng chụp gel:** cửa mở, bàn soi phát sáng.
7. **Cây xanh (nhà kính, chậu trong phòng):**
   - lá là lưới mỏng uốn cong sinh bằng code (cuống, gân giữa, mép), ảnh vân lá vẽ bằng code;
   - cắt alpha (alpha clip), không trong suốt;
   - vài kiểu cây: rong đuôi chó trong cốc, đậu mầm, cây lá to (môn), dương xỉ, cây cảnh văn phòng;
   - dùng chung lưới, xoay / co giãn ngẫu nhiên có hạt giống cố định (`rnd`) để mỗi lần dựng ra giống hệt;
   - shader `LaCay` có đung đưa theo gió ở đỉnh lưới (mục 7).
8. **Chữ và biển:**
   - biển song ngữ Việt – Anh như tầng 1;
   - nhãn lọ, nhãn tủ, biển cảnh báo nguy hại sinh học (biểu tượng biohazard vẽ bằng code), biển "VÙNG SẠCH", biển UV.
9. **Ngân sách hiệu năng:**
   - ảnh vân ≤ 512 px, dùng chung;
   - vật nhỏ không đổ bóng (`OptimizeRenderers` đã lo);
   - không đèn thời gian thực có bóng ngoài mặt trời;
   - đồ tĩnh `MarkStatic` để gộp lô (trừ đồ tương tác);
   - màn hình động chỉ vẽ lại khi đổi;
   - đo FPS ở trạm 20, 21, 30 (nhà kính nhiều lá nhất) và ghi vào tài liệu;
   - **dưới 60 FPS thì giảm chi tiết, không bỏ bố cục.**

---

## 5. Liên kết với phòng sinh học 3D trong app cũ

App cũ (web CHEMISTRY, `/sinh-hoc`, Three.js) có:
- **giải phẫu thật BodyParts3D** (xương, cơ, nội tạng);
- **6 cảnh hiển vi**;
- **15 cơ quan** có kiến thức SGK.

Tài nguyên đã chép sẵn vào `phong_sinh/tu_app_cu/`:

| Tệp | Dùng vào |
|---|---|
| `bp3d.bin.gz` (BodyParts3D © DBCLS, **CC BY 4.0**) | Bàn giải phẫu số, bộ xương trên giá, mô hình trong kho |
| `dung_bp3d_tham_khao.py` | **Định dạng nhị phân**: "BP3D" + phiên bản + độ dài JSON + JSON (nhóm, neo nhãn, khớp chi, giấy phép) + đỉnh Uint16 lượng tử trong hộp bao + UV Uint16 + chỉ số tam giác mã hóa hiệu, nén gzip |
| `kien_thuc/*.json` (15 cơ quan + `danh-muc.json` + `thuat-ngu.json`) | Thẻ thông tin (`EquipmentInfo`) khi bấm cơ quan, câu hỏi ôn tập trên bàn giải phẫu |
| `vi_mo_tham_khao.js` | 6 cảnh hiển vi: phế nang, nephron, synapse, sarcomere, tế bào viền, tiểu thùy gan. Làm thành **tiêu bản cố định** trong hộp tiêu bản |
| `phong_lab_cu_tham_khao.js` | Phong cách hologram cũ (vòng sáng, lưới lục giác). Chỉ lấy **ý tưởng** cho màn hình bàn giải phẫu, **không** đưa phong cách hologram tối vào phòng |

### Việc phải làm

1. **`Editor/LabBuilder/Tang2/Bp3dImporter.cs`:**
   - đọc `phong_sinh/tu_app_cu/bp3d.bin.gz` (dùng `Path.Combine`, không viết cứng đường dẫn);
   - giải nén, giải lượng tử, giải mã chỉ số;
   - tạo `Mesh` theo nhóm, lưu vào `Assets/Lab/Tang2/GiaiPhau/`;
   - đổi hệ tọa độ trang web (x trái, y lên, z trước) sang Unity;
   - chỉnh tỉ lệ cơ thể cao 1,7 m cho bộ xương đứng, 1/3 cho mô hình trên bàn.
   - Thiếu tệp thì dựng bản thay thế bằng khối cơ bản và ghi cảnh báo, **không được làm hỏng việc dựng cảnh**.
2. **Bàn giải phẫu số** (khu giải phẫu):
   - mặt bàn là màn hình nằm 2,1 × 0,65 m;
   - cơ thể BodyParts3D nằm trên, chia lớp: da / cơ / xương / nội tạng;
   - bấm (`IDemoUsable`) để đổi lớp, tách lớp có hoạt cảnh; chọn cơ quan thì hiện thẻ kiến thức từ `kien_thuc/<id>.json`;
   - tim đập 72 lần/phút, phổi thở (lấy nhịp từ app cũ).
   - Ghi công BodyParts3D trên một nhãn nhỏ cạnh bàn và trong `docs/20`.
3. **Bộ xương đứng trên giá bánh xe** (lớp xương bp3d) ở khu giải phẫu. Bản thu nhỏ đặt trong tủ kính kho sinh phẩm.
4. **Hộp tiêu bản cố định** trên mỗi bàn:
   - **6 tiêu bản mô học** lấy nội dung từ 6 cảnh `viMo`, vẽ lại thành **ảnh 2D nhuộm H&E** bằng code (`Tex`, 512 px): hồng tím, nhân tím đậm, nền hồng nhạt;
   - các tiêu bản theo chương trình: biểu bì vảy hành, khí khổng lá thài lài tía, máu người (phết máu), vi khuẩn sữa chua (Gram +), nấm men.
5. **Màn hình hành lang** quay chậm một cơ quan (đổi 15 cơ quan theo vòng), có chữ "Phòng Lab Sinh học 3D".
6. **AI:**
   - máy tầng 2 thêm vào `data/may` thì `server/ai/machines.py` tự đưa vào ngữ cảnh;
   - thêm từ khóa vào `ALIASES` (kính hiển vi, tủ an toàn sinh học, PCR, điện di…);
   - kiến thức cơ quan có thể thêm vào ngữ cảnh AI của máy chủ KHKT, **không** gọi sang máy chủ app cũ;
   - AI phải **từ chối nhẹ nhàng** yêu cầu nuôi / tạo tác nhân gây bệnh (contracts.md mục 4).

---

## 6. Chủ đề "Tối giản" (mới) và chủ đề "Quốc tế"

### Tham khảo
- Phòng lab Adimab (gỗ sồi trắng, vật liệu tiết chế, thiết kế ưa thiên nhiên): https://www.thinkwood.com/?p=8190
- Kewaunee, *Designing Inspiring Human-Centric Labs* (gỗ trong lab theo SEFA, cây xanh, màu từ thiên nhiên): https://kewaunee.com/wp-content/uploads/2026/04/Designing-Inspiring-Human-Centric-Labs-White-Paper.pdf
- Hughes & Pearson Halls, Đại học Miami (lab sáng, tối giản, nhìn xuyên giữa các không gian): https://www.bsadesign.com/?p=1364
- Tinh thần Japandi / Scandinavian và nguyên tắc "less but better" (Dieter Rams): ít màu, nhiều khoảng trống, vật liệu thật, chi tiết giấu đi.

### Chủ đề "Quốc tế"
Mặc định, **đồng bộ tầng 1**:
- mỗi khu một màu nhận diện (`mau_khu_quoc_te` trong JSON);
- dải sọc màu trên tường, mảng màu quanh cửa;
- ghế nhiều màu, đèn LED xanh ngọc mép bàn.

### Chủ đề "Tối giản"
- **Bảng màu:**

  | Vai trò | Màu |
  |---|---|
  | Tường trắng ngà | `#F4F2EE` |
  | Trần | `#F7F6F3` |
  | Sàn: vinyl liền mảnh xám đá nhạt, **không ron**, hoặc gạch lớn 120 × 60 ron rất mảnh | `#D9D6D0` |
  | Gỗ sồi trắng | `#CDB79A` |
  | Mặt bàn trắng mờ | `#E9E8E4` |
  | Kim loại: nhôm anod xám nhạt | |
  | Chân bàn, màn hình: than chì | `#2B2D2F` |
  | **Một màu nhấn duy nhất:** xanh lá xô thơm (sage) | `#7A9A82` |

- **Hình khối:**
  - tay nắm âm (rãnh), không tay nắm nổi;
  - bo góc đồng nhất;
  - mặt tủ phẳng, khe ron đều 3 mm.
- **Bỏ:**
  - dải sọc cầu vồng, mảng màu quanh cửa;
  - viền LED xanh ở mép bàn: thay bằng một đường hắt sáng ấm 3000 K giấu dưới tủ;
  - ghế nhiều màu: thay bằng mặt ghế gỗ sồi;
  - bớt áp phích, chỉ giữ 1–2 tấm khung mảnh.
- **Giữ và nhấn:**
  - cây xanh thật (ưa thiên nhiên);
  - ánh sáng tự nhiên;
  - khoảng trống;
  - biển hiệu chữ mảnh, chữ hoa giãn cách, nền trắng đục, không khung dày.
- **Ánh sáng:** đèn 4000 K đều, tương phản thấp. Hậu kỳ cho chủ đề này giảm bão hòa (saturation +4 thay vì +14), cùng tonemapping.

### Cách làm
- **Lúc dựng:** sinh **hai bộ vật liệu** `T2_QuocTe_*` và `T2_ToiGian_*`. Ghi bảng cặp vật liệu vào ScriptableObject `Assets/Lab/Tang2/ChuDe.asset` (`BioThemeMap`). Vật chỉ có ở một chủ đề (sọc màu, viền LED…) gắn thẻ thành phần `ThemeOnly { quocTe | toiGian }`.
- **Lúc chạy:** `Environment/Tang2/BioThemeSwitcher.cs`.
  - Đổi chủ đề bằng phím tắt giao diện (chọn phím chưa dùng, ví dụ F7; kiểm tra `DesktopViewer` / `HandController`) và trong bảng Trợ giúp.
  - Lưu lựa chọn bằng `PlayerPrefs`. Biến môi trường `KHKT_CHU_DE=toi_gian` để kiểm thử.
- **Hoạt cảnh đổi chủ đề, 0,6 s:**
  - tạo bản sao vật liệu lúc chạy (**không sửa asset**, để Play trong Editor không làm bẩn asset);
  - nội suy màu, độ bóng theo đường cong *emphasized decelerate*;
  - vật `ThemeOnly` co giãn / mờ dần;
  - kết thúc thì gán hẳn bộ vật liệu đích (giữ SRP Batcher).
  - Khu đang gỡ thì áp chủ đề khi khu được tải lại (móc vào `ZoneRoot` / `ZoneStreamer`).
- Chủ đề chỉ áp cho **tầng 2**. Không đổi vật liệu tầng 1.

---

## 7. Hoạt cảnh (animation) đẹp mắt, nhẹ

### Nguyên tắc
- **Thời lượng:** vi tương tác 100–200 ms; giao diện 200–400 ms; chuyển cảnh 600–1200 ms.
- **Đường cong** (Material 3):
  - emphasized decelerate `(0.05, 0.7, 0.1, 1)` khi xuất hiện;
  - emphasized accelerate `(0.3, 0, 0.8, 0.15)` khi biến mất;
  - standard `(0.2, 0, 0, 1)` cho chuyển động thường.
  - Viết `Khkt.Environment.Easing` (đánh giá cubic-bezier, C# thuần) và **dùng chung**.
- Chỉ chạy khi nhìn thấy (`renderer.isVisible`, khu đang tải). Không cấp phát bộ nhớ mỗi khung hình. Giao diện dùng `unscaledTime`.
- Có tùy chọn **"Giảm chuyển động"**: tắt hoạt cảnh trang trí, giữ hoạt cảnh mang thông tin (bọt khí, khuẩn lạc mọc…).
- Kết quả phải **xác định** (cùng thời điểm → cùng hình) để kiểm thử chụp ảnh ổn định.

### Danh sách
| Ở đâu | Hoạt cảnh |
|---|---|
| Vào phòng lần đầu | Đèn trần bật lần lượt theo khoảng cách tới cửa, mỗi hàng trễ 40 ms, sáng dần 300 ms. Chỉ lần đầu |
| Trạm dịch chuyển | Mờ 250 ms + FOV 70 → 66 → 70 |
| Thẻ thông tin | Phóng 0,96 → 1 + hiện dần 200 ms; mục trong danh sách trễ nhau 30 ms |
| Viền sáng khi nhắm vật | Nhịp thở nhẹ 1,2 s (dùng `VienSang.shader` sẵn có) |
| Cửa tự động, thang máy | Trượt có đường cong; thang máy: đèn số tầng, tiếng "ting", cửa 0,8 s |
| Kính hiển vi | Mâm vật kính xoay có "tách" (0,25 s). Ghé mắt: khung tròn thị kính khép vào 350 ms. Lấy nét: ảnh tiêu bản mờ theo khoảng lệch tiêu cự (shader `TieuBan` chọn mức mip theo độ lệch, rẻ). Màn chắn sáng đổi độ sáng mượt |
| Co / phản co nguyên sinh | Màng sinh chất tách dần khỏi thành tế bào theo nồng độ muối (time-lapse, tốc độ chỉnh được) |
| Khí khổng | Tế bào hạt đậu phồng / xẹp, khe mở / đóng |
| Tủ ấm | Khuẩn lạc mọc theo mô hình logistic (lag → log → ổn định). Thanh tua thời gian. Mở cửa có hơi ấm nhẹ (hạt ít) |
| Tủ an toàn sinh học | Nút "Xem luồng khí": mũi tên / dòng hạt cho dòng xuống và màn khí hút vào. Thao tác sai (tay chắn lưới, kính kéo quá cao) → dòng rối đổi màu cam, cảnh báo nhiễm mẫu |
| Máy PCR | Khối nhiệt đổi màu xanh ↔ đỏ theo chu kỳ, đếm chu kỳ, đồ thị nhiệt độ vẽ dần |
| Điện di | Vạch thuốc nhuộm xanh chạy dần, bọt khí li ti ở điện cực. Buồng chụp gel: bật đèn → các vạch DNA phát sáng hiện dần |
| Máy ly tâm | Tăng / giảm tốc có đường cong (dùng `Centrifuge` sẵn có) |
| Nhà kính | Lá đung đưa (shader `LaCay`: dịch đỉnh theo sin(thời gian + vị trí) × trọng số màu đỉnh). Phun sương mỗi 90 s. Bụi lơ lửng trong vệt nắng (≤ 150 hạt). Bọt O₂ từ rong đuôi chó: tốc độ tỉ lệ cường độ sáng (luật bình phương nghịch theo khoảng cách đèn) |
| Bàn giải phẫu | Mô hình xoay chậm 6°/s khi rảnh; chọn cơ quan → tách lớp kiểu lò xo, nhãn hiện dần; tim đập, phổi thở |
| Lên men | Bóng bay trên miệng bình phồng dần theo lượng CO₂ |
| Đổi chủ đề | Mục 6 |

---

## 8. Thí nghiệm và mô phỏng (sau khi cảnh đã đẹp)

Theo `contracts.md` mục 4. Tóm tắt:

- **Mô phỏng sinh học** `Simulation/Bio/`, C# thuần:
  - `Optics`: độ phóng đại, đường kính thị trường = số trường / độ phóng đại vật kính, độ phân giải 0,61 λ / NA, độ sâu trường;
  - `Osmosis`: co nguyên sinh theo chênh lệch thế nước;
  - `ColonyGrowth`: logistic, E. coli K-12 ở 37 °C;
  - `Od600`;
  - `Pcr`: số bản sao theo hiệu suất và số chu kỳ;
  - `Electrophoresis`: quãng đường chạy tỉ lệ tuyến tính với log(bp);
  - `Fermentation`;
  - `Photosynthesis`.
- **Enzyme catalase + H₂O₂:** **phải qua `ChemEngine`**.
- **Số liệu phải có nguồn** (URL trong JSON). Phần ước lượng ghi `"mo_phong": true`. Ưu tiên SGK Sinh học 10 – 12, TCVN, giáo trình đại học.
- **Máy có màn hình vận hành** (kế thừa `InstrumentProgram`, Hướng dẫn / Thực hành, sơ đồ vẽ bằng `Painter`):
  - kính hiển vi (có camera), tủ an toàn sinh học, tủ ấm, máy PCR, điện di + chụp gel, máy đo OD600 (dùng lại khung UV-Vis);
  - nồi hấp dùng lại `AutoclaveProgram` / `SteamTable`.
  - Hồ sơ `data/may/<ma>.json` đúng khóa. Chép sang Unity bằng `tools/chep_du_lieu_may.py`.
- **Dùng lại**, không viết lại: `Pipette`, `Centrifuge`, `HeatSource` (đèn cồn, bể ổn nhiệt), `BalanceScale`, `LabContainer`, `Grabbable`, `PlaceSocket`.
- **Bài thí nghiệm:**
  - quan sát tế bào hành, khí khổng;
  - co và phản co nguyên sinh;
  - catalase;
  - lên men rượu, lên men lactic;
  - quang hợp thải O₂;
  - tách chiết DNA;
  - nuôi cấy và đếm vi sinh vật.
- **An toàn sinh học:** chỉ chủng dạy học an toàn (E. coli K-12, nấm men, vi khuẩn sữa chua). Không hướng dẫn nuôi hay tạo tác nhân gây bệnh.

---

## 9. Các giai đoạn (mỗi giai đoạn: build → kiểm thử → commit → push → cập nhật PR)

Làm trên nhánh **`tang2/phong-sinh`**. Đúng `contracts.md` mục 10:
- cấm force-push;
- không commit `Assets/Scenes/**/*.unity` và `OcclusionCullingData.asset`;
- vẫn commit `.meta` của script / dữ liệu mới;
- không commit khóa API.

| GĐ | Nội dung | Xong khi |
|---|---|---|
| 1 | Kế hoạch + PR nháp đề xuất lõi thang, sửa trần / mái, `ZoneMap.floor` | PR nháp có ảnh mặt bằng và danh sách file tầng 1 sẽ sửa |
| 2 | Vỏ tầng 2: sàn, tường, cửa, cửa sổ, mái, mái kính nhà kính, lõi thang + thang máy, hành lang đông, 8 khu mới, ánh sáng, đầu dò phản chiếu, trạm 20..31 | Đi bộ từ tầng 1 lên tầng 2 bằng cả thang bộ lẫn thang máy; vào được mọi phòng; tải / gỡ khu đúng tầng |
| 3 | `RoundBox`, `TexN`, lá cây, hai bộ vật liệu, `BioThemeSwitcher` | Ảnh so sánh hai chủ đề ở trạm 20, 26, 30 |
| 4 | Phòng TN sinh học đầy đủ: 9 `BioBench`, 27 kính hiển vi, bàn giáo viên, tủ an toàn sinh học, khu giải phẫu (BodyParts3D), tủ ấm / nồi hấp, khu an toàn, áp phích, tường nhà sinh học | Ảnh chụp ngang chất lượng hoặc đẹp hơn `docs/anh-phong-lab/03_ban_hoc_sinh.png`; ≥ 60 FPS |
| 5 | Phòng vi sinh, kho sinh phẩm, sinh học phân tử, nhà kính, hành lang | Ảnh mỗi phòng; FPS nhà kính ≥ 60 |
| 6 | Hoạt cảnh mục 7 + "Giảm chuyển động" | Video ngắn / chuỗi ảnh; không rớt FPS |
| 7 | `Simulation/Bio` + kiểm thử C#, máy có màn hình, thí nghiệm mục 8, AI | Kiểm thử mục 10 đạt |
| 8 | `docs/20-tang-2-khu-sinh-hoc.md`, bảng FPS, ghi công, cập nhật README (bảng Trạng thái, Tài liệu) | PR sẵn sàng duyệt |

**Không tự gộp PR.** Chủ dự án duyệt.

---

## 10. Kiểm thử

- **Python:** `python -m unittest discover -s tests` đạt hết, kể cả `tests/test_du_lieu_may.py` cho máy mới.
- **C# mô phỏng:** thêm bài cho `Simulation/Bio` theo kiểu `Check` / `Near` của `tests/ChemEngineTests.cs`. Chạy được ngoài Unity (`dotnet`, đặt `KHKT_ROOT`).
- **Unity tự động** `KHKT_TEST=tang2` (trong `KhktAutoTest.cs`):
  - dựng cảnh, đi từ trạm 1 lên tầng 2 bằng thang bộ, rồi xuống bằng thang máy;
  - dịch chuyển qua trạm 20..31: chụp ảnh `docs/anh-tang2/NN_ten.png`, ghi FPS trung bình 5 s vào log `[KHKT-PLAY]`;
  - đổi chủ đề rồi chụp lại trạm 20, 26, 30;
  - mở kính hiển vi, đổi vật kính, lấy nét;
  - kiểm tra `ZoneMap.At` đúng tầng (điểm (10, 1, 7) → `phong_lab`; (10, 4.5, 7) → `phong_sinh`; (28.5, 2, 2) → `loi_thang`).
- **Hồi quy:** các chế độ `may`, `may2`, `may3`, `may4`, `khu` và bài đầy đủ tầng 1 vẫn đạt. Ảnh tầng 1 trước / sau không khác (trừ cửa mới ở đầu hành lang).

---

## 11. Những điều KHÔNG làm

- Không chỉnh tay trong Scene rồi lưu. **Mọi thứ dựng bằng code** (menu KHKT → Dựng phòng thí nghiệm).
- Không làm góc nhìn thứ ba, camera bay tự do, bản đồ nhìn từ trên để chơi. **Chỉ góc nhìn thứ nhất.**
- Không nâng cấp Unity / gói.
- Không đổi tên kỹ năng robot, không đổi chỉ số khu 0..4, không đổi hành vi tầng 1.
- Không đọc phím trực tiếp rải rác (trừ phím tắt giao diện). Điều khiển đi qua `ControllerState`.
- Không đặt gì ở y < 0.
- Không tải mô hình / texture từ nguồn không rõ giấy phép. BodyParts3D (CC BY 4.0) được dùng **có ghi công**. Ảnh chân dung chỉ dùng ảnh phạm vi công cộng.
- Không đưa phong cách hologram tối của app cũ vào phòng. Phòng phải sáng, sạch, như trường thật. Hologram chỉ nằm trong màn hình bàn giải phẫu.
