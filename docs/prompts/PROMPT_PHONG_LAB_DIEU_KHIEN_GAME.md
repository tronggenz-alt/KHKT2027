# PROMPT – Phòng lab Hoá 3D: sửa lỗi camera + nội thất giống lab thật + điều khiển kiểu Genshin Impact + "vào phòng" khi tới bàn

> Dán nguyên file này cho Antigravity, hoặc gõ: "Đọc và làm theo PROMPT_PHONG_LAB_DIEU_KHIEN_GAME.md".
> Đây là **Giai đoạn 1c**, làm tiếp trên bản phòng mở đã commit (`a998354`, theo `PROMPT_PHONG_LAB_KHONG_GIAN_MO.md`).
> Phạm vi: chỉ **bản Web 3D**. Không sửa lõi hoá học và bản Unity.

---

## DÀN Ý NHANH
1. **Sửa lỗi gốc trước:** camera đi dạo không nhìn vào nhân vật, góc toàn cảnh trống, "Đến ngay" không vào bàn, khung 3D bị ép nhỏ và bị che.
2. **Rõ ràng hơn, giống phòng lab thật:** bàn thí nghiệm Hoá – Sinh kiểu Hòa Phát, tủ hút kiểu Lý Sơn Sa Kỳ. Mỗi khu thành một **phòng lab con** vách kính trong phòng mở. Góc toàn cảnh kiểu **nhà búp bê** (cắt trần).
3. **Điều khiển kiểu Genshin Impact:** di chuột là xoay camera (khoá con trỏ), WASD đi theo hướng camera, nhân vật xoay mặt theo hướng chạy, Shift chạy nhanh, Space nhảy, lăn chuột thu/phóng. Có **góc nhìn thứ 3** (mặc định) và **góc nhìn thứ nhất**.
4. **Tới bàn là "vào phòng thí nghiệm đó":** camera bay vào cận cảnh, phòng xung quanh tối/mờ đi để dụng cụ nổi bật, bảng thí nghiệm trượt ra. Rời bàn thì camera bay về sau lưng nhân vật.
5. **Bố cục trang kiểu game:** khung 3D toàn màn hình + HUD nhỏ ở các góc. Danh sách thí nghiệm và bảng điều khiển là ngăn kéo/tấm trượt, không còn 2 cột cố định ép khung 3D.
6. **Kiểm chứng có tiêu chí nhìn thấy cụ thể.** Lần trước ảnh chụp "kiểm chứng" không thể hiện đúng nội dung (xem mục 1, L7).

---

## 0. Việc phải làm trước khi viết code
1. Đọc **AGENTS.md**, **CLAUDE.md** (mục "Phòng thí nghiệm Hoá ảo"), phần đầu **HANDOFF.md**, **PROMPT_PHONG_LAB_KHONG_GIAN_MO.md**.
2. Chạy và ghi lại kết quả ban đầu của `.venv/bin/python -m pytest -q` (lần gần nhất 593 PASS) và `node --test tests/js/` (25 PASS). Cuối việc, mọi test cũ vẫn PASS.
3. Tạo điểm phục hồi: `git add -A && git commit -m "chore: diem phuc hoi truoc Phong lab dieu khien game"`.
4. Thư mục dự án nằm trong iCloud: **không** `grep -r` toàn repo. Grep đúng file rồi đọc theo khoảng dòng bằng `sed -n`.
5. Không sửa vùng Lab Sinh học 3D (`frontend/biology-lab-3d*.html`, `frontend/sinh-hoc/`, `backend/sinh_hoc_ai.py`). Không sửa `.env`. Không `git push` hoặc deploy khi chưa được đồng ý.
6. **Không thêm thư viện mới** ngoài three@0.160.0 (gồm `three/addons/...`) và gsap. Không engine vật lí.
   **Không tải model 3D** từ hai trang tham khảo: đó là file 3ds Max (.max), có bản quyền, không dùng được trên web. Chỉ xem ảnh để học hình dáng, rồi tự dựng procedural.
7. `tinh-toan.js`, `mo-phong.js`, 5 file `thi-nghiem/*.json`, `bang-ion.json`: **không sửa logic**. Các hàm dựng dụng cụ `_dungX` trong `ve-web.js` giữ nguyên, chỉ được đổi vị trí/khung nhìn.

