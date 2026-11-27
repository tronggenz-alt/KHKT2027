# Prompt cho Antigravity: cổng vào "Phòng Sinh học 3D" từ mô hình giải phẫu tầng 2

> Dán toàn bộ file này vào Antigravity. Làm **từng giai đoạn**, xong giai đoạn nào kiểm thử giai đoạn đó. Không gộp tất cả vào một lần build.

---

## 0. Vai trò và mục tiêu

Bạn là kỹ sư Unity kiêm họa sĩ hiệu ứng của dự án **VERA Lab** (Unity **6000.6.7f1**, URP 17.6.0, Input System 1.20.1, bundle `com.trantrong.veralab`, máy đích chính là **iPad**, máy phụ là laptop Intel Iris Xe).

Việc cần làm, nói bằng lời người dùng:

> Khi nhân vật lại gần mô hình ở phòng thí nghiệm sinh học tầng 2 thì hiện ra một cái nút / bảng để nhấn. Nhấn vào thì đi thẳng sang **phòng sinh học 3D của app CHEMISTRY cũ**, có hiệu ứng chuyển cảnh đẹp, mang chất khoa học, chạy mượt, **đủ cả hiệu ứng mở lẫn hiệu ứng đóng**.

Chia thành 4 phần:

1. **Cổng (nút lại gần):** thẻ kính mờ + vòng sáng dưới chân mô hình, chỉ hiện khi người chơi đứng gần.
2. **Phòng Sinh học 3D:** dựng lại **ngay trong Unity** phòng hologram của app cũ (web CHEMISTRY, `/sinh-hoc`) từ tài nguyên đã chép sẵn ở `phong_sinh/tu_app_cu/`. **Không** mở trình duyệt, **không** dùng WebView, **không** gọi sang máy chủ app cũ: app phải chạy được khi không có mạng.
3. **Chuyển cảnh mở:** quét mô hình → lưới lục giác phủ màn hình → phòng hologram sáng dần → cơ thể hiện hình từ chân lên đầu.
4. **Chuyển cảnh đóng:** trình tự ngược lại, trả người chơi về **đúng chỗ, đúng hướng nhìn** lúc bấm.

Ngân sách: **≥ 60 FPS trên iPad trong suốt lúc chuyển cảnh**, không có khung hình nào dài hơn 33 ms, không cấp phát bộ nhớ theo từng khung hình.

---

## 1. Đọc trước khi viết code (bắt buộc)

1. `contracts.md`: **luật của dự án, ưu tiên cao hơn prompt này**. Đọc kỹ mục 3, 8, 9, 10.
2. `phong_sinh/README.md` và `phong_sinh/PROMPT_ANTIGRAVITY.md` mục 5 (liên kết app cũ).
3. Tài nguyên app cũ, trong `phong_sinh/tu_app_cu/`:
   - `phong_lab_cu_tham_khao.js`: phòng hologram cũ (sàn tròn bán kính 10, lưới, bệ, 4 vòng sáng, chùm sáng có vạch chạy). **Đây là bản thiết kế hình ảnh phải bám theo.**
   - `vi_mo_tham_khao.js`: 6 cảnh hiển vi (phế nang, nephron, synapse, sarcomere, tế bào viền, tiểu thùy gan).
   - `kien_thuc/*.json`: 15 cơ quan. Mỗi tệp có khóa `id`, `ten`, `chuong_trinh`, `vi_tri`, `cau_tao`, `chuc_nang`, `co_che`, `lien_he`, `diem_thi`, `trac_nghiem`, `goi_y_hoi`, `mo_rong`. Thêm `danh-muc.json`, `thuat-ngu.json`.
   - `bp3d.bin.gz` + `dung_bp3d_tham_khao.py`: giải phẫu BodyParts3D.
