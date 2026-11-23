# Tư duy vẽ mũi tên cong 3D (cơ chế phản ứng)

Áp dụng cho MỌI bài, không sửa riêng từng phản ứng. Mã: `Scripts/CoChe/TrinhPhat3D.cs` → `DiemMuiTen` (dựng) và
`KiemMuiTenHinhHoc` (kiểm). Kiểm thử: `KhktCoCheRunner` – mọi bài mẫu × mọi bước × 24 góc nhìn × Ẩn H bật/tắt.

## 1. Mũi tên nói gì
Mũi tên cong = **một cặp electron đi từ chỗ có e (đuôi) tới chỗ nhận e (mũi)**.
- Đuôi đặt ở **chỗ chứa cặp e**: cặp e tự do → mặt cầu nguyên tử nguồn; liên kết → giữa liên kết (lệch ra ngoài 0,3 Å).
- Mũi đặt ở **chỗ e tới**: nguyên tử → mặt cầu nguyên tử đó (không đâm vào trong); liên kết mới → giữa liên kết đó.
- Liên kết đích mà 2 nguyên tử **còn xa (> 1,9 Å)** = liên kết đang hình thành → mũi chỉ vào **nguyên tử nhận**
  (nguyên tử không thuộc phía nguồn), vì giữa hai nguyên tử xa là khoảng trống.

## 2. Sáu kiểu mũi tên
| Kiểu | Ví dụ | Đuôi | Mũi | Độ vồng |
|---|---|---|---|---|
| Nguyên tử → nguyên tử | OH⁻ tấn công C (SN2), OH⁻ lấy H (E2) | mặt cầu nguồn, nghiêng về đích | mặt cầu đích, phía tới | 22% khoảng cách (0,35–1,2 Å) |
| Nguyên tử → liên kết của chính nó | O⁻ dùng cặp e tạo lại C=O | mặt cầu nguồn, nghiêng ra ngoài | giữa liên kết | ≥ 0,7 Å (cung nhỏ cuộn ra) |
| Nguyên tử → liên kết khác | hiếm | mặt cầu nguồn | giữa liên kết | 25% (0,45–1,2 Å) |
| Liên kết → một đầu của nó | C–Br → Br (nhóm rời đi), O–H → O | giữa liên kết | mặt cầu đích **phía xa** | ≥ 0,9 Å, ôm sang đích |
| Liên kết → liên kết kề | C–H → C=C (E2), π dịch chuyển | giữa liên kết nguồn | giữa liên kết đích | ≥ 0,6 Å, vồng cao |
| Liên kết → nguyên tử xa | π của anken → H⁺ / N của NO₂⁺ | giữa liên kết | mặt cầu nguyên tử nhận | 30% (0,5–1,2 Å) |

Hai kiểu "của chính nó" có nguồn và đích gần như trùng nhau → hướng đi lấy theo **trục liên kết**, không theo
đoạn nối đuôi–mũi (đoạn đó gần bằng 0 nên hướng loạn → mũi tên xoắn, lật – lỗi C–Br → Br trước đây).

## 3. Đúng ở mọi góc nhìn
- Mặt phẳng chứa cung luôn **vuông góc hướng nhìn** (pháp tuyến = hướng đi × hướng nhìn): xoay góc nào cũng thấy
  một cung cong, không bao giờ thấy "que thẳng" hay một chấm.
- Mặc định cong **ra phía ngoài phân tử** (xa tâm khối), để không cắt qua các nguyên tử khác.

## 4. Chọn phương án bằng điểm phạt (thay cho sửa tay từng bài)
Mỗi khung, mỗi mũi tên thử 6 phương án: cong ra ngoài / vào trong × độ vồng ×1; ×1,5; ×2,2. Điểm phạt:
- xuyên quả cầu nguyên tử **không tham gia** và đang hiện: +10 mỗi điểm cắt;
- độ dài **nhìn thấy trên màn** < 0,8 Å: +10 × phần thiếu;
- cong vào trong: +0,6; vồng cao hơn chuẩn: +0,4 mỗi bậc;
- là phương án đã chọn ở khung trước: −0,8 (chống nhảy qua lại khi camera tự lắc).
Chọn phương án ít phạt nhất.

## 5. Hiện H
H có trong mũi tên của bước đang xem (bị lấy, nhận, cho e) **luôn hiện**, kể cả khi bật Ẩn H. Ẩn H chỉ ẩn H gắn C
không tham gia bước đó.

## 6. Kiểm thử bắt buộc khi thêm bài / sửa luật
Ở mỗi bước × 24 góc (12 hướng quanh trục đứng × nhìn từ trên 28° và dưới 28°), mỗi mũi tên phải:
1. đuôi cách chỗ chứa e ≤ 0,45 Å; 2. mũi cách mặt cầu đích ≤ 0,45 Å và không đâm vào trong;
3. dài nhìn thấy ≥ 0,6 Å; 4. không xuyên nguyên tử không tham gia.
Bài đạt khi ≥ 90% góc không lỗi; nhật ký `[CC_GOC]` ghi bước, góc và lỗi cụ thể để sửa **luật**, không sửa riêng bài.
