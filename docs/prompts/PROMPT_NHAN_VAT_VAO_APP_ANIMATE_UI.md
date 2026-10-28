# PROMPT – Đưa nhân vật "Nhà nghiên cứu áo blouse trắng" vào app + dùng MCP shadcn / Animate UI cho chuyển động giao diện

> Gõ cho Antigravity: **"Đọc và làm theo PROMPT_NHAN_VAT_VAO_APP_ANIMATE_UI.md, chạy liên tục đến hết."**
> Bị ngắt, hoặc người dùng vừa tải xong file nhân vật: gõ **"Làm tiếp PROMPT_NHAN_VAT_VAO_APP_ANIMATE_UI.md"**.
>
> **Đầu vào đã có** (làm sáng 25/10/2026):
> - `PROMPT_NHAN_VAT_NHA_NGHIEN_CUU.md`: nguồn mô hình Sketchfab, 15 chuyển động Mixamo, quy trình, code mẫu three.js.
> - `tools/du_lieu/nhan-vat-nha-nghien-cuu.json`: manifest nhân vật, gồm `character`, 15 `animations` (Mixamo ID, loop) và `stateMap`.
> - MCP **`shadcn`** đã khai báo cho Antigravity trong `~/.gemini/config/mcp_config.json` (`npx -y shadcn@latest mcp`).
> - Skill **`animate-ui`** ở `~/.gemini/antigravity/skills/animate-ui/` (đọc `SKILL.md` + `references/`).
>
> **Không chạy cùng lúc** với `PROMPT_LAB_UNITY_TI_LE_BO_CUC_THAT.md`: cả hai cùng sửa dự án Unity. Prompt kia đang dở thì làm xong nó trước.
> Luật vận hành như mọi prompt: chạy liên tục, commit theo bước, cập nhật `HANDOFF.md` + `TIEN_DO_APP_LAB.md`, tự mở xem mọi ảnh kiểm tra, chưa push/deploy.

---

## 0. HIỂU ĐÚNG HAI CÔNG CỤ (đọc trước khi làm)
| Công cụ | Làm được | KHÔNG làm được |
|---|---|---|
| **Mixamo** (chuyển động trong manifest) | Làm **nhân vật 3D cử động**: đứng, đi, nói, chỉ tay, gật đầu… | Không có sẵn nhân vật mặc blouse (phải auto-rig mô hình Sketchfab) |
| **MCP shadcn + skill Animate UI** (người dùng cài chiều 25/10) | Chuyển động **giao diện**: bong bóng lời thoại của nhân vật bật ra, bảng trượt vào, toast, nút nhấn, chỉ báo tab, chuyển trang. Dùng thư viện **Motion** theo "token" chuyển động của Animate UI | **Không điều khiển xương/hoạt ảnh 3D** của nhân vật, không đọc FBX/GLB |

→ Chuyển động nhân vật 3D lấy từ **clip Mixamo**: three.js `AnimationMixer` trên web, `Animator` trong Unity.
→ Animate UI lo **mọi thứ chuyển động xung quanh nhân vật**, để nhân vật và giao diện ăn khớp nhau.
Ghi rõ cách chia việc này vào HANDOFF và báo lại người dùng.

## 1. Trước khi làm
1. Đọc `AGENTS.md`, `CLAUDE.md`, phần đầu `HANDOFF.md`, `PROMPT_NHAN_VAT_NHA_NGHIEN_CUU.md`, `tools/du_lieu/nhan-vat-nha-nghien-cuu.json`, `~/.gemini/antigravity/skills/animate-ui/SKILL.md`.
2. **Ổ đĩa:** chạy `df -h ~`. Lúc Claude kiểm tra (25/10 15:46) chỉ còn khoảng **15 GB**. Dưới 15 GB thì **không tải gì thêm**, chỉ làm phần code + dự phòng, ghi vào "VIỆC NGƯỜI DÙNG CẦN LÀM".
3. Chạy `pytest -q`, `node --test tests/js/`, EditMode Unity. Ghi lại kết quả. Commit điểm phục hồi `chore: diem phuc hoi truoc nhan vat nha nghien cuu`.
4. **Kiểm tra MCP `shadcn` chạy được trong Antigravity** (bảng MCP báo đã kết nối, gọi thử một công cụ liệt kê/tìm registry).
   - Nếu MCP báo thiếu `components.json`: tạo thư mục **tham chiếu** `tools/animate-ui-tham-chieu/` có `components.json` khai báo registry `@animate-ui`, chỉ để tra cứu. **KHÔNG chuyển web sang React/shadcn.** Web là HTML + JS thuần, giữ nguyên.
   - MCP vẫn lỗi: dùng trực tiếp `SKILL.md` + `references/` của skill (đủ token và recipe). Ghi lỗi vào HANDOFF, **không dừng**.
