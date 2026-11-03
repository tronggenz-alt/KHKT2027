// Các cảnh 3D của trang: tòa nhà hologram, mặt bằng bấm được, tay cầm VERA Drop, phân tử, ADN
import * as THREE from 'three';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';

const yenTinh = matchMedia('(prefers-reduced-motion: reduce)').matches;
const dienThoai = () => innerWidth < 961;
const TANG = window.VERA_TANG;
const Y_TANG = { 1: 0, 2: 3.8 }, CAO = 3.2;
const MAU = { 1: 0x5b9dff, 2: 0x2ee6b4 };
const kep = (x, a = 0, b = 1) => Math.max(a, Math.min(b, x));
const em = t => t * t * (3 - 2 * t);

// ---------------------------------------------------------------- vòng lặp chung
const canh = [];
let tCu = performance.now();
function lap(now) {
  const dt = Math.min(.05, (now - tCu) / 1000); tCu = now;
  for (const c of canh) if (c.hien) c.ve(now / 1000, dt);
  requestAnimationFrame(lap);
}

function dangKy(canvas, khoiTao) {
  const c = { hien: false, ve: () => {} };
  let daTao = false;
  const io = new IntersectionObserver(d => {
    const vao = d[0].isIntersecting;
    if (vao && !daTao) { daTao = true; khoiTao(c); }
    c.hien = vao;
    if (vao && yenTinh && c.ve) requestAnimationFrame(t => c.ve(t / 1000, 0));
  }, { rootMargin: '300px' });
  io.observe(canvas);
  canh.push(c);
}

function taoRenderer(canvas, { alpha = true, dpr = 1.75 } = {}) {
  const r = new THREE.WebGLRenderer({ canvas, antialias: true, alpha, powerPreference: 'high-performance' });
  r.setPixelRatio(Math.min(devicePixelRatio || 1, dienThoai() ? 1.5 : dpr));
  return r;
}

function theoKichThuoc(canvas, cb) {
  const ro = new ResizeObserver(() => { const w = canvas.clientWidth, h = canvas.clientHeight; if (w && h) cb(w, h); });
  ro.observe(canvas);
}

// Kéo để xoay, không cản cuộn dọc trên điện thoại (canvas có touch-action: pan-y)
function keoQuay(canvas, cb) {
  let keo = false, x = 0, tong = 0;
  canvas.addEventListener('pointerdown', e => { keo = true; x = e.clientX; tong = 0; canvas.setPointerCapture(e.pointerId); canvas.classList.add('dang-keo'); });
  canvas.addEventListener('pointermove', e => { if (!keo) return; const d = e.clientX - x; x = e.clientX; tong += Math.abs(d); cb(d * .008); });
  const het = e => { keo = false; canvas.classList.remove('dang-keo'); canvas.vuaKeo = tong > 6; };
  canvas.addEventListener('pointerup', het);
  canvas.addEventListener('pointercancel', het);
}

function diemTron() {
  const c = document.createElement('canvas'); c.width = c.height = 64;
  const g = c.getContext('2d'), r = g.createRadialGradient(32, 32, 0, 32, 32, 32);
  r.addColorStop(0, 'rgba(255,255,255,1)'); r.addColorStop(.3, 'rgba(255,255,255,.7)'); r.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = r; g.fillRect(0, 0, 64, 64);
  return new THREE.CanvasTexture(c);
}

// ---------------------------------------------------------------- tòa nhà từ mặt bằng thật
const W = (x, z) => [x - 15, z - 3];

function themHop(a, x, y, z, w, h, d) {
  const [x0, x1, y0, y1, z0, z1] = [x - w / 2, x + w / 2, y, y + h, z - d / 2, z + d / 2];
  const v = [[x0, y0, z0], [x1, y0, z0], [x1, y0, z1], [x0, y0, z1], [x0, y1, z0], [x1, y1, z0], [x1, y1, z1], [x0, y1, z1]];
  for (const [p, q] of [[0, 1], [1, 2], [2, 3], [3, 0], [4, 5], [5, 6], [6, 7], [7, 4], [0, 4], [1, 5], [2, 6], [3, 7]]) a.push(...v[p], ...v[q]);
}

