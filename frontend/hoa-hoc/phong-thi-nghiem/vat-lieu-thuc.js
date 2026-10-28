// Phòng thí nghiệm Hoá ảo – VẬT LIỆU THỰC (texture sinh bằng canvas, KHÔNG tải ảnh ngoài): sàn vinyl có ron, mặt bàn epoxy có hạt,
// gỗ sồi có vân, tường sơn, trần thạch cao ô vuông, inox chải. Mọi texture đều LIỀN MẠCH khi lặp (nhiễu tuần hoàn) và dùng cùng một
// bộ sinh số giả ngẫu nhiên → mỗi lần mở trang ra đúng một hình. Trả về {map, roughnessMap, bumpMap} để gán vào MeshStandardMaterial.
import * as THREE from "three";

// ------------------------------------------------------------ nhiễu giá trị tuần hoàn
const bam = (x, y, s) => {
  let h = (Math.imul(x | 0, 374761393) + Math.imul(y | 0, 668265263) + Math.imul(s | 0, 2147483629)) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967295;
};
const lam = (t) => t * t * (3 - 2 * t);
/** Nhiễu giá trị 2D lặp sau `chuKy` ô (x, y tính theo ô). */
function nhieu(x, y, s, chuKy) {
  const xi = Math.floor(x), yi = Math.floor(y), u = lam(x - xi), v = lam(y - yi);
  const m = (a) => ((a % chuKy) + chuKy) % chuKy;
  const a = bam(m(xi), m(yi), s), b = bam(m(xi + 1), m(yi), s), c = bam(m(xi), m(yi + 1), s), d = bam(m(xi + 1), m(yi + 1), s);
  return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
}
/** Nhiễu phân hình (nhiều tầng), u, v ∈ [0,1); tf = số ô ở tầng thấp nhất (tần số thấp). */
function fbm(u, v, s, tf = 4, tang = 4) {
  let t = 0, bien = 0.5, f = tf, tong = 0;
  for (let i = 0; i < tang; i++) { t += bien * nhieu(u * f, v * f, s + i * 17, f); tong += bien; bien *= 0.5; f *= 2; }
  return t / tong;
}
/** nhieu với tỉ lệ khác nhau theo hai trục (vân gỗ, thép chải). */
function nhieuDan(u, v, s, fu, fv) { return nhieu(u * fu, v * fv, s, Math.max(fu, fv)); }

const kep = (x) => Math.max(0, Math.min(255, x));

/** Vẽ ảnh kích thước kt×kt: fn(u, v, x, y) → [r, g, b] (0–255). */
function veAnh(kt, fn) {
  const c = document.createElement("canvas");
  c.width = c.height = kt;
  const g = c.getContext("2d");
  const d = g.createImageData(kt, kt);
  for (let y = 0; y < kt; y++) for (let x = 0; x < kt; x++) {
    const [r, gg, b] = fn(x / kt, y / kt, x, y), o = (y * kt + x) * 4;
    d.data[o] = kep(r); d.data[o + 1] = kep(gg); d.data[o + 2] = kep(b); d.data[o + 3] = 255;
  }
  g.putImageData(d, 0, 0);
  return c;
}
const xam = (v) => [v, v, v];

function tex(canvas, { laMau = false, lap = true, anisotropy = 8 } = {}) {
  const t = new THREE.CanvasTexture(canvas);
  if (lap) t.wrapS = t.wrapT = THREE.RepeatWrapping;
  if (laMau) t.colorSpace = THREE.SRGBColorSpace;
  t.anisotropy = anisotropy;
  t.needsUpdate = true;
  return t;
}

// ------------------------------------------------------------ các vật liệu
/** Sàn vinyl xám 3×3 ô (mỗi ô 0,6 m khi lặp 1 lần / 1,8 m), ron mờ, hạt li ti, vệt mòn. */
export function sanVinyl(kt = 512) {
  const ON = 3, sinhMau = (u, v, x, y) => {
    const ti = Math.floor(u * ON), tj = Math.floor(v * ON);
    const sac = 1 + (bam(ti, tj, 7) - 0.5) * 0.05;
    const lamTrai = (u * ON - ti) * kt / ON, lamTren = (v * ON - tj) * kt / ON;
    const bien = Math.min(lamTrai, kt / ON - lamTrai, lamTren, kt / ON - lamTren);
    const ron = bien < 1.6 ? 0.78 + 0.06 * bien : 1;
    const hat = fbm(u, v, 31, 90, 2) > 0.62 ? 1.045 : fbm(u, v, 33, 70, 2) < 0.34 ? 0.95 : 1;
    const mon = 0.97 + 0.06 * fbm(u, v, 5, 3, 3);
    const k = sac * ron * hat * mon;
    return [200 * k, 207 * k, 214 * k];
  };
  const map = veAnh(kt, sinhMau);
  const rough = veAnh(kt, (u, v) => xam(255 * (0.82 + 0.16 * fbm(u, v, 9, 6, 3) - (fbm(u, v, 11, 40, 2) > 0.7 ? 0.1 : 0))));
  const bump = veAnh(kt, (u, v, x, y) => {
    const ti = Math.floor(u * ON), tj = Math.floor(v * ON);
    const lt = (u * ON - ti) * kt / ON, lg = (v * ON - tj) * kt / ON;
    const bien = Math.min(lt, kt / ON - lt, lg, kt / ON - lg);
    return xam(bien < 2 ? 90 + 40 * bien : 205 + 30 * fbm(u, v, 21, 120, 2));
  });
  return { map: tex(map, { laMau: true }), roughnessMap: tex(rough), bumpMap: tex(bump) };
}

