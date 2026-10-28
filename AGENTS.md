# Luật làm việc chung (Claude và Gemini đều phải tuân theo)

## ⚠️ Phòng Lab Sinh học 3D – Gemini và Claude đều được sửa trực tiếp (người dùng quyết định 26/10/2026)
Bỏ luật "chỉ Claude ghi" ngày 17/10/2026. `frontend/biology-lab-3d*.html`, `frontend/sinh-hoc/`, `backend/sinh_hoc_ai.py` nay sửa trực tiếp được,
nhưng phải phối hợp để không đè lên nhau (lý do từng có luật cũ):
- Trước khi sửa: `git status` + `git log -3 -- <file>`. File đang có thay đổi CHƯA COMMIT của AI khác → dừng, báo người dùng, không sửa đè.
- Tạo điểm phục hồi trước; sửa xong chạy `.venv/bin/python -m pytest -q tests/test_sinh_hoc*.py tests/test_kien_thuc_sinh_hoc.py` rồi commit ngay, không để dở.
Chi tiết: `.agents/rules/phong_lab_3d_phoi_hop.md`. Hàng đợi `.agents/hang-doi/sinh-hoc/` không còn bắt buộc.

## 🧪 Chuẩn Biện Luận Hoá Hữu Cơ – BẮT BUỘC (Claude, Gemini & toàn bộ AI)
Chi tiết: `.agents/rules/bien_luan_chat_huu_co.md`. Khi giải, suy luận hoặc hướng dẫn xác định chất hữu cơ (chất ẩn X, Y, Z, chuỗi phản ứng, este/peptit/chất đa chức, đề HSG):
- Trình bày ngắn gọn bằng ký hiệu `->`, không văn mẫu, đi thẳng vào bản chất hoá học.
- Bắt buộc tuân thủ 4 bước tuần tự:
  1. **$\Delta$ & Nhóm chức**: Tính $\Delta = \frac{2C+2+N-H-X}{2}$. Dịch nhanh dữ kiện: `[Dữ kiện] -> [Cấu tạo/Nhóm chức/Phản ứng]`.
  2. **Truy vết sơ đồ**: Biện luận từng nấc (`Chất A -> Chất B`). Chỉ nêu tác nhân, sự biến đổi khung C/nhóm chức và cơ chế trọng tâm (đóng/mở vòng, chuyển vị, cắt mạch).
  3. **Tổng hợp CTCT**: Liệt kê rõ CTCT thu gọn của tất cả các chất chưa biết.
  4. **Kiểm chứng (Bắt buộc)**: Check nhanh kết quả với [CTPT, $\Delta$, số mol/khối lượng, lập thể/đồng phân]. Nếu lệch dữ kiện -> tự sửa lại cấu trúc trước khi chốt.

## Quy tắc vận hành & Tự chủ (Toàn bộ dự án)
1. **Làm việc tự chủ tối đa**: Không dừng lại hỏi xác nhận cho từng bước nhỏ, không chờ duyệt kế hoạch. Tự quyết định hợp lý rồi làm tiếp đến khi xong toàn bộ yêu cầu.
2. **Gom các thay đổi**: Sửa nhiều file trong một lượt, gộp các lệnh terminal liên tiếp thành một lệnh (dùng `&&`) để giảm số lần chạy lệnh.
3. **Chỉ dừng lại hỏi người dùng trong các trường hợp sau**:
   - Xóa file/thư mục hoặc xóa dữ liệu trong CSDL
   - `git push`, deploy lên web
   - Cần khóa API hoặc thông tin người dùng chưa cung cấp
   - Hai cách làm khác nhau rõ rệt về tính năng mà người dùng chưa nói rõ
4. **Điểm phục hồi**: Trước khi bắt đầu một tính năng lớn, tự tạo git commit để có điểm quay lại nếu hỏng.
5. **Tự xử lý lỗi**: Khi gặp lỗi, tự đọc log, tự sửa và chạy lại tối đa 3 lần trước khi báo người dùng.
6. **Báo cáo ngắn gọn**: Làm xong thì báo ngắn gọn: đã làm gì, file nào thay đổi, cách chạy thử.

## Quy tắc kỹ thuật & Quy trình bàn giao
7. Trước khi làm, đọc [HANDOFF.md](file:///Users/trantrong/Documents/Antigravity/HANDOFF.md) để biết tiến độ.
8. Sau mỗi phần việc nhỏ (khoảng 10–15 phút), cập nhật [HANDOFF.md](file:///Users/trantrong/Documents/Antigravity/HANDOFF.md) gồm:
   - ĐÃ XONG: các phần đã hoàn thành
   - ĐANG LÀM: việc đang dở, file nào, dòng nào
   - VIỆC TIẾP THEO: danh sách theo thứ tự ưu tiên
   - LỖI CÒN TỒN: lỗi chưa sửa và hướng xử lý
   - CÁCH CHẠY: lệnh để chạy app và chạy test
9. Sau khi cập nhật [HANDOFF.md](file:///Users/trantrong/Documents/Antigravity/HANDOFF.md) thì chạy: `git add -A && git commit -m "<mô tả ngắn>"`
   (Nếu git từ chối vì vùng Phòng Lab 3D: bỏ thay đổi ở vùng đó và gửi đề xuất cho Claude – xem mục ⚠️ ở đầu file.)
10. Không sửa file `.env`, không in API key ra màn hình.
11. Giữ nguyên kiến trúc trong [README.md](file:///Users/trantrong/Documents/Antigravity/README.md); muốn đổi lớn thì ghi lý do vào [HANDOFF.md](file:///Users/trantrong/Documents/Antigravity/HANDOFF.md).