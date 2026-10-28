# PROMPT – Nâng cấp Phòng thí nghiệm Hoá ảo thành CĂN PHÒNG 3D KHÔNG GIAN MỞ (có phân khu + nhân vật đi lại)

> Dán nguyên file này cho Antigravity, hoặc gõ: "Đọc và làm theo PROMPT_PHONG_LAB_KHONG_GIAN_MO.md".
> Đây là **Giai đoạn 1b**, làm tiếp trên bản đã xong của `PROMPT_PHONG_THI_NGHIEM_AO.md` (5 thí nghiệm, lõi mô phỏng, công tắc Unity).
> Phạm vi: chỉ **bản Web 3D**. Bản Unity giữ nguyên (xem mục 8).

---

## DÀN Ý NHANH
1. **Hiện tại:** mỗi thí nghiệm là **một cái bàn riêng**. Bấm thí nghiệm khác thì `ve-web.js` xoá cảnh (`_donCanh`) rồi dựng bàn mới. Camera là OrbitControls xoay quanh bàn.
2. **Mục tiêu:** chỉ có **một căn phòng thí nghiệm 3D lớn, không gian mở**, chia thành **5 phân khu** (An toàn · Định lượng · Định tính · Điện hoá · Trung tâm).
   **Cả 5 trạm thí nghiệm cùng có mặt trong phòng.** Người học điều khiển **nhân vật** đi lại, xem từng khu, lại gần bàn rồi nhấn **E** để "vào bàn" làm thí nghiệm, nhấn **Esc** để rời bàn và đi tiếp.
3. **Giữ nguyên:** lõi `tinh-toan.js` và `mo-phong.js` (nguồn sự thật), 5 file dữ liệu, bảng điều khiển, sổ tay, câu hỏi, công tắc Unity, route, 4 lối vào, toàn bộ test cũ.
4. **Làm mới:** `ban-do.json` (sơ đồ phòng) · `ban-do.js` (va chạm + tìm đường, hàm thuần) · `phong-mo.js` (dựng căn phòng) · `nhan-vat.js` (nhân vật + điều khiển + camera theo sau).
   `ve-web.js` được tái cấu trúc thành **nhiều trạm cùng lúc**. Thêm bản đồ nhỏ, joystick cho điện thoại, chế độ "Đi dạo" và "Tại bàn".
5. **Thứ tự làm:** sơ đồ + tìm đường → tách `ve-web.js` thành nhiều trạm → dựng căn phòng → nhân vật + camera → giao diện → hiệu năng + test + bàn giao.

---

## 0. Việc phải làm trước khi viết code
1. Đọc **AGENTS.md**, **CLAUDE.md** (mục "Phòng thí nghiệm Hoá ảo"), phần đầu **HANDOFF.md** và **PROMPT_PHONG_THI_NGHIEM_AO.md** để nắm kiến trúc đang có.
2. Chạy `.venv/bin/python -m pytest -q` và ghi lại số test PASS (lần gần nhất: 590/590). Cuối việc, mọi test cũ vẫn phải PASS.
3. Tạo điểm phục hồi: `git add -A && git commit -m "chore: diem phuc hoi truoc Phong lab khong gian mo"`.
4. Thư mục dự án nằm trong iCloud nên đọc file chậm. **Không** `grep -r` toàn repo (`outputs/` có hơn 1.000 thư mục). Grep đúng file rồi đọc theo khoảng dòng, ví dụ:
   `grep -n "_dungBan\|_donCanh\|mo(id\|this.ct" frontend/hoa-hoc/phong-thi-nghiem/ve-web.js` rồi `sed -n 260,375p ...`.
5. **Không sửa** vùng Lab Sinh học 3D (`frontend/biology-lab-3d*.html`, `frontend/sinh-hoc/`, `backend/sinh_hoc_ai.py`). Vùng này chỉ Claude được ghi.
   Được phép ĐỌC `frontend/biology-lab-3d.html` để tham khảo cách dừng render khi ẩn tab.
6. Không sửa `.env`. Không `git push`, không chạy `dua-len-web.command` khi người dùng chưa đồng ý.
7. **Không thêm thư viện mới** ngoài three@0.160.0 (kể cả `three/addons/...`: `BufferGeometryUtils`, `CSS2DRenderer`, `RoundedBoxGeometry`, `OrbitControls`) và gsap đang dùng.
   Không dùng engine vật lí (cannon, rapier…). Không tải model GLB hay ảnh texture từ ngoài: **mọi thứ dựng procedural**, texture vẽ bằng canvas.

