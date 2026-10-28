/**
 * mo-hinh.js - Mô hình cân bằng hoá học, hằng số Kc, Kp, bảng ICE, nguyên lý Le Chatelier.
 * Chạy độc lập trên cả Node.js và trình duyệt (window.HL_CanBang).
 */

(function (global) {
  'use strict';

  var R_SI = 8.314462618; // J/(mol·K)
  var R_BAR = 0.08314462618; // bar·L/(mol·K)

  var HL_CanBang = {
    R_SI: R_SI,
    R_BAR: R_BAR,

    /**
     * Tính Kc theo nhiệt độ T bằng phương trình van 't Hoff
     */
    kcTheoT: function (T_K, deltaH_kJ, T_ref, Kc_ref) {
      if (typeof T_K === 'object' && T_K !== null) {
        var p = T_K;
        T_K = p.T !== undefined ? p.T : (p.T_K || 298.15);
        deltaH_kJ = p.deltaH_kJ !== undefined ? p.deltaH_kJ : (p.dH_kJ || 57.2);
        T_ref = p.T_ref !== undefined ? p.T_ref : 298.15;
        Kc_ref = p.Kc_ref !== undefined ? p.Kc_ref : (p.K_ref || 0.00461);
      }
      T_K = T_K !== undefined ? T_K : 298.15;
      deltaH_kJ = deltaH_kJ !== undefined ? deltaH_kJ : 57.2;
      T_ref = T_ref !== undefined ? T_ref : 298.15;
      Kc_ref = Kc_ref !== undefined ? Kc_ref : 0.00461;

      var deltaH_J = deltaH_kJ * 1000;
      return Kc_ref * Math.exp(-(deltaH_J / R_SI) * (1 / T_K - 1 / T_ref));
    },

    /**
     * Tính Kp từ Kc: Kp = Kc * (R_bar * T)^delta_n
     */
    kpTuKc: function (Kc, T_K, delta_n) {
      if (typeof Kc === 'object' && Kc !== null) {
        var p = Kc;
        Kc = p.Kc !== undefined ? p.Kc : 0.00461;
        T_K = p.T !== undefined ? p.T : (p.T_K || 298.15);
        delta_n = p.delta_n !== undefined ? p.delta_n : 1;
      }
      Kc = Kc !== undefined ? Kc : 0.00461;
      T_K = T_K !== undefined ? T_K : 298.15;
      delta_n = delta_n !== undefined ? delta_n : 1;
      return Kc * Math.pow(R_BAR * T_K, delta_n);
    },

    /**
     * Hằng số Kp của phản ứng Haber N2 + 3H2 ⇌ 2NH3 từ nhiệt động (dH, dS)
     */
    kpHaber: function (T_K, dH_kJ, dS_J_K) {
      dH_kJ = dH_kJ !== undefined ? dH_kJ : -91.8;
      dS_J_K = dS_J_K !== undefined ? dS_J_K : -198.1;
      T_K = T_K !== undefined ? T_K : 298.15;
      var dH_J = dH_kJ * 1000;
      var lnK = -(dH_J / (R_SI * T_K)) + (dS_J_K / R_SI);
      return Math.exp(lnK);
    },

    /**
     * Thương số phản ứng Q
     */
    tinhQ: function (cA, cB, a, b) {
      if (typeof cA === 'object' && cA !== null) {
        var p = cA;
        cA = p.cA !== undefined ? p.cA : (p.N2O4 || 1);
        cB = p.cB !== undefined ? p.cB : (p.NO2 || 0);
        a = p.a !== undefined ? p.a : 1;
        b = p.b !== undefined ? p.b : 2;
      }
      cA = cA !== undefined ? cA : 1;
      cB = cB !== undefined ? cB : 0;
      a = a !== undefined ? a : 1;
      b = b !== undefined ? b : 2;
      if (cA <= 0) return Infinity;
      return Math.pow(cB, b) / Math.pow(cA, a);
    },

    /**
     * Q sau khi thay đổi thể tích hệ s lần (V_moi = V_cu / s => nồng độ nhân s)
     */
    qSauNen: function (K, s, delta_n) {
      if (typeof K === 'object' && K !== null) {
        var p = K;
        K = p.K !== undefined ? p.K : 0.00461;
        s = p.s !== undefined ? p.s : 2;
        delta_n = p.delta_n !== undefined ? p.delta_n : 1;
      }
      K = K !== undefined ? K : 0.00461;
      s = s !== undefined ? s : 2;
      delta_n = delta_n !== undefined ? delta_n : 1;
      return K * Math.pow(s, delta_n);
    },

    /**
     * Xác định chiều chuyển dịch dựa trên Q và K
     */
    chieu: function (Q, K) {
      if (typeof Q === 'object' && Q !== null) {
        var p = Q;
        Q = p.Q;
        K = p.K;
      }
      var saiKhac = (Q - K) / K;
      if (Math.abs(saiKhac) <= 0.005) return 'cân bằng';
      if (Q < K) return 'thuận';
      return 'nghịch';
    },

    /**
     * Giải nghiệm bảng ICE cho phản ứng phân ly A ⇌ 2B (N2O4 ⇌ 2NO2)
     * Kc = (cB0 + 2x)^2 / (cA0 - x)
     * Khi cB0 = 0: 4x^2 + Kc*x - Kc*cA0 = 0 => x = (-Kc + sqrt(Kc^2 + 16*Kc*cA0)) / 8
     */
    canBangPhanLi: function (cA0, cB0, Kc) {
      if (typeof cA0 === 'object' && cA0 !== null) {
        var p = cA0;
        cA0 = p.C0 !== undefined ? p.C0 : (p.cA0 !== undefined ? p.cA0 : 0.04);
        cB0 = p.cB0 !== undefined ? p.cB0 : 0;
        Kc = p.Kc !== undefined ? p.Kc : 0.00461;
      }
      cA0 = cA0 !== undefined ? cA0 : 0.04;
      cB0 = cB0 !== undefined ? cB0 : 0;
      Kc = Kc !== undefined ? Kc : 0.00461;

      if (cB0 === 0) {
        var delta = Kc * Kc + 16 * Kc * cA0;
        var x = (-Kc + Math.sqrt(delta)) / 8;
        return {
          x: x,
          cA: cA0 - x,
          cB: 2 * x,
          alpha: x / cA0,
          Q: Math.pow(2 * x, 2) / (cA0 - x)
        };
      }

      // Trường hợp tổng quát cB0 > 0:
      // (cB0 + 2x)^2 = Kc * (cA0 - x)
      // 4x^2 + (4*cB0 + Kc)*x + (cB0^2 - Kc*cA0) = 0
      var A = 4;
      var B = 4 * cB0 + Kc;
      var C = cB0 * cB0 - Kc * cA0;
      var deltaGen = B * B - 4 * A * C;
      var xGen = (-B + Math.sqrt(Math.max(0, deltaGen))) / (2 * A);
      var cA_eq = cA0 - xGen;
      var cB_eq = cB0 + 2 * xGen;
      return {
        x: xGen,
        cA: cA_eq,
        cB: cB_eq,
        alpha: xGen / (cA0 + cB0 / 2),
        Q: Math.pow(cB_eq, 2) / cA_eq
      };
    },

    /**
     * Tích phân số Euler / RK4 cho phản ứng thuận nghịch:
     * A ⇌ 2B với r_t = k_t * [A], r_n = k_n * [B]^2
     */
    buocTichPhan: function (cA, cB, Kc, dt, k_t) {
      k_t = k_t || 1.0;
      var k_n = k_t / Kc;
      var r_t = k_t * cA;
      var r_n = k_n * cB * cB;
      var r_net = r_t - r_n; // Tốc độ tiêu thụ A và sinh B

      var dcA = -r_net * dt;
      var dcB = 2 * r_net * dt;

      var cA_moi = Math.max(1e-6, cA + dcA);
      var cB_moi = Math.max(1e-6, cB + dcB);

      return {
        cA: cA_moi,
        cB: cB_moi,
        r_t: r_t,
        r_n: r_n,
        Q: Math.pow(cB_moi, 2) / cA_moi
      };
    },

    /**
     * Sinh lời giải thích Le Chatelier động từ số liệu thực
     */
    giaiThichLeChatelier: function (thongSo) {
      var Q = thongSo.Q;
      var K = thongSo.K;
      var tacDong = thongSo.tacDong || 'nén';
      var he = thongSo.he || 'N2O4_2NO2';
      var cau = [];

      var tySo = Q / K;
      if (Math.abs(tySo - 1.0) < 0.02) {
        cau.push('Hệ đang ở trạng thái cân bằng động (Q ≈ K).');
        cau.push('Tốc độ phản ứng thuận bằng tốc độ phản ứng nghịch (v_t = v_n).');
        cau.push('Nồng độ các chất và cường độ màu sắc của hệ không đổi theo thời gian.');
        return { chieu: 'cân bằng', cau: cau, vanBan: cau.join(' ') };
      }

      if (Q > K) {
        cau.push('Ngay sau tác động ' + tacDong + ', thương số phản ứng Q = ' + Q.toFixed(5) + ' > K = ' + K.toFixed(5) + '.');
        cau.push('Theo nguyên lý Le Chatelier, hệ sẽ tự chuyển dịch theo chiều nghịch để làm giảm thương số Q về lại K.');
        if (he === 'N2O4_2NO2') {
          cau.push('Chiều nghịch làm giảm nồng độ khí NO₂ nâu đỏ và tăng nồng độ N₂O₄ không màu, làm giảm tổng số mol khí.');
        } else {
          cau.push('Tốc độ phản ứng nghịch lớn hơn tốc độ phản ứng thuận (v_n > v_t).');
        }
        return { chieu: 'nghịch', cau: cau, vanBan: cau.join(' ') };
      } else {
        cau.push('Ngay sau tác động ' + tacDong + ', thương số phản ứng Q = ' + Q.toFixed(5) + ' < K = ' + K.toFixed(5) + '.');
        cau.push('Theo nguyên lý Le Chatelier, hệ sẽ tự chuyển dịch theo chiều thuận để tăng nồng độ sản phẩm.');
        if (he === 'N2O4_2NO2') {
          cau.push('Chiều thuận tạo thêm sản phẩm NO₂ nâu đỏ, đưa thương số Q tăng dần cho tới khi bằng K.');
        } else {
          cau.push('Tốc độ phản ứng thuận lớn hơn tốc độ phản ứng nghịch (v_t > v_n).');
        }
        return { chieu: 'thuận', cau: cau, vanBan: cau.join(' ') };
      }
    },

    // Giữ tương thích với tên gọi cũ
    tinhKc: function (p) {
      var a = p.a !== undefined ? p.a : 1;
      var b = p.b !== undefined ? p.b : 2;
      return Math.pow(p.cB, b) / Math.pow(p.cA, a);
    },
    tiLeKcXucTac: function () {
      return 1.0;
    }
  };

  if (typeof module !== 'undefined' && module.exports) {
    module.exports = HL_CanBang;
  }
  global.HL_CanBang = HL_CanBang;
})(typeof window !== 'undefined' ? window : global);
