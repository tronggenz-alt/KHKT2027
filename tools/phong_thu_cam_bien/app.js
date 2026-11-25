// Phòng thử cảm biến VERA: đọc tay cầm qua Web Serial (sketch firmware/thu_cam_bien), căn chỉnh từng cảm biến,
// rồi thử bằng 4 thao tác thí nghiệm: rót (BMI160 + cảm ứng B), trỏ chọn lọ (BMI160 + cảm ứng A),
// pipet trên khay giếng (joystick), chuẩn độ bằng buret (cò Hall).
// Hệ trục logic (giống firmware): x tiến ra trước, y sang trái, z lên; đơn vị mét. Nhóm `goc` đổi sang hệ three (y lên).
import * as THREE from 'three';

const $ = id => document.getElementById(id);
const clamp = (v, a, b) => Math.min(b, Math.max(a, v));
const DEG = Math.PI / 180;
const V3 = (x = 0, y = 0, z = 0) => new THREE.Vector3(x, y, z);
const tsv = v => v.toFixed(2).replace('.', ',');

// =====================================================================================
// 1. Cấu hình căn chỉnh (lưu trong trình duyệt)
// =====================================================================================
const MAC_DINH = {
  ghep: [[1, 0, 0], [0, 1, 0], [0, 0, 1]],      // trục tay cầm xC, yC, zC viết theo trục chip BMI160
  ghepB1: null,                                  // gia tốc lúc nằm ngang (bước 1 căn trục)
  yaw0: 0,
  joy: { cx: 1650, cy: 1650, minX: 150, maxX: 3150, minY: 150, maxY: 3150, vung: 0.10, tien: { truc: 'y', dau: 1 }, phai: { truc: 'x', dau: 1 }, daTam: false, daBien: false, daHuong: 0 },
  hall: { nha: 1650, bop: 2250, gamma: 1.0, daNha: false, daBop: false },
  cham: { nenA: 0, chamA: 0, nenB: 0, chamB: 0, bat: 0.5, tat: 0.35 },
  rot: { canGiuB: true },
  ong: { tocDo: 0.10 },
};
let cal = taiCal();
function gopSau(a, b) {
  for (const k in b) {
    if (b[k] && typeof b[k] === 'object' && !Array.isArray(b[k]) && a[k] && typeof a[k] === 'object') gopSau(a[k], b[k]);
    else a[k] = b[k];
  }
  return a;
}
function taiCal() {
  try { const c = JSON.parse(localStorage.getItem('vera_cal') || 'null'); if (c) return gopSau(structuredClone(MAC_DINH), c); } catch (e) { /* bỏ qua */ }
  return structuredClone(MAC_DINH);
}
function luuCal() { try { localStorage.setItem('vera_cal', JSON.stringify(cal)); } catch (e) { /* bỏ qua */ } }

// =====================================================================================
// 2. Số liệu thô từ tay cầm
// =====================================================================================
const s = { t: 0, q: [1, 0, 0, 0], a: [0, 0, 1], g: [0, 0, 0], jx: 1650, jy: 1650, sw: 0, hall: 1650, ta: 0, tb: 0, nhiet: 0, imu: 0, moi: 0 };
class CuaSo {
  constructor(n) { this.n = n; this.v = []; }
  them(x) { this.v.push(x); if (this.v.length > this.n) this.v.shift(); }
  get tb() { return this.v.length ? this.v.reduce((a, b) => a + b, 0) / this.v.length : 0; }
  get lc() { const m = this.tb; return this.v.length > 1 ? Math.sqrt(this.v.reduce((a, b) => a + (b - m) ** 2, 0) / (this.v.length - 1)) : 0; }
  xoa() { this.v = []; }
}
const bo = { hall: new CuaSo(50), ta: new CuaSo(30), tb: new CuaSo(30), ax: new CuaSo(50), ay: new CuaSo(50), az: new CuaSo(50), an: new CuaSo(100), jx: new CuaSo(50), jy: new CuaSo(50) };
let demDong = 0, tanSo = 0, lanCuoi = 0;
let dangGhi = false, banGhi = [];

function xuLyDong(l) {
  if (l.startsWith('D,')) {
    const p = l.split(',');
    if (p.length !== 20) return;   // 'D' + 19 số
    const n = p.slice(1).map(Number);
    if (n.some(Number.isNaN)) return;
    s.t = n[0]; s.q = [n[1], n[2], n[3], n[4]]; s.a = [n[5], n[6], n[7]]; s.g = [n[8], n[9], n[10]];
    s.jx = n[11]; s.jy = n[12]; s.sw = n[13]; s.hall = n[14]; s.ta = n[15]; s.tb = n[16]; s.nhiet = n[17]; s.imu = n[18];
    s.moi = performance.now();
    bo.hall.them(s.hall); bo.ta.them(s.ta); bo.tb.them(s.tb); bo.jx.them(s.jx); bo.jy.them(s.jy);
    bo.ax.them(s.a[0]); bo.ay.them(s.a[1]); bo.az.them(s.a[2]); bo.an.them(Math.hypot(...s.a));
    if (dangBien) {
      const j = cal.joy;
      j.minX = Math.min(j.minX, s.jx); j.maxX = Math.max(j.maxX, s.jx); j.minY = Math.min(j.minY, s.jy); j.maxY = Math.max(j.maxY, s.jy);
    }
    demDong++;
    if (dangGhi) banGhi.push(l.slice(2));
  } else if (l.startsWith('#')) ghiNk(l.slice(1).trim());
}

// ---------------- Web Serial ----------------
let cong = null, viet = null, docGiu = null, dangNoi = false;
async function ketNoi() {
  if (cong) return ngatKetNoi();
  if (!('serial' in navigator)) { thongBao('Trình duyệt này không có Web Serial.\nHãy mở bằng Chrome hoặc Edge.'); return; }
  try {
    cong = await navigator.serial.requestPort();
    await cong.open({ baudRate: 921600 });
  } catch (e) { cong = null; ghiNk('Không mở được cổng: ' + e.message); return; }
  dangNoi = true;
  $('gia-lap').checked = false; giaLap = false;
  viet = cong.writable.getWriter();
  $('nut-noi').textContent = 'Ngắt kết nối';
  ghiNk('Đã mở cổng. ESP32 khởi động lại và đo độ lệch con quay: để yên tay cầm 3 giây.');
  thongBao('Để yên tay cầm 3 giây');
  docVong();
}
async function docVong() {
  const giaiMa = new TextDecoder();
  let du = '';
  while (cong && cong.readable && dangNoi) {
    const r = cong.readable.getReader(); docGiu = r;
    try {
      for (;;) {
        const { value, done } = await r.read();
        if (done) break;
        du += giaiMa.decode(value, { stream: true });
        let i;
        while ((i = du.indexOf('\n')) >= 0) { xuLyDong(du.slice(0, i).trim()); du = du.slice(i + 1); }
        if (du.length > 4000) du = '';
      }
    } catch (e) { if (dangNoi) ghiNk('Lỗi đọc cổng: ' + e.message); }
    finally { r.releaseLock(); }
  }
}
async function ngatKetNoi() {
  dangNoi = false;
  try { if (docGiu) await docGiu.cancel(); } catch (e) { /* bỏ qua */ }
  try { if (viet) viet.releaseLock(); } catch (e) { /* bỏ qua */ }
  try { await cong.close(); } catch (e) { /* bỏ qua */ }
  cong = null; viet = null;
  $('nut-noi').textContent = 'Kết nối tay cầm';
  ghiNk('Đã ngắt kết nối.');
}
async function gui(c) {
  if (!viet) { thongBao('Chưa kết nối tay cầm'); return; }
  try { await viet.write(new TextEncoder().encode(c)); } catch (e) { ghiNk('Không gửi được lệnh: ' + e.message); }
}
if ('serial' in navigator) navigator.serial.addEventListener('disconnect', e => { if (cong && e.target === cong) { ghiNk('Tay cầm bị rút cáp.'); ngatKetNoi(); } });

function ghiNk(t) {
  const nk = $('nk');
  nk.textContent += (nk.textContent ? '\n' : '') + t;
  if (nk.textContent.length > 6000) nk.textContent = nk.textContent.slice(-4000);
  nk.scrollTop = nk.scrollHeight;
}
let hetThongBao = 0;
function thongBao(t, giay = 2.2) { const e = $('thong-bao'); e.innerText = t; e.style.opacity = 1; hetThongBao = performance.now() + giay * 1000; }

// =====================================================================================
// 3. Đầu vào đã căn chỉnh (thật hoặc giả lập)
// =====================================================================================
const vao = { jx: 0, jy: 0, sw: false, co: 0, A: false, B: false, mA: 0, mB: 0, q: new THREE.Quaternion(), f: V3(1), l: V3(0, 1), u: V3(0, 0, 1), roll: 0, pitch: 0, yaw: 0, yawTho: 0 };
const truoc = { A: false, B: false, sw: false };
const len = { A: false, B: false, sw: false };   // vừa nhấn trong khung hình này
let giaLap = false;
const gl = { yaw: 0, pitch: 0, roll: 0, phim: new Set(), co: 0, keoCo: null, jx: 0, jy: 0 };

const qTam = new THREE.Quaternion(), qSC = new THREE.Quaternion(), qYaw = new THREE.Quaternion();
const TRUC_Z = V3(0, 0, 1);
function capNhatGhep() {
  const [x, y, z] = cal.ghep;
  qSC.setFromRotationMatrix(new THREE.Matrix4().makeBasis(V3(...x), V3(...y), V3(...z)));
}
capNhatGhep();

function chuanTruc(tho, tam, mn, mx) {
  const d = tho - tam;
  return clamp(d >= 0 ? d / Math.max(50, mx - tam) : d / Math.max(50, tam - mn), -1, 1);
}
function joyChuan() {   // trả về [phải, tiến] trước vùng chết
  const j = cal.joy;
  const n = { x: chuanTruc(s.jx, j.cx, j.minX, j.maxX), y: chuanTruc(s.jy, j.cy, j.minY, j.maxY) };
  return [j.phai.dau * n[j.phai.truc], j.tien.dau * n[j.tien.truc]];
}
function vungChet(x, y, dz) {
  const r = Math.hypot(x, y);
  if (r < dz) return [0, 0];
  const k = Math.min(1, (r - dz) / (1 - dz)) / r;
  return [x * k, y * k];
}
function mucCham(tho, nen, cham) { return Math.abs(cham - nen) < 3 ? 0 : clamp((tho - nen) / (cham - nen), -0.5, 1.5); }

