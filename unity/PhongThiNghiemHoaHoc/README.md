# Phòng thí nghiệm Hoá học ảo – bản Unity 3D

Bản Unity là **tuỳ chọn**: trang `/hoa-hoc/phong-thi-nghiem` luôn chạy bản Web 3D (Three.js). Bản Unity chỉ hiện nút chuyển khi
**đã có bản build** trong `frontend/unity-lab/Build/` **và** admin bật công tắc ở `/admin → Phòng thí nghiệm` (admin luôn thấy nút khi có build).

## Kiến trúc – Unity chỉ VẼ, không tính hoá học
- Nguồn sự thật duy nhất là lõi JS `frontend/hoa-hoc/phong-thi-nghiem/mo-phong.js` (+ `tinh-toan.js`). Unity nhận trạng thái và vẽ lại;
  thao tác người dùng trong Unity được gửi ngược về lõi JS. Không có phép tính pH / Faraday / E° nào trong C#.
- Cảnh chỉ có **một** GameObject `LabBridge`; camera, đèn, bàn và toàn bộ dụng cụ được **dựng bằng code lúc chạy** (LatheGeometry kiểu
  `LabHinh.Lathe`, ống uốn `LabHinh.Ong`) → build bằng dòng lệnh, không cần chỉnh cảnh bằng tay, bản build nhỏ.

| Tệp | Vai trò |
|---|---|
| `Assets/Scripts/LabBridge.cs` | nhận lệnh từ web (`NhanLenh`), dựng camera / đèn / bàn, raycast bấm → gửi thao tác |
| `Assets/Scripts/LabTrangThai.cs` | kiểu dữ liệu JSON (tên trường = khoá JSON của `mo-phong.js`) |
| `Assets/Scripts/LabHinh.cs`, `LabVatLieu.cs`, `LabCamera.cs` | hình procedural, vật liệu URP (thuỷ tinh trong suốt, dung dịch, kim loại), camera quỹ đạo |
| `Assets/Scripts/Canh/*.cs` | 5 cảnh: chuẩn độ, an toàn, nhận biết ion, pin Galvani, điện phân |
| `Assets/Plugins/WebGL/LabBridge.jslib` | Unity → web: `window.PhongLabUnity.nhan(json)` |
| `Assets/Editor/BuildWebGL.cs` | `BuildWebGL.Build()`: tạo URP, vật liệu, cảnh, Player Settings rồi build |

### Giao thức
- Web → Unity: `unityInstance.SendMessage("LabBridge", "NhanLenh", '{"lenh":"…","du_lieu":"<chuỗi JSON>"}')`
  - `mo_thi_nghiem` `{id, ten, trang_thai: "<JSON>"}` · `trang_thai` `"<JSON>"` · `hieu_ung` `{loai, so, id}`
  - `doi_giao_dien` `{nen, nhan, the, chu, toi}` (màu đọc từ biến CSS của web) · `chat_luong` `{q}` · `camera` `{lenh}`
- Unity → web: `{su_kien: "san_sang"}` · `{su_kien: "thao_tac", lenh, tham_so: "<JSON>"}` · `{su_kien: "chon", loai, so}` · `{su_kien: "loi", thong_bao}`

## Cài Unity (máy chưa có Unity Editor)
1. Mở **Unity Hub** (máy đã có) → **Installs → Install Editor** → chọn **Unity 6 LTS (6000.0.x)**.
2. Ở bước chọn module, tích **WebGL Build Support** (bắt buộc). Không cần Android / iOS.
3. Đăng nhập Unity ID và kích hoạt giấy phép **Personal** (miễn phí) khi Hub yêu cầu.

## Build
```bash
# từ thư mục gốc dự án
ls /Applications/Unity/Hub/Editor            # xem tên phiên bản đã cài, ví dụ 6000.0.40f1
"/Applications/Unity/Hub/Editor/<phiên bản>/Unity.app/Contents/MacOS/Unity" -batchmode -quit \
  -projectPath unity/PhongThiNghiemHoaHoc -executeMethod BuildWebGL.Build -logFile logs/unity-build.log
```
Hoặc mở dự án bằng Unity Hub (**Add → Add project from disk** → chọn `unity/PhongThiNghiemHoaHoc`) rồi menu **Phòng thí nghiệm → Build WebGL**.
Lần mở đầu Unity tự tải gói URP và sinh `Library/` (vài phút). Nếu Unity báo phiên bản gói URP khác – chấp nhận bản Unity đề xuất.

