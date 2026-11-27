# Kế hoạch hoàn thành trong 1 ngày: VERA Lab (dự kiến 18/11/2026)

Kế hoạch này lấy từ tờ ghi chú tay (ảnh IMG_0496). Hai AI làm song song: **Antigravity** lo toàn bộ phần hình ảnh trong Unity,
**Claude** lo phần cơ chế Hóa, bảo vệ dữ liệu, AI tại máy, giao diện ngoài lab, rồi cuối ngày gộp lại và xuất app Mac.
Mỗi prompt bên dưới đều dán thẳng được, không cần chỉnh.

---

## 1. Chín việc trong ghi chú và cách hiểu

| Mã | Ghi chú trong ảnh | Mình hiểu là | Prompt | Ai làm |
|---|---|---|---|---|
| N1 | Mã hoá dữ liệu 3D cho cơ chế để tránh copy ý tưởng / hoặc chạy RAG qua AI local | Làm cả hai: dữ liệu gốc không lên GitHub, app chỉ chứa gói đã mã hóa, còn "bộ não" (kho mẫu, RAG) chạy trên AI tại máy | C1, C2 | Claude |
| N2 | 1 số bài demo (cơ sở bài) | 8 bài cơ chế mẫu, mỗi bài có đề, các bước, câu hỏi, lỗi hay gặp | C1 | Claude |
| N3 | Tùy chỉnh thông số phần cứng để chạy 3D cơ chế và giải chuỗi | Hồ sơ phần cứng ở hai phía: trong app (mức đồ họa) và ở máy chủ (chọn mô hình AI, thông số RDKit). App có thêm bảng giải chuỗi | G1, C2, C3 | Cả hai |
| N4 | Thêm phần giải phẫu | Giải phẫu người chạy ngay trong app. Hiện trên Mac nó phải mở trình duyệt ngoài | G4 | Antigravity |
| N5 | Mô hình 3D thực vật: cấu trúc, tính năng, chuyển động dạy học theo thời gian thực | Mô-đun thực vật gồm 3 lát cắt và 4 quá trình mô phỏng chỉnh bằng thanh trượt | G3 | Antigravity |
| N6 | Chỉnh lại bàn tay người và render mô hình 3D cho người | Bàn tay chân thật hơn, thêm mô hình người 3D (người khác trong phòng học chung, thân của mình) | G2 | Antigravity |
| N7 | Tư duy vẽ hình | Nâng `tu_duy_ve.py` để vẽ đẹp cả những khung chưa có trong kho mẫu | C1 | Claude |
| N8 | Đồ hoạ giao diện bên ngoài app và thông số sắc nét | Các màn ngoài lab, biểu tượng app, chữ và ảnh sắc nét trên màn Retina | C4 (phần render sắc nét ở G1) | Claude |
| N9 | Mã phần cứng và phần render khi xuất app để đồ hoạ không lag khi di chuyển | Đo và sửa giật trong bản app xuất ra, thêm script xuất app Mac | G1, kiểm lại ở C5 | Antigravity |

> Chữ "cho ng…" ở dòng N6 mình đọc là "cho người". Mình làm luôn mô hình người cho phòng học chung có nhiều người.
> Nếu bạn muốn ý khác thì sửa mục C trong prompt G2.

---

## 2. Vì sao chia như vậy

- **Máy chỉ có 8 GB RAM và khoảng 15 GB trống.** Vì vậy chỉ Antigravity được mở Unity có giao diện, trong thư mục chính
  `~/Developer/Antigravity`. Claude chỉ chạy Unity `-batchmode` trong một bản sao riêng (`~/AppBuild/VERA_claude`, bản sao
  APFS nên gần như không tốn đĩa). Mỗi lúc chỉ một Unity `-batchmode` chạy, AI nào tới sau tự đợi.
- **Unity không mở được dự án nằm trong thư mục ẩn** (`.claude/worktrees`, `~/.gemini/...`). Vì vậy Antigravity làm thẳng
  trong thư mục chính, còn Claude chép mã sang bản sao không ẩn rồi mới biên dịch.
- **Antigravity** lo những việc nặng về hình ảnh 3D trong Unity (G1–G4). Nó đã làm đồ họa HD, Wave 6 và Cổng Sinh học.
- **Claude** lo cơ chế Hóa vì đã làm bản thử và hợp đồng dữ liệu `HOP_DONG_APP.md`. Claude cũng lo bảo vệ dữ liệu,
  AI tại máy, giải chuỗi, giao diện ngoài lab, và cuối ngày gộp, kiểm thử, xuất app.
- Hai bên **không sửa chung file nào** (bảng mục 4). Chỗ phải dùng mã của nhau đã có hợp đồng sẵn (mục 3).

### Nhánh và thư mục

| Bên | Thư mục | Nhánh |
|---|---|---|
| Antigravity | `~/Developer/Antigravity` (thư mục chính, KHÔNG tạo worktree) | `n18/antigravity` |
| Claude, phiên "N18 Cơ chế" | worktree của phiên + bản sao Unity `~/AppBuild/VERA_claude` | `n18/co-che` |
| Claude, phiên "N18 Tích hợp" | `~/Developer/Antigravity` (sau khi Antigravity xong) | `n18/tich-hop` |
| Dữ liệu kín (ngoài git công khai) | `~/Developer/VERA_du_lieu_rieng/` (kho git riêng tại máy) | — |

---

## 3. Hợp đồng giữa hai bên (không đổi tên)

### 3.1. Hồ sơ phần cứng (Antigravity tạo ở G1, Claude dùng ở C3)

```csharp
// Phong thi nghiem ao/Assets/Scripts/Rendering/HardwareProfile.cs
namespace Khkt.Rendering
{
    public enum MucDoHoa { Thap = 0, Vua = 1, Cao = 2, SieuNet = 3 }

    public static class HardwareProfile
    {
        public static MucDoHoa Muc { get; }            // mức đang dùng
        public static bool TuDong { get; }             // true: đang tự chọn theo máy
        public static event System.Action Changed;     // phát khi đổi mức
        public static void Dat(MucDoHoa muc);          // chọn tay
        public static void DatTuDong();                // về Tự động
        public static MucDoHoa DeXuat();               // đoán theo phần cứng, không đổi gì
        public static string MoTaMay();                // ví dụ "Apple M2, 8 GB RAM"

        // Gợi ý cho các mô-đun 3D (cơ chế, thực vật, giải phẫu, người):
        public static int SoManhCau { get; }    // độ mịn quả cầu: 12 / 16 / 24 / 32
        public static int SoHatToiDa { get; }   // số hạt hiệu ứng tối đa: 200 / 500 / 1500 / 3000
        public static float TiLeRender { get; } // render scale: 0.7 / 0.85 / 1 / 1
        public static bool BongMem { get; }     // bóng đổ mềm
    }
}
```

**Mốc M1:** Antigravity commit riêng file này với tiêu đề `[G1] HardwareProfile: hợp đồng mức đồ họa`. Claude chỉ bắt đầu C3 khi đã có commit này.

### 3.2. Mở mô-đun

Mỗi mô-đun có một lớp tĩnh với `Mo()`, `Dong()` và `bool DangMo`:

| Lớp | Ai làm |
|---|---|
| `Khkt.CoChe.CoCheMoDun` | Claude (C3) |
| `Khkt.Bio.ThucVat.ThucVatMoDun` | Antigravity (G3) |
| `Khkt.Bio.GiaiPhau.GiaiPhauMoDun` | Antigravity (G4) |

Bảng điều khiển (C4) tìm các lớp này theo tên trong mọi assembly đã nạp. Lớp nào chưa có thì thẻ ghi "Sắp có". Nhờ vậy
hai nhánh biên dịch độc lập được.

---

## 4. Ai được sửa file nào

| Việc | Được sửa |
|---|---|
| G1 | `Scripts/Rendering/*`, `Scripts/Environment/*` (camera), `Scripts/Editor/MacBuild.cs` (mới), `Demo/IntroScreen.cs` (chỉ tab Hiển thị), `ProjectSettings/QualitySettings.asset`, `ProjectSettings/ProjectSettings.asset` (mục Mac/Retina/đồ họa), `Settings/*RPAsset*`, `Settings/*Renderer*` |
| G2 | `Demo/HandRig.cs`, `Demo/ArmRig.cs`, `Demo/HandController.cs` (chỉ phần dựng và tư thế tay), `Input/InAppHandTracker.cs` (ánh xạ khớp), `Scripts/Network/*`, `Scripts/Avatar/*` (mới), `Editor/KhktN18Verify.cs` (mới), shader da trong `Lab/Shaders/` |
| G3 | `Demo/Bio/ThucVat/*`, `Simulation/Bio/ThucVat/*`, `Lab/Shaders/LaCay*`, `data/sinh/thuc_vat.json`, `Editor/KhktN18Verify.cs` (nhóm G3) |
| G4 | `Demo/Bio/GiaiPhau/*`, `Demo/Bio/DigitalAnatomyTable.cs`, `Demo/Bio/Portal/BioWebViewBridge.cs` và `BioPortalTransition.cs` (chỉ nhánh Mac/Windows), `Lab/Shaders/MatCat*`, `Editor/KhktN18Verify.cs` (nhóm G4) |
| C1–C2 | `server/ai/co_che/*`, `nguyen_mau/co_che_3d/*`, `tests/test_co_che*.py`, `tests/test_khong_lo_du_lieu.py`, `.gitignore`, `server/ai/cau_hinh_may.py`, các điểm cuối mới trong `server/` |
| C3 | `Scripts/CoChe/*`, `Editor/KhktCoCheVerify.cs`, `StreamingAssets/CoChe/` và `Resources/CoChe/` (cả hai bị git bỏ qua) |
| C4 | `Menu/*`, `Menu/Dashboard/*`, `Demo/UiTheme.cs`, `GlassKit.cs`, `ModernUI.cs`, `UiMotion.cs`, `NutHieuUng.cs`, `IntroScreen.cs` (trừ tab Hiển thị), `Lab/Logo/*`, ảnh giao diện mới, biểu tượng app |

Các đường dẫn trên tính từ `Phong thi nghiem ao/Assets/Scripts/` hoặc `Phong thi nghiem ao/Assets/`.

---

## 5. Lịch trong ngày và thứ tự gửi

```
Giờ      07:30  08    09    10    11    12    13    14    15    16    17    18    19    20    21    22    23
Antig.          [====== G1 ======][=== G2 ===] ăn [====== G3 ======][====== G4 ======]
                       ^M1
Claude   [P0]   [====== C1 ======][====== C2 ======][====== C3 ======][=== C4 ===][=== C5 ===]
Bạn       gửi #1   xem ảnh mỗi lần AI báo XONG ............................................ [thử tay]
```

**Luật gửi:** chỉ gửi prompt tiếp cho một AI khi AI đó đã trả lời dòng đầu `XONG <mã>` (hoặc `CHƯA XONG` sau khi làm theo
mục "Nếu trễ") **và** điều kiện ở cột "Gửi khi" đã đủ. Giờ trong bảng chỉ để tham khảo.

