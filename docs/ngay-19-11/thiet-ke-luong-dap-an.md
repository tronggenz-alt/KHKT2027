# Thiết kế luồng đáp án chung – Chuỗi phản ứng · Mô hình 3D · Cơ chế (19/11/2026)

Yêu cầu: bấm Giải thì màn tự trượt xuống đáp án (như trợ lý AI: câu hỏi ở trên, câu trả lời hiện dần bên dưới); đáp án là
HÌNH ẢNH (sơ đồ cơ chế giấy, chuỗi vẽ thành hình, ảnh chụp mô hình 3D); nút "Mở lớn" mở bảng ~75% màn ở giữa (nền mờ phía sau)
để xem và tương tác; giải nhiều lần thì thành lịch sử cuộn được. Giữ chủ đề VERA, bo góc vừa phải (8–20 đơn vị canvas).

## 1. Tham khảo (rút từ cách các app phổ biến hiện câu hỏi → trả lời)

| App | Điều đáng học | Dùng ở VERA |
|---|---|---|
| ChatGPT / Claude | Câu hỏi là một "bong bóng" ngắn bên phải hoặc một dòng đậm; câu trả lời hiện dần ngay bên dưới, khung tự cuộn theo nội dung mới nhưng **dừng tự cuộn khi người dùng tự cuộn lên**; ô nhập dính đáy; mỗi câu trả lời có hàng nút nhỏ (sao chép, làm lại, thích/không thích); Claude mở **artifact** ở khung lớn bên cạnh/giữa để xem và tương tác mà không mất mạch hội thoại. | Ô đề dính đáy trang; thẻ đáp án có hàng nút; "Mở lớn" ≈ artifact (bảng 75% giữa màn). Tự cuộn chỉ khi người dùng đang ở gần đáy. |
| Gemini | Có trạng thái "đang nghĩ" hiện các bước đang làm; câu trả lời có ảnh/biểu đồ nằm trong thẻ; gợi ý câu hỏi tiếp dạng chip dưới câu trả lời. | Trạng thái đang giải hiện **tiến trình từng bước** (ví dụ "Đọc đề → Tìm chất → Kiểm RDKit → Vẽ"); chip "hỏi tiếp" dưới thẻ. |
| Perplexity | Câu hỏi thành **tiêu đề lớn** của khối trả lời; ngay dưới là hàng nguồn/ảnh; câu trả lời chia mục; câu hỏi tiếp theo nối thành chuỗi các khối dọc. | Mỗi lần giải = một **khối**: đề làm tiêu đề, ảnh đáp án to, tóm tắt, nguồn kiểm chứng (bảng đã kiểm chứng / RDKit / AI). |
| Photomath | Kết quả hiện **đáp án cuối trước** (thẻ to), rồi "Xem các bước" mở từng bước một; mỗi bước có giải thích gập/mở. | Thẻ đáp án đặt **hình đáp án + một dòng kết luận** lên đầu; các bước để trong "Mở lớn" hoặc gập/mở. |
| Wolfram Alpha | Đầu là "Input interpretation" (máy hiểu đề thế nào), sau đó các "pod" kết quả, mỗi pod có nút phóng to / tải ảnh / dữ liệu. | Dòng "Máy hiểu đề là: C₂H₄ → … (đọc từ ảnh)" ngay dưới đề; mỗi hình có nút Mở lớn / Xuất PNG. |
| Socratic / Google Lens | Chụp ảnh → khung ảnh đề hiện nhỏ ở đầu câu trả lời; thẻ kết quả trượt lên từ đáy. | Đề bằng ảnh thì hiện ảnh thu nhỏ trong khối câu hỏi. |

## 2. Nguyên tắc chọn dùng

1. **Một luồng cho cả 3 mục**: ô đề (3 tab Văn bản / Hình ảnh / Mẫu như hiện nay) → bấm Giải → khối mới gồm *câu hỏi* + *đáp án*.
2. **Câu hỏi ở trên, đáp án hiện dần bên dưới**, khối mới thêm vào cuối "dòng thời gian"; các khối cũ giữ nguyên = lịch sử.
3. **Đáp án là hình** đứng đầu thẻ (giấy cơ chế / chuỗi vẽ thành hình / ảnh 3D tĩnh), sau đó một dòng kết luận, rồi tóm tắt ngắn.
4. **Trạng thái đang giải** là khung xương (skeleton) của chính thẻ đáp án + dòng tiến trình từng bước có giờ đếm,
   không phải vòng xoay trơ trọi.
5. **Tự trượt xuống** khi bấm Giải (tới khối mới) và khi đáp án về (tới đầu hình) – nhưng **không giật** nếu người dùng
   đã tự cuộn lên đọc lịch sử (hiện nút nổi "↓ Đáp án mới").
6. **"Mở lớn"** = bảng 75% chiều rộng × 80% chiều cao ở giữa, nền mờ tối phía sau, đóng bằng nút ✕ / Esc / bấm nền.
   Trong bảng: Cơ chế có tờ giấy + 3D ⇄ giấy, thanh tốc độ, phóng toàn màn (dùng lại khung 3D đã làm); Chuỗi có hình chuỗi
   lớn + các bước gập/mở; Mô hình 3D có khung xoay được + Ẩn H + đặt lại góc.
