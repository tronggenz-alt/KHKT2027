# Báo cáo C2: Bảo vệ dữ liệu cơ chế, AI tại máy (RAG local), API giải chuỗi, thông số phần cứng phía máy chủ

Phiên "N18 Cơ chế" (Claude), nhánh `n18/co-che`, ngày 18/11/2026. **Kết quả: XONG C2.**
Đủ 8 gói `.verapack` và 3 điểm cuối. Không phải dùng mục "Nếu trễ": vẫn giữ dấu ẩn và phần chọn mô hình cho Windows/Linux.

## Phần 1: Bảo vệ và chống chép ý tưởng

**Nói thật về giới hạn.** App phải tự giải được gói nên khóa buộc phải nằm trong app. Người quyết tâm dịch ngược app (IL2CPP/Mono) vẫn lấy được
khóa và mở gói. Mã hóa phía app chỉ chặn được sao chép thông thường, tức là mở thư mục app rồi chép file JSON. Việc che khóa bằng XOR với các
hằng rải rác (làm ở C3) chỉ làm việc lấy khóa khó thêm một chút. Vì vậy có 3 lớp:

a) **Dữ liệu gốc không vào kho công khai.**
- Phần tách dữ liệu đã làm ở C1.
- `tests/test_khong_lo_du_lieu.py` (4 test) dò `git ls-files`:
  - tên tệp kín: kho mẫu, báo cáo bộ đo, bài demo, `.verapack`, `.key`, `khoa.bytes`, `.env`, `tai_khoan_admin.txt`, `email_tam_thoi.txt`,
    `data/ai_rieng/`, `data/geoip/`, chỉ mục RAG, nhật ký đóng gói;
  - nội dung giống khóa: PEM, Anthropic, Google, Groq, GitHub, OpenAI, có bỏ qua khóa giả kiểu `AIzaSyAAAA…` trong test;
  - dấu hiệu dữ liệu kín: cấu trúc `"khung_vo"` của kho mẫu, SMILES có map của bài HSG propellane;
  - `.gitignore` phải chặn được các đường dẫn gói và khóa.
  Kho hiện tại đạt cả 4 test.

b) **Gói mã hóa** `server/ai/co_che/dong_goi.py`, tạo `<ma>.verapack`:
- Cấu trúc gói: `"VERA1"` | phiên bản | salt 16 | IV 16 | AES-256-CBC(gzip(JSON gộp bai + 3d + giay)) | HMAC-SHA256 32.
- Khóa mã và khóa ký được dẫn xuất từ khóa gốc bằng HMAC(…, "VERA-ma"/"VERA-ky" ‖ salt).
- Mã hóa trước rồi mới ký. Khi mở gói, so HMAC bằng `hmac.compare_digest` (thời gian hằng) **trước** khi giải mã.
- **AES viết thuần Python** (`aes_thuan.py`), vì .venv không có `cryptography`/`pycryptodome` và tôi không thêm phụ thuộc. Mã đã kiểm bằng
  véc-tơ chuẩn FIPS-197 C.3 và NIST SP 800-38A F.2.5. Gói 60 KB mã hóa mất 0,45 s; gói thật chỉ 2–4 KB.
- Khóa 32 byte sinh ngẫu nhiên một lần, nằm ở `~/Developer/VERA_du_lieu_rieng/khoa/vera_co_che.key` (quyền 600).
- `--unity` chép khóa vào `Assets/Resources/CoChe/khoa.bytes` và 8 gói vào `Assets/StreamingAssets/CoChe/`. Cả hai đều git bỏ qua.
- **Dấu ẩn** nằm trong bản rõ đã mã hóa: `_d = {m: băm(máy dựng) 12 ký tự, n: ngày, id: mã gói ngẫu nhiên}`. Mỗi lần đóng gói, kể cả khi máy
  chủ trả `/co-che/bai/{ma}`, đều ghi vào `khoa/nhat_ky_dong_goi.jsonl` (thư mục riêng) để lần ra nguồn nếu gói bị rò.