4. Mã đang có, sẽ dùng lại:
   - `Assets/Scripts/Editor/LabBuilder/Tang2/LabSceneBuilder.Floor2.BioLab.cs`, hàm `AnatomyArea` (khoảng dòng 576): nơi dựng **Bàn giải phẫu số**, **Bộ xương trên giá**, **Bục mô hình bán thân**.
   - `Assets/Scripts/Editor/LabBuilder/Tang2/Bp3dImporter.cs`: `ImportBodyParts3D()` đã tạo mesh vào `Assets/Lab/Tang2/GiaiPhau/`.
   - `Assets/Scripts/Demo/Bio/DigitalAnatomyTable.cs`: đổi lớp xương / cơ / da, danh sách `OrganIds`, `OrganNames`.
   - `Assets/Scripts/Demo/TouchHud.cs` (`RegisterQuickActionSlot`), `Demo/NutHieuUng.cs`, `Demo/UiMotion.cs`, `Demo/GlassKit.cs`, `Demo/ModernUI.cs`: giao diện kính mờ và hiệu ứng nút đang dùng.
   - `Assets/Scripts/Demo/HandController.cs` (chỗ dựng gợi ý `Hints.Add(("Nhấn", ...))`, khoảng dòng 940), `Demo/EquipmentInfo.cs` (`IDemoUsable`), `Interaction/Bio/IBioTarget.cs` (`IInteractable`).
   - `Assets/Scripts/Environment/DesktopViewer.cs` (`UiCapture`, `GoTo`), `Environment/Easing.cs`, `Environment/Tang2/ReducedMotion.cs`.
   - `Assets/Scripts/Interaction/Bio/CloseUpView.cs`: cách đưa camera lại gần rồi trả về.
   - `Assets/Scripts/Rendering/ZoneStreamer.cs` (`Keep`, `Release`, `WaitReadyAt`), `Rendering/ZoneMap.cs`.
   - `Assets/Scripts/Demo/Instruments/InstrumentScreen.cs` và `FabricationWorkspace.cs` / `AssemblyManager.cs`: **cách dự án hiện đưa người chơi vào một không gian ảo** (màn hình máy ở y −500, xưởng lắp ráp ảo). Cổng mới phải theo đúng kiểu này.
   - `tools/kiem_thu_unity/KhktAutoTest.cs`: cách viết chế độ `KHKT_TEST`.

Đọc xong, **viết kế hoạch ngắn** (tệp, lớp, thứ tự) và trả lời rõ 3 câu trước khi code:

- Không gian ảo hiện có được vào bằng cách nào (camera riêng hay dời người chơi)? Cổng mới sẽ làm giống ở điểm nào?
- Làm sao để `ZoneStreamer` **không gỡ khu `phong_sinh`** và không tải nhầm khu tầng 1 trong lúc người chơi ở phòng ảo? (Xem cạm bẫy ở mục 7.)
- `bp3d.bin.gz` có nhóm nội tạng không (`layerOrgans` của bàn giải phẫu hiện có thể rỗng)? Nếu có thì nhập thêm, nếu không thì ghi rõ và dùng 3 lớp xương / cơ / da.

Chỗ nào prompt này mâu thuẫn với code thật thì **theo code thật và ghi lại** trong tài liệu.

---

## 2. Phần 1 – Cổng: nút hiện khi lại gần mô hình

### Vị trí

Cổng gắn vào **Khu giải phẫu** của phòng thí nghiệm sinh học tầng 2 (khu `ZoneMap.BioLab`, sàn y = 3,8). Tọa độ cục bộ trong phòng:

| Mô hình | Vị trí (x, z) |
|---|---|
| Bàn giải phẫu số | 18,25 ; 11,25 |
| Bục mô hình bán thân | 19,4 ; 12,3 |
| Bộ xương trên giá | 19,35 ; 13,15 |

Tâm cổng đặt ở khoảng trống phía tây ba mô hình này (gần 18,3 ; 12,6, tự kiểm tra không đè lên lối đi kẻ vạch hay chân bàn). Một cổng duy nhất cho cả cụm, không làm ba cổng.

### Hình ảnh ngoài thế giới (luôn có, rất nhẹ)

- Một **vòng sáng mảnh màu lục lam** trên sàn (bán kính 0,55 m) kèm một **lục giác** nhỏ ở giữa, lấy đúng màu app cũ: `#22D3EE`, `#10B981`, `#06B6D4`.
- Khi chưa có ai lại gần: vòng sáng "thở" rất chậm (chu kỳ 4 s, độ sáng 35 % ↔ 55 %).
- Vật liệu unlit, cộng sáng, không đổ bóng, không nhận bóng, không đèn thật. Chỉ một draw call.
- Phong cách hologram tối **chỉ** được phép ở vòng sáng này và bên trong phòng ảo. Không đổi ánh sáng hay vật liệu của phòng thí nghiệm thật.

### Thẻ lời mời (hiện khi lại gần)

- **Hiện** khi mắt người chơi cách tâm cổng ≤ 2,2 m **và** đang nhìn về phía cổng (góc lệch ≤ 60°). **Ẩn** khi > 2,8 m. Hai ngưỡng khác nhau để thẻ không nhấp nháy ở mép vùng.
- Kiểm tra khoảng cách bằng bình phương khoảng cách, 10 lần/giây là đủ, không dùng `Update` mỗi khung hình cho việc này, không dùng `FindObjectOfType` lặp lại.
- Thẻ là kính mờ đúng kiểu `GlassKit` / `ModernUI` đang dùng, nội dung:
  - biểu tượng xoắn kép ADN hoặc lục giác;
  - tiêu đề **"Phòng Sinh học 3D"**;
  - dòng phụ "Giải phẫu người thật · 15 cơ quan · 6 cảnh hiển vi";
  - nút chính **"Vào phòng"**.