## 1. Những gì đang có (đọc để không phá)
- `frontend/hoa-hoc/phong-thi-nghiem/ve-web.js` – class `PhongLabWeb`:
  - API chung với `cau-unity.js`: `mo(id, tn, s)`, `capNhat(s)`, `hieuUng(e)`, `doiGiaoDien()`, `datChatLuong(q)`, `dieuKhienCamera(lenh)`, `huy()`.
  - `_dungBan()` dựng **một** mặt bàn 2,6 × 1,25 m, mặt bàn ở y = 0. Đơn vị là **mét, kích thước thật** (burette cao khoảng 0,6 m).
  - `mo()` gọi `_donCanh()` (xoá và giải phóng mọi thứ) rồi gọi một trong năm hàm `_dungChuanDo / _dungAnToan / _dungNhanBiet / _dungPin / _dungDienPhan`.
    Các hàm này dựng vào `this.nhom`, đẩy vào `this._hoatHinh`, `this._chon`, `this._vlNhan`, và gán `this.ct.goc`, `this.ct.capNhat`, `this.ct.hieuUng`.
  - `ct.goc = { vi_tri, dich, gan, xa, rong, cao }` là góc nhìn đẹp của từng bàn, tính trong **toạ độ cục bộ** của bàn.
- `giao-dien.js`: `batDau()`, `moThiNghiem(id)`, `U.mp[id]` (mỗi thí nghiệm một máy trạng thái, tạo khi mở lần đầu). Nhịp `requestAnimationFrame` chỉ gọi `U.mp[U.id].capNhat(dt)`.
  Ngoài ra có `window.PhongLab` (dùng cho test), lối chuyển Unity (`moUnity`, `veWeb`) và tham số `?tn=<id>`.
- `mo-phong.js` / `tinh-toan.js`: **KHÔNG sửa logic hoá học**. Chỉ được thêm, ví dụ một hàm `tamDung` nếu thật cần, và phải có test đi kèm.

## 2. Bố cục căn phòng (sơ đồ đề xuất; được tinh chỉnh nhưng phải giữ các ràng buộc)
Toạ độ thế giới: sàn ở **y = 0**; trục **x** từ 0 (Tây) đến 22 (Đông); trục **z** từ 0 (Bắc, tường có cửa sổ lớn) đến 16 (Nam, cửa ra vào). Trần cao 4,2 m.
Mặt bàn thí nghiệm cao **0,9 m**. Mỗi trạm là một `Group` đặt ở `(x, 0.9, z)` và xoay theo `huong`, nên các hàm `_dungX` cũ vẫn dựng quanh gốc y = 0 như trước.

```
z=0   ┌──────────────── CỬA SỔ LỚN (tường Bắc) – ánh sáng ban ngày ────────────────┐
      │ [C] ĐỊNH TÍNH         │ tủ hoá chất │ BẢNG TƯƠNG TÁC │ tủ hoá chất │ [D] ĐIỆN HOÁ   │
      │  tủ hút (tường Tây)   │                                          │  pin-galvani   │
      │  nhan-biet-ion        │       [E] KHU TRUNG TÂM                  │  (bàn D1)      │
      │  giá thuốc thử        │       bàn giáo viên · bảng tuần hoàn     │                │
      │- - - - - - - - - - - -│       lối đi chính rộng ≥ 2,4 m          │- - - - - - - - │
      │ [B] ĐỊNH LƯỢNG        │                                          │  dien-phan     │
      │  chuan-do             │                                          │  (bàn D2)      │
      │  bồn rửa + vòi        ├──────────────────────────────────────────┤  nguồn DC      │
      │                       │ [A] SẢNH + AN TOÀN                       │  bảng tuần hoàn│
      │                       │  bàn "tìm lỗi" an-toan · tường GHS        │  (tường Đông)  │
      │                       │  tủ đồ bảo hộ ▣   ▣ vòi rửa mắt + sen KC  │                │
z=16  └───────────────────────┴──────────────[ CỬA CHÍNH ]──────────────┴────────────────┘
      x=0                    x=7                                      x=15             x=22
```

| Khu | id khu | Hình chữ nhật [x0, z0, x1, z1] | Trạm (tâm bàn x, z) | Màu nhấn (biến CSS) |
|---|---|---|---|---|
| A. Sảnh + An toàn | `an-toan` | [7, 11, 15, 16] | `an-toan` ≈ (9,8; 12,6) | `--ptn-khu-an-toan` (hổ phách) |
| B. Phân tích định lượng | `dinh-luong` | [0, 8, 7, 16] | `chuan-do` ≈ (3,5; 11,5) | `--ptn-khu-dinh-luong` (xanh ngọc) |
| C. Phân tích định tính | `dinh-tinh` | [0, 0, 7, 8] | `nhan-biet-ion` ≈ (3,5; 4,5) | `--ptn-khu-dinh-tinh` (xanh lá) |
| D. Điện hoá | `dien-hoa` | [15, 0, 22, 16] | `pin-galvani` ≈ (18,5; 4,5) · `dien-phan` ≈ (18,5; 10,5) | `--ptn-khu-dien-hoa` (tím) |
| E. Trung tâm | `trung-tam` | [7, 0, 15, 11] | (không có trạm) bàn giáo viên + bảng tương tác | `--ptn-khu-trung-tam` (= `--nhan`) |