// Đồ đạc gợi hình theo từng phòng (x, z theo mét mặt bằng, y từ mặt sàn)
function doDac(k, a, y) {
  const hop = (x, z, w, h, d, day = 0) => { const [wx, wz] = W(x, z); themHop(a, wx, y + day, wz, w, h, d); };
  if (k === 'lab') {
    for (const x of [4, 9, 14]) for (const z of [3.4, 7.2, 11]) { hop(x, z, 2.6, .9, 1.1); hop(x, z, 1.1, .5, .12, .9); }
    hop(1.2, 7.5, .9, .9, 3.4); hop(.25, 7.5, .1, 1.4, 5, 1);
    for (const x of [15.2, 16.8, 18.4]) hop(x, 14.4, 1.4, 2.3, .9);
    hop(18.2, 10.3, 3, 2.4, 2.5);
  } else if (k === 'kho') {
    for (const x of [21.6, 23.5, 25.4]) for (let l = 0; l < 4; l++) hop(x, 7.5, .55, .52, 11, l * .56);
  } else if (k === 'may') {
    for (let i = 0; i < 6; i++) hop(1.3 + i * 2.1, -8.3, 1.4, 1.4, .9);
    for (const z of [-4.2, -6]) hop(.8, z, .9, 1.5, 1.1);
    hop(6.7, -5.4, 3.6, .9, 1.2);
  } else if (k === 'robot') {
    hop(18.5, -6.5, 3.6, .75, 1.3); hop(23.5, -7.6, 1, 1.05, 1); hop(23.5, -7.6, .3, .9, .3, 1.05);
    hop(25.8, -4, .9, 2, .9); hop(15, -8.4, 2, 1, .7);
  } else if (k === 'sinh') {
    for (const x of [4.5, 9.5, 14.5]) for (const z of [4, 9.6]) { hop(x, z, 3.2, .9, 1.2); for (const dx of [-1, 0, 1]) hop(x + dx, z, .22, .42, .3, .9); }
    hop(1.1, 7.5, .9, .9, 3.2); hop(10, 14.5, 7, .9, .7); hop(.25, 7.5, .1, 1.4, 5, 1);
  } else if (k === 'visinh') {
    for (const x of [21, 22.6]) hop(x, 14.3, 1.4, 2.2, .9);
    hop(22.5, 11, 3, .9, 1.1); hop(26.3, 9.5, .7, 1.3, .7); hop(26.3, 12, .7, 1.9, .9);
  } else if (k === 'khosp') {
    for (const z of [1, 3, 5]) hop(26.3, z, .8, 2, 1.3);
    hop(22.5, 3.6, 3, .9, 1); hop(20.5, .9, .6, 2, 1.1);
  } else if (k === 'phantu') {
    hop(.7, -6.1, .9, 1.4, 1.5); hop(6.6, -6.3, 3.4, .95, 1.2); hop(2.7, -4.05, 3.2, .85, .6); hop(10.9, -8.5, 4, .9, .7);
  } else if (k === 'kinh') {
    for (const z of [-5, -7.2]) for (let x = 15; x < 26.5; x += 1.5) { hop(x, z, .5, .75, .5); hop(x, z, .22, .55, .22, .75); }
    for (let x = 13.5; x <= 27; x += 2.7) { const [wx, wz0] = W(x, -9), [, wz1] = W(x, -3.6); a.push(wx, y + CAO, wz0, wx, y + CAO + .9, (wz0 + wz1) / 2, wx, y + CAO + .9, (wz0 + wz1) / 2, wx, y + CAO, wz1); }
  } else if (k === 'thang') {
    hop(28.1, -.9, 2, CAO, 2); for (let i = 0; i < 7; i++) hop(29.2, .4 + i * .7, 1.1, .28 + i * .22, .7);
  }
}

function dungToa({ fillManh = .05, tuongTac = false } = {}) {
  const goc = new THREE.Group(), tang = {};
  for (const t of [1, 2]) {
    const g = new THREE.Group(); g.position.y = Y_TANG[t]; goc.add(g);
    const chiTiet = [], matDo = new THREE.LineBasicMaterial({ color: MAU[t], transparent: true, opacity: .32, blending: THREE.AdditiveBlending, depthWrite: false });
    const phong = TANG[t].map((p, i) => {
      const [x0, z0, x1, z1] = p.r, w = x1 - x0 - .08, d = z1 - z0 - .08, [cx, cz] = W((x0 + x1) / 2, (z0 + z1) / 2);
      const geo = new THREE.BoxGeometry(w, CAO, d);
      const matL = new THREE.LineBasicMaterial({ color: MAU[t], transparent: true, opacity: .8, blending: THREE.AdditiveBlending, depthWrite: false });
      const matF = new THREE.MeshBasicMaterial({ color: MAU[t], transparent: true, opacity: fillManh, side: THREE.DoubleSide, blending: THREE.AdditiveBlending, depthWrite: false });
      const khung = new THREE.LineSegments(new THREE.EdgesGeometry(geo), matL), khoi = new THREE.Mesh(geo, matF);
      khung.position.set(cx, CAO / 2, cz); khoi.position.copy(khung.position);
      g.add(khung, khoi);
      khoi.userData = { t, i };
      doDac(p.k, chiTiet, 0);
      return { khoi, khung, matL, matF };
    });
    const hd = new THREE.BufferGeometry();
    hd.setAttribute('position', new THREE.Float32BufferAttribute(chiTiet, 3));
    g.add(new THREE.LineSegments(hd, matDo));
    tang[t] = { g, phong, matDo };
  }
  return { goc, tang, tuongTac };
}

