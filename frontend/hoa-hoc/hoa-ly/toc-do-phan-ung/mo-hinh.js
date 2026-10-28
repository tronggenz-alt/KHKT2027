/**
 * mo-hinh.js - Mô hình động học phản ứng, phương trình Arrhenius, bậc phản ứng.
 * Chạy độc lập trên cả Node.js và trình duyệt (window.HL_TocDo).
 */

(function (global) {
  'use strict';

  var R_SI = 8.314462618; // J/(mol·K)

  /**
   * Tính erfc chính xác cho x lớn bằng chuỗi tiệm cận
   */
  function erfc(z) {
    if (z > 4) {
      var z2 = z * z;
      return (Math.exp(-z2) / (z * Math.sqrt(Math.PI))) * (1 - 1 / (2 * z2) + 3 / (4 * z2 * z2));
    }
    // Xấp xỉ Chebyshev cho z <= 4
    var t = 1.0 / (1.0 + 0.5 * z);
    var ans = t * Math.exp(-z * z - 1.26551223 +
      t * (1.00002368 +
      t * (0.37409196 +
      t * (0.09678418 +
      t * (-0.18628806 +
      t * (0.27886807 +
      t * (-1.13520398 +
      t * (1.48851587 +
      t * (-0.82215223 +
      t * 0.17087277)))))))));
    return ans;
  }

  var HL_TocDo = {
    R_SI: R_SI,

    /**
     * Hằng số tốc độ phản ứng k theo phương trình Arrhenius
     */
    kTheoArrhenius: function (A, Ea_kJ, T_K) {
      if (typeof A === 'object' && A !== null) {
        var p = A;
        A = p.A !== undefined ? p.A : 1e10;
        Ea_kJ = p.Ea_kJ !== undefined ? p.Ea_kJ : (p.Ea || 50.0);
        T_K = p.T !== undefined ? p.T : (p.T_K || 298.15);
      }
      A = A !== undefined ? A : 1e10;
      Ea_kJ = Ea_kJ !== undefined ? Ea_kJ : 50.0;
      T_K = T_K !== undefined ? T_K : 298.15;
      var Ea_J = Ea_kJ * 1000;
      return A * Math.exp(-Ea_J / (R_SI * T_K));
    },

    /**
     * Tỉ số tốc độ phản ứng k2 / k1
     */
    tiLeTocDo: function (Ea_kJ, T1, T2) {
      if (typeof Ea_kJ === 'object' && Ea_kJ !== null) {
        var p = Ea_kJ;
        Ea_kJ = p.Ea_kJ !== undefined ? p.Ea_kJ : 50.0;
        T1 = p.T1 !== undefined ? p.T1 : 298.15;
        T2 = p.T2 !== undefined ? p.T2 : 308.15;
      }
      var Ea_J = Ea_kJ * 1000;
      return Math.exp(-(Ea_J / R_SI) * (1 / T2 - 1 / T1));
    },

    /**
     * Hệ số nhiệt độ Van 't Hoff gamma xấp xỉ theo D3: gamma = exp(10*Ea/(R*T^2))
     */
    heSoVantHoff: function (Ea_kJ, T_K) {
      Ea_kJ = Ea_kJ !== undefined ? Ea_kJ : 50.0;
      T_K = T_K !== undefined ? T_K : 298.15;
      var Ea_J = Ea_kJ * 1000;
      return Math.exp((10 * Ea_J) / (R_SI * T_K * T_K));
    },

    /**
     * Tìm Ea (kJ/mol) để gamma đạt giá trị cho trước ở nhiệt độ T
     */
    eaTuGamma: function (gamma, T_K) {
      gamma = gamma !== undefined ? gamma : 2.0;
      T_K = T_K !== undefined ? T_K : 298.15;
      var Ea_J = (R_SI * T_K * T_K * Math.log(gamma)) / 10;
      return Ea_J / 1000;
    },

    /**
     * Hệ số tăng tốc độ khi có chất xúc tác làm giảm Ea: exp(deltaEa / (R*T))
     */
    heSoTangXucTac: function (deltaEa_kJ, T_K) {
      if (typeof deltaEa_kJ === 'object' && deltaEa_kJ !== null) {
        var p = deltaEa_kJ;
        deltaEa_kJ = p.deltaEa_kJ !== undefined ? p.deltaEa_kJ : (p.deltaEa || 20.0);
        T_K = p.T !== undefined ? p.T : 298.15;
      }
      deltaEa_kJ = deltaEa_kJ !== undefined ? deltaEa_kJ : 20.0;
      T_K = T_K !== undefined ? T_K : 298.15;
      var deltaEa_J = deltaEa_kJ * 1000;
      return Math.exp(deltaEa_J / (R_SI * T_K));
    },

    /**
     * Khớp Arrhenius tuyến tính: ln(k) = ln(A) - (Ea/R) * (1/T)
     */
    khopArrhenius: function (danhSachDiem) {
      if (!danhSachDiem || danhSachDiem.length < 2) return null;
      var N = danhSachDiem.length;
      var sumX = 0, sumY = 0, sumXX = 0, sumXY = 0;
      for (var i = 0; i < N; i++) {
        var x = 1.0 / danhSachDiem[i].T;
        var y = Math.log(danhSachDiem[i].k);
        sumX += x;
        sumY += y;
        sumXX += x * x;
        sumXY += x * y;
      }
      var denom = N * sumXX - sumX * sumX;
      if (Math.abs(denom) < 1e-15) return null;
      var slope = (N * sumXY - sumX * sumY) / denom;
      var intercept = (sumY - slope * sumX) / N;

      var Ea_kJ = (-slope * R_SI) / 1000;
      var A = Math.exp(intercept);
      return {
        Ea_kJ: Ea_kJ,
        A: A,
        slope: slope,
        intercept: intercept
      };
    },

    /**
     * Bậc phản ứng: Bậc 0
     */
    nongDoBac0: function (C0, k, t) {
      if (typeof C0 === 'object' && C0 !== null) {
        var p = C0;
        C0 = p.C0 !== undefined ? p.C0 : 1.0;
        k = p.k !== undefined ? p.k : 0.01;
        t = p.t !== undefined ? p.t : 0;
      }
      return Math.max(0, C0 - k * t);
    },
    thoiGianBanHuyBac0: function (C0, k) {
      if (typeof C0 === 'object' && C0 !== null) {
        var p = C0;
        C0 = p.C0 !== undefined ? p.C0 : 1.0;
        k = p.k !== undefined ? p.k : 0.01;
      }
      return C0 / (2 * k);
    },

    /**
     * Bậc phản ứng: Bậc 1
     */
    nongDoBac1: function (C0, k, t) {
      if (typeof C0 === 'object' && C0 !== null) {
        var p = C0;
        C0 = p.C0 !== undefined ? p.C0 : 1.0;
        k = p.k !== undefined ? p.k : 0.01;
        t = p.t !== undefined ? p.t : 0;
      }
      return C0 * Math.exp(-k * t);
    },
    thoiGianBanHuyBac1: function (k) {
      if (typeof k === 'object' && k !== null) {
        k = k.k !== undefined ? k.k : 0.01;
      }
      return Math.log(2) / k;
    },

    /**
     * Bậc phản ứng: Bậc 2
     */
    nongDoBac2: function (C0, k, t) {
      if (typeof C0 === 'object' && C0 !== null) {
        var p = C0;
        C0 = p.C0 !== undefined ? p.C0 : 1.0;
        k = p.k !== undefined ? p.k : 0.01;
        t = p.t !== undefined ? p.t : 0;
      }
      return 1.0 / (1.0 / C0 + k * t);
    },
    thoiGianBanHuyBac2: function (C0, k) {
      if (typeof C0 === 'object' && C0 !== null) {
        var p = C0;
        C0 = p.C0 !== undefined ? p.C0 : 1.0;
        k = p.k !== undefined ? p.k : 0.01;
      }
      return 1.0 / (k * C0);
    },

    /**
     * Tỉ lệ hạt có năng lượng E >= Ea theo phân bố Maxwell-Boltzmann 3D
     */
    phanBoNangLuong3D: function (Ea_kJ, T_K) {
      Ea_kJ = Ea_kJ !== undefined ? Ea_kJ : 50.0;
      T_K = T_K !== undefined ? T_K : 298.15;
      var x = (Ea_kJ * 1000) / (R_SI * T_K);
      var exp_x = Math.exp(-x);
      var sqrt_x = Math.sqrt(x);
      var tiLe = erfc(sqrt_x) + 2 * Math.sqrt(x / Math.PI) * exp_x;
      return {
        x: x,
        exp_am_x: exp_x,
        tiLe3D: tiLe
      };
    },

    // Giữ tương thích với các tên hàm cũ
    tocDoTrungBinh: function (p) {
      var heSo = p && p.heSo !== undefined ? p.heSo : 1;
      return p.deltaC / (heSo * p.deltaT);
    },
    tocDoTucThoi: function (p) {
      var a = p && p.a !== undefined ? p.a : 1;
      var b = p && p.b !== undefined ? p.b : 1;
      return p.k * Math.pow(p.cA, a) * Math.pow(p.cB, b);
    },
    tiLeTocDoVanTHoff: function (p) {
      var gamma = p && p.gamma !== undefined ? p.gamma : 2;
      return Math.pow(gamma, (p.T2 - p.T1) / 10);
    }
  };

  if (typeof module !== 'undefined' && module.exports) {
    module.exports = HL_TocDo;
  }
  global.HL_TocDo = HL_TocDo;
})(typeof window !== 'undefined' ? window : global);