5. Máy **không có Blender**. Mọi bước xử lí mô hình làm bằng dòng lệnh: `npx gltfpack`, `npx @gltf-transform/cli`, `npx fbx2gltf`, `brew install assimp` nếu cần.
   Tải công cụ, mô hình, file thì kiểm ổ trước.

## 2. Lấy file nhân vật (phần cần người dùng đăng nhập – KHÔNG dừng chờ)
Kiểm tra theo thứ tự, có cái nào làm tiếp cái đó:
1. **Đã có FBX Mixamo** ở `~/Downloads/MoHinh3D/mixamo/`, gồm `idle.fbx` (With Skin) và các clip khác (Without Skin), tên file = key trong manifest → sang mục 3.
2. **Mới có mô hình Sketchfab** (GLB/zip) ở `~/Downloads/MoHinh3D/` (ưu tiên mô hình D "Female_Lab_Coat" vì lưới sạch, hoặc A "Modern Scientist"). Agent chuẩn bị file cho Mixamo:
   - `npx gltfpack -i <vao>.glb -o nhe.glb -si <tỉ lệ> -tc` để giảm lưới ≤ 30k tam giác, texture ≤ 1024 px.
   - Đặt đứng thẳng, cao khoảng 1,7 m, tư thế T/A-pose.
   - Chuyển sang **OBJ + MTL + texture** (`assimp export nhe.glb researcher.obj`), nén thành `~/Downloads/MoHinh3D/researcher-cho-mixamo.zip`.
   - Ghi vào "VIỆC NGƯỜI DÙNG CẦN LÀM": *"Vào mixamo.com → Upload Character → chọn researcher-cho-mixamo.zip → đặt điểm cằm/cổ tay/khuỷu/gối/háng → tải các chuyển động trong `PROMPT_NHAN_VAT_NHA_NGHIEN_CUU.md` mục 3 + 6 clip bổ sung ở mục 4.1 dưới đây vào `~/Downloads/MoHinh3D/mixamo/`."*
3. **Chưa có gì:**
   - Làm hết phần code ở mục 4–6 với **nhân vật dự phòng**: Unity giữ nhân vật dựng bằng code `DungNhanVat.cs`; web hiện một hình đơn giản cùng bộ trạng thái.
   - Ghi danh sách tải vào HANDOFF.
   - Khi file có, chỉ cần chạy "Làm tiếp…" để thay vào. Code đọc theo manifest, không phải sửa lại.

## 3. Chuẩn hoá tài nguyên nhân vật (khi đã có FBX Mixamo)
1. Đối chiếu từng file với `animations` trong manifest. Thiếu file nào thì ghi lại, các trạng thái dùng clip đó **lùi về `idle`**, không lỗi.
2. **Cho web/app (three.js):**
   - Đổi FBX sang GLB (`npx fbx2gltf`), gộp clip (`gltf-transform merge`), đặt **tên clip = key**.
   - Bỏ track `mixamorigHips.position` ở clip tại chỗ (trừ `walk` nếu cần di chuyển thật).
   - Nén: `gltf-transform optimize --compress draco --texture-compress webp`. Ra `frontend/nhan-vat/researcher.glb` ≤ **5 MB**, ≤ 30k tam giác.
3. **Bộ giải nén Draco phải để trong máy** (`frontend/vendor/three/draco/`), **không** dùng `gstatic.com` như code mẫu cũ, để app chạy được khi không có mạng.
   Cập nhật `tools/tai_vendor.py` + `vendor/PHIEN_BAN.json`.
4. Chép manifest vào `frontend/nhan-vat/nhan-vat-nha-nghien-cuu.json` để trang tải được. Bản gốc trong `tools/du_lieu/` vẫn là nguồn chuẩn; có test so khớp nội dung.
5. Ghi `TAI_NGUYEN.json` (uid Sketchfab, tác giả, CC BY 4.0, số tam giác trước/sau) + `GHI_CONG.md`.
   Thêm dòng ghi công ở **Giới thiệu** (app CHEMISTRY và app Lab) và footer web: *"<tên mô hình> by <tác giả>, CC BY 4.0, Sketchfab · Chuyển động: Adobe Mixamo"*.