Năm biến `--ptn-khu-*` là biến MỚI: khai báo ở cuối `style.css` cho cả `:root` (sáng) và `[data-theme="toi"]` (tối, sáng màu hơn một chút để nổi trên nền navy).
An toàn lấy theo `--ho-phach`, Trung tâm theo `--nhan`. **Không dùng đỏ** (`--gach`) cho khu nào, vì đỏ là màu cảnh báo nguy hiểm.

Ràng buộc bắt buộc:
- **Không gian mở:** giữa các khu KHÔNG có tường kín. Chỉ phân ranh bằng: dải màu trên sàn (màu nhấn của khu, mờ khoảng 12–18 %), vách kính thấp cao 1,1 m ở vài đoạn,
  biển tên khu treo trần, và cụm đèn trần riêng. Đứng ở cửa phải nhìn bao quát được phần lớn căn phòng.
- Lối đi giữa các bàn và đồ nội thất **rộng ≥ 1,6 m**. Lối đi chính từ cửa tới bảng tương tác **rộng ≥ 2,4 m**.
- Mỗi trạm có `diem_dung`: chỗ nhân vật đứng làm thí nghiệm, cách mép bàn khoảng 0,55 m, phía người làm. Có `ban_kinh_tuong_tac` = 1,6 m.
  Phải đi tới được mọi trạm từ điểm xuất phát (test kiểm tra).
- Bàn của mỗi trạm giữ kích thước **2,6 × 1,25 m** vì các hàm `_dungX` cũ bố trí dụng cụ theo khổ này. Phòng dựng chân bàn và tủ dưới bàn; mặt bàn đặt đúng y = 0,9.
- **Điểm xuất phát:** khoảng (11; 15,2), nhìn về phía Bắc.

Đồ nội thất (dựng procedural, gộp hình học, xem mục 6):
- Tủ đồ bảo hộ (áo blouse, kính, găng). Vòi rửa mắt + vòi sen khẩn cấp có biển xanh lá. Bình chữa cháy ở cạnh cửa. Tủ sơ cứu.
  Tường **9 biểu tượng GHS**: dùng lại SVG `veGHS` trong `giao-dien.js`, vẽ ra canvas texture.
- Tủ hút khí độc (cửa kính kéo, đèn bên trong) cạnh khu Định tính. Giá lọ thuốc thử (InstancedMesh). Bồn rửa + vòi nước.
- Tủ kính chứa hoá chất dọc tường Bắc: chai lọ dùng `InstancedMesh`, nhãn màu.
- Bàn giáo viên + **bảng tương tác lớn** (canvas texture): tên phòng, sơ đồ 5 thí nghiệm, tiến độ của người học (đọc từ localStorage như hiện nay). Bảng tuần hoàn treo tường Đông (canvas, tự vẽ).
- Ghế đẩu ở các bàn (InstancedMesh). Cây xanh trong chậu ở góc (low-poly). Đèn trần dạng tấm phát sáng (emissive, không phải đèn thật).
- Cửa sổ lớn tường Bắc: mặt kính + bầu trời gradient (shader nhỏ hoặc canvas). Chế độ tối chuyển thành bầu trời đêm xanh navy và đèn trần sáng hơn.

## 3. Dữ liệu + hàm thuần (làm TRƯỚC, có test)
### 3.1 `frontend/hoa-hoc/phong-thi-nghiem/ban-do.json`
```json
{
  "phien_ban": 1,
  "phong": { "rong": 22, "sau": 16, "cao": 4.2, "cao_ban": 0.9,
             "cua_chinh": { "x": 11, "z": 16, "rong": 2.4 },
             "cua_so": [ { "tuong": "bac", "tu": 1.5, "den": 20.5, "day": 1.0, "dinh": 3.6 } ] },
  "xuat_phat": { "x": 11, "z": 15.2, "huong": 180 },
  "khu": [ { "id": "an-toan", "ten": "Sảnh & Khu An toàn", "mau": "--ptn-khu-an-toan", "hcn": [7, 11, 15, 16],
             "bien": { "x": 11, "z": 12.2, "y": 3.1 }, "mo_ta": "Mặc đồ bảo hộ, học quy tắc an toàn trước khi làm thí nghiệm" } ],
  "tram": [ { "tn": "chuan-do", "khu": "dinh-luong", "tam": [3.5, 11.5], "huong": 0, "ban": [2.6, 1.25],
              "diem_dung": [3.5, 12.7], "ban_kinh_tuong_tac": 1.6 } ],
  "noi_that": [ { "id": "tu-bao-ho", "loai": "tu_bao_ho", "tam": [8.2, 15.5], "kich_thuoc": [1.2, 2.0, 0.6], "huong": 0,
                  "chan": true, "tuong_tac": "mac_bao_ho", "chi_khi_cao": false } ]
}
```
- `huong` tính bằng độ, chỉ nhận **0 / 90 / 180 / 270**. Nhờ vậy mọi vật cản là hình hộp thẳng trục và va chạm đơn giản.
- `chan: true` nghĩa là vật chắn đường đi. `chi_khi_cao: true` nghĩa là đồ trang trí chỉ dựng khi chất lượng **Cao**.
- Điền đủ 5 khu, 5 trạm (`tn` khớp đúng 5 id thí nghiệm) và toàn bộ đồ nội thất ở mục 2.

