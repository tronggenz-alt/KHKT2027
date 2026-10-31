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

Phần này do nhánh `tang2/dung-cu-giao-dien` thực hiện, bám sát các yêu cầu trong `phong_sinh/PROMPT_DUNG_CU_GIAO_DIEN.md` và `phong_sinh/HOP_DONG_GIAO_DIEN_BACKEND.md`.

---

### 1. Khung tương tác sinh học cốt lõi (`Khkt.Interaction.Bio`)

Toàn bộ hệ thống tương tác dụng cụ và thiết bị tầng 2 được chuẩn hóa theo khung chung, đảm bảo phản hồi tức thì ($\le 1$ khung hình), đường cong chuyển động vật lý mượt mà, định dạng hiển thị tiếng Việt với số thập phân chuẩn `vi-VN` (dấu phẩy), giải phóng hoàn toàn rác bộ nhớ trong vòng lặp chính (0 B GC Alloc mỗi khung hình) và tuân thủ nguyên tắc "Giảm chuyển động":

- **`BioKnob`**: Điều khiển các núm vặn vật lý: núm vi cấp / đại cấp và chiết áp đèn kính hiển vi, núm điều chỉnh thể tích micropipet, núm xoay điện áp nguồn điện di, núm chỉnh tốc độ ly tâm. Hỗ trợ bước vặn liên tục hoặc từng nấc rời rạc, góc giới hạn min/max, con trỏ xoay mượt mà với âm thanh click vật lý theo tần số góc.
- **`BioButton`**: Nút bấm cơ học có hiệu ứng lún cơ học đàn hồi, độ phản hồi xúc giác (nút nguồn máy quang phổ, nút Tare/Blank, nút nhả đầu côn micropipet, nút chạy luân nhiệt PCR, nút mở nắp tủ vi sinh).
- **`BioHinge`**: Bản lề giảm chấn góc xoay mềm cho nắp kính tủ an toàn sinh học cấp II, cửa kính tủ mát 4 °C, nắp tủ đông, nắp máy ly tâm, nắp buồng soi gel doc và cửa buồng đệm airlock.
- **`IBioTarget`**: Giao diện tiêu chuẩn cho mọi vật mẫu và dụng cụ tiếp nhận dung dịch / sinh phẩm (`GlassSlide`, `PetriDish`, `EppendorfTube`, `ElectrophoresisWell`, `SpectroCuvette`, `PlantPot`, `PotometerTube`).
- **`BioTool`**: Lớp cơ sở cho 108 dụng cụ cầm tay sinh học. Hỗ trợ cơ chế phím `Q` tự động trả dụng cụ về đúng giá / khay ban đầu; tâm ngắm (crosshair) chuyển trạng thái động; cơ chế nhấn giữ kèm vòng tiến trình `ProgressRing` cho các thao tác đòi hỏi thời gian tiêu chuẩn (đốt que cấy 3 s, nghiền mẫu 5 s, tẩy cồn 15 s).
- **`BioFeedback`**: Hệ thống phản hồi xúc giác và thị giác: âm thanh thao tác, cảnh báo lỗi khoa học, thông báo toast tiếng Việt thanh thoát, ghi nhật ký sự kiện và lỗi sai khoa học sang `BioLabSync`.
- **`CloseUpView`**: Hệ thống camera cận cảnh thị kính hiển vi, giếng gel điện di, buồng chụp UV gel doc và màn hình thiết bị phân tích, làm mờ hậu cảnh và chuyển đổi góc nhìn mượt mà.

---

### 2. Mô phỏng 108 dụng cụ tương tác theo 6 phân khu

