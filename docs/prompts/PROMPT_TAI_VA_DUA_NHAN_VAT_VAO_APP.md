# PROMPT – Gemini TỰ TẢI chuyển động Mixamo, gắn xương cho nhân vật, rồi đưa nhân vật + chuyển động vào app

> Gõ cho Antigravity: **"Đọc và làm theo PROMPT_TAI_VA_DUA_NHAN_VAT_VAO_APP.md, chạy liên tục đến hết."**
> Bị ngắt thì gõ: **"Làm tiếp PROMPT_TAI_VA_DUA_NHAN_VAT_VAO_APP.md"**. Đọc `HANDOFF.md` + `TIEN_DO_APP_LAB.md` rồi làm tiếp đúng chỗ.
>
> File này **thay phần "chờ người dùng tải"** của `PROMPT_NHAN_VAT_VAO_APP_ANIMATE_UI.md`. Các phần kĩ thuật ở prompt đó vẫn dùng, file này dẫn lại theo số mục:
> - Unity: mục 4.
> - Trợ lý web: mục 5.
> - Animate UI: mục 6.
> - Test: mục 7.
>
> Luật vận hành như mọi prompt: chạy liên tục, commit theo bước, cập nhật `HANDOFF.md` + `TIEN_DO_APP_LAB.md`, tự mở xem mọi ảnh kiểm tra, chưa push/deploy.
> **Không chạy cùng lúc** với `PROMPT_LAB_UNITY_TI_LE_BO_CUC_THAT.md`.

---

## 0. Hiện trạng (Claude kiểm tra 26/10/2026, 19:40)
- **Mô hình đã tải xong:** `~/Downloads/MoHinh3D/modern_scientist_in_a_lab_coat.glb` (22 MB). Đây là **mẫu A**, "Modern Scientist in a Lab Coat" của restore50, giấy phép CC BY 4.0.
  - 2 lưới (`Object_0`, `Object_1`, cùng một vật liệu `material_0`), tổng **214.666 tam giác**.
  - 4 ảnh PNG 2k (khoảng 9,4 MB).
  - **Chưa có xương, chưa có chuyển động.**
  - Trục Y hướng lên. Cao **1,898 m** → phải scale **0,896** để thành 1,70 m. Hai bàn chân ở y = −0,95.
  - **Tay buông sát người**: bề ngang chỉ 0,54 m, không phải T-pose. Mixamo vẫn nhận nhưng xương tay dễ kéo theo vạt áo. Xem cách xử lí ở mục 3.4.
- **File nhân vật** `tools/du_lieu/nhan-vat-nha-nghien-cuu.json` nay có **47 chuyển động**:
  - `animations`: 15 cái, đã có Mixamo ID.
  - `unity_bo_sung`: 6 cái chạy/nhảy.
  - **`mo_rong`: 26 cái mới**: đi lại tự nhiên, thao tác ở bàn, đứng chờ, ngồi, cảm xúc. Mỗi cái có `tu_khoa`, `nhom`, `loop`, `inPlace`, `dung_khi`, `uu_tien`.

  Thêm mục `tai_ve_mixamo` (cách đặt tên file, thiết lập tải) và lời ghi công thật.
- Trong code đã có `BangTrangThaiNhanVat.cs` (bảng ánh xạ sự kiện → động tác). Nhân vật trong lab vẫn là nhân vật tạm `DungNhanVat.cs`.
- Ổ đĩa khoảng **17 GB**. Kiểm `df -h ~` trước mỗi lần tải; **dưới 15 GB thì dừng tải**, làm phần khác.

## 1. Luật khi Gemini tự thao tác trên Mixamo (bắt buộc)
- **Gemini KHÔNG gõ mật khẩu, KHÔNG tạo tài khoản.**
  Mở `https://www.mixamo.com` trong **trình duyệt của Antigravity**. Nếu chưa đăng nhập: báo người dùng **một lần** *"Hãy đăng nhập Mixamo (Adobe) trong cửa sổ trình duyệt này rồi nhắn 'đã đăng nhập'"*, và **trong lúc chờ làm phần khác** (mục 2, mục 6).
- **Thao tác như người dùng, qua giao diện trang:** tải nhân vật lên, đặt điểm khớp, tìm, chọn thiết lập, bấm Download.
  - **Không** lấy/trích token đăng nhập, **không** gọi API ẩn của Mixamo bằng script.
  - Mỗi lần tải nghỉ **3–5 giây**, tải **lần lượt từng file**.
  - Gặp captcha, báo giới hạn hay lỗi tải lặp lại: **dừng phần Mixamo**, ghi số đã tải vào HANDOFF, làm tiếp phần khác, rồi báo người dùng.