- **iPad:** thẻ nằm trên màn hình, phía dưới giữa, trên dock; nút chạm ≥ 65 đơn vị canvas (chuẩn 44 pt như `TouchHud`), gắn `NutHieuUng`. Không che vùng vuốt xoay góc nhìn bên phải và cần di chuyển bên trái. Tôn trọng `safeArea`.
- **Máy tính:** cùng thẻ đó, thêm nhãn phím theo đúng cách `HandController` đang hiện gợi ý "Nhấn". Đầu vào đi qua lớp `Input/` sẵn có, **không** đọc phím trực tiếp.
- Hiện: `UiMotion.Popup` (thu phóng 0,95 → 1 kèm mờ dần, 0,20 s). Ẩn: `UiMotion.FadeOut` (0,12 s). Lúc thẻ hiện, vòng sáng trên sàn sáng lên 100 % và quay chậm.
- Không cho bấm khi: đang cầm dụng cụ (hiện dòng "Hãy đặt dụng cụ xuống trước"), đang mở màn hình máy / cận cảnh (`CloseUpView.IsActive`, `DesktopViewer.UiCapture > 0`), hoặc giáo viên đang khóa thao tác (`ClassRules`).

---

## 3. Phần 2 – Phòng Sinh học 3D (dựng lại từ app cũ)

### Đích phải đạt: đúng phòng "BIO-LAB 3D" của app cũ (ưu tiên cao nhất trong mục 3)

Chủ dự án đã xác nhận bằng ảnh chụp: phòng cần đưa vào Unity **chính là trang "Phòng Lab Sinh học 3D · CHEMISTRY"** (`/sinh-hoc`), và nó phải **nằm hẳn trong dự án Unity này**, chạy không cần mạng. Các tiểu mục "Hình ảnh" và "Nội dung" bên dưới chỉ là mức tối thiểu; chỗ nào khác với app cũ thì **theo app cũ**.

**Mã nguồn gốc nằm trong lịch sử git của chính kho này**, commit `5e6066b7` ("Release CHEMISTRY v1.1.0"). Lấy ra để đọc, **không** chép vào `Assets/`:

```bash
git show 5e6066b7:frontend/biology-lab-3d.html > /tmp/biology-lab-3d.html
git archive 5e6066b7 frontend/sinh-hoc | tar -x -C /tmp
```

Tệp HTML dài 7188 dòng, chia 20 mục đánh số (`// 1. DỮ LIỆU GIẢI PHẪU` … `// 20. TAB HOÁ HỌC ↔ SINH HỌC`). Đọc hết các mục 1 – 17 và thư mục `frontend/sinh-hoc/giai-thich/` trước khi dựng. Có thể mở tệp HTML bằng một máy chủ tĩnh để xem tận mắt (trang ghi rõ "mở trực tiếp cũng chạy").

Những gì người dùng nhìn thấy ở app cũ và **phải có lại** trong Unity:

| Thành phần | Chi tiết ở app cũ |
|---|---|
| Cơ thể | Bộ xương, cơ, mạch máu đỏ / xanh, da từ BodyParts3D, cộng nội tạng dựng bằng code (tim 4 ngăn, phổi và cây phế quản, não – tủy sống – dây thần kinh, thận – niệu quản – bàng quang, gan – túi mật – dạ dày – ruột). Tổng **352 cấu trúc** chọn được |
| Cột trái "Khám phá" | Ô tìm cấu trúc; 7 nút cơ quan (Tim, Não, Phổi, Gan, Thận, Dạ dày, Ruột); ba tab **Vùng / Hệ cơ quan / Lớp**; 6 vùng (Đầu và cổ 9, Ngực 22, Bụng 22, Chậu – hông 9, Tay 12, Chân 14 cấu trúc); Bảng tra cứu giải phẫu; ô "Nhãn thông minh" |
| Giải thích từng bước | Hai bài **Vòng tuần hoàn nhỏ** và **Vòng tuần hoàn lớn** có mũi tên 3D, camera tự lia, bảng lời giải, câu kiểm tra (chuyển từ `giai-thich/data/*.js`) |
| Thanh công cụ phải | Đặt lại camera; Giải thích từng bước; Dòng hạt máu chạy liên tục; **X-Quang**; **Tách lớp cơ quan**; **Soi hiển vi** (6 cảnh); Hiện / ẩn nhãn 3D; Tạm dừng |
| Khung nhịp tim | Số nhịp tim góc trên, tim đập theo nhịp; trạng thái sinh lý và chỉ số hóa sinh (mục 13 của tệp gốc) |
| Góc nhìn | Trước / Sau / Bên / Phối cảnh |
| Kiểu phòng | 5 kiểu: **Phòng giải phẫu (Tối)** – mặc định, nền đen trơn như ảnh chụp; Phòng Lab 3D – phòng hologram có bệ và vòng sáng; Buồng tối giải phẫu; Studio Y khoa (nền trắng); X-Quang |
| Ghi công | "Xương, cơ, da: BodyParts3D © DBCLS · CC BY 4.0" và số cấu trúc đang hiện |