function capNhatVao() {
  if (giaLap) {
    const p = gl.phim;
    const tx = (p.has('ArrowRight') ? 1 : 0) - (p.has('ArrowLeft') ? 1 : 0), ty = (p.has('ArrowUp') ? 1 : 0) - (p.has('ArrowDown') ? 1 : 0);
    gl.jx += (tx - gl.jx) * 0.25; gl.jy += (ty - gl.jy) * 0.25;
    [vao.jx, vao.jy] = vungChet(gl.jx, gl.jy, 0.02);
    vao.sw = p.has('Enter');
    if (p.has(' ')) gl.co = Math.min(1, gl.co + 0.012); else if (gl.keoCo === null) gl.co = Math.max(0, gl.co - 0.03);
    vao.co = gl.keoCo !== null ? gl.keoCo : gl.co;
    vao.A = p.has('z') || p.has('Z'); vao.B = p.has('x') || p.has('X');
    vao.mA = vao.A ? 1 : 0; vao.mB = vao.B ? 1 : 0;
    if (p.has('q') || p.has('Q')) gl.roll -= 1.5;
    if (p.has('e') || p.has('E')) gl.roll += 1.5;
    const q = new THREE.Quaternion().setFromAxisAngle(TRUC_Z, gl.yaw * DEG);
    q.multiply(new THREE.Quaternion().setFromAxisAngle(V3(0, 1, 0), -gl.pitch * DEG));
    q.multiply(new THREE.Quaternion().setFromAxisAngle(V3(1, 0, 0), gl.roll * DEG));
    vao.q.copy(q);
  } else {
    const [x, y] = joyChuan();
    [vao.jx, vao.jy] = vungChet(x, y, cal.joy.vung);
    vao.sw = s.sw === 1;
    const h = cal.hall;
    let t = (h.daNha && h.daBop) ? (s.hall - h.nha) / (h.bop - h.nha) : Math.abs(s.hall - h.nha) / 600;
    t = clamp(t, 0, 1);
    vao.co = t < 0.03 ? 0 : Math.pow(t, h.gamma);
    const c = cal.cham;
    vao.mA = mucCham(s.ta, c.nenA, c.chamA); vao.mB = mucCham(s.tb, c.nenB, c.chamB);
    vao.A = vao.A ? vao.mA > c.tat : vao.mA > c.bat;
    vao.B = vao.B ? vao.mB > c.tat : vao.mB > c.bat;
    qTam.set(s.q[1], s.q[2], s.q[3], s.q[0]).normalize().multiply(qSC);   // hướng tay cầm (chưa trừ yaw)
    vao.yawTho = huongCuaQ(qTam).yaw;
    qYaw.setFromAxisAngle(TRUC_Z, -cal.yaw0);
    vao.q.copy(qYaw).multiply(qTam);
  }
  const h = huongCuaQ(vao.q);
  vao.roll = h.roll; vao.pitch = h.pitch; vao.yaw = h.yaw;
  vao.f.set(1, 0, 0).applyQuaternion(vao.q); vao.l.set(0, 1, 0).applyQuaternion(vao.q); vao.u.set(0, 0, 1).applyQuaternion(vao.q);
  for (const k of ['A', 'B', 'sw']) { len[k] = vao[k] && !truoc[k]; truoc[k] = vao[k]; }
}
function huongCuaQ(q) {
  const f = V3(1, 0, 0).applyQuaternion(q), l = V3(0, 1, 0).applyQuaternion(q), u = V3(0, 0, 1).applyQuaternion(q);
  return { yaw: Math.atan2(f.y, f.x), pitch: Math.asin(clamp(f.z, -1, 1)), roll: Math.atan2(l.z, u.z) };
}
function canHuong() {
  if (giaLap) { gl.yaw = 0; gl.pitch = 0; gl.roll = 0; }
  else { cal.yaw0 = vao.yawTho; luuCal(); }
  thongBao('Đã căn hướng: tay cầm đang chỉ thẳng ra trước');
}

// =====================================================================================
// 4. Cảnh 3D
// =====================================================================================
const khung = $('canh');
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.localClippingEnabled = true;
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.toneMapping = THREE.ACESFilmicToneMapping;
khung.appendChild(renderer.domElement);
const scene = new THREE.Scene();
scene.background = new THREE.Color(0x182127);
scene.fog = new THREE.Fog(0x182127, 3, 7);
const cam = new THREE.PerspectiveCamera(42, 1, 0.01, 30);
const goc = new THREE.Group();
goc.quaternion.setFromRotationMatrix(new THREE.Matrix4().makeBasis(V3(0, 0, -1), V3(-1, 0, 0), V3(0, 1, 0)));
scene.add(goc);
const W = v => V3(-v.y, v.z, -v.x);   // hệ logic -> hệ three

scene.add(new THREE.HemisphereLight(0xdfefff, 0x2a2420, 1.1));
const den = new THREE.DirectionalLight(0xffffff, 2.2); den.position.set(1.5, 3, 2); scene.add(den);
const den2 = new THREE.DirectionalLight(0x9fd8ff, 0.7); den2.position.set(-2, 1.5, -1); scene.add(den2);

const truZ = g => g.rotateX(Math.PI / 2);   // trục y của hình học three -> trục z của hệ logic
const VL = {
  ban: new THREE.MeshStandardMaterial({ color: 0x2b3741, roughness: 0.75 }),
  chan: new THREE.MeshStandardMaterial({ color: 0x1b2329, roughness: 0.6, metalness: 0.4 }),
  sat: new THREE.MeshStandardMaterial({ color: 0x9aa6ae, roughness: 0.35, metalness: 0.8 }),
  thuyTinh: new THREE.MeshPhysicalMaterial({ color: 0xdff4ff, transparent: true, opacity: 0.2, roughness: 0.05, side: THREE.DoubleSide, depthWrite: false }),
  vienTT: new THREE.MeshStandardMaterial({ color: 0xcfe9f5, transparent: true, opacity: 0.55, roughness: 0.1 }),
  trang: new THREE.MeshStandardMaterial({ color: 0xeef2f4, roughness: 0.5 }),
  nhan: new THREE.MeshStandardMaterial({ color: 0x38d6c4, emissive: 0x0d4b45, roughness: 0.4 }),
};
function hop(sx, sy, sz, p, vl) { const m = new THREE.Mesh(new THREE.BoxGeometry(sy, sz, sx), vl); m.position.copy(W(p)); scene.add(m); return m; }  // hộp đặt thẳng trong hệ three
function them(m, p) { if (p) m.position.copy(p); goc.add(m); return m; }

// bàn thí nghiệm dài chạy theo trục y (trái phải), mặt bàn ở z = 0
them(new THREE.Mesh(new THREE.BoxGeometry(0.9, 3.0, 0.04).translate(0, 0, -0.02), VL.ban), V3(0.15, 0, 0));
for (const y of [-1.4, 1.4]) for (const x of [-0.22, 0.52]) them(new THREE.Mesh(new THREE.BoxGeometry(0.05, 0.05, 0.86).translate(0, 0, -0.47), VL.chan), V3(x, y, 0));
const san = new THREE.Mesh(new THREE.PlaneGeometry(12, 12), new THREE.MeshStandardMaterial({ color: 0x11181d, roughness: 0.9 }));
san.rotation.x = -Math.PI / 2; san.position.y = -0.9; scene.add(san);

function chuNoi(text, cao = 0.03, mau = '#dfe7ec', nen = 'rgba(15,20,24,.72)') {
  const c = document.createElement('canvas'), ctx = c.getContext('2d');
  const fs = 56; ctx.font = `600 ${fs}px "Segoe UI", sans-serif`;
  const dong = String(text).split('\n');
  const w = Math.ceil(Math.max(...dong.map(d => ctx.measureText(d).width))) + 28;
  c.width = w; c.height = dong.length * (fs + 8) + 18;
  ctx.font = `600 ${fs}px "Segoe UI", sans-serif`;
  ctx.fillStyle = nen; ctx.beginPath(); ctx.roundRect(0, 0, c.width, c.height, 14); ctx.fill();
  ctx.fillStyle = mau; ctx.textBaseline = 'top';
  dong.forEach((d, i) => ctx.fillText(d, 14, 9 + i * (fs + 8)));
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
  const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: t, depthTest: false, transparent: true }));
  sp.scale.set(cao * c.width / c.height * dong.length, cao * dong.length, 1);
  sp.renderOrder = 20;
  return sp;
}

// bình tam giác: mặt cắt (bán kính, cao) quay quanh trục z
const BIEN_BINH = [[0, 0], [0.040, 0], [0.042, 0.004], [0.041, 0.010], [0.016, 0.075], [0.014, 0.080], [0.014, 0.100], [0.0165, 0.102]];
const BIEN_LONG = [[0, 0.002], [0.038, 0.002], [0.0388, 0.009], [0.0145, 0.075], [0.0122, 0.080], [0.0122, 0.098]];
function latheZ(bien, doan = 40) { return truZ(new THREE.LatheGeometry(bien.map(([r, z]) => new THREE.Vector2(r, z)), doan)); }
function banKinhLong(z) {
  for (let i = 1; i < BIEN_LONG.length; i++) {
    const [r0, z0] = BIEN_LONG[i - 1], [r1, z1] = BIEN_LONG[i];
    if (z <= z1) return z1 === z0 ? r1 : r0 + (r1 - r0) * (z - z0) / (z1 - z0);
  }
  return BIEN_LONG.at(-1)[0];
}
const BANG_THE_TICH = [];   // [z, mL] của lòng bình
{ let v = 0; for (let z = 0.002; z <= 0.098; z += 0.0005) { BANG_THE_TICH.push([z, v]); v += Math.PI * banKinhLong(z + 0.00025) ** 2 * 0.0005 * 1e6; } }
function caoTheoTheTich(ml) {
  for (let i = 1; i < BANG_THE_TICH.length; i++) if (BANG_THE_TICH[i][1] >= ml) return BANG_THE_TICH[i][0];
  return 0.098;
}
function taoBinh(p, mauLong) {
  const g = new THREE.Group(); g.position.copy(p);
  g.add(new THREE.Mesh(latheZ(BIEN_BINH), VL.thuyTinh));
  const mp = new THREE.Plane(V3(0, -1, 0), 0);
  const ml = new THREE.MeshPhysicalMaterial({ color: mauLong, transparent: true, opacity: 0.72, roughness: 0.1, side: THREE.DoubleSide, clippingPlanes: [mp] });
  const long = new THREE.Mesh(latheZ(BIEN_LONG), ml); long.renderOrder = 1; g.add(long);
  goc.add(g);
  return { g, mp, ml, datTheTich(ml_) { mp.constant = p.z + caoTheoTheTich(ml_); long.visible = ml_ > 0.05; } };
}

// =====================================================================================
// 5. Mô hình tay cầm VERA Band 2 (từ trang giới thiệu)
// =====================================================================================
const MAU_CT = { than_phai: [0xeef2f4, 0.45, 0.05], than_trai: [0xeef2f4, 0.45, 0.05], nap: [0x15191d, 0.5, 0.2], chen: [0x18c8e6, 0.4, 0.1], co: [0xaab4ba, 0.32, 0.7],
  tam_mu_tay: [0x18c8e6, 0.42, 0.1], nap_pin: [0x15191d, 0.5, 0.2], nut_phu: [0xaab4ba, 0.32, 0.7], chu: [0x15191d, 0.5, 0.1] };
