/**
 * ve-hat.js - Trình render hạt khí lên Canvas đặt chồng khít lên SVG
 * Tự động đồng bộ hệ toạ độ và cắt (clip) theo vùng chứa khí (vungKhi)
 * UMD: Chạy được trong Node.js (test) và trình duyệt (window.HLVeHat)
 */
(function (root, factory) {
  if (typeof define === 'function' && define.amd) {
    define([], factory);
  } else if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.HLVeHat = factory();
  }
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  function TrinhVeHat(canvas, svg) {
    this.canvas = canvas;
    this.svg = svg;
    this.ctx = canvas && canvas.getContext ? canvas.getContext('2d') : null;
    this.dpr = typeof window !== 'undefined' ? (window.devicePixelRatio || 1) : 1;
    this.tiLeX = 1;
    this.tiLeY = 1;
    this.gocX = 0;
    this.gocY = 0;
  }

  /**
   * Đồng bộ kích thước canvas với SVG
   */
  TrinhVeHat.prototype.dongBoKichThuoc = function () {
    if (!this.canvas || !this.svg || !this.ctx) return;
    var rect = this.svg.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) return;

    this.dpr = window.devicePixelRatio || 1;
    var w = Math.round(rect.width);
    var h = Math.round(rect.height);

    if (this.canvas.width !== w * this.dpr || this.canvas.height !== h * this.dpr) {
      this.canvas.width = w * this.dpr;
      this.canvas.height = h * this.dpr;
      this.canvas.style.width = w + 'px';
      this.canvas.style.height = h + 'px';
    }

    // Lấy viewBox của SVG để tính ma trận quy đổi
    var vb = this.svg.viewBox ? this.svg.viewBox.baseVal : null;
    if (vb && vb.width > 0 && vb.height > 0) {
      this.tiLeX = (w * this.dpr) / vb.width;
      this.tiLeY = (h * this.dpr) / vb.height;
      this.gocX = -vb.x * this.tiLeX;
      this.gocY = -vb.y * this.tiLeY;
    } else {
      this.tiLeX = this.dpr;
      this.tiLeY = this.dpr;
      this.gocX = 0;
      this.gocY = 0;
    }
  };

  /**
   * Quy đổi toạ độ từ viewBox sang toạ độ client màn hình (để test Playwright kiểm chứng)
   */
  TrinhVeHat.prototype.toaDoClient = function (x, y) {
    if (!this.svg) return { x: x, y: y };
    var rect = this.svg.getBoundingClientRect();
    var vb = this.svg.viewBox ? this.svg.viewBox.baseVal : null;
    if (!vb || vb.width <= 0) return { x: rect.left + x, y: rect.top + y };
    var px = rect.left + ((x - vb.x) / vb.width) * rect.width;
    var py = rect.top + ((y - vb.y) / vb.height) * rect.height;
    return { x: px, y: py };
  };

  /**
   * Vẽ danh sách hạt lên canvas và cắt theo vùng chứa khí
   * @param {Array} danhSachHat - Mảng các hạt {x, y, r, mau, noiBat}
   * @param {Object} vungKhi - Vùng chứa khí {x, y, w, h} theo toạ độ viewBox
   * @param {Object} options - Tuỳ chọn {mauNen, doMo}
   */
  TrinhVeHat.prototype.ve = function (danhSachHat, vungKhi, options) {
    if (!this.ctx || !this.canvas) return;
    this.dongBoKichThuoc();

    var ctx = this.ctx;
    var w = this.canvas.width;
    var h = this.canvas.height;
    options = options || {};

    ctx.clearRect(0, 0, w, h);
    if (!danhSachHat || danhSachHat.length === 0) return;

    ctx.save();

    // Cắt (clip) theo vùng chứa khí nếu được cung cấp
    if (vungKhi && vungKhi.w > 0 && vungKhi.h > 0) {
      var clipX = this.gocX + vungKhi.x * this.tiLeX;
      var clipY = this.gocY + vungKhi.y * this.tiLeY;
      var clipW = vungKhi.w * this.tiLeX;
      var clipH = vungKhi.h * this.tiLeY;

      ctx.beginPath();
      ctx.rect(clipX, clipY, clipW, clipH);
      ctx.clip();

      // Nếu có màu nền khí (ví dụ màu NO2)
      if (options.mauNenKhí) {
        ctx.fillStyle = options.mauNenKhí;
        ctx.fillRect(clipX, clipY, clipW, clipH);
      }
    }

    // Vẽ từng hạt
    for (var i = 0; i < danhSachHat.length; i++) {
      var hat = danhSachHat[i];
      var cx = this.gocX + hat.x * this.tiLeX;
      var cy = this.gocY + hat.y * this.tiLeY;
      var cr = (hat.r || 3.0) * Math.min(this.tiLeX, this.tiLeY);

      ctx.beginPath();
      ctx.arc(cx, cy, Math.max(1.5, cr), 0, Math.PI * 2);

      if (hat.noiBat) {
        // Phân tử đang được theo dõi: phát sáng và có viền
        ctx.fillStyle = '#f59e0b';
        ctx.shadowColor = '#f59e0b';
        ctx.shadowBlur = 8;
        ctx.fill();
        ctx.shadowBlur = 0;

        ctx.strokeStyle = '#ffffff';
        ctx.lineWidth = 1.5;
        ctx.stroke();
      } else {
        ctx.fillStyle = hat.mau || 'var(--hl-khi, #38bdf8)';
        ctx.fill();
      }
    }

    ctx.restore();
  };

  return {
    TrinhVeHat: TrinhVeHat
  };
});
