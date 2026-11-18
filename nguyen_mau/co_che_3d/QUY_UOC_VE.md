# Quy ước vẽ cơ chế trên giấy (kiểu đáp án HSG)

Mục tiêu: hình **đúng** chưa đủ, phải **dễ đọc như đáp án**. RDKit chỉ lo phần đúng (kiểm tra bảo toàn nguyên tử, electron,
điện tích) và hình học 3D. Cách bày hình trên giấy theo các quy ước dưới đây, cài trong [ve_giay.js](ve_giay.js).

## 1. Thư viện khung mẫu (`KHUNG_MAU`)

Khung khó vẽ được lưu **một lần**, theo đúng cách đáp án vẽ. Gặp lại khung đó thì dùng `datKhung(...)`, không để máy tự bố trí.

| Mẫu | Dùng cho | Cách vẽ (lấy từ đáp án) |
|---|---|---|
| `leu` | [1.1.1]propellane, bicyclo[1.1.1]pentane | "Lều" phối cảnh: hai C đầu cầu chụm giữa (B1 trên-phải, B2 dưới-trái), ba cầu CH₂ toả ra đỉnh trên T, xa bên trái L, góc dưới phải R. Cầu L–B1 đi phía sau. To hơn vòng thường 1,35 lần. |
| `thoi` | cyclobutane, methylenecyclobutane | Hình thoi "◇"; =CH₂ nằm ngang bên trái. |
| `cyclopropen_dung` | cyclopropene spiro | C=C dựng đứng bên phải C spiro, nét đôi thứ hai phía trong vòng. |

Thêm mẫu mới: thêm một mục vào `KHUNG_MAU` (toạ độ chuẩn hoá, độ dài liên kết = 1, trục y hướng lên), ghi nguồn đáp án đã
lấy cách vẽ đó vào bảng này.

## 2. Quy ước vẽ

1. **Không để nét cắt nhau.** Nếu phối cảnh bắt buộc phải cắt (khung lồng, cầu), nét phía sau đánh dấu `"sau"`. Nét phía
   trước có **khe che** màu giấy ở đoạn giữa, nên chỗ giao đọc được là "đi phía trước" (giống ChemDraw).
2. **Nhóm thế viết gọn** như đáp án: CO₂Me, MeO₂C (đổi chiều theo phía gắn), OMe, Ph, Ts…; chữ cắt nét liên kết.
3. **Liên kết đôi trong vòng:** nét thứ hai ngắn hơn, nằm phía trong vòng. Liên kết đôi ngoài vòng (=CH₂) vẽ cân hai nét.
4. **Chất phản ứng thứ hai** đặt **trên mũi tên phản ứng** (như đáp án), điều kiện (t°, xt…) đặt dưới.
5. **Mũi tên cong:**
   - nửa đầu (một ngạnh) cho 1 electron, đầy đủ cho 2 electron;
   - vòng **ra ngoài phân tử**, không cắt ngang khung;
   - hai electron tạo cùng một liên kết thì hai mũi tên **gặp nhau** ở chỗ liên kết mới;
   - mũi tên từ liên kết trung tâm của khung lồng đi ra phía có chất phản ứng tới (dấu ")" bên phải lều).
6. **Electron độc thân:** một chấm đặt ở phía trống của nguyên tử (ngược hướng các liên kết).
7. **Giữ hướng khung giữa các bước:** chất sau đặt cùng hướng với chất trước để mắt dò theo được.
8. **Nhãn chất** (A1, A4…) in đậm, ngay dưới chất.

## 3. Bộ chấm độ dễ nhìn (`kiemTraDeNhin`)

Chạy tự động mỗi lần vẽ, in kết quả ngay dưới sơ đồ và trong console. Đếm 5 loại lỗi:

| Lỗi | Ý nghĩa |
|---|---|
| nét cắt nhau | hai liên kết cắt nhau mà không nét nào là `"sau"` |
| chữ đè | nhãn nhóm thế đè lên nét hoặc đè lên nhãn khác |
| mũi tên cắt phân tử | mũi tên cong cắt ngang liên kết hoặc đè chữ (bỏ qua đoạn trong 0,4 quanh hai đầu mũi tên) |
| mũi tên sát nhau | hai mũi tên chạy sát nhau trên một quãng dài, khó biết của bước nào |
| góc hẹp | hai liên kết tại một nguyên tử hợp góc < 14° |