### 3.2 `frontend/hoa-hoc/phong-thi-nghiem/ban-do.js` (UMD giống `tinh-toan.js`, chạy được cả trình duyệt lẫn Node, KHÔNG dùng DOM hay THREE)
- `taoLuoi(banDo, { o: 0.25, banKinhNguoi: 0.3 })` → lưới chiếm chỗ: ô 0,25 m; tường và mọi vật `chan` được nới thêm bán kính người.
- `timDuong(luoi, tu, den)` → A* 8 hướng (không cắt góc qua vật cản), sau đó **làm mượt** bằng kiểm tra tầm nhìn thẳng (string-pulling).
  Trả về mảng điểm `[x, z]`. Không có đường thì trả `null`.
- `diChuyen(banDo, viTri, buoc, banKinh)` → va chạm hình tròn với các hộp thẳng trục và tường, có **trượt dọc mép**. Không xuyên vật cản khi bước lớn (chia nhỏ bước).
- `tramGanNhat(banDo, viTri)` → `{ tn, khoangCach }` nếu nằm trong `ban_kinh_tuong_tac`, ngược lại `null`.
- `khuTai(banDo, viTri)` → id khu chứa điểm đó.
- `kiemTra(banDo)` → danh sách lỗi: khu chồng nhau, trạm nằm ngoài khu, `diem_dung` trong vật cản, trạm không tới được từ `xuat_phat`, lối đi hẹp hơn 1,6 m.
  Dùng trong test và in cảnh báo ra console khi trang tải.

## 4. Tái cấu trúc `ve-web.js` thành NHIỀU TRẠM (thay đổi ít nhất có thể)
- Mỗi trạm có một ngữ cảnh riêng:
  `this._tram[id] = { goc: Group, nhom: Group, hoatHinh: [], chon: [], vlNhan: [], donDep: [], taiNguyen: Set, ct: { id, tn, s, goc, capNhat, hieuUng } }`.
  `goc` đặt theo `tam`, `cao_ban` và `huong` trong `ban-do.json`.
- Thêm hàm `_trongTram(id, fn)`: tạm trỏ `this.nhom`, `this._hoatHinh`, `this._chon`, `this._vlNhan`, `this._donDep`, `this._taiNguyen`, `this.ct` sang ngữ cảnh của trạm,
  gọi `fn`, rồi trả lại như cũ. Nhờ đó 5 hàm `_dungX` (khoảng 600 dòng) **gần như giữ nguyên**.
- **Lỗi dễ mắc:** trong các closure `ct.capNhat` / `ct.hieuUng` / sự kiện, chỗ nào đọc `this.ct...` lúc CHẠY phải đổi thành biến `ct` bắt lúc DỰNG
  (`const ct = this.ct;` ở đầu mỗi `_dungX`). Ví dụ dòng đổi góc nhìn ngọn lửa trong `_dungNhanBiet`.
  Nếu không đổi, trạm đang không làm sẽ ghi đè lên trạm đang làm. Grep `this.ct` trong file và xử lí từng chỗ.
- Bỏ `_dungBan()` chung (bàn giờ do phòng dựng). `_donCanh()` đổi thành `_donTram(id)` (chỉ dọn một trạm).
- API mới, giữ nguyên các API cũ để `giao-dien.js` và `cau-unity.js` không gãy:
  - `dungPhong(banDo, duLieu, trangThai)`: dựng phòng + 5 trạm, có callback tiến độ để hiện "Đang dựng Khu Điện hoá…".
  - `vaoTram(id)` / `roiTram()`, getter `cheDo` (`"di_dao"` | `"tai_ban"`), `tramDangLam`.
  - Callback `onDenGan(id | null)`, `onVaoTram(id)`, `onRoiTram()`, `onKhu(idKhu)`.
  - `mo(id, tn, s)` giữ nghĩa cũ "mở thí nghiệm id" = `vaoTram(id)` + `capNhat(s)`, KHÔNG dựng lại cảnh.
  - `capNhat(s)` định tuyến theo `s.id` tới đúng trạm (trạm nào cũng nhận được, không chỉ trạm đang làm).
  - `hieuUng(e)` chỉ chạy ở trạm đang làm.
  - `dieuKhienCamera(lenh)`: ở chế độ `tai_ban` giữ y như cũ; ở `di_dao` thì xoay/phóng camera theo sau nhân vật.