## 1. Lỗi đã xác minh trong trình duyệt thật (Claude kiểm tra ngày 24/10/2026)
Kiểm tra trên `http://127.0.0.1:8000/hoa-hoc/phong-thi-nghiem` bằng `window.PhongLab`:

| Mã | Lỗi | Bằng chứng / nguyên nhân |
|---|---|---|
| **L1** | Camera đi dạo **không nhìn vào nhân vật** nên không bao giờ thấy nhân vật | `ve-web.js:230` chỉ gọi `controls.update()` khi `cheDo === "tai_ban"`. `nhan-vat.js` `_capNhatCamera()` (khoảng dòng 707–745) chỉ `lerp` vị trí và `controls.target`, **không `lookAt`**, nên camera giữ nguyên hướng `(0, 0, −1)`. Đo được: nhân vật ở (11; 15,2), camera ở (11; 3,9; 12,85) nhìn về phía Bắc, tức nhân vật nằm **sau lưng camera**. |
| **L2** | Camera đặt **trước mặt** nhân vật, bị kẹp sát trần | `nhan-vat.js:69` `camGocTheta = Math.PI` đặt camera ở phía Bắc nhân vật. Điểm xuất phát z = 15,2 sát tường Nam (z = 16), nên nếu đặt sau lưng thì chỉ còn 0,4 m. Code kẹp từng trục x/z/y riêng (y bị kẹp 3,9) thay vì co "tay đòn". |
| **L3** | Góc **Toàn cảnh** trống trơn | Camera đặt ở (11; 14,5; 21,5): **ngoài phòng** (phòng sâu 16 m, trần 4,2 m) và cũng không `lookAt`. Ảnh `09_desktop_toan_canh_phong.png` toàn màu nền. |
| **L4** | "Đến ngay" không vào bàn | `PhongLab.phong.diToi("chuan-do", { tucThi: true })` dịch chuyển tới chỗ đứng nhưng `cheDo()` vẫn là `"di_dao"`. |
| **L5** | Khung 3D quá nhỏ và bị che | `style.css:2626` `.ptn-bo-cuc { grid-template-columns: 236px … 392px }`: khung 3D chỉ còn khoảng 410×520 px ở cửa sổ 800 px. Bản đồ nhỏ, nút "Rời bàn (Esc)" và thẻ hướng dẫn lần đầu đè lên dụng cụ. Nút "← Rời bàn" vẫn hiện khi đang đi dạo. |
| **L6** | Phòng khó nhìn, không giống lab | Tường/sàn/bàn trắng toát, ít tương phản. Biển khu khổng lồ lơ lửng giữa tầm nhìn (ảnh 01, 04). Bàn và tủ chỉ là khối hộp trắng. |
| **L7** | Ảnh "kiểm chứng" không đúng nội dung | `outputs/anh-chup-phong-mo/`: ảnh 09 trống; ảnh 04 ghi "chuẩn độ tại bàn" nhưng không thấy bàn chuẩn độ; không ảnh nào thấy nhân vật. Lần này mọi ảnh phải qua tiêu chí ở mục 9. |

## 2. Phong cách hình ảnh (tham khảo 2 trang người dùng gửi, chỉ học hình dáng)
- https://noithathoaphat.com/thu-vien-3d/noi-that-phong-thi-nghiem gồm các mẫu BTNHS01, BTNHS02, BTN101, BTN102.
- https://banthinghiemlysonsaky.vn/mo-hinh-3d-thiet-ke-phong-thi-nghiem/ gồm tủ hút khí độc, tủ an toàn sinh học, ảnh cắt lớp nhiều phòng lab.

Dựng procedural bằng `RoundedBoxGeometry` / `BoxGeometry` / `LatheGeometry`, gộp hình học tĩnh, dùng `InstancedMesh` cho vật lặp:
- **Bàn thí nghiệm Hoá – Sinh** (mỗi trạm 1 bàn, khổ vẫn 2,6 × 1,25 m, mặt bàn y = 0,9):
  - Mặt bàn phenolic **xanh lam** có gờ chắn phía sau cao 8 cm.
  - Thân tủ **vân gỗ sáng** (canvas texture sọc gỗ nhẹ), có cánh tủ, ngăn kéo, tay nắm kim loại.
  - Khung thép hộp sơn **xám**, chân có tăng chỉnh.
  - Đầu bàn có **chậu rửa inox + vòi cổ ngỗng**.
  - **Giá thuốc thử 2 tầng** (khung thép + kệ gỗ) ở mép sau. Không đặt lên vùng đặt dụng cụ của `_dungX`.
  - 2–4 **ghế đẩu tròn** chân thép.