Bộ chấm chỉ bắt được lỗi **đo được**. Vẫn phải nhìn bằng mắt và so với đáp án; thấy lỗi mới thì bổ sung quy tắc vào bộ chấm.

## 4. Kho mẫu học từ đáp án (đã chạy 15/11/2026)

`server/ai/co_che/hoc_ve/hoc_mau.py` đọc hình ChemDraw nhúng (CDX) trong file Word đáp án qua `server/ai/co_che/hoc_ve/doc_cdx.py`, không cần xem ảnh.

- Nguồn: thư mục "TỔNG HỢP ĐỀ" (DHBB 2024–2026, Chọn ĐT các tỉnh 2024, Olympic): **318 file, 1.794 hình ChemDraw** sau
  khi bỏ trùng và bỏ "(Bản sao)". Thư mục chỉ có PDF (Mendeleev, Trại hè, ICHO, Lọc ĐT) chưa học được: ChemDraw xuất PDF
  mất thông tin nguyên tử, cần phần mềm nhận dạng công thức từ ảnh.
- Kết quả trong `$VERA_DU_LIEU_RIENG/co_che/kho_mau/khung_mau_dap_an.json` (dữ liệu kín, ngoài kho git): **101 khung** (≥ 3 lần) và **512 khung kèm nhóm gắn** (≥ 2 lần), mỗi
  mẫu tối đa 3 cách vẽ kèm tỉ lệ dùng.
- Quy ước rút ra: độ dài nét trung vị **14,4 pt** (chuẩn ACS 1996 của ChemDraw); lục giác, ngũ giác, norbornane, propellane
  đặt **đỉnh nhọn lên trên**; vòng 4 cạnh và decalin, adamantane **cạnh phẳng ở trên** (trừ khi nhóm gắn cần nằm ngang,
  vd "=◇"); nhãn hay dùng OH, OMe, Me, Ph, CO₂Me (nhiều hơn COOMe), OBn, OAc, OTs, OTBS.
- `server/ai/co_che/hoc_ve/ve_theo_mau.py` vẽ tự động từ SMILES theo kho mẫu (thử mọi cách vẽ đã học, chấm, chọn tốt nhất; xoay chất sau
  theo chất trước bằng atom map). **Chưa đạt bằng bản chỉnh tay**: với cơ chế A4, bộ chấm còn báo 10 lỗi. Xem ở
  `so_sanh_ve.html`.

### Tư duy vẽ và bộ đo (CHỈ ADMIN)

`server/ai/co_che/hoc_ve/tu_duy_ve.py` – ngoài kho mẫu, học **nguyên tắc chung** để vẽ được khung chưa gặp: 69% nét nằm trên lưới 30°;
vòng 3, 5, 6 cạnh đa số đỉnh nhọn lên trên, vòng 4, 7, 8 cạnh cạnh phẳng ở trên; hình bày bề ngang; nhánh ra khỏi vòng chia
đôi góc ngoài. Thứ tự ưu tiên như người vẽ: khung đã gặp → theo mẫu; mạch hở → cách bày tự nhiên; tư duy quyết khi khung
mới, hoặc khi phương án kia gây lỗi dễ nhìn.

`server/ai/co_che/hoc_ve/bo_do.py` – học trên ~75% file, chấm trên ~25% file máy chưa thấy → `$VERA_DU_LIEU_RIENG/co_che/kho_mau/bao_cao_bo_do.json` (chỉ admin).
Kết quả 15/11/2026 (500 phân tử):

| | RDKit mặc định | Kho mẫu + tư duy |
|---|---|---|
| giống hướng đáp án (tất cả) | 12,2% | 24,0% |
| lỗi dễ nhìn / hình (tất cả) | 0,06 | 0,04 |
| giống dáng – khung mới | 31,0% | 37,9% |
| lỗi dễ nhìn / hình – khung mới | 0,69 | 0,34 |

