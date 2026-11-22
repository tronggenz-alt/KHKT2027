// arrow-renderer.js – ArrowRenderer: tạo, vẽ dần, làm mờ và xoá mũi tên 3D của từng bước.
// - Một bước = 1..n mũi tên (from/to là mảng → nhiều nhánh) + số thứ tự + nhãn; bước "tại chỗ" (from = to) = vòng sáng nhịp đập
//   (KHÔNG thêm đèn: thêm/bớt đèn buộc three.js biên dịch lại shader mọi vật liệu → giật hình).
// - Dùng lại: hình nón đầu mũi tên, hình vòng xung dùng chung; vật liệu cache theo (màu, kiểu). Ống thân là riêng từng mũi tên
//   (đường cong khác nhau) nhưng chỉ tạo MỘT lần cho mỗi bước của quy trình đang mở.
// - dongBo(chiSo, tienDo, giay) gọi mỗi khung hình: bước < chiSo → đã qua (mờ, mảnh), = chiSo → đang vẽ, > chiSo → xoá.
// - clear() xoá + dispose() toàn bộ (gọi khi đổi quy trình / đóng chế độ).
import * as THREE from "three";
import { CSS2DObject } from "three/addons/renderers/CSS2DRenderer.js";
import { CAU_HINH } from "./config.js";

const C = CAU_HINH.muiTen, X = CAU_HINH.xung;
const TREN = new THREE.Vector3(0, 1, 0);
const _p = new THREE.Vector3(), _tt = new THREE.Vector3(), _n = new THREE.Vector3();

export class ArrowRenderer {
  constructor({ scene, camera, viTri }) {
    this.camera = camera;
    this.viTri = viTri;                 // (id bộ phận) → Vector3 vị trí neo trên mô hình
    this.nhom = new THREE.Group();
    this.nhom.name = "giai-thich-mui-ten";
    scene.add(this.nhom);
    this._buoc = new Map();             // chiSo → { muiTen: [...], xung: [...], nhan: [CSS2DObject...], kieu }
    this._vatLieu = new Map();          // "màu|kiểu" → Material
    this._dauGeo = null;
    this._vongGeo = null;
  }

  // ---------- tài nguyên dùng chung ----------
  _vl(mau, kieu) {
    const khoa = `${mau}|${kieu}`;
    let m = this._vatLieu.get(khoa);
    if (!m) {
      const chung = { color: new THREE.Color(mau), transparent: true, depthTest: false, depthWrite: false };
      if (kieu === "than") m = new THREE.MeshBasicMaterial({ ...chung, opacity: 1 });
      else if (kieu === "than_qua") m = new THREE.MeshBasicMaterial({ ...chung, opacity: C.doMoDaQua });
      else if (kieu === "vien") m = new THREE.MeshBasicMaterial({ ...chung, opacity: C.doMoVien, blending: THREE.AdditiveBlending });
      else m = new THREE.MeshBasicMaterial({ ...chung, opacity: kieu === "xung_qua" ? X.doMo * 0.4 : X.doMo,   // xung / xung_qua
        blending: THREE.AdditiveBlending, side: THREE.DoubleSide });
      this._vatLieu.set(khoa, m);
    }
    return m;
  }
  get dauGeo() { return (this._dauGeo ||= new THREE.ConeGeometry(C.banKinhDau, C.daiDau, 14)); }
  get vongGeo() { return (this._vongGeo ||= new THREE.RingGeometry(X.banKinh * 0.62, X.banKinh, 40)); }

  // ---------- dựng một bước ----------
  _dung(chiSo, step) {
    const b = { muiTen: [], xung: [], nhan: [], kieu: "" };
    if (step.tai_cho) {
      for (const id of step.from) {
        const vong = new THREE.Mesh(this.vongGeo, this._vl(step.color, "xung"));
        vong.position.copy(this.viTri(id));
        vong.renderOrder = 999;
        vong.userData.mau = step.color;
        this.nhom.add(vong);
        b.xung.push(vong);
      }
      this._themNhan(b, chiSo, step, this.viTri(step.from[0]), _n.set(0.04, 0.05, 0.02));
    } else {
      const cap = [];
      if (step.from.length === 1) for (const den of step.to) cap.push([step.from[0], den]);
      else for (const tu of step.from) cap.push([tu, step.to[0]]);
      for (const [tu, den] of cap) b.muiTen.push(this._taoMuiTen(this.viTri(tu), this.viTri(den), step.color));
      const dau = b.muiTen[0];
      if (dau) {
        dau.duong.getPointAt(0.5, _p);
        dau.duong.getTangentAt(0.5, _tt);
        _n.crossVectors(_tt, TREN);
        if (_n.lengthSq() < 1e-6) _n.set(1, 0, 0);
        _n.normalize().multiplyScalar(0.04).add(_tt.set(0, 0.015, 0.015));
        this._themNhan(b, chiSo, step, _p, _n);
      }
    }
    this._buoc.set(chiSo, b);
    return b;
  }