- **Bàn tường** chạy dọc tường trong mỗi phòng con: cùng phong cách, có kệ treo, lọ hoá chất (`InstancedMesh`, nắp màu).
- **Tủ hút khí độc** (khu Định tính, có thể thêm ở Điện hoá):
  - Thân xám nhạt, đầu tủ có dải đèn báo.
  - **Cửa kính kéo** trong suốt (vật liệu rẻ, không transmission), lòng tủ có đèn sáng, mặt thao tác xanh đậm, chân tủ là tủ đựng.
- **Tủ cao 2 cánh** đựng hoá chất (xám, nhãn cảnh báo GHS), **tủ an toàn sinh học** (trang trí), **bàn giáo viên + bảng tương tác**, trạm rửa mắt / vòi sen khẩn cấp, bình chữa cháy, tủ đồ bảo hộ (giữ tương tác E).
- **Màu và vật liệu:**
  - Tường trắng ngà / xám rất nhạt có chân tường xám đậm. Sàn vinyl xám lạnh, đường ron mờ. Nội thất xám + gỗ sáng, mặt bàn xanh lam.
  - Mỗi khu có màu nhấn riêng (biến `--ptn-khu-*` đã có) ở viền sàn, khung cửa, biển tên và dải đèn.
  - Không để mảng trắng lớn liền nhau (đó là lí do ảnh hiện tại "trắng toát").
- **Phòng lab con trong phòng mở:**
  - Mỗi khu được bao bằng **vách kính cao 2,4 m có khung nhôm**, không kín tới trần, có **lối vào rộng ≥ 1,6 m**, biển tên trên khung cửa.
  - Trong mỗi phòng con có: bàn chính (trạm), bàn tường + kệ, tủ hút hoặc tủ hoá chất, chậu rửa. Bố trí giống ảnh cắt lớp ở trang Lý Sơn Sa Kỳ.
  - Khu Trung tâm là sảnh/hành lang nối các phòng.
- **Biển tên:** đặt trên khung cửa phòng con hoặc ốp tường, rộng ≤ 1,6 m. **Xoá biển "Khu Trung tâm & Giảng dạy" lơ lửng** và mọi biển chắn giữa tầm nhìn.
  Nhãn CSS2D của trạm chỉ hiện khi đứng trong khoảng 6 m.
- **Ánh sáng:**
  - Đèn panel âm trần theo từng phòng con (emissive) + nắng qua cửa sổ.
  - Bóng giả AO dưới mọi đồ vật. Giảm `toneMappingExposure` nếu cảnh bị cháy sáng.
  - Chế độ tối: buổi tối, đèn trần ấm hơn, cửa sổ xanh navy.
- **Góc Toàn cảnh "nhà búp bê":**
  - Ẩn trần; tường phía gần camera hạ thấp còn 0,4 m hoặc ẩn.
  - Camera nhìn chéo khoảng 50° từ trên xuống, thấy rõ cả 5 phòng con như ảnh banner Lý Sơn Sa Kỳ.
  - Bấm vào một phòng thì nhân vật tự đi tới đó.

## 3. Sửa dữ liệu `ban-do.json` / `ban-do.js`
- Dời `xuat_phat` vào trong phòng, khoảng (11; 13,6), để camera thứ 3 có chỗ phía sau.
- Thêm mảng `phong_con` cho mỗi khu: các đoạn vách kính và `loi_vao` (tâm + rộng). Vách là vật cản đi lại; lối vào thì không.
- Đánh dấu vật **chắn camera** (`chan_camera: true`): tường, vách kính, tủ cao. Bàn thấp thì không. Dùng cho "tay đòn" camera (mục 4).
- Cập nhật `kiemTra()`: mọi trạm vẫn tới được từ điểm xuất phát, đi qua lối vào phòng con; lối vào rộng ≥ 1,6 m.

## 4. Điều khiển + camera kiểu Genshin Impact (viết lại phần camera/điều khiển trong `nhan-vat.js`)
Tách phần toán thuần ra file mới `frontend/hoa-hoc/phong-thi-nghiem/dieu-khien.js` (UMD như `ban-do.js`, không DOM, không THREE) để test bằng Node:
`viTriCamera(yaw, pitch, khoangCach, diemNhin)`, `huongDiChuyen(phim, yaw)`, `tayDon(diemNhin, viTriMuon, hopChan[])` (trả khoảng cách sau khi co), `quayMuot(gocHienTai, gocDich, tocDo, dt)`.