1. **Phòng thí nghiệm sinh học chung (x 0..20, z 0..15):**
   - 29 kính hiển vi quang học hai mắt (`MicroscopeController`, `MicroscopeCameraProgram`): 27 kính trên 9 bàn học sinh, 1 kính bàn giáo viên có camera nối màn hình phụ, 1 kính tại đảo nhuộm Gram. Mô phỏng đầy đủ vật kính 4x, 10x, 40x, 100x dầu soi, bàn sa trượt XY, ốc vi cấp/đại cấp, màn chắn sáng, tụ quang Abbe và độ sáng đèn LED.
   - Dụng cụ chuẩn bị tiêu bản (`SlidePrepTools`): Lam kính 75×25 mm, lamen 22×22 mm, kim mũi mác, panh mũi nhọn, dao lam, giấy thấm, giấy lau kính, lọ dầu soi cedar n = 1,515.
   - Thí nghiệm sinh lý thực vật & vi sinh: Bình lên men rượu kị khí với bóng bay co dãn theo thể tích khí $\text{CO}_2$; bộ làm sữa chua lên men lactic ủ 42 °C; ống nghiệm catalase phân hủy $\text{H}_2\text{O}_2$.
   - Bàn giải phẫu số (`DigitalAnatomyTable`): Mô hình giải phẫu 3D đa giác quan, chọn bóc tách từng lớp cơ quan (tuần hoàn, hô hấp, tiêu hóa, thần kinh) và phóng to chi tiết tiêu bản mô học.
   - Tủ an toàn sinh học cấp II phòng chung: Quạt hút dòng khí tầng, đèn UV tiệt trùng trần, nắp kính trượt an toàn.

2. **Phòng vi sinh (x 20..27, z 7,5..15) & buồng đệm:**
   - Tủ an toàn sinh học cấp II loại A2 và pass-box đưa mẫu hai chiều.
   - Đèn cồn tiệt trùng, que cấy vòng, que cấy thẳng (nung đỏ và làm nguội cạnh đèn cồn), que trang thủy tinh chữ L gạt thạch.
   - Đĩa petri thạch PCA/LB/Sabouraud, ống thạch nghiêng, ống canh thang vô trùng.
   - Tủ ấm vi sinh 37 °C có tuần hoàn khí, nồi hấp tiệt trùng áp suất đứng 121 °C.
   - Đảo nhuộm Gram 4 bước (`dao_nhuom_gram`): Cố định nhiệt vết bôi, tím kết tinh (1 phút), dung dịch Lugol (1 phút), cồn 96% (15 giây), safranin (1 phút), vòi nước rửa trôi.
   - Máy đếm khuẩn lạc bán tự động (`may_dem_khuan_lac`): Bút đếm điện tử, kính lúp chiếu sáng nền đen, đếm khuẩn lạc trong khoảng chuẩn 30–300 theo ISO 4833-1.

3. **Kho sinh phẩm (x 20..27, z 0..7,5):**
   - Tủ mát bảo quản 4 °C hai cánh kính cường lực (`GlassFridge`), cảm biến nhiệt độ thời gian thực.
   - Tủ đông sâu -20 °C bảo quản kit PCR, enzyme và ADN.
   - Kệ inox đa tầng chứa mẫu thực vật tươi, mô hình tiêu bản và hóa chất đệm.
   - Ki-ốt sổ kho điện tử màn hình cảm ứng (`StockKiosk`, `StockProgram`): Tra cứu tồn kho 35 sinh phẩm BSL-1, kiểm tra hạn dùng và xuất/nhập sinh phẩm vào khay thí nghiệm.

4. **Phòng sinh học phân tử (x 0..13,5, z -9..-3,6 sau nới hành lang):**
   - Bộ micropipet cơ học thể tích thay đổi (P10: 0,5–10 µL, P200: 20–200 µL, P1000: 100–1000 µL) mô phỏng chính xác cơ cấu 2 nấc bấm vật lý: nấc 1 hút đúng thể tích định mức, nấc 2 xả cạn hoàn toàn (blow-out); đầu côn dùng 1 lần (đầu trắng, vàng, xanh) và nút bấm nhả đầu côn vào hộp thải.
   - Máy lắc xoáy vortex trộn đều mẫu eppendorf 1,5 mL; máy ly tâm mini góc nghiêng và máy ly tâm để bàn có kiểm tra cân bằng đối xứng rotor theo công thức RCF.
   - Tủ thao tác tiền PCR (PCR workstation) có đèn chiếu UV khử khuẩn và quạt thổi khí sạch HEPA.
   - Máy luân nhiệt PCR 96 giếng (`PcrProgram`): Cài đặt chu kỳ nhiệt biến tính (94–95 °C), gắn mồi ($T_a = T_m - 5\,^\circ\text{C}$) và kéo dài (72 °C).
   - Hệ thống điện di gel agarose 1% kèm bộ nguồn điện áp 50–150 V, khay đổ gel có lược tạo giếng, thước đo di chuyển vạch băng ADN.
   - Hệ thống buồng chụp ảnh gel huỳnh quang Gel Doc (`GelDocProgram`): Nguồn kích thích ánh sáng xanh / UV, kính lọc màu cam, chụp ảnh lưu kết quả điện di với thang chuẩn ADN 1 kb / 100 bp.
   - Máy quang phổ đo mật độ quang vi khuẩn $\text{OD}_{600}$ (`Od600Program`): Bước sóng 600 nm, chức năng Blank/Zero với môi trường nuôi cấy trắng, đo mật độ tế bào vi khuẩn.

