// Phòng thí nghiệm Hoá ảo – RENDERER BẢN WEB 3D (Three.js r160, dựng procedural – không tải model).
// Chỉ VẼ theo trạng thái từ mo-phong.js và GỬI thao tác người dùng về qua onThaoTac(lenh, thamSo). Không tính hoá học ở đây.
// Giao diện chung với cau-unity.js: mo(id, tn, s) · capNhat(s) · hieuUng(e) · doiGiaoDien() · datChatLuong(q) · camera(lenh) · huy().
import * as THREE from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
import { RoomEnvironment } from "three/addons/environments/RoomEnvironment.js";
import { CSS2DRenderer, CSS2DObject } from "three/addons/renderers/CSS2DRenderer.js";
import { RoundedBoxGeometry } from "three/addons/geometries/RoundedBoxGeometry.js";
import { PhongMo } from "./phong-mo.js";
import { NhanVat } from "./nhan-vat.js";

const gsap = window.gsap;
const V3 = (x, y, z) => new THREE.Vector3(x, y, z);
const V2 = (x, y) => new THREE.Vector2(x, y);
const rgbMau = (rgb) => new THREE.Color(rgb[0] / 255, rgb[1] / 255, rgb[2] / 255);
const MAU_NUOC = [214, 234, 248];

const HOTSPOTS_TRAM = {
  "chuan-do": [
    { so: "①", viTri: [0, 0.45, 0], ten: "Buret chuẩn độ", moTa: "Vặn khoá để nhỏ từng giọt dung dịch chuẩn vào bình tam giác" },
    { so: "②", viTri: [0, 0.08, 0], ten: "Bình tam giác Erlenmeyer", moTa: "Chứa dung dịch cần chuẩn độ và chất chỉ thị màu, lắc đều khi chuẩn độ" },
    { so: "③", viTri: [0.35, 0.1, -0.1], ten: "Chai dung dịch chuẩn", moTa: "Chai đựng dung dịch hoá chất chuẩn nồng độ" }
  ],
  "an-toan": [
    { so: "①", viTri: [0, 0.35, 0], ten: "Bình chữa cháy CO₂", moTa: "Rút chốt an toàn, hướng loa phun vào gốc lửa để dập tắt đám cháy" },
    { so: "②", viTri: [0.45, 0.3, 0], ten: "Vòi rửa mắt khẩn cấp", moTa: "Đẩy cần gạt xả nước ngay khi hoá chất văng vào mắt, rửa tối thiểu 15 phút" },
    { so: "③", viTri: [-0.4, 0.1, 0], ten: "Kính bảo hộ & Găng tay", moTa: "Trang bị bảo hộ bắt buộc trước khi bước vào thực hành hoá học" }
  ],
  "nhan-biet-ion": [
    { so: "①", viTri: [0, 0.15, 0], ten: "Bộ ống nghiệm mẫu", moTa: "Chứa các mẫu dung dịch ion kim loại cần nhận biết" },
    { so: "②", viTri: [0.3, 0.08, -0.1], ten: "Chai thuốc thử đặc trưng", moTa: "Nhỏ thuốc thử (NaOH, AgNO₃, BaCl₂...) quan sát màu kết tủa" },
    { so: "③", viTri: [-0.3, 0.1, 0], ten: "Kẹp gỗ ống nghiệm", moTa: "Dùng để kẹp giữ ống nghiệm khi làm phản ứng có sinh nhiệt hoặc đun nóng" }
  ],
  "pin-galvani": [
    { so: "①", viTri: [-0.35, 0.15, 0], ten: "Cực âm Anot Zn", moTa: "Thanh kẽm nhúng trong ZnSO₄: Xảy ra quá trình oxy hoá Zn → Zn²⁺ + 2e" },
    { so: "②", viTri: [0.35, 0.15, 0], ten: "Cực dương Catot Cu", moTa: "Thanh đồng nhúng trong CuSO₄: Xảy ra quá trình khử Cu²⁺ + 2e → Cu" },
    { so: "③", viTri: [0, 0.18, -0.1], ten: "Cầu muối & Vôn kế", moTa: "Cầu muối agar-KCl cân bằng điện tích, vôn kế đo suất điện động chuẩn E° = 1.10V" }
  ],
  "dien-phan": [
    { so: "①", viTri: [0, 0.15, 0], ten: "Bình điện phân", moTa: "Chứa dung dịch CuCl₂ và hai điện cực trơ than chì" },
    { so: "②", viTri: [-0.4, 0.12, -0.1], ten: "Nguồn điện DC", moTa: "Cung cấp dòng điện một chiều thúc đẩy phản ứng điện phân" },
    { so: "③", viTri: [0.15, 0.18, 0], ten: "Điện cực trơ Anot / Catot", moTa: "Catot bám kim loại Cu đỏ, Anot sủi bọt khí clo vàng lục có mùi hắc" }
  ]
};

/** Đọc biến màu CSS hiện tại của web (sáng / tối) – không gán cứng. */
export function bienMauWeb() {
  const cs = getComputedStyle(document.documentElement);
  const doc = (ten, md) => (cs.getPropertyValue(ten).trim() || md);
  return { nen: doc("--nhan-lom", "#eef4fa"), nhan: doc("--nhan", "#0f2b48"), the: doc("--the-lon-nen", "#ffffff"), chu: doc("--muc", "#09090b"),
    toi: document.documentElement.dataset.theme === "toi" };
}

// ------------------------------------------------------------------ hình học
// Hình dạng dụng cụ nằm ở hinh-hoc.js (UMD, nạp bằng <script> trước module này): profile bo góc liền tiếp tuyến, mặt tròn xoay có
// pháp tuyến theo công thức (không đường may), thành thuỷ tinh dày thật + gờ miệng. LabHinh.cs (Unity) cùng thuật toán.
const HH = window.PTNHinhHoc;
function hinhTuLuoi(m) {
  const g = new THREE.BufferGeometry();
  g.setAttribute("position", new THREE.BufferAttribute(m.v, 3));
  g.setAttribute("normal", new THREE.BufferAttribute(m.n, 3));
  g.setAttribute("uv", new THREE.BufferAttribute(m.uv, 2));
  g.setIndex(new THREE.BufferAttribute(m.i, 1));
  g.computeBoundingBox();
  g.computeBoundingSphere();
  return g;
}
const latheMem = (ctrl, phan = 72) => hinhTuLuoi(HH.latheMem(ctrl, { phan }));
/** Thành thuỷ tinh dày của dụng cụ có profile NGOÀI `ngoai` (xem HH.DUNG_CU). */
const thuyTinhMem = (ngoai, phan = 72) => latheMem(HH.thanhThuyTinh(ngoai), phan);
/** Profile ngoài dày (dùng tính lòng bình / mực chất lỏng). */
const profileCoc = (r, h) => HH.muotProfile(HH.DUNG_CU.coc(r, h));
const profileBinhTamGiac = (r, h, rc, hc) => HH.muotProfile(HH.DUNG_CU.binhTamGiac(r, h, rc, hc));
const profileOngNghiem = (r, h) => HH.muotProfile(HH.DUNG_CU.ongNghiem(r, h));
const banKinhTai = HH.banKinhTai;
const theTichDenH = HH.theTichDenH;
const hDeCoTheTich = HH.hDeCoTheTich;
/** Khối chất lỏng (lathe kín) trong bình có profile `profile`, cao h, mặt khum lõm. */
const hinhChatLong = (profile, h, day = HH.DAY_KINH, phan = 48) => latheMem(HH.chatLong(profile, h, day), phan);