Đã thêm quy tắc "hướng nhánh chính" (nhánh lớn nhất ra khỏi vòng chĩa về đâu): phân bố trong đáp án gần đều (cao nhất
15% ở 30°), chỉ giúp nhẹ – giống dáng tất cả 55,0 → 55,8%, khung mới 37,9 → 41,4%.

Chẩn đoán 152 phân tử có vòng 6 cạnh vẽ sai (file chưa thấy): 25 đúng hẳn; 84 vòng đúng hướng nhưng nhánh gắn sai đỉnh
(lệch 60° ×29, lật gương 180° ×20, 120° ×14); 43 vòng sai hướng nhọn/phẳng. **Kết luận:** chọn đỉnh gắn nhánh và lật gương
trong đáp án phụ thuộc NGỮ CẢNH SƠ ĐỒ (chất trước vẽ thế nào, chỗ phản ứng quay về mũi tên, chừa chỗ cho mũi tên cong), không
có quy tắc chung cho phân tử đứng riêng. Bước sau: học và đo trên cả SƠ ĐỒ (chuỗi chất + mũi tên trong cùng một hình ChemDraw),
không chấm từng phân tử tách rời.

### Học theo sơ đồ (`server/ai/co_che/hoc_ve/so_do.py`, chỉ admin → `$VERA_DU_LIEU_RIENG/co_che/kho_mau/bao_cao_so_do.json`)

1.058 sơ đồ (≥ 2 chất trong một hình ChemDraw). Quy tắc ngữ cảnh học từ 882 sơ đồ:

| Quy tắc | Tỉ lệ trong đáp án |
|---|---|
| chất sau giữ hướng phần khung chung với chất trước | 65% |
| ở chất trước, phần sắp đổi nằm phía mũi tên (bên phải) | 69% |
| ở chất sau, phần mới sinh ra nằm **bên phải** (tiếp tục chiều đọc; đoán ban đầu "bên trái" là sai) | 67% |

Vẽ cả chuỗi: chọn đồng thời (thử 8 hướng cho chất đầu, dựng cả chuỗi theo sau, lấy chuỗi tổng điểm thấp nhất); trọng số
"giữ hướng" chỉnh để tỉ lệ giữ hướng của máy gần mức đáp án, không ép quá. Chấm 120 sơ đồ chưa thấy (448 chất):

| | vẽ từng chất | vẽ theo sơ đồ |
|---|---|---|
| cặp chất giữ hướng nhau (đáp án 65%) | 41,1% | 64,0% |
| giống dáng | 56,9% | 57,8% |
| giống hướng (từng chất) | 20,3% | 16,7% |
| lỗi dễ nhìn / hình | 0,05 | 0,05 |

Kết luận: vẽ theo sơ đồ cho chuỗi **nhất quán như đáp án** nhưng **không** làm tăng "giống hướng" tính trên từng chất –
cùng một chuỗi, mỗi người ra đề chọn một hướng khác nhau. Điểm "giống hướng" từng chất có trần thấp; thước đo nên dùng tiếp:
"giống một trong các cách vẽ có trong đáp án" và độ nhất quán của chuỗi. Với cơ chế (có atom map), ve_theo_mau.py đã xoay
chất sau theo chất trước bằng atom map – chính xác hơn khung chung tìm bằng MCS.

## 5. Học thêm từ bộ đề

Với mỗi đề có đáp án:

1. Tìm các khung chưa có trong `KHUNG_MAU`. Ghi lại cách đáp án vẽ thành mẫu mới (toạ độ + nét sau).
2. Ghi các quy ước mới gặp (cách đặt chất phản ứng, kiểu mũi tên, cách viết nhóm thế) vào mục 2.
3. Vẽ lại cơ chế của đáp án bằng `ve_giay.js`, so từng hình. Bộ chấm phải "đạt", và hình phải đọc được như đáp án.

File Word có hình ChemDraw nhúng là tốt nhất (lấy được toạ độ chính xác); PDF hay ảnh vẫn dùng được.