- **Camera rig góc nhìn thứ 3 (mặc định):**
  - Quay quanh **điểm nhìn ở vai nhân vật**: cao 1,45 m, lệch sang phải 0,3 m.
  - `yaw` tự do 360°, `pitch` từ −35° đến +60°, khoảng cách 1,8–6 m (lăn chuột, có quán tính nhẹ).
  - **Mỗi khung** tính vị trí rồi gọi `camera.lookAt(diemNhin)`.
  - Khi đi dạo **tắt `controls.enabled`**. OrbitControls chỉ dùng ở chế độ tại bàn. Sửa L1 ở `ve-web.js:230` cho khớp.
  - **Tay đòn chống xuyên tường:** tia từ điểm nhìn tới vị trí camera mong muốn, cắt các hộp `chan_camera` → co còn (khoảng trúng − 0,2 m) ngay lập tức, giãn lại từ từ (khoảng 0,25 s) khi hết vật chắn.
    Bỏ cách kẹp từng trục x/z/y hiện tại. Camera < 0,9 m thì nhân vật mờ dần (opacity 1 → 0,25).
  - Khởi tạo: camera **sau lưng** nhân vật, nhìn theo hướng nhân vật quay mặt (sửa L2).
- **Chuột kiểu game (desktop):**
  - Bấm vào khung 3D → `requestPointerLock()`. Khi đã khoá: `movementX/Y` xoay `yaw/pitch`, ẩn con trỏ, hiện chấm tâm nhỏ giữa khung.
  - **Esc** (trình duyệt tự nhả khoá) hoặc **giữ Alt** → hiện con trỏ để bấm giao diện, nhả Alt thì khoá lại.
  - Có thẻ nhỏ "Bấm vào khung để điều khiển · Alt để hiện chuột".
  - Pointer Lock bị từ chối hoặc không hỗ trợ → tự chuyển sang **giữ chuột phải (hoặc kéo chuột trái) để xoay**.
  - **Không dùng Esc để rời bàn khi đang khoá chuột** (trình duyệt chiếm phím này). Rời bàn: **Q**, hoặc Esc khi đang tại bàn (lúc đó không khoá chuột).
- **Di chuyển:**
  - WASD/mũi tên theo **hướng camera chiếu ngang** (W luôn đi "vào màn hình").
  - Nhân vật **quay mặt mượt** theo hướng chạy (khoảng 10 rad/s, không giật 180°).
  - Có gia tốc (8 m/s²) và phanh (12 m/s²). Đi 1,6 m/s; **giữ Shift chạy 3,4 m/s**; Ctrl đi chậm.
  - **Space nhảy:** cao 0,5 m, trọng lực 18 m/s², chỉ là hiệu ứng (va chạm vẫn 2D), có nhún khi tiếp đất.
  - Đi lâu mà không xoay chuột thì camera **tự xoay dần về sau lưng** sau 1,5 s (tắt được).
  - Bấm chuột trái lên sàn vẫn đi tới bằng tìm đường, nhưng **chỉ khi không khoá chuột**.
- **Hoạt hình:** chu kì bước khớp tốc độ thật (chân không trượt), nghiêng người nhẹ khi rẽ, tay vung theo bước, đứng yên thì thở. Chạy nhanh thì người đổ về trước.
- **Góc nhìn thứ nhất:** phím **F**, hoặc lăn chuột vào hết mức.
  - Camera ở mắt (1,55 m), `pitch` từ −80° đến +80°, ẩn đầu/thân nhân vật. Nên có hai tay/găng thấp ở mép dưới khung nếu đẹp, không bắt buộc.
  - Chuyển 1 ⇄ 3 mượt khoảng 0,35 s.
- **Toàn cảnh:** phím **V** (kiểu nhà búp bê, mục 2). V lần nữa thì quay về góc đang dùng trước đó.
- **Điện thoại:**
  - Joystick trái để đi. **Kéo nửa phải màn hình để xoay camera** (như game mobile), chụm hai ngón để thu/phóng.
  - Các nút tròn: Nhảy, Đổi góc nhìn, **Vào bàn** (lớn, hiện khi gần bàn).