- Chỉ dùng tài nguyên Mixamo **bên trong app** (đúng điều khoản Adobe). **Không commit FBX gốc Mixamo**. Chỉ commit GLB và prefab đã nhúng vào sản phẩm.

## 2. Bước A – Chuẩn bị mô hình để đưa lên Mixamo (làm ngay, không cần đăng nhập)
1. Cài công cụ khi thiếu: `brew install assimp`; dùng `npx gltfpack`, `npx @gltf-transform/cli`. Không cần Blender.
2. Từ file gốc tạo **`~/Downloads/MoHinh3D/researcher-60k.glb`**:
   - Gộp 2 lưới thành 1 (`gltf-transform join` + `weld`).
   - Giảm còn **≤ 60.000 tam giác** (`gltfpack -si …`); đây sẽ là mức chi tiết cao nhất cho Unity.
   - Scale **0,896**, đặt gót chân ở y = 0, mặt nhìn về **+Z**.
   - Giữ nguyên UV và ảnh 2k gốc.
3. Xuất **OBJ + MTL + ảnh** (`assimp export … researcher.obj`) và nén thành **`~/Downloads/MoHinh3D/researcher-cho-mixamo.zip`**.
   Mở thử bằng một trình xem (render ảnh nhanh bằng three.js headless hoặc Quick Look) để chắc hình không vỡ. Tự xem ảnh.
4. Giữ riêng **bộ ảnh chất liệu gốc** trong `~/Downloads/MoHinh3D/textures/`. FBX từ Mixamo thường làm mất hoặc giảm chất lượng ảnh, sau này gắn lại bằng UV (Mixamo giữ nguyên UV).

## 3. Bước B – Gắn xương trên Mixamo (Gemini tự thao tác sau khi người dùng đăng nhập)
1. **Upload Character** → chọn `researcher-cho-mixamo.zip` → chờ xử lí → **Next**.
2. **Đặt 5 điểm khớp** ở hình nhìn thẳng (kéo thả): cằm, 2 cổ tay, 2 khuỷu tay, 2 đầu gối, háng.
   Bật **Use Symmetry**. **Skeleton LOD: Standard Skeleton (65)** để có xương ngón tay. → **Next**.
3. **Kiểm tra bản xem trước:**
   - Chọn thử 3 động tác *Waving*, *Reaching Out*, *Walking*.
   - Chụp màn hình rồi **tự xem**: tay có kéo rách hoặc méo vạt áo blouse không, đầu/cổ có gãy không.
4. **Nếu tay/áo méo nặng** (vì mô hình buông tay sát người):
   - Làm lại 1 lần, đặt điểm cổ tay/khuỷu sát mép tay hơn.
   - Vẫn méo thì ghi vào HANDOFF và chọn hướng xử lí:
     - (a) Tạm dùng, và trong Unity **giới hạn biên độ tay** bằng Animation Rigging.
     - (b) Báo người dùng tải **mẫu D** (Female Lab Coat, lưới sạch hơn) theo `tools/du_lieu/nhan-vat-nha-nghien-cuu.json → sketchfab_sources`.

     **Không dừng**: làm tiếp phần tải động tác với nhân vật hiện tại.

## 4. Bước C – Gemini tự tải 47 chuyển động
Đọc `tools/du_lieu/nhan-vat-nha-nghien-cuu.json`: `animations` (15) + `unity_bo_sung` (6) + `mo_rong` (26).

1. **Thứ tự tải:**

   | Lượt | Gồm |
   |---|---|
   | 1 | `idle` |
   | 2 | 14 cái còn lại của `animations` |
   | 3 | `unity_bo_sung` |
   | 4 | `mo_rong` có `uu_tien: 1` |
   | 5 | `mo_rong` có `uu_tien: 2` |
   | 6 | `mo_rong` có `uu_tien: 3` |

2. **Mỗi chuyển động:**
   1. Trong tab **Animations**, tìm bằng `mixamoId` nếu có cách mở thẳng; không có thì gõ `ten` hoặc `tu_khoa`.
   2. Xem bản xem trước, chọn **đúng nghĩa** `dung_khi`. Không có đúng tên thì chọn cái gần nhất.
   3. Ghi tên thật vào `mixamo_ten_thuc` trong file nhân vật.
3. **Thiết lập** (theo `tai_ve_mixamo`):
   - Format **FBX Binary**, **30 fps**, Keyframe Reduction **none**.
   - **`idle` là With Skin**, tất cả còn lại **Without Skin**.
   - Tích **In Place** cho mọi chuyển động có `inPlace: true`.
   - Giữ mặc định Overdrive/Trim, trừ khi động tác bị cắt sai.