const CHOT_CO = V3(0.025, 0, 0.057);   // trục quay cò (m, hệ tay cầm)
let moHinh = null;            // nhóm gốc (đơn vị m, hệ tay cầm), có .userData.co = nhóm cò
const choMoHinh = [];
function taoMoHinh() {
  return Promise.all([fetch('mo_hinh/band2.json').then(r => r.json()), fetch('mo_hinh/band2.bin').then(r => r.arrayBuffer())]).then(([tt, buf]) => {
    const g = new THREE.Group(), nhomCo = new THREE.Group();
    nhomCo.position.copy(CHOT_CO); g.add(nhomCo);
    for (const p of tt) {
      if (p.lk) continue;
      const geo = new THREE.BufferGeometry();
      geo.setAttribute('position', new THREE.InterleavedBufferAttribute(new THREE.InterleavedBuffer(new Int16Array(buf, p.v[0], p.v[1] * 5), 5), 3, 0, false));
      geo.setAttribute('normal', new THREE.InterleavedBufferAttribute(new THREE.InterleavedBuffer(new Int8Array(buf, p.v[0], p.v[1] * 10), 10), 3, 6, true));
      geo.setIndex(new THREE.BufferAttribute(new (p.i[2] === 4 ? Uint32Array : Uint16Array)(buf, p.i[0], p.i[1]), 1));
      const [mau, nham, kl] = MAU_CT[p.ma] || [new THREE.Color(p.mau).getHex(), 0.5, 0.2];
      const m = new THREE.Mesh(geo, new THREE.MeshStandardMaterial({ color: mau, roughness: nham, metalness: kl }));
      m.scale.setScalar(1 / 256 / 1000);
      if (p.ma === 'co') { m.position.copy(CHOT_CO).negate(); nhomCo.add(m); } else g.add(m);
    }
    g.userData.co = nhomCo;
    return g;
  });
}
function moHinhThay() {   // hộp đơn giản khi không tải được mô hình
  const g = new THREE.Group();
  g.add(new THREE.Mesh(truZ(new THREE.CylinderGeometry(0.017, 0.017, 0.09, 20)).translate(0, 0, 0.025), VL.trang));
  const dau = new THREE.Mesh(new THREE.BoxGeometry(0.07, 0.05, 0.03), VL.trang); dau.position.set(0.01, 0, 0.085); g.add(dau);
  const co = new THREE.Group(); co.position.copy(CHOT_CO);
  const la = new THREE.Mesh(new THREE.BoxGeometry(0.012, 0.012, 0.03), VL.sat); la.position.set(0.003, 0, -0.015); co.add(la);
  g.add(co); g.userData.co = co;
  return g;
}
function trucToaDo(dai = 0.07) {
  const g = new THREE.Group();
  for (const [v, mau] of [[V3(1, 0, 0), 0xff5a5a], [V3(0, 1, 0), 0x6fdc8c], [V3(0, 0, 1), 0x5aa8ff]]) g.add(new THREE.ArrowHelper(v, V3(), dai, mau, dai * 0.18, dai * 0.1));
  return g;
}

// =====================================================================================
// 6. Các trạm
// =====================================================================================
const TRAM = {};
let theHienTai = 'can';

// ---------- Trạm căn chỉnh: mô hình tay cầm lớn quay theo cảm biến ----------
{
  const Y = 1.25, tam = V3(0.10, Y, 0.20);
  const de = them(new THREE.Mesh(truZ(new THREE.CylinderGeometry(0.06, 0.07, 0.012, 40)).translate(0, 0, 0.006), VL.chan), V3(0.10, Y, 0));
  them(new THREE.Mesh(truZ(new THREE.CylinderGeometry(0.004, 0.004, 0.12, 12)).translate(0, 0, 0.06), VL.sat), V3(0.10, Y, 0.012));
  const xoay = them(new THREE.Group(), tam.clone());
  const trong = new THREE.Group(); trong.position.set(0, 0, -0.045); xoay.add(trong);
  xoay.add(trucToaDo(0.11));
  // vòng tròn chỉ cần gạt trên đỉnh và 2 chấm cảm ứng (chỉ để nhìn)
  const cham = { A: new THREE.Mesh(new THREE.SphereGeometry(0.006, 16, 12), new THREE.MeshStandardMaterial({ color: 0x334, emissive: 0x000000 })), B: null };
  cham.B = new THREE.Mesh(new THREE.SphereGeometry(0.006, 16, 12), new THREE.MeshStandardMaterial({ color: 0x334, emissive: 0x000000 }));
  cham.A.position.set(0.0, 0.03, 0.06); cham.B.position.set(0.04, 0, 0.0); trong.add(cham.A, cham.B);
  const chamJoy = new THREE.Mesh(new THREE.SphereGeometry(0.005, 16, 12), new THREE.MeshStandardMaterial({ color: 0xffc857, emissive: 0x6a4a00 }));
  trong.add(chamJoy);
  const nhanTram = them(chuNoi('CĂN CHỈNH', 0.035, '#38d6c4'), V3(0.10, Y, 0.36));
  choMoHinh.push(m => { trong.add(m); });
  TRAM.can = {
    cam: { dich: V3(0.10, Y, 0.19), az: 35, el: 18, r: 0.55 },
    capNhat() {
      xoay.quaternion.copy(vao.q);
      const m = trong.children.find(c => c.userData.co);
      if (m) m.userData.co.rotation.y = 13 * DEG * vao.co;
      cham.A.material.emissive.setHex(vao.A ? 0x38d6c4 : 0x000000);
      cham.B.material.emissive.setHex(vao.B ? 0x38d6c4 : 0x000000);
      chamJoy.position.set(0.004 + vao.jy * 0.012, -vao.jx * 0.012, 0.112);
      chamJoy.material.emissive.setHex(vao.sw ? 0xffc857 : 0x6a4a00);
    },
    hud() {
      return `<b>Căn chỉnh</b><br>Nghiêng trái/phải (roll): <b>${(vao.roll / DEG).toFixed(1)}°</b><br>Ngóc/chúc (pitch): <b>${(vao.pitch / DEG).toFixed(1)}°</b><br>Hướng ngang (yaw): <b>${(vao.yaw / DEG).toFixed(1)}°</b><br>`
        + `Cần gạt: ${vao.jx.toFixed(2)}, ${vao.jy.toFixed(2)} ${vao.sw ? '<b>[nhấn]</b>' : ''}<br>Cò: <b>${Math.round(vao.co * 100)}%</b> · A ${vao.A ? '<b>chạm</b>' : '–'} · B ${vao.B ? '<b>chạm</b>' : '–'}`;
    },
  };
  void de; void nhanTram;
}

// ---------- Trạm 1: Rót (BMI160 + cảm ứng B) ----------
{
  const Y = 0.75, H = 0.09, R = 0.03, VMAX = Math.PI * R * R * H * 1e6, P = V3(0.02, Y, 0.27);
  const PHEU = V3(0.02, Y - 0.06, 0.165), R_PHEU = 0.037;
  const st = { vCoc: 200, vBinh: 0, vTran: 0, dangRot: false, ketQua: '', daRot: false };
  const coc = them(new THREE.Group(), P.clone());
  coc.add(new THREE.Mesh(truZ(new THREE.CylinderGeometry(R, R, H, 40, 1, true)), VL.thuyTinh));
  const day = new THREE.Mesh(new THREE.CircleGeometry(R, 40).translate(0, 0, -H / 2), VL.thuyTinh); coc.add(day);
  coc.add(new THREE.Mesh(new THREE.TorusGeometry(R, 0.0012, 8, 48).translate(0, 0, H / 2), VL.vienTT));
  for (let i = 1; i <= 4; i++) {   // vạch 50, 100, 150, 200 mL
    const z = -H / 2 + (i * 50 / VMAX) * H;
    const v = new THREE.Mesh(new THREE.BoxGeometry(0.0006, 0.012, 0.0008), VL.vienTT); v.position.set(R, 0, z); coc.add(v);
  }
  const mpCoc = new THREE.Plane(V3(0, -1, 0), 0);
  const longCoc = new THREE.Mesh(truZ(new THREE.CylinderGeometry(R * 0.96, R * 0.96, H * 0.99, 40)), new THREE.MeshPhysicalMaterial({ color: 0x2f8cff, transparent: true, opacity: 0.7, side: THREE.DoubleSide, clippingPlanes: [mpCoc] }));
  longCoc.renderOrder = 1; coc.add(longCoc);
  // phễu và bình hứng
  them(new THREE.Mesh(truZ(new THREE.CylinderGeometry(R_PHEU, 0.006, 0.05, 40, 1, true)).translate(0, 0, -0.025), VL.thuyTinh), PHEU.clone());
  them(new THREE.Mesh(truZ(new THREE.CylinderGeometry(0.005, 0.005, 0.03, 12, 1, true)).translate(0, 0, -0.065), VL.thuyTinh), PHEU.clone());
  them(new THREE.Mesh(new THREE.TorusGeometry(R_PHEU, 0.0012, 8, 48), VL.vienTT), PHEU.clone());
  const binh = taoBinh(V3(PHEU.x, PHEU.y, 0), 0x2f8cff);
  // vạch đích 100 mL trên bình
  const zDich = caoTheoTheTich(100);
  them(new THREE.Mesh(new THREE.TorusGeometry(banKinhLong(zDich) + 0.003, 0.0009, 6, 48), new THREE.MeshBasicMaterial({ color: 0xffc857 })), V3(PHEU.x, PHEU.y, zDich));
  // vũng tràn và dòng chảy
  const vung = them(new THREE.Mesh(new THREE.CircleGeometry(1, 32), new THREE.MeshStandardMaterial({ color: 0x2f8cff, transparent: true, opacity: 0.55, roughness: 0.05 })), V3(P.x, P.y, 0.001));
  vung.visible = false;
  const dong = them(new THREE.Mesh(truZ(new THREE.CylinderGeometry(1, 1, 1, 10, 1, true)).translate(0, 0, 0.5), new THREE.MeshStandardMaterial({ color: 0x5aa8ff, transparent: true, opacity: 0.75 })));
  dong.visible = false;
  them(chuNoi('RÓT DUNG DỊCH', 0.03, '#38d6c4'), V3(0.02, Y, 0.43));
  them(chuNoi('đích 100 mL', 0.016, '#ffc857'), V3(PHEU.x, PHEU.y - 0.06, zDich));
  const qThang = new THREE.Quaternion();
  const datLai = () => { Object.assign(st, { vCoc: 200, vBinh: 0, vTran: 0, ketQua: '', daRot: false }); vung.visible = false; };
  TRAM.rot = {
    cam: { dich: V3(0.02, Y - 0.03, 0.18), az: 18, el: 16, r: 0.62 },
    datLai,
    capNhat(dt) {
      const giu = !cal.rot.canGiuB || vao.B;
      coc.quaternion.slerp(giu ? vao.q : qThang, 1 - Math.exp(-dt * 18));
      const u = V3(0, 0, 1).applyQuaternion(coc.quaternion);
      const nghieng = Math.acos(clamp(u.z, -1, 1));
      const h = (st.vCoc / VMAX) * H;
      const gocTran = Math.atan2(H - h, R);
      let luu = nghieng > gocTran ? Math.min(90, 3.5 * (nghieng - gocTran) / DEG) : 0;   // mL/s
      const dv = Math.min(st.vCoc, luu * dt);
      // điểm thấp nhất của miệng cốc
      const dinh = P.clone().addScaledVector(u, H / 2);
      const d = V3(0, 0, -1).addScaledVector(u, u.z);
      if (d.lengthSq() < 1e-6) d.set(1, 0, 0);
      const mieng = dinh.addScaledVector(d.normalize(), R);
      const vaoPheu = Math.hypot(mieng.x - PHEU.x, mieng.y - PHEU.y) < R_PHEU * 0.92;
      st.dangRot = dv > 0;
      if (dv > 0) {
        st.daRot = true;
        st.vCoc -= dv;
        if (vaoPheu) st.vBinh += dv; else { st.vTran += dv; vung.visible = true; vung.position.set(mieng.x, mieng.y, 0.001); vung.scale.setScalar(0.01 + Math.sqrt(st.vTran) * 0.006); }
        const zDay = vaoPheu ? PHEU.z - 0.02 : 0.0;
        dong.visible = true; dong.position.set(mieng.x, mieng.y, zDay);
        const r = 0.0012 + 0.0004 * Math.sqrt(luu);
        dong.scale.set(r, r, Math.max(0.001, mieng.z - zDay));
      } else dong.visible = false;
      // mặt chất lỏng nằm ngang
      const dayCoc = P.clone().addScaledVector(u, -H / 2);
      mpCoc.constant = dayCoc.z + h * Math.max(0.05, u.z) + 0.0004;
      longCoc.visible = st.vCoc > 0.3;
      binh.datTheTich(st.vBinh);
      if (st.daRot && !st.dangRot && nghieng < 15 * DEG) {
        const sai = st.vBinh - 100;
        st.ketQua = Math.abs(sai) <= 2 && st.vTran < 1 ? `ĐẠT: ${st.vBinh.toFixed(1)} mL` : `Chưa đạt: lệch ${sai >= 0 ? '+' : ''}${sai.toFixed(1)} mL${st.vTran >= 1 ? `, tràn ${st.vTran.toFixed(1)} mL` : ''}`;
      }
      this.nghieng = nghieng; this.gocTran = gocTran; this.luu = luu; this.giu = giu;
    },
    hud() {
      return `<b>Rót 100 mL vào bình qua phễu</b><br>${this.giu ? 'Đang cầm cốc' : '<span style="color:#ffc857">Chạm và giữ B để cầm cốc</span>'}<br>`
        + `Nghiêng cốc: <b>${(this.nghieng / DEG || 0).toFixed(0)}°</b> (bắt đầu chảy ở ${(this.gocTran / DEG || 0).toFixed(0)}°)<br>`
        + `Trong cốc: ${st.vCoc.toFixed(1)} mL · Tốc độ chảy: ${(this.luu || 0).toFixed(0)} mL/s<br>`
        + `Vào bình: <span class="lon">${st.vBinh.toFixed(1)}</span> mL · Tràn: ${st.vTran.toFixed(1)} mL<br>${st.ketQua ? `<b>${st.ketQua}</b>` : ''}`;
    },
  };
}

