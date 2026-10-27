# Phòng lab 300 m², 720 hóa chất, bộ máy hóa học và trợ lý AI

## Mặt bằng mới

| Khu | Vị trí | Có gì |
| --- | --- | --- |
| Phòng thí nghiệm | x 0–20, z 0–15 (300 m²) | 9 bàn học sinh (3 hàng × 3 dãy), bàn giáo viên, bảng, màn hình tương tác |
| Bàn 1 (phím 1) | hàng đầu, sát tường nam | bộ chuẩn độ đầy đủ |
| Bàn 4 (phím 2) | hàng đầu, dãy giữa | bộ định tính: 6 ống nghiệm, cốc, bình nón, đèn cồn, 9 thuốc thử hay dùng |
| Khu thiết bị phân tích (phím 3) | tường nam, phía đông | cân phân tích 0,0001 g, cân điện tử, cân đĩa, cân bàn, máy đo pH, độ dẫn, oxi hòa tan, máy pH cầm tay, nhiệt kế, giấy pH, máy quang phổ, máy so màu, máy ly tâm |
| Bàn đun nóng - tổng hợp (phím 4) | giữa phía đông | bếp khuấy từ, bếp bọc + hồi lưu, bộ chưng cất, đèn Bunsen, bộ chiết, lọc chân không, bình Kíp, nung chén sứ, cô cạn, buret tự động |
| Tủ hút (phím 5) | tường bắc, phía đông | 3 tủ hút có công tắc quạt; khí độc sinh trong tủ được hút đi |
| Khu an toàn (phím 0) | cạnh cửa ra vào | vòi rửa mắt, vòi sen khẩn cấp, bình chữa cháy, hộp y tế, xô cát, tủ đồ bảo hộ, nội quy |
| Phòng hóa chất (phím 6) | x 20–27, sau vách kính | 27 kệ/tủ theo nhóm, sơ đồ kho, máy tra cứu, bàn pha chế |
| Dãy phòng phía nam (phím 7, 8) | z −9…0, cùng tầng | phòng máy phân tích hiện đại, phòng tự động hóa & robot: xem [07](07-web-quan-ly-robot-phong-moi.md) |

Dựng lại: menu **KHKT → Dựng phòng thí nghiệm** (hoặc chạy builder ở chế độ batch).

## Danh mục hóa chất và thiết bị

`tools/chuyen_hoa_chat.py` chuyển hai file CSV (`data/chem/nguon/`) thành `data/chem/hoa_chat.json`, `thiet_bi.json` và bản Unity trong `Assets/Lab/Data/`.

```bash
python tools/chuyen_hoa_chat.py data/chem/nguon/hoa_chat.csv data/chem/nguon/thiet_bi.csv
```

Với mỗi chất, script suy ra: tên có dấu, nhóm bảo quản, vị trí (kệ · tầng · ô), thành phần ion khi hòa tan (tách cation/gốc axit theo bảo toàn điện tích), khối lượng mol, nồng độ gợi ý, nhãn tính chất hữu cơ (không no, anđehit, poliol, phenol...), nhãn nguy hiểm.

Sắp xếp kệ: tủ axit (K01), tủ bazơ (K02), chất oxi hóa (K03), muối theo gốc axit (halogenua, sunfat/sunfua, nitrat, cacbonat/silicat, photphat, muối khác, muối hữu cơ: K04–K11, tường đông), oxit, kim loại, phi kim, chỉ thị, thuốc thử, dung dịch pha sẵn, tủ chất dễ cháy (hiđrocacbon, ancol/anđehit, este/dung môi), hữu cơ khác, cacbohiđrat/protein, polime, hóa chất đời sống, bình khí (K12–K27, dãy giữa). Mỗi chất là một lọ thật đúng ô, nhãn có công thức và dải màu nhóm.

## Bộ máy hóa học (`Assets/Scripts/Simulation/Chem/`)

C# thuần, không phụ thuộc Unity, kiểm thử độc lập:

```bash
powershell -ExecutionPolicy Bypass -File tools/chay_kiem_thu_hoa.ps1
```

- **pH** từ bảo toàn điện tích: axit chỉ để lại gốc axit, bazơ chỉ để lại cation; axit/bazơ yếu lưu theo tổng (cacbonat, photphat, sunfua, amoni, axetat, oxalat, xitrat...). Trộn theo thứ tự nào cũng đúng.
- **Kết tủa** theo Ksp: hiđroxit kim loại (lưỡng tính thì tan lại trong kiềm dư), 55 cặp ion (AgCl, BaSO₄, CaCO₃, PbI₂, CuS, xanh Phổ...), xếp theo độ tan (chuẩn độ Mohr ra đúng thứ tự).
- **Phức:** [Cu(NH₃)₄]²⁺, [Ag(NH₃)₂]⁺, [Zn(NH₃)₄]²⁺, [Fe(SCN)]²⁺, đồng - poliol (glixerol, glucozơ, Fehling, Benedict).
- **Khí** thoát khi vượt độ tan: CO₂, SO₂, H₂S, NH₃ (đun), HCN, NO, Cl₂.
- **Oxi hóa - khử:** KMnO₄ (axit, trung tính, kiềm), Cr(VI), halogen, Fe³⁺, H₂O₂, nước Gia-ven, HNO₃, iođat, pesunfat...
- **Theo thời gian:** kim loại tan trong axit, kiềm (Al, Zn), nước (Na, K, Ca); kim loại đẩy kim loại; oxit tan; CaC₂, Al₄C₃, Mg₃N₂, Na₂O₂ với nước; H₂O₂ phân hủy (MnO₂); Fe(OH)₂ hóa nâu; thiosunfat + axit; tráng bạc, Fehling, protein đông tụ, xà phòng hóa, ninhiđrin khi đun.
- **Màu** theo định luật Beer (3 kênh), chất chỉ thị theo pH, độ đục kết tủa, lớp cặn lắng, lớp hữu cơ tách lớp, gương bạc.
- **Nhiệt:** trung hòa, hòa tan (NaOH, NH₄NO₃...), kim loại với nước; pha loãng H₂SO₄ đặc (đổ nước vào axit là sự cố).

