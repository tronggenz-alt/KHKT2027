# Dụng cụ và Hệ thống Khu Sinh Học Tầng 2 (VERA Lab)

Tài liệu đặc tả kỹ thuật cho hệ thống dụng cụ, dữ liệu, mô phỏng sinh học, máy chủ quản lý và giao diện Tầng 2 – Khu Sinh Học của dự án VERA Lab (KHKT2027).

---

## Backend

Phần này do nhánh `tang2/dung-cu-backend` thực hiện, tuân thủ nghiêm ngặt `phong_sinh/HOP_DONG_GIAO_DIEN_BACKEND.md` và quy định an toàn sinh học cấp 1 (`BSL-1`).

### 1. Kiến trúc dữ liệu (`data/sinh/` và `data/may/`)

Toàn bộ danh mục tĩnh được lưu dưới dạng JSON UTF-8 có `"phien_ban": 1`. Mọi thông số kỹ thuật đều dẫn nguồn URL `https://` chính thống (tiêu chuẩn ISO, giáo trình ASM, ATCC, nhà sản xuất Olympus, Eppendorf, Bio-Rad, Thermo Fisher, Esco) hoặc đánh dấu rõ `"mo_phong": true`.

| Tập tin | Số bản ghi | Nội dung chính |
|---|:---:|---|
| `data/sinh/dung_cu.json` | **108** | Đầy đủ 108 mã dụng cụ chia theo 6 phân khu (`phong_sinh`, `phong_vi_sinh`, `kho_sinh_pham`, `phong_sinh_phan_tu`, `nha_kinh`, `hanh_lang_t2`). Mỗi bản ghi gồm `cong_dung`, `thong_so`, `cach_dung`, `loi_thuong_gap`, `an_toan`, `hieu_chuan_ngay`, `thanh_phan_unity`, `may_man_hinh`, `muc_dac_ta` và `stt_thiet_bi_tang_1` (nếu dùng chung với tầng 1). |
| `data/sinh/tieu_ban.json` | **20** | 12 tiêu bản tự làm (biểu bì hành, thài lài tía bình thường / co nguyên sinh / phản co nguyên sinh, khí khổng đóng / mở, niêm mạc miệng, nấm men, vi khuẩn sữa chua, *E. coli* K-12, *B. subtilis*) và 8 tiêu bản cố định (6 cảnh mô học kế thừa app cũ: phế nang, nephron, synapse, sarcomere, tế bào viền dạ dày, tiểu thùy gan + phết máu người, rễ hành nguyên phân) kèm tọa độ chú thích `bo_phan_chu_thich` $(x, y) \in [0, 1]^2$. |
| `data/sinh/chung_vi_sinh.json` | **7** | 5 chủng dạy học chuẩn `BSL-1` (*Escherichia coli* K-12, *Saccharomyces cerevisiae*, *Lactobacillus delbrueckii* subsp. *bulgaricus*, *Streptococcus thermophilus*, *Bacillus subtilis* 168) + 2 chủng tạp nhiễm môi trường `BSL-1` (`tap_nam_moc`, `tap_bacillus_khong_khi`) và bảng hệ số `he_so_nhiem`. |
| `data/sinh/moi_truong.json` | **6** | PCA (`thach_pca`), Thạch dinh dưỡng (`thach_dinh_duong`), LB Agar (`thach_lb`), Sabouraud Dextrose Agar (`thach_sabouraud`), Canh thang LB (`canh_lb`), Nước muối sinh lý 0,85% (`nuoc_muoi_sinh_ly`). |
| `data/sinh/sinh_pham_mac_dinh.json` | **35** | Vật tư tiêu hao khởi tạo kho (`sp_*`): mẫu thực vật/động vật, thuốc thử nhuộm Gram & hiển vi (khớp `ma_chat` trong `ChemEngine`), môi trường, giống vi sinh, kit PCR, agarose, đệm TAE. Có sẵn mẫu cận hạn (`sp_la_thai_lai`: còn 3 ngày) và mẫu quá hạn (`sp_kit_ly_giai_het_han`: -5 ngày) để kiểm thử cảnh báo. |
| `data/sinh/bai_thi_nghiem.json` | **15** | 15 bài thí nghiệm bám sát chương trình GDPT 2018 (Sinh học 10, 11, 12): `te_bao_hanh`, `khi_khong`, `co_nguyen_sinh`, `catalase`, `len_men_ruou`, `len_men_lactic`, `quang_hop`, `thoat_hoi_nuoc`, `huong_sang`, `tach_chiet_adn`, `dem_vi_sinh`, `cay_ria`, `nhuom_gram`, `pcr_dien_di`, `duong_cong_od600`. Mỗi bước có `dieu_kien` máy đọc được. |
| `data/may/gel_doc.json`, `may_do_od600.json` + 6 máy cập nhật | **8** | Hồ sơ SOP máy phân tích sinh học (`kinh_hien_vi`, `tu_an_toan_sinh_hoc`, `tu_am_vi_sinh`, `noi_hap`, `may_pcr`, `be_dien_di`, `gel_doc`, `may_do_od600`), đồng bộ sang `Phong thi nghiem ao/Assets/Lab/Data/May/` qua `tools/chep_du_lieu_may.py`. |