// ---------- Trạm 2: Trỏ chọn lọ (BMI160 + cảm ứng A) ----------
{
  const Y = 0.0, O = V3(-0.18, Y, 0.20), KC = 0.62;
  const MAU = [0xff6b6b, 0xffc857, 0x6fdc8c, 0x38d6c4, 0x5aa8ff, 0xb48cff, 0xff8fd0, 0xf0f0f0, 0xffa45a, 0x9ad94a];
  const lo = [];
  const YAWS = [30, 15, 0, -15, -30], PITCHS = [4, 20];   // đánh số từ trái sang phải
  let k = 0;
  for (const p of PITCHS) for (const y of YAWS) {
    const huong = V3(Math.cos(p * DEG) * Math.cos(y * DEG), Math.cos(p * DEG) * Math.sin(y * DEG), Math.sin(p * DEG));
    const vt = O.clone().addScaledVector(huong, KC);
    const g = them(new THREE.Group(), vt.clone().add(V3(0, 0, -0.03)));
    const than = new THREE.Mesh(truZ(new THREE.CylinderGeometry(0.018, 0.018, 0.045, 24)).translate(0, 0, 0.0225), new THREE.MeshStandardMaterial({ color: MAU[k], roughness: 0.3, emissive: 0x000000 }));
    const co = new THREE.Mesh(truZ(new THREE.CylinderGeometry(0.008, 0.008, 0.015, 16)).translate(0, 0, 0.052), VL.trang);
    g.add(than, co);
    const so = chuNoi(String(k + 1), 0.022); so.position.set(0, 0, 0.08); g.add(so);
    them(new THREE.Mesh(new THREE.BoxGeometry(0.06, 0.07, 0.006), VL.ban), vt.clone().add(V3(0, 0, -0.033)));
    lo.push({ g, than, huong, vt });
    k++;
  }
  // chân giá đỡ
  for (const y of [-0.38, 0.38]) them(new THREE.Mesh(new THREE.BoxGeometry(0.02, 0.02, 0.45).translate(0, 0, 0.225), VL.chan), V3(0.42, Y + y, 0));
  const vong = them(new THREE.Mesh(new THREE.TorusGeometry(0.032, 0.003, 8, 40), new THREE.MeshBasicMaterial({ color: 0xffc857 })));
  const tia = new THREE.Line(new THREE.BufferGeometry().setFromPoints([V3(), V3(1, 0, 0)]), new THREE.LineBasicMaterial({ color: 0xff4d6d }));
  them(tia, O.clone());
  const diem = them(new THREE.Mesh(new THREE.SphereGeometry(0.006, 12, 10), new THREE.MeshBasicMaterial({ color: 0xff4d6d })));
  const tay = them(new THREE.Group(), O.clone());
  choMoHinh.push(m => { const b = m.clone(); b.position.set(-0.035, 0, -0.06); tay.add(b); });
  them(chuNoi('TRỎ CHỌN LỌ', 0.03, '#38d6c4'), V3(0.42, Y, 0.52));
  const st = { dich: 0, tBat: 0, lan: [], sai: 0, tro: -1, giuAB: 0 };
  const moi = () => { let d; do d = Math.floor(Math.random() * lo.length); while (d === st.dich && lo.length > 1); st.dich = d; st.tBat = performance.now(); };
  const datLai = () => { st.lan = []; st.sai = 0; moi(); };
  datLai();
  TRAM.tro = {
    cam: { dich: V3(0.12, Y, 0.30), az: 0, el: 8, r: 0.82 },
    datLai,
    capNhat(dt) {
      tay.quaternion.copy(vao.q);
      let tot = -1, gocMin = 7 * DEG;
      lo.forEach((l, i) => {
        const g = vao.f.angleTo(l.vt.clone().sub(O).normalize());
        if (g < gocMin) { gocMin = g; tot = i; }
        l.than.material.emissive.setHex(i === tot ? 0x444444 : 0x000000);
        l.g.scale.setScalar(i === st.dich ? 1.12 : 1);
      });
      st.tro = tot;
      const dai = tot >= 0 ? lo[tot].vt.distanceTo(O) : 0.9;
      tia.quaternion.setFromUnitVectors(V3(1, 0, 0), vao.f); tia.scale.setScalar(dai);
      diem.position.copy(O).addScaledVector(vao.f, dai);
      vong.position.copy(lo[st.dich].vt).add(V3(0, 0, -0.03)); vong.quaternion.setFromUnitVectors(V3(0, 0, 1), lo[st.dich].huong.clone().negate());
      if (vao.A && vao.B) { st.giuAB += dt; if (st.giuAB > 1) { canHuong(); st.giuAB = -99; } } else st.giuAB = 0;
      if ((len.A || len.sw) && st.lan.length < 10) {
        if (tot === st.dich) {
          st.lan.push((performance.now() - st.tBat) / 1000);
          if (st.lan.length >= 10) thongBao(`Xong 10 lượt\nTrung bình ${tsv(st.lan.reduce((a, b) => a + b) / 10)} s, chọn nhầm ${st.sai} lần`, 4);
          else { thongBao('Đúng!', 0.6); moi(); }
        } else { st.sai++; thongBao(tot < 0 ? 'Chưa trỏ vào lọ nào' : 'Nhầm lọ', 0.8); }
      }
    },
    hud() {
      const tb = st.lan.length ? st.lan.reduce((a, b) => a + b) / st.lan.length : 0;
      return `<b>Trỏ vào lọ số <span class="lon">${st.dich + 1}</span>, chạm A để chọn</b><br>Đang trỏ: ${st.tro >= 0 ? 'lọ ' + (st.tro + 1) : '–'}<br>`
        + `Lượt: ${st.lan.length}/10 · TB ${tsv(tb)} s · Nhầm ${st.sai}<br>Hướng: yaw ${(vao.yaw / DEG).toFixed(1)}°, pitch ${(vao.pitch / DEG).toFixed(1)}°<br><span style="color:#8a9aa6">Giữ A + B 1 giây để căn hướng lại</span>`;
    },
  };
}

