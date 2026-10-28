// camera-director.js – CameraDirector: đóng khung camera vừa các bộ phận của bước, bù phần bị bảng giải thích che
// (camera.setViewOffset dịch tâm hình sang phần còn nhìn thấy) và bay mượt theo delta time của vòng render chung.
// Người dùng tự xoay / zoom → dung() huỷ chuyến bay ngay, không giành lại camera.
import * as THREE from "three";
import { CAU_HINH } from "./config.js";

const K = CAU_HINH.camera;
const muotMa = (x) => (x < 0.5 ? 2 * x * x : 1 - (-2 * x + 2) ** 2 / 2); // easeInOutQuad

export class CameraDirector {
  constructor({ camera, controls, khung3D }) {
    this.camera = camera;
    this.controls = controls;
    this.khung3D = khung3D;
    this.che = { phai: 0, duoi: 0 };   // số điểm ảnh bị bảng che
    this._bay = null;                   // { tuDich, denDich, tuViTri, denViTri, t, T }
    this._huong = new THREE.Vector3(...K.huongNhin).normalize();
  }

  get dangBay() { return !!this._bay; }

  /** Cập nhật phần bị che (0 = không che). Đặt / bỏ view offset rồi cập nhật ma trận chiếu. */
  datVungChe({ phai = 0, duoi = 0 } = {}) {
    this.che = { phai, duoi };
    this.capNhatViewOffset();
  }
  capNhatViewOffset() {
    const w = this.khung3D.clientWidth || 1, h = this.khung3D.clientHeight || 1;
    this.camera.aspect = w / h;
    if (this.che.phai > 0 || this.che.duoi > 0) this.camera.setViewOffset(w, h, Math.round(this.che.phai / 2), Math.round(this.che.duoi / 2), w, h);
    else this.camera.clearViewOffset();
    this.camera.updateProjectionMatrix();
  }

  /** Bay tới khung chứa hộp `hop` (THREE.Box3) trong `T` giây. */
  dongKhung(hop, T) {
    if (!hop || hop.isEmpty()) return;
    const tam = hop.getCenter(new THREE.Vector3());
    const r = Math.max(K.banKinhToiThieu, hop.getBoundingSphere(new THREE.Sphere()).radius);
    const w = this.khung3D.clientWidth || 1, h = this.khung3D.clientHeight || 1;
    const hThay = Math.max(1, h - this.che.duoi), wThay = Math.max(1, w - this.che.phai);
    const tanDoc = Math.tan(THREE.MathUtils.degToRad(this.camera.fov) / 2);
    const tanNgang = tanDoc * (wThay / hThay);
    // khoảng cách để quả cầu bao chiếm tyLeKhung phần nhìn thấy theo chiều hẹp hơn
    const tyLeDoc = hThay / h;
    let khoang = r / (K.tyLeKhung * Math.min(tanDoc * tyLeDoc, tanNgang * tyLeDoc));
    khoang = Math.min(K.khoangXa, Math.max(K.khoangGan, khoang));
    const viTri = tam.clone().addScaledVector(this._huong, khoang);
    this.bayToi(tam, viTri, T);
  }

  bayToi(dich, viTri, T) {
    if (T <= 0.001) {
      this.controls.target.copy(dich);
      this.camera.position.copy(viTri);
      this._bay = null;
      return;
    }
    this._bay = { tuDich: this.controls.target.clone(), denDich: dich.clone(), tuViTri: this.camera.position.clone(), denViTri: viTri.clone(), t: 0, T };
  }

  dung() { this._bay = null; }

  /** Gọi mỗi khung hình từ vòng render chung. */
  update(dt) {
    const b = this._bay;
    if (!b) return;
    b.t += dt;
    const p = muotMa(Math.min(1, b.t / b.T));
    this.controls.target.lerpVectors(b.tuDich, b.denDich, p);
    this.camera.position.lerpVectors(b.tuViTri, b.denViTri, p);
    if (b.t >= b.T) this._bay = null;
  }
}
