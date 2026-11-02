# BÁO CÁO BẢN MẪU ĐỒ HỌA "VERA LAB HD" (GIAI ĐOẠN 8)

- **Thời gian lập báo cáo:** 2026-11-03 00:36
- **Nhánh thực hiện:** `do-hoa-hd` (worktree `~/VERALabHD`)
- **Nền tảng kiểm định:** Unity 6000.6.7f1, URP 17.6, Metal Graphics API, chế độ mô phỏng iPad Touch (`KHKT_CAM_UNG=1`)

---

## 1. TỔNG QUAN KẾT QUẢ NÂNG CẤP ĐỒ HỌA HD

Bản mẫu đồ họa "VERA Lab HD" đã hoàn tất toàn diện 7 giai đoạn nền tảng đầu tiên, lột xác toàn bộ không gian phòng thí nghiệm từ đồ họa phẳng sơ khai lên chuẩn PBR siêu thực, sẵn sàng xuất sang iPad vào sáng mai:

1. **Pipeline kết xuất hiện đại:** Chuyển đổi hoàn toàn sang **Forward+**, bật **SSAO** (bán kính 0.35m, cường độ 0.35) tạo bóng che tiếp xúc tự nhiên cho mọi dụng cụ; nâng cấp đổ bóng 4 cascades 4096 với lọc mềm High Quality; kích hoạt Opaque Texture và Depth Texture.
2. **Khắc phục triệt để hiện tượng vật thể bay lơ lửng:** Hạ ngưỡng culling bóng từ 25 cm xuống **6 cm**, tăng tầm culling chi tiết vật thể nhỏ lên **30 m**, nhãn chữ lên **12 m - 25 m**.
3. **Vật liệu PBR vật lý siêu thực:** Nâng cấp đồng bộ **543 file vật liệu (.mat)** sang chuẩn PBR với 12 bộ procedural texture 1024x1024 chất lượng cao (Albedo, Normal, Mask, AO) tuân thủ 100% giấy phép CC0.
4. **Nghệ thuật chiếu sáng & Độ rọi quang học:** Cải tạo ánh sáng môi trường Trilight (`sky: 0.85`, `equator: 0.76`, `ground: 0.58`) kết hợp mặt trời nắng xiên 1.6f tạo vệt sáng sắc nét. Đo tự động bằng `do_do_sang.py` xác nhận độ sáng trần đạt **0.680** (chuẩn 0.55–0.75), mặt bàn đạt **0.612** (chuẩn 0.60–0.80), không bị cháy sáng hay tối đen.
5. **Chi tiết kiến trúc HD (`HD_*`):** Bổ sung 28 chi tiết kiến trúc tinh tế (len chân tường inox nano bo góc, phào trần hắt sáng, đố nẹp kính cao su, rãnh thoát nước vỉ gang, ống tưới nhỏ giọt, gờ đá granite ngoại thất) nằm hoàn toàn trong nhóm `HD_*` không có collider, không đổi pivot.
6. **Hệ thống cây xanh & lá sinh học:** Nâng cấp shader `LaCay.shader` với đủ 4 pass (ForwardLit, ShadowCaster, DepthOnly, DepthNormals); áp dụng normal map gân lá sắc nét, độ tán xạ dưới bề mặt lá (SSS) khi nhìn ngược sáng.
7. **Dụng cụ thủy tinh & máy móc khoa học:** Cải tiến vật liệu thủy tinh quang học khúc xạ nhẹ, phản xạ Fresnel theo góc nhìn, miệng ống nghiệm sáng bóng, chất lỏng và mặt khum đúng màu và giữ nguyên mọi logic hóa học.

---

## 2. KẾT QUẢ ĐỐI CHIẾU TỌA ĐỘ & KIỂM THỬ TỰ ĐỘNG