4. Tải về rồi **đổi tên thành `<key>.fbx`**, chuyển vào `~/Downloads/MoHinh3D/mixamo/`.
   Kiểm tra file > 50 KB và đúng định dạng FBX. Lỗi thì tải lại 1 lần.
5. Ghi tiến độ ngay vào `TIEN_DO_APP_LAB.md` (đã tải x/47). Bị ngắt thì tải tiếp từ chỗ dừng, không tải lại file đã có.

## 5. Bước D – Đưa nhân vật + chuyển động vào PHÒNG LAB UNITY
Làm theo **mục 4 của `PROMPT_NHAN_VAT_VAO_APP_ANIMATE_UI.md`**, cộng thêm:
1. **Import:**
   - `idle.fbx` (With Skin): Rig **Humanoid**, tạo Avatar. Các clip khác *Copy From Other Avatar*. Tắt Root Motion.
   - **Gắn lại ảnh chất liệu 2k gốc** (URP Lit: baseColor, normal, metallic/roughness) theo UV. So ảnh với bản gốc cho đúng màu.
   - LOD: LOD0 60k, LOD1 30k, LOD2 10k.
   - Prefab `NhaNghienCuu.prefab` đăng kí trong `KhoMoHinh`. Cao đúng **1,70 m**.
2. **Animator đầy đủ:**
   - Di chuyển: blend 2D theo hướng + tốc độ, gồm `idle`, `walk`, `walk_backward`, `strafe_left/right`, `running`, `fast_run`.
   - **Chuyển trạng thái có bước chân thật:** `start_walking` / `stop_walking` khi bắt đầu/dừng; `turn_left_90` / `turn_right_90` khi xoay quá 60° lúc đứng yên.
   - Nhảy: `jump → falling_idle → landing`.
   - Ngồi: `stand_to_sit → sitting_idle / sitting_talking → sit_to_stand` ở ghế đẩu và bàn giáo viên.
   - **Đứng chờ đa dạng:** đứng yên 8–15 giây (ngẫu nhiên) thì chọn ngẫu nhiên `looking_around`, `weight_shift`, `checking_watch`, `stretching`, rồi về `idle`.
3. **Mở rộng `BangTrangThaiNhanVat.cs`** cho các sự kiện mới:

   | Sự kiện | Động tác |
   |---|---|
   | Mở khoá burette / nhỏ thuốc thử | `reaching` |
   | Cầm ống nghiệm | `picking_up` |
   | Đặt dụng cụ | `putting_down` |
   | Bật nguồn DC / đồng hồ đo | `button_push` |
   | Chờ phản ứng | `looking_down` hoặc `checking_watch` |
   | Xuất hiện kết tủa / đổi màu / sủi bọt | `surprised` |
   | Đúng một bước | `thumbs_up` |
   | Hoàn thành thí nghiệm | `clapping` |
   | Sai thao tác / quên đồ bảo hộ | `shaking_head_no` |
   | Trả lời sai ôn thi | `disappointed` → `scratching_head` |

   Có test EditMode cho từng dòng.
4. **4 kỹ thuật làm thật** (gói **Animation Rigging**, có sẵn của Unity):
   - **Multi-Aim** đầu/cổ: nhìn dụng cụ đang dùng; nhìn camera khi `talk` / `ask`; giới hạn góc ±60°.
   - **Two Bone IK** cho tay: khi `reaching` / `picking_up` / `button_push` thì bàn tay tới đúng điểm đích trên dụng cụ (khoá burette, ống nghiệm, nút nguồn). Mỗi dụng cụ có một `Transform` đích.
   - **Foot IK** (Animator IK pass): chân bám sàn, không trượt khi quay hoặc dừng.
   - **Lớp thở cộng thêm** (additive, trọng số nhỏ) chồng lên mọi động tác. Không bao giờ đứng im như tượng.

   Bật/tắt từng kỹ thuật theo mức chất lượng (Thấp tắt Multi-Aim + additive).
5. **Hiệu năng:** giữ ≥ 60 fps (Cao) trên M2 ở bản macOS. Bản WebGL trong app CHEMISTRY tăng ≤ 10 MB, fps tụt ≤ 10%.
   Nén clip *Optimal*, lưới *Medium*.