---

### 2. Mở rộng `ChemEngine` và Mô phỏng Sinh học (`Khkt.Simulation.Bio`)

#### 2.1. Chất hóa sinh và phản ứng xúc tác trong `Khkt.Simulation.Chem`
- **Các chất bổ sung vào `Species.cs`:**
  - `xanh_methylen` ($\text{C}_{16}\text{H}_{18}\text{ClN}_3\text{S}$, xanh lam đậm, dùng nhuộm nhân tế bào và thử sống/chết nấm men).
  - `tim_ket_tinh` (Crystal violet $\text{C}_{25}\text{H}_{30}\text{ClN}_3$, tím đậm, bước 1 nhuộm Gram).
  - `safranin` (Safranin O $\text{C}_{20}\text{H}_{19}\text{ClN}_4$, đỏ hồng, nhuộm đối kháng bước 4 nhuộm Gram).
  - `catalase` (enzyme hoạt động từ dịch chiết khoai tây / gan tươi) và `catalase_bat_hoat` (enzyme đã đun sôi biến tính).
  - Bí danh tương thích dữ liệu sinh học: `axit_lactic` $\to$ `Lac`, `con_70` / `con_96` / `etanol` $\to$ `C2H5OH`, `mb` $\to$ `xanh_methylen`.
- **Phản ứng phân hủy $\text{H}_2\text{O}_2$ xúc tác bởi Catalase (`ChemKinetics.cs` & `data/reactions/phan_ung.json`):**
  $$2\,\text{H}_2\text{O}_2(dd) \xrightarrow{\text{catalase}} 2\,\text{H}_2\text{O}(l) + \text{O}_2\uparrow$$
  Tốc độ phản ứng tỷ lệ thuận với hệ số hoạt tính nhiệt độ `CatalaseActivity(tempC, boiled)`: đạt cực đại tại $37\,^\circ\text{C}$, giảm còn $\approx 0,22$ ở $4\,^\circ\text{C}$, và bằng $0$ khi mẫu bị đun sôi hoặc $T \ge 65\,^\circ\text{C}$ (biến tính thuận nghịch không hoàn toàn / mất cấu trúc bậc 3).

#### 2.2. Các lớp mô phỏng C# thuần (`Phong thi nghiem ao/Assets/Scripts/Simulation/Bio/`)

Tất cả các lớp đều nằm trong namespace `Khkt.Simulation.Bio`, không phụ thuộc `UnityEngine`, không cấp phát bộ nhớ động trong vòng lặp khung hình (nhận `List<T>` đầu ra từ bên gọi) và tất định 100% theo `seed` (bộ sinh giả ngẫu nhiên Xorshift32).

1. **`BioSampleData.cs`**:
   - Quản lý trạng thái mẫu sinh học: `organismId`, `cfuPerMl`, `specimenId`, `dna` (`List<DnaBand>`), `sterile`, `contaminants` (`List<Contaminant>`).
   - `MixInto(dst, dstMl, src, srcMl)`: bảo toàn tổng số tế bào $N = C_1 V_1 + C_2 V_2$ chia cho tổng thể tích $V_1 + V_2$; nếu trộn 2 chủng khác nhau thì chủng có tổng CFU lớn hơn giữ `organismId` chính, chủng ít hơn chuyển sang `contaminants`; các băng ADN cùng kích thước `bp` được cộng dồn khối lượng `ng`; cờ `sterile = dst.sterile && src.sterile`.
2. **`Dilution.cs`**:
   - `CfuPerMl(colonies, platedMl, dilution)` $= \dfrac{\text{colonies}}{\text{platedMl} \times \text{dilution}}$.
   - `IsCountable(colonies)`: trả `true` khi $30 \le \text{colonies} \le 300$ (theo ISO 4833-1).
   - `CarryOverMl(tipNominalUl)`: thể tích bám dính mặt trong đầu côn khi không thay đầu côn giữa các bậc pha loãng ($\approx 0,25\%$ thể tích danh định, quy đổi ra mL).
   - `SampleColonies(expected, seed)`: lấy mẫu phân phối Poisson bằng thuật toán Knuth kết hợp xấp xỉ chuẩn khi $\lambda > 60$.