- `ct.goc` vẫn ở toạ độ cục bộ. Khi vào bàn, đổi sang toạ độ thế giới bằng `tram.goc.localToWorld(...)`.
- Vòng lặp `_vong`: chỉ cập nhật `hoatHinh` của trạm đang làm và các trạm **trong bán kính 6 m và nằm trong khung nhìn**. Trạm xa đứng yên.
- `datChatLuong(q)`: dựng lại 5 trạm bằng `_trongTram` + dựng lại đồ trang trí `chi_khi_cao`. Không tải lại trang.
- Nhãn CSS2D của dụng cụ **chỉ hiện ở trạm đang làm** (ẩn ở các trạm khác để khỏi rối).

## 5. Căn phòng, nhân vật, camera, điều khiển
### 5.1 `frontend/hoa-hoc/phong-thi-nghiem/phong-mo.js` (ES module) – dựng căn phòng
- Sàn epoxy/vinyl sáng có lưới gạch mờ (canvas texture lặp). Dải màu khu trên sàn đọc từ biến CSS (`getComputedStyle`, giống `bienMauWeb()`), đổi ngay khi chuyển sáng/tối.
- Tường, trần, cửa sổ, cửa chính, vách kính thấp, biển tên khu (canvas texture vẽ bằng font "Be Vietnam Pro" **sau** `document.fonts.ready`).
- Đồ nội thất ở mục 2 dựng từ `noi_that`. Hình học tĩnh **gộp theo vật liệu** bằng `BufferGeometryUtils.mergeGeometries`. Vật lặp lại dùng `InstancedMesh`.
- Ánh sáng:
  - Một `HemisphereLight` + một `DirectionalLight` "nắng qua cửa sổ" có bóng.
  - **Bóng theo tiêu điểm:** khi đi dạo, khung bóng phủ khoảng 10 m quanh nhân vật; khi tại bàn, khung bóng thu về khoảng 1,6 m quanh trạm (như bàn cũ) để bóng dụng cụ sắc nét.
  - Đèn trần chỉ là tấm phát sáng (emissive), không thêm đèn thật.
  - Giả bóng tiếp xúc / AO bằng mặt phẳng gradient mờ dưới chân bàn, tủ và nhân vật (rẻ, dùng cả ở chất lượng Thấp).
- Màu theo web: sáng = nền Icy blue `#eef4fa`, điểm nhấn Midnight Navy `#0f2b48`; tối = nền `#0f2438`, điểm nhấn Sky Blue `#38bdf8`. Đọc từ biến CSS, không gán cứng.
  Tông phòng nhẹ, sạch, hiện đại (tường trắng ngà, mặt bàn phenolic xám đậm hoặc trắng, tủ gỗ sáng). Không dùng màu chói.

### 5.2 `frontend/hoa-hoc/phong-thi-nghiem/nhan-vat.js` (ES module) – nhân vật + điều khiển + camera
- **Nhân vật procedural**, cao khoảng 1,65 m, phong cách tối giản dễ thương hợp giao diện web:
  - Đầu là hình cầu, tóc là chỏm cầu. Thân hình capsule. Tay và chân là capsule có khớp vai, hông, gối để vung khi đi. Có tay cầm.
  - **3 bộ ngoại hình** chọn được (màu tóc/da/áo), lưu localStorage.
  - Ở tủ đồ bảo hộ nhấn E thì nhân vật **mặc áo blouse trắng viền màu nhấn + kính bảo hộ + găng tay** (hiện trên người).
    Vào bàn khi chưa mặc → toast nhắc nhẹ "Nên mặc đồ bảo hộ trước" và ghi vào sổ tay. **Không chặn** (giữ nguyên tắc không khoá bài).
- **Hoạt hình procedural:** chu kì bước theo tốc độ (vung tay chân bằng sin, nhún nhẹ), đứng yên thì "thở". Quay người mượt (nội suy góc). Không dùng file animation.
- **Di chuyển:** đi bộ 1,4 m/s, chạy 3 m/s (giữ Shift). Va chạm qua `ban-do.js`.
  - **Bàn phím:** WASD / mũi tên đi theo hướng camera; Shift chạy; **E** hoặc Enter tương tác; **Esc** rời bàn; **M** phóng to/thu nhỏ bản đồ; **V** đổi góc nhìn.
    **Bỏ qua phím** khi con trỏ đang ở `input`, `textarea`, `select`, `[contenteditable]` hoặc đang mở `dialog`
    (ví dụ ô nhập C_M ở chế độ Ôn thi chuẩn độ). Ở chế độ `tai_ban`, mũi tên quay lại vai trò xoay camera như cũ.
  - **Chuột:** bấm lên sàn → nhân vật tự đi tới đó theo `timDuong`, có vòng đánh dấu đích trên sàn. Bấm vào bàn hoặc biển khu → đi tới `diem_dung` của trạm đó.
    Kéo chuột để xoay camera quanh nhân vật, lăn chuột để phóng to/thu nhỏ (2,5–7 m). Bấm phím di chuyển thì huỷ đường tự đi.
  - **Điện thoại:** joystick ảo góc trái dưới (pointer events, vùng chạm ≥ 44 px, `touch-action: none`). Kéo ở chỗ khác để xoay camera, chụm hai ngón để phóng to/thu nhỏ, chạm sàn để đi tới.
    Khi đứng gần bàn, hiện nút lớn **"Làm thí nghiệm"** ở góc phải dưới.
