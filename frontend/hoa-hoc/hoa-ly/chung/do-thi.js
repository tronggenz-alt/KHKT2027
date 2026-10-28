/**
 * do-thi.js - Thành phần vẽ đồ thị SVG khoa học tái sử dụng
 * Tạo DOM 1 lần, cập nhật bằng setAttribute (không gán innerHTML mỗi frame)
 * Tự động chia vạch đẹp 1-2-5 và tránh nhãn đè nhau (< 28px)
 * UMD: Chạy được trong Node.js (test) và trình duyệt (window.HLDoThi)
 */
(function (root, factory) {
  if (typeof define === 'function' && define.amd) {
    define(['./dinh-dang'], factory);
  } else if (typeof module === 'object' && module.exports) {
    module.exports = factory(require('./dinh-dang'));
  } else {
    root.HLDoThi = factory(root.HLDinhDang);
  }
})(typeof self !== 'undefined' ? self : this, function (HLDinhDang) {
  'use strict';

  var SVG_NS = 'http://www.w3.org/2000/svg';

  function DoThiSVG(nhomSVG, cauHinh) {
    this.g = nhomSVG;
    this.cfg = cauHinh || {};

    // Vùng vẽ trong hệ toạ độ viewBox
    this.x = this.cfg.x || 60;
    this.y = this.cfg.y || 40;
    this.w = this.cfg.w || 500;
    this.h = this.cfg.h || 300;

    this.xMin = this.cfg.xMin || 0;
    this.xMax = this.cfg.xMax || 500;
    this.yMin = this.cfg.yMin || 0;
    this.yMax = this.cfg.yMax || 100;

    this.tenTrucX = this.cfg.tenTrucX || 'V (cm³)';
    this.tenTrucY = this.cfg.tenTrucY || 'p (bar)';

    // Nhóm phần tử con
    this.gLuoi = null;
    this.gTruc = null;
    this.gVungTo = null;
    this.gDuong = null;
    this.gBongMa = null;
    this.gDiem = null;
    this.chamHienTai = null;

    if (this.g && typeof document !== 'undefined') {
      this._khoiTaoDOM();
    }
  }

  DoThiSVG.prototype._khoiTaoDOM = function () {
    this.g.innerHTML = '';

    this.gLuoi = document.createElementNS(SVG_NS, 'g');
    this.gLuoi.setAttribute('class', 'hl-do-thi-luoi');

    this.gVungTo = document.createElementNS(SVG_NS, 'path');
    this.gVungTo.setAttribute('class', 'hl-do-thi-vung-to');
    this.gVungTo.setAttribute('fill', 'var(--hl-nhan, rgba(56, 189, 248, 0.15))');
    this.gVungTo.setAttribute('stroke', 'none');

    this.gBongMa = document.createElementNS(SVG_NS, 'path');
    this.gBongMa.setAttribute('class', 'hl-do-thi-bong-ma');
    this.gBongMa.setAttribute('fill', 'none');
    this.gBongMa.setAttribute('stroke', 'var(--hl-chu-phu, #94a3b8)');
    this.gBongMa.setAttribute('stroke-width', '1.5');
    this.gBongMa.setAttribute('stroke-dasharray', '4 4');

    this.gDuong = document.createElementNS(SVG_NS, 'path');
    this.gDuong.setAttribute('class', 'hl-do-thi-duong');
    this.gDuong.setAttribute('fill', 'none');
    this.gDuong.setAttribute('stroke', 'var(--hl-chu, #09090b)');
    this.gDuong.setAttribute('stroke-width', '2.5');

    this.gTruc = document.createElementNS(SVG_NS, 'g');
    this.gTruc.setAttribute('class', 'hl-do-thi-truc');

    this.gDiem = document.createElementNS(SVG_NS, 'g');
    this.gDiem.setAttribute('class', 'hl-do-thi-diem');

    this.chamHienTai = document.createElementNS(SVG_NS, 'circle');
    this.chamHienTai.setAttribute('class', 'hl-cham-hien-tai');
    this.chamHienTai.setAttribute('r', '6');
    this.chamHienTai.setAttribute('fill', 'var(--hl-chay, #ef4444)');
    this.chamHienTai.setAttribute('stroke', '#ffffff');
    this.chamHienTai.setAttribute('stroke-width', '2');
    this.chamHienTai.style.cursor = 'grab';

    this.g.appendChild(this.gLuoi);
    this.g.appendChild(this.gVungTo);
    this.g.appendChild(this.gBongMa);
    this.g.appendChild(this.gDuong);
    this.g.appendChild(this.gTruc);
    this.g.appendChild(this.gDiem);
    this.g.appendChild(this.chamHienTai);
  };

  /**
   * Chuyển đổi toạ độ giá trị thực (xReal, yReal) sang toạ độ SVG pixel
   */
  DoThiSVG.prototype.toDoSVG = function (xReal, yReal) {
    var fx = (xReal - this.xMin) / (this.xMax - this.xMin);
    var fy = (yReal - this.yMin) / (this.yMax - this.yMin);
    var px = this.x + fx * this.w;
    var py = this.y + (1 - fy) * this.h;
    return { x: px, y: py };
  };

  /**
   * Chuyển đổi toạ độ SVG pixel sang toạ độ giá trị thực
   */
  DoThiSVG.prototype.toDoThuc = function (px, py) {
    var fx = (px - this.x) / this.w;
    var fy = 1 - (py - this.y) / this.h;
    var xReal = this.xMin + fx * (this.xMax - this.xMin);
    var yReal = this.yMin + fy * (this.yMax - this.yMin);
    return { x: xReal, y: yReal };
  };

  /**
   * Cập nhật kích thước vùng vẽ và dải giá trị
   */
  DoThiSVG.prototype.datKichThuoc = function (x, y, w, h, xMin, xMax, yMin, yMax) {
    this.x = x !== undefined ? x : this.x;
    this.y = y !== undefined ? y : this.y;
    this.w = w !== undefined ? w : this.w;
    this.h = h !== undefined ? h : this.h;
    this.xMin = xMin !== undefined ? xMin : this.xMin;
    this.xMax = xMax !== undefined ? xMax : this.xMax;
    this.yMin = yMin !== undefined ? yMin : this.yMin;
    this.yMax = yMax !== undefined ? yMax : this.yMax;
  };

  /**
   * Vẽ lại các trục toạ độ và vạch chia đẹp (1-2-5), tự né nhãn chồng lấn
   */
  DoThiSVG.prototype.veTruc = function () {
    if (!this.gTruc || typeof document === 'undefined') return;
    this.gTruc.innerHTML = '';
    this.gLuoi.innerHTML = '';

    var vachX = HLDinhDang ? HLDinhDang.vachDep(this.xMin, this.xMax, 5) : [this.xMin, this.xMax];
    var vachY = HLDinhDang ? HLDinhDang.vachDep(this.yMin, this.yMax, 5) : [this.yMin, this.yMax];

    var viTriXCu = -999;
    for (var i = 0; i < vachX.length; i++) {
      var vx = vachX[i];
      var pos = this.toDoSVG(vx, this.yMin);

      // Đường lưới dọc
      var lineX = document.createElementNS(SVG_NS, 'line');
      lineX.setAttribute('x1', pos.x);
      lineX.setAttribute('y1', this.y);
      lineX.setAttribute('x2', pos.x);
      lineX.setAttribute('y2', this.y + this.h);
      lineX.setAttribute('stroke', 'var(--hl-luoi, rgba(148, 163, 184, 0.15))');
      lineX.setAttribute('stroke-width', '1');
      this.gLuoi.appendChild(lineX);

      // Nhãn trục X: tự tránh chồng lấn nếu khoảng cách < 28px
      if (pos.x - viTriXCu >= 28 && pos.x <= this.x + this.w + 5) {
        var txtX = document.createElementNS(SVG_NS, 'text');
        txtX.setAttribute('x', pos.x);
        txtX.setAttribute('y', this.y + this.h + 18);
        txtX.setAttribute('text-anchor', 'middle');
        txtX.setAttribute('fill', 'var(--hl-chu-phu, #71717a)');
        txtX.setAttribute('font-size', '12');
        txtX.textContent = HLDinhDang ? HLDinhDang.so(vx) : String(vx);
        this.gTruc.appendChild(txtX);
        viTriXCu = pos.x;
      }
    }

    var viTriYCu = 99999;
    for (var j = 0; j < vachY.length; j++) {
      var vy = vachY[j];
      var posY = this.toDoSVG(this.xMin, vy);

      // Đường lưới ngang
      var lineY = document.createElementNS(SVG_NS, 'line');
      lineY.setAttribute('x1', this.x);
      lineY.setAttribute('y1', posY.y);
      lineY.setAttribute('x2', this.x + this.w);
      lineY.setAttribute('y2', posY.y);
      lineY.setAttribute('stroke', 'var(--hl-luoi, rgba(148, 163, 184, 0.15))');
      lineY.setAttribute('stroke-width', '1');
      this.gLuoi.appendChild(lineY);

      // Nhãn trục Y: tránh chồng lấn nếu khoảng cách < 20px
      if (viTriYCu - posY.y >= 20 && posY.y >= this.y - 5) {
        var txtY = document.createElementNS(SVG_NS, 'text');
        txtY.setAttribute('x', this.x - 8);
        txtY.setAttribute('y', posY.y + 4);
        txtY.setAttribute('text-anchor', 'end');
        txtY.setAttribute('fill', 'var(--hl-chu-phu, #71717a)');
        txtY.setAttribute('font-size', '12');
        txtY.textContent = HLDinhDang ? HLDinhDang.so(vy) : String(vy);
        this.gTruc.appendChild(txtY);
        viTriYCu = posY.y;
      }
    }

    // Tên trục X
    var tenX = document.createElementNS(SVG_NS, 'text');
    tenX.setAttribute('x', this.x + this.w);
    tenX.setAttribute('y', this.y + this.h + 34);
    tenX.setAttribute('text-anchor', 'end');
    tenX.setAttribute('fill', 'var(--hl-chu, #09090b)');
    tenX.setAttribute('font-size', '13');
    tenX.setAttribute('font-weight', '600');
    tenX.textContent = this.tenTrucX;
    this.gTruc.appendChild(tenX);

    // Tên trục Y
    var tenY = document.createElementNS(SVG_NS, 'text');
    tenY.setAttribute('x', this.x);
    tenY.setAttribute('y', this.y - 12);
    tenY.setAttribute('text-anchor', 'start');
    tenY.setAttribute('fill', 'var(--hl-chu, #09090b)');
    tenY.setAttribute('font-size', '13');
    tenY.setAttribute('font-weight', '600');
    tenY.textContent = this.tenTrucY;
    this.gTruc.appendChild(tenY);
  };

  /**
   * Cập nhật đường đồ thị chính và vùng tô
   */
  DoThiSVG.prototype.capNhatDuong = function (dStr, dVungTo) {
    if (this.gDuong && dStr) {
      this.gDuong.setAttribute('d', dStr);
    }
    if (this.gVungTo && dVungTo) {
      this.gVungTo.setAttribute('d', dVungTo);
    }
  };

  /**
   * Cập nhật vị trí chấm trạng thái
   */
  DoThiSVG.prototype.capNhatCham = function (xReal, yReal) {
    if (!this.chamHienTai) return;
    var pos = this.toDoSVG(xReal, yReal);
    this.chamHienTai.setAttribute('cx', pos.x.toFixed(1));
    this.chamHienTai.setAttribute('cy', pos.y.toFixed(1));
  };

  return {
    DoThiSVG: DoThiSVG
  };
});
