/**
 * nhiet-dong.js - Tính toán nhiệt động lực học cho chu trình Otto, Diesel,
 * định luật I nhiệt động lực học, phản ứng cháy nhiên liệu và hàm cháy Wiebe.
 * Chuẩn UMD: chạy trên trình duyệt (window.HLNhietDong) và Node.js (module.exports).
 * Không chạm DOM.
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) {
    var coCau = null;
    try {
      coCau = require('./co-cau.js');
    } catch (e) {}
    module.exports = factory(coCau);
  } else {
    root.HLNhietDong = factory(root.HLCoCau);
  }
})(typeof globalThis !== 'undefined' ? globalThis : this, function (HLCoCau) {
  'use strict';

  var R = 0.287; // Hằng số khí riêng của không khí: kJ/(kg·K)

  /**
   * Tính toán chu trình Otto lý tưởng
   * @param {Object} ts - { r, gamma, T1 (K), p1 (kPa), q_in (kJ/kg), B (mm), S (mm), L (mm), n (rpm) }
   */
  function otto(ts) {
    ts = ts || {};
    var r = ts.r !== undefined ? ts.r : 10;
    var gamma = ts.gamma !== undefined ? ts.gamma : 1.4;
    var T1 = ts.T1 !== undefined ? ts.T1 : 300; // K
    var p1 = ts.p1 !== undefined ? ts.p1 : 100; // kPa
    var q_in = ts.q_in !== undefined ? ts.q_in : 1500; // kJ/kg
    var n = ts.n !== undefined ? ts.n : 1200; // rpm

    var cv = R / (gamma - 1);
    var cp = gamma * cv;

    // Trạng thái 2: sau nén đoạn nhiệt 1->2
    var T2 = T1 * Math.pow(r, gamma - 1);
    var p2 = p1 * Math.pow(r, gamma);

    // Trạng thái 3: sau cấp nhiệt đẳng tích 2->3
    var T3 = T2 + q_in / cv;
    var p3 = p2 * (T3 / T2);

    // Trạng thái 4: sau giãn nở đoạn nhiệt 3->4
    var T4 = T3 * Math.pow(r, 1 - gamma);
    var p4 = p1 * (T4 / T1);

    // Năng lượng chu trình
    var q_out = cv * (T4 - T1);
    var w = q_in - q_out;
    var eta = 1 - Math.pow(r, 1 - gamma);
    var eta_Carnot = 1 - T1 / T3;

    // Thể tích riêng và MEP (kPa)
    var v1 = (R * T1) / p1; // m³/kg
    var v2 = v1 / r;
    var MEP = w / (v1 - v2); // kPa

    // Biến thiên entropy s3 - s2 (kJ/(kg·K))
    var delta_s_chay = cv * Math.log(T3 / T2);

    // Khối lượng khí và công suất (nếu có hình học)
    var B = ts.B || 80;
    var S = ts.S || 80;
    var A_mm2 = (Math.PI * B * B) / 4;
    var Vd_m3 = (A_mm2 * S) * 1e-9;
    var V1_m3 = Vd_m3 * r / (r - 1);
    var m_kg = (p1 * V1_m3) / (R * T1); // kg khí mỗi chu trình
    var W_J = m_kg * w * 1000; // J mỗi chu trình
    var P_kW = (W_J * (n / 2) / 60) / 1000; // Công suất 1 xi-lanh máy 4 kỳ (kW)

    return {
      kieu: 'otto',
      r: r,
      gamma: gamma,
      cv: cv,
      cp: cp,
      R: R,
      T1: T1,
      p1: p1,
      T2: T2,
      p2: p2,
      T3: T3,
      p3: p3,
      T4: T4,
      p4: p4,
      q_in: q_in,
      q_out: q_out,
      w: w,
      eta: eta,
      eta_phan_tram: eta * 100,
      eta_Carnot: eta_Carnot,
      MEP: MEP,
      delta_s_chay: delta_s_chay,
      v1: v1,
      v2: v2,
      m_kg: m_kg,
      m_g: m_kg * 1000,
      W_J: W_J,
      P_kW: P_kW,
    };
  }

  /**
   * Tính toán chu trình Diesel lý tưởng
   * @param {Object} ts - { r, rc (tỉ số ngắt), gamma, T1, p1, ... }
   */
  function diesel(ts) {
    ts = ts || {};
    var r = ts.r !== undefined ? ts.r : 18;
    var rc = ts.rc !== undefined ? ts.rc : 2.0;
    var gamma = ts.gamma !== undefined ? ts.gamma : 1.4;
    var T1 = ts.T1 !== undefined ? ts.T1 : 300;
    var p1 = ts.p1 !== undefined ? ts.p1 : 100;
    var n = ts.n !== undefined ? ts.n : 1200;

    var cv = R / (gamma - 1);
    var cp = gamma * cv;

    // Trạng thái 2: sau nén đoạn nhiệt 1->2
    var T2 = T1 * Math.pow(r, gamma - 1);
    var p2 = p1 * Math.pow(r, gamma);

    // Trạng thái 3: sau cấp nhiệt đẳng áp 2->3 (p3 = p2, V3 = rc * V2)
    var p3 = p2;
    var T3 = T2 * rc;
    var q_in = cp * (T3 - T2);

    // Trạng thái 4: sau giãn nở đoạn nhiệt 3->4 về V1 (tỉ số giãn nở re = r / rc)
    var T4 = T3 * Math.pow(rc / r, gamma - 1);
    var p4 = p3 * Math.pow(rc / r, gamma);

    // Năng lượng chu trình
    var q_out = cv * (T4 - T1);
    var w = q_in - q_out;

    // Hiệu suất Diesel
    var tuSo = Math.pow(rc, gamma) - 1;
    var mauSo = gamma * (rc - 1);
    var eta = 1 - Math.pow(r, 1 - gamma) * (tuSo / mauSo);
    var eta_Carnot = 1 - T1 / T3;

    var v1 = (R * T1) / p1;
    var v2 = v1 / r;
    var MEP = w / (v1 - v2);

    var B = ts.B || 80;
    var S = ts.S || 80;
    var A_mm2 = (Math.PI * B * B) / 4;
    var Vd_m3 = (A_mm2 * S) * 1e-9;
    var V1_m3 = Vd_m3 * r / (r - 1);
    var m_kg = (p1 * V1_m3) / (R * T1);
    var W_J = m_kg * w * 1000;
    var P_kW = (W_J * (n / 2) / 60) / 1000;

    return {
      kieu: 'diesel',
      r: r,
      rc: rc,
      gamma: gamma,
      cv: cv,
      cp: cp,
      R: R,
      T1: T1,
      p1: p1,
      T2: T2,
      p2: p2,
      T3: T3,
      p3: p3,
      T4: T4,
      p4: p4,
      q_in: q_in,
      q_out: q_out,
      w: w,
      eta: eta,
      eta_phan_tram: eta * 100,
      eta_Carnot: eta_Carnot,
      MEP: MEP,
      v1: v1,
      v2: v2,
      m_kg: m_kg,
      m_g: m_kg * 1000,
      W_J: W_J,
      P_kW: P_kW,
    };
  }

  /**
   * Tính toán phản ứng cháy và năng lượng nhiên liệu (cầu nối Hoá 10)
   * Phản ứng: CxHyOz + nO2 (O2 + 3.76 N2) -> nCO2 CO2 + nH2O H2O(g) + nN2 N2
   */
  function tinhNhienLieu(nl, tuyChon) {
    tuyChon = tuyChon || {};
    var lambda = tuyChon.lambda !== undefined ? tuyChon.lambda : 1.0;
    var k = tuyChon.k !== undefined ? tuyChon.k : 0.55;

    var C = nl.C || 0;
    var H = nl.H || 0;
    var O = nl.O || 0;
    var M = nl.M || 1;
    var delta_f_H = nl.delta_f_H || 0; // kJ/mol

    var delta_f_CO2 = -393.5; // kJ/mol
    var delta_f_H2O_g = -241.8; // kJ/mol (hơi)
    var delta_f_H2O_l = -285.8; // kJ/mol (lỏng)

    var nCO2 = C;
    var nH2O = H / 2;
    var nO2 = C + H / 4 - O / 2;
    var nN2 = 3.76 * nO2;

    // Biến thiên enthalpy chuẩn phản ứng cháy (LHV: nước thể hơi)
    var delta_cH_LHV = nCO2 * delta_f_CO2 + nH2O * delta_f_H2O_g - delta_f_H;
    // HHV: nước ngưng tụ thành lỏng
    var delta_cH_HHV = nCO2 * delta_f_CO2 + nH2O * delta_f_H2O_l - delta_f_H;

    // Nhiệt trị LHV theo MJ/kg
    var LHV_MJ_kg = Math.abs(delta_cH_LHV) / M;
    var HHV_MJ_kg = Math.abs(delta_cH_HHV) / M;

    // Tỉ lệ không khí / nhiên liệu theo khối lượng AFR lý thuyết
    var AFR = (nO2 * 32.0 + nN2 * 28.0) / M;

    // Nhiệt cấp q_in cho 1 kg môi chất (kJ/kg)
    var q_in = (k * (LHV_MJ_kg * 1000) * Math.min(1, lambda)) / (lambda * AFR + 1);

    // Chuỗi phương trình cân bằng
    var ptText = '';
    if (C > 0 || H > 0) {
      ptText = nl.cong_thuc + ' + ' + (nO2 % 1 === 0 ? nO2 : nO2.toFixed(2)) + ' (O₂ + 3,76 N₂) → ' +
        (nCO2 > 0 ? (nCO2 + ' CO₂ + ') : '') +
        (nH2O > 0 ? (nH2O + ' H₂O + ') : '') +
        (nN2 % 1 === 0 ? nN2 : nN2.toFixed(2)) + ' N₂';
    }

    return {
      id: nl.id,
      ten: nl.ten,
      cong_thuc: nl.cong_thuc,
      M: M,
      delta_f_H: delta_f_H,
      delta_cH_LHV: delta_cH_LHV,
      delta_cH_HHV: delta_cH_HHV,
      LHV_MJ_kg: LHV_MJ_kg,
      HHV_MJ_kg: HHV_MJ_kg,
      AFR: AFR,
      lambda: lambda,
      k: k,
      q_in: q_in,
      phuong_trinh: ptText,
    };
  }

  /**
   * Tính toán chu trình cháy hữu hạn Wiebe (P3)
   * Trả về bảng 1441 điểm { phi, p, V, T } từ 0° đến 720°
   */
  function tinhWiebe(ts, coCauLib) {
    var CC = coCauLib || HLCoCau;
    if (!CC) {
      throw new Error('Cần module HLCoCau để tính Wiebe');
    }
    ts = ts || {};
    var r = ts.r !== undefined ? ts.r : 10;
    var gamma = ts.gamma !== undefined ? ts.gamma : 1.4;
    var T1 = ts.T1 !== undefined ? ts.T1 : 300;
    var p1 = ts.p1 !== undefined ? ts.p1 : 100; // kPa
    var q_in = ts.q_in !== undefined ? ts.q_in : 1500; // kJ/kg

    var a = ts.wiebe_a !== undefined ? ts.wiebe_a : 5;
    var m = ts.wiebe_m !== undefined ? ts.wiebe_m : 2;
    var phi0 = ts.wiebe_phi0 !== undefined ? ts.wiebe_phi0 : 350; // góc bắt đầu cháy
    var delta_phi = ts.wiebe_delta_phi !== undefined ? ts.wiebe_delta_phi : 60; // thời gian cháy
    var phi_xa_mo = ts.wiebe_phi_xa !== undefined ? ts.wiebe_phi_xa : 520; // van xả mở sớm

    var cv = R / (gamma - 1);
    var hh = CC.hinhHoc(ts);
    var Vc_m3 = hh.Vc_cm3 * 1e-6;
    var V1_m3 = hh.V1_cm3 * 1e-6;
    var m_kg = (p1 * V1_m3) / (R * T1);
    var Q_tong = m_kg * q_in; // kJ

    var dphi = 0.5; // bước góc (độ)
    var soDiem = Math.round(720 / dphi) + 1; // 1441 điểm
    var ketQua = new Array(soDiem);

    // Trạng thái ban đầu tại phi = 0
    var p_hien_tai = p1; // kPa
    var DEG2RAD = Math.PI / 180;

    var tongCong = 0; // kJ

    for (var i = 0; i < soDiem; i++) {
      var phi = i * dphi;
      var V_cm3 = CC.V(phi, ts);
      var V_m3 = V_cm3 * 1e-6;

      if (i > 0) {
        var phi_truoc = (i - 1) * dphi;
        // Substep integration between phi_truoc and phi
        var n_sub = 10;
        var sub_step = dphi / n_sub;
        for (var s = 0; s < n_sub; s++) {
          var cur_phi = phi_truoc + (s + 0.5) * sub_step;
          var cur_V_m3 = CC.V(cur_phi, ts) * 1e-6;
          var cur_dV = CC.dV_dtheta(cur_phi, ts) * 1e-6 * DEG2RAD; // m³/deg

          var cur_dQ = 0;
          if (cur_phi >= phi0 && cur_phi <= phi0 + delta_phi) {
            var xi = (cur_phi - phi0) / delta_phi;
            var expTerm = Math.exp(-a * Math.pow(xi, m + 1));
            var dx_dphi = (a * (m + 1) / delta_phi) * Math.pow(xi, m) * expTerm;
            cur_dQ = Q_tong * dx_dphi;
          }

          if (cur_phi < 180) {
            p_hien_tai = p1;
          } else if (cur_phi >= phi_xa_mo) {
            var f_giam = Math.min(1, (cur_phi - phi_xa_mo) / Math.max(1, 560 - phi_xa_mo));
            p_hien_tai = p_hien_tai + (p1 - p_hien_tai) * (sub_step / Math.max(1, 560 - phi_xa_mo));
            if (cur_phi >= 560) p_hien_tai = p1;
          } else {
            var dp_dphi = -gamma * (p_hien_tai / cur_V_m3) * cur_dV + ((gamma - 1) / cur_V_m3) * cur_dQ;
            p_hien_tai += dp_dphi * sub_step;
          }
        }
      }

      // Đảm bảo không âm
      p_hien_tai = Math.max(p1 * 0.1, p_hien_tai);

      var T_K = (p_hien_tai * V_m3) / (m_kg * R);

      ketQua[i] = {
        phi: phi,
        p_kPa: p_hien_tai,
        p_bar: p_hien_tai / 100,
        V_cm3: V_cm3,
        V_m3: V_m3,
        T_K: T_K,
      };

      if (i > 0) {
        var dV = (ketQua[i].V_m3 - ketQua[i - 1].V_m3);
        var p_tb = (ketQua[i].p_kPa + ketQua[i - 1].p_kPa) / 2;
        tongCong += p_tb * dV; // kJ
      }
    }

    var w_wiebe = tongCong / m_kg; // kJ/kg
    var eta_wiebe = w_wiebe / q_in;

    return {
      diem: ketQua,
      w: w_wiebe,
      eta: eta_wiebe,
      tongCong_J: tongCong * 1000,
    };
  }

  // Các hàm hỗ trợ tính ví dụ số cho thư viện công thức hoá lý
  var HL_DongCoNhiet = {
    R: R,
    otto: otto,
    diesel: diesel,
    tinhNhienLieu: tinhNhienLieu,
    tinhWiebe: tinhWiebe,
    dinhLuat1: function (p) { return (p.A || 0) + (p.Q || 0); },
    congDangAp: function (p) { return (p.p_bar * 1e5) * (p.deltaV_L * 1e-3); },
    noiNang: function (p) { return ((p.f || 5) / 2) * (p.n || 1) * 8.31446 * (p.T || 300); },
    hieuNhietDungMayer: function () { return 8.314; },
    apSuatDoanNhiet: function (p) { return p.p1 * Math.pow(p.V1 / p.V2, p.gamma); },
    hieuSuatNhiet: function (p) { return (p.Q1 - p.Q2) / p.Q1; },
    hieuSuatOtto: function (p) { return 1 - 1 / Math.pow(p.r, p.gamma - 1); },
    hieuSuatCarnot: function (p) { return 1 - p.T_lanh / p.T_nong; },
    deltaH_isooctane: function () { return -5065.0; },
    deltaH_Eb_H2_Cl2: function (p) { return (p.Eb_H2 + p.Eb_Cl2) - 2 * p.Eb_HCl; },
    lhv_isooctane: function () { return 44.43; },
    kirchhoff: function (p) { return (p.dH1 || 0) + ((p.dCp || 0) * (p.dT || 0)); }
  };

  if (typeof global !== 'undefined') {
    global.HL_DongCoNhiet = HL_DongCoNhiet;
  }
  if (typeof window !== 'undefined') {
    window.HL_DongCoNhiet = HL_DongCoNhiet;
  }

  return HL_DongCoNhiet;
});

