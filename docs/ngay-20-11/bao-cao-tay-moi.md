# Báo cáo: làm lại tay và cánh tay người học (VERA Lab, Unity)

- **Nhánh:** `tay-chan-thuc` (cắt từ `n18/tich-hop` @ `a58a3ba6`), ngày 20/11/2026
- **Máy:** MacBook Air M2, Unity 6000.6.7f1 (URP, Forward+), ưu tiên Mac > Windows > iPad
- **Tài nguyên ngoài:** không dùng. Toàn bộ lưới, vật liệu, chi tiết bề mặt dựng bằng mã, không texture, không tải về.

## 1. Hiện trạng trước khi sửa (ảnh `anh/tay-moi/truoc_*.png`)

- Bàn tay ghép từ ~45 viên nang/cầu/hộp rời: đốt ngón như chuỗi xúc xích, khớp là quả cầu lồi, móng là tấm hộp phẳng.
- **Hai lớp tay áo chồng nhau:** cổ tay áo trụ của `HandRig` + ống trụ `ArmRig` (thấy rõ ở `truoc_5_ong_nghiem_goc_nghieng.png`).
- Cánh tay là hai viên nang cứng + cầu ở vai, khuỷu; không có cẳng tay xoay sấp/ngửa, không nếp vải.
- Không lắc khi đi / xoay camera; `ArmRig` chạy trước hoặc sau camera tùy khung (trễ 1 khung).
- IK dụng cụ: bình nón 0,51 mm, pipet 0,45 mm, khóa buret 0 mm, **ống nghiệm 7,51 mm (vượt mốc 3 mm)**.
- Ánh xạ MediaPipe chỉ đo góc giữa các đoạn (không dấu), độ dạng/khép ngón cố định, ngón cái gần đúng.
- Shader `Khkt/DaNguoi` không được tài nguyên nào tham chiếu → bản app có thể không có shader này (rơi về URP Lit).
- Cấp phát mỗi khung: `new bool[]` trong IK, `new Vector3[21]` khi dùng webcam.

## 2. Đã làm

### Bàn tay (`Scripts/Avatar/TayNguoiLuoi.cs`, `Scripts/Demo/HandRig.cs`)
- **Một lưới da liền khối có skinning 17 xương**: gan tay, 4 ngón × 3 đốt (MCP–PIP–DIP), ngón cái CMC (3 bậc tự do:
  gập, dạng–đối chiếu từ −35° đến 68°, xoay sấp) – MCP – IP, và xương cổ tay ảo nằm trong ống tay áo.
- Dựng bằng **trường khoảng cách (SDF)** ghép mềm các khối giải phẫu: phiến gan tay có vòm ngang, gờ xương bàn, gồ khớp
  bàn–ngón, mô cái, mô út, đệm gan tay, màng kẽ ngón cái, cổ tay dẹt, mỏm trâm trụ; mỗi đốt ngón là nón tròn dẹt có gồ
  khớp mu, đệm thịt mặt lòng, đệm đầu ngón. Ngón chỉ ghép mềm với gan tay nên không dính nhau.
- Đa giác hóa **surface nets** + chiếu đỉnh lên mặt + pháp tuyến theo gradient SDF → mặt cong mượt (Cao: ~28 600 đỉnh).
- **Trọng số skinning** theo khối gần nhất + toạ độ dọc chuỗi khớp, chỉ trộn cha–con quanh khớp → gập ngón giữa hết cỡ,
  da ngón trỏ dịch 0,000 mm.
- **Nếp gấp** (màu đỉnh R): nếp lòng gốc ngón, PIP (đôi), DIP, nếp nhăn mu khớp, đường chỉ tay (tim, đầu, đời), nếp cổ tay.
  **AO nướng sẵn** từ SDF (màu đỉnh B), mặt lòng (G), độ mỏng đầu ngón (A).
