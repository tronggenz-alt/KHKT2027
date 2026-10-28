# BẢNG ĐIỂM ĐÁNH GIÁ AI 3D BIO-TUTOR (EVALUATION REPORT)

Thời gian đánh giá: 40 câu hỏi chuẩn hóa (SGK Sinh học GDPT 2018).

## 1. SO SÁNH TỔNG QUAN THEO 4 TIÊU CHÍ

| Tiêu chí đánh giá (Thang điểm 100) | AI CŨ (Chưa có RAG & Tool) | AI MỚI (RAG + Function Calling) | Mức tăng trưởng |
| :--- | :---: | :---: | :---: |
| **1. Đúng kiến thức SGK & Tránh bẫy** | **96.6** | **97.8** | **+1.1 điểm** |
| **2. Đủ ý bắt buộc & Thuật ngữ chuẩn** | **7.5** | **94.2** | **+86.7 điểm** |
| **3. Điều khiển 3D & Focus đúng Mesh** | **8.0** | **100.0** | **+92.0 điểm** |
| **4. Đúng độ dài (≤260 từ) & Khung HTML** | **50.0** | **98.8** | **+48.8 điểm** |
| 🏆 **ĐIỂM TỔNG HỢP (COMPOSITE SCORE)** | **45.2 / 100** | **97.3 / 100** | **+52.1 ĐIỂM** |

---

## 2. ĐIỂM THEO TỪNG CHỦ ĐỀ SINH HỌC

| Chủ đề kiểm thử | AI CŨ | AI MỚI | Chênh lệch |
| :--- | :---: | :---: | :---: |
| Bai Tiet           |   42.5 |  100.0 | + 57.5 |
| Bay Thi            |   42.5 |  100.0 | + 57.5 |
| Co Xuong           |   42.5 |  100.0 | + 57.5 |
| Ho Hap             |   42.5 |  100.0 | + 57.5 |
| Mien Dich          |   46.2 |  100.0 | + 53.8 |
| Ngoai Le           |   34.6 |   70.0 | + 35.4 |
| Noi Tiet           |   42.5 |  100.0 | + 57.5 |
| Sinh San           |   45.0 |  100.0 | + 55.0 |
| Than Kinh          |   51.7 |   90.2 | + 38.6 |
| Tieu Hoa           |   44.5 |  100.0 | + 55.5 |
| Tuan Hoan          |   55.7 |   97.5 | + 41.8 |

---

## 3. CÁC ĐIỂM CẢI TIẾN ĐỘT PHÁ CỦA AI MỚI
1. **Triệt tiêu bẫy đề thi THPT**: AI mới phân biệt chính xác máu giàu/nghèo O2 ở động mạch phổi và tĩnh mạch phổi, tỉ lệ các pha trong chu kì tim (0,1s - 0,3s - 0,4s), không còn bịa đặt số liệu.
2. **Điều khiển mô hình 3D chính xác**: 100% câu hỏi nhắc đến bộ phận cơ thể đều gọi đúng `focus`, `isolate`, `label` và meshId hợp lệ, loại bỏ hoàn toàn tình trạng camera bay lệch không gian.
3. **Cập nhật Vitals động**: Tự động điều chỉnh `setVitals` (('bpm', 'rr')) khi thảo luận các trạng thái vận động thể thao hoặc hồi hộp.
4. **An toàn y tế & Sư phạm chuẩn mực**: Từ chối lịch sự và khuyên đến cơ sở y tế khi học sinh hỏi triệu chứng bệnh cá nhân, từ chối câu hỏi ngoài môn sinh học.
