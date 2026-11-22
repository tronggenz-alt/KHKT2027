// arrow-renderer.js – ArrowRenderer: tạo, vẽ dần, làm mờ và xoá mũi tên 3D của từng bước.
// - Một bước = 1..n mũi tên (from/to là mảng → nhiều nhánh) + số thứ tự + nhãn; bước "tại chỗ" (from = to) = vòng sáng nhịp đập
//   (KHÔNG thêm đèn: thêm/bớt đèn buộc three.js biên dịch lại shader mọi vật liệu → giật hình).
// - Dùng lại: hình nón đầu mũi tên, hình vòng xung dùng chung; vật liệu cache theo (màu, kiểu). Ống thân là riêng từng mũi tên
//   (đường cong khác nhau) nhưng chỉ tạo MỘT lần cho mỗi bước của quy trình đang mở.
// - dongBo(chiSo, tienDo, giay) gọi mỗi khung hình: bước < chiSo → đã qua (mờ, mảnh), = chiSo → đang vẽ, > chiSo → xoá.
// - clear() xoá + dispose() toàn bộ (gọi khi đổi quy trình / đóng chế độ).
import * as THREE from "three";
import { CSS2DObject } from "three/addons/renderers/CSS2DRenderer.js";
import { CAU_HINH } from "./config.js?v=4";

const C = CAU_HINH.muiTen, X = CAU_HINH.xung;
const TREN = new THREE.Vector3(0, 1, 0);
const _p = new THREE.Vector3(), _tt = new THREE.Vector3(), _n = new THREE.Vector3();

export class ArrowRenderer {
  constructor({ scene, camera, viTri }) {
    this.camera = camera;
    this.viTri = viTri;                 // (id bộ phận) → Vector3 vị trí neo giải phẫu trên mô hình
    this.nhom = new THREE.Group();
    this.nhom.name = "giai-thich-mui-ten";
    scene.add(this.nhom);
    this._buoc = new Map();             // chiSo → { muiTen: [...], xung: [...], nhan: [CSS2DObject...], kieu }
    this._vatLieu = new Map();          // "màu|kiểu" → Material
    this._dauGeo = null;
    this._dauVienGeo = null;
    this._diemGeo = null;
    this._vongGeo = null;
    this._vongVienGeo = null;
  }

