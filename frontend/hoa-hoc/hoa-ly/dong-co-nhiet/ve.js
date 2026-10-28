/**
 * ve.js - Vẽ SVG mô phỏng 2D Động cơ nhiệt 4 kỳ và đồ thị p–V, T–s, p–φ.
 * Hỗ trợ Phễu phóng to (Zooming Funnel) cho đồ thị kích thước cực đại,
 * và chế độ "Thẳng hàng như video" tuỳ chọn.
 * Toàn bộ màu sắc đọc từ CSS token (Awesomic dark/light), không viết cứng mã hex.
 * Tạo DOM 1 lần duy nhất, cập nhật bằng setAttribute (không gán innerHTML mỗi khung hình).
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.HLVe = factory();
  }
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';

  var SVG_NS = 'http://www.w3.org/2000/svg';

  function taoSVG(ten, attrs) {
    var el = document.createElementNS(SVG_NS, ten);
    if (attrs) {
      for (var k in attrs) {
        el.setAttribute(k, attrs[k]);
      }
    }
    return el;
  }

  /**
   * Tính toán tỉ lệ toạ độ động thích ứng theo kích thước thật (W, H)
   */
  function tinhTiLe(ts, tinhND, hhCC, W, H, canhThang) {
    W = Math.max(340, W || 811);
    H = Math.max(460, H || 640);
    var laMobile = W < 500;

    var Vc = hhCC.Vc_cm3;
    var V1 = hhCC.V1_cm3;
    var Vd = hhCC.Vd_cm3;

    var p_max = Math.max(100, (tinhND.p3 / 100) * 1.08); // bar
    var V_max = V1 * (laMobile ? 1.015 : 1.06);

    // 1. Vùng đồ thị
    var x_g_left = laMobile ? 32 : 62;
    var x_g_right = W - (laMobile ? 10 : 26);
    var y_g_top = laMobile ? 26 : 32;
    var y_g_bot = Math.round(H * (laMobile ? 0.48 : 0.58));
    // Đảm bảo chiều cao đồ thị đủ lớn
    if (!laMobile && (y_g_bot - y_g_top) < 325) {
      y_g_bot = Math.min(H - 220, y_g_top + 335);
    }
    var w_g = x_g_right - x_g_left;
    var h_g = y_g_bot - y_g_top;

    var scale_g_x = w_g / V_max;
    var scale_g_y = h_g / p_max;

    // 2. Vùng cơ cấu xi-lanh & bánh đà
    var y_tam = Math.round(y_g_bot + (H - y_g_bot) * 0.54);
    var H_CYL = Math.min(84, Math.max(54, (H - y_g_bot) * 0.44));
    var y_cyl_top = y_tam - H_CYL / 2;
    var y_cyl_bot = y_tam + H_CYL / 2;

    var X_NAP = Math.round(laMobile ? 24 : Math.min(75, W * 0.08));
    var a_px = Math.round(Math.min(75, Math.max(30, (W - X_NAP - 150) / 7.2)));
    var S_px = a_px * 2;
    var scale_piston = S_px / Vd;

    if (canhThang) {
      // Chế độ thẳng hàng như video: trục V đồ thị dóng thẳng 1:1 với pít-tông
      x_g_left = X_NAP;
      scale_g_x = scale_piston;
      w_g = V_max * scale_g_x;
      x_g_right = x_g_left + w_g;
    }

    var L_px = Math.round(a_px * (2 * ts.L / ts.S));
    var chieuDaiCanPiston = Math.round(Math.max(16, a_px * 0.38));
    var x_TDC = X_NAP + Vc * scale_piston;
    var x_BDC = X_NAP + V1 * scale_piston;
    var x_wrist_TDC = x_TDC + chieuDaiCanPiston;
    var x_tam = Math.round(x_wrist_TDC + L_px + a_px);
    var r_banh_da = Math.round(a_px * 1.2);

    return {
      W: W, H: H,
      laMobile: laMobile,
      canhThang: !!canhThang,
      // Đồ thị
      x_g_left: x_g_left,
      x_g_right: x_g_right,
      y_g_top: y_g_top,
      y_g_bot: y_g_bot,
      w_g: w_g,
      h_g: h_g,
      scale_g_x: scale_g_x,
      scale_g_y: scale_g_y,
      p_max: p_max,
      V_max: V_max,
      // Cơ cấu
      X_NAP: X_NAP,
      x_TDC: x_TDC,
      x_BDC: x_BDC,
      scale_piston: scale_piston,
      H_CYL: H_CYL,
      y_cyl_top: y_cyl_top,
      y_cyl_bot: y_cyl_bot,
      y_tam: y_tam,
      a_px: a_px,
      S_px: S_px,
      L_px: L_px,
      x_tam: x_tam,
      r_banh_da: r_banh_da,
      chieuDaiCanPiston: chieuDaiCanPiston,
      // Hàm quy đổi
      xGraph: function (V) { return x_g_left + V * scale_g_x; },
      yGraph: function (p, laLog) {
        if (laLog && p > 0) {
          var minLog = 0.5;
          var u = (Math.log(Math.max(minLog, p)) - Math.log(minLog)) / (Math.log(p_max) - Math.log(minLog));
          return y_g_bot - u * h_g;
        }
        return y_g_bot - (p / p_max) * h_g;
      },
      xPiston: function (V) { return X_NAP + V * scale_piston; },
      V_tu_piston: function (px) { return Math.max(0, (px - X_NAP) / scale_piston); },
      V_tu_graph: function (px) { return Math.max(0, (px - x_g_left) / scale_g_x); },
      p_tu_graph: function (py) { return Math.max(0, ((y_g_bot - py) / h_g) * p_max); }
    };
  }

  /**
   * Khởi tạo cây DOM SVG một lần duy nhất
   */
  function khoiTaoSVG(svgEl) {
    svgEl.innerHTML = '';

    // 1. Defs
    var defs = taoSVG('defs');
    defs.innerHTML =
      '<linearGradient id="hl-grad-piston" x1="0" y1="0" x2="0" y2="1">' +
        '<stop offset="0%" stop-color="var(--hl-piston, #94a3b8)"/>' +
        '<stop offset="50%" stop-color="var(--hl-banh-da, #cbd5e1)"/>' +
        '<stop offset="100%" stop-color="var(--hl-thanh-truyen, #64748b)"/>' +
      '</linearGradient>' +
      '<linearGradient id="hl-grad-rod" x1="0" y1="0" x2="1" y2="0">' +
        '<stop offset="0%" stop-color="var(--hl-thanh-truyen, #64748b)"/>' +
        '<stop offset="50%" stop-color="var(--hl-piston, #94a3b8)"/>' +
        '<stop offset="100%" stop-color="var(--hl-thanh-truyen, #475569)"/>' +
      '</linearGradient>' +
      '<linearGradient id="hl-grad-flywheel" x1="0" y1="0" x2="1" y2="1">' +
        '<stop offset="0%" stop-color="var(--hl-banh-da, #cbd5e1)"/>' +
        '<stop offset="50%" stop-color="var(--hl-piston, #94a3b8)"/>' +
        '<stop offset="100%" stop-color="var(--hl-thanh-truyen, #64748b)"/>' +
      '</linearGradient>' +
      '<filter id="hl-glow-bugi" x="-50%" y="-50%" width="200%" height="200%">' +
        '<feGaussianBlur in="SourceGraphic" stdDeviation="4" result="blur"/>' +
        '<feMerge><feMergeNode in="blur"/><feMergeNode in="SourceGraphic"/></feMerge>' +
      '</filter>';
    svgEl.appendChild(defs);

    // 2. Nhóm Đồ thị
    var gDoThi = taoSVG('g', { id: 'hl-g-dothi' });
    var rectKhungDoThi = taoSVG('rect', {
      id: 'hl-rect-khung-dothi',
      fill: 'var(--giay, #ffffff)',
      stroke: 'var(--chi, #e4e4e7)',
      'stroke-width': '1',
      rx: '6'
    });
    var gLuoivaTruc = taoSVG('g', { id: 'hl-g-luoi-truc' });
    var gDienTichCong = taoSVG('path', {
      id: 'hl-path-dien-tich',
      fill: 'var(--hl-nhan, rgba(56, 189, 248, 0.15))',
      stroke: 'none'
    });
    var gBongMa = taoSVG('path', {
      id: 'hl-path-bong-ma',
      fill: 'none',
      stroke: 'var(--hl-chu-phu, #94a3b8)',
      'stroke-width': '1.5',
      'stroke-dasharray': '4 4'
    });
    var gDuongChuTrinh = taoSVG('g', { id: 'hl-g-duong-chu-trinh' });
    var gNhanDiem = taoSVG('g', { id: 'hl-g-nhan-diem' });
    var chamTrangThai = taoSVG('circle', {
      id: 'hl-dot-trang-thai',
      r: '6',
      fill: 'var(--hl-chay, #ef4444)',
      stroke: '#ffffff',
      'stroke-width': '2',
      class: 'cursor-pointer',
      tabindex: '0',
      role: 'slider'
    });

    gDoThi.appendChild(rectKhungDoThi);
    gDoThi.appendChild(gLuoivaTruc);
    gDoThi.appendChild(gDienTichCong);
    gDoThi.appendChild(gBongMa);
    gDoThi.appendChild(gDuongChuTrinh);
    gDoThi.appendChild(gNhanDiem);
    gDoThi.appendChild(chamTrangThai);
    svgEl.appendChild(gDoThi);

    // 3. Nhóm Phễu phóng to & Đường dóng nối đồ thị - pít-tông
    var gFunnel = taoSVG('g', { id: 'hl-g-funnel' });
    var funnelLeft = taoSVG('line', {
      id: 'hl-funnel-left',
      stroke: 'var(--chi, #cbd5e1)',
      'stroke-width': '1.2',
      'stroke-dasharray': '3 3',
      opacity: '0.7'
    });
    var funnelRight = taoSVG('line', {
      id: 'hl-funnel-right',
      stroke: 'var(--chi, #cbd5e1)',
      'stroke-width': '1.2',
      'stroke-dasharray': '3 3',
      opacity: '0.7'
    });
    // Đường gấp khúc trạng thái hiện tại: dot -> đáy đồ thị -> nắp thước V -> mặt pít-tông
    var pathGiong = taoSVG('path', {
      id: 'hl-path-giong-state',
      fill: 'none',
      stroke: 'var(--hl-chay, #ef4444)',
      'stroke-width': '1.5',
      'stroke-dasharray': '4 4',
      opacity: '0.85'
    });
    gFunnel.appendChild(funnelLeft);
    gFunnel.appendChild(funnelRight);
    gFunnel.appendChild(pathGiong);
    svgEl.appendChild(gFunnel);

    // 4. Nhóm Cơ cấu: Xi-lanh, Thước V, Bánh đà, Thanh truyền, Pít-tông
    var gCoCau = taoSVG('g', { id: 'hl-g-cocau' });
    var rectKhi = taoSVG('rect', { id: 'hl-rect-khi', rx: '2', ry: '2' });
    var gXiLanh = taoSVG('g', { id: 'hl-g-xilanh' });
    var gThuocV = taoSVG('g', { id: 'hl-g-thuoc-v' });
    var gVanVaBugi = taoSVG('g', { id: 'hl-g-van-bugi' });

    // Thứ tự vẽ chuẩn: Bánh đà -> Thanh truyền -> Pít-tông (thanh truyền nằm trên bánh đà)
    var gBanhDa = taoSVG('g', { id: 'hl-g-banhda', class: 'cursor-grab' });
    var gThanhTruyen = taoSVG('g', { id: 'hl-g-thanhtruyen' });
    var gPiston = taoSVG('g', { id: 'hl-g-piston', class: 'cursor-ew-resize' });
    var gNhanCoCau = taoSVG('g', { id: 'hl-g-nhan-cocau' });

    gCoCau.appendChild(rectKhi);
    gCoCau.appendChild(gXiLanh);
    gCoCau.appendChild(gThuocV);
    gCoCau.appendChild(gVanVaBugi);
    gCoCau.appendChild(gBanhDa);
    gCoCau.appendChild(gThanhTruyen);
    gCoCau.appendChild(gPiston);
    gCoCau.appendChild(gNhanCoCau);
    svgEl.appendChild(gCoCau);

    // Khởi tạo sẵn các phần tử cố định bên trong Pít-tông để chỉ cần setAttribute khi chạy
    var pistonRect = taoSVG('rect', {
      id: 'hl-piston-body',
      fill: 'url(#hl-grad-piston)',
      stroke: 'var(--hl-thanh-truyen, #475569)',
      'stroke-width': '1.2',
      rx: '2'
    });
    var pistonRing1 = taoSVG('line', { stroke: 'var(--hl-thanh-truyen, #334155)', 'stroke-width': '1.5' });
    var pistonRing2 = taoSVG('line', { stroke: 'var(--hl-thanh-truyen, #334155)', 'stroke-width': '1.5' });
    var pistonRod = taoSVG('line', {
      id: 'hl-piston-wrist-rod',
      stroke: 'url(#hl-grad-rod)',
      'stroke-width': '8',
      'stroke-linecap': 'round'
    });
    var pistonWrist = taoSVG('circle', {
      id: 'hl-piston-wrist-pin',
      r: '5.5',
      fill: 'var(--hl-banh-da, #cbd5e1)',
      stroke: 'var(--hl-thanh-truyen, #334155)',
      'stroke-width': '1.5'
    });
    gPiston.appendChild(pistonRect);
    gPiston.appendChild(pistonRing1);
    gPiston.appendChild(pistonRing2);
    gPiston.appendChild(pistonRod);
    gPiston.appendChild(pistonWrist);

    // Khởi tạo sẵn các phần tử bên trong Bánh đà
    var flywheelBody = taoSVG('circle', {
      id: 'hl-flywheel-body',
      fill: 'url(#hl-grad-flywheel)',
      stroke: 'var(--hl-thanh-truyen, #334155)',
      'stroke-width': '3'
    });
    var flywheelHoles = [];
    for (var h = 0; h < 4; h++) {
      var hole = taoSVG('circle', {
        id: 'hl-flywheel-hole-' + h,
        fill: 'var(--giay, #ffffff)',
        stroke: 'var(--hl-thanh-truyen, #64748b)',
        'stroke-width': '1'
      });
      flywheelHoles.push(hole);
    }
    var flywheelHub1 = taoSVG('circle', {
      r: '10',
      fill: 'var(--hl-thanh-truyen, #475569)',
      stroke: 'var(--hl-thanh-truyen, #1e293b)',
      'stroke-width': '2'
    });
    var flywheelHub2 = taoSVG('circle', { r: '3', fill: 'var(--hl-banh-da, #cbd5e1)' });
    var crankPin = taoSVG('circle', {
      id: 'hl-chot-khuyu',
      r: '6.5',
      fill: 'var(--hl-nhan, #f59e0b)',
      stroke: '#ffffff',
      'stroke-width': '2'
    });

    gBanhDa.appendChild(flywheelBody);
    for (var k = 0; k < 4; k++) gBanhDa.appendChild(flywheelHoles[k]);
    gBanhDa.appendChild(flywheelHub1);
    gBanhDa.appendChild(flywheelHub2);
    gBanhDa.appendChild(crankPin);

    // Khởi tạo sẵn Thanh truyền
    var rodOuter = taoSVG('line', {
      id: 'hl-rod-outer',
      stroke: 'url(#hl-grad-rod)',
      'stroke-width': '7',
      'stroke-linecap': 'round'
    });
    var rodInner = taoSVG('line', {
      id: 'hl-rod-inner',
      stroke: 'var(--hl-thanh-truyen, #475569)',
      'stroke-width': '2',
      'stroke-linecap': 'round'
    });
    gThanhTruyen.appendChild(rodOuter);
    gThanhTruyen.appendChild(rodInner);

    return {
      svg: svgEl,
      gDoThi: gDoThi,
      rectKhungDoThi: rectKhungDoThi,
      gLuoivaTruc: gLuoivaTruc,
      gDienTichCong: gDienTichCong,
      gBongMa: gBongMa,
      gDuongChuTrinh: gDuongChuTrinh,
      gNhanDiem: gNhanDiem,
      chamTrangThai: chamTrangThai,
      gFunnel: gFunnel,
      funnelLeft: funnelLeft,
      funnelRight: funnelRight,
      pathGiong: pathGiong,
      rectKhi: rectKhi,
      gXiLanh: gXiLanh,
      gThuocV: gThuocV,
      gVanVaBugi: gVanVaBugi,
      gPiston: gPiston,
      pistonRect: pistonRect,
      pistonRing1: pistonRing1,
      pistonRing2: pistonRing2,
      pistonRod: pistonRod,
      pistonWrist: pistonWrist,
      gThanhTruyen: gThanhTruyen,
      rodOuter: rodOuter,
      rodInner: rodInner,
      gBanhDa: gBanhDa,
      flywheelBody: flywheelBody,
      flywheelHoles: flywheelHoles,
      flywheelHub1: flywheelHub1,
      flywheelHub2: flywheelHub2,
      crankPin: crankPin,
      gNhanCoCau: gNhanCoCau
    };
  }

  /**
   * Vẽ phần tĩnh khi khởi tạo hoặc khi kích thước khung thay đổi
   */
  function veCoDinh(elems, ts, tinhND, hhCC, tuyChon, W, H) {
    tuyChon = tuyChon || {};
    var laLog = tuyChon.logP || false;
    var canhThang = tuyChon.canhThang || false;
    var tl = tinhTiLe(ts, tinhND, hhCC, W, H, canhThang);

    // Cập nhật viewBox = đúng kích thước pixel thật của SVG container
    elems.svg.setAttribute('viewBox', '0 0 ' + tl.W + ' ' + tl.H);

    // 1. Khung đồ thị
    elems.rectKhungDoThi.setAttribute('x', tl.x_g_left);
    elems.rectKhungDoThi.setAttribute('y', tl.y_g_top);
    elems.rectKhungDoThi.setAttribute('width', tl.w_g);
    elems.rectKhungDoThi.setAttribute('height', tl.h_g);

    // 2. Trục và Lưới đồ thị (dùng chia vạch đẹp)
    var gLT = elems.gLuoivaTruc;
    gLT.innerHTML = '';

    // Chia vạch p
    var vachY = [0, 20, 40, 60, 80, 100];
    if (tl.p_max > 110) vachY = [0, 25, 50, 75, 100, 125];
    for (var i = 0; i < vachY.length; i++) {
      var pVal = vachY[i];
      if (pVal > tl.p_max * 1.02) continue;
      var yVal = tl.yGraph(pVal, laLog);

      var lineY = taoSVG('line', {
        x1: tl.x_g_left,
        y1: yVal,
        x2: tl.x_g_right,
        y2: yVal,
        stroke: 'var(--hl-luoi, rgba(148, 163, 184, 0.2))',
        'stroke-width': '1',
        'stroke-dasharray': pVal === 0 ? 'none' : '3 3'
      });
      gLT.appendChild(lineY);

      var txtP = taoSVG('text', {
        x: tl.x_g_left - 8,
        y: yVal + 4,
        'text-anchor': 'end',
        'font-family': 'var(--font-mono, monospace)',
        'font-size': '12',
        fill: 'var(--hl-chu-phu, #71717a)'
      });
      txtP.textContent = pVal;
      gLT.appendChild(txtP);
    }

    // Chia vạch V đẹp trên đồ thị
    var vachX = [0, 100, 200, 300, 400];
    if (tl.V_max > 450) vachX.push(500);
    for (var j = 0; j < vachX.length; j++) {
      var vVal = vachX[j];
      var xVal = tl.xGraph(vVal);
      if (xVal > tl.x_g_right + 5) continue;

      var lineX = taoSVG('line', {
        x1: xVal,
        y1: tl.y_g_top,
        x2: xVal,
        y2: tl.y_g_bot,
        stroke: 'var(--hl-luoi, rgba(148, 163, 184, 0.2))',
        'stroke-width': '1',
        'stroke-dasharray': vVal === 0 ? 'none' : '3 3'
      });
      gLT.appendChild(lineX);

      var txtV = taoSVG('text', {
        x: xVal,
        y: tl.y_g_bot + 18,
        'text-anchor': 'middle',
        'font-family': 'var(--font-mono, monospace)',
        'font-size': '12',
        fill: 'var(--hl-chu-phu, #71717a)'
      });
      txtV.textContent = vVal;
      gLT.appendChild(txtV);
    }

    // Tên trục
    var tenP = taoSVG('text', {
      x: tl.x_g_left,
      y: tl.y_g_top - 10,
      'text-anchor': 'start',
      'font-size': '13',
      'font-weight': '600',
      fill: 'var(--hl-chu, #09090b)'
    });
    tenP.textContent = 'p (bar)';
    gLT.appendChild(tenP);

    var tenV = taoSVG('text', {
      x: tl.x_g_right,
      y: tl.y_g_bot + 34,
      'text-anchor': 'end',
      'font-size': '13',
      'font-weight': '600',
      fill: 'var(--hl-chu, #09090b)'
    });
    tenV.textContent = 'V (cm³)';
    gLT.appendChild(tenV);

    // 3. Đường chu trình lý tưởng 0-1-2-3-4
    var gCT = elems.gDuongChuTrinh;
    gCT.innerHTML = '';
    var gND = elems.gNhanDiem;
    gND.innerHTML = '';

    var p1 = tinhND.p1 / 100;
    var p2 = tinhND.p2 / 100;
    var p3 = tinhND.p3 / 100;
    var p4 = tinhND.p4 / 100;
    var Vc = hhCC.Vc_cm3;
    var V1 = hhCC.V1_cm3;
    var gamma = ts.gamma || 1.4;

    var toX = function (V) { return tl.xGraph(V); };
    var toY = function (p) { return tl.yGraph(p, laLog); };

    // Đoạn nạp/xả: 0 - 1
    var d01 = 'M ' + toX(Vc) + ' ' + toY(p1) + ' L ' + toX(V1) + ' ' + toY(p1);
    gCT.appendChild(taoSVG('path', { d: d01, stroke: 'var(--hl-chu, #09090b)', 'stroke-width': '2.2', fill: 'none' }));

    // Đoạn nén: 1 -> 2
    var d12 = 'M ' + toX(V1) + ' ' + toY(p1);
    for (var k = 1; k <= 30; k++) {
      var vK = V1 - (V1 - Vc) * (k / 30);
      var pK = p1 * Math.pow(V1 / vK, gamma);
      d12 += ' L ' + toX(vK) + ' ' + toY(pK);
    }
    gCT.appendChild(taoSVG('path', { d: d12, stroke: 'var(--hl-chu, #09090b)', 'stroke-width': '2.2', fill: 'none' }));

    // Đoạn cấp nhiệt: 2 -> 3
    var d23 = 'M ' + toX(Vc) + ' ' + toY(p2) + ' L ' + toX(Vc) + ' ' + toY(p3);
    gCT.appendChild(taoSVG('path', { d: d23, stroke: 'var(--hl-chay, #ef4444)', 'stroke-width': '3', fill: 'none' }));

    // Đoạn sinh công: 3 -> 4
    var d34 = 'M ' + toX(Vc) + ' ' + toY(p3);
    for (var m = 1; m <= 30; m++) {
      var vM = Vc + (V1 - Vc) * (m / 30);
      var pM = p3 * Math.pow(Vc / vM, gamma);
      d34 += ' L ' + toX(vM) + ' ' + toY(pM);
    }
    gCT.appendChild(taoSVG('path', { d: d34, stroke: 'var(--hl-chu, #09090b)', 'stroke-width': '2.2', fill: 'none' }));

    // Đoạn thải nhiệt: 4 -> 1
    var d41 = 'M ' + toX(V1) + ' ' + toY(p4) + ' L ' + toX(V1) + ' ' + toY(p1);
    gCT.appendChild(taoSVG('path', { d: d41, stroke: 'var(--hl-nhan, #0284c7)', 'stroke-width': '3', fill: 'none' }));

    // Tô diện tích công chu trình (1-2-3-4)
    var dDienTich = d12 + ' L ' + toX(Vc) + ' ' + toY(p3) + d34.replace('M ', ' L ') + ' Z';
    elems.gDienTichCong.setAttribute('d', dDienTich);

    // Điểm mốc và nhãn tự né nhau (0, 1, 2, 3, 4)
    var cacDiemMoc = [
      { id: '0', x: toX(Vc), y: toY(p1), ox: -14, oy: 14, mau: 'var(--hl-chu, #09090b)' },
      { id: '1', x: toX(V1), y: toY(p1), ox: 10, oy: 12, mau: 'var(--hl-chu, #09090b)' },
      { id: '2', x: toX(Vc), y: toY(p2), ox: -14, oy: 4, mau: 'var(--hl-chay, #ef4444)' },
      { id: '3', x: toX(Vc), y: toY(p3), ox: -14, oy: -6, mau: 'var(--hl-chay, #ef4444)' },
      { id: '4', x: toX(V1), y: toY(p4), ox: 10, oy: -6, mau: 'var(--hl-nhan, #0284c7)' }
    ];
    cacDiemMoc.forEach(function (dm) {
      var c = taoSVG('circle', {
        cx: dm.x, cy: dm.y, r: '4',
        fill: dm.mau, stroke: '#ffffff', 'stroke-width': '1.5'
      });
      var t = taoSVG('text', {
        x: dm.x + dm.ox, y: dm.y + dm.oy,
        'font-size': '13', 'font-weight': '700',
        fill: dm.mau
      });
      t.textContent = dm.id;
      gND.appendChild(c);
      gND.appendChild(t);
    });

    // 4. Phễu phóng to nối đồ thị và xi-lanh
    if (!canhThang) {
      elems.funnelLeft.setAttribute('x1', tl.x_g_left);
      elems.funnelLeft.setAttribute('y1', tl.y_g_bot);
      elems.funnelLeft.setAttribute('x2', tl.X_NAP);
      elems.funnelLeft.setAttribute('y2', tl.y_cyl_top - 12);

      elems.funnelRight.setAttribute('x1', toX(V1));
      elems.funnelRight.setAttribute('y1', tl.y_g_bot);
      elems.funnelRight.setAttribute('x2', tl.x_BDC);
      elems.funnelRight.setAttribute('y2', tl.y_cyl_top - 12);
      elems.gFunnel.style.display = '';
    } else {
      elems.gFunnel.style.display = 'none';
    }

    // 5. Thước V nhỏ trên xi-lanh (chứa nhãn Vc và V1 rõ ràng)
    var gTV = elems.gThuocV;
    gTV.innerHTML = '';
    var lineThuoc = taoSVG('line', {
      x1: tl.X_NAP, y1: tl.y_cyl_top - 12,
      x2: tl.x_BDC + 10, y2: tl.y_cyl_top - 12,
      stroke: 'var(--hl-chu-phu, #94a3b8)',
      'stroke-width': '1.2'
    });
    gTV.appendChild(lineThuoc);

    // Vạch ĐCT (Vc)
    var vachTDC = taoSVG('line', {
      x1: tl.x_TDC, y1: tl.y_cyl_top - 16,
      x2: tl.x_TDC, y2: tl.y_cyl_top - 8,
      stroke: 'var(--hl-chu, #09090b)', 'stroke-width': '1.5'
    });
    var txtTDC = taoSVG('text', {
      x: tl.x_TDC, y: tl.y_cyl_top - 20,
      'text-anchor': 'middle', 'font-size': '11', 'font-weight': '600',
      fill: 'var(--hl-chu, #09090b)'
    });
    txtTDC.textContent = 'ĐCT (' + Math.round(Vc) + ')';
    gTV.appendChild(vachTDC);
    gTV.appendChild(txtTDC);

    // Vạch ĐCD (V1)
    var vachBDC = taoSVG('line', {
      x1: tl.x_BDC, y1: tl.y_cyl_top - 16,
      x2: tl.x_BDC, y2: tl.y_cyl_top - 8,
      stroke: 'var(--hl-chu, #09090b)', 'stroke-width': '1.5'
    });
    var txtBDC = taoSVG('text', {
      x: tl.x_BDC, y: tl.y_cyl_top - 20,
      'text-anchor': 'middle', 'font-size': '11', 'font-weight': '600',
      fill: 'var(--hl-chu, #09090b)'
    });
    txtBDC.textContent = 'ĐCD (' + Math.round(V1) + ')';
    gTV.appendChild(vachBDC);
    gTV.appendChild(txtBDC);

    // 6. Thân Xi-lanh
    var gXL = elems.gXiLanh;
    gXL.innerHTML = '';
    var lLongXL = tl.x_BDC + tl.a_px * 0.8;
    var dXiLanh =
      'M ' + lLongXL + ' ' + tl.y_cyl_top +
      ' L ' + tl.X_NAP + ' ' + tl.y_cyl_top +
      ' L ' + tl.X_NAP + ' ' + tl.y_cyl_bot +
      ' L ' + lLongXL + ' ' + tl.y_cyl_bot;
    var pathXL = taoSVG('path', {
      d: dXiLanh,
      fill: 'none',
      stroke: 'var(--hl-thanh-truyen, #64748b)',
      'stroke-width': '3',
      'stroke-linecap': 'round'
    });
    gXL.appendChild(pathXL);

    // Bugi và Van nạp / xả
    var gVB = elems.gVanVaBugi;
    gVB.innerHTML = '';
    // Thân bugi ở nắp xilanh
    var bugiThan = taoSVG('rect', {
      x: tl.X_NAP - 16, y: tl.y_tam - 6,
      width: '16', height: '12',
      fill: 'var(--hl-thanh-truyen, #64748b)', rx: '2'
    });
    var bugiDienCuc = taoSVG('rect', {
      x: tl.X_NAP, y: tl.y_tam - 2,
      width: '6', height: '4',
      fill: 'var(--hl-nhan, #f59e0b)'
    });
    var bugiTiaLua = taoSVG('circle', {
      id: 'hl-bugi-tia-lua',
      cx: tl.X_NAP + 8, cy: tl.y_tam, r: '8',
      fill: 'var(--hl-nhan, #f59e0b)',
      opacity: '0',
      filter: 'url(#hl-glow-bugi)'
    });
    gVB.appendChild(bugiThan);
    gVB.appendChild(bugiDienCuc);
    gVB.appendChild(bugiTiaLua);

    // Van nạp
    var vanNap = taoSVG('g', { id: 'hl-van-nap' });
    vanNap.innerHTML =
      '<line x1="' + (tl.X_NAP + 14) + '" y1="' + (tl.y_cyl_top - 16) + '" x2="' + (tl.X_NAP + 14) + '" y2="' + tl.y_cyl_top + '" stroke="var(--hl-thanh-truyen, #64748b)" stroke-width="3"/>' +
      '<polygon points="' + (tl.X_NAP + 8) + ',' + tl.y_cyl_top + ' ' + (tl.X_NAP + 20) + ',' + tl.y_cyl_top + ' ' + (tl.X_NAP + 14) + ',' + (tl.y_cyl_top + 4) + '" fill="var(--hl-nhan, #0284c7)"/>';
    gVB.appendChild(vanNap);

    // Van xả
    var vanXa = taoSVG('g', { id: 'hl-van-xa' });
    vanXa.innerHTML =
      '<line x1="' + (tl.X_NAP + 14) + '" y1="' + (tl.y_cyl_bot + 16) + '" x2="' + (tl.X_NAP + 14) + '" y2="' + tl.y_cyl_bot + '" stroke="var(--hl-thanh-truyen, #64748b)" stroke-width="3"/>' +
      '<polygon points="' + (tl.X_NAP + 8) + ',' + tl.y_cyl_bot + ' ' + (tl.X_NAP + 20) + ',' + tl.y_cyl_bot + ' ' + (tl.X_NAP + 14) + ',' + (tl.y_cyl_bot - 4) + '" fill="var(--hl-chu-phu, #475569)"/>';
    gVB.appendChild(vanXa);

    // Nhãn mũi tên và chú thích
    var gNC = elems.gNhanCoCau;
    gNC.innerHTML =
      '<text id="hl-txt-nhan-xilanh" x="' + ((tl.x_TDC + tl.x_BDC) / 2) + '" y="' + (tl.y_cyl_bot + 36) + '" text-anchor="middle" font-size="12" font-weight="600" fill="var(--hl-chu, #09090b)">d\'Q = 0</text>' +
      '<text id="hl-txt-mui-ten" x="' + (tl.X_NAP + 24) + '" y="' + (tl.y_tam + 4) + '" text-anchor="start" font-size="16" font-weight="bold" fill="var(--hl-nhan, #0284c7)" opacity="0">→</text>';

    // Thiết lập bán kính bánh đà
    elems.flywheelBody.setAttribute('cx', tl.x_tam);
    elems.flywheelBody.setAttribute('cy', tl.y_tam);
    elems.flywheelBody.setAttribute('r', tl.r_banh_da);

    elems.flywheelHub1.setAttribute('cx', tl.x_tam);
    elems.flywheelHub1.setAttribute('cy', tl.y_tam);
    elems.flywheelHub2.setAttribute('cx', tl.x_tam);
    elems.flywheelHub2.setAttribute('cy', tl.y_tam);

    return tl;
  }

  /**
   * Cập nhật chuyển động trong từng khung hình bằng setAttribute thuần (ZERO innerHTML)
   */
  function capNhatChuyenDong(elems, st, ts, tinhND, hhCC, tuyChon, W, H) {
    tuyChon = tuyChon || {};
    var canhThang = tuyChon.canhThang || false;
    var tl = tinhTiLe(ts, tinhND, hhCC, W, H, canhThang);

    var xDot = tl.xGraph(st.V);
    var yDot = tl.yGraph(st.p, tuyChon.logP);
    var xPiston = tl.xPiston(st.V);

    // 1. Chấm đỏ trạng thái trên đồ thị
    elems.chamTrangThai.setAttribute('cx', xDot.toFixed(1));
    elems.chamTrangThai.setAttribute('cy', yDot.toFixed(1));
    elems.chamTrangThai.setAttribute('aria-valuenow', st.t.toFixed(2));

    // 2. Đường dóng phễu nối đồ thị với pít-tông
    var dGiong = 'M ' + xDot.toFixed(1) + ' ' + yDot.toFixed(1) +
                 ' L ' + xDot.toFixed(1) + ' ' + tl.y_g_bot;
    if (!canhThang) {
      dGiong += ' L ' + xPiston.toFixed(1) + ' ' + (tl.y_cyl_top - 12) +
                ' L ' + xPiston.toFixed(1) + ' ' + tl.y_cyl_top;
    } else {
      dGiong += ' L ' + xPiston.toFixed(1) + ' ' + tl.y_cyl_top;
    }
    elems.pathGiong.setAttribute('d', dGiong);

    // 3. Khối khí trong xi-lanh
    var wKhi = Math.max(0, xPiston - tl.X_NAP);
    elems.rectKhi.setAttribute('x', tl.X_NAP);
    elems.rectKhi.setAttribute('y', tl.y_cyl_top);
    elems.rectKhi.setAttribute('width', wKhi);
    elems.rectKhi.setAttribute('height', tl.H_CYL);

    // Màu khí theo nhiệt độ T
    var fT = (Math.log(Math.max(tinhND.T1, st.T)) - Math.log(tinhND.T1)) / (Math.log(tinhND.T3) - Math.log(tinhND.T1));
    fT = Math.max(0, Math.min(1, fT));
    var mauKhi = 'color-mix(in srgb, var(--hl-chay, #ef4444) ' + Math.round(fT * 100) + '%, var(--hl-khi, #38bdf8))';
    elems.rectKhi.setAttribute('fill', mauKhi);

    // 4. Pít-tông
    var wP = Math.max(12, Math.round(tl.a_px * 0.25));
    var xWrist = xPiston + tl.chieuDaiCanPiston;

    elems.pistonRect.setAttribute('x', (xPiston - wP).toString());
    elems.pistonRect.setAttribute('y', (tl.y_cyl_top + 1).toString());
    elems.pistonRect.setAttribute('width', wP.toString());
    elems.pistonRect.setAttribute('height', (tl.H_CYL - 2).toString());

    elems.pistonRing1.setAttribute('x1', (xPiston - Math.round(wP * 0.35)).toString());
    elems.pistonRing1.setAttribute('y1', (tl.y_cyl_top + 6).toString());
    elems.pistonRing1.setAttribute('x2', (xPiston - Math.round(wP * 0.35)).toString());
    elems.pistonRing1.setAttribute('y2', (tl.y_cyl_bot - 6).toString());

    elems.pistonRing2.setAttribute('x1', (xPiston - Math.round(wP * 0.70)).toString());
    elems.pistonRing2.setAttribute('y1', (tl.y_cyl_top + 6).toString());
    elems.pistonRing2.setAttribute('x2', (xPiston - Math.round(wP * 0.70)).toString());
    elems.pistonRing2.setAttribute('y2', (tl.y_cyl_bot - 6).toString());

    elems.pistonRod.setAttribute('x1', xPiston.toString());
    elems.pistonRod.setAttribute('y1', tl.y_tam.toString());
    elems.pistonRod.setAttribute('x2', xWrist.toString());
    elems.pistonRod.setAttribute('y2', tl.y_tam.toString());

    elems.pistonWrist.setAttribute('cx', xWrist.toString());
    elems.pistonWrist.setAttribute('cy', tl.y_tam.toString());

    // 5. Bánh đà & Chốt khuỷu
    var thetaDeg = ((st.phi % 360) + 360) % 360;
    var rad = (180 - thetaDeg) * (Math.PI / 180);

    var xChot = tl.x_tam + tl.a_px * Math.cos(rad);
    var yChot = tl.y_tam - tl.a_px * Math.sin(rad);

    elems.crankPin.setAttribute('cx', xChot.toFixed(1));
    elems.crankPin.setAttribute('cy', yChot.toFixed(1));

    // 4 lỗ tròn bánh đà
    for (var h = 0; h < 4; h++) {
      var loGoc = rad + (h * Math.PI / 2);
      var rDist = tl.a_px * 0.72;
      var rLo = tl.a_px * 0.26;
      var xLo = tl.x_tam + rDist * Math.cos(loGoc);
      var yLo = tl.y_tam - rDist * Math.sin(loGoc);
      elems.flywheelHoles[h].setAttribute('cx', xLo.toFixed(1));
      elems.flywheelHoles[h].setAttribute('cy', yLo.toFixed(1));
      elems.flywheelHoles[h].setAttribute('r', rLo.toFixed(1));
    }

    // 6. Thanh truyền (connecting rod)
    elems.rodOuter.setAttribute('x1', xWrist.toFixed(1));
    elems.rodOuter.setAttribute('y1', tl.y_tam.toString());
    elems.rodOuter.setAttribute('x2', xChot.toFixed(1));
    elems.rodOuter.setAttribute('y2', yChot.toFixed(1));

    elems.rodInner.setAttribute('x1', xWrist.toFixed(1));
    elems.rodInner.setAttribute('y1', tl.y_tam.toString());
    elems.rodInner.setAttribute('x2', xChot.toFixed(1));
    elems.rodInner.setAttribute('y2', yChot.toFixed(1));

    // 7. Van và Bugi
    var vanNapEl = document.getElementById('hl-van-nap');
    var vanXaEl = document.getElementById('hl-van-xa');
    var bugiTiaLua = document.getElementById('hl-bugi-tia-lua');
    var txtNhanXL = document.getElementById('hl-txt-nhan-xilanh');
    var txtMuiTen = document.getElementById('hl-txt-mui-ten');

    if (vanNapEl) {
      vanNapEl.setAttribute('transform', st.doan === 0 ? 'translate(0, 7)' : 'translate(0, 0)');
    }
    if (vanXaEl) {
      vanXaEl.setAttribute('transform', st.doan === 5 ? 'translate(0, -7)' : 'translate(0, 0)');
    }
    if (bugiTiaLua) {
      if (st.doan === 2) {
        bugiTiaLua.setAttribute('opacity', '1');
        bugiTiaLua.setAttribute('r', (6 + st.f * 6).toString());
      } else {
        bugiTiaLua.setAttribute('opacity', '0');
      }
    }
    if (txtNhanXL) {
      if (st.doan === 1 || st.doan === 3) {
        txtNhanXL.textContent = "d'Q = 0";
      } else if (st.doan === 2) {
        txtNhanXL.textContent = "d'Q > 0 (Cháy)";
      } else if (st.doan === 4) {
        txtNhanXL.textContent = "d'Q < 0 (Thải nhiệt)";
      } else if (st.doan === 0) {
        txtNhanXL.textContent = "Hút khí";
      } else if (st.doan === 5) {
        txtNhanXL.textContent = "Đẩy khí xả";
      }
    }
    if (txtMuiTen) {
      if (st.doan === 0) {
        txtMuiTen.textContent = '→ Hút';
        txtMuiTen.setAttribute('opacity', '1');
      } else if (st.doan === 5) {
        txtMuiTen.textContent = '← Xả';
        txtMuiTen.setAttribute('opacity', '1');
      } else {
        txtMuiTen.setAttribute('opacity', '0');
      }
    }
  }

  return {
    khoiTaoSVG: khoiTaoSVG,
    tinhTiLe: tinhTiLe,
    veCoDinh: veCoDinh,
    capNhatChuyenDong: capNhatChuyenDong
  };
});