// ---------------------------------------------------------------- mở đầu: tòa nhà hologram
function canhMoDau(c) {
  const canvas = document.getElementById('c-toa'), khu = document.getElementById('mo-dau');
  const r = taoRenderer(canvas, { alpha: false, dpr: 1.5 });
  r.setClearColor(0x000000, 1);
  const scene = new THREE.Scene();
  const cam = new THREE.PerspectiveCamera(36, 1, .1, 200);
  const toa = dungToa({ fillManh: .012 }); scene.add(toa.goc);

  // Nền lưới và vòng radar
  const luoi = new THREE.GridHelper(70, 70, 0x25507f, 0x12283f);
  luoi.material.transparent = true; luoi.material.opacity = .5; luoi.position.y = -.02; scene.add(luoi);
  const vong = new THREE.Mesh(new THREE.RingGeometry(.993, 1, 128), new THREE.MeshBasicMaterial({ color: 0x38e8ff, transparent: true, opacity: .6, side: THREE.DoubleSide, blending: THREE.AdditiveBlending, depthWrite: false }));
  vong.rotation.x = -Math.PI / 2; vong.position.y = .01; scene.add(vong);

  // Mặt quét đi từ móng lên mái
  const quet = new THREE.Mesh(new THREE.PlaneGeometry(32, 26), new THREE.MeshBasicMaterial({ color: 0x38e8ff, transparent: true, opacity: .035, side: THREE.DoubleSide, blending: THREE.AdditiveBlending, depthWrite: false }));
  quet.rotation.x = -Math.PI / 2; scene.add(quet);
  const quetVien = new THREE.LineSegments(new THREE.EdgesGeometry(new THREE.PlaneGeometry(32, 26)), new THREE.LineBasicMaterial({ color: 0x9df3ff, transparent: true, opacity: .8, blending: THREE.AdditiveBlending }));
  quetVien.rotation.x = -Math.PI / 2; scene.add(quetVien);

  // Bụi sáng và các chấm dữ liệu chạy dọc hành lang, lõi thang
  const tex = diemTron(), N = dienThoai() ? 160 : 360, bui = new Float32Array(N * 3), toc = new Float32Array(N);
  for (let i = 0; i < N; i++) { bui[i * 3] = (Math.random() - .5) * 44; bui[i * 3 + 1] = Math.random() * 16; bui[i * 3 + 2] = (Math.random() - .5) * 36; toc[i] = .05 + Math.random() * .22; }
  const geoBui = new THREE.BufferGeometry(); geoBui.setAttribute('position', new THREE.BufferAttribute(bui, 3));
  scene.add(new THREE.Points(geoBui, new THREE.PointsMaterial({ map: tex, color: 0x8fdcff, size: .16, transparent: true, opacity: .75, blending: THREE.AdditiveBlending, depthWrite: false })));

  const [ax, az] = W(0, -1.1), [bx, bz] = W(28.5, -1.1), [cx, cz] = W(0, -1.8);
  const duong = [[ax, .2, az], [bx, .2, bz], [bx, 3.6 + .4, bz], [cx, 4, cz]];
  const M = 42, xung = new Float32Array(M * 3), geoX = new THREE.BufferGeometry();
  geoX.setAttribute('position', new THREE.BufferAttribute(xung, 3));
  scene.add(new THREE.Points(geoX, new THREE.PointsMaterial({ map: tex, color: 0xffffff, size: .34, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false })));
  function diemTrenDuong(u, ra, tach) {
    const n = duong.length - 1, s = u * n, i = Math.min(n - 1, Math.floor(s)), f = s - i, p = duong[i], q = duong[i + 1];
    ra[0] = p[0] + (q[0] - p[0]) * f; ra[1] = p[1] + (q[1] - p[1]) * f; ra[2] = p[2] + (q[2] - p[2]) * f;
    if (ra[1] > 3.7) ra[1] += tach; else if (i === 1) ra[1] += tach * f;
  }

  let comp = null, bloom = null;
  function damBloom() {
    if (dienThoai()) return;
    comp = new EffectComposer(r);
    comp.addPass(new RenderPass(scene, cam));
    bloom = new UnrealBloomPass(new THREE.Vector2(256, 256), .3, .4, .12);
    comp.addPass(bloom);
    comp.addPass(new OutputPass());
  }
  damBloom();
  theoKichThuoc(canvas, (w, h) => {
    r.setSize(w, h, false);
    cam.aspect = w / h;
    if (dienThoai()) cam.setViewOffset(w, h, 0, h * .24, w, h); else cam.setViewOffset(w, h, -w * .23, h * .01, w, h);
    cam.updateProjectionMatrix();
    if (comp) { comp.setSize(w, h); bloom.setSize(w / 2, h / 2); }
  });

  // Nhãn hai tầng bám theo mô hình
  const nhan = [document.getElementById('nhan-t1'), document.getElementById('nhan-t2')];
  const P = new THREE.Vector3(), tmp = [0, 0, 0];
  const px = { x: 0, y: 0, cx: 0, cy: 0 };
  addEventListener('pointermove', e => { px.x = e.clientX / innerWidth - .5; px.y = e.clientY / innerHeight - .5; }, { passive: true });

  let dem = 0, tong = 0, daDo = false;
  c.ve = (t, dt) => {
    if (!daDo && dt > 0) {
      dem++; if (dem > 12) tong += dt;
      if (dem === 90) { daDo = true; const tb = tong / 78; if (tb > .036) { comp = null; r.setPixelRatio(tb > .08 ? .7 : 1); r.setSize(canvas.clientWidth, canvas.clientHeight, false); } window.VERA_KHUNG_MS = Math.round(tb * 1000); }
    }
    const rk = khu.getBoundingClientRect(), tb = khu.offsetHeight - innerHeight;
    const k = tb > 10 ? kep(-rk.top / tb) : 0, ke = em(kep(k * 1.25));
    px.cx += (px.x - px.cx) * .05; px.cy += (px.y - px.cy) * .05;
    const tach = ke * 6.2;
    toa.tang[2].g.position.y = Y_TANG[2] + tach;
    const yaw = (yenTinh ? .7 : t * .09 + .55) + ke * .9 + px.cx * .5, dai = (dienThoai() ? 96 : 66) - ke * 2;
    const cao = 22 - ke * 8 + px.cy * -5;
    cam.position.set(Math.sin(yaw) * dai, cao + tach * .4, Math.cos(yaw) * dai);
    cam.lookAt(0, 3.6 + tach * .45, 0);

    const v = (t * .12) % 1;
    vong.scale.setScalar(6 + v * 28); vong.material.opacity = .7 * (1 - v);
    const yq = ((t * .16) % 1) * (CAO + Y_TANG[2] + tach + 1.4);
    quet.position.y = quetVien.position.y = yq;
    if (!yenTinh) {
      for (let i = 0; i < N; i++) { bui[i * 3 + 1] += toc[i] * dt; if (bui[i * 3 + 1] > 16) bui[i * 3 + 1] = 0; }
      geoBui.attributes.position.needsUpdate = true;
    }
    for (let i = 0; i < M; i++) {
      diemTrenDuong(((t * .05 + i / M) % 1), tmp, tach);
      xung[i * 3] = tmp[0]; xung[i * 3 + 1] = tmp[1]; xung[i * 3 + 2] = tmp[2];
    }
    geoX.attributes.position.needsUpdate = true;

    // Nhãn chỉ hiện khi hai tầng đã tách
    nhan.forEach((n, i) => {
      if (!n) return;
      const y = i === 0 ? CAO + .3 : Y_TANG[2] + tach + CAO + .3;
      P.set(0, y, -9).project(cam);
      const w = canvas.clientWidth, h = canvas.clientHeight;
      n.style.transform = `translate(${(P.x * .5 + .5) * w}px,${(-P.y * .5 + .5) * h}px) translate(-50%,-100%)`;
      n.style.opacity = kep((k - .28) * 4) * ((-P.y * .5 + .5) * h > 76 ? 1 : 0);
    });
    if (comp) comp.render(dt); else r.render(scene, cam);
  };
}

