# DANH SÁCH TÍNH NĂNG ỨNG DỤNG CHEMISTRY

Bảng danh sách toàn bộ các trang, giao diện và tính năng cần có trong ứng dụng desktop `CHEMISTRY.app`.
Đây là căn cứ nghiệm thu toàn diện tại Giai đoạn 7.

| STT | Tên trang / Tính năng | Đường dẫn (Route) | Tệp giao diện / Mã nguồn | Cần Đăng nhập / Admin | Có gọi AI? | Ghi chú kiểm thử offline & chế độ trên máy |
|---|---|---|---|---|---|---|
| 1 | **Trang chủ - Giải chuỗi phản ứng** | `/` | `frontend/index.html`, `frontend/app.js` | Không (có dùng thử) | Có (hỗ trợ AI giả khi test) | Vẽ sơ đồ chuỗi phản ứng 5-6 chất, hiển thị cấu trúc SVG, tải tệp ZIP, PNG |
| 2 | **Trang chủ - Tìm cấu tạo chất** | `/?muc=tim-chat` | `frontend/index.html`, `frontend/app.js` | Không | Có (hỗ trợ AI giả khi test) | Biện luận cấu trúc phân tử, công thức cấu tạo thu gọn, xuất ảnh |
| 3 | **Trang chủ - Mô hình 3D phân tử** | `/?muc=3d` | `frontend/index.html`, `frontend/app.js` | Không | Không bắt buộc (RDKit / 3Dmol) | Xoay, thu phóng mô hình 3D quả cầu - que nối (ball-and-stick) |
| 4 | **Trang chủ - Cơ chế phản ứng** | `/?muc=co-che` | `frontend/index.html`, `frontend/app.js` | Không | Có (khi giải thích cơ chế) | Vẽ sơ đồ cơ chế mũi tên cong electron, tải ChemDraw CDXML |
| 5 | **Phòng Thí Nghiệm Hoá Ảo (Web 3D)** | `/hoa-hoc/phong-thi-nghiem` | `frontend/phong-thi-nghiem.html`, `frontend/hoa-hoc/phong-thi-nghiem/*` | Không | Không (chạy hoàn toàn offline) | 5 trạm: Chuẩn độ, Nhận biết ion, Pin Galvani, Điện phân, An toàn |
| 6 | **Phòng Lab Hoá 3D (Unity App)** | `phonglabhoa://mo?tn=<id>` hoặc nút "Mở bằng ứng dụng" | `Contents/Resources/PhongLabHoa3D.app` | Không | Không | Mở standalone Unity lab tốc độ cao (>110 FPS), 7 phòng, PBR ISO, font DejaVuSans |
| 7 | **Phòng Lab Hoá 3D Unity WebGL nhúng** | `/hoa-hoc/phong-thi-nghiem` | `desktop/lab_webgl.js`, `frontend/hoa-hoc/phong-thi-nghiem/lab-unity-app.js`, `outputs/app-lab/webgl/*` | Không | Không | Nhúng trực tiếp bản build Unity WebGL nội bộ qua scheme `chemlab://`, font tiếng Việt DejaVuSans |
| 8 | **Thư viện dụng cụ thí nghiệm** | `/hoa-hoc/phong-thi-nghiem/thu-vien-dung-cu.html` | `frontend/hoa-hoc/phong-thi-nghiem/thu-vien-dung-cu.html` | Không | Không | Xem trực quan các mô hình dụng cụ chuẩn ISO 3D |
| 9 | **Trung tâm Hoá lý 2D** | `/hoa-hoc/hoa-ly` | `frontend/hoa-hoc/hoa-ly/index.html`, `trung-tam.js` | Không | Không | Hub trung tâm mô phỏng hoá lý tương tác 2D & thư viện công thức |
| 10 | **4 Mô phỏng tương tác Hoá lý 2D** | `/hoa-hoc/hoa-ly/{dong-co-nhiet,khi-ly-tuong,can-bang-le-chatelier,toc-do-phan-ung}` | `frontend/hoa-hoc/hoa-ly/**` | Không | Không | Động cơ nhiệt 4 kỳ (Otto/Diesel), Định luật Khí lý tưởng, Cân bằng Le Chatelier, Tốc độ phản ứng & Năng lượng hoạt hoá |
| 11 | **Thư viện 34 Công thức Hoá lý & Drawer** | `/hoa-hoc/hoa-ly/cong-thuc` | `frontend/hoa-hoc/hoa-ly/cong-thuc/**`, `cong-thuc.json` | Không | Không | 34 công thức hoá lý, ngăn kéo chứng minh từng bước, 23 font MathJax offline |
| 12 | **Lab Sinh học 3D** | `/sinh-hoc` | `frontend/biology-lab-3d.html` | Không | Tùy chọn (Hỏi AI sinh học) | Mô hình giải phẫu BodyParts3D 3D cơ thể người, 5 hệ cơ quan, vi mô, điều khiển tay MediaPipe |
| 13 | **Bảng tuần hoàn 118 nguyên tố** | Tích hợp trong `/hoa-hoc/phong-thi-nghiem` & Modal sảnh | `frontend/hoa-hoc/bang-tuan-hoan/nguyen-to.json`, `nguyen-to.js` | Không | Không | Bảng tuần hoàn chuẩn hóa PubChem/IUPAC, 118 ô nguyên tố chi tiết |
| 14 | **Cài đặt người dùng & Khoá AI** | `/cai-dat` | `frontend/cai-dat.html`, `frontend/cai-dat.js` | Không (cục bộ) | Không | Quản lý key Gemini BYOK, Claude key, lịch sử giải bài, nhật ký |
| 15 | **Đăng nhập / Tài khoản** | `/dang-nhap` | `frontend/dang-nhap.html`, `frontend/dang-nhap.js` | Không | Không | Chế độ trên máy tự động nhận quyền chủ máy; chế độ trực tuyến dùng mã 1 lần |
| 16 | **Admin - Tổng quan & Bảng điều khiển** | `/admin` | `frontend/admin.html`, `frontend/admin.js` | Có (Admin / Chủ máy) | Không | Lưới 13 thẻ quản trị, thống kê người dùng, hoạt động hệ thống |
| 17 | **Admin - Chi phí AI & Claude API** | `/admin#khoi-chi-phi` | `frontend/admin.html` | Có (Admin) | Không | Thống kê token và chi phí các nhà cung cấp Claude / Gemini / OpenAI |
| 18 | **Admin - Thứ tự ưu tiên AI** | `/admin#khoi-thu-tu-ai` | `frontend/admin.html` | Có (Admin) | Không | Cấu hình router kéo thả thứ tự nhà cung cấp Claude -> Gemini -> OpenAI |
| 19 | **Admin - Sơ đồ hệ thống (Mindmap)** | `/admin/mindmap` | `frontend/mindmap.html`, `frontend/mindmap.js` | Có (Admin) | Không | Biểu đồ trực quan sơ đồ luồng dữ liệu và trạng thái sức khỏe các node AI |
| 20 | **Admin - AI Riêng & Bộ đề** | `/admin/ai-rieng` | `frontend/ai-rieng.html`, `frontend/ai-rieng.js` | Có (Admin) | Có (Embedding & Solver) | Quản lý kho kiến thức riêng, tìm kiếm vector lai FTS5 + nhúng, chấm bộ đề |
| 21 | **Admin - Lộ trình học Sinh học** | `/admin#khoi-lo-trinh` | `frontend/admin.html` | Có (Admin) | Không | Danh mục chương bài và theo dõi tiến độ học tập Sinh học |
| 22 | **Admin - Quản lý Email rác & Bảo mật** | `/admin#khoi-email-rac` | `frontend/admin.html` | Có (Admin) | Không | Chặn tên miền tạm thời, cấu hình quy tắc lọc đăng ký |
| 23 | **Cài đặt App & Menu Desktop** | Menu native macOS / Windows | `desktop/main.js` | Không | Không | Chuyển chế độ Trên máy ⇄ Trực tuyến, Nhập dữ liệu từ dự án, Phím tắt |