Kết quả: `frontend/unity-lab/Build/` có 4 tệp `*.loader.js`, `*.data.unityweb`, `*.framework.js.unityweb`, `*.wasm.unityweb`
(Gzip + Decompression Fallback → máy chủ không cần header đặc biệt). Sau đó:
1. Commit thư mục `frontend/unity-lab/Build/` (đưa lên web bằng `dua-len-web.command`).
2. `/admin → Phòng thí nghiệm` sẽ báo "Đã có – sẵn sàng" + dung lượng; bật công tắc **Cho mọi người dùng bản 3D Unity**.

## Bản build hiện có (23/10/2026)
- Build bằng **Unity 6000.6.7f1** (URP 17.6.0, uGUI 2.6.0): 4 tệp, tổng **≈ 9,3 MB** (`wasm` 6,4 MB · `data` 3,2 MB · `framework` 68 KB · `loader` 49 KB).
- Đã chạy thử trong trang: nút Unity → tải → `san_sang` → đồng bộ trạng thái 5 thí nghiệm; quay về Web 3D giữ nguyên tiến trình.
- **Máy để dự án trong iCloud** (thư mục Tài liệu): build bằng bản sao NGOÀI iCloud để `Library/` không bị đẩy lên mây:
  ```bash
  G=~/Library/Caches/antigravity/unity-build; mkdir -p $G/unity $G/frontend/unity-lab
  rsync -a --exclude Library unity/PhongThiNghiemHoaHoc/ $G/unity/PhongThiNghiemHoaHoc/
  "<Unity>/Contents/MacOS/Unity" -batchmode -quit -buildTarget WebGL -projectPath $G/unity/PhongThiNghiemHoaHoc \
    -executeMethod BuildWebGL.Build -logFile $G/unity-build.log
  cp $G/frontend/unity-lab/Build/unity-lab.* frontend/unity-lab/Build/
  ```
  Unity Editor cũng nên cài ngoài iCloud (Unity Hub → Settings → Installs → `/Applications/Unity/Hub/Editor`).

## Ghi chú kỹ thuật
- Toạ độ dụng cụ chép từ bản Web 3D (three.js – hệ tay phải); Unity là hệ tay trái → `LabBridge` lật ngang phép chiếu camera
  (`projectionMatrix × Scale(-1,1,1)`) + `GL.invertCulling` khi vẽ; nhãn chữ quay theo camera lật x để đọc được.
- `Assets/link.xml` giữ `BoxCollider/SphereCollider/CapsuleCollider/MeshCollider` (Strip Engine Code không thấy collider tạo bằng `CreatePrimitive`).
- `BuildWebGL` đặt Active Input Handling = Both (code dùng `UnityEngine.Input`).

## Giảm dung lượng (mục tiêu ≤ 25 MB)
`BuildWebGL` đã đặt: Gzip, Strip Engine Code, Managed Stripping = High, IL2CPP Optimize Size, Code Optimization = Disk Size,
Exception Support = None, chỉ WebGL 2. Nếu vẫn lớn: tắt bóng mềm trong URP Asset, bỏ gói không dùng trong `Packages/manifest.json`.

## Lỗi thường gặp
- **Không thấy nút Unity trên trang** – chưa có đủ 4 tệp build, hoặc công tắc admin đang tắt (người thường); xem `/admin → Phòng thí nghiệm`.
- **Tải Unity lỗi** – trang tự quay lại bản Web 3D và báo lỗi; xem Console trình duyệt (dòng `[Unity]`).
- **Chữ hiển thị ô vuông** – font LegacyRuntime thiếu ký tự; `CanhCoSo.KyTu()` đã đổi chỉ số dưới/trên sang ký tự thường.