- **Cài đặt "Điều khiển"** (lưu localStorage): độ nhạy chuột 0,5–2×, đảo trục Y, tự xoay camera bật/tắt, góc nhìn mặc định 3/1, kiểu chuột (Khoá con trỏ / Kéo để xoay), tự vào bàn bật/tắt.
- Giữ quy tắc cũ: **bỏ qua phím** khi đang gõ trong `input`, `textarea`, `select`, `[contenteditable]` hoặc khi `dialog` đang mở.

## 5. Tới bàn = "đưa vào phòng thí nghiệm đó"
- **Bước qua cửa phòng con:**
  - Hiện thẻ tên kiểu game ở giữa trên khung, khoảng 1,6 s, mờ dần: "📍 Khu Phân tích định lượng · Chuẩn độ acid – base".
  - Đèn phòng con sáng dần, mini-map tô đậm phòng đó.
- **Vào bàn:**
  - Đứng trong vùng 1,3 m trước bàn và **dừng chân 0,6 s** thì tự vào bàn. Có vòng tròn đếm ngược quanh nhãn "E".
  - Nhấn **E** để vào ngay. Tắt được "tự vào bàn" trong Cài đặt.
  - "Đến ngay" và "Đi tới" trong danh sách đều phải kết thúc ở chế độ **tại bàn** (sửa L4).
- **Chuyển cảnh vào:**
  1. Nhả khoá chuột.
  2. Nhân vật đi tới `diem_dung`, quay mặt vào bàn.
  3. Camera **bay theo đường cong qua vai nhân vật** tới khung cận cảnh của trạm (gsap 1,0 s, `power2.inOut`).
  4. Đồng thời **phòng xung quanh tối/mờ đi**: giảm sáng mọi thứ ngoài trạm khoảng 35%, sương mù gần lại. Vách kính phòng con hơi đục, dụng cụ trên bàn nổi bật.
  5. Nhân vật mờ đi nếu chắn dụng cụ.
- **Khung cận cảnh:**
  - Tính lại từ **bounding box nhóm dụng cụ** của trạm (dùng `_viTriVua` sẵn có), sao cho bàn chiếm 75–85% bề ngang khung.
  - Không cắt đỉnh burette hay giá đỡ. Khung nhìn bám theo kích thước thật của khung 3D sau bố cục mới.
- **Giao diện khi tại bàn:**
  - Khung 3D vẫn toàn màn hình. **Bảng thí nghiệm trượt vào từ phải**: desktop rộng 380 px, nền mờ `backdrop-filter`, khung 3D tự lệch tâm (`setViewOffset`) để bàn không bị bảng che. Điện thoại dùng bottom sheet.
  - Ẩn bản đồ nhỏ và HUD đi dạo. Nút **"← Rời bàn (Q)"** ở góc trái trên khung 3D. Các nút camera cũ gom vào cụm nhỏ góc phải dưới.
  - **Điểm nóng đánh số** (①②③…) trên dụng cụ chính, giống viewer 3D của Lý Sơn Sa Kỳ. Bấm để chọn/xem tên, dùng `_chon` và `onChon` sẵn có. Chỉ hiện ở trạm đang làm.
- **Rời bàn:** camera bay ngược về sau lưng nhân vật (0,8 s), nhân vật lùi 0,6 m, phòng sáng lại, bảng trượt ra. Bấm vào khung để khoá chuột lại.
- Giữ nguyên các phần sau:
  - `?tn=<id>` mở thẳng vào bàn.
  - Tiến trình được giữ khi rời bàn (tạm dừng, quay lại chạy tiếp).
  - Nút Unity chỉ hiện ở chế độ tại bàn.

## 6. Bố cục trang kiểu game (`phong-thi-nghiem.html`, `giao-dien.js`, CSS `ptn-*` thêm ở CUỐI `style.css`)
- **Bỏ lưới 3 cột** `.ptn-bo-cuc` (mục L5). Khung 3D chiếm **toàn bộ** vùng dưới header (desktop và điện thoại).
- **HUD khi đi dạo** (chữ nhỏ, nền mờ, bo góc, theo màu web sáng/tối):
  - Trái trên: tên khu hiện tại + nút **☰ Bản đồ phòng**, mở **ngăn kéo trái** chứa danh sách cũ "Đi tới / Đến ngay" và tiến độ 5 thí nghiệm.
  - Phải trên: **bản đồ nhỏ** khoảng 160 px. Bấm để phóng to giữa màn hình (phím M).
  - Giữa dưới: gợi ý ngữ cảnh "**E** – Vào bàn *Chuẩn độ acid – base*" / "**E** – Mặc đồ bảo hộ".
  - Trái dưới: phím tắt thu gọn "WASD · Chuột · Shift · Space · F · V · ?". Phím **?** mở bảng đầy đủ.
  - Phải dưới: nút đổi góc nhìn (3 / 1 / Toàn cảnh) + ⚙ Cài đặt (gồm cả mục Nhân vật & ngoại hình của tab Tham quan cũ).
