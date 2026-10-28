// config.js – MỌI thông số chỉnh được của chế độ "Giải thích từng bước có mũi tên" (kích thước, màu, thời lượng, độ mờ, camera).
// Sửa ở đây, không sửa rải rác trong code. Đơn vị độ dài = đơn vị cảnh 3D (cơ thể cao ~4,2).
export const CAU_HINH = Object.freeze({
  muiTen: Object.freeze({
    banKinh: 0.006,          // bán kính thân ống
    banKinhVien: 0.0096,     // ống viền phát sáng (cộng màu) bao quanh thân
    doMoVien: 0.25,
    banKinhDau: 0.013,       // đầu nhọn (hình nón)
    daiDau: 0.04,
    doanOng: 54,             // số đoạn dọc ống (vẽ dần theo đoạn)
    vongOng: 12,             // số cạnh tiết diện ống
    vongVien: 10,
    doCong: 0.2,             // độ cong của mũi tên so với khoảng cách hai đầu (đẩy ra phía trước mô hình)
    congToiDa: 0.12,
    doMoDaQua: 0.3,          // mũi tên các bước đã qua: mờ + mảnh lại
    tyLeDaQua: 0.7,
    doMoNhanDaQua: 0.45,
  }),
  xung: Object.freeze({      // bước "tại chỗ" (from = to, vd trao đổi khí): vòng sáng nhịp đập thay cho đèn (không tạo đèn → không biên dịch lại shader)
    banKinh: 0.05,
    tanSo: 2.2,              // nhịp / giây
    doMo: 0.75,
  }),
  thoiGian: Object.freeze({
    veMacDinh: 2.4,          // giây vẽ một mũi tên khi bước không ghi duration
    veToiThieu: 0.6,
    veToiDa: 8,
    camera: 0.8,             // giây camera bay tới khung của bước
    docCoBan: 2.5,           // Tự động chạy: thời gian dừng đọc sau khi vẽ xong = cơ bản + số ký tự × giây/ký tự (kẹp tối đa)
    giayMoiKyTu: 0.028,
    docToiDa: 14,
  }),
  tocDo: Object.freeze([0.5, 1, 1.5, 2]),
  mau: Object.freeze({
    macDinh: "#06b6d4",
    giauO2: "#E53935",       // đỏ – máu giàu O₂
    ngheoO2: "#1E63D6",      // xanh dương – máu nghèo O₂
    xungThanKinh: "#F2C94C",
    tieuHoa: "#43A047",
    nuocTieu: "#8E44AD",
  }),
  camera: Object.freeze({
    tyLeKhung: 0.58,         // vùng của bước chiếm ~58 % phần khung nhìn KHÔNG bị bảng giải thích che
    banKinhToiThieu: 0.18,
    khoangGan: 0.7,
    khoangXa: 4.2,
    huongNhin: Object.freeze([0.31, 0.17, 0.94]), // nhìn chếch ~18° sang phải, ~10° từ trên xuống
    rongBang: 320,           // bề rộng bảng giải thích (máy tính) – dùng khi chưa đo được
    tyLeBangDienThoai: 0.45, // bảng dạng tấm dưới đáy (điện thoại) che tối đa 45 % chiều cao
    manHep: 768,
  }),
  gioiHan: Object.freeze({   // dữ liệu từ AI: cắt cho gọn, chống dữ liệu lạ
    soBuoc: 14,
    boPhanMoiDau: 4,
    tenQuyTrinh: 80,
    tieuDe: 80,
    what: 420,
    why: 700,
    ghiNho: 240,
    luong: 40,
  }),
});