| Gửi | Giờ (gợi ý) | Gửi cho | Prompt | Gửi khi |
|---|---|---|---|---|
| **#1** | 07:30 | Claude, phiên mới "N18 Chuẩn bị" | **P0** Chuẩn bị | ngay |
| **#2** | 08:00 | Antigravity, chat mới | **G1** Phần cứng, hết giật, sắc nét | P0 báo `SẴN SÀNG` |
| **#3** | 08:00 | Claude, phiên mới "N18 Cơ chế" (giữ phiên này tới hết C4) | **C1** Lõi cơ chế, bài demo, tư duy vẽ | P0 báo `SẴN SÀNG` |
| — | ~09:30 | (Antigravity báo đạt mốc M1) | không gửi gì, chỉ ghi nhận | — |
| **#4** | ~10:30 | Antigravity, chat mới | **G2** Bàn tay và mô hình người | G1 `XONG` |
| #4R | ~10:30 | Claude, phiên duyệt mới (dùng Sonnet cho rẻ cũng được) | **R** Duyệt G1 *(tùy chọn)* | G1 `XONG` |
| **#5** | ~11:00 | Claude, phiên "N18 Cơ chế" | **C2** Bảo vệ dữ liệu, AI tại máy, giải chuỗi | C1 `XONG` |
| **#6** | ~13:15 | Antigravity, chat mới | **G3** Thực vật 3D | G2 `XONG` |
| **#7** | ~14:00 | Claude, phiên "N18 Cơ chế" | **C3** Trình xem cơ chế trong Unity | C2 `XONG` **và** đã có M1 |
| **#8** | ~16:15 | Antigravity, chat mới | **G4** Giải phẫu trong app | G3 `XONG` |
| #8R | ~16:15 | Claude, phiên duyệt | **R** Duyệt G2 + G3 *(tùy chọn)* | G3 `XONG` |
| **#9** | ~17:30 | Claude, phiên "N18 Cơ chế" | **C4** Giao diện ngoài lab, sắc nét | C3 `XONG` |
| **#10** | ~20:00 | Claude, phiên mới "N18 Tích hợp" | **C5** Gộp, kiểm thử, xuất app Mac | G4 `XONG`, C4 `XONG`, **bạn đã tắt Unity có giao diện** |
| **#11** | ~22:30 | Bạn | Thử tay 10 phút (mục 8) | C5 `XONG` |

**Hai mốc kiểm tra tiến độ:**
- **12:30:** G1 hoặc C1 chưa xong thì gửi prompt **S** cho bên đó, rồi chuyển sang việc kế tiếp.
- **17:00:** G3 chưa xong thì gửi **S** cho Antigravity, rồi gửi G4 với dòng mở đầu "Làm theo mục Nếu trễ ngay từ đầu".

Mục "Phải sửa" của prompt R thì dán vào **đầu** prompt kế tiếp gửi cho Antigravity, kèm câu: "Trước khi làm việc mới, sửa các mục sau rồi commit `[sua]`: …".

---

## 6. Việc của bạn

**Tối nay hoặc trước 07:30:**
1. Cắm sạc và đóng các app nặng (Chrome nhiều tab, Xcode, Simulator).
2. Trong Antigravity IDE, mở đúng thư mục `~/Developer/Antigravity` (File → Open Folder). KHÔNG mở `~/Documents/Antigravity`
   vì đó là bản cũ và từng làm treo. Nếu Antigravity hỏi có tạo worktree không thì chọn **không**.
3. Ở Antigravity, chọn mô hình mạnh nhất và chế độ lập kế hoạch (Planning) nếu có. Ở Claude, dùng Opus cho P0, C1–C5.

**Trong ngày:**
- Mỗi lần một AI báo `XONG`, mở 1–2 ảnh trong `docs/ngay-18-11/anh/<mã>/` xem nhanh khoảng 2 phút. Thấy sai thì nói ngay, đừng để tới tối.
- Khi một AI hỏi xin phép (dọn đĩa, tải mô hình) thì trả lời liền để nó không đứng chờ.
- **Máy tự ngủ:** P0 chạy script `~/AppBuild/canh_gac_ngu.sh`. Script giữ máy thức trong lúc làm, và cho máy ngủ khi C5 xong
  hoặc khi các AI đã dừng 90 phút (hết token, kẹt). Nó chỉ cho ngủ khi bạn đã không chạm máy 15 phút, nên đang dùng máy
  thì không bị ngắt. Muốn tắt script: `pkill -f canh_gac_ngu.sh`.

**Quyết định chỉ bạn làm được:**
- Kho `github.com/tronggenz-alt/KHKT2027` đang **công khai**. Muốn chặn người khác chép ý tưởng thì cách hiệu quả nhất
  là chuyển kho sang Private: trên github.com vào Settings → Danger Zone → Change visibility. Mã hóa trong app chỉ là lớp phụ.
- Cuối ngày: có gộp `n18/tich-hop` vào `main` và có đẩy lên GitHub không. Các AI sẽ không tự làm.

---

## 7. Toàn bộ prompt

### Prompt P0: Chuẩn bị (gửi #1, Claude)

```text
[P0] Chuẩn bị ngày làm việc 18/11 cho VERA Lab. Chỉ chuẩn bị, KHÔNG sửa mã nguồn.

Bối cảnh: hôm nay hai AI làm song song.
- Antigravity làm Unity trực tiếp trong thư mục chính ~/Developer/Antigravity, nhánh n18/antigravity, được mở Unity có giao diện.
- Claude làm phần cơ chế Hóa trên nhánh n18/co-che (worktree riêng). Claude biên dịch và chạy thử Unity bằng -batchmode trong bản sao riêng ~/AppBuild/VERA_claude.
- Máy: MacBook M2, RAM 8 GB, ổ còn khoảng 15 GB. Mỗi lúc chỉ được chạy MỘT Unity -batchmode.

Làm lần lượt:
1. Đọc AGENTS.md, TIEN_DO.md, BAO_CAO_WAVE6_MAC.md. Chạy `pgrep -fl Unity`: nếu có Unity đang chạy thì dừng lại và báo tôi.
2. Giữ máy thức khi còn làm, cho máy ngủ khi xong hoặc khi các AI đã dừng (hết token, kẹt). Viết ~/AppBuild/canh_gac_ngu.sh rồi chạy nền bằng nohup. Script làm như sau:
   - Giữ máy thức bằng `caffeinate -dimsu -w $$` (tự hết khi script dừng).
   - Cứ 5 phút kiểm tra một lần. Chỉ cho máy ngủ khi người dùng đã không chạm phím/chuột ít nhất 15 phút (đọc HIDIdleTime bằng `ioreg -c IOHIDSystem`) VÀ gặp một trong ba điều:
     (a) có tệp ~/AppBuild/XONG_NGAY_18-11 (C5 tạo ở cuối ngày);
     (b) 90 phút liền không có tệp nào thay đổi trong ~/Developer/Antigravity (bỏ qua Library, Temp, Logs) và ~/Developer/VERA_du_lieu_rieng, đồng thời không có tiến trình Unity, xcodebuild hay ollama đang chạy. Nghĩa là các AI đã dừng;
     (c) đã quá 01:00 đêm.
   - Trước khi cho ngủ: ghi lý do vào ~/AppBuild/canh_gac_ngu.log, đợi 2 phút để còn kịp hủy, rồi chạy `pmset sleepnow` và thoát.
   - Tắt tay được bằng `pkill -f canh_gac_ngu.sh`.
3. Ổ đĩa: chạy `df -h /`. Nếu còn dưới 12 GB thì liệt kê những thứ dọn được an toàn (cache Google, pip, Homebrew, uv, worktree cũ đã gộp) kèm dung lượng, CHƯA xóa, hỏi tôi trước. Tuyệt đối không đụng: Library/ và Builds/ của Unity, ~/AppBuild/VERALab_ui (cả dd), cache Unity, /Applications/Unity, ~/Documents/Antigravity, ~/Developer/Antigravity-luu-ban-cu, ~/CHEMISTRY_xem.
4. Git ở ~/Developer/Antigravity: `git status` phải sạch. Có thay đổi chưa commit thì dừng và báo tôi, không stash. Tạo nhánh n18/antigravity và n18/co-che từ main (không đẩy lên GitHub). Chuyển thư mục chính sang n18/antigravity.
5. Tạo bản sao Unity cho Claude bằng bản sao APFS, gần như không tốn đĩa:
   mkdir -p "$HOME/AppBuild/VERA_claude"
   cp -cR "$HOME/Developer/Antigravity/Phong thi nghiem ao" "$HOME/Developer/Antigravity/data" "$HOME/Developer/Antigravity/phong_sinh" "$HOME/Developer/Antigravity/tools" "$HOME/AppBuild/VERA_claude/"
   Viết script ~/AppBuild/VERA_claude/dong_bo_va_chay.sh <thư mục worktree> <Lớp.Hàm> [tên log]. Script làm các việc sau:
   (a) rsync Assets/, ProjectSettings/, Packages/ và data/ từ worktree sang bản sao (--delete trong Assets, giữ nguyên Library);
   (b) nếu `pgrep -f -- -batchmode` thấy Unity khác đang chạy thì đợi, cứ 2 phút xem lại một lần;
   (c) chạy /Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -buildTarget StandaloneOSX -projectPath <bản sao> -executeMethod <Lớp.Hàm> -logFile <TMPDIR>/<tên log>.log, không dùng -nographics;
   (d) in 40 dòng cuối của log và mọi dòng có "error CS" hoặc "Exception".
6. Lấy mốc gốc trên bản sao: chạy KhktWave6Verify.Run bằng script trên. Ghi số PASS/FAIL. Cuối ngày sẽ so với mốc này.
7. Python: ~/Developer/Antigravity/.venv phải chạy được `python -m unittest discover -s tests` (ghi số test đạt). Kiểm tra `import rdkit`, chưa có thì `pip install rdkit` trong .venv. Ghi lại kết quả `ollama --version` và `ollama list`.
8. Chạy `lsof -i :8000` xem ai đang giữ cổng 8000, và xem tình trạng LaunchAgent vn.giaichuoihuuco.app. Chỉ xem, không sửa.
9. Ghi kết quả vào docs/ngay-18-11/moc-goc.md trên nhánh n18/antigravity, commit đúng file đó với tiêu đề `[P0] mốc gốc ngày 18/11`.

Không push, không đưa lên web, không git add -A.
Dòng đầu câu trả lời ghi `SẴN SÀNG` hoặc `CHƯA SẴN SÀNG: <lý do>`. Sau đó là bảng: ổ trống, RAM trống, mốc Wave6 (PASS/FAIL), số test Python, rdkit, ollama, cổng 8000, LaunchAgent.
```

### Prompt G1: Hồ sơ phần cứng, hết giật khi di chuyển, sắc nét (gửi #2, Antigravity)

