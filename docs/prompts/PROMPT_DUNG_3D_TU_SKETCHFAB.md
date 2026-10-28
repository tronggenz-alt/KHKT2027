# PROMPT – Dựng phòng lab 3D chân thật từ kho mô hình Sketchfab đã chọn

> Gõ cho Antigravity: **"Đọc và làm theo PROMPT_DUNG_3D_TU_SKETCHFAB.md, chạy liên tục đến hết."**
> **Đi kèm 2 file** (Claude chuẩn bị 24/10/2026):
> - `tools/du_lieu/mo-hinh-sketchfab.json`: 34 mô hình, mỗi mô hình có `uid`, `url`, `giay_phep`, `so_mat`, `tac_gia`, `vai_tro` (thay vật nào trong app), `dung_cho` (đặt ở đâu).
> - `tools/du_lieu/kho-mo-hinh-3d.html`: trang có ảnh để xem nhanh. Mở bằng trình duyệt.
>
> File này **bổ sung cho GĐ9–GĐ11** của `PROMPT_LAB_UNITY_TRON_GOI.md`. Đang dở giai đoạn nào thì cứ làm tiếp. Khi tới phần nội thất, dụng cụ và nhân vật thì dùng mô hình thật theo file này thay cho hình khối dựng bằng code.
> Luật vận hành vẫn như prompt trọn gói: chạy liên tục, commit theo bước, cập nhật `TIEN_DO_APP_LAB.md` + `HANDOFF.md`, tự mở xem mọi ảnh kiểm tra, chưa push/deploy.

## 0. Trước khi làm
1. Đọc `tools/du_lieu/mo-hinh-sketchfab.json`. Chỉ dùng mô hình có `vai_tro` khác `tham_khao` và `loai`.
2. **Giấy phép:**
   - Chỉ import `CC Attribution` hoặc `CC0`.
   - `NonCommercial`: **không dùng**.
   - `Free Standard`: đọc điều khoản trên trang trước; không rõ thì bỏ.
3. **Ổ đĩa:** chạy `df -h ~`. Dưới 15 GB thì không tải; dùng bản dựng bằng code và ghi vào HANDOFF.
   Tổng mô hình tải về ≤ 1,5 GB. Để trong `~/UnityProjects/PhongLabHoa3D/Assets/TaiNguyenNgoai/Sketchfab/` (**không commit**).

## 1. Lấy file mô hình (Sketchfab bắt đăng nhập mới cho tải)
Thử theo thứ tự:
1. **Người dùng đã tải tay** vào `~/Downloads/MoHinh3D/` (mỗi mô hình một file zip/glb, tên tuỳ ý). Đối chiếu từng file với `uid`/`ten` trong JSON rồi dùng.
2. **Có API token Sketchfab** ở `~/.sketchfab_token`: người dùng lấy tại sketchfab.com → Settings → Password & API; Gemini **không tự đăng nhập**.
   - Gọi `GET https://api.sketchfab.com/v3/models/{uid}/download` với header `Authorization: Token <token>`.
   - Tải bản **glTF** (ưu tiên `glb`), giải nén vào thư mục mô hình.
   - Không in token ra log, không commit token.
3. **Chưa có cả hai:**
   - Giữ hình dựng bằng code đang có.
   - Viết danh sách cần tải vào HANDOFF → "VIỆC NGƯỜI DÙNG CẦN LÀM": link + định dạng glTF; xếp theo ưu tiên ở mục 3.
   - Làm tiếp phần khác, **không dừng**.

Mỗi mô hình đã tải ghi vào `unity/PhongLabHoa3D/TAI_NGUYEN.json` (uid, url, giấy phép, tác giả, sha256, số tam giác trước/sau khi giảm).

## 2. Quy trình đưa một mô hình vào Unity (làm giống nhau cho mọi mô hình)
1. **Import glTF:** thêm gói `com.unity.cloud.gltfast` (glTFast). Import vào `Assets/TaiNguyenNgoai/Sketchfab/<vai_tro>/<uid>/`.
2. **Giảm lưới** cho mô hình vượt ngân sách. Ngân sách số tam giác:

   | Loại | Tối đa |
   |---|---|
   | Dụng cụ nhỏ | 5k |
   | Bàn, tủ | 20k |
   | Tủ hút, nguồn DC | 30k |
   | Nhân vật | 60k |

   Cách giảm: `gltfpack -si <tỉ lệ>` (cài `meshoptimizer` bằng brew) **hoặc** giảm lưới trong Unity. Thêm **LOD Group** (LOD0 / LOD1 ≈ 40% / cull) cho vật lớn.
3. **Kích thước thật** (mét, y lên trên):
   - Đo bounding box rồi scale về đúng: bàn cao mặt 0,90 m; burette khoảng 0,75 m; bình tam giác 250 mL cao khoảng 0,145 m; ống nghiệm 0,16 m; tủ hút cao khoảng 2,3 m; bình chữa cháy khoảng 0,5 m; người 1,70 m.
   - Đặt pivot ở đáy giữa.
4. **Vật liệu:**
   - Chuyển sang **URP Lit**, giữ texture PBR; texture tối đa 2K, nén ASTC/BC7.
   - **Thuỷ tinh:** bỏ vật liệu gốc, gán **shader thuỷ tinh của dự án** (khúc xạ + fresnel).
   - **Dung dịch:** mesh riêng do code điều khiển mức/màu theo lõi hoá học. Không dùng chất lỏng trong mô hình tải về.
