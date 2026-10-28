Đọc và tuân theo AGENTS.md.
Phòng Lab 3D (biology-lab-3d*.html, frontend/sinh-hoc/, backend/sinh_hoc_ai.py): từ 26/10/2026 Gemini cũng sửa trực tiếp (bỏ luật chỉ Claude ghi).
Trước khi sửa xem `git status` / `git log -3 -- <file>`, không đè thay đổi chưa commit của AI khác. `.agents/hang-doi/sinh-hoc/` còn đề xuất nào thì vẫn xử lý.

# Sơ đồ dự án (đọc file này thay vì quét lại code; dùng grep/sed -n theo khoảng dòng)
App web "CHEMISTRY – Giải Chuỗi Hữu Cơ": FastAPI (backend/, .venv Python 3.11) + HTML/Tailwind/JS thuần (frontend/). Tiếng Việt.
Chạy: launchd (bat-tat.command / tu-chay.command) cổng 8000 + Cloudflare tunnel. Test: `.venv/bin/python -m pytest -q` (AI giả ở tầng API).
Không sửa .env (AGENTS.md) → cấu hình qua script *.command (cau-hinh-email/-key-claude/-claude-admin/-openai…) + .env.example.
Máy chủ (Render FREE, HUONG_DAN_DUA_LEN_WEB.md): Dockerfile + render.yaml (không ổ đĩa → sao_luu.py: data/app.db + ai_rieng ↔ repo GitHub riêng
tư SAO_LUU_GITHUB_REPO, khôi phục TRƯỚC khi mở CSDL ở đầu main.py, sao lưu mỗi SAO_LUU_PHUT + khi tắt; CAP_ADMIN_EMAIL) · biến STORAGE_DIR (data/ outputs/ logs/
ra ổ đĩa), CHE_DO_CUC_BO=0, TRUSTED_PROXIES=10.0.0.0/8; key ở Environment của Render. Đưa lên: dua-len-web.command (ảnh chụp HEAD → nhánh ban-web →
remote "web" main, KHÔNG kèm lịch sử cũ vì lịch sử có .env/.venv). Không có Chrome trên máy chủ (chỉ Demo cần) · font Linux Liberation/DejaVu.

## Gọi AI – backend/llm.py + backend/ai/
- `PhienHoiThoai(system, output_schema=, effort=, max_tokens=, bac=)` `.gui(text=, anh=[(mime,b64)])` → PhanHoi; bậc lite/flash/pro/claude.
  Chuỗi AI của phiên = `ai/router.thu_tu(he_thong)` (giai_chuoi / mo_hinh_3d / co_che; mặc định claude → gemini → openai) bỏ AI không dùng
  được (`ly_do_bo_qua`); AI lỗi → kế tiếp + `router.ghi_du_phong` (bảng ai_fallbacks). `ten_ai(bac)` = tên AI sẽ giải trước (tiến trình).
- backend/ai: types.py (AIProvider, LoiAI, PhanHoi) · router.py + routing.json (admin đổi: app_settings "thu_tu_ai") · providers/claude
  (`goi_claude`, streaming) · providers/gemini (`_goi_gemini`, tự lùi mô hình khi hết hạn mức, `mo_hinh_gemini`) · providers/openai
  (`goi_openai`, Responses API; OPENAI_API_KEY / OPENAI_MODEL, chỉ key máy chủ, không tools). Registry `NHA_CUNG_CAP`. Test monkeypatch
  ở module provider (claude_chat / gemini_chat / openai_client), KHÔNG ở llm. Timeout = `anthropic.Timeout` / `openai.Timeout` (SDK chạy httpx2).
