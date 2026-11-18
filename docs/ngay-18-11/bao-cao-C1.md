# Báo cáo C1: Cơ chế Hóa vào kho VERA, bộ 8 bài demo, nâng "tư duy vẽ"

Phiên "N18 Cơ chế" (Claude), nhánh `n18/co-che`, ngày 18/11/2026. **Kết quả: XONG C1** (đủ 8 bài, không phải cắt theo mục "Nếu trễ").

## 1. Đưa mã vào kho, không mang dữ liệu gốc

- **Không merge 494acdb7.** Mã lấy bằng `git checkout <nhánh> -- <đường dẫn>`, chỉ lấy tệp .py, .js, .html, .md:
  - từ `claude/chemistry-3d-rag-mechanism-c9669e`: `nguyen_mau/co_che_3d/{HOP_DONG_APP.md, QUY_UOC_VE.md, *.html, ve_giay.js}`,
    `dung_kich_ban_3d.py` và `hoc_ve/*.py` (chuyển sang `server/ai/co_che/`);
  - từ `chemistry`: `backend/co_che.py` → `server/ai/co_che/co_che.py`.
- **Module thật sự cần (đọc theo import):** bộ dựng 3D và `hoc_ve/` chỉ cần RDKit, numpy. Lõi `co_che.py` cần thêm `config` và `llm`, nhưng
  chỉ cho phần "AI viết cơ chế". Tôi bỏ phần AI (`SCHEMA_CO_CHE`, `sinh_bang_ai`, `tao_co_che`, `giai_thich_chi_tiet_co_che`), giữ định dạng,
  bộ đẩy electron, `kiem_tra`, `dung_tu_mui_ten`, `lam_sach`. `LoiCoChe` giờ kế thừa `ValueError`.
  **Không chép** `mo_hinh_3d.py` (cần db, llm, parser, research, PubChem), `renderer.py`, `ve_co_che.py` (cần chemdraw.py), vì
  không có mã nào trong luồng 3D ⇄ giấy gọi tới. C2 sẽ thêm phần AI theo kiến trúc RAG local.
- `doc_cdx.py`: import `olefile` thành không bắt buộc (chỉ admin cần khi đọc file Word .doc).
- **Dữ liệu kín** ở `~/Developer/VERA_du_lieu_rieng/` (đã `git init`, chưa commit, chỉ lưu tại máy):
  - `co_che/kho_mau/`: `khung_mau_dap_an.json`, `bao_cao_bo_do.json`, `bao_cao_so_do.json` (lấy blob từ 494acdb7 bằng `git show`), cùng hai
    báo cáo đo dễ nhìn trước/sau;
  - `co_che/bai_demo/<ma>/{bai.json, co_che.json}`: 8 bài, và nguồn `co_che/tao_bai_demo.py`;
  - `co_che/anh/hsg_propellane_a4.png`.
  Mã đọc qua `server/ai/co_che/duong_dan.py`: biến môi trường `VERA_DU_LIEU_RIENG`, mặc định là thư mục trên. Đường dẫn dùng `pathlib`, không
  cứng kiểu Mac. Không có kho mẫu thì vẫn vẽ được nhờ tư duy vẽ.
- **.gitignore** thêm: `/build/`, `*.verapack`, `*khung_mau_dap_an*`, `bao_cao_bo_do*.json`, `bao_cao_so_do*.json`,
  `nguyen_mau/co_che_3d/du_lieu/`, `VERA_du_lieu_rieng/`, `*.key`.
- **Kiểm tra rò rỉ:**
  - `git log --all --oneline -- '*khung_mau_dap_an*'` chỉ ra đúng `494acdb7`.
  - `git branch -r --contains 494acdb7` rỗng. `git ls-remote origin` cũng không có nhánh `chemistry-3d`.
  - **Kết luận: kho mẫu đáp án CHƯA từng lên GitHub.** Commit 494acdb7 chỉ nằm ở máy. Lịch sử không bị viết lại.

## 2. Bộ 8 bài demo