Chuyển đổi cho phù hợp iPad và dự án này:

- Tab "Hoá học" của app cũ thay bằng nút **"Về phòng thí nghiệm"** (chạy hiệu ứng đóng).
- **AI Bio-Tutor:** không gọi máy chủ app cũ. Dùng `AiClient` và `server/ai` của dự án này; khi không có mạng thì dùng bản "AI nội bộ" theo luật và từ khóa như app cũ (mục 18 của tệp gốc). Làm ở giai đoạn cuối.
- **Bỏ:** Tải mô hình .GLB, Giải thích từ ảnh, điều khiển bằng tay qua camera.
- Giao diện dựng bằng uGUI kính mờ sẵn có (`GlassKit`, `ModernUI`), bố cục và tên nút giữ như app cũ; cột trái thu gọn được để nhường chỗ cho mô hình.
- Mô tả phòng hologram ở tiểu mục "Hình ảnh" bên dưới áp dụng cho kiểu **Phòng Lab 3D**. Kiểu mặc định là **Phòng giải phẫu (Tối)**; với kiểu này, bước "Phòng thức dậy" của hiệu ứng mở (mục 4, bước 6) là bóng đổ dưới chân và các nhãn hiện dần thay cho vòng sáng.
- Nội tạng app cũ dựng bằng code Three.js: dựng lại bằng `LabKit` (`Lathe`, `Ball`, `Cyl`…) lúc dựng cảnh và lưu thành mesh, không tạo hình lúc chạy.

### Dựng ở đâu

- Dựng bằng code theo đúng luật "toàn bộ cảnh dựng bằng code": thêm tệp `Assets/Scripts/Editor/LabBuilder/Tang2/LabSceneBuilder.Floor2.Portal.cs` (`partial class LabSceneBuilder`, namespace `Khkt.EditorTools`), gọi từ chỗ dựng tầng 2.
- Phòng ảo đặt ở **vùng y < 0**, là vùng `contracts.md` dành cho không gian ảo. Chọn độ cao không đụng màn hình máy (y −500) và xưởng lắp ráp ảo; ghi độ cao đã chọn thành hằng số có tên.
- Phòng là **một gốc riêng, tắt sẵn** (`SetActive(false)`), chỉ bật trong lúc chuyển cảnh. Khi tắt thì không vẽ, không chạy script nào.

### Hình ảnh (bám theo `phong_lab_cu_tham_khao.js`)

- Nền tối xanh đen `#0A1222`, không có tường; xa dần thì chìm vào tối (sương mù hoặc gradient trên một mái vòm unlit).
- **Sàn tròn** bán kính 10 m, hơi bóng. **Lưới** 20 × 20 m màu `#06B6D4` / `#0E3A4A`, độ mờ 28 %.
- **Bệ hologram** hình trụ (bán kính 1,05 → 1,2 m, cao 0,14 m), phát sáng nhẹ.
- **4 vòng sáng:** vòng tròn sát bệ `#22D3EE`, lục giác bên trong `#10B981`, hai vòng xuyến mảnh ở độ cao 0,6 m và 2,4 m. Hai vòng xuyến quay ngược chiều nhau, chậm.
- **Chùm sáng** hình trụ hở quanh bệ, cao 3,9 m, đậm ở chân, tan dần lên trên, có **vạch sáng chạy lên** (đúng công thức trong shader cũ: `pow(1 - v, 2.2) * 0.22` cộng vạch `sin(v * 40 - t * 3)`).
- **Hạt bụi sáng** bay chậm: một hệ hạt duy nhất, tối đa 150 hạt.
- Toàn bộ vật liệu trong phòng là unlit hoặc lit tối giản; **không có đèn thời gian thực đổ bóng**; mọi thứ tĩnh thì gộp batch.

### Nội dung

