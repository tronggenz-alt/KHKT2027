/**
 * ngau-nhien.js - Bộ sinh số ngẫu nhiên có seed (Mulberry32) và các phân bố vật lý
 * UMD: Chạy được trong Node.js (test) và trình duyệt (window.HLNgauNhien)
 */
(function (root, factory) {
  if (typeof define === 'function' && define.amd) {
    define([], factory);
  } else if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.HLNgauNhien = factory();
  }
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  function taoMulberry32(seed) {
    var a = (seed !== undefined && seed !== null) ? (seed >>> 0) : ((Math.random() * 0xFFFFFFFF) >>> 0);
    if (a === 0) a = 123456789;
    return function () {
      var t = (a += 0x6D2B79F5);
      t = Math.imul(t ^ (t >>> 15), t | 1);
      t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  function taoBoNgauNhien(seed) {
    var rng = taoMulberry32(seed);

    // Lưu biến tạm cho Box-Muller
    var hasSpare = false;
    var spare = 0;

    function deu(min, max) {
      if (min === undefined) min = 0;
      if (max === undefined) max = 1;
      return min + (max - min) * rng();
    }

    function chuan(mean, std) {
      if (mean === undefined) mean = 0;
      if (std === undefined) std = 1;

      if (hasSpare) {
        hasSpare = false;
        return mean + std * spare;
      }

      var u, v, s;
      do {
        u = rng() * 2 - 1;
        v = rng() * 2 - 1;
        s = u * u + v * v;
      } while (s >= 1 || s === 0);

      var mul = Math.sqrt(-2.0 * Math.log(s) / s);
      spare = v * mul;
      hasSpare = true;
      return mean + std * (u * mul);
    }

    /**
     * Độ lớn vận tốc phân bố Maxwell-Boltzmann 3D
     * vx, vy, vz ~ N(0, kT/m) => v = sqrt(vx^2 + vy^2 + vz^2)
     * Kỳ vọng <v^2> = 3 * kT / m
     */
    function maxwell3D(m, kT) {
      var sigma = Math.sqrt(kT / m);
      var vx = chuan(0, sigma);
      var vy = chuan(0, sigma);
      var vz = chuan(0, sigma);
      return Math.sqrt(vx * vx + vy * vy + vz * vz);
    }

    /**
     * Độ lớn vận tốc phân bố Maxwell-Boltzmann 2D
     * vx, vy ~ N(0, kT/m) => v = sqrt(-2 * (kT/m) * ln(U))
     * Kỳ vọng <v^2> = 2 * kT / m
     */
    function maxwell2D(m, kT) {
      var u = Math.max(1e-12, rng());
      return Math.sqrt(-2.0 * (kT / m) * Math.log(u));
    }

    /**
     * Lấy mẫu phân bố Poisson với tham số lambda
     */
    function poisson(lambda) {
      if (lambda <= 0) return 0;
      if (lambda < 30) {
        var L = Math.exp(-lambda);
        var k = 0;
        var p = 1.0;
        do {
          k++;
          p *= rng();
        } while (p > L);
        return k - 1;
      }
      // Xấp xỉ Gauss cho lambda lớn
      var g = chuan(lambda, Math.sqrt(lambda));
      return Math.max(0, Math.round(g));
    }

    return {
      rng: rng,
      deu: deu,
      chuan: chuan,
      maxwell3D: maxwell3D,
      maxwell2D: maxwell2D,
      poisson: poisson
    };
  }

  // Bộ sinh mặc định không seed
  var macDinh = taoBoNgauNhien(1337);

  return {
    taoBoNgauNhien: taoBoNgauNhien,
    deu: macDinh.deu,
    chuan: macDinh.chuan,
    maxwell3D: macDinh.maxwell3D,
    maxwell2D: macDinh.maxwell2D,
    poisson: macDinh.poisson
  };
});
