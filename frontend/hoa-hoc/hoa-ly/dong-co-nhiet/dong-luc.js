/**
 * dong-luc.js - Động lực học bánh đà: quán tính tự do và chế độ đề máy (I * dω/dt = τ_khí - τ_ma_sát - τ_tải).
 * Chuẩn UMD: window.HLDongLuc.
 * Không chạm DOM.
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.HLDongLuc = factory();
  }
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';

  var DEG2RAD = Math.PI / 180;
  var RAD2DEG = 180 / Math.PI;

  function taoMoHinhDongLuc(ts) {
    ts = ts || {};
    var I = ts.I !== undefined ? ts.I : 0.05; // kg·m² (mô-men quán tính bánh đà)
    var c_quan_tinh = 0.015; // hệ số cản quán tính tự do
    var ma_sat_tinh = 2.0; // deg/s² ma sát khô

    // Tham số đề máy
    var c0 = 1.2; // N·m (ma sát tĩnh/cơ khí)
    var c1 = 0.008; // N·m·s/rad (ma sát nhớt)
    var tai_can = 0; // N·m (tải bên ngoài)

    var p_atm_Pa = 101325; // Pa

    /**
     * Cập nhật một bước quán tính tự do
     * @param {number} omega_deg - vận tốc góc deg/s
     * @param {number} dt_s - bước thời gian giây
     */
    function buocQuanTinh(omega_deg, dt_s) {
      if (Math.abs(omega_deg) < 2.0) {
        return 0; // dừng hẳn khi < 2°/s
      }
      var huong = omega_deg > 0 ? 1 : -1;
      var dOmega = (-c_quan_tinh * omega_deg - ma_sat_tinh * huong) * dt_s;
      var omega_moi = omega_deg + dOmega;

      // Nếu đổi dấu do ma sát thì dừng
      if (omega_moi * omega_deg < 0) {
        return 0;
      }
      return omega_moi;
    }

    /**
     * Cập nhật mô hình Đề máy (nhiều bước con dt <= 1e-4 s)
     * @param {number} phi_deg - góc trục khuỷu hiện tại (độ)
     * @param {number} omega_rad_s - vận tốc góc hiện tại (rad/s)
     * @param {number} dt_khung - thời gian khung hình thật (giây)
     * @param {Function} layTrangThaiKhi - hàm trả về { p_Pa, ds_dtheta_m_rad }
     */
    function buocDeMay(phi_deg, omega_rad_s, dt_khung, layTrangThaiKhi, tuyChon) {
      tuyChon = tuyChon || {};
      var tai = tuyChon.tai !== undefined ? tuyChon.tai : tai_can;
      var I_dung = tuyChon.I !== undefined ? tuyChon.I : I;
      var c0_dung = tuyChon.c0 !== undefined ? tuyChon.c0 : c0;
      var c1_dung = tuyChon.c1 !== undefined ? tuyChon.c1 : c1;
      var p_atm_dung = tuyChon.p_atm !== undefined ? tuyChon.p_atm : 100000; // 1 bar (100 kPa)

      var dt_sub = 1e-4; // 0.1 ms mỗi bước con
      var soBuoc = Math.max(1, Math.ceil(dt_khung / dt_sub));
      var dt_thuc = dt_khung / soBuoc;

      var phi = phi_deg;
      var omega = omega_rad_s;
      var omega_idle = 1300 * (2 * Math.PI / 60);

      for (var s = 0; s < soBuoc; s++) {
        var khi = layTrangThaiKhi(phi);
        var p_Pa = khi.p_Pa !== undefined ? khi.p_Pa : p_atm_dung;
        var A_m2 = khi.A_m2 || 0.0050265;
        var ds_dtheta_m = khi.ds_dtheta_m || 0; // m/rad

        // Mô-men do áp suất khí tác dụng lên trục khuỷu: tau_khi = (p - p_atm) * A * ds/dtheta
        var tau_khi = (p_Pa - p_atm_dung) * A_m2 * ds_dtheta_m; // N·m

        var dieuToc = tuyChon.dieuToc !== undefined ? tuyChon.dieuToc : (c0_dung > 0);
        // Bướm ga tự khép khi chạy không tải vượt ngưỡng idle
        if (dieuToc && tau_khi > 0 && omega > omega_idle) {
          var heSoGa = Math.max(0.04, 1.0 - (omega - omega_idle) / 20);
          tau_khi *= heSoGa;
        }

        // Mô-men cản: ma sát + tải
        var tau_ma_sat = (c0_dung + c1_dung * Math.abs(omega)) * (omega >= 0 ? 1 : -1);
        var tau_tong = tau_khi - tau_ma_sat - (omega > 0 ? tai : (omega < 0 ? -tai : 0));

        // Phương trình quay: I * domega/dt = tau_tong
        var dOmega = (tau_tong / I_dung) * dt_thuc;
        omega += dOmega;

        // Động cơ 4 kỳ khi bị khựng thì chết máy, không quay lùi
        if (omega < 0) {
          omega = 0;
          break;
        }

        // Giới hạn tốc độ tối đa 6000 vòng/phút = 628 rad/s
        var max_omega = 6000 * (2 * Math.PI / 60);
        omega = Math.min(max_omega, omega);

        // Cập nhật góc phi
        phi += (omega * RAD2DEG) * dt_thuc;
      }

      return {
        phi: phi,
        omega_rad_s: omega,
        rpm: omega * (60 / (2 * Math.PI)),
      };
    }

    return {
      buocQuanTinh: buocQuanTinh,
      buocDeMay: buocDeMay,
    };
  }

  return {
    taoMoHinhDongLuc: taoMoHinhDongLuc,
  };
});