3. **`StreakPlate.cs`**:
   - `Colonies(strokes, inoculumCfu, organismId, hours, tempC, seed, output)`: mô phỏng cấy ria 4 vùng tách khuẩn lạc đơn.
   - Nếu `loopSterilizedBefore == true` khi chuyển sang vùng mới, mật độ tế bào mang theo giảm theo hệ số pha loãng bậc vùng ($\sim 0,035$); nếu quên đốt que cấy (`loopSterilizedBefore == false`), mật độ vùng trước truyền sang vùng sau làm khuẩn lạc mọc dày chồng lấp, không tách được khuẩn lạc đơn ở vùng 3–4.
   - Đường kính khuẩn lạc phụ thuộc thời gian ủ `hours` và nhiệt độ `tempC` (không mọc nếu $T < 8\,^\circ\text{C}$ hoặc $T > 50\,^\circ\text{C}$ hoặc cấy khi que còn nóng đỏ `inoculumCfu <= 0`).
4. **`ContaminationRules.cs`**:
   - `Probability(in AsepticContext c)` và `Roll(in AsepticContext c, int seed, out Contaminant result)`: tính xác suất rơi tạp nhiễm từ không khí và thao tác theo phương trình nhân các hệ số nguy cơ.
5. **`GramStain.cs`**:
   - `Result(gramPositive, crystalVioletS, iodineS, decolorizeS, safraninS, heatFixed, smearThickness01)`:
     - Chưa cố định nhiệt (`!heatFixed`) hoặc bỏ bước nhuộm đầu $\to$ `GramResult.KhongCoTeBao`.
     - Vết bôi quá dày (`smearThickness01 > 0.78`) $\to$ `GramResult.LoangLo`.
     - Tẩy cồn quá lâu (`decolorizeS > 30`) hoặc quên Lugol (`iodineS < 15`) làm phức hợp CV–I của vi khuẩn Gram dương bị rửa trôi $\to$ nhuộm bắt màu hồng giả (`GramResult.Hong`).
     - Tẩy cồn quá ngắn (`decolorizeS < 5`) làm vi khuẩn Gram âm giữ lại màu tím $\to$ dương tính giả (`GramResult.Tim`).
6. **`PipetteAccuracy.cs`**:
   - `ActualUl(setUl, nominalMaxUl, stageWhenAspirating, tiltDeg, tipSubmerged)`:
     - Không nhúng ngập đầu côn (`!tipSubmerged`) $\to$ hút khí, chỉ lấy được $25\%$ thể tích đặt.
     - Bấm xuống nấc 2 trước khi hút (`stageWhenAspirating >= 2`) $\to$ hút dư $+12\%$ thể tích đặt.
     - Nghiêng pipet góc $\theta > 20^\circ$ $\to$ tăng thể tích hút do áp suất thủy tĩnh $+0,12\%$ mỗi độ vượt $20^\circ$.
     - Sai số hệ thống trong điều kiện chuẩn tuân theo giới hạn ISO 8655-2 ($\le 0,8\%$).
   - `ResidualFraction(stageWhenDispensing)`: chỉ nhả tới nấc 1 còn đọng lại $3,5\%$ ($0,035$); nhả hết nấc 2 (blow-out) còn $0\%$.
7. **`Centrifugation.cs`**:
   - $\text{RCF} = 1,118 \times 10^{-5} \times r_{\text{cm}} \times \text{rpm}^2$ và hàm ngược `Rpm(radiusCm, rcf)`.
   - `IsBalanced(load, slotCount, out reasonVi)`: kiểm tra tổng mô-men khối lượng вектор $\sum m_i \cos\theta_i \approx 0, \sum m_i \sin\theta_i \approx 0$ (sai số cho phép $\le 0,1\text{ g}$). Nếu lệch, chỉ rõ bằng tiếng Việt lỗ nào thiếu ống đối diện hoặc lệch bao nhiêu gam.
8. **`Pcr.cs` (mở rộng)**:
   - `Pcr.Run(in PcrSetup s, int seed, List<DnaBand> output)`: kiểm tra đủ 5 thành phần (`hasTemplate`, `hasPrimers`, `hasTaq`, `hasDntp`, `hasBuffer`); nếu thiếu trả danh sách rỗng.
   - Khi $T_a < T_m - 10\,^\circ\text{C}$: mồi bắt cặp không đặc hiệu, sinh thêm băng phụ (non-specific bands) và giảm hiệu suất băng chính.
   - Khi $T_a > T_m + 3\,^\circ\text{C}$: mồi khó bắt cặp, băng mờ dần; khi $T_a > T_m + 8\,^\circ\text{C}$: mất băng hoàn toàn.
   - Không bật nắp nhiệt (`!lidHeated`) hoặc để enzyme ở nhiệt độ phòng lâu (`enzymeWarmMinutes`) làm giảm hiệu suất khuếch đại.
   - Nếu `productContamination == true` trên mẫu đối chứng âm (`!hasTemplate`), xuất hiện băng dương tính giả.
