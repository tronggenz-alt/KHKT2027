# Tiến độ: làm lại tay + cánh tay (nhánh `tay-chan-thuc`)

Nhánh `tay-chan-thuc` cắt từ `n18/tich-hop` @ a58a3ba6. Thư mục làm việc dùng chung với phiên Giải bài tập
nên KHÔNG chuyển nhánh: sửa ngay trong thư mục chính, khi commit dùng chỉ mục tạm
(`GIT_INDEX_FILE=... git read-tree tay-chan-thuc; git update-index --add <tệp>; git commit-tree; git update-ref`).

## Đã xong
- Ảnh "trước" 8 tư thế × 2 góc + da trần + hai cánh tay: `docs/ngay-20-11/anh/tay-moi/truoc_*.png`
  (đo IK cũ: ống nghiệm sai lệch 7,51 mm > 3 mm; còn lại ≤ 0,51 mm).
- Mã mới (đã chép vào dự án):
  - `Assets/Scripts/Avatar/TayNguoiLuoi.cs` – SDF bàn tay + surface nets + skinning 17 xương, móng, cổ găng, ống tay áo 5 xương + blendshape.
  - `Assets/Scripts/Demo/HandRig.cs` – dùng lưới skinning, lệch pha ngón, ánh xạ MediaPipe nghịch đảo đúng, LOD.
  - `Assets/Scripts/Demo/ArmRig.cs` – IK có giới hạn, xoay sấp/ngửa, lắc/trễ lò xo tới hạn, không cấp phát.
  - `Assets/Scripts/Avatar/HandPoseTable.cs` – ngón cái CMC dạng ra ngoài tới −35°.
  - `Assets/Lab/Shaders/TayNguoi.shader` + `Assets/Scripts/Avatar/Resources/TayNguoi/TayNguoi.mat`.
  - `Assets/Scripts/Editor/KhktTayMoiVerify.cs` (+ `.KiemThu.cs`): `ChupTruoc`, `Run`.

## Kết quả (20/11 tối)
- KhktTayMoiVerify 31/31, G2 16/16, Wave6 63 đạt + 1 lỗi ngoài phạm vi (bước 7.7 menu chính). App Mac xuất OK.
- Còn lại: đo lại `-vera-do` khi không có bản VERA Lab nào khác chạy (lần đo 23:43 bị nhiễu). Xem bao-cao-tay-moi.md mục 6.