- **5 móng tay** cong bám đúng mặt mu đốt xa (dò mặt bằng SDF), có liềm móng, bờ tự do dày; khi đeo găng hiện gờ móng dưới găng.
- **Cổ găng nitrile** cuộn mép quanh cổ tay. Công tắc găng ⇄ da giữ nguyên (`HandRig.WearGloves`).
- Dựng lưới trên **luồng nền** (không chặn khung hình), bộ nhớ đệm dùng chung giữa các bàn tay cùng bên.
- **Mức chi tiết theo `HardwareProfile`**: Siêu nét / Cao / Vừa / Thấp = 41 130 / 28 578 / 14 565 / 7 110 đỉnh; Thấp dùng
  skinning 2 xương; bóng đổ của tay và tay áo chỉ bật ở Cao trở lên.

### Vật liệu (`Lab/Shaders/TayNguoi.shader`, nạp qua `Scripts/Avatar/Resources/TayNguoi/TayNguoi.mat`)
- Một shader ba chế độ, hỗ trợ Forward+ (vòng đèn cụm), bóng đổ, SSAO, sương mù:
  - **Da:** bọc sáng + dải tán xạ dưới da + sáng xuyên mép mỏng, hai lớp phản xạ dầu da (GGX), lỗ chân lông, vân nếp khớp,
    lòng bàn tay hồng nhạt hơn, khớp/đầu ngón đỏ hơn.
  - **Găng nitrile:** bóng mềm, hạt nhám mịn, sáng xuyên đầu ngón, phồng 0,6 mm theo pháp tuyến.
  - **Vải blouse:** khuếch tán mềm, ánh vải ở rìa (sheen), sợi dệt chéo mờ dần theo khoảng cách để không nhiễu răng cưa.
- Chi tiết vi mô tính theo vị trí tư thế gốc (UV1) nên không "trôi" khi khớp gập; nổi khối bằng surface gradient, không cần texture.
- Vật liệu nằm trong `Resources` → shader chắc chắn được đưa vào bản dựng.

### Cánh tay (`Scripts/Demo/ArmRig.cs`)
- Mỗi tay **một ống tay áo blouse skinning 5 xương** (cánh tay trên, khuỷu, 2 xương xoắn cẳng tay, cổ tay áo): nếp gấp khuỷu
  dồn ở mặt trong, nếp rủ dọc, nếp dúm trên cổ tay áo, đường may mặt dưới, dải cổ tay áo dày có 2 đường chỉ, mép cuộn và mặt vải trong.
  **Blendshape "gập khuỷu"** làm nếp sâu dần theo góc gập.
- **IK hai khớp vai – khuỷu – cổ tay** có giới hạn sinh lý: khuỷu 0–150° (không duỗi quá), vai đưa trước/lùi khi với xa/gần;
  hướng khuỷu chọn tự động để cổ tay ít gập nhất rồi lọc bằng lò xo tới hạn.
- **Xoay sấp/ngửa** cẳng tay (−80…+88°) chia dần cho 2 xương xoắn; xương cổ tay ảo xoay theo cẳng tay (giới hạn gập cổ tay 70°)
  nên da cổ tay uốn mượt trong ống tay áo, không hở (đo: 0,00 mm ở mọi tư thế).
- **Lắc theo bước đi, trễ + nghiêng khi xoay camera, nhịp thở** bằng lò xo tới hạn giải đúng nghiệm (không phụ thuộc FPS).
- Chạy sau camera (`DefaultExecutionOrder(1000)`) → hết trễ 1 khung. Tự gọi `HandRig.Tick` đúng thứ tự (vị trí tay → tư thế/IK → cánh tay).
- Bỏ cổ tay áo trùng lặp của `HandRig` khi có `ArmRig` (`ownSleeve`); mô hình người từ xa (`HumanoidAvatar`) vẫn có cổ tay áo ngắn.

### Chuyển động ngón tay
- Nội suy tư thế **0,15 s cố định theo thời gian** (smootherstep bậc 5), ngón lệch pha nhẹ (cái → trỏ → út, tổng thời gian không đổi).
- Chuyển động "sống" rất nhẹ khi tay rảnh (vài độ theo nhịp thở, tắt khi cầm dụng cụ hoặc khi kiểm thử ép tư thế).
- IK bề mặt chống xuyên dụng cụ giữ nguyên thuật toán, nay dùng **bán kính đệm đầu ngón đo trên lưới thật** (+ độ dày găng),
  không cấp phát; ống nghiệm chuyển sang kẹp ba điểm (cái + trỏ + giữa) và đặt lại gan tay.