// ---------------------------------------------------------------- mặt bằng 3D bấm được
function canhMatBang(c) {
  const canvas = document.getElementById('c-plan');
  const r = taoRenderer(canvas);
  const scene = new THREE.Scene(), cam = new THREE.PerspectiveCamera(34, 1, .1, 200);
  const toa = dungToa({ fillManh: .05 }); scene.add(toa.goc);
  const luoi = new THREE.GridHelper(60, 60, 0x1d3f63, 0x0f2133); luoi.material.transparent = true; luoi.material.opacity = .5; scene.add(luoi);
  theoKichThuoc(canvas, (w, h) => { r.setSize(w, h, false); cam.aspect = w / h; cam.updateProjectionMatrix(); });

  let tangChon = 1, phongChon = 0, yaw = .55, yawDich = .55, mo = 1;
  const trong = { 1: 1, 2: 0 }, ray = new THREE.Raycaster(), chuot = new THREE.Vector2();
  keoQuay(canvas, d => { yawDich -= d; });

  function trungPhong(e) {
    const b = canvas.getBoundingClientRect();
    chuot.set((e.clientX - b.left) / b.width * 2 - 1, -(e.clientY - b.top) / b.height * 2 + 1);
    ray.setFromCamera(chuot, cam);
    const hit = ray.intersectObjects(toa.tang[tangChon].phong.map(p => p.khoi));
    return hit.length ? hit[0].object.userData : null;
  }
  canvas.addEventListener('click', e => {
    if (canvas.vuaKeo) return;
    const u = trungPhong(e);
    if (u) dispatchEvent(new CustomEvent('vera:chon', { detail: u }));
  });
  canvas.addEventListener('pointermove', e => { if (!canvas.classList.contains('dang-keo')) canvas.style.cursor = trungPhong(e) ? 'pointer' : 'grab'; });
  addEventListener('vera:da-chon', e => { tangChon = e.detail.t; phongChon = e.detail.i; });
  if (window.VERA_PHONG) { tangChon = window.VERA_PHONG.t; phongChon = window.VERA_PHONG.i; }

  c.ve = (t, dt) => {
    if (!yenTinh && !canvas.classList.contains('dang-keo')) yawDich += dt * .05;
    yaw += (yawDich - yaw) * .08;
    const dai = dienThoai() ? 66 : 54;
    cam.position.set(Math.sin(yaw) * dai, 30, Math.cos(yaw) * dai);
    cam.lookAt(0, tangChon === 1 ? 1.4 : 5.4, 0);
    for (const n of [1, 2]) {
      const dich = n === tangChon ? 1 : 0;
      trong[n] += (dich - trong[n]) * .09;
      const q = toa.tang[n], a = trong[n];
      const y = n === 2 ? Y_TANG[2] + (1 - a) * 9 : Y_TANG[1] - (1 - a) * 2;
      q.g.position.y = y;
      q.matDo.opacity = .06 + a * .5;
      q.phong.forEach((p, i) => {
        const chon = n === tangChon && i === phongChon;
        p.matL.opacity = .08 + a * (chon ? 1 : .72);
        const dichF = chon ? .34 + Math.sin(t * 3) * .05 : .012 + a * .035;
        p.matF.opacity += (dichF - p.matF.opacity) * .14;
      });
          }
    r.render(scene, cam);
  };
}