## 6. Bước E – Đưa nhân vật vào web/app CHEMISTRY (trợ lý góc màn hình)
Làm theo **mục 5 của `PROMPT_NHAN_VAT_VAO_APP_ANIMATE_UI.md`**, cộng thêm:
1. GLB nhân vật **≤ 30k tam giác**, ảnh 1k, Draco/meshopt → `frontend/nhan-vat/researcher.glb` **≤ 5 MB**, kèm 15 clip của `animations`.
2. Chuyển động `mo_rong` chia **theo nhóm** thành file riêng `frontend/nhan-vat/clip-<nhom>.glb` (`cam_xuc`, `cho`, `ban_thi_nghiem`…), mỗi file ≤ 2 MB. **Chỉ tải nhóm khi cần.**
   Trợ lý dùng thêm `thumbs_up`, `clapping`, `surprised`, `shaking_head_no`, `looking_around`.
3. Bộ giải nén Draco/meshopt đặt trong `frontend/vendor/`, chạy offline.

## 7. Bước F – Chuyển động giao diện (Animate UI)
Làm theo **mục 6 của `PROMPT_NHAN_VAT_VAO_APP_ANIMATE_UI.md`**: Motion trong vendor, `frontend/chuyen-dong.js`, bong bóng lời thoại, bảng trượt, toast, audit. Nếu đã làm rồi thì chỉ kiểm tra lại.

## 8. Kiểm chứng (bắt buộc – lần trước ảnh `nhan-vat-canh-ban.png` chỉ là màn xám)
1. **Bảng động tác:** render mỗi chuyển động ở khung giữa thành lưới ảnh `outputs/nhan-vat/bang-dong-tac.png`, có nhãn key.
   **Tự mở xem.** Tiêu chí:
   - không có cái nào kẹt tư thế T;
   - không trôi khỏi chỗ;
   - không méo áo nặng;
   - đúng nghĩa tên.

   Ghi danh sách cái lỗi vào HANDOFF và sửa.
2. **Ảnh trong app**, lưu `outputs/app-chemistry/nhan-vat/`:
   - Lab Unity: đi (góc 3), quay 90°, vào bàn, với tay mở khoá burette (tay chạm đúng khoá), kết tủa xuất hiện (`surprised`), ngồi bàn giáo viên.
   - Trợ lý web: đang nghĩ / đang nói / vỗ tay.

   **Tự mở xem từng ảnh**, mô tả vào HANDOFF. Ảnh trống/xám/không thấy nhân vật thì chưa xong.
3. Test: theo mục 7 của prompt nhân vật, cộng test cho các sự kiện mới và việc "lùi về `idle` khi thiếu clip". `pytest -q`, `node --test tests/js/`, EditMode: PASS hết.
4. Ghi công *"Modern Scientist in a Lab Coat" by restore50, CC BY 4.0, Sketchfab · Animations: Adobe Mixamo* ở trang Giới thiệu của app CHEMISTRY, app Lab và footer web.

## 9. Bàn giao
- Commit theo phần:
  1. Chuẩn bị mô hình.
  2. Unity nhân vật + Animator + Rigging.
  3. Trợ lý web.
  4. Kiểm chứng.

  Đồng bộ Unity bằng `tools/unity_dong_bo.sh day` trước khi commit.
- Build lại app Lab + app CHEMISTRY (`desktop/build-mac.sh`). Ghi dung lượng mới.
- Cập nhật `HANDOFF.md`, `TIEN_DO_APP_LAB.md`, `CLAUDE.md`.
- **Báo cáo ngắn cho người dùng:**
  - đã tải bao nhiêu/47 chuyển động;
  - gắn xương ổn không (tay/áo);
  - ảnh bảng động tác;
  - việc còn lại.

  **Chưa push/deploy.**

## VIỆC NGƯỜI DÙNG CẦN LÀM
1. Khi Gemini nhắn: **đăng nhập Mixamo (tài khoản Adobe, miễn phí)** trong cửa sổ trình duyệt của Antigravity, rồi nhắn "đã đăng nhập". Chỉ cần 1 lần.
2. Nếu Gemini báo mẫu A gắn xương tay xấu: tải **mẫu D** (GLB, ảnh 2k) vào `Downloads/MoHinh3D`.

## ĐIỀU KIỆN XONG
- [ ] Nhân vật mẫu A có xương Mixamo, cao 1,70 m, đúng ảnh chất liệu. Đã tải đủ 47 chuyển động (hoặc ghi rõ cái nào thiếu và lí do).
- [ ] Lab Unity: di chuyển có bước bắt đầu/dừng/quay, ngồi, đứng chờ đa dạng, phản ứng theo sự kiện thí nghiệm. Đầu nhìn theo dụng cụ, tay chạm đúng chỗ, chân bám sàn. fps đạt.
- [ ] Web/app: trợ lý dùng nhân vật thật, tải chuyển động theo nhóm, chạy offline.
- [ ] Bảng động tác và ảnh trong app đã được mở xem, mô tả đúng. Test PASS. Ghi công đủ. FBX gốc không bị commit. Chưa push/deploy.