- Kế hoạch yêu cầu: `KeHoachAI` + `dung_ke_hoach()` (ContextVar): bậc, quyền Claude (`.claude`), user/job/bước → ghi `ai_usage` (chi_phi_ai.py).
- Key của yêu cầu: `KhoaAI` + `dung_khoa()` (chu_app / ca_nhan = Gemini BYOK / dung_thu / khong_co) – tai_khoan.chon_khoa_giai/_3d.
## Chọn model / quyền Claude
- do_kho.py: phân loại Dễ/TB/HSG (Flash-Lite), `bac_giai`, `bac_tiep`. config.py: tên model + mọi biến .env.
- Quyền Claude: key_claude.chon_quyen (API key Claude riêng [claude_rieng.lay_key: admin bật + "Dùng key của tôi"] → admin → Key Claude chung → ma_claude) · ma_claude.py (mã CLD-…) · main._quyen_claude.
## Luồng giải chuỗi – backend/pipeline.py `chay()` (thread nền, sự kiện SSE `job.emit`)
AI: `_chay_nhanh` → do_kho.phan_loai (nếu Tự động) → giai_nhanh.giai (MỘT lần gọi, streaming, JSON chuỗi gọn) → giai_nhanh.kiem_tra (RDKit)
→ sua theo mức → sang_loi_giai → renderer / image_export / chemdraw. Mức: giai_nhanh.chon_muc/cau_hinh_muc (THINKING_*, MAX_TOKENS_* .env).
Cache: bảng giai_cache (7 ngày). Phương trình: POST /api/phuong-trinh/{job}. Demo: `_chay` luồng cũ (parser/research/solver/validator).
## Chi phí / Claude: chi_phi_ai.py (ai_usage, thong_ke, tong_job) · claude_admin.py (Admin API, claude_nap) · claude_rieng.py (key riêng – CHỈ tài khoản
được admin bật (duoc_phep: admin luôn được): cap_quyen / yeu_cau / tu_choi / dat_dung / ghi_loi; tắt → vô hiệu ngay, don_het_han xoá sau 30 ngày).
## AI Riêng (backend/ai_rieng, /admin/ai-rieng): kho.py (bảng ar_*, FTS5, cài đặt app_settings "ai_rieng" + tên trường form) · nhung.py
(gemini-embedding-2, vector numpy) · tim.py (tìm lai → nhãn S/Q/M/T) · giai.py (tầng 1 thư viện → tầng 2 AI Riêng ‖ Gemini đối chiếu SONG SONG
→ tầng 3 mạnh / trọng tài; `doi_chieu`, `nghi_van`) · song_song.py (thread giữ ContextVar) · xu_ly.py (việc nền) · cham.py (Bộ đề) · web.py.
pipeline.chay(ai="ai_rieng"|"gemini"|"openai") · KeHoachAI.uu_tien_ai · test: conftest tắt làn song song (bật riêng + cho_nen_xong).
## Sinh học 3D (/sinh-hoc): frontend/biology-lab-3d.html – MỘT file (three@0.160.0 + gsap + MathJax qua CDN):
dữ liệu BO_PHAN (+ `cu` = tên gọi cũ để tìm) / CO_QUAN (tab Giải phẫu · Cơ chế mức SGK · Mở rộng = hoa_sinh, vượt CT) / Y_DINH (AI nội bộ,
khớp nguyên từ `tuKhoa`), dựng procedural 5 hệ + hệ cơ (Gemini), viMo.* (6 cảnh hiển vi), thucHienHanhDong(payload, có co_quan),
window.BioLab (pauseRendering/resumeRendering, trangThai cho test). Lớp ngoài: vatLieuCo (MeshPhysical + THO_CO vân thớ + VUNG_GLSL ngực–bụng trong),
môi trường giai_phau (datNen, denKhoa/denVienSau, bóng), lopAn/LOP_V1, heSoTach, GOC_NHIN; mục 15b Khám phá: THONG_TIN_CQ + moCoQuan/dongCoQuan
(điểm nóng, thẻ), tìm, vùng/hệ/lớp, đếm cấu trúc, nhãn thông minh, HUONG_DAN. AI: "AI của web" = POST /api/sinh-hoc/hoi (main.hoi_sinh_hoc →
sinh_hoc_ai.hoi, tai_khoan.chon_khoa_sinh_hoc, giới hạn "sinh_hoc", router loại sinh_hoc → mo_hinh_3d); lỗi → AI nội bộ. Test: tests/test_sinh_hoc(_ai).py.
Điều khiển bằng tay: nút `#btn-dieu-khien-tay` (phím H), `chonTaiDiem`, `boApOrbit`, `window.chonTaiDiem`, `window.chonTab`; API `BioLab.dieuKhienTay` (`bat(nguon?)`, `tat()`, `trangThai`), `BioLab.gocCamera` (`phuongVi`, `cuc`, `khoangCach`).
Giải phẫu THẬT: frontend/sinh-hoc/bp3d.bin.gz (BodyParts3D © DBCLS, CC BY 4.0 – ghi công #ghi-cong-bp3d) do tools/dung_bp3d.py tạo từ
isa_BP3D_4.0_obj_99.zip (không để trong repo): xương / cơ / gân / mạch chi / da → nhóm id (xuong_dui, co_delta…), K=0,00243 khớp nội tạng sẵn có,
giảm đa giác gộp lưới theo pháp tuyến; cơ thẳng bụng + lưng rộng dựng theo da. Trang: `await taiBP3D` → BP3D (lỗi → dựng bằng code), dungTuBP3D(lop, he),
KHOP (khớp chi), LOP_AN_MAC_DINH (mở trang: xương + nội tạng; da, cơ ẩn), SO_PHAN_BP3D (đếm cấu trúc).
Bản v2 (/sinh-hoc-v2, biology-lab-3d-v2.html) ĐÃ XOÁ 26/10/2026 theo yêu cầu người dùng – route cũ chuyển 307 về /sinh-hoc; xem lại bằng `git show a9f83c32:frontend/biology-lab-3d-v2.html`.
## Kiến thức Sinh học: frontend/sinh-hoc/kien-thuc/ (15 cơ quan <id>.json khung Vị trí→Cấu tạo→Chức năng→Cơ chế→Liên hệ→Điểm thi + trac_nghiem,
goi_y_hoi, mo_rong; danh-muc.json = cơ quan ↔ mã BO_PHAN; thuat-ngu.json = thuật ngữ SGK, test kiểm cả trang 3D + knowledge/*.md;
knowledge/*.md = tài liệu RAG (sinh_hoc_rag tự index lại khi file .md mới hơn) · test: tests/test_kien_thuc_sinh_hoc.py.
## Lộ trình học Sinh (chỉ admin, /admin → thẻ lo-trinh): config/lo_trinh_sinh_hoc.json (chương → bài, id cố định) · backend/lo_trinh.py
(tien_do / danh_dau, bảng lo_trinh_tien_do theo user) · /admin/api/lo-trinh (+/{bai_id}) · admin.js veLoTrinh · test: tests/test_lo_trinh.py.
## Phòng thí nghiệm Hoá ảo (/hoa-hoc/phong-thi-nghiem?tn=<id>): frontend/phong-thi-nghiem.html + frontend/hoa-hoc/phong-thi-nghiem/ (ban-do.json, ban-do.js UMD
va chạm & tìm đường A*, dieu-khien.js UMD toán camera/tay đòn/hướng nhìn, phong-mo.js phòng mở procedural 22×16m 5 khu có bàn phenolic gờ chắn nước Hoà Phát, tủ hút Lý Sơn Sa Kỳ & vách kính phòng con, nhan-vat.js điều khiển game Genshin Impact (WASD tương đối yaw camera, chuột tự do/pointer lock, Shift chạy 3.4m/s, Space nhảy 0.5m, F góc 1/3, V nhà búp bê, cooldown tự vào bàn 3.5s), thi-nghiem/<id>.json ×5
an-toan|chuan-do|nhan-biet-ion|pin-galvani|dien-phan, bang-ion.json, tinh-toan.js UMD hàm thuần, hinh-hoc.js UMD hình dụng cụ (profile bo góc + thành kính dày + chất lỏng ôm lòng bình; Unity: ThiNghiem/LabHinh.cs cùng thuật toán, kính trong suốt vẽ HAI LƯỢT LabVatLieu.HaiLuot), vat-lieu-thuc.js + noi-that-thuc.js (texture canvas + chi tiết phòng thật, phong-mo.js gọi nangCapPhong), thu-vien-dung-cu.html (xem model, ?so-sanh=1&khung=1), mo-phong.js UMD máy trạng thái = NGUỒN SỰ THẬT, ve-web.js
Three.js r160 nhiều trạm (setViewOffset lệch tâm +190px tránh bảng điều khiển 380px, hotspot ①②③ CSS2D), hai chế độ di_dao / tai_ban, giao-dien.js (HUD 4 góc toàn màn hình, ngăn kéo trái danh sách trạm, bảng phải điều khiển/bottom sheet mobile), cau-unity.js; hook window.PhongLab.phong) · backend/phong_thi_nghiem.py (app_settings
"phong_thi_nghiem" {unity_bat}, THU_MUC_BUILD = frontend/unity-lab/Build/) · /api/phong-thi-nghiem/cau-hinh · /admin/api/phong-thi-nghiem ·
unity/PhongThiNghiemHoaHoc (LabBridge, Canh/*, Editor/BuildWebGL.Build) · CSS ptn-* · test: tests/test_phong_thi_nghiem.py + node --test tests/js/.
## Tài liệu khoa học: literature.py (rut_tu_khoa AI rẻ → tra_cuu: Crossref/Unpaywall/OpenAlex/S2/Europe PMC/arXiv/PubChem song song ≤20 s,
toàn văn mở → trich_doan ≤3.000 từ; cache lit_cache DOI 30 ngày / tìm 7 ngày) · nối vào pipeline._tra_cuu_tai_lieu + /api/co-che.
## Cơ chế (mục 05): co_che.py (tao_co_che, WIP v2 chưa commit) · ve_co_che.py (SVG/CDXML) · co_che_anh.py (đọc ảnh đề → xác nhận).
## 3D (mục 04): mo_hinh_3d.py (nhan_dien, _anh_2d).
## Sơ đồ hệ thống (/admin/mindmap): config/so_do_he_thong.json (hệ thống AI: "ai": {claude|gemini|openai: {dieu_kien, con}} + "khac")
· so_do_he_thong.py (dung_nhanh_ai theo router: vai trò trong AI thật sự dùng, model thật từ settings, đếm ai_usage, health-check
models.retrieve/get cache 5 phút) · frontend/mindmap.html/.js · logo simple-icons ở frontend/logos (openai.svg).
## Mã giao diện: ma_giao_dien.py (kiểm bằng HMAC + code_enc Fernet để admin xem / chép, theme_id, lượt, hạn, khoá sai; tao_ma_mau SAKURA2026).
## Email rác (đăng ký): email_rac.py (kiem_tra: định dạng → cho phép/chặn của admin → email tạm [email_tam_thoi.txt + data/ tải mỗi 7 ngày]
→ MX dnspython; ly_do_nghi_ngo; ghi_chan/email_blocks; so_lan_thu_gio; xoa_chua_xac_minh + chay_nen mỗi giờ) · nối ở auth.dang_ky + auth_callback.
## CSDL – backend/db.py (SQLite data/app.db, `ket_noi()`, `cai_dat()/dat_cai_dat()` = bảng app_settings)
users · solutions · email_codes · activity_log · name_changes · app_settings · claude_codes / claude_code_users / claude_code_fails ·
ai_usage · ai_fallbacks (chuyển AI, 30 ngày) · claude_key_users / claude_key_fails / claude_key_uses · giai_cache · claude_nap · lit_cache · email_blocks · email_domain_rules ·
theme_codes / theme_code_uses / theme_code_fails (mã giao diện).
users.verify_deadline (tự xoá nếu chưa xác minh) · users.suspect_* (email nghi ngờ). users.claude_key_enc (key Claude riêng)
+ allow_own_claude_key(_at/_by) · own_claude_key_off_at · use_own_claude_key · claude_key_error · own_claude_key_req_at (yêu cầu bật).
## Route
- main.py: / /cai-dat /admin /admin/mindmap /dang-nhap · /api/giai /api/tien-trinh/{job} /api/ket-qua/{job} /api/tep /api/tai /api/chemdraw ·
  /api/3d · /api/co-che, /api/co-che/doc-anh, /api/co-che/chat · /api/suc-khoe · /hoa-hoc/phong-thi-nghiem · /api/phong-thi-nghiem/cau-hinh.
- auth.py: /api/toi /api/theme/redeem (mã giao diện +K) /api/key (Gemini BYOK) /api/key-claude /api/claude-rieng (+/yeu-cau, /dung) /api/ma-claude /api/lich-su /api/doi-ten /auth/*.
- quan_tri_web.py (prefix /admin/api, chỉ admin – `can_admin`): tong-quan online hoat-dong ip luong nguoi-dung (+/{uid}/key-claude-rieng) canh-bao bieu-do
  ma-claude key-claude chi-phi claude (+/nap, /canh-bao) email-rac (+/quy-tac, /nghi-ngo/{uid}, /cap-nhat) ma-giao-dien (+/{id}/thu-hoi) so-do (+/suc-khoe) thu-tu-ai cai-dat-chung lo-trinh phong-thi-nghiem. CLI: admin_cli.py (live/log/users/ip/code/claude-key/ownkey/email/theme/claude/usage…).
- Bảo mật: bao_mat.py (Fernet `ma_hoa_key`, `_suy_khoa`, CSRF, `GIOI_HAN` rate limit) · nhat_ky.py (activity_log, SU_KIEN) · dia_chi_ip.py.
## Giao diện – frontend/
index.html + app.js: 01 Nhập đề · 02 Tuỳ chọn (mức độ, Dùng Claude, thanh mức thông minh) · 03 Kết quả (sơ đồ chuỗi) · 04 3D · 05 Cơ chế.
Trang chủ (trang-chu.js): 3 thẻ lớn → FLIP thu nhỏ về nút góc trái; body[data-muc-lon] giai|3d|co-che ẩn/hiện mục [data-thuoc-muc];
TrangChu.mo(nhóm) khi nhảy mục. Theme: giao-dien.js (GiaoDien.ap, hộp +K) + themes/<id>.js {bien, hieuUng}; chỉ index.html.
cai-dat.html + cai-dat.js: 01 Tài khoản · 02 Key Gemini · 03 Cách lấy key · 04 Lịch sử · 05 Mã Claude · 06 Key Claude ·
07 API key Claude · 08 Hoạt động; hoạt ảnh mở (moCaiDat, công tắc admin app_settings "hoat_anh_cai_dat").
admin.html + admin.js (+ hoat-dong.js): tổng quan, trực tiếp, người dùng, Chi phí AI, Cài đặt chung, Claude API (máy chủ), Mã/Key Claude, Email rác,
Thứ tự AI (kéo thả theo hệ thống + bảng chuyển AI gần đây). /admin = lưới 13 thẻ thu gọn (.the-nho, tomTat()) → bấm: hiệu ứng quét (chuyen-canh.js ChuyenCanh.chuyen, biến --cn-* trong style.css) → mở 1 khối
[data-khoi] (#ten-khoi + history: Back / Esc đóng). Các khối luôn trong DOM (chỉ ẩn) → loader / luồng trực tiếp không đổi.
Test: conftest có fixture autouse → MỌI test dùng CSDL tạm (không bao giờ ghi data/app.db), DNS / GitHub giả.
tai-khoan.js: TaiKhoan.tai()/goi() (CSRF). style.css: biến màu :root + [data-theme="toi"], chỉ THÊM class mới ở cuối file.
## Tiến độ / việc dở: HANDOFF.md (WIP cơ chế v2: co_che.py, bieu_dien.py – commit chọn lọc `git commit -- <file>` để không lẫn WIP).

## App máy tính CHEMISTRY
- Thư mục `desktop/`:
  - `main.js`: Main process Electron, menu native macOS, an toàn Keychain `safeStorage`, deep link `chemistry://dang-nhap`, single-instance lock.
  - `preload.js`: Bridge contextIsolation an toàn (`window.chemistryApp`).
  - `may_chu/`: `khoi_dong.py` chạy Uvicorn ở cổng trống, `chemistry.spec` PyInstaller onedir đóng gói backend + frontend + config.
  - `build-mac.sh` / `build-win.ps1`: Kịch bản đóng gói tự động ngoài iCloud (`~/AppBuild/CHEMISTRY/`), DMG 231 MB, ZIP 232 MB, codesign ad-hoc.
  - `kiem_tra/kiem_tra_app.py`: Nghiệm thu tự động Playwright CDP 14 kịch bản.
- Biến môi trường chế độ app: `CHE_DO_APP=1`, `CHE_DO_CUC_BO=1`, `STORAGE_DIR=~/Library/Application Support/CHEMISTRY` (tắt Cloudflare tunnel, tắt sao lưu GitHub, nạp vendor CDN nội bộ).
- Xác thực chế độ Trực tuyến: `POST /api/app/ma-dang-nhap` (sinh mã 6 số một lần, 5 phút), `POST /api/app/doi-ma` (đổi mã lấy token phiên), deep link `chemistry://dang-nhap?ma=...`.
- App Unity tích hợp: `PhongLabHoa3D.app` nằm trong `Contents/Resources/`, mở qua `Cmd+Shift+L` hoặc nút "Mở bằng ứng dụng" tại Web Lab 3D.
- Kiểm thử: `pytest tests/test_app_auth.py` và `./.venv/bin/python3 desktop/kiem_tra/kiem_tra_app.py`.
- Tự cập nhật: `desktop/cap_nhat.js` (thuần Node: so phiên bản, kiểm tra bản phát hành, tải + xác minh SHA-256, chỉ https + tên miền cho phép) ← `main.js`
  `kiemTraCapNhat` (hộp thoại, thanh tiến độ, mở .dmg/.exe; menu "Kiểm tra cập nhật…" + tự kiểm tra ≤ 6 giờ/lần khi app đóng gói) ← `GET /api/app/phien-ban`
  (backend/cap_nhat_app.py đọc `config/phien_ban_app.json`). KHÔNG tự thay app tại chỗ (app ký ad-hoc, không có Apple Developer). Phát hành:
  `tools/phat_hanh_app.py` (xem docstring). Test: tests/test_cap_nhat_app.py + tests/js/cap_nhat_app.test.mjs + cap_nhat_main.test.mjs.

## Hoá lý (/hoa-hoc/hoa-ly)
- Trang hub: `/hoa-hoc/hoa-ly` (`frontend/hoa-ly.html`) nạp động các thẻ mô phỏng từ `frontend/hoa-hoc/hoa-ly/danh-muc.json`.
- Thư mục dùng chung (`frontend/hoa-hoc/hoa-ly/chung/`):
  - `dinh-dang.js` (`HLDinhDang`): Số tiếng Việt, số mũ khoa học Unicode (`1,23 · 10⁻⁵`), vạch chia đẹp (1-2-5), logarit.
  - `ngau-nhien.js` (`HLNgauNhien`): Mulberry32 PRNG tái lập được từ seed, phân bố đều, chuẩn Box-Muller, Maxwell-Boltzmann 2D/3D.
  - `hat.js` (`HLHat`): Mô phỏng hạt 2D độc lập DOM, va chạm thành pít-tông, va chạm đàn hồi 2 hạt bảo toàn động lượng và động năng tới $10^{-9}$, ghi nhận xung lực để tính áp suất thực.
  - `ve-hat.js` (`HLVeHat`): Bộ vẽ Canvas đồng bộ toạ độ SVG qua `getScreenCTM()`, cắt chặt trong `vungKhi`.
  - `do-thi.js` (`HLDoThi`): Đồ thị vector SVG tốc độ cao, nhãn né đè nhau, vạch chia đẹp.
  - `keo.js` (`HLKeo`): Pointer Events kéo góc, kéo trục dọc/ngang, quán tính thả chuột, ARIA slider.
  - `bang-mau.js` (`HLMau`): Đọc CSS variable tokens qua computedStyle, dải màu `mauNhietDo(T)`, `tronMau`.
  - `khung-trang.js` (`HLKhung`): Khung header chuẩn, đổi sáng/tối Awesomic, thu gọn bảng, toàn màn hình.
  - `cau-hoi.js` (`HLCauHoi`): Thành phần câu hỏi trắc nghiệm THPT 3 dạng (I, II, III).
  - `cong-thuc.js` (`HLCongThuc`): Thẻ công thức, drawer trượt 560px chứng minh từng bước, tính thử số liệu.
  - `hinh-chung-minh.js` (`HLHinhChungMinh`): 7 sơ đồ SVG động minh hoạ các bước chứng minh toán học.
- Thư viện công thức Hoá lý:
  - `frontend/hoa-hoc/hoa-ly/cong-thuc.json`: 34 công thức (K1-K8, N1-N8, H1-H4, D1-D7, C1-C7) kèm chứng minh sư phạm, MathJax 3.2.2 offline + 23 font CHTML woff-v2.
  - Trang riêng: `/hoa-hoc/hoa-ly/cong-thuc` hỗ trợ tìm kiếm không dấu ("dang nhiet", "arrhenius"), bộ lọc cấp độ, query `?chung-minh=<ma>`.
- 4 mô phỏng tương tác 2D:
  1. `dong-co-nhiet`: Otto/Diesel 4 kỳ, đồ thị p-V phóng to 828x339px, phễu dóng zooming funnel, Wiebe ODE, quán tính bánh đà, đề máy.
  2. `khi-ly-tuong`: $pV = nRT$, 4 quá trình đẳng nhiệt/áp/tích/đoạn nhiệt, đo $p$ thực từ va chạm hạt, phân bố Maxwell, Van der Waals CO2 dual isotherms, Charles ngoại suy $-273,15\text{ }^\circ\text{C}$.
  3. `can-bang-le-chatelier`: 4 hệ phản ứng ($2\text{NO}_2 \rightleftharpoons \text{N}_2\text{O}_4$, $\text{H}_2+\text{I}_2 \rightleftharpoons 2\text{HI}$, Haber, $\text{Fe}^{3+}+\text{SCN}^-$), ống tiêm kéo ngang đổi màu tức thì, Le Chatelier thuyết minh tự động từ số liệu, tab $G-\xi$ bóng lăn về đáy cân bằng.
  4. `toc-do-phan-ung`: Hàng rào $E_a$, bóng thế năng lăn, Arrhenius $k(T)$, chất xúc tác, đo thực nghiệm $k$ ở 4 nhiệt độ tự động hồi quy $E_a$, bộ đếm va chạm hạt 2D, phân bố năng lượng 3D.
- Route máy chủ (`backend/main.py`):
  - `@app.get("/hoa-hoc/hoa-ly")` (trung tâm)
  - `@app.get("/hoa-hoc/hoa-ly/{ma}")` (nạp động theo danh mục)
  - `@app.get("/favicon.ico")` (204 No Content chống lỗi console)
- Kiểm thử tự động:
  - `node --test tests/js/hoa_ly_*.test.mjs` (39/39 tests PASS 100%).
  - `pytest tests/test_hoa_ly.py -v` (12/12 tests PASS 100%, bao gồm kiểm tra ngoại tuyến app mode và 23 font).
  - `python3 tools/kiem_tra_hoa_ly.py` (42/42 kịch bản Playwright CDP trên trình duyệt thật 100% PASS, 0 lỗi console).

## Điều khiển bằng tay (chỉ Sinh học)
- Module JavaScript chạy ngoại tuyến hoàn toàn trong trình duyệt (`frontend/dieu-khien-tay/` và `frontend/vendor/mediapipe/`), chuyển 1:1 từ bản mẫu Python (`tools/tay_python/`):
  - Nhận diện ở luồng riêng `nhan-dien-worker.js` (GPU trước; công tắc `localStorage['dkt-nhan-dien']` = '' | 'rieng-cpu' | 'chinh'), cắt 4:3 + đổi toạ độ ở `khung-hinh.js`, ngưỡng 0,5; bảng camera hiện số đo + gợi ý (tối / camera chậm / tay xa).
  - `cu-chi.js` (DieuKhien, LocOneEuro, docTay, tiLeChum, laXoe), `ap-orbit.js` (taoBoApOrbit), `chon-camera.js` (FaceTime HD, chặn iPhone/iPad), `theo-doi-tay.js` (taoDieuKhienTay), `dieu-khien-tay.css`.
  - Khớp 9/9 kịch bản dữ liệu vàng `tests/js/du_lieu/dieu_khien_tay_vang.json` (sai số $\le 10^{-6}$); demo độc lập tại `demo.html` (`?phat=<kich_ban>`).
  - Kiểm thử tự động Playwright (K1..K10 trong `tools/kiem_tra_dieu_khien_tay.py`, K11..K17 trong `tools/kiem_tra_tay_sinh_hoc.py`), pytest (`tests/test_dieu_khien_tay.py`, `tests/test_sinh_hoc*.py`), và `node --test tests/js/dieu_khien_tay_*.test.mjs`.