- **Hướng dẫn lần đầu:** thẻ nhỏ góc dưới, **không che giữa màn hình**. Tự ẩn sau khi người dùng đã đi và xoay camera, nhớ bằng localStorage.
- **Điện thoại:** khung 3D toàn màn hình, HUD tối giản, danh sách thí nghiệm trong ngăn kéo, bảng thí nghiệm là bottom sheet như cũ.
- Giữ chuẩn cũ: chữ lấy từ dữ liệu đưa vào bằng `textContent`, mọi nút có `aria-label`, focus nhìn thấy được, `prefers-reduced-motion` thì thay bay camera bằng chuyển mờ.

## 7. Hiệu năng
- Giữ **≥ 50 fps** desktop chất lượng Cao và **≥ 30 fps** điện thoại chất lượng Thấp, ở cả đi dạo, toàn cảnh và tại bàn.
- Tay đòn camera: tối đa 1 lần kiểm tra tia mỗi khung, với **hộp AABB đơn giản** từ `ban-do.json`. Không raycast cả scene.
- Vách kính, cửa tủ hút: vật liệu trong suốt rẻ (không `transmission`). `transmission` chỉ cho dụng cụ ở trạm đang làm hoặc trạm gần (như cũ).
- Ghế, lọ, chai, tay nắm: `InstancedMesh`. Hình học tĩnh gộp theo vật liệu. Texture canvas ≤ 1024².
- Ghi số đo vào HANDOFF.md cho cả ba chế độ, kèm draw calls và số tam giác.

## 8. Test tự động
- `tests/js/phong_thi_nghiem_dieu_khien.test.mjs` cho `dieu-khien.js`:
  - Với mọi `yaw`, `huongDiChuyen({W})` cùng hướng với hướng camera chiếu ngang (tích vô hướng > 0,99); A/D vuông góc.
  - `viTriCamera` nằm **sau** điểm nhìn theo hướng `yaw`.
  - `tayDon` co lại khi có hộp chắn giữa, giữ nguyên khi không có.
  - `quayMuot` không vượt quá góc đích và đi đường ngắn nhất qua ±π.
- Cập nhật `tests/js/phong_thi_nghiem_ban_do.test.mjs`: lối vào phòng con đủ rộng, mọi trạm tới được qua lối vào, điểm xuất phát mới hợp lệ.
- `tests/test_phong_thi_nghiem.py`: trang có importmap `ptn/dieu-khien`, có HUD và ngăn kéo bản đồ. Test cũ vẫn PASS.
- Mở rộng hook `window.PhongLab.phong`:
  - `camera()` → `{ vi_tri, huong, che_do, thay_nhan_vat }`. `thay_nhan_vat` = chiếu đầu nhân vật ra NDC, nằm trong [−0,9; 0,9] và ở phía trước camera.
  - `tramTrongKhung(id)` → tỉ lệ bề ngang bàn trên màn hình, và phần tử ở tâm bàn theo `document.elementFromPoint` có phải canvas không.
- Cuối cùng: toàn bộ `pytest -q` và `node --test tests/js/` PASS.

