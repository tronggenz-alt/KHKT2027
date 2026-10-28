/* Phòng thí nghiệm Hoá học ảo – DỰNG CĂN PHÒNG 3D KHÔNG GIAN MỞ & PHÒNG CON (phong-mo.js)
   Sàn vinyl xám lạnh, tường sáng viền chân tường, trần âm đèn panel, 5 phòng lab con vách kính
   khung nhôm 2.4m có lối vào và biển tên trên cửa (không còn biển lơ lửng), bàn lab Hoá - Sinh
   mặt xanh lam gờ chắn 8cm + tủ gỗ sáng + chậu rửa inox vòi cổ ngỗng + giá thuốc thử + ghế đẩu,
   tủ hút khí độc Lý Sơn Sa Kỳ, góc nhìn toàn cảnh nhà búp bê (cắt trần + ẩn tường Nam). */

import * as THREE from "three";
import { RoundedBoxGeometry } from "three/addons/geometries/RoundedBoxGeometry.js";
import { nangCapPhong } from "ptn/noi-that-thuc";

const V3 = (x, y, z) => new THREE.Vector3(x, y, z);
const V2 = (x, y) => new THREE.Vector2(x, y);

/** Đọc màu từ biến CSS của web để đồng bộ giao diện */
function docMauCSS(bien, macDinh = "#0f2b48") {
  if (typeof window === "undefined" || !window.getComputedStyle) return macDinh;
  const v = getComputedStyle(document.documentElement).getPropertyValue(bien).trim();
  return v || macDinh;
}

export class PhongMo {
  /**
   * @param {THREE.Scene} scene - Scene Three.js
   * @param {Object} banDo - Dữ liệu ban-do.json
   * @param {string} [chatLuong="cao"] - "cao" | "thap"
   * @param {Function} [onTienDo] - Callback tiến độ tải
   */
  constructor(scene, banDo, chatLuong = "cao", onTienDo = () => {}) {
    this.scene = scene;
    this.banDo = banDo;
    this.chatLuong = chatLuong;
    this.onTienDo = onTienDo;
    this.goc = new THREE.Group();
    this.scene.add(this.goc);

    this._taiNguyen = new Set();
    this._doCao = [];
    this._vatLieuKhu = {};
    this._bienCuaTextures = [];
    this._denPanel = [];

    this._dungPhong();
  }

  _theo(o) {
    this._taiNguyen.add(o);
    return o;
  }

  _dungPhong() {
    const bd = this.banDo;
    const { rong = 22, sau = 16, cao = 4.2 } = bd.phong || {};

    this.onTienDo("Đang dựng sàn và trần phòng…");
    this._dungSanVaTran(rong, sau, cao);

    this.onTienDo("Đang dựng tường và cửa sổ…");
    this._dungTuong(rong, sau, cao);

    this.onTienDo("Đang dựng vách kính các phòng lab con…");
    this._dungPhongCon();

    this.onTienDo("Đang dựng bàn thí nghiệm Hoá – Sinh tiêu chuẩn…");
    this._dungBanThiNghiem();

    this.onTienDo("Đang bố trí nội thất lab: tủ hút, tủ hoá chất, bồn rửa…");
    this._dungNoiThat();

    this.onTienDo("Đang hoàn thiện vật liệu và chi tiết như phòng thật…");
    nangCapPhong(this);   // noi-that-thuc.js: texture sàn / bàn / gỗ / tường / trần + khe tủ, vòi nước, lọ thuốc, khung cửa sổ… (bỏ dòng này = bản cũ)

    this.doiGiaoDien();
  }

  // ---------------------------------------------------------------- SÀN VÀ TRẦN
  _dungSanVaTran(rong, sau, cao) {
    // 1. Sàn vinyl xám lạnh chuẩn phòng thí nghiệm (đường ron mờ, không trơn trượt)
    const canvasSan = document.createElement("canvas");
    canvasSan.width = 512;
    canvasSan.height = 512;
    const ctxSan = canvasSan.getContext("2d");
    ctxSan.fillStyle = "#cbd5e1";
    ctxSan.fillRect(0, 0, 512, 512);

    // Lưới ron gạch vinyl 60x60
    ctxSan.strokeStyle = "rgba(100, 116, 139, 0.25)";
    ctxSan.lineWidth = 2;
    for (let i = 0; i <= 512; i += 64) {
      ctxSan.beginPath();
      ctxSan.moveTo(i, 0); ctxSan.lineTo(i, 512);
      ctxSan.moveTo(0, i); ctxSan.lineTo(512, i);
      ctxSan.stroke();
    }
    // Hạt chống trượt li ti
    for (let i = 0; i < 600; i++) {
      const px = Math.random() * 512;
      const py = Math.random() * 512;
      ctxSan.fillStyle = Math.random() > 0.5 ? "rgba(255,255,255,0.4)" : "rgba(71,85,105,0.2)";
      ctxSan.fillRect(px, py, 2, 2);
    }

    this.texSan = this._theo(new THREE.CanvasTexture(canvasSan));
    this.texSan.wrapS = THREE.RepeatWrapping;
    this.texSan.wrapT = THREE.RepeatWrapping;
    this.texSan.repeat.set(rong / 2.0, sau / 2.0);

    this.vlSan = this._theo(new THREE.MeshStandardMaterial({
      map: this.texSan,
      roughness: 0.65,
      metalness: 0.08
    }));

    const geoSan = this._theo(new THREE.PlaneGeometry(rong, sau));
    this.sanMesh = new THREE.Mesh(geoSan, this.vlSan);
    this.sanMesh.rotation.x = -Math.PI / 2;
    this.sanMesh.position.set(rong / 2, 0, sau / 2);
    this.sanMesh.receiveShadow = true;
    this.goc.add(this.sanMesh);

    // 2. Viền phân khu vực trên sàn bằng màu sắc nhẹ
    if (Array.isArray(this.banDo.khu)) {
      for (const k of this.banDo.khu) {
        const [x0, z0, x1, z1] = k.hcn;
        const kw = x1 - x0;
        const kd = z1 - z0;
        const mauKhu = docMauCSS(k.mau, "#38bdf8");

        const vlKhu = this._theo(new THREE.MeshBasicMaterial({
          color: mauKhu,
          transparent: true,
          opacity: 0.12,
          depthWrite: false
        }));
        this._vatLieuKhu[k.id] = { vl: vlKhu, bien: k.mau };

        const miengKhu = new THREE.Mesh(this._theo(new THREE.PlaneGeometry(kw - 0.1, kd - 0.1)), vlKhu);
        miengKhu.rotation.x = -Math.PI / 2;
        miengKhu.position.set(x0 + kw / 2, 0.002, z0 + kd / 2);
        this.goc.add(miengKhu);
      }
    }

    // 3. Trần thạch cao âm đèn panel
    const geoTran = this._theo(new THREE.PlaneGeometry(rong, sau));
    this.vlTran = this._theo(new THREE.MeshStandardMaterial({
      color: 0xf8fafc,
      roughness: 0.95,
      side: THREE.BackSide
    }));
    this.tran = new THREE.Mesh(geoTran, this.vlTran);
    this.tran.rotation.x = Math.PI / 2;
    this.tran.position.set(rong / 2, cao, sau / 2);
    this.goc.add(this.tran);

    // Đèn panel âm trần theo từng phòng
    this.vlDenTran = this._theo(new THREE.MeshStandardMaterial({
      color: 0xffffff,
      emissive: 0xffffff,
      emissiveIntensity: 0.95,
      roughness: 0.2
    }));
    const geoTamDen = this._theo(new THREE.BoxGeometry(1.6, 0.05, 0.6));
    const viTriDen = [
      [3.5, cao - 0.02, 4], [3.5, cao - 0.02, 11.5],
      [11, cao - 0.02, 3], [11, cao - 0.02, 7.5], [11, cao - 0.02, 13.5],
      [18.5, cao - 0.02, 4.5], [18.5, cao - 0.02, 10.5]
    ];
    for (const [dx, dy, dz] of viTriDen) {
      const denMesh = new THREE.Mesh(geoTamDen, this.vlDenTran);
      denMesh.position.set(dx, dy, dz);
      this.goc.add(denMesh);
      this._denPanel.push(denMesh);
    }
  }

