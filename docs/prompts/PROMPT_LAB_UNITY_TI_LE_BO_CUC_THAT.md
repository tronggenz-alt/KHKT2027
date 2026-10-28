# PROMPT – Phòng lab Unity: TỈ LỆ CHUẨN như game Genshin + BỐ CỤC & ĐỒ ĐẠC như phòng thí nghiệm ngoài đời + ĐỒ HOẠ CHÂN THỰC như game hiện đại

> Gõ cho Antigravity: **"Đọc và làm theo PROMPT_LAB_UNITY_TI_LE_BO_CUC_THAT.md, chạy liên tục đến hết."**
> Phạm vi: **app Unity `PhongLabHoa3D`**, gồm cả bản macOS lẫn bản WebGL nhúng trong app CHEMISTRY. Bản Three.js trên web **không đổi**.
> Chạy **sau khi** `PROMPT_NHUNG_LAB_UNITY_VAO_APP.md` xong (xem mục 0.2).

---

## DÀN Ý NHANH
**Hiện trạng** (ảnh `outputs/app-lab/sketchfab/01, 03, 04, 07, 08`, đọc code ngày 25/10):
- Toà nhà 36 × 30 m, trần phòng **5 m**, hành lang 4 m, sảnh 8 m. Mỗi phòng 8–9 × 8 m chỉ có **1 bàn** 2,4 × 1,2 m và 0–1 ghế. Sàn trắng trống trơn, giống nhà kho hơn phòng thí nghiệm.
- Cửa 1,8 × 2,6 m. Kính tủ hút chỉ hở 11 cm. Bình chữa cháy lơ lửng trên tường, không có giá treo.
- Toàn bộ chi tiết thêm trong `ChiTietThuc.cs` **không có collider**, nên nhân vật đi xuyên qua.
- Tại bàn, chai hoá chất trông to hơn bình tam giác, núm gas vàng to như cái cốc. Bàn nhận biết ion gần như trống.

**Mục tiêu "như Genshin":**
1. **Đồ vật và dụng cụ đúng kích thước thật so với nhân vật 1,70 m.** Mặt bàn ngang hông, ghế đẩu ngang đùi, tay nắm ngang tay, vòi nước, ổ điện, chai lọ… đều đúng số thật.
2. **Không gian nới vừa phải (×1,15–1,3) cho camera góc 3.** Áp cho lối đi, ô cửa, trần. Game như Genshin làm vậy để camera không kẹt tường mà vẫn trông thật. **Không** nới rộng thành nhà kho.
3. **Mỗi phòng đầy đủ như phòng thí nghiệm trường học thật:**
   - bàn giáo viên + bảng;
   - 3–4 bàn đảo học sinh, mỗi bàn có giá thuốc thử, ổ điện, van gas, chậu;
   - bàn áp tường có chậu rửa và giá úp dụng cụ;
   - tủ hút, tủ hoá chất;
   - góc an toàn: rửa mắt, tắm khẩn cấp, bình chữa cháy, chăn chống cháy, tủ sơ cứu;
   - thùng chất thải, biển báo ISO 7010, đèn, miệng gió…
4. **Có số đo kiểm chứng:** mọi kích thước nằm trong bảng chuẩn ở mục 2, có test tự đo. Ảnh chụp có người làm thước tham chiếu.
5. **Đồ hoạ chân thực như game hiện đại** (người dùng đã chọn kiểu này, **không** dùng kiểu anime/cel-shading).
   - **Hiện tại:**
     - chưa có ánh sáng dội (GI), ánh sáng nền 3 màu phẳng nên phòng trông phẳng;
     - đèn trần chỉ là tấm phát sáng, không chiếu sáng thật;
     - SSAO bán kính 0,035 m, quá nhỏ;
     - chỉ 4 vùng phản chiếu (reflection probe) cho cả toà nhà;
     - mặt bàn có texture lấm tấm lấp lánh.
   - **Cách sửa:**
     - bake ánh sáng (lightmap + đèn trần dạng area light) cùng light probe và reflection probe cho từng phòng;
     - vật liệu theo đúng thông số PBR;
     - hậu kỳ chuẩn game hiện đại;
     - cảnh thật ngoài cửa sổ.

   Chi tiết ở mục 8.

**Thứ tự làm:**
1. Đọc nguồn tham khảo (mục 1).
2. Viết bảng chuẩn dạng JSON (mục 2).
3. Sửa tỉ lệ không gian (mục 3).
4. Bố cục từng phòng (mục 4).
5. Thêm đồ (mục 5).
6. Collider + NavMesh (mục 6).
7. Camera kiểu Genshin (mục 7).
8. **Đồ hoạ: bake ánh sáng, vật liệu, hậu kỳ (mục 8)** – làm sau khi bố cục đã xong, vì đổi đồ đạc là phải bake lại.
9. Hiệu năng (mục 9).
10. Test + ảnh nghiệm thu (mục 10–11).

---

## 0. Trước khi làm
### 0.1 Đọc
AGENTS.md, CLAUDE.md, phần đầu HANDOFF.md, TIEN_DO_APP_LAB.md, `PROMPT_SUA_APP_LAB_FPS_CHAN_THAT.md` (bước 2 "chân thật"; commit `8e263c8` đã làm texture tự sinh, PBR, ACES + bloom, MSAA 4×, SSAO, shader ngọn lửa – mục 8 **nâng tiếp** phần này, không làm lại từ đầu), `PROMPT_NHUNG_LAB_UNITY_VAO_APP.md`.
Đọc thêm phần đồ hoạ:
- `Scripts/ToaNha/ChatLuongDoHoa.cs` (hậu kỳ, ánh sáng nền)
- `Scripts/ToaNha/VatLieuThuc.cs` (texture tự sinh)
- `Editor/CauHinhDoHoa.cs`
- `Settings/URP_Asset.asset`, `Settings/URP_Renderer.asset` (SSAO)
- `DungToaNha.DungAnhSangVaReflectionProbes` (dòng khoảng 539)
File Unity cần đọc:
- `Assets/StreamingAssets/ptn/toa-nha.json`
- `Scripts/ToaNha/DungToaNha.cs` (dựng phòng, cửa, trần, tủ hút, rửa mắt, bàn cân; dòng 420–482 dựng bàn trạm + đồ từ `KhoMoHinh`)
- `Scripts/ToaNha/ChiTietThuc.cs`
- `Scripts/ToaNha/DungPhongLab.cs`
- `Scripts/ThiNghiem/KhoMoHinh.cs` (ghế, bình chữa cháy dựng bằng code)
- `Scripts/ThiNghiem/DungCuISO.cs`
- `Scripts/NhanVat/DieuKhienNhanVat.cs`
- `Scripts/GiaoDien/GiaoDienApp.cs` (FOV)
- `Tests/Editor/TestToaNha.cs`
- `Tests/Editor/TestNavMeshVaPhongLab.cs`