5. **Va chạm:** chỉ dùng box/capsule collider đơn giản; không dùng mesh collider cho vật nhỏ. Đánh dấu static để bake ánh sáng và occlusion.
6. Tạo **prefab** `Assets/Prefabs/<vai_tro>/<ten>.prefab`.
   Bộ dựng phòng và trạm chỉ tham chiếu prefab qua một bảng `KhoMoHinh` (ScriptableObject: `vai_tro` → prefab). **Không có prefab thì tự dùng hình dựng bằng code**, để app luôn chạy được.
7. **Ghi công:** mọi mô hình CC BY ghi vào `GHI_CONG.md` và màn hình **Giới thiệu** trong app theo mẫu *"Tên mô hình" by Tác giả, CC BY 4.0, Sketchfab, link*. Không ghi công là vi phạm giấy phép.

## 3. Đặt mô hình vào app (theo `vai_tro`, làm theo thứ tự ưu tiên)
| Ưu tiên | `vai_tro` | Đặt ở đâu | Lưu ý |
|---|---|---|---|
| 1 | `ban_chinh` | 5 bàn trạm thí nghiệm | Mặt bàn đúng 0,90 m. Chỗ đặt dụng cụ của từng trạm phải trùng mặt bàn mới |
| 1 | `burette`, `gia_burette`, `binh_tam_giac` | Trạm chuẩn độ | Burette mới phải có vạch chia đọc được; khoá burette vẫn bấm được (gắn lại `_chon`/điểm nóng) |
| 1 | `ong_nghiem`, `gia_ong_nghiem`, `lo_hoa_chat` | Trạm nhận biết ion | Màu dung dịch và kết tủa vẫn do code điều khiển |
| 1 | `den_bunsen`, `ngon_lua` | Trạm nhận biết (thử lửa), trạm an toàn | Ngọn lửa nên làm bằng VFX/shader theo mẫu, có đổi màu Na/K/Li/Ca/Ba/Cu |
| 1 | `dien_hoa` | Trạm pin Galvani (đồng hồ vạn năng), trạm điện phân (nguồn DC) | Màn hình số của đồng hồ/nguồn hiển thị **giá trị thật từ lõi** (1,10 V…) bằng TextMeshPro đè lên |
| 2 | `tu_hut` | Phòng Định tính (2 cái) | Cửa kính kéo lên/xuống được nếu dễ làm |
| 2 | `an_toan` | Sảnh an toàn (vòi sen + rửa mắt, bình chữa cháy). Kính bảo hộ gắn lên đầu nhân vật khi mặc đồ bảo hộ | |
| 2 | `ban_chau`, `ban_tuong`, `ghe`, `bo_do_lab` | Bàn tường, bàn phụ, ghế trong các phòng | Dùng instancing |
| 3 | `nhan_vat` | Giáo sư mặc blouse: rig bằng Mixamo (người dùng tải, để ở `~/Downloads/Mixamo/`) | Chưa có thì giữ nhân vật tạm |
| — | `tham_khao` | Không import | Chỉ mở link xem để học bố cục, kiểu dáng |

- Bảng tuần hoàn **không** lấy từ Sketchfab: dựng từ `frontend/hoa-hoc/bang-tuan-hoan/nguyen-to.json` như GĐ8.
- Nhóm nào chưa có mô hình tốt thì giữ bản dựng bằng code, nhưng nâng chi tiết theo ảnh tham khảo trong `kho-mo-hinh-3d.html`.

## 4. Giữ hiệu năng (bắt buộc)
- Sau mỗi nhóm mô hình: build, chạy `-tu-kiem-tra`, so fps với lần trước.
- Không được tụt dưới **60 fps (Cao) trên M2**. Tụt thì giảm lưới / LOD / gộp vật liệu trước khi thêm nhóm tiếp.
- Toàn cảnh: ≤ 600k tam giác hiển thị, ≤ 300 draw call (ghi lại số đo).
- Cả 4 kịch bản hoá học vẫn cho đúng kết quả (V ≈ 20,0 mL; 4 ống; 1,10 V và 0 V khi rút cầu muối; 0,64 g).

## 5. Kiểm chứng và bàn giao
1. Chụp `-tu-kiem-tra outputs/app-lab/sketchfab/`. **Tự mở xem từng ảnh.** Ghi vào HANDOFF mỗi ảnh thấy mô hình nào.
   Tiêu chí:
   - đúng tỉ lệ (không có burette to bằng người);
   - không lơ lửng, không xuyên mặt bàn;
   - không có vật liệu hồng/thiếu texture;
   - thuỷ tinh trong suốt.
2. Test EditMode thêm:
   - Mọi prefab trong `KhoMoHinh` có collider và đúng kích thước (bounding box trong khoảng cho phép).
   - Mọi mô hình trong `TAI_NGUYEN.json` có giấy phép CC BY/CC0 và có dòng trong `GHI_CONG.md`.
3. `pytest -q`, `node --test tests/js/`, EditMode: PASS hết.
4. `tools/unity_dong_bo.sh day` → commit `feat(app-lab): mo hinh that tu Sketchfab (…)` theo từng nhóm.
5. Cập nhật `TIEN_DO_APP_LAB.md`, `HANDOFF.md` (mô hình nào đã dùng / chưa tải / số fps), rồi **làm tiếp prompt trọn gói**.