| # | Mã | Bài | Bước | Trạng thái 3D | Nhánh sai | co_che.kiem_tra |
|---|---|---|---|---|---|---|
| 1 | `sn2_ch3br` | SN2: CH₃Br + OH⁻ | 1 | 3 | – | đạt |
| 2 | `sn1_tbubr` | SN1: (CH₃)₃CBr + H₂O | 3 | 8 | SN2 bị chặn (cản trở không gian) | đạt |
| 3 | `e2_2_brombutan` | E2: 2-bromobutane + KOH/ethanol (Zaitsev) | 1 | 3 | H gauche, không anti-periplanar | đạt |
| 4 | `cong_hbr_propen` | Cộng HBr vào propene (Markovnikov) | 2 | 4 | – | đạt |
| 5 | `clo_hoa_metan` | Thế Cl vào methane: khơi mào, phát triển mạch, tắt mạch | 4 | 11 | – | đạt |
| 6 | `nitro_hoa_benzen` | Nitro hóa benzene: SEAr, ion arenium có 3 cấu trúc cộng hưởng | 2 | 6 | – | đạt |
| 7 | `este_hoa_fischer` | Ester hóa Fischer, xúc tác H⁺, thuận nghịch | 5 | 9 | – | đạt |
| 8 | `hsg_propellane_a4` | HSG: [1.1.1]propellane + DMAD → A4 | 3 | 5 | – | đạt |

- Mỗi bài có `bai.json`: đề, lớp, mục tiêu, điều kiện, các bước kèm giải thích ngắn, 3 câu hỏi có đáp án và lời giải, lỗi hay gặp, nguồn.
  Bài 2 và 3 có thêm `nhanh_sai`.
- `co_che.kiem_tra` (bảo toàn nguyên tử, H, điện tích, hóa trị, lập thể, cộng hưởng) đạt cả 8 bài ngay lần đầu, không bài nào phải bỏ qua.
  `dung_bai_demo` kiểm tra thêm: mũi tên của nhánh sai áp được lên trạng thái gốc.
- Đối chiếu chương trình GDPT 2018:
  - SGK Hóa 11 gồm các bài Alkane (thế halogen), Alkene (cộng HX, Markovnikov), Arene (nitro hóa), Dẫn xuất halogen (thế OH, tách HX,
    Zaitsev), Carboxylic acid (ester hóa); SGK Hóa 12 có bài Ester.
  - SN1, SN2, E2 và cơ chế gốc tự do nằm trong Chuyên đề học tập.
  - Mỗi bài có thêm Clayden làm nguồn đối chiếu cơ chế.
  - **Số trang chưa ghi**, vì tôi không có sách để tra. Mỗi bài đã ghi chú "cần ghi thêm khi in". Tôi không bịa số trang.
- Một số lựa chọn hóa học cần biết:
  - SN1: bước tách H⁺ dùng `chuyen_h` (H₂O thứ hai nhận H⁺).
  - E2: hai H ở C3 đều là nguyên tử riêng có map ([H:6] anti, [H:8] gauche). Gợi ý 3D đặt góc nhị diện H6–C3–C2–Br = 180°.
  - Nitro hóa: bài bắt đầu từ NO₂⁺; phản ứng tạo NO₂⁺ ghi trong `giai_thich`. Base tách H⁺ là HSO₄⁻.
  - Fischer: H⁺ là nguyên tử riêng [H+:10], được tái tạo ở cuối. O của nước đến từ acid, đúng với thí nghiệm ¹⁸O.

## 3. Lệnh dựng tất cả

```
python -m server.ai.co_che.dung_bai_demo            # → build/co_che/<ma>/<ma>.{bai,3d,giay}.json (+ .js cho trang web), muc_luc.json
```

- `build/` bị git bỏ qua. 8 bài dựng xong trong khoảng 1 giây.
- Bộ dựng 3D (`dung_kich_ban_3d.py`) được viết lại cho tổng quát. Bản thử cũ chỉ chạy được kiểu propellane, gặp 5/8 bài thì lỗi. Bản mới:
  - xử lý được bài chỉ có một chất ban đầu (SN1, Cl₂);
  - xử lý chất mới tham gia giữa chừng: thêm trạng thái "xa" rồi "gần";
  - xử lý chất phụ tách ra;
  - giữ H mang map và giữ tên H khi chuyển H⁺ rút gọn;
  - đặt chất tấn công ở phía "mở": sau lưng nhóm rời đi, vuông góc mặt phẳng π;
  - dựng nhánh sai kèm màn chắn.
- Trạng thái sau mỗi bước được tính bằng chính bộ đẩy electron (`co_che.ap_mui_ten`).
- Đã kiểm tra mọi liên kết có độ dài hợp lý, không có nguyên tử nào chen sát.
- Hợp đồng được mở rộng, các trường cũ giữ nguyên. Các trường mới ghi trong `HOP_DONG_APP.md` (đánh dấu "18/11"):
  - `.3d.json`: `dien_tich`, `nhanh_sai`, cờ bước;
  - `.giay.json`: nét `dam` và `gach`, `dt`, `chu`, `mt.kieu`, `pu.thuan_nghich`.
- `ve_giay.js` đã vẽ được các trường mới.