  // ---------------------------------------------------------------- TƯỜNG VÀ CỬA
  _dungTuong(rong, sau, cao) {
    this.vlTuong = this._theo(new THREE.MeshStandardMaterial({
      color: 0xf8fafc,
      roughness: 0.9,
      metalness: 0.02
    }));
    this.vlChanTuong = this._theo(new THREE.MeshStandardMaterial({
      color: 0x475569, // Chân tường xám đậm
      roughness: 0.6
    }));

    // Tường Tây (x = 0)
    const tuongTay = new THREE.Mesh(this._theo(new THREE.BoxGeometry(0.2, cao, sau)), this.vlTuong);
    tuongTay.position.set(-0.1, cao / 2, sau / 2);
    tuongTay.receiveShadow = true;
    this.goc.add(tuongTay);

    // Tường Đông (x = rong)
    const tuongDong = new THREE.Mesh(this._theo(new THREE.BoxGeometry(0.2, cao, sau)), this.vlTuong);
    tuongDong.position.set(rong + 0.1, cao / 2, sau / 2);
    tuongDong.receiveShadow = true;
    this.goc.add(tuongDong);

    // Tường Nam (z = sau, có cửa chính ở giữa x=11, rộng 2.4 m)
    this.tuongNam = new THREE.Group();
    const cuaRong = 2.4;
    const rongCanhNam = (rong - cuaRong) / 2;
    const namT = new THREE.Mesh(this._theo(new THREE.BoxGeometry(rongCanhNam, cao, 0.2)), this.vlTuong);
    namT.position.set(rongCanhNam / 2, cao / 2, sau + 0.1);
    this.tuongNam.add(namT);

    const namP = new THREE.Mesh(this._theo(new THREE.BoxGeometry(rongCanhNam, cao, 0.2)), this.vlTuong);
    namP.position.set(rong - rongCanhNam / 2, cao / 2, sau + 0.1);
    this.tuongNam.add(namP);

    // Xà cửa chính
    const xaCua = new THREE.Mesh(this._theo(new THREE.BoxGeometry(cuaRong, cao - 2.8, 0.2)), this.vlTuong);
    xaCua.position.set(11, cao - (cao - 2.8) / 2, sau + 0.1);
    this.tuongNam.add(xaCua);

    this.goc.add(this.tuongNam);

    // Tường Bắc (z = 0, có cửa sổ lớn đón ánh sáng tự nhiên)
    const tuongBac = new THREE.Group();
    // Bệ dưới cửa sổ
    const beBac = new THREE.Mesh(this._theo(new THREE.BoxGeometry(rong, 1.0, 0.3)), this.vlTuong);
    beBac.position.set(rong / 2, 0.5, -0.15);
    tuongBac.add(beBac);

    // Trán tường trên cửa sổ
    const tranBac = new THREE.Mesh(this._theo(new THREE.BoxGeometry(rong, cao - 3.6, 0.3)), this.vlTuong);
    tranBac.position.set(rong / 2, 3.6 + (cao - 3.6) / 2, -0.15);
    tuongBac.add(tranBac);

    // Kính cửa sổ nhìn ra trời mây
    const vlKinhCuaSo = this._theo(new THREE.MeshStandardMaterial({
      color: 0x93c5fd,
      roughness: 0.1,
      metalness: 0.1,
      transparent: true,
      opacity: 0.35
    }));
    const kinhCuaSo = new THREE.Mesh(this._theo(new THREE.PlaneGeometry(rong - 2, 2.6)), vlKinhCuaSo);
    kinhCuaSo.position.set(rong / 2, 2.3, -0.15);
    tuongBac.add(kinhCuaSo);

    // Tranh cảnh bầu trời và cây xanh ngoài cửa sổ
    this._veBauTroi(false);
    this.goc.add(tuongBac);
  }

  _veBauTroi(toi = false) {
    if (!this.bauTroiMesh) {
      const c = document.createElement("canvas");
      c.width = 1024; c.height = 256;
      this.ctxBtroi = c.getContext("2d");
      this.texBtroi = this._theo(new THREE.CanvasTexture(c));
      this.texBtroi.colorSpace = THREE.SRGBColorSpace;
      const mat = this._theo(new THREE.MeshBasicMaterial({ map: this.texBtroi }));
      this.bauTroiMesh = new THREE.Mesh(this._theo(new THREE.PlaneGeometry(24, 6)), mat);
      this.bauTroiMesh.position.set(11, 2.5, -1.8);
      this.goc.add(this.bauTroiMesh);
    }
    const g = this.ctxBtroi;
    if (!g) return;
    const grad = g.createLinearGradient(0, 0, 0, 256);
    if (toi) {
      grad.addColorStop(0, "#020617");
      grad.addColorStop(1, "#0f172a");
    } else {
      grad.addColorStop(0, "#38bdf8");
      grad.addColorStop(0.7, "#bae6fd");
      grad.addColorStop(1, "#f0fdf4");
    }
    g.fillStyle = grad;
    g.fillRect(0, 0, 1024, 256);

    // Cây cối xanh mờ ngoài sân
    g.fillStyle = toi ? "rgba(15,23,42,0.8)" : "rgba(34, 197, 94, 0.4)";
    for (let i = 0; i < 20; i++) {
      g.beginPath();
      g.arc(i * 55 + 20, 240, 45, 0, Math.PI * 2);
      g.fill();
    }
  }

