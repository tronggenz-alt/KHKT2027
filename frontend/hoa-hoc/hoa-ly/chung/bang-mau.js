/**
 * bang-mau.js - Quản lý màu sắc, nội suy nhiệt độ và trộn màu theo nồng độ
 * Đảm bảo hỗ trợ cả Dark mode và Light mode, không viết cứng mã hex.
 * UMD: Chạy được trong Node.js (test) và trình duyệt (window.HLMau)
 */
(function (root, factory) {
  if (typeof define === 'function' && define.amd) {
    define([], factory);
  } else if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.HLMau = factory();
  }
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  var TOKENS_MAC_DINH = {
    nen: 'var(--hl-nen, #ffffff)',
    vien: 'var(--hl-vien, #e4e4e7)',
    chu: 'var(--hl-chu, #09090b)',
    chuPhu: 'var(--hl-chu-phu, #71717a)',
    nhan: 'var(--hl-nhan, #0284c7)',
    khi: 'var(--hl-khi, #38bdf8)',
    chay: 'var(--hl-chay, #ef4444)',
    piston: 'var(--hl-piston, #94a3b8)',
    thanhTruyen: 'var(--hl-thanh-truyen, #64748b)',
    banhDa: 'var(--hl-banh-da, #cbd5e1)',
    luoi: 'var(--hl-luoi, rgba(148, 163, 184, 0.2))'
  };

  /**
   * Chuyển mã hex hoặc rgb sang {r, g, b, a}
   */
  function parseRgba(str) {
    if (!str || typeof str !== 'string') return { r: 0, g: 0, b: 0, a: 1 };
    str = str.trim();
    if (str.startsWith('#')) {
      var hex = str.slice(1);
      if (hex.length === 3) {
        return {
          r: parseInt(hex[0] + hex[0], 16),
          g: parseInt(hex[1] + hex[1], 16),
          b: parseInt(hex[2] + hex[2], 16),
          a: 1
        };
      }
      if (hex.length === 6) {
        return {
          r: parseInt(hex.slice(0, 2), 16),
          g: parseInt(hex.slice(2, 4), 16),
          b: parseInt(hex.slice(4, 6), 16),
          a: 1
        };
      }
    }
    var m = str.match(/rgba?\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)(?:\s*,\s*([\d.]+))?\s*\)/);
    if (m) {
      return {
        r: parseInt(m[1], 10),
        g: parseInt(m[2], 10),
        b: parseInt(m[3], 10),
        a: m[4] !== undefined ? parseFloat(m[4]) : 1
      };
    }
    return { r: 56, g: 189, b: 248, a: 1 };
  }

  /**
   * Nội suy màu nhiệt độ từ xanh lơ (300 K) -> vàng (1000 K) -> cam (1800 K) -> đỏ rực lửa (> 2500 K)
   */
  function mauNhietDo(T, Tmin, Tmax) {
    Tmin = Tmin || 300;
    Tmax = Tmax || 2500;
    var f = Math.max(0, Math.min(1, (T - Tmin) / (Tmax - Tmin)));

    // Điểm màu:
    // f = 0.0: rgb(56, 189, 248) - xanh lơ dịu
    // f = 0.35: rgb(251, 191, 36) - vàng
    // f = 0.70: rgb(249, 115, 22) - cam rực
    // f = 1.0: rgb(239, 68, 68) - đỏ rực lửa
    var r, g, b;
    if (f < 0.35) {
      var t1 = f / 0.35;
      r = Math.round(56 + (251 - 56) * t1);
      g = Math.round(189 + (191 - 189) * t1);
      b = Math.round(248 + (36 - 248) * t1);
    } else if (f < 0.70) {
      var t2 = (f - 0.35) / 0.35;
      r = Math.round(251 + (249 - 251) * t2);
      g = Math.round(191 + (115 - 191) * t2);
      b = Math.round(36 + (22 - 36) * t2);
    } else {
      var t3 = (f - 0.70) / 0.30;
      r = Math.round(249 + (239 - 249) * t3);
      g = Math.round(115 + (68 - 115) * t3);
      b = Math.round(22 + (68 - 22) * t3);
    }
    return 'rgb(' + r + ', ' + g + ', ' + b + ')';
  }

  /**
   * Trộn nhiều màu theo tỉ lệ trọng số
   * @param {Array<{mau: string, trongSo: number}>} danhSachMau
   */
  function tronMau(danhSachMau) {
    if (!danhSachMau || danhSachMau.length === 0) return 'rgba(0,0,0,0)';
    var tongW = 0;
    for (var i = 0; i < danhSachMau.length; i++) {
      tongW += Math.max(0, danhSachMau[i].trongSo || 0);
    }
    if (tongW <= 0) return 'rgba(0,0,0,0)';

    var r = 0, g = 0, b = 0, a = 0;
    for (var j = 0; j < danhSachMau.length; j++) {
      var item = danhSachMau[j];
      var w = Math.max(0, item.trongSo || 0) / tongW;
      var c = parseRgba(item.mau);
      r += c.r * w;
      g += c.g * w;
      b += c.b * w;
      a += c.a * w;
    }
    return 'rgba(' + Math.round(r) + ', ' + Math.round(g) + ', ' + Math.round(b) + ', ' + a.toFixed(3) + ')';
  }

  /**
   * Lấy giá trị token thực tế trong DOM (hoặc trả về fallback)
   */
  function layToken(ten, el) {
    if (typeof window === 'undefined' || !window.getComputedStyle) {
      return TOKENS_MAC_DINH[ten] || '#38bdf8';
    }
    var target = el || document.documentElement;
    var val = window.getComputedStyle(target).getPropertyValue('--hl-' + ten).trim();
    return val || TOKENS_MAC_DINH[ten] || '#38bdf8';
  }

  return {
    tokens: TOKENS_MAC_DINH,
    layToken: layToken,
    mauNhietDo: mauNhietDo,
    tronMau: tronMau,
    parseRgba: parseRgba
  };
});
