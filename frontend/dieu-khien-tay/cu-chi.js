function factory() {
  'use strict';

  // ============================================================================
  // Hằng số cử chỉ & nhận diện (khớp 1:1 với bản Python)
  // ============================================================================
  var HANG_SO = {
    CHAM_BAT: 0.32,     // tỉ lệ chụm < 0.32 là chạm (26/10: 0.25 → 0.32, người dùng chụm đo ~0.20 nên "chụm khó ăn")
    CHAM_NHA: 0.45,     // tỉ lệ chụm > 0.45 là nhả
    GOC_DAU: [-25 * Math.PI / 180, 8 * Math.PI / 180], // góc nhìn đầu (yaw, pitch)
    ZOOM_MIN: 0.45,
    ZOOM_MAX: 3.0,
    PITCH_MAX: 75 * Math.PI / 180,
    DO_NHAY: 3.5,       // rad xoay khi tay đi một chiều cao khung
    NGUONG_TRUOT: 0.008,// ngưỡng trượt để bắt đầu xoay (26/10: 0.015 → 0.008 cho đỡ trễ)
    QT_NGUONG: 0.6,     // ngưỡng tốc độ để lướt quán tính
    QT_TAT: 0.6,        // hằng số thời gian tắt dần quán tính
    QT_MAX: 8.0,
    MAT_TAY_S: 0.15,    // thời gian chờ khi mất dấu tay
    XOE_GIU_S: 1.0,     // thời gian giữ xoè để về góc đầu
    ONE_EURO: {         // 26/10: (0.8, 5, 1) → (1, 15, 3): bắt kịp tay sau 100 ms thay vì 200 ms, vẫn lọc ~63 % rung
      minCutoff: 1.0,
      beta: 15.0,
      dCutoff: 3.0
    }
  };

  var VUNG = [[0.15, 0.85], [0.12, 0.82]]; // vùng tay hay hoạt động
  // Chạm nhẹ (phần mở rộng, không có trong bản Python): chụm rồi nhả trong chừng này giây mà chưa kéo = chọn cơ quan.
  // 26/10: 0.3 → 0.45 s – xác nhận chụm và nhả mỗi bên cần 2 khung, 0.3 s hay hụt.
  var CHAM_NHE_S = 0.45;

  function kep(x, a, b) {
    return x < a ? a : x > b ? b : x;
  }

  function gocNgan(a) {
    var haiPi = 2 * Math.PI;
    var m = (a + Math.PI) % haiPi;
    if (m < 0) m += haiPi;
    return m - Math.PI;
  }

  // ============================================================================
  // Lọc One Euro (lọc rung con trỏ)
  // ============================================================================
  function LocOneEuro(minCutoff, beta, dCutoff) {
    this.mc = minCutoff !== undefined ? minCutoff : HANG_SO.ONE_EURO.minCutoff;
    this.beta = beta !== undefined ? beta : HANG_SO.ONE_EURO.beta;
    this.dc = dCutoff !== undefined ? dCutoff : HANG_SO.ONE_EURO.dCutoff;
    this.x = null;
    this.dx = null;
    this.t = null;
  }

  LocOneEuro._a = function (cutoff, te) {
    return 1.0 / (1.0 + 1.0 / (2 * Math.PI * cutoff * te));
  };

  LocOneEuro.prototype.loc = function (x, t) {
    var x0 = Number(x[0]);
    var x1 = Number(x[1]);
    if (isNaN(x0)) x0 = 0;
    if (isNaN(x1)) x1 = 0;

    if (this.x === null || t <= this.t) {
      this.x = [x0, x1];
      this.dx = [0, 0];
      this.t = t;
      return [x0, x1];
    }
    var te = t - this.t;
    this.t = t;

    var a_d = LocOneEuro._a(this.dc, te);
    this.dx[0] = this.dx[0] + a_d * ((x0 - this.x[0]) / te - this.dx[0]);
    this.dx[1] = this.dx[1] + a_d * ((x1 - this.x[1]) / te - this.dx[1]);

    var norm_dx = Math.hypot(this.dx[0], this.dx[1]);
    var a_x = LocOneEuro._a(this.mc + this.beta * norm_dx, te);
    this.x[0] = this.x[0] + a_x * (x0 - this.x[0]);
    this.x[1] = this.x[1] + a_x * (x1 - this.x[1]);

    return [this.x[0], this.x[1]];
  };

  // ============================================================================
  // Trạng thái một bàn tay
  // ============================================================================
  function Tay() {
    this.loc = new LocOneEuro();
    this.cham = false;
    this.dem = 0;
    this.thayLuc = -1.0;
  }

  // ============================================================================
  // Tỉ lệ chụm & Kiểm tra xoè
  // ============================================================================
  function tiLeChum(diem, theGioi, tiLeKhung) {
    if (!diem || diem.length < 21) return 9.0;
    var kc;
    if (theGioi && theGioi.length >= 21) {
      kc = function (i, j) {
        var p = theGioi[i];
        var q = theGioi[j];
        if (!p || !q) return 0;
        var px = p.x !== undefined ? p.x : p[0];
        var py = p.y !== undefined ? p.y : p[1];
        var pz = p.z !== undefined ? p.z : (p[2] || 0);
        var qx = q.x !== undefined ? q.x : q[0];
        var qy = q.y !== undefined ? q.y : q[1];
        var qz = q.z !== undefined ? q.z : (q[2] || 0);
        return Math.hypot(px - qx, py - qy, pz - qz);
      };
    } else {
      kc = function (i, j) {
        var p = diem[i];
        var q = diem[j];
        if (!p || !q) return 0;
        var px = p.x !== undefined ? p.x : p[0];
        var py = p.y !== undefined ? p.y : p[1];
        var qx = q.x !== undefined ? q.x : q[0];
        var qy = q.y !== undefined ? q.y : q[1];
        return Math.hypot((px - qx) * tiLeKhung, py - qy);
      };
    }

    var coTay = kc(0, 9);
    if (isNaN(coTay) || coTay <= 1e-6) return 9.0;
    var kcAB = kc(4, 8);
    return isNaN(kcAB) ? 9.0 : kcAB / coTay;
  }

  function laXoe(diem, tiLe) {
    if (!diem || diem.length < 21) return false;
    var c = diem[0];
    if (!c) return false;
    var cx = c.x !== undefined ? c.x : c[0];
    var cy = c.y !== undefined ? c.y : c[1];

    var kc = function (i) {
      var p = diem[i];
      if (!p) return 0;
      var px = p.x !== undefined ? p.x : p[0];
      var py = p.y !== undefined ? p.y : p[1];
      return Math.hypot((px - cx) * tiLe, py - cy);
    };

    var cap = [[8, 6], [12, 10], [16, 14], [20, 18]];
    for (var k = 0; k < cap.length; k++) {
      var dau = cap[k][0];
      var giua = cap[k][1];
      if (!(kc(dau) > kc(giua) * 1.15)) {
        return false;
      }
    }
    return true;
  }

  // ============================================================================
  // Đọc tay từ kết quả MediaPipe
  // ============================================================================
  function docTay(kq, tiLe, t, cacTay) {
    var ra = [];
    if (!kq) return ra;
    var hl = kq.hand_landmarks || kq.landmarks;
    if (!hl) return ra;
    var hwl = kq.hand_world_landmarks || kq.worldLandmarks;
    var hd = kq.handedness || kq.handednesses;

    for (var i = 0; i < hl.length; i++) {
      var diem = hl[i];
      if (!diem || diem.length < 21) continue;

      var ben = 'Tay' + i;
      if (hd && hd[i] && hd[i][0]) {
        var hItem = hd[i][0];
        ben = hItem.categoryName || hItem.category_name || ben;
      }

      var tay = cacTay[ben];
      if (!tay || (t - tay.thayLuc > 0.3)) {
        tay = cacTay[ben] = new Tay();
      }
      tay.thayLuc = t;

      var theGioi = hwl ? hwl[i] : null;
      var tl = tiLeChum(diem, theGioi, tiLe);

      var muonCham = !tay.cham ? (tl < HANG_SO.CHAM_BAT) : (tl <= HANG_SO.CHAM_NHA);
      tay.dem = (muonCham !== tay.cham) ? tay.dem + 1 : 0;
      if (tay.dem >= 2) {
        tay.cham = muonCham;
        tay.dem = 0;
      }

      var p4 = diem[4];
      var p8 = diem[8];
      var p4x = p4.x !== undefined ? p4.x : p4[0];
      var p4y = p4.y !== undefined ? p4.y : p4[1];
      var p8x = p8.x !== undefined ? p8.x : p8[0];
      var p8y = p8.y !== undefined ? p8.y : p8[1];

      var xCam = (p4x + p8x) / 2;
      var yCam = (p4y + p8y) / 2;
      var diemChum = [xCam * tiLe, yCam];

      var conTro = tay.loc.loc(diemChum, t);
      var xoe = (tl > 0.6) && laXoe(diem, tiLe);

      ra.push({
        ben: ben,
        diem: diem,
        conTro: conTro,
        cham: tay.cham,
        tl: tl,
        xoe: xoe,
        _xCam: xCam,
        _yCam: yCam,
        _tiLe: tiLe
      });
    }
    return ra;
  }

  // ============================================================================
  // Bộ điều khiển (cử chỉ → góc quay + zoom)
  // ============================================================================
  function DieuKhien(tuyChon) {
    tuyChon = tuyChon || {};
    this.veGocDauNoiBo = tuyChon.veGocDauNoiBo !== undefined ? Boolean(tuyChon.veGocDauNoiBo) : true;
    this.khiSuKien = typeof tuyChon.khiSuKien === 'function' ? tuyChon.khiSuKien : null;

    this.yaw = HANG_SO.GOC_DAU[0];
    this.pitch = HANG_SO.GOC_DAU[1];
    this.zoom = 1.0;

    this.wYaw = 0.0;
    this.wPitch = 0.0;
    this.cheDo = 'cho'; // cho | keo | zoom | quan_tinh
    this.veDau = false;

    this.benKeo = null;
    this.gocKeo = null;
    this.gocKeoCam = null;
    this.cuoi = null;
    this.daTruot = false;
    this.tBatDauKeo = 0.0;

    this.lichSu = []; // deque 90 mẫu: [t, yaw, pitch]
    this.kcZoom = null;
    this.matTayTu = null;

    this.xoeTu = null;
    this.daDatLai = false;

    this._dangHoatDong = false;
  }

  DieuKhien.prototype._phat = function (kieu, duLieu) {
    if (this.khiSuKien) {
      try {
        var goi = Object.assign({ kieu: kieu }, duLieu || {});
        this.khiSuKien(goi);
      } catch (e) {
        console.error(e);
      }
    }
  };

  DieuKhien.prototype.veGocDau = function () {
    this.wYaw = 0.0;
    this.wPitch = 0.0;
    if (this.veGocDauNoiBo) {
      this.veDau = true;
    } else {
      this.veDau = false;
    }
    if (this.cheDo === 'quan_tinh') {
      this.cheDo = 'cho';
      if (this._dangHoatDong) {
        this._dangHoatDong = false;
        this._phat('ket_thuc');
      }
    }
    this._phat('dat_lai');
  };

  DieuKhien.prototype._batDau = function () {
    this.wYaw = 0.0;
    this.wPitch = 0.0;
    this.veDau = false;
    if (!this._dangHoatDong) {
      this._dangHoatDong = true;
      this._phat('bat_dau');
    }
  };

  DieuKhien.prototype._tha = function (t) {
    this.cheDo = 'cho';
    if (!this.daTruot || this.lichSu.length < 2 || (t - this.lichSu[this.lichSu.length - 1][0] > 0.25)) {
      if (this._dangHoatDong) {
        this._dangHoatDong = false;
        this._phat('ket_thuc');
      }
      return;
    }

    var t1 = this.lichSu[this.lichSu.length - 1][0];
    var mau = [];
    for (var i = 0; i < this.lichSu.length; i++) {
      if (this.lichSu[i][0] >= t1 - 0.12) {
        mau.push(this.lichSu[i]);
      }
    }

    var m0 = mau[0];
    var m1 = mau[mau.length - 1];
    var dtMau = m1[0] - m0[0];
    if (dtMau < 0.03) {
      if (this._dangHoatDong) {
        this._dangHoatDong = false;
        this._phat('ket_thuc');
      }
      return;
    }

    var wy = (m1[1] - m0[1]) / dtMau;
    var wp = (m1[2] - m0[2]) / dtMau;
    if (Math.hypot(wy, wp) > HANG_SO.QT_NGUONG) {
      this.wYaw = kep(wy, -HANG_SO.QT_MAX, HANG_SO.QT_MAX);
      this.wPitch = kep(wp, -HANG_SO.QT_MAX, HANG_SO.QT_MAX) * 0.5;
      this.cheDo = 'quan_tinh';
    } else {
      if (this._dangHoatDong) {
        this._dangHoatDong = false;
        this._phat('ket_thuc');
      }
    }
  };

  DieuKhien.prototype.capNhat = function (t, dt, dsTay) {
    dsTay = dsTay || [];
    var dangCham = [];
    for (var i = 0; i < dsTay.length; i++) {
      if (dsTay[i].cham) dangCham.push(dsTay[i]);
    }

    // Xoè cả hai tay giữ 1 giây → về góc đầu (mỗi lần xoè chỉ một lần)
    if (dsTay.length === 2 && dsTay[0].xoe && dsTay[1].xoe) {
      this.xoeTu = this.xoeTu !== null ? this.xoeTu : t;
      if (!this.daDatLai && (t - this.xoeTu >= HANG_SO.XOE_GIU_S)) {
        this.veGocDau();
        this.daDatLai = true;
      }
    } else {
      this.xoeTu = null;
      this.daDatLai = false;
    }

    if (dangCham.length >= 2) {
      // Hai tay chụm → phóng to / thu nhỏ
      var dx = dangCham[0].conTro[0] - dangCham[1].conTro[0];
      var dy = dangCham[0].conTro[1] - dangCham[1].conTro[1];
      var kc = Math.hypot(dx, dy);

      if (this.cheDo !== 'zoom') {
        this._batDau();
        this.cheDo = 'zoom';
        this.kcZoom = kc;
      } else if (kc > 1e-3 && this.kcZoom > 1e-3) {
        var heSo = kep(kc / this.kcZoom, 0.9, 1.1);
        if (Math.abs(heSo - 1) > 0.003) {
          this.zoom = kep(this.zoom * heSo, HANG_SO.ZOOM_MIN, HANG_SO.ZOOM_MAX);
        }
        this.kcZoom = kc;
      }
      this.matTayTu = null;
    } else if (dangCham.length === 1) {
      // Một tay chụm → kéo để xoay
      var h = dangCham[0];
      var p = [h.conTro[0], h.conTro[1]];
      var pCam = [h._xCam !== undefined ? h._xCam : (h.conTro[0] / (h._tiLe || 1)),
                  h._yCam !== undefined ? h._yCam : h.conTro[1]];

      if (this.cheDo !== 'keo' || this.benKeo !== h.ben) {
        this._batDau();
        this.cheDo = 'keo';
        this.benKeo = h.ben;
        this.gocKeo = [p[0], p[1]];
        this.gocKeoCam = [pCam[0], pCam[1]];
        this.cuoi = [p[0], p[1]];
        this.daTruot = false;
        this.tBatDauKeo = t;
        this.lichSu = [];
      } else {
        var dCuoiX = p[0] - this.cuoi[0];
        var dCuoiY = p[1] - this.cuoi[1];
        if (Math.hypot(dCuoiX, dCuoiY) > 0.2) {
          this.cuoi = [p[0], p[1]];
        }

        var dGocX = p[0] - this.gocKeo[0];
        var dGocY = p[1] - this.gocKeo[1];
        if (!this.daTruot && Math.hypot(dGocX, dGocY) > HANG_SO.NGUONG_TRUOT) {
          this.daTruot = true;
          this.cuoi = [p[0], p[1]];
        }

        if (this.daTruot && h.tl < (HANG_SO.CHAM_BAT + HANG_SO.CHAM_NHA) / 2) {
          var dX = p[0] - this.cuoi[0];
          var dY = p[1] - this.cuoi[1];
          this.yaw += dX * HANG_SO.DO_NHAY;
          this.pitch = kep(this.pitch + dY * HANG_SO.DO_NHAY, -HANG_SO.PITCH_MAX, HANG_SO.PITCH_MAX);
          this.lichSu.push([t, this.yaw, this.pitch]);
          if (this.lichSu.length > 90) this.lichSu.shift();
        }
        this.cuoi = [p[0], p[1]];
      }
      this.matTayTu = null;
    } else if (this.cheDo === 'keo') {
      var tayVanCon = false;
      for (var k = 0; k < dsTay.length; k++) {
        if (dsTay[k].ben === this.benKeo) {
          tayVanCon = true;
          break;
        }
      }
      if (tayVanCon) {
        // Tay vẫn còn, chỉ nhả ngón: kiểm tra chạm nhẹ
        if (!this.daTruot && (t - this.tBatDauKeo <= CHAM_NHE_S) && this.gocKeoCam) {
          var x0 = VUNG[0][0], x1 = VUNG[0][1];
          var y0 = VUNG[1][0], y1 = VUNG[1][1];
          var xChon = kep((this.gocKeoCam[0] - x0) / (x1 - x0), 0, 1);
          var yChon = kep((this.gocKeoCam[1] - y0) / (y1 - y0), 0, 1);
          this._phat('cham_chon', { x: xChon, y: yChon });
        }
        this._tha(t);
      } else {
        // Mất tay
        this.matTayTu = this.matTayTu !== null ? this.matTayTu : t;
        if (t - this.matTayTu > HANG_SO.MAT_TAY_S) {
          this.cheDo = 'cho';
          this.matTayTu = null;
          if (this._dangHoatDong) {
            this._dangHoatDong = false;
            this._phat('ket_thuc');
          }
        }
      }
    } else if (this.cheDo === 'zoom') {
      this.matTayTu = this.matTayTu !== null ? this.matTayTu : t;
      if (dsTay.length > 0 || (t - this.matTayTu > HANG_SO.MAT_TAY_S)) {
        this.cheDo = 'cho';
        this.matTayTu = null;
        if (this._dangHoatDong) {
          this._dangHoatDong = false;
          this._phat('ket_thuc');
        }
      }
    }

    if (this.cheDo === 'quan_tinh') {
      this.yaw += this.wYaw * dt;
      var pMoi = this.pitch + this.wPitch * dt;
      this.pitch = kep(pMoi, -HANG_SO.PITCH_MAX, HANG_SO.PITCH_MAX);
      if (this.pitch !== pMoi) {
        this.wPitch = 0.0;
      }
      this.wYaw *= Math.exp(-dt / HANG_SO.QT_TAT);
      this.wPitch *= Math.exp(-dt / (HANG_SO.QT_TAT / 2));
      if (Math.hypot(this.wYaw, this.wPitch) < 0.05) {
        this.cheDo = 'cho';
        if (this._dangHoatDong) {
          this._dangHoatDong = false;
          this._phat('ket_thuc');
        }
      }
    }

    if (this.veDau) {
      var heSoVeDau = 1 - Math.exp(-dt / 0.15);
      var lech = gocNgan(HANG_SO.GOC_DAU[0] - this.yaw);
      this.yaw += lech * heSoVeDau;
      this.pitch += (HANG_SO.GOC_DAU[1] - this.pitch) * heSoVeDau;
      this.zoom += (1.0 - this.zoom) * heSoVeDau;
      if (Math.abs(lech) < 1e-3 && Math.abs(HANG_SO.GOC_DAU[1] - this.pitch) < 1e-3 && Math.abs(1.0 - this.zoom) < 1e-3) {
        this.veDau = false;
      }
    }
  };

  // ============================================================================
  // Đổi toạ độ ra khung hiển thị
  // ============================================================================
  function raCuaSo(conTro, tiLe, rong, cao) {
    var x = conTro[0] / tiLe;
    var y = conTro[1];
    var x0 = VUNG[0][0], x1 = VUNG[0][1];
    var y0 = VUNG[1][0], y1 = VUNG[1][1];
    var rx = kep((x - x0) / (x1 - x0), 0, 1) * rong;
    var ry = kep((y - y0) / (y1 - y0), 0, 1) * cao;
    return [Math.round(rx), Math.round(ry)];
  }

  return {
    HANG_SO: HANG_SO,
    VUNG: VUNG,
    CHAM_NHE_S: CHAM_NHE_S,
    kep: kep,
    gocNgan: gocNgan,
    LocOneEuro: LocOneEuro,
    Tay: Tay,
    tiLeChum: tiLeChum,
    laXoe: laXoe,
    docTay: docTay,
    DieuKhien: DieuKhien,
    raCuaSo: raCuaSo
  };
}

const _cuChi = factory();
if (typeof self !== 'undefined') {
  self.DieuKhienTayCuChi = _cuChi;
}
if (typeof module !== 'undefined' && module.exports) {
  module.exports = _cuChi;
}

export default _cuChi;
export const HANG_SO = _cuChi.HANG_SO;
export const VUNG = _cuChi.VUNG;
export const CHAM_NHE_S = _cuChi.CHAM_NHE_S;
export const kep = _cuChi.kep;
export const gocNgan = _cuChi.gocNgan;
export const LocOneEuro = _cuChi.LocOneEuro;
export const Tay = _cuChi.Tay;
export const tiLeChum = _cuChi.tiLeChum;
export const laXoe = _cuChi.laXoe;
export const docTay = _cuChi.docTay;
export const DieuKhien = _cuChi.DieuKhien;
export const raCuaSo = _cuChi.raCuaSo;
