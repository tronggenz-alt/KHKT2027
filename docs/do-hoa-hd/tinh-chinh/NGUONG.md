# KHÓA NGƯỠNG ĐÁNH GIÁ CHẤT LƯỢNG ĐỒ HỌA (LOCKED THRESHOLDS)
**Dự án:** VERA Lab HD (`do-hoa-hd-tinh-chinh`)  
**Thời điểm khóa:** Trước Vòng 1 (2026-11-03)  
**Quy tắc:** Đã khóa là cố định, tuyệt đối không được nới lỏng hay sửa đổi sau khi thấy kết quả.

---

## 1. SỐ ĐO MỐC NỀN THỰC TẾ (BASELINE v00 - 44 ẢNH 2360x1640)

- **Độ sáng trung bình toàn ảnh:** `0.5117`
- **% điểm cháy trung bình (> 0.98):** `0.000%`
- **% điểm đen trung bình (< 0.03):** `0.779%`
- **Độ bão hòa trung bình:** `0.2021`
- **% điểm rực trung bình (S > 0.6 và V > 0.5):** `2.913%`
- **Độ nét trung bình (Phương sai Laplacian):** `157.51`
- **Mức quầng quanh cạnh trung bình:** `3.303`
- **% điểm màu hồng tím (Missing Shader):** `0.000%`

---

## 2. CÁC NGƯỠNG KHÓA CỨNG (LOCKED CONSTRAINTS)

| Chỉ số kỹ thuật | Ngưỡng khóa | Căn cứ quy định |
|---|---|---|
| **% điểm hồng tím (Missing Shader)** | $\le 0.001\%$ (Thực chất = 0) | Mục 4.4: Tuyệt đối không có lỗi vật liệu |
| **% điểm ảnh cháy sáng (> 0.98)** | $\le 3.0\%$ toàn ảnh (TB $\le 0.5\%$) | Mục 4 prompt gốc |
| **% điểm ảnh tối đen (< 0.03)** | $\le 2.0\%$ (trừ khu tối có chủ đích như kho, tủ hút) | Mục 4 prompt gốc |
| **Độ bão hòa trung bình** | $\le 0.210$ (Không tăng so với nền) | Mục 4.4 & Định hướng thẩm mỹ tiết chế |
| **% điểm rực màu (S > 0.6, V > 0.5)** | $\le 3.0\%$ (Không tăng so với nền) | Mục 4.4 & Chống màu mè, không neon |
| **Độ nét trung bình (Laplacian)** | $\ge 150.0$ (Không giảm so với nền) | Mục 4.4 & Tăng độ sắc nét thật |
| **Mức quầng quanh cạnh (Edge Halo)** | $\le 4.5$ (Không tạo viền sáng giả quanh cạnh) | Mục 3 & Mục 4.4: Làm nét tự nhiên |
| **Sai lệch tọa độ, góc, tỉ lệ** | **0 LỖI TUYỆT ĐỐI** trên 18.964 vật thể | Mục 2: Cấm dời bất cứ thứ gì |
| **Lỗi runtime trong Unity Log** | **0 LỖI** (`error CS`, `Exception`, `Shader error`) | Mục 6: Không được giả PASS |

---

## 3. TIÊU CHÍ BẢN MỚI THẮNG BẢN TỐT NHẤT (MỤC 5.7)

Một bản ở vòng mới chỉ được công nhận là **THẮNG** khi thỏa mãn đồng thời:
1. Không vi phạm bất kỳ ngưỡng khóa cứng nào ở Bảng 2.
2. Kiểm tra đối chiếu tọa độ `HdCoordinateTool.CompareBatch` đạt 0 lỗi tuyệt đối.
3. Lỗi hình ảnh đã chọn giảm rõ rệt trên ảnh thật so với bản tốt nhất hiện tại.
4. Không có bất kỳ góc ảnh nào trong 44 góc bị xấu đi (không sụt độ nét, không cháy, không tối sầm).
5. Vượt qua 100% kiểm chứng toàn vẹn của `kiem_chung.py`.