  // ---------- tài nguyên dùng chung ----------
  _vl(mau, kieu) {
    const khoa = `${mau}|${kieu}`;
    let m = this._vatLieu.get(khoa);
    if (!m) {
      const chung = { color: new THREE.Color(mau), transparent: true, depthTest: false, depthWrite: false };
      if (kieu === "than") m = new THREE.MeshBasicMaterial({ ...chung, opacity: 1 });
      else if (kieu === "than_qua") m = new THREE.MeshBasicMaterial({ ...chung, opacity: C.doMoDaQua });
      else if (kieu === "vien") m = new THREE.MeshBasicMaterial({ color: 0x070b14, transparent: true, opacity: C.doMoVien, depthTest: false, depthWrite: false });
      else if (kieu === "hat") m = new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.96, depthTest: false, depthWrite: false });
      else if (kieu === "xung_vien") m = new THREE.MeshBasicMaterial({ color: 0x070b14, transparent: true, opacity: 0.72, depthTest: false, depthWrite: false, side: THREE.DoubleSide });
      else m = new THREE.MeshBasicMaterial({ ...chung, opacity: kieu === "xung_qua" ? X.doMo * 0.28 : X.doMo, side: THREE.DoubleSide });
      this._vatLieu.set(khoa, m);
    }
    return m;
  }
  // Dịch trục Y của hình nón xuống -daiDau/2 để ĐỈNH nhọn trùng đúng gốc (0,0,0) → chạm chính xác điểm cuối p2
  get dauGeo() {
    if (!this._dauGeo) {
      this._dauGeo = new THREE.ConeGeometry(C.banKinhDau, C.daiDau, 16);
      this._dauGeo.translate(0, -C.daiDau * 0.5, 0);
    }
    return this._dauGeo;
  }
  get dauVienGeo() {
    if (!this._dauVienGeo) {
      this._dauVienGeo = new THREE.ConeGeometry(C.banKinhDau * 1.24, C.daiDau * 1.12, 16);
      this._dauVienGeo.translate(0, -C.daiDau * 0.52, 0);
    }
    return this._dauVienGeo;
  }
  get diemGeo() { return (this._diemGeo ||= new THREE.SphereGeometry(C.banKinhDiem || 0.014, 14, 10)); }
  get vongGeo() { return (this._vongGeo ||= new THREE.RingGeometry(X.banKinh * 0.58, X.banKinh, 40)); }
  get vongVienGeo() { return (this._vongVienGeo ||= new THREE.RingGeometry(X.banKinh * 0.48, X.banKinh * 1.12, 40)); }

  // ---------- dựng một bước ----------
  _dung(chiSo, step) {
    const b = { muiTen: [], xung: [], nhan: [], kieu: "" };
    if (step.tai_cho) {
      _p.set(0, 0, 0);
      for (const id of step.from) {
        const vt = this.viTri(id);
        const vien = new THREE.Mesh(this.vongVienGeo, this._vl("#070b14", "xung_vien"));
        const vong = new THREE.Mesh(this.vongGeo, this._vl(step.color, "xung"));
        vien.position.copy(vt);
        vong.position.copy(vt);
        vien.renderOrder = 995;
        vong.renderOrder = 999;
        vong.userData.mau = step.color;
        vong.userData.vien = vien;
        this.nhom.add(vien, vong);
        b.xung.push(vong);
        _p.add(vt);
      }
      if (step.from.length > 0) _p.multiplyScalar(1 / step.from.length);
      this._themNhan(b, chiSo, step, _p, _n.set(0, 0.14, 0.06));
    } else {
      const cap = [];
      if (step.from.length === 1) for (const den of step.to) cap.push([step.from[0], den]);
      else for (const tu of step.from) cap.push([tu, step.to[0]]);
      for (let idx = 0; idx < cap.length; idx++) {
        const [tu, den] = cap[idx];
        b.muiTen.push(this._taoMuiTen(this.viTri(tu), this.viTri(den), step.color, idx, cap.length));
      }
      // Đặt nhãn bước căn giữa phía trên điểm giữa các nhánh để không che điểm đầu/điểm cuối mũi tên
      if (b.muiTen.length) {
        _p.set(0, 0, 0);
        for (const mt of b.muiTen) {
          mt.duong.getPointAt(0.5, _tt);
          _p.add(_tt);
        }
        _p.multiplyScalar(1 / b.muiTen.length);
        this._themNhan(b, chiSo, step, _p, _n.set(0, 0.10, 0.05));
      }
    }
    this._buoc.set(chiSo, b);
    return b;
  }

  _taoMuiTen(p1, p2, mau, nhanhIdx = 0, tongNhanh = 1) {
    const d = Math.max(0.01, p1.distanceTo(p2));
    const giua = p1.clone().lerp(p2, 0.5);
    // Độ cong vừa phải tỷ lệ theo chiều dài đoạn (không bóp méo mũi tên ngắn) và nhô nhẹ về phía trước (+Z) cho dễ nhìn
    const zCong = Math.min(C.congToiDa, Math.max(0.016, d * C.doCong));
    giua.z += zCong;
    // Khi có nhiều nhánh xuất phát hoặc hội tụ, tách nhẹ theo phương ngang của từng nhánh để các thân mũi tên không đè nhau
    const dx = p2.x - p1.x, dy = p2.y - p1.y;
    const lenXY = Math.hypot(dx, dy);
    if (tongNhanh > 1 && lenXY > 0.02) {
      const phanBo = ((nhanhIdx / (tongNhanh - 1)) - 0.5) * 0.045;
      giua.x += (-dy / lenXY) * phanBo;
      giua.y += (dx / lenXY) * phanBo * 0.6;
    } else if (Math.abs(dx) < 0.05 && d < 0.35) {
      // Mũi tên gần thẳng đứng trong vùng tim: cong ngang nhẹ sang phía có khoảng trống để dễ phân biệt điểm đầu & điểm cuối
      giua.x += (p1.x >= 0 ? 1 : -1) * Math.min(0.028, d * 0.14);
    }

    const duong = new THREE.CatmullRomCurve3([p1.clone(), giua, p2.clone()]);
    const chieuDai = Math.max(0.01, duong.getLength());
    const tyLeDau = Math.min(0.45, (C.daiDau * 0.82) / chieuDai);

    const thanGeo = new THREE.TubeGeometry(duong, C.doanOng, C.banKinh, C.vongOng, false);
    const vienGeo = new THREE.TubeGeometry(duong, C.doanOng, C.banKinhVien, C.vongVien, false);
    const than = new THREE.Mesh(thanGeo, this._vl(mau, "than"));
    const vien = new THREE.Mesh(vienGeo, this._vl("#070b14", "vien"));
    const dauVien = new THREE.Mesh(this.dauVienGeo, this._vl("#070b14", "vien"));
    const dau = new THREE.Mesh(this.dauGeo, this._vl(mau, "than"));
    const diemDauVien = new THREE.Mesh(this.diemGeo, this._vl("#070b14", "vien"));
    const diemDau = new THREE.Mesh(this.diemGeo, this._vl(mau, "than"));
    const hatChay = new THREE.Mesh(this.diemGeo, this._vl("#ffffff", "hat"));

    diemDauVien.position.copy(p1);
    diemDauVien.scale.setScalar(1.25);
    diemDau.position.copy(p1);
    diemDau.scale.setScalar(0.92);
    hatChay.scale.setScalar(0.76);

    vien.renderOrder = 995;
    diemDauVien.renderOrder = 995;
    diemDau.renderOrder = 997;
    than.renderOrder = 998;
    dauVien.renderOrder = 999;
    dau.renderOrder = 1000;
    hatChay.renderOrder = 1001;

    thanGeo.setDrawRange(0, 0);
    vienGeo.setDrawRange(0, 0);
    dau.visible = false;
    dauVien.visible = false;
    hatChay.visible = false;

    this.nhom.add(vien, diemDauVien, diemDau, than, dauVien, dau, hatChay);
    return { duong, than, vien, dau, dauVien, diemDau, diemDauVien, hatChay, mau, tyLeDau };
  }

  _themNhan(b, chiSo, step, viTri, lech) {
    // Gộp số bước + tiêu đề trong MỘT CSS2DObject (flex ngang) để số không bao giờ đè lên chữ
    const the = document.createElement("div");
    the.className = "the-buoc-3d";
    the.style.setProperty("--mau-buoc", step.color);
    const so = document.createElement("span");
    so.className = "so-buoc-3d";
    so.style.background = step.color;
    so.textContent = String(chiSo + 1);
    const nhan = document.createElement("span");
    nhan.className = "nhan-buoc-3d";
    nhan.textContent = step.title;
    the.append(so, nhan);
    const o = new CSS2DObject(the);
    o.position.copy(viTri).add(lech);
    o.visible = false;
    the.style.display = "none";
    this.nhom.add(o);
    b.nhan.push(o);
  }

  // ---------- cập nhật mỗi khung hình ----------
  dongBo(steps, chiSo, tienDo, giay) {
    for (const [k] of this._buoc) if (k > chiSo || !steps[k]) this._xoaBuoc(k);   // lùi bước / đổi bước: xoá phần thừa ngay
    for (let k = 0; k <= chiSo && k < steps.length; k++) {
      const b = this._buoc.get(k) || this._dung(k, steps[k]);
      const kieu = k < chiSo ? "qua" : "dang";
      const u = k < chiSo ? 1 : tienDo;
      for (const mt of b.muiTen) this._capNhatMuiTen(mt, u, kieu, b.kieu !== kieu, giay);
      for (const v of b.xung) this._capNhatXung(v, kieu, giay, u);
      // Chỉ hiển thị nhãn chữ của ĐÚNG bước hiện tại; ẩn nhãn các bước đã qua để không chồng chữ
      const hienNhan = kieu === "dang";
      for (const o of b.nhan) {
        o.visible = hienNhan;
        o.element.style.display = hienNhan ? "" : "none";
        o.element.style.opacity = hienNhan ? "1" : "0";
      }
      if (b.kieu !== kieu) b.kieu = kieu;
    }
  }

  _capNhatMuiTen(mt, u, kieu, doiKieu, giay = 0) {
    const qua = kieu === "qua";
    if (doiKieu) {
      mt.than.material = mt.dau.material = this._vl(mt.mau, qua ? "than_qua" : "than");
      const tl = qua ? C.tyLeDaQua : 1;
      mt.than.scale.setScalar(1);
      mt.dau.scale.setScalar(tl);
      mt.than.renderOrder = qua ? 990 : 998;
      mt.dau.renderOrder = qua ? 991 : 1000;
      mt.vien.visible = !qua;
      mt.diemDau.visible = !qua;
      mt.diemDauVien.visible = !qua;
    }
    const t = Math.min(1, Math.max(0, u));
    // Thân ống dừng trước đáy hình nón (không thò xuyên qua đầu nhọn của mũi tên)
    const uThan = t <= 0.005 ? 0 : Math.max(0, t - mt.tyLeDau * Math.min(1, t * 2.5));
    const doan = uThan <= 0.002 ? 0 : Math.max(1, Math.min(C.doanOng, Math.round(uThan * C.doanOng)));
    mt.than.geometry.setDrawRange(0, doan * C.vongOng * 6);
    mt.vien.geometry.setDrawRange(0, doan * C.vongVien * 6);

    const coDau = t > 0.02;
    mt.dau.visible = coDau;
    mt.dauVien.visible = coDau && !qua;
    if (coDau) {
      mt.duong.getPointAt(t, mt.dau.position);
      mt.duong.getTangentAt(t, _tt);
      mt.dau.quaternion.setFromUnitVectors(TREN, _tt.normalize());
      mt.dauVien.position.copy(mt.dau.position);
      mt.dauVien.quaternion.copy(mt.dau.quaternion);
    }

    // Chấm sáng lướt liên tục từ điểm đầu → điểm cuối trên bước đang chạy để thể hiện rõ chiều dòng chảy
    const hienHat = !qua && t > 0.15;
    mt.hatChay.visible = hienHat;
    if (hienHat) {
      const pha = (giay * 0.75) % 1;
      const uHat = pha * Math.max(0.05, t - mt.tyLeDau * 0.6);
      mt.duong.getPointAt(uHat, mt.hatChay.position);
    }
  }

  _capNhatXung(v, kieu, giay, u) {
    const vien = v.userData.vien;
    v.quaternion.copy(this.camera.quaternion);                          // luôn quay mặt về camera
    if (vien) vien.quaternion.copy(this.camera.quaternion);
    if (kieu === "qua") {
      v.material = this._vl(v.userData.mau, "xung_qua");
      v.scale.setScalar(0.75);
      if (vien) vien.visible = false;
      return;
    }
    v.material = this._vl(v.userData.mau, "xung");                      // vật liệu nhịp đập chỉ dùng cho bước đang chạy
    const nhip = 0.5 + 0.5 * Math.sin(giay * X.tanSo * Math.PI * 2);
    const sc = (0.65 + 0.35 * Math.min(1, u * 3)) * (1 + 0.28 * nhip);
    v.scale.setScalar(sc);
    v.material.opacity = X.doMo * (0.68 + 0.32 * nhip);
    if (vien) { vien.visible = true; vien.scale.setScalar(sc); }
  }

  // ---------- xoá ----------
  _xoaBuoc(k) {
    const b = this._buoc.get(k);
    if (!b) return;
    for (const mt of b.muiTen) {
      this.nhom.remove(mt.than, mt.vien, mt.dau, mt.dauVien, mt.diemDau, mt.diemDauVien, mt.hatChay);
      mt.than.geometry.dispose();
      mt.vien.geometry.dispose();
    }
    for (const v of b.xung) {
      if (v.userData.vien) this.nhom.remove(v.userData.vien);
      this.nhom.remove(v);
    }
    for (const o of b.nhan) { this.nhom.remove(o); o.element.remove(); }
    this._buoc.delete(k);
  }

  /** Xoá mọi mũi tên + giải phóng toàn bộ geometry / material (kể cả phần dùng chung). */
  clear() {
    for (const k of [...this._buoc.keys()]) this._xoaBuoc(k);
    for (const m of this._vatLieu.values()) m.dispose();
    this._vatLieu.clear();
    this._dauGeo?.dispose(); this._dauGeo = null;
    this._dauVienGeo?.dispose(); this._dauVienGeo = null;
    this._diemGeo?.dispose(); this._diemGeo = null;
    this._vongGeo?.dispose(); this._vongGeo = null;
    this._vongVienGeo?.dispose(); this._vongVienGeo = null;
  }

  /** Số đối tượng 3D đang có (kiểm thử: bấm Tiếp liên tục không được sinh thừa). */
  get soDoiTuong() { return this.nhom.children.length; }
}