// ---------- Trạm 3: Pipet trên khay giếng (joystick) ----------
{
  const Y = -0.5, TAM = V3(0.12, Y, 0.0), HANG = 4, COT = 6, BUOC = 0.03, Z_KHAY = 0.016;
  them(new THREE.Mesh(new THREE.BoxGeometry(HANG * BUOC + 0.02, COT * BUOC + 0.02, Z_KHAY).translate(0, 0, Z_KHAY / 2), new THREE.MeshStandardMaterial({ color: 0xdfe7ec, roughness: 0.4, transparent: true, opacity: 0.9 })), TAM.clone());
  const gieng = [];
  for (let i = 0; i < HANG; i++) for (let j = 0; j < COT; j++) {
    const p = V3(TAM.x + (i - (HANG - 1) / 2) * BUOC, TAM.y + (j - (COT - 1) / 2) * BUOC, Z_KHAY + 0.0005);
    const m = them(new THREE.Mesh(new THREE.CircleGeometry(0.0105, 28), new THREE.MeshStandardMaterial({ color: 0x9aa6ae, roughness: 0.3 })), p.clone());
    gieng.push({ p, m, day: false, ten: 'ABCD'[i] + (j + 1) });
  }
  const dichVong = them(new THREE.Mesh(new THREE.TorusGeometry(0.0135, 0.0018, 8, 32), new THREE.MeshBasicMaterial({ color: 0xffc857 })));
  const pip = them(new THREE.Group(), V3(TAM.x, TAM.y, 0));
  pip.add(new THREE.Mesh(truZ(new THREE.CylinderGeometry(0.008, 0.008, 0.12, 20)).translate(0, 0, 0.13), VL.trang));
  pip.add(new THREE.Mesh(truZ(new THREE.CylinderGeometry(0.0045, 0.0008, 0.05, 16)).translate(0, 0, 0.045), new THREE.MeshPhysicalMaterial({ color: 0xffffff, transparent: true, opacity: 0.6 })));
  pip.add(new THREE.Mesh(truZ(new THREE.CylinderGeometry(0.0055, 0.0055, 0.012, 16)).translate(0, 0, 0.196), VL.nhan));
  const bong = them(new THREE.Mesh(new THREE.RingGeometry(0.003, 0.0045, 24), new THREE.MeshBasicMaterial({ color: 0xff4d6d })));
  const giot = them(new THREE.Mesh(new THREE.SphereGeometry(0.0025, 10, 8), new THREE.MeshStandardMaterial({ color: 0xff3fa4 })));
  giot.visible = false;
  them(chuNoi('PIPET · CẦN GẠT', 0.03, '#38d6c4'), V3(0.12, Y, 0.3));
  const st = { x: TAM.x, y: TAM.y, dich: 0, tBat: 0, lan: [], lech: [], sai: 0, giotZ: -1, giotP: null };
  const moi = () => { let d; do d = Math.floor(Math.random() * gieng.length); while (gieng[d].day || d === st.dich); st.dich = d; st.tBat = performance.now(); };
  const datLai = () => { gieng.forEach(g => { g.day = false; g.m.material.color.setHex(0x9aa6ae); }); st.lan = []; st.lech = []; st.sai = 0; moi(); };
  datLai();
  TRAM.ong = {
    cam: { dich: V3(0.12, Y, 0.03), az: 0, el: 48, r: 0.42 },
    datLai,
    capNhat(dt) {
      const v = cal.ong.tocDo;
      st.x = clamp(st.x + vao.jy * v * dt, TAM.x - 0.08, TAM.x + 0.08);
      st.y = clamp(st.y - vao.jx * v * dt, TAM.y - 0.11, TAM.y + 0.11);
      pip.position.set(st.x, st.y, Z_KHAY + 0.004);
      bong.position.set(st.x, st.y, Z_KHAY + 0.001);
      dichVong.position.copy(gieng[st.dich].p);
      if ((len.B || len.sw) && st.giotZ < 0 && st.lan.length < 10) { st.giotZ = Z_KHAY + 0.02; st.giotP = V3(st.x, st.y); giot.visible = true; }
      if (st.giotZ >= 0) {
        st.giotZ -= dt * 0.25; giot.position.set(st.giotP.x, st.giotP.y, st.giotZ);
        if (st.giotZ <= Z_KHAY + 0.002) {
          st.giotZ = -1; giot.visible = false;
          let gan = 0, kc = 1;
          gieng.forEach((g, i) => { const d = Math.hypot(g.p.x - st.giotP.x, g.p.y - st.giotP.y); if (d < kc) { kc = d; gan = i; } });
          if (kc < 0.011) {
            gieng[gan].m.material.color.setHex(gan === st.dich ? 0xff3fa4 : 0xffa45a);
            if (gan === st.dich) {
              gieng[gan].day = true; st.lan.push((performance.now() - st.tBat) / 1000); st.lech.push(kc * 1000);
              if (st.lan.length >= 10) thongBao(`Xong 10 giếng\nTB ${tsv(st.lan.reduce((a, b) => a + b) / 10)} s, lệch tâm TB ${tsv(st.lech.reduce((a, b) => a + b) / 10)} mm`, 4);
              else moi();
            } else st.sai++;
          } else { st.sai++; thongBao('Nhỏ ra ngoài giếng', 0.8); }
        }
      }
    },
    hud() {
      const tb = a => a.length ? tsv(a.reduce((x, y) => x + y) / a.length) : '–';
      return `<b>Đưa pipet tới giếng <span class="lon">${gieng[st.dich].ten}</span>, chạm B (hoặc nhấn cần) để nhỏ</b><br>`
        + `Cần gạt: phải ${vao.jx.toFixed(2)} · tiến ${vao.jy.toFixed(2)}<br>Lượt ${st.lan.length}/10 · TB ${tb(st.lan)} s · lệch tâm TB ${tb(st.lech)} mm · sai ${st.sai}`;
    },
  };
}

// ---------- Trạm 4: Chuẩn độ bằng buret (cò Hall) ----------
{
  const Y = -1.0, X = 0.10, Z0 = 0.62, Z50 = 0.25, R_ONG = 0.0065;
  const st = { vRa: 0, vBinh: 0, vTd: 10, giot: [], hong: 0, soGiot: 0, ketQua: '', dongChay: false };
  them(new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.1, 0.01).translate(0, 0, 0.005), VL.chan), V3(X - 0.04, Y, 0));
  them(new THREE.Mesh(truZ(new THREE.CylinderGeometry(0.005, 0.005, 0.72, 12)).translate(0, 0, 0.36), VL.sat), V3(X - 0.09, Y, 0.01));
  them(new THREE.Mesh(new THREE.BoxGeometry(0.09, 0.012, 0.012), VL.sat), V3(X - 0.045, Y, 0.5));
  them(new THREE.Mesh(truZ(new THREE.CylinderGeometry(R_ONG, R_ONG, Z0 - Z50 + 0.04, 20, 1, true)).translate(0, 0, (Z0 + Z50) / 2), VL.thuyTinh), V3(X, Y, 0));
  for (let ml = 0; ml <= 50; ml += 1) {
    const z = Z0 - (ml / 50) * (Z0 - Z50), dai = ml % 5 === 0 ? 0.008 : 0.004;
    them(new THREE.Mesh(new THREE.BoxGeometry(0.0005, dai, 0.0006), new THREE.MeshBasicMaterial({ color: 0xdfe7ec })), V3(X - R_ONG, Y + dai / 2, z));
    if (ml % 10 === 0) them(chuNoi(String(ml), 0.011, '#dfe7ec', 'rgba(0,0,0,0)'), V3(X - R_ONG, Y + 0.022, z));
  }
  const mpBuret = new THREE.Plane(V3(0, -1, 0), Z0);
  them(new THREE.Mesh(truZ(new THREE.CylinderGeometry(R_ONG * 0.9, R_ONG * 0.9, Z0 - Z50 + 0.035, 20)).translate(0, 0, (Z0 + Z50) / 2 - 0.002), new THREE.MeshPhysicalMaterial({ color: 0xbfe3ff, transparent: true, opacity: 0.55, clippingPlanes: [mpBuret] })), V3(X, Y, 0));
  them(new THREE.Mesh(truZ(new THREE.CylinderGeometry(0.0025, 0.001, 0.035, 12)).translate(0, 0, Z50 - 0.055), VL.thuyTinh), V3(X, Y, 0));
  const khoa = them(new THREE.Group(), V3(X, Y, Z50 - 0.03));
  khoa.add(new THREE.Mesh(new THREE.BoxGeometry(0.004, 0.03, 0.006), new THREE.MeshStandardMaterial({ color: 0x38d6c4 })));
  const binh = taoBinh(V3(X, Y, 0), 0xeaf4ff);
  const dong = them(new THREE.Mesh(truZ(new THREE.CylinderGeometry(0.0011, 0.0011, 1, 8, 1, true)).translate(0, 0, 0.5), new THREE.MeshStandardMaterial({ color: 0xbfe3ff, transparent: true, opacity: 0.8 })), V3(X, Y, 0.09));
  dong.visible = false;
  const giotM = [];
  for (let i = 0; i < 30; i++) { const g = them(new THREE.Mesh(new THREE.SphereGeometry(0.0022, 10, 8), new THREE.MeshStandardMaterial({ color: 0xbfe3ff }))); g.visible = false; giotM.push(g); }
  them(chuNoi('CHUẨN ĐỘ · CÒ HALL', 0.03, '#38d6c4'), V3(X, Y, 0.72));
  const MAU_TRONG = new THREE.Color(0xeaf4ff), MAU_HONG = new THREE.Color(0xff3fa4);
  const datLai = () => { Object.assign(st, { vRa: 0, vBinh: 0, vTd: Math.round((9 + Math.random() * 5) * 100) / 100, giot: [], hong: 0, soGiot: 0, ketQua: '', tich: 0 }); };
  datLai();
  const ketThuc = () => {
    const sai = st.vBinh - st.vTd;
    st.ketQua = sai >= -0.005 && sai <= 0.10 ? `ĐẠT: dừng ở ${tsv(st.vBinh)} mL (điểm tương đương ${tsv(st.vTd)} mL)` : sai < 0 ? `Chưa tới điểm tương đương (thiếu ${tsv(-sai)} mL)` : `Quá ${tsv(sai)} mL so với điểm tương đương ${tsv(st.vTd)} mL`;
    thongBao(st.ketQua.split(':')[0], 1.5);
  };
  TRAM.buret = {
    cam: { dich: V3(X, Y - 0.03, 0.33), az: 8, el: 4, r: 1.05 },
    datLai, ketThuc,
    capNhat(dt) {
      const c = vao.co;
      const luu = c < 0.04 ? 0 : 0.02 + 1.6 * Math.pow((c - 0.04) / 0.96, 1.6);   // mL/s
      khoa.rotation.x = c * Math.PI / 2;
      const ra = Math.min(luu * dt, 50 - st.vRa);
      st.vRa += ra;
      mpBuret.constant = Z0 - (st.vRa / 50) * (Z0 - Z50);
      st.dongChay = luu > 0.45;
      if (st.dongChay) { st.vBinh += ra; dong.visible = true; dong.position.set(X, Y, 0.09); dong.scale.set(1, 1, Z50 - 0.09 - 0.055); st.hong = Math.max(st.hong, Math.exp(-(st.vTd - st.vBinh) / 0.35)); }
      else {
        dong.visible = false;
        st.tich = (st.tich || 0) + ra;
        while (st.tich >= 0.05) { st.tich -= 0.05; st.giot.push({ z: Z50 - 0.072, v: 0 }); }
      }
      for (const g of st.giot) { g.v += 9.8 * dt; g.z -= g.v * dt; }
      const roi = st.giot.filter(g => g.z <= 0.095);
      for (let i = 0; i < roi.length; i++) {
        st.vBinh += 0.05; st.soGiot++;
        st.hong = Math.max(st.hong, Math.exp(-(st.vTd - st.vBinh) / 0.35));
      }
      st.giot = st.giot.filter(g => g.z > 0.095);
      giotM.forEach((m, i) => { const g = st.giot[i]; m.visible = !!g; if (g) m.position.set(X, Y, g.z); });
      const ben = st.vBinh >= st.vTd ? clamp(0.55 + (st.vBinh - st.vTd) * 1.5, 0, 1) : 0;
      const tg = 0.25 + 4 * Math.exp(-(st.vTd - st.vBinh) / 0.25);
      st.hong = Math.max(ben, st.hong - dt / tg * 0.6);
      binh.ml.color.copy(MAU_TRONG).lerp(MAU_HONG, clamp(st.hong, 0, 1));
      binh.datTheTich(25 + st.vBinh);
      if (len.A) ketThuc();
    },
    hud() {
      const c = vao.co;
      return `<b>Chuẩn độ: dừng ở giọt đầu tiên làm bình hồng bền, rồi chạm A</b><br>Cò: <b>${Math.round(c * 100)}%</b> ${st.dongChay ? '(chảy thành dòng)' : c >= 0.04 ? '(nhỏ giọt)' : '(khóa)'}<br>`
        + `Đã nhỏ: <span class="lon">${st.vBinh.toFixed(2).replace('.', ',')}</span> mL · ${st.soGiot} giọt<br>${st.ketQua ? `<b>${st.ketQua}</b>` : ''}`;
    },
  };
}

