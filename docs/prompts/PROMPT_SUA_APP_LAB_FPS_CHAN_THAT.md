# PROMPT – Sửa app Phòng Lab Hoá 3D (Unity): TĂNG FPS trước, rồi DỰNG LẠI CHÂN THẬT

> Gõ cho Antigravity: **"Đọc và làm theo PROMPT_SUA_APP_LAB_FPS_CHAN_THAT.md, chạy liên tục đến hết."**
> Đây là bản **chen ngang** vào `PROMPT_LAB_UNITY_TRON_GOI.md` (đang dừng ở GĐ7):
> - Làm xong file này thì quay về `PROMPT_LAB_UNITY_TRON_GOI.md`. Đánh dấu GĐ7 / Mốc 1, rồi làm tiếp từ GĐ8.
> - Những phần "chân thật" làm ở đây được tính là đã làm một phần GĐ9–GĐ10.
>
> Luật vận hành vẫn như `PROMPT_LAB_UNITY_TRON_GOI.md`: không dừng hỏi giữa chừng, commit theo bước, cập nhật `TIEN_DO_APP_LAB.md` + `HANDOFF.md`, tự mở xem mọi ảnh kiểm tra.

## 0. Hiện trạng (Claude kiểm tra 24/10/2026, 18:25)
- **Commit:** đã commit GĐ1–GĐ6 (`901776b`). Gemini ngừng ở **GĐ7** từ khoảng 16:09.
- **Tự kiểm tra:** `outputs/app-lab/v1/bao-cao.json` **KHÔNG ĐẠT**.
  - fps trung bình **2,0**, 1% low 1,7; khởi động **827 giây**; `ram_mb` 7,7 (số này vô lí).
  - 4 kịch bản hoá học đều đúng.
- **Ảnh:**
  - `01_cua_vao_goc3.png`: đầu nhân vật chiếm nửa màn hình.
  - `03_tram_chuan_do.png`: camera nằm trong người nhân vật, menu chính đè lên cảnh tại bàn.
  - Phòng nhìn thô: khối hộp xám, sàn màu neon chói (tím, xanh lơ, cam), nhân vật làm từ trụ tròn.
- **Thay đổi CHƯA commit đang xoá mất phần GĐ6:** `backend/phong_thi_nghiem.py`, `quan_tri_web.py`, `main.py`, `nhat_ky.py`, `frontend/hoa-hoc/phong-thi-nghiem/giao-dien.js`, `frontend/phong-thi-nghiem.html`, và các dòng Unity trong `.gitignore`.
- **Ổ đĩa:** còn khoảng **18 GB**. Tối nay người dùng sẽ dọn thêm. Vì vậy:
  - **Bước 1 (FPS) không tải gì thêm.**
  - Bước 2 chỉ tải tài nguyên nhỏ: tổng ≤ 1,5 GB, texture 1K–2K.
  - Còn dưới 15 GB thì không tải thêm. Dùng vật liệu dựng bằng code và ghi việc còn lại vào HANDOFF.

## 1. Cứu code trước (bắt buộc, làm đầu tiên)
1. **KHÔNG commit** trạng thái hiện tại.
2. Khôi phục các file web về bản `901776b`:
   ```bash
   git checkout 901776b -- .gitignore backend/main.py backend/nhat_ky.py backend/phong_thi_nghiem.py backend/quan_tri_web.py frontend/hoa-hoc/phong-thi-nghiem/giao-dien.js frontend/phong-thi-nghiem.html
   ```
3. Xem `git diff` trong `unity/PhongLabHoa3D/`: giữ phần sửa đúng, bỏ phần làm hỏng.
4. Chạy `pytest -q` (phải PASS, gồm `tests/test_ung_dung_lab.py`).
5. Commit: `fix: khoi phuc phan web GD6 bi xoa nham`.