9. **`Electrophoresis.cs` (mở rộng)**:
   - `BandPositionCm(bp, agarosePct, volts, minutes, reversedPolarity)`: quãng đường di chuyển tỷ lệ thuận với điện áp, thời gian, nghịch biến với $\log_{10}(\text{bp})$ và nồng độ gel agarose; nếu cắm ngược cực (`reversedPolarity == true`) trả giá trị âm (ADN chạy ngược về phía giếng / ra khỏi đầu gel).
   - `LeftGel(bp, agarosePct, volts, minutes, gelLengthCm)`: trả `true` khi băng ADN chạy vượt chiều dài bản gel.
10. **`Transpiration.cs`, `Phototropism.cs`, `DnaExtraction.cs`, `Catalase.cs`, `Optics.cs`, `Fermentation.cs`**:
    - `Transpiration.BubbleShiftMmPerMin(wind01, light01, humidity01, stomataBlocked)`: tốc độ dịch chuyển bọt khí potometer tăng theo ánh sáng và gió, giảm khi độ ẩm cao, giảm còn $5\%$ (thoát qua tầng cutin) khi bôi vaseline bịt khí khổng.
    - `Phototropism.BendAngleDeg(hours, lightAzimuthDeg, fullyDark)` và `EtiolationFactor(hours, fullyDark)`: uốn cong hướng sáng theo hàm bão hòa thời gian; cây để trong tối hoàn toàn không uốn hướng sáng nhưng vươn dài mạnh (`EtiolationFactor > 1`).
    - `DnaExtraction.YieldUg(sampleGrams, sampleKind, salt, detergent, incubateC, ethanolC, ethanolToLysateRatio)`: hiệu suất thu hồi ADN thô phụ thuộc chất tẩy rửa phá màng, muối $\text{NaCl}$ trung hòa điện tích photphat, nhiệt độ ủ tối ưu $\sim 55\,^\circ\text{C}$ và cồn lạnh $(-20\,^\circ\text{C})$.
    - `Optics.Brightness(lamp01, iris01, condenser01, oilNeededButMissing)`: độ sáng trường nhìn kính hiển vi; khi dùng vật kính $100\times$ mà thiếu dầu soi (`oilNeededButMissing`), độ sáng sụt giảm $65\%$ do phản xạ toàn phần tại khe không khí.
    - `Fermentation.LacticAcidGramsAt(hours, tempC, milkMl)` và `Co2MolesAt(hours, tempC, sugarG, yeastAlive)`: động học tạo axit lactic (tối ưu $42\,^\circ\text{C}$) và sinh khí $\text{CO}_2$ lên men rượu (tối ưu $32\,^\circ\text{C}$, bằng $0$ khi nấm men chết).

---

### 3. Bảng hệ số mô phỏng và nguồn tham khảo

#### 3.1. Bảng hệ số nguy cơ tạp nhiễm vi sinh (`ContaminationRules` & `chung_vi_sinh.json -> he_so_nhiem`)

Công thức xác suất nhiễm bẩn mẫu khi mở nắp dụng cụ trong thời gian $t = \text{openSeconds}$ (giây):
$$r = r_0 \times \prod_i k_i, \qquad P(\text{nhiễm}) = \text{clamp}\!\left(1 - e^{-r \cdot t},\; 0,\; 0,995\right)$$

