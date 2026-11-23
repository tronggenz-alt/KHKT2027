# Báo cáo C3: Trình xem cơ chế 3D ⇄ Giấy và bảng giải chuỗi trong Unity (Mac trước)

Phiên "N18 Cơ chế" (Claude), nhánh `n18/co-che`, ngày 18/11/2026. **Kết quả: XONG C3.**
- Mốc M1 đã có. Tôi đã merge `n18/antigravity` (`[G1] HardwareProfile`, `[G1] MacBuild/DoKhungHinh`) vào `n18/co-che`.
- Unity chỉ chạy ngầm qua `~/AppBuild/VERA_claude/dong_bo_va_chay.sh`. Không mở Unity có giao diện.
- Không sửa tệp của Antigravity, không sửa HandController, không sửa cảnh.

## Đã làm (namespace `Khkt.CoChe`, thư mục `Assets/Scripts/CoChe/`)

| Tệp | Việc |
|---|---|
| `GoiCoChe.cs` | Đọc `.verapack` từ `StreamingAssets/CoChe`. Kiểm HMAC-SHA256 bằng phép so thời gian hằng **trước** khi giải mã. Giải AES-256-CBC bằng `Aes.Create()` (chạy được trên Mac, Windows, iOS/IL2CPP), giải nén gzip. Mọi bước nằm trong bộ nhớ, không ghi bản rõ ra đĩa. Khóa ở `Resources/CoChe/khoa.bytes` (git bỏ qua) đã được **che**: XOR với SHA256 của ba hằng đặt ở `GoiCoChe`, `TrinhPhat3D`, `CoCheMoDun`; `dong_goi.py --unity` ghi khóa đã che. Việc che chỉ làm khó thêm, không phải bảo mật thật (xem báo cáo C2). |
| `JsonNho.cs` | Bộ đọc JSON nhỏ, vì `JsonUtility` không đọc được dạng từ điển `{id: [x,y,z]}`. |
| `DuLieuCoChe.cs` | Lớp dữ liệu theo đúng hợp đồng `.3d.json`, `.giay.json` và `bai`. **Đổi dấu trục z** sang hệ tọa độ trái của Unity. Đọc cả các trường mới thêm ngày 18/11: nhánh sai, màn chắn, điện tích, nét đậm/gạch, ⇌. |
| `TrinhPhat3D.cs` | Quả cầu màu CPK, bán kính theo nguyên tố, độ mịn theo `HardwareProfile.SoManhCau`; trụ liên kết bậc 1/2/3. Vẽ bằng **GPU instancing** (`Graphics.RenderMeshInstanced`, mỗi nguyên tố một lô) qua camera riêng. Cách phát giống bản thử: electron đi trước, nguyên tử dịch từ khoảng 35% thời gian bước, liên kết mới mọc dần, liên kết mất co dần. Hạt electron bay theo đường Bézier, có vệt, tổng số ≤ `SoHatToiDa`. **Màn chắn chỉ hiện khi đang phát nhánh sai**, electron dừng ngay trước màn chắn. Có nút Ẩn H. Kéo chuột xoay 360°, cuộn để phóng to, ← → chuyển bước, Space phát/dừng. Phân tử đặt ở xa khu lab (y = −500); camera chỉ vẽ lớp UI, không lẫn với cảnh. |
| `SoDoGiay.cs` | Bản Unity của `veSoDo` (ve_giay.js): nét đơn/đôi/ba, nét đôi trong vòng có nét thứ hai ngắn ở phía trong, nét "sau" vẽ trước và nét trước có khe che, nêm đặc/nêm gạch, mũi tên cong Bézier (một ngạnh cho 1 e, hai ngạnh cho 2 e), mũi tên phản ứng có chữ trên/dưới, ⇌, chấm gốc tự do, điện tích, nhãn. Chữ dùng phông Be Vietnam Pro của GlassKit, hiện đúng chỉ số dưới Unicode (CO₂Me, MeO₂C). |
| `MayChuCoChe.cs` | Gọi `/co-che/*` bằng UnityWebRequest. URL lưu trong PlayerPrefs, mặc định `http://127.0.0.1:8010` (HOP_DONG mục 5). Có móc `GiaLap` để chạy máy chủ giả khi kiểm thử. |
| `CoCheMoDun.cs` | `Mo()`, `Dong()`, `DangMo`. Lớp phủ toàn màn hình (camera riêng, canvas Screen Space-Camera 1920×1080). Danh sách 8 bài đọc từ gói. Chuyển 3D ⇄ Giấy trong 0,8 s: hình 3D trải phẳng (z → 0) rồi tờ giấy hiện lên. Ô "Giải thích" mặc định đóng. Nút "Hỏi AI" gọi `/co-che/giai-thich`, hiện nguồn [n]; không kết nối được máy chủ thì nút mờ đi, chữ thành "Hỏi AI (tắt)" và ghi rõ lý do. Có bảng giải chuỗi (ô nhập, 3 chuỗi mẫu, thẻ từng bước; bước có cơ chế thì bấm để mở trình phát). Có ô cài đặt URL máy chủ. Khi mở lớp phủ: `DesktopViewer.UiCapture++` để khóa đi lại trong lab, tạm tắt các camera khác để trả GPU, đóng thì bật lại. |
| Phím tắt | **M**. Đã kiểm tra mọi phím trong `Assets/Scripts`: Esc, Enter, mũi tên, A B C D E F G H Q R S T W X Z, Space, Tab, Shift, Ctrl, Alt, PgUp, PgDn, số 0–4, F2–F4, F7–F12 đã có chỗ dùng. M thì chưa. Gắn bằng `[RuntimeInitializeOnLoadMethod]` (lớp `PhimTatCoChe`); khi đang gõ vào ô nhập chữ thì phím M không có tác dụng. |
| `Resources/CoCheVe/*.mat` | 3 vật liệu URP (Lit, Unlit, Unlit trong suốt) **bật GPU instancing**. Mục đích: bản dựng giữ lại biến thể shader có instancing; nếu chỉ tạo vật liệu bằng mã thì Unity có thể loại các biến thể này khi dựng. |
| `KhktCoCheRunner.cs`, `Editor/KhktCoCheVerify.cs` | Bộ kiểm thử, hàm `Run`. |
| `StreamingAssets/CoChe/`, `Resources/CoChe/` | 8 gói và khóa đã che. Cả hai đều **git bỏ qua**. |