```text
[G1] Hồ sơ phần cứng + hết giật khi di chuyển + thông số sắc nét (VERA Lab, Unity)

LUẬT CHUNG (giữ suốt việc này)
- Dự án VERA Lab: kho git ~/Developer/Antigravity. Unity 6000.6.7f1 ở /Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app. Đọc AGENTS.md trước tiên.
- Nền tảng: Mac là chính, rồi Windows, iPad cuối cùng. Không dùng đường dẫn cứng (/Users, /tmp, ~) hay API chỉ có trên Mac. Dùng Path.Combine, Application.persistentDataPath, Path.GetTempPath().
- Chỉ sửa file trong PHẠM VI của việc này. Cần sửa file ngoài phạm vi thì ghi vào báo cáo, không tự sửa.
- Git: chỉ `git add <từng file>` của mình. Cấm `git add -A`, `git add .`, force push, reset --hard, stash. Không push, không merge vào main, không đưa lên web.
- Commit nhỏ: mỗi phần chạy được thì commit một lần, tiêu đề bắt đầu bằng mã việc, ví dụ `[G1] ...`.
- Kho GitHub đang CÔNG KHAI: không commit khóa, mật khẩu, .env, CSDL, dữ liệu cơ chế gốc, gói .verapack.
- Không xóa: Library/, Builds/, ~/AppBuild/VERALab_ui, cache Unity, /Applications/Unity, ~/Documents/Antigravity. Không tạo dự án trong ~/Documents hay ~/Desktop.
- Máy M2 có 8 GB RAM: mỗi lúc chỉ MỘT Unity -batchmode. Trước khi chạy: `pgrep -fl -- -batchmode`. Nếu đang có thì đợi, 2 phút xem lại. Unity có giao diện đang mở dự án thì batchmode báo khóa: đóng giao diện trước hoặc chạy kiểm thử từ menu trong giao diện. Dựng cảnh hay chụp ảnh thì không dùng -nographics.
- Giao diện, chú thích, tài liệu viết tiếng Việt. Tên file và thư mục mới không dấu, không cách.
- Kết thúc: viết báo cáo docs/ngay-18-11/bao-cao-<mã>.md (đã làm gì, file đã đổi, lệnh thử và kết quả bằng số, ảnh, việc còn lại), commit, rồi trả lời dòng đầu đúng dạng `XONG <mã>` hoặc `CHƯA XONG <mã>: <lý do>`.
- Quá giờ mà chưa xong: làm theo mục "Nếu trễ", để mã biên dịch được, commit, ghi phần còn lại. Không bỏ dở giữa chừng.

THƯ MỤC VÀ NHÁNH: làm trực tiếp trong ~/Developer/Antigravity, nhánh n18/antigravity (đã tạo sẵn). KHÔNG tạo worktree, vì Unity không mở được dự án nằm trong thư mục ẩn ~/.gemini. Thời gian: khoảng 2,5 giờ.

MỤC TIÊU
(1) App Mac xuất ra chạy mượt khi đi lại, xoay camera, lên thang bộ, chuyển khu.
(2) Người dùng chọn được mức đồ họa hợp với phần cứng.
(3) Hình sắc nét trên màn Retina.

ĐỌC TRƯỚC
TIEN_DO.md, BAO_CAO_WAVE6_MAC.md, docs/23-toi-uu-va-lab-sinh-tu-do.md, docs/do-hoa-hd/trang-thai.md.
Mã: Scripts/Rendering/* (FramePacing, ZoneStreamer, ZoneMap, ZoneRoot, ClusterHlod, ImpostorBillboard, LiveSet), Scripts/Environment/* (camera người chơi), Demo/IntroScreen.cs (tab Hiển thị), Editor/IosBuild.cs.
Cài đặt: ProjectSettings/QualitySettings.asset, ProjectSettings/ProjectSettings.asset (macRetinaSupport), Settings/PC_RPAsset, Settings/Mobile_RPAsset, các Renderer.

VIỆC
A. Đo trước khi sửa (bắt buộc)
 1. Viết Scripts/Editor/MacBuild.cs với các hàm:
    - Khkt.EditorTools.MacBuild.Build: StandaloneOSX, ra ~/AppBuild/VERA_mac/VERA Lab.app, NGOÀI kho git.
    - .BuildDev: như trên nhưng là Development Build.
    - .BuildWindows: StandaloneWindows64. Chỉ chạy khi có module Windows; không có thì ghi log nhẹ, không báo lỗi.
    Bản xuất không được chứa script kiểm thử (KhktWave6Runner, KhktAutoTest…): bọc chúng bằng #if UNITY_EDITOR || KHKT_TEST hoặc loại khỏi bản dựng. Không xóa script.
 2. Viết Scripts/Rendering/DoKhungHinh.cs, bật bằng tham số dòng lệnh -vera-do hoặc phím F7 (kiểm tra F7 chưa ai dùng).
    - Chạy một "đường đi chuẩn" cố định 90 giây: sảnh tầng 1 → quầy hóa chất → xoay 360° → lên thang bộ → nhà kính tầng 2 → kính hiển vi.
    - Ghi thời gian từng khung. Tính p50/p95/p99, số khung trên 33 ms và trên 50 ms, lượng cấp phát GC.
    - Ghi CSV và bản tóm tắt vào Application.persistentDataPath/do_khung/.
 3. Dựng bản Mac, chạy đường đi chuẩn 2 lần, ghi bộ số "TRƯỚC".

B. Tìm và sửa nguyên nhân giật. Dựa vào số đo, không đoán. Xét lần lượt từng nguyên nhân dưới đây, chỉ sửa cái có thật:
 - ZoneStreamer nạp hoặc gỡ khu đồng bộ gây khựng: nạp trước theo hướng đi, chia việc qua nhiều khung, dùng async.
 - Shader biên dịch lần đầu khi nhìn thấy: làm nóng trước ở màn chờ (ShaderVariantCollection, hoặc GraphicsStateCollection của Unity 6 trên Metal).
 - Cấp phát mỗi khung ở UI/IMGUI, LINQ, nối chuỗi: bỏ cấp phát trong Update/OnGUI trên đường nóng.
 - Camera cập nhật sai chỗ hoặc vật lý không nội suy: camera và vật đang cầm chạy theo LateUpdate, Rigidbody của vật đang cầm bật interpolate.
 - Bóng đổ hoặc phản chiếu cập nhật thời gian thực khi di chuyển; texture streaming thiếu ngân sách.
 - Nhịp khung: FramePacing vẫn là nơi duy nhất đặt nhịp. Xử lý màn 120 Hz (ProMotion, màn ngoài).

C. Hồ sơ phần cứng
 Tạo Scripts/Rendering/HardwareProfile.cs đúng hợp đồng dưới đây. Claude và các mô-đun khác sẽ dùng file này, KHÔNG đổi tên:

   namespace Khkt.Rendering {
     public enum MucDoHoa { Thap = 0, Vua = 1, Cao = 2, SieuNet = 3 }
     public static class HardwareProfile {
       public static MucDoHoa Muc { get; }
       public static bool TuDong { get; }
       public static event System.Action Changed;
       public static void Dat(MucDoHoa muc);
       public static void DatTuDong();
       public static MucDoHoa DeXuat();
       public static string MoTaMay();
       public static int SoManhCau { get; }     // 12 / 16 / 24 / 32
       public static int SoHatToiDa { get; }    // 200 / 500 / 1500 / 3000
       public static float TiLeRender { get; }  // 0.7 / 0.85 / 1 / 1
       public static bool BongMem { get; }
     }
   }

 - Chế độ Tự động đoán mức theo SystemInfo (tên GPU, bộ nhớ đồ họa, RAM, số nhân): Apple M1/M2 8 GB → Cao, Intel Iris Xe → Vừa, máy yếu hơn → Thấp.
 - Mỗi mức đặt các thông số: render scale (dưới 1 thì kèm bộ nâng độ phân giải FSR hoặc STP), MSAA, bóng đổ (khoảng cách, số cascade, độ phân giải), LOD bias, giới hạn mip của texture, hậu kỳ, số hạt tối đa, độ mịn quả cầu.
 - Đổi mức có tác dụng ngay, không cần khởi động lại. Lưu lựa chọn bằng PlayerPrefs. Lần chạy đầu để Tự động.
 - Tab "Hiển thị" trong IntroScreen gồm:
   + 5 chip: Tự động, Thấp, Vừa, Cao, Siêu nét;
   + dòng chữ nhỏ "Máy này: <GPU>, <RAM> GB → đề xuất <mức>";
   + nút "Đo thử 30 giây": chạy DoKhungHinh bản rút gọn rồi gợi ý mức.

 MỐC M1: ngay khi HardwareProfile.cs biên dịch được, commit RIÊNG file này với tiêu đề `[G1] HardwareProfile: hợp đồng mức đồ họa`. Ghi một dòng `M1 ĐẠT` trong câu trả lời rồi làm tiếp.

D. Thông số sắc nét
 - Bật Retina cho Mac (macRetinaSupport). Kiểm tra ở mức Cao và Siêu nét, khung hình phải có độ phân giải bằng số điểm ảnh vật lý của màn.
 - Khử răng cưa: so MSAA 4x, SMAA và TAA trên cảnh có chữ biển báo và vạch chia buret, chọn mặc định ít nhòe nhất. TAA làm nhòe chữ thì không để mặc định.
 - Bật lọc dị hướng cho vân sàn và mặt bàn. Mip bias không âm quá −0,5.
 - KHÔNG đổi vật liệu và ánh sáng nướng sẵn, vì phần đồ họa HD đó đã được duyệt.

E. Đo lại bộ số "SAU" như bước A.3, ở mức Tự động và mức Thấp. Chụp ảnh so sánh độ sắc nét trước/sau vào docs/ngay-18-11/anh/g1/.

XONG KHI
- Bản Mac dựng được bằng MacBuild.Build.
- Đường đi chuẩn ở mức Tự động trên máy này đạt p95 ≤ 18 ms và không quá 2 khung trên 50 ms mỗi phút. Không đạt thì ghi rõ nguyên nhân còn lại kèm số đo.
- Đổi mức trong Cài đặt có tác dụng ngay.
- KhktWave6Verify vẫn đạt 67/67.
- Báo cáo có bảng TRƯỚC/SAU.

NẾU TRỄ: bỏ nút "Đo thử 30 giây" và bỏ phần so sánh SMAA/TAA. Giữ MacBuild, DoKhungHinh, HardwareProfile.

PHẠM VI
- Được sửa: Scripts/Rendering/*, Scripts/Environment/* (phần camera), Scripts/Editor/MacBuild.cs, Demo/IntroScreen.cs (chỉ tab Hiển thị), ProjectSettings/QualitySettings.asset, ProjectSettings/ProjectSettings.asset (chỉ mục Mac/Retina/đồ họa), Settings/*RPAsset*, Settings/*Renderer*.
- Không sửa: HandController.cs, Menu/*, Scripts/CoChe/*, server/*.
```

### Prompt C1: Lõi cơ chế, bài demo, tư duy vẽ (gửi #3, Claude)