- Định dạng chi tiết cho C# nằm trong `HOP_DONG_APP.md` mục 4, có gói thử cố định để kiểm chéo.
- Kết quả: 8 gói, mỗi gói 2,3–4,3 KB.

c) **"Bộ não" chỉ ở máy chủ.** Kho mẫu, tư duy vẽ, bộ đo, RAG và mô hình AI chạy ở máy chủ Python. App chỉ nhận hình đã dựng (gói) và lời
giải thích.

Test `tests/test_co_che_dong_goi.py` (10 test):
- véc-tơ AES chuẩn; đệm PKCS#7 với mọi độ dài;
- đóng gói rồi mở phải khớp, bản rõ không lộ trong gói;
- **sửa 1 byte** ở 6 vị trí (đầu gói, salt, IV, bản mã, HMAC) thì đều bị từ chối;
- sai khóa, gói bị cắt cụt, sai chữ "VERA1" đều bị từ chối;
- hai lần đóng gói cho kết quả khác nhau.

## Phần 2: AI tại máy (RAG local)

- `server/ai/cau_hinh_may.py` đọc phần cứng:
  - macOS: `sysctl`;
  - Windows: `psutil` + `wmic`;
  - Linux: `/proc/meminfo` + `nvidia-smi`.

  Rồi chọn mức:

  | Phần cứng | Mô hình | Cấu dạng ETKDG | Thời gian chờ |
  |---|---|---|---|
  | VRAM ≥ 24 GB | `vera-hoa-32b` | 60 | 90 s |
  | RAM ≥ 16 GB | `qwen2.5:7b-instruct` | 40 | 90 s |
  | còn lại | `vera-hoa-3b` | 20 | 120 s |

  Ghi đè được bằng `VERA_MO_HINH`, `VERA_RAM_GB`, `VERA_VRAM_GB`, `VERA_SO_CAU_DANG`, `VERA_THOI_GIAN_CHO`, `OLLAMA_URL`, `VERA_KEEP_ALIVE`.
  **Máy này:** Apple M2, RAM 8 GB → mức "thấp", mô hình `vera-hoa-3b`.
- Đã tải `ollama pull qwen2.5:3b-instruct` (1,9 GB). Trước khi tải ổ còn 12 GB (≥ 6 GB), sau khi tải còn 11 GB.
- **Modelfile `vera-hoa-3b`** (`server/ai/co_che/Modelfile.vera-hoa-3b`) dùng `num_ctx 8192` và cùng lời dặn SYSTEM với Modelfile 32B. Đã tạo
  bằng `ollama create`.
  - **Lưu ý:** `keep_alive` không đặt được trong Modelfile (Ollama không có tham số đó). Máy chủ gửi `"keep_alive": "5m"` trong mỗi yêu cầu
    `/api/chat`, nên tác dụng như nhau.
  - Sau khi thử đã chạy `ollama stop` (`ollama ps` rỗng).
- **RAG:** dùng lại `server/ai/rag.py` (BM25 + vector bge-m3, gộp RRF).
  - Nguồn mới (`server/ai/co_che/tri_thuc.py`):
    - 8 bài demo, mỗi bài 3 đoạn: tổng quan, các bước kèm nhánh sai, câu hỏi kèm lỗi hay gặp;
    - `quy_tac_co_che.md` gồm 9 mục quy tắc đã kiểm chứng (mũi tên cong, SN2, SN1, E2/Zaitsev, AE/Markovnikov, gốc tự do, SEAr,
      ester hóa Fischer, β-phân cắt).
  - Tất cả đọc từ thư mục riêng. Chỉ mục ở `~/Developer/VERA_du_lieu_rieng/co_che/rag_co_che.db`: 33 đoạn, có vector, dựng trong 5,5 s.