  // ---------------------------------------------------------------- VÁCH KÍNH CÁC PHÒNG CON
  _dungPhongCon() {
    this.vlVachKinh = this._theo(new THREE.MeshStandardMaterial({
      color: 0x94a3b8,
      roughness: 0.1,
      metalness: 0.1,
      transparent: true,
      opacity: 0.32
    }));

    this.vlKhungNhom = this._theo(new THREE.MeshStandardMaterial({
      color: 0x334155, // Nhôm xám đậm
      roughness: 0.35,
      metalness: 0.8
    }));

    if (!Array.isArray(this.banDo.phong_con)) return;

    for (const pc of this.banDo.phong_con) {
      const nhomPC = new THREE.Group();

      // Dựng các đoạn vách kính
      if (Array.isArray(pc.vach)) {
        for (const v of pc.vach) {
          const dx = v.x2 - v.x1;
          const dz = v.z2 - v.z1;
          const len = Math.hypot(dx, dz);
          const caoVach = v.cao || 2.4;
          const day = v.day || 0.12;

          const cx = (v.x1 + v.x2) / 2;
          const cz = (v.z1 + v.z2) / 2;
          const ang = Math.atan2(dx, dz);

          const seg = new THREE.Group();
          seg.position.set(cx, 0, cz);
          seg.rotation.y = ang;

          // Tấm kính
          const kinh = new THREE.Mesh(this._theo(new THREE.BoxGeometry(0.02, caoVach - 0.1, len - 0.04)), this.vlVachKinh);
          kinh.position.y = caoVach / 2;
          seg.add(kinh);

          // Khung nhôm đế sàn và đỉnh
          const deNhom = new THREE.Mesh(this._theo(new THREE.BoxGeometry(day, 0.08, len)), this.vlKhungNhom);
          deNhom.position.y = 0.04;
          seg.add(deNhom);

          const dinhNhom = new THREE.Mesh(this._theo(new THREE.BoxGeometry(day, 0.06, len)), this.vlKhungNhom);
          dinhNhom.position.y = caoVach - 0.03;
          seg.add(dinhNhom);

          nhomPC.add(seg);
        }
      }

      // Khung cửa và biển tên trên lối vào
      if (pc.loi_vao) {
        const lv = pc.loi_vao;
        const rongCua = lv.rong || 2.0;
        const caoCua = 2.4;

        // Khung cửa: 2 cột và 1 xà ngang
        const ngangCua = lv.x === 11.0; // Lối vào cửa Bắc/Nam
        const angCua = ngangCua ? Math.PI / 2 : 0;

        const cuaGroup = new THREE.Group();
        cuaGroup.position.set(lv.x, 0, lv.z);
        cuaGroup.rotation.y = angCua;

        // 2 cột khung nhôm 2 bên cửa
        const cot1 = new THREE.Mesh(this._theo(new THREE.BoxGeometry(0.12, caoCua, 0.12)), this.vlKhungNhom);
        cot1.position.set(0, caoCua / 2, -rongCua / 2);
        cuaGroup.add(cot1);

        const cot2 = new THREE.Mesh(this._theo(new THREE.BoxGeometry(0.12, caoCua, 0.12)), this.vlKhungNhom);
        cot2.position.set(0, caoCua / 2, rongCua / 2);
        cuaGroup.add(cot2);

        // Xà ngang trên cửa
        const xaNgang = new THREE.Mesh(this._theo(new THREE.BoxGeometry(0.14, 0.08, rongCua)), this.vlKhungNhom);
        xaNgang.position.set(0, caoCua - 0.04, 0);
        cuaGroup.add(xaNgang);

        // Biển tên phòng con gắn trực tiếp trên khung cửa (rộng <= 1.6m, cao 0.35m)
        const c = document.createElement("canvas");
        c.width = 512; c.height = 128;
        const g = c.getContext("2d");

        const veBien = () => {
          g.clearRect(0, 0, 512, 128);
          // Nền mica bo góc
          g.fillStyle = "rgba(15, 23, 42, 0.92)";
          g.beginPath();
          g.roundRect(8, 8, 496, 112, 16);
          g.fill();

          // Viền màu khu vực
          const khuObj = this.banDo.khu?.find((k) => k.id === pc.khu);
          const mauKhu = khuObj ? docMauCSS(khuObj.mau, "#38bdf8") : "#38bdf8";
          g.strokeStyle = mauKhu;
          g.lineWidth = 4;
          g.stroke();

          // Biểu tượng chấm tròn màu
          g.fillStyle = mauKhu;
          g.beginPath();
          g.arc(36, 64, 12, 0, Math.PI * 2);
          g.fill();

          // Tên phòng con
          g.fillStyle = "#ffffff";
          g.font = '700 28px "Be Vietnam Pro", sans-serif';
          g.textAlign = "left";
          g.textBaseline = "middle";
          g.fillText(pc.ten, 64, 64);
        };

        veBien();
        if (document.fonts?.ready) {
          document.fonts.ready.then(() => {
            veBien();
            texBien.needsUpdate = true;
          });
        }

        const texBien = this._theo(new THREE.CanvasTexture(c));
        texBien.colorSpace = THREE.SRGBColorSpace;
        this._bienCuaTextures.push({ tex: texBien, ve: veBien });

        const matBien = this._theo(new THREE.MeshBasicMaterial({
          map: texBien,
          side: THREE.DoubleSide
        }));
        const bienMesh = new THREE.Mesh(this._theo(new THREE.PlaneGeometry(Math.min(1.6, rongCua - 0.2), 0.38)), matBien);
        bienMesh.rotation.y = Math.PI / 2;
        bienMesh.position.set(0, caoCua + 0.2, 0);
        cuaGroup.add(bienMesh);

        nhomPC.add(cuaGroup);
      }

      this.goc.add(nhomPC);
    }
  }

