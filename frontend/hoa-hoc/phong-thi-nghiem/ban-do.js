/* Phòng thí nghiệm Hoá học ảo – BẢN ĐỒ & TÌM ĐƯỜNG (hàm thuần, không DOM / THREE).
   UMD: trình duyệt → window.PTNBanDo · Node → require("./ban-do.js"). */
(function (goc, nhaMay) {
  if (typeof module === "object" && module.exports) module.exports = nhaMay();
  else {
    const inst = nhaMay();
    goc.PTNBanDo = inst;
    goc.BanDoPhongLab = inst;
  }
})(typeof self !== "undefined" ? self : this, function () {
  "use strict";

  /** Trích danh sách hộp vật cản AABB [minX, minZ, maxX, maxZ] từ sơ đồ phòng */
  function layVatCan(banDo) {
    const vc = [];
    if (!banDo) return vc;

    // Các bàn thí nghiệm
    if (Array.isArray(banDo.tram)) {
      for (let i = 0; i < banDo.tram.length; i++) {
        const t = banDo.tram[i];
        const ngang = t.huong === 90 || t.huong === 270;
        const sx = ngang ? t.ban[1] : t.ban[0];
        const sz = ngang ? t.ban[0] : t.ban[1];
        vc.push({
          loai: "tram",
          tn: t.tn,
          minX: t.tam[0] - sx / 2,
          maxX: t.tam[0] + sx / 2,
          minZ: t.tam[1] - sz / 2,
          maxZ: t.tam[1] + sz / 2
        });
      }
    }

    // Đồ nội thất có tính năng chắn đường (chan: true)
    if (Array.isArray(banDo.noi_that)) {
      for (let i = 0; i < banDo.noi_that.length; i++) {
        const f = banDo.noi_that[i];
        if (!f.chan) continue;
        const ngang = f.huong === 90 || f.huong === 270;
        const sx = ngang ? f.kich_thuoc[2] : f.kich_thuoc[0];
        const sz = ngang ? f.kich_thuoc[0] : f.kich_thuoc[2];
        vc.push({
          loai: "noi_that",
          id: f.id,
          minX: f.tam[0] - sx / 2,
          maxX: f.tam[0] + sx / 2,
          minZ: f.tam[1] - sz / 2,
          maxZ: f.tam[1] + sz / 2
        });
      }
    }

    // Vách kính phòng con
    if (Array.isArray(banDo.phong_con)) {
      for (let i = 0; i < banDo.phong_con.length; i++) {
        const pc = banDo.phong_con[i];
        if (Array.isArray(pc.vach)) {
          for (let j = 0; j < pc.vach.length; j++) {
            const v = pc.vach[j];
            const day = v.day || 0.12;
            const xMin = Math.min(v.x1, v.x2) - day / 2;
            const xMax = Math.max(v.x1, v.x2) + day / 2;
            const zMin = Math.min(v.z1, v.z2) - day / 2;
            const zMax = Math.max(v.z1, v.z2) + day / 2;
            vc.push({
              loai: "vach_kinh",
              id: `${pc.khu}-vach-${j}`,
              minX: xMin,
              maxX: xMax,
              minZ: zMin,
              maxZ: zMax,
              chan_camera: v.chan_camera !== false,
              cao: v.cao || 2.4
            });
          }
        }
      }
    }

    return vc;
  }

  /** Trích danh sách hộp AABB 3D chắn camera { minX, maxX, minY, maxY, minZ, maxZ } */
  function layVatChanCamera(banDo) {
    const hop = [];
    if (!banDo || !banDo.phong) return hop;
    const r = banDo.phong.rong;
    const s = banDo.phong.sau;
    const h = banDo.phong.cao || 4.2;

    // 4 bức tường bao của phòng
    const day = 0.5;
    hop.push({ minX: -day, maxX: r + day, minY: 0, maxY: h, minZ: -day, maxZ: 0 });
    hop.push({ minX: -day, maxX: r + day, minY: 0, maxY: h, minZ: s, maxZ: s + day });
    hop.push({ minX: -day, maxX: 0, minY: 0, maxY: h, minZ: -day, maxZ: s + day });
    hop.push({ minX: r, maxX: r + day, minY: 0, maxY: h, minZ: -day, maxZ: s + day });

    // Đồ nội thất có chan_camera: true
    if (Array.isArray(banDo.noi_that)) {
      for (let i = 0; i < banDo.noi_that.length; i++) {
        const f = banDo.noi_that[i];
        if (!f.chan_camera) continue;
        const ngang = f.huong === 90 || f.huong === 270;
        const sx = ngang ? f.kich_thuoc[2] : f.kich_thuoc[0];
        const sz = ngang ? f.kich_thuoc[0] : f.kich_thuoc[2];
        const cao = f.kich_thuoc[1] || 2.0;
        hop.push({
          minX: f.tam[0] - sx / 2,
          maxX: f.tam[0] + sx / 2,
          minY: 0,
          maxY: cao,
          minZ: f.tam[1] - sz / 2,
          maxZ: f.tam[1] + sz / 2
        });
      }
    }

    // Vách kính phòng con có chan_camera: true
    if (Array.isArray(banDo.phong_con)) {
      for (let i = 0; i < banDo.phong_con.length; i++) {
        const pc = banDo.phong_con[i];
        if (!Array.isArray(pc.vach)) continue;
        for (let j = 0; j < pc.vach.length; j++) {
          const v = pc.vach[j];
          if (v.chan_camera === false) continue;
          const day = v.day || 0.12;
          const cao = v.cao || 2.4;
          hop.push({
            minX: Math.min(v.x1, v.x2) - day / 2,
            maxX: Math.max(v.x1, v.x2) + day / 2,
            minY: 0,
            maxY: cao,
            minZ: Math.min(v.z1, v.z2) - day / 2,
            maxZ: Math.max(v.z1, v.z2) + day / 2
          });
        }
      }
    }

    return hop;
  }

  /** Tạo lưới chiếm chỗ 2D (ô vuông o = 0.25 m, nới thêm bán kính người = 0.3 m) */
  function taoLuoi(banDo, { o = 0.25, banKinhNguoi = 0.3 } = {}) {
    const rong = banDo.phong.rong;
    const sau = banDo.phong.sau;
    const nx = Math.round(rong / o);
    const nz = Math.round(sau / o);
    const luoi = new Uint8Array(nx * nz);
    const vatCan = layVatCan(banDo);

    for (let iz = 0; iz < nz; iz++) {
      const cz = (iz + 0.5) * o;
      for (let ix = 0; ix < nx; ix++) {
        const cx = (ix + 0.5) * o;
        let chan = false;
        // Tường bao quanh phòng
        if (cx < banKinhNguoi || cx > rong - banKinhNguoi || cz < banKinhNguoi || cz > sau - banKinhNguoi) {
          chan = true;
        } else {
          // Va chạm với các vật cản
          for (let k = 0; k < vatCan.length; k++) {
            const v = vatCan[k];
            if (
              cx >= v.minX - banKinhNguoi &&
              cx <= v.maxX + banKinhNguoi &&
              cz >= v.minZ - banKinhNguoi &&
              cz <= v.maxZ + banKinhNguoi
            ) {
              chan = true;
              break;
            }
          }
        }
        if (chan) luoi[iz * nx + ix] = 1;
      }
    }

    const obj = {
      banDo,
      o,
      banKinhNguoi,
      rong,
      sau,
      nx,
      nz,
      luoi,
      vatCan,
      toaDoLuoi(x, z) {
        return [
          Math.min(nx - 1, Math.max(0, Math.floor(x / o))),
          Math.min(nz - 1, Math.max(0, Math.floor(z / o)))
        ];
      },
      toaDoThuc(ix, iz) {
        return [(ix + 0.5) * o, (iz + 0.5) * o];
      },
      laChan(ix, iz) {
        if (ix < 0 || ix >= nx || iz < 0 || iz >= nz) return true;
        return luoi[iz * nx + ix] === 1;
      },
      laChanViTri(x, z) {
        if (x < banKinhNguoi || x > rong - banKinhNguoi || z < banKinhNguoi || z > sau - banKinhNguoi) return true;
        const [ix, iz] = obj.toaDoLuoi(x, z);
        return obj.laChan(ix, iz);
      }
    };

    return obj;
  }

  /**
   * Tìm đường A* 8 hướng (không cắt góc vật cản) và làm mượt bằng string-pulling
   * @param {Object} luoi - Đối tượng trả về từ taoLuoi
   * @param {number[]} tu - Điểm xuất phát [x, z]
   * @param {number[]} den - Điểm đích [x, z]
   * @returns {number[][]|null} - Danh sách điểm [x, z] hoặc null nếu không có đường
   */
  function timDuong(luoi, tu, den) {
    if (!luoi || !tu || !den) return null;
    const nx = luoi.nx;
    const nz = luoi.nz;

    let [sx, sz] = luoi.toaDoLuoi(tu[0], tu[1]);
    let [ex, ez] = luoi.toaDoLuoi(den[0], den[1]);

    // Nếu điểm xuất phát hoặc điểm đích bị chặn, tìm ô trống gần nhất trong bán kính 1.2 m
    function timOGan(ix, iz) {
      if (!luoi.laChan(ix, iz)) return [ix, iz];
      const maxBanKinh = Math.ceil(1.2 / luoi.o);
      let totNhat = null;
      let minD = Infinity;
      for (let r = 1; r <= maxBanKinh; r++) {
        for (let dx = -r; dx <= r; dx++) {
          for (let dz = -r; dz <= r; dz++) {
            const x2 = ix + dx;
            const z2 = iz + dz;
            if (!luoi.laChan(x2, z2)) {
              const d = dx * dx + dz * dz;
              if (d < minD) {
                minD = d;
                totNhat = [x2, z2];
              }
            }
          }
        }
        if (totNhat) return totNhat;
      }
      return null;
    }

    if (luoi.laChan(sx, sz)) {
      const sMoi = timOGan(sx, sz);
      if (!sMoi) return null;
      [sx, sz] = sMoi;
    }
    if (luoi.laChan(ex, ez)) {
      const eMoi = timOGan(ex, ez);
      if (!eMoi) return null;
      [ex, ez] = eMoi;
    }

    // Kiểm tra nếu đi thẳng không vướng gì
    function kiemTraTamNhin(x1, z1, x2, z2) {
      const d = Math.hypot(x2 - x1, z2 - z1);
      const buoc = luoi.o * 0.45;
      const soBuoc = Math.max(1, Math.ceil(d / buoc));
      for (let i = 1; i < soBuoc; i++) {
        const t = i / soBuoc;
        const px = x1 + (x2 - x1) * t;
        const pz = z1 + (z2 - z1) * t;
        if (luoi.laChanViTri(px, pz)) return false;
      }
      return true;
    }

    if (sx === ex && sz === ez) {
      if (kiemTraTamNhin(tu[0], tu[1], den[0], den[1])) {
        return [[tu[0], tu[1]], [den[0], den[1]]];
      }
    }

    // A*
    const tongO = nx * nz;
    const gScore = new Float32Array(tongO).fill(Infinity);
    const fScore = new Float32Array(tongO).fill(Infinity);
    const cha = new Int32Array(tongO).fill(-1);
    const daXet = new Uint8Array(tongO);

    const SQRT2 = Math.SQRT2;
    const huong = [
      [-1, 0, 1], [1, 0, 1], [0, -1, 1], [0, 1, 1],
      [-1, -1, SQRT2], [-1, 1, SQRT2], [1, -1, SQRT2], [1, 1, SQRT2]
    ];

    const startIdx = sz * nx + sx;
    const endIdx = ez * nx + ex;

    function h(x, z) {
      const dx = Math.abs(x - ex);
      const dz = Math.abs(z - ez);
      return SQRT2 * Math.min(dx, dz) + Math.abs(dx - dz);
    }

    gScore[startIdx] = 0;
    fScore[startIdx] = h(sx, sz);

    // Mảng mở dạng flat
    const hangDoi = [startIdx];

    let timThay = false;

    while (hangDoi.length > 0) {
      // Tìm node có fScore nhỏ nhất
      let totNhatI = 0;
      let minF = fScore[hangDoi[0]];
      for (let i = 1; i < hangDoi.length; i++) {
        const f = fScore[hangDoi[i]];
        if (f < minF) {
          minF = f;
          totNhatI = i;
        }
      }
      const currIdx = hangDoi[totNhatI];
      hangDoi.splice(totNhatI, 1);

      if (currIdx === endIdx) {
        timThay = true;
        break;
      }

      daXet[currIdx] = 1;
      const cx = currIdx % nx;
      const cz = Math.floor(currIdx / nx);
      const currG = gScore[currIdx];

      for (let i = 0; i < 8; i++) {
        const [dx, dz, chiPhi] = huong[i];
        const nxCell = cx + dx;
        const nzCell = cz + dz;
        if (nxCell < 0 || nxCell >= nx || nzCell < 0 || nzCell >= nz) continue;

        const neighborIdx = nzCell * nx + nxCell;
        if (daXet[neighborIdx] || luoi.laChan(nxCell, nzCell)) continue;

        // Tránh cắt góc vật cản đối với bước chéo
        if (dx !== 0 && dz !== 0) {
          if (luoi.laChan(cx + dx, cz) || luoi.laChan(cx, cz + dz)) continue;
        }

        const tentG = currG + chiPhi;
        if (tentG < gScore[neighborIdx]) {
          cha[neighborIdx] = currIdx;
          gScore[neighborIdx] = tentG;
          fScore[neighborIdx] = tentG + h(nxCell, nzCell);
          if (!hangDoi.includes(neighborIdx)) {
            hangDoi.push(neighborIdx);
          }
        }
      }
    }

    if (!timThay) return null;

    // Truy vết đường đi ô lưới
    const cacO = [];
    let duyet = endIdx;
    while (duyet !== -1) {
      cacO.push(duyet);
      if (duyet === startIdx) break;
      duyet = cha[duyet];
    }
    cacO.reverse();

    // Chuyển sang toạ độ thực
    const cacDiem = cacO.map((idx) => {
      const ix = idx % nx;
      const iz = Math.floor(idx / nx);
      return luoi.toaDoThuc(ix, iz);
    });

    // Thay điểm đầu bằng tu và điểm cuối bằng den nếu hợp lệ
    cacDiem[0] = [tu[0], tu[1]];
    cacDiem[cacDiem.length - 1] = [den[0], den[1]];

    // Làm mượt đường đi (String-pulling / Raycast)
    const lamMuot = [cacDiem[0]];
    let hienTai = 0;

    while (hienTai < cacDiem.length - 1) {
      let xaNhat = hienTai + 1;
      for (let tiep = cacDiem.length - 1; tiep > hienTai + 1; tiep--) {
        if (kiemTraTamNhin(cacDiem[hienTai][0], cacDiem[hienTai][1], cacDiem[tiep][0], cacDiem[tiep][1])) {
          xaNhat = tiep;
          break;
        }
      }
      lamMuot.push(cacDiem[xaNhat]);
      hienTai = xaNhat;
    }

    return lamMuot;
  }

  /**
   * Di chuyển nhân vật có va chạm hình tròn – hộp trục thẳng (AABB) và trượt dọc mép
   * @param {Object} banDo - Sơ đồ phòng
   * @param {number[]} viTri - [x, z] hiện tại
   * @param {number[]} buoc - [dx, dz] khoảng dịch
   * @param {number} [banKinh=0.3] - Bán kính nhân vật
   * @returns {number[]} - [x, z] mới
   */
  function diChuyen(banDo, viTri, buoc, banKinh = 0.3) {
    if (!banDo || !viTri || !buoc) return viTri || [0, 0];
    const dist = Math.hypot(buoc[0], buoc[1]);
    if (dist <= 1e-6) return [viTri[0], viTri[1]];

    const maxSub = 0.05;
    const soBuoc = Math.max(1, Math.ceil(dist / maxSub));
    const stepX = buoc[0] / soBuoc;
    const stepZ = buoc[1] / soBuoc;

    const vatCan = layVatCan(banDo);
    const rong = banDo.phong.rong;
    const sau = banDo.phong.sau;

    let x = viTri[0];
    let z = viTri[1];

    for (let s = 0; s < soBuoc; s++) {
      // 1. Thử bước theo trục X
      let nx = x + stepX;
      nx = Math.max(banKinh, Math.min(rong - banKinh, nx));

      for (let i = 0; i < vatCan.length; i++) {
        const v = vatCan[i];
        const cx = Math.max(v.minX, Math.min(v.maxX, nx));
        const cz = Math.max(v.minZ, Math.min(v.maxZ, z));
        const dSq = (nx - cx) ** 2 + (z - cz) ** 2;
        if (dSq < banKinh * banKinh) {
          if (x <= v.minX) nx = v.minX - banKinh;
          else if (x >= v.maxX) nx = v.maxX + banKinh;
          else nx = x;
        }
      }
      x = nx;

      // 2. Thử bước theo trục Z
      let nz = z + stepZ;
      nz = Math.max(banKinh, Math.min(sau - banKinh, nz));

      for (let i = 0; i < vatCan.length; i++) {
        const v = vatCan[i];
        const cx = Math.max(v.minX, Math.min(v.maxX, x));
        const cz = Math.max(v.minZ, Math.min(v.maxZ, nz));
        const dSq = (x - cx) ** 2 + (nz - cz) ** 2;
        if (dSq < banKinh * banKinh) {
          if (z <= v.minZ) nz = v.minZ - banKinh;
          else if (z >= v.maxZ) nz = v.maxZ + banKinh;
          else nz = z;
        }
      }
      z = nz;
    }

    return [x, z];
  }

  function layToaDo(v, z2) {
    if (typeof v === "number" && typeof z2 === "number") return [v, z2];
    if (Array.isArray(v)) return [v[0], v[1]];
    if (v && typeof v === "object") return [v.x ?? v[0], v.z ?? v[1]];
    return null;
  }

  /**
   * Tìm trạm gần nhất trong bán kính tương tác
   * @param {Object} banDo - Sơ đồ phòng
   * @param {number[]|Object} viTri - [x, z] hoặc {x, z} hiện tại
   * @param {number} [banKinhToiDa]
   * @returns {{ tn: string, khoangCach: number, tram: Object }|null}
   */
  function tramGanNhat(banDo, viTri, banKinhToiDa) {
    if (!banDo || !banDo.tram || !viTri) return null;
    let pt = layToaDo(viTri);
    let bk = typeof banKinhToiDa === "number" ? banKinhToiDa : null;
    if (!pt && typeof viTri === "number" && typeof arguments[2] === "number") {
      pt = [viTri, arguments[2]];
      bk = typeof arguments[3] === "number" ? arguments[3] : null;
    }
    if (!pt) return null;
    let totNhat = null;
    let minD = Infinity;

    for (let i = 0; i < banDo.tram.length; i++) {
      const t = banDo.tram[i];
      const dTam = Math.hypot(pt[0] - t.tam[0], pt[1] - t.tam[1]);
      const dDiem = Math.hypot(pt[0] - t.diem_dung[0], pt[1] - t.diem_dung[1]);
      const d = Math.min(dTam, dDiem);
      const nguong = bk !== null ? bk : t.ban_kinh_tuong_tac;

      if (d <= nguong && d < minD) {
        minD = d;
        totNhat = t;
      }
    }

    if (!totNhat) return null;
    return { tn: totNhat.tn, khoangCach: minD, tram: totNhat };
  }

  /**
   * Xác định id khu chứa điểm [x, z]
   * @param {Object} banDo - Sơ đồ phòng
   * @param {number[]|Object} viTri - [x, z] hoặc {x, z}
   * @param {number} [zVal]
   * @returns {string|null} - id khu hoặc null
   */
  function khuTai(banDo, viTri, zVal) {
    if (!banDo || !banDo.khu || !viTri) return null;
    let pt = layToaDo(viTri, zVal);
    if (!pt) return null;
    const x = pt[0];
    const z = pt[1];

    for (let i = 0; i < banDo.khu.length; i++) {
      const k = banDo.khu[i];
      if (x >= k.hcn[0] && x <= k.hcn[2] && z >= k.hcn[1] && z <= k.hcn[3]) {
        return k.id;
      }
    }
    return null;
  }

  /**
   * Kiểm tra tính hợp lệ của sơ đồ banDo:
   * - Không có khu chồng nhau
   * - Đủ 5 trạm và mỗi trạm nằm trong khu tương ứng
   * - diem_dung không nằm trong vật cản
   * - Tìm được đường từ xuat_phat tới diem_dung của tất cả các trạm
   * - Lối đi tới mọi trạm rộng >= 1.6 m
   * @param {Object} banDo
   * @returns {string[]} - Danh sách lỗi (rỗng nếu hoàn toàn hợp lệ)
   */
  function kiemTra(banDo) {
    const loi = [];
    if (!banDo) return ["Thiếu dữ liệu banDo"];

    // 1. Kiểm tra khu
    if (!Array.isArray(banDo.khu) || banDo.khu.length < 5) {
      loi.push("Cần có đủ 5 khu");
    } else {
      for (let i = 0; i < banDo.khu.length; i++) {
        const ki = banDo.khu[i];
        for (let j = i + 1; j < banDo.khu.length; j++) {
          const kj = banDo.khu[j];
          // Kiểm tra chồng lấn diện tích dương
          const gX0 = Math.max(ki.hcn[0], kj.hcn[0]);
          const gX1 = Math.min(ki.hcn[2], kj.hcn[2]);
          const gZ0 = Math.max(ki.hcn[1], kj.hcn[1]);
          const gZ1 = Math.min(ki.hcn[3], kj.hcn[3]);
          if (gX1 - gX0 > 0.001 && gZ1 - gZ0 > 0.001) {
            loi.push(`Khu ${ki.id} và ${kj.id} chồng lấn nhau`);
          }
        }
      }
    }

    // 2. Kiểm tra trạm
    const dsTNCan = ["an-toan", "chuan-do", "nhan-biet-ion", "pin-galvani", "dien-phan"];
    if (!Array.isArray(banDo.tram) || banDo.tram.length !== 5) {
      loi.push("Cần có đúng 5 trạm thí nghiệm");
    } else {
      const coTN = banDo.tram.map((t) => t.tn);
      for (let i = 0; i < dsTNCan.length; i++) {
        if (!coTN.includes(dsTNCan[i])) {
          loi.push(`Thiếu trạm ${dsTNCan[i]}`);
        }
      }

      for (let i = 0; i < banDo.tram.length; i++) {
        const t = banDo.tram[i];
        const k = banDo.khu.find((kh) => kh.id === t.khu);
        if (!k) {
          loi.push(`Trạm ${t.tn} tham chiếu tới khu không tồn tại: ${t.khu}`);
          continue;
        }
        // Kiểm tra tâm bàn và diem_dung có nằm trong khu không
        if (
          t.tam[0] < k.hcn[0] || t.tam[0] > k.hcn[2] ||
          t.tam[1] < k.hcn[1] || t.tam[1] > k.hcn[3]
        ) {
          loi.push(`Tâm trạm ${t.tn} nằm ngoài khu ${k.id}`);
        }
        if (
          t.diem_dung[0] < k.hcn[0] || t.diem_dung[0] > k.hcn[2] ||
          t.diem_dung[1] < k.hcn[1] || t.diem_dung[1] > k.hcn[3]
        ) {
          loi.push(`Điểm dừng trạm ${t.tn} nằm ngoài khu ${k.id}`);
        }
      }
    }

    // 3. Tạo lưới để kiểm tra va chạm và tìm đường
    const luoi = taoLuoi(banDo, { o: 0.25, banKinhNguoi: 0.3 });
    const vatCan = layVatCan(banDo);

    // Kiểm tra điểm xuất phát
    const xp = banDo.xuat_phat;
    if (!xp) {
      loi.push("Thiếu điểm xuất phát xuat_phat");
    } else {
      if (luoi.laChanViTri(xp.x, xp.z)) {
        loi.push("Điểm xuất phát bị vướng vật cản hoặc tường");
      }
    }

    // Kiểm tra diem_dung của các trạm
    if (Array.isArray(banDo.tram)) {
      for (let i = 0; i < banDo.tram.length; i++) {
        const t = banDo.tram[i];
        // Điểm dừng không được nằm trong bất kỳ hộp vật cản nào (với bán kính 0.25 m)
        for (let k = 0; k < vatCan.length; k++) {
          const v = vatCan[k];
          if (
            t.diem_dung[0] > v.minX - 0.15 &&
            t.diem_dung[0] < v.maxX + 0.15 &&
            t.diem_dung[1] > v.minZ - 0.15 &&
            t.diem_dung[1] < v.maxZ + 0.15
          ) {
            loi.push(`Điểm dừng của trạm ${t.tn} bị chạm vật cản (${v.loai}: ${v.id || v.tn})`);
          }
        }

        // Tìm đường từ xuất phát tới điểm dừng của trạm
        if (xp) {
          const duong = timDuong(luoi, [xp.x, xp.z], t.diem_dung);
          if (!duong || duong.length < 2) {
            loi.push(`Không tìm được đường đi từ xuất phát tới trạm ${t.tn}`);
          }
        }
      }
    }

    // 4. Kiểm tra phòng con và lối vào
    if (Array.isArray(banDo.phong_con)) {
      for (let i = 0; i < banDo.phong_con.length; i++) {
        const pc = banDo.phong_con[i];
        if (pc.loi_vao && typeof pc.loi_vao.rong === "number") {
          if (pc.loi_vao.rong < 1.6) {
            loi.push(`Lối vào phòng con ${pc.khu} hẹp hơn 1.6 m (thực tế: ${pc.loi_vao.rong} m)`);
          }
        }
      }
    }

    return loi;
  }

  return {
    layVatCan,
    layVatChanCamera,
    taoLuoi,
    timDuong,
    diChuyen,
    tramGanNhat,
    khuTai,
    kiemTra
  };
});