- **`/co-che/giai-thich`** (`giai_thich.py`):
  - RAG ưu tiên đoạn của bài đang mở;
  - gọi mô hình tại máy qua Ollama `/api/chat` trước, lỗi thì sang chuỗi nhà cung cấp trực tuyến có sẵn (`providers.pick()`);
  - lời dặn yêu cầu trích `[n]`, **không tự đề xuất cơ chế, không viết SMILES/JSON**, hỏi chất khác thì "thay chất vào mẫu";
  - nếu câu trả lời vẫn chứa cơ chế JSON thì cho qua `co_che.kiem_tra`; trượt thì thay bằng lời giải thích mẫu đã kiểm chứng, có test;
  - không có AI nào thì trả lời theo mẫu. Kết quả luôn kèm danh sách `nguon`.

### Bảng đo: mô hình 3B tại máy (Apple M2, 8 GB), qua HTTP thật tới máy chủ cổng 8010

| # | Bài | Câu hỏi | Thời gian (s) | Tự chấm theo SGK |
|---|---|---|---|---|
| 1 | sn2_ch3br | Vì sao OH⁻ tấn công C từ phía đối diện Br? | 12,9 (gồm nạp mô hình) | **Đúng một phần**: đúng ý orbital σ* và phía ít bị che, nhưng thêm câu sai "áp dụng tương tự quy tắc Markovnikov trong SN2" |
| 2 | sn1_tbubr | Giai đoạn nào quyết định tốc độ? | 10,8 | **Đúng**: bước 1, C–Br phân cắt dị li tạo cacbocation, có trích [1] |
| 3 | e2_2_brombutan | Sản phẩm chính với KOH/ethanol, vì sao? | 10,3 | **Đúng**: but-2-ene, Zaitsev, anti-periplanar |
| 4 | cong_hbr_propen | Vì sao ra 2-bromopropane? | 7,4 | **Đúng**: Markovnikov, cacbocation bậc hai bền hơn |
| 5 | este_hoa_fischer | O của nước lấy từ đâu? | 4,3 | **Đúng**: từ nhóm OH của acid, thí nghiệm ¹⁸O |

- **Thời gian:** trung bình 9,1 s/câu; câu đầu chậm hơn do phải nạp mô hình. Tối đa đo được 12,9 s.
- **RAM:** mô hình chiếm **2,2 GB** (`ollama ps`, 100% GPU, bộ nhớ dùng chung của Apple Silicon). Máy chủ Python khoảng 80 MB. Mô hình tự rời
  RAM sau 5 phút rảnh hoặc khi chạy `ollama stop`.
- **Tổng tự chấm: 4/5 đúng, 1/5 đúng một phần.**
- Nhận xét:
  - Mô hình 3B chưa trích nguồn `[n]` đều đặn: chỉ 1/5 câu có trích. Điểm cuối luôn trả `nguon` riêng để app hiện.
  - Mô hình 3B đôi khi chêm khái niệm sai chỗ (câu 1). Muốn chặt hơn thì dùng mức "vừa" (7B, cần RAM 16 GB) hoặc máy GPU (32B).

## Phần 3: API giải chuỗi

- Đã đọc cách CHEMISTRY giải chuỗi trên nhánh `chemistry`:
  - `parser.tach_chuoi`;
  - `giai_nhanh.SYSTEM_GIAI_GON` (một lần gọi AI, trả JSON gọn), RDKit kiểm "đúng công thức đề cho";
  - route `/api/giai` (SSE, có mức độ thông minh).
- Phần lõi viết lại gọn trong `server/ai/co_che/giai_chuoi.py`. **Không import chéo** `~/CHEMISTRY_xem`, không sửa nhánh chemistry, không đụng
  LaunchAgent.
- Hai tầng:
  1. **Tại máy, không cần mạng:** bảng 24 chất thường gặp và bảng 22 biến đổi đã kiểm chứng, gồm phản ứng, điều kiện, loại, mã cơ chế demo
     và mức khớp (`dung_mau` hoặc `cung_loai`).
  2. **Chất hoặc bước lạ:** nhờ AI (tại máy trước, trực tuyến sau). SMILES phải hợp lệ trong RDKit và đúng công thức đề cho; ký hiệu như X,
     A1 không bị coi là công thức. Mọi phần do AI đưa ra đều ghi `chua_kiem_chung`, kể cả bước có trong bảng mà một đầu là chất do AI nhận.