7. **Hàng nút dưới mỗi thẻ** (bo 12, cỡ nhỏ): Mở lớn · Giải thích · Hỏi tiếp · Xuất PNG · Báo sai. "Hỏi tiếp" mở ô hỏi đáp
   ngay dưới thẻ (dùng /co-che/hoi-dap), câu trả lời cũng hiện dần.
8. **Chip gợi ý tiếp** dưới thẻ mới nhất (ví dụ "Vì sao không theo SN1?", "Xem cơ chế bước 2").
9. **Chuyển động**: trượt cuộn 0,45 s ease-out (cubic), thẻ hiện: mờ → rõ + trượt lên 16 đv trong 0,35 s, các phần trong thẻ
   lần lượt cách 60 ms; bảng lớn: nền mờ 0,2 s, bảng phóng từ 0,96 → 1 trong 0,28 s. Tất cả theo `Time.unscaledDeltaTime`
   (không phụ thuộc FPS), dùng MenuAnim sẵn có; không tạo đối tượng mới mỗi khung.
10. **Ảnh đáp án tạo thế nào** (không cần máy chủ vẽ thêm):
    - Cơ chế: chụp tờ giấy SoDoGiay vào RenderTexture (như Xuất PNG đã có) → Texture2D gắn RawImage trong thẻ.
    - Mô hình 3D: chụp một khung TrinhPhat3D ở góc tự chọn vào RenderTexture → Texture2D.
    - Chuỗi: vẽ bằng uGUI ngay trong thẻ (ô chất – mũi tên có điều kiện ở trên/dưới – ô chất…, xuống dòng khi dài); mở lớn thì
      thêm tên chất và loại phản ứng dưới từng mũi tên.
    Giải phóng Texture khi xoá khối (giới hạn lịch sử 20 khối/mục).

## 3. Bố cục (1920 × 1080 đơn vị canvas)

```
┌ thanh điều hướng (như hiện tại) ─────────────────────────────────────────────┐
│  ┌ dòng thời gian (cuộn) — rộng tối đa 1100, giữa màn ──────────────────────┐ │
│  │  [khối 1]  Đề: C2H4 → X → …                     (chữ đậm, ảnh đề nhỏ)    │ │
│  │            Máy hiểu: C2H4 → C2H5OH → CH3CHO                              │ │
│  │            ┌ thẻ đáp án ──────────────────────────────────────────────┐  │ │
│  │            │ [HÌNH ĐÁP ÁN rộng hết thẻ, cao ≤ 420]                     │  │ │
│  │            │ Kết luận một dòng · huy hiệu "đã kiểm chứng"             │  │ │
│  │            │ tóm tắt 2–3 dòng                                         │  │ │
│  │            │ [Mở lớn] [Giải thích] [Hỏi tiếp] [Xuất PNG] [Báo sai]     │  │ │
│  │            └──────────────────────────────────────────────────────────┘  │ │
│  │            chip gợi ý tiếp …                                            │ │
│  │  [khối 2] …                                                              │ │
│  └──────────────────────────────────────────────────────────────────────────┘ │
│  ┌ ô đề dính đáy: tab Văn bản | Hình ảnh | Mẫu · ô nhập · tuỳ chọn ▾ · [Giải →] ┐ │
└──────────────────────────────────────────────────────────────────────────────┘
```
Trang trống (chưa giải lần nào): ô đề đặt giữa màn kèm tiêu đề lớn và chip mẫu (như màn mở đầu của trợ lý AI); giải lần đầu thì
ô đề trượt xuống đáy và dòng thời gian xuất hiện.

## 4. Kế hoạch mã (lần sau làm tiếp)

1. `Scripts/Menu/GiaiBaiTap/DongThoiGian.cs`: ScrollRect + danh sách khối; `ThemKhoi(cauHoi, anhDe)` trả về `KhoiDapAn`;
   `TruotToi(rt)` (coroutine cuộn mượt, tôn trọng người dùng đang cuộn); nút nổi "↓ Đáp án mới".
2. `KhoiDapAn`: khung xương + tiến trình → `DatHinh(Texture/RectTransform)`, `DatKetLuan`, `DatTomTat`, hàng nút, chip.
3. `BangLon.cs`: bảng 75% có nền mờ, nhận nội dung (RectTransform) và trả lại khi đóng; Esc đóng.
4. `ChupHinh.cs`: chụp tờ giấy / 3D vào Texture2D (dùng lại mã Xuất PNG của CoCheMoDun và TrinhPhat3D).
5. Gắn vào 3 trang: thay mục "03 Kết quả" + "Khối nút" hiện tại bằng dòng thời gian + ô đề dính đáy; giữ tab nhập và tuỳ chọn
   (tuỳ chọn gập vào "Tuỳ chọn ▾" trong ô đề).
6. Cơ chế "Mở lớn": đưa khung 3D ⇄ giấy (CoCheMoDun) vào bảng 75% thay vì lớp phủ toàn màn.
7. Kiểm thử: KhktUiRunner – giải chuỗi 2 lần → 2 khối, cuộn tới khối mới, thẻ có hình; Mở lớn mở/đóng; Esc; Cơ chế mở lớn có
   3D ⇄ giấy. KhktCoCheVerify giữ nguyên.
