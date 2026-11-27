# Prompt cho Antigravity: Liquid Glass giống Apple cho các màn ngoài phòng lab

Viết ngày 27/11/2026, sau khi xem trực tiếp trang và video của Apple (WWDC25, WWDC26, HIG) và đọc mã hiện có của VERA Lab.
Cách dùng: dán toàn bộ phần dưới vạch kẻ vào Antigravity, hoặc bảo nó "đọc và làm theo tệp này".

---

## 0. Vai trò và mục tiêu

Bạn là kỹ sư đồ họa Unity (URP, HLSL, C#) kiêm nhà thiết kế chuyển động. Việc của bạn là làm lại giao diện **ngoài phòng thí nghiệm** của app VERA Lab
theo đúng vật liệu **Liquid Glass** của Apple (iOS 26/27, macOS Tahoe 26 / macOS 27), giống từng chi tiết:
độ trong, độ mờ, độ cong của mép, độ lồi của thấu kính, ánh sáng viền, bóng đổ, lực nhấn và chuyển động.

Liquid Glass **không phải** là "kính mờ + bo góc". Nó là một vật liệu vừa là **kính** (bẻ cong ánh sáng như thấu kính) vừa là **chất lỏng**
(co giãn như gel, dính vào nhau như giọt nước, ngưng tụ thay vì hiện mờ dần). Nếu chỉ làm mờ nền rồi phủ một lớp trắng, kết quả là **nhựa mờ**.
Đó đúng là lỗi của bản hiện tại và là thứ phải bỏ.

Yêu cầu riêng của chủ dự án:
1. **Bỏ hết màu trên kính.** Kính chỉ trong suốt và hơi mờ, không nhuộm xanh, không nhuộm màu nhấn, không dải màu, không lưới bản vẽ.
   Các bảng nổi, thẻ "thư mục", thanh bên, nút: tất cả là kính không màu.
2. **Chỉ làm ở ngoài phòng lab.** Màn chọn lối vào, đăng nhập và cài đặt, bảng điều khiển, Giải bài tập.
   Bên trong phòng lab (cảnh 3D, HUD, máy phân tích, chat robot…) **không đụng tới** và phải giữ y nguyên từng điểm ảnh.
3. Các thẻ / thư mục **nổi lên** khỏi nền như những miếng kính dày có nước bên trong, phản ứng với chuột và chạm như chất lỏng.

## 1. Luật dự án phải theo (đọc trước khi làm)

- Đọc `AGENTS.md`. Thứ tự nền tảng: **macOS là chính → Windows → iPad cuối cùng.** Mọi thứ thiết kế và kiểm tra trên Mac trước, bằng chuột và bàn phím.
  Không dùng API, đường dẫn hay lệnh chỉ có trên Mac. Việc của iPad không được làm chậm Mac và Windows.
- Làm trong `~/Developer/Antigravity`, nhánh `main` (một bản duy nhất). Trước khi sửa: `git branch --show-current` phải là `main`
  và `git rev-list --count HEAD..origin/main` phải bằng 0. Không tạo worktree trong thư mục ẩn rồi dựng Unity ở đó.
- Dựng và kiểm thử Unity: `~/AppBuild/VERA_claude/dong_bo_va_chay.sh <thư mục dự án> <Lớp.Hàm> [tên log]`
  (chạy `-batchmode -buildTarget StandaloneOSX`, không dùng `-nographics`). App Mac: `Khkt.EditorTools.MacBuild.Build` → `~/AppBuild/VERA_mac/VERA Lab.app`.
- Kiểm thử Unity dùng chung PlayerPrefs với app thật (`~/Library/Preferences/unity.DefaultCompany.VERA Lab.plist`).
  Sau mỗi lượt kiểm thử, đọc `plutil -p` và trả `KHKT_GIAM_TRONG_SUOT`, `KHKT_GIAM_CHUYEN_DONG` về 0, `KHKT_KINH_LONG` về 1.
  Nếu không, app mở ra sẽ mất kính và mất chuyển động.
- **Không** đưa lên web. Commit chọn lọc đúng tệp của mình, lời commit tiếng Việt. Không commit ảnh chụp từ video của Apple (bản quyền):
  ảnh so sánh để ở `~/AppBuild/kinh_long_so_sanh/`, ngoài repo.
- Ghi tiến độ vào `docs/kinh-long-apple/tien-do.md` sau mỗi giai đoạn (đã xong gì, đang dở gì, số đo), để phiên sau làm tiếp được nếu hết lượt.

## 2. Nguồn gốc. Hãy tự mở và xem lại trước khi code

Đừng làm theo trí nhớ. Mở các trang sau bằng trình duyệt của bạn, đọc lời thoại (tab *Transcript*) và **tua video đến đúng giây** để nhìn.

| Nguồn | Xem gì |
|---|---|
| WWDC25 *Meet Liquid Glass* — https://developer.apple.com/videos/play/wwdc2025/219/ | Nguồn chính về quang học và chuyển động. Mốc thời gian ở mục 4. |
| WWDC25 *Get to know the new design system* — https://developer.apple.com/videos/play/wwdc2025/356/ | Độ cong đồng tâm (3:21–6:16), lớp điều hướng, scroll edge effect (11:02–12:22), thanh bên (12:34). |
| WWDC25 *Build a SwiftUI app with the new design* — https://developer.apple.com/videos/play/wwdc2025/323/ | `glassEffect`, `.interactive()`, `GlassEffectContainer`, `glassEffectID` (18:09–21:00). |
| HIG Materials — https://developer.apple.com/design/human-interface-guidelines/materials | Regular và Clear, lớp làm tối 35 %, không dùng kính ở lớp nội dung. |
| Applying Liquid Glass to custom views — https://developer.apple.com/documentation/swiftui/applying-liquid-glass-to-custom-views | Ý nghĩa `spacing` của container, hợp nhất (union), biến hình (matchedGeometry / materialize). |
| WWDC26 *What's new in SwiftUI* — https://developer.apple.com/videos/play/wwdc2026/269/ (2:20–2:51) | Bản 2027: kính tự theo thanh trượt độ trong; trên Mac kính `interactive` phản hồi cú bấm chuột. |
| WWDC26 *Modernize your AppKit app* — https://developer.apple.com/videos/play/wwdc2026/289/ (14:09–16:54) | macOS 27: kính **nảy nhẹ khi bấm** (Bản đồ dùng cho nút tự làm), bán kính góc đồng tâm theo khung chứa. |
| kube.io *Liquid Glass in the Browser* — https://kube.io/blog/liquid-glass-css-svg/ | Mô hình vật lý: định luật Snell, n = 1,5, các hàm mặt cắt mép, bản đồ dời điểm ảnh. |
| MacRumors *How Liquid Glass is changing in iOS 27* — https://www.macrumors.com/2026/06/10/how-liquid-glass-is-changing-in-ios-27/ | iOS 27 / macOS 27: thanh trượt "ultra clear → fully tinted", **viền tối quanh kính**, **vệt sáng viền sáng hơn**, khuếch tán nội dung rối tốt hơn. |

Tham chiếu sống: máy Mac này chạy macOS 27. Mở app **Bản đồ (Maps)** và **Trung tâm điều khiển** để nhìn kính thật:
nút nổi trên bản đồ, cú bấm nảy nhẹ, cách vệt sáng chạy quanh mép. Quay màn hình 60 khung/giây (Cmd+Shift+5) để đo chuyển động.

## 3. Liquid Glass thật sự là gì (tóm tắt lời Apple)

Những câu dưới đây là ý của Apple trong *Meet Liquid Glass* và HIG. Mỗi câu là một yêu cầu kỹ thuật.

1. **Thấu kính, không tán xạ.** Vật liệu cũ làm tán xạ ánh sáng (blur). Liquid Glass bẻ cong, nắn và **hội tụ** ánh sáng theo thời gian thực.
   Cách nó tự định hình chủ yếu là **lensing**: sự cong vênh của hình phía sau cho mắt biết có vật trong suốt, hình dạng và chuyển động của nó.
2. **Ngưng tụ thay vì mờ dần.** Khi hiện hoặc ẩn, kính *materialize*: tăng dần độ bẻ cong và lensing. **Không fade alpha.**
3. **Phản hồi như chất lỏng.** Khi tương tác, kính lập tức uốn và **bừng sáng**. Nó có độ dẻo như gel, chuyển động cùng tay người dùng.
   Phần tử có thể tạm "nổi lên thành kính" khi được chạm (núm công tắc, núm thanh trượt), lúc nghỉ thì yên lặng.
4. **Biến hình liên tục.** Khi đổi trạng thái, kính chảy từ dạng điều khiển này sang dạng kia, giữ ý niệm một mặt phẳng nổi duy nhất.
   Menu "bung" ra từ chính cái nút được bấm.
5. **Cấu tạo nhiều lớp, lớp nào cũng tự thích ứng** theo thứ ở phía sau: lớp vệt sáng, lớp bóng, lớp nhuộm (ta không dùng), lớp phát sáng khi chạm.
   Chữ cuộn bên dưới thì bóng đậm hơn. Lượng phủ và **dải động** dịch chuyển để nút luôn đọc được mà vẫn cho nội dung xuyên qua nhiều nhất.
6. **Lớn thì dày.** Khi kính phình to (nút → menu), nó giả lập vật liệu dày hơn: bóng sâu hơn, lensing và khúc xạ rõ hơn, tán xạ ánh sáng mềm hơn.
7. **Ánh sáng môi trường.** Có nguồn sáng chiếu lên kính, tạo vệt sáng theo hình học. Khi tương tác, đèn di chuyển, ánh sáng chạy quanh viền.
   Màu từ nội dung gần đó có thể **tràn nhẹ lên mặt kính và vào cả bóng**.
8. **Bóng biết nền.** Bóng đậm lên khi nằm trên chữ, nhạt đi khi nằm trên nền sáng trơn.
9. **Phát sáng từ bên trong khi chạm.** Ánh sáng bắt đầu ngay dưới đầu ngón tay, lan khắp phần tử và lan sang các miếng kính gần đó.
10. **Chỉ dùng cho lớp điều hướng.** Không chồng kính lên kính. Thứ nằm trên kính dùng nền tô, độ trong và vibrancy, **không** dùng kính lần hai.
11. **Hai biến thể, không trộn lẫn.** *Regular*: làm mờ và chỉnh độ sáng nền, tự thích ứng, dùng được mọi nơi.
    *Clear*: rất trong, không thích ứng, chỉ dùng trên nền ảnh hoặc video giàu chi tiết, và cần **lớp làm tối 35 %** nếu nền sáng.
12. **Phần tử nhỏ đổi sáng ↔ tối theo nền**, phần tử lớn (menu, thanh bên) thì **không** đổi để khỏi chói mắt. Chữ và biểu tượng trên kính đổi ngược theo.
13. **Trợ năng:** Giảm độ trong suốt → kính "đục sương" hơn. Tăng tương phản → kính chủ yếu đen hoặc trắng, có viền tương phản.
    Giảm chuyển động → bớt cường độ hiệu ứng và **tắt mọi tính đàn hồi**.
14. **Bản 2027 (iOS 27 / macOS 27):** thanh trượt độ trong từ *ultra clear* đến *fully tinted*; viền tối quanh kính để tách khỏi nền;
    vệt sáng viền sáng hơn; khi cửa sổ mất tiêu điểm (Mac) kính lùi lại; trên Mac kính tương tác **nảy nhẹ khi bấm chuột**.

## 4. Những gì tôi đã đo được khi tua từng khung video Apple (WWDC25 #219)

Video 4K, tua từng 0,03–0,1 s. Số đo thời gian có sai số khoảng ±0,04 s. Bạn phải tự tua lại các mốc này và so với bản của mình.

**A. Viên nang trên ranh sáng/tối (2:32, 152 s).** Một viên nang kính nằm vắt qua đường ranh nền sáng trên, nền tối dưới.
Bên trong viên nang, đường ranh **không còn thẳng và sắc**: nó bị kéo giãn thành dải chuyển mềm và lệch vị trí so với bên ngoài.
Ở hai đầu bo tròn, đường ranh uốn cong theo mép. Không có viền vẽ. Mép chỉ là chỗ hình nền bị bẻ.

**B. Viên nang trên nền tối (2:36–2:40, 156–160 s).** Lòng kính gần như vô hình, hoàn toàn trong. Hình dạng chỉ hiện ra nhờ
**một đường sáng mảnh 1–2 px** chạy quanh mép: sáng nhất ở phía có đèn (dưới phải trong cảnh này), phía đối diện cũng có đường sáng nhưng yếu hơn.
Không có lớp trắng đục nào.

**C. Ngưng tụ: nút Play tròn trên đồi cát (3:00, 180,2 → 180,7 s).**
- 0,00 s: chưa có gì.
- ~0,10 s: **chỉ có đường viền sáng** hình tròn hiện ra, đủ cỡ ngay (không phóng to từ nhỏ). Lòng chưa bẻ cong gì.
- ~0,17 s: lòng kính **bẻ cong cực mạnh**: biểu tượng Play bị phóng to và kéo méo thành một vệt sáng mềm lớn, như nhìn qua thấu kính quá dày.
- ~0,25 s: biểu tượng còn to và méo, đang co về.
- ~0,33 s: gần đúng cỡ, hơi mềm. Dải khúc xạ trong mép đã thấy rõ.
- ~0,40–0,50 s: lắng hẳn. Lòng trong suốt, cát phía sau vẫn sắc nét (đây là biến thể Clear trên ảnh). Mép trong có dải ~8–10 % bán kính
  nơi các đường cong của đồi cát bị bẻ theo mép tròn. Hai vệt sáng viền đối nhau: trên trái và dưới phải.
- Kết luận: ngưng tụ = **viền sáng trước → công suất thấu kính vọt lên ~3 lần rồi lắng về 1** trong khoảng 0,3 s; nội dung bên trong "lấy nét" từ to và mờ về đúng.
  **Độ alpha của cả khối không hề fade.**

**D. Nhấn nút tròn (3:38–3:47, 218–227 s).** Đầu ngón tay chạm vào: kính **sáng bừng từ đúng điểm chạm**, ánh sáng lan ra thành đĩa sáng trên mặt kính.
Ở góc nhìn nghiêng thấy rõ quầng sáng toả ra quanh ngón tay như gợn sóng ánh sáng.

**E. Kính phồng to khi giữ (3:54, 234 s).** Nút phồng lên rõ rệt, sáng trắng; biểu tượng bên trong mềm đi vì kính dày lên.

**F. Công tắc (4:12–4:21, 252–261 s).** Lúc nghỉ: núm trắng đặc, rãnh xám nhạt. Khi chạm: **núm hóa kính trong**, rộng ra ~1,5 lần,
cao hơn cả rãnh, nổi lên khỏi rãnh (có bóng mềm bên dưới). Mép núm dày, kiểu "môi" (*lip*): vành ngoài sáng mảnh, ngay trong là dải nơi
màu xanh của rãnh bị ép và bẻ, lòng phẳng cho thấy rãnh xanh **phóng to** bên dưới.

**G. Thanh trượt (4:24–4:27, 264–267 s).** Núm trắng khi chạm biến thành **thấu kính trong**, rộng gấp nhiều lần độ dày rãnh, nổi lên có bóng.
Rãnh xanh đi xuyên qua thấu kính, đoạn cuối của phần đã kéo hiện **to hơn** và **uốn cong ở mép trái** của thấu kính.
Đây là ý "nhìn qua thấu kính để thấy đúng giá trị bên dưới".

**H. Biến hình (5:00–5:04, 300–304 s).** Nút "Select" → tan thành các khối kính sáng trắng **dính vào nhau như giọt thủy ngân**
(ba khối tròn nối nhau bằng cổ thắt mềm, kiểu metaball) → tách ra thành ba nút ○ △ Done.
Trong lúc biến hình, kính **sáng rực hơn** lúc nghỉ. Các nút lúc nghỉ: lòng trong, quầng sáng nhẹ trong lòng, mép dưới sáng, bóng mềm bên dưới.

**I. Menu bung ra (5:05,2 → 5:05,4 s).** Bấm nút → nút sáng lên (~0,05 s) → trong ~0,05–0,1 s khối kính **bung thành menu** neo đúng tại nút.
Chữ trong menu lúc đầu **mờ và hơi giãn**, rõ hẳn sau thêm ~0,05–0,1 s. Menu là kính dày: nền phía sau (ảnh chậu cây) mờ hẳn.

**J. Viên nang Regular trên đoạn văn (11:52, 712 s).** Chữ đen phía sau bị **mờ đến mức không đọc được** nhưng vẫn thấy các dòng chữ là vệt xám.
Lòng kính sáng hơn nền một chút. Ở đầu bo tròn, các vệt chữ mờ **cong theo mép**. Mép dưới có đường sáng, bên dưới có bóng.
Đây chính là "Regular + nền có chữ ⇒ bóng đậm hơn".

**K. Nút kính trên ảnh hoa (12:11, 731 s).** Một nhánh lá màu xanh lam đi xuyên qua viên nang và **bị uốn mạnh ở gần mép**.
Lòng gần như trong, chỉ hơi sáng lên. Vệt sáng viền trên trái rõ. Màu của ảnh tràn nhẹ lên mặt kính.

## 5. Vì sao bản hiện tại trông như nhựa (chỉ đúng chỗ trong mã)

Đọc các tệp này trước khi sửa:
`Phong thi nghiem ao/Assets/Lab/Shaders/KinhMo.shader`,
`Assets/Scripts/Demo/GlassKit.cs` (`Configure`, `MakeLiquidShape`, `SquircleSdf`),
`Assets/Scripts/Demo/KinhLong*.cs`, `Assets/Scripts/Demo/NutHieuUng.cs`,
`Assets/Scripts/Menu/Dashboard/MenuKit.cs` (`Kinh`, `LamKinh`, `DressKinh`, `Backdrop`), `MenuAnim.cs`,
`Assets/Scripts/Menu/ManChonLuong.cs`, `MainMenu.cs`, `Dashboard/DashboardShell.cs`, `Dashboard/PageOverview.cs`, `Menu/GiaiBaiTap/*.cs`.

| Chỗ | Vấn đề | Kính thật của Apple |
|---|---|---|
| `GlassKit.Configure`: `_Tint` trắng α 0,20–0,26 (nền sáng), xám xanh α 0,22–0,30 (nền tối), xanh mực ở chủ đề VERA | Phủ đều một lớp sữa lên toàn bộ mặt → nhựa mờ, và có màu | Không phủ màu. Chỉ **nén dải động độ sáng** về mức đích (giữ sắc độ), mạnh yếu theo nền. |
| `BlurAt`: 17 mẫu trên 2 vòng tròn, bán kính tính theo điểm ảnh màn hình | Mờ lốm đốm, có bóng ma, phụ thuộc độ phân giải | Gaussian thật (tháp mờ giảm mẫu), tính theo đơn vị giao diện. |
| Khúc xạ: `edgeZone = smoothstep(0.26, 0, x)` nhân `_Refraction` px | Chỉ uốn trong dải ~10 px, không theo mặt cắt thấu kính, không có định luật Snell | Mặt cắt lồi dạng squircle, độ dời tính từ Snell (n = 1,5), có đỉnh gần mép rồi tắt dần vào lòng. |
| `_BorderColor` trộn vào mép, `DressKinh` thêm `Ring` trắng α 0,30 | Viền **vẽ bằng sơn** → nhựa | Mép chỉ hiện nhờ khúc xạ + vệt sáng mảnh theo hướng đèn (+ viền tối mảnh từ iOS 27). |
| `_Reflect` lấy mẫu cảnh ngoài mép 22–44 px | Quầng giả ở mép | Bỏ. |
| `_VertTint`, `_AccentColor`, `_Grid` | Kính màu, ánh nhấn màu, lưới bản vẽ | Bỏ hết ở màn ngoài lab. |
| `KinhLongNgungTu`: alpha theo `1 − (1 − t)^5` trong 0,38 s | Đây là **fade**, Apple nói rõ là không fade | Viền trước, công suất thấu kính vọt rồi lắng, nội dung lấy nét (mục 4C). |
| `MenuKit.Shadow`: bóng cố định | Không biết nền | Bóng thích ứng theo độ rối của nền phía dưới. |
| `KinhLongNen` + `_KL_Nen`: kính chỉ khúc xạ hình nền tĩnh | Kính không nhìn thấy nội dung (thẻ, chữ, danh sách) đang nằm hoặc cuộn bên dưới | Kính lấy mẫu **lớp nội dung thật** phía sau nó. |
| `ManChonLuong.TheLon`, `DashboardShell` (vệt chọn `AccentSoft`, avatar `Accent`), `PageOverview` (`*Soft`, `IconBadge`), `ManGiaiBaiTap` | Thẻ và ô có nền màu, dải màu chéo | Thẻ kính không màu; màu chỉ còn ở chữ hoặc biểu tượng nhỏ mang nghĩa. |

Những thứ **giữ lại và dùng tiếp** vì đã đúng hướng: `LoXo` / `LoXo2` (lò xo kiểu SwiftUI, response + dampingFraction), `KinhLong.CaoSu` (kéo cao su),
`GlassKit.SquircleSdf` (góc liên tục), `KinhLongBeMat` (không chồng kính), `KinhLongGiotChon` và `KinhLongVetChon` (giọt chọn trượt),
`KinhLongCongTac` và `KinhLongNumTruot` (núm hóa kính), `KinhLongAnhSang` (đèn chung), `KinhLongAnhCham` (ánh chạm), nút chuyển Kính lỏng ⇄ Cổ điển.

## 6. Ranh giới: không được làm hỏng phòng lab

- `Khkt/KinhMo` và vật liệu tĩnh `GlassKit.Glass(...)` đang **dùng chung** cho cả phòng lab (`ModernUI`, `TouchHud`, `InstrumentScreen`, `RobotChatUI`,
  `ChemSearchUI`, `ReactionAnnotator`, `BioPortal`, `IntroScreen`, `DemoShowcase`, `TrackingViewer`…) và các màn ngoài lab.
  **Không sửa `KinhMo.shader` và không đổi `GlassKit.Configure`** theo cách làm đổi giao diện trong lab.
- Tạo shader mới **`Khkt/KinhLongApple`** (`Assets/Lab/Shaders/KinhLongApple.shader`) và vật liệu riêng cho màn ngoài lab.
  Thêm nó vào **Always Included Shaders** trong `ProjectSettings/GraphicsSettings.asset`.
  Nếu không, bản dựng sẽ thiếu shader, `Shader.Find` trả `null` và màn rơi về giao diện cũ (lỗi đã gặp ở commit 80a18ecd; trong editor vẫn thấy kính nên kiểm thử không bắt được).
- Đánh dấu vùng ngoài lab bằng một thành phần trên Canvas gốc, ví dụ `VungKinhApple`. Các thành phần dùng chung (`GlassKit.MakeButton`, `MakeToggle`,
  `MakeSlider`, `NutHieuUng`, `MenuKit.LamKinh`…) chỉ dùng hành vi mới khi `GetComponentInParent<VungKinhApple>() != null`. Trong lab: hành vi cũ.
- Chế độ **Cổ điển** (`KHKT_KINH_LONG = 0`) vẫn phải ra giao diện cũ.
- Kiểm chứng: chụp các màn trong lab (`KhktUiVerify.ChupLab`) **trước và sau**; hai bộ ảnh phải trùng gần như tuyệt đối (sai khác điểm ảnh ≈ 0).

## 7. Kiến trúc vẽ (URP 17, Unity 6000.6)

### 7.1 Hai tầng kính

Apple: kính lấy mẫu một vùng lớn hơn chính nó phía sau, nhưng **kính không lấy mẫu được kính**. Ta tách:

- **Tầng nội dung** (camera nền): hình nền, các thẻ / thư mục, danh sách, trang đề. Thẻ kính ở tầng này chỉ có hình nền phía sau,
  nên chúng khúc xạ **hình nền** (`_KL_Nen`, như bây giờ, nhưng qua shader mới).
- **Tầng điều hướng** (camera overlay xếp chồng): thanh trên, thanh bên nổi, thanh công cụ, menu, popover, hộp thoại, nút nổi.
  Những thứ này khúc xạ **ảnh chụp tầng nội dung** của khung hình hiện tại.

Cách làm trong URP 17: camera stack (Base + Overlay). Viết một `ScriptableRendererFeature` cho camera nền, dùng **RenderGraph** (`RecordRenderGraph`).
Kiểm tra trước xem Project Settings → Graphics có đang bật *Compatibility Mode (Render Graph Disabled)* không, và viết theo đúng chế độ đang dùng.
Pass này chạy sau khi vẽ xong UI trong suốt của tầng nội dung:
1. Sao màu camera vào `_KL_NoiDung` (đủ độ phân giải).
2. Dựng **tháp mờ** từ đó: 1/2, 1/4, 1/8, 1/16 độ phân giải, lọc Dual Kawase hoặc Gaussian tách hai chiều. Đặt toàn cục `_KL_Mo1.._KL_Mo4`.
   Tháp này dùng chung cho mọi miếng kính trong khung hình, tính **một lần**.
3. Mức 1/16 dùng làm **đầu dò độ sáng** cho tính thích ứng (mục 8.8).

Chỉ dựng lại khi tầng nội dung thay đổi (cờ bẩn) hoặc đang có chuyển động / cuộn. Màn đứng yên thì dùng lại khung trước.
Kiểm tra raycast của Canvas tầng nội dung vẫn đúng sau khi tách camera (camera đích có cùng cỡ với màn hình).
Mọi thứ phải chạy trên Metal (Mac, iPad), DirectX 11/12 và Vulkan (Windows): dùng macro của URP Core (`SAMPLE_TEXTURE2D_LOD`…), không dùng tính năng riêng của Metal.

### 7.2 Hình dạng tính trong shader, không dùng sprite 9-slice

Biến hình và hợp nhất giọt nước cần hình dạng giải tích. Mỗi miếng kính là một hình chữ nhật góc liên tục (SDF), truyền qua kênh đỉnh:
`TEXCOORD1` = (nửa rộng, nửa cao) theo đơn vị canvas, `TEXCOORD2` = (bán kính góc, độ dày T, độ rộng mép b, biến thể),
`TEXCOORD3` = (mức ngưng tụ, độ sáng chạm, tâm chạm x, tâm chạm y).
Bật `Canvas.additionalShaderChannels` cho TexCoord1–3. Lưới đỉnh phủ rộng hơn hình 1 khoảng bằng bán kính bóng để vẽ được bóng và vùng hợp nhất.
Góc liên tục: dùng lại công thức siêu elip bậc 4 của `GlassKit.SquircleSdf` (đường cong bắt đầu sớm hơn góc tròn thường), chuyển vào HLSL.

### 7.3 Bộ chứa kính (tương đương `GlassEffectContainer`)

Một thành phần `HopKinh` gom các miếng kính đứng gần nhau (thanh công cụ, cụm nút, giọt chọn). Nó vẽ chúng **trong một Graphic duy nhất**,
truyền tối đa 16 hình qua mảng uniform, và hợp nhất bằng smooth-min để các miếng **dính vào nhau như giọt nước** khi đủ gần:

```hlsl
// k = độ "dính" theo spacing của bộ chứa. Hai hình bắt đầu nối cổ khi khe hở ≤ spacing.
float sminPoly(float a, float b, float k) {
    float h = max(k - abs(a - b), 0.0) / k;
    return min(a, b) - h * h * k * 0.25;
}
```

Ý nghĩa `spacing` y như Apple: spacing càng lớn thì các hình càng sớm hòa vào nhau. Nếu spacing lớn hơn khoảng cách bố cục thì chúng dính nhau ngay cả lúc nghỉ.
Phần tử xuất hiện **trong** tầm spacing của phần tử khác thì "nảy chồi" ra từ phần tử đó (matchedGeometry). Xa hơn tầm spacing thì dùng ngưng tụ (materialize).

## 8. Đặc tả quang học của shader `Khkt/KinhLongApple`

Đơn vị: **đv** = đơn vị canvas (CanvasScaler tham chiếu 1920×1080). Các số dưới đây có ba loại:
**[Apple]** do Apple công bố, **[đo]** đo từ video, **[khởi điểm]** là giá trị bắt đầu, phải hiệu chỉnh bằng cách so ảnh (mục 12).

### 8.1 Mặt cắt mép (độ cong, độ lồi)

Gọi `d` = khoảng cách từ mép vào trong (đv, lấy từ SDF), `b` = độ rộng mép, `x = saturate(d / b)`. Độ cao mặt kính `y = T · h(x)`, lòng phẳng khi `x ≥ 1`.

```text
Lồi squircle (mặc định, Apple ưa dùng):  h(x) = (1 − (1 − x)^4)^(1/4)
Lồi tròn (chỉ để so sánh):               h(x) = sqrt(1 − (1 − x)^2)
Lõm:                                      h(x) = 1 − Lồi(x)
Môi (lip, núm công tắc khi hóa kính):    h(x) = lerp(Lồi(x), Lõm(x), smootherstep(x)),  smootherstep(t) = 6t^5 − 15t^4 + 10t^3
```

Squircle cho dải chuyển từ mép sang lòng mềm, không có vết gãy, mép trông mỏng hơn thực tế. Đó là cảm giác "nước" ở mép.

### 8.2 Khúc xạ (Snell, n = 1,5)

Tia nhìn thẳng từ trên xuống. Pháp tuyến nghiêng góc `θ1 = atan(dy/dd)`, khúc xạ `θ2 = asin(sin θ1 / 1.5)`, tia lệch `δ = θ1 − θ2`
về phía pháp tuyến, tức là **vào trong**. Đi hết độ cao kính tại điểm đó thì độ dời là:

```text
Δ(x) = T · h(x) · tan(θ1 − θ2)          // đv, hướng vào trong:  uvMau = uv − nOut · Δ   (nOut = pháp tuyến SDF hướng ra ngoài)
θ1 kẹp ≤ 85° để tránh vô hạn ở mép
```

Với squircle và T = b, Δ đạt đỉnh khoảng 0,44·T ở x ≈ 0,03–0,05, về 0 tại mép ngoài cùng (vì h = 0) và tắt dần vào lòng.
Đừng tính lượng giác từng điểm ảnh: dựng sẵn một **bảng tra 1D** (128 mẫu, như kube.io dùng 127 tia) cho mỗi mặt cắt bằng C# lúc khởi động,
chuẩn hóa về [0,1], lưu vào texture `R16` tuyến tính. Shader đọc `Δ = LUT(x) · ΔmaxScale`.

| Thông số | Nút nhỏ (≤ 56 đv cao) | Thẻ / thư mục | Thanh bên, menu, hộp thoại | Núm hóa kính |
|---|---|---|---|---|
| Mặt cắt | squircle | squircle | squircle | môi (lip) |
| b (độ rộng mép) | 0,45·r, kẹp 6–14 đv **[khởi điểm]** | 14–18 đv | 16–22 đv | 0,5 · nửa chiều cao |
| T (độ dày) | 12 đv | 18 đv | 24 đv | 18 đv |
| Δ lớn nhất | ~5 đv | ~8 đv | ~10 đv | ~8 đv, lòng phóng to ×1,15–1,2 |

Lớn thì dày **[Apple]**: khi một miếng kính đổi cỡ (nút → menu), T, b, độ mờ và bóng **nội suy liên tục** theo cỡ hiện tại,
không nhảy bậc. Dùng hệ số dày `t = saturate((min(w, h) − 44) / (320 − 44))`.

### 8.3 Tán sắc (rất nhẹ)

Chỉ trong dải mép, tỉ lệ với Δ: lấy kênh R tại `uv − nOut·Δ·1.04`, kênh B tại `uv − nOut·Δ·0.96`, G tại `uv − nOut·Δ`.
Phải gần như không thấy được khi nhìn thường; chỉ hiện vệt cầu vồng rất mảnh trên nền nhiều tương phản (khung 4K).
Nếu nhìn ra viền màu rõ thì đã quá tay.

### 8.4 Độ mờ (độ "sương")

| Biến thể | Độ mờ (σ Gaussian) | Dùng ở đâu |
|---|---|---|
| Regular, nút nhỏ | 6–8 đv **[khởi điểm]** | thanh trên, nút nổi, tab |
| Regular, thẻ lớn / menu / thanh bên | 10–14 đv **[khởi điểm]**, lớn thì mờ mềm hơn **[Apple]** | thẻ thư mục, menu, đăng nhập |
| Clear | 0–1,5 đv | chỉ nút nổi trên khung mô hình 3D của Giải bài tập |

Lấy mẫu tháp mờ ở mức tương ứng σ, nội suy giữa hai mức gần nhất. Mép mờ hơi ít hơn lòng (×0,85) để thấy rõ sự uốn.
Tiêu chí hiệu chỉnh (như khung 4J): chữ cỡ 17 đv phía sau kính Regular **không đọc được** nhưng vẫn thấy từng dòng là vệt xám liền;
phía sau kính Clear thì **đọc được**.

### 8.5 Độ sáng và màu bên trong: không phủ sữa

Thay lớp `_Tint` bằng **nén dải động** trên độ sáng, giữ nguyên sắc độ:

```hlsl
const float3 LUM = float3(0.2126, 0.7152, 0.0722);
float L  = dot(c, LUM);
float Lt = lerp(L, Ldich, nen);          // Ldich: kính sáng ≈ 0.90, kính tối ≈ 0.16
c *= (Lt + 1e-4) / (L + 1e-4);           // đổi độ sáng, không đổi màu
c  = lerp(dot(c, LUM).xxx, c, 1.12);     // độ rực nhẹ: kính thật làm màu phía sau sống động, không xám
```

`nen` (độ nén): Regular 0,12–0,22 tùy độ rối của nền **[khởi điểm]**, Clear 0. Thanh trượt độ trong (mục 10) nhân thêm vào đây.
Kết quả trên nền xám trơn: lòng kính **không được có màu**: |R − G| và |G − B| ≤ 2/255.
Clear trên nền sáng: thêm lớp làm tối **35 %** dưới phần tử **[Apple]**, hoặc làm tối cục bộ quanh phần tử nhỏ.

### 8.6 Vệt sáng viền (specular): ánh sáng, không phải sơn

```hlsl
// nOut = pháp tuyến SDF hướng ra ngoài; d = khoảng cách từ mép vào trong (đv); nenMo = màu nền đã làm mờ tại điểm này
float2 L  = normalize(_KL_Light.xy);                  // mặc định trên-trái, ≈ (-0.45, 0.89)
float  f  = dot(nOut, L);
float  lobe = 0.90 * pow(saturate(f), 2.5)           // phía hướng đèn
            + 0.55 * pow(saturate(-f), 2.5)          // phía đối diện: ánh sáng xuyên qua kính ló ra
            + 0.10;                                   // vành rất mờ quanh hết chu vi
float  loi  = exp(-(d / 0.8) * (d / 0.8));            // đường lõi ~1 đv (≈ 1,5–2 px trên Retina)
float  mem  = exp(-d / 3.0) * 0.25;                   // dải mềm 3 đv phía trong
float3 mau  = lerp(float3(1, 1, 1), nenMo, 0.25);     // 25 % màu nền tràn lên vệt sáng [Apple: light spills]
c = 1 - (1 - c) * (1 - saturate(mau * lobe * (loi + mem) * doSang));   // cộng kiểu screen, không cháy trắng
```

Bản iOS 27 / macOS 27: **viền tối** 1 đv ngay **ngoài** mép, α 0,08 (nền sáng) / 0,22 (nền tối), để tách kính khỏi nền.
Vệt sáng sáng hơn bản 26 một chút **[Apple 2027]**.
Đèn `_KL_Light` (giữ `KinhLongAnhSang`, nhưng đổi hành vi): trên Mac và Windows **không bám theo chuột khắp màn hình**.
Đèn đứng yên ở góc trên-trái, chỉ lệch nhẹ ±15° khi con trỏ ở gần miếng kính. Đèn **chạy quanh mép** khi có tương tác: nhấn, menu bung, kính ngưng tụ, cửa sổ lấy lại tiêu điểm.
iPad: theo độ nghiêng máy, biên độ nhỏ.

### 8.7 Bóng đổ thích ứng

| Cỡ | Lệch y | Độ mềm | α nền sáng | α nền tối |
|---|---|---|---|---|
| Nút nhỏ | −2 đv | 10 đv | 0,12 | 0,32 |
| Thẻ, thư mục | −8 đv | 26 đv | 0,14 | 0,40 |
| Menu, hộp thoại | −12 đv | 36 đv | 0,20 | 0,48 |

Nhân α với `m = lerp(0.6, 1.6, roi)`, trong đó `roi` = độ rối của nền dưới bóng, ước bằng `saturate(4 · |L_sắc − L_mờ|)` lấy từ tháp mờ.
Trên chữ thì đậm, trên nền sáng trơn thì nhạt **[Apple]**. Màu bóng nhuốm 20 % màu nền đã làm mờ ("ánh sáng tràn vào bóng" **[Apple]**).
Bóng vẽ trong cùng shader, cùng SDF (đẩy SDF ra ngoài theo độ mềm), để bóng cũng hợp nhất khi các giọt dính nhau.

### 8.8 Tự thích ứng sáng ↔ tối

- Phần tử nhỏ (cao ≤ 64 đv): đo độ sáng trung bình của nền phía sau từ mức 1/16. Có trễ để khỏi nhấp nháy:
  sang kính sáng khi `L > 0.58`, sang kính tối khi `L < 0.42`. Chuyển trong 0,3 s. Chữ và biểu tượng đổi ngược lại cùng lúc.
- Phần tử lớn (thẻ, thanh bên, menu): **không đổi** sáng ↔ tối **[Apple]**, theo chủ đề app (Trắng / Đen / Xanh mực). Chỉ chỉnh `nen` và bóng.
- Cửa sổ mất tiêu điểm (Mac, `OnApplicationFocus(false)`): kính lùi lại trong 0,25 s: vệt sáng ×0,4, khúc xạ ×0,7, `nen` +0,08, chữ mờ đi 40 % **[Apple]**.

### 8.9 Chữ và biểu tượng trên kính

Không dùng kính chồng kính. Nút nằm trong thẻ kính dùng nền tô trong suốt (trắng hoặc đen α 0,06–0,12), không dùng shader kính lần hai (giữ `KinhLongBeMat`).
Chữ chính: gần đen α 0,88 (kính sáng) / trắng α 0,92 (kính tối). Chữ phụ: α 0,6. Không chữ màu trên mặt kính,
trừ chữ hoặc biểu tượng **nhỏ** mang nghĩa trạng thái (điểm thấp, lỗi), và tuyệt đối không tô nền ô bằng màu.

## 9. Đặc tả chuyển động (phần "nước")

Mọi chuyển động dùng `LoXo` (response = thời gian một dao động, damping = dampingFraction). Quy đổi từ SwiftUI: `spring(duration: D, bounce: B)` ⇔
response = D, damping = 1 − B. `interactiveSpring` ⇔ (0,15; 0,86). `smooth` ⇔ (0,5; 1,0). `snappy` ⇔ (0,5; 0,85). `bouncy` ⇔ (0,5; 0,7).
Apple **không công bố** thông số lò xo của Liquid Glass. Số dưới đây là khởi điểm; phải đo lại trên Mac thật (mục 12.3).

### 9.1 Ngưng tụ khi hiện, tan khi ẩn (thay `KinhLongNgungTu`)

Theo khung hình mục 4C (thời gian tính từ lúc bắt đầu hiện):

| t | Viền sáng | Công suất thấu kính (×Δ) | Độ sương (σ, nen) | Nội dung bên trong |
|---|---|---|---|---|
| 0,00 s | 0 | 0 | 0 | ẩn |
| 0,07 s | 1 (ease-out) | 0 | 0 | ẩn |
| 0,12 s | 1 | **3,0** | 0,3 | hiện, ×1,25, mờ 8 đv |
| 0,20 s | 1 | 1,8 | 0,7 | ×1,12, mờ 4 đv |
| 0,28 s | 1 | 1,2 | 1,0 | ×1,03, mờ 1 đv |
| 0,40 s | 1 | 1,0 | 1,0 | ×1,0, sắc nét |

Nội suy mượt (Hermite) giữa các mốc. **Không** nhân alpha của cả khối, trừ viền sáng trong 0,07 s đầu.
Ẩn: chạy ngược trong 0,28 s (thấu kính vọt lên rồi cả khối tan, viền tắt sau cùng).
Các phần tử của một màn hiện **nối tiếp** cách nhau 0,04–0,06 s (giữ danh sách `lanLuot` của `ManChonLuong`).
Giảm chuyển động: chỉ tăng dần công suất thấu kính và độ sương 0 → 1 trong 0,15 s, không vọt, nội dung không phóng to.

### 9.2 Rê chuột (Mac, Windows)

Không phồng. Quầng sáng rất nhẹ (cường độ 0,06–0,10, bán kính ≈ 0,6 × cạnh ngắn) đi theo con trỏ **bên trong** hình,
trượt bằng lò xo (0,2; 1,0). Vệt sáng viền phía gần con trỏ sáng thêm 15 %. Con trỏ ra ngoài: tắt trong 0,25 s.

### 9.3 Nhấn, giữ, kéo, nhả (thay phần chuyển động của `NutHieuUng` ở vùng ngoài lab)

- **Ánh chạm** (mục 4D): bắt đầu đúng điểm con trỏ / ngón tay (sai lệch ≤ 4 đv). Bán kính 0 → 1,2 × đường chéo trong 0,35 s (ease-out).
  Cường độ đỉnh 0,35 ở 0,08 s, giữ 0,12 khi còn nhấn. Lan sang các miếng kính trong cùng `HopKinh` trong vòng 40 đv,
  cường độ × `exp(−khoảng cách / 24)` **[Apple: lan sang kính gần đó]**.
- **Phồng** (lò xo nhấn 0,15; 0,86):
  - Mac / Windows, bấm chuột: nút nhỏ ×1,05, thẻ ×1,02, rồi khi nhả **nảy nhẹ** về 1 bằng (0,35; 0,55) **[Apple macOS 27: subtly bounces when clicked]**.
  - iPad, chạm: nút nhỏ ×1,12–1,18 (kính "bật" lên rõ như mục 4E), thẻ ×1,03.
  - Phần tử lớn (thanh bên, menu, hộp thoại): không phồng, chỉ có ánh chạm.
- **Kéo khi đang giữ** (gel): độ dời theo `KinhLong.CaoSu(kéo, cạnh × 0.5, 0.55)`, kẹp 12 đv. Giãn theo hướng kéo
  `s∥ = 1 + 0.10·tanh(|v| / 1500) + 0.06·tanh(|kéo| / cạnh)`, co chiều vuông góc `s⊥ = 1 / sqrt(s∥)` (giữ thể tích như chất lỏng).
- **Nhả**: về hình nghỉ bằng (0,45; 0,62), vọt 1–2 nhịp nhỏ rồi lặng như giọt nước. Ánh chạm bừng 0,25 rồi tắt trong 0,4 s.
- Luật an toàn của `NutHieuUng` giữ nguyên (luôn nhả khi nhấc tay, trượt ra ngoài, `SetActive(false)`, mất tiêu điểm).
- Bàn phím (Mac): Tab chuyển tiêu điểm, vòng tiêu điểm là vệt sáng viền sáng gấp đôi cộng viền 2 đv trắng/đen tương phản (không dùng màu).
  Space / Enter cho hiệu ứng nhấn y như bấm chuột.
- Giảm chuyển động: không phồng, không giãn, không nảy; chỉ còn ánh chạm mờ dần.

### 9.4 Núm hóa kính (công tắc, thanh trượt) — mục 4F, 4G

- Nghỉ: núm trắng đặc như hiện tại.
- Chạm: trong 0,12 s núm thành thấu kính trong (mặt cắt **môi** cho công tắc, **squircle** cho thanh trượt), rộng ×1,55 / cao ×1,3 (công tắc),
  rộng ×1,7 / cao ×1,3 (thanh trượt), nổi lên có bóng (mục 8.7, cỡ nút nhỏ). Rãnh và phần đã chọn bên dưới **phóng to ×1,15–1,2** khi nhìn qua lòng núm,
  và uốn cong ở mép núm.
- Kéo nhanh: núm kéo dài theo vận tốc (như 9.3), co về khi dừng.
- Nhả: trượt tới vị trí cuối bằng (0,38; 0,68), chạm đích xong mới "đặc lại" thành núm trắng trong 0,15 s.
- `KinhLongCongTac` hiện phình rộng ×1,55 nhưng cao chỉ ×1,12 (dòng `float h = knobSize.y * (1f + 0.12f * g ...)`): tăng lên ~×1,3
  để núm kính cao hơn rãnh như khung 4F. Lò xo trượt hiện có (0,36; 0,66) gần đúng, giữ làm khởi điểm. Phần vẽ núm thay bằng shader mới để có khúc xạ thật.

### 9.5 Giọt chọn trượt (tab, nhóm chọn, mục chọn của thanh bên)

Một giọt kính duy nhất trượt giữa các mục bằng (0,40; 0,74). Khi bay: kéo dài theo hướng chạy (s∥ tới 1,25), thắt lại ở giữa như giọt nước,
sáng hơn 20 %, độ trong tăng. Khi tới: co về, nảy nhẹ một nhịp. Giữ `KinhLongGiotChon` / `KinhLongVetChon`, nhưng giọt là kính **không màu**:
bỏ `AccentSoft`, bỏ vạch màu `Accent` bên trái mục chọn của thanh bên. Mục được chọn phân biệt bằng giọt kính sáng hơn và chữ đậm.

### 9.6 Biến hình trong bộ chứa (mục 4H)

Khi một nhóm nút đổi trạng thái (ví dụ thanh trên của bảng điều khiển đổi theo trang, nút "Chọn" tách thành các nút thao tác):
- Phần tử mới nằm trong tầm `spacing` (mặc định 24 đv) của phần tử cũ: nảy chồi ra từ nó. Hình chữ nhật nội suy từ hình gốc sang hình đích bằng (0,42; 0,78).
  Smooth-min tạo cổ thắt, cổ đứt khi khe hở vượt spacing.
- Trong lúc biến hình kính sáng thêm 15 %, ánh chạm lan khắp nhóm.
- Phần tử biến mất thì "được hút ngược" vào phần tử gần nhất trong nhóm (re-absorbed **[Apple]**).
- Xa hơn spacing: ngưng tụ / tan (9.1).

### 9.7 Menu, popover, hộp thoại bung ra từ nguồn (mục 4I)

- Bấm nút → nút sáng (0,05 s) → hình kính của nút **nở thành** khung menu, neo tại nút, bằng (0,32; 0,78).
  Trong lúc nở: T, b, độ mờ, bóng tăng dần từ cỡ nút sang cỡ menu (8.2).
- Nội dung menu hiện khi hình đạt 60 %: từ mờ 6 đv và ×1,06 về sắc nét ×1 trong 0,12 s.
- Đóng: ngược lại, thu về đúng nút nguồn bằng (0,28; 0,9).
- Việc làm gián đoạn (hộp thoại xác nhận, đăng xuất): thêm lớp làm tối phía sau. Việc song song (popover thông tin): không làm tối **[Apple]**.

### 9.8 Cuộn dưới kính (scroll edge effect, kiểu *soft*)

Danh sách trong bảng điều khiển và Giải bài tập cuộn dưới thanh trên: trong vùng cao = chiều cao thanh + 20 đv, nội dung mờ dần (σ 0 → 6 đv)
và nhạt dần (α 1 → 0) về phía mép thanh. Nội dung tối thì làm tối nhẹ thay vì làm mờ. Mỗi khung cuộn chỉ một hiệu ứng **[Apple]**.

## 10. Trợ năng và thanh trượt độ trong (bản 2027)

- `KHKT_GIAM_TRONG_SUOT` (Giảm độ trong suốt): kính sương hơn, che nhiều nội dung hơn: σ ×1,8, `nen` +0,35, khúc xạ ×0,3. Vẫn là kính, không thành khối đặc.
- `KHKT_TANG_TUONG_PHAN` (Tăng tương phản): kính chủ yếu trắng hoặc đen (`nen` 0,85), viền tương phản 1,5 đv (đen trên kính sáng, trắng trên kính tối), bỏ tán sắc.
- `KHKT_GIAM_CHUYEN_DONG` (Giảm chuyển động): tắt mọi tính đàn hồi, phồng, giãn, nảy, vọt; ngưng tụ thành tăng dần 0,15 s; đèn đứng yên.
- Mới: **"Độ trong của kính"** trong Cài đặt (PlayerPrefs `KHKT_DO_TRONG`, 0..1), như thanh trượt iOS 27 từ *ultra clear* đến *fully tinted*,
  nhưng **không màu**: 0 = trong nhất (σ ×0,6, `nen` ×0,3), mặc định 0,35, 1 = đục nhất (σ ×1,6, `nen` 0,6, nền trung tính trắng hoặc đen).
  Đổi là thấy ngay, không cần dựng lại màn.

## 11. Áp dụng vào từng màn ngoài lab

**Hình nền** (`KinhLongNen`): kính cần có thứ để bẻ cong, nên nền phải có chi tiết (ranh sáng tối, đường cong mảnh, mảng sáng mềm).
Hình nền là **nội dung**, không phải kính, nên được giữ màu dịu như hiện tại. Kính phía trên tuyệt đối không nhuộm màu.
Thêm vài đường cong mảnh và một dải sáng tối rõ để thấy khúc xạ ở mép (như ranh sáng tối trong mục 4A). Parallax theo chuột giữ nguyên.

1. **Màn chọn lối vào** — `Menu/ManChonLuong.cs`
   - Thanh trên (logo, nút Thoát, nhóm Kính lỏng | Cổ điển): kính Regular nhỏ dạng viên nang, tự đổi sáng ↔ tối, chung một `HopKinh` (spacing 16 đv).
   - Nhãn chào `Tag(... AccentSoft ...)`: bỏ nền màu. Thành viên nang kính nhỏ không màu, hoặc chỉ còn chữ.
   - Hai thẻ lớn `TheLon` ("Giải bài tập", "Phòng Lab") = **thư mục kính nổi**: Regular, thẻ lớn (8.2, 8.7). Bỏ hẳn dải màu `dau/cuoi`, bỏ `Sheen`,
     bỏ `Ring` trắng. Nút giả "Vào ngay →" màu trắng đặc thành nền tô trong suốt bên trong thẻ (không chồng kính), chữ đậm.
     Rê chuột: thẻ nhấc lên 6 đv, bóng xa hơn và đậm hơn, quầng sáng theo con trỏ. Bấm: 9.3. Hiện: ngưng tụ nối tiếp 9.1.
2. **Đăng nhập và Cài đặt** — `Menu/MainMenu.cs`
   - Khung đăng nhập và khung cài đặt: kính Regular lớn, không màu. Ô nhập chữ là nền tô trong suốt bên trong kính.
   - Công tắc và thanh trượt: 9.4. Thêm thanh trượt "Độ trong của kính" (mục 10) cạnh công tắc "Giao diện kính lỏng".
3. **Bảng điều khiển** — `Dashboard/DashboardShell.cs`, `PageOverview.cs`, `PageController.cs`, `PageAiKey.cs`, `ControllerCalPanel.cs`, `MenuKit.cs`, `MenuAnim.cs`
   - Thanh bên: kính Regular **nổi, thụt vào** cách mép cửa sổ 12 đv, bán kính đồng tâm với cửa sổ (mục 11.6), tầng điều hướng; nội dung trang chảy được ra sau nó.
   - Mục chọn: giọt chọn kính không màu (9.5). Avatar `Accent` thành ô tròn kính trung tính, chữ cái đầu màu chữ chính.
   - Thẻ KPI, thẻ bài, thẻ kỹ năng: thẻ kính tầng nội dung, không màu. `IconBadge` nền `*Soft` thành nền tô trung tính α 0,08.
     Nhãn môn `MakeChipTag(... BioSoft / AccentSoft ...)` thành viên nang trung tính. Màu chỉ còn ở **chữ số điểm** khi cần báo thấp hoặc cao.
   - Thanh trên của trang (nếu có): tầng điều hướng, scroll edge effect 9.8.
4. **Giải bài tập** — `Menu/GiaiBaiTap/ManGiaiBaiTap.cs`, `KhungTrangGbt.cs`, `DapAn.cs`, `KhungMoHinh3D.cs`
   - `ManGiaiBaiTap`: thanh điều hướng trên (Trang chủ · Chuỗi phản ứng · Cơ chế · Mô hình 3D · Hóa lý, Cài đặt, ← Màn chính) là kính tầng điều hướng,
     mục đang mở là giọt chọn không màu (9.5). Hàng thẻ lớn của Trang chủ (Chuỗi, Cơ chế, Mô hình 3D, Hóa lý) là thư mục kính như `TheLon`:
     bỏ dải màu `Gradient(nen, dau, cuoi, 150f)`, bỏ ô biểu tượng `AccentSoft`. Các nút bật/tắt đang tô `AccentSoft` khi bật (ẩn H, hiện e, Giấy ⇄ 3D)
     thành nhóm chọn kính có giọt chọn.
   - `KhungTrangGbt`: thanh phụ dính trên đầu trang (nút "‹ tên công cụ" + các bước 01 · 02 · 03) là kính tầng điều hướng có scroll edge effect (9.8);
     bước đang xem là giọt chọn không màu thay cho màu nhấn. Thẻ trắng bo 20 viền tóc thành thẻ kính tầng nội dung. Nút chính màu nhấn thành kính không màu chữ đậm.
   - `DapAn`: **tờ giấy** của sơ đồ cơ chế (`SoDoGiay.MauGiay`) và hình đáp án là nội dung để đọc như bài thi: **giữ nền giấy đặc**, không làm kính.
     Huy hiệu `BioSoft` / `WarnSoft` thành biểu tượng + chữ màu nhỏ; ô chất `AccentSoft` thành nền tô trung tính.
   - `KhungMoHinh3D`: khung 3D nền sáng studio là nội dung. Nút nổi trên khung 3D (ví dụ "Toàn màn hình") là kính **Clear**, làm tối cục bộ 35 % vì nền sáng.
     Thanh điều khiển bên trái nằm trong thẻ: nền tô trong suốt, không chồng kính.
   - Các lớp phủ mở từ đây (`CoCheMoDun` 3D ⇄ Giấy, `MoHinh3DMoDun`, `HoaLyMoDun`): đọc mã rồi áp dụng cùng nguyên tắc: thanh điều khiển nổi là kính,
     vùng 3D và tờ giấy là nội dung.
5. **Không đụng:** mọi thứ trong phòng lab (mục 6). `Demo/IntroScreen.cs` (màn chọn chế độ hiện trong cảnh lab, dùng `ModernUI`) tính là **trong** lab.
   `Menu/VeSanh.cs` chỉ là đoạn chuyển cảnh, không có giao diện. Gặp màn nào chưa chắc thuộc trong hay ngoài lab: đọc mã, quyết định, ghi lý do vào `tien-do.md`.
6. **Độ cong đồng tâm** **[Apple]**: viên nang cho điều khiển cao ≤ 56 đv (bán kính = h/2). Phần tử lồng trong phần tử khác:
   `r_trong = max(r_ngoài − lề, 8 đv)`. Phần tử cạnh góc cửa sổ dùng bán kính đồng tâm với góc cửa sổ.
   Không để góc nào trông "véo" hay "loe". Mọi thẻ cấp cao nhất cùng một bán kính (macOS 27: bán kính thống nhất).

## 12. Kiểm chứng (bắt buộc, có số đo)

### 12.1 Cảnh thử dựng lại các cảnh demo của Apple

Tạo cảnh / màn kiểm thử `KinhLongPhongThu` (chỉ trong editor, không đưa vào bản dựng) gồm đúng các cảnh trong mục 4:
(a) viên nang di chuyển qua ranh sáng/tối; (b) viên nang trên nền tối chỉ có viền; (c) nút Play tròn ngưng tụ lặp lại trên ảnh nhiều chi tiết;
(d) viên nang Regular trên đoạn văn đen; (e) công tắc và thanh trượt; (f) "Chọn" → ba nút; (g) menu bung từ nút.
Chụp ảnh ở đúng các mốc thời gian của mục 4. Đặt **cạnh nhau** với khung tương ứng của video Apple (tự chụp từ trang WWDC), lưu ở `~/AppBuild/kinh_long_so_sanh/`.
Ghi nhận xét từng cặp vào `tien-do.md`: giống chỗ nào, khác chỗ nào, đã chỉnh gì.

### 12.2 Tiêu chí đạt (đo được)

1. Kính trên nền xám trơn: |R − G|, |G − B| ≤ 2/255 ở lòng kính (không màu).
2. Cảnh (a): đường ranh bên trong viên nang bị uốn ở hai đầu bo và lệch so với bên ngoài; không phải một đường thẳng sắc.
3. Cảnh (b): đường sáng viền rộng ≤ 2 px ở 2880×1800; phía đối diện đèn sáng bằng 40–70 % phía hướng đèn; lòng không có lớp trắng.
4. Cảnh (c): không có khung nào mà cả khối có alpha đều nằm giữa 0 và 1 sau 0,07 s đầu; có khung ở 0,10–0,15 s mà nội dung bị thấu kính phóng to rõ.
5. Cảnh (d): chữ 17 đv sau kính Regular không đọc được nhưng thấy vệt dòng; sau kính Clear đọc được; bóng trên đoạn văn đậm hơn bóng trên nền trơn ≥ 1,4 lần.
6. Ánh chạm bắt đầu tại điểm bấm, sai lệch ≤ 4 đv.
7. Bật Giảm chuyển động: tỉ lệ phóng không bao giờ vượt đích (không vọt), không có giãn theo hướng kéo.
8. Ảnh trong phòng lab trước / sau trùng nhau (mục 6). Chế độ Cổ điển ra giao diện cũ.
9. Hiệu năng trên chính máy Mac này ở 2880×1800: tổng thời gian GPU của kính (chụp tầng nội dung + tháp mờ + mọi miếng kính) ≤ 2,0 ms/khung;
   màn đứng yên không dựng lại tháp mờ. Đo bằng Unity Profiler / Frame Debugger, ghi số vào `tien-do.md`.
10. Bản dựng Windows (`MacBuild.BuildWindows`) biên dịch được shader và mã, không có API riêng của Mac.

### 12.3 Đo chuyển động thật

Quay màn hình app của bạn và app Bản đồ / Trung tâm điều khiển trên Mac này ở 60 khung/giây, so từng khung cú bấm, cú nảy, menu bung.
Chỉnh response / damping cho tới khi số khung tới đích và độ vọt khớp trong ±1 khung. Ghi giá trị cuối cùng vào `tien-do.md`
và vào hằng số trong `KinhLong.cs`.

### 12.4 Lệnh chạy

- Kiểm thử giao diện ngoài lab (3 nền × các màn × 1440×900 và 2880×1800): `KhktUiVerify.Run`, chụp trong lab: `KhktUiVerify.ChupLab`
  (xem `Assets/Scripts/Editor/KhktUiVerify.cs` và `Menu/KhktUiRunner.cs`; thêm các kiểm tra `[UI_PASS]/[UI_FAIL]` cho mục 12.2).
- Dựng app Mac: `Khkt.EditorTools.MacBuild.Build`, rồi **mở app thật** và nhìn lại tất cả, vì editor và bản dựng có thể khác nhau (shader bị loại khỏi bản dựng).
- Sau mỗi lượt: kiểm `plutil -p ~/Library/Preferences/unity.DefaultCompany.VERA\ Lab.plist` và trả các cờ về như mục 1.

## 13. Thứ tự làm

1. **P0** Đọc mã và nguồn (mục 2, 5); xem lại video ở các mốc mục 4; tạo `tien-do.md`.
2. **P1** Tầng nội dung + tháp mờ (RenderGraph), đo hiệu năng.
3. **P2** Shader `Khkt/KinhLongApple`: SDF góc liên tục, bảng tra khúc xạ, mờ, nén dải động, vệt sáng, viền tối, bóng. Thêm vào Always Included Shaders.
4. **P3** Cảnh thử `KinhLongPhongThu`, so ảnh tĩnh (cảnh a, b, d) cho tới khi đạt 12.2 mục 1–3, 5.
5. **P4** Thích ứng sáng ↔ tối, bóng thích ứng, mất tiêu điểm.
6. **P5** Chuyển động: ngưng tụ, rê, nhấn / kéo / nhả, nảy khi bấm trên Mac, ánh chạm lan.
7. **P6** `HopKinh` (smooth-min), giọt chọn, biến hình, menu bung, scroll edge.
8. **P7** Áp dụng vào các màn (mục 11), bỏ toàn bộ màu trên kính.
9. **P8** Trợ năng + thanh trượt "Độ trong của kính".
10. **P9** Kiểm chứng đầy đủ mục 12 trên Mac → dựng Windows → (cuối cùng) iPad: chạm, nghiêng máy, hiệu năng chip A.

Mỗi giai đoạn xong: chạy kiểm thử, cập nhật `tien-do.md`, commit riêng.

## 14. Những điều cấm

- Phủ lớp trắng hoặc màu đều lên kính (α > 0,08) để "cho giống kính mờ". Đó là nhựa.
- Viền vẽ bằng màu cố định (`Ring`, `_BorderColor`) thay cho vệt sáng theo đèn.
- Fade alpha để hiện / ẩn kính.
- Kính chồng kính.
- Kính màu, nền ô màu, dải màu trên bất kỳ bề mặt kính nào ở màn ngoài lab.
- Sửa `KinhMo.shader`, `GlassKit.Configure` hoặc bất kỳ thứ gì làm đổi giao diện trong phòng lab.
- Dùng API hoặc đường dẫn chỉ có trên Mac; làm việc của iPad trước Mac / Windows.
- Đẩy lên web, commit ảnh từ video Apple, commit tệp riêng tư (`tai_khoan_admin.txt`, `data/email_tam_thoi.txt`, `data/geoip/`, `data/ai_rieng/`).
- Báo "đã giống Apple" khi chưa có ảnh so sánh cạnh nhau và số đo của mục 12.

Khi xong, báo lại: các tệp đã thêm / sửa, ảnh so sánh nằm ở đâu, số đo hiệu năng, các tiêu chí 12.2 nào đạt / chưa đạt và vì sao.
