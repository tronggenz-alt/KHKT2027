# PROMPT – Nhân vật "Nhà nghiên cứu áo blouse trắng" + bộ chuyển động Mixamo

> Gõ cho Antigravity: **"Đọc và làm theo PROMPT_NHAN_VAT_NHA_NGHIEN_CUU.md, chạy liên tục đến hết."**
> **Đi kèm:** `tools/du_lieu/nhan-vat-nha-nghien-cuu.json` (manifest: nguồn mô hình, 15 chuyển động, stateMap).
> Luật vận hành như các prompt khác: chạy liên tục, commit theo bước, cập nhật `HANDOFF.md`, chưa push/deploy. Bước cần đăng nhập (Sketchfab, Mixamo) thì ghi vào HANDOFF → "VIỆC NGƯỜI DÙNG CẦN LÀM" rồi làm tiếp phần khác, **không dừng**.
> Nhân vật dùng cho web (three.js, thư mục `frontend/`). Nếu dùng trong lab Unity thì import cùng file FBX từ Mixamo, Rig = Humanoid.
> Dữ liệu được thu thập trực tiếp từ Mixamo và Sketchfab ngày 2026-10-25.


> ⚠️ **ĐIỀU CHỈNH 25/10 (ưu tiên cao nhất):** Nhân vật CHỈ nằm trong phòng lab Unity (`unity/PhongLabHoa3D`), là chính nhân vật giáo sư người chơi điều khiển. **KHÔNG làm trợ lý 3D trên trang web.** Phần three.js ở mục 5–6 chỉ để tham khảo tên clip; trong Unity dùng FBX Mixamo, Rig = Humanoid, Animator Controller.

---

## 1. Kết luận khi khảo sát Mixamo

- Mixamo có **108 nhân vật** có sẵn. **Không có nhân vật nào mặc áo blouse trắng (lab coat).**
- Mixamo **không có chức năng thay trang phục**. Nhân vật nào thì trang phục đó.
- Mixamo **có** chức năng *Upload Character*: tải một mô hình người (FBX/OBJ, tư thế T-pose hoặc A-pose) lên, Mixamo tự gắn xương (auto-rig), sau đó áp được mọi chuyển động của Mixamo.

=> Hướng làm: **lấy mô hình nhà nghiên cứu mặc áo blouse trắng từ Sketchfab → đưa vào Mixamo auto-rig → áp các chuyển động bên dưới.**

Nhân vật Mixamo gần nhất (chỉ dùng tạm nếu cần chạy thử ngay):

| Tên | Mixamo ID | Ghi chú |
|---|---|---|
| Chad | 8833e228-8c3e-4a62-863a-9101c3ff053c | Bác sĩ phẫu thuật, áo scrubs xanh, đeo khẩu trang |
| Roth | c5084efc-f1f6-4721-84f9-4e2cb0c7a4a6 | Nữ, áo sơ mi trắng, quần đen |
| Suzie | 503f4a55-0773-461d-b46c-90e5cee9c7d4 | Nữ, áo trắng dài tay, quần đen |
| Olivia | 1b88326e-34c2-4e57-b582-6f8d8017343a | Đồ bảo hộ trắng trùm kín (không phải blouse) |

---

## 2. Nguồn mô hình áo blouse trắng (Sketchfab, tải miễn phí)

Tất cả đều **CC Attribution (CC-BY)**: được dùng cả thương mại, **bắt buộc ghi tên tác giả** trên web (ví dụ ở trang "Giới thiệu" hoặc footer).
Tất cả đều **chưa gắn xương** (cần qua Mixamo).

