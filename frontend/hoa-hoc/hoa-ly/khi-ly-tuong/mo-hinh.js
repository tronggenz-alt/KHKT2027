/**
 * mo-hinh.js - Mô hình vật lý thuần cho khí lý tưởng và khí thực Van der Waals.
 * Chạy độc lập trên cả Node.js và trình duyệt.
 */

(function (global) {
  'use strict';

  var R_SI = 8.314462618; // J/(mol·K)
  var R_BAR = 0.08314462618; // bar·L/(mol·K)
  var K_B = 1.380649e-23; // J/K
  var N_A = 6.02214076e23; // 1/mol

  var HL_KhiLyTuong = {
    R_SI: R_SI,
    R_BAR: R_BAR,
    K_B: K_B,
    N_A: N_A,

    apSuat: function (p) {
      if (typeof p === 'number') {
        // apSuat(n, V_L, T_K)
        var n_val = arguments[0];
        var V_val = arguments[1] !== undefined ? arguments[1] : 24.79;
        var T_val = arguments[2] !== undefined ? arguments[2] : 298.15;
        return (n_val * R_BAR * T_val) / V_val;
      }
      p = p || {};
      var n = p.n !== undefined ? p.n : 1;
      var T = p.T !== undefined ? p.T : (p.T_K !== undefined ? p.T_K : 298.15);
      var V_L = p.V_L !== undefined ? p.V_L : (p.V !== undefined ? p.V : 24.79);
      return (n * R_BAR * T) / V_L;
    },

    theTich: function (p) {
      if (typeof p === 'number') {
        var n_val = arguments[0];
        var p_val = arguments[1] !== undefined ? arguments[1] : 1;
        var T_val = arguments[2] !== undefined ? arguments[2] : 298.15;
        return (n_val * R_BAR * T_val) / p_val;
      }
      p = p || {};
      var n = p.n !== undefined ? p.n : 1;
      var T = p.T !== undefined ? p.T : (p.T_K !== undefined ? p.T_K : 298.15);
      var p_bar = p.p_bar !== undefined ? p.p_bar : (p.p !== undefined ? p.p : 1);
      return (n * R_BAR * T) / p_bar;
    },

    vRms: function (M_g_mol, T_K) {
      if (typeof M_g_mol === 'object' && M_g_mol !== null) {
        var p = M_g_mol;
        var T = p.T !== undefined ? p.T : (p.T_K !== undefined ? p.T_K : 298.15);
        var M_kg = (p.M_g_mol || p.M || 28.013) * 1e-3;
        return Math.sqrt((3 * R_SI * T) / M_kg);
      }
      var M_kg_val = (M_g_mol || 28.013) * 1e-3;
      var T_val = T_K !== undefined ? T_K : 298.15;
      return Math.sqrt((3 * R_SI * T_val) / M_kg_val);
    },

    vP: function (M_g_mol, T_K) {
      if (typeof M_g_mol === 'object' && M_g_mol !== null) {
        var p = M_g_mol;
        var T = p.T !== undefined ? p.T : (p.T_K !== undefined ? p.T_K : 298.15);
        var M_kg = (p.M_g_mol || p.M || 28.013) * 1e-3;
        return Math.sqrt((2 * R_SI * T) / M_kg);
      }
      var M_kg_val = (M_g_mol || 28.013) * 1e-3;
      var T_val = T_K !== undefined ? T_K : 298.15;
      return Math.sqrt((2 * R_SI * T_val) / M_kg_val);
    },

    vTb: function (M_g_mol, T_K) {
      if (typeof M_g_mol === 'object' && M_g_mol !== null) {
        var p = M_g_mol;
        var T = p.T !== undefined ? p.T : (p.T_K !== undefined ? p.T_K : 298.15);
        var M_kg = (p.M_g_mol || p.M || 28.013) * 1e-3;
        return Math.sqrt((8 * R_SI * T) / (Math.PI * M_kg));
      }
      var M_kg_val = (M_g_mol || 28.013) * 1e-3;
      var T_val = T_K !== undefined ? T_K : 298.15;
      return Math.sqrt((8 * R_SI * T_val) / (Math.PI * M_kg_val));
    },

    dongNangTb: function (T_K) {
      if (typeof T_K === 'object' && T_K !== null) {
        T_K = T_K.T !== undefined ? T_K.T : (T_K.T_K !== undefined ? T_K.T_K : 298.15);
      }
      var T = T_K !== undefined ? T_K : 298.15;
      return 1.5 * K_B * T;
    },

    noiNang: function (n, T_K, gamma) {
      if (typeof n === 'object' && n !== null) {
        var p = n;
        n = p.n !== undefined ? p.n : 1;
        T_K = p.T !== undefined ? p.T : (p.T_K !== undefined ? p.T_K : 298.15);
        gamma = p.gamma !== undefined ? p.gamma : 1.4;
      }
      n = n !== undefined ? n : 1;
      T_K = T_K !== undefined ? T_K : 298.15;
      gamma = gamma !== undefined ? gamma : 1.4;
      var Cv = R_SI / (gamma - 1);
      return n * Cv * T_K;
    },

    vanDerWaals: function (n, V_L, T_K, a, b) {
      if (typeof n === 'object' && n !== null) {
        var p = n;
        n = p.n !== undefined ? p.n : 1;
        V_L = p.V_L !== undefined ? p.V_L : (p.V !== undefined ? p.V : 1.0);
        T_K = p.T !== undefined ? p.T : (p.T_K !== undefined ? p.T_K : 300);
        a = p.a !== undefined ? p.a : 3.640;
        b = p.b !== undefined ? p.b : 0.04267;
      }
      n = n !== undefined ? n : 1;
      V_L = V_L !== undefined ? V_L : 1.0;
      T_K = T_K !== undefined ? T_K : 300;
      a = a !== undefined ? a : 3.640;
      b = b !== undefined ? b : 0.04267;

      var p_ly_tuong = (n * R_BAR * T_K) / V_L;
      var V_eff = V_L - n * b;
      var p_vdw = (V_eff > 0) ? ((n * R_BAR * T_K) / V_eff - a * Math.pow(n / V_L, 2)) : Infinity;
      var Z = (p_vdw * V_L) / (n * R_BAR * T_K);
      return {
        p_ly_tuong: p_ly_tuong,
        p_vdw: p_vdw,
        Z: Z
      };
    },

    quaTrinh: function (tuTrangThai, denGiaTri, giu) {
      var n = tuTrangThai.n !== undefined ? tuTrangThai.n : 1;
      var T1 = tuTrangThai.T !== undefined ? tuTrangThai.T : (tuTrangThai.T_K || 298.15);
      var V1 = tuTrangThai.V !== undefined ? tuTrangThai.V : (tuTrangThai.V_L || 24.79);
      var p1 = tuTrangThai.p !== undefined ? tuTrangThai.p : (tuTrangThai.p_bar || (n * R_BAR * T1) / V1);
      var gamma = tuTrangThai.gamma !== undefined ? tuTrangThai.gamma : 1.4;

      var Cv = R_SI / (gamma - 1);
      var Cp = Cv + R_SI;

      var T2 = T1;
      var V2 = V1;
      var p2 = p1;
      var Q = 0;
      var A = 0;
      var dU = 0;

      if (giu === 'T' || giu === 'dang-nhiet') {
        T2 = T1;
        if (denGiaTri.V !== undefined || denGiaTri.V_L !== undefined) {
          V2 = denGiaTri.V !== undefined ? denGiaTri.V : denGiaTri.V_L;
          p2 = (n * R_BAR * T2) / V2;
        } else if (denGiaTri.p !== undefined || denGiaTri.p_bar !== undefined) {
          p2 = denGiaTri.p !== undefined ? denGiaTri.p : denGiaTri.p_bar;
          V2 = (n * R_BAR * T2) / p2;
        }
        var A_sinh = n * R_SI * T1 * Math.log(V2 / V1);
        A = -A_sinh;
        dU = 0;
        Q = A_sinh;
      } else if (giu === 'p' || giu === 'dang-ap') {
        p2 = p1;
        if (denGiaTri.T !== undefined || denGiaTri.T_K !== undefined) {
          T2 = denGiaTri.T !== undefined ? denGiaTri.T : denGiaTri.T_K;
          V2 = (n * R_BAR * T2) / p2;
        } else if (denGiaTri.V !== undefined || denGiaTri.V_L !== undefined) {
          V2 = denGiaTri.V !== undefined ? denGiaTri.V : denGiaTri.V_L;
          T2 = (p2 * V2) / (n * R_BAR);
        }
        var A_sinh_p = p1 * 100 * (V2 - V1); // 1 bar = 10^5 Pa, 1 L = 10^-3 m^3 -> p_bar * V_L * 100 J
        A = -A_sinh_p;
        dU = n * Cv * (T2 - T1);
        Q = dU - A; // = n * Cp * (T2 - T1)
      } else if (giu === 'V' || giu === 'dang-tich') {
        V2 = V1;
        if (denGiaTri.T !== undefined || denGiaTri.T_K !== undefined) {
          T2 = denGiaTri.T !== undefined ? denGiaTri.T : denGiaTri.T_K;
          p2 = (n * R_BAR * T2) / V2;
        } else if (denGiaTri.p !== undefined || denGiaTri.p_bar !== undefined) {
          p2 = denGiaTri.p !== undefined ? denGiaTri.p : denGiaTri.p_bar;
          T2 = (p2 * V2) / (n * R_BAR);
        }
        A = 0;
        dU = n * Cv * (T2 - T1);
        Q = dU;
      } else if (giu === 'doan-nhiet') {
        Q = 0;
        if (denGiaTri.V !== undefined || denGiaTri.V_L !== undefined) {
          V2 = denGiaTri.V !== undefined ? denGiaTri.V : denGiaTri.V_L;
          T2 = T1 * Math.pow(V1 / V2, gamma - 1);
          p2 = p1 * Math.pow(V1 / V2, gamma);
        } else if (denGiaTri.T !== undefined || denGiaTri.T_K !== undefined) {
          T2 = denGiaTri.T !== undefined ? denGiaTri.T : denGiaTri.T_K;
          V2 = V1 * Math.pow(T1 / T2, 1 / (gamma - 1));
          p2 = (n * R_BAR * T2) / V2;
        }
        dU = n * Cv * (T2 - T1);
        A = dU;
      } else {
        // Tu do
        if (denGiaTri.V !== undefined) V2 = denGiaTri.V;
        if (denGiaTri.T !== undefined) T2 = denGiaTri.T;
        if (denGiaTri.p !== undefined) {
          p2 = denGiaTri.p;
        } else {
          p2 = (n * R_BAR * T2) / V2;
        }
        dU = n * Cv * (T2 - T1);
        var p_tb = (p1 + p2) / 2;
        var A_sinh_td = p_tb * 100 * (V2 - V1);
        A = -A_sinh_td;
        Q = dU - A;
      }

      return {
        n: n,
        p1: p1,
        V1: V1,
        T1: T1,
        p2: p2,
        V2: V2,
        T2: T2,
        Q: Q,
        A: A,
        A_sinh: -A,
        dU: dU
      };
    },

    phanBoMaxwell: function (v, M_kg, T_K) {
      M_kg = M_kg || 0.028013;
      T_K = T_K || 298.15;
      var a = M_kg / (2 * R_SI * T_K);
      return 4 * Math.PI * Math.pow(a / Math.PI, 1.5) * v * v * Math.exp(-a * v * v);
    },

    hopKhi: function (danhSach, V_L, T_K) {
      danhSach = danhSach || [];
      V_L = V_L || 24.79;
      T_K = T_K || 298.15;
      var n_tong = 0;
      for (var i = 0; i < danhSach.length; i++) {
        n_tong += (danhSach[i].n || 0);
      }
      var p_tong = (n_tong * R_BAR * T_K) / V_L;
      var ketQua = [];
      for (var j = 0; j < danhSach.length; j++) {
        var n_i = danhSach[j].n || 0;
        var x_i = n_tong > 0 ? n_i / n_tong : 0;
        ketQua.push({
          id: danhSach[j].id,
          ten: danhSach[j].ten,
          ct: danhSach[j].ct,
          n: n_i,
          x_i: x_i,
          p_i: x_i * p_tong
        });
      }
      return {
        n_tong: n_tong,
        p_tong: p_tong,
        cac_khi: ketQua
      };
    },

    doKhongTuyetDoi: function (cacDiem) {
      // cacDiem: [{ t_C, V_L }]
      if (!cacDiem || cacDiem.length < 2) return null;
      var N = cacDiem.length;
      var sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
      for (var i = 0; i < N; i++) {
        var x = cacDiem[i].t_C;
        var y = cacDiem[i].V_L;
        sumX += x;
        sumY += y;
        sumXY += x * y;
        sumX2 += x * x;
      }
      var denom = N * sumX2 - sumX * sumX;
      if (Math.abs(denom) < 1e-12) return null;
      var m = (N * sumXY - sumX * sumY) / denom;
      var c = (sumY * sumX2 - sumX * sumXY) / denom;
      var t0 = m !== 0 ? -c / m : -273.15;
      return {
        he_so_goc: m,
        he_so_tu_do: c,
        do_khong_tuyet_doi: t0
      };
    },

    // Giữ tương thích với các hàm hỗ trợ cũ nếu có
    dongNangTrungBinh: function (p) {
      var T = p && p.T !== undefined ? p.T : 298.15;
      return 1.5 * K_B * T;
    },
    vanTocRms: function (p) {
      var T = p && p.T !== undefined ? p.T : 298.15;
      var M_kg = ((p && (p.M_g_mol || p.M)) || 28.013) * 1e-3;
      return Math.sqrt((3 * R_SI * T) / M_kg);
    },
    theTichCharles: function (p) {
      return p.V1 * (p.T2 / p.T1);
    },
    congDangNhiet: function (p) {
      var n = p && p.n !== undefined ? p.n : 1;
      var T = p && p.T !== undefined ? p.T : 298.15;
      return n * R_SI * T * Math.log(p.V2 / p.V1);
    },
    apSuatVanDerWaals: function (p) {
      var n = p && p.n !== undefined ? p.n : 1;
      var T = p && p.T !== undefined ? p.T : 300;
      var V = p && p.V_L !== undefined ? p.V_L : 0.5;
      var a = p && p.a !== undefined ? p.a : 3.640;
      var b = p && p.b !== undefined ? p.b : 0.04267;
      var V_eff = V - n * b;
      if (V_eff <= 0) return Infinity;
      return (n * R_BAR * T) / V_eff - a * Math.pow(n / V, 2);
    },
    apSuatViMo: function (p) {
      var V_m3 = (p.V_L || 1.0) * 1e-3;
      var p_Pa = (1 / 3) * (p.N / V_m3) * p.m_kg * (p.v_rms * p.v_rms);
      return p_Pa * 1e-5;
    },
    apSuatRiengPhan: function (p) {
      return (p.p_tong || 1.0) * (p.x_i || 1.0);
    },
    doanNhietT2: function (T1, V1, V2, gamma) {
      return T1 * Math.pow(V1 / V2, gamma - 1);
    },
    doanNhietP2: function (p1, V1, V2, gamma) {
      return p1 * Math.pow(V1 / V2, gamma);
    }
  };

  if (typeof module !== 'undefined' && module.exports) {
    module.exports = HL_KhiLyTuong;
  }
  global.HL_KhiLyTuong = HL_KhiLyTuong;
})(typeof window !== 'undefined' ? window : global);