5. **Nhà kính thực vật (x 13,5..27, z -9..-3,6 sau nới hành lang):**
   - Trạm quang hợp rong đuôi chó *Elodea*: Bể thủy sinh, phễu gom khí, ống nghiệm hứng oxy, đèn LED quang hợp điều chỉnh khoảng cách và cường độ sáng, bọt khí $\text{O}_2$ thoát ra tính theo phương trình sinh lý thực vật.
   - Trạm thoát hơi nước mao quản (Potometer): Ống thủy tinh chứa bọt khí chỉ thị dịch chuyển theo cường độ thoát hơi nước, chịu ảnh hưởng của quạt gió và độ ẩm môi trường.
   - Trạm thí nghiệm tính hướng sáng: Hộp tối có khe hẹp một bên, cây mầm đậu uốn cong về phía khe sáng theo gradient auxin.
   - Giàn thủy canh màng dinh dưỡng NFT (Nutrient Film Technique): Tuần hoàn dung dịch khoáng, đo pH và EC dinh dưỡng tự động.

6. **Hành lang tầng 2 (rộng 3,6 m) & khu vực chung:**
   - Bảng tin thông báo điện tử hành lang (`ClassroomProgram`), ki-ốt thông tin tra cứu lịch thực hành và nội quy an toàn BSL-1.
   - Tủ locker cá nhân học sinh, bồn rửa mắt khẩn cấp, vòi tắm an toàn, bộ xử lý sự cố tràn đổ sinh học.

---

### 3. Kịch bản thực hiện 15 bài thí nghiệm SGK GDPT 2018

Hệ thống cho phép thực hiện trọn vẹn cả 15 bài thí nghiệm chuẩn, tự động đối chiếu các bước thao tác và tính toán hậu quả khoa học chính xác khi học sinh phạm lỗi:

| Mã bài | Lớp | Tên bài thí nghiệm | Thao tác đúng chuẩn | Lỗi cố ý & Hậu quả khoa học tái hiện |
|---|:---:|---|---|---|
| `te_bao_hanh` | 10 | Tế bào biểu bì vảy hành | Dao lam cắt vảy mỏng, nhỏ xanh methylen, đậy lamen 45° | Để khô mẫu, không đậy lamen $\to$ bọt khí che khuất, tế bào nhăn rúm |
| `khi_khong` | 10/11 | Khí khổng lá thài lài tía | Tách biểu bì mặt dưới lá, nhỏ nước cất, soi 40x | Tách nhầm biểu bì mặt trên $\to$ mật độ khí khổng cực thấp |
| `co_nguyen_sinh` | 10 | Co và phản co nguyên sinh | Nhỏ NaCl 10% hút giấy thấm mép đối diện $\to$ co; nhỏ nước cất $\to$ phản co | Dùng nhầm nước cất ngay từ đầu $\to$ không xảy ra co nguyên sinh |
| `catalase` | 10 | Phân hủy $\text{H}_2\text{O}_2$ bằng catalase | Khoai tây/gan tươi cho vào $\text{H}_2\text{O}_2$ 3% $\to$ sủi bọt oxy mạnh | Dùng mẫu đã luộc chín $\ge 65\,^\circ\text{C}$ $\to$ enzyme catalase biến tính, không bọt |
| `len_men_ruou` | 10 | Lên men ethanol | Men sống + glucose 10% ở 32 °C đậy bóng bay $\to$ bóng phồng căng $\text{CO}_2$ | Dùng nước sôi $> 60\,^\circ\text{C}$ $\to$ nấm men chết nhiệt, bóng xẹp lép |
| `len_men_lactic` | 10 | Lên men lactic (làm sữa chua) | Sữa ấm + men sữa chua ủ 42 °C trong 8h $\to$ pH giảm về 4,2, đông tụ mịn | Ủ ở nhiệt độ phòng 18 °C $\to$ vi khuẩn chậm lớn, pH không đạt, sữa lỏng |
| `quang_hop` | 11 | Quang hợp rong đuôi chó | Ngập nước chứa $\text{NaHCO}_3$, bật đèn 100W cách 20cm $\to$ 35 bọt khí/phút | Tắt đèn hoặc che tối $\to$ pha sáng dừng lại, tốc độ bọt = 0 |
| `thoat_hoi_nuoc` | 11 | Đo thoát hơi nước bằng potometer | Cuống cành cắm khít trong potometer, mở quạt $\to$ bọt dịch chuyển nhanh | Bôi vaseline phủ kín hai mặt lá $\to$ bít khí khổng, bọt khí đứng yên |
| `huong_sang` | 11 | Hướng sáng cây mầm | Hộp có khe hẹp một bên, chiếu sáng lệch 45° $\to$ ngọn mầm uốn cong 42° | Đặt trong tối hoàn toàn $\to$ mầm vống dài vàng vọt, không uốn cong (0°) |
| `tach_chiet_adn` | 12 | Tách chiết ADN thực vật | Nghiền chuối với đệm ly giải + muối, ủ 55 °C, lọc, rót cồn lạnh -20 °C $\to$ sợi ADN | Cồn ấm 25 °C và thiếu muối $\to$ không trung hòa photphat, không thấy sợi ADN |
| `dem_vi_sinh` | 10 | Pha loãng & đếm khuẩn lạc | Pha loãng bậc 10, trang đĩa PCA, ủ 37 °C 24h $\to$ đĩa $10^{-4}$ có 120 CFU | Quên thay đầu côn micropipet $\to$ nhiễm carryover, khuẩn lạc mọc thành mảng |
| `cay_ria` | 10 | Cấy ria trên đĩa thạch | Đốt đỏ que cấy vòng, ria 4 vùng $\to$ tách khuẩn lạc đơn ở vùng 3–4 | Quên đốt que cấy giữa các vùng $\to$ vi khuẩn mọc dày đặc, không tách đơn |
| `nhuom_gram` | 10 | Kỹ thuật nhuộm Gram vi khuẩn | Tím kết tinh $\to$ Lugol $\to$ cồn 96% 15s $\to$ safranin: sữa chua tím, E. coli hồng | Tẩy cồn kéo dài 45s $\to$ tẩy trôi peptidoglycan Gram dương, hóa hồng giả |
| `pcr_dien_di` | 12 | PCR và điện di gel agarose | Chạy PCR $T_a = 55\,^\circ\text{C}$, tra giếng gel 1%, cắm điện cực âm ở giếng $\to$ băng 500 bp | Cắm ngược cực điện di $\to$ ADN âm chạy ngược lên trên thoát khỏi giếng |
| `duong_cong_od600` | 10 | Đo mật độ quang vi khuẩn | Bật máy quang phổ 600 nm, chuẩn Blank với môi trường trắng $\to$ đo đường cong log | Quên chuẩn Blank $\to$ hấp thụ nền môi trường làm lệch tuyệt đối kết quả |

---

### 4. Trang web quản lý phòng thí nghiệm sinh học (`server/web/sinh.html`)

Trang quản lý phòng thí nghiệm tầng 2 được tích hợp mượt mà vào máy chủ FastAPI (`/sinh`), cùng ngôn ngữ thiết kế CSS với hệ thống chung (biến màu, thẻ, bảng cuộn `tablewrap`, huy hiệu, đồ thị SVG nhẹ không dùng thư viện ngoài) và tương thích hoàn toàn trên thiết bị di động (chiều rộng 375 px không vỡ layout, không cuộn ngang toàn trang):