- **Cơ thể người BodyParts3D** đứng trên bệ, cao 1,7 m, **dùng lại đúng các mesh** `Bp3dImporter` đã tạo (không nhập lần hai, không nhân đôi dữ liệu).
- Thanh chọn lớp: **Da / Hệ cơ / Bộ xương** (thêm **Nội tạng** nếu dữ liệu có). Đổi lớp có hoạt cảnh tan – hiện 0,35 s.
- Tim đập 72 lần/phút, phổi thở 16 nhịp/phút nếu có lớp nội tạng (lấy nhịp như `DigitalAnatomyTable`).
- **15 cơ quan:** chạm vào cơ quan hoặc chọn trong danh sách thì camera lia tới, cơ quan sáng viền, hiện **thẻ kiến thức** kính mờ đọc từ `kien_thuc/<id>.json`: tên, vị trí, cấu tạo, chức năng, cơ chế, liên hệ thực tế, điểm hay thi. Có nút **"Ôn tập"** dùng mảng `trac_nghiem`.
- **6 cảnh hiển vi** từ `vi_mo_tham_khao.js`: mở từ cơ quan tương ứng (phổi → phế nang, thận → nephron, não → synapse, cơ → sarcomere, ruột non → tế bào viền, gan → tiểu thùy gan). Giai đoạn đầu cho phép làm bản đơn giản (hình khối + nhãn), nhưng phải đủ 6 cảnh.
- **Điều khiển:** kéo một ngón để xoay quanh cơ thể, chụm hai ngón để phóng to / thu nhỏ (giới hạn 0,8 → 4 m), chạm đúp để về góc nhìn mặc định. Trên máy tính: kéo chuột và con lăn.
- **Nút "Về phòng thí nghiệm"** luôn ở góc trên trái, ≥ 65 đơn vị canvas. Phím Esc trên máy tính cũng thoát.
- **Ghi công bắt buộc**, luôn nhìn thấy ở góc dưới: "BodyParts3D © The Database Center for Life Science · CC BY 4.0".
- Dữ liệu `kien_thuc/` phải nằm trong bản build: chép vào `Assets/StreamingAssets/data/sinh/kien_thuc/` bằng công cụ chép dữ liệu sẵn có (xem `tools/`), không chép tay. Đọc bằng `Path.Combine(Application.streamingAssetsPath, ...)`. Đọc **một lần** lúc vào phòng rồi giữ lại, không đọc tệp trong lúc đang chuyển cảnh.
- Mọi chữ trên giao diện bằng tiếng Việt, số thập phân dùng dấu phẩy.

---

## 4. Phần 3 – Hiệu ứng chuyển cảnh MỞ (tổng khoảng 1,8 s)

Viết thành **máy trạng thái** rõ ràng: `Nghi → DangMo → DangTai → HienPhong → TrongPhong → DangDong → Nghi`. Mọi nút bấm chỉ có tác dụng ở đúng trạng thái của nó; bấm hai lần liên tiếp không được chạy hai lần.

| Bước | Thời gian | Diễn ra |
|---|---|---|
| 1. Xác nhận | 0 – 0,20 s | Nút co 0,95 lần (`NutHieuUng`), tiếng click, thẻ lời mời thu lại vào tâm vòng sáng. Khóa di chuyển và HUD (`DesktopViewer.UiCapture++`, ẩn `TouchHud` bằng mờ dần). Lưu vị trí, hướng nhìn, góc ngẩng, FOV của người chơi. |
| 2. Quét mô hình | 0,20 – 0,80 s | Một **vạch quét sáng** chạy từ chân lên đầu bộ xương / mô hình bán thân (shader viền sáng theo độ cao, không tạo mesh mới). Vòng sáng dưới sàn nở rộng ra 1,5 lần. Camera **tiến nhẹ** về phía mô hình 0,4 m và FOV thu hẹp 60° → 50° (`Easing.EmphasizedDecelerate`). |
| 3. Phủ lục giác | 0,60 – 1,10 s | **Lưới lục giác** phủ màn hình, lan theo sóng tròn **từ vị trí mô hình trên màn hình** ra bốn góc. Mỗi ô lật sáng viền lục lam rồi đặc lại thành nền `#0A1222`. Kèm tăng nhẹ quang sai màu và tối viền (đổi `weight` của một Volume cục bộ riêng). |
| 4. Chờ sẵn sàng | giữ đến khi xong | Màn hình đã phủ kín. Lúc này mới: bật gốc phòng ảo, chuyển camera sang phòng ảo, tắt vẽ phòng thật. Hiện vòng tiến trình mảnh + chữ "Đang mở Phòng Sinh học 3D…" **chỉ khi** phải chờ quá 0,3 s. **Tuyệt đối không mở màn khi phòng chưa sẵn sàng.** |
| 5. Mở màn | 0,50 s | Lưới lục giác tan ngược lại, **từ tâm màn hình ra ngoài**, lộ ra phòng hologram còn tối. |
| 6. Phòng thức dậy | 0,60 s, gối lên bước 5 | Lưới sàn lan sáng từ bệ ra ngoài → 4 vòng sáng bật lần lượt, mỗi vòng cách nhau 0,08 s → chùm sáng dâng lên. |
| 7. Cơ thể hiện hình | 0,70 s | Cơ thể **hiện dần từ chân lên đầu** bằng shader cắt theo độ cao, mép cắt là một vạch sáng lục lam có nhiễu hạt. |
| 8. Giao diện vào | 0,25 s | Thanh chọn lớp, danh sách cơ quan, nút "Về phòng thí nghiệm" trượt vào bằng `UiMotion.Popup`, lệch nhau 0,04 s. Mở khóa điều khiển xoay. |

Âm thanh: một tiếng "vút" tổng hợp ngắn ở bước 3 và một tiếng ngân nhẹ ở bước 7; tạo bằng code (`AudioClip.Create`) hoặc dùng lại `BioFeedback`; tôn trọng công tắc `NutHieuUng.SoundEnabled`.