/** Mặt bàn epoxy xanh navy đậm có hạt sáng li ti (kiểu đá granite nhân tạo). */
export function matBanEpoxy(kt = 512) {
  const map = veAnh(kt, (u, v) => {
    const m = 0.94 + 0.12 * fbm(u, v, 41, 5, 3);
    let k = m;
    const h = bam(Math.floor(u * kt), Math.floor(v * kt), 77);
    if (h > 0.985) k *= 1.55; else if (h < 0.012) k *= 0.7;
    return [36 * k, 58 * k, 98 * k];
  });
  const rough = veAnh(kt, (u, v) => xam(255 * (0.9 + 0.1 * fbm(u, v, 45, 8, 2))));
  return { map: tex(map, { laMau: true }), roughnessMap: tex(rough) };
}

/** Gỗ sồi sáng: vân dọc theo trục v, 3 tấm ghép, mạch ghép tối. */
export function goSoi(kt = 512) {
  const map = veAnh(kt, (u, v) => {
    const tam = Math.floor(u * 3), dich = bam(tam, 3, 13);
    const uu = (u * 3 - tam), vv = (v + dich) % 1;
    const bien = Math.min(uu, 1 - uu) * kt / 3;
    const vong = Math.sin((uu * 11 + fbm(uu, vv, 51 + tam, 3, 3) * 5) * Math.PI) * 0.5 + 0.5;
    const tho = nhieuDan(uu, vv, 61 + tam, 70, 3);
    let k = (0.9 + 0.1 * vong) * (0.94 + 0.1 * tho) * (0.97 + 0.06 * bam(tam, 5, 1));
    if (bien < 1.2) k *= 0.62;
    return [216 * k, 182 * k, 134 * k];
  });
  const rough = veAnh(kt, (u, v) => xam(255 * (0.8 + 0.2 * nhieuDan(u, v, 71, 60, 3))));
  return { map: tex(map, { laMau: true }), roughnessMap: tex(rough) };
}

/** Tường sơn lăn: sắc trắng ngà, hạt rất nhẹ. */
export function tuongSon(kt = 256) {
  const map = veAnh(kt, (u, v) => {
    const k = 0.985 + 0.03 * fbm(u, v, 81, 60, 2) + 0.012 * nhieuDan(u, v, 83, 4, 90);
    return [250 * k, 251 * k, 252 * k];
  });
  return { map: tex(map, { laMau: true }) };
}

/** Trần thạch cao 3×3 tấm 0,6 m có thanh T xám và lỗ tiêu âm. */
export function tranTam(kt = 512) {
  const ON = 3;
  const map = veAnh(kt, (u, v) => {
    const lt = (u * ON % 1), lg = (v * ON % 1);
    const bien = Math.min(lt, 1 - lt, lg, 1 - lg) * kt / ON;
    if (bien < 2.4) return [196, 200, 206];
    const cx = (lt * 9) % 1 - 0.5, cy = (lg * 9) % 1 - 0.5;
    const lo = cx * cx + cy * cy < 0.035 ? 0.9 : 1;
    const k = lo * (0.97 + 0.04 * fbm(u, v, 91, 30, 2));
    return [247 * k, 248 * k, 250 * k];
  });
  return { map: tex(map, { laMau: true }) };
}

/** Inox / nhôm chải: chỉ cần bản đồ độ nhám có vệt dọc. */
export function inoxChai(kt = 256) {
  const rough = veAnh(kt, (u, v) => xam(255 * (0.55 + 0.45 * nhieuDan(u, v, 101, 3, 120))));
  return { roughnessMap: tex(rough) };
}

/** Mặt đồng hồ treo tường (canvas) – kim chỉ 8:05 cho đẹp. */
export function matDongHo(kt = 256) {
  const c = document.createElement("canvas");
  c.width = c.height = kt;
  const g = c.getContext("2d"), m = kt / 2;
  g.fillStyle = "#f8fafc"; g.beginPath(); g.arc(m, m, m - 2, 0, Math.PI * 2); g.fill();
  g.strokeStyle = "#1f2937"; g.lineWidth = 6; g.stroke();
  g.fillStyle = "#1f2937"; g.textAlign = "center"; g.textBaseline = "middle"; g.font = `bold ${kt * 0.13}px sans-serif`;
  for (let i = 1; i <= 12; i++) { const a = (i / 12) * Math.PI * 2 - Math.PI / 2; g.fillText(String(i), m + Math.cos(a) * m * 0.78, m + Math.sin(a) * m * 0.78); }
  const kim = (gio, dai, rong) => { const a = gio * Math.PI * 2 - Math.PI / 2; g.lineWidth = rong; g.lineCap = "round"; g.beginPath(); g.moveTo(m, m); g.lineTo(m + Math.cos(a) * dai, m + Math.sin(a) * dai); g.stroke(); };
  kim((8 + 5 / 60) / 12, m * 0.5, 9); kim(5 / 60, m * 0.72, 5);
  g.fillStyle = "#dc2626"; g.beginPath(); g.arc(m, m, 7, 0, Math.PI * 2); g.fill();
  return tex(c, { laMau: true, lap: false });
}

/** Biển chỉ dẫn phát sáng (lối thoát hiểm…) – chữ trắng trên nền xanh lá. */
export function bienPhatSang(chu, kt = 256, nen = "#15803d") {
  const c = document.createElement("canvas");
  c.width = kt * 2; c.height = kt / 2;
  const g = c.getContext("2d");
  g.fillStyle = nen; g.fillRect(0, 0, c.width, c.height);
  g.fillStyle = "#fff"; g.textAlign = "center"; g.textBaseline = "middle"; g.font = `bold ${kt * 0.26}px sans-serif`;
  g.fillText(chu, c.width / 2, c.height / 2 + 4);
  return tex(c, { laMau: true, lap: false });
}