- **Tab 1: Tổng quan tầng 2 (`tong-quan`):** Sơ đồ mặt bằng SVG thể hiện chính xác 8 phân khu sau khi mở rộng hành lang 3,6 m. Từng thiết bị là một điểm báo trạng thái trực tiếp (xanh: sẵn sàng, lam: đang chạy, vàng: cần bảo trì, đỏ: hỏng); các thẻ thống kê tổng số dụng cụ, mượn trả, sinh phẩm sắp hết, cảnh báo nhiệt độ tủ và sự cố an toàn.
- **Tab 2: Dụng cụ & thiết bị (`dung-cu`):** Danh mục 108 dụng cụ theo phòng và loại; tìm kiếm tiếng Việt tức thì không dấu; kiểm tra trạng thái quá hạn hiệu chuẩn; thao tác sửa đổi số lượng trực tiếp.
- **Tab 3: Mượn – trả (`muon-tra`):** Quản lý phiếu mượn dụng cụ theo học sinh và lớp; hiển thị trạng thái quá hạn trả (màu đỏ); ghi nhận trả dụng cụ và tự động cập nhật số lượng hỏng về cơ sở dữ liệu.
- **Tab 4: Kho sinh phẩm (`sinh-pham`):** Phân loại vị trí lưu trữ (tủ mát 4 °C, tủ đông -20 °C, kệ); phân loại hạn sử dụng (vàng $\le 30$ ngày, đỏ quá hạn); giao diện nhập/xuất tuân thủ nghiêm ngặt cấp an toàn `BSL-1`.
- **Tab 5: Nhiệt độ tủ (`nhiet-do`):** Đồ thị SVG biến thiên nhiệt độ 24 giờ cho 3 tủ (`tu_mat_4`, `tu_dong_am20`, `tu_am_37`), đánh dấu ngưỡng cảnh báo chuẩn ($4 \pm 2\,^\circ\text{C}$, $-20 \pm 5\,^\circ\text{C}$, $37 \pm 1\,^\circ\text{C}$) và nhật ký mở cửa tủ kéo dài.
- **Tab 6: Đặt lịch máy / phòng (`dat-lich`):** Lưới lịch tuần 7 ngày trực quan; đặt lịch máy/phòng; kiểm tra xung đột thời gian và trả về thông báo lỗi 409 Conflict chi tiết.
- **Tab 7: Kết quả thí nghiệm (`ket-qua`):** Bảng tổng hợp kết quả 15 bài thí nghiệm theo lớp và học sinh; giáo viên chấm điểm trực tiếp; nút tải file CSV mã hóa UTF-8 BOM hiển thị chuẩn tiếng Việt trên Microsoft Excel.
- **Tab 8: Sự cố an toàn sinh học (`su-co`):** Theo dõi các báo cáo sự cố (đổ vỡ lam kính, kim đâm, tràn hóa chất); giáo viên cập nhật quy trình xử lý và trạng thái giải quyết.
- **Tab 9: Bảo trì – hiệu chuẩn (`bao-tri`):** Lập lịch và theo dõi lịch sử hiệu chuẩn micropipet, máy đo OD600, cân phân tích, tủ cấy an toàn sinh học.
- **Tab 10: Hướng dẫn dụng cụ & 15 bài thí nghiệm (`huong-dan`):** Tra cứu nhanh quy trình thao tác chuẩn (SOP), thông số kỹ thuật có trích dẫn nguồn chuẩn hoặc cờ mô phỏng, lỗi thường gặp và quy tắc an toàn BSL-1 cho mọi dụng cụ.

---

### 5. Cơ chế đồng bộ dữ liệu Unity $\leftrightarrow$ Web (`BioLabSync.cs`)

