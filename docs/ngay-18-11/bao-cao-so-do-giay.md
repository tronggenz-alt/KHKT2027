# Báo cáo: sơ đồ GIẤY giống bản demo đã duyệt (kiểu đáp án bài thi)

Ngày 18–19/11/2026, nhánh `n18/tich-hop`. Tiến độ chi tiết: [tien-do-so-do-giay.md](tien-do-so-do-giay.md).

## 1. Khác biệt đã tìm (bản tự sinh cũ so với `nguyen_mau/co_che_3d/sn2_sn1.html`, `hsg_propellane.html`)

| # | Bản demo đã duyệt | Bản tự sinh cũ (app 18/11) | Nay |
|---|---|---|---|
| 1 | Công thức thu gọn H₃C–C(CH₃)₂–Br: mạch ngang, nhánh dựng đứng | khung xương zigzag | thu gọn cho chất mạch hở (SGK); khung xương cho bài kiểu HSG |
| 2 | Cặp e tự do (chấm đôi) trên O, Br | không có | có, trên dị tố tham gia mũi tên 2 e |
| 3 | Điện tích trong vòng tròn ⊕ ⊖ góc trên-phải | chữ mũ OH⁻, C⁺ | vòng tròn (`dt_vong`), tránh nét/cặp e |
| 4 | Tiêu đề giai đoạn in nghiêng, gạch chân lượn sóng | không có | `giai_doan` ("Cơ chế SN1:", "Khơi mào:"…) |
| 5 | Nhánh sai "Không theo SN2 (C bậc III bị cản trở không gian):", mũi tên đứt có ✕ | không có trên giấy | có, đứng trước, nền nhấn đỏ nhạt; nhánh chính nền vàng nhạt |
| 6 | Mỗi bước một phương trình, bỏ chất đứng ngoài | một chuỗi dài, tự xuống hàng | mỗi bước một hàng; sản phẩm tính bằng áp mũi tên (ra cả HCl, gộp 2 Cl•) |
| 7 | "chậm" / "nhanh" trên mũi tên | "chậm" cả ở cơ chế 1 bước, không có "nhanh" | đúng như demo; bước chỉ chuyển H⁺ ghi "chuyển H⁺" |
| 8 | Phông có chân | phông không chân | `phong: "co_chan"` |
| 9 | Mũi tên từ cặp e / liên kết C–Br cong lên chạm Br | mũi tên ngắn nằm trên nét | xuất phát từ chấm cặp e; C–X đứt cong lên |
| 10 | (Gốc tự do) chấm e độc thân | không hiện (đọc SMILES không tính e độc thân) | có |

## 2. Đã làm

- **Máy chủ** `server/ai/co_che/hoc_ve/thu_gon.py` (mới) – bày công thức thu gọn, đếm cặp e / e độc thân, chọn chỗ cặp e và
  vòng điện tích. `ve_theo_mau.py` – viết lại `tao_so_do` (phương trình theo bước, giai đoạn, nhánh sai, chậm/nhanh, chuỗi gốc
  tự do) và `_mui_ten` (từ cặp e). Áp dụng cho cả 8 bài mẫu và cơ chế AI sinh (`giai_de.py` gọi cùng `tao_so_do`).
  Chọn kiểu: thu gọn trừ khi bài có nhóm viết tắt (R, CO₂Me) hoặc khung ≥ 2 vòng; `co_che.json` ép được bằng `kieu_ve`, đặt
  được `tieu_de_giay`, `nhanh_sai[].tieu_de`, `buoc[].cap_e`.
- **Hợp đồng** `HOP_DONG_APP.md` mục 2b (giữ mọi trường cũ: nhãn vẫn có ⁺⁻, `dt` vẫn có – app cũ vẫn đọc được), `QUY_UOC_VE.md`
  quy ước 9–12, `ve_giay.js` + `xem_bai_demo.html` vẽ trường mới.
- **App** `SoDoGiay.cs`: cặp e, vòng điện tích, khối giai đoạn (nền + vạch trái), tiêu đề nghiêng gạch lượn sóng, mũi tên
  đứt + ✕, phông có chân qua `CoCheMoDun.PhongCoChan`. Thêm `DanhDauBuoc(int)` để tô khối đang xem (như `.buoc.dang-xem`
  của demo) – **CoCheMoDun chưa gọi** (ngoài phạm vi phiên này). `DuLieuCoChe.cs`: đọc `cap_e`, `dt_vong`, `giai_doan`,
  `mt.gach/cheo`, `phong`; có `dt_vong` thì bỏ ký tự điện tích ở nhãn và bỏ `dt`.
- Dựng lại 8 bài (`dung_bai_demo`) và gói `.verapack` (`dong_goi --unity`, không vào git).

## 3. Kiểm tra

- `tests/test_co_che_so_do_giay.py` (mới, 9 test: đếm e, bày tBuBr như demo, gộp H, nhánh sai, phương trình theo bước,
  chuỗi gốc tự do, 8 bài) và `tests/test_co_che_demo.py` (cũ): đạt.
- Ảnh so sánh: `anh/so-do-giay/<bài>.png` (trái: bản demo vẽ tay hoặc ảnh app trước khi sửa; phải: tự sinh mới). Trang tạo
  ảnh: `anh/so-do-giay/so_sanh.html?bai=<mã>&demo=<sn2_sn1|hsg_propellane>`.
- **KhktCoCheVerify: ALL PASSED** (Unity 6000.6.7f1, batchmode có đồ hoạ, 19/11). Ảnh tờ giấy trong app:
  `anh/so-do-giay/app_<bài>.png`. Vật liệu `Lab/Materials/*.mat` bị test đổi đã `git checkout --`.

## 4. Còn lại / hạn chế

- Propellane (kiểu HSG): mũi tên bước 1 còn rối, bản demo vẽ tay gọn hơn nhiều (đặt DMAD trên mũi tên, khung "lều" có sẵn).
- "SN2" chưa viết chỉ số dưới N như demo (Unicode không có N nhỏ dưới; cần rich text).
- Vế phải của chuỗi gốc tự do (Cl₂ → 2 Cl•) đúng; nhưng nếu AI đưa mũi tên áp không được thì rơi về cách cũ (lọc trạng thái sau).
- CoCheMoDun gọi `Giay.DanhDauBuoc(buoc)` khi đổi bước để tô khối đang xem (việc của phiên co-che-tu-de).
