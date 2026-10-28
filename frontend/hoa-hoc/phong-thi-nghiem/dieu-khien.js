/* Phòng thí nghiệm Hoá học ảo – ĐIỀU KHIỂN & CAMERA KIỂU GAME (hàm thuần, không DOM / THREE).
   UMD: trình duyệt → window.PTNDieuKhien · Node → require("./dieu-khien.js"). */
(function (goc, nhaMay) {
  if (typeof module === "object" && module.exports) module.exports = nhaMay();
  else {
    const inst = nhaMay();
    goc.PTNDieuKhien = inst;
    goc.DieuKhienPhongLab = inst;
  }
})(typeof self !== "undefined" ? self : this, function () {
  "use strict";

  /**
   * Tính vị trí camera từ yaw, pitch, khoảng cách và điểm nhìn.
   * Hệ toạ độ: +X = Đông (phải), +Y = Trên, +Z = Nam (sau), -Z = Bắc (trước).
   * yaw = 0 -> camera ở phía Nam nhìn về phía Bắc (-Z).
   * pitch > 0 -> camera nhìn từ trên xuống (camY cao hơn diemNhin).
   * @param {number} yaw - Góc xoay quanh trục Y (radian)
   * @param {number} pitch - Góc ngẩng/cúi (radian), từ -35° đến +60°
   * @param {number} khoangCach - Khoảng cách tới điểm nhìn (m)
   * @param {number[]|{x:number, y:number, z:number}} diemNhin - [x, y, z] hoặc {x, y, z}
   * @returns {[number, number, number]} [camX, camY, camZ]
   */
  function viTriCamera(yaw, pitch, khoangCach, diemNhin) {
    const dx = Array.isArray(diemNhin) ? diemNhin[0] : diemNhin.x;
    const dy = Array.isArray(diemNhin) ? diemNhin[1] : diemNhin.y;
    const dz = Array.isArray(diemNhin) ? diemNhin[2] : diemNhin.z;

    const dNgang = khoangCach * Math.cos(pitch);
    const dDoc = khoangCach * Math.sin(pitch);

    // Camera đặt sau lưng điểm nhìn: ngược hướng nhìn
    // Hướng nhìn ngang của camera = [-sin(yaw), -cos(yaw)]
    // Vị trí camera = diemNhin - khoangCach * huongNhin
    const camX = dx + dNgang * Math.sin(yaw);
    const camY = dy + dDoc;
    const camZ = dz + dNgang * Math.cos(yaw);

    return [camX, camY, camZ];
  }

  /**
   * Hướng nhìn chiếu ngang của camera (vector đơn vị [vx, vz]).
   * @param {number} yaw - Góc yaw của camera
   * @returns {[number, number]} [vx, vz]
   */
  function huongNhinNgang(yaw) {
    return [-Math.sin(yaw), -Math.cos(yaw)];
  }

  /**
   * Tính vector hướng di chuyển 2D trên mặt phẳng XZ dựa trên phím và góc yaw của camera.
   * W luôn đi theo hướng camera chiếu ngang ("vào trong màn hình").
   * S đi lùi ("ra ngoài màn hình").
   * A đi sang trái.
   * D đi sang phải.
   * @param {Object} phim - { W?: boolean, S?: boolean, A?: boolean, D?: boolean, ... }
   * @param {number} yaw - Góc yaw của camera (radian)
   * @returns {[number, number]} Vector đơn vị di chuyển [vx, vz], hoặc [0, 0] nếu không bấm phím
   */
  function huongDiChuyen(phim, yaw) {
    if (!phim) return [0, 0];

    const w = !!(phim.W || phim.w || phim.ArrowUp);
    const s = !!(phim.S || phim.s || phim.ArrowDown);
    const a = !!(phim.A || phim.a || phim.ArrowLeft);
    const d = !!(phim.D || phim.d || phim.ArrowRight);

    let tien = 0;
    if (w && !s) tien = 1;
    else if (s && !w) tien = -1;

    let ngang = 0;
    if (d && !a) ngang = 1;
    else if (a && !d) ngang = -1;

    if (tien === 0 && ngang === 0) return [0, 0];

    // Vector hướng nhìn ngang: W = [-sin(yaw), -cos(yaw)]
    const fx = -Math.sin(yaw);
    const fz = -Math.cos(yaw);

    // Vector sang phải: D = [cos(yaw), -sin(yaw)]
    const rx = Math.cos(yaw);
    const rz = -Math.sin(yaw);

    const vx = tien * fx + ngang * rx;
    const vz = tien * fz + ngang * rz;

    const len = Math.hypot(vx, vz);
    if (len < 1e-6) return [0, 0];

    return [vx / len, vz / len];
  }

  /**
   * Giao điểm tia với một hộp AABB 3D.
   * @param {number[]} O - Gốc tia [ox, oy, oz]
   * @param {number[]} D - Vector hướng tia [dx, dy, dz] (đã chuẩn hoá)
   * @param {Object} b - Hộp {minX, maxX, minY, maxY, minZ, maxZ}
   * @returns {number|null} Khoảng cách t tới điểm cắt gần nhất, hoặc null
   */
  function catTiaAABB(O, D, b) {
    let tmin = -Infinity;
    let tmax = Infinity;

    const minX = b.minX !== undefined ? b.minX : b[0];
    const maxX = b.maxX !== undefined ? b.maxX : b[3];
    const minY = b.minY !== undefined ? b.minY : (b[1] !== undefined ? b[1] : -Infinity);
    const maxY = b.maxY !== undefined ? b.maxY : (b[4] !== undefined ? b[4] : Infinity);
    const minZ = b.minZ !== undefined ? b.minZ : (b[2] !== undefined ? b[2] : (b[5] !== undefined ? b[2] : b[1]));
    const maxZ = b.maxZ !== undefined ? b.maxZ : (b[5] !== undefined ? b[5] : (b[3] !== undefined ? b[3] : b[3]));

    // Trục X
    if (Math.abs(D[0]) < 1e-8) {
      if (O[0] < minX || O[0] > maxX) return null;
    } else {
      let t1 = (minX - O[0]) / D[0];
      let t2 = (maxX - O[0]) / D[0];
      if (t1 > t2) { const tmp = t1; t1 = t2; t2 = tmp; }
      tmin = Math.max(tmin, t1);
      tmax = Math.min(tmax, t2);
      if (tmin > tmax) return null;
    }

    // Trục Y
    if (Math.abs(D[1]) < 1e-8) {
      if (O[1] < minY || O[1] > maxY) return null;
    } else {
      let t1 = (minY - O[1]) / D[1];
      let t2 = (maxY - O[1]) / D[1];
      if (t1 > t2) { const tmp = t1; t1 = t2; t2 = tmp; }
      tmin = Math.max(tmin, t1);
      tmax = Math.min(tmax, t2);
      if (tmin > tmax) return null;
    }

    // Trục Z
    if (Math.abs(D[2]) < 1e-8) {
      if (O[2] < minZ || O[2] > maxZ) return null;
    } else {
      let t1 = (minZ - O[2]) / D[2];
      let t2 = (maxZ - O[2]) / D[2];
      if (t1 > t2) { const tmp = t1; t1 = t2; t2 = tmp; }
      tmin = Math.max(tmin, t1);
      tmax = Math.min(tmax, t2);
      if (tmin > tmax) return null;
    }

    return tmin >= 0 ? tmin : (tmax >= 0 ? 0 : null);
  }

  /**
   * Tính tay đòn camera chống xuyên tường: bắn tia từ điểm nhìn tới vị trí mong muốn của camera,
   * cắt các hộp chắn trong hopChan (tường, vách kính, tủ cao).
   * @param {number[]} diemNhin - [x, y, z] điểm nhìn (trên người nhân vật)
   * @param {number[]} viTriMuon - [x, y, z] vị trí camera mong muốn
   * @param {Array<Object>} hopChan - Danh sách hộp chắn AABB {minX, maxX, minY, maxY, minZ, maxZ}
   * @param {number} [dem=0.2] - Khoảng đệm an toàn lùi lại trước vật chắn (m)
   * @param {number} [khoangCachToiThieu=0.4] - Khoảng cách co lại tối thiểu (m)
   * @returns {{ khoangCach: number, viTri: [number, number, number], biChan: boolean }}
   */
  function tayDon(diemNhin, viTriMuon, hopChan, dem = 0.2, khoangCachToiThieu = 0.4) {
    const ox = diemNhin[0], oy = diemNhin[1], oz = diemNhin[2];
    const tx = viTriMuon[0], ty = viTriMuon[1], tz = viTriMuon[2];

    const dx = tx - ox;
    const dy = ty - oy;
    const dz = tz - oz;
    const tongKhoangCach = Math.hypot(dx, dy, dz);

    if (tongKhoangCach < 1e-6 || !Array.isArray(hopChan) || hopChan.length === 0) {
      return { khoangCach: tongKhoangCach, viTri: [tx, ty, tz], biChan: false };
    }

    const dir = [dx / tongKhoangCach, dy / tongKhoangCach, dz / tongKhoangCach];
    let tGanNhat = tongKhoangCach;
    let biChan = false;

    for (let i = 0; i < hopChan.length; i++) {
      const h = hopChan[i];
      if (!h) continue;
      const t = catTiaAABB(diemNhin, dir, h);
      if (t !== null && t > 0.05 && t < tGanNhat) {
        tGanNhat = t;
        biChan = true;
      }
    }

    if (!biChan) {
      return { khoangCach: tongKhoangCach, viTri: [tx, ty, tz], biChan: false };
    }

    // Co khoảng cách lại: (khoảng trúng - dem)
    const kcMoi = Math.max(khoangCachToiThieu, Math.min(tongKhoangCach, tGanNhat - dem));
    const viTriMoi = [
      ox + dir[0] * kcMoi,
      oy + dir[1] * kcMoi,
      oz + dir[2] * kcMoi
    ];

    return { khoangCach: kcMoi, viTri: viTriMoi, biChan: true };
  }

  /**
   * Quay mượt một góc tới góc đích qua đường ngắn nhất (quanh vòng tròn ±π).
   * Không bao giờ vượt quá góc đích.
   * @param {number} gocHienTai - Góc hiện tại (radian)
   * @param {number} gocDich - Góc đích (radian)
   * @param {number} tocDo - Tốc độ góc tối đa (rad/s)
   * @param {number} dt - Thời gian bước trôi qua (s)
   * @returns {number} Góc mới (radian) trong khoảng [-π, π]
   */
  function quayMuot(gocHienTai, gocDich, tocDo, dt) {
    const HAI_PI = Math.PI * 2;
    let diff = (gocDich - gocHienTai) % HAI_PI;
    if (diff > Math.PI) diff -= HAI_PI;
    if (diff < -Math.PI) diff += HAI_PI;

    const buocMax = Math.abs(tocDo * dt);
    if (Math.abs(diff) <= buocMax) {
      let g = gocDich % HAI_PI;
      if (g > Math.PI) g -= HAI_PI;
      if (g < -Math.PI) g += HAI_PI;
      return g;
    }

    let gMoi = gocHienTai + Math.sign(diff) * buocMax;
    gMoi = gMoi % HAI_PI;
    if (gMoi > Math.PI) gMoi -= HAI_PI;
    if (gMoi < -Math.PI) gMoi += HAI_PI;
    return gMoi;
  }

  return {
    viTriCamera,
    huongNhinNgang,
    huongDiChuyen,
    catTiaAABB,
    tayDon,
    quayMuot
  };
});
