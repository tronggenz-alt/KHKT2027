# Tầng 2 – Khu Sinh Học (VERA Lab)

Tài liệu thiết kế, kiến trúc, đồ họa và mô phỏng cho **Tầng 2 – Khu sinh học** của dự án phòng thí nghiệm ảo VERA Lab (KHKT2027, Unity 6000.6.7f1, URP 17.6.0).

---

## 1. Bố cục không gian và kiến trúc

Bố cục tầng 2 được dựng chính xác theo bản đồ quy hoạch mặt bằng trang 13 (`phong_sinh/mat_bang_tang2.png`) và thông số tọa độ tại `phong_sinh/bo_cuc_tang2.json`.

- **Mặt sàn tầng 2:** $y = 3,8\text{ m}$, trần cao $y = 7,4\text{ m}$ (chiều cao lọt lòng 3,6 m).
- **Gốc thế giới Tầng 2:** GameObject `"Tầng 2"` đặt tại tọa độ `(0, 3.8, 0)`.
- **Hệ thống kết nối liên tầng:**
  - **Lõi thang bộ & thang máy:** đặt tại tọa độ $x \in [27, 30]\text{ m}$, $z \in [-2.2, 6]\text{ m}$ bao trọn cả hai tầng ($y = 0 \to 7,4\text{ m}$).
  - Cầu thang bộ một vế thẳng gồm 22 bậc, có mặt dốc va chạm ẩn ($32,61^\circ$) giúp `CharacterController` di chuyển mượt mà không bị khựng bậc.
  - Thang máy thông tầng (`ElevatorController.cs`) hỗ trợ đóng mở cửa có giảm tốc, hiệu ứng mờ chuyển tầng và chuông báo tầng.
  - Lõi thang đặt hoàn toàn ngoài khung phòng thí nghiệm tầng 1, không khoét sàn tầng 1, bảo toàn 100% kết cấu và thẩm mỹ tầng 1.

### Bản đồ phân khu Tầng 2

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

### 8 phân khu mới (Chỉ số ZoneMap 5 đến 12)

| Chỉ số | Mã khu | Tên hiển thị | Tọa độ giới hạn $(x, z)$ | Tầng |
|:---:|---|---|---|:---:|
| 5 | `phong_sinh` | Phòng thí nghiệm sinh học | $x \in [0, 20]$, $z \in [0, 15]$ | 2 |
| 6 | `vi_sinh` | Phòng vi sinh | $x \in [20, 27]$, $z \in [7.5, 15]$ | 2 |
| 7 | `kho_sinh` | Kho sinh phẩm | $x \in [20, 27]$, $z \in [0, 7.5]$ | 2 |
| 8 | `hanh_lang_t2` | Hành lang tầng 2 | $x \in [0, 27]$, $z \in [-2.2, 0]$ | 2 |
| 9 | `sinh_phan_tu` | Sinh học phân tử | $x \in [0, 13.5]$, $z \in [-9, -2.2]$ | 2 |
| 10 | `nha_kinh` | Nhà kính thực vật | $x \in [13.5, 27]$, $z \in [-9, -2.2]$ | 2 |
| 11 | `hanh_lang_dong`| Hành lang đông tầng 2 | $x \in [27, 30]$, $z \in [6, 15]$ | 2 |
| 12 | `loi_thang` | Lõi thang bộ & thang máy | $x \in [27, 30]$, $z \in [-2.2, 6]$ | Cả hai (-1) |

---

## 2. Trang thiết bị và tính năng giáo dục

### 2.1. Phòng thí nghiệm sinh học (300 m²)
- **Bàn học sinh `BioBench`:** 9 bàn thí nghiệm (kích thước chuẩn $2,6\text{ m} \times 0,9\text{ m} \times 0,8\text{ m}$, bo cạnh $R = 6\text{ mm}$), bố trí 3 hàng $\times$ 3 cột.
- **Kính hiển vi quang học hai mắt (27 máy trên bàn học sinh + 2 máy phụ):**
  - Mô phỏng chuẩn Olympus CX23.
  - Mâm xoay 4 vật kính có vòng màu tiêu chuẩn: $4\times$ (đỏ, NA 0,10), $10\times$ (vàng, NA 0,25), $40\times$ (xanh lam, NA 0,65), $100\times$ dầu (trắng, NA 1,25).
  - Tụ quang Abbe NA 1,25, màn chắn sáng iris, đèn LED chiếu sáng xuyên tâm.
  - Hệ thống lấy nét đồng trục: ốc sơ cấp và ốc vi cấp.