```text
[C1] Cơ chế Hóa: đưa lõi vào kho, làm bộ bài demo, nâng "tư duy vẽ"

LUẬT CHUNG (giữ suốt việc này)
- Dự án VERA Lab: kho git ~/Developer/Antigravity. Unity 6000.6.7f1 ở /Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app. Đọc AGENTS.md trước tiên.
- Nền tảng: Mac là chính, rồi Windows, iPad cuối cùng. Không dùng đường dẫn cứng hay API chỉ có trên Mac.
- Chỉ sửa file trong PHẠM VI. Cần sửa file ngoài phạm vi thì ghi vào báo cáo, không tự sửa.
- Git: chỉ `git add <từng file>`. Cấm `git add -A`, `git add .`, force push, reset --hard, stash. Không push, không merge vào main, không đưa lên web.
- Commit nhỏ, tiêu đề bắt đầu bằng mã việc, ví dụ `[C1] ...`.
- Kho GitHub đang CÔNG KHAI: không commit khóa, mật khẩu, .env, CSDL, dữ liệu cơ chế gốc, gói .verapack.
- Không xóa Library/, Builds/, ~/AppBuild/VERALab_ui, cache Unity, /Applications/Unity, ~/Documents/Antigravity, ~/CHEMISTRY_xem. Không sửa nhánh chemistry, không đụng LaunchAgent vn.giaichuoihuuco.*.
- RAM chỉ 8 GB: Claude không mở Unity có giao diện. Chạy Unity bằng ~/AppBuild/VERA_claude/dong_bo_va_chay.sh (script tự đợi khi đang có Unity khác chạy).
- Giao diện, chú thích, tài liệu viết tiếng Việt. Tên file mới không dấu, không cách.
- Kết thúc: viết báo cáo docs/ngay-18-11/bao-cao-<mã>.md, commit, trả lời dòng đầu `XONG <mã>` hoặc `CHƯA XONG <mã>: <lý do>`.
- Quá giờ mà chưa xong: làm theo "Nếu trễ", để mã chạy được, commit, ghi phần còn lại.

BỐI CẢNH
- Bản thử cơ chế 3D ⇄ giấy nằm ở nhánh claude/chemistry-3d-rag-mechanism-c9669e, commit 494acdb7, thư mục nguyen_mau/co_che_3d (có HOP_DONG_APP.md, QUY_UOC_VE.md, hoc_ve/).
- Lõi co_che.py (định dạng cơ chế và hàm kiểm tra RDKit), mo_hinh_3d.py, renderer.py, ve_co_che.py nằm trong backend/ của nhánh chemistry.
- Hôm nay cơ chế sẽ được GẮN VÀO VERA. Tôi đồng ý việc này.
- Làm trong worktree của phiên này. Chuyển sang nhánh n18/co-che (P0 đã tạo từ main). Bước này không cần Unity. Thời gian: khoảng 3 giờ.

VIỆC
1. Đưa mã vào nhánh mà KHÔNG mang theo dữ liệu gốc.
   - KHÔNG merge commit 494acdb7. Commit đó chứa du_lieu/khung_mau_dap_an.json, là kho mẫu học từ đáp án HSG, cần giữ kín.
   - Lấy mã (.py, .js, .html, .md) bằng `git checkout <nhánh> -- <đường dẫn>`.
   - Lấy các module thật sự cần từ backend/ của nhánh chemistry đưa vào server/ai/co_che/. Đọc các dòng import để biết cần module nào.
   - Dữ liệu kín (kho mẫu, báo cáo bộ đo, dữ liệu đáp án, bài demo) đặt ở ~/Developer/VERA_du_lieu_rieng/co_che/. Chạy `git init` ở ~/Developer/VERA_du_lieu_rieng để có lịch sử riêng tại máy. Mã đọc thư mục này qua biến môi trường VERA_DU_LIEU_RIENG, mặc định là thư mục trên.
   - Thêm vào .gitignore các mẫu tên của dữ liệu kín.
   - Kiểm tra bằng `git log --all --oneline -- '*khung_mau_dap_an*'` và `git branch -r --contains 494acdb7`. Báo tôi nếu dữ liệu này đã từng lên GitHub. Chỉ báo, không viết lại lịch sử.
2. Bộ bài demo ("cơ sở bài"): 8 bài trong ~/Developer/VERA_du_lieu_rieng/co_che/bai_demo/<ma>/. Mỗi bài có:
   - bai.json: đề, lớp, mục tiêu, điều kiện, các bước, giải thích ngắn từng bước, 3 câu hỏi kiểm tra có đáp án, lỗi hay gặp, nguồn;
   - co_che.json: theo định dạng co_che.py.
   Tám bài:
   1) SN2: CH₃Br + OH⁻.
   2) SN1: (CH₃)₃CBr + H₂O, có nhánh SN2 bị chặn (màn chắn cản trở không gian).
   3) E2: 2-bromobutan + KOH/etanol, quy tắc Zaitsev, có nhánh sai do không anti-periplanar.
   4) Cộng HBr vào propen: Markovnikov, cacbocation bậc 2.
   5) Thế Cl vào metan theo gốc tự do: khơi mào, phát triển mạch, tắt mạch.
   6) Nitro hóa benzen: thế electrophin, ion arenium cộng hưởng.
   7) Este hóa Fischer: CH₃COOH + C₂H₅OH với H₂SO₄ đặc.
   8) Bài HSG [1.1.1]propellane + DMAD → A4 (đã có).
   Hóa học phải đúng:
   - Kiểm tra từng bước bằng co_che.kiem_tra (bảo toàn nguyên tử, điện tích, hóa trị).
   - Đối chiếu SGK Hóa 11–12 chương trình GDPT 2018.
   - Bài nào không qua kiểm tra thì sửa cho qua, không được bỏ qua.
3. Sinh .3d.json và .giay.json cho cả 8 bài bằng dung_kich_ban_3d.py và hoc_ve/ve_theo_mau.py. Viết một lệnh dựng tất cả, ví dụ `python -m server.ai.co_che.dung_bai_demo`. Kết quả ra thư mục build bị git bỏ qua. Bước C2 sẽ đóng gói mã hóa.
4. Tư duy vẽ (hoc_ve/tu_duy_ve.py). Mục tiêu: vẽ được khung chưa có trong kho mẫu mà vẫn dễ nhìn.
   - Đo trước: chạy bộ đo bo_do.py trên 8 bài và 20 chất ngẫu nhiên không có trong kho mẫu (lấy từ data/chem). Ghi điểm.
   - Sửa các nguyên tắc vẽ:
     + mạch zigzag 120°;
     + vòng 6 cạnh đặt theo hướng chuẩn;
     + dò va chạm giữa nhãn và nét, đẩy lệch khi chồng;
     + hướng nhãn MeO₂C hay CO₂Me tùy phía;
     + nét đậm và nét gạch cho lập thể;
     + mũi tên cong không cắt qua chữ;
     + khung lồng vẽ kiểu "lều".
   - Đo sau và lập bảng trước/sau. Điểm của 8 bài demo không được giảm.
   - Xem nhanh bằng launch.json "co-che-3d" (so_sanh_ve.html). Chụp ảnh 8 sơ đồ giấy vào docs/ngay-18-11/anh/c1/.
5. Unit test trong tests/test_co_che_demo.py:
   - một ví dụ SN2 nhỏ nằm trong kho để test luôn chạy được;
   - nếu thư mục dữ liệu kín tồn tại thì chạy thêm cả 8 bài: qua kiểm tra RDKit, .3d.json đúng HOP_DONG_APP.md, .giay.json không có nhãn chồng nhau;
   - không có thư mục đó thì bỏ qua (skip).

XONG KHI
- 8 bài sinh đủ file.
- `python -m unittest discover -s tests` xanh (5 tệp test máy chủ hỏng sẵn từ mốc gốc do FastAPI 0.162 — xem moc-goc.md — không bắt buộc sửa; chỉ cần không có test hỏng MỚI).
- Có bảng điểm tư duy vẽ trước/sau và ảnh 8 sơ đồ.
- Đã viết báo cáo bao-cao-C1.md và commit.

NẾU TRỄ: còn 6 bài (bỏ bài 6 và 7). Tư duy vẽ chỉ sửa lỗi chồng nhãn.

PHẠM VI: server/ai/co_che/*, nguyen_mau/co_che_3d/*, tests/test_co_che*.py, .gitignore, ~/Developer/VERA_du_lieu_rieng/*. Không đụng thư mục Unity ở bước này.
```

### Prompt G2: Bàn tay người và mô hình người 3D (gửi #4, Antigravity)

