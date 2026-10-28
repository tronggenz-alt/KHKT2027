# Phối hợp Claude ↔ Gemini: đồ hoạ Phòng thí nghiệm Hoá ảo (24/10/2026)

Yêu cầu của người dùng: dụng cụ sắc nét, **không còn nếp gấp giữa các đoạn**, liền mạch; không gian phòng chân thật như phòng thí nghiệm ngoài đời.
Cách làm: không tải model ngoài (giữ bản build nhỏ); mọi hình dựng bằng code, **một nguồn số liệu cho cả Web và Unity**.

## Claude đã làm (đã commit, Web đã đưa lên)

| Nơi | Việc |
|---|---|
| `frontend/hoa-hoc/phong-thi-nghiem/hinh-hoc.js` (UMD) | Profile dụng cụ **bo góc liền tiếp tuyến**, mặt tròn xoay pháp tuyến tính theo công thức (không đường may), **thành thuỷ tinh dày thật + gờ miệng bo**, chất lỏng ôm sát lòng bình. Số liệu dụng cụ ở `DUNG_CU`. Test: `tests/js/phong_thi_nghiem_hinh_hoc.test.mjs` |
| `ve-web.js` | Dùng `hinh-hoc.js` cho mọi dụng cụ thuỷ tinh + chất lỏng |
| `unity/PhongLabHoa3D/.../ThiNghiem/LabHinh.cs` | Bản C# **cùng thuật toán + cùng số liệu** (`LabHinh.DungCu.Coc/BinhTamGiac/OngNghiem/LoThuoc/DenCon/Pheu`) |
| `LabVatLieu.HaiLuot` + `CanhCoSo.LuoiGO` | Thuỷ tinh / chất lỏng trong suốt **vẽ hai lượt** (mặt sau rồi mặt trước). Đây mới là nguyên nhân vệt răng cưa lặp theo từng vòng ngang: vật liệu trong suốt Cull Off, không ghi chiều sâu → mặt trước/sau của cùng lưới trộn màu theo thứ tự tam giác. `DatMau()` cập nhật cả hai bản |
| `CanhCoSo.Chu()` | Sửa chữ nổi bị **ngược gương** (`-co` → `co`): `-co` chỉ đúng ở dự án Unity cũ có lật phép chiếu |
| `vat-lieu-thuc.js`, `noi-that-thuc.js` | Texture sinh bằng canvas (sàn vinyl có ron, mặt bàn epoxy navy có hạt, gỗ sồi, tường sơn, trần ô vuông, inox chải) + chi tiết phòng (khe cánh tủ, tay nắm, chân đế, vòi cổ ngỗng, núm nước, lòng chậu, ổ cắm, van gas, lọ thuốc thử trên giá, khung cửa sổ nhôm, khung cửa chính, biển LỐI RA, đồng hồ, khung đèn, đầu phun, cửa gió). `phong-mo.js` gọi `nangCapPhong(this)` một dòng (bỏ dòng đó = bản cũ) |
| `thu-vien-dung-cu.html` | Trang xem model: `/static/hoa-hoc/phong-thi-nghiem/thu-vien-dung-cu.html` (`?so-sanh=1` hàng cũ phía sau, `?solo=cu`, `?khung=1` lưới đa giác, `?cam=x,y,z,tx,ty,tz`) |
| `phong-mo.js` `lamMo()` | Sửa lỗi nhân màu **cộng dồn** mỗi lần vào bàn (sàn, tường tối dần); nay nhớ màu nền gốc |
| `tests/test_phong_thi_nghiem_module.py` | Bắt thiếu importmap / thẻ script / file khi thêm module JS mới |

## Đề nghị Gemini làm tiếp (Claude không làm để tránh đè lên việc đang dở của bạn)

1. **Chuyển chi tiết phòng sang Unity**: tạo `ToaNha/ChiTietThuc.cs` theo `noi-that-thuc.js` (đọc file đó, có đủ toạ độ + kích thước, đơn vị mét) rồi gọi từ `DungPhongLab` / `DungToaNha`.
   Màu gốc: sàn vinyl (200,207,214) ô 0,6 m ron tối · mặt bàn epoxy (36,58,98) có hạt sáng · gỗ sồi (216,182,134) vân dọc · tường (250,251,252) · trần ô 0,6 m thanh T xám.
   Unity không cần texture canvas: dùng màu + `_Smoothness`, hoặc sinh `Texture2D` bằng code (cùng hàm nhiễu).
2. **`DungCuISO.cs`**: truyền kích thước ISO vào `LabHinh.DungCu.*` thay vì dựng riêng, để cùng một thuật toán (không nếp gấp, thành dày).
   Lưu ý `LabHinh.Lathe(Vector2[] profileNgoai)` nay dựng **thành kính dày** (hai submesh) — chỉ dùng cho thuỷ tinh. Vật đặc dùng `LabHinh.LatheMem(ctrl)`.
3. **Mesh có 2 submesh** (`subMeshCount == 2`) phải gán bằng `LabVatLieu.HaiLuot(vl)` – `LuoiGO` đã tự làm; nếu bạn tạo `MeshRenderer` ở chỗ khác thì làm tương tự.
4. **Nhân vật** vẫn là viên nang + hình trụ (blouse, đầu, tay): nâng cấp hình khối / vật liệu cho giống người thật hơn.
5. **Commit chọn lọc**: lần trước `git add -A` đã gộp cả file dở của Claude vào commit `593b337` / `93b5cc7`. Dùng `git add <file>` hoặc `git commit -- <file>`.

## Cách kiểm tra bằng mắt

- Web: mở trang thư viện ở trên; vào từng bàn trong `/hoa-hoc/phong-thi-nghiem`.
- Unity (batchmode, không cần build player): bản sao dự án ngoài iCloud (`~/UnityProjects/PhongLabHoa3D`) → script Editor dựng dụng cụ + `Camera.Render()` → PNG. Đừng mở Unity trực tiếp trên thư mục trong repo (không có `Library/`, mở lần đầu rất lâu).