| # | Tên | Tác giả | Số mặt (faces) | GLB | Link |
|---|---|---|---|---|---|
| A | Modern Scientist in a Lab Coat | restore50 | ~215k | ~22 MB | https://sketchfab.com/3d-models/e835a3b126a4463583727dbac2fedecc |
| B | Casual Scientist In A Lab Coat | restore50 | ~222k | ~24 MB | https://sketchfab.com/3d-models/59db594edf914c62827150c307e162e4 |
| C | Young Scientist in a White Lab Coat | restore50 | ~460k | ~38 MB | https://sketchfab.com/3d-models/fe3aac0053334bd99c0d9e134fd65d45 |
| D | Female_Lab_Coat_Full_Sail_CGG | KnightmareDave | ~158k | ~14 MB | https://sketchfab.com/3d-models/7fe8ef9f5e724b37a4e03ec9c08eb0c2 |

Lưu ý:
- A, B, C được tạo bằng AI (Meshy). Lưới thường dày và không sạch, **phải giảm polygon** trước khi đưa lên web.
- D là mô hình làm tay (Maya/ZBrush), thường sạch hơn.
- Mục tiêu cho web: **≤ 30.000 tam giác**, texture **1024px**, file GLB cuối **≤ 5 MB**.

---

## 3. Danh sách chuyển động đã chọn từ Mixamo

| Key dùng trong code | Mô tả Mixamo | Mixamo ID | Dùng khi | Loop |
|---|---|---|---|---|
| `idle` | Standing Idle | c9c972d1-b96c-11e4-a802-0aaa78deedf9 | Mặc định, đứng chờ | ✔ |
| `idle_breathing` | Breathing Idle | c9c6d0d5-b96c-11e4-a802-0aaa78deedf9 | Đứng chờ nhẹ nhàng | ✔ |
| `talk` | Talking – General Conversation | c9c88301-b96c-11e4-a802-0aaa78deedf9 | Khi AI đang trả lời | ✔ |
| `ask` | Talking – Asking A Question With Two Hands | c9c6fd7b-b96c-11e4-a802-0aaa78deedf9 | Đặt câu hỏi cho học sinh | ✘ |
| `point_back` | Pointing Behind With Thumb | c9c693bf-b96c-11e4-a802-0aaa78deedf9 | Chỉ ra phía sau (vào mô hình) | ✘ |
| `point` | Pointing With Arm Bent | c9c6947c-b96c-11e4-a802-0aaa78deedf9 | Chỉ vào một bộ phận | ✘ |
| `think` | Thinking While Standing | c9c990e8-b96c-11e4-a802-0aaa78deedf9 | Đang xử lý câu hỏi | ✔ |
| `typing` | Sitting At A Computer And Typing | c9ccaf87-b96c-11e4-a802-0aaa78deedf9 | Cảnh ngồi máy tính | ✔ |
| `writing` | Male Writing While Seated | c9c64e6f-b96c-11e4-a802-0aaa78deedf9 | Cảnh ngồi ghi chép | ✔ |
| `search_files` | Male Looking Through A File Cabinet Low | c9ca04b6-b96c-11e4-a802-0aaa78deedf9 | Tra cứu tài liệu | ✘ |
| `wave` | Waving | c9c5ed32-b96c-11e4-a802-0aaa78deedf9 | Chào khi vào trang | ✘ |
| `walk` | Walking Forwards | 4d2adf5a-39fa-4aa1-9272-e07c334bc0a3 | Đi vào khung hình | ✔ |
| `acknowledge` | Acknowledging Gesture | c9c91c6e-b96c-11e4-a802-0aaa78deedf9 | "Đúng rồi" | ✘ |
| `nod` | Thoughtfully Nodding Head Yes | c9c91939-b96c-11e4-a802-0aaa78deedf9 | Gật đầu xác nhận | ✘ |
| `happy` | Happy Forward Hand Gesture | c9c91ba2-b96c-11e4-a802-0aaa78deedf9 | Khen khi trả lời đúng | ✘ |

Tìm trên Mixamo: tab **Animations**, gõ đúng phần "Mô tả Mixamo" vào ô tìm kiếm.

---

## 4. Quy trình dựng (người làm thủ công các bước 1–4)