- **Camera:**
  - **Theo nhân vật (mặc định):** góc người thứ ba sau lưng, cao khoảng 2,6 m, cách khoảng 4,5 m, nhìn vào đầu nhân vật, bám theo mượt.
    Không xuyên tường: kẹp camera trong phòng (x 0,3–21,7; z 0,3–15,7; y < 3,9) và tự kéo gần khi có tủ cao chắn giữa camera và nhân vật.
  - **Toàn cảnh (phím V):** nhìn chéo từ trên cao thấy cả phòng, bấm khu để đi tới.
  - **Tại bàn:** gsap chuyển mượt (khoảng 0,9 s) từ camera theo sau sang `ct.goc` (đã đổi sang toạ độ thế giới) của trạm. OrbitControls chỉ bật ở chế độ này, giới hạn như bàn cũ.
    Nhân vật đứng ở `diem_dung`, quay mặt vào bàn; nếu camera lại quá gần (< 0,8 m) thì nhân vật mờ dần để không che dụng cụ.
  - `prefers-reduced-motion`: không lắc camera, đi tới khu bằng **dịch chuyển tức thì** (mờ đen 300 ms) thay vì đi bộ.
- **Lại gần trạm** (`tramGanNhat` khác null): viền bàn sáng nhẹ (mesh viền hơi phóng to, mặt sau, màu nhấn) và bong bóng CSS2D "**E** – Làm thí nghiệm *Chuẩn độ acid – base*".
- **Vào bàn:** nhân vật tự đi tới `diem_dung` → camera chuyển → `giao-dien.js` gọi `moThiNghiem(id)` → URL có `?tn=id`.
  **Rời bàn** (Esc / nút "← Rời bàn"): camera quay về theo sau, URL bỏ `?tn`, bảng phải về tab "Tham quan".

## 6. Giao diện trang (`phong-thi-nghiem.html`, `giao-dien.js`, CSS `ptn-*` thêm ở CUỐI `style.css`)
- **Cột trái** đổi thành "**Bản đồ phòng**": nhóm theo khu (chấm màu nhấn), trong mỗi khu là các thí nghiệm, mỗi mục có 2 nút:
  - **Đi tới:** nhân vật tự đi tới bàn đó rồi tự vào bàn.
  - **Đến ngay:** dịch chuyển tức thì có mờ đen rồi vào bàn.

  Mục còn hiện trạng thái: "Bạn đang ở đây" / "Đã làm ✓" / huy hiệu Nhập môn. Giữ khối Chế độ (Hướng dẫn/Tự do) và Chất lượng (Cao/Thấp).
  Thêm lựa chọn **Di chuyển: Đi bộ / Đến ngay**. Máy yếu hoặc có `prefers-reduced-motion` thì mặc định "Đến ngay".
- **Thanh công cụ khung 3D:**
  - Chế độ `di_dao`: Theo nhân vật/Toàn cảnh · ＋ · － · "Về cửa".
  - Chế độ `tai_ban`: giữ các nút camera cũ + **"← Rời bàn (Esc)"**. Nút Unity **chỉ hiện ở chế độ `tai_ban`** (xem mục 8).
- **Lớp phủ trong khung:**
  - **Bản đồ nhỏ** góc phải trên: SVG vẽ từ `ban-do.json` (khu tô màu, chấm trạm, mũi tên nhân vật cập nhật khoảng 10 lần/giây). Bấm khu để đi tới. Thu gọn được.
  - Joystick + nút "Làm thí nghiệm" (chỉ trên thiết bị cảm ứng).
  - Bong bóng gợi ý. **Hướng dẫn lần đầu** 3 bước ("Di chuyển → Lại gần bàn → Nhấn E"), có nút tắt, nhớ bằng localStorage.
- **Cột phải:**
  - Ở `di_dao`: tab mới **"Tham quan"** gồm khu đang đứng + mô tả, thí nghiệm gần nhất, bảng phím tắt, tiến độ 5 thí nghiệm, chọn ngoại hình nhân vật.
  - Ở `tai_ban`: 4 tab cũ (Điều khiển · Sổ tay · Câu hỏi · Thông tin) **giữ nguyên**. Điện thoại vẫn dùng tấm kéo lên (bottom sheet).