## 9. Kiểm chứng trong trình duyệt thật – có tiêu chí NHÌN THẤY (bắt buộc)
Viết `outputs/kiem_tra_phong_lab_game.py` (Playwright Chrome thật, desktop 1440×900 và điện thoại 390×844, cả Sáng và Tối). **Script phải `assert`** các điều sau, không chỉ chụp ảnh:
1. Vừa tải trang: `camera().thay_nhan_vat === true`, camera ở sau lưng nhân vật.
2. Giữ W 2 s: nhân vật đi đúng hướng camera đang nhìn (sai lệch < 10°). Xoay camera 90° (kéo chuột ở chế độ dự phòng, vì headless không khoá được con trỏ) rồi giữ W: hướng đi đổi theo.
3. Đi sát tường / tủ cao rồi xoay camera một vòng: `thay_nhan_vat` luôn đúng, camera không ra ngoài phòng.
4. Phím V: thấy ít nhất 4/5 bàn trong khung (chiếu bounding box ra NDC). Ảnh không được là một màu nền.
5. Với **từng** trạm: "Đi tới" → tự vào bàn, `cheDo() === "tai_ban"`, `tramTrongKhung(id).ti_le` trong 0,75–0,85, phần tử ở tâm bàn **là canvas** (không bị HUD/bảng che).
   Làm lại các kịch bản thí nghiệm cũ: chuẩn độ HCl chuyển hồng ở V ≈ 20,0 mL; nhận biết đúng 4 ống; pin Zn – Cu 1,10 V, rút cầu muối về 0 V; điện phân CuSO₄ 2 A 965 s ra 0,64 g.
6. Q rời bàn: camera về sau lưng, `thay_nhan_vat === true`.
7. Góc nhìn thứ nhất (F): thân nhân vật bị ẩn, đi WASD được.
8. Gõ "w", "e", "q" trong ô nhập ở bảng điều khiển: nhân vật không đi, không rời bàn.
- Chụp ảnh vào `outputs/anh-chup-phong-lab-game/`:
  - đi dạo góc 3 (thấy nhân vật + phòng con), góc 1, toàn cảnh nhà búp bê, bước qua cửa (thẻ tên phòng);
  - tại bàn của **mỗi** trạm (thấy rõ dụng cụ, bảng trượt bên phải không che bàn);
  - điện thoại đi dạo + tại bàn.
- **Tự mở từng ảnh ra xem.** Ghi vào HANDOFF.md một dòng mô tả thật mỗi ảnh đang thấy gì. Ảnh nào không thấy đúng thứ cần thấy thì phần đó **chưa xong**.
- Console không có lỗi.

## 10. Bàn giao
- Commit theo từng phần:
  1. Sửa lỗi camera L1–L4 + `dieu-khien.js` + test.
  2. Bố cục kiểu game + HUD.
  3. Nội thất + phòng con + toàn cảnh nhà búp bê.
  4. Chuyển cảnh vào/rời bàn + điểm nóng.
  5. Kiểm chứng + tài liệu.
- Cập nhật **HANDOFF.md** (ĐÃ XONG / ĐANG LÀM / VIỆC TIẾP THEO / LỖI CÒN TỒN / CÁCH CHẠY + bảng fps + mô tả từng ảnh).
  Sửa đoạn "## Phòng thí nghiệm Hoá ảo" trong **CLAUDE.md**: `dieu-khien.js`, phím tắt mới, các chế độ camera, hook `camera()` / `tramTrongKhung()`.
- Báo cáo ngắn: đã sửa gì, file nào, cách chạy thử, ảnh chụp, số fps, việc còn lại. **Chưa push hoặc deploy.**

## 11. Điều kiện "XONG"
- [ ] Mở trang là thấy nhân vật đứng giữa phòng, camera sau lưng. Lỗi L1–L5 hết, có test và assert chứng minh.
- [ ] Phòng trông giống lab thật: bàn xanh lam + tủ gỗ + khung thép, tủ hút, tủ hoá chất, chậu rửa. 5 phòng con vách kính rõ ràng, không còn biển khổng lồ chắn tầm nhìn.
- [ ] Di chuột (khoá con trỏ) là xoay camera như Genshin. WASD theo hướng camera, Shift chạy, Space nhảy, lăn chuột thu/phóng, F đổi góc 1/3, V toàn cảnh. Điện thoại kéo nửa phải để xoay.
- [ ] Tới bàn (đứng 0,6 s hoặc nhấn E) là bay vào cận cảnh, phòng xung quanh mờ đi, bảng trượt ra không che bàn. Q/Esc rời bàn mượt. Làm đủ 5 thí nghiệm như trước.
- [ ] Bố cục toàn màn hình kiểu game, HUD gọn, chạy đúng cả sáng/tối, desktop/điện thoại.
- [ ] Đạt hiệu năng mục 7. Script mục 9 PASS hết. Ảnh đã được mở xem và mô tả đúng.
- [ ] Toàn bộ test PASS. HANDOFF.md và CLAUDE.md đã cập nhật. Đã commit. CHƯA push hoặc deploy.