// ---------------------------------------------------------------- tay cầm VERA Drop
function canhTayCam(c) {
  const canvas = document.getElementById('c-drop'), khu = document.getElementById('tay-cam');
  const r = taoRenderer(canvas, { dpr: 1.75 });
  r.toneMapping = THREE.ACESFilmicToneMapping; r.toneMappingExposure = 1.05;
  const scene = new THREE.Scene(), cam = new THREE.PerspectiveCamera(30, 1, .01, 50);
  const pm = new THREE.PMREMGenerator(r);
  scene.environment = pm.fromScene(new RoomEnvironment(), .04).texture;
  scene.environmentIntensity = .62;
  const den1 = new THREE.DirectionalLight(0xffffff, 2); den1.position.set(3, 5, 4); scene.add(den1);
  const den2 = new THREE.DirectionalLight(0x38e8ff, 2.4); den2.position.set(-5, 1.5, -3); scene.add(den2);
  const den3 = new THREE.DirectionalLight(0x8a7bff, 1.6); den3.position.set(4, -2, -4); scene.add(den3);

  const truc = new THREE.Group(); scene.add(truc);
  const goc = new THREE.Group(); goc.rotation.x = -Math.PI / 2; goc.scale.setScalar(.01); goc.position.y = -.52; truc.add(goc);

  // Bệ vòng tròn dưới tay cầm
  const be = new THREE.Group(); be.position.y = -.68; scene.add(be);
  for (const [rd, op] of [[.62, .7], [.86, .32], [1.14, .16]]) {
    const v = new THREE.Mesh(new THREE.RingGeometry(rd - .005, rd, 96), new THREE.MeshBasicMaterial({ color: 0x38e8ff, transparent: true, opacity: op, side: THREE.DoubleSide, blending: THREE.AdditiveBlending, depthWrite: false }));
    v.rotation.x = -Math.PI / 2; be.add(v);
  }
  const hao = new THREE.Sprite(new THREE.SpriteMaterial({ map: diemTron(), color: 0x18c8e6, transparent: true, opacity: 0, blending: THREE.AdditiveBlending, depthWrite: false, depthTest: false }));
  hao.scale.setScalar(1.3); truc.add(hao);

  const VL = {
    than_phai: { color: '#eef2f4', roughness: .36, clearcoat: .7, clearcoatRoughness: .35 },
    than_trai: { color: '#eef2f4', roughness: .36, clearcoat: .7, clearcoatRoughness: .35 },
    thanh: { color: '#15191d', roughness: .32, metalness: .25, clearcoat: .5 },
    chu: { color: '#15191d', roughness: .6 },
    vit: { color: '#aab4ba', roughness: .3, metalness: .9 },
    nap: { color: '#101418', roughness: .22, metalness: .2, clearcoat: 1, clearcoatRoughness: .12 },
    co: { color: '#15191d', roughness: .32, metalness: .25, clearcoat: .5 },
    chen: { color: '#3a444b', roughness: .42, metalness: .65 },
    ky023: { color: '#15191d', roughness: .45, metalness: .2 },
    vong: { color: '#02242e', roughness: .3, emissive: '#00b4e0', emissiveIntensity: 1.6 }
  };
  const vat = {}, ds = [], VO = ['than_phai', 'than_trai', 'chu'];
  let xong = false;
  Promise.all([fetch('mo_hinh/drop.json').then(x => x.json()), fetch('mo_hinh/drop.bin').then(x => x.arrayBuffer())]).then(([tt, buf]) => {
    for (const p of tt) {
      const g = new THREE.BufferGeometry();
      const ib = new THREE.InterleavedBuffer(new Int16Array(buf, p.v[0], p.v[1] * 5), 5);
      const nb = new THREE.InterleavedBuffer(new Int8Array(buf, p.v[0], p.v[1] * 10), 10);
      g.setAttribute('position', new THREE.InterleavedBufferAttribute(ib, 3, 0, false));
      g.setAttribute('normal', new THREE.InterleavedBufferAttribute(nb, 3, 6, true));
      const Kieu = p.i[2] === 4 ? Uint32Array : Uint16Array;
      g.setIndex(new THREE.BufferAttribute(new Kieu(buf, p.i[0], p.i[1]), 1));
      g.computeBoundingBox();
      const mat = new THREE.MeshPhysicalMaterial(VL[p.ma] || { color: p.mau, roughness: .5, metalness: .25 });
      const m = new THREE.Mesh(g, mat);
      m.scale.setScalar(1 / 256);
      m.renderOrder = VO.includes(p.ma) ? 2 : 1;
      goc.add(m);
      const o = { ma: p.ma, m, mat, tach: new THREE.Vector3(...p.tach), tam: g.boundingBox.getCenter(new THREE.Vector3()).divideScalar(256) };
      vat[p.ma] = o; ds.push(o);
    }
    xong = true;
    document.getElementById('tay-cam').classList.add('da-tai');
  }).catch(() => document.getElementById('tay-cam').classList.add('loi-tai'));

  theoKichThuoc(canvas, (w, h) => { r.setSize(w, h, false); cam.aspect = w / h; cam.updateProjectionMatrix(); });
  let yawKeo = 0; keoQuay(canvas, d => { yawKeo += d; });

  const buoc = [...document.querySelectorAll('.buoc-tc')], thanh = document.getElementById('tc-tien-do');
  const CHAM = [
    ['ky023', 'Joystick', 0, 0, 0], ['co', 'Cò bóp cảm biến Hall', 0, 0, 0], ['esp32', 'ESP32 không dây', 0, 0, 0],
    ['bmi160', 'Cảm biến chuyển động', 0, 0, 0], ['pin', 'Pin LiPo', 0, 0, 0], ['tp4056', 'Sạc USB-C', 0, 0, 0]
  ].map(a => {
    const hop = document.getElementById('tc-cham');
    const dot = document.createElement('i'); dot.className = 'cham-dot';
    const nh = document.createElement('span'); nh.className = 'cham-nhan'; nh.textContent = a[1];
    const ln = document.createElementNS('http://www.w3.org/2000/svg', 'line');
    document.getElementById('tc-duong').appendChild(ln);
    hop.append(dot, nh);
    return { ma: a[0], dot, nh, ln, x: 0, y: 0 };
  });
  const P = new THREE.Vector3();
  let bHien = -1;

  c.ve = (t, dt) => {
    const b = khu.getBoundingClientRect(), tb = khu.offsetHeight - innerHeight;
    const p = tb > 10 ? kep(-b.top / tb) : 0;
    if (thanh) thanh.style.transform = `scaleX(${p})`;
    const i = p < .24 ? 0 : p < .52 ? 1 : p < .78 ? 2 : 3;
    if (i !== bHien) { bHien = i; buoc.forEach((n, j) => n.classList.toggle('hien', j === i)); khu.dataset.buoc = i; }

    const ke = em(kep((p - .2) / .2)) * (1 - em(kep((p - .74) / .16)));
    const mo = em(kep((p - .46) / .12)) * (1 - em(kep((p - .72) / .1)));
    const xoay = (yenTinh ? .5 : t * .22) + p * Math.PI * 2.2 + yawKeo;
    truc.rotation.y = xoay;
    truc.position.y = (yenTinh ? 0 : Math.sin(t * .9) * .018);
    const hep = dienThoai();
    const gan = (hep ? 3.9 : 3.15) + ke * (hep ? 2.9 : 2.3) - em(kep((p - .82) / .16)) * .75;
    cam.position.set(0, .34 + em(kep((p - .82) / .16)) * .28, gan);
    cam.lookAt(0, (hep ? -.32 : 0) + ke * .3, 0);
    if (!hep) { const w = canvas.clientWidth, h = canvas.clientHeight; cam.setViewOffset(w, h, -w * .17, 0, w, h); }
    else cam.clearViewOffset();
    be.rotation.y = -t * .15;

    if (xong) {
      for (const o of ds) {
        o.m.position.copy(o.tach).multiplyScalar(ke);
        if (VO.includes(o.ma)) {
          const a = 1 - mo * .9;
          o.mat.transparent = a < .995; o.mat.depthWrite = a > .6; o.mat.opacity = a;
        }
      }
      const v = vat.vong;
      v.mat.emissiveIntensity = 1.4 + Math.sin(t * 2.2) * .35;
      goc.updateMatrixWorld();
      P.copy(v.m.position).add(v.tam).applyMatrix4(goc.matrixWorld);
      hao.position.copy(P).sub(truc.position); hao.position.applyAxisAngle(new THREE.Vector3(0, 1, 0), -truc.rotation.y);
      hao.material.opacity = .22 + Math.sin(t * 2.2) * .05;
      const w = canvas.clientWidth, h = canvas.clientHeight;
      document.getElementById('tc-cham').classList.toggle('hien', mo > .55);
      document.getElementById('tc-duong').classList.toggle('hien', mo > .55);
      if (mo > .3) {
        for (const q of CHAM) {
          const o = vat[q.ma]; if (!o) continue;
          P.copy(o.m.position).add(o.tam).applyMatrix4(goc.matrixWorld).project(cam);
          q.x = (P.x * .5 + .5) * w; q.y = (-P.y * .5 + .5) * h;
        }
        // Cột nhãn bên phải: xếp theo độ cao để đường dẫn không cắt nhau, giữ khoảng cách tối thiểu
        const thu = [...CHAM].sort((a, b) => a.y - b.y), cach = 38, cot = hep ? w - 150 : Math.min(w - 210, w * .84);
        let yN = 90;
        for (const q of thu) { q.ly = Math.max(q.y, yN); yN = q.ly + cach; }
        const tran = thu[thu.length - 1].ly - (h - 90);
        if (tran > 0) { let dich = tran; for (let n = thu.length - 1; n >= 0; n--) { thu[n].ly -= dich; if (n > 0) dich = Math.max(0, thu[n - 1].ly + cach - thu[n].ly); } }
        for (const q of thu) {
          q.dot.style.transform = `translate(${q.x}px,${q.y}px)`;
          q.nh.style.transform = `translate(${cot}px,${q.ly}px)`;
          q.ln.setAttribute('x1', q.x); q.ln.setAttribute('y1', q.y); q.ln.setAttribute('x2', cot); q.ln.setAttribute('y2', q.ly);
        }
      }
    }
    r.render(scene, cam);
  };
}