### 0.2 Trạng thái git và dự án
- `git status`. Nếu prompt nhúng WebGL (`PROMPT_NHUNG_LAB_UNITY_VAO_APP.md`) **chưa xong** (xem HANDOFF.md / TIEN_DO_APP_LAB.md) thì làm xong nó trước.
  Gặp thay đổi chưa commit thì commit nguyên trạng. **Không** checkout / stash / reset.
- Dự án làm việc ngoài iCloud: `~/UnityProjects/PhongLabHoa3D`. Bản lưu trong repo: `unity/PhongLabHoa3D`. Đồng bộ bằng `tools/unity_dong_bo.sh keo | day`.
  Trước khi làm, chạy `diff -rq` để biết bản nào mới hơn. Bản repo mới hơn thì `keo` trước. **Không bao giờ chạy `day` khi chưa `keo`.**
- Baseline: pytest, `node --test tests/js/`, Unity EditMode. Ghi lại số PASS.
- Commit điểm phục hồi: `chore: diem phuc hoi truoc khi chinh ti le va bo cuc lab Unity`.
- Máy M2 / 8 GB RAM / ổ còn khoảng 16 GB. Tải tài nguyên CC0 **≤ 1 GB**. Ổ < 12 GB thì không tải, dựng bằng code.
  Không sửa `.env`. Không push, không deploy. Không sửa vùng Lab Sinh học.

---