- **Lệch so với đề (TextMeshPro):** dự án hiện không có TMP. Không tệp nào dùng `TMPro` và chưa nhập TMP Essential Resources. Mọi giao diện (GlassKit) dùng `UnityEngine.UI.Text` với phông Be Vietnam Pro nhúng sẵn, đủ dấu tiếng Việt và chỉ số dưới. Tôi dùng cùng cách đó. Nếu nhập TMP Essential vào thì phải thêm nhiều tài nguyên và có thể đụng việc của Antigravity và C4. Khi cả dự án chuyển sang TMP thì chỉ cần đổi hàm `ThemChu` trong `SoDoGiay`.
- **Lỗi đã gặp và cách sửa:**
  - Bản đầu vẽ nét giấy bằng lưới tự dựng (`OnPopulateMesh`). Lưới có đủ đỉnh nhưng **không hiện** trên camera Screen Space-Camera; chưa tìm ra vì sao.
  - Tôi đổi sang mỗi đoạn nét là một `Image` xoay, đúng cách các bảng khác đang dùng. Sau khi đổi, ảnh khớp bản web.
  - Thêm sửa lỗi lệch khung: phải đặt pivot trước khi đặt offset.

## Kiểm thử: `KhktCoCheVerify.Run`, 46/46 đạt, 0 lỗi, 0 ngoại lệ, 0 `error CS`