- **Màn hình tải:** thanh tiến độ "Đang dựng căn phòng… Khu An toàn ✓ · Khu Định lượng…". Tải xong thì mờ dần.
- **Trạng thái và máy trạng thái:**
  - Tạo đủ 5 `U.mp[id]` ngay khi tải, để mỗi bàn hiện đúng trạng thái ban đầu.
  - Sự kiện `trang_thai` của MỌI trạm đều gửi tới `U.web.capNhat(s)`. Bảng điều khiển, sổ tay và hiệu ứng chỉ theo trạm đang làm.
  - Nhịp `capNhat(dt)` chỉ chạy cho trạm đang làm. **Rời bàn thì thí nghiệm tạm dừng** (ví dụ điện phân đang chạy), quay lại thì chạy tiếp.
    Ghi dòng "⏸ Tạm dừng khi rời bàn" vào sổ tay.
- **Liên kết thẳng `?tn=<id>`:** nhân vật xuất hiện ngay ở `diem_dung` của trạm đó, ở chế độ `tai_ban` (không bắt đi bộ), để các lối vào cũ và test cũ vẫn đúng.
  Không có `?tn` thì xuất hiện ở cửa, chế độ `di_dao`.
- **Hook cho test:** mở rộng `window.PhongLab` thêm:
  `phong: { cheDo(), viTri(), khu(), tramGan(), diToi(id, { tucThi }), vaoTram(id), roiTram(), thongKe() }`.
  `thongKe()` trả `renderer.info` gồm draw calls, số tam giác và fps trung bình 2 giây.
- Hiển thị chữ lấy từ JSON bằng `textContent` (không `innerHTML` với dữ liệu). Mọi nút có `aria-label`. Khung 3D có mô tả bàn phím mới trong `aria-label`.

## 7. Hiệu năng (bắt buộc đo, ghi số vào HANDOFF.md)
| | Máy tính – Cao | Điện thoại – Thấp |
|---|---|---|
| fps khi đi dạo | ≥ 50 | ≥ 30 |
| fps khi tại bàn | ≥ 50 (như cũ) | ≥ 30 |
| draw calls khi đi dạo | ≤ 250 | ≤ 150 |
| tam giác | ≤ 600k | ≤ 250k |

- **Thuỷ tinh theo khoảng cách:** `transmission` (MeshPhysical) chỉ bật cho trạm đang làm và trạm trong vòng 4 m. Trạm xa đổi sang thuỷ tinh rẻ
  (MeshStandard trong suốt + envMap) để three.js không phải chạy thêm lượt render truyền qua cho cả phòng. Chất lượng Thấp: không dùng transmission.
- Chất lượng **Thấp**: tắt bóng thật (chỉ giữ bóng giả), pixel ratio 1, bỏ đồ `chi_khi_cao`, giảm số hạt kết tủa/bọt khí, sương mù gần hơn.
- Dùng frustum culling, gộp hình học tĩnh, `InstancedMesh`. Texture canvas ≤ 1024². **Dừng render khi tab ẩn hoặc khung ra khỏi màn hình** (giữ cơ chế cũ).
- Kiểm tra rò bộ nhớ: đổi chất lượng Cao ⇄ Thấp 5 lần, `renderer.info.memory` không tăng mãi.

## 8. Bản Unity (giữ nguyên trong giai đoạn này)
- Unity vẫn là bản **một bàn / một thí nghiệm**. Nút "Web 3D ⇄ Unity 3D" chỉ hiện ở chế độ `tai_ban`, với điều kiện quyền như cũ (`unity_bat && unity_san_sang`, admin luôn thấy).
- Chuyển sang Unity: **tạm dừng** `PhongLabWeb` (`hien(false)`, không huỷ phòng). Quay về Web: trở lại đúng trạm đó ở chế độ `tai_ban`, không mất tiến trình.
- KHÔNG sửa `unity/PhongThiNghiemHoaHoc/` và `cau-unity.js`, trừ khi cần sửa lỗi gọi API. Nâng Unity thành phòng mở sẽ là một prompt riêng sau này.

## 9. Test (bắt buộc)
- `tests/js/phong_thi_nghiem_ban_do.test.mjs` (`node --test`, đọc `ban-do.json` thật):
  - `kiemTra(banDo)` trả về rỗng. Có đủ 5 trạm, `tn` khớp 5 id thí nghiệm, mỗi trạm nằm trong khu của nó.
  - `timDuong` từ `xuat_phat` tới `diem_dung` của **từng** trạm khác `null`; mọi điểm trên đường nằm trong ô trống; các đoạn sau khi làm mượt không cắt qua vật cản.
  - `diChuyen` lao thẳng vào bàn thì bị chặn và trượt dọc mép; bước rất lớn cũng không xuyên tường.
  - `tramGanNhat` đúng ngay tại `diem_dung`, và trả `null` ở giữa lối đi chính. `khuTai` đúng tại tâm mỗi khu.