## 1. Nguồn tham khảo (mở ra đọc trước khi dựng; chỉ xem để dựng lại, KHÔNG tải ảnh hay mô hình có bản quyền)
| Nguồn | Dùng cho |
|---|---|
| [RSC – Royal Society of Chemistry (tài liệu phòng thí nghiệm trường học)](https://edu.rsc.org/download?ac=13913) | Mặt bàn cao **900 mm**; mỗi học sinh tối thiểu **600 mm** chiều dài mặt bàn |
| [NZASE – Laboratory building standards](https://nzase.org.nz/wp-content/uploads/2023/06/2023-06-Laboratory-building-standards-NZASE-resource.pdf) | Lối đi tối thiểu **1200 mm**; phòng học sinh cần lối thoát rộng hơn |
| [ASE – Laboratory design for health and safety](https://science.cleapss.org.uk/resource/LABORATORY-DESIGN-FOR-HEALTH-AND-SAFETY-by-the-ASE.pdf) · [ASE Lab Design Gallery](https://science.cleapss.org.uk/Resource/ASE-Lab-Design-Gallery.pdf) | Không xếp dãy bàn dài chia cắt phòng, không tạo góc cụt; các kiểu bố cục phòng lab trường học (đảo, bán đảo, quanh tường) |
| [Syracuse – Laboratory Planning and Design Guidelines](https://answers.atlassian.syr.edu/wiki/spaces/ecs/pages/156795453/Laboratory+Planning+and+Design+Guidelines) · [NIH – Lab Module Design Considerations](https://orf.od.nih.gov/TechnicalResources/Documents/Technical%20Bulletins/13TB/Lab%20Module%20Design%20Considerations%20December%202013%20Bulletin_508.pdf) | Lối đi giữa hai bàn ≥ 57" (1,45 m), nên 60" (1,5 m); bàn sâu 30" (750 mm); module 11' (3,35 m) = tủ hút sâu 36" + lối 60" + bàn 30" |
| [UC Berkeley – Ergonomic guidelines for lab benches](https://uhs.berkeley.edu/sites/default/files/ergonomicguidelinesforlaboratorybenches.pdf) · [CU Boulder lab ergonomics](https://www.colorado.edu/ecee/media/2458) | Bàn đứng khoảng 36" (≈ 900 mm), bàn ngồi 30" (≈ 750 mm); ghế đẩu có vòng gác chân |
| [Ghế lab LLG (thông số)](https://www.labfriend.co.za/llg-lab-stool-foot-ring-pu-foam-black-glides-seat-height-570-850mm) | Ghế đẩu lab có mặt ngồi 570–850 mm, có vòng gác chân |
| [Fume cupboard – Scientific Labs](https://www.scientificlabs.co.uk/product/fume-cupboards/CAB7444) · [Thông số tủ hút IIT Kanpur](https://iitk.ac.in/new/hindi/data/tender/file/IITKCHMDHD16-1714.pdf) | Tủ hút rộng **1200 / 1500 / 1800 mm**, ví dụ 1800 × 900 × 2400 mm; theo BS EN 14175 |
| [Hướng dẫn rửa mắt / tắm khẩn cấp (ANSI Z358.1) – Loyola](https://luc.edu/media/lucedu/environmentalservices/pdfs/LUC%20Emergency%20Eyewash%20and%20Safety%20Shower%20Guidelines.pdf) · [Auburn](https://cws.auburn.edu/rms/pm/ShowerStandards) · [CSB/SJU](https://csbsju.edu/environmental-health-safety/programs/laboratory-safety/eyewash-showers) | Đầu sen cao **2083–2438 mm**; cần kéo ≤ **1730 mm**; tâm tia cách vật cản ≥ **406 mm**; đi tới trong **≤ 10 giây (≈ 17 m)**, cùng tầng |
| [Thông tư 14/2020/TT-BGDĐT – Quy định phòng học bộ môn (bản gốc)](https://vbpl.vn/FileData/TW/Lists/vbpq/Attachments/143802/VanBanGoc_2020_639%20+%20640_14-2020-TT-BGDĐT..pdf) · [Thông tư 14/2025 sửa đổi](https://vbpl.moj.gov.vn/TW/Lists/vbpq/Attachments/179994/142025.docx) | Phòng bộ môn Hoá phải có bàn ghế chịu nhiệt / hoá chất, chậu rửa + vòi nước, hệ thống điện, tủ thuốc sơ cứu, kho/phòng chuẩn bị. **Mở bản gốc** đọc diện tích tối thiểu và yêu cầu kĩ thuật; nếu có số khác bảng mục 2 thì theo văn bản |
| [TCVN 8794:2011 Trường trung học – Yêu cầu thiết kế](https://thuvienphapluat.vn/tcvn/xay-dung/tcvn-8794-2011-truong-trung-hoc-yeu-cau-thiet-ke-901835.aspx?temp=d) | Mục 5.2–5.3 (khối phòng học, phòng bộ môn). Không mở được toàn văn thì ghi "chưa đối chiếu" trong HANDOFF |
| NFPA 10 (bình chữa cháy xách tay) · TCVN 3890:2023 | Bình ≤ 18 kg: **đỉnh bình ≤ 1,53 m**, đáy cách sàn ≥ 0,1 m, treo trên giá, có biển F001 |
| [Nội thất phòng thí nghiệm Hoà Phát](https://noithathoaphat.com/thu-vien-3d/noi-that-phong-thi-nghiem) · [Lý Sơn Sa Kỳ – mô hình 3D phòng thí nghiệm](https://banthinghiemlysonsaky.vn/mo-hinh-3d-thiet-ke-phong-thi-nghiem/) | Hình dáng bàn ghế lab kiểu Việt Nam (BTNHS01/02, BTN101/102 – GTN101/102). Trang không ghi kích thước, chỉ dùng để xem hình dáng |
| [Unity – So sánh tính năng các render pipeline](https://docs.unity3d.com/cn/2022.3/Manual/render-pipelines-feature-comparison.html) | URP hỗ trợ bake lightmap Progressive CPU/GPU và reflection probe **Box Projection** |
| [Unity 6 – Adaptive Probe Volumes (khái niệm)](https://docs.unity3d.com/Manual/urp/probevolumes-concept.html) · [Cách dùng APV](https://docs.unity3d.com/Manual/urp/probevolumes-use.html) | Ánh sáng dội theo điểm ảnh cho bản macOS. WebGL **chưa xác nhận** hỗ trợ APV, nên WebGL dùng Light Probe thường |
| [STYLY – Ánh sáng nội thất trong Unity](https://styly.cc/tips/unity-interior-lighting/) | Đèn dạng area (hình chữ nhật) chế độ **Baked** cho đèn trần, kết hợp reflection probe trong phòng |
| [Polycount wiki – PBR](http://wiki.polycount.com/wiki/PBR) | Màu gốc (albedo) vật liệu phi kim nên nằm trong khoảng sRGB **≈ 30–240**, không trắng tinh, không đen tuyệt đối |
| [ambientCG](https://ambientcg.com) · [Poly Haven](https://polyhaven.com) | Texture PBR và HDRI bầu trời **CC0** (miễn phí, dùng thương mại được) |

Tự tìm thêm **ảnh phòng thật** để so (chỉ xem, không chép): "phòng học bộ môn hoá học THPT đạt chuẩn", "school chemistry laboratory interior island benches", "lab casework service spine reagent shelf".
Ghi 5–10 liên kết đã xem vào `GHI_CONG.md` (mục "Tham khảo bố cục").

---

## 2. Bảng chuẩn kích thước – `Assets/StreamingAssets/ptn/kich-thuoc-chuan.json`
- Chép thêm vào `Assets/Resources/ptn/` qua `Editor/DongBoDuLieu.cs`, đọc bằng `DocTaiNguyen`.
- **Mọi** builder và test lấy số từ file này, không gán cứng.
- Đơn vị mét. Mỗi mục có `gia_tri`, `min`, `max`, `nguon`.

**Người & đồ đạc – đúng số thật (không nới):**
| Mục | Giá trị |
|---|---|
| Nhân vật | cao 1,70 (CharacterController đang 1,7 ✓); mắt 1,58–1,62; hông khoảng 0,92 |
| Mặt bàn đứng làm (bàn đảo, bàn áp tường, bàn giáo viên) | **mặt trên 0,90** (0,88–0,92); mặt dày 0,019–0,025; chân lùi cao 0,10, sâu 0,05–0,075 |
| Bàn đảo học sinh | 2,4 × **1,5** (hai phía, mỗi phía 4 HS × 0,6 m); có cột kĩ thuật giữa cao 0,25–0,30 trên mặt bàn |
| Bàn áp tường | sâu **0,75**; gờ chắn sau 0,10–0,15 |
| Bàn giáo viên (bàn biểu diễn) | 2,4 × 0,75 × 0,90, có chậu |
| Giá thuốc thử trên cột kĩ thuật | tầng 1 cao +0,40, tầng 2 +0,70 so với mặt bàn; sâu 0,20–0,25 |
| Ghế đẩu lab | **mặt ngồi 0,60–0,65**; Ø 0,33–0,38; **vòng gác chân 0,30–0,35**; đế 5 cánh Ø 0,50–0,55 hoặc 4 chân; khoảng mặt ngồi tới mặt bàn 0,25–0,30 |
| Ghế ngồi (phòng hội thảo) | mặt ngồi 0,43–0,46; bàn 0,75 |
| Tủ treo tường | đáy **≥ 1,45** (cách mặt bàn ≥ 0,55); sâu 0,30–0,35; cao 0,70 |
| Tủ hút | rộng **1,2 / 1,5 / 1,8**; sâu 0,85–0,90; cao 2,35–2,45; mặt làm việc 0,88–0,92; **kính kéo hở 0,40–0,50** khi làm (hiện 0,11 → sửa) |
| Chậu rửa lab | lòng 0,40 × 0,30 × 0,25; **vòi cổ ngỗng cao 0,30–0,38** trên mặt bàn |
| Giá úp dụng cụ | 0,60 × 0,45 (khoảng 40 chốt), cạnh chậu, đáy cách mặt bàn khoảng 0,35 |
| Ổ điện | hộp 0,086 × 0,086; trên cột kĩ thuật hoặc gờ, cao +0,10–0,20 so với mặt bàn |
| Van gas / nước / khí | thân Ø 0,015–0,02; núm Ø **0,03–0,04** (hiện có núm Ø 0,034 nhưng nhìn quá to → kiểm tra lại tỉ lệ khi render); màu theo DIN 12920: gas **vàng**, nước lạnh **xanh lá**, khí nén **xanh dương**, chân không **xám** |
| Tủ hoá chất kính 2 cánh | 1,0 × 0,45 × 1,8–2,0 |
| Tủ an toàn | chất dễ cháy (vàng) và ăn mòn (xanh dương / trắng): 1,09 × 0,46 × 1,65 |
| Kệ kho thép 4 tầng | 1,2 × 0,5 × 2,0 |
| Rửa mắt + tắm khẩn cấp | chậu rửa mắt 0,84–1,14; đầu sen **2,08–2,44**; cần kéo ≤ 1,73; vùng trống bán kính 0,41; ô sàn sơn 1,2 × 1,2 (xanh lá, viền vàng–đen) |
| Bình chữa cháy | CO₂ 5 kg (Ø 0,14 × cao 0,65) và bột ABC 4 kg (Ø 0,16 × 0,50); **treo giá**; đỉnh ≤ 1,53, đáy ≥ 0,10 |
| Đồ treo tường | chăn chống cháy (hộp đỏ 0,30 × 0,30 × 0,10, tâm 1,5); tủ sơ cứu xanh lá (0,40 × 0,30 × 0,15, tâm 1,5) |
| Biển ISO 7010 | E001/E002 thoát hiểm 0,40 × 0,20 trên cửa (2,3–2,5); E003 sơ cứu, E011 rửa mắt, E012 tắm khẩn cấp, F001 bình chữa cháy, M004 đeo kính (0,20 × 0,20, tâm 1,6) |
| Bảng trắng | 3,0 × 1,2, mép dưới 0,90 |
| Màn hình / TV | 65" = 1,45 × 0,83, tâm khoảng 1,9 |
| Đồng hồ | Ø 0,30 ở 2,3 |
| Móc áo blouse | 8 móc ở 1,65 |
| Tủ tiệt trùng kính bảo hộ | 0,60 × 0,30 × 0,60, đáy 1,2 |
| Thùng rác & chất thải | thùng rác đạp chân 0,30 × 0,30 × 0,45; can chất thải HDPE 20 L (0,30 × 0,25 × 0,42) có nhãn; hộp thuỷ tinh vỡ 0,30 × 0,30 × 0,60 trên chân; xô cát Ø 0,30 × 0,30 |
| Tủ để cặp ngoài hành lang | 0,9 × 0,45 × 1,8 (3 cột × 4 ngăn) |
| Dụng cụ | giữ đúng số trong `DungCuISO` / prompt trước: burette 50 mL dài khoảng 0,75; bình tam giác 250 mL cao 0,145; cốc 250 mL Ø 0,07 × 0,095; ống nghiệm 0,016 × 0,16; chai thuốc thử 100–250 mL Ø 0,05–0,07 × 0,12–0,16; chai nhỏ giọt 30–60 mL Ø 0,035 × 0,09; bình tia 500 mL Ø 0,075 × 0,19; đèn cồn Ø 0,07 × 0,10; giá sắt đế 0,20 × 0,13, cọc 0,60 |

**Không gian – nới kiểu game cho camera góc 3** (ghi cả số "thật" và số "game" trong JSON):
| Mục | Thật | Game (dùng) |
|---|---|---|
| Trần phòng lab | 3,0–3,3 | **3,6** (hiện 5,0 → hạ) |
| Trần hành lang | 2,7–3,0 | **3,3** (hiện 4,0) |
| Sảnh | – | **6,5** thông 2 tầng (hiện 8,0) |
| Ô cửa phòng lab | thông thuỷ 1,0 × 2,1 | **1,4 × 2,4** cửa trượt kính có khung (hiện 1,8 × 2,6) |
| Lối giữa hai mặt làm việc đối nhau | ≥ 1,5 | **1,8–2,0** |
| Lối giữa bàn và tường / bàn áp tường | ≥ 1,2 | **1,5** |
| Lối chính từ cửa vào | ≥ 1,2 | **1,8** |
| Cửa sổ | bệ 0,95, đầu 2,7 | bệ **1,0**, đầu **3,0** (theo trần 3,6) |

---

## 3. Sửa tỉ lệ không gian (`toa-nha.json` + `DungToaNha.cs` + `DungPhongLab.cs`)
- Đổi `cao_phong`, `cao_hanh_lang`, `cao_sanh`, `phong[].cao`, `phong[].cua` (rộng/cao) theo bảng "Game" ở mục 2.
- Sửa các khối tường đang gán cứng theo trần 5 m / 8 m, ví dụ `TuongBac_Duoi` 1,2 m, kính Bắc cao 3 m ở tâm 2,7, `TuongNam_LanhTo` "lên tới trần sảnh 8m". Sửa cho khớp trần mới, có ô trần, gờ tường và chân tường 0,10.
- Ống thoát tủ hút (`OngThoat`) dài đúng tới trần mới. Đèn panel 0,6 × 0,6 xếp lưới khoảng 2,4 m. Thêm miệng gió cấp/hút 0,6 × 0,6, đầu báo khói, đầu phun chữa cháy trên trần.
- Bảng tuần hoàn ở sảnh (7 × 3,6 m) giữ, nhưng trần sảnh 6,5 m phải còn ≥ 1,5 m phía trên.
- **Giữ kích thước phủ bì toà nhà 36 × 30 m** và vị trí các phòng (đỡ phải dựng lại NavMesh / bản đồ). Nếu phòng nào quá trống thì **thêm phòng con** (kho hoá chất, phòng chuẩn bị, khu rửa dụng cụ), không phóng to đồ đạc.

## 4. Bố cục từng phòng (dữ liệu trong `toa-nha.json`, không gán cứng)
- Thêm `phong[].noi_that`: danh sách `{ loai, tam:[x,z], huong:0|90|180|270, tram?: id, chi_khi_cao?: bool }`. Kích thước lấy từ `kich-thuoc-chuan.json`.
- Viết `Scripts/ToaNha/DungNoiThatChuan.cs`, dựng từ dữ liệu này. Dùng lại các hàm dựng hình có bo cạnh và vật liệu PBR sẵn có (`ChiTietThuc`, `VatLieuThuc`, `LabHinh`).
- `KhoMoHinh` có prefab Sketchfab thì **tự co giãn theo bounds để khớp đúng kích thước chuẩn** (không tin scale gốc của file).
- **Mẫu phòng thí nghiệm học sinh** (phòng 8–9 × 8 m: chuẩn độ, nhận biết, pin, điện phân). Áp mẫu này, xoay theo vị trí cửa:
```
 tường có cửa sổ (bệ 1,0)
 ┌───────────────────────────────────────────────┐
 │ [bàn áp tường + 2 chậu + giá úp + tủ treo]      │  ← sâu 0,75, lối 1,5 phía trước
 │                                               │
 │   [ĐẢO 1: 2,4×1,5]      [ĐẢO 2: 2,4×1,5]       │  ← mỗi đảo 8 ghế đẩu, giá thuốc thử 2 tầng
 │        lối 1,8–2,0 giữa các mặt làm việc       │
 │   [ĐẢO 3 = BÀN TRẠM]    [ĐẢO 4 / bỏ nếu chật]  │  ← bàn trạm thí nghiệm = một đảo hàng trước
 │                                               │
 │ [TỦ HÚT 1,5]   [BÀN GV 2,4×0,75 + chậu]  [BẢNG 3,0×1,2 + TV]
 │ [tủ hoá chất kính]               [móc áo][tủ kính BH]  │
 │ [RỬA MẮT+TẮM KC][bình CC][chăn][sơ cứu] ║ CỬA 1,4 ║ [thùng rác][can thải][hộp TT vỡ]
 └───────────────────────────────────────────────┘
```
  - **Bàn trạm** (`tram[]`) là một bàn đảo **có thật trong bố cục**, không đứng lẻ giữa phòng.
    Cập nhật `tram[].tam`, `kich_thuoc_ban` (2,4 × 1,5, mặt 0,90) và `diem_dung` (trước mặt làm việc 0,55 m, phía sau trống ≥ 0,9 m).
    Kiểm tra lại các cảnh `Canh*.cs` vẫn đặt dụng cụ đúng trên mặt bàn, góc nhìn tại bàn vẫn đẹp.
  - Các bàn đảo **không phải trạm** vẫn bày đồ tĩnh hợp lí: giá ống nghiệm, bình tia, cốc úp ngược, giấy lau, chai thuốc thử. Ghế đẩu kê sát bàn, lệch nhẹ ngẫu nhiên (có seed) cho tự nhiên.
  - Rửa mắt / tắm khẩn cấp gần cửa: **mọi bàn đi tới ≤ 17 m theo NavMesh**. Tủ hút cách cửa ra ≥ 3 m, không chắn lối thoát.
- **Phòng Kho hoá chất & An toàn:**
  - tủ an toàn vàng (dễ cháy) + xanh dương (ăn mòn);
  - 2–3 kệ kho thép có chai/hộp dán nhãn;
  - tủ lạnh hoá chất;
  - bộ ứng phó tràn đổ (thùng vàng 0,4 × 0,4 × 0,6);
  - xô cát, bình chữa cháy, bảng nội quy kho.
  Giữ bàn trạm an toàn hiện có, đặt theo đúng mẫu.
- **Phòng Cân & Pha chế:**
  - 2 bàn cân chống rung (1,2 × 0,75 × 0,80, mặt đá) có cân phân tích lồng kính;
  - bàn áp tường đặt máy khuấy từ, bếp điện (0,2 × 0,3 × 0,1), tủ sấy (0,6 × 0,55 × 0,8);
  - máy cất nước treo tường + can nước cất 20 L có vòi;
  - 1 tủ hút.
- **Phòng Hội thảo:** bàn họp 3,6 × 1,2 × 0,75 + 10 ghế (mặt ngồi 0,45), TV, tủ trưng bày kính, bảng trắng.
- **Hành lang:** tủ để cặp học sinh, bảng tin 1,2 × 0,9 (tâm 1,5), thùng rác, biển chỉ hướng tới từng phòng, biển thoát hiểm.
- **Sảnh:** băng ghế chờ, chậu cây cao (Ø 0,4 × 1,6), quầy hướng dẫn 2,4 × 0,6 × 1,05, bảng tuần hoàn (giữ nguyên).
- Theo ASE: không tạo dãy bàn dài chia cắt phòng, không tạo góc cụt. Mỗi phòng có lối thoát rõ ràng tới cửa.

## 5. Thêm đồ – danh sách bắt buộc (mỗi phòng thí nghiệm học sinh)
Phải có tối thiểu:
- bàn GV có chậu, bảng trắng, TV;
- 3–4 bàn đảo có cột kĩ thuật (ổ điện + van gas vàng + vòi nước xanh) và giá thuốc thử 2 tầng;
- bàn áp tường có 2 chậu, vòi cổ ngỗng, giá úp, tủ treo;
- 1 tủ hút, 1 tủ hoá chất kính;
- rửa mắt + tắm khẩn cấp (có ô sàn sơn), 2 bình chữa cháy trên giá, chăn chống cháy, tủ sơ cứu;
- móc áo blouse, tủ kính bảo hộ;
- thùng rác, 3 can chất thải dán nhãn ("Acid – Base", "Hữu cơ có halogen", "Hữu cơ không halogen"), hộp thuỷ tinh vỡ;
- biển ISO 7010 (E001/E002, E003, E011, E012, F001, M004), bảng nội quy;
- đồng hồ, đèn panel, miệng gió, đầu báo khói;
- ổ điện tường cao 0,30, công tắc 1,3.

Khai báo danh sách này trong `kich-thuoc-chuan.json` → `bo_do_bat_buoc.phong_hoc_sinh` để test kiểm tra được.
Đồ nhỏ, chỉ để trang trí, đánh dấu `chi_khi_cao` (bản WebGL và mức Thấp sẽ bỏ qua).

## 6. Collider + NavMesh: không đi xuyên đồ
- Mọi đồ đứng trên sàn (bàn, tủ, ghế, thùng, kệ, chậu cây, rửa mắt) có **một BoxCollider bao ngoài**. Đồ nhỏ trên bàn không cần collider.
- `ChiTietThuc.cs` hiện ghi "KHÔNG thêm collider (NavMesh đã bake)". Đổi sang cách dự án đang dựng NavMesh (đọc `TestNavMeshVaPhongLab.cs` / `DieuKhienNhanVat.cs`), và dựng lại NavMesh **sau khi** dựng xong nội thất.
- Ghế đẩu đẩy được thì vẫn chặn bằng collider nhỏ (Ø 0,4). Lối đi phải đúng bảng mục 2 **sau khi** trừ bán kính nhân vật.

## 7. Camera & cảm giác kiểu Genshin
- **Góc 3 (mặc định):**
  - Tâm xoay ở ngang ngực/đầu (khoảng 1,45 m). Khoảng cách mặc định 4,0 m, lăn chuột 1,5–7 m. FOV dọc **50–60°** (mặc định 55; FOV quá rộng làm đồ trông nhỏ, phòng trông to).
  - Lệch phải 0,3–0,4 m. Nhân vật chiếm khoảng 25–35 % chiều cao khung hình.
  - **Va chạm camera:** spherecast từ tâm xoay; có tường hoặc tủ chắn thì kéo camera lại gần mượt (0,15 s), hết chắn thì lùi ra từ từ (0,4 s), không giật.
- **Góc 1:** mắt ở 1,60 m, FOV 60.
- Di chuột xoay camera (đang có). Khi tới gần bàn trạm, camera bay vào cận cảnh như prompt trước. **Không đổi phím điều khiển** đang có.
- **Nghiệm thu bằng mắt:** đứng cạnh bàn, mặt bàn phải **ngang hông** nhân vật, ghế đẩu ngang khoảng giữa đùi, tay nắm tủ ngang tay buông. Sai thì sửa số, không sửa camera để che.

## 8. Đồ hoạ chân thực như game hiện đại (nâng tiếp `8e263c8`, không làm lại từ đầu)
**Hiện trạng** (đọc code 25/10):
- `ChatLuongDoHoa.cs`: ánh sáng nền kiểu Trilight (phẳng), 1 đèn mặt trời realtime + vài đèn điểm.
- Đèn trần chỉ là vật liệu phát sáng, **không chiếu sáng gì**.
- SSAO trong `URP_Renderer`: Intensity 3, **Radius 0,035** (chỉ tối khe rất nhỏ).
- 4 reflection probe realtime cho cả toà nhà; **các phòng lab không có probe riêng**.
- **Không có ánh sáng dội.** Mặt bàn epoxy tự sinh có chấm sáng lấp lánh.
- Bóng đổ 2048, 2 cascade, 25 m.

### 8.1 Ánh sáng toàn cục – việc quan trọng nhất
Phòng thật sáng đều là nhờ ánh sáng dội từ trần, tường, sàn. Thiếu phần này thì phòng phẳng như game cũ.
Toà nhà hiện dựng bằng code lúc chạy, nên muốn bake phải dựng sẵn trong Editor:
1. **Công cụ Editor** `Editor/DungSanVaBake.cs`, menu "Phòng Lab/Dựng sẵn toà nhà + bake ánh sáng":
   - Chạy đúng các builder runtime (`DungToaNha`, `ChiTietThuc`, `DungNoiThatChuan`) trong Edit mode.
   - Lưu vào scene mới `Assets/Scenes/PhongLab_Bake.unity`.
   - Mesh và texture sinh bằng code thì lưu thành asset trong `Assets/SinhRa/` (texture PNG) để bake và build dùng được.
2. **Static:** kiến trúc + nội thất đứng yên được bật Contribute GI, Reflection Probe Static, Batching Static.
   Dụng cụ thí nghiệm, dung dịch, nhân vật, cửa trượt là đồ động.
3. **Đèn:**
   - Mỗi tấm đèn trần 0,6 × 0,6 là một **Area Light hình chữ nhật, chế độ Baked**, 4000–5000 K.
   - Đèn trong tủ hút cũng là area light baked.
   - Mặt trời: Directional, chế độ **Mixed (Baked Indirect)**, 5500–6500 K, chiếu qua cửa sổ.
   - Không thêm đèn realtime mới. Ngọn lửa đèn cồn chỉ có 1 point light nhỏ, bật khi đang cháy, không đổ bóng.
4. **Lightmap:**
   - Progressive GPU nếu chạy được trên M2, không thì CPU.
   - Mật độ 10–15 texel/m cho tường/sàn, 20–30 texel/m cho mặt bàn. Atlas ≤ 2048, có nén.
   - Bật khử nhiễu, 2–3 lần dội, AO baked (khoảng cách tối đa 0,5 m).
   - Ghi thời gian bake và dung lượng vào TIEN_DO_APP_LAB.md. Ổ < 8 GB thì dừng, báo người dùng.
5. **Light Probe** cho nhân vật và đồ động: lưới 1,5 m trong mỗi phòng, ở độ cao 0,5 / 1,2 / 2,0 m, dày hơn quanh bàn trạm.
   - Bản macOS có thể dùng **APV** của Unity 6.
   - WebGL dùng Light Probe thường.
6. **Reflection Probe:** mỗi phòng 1 probe **Baked, Box Projection**, hộp khớp sát tường. Thêm 1 probe nhỏ ở mỗi bàn trạm cho kính và inox khi cận cảnh.
7. **Lúc chạy:** app nạp `PhongLab_Bake.unity`. Thiếu scene này (hoặc đang chạy test) thì về đường dựng runtime cũ, giữ được.
   Builder phải nhận ra "đã dựng sẵn" để không dựng chồng lần hai.
   **Đổi bố cục hay đồ đạc thì phải bake lại.** Ghi rõ cách bake lại trong `desktop/README.md` / TIEN_DO_APP_LAB.md.

### 8.2 Vật liệu đúng thông số thật (PBR)
Thêm mục `vat_lieu` vào `kich-thuoc-chuan.json` để test kiểm tra được.
| Vật liệu | Albedo (sRGB) | Metallic | Smoothness |
|---|---|---|---|
| Tường sơn trắng ngà | 200–215 | 0 | 0,15–0,25 |
| Trần tấm | khoảng 220 | 0 | 0,10–0,20 |
| Sàn vinyl xám | 140–170 | 0 | 0,40–0,50 |
| Mặt bàn phenolic / epoxy (xanh đậm hoặc đen) | 35–60 | 0 | 0,35–0,50 (**bỏ chấm lấp lánh**) |
| Tủ gỗ / melamine sáng | 150–190 | 0 | 0,30–0,40 |
| Inox chải (chậu, tay nắm, khung) | 170–230 | 1 | 0,60–0,70 |
| Thép sơn tĩnh điện (khung, tủ an toàn) | theo màu | 0 | 0,40–0,55 |
| Nhựa PP (chậu, can) | theo màu | 0 | 0,35–0,45 |
| Kính | – | 0 | 0,95 |

- Mọi vật liệu phi kim có albedo sRGB trong khoảng **30–240**: không trắng tinh 255, không đen tuyệt đối.
- **Texture:** ưu tiên CC0 từ ambientCG / Poly Haven (vinyl, sơn tường, inox chải, gỗ, trần tấm), 1K–2K, tổng ≤ 1 GB. Mật độ khoảng 512 px/m cho đồ đạc, 256–512 px/m cho sàn/tường.
  **UV theo kích thước thật**: tính tiling theo kích thước hộp, không để texture bị kéo giãn khi scale. Ghi nguồn vào `TAI_NGUYEN.json` + `GHI_CONG.md`.
- **Bo cạnh 2–5 mm** cho mọi đồ đạc để có viền sáng ở mép.
- **Decal** (URP Decal Projector):
  - băng vàng–đen quanh ô rửa mắt;
  - vạch chỉ lối thoát;
  - vết bẩn rất nhẹ gần cửa và chậu;
  - nhãn tủ.
- **Nhãn chai thuốc thử** là texture chữ thật, đọc được khi cận cảnh: "HCl 0,1 M", "NaOH 0,1 M", "CuSO₄ 1 M", "AgNO₃", "BaCl₂", "Phenolphthalein"…
- **Kính + dung dịch:** giữ shader vẽ hai lượt đã có. Bật Opaque Texture trong URP rồi thêm **khúc xạ giả** (lệch `_CameraOpaqueTexture` theo pháp tuyến), fresnel, màu theo độ dày. Vạch chia sắc nét.
- **Nhân vật:** da PBR, vải áo có normal map, nhận Light Probe, nhận bóng mặt trời. Thêm vệt bóng mềm dưới chân bằng Decal khi ở trong nhà.

### 8.3 Hậu kỳ chuẩn game hiện đại (sửa `VolumeHauKy` trong `ChatLuongDoHoa.cs`)
- **Tonemapping:** chụp so sánh ACES và Neutral, chọn bản giống ảnh phòng thật hơn và ghi lí do. Thêm White Balance khớp đèn 4500 K.
- **Phơi sáng không cháy, không bệt:** trên ảnh nghiệm thu, < 1 % điểm ảnh ở mức 255 và < 1 % ở mức 0. Độ sáng trung bình 0,35–0,55.
- **Bloom:** threshold ≥ 1,0, intensity 0,15–0,25 (chỉ quanh đèn và lửa).
- **SSAO:** Radius **0,25–0,4 m**, Intensity 0,6–1,0, blur cao. Tắt trên WebGL vì đã có AO bake.
- **Bóng mặt trời:** Cao = 4096, 4 cascade, 30 m, soft cao. Vừa = 2048.
- **Khử răng cưa:** Cao = MSAA 4× (chữ nét). Render scale < 1 thì dùng STP.
- **Depth of Field** Gaussian rất nhẹ, **chỉ ở chế độ tại bàn**: nền mờ nhẹ, dụng cụ nét.
- **Tắt:** motion blur, chromatic aberration, film grain mạnh, lens dirt. Vignette ≤ 0,15.

### 8.4 Ngoài cửa sổ
- Skybox là **HDRI CC0 ban ngày** (Poly Haven), cũng là nguồn sáng trời và phản chiếu.
- Thêm vài khối nhà và cây low-poly ở xa, mờ nhẹ theo sương, để nhìn ra cửa sổ thấy cảnh thật, không phải màu trơn. Kính cửa sổ phản chiếu nhẹ.

### 8.5 Các mức chất lượng
| Mức | Thiết lập |
|---|---|
| **Cao** (mặc định trên M2) | Tất cả mục 8 |
| **Vừa** | Bóng 2048, SSAO thấp, không DoF |
| **Thấp** | Không SSAO, bóng 1024 1 cascade, render scale 0,8 + STP |
| **WebGL trong app** | Lightmap + Light Probe + reflection probe baked (rẻ), không SSAO, MSAA 2×, bỏ đồ `chi_khi_cao` |

Bake ánh sáng vừa **đẹp hơn vừa nhẹ hơn**, vì ánh sáng đã tính sẵn. Đo fps trước và sau để chứng minh.

## 9. Hiệu năng (đồ nhiều lên nhưng không được tụt fps)
- Bật GPU instancing cho vật liệu dùng chung. Gộp tĩnh (`StaticBatchingUtility.Combine`) sau khi dựng. Chai lọ, ghế, đèn, biển báo dùng chung mesh.
- Không thêm đèn realtime. Đèn trần chỉ là emissive. Bóng đổ như hiện có.
- Ngân sách:
  - **macOS (M2, 1920×1200):** ≥ 60 fps khi đi và tại bàn; batches ≤ 1500; SetPass ≤ 250; tam giác ≤ 1,5 triệu.
  - **WebGL trong app:** ≥ 45 fps (bỏ đồ `chi_khi_cao`); tam giác ≤ 800 nghìn.
- Đo bằng camera tự kiểm tra sẵn có (`TuKiemTraApp`) và ghi số thật vào `bao-cao.json`.

## 10. Test bắt buộc (Unity EditMode, thêm `Tests/Editor/TestTiLeThat.cs` + `Tests/Editor/TestDoHoaThat.cs`)
**Đồ hoạ (`TestDoHoaThat.cs`):**
- Không vật liệu nào dùng shader lỗi (màu tím/magenta, `Hidden/InternalErrorShader`).
- Vật liệu URP/Lit phi kim có albedo trong sRGB 30–240. Metallic là 0 hoặc 1 (sai lệch ≤ 0,05, trừ ngoại lệ có ghi). Smoothness đúng bảng `vat_lieu`.
- Scene `PhongLab_Bake.unity` tồn tại và có lightmap (`LightmapSettings.lightmaps.Length > 0`).
- Mỗi phòng có Light Probe (≥ 12 probe) và 1 reflection probe Baked có Box Projection.
- Đèn trần là Area Light chế độ Baked.
- Hồ sơ hậu kỳ có SSAO Radius 0,25–0,4, Bloom intensity ≤ 0,25, không bật Motion Blur / Chromatic Aberration.

**Tỉ lệ (`TestTiLeThat.cs`):**
Dựng toà nhà trong test (như `TestToaNha`), đo bằng `Renderer.bounds` / `Collider.bounds` rồi so với `kich-thuoc-chuan.json`:
- Mặt bàn mọi bàn đứng ở 0,88–0,92. Mặt ngồi ghế đẩu ở 0,60–0,65 và có vòng gác chân. Tủ treo đáy ≥ 1,45.
- Tủ hút: rộng thuộc {1,2; 1,5; 1,8} ± 0,02, cao 2,35–2,45, kính hở 0,40–0,50.
- Rửa mắt / tắm: chậu 0,84–1,14, đầu sen 2,08–2,44, cần kéo ≤ 1,73, vùng 0,41 m không có collider.
- Bình chữa cháy: đỉnh ≤ 1,53, đáy ≥ 0,10, có giá treo.
- Cửa, trần, bệ cửa sổ đúng bảng "Game".
- **Lối đi:** chiếu mọi collider đồ đứng xuống mặt sàn (XZ):
  - Khoảng trống giữa hai mặt làm việc đối nhau ≥ 1,8.
  - Khoảng trống giữa bàn và tường ≥ 1,5.
  - Ô 1,0 × 1,0 trước mỗi cửa trống.
- NavMesh: từ `diem_dung` mỗi bàn tới rửa mắt gần nhất ≤ 17 m. Từ `xuat_phat` tới mọi `diem_dung` đều có đường.
- Mỗi phòng có đủ `bo_do_bat_buoc`. Không có hai collider đồ đứng chồng lên nhau.
- Nhân vật: cao 1,70 ± 0,05; mắt 1,55–1,65.
- **Tỉ lệ dụng cụ tại bàn:** chiều cao chai thuốc thử / bình tam giác 250 mL trong 0,8–1,2. Núm van gas Ø ≤ 0,04.
- Mọi test cũ (37+) và test mới đều PASS. Pytest + `node --test` vẫn PASS.

## 11. Ảnh nghiệm thu (tự mở ra xem từng ảnh; ảnh phải kèm số đo chứng minh không trống)
**Đồ hoạ:**
- Trước khi sửa, chụp **ảnh "trước"** ở cùng các vị trí (từ cửa mỗi phòng, tại 1 bàn trạm), rồi chụp lại **ảnh "sau"** và ghép đôi trước/sau.
- Mỗi ảnh ghi vào `bao-cao.json`: tỉ lệ điểm ảnh ở mức 0 và mức 255, độ sáng trung bình.
- Đặt 1 ảnh "sau" cạnh 1 ảnh phòng thật đã xem ở mục 1, viết nhận xét (ánh sáng, vật liệu, độ sạch).

**Tỉ lệ và bố cục:**
Lưu vào `outputs/app-lab/ti-le-that/`. Mỗi ảnh tính độ lệch chuẩn độ sáng > 12 và ≥ 20 màu, ghi vào `bao-cao.json`:
1. Mỗi phòng: một ảnh **từ cửa nhìn vào** (mắt 1,6 m, FOV 55) và một ảnh **mặt bằng nhìn thẳng từ trên** (camera trực giao, có lưới 1 m).
2. **Ảnh thước người:** camera trực giao nhìn ngang, nhân vật đứng cạnh bàn đảo + ghế đẩu + tủ hút + rửa mắt, kèm vạch đo 0,5 m. Nhìn vào là thấy tỉ lệ đúng.
3. Góc 3 khi đi giữa hai bàn đảo: camera không xuyên tường hay tủ. Có ảnh trước/sau khi camera tự kéo gần.
4. Ảnh tại từng bàn trạm: dụng cụ cân đối, chai lọ không to hơn bình.
5. So từng ảnh "từ cửa" với 2–3 ảnh phòng thật đã xem ở mục 1. Viết 2–3 câu nhận xét giống / chưa giống ở đâu vào HANDOFF.md, sửa tiếp nếu lệch rõ.

## 12. Bàn giao
- Commit theo phần:
  1. Bảng chuẩn + test.
  2. Không gian.
  3. Bố cục.
  4. Đồ đạc.
  5. Collider / NavMesh.
  6. Camera.
  7. Bake ánh sáng.
  8. Vật liệu + hậu kỳ.
  9. Hiệu năng + ảnh.

  Không commit lightmap hay texture CC0 lớn nếu `.gitignore` đã loại trừ; ghi rõ cách tạo lại.

  Mỗi lần sửa Unity: chạy `tools/unity_dong_bo.sh day` rồi mới commit.
- Build lại `PhongLabHoa3D.app` (macOS) và bản WebGL cho app (nếu prompt nhúng đã có hàm build). Chép vào `outputs/app-lab/` như cũ.
- Cập nhật TIEN_DO_APP_LAB.md, HANDOFF.md (bảng số đo + nhận xét so với ảnh thật), CLAUDE.md (thêm `kich-thuoc-chuan.json`, `DungNoiThatChuan.cs`, `noi_that` trong `toa-nha.json`, `TestTiLeThat`, `DungSanVaBake.cs`, `PhongLab_Bake.unity`, cách bake lại, `TestDoHoaThat`), GHI_CONG.md (nguồn tham khảo + tài nguyên CC0 nếu có).
- Báo cáo ngắn cho người dùng: đã đổi gì (trần, cửa, lối đi, số bàn ghế, đồ mới), số fps thật, ảnh thước người, những gì còn chưa giống phòng thật.
- **KHÔNG push, KHÔNG deploy.**

## 13. Điều kiện "XONG"
- [ ] Ánh sáng đã bake: đèn trần chiếu sáng thật, có ánh sáng dội, mỗi phòng có reflection probe box và light probe.
- [ ] Vật liệu đúng bảng PBR, mặt bàn hết lấp lánh, nhãn chai đọc được.
- [ ] Hậu kỳ không cháy sáng, không bệt tối. Có ảnh trước/sau và so với ảnh phòng thật.
- [ ] Mọi kích thước nằm trong `kich-thuoc-chuan.json`, có test đo tự động.
- [ ] Mỗi phòng thí nghiệm có đủ bộ đồ bắt buộc, bố cục theo mẫu. Bàn trạm là một bàn đảo trong bố cục.
- [ ] Trần, cửa, lối đi theo bảng "Game". Camera góc 3 không kẹt hay xuyên tường. Nhân vật không đi xuyên đồ.
- [ ] Ảnh thước người cho thấy bàn ngang hông, ghế ngang đùi, dụng cụ cân đối.
- [ ] macOS ≥ 60 fps, WebGL trong app ≥ 45 fps, đo bằng số thật.
- [ ] Mọi test PASS. Tài liệu đã cập nhật. Đã commit. Chưa push / deploy.
