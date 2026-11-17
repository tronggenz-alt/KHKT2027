// ============================================================================
// Xử lý khung hình cho nhận diện tay (hàm thuần, test bằng node):
// - cắt khung camera 16:9 về 4:3 ở giữa trước khi nhận diện: bộ dò lòng bàn tay của MediaPipe
//   co ảnh về 192×192 có viền đen, nên ảnh càng "vuông" thì bàn tay càng to (4:3 → to hơn 1,33 lần),
//   tay ở xa vẫn bắt được; hai mép bị cắt nằm ngoài vùng tay hay hoạt động (VUNG x 0,15–0,85);
// - đổi toạ độ điểm tay từ khung đã cắt về khung đầy đủ (đúng như bản Python dùng);
// - thống kê 3 giây gần nhất (tần số camera / nhận diện, tỉ lệ khung thấy tay) và gợi ý khi khó nhận.
// ============================================================================

export const TI_LE_CAT = 4 / 3;
export const RONG_NHAN_DIEN = 640;          // như RONG_NHAN_DIEN của xem_tay.py
export const NGUONG_TOI = 55;               // độ sáng trung bình (0–255) dưới mức này là phòng tối
export const NGUONG_TAY_XA = 0.085;         // cổ tay → gốc ngón giữa, theo chiều cao khung (~1,4 m với camera MacBook)
export const NGUONG_CAMERA_CHAM = 20;       // camera dưới 20 hình/giây (macOS tự hạ khi thiếu sáng)

export function tinhVungCat(vw, vh, tiLeCat = TI_LE_CAT) {
  if (!(vw > 0 && vh > 0) || vw / vh <= tiLeCat + 1e-6) return { sx: 0, sy: 0, sw: vw, sh: vh };
  const sw = Math.round(vh * tiLeCat);
  return { sx: Math.round((vw - sw) / 2), sy: 0, sw, sh: vh };
}

// Khung đã cắt được lật gương rồi mới nhận diện; vùng cắt nằm giữa nên lật gương không đổi vị trí vùng cắt.
export function doiVeKhungDay(kq, cat, vw, vh) {
  const kx = cat.sw / vw, ky = cat.sh / vh, x0 = cat.sx / vw, y0 = cat.sy / vh;
  const ra = { hand_landmarks: [], hand_world_landmarks: [], handedness: [] };
  if (!kq || !Array.isArray(kq.landmarks)) return ra;
  kq.landmarks.forEach((diem, i) => {
    ra.hand_landmarks.push(diem.map((p) => ({ x: x0 + p.x * kx, y: y0 + p.y * ky, z: (p.z || 0) * kx })));
    if (kq.worldLandmarks && kq.worldLandmarks[i]) ra.hand_world_landmarks.push(kq.worldLandmarks[i]);
    const c = (kq.handedness && kq.handedness[i] && kq.handedness[i][0]) || {};
    const ten = c.categoryName || c.category_name || "Tay" + i;
    ra.handedness.push([{ categoryName: ten, category_name: ten, score: c.score }]);
  });
  if (ra.hand_world_landmarks.length !== ra.hand_landmarks.length) ra.hand_world_landmarks = [];
  return ra;
}

export function kichThuocTay(diem, tiLe) {
  const a = diem && diem[0], b = diem && diem[9];
  if (!a || !b) return 0;
  return Math.hypot((a.x - b.x) * tiLe, a.y - b.y);
}

// Thống kê trượt 3 giây: camera ra bao nhiêu hình/giây, nhận diện được bao nhiêu lần/giây, bao nhiêu % lần thấy tay.
export class ThongKeTay {
  constructor(cuaSo = 3) {
    this.cuaSo = cuaSo;
    this.camera = [];
    this.nhanDien = [];          // [t, coTay]
    this.lanCuoiThayTay = null;
  }

  _cat(ds, t) {
    while (ds.length && t - (Array.isArray(ds[0]) ? ds[0][0] : ds[0]) > this.cuaSo) ds.shift();
  }

  ghiKhungCamera(t) {
    this.camera.push(t);
    this._cat(this.camera, t);
  }

  ghiNhanDien(t, soTay) {
    this.nhanDien.push([t, soTay > 0]);
    this._cat(this.nhanDien, t);
    if (soTay > 0 || this.lanCuoiThayTay === null) this.lanCuoiThayTay = t;
  }

  _tanSo(ds) {
    if (ds.length < 2) return 0;
    const dau = Array.isArray(ds[0]) ? ds[0][0] : ds[0];
    const cuoi = Array.isArray(ds[ds.length - 1]) ? ds[ds.length - 1][0] : ds[ds.length - 1];
    return cuoi > dau ? (ds.length - 1) / (cuoi - dau) : 0;
  }

  get tanSoCamera() { return this._tanSo(this.camera); }
  get tanSoNhanDien() { return this._tanSo(this.nhanDien); }
  get tiLeThayTay() {
    return this.nhanDien.length ? this.nhanDien.filter((m) => m[1]).length / this.nhanDien.length : 0;
  }
  giayKhongThayTay(t) { return this.lanCuoiThayTay === null ? 0 : t - this.lanCuoiThayTay; }
}

// Gợi ý quan trọng nhất cho người dùng khi máy khó nhận ra tay (null = mọi thứ ổn).
export function goiYNhanDien({ doSang = -1, tanSoCamera = 0, kichThuocNhoNhat = null, giayKhongThayTay = 0 }) {
  if (doSang >= 0 && doSang < NGUONG_TOI) return "Phòng hơi tối – bật thêm đèn hoặc quay mặt về phía nguồn sáng.";
  if (tanSoCamera > 0 && tanSoCamera < NGUONG_CAMERA_CHAM)
    return `Camera chỉ đạt ${Math.round(tanSoCamera)} hình/giây (thường do thiếu sáng) – tay đi nhanh sẽ khó bắt.`;
  if (kichThuocNhoNhat !== null && kichThuocNhoNhat < NGUONG_TAY_XA) return "Tay hơi xa – đưa lại gần camera hơn (khoảng 40–80 cm).";
  if (giayKhongThayTay > 2) return "Chưa thấy tay – giơ lòng bàn tay về phía camera, các ngón hơi xoè.";
  return null;
}

export default { TI_LE_CAT, RONG_NHAN_DIEN, NGUONG_TOI, NGUONG_TAY_XA, NGUONG_CAMERA_CHAM, tinhVungCat, doiVeKhungDay, kichThuocTay, ThongKeTay, goiYNhanDien };