---

## 5. Phần 4 – Hiệu ứng chuyển cảnh ĐÓNG (tổng khoảng 1,4 s)

Trình tự ngược, nhanh hơn một chút để người dùng không phải chờ:

| Bước | Thời gian | Diễn ra |
|---|---|---|
| 1 | 0 – 0,15 s | Nút co lại, giao diện phòng ảo mờ dần, đóng thẻ kiến thức đang mở. Khóa điều khiển. |
| 2 | 0,15 – 0,60 s | Cơ thể **tan từ đầu xuống chân** (đảo chiều shader cắt). Chùm sáng hạ xuống, 4 vòng sáng tắt theo thứ tự ngược. |
| 3 | 0,45 – 0,90 s | Lưới lục giác phủ màn hình, lan **từ ngoài vào tâm**. |
| 4 | khi đã phủ kín | Tắt gốc phòng ảo, bật lại vẽ phòng thật, trả camera về **đúng vị trí, hướng nhìn, góc ngẩng đã lưu** (sai số ≤ 1 cm, ≤ 0,5°), FOV 50°. Chờ `ZoneStreamer` báo khu `phong_sinh` sẵn sàng. |
| 5 | 0,45 s | Lưới lục giác tan **từ vị trí mô hình trên màn hình** ra ngoài; FOV 50° → 60°; một vạch quét chạy từ đầu xuống chân mô hình như lời chào kết. |
| 6 | 0,20 s | HUD hiện lại, mở khóa di chuyển (`UiCapture--`). Thẻ lời mời **chờ 0,8 s** rồi mới được hiện lại, để người dùng không bấm nhầm quay vào. |

Mở và đóng phải **đối xứng về hình ảnh**: cùng lưới lục giác, cùng màu, cùng đường cong chuyển động. Người xem phải cảm thấy "đi vào" và "đi ra" cùng một cánh cổng.

---

## 6. Kỹ thuật hiệu ứng và hiệu năng

- **Lưới lục giác phủ màn hình:** một `RawImage` trên Canvas Screen Space – Overlay riêng (thứ tự vẽ cao nhất), dùng **một shader UI thủ tục** (tính ô lục giác, khoảng cách tới tâm sóng, viền sáng trong fragment shader). Tham số: `_TienDo` (0 → 1), `_Tam` (tâm sóng theo tọa độ màn hình), `_Chieu` (mở / đóng), `_TiLeManHinh`.
  - **Không** dùng GrabPass, **không** bật Opaque Texture, **không** thêm Renderer Feature, **không** sửa `Mobile_RPAsset` hay các asset URP. Lưới chỉ che cảnh, không bóp méo cảnh, nên rẻ và ổn định trên iPad.
  - Kích thước ô lục giác tính theo điểm ảnh logic để iPad và laptop trông như nhau.
- **Shader hiện hình cơ thể:** URP, cắt theo `_DoCaoCat` trong không gian thế giới, mép sáng rộng 3 cm, nhiễu bằng hàm băm (không cần texture). Đặt tham số bằng `MaterialPropertyBlock`, không tạo bản sao vật liệu.
- **Vạch quét mô hình ở phòng thật:** vỏ viền hoặc tham số phát sáng theo độ cao, cũng qua `MaterialPropertyBlock`; xong hiệu ứng phải trả vật liệu về **y nguyên**.
- **Hậu kỳ:** một `Volume` toàn cục riêng, ưu tiên cao, bình thường `weight = 0`. Chỉ đổi `weight` trong lúc chuyển cảnh. Chỉ dùng hiệu ứng mà cấu hình URP hiện tại đã bật.
- **Thời gian:** mọi hoạt cảnh dùng `Time.unscaledDeltaTime`, chạy bằng coroutine hoặc một `Update` duy nhất của bộ điều khiển chuyển cảnh. Bộ điều khiển tự `enabled = false` khi ở trạng thái nghỉ.
- **Không cấp phát mỗi khung hình:** không `new`, không nối chuỗi, không LINQ, không `GetComponent` trong vòng lặp hoạt cảnh. Bộ đệm `WaitForSeconds` / đối tượng dùng lại.
- **Làm ấm trước:** khi người chơi bước vào khu `phong_sinh`, chuẩn bị sẵn (shader đã biên dịch, vật liệu đã tạo, JSON danh mục đã đọc) theo kiểu rải ra vài khung hình, để lần bấm đầu tiên không bị khựng.
- **Trong phòng ảo:** phòng thật không được vẽ (camera không nhìn thấy / tắt layer), nhưng **mô phỏng vẫn chạy** (tủ ấm, hẹn giờ, khuẩn lạc tiếp tục). Ngược lại, khi ở phòng thật thì phòng ảo tắt hẳn.
- **Giảm chuyển động:** nếu `ReducedMotion.IsEnabled` thì bỏ toàn bộ quét, lục giác, hiện hình; thay bằng **mờ chéo 0,25 s** sang nền tối rồi mờ ra. Thứ tự trạng thái và việc lưu / trả vị trí giữ nguyên.
- **Ngân sách:** thêm không quá 6 draw call ở phòng thật; phòng ảo dưới 60 draw call và dưới 400 nghìn tam giác; bộ nhớ sau 20 lần vào – ra không tăng quá 5 MB.

