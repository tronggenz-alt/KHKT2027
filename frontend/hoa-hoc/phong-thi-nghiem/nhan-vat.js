/* Phòng thí nghiệm Hoá học ảo – NHÂN VẬT & ĐIỀU KHIỂN GENSHIN IMPACT (nhan-vat.js)
   Nhân vật 3D procedural, điều khiển WASD theo hướng camera, di chuột khoá con trỏ xoay camera,
   tay đòn camera chống xuyên tường, góc nhìn thứ 3 (vai) & góc nhìn thứ nhất (F), nhảy Space,
   chạy nhanh Shift, tự động vào bàn khi dừng chân, toàn cảnh nhà búp bê (V). */

import * as THREE from "three";
import { CSS2DObject } from "three/addons/renderers/CSS2DRenderer.js";

const V3 = (x, y, z) => new THREE.Vector3(x, y, z);

function taoViTri(x, z) {
  const arr = [x, z];
  Object.defineProperties(arr, {
    x: { get() { return this[0]; }, set(v) { this[0] = v; } },
    z: { get() { return this[1]; }, set(v) { this[1] = v; } }
  });
  return arr;
}

function laOnhap(el) {
  if (!el) return false;
  const tag = (el.tagName || "").toLowerCase();
  return tag === "input" || tag === "textarea" || tag === "select" || el.isContentEditable || el.closest?.("dialog[open]");
}

export class NhanVat {
  /**
   * @param {THREE.Scene} scene - Scene Three.js
   * @param {Object} banDo - Dữ liệu ban-do.json
   * @param {THREE.PerspectiveCamera} camera - Camera chính
   * @param {Object} controls - OrbitControls
   * @param {HTMLElement} domKhung - Thẻ container 3D (#ptn-khung)
   * @param {Object} options - Callbacks
   */
  constructor(scene, banDo, camera, controls, domKhung, options = {}) {
    this.scene = scene;
    this.banDo = banDo;
    this.camera = camera;
    this.controls = controls;
    this.domKhung = domKhung;

    this.onDenGan = options.onDenGan || (() => {});
    this.onTramGan = options.onTramGan || null;
    this.onVaoTram = options.onVaoTram || (() => {});
    this.onRoiTram = options.onRoiTram || (() => {});
    this.onKhu = options.onKhu || (() => {});
    this.onKhuVuc = options.onKhuVuc || null;
    this.onDoiViTri = options.onDoiViTri || null;
    this.onThongBao = options.onThongBao || (() => {});
    this.onTongQuan = options.onTongQuan || (() => {});

    // Cài đặt điều khiển (lưu localStorage)
    this.caiDat = this._docCaiDat();

    // Vị trí & hướng nhân vật ban đầu
    const xp = banDo.xuat_phat || { x: 11, z: 13.6, huong: 0 };
    this.viTri = taoViTri(xp.x, xp.z);
    this.gocQuay = THREE.MathUtils.degToRad(xp.huong || 0); // Hướng nhìn ban đầu về phía Bắc (-Z)
    this.tocDoHienTai = 0;
    this.phaBuoc = 0;
    this.khuHienTai = null;
    this.tramGan = null;
    this.cooldownTuVaoBan = 0; // Thời gian hồi chiêu tự vào bàn sau khi vừa rời trạm

    // Nhảy Space
    this.nhayY = 0;
    this.vanTocY = 0;
    this.dangNhay = false;

    // Tự động vào bàn khi dừng chân
    this.thoiGianDungGanBan = 0;
    this.thoiGianKhongXoayChuot = 0;

    // Ngoại hình & đồ bảo hộ
    this.phongCach = parseInt(localStorage.getItem("ptn_nhan_vat_style") || "0", 10) % 3;
    this.daMacBaoHo = localStorage.getItem("ptn_bao_ho") === "1";

    // Camera Rig kiểu Genshin Impact
    // camYaw: góc ngang (0 = nhìn về Bắc -Z; camera ở sau lưng phía Nam +Z)
    this.camYaw = this.gocQuay;
    this.camPitch = 0.22;       // Ngửa nhẹ nhìn xuống (khoảng 13°)
    this.camKhoangCach = 3.2;   // Khoảng cách mong muốn (1.8 - 6.0 m)
    this.camKhoangCachHienTai = 3.2;
    this.cheDoCamera = "theo_sau"; // "theo_sau" | "toan_canh" | "tai_ban"
    this.cheDoGocNhin = this.caiDat.gocNhinMacDinh || "goc_3"; // "goc_3" | "goc_1"
    this.gocNhinTruocDo = "goc_3";

    // Pointer Lock & Chuột
    this.coKhoaChuot = false;
    this.chuotDangKeo = false;

    // Phím bấm & tìm đường
    this.phim = {};
    this.duongDi = [];
    this._onDenDich = null;

    // Lưới tìm đường
    const BanDoMod = (typeof window !== "undefined" && window.PTNBanDo) ? window.PTNBanDo : null;
    this.luoi = BanDoMod ? BanDoMod.taoLuoi(this.banDo, { o: 0.25, banKinhNguoi: 0.3 }) : null;
    this.hopChanCamera = BanDoMod ? BanDoMod.layVatChanCamera(this.banDo) : [];

    this._dungMesh();
    this._dungBongVaDauDich();
    this._dungBongBongTuongTac();
    this._dungChamTam();
    this._langNgheSuKien();
    this._khoiTaoJoystick();
  }

  _docCaiDat() {
    return {
      doNhay: parseFloat(localStorage.getItem("ptn_do_nhay_chuot") || "1.0") || 1.0,
      daoY: localStorage.getItem("ptn_dao_truc_y") === "1",
      tuXoayCam: localStorage.getItem("ptn_tu_xoay_cam") !== "0",
      gocNhinMacDinh: localStorage.getItem("ptn_goc_nhin_mac_dinh") || "goc_3",
      kieuChuot: localStorage.getItem("ptn_kieu_chuot") || "khoa_con_tro",
      tuVaoBan: localStorage.getItem("ptn_tu_vao_ban") !== "0"
    };
  }