1. **Tải mô hình** (chọn A/B/C/D) từ Sketchfab, định dạng *Original* hoặc *glTF*.
2. **Blender – dọn mô hình:**
   - Import → kiểm tra đứng thẳng, nhìn trục -Y, đơn vị mét, cao ~1,7 m.
   - Modifier *Decimate* (Collapse) đưa về ≤ 30k tam giác → Apply.
   - Nếu không phải T-pose/A-pose, chỉnh lại tư thế (Mixamo auto-rig cần tay dang ngang hoặc chéo xuống).
   - Gộp mesh, xóa vật thể thừa (bệ đứng, đèn, camera).
   - Export **FBX** (Path Mode: Copy, bật Embed Textures).
3. **Mixamo – auto-rig:** Upload Character → đánh dấu cằm, cổ tay, khuỷu tay, đầu gối, háng → Skeleton LOD: *Standard (65)* → Next.
4. **Mixamo – tải chuyển động** cho từng key ở mục 3:
   - Lần đầu (`idle`): Format **FBX Binary**, Skin **With Skin**, 30 FPS, Keyframe Reduction **none**.
   - Các chuyển động còn lại: Skin **Without Skin** (nhẹ hơn, dùng chung xương).
   - Riêng `walk`: tick **In Place** nếu muốn nhân vật đi tại chỗ và tự di chuyển bằng code.
   - Đặt tên file đúng key: `idle.fbx`, `talk.fbx`, `point.fbx`, …
5. **Gộp thành 1 file GLB** (agent làm được bước này):
   - Cách 1 (Blender script): import `idle.fbx` (with skin) → import từng FBX without skin → gán action vào armature, đổi tên action = key → push vào NLA → export GLB (bật *Animation*, *NLA Strips*, *Draco compression*).
   - Cách 2 (CLI): chuyển từng FBX sang GLB bằng `FBX2glTF`, rồi gộp clip bằng `@gltf-transform/cli` (`gltf-transform merge`), cuối cùng `gltf-transform optimize --compress draco --texture-compress webp`.
6. Đặt file cuối tại: `frontend/models/researcher.glb` (FBX gốc tải từ Mixamo để ở `~/Downloads/MoHinh3D/mixamo/`, **không commit**).
7. Ghi mô hình đã dùng (uid, tác giả, giấy phép, số tam giác trước/sau) vào `TAI_NGUYEN.json`, và thêm dòng ghi công tác giả ở footer web.

---

## 5. Manifest (JSON) cho code đọc

```json
{
  "character": {
    "id": "researcher",
    "file": "models/researcher.glb",
    "displayName": "Nhà nghiên cứu",
    "outfit": "white lab coat",
    "rig": "mixamo",
    "boneNamePrefix": "mixamorig",
    "heightMeters": 1.7,
    "defaultAnimation": "idle",
    "credit": "Model: <tên mô hình> by <tác giả> (Sketchfab, CC BY 4.0). Animations: Adobe Mixamo."
  },
  "animations": {
    "idle":           { "mixamoId": "c9c972d1-b96c-11e4-a802-0aaa78deedf9", "loop": true },
    "idle_breathing": { "mixamoId": "c9c6d0d5-b96c-11e4-a802-0aaa78deedf9", "loop": true },
    "talk":           { "mixamoId": "c9c88301-b96c-11e4-a802-0aaa78deedf9", "loop": true },
    "ask":            { "mixamoId": "c9c6fd7b-b96c-11e4-a802-0aaa78deedf9", "loop": false },
    "point_back":     { "mixamoId": "c9c693bf-b96c-11e4-a802-0aaa78deedf9", "loop": false },
    "point":          { "mixamoId": "c9c6947c-b96c-11e4-a802-0aaa78deedf9", "loop": false },
    "think":          { "mixamoId": "c9c990e8-b96c-11e4-a802-0aaa78deedf9", "loop": true },
    "typing":         { "mixamoId": "c9ccaf87-b96c-11e4-a802-0aaa78deedf9", "loop": true },
    "writing":        { "mixamoId": "c9c64e6f-b96c-11e4-a802-0aaa78deedf9", "loop": true },
    "search_files":   { "mixamoId": "c9ca04b6-b96c-11e4-a802-0aaa78deedf9", "loop": false },
    "wave":           { "mixamoId": "c9c5ed32-b96c-11e4-a802-0aaa78deedf9", "loop": false },
    "walk":           { "mixamoId": "4d2adf5a-39fa-4aa1-9272-e07c334bc0a3", "loop": true, "inPlace": true },
    "acknowledge":    { "mixamoId": "c9c91c6e-b96c-11e4-a802-0aaa78deedf9", "loop": false },
    "nod":            { "mixamoId": "c9c91939-b96c-11e4-a802-0aaa78deedf9", "loop": false },
    "happy":          { "mixamoId": "c9c91ba2-b96c-11e4-a802-0aaa78deedf9", "loop": false }
  },
  "stateMap": {
    "pageEnter":      ["walk", "wave", "idle"],
    "aiThinking":     "think",
    "aiAnswering":    "talk",
    "highlightPart":  "point",
    "correctAnswer":  ["happy", "idle"],
    "confirm":        "nod",
    "askStudent":     "ask"
  }
}
```