---

## 7. Cạm bẫy phải xử lý

1. **`ZoneStreamer` theo dõi vị trí người xem.** `ZoneMap.At` coi y < 3,6 là tầng 1. Nếu dời cả người chơi xuống phòng ảo, bộ tải khu sẽ gỡ `phong_sinh` và tải nhầm khu tầng 1, lúc về sẽ khựng. Cách làm đúng: theo kiểu không gian ảo sẵn có của dự án, **không dời gốc người chơi** nếu tránh được; trong mọi trường hợp gọi `ZoneStreamer.Instance.Keep(chủ, giây, vị trí cổng)` suốt thời gian ở phòng ảo và `Release` khi đã về.
2. **Một bộ camera góc nhìn thứ nhất duy nhất** (`contracts.md` mục 3). Nếu cần camera cho phòng ảo thì làm đúng như `InstrumentScreen` / xưởng lắp ráp đang làm; không thêm AudioListener thứ hai.
3. **Ứng dụng bị đưa xuống nền giữa lúc chuyển cảnh** (`OnApplicationPause`): khi quay lại phải kết thúc gọn về trạng thái đích, không kẹt ở màn hình phủ kín.
4. **Lỗi khi chuẩn bị phòng** (thiếu mesh, thiếu JSON): không được treo. Quá 10 s hoặc có ngoại lệ thì đóng màn, trả người chơi về, báo "Chưa mở được Phòng Sinh học 3D" bằng kênh thông báo sẵn có. Thiếu `bp3d` thì dùng bản thay thế bằng khối cơ bản như `Bp3dImporter` đang làm.
5. **Phòng học chung (Netcode):** cổng là việc **cục bộ**. Người khác vẫn thấy bạn đứng ở khu giải phẫu. Không đồng bộ phòng ảo, không làm hỏng `LabPresence`.
6. **Trình chiếu / lớp học từ web** (`PresentationClient`, `ClassRules`): nếu giáo viên chuyển trang hoặc khóa thao tác khi học sinh đang ở phòng ảo thì chạy hiệu ứng đóng rồi mới làm theo lệnh.
7. **Xoay màn hình, đổi kích thước cửa sổ** giữa lúc chuyển cảnh: cập nhật `_TiLeManHinh`, lưới lục giác không bị méo.
8. **Không sửa hành vi tầng 1** và không đổi cách dùng hiện có của `DigitalAnatomyTable` (bấm vào bàn vẫn đổi lớp như cũ). Cổng là vật bấm riêng.
9. Tên lớp phải **trùng tên tệp** (Unity chỉ lưu được thành phần khi trùng). Tên tệp, thư mục mới không dấu, không cách.

---

## 8. Tệp cần tạo (đề xuất, đặt tên theo kiểu code xung quanh)

| Tệp | Việc |
|---|---|
| `Assets/Scripts/Demo/Bio/Portal/BioPortal.cs` | Vòng sáng, kiểm tra lại gần, thẻ lời mời, điều kiện cho bấm |
| `Assets/Scripts/Demo/Bio/Portal/BioPortalTransition.cs` | Máy trạng thái, toàn bộ trình tự mở / đóng, lưu và trả vị trí |
| `Assets/Scripts/Demo/Bio/Portal/HoloBioRoom.cs` | Phòng ảo: lớp cơ thể, chọn cơ quan, xoay / phóng, nhịp tim – thở |
| `Assets/Scripts/Demo/Bio/Portal/OrganKnowledgeCard.cs` | Đọc `kien_thuc/*.json`, thẻ kiến thức, ôn tập |
| `Assets/Scripts/Demo/Bio/Portal/MicroSceneViewer.cs` | 6 cảnh hiển vi |
| `Assets/Lab/Tang2/Shader/HexWipe.shader` | Lưới lục giác phủ màn hình |
| `Assets/Lab/Tang2/Shader/HoloReveal.shader` | Hiện hình / tan theo độ cao |
| `Assets/Lab/Tang2/Shader/HoloBeam.shader` | Chùm sáng có vạch chạy |
| `Assets/Scripts/Editor/LabBuilder/Tang2/LabSceneBuilder.Floor2.Portal.cs` | Dựng cổng ở khu giải phẫu và dựng phòng ảo |
| `tools/kiem_thu_unity/KhktAutoTest.cs` (thêm chế độ) | `KHKT_TEST=cong_sinh_hoc` |
| `docs/27-cong-phong-sinh-hoc-3d.md` | Tài liệu, ảnh chụp, số đo |