6. FBX gốc Mixamo **không commit** (điều khoản Mixamo không cho phát tán file rời). Chỉ commit GLB đã nhúng vào sản phẩm và prefab Unity.

## 4. Nhân vật trong PHÒNG LAB UNITY (app Lab macOS + bản WebGL nhúng trong app CHEMISTRY)
1. **Thêm 6 clip cho di chuyển kiểu Genshin** (thêm vào manifest mục `"unity_bo_sung"`): *Running*, *Fast Run*, *Jump*, *Falling Idle*, *Landing*, *Left/Right Turn*.
   Tải Without Skin, 30 fps; tích *In Place* cho Running/Fast Run.
2. **Import:**
   - FBX vào `Assets/TaiNguyenNgoai/NhanVat/` (không commit): Rig **Humanoid**, avatar từ `idle.fbx`, các clip khác *Copy From Other Avatar*.
   - Loop đúng manifest. Tắt Root Motion (code tự di chuyển).
   - Nén clip *Optimal*, mesh compression *Medium*, để bản WebGL không phình (ghi dung lượng build WebGL trước/sau, tăng ≤ 8 MB).
3. **Animator Controller `NhaNghienCuu.controller`:**
   - Layer **Di chuyển:** blend tree theo tốc độ `idle → walk → run → sprint`; nhảy `Jump → Falling → Landing`; quay tại chỗ.
   - Layer **Cử chỉ:** mask thân trên khi đang đi, toàn thân khi đứng yên. Trạng thái theo `stateMap` + sự kiện của lab:

     | Sự kiện trong lab | Clip |
     |---|---|
     | Mở app / vào toà nhà | `wave` |
     | Vào bàn thí nghiệm | `nod` → `idle` |
     | Bấm điểm nóng ①②③ trên dụng cụ | `point` |
     | Đứng trước bảng tuần hoàn, mở bảng | `point_back` |
     | Chờ kết quả (đang chạy điện phân / chờ chuẩn độ) | `think` |
     | Trả lời đúng câu hỏi ôn thi | `happy` → `idle` |
     | Trả lời sai | `think` → `idle` |
     | Hỏi lại học sinh (gợi ý) | `ask` |
     | Bàn giáo viên / máy tính trong phòng | `typing` hoặc `writing` |
     | Kho hoá chất / tủ hồ sơ | `search_files` |

   - Ánh xạ sự kiện → trạng thái viết thành **hàm C# thuần** `BangTrangThaiNhanVat` (đọc từ manifest đã đồng bộ vào `StreamingAssets`) để test được.
4. **Thay nhân vật:**
   - Prefab `NhaNghienCuu.prefab` đăng kí trong `KhoMoHinh` (vai trò `nhan_vat`). Có prefab thì dùng; không có thì **tự lùi về `DungNhanVat.cs`**.
   - Điều khiển (WASD, chuột, Shift, Space, F/V, E/Q) **giữ nguyên**, chỉ thay phần hiển thị và hoạt ảnh.
5. **Chi tiết:**
   - Cao đúng 1,70 m.
   - **Foot IK** (Animator IK pass) để chân bám sàn.
   - Kính bảo hộ và găng (tủ đồ bảo hộ) gắn vào xương `Head` / `LeftHand` / `RightHand` lấy bằng `Animator.GetBoneTransform`.
   - Góc nhìn thứ nhất: thu nhỏ xương `Head` về 0 khi ở góc 1, vì lưới AI là một khối liền, không tách riêng đầu được.
6. **Hiệu năng:** giữ ≥ 60 fps (Cao) trên M2 ở bản macOS. Ghi fps bản WebGL trong app CHEMISTRY trước/sau. Tụt quá 10% thì giảm lưới/LOD.

## 5. Nhân vật "trợ lý" trên các trang web / app CHEMISTRY (three.js)
1. **Module chung** `frontend/nhan-vat/tro-ly.js` (ES module, three r160 từ `vendor`):
   - Tải GLB + manifest, tạo widget nhỏ ở góc phải dưới (khoảng 220 × 300 px), nền trong suốt.
   - API `window.TroLy.trangThai(ten)` theo `stateMap` (`pageEnter`, `aiThinking`, `aiAnswering`, `highlightPart`, `correctAnswer`, `confirm`, `askStudent`) và `window.TroLy.noi(chu)`: hiện **bong bóng lời thoại**.
   - Dùng hàm `play/sequence` theo code mẫu trong `PROMPT_NHAN_VAT_NHA_NGHIEN_CUU.md` mục 6. Đổi clip có fade 0,3 s.