- **Hộp tiêu bản cố định nhuộm H&E:** 6 tiêu bản mô học giáo khoa THPT (biểu bì vảy hành, khí khổng lá thài lài tía, phết máu người, vi khuẩn sữa chua Gram dương, nấm men nảy chồi, tế bào cơ vân).
- **Tủ an toàn sinh học cấp II loại A2 (2 tủ sát tường bắc):** dòng khí thổi tầng laminar $0,45\text{ m/s}$, tuần hoàn 70% khí qua lọc HEPA H14, xả 30% bảo vệ người làm việc.
- **Khu giải phẫu cơ thể người (Digital Anatomy Area):**
  - Bàn giải phẫu số cảm ứng nằm ngang $2,1\text{ m} \times 0,65\text{ m}$.
  - Mô hình BodyParts3D phân 4 lớp tương tác: Da, Cơ, Nội tạng, Xương.
  - Tương tác tách lớp, hoạt cảnh tim đập (72 bpm), phổi hô hấp (16 bpm).
  - Mô hình bộ xương người tỷ lệ 1:1 ($1,7\text{ m}$) trên giá inox có bánh xe.

### 2.2. Phòng vi sinh (Vùng vô trùng)
- **Buồng đệm cách ly (Airlock):** cửa liên động, biển cảnh báo vô trùng.
- **Hệ thống khử khuẩn:** đèn tia cực tím UV germicidal $254\text{ nm}$ gắn trần tự động ngắt khi mở cửa.
- **Đảo nhuộm Gram trung tâm:** bộ 4 hóa chất nhuộm chuẩn (Crystal violet, Dung dịch Lugol/Iodine, Cồn $95^\circ$ tẩy màu, Phẩm nhuộm đối kháng Safranin).
- **Pass-box hai chiều:** hộp chuyển mẫu vô trùng thông trực tiếp sang phòng thí nghiệm chính.

### 2.3. Kho sinh phẩm
- Tủ lạnh sâu $-20^\circ\text{C}$ và tủ mát $4^\circ\text{C}$ hai cánh kính có màn LED giám sát nhiệt độ liên tục.
- Giá kệ inox chứa mẫu ngâm tiêu bản fooc-môn và mô hình chuỗi xoắn kép DNA (14 cặp base Watson-Crick).

### 2.4. Phòng sinh học phân tử (Luồng một chiều)
- Thiết kế luồng một chiều chống tạp nhiễm: **Tiền PCR** (pha master mix, bộ pipette tự động) $\to$ **Tách chiết DNA** (máy ly tâm microcentrifuge $14.000\text{ rpm}$) $\to$ **Hậu PCR** (máy luân nhiệt PCR 96 giếng, bể điện di agarose, buồng tối chụp huỳnh quang Gel Doc UV $302\text{ nm}$).

### 2.5. Nhà kính thực vật
- Mái kính dốc đôi đón ánh sáng tự nhiên với khung nhôm đổ bóng thực tế.
- Giàn thủy canh hồi lưu NFT 3 tầng tích hợp đèn LED sinh trưởng quang phổ đỏ/xanh ($660\text{ nm} / 450\text{ nm}$).
- Trạm thí nghiệm quang hợp thải $\text{O}_2$ từ rong đuôi chó (*Elodea*), trạm đo thoát hơi nước Potometer, hộp thí nghiệm tính hướng sáng.

### 2.6. Hành lang đông
- Lan can kính chịu lực dài $33\text{ m}$ mở rộng tầm nhìn, ghế băng gỗ sồi nghỉ chân, chậu cây xanh nội thất.
- Bảng thông tin khoa học: Hệ thống phân loại sinh giới Whittaker (5 giới) và Carl Woese (3 lãnh giới: Bacteria, Archaea, Eukarya).

---

## 3. Hệ thống Vật liệu và Hai Chủ đề Mỹ thuật

### 3.1. Chủ đề "Quốc tế" (Mặc định)
Đồng bộ ngôn ngữ thiết kế với Tầng 1:
- Bảng màu nhận diện từng khu (`zoneBioLab`, `zoneMicroBio`, `zoneBioStore`, `zoneMolecular`, `zoneGreenhouse`).
- Dải sọc màu trên tường, mảng màu quanh cửa tự động.
- Viền LED dạ quang mép bàn học sinh.

### 3.2. Chủ đề "Tối giản" (Japandi / Scandinavian)
Thiết kế theo triết lý "Less but better" (Dieter Rams) và kiến trúc lab Adimab/Kewaunee:
- **Tường:** Trắng ngà `#F4F2EE`.
- **Sàn:** Đá vinyl liền khối không ron xám nhạt `#D9D6D0`.
- **Mặt bàn:** Trắng mờ `#E9E8E4`.
- **Nội thất gỗ:** Gỗ sồi trắng tự nhiên `#CDB79A`.
- **Màu nhấn duy nhất:** Xanh lá xô thơm (Sage Green) `#7A9A82`.
- **Kim loại:** Nhôm anod hóa mờ và than chì `#2B2D2F`.

### 3.3. Bộ chuyển đổi chủ đề thời gian thực (`BioThemeSwitcher.cs`)
- Nhấn phím **F7** hoặc gọi qua menu/biến môi trường `KHKT_CHU_DE=toi_gian`.
- Chuyển đổi vật liệu mượt mà trong $0,6\text{ s}$ theo đường cong Material 3 *Emphasized Decelerate* (`(0.05, 0.7, 0.1, 1)`).
- Không làm bẩn asset gốc trong Unity Editor (sao chép runtime instance).