```text
[G2] Bàn tay người chân thật + mô hình người 3D (thân của mình, người khác trong phòng học chung)

LUẬT CHUNG (giữ suốt việc này)
- Dự án VERA Lab: kho git ~/Developer/Antigravity. Unity 6000.6.7f1 ở /Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app. Đọc AGENTS.md trước tiên.
- Nền tảng: Mac là chính, rồi Windows, iPad cuối cùng. Không dùng đường dẫn cứng hay API chỉ có trên Mac.
- Chỉ sửa file trong PHẠM VI. Cần sửa file ngoài phạm vi thì ghi vào báo cáo, không tự sửa.
- Git: chỉ `git add <từng file>`. Cấm `git add -A`, `git add .`, force push, reset --hard, stash. Không push, không merge vào main, không đưa lên web.
- Commit nhỏ, tiêu đề bắt đầu bằng mã việc, ví dụ `[G2] ...`.
- Kho GitHub đang CÔNG KHAI: không commit khóa, mật khẩu, .env, CSDL.
- Không xóa Library/, Builds/, ~/AppBuild/VERALab_ui, cache Unity, /Applications/Unity.
- RAM 8 GB: mỗi lúc chỉ MỘT Unity -batchmode. Trước khi chạy: `pgrep -fl -- -batchmode`. Nếu đang có thì đợi, 2 phút xem lại. Dựng cảnh hay chụp ảnh thì không dùng -nographics.
- Giao diện, chú thích, tài liệu viết tiếng Việt. Tên file mới không dấu, không cách.
- Kết thúc: viết báo cáo docs/ngay-18-11/bao-cao-<mã>.md, commit, trả lời dòng đầu `XONG <mã>` hoặc `CHƯA XONG <mã>: <lý do>`.
- Quá giờ mà chưa xong: làm theo "Nếu trễ", để mã biên dịch được, commit, ghi phần còn lại.

THƯ MỤC VÀ NHÁNH: ~/Developer/Antigravity, nhánh n18/antigravity (G1 đã làm ở đây). Không tạo worktree. Thời gian: khoảng 2 giờ.

ĐỌC TRƯỚC
Demo/HandRig.cs, Demo/ArmRig.cs, Demo/HandController.cs. HandController đã có 3 cụm thay đổi được duyệt trong BAO_CAO_WAVE6_MAC.md, không được phá.
Thêm: Input/InAppHandTracker.cs, Input/ArmTracking.cs, Scripts/Network/*, docs/26-phong-hoc-chung-va-lop-hoc-tren-web.md, docs/20-mediapipe.md, Rendering/HardwareProfile.cs.

VIỆC
A. Chụp hiện trạng
 - 6 tư thế: xòe, chụm cầm, cầm bình nón, cầm pipet, vặn khóa buret, chỉ tay. Mỗi tư thế chụp từ góc nhìn người chơi và từ góc nghiêng, lưu vào docs/ngay-18-11/anh/g2/truoc_*.png.
 - Liệt kê lỗi thấy được: tỉ lệ ngón, khớp gập ngược, ngón cái không đối diện, cổ tay hở so với tay áo, ngón xuyên vào vật, da trông như nhựa.

B. Sửa bàn tay
 - Tỉ lệ theo bàn tay người thật: các đốt ngón giảm dần, ngón cái gồm 2 đốt và xương bàn, gan tay có độ dày, mu tay hơi cong, có móng tay.
 - Giới hạn góc khớp đúng sinh lý, không gập ngược. Ngón cái xoay về phía đối diện khi cầm.
 - Tư thế cầm theo loại dụng cụ (bình, ống nghiệm, pipet, buret, kẹp, que). Lưu thành bảng dữ liệu tư thế, chuyển giữa các tư thế mềm trong 0,15 s.
 - Ngón không xuyên vật: đặt đầu ngón lên bề mặt bằng raycast và IK 2 khớp đơn giản, sai lệch tối đa 3 mm.
 - Vật liệu da: màu da tự nhiên, giả tán xạ dưới da cho rẻ (wrap lighting, viền sáng ấm), không dùng shader nặng.
 - Găng tay nitrile xanh: bật/tắt trong Cài đặt > Điều khiển, mặc định BẬT khi ở trong lab (đúng quy tắc an toàn).
 - Cổ tay nối liền mạch với tay áo blouse.
 - Nhận diện tay bằng camera (InAppHandTracker) phải điều khiển đúng bàn tay mới: ánh xạ 21 điểm MediaPipe vào các khớp mới.

C. Mô hình người 3D
 - Dựng bằng mã, không tải mô hình ngoài có giấy phép không rõ ràng.
 - Hình dáng: áo blouse trắng, quần, giày, kính bảo hộ, cao khoảng 1,65 m như học sinh THPT.
 - Có 2–3 mức chi tiết (LOD) chọn theo HardwareProfile.
 - Phòng học chung: người khác hiện bằng mô hình này thay cho khối đơn giản (nếu hiện đang là khối). Đầu và hai tay đi theo dữ liệu mạng sẵn có. Có bảng tên trên đầu. Màu tay áo theo vai trò giáo viên hay học sinh.
 - Thân của chính mình: nhìn xuống thấy thân và tay áo, có thể tắt.
 - Robot giữ nguyên.

D. Kiểm thử
 - Tạo Editor/KhktN18Verify.cs (G3, G4 sẽ thêm nhóm của mình vào file này). Nhóm "G2": chụp lại 6 tư thế, không có lỗi.
 - Giả lập 1 người chơi từ xa trong chế độ thử (không cần 2 máy) để chụp mô hình người.
 - KhktWave6Verify vẫn đạt 67/67.

XONG KHI
- Có ảnh sau_*.png chụp cùng góc với truoc_*.png. Bàn tay nhìn tự nhiên, không ngón nào xuyên vật trong 6 tư thế.
- Người khác trong phòng học chung hiện bằng mô hình người.
- Đo bằng DoKhungHinh: FPS không tụt quá 5% so với số SAU của G1.

NẾU TRỄ: bỏ phần "thân của chính mình", LOD chỉ giữ 2 mức.

PHẠM VI: Demo/HandRig.cs, Demo/ArmRig.cs, Demo/HandController.cs (chỉ phần dựng và tư thế tay, không đổi luồng cầm/thả hay cổng an toàn), Input/InAppHandTracker.cs (ánh xạ khớp), Scripts/Network/*, Scripts/Avatar/* (mới), Editor/KhktN18Verify.cs (mới), shader da mới trong Lab/Shaders/.
```

### Prompt C2: Bảo vệ dữ liệu, AI tại máy, API giải chuỗi (gửi #5, Claude, cùng phiên "N18 Cơ chế")

```text
[C2] Bảo vệ dữ liệu cơ chế + AI chạy tại máy (RAG local) + API giải chuỗi + thông số phần cứng phía máy chủ

Làm tiếp C1, cùng worktree, nhánh n18/co-che. Luật chung giữ như C1. Thời gian: khoảng 3 giờ.

PHẦN 1: BẢO VỆ, CHỐNG CHÉP Ý TƯỞNG
Trong báo cáo phải nói thật: mã hóa phía app chỉ chặn được sao chép thông thường; người quyết tâm dịch ngược app vẫn lấy được khóa. Vì vậy làm 3 lớp:
 a) Dữ liệu gốc không bao giờ vào kho công khai. Phần tách dữ liệu đã làm ở C1. Thêm tests/test_khong_lo_du_lieu.py dò `git ls-files` tìm tên tệp hoặc nội dung giống khóa và dữ liệu kín.
 b) Gói mã hóa cho app: server/ai/co_che/dong_goi.py tạo <ma>.verapack.
    - Cấu trúc: "VERA1" + phiên bản + salt + IV + AES-256-CBC(gzip(JSON gộp bai + 3d + giay)) + HMAC-SHA256. Mã hóa trước rồi ký; so HMAC bằng hàm thời gian hằng.
    - Dùng AES-CBC kèm HMAC thay cho AES-GCM, vì Aes.Create() chạy trên mọi nền tảng Unity/IL2CPP, còn AesGcm có thể không có trên iOS.
    - Khóa 32 byte, sinh ngẫu nhiên một lần, lưu ở ~/Developer/VERA_du_lieu_rieng/khoa/vera_co_che.key.
    - Chép khóa vào Unity tại Assets/Resources/CoChe/khoa.bytes (git bỏ qua) để bản dựng có khóa. Trong app, khóa được che (XOR với các hằng đặt rải nhiều nơi). Ghi rõ việc che này chỉ làm khó thêm.
    - Đóng dấu ẩn trong gói (mã máy dựng + ngày) để lần ra nguồn nếu bị rò.
    - Gói xuất ra Assets/StreamingAssets/CoChe/*.verapack (git bỏ qua).
 c) "Bộ não" ở máy chủ: kho mẫu, tư duy vẽ, bộ đo và RAG chỉ chạy ở máy chủ Python (máy Mac này hoặc máy GPU thuê). App chỉ nhận hình đã dựng.
 Test: đóng gói rồi giải gói phải khớp; sửa 1 byte thì gói bị từ chối.

PHẦN 2: AI TẠI MÁY (RAG LOCAL)
 - server/ai/cau_hinh_may.py: đọc RAM và chip/GPU (Apple Silicon dùng sysctl; Windows dùng psutil hoặc wmic; Linux dùng nvidia-smi), rồi chọn mô hình:
   + RAM ≤ 8 GB → qwen2.5:3b-instruct;
   + 16 GB → qwen2.5:7b-instruct;
   + GPU ≥ 24 GB → vera-hoa-32b (đã có Modelfile).
   File này cũng chọn số lần thử dựng cấu dạng RDKit và thời gian chờ. Cho phép ghi đè bằng biến môi trường. Đây là phần "thông số phần cứng" phía máy chủ.
 - Tải mô hình cho máy này: `ollama pull qwen2.5:3b-instruct`, khoảng 2 GB. Kiểm tra đĩa trước, còn dưới 6 GB thì hỏi tôi.
 - Tạo Modelfile nhỏ vera-hoa-3b: num_ctx 8192, cùng lời dặn với Modelfile 32B. Đặt keep_alive 5 phút để trả RAM cho Unity. Thử xong chạy `ollama stop`.
 - RAG: dùng server/ai/rag.py có sẵn (BM25 + vector bge-m3, mô hình này đã có trong ollama). Thêm nguồn kiến thức cơ chế: 8 bài demo và các quy tắc cơ chế đã kiểm chứng. Đọc từ thư mục riêng; chỉ mục cũng lưu ở thư mục riêng, không vào git.
 - Điểm cuối /co-che/giai-thich:
   + nhận câu hỏi và mã bài; dùng RAG và mô hình tại máy để trả lời tiếng Việt, có trích nguồn;
   + mô hình KHÔNG được tự tạo cơ chế mới, chỉ giải thích và điền chất vào mẫu đã kiểm chứng;
   + mọi cơ chế AI đề xuất phải qua co_che.kiem_tra, trượt thì trả về lời giải thích theo mẫu;
   + thứ tự nhà cung cấp: tại máy trước, trực tuyến sau (dùng cơ chế thứ tự nhà cung cấp có sẵn).

PHẦN 3: API GIẢI CHUỖI
 - Đọc cách app CHEMISTRY giải chuỗi trên nhánh chemistry: backend/pipeline.py, giai_nhanh.py, parser.py, co_che.py và route /api/giai trong main.py.
 - Chép phần lõi cần thiết vào server/ai/co_che/giai_chuoi.py. Không import chéo từ ~/CHEMISTRY_xem, không sửa nhánh chemistry, không đụng LaunchAgent vn.giaichuoihuuco.*.
 - Thêm vào máy chủ VERA cục bộ (FastAPI trong server/) các điểm cuối:
   + GET /co-che/bai: danh sách bài;
   + GET /co-che/bai/{ma}: trả gói .verapack;
   + POST /co-che/giai-chuoi với dữ liệu ví dụ {"chuoi": "C2H4 -> C2H5OH -> CH3CHO -> CH3COOH"}: trả từng bước gồm chất, điều kiện, loại phản ứng, và mã cơ chế nếu khớp mẫu (kèm .3d/.giay đã dựng). Phần nào không chắc thì ghi "chưa kiểm chứng".
 - Có 3 chuỗi mẫu chạy được khi không có mạng.
 - Cổng: kiểm tra xung đột cổng 8000 với dịch vụ CHEMISTRY. Nếu trùng thì máy chủ cơ chế của VERA chạy cổng 8010 (cấu hình được). Ghi URL này vào nguyen_mau/co_che_3d/HOP_DONG_APP.md, mục mới "Máy chủ cơ chế", để C3 dùng.

KIỂM THỬ: tests/test_co_che_api.py (dùng FastAPI TestClient), cùng test cho cau_hinh_may và dong_goi. Toàn bộ `python -m unittest discover -s tests` phải xanh (5 tệp test máy chủ hỏng sẵn từ mốc gốc do FastAPI 0.162 — xem moc-goc.md — không bắt buộc sửa; chỉ cần không có test hỏng MỚI).

XONG KHI
- Có 8 gói .verapack (hoặc 6 nếu C1 đã cắt bớt).
- Ba điểm cuối chạy được.
- Báo cáo bao-cao-C2.md có bảng đo: thời gian trả lời của /co-che/giai-thich với mô hình 3B (giây), RAM dùng, kết quả 5 câu hỏi mẫu (đúng hay sai, tự chấm theo SGK).
- Đã commit.

NẾU TRỄ: bỏ đóng dấu ẩn và phần chọn mô hình cho Windows/Linux. Giữ gói mã hóa, /co-che/bai, /co-che/giai-thich và 3 chuỗi mẫu.

PHẠM VI: server/ai/co_che/*, server/ai/cau_hinh_may.py, các điểm cuối mới trong server/, tests/test_co_che*.py, tests/test_khong_lo_du_lieu.py, .gitignore, ~/Developer/VERA_du_lieu_rieng/*.
```

### Prompt G3: Mô hình 3D thực vật (gửi #6, Antigravity)