2. **Gắn vào trang** (không sửa logic sẵn có, chỉ gọi thêm):

   | Trang | Sự kiện → trạng thái |
   |---|---|
   | Giải chuỗi | bấm Giải → `aiThinking`; đang hiện dần lời giải → `aiAnswering`; xong → `correctAnswer`; lỗi → `confirm` + lời nhắc |
   | Cơ chế | đang chờ "Giải thích" chạy nền → `aiThinking`; mở giải thích → `aiAnswering` |
   | Phòng thí nghiệm (bản Three.js) | vào trang → `pageEnter`; chọn dụng cụ → `highlightPart`; trả lời câu hỏi đúng → `correctAnswer`, sai → `askStudent` |

   **Lab Sinh 3D** là vùng chỉ Claude ghi: muốn gắn trợ lý thì **viết đề xuất** vào `.agents/hang-doi/sinh-hoc/`, không sửa trực tiếp.
3. **Tôn trọng người dùng và máy:**
   - Có công tắc **"Hiện trợ lý nhà nghiên cứu"** trong Cài đặt (nhớ localStorage). Mặc định bật trên desktop ≥ 1024 px, **tắt trên điện thoại**.
   - Chỉ tải GLB khi widget hiện (lazy).
   - Dừng render khi tab ẩn, widget bị thu gọn, hoặc ra khỏi màn hình.
   - `prefers-reduced-motion` → chỉ đứng `idle`, không chuỗi động tác.
   - Không chặn thao tác trên trang (pointer-events chỉ ở nút thu gọn).
4. **Chưa có GLB** → widget hiện hình dự phòng đơn giản (vẫn có bong bóng lời thoại, trạng thái), không báo lỗi console.

## 6. Dùng MCP shadcn + skill Animate UI cho chuyển động GIAO DIỆN
1. **Đọc `SKILL.md`** của animate-ui để lấy **token chuyển động** (spring, curve, scale, blur, thời lượng) và các **recipe** liên quan.
   Dùng MCP `shadcn` để tra registry `@animate-ui` khi cần xem cách một thành phần chuyển động. **Chỉ dùng làm mẫu**: web là HTML + JS thuần.
2. **Thêm thư viện Motion bản dùng không cần React** (`motion`, UMD/ESM), **đóng gói vào `frontend/vendor/`** (khoá phiên bản chính xác, ghi `PHIEN_BAN.json`) để chạy offline trong app.
   Tạo `frontend/chuyen-dong.js` chứa token theo Animate UI và vài hàm dùng chung: `hienRa(el)`, `anDi(el)`, `nhanNut(el)`, `truotVao(el, huong)`, `chiBaoTab(tab)`.
3. **Áp vào** (đúng chỗ, không rải khắp nơi):
   - Bong bóng lời thoại + widget trợ lý: bật ra bằng spring, thu gọn/mở.
   - Bảng thí nghiệm trượt vào khi vào bàn (Three.js), tấm dưới trên điện thoại.
   - Toast thông báo, hộp "Chưa cài ứng dụng?", nút "Giải thích từng bước" khi nội dung giải thích tới.
   - Chỉ báo tab ở bảng điều khiển.

   Nếu một chỗ đã có chuyển động cũ, **thay bằng token chung** cho đồng bộ; không chồng hai hiệu ứng.
4. **Trong Unity** (app Lab): chép **cùng bộ thời lượng và đường cong** sang USS transitions / tween của UI Toolkit (bảng thí nghiệm, menu, thẻ tên phòng) để cảm giác giống web. Ghi bảng đối chiếu token → giá trị Unity vào README.
5. Chạy lệnh **`audit`** của skill animate-ui trên các file đã sửa. Sửa hết cảnh báo "giật / chậm / sai token". Ghi kết quả vào HANDOFF.
6. **Bắt buộc:**
   - Tôn trọng `prefers-reduced-motion` (tắt chuyển động trang trí, giữ phản hồi trạng thái).
   - Chỉ animate `transform` / `opacity`.
   - Không làm trễ thao tác: hiệu ứng nút ≤ 150 ms; bảng ≤ 350 ms.

## 7. Test + kiểm chứng (bắt buộc)
- **Node** (`tests/js/`):
  - manifest web == manifest gốc;
  - `stateMap` chỉ trỏ tới key có trong `animations`;
  - `TroLy` lùi về `idle` khi thiếu clip;
  - `chuyen-dong.js` tắt hiệu ứng khi reduced-motion.