// nạp mô hình rồi đưa vào các trạm
taoMoHinh().catch(e => { ghiNk('Không tải được mô hình tay cầm (' + e.message + '), dùng hình đơn giản.'); return moHinhThay(); })
  .then(m => { moHinh = m; choMoHinh.forEach(f => f(m)); });

// =====================================================================================
// 7. Camera (kéo chuột trái để xoay quanh trạm, lăn để phóng to)
// =====================================================================================
const camSt = { dich: V3(), az: 0, el: 20, r: 0.6, dichMuc: V3(), azMuc: 0, elMuc: 20, rMuc: 0.6 };
function datCam(c, ngay = false) {
  camSt.dichMuc.copy(W(c.dich)); camSt.azMuc = c.az; camSt.elMuc = c.el; camSt.rMuc = c.r;
  if (ngay) { camSt.dich.copy(camSt.dichMuc); camSt.az = c.az; camSt.el = c.el; camSt.r = c.r; }
}
function capNhatCam(dt) {
  const k = 1 - Math.exp(-dt * 6);
  camSt.dich.lerp(camSt.dichMuc, k); camSt.az += (camSt.azMuc - camSt.az) * k; camSt.el += (camSt.elMuc - camSt.el) * k; camSt.r += (camSt.rMuc - camSt.r) * k;
  const a = camSt.az * DEG, e = camSt.el * DEG;
  cam.position.set(camSt.dich.x + camSt.r * Math.cos(e) * Math.sin(a), camSt.dich.y + camSt.r * Math.sin(e), camSt.dich.z + camSt.r * Math.cos(e) * Math.cos(a));
  cam.lookAt(camSt.dich);
}
let keo = null;
renderer.domElement.addEventListener('pointerdown', e => { keo = { x: e.clientX, y: e.clientY, shift: e.shiftKey }; renderer.domElement.setPointerCapture(e.pointerId); });
renderer.domElement.addEventListener('pointermove', e => {
  if (!keo) return;
  const dx = e.clientX - keo.x, dy = e.clientY - keo.y; keo.x = e.clientX; keo.y = e.clientY;
  if (giaLap && keo.shift) { gl.yaw -= dx * 0.25; gl.pitch = clamp(gl.pitch - dy * 0.25, -85, 85); }
  else { camSt.azMuc -= dx * 0.3; camSt.elMuc = clamp(camSt.elMuc + dy * 0.3, -10, 80); }
});
renderer.domElement.addEventListener('pointerup', () => { keo = null; });
renderer.domElement.addEventListener('wheel', e => { e.preventDefault(); camSt.rMuc = clamp(camSt.rMuc * (e.deltaY > 0 ? 1.1 : 0.9), 0.15, 3); }, { passive: false });
addEventListener('keydown', e => { if (e.target.tagName === 'INPUT') return; gl.phim.add(e.key); if (giaLap && [' ', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.key)) e.preventDefault(); });
addEventListener('keyup', e => gl.phim.delete(e.key));
addEventListener('blur', () => gl.phim.clear());

function coKichThuoc() {
  const w = khung.clientWidth, h = khung.clientHeight;
  renderer.setSize(w, h, false); cam.aspect = w / h; cam.updateProjectionMatrix();
}
addEventListener('resize', coKichThuoc); coKichThuoc();

// =====================================================================================
// 8. Bảng bên trái
// =====================================================================================
let dangBien = false, doTroi = null;
const BANG = {
  can: () => `
  <div class="muc"><h3>Tổng quan <i id="o-tong-tt"></i></h3><div class="than"><div class="so" id="o-tong"></div>
    <div class="goi">Mới nạp sketch hoặc mới cắm cáp: để yên tay cầm 3 giây cho mạch đo độ lệch con quay.</div>
    <div class="hang"><button class="nho" data-l="gui-i">In thông tin chẩn đoán</button></div></div></div>

  <div class="muc"><h3>1. BMI160 (hướng) <i id="o-imu-tt"></i></h3><div class="than">
    <div class="so" id="o-imu"></div>
    <div class="buoc"><span>a) Đặt tay cầm nằm yên trên bàn</span><button class="nho" data-l="gui-k">Đo lệch con quay</button></div>
    <div class="buoc"><span id="b-trongluc">b) Kiểm tra độ lớn gia tốc ≈ 1,00 g</span></div>
    <div class="buoc"><span id="b-ghep1">c) Căn trục gắn, bước 1: đặt tay cầm đứng thẳng như khi cầm (cán thẳng đứng), giữ yên</span><button class="nho" data-l="ghep1">Lưu</button></div>
    <div class="buoc"><span id="b-ghep2">bước 2: chúc mũi (phía ngón trỏ) xuống khoảng 45°, giữ yên</span><button class="nho" data-l="ghep2">Lưu</button></div>
    <div class="buoc"><span>d) Kiểm tra chiều trục (sau khi căn):</span></div>
    <div class="so" id="o-truc"></div>
    <div class="buoc"><span>e) Chỉ tay cầm thẳng vào màn hình</span><button class="nho" data-l="can-huong">Căn hướng</button></div>
    <div class="buoc"><span id="b-troi">f) Đo trôi hướng 60 giây (để yên)</span><button class="nho" data-l="do-troi">Đo</button></div>
    <div class="hang"><button class="nho" data-l="bo-ghep">Bỏ căn trục gắn</button></div></div></div>

  <div class="muc"><h3>2. Cần gạt KY-023 <i id="o-joy-tt"></i></h3><div class="than">
    <canvas class="ve" id="ve-joy" width="170" height="170"></canvas>
    <div class="so" id="o-joy"></div>
    <div class="buoc"><span>a) Thả tay khỏi cần</span><button class="nho" data-l="joy-tam">Lấy tâm</button></div>
    <div class="buoc"><span>b) Bấm rồi xoay cần sát mép 3 vòng</span><button class="nho" data-l="joy-bien" id="nut-bien">Đo biên</button></div>
    <div class="buoc"><span>c) Giữ cần đẩy hết về phía TRƯỚC (xa người)</span><button class="nho" data-l="joy-tien">Lưu</button></div>
    <div class="buoc"><span>d) Giữ cần đẩy hết sang PHẢI</span><button class="nho" data-l="joy-phai">Lưu</button></div>
    <div class="goi">Vùng chết <b id="o-vung">${Math.round(cal.joy.vung * 100)}%</b>: cần thả mà nhân vật vẫn trôi thì tăng.</div>
    <input type="range" min="0" max="30" value="${Math.round(cal.joy.vung * 100)}" data-k="vung"></div></div>

  <div class="muc"><h3>3. Cò Hall SS49E <i id="o-hall-tt"></i></h3><div class="than">
    <div class="thanh" id="th-hall"><div></div><s id="m-nha"></s><s id="m-bop"></s></div>
    <div class="so" id="o-hall"></div>
    <div class="buoc"><span>a) Nhả cò hoàn toàn, giữ yên</span><button class="nho" data-l="hall-nha">Lưu</button></div>
    <div class="buoc"><span>b) Bóp cò hết mức và giữ</span><button class="nho" data-l="hall-bop">Lưu</button></div>
    <div class="goi">Đường cong <b id="o-gamma">${cal.hall.gamma.toFixed(1)}</b> (lớn hơn 1: đầu cò nhạy ít, dễ nhỏ từng giọt)</div>
    <input type="range" min="5" max="30" value="${Math.round(cal.hall.gamma * 10)}" data-k="gamma"></div></div>

  <div class="muc"><h3>4. Hai nút cảm ứng <i id="o-cham-tt"></i></h3><div class="than">
    <div class="goi">A (ngón cái)</div><div class="thanh" id="th-a"><div></div><s style="left:50%"></s></div>
    <div class="goi">B (ngón trỏ)</div><div class="thanh" id="th-b"><div></div><s style="left:50%"></s></div>
    <div class="so" id="o-cham"></div>
    <div class="buoc"><span>a) Không chạm nút nào</span><button class="nho" data-l="cham-nen">Lưu nền</button></div>
    <div class="buoc"><span>b) Đặt ngón cái lên A như khi dùng</span><button class="nho" data-l="cham-a">Lưu</button></div>
    <div class="buoc"><span>c) Đặt ngón trỏ lên B</span><button class="nho" data-l="cham-b">Lưu</button></div></div></div>

  <div class="muc"><h3>Lưu và xuất</h3><div class="than">
    <div class="goi">Kết quả căn chỉnh tự lưu trong trình duyệt. Tải tệp để gửi nhóm hoặc đưa vào firmware chính.</div>
    <div class="hang"><button class="nho" data-l="tai-cal">Tải tệp căn chỉnh</button><button class="nho" data-l="chep-cal">Chép tóm tắt</button><button class="nho" data-l="mac-dinh">Về mặc định</button></div></div></div>`,
  rot: () => `
  <div class="muc"><h3>Rót dung dịch: BMI160 + cảm ứng B</h3><div class="than">
    <div class="goi">Cốc 200 mL nằm trong tay. <b>Chạm và giữ B</b> để cầm cốc, nghiêng tay cầm sang phải để rót qua phễu vào bình. Rót đúng <b>100 mL</b> (vạch vàng), không để tràn ra bàn.</div>
    <div class="goi">Cốc càng vơi thì phải nghiêng càng nhiều mới chảy, giống cốc thật. Đạt khi lệch ≤ 2 mL và không tràn.</div>
    <div class="goi">Kiểm tra: tay cầm phải nghiêng đúng chiều tay bạn, dừng tay thì cốc đứng yên (không trôi), nhả B thì cốc dựng thẳng lại.</div>
    <label class="chk"><input type="checkbox" data-k="canGiuB" ${cal.rot.canGiuB ? 'checked' : ''}> Phải giữ B mới cầm được cốc</label>
    <div class="hang" style="margin-top:6px"><button data-l="dat-lai">Làm lại</button><button data-l="can-huong">Căn hướng</button></div></div></div>`,
  tro: () => `
  <div class="muc"><h3>Trỏ chọn lọ: BMI160 + cảm ứng A</h3><div class="than">
    <div class="goi">Tia đỏ đi theo mũi tay cầm. Trỏ vào lọ có <b>vòng vàng</b> rồi <b>chạm A</b> (hoặc nhấn cần). 10 lượt, đo thời gian và số lần nhầm.</div>
    <div class="goi">Kiểm tra trôi: chỉ tay cầm thẳng ra trước rồi bấm <b>Căn hướng</b>. Làm hết 10 lượt, sau đó chỉ lại vào lọ số 3 (giữa hàng dưới): tia lệch nhiều tức là hướng ngang bị trôi.</div>
    <div class="hang"><button data-l="dat-lai">Làm lại</button><button data-l="can-huong">Căn hướng</button></div></div></div>`,
  ong: () => `
  <div class="muc"><h3>Pipet trên khay giếng: cần gạt</h3><div class="than">
    <div class="goi">Đẩy cần để đưa đầu pipet tới giếng có <b>vòng vàng</b>, <b>chạm B</b> (hoặc nhấn cần) để nhỏ một giọt. 10 giếng, đo thời gian và độ lệch tâm.</div>
    <div class="goi">Kiểm tra: thả cần thì pipet đứng yên hẳn; đẩy trước thì đi ra xa, đẩy phải thì đi sang phải. Sai chiều thì làm lại bước c, d ở mục Căn chỉnh.</div>
    <div class="goi">Tốc độ <b id="o-tocdo">${Math.round(cal.ong.tocDo * 100)} cm/s</b></div>
    <input type="range" min="3" max="25" value="${Math.round(cal.ong.tocDo * 100)}" data-k="tocDo">
    <div class="hang"><button data-l="dat-lai">Làm lại</button></div></div></div>`,
  buret: () => `
  <div class="muc"><h3>Chuẩn độ: cò Hall</h3><div class="than">
    <div class="goi">Bóp cò để mở khóa buret: bóp nhẹ thì nhỏ từng giọt (0,05 mL), bóp mạnh thì chảy thành dòng. Bình chứa NaOH + phenolphtalein chưa biết nồng độ.</div>
    <div class="goi">Gần điểm tương đương, mỗi giọt làm bình hồng thoáng qua. Dừng ở <b>giọt đầu tiên làm bình hồng bền</b>, rồi <b>chạm A</b> (hoặc bấm Kết thúc). Đạt khi quá điểm tương đương không quá 0,10 mL (2 giọt).</div>
    <div class="goi">Kiểm tra cò: bóp rất nhẹ phải nhỏ được từng giọt rời; nhả cò thì khóa ngay. Không được thì chỉnh lại đường cong ở mục Cò Hall.</div>
    <div class="hang"><button data-l="ket-thuc">Kết thúc</button><button data-l="dat-lai">Làm lại (mẫu mới)</button></div></div></div>`,
};