  // ---------------------------------------------------------------- BÀN THÍ NGHIỆM TIÊU CHUẨN
  _dungBanThiNghiem() {
    // 1. Mặt bàn phenolic xanh lam chuẩn phòng lab Hoá - Sinh (Hòa Phát)
    this.vlMocBan = this._theo(new THREE.MeshStandardMaterial({
      color: 0x1d4ed8, // Xanh lam đậm phenolic
      roughness: 0.35,
      metalness: 0.05
    }));

    // Khung thép hộp sơn xám đậm
    this.vlChanThep = this._theo(new THREE.MeshStandardMaterial({
      color: 0x475569,
      roughness: 0.45,
      metalness: 0.75
    }));

    // 2. Texture vân gỗ sồi sáng tự nhiên (Light Oak) cho thân tủ bàn
    const cGo = document.createElement("canvas");
    cGo.width = 256; cGo.height = 256;
    const ctxGo = cGo.getContext("2d");
    ctxGo.fillStyle = "#fef3c7"; // Nền gỗ sáng nhạt
    ctxGo.fillRect(0, 0, 256, 256);
    // Thớ gỗ dọc
    for (let x = 0; x < 256; x += 4) {
      ctxGo.fillStyle = Math.random() > 0.5 ? "rgba(217, 119, 6, 0.12)" : "rgba(180, 83, 9, 0.06)";
      ctxGo.fillRect(x, 0, Math.random() * 3 + 1, 256);
    }
    const texGo = this._theo(new THREE.CanvasTexture(cGo));
    texGo.wrapS = THREE.RepeatWrapping;
    texGo.wrapT = THREE.RepeatWrapping;

    this.vlGoTu = this._theo(new THREE.MeshStandardMaterial({
      map: texGo,
      roughness: 0.65,
      metalness: 0.02
    }));

    // Inox sáng chậu rửa và vòi nước
    this.vlInox = this._theo(new THREE.MeshStandardMaterial({
      color: 0xe2e8f0,
      roughness: 0.2,
      metalness: 0.9
    }));

    // Bóng tiếp xúc dưới chân bàn
    const cAO = document.createElement("canvas");
    cAO.width = 128; cAO.height = 128;
    const gAO = cAO.getContext("2d");
    const radG = gAO.createRadialGradient(64, 64, 10, 64, 64, 64);
    radG.addColorStop(0, "rgba(15, 23, 42, 0.45)");
    radG.addColorStop(0.6, "rgba(15, 23, 42, 0.18)");
    radG.addColorStop(1, "rgba(15, 23, 42, 0)");
    gAO.fillStyle = radG;
    gAO.fillRect(0, 0, 128, 128);
    const texAO = this._theo(new THREE.CanvasTexture(cAO));
    this.vlAO = this._theo(new THREE.MeshBasicMaterial({
      map: texAO,
      transparent: true,
      opacity: 0.65,
      depthWrite: false
    }));

    if (!Array.isArray(this.banDo.tram)) return;

    for (const t of this.banDo.tram) {
      const [bx, bz] = t.tam;
      const ngang = t.huong === 90 || t.huong === 270;
      const dai = ngang ? t.ban[1] : t.ban[0]; // 2.6 m
      const rong = ngang ? t.ban[0] : t.ban[1]; // 1.25 m

      const banGroup = new THREE.Group();
      banGroup.position.set(bx, 0, bz);

      // Bóng tiếp xúc dưới gầm bàn
      const matAO = new THREE.Mesh(this._theo(new THREE.PlaneGeometry(dai + 0.4, rong + 0.4)), this.vlAO);
      matAO.rotation.x = -Math.PI / 2;
      matAO.position.set(0, 0.003, 0);
      banGroup.add(matAO);

      // Mặt bàn phenolic dày 0.06m, mặt trên chuẩn y = 0.90m
      const matBanGeo = this._theo(new RoundedBoxGeometry(dai, 0.06, rong, 4, 0.02));
      const matBan = new THREE.Mesh(matBanGeo, this.vlMocBan);
      matBan.position.set(0, 0.87, 0);
      matBan.castShadow = true;
      matBan.receiveShadow = true;
      matBan.userData = { loai: "ban", tn: t.tn };
      banGroup.add(matBan);

      // Gờ chắn phía sau cao 8 cm ngăn hoá chất chảy tràn
      const goChan = new THREE.Mesh(this._theo(new THREE.BoxGeometry(dai, 0.08, 0.02)), this.vlMocBan);
      goChan.position.set(0, 0.94, -rong / 2 + 0.01);
      banGroup.add(goChan);

      // 4 chân thép hộp có núm tăng chỉnh ở đáy
      const geoChan = this._theo(new THREE.CylinderGeometry(0.025, 0.025, 0.84, 16));
      const nuaD = dai / 2 - 0.12;
      const nuaR = rong / 2 - 0.12;
      const cacChan = [
        [-nuaD, 0.42, -nuaR],
        [nuaD, 0.42, -nuaR],
        [-nuaD, 0.42, nuaR],
        [nuaD, 0.42, nuaR]
      ];
      for (const [cx, cy, cz] of cacChan) {
        const chanMesh = new THREE.Mesh(geoChan, this.vlChanThep);
        chanMesh.position.set(cx, cy, cz);
        chanMesh.castShadow = true;
        banGroup.add(chanMesh);
      }

      // Thân tủ vân gỗ sáng dưới gầm bàn (2 khoang tủ 2 cánh + ngăn kéo)
      const tuGeo = this._theo(new RoundedBoxGeometry(0.65, 0.65, rong - 0.2, 3, 0.02));
      const tuMeshTrai = new THREE.Mesh(tuGeo, this.vlGoTu);
      tuMeshTrai.position.set(-dai / 2 + 0.45, 0.45, 0);
      tuMeshTrai.castShadow = true;
      banGroup.add(tuMeshTrai);

      const tuMeshPhai = new THREE.Mesh(tuGeo, this.vlGoTu);
      tuMeshPhai.position.set(dai / 2 - 0.45, 0.45, 0);
      tuMeshPhai.castShadow = true;
      banGroup.add(tuMeshPhai);

      // Tay nắm kim loại trên cánh tủ
      const geoTayNam = this._theo(new THREE.CylinderGeometry(0.008, 0.008, 0.15, 8));
      const tay1 = new THREE.Mesh(geoTayNam, this.vlInox);
      tay1.position.set(-dai / 2 + 0.45, 0.55, rong / 2 - 0.08);
      banGroup.add(tay1);

      const tay2 = new THREE.Mesh(geoTayNam, this.vlInox);
      tay2.position.set(dai / 2 - 0.45, 0.55, rong / 2 - 0.08);
      banGroup.add(tay2);

      // Chậu rửa inox âm bàn ở một đầu
      const chauRua = new THREE.Mesh(this._theo(new THREE.BoxGeometry(0.38, 0.2, 0.38)), this.vlInox);
      chauRua.position.set(-dai / 2 + 0.35, 0.8, -rong / 4);
      banGroup.add(chauRua);

      // Vòi nước cổ ngỗng inox uốn cong
      const voiCong = new THREE.Mesh(this._theo(new THREE.TorusGeometry(0.08, 0.014, 12, 24, Math.PI)), this.vlInox);
      voiCong.rotation.z = -Math.PI / 2;
      voiCong.position.set(-dai / 2 + 0.35, 1.05, -rong / 4 - 0.12);
      banGroup.add(voiCong);

      // Giá thuốc thử 2 tầng ở mép sau bàn (không đè lên vùng dụng cụ ở giữa bàn)
      const keGeo = this._theo(new THREE.BoxGeometry(1.4, 0.02, 0.16));
      const ke1 = new THREE.Mesh(keGeo, this.vlGoTu);
      ke1.position.set(0, 1.18, -rong / 2 + 0.1);
      banGroup.add(ke1);

      const ke2 = new THREE.Mesh(keGeo, this.vlGoTu);
      ke2.position.set(0, 1.42, -rong / 2 + 0.1);
      banGroup.add(ke2);

      // Trụ đỡ giá thuốc thử
      const cotKeGeo = this._theo(new THREE.CylinderGeometry(0.012, 0.012, 0.6, 8));
      const cotKe1 = new THREE.Mesh(cotKeGeo, this.vlChanThep);
      cotKe1.position.set(-0.68, 1.2, -rong / 2 + 0.1);
      banGroup.add(cotKe1);

      const cotKe2 = new THREE.Mesh(cotKeGeo, this.vlChanThep);
      cotKe2.position.set(0.68, 1.2, -rong / 2 + 0.1);
      banGroup.add(cotKe2);

      // Ghế đẩu tròn chân thép
      this._taoGheDau(banGroup, -0.6, 0, rong / 2 + 0.35);
      this._taoGheDau(banGroup, 0.6, 0, rong / 2 + 0.35);

      this.goc.add(banGroup);
    }
  }

  _taoGheDau(parent, x, y, z) {
    const demGeo = this._theo(new THREE.CylinderGeometry(0.16, 0.16, 0.05, 16));
    const demMat = this._theo(new THREE.MeshStandardMaterial({ color: 0x334155, roughness: 0.5 }));
    const dem = new THREE.Mesh(demGeo, demMat);
    dem.position.set(x, y + 0.55, z);
    dem.castShadow = true;
    parent.add(dem);

    const chanGeo = this._theo(new THREE.CylinderGeometry(0.012, 0.012, 0.55, 8));
    for (let a = 0; a < 4; a++) {
      const ang = (a * Math.PI) / 2 + Math.PI / 4;
      const chan = new THREE.Mesh(chanGeo, this.vlChanThep);
      chan.position.set(x + Math.cos(ang) * 0.11, y + 0.27, z + Math.sin(ang) * 0.11);
      parent.add(chan);
    }
  }

  // ---------------------------------------------------------------- NỘI THẤT LAB
  _dungNoiThat() {
    if (!Array.isArray(this.banDo.noi_that)) return;

    for (const f of this.banDo.noi_that) {
      if (f.chi_khi_cao && this.chatLuong !== "cao") continue;
      const [fx, fz] = f.tam;
      const [sx, sy, sz] = f.kich_thuoc;
      const huongRad = THREE.MathUtils.degToRad(f.huong || 0);

      const nhomF = new THREE.Group();
      nhomF.position.set(fx, 0, fz);
      nhomF.rotation.y = huongRad;
      if (f.chi_khi_cao) this._doCao.push(nhomF);

      switch (f.loai) {
        case "tu_bao_ho":
          this._taoTuBaoHo(nhomF, sx, sy, sz);
          break;
        case "voi_khan_cap":
          this._taoVoiKhanCap(nhomF, sx, sy, sz);
          break;
        case "binh_chua_chay":
          this._taoBinhChuaChay(nhomF, sx, sy, sz);
          break;
        case "tu_so_cuu":
          this._taoTuSoCuu(nhomF, sx, sy, sz);
          break;
        case "tuong_ghs":
          this._taoTuongGHS(nhomF, sx, sy, sz);
          break;
        case "tu_hut":
          this._taoTuHut(nhomF, sx, sy, sz);
          break;
        case "gia_thuoc_thu":
          this._taoGiaThuocThu(nhomF, sx, sy, sz);
          break;
        case "bon_rua":
          this._taoBonRua(nhomF, sx, sy, sz);
          break;
        case "tu_hoa_chat":
          this._taoTuHoaChat(nhomF, sx, sy, sz);
          break;
        case "bang_tuong_tac":
          this._taoBangTuongTac(nhomF, sx, sy, sz);
          break;
        case "ban_giao_vien":
          this._taoBanGiaoVien(nhomF, sx, sy, sz);
          break;
        case "bang_tuan_hoan":
          this._taoBangTuanHoan(nhomF, sx, sy, sz);
          break;
        case "nguon_dc":
          this._taoNguonDC(nhomF, sx, sy, sz);
          break;
        case "cay_xanh":
          this._taoCayXanh(nhomF, sx, sy, sz);
          break;
        default:
          break;
      }

      this.goc.add(nhomF);
    }
  }