## 2. BƯỚC 1 – Tăng FPS (mục tiêu ≥ 60 fps, mở app ≤ 10 s trên M2 8 GB)
1. **Đo trước khi sửa.**
   - Build bản **Development + Autoconnect Profiler**, chạy app thật (cửa sổ có GPU, không chạy `-batchmode` / `-nographics` khi đo fps).
   - Ghi 10 giây Profiler, tìm hàm chiếm nhiều ms nhất ở luồng chính và luồng kết xuất.
   - Ghi kết quả "trước" vào HANDOFF.md.
2. **Kiểm từng nghi phạm, sửa theo thứ tự:**
   - **Đo sai:** tự kiểm tra có chạy `-batchmode`/headless không? Có `Time.captureFramerate`, `Application.targetFrameRate` thấp, VSync sai không? `ram_mb` phải đọc bằng `Profiler.GetTotalAllocatedMemoryLong()`, đổi ra MB.
   - **Khởi động 827 s:**
     - Không bake NavMesh, không dựng lưới lớn, không biên dịch shader lúc mở.
     - NavMesh bake sẵn trong Editor rồi lưu vào cảnh.
     - Dùng **Shader Variant Collection + warmup**.
     - Không tạo hàng nghìn vật liệu mới (dùng chung vật liệu, `MaterialPropertyBlock`).
   - **Jint:** chỉ chạy trên luồng riêng, 30 Hz. Luồng chính **không bao giờ** gọi `Engine.Execute` hay chờ khoá. Không parse JSON trạng thái mỗi khung (chỉ khi có bản mới).
   - **OnGUI:** giao diện hiện đang vẽ bằng OnGUI (rất chậm, cấp phát rác mỗi khung). **Chuyển sang UI Toolkit**, hoặc ít nhất cache `GUIStyle`/`GUIContent` và không tạo chuỗi mỗi khung.
   - **Dựng hình:**
     - 5 trạm + phòng: gộp lưới tĩnh (static batching / `Mesh.CombineMeshes`), bật SRP Batcher, GPU Resident Drawer, GPU instancing cho vật lặp.
     - Tắt bóng của vật nhỏ.
     - Chỉ 1 đèn có bóng, shadow distance ≤ 15 m, cascade 2.
     - Hạt (bọt, kết tủa) chỉ chạy ở trạm đang làm.
   - **Độ phân giải:** ảnh chụp là 3840×2160 (Retina). Mặc định **Fullscreen Window + giới hạn 1920×1200**, có tuỳ chọn STP / tỉ lệ dựng hình 70–100%.
   - **Camera tự kiểm tra:** đo fps khi đi và khi đứng tại bàn, **mỗi lần ít nhất 10 s**, bỏ 2 s đầu.
3. **Đạt khi** `bao-cao.json` cho fps trung bình ≥ 60, 1% low ≥ 45, khởi động ≤ 10 s, RAM thực ≤ 1,5 GB, cả 4 kịch bản đúng.
   Ghi bảng trước/sau vào HANDOFF.md. Commit `perf(app-lab): …`.

## 3. BƯỚC 2 – Dựng lại cho chân thật (giữ ≥ 60 fps)
Tham khảo hình dáng: noithathoaphat.com/thu-vien-3d/noi-that-phong-thi-nghiem (bàn BTNHS01/02, BTN102) và banthinghiemlysonsaky.vn (tủ hút, tủ an toàn). Chỉ xem để dựng lại, **không tải file .max**, không dùng ảnh có bản quyền.

1. **Màu và vật liệu thật, bỏ màu neon:**
   - Sàn vinyl xám nhạt có ron mờ, dải màu khu chỉ là **viền mảnh 10 cm**, độ bão hoà thấp.
   - Tường trắng ngà, chân tường xám đậm.
   - Mặt bàn phenolic **xanh lam đậm** có gờ chắn, tủ dưới **vân gỗ sáng**, khung **thép sơn xám**, tay nắm inox.
   - Dùng URP Lit đúng PBR (metallic/smoothness hợp lí). Texture **CC0** từ Poly Haven / ambientCG, 1K–2K, tổng ≤ 1,5 GB; ghi vào `TAI_NGUYEN.json` + `GHI_CONG.md`.
   - Không đủ ổ thì vẽ texture bằng code (vân gỗ, ron sàn).