---

## 6. Code mẫu three.js (để agent dựng lại)

```js
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { DRACOLoader } from 'three/examples/jsm/loaders/DRACOLoader.js';

export async function loadResearcher(scene, manifest) {
  const draco = new DRACOLoader().setDecoderPath('https://www.gstatic.com/draco/versioned/decoders/1.5.7/');
  const loader = new GLTFLoader().setDRACOLoader(draco);
  const gltf = await loader.loadAsync(manifest.character.file);

  const model = gltf.scene;
  model.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  scene.add(model);

  const mixer = new THREE.AnimationMixer(model);
  const actions = {};
  for (const clip of gltf.animations) {
    const cfg = manifest.animations[clip.name];
    if (!cfg) continue;
    const action = mixer.clipAction(clip);
    if (!cfg.loop) { action.setLoop(THREE.LoopOnce, 1); action.clampWhenFinished = true; }
    actions[clip.name] = action;
  }

  let current = actions[manifest.character.defaultAnimation];
  current?.play();

  function play(name, fade = 0.3) {
    const next = actions[name];
    if (!next || next === current) return;
    next.reset().fadeIn(fade).play();
    current?.fadeOut(fade);
    current = next;
  }

  // Chuỗi động tác: ["walk","wave","idle"]
  async function sequence(names) {
    for (const n of names) {
      play(n);
      const a = actions[n];
      if (a && a.loop === THREE.LoopOnce) {
        await new Promise(res => {
          const done = e => { if (e.action === a) { mixer.removeEventListener('finished', done); res(); } };
          mixer.addEventListener('finished', done);
        });
      }
    }
  }

  return { model, mixer, play, sequence };
}

// Trong vòng lặp render: mixer.update(clock.getDelta());
```

Ghi chú cho agent:
- three.js bỏ dấu `:` trong tên xương Mixamo (`mixamorig:Hips` → `mixamorigHips`).
- Nếu một clip làm nhân vật trôi khỏi vị trí, xóa track `mixamorigHips.position` của clip đó (trừ khi muốn di chuyển thật).
- Mixamo xuất theo đơn vị cm; nếu nhân vật to gấp 100 lần thì `model.scale.setScalar(0.01)`.

---

## 7. Giấy phép
- **Mixamo:** nhân vật và chuyển động được dùng miễn phí trong dự án cá nhân lẫn thương mại; không được bán lại file Mixamo dưới dạng tài nguyên rời.
- **Sketchfab CC-BY:** phải ghi "tên mô hình – tác giả – CC BY 4.0" ở chỗ người xem đọc được.