- Giải đúng gói thử do Python tạo (khóa, salt, IV cố định; chuỗi "Ặ ỷ ữ"). Gói sửa 1 byte bị từ chối ở cả 6/6 vị trí. Sai khóa bị từ chối.
- Nạp đủ **8 gói thật**, 8/8 giải được.
- **Phát hết các bước ở mức Thấp và mức Cao**: mỗi mức 41/41 bước. Bước có mũi tên đều có hạt electron. Số hạt ≤ `SoHatToiDa` (200 ở mức Thấp, 1.500 ở mức Cao).
- Nhánh sai của SN1 ("SN2 bị chặn") và E2 ("H gauche") phát được, có màn chắn.
- Chuyển 3D → Giấy → 3D cho cả 8 bài; mỗi lần mất khoảng 0,84 s.
- Sơ đồ giấy dựng đủ nét cho cả 8 bài, ví dụ propellane có 39 nét và 9 mũi tên cong.
- Ô Giải thích mặc định đóng. Hỏi AI nhận được trả lời từ máy chủ giả.
- Bảng giải chuỗi hiện 4 thẻ bước; thẻ có cơ chế có nút "Xem cơ chế" hoặc "Cơ chế tương tự".
- Khi không có máy chủ (`127.0.0.1:9`): nút Hỏi AI mờ, có ghi lý do "Không kết nối được…".
- `Mo()`/`Dong()` tăng rồi trả lại `UiCapture` đúng.
- **FPS**: propellane ở mức Cao đạt **≈ 619 FPS** (Apple M2). **Lưu ý thật:** số này đo trong Editor batchmode, cửa sổ 640×480, nên chưa đại diện cho toàn màn hình Retina. Có dư nhiều so với ngưỡng 60 FPS, nhưng cần đo lại trên bản dựng Mac (`MacBuild`) ở 2880×1800.
- **`KhktWave6Verify`:** xem mục cuối.

## Ảnh (Path.GetTempPath()/vera_coche_shots → `docs/ngay-18-11/anh/c3/`, thu về 1280 px)

- Có 2 chế độ (`_3d`, `_giay`) cho từng bài SGK, thêm ảnh nhánh sai (`sn1_tbubr_nhanh_sai`, `e2_2_brombutan_nhanh_sai`), `sn2_ch3br_giai_thich`, `bang_giai_chuoi`, `khong_may_chu`.
- Ảnh bài HSG propellane (`hsg_propellane_a4_3d/_giay`) **giữ ở** `~/Developer/VERA_du_lieu_rieng/co_che/anh/c3/`, vì đó là lời giải đề HSG (cùng lý do như ở C1).
- **So với `ve_giay.js`:** đặt `anh/c1/<bài>.png` (bản web) cạnh `anh/c3/<bài>_giay.png` (bản Unity). Cùng bố cục, nét, nhãn, mũi tên cong và khung lều. Khác biệt: Unity tô mũi tên cong màu đỏ như trang xem thử, nét hơi dày hơn vì tỉ lệ khung lớn hơn.

## Còn lại

- Một số chữ trên chip đang bật (nút "Phát" khi đang phát) hơi nhạt, do màu chip của GlassKit. Phần này để C4 xử lý chung về độ sắc nét.
- Cần đo FPS trên bản dựng Mac thật ở Retina.

## KhktWave6Verify

- Trên nhánh `n18/co-che` (sau khi gộp G1 và có mã C3): lần chạy đầu đạt **65/67**, trượt 2 mục 7.2 (công thức pha loãng V₁ = null,
  thanh rót trước = sau = 5); 0 lỗi runtime.
- Để biết lỗi có sẵn hay do C3, tôi chạy Wave6 trên bản checkout tạm của `n18/antigravity` (7117e35d, không có mã cơ chế): **67/67**.
- Chạy lại trên `n18/co-che`, không sửa gì: **67/67**, 0 lỗi runtime, 12 ảnh. Kết luận: mục 7.2 **chập chờn** (phụ thuộc thời điểm các chip
  "Nồng độ/Thể tích đích" dựng xong), không do mô-đun cơ chế. Mô-đun cơ chế chỉ thêm một đối tượng nghe phím M; lớp phủ không mở trong Wave6.
  Nên báo Antigravity để tăng thời gian chờ ở bước 7.2 của KhktWave6Runner (tệp đó ngoài phạm vi của tôi).
