# DÀN Ý – Mở rộng CHEMISTRY thành web học trọn môn Hoá THPT (GDPT 2018)

> Tài liệu định hướng cho mọi AI làm việc trong dự án (Gemini/Antigravity, Claude). Đọc kèm AGENTS.md, CLAUDE.md, HANDOFF.md.
> Prompt thực thi: `PROMPT_PHONG_THI_NGHIEM_AO.md` (Giai đoạn 1 – làm trước). Các giai đoạn sau sẽ có file prompt riêng.
> Giai đoạn 1b: `PROMPT_PHONG_LAB_KHONG_GIAN_MO.md` – đổi 5 bàn rời thành một căn phòng 3D không gian mở có phân khu + nhân vật đi lại (bản Web 3D).
> Giai đoạn 1c: `PROMPT_PHONG_LAB_DIEU_KHIEN_GAME.md` – sửa lỗi camera, nội thất giống lab thật, phòng con vách kính, điều khiển kiểu Genshin (góc 1/3), tới bàn bay vào cận cảnh.
> Giai đoạn 2 (lab): `PROMPT_LAB_UNITY_TRON_GOI.md` – MỘT file chạy liên tục 12 giai đoạn: xuất phòng lab thành app Unity riêng trên máy tính (Mốc 1) → bảng tuần hoàn 118 nguyên tố, toà nhà nhiều phòng trần cao, dụng cụ đúng kích thước, nhân vật giáo sư, hiệu năng đa nhân (Mốc 2). Tiến độ ở `TIEN_DO_APP_LAB.md`. (Thay cho `PROMPT_APP_LAB_MAY_TINH.md` + `PROMPT_UNG_DUNG_LAB_UNITY.md`, hai file cũ chỉ để tham khảo.)
> Lab trong app máy tính: `PROMPT_NHUNG_LAB_UNITY_VAO_APP.md` – app CHEMISTRY mở thẳng phòng lab Unity mới (bản WebGL đóng gói trong app, giao thức chemlab://); máy yếu/lỗi tự về Three.js; nút "Mở cửa sổ mượt" mở PhongLabHoa3D.app. Web không đổi.
> Tự tải + đưa nhân vật vào app: `PROMPT_TAI_VA_DUA_NHAN_VAT_VAO_APP.md` – mẫu A đã tải (~/Downloads/MoHinh3D), Gemini tự thao tác Mixamo (người dùng chỉ đăng nhập 1 lần) để gắn xương + tải 47 chuyển động (manifest có thêm mục `mo_rong` 26 cái), rồi đưa vào lab Unity (Animation Rigging: nhìn theo, tay chạm đúng, chân bám sàn) và trợ lý web.
> Nhân vật + chuyển động giao diện: `PROMPT_NHAN_VAT_VAO_APP_ANIMATE_UI.md` – đưa nhân vật "Nhà nghiên cứu" (manifest `tools/du_lieu/nhan-vat-nha-nghien-cuu.json`, Mixamo) vào lab Unity + trợ lý góc màn hình trên web/app; MCP shadcn + skill animate-ui CHỈ dùng cho chuyển động giao diện (Motion), không điều khiển nhân vật 3D. Không chạy cùng lúc với prompt tỉ lệ/bố cục bên dưới.
> Tỉ lệ & bố cục lab Unity: `PROMPT_LAB_UNITY_TI_LE_BO_CUC_THAT.md` – chạy SAU prompt nhúng; đồ đạc đúng kích thước thật so với người 1,70 m, không gian nới kiểu game Genshin cho camera góc 3, mỗi phòng đủ bàn đảo / bàn GV / tủ hút / góc an toàn như phòng lab thật; bảng chuẩn kich-thuoc-chuan.json + test đo tự động; đồ hoạ chân thực kiểu game hiện đại (bake ánh sáng + area light trần, light/reflection probe từng phòng, vật liệu PBR đúng số, hậu kỳ, HDRI ngoài cửa sổ).
> Tối ưu giao diện: `PROMPT_TOI_UU_CHUYEN_MON_CO_CHE_CHUOI.md` – sửa lại và làm nốt sau commit 3ad555c: nút chuyển Hoá ⇄ Sinh, cơ chế "vẽ trước – giải thích chạy nền", ảnh sơ đồ chuỗi 5–6 chất/hàng.
> Mục Hoá lý: `PROMPT_HOA_LY_DONG_CO_NHIET_2D.md` – mục mới `/hoa-hoc/hoa-ly` (lưới mô phỏng, tên đọc từ danh-muc.json) + mô phỏng 2D đầu tiên "Động cơ nhiệt 4 kỳ" (TÊN TẠM, người dùng đặt sau): chu trình Otto dựng lại từ video, kéo bánh đà / pít-tông / chấm p–V / thanh thời gian, thêm T–s, công, nhiên liệu từ ΔfH (Hoá 10), định luật I, đề máy, Diesel. SVG thuần, không thêm thư viện. Chạy SAU khi việc nhân vật đã commit.
> Hoàn thiện Hoá lý: `PROMPT_HOA_LY_HOAN_THIEN.md` – chạy SAU prompt Động cơ nhiệt (Gemini đã xong, commit ca5d165). GĐ0 phóng to hình Động cơ nhiệt (đường p–V đang 149×232 px) + sửa hạt khí lệch ra ngoài xi-lanh, tách khung dùng chung `chung/`; GĐ1 thư viện "Công thức Hoá lý" (`cong-thuc.json`, ≥ 30 công thức, nút **Chứng minh** từng bước, MathJax offline); GĐ2–4 ba mô phỏng 2D kéo chuột: Khí lý tưởng · Cân bằng & Le Chatelier (ống tiêm NO₂/N₂O₄) · Tốc độ phản ứng & Ea (va chạm hạt thật, giản đồ năng lượng kéo được).

## 0. Hiện trạng
- Web mạnh ở **Hữu cơ**: 4 công cụ trên trang chủ – Giải chuỗi phản ứng · Tìm cấu tạo chất · Cơ chế phản ứng · Mô hình 3D phân tử.
- Đã có khuôn mẫu làm bên **Sinh học**, dùng lại cho Hoá: lộ trình JSON (`config/lo_trinh_sinh_hoc.json` + `backend/lo_trinh.py`),
  kiến thức mỗi bài một file JSON (`frontend/sinh-hoc/kien-thuc/`), thuật ngữ chuẩn (`thuat-ngu.json`), test kiểm dữ liệu.
- Còn thiếu: **Đại cương – Hoá lí**, **Vô cơ**, và lớp "học" (bài học, lộ trình, luyện đề, gia sư).

## 1. Mục tiêu & đối tượng
- Học sinh lớp 10–12 theo CT GDPT 2018; ôn thi TN THPT định dạng mới (Phần I: 4 lựa chọn · Phần II: Đúng/Sai 4 ý · Phần III: Trả lời ngắn); HSG.
- Ba trục nội dung: **Đại cương & Hoá lí** · **Vô cơ** · **Hữu cơ**.

## 2. Cây kiến thức (đối chiếu lại với bộ SGK đang dùng: KNTT / CTST / Cánh Diều)
| Lớp | Chương |
|---|---|
| 10 | Cấu tạo nguyên tử · Bảng tuần hoàn · Liên kết hoá học · Oxi hoá – khử · Năng lượng hoá học (enthalpy) · Tốc độ phản ứng · Nhóm VIIA (halogen) |
| 11 | Cân bằng hoá học (Kc, Le Chatelier, điện li, pH, chuẩn độ) · Nitrogen – Sulfur · **Đại cương hữu cơ** · **Hydrocarbon** · **Dẫn xuất halogen – Alcohol – Phenol** · **Carbonyl – Carboxylic acid** |
| 12 | **Ester – Lipid** · **Carbohydrate** · **Hợp chất chứa nitrogen** · **Polymer** · Pin điện & điện phân · Đại cương kim loại · Nhóm IA – IIA · Kim loại chuyển tiếp & phức chất |
| Chuyên đề | Cơ chế phản ứng hữu cơ (12), phân bón, dầu mỏ, phức chất, phòng chống cháy nổ… |

(**Đậm** = phần Hữu cơ, làm trong Giai đoạn 1.)

Khung chung của MỘT bài học:
`Mục tiêu → Khái niệm cốt lõi → Tính chất & phương trình → Bản chất / cơ chế → Dạng bài & phương pháp → Thí nghiệm → Liên hệ thực tế → Điểm thi & bẫy → Trắc nghiệm 3 dạng → Gợi ý hỏi AI`

## 3. Các phân hệ của web
| # | Phân hệ | Phục vụ | Trạng thái | Giai đoạn |
|---|---|---|---|---|
| 1 | Giải chuỗi phản ứng | Hữu cơ | Có | – |
| 2 | Tìm cấu tạo chất | Hữu cơ | Có | – |
| 3 | Cơ chế phản ứng | Hữu cơ, chuyên đề 12 | Có (v2 đang làm) | – |
| 4 | Mô hình 3D | Liên kết, đồng phân | Có → mở rộng VSEPR, orbital, tinh thể | 2 |
| 5 | Phòng thí nghiệm ảo (an toàn PTN, chuẩn độ, nhận biết ion, pin Galvani, điện phân) – bản Web 3D + bản Unity 3D do admin bật/tắt | Thực hành | Mới | **1** |
| 6 | Thư viện bài học `/hoa-hoc` | Toàn bộ | Mới | 2 (Hữu cơ trước) |
| 7 | Lộ trình học Hoá + tiến độ | Toàn bộ | Mới | 2 (Hữu cơ trước) |
| 8 | Trang chủ chia 3 nhóm Đại cương · Vô cơ · Hữu cơ | Điều hướng | Mới | 2 |
| 9 | Bảng tuần hoàn tương tác | Lớp 10 | Mới | 3 |
| 10 | Cân bằng phương trình / oxi hoá – khử | 10–12 | Mới | 3 |
| 11 | Máy tính hoá học (mol, C_M, pH, Kc, ΔH, tốc độ, Faraday, hiệu suất) | Hoá lí | Mới | 3 |
| 12 | Giải bài vô cơ / định lượng (bảo toàn KL, nguyên tố, e, điện tích) | Vô cơ | Mới | 4 |
| 13 | Sổ tay tra cứu (danh pháp IUPAC, tính tan, dãy điện hoá, màu kết tủa/khí, nhận biết) | Toàn bộ | Mới | 4 |
| 14 | Gia sư AI Hoá (Chem-Tutor) + RAG trên kiến thức Hoá | Toàn bộ | Mới | 4 |
| 15 | Luyện đề 3 dạng + chấm tự động + thống kê | Ôn thi | Mới | 5 |
| 16 | Mục **Hoá lý** `/hoa-hoc/hoa-ly` – trọn bộ 4 mô phỏng tương tác 2D (Động cơ nhiệt 4 kỳ, Khí lý tưởng, Cân bằng hoá học & Le Chatelier, Tốc độ phản ứng & Ea) + Thư viện 34 công thức có nút Chứng minh | Hoá lí, nhiệt động, động học, cân bằng | Đã hoàn thành toàn bộ 6 giai đoạn (25/10/2026) | → `PROMPT_HOA_LY_HOAN_THIEN.md` |

## 4. Giao diện & điều hướng
- Trang chủ: thanh chọn 3 nhóm **Đại cương · Vô cơ · Hữu cơ**; 4 công cụ cũ thuộc nhóm Hữu cơ; công cụ chưa có hiện thẻ "Sắp ra mắt".
- Trang mới: `/hoa-hoc` (lộ trình + bài học, `?bai=<id>`); sau này `/hoa-hoc/bang-tuan-hoan`, `/hoa-hoc/lab`, `/hoa-hoc/luyen-de`.
- Mỗi bài học có nút "Mở công cụ" nhảy sang công cụ liên quan (giống `co_quan` → Lab 3D bên Sinh).

## 5. Kỹ thuật (bám kiến trúc hiện có)
- Dữ liệu: `config/lo_trinh_hoa_hoc.json` · `frontend/hoa-hoc/kien-thuc/<bai_id>.json` + `thuat-ngu.json` + `README.md`.
- Backend: `backend/lo_trinh.py` thêm tham số môn (sinh mặc định, không phá API Sinh) · route trang + API Hoá.
  Giai đoạn sau: `can_bang.py`, `may_tinh_hoa.py`, `giai_vo_co.py`, `hoa_hoc_ai.py` + `hoa_hoc_rag.py` (theo mẫu sinh_hoc_ai / sinh_hoc_rag),
  thêm hệ thống `giai_vo_co`, `gia_su_hoa` vào `backend/ai/routing.json`; luyện đề dùng lại `ai_rieng/cham.py`.
- Gia sư / lời giải Hữu cơ bắt buộc chuẩn biện luận 4 bước: `.agents/rules/bien_luan_chat_huu_co.md`.
- Test cho mọi dữ liệu và API mới; toàn bộ test cũ phải giữ PASS.

## 6. Lộ trình triển khai
1. **Giai đoạn 1 – Phòng thí nghiệm ảo** (người dùng chọn làm trước, 23/10/2026): 5 thí nghiệm, bản Web 3D (Three.js) + bản Unity 3D,
   công tắc Unity cho mọi người trong `/admin`. → `PROMPT_PHONG_THI_NGHIEM_AO.md`
2. **Giai đoạn 2 – Khung học + Hữu cơ 11–12**: lộ trình JSON, kiến thức 26 bài Hữu cơ, trang `/hoa-hoc`, trang chủ 3 nhóm; bài học nối sang thí nghiệm ảo.
3. **Giai đoạn 3 – Đại cương**: bài lớp 10 + Cân bằng hoá học lớp 11; bảng tuần hoàn, cân bằng phương trình, máy tính hoá, 3D VSEPR/tinh thể.
4. **Giai đoạn 4 – Vô cơ**: bài Nitrogen – Sulfur, kim loại, điện hoá; giải bài định lượng, sổ tay, Chem-Tutor + RAG.
5. **Giai đoạn 5 – Thi**: ngân hàng đề 3 dạng, chấm tự động, thống kê tiến độ cho giáo viên/admin; thêm thí nghiệm ảo mới theo bài.