| Tham số / Điều kiện | Mã trong `he_so_nhiem` | Giá trị | Ý nghĩa vật lý – sinh học | Nguồn / Chế độ |
|---|---|:---:|---|---|
| Tốc độ nhiễm cơ bản ngoài không khí | `toc_do_co_ban_moi_giay` | $0,0025\text{ s}^{-1}$ | Mật độ bào tử nấm mốc / vi khuẩn lơ lửng trong phòng thường | `"mo_phong": true` |
| Trong BSC chạy chuẩn ($\ge 5$ phút, kính $\le 22\text{ cm}$, không chắn lưới) | `he_so_bsc_chuan` | $\times 0,04$ | Màng lọc HEPA H14 và màn khí giữ vô trùng vùng thao tác | `"mo_phong": true` |
| Trong BSC nhưng chưa chạy quạt đủ $5$ phút | `he_so_bsc_chua_du_thoi_gian` | $\times 0,35$ | Chưa đuổi hết hạt bụi tồn lưu trong buồng thao tác | `"mo_phong": true` |
| Trong BSC nhưng mở kính cao $> 22\text{ cm}$ | `he_so_bsc_kinh_cao` | $\times 2,50$ | Phá vỡ vận tốc màn khí bảo vệ trước mặt ($0,45\text{ m/s}$) | `"mo_phong": true` |
| Bị chắn lưới hút gió trước / sau trong BSC | `he_so_bsc_chan_luoi` | $\times 3,00$ | Dòng khí xoáy cuốn không khí ngoài phòng vào vùng sạch | `"mo_phong": true` |
| Thao tác trong vùng nón bảo vệ của ngọn lửa đèn cồn | `he_so_gan_lua_den_con` | $\times 0,25$ | Dòng đối lưu nhiệt đẩy hạt bụi hướng lên trên | `"mo_phong": true` |
| Mặt bàn chưa lau cồn $70\%$ | `he_so_ban_ban` | $\times 2,20$ | Bào tử và vi khuẩn bám trên bề mặt bàn phát tán | `"mo_phong": true` |
| Chưa rửa tay trước khi thao tác | `he_so_chua_rua_tay` | $\times 1,80$ | Hệ vi sinh vật thường trú trên da tay rơi vào mẫu | `"mo_phong": true` |
| Không mặc đồ bảo hộ (áo blouse, găng, khẩu trang) | `he_so_khong_bao_ho` | $\times 1,50$ | Hạt giọt bắn và bụi vải từ quần áo thường | `"mo_phong": true` |
| Dụng cụ tiếp xúc trực tiếp chưa tiệt trùng | `he_so_dung_cu_khong_vo_trung` | $+0,65$ (cộng đáy) | Nhiễm trực tiếp từ đầu côn / que cấy bẩn | `"mo_phong": true` |

#### 3.2. Bảng tổng hợp các hằng số mô phỏng khác

| Mô-đun | Thông số / Công thức | Giá trị | Nguồn tham khảo |
|---|---|---|---|
| `Dilution` | Ngưỡng đĩa đếm chuẩn | $30 \text{ – } 300\text{ CFU/đĩa}$ | ISO 4833-1:2013 (`https://www.iso.org/standard/53728.html`) |
| `Dilution` | Hệ số bám dính đầu côn (`CarryOverMl`) | $0,25\%$ thể tích danh định | `"mo_phong": true` |
| `GramStain` | Thời gian chuẩn: Tím kết tinh $60\text{ s}$, Lugol $60\text{ s}$, Tẩy cồn $10\text{–}20\text{ s}$, Safranin $45\text{–}60\text{ s}$ | Ngưỡng tẩy cồn $<5\text{ s}$ (dương giả), $>30\text{ s}$ (âm giả) | ASM Gram Stain Protocol (`https://asm.org/Protocols/Gram-Stain-Protocol`) |
| `PipetteAccuracy` | Sai số hệ thống cho phép P10 / P200 / P1000 | $\pm 0,8\%$ đến $\pm 1,5\%$ | ISO 8655-2:2022 (`https://www.iso.org/standard/77394.html`) |
| `PipetteAccuracy` | Lỗi hút ở nấc 2; lượng sót khi nhả ở nấc 1 | $+12\%$ khi hút nấc 2; sót $3,5\%$ ở nấc 1 | `"mo_phong": true` |
| `Centrifugation` | Công thức chuyển đổi RPM $\leftrightarrow$ RCF | $\text{RCF} = 1,118 \times 10^{-5} \cdot r_{\text{cm}} \cdot \text{rpm}^2$ | Eppendorf Centrifuge Guide (`https://www.eppendorf.com/`) |
| `Pcr` | Nhiệt độ gắn mồi tối ưu $T_a$ so với $T_m$ | $T_a \approx T_m - 5\,^\circ\text{C}$; băng phụ khi $T_a < T_m - 10\,^\circ\text{C}$ | Thermo Fisher PCR Guide (`https://www.thermofisher.com/`) |
| `Electrophoresis` | Tốc độ điện di ADN trên gel agarose $0,5\text{–}2,0\%$ | Phụ thuộc $\log_{10}(\text{bp})$, $V$, $t$, $\% \text{agarose}$ | Bio-Rad Electrophoresis Guide (`https://www.bio-rad.com/`) |
| `Catalase` | Nhiệt độ tối ưu và nhiệt độ biến tính mất hoạt tính | $T_{\text{opt}} = 37\,^\circ\text{C}$, mất hoạt tính ở $T \ge 65\,^\circ\text{C}$ | `"mo_phong": true` (theo SGK Sinh học 10) |
| `Fermentation` | Nhiệt độ tối ưu lên men lactic và lên men rượu | Lactic: $42\,^\circ\text{C}$; Rượu (*S. cerevisiae*): $32\,^\circ\text{C}$ | ATCC / DSMZ & `"mo_phong": true` |

---