  _taoTuBaoHo(g, sx, sy, sz) {
    const vlTu = this._theo(new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.3 }));
    const than = new THREE.Mesh(this._theo(new RoundedBoxGeometry(sx, sy, sz, 3, 0.03)), vlTu);
    than.position.y = sy / 2;
    than.castShadow = true;
    g.add(than);

    const vlKinh = this._theo(new THREE.MeshStandardMaterial({ color: 0xe0f2fe, roughness: 0.1, transparent: true, opacity: 0.4 }));
    const cua = new THREE.Mesh(this._theo(new THREE.PlaneGeometry(sx - 0.1, sy - 0.2)), vlKinh);
    cua.position.set(0, sy / 2, sz / 2 + 0.01);
    g.add(cua);

    const c = document.createElement("canvas");
    c.width = 256; c.height = 64;
    const ctx = c.getContext("2d");
    ctx.fillStyle = "#0f2b48"; ctx.fillRect(0, 0, 256, 64);
    ctx.fillStyle = "#38bdf8"; ctx.font = '700 22px "Be Vietnam Pro", sans-serif';
    ctx.textAlign = "center"; ctx.textBaseline = "middle";
    ctx.fillText("🥼 ĐỒ BẢO HỘ (E)", 128, 32);
    const tex = this._theo(new THREE.CanvasTexture(c));
    const decal = new THREE.Mesh(this._theo(new THREE.PlaneGeometry(0.8, 0.2)), this._theo(new THREE.MeshBasicMaterial({ map: tex })));
    decal.position.set(0, sy - 0.25, sz / 2 + 0.02);
    g.add(decal);
  }

  _taoVoiKhanCap(g, sx, sy, sz) {
    const vlCot = this._theo(new THREE.MeshStandardMaterial({ color: 0x16a34a, roughness: 0.4 }));
    const cot = new THREE.Mesh(this._theo(new THREE.CylinderGeometry(0.04, 0.04, sy, 16)), vlCot);
    cot.position.y = sy / 2;
    g.add(cot);

    const sen = new THREE.Mesh(this._theo(new THREE.ConeGeometry(0.18, 0.12, 16)), vlCot);
    sen.rotation.x = Math.PI;
    sen.position.set(0, sy - 0.1, 0.25);
    g.add(sen);

    const chau = new THREE.Mesh(this._theo(new THREE.CylinderGeometry(0.16, 0.12, 0.08, 16)), this._theo(new THREE.MeshStandardMaterial({ color: 0xfacc15 })));
    chau.position.set(0, 1.0, 0.2);
    g.add(chau);
  }

  _taoBinhChuaChay(g, sx, sy, sz) {
    const vlBinh = this._theo(new THREE.MeshStandardMaterial({ color: 0xdc2626, roughness: 0.3 }));
    const than = new THREE.Mesh(this._theo(new THREE.CylinderGeometry(0.1, 0.1, 0.55, 16)), vlBinh);
    than.position.y = 0.5;
    than.castShadow = true;
    g.add(than);

    const co = new THREE.Mesh(this._theo(new THREE.CylinderGeometry(0.03, 0.03, 0.1, 8)), this.vlChanThep);
    co.position.y = 0.82;
    g.add(co);
  }

  _taoTuSoCuu(g, sx, sy, sz) {
    const than = new THREE.Mesh(this._theo(new THREE.BoxGeometry(sx, sy, sz)), this._theo(new THREE.MeshStandardMaterial({ color: 0xffffff })));
    than.position.y = 1.4;
    g.add(than);

    const c = document.createElement("canvas");
    c.width = 128; c.height = 128;
    const ctx = c.getContext("2d");
    ctx.fillStyle = "#ffffff"; ctx.fillRect(0, 0, 128, 128);
    ctx.fillStyle = "#16a34a";
    ctx.fillRect(48, 16, 32, 96);
    ctx.fillRect(16, 48, 96, 32);
    const tex = this._theo(new THREE.CanvasTexture(c));
    const decal = new THREE.Mesh(this._theo(new THREE.PlaneGeometry(sx * 0.7, sy * 0.7)), this._theo(new THREE.MeshBasicMaterial({ map: tex })));
    decal.position.set(0, 1.4, sz / 2 + 0.01);
    g.add(decal);
  }

  _taoTuongGHS(g, sx, sy, sz) {
    const c = document.createElement("canvas");
    c.width = 512; c.height = 256;
    const ctx = c.getContext("2d");
    ctx.fillStyle = "#0f172a"; ctx.fillRect(0, 0, 512, 256);
    ctx.strokeStyle = "#e2e8f0"; ctx.lineWidth = 4; ctx.strokeRect(4, 4, 504, 248);
    ctx.fillStyle = "#f8fafc"; ctx.font = '700 24px "Be Vietnam Pro", sans-serif';
    ctx.textAlign = "center";
    ctx.fillText("HỆ THỐNG BIỂU TƯỢNG NGUY HIỂM GHS", 256, 38);

    const startX = 64, gapX = 48, startY = 90, gapY = 65;
    for (let row = 0; row < 2; row++) {
      for (let col = 0; col < (row === 0 ? 5 : 4); col++) {
        const cx = startX + col * gapX * 1.8 + (row === 1 ? gapX * 0.9 : 0);
        const cy = startY + row * gapY * 1.5;
        ctx.save();
        ctx.translate(cx, cy);
        ctx.rotate(Math.PI / 4);
        ctx.fillStyle = "#ffffff";
        ctx.fillRect(-18, -18, 36, 36);
        ctx.strokeStyle = "#dc2626";
        ctx.lineWidth = 4;
        ctx.strokeRect(-18, -18, 36, 36);
        ctx.restore();
      }
    }
    const tex = this._theo(new THREE.CanvasTexture(c));
    const bang = new THREE.Mesh(this._theo(new THREE.PlaneGeometry(sx, sy)), this._theo(new THREE.MeshBasicMaterial({ map: tex })));
    bang.position.y = 2.0;
    g.add(bang);
  }

  // Tủ hút khí độc kiểu Lý Sơn Sa Kỳ
  _taoTuHut(g, sx, sy, sz) {
    const vlThanTuHut = this._theo(new THREE.MeshStandardMaterial({
      color: 0xe2e8f0, // Xám nhạt
      roughness: 0.35,
      metalness: 0.15
    }));

    // Thân tủ chính
    const than = new THREE.Mesh(this._theo(new RoundedBoxGeometry(sx, sy, sz, 3, 0.04)), vlThanTuHut);
    than.position.y = sy / 2;
    than.castShadow = true;
    g.add(than);

    // Chân tủ: khoang chứa hoá chất có 2 cánh tủ dưới
    const chanTu = new THREE.Mesh(this._theo(new THREE.BoxGeometry(sx + 0.02, 0.82, sz + 0.02)), this.vlChanThep);
    chanTu.position.y = 0.41;
    g.add(chanTu);

    // Mặt bàn thao tác phenolic màu xanh đậm
    const matThaoTac = new THREE.Mesh(this._theo(new THREE.BoxGeometry(sx - 0.05, 0.05, sz - 0.1)), this.vlMocBan);
    matThaoTac.position.set(0, 0.85, 0);
    g.add(matThaoTac);

    // Cửa kính kéo trong suốt (kéo lên 50%)
    const vlKinhHut = this._theo(new THREE.MeshStandardMaterial({
      color: 0x93c5fd,
      roughness: 0.1,
      transparent: true,
      opacity: 0.32
    }));
    const kinh = new THREE.Mesh(this._theo(new THREE.PlaneGeometry(sz * 0.85, sy * 0.45)), vlKinhHut);
    kinh.rotation.y = Math.PI / 2;
    kinh.position.set(sx / 2 + 0.01, sy * 0.65, 0);
    g.add(kinh);

    // Dải đèn báo trạng thái LED trên nóc tủ
    const denBao = new THREE.Mesh(this._theo(new THREE.BoxGeometry(0.04, 0.08, sz * 0.6)), this._theo(new THREE.MeshStandardMaterial({
      color: 0x22c55e,
      emissive: 0x22c55e,
      emissiveIntensity: 0.8
    })));
    denBao.position.set(sx / 2 + 0.01, sy - 0.15, 0);
    g.add(denBao);
  }

  _taoGiaThuocThu(g, sx, sy, sz) {
    const khung = new THREE.Mesh(this._theo(new THREE.BoxGeometry(sx, sy, sz)), this.vlGoTu);
    khung.position.y = sy / 2;
    khung.castShadow = true;
    g.add(khung);

    const soLo = 18;
    const loGeo = this._theo(new THREE.CylinderGeometry(0.035, 0.035, 0.12, 12));
    const loMat = this._theo(new THREE.MeshStandardMaterial({ color: 0x38bdf8, roughness: 0.2 }));
    const instLo = new THREE.InstancedMesh(loGeo, loMat, soLo);
    const dummy = new THREE.Object3D();
    let idx = 0;
    for (let tang = 0; tang < 3; tang++) {
      const yLo = 0.35 + tang * 0.45;
      for (let c = 0; c < 6; c++) {
        const zLo = -sz / 2 + 0.25 + c * (sz - 0.5) / 5;
        dummy.position.set(0.05, yLo, zLo);
        dummy.updateMatrix();
        instLo.setMatrixAt(idx++, dummy.matrix);
      }
    }
    instLo.instanceMatrix.needsUpdate = true;
    g.add(instLo);
  }

  _taoBonRua(g, sx, sy, sz) {
    const be = new THREE.Mesh(this._theo(new RoundedBoxGeometry(sx, sy, sz, 3, 0.03)), this.vlGoTu);
    be.position.y = sy / 2;
    be.castShadow = true;
    g.add(be);

    const long = new THREE.Mesh(this._theo(new THREE.BoxGeometry(sx * 0.7, 0.25, sz * 0.6)), this.vlInox);
    long.position.set(0, sy - 0.12, 0);
    g.add(long);

    const voi = new THREE.Mesh(this._theo(new THREE.TorusGeometry(0.1, 0.015, 12, 24, Math.PI)), this.vlInox);
    voi.rotation.z = -Math.PI / 2;
    voi.position.set(0, sy + 0.15, -0.15);
    g.add(voi);
  }

  _taoTuHoaChat(g, sx, sy, sz) {
    const than = new THREE.Mesh(this._theo(new RoundedBoxGeometry(sx, sy, sz, 3, 0.04)), this.vlGoTu);
    than.position.y = sy / 2;
    than.castShadow = true;
    g.add(than);

    const vlKinh = this._theo(new THREE.MeshStandardMaterial({ color: 0xdbeafe, roughness: 0.1, transparent: true, opacity: 0.35 }));
    const cua = new THREE.Mesh(this._theo(new THREE.PlaneGeometry(sx - 0.1, sy - 0.2)), vlKinh);
    cua.position.set(0, sy / 2, sz / 2 + 0.01);
    g.add(cua);

    const chaiGeo = this._theo(new THREE.CylinderGeometry(0.045, 0.045, 0.18, 12));
    const chaiMat = this._theo(new THREE.MeshStandardMaterial({ color: 0xd97706, roughness: 0.2 }));
    const instChai = new THREE.InstancedMesh(chaiGeo, chaiMat, 24);
    const dummy = new THREE.Object3D();
    let idx = 0;
    for (let tang = 0; tang < 3; tang++) {
      const yC = 0.35 + tang * 0.55;
      for (let col = 0; col < 8; col++) {
        const xC = -sx / 2 + 0.25 + col * (sx - 0.5) / 7;
        dummy.position.set(xC, yC, 0);
        dummy.updateMatrix();
        instChai.setMatrixAt(idx++, dummy.matrix);
      }
    }
    instChai.instanceMatrix.needsUpdate = true;
    g.add(instChai);
  }

  _taoBangTuongTac(g, sx, sy, sz) {
    const c = document.createElement("canvas");
    c.width = 1024; c.height = 512;
    this.ctxBang = c.getContext("2d");
    this._veBangTuongTac();
    this.texBang = this._theo(new THREE.CanvasTexture(c));
    this.texBang.colorSpace = THREE.SRGBColorSpace;

    const vlBang = this._theo(new THREE.MeshBasicMaterial({ map: this.texBang }));
    const bang = new THREE.Mesh(this._theo(new THREE.PlaneGeometry(sx, sy)), vlBang);
    bang.position.y = 2.1;
    g.add(bang);

    const khung = new THREE.Mesh(this._theo(new THREE.BoxGeometry(sx + 0.08, sy + 0.08, 0.04)), this.vlChanThep);
    khung.position.set(0, 2.1, -0.02);
    g.add(khung);
  }

  _veBangTuongTac() {
    if (!this.ctxBang) return;
    const g = this.ctxBang;
    g.fillStyle = "#0b1622";
    g.fillRect(0, 0, 1024, 512);

    g.fillStyle = "#0f2438";
    g.fillRect(20, 20, 984, 80);
    g.strokeStyle = "#38bdf8";
    g.lineWidth = 2;
    g.strokeRect(20, 20, 984, 80);

    g.fillStyle = "#38bdf8";
    g.font = '700 32px "Be Vietnam Pro", sans-serif';
    g.fillText("PHÒNG THÍ NGHIỆM HOÁ HỌC ẢO 3D", 50, 70);

    g.fillStyle = "#94a3b8";
    g.font = '500 18px "JetBrains Mono", monospace';
    g.fillText("GDPT 2018 · THPT & HSG QUỐC GIA", 660, 68);

    const ds = [
      { ten: "An toàn phòng thí nghiệm", khu: "Khu An toàn", mau: "#d97706" },
      { ten: "Chuẩn độ Acid – Base", khu: "Khu Định lượng", mau: "#0d9488" },
      { ten: "Nhận biết Cation & Anion", khu: "Khu Định tính", mau: "#16a34a" },
      { ten: "Pin Galvani Zn – Cu", khu: "Khu Điện hoá", mau: "#7c3aed" },
      { ten: "Điện phân dung dịch CuSO₄", khu: "Khu Điện hoá", mau: "#7c3aed" }
    ];

    for (let i = 0; i < ds.length; i++) {
      const item = ds[i];
      const y = 130 + i * 72;
      g.fillStyle = "#132337";
      g.fillRect(30, y, 964, 60);
      g.strokeStyle = "rgba(56, 189, 248, 0.2)";
      g.lineWidth = 1;
      g.strokeRect(30, y, 964, 60);

      g.fillStyle = item.mau;
      g.beginPath();
      g.arc(65, y + 30, 16, 0, Math.PI * 2);
      g.fill();
      g.fillStyle = "#ffffff";
      g.font = '700 16px "JetBrains Mono", monospace';
      g.textAlign = "center";
      g.fillText(String(i + 1), 65, y + 36);

      g.textAlign = "left";
      g.fillStyle = "#f8fafc";
      g.font = '600 22px "Be Vietnam Pro", sans-serif';
      g.fillText(item.ten, 105, y + 38);

      g.fillStyle = item.mau;
      g.font = '500 16px "Be Vietnam Pro", sans-serif';
      g.fillText(item.khu, 560, y + 37);

      g.fillStyle = "rgba(56, 189, 248, 0.15)";
      g.beginPath();
      g.roundRect(830, y + 14, 140, 32, 16);
      g.fill();
      g.fillStyle = "#38bdf8";
      g.font = '600 15px "Be Vietnam Pro", sans-serif';
      g.textAlign = "center";
      g.fillText("Sẵn sàng ⚗️", 900, y + 36);
    }
  }

  _taoBanGiaoVien(g, sx, sy, sz) {
    const ban = new THREE.Mesh(this._theo(new RoundedBoxGeometry(sx, sy, sz, 3, 0.03)), this.vlMocBan);
    ban.position.y = sy / 2;
    ban.castShadow = true;
    g.add(ban);

    const manHinh = new THREE.Mesh(this._theo(new THREE.BoxGeometry(0.5, 0.32, 0.04)), this.vlChanThep);
    manHinh.position.set(0, sy + 0.22, 0);
    g.add(manHinh);

    const de = new THREE.Mesh(this._theo(new THREE.CylinderGeometry(0.08, 0.08, 0.08, 12)), this.vlChanThep);
    de.position.set(0, sy + 0.04, 0);
    g.add(de);
  }

  _taoBangTuanHoan(g, sx, sy, sz) {
    const c = document.createElement("canvas");
    c.width = 2048; c.height = 1152;
    const ctx = c.getContext("2d");
    const tex = this._theo(new THREE.CanvasTexture(c));
    const poster = new THREE.Mesh(this._theo(new THREE.PlaneGeometry(sz, sy)), this._theo(new THREE.MeshBasicMaterial({ map: tex })));
    poster.rotation.y = -Math.PI / 2;
    poster.position.y = 2.0;
    g.add(poster);

    const MAU_LOAI = {
      phi_kim: { bg: "#dcfce7", border: "#22c55e", ten: "Phi kim" },
      khi_hiem: { bg: "#fce7f3", border: "#ec4899", ten: "Khí hiếm" },
      kim_loai_kiem: { bg: "#ffedd5", border: "#f97316", ten: "Kim loại kiềm" },
      kiem_tho: { bg: "#fef3c7", border: "#f59e0b", ten: "Kiềm thổ" },
      chuyen_tiep: { bg: "#e0e7ff", border: "#6366f1", ten: "Chuyển tiếp" },
      kim_loai_nhom_p: { bg: "#e0f2fe", border: "#0284c7", ten: "Kim loại nhóm p" },
      a_kim: { bg: "#f3e8ff", border: "#a855f7", ten: "Á kim" },
      halogen: { bg: "#ccfbf1", border: "#14b8a6", ten: "Halogen" },
      lanthanide: { bg: "#fee2e2", border: "#ef4444", ten: "Lanthanide" },
      actinide: { bg: "#f5d0fe", border: "#d946ef", ten: "Actinide" }
    };

    const NHOM_LABEL = [
      "1\nIA", "2\nIIA", "3\nIIIB", "4\nIVB", "5\nVB", "6\nVIB",
      "7\nVIIB", "8\nVIIIB", "9\nVIIIB", "10\nVIIIB", "11\nIB", "12\nIIB",
      "13\nIIIA", "14\nIVA", "15\nVA", "16\nVIA", "17\nVIIA", "18\nVIIIA"
    ];

    const veBang = (danhSach) => {
      // 1. Nền canvas
      ctx.fillStyle = "#ffffff";
      ctx.fillRect(0, 0, 2048, 1152);
      ctx.strokeStyle = "#0f2b48";
      ctx.lineWidth = 6;
      ctx.strokeRect(10, 10, 2028, 1132);

      // 2. Tiêu đề chính
      ctx.fillStyle = "#0f2b48";
      ctx.font = '700 32px "Be Vietnam Pro", sans-serif';
      ctx.textAlign = "center";
      ctx.fillText("BẢNG TUẦN HOÀN CÁC NGUYÊN TỐ HOÁ HỌC (IUPAC - CT GDPT 2018)", 1024, 52);

      ctx.fillStyle = "#475569";
      ctx.font = '500 16px "Be Vietnam Pro", sans-serif';
      ctx.fillText("Số thứ tự (Z) · Kí hiệu · Tên nguyên tố · Nguyên tử khối chuẩn IUPAC", 1024, 80);

      const ow = 96, oh = 76, stepX = 103, stepY = 82;
      const ox0 = 98, oy0 = 135;

      // 3. Nhãn nhóm 1-18
      ctx.font = '600 13px "Be Vietnam Pro", sans-serif';
      ctx.textAlign = "center";
      ctx.fillStyle = "#1e293b";
      for (let col = 0; col < 18; col++) {
        const x = ox0 + col * stepX + ow / 2;
        const lines = NHOM_LABEL[col].split("\n");
        ctx.fillText(lines[0], x, oy0 - 22);
        ctx.fillStyle = "#0284c7";
        ctx.fillText(lines[1], x, oy0 - 8);
        ctx.fillStyle = "#1e293b";
      }

      // 4. Nhãn chu kỳ 1-7
      ctx.font = '700 15px "Be Vietnam Pro", sans-serif';
      ctx.textAlign = "right";
      ctx.fillStyle = "#0f2b48";
      for (let r = 0; r < 7; r++) {
        const y = oy0 + r * stepY + oh / 2 + 5;
        ctx.fillText(`${r + 1}`, ox0 - 15, y);
      }

      // Tạo map tra cứu nguyên tố
      const mapZ = {};
      if (Array.isArray(danhSach)) {
        danhSach.forEach(nt => { mapZ[nt.z] = nt; });
      }

      const veO = (x, y, z) => {
        const nt = mapZ[z];
        const loai = nt ? nt.loai : "phi_kim";
        const info = MAU_LOAI[loai] || MAU_LOAI.phi_kim;

        // Nền ô
        ctx.fillStyle = info.bg;
        ctx.fillRect(x, y, ow, oh);
        ctx.strokeStyle = info.border;
        ctx.lineWidth = 1.5;
        ctx.strokeRect(x, y, ow, oh);

        if (!nt) return;

        // Số Z
        ctx.fillStyle = "#475569";
        ctx.font = '600 12px "JetBrains Mono", monospace';
        ctx.textAlign = "left";
        ctx.fillText(`${nt.z}`, x + 5, y + 15);

        // Kí hiệu
        ctx.fillStyle = "#0f2b48";
        ctx.font = '700 24px "Be Vietnam Pro", sans-serif';
        ctx.textAlign = "center";
        ctx.fillText(nt.ki_hieu, x + ow / 2, y + 38);

        // Tên tiếng Việt
        ctx.fillStyle = "#1e293b";
        ctx.font = '500 10.5px "Be Vietnam Pro", sans-serif';
        ctx.fillText(nt.ten_viet || nt.ten, x + ow / 2, y + 54);

        // Nguyên tử khối
        ctx.fillStyle = "#475569";
        ctx.font = '500 10px "JetBrains Mono", monospace';
        ctx.fillText(nt.nguyen_tu_khoi || `${nt.z}`, x + ow / 2, y + 68);
      };

      // 5. Vẽ 118 nguyên tố trên lưới chính 7 hàng
      for (let z = 1; z <= 118; z++) {
        const nt = mapZ[z];
        if (!nt) continue;
        const ck = nt.chu_ky - 1;
        let col = nt.nhom - 1;

        if (z >= 58 && z <= 71) {
          // Lanthanide ở hàng 8
          const lx = ox0 + (2 + (z - 58)) * stepX;
          const ly = oy0 + 7 * stepY + 16;
          veO(lx, ly, z);
          continue;
        }
        if (z >= 90 && z <= 103) {
          // Actinide ở hàng 9
          const ax = ox0 + (2 + (z - 90)) * stepX;
          const ay = oy0 + 8 * stepY + 16;
          veO(ax, ay, z);
          continue;
        }

        const x = ox0 + col * stepX;
        const y = oy0 + ck * stepY;
        veO(x, y, z);
      }

      // Ô giữ chỗ Lanthanide (*) và Actinide (**) tại nhóm 3
      const lxStar = ox0 + 2 * stepX;
      const lyStar = oy0 + 5 * stepY;
      ctx.fillStyle = "#fee2e2";
      ctx.fillRect(lxStar, lyStar, ow, oh);
      ctx.strokeStyle = "#ef4444";
      ctx.strokeRect(lxStar, lyStar, ow, oh);
      ctx.fillStyle = "#b91c1c";
      ctx.font = '700 15px "Be Vietnam Pro", sans-serif';
      ctx.textAlign = "center";
      ctx.fillText("* 57-71", lxStar + ow / 2, lyStar + 34);
      ctx.font = '500 10px "Be Vietnam Pro", sans-serif';
      ctx.fillText("Lanthanide", lxStar + ow / 2, lyStar + 52);

      const axStar = ox0 + 2 * stepX;
      const ayStar = oy0 + 6 * stepY;
      ctx.fillStyle = "#f5d0fe";
      ctx.fillRect(axStar, ayStar, ow, oh);
      ctx.strokeStyle = "#d946ef";
      ctx.strokeRect(axStar, ayStar, ow, oh);
      ctx.fillStyle = "#a21caf";
      ctx.font = '700 15px "Be Vietnam Pro", sans-serif';
      ctx.fillText("** 89-103", axStar + ow / 2, ayStar + 34);
      ctx.font = '500 10px "Be Vietnam Pro", sans-serif';
      ctx.fillText("Actinide", axStar + ow / 2, ayStar + 52);

      // Nhãn hàng Lanthanide và Actinide
      ctx.textAlign = "right";
      ctx.font = '600 12px "Be Vietnam Pro", sans-serif';
      ctx.fillStyle = "#b91c1c";
      ctx.fillText("* Họ Lanthanide", ox0 + 2 * stepX - 14, oy0 + 7 * stepY + oh / 2 + 20);
      ctx.fillStyle = "#a21caf";
      ctx.fillText("** Họ Actinide", ox0 + 2 * stepX - 14, oy0 + 8 * stepY + oh / 2 + 20);

      // 6. Chú giải phân loại màu sắc (Legend) ở góc dưới
      const legX = ox0 + 10;
      const legY = 1000;
      ctx.textAlign = "left";
      ctx.font = '700 14px "Be Vietnam Pro", sans-serif';
      ctx.fillStyle = "#0f2b48";
      ctx.fillText("CHÚ GIẢI PHÂN LOẠI NGUYÊN TỐ:", legX, legY);

      const loaiKeys = Object.keys(MAU_LOAI);
      for (let i = 0; i < loaiKeys.length; i++) {
        const k = loaiKeys[i];
        const row = Math.floor(i / 5);
        const col = i % 5;
        const bx = legX + col * 360;
        const by = legY + 16 + row * 34;

        ctx.fillStyle = MAU_LOAI[k].bg;
        ctx.fillRect(bx, by, 28, 20);
        ctx.strokeStyle = MAU_LOAI[k].border;
        ctx.lineWidth = 1.5;
        ctx.strokeRect(bx, by, 28, 20);

        ctx.fillStyle = "#1e293b";
        ctx.font = '500 13px "Be Vietnam Pro", sans-serif';
        ctx.fillText(MAU_LOAI[k].ten, bx + 36, by + 15);
      }

      tex.needsUpdate = true;
    };

    if (window.BANG_TUAN_HOAN && Array.isArray(window.BANG_TUAN_HOAN)) {
      if (document.fonts && document.fonts.ready) {
        document.fonts.ready.then(() => veBang(window.BANG_TUAN_HOAN));
      } else {
        veBang(window.BANG_TUAN_HOAN);
      }
    } else {
      fetch("/static/hoa-hoc/bang-tuan-hoan/nguyen-to.json")
        .then(r => r.json())
        .then(data => {
          window.BANG_TUAN_HOAN = data;
          if (document.fonts && document.fonts.ready) {
            document.fonts.ready.then(() => veBang(data));
          } else {
            veBang(data);
          }
        })
        .catch(() => {
          veBang([]);
        });
    }
  }

  _taoNguonDC(g, sx, sy, sz) {
    const hop = new THREE.Mesh(this._theo(new RoundedBoxGeometry(sx, sy, sz, 3, 0.03)), this.vlChanThep);
    hop.position.y = sy / 2;
    hop.castShadow = true;
    g.add(hop);

    const mh = new THREE.Mesh(this._theo(new THREE.PlaneGeometry(sx * 0.7, sy * 0.35)), this._theo(new THREE.MeshBasicMaterial({ color: 0x0b1622 })));
    mh.position.set(0, sy * 0.65, sz / 2 + 0.01);
    g.add(mh);

    const cocAm = new THREE.Mesh(this._theo(new THREE.CylinderGeometry(0.03, 0.03, 0.04, 12)), this._theo(new THREE.MeshStandardMaterial({ color: 0x09090b })));
    cocAm.rotation.x = Math.PI / 2;
    cocAm.position.set(-0.15, sy * 0.25, sz / 2 + 0.02);
    g.add(cocAm);

    const cocDuong = new THREE.Mesh(this._theo(new THREE.CylinderGeometry(0.03, 0.03, 0.04, 12)), this._theo(new THREE.MeshStandardMaterial({ color: 0xdc2626 })));
    cocDuong.rotation.x = Math.PI / 2;
    cocDuong.position.set(0.15, sy * 0.25, sz / 2 + 0.02);
    g.add(cocDuong);
  }

  _taoCayXanh(g, sx, sy, sz) {
    const chau = new THREE.Mesh(this._theo(new THREE.CylinderGeometry(0.24, 0.18, 0.45, 16)), this.vlGoTu);
    chau.position.y = 0.225;
    chau.castShadow = true;
    g.add(chau);

    const vlLa = this._theo(new THREE.MeshStandardMaterial({ color: 0x15803d, roughness: 0.7 }));
    const cay = new THREE.Mesh(this._theo(new THREE.DodecahedronGeometry(0.38, 1)), vlLa);
    cay.position.y = 0.65;
    cay.castShadow = true;
    g.add(cay);
  }

  // ---------------------------------------------------------------- GÓC TOÀN CẢNH NHÀ BÚP BÊ & LÀM MỜ
  lamNhaBupBe(bat = true) {
    if (this.tran) this.tran.visible = !bat;
    if (this.tuongNam) this.tuongNam.visible = !bat;
  }

  lamMo(bat = true) {
    // màu nền gốc (do doiGiaoDien đặt) được nhớ lại để vào / ra bàn nhiều lần không làm sàn, tường tối dần
    for (const vl of [this.vlSan, this.vlTuong]) {
      if (!vl) continue;
      vl.userData.mauNen ??= vl.color.clone();
      vl.color.copy(vl.userData.mauNen).multiplyScalar(bat ? 0.65 : 1.0);
    }
    if (this.vlDenTran) this.vlDenTran.emissiveIntensity = bat ? 0.4 : 0.95;
  }

  doiGiaoDien() {
    const toi = document.documentElement.getAttribute("data-theme") === "toi";
    if (this.vlSan) {
      this.vlSan.color.set(toi ? 0x0f172a : 0xf1f5f9);
      this.vlSan.userData.mauNen = null;
    }
    if (this.vlTuong) {
      this.vlTuong.color.set(toi ? 0x1e293b : 0xf8fafc);
      this.vlTuong.userData.mauNen = null;
    }
    if (this.vlTran) {
      this.vlTran.color.set(toi ? 0x090d16 : 0xf8fafc);
    }
    if (this.vlDenTran) {
      this.vlDenTran.emissiveIntensity = toi ? 1.2 : 0.95;
    }
    this._veBauTroi(toi);
    if (this.texBtroi) this.texBtroi.needsUpdate = true;

    for (const [id, item] of Object.entries(this._vatLieuKhu)) {
      const mauMoi = docMauCSS(item.bien, "#0f2b48");
      item.vl.color.set(mauMoi);
    }

    for (const b of this._bienCuaTextures) {
      b.ve();
      b.tex.needsUpdate = true;
    }

    if (this.ctxBang) {
      this._veBangTuongTac();
      if (this.texBang) this.texBang.needsUpdate = true;
    }
  }

  datChatLuong(q) {
    this.chatLuong = q;
    const hienDoCao = q === "cao";
    for (const obj of this._doCao) {
      obj.visible = hienDoCao;
    }
  }

  capNhat(dt, t) {}

  huy() {
    this.scene.remove(this.goc);
    for (const x of this._taiNguyen) {
      x.dispose?.();
    }
    this._taiNguyen.clear();
  }
}