// ---------------------------------------------------------------- phân tử và chuỗi ADN nhỏ ở đầu mỗi khu
function canhNho(id, dung) {
  const canvas = document.getElementById(id);
  if (!canvas) return;
  dangKy(canvas, c => {
    const r = taoRenderer(canvas, { dpr: 1.5 });
    r.toneMapping = THREE.ACESFilmicToneMapping;
    const scene = new THREE.Scene(), cam = new THREE.PerspectiveCamera(30, 1, .1, 60); cam.position.z = 11;
    const pm = new THREE.PMREMGenerator(r);
    scene.environment = pm.fromScene(new RoomEnvironment(), .04).texture;
    scene.environmentIntensity = .85;
    const den = new THREE.DirectionalLight(0xffffff, 2); den.position.set(3, 4, 5); scene.add(den);
    const nhom = new THREE.Group(); scene.add(nhom);
    const ve = dung(nhom);
    theoKichThuoc(canvas, (w, h) => { r.setSize(w, h, false); cam.aspect = w / h; cam.updateProjectionMatrix(); });
    c.ve = (t) => { ve(yenTinh ? 1 : t); r.render(scene, cam); };
  });
}

const Y = new THREE.Vector3(0, 1, 0);
function thanhNoi(nhom, a, b, ban, mat) {
  const d = b.clone().sub(a), m = new THREE.Mesh(new THREE.CylinderGeometry(ban, ban, d.length(), 12), mat);
  m.position.copy(a).addScaledVector(d, .5); m.quaternion.setFromUnitVectors(Y, d.normalize()); nhom.add(m);
}