```text
[G3] Mô hình 3D thực vật: cấu trúc, chức năng, chuyển động dạy học theo thời gian thực

LUẬT CHUNG (giữ suốt việc này)
- Dự án VERA Lab: kho git ~/Developer/Antigravity. Unity 6000.6.7f1 ở /Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app. Đọc AGENTS.md trước tiên.
- Nền tảng: Mac là chính, rồi Windows, iPad cuối cùng. Không dùng đường dẫn cứng hay API chỉ có trên Mac.
- Chỉ sửa file trong PHẠM VI. Cần sửa file ngoài phạm vi thì ghi vào báo cáo, không tự sửa.
- Git: chỉ `git add <từng file>`. Cấm `git add -A`, `git add .`, force push, reset --hard, stash. Không push, không merge vào main, không đưa lên web.
- Commit nhỏ, tiêu đề bắt đầu bằng `[G3]`.
- Không xóa Library/, Builds/, ~/AppBuild/VERALab_ui, cache Unity, /Applications/Unity.
- RAM 8 GB: mỗi lúc chỉ MỘT Unity -batchmode. Trước khi chạy: `pgrep -fl -- -batchmode`. Nếu đang có thì đợi. Dựng cảnh hay chụp ảnh thì không dùng -nographics.
- Giao diện, chú thích, tài liệu viết tiếng Việt. Tên file mới không dấu, không cách.
- Kết thúc: viết báo cáo docs/ngay-18-11/bao-cao-G3.md, commit, trả lời dòng đầu `XONG G3` hoặc `CHƯA XONG G3: <lý do>`.
- Quá giờ mà chưa xong: làm theo "Nếu trễ", để mã biên dịch được, commit, ghi phần còn lại.

THƯ MỤC VÀ NHÁNH: ~/Developer/Antigravity, nhánh n18/antigravity. Thời gian: khoảng 3 giờ.

ĐỌC TRƯỚC
Demo/Bio/PhotosynthesisRig.cs, PotometerRig.cs, PhototropismBox.cs, NftRackController.cs, MistSystem.cs, ClimateSensor.cs, TeachingModel.cs.
Cách dựng hình bằng mã: Editor/LabBuilder/LabKit*.cs. Nhà kính tầng 2: Editor/LabBuilder/Tang2/*.
Thêm: data/sinh/*, docs/20-tang-2-khu-sinh-hoc.md, Rendering/HardwareProfile.cs.

NỘI DUNG KHOA HỌC
Phải đúng chương trình Sinh 10–11 GDPT 2018. Con số nào cũng ghi nguồn trong data/sinh/thuc_vat.json.

1. Cấu trúc
 Một cây hai lá mầm, ví dụ cây đậu: rễ (chóp rễ, miền lông hút), thân, lá, hoa.
 Có ba lát cắt phóng to, bấm vào là mở:
  - Rễ: lông hút, vỏ, nội bì có đai Caspari, mạch gỗ, mạch rây.
  - Thân: biểu bì, vỏ, bó mạch (gỗ ở trong, rây ở ngoài), tầng sinh mạch.
  - Lá: biểu bì có lớp cutin, mô giậu, mô xốp, gân lá, khí khổng với 2 tế bào hạt đậu.
 Mỗi bộ phận có chú thích bật/tắt được và thẻ kiến thức (tên, cấu tạo, chức năng, câu hỏi nhanh).

2. Chức năng chạy theo thời gian thực
 Chỉnh bằng các thanh trượt: ánh sáng (lux), CO₂ (ppm), nhiệt độ (°C), độ ẩm không khí (%), độ ẩm đất (%), gió.
  - Hút nước: dòng hạt đi lên trong mạch gỗ, tốc độ tỉ lệ với thoát hơi nước. Dòng đường trong mạch rây đi xuống.
  - Thoát hơi nước qua khí khổng: khí khổng mở/đóng theo ánh sáng, độ ẩm và tình trạng thiếu nước (ABA). Thấy rõ tế bào hạt đậu trương lên hay xẹp xuống.
  - Quang hợp: CO₂ đi vào, O₂ đi ra. Cường độ theo đường bão hòa ánh sáng và CO₂, có điểm bù ánh sáng và nhiệt độ tối ưu. Ban đêm chỉ còn hô hấp.
  - Hướng sáng: chiếu đèn một phía, auxin dồn về phía tối, thân uốn cong dần (có tua nhanh thời gian).
  - Tua nhanh ngày/đêm: ×1, ×60, ×600.
 Nếu PhotosynthesisRig hay PotometerRig đã có công thức thì dùng chung hàm, không được tạo con số mâu thuẫn.
 Phần mô phỏng viết bằng C# thuần, đặt ở Scripts/Simulation/Bio/ThucVat (không using UnityEngine), có unit test.

3. Chế độ dạy học
  - "Bài giảng": 5 bước có lời dẫn (cấu trúc → hút nước → thoát hơi nước → quang hợp → hướng sáng).
  - "Tự do": dùng thanh trượt tùy ý.
  - "Câu hỏi": 5 câu, chấm điểm ngay.

KỸ THUẬT
 - Dựng hình bằng mã theo kiểu LabKit: thân là ống cong, lá là mặt có gân, rễ phân nhánh. Không dùng mô hình tải về. Lá đung đưa bằng shader nhẹ (Lab/Shaders/LaCay*).
 - Dòng chảy trong mạch làm bằng shader cuộn UV hoặc bằng hạt. Số hạt không quá HardwareProfile.SoHatToiDa.
 - Mở bằng Khkt.Bio.ThucVat.ThucVatMoDun.Mo(), kèm Dong() và bool DangMo (theo hợp đồng chung, không đổi tên).
   + Mở ra là lớp phủ toàn màn hình với sân khấu riêng (layer riêng).
   + Camera quỹ đạo: kéo chuột để xoay, cuộn để phóng to, nhấp đúp vào bộ phận để lấy nét. Esc để đóng.
   + Viết lớp camera quỹ đạo dùng chung được, vì G4 sẽ dùng lại.
 - Lối vào trong lab: đặt "bàn mô hình thực vật" ở nhà kính tầng 2, gắn lúc chạy (runtime) để khỏi phải dựng lại cảnh. Ngắm vào rồi bấm thì gọi Mo().

KIỂM THỬ (nhóm "G3" trong Editor/KhktN18Verify.cs)
 - Mở và đóng mô-đun.
 - Kéo thanh trượt, số phải đổi đúng chiều: ánh sáng tăng thì quang hợp tăng tới mức bão hòa; độ ẩm không khí tăng thì thoát hơi nước giảm.
 - Chụp 6 ảnh vào docs/ngay-18-11/anh/g3/.
 - Unit test phần mô phỏng phải qua.
 - KhktWave6Verify vẫn đạt 67/67.

XONG KHI: đủ 3 lát cắt, 4 quá trình, chế độ Bài giảng; có lối vào ở nhà kính; tất cả kiểm thử đạt; khi mở mô-đun ở mức Cao đạt ≥ 60 FPS.

NẾU TRỄ: bỏ hoa và chế độ "Câu hỏi". Giữ 3 lát cắt và 4 quá trình.

PHẠM VI: Demo/Bio/ThucVat/*, Simulation/Bio/ThucVat/*, Lab/Shaders/LaCay*, data/sinh/thuc_vat.json, Editor/KhktN18Verify.cs (nhóm G3).
```

### Prompt C3: Trình xem cơ chế và bảng giải chuỗi trong Unity (gửi #7, Claude, cùng phiên "N18 Cơ chế")

```text
[C3] Trình xem cơ chế 3D ⇄ giấy và bảng giải chuỗi trong Unity (làm cho Mac trước)

Làm tiếp C2, cùng worktree, nhánh n18/co-che. Luật chung giữ như C1. Thời gian: khoảng 3 giờ.

ĐIỀU KIỆN TRƯỚC KHI LÀM
 - Antigravity đã commit `[G1] HardwareProfile` (mốc M1). Merge n18/antigravity vào n18/co-che để có HardwareProfile.cs. Nếu nhánh kia đang dở dang thì chỉ cherry-pick commit HardwareProfile.
 - KHÔNG mở Unity có giao diện. Biên dịch và chạy thử bằng ~/AppBuild/VERA_claude/dong_bo_va_chay.sh. Script tự đợi khi có Unity -batchmode khác đang chạy.

ĐỌC
nguyen_mau/co_che_3d/HOP_DONG_APP.md, QUY_UOC_VE.md, ve_giay.js, hsg_propellane.html. Bản thử là chuẩn cho giao diện và cách phát.
Thêm: Rendering/HardwareProfile.cs, và Demo/GlassKit.cs, UiTheme.cs, ModernUI.cs, ToolPreview.cs (theo phong cách giao diện hiện có).

VIỆC (namespace Khkt.CoChe, thư mục Phong thi nghiem ao/Assets/Scripts/CoChe/)
 1. GoiCoChe.cs
    - Đọc .verapack từ StreamingAssets/CoChe, hoặc tải từ máy chủ cơ chế.
    - Kiểm HMAC, giải AES-CBC, giải nén. Tất cả chỉ diễn ra trong bộ nhớ, không ghi bản rõ ra đĩa.
    - Test giải được đúng gói mẫu do Python tạo.
 2. Lớp dữ liệu theo đúng hợp đồng .3d.json và .giay.json. Đổi dấu trục z để sang hệ tọa độ trái của Unity.
 3. TrinhPhat3D.cs
    - Nguyên tử là quả cầu: màu CPK, bán kính theo nguyên tố, độ mịn lấy HardwareProfile.SoManhCau, vẽ bằng GPU instancing.
    - Liên kết là hình trụ, thể hiện bậc 1, 2, 3.
    - Nội suy giữa các trạng thái như bản thử: electron đi trước, nguyên tử bắt đầu dịch từ khoảng 35% thời gian của bước.
    - Mỗi mũi tên là hạt electron bay theo đường cong. Tổng số hạt không quá SoHatToiDa.
    - Liên kết mới thì mọc dần, liên kết mất thì mờ dần.
    - Màn chắn chỉ hiện khi nhánh sai đang bị cản, bình thường ẩn hẳn.
    - Có nút "Ẩn H".
    - Điều khiển: kéo chuột xoay 360°, cuộn để phóng to, phím ← → chuyển bước, Space để phát/dừng.
 4. SoDoGiay.cs
    - Vẽ .giay.json bằng một UI Graphic tự dựng lưới.
    - Thể hiện được: nét đơn, đôi, ba; nét phía sau có khe che; mũi tên cong nửa đầu (Bézier); mũi tên phản ứng có chữ trên và dưới; chấm gốc tự do; nhãn A1…
    - Chữ dùng TextMeshPro, chỉ số dưới Unicode (CO₂Me) hiện đúng.
    - Phải giống ve_giay.js: chụp hai bản cạnh nhau để so.
 5. Chuyển 3D ⇄ Giấy
    - Chỉ có hoạt cảnh lúc chuyển: 3D trải phẳng ra rồi tờ giấy hiện lên, 0,8 s, mượt.
    - Ô "Giải thích" mặc định đóng, bấm mới mở.
    - Nút "Hỏi AI" gọi /co-che/giai-thich. Không kết nối được máy chủ thì nút mờ đi và ghi rõ lý do.
 6. Bảng giải chuỗi
    - Có ô nhập chuỗi và 3 chuỗi mẫu.
    - Gọi /co-che/giai-chuoi, hiện kết quả từng bước dạng thẻ.
    - Bước nào có cơ chế thì bấm vào là mở trình phát.
 7. Khkt.CoChe.CoCheMoDun với Mo(), Dong(), DangMo (theo hợp đồng chung)
    - Mở ra là lớp phủ toàn màn hình, có danh sách 8 bài.
    - Phím tắt trong lab: chọn phím chưa ai dùng. Kiểm tra KeyboardMouseControllerSource, HandController, IntroScreen; hiện đã dùng F7–F12, Esc, B, T, H…
    - Gắn phím tắt bằng [RuntimeInitializeOnLoadMethod]. Không sửa HandController, không sửa cảnh.
 8. Cài đặt máy chủ cơ chế: ô nhập URL, mặc định theo HOP_DONG_APP.md (127.0.0.1:8010 hoặc cổng C2 đã chọn), lưu bằng PlayerPrefs.
 9. Kiểm thử: Editor/KhktCoCheVerify.cs có hàm Run.
    - Nạp 8 gói, phát hết các bước ở hai mức Thấp và Cao, chuyển qua lại hai chế độ.
    - Mở bảng giải chuỗi với máy chủ giả.
    - Chụp ảnh từng bài ở cả hai chế độ vào Path.GetTempPath()/vera_coche_shots, rồi chép sang docs/ngay-18-11/anh/c3/.
    - 0 lỗi biên dịch, 0 ngoại lệ. KhktWave6Verify vẫn đạt 67/67.

XONG KHI: tất cả mục trên đạt; khi phát bài propellane ở mức Cao đạt ≥ 60 FPS trên máy này; đã viết báo cáo bao-cao-C3.md và commit.

NẾU TRỄ: hoạt cảnh chuyển chế độ thay bằng mờ chéo; bảng giải chuỗi chỉ hiện chữ.

PHẠM VI: Scripts/CoChe/*, Editor/KhktCoCheVerify.cs, StreamingAssets/CoChe/ và Resources/CoChe/ (git bỏ qua). Không sửa file của Antigravity.
```