- **MediaPipe 21 điểm:** khung gan tay khớp từ 4 điểm (0, 5, 9, 17) theo đúng hình học bàn tay ảo; mỗi ngón giải gập + dạng/khép
  MCP, gập PIP, DIP bằng nghịch đảo đúng thứ tự xoay khớp; ngón cái giải CMC 3 bậc (xoay sấp lấy từ mặt phẳng gập MCP–IP),
  MCP, IP. Khứ hồi 60 tư thế ngẫu nhiên: sai số 0,00°. Không cấp phát.
- **Chuột/bàn phím và tay cầm VERA Band:** tay phải vẫn theo `HandController` (chuột hoặc cò/cần của tay cầm qua `PadInput`),
  không đổi luồng điều khiển; tay trái nghỉ, cùng lắc theo bước đi.

## 3. Kiểm thử

| Bộ | Kết quả |
|---|---|
| `Khkt.EditorTools.KhktTayMoiVerify.Run` (mới) | `passes=32 failures=0 runtimeErrors=0` – **ALL PASSED** (gồm bộ đệm đĩa) |
| `Khkt.EditorTools.KhktN18Verify.RunG2` | `passes=16 failures=0 runtimeErrors=0` – **ALL PASSED** |
| `KhktWave6Verify.Run` | 63 đạt, 1 lỗi + hết giờ ở **bước 7.7** "Guest entered DashboardShell" (màn menu chính, bấm "Nút Dùng thử" → `DashboardShell` null, `KhktWave6Runner.cs:1182`). Bước này không có bàn tay; `MainMenu.cs`/`KhktUiRunner.cs` vừa được phiên Giải bài tập sửa trong ngày → ngoài phạm vi, cần phiên đó xem lại. Mọi bước phòng lab (1–7.4) đạt. |

Chi tiết bộ mới (lần chạy cuối):

- Lưới da liền khối 17 xương: 28 578 đỉnh / 57 152 tam giác (Cao); 57 147/57 152 tam giác hướng đúng ra ngoài; trọng số chuẩn hóa.
- Gập ngón giữa hết cỡ: da ngón trỏ dịch **0,000 mm**, đầu ngón giữa dịch 124 mm.
- Mức chi tiết: 41 130 / 28 578 / 14 565 / 7 110 đỉnh; dựng trên luồng nền 2,5–15 s trong Editor (tùy máy bận), không chặn khung.
- MediaPipe 21 điểm, khứ hồi 60 tư thế ngẫu nhiên: gan tay / ngón / ngón cái **0,00° / 0,00° / 0,00°**; 0 byte cấp phát.
- Nội suy 0,15 s: 30 FPS và 150 FPS lệch **0,000°** giữa chừng và cuối; bước lớn nhất 8,3°/khung ở 150 FPS.
- Lò xo lắc: 30 FPS vs 150 FPS lệch 0,0000 mm. Xoay camera 120°/s: lệch 13,2 mm rồi về 0,9 mm sau 1,5 s. Đi 1,3 m/s: nhún 6,6 mm.
- IK hai khớp (2000 điểm ngẫu nhiên): chiều dài xương lệch 0,000 mm, khuỷu gập 7,0–150,0°.
- IK dụng cụ (pen = số lần xuyên): bình nón 0/0,74 mm · **ống nghiệm 0/1,58 mm (trước: 7,51 mm)** · pipet 0/2,35 mm · khóa buret 0/0,00 mm.
- Ống tay áo – cổ tay hở **0,00 mm** ở cả 8 tư thế; khuỷu 10–56°, xoay sấp −54…+32°, gập cổ tay ≤ 34°.
- Cập nhật hai cánh tay + bàn tay + IK: **0 byte / 200 khung**, 0,27–0,76 ms/khung (Editor).