### 4. Cơ sở dữ liệu SQLite (`server/lab/sinh_db.py`)

Sử dụng chung tệp CSDL `data/lab.db` (hoặc biến môi trường `KHKT_DB` khi chạy kiểm thử) với khóa luồng `threading.Lock()`, không sửa đổi bất kỳ bảng nào của Tầng 1.

| Tên bảng | Khóa chính | Các cột dữ liệu | Chức năng |
|---|---|---|---|
| `sinh_dung_cu` | `ma` (`TEXT`) | `so_luong`, `hong`, `dang_muon`, `can_bao_tri`, `vi_tri`, `phong`, `lan_hieu_chuan`, `han_hieu_chuan`, `ghi_chu`, `cap_nhat` | Lưu trạng thái thực tế của 108 dụng cụ; tự động ghép với thông tin tĩnh trong `dung_cu.json`. |
| `sinh_pham` | `id` (`TEXT`) | `ten`, `loai`, `tu`, `ngan`, `so_luong`, `don_vi`, `so_lo`, `han_dung`, `cap_an_toan`, `ma_chung`, `ma_chat`, `ghi_chu`, `cap_nhat` | Quản lý tồn kho và hạn dùng sinh phẩm; cưỡng chế `cap_an_toan == "BSL-1"`. |
| `nhiet_do_tu` | `id` (`INTEGER`) | `tu`, `thoi_gian`, `nhiet_do`, `cua_mo` | Nhật ký chuỗi thời gian nhiệt độ tủ mát $4\,^\circ\text{C}$, tủ đông $-20\,^\circ\text{C}$, tủ ấm; tự dọn bản ghi $> 30$ ngày. |
| `dat_lich` | `id` (`INTEGER`) | `doi_tuong`, `nguoi`, `lop`, `bat_dau`, `ket_thuc`, `muc_dich`, `trang_thai` | Đặt lịch sử dụng phòng / máy; kiểm tra giao khoảng thời gian $(A_{\text{start}} < B_{\text{end}}) \land (A_{\text{end}} > B_{\text{start}})$. |
| `muon_tra` | `id` (`INTEGER`) | `ma_dung_cu`, `so_luong`, `nguoi`, `lop`, `luc_muon`, `han_tra`, `luc_tra`, `tinh_trang_tra` | Quản lý phiếu mượn – trả; tự tăng/giảm `dang_muon` và cộng `hong` nếu trả báo hỏng. |
| `ket_qua_tn` | `id` (`INTEGER`) | `thoi_gian`, `hoc_sinh`, `lop`, `ban`, `ma_bai`, `du_lieu` (JSON), `nhan_xet`, `diem` | Lưu kết quả 15 bài thí nghiệm của học sinh và điểm/nhận xét của giáo viên. |
| `su_co_sinh` | `id` (`INTEGER`) | `thoi_gian`, `phong`, `loai`, `muc_do`, `mo_ta`, `xu_ly`, `trang_thai`, `nguon` | Báo cáo sự cố an toàn sinh học (vỡ dụng cụ, tràn đổ vi sinh, lệch nhiệt độ tủ). |
| `bao_tri` | `id` (`INTEGER`) | `ma_dung_cu`, `loai`, `ngay`, `nguoi`, `ket_qua`, `ghi_chu` | Hồ sơ hiệu chuẩn và bảo trì định kỳ thiết bị sinh học. |

---

### 5. Danh sách API Máy chủ (`/lab/sinh/*` và `/sinh` trong `server/lab/sinh_api.py`)

Tất cả các endpoint trả JSON UTF-8; danh sách có dạng `{"muc": [...], "tong": n}`; lỗi trả `{"detail": "<thông báo tiếng Việt>"}`.