### Prompt G4: Giải phẫu người trong app (gửi #8, Antigravity)

```text
[G4] Phần giải phẫu người chạy ngay trong app (Mac không phải mở trình duyệt)

LUẬT CHUNG (giữ suốt việc này)
- Dự án VERA Lab: kho git ~/Developer/Antigravity. Unity 6000.6.7f1 ở /Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app. Đọc AGENTS.md trước tiên.
- Nền tảng: Mac là chính, rồi Windows, iPad cuối cùng. Không dùng đường dẫn cứng hay API chỉ có trên Mac.
- Chỉ sửa file trong PHẠM VI. Cần sửa file ngoài phạm vi thì ghi vào báo cáo, không tự sửa.
- Git: chỉ `git add <từng file>`. Cấm `git add -A`, `git add .`, force push, reset --hard, stash. Không push, không merge vào main, không đưa lên web.
- Commit nhỏ, tiêu đề bắt đầu bằng `[G4]`.
- Không xóa Library/, Builds/, ~/AppBuild/VERALab_ui, cache Unity, /Applications/Unity.
- RAM 8 GB: mỗi lúc chỉ MỘT Unity -batchmode. Trước khi chạy: `pgrep -fl -- -batchmode`. Nếu đang có thì đợi. Dựng cảnh hay chụp ảnh thì không dùng -nographics.
- Giao diện, chú thích, tài liệu viết tiếng Việt. Tên file mới không dấu, không cách.
- Kết thúc: viết báo cáo docs/ngay-18-11/bao-cao-G4.md, commit, trả lời dòng đầu `XONG G4` hoặc `CHƯA XONG G4: <lý do>`.
- Quá giờ mà chưa xong: làm theo "Nếu trễ", để mã biên dịch được, commit, ghi phần còn lại.

THƯ MỤC VÀ NHÁNH: ~/Developer/Antigravity, nhánh n18/antigravity. Thời gian: khoảng 3 giờ. Đây là việc cuối của Antigravity hôm nay.

HIỆN TRẠNG
BIO-LAB 3D (StreamingAssets/biology_lab, dữ liệu BodyParts3D trong sinh-hoc/bp3d.bin.gz, 15 cơ quan, thẻ kiến thức) mở bằng WebView trên iPad. Trên Mac nó lại mở trình duyệt ngoài, vì BioWebViewBridge gọi Application.OpenURL. Mac là nền tảng chính nên cần một bản chạy ngay trong Unity.

ĐỌC TRƯỚC
Demo/Bio/Portal/* (BioWebViewBridge, BioPortalTransition…), Demo/Bio/DigitalAnatomyTable.cs, SkeletonStand.cs, TeachingModel.cs.
StreamingAssets/biology_lab/biology-lab-3d.html: đọc đoạn JS nạp bp3d.bin.gz để biết định dạng.
Thêm: thư mục sinh-hoc/kien-thuc và sinh-hoc/giai-thich, docs/27-cong-phong-sinh-hoc-3d.md, Rendering/HardwareProfile.cs, lớp camera quỹ đạo G3 vừa làm.

VIỆC
 1. Bộ đọc bp3d.bin.gz bằng C#
    - Giải nén GZip, dựng Mesh, dùng chỉ số 32-bit khi cần.
    - Chỉ nạp hệ nào đang cần, không nạp hết lúc vào app.
    - Giảm lưới theo HardwareProfile.
 2. Khkt.Bio.GiaiPhau.GiaiPhauMoDun với Mo(), Dong(), DangMo (theo hợp đồng chung). Có sân khấu riêng, dùng lại camera quỹ đạo của G3.
 3. Chức năng
    - Bật/tắt từng hệ: xương, cơ, tiêu hóa, tuần hoàn, hô hấp, thần kinh, bài tiết, nội tiết (hệ nào có dữ liệu thì làm).
    - Bóc lớp: thanh trượt từ da → cơ → xương và nội tạng, lớp ngoài mờ dần.
    - Mặt cắt: mặt phẳng cắt dọc hoặc ngang, kéo được (dùng shader cắt).
    - Bấm vào cơ quan: viền sáng và hiện thẻ kiến thức (dùng nội dung tiếng Việt có sẵn trong sinh-hoc/kien-thuc). Nút "Tách ra" đưa cơ quan ra giữa để xoay riêng.
    - Hoạt cảnh dạy học:
      + tim đập, nhịp chỉnh được 60–120 lần/phút, dòng máu đỏ và xanh đi qua các ngăn;
      + phổi phồng và xẹp theo nhịp thở;
      + nhu động ruột (biến dạng đỉnh đơn giản).
    - Câu hỏi nhanh: đọc tên cơ quan, người học chỉ đúng vào cơ quan đó (5 câu).
    - Ghi công BodyParts3D (giấy phép CC BY-SA 2.1 JP) trong mô-đun và trong docs.
 4. Lối vào
    - Trên Mac và Windows: DigitalAnatomyTable và cổng tầng 2 mở GiaiPhauMoDun thay cho trình duyệt.
    - iPad giữ nguyên đường WebView cũ. Không sửa BioWebViewPlugin.mm.
 5. Nếu còn giờ: bài thực hành mổ ảo dùng các dụng cụ có sẵn (Scissors, Forceps, DissectingNeedle). Không kịp thì chỉ ghi ý tưởng vào báo cáo.
 6. Cuối G4: nếu G2, G3, G4 có thay đổi cần dựng lại cảnh thì dựng lại MỘT lần ở đây (menu KHKT > Dựng phòng thí nghiệm, có giao diện, không -nographics). Sau đó chạy lại toàn bộ KhktWave6Verify và KhktN18Verify.

KIỂM THỬ (nhóm "G4" trong Editor/KhktN18Verify.cs)
 - Mở/đóng 5 lần, bộ nhớ không tăng mãi (đo bằng Profiler.GetTotalAllocatedMemoryLong).
 - Bật lần lượt từng hệ, bấm thử 3 cơ quan, dùng mặt cắt.
 - Chụp 6 ảnh vào docs/ngay-18-11/anh/g4/.
 - Lần mở đầu tiên mất ≤ 3 giây trên máy này.

XONG KHI: trên Mac mở được giải phẫu ngay trong app; mọi kiểm thử đạt; KhktWave6Verify vẫn 67/67.

NẾU TRỄ: bỏ nhu động ruột và câu hỏi. Giữ bật/tắt hệ, bóc lớp, bấm cơ quan, tim đập.

PHẠM VI: Demo/Bio/GiaiPhau/*, Demo/Bio/DigitalAnatomyTable.cs, Demo/Bio/Portal/BioWebViewBridge.cs và BioPortalTransition.cs (chỉ nhánh Mac/Windows), Lab/Shaders/MatCat*, Editor/KhktN18Verify.cs (nhóm G4).
```

### Prompt C4: Giao diện ngoài lab và độ sắc nét (gửi #9, Claude, cùng phiên "N18 Cơ chế")

