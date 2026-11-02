# TRẠNG THÁI TIẾN ĐỘ NÂNG CẤP ĐỒ HỌA HD (VERA LAB HD)

Cập nhật: 2026-11-03 00:07
Nhánh: `do-hoa-hd` | Thư mục: `~/VERALabHD`
Mục tiêu hiện tại: Nhiệm vụ 1 - Nâng cấp toàn bộ đồ họa VERA Lab lên mức siêu thực (PBR), sau đó chuyển sang Nhiệm vụ 2 - Giao diện iPad.

---

## 1. TIẾN ĐỘ CÁC GIAI ĐOẠN

- [x] **Giai đoạn 0: Chuẩn bị và khóa tọa độ** (Hoàn thành, commit `34e22e05`, mốc gốc `toa_do_goc.json` 18.964 đối tượng, 0 lỗi).
- [x] **Giai đoạn 1: Tham khảo mạng và bản định hướng** (Hoàn thành: `art-bible.md` với 12 nguồn cảm hứng và thông số vật lý chuẩn; `nguon-tai-nguyen.md` với 100% tài sản CC0 Poly Haven / ambientCG).
- [x] **Giai đoạn 2: Nền kết xuất (Pipeline, Post-processing, 3 mức chất lượng, culling)** (Hoàn thành: Forward+, SSAO, 4 cascades bóng 4096, culling 6cm/30m, HdQualityManager, HdPerformanceHud).
- [x] **Giai đoạn 3: Vật liệu nền PBR & World-Aligned UV** (Hoàn thành: 543 vật liệu nâng cấp PBR, 12 bộ vân 1024x1024 Normal/AO/Mask tại Assets/Lab/HD/Textures, LabKit.HdMaterials.cs).
- [/] **Giai đoạn 4: Ánh sáng (Phòng Lab & Nhà Kính, baking, GI)** (Đang thực hiện).
- [ ] **Giai đoạn 5: Kiến trúc phòng và mặt ngoài (nhóm `HD_*`, HDRI)**.
- [ ] **Giai đoạn 6: Hệ thống cây siêu thực (Subsurface, LaCay.shader pass)**.
- [ ] **Giai đoạn 7: Dụng cụ và máy móc (Thủy tinh khúc xạ/Fresnel, inox, vạch chia sắc nét)**.
- [ ] **Giai đoạn 8: Bản mẫu trong Unity (So tọa độ 0 lỗi, báo cáo bản mẫu, tag `do-hoa-mau-1`)**.
- [ ] **Giai đoạn 9: Nhân rộng toàn bộ 13 khu**.
- [ ] **Giai đoạn 10: Kiểm thử cuối và bàn giao đồ họa**.
- [ ] **CHUYỂN SANG NHIỆM VỤ GIAO DIỆN IPAD** (Giai đoạn 0 - 9).

---

## 2. VIỆC ĐANG LÀM
- Triển khai Giai đoạn 2:
  1. Cấu hình URP Renderer và RPAsset: Forward+, render scale 1.0, HDR, 4 cascades shadow, soft shadows, SSAO, Depth & Opaque Textures.
  2. Điều chỉnh ngưỡng culling: Đổ bóng vật nhỏ từ 6 cm (không còn bay lơ lửng), khoảng cách culling chi tiết.
  3. Bộ điều khiển 3 mức chất lượng đồ họa (Ultra mặc định, Cao, Cân bằng) và HUD giám sát hiệu năng (FPS, RAM, Draw Calls).
