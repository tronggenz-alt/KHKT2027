# Tiến độ: sơ đồ GIẤY giống bản demo đã duyệt (phiên so-do-giay)

Đọc tệp này trước khi làm tiếp. Nhánh `n18/tich-hop`, chỉ `git add` tệp của việc này.

## Đã xong
- Bước 1 – khác biệt (bản tự sinh so với `nguyen_mau/co_che_3d/sn2_sn1.html`):
  1. Công thức khung xương thay vì thu gọn (H₃C–C(CH₃)₂–Br, mạch ngang, nhánh dựng đứng).
  2. Không có cặp electron tự do (chấm đôi) trên O, Br…; mũi tên không xuất phát từ cặp e.
  3. Điện tích viết chữ mũ (OH⁻, C⁺) thay vì vòng tròn ⊕ ⊖ ở góc trên-phải.
  4. Không có tiêu đề giai đoạn in nghiêng gạch chân lượn sóng; không có khối nhánh sai (nền nhấn, mũi tên gạch có ✕).
  5. Cả cơ chế là một chuỗi dài tự xuống hàng tuỳ ý; demo viết mỗi bước một phương trình (chất phản ứng → sản phẩm của bước, bỏ chất đứng ngoài như Br⁻).
  6. "chậm" có cả ở cơ chế một bước; thiếu "nhanh" ở các bước còn lại; chữ không phải phông có chân.
  7. Gốc tự do (Cl•, CH₃•) không hiện chấm (bản cũ đọc SMILES không tính e độc thân).
- `server/ai/co_che/hoc_ve/thu_gon.py` (mới): bày công thức thu gọn kiểu SGK, đếm cặp e / e độc thân, chọn chỗ cặp e và vòng điện tích.
- `ve_theo_mau.py`: lớp `Chat` có tham số `thu_gon`, `giu` (mặc định tắt → hành vi cũ, test cũ đạt).

## Đang làm dở
- Viết lại `tao_so_do` trong `ve_theo_mau.py`: mỗi bước một phương trình (áp mũi tên bằng `co_che.ap_mui_ten` để ra sản phẩm thật, gộp 2 Cl•),
  giai đoạn có tiêu đề (`giai_doan`), nhánh sai trước (nền "sai", mũi tên `gach` + `cheo`), cặp e (`cap_e: [[x,y,góc]]`),
  điện tích vòng (`dt_vong: [[x,y,"+"]]`, vẫn giữ nhãn có điện tích và `dt` cho app cũ), `phong: "co_chan"`, nhãn chậm/nhanh,
  gốc chuỗi: Khơi mào / Phát triển mạch / Tắt mạch.

## Còn lại (theo thứ tự)
1. Xong `tao_so_do` mới; test Python mới `tests/test_co_che_so_do_giay.py`.
2. Hợp đồng: HOP_DONG_APP.md mục "18/11 – giấy kiểu bài thi"; QUY_UOC_VE.md; `ve_giay.js` vẽ trường mới.
3. Dựng lại: `.venv/bin/python -m server.ai.co_che.dung_bai_demo` rồi `.venv/bin/python -m server.ai.co_che.dong_goi --unity`.
4. App: `SoDoGiay.cs` + `DuLieuCoChe.cs` (đọc cap_e, dt_vong, giai_doan, mt.gach/cheo, phong).
5. Kiểm tra: `.venv/bin/python -m unittest tests.test_co_che_demo tests.test_co_che_so_do_giay`; KhktCoCheVerify (một -batchmode);
   ảnh so sánh vào `docs/ngay-18-11/anh/so-do-giay/`.
6. `docs/ngay-18-11/bao-cao-so-do-giay.md`, commit.

## Lệnh kiểm thử
- `.venv/bin/python -m unittest tests.test_co_che_demo`
- `.venv/bin/python -m unittest tests.test_khong_lo_du_lieu` (trước mỗi commit)