## 4. Tư duy vẽ: đo trước, sửa, đo sau

**Bộ đo** (`bo_do.py --de-nhin`): bộ đo "giống đáp án" cũ cần thư mục đề Word, và các bài demo không có đáp án ChemDraw. Vì vậy tôi thêm bộ chấm
"dễ nhìn" theo đúng luật `kiemTraDeNhin` của `ve_giay.js`, cộng thêm hai tiêu chí: độ lệch khỏi lưới 30° và độ đều của độ dài liên kết.
Điểm tính trên thang 100, mỗi lỗi bị trừ: nét cắt nhau −10, chữ đè nét/chữ −5, mũi tên cắt nét/đè chữ −4, mũi tên sát nhau/góc hẹp −3.

**20 chất** lấy ngẫu nhiên (`random.seed(18112026)`) từ `data/chem/hoa_chat.json`, bỏ các chất không có cấu tạo xác định. Kho dữ liệu không
có SMILES nên tôi ghi tay (`hoc_ve/chat_do_thu.json`). 12/20 chất là mạch hở, không có khung trong kho mẫu. Bromocresol xanh có khung
sultone spiro chưa có trong kho. 7 chất có vòng benzene đã có trong kho.

**Đã sửa:**
- **Lỗi gốc trong kho mẫu:** khi ghép khung kèm nhóm gắn, nguyên tử "*" của RDKit khớp mọi nguyên tử, nên nhóm thế bị đặt vào vị trí trong
  vòng (spiro A4 bị bẹp). Đã thêm `hoc_mau.doi_xung_giu_loai`, dùng trong `ve_theo_mau` và `tu_duy_ve`, và phạt nét dài hoặc ngắn bất thường.
- **Mạch zigzag 120°:** dùng CoordGen, rồi xoay để nét nằm trên lưới 30° và trục dài nằm ngang (`_xoay_luoi`).
- **Vòng 6 cạnh hướng chuẩn:** khung lạ hoặc mẫu gây lỗi thì chuyển cho `TuDuy`. `TuDuy` dùng nguyên tắc học được (vòng 6 đỉnh nhọn 73%,
  12 góc × lật). Nguyên tắc đọc từ báo cáo bộ đo, không có thì dùng giá trị mặc định.
- **Dò chồng nhãn và đẩy lệch:** nhãn đầu mạch đè nét hoặc đè nhãn khác thì xoay nét ±30…180° quanh nguyên tử kề. Chất xếp cạnh nhau
  theo khung bao có tính cả hộp chữ.
- **MeO₂C / CO₂Me theo phía:** đảo chiều cả nhãn nhóm thế (`[*]`) lẫn nhãn dị tố. Ví dụ: HO, H₂O⁺, H₃C.
- **Điện tích:** ghi trong nhãn dị tố (OH⁻, Br⁻, O⁺). Cacbocation có dấu + riêng (`dt`).
- **Nét đậm/gạch cho lập thể:** dùng RDKit `WedgeMolBonds` trên tọa độ 2D. Có test với (S)-2-bromobutane.
- **Mũi tên cong không cắt chữ:**
  - điểm đầu đặt ở phía trống, ngoài hộp chữ; điểm cuối dừng trước nguyên tử đích;
  - thử 2 phía × 4 độ cong, chọn đường ít cắt nét, không đè chữ, không sát mũi tên khác;
  - mũi tên 2 e có hai ngạnh.
- **Khung lồng kiểu "lều":** bicyclo[1.1.1] dùng khung dựng sẵn (như `KHUNG_MAU.leu`). Cầu L–B1 vẽ nét "sau". Không cần kho mẫu.
- **Sửa thêm:**
  - ion một nguyên tử (H⁺) không còn ra tọa độ NaN;
  - không còn lỗi NaN khi xoay theo chất trước, là lỗi làm bài este hóa không vẽ được;
  - vòng thơm vẽ nét đôi xen kẽ (Kekulé);
  - sơ đồ dài tự xuống hàng;
  - có dấu "+" giữa các chất;
  - vẽ được ⇌ và chữ "chậm".

**Bảng điểm trước/sau** (`bao_cao_de_nhin_{truoc,sau}.json` trong kho mẫu riêng):