- **pytest:**
  - trang có importmap/vendor cho Motion + Draco nội bộ (không còn `gstatic.com`);
  - công tắc trợ lý mặc định đúng theo kích thước màn hình (test phần cấu hình);
  - ghi công có trong trang Giới thiệu.
- **Unity EditMode:**
  - `BangTrangThaiNhanVat` ánh xạ đúng mọi sự kiện ở mục 4.3;
  - prefab (nếu có) có Animator Humanoid + đủ state;
  - không có prefab thì lùi về `DungNhanVat`.
- **Nghiệm thu bằng app thật** (mở rộng `desktop/kiem_tra/kiem_tra_app.py` và `kiem_tra_lab_unity.py`). Chụp vào `outputs/app-chemistry/nhan-vat/`:
  1. Trang Giải chuỗi đang giải (trợ lý `think`) và khi xong (`happy`).
  2. Cơ chế mở Giải thích.
  3. Lab Three.js chọn dụng cụ.
  4. Lab Unity: nhân vật đi (góc 3), vào bàn (`nod`), bấm điểm nóng (`point`), trả lời đúng (`happy`).
  5. Điện thoại: trợ lý tắt.
- **Tự mở xem từng ảnh**, ghi vào HANDOFF ảnh đang thấy gì. Tiêu chí:
  - nhân vật đúng tỉ lệ, không méo, không xuyên sàn, có texture;
  - động tác đúng trạng thái;
  - bong bóng không che nội dung chính.
- Mọi test cũ PASS. fps lab không tụt quá 10%. App vẫn chạy offline (tắt mạng: trợ lý vẫn tải được).

## 8. Bàn giao
- Commit theo phần:
  1. Kiểm tra MCP + tài nguyên.
  2. Unity nhân vật.
  3. Trợ lý web.
  4. Chuyển động giao diện Animate UI.
  5. Test + nghiệm thu.
- Build lại app Lab và app CHEMISTRY nếu cần (theo `desktop/build-mac.sh`). Ghi dung lượng mới.
- Cập nhật `HANDOFF.md`, `TIEN_DO_APP_LAB.md`, `CLAUDE.md` (mục mới: `frontend/nhan-vat/`, `chuyen-dong.js`, `BangTrangThaiNhanVat`, MCP shadcn + skill animate-ui dùng cho việc gì).
- **Báo cáo ngắn cho người dùng:**
  - nhân vật đã vào chưa hay đang dùng dự phòng;
  - MCP dùng vào đâu (và vì sao nó không điều khiển nhân vật 3D);
  - việc người dùng cần làm (tải Sketchfab, Mixamo).

  **Chưa push hoặc deploy.**

## VIỆC NGƯỜI DÙNG CẦN LÀM (ghi vào HANDOFF; Gemini không đăng nhập thay)
1. Dọn ổ để còn ≥ 20 GB (đang khoảng 15 GB).
2. Tải mô hình blouse trắng từ Sketchfab (khuyên dùng **D – Female_Lab_Coat** hoặc **A – Modern Scientist**) vào `~/Downloads/MoHinh3D/`.
3. Sau khi Gemini tạo `researcher-cho-mixamo.zip`: lên **mixamo.com** → Upload Character → tải 15 chuyển động (mục 3 của `PROMPT_NHAN_VAT_NHA_NGHIEN_CUU.md`) + 6 clip di chuyển (mục 4.1) vào `~/Downloads/MoHinh3D/mixamo/`, đặt tên file đúng key.
4. Gõ "Làm tiếp PROMPT_NHAN_VAT_VAO_APP_ANIMATE_UI.md – đã có nhân vật".

## ĐIỀU KIỆN XONG
- [ ] Unity: nhân vật nhà nghiên cứu (hoặc dự phòng nếu chưa có file) có Animator đủ di chuyển + cử chỉ theo sự kiện lab. Chạy được cả bản macOS và bản WebGL trong app CHEMISTRY, fps đạt.
- [ ] Web/app: trợ lý góc màn hình theo `stateMap` ở Giải chuỗi, Cơ chế, Lab Three.js. Có công tắc, lazy-load, chạy offline (Draco + Motion nội bộ).
- [ ] Chuyển động giao diện dùng chung token Animate UI (qua MCP shadcn/skill), audit sạch, tôn trọng reduced-motion.
- [ ] Ghi công CC BY + Mixamo đầy đủ. FBX gốc không bị commit.
- [ ] Test + ảnh nghiệm thu đạt và đã được mô tả. HANDOFF/CLAUDE.md cập nhật. Đã commit, **chưa push/deploy**.