Mỗi hiện tượng sinh một sự kiện (tên, phương trình, giải thích, mức nguy hiểm). Unity chú thích ngay bằng card; AI chỉ diễn giải thêm.

## Điều khiển mới

| Phím | Việc |
| --- | --- |
| Rê chuột | Nhìn theo chuột (tâm ngắm giữa màn hình) |
| Tab / giữ Alt | Bật-tắt nhìn theo chuột / tạm thả con trỏ |
| B | Tra cứu hóa chất (tên có dấu hay không, công thức, nhóm, số thứ tự) |
| T | Trò chuyện với robot trợ lý |
| Esc | Menu: thí nghiệm tự do, chọn bài chuẩn độ, khám phá |
| 1–9 | Bàn chuẩn độ, bàn định tính, thiết bị phân tích, bàn đun nóng, tủ hút, phòng hóa chất, phòng máy phân tích hiện đại, phòng tự động hóa & robot, toàn cảnh |
| 0 | Lần lượt bàn giáo viên, khu an toàn |
| ← → / PageUp PageDown | Xoay / ngẩng cúi bằng phím (khi touchpad bị Windows khóa lúc đang nhấn phím) |

Cầm lọ hóa chất rồi trỏ vào vật chứa: dung dịch thì **giữ để rót**, chất rắn thì **nhấn để xúc 1 thìa**, lọ nhỏ giọt thì **nhấn để nhỏ giọt**, bình khí thì **giữ để sục khí**, giấy thử thì **nhấn để nhúng**, đầu đo thì số hiện ngay. Bếp, đèn cồn, Bunsen, máy khuấy, máy ly tâm, công tắc tủ hút: bấm để bật/tắt. Cân: bấm để trừ bì.

## Chuẩn độ tự chọn

Mặc định là **thí nghiệm tự do** (bàn 1 vẫn bày sẵn bộ chuẩn độ, ai muốn thì làm). Menu → **Chuẩn độ**: chọn chất cần chuẩn (HCl, H₂SO₄, HNO₃, CH₃COOH, H₂C₂O₄, NaOH, KOH, NH₃, FeSO₄), dung dịch chuẩn (NaOH, HCl 0,1 M; KMnO₄ 0,02 M), chỉ thị (phenolphtalein, metyl da cam, bromothymol xanh) và nồng độ mẫu (ẩn hoặc 0,05/0,1/0,2 M). Thanh bước, đương lượng, điểm cuối và bảng kết quả tự đổi theo bài.

## AI đám mây và robot trợ lý

Máy chủ `server/ai/main.py` (FastAPI). Khóa API chỉ đặt trên máy chủ.

```bash
pip install -r requirements.txt
set ANTHROPIC_API_KEY=...        (Claude, mặc định claude-opus-5-5)
uvicorn server.ai.main:app --host 127.0.0.1 --port 8000
```

Hoặc dùng API kiểu OpenAI: `LLM_BASE_URL`, `LLM_MODEL`, `LLM_API_KEY`. Chỉ định rõ bằng `AI_PROVIDER=claude|openai_compat`.

| Điểm cuối | Việc |
| --- | --- |
| `GET /suc-khoe` | Có AI không, nhà cung cấp, mô hình |
| `POST /chu-thich` | Giải thích 1–2 câu cho một hiện tượng, dựa đúng dữ kiện mô phỏng (có cache) |
| `POST /tro-ly` | Trả lời và danh sách hành động cho robot (đầu ra JSON theo lược đồ). Tên hóa chất AI nêu được máy chủ tra thành số thứ tự trong danh mục |

Robot: bay theo người dùng; lấy lọ trong kho mang ra bàn; chỉ vị trí bằng cột sáng; dọn bàn; bật tủ hút; tắt lửa; dẫn tới vòi rửa mắt. Khi có sự cố (khí độc ngoài tủ hút, kim loại kiềm bốc cháy, đổ nước vào axit đặc, tràn dung dịch ăn mòn), robot tự bay tới, cảnh báo và xử lý. Không có máy chủ AI thì robot vẫn hiểu lệnh cơ bản ("lấy CuSO4 ra bàn", "KMnO4 ở đâu", "dọn bàn", "bật tủ hút", "giải thích hiện tượng").

## Kiểm thử

| Bộ | Lệnh | Kết quả |
| --- | --- | --- |
| Bộ máy hóa học (C#) | `tools/chay_kiem_thu_hoa.ps1` | 46/46 |
| Danh mục, máy chủ AI, thư viện cũ (Python) | `python -m unittest tests.test_hoa_chat tests.test_ai_server tests.test_khkt` | 34/34 |

Ảnh chạy thử tự động (chuẩn độ sai số 0,17 %; AgCl, Cu(OH)₂, [Fe(SCN)]²⁺ ở bàn định tính; tra cứu KMnO₄ ở K03-6-05; robot mang CuSO₄ ra bàn; Cu + HNO₃ đặc sinh NO₂): [docs/anh-demo/v4/](anh-demo/v4/).