function dungPhanTu(nhom) {
  const C = new THREE.MeshPhysicalMaterial({ color: 0x4d8dff, roughness: .25, clearcoat: 1 });
  const H = new THREE.MeshPhysicalMaterial({ color: 0xdfe8f5, roughness: .3, clearcoat: 1 });
  const L = new THREE.MeshStandardMaterial({ color: 0x8ab0e8, roughness: .35, metalness: .4 });
  const qC = new THREE.SphereGeometry(.56, 32, 24), qH = new THREE.SphereGeometry(.32, 24, 18);
  const pt = [];
  for (let i = 0; i < 6; i++) {
    const a = i * Math.PI / 3, p = new THREE.Vector3(Math.cos(a) * 2, Math.sin(a) * 2, 0), h = new THREE.Vector3(Math.cos(a) * 3.5, Math.sin(a) * 3.5, 0);
    const mc = new THREE.Mesh(qC, C); mc.position.copy(p); nhom.add(mc);
    const mh = new THREE.Mesh(qH, H); mh.position.copy(h); nhom.add(mh);
    thanhNoi(nhom, p, h, .1, L); pt.push(p);
  }
  for (let i = 0; i < 6; i++) {
    const a = pt[i], b = pt[(i + 1) % 6];
    if (i % 2 === 0) {
      const n = b.clone().sub(a).cross(new THREE.Vector3(0, 0, 1)).normalize().multiplyScalar(.14);
      thanhNoi(nhom, a.clone().add(n), b.clone().add(n), .08, L); thanhNoi(nhom, a.clone().sub(n), b.clone().sub(n), .08, L);
    } else thanhNoi(nhom, a, b, .1, L);
  }
  return t => { nhom.rotation.set(.6 + Math.sin(t * .4) * .3, t * .45, Math.sin(t * .3) * .2); };
}

