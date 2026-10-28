/**
 * dinh-dang.js - Tiện ích định dạng số và chia vạch đồ thị theo chuẩn Việt Nam
 * UMD: Chạy được trong cả Node.js (test) và trình duyệt (window.HLDinhDang)
 */
(function (root, factory) {
  if (typeof define === 'function' && define.amd) {
    define([], factory);
  } else if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.HLDinhDang = factory();
  }
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  var MU_UNICODE = {
    '0': '⁰', '1': '¹', '2': '²', '3': '³', '4': '⁴',
    '5': '⁵', '6': '⁶', '7': '⁷', '8': '⁸', '9': '⁹',
    '-': '⁻', '+': '⁺'
  };

  function doiSoMuUnicode(n) {
    var s = String(n);
    var res = '';
    for (var i = 0; i < s.length; i++) {
      res += MU_UNICODE[s[i]] || s[i];
    }
    return res;
  }

  /**
   * Định dạng số theo chuẩn Việt Nam: dấu phẩy thập phân, dấu chấm hàng nghìn
   */
  function dinhDangSo(val, soThapPhan) {
    if (val === null || val === undefined || isNaN(val)) return '—';
    if (!isFinite(val)) return val > 0 ? '+∞' : '-∞';
    var n = typeof soThapPhan === 'number' ? Number(val.toFixed(soThapPhan)) : val;
    var parts = String(n).split('.');
    var nguyen = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, '.');
    if (parts.length > 1) {
      return nguyen + ',' + parts[1];
    }
    return nguyen;
  }

  /**
   * Định dạng khoa học với số mũ Unicode (ví dụ: 6,21·10⁻²¹)
   */
  function dinhDangKhoaHoc(val, soChuSo) {
    if (val === null || val === undefined || isNaN(val)) return '—';
    if (val === 0) return '0';
    var chuSo = typeof soChuSo === 'number' ? soChuSo : 2;
    var exp = Math.floor(Math.log10(Math.abs(val)));
    var m = val / Math.pow(10, exp);
    if (Math.abs(exp) <= 2) {
      return dinhDangSo(val, chuSo);
    }
    return dinhDangSo(m, chuSo) + '·10' + doiSoMuUnicode(exp);
  }

  /**
   * Chia vạch đẹp (nice ticks) theo quy tắc 1 - 2 - 5
   * @param {number} min - Giá trị nhỏ nhất
   * @param {number} max - Giá trị lớn nhất
   * @param {number} soVach - Số lượng vạch mong muốn (mặc định 5)
   * @param {boolean} baoTron - Có bao trọn khoảng [min, max] hay không (mặc định false)
   * @returns {number[]} Mảng các giá trị vạch chia
   */
  function vachDep(min, max, soVach, baoTron) {
    if (min === max) return [min];
    if (min > max) {
      var tmp = min; min = max; max = tmp;
    }
    soVach = soVach || 5;
    var range = max - min;
    var roughStep = range / soVach;
    var exp = Math.floor(Math.log10(roughStep));
    var frac = roughStep / Math.pow(10, exp);

    var step;
    if (frac < 1.5) {
      step = 1 * Math.pow(10, exp);
    } else if (frac < 3.5) {
      step = 2 * Math.pow(10, exp);
    } else if (frac < 7.5) {
      step = 5 * Math.pow(10, exp);
    } else {
      step = 10 * Math.pow(10, exp);
    }

    // Làm tròn chính xác bước nhảy để tránh lỗi phẩy động
    step = parseFloat(step.toPrecision(12));

    var start = Math.ceil(min / step) * step;
    var end = Math.floor(max / step) * step;

    if (baoTron) {
      start = Math.floor(min / step) * step;
      end = Math.ceil(max / step) * step;
    }

    var ticks = [];
    var cur = start;
    // Đảm bảo không rơi vào vòng lặp vô tận
    var safety = 0;
    while (cur <= end + step * 0.001 && safety < 100) {
      var rounded = parseFloat(cur.toPrecision(12));
      ticks.push(rounded);
      cur += step;
      safety++;
    }
    return ticks;
  }

  /**
   * Chia vạch log đẹp theo các lũy thừa của 10 hoặc nửa thập kỷ (1, 2, 5)
   */
  function vachLog(min, max) {
    if (min <= 0 || max <= 0) return [];
    var logMin = Math.floor(Math.log10(min));
    var logMax = Math.ceil(Math.log10(max));
    var ticks = [];
    var span = logMax - logMin;

    if (span <= 3) {
      for (var d = logMin; d <= logMax; d++) {
        var base = Math.pow(10, d);
        [1, 2, 5].forEach(function (m) {
          var v = m * base;
          if (v >= min * 0.999 && v <= max * 1.001) {
            ticks.push(parseFloat(v.toPrecision(10)));
          }
        });
      }
    } else {
      for (var d2 = logMin; d2 <= logMax; d2++) {
        var v2 = Math.pow(10, d2);
        if (v2 >= min * 0.999 && v2 <= max * 1.001) {
          ticks.push(parseFloat(v2.toPrecision(10)));
        }
      }
    }
    return ticks;
  }

  return {
    so: dinhDangSo,
    khoaHoc: dinhDangKhoaHoc,
    soKhoaHoc: dinhDangKhoaHoc,
    doiSoMuUnicode: doiSoMuUnicode,
    vachDep: vachDep,
    vachLog: vachLog
  };
});