## 4. Ảnh trước / sau (`docs/ngay-20-11/anh/tay-moi/`)

Mỗi tư thế 2 góc: `*_goc_nguoi_choi.png` (góc người chơi) và `*_goc_nghieng.png` (cận cảnh nghiêng).

| Tư thế | Trước | Sau |
|---|---|---|
| 1 Xòe | [người chơi](anh/tay-moi/truoc_1_xoe_goc_nguoi_choi.png), [nghiêng](anh/tay-moi/truoc_1_xoe_goc_nghieng.png) | [người chơi](anh/tay-moi/sau_1_xoe_goc_nguoi_choi.png), [nghiêng](anh/tay-moi/sau_1_xoe_goc_nghieng.png) |
| 2 Nghỉ | [người chơi](anh/tay-moi/truoc_2_nghi_goc_nguoi_choi.png), [nghiêng](anh/tay-moi/truoc_2_nghi_goc_nghieng.png) | [người chơi](anh/tay-moi/sau_2_nghi_goc_nguoi_choi.png), [nghiêng](anh/tay-moi/sau_2_nghi_goc_nghieng.png) |
| 3 Chụm cầm | [người chơi](anh/tay-moi/truoc_3_chum_cam_goc_nguoi_choi.png), [nghiêng](anh/tay-moi/truoc_3_chum_cam_goc_nghieng.png) | [người chơi](anh/tay-moi/sau_3_chum_cam_goc_nguoi_choi.png), [nghiêng](anh/tay-moi/sau_3_chum_cam_goc_nghieng.png) |
| 4 Bình nón | [người chơi](anh/tay-moi/truoc_4_binh_non_goc_nguoi_choi.png), [nghiêng](anh/tay-moi/truoc_4_binh_non_goc_nghieng.png) | [người chơi](anh/tay-moi/sau_4_binh_non_goc_nguoi_choi.png), [nghiêng](anh/tay-moi/sau_4_binh_non_goc_nghieng.png) |
| 5 Ống nghiệm | [người chơi](anh/tay-moi/truoc_5_ong_nghiem_goc_nguoi_choi.png), [nghiêng](anh/tay-moi/truoc_5_ong_nghiem_goc_nghieng.png) | [người chơi](anh/tay-moi/sau_5_ong_nghiem_goc_nguoi_choi.png), [nghiêng](anh/tay-moi/sau_5_ong_nghiem_goc_nghieng.png) |
| 6 Pipet | [người chơi](anh/tay-moi/truoc_6_pipet_goc_nguoi_choi.png), [nghiêng](anh/tay-moi/truoc_6_pipet_goc_nghieng.png) | [người chơi](anh/tay-moi/sau_6_pipet_goc_nguoi_choi.png), [nghiêng](anh/tay-moi/sau_6_pipet_goc_nghieng.png) |
| 7 Khóa buret | [người chơi](anh/tay-moi/truoc_7_khoa_buret_goc_nguoi_choi.png), [nghiêng](anh/tay-moi/truoc_7_khoa_buret_goc_nghieng.png) | [người chơi](anh/tay-moi/sau_7_khoa_buret_goc_nguoi_choi.png), [nghiêng](anh/tay-moi/sau_7_khoa_buret_goc_nghieng.png) |
| 8 Chỉ tay | [người chơi](anh/tay-moi/truoc_8_chi_tay_goc_nguoi_choi.png), [nghiêng](anh/tay-moi/truoc_8_chi_tay_goc_nghieng.png) | [người chơi](anh/tay-moi/sau_8_chi_tay_goc_nguoi_choi.png), [nghiêng](anh/tay-moi/sau_8_chi_tay_goc_nghieng.png) |
| Da trần cận cảnh | [ảnh](anh/tay-moi/truoc_9_da_tran_can_canh.png) | [ảnh](anh/tay-moi/sau_9_da_tran_can_canh.png) |
| Hai cánh tay (góc ngoài) | [ảnh](anh/tay-moi/truoc_10_hai_canh_tay.png) | [ảnh](anh/tay-moi/sau_10_hai_canh_tay.png) |