2. **Hình khối có chi tiết:** dùng bo cạnh (bevel) cho mọi bàn và tủ, không còn khối hộp trơn.
   Thêm chậu inox + vòi cổ ngỗng, giá thuốc thử 2 tầng, tủ hút khí độc có cửa kính kéo + đèn trong lòng, tủ hoá chất 2 cánh, ghế đẩu, ổ điện, ống gió trên trần.
3. **Ánh sáng giống phòng thật:**
   - Đèn panel âm trần (emissive) + 1 nắng qua cửa sổ.
   - **Bake lightmap** cho phần tĩnh (Progressive GPU, lightmap ≤ 1024–2048), Light Probes cho nhân vật.
   - 1 **Reflection Probe dạng hộp** cho phòng. SSAO nhẹ, tonemapping ACES, Bloom rất nhẹ, màu trung tính.
   - Bake ánh sáng chính là thứ vừa **đẹp hơn vừa nhanh hơn** đèn realtime.
4. **Thuỷ tinh và dung dịch:** shader trong suốt có fresnel + cạnh kính sáng, dung dịch có mặt khum, vạch chia rõ.
   Dụng cụ theo kích thước thật: burette 50 mL dài khoảng 750 mm, bình tam giác 250 mL cao khoảng 145 mm, cốc 250 mL khoảng Ø70 × 95 mm, ống nghiệm 16 × 160 mm.
5. **Nhân vật giáo sư** (bản tạm, chưa có file Mixamo):
   - Dựng lại cho ra dáng người thật thay vì ghép trụ: thân thuôn, vai, cổ, đầu tỉ lệ đúng; áo blouse dài qua gối có ve áo và túi; quần sẫm, giày; tóc hoa râm; kính.
   - Shading mượt (`smooth normals`), đủ đa giác ở chỗ cong.
   - Có file ở `~/Downloads/Mixamo/` thì dùng file đó (xem GĐ4 của prompt trọn gói).
6. **Camera và giao diện:**
   - Góc 3: cách 3–4,5 m, cao ngang vai, lệch phải 0,4 m, FOV 60. Nhân vật chiếm khoảng 25–35% chiều cao khung, **không che giữa màn hình**.
   - Tại bàn: camera cận cảnh bàn, **ẩn nhân vật**, **đóng menu chính**; bảng điều khiển ở cạnh phải, không đè dụng cụ.

## 4. Kiểm chứng (bắt buộc)
1. Build lại, chạy `-tu-kiem-tra outputs/app-lab/v1b/` **ở chế độ có cửa sổ** (không headless).
2. `bao-cao.json` đạt mục 2.3. Cả 4 kịch bản hoá học đúng.
3. **Tự mở từng ảnh xem.** Mỗi ảnh ghi 1 dòng mô tả vào HANDOFF.md. Ảnh chưa đạt thì chưa xong. Tiêu chí:
   - Ảnh cửa vào: thấy rõ phòng + nhân vật nhỏ ở giữa dưới, không thấy màu neon.
   - Ảnh tại bàn: thấy rõ dụng cụ, không có menu đè, không thấy thân nhân vật.
4. Chạy `pytest -q`, `node --test tests/js/`, test EditMode Unity: PASS hết.
5. `tools/unity_dong_bo.sh day` rồi commit. Cập nhật `TIEN_DO_APP_LAB.md`: GĐ7 / Mốc 1 xong, các mục chân thật đã làm trong GĐ9–GĐ10.
6. **Làm tiếp `PROMPT_LAB_UNITY_TRON_GOI.md` từ GĐ8.** Mỗi lần tải tài nguyên đều kiểm `df -h ~` trước. Chưa push hoặc deploy.