function veBang() {
  $('noi').innerHTML = BANG[theHienTai]();
  document.querySelectorAll('.muc > h3').forEach(h => h.addEventListener('click', () => h.parentElement.classList.toggle('dong')));
}
document.querySelectorAll('#the button').forEach(b => b.addEventListener('click', () => chonThe(b.dataset.the)));
function chonThe(t) {
  theHienTai = t;
  document.querySelectorAll('#the button').forEach(b => b.classList.toggle('chon', b.dataset.the === t));
  veBang(); datCam(TRAM[t].cam);
}

// trung bình 0,5 giây gần nhất
const tb = (c, n = 50) => { const v = c.v.slice(-n); return v.reduce((a, b) => a + b, 0) / Math.max(1, v.length); };
function canKetNoi() { if (giaLap || !s.moi) { thongBao('Cần kết nối tay cầm thật để căn chỉnh'); return false; } return true; }
const chuanHoa = v => { const n = Math.hypot(...v); return v.map(x => x / n); };
const cheo = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
const vo = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

$('noi').addEventListener('click', e => {
  const l = e.target.dataset && e.target.dataset.l;
  if (!l) return;
  const j = cal.joy, h = cal.hall, c = cal.cham;
  switch (l) {
    case 'gui-i': gui('i'); break;
    case 'gui-k': gui('k'); thongBao('Để yên tay cầm 2 giây'); break;
    case 'can-huong': canHuong(); break;
    case 'dat-lai': TRAM[theHienTai].datLai(); break;
    case 'ket-thuc': TRAM.buret.ketThuc(); break;
    case 'ghep1':
      if (!canKetNoi()) return;
      cal.ghepB1 = chuanHoa([tb(bo.ax), tb(bo.ay), tb(bo.az)]); luuCal(); thongBao('Đã lưu bước 1. Giờ chúc mũi xuống 45°'); break;
    case 'ghep2': {
      if (!canKetNoi()) return;
      if (!cal.ghepB1) { thongBao('Làm bước 1 trước'); return; }
      const z = cal.ghepB1, a2 = [tb(bo.ax), tb(bo.ay), tb(bo.az)];
      const ngang = a2.map((v, i) => -(v - vo(a2, z) * z[i]));
      if (Math.hypot(...ngang) < 0.25) { thongBao('Chưa chúc đủ: nghiêng mũi xuống nhiều hơn rồi lưu lại'); return; }
      const x = chuanHoa(ngang), y = cheo(z, x);
      cal.ghep = [x, y, z]; capNhatGhep(); cal.yaw0 = 0; luuCal();
      thongBao('Đã căn trục gắn BMI160.\nBấm Căn hướng khi chỉ thẳng vào màn hình', 3);
      break;
    }
    case 'bo-ghep': cal.ghep = structuredClone(MAC_DINH.ghep); cal.ghepB1 = null; capNhatGhep(); luuCal(); thongBao('Đã bỏ căn trục gắn'); break;
    case 'do-troi':
      if (!canKetNoi()) return;
      doTroi = { t0: performance.now(), y0: vao.yawTho, cong: 0, truoc: vao.yawTho, p0: vao.pitch, r0: vao.roll }; thongBao('Để yên tay cầm 60 giây'); break;
    case 'joy-tam':
      if (!canKetNoi()) return;
      j.cx = tb(bo.jx); j.cy = tb(bo.jy); j.daTam = true; luuCal(); thongBao('Đã lấy tâm cần gạt'); break;
    case 'joy-bien':
      if (!canKetNoi()) return;
      if (!dangBien) { dangBien = true; j.minX = j.maxX = s.jx; j.minY = j.maxY = s.jy; $('nut-bien').textContent = 'Xong'; thongBao('Xoay cần sát mép 3 vòng rồi bấm Xong'); }
      else { dangBien = false; j.daBien = true; luuCal(); $('nut-bien').textContent = 'Đo biên'; thongBao('Đã lưu biên cần gạt'); }
      break;
    case 'joy-tien': case 'joy-phai': {
      if (!canKetNoi()) return;
      const dx = tb(bo.jx, 20) - j.cx, dy = tb(bo.jy, 20) - j.cy;
      if (Math.max(Math.abs(dx), Math.abs(dy)) < 400) { thongBao('Cần chưa đẩy đủ xa (hoặc chưa lấy tâm)'); return; }
      const ket = Math.abs(dx) > Math.abs(dy) ? { truc: 'x', dau: Math.sign(dx) } : { truc: 'y', dau: Math.sign(dy) };
      if (l === 'joy-tien') j.tien = ket; else j.phai = ket;
      j.daHuong |= l === 'joy-tien' ? 1 : 2;
      if (j.tien.truc === j.phai.truc && j.daHuong === 3) thongBao('Hai hướng trùng một trục: làm lại bước c và d', 3);
      else thongBao(`${l === 'joy-tien' ? 'Trước' : 'Phải'} = trục ${ket.truc.toUpperCase()} ${ket.dau > 0 ? 'tăng' : 'giảm'}`);
      luuCal(); break;
    }
    case 'hall-nha': if (!canKetNoi()) return; h.nha = tb(bo.hall); h.daNha = true; luuCal(); thongBao(`Nhả cò: ${Math.round(h.nha)} mV`); break;
    case 'hall-bop': if (!canKetNoi()) return; h.bop = tb(bo.hall, 30); h.daBop = true; luuCal(); thongBao(`Bóp hết: ${Math.round(h.bop)} mV`); break;
    case 'cham-nen': if (!canKetNoi()) return; c.nenA = tb(bo.ta, 20); c.nenB = tb(bo.tb, 20); luuCal(); thongBao('Đã lưu mức nền'); break;
    case 'cham-a': if (!canKetNoi()) return; c.chamA = tb(bo.ta, 20); luuCal(); thongBao(`Chạm A: ${Math.round(c.chamA)} (nền ${Math.round(c.nenA)})`); break;
    case 'cham-b': if (!canKetNoi()) return; c.chamB = tb(bo.tb, 20); luuCal(); thongBao(`Chạm B: ${Math.round(c.chamB)} (nền ${Math.round(c.nenB)})`); break;
    case 'tai-cal': taiTep(`vera_can_chinh_${dauThoiGian()}.json`, JSON.stringify(cal, null, 1), 'application/json'); break;
    case 'chep-cal': navigator.clipboard.writeText(tomTat()).then(() => thongBao('Đã chép tóm tắt')); break;
    case 'mac-dinh': if (confirm('Xóa toàn bộ căn chỉnh?')) { cal = structuredClone(MAC_DINH); capNhatGhep(); luuCal(); veBang(); } break;
  }
});
$('noi').addEventListener('input', e => {
  const k = e.target.dataset && e.target.dataset.k;
  if (!k) return;
  if (k === 'vung') { cal.joy.vung = e.target.value / 100; $('o-vung').textContent = e.target.value + '%'; }
  if (k === 'gamma') { cal.hall.gamma = e.target.value / 10; $('o-gamma').textContent = cal.hall.gamma.toFixed(1); }
  if (k === 'tocDo') { cal.ong.tocDo = e.target.value / 100; $('o-tocdo').textContent = e.target.value + ' cm/s'; }
  if (k === 'canGiuB') cal.rot.canGiuB = e.target.checked;
  luuCal();
});

function tomTat() {
  const j = cal.joy, h = cal.hall, c = cal.cham, g = cal.ghep.map(v => v.map(x => x.toFixed(3)).join(' '));
  return [`VERA căn chỉnh ${new Date().toLocaleString('vi-VN')}`,
    `BMI160 trục gắn (cột x, y, z của tay cầm theo trục chip): ${g.join(' | ')}`,
    `Cần gạt: tâm X ${Math.round(j.cx)} mV, Y ${Math.round(j.cy)} mV; biên X ${Math.round(j.minX)}–${Math.round(j.maxX)}, Y ${Math.round(j.minY)}–${Math.round(j.maxY)}; trước = ${j.tien.truc}${j.tien.dau > 0 ? '+' : '-'}, phải = ${j.phai.truc}${j.phai.dau > 0 ? '+' : '-'}; vùng chết ${Math.round(j.vung * 100)}%`,
    `Cò Hall: HALL_NHA = ${Math.round(h.nha)} mV, HALL_BOP = ${Math.round(h.bop)} mV, đường cong ${h.gamma.toFixed(1)}`,
    `Cảm ứng: A nền ${Math.round(c.nenA)} chạm ${Math.round(c.chamA)}; B nền ${Math.round(c.nenB)} chạm ${Math.round(c.chamB)}`].join('\n');
}
function dauThoiGian() { const d = new Date(), p = n => String(n).padStart(2, '0'); return `${d.getFullYear()}${p(d.getMonth() + 1)}${p(d.getDate())}_${p(d.getHours())}${p(d.getMinutes())}${p(d.getSeconds())}`; }
function taiTep(ten, noi, kieu) { const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([noi], { type: kieu })); a.download = ten; a.click(); setTimeout(() => URL.revokeObjectURL(a.href), 2000); }