- **3 chuỗi mẫu chạy được khi không có mạng** (có test bảo đảm không gọi AI):
  - `C2H4 → C2H5OH → CH3CHO → CH3COOH → CH3COOC2H5`: bước cuối là ester hóa, gắn bài `este_hoa_fischer`.
  - `CH4 → CH3Cl → CH3OH → HCHO`: gắn bài `clo_hoa_metan` và `sn2_ch3br` (cùng loại).
  - `C3H6 → CH3CHBrCH3 → CH3CH(OH)CH3 → CH3COCH3`: gắn bài `cong_hbr_propen`, `sn2_ch3br` (cùng loại).
  - Ví dụ trong đề `C2H4 -> C2H5OH -> CH3CHO -> CH3COOH` cũng giải được ở tầng tại máy.
- **Máy chủ:** `server/ai/co_che/api.py` (router `/co-che`) và `may_chu.py` (FastAPI riêng).
  - **Cổng:** LaunchAgent CHEMISTRY `vn.giaichuoihuuco.app` chạy `--port 8000`, trùng cổng mặc định của máy chủ lab `server/ai/main.py`.
    Vì vậy máy chủ cơ chế của VERA chạy **cổng 8010**, đổi được bằng `VERA_CO_CHE_PORT`.
  - URL ghi trong `HOP_DONG_APP.md` mục 5 "Máy chủ cơ chế".
  - Đã chạy thật: `/co-che/suc-khoe`, `/co-che/bai` (8 bài), `/co-che/bai/sn2_ch3br` (200, 2.326 byte), `/co-che/giai-chuoi`,
    `/co-che/giai-thich` đều trả 200.
  - Chưa gắn router vào `server/ai/main.py` (cổng 8000). Lý do: middleware đăng nhập `server/lab/dang_nhap.py` chặn mọi đường dẫn không có
    trong bảng `QUYEN`, mà sửa bảng quyền nằm ngoài phạm vi. Muốn gộp thì thêm `("GET"/"POST", "/co-che/...") : UNITY` vào `QUYEN` và
    `app.include_router(co_che.api.router)`.

## Kiểm thử

- `tests/test_co_che_api.py` (14 test, TestClient, khép kín: thư mục riêng, build và khóa đều tạm; AI giả): sức khỏe, danh sách bài, gói giải
  được, 404, 3 chuỗi mẫu không gọi AI, ví dụ của đề, chuỗi lạ ghi "chưa kiểm chứng", lỗi 422, giải thích theo mẫu khi không có AI, AI tại máy,
  AI đề xuất cơ chế sai thì bị thay, cùng 3 test `cau_hinh_may`.
- `tests/test_co_che_dong_goi.py`: 10 test. `tests/test_khong_lo_du_lieu.py`: 4 test.
- `python -m unittest discover -s tests`: **419 test**, 77 FAIL và 100 ERROR, **đúng như mốc gốc**, đều thuộc 5 tệp hỏng sẵn (test_dang_nhap,
  test_lop_hoc, test_truc_tiep, test_buoi_hoc, test_trinh_chieu). **Không có test hỏng mới.** Số test đạt: 214 → 242.

## Ghi chú cho C3

- Gói đã có ở `Phong thi nghiem ao/Assets/StreamingAssets/CoChe/*.verapack`, khóa ở `Assets/Resources/CoChe/khoa.bytes`.
- Định dạng gói xem `HOP_DONG_APP.md` mục 4, URL và điểm cuối xem mục 5.
- Khi dựng Unity cần chép thêm hai thư mục này sang bản sao `~/AppBuild/VERA_claude` nếu script đồng bộ không tự chép tệp bị git bỏ qua.