| Phương thức & Đường dẫn | Tham số / Body gửi | Phản hồi & Quy tắc kiểm tra |
|---|---|---|
| `GET /sinh` | — | Phục vụ `server/web/sinh.html`; nếu file chưa có thì trả trang HTML thông báo tạm thời (`200 OK`, không lỗi `500`). |
| `GET /lab/sinh/tong-quan` | — | Tổng hợp số lượng dụng cụ (`tong`, `hong`, `dang_muon`, `can_bao_tri`, `qua_han_hieu_chuan`), cảnh báo sinh phẩm (`sap_het`, `sap_het_han`, `qua_han`), nhiệt độ các tủ lạnh/tủ ấm, sự cố mới và lịch hôm nay. |
| `GET /lab/sinh/dung-cu` | `?phong=&phan_loai=&tim=` | Danh sách 108 dụng cụ ghép thông tin tĩnh + trạng thái CSDL, lọc theo phòng, phân loại, từ khóa không dấu. |
| `PATCH /lab/sinh/dung-cu/{ma}` | `so_luong`, `hong`, `dang_muon`, `can_bao_tri`, `vi_tri`, `lan_hieu_chuan`, `han_hieu_chuan`, `ghi_chu` | Cập nhật trạng thái dụng cụ; trả `404` nếu mã không tồn tại, `422` nếu số âm. |
| `GET /lab/sinh/sinh-pham` | `?tu=&loai=&tim=` | Danh sách sinh phẩm trong kho kèm trạng thái hạn dùng. |
| `POST /lab/sinh/sinh-pham` | `ten`, `loai`, `tu`, `ngan`, `so_luong`, `don_vi`, `so_lo`, `han_dung`, `cap_an_toan` | Tạo mới sinh phẩm. Nếu `cap_an_toan != "BSL-1"` $\to$ chặn với mã `422` ("Phòng thí nghiệm trường học chỉ dùng vi sinh vật an toàn cấp 1 (BSL-1)"). |
| `PATCH /lab/sinh/sinh-pham/{id}` | Các trường cập nhật sinh phẩm | Cập nhật sinh phẩm; chặn `422` nếu đổi `cap_an_toan` khác `BSL-1`. |
| `DELETE /lab/sinh/sinh-pham/{id}` | — | Xóa mặt hàng sinh phẩm khỏi kho (`404` nếu không có). |
| `POST /lab/sinh/sinh-pham/{id}/xuat` | `{"so_luong", "nguoi", "ghi_chu"}` | Xuất kho tiêu hao, ghi vào bảng `nhat_ky` chung (`nguon="khu_sinh"`). Vượt số lượng tồn $\to$ `422`. |
| `POST /lab/sinh/sinh-pham/{id}/nhap` | `{"so_luong", "nguoi", "ghi_chu"}` | Nhập thêm sinh phẩm vào kho, ghi vào `nhat_ky` chung. |
| `GET /lab/sinh/nhiet-do` | `?tu=&tu_luc=` | Chuỗi lịch sử nhiệt độ của tủ bảo quản (`tu_mat_4`, `tu_dong_am20`, `tu_am`). |
| `POST /lab/sinh/nhiet-do` | `{"tu", "nhiet_do", "cua_mo"}` | Ghi điểm đo nhiệt độ mới. |
| `GET /lab/sinh/dat-lich` | `?doi_tuong=&lop=` | Danh sách lịch đăng ký sử dụng phòng / thiết bị. |
| `POST /lab/sinh/dat-lich` | `{"doi_tuong", "nguoi", "lop", "bat_dau", "ket_thuc", "muc_dich"}` | Đặt lịch mới; `ket_thuc <= bat_dau` $\to$ `422`; trùng khoảng thời gian cùng `doi_tuong` $\to$ `409 Conflict` kèm `{"detail": "...", "trung": [...]}`. |
| `PATCH /lab/sinh/dat-lich/{id}` | Các trường sửa lịch hoặc `trang_thai` | Cập nhật lịch (kiểm tra trùng lịch như khi tạo mới). |
| `DELETE /lab/sinh/dat-lich/{id}` | — | Hủy lịch đã đặt. |
| `GET /lab/sinh/muon-tra` | `?dang_muon=` | Danh sách phiếu mượn – trả dụng cụ. |
| `POST /lab/sinh/muon-tra` | `{"ma_dung_cu", "so_luong", "nguoi", "lop", "han_tra"}` | Tạo phiếu mượn; mượn vượt số khả dụng (`so_luong - hong - dang_muon`) $\to$ `422`. |
| `PATCH /lab/sinh/muon-tra/{id}` | `{"luc_tra", "tinh_trang_tra": "tot" \| "hong"}` | Ghi nhận trả dụng cụ; nếu `"hong"` thì tự động cộng dồn vào cột `hong` của `sinh_dung_cu`. |
| `GET /lab/sinh/ket-qua` | `?lop=&ma_bai=&hoc_sinh=` | Danh sách kết quả bài thí nghiệm của học sinh. |
| `GET /lab/sinh/ket-qua/{id}` | — | Chi tiết một bản ghi kết quả thí nghiệm. |
| `POST /lab/sinh/ket-qua` | `{"hoc_sinh", "lop", "ban", "ma_bai", "du_lieu"}` | Lưu kết quả bài thí nghiệm từ Unity hoặc web. |
| `PATCH /lab/sinh/ket-qua/{id}` | `{"nhan_xet", "diem"}` | Giáo viên chấm điểm ($0\text{–}10$) và ghi nhận xét. |
| `GET /lab/sinh/ket-qua.csv` | `?lop=&ma_bai=` | Xuất bảng điểm và dữ liệu thí nghiệm ra CSV mã hóa UTF-8 có BOM (`\xef\xbb\xbf`) để mở trực tiếp bằng Microsoft Excel hiển thị đúng tiếng Việt. |
| `GET /lab/sinh/su-co`, `POST /lab/sinh/su-co`, `PATCH /lab/sinh/su-co/{id}` | `{"phong", "loai", "muc_do", "mo_ta", "xu_ly", "trang_thai"}` | Danh sách, báo cáo mới và cập nhật xử lý sự cố an toàn sinh học (đồng bộ sang `nhat_ky` chung). |
| `GET /lab/sinh/bao-tri`, `POST /lab/sinh/bao-tri` | `{"ma_dung_cu", "loai", "ngay", "nguoi", "ket_qua", "ghi_chu"}` | Tra cứu và ghi phiếu bảo trì / hiệu chuẩn dụng cụ, tự cập nhật `lan_hieu_chuan` và xóa cờ `can_bao_tri`. |
| `GET /lab/sinh/huong-dan/{ma}` | — | Trả toàn bộ hồ sơ hướng dẫn sử dụng, thông số, lỗi thường gặp và an toàn của dụng cụ `{ma}` từ `dung_cu.json`. |
| `GET /lab/sinh/bai-thi-nghiem` | — | Trả danh sách 15 bài thí nghiệm sinh học từ `bai_thi_nghiem.json`. |
| `GET /lab/sinh/trang-thai-may` | — | Trả trạng thái thời gian thực của các máy trong phòng sinh học (lưu trên bộ nhớ RAM có khóa luồng). |
| `POST /lab/sinh/su-kien` | `{"su_kien": [{"loai", "thoi_gian", "du_lieu"}]}` | Cổng nhận sự kiện theo lô từ Unity (tối đa 500 sự kiện/lần, vượt quá trả `413`). Hỗ trợ 8 loại: `dung_cu_hong`, `can_bao_tri`, `sinh_pham_xuat`, `sinh_pham_nhap`, `nhiet_do_tu`, `trang_thai_may`, `ket_qua_tn`, `su_co`. Xử lý độc lập từng phần tử, trả `{"nhan": n, "loi": [{"chi_so", "ly_do"}]}`. |