---

## 4. Mô phỏng Sinh học thuần C# (`Khkt.Simulation.Bio`)

Toàn bộ các thuật toán mô phỏng được viết bằng C# thuần, **không phụ thuộc UnityEngine**, cho phép kiểm thử tự động độc lập tốc độ cao bằng `dotnet`.

1. **`Optics`:** Khẩu độ số NA, giới hạn phân giải Abbe $d = 0,61\lambda/\text{NA}$, đường kính thị trường $\text{FOV} = \text{FN}/M_{\text{vật}}$, độ sâu trường ảnh DOF.
2. **`Osmosis`:** Phương trình thế nước $\Psi_w = \Psi_s + \Psi_p$, áp suất thẩm thấu van 't Hoff $\Psi_s = -iCRT$, động học co nguyên sinh và phản co nguyên sinh tế bào thực vật.
3. **`ColonyGrowth`:** Tăng trưởng vi sinh vật logistic $\frac{dN}{dt} = rN(1 - N/K)$, phân tích pha lag $\to$ log $\to$ bão hòa $10^9\text{ CFU/mL}$, độ hấp thụ $\text{OD}_{600}$.
4. **`Pcr`:** Động học nhân bản $N = N_0(1 + E)^n$, nhiệt độ nóng chảy của mồi $T_m$ (công thức Wallace), nhiệt độ gắn mồi $T_a = T_m - 5^\circ\text{C}$, bão hòa phản ứng $10^{12}$ bản sao.
5. **`Electrophoresis`:** Tốc độ di chuyển điện di gel agarose tỉ lệ nghịch với logarit số cặp bazơ: $v \sim 1/\log_{10}(\text{bp})$, chuẩn đoán kích thước phân tử theo DNA Ladder.
6. **`Photosynthesis`:** Cường độ sáng theo định luật bình phương nghịch khoảng cách $I = P/(4\pi d^2)$, tốc độ tạo bọt khí $\text{O}_2$ của rong đuôi chó theo điểm bù và điểm bão hòa ánh sáng.
7. **`Fermentation`:** Động học lên men rượu ($C_6H_{12}O_6 \to 2C_2H_5OH + 2CO_2$) và lên men lactic, áp suất và thể tích khí làm căng bóng bay theo phương trình khí lý tưởng $PV = nRT$.

---

## 5. Hiệu năng Đồ họa và Tối ưu hóa (Mục tiêu Iris Xe $\ge 60$ FPS)

| Hạng mục | Giải pháp kỹ thuật | Kết quả đạt được |
|---|---|---|
| **Occlusion Culling** | Nướng Umbra Occlusion Culling trên 13 scene cộng dồn | 45.862 ô thể tích (tiles), loại bỏ triệt để vật khuất |
| **Zone Streaming** | Tách 8 phòng tầng 2 thành các scene con trong `Assets/Scenes/Khu/` | Bộ nhớ đồ họa $< 400\text{ MB}$, tải/gỡ mượt mà |
| **Vật liệu & Texture** | Tạo procedural bằng mã nguồn, kích thước tối đa $512\text{ px}$ | Không tốn dung lượng ổ đĩa, load tức thì |
| **Lưới bo cạnh (Bevel)** | Thuật toán `LabKit.RoundBox` bo góc $R = 2..6\text{ mm}$ có bộ nhớ đệm | Bắt sáng chân thực, không dùng đa giác thừa |
| **Shader đung đưa gió** | `Assets/Lab/Shaders/LaCay.shader` tính toán vertex displacement trên GPU | Lá cây và giàn thủy canh sinh động, chi phí CPU = 0 |
| **Shader tiêu bản vi thể** | `Assets/Lab/Shaders/TieuBan.shader` chọn mip bias theo khoảng lệch tiêu cự | Lấy nét mờ/rõ chân thực không tốn post-processing |
| **Frame rate thực nghiệm** | Đo tại trạm 20 (cửa vào), trạm 26 (kho), trạm 30 (nhà kính nhiều lá) | **$\ge 60$ FPS ổn định trên đồ họa tích hợp Intel Iris Xe** |

---

## 6. Ghi công Bản quyền (Attribution)

- **BodyParts3D:** Dữ liệu giải phẫu số 3D (`bp3d.bin.gz`) được cung cấp bởi **Trung tâm Cơ sở Dữ liệu Khoa học Đời sống (DBCLS - Database Center for Life Science), Nhật Bản**, phát hành dưới giấy phép quốc tế **Creative Commons Attribution 4.0 International (CC BY 4.0)**.
- **Tiêu bản vi mô và kiến thức cơ quan:** Kế thừa và chuẩn hóa từ hệ thống dữ liệu số SGK Sinh học 10, 11, 12 và đồ án nghiên cứu KHKT2027.