- **Địa chỉ máy chủ & chế độ ngoại tuyến:** Đồng bộ qua biến môi trường `KHKT_SERVER_URL` (mặc định `http://127.0.0.1:8000`). Chế độ `KHKT_WEB_SINH=0` tắt hoàn toàn đồng bộ mạng cho các môi trường kiểm thử cô lập.
- **Gom mẻ sự kiện không chặn (Non-blocking Batching):** Gom mẻ định kỳ 5 giây hoặc khi hàng đợi đạt $\ge 20$ sự kiện, gửi bất đồng bộ qua `UnityWebRequest` tới endpoint `POST /lab/sinh/su-kien`. Cơ chế lùi thời gian thử lại khi mất kết nối (5s $\to$ 10s $\to$ 30s $\to$ 60s).
- **Lưu trữ đệm ngoại tuyến (Offline Resiliency):** Hàng đợi tối đa 500 sự kiện; tự động lưu xuống tệp JSON Lines `hang_doi_sinh.jsonl` tại thư mục dữ liệu cục bộ khi thoát ứng dụng và nạp lại khi khởi động tiếp theo.

---

### 6. Kết quả kiểm thử tự động & Hiệu năng đồ họa

Toàn bộ các bộ kiểm thử tự động đều đạt **100% PASS**:

| Bộ kiểm thử | Lệnh thực thi | Kết quả | Ghi chú |
|---|---|:---:|---|
| **Python Backend Tests** | `/Users/trantrong/Documents/Antigravity/.venv/bin/python -m unittest discover -s tests` | **139 / 139 PASS** (1,02 s) | Kiểm tra toàn bộ API REST, CSDL SQLite, AI SOP, an toàn BSL-1 và xuất CSV BOM. |
| **C# Chem Tests** | `bash tools/chay_kiem_thu_hoa.sh` | **147 / 147 PASS** (0,32 s) | Mô phỏng hóa học phân tích, UV-Vis, AAS, GC-MS, HPLC và xúc tác catalase. |
| **C# Bio Tests** | `bash tools/chay_kiem_thu_hoa.sh tests/BioSimulationTests.cs` | **82 / 82 PASS** (0,15 s) | Vi sinh pha loãng, cấy ria, nhuộm Gram, micropipet, ly tâm, PCR, điện di, quang hợp, potometer. |
| **Hành lang tầng 2** | `KHKT_TEST=tang2_hanh_lang` (Unity batchmode) | **PASS** | Kiểm tra mở rộng hành lang lên 3,6 m ($z \in [-3,6; 0]$), `ZoneMap.At` chuẩn xác, nhân vật di chuyển không kẹt cửa 3 phòng phía nam, chụp 5 ảnh mặt bằng và phối cảnh. |
| **Đồng bộ Web Tầng 2** | `KHKT_TEST=tang2_web` (Unity batchmode) | **PASS** | Gửi sự kiện thành công lên FastAPI server, xử lý mượt mà khi ngắt mạng, lưu và nạp hàng đợi đĩa ngoại tuyến. |
| **15 bài thí nghiệm** | `KHKT_TEST=tang2_dung_cu` (Unity batchmode) | **PASS** | Chạy toàn bộ 15 bài thí nghiệm theo 2 lượt (đúng chuẩn và sai cố ý), log chi tiết từng bước, chụp đủ 30 ảnh vào `docs/anh-tang2/dung_cu/`. |
| **Hồi quy Tầng 2** | `KHKT_TEST=tang2` (Unity batchmode) | **PASS** | Kiểm tra 12 trạm tầng 2 (trạm 20..31), đổi theme Quốc tế / Tối giản Japandi. |
| **Kiểm thử giao diện Web** | `capture_web_shots.py` (Playwright Chromium) | **PASS** | Kiểm tra cả 10 tab trên Desktop 1280px và Mobile 375px ở 2 chế độ Sáng / Tối, không lỗi console trình duyệt, chụp đủ 40 ảnh vào `docs/anh-tang2/web/`. |

- **Hiệu năng hiển thị thực tế (Unity):**
  - Trạm 23 (Tủ an toàn sinh học): **126,3 FPS**
  - Trạm 30 (Nhà kính thực vật): **118,4 FPS**
  - Trạm 28 (Sinh học phân tử): **117,4 FPS**
  - Trạm 21 (Bàn kính hiển vi 1): **99,5 FPS**
  - Trạm 29 (PCR điện di): **90,3 FPS**
  - Trạm 25 (Phòng vi sinh): **83,0 FPS**
  - Mọi điểm đều vượt xa mục tiêu mượt mà của dự án.