export class PhongLabWeb {
  constructor(khung, { chatLuong = "cao", onThaoTac = () => {}, onChon = () => {}, onDenGan = () => {}, onVaoTram = () => {}, onRoiTram = () => {}, onKhu = () => {}, onThongBao = () => {} } = {}) {
    this.khung = khung;
    this.onThaoTac = onThaoTac;
    this.onChon = onChon;
    this.onDenGan = onDenGan;
    this.onVaoTram = onVaoTram;
    this.onRoiTram = onRoiTram;
    this.onKhu = onKhu;
    this.onThongBao = onThongBao;
    this.chatLuong = chatLuong;
    this._taiNguyen = new Set();
    this._hoatHinh = [];
    this._chon = [];
    this._vlNhan = [];
    this._donDep = [];
    this._dangChay = true;
    this._trongKhung = true;
    this.ct = null;
    this._tram = {};
    this._cheDo = "di_dao";
    this.tramDangLam = null;
    this._banDo = null;

    const cao = chatLuong === "cao";
    const r = (this.renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: "high-performance" }));
    r.outputColorSpace = THREE.SRGBColorSpace;
    r.toneMapping = THREE.ACESFilmicToneMapping;
    r.toneMappingExposure = 1.0;
    r.shadowMap.enabled = cao;
    r.shadowMap.type = THREE.PCFSoftShadowMap;
    this._datPixelRatio();
    khung.appendChild(r.domElement);
    r.domElement.setAttribute("aria-hidden", "true");

    this.nhan2d = new CSS2DRenderer();
    this.nhan2d.domElement.className = "ptn-lop-nhan";
    khung.appendChild(this.nhan2d.domElement);

    this.scene = new THREE.Scene();
    this.camera = new THREE.PerspectiveCamera(38, 1, 0.01, 40);
    this.controls = new OrbitControls(this.camera, r.domElement);
    Object.assign(this.controls, { enableDamping: true, dampingFactor: 0.08, minPolarAngle: 0.25, maxPolarAngle: 1.45, enablePan: false, rotateSpeed: 0.7 });
    this.controls.addEventListener("start", () => { this._daChinhTay = true; });

    const pm = new THREE.PMREMGenerator(r);
    this.scene.environment = pm.fromScene(new RoomEnvironment(r), 0.04).texture;
    pm.dispose();

    this.anhSang = new THREE.HemisphereLight(0xffffff, 0x8899aa, 0.55);
    this.scene.add(this.anhSang);
    const d = (this.den = new THREE.DirectionalLight(0xffffff, 1.6));
    d.position.set(1.4, 2.6, 1.6);
    d.castShadow = cao;
    d.shadow.mapSize.set(2048, 2048);
    Object.assign(d.shadow.camera, { left: -1.4, right: 1.4, top: 1.4, bottom: -1.4, near: 0.5, far: 7 });
    d.shadow.radius = 4;
    d.shadow.bias = -0.0004;
    this.scene.add(d);
    const fill = new THREE.DirectionalLight(0xcfe3ff, 0.45);
    fill.position.set(-2, 1.2, -1);
    this.scene.add(fill);

    this.nhom = new THREE.Group();
    this.scene.add(this.nhom);
    this._dungBan();
    this.doiGiaoDien();

    this._dongHo = new THREE.Clock();
    this._fpsDem = 0;
    this._fpsTruoc = performance.now();
    this._fpsHienTai = 60;
    this._vong = this._vong.bind(this);
    this._suKien();
    requestAnimationFrame(this._vong);
  }

  // ---------------------------------------------------------------- hạ tầng
  _datPixelRatio() {
    const dt = matchMedia("(max-width: 900px)").matches;
    const tran = this.chatLuong === "cao" ? (dt ? 1.5 : 2) : 1;
    this.renderer.setPixelRatio(Math.min(devicePixelRatio || 1, tran));
  }
  _suKien() {
    const ro = (this._ro = new ResizeObserver(() => this._doiCo()));
    ro.observe(this.khung);
    this._io = new IntersectionObserver((e) => { this._trongKhung = e[0]?.isIntersecting ?? true; }, { threshold: 0.01 });
    this._io.observe(this.khung);
    this._anTab = () => { this._dangChay = !document.hidden; if (this._dangChay) this._dongHo.getDelta(); };
    document.addEventListener("visibilitychange", this._anTab);
    const el = this.renderer.domElement;
    let xuong = null;
    el.addEventListener("pointerdown", (e) => { xuong = { x: e.clientX, y: e.clientY }; });
    el.addEventListener("pointerup", (e) => {
      if (!xuong || Math.hypot(e.clientX - xuong.x, e.clientY - xuong.y) > 6) return;
      const r = el.getBoundingClientRect();
      const ndc = V2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
      const ray = new THREE.Raycaster();
      ray.setFromCamera(ndc, this.camera);
      const dsChon = (this.tramDangLam && this._tram[this.tramDangLam])
        ? this._tram[this.tramDangLam].chon
        : this._chon;
      const trung = ray.intersectObjects(dsChon, true)[0];
      if (!trung) return;
      let o = trung.object;
      while (o && !o.userData.lenh && !o.userData.chon) o = o.parent;
      if (o?.userData.lenh) this.onThaoTac(o.userData.lenh, o.userData.thamSo || {});
      else if (o?.userData.chon) this.onChon(o.userData.chon);
    });
    this.khung.addEventListener("keydown", (e) => {
      const tag = document.activeElement?.tagName?.toLowerCase();
      if (tag === "input" || tag === "textarea" || tag === "select" || document.activeElement?.isContentEditable || document.activeElement?.closest("dialog")) return;
      const m = { ArrowLeft: "trai", ArrowRight: "phai", ArrowUp: "len", ArrowDown: "xuong", "+": "gan", "=": "gan", "-": "xa", r: "dat_lai", R: "dat_lai" }[e.key];
      if (!m || e.target !== this.khung) return;
      e.preventDefault();
      this.dieuKhienCamera(m);
    });
  }
  _capNhatViewOffset(bat) {
    const w = this.khung.clientWidth || window.innerWidth;
    const h = this.khung.clientHeight || window.innerHeight;
    if (!w || !h) return;
    const isDesktop = w >= 900;
    if (bat && isDesktop) {
      const panelW = 380;
      this.camera.setViewOffset(w, h, Math.round(panelW / 2), 0, w, h);
    } else {
      this.camera.clearViewOffset();
    }
  }
  _doiCo() {
    const w = this.khung.clientWidth, h = this.khung.clientHeight;
    if (!w || !h) return;
    this.renderer.setSize(w, h, false);
    this.nhan2d.setSize(w, h);
    this.camera.aspect = w / h;
    if (this.cheDo === "tai_ban") {
      this._capNhatViewOffset(true);
    } else {
      this.camera.clearViewOffset();
    }
    this.camera.updateProjectionMatrix();
    if (this.ct?.goc && !this._daChinhTay && this.cheDo === "tai_ban") {
      const goc = (this.tramDangLam && this._tram[this.tramDangLam])
        ? this._gocTheGioi(this._tram[this.tramDangLam])
        : this.ct.goc;
      this.camera.position.copy(this._viTriVua(goc));
    }
  }
  _vong() {
    requestAnimationFrame(this._vong);
    if (!this._dangChay || !this._trongKhung || this._an) return;   // tab ẩn / khung ra khỏi màn hình / đang dùng Unity → không vẽ
    const dt = Math.min(0.05, this._dongHo.getDelta());
    const t = this._dongHo.elapsedTime;

    this._fpsDem = (this._fpsDem || 0) + 1;
    const bayGio = performance.now();
    if (bayGio - this._fpsTruoc >= 1000) {
      this._fpsHienTai = Math.round((this._fpsDem * 1000) / (bayGio - this._fpsTruoc));
      this._fpsDem = 0;
      this._fpsTruoc = bayGio;
    }

    if (this.nhanVat) this.nhanVat.capNhat(dt, t);
    if (this.phong) this.phong.capNhat(dt, t);

    if (this._tram && Object.keys(this._tram).length > 0) {
      const camPos = this.camera.position;
      for (const [id, tr] of Object.entries(this._tram)) {
        if (id === this.tramDangLam) {
          for (const f of tr.hoatHinh) f(dt, t);
        } else {
          const d = Math.hypot(camPos.x - tr.tam[0], camPos.z - tr.tam[1]);
          if (d <= 6) {
            for (const f of tr.hoatHinh) f(dt, t);
          }
        }
      }
    } else {
      for (const f of this._hoatHinh) f(dt, t);
    }

    if (this.cheDo === "tai_ban" || this.cheDo === "tong_quan") {
      this.controls.enabled = true;
      this.controls.update();
    } else {
      this.controls.enabled = false;
    }
    this.renderer.render(this.scene, this.camera);
    this.nhan2d.render(this.scene, this.camera);
    this._demKhung = (this._demKhung || 0) + 1;
  }
  /** Ẩn / hiện bản Web 3D (khi chuyển sang Unity): ẩn thì ngừng vẽ hoàn toàn. */
  hien(co) {
    this._an = !co;
    this.renderer.domElement.style.display = co ? "" : "none";
    this.nhan2d.domElement.style.display = co ? "" : "none";
    if (co) { this._dongHo.getDelta(); this._doiCo(); }
  }
  /** Số khung hình đã vẽ (để đo fps khi kiểm thử). */
  get soKhung() { return this._demKhung || 0; }

  /** Thống kê hiệu năng: draw calls, số tam giác và fps trung bình. */
  thongKe() {
    return {
      fps: this._fpsHienTai || 60,
      draw_calls: this.renderer?.info?.render?.calls ?? 0,
      tam_giac: this.renderer?.info?.render?.triangles ?? 0,
      so_khung: this.soKhung
    };
  }

  _theo(o) { this._taiNguyen.add(o); return o; }
  /** Số mặt quanh trục của dụng cụ tròn xoay theo chất lượng đồ hoạ. */
  _phan() { return this.chatLuong === "cao" ? 72 : 40; }
  _vl(loai, tuy = {}) {
    const cao = this.chatLuong === "cao";
    let m;
    if (loai === "thuy_tinh") {
      m = cao
        ? new THREE.MeshPhysicalMaterial({ color: 0xffffff, metalness: 0, roughness: 0.05, transmission: 1, ior: 1.5, thickness: 0.02,
          specularIntensity: 1, envMapIntensity: 1.7, clearcoat: 1, clearcoatRoughness: 0.04,
          attenuationColor: new THREE.Color(0xd6e8fb), attenuationDistance: 0.5, side: THREE.DoubleSide, depthWrite: false })
        : new THREE.MeshPhysicalMaterial({ color: 0xe1ecf7, roughness: 0.06, transparent: true, opacity: 0.26, clearcoat: 1, envMapIntensity: 1.7, side: THREE.DoubleSide, depthWrite: false });
      m.userData.thuyTinh = true;
    } else if (loai === "long") {
      const c = tuy.rgb ? rgbMau(tuy.rgb) : rgbMau(MAU_NUOC);
      m = new THREE.MeshPhysicalMaterial({ color: c, roughness: 0.12, metalness: 0, transparent: true, opacity: tuy.opacity ?? 0.42, clearcoat: 0.6,
        clearcoatRoughness: 0.1, envMapIntensity: 0.9, depthWrite: false });
    } else if (loai === "kim_loai") {
      m = new THREE.MeshStandardMaterial({ color: tuy.rgb ? rgbMau(tuy.rgb) : 0xb8bec8, metalness: 0.85, roughness: tuy.nham ?? 0.32, envMapIntensity: 1.1 });
    } else if (loai === "nhua") {
      m = new THREE.MeshStandardMaterial({ color: tuy.mau ?? 0x30363d, metalness: 0.05, roughness: tuy.nham ?? 0.55 });
    } else if (loai === "phat_sang") {
      m = new THREE.MeshBasicMaterial({ color: tuy.mau ?? 0x38bdf8, transparent: true, opacity: tuy.opacity ?? 1, depthWrite: false,
        blending: tuy.cong ? THREE.AdditiveBlending : THREE.NormalBlending });
    } else if (loai === "mo") {
      m = new THREE.MeshStandardMaterial({ color: tuy.mau ?? 0xffffff, roughness: tuy.nham ?? 0.85, metalness: 0, map: tuy.map ?? null, transparent: !!tuy.trongSuot, alphaTest: tuy.alphaTest ?? 0 });
    }
    return this._theo(m);
  }
  _luoi(geo, vl, { bong = true, nhan = true } = {}) {
    this._theo(geo);
    const m = new THREE.Mesh(geo, vl);
    m.castShadow = bong && this.chatLuong === "cao";
    m.receiveShadow = nhan;
    if (vl.userData?.thuyTinh) { m.renderOrder = 2; m.castShadow = false; }
    if (vl.transparent && !vl.userData?.thuyTinh) m.renderOrder = 1;
    return m;
  }
  _nhan(chu, vt, lop = "", cha = this.nhom) {
    const d = document.createElement("div");
    d.className = `ptn-nhan ${lop}`.trim();
    d.textContent = chu;
    const o = new CSS2DObject(d);
    o.position.copy(vt);
    cha.add(o);
    return o;
  }
  _nutNhan(chu, vt, lop, onClick, nhanTruyCap) {
    const b = document.createElement("button");
    b.type = "button";
    b.className = lop;
    b.textContent = chu;
    if (nhanTruyCap) b.setAttribute("aria-label", nhanTruyCap);
    b.addEventListener("click", (e) => { e.stopPropagation(); onClick(); });
    b.addEventListener("pointerdown", (e) => e.stopPropagation());
    const o = new CSS2DObject(b);
    o.position.copy(vt);
    this.nhom.add(o);
    return o;
  }
  _manHinh(rong = 256, cao = 96) {
    const c = document.createElement("canvas");
    c.width = rong; c.height = cao;
    const tex = this._theo(new THREE.CanvasTexture(c));
    tex.colorSpace = THREE.SRGBColorSpace;
    return { c, g: c.getContext("2d"), tex };
  }
  _veManHinh(mh, chu, mau = "#7ef0c2") {
    const { c, g, tex } = mh;
    g.fillStyle = "#0b1622"; g.fillRect(0, 0, c.width, c.height);
    g.fillStyle = mau; g.font = `600 ${Math.round(c.height * 0.52)}px "JetBrains Mono", monospace`;
    g.textAlign = "center"; g.textBaseline = "middle";
    g.fillText(chu, c.width / 2, c.height / 2 + 2);
    tex.needsUpdate = true;
  }

  _dungBan() {
    const geo = new RoundedBoxGeometry(2.6, 0.06, 1.25, 4, 0.025);
    this.vlBan = new THREE.MeshStandardMaterial({ color: 0xe9eef4, roughness: 0.92, metalness: 0 });
    this.ban = new THREE.Mesh(geo, this.vlBan);
    this.ban.position.y = -0.03;
    this.ban.receiveShadow = true;
    this.scene.add(this.ban);
  }
  doiGiaoDien() {
    const b = bienMauWeb();
    const nen = new THREE.Color(b.nen), nhan = new THREE.Color(b.nhan);
    this.scene.background = nen;
    this.scene.fog = new THREE.Fog(nen, this._banDo ? 18 : 3.2, this._banDo ? 35 : 9);
    if (this.vlBan) {
      this.vlBan.color.copy(nen).lerp(b.toi ? new THREE.Color(0xffffff) : nhan, b.toi ? 0.07 : 0.13);
    }
    this.anhSang.intensity = b.toi ? 0.45 : 0.65;
    this.renderer.toneMappingExposure = b.toi ? 1.08 : 1.0;
    this.mauNhan = nhan;
    for (const m of this._vlNhan || []) m.color.copy(nhan);
    for (const tr of Object.values(this._tram || {})) {
      for (const m of tr.vlNhan || []) m.color.copy(nhan);
    }
    if (this.phong) this.phong.doiGiaoDien();
  }
  datChatLuong(q) {
    if (q === this.chatLuong) return;
    this.chatLuong = q;
    this.renderer.shadowMap.enabled = q === "cao";
    this.den.castShadow = q === "cao";
    this._datPixelRatio();
    this._doiCo();
    if (this._banDo) {
      if (this.phong) this.phong.datChatLuong(q);
      const dsDung = { "chuan-do": this._dungChuanDo, "an-toan": this._dungAnToan, "nhan-biet-ion": this._dungNhanBiet, "pin-galvani": this._dungPin, "dien-phan": this._dungDienPhan };
      for (const [id, tr] of Object.entries(this._tram)) {
        const dl = tr.ct.tn, st = tr.ct.s;
        this._donTram(id);
        const hamDung = dsDung[id];
        if (hamDung) {
          this._trongTram(id, () => { hamDung.call(this, dl, st); });
          if (st) this.capNhat(st);
        }
        tr.nhom.traverse((o) => {
          if (o.isCSS2DObject && o.element) {
            o.element.style.display = (id === this.tramDangLam) ? "" : "none";
          }
        });
      }
    } else if (this.ct) {
      this.mo(this.ct.id, this.ct.tn, this.ct.s, true);
    }
  }

  // ---------------------------------------------------------------- camera
  dieuKhienCamera(lenh) {
    if (this.cheDo === "di_dao" && this.nhanVat) {
      this.nhanVat.dieuKhienCamera(lenh);
      return;
    }
    const c = this.controls, cam = this.camera;
    const v = cam.position.clone().sub(c.target);
    const sph = new THREE.Spherical().setFromVector3(v);
    if (lenh === "trai") sph.theta -= 0.3;
    else if (lenh === "phai") sph.theta += 0.3;
    else if (lenh === "len") sph.phi = Math.max(c.minPolarAngle, sph.phi - 0.15);
    else if (lenh === "xuong") sph.phi = Math.min(c.maxPolarAngle, sph.phi + 0.15);
    else if (lenh === "gan") sph.radius = Math.max(c.minDistance, sph.radius * 0.82);
    else if (lenh === "xa") sph.radius = Math.min(c.maxDistance, sph.radius * 1.2);
    else if (lenh === "dat_lai") {
      const goc = (this.tramDangLam && this._tram[this.tramDangLam])
        ? this._gocTheGioi(this._tram[this.tramDangLam])
        : this.ct?.goc;
      return this._gocNhin(goc, true);
    }
    this._daChinhTay = true;
    const dich = V3(0, 0, 0).setFromSpherical(sph).add(c.target);
    gsap.to(cam.position, { x: dich.x, y: dich.y, z: dich.z, duration: 0.45, ease: "power2.out" });
  }
  /** Vị trí camera cho góc nhìn: lùi ra đủ xa để cả chiều rộng `rong` của cảnh lọt khung (khung hẹp / điện thoại thì lùi xa hơn). */
  _viTriVua(goc) {
    const huong = goc.vi_tri.clone().sub(goc.dich);
    const d = huong.length();
    if (!goc.rong && !goc.cao) return goc.vi_tri.clone();
    const w = this.khung.clientWidth || window.innerWidth;
    const h = this.khung.clientHeight || window.innerHeight;
    const panelW = (this.cheDo === "tai_ban" && w >= 900) ? 380 : 0;
    const aspectHien = Math.max(0.3, (w - panelW) / h);
    const nuaDoc = THREE.MathUtils.degToRad(this.camera.fov / 2);
    const nuaNgang = Math.atan(Math.tan(nuaDoc) * aspectHien);
    const can = Math.max(goc.rong ? (goc.rong / 2) / Math.tan(nuaNgang) : 0, goc.cao ? (goc.cao / 2) / Math.tan(nuaDoc) : 0) * 1.08;
    return goc.dich.clone().add(huong.multiplyScalar(Math.max(d, can) / d));
  }
  _gocNhin(goc, muot = true) {
    if (!goc) return;
    const c = this.controls;
    const vt = this._viTriVua(goc);
    this._daChinhTay = false;
    c.minDistance = goc.gan ?? 0.25;
    c.maxDistance = Math.max(goc.xa ?? 3, vt.distanceTo(goc.dich) * 1.4);
    if (!muot) { this.camera.position.copy(vt); c.target.copy(goc.dich); return; }
    gsap.to(this.camera.position, { x: vt.x, y: vt.y, z: vt.z, duration: 0.9, ease: "power2.inOut" });
    gsap.to(c.target, { x: goc.dich.x, y: goc.dich.y, z: goc.dich.z, duration: 0.9, ease: "power2.inOut" });
  }

  // ---------------------------------------------------------------- quản lý nhiều trạm & căn phòng
  get cheDo() {
    if (this._cheDo === "tai_ban") return "tai_ban";
    if (this.nhanVat?.cheDoCamera === "toan_canh") return "tong_quan";
    return this._cheDo || "di_dao";
  }

  _gocTheGioi(tr) {
    if (!tr || !tr.ct?.goc) return null;
    const lg = tr.ct.goc;
    const wPos = tr.goc.localToWorld(lg.vi_tri.clone());
    const wDich = tr.goc.localToWorld(lg.dich.clone());
    return {
      vi_tri: wPos,
      dich: wDich,
      gan: lg.gan,
      xa: lg.xa,
      rong: lg.rong,
      cao: lg.cao
    };
  }

  _trongTram(id, fn) {
    let tr = this._tram[id];
    if (!tr) {
      const cfg = this._banDo?.tram?.find((t) => t.tn === id);
      const tam = cfg?.tam || [0, 0];
      const huongDeg = cfg?.huong || 0;
      const caoBan = this._banDo?.phong?.cao_ban ?? 0.9;
      const diemDung = cfg?.diem_dung || [tam[0], tam[1] + 1.2];

      const goc = new THREE.Group();
      goc.position.set(tam[0], caoBan, tam[1]);
      goc.rotation.y = THREE.MathUtils.degToRad(huongDeg);
      this.scene.add(goc);

      const nhom = new THREE.Group();
      goc.add(nhom);

      tr = this._tram[id] = {
        id,
        tn: id,
        goc,
        nhom,
        tam,
        diem_dung: diemDung,
        hoatHinh: [],
        chon: [],
        vlNhan: [],
        donDep: [],
        taiNguyen: new Set(),
        ct: { id, tn: id, s: null, goc: null, capNhat: null, hieuUng: null }
      };
    }

    const luuNhom = this.nhom;
    const luuHoatHinh = this._hoatHinh;
    const luuChon = this._chon;
    const luuVlNhan = this._vlNhan;
    const luuDonDep = this._donDep;
    const luuTaiNguyen = this._taiNguyen;
    const luuCt = this.ct;

    this.nhom = tr.nhom;
    this._hoatHinh = tr.hoatHinh;
    this._chon = tr.chon;
    this._vlNhan = tr.vlNhan;
    this._donDep = tr.donDep;
    this._taiNguyen = tr.taiNguyen;
    this.ct = tr.ct;

    try {
      fn();
    } finally {
      this.nhom = luuNhom;
      this._hoatHinh = luuHoatHinh;
      this._chon = luuChon;
      this._vlNhan = luuVlNhan;
      this._donDep = luuDonDep;
      this._taiNguyen = luuTaiNguyen;
      this.ct = luuCt;
    }
  }

  _donTram(id) {
    const tr = this._tram[id];
    if (!tr) return;
    for (const f of tr.donDep || []) f();
    tr.donDep = [];
    if (tr.hotspots) {
      for (const h of tr.hotspots) {
        if (h.element?.parentNode) h.element.parentNode.removeChild(h.element);
        tr.goc.remove(h);
      }
      tr.hotspots = null;
    }
    tr.nhom.traverse((o) => {
      if (o.isCSS2DObject && o.element?.parentNode) {
        o.element.parentNode.removeChild(o.element);
      }
    });
    tr.goc.remove(tr.nhom);
    for (const x of tr.taiNguyen) x.dispose?.();
    tr.taiNguyen.clear();
    tr.hoatHinh = [];
    tr.chon = [];
    tr.vlNhan = [];
    tr.nhom = new THREE.Group();
    tr.goc.add(tr.nhom);
  }

  _taoHotspots(id) {
    const tr = this._tram[id];
    if (!tr || tr.hotspots) return;
    const ds = HOTSPOTS_TRAM[id] || [];
    tr.hotspots = [];
    for (const h of ds) {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "ptn-hotspot";
      btn.textContent = h.so;
      btn.setAttribute("aria-label", `${h.ten}: ${h.moTa}`);
      btn.title = `${h.ten} - Bấm để xem hướng dẫn`;
      btn.addEventListener("click", (e) => {
        e.stopPropagation();
        this.onThongBao(`💡 ${h.ten}: ${h.moTa}`);
      });
      btn.addEventListener("pointerdown", (e) => e.stopPropagation());

      const obj2d = new CSS2DObject(btn);
      obj2d.position.set(h.viTri[0], h.viTri[1], h.viTri[2]);
      tr.goc.add(obj2d);
      tr.hotspots.push(obj2d);
    }
  }

  _hienHotspots(id, hien) {
    const tr = this._tram[id];
    if (!tr) return;
    if (hien && !tr.hotspots) this._taoHotspots(id);
    for (const obj of tr.hotspots || []) {
      if (obj.element) obj.element.style.display = hien ? "" : "none";
    }
  }

  _dungLaiTram(id, st) {
    const tr = this._tram[id];
    if (!tr) return;
    this._donTram(id);
    const hamDung = {
      "chuan-do": this._dungChuanDo,
      "an-toan": this._dungAnToan,
      "nhan-biet-ion": this._dungNhanBiet,
      "pin-galvani": this._dungPin,
      "dien-phan": this._dungDienPhan
    }[id];
    if (hamDung) {
      this._trongTram(id, () => {
        hamDung.call(this, tr.ct.tn, st);
      });
      this.capNhat(st);
      if (this.tramDangLam === id) {
        tr.nhom.traverse((o) => {
          if (o.isCSS2DObject && o.element) o.element.style.display = "";
        });
      }
    }
  }

  dungPhong(banDo, duLieu = {}, trangThai = {}, onTienDo = () => {}) {
    this._banDo = banDo;
    if (this.ban) this.ban.visible = false;

    for (const id of Object.keys(this._tram)) {
      this._donTram(id);
      this.scene.remove(this._tram[id].goc);
    }
    this._tram = {};

    if (this.phong) this.phong.huy();
    this.phong = new PhongMo(this.scene, banDo, this.chatLuong, onTienDo);

    const dsDung = {
      "an-toan": this._dungAnToan,
      "chuan-do": this._dungChuanDo,
      "nhan-biet-ion": this._dungNhanBiet,
      "pin-galvani": this._dungPin,
      "dien-phan": this._dungDienPhan
    };

    const tenKhu = {
      "an-toan": "Khu An toàn",
      "chuan-do": "Khu Định lượng",
      "nhan-biet-ion": "Khu Định tính",
      "pin-galvani": "Khu Điện hoá (Pin)",
      "dien-phan": "Khu Điện hoá (Điện phân)"
    };

    for (const [id, hamDung] of Object.entries(dsDung)) {
      onTienDo(`Đang dựng ${tenKhu[id] || id}…`);
      const dl = duLieu[id] || {};
      const st = trangThai[id] || {};
      this._trongTram(id, () => {
        hamDung.call(this, dl, st);
      });
      if (st && Object.keys(st).length > 0) {
        this.capNhat(st);
      }
    }

    // Ẩn nhãn CSS2D ban đầu
    for (const [id, tr] of Object.entries(this._tram)) {
      tr.nhom.traverse((o) => {
        if (o.isCSS2DObject && o.element) {
          o.element.style.display = "none";
        }
      });
    }

    if (this.nhanVat) this.nhanVat.huy();
    this.nhanVat = new NhanVat(this.scene, banDo, this.camera, this.controls, this.khung, {
      onDenGan: (gan) => this.onDenGan?.(gan ? gan.tn : null),
      onVaoTram: (id) => this.vaoTram(id),
      onRoiTram: () => this.roiTram(),
      onKhu: (khuId) => this.onKhu?.(khuId),
      onThongBao: (msg) => this.onThongBao?.(msg),
      onTongQuan: (bat) => {
        this._cheDo = bat ? "tong_quan" : "di_dao";
        this.phong?.lamNhaBupBe(bat);
      }
    });

    this._cheDo = "di_dao";
    this.tramDangLam = null;
    this.doiGiaoDien();
  }

  vaoTram(id, { tucThi = false } = {}) {
    const tr = this._tram[id];
    if (!tr) return;
    this._cheDo = "tai_ban";
    this.tramDangLam = id;
    this.ct = tr.ct;

    if (document.exitPointerLock && document.pointerLockElement) {
      document.exitPointerLock();
    }

    if (this.nhanVat && this.nhanVat.cheDoCamera !== "tai_ban") {
      this.nhanVat.vaoTram(id);
    }

    this.phong?.lamMo(true);
    this._capNhatViewOffset(true);

    const wgoc = this._gocTheGioi(tr);
    if (wgoc) {
      this.controls.enabled = true;
      this.controls.minDistance = wgoc.gan ?? 0.25;
      this.controls.maxDistance = wgoc.xa ?? 3.0;
      const vt = this._viTriVua(wgoc);
      this._daChinhTay = false;
      if (tucThi) {
        this.camera.position.copy(vt);
        this.controls.target.copy(wgoc.dich);
      } else {
        gsap.to(this.camera.position, { x: vt.x, y: vt.y, z: vt.z, duration: 1.0, ease: "power2.inOut" });
        gsap.to(this.controls.target, { x: wgoc.dich.x, y: wgoc.dich.y, z: wgoc.dich.z, duration: 1.0, ease: "power2.inOut" });
      }
    }

    for (const [tid, tObj] of Object.entries(this._tram)) {
      const hien = (tid === id);
      tObj.nhom.traverse((o) => {
        if (o.isCSS2DObject && o.element) {
          o.element.style.display = hien ? "" : "none";
        }
      });
      this._hienHotspots(tid, hien);
    }

    if (this.den && this.den.shadow) {
      const sc = this.den.shadow.camera;
      sc.left = -1.6; sc.right = 1.6; sc.top = 1.6; sc.bottom = -1.6;
      sc.updateProjectionMatrix();
      this.den.target.position.set(tr.tam[0], 0.9, tr.tam[1]);
      this.scene.add(this.den.target);
    }

    this.onVaoTram?.(id);
  }

  roiTram() {
    const idCu = this.tramDangLam;
    this._cheDo = "di_dao";
    this.tramDangLam = null;

    this._capNhatViewOffset(false);
    this.phong?.lamMo(false);
    this.controls.enabled = false;

    if (this.nhanVat && this.nhanVat.cheDoCamera === "tai_ban") {
      this.nhanVat.roiTram();
    }

    for (const [tid, tObj] of Object.entries(this._tram)) {
      tObj.nhom.traverse((o) => {
        if (o.isCSS2DObject && o.element) {
          o.element.style.display = "none";
        }
      });
      this._hienHotspots(tid, false);
    }

    if (this.den && this.den.shadow) {
      const sc = this.den.shadow.camera;
      sc.left = -8.0; sc.right = 8.0; sc.top = 8.0; sc.bottom = -8.0;
      sc.updateProjectionMatrix();
      if (this.nhanVat) {
        this.den.target.position.set(this.nhanVat.viTri[0], 0, this.nhanVat.viTri[1]);
      }
    }

    this.onRoiTram?.(idCu);
  }

  layThongTinCamera() {
    const cam = this.camera;
    const dir = new THREE.Vector3();
    cam.getWorldDirection(dir);
    return {
      vi_tri: [cam.position.x, cam.position.y, cam.position.z],
      huong: [dir.x, dir.y, dir.z],
      che_do: this.cheDo,
      goc_nhin: this.nhanVat?.cheDoGocNhin === "goc_1" ? "ngoi_nhat" : "ngoi_ba",
      thay_nhan_vat: this.nhanVat?.kiemTraThayNhanVat() ?? true
    };
  }

  tramTrongKhung(id) {
    const tr = this._tram[id];
    if (!tr) return { ti_le: 0, tam_la_canvas: false };
    const w = this.khung.clientWidth || window.innerWidth;
    const h = this.khung.clientHeight || window.innerHeight;
    const panelW = (this.cheDo === "tai_ban" && w >= 900) ? 380 : 0;
    const gioiHanPhaiNdc = 1 - 2 * (panelW / w);

    const tamWorld = new THREE.Vector3(tr.tam[0], 0.9, tr.tam[1]);
    const tamNdc = tamWorld.clone().project(this.camera);

    const tamLaCanvas = (
      tamNdc.z > -1 && tamNdc.z < 1 &&
      tamNdc.x >= -1 && tamNdc.x <= gioiHanPhaiNdc &&
      tamNdc.y >= -1 && tamNdc.y <= 1
    );

    const dx = 1.2, dz = 0.6, dy = 0.45;
    const cacDinh = [
      new THREE.Vector3(tr.tam[0] - dx, 0.9 - dy, tr.tam[1] - dz),
      new THREE.Vector3(tr.tam[0] + dx, 0.9 - dy, tr.tam[1] - dz),
      new THREE.Vector3(tr.tam[0] - dx, 0.9 + dy, tr.tam[1] - dz),
      new THREE.Vector3(tr.tam[0] + dx, 0.9 + dy, tr.tam[1] - dz),
      new THREE.Vector3(tr.tam[0] - dx, 0.9 - dy, tr.tam[1] + dz),
      new THREE.Vector3(tr.tam[0] + dx, 0.9 - dy, tr.tam[1] + dz),
      new THREE.Vector3(tr.tam[0] - dx, 0.9 + dy, tr.tam[1] + dz),
      new THREE.Vector3(tr.tam[0] + dx, 0.9 + dy, tr.tam[1] + dz),
    ];

    let trongKhung = 0;
    for (const p of cacDinh) {
      const pNdc = p.clone().project(this.camera);
      if (
        pNdc.z > -1 && pNdc.z < 1 &&
        pNdc.x >= -1 && pNdc.x <= gioiHanPhaiNdc &&
        pNdc.y >= -1 && pNdc.y <= 1
      ) {
        trongKhung++;
      }
    }

    return {
      ti_le: Number((trongKhung / cacDinh.length).toFixed(2)),
      tam_la_canvas: tamLaCanvas
    };
  }

  // ---------------------------------------------------------------- vòng đời cảnh
  _donCanh() {
    for (const f of this._donDep || []) f();
    this._donDep = [];
    this.nhom.traverse((o) => { if (o.isCSS2DObject && o.element?.parentNode) o.element.parentNode.removeChild(o.element); });
    this.scene.remove(this.nhom);
    for (const x of this._taiNguyen) x.dispose?.();
    this._taiNguyen.clear();
    this._hoatHinh = [];
    this._chon = [];
    this._vlNhan = [];
    this.nhom = new THREE.Group();
    this.scene.add(this.nhom);
  }
  mo(id, tn, s, giuGoc = false) {
    if (this._banDo && this._tram[id]) {
      this.vaoTram(id);
      if (s) this.capNhat(s);
      return;
    }
    this._donCanh();
    const dung = { "chuan-do": this._dungChuanDo, "an-toan": this._dungAnToan, "nhan-biet-ion": this._dungNhanBiet, "pin-galvani": this._dungPin, "dien-phan": this._dungDienPhan }[id];
    this.ct = { id, tn, s };
    dung.call(this, tn, s);
    if (giuGoc) { this.controls.minDistance = this.ct.goc.gan; this.controls.maxDistance = this.ct.goc.xa; }
    else this._gocNhin(this.ct.goc, false);
    this._doiCo();
    this.capNhat(s);
  }
  capNhat(s) {
    if (!s || !s.id) return;
    const tr = this._tram?.[s.id];
    if (tr && tr.ct) {
      tr.ct.s = s;
      tr.ct.capNhat?.(s);
    } else if (this.ct && s.id === this.ct.id) {
      this.ct.s = s;
      this.ct.capNhat?.(s);
    }
  }
  hieuUng(e) {
    if (this.tramDangLam && this._tram?.[this.tramDangLam]) {
      this._tram[this.tramDangLam].ct?.hieuUng?.(e);
    } else if (this.ct) {
      this.ct.hieuUng?.(e);
    }
  }
  huy() {
    this._donCanh();
    for (const id of Object.keys(this._tram)) {
      this._donTram(id);
    }
    if (this.phong) this.phong.huy();
    if (this.nhanVat) this.nhanVat.huy();
    this._ro?.disconnect();
    this._io?.disconnect();
    document.removeEventListener("visibilitychange", this._anTab);
    this._dangChay = false;
    this.renderer.dispose();
    this.renderer.domElement.remove();
    this.nhan2d.domElement.remove();
  }

  // ---------------------------------------------------------------- dụng cụ dùng chung
  _coc(r, h, vt) {
    const p = profileCoc(r, h);
    const m = this._luoi(thuyTinhMem(HH.DUNG_CU.coc(r, h), this._phan()), this._vl("thuy_tinh"));
    m.position.copy(vt);
    this.nhom.add(m);
    return { m, p, r, h };
  }
  _chatLongTrong(binh, hMuc, rgb, opacity) {
    const vl = this._vl("long", { rgb, opacity });
    const geo = hinhChatLong(binh.p, Math.max(0.006, hMuc));
    const m = this._luoi(geo, vl, { bong: false, nhan: false });
    m.position.copy(binh.m.position);
    this.nhom.add(m);
    return { m, vl, h: hMuc, binh };
  }
  _doiMucLong(cl, hMuc) {
    if (Math.abs(hMuc - cl.h) < 0.0004) return;
    cl.h = hMuc;
    const cu = cl.m.geometry;
    cl.m.geometry = this._theo(hinhChatLong(cl.binh.p, Math.max(0.006, hMuc)));
    cu.dispose();
    this._taiNguyen.delete(cu);
  }
  _doiMauLong(cl, rgb, opacity, nhanh = false) {
    const c = rgbMau(rgb || MAU_NUOC);
    const op = opacity ?? (rgb ? 0.78 : 0.4);
    if (nhanh) { cl.vl.color.copy(c); cl.vl.opacity = op; return; }
    gsap.to(cl.vl.color, { r: c.r, g: c.g, b: c.b, duration: 0.8, ease: "power1.out" });
    gsap.to(cl.vl, { opacity: op, duration: 0.8 });
  }
  _denCon(vt, { lua = true } = {}) {
    const g = new THREE.Group();
    g.position.copy(vt);
    const than = this._luoi(thuyTinhMem(HH.DUNG_CU.denCon(), this._phan()), this._vl("thuy_tinh"));
    const con = this._luoi(new THREE.CylinderGeometry(0.038, 0.04, 0.03, 32), this._vl("long", { rgb: [190, 220, 255], opacity: 0.35 }), { bong: false });
    con.position.y = 0.016;
    const bac = this._luoi(new THREE.CylinderGeometry(0.004, 0.004, 0.02, 8), this._vl("mo", { mau: 0xf5f0e6 }));
    bac.position.y = 0.093;
    g.add(than, con, bac);
    const sprite = this._ngonLua();
    sprite.position.y = 0.135;
    sprite.visible = lua;
    g.add(sprite);
    this.nhom.add(g);
    return { g, lua: sprite };
  }
  _ngonLua(trang = false) {
    const c = document.createElement("canvas");
    c.width = 64; c.height = 128;
    const x = c.getContext("2d");
    const gr = x.createRadialGradient(32, 92, 2, 32, 80, 52);
    if (trang) { gr.addColorStop(0, "rgba(255,255,255,1)"); gr.addColorStop(0.35, "rgba(255,255,255,0.9)"); gr.addColorStop(0.7, "rgba(255,255,255,0.45)"); gr.addColorStop(1, "rgba(255,255,255,0)"); }
    else { gr.addColorStop(0, "rgba(255,255,255,1)"); gr.addColorStop(0.25, "rgba(255,226,140,0.95)"); gr.addColorStop(0.6, "rgba(255,140,40,0.55)"); gr.addColorStop(1, "rgba(255,90,20,0)"); }
    x.fillStyle = gr;
    x.beginPath(); x.moveTo(32, 4); x.bezierCurveTo(56, 52, 58, 112, 32, 124); x.bezierCurveTo(6, 112, 8, 52, 32, 4); x.fill();
    const tex = this._theo(new THREE.CanvasTexture(c));
    tex.colorSpace = THREE.SRGBColorSpace;
    const vl = this._theo(new THREE.SpriteMaterial({ map: tex, blending: THREE.AdditiveBlending, depthWrite: false, transparent: true, color: 0xffffff }));
    const s = new THREE.Sprite(vl);
    s.scale.set(0.05, 0.1, 1);
    const goc = 0.1;
    this._hoatHinh.push((dt, t) => { if (s.visible) { s.scale.y = goc * (0.94 + 0.08 * Math.sin(t * 17) + 0.04 * Math.sin(t * 29)); } });
    return s;
  }
  _hatInstanced(soLuong, r, mau) {
    const geo = new THREE.IcosahedronGeometry(r, 0);
    const vl = this._theo(new THREE.MeshStandardMaterial({ color: mau, roughness: 0.6 }));
    const m = new THREE.InstancedMesh(this._theo(geo), vl, soLuong);
    m.count = 0;
    m.frustumCulled = false;
    this.nhom.add(m);
    return m;
  }
  /** Bọt khí: hạt nhỏ nổi từ đáy lên mặt chất lỏng rồi vỡ (lặp lại) khi `bat` = true. */
  _botKhi({ soLuong, x, z, r, yDay, yMat, kichThuoc = 0.0022 }) {
    const vl = this._theo(new THREE.MeshPhysicalMaterial({ color: 0xffffff, roughness: 0.05, transparent: true, opacity: 0.55, depthWrite: false }));
    const m = new THREE.InstancedMesh(this._theo(new THREE.SphereGeometry(kichThuoc, 8, 6)), vl, soLuong);
    m.frustumCulled = false;
    m.renderOrder = 3;
    m.count = 0;
    this.nhom.add(m);
    const hat = Array.from({ length: soLuong }, () => ({ x: (Math.random() - 0.5) * r, z: (Math.random() - 0.5) * r, y: yDay + Math.random() * (yMat - yDay), v: 0.03 + Math.random() * 0.05, s: 0.6 + Math.random() * 0.8 }));
    const mt = new THREE.Matrix4();
    const bk = { m, bat: false, yMat, yDay, x, z };
    this._hoatHinh.push((dt) => {
      if (!bk.bat && m.count === 0) return;
      let i = 0;
      for (const h of hat) {
        h.y += h.v * dt * 3;
        if (h.y > bk.yMat) { if (!bk.bat) continue; h.y = bk.yDay; }
        mt.makeScale(h.s, h.s, h.s).setPosition(bk.x + h.x, h.y, bk.z + h.z);
        m.setMatrixAt(i++, mt);
      }
      m.count = i;
      m.instanceMatrix.needsUpdate = true;
    });
    return bk;
  }
  _day(diem, mau, ban = 0.0035) {
    const cong = new THREE.CatmullRomCurve3(diem);
    const m = this._luoi(new THREE.TubeGeometry(cong, 64, ban, 8, false), this._vl("nhua", { mau, nham: 0.4 }));
    this.nhom.add(m);
    return { m, cong };
  }
  /** Chấm sáng chạy dọc một đường (dòng electron, ion). */
  _dongHat(cong, { so = 10, mau = 0x38bdf8, r = 0.0045, tocDo = 0.18 } = {}) {
    const vl = this._vl("phat_sang", { mau, cong: true });
    const m = new THREE.InstancedMesh(this._theo(new THREE.SphereGeometry(r, 10, 8)), vl, so);
    m.frustumCulled = false;
    this.nhom.add(m);
    const dh = { m, bat: true, tocDo, nguoc: false, pha: 0 };
    const mt = new THREE.Matrix4();
    this._hoatHinh.push((dt) => {
      m.visible = dh.bat;
      if (!dh.bat) return;
      dh.pha = (dh.pha + dt * dh.tocDo) % 1;
      for (let i = 0; i < so; i++) {
        let u = (i / so + dh.pha) % 1;
        if (dh.nguoc) u = 1 - u;
        m.setMatrixAt(i, mt.makeTranslation(...cong.getPointAt(u).toArray()));
      }
      m.instanceMatrix.needsUpdate = true;
    });
    return dh;
  }
  _giaDo(vt, cao = 0.95) {
    const vlKl = this._vl("kim_loai", { rgb: [150, 156, 166], nham: 0.35 });
    const de = this._luoi(new RoundedBoxGeometry(0.3, 0.02, 0.2, 3, 0.008), this._vl("nhua", { mau: 0x2b3440, nham: 0.6 }));
    de.position.set(vt.x, 0.01, vt.z);
    const coc = this._luoi(new THREE.CylinderGeometry(0.007, 0.007, cao, 16), vlKl);
    coc.position.set(vt.x - 0.1, cao / 2 + 0.02, vt.z - 0.05);
    this.nhom.add(de, coc);
    return { de, coc, vlKl };
  }

  // ================================================================ 1. CHUẨN ĐỘ
  _dungChuanDo(tn, s) {
    const ct = this.ct;
    const X = 0.0;
    const gia = this._giaDo(V3(X, 0, 0));
    // kẹp burette
    const kep = this._luoi(new RoundedBoxGeometry(0.12, 0.018, 0.022, 2, 0.006), gia.vlKl);
    kep.position.set(X - 0.05, 0.74, -0.05);
    const nutKep = this._luoi(new THREE.SphereGeometry(0.012, 16, 12), this._vl("nhua", { mau: 0x0f2b48 }));
    nutKep.position.set(X - 0.1, 0.74, -0.03);
    this._vlNhan.push(nutKep.material);
    nutKep.material.color.copy(this.mauNhan);
    this.nhom.add(kep, nutKep);

    // burette: ống thuỷ tinh + vạch chia + dung dịch NaOH + khoá + đầu nhỏ giọt
    const bx = X + 0.02, bz = -0.05, yDuoi = 0.34, yVach0 = 0.86, yVach50 = 0.38, yTren = 0.93, rB = 0.0085;
    const ong = this._luoi(new THREE.CylinderGeometry(rB, rB, yTren - yDuoi, 32, 1, true), this._vl("thuy_tinh"));
    ong.position.set(bx, (yTren + yDuoi) / 2, bz);
    this.nhom.add(ong);
    const mh = this._vachBuret();
    const vach = this._luoi(new THREE.CylinderGeometry(rB + 0.0006, rB + 0.0006, yVach0 - yVach50, 32, 1, true, -Math.PI * 0.35, Math.PI * 0.7),
      this._vl("mo", { map: mh, trongSuot: true, alphaTest: 0.35, mau: 0xffffff }), { bong: false });
    vach.position.set(bx, (yVach0 + yVach50) / 2, bz);
    vach.rotation.y = Math.PI / 2 + 0.6;
    this.nhom.add(vach);
    const vlNaOH = this._vl("long", { rgb: MAU_NUOC, opacity: 0.5 });
    const cotNaOH = this._luoi(new THREE.CylinderGeometry(rB - 0.0018, rB - 0.0018, 1, 24), vlNaOH, { bong: false });
    this.nhom.add(cotNaOH);
    const khoa = this._luoi(new THREE.CylinderGeometry(0.0055, 0.0055, 0.03, 16), this._vl("nhua", { mau: 0x1e293b }));
    khoa.rotation.z = Math.PI / 2;
    khoa.position.set(bx, yDuoi - 0.01, bz);
    const tay = this._luoi(new RoundedBoxGeometry(0.008, 0.032, 0.005, 2, 0.002), this._vl("nhua", { mau: 0x0f2b48 }));
    tay.position.set(bx + 0.02, yDuoi - 0.01, bz);
    this._vlNhan.push(tay.material);
    tay.material.color.copy(this.mauNhan);
    khoa.userData.lenh = "bat_tat_khoa";
    tay.userData.lenh = "bat_tat_khoa";
    const dau = this._luoi(new THREE.CylinderGeometry(0.0022, 0.005, 0.05, 16), this._vl("thuy_tinh"));
    dau.position.set(bx, yDuoi - 0.045, bz);
    this.nhom.add(khoa, tay, dau);
    this._chon.push(khoa, tay);

    // bình tam giác trên tờ giấy trắng
    const giay = this._luoi(new THREE.PlaneGeometry(0.16, 0.12), this._vl("mo", { mau: 0xffffff, nham: 0.95 }), { bong: false });
    giay.rotation.x = -Math.PI / 2;
    giay.position.set(bx, 0.0205, bz);
    this.nhom.add(giay);
    const pBinh = profileBinhTamGiac(0.05, 0.15, 0.016, 0.1);
    const binhM = this._luoi(thuyTinhMem(HH.DUNG_CU.binhTamGiac(0.05, 0.15, 0.016, 0.1), this._phan()), this._vl("thuy_tinh"));
    const goc = new THREE.Group();
    goc.position.set(bx, 0.021, bz);
    this.nhom.add(goc);
    goc.add(binhM);
    binhM.userData.lenh = "lac";
    this._chon.push(binhM);
    const binh = { m: binhM, p: pBinh };
    const vlDd = this._vl("long", { rgb: MAU_NUOC, opacity: 0.42 });
    const ddM = this._luoi(hinhChatLong(pBinh, 0.03), vlDd, { bong: false, nhan: false });
    goc.add(ddM);
    const dd = { m: ddM, vl: vlDd, h: 0.03, binh };
    const doiMuc = (mL) => {
      const h = hDeCoTheTich(pBinh, mL, HH.DAY_KINH, 0.12);
      if (Math.abs(h - dd.h) < 0.0004) return;
      dd.h = h;
      const cu = ddM.geometry;
      ddM.geometry = this._theo(hinhChatLong(pBinh, h));
      cu.dispose(); this._taiNguyen.delete(cu);
    };
    // giọt, dòng chảy, vệt màu
    const giot = this._luoi(new THREE.SphereGeometry(0.003, 12, 8), this._vl("long", { rgb: [200, 225, 245], opacity: 0.85 }), { bong: false });
    giot.visible = false;
    const dong = this._luoi(new THREE.CylinderGeometry(0.0012, 0.0012, 1, 8), this._vl("long", { rgb: [200, 225, 245], opacity: 0.7 }), { bong: false });
    dong.visible = false;
    const vlVet = this._vl("phat_sang", { mau: 0xe83e8c, opacity: 0 });
    const vet = this._luoi(new THREE.CircleGeometry(0.012, 24), vlVet, { bong: false, nhan: false });
    vet.rotation.x = -Math.PI / 2;
    this.nhom.add(giot, dong, vet);

    this._nhan("Burette – NaOH 0,100 M", V3(bx + 0.06, 0.8, bz), "nho");
    const nhanBinh = this._nhan("Bình tam giác", V3(bx + 0.085, 0.09, bz + 0.02), "nho");
    const nhanKhoa = this._nhan("Khoá đóng", V3(bx + 0.06, yDuoi - 0.012, bz), "nho");
    ct.goc = { vi_tri: V3(0.08, 0.85, 3.2), dich: V3(bx, 0.42, bz), gan: 0.25, xa: 5.0, rong: 3.2, cao: 1.45 };

    const yMatDd = () => goc.position.y + dd.h;
    ct.capNhat = (st) => {
      const V = st.V;
      const muc = yVach0 - (V / st.V_buret) * (yVach0 - yVach50);
      const hCot = Math.max(0.001, muc - (yDuoi - 0.005));
      cotNaOH.scale.y = hCot;
      cotNaOH.position.set(bx, yDuoi - 0.005 + hCot / 2, bz);
      doiMuc(st.the_tich_dd);
      const a = st.mau.rgba;
      const coMau = a[3] > 0.02;
      const rgb = coMau ? MAU_NUOC.map((c, i) => c + (a[i] - c) * Math.min(1, a[3] + 0.15)) : MAU_NUOC;
      this._doiMauLong(dd, rgb, coMau ? 0.45 + a[3] * 0.4 : 0.42);
      tay.rotation.z = st.khoa === "dong" ? 0 : Math.PI / 2;
      nhanKhoa.element.textContent = st.khoa === "dong" ? "Khoá đóng" : st.khoa === "nhanh" ? "Chảy nhanh" : "Nhỏ từng giọt";
      nhanBinh.element.textContent = `${st.acid.cong_thuc} + ${st.ten_chi_thi || "chưa có chỉ thị"}`;
      const yTip = yDuoi - 0.07;
      if (st.khoa === "nhanh") {
        const h = yTip - yMatDd();
        dong.visible = true;
        dong.scale.y = Math.max(0.001, h);
        dong.position.set(bx, yMatDd() + h / 2, bz);
      } else dong.visible = false;
      if (!st.vet_mau && vlVet.opacity > 0) gsap.to(vlVet, { opacity: 0, duration: 0.5 });
    };
    ct.hieuUng = (e) => {
      if (e.loai === "giot") {
        giot.visible = true;
        giot.position.set(bx, yDuoi - 0.075, bz);
        gsap.killTweensOf(giot.position);
        gsap.to(giot.position, { y: yMatDd() + 0.003, duration: 0.32, ease: "power2.in", onComplete: () => { giot.visible = false; } });
      } else if (e.loai === "lac") {
        gsap.fromTo(goc.rotation, { z: 0 }, { z: 0.08, duration: 0.12, yoyo: true, repeat: 5, ease: "sine.inOut", onComplete: () => { goc.rotation.z = 0; } });
        gsap.fromTo(goc.position, { x: bx }, { x: bx + 0.006, duration: 0.12, yoyo: true, repeat: 5, ease: "sine.inOut", onComplete: () => { goc.position.x = bx; } });
      } else if (e.loai === "vet_mau") {
        const st = ct.s;
        if (st?.chi_thi === "methyl_orange") vlVet.color.set(0xf1c40f); else vlVet.color.set(0xe83e8c);
        vet.position.set(bx + 0.004, yMatDd() + 0.0035, bz);
        gsap.fromTo(vlVet, { opacity: 0.85 }, { opacity: 0, duration: 2.4, ease: "power1.in" });
        gsap.fromTo(vet.scale, { x: 0.4, y: 0.4 }, { x: 1.5, y: 1.5, duration: 1.4 });
      } else if (e.loai === "doi_mau") {
        gsap.fromTo(goc.scale, { x: 1, y: 1, z: 1 }, { x: 1.03, y: 1.03, z: 1.03, duration: 0.18, yoyo: true, repeat: 1 });
      }
    };
  }
  _vachBuret() {
    const c = document.createElement("canvas");
    c.width = 128; c.height = 2048;
    const g = c.getContext("2d");
    g.clearRect(0, 0, c.width, c.height);
    g.fillStyle = "#1b2b3d";
    g.font = '600 30px "JetBrains Mono", monospace';
    for (let v = 0; v <= 50 + 1e-9; v += 0.5) {
      const y = 12 + ((c.height - 24) * v) / 50;
      const dai = v % 5 === 0 ? 60 : v % 1 === 0 ? 40 : 24;
      g.fillRect(0, y - 1.5, dai, 3);
      if (v % 5 === 0) g.fillText(String(v), 66, y + 10);
    }
    const tex = this._theo(new THREE.CanvasTexture(c));
    tex.colorSpace = THREE.SRGBColorSpace;
    tex.anisotropy = 4;
    return tex;
  }

  // ================================================================ 2. AN TOÀN PHÒNG THÍ NGHIỆM – bàn "tìm lỗi"
  _dungAnToan(tn, s) {
    const ct = this.ct;
    const vt = {};
    const nguoi = (x, z, mauAo, nghieng = 0) => {
      const g = new THREE.Group();
      g.position.set(x, 0, z);
      const than = this._luoi(new THREE.CapsuleGeometry(0.075, 0.22, 6, 16), this._vl("nhua", { mau: mauAo, nham: 0.8 }));
      than.position.y = 0.2;
      const dau = this._luoi(new THREE.SphereGeometry(0.06, 24, 18), this._vl("nhua", { mau: 0xf1c9a5, nham: 0.7 }));
      dau.position.set(0, 0.42, 0);
      g.add(than, dau);
      g.rotation.x = nghieng;
      this.nhom.add(g);
      return { g, dau };
    };
    const lo = (x, z, mau, coNhan, chu) => {
      const m = this._luoi(thuyTinhMem(HH.DUNG_CU.loThuoc(0.03, 0.12, 0.0135), this._phan()), this._vl("thuy_tinh"));
      m.position.set(x, 0, z);
      const dd = this._luoi(new THREE.CylinderGeometry(0.026, 0.026, 0.06, 24), this._vl("long", { rgb: mau, opacity: 0.75 }), { bong: false });
      dd.position.set(x, 0.035, z);
      const nap = this._luoi(new THREE.CylinderGeometry(0.015, 0.015, 0.018, 16), this._vl("nhua", { mau: 0x2b3440 }));
      nap.position.set(x, 0.128, z);
      this.nhom.add(m, dd, nap);
      if (coNhan) {
        const nh = this._luoi(new THREE.CylinderGeometry(0.0305, 0.0305, 0.03, 24, 1, true), this._vl("mo", { mau: 0xffffff }), { bong: false });
        nh.position.set(x, 0.05, z);
        this.nhom.add(nh);
        if (chu) this._nhan(chu, V3(x, 0.15, z), "nho");
      }
    };
    // dãy lọ hoá chất: một lọ KHÔNG nhãn
    lo(-0.95, -0.35, [230, 240, 250], true, "NaCl");
    lo(-0.86, -0.35, [70, 150, 230], true, "CuSO₄");
    lo(-0.77, -0.35, [240, 220, 120], false);
    vt.lo_khong_nhan = V3(-0.77, 0.2, -0.35);
    // kính bảo hộ để trên bàn (không đeo)
    const kinh = new THREE.Group();
    const vlGong = this._vl("nhua", { mau: 0x0f2b48, nham: 0.4 });
    this._vlNhan.push(vlGong);
    vlGong.color.copy(this.mauNhan);
    for (const sx of [-1, 1]) {
      const vien = this._luoi(new THREE.TorusGeometry(0.03, 0.006, 10, 28), vlGong);
      vien.position.x = sx * 0.034;
      const mat = this._luoi(new THREE.CircleGeometry(0.03, 24), this._vl("thuy_tinh"), { bong: false });
      mat.position.x = sx * 0.034;
      kinh.add(vien, mat);
    }
    kinh.rotation.x = -Math.PI / 2.2;
    kinh.position.set(-0.55, 0.012, 0.25);
    this.nhom.add(kinh);
    vt.khong_kinh = V3(-0.55, 0.09, 0.25);
    // cốc H₂SO₄ đặc + bình nước nghiêng đổ VÀO acid
    const cocAcid = this._coc(0.04, 0.09, V3(-0.3, 0, -0.05));
    this._chatLongTrong(cocAcid, 0.045, [245, 240, 225], 0.55);
    this._nhan("H₂SO₄ đặc", V3(-0.3, 0.13, -0.05), "nho");
    const binhNuoc = this._coc(0.032, 0.1, V3(0, 0, 0));
    const gBinh = new THREE.Group();
    gBinh.add(binhNuoc.m);
    binhNuoc.m.position.set(0, 0, 0);
    const nuoc = this._luoi(hinhChatLong(binhNuoc.p, 0.05), this._vl("long", { rgb: MAU_NUOC, opacity: 0.45 }), { bong: false });
    gBinh.add(nuoc);
    gBinh.position.set(-0.24, 0.14, -0.05);
    gBinh.rotation.z = 1.05;
    this.nhom.add(gBinh);
    const dongNuoc = this._luoi(new THREE.CylinderGeometry(0.002, 0.002, 0.06, 8), this._vl("long", { rgb: MAU_NUOC, opacity: 0.6 }), { bong: false });
    dongNuoc.position.set(-0.29, 0.11, -0.05);
    this.nhom.add(dongNuoc);
    vt.do_nuoc_vao_acid = V3(-0.27, 0.24, -0.05);
    // giá ống nghiệm + bạn học sinh cúi ngửi trực tiếp
    const gia = this._luoi(new RoundedBoxGeometry(0.2, 0.012, 0.06, 2, 0.004), this._vl("nhua", { mau: 0xd6c3a5, nham: 0.8 }));
    gia.position.set(0.12, 0.06, -0.2);
    this.nhom.add(gia);
    for (let i = 0; i < 4; i++) {
      const o = this._luoi(thuyTinhMem(HH.DUNG_CU.ongNghiem(0.009, 0.12), this._phan()), this._vl("thuy_tinh"));
      o.position.set(0.05 + i * 0.045, 0.01, -0.2);
      this.nhom.add(o);
    }
    nguoi(0.13, -0.42, 0x5b7fa6, 0.32);
    vt.ngui_truc_tiep = V3(0.13, 0.42, -0.36);
    // ống nghiệm đun trên đèn cồn, miệng ống hướng vào bạn khác
    const den = this._denCon(V3(0.45, 0, 0.05));
    const ongDun = this._luoi(thuyTinhMem(HH.DUNG_CU.ongNghiem(0.01, 0.13), this._phan()), this._vl("thuy_tinh"));
    ongDun.position.set(0.45, 0.16, 0.05);
    ongDun.rotation.z = -1.0;
    ongDun.rotation.y = -0.5;
    this.nhom.add(ongDun);
    nguoi(0.72, 0.35, 0x8a6db1);
    vt.huong_mieng_ong = V3(0.5, 0.3, 0.05);
    // cốc nước + bánh mì
    const ly = this._luoi(new THREE.CylinderGeometry(0.03, 0.025, 0.08, 24), this._vl("nhua", { mau: 0xf97316, nham: 0.5 }));
    ly.position.set(0.82, 0.04, -0.25);
    const banh = this._luoi(new RoundedBoxGeometry(0.1, 0.03, 0.06, 3, 0.012), this._vl("nhua", { mau: 0xd9a35b, nham: 0.9 }));
    banh.position.set(0.93, 0.016, -0.22);
    this.nhom.add(ly, banh);
    vt.an_uong = V3(0.87, 0.13, -0.23);
    // đĩa bột hoá chất + bàn tay trần
    const dia = this._luoi(new THREE.CylinderGeometry(0.06, 0.05, 0.01, 32), this._vl("mo", { mau: 0xf8fafc, nham: 0.5 }));
    dia.position.set(-0.12, 0.005, 0.32);
    const bot = this._luoi(new THREE.ConeGeometry(0.035, 0.025, 24), this._vl("mo", { mau: 0x4f9ad8 }));
    bot.position.set(-0.12, 0.022, 0.32);
    const ban = new THREE.Group();
    const vlDa = this._vl("nhua", { mau: 0xf1c9a5, nham: 0.7 });
    const long = this._luoi(new RoundedBoxGeometry(0.05, 0.016, 0.06, 2, 0.007), vlDa);
    ban.add(long);
    for (let i = 0; i < 4; i++) {
      const ngon = this._luoi(new THREE.CapsuleGeometry(0.006, 0.03, 4, 8), vlDa);
      ngon.rotation.x = Math.PI / 2;
      ngon.position.set(-0.018 + i * 0.012, 0, 0.045);
      ban.add(ngon);
    }
    ban.position.set(-0.12, 0.05, 0.24);
    ban.rotation.x = 0.4;
    this.nhom.add(dia, bot, ban);
    vt.cam_tay = V3(-0.12, 0.12, 0.3);
    // đèn cồn cuối bàn cháy, không ai trông
    this._denCon(V3(1.05, 0, 0.3));
    vt.den_khong_nguoi = V3(1.05, 0.22, 0.3);
    // việc làm ĐÚNG (bẫy): tủ hút, bình chữa cháy, thùng chất thải, hộp găng tay
    const tu = this._luoi(new RoundedBoxGeometry(0.42, 0.5, 0.3, 3, 0.02), this._vl("nhua", { mau: 0xdfe6ee, nham: 0.6 }));
    tu.position.set(-1.0, 0.25, 0.25);
    const kinhTu = this._luoi(new THREE.PlaneGeometry(0.36, 0.3), this._vl("thuy_tinh"), { bong: false });
    kinhTu.position.set(-1.0, 0.28, 0.401);
    this.nhom.add(tu, kinhTu);
    vt.tu_hut = V3(-1.0, 0.56, 0.3);
    const binhCh = this._luoi(new THREE.CapsuleGeometry(0.04, 0.16, 6, 16), this._vl("nhua", { mau: 0xdc2626, nham: 0.35 }));
    binhCh.position.set(1.2, 0.12, -0.38);
    this.nhom.add(binhCh);
    vt.binh_cuu_hoa = V3(1.2, 0.3, -0.38);
    const thung = this._luoi(new THREE.CylinderGeometry(0.07, 0.06, 0.16, 24), this._vl("nhua", { mau: 0xeab308, nham: 0.5 }));
    thung.position.set(0.55, 0.08, -0.42);
    this.nhom.add(thung);
    vt.thung_thai = V3(0.55, 0.22, -0.42);
    const hop = this._luoi(new RoundedBoxGeometry(0.12, 0.06, 0.08, 2, 0.008), this._vl("nhua", { mau: 0x60a5fa, nham: 0.5 }));
    hop.position.set(-0.6, 0.03, -0.3);
    this.nhom.add(hop);
    vt.gang_tay = V3(-0.6, 0.12, -0.3);

    const nut = {};
    for (const d of s.diem_nong) {
      const o = this._nutNhan(String(d.so), vt[d.id] || V3(0, 0.2, 0), "ptn-diem-nong", () => this.onThaoTac("bam_diem", { id: d.id }), `Vị trí ${d.so}: ${d.vi_tri}`);
      nut[d.id] = o.element;
    }
    ct.goc = { vi_tri: V3(0.1, 1.45, 1.95), dich: V3(0.08, 0.05, 0), gan: 0.6, xa: 4.5, rong: 2.6 };
    ct.capNhat = (st) => {
      for (const d of st.diem_nong) {
        const b = nut[d.id];
        if (!b) continue;
        b.classList.toggle("da-tim", d.da_tim);
        b.classList.toggle("viec-dung", d.la_viec_dung);
        b.textContent = d.da_tim ? "✓" : String(d.so);
        b.setAttribute("aria-label", `Vị trí ${d.so}: ${d.vi_tri}${d.da_tim ? " – đã tìm ra lỗi" : d.la_viec_dung ? " – việc làm đúng" : ""}`);
      }
    };
    ct.hieuUng = (e) => {
      if (e.loai === "tim_loi" && nut[e.id]) gsap.fromTo(nut[e.id], { scale: 1.6 }, { scale: 1, duration: 0.5, ease: "back.out(3)" });
    };
  }

  // ================================================================ 3. NHẬN BIẾT ION
  _dungNhanBiet(tn, s) {
    const ct = this.ct;
    const n = s.ong.length, cach = 0.075, x0 = -((n - 1) * cach) / 2;
    const gia = this._luoi(new RoundedBoxGeometry(n * cach + 0.08, 0.014, 0.08, 2, 0.005), this._vl("nhua", { mau: 0xd6c3a5, nham: 0.85 }));
    gia.position.set(0, 0.075, 0);
    const chan1 = this._luoi(new RoundedBoxGeometry(0.012, 0.075, 0.07, 2, 0.003), gia.material);
    chan1.position.set(x0 - 0.05, 0.037, 0);
    const chan2 = chan1.clone();
    chan2.position.x = -x0 + 0.05;
    this.nhom.add(gia, chan1, chan2);
    const pOng = profileOngNghiem(0.0115, 0.15);
    const ongs = s.ong.map((o, i) => {
      const x = x0 + i * cach;
      const g = new THREE.Group();
      g.position.set(x, 0.012, 0);
      const m = this._luoi(thuyTinhMem(HH.DUNG_CU.ongNghiem(0.0115, 0.15), this._phan()), this._vl("thuy_tinh"));
      m.userData.chon = { loai: "ong", so: o.so };
      g.add(m);
      this._chon.push(m);
      const vlDd = this._vl("long", { rgb: MAU_NUOC, opacity: 0.42 });
      const dd = this._luoi(hinhChatLong(pOng, 0.075, HH.DAY_KINH, 32), vlDd, { bong: false, nhan: false });
      g.add(dd);
      this.nhom.add(g);
      const soHat = this.chatLuong === "cao" ? 70 : 30;
      const kt = this._hatInstanced(soHat, 0.0022, 0xffffff);
      g.add(kt);
      const hat = Array.from({ length: soHat }, (_, k) => {
        const a = Math.random() * Math.PI * 2, rr = Math.sqrt(Math.random()) * 0.008;
        return { x: Math.cos(a) * rr, z: Math.sin(a) * rr, yDay: 0.004 + Math.random() * 0.007 + (k / soHat) * 0.006, y: 0.004 };
      });
      const bot = this._botKhi({ soLuong: this.chatLuong === "cao" ? 26 : 12, x: 0, z: 0, r: 0.012, yDay: 0.018, yMat: 0.076 });
      g.add(bot.m);
      const nutSo = this._nutNhan(String(o.so), V3(x, 0.215, 0), "ptn-so-ong", () => this.onChon({ loai: "ong", so: o.so }), `Ống nghiệm ${o.so}`);
      const quy = this._luoi(new THREE.PlaneGeometry(0.008, 0.05), this._vl("mo", { mau: 0x8b5cf6 }), { bong: false });
      quy.position.set(0.004, 0.178, 0.012);
      quy.visible = false;
      g.add(quy);
      return { o, g, dd, vl: vlDd, kt, hat, bot, nutSo, quy, x, luong: 0 };
    });
    // lọ thuốc thử phía sau
    s.thuoc_thu.forEach((t, i) => {
      const x = -0.24 + i * 0.08;
      const m = this._luoi(thuyTinhMem(HH.DUNG_CU.loThuoc(0.022, 0.085, 0.0085), this._phan()), this._vl("thuy_tinh"));
      m.position.set(x, 0, -0.16);
      const dd = this._luoi(new THREE.CylinderGeometry(0.019, 0.019, 0.045, 20), this._vl("long", { rgb: MAU_NUOC, opacity: 0.45 }), { bong: false });
      dd.position.set(x, 0.026, -0.16);
      const bop = this._luoi(new THREE.SphereGeometry(0.011, 16, 12), this._vl("nhua", { mau: 0x1e293b }));
      bop.position.set(x, 0.1, -0.16);
      this.nhom.add(m, dd, bop);
      this._nhan(t.ten, V3(x, 0.135, -0.16), "nho");
    });
    // đèn cồn + dây platinum (thử màu ngọn lửa)
    const den = this._denCon(V3(0.3, 0, 0.1), { lua: true });
    const luaMau = this._ngonLua(true);
    luaMau.material.blending = THREE.NormalBlending;
    luaMau.material.opacity = 0.92;
    luaMau.position.set(0.3, 0.142, 0.1);
    luaMau.scale.set(0.06, 0.12, 1);
    luaMau.visible = false;
    this.nhom.add(luaMau);
    const day = this._day([V3(0.46, 0.2, 0.16), V3(0.38, 0.17, 0.13), V3(0.315, 0.14, 0.105)], 0xc8ccd2, 0.0012);
    const vongDay = this._luoi(new THREE.TorusGeometry(0.004, 0.0009, 6, 16), this._vl("kim_loai", { rgb: [210, 210, 215] }));
    vongDay.position.set(0.312, 0.138, 0.104);
    this.nhom.add(vongDay);
    day.m.visible = vongDay.visible = false;
    const nhanLua = this._nhan("", V3(0.3, 0.215, 0.1), "nhan-manh");
    nhanLua.visible = false;
    ct.goc = { vi_tri: V3(0.05, 0.75, 2.5), dich: V3(0.03, 0.25, -0.03), gan: 0.2, xa: 4.5, rong: 2.5, cao: 1.2 };
    const nhanGoc = ct.goc;
    const gocLua = { vi_tri: V3(0.48, 0.28, 0.46), dich: V3(0.3, 0.13, 0.1), gan: 0.2, xa: 1.4, rong: 0.3 };

    let chon = null;
    const vongChon = this._luoi(new THREE.TorusGeometry(0.018, 0.0018, 8, 32), this._vl("phat_sang", { mau: 0x38bdf8 }), { bong: false });
    vongChon.rotation.x = Math.PI / 2;
    vongChon.visible = false;
    this._vlNhan.push(vongChon.material);
    vongChon.material.color.copy(this.mauNhan);
    this.nhom.add(vongChon);
    const mt = new THREE.Matrix4();
    this._hoatHinh.push((dt) => {
      for (const ong of ongs) {
        let doi = false;
        for (let k = 0; k < ong.kt.count; k++) {
          const h = ong.hat[k];
          if (h.y > h.yDay) { h.y = Math.max(h.yDay, h.y - dt * (0.03 + (k % 7) * 0.004)); doi = true; }
          ong.kt.setMatrixAt(k, mt.makeTranslation(h.x, h.y, h.z));
        }
        if (doi || ong._moi) { ong.kt.instanceMatrix.needsUpdate = true; ong._moi = false; }
      }
      if (vongChon.visible) vongChon.rotation.z += dt * 0.8;
    });
    let tabTruoc = s.tab;
    ct.capNhat = (st) => {
      if (st.ong.length !== ongs.length) {
        if (this._tram && this._tram[ct.id]) this._dungLaiTram(ct.id, st);
        else this.mo(ct.id, ct.tn, st, true);
        return;
      }
      st.ong.forEach((o, i) => {
        const ong = ongs[i];
        if (!ong) return;
        if (o.mau_dd) this._doiMauLong(ong, o.mau_dd.rgb, 0.72); else this._doiMauLong(ong, null, 0.42);
        const muon = o.ket_tua ? Math.round(o.ket_tua.luong * ong.hat.length) : 0;
        if (o.ket_tua) ong.kt.material.color.copy(rgbMau(o.ket_tua.rgb));
        if (muon > ong.kt.count) for (let k = ong.kt.count; k < muon; k++) ong.hat[k].y = 0.058 + Math.random() * 0.01;
        ong.kt.count = muon;
        ong._moi = true;
        ong.bot.bat = !!o.khi;
        ong.quy.visible = !!o.quy;
        if (o.quy) ong.quy.material.color.set(o.quy === "xanh" ? 0x2563eb : o.quy === "đỏ" ? 0xdc2626 : 0x8b5cf6);
        ong.nutSo.element.textContent = st.ket_qua ? `${o.so}·${o.ten_ion}` : String(o.so);
      });
      const lua = st.ngon_lua;
      den.lua.visible = !lua;
      luaMau.visible = !!lua;
      if (lua) luaMau.material.color.copy(rgbMau(lua.rgb));
      day.m.visible = vongDay.visible = !!lua;
      nhanLua.visible = !!lua;
      if (lua) nhanLua.element.textContent = `Ngọn lửa ${lua.mau}`;
      if (st.tab !== tabTruoc) {
        tabTruoc = st.tab;
        ct.goc = st.tab === "ngon_lua" ? gocLua : nhanGoc;
        if (this.tramDangLam === ct.id) this._gocNhin(ct.goc);
      }
    };
    ct.chonOng = (so) => {
      chon = ongs.find((x) => x.o.so === so) || null;
      vongChon.visible = !!chon;
      ongs.forEach((x) => { x.nutSo.element.classList.toggle("chon", x === chon); gsap.to(x.g.position, { y: x === chon ? 0.03 : 0.012, duration: 0.3 }); });
      if (chon) vongChon.position.set(chon.x, 0.0835, 0);
    };
    ct.hieuUng = (e) => {
      const ong = ongs.find((x) => x.o.so === e.so);
      if (e.loai === "nho" && ong) {
        const g = this._luoi(new THREE.SphereGeometry(0.0028, 10, 8), this._vl("long", { rgb: [210, 230, 250], opacity: 0.85 }), { bong: false });
        g.position.set(ong.x, 0.26, 0);
        this.nhom.add(g);
        gsap.to(g.position, { y: 0.1, duration: 0.35, ease: "power2.in", onComplete: () => this.nhom.remove(g) });
      }
    };
  }

  // ================================================================ 4. PIN GALVANI
  _dungPin(tn, s) {
    const ct = this.ct;
    const xT = -0.17, xP = 0.17;
    const cocT = this._coc(0.07, 0.14, V3(xT, 0, 0)), cocP = this._coc(0.07, 0.14, V3(xP, 0, 0));
    const ddT = this._chatLongTrong(cocT, 0.1, null, 0.42), ddP = this._chatLongTrong(cocP, 0.1, null, 0.42);
    const dienCuc = (x) => {
      const g = new THREE.Group();
      const vl = this._vl("kim_loai", { rgb: [180, 180, 186] });
      const thanh = this._luoi(new RoundedBoxGeometry(0.035, 0.2, 0.006, 2, 0.002), vl);
      thanh.position.y = 0.12;
      const vlPhu = this._vl("kim_loai", { rgb: [196, 112, 64], nham: 0.6 });
      const phu = this._luoi(new RoundedBoxGeometry(0.037, 0.09, 0.008, 2, 0.002), vlPhu);
      phu.position.y = 0.065;
      phu.scale.set(1, 1, 0.01);
      phu.visible = false;
      g.add(thanh, phu);
      g.position.set(x - 0.02, 0, 0);
      this.nhom.add(g);
      return { g, thanh, vl, phu, vlPhu };
    };
    const dcT = dienCuc(xT), dcP = dienCuc(xP);
    // cầu muối chữ U
    const u = new THREE.CatmullRomCurve3([V3(xT + 0.03, 0.06, 0), V3(xT + 0.03, 0.2, 0), V3(-0.06, 0.25, 0), V3(0.06, 0.25, 0), V3(xP - 0.03 + 0.0, 0.2, 0), V3(xP - 0.03, 0.06, 0)]);
    const cau = new THREE.Group();
    const ongU = this._luoi(new THREE.TubeGeometry(u, 64, 0.014, 16, false), this._vl("thuy_tinh"));
    const ruotU = this._luoi(new THREE.TubeGeometry(u, 64, 0.0105, 12, false), this._vl("long", { rgb: [240, 236, 226], opacity: 0.55 }), { bong: false });
    cau.add(ongU, ruotU);
    this.nhom.add(cau);
    const ionK = this._dongHat(u, { so: 6, mau: 0xa78bfa, r: 0.0035, tocDo: 0.12 });
    const ionNO3 = this._dongHat(u, { so: 6, mau: 0xfb923c, r: 0.0035, tocDo: 0.12 });
    ionNO3.pha = 0.5;
    // vôn kế + dây
    const von = this._luoi(new RoundedBoxGeometry(0.16, 0.1, 0.05, 3, 0.012), this._vl("nhua", { mau: 0x1e293b, nham: 0.45 }));
    von.position.set(0, 0.5, -0.02);
    const mh = this._manHinh(256, 96);
    const manHinh = this._luoi(new THREE.PlaneGeometry(0.12, 0.045), new THREE.MeshBasicMaterial({ map: mh.tex, toneMapped: false }), { bong: false });
    this._theo(manHinh.material);
    manHinh.position.set(0, 0.505, 0.0255);
    this.nhom.add(von, manHinh);
    this._nhan("Vôn kế", V3(0, 0.575, 0), "nho");
    const dayT = this._day([V3(xT - 0.02, 0.22, 0), V3(xT - 0.02, 0.36, 0), V3(-0.12, 0.5, 0), V3(-0.08, 0.5, -0.02)], 0x1f2937);
    const dayP = this._day([V3(xP - 0.02, 0.22, 0), V3(xP - 0.02, 0.36, 0), V3(0.12, 0.5, 0), V3(0.08, 0.5, -0.02)], 0xb91c1c);
    const duongE = new THREE.CatmullRomCurve3([V3(xT - 0.02, 0.22, 0.006), V3(xT - 0.02, 0.36, 0.006), V3(-0.12, 0.5, 0.008), V3(0, 0.56, 0.03), V3(0.12, 0.5, 0.008), V3(xP - 0.02, 0.36, 0.006), V3(xP - 0.02, 0.22, 0.006)]);
    const e = this._dongHat(duongE, { so: 14, mau: 0x38bdf8, r: 0.004, tocDo: 0.16 });
    this._vlNhan.push(e.m.material);
    e.m.material.color.copy(this.mauNhan).lerp(new THREE.Color(0x7dd3fc), 0.4);
    const nhanT = this._nhan("", V3(xT, 0.3, 0.06), "nhan-manh"), nhanP = this._nhan("", V3(xP, 0.3, 0.06), "nhan-manh");
    const nhanDdT = this._nhan("", V3(xT, 0.02, 0.09), "nho"), nhanDdP = this._nhan("", V3(xP, 0.02, 0.09), "nho");
    const nhanCau = this._nhan("Cầu muối KNO₃", V3(0, 0.29, 0), "nho");
    const nhanE = this._nhan("e⁻ →", V3(0, 0.62, 0), "nho");
    cau.userData.lenh = "doi_cau_muoi";
    ongU.userData.lenh = "doi_cau_muoi";
    this._chon.push(ongU);
    ct.goc = { vi_tri: V3(0.05, 0.75, 2.5), dich: V3(0, 0.25, 0), gan: 0.35, xa: 4.5, rong: 2.5, cao: 1.2 };
    let cauTruoc = true;
    ct.capNhat = (st) => {
      dcT.vl.color.copy(rgbMau(st.mau_kim_loai.trai));
      dcP.vl.color.copy(rgbMau(st.mau_kim_loai.phai));
      this._doiMauLong(ddT, st.mau_dd.trai, st.mau_dd.trai ? 0.6 : 0.42);
      this._doiMauLong(ddP, st.mau_dd.phai, st.mau_dd.phai ? 0.6 : 0.42);
      this._veManHinh(mh, `${st.E_pin.toFixed(3).replace(".", ",")} V`);
      const aT = st.ben_cathode === "phai";   // trái là anode?
      nhanT.element.textContent = `${aT ? "Anode (−)" : "Cathode (+)"} ${st.trai}`;
      nhanP.element.textContent = `${aT ? "Cathode (+)" : "Anode (−)"} ${st.phai}`;
      nhanDdT.element.textContent = `${st.muoi_trai} ${st.C_trai < 1 ? st.C_trai.toString().replace(".", ",") : "1"} M`;
      nhanDdP.element.textContent = `${st.muoi_phai} ${st.C_phai < 1 ? st.C_phai.toString().replace(".", ",") : "1"} M`;
      // anode mòn dần, cathode dày lên
      const anode = aT ? dcT : dcP, cathode = aT ? dcP : dcT;
      anode.thanh.scale.set(1 - st.mon_anode * 0.5, 1, 1 - st.mon_anode * 0.4);
      cathode.thanh.scale.set(1, 1, 1);
      cathode.phu.visible = st.day_cathode > 0.01;
      cathode.phu.scale.z = 0.01 + st.day_cathode * 2.2;
      cathode.vlPhu.color.copy(rgbMau(st.mau_kim_loai[aT ? "phai" : "trai"]));
      anode.phu.visible = false;
      const chay = !!st.chieu_electron;
      e.bat = chay; ionK.bat = chay; ionNO3.bat = chay;
      e.nguoc = st.chieu_electron === "phai_sang_trai";
      // K⁺ về phía cathode, NO₃⁻ về phía anode
      ionK.nguoc = !aT; ionNO3.nguoc = aT;
      e.tocDo = 0.06 + Math.min(0.3, st.E_pin * 0.1);
      nhanE.element.textContent = chay ? (e.nguoc ? "← e⁻ (dòng electron)" : "e⁻ → (dòng electron)") : "Không có dòng điện";
      nhanCau.visible = st.cau_muoi;
      if (st.cau_muoi !== cauTruoc) {
        cauTruoc = st.cau_muoi;
        gsap.to(cau.position, { y: st.cau_muoi ? 0 : 0.25, z: st.cau_muoi ? 0 : -0.25, duration: 0.7, ease: "power2.inOut" });
      }
    };
  }

  // ================================================================ 5. ĐIỆN PHÂN
  _dungDienPhan(tn, s) {
    const ct = this.ct;
    const binh = this._coc(0.11, 0.17, V3(0, 0, 0));
    const dd = this._chatLongTrong(binh, 0.12, [70, 150, 230], 0.6);
    const mang = this._luoi(new THREE.PlaneGeometry(0.2, 0.12), this._vl("mo", { mau: 0xf1f5f9, trongSuot: true }), { bong: false });
    mang.material.opacity = 0.45;
    mang.rotation.y = Math.PI / 2;
    mang.position.set(0, 0.07, 0);
    this.nhom.add(mang);
    const hong = this._luoi(new THREE.CylinderGeometry(0.1, 0.1, 0.1, 32, 1, false, Math.PI, Math.PI), this._vl("long", { rgb: [232, 62, 140], opacity: 0 }), { bong: false, nhan: false });
    hong.position.set(0, 0.062, 0);
    this.nhom.add(hong);
    const dienCuc = (x, nhan) => {
      const vl = this._vl("kim_loai", { rgb: [70, 74, 80], nham: 0.7 });
      const thanh = this._luoi(new RoundedBoxGeometry(0.03, 0.22, 0.012, 2, 0.003), vl);
      thanh.position.set(x, 0.13, 0);
      const vlPhu = this._vl("kim_loai", { rgb: [196, 112, 64], nham: 0.55 });
      const phu = this._luoi(new RoundedBoxGeometry(0.032, 0.1, 0.014, 2, 0.003), vlPhu);
      phu.position.set(x, 0.07, 0);
      phu.scale.set(1, 1, 0.05);
      phu.visible = false;
      this.nhom.add(thanh, phu);
      const nh = this._nhan(nhan, V3(x + Math.sign(x) * 0.06, 0.3, 0.02), "nhan-manh");
      return { thanh, vl, phu, vlPhu, nh, x };
    };
    const ca = dienCuc(-0.055, "Cathode (−)"), an = dienCuc(0.055, "Anode (+)");
    const botC = this._botKhi({ soLuong: this.chatLuong === "cao" ? 34 : 14, x: -0.055, z: 0, r: 0.03, yDay: 0.03, yMat: 0.125 });
    const botA = this._botKhi({ soLuong: this.chatLuong === "cao" ? 34 : 14, x: 0.055, z: 0, r: 0.03, yDay: 0.03, yMat: 0.125 });
    const quy = this._luoi(new THREE.PlaneGeometry(0.012, 0.06), this._vl("mo", { mau: 0x8b5cf6 }), { bong: false });
    quy.position.set(0.075, 0.2, 0.02);
    this.nhom.add(quy);
    const nhanQuy = this._nhan("Quỳ ẩm", V3(0.11, 0.22, 0.02), "nho");
    // nguồn điện + dây
    const nguon = this._luoi(new RoundedBoxGeometry(0.22, 0.12, 0.12, 3, 0.014), this._vl("nhua", { mau: 0x1e293b, nham: 0.5 }));
    nguon.position.set(0, 0.06, -0.32);
    const mhI = this._manHinh(256, 96), mhT = this._manHinh(256, 96);
    const pI = this._luoi(new THREE.PlaneGeometry(0.085, 0.032), new THREE.MeshBasicMaterial({ map: mhI.tex, toneMapped: false }), { bong: false });
    const pT = this._luoi(new THREE.PlaneGeometry(0.085, 0.032), new THREE.MeshBasicMaterial({ map: mhT.tex, toneMapped: false }), { bong: false });
    this._theo(pI.material); this._theo(pT.material);
    pI.position.set(-0.05, 0.08, -0.2595);
    pT.position.set(0.05, 0.08, -0.2595);
    this.nhom.add(nguon, pI, pT);
    this._nhan("Nguồn một chiều", V3(0, 0.03, -0.24), "nho");
    const dayC = this._day([V3(-0.06, 0.06, -0.26), V3(-0.12, 0.22, -0.2), V3(-0.055, 0.3, -0.02), V3(-0.055, 0.24, 0)], 0x1f2937);
    const dayA = this._day([V3(0.06, 0.06, -0.26), V3(0.12, 0.22, -0.2), V3(0.055, 0.3, -0.02), V3(0.055, 0.24, 0)], 0xb91c1c);
    const e1 = this._dongHat(new THREE.CatmullRomCurve3([V3(0.055, 0.24, 0.004), V3(0.055, 0.3, -0.016), V3(0.12, 0.22, -0.196), V3(0.06, 0.06, -0.256)]), { so: 8, mau: 0x38bdf8, r: 0.0035, tocDo: 0.25 });
    const e2 = this._dongHat(new THREE.CatmullRomCurve3([V3(-0.06, 0.06, -0.256), V3(-0.12, 0.22, -0.196), V3(-0.055, 0.3, -0.016), V3(-0.055, 0.24, 0.004)]), { so: 8, mau: 0x38bdf8, r: 0.0035, tocDo: 0.25 });
    const nhanKq = this._nhan("", V3(0, 0.36, 0), "nhan-manh");
    ct.goc = { vi_tri: V3(0.08, 0.75, 2.5), dich: V3(0, 0.25, -0.08), gan: 0.3, xa: 4.5, rong: 2.5, cao: 1.2 };
    ct.capNhat = (st) => {
      const kq = st.ket_qua;
      const nacl = st.bai === "nacl_mang_ngan", anodeCu = st.bai === "cuso4_anode_cu";
      mang.visible = nacl;
      if (st.mau_dd) this._doiMauLong(dd, MAU_NUOC.map((c, i) => c + (st.mau_dd.rgb[i] - c) * st.mau_dd.dam), 0.42 + 0.3 * st.mau_dd.dam, true);
      else this._doiMauLong(dd, null, 0.42, true);
      // cathode: lớp Cu bám dày dần; anode Cu tan mòn dần
      const mMax = 1.5;
      ca.vl.color.copy(rgbMau(anodeCu ? [196, 112, 64] : [70, 74, 80]));
      an.vl.color.copy(rgbMau(anodeCu ? [176, 106, 70] : [70, 74, 80]));
      ca.phu.visible = kq.m_cathode > 0.0005;
      ca.phu.scale.z = 0.05 + Math.min(1, kq.m_cathode / mMax) * 1.6;
      an.thanh.scale.set(anodeCu ? 1 - Math.min(0.45, kq.m_anode_giam / mMax * 0.5) : 1, 1, anodeCu ? 1 - Math.min(0.5, kq.m_anode_giam / mMax * 0.6) : 1);
      botC.bat = st.khi_dang_thoat.cathode;
      botA.bat = st.khi_dang_thoat.anode;
      hong.visible = nacl;
      hong.material.opacity = nacl && st.chi_thi_cathode ? st.chi_thi_cathode.ti_le * 0.55 : 0;
      quy.visible = nhanQuy.visible = nacl;
      if (nacl) quy.material.color.set(st.quy_anode === "mất màu" ? 0xf5f5f4 : 0x8b5cf6);
      e1.bat = e2.bat = st.dang_chay;
      ca.nh.element.textContent = `Cathode (−) ${st.dien_cuc.cathode}`;
      an.nh.element.textContent = `Anode (+) ${st.dien_cuc.anode}`;
      this._veManHinh(mhI, `${st.I.toFixed(1).replace(".", ",")} A`);
      this._veManHinh(mhT, `${Math.round(st.t)} s`, "#fbbf24");
      nhanKq.element.textContent = nacl
        ? `V(H₂) = ${kq.V_khi_cathode.toFixed(3).replace(".", ",")} L`
        : anodeCu ? `Cathode +${kq.m_cathode.toFixed(3).replace(".", ",")} g` : `m(Cu) = ${kq.m_cathode.toFixed(3).replace(".", ",")} g`;
    };
  }
}