---

### 6. Trợ lý AI và Kiểm soát An toàn Sinh học BSL-1 (`server/ai/machines.py`)

- **Nhận diện máy và dụng cụ (`ALIASES`, `find`, `find_bio_tools`):**
  - Bổ sung từ khóa cho 2 máy phân tích mới (`gel_doc`, `may_do_od600`) và các nhóm dụng cụ sinh học tầng 2 (`micropipet_p10/p200/p1000`, `hop_dau_con`, `que_cay_vong`, `que_cay_thang`, `que_trang`, `dao_nhuom_gram`, `may_ly_tam_de_ban`, `may_ly_tam_mini`, `may_vortex`, `may_dem_khuan_lac`, `coi_chay_su`, `tram_potometer`, `tram_quang_hop`, `hop_huong_sang`, `gian_thuy_canh_nft`, `binh_len_men`, `bo_lam_sua_chua`, `ban_giai_phau_so`, `tu_thao_tac_pcr`, `bo_xu_ly_tran`, `lo_dau_soi`).
  - Khi học sinh hỏi `/tro-ly` hoặc `/robot/ke-hoach`, `machines.context(...)` tự động trích xuất `cong_dung`, `cach_dung`, `loi_thuong_gap`, `an_toan` từ `data/sinh/dung_cu.json` (tối đa 2 dụng cụ, $\le 1500$ ký tự/dụng cụ) cùng quy trình máy từ `data/may/*.json`.
- **Kiểm soát an toàn sinh học cấp 1 (`is_unsafe_bio`, `bio_safety_refusal`):**
  - Tự động phát hiện các từ khóa liên quan đến tác nhân gây bệnh nguy hiểm (`BSL-2`, `BSL-3`, `BSL-4`, *Vibrio cholerae*, *Bacillus anthracis*, *Mycobacterium tuberculosis*, *Yersinia pestis*, *Salmonella*, *Staphylococcus aureus*, MRSA, *Clostridium botulinum*, virus gây bệnh, phân lập từ bệnh phẩm máu/đờm/mủ, tăng độc lực, tạo chủng kháng kháng sinh).
  - Tự động chèn chỉ thị bắt buộc `BSL1_SAFETY_CONTEXT` để AI từ chối nhẹ nhàng và hướng dẫn học sinh chuyển sang các thí nghiệm an toàn tương đương với chủng `BSL-1` (*E. coli* K-12, *Saccharomyces cerevisiae*, *Lactobacillus bulgaricus*, *Bacillus subtilis*).

---

## Giao diện

*(Phần này dành cho nhánh `tang2/dung-cu-giao-dien` ghi bổ sung)*