| Bài demo | Trước | Sau | Lỗi trước | Lỗi sau |
|---|---|---|---|---|
| clo_hoa_metan | 82,0 | 97,0 | mũi tên đè chữ 3, sát nhau 2 | sát nhau 1 |
| cong_hbr_propen | 94,6 | 100 | mũi tên đè chữ 1 | – |
| e2_2_brombutan | 92,6 | 100 | chữ đè chữ 1 | – |
| este_hoa_fischer | **0 (không vẽ được)** | 96,0 | lỗi NaN | mũi tên đè chữ 1 |
| hsg_propellane_a4 | 46,3 | 75,5 | nét cắt 1, chữ đè 1, mũi tên cắt 4, góc hẹp 4 | mũi tên sát 2, góc hẹp 3 (của khung lều) |
| nitro_hoa_benzen | 88,4 | 92,6 | mũi tên cắt nét 2 | mũi tên cắt nét 1 |
| sn1_tbubr | 91,0 | 100 | mũi tên đè chữ 2 | – |
| sn2_ch3br | 96,0 | 100 | mũi tên đè chữ 1 | – |
| **Trung bình 8 bài** | **73,9** | **95,1** | | |
| **Trung bình 20 chất** | **95,6** | **99,8** | Bromocresol xanh 36,1 (6 nét cắt) | Bromocresol xanh 96,2 (0 nét cắt) |

Không bài nào bị giảm điểm. Chi tiết 20 chất: 12 chất đã đạt 100 từ trước. Các chất còn lại (etylen glicol, 1,2-đibromoetan, ion axetat,
Freon-22, pentyl axetat, ion xitrat) chỉ lệch lưới 30° và nay đều đạt 100.

**Ảnh 8 sơ đồ:**
- 7 bài SGK nằm ở `docs/ngay-18-11/anh/c1/*.png`.
- Ảnh bài HSG propellane **giữ ở** `~/Developer/VERA_du_lieu_rieng/co_che/anh/`. Đó là lời giải đề HSG lấy từ đáp án, mà kho đang công khai.
  Theo luật "không commit dữ liệu cơ chế gốc", tôi chọn cách an toàn này. Nếu muốn đưa ảnh vào docs thì chép sang.
- Cách xem nhanh: launch.json `co-che-3d` (máy chủ tĩnh ở gốc kho, cổng 8765), mở `/nguyen_mau/co_che_3d/xem_bai_demo.html`. Ảnh chụp bằng
  Chrome headless.

## 5. Unit test

`tests/test_co_che_demo.py` có 10 test:
- **Ví dụ SN2 công khai** (`server/ai/co_che/vi_du/sn2_nho.json`), luôn chạy:
  - kiểm tra RDKit đạt;
  - bắt được lỗi điện tích và lỗi thiếu mũi tên;
  - `.3d.json` đúng hợp đồng;
  - `.giay.json` không chồng nhãn;
  - nhãn có điện tích;
  - nét đậm/gạch.
- **8 bài**, chỉ chạy khi có thư mục kín, không có thì bỏ qua:
  - qua `kiem_tra_bai`;
  - `.3d.json` đúng hợp đồng;
  - `.giay.json` không chồng nhãn, không nhãn đè nét, không nét cắt;
  - `bai.json` đủ trường, có 3 câu hỏi hợp lệ;
  - bài SN1 và E2 có nhánh sai.

Kết quả chạy:
- `python -m unittest tests.test_co_che_demo`: **10/10 đạt**. Khi `VERA_DU_LIEU_RIENG=/nonexistent`: 7 đạt, 3 bỏ qua.
- `python -m unittest discover -s tests`: 391 test, 77 FAIL và 100 ERROR, **đúng bằng mốc gốc** và đều thuộc 5 tệp hỏng sẵn (test_dang_nhap,
  test_lop_hoc, test_truc_tiep, test_buoi_hoc, test_trinh_chieu). **Không có test hỏng mới.** Số test đạt: 204 → 214.

## 6. Còn lại, ghi chú cho C2/C3

- Sơ đồ propellane còn 75,5 điểm. Phần bị trừ chủ yếu là góc hẹp của khung lều, vốn là kiểu vẽ của đáp án, và hai mũi tên nửa đầu đi sát nhau
  ở bước β-phân cắt.
- Bài este hóa: nét đôi C=O⁺ trong acid đã proton hóa nằm sát nhãn H nên khó thấy nét thứ hai. Cần chỉnh thêm nếu có thời gian.
- Nhãn H mang map (H tham gia mũi tên) được vẽ thành nguyên tử riêng. Nét H–O rất ngắn vì bị hai nhãn che.
- Việc sửa tư duy vẽ chủ yếu nằm trong `ve_theo_mau.py` (lớp `Chat`, bày, nhãn, mũi tên). `tu_duy_ve.py` chỉ sửa phép khớp đối xứng.
  `TuDuy` được gọi khi khung lạ hoặc khi mẫu gây lỗi.
- Không đụng thư mục Unity. Không push.