Shader phải có mặt trong bản build iOS: tham chiếu từ vật liệu được lưu thành asset, hoặc thêm vào danh sách Always Included; **không** dựa vào `Shader.Find` với shader không ai tham chiếu.

---

## 9. Các giai đoạn

1. **GĐ1 – Cổng:** vòng sáng + thẻ lời mời + điều kiện lại gần. Bấm nút mới chỉ ghi log. Kiểm thử trên iPad và máy tính.
2. **GĐ2 – Phòng ảo tĩnh:** dựng phòng hologram và cơ thể, vào – ra bằng **mờ chéo đơn giản**. Lưu / trả vị trí đúng, `ZoneStreamer` không gỡ khu.
3. **GĐ3 – Chuyển cảnh mở và đóng đầy đủ** theo mục 4, 5, 6. Thêm nhánh Giảm chuyển động.
4. **GĐ4 – Nội dung:** 15 cơ quan, thẻ kiến thức, ôn tập, 6 cảnh hiển vi, ghi công.
5. **GĐ5 – Đo và tinh chỉnh:** đo FPS, thời gian khung hình dài nhất, bộ nhớ; chụp ảnh; viết `docs/27`.

Xong mỗi giai đoạn: chạy kiểm thử mục 10, commit, đẩy nhánh.

---

## 10. Kiểm thử và tiêu chí hoàn thành

Thêm chế độ `KHKT_TEST=cong_sinh_hoc` vào `KhktAutoTest.cs`, làm như học sinh, ghi log `[KHKT-PLAY]`:

1. Đứng cách cổng 4 m: thẻ **không** hiện. Đi tới 2 m: thẻ hiện. Lùi ra 3 m: thẻ ẩn. Đứng ở mép 2,5 m đi qua lại: thẻ không nhấp nháy.
2. Bấm "Vào phòng": đi qua đủ các trạng thái theo đúng thứ tự, vào tới `TrongPhong`. Chụp ảnh ở từng bước: quét, phủ lục giác nửa chừng, phủ kín, mở màn nửa chừng, cơ thể hiện nửa người, phòng hoàn chỉnh.
3. Đổi đủ các lớp, chọn lần lượt 15 cơ quan (thẻ nào cũng có nội dung, không ô trống), mở đủ 6 cảnh hiển vi.
4. Bấm "Về phòng thí nghiệm": về `Nghi`, vị trí lệch ≤ 1 cm, hướng nhìn lệch ≤ 0,5°, FOV về đúng giá trị cũ, HUD hiện lại, đi lại được.
5. Bấm nút vào hai lần liên tiếp trong 0,1 s, và bấm ra ngay khi đang mở: không kẹt, không chạy đôi.
6. **20 lần vào – ra liên tục:** không lỗi, không cảnh báo mới trong log, bộ nhớ tăng < 5 MB, `ZoneStreamer.Unloads` của khu `phong_sinh` không tăng.
7. Bật Giảm chuyển động: vào – ra bằng mờ chéo, kết quả vị trí vẫn đúng.
8. Ghi **thời gian khung hình dài nhất** trong lúc mở và đóng: phải < 33 ms. FPS trung bình trong phòng ảo ≥ 60.
9. **Hồi quy:** các chế độ cũ (`tang2`, `khu`, `may`, `may2`, `may3`, `may4`, bài đầy đủ) vẫn đạt. `python -m unittest discover -s tests` đạt hết.

Kiểm thử chụp ảnh phải chạy **có đồ họa** (không dùng `-nographics`, vì shader chuyển cảnh cần GPU). Dựng Unity ở thư mục dự án chính `~/Developer/Antigravity`, **không** dựng trong `.claude/worktrees`.

Ngoài bài tự động, **chạy thật trên iPad** một lần và ghi vào `docs/27`: có mượt không, nút có dễ chạm không, chữ có đọc được không.

---

## 11. Git và giao nộp

- Làm trên nhánh riêng `tang2/cong-sinh-hoc-3d`, mở PR, không đẩy thẳng `main`. Cấm `push --force`, cấm `reset --hard` trên nhánh đã đẩy.
- **Không commit** `Assets/Scenes/**/*.unity` và `OcclusionCullingData.asset` (cảnh dựng lại từ code). **Có commit** `.meta` của script, shader, dữ liệu mới.
- **Không tự đưa lên web / không tự phát hành.** Chỉ làm khi chủ dự án bảo.
- Báo cáo cuối cùng gồm: danh sách tệp đã thêm / sửa; chỗ nào làm khác prompt và vì sao; bảng số đo (FPS, khung hình dài nhất khi mở / đóng, bộ nhớ sau 20 vòng, số draw call); ảnh chụp 6 bước chuyển cảnh; những gì **chưa làm được hoặc chưa kiểm chứng**, nói thẳng, không viết "đã xong" cho phần chưa chạy thử.
