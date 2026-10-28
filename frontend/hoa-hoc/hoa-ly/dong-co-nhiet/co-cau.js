/**
 * co-cau.js - Động học & hình học cơ cấu tay quay – thanh truyền
 * Chuẩn UMD: chạy trên trình duyệt (window.HLCoCau) và Node.js (module.exports).
 * Không chạm DOM.
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.HLCoCau = factory();
  }
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';

  var DEG2RAD = Math.PI / 180;
  var RAD2DEG = 180 / Math.PI;

  /**
   * Tính các đại lượng hình học xi-lanh từ thông số kết cấu
   * @param {Object} ts - { B (mm), S (mm), L (mm), r }
   */
  function hinhHoc(ts) {
    var B = ts.B || 80; // mm
    var S = ts.S || 80; // mm
    var L = ts.L || 140; // mm
    var r = ts.r || 10;

    var a = S / 2; // bán kính tay quay (mm)
    var A_mm2 = (Math.PI * B * B) / 4; // diện tích mặt pít-tông (mm²)
    var Vd_mm3 = A_mm2 * S; // thể tích công tác (mm³)
    var Vc_mm3 = Vd_mm3 / (r - 1); // thể tích buồng cháy (mm³)
    var V1_mm3 = Vc_mm3 + Vd_mm3; // thể tích toàn phần (mm³)

    return {
      B: B,
      S: S,
      L: L,
      r: r,
      a: a,
      A_mm2: A_mm2,
      A_cm2: A_mm2 / 100,
      Vd_cm3: Vd_mm3 / 1000,
      Vc_cm3: Vc_mm3 / 1000,
      V1_cm3: V1_mm3 / 1000,
    };
  }

  /**
   * Quãng dịch chuyển của pít-tông tính từ ĐCT: s(θ) theo mm
   * θ tính bằng độ hoặc radian từ ĐCT
   */
  function s(phiDeg, ts) {
    var hh = hinhHoc(ts);
    var theta = (phiDeg % 360 + 360) % 360;
    var rad = theta * DEG2RAD;
    var a = hh.a;
    var L = hh.L;

    var sinTheta = Math.sin(rad);
    var cosTheta = Math.cos(rad);
    var can = Math.sqrt(Math.max(0, L * L - a * a * sinTheta * sinTheta));

    return a * (1 - cosTheta) + L - can;
  }

  /**
   * Thể tích xi-lanh V(θ) theo cm³
   */
  function V(phiDeg, ts) {
    var hh = hinhHoc(ts);
    var s_mm = s(phiDeg, ts);
    var V_mm3 = (hh.Vc_cm3 * 1000) + (hh.A_mm2 * s_mm);
    return V_mm3 / 1000;
  }

  /**
   * Nghịch đảo: Cho trước s (mm) từ ĐCT, tìm góc θ ∈ [0°, 180°]
   */
  function thetaTuS(s_mm, ts) {
    var hh = hinhHoc(ts);
    var a = hh.a;
    var L = hh.L;
    var S = hh.S;

    var s_clamped = Math.max(0, Math.min(S, s_mm));
    var d = a + L - s_clamped;

    var cosTheta = (d * d + a * a - L * L) / (2 * a * d);
    cosTheta = Math.max(-1, Math.min(1, cosTheta));

    return Math.acos(cosTheta) * RAD2DEG;
  }

  /**
   * Nghịch đảo: Cho trước thể tích V (cm³), tìm góc θ ∈ [0°, 180°]
   */
  function thetaTuV(V_cm3, ts) {
    var hh = hinhHoc(ts);
    var V_clamped = Math.max(hh.Vc_cm3, Math.min(hh.V1_cm3, V_cm3));
    var s_mm = ((V_clamped - hh.Vc_cm3) * 1000) / hh.A_mm2;
    return thetaTuS(s_mm, ts);
  }

  /**
   * Đạo hàm ds/dθ (mm/rad)
   */
  function ds_dtheta(phiDeg, ts) {
    var hh = hinhHoc(ts);
    var theta = (phiDeg % 360 + 360) % 360;
    var rad = theta * DEG2RAD;
    var a = hh.a;
    var L = hh.L;

    var sinTheta = Math.sin(rad);
    var cosTheta = Math.cos(rad);
    var can = Math.sqrt(Math.max(1e-9, L * L - a * a * sinTheta * sinTheta));

    return a * sinTheta + (a * a * sinTheta * cosTheta) / can;
  }

  /**
   * Đạo hàm dV/dθ (cm³/rad)
   */
  function dV_dtheta(phiDeg, ts) {
    var hh = hinhHoc(ts);
    var ds = ds_dtheta(phiDeg, ts); // mm/rad
    return (hh.A_mm2 * ds) / 1000; // cm³/rad
  }

  /**
   * Định dạng số có dấu phẩy thập phân
   */
  function dinhDang(so, chuSoLe) {
    if (so === null || so === undefined || isNaN(so)) return '--';
    var n = typeof chuSoLe === 'number' ? so.toFixed(chuSoLe) : so.toString();
    return n.replace('.', ',');
  }

  return {
    hinhHoc: hinhHoc,
    s: s,
    V: V,
    thetaTuS: thetaTuS,
    thetaTuV: thetaTuV,
    ds_dtheta: ds_dtheta,
    dV_dtheta: dV_dtheta,
    dinhDang: dinhDang,
  };
});