```text
[C4] Đồ họa giao diện ngoài lab (màn hình chính, bảng điều khiển, cài đặt, biểu tượng app) + độ sắc nét của giao diện

Làm tiếp C3, cùng worktree, nhánh n18/co-che. Luật chung giữ như C1. Thời gian: khoảng 2,5 giờ.

TRƯỚC KHI LÀM: merge bản mới nhất của n18/antigravity vào n18/co-che (chỉ lấy những gì đã commit, vì Antigravity đang làm G4), rồi biên dịch lại.

ĐỌC
Menu/MainMenu.cs, Menu/Dashboard/* (DashboardShell, PageOverview, MenuKit), Demo/UiTheme.cs, GlassKit.cs, ModernUI.cs, UiMotion.cs, NutHieuUng.cs, IntroScreen.cs.
BAO_CAO_WAVE6_MAC.md (mục 7.7 và 7.8), docs/giao-dien-hd/, docs/do-hoa-hd/art-bible.md.
trang_gioi_thieu/: lấy màu và logo của trang giới thiệu để đồng bộ thương hiệu.
Giao diện ở đây dựng bằng mã. Không sửa tay YAML của cảnh trừ khi bắt buộc.

VIỆC
 1. Chụp hiện trạng mọi màn ngoài lab, ở 1440×900 và 2880×1800 (Retina), với cả 3 nền. Lưu vào docs/ngay-18-11/anh/c4/truoc_*. Chụp theo cách KhktWave6Runner đang chụp.
 2. Độ sắc nét
    - Chỉnh CanvasScaler (độ phân giải tham chiếu, mức match).
    - Chữ TMP: atlas phải đủ ký tự tiếng Việt có dấu (thử "Ặ ỷ ữ ợ ự ẫ"), cỡ atlas và padding đủ sắc trên Retina, phóng to không mờ.
    - Ảnh giao diện: tắt mipmap, không nén mất nét, làm ở kích thước 2x.
    - Viền 1 px phải thẳng hàng điểm ảnh.
 3. Đồ họa
    - Màn chính: nền là ảnh dựng thật của phòng lab (chụp bằng camera trong cảnh, làm mờ nhẹ, phủ một lớp màu thương hiệu) thay cho nền phẳng. Logo VERA Lab rõ nét. Nút "Vào phòng thí nghiệm" nổi bật.
    - Bảng điều khiển (DashboardShell): thẻ mô-đun có ảnh thu nhỏ cho Phòng Hóa, Phòng Sinh, Cơ chế 3D, Thực vật 3D, Giải phẫu.
      Mỗi thẻ gọi Mo() bằng cách tìm lớp theo tên trong mọi assembly đã nạp: Khkt.CoChe.CoCheMoDun, Khkt.Bio.ThucVat.ThucVatMoDun, Khkt.Bio.GiaiPhau.GiaiPhauMoDun. Lớp nào chưa có thì thẻ ghi "Sắp có". Như vậy biên dịch không lỗi dù nhánh bên kia chưa gộp.
    - Màn chờ khi nạp cảnh: thanh tiến trình thật (AsyncOperation.progress) kèm mẹo an toàn phòng thí nghiệm.
    - Biểu tượng app cho Mac (từ ảnh 1024 ra đủ các cỡ) và Windows. Tên cửa sổ là "VERA Lab".
    - Giữ đúng 3 nền của UiTheme, nút cao tối thiểu 44 pt, hoạt cảnh nhẹ theo UiMotion.
 4. Chụp lại ảnh "sau" cùng khung với ảnh "trước", đặt cạnh nhau để so.
 5. Kiểm thử: KhktWave6Verify phải đạt (nhất là mục 7.7 và 7.8). Thêm nhóm giao diện vào KhktCoCheVerify hoặc tạo file KhktUiVerify.cs.

XONG KHI: mọi màn ngoài lab sắc nét trên Retina; chữ tiếng Việt không vỡ dấu; có các thẻ mô-đun và biểu tượng app; kiểm thử đạt; đã viết báo cáo bao-cao-C4.md và commit.

NẾU TRỄ: bỏ màn chờ và biểu tượng Windows.

PHẠM VI: Menu/*, Menu/Dashboard/*, Demo/UiTheme.cs, GlassKit.cs, ModernUI.cs, UiMotion.cs, NutHieuUng.cs, IntroScreen.cs (trừ tab Hiển thị của G1), Lab/Logo/*, ảnh giao diện mới, ProjectSettings (chỉ biểu tượng và tên).
```

### Prompt C5: Gộp, kiểm thử, xuất app Mac (gửi #10, Claude, phiên mới "N18 Tích hợp")

```text
[C5] Gộp hai nhánh, kiểm thử toàn bộ, xuất app Mac (và Windows nếu được), tổng kết ngày 18/11

LUẬT CHUNG
- Dự án VERA Lab: kho git ~/Developer/Antigravity. Unity 6000.6.7f1 ở /Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app. Đọc AGENTS.md trước tiên. Ưu tiên Mac, rồi Windows, iPad cuối cùng.
- Git: chỉ `git add <từng file>`. Cấm `git add -A`, force push, reset --hard, stash. KHÔNG merge vào main, KHÔNG push, KHÔNG đưa lên web. Chờ tôi quyết.
- Kho GitHub đang CÔNG KHAI: không để lọt khóa, .env, CSDL, dữ liệu cơ chế gốc, gói .verapack.
- Không xóa Library/, Builds/, ~/AppBuild/VERALab_ui, cache Unity, /Applications/Unity.
- Dựng cảnh hay chụp ảnh thì không dùng -nographics.

ĐIỀU KIỆN: Antigravity đã báo XONG G4 (hoặc đã dừng và commit); Claude đã báo XONG C4; tôi đã tắt Unity có giao diện. Chạy `pgrep -fl Unity`: nếu còn Unity thì dừng và nhắc tôi.

VIỆC
 1. Gộp nhánh ở ~/Developer/Antigravity
    - Kiểm tra thư mục sạch. Tạo nhánh n18/tich-hop từ main.
    - Merge --no-ff n18/antigravity, rồi n18/co-che.
    - Gặp xung đột: giữ ý của cả hai bên và theo bảng PHẠM VI (file của ai thì bên đó quyết). Xung đột khó thì dừng và hỏi tôi.
 2. Kiểm tra
    - `python -m unittest discover -s tests` (dùng .venv). So với mốc gốc: không có test hỏng MỚI; 5 tệp test máy chủ hỏng sẵn do FastAPI 0.162 thì ghi lại, không chặn.
    - Unity biên dịch 0 lỗi.
    - KhktWave6Verify đạt 67/67, so với mốc trong docs/ngay-18-11/moc-goc.md.
    - Toàn bộ KhktN18Verify (G2–G4) và KhktCoCheVerify đạt.
    - Quét Windows: không có đường dẫn cứng /Users, /tmp, ~; không có API chỉ có trên Mac nằm ngoài #if.
    - Quét lộ dữ liệu: chạy tests/test_khong_lo_du_lieu.py; `git diff main --stat` không có .key, .env, .db, .verapack hay khung_mau.
 3. Cảnh: nếu G4 chưa dựng lại cảnh sau G2–G4 thì dựng lại ở đây (cần GPU, không -nographics).
 4. Xuất app Mac
    - Dùng Khkt.EditorTools.MacBuild.Build, ra ~/AppBuild/VERA_mac/VERA Lab.app (ngoài kho git).
    - Mở app thật bằng `open`. Chạy DoKhungHinh (tham số -vera-do) ở mức Tự động. Ghi p50, p95 và số khung giật, so với số TRƯỚC của G1.
    - Kiểm tra app có khóa giải gói (mở được bài cơ chế), và máy chủ cơ chế cục bộ chạy được theo cách C2 đã ghi (cổng 8010 hoặc cổng đã chọn).
 5. Windows: nếu có module Windows thì chạy MacBuild.BuildWindows, ra ~/AppBuild/VERA_win/ để chép sang máy Windows thử. Không có module thì ghi danh sách việc cần thử trên máy Windows.
 6. iPad: KHÔNG làm hôm nay (theo thứ tự ưu tiên). Chỉ ghi những chỗ có thể vướng: WebView, cảm ứng cho các mô-đun mới.
 7. Duyệt mã nhanh toàn bộ thay đổi trong ngày (`git diff main...n18/tich-hop`): tìm lỗi rõ ràng, rò bộ nhớ, null, cấp phát mỗi khung, đường dẫn. Lỗi nhỏ thì sửa, lỗi lớn thì ghi lại.
 8. Tổng kết
    - Viết docs/ngay-18-11/TONG_KET.md: bảng 9 việc trong ghi chú (N1–N9) với trạng thái Xong / Một phần / Chưa, số đo, ảnh, việc mai làm tiếp.
    - Cập nhật TIEN_DO.md (mục Đã xong và Đang dở).
    - Commit trên n18/tich-hop.
 9. Cho máy ngủ: tắt máy chủ cơ chế và ollama nếu C5 đã bật (`ollama stop` mô hình đang chạy), rồi tạo tệp ~/AppBuild/XONG_NGAY_18-11. Script canh_gac_ngu.sh sẽ cho máy ngủ khi tôi không dùng máy 15 phút. Nếu script đó không chạy (kiểm bằng `pgrep -f canh_gac_ngu.sh`) thì tự hẹn: `nohup sh -c 'sleep 300; pmset sleepnow' >/dev/null 2>&1 &`.

TRẢ LỜI: dòng đầu `XONG C5`. Sau đó là bảng N1–N9, đường dẫn app, các số đo chính, và 5 thao tác tôi nên tự bấm thử.
```

### Prompt R: Duyệt nhanh việc của Antigravity (gửi #4R, #8R, tùy chọn, Claude phiên mới)

```text
[R] Duyệt nhanh việc Antigravity vừa làm. Chỉ đọc, KHÔNG sửa, không commit.

Ở ~/Developer/Antigravity, nhánh n18/antigravity, xem các commit có tiêu đề bắt đầu bằng [G1] (hoặc [G2], [G3] — tôi ghi ở cuối tin này). Dùng `git log --oneline main..n18/antigravity` và `git show`.

Tìm các lỗi sau:
- lỗi biên dịch hoặc lỗi chạy trên Windows (đường dẫn cứng, API chỉ có trên Mac);
- NullReference, rò bộ nhớ, cấp phát mỗi khung trong Update/OnGUI;
- sửa file ngoài phạm vi được giao. Phạm vi: G1 = Rendering, Environment (camera), MacBuild, tab Hiển thị của IntroScreen, Quality/URP. G2 = HandRig, ArmRig, phần tay trong HandController, InAppHandTracker, Network, Avatar. G3 = Demo/Bio/ThucVat, Simulation/Bio/ThucVat, LaCay, thuc_vat.json;
- làm hỏng 3 cụm HandController đã duyệt (xem BAO_CAO_WAVE6_MAC.md);
- hợp đồng HardwareProfile hoặc ThucVatMoDun bị đổi tên;
- khóa, mật khẩu, dữ liệu lọt vào commit.

Trả lời tối đa 10 mục. Mỗi mục ghi: file:dòng, lỗi, cách sửa trong 1 câu, mức "Phải sửa" hoặc "Nên sửa". Không thấy gì thì ghi "Không thấy lỗi đáng kể".

Mã việc cần duyệt: G1
```

### Prompt S: Dừng an toàn khi trễ hoặc kẹt (gửi cho AI đang trễ)

```text
Dừng việc đang làm. Đưa mã về trạng thái biên dịch được. Commit phần đã chạy được với tiêu đề `[<mã việc>] dừng giữa chừng`.
Ghi vào báo cáo docs/ngay-18-11/bao-cao-<mã>.md: đã xong gì, đang kẹt ở đâu (kèm thông báo lỗi), cần gì để làm tiếp.
Không xóa gì, không reset --hard, không stash. Trả lời dòng đầu `CHƯA XONG <mã>: <lý do ngắn>`.
```

---

## 8. Thử tay cuối ngày (10 phút, bạn tự làm)

1. Mở `~/AppBuild/VERA_mac/VERA Lab.app`. Màn chính phải nét, chữ tiếng Việt không vỡ dấu.
2. Vào Cài đặt > Hiển thị, đổi qua lại Thấp và Siêu nét: hình phải đổi ngay.
3. Vào lab, đi từ tầng 1 lên thang bộ tới tầng 2: không bị khựng.
4. Nhìn bàn tay khi cầm bình và pipet. Bật/tắt găng tay.
5. Mở Cơ chế 3D, chọn bài SN1: phải thấy nhánh SN2 bị chặn. Chuyển sang Giấy. Bấm "Hỏi AI" (cần bật máy chủ cơ chế).
6. Giải một chuỗi mẫu.
7. Vào nhà kính, mở Thực vật 3D: kéo thanh ánh sáng, xem khí khổng mở và đóng.
8. Mở Giải phẫu, bật hệ tuần hoàn: tim phải đập.

Có lỗi thì ghi lại để làm tiếp ngày mai, kèm `docs/ngay-18-11/TONG_KET.md`.
