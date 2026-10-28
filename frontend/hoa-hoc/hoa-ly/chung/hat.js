/**
 * hat.js - Động cơ mô phỏng hạt 2D độc lập DOM
 * UMD: Chạy được trong Node.js (test) và trình duyệt (window.HLHat)
 */
(function (root, factory) {
  if (typeof define === 'function' && define.amd) {
    define([], factory);
  } else if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.HLHat = factory();
  }
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  function HeHat(options) {
    options = options || {};
    this.hat = []; // mảng các hạt
    this.thanh = {
      xMin: options.xMin || 0,
      xMax: options.xMax || 400,
      yMin: options.yMin || 0,
      yMax: options.yMax || 200,
      vxMax: options.vxMax || 0 // vận tốc của thành phải (pít-tông)
    };
    this.choPhepVaChamHat = options.choPhepVaChamHat !== undefined ? options.choPhepVaChamHat : false;
    this.khiVaCham = options.khiVaCham || null; // hàm gọi lại khi va chạm: (h1, h2, En)

    // Thống kê đo lường
    this.tongXungLuc = 0; // tổng delta p lên tất cả các thành
    this.tongXungLucPhai = 0; // riêng thành phải (pít-tông)
    this.tongThoiGian = 0;
    this.demVaCham = 0;
    this.demVaChamHieuQua = 0;
  }

  HeHat.prototype.datThanh = function (thanh) {
    if (!thanh) return;
    if (thanh.xMin !== undefined) this.thanh.xMin = thanh.xMin;
    if (thanh.xMax !== undefined) this.thanh.xMax = thanh.xMax;
    if (thanh.yMin !== undefined) this.thanh.yMin = thanh.yMin;
    if (thanh.yMax !== undefined) this.thanh.yMax = thanh.yMax;
    if (thanh.vxMax !== undefined) this.thanh.vxMax = thanh.vxMax;

    // Giữ tất cả các hạt nằm gọn trong thành mới nếu thành co lại
    for (var i = 0; i < this.hat.length; i++) {
      var h = this.hat[i];
      if (h.x + h.r > this.thanh.xMax) {
        h.x = Math.max(this.thanh.xMin + h.r, this.thanh.xMax - h.r);
        if (h.vx > this.thanh.vxMax) h.vx = -Math.abs(h.vx);
      }
      if (h.x - h.r < this.thanh.xMin) {
        h.x = this.thanh.xMin + h.r;
        if (h.vx < 0) h.vx = Math.abs(h.vx);
      }
      if (h.y + h.r > this.thanh.yMax) {
        h.y = this.thanh.yMax - h.r;
        if (h.vy > 0) h.vy = -Math.abs(h.vy);
      }
      if (h.y - h.r < this.thanh.yMin) {
        h.y = this.thanh.yMin + h.r;
        if (h.vy < 0) h.vy = Math.abs(h.vy);
      }
    }
  };

  HeHat.prototype.themHat = function (h) {
    var hatMoi = {
      id: h.id !== undefined ? h.id : this.hat.length + 1,
      loai: h.loai || 'A',
      m: h.m !== undefined ? h.m : 1.0,
      r: h.r !== undefined ? h.r : 3.0,
      x: h.x !== undefined ? h.x : (this.thanh.xMin + this.thanh.xMax) / 2,
      y: h.y !== undefined ? h.y : (this.thanh.yMin + this.thanh.yMax) / 2,
      vx: h.vx !== undefined ? h.vx : 0,
      vy: h.vy !== undefined ? h.vy : 0,
      mau: h.mau || 'var(--hl-khi, #38bdf8)',
      noiBat: !!h.noiBat
    };
    this.hat.push(hatMoi);
    return hatMoi;
  };

  HeHat.prototype.xoaHet = function () {
    this.hat = [];
    this.tongXungLuc = 0;
    this.tongXungLucPhai = 0;
    this.tongThoiGian = 0;
    this.demVaCham = 0;
    this.demVaChamHieuQua = 0;
  };

  HeHat.prototype.buoc = function (dt) {
    if (dt <= 0) return;
    this.tongThoiGian += dt;

    var n = this.hat.length;
    var thanh = this.thanh;

    // 1. Cập nhật vị trí và va chạm thành
    for (var i = 0; i < n; i++) {
      var h = this.hat[i];
      h.x += h.vx * dt;
      h.y += h.vy * dt;

      // Va chạm thành trái
      if (h.x - h.r < thanh.xMin) {
        h.x = thanh.xMin + h.r;
        var dpL = 2 * h.m * Math.abs(h.vx);
        this.tongXungLuc += dpL;
        h.vx = Math.abs(h.vx);
      }

      // Va chạm thành phải (có thể chuyển động với vxMax)
      if (h.x + h.r > thanh.xMax) {
        h.x = thanh.xMax - h.r;
        var vPiston = thanh.vxMax;
        var vxCu = h.vx;
        // Va chạm đàn hồi với thành di chuyển: v' = 2*v_wall - v
        var vxMoi = 2 * vPiston - h.vx;
        // Đảm bảo sau va chạm hạt phải bay ngược về bên trái so với thành
        if (vxMoi >= vPiston) {
          vxMoi = vPiston - Math.max(Math.abs(vxCu - vPiston), 1e-4);
        }
        var dpR = h.m * Math.abs(vxCu - vxMoi);
        this.tongXungLuc += dpR;
        this.tongXungLucPhai += dpR;
        h.vx = vxMoi;
      }

      // Va chạm thành trên
      if (h.y - h.r < thanh.yMin) {
        h.y = thanh.yMin + h.r;
        var dpT = 2 * h.m * Math.abs(h.vy);
        this.tongXungLuc += dpT;
        h.vy = Math.abs(h.vy);
      }

      // Va chạm thành dưới
      if (h.y + h.r > thanh.yMax) {
        h.y = thanh.yMax - h.r;
        var dpB = 2 * h.m * Math.abs(h.vy);
        this.tongXungLuc += dpB;
        h.vy = -Math.abs(h.vy);
      }
    }

    // 2. Va chạm hạt - hạt đàn hồi (nếu bật)
    if (this.choPhepVaChamHat && n > 1) {
      for (var i1 = 0; i1 < n - 1; i1++) {
        var h1 = this.hat[i1];
        for (var i2 = i1 + 1; i2 < n; i2++) {
          var h2 = this.hat[i2];
          var dx = h2.x - h1.x;
          var dy = h2.y - h1.y;
          var distSq = dx * dx + dy * dy;
          var rSum = h1.r + h2.r;

          if (distSq < rSum * rSum && distSq > 1e-12) {
            var dist = Math.sqrt(distSq);
            var nx = dx / dist;
            var ny = dy / dist;

            // Vận tốc tương đối dọc pháp tuyến nối tâm
            var vRelX = h2.vx - h1.vx;
            var vRelY = h2.vy - h1.vy;
            var vn = vRelX * nx + vRelY * ny;

            // Chỉ va chạm khi 2 hạt đang tiến lại gần nhau
            if (vn < 0) {
              var mu = (h1.m * h2.m) / (h1.m + h2.m);
              var En = 0.5 * mu * vn * vn;
              this.demVaCham++;

              var daPhanUng = false;
              if (this.khiVaCham) {
                var kq = this.khiVaCham(h1, h2, En);
                if (kq && kq.phanUng) {
                  this.demVaChamHieuQua++;
                  daPhanUng = true;
                }
              }

              if (!daPhanUng) {
                // Xung lực va chạm đàn hồi 2D
                var J = 2 * mu * (-vn);
                h1.vx -= (J / h1.m) * nx;
                h1.vy -= (J / h1.m) * ny;
                h2.vx += (J / h2.m) * nx;
                h2.vy += (J / h2.m) * ny;
              }

              // Đẩy nhẹ 2 hạt ra để tránh chồng lấn
              var overlap = 0.5 * (rSum - dist);
              h1.x -= nx * overlap;
              h1.y -= ny * overlap;
              h2.x += nx * overlap;
              h2.y += ny * overlap;
            }
          }
        }
      }
    }
  };

  /**
   * Tính tổng động lượng toàn hệ (để test bảo toàn)
   */
  HeHat.prototype.dongLuong = function () {
    var px = 0, py = 0;
    for (var i = 0; i < this.hat.length; i++) {
      px += this.hat[i].m * this.hat[i].vx;
      py += this.hat[i].m * this.hat[i].vy;
    }
    return { px: px, py: py };
  };

  /**
   * Tính tổng động năng toàn hệ (để test bảo toàn)
   */
  HeHat.prototype.dongNang = function () {
    var E = 0;
    for (var i = 0; i < this.hat.length; i++) {
      var h = this.hat[i];
      E += 0.5 * h.m * (h.vx * h.vx + h.vy * h.vy);
    }
    return E;
  };

  /**
   * Áp suất trung bình đo từ xung lực va chạm chia cho thời gian và chu vi
   */
  HeHat.prototype.apSuatDo = function () {
    if (this.tongThoiGian <= 0) return 0;
    var W = Math.max(1, this.thanh.xMax - this.thanh.xMin);
    var H = Math.max(1, this.thanh.yMax - this.thanh.yMin);
    var chuVi = 2 * (W + H);
    return this.tongXungLuc / (this.tongThoiGian * chuVi);
  };

  return {
    HeHat: HeHat
  };
});