  luuCaiDat(cdMoi = {}) {
    Object.assign(this.caiDat, cdMoi);
    if (cdMoi.doNhay !== undefined) localStorage.setItem("ptn_do_nhay_chuot", String(this.caiDat.doNhay));
    if (cdMoi.daoY !== undefined) localStorage.setItem("ptn_dao_truc_y", this.caiDat.daoY ? "1" : "0");
    if (cdMoi.tuXoayCam !== undefined) localStorage.setItem("ptn_tu_xoay_cam", this.caiDat.tuXoayCam ? "1" : "0");
    if (cdMoi.gocNhinMacDinh !== undefined) localStorage.setItem("ptn_goc_nhin_mac_dinh", this.caiDat.gocNhinMacDinh);
    if (cdMoi.kieuChuot !== undefined) localStorage.setItem("ptn_kieu_chuot", this.caiDat.kieuChuot);
    if (cdMoi.tuVaoBan !== undefined) localStorage.setItem("ptn_tu_vao_ban", this.caiDat.tuVaoBan ? "1" : "0");
  }

  // ---------------------------------------------------------------- DỰNG HÌNH NHÂN VẬT
  _dungMesh() {
    this.nhom = new THREE.Group();
    this.nhom.position.set(this.viTri[0], 0, this.viTri[1]);
    this.nhom.rotation.y = this.gocQuay;
    this.scene.add(this.nhom);

    const BANG_MAU = [
      { da: 0xffdfc4, toc: 0x1e293b, ao: 0x0f2b48, quan: 0x334155 },
      { da: 0xf5d0b5, toc: 0x854d0e, ao: 0x0284c7, quan: 0x1e293b },
      { da: 0xeac0a2, toc: 0x09090b, ao: 0x059669, quan: 0x374151 }
    ];
    const mau = BANG_MAU[this.phongCach];

    this.vlDa = new THREE.MeshStandardMaterial({ color: mau.da, roughness: 0.7 });
    this.vlToc = new THREE.MeshStandardMaterial({ color: mau.toc, roughness: 0.8 });
    this.vlAo = new THREE.MeshStandardMaterial({ color: mau.ao, roughness: 0.6 });
    this.vlQuan = new THREE.MeshStandardMaterial({ color: mau.quan, roughness: 0.7 });
    this.vlGiay = new THREE.MeshStandardMaterial({ color: 0x09090b, roughness: 0.5 });

    // Đồ bảo hộ
    this.vlBlouse = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.4 });
    this.vlKinh = new THREE.MeshStandardMaterial({ color: 0x38bdf8, roughness: 0.1, transparent: true, opacity: 0.65 });
    this.vlGang = new THREE.MeshStandardMaterial({ color: 0x67e8f9, roughness: 0.3 });

    // Danh sách vật liệu cần làm mờ khi camera quá gần
    this._vatLieuNhanVat = [this.vlDa, this.vlToc, this.vlAo, this.vlQuan, this.vlGiay, this.vlBlouse, this.vlGang];

    // 1. Thân người (Capsule)
    this.than = new THREE.Mesh(new THREE.CapsuleGeometry(0.18, 0.42, 8, 16), this.vlAo);
    this.than.position.y = 0.98;
    this.than.castShadow = true;
    this.nhom.add(this.than);

    // Áo blouse bảo hộ
    this.aoBlouse = new THREE.Mesh(new THREE.CapsuleGeometry(0.195, 0.48, 8, 16), this.vlBlouse);
    this.aoBlouse.position.y = 0.95;
    this.aoBlouse.castShadow = true;
    this.aoBlouse.visible = this.daMacBaoHo;
    this.nhom.add(this.aoBlouse);

    // 2. Đầu & Tóc & Mắt
    this.dau = new THREE.Group();
    this.dau.position.y = 1.48;
    this.nhom.add(this.dau);

    const matDau = new THREE.Mesh(new THREE.SphereGeometry(0.15, 16, 16), this.vlDa);
    matDau.castShadow = true;
    this.dau.add(matDau);

    const toc = new THREE.Mesh(new THREE.SphereGeometry(0.16, 16, 12, 0, Math.PI * 2, 0, Math.PI * 0.55), this.vlToc);
    toc.position.y = 0.02;
    this.dau.add(toc);

    // Mắt
    const vlMat = new THREE.MeshBasicMaterial({ color: 0x09090b });
    const matP = new THREE.Mesh(new THREE.SphereGeometry(0.018, 8, 8), vlMat);
    matP.position.set(0.05, 0.01, 0.14);
    this.dau.add(matP);
    const matT = matP.clone();
    matT.position.x = -0.05;
    this.dau.add(matT);

    // Kính bảo hộ
    this.kinhBaoHo = new THREE.Group();
    const gongKinh = new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.045, 0.03), this.vlKinh);
    gongKinh.position.set(0, 0.015, 0.145);
    this.kinhBaoHo.add(gongKinh);
    this.kinhBaoHo.visible = this.daMacBaoHo;
    this.dau.add(this.kinhBaoHo);

    // 3. Tay
    this.tayT = this._taoChi(0.05, 0.32, this.vlAo, this.vlDa);
    this.tayT.position.set(-0.24, 1.15, 0);
    this.nhom.add(this.tayT);

    this.tayP = this._taoChi(0.05, 0.32, this.vlAo, this.vlDa);
    this.tayP.position.set(0.24, 1.15, 0);
    this.nhom.add(this.tayP);

    this.gangT = new THREE.Mesh(new THREE.SphereGeometry(0.055, 8, 8), this.vlGang);
    this.gangT.position.set(0, -0.32, 0);
    this.gangT.visible = this.daMacBaoHo;
    this.tayT.add(this.gangT);

    this.gangP = new THREE.Mesh(new THREE.SphereGeometry(0.055, 8, 8), this.vlGang);
    this.gangP.position.set(0, -0.32, 0);
    this.gangP.visible = this.daMacBaoHo;
    this.tayP.add(this.gangP);

    // 4. Chân
    this.chanT = this._taoChi(0.065, 0.44, this.vlQuan, this.vlGiay);
    this.chanT.position.set(-0.11, 0.65, 0);
    this.nhom.add(this.chanT);

    this.chanP = this._taoChi(0.065, 0.44, this.vlQuan, this.vlGiay);
    this.chanP.position.set(0.11, 0.65, 0);
    this.nhom.add(this.chanP);
  }

  _taoChi(bk, dai, vlTren, vlDuoi) {
    const g = new THREE.Group();
    const tren = new THREE.Mesh(new THREE.CylinderGeometry(bk, bk * 0.9, dai * 0.8, 8), vlTren);
    tren.position.y = -dai * 0.4;
    tren.castShadow = true;
    g.add(tren);

    const duoi = new THREE.Mesh(new THREE.SphereGeometry(bk * 0.95, 8, 8), vlDuoi);
    duoi.position.y = -dai * 0.85;
    duoi.castShadow = true;
    g.add(duoi);
    return g;
  }

  _dungBongVaDauDich() {
    const c = document.createElement("canvas");
    c.width = 64; c.height = 64;
    const g = c.getContext("2d");
    const rad = g.createRadialGradient(32, 32, 4, 32, 32, 32);
    rad.addColorStop(0, "rgba(15, 43, 72, 0.5)");
    rad.addColorStop(1, "rgba(15, 43, 72, 0)");
    g.fillStyle = rad;
    g.fillRect(0, 0, 64, 64);
    const texBong = new THREE.CanvasTexture(c);
    const matBong = new THREE.MeshBasicMaterial({ map: texBong, transparent: true, depthWrite: false });

    this.bongMesh = new THREE.Mesh(new THREE.PlaneGeometry(0.8, 0.8), matBong);
    this.bongMesh.rotation.x = -Math.PI / 2;
    this.bongMesh.position.set(this.viTri[0], 0.005, this.viTri[1]);
    this.scene.add(this.bongMesh);

    // Vòng tròn điểm đích khi click sàn
    const cDich = document.createElement("canvas");
    cDich.width = 128; cDich.height = 128;
    const gD = cDich.getContext("2d");
    gD.strokeStyle = "#38bdf8";
    gD.lineWidth = 6;
    gD.beginPath();
    gD.arc(64, 64, 52, 0, Math.PI * 2);
    gD.stroke();
    const texDich = new THREE.CanvasTexture(cDich);
    this.vongDich = new THREE.Mesh(
      new THREE.PlaneGeometry(0.7, 0.7),
      new THREE.MeshBasicMaterial({ map: texDich, transparent: true, depthWrite: false })
    );
    this.vongDich.rotation.x = -Math.PI / 2;
    this.vongDich.visible = false;
    this.scene.add(this.vongDich);
  }

  _dungBongBongTuongTac() {
    const el = document.createElement("div");
    el.className = "ptn-nhan ptn-nhan-tuong-tac";
    el.style.display = "none";
    el.style.pointerEvents = "none";
    el.innerHTML = '<b style="color:var(--nhan)">E</b> – Bắt đầu';
    this.elBongBong = el;

    this.objBongBong = new CSS2DObject(el);
    this.objBongBong.position.set(0, 1.85, 0);
    this.nhom.add(this.objBongBong);
  }

  _dungChamTam() {
    let cham = document.getElementById("ptn-cham-tam");
    if (!cham && this.domKhung) {
      cham = document.createElement("div");
      cham.id = "ptn-cham-tam";
      cham.className = "ptn-cham-tam";
      cham.setAttribute("aria-hidden", "true");
      this.domKhung.appendChild(cham);
    }
    this.elChamTam = cham;
  }

  // ---------------------------------------------------------------- BẢO HỘ & NGOẠI HÌNH
  macBaoHo(bat = !this.daMacBaoHo) {
    this.daMacBaoHo = bat;
    localStorage.setItem("ptn_bao_ho", bat ? "1" : "0");
    if (this.aoBlouse) this.aoBlouse.visible = bat;
    if (this.kinhBaoHo) this.kinhBaoHo.visible = bat;
    if (this.gangT) this.gangT.visible = bat;
    if (this.gangP) this.gangP.visible = bat;
    this.onThongBao(bat ? "🥼 Đã mặc đồ bảo hộ (Áo blouse, Kính, Găng tay)" : "Đã cởi đồ bảo hộ");
  }

  doiNgoaiHinh(kieu) {
    this.phongCach = (kieu !== undefined ? kieu : this.phongCach + 1) % 3;
    localStorage.setItem("ptn_nhan_vat_style", String(this.phongCach));
    const BANG_MAU = [
      { da: 0xffdfc4, toc: 0x1e293b, ao: 0x0f2b48, quan: 0x334155 },
      { da: 0xf5d0b5, toc: 0x854d0e, ao: 0x0284c7, quan: 0x1e293b },
      { da: 0xeac0a2, toc: 0x09090b, ao: 0x059669, quan: 0x374151 }
    ];
    const m = BANG_MAU[this.phongCach];
    this.vlDa.color.set(m.da);
    this.vlToc.color.set(m.toc);
    this.vlAo.color.set(m.ao);
    this.vlQuan.color.set(m.quan);
  }

  // ---------------------------------------------------------------- SỰ KIỆN & PHÍM BẤM
  _langNgheSuKien() {
    const el = this.domKhung;

    // Bàn phím
    window.addEventListener("keydown", (e) => {
      if (laOnhap(document.activeElement)) return;

      const k = e.key.toLowerCase();
      this.phim[k] = true;
      if (e.shiftKey) this.phim.shift = true;
      if (e.ctrlKey) this.phim.ctrl = true;

      // Huỷ đường tự đi khi bấm WASD
      if (["w", "a", "s", "d", "arrowup", "arrowdown", "arrowleft", "arrowright"].includes(k)) {
        this.duongDi = [];
        this.vongDich.visible = false;
      }

      // Space: Nhảy
      if (e.code === "Space" && !this.dangNhay && this.cheDoCamera !== "tai_ban") {
        e.preventDefault();
        this.dangNhay = true;
        this.vanTocY = 4.2; // Độ cao ~0.5m với g=18
      }

      // E / Enter: Tương tác
      if (k === "e" || k === "enter") {
        this._xuLyTuongTac();
      }

      // Q hoặc Esc: Rời bàn khi đang tại bàn
      if (k === "q" || (k === "escape" && this.cheDoCamera === "tai_ban")) {
        if (this.cheDoCamera === "tai_ban") {
          this.roiTram();
        }
      }

      // F: Đổi góc nhìn thứ nhất (1) ⇄ thứ ba (3)
      if (k === "f" && this.cheDoCamera !== "tai_ban") {
        this.datGocNhin(this.cheDoGocNhin === "goc_3" ? "goc_1" : "goc_3");
      }

      // V: Đổi sang Toàn cảnh (nhà búp bê) ⇄ Trở lại góc nhìn trước đó
      if (k === "v") {
        this.chuyenCheDoTongQuan();
      }

      // M: Phóng to / thu nhỏ bản đồ
      if (k === "m") {
        const nutM = document.getElementById("ptn-mini-thu-phong");
        if (nutM) nutM.click();
      }

      // ?: Mở bảng phím tắt trợ giúp
      if (e.key === "?" || k === "/") {
        const nutTroGiup = document.getElementById("ptn-nut-tro-giup");
        if (nutTroGiup) nutTroGiup.click();
      }
    });

    window.addEventListener("keyup", (e) => {
      const k = e.key.toLowerCase();
      this.phim[k] = false;
      if (!e.shiftKey) this.phim.shift = false;
      if (!e.ctrlKey) this.phim.ctrl = false;
    });

    if (!el) return;

    // Pointer Lock & Chuột
    el.addEventListener("click", () => {
      if (this.cheDoCamera === "tai_ban") return;
      if (this.caiDat.kieuChuot === "khoa_con_tro") {
        if (document.pointerLockElement !== el && el.requestPointerLock) {
          el.requestPointerLock();
        }
      }
    });

    document.addEventListener("pointerlockchange", () => {
      const dangKhoa = document.pointerLockElement === el;
      this.coKhoaChuot = dangKhoa;
      if (this.elChamTam) this.elChamTam.classList.toggle("hien", dangKhoa);
    });

    // Xoay chuột khi đã khoá con trỏ
    window.addEventListener("mousemove", (e) => {
      if (this.cheDoCamera === "tai_ban") return;

      if (this.coKhoaChuot) {
        const doNhay = (this.caiDat.doNhay || 1.0) * 0.0022;
        const daoY = this.caiDat.daoY ? -1 : 1;

        this.camYaw -= e.movementX * doNhay;
        this.camPitch -= e.movementY * doNhay * daoY;

        // Giới hạn pitch
        if (this.cheDoGocNhin === "goc_1") {
          this.camPitch = Math.max(-1.4, Math.min(1.4, this.camPitch)); // -80° đến +80°
        } else {
          this.camPitch = Math.max(-0.61, Math.min(1.05, this.camPitch)); // -35° đến +60°
        }
        this.thoiGianKhongXoayChuot = 0;
      } else if (this.chuotDangKeo) {
        // Fallback khi kéo chuột
        const doNhay = (this.caiDat.doNhay || 1.0) * 0.0035;
        const daoY = this.caiDat.daoY ? -1 : 1;
        const dx = e.clientX - this._cuX;
        const dy = e.clientY - this._cuY;
        this._cuX = e.clientX;
        this._cuY = e.clientY;

        this.camYaw -= dx * doNhay;
        this.camPitch -= dy * doNhay * daoY;

        if (this.cheDoGocNhin === "goc_1") {
          this.camPitch = Math.max(-1.4, Math.min(1.4, this.camPitch));
        } else {
          this.camPitch = Math.max(-0.61, Math.min(1.05, this.camPitch));
        }
        this.thoiGianKhongXoayChuot = 0;
      }
    });

    el.addEventListener("mousedown", (e) => {
      if (this.cheDoCamera === "tai_ban") return;
      if (!this.coKhoaChuot) {
        if (e.button === 0 || e.button === 2) {
          this.chuotDangKeo = true;
          this._cuX = e.clientX;
          this._cuY = e.clientY;
          this._clickGocX = e.clientX;
          this._clickGocY = e.clientY;
        }
      }
    });

    window.addEventListener("mouseup", (e) => {
      if (this.chuotDangKeo) {
        this.chuotDangKeo = false;
        // Nếu chỉ là cú click nhẹ và không kéo, thực hiện tìm đường sàn
        if (!this.coKhoaChuot && this._clickGocX !== undefined) {
          const dist = Math.hypot(e.clientX - this._clickGocX, e.clientY - this._clickGocY);
          if (dist < 6 && e.button === 0 && this.cheDoCamera !== "tai_ban") {
            this._diToiTheoToaDoManHinh(e.clientX, e.clientY);
          }
        }
      }
    });

    // Lăn chuột thu / phóng
    el.addEventListener("wheel", (e) => {
      if (this.cheDoCamera === "tai_ban") return;
      e.preventDefault();
      const delta = e.deltaY * 0.003;
      this.camKhoangCach = Math.max(1.0, Math.min(6.5, this.camKhoangCach + delta));

      // Tự chuyển sang góc nhìn 1 nếu lăn hết mức vào trong
      if (this.camKhoangCach <= 1.2 && this.cheDoGocNhin === "goc_3") {
        this.cheDoGocNhin = "goc_1";
      } else if (this.camKhoangCach > 1.4 && this.cheDoGocNhin === "goc_1") {
        this.cheDoGocNhin = "goc_3";
      }
    }, { passive: false });
  }

  _diToiTheoToaDoManHinh(clientX, clientY) {
    const rect = this.domKhung.getBoundingClientRect();
    const ndc = new THREE.Vector2(
      ((clientX - rect.left) / rect.width) * 2 - 1,
      -((clientY - rect.top) / rect.height) * 2 + 1
    );
    const ray = new THREE.Raycaster();
    ray.setFromCamera(ndc, this.camera);

    const matSan = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
    const diemGiao = new THREE.Vector3();
    if (ray.ray.intersectPlane(matSan, diemGiao)) {
      if (
        diemGiao.x >= 0.3 && diemGiao.x <= this.banDo.phong.rong - 0.3 &&
        diemGiao.z >= 0.3 && diemGiao.z <= this.banDo.phong.sau - 0.3
      ) {
        this.diToi([diemGiao.x, diemGiao.z]);
      }
    }
  }

  _khoiTaoJoystick() {
    this.joystickVec = { x: 0, y: 0 };
    this.joystickDangDung = false;

    const elJoy = document.getElementById("ptn-joystick");
    const elNut = document.getElementById("ptn-joystick-nut");
    if (!elJoy || !elNut) return;

    let startX = 0, startY = 0;
    const banKinh = 40;

    const capNhatNut = (cx, cy) => {
      let dx = cx - startX;
      let dy = cy - startY;
      const d = Math.hypot(dx, dy);
      if (d > banKinh) {
        dx = (dx / d) * banKinh;
        dy = (dy / d) * banKinh;
      }
      elNut.style.transform = `translate(${dx}px, ${dy}px)`;
      this.joystickVec = { x: dx / banKinh, y: -dy / banKinh };
    };

    elJoy.addEventListener("pointerdown", (e) => {
      this.joystickDangDung = true;
      elJoy.setPointerCapture(e.pointerId);
      const r = elJoy.getBoundingClientRect();
      startX = r.left + r.width / 2;
      startY = r.top + r.height / 2;
      capNhatNut(e.clientX, e.clientY);
    });

    elJoy.addEventListener("pointermove", (e) => {
      if (!this.joystickDangDung) return;
      capNhatNut(e.clientX, e.clientY);
    });

    const ketThuc = () => {
      this.joystickDangDung = false;
      elNut.style.transform = "translate(0px, 0px)";
      this.joystickVec = { x: 0, y: 0 };
    };

    elJoy.addEventListener("pointerup", ketThuc);
    elJoy.addEventListener("pointercancel", ketThuc);
  }

  // ---------------------------------------------------------------- TƯƠNG TÁC BÀN & ĐỒ VẬT
  _xuLyTuongTac() {
    // 1. Tủ đồ bảo hộ
    const tuBaoHo = this.banDo.noi_that?.find((n) => n.id === "tu-bao-ho");
    if (tuBaoHo) {
      const d = Math.hypot(this.viTri[0] - tuBaoHo.tam[0], this.viTri[1] - tuBaoHo.tam[1]);
      if (d <= 1.8) {
        this.macBaoHo();
        return;
      }
    }

    // 2. Trạm thí nghiệm
    const BanDoMod = (typeof window !== "undefined" && window.PTNBanDo) ? window.PTNBanDo : null;
    if (!BanDoMod) return;

    const gan = BanDoMod.tramGanNhat(this.banDo, this.viTri, 1.8);
    if (gan && gan.tram) {
      const id = gan.tram.tn || gan.tram.id;
      this.vaoTram(id);
    }
  }

  dichChuyen(x, z, huong) {
    if (Array.isArray(x)) {
      if (typeof z === "number") huong = z;
      z = x[1];
      x = x[0];
    }
    this.viTri[0] = x;
    this.viTri[1] = z;
    this.nhom.position.set(x, this.nhayY, z);
    this.bongMesh.position.set(x, 0.005, z);
    if (typeof huong === "number") {
      this.gocQuay = THREE.MathUtils.degToRad(huong);
      this.nhom.rotation.y = this.gocQuay;
      this.camYaw = this.gocQuay;
    }
    this.duongDi = [];
    if (this.vongDich) this.vongDich.visible = false;

    const BanDoMod = (typeof window !== "undefined" && window.PTNBanDo) ? window.PTNBanDo : null;
    if (BanDoMod) {
      const khu = BanDoMod.khuTai(this.banDo, this.viTri);
      if (khu) {
        this.khuHienTai = khu;
        this.onKhu?.(khu);
        this.onKhuVuc?.(khu);
      }
      const gan = BanDoMod.tramGanNhat(this.banDo, this.viTri, 1.8);
      this.tramGan = gan;
      this.onDenGan?.(gan);
      const idGan = gan ? (gan.tram?.tn || gan.tram?.id || gan.tn || null) : null;
      this.onTramGan?.(idGan);
    }
    this.onDoiViTri?.(this.viTri[0], this.viTri[1]);

    if (this._onDenDich) {
      const cb = this._onDenDich;
      this._onDenDich = null;
      cb();
    }
  }

  diToi(arg1, arg2, arg3) {
    let diemDich, cb = null, tucThi = false;
    if (typeof arg1 === "number" && typeof arg2 === "number") {
      diemDich = [arg1, arg2];
      if (typeof arg3 === "function") cb = arg3;
      else if (arg3 && typeof arg3 === "object") {
        cb = arg3.onXong || null;
        tucThi = !!arg3.tucThi;
      }
    } else if (Array.isArray(arg1)) {
      diemDich = [arg1[0], arg1[1]];
      if (typeof arg2 === "function") cb = arg2;
      else if (arg2 && typeof arg2 === "object") {
        cb = arg2.onXong || null;
        tucThi = !!arg2.tucThi;
      }
    } else {
      return;
    }

    if (tucThi) {
      this.dichChuyen(diemDich[0], diemDich[1]);
      if (cb) cb();
      return;
    }

    const BanDoMod = (typeof window !== "undefined" && window.PTNBanDo) ? window.PTNBanDo : null;
    if (!BanDoMod || !this.luoi) {
      this.dichChuyen(diemDich[0], diemDich[1]);
      if (cb) cb();
      return;
    }

    const duong = BanDoMod.timDuong(this.luoi, this.viTri, diemDich);
    if (duong && duong.length > 1) {
      this.duongDi = duong.slice(1);
      this.vongDich.position.set(diemDich[0], 0.006, diemDich[1]);
      this.vongDich.visible = true;
      this._onDenDich = cb;
    } else {
      this.dichChuyen(diemDich[0], diemDich[1]);
      if (cb) cb();
    }
  }

  vaoTram(id) {
    const tr = this.banDo.tram?.find((t) => t.tn === id || t.id === id);
    if (!tr) return;

    if (!this.daMacBaoHo) {
      this.onThongBao("⚠️ Bạn nên mặc áo blouse và kính bảo hộ tại Tủ đồ bảo hộ trước khi thao tác!");
    }

    // Nhả khoá chuột
    if (document.exitPointerLock && document.pointerLockElement) {
      document.exitPointerLock();
    }
    this.coKhoaChuot = false;
    if (this.elChamTam) this.elChamTam.classList.remove("hien");

    this.cheDoCamera = "tai_ban";
    this.duongDi = [];
    this.vongDich.visible = false;

    // Đặt nhân vật tại điểm dừng quay mặt vào bàn
    this.viTri[0] = tr.diem_dung[0];
    this.viTri[1] = tr.diem_dung[1];
    this.nhom.position.set(this.viTri[0], 0, this.viTri[1]);
    this.bongMesh.position.set(this.viTri[0], 0.005, this.viTri[1]);
    this.gocQuay = Math.atan2(tr.tam[0] - tr.diem_dung[0], tr.tam[1] - tr.diem_dung[1]);
    this.nhom.rotation.y = this.gocQuay;

    const tnId = tr.tn || tr.id;
    this.onVaoTram(tnId);
  }

  roiTram() {
    this.cheDoCamera = "theo_sau";
    this.cheDoGocNhin = this.gocNhinTruocDo || "goc_3";
    this.cooldownTuVaoBan = 3.5;
    this.thoiGianDungGanBan = 0;

    // Nhân vật lùi lại 0.6m để có không gian
    const luidx = -Math.sin(this.gocQuay) * 0.6;
    const luidz = -Math.cos(this.gocQuay) * 0.6;
    const BanDoMod = (typeof window !== "undefined" && window.PTNBanDo) ? window.PTNBanDo : null;
    if (BanDoMod) {
      const posMoi = BanDoMod.diChuyen(this.banDo, this.viTri, [luidx, luidz], 0.3);
      this.viTri[0] = posMoi[0];
      this.viTri[1] = posMoi[1];
    } else {
      this.viTri[0] += luidx;
      this.viTri[1] += luidz;
    }
    this.nhom.position.set(this.viTri[0], 0, this.viTri[1]);
    this.bongMesh.position.set(this.viTri[0], 0.005, this.viTri[1]);

    // Đặt camera sau lưng nhân vật nhìn theo hướng nhân vật
    this.camYaw = this.gocQuay;
    this.camPitch = 0.22;
    this.camKhoangCach = 3.2;

    this.onRoiTram();
  }

  // ---------------------------------------------------------------- CẬP NHẬT HOẠT HÌNH & CAMERA MỖI FRAME
  capNhat(dt, t) {
    if (this.cheDoCamera === "tai_ban") {
      this.tayT.rotation.x = -0.5;
      this.tayP.rotation.x = -0.5;
      this.chanT.rotation.x = 0;
      this.chanP.rotation.x = 0;
      this.than.position.y = 0.98 + Math.sin(t * 2) * 0.005;
      this.elBongBong.style.display = "none";
      return;
    }

    const BanDoMod = (typeof window !== "undefined" && window.PTNBanDo) ? window.PTNBanDo : null;
    const DieuKhienMod = (typeof window !== "undefined" && window.PTNDieuKhien) ? window.PTNDieuKhien : null;

    // 1. Nhảy Space & Trọng lực
    if (this.dangNhay) {
      this.vanTocY -= 18.0 * dt;
      this.nhayY += this.vanTocY * dt;
      if (this.nhayY <= 0) {
        this.nhayY = 0;
        this.vanTocY = 0;
        this.dangNhay = false;
      }
    }

    // 2. Tính toán di chuyển
    let buocX = 0, buocZ = 0;
    const ph = this.phim;
    const coPhim = ph.w || ph.s || ph.a || ph.d || ph.arrowup || ph.arrowdown || ph.arrowleft || ph.arrowright;

    let tocDoMoi = 0;
    if (ph.shift) tocDoMoi = 3.4; // Chạy nhanh
    else if (ph.ctrl) tocDoMoi = 1.0; // Đi chậm
    else tocDoMoi = 1.6; // Đi bộ bình thường

    if (coPhim && DieuKhienMod) {
      const vec = DieuKhienMod.huongDiChuyen(ph, this.camYaw);
      if (vec[0] !== 0 || vec[1] !== 0) {
        // Gia tốc mượt
        this.tocDoHienTai = THREE.MathUtils.lerp(this.tocDoHienTai, tocDoMoi, dt * 8);
        buocX = vec[0] * this.tocDoHienTai * dt;
        buocZ = vec[1] * this.tocDoHienTai * dt;

        // Quay mặt nhân vật mượt theo hướng chạy
        const gocMoi = Math.atan2(vec[0], vec[1]);
        this.gocQuay = DieuKhienMod.quayMuot(this.gocQuay, gocMoi, 10.0, dt);
        this.nhom.rotation.y = this.gocQuay;
      }
    } else if (this.joystickDangDung && DieuKhienMod) {
      // Joystick điện thoại
      const mx = this.joystickVec.x;
      const mz = this.joystickVec.y;
      const len = Math.hypot(mx, mz);
      if (len > 0.05) {
        this.tocDoHienTai = THREE.MathUtils.lerp(this.tocDoHienTai, tocDoMoi * Math.min(1, len), dt * 8);
        const fx = -Math.sin(this.camYaw);
        const fz = -Math.cos(this.camYaw);
        const rx = Math.cos(this.camYaw);
        const rz = -Math.sin(this.camYaw);
        const vx = (mx / len) * rx + (mz / len) * fx;
        const vz = (mx / len) * rz + (mz / len) * fz;
        buocX = vx * this.tocDoHienTai * dt;
        buocZ = vz * this.tocDoHienTai * dt;

        const gocMoi = Math.atan2(vx, vz);
        this.gocQuay = DieuKhienMod.quayMuot(this.gocQuay, gocMoi, 10.0, dt);
        this.nhom.rotation.y = this.gocQuay;
      }
    } else if (this.duongDi.length > 0) {
      // Tự đi theo click sàn
      const mucTieu = this.duongDi[0];
      const toiX = mucTieu[0] - this.viTri[0];
      const toiZ = mucTieu[1] - this.viTri[1];
      const dist = Math.hypot(toiX, toiZ);

      if (dist < 0.15) {
        this.duongDi.shift();
        if (this.duongDi.length === 0) {
          this.vongDich.visible = false;
          if (this._onDenDich) {
            const cb = this._onDenDich;
            this._onDenDich = null;
            cb();
          }
        }
      } else {
        this.tocDoHienTai = THREE.MathUtils.lerp(this.tocDoHienTai, tocDoMoi, dt * 8);
        const huongX = toiX / dist;
        const huongZ = toiZ / dist;
        buocX = huongX * Math.min(dist, this.tocDoHienTai * dt);
        buocZ = huongZ * Math.min(dist, this.tocDoHienTai * dt);

        if (DieuKhienMod) {
          const gocMoi = Math.atan2(huongX, huongZ);
          this.gocQuay = DieuKhienMod.quayMuot(this.gocQuay, gocMoi, 10.0, dt);
          this.nhom.rotation.y = this.gocQuay;
        }
      }
    } else {
      // Phanh dừng lại
      this.tocDoHienTai = THREE.MathUtils.lerp(this.tocDoHienTai, 0, dt * 12);
    }

    // Tự xoay camera sau 1.5s đi mà không xoay chuột
    if (this.tocDoHienTai > 0.5) {
      this.thoiGianKhongXoayChuot += dt;
      if (this.caiDat.tuXoayCam && this.thoiGianKhongXoayChuot >= 1.5 && DieuKhienMod) {
        this.camYaw = DieuKhienMod.quayMuot(this.camYaw, this.gocQuay, 1.8, dt);
      }
    } else {
      this.thoiGianKhongXoayChuot = 0;
    }

    // 3. Thực hiện di chuyển có kiểm tra va chạm
    if (buocX !== 0 || buocZ !== 0) {
      if (BanDoMod) {
        const viTriMoi = BanDoMod.diChuyen(this.banDo, this.viTri, [buocX, buocZ], 0.3);
        this.viTri[0] = viTriMoi[0];
        this.viTri[1] = viTriMoi[1];
      } else {
        this.viTri[0] += buocX;
        this.viTri[1] += buocZ;
      }
    }

    // Cập nhật vị trí mesh
    this.nhom.position.set(this.viTri[0], this.nhayY, this.viTri[1]);
    this.bongMesh.position.set(this.viTri[0], 0.005, this.viTri[1]);

    // 4. Hoạt ảnh bước chân & tay vung
    if (this.tocDoHienTai > 0.1 && !this.dangNhay) {
      const tanSo = this.tocDoHienTai > 2.5 ? 12 : 8;
      this.phaBuoc += dt * tanSo;
      const gocVung = (this.tocDoHienTai > 2.5 ? 0.65 : 0.45) * Math.sin(this.phaBuoc);

      this.chanT.rotation.x = gocVung;
      this.chanP.rotation.x = -gocVung;
      this.tayT.rotation.x = -gocVung * 0.9;
      this.tayP.rotation.x = gocVung * 0.9;

      // Người nhún nhẹ & đổ về trước khi chạy nhanh
      this.than.position.y = 0.98 + Math.abs(Math.sin(this.phaBuoc)) * 0.02;
      this.than.rotation.x = this.tocDoHienTai > 2.5 ? 0.15 : 0.05;
    } else if (!this.dangNhay) {
      // Đứng yên thở nhẹ
      this.chanT.rotation.x = 0;
      this.chanP.rotation.x = 0;
      this.tayT.rotation.x = THREE.MathUtils.lerp(this.tayT.rotation.x, 0, dt * 10);
      this.tayP.rotation.x = THREE.MathUtils.lerp(this.tayP.rotation.x, 0, dt * 10);
      this.than.position.y = 0.98 + Math.sin(t * 2) * 0.008;
      this.than.rotation.x = 0;
    }

    // 5. Kiểm tra phòng con / khu vực & bong bóng tương tác
    if (BanDoMod) {
      const khu = BanDoMod.khuTai(this.banDo, this.viTri);
      if (khu && khu !== this.khuHienTai) {
        this.khuHienTai = khu;
        this.onKhu(khu);
        this.onKhuVuc?.(khu);
      }

      const gan = BanDoMod.tramGanNhat(this.banDo, this.viTri, 1.8);
      this.tramGan = gan;
      this.onDenGan(gan);
      const idGan = gan ? (gan.tram?.tn || gan.tram?.id || gan.tn || null) : null;
      this.onTramGan?.(idGan);

      if (gan && gan.tram) {
        this.elBongBong.style.display = "";
        const tenTr = gan.tram.tn || gan.tram.id;
        this.elBongBong.innerHTML = `<b style="color:var(--nhan)">E</b> – Vào bàn <i>${tenTr}</i>`;

        // Tự động vào bàn khi dừng chân 0.6s (không tự hút vào bàn khi vừa tải trang ở điểm xuất phát hoặc vừa rời bàn)
        if (this.cooldownTuVaoBan > 0) {
          this.cooldownTuVaoBan -= dt;
        }
        const dXuatPhat = Math.hypot(
          this.viTri[0] - (this.banDo?.xuat_phat?.x ?? 11),
          this.viTri[1] - (this.banDo?.xuat_phat?.z ?? 13.6)
        );
        const oDiemXuatPhat = dXuatPhat < 0.35;
        if (!oDiemXuatPhat && this.cooldownTuVaoBan <= 0 && gan.khoangCach <= 1.3 && this.tocDoHienTai < 0.15 && !this.dangNhay) {
          this.thoiGianDungGanBan += dt;
          if (this.caiDat.tuVaoBan && this.thoiGianDungGanBan >= 0.6) {
            this.thoiGianDungGanBan = 0;
            this.vaoTram(tenTr);
            return;
          }
        } else {
          this.thoiGianDungGanBan = 0;
        }
      } else {
        this.elBongBong.style.display = "none";
        this.thoiGianDungGanBan = 0;
      }
    }

    // 6. Cập nhật camera
    this._capNhatCamera(dt);
  }

  // ---------------------------------------------------------------- CAMERA UPDATE
  _capNhatCamera(dt) {
    if (this.cheDoCamera === "tai_ban") return;

    const DieuKhienMod = (typeof window !== "undefined" && window.PTNDieuKhien) ? window.PTNDieuKhien : null;
    const BanDoMod = (typeof window !== "undefined" && window.PTNBanDo) ? window.PTNBanDo : null;

    // A. Chế độ Toàn cảnh nhà búp bê (Phím V)
    if (this.cheDoCamera === "toan_canh") {
      this.nhom.visible = true;
      // Vị trí nhìn chéo 50° từ trên cao xuống bao quát 5 phòng
      const tamNhin = V3(11, 0.9, 7.5);
      const camDich = V3(11, 13.5, 15.5);
      this.camera.position.lerp(camDich, Math.min(1, dt * 6));
      this.camera.lookAt(tamNhin.x, tamNhin.y, tamNhin.z);
      return;
    }

    const nvX = this.viTri[0];
    const nvZ = this.viTri[1];

    // B. Chế độ Góc nhìn thứ nhất (F)
    if (this.cheDoGocNhin === "goc_1") {
      this.nhom.visible = false;
      const matY = 1.55 + this.nhayY;
      this.camera.position.set(nvX, matY, nvZ);

      // Vector hướng nhìn từ yaw và pitch
      const fx = -Math.sin(this.camYaw) * Math.cos(this.camPitch);
      const fy = Math.sin(this.camPitch);
      const fz = -Math.cos(this.camYaw) * Math.cos(this.camPitch);

      this.camera.lookAt(nvX + fx, matY + fy, nvZ + fz);
      return;
    }

    // C. Chế độ Góc nhìn thứ ba (vai nhân vật)
    this.nhom.visible = true;

    // Điểm nhìn ở vai phải nhân vật
    const vaiR = 0.3;
    const rx = Math.cos(this.camYaw);
    const rz = -Math.sin(this.camYaw);
    const diemNhin = [nvX + vaiR * rx, 1.45 + this.nhayY, nvZ + vaiR * rz];

    if (!DieuKhienMod) {
      // Fallback nếu chưa nạp DieuKhienMod
      this.camera.position.set(nvX, 2.5, nvZ + 3.5);
      this.camera.lookAt(diemNhin[0], diemNhin[1], diemNhin[2]);
      return;
    }

    // Tính vị trí camera mong muốn khi chưa có vật cản
    const viTriMuon = DieuKhienMod.viTriCamera(this.camYaw, this.camPitch, this.camKhoangCach, diemNhin);

    // Bắn tia kiểm tra tay đòn chống xuyên tường
    if (!this.hopChanCamera || this.hopChanCamera.length === 0) {
      this.hopChanCamera = BanDoMod ? BanDoMod.layVatChanCamera(this.banDo) : [];
    }

    const td = DieuKhienMod.tayDon(diemNhin, viTriMuon, this.hopChanCamera, 0.2, 0.5);

    if (td.biChan) {
      // Co ngay lập tức tránh xuyên tường
      this.camKhoangCachHienTai = td.khoangCach;
    } else {
      // Giãn từ từ về khoảng cách mong muốn
      this.camKhoangCachHienTai = THREE.MathUtils.lerp(this.camKhoangCachHienTai, this.camKhoangCach, Math.min(1, dt * 5));
    }

    // Vị trí camera thực tế sau khi co/giãn tay đòn
    const camThuc = DieuKhienMod.viTriCamera(this.camYaw, this.camPitch, this.camKhoangCachHienTai, diemNhin);

    this.camera.position.set(camThuc[0], camThuc[1], camThuc[2]);
    this.camera.lookAt(diemNhin[0], diemNhin[1], diemNhin[2]);

    // Mờ dần nhân vật nếu camera quá sát (< 0.9m)
    if (this.camKhoangCachHienTai < 0.9) {
      const op = Math.max(0.25, (this.camKhoangCachHienTai - 0.4) / 0.5);
      for (const m of this._vatLieuNhanVat) {
        m.transparent = true;
        m.opacity = op;
      }
    } else {
      for (const m of this._vatLieuNhanVat) {
        m.transparent = false;
        m.opacity = 1.0;
      }
    }
  }

  // ---------------------------------------------------------------- HOOK KIỂM THỬ
  kiemTraThayNhanVat() {
    if (this.cheDoCamera === "tai_ban" || this.cheDoGocNhin === "goc_1") return false;
    const dauPos = V3(this.viTri[0], 1.5, this.viTri[1]);
    const ndc = dauPos.clone().project(this.camera);

    const trongKhung = ndc.x >= -0.9 && ndc.x <= 0.9 && ndc.y >= -0.9 && ndc.y <= 0.9 && ndc.z < 1.0;
    // Kiểm tra camera ở sau lưng nhân vật (khoảng cách > 0.4m)
    const dist = this.camera.position.distanceTo(dauPos);
    return trongKhung && dist >= 0.4;
  }

  chuyenCheDoTongQuan(bat) {
    if (bat === undefined) {
      bat = (this.cheDoCamera !== "toan_canh");
    }
    if (bat) {
      if (this.cheDoCamera === "toan_canh") return;
      this.gocNhinTruocDo = this.cheDoGocNhin;
      this.cheDoCamera = "toan_canh";
      this.onTongQuan(true);
    } else {
      if (this.cheDoCamera !== "toan_canh") return;
      this.cheDoCamera = "theo_sau";
      this.cheDoGocNhin = this.gocNhinTruocDo || "goc_3";
      this.onTongQuan(false);
    }
  }

  datGocNhin(kieu) {
    if (kieu === "toan_canh" || kieu === "bup_be") {
      this.chuyenCheDoTongQuan(true);
    } else if (kieu === "goc_1" || kieu === "ngoi_nhat") {
      this.chuyenCheDoTongQuan(false);
      this.cheDoGocNhin = "goc_1";
    } else if (kieu === "goc_3" || kieu === "theo_sau" || kieu === "ngoi_ba") {
      this.chuyenCheDoTongQuan(false);
      this.cheDoGocNhin = "goc_3";
    }
  }

  dieuKhienCamera(lenh) {
    if (lenh === "trai") this.camYaw -= 0.2;
    else if (lenh === "phai") this.camYaw += 0.2;
    else if (lenh === "len") this.camPitch = Math.min(1.2, this.camPitch + 0.1);
    else if (lenh === "xuong") this.camPitch = Math.max(-0.6, this.camPitch - 0.1);
    else if (lenh === "gan") this.camKhoangCach = Math.max(1.8, this.camKhoangCach - 0.4);
    else if (lenh === "xa") this.camKhoangCach = Math.min(6.0, this.camKhoangCach + 0.4);
    else if (lenh === "dat_lai") {
      this.camYaw = this.gocQuay;
      this.camPitch = 0.22;
      this.camKhoangCach = 3.2;
    }
  }

  capNhatCaiDat(cd = {}) {
    Object.assign(this.caiDat, cd);
    if (cd.doNhay !== undefined) localStorage.setItem("ptn_do_nhay_chuot", String(cd.doNhay));
    if (cd.daoY !== undefined) localStorage.setItem("ptn_dao_truc_y", cd.daoY ? "1" : "0");
    if (cd.tuXoayCam !== undefined) localStorage.setItem("ptn_tu_xoay_cam", cd.tuXoayCam ? "1" : "0");
    if (cd.gocNhinMacDinh !== undefined) {
      localStorage.setItem("ptn_goc_nhin_mac_dinh", cd.gocNhinMacDinh);
      this.cheDoGocNhin = cd.gocNhinMacDinh;
    }
    if (cd.tuVaoBan !== undefined) localStorage.setItem("ptn_tu_vao_ban", cd.tuVaoBan ? "1" : "0");
  }

  huy() {
    this.scene.remove(this.nhom);
    this.scene.remove(this.bongMesh);
    this.scene.remove(this.vongDich);
  }
}