Đã tự xem từng ảnh: góc người chơi giờ là hai ống tay áo vải trắng liền mạch có nếp, cổ tay áo khít găng; nhìn nghiêng thấy mu bàn tay
có gồ khớp, gờ gân, ngón thuôn dần, đầu ngón tròn; da trần có lỗ chân lông, nếp khớp, móng có liềm; góc ngoài thấy nếp gấp khuỷu,
cổ tay áo, mép cuộn.

## 5. Hiệu năng

- Không cấp phát bộ nhớ mỗi khung (đo: 0 byte / 200 khung cầm bình nón có IK; 0 byte / 200 lần ánh xạ MediaPipe).
- CPU tay + cánh tay + IK: 0,27–0,76 ms/khung (Editor, chưa tối ưu như bản app).
- GPU: thêm ~2 × 28,6 nghìn đỉnh skinning + 2 × 5,4 nghìn đỉnh tay áo ở mức Cao (Thấp: 7,1 nghìn / 2,6 nghìn).
- **Xuất app Mac** (`Khkt.EditorTools.MacBuild.Build`): Succeeded, 0 lỗi, 248,4 MB (lần cuối 45,4 s) → `~/AppBuild/VERA_mac/VERA Lab.app`.

## 6. Việc còn lại / ghi chú

- **Đo khung hình – A/B cùng điều kiện.** Suốt buổi tối có một bản VERA Lab khác chạy song song (PID 70352, mở 23:09, ~28% CPU,
  không do phiên này mở, không tắt) nên mọi lần đo đều bị nhiễu so với mốc 18/11 (p95 17,60 ms). Để tách chi phí của tay, thêm cờ
  `-vera-an-tay` (ẩn tay + cánh tay) và đo xen kẽ 4 × 90 s cùng màn hình 3840×2160, mức Cao, vSync=2:

  | Lần | Có tay | Ẩn tay |
  |---|---|---|
  | 1 | TB 19,41 ms · p50 16,67 · **p95 33,32** · p99 41,00 | TB 24,99 ms · p50 24,99 · **p95 33,52** · p99 41,80 |
  | 2 | TB 21,60 ms · p50 17,05 · **p95 33,36** · p99 33,98 | TB 20,28 ms · p50 16,73 · **p95 33,27** · p99 33,59 |

  → Ẩn tay vẫn p95 ≈ 33,3 ms: chênh lệch của tay mới nằm trong mức nhiễu; p95 cao là do máy đang chạy song song một bản app khác
  (vSync=2 nên khung trễ nhảy thẳng lên 33,3 ms). Muốn có số so trực tiếp với mốc 17,6 ms: đóng mọi cửa sổ VERA Lab rồi chạy
  `"$HOME/AppBuild/VERA_mac/VERA Lab.app/Contents/MacOS/VERA Lab" -vera-do -vera-muc=tudong -vera-nhan=tay_moi_sach`.
- **Bộ đệm lưới trên đĩa** (`persistentDataPath/tay_luoi/*.bin`, commit `8dd32aed`): lần đầu dựng trên luồng nền (~5 s trong Editor,
  tay hiện khi màn giới thiệu còn mở); các lần sau đọc lại **66 ms**. Kiểm thử mới thêm 1 mục → **32/32**.
- Bước 7.7 của Wave6 lỗi ở màn menu chính (ngoài phạm vi tay), cần phiên Giải bài tập/giao diện xem.
- Bộ `KhktN18Verify.RunG2` ghi đè ảnh `docs/ngay-18-11/anh/g2/sau_*.png` mỗi lần chạy; các ảnh đó đang bị đổi trong thư mục
  làm việc (không đưa vào commit này). Muốn giữ ảnh cũ: `git checkout -- docs/ngay-18-11/anh/g2`.
- Windows: mã không dùng API riêng Mac (Task.Run, Mesh API chung); chưa có mô-đun Windows trên máy để dựng thử.
- iPad: mức Thấp/Vừa phù hợp; chưa xuất thử (iPad xếp sau cùng).