$('nut-noi').addEventListener('click', ketNoi);
$('gia-lap').addEventListener('change', e => { giaLap = e.target.checked; if (giaLap) thongBao('Giả lập: phím mũi tên = cần gạt, Space = cò,\nZ = chạm A, X = chạm B, Enter = nhấn cần,\nShift + kéo chuột = xoay tay cầm, Q/E = nghiêng', 5); });
$('nut-ghi').addEventListener('click', () => {
  if (!dangGhi) { if (!s.moi) { thongBao('Chưa có số liệu từ tay cầm'); return; } dangGhi = true; banGhi = []; $('nut-ghi').textContent = '■ Dừng ghi'; ghiNk('Bắt đầu ghi số liệu thô.'); }
  else {
    dangGhi = false; $('nut-ghi').textContent = '● Ghi CSV';
    const dau = 'ms,qw,qx,qy,qz,ax_g,ay_g,az_g,gx_dps,gy_dps,gz_dps,joyX_mV,joyY_mV,joySW,hall_mV,chamA,chamB,nhietDo_C,imuOK';
    taiTep(`vera_cam_bien_${dauThoiGian()}.csv`, dau + '\n' + banGhi.join('\n') + '\n', 'text/csv');
    ghiNk(`Đã lưu ${banGhi.length} dòng (${(banGhi.length / 100).toFixed(0)} giây).`);
  }
});

// cập nhật số liệu trong bảng (10 lần/giây)
function veJoy() {
  const cv = $('ve-joy'); if (!cv) return;
  const x = cv.getContext('2d'), w = cv.width, c = w / 2, r = w / 2 - 12;
  x.clearRect(0, 0, w, w);
  x.strokeStyle = '#2a3640'; x.lineWidth = 1;
  x.beginPath(); x.arc(c, c, r, 0, 7); x.stroke(); x.beginPath(); x.moveTo(c - r, c); x.lineTo(c + r, c); x.moveTo(c, c - r); x.lineTo(c, c + r); x.stroke();
  x.strokeStyle = '#ffc85788'; x.beginPath(); x.arc(c, c, r * cal.joy.vung, 0, 7); x.stroke();
  let tx = 0, ty = 0;
  if (!giaLap) [tx, ty] = joyChuan(); else [tx, ty] = [gl.jx, gl.jy];
  x.fillStyle = '#8a9aa6'; x.beginPath(); x.arc(c + tx * r, c - ty * r, 4, 0, 7); x.fill();
  x.fillStyle = vao.sw ? '#ffc857' : '#38d6c4'; x.beginPath(); x.arc(c + vao.jx * r, c - vao.jy * r, 6, 0, 7); x.fill();
  x.fillStyle = '#8a9aa6'; x.font = '11px Segoe UI'; x.fillText('trước', c - 13, 10); x.fillText('phải', w - 26, c - 4);
}
const dat = (id, html) => { const e = $(id); if (e) e.innerHTML = html; };
const nhanTT = (ok, chu) => `<span class="${ok === true ? 'dat' : ok === false ? 'hong' : 'canh-bao'}">${chu}</span>`;
function capNhatBang() {
  const tt = $('tt'), co = !!cong && performance.now() - s.moi < 500;
  tt.innerHTML = giaLap ? '<span class="ok">Đang giả lập</span> (không có tay cầm)' : !cong ? 'Chưa kết nối. Cần Chrome hoặc Edge.' : co ? `<span class="ok">Đang nhận số liệu</span> · ${tanSo} dòng/s${dangGhi ? ' · <span class="loi">đang ghi ' + banGhi.length + '</span>' : ''}` : '<span class="loi">Đã mở cổng nhưng chưa có số liệu</span> (đúng sketch thu_cam_bien chưa? đóng Serial Monitor chưa?)';
  if (theHienTai !== 'can') return;
  const an = bo.an.tb, aC = V3(...s.a).applyQuaternion(qSC.clone().invert());
  dat('o-tong', `Tần số ${tanSo} dòng/s · BMI160 ${s.imu ? 'OK' : 'LỖI'} · nhiệt độ chip ${s.nhiet.toFixed(1)} °C`);
  dat('o-tong-tt', co ? nhanTT(tanSo > 80, tanSo > 80 ? 'ổn' : 'chậm') : nhanTT(null, 'chưa có số liệu'));
  dat('o-imu', `gia tốc chip (g): ${s.a.map(v => v.toFixed(3).padStart(7)).join('')}\ncon quay (°/s): ${s.g.map(v => v.toFixed(2).padStart(8)).join('')}\nroll ${(vao.roll / DEG).toFixed(1)}°  pitch ${(vao.pitch / DEG).toFixed(1)}°  yaw ${(vao.yaw / DEG).toFixed(1)}°`);
  dat('o-imu-tt', !s.imu ? nhanTT(false, 'không thấy chip') : nhanTT(Math.abs(an - 1) < 0.03, `|a| = ${an.toFixed(3)} g`));
  dat('b-trongluc', `b) Độ lớn gia tốc lúc để yên: ${nhanTT(Math.abs(an - 1) < 0.03, an.toFixed(3) + ' g')} (đạt khi 0,97–1,03)`);
  dat('b-ghep1', `c) Căn trục gắn, bước 1: dựng tay cầm thẳng như khi cầm, giữ yên ${cal.ghepB1 ? nhanTT(true, '✓') : ''}`);
  const yen = Math.hypot(...s.g) < 3;
  dat('o-truc', `trục tay cầm (g):  x ${aC.x.toFixed(2)}  y ${aC.y.toFixed(2)}  z ${aC.z.toFixed(2)}\n`
    + `${nhanTT(aC.z > 0.95 ? true : null, '• Dựng thẳng: z ≈ +1')}\n${nhanTT(aC.x < -0.5 ? true : null, '• Chúc mũi xuống: x âm')}\n${nhanTT(aC.y > 0.5 ? true : null, '• Nghiêng sang phải (bên phải xuống): y dương')}${yen ? '' : '\n(đang rung, giữ yên để đọc)'}`);
  if (doTroi) {
    let d = vao.yawTho - doTroi.truoc; d = Math.atan2(Math.sin(d), Math.cos(d)); doTroi.cong += d; doTroi.truoc = vao.yawTho;
    const gay = (performance.now() - doTroi.t0) / 1000, troi = doTroi.cong / DEG;
    if (gay >= 60) {
      dat('b-troi', `f) Trôi hướng ngang sau 60 s: ${nhanTT(Math.abs(troi) < 1, troi.toFixed(2) + '°')} (tốt khi dưới 1°)`);
      ghiNk(`Đo trôi 60 s: yaw ${troi.toFixed(2)}°, pitch ${((vao.pitch - doTroi.p0) / DEG).toFixed(2)}°, roll ${((vao.roll - doTroi.r0) / DEG).toFixed(2)}°`);
      doTroi = null;
    } else dat('b-troi', `f) Đang đo trôi: ${gay.toFixed(0)}/60 s, đã trôi ${troi.toFixed(2)}°`);
  }
  const j = cal.joy;
  dat('o-joy', `thô X ${s.jx} mV, Y ${s.jy} mV ${vao.sw ? '[NHẤN]' : ''}\ntâm ${Math.round(j.cx)}, ${Math.round(j.cy)} · nhiễu lúc thả ±${bo.jx.lc.toFixed(0)} mV\nbiên X ${Math.round(j.minX)}–${Math.round(j.maxX)}, Y ${Math.round(j.minY)}–${Math.round(j.maxY)}`);
  const tamLech = Math.abs(j.cx - 1650) > 400 || Math.abs(j.cy - 1650) > 400;
  dat('o-joy-tt', !j.daTam ? nhanTT(null, 'chưa căn') : tamLech ? nhanTT(null, 'tâm lệch, cấp 3V3 chưa?') : j.daHuong === 3 ? nhanTT(true, 'đã căn') : nhanTT(null, 'thiếu chiều'));
  veJoy();
  const h = cal.hall, tam = 3300;
  const th = $('th-hall');
  if (th) {
    th.firstElementChild.style.width = (vao.co * 100) + '%';
    $('m-nha').style.left = (h.nha / tam * 100) + '%'; $('m-bop').style.left = (h.bop / tam * 100) + '%';
  }
  const hanhTrinh = Math.abs(h.bop - h.nha), nhieu = bo.hall.lc;
  dat('o-hall', `thô ${s.hall} mV · cò ${Math.round(vao.co * 100)}% · nhiễu ±${nhieu.toFixed(1)} mV\nnhả ${Math.round(h.nha)} mV, bóp ${Math.round(h.bop)} mV, hành trình ${Math.round(hanhTrinh)} mV${nhieu > 0 ? ` (≈ ${Math.round(hanhTrinh / Math.max(1, 2 * nhieu))} mức phân biệt được)` : ''}`);
  dat('o-hall-tt', s.hall < 150 ? nhanTT(false, 'gần 0 V: chưa nối?') : s.hall > 3000 ? nhanTT(false, 'gần 3,3 V: chạm VCC?') : !(h.daNha && h.daBop) ? nhanTT(null, 'chưa căn') : nhanTT(hanhTrinh >= 300 ? true : hanhTrinh >= 100 ? null : false, hanhTrinh >= 300 ? 'tốt' : hanhTrinh >= 100 ? 'hơi hẹp' : 'quá hẹp'));
  const c = cal.cham;
  for (const [k, m, ten] of [['th-a', vao.mA, 'A'], ['th-b', vao.mB, 'B']]) {
    const e = $(k); if (e) { e.firstElementChild.style.width = clamp(m, 0, 1) * 100 + '%'; e.firstElementChild.style.background = vao[ten] ? '#6fdc8c' : '#38d6c4'; }
  }
  dat('o-cham', `A: thô ${s.ta} (nền ${Math.round(c.nenA)}, chạm ${Math.round(c.chamA)}) ${vao.A ? '[CHẠM]' : ''}\nB: thô ${s.tb} (nền ${Math.round(c.nenB)}, chạm ${Math.round(c.chamB)}) ${vao.B ? '[CHẠM]' : ''}`);
  const okA = Math.abs(c.chamA - c.nenA) > 5 * Math.max(1, bo.ta.lc), okB = Math.abs(c.chamB - c.nenB) > 5 * Math.max(1, bo.tb.lc);
  dat('o-cham-tt', !(c.chamA && c.chamB) ? nhanTT(null, 'chưa căn') : nhanTT(okA && okB ? true : null, okA && okB ? 'đã căn' : 'chênh lệch nhỏ'));
}

// =====================================================================================
// 9. Vòng lặp
// =====================================================================================
const thamSo = new URLSearchParams(location.search);
if (thamSo.has('gia_lap')) { giaLap = true; $('gia-lap').checked = true; }
chonThe(TRAM[thamSo.get('the')] ? thamSo.get('the') : 'can');
datCam(TRAM[theHienTai].cam, true);
let tTruoc = performance.now(), tBang = 0;
function khungHinh(t) {
  const dt = Math.min(0.05, (t - tTruoc) / 1000); tTruoc = t;
  if (t - lanCuoi >= 1000) { tanSo = demDong; demDong = 0; lanCuoi = t; }
  capNhatVao();
  for (const k in TRAM) TRAM[k].capNhat(dt);
  capNhatCam(dt);
  if (t - tBang > 100) { tBang = t; capNhatBang(); $('hud').innerHTML = TRAM[theHienTai].hud(); }
  if (t > hetThongBao) $('thong-bao').style.opacity = 0;
  renderer.render(scene, cam);
  requestAnimationFrame(khungHinh);
}
requestAnimationFrame(khungHinh);
window.__vera = { vao, s, cal, TRAM, gl };