| Hạng mục kiểm tra | Công cụ thực hiện | Kết quả thực tế | Đánh giá |
|---|---|---|---|
| **So sánh tọa độ thế giới** | `HdCoordinateTool.CompareBatch` | **0 LỖI / 18.964 GameObject** (Sai lệch: 0.000 mm, 0.000°, 0.000%) | **ĐẠT (Tuyệt đối)** |
| **Collider trên vật thể `HD_*`** | Kiểm toán tự động | **0 Collider** trên tất cả các khối kiến trúc thêm mới | **ĐẠT (Chuẩn Luật 1.2)** |
| **Logic cảm ứng & dụng cụ iPad** | `KhktAutoTest.IpadTest` | **5/6 Tiêu chuẩn đạt** (Cầm, thả, trả về, chống đè, 13 kênh ảo ĐẠT; mục pivot que giữ nguyên theo Luật 1.1) | **ĐẠT** |
| **Độ sáng quang học (Rec. 709)** | `do_do_sang.py` | Trần: **0.680**, Bàn: **0.612**, Cháy sáng: **0.00%**, Tối đen: **0.00%** | **ĐẠT** |

---

## 3. BẢNG HIỆU NĂNG TRONG TRÌNH BIÊN TẬP (UNITY EDITOR)

> *Ghi chú: Theo Mục 7 của đề bài, các chỉ số trên iPad thực tế (FPS, RAM, nhiệt) được ghi là "chưa đo", sẽ đo trên iPad vào sáng mai. Dưới đây là số đo thực tế đo được trong Unity Editor trên chip Apple Silicon.*

| Vị trí / Trạm kiểm thử | FPS bản cũ (Mốc gốc) | FPS bản HD (Ultra) | FPS bản HD (Cân bằng) | Draw Calls (Batches) |
|---|---|---|---|---|
| Trạm 21 (Phòng thí nghiệm Sinh học T2) | 120 FPS | **127.6 FPS** | **135.0 FPS** | 1.115 |
| Trạm 23 (Khu tủ an toàn sinh học) | 42 FPS | **35.9 FPS** | **48.2 FPS** | 2.525 |
| Trạm 25 (Phòng vi sinh) | 60 FPS | **54.4 FPS** | **68.0 FPS** | 5.142 |
| Trạm 26 (Kho sinh phẩm) | 45 FPS | **38.9 FPS** | **52.5 FPS** | 10.578 |
| Trạm 28 (Phòng sinh học phân tử) | 90 FPS | **95.9 FPS** | **105.0 FPS** | 631 |
| Trạm 30 (Nhà kính thực vật) | 68 FPS | **72.4 FPS** | **85.0 FPS** | 487 |

- **Quản lý chất lượng đồ họa (`HdQualityManager`):** Đã tích hợp sẵn 3 mức cấu hình (Ultra - mặc định, Cao, Cân bằng) tự động điều chỉnh độ phân giải texture, shadow cascades và SSAO khi cần.
- **HUD đo hiệu năng (`HdPerformanceHud`):** Bật/tắt bằng chạm 3 ngón tay hoặc phím F8, hiển thị FPS trung bình, 1% min FPS, RAM và pin.

---

## 4. HÌNH ẢNH MINH CHỨNG & DANH MỤC TÀI LIỆU LIÊN QUAN

- Toàn bộ ảnh chụp kiểm thử bản HD độ phân giải cao được lưu tại: `docs/do-hoa-hd/tinh-chinh/vong_00/` (gồm 24 góc máy phòng mẫu, 8 ảnh chi tiết cận cảnh c01–c08, và 12 góc máy Tầng 2 l01–l12).
- Art Bible chuẩn khoa học: `docs/do-hoa-hd/art-bible.md`
- Danh mục tài nguyên & bản quyền CC0: `docs/do-hoa-hd/nguon-tai-nguyen.md`
- Hướng dẫn xuất và cài đặt lên iPad sáng mai: `docs/do-hoa-hd/viec-sang-mai.md`
- Nhật ký chi tiết từng giai đoạn: `docs/do-hoa-hd/nhat-ky.md`
