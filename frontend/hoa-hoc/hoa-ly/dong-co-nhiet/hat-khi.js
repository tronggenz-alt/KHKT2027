/**
 * hat-khi.js - Mô phỏng chuyển động nhiệt của phân tử khí trong xi-lanh
 * Sử dụng HLVeHat và cắt (clip) tuyệt đối theo vùng chứa khí (vungKhi)
 * UMD: Chạy được trong Node.js (test) và trình duyệt (window.HLHatKhi)
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.HLHatKhi = factory();
  }
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';

  function khoiTaoHatKhi(canvasEl, svgEl) {
    if (!canvasEl || !svgEl) return null;
    var ctx = canvasEl.getContext('2d');
    if (!ctx) return null;

    var SO_HAT = 120;
    var hat = [];
    var kichHoat = true;

    if (typeof window !== 'undefined' && window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      kichHoat = false;
    }

    // Khởi tạo các hạt
    for (var i = 0; i < SO_HAT; i++) {
      hat.push({
        x: 0,
        y: 0,
        vx: (Math.random() - 0.5) * 2,
        vy: (Math.random() - 0.5) * 2,
        r: 2.0 + Math.random() * 0.8,
        mau: 'var(--hl-khi, #38bdf8)'
      });
    }

    /**
     * Cập nhật vị trí và vẽ các hạt, cắt clip theo vungKhi
     * @param {Object} vungKhi - { x, y, w, h } trong hệ toạ độ SVG viewBox
     * @param {number} T_K - Nhiệt độ hiện tại
     * @param {number} doan - Kỳ hoạt động (0-5)
     */
    function capNhatVaVe(vungKhi, T_K, doan) {
      if (!kichHoat || !vungKhi || vungKhi.w <= 0 || vungKhi.h <= 0) {
        ctx.clearRect(0, 0, canvasEl.width, canvasEl.height);
        return;
      }

      var rect = svgEl.getBoundingClientRect();
      var dpr = window.devicePixelRatio || 1;
      var w = Math.round(rect.width);
      var h = Math.round(rect.height);

      if (canvasEl.width !== w * dpr || canvasEl.height !== h * dpr) {
        canvasEl.width = w * dpr;
        canvasEl.height = h * dpr;
        canvasEl.style.width = w + 'px';
        canvasEl.style.height = h + 'px';
      }

      var vb = svgEl.viewBox ? svgEl.viewBox.baseVal : null;
      var tiLeX = (vb && vb.width > 0) ? (w * dpr) / vb.width : dpr;
      var tiLeY = (vb && vb.height > 0) ? (h * dpr) / vb.height : dpr;

      ctx.clearRect(0, 0, canvasEl.width, canvasEl.height);
      ctx.save();

      // Cắt (clip) theo đúng vùng chứa khí trong xi-lanh
      var clipX = vungKhi.x * tiLeX;
      var clipY = vungKhi.y * tiLeY;
      var clipW = vungKhi.w * tiLeX;
      var clipH = vungKhi.h * tiLeY;

      ctx.beginPath();
      ctx.rect(clipX, clipY, clipW, clipH);
      ctx.clip();

      // Tốc độ tỉ lệ với căn nhiệt độ sqrt(T / 300)
      var tocDo = Math.sqrt(Math.max(250, T_K) / 300) * 2.0;

      // Màu hạt theo nhiệt độ
      var mauHat = '#38bdf8';
      if (T_K > 1500) mauHat = '#ef4444';
      else if (T_K > 800) mauHat = '#f59e0b';
      ctx.fillStyle = mauHat;

      var xMin = vungKhi.x;
      var xMax = vungKhi.x + vungKhi.w;
      var yMin = vungKhi.y;
      var yMax = vungKhi.y + vungKhi.h;

      for (var i = 0; i < SO_HAT; i++) {
        var p = hat[i];

        // Khởi tạo lại nếu hạt nằm ngoài vùng khí
        if (p.x < xMin || p.x > xMax || p.y < yMin || p.y > yMax) {
          p.x = xMin + Math.random() * vungKhi.w;
          p.y = yMin + Math.random() * vungKhi.h;
        }

        p.x += p.vx * tocDo;
        p.y += p.vy * tocDo;

        // Va chạm đàn hồi với 4 vách xi-lanh
        if (p.x <= xMin + p.r) {
          p.x = xMin + p.r;
          p.vx = Math.abs(p.vx);
        } else if (p.x >= xMax - p.r) {
          p.x = xMax - p.r;
          p.vx = -Math.abs(p.vx);
        }

        if (p.y <= yMin + p.r) {
          p.y = yMin + p.r;
          p.vy = Math.abs(p.vy);
        } else if (p.y >= yMax - p.r) {
          p.y = yMax - p.r;
          p.vy = -Math.abs(p.vy);
        }

        // Vẽ hạt
        var cx = p.x * tiLeX;
        var cy = p.y * tiLeY;
        var cr = p.r * Math.min(tiLeX, tiLeY);

        ctx.beginPath();
        ctx.arc(cx, cy, Math.max(1.5, cr), 0, Math.PI * 2);
        ctx.fill();
      }

      ctx.restore();
    }

    /**
     * Lấy danh sách hạt quy đổi ra toạ độ client (để test Playwright kiểm tra)
     */
    function layDanhSachHat() {
      var rect = svgEl.getBoundingClientRect();
      var vb = svgEl.viewBox ? svgEl.viewBox.baseVal : null;
      var tiLeX = (vb && vb.width > 0) ? rect.width / vb.width : 1;
      var tiLeY = (vb && vb.height > 0) ? rect.height / vb.height : 1;

      return hat.map(function (h) {
        return {
          x: rect.left + h.x * tiLeX,
          y: rect.top + h.y * tiLeY,
          loai: 'khi'
        };
      });
    }

    return {
      capNhatVaVe: capNhatVaVe,
      layDanhSachHat: layDanhSachHat
    };
  }

  return {
    khoiTaoHatKhi: khoiTaoHatKhi
  };
});