function dungAdn(nhom) {
  const A = new THREE.MeshPhysicalMaterial({ color: 0x2ee6b4, roughness: .25, clearcoat: 1 });
  const B = new THREE.MeshPhysicalMaterial({ color: 0x4d8dff, roughness: .25, clearcoat: 1 });
  const L = new THREE.MeshStandardMaterial({ color: 0xcfe6e0, roughness: .4, metalness: .3 });
  const q = new THREE.SphereGeometry(.26, 20, 14), xoan = new THREE.Group();
  const n = 26, ban = 1.45, buoc = .37;
  for (let i = 0; i < n; i++) {
    const a = i * .52, y = (i - n / 2) * buoc;
    const p1 = new THREE.Vector3(Math.cos(a) * ban, y, Math.sin(a) * ban), p2 = new THREE.Vector3(-Math.cos(a) * ban, y, -Math.sin(a) * ban);
    const m1 = new THREE.Mesh(q, A); m1.position.copy(p1); const m2 = new THREE.Mesh(q, B); m2.position.copy(p2);
    xoan.add(m1, m2);
    if (i % 2 === 0) thanhNoi(xoan, p1, p2, .055, L);
  }
  xoan.rotation.z = .42; nhom.add(xoan);
  return t => { xoan.rotation.y = t * .5; };
}

// ---------------------------------------------------------------- khởi động
try {
  const thu = document.createElement('canvas').getContext('webgl2');
  if (!thu) throw new Error('webgl');
  dangKy(document.getElementById('c-toa'), canhMoDau);
  dangKy(document.getElementById('c-plan'), canhMatBang);
  dangKy(document.getElementById('c-drop'), canhTayCam);
  canhNho('c-phan-tu', dungPhanTu);
  canhNho('c-adn', dungAdn);
  document.documentElement.classList.add('co-3d');
  requestAnimationFrame(lap);
} catch (e) {
  document.documentElement.classList.add('khong-3d');
}