- `tests/test_phong_thi_nghiem.py` thêm:
  - `ban-do.json` hợp lệ: đủ trường, `huong` ∈ {0, 90, 180, 270}, khu không chồng nhau, mọi `tn` có file trong `thi-nghiem/`.
  - Trang đã đăng nhập chứa importmap `ptn/phong-mo` và `ptn/nhan-vat`, khung bản đồ nhỏ và joystick. Chưa đăng nhập → 303 (test cũ).
  - Test Node có sẵn (`node --test tests/js/`) tự chạy file test mới. Không có `node` thì `skip` như cũ.
- Cuối cùng chạy lại toàn bộ `.venv/bin/python -m pytest -q`: **tất cả PASS** (gồm 590 test cũ).

## 10. Kiểm tra bằng trình duyệt thật trước khi báo xong
- Desktop 1440×900 và điện thoại 390×844, cả Sáng lẫn Tối. Chụp ảnh: toàn cảnh từ cửa, từng khu, nhân vật đang đi, góc Toàn cảnh, từng bàn ở chế độ tại bàn. Lưu vào `outputs/anh-chup-phong-mo/`.
  Console không có lỗi.
- Kịch bản:
  1. Vào trang không có `?tn` → xuất hiện ở cửa → tới tủ bảo hộ nhấn E (nhân vật mặc blouse + kính).
  2. Đi bằng bàn phím tới khu Định lượng → E → chuẩn độ HCl tới khi chuyển hồng, V ≈ 20,0 mL → Esc.
  3. Bấm sàn để tự đi tới khu Định tính → nhận biết đúng 4 ống → thử màu ngọn lửa (góc nhìn đổi đúng).
  4. Dùng "Đến ngay" tới Điện hoá → pin Zn – Cu hiện 1,10 V, rút cầu muối về 0 V.
  5. Điện phân CuSO₄ 2 A, chạy dở thì Esc rời bàn, đi một vòng, quay lại → tiếp tục đến 965 s ra 0,64 g (trạng thái được giữ).
  6. Mở `?tn=dien-phan` → vào thẳng bàn. Điện thoại: joystick, chụm hai ngón, nút "Làm thí nghiệm", bottom sheet đều dùng được.
  7. Nút Unity chỉ hiện khi tại bàn (nếu được phép). Chuyển Unity rồi quay lại Web → vẫn ở đúng bàn.
  8. Gõ chữ "w", "a", "e" trong ô nhập ở bảng điều khiển → nhân vật **không** di chuyển.
- Đo fps + draw calls bằng `PhongLab.phong.thongKe()` và Performance của DevTools ở cả hai chế độ, rồi ghi vào HANDOFF.md.

## 11. Bàn giao
- Commit theo từng phần:
  1. `ban-do.json` + `ban-do.js` + test Node.
  2. Tách `ve-web.js` thành nhiều trạm (chế độ tại bàn trông y như cũ, test cũ PASS).
  3. `phong-mo.js` (căn phòng).
  4. `nhan-vat.js` (nhân vật + camera + điều khiển).
  5. Giao diện (bản đồ, joystick, tab Tham quan, Unity).
  6. Hiệu năng + test + tài liệu.
- Cập nhật **HANDOFF.md** (ĐÃ XONG / ĐANG LÀM / VIỆC TIẾP THEO / LỖI CÒN TỒN / CÁCH CHẠY + bảng fps).
  Sửa đoạn "## Phòng thí nghiệm Hoá ảo" trong **CLAUDE.md**: thêm `ban-do.json`, `ban-do.js`, `phong-mo.js`, `nhan-vat.js`, hai chế độ `di_dao` / `tai_ban`, hook `PhongLab.phong`.
- Báo cáo ngắn: đã làm gì, file nào, cách chạy thử, ảnh chụp, số fps, việc còn lại.

## 12. Điều kiện "XONG"
- [ ] Một căn phòng 3D mở duy nhất, 5 khu phân biệt rõ (màu sàn, biển, đèn, nội thất), không có tường ngăn kín. Đẹp và hợp màu web ở cả sáng lẫn tối.
- [ ] Nhân vật đi lại mượt bằng bàn phím, chuột (bấm sàn), joystick điện thoại; không xuyên bàn/tường; camera không xuyên tường.
- [ ] Lại gần bàn → E → vào bàn (camera chuyển mượt) → làm đủ 5 thí nghiệm như bản cũ → Esc rời bàn. Tiến trình giữ nguyên khi rời và quay lại.
- [ ] Bản đồ nhỏ, danh sách "Đi tới / Đến ngay", tab Tham quan, hướng dẫn lần đầu, tủ đồ bảo hộ đều hoạt động.
- [ ] `?tn=<id>`, 4 lối vào, công tắc và nút Unity vẫn đúng như trước.
- [ ] Đạt bảng hiệu năng mục 7, có số đo ghi trong HANDOFF.md.
- [ ] Toàn bộ test PASS (cũ + mới). HANDOFF.md và CLAUDE.md đã cập nhật. Đã commit. CHƯA push hoặc deploy.