  _taoMuiTen(p1, p2, mau) {
    const d = p1.distanceTo(p2);
    const giua = p1.clone().lerp(p2, 0.5);
    giua.z += Math.min(C.congToiDa, d * C.doCong) + 0.04;       // cong ra phía trước để không chìm vào bộ phận
    const duong = new THREE.CatmullRomCurve3([p1.clone(), giua, p2.clone()]);
    const thanGeo = new THREE.TubeGeometry(duong, C.doanOng, C.banKinh, C.vongOng, false);
    const vienGeo = new THREE.TubeGeometry(duong, C.doanOng, C.banKinhVien, C.vongVien, false);
    const than = new THREE.Mesh(thanGeo, this._vl(mau, "than"));
    const vien = new THREE.Mesh(vienGeo, this._vl(mau, "vien"));
    const dau = new THREE.Mesh(this.dauGeo, this._vl(mau, "than"));
    than.renderOrder = 998; vien.renderOrder = 997; dau.renderOrder = 999;
    thanGeo.setDrawRange(0, 0); vienGeo.setDrawRange(0, 0); dau.visible = false;
    this.nhom.add(vien, than, dau);
    return { duong, than, vien, dau, mau };
  }

  _themNhan(b, chiSo, step, viTri, lech) {
    const so = document.createElement("div");
    so.className = "so-buoc-3d";
    so.style.background = step.color;
    so.textContent = String(chiSo + 1);
    const nhan = document.createElement("div");
    nhan.className = "nhan-buoc-3d";
    nhan.style.borderLeftColor = step.color;
    nhan.textContent = step.title;
    for (const [el, dx] of [[so, 0], [nhan, 0.07]]) {
      const o = new CSS2DObject(el);
      o.position.copy(viTri).add(lech).x += dx;
      this.nhom.add(o);
      b.nhan.push(o);
    }
  }

  // ---------- cập nhật mỗi khung hình ----------
  dongBo(steps, chiSo, tienDo, giay) {
    for (const [k] of this._buoc) if (k > chiSo || !steps[k]) this._xoaBuoc(k);   // lùi bước / đổi bước: xoá phần thừa ngay
    for (let k = 0; k <= chiSo && k < steps.length; k++) {
      const b = this._buoc.get(k) || this._dung(k, steps[k]);
      const kieu = k < chiSo ? "qua" : "dang";
      const u = k < chiSo ? 1 : tienDo;
      for (const mt of b.muiTen) this._capNhatMuiTen(mt, u, kieu, b.kieu !== kieu);
      for (const v of b.xung) this._capNhatXung(v, kieu, giay, u);
      if (b.kieu !== kieu) {
        for (const o of b.nhan) o.element.style.opacity = kieu === "qua" ? String(C.doMoNhanDaQua) : "1";
        b.kieu = kieu;
      }
      for (const o of b.nhan) o.visible = kieu === "qua" || u > 0.35;   // nhãn hiện khi mũi tên đã mọc quá 1/3
    }
  }

  _capNhatMuiTen(mt, u, kieu, doiKieu) {
    if (doiKieu) {
      const qua = kieu === "qua";
      mt.than.material = mt.dau.material = this._vl(mt.mau, qua ? "than_qua" : "than");
      const tl = qua ? C.tyLeDaQua : 1;
      mt.than.scale.setScalar(1); mt.dau.scale.setScalar(tl);
      mt.vien.visible = !qua;
    }
    const doan = u <= 0.005 ? 0 : Math.max(1, Math.min(C.doanOng, Math.floor(u * C.doanOng)));
    mt.than.geometry.setDrawRange(0, doan * C.vongOng * 6);
    mt.vien.geometry.setDrawRange(0, doan * C.vongVien * 6);
    mt.dau.visible = doan > 0;
    if (doan > 0) {
      const t = Math.min(1, u);
      mt.duong.getPointAt(t, mt.dau.position);
      mt.duong.getTangentAt(t, _tt);
      mt.dau.quaternion.setFromUnitVectors(TREN, _tt.normalize());
    }
  }

  _capNhatXung(v, kieu, giay, u) {
    v.quaternion.copy(this.camera.quaternion);                          // luôn quay mặt về camera
    if (kieu === "qua") { v.material = this._vl(v.userData.mau, "xung_qua"); v.scale.setScalar(0.8); return; }
    v.material = this._vl(v.userData.mau, "xung");                      // vật liệu nhịp đập chỉ dùng cho bước đang chạy
    const nhip = 0.5 + 0.5 * Math.sin(giay * X.tanSo * Math.PI * 2);
    v.scale.setScalar((0.6 + 0.4 * Math.min(1, u * 3)) * (1 + 0.35 * nhip));
    v.material.opacity = X.doMo * (0.55 + 0.45 * nhip);
  }

  // ---------- xoá ----------
  _xoaBuoc(k) {
    const b = this._buoc.get(k);
    if (!b) return;
    for (const mt of b.muiTen) {
      this.nhom.remove(mt.than, mt.vien, mt.dau);
      mt.than.geometry.dispose();
      mt.vien.geometry.dispose();
    }
    for (const v of b.xung) this.nhom.remove(v);
    for (const o of b.nhan) { this.nhom.remove(o); o.element.remove(); }
    this._buoc.delete(k);
  }

  /** Xoá mọi mũi tên + giải phóng toàn bộ geometry / material (kể cả phần dùng chung). */
  clear() {
    for (const k of [...this._buoc.keys()]) this._xoaBuoc(k);
    for (const m of this._vatLieu.values()) m.dispose();
    this._vatLieu.clear();
    this._dauGeo?.dispose(); this._dauGeo = null;
    this._vongGeo?.dispose(); this._vongGeo = null;
  }

  /** Số đối tượng 3D đang có (kiểm thử: bấm Tiếp liên tục không được sinh thừa). */
  get soDoiTuong() { return this.nhom.children.length; }
}
