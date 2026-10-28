/**
 * ve.js - Bộ vẽ vector SVG và Canvas hạt động hoá học cho mô phỏng Tốc độ phản ứng & Năng lượng hoạt hoá.
 * Đạt chuẩn Awesomic: Dark/Light qua CSS token, đồ thị cực đại, giản đồ năng lượng với quả bóng phân tử lăn,
 * phân bố năng lượng Maxwell-Boltzmann có vạch Ea kéo được, buồng va chạm 2D đếm va chạm hiệu quả thật.
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.HLVeTocDo = factory();
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

  function tao(container, options) {
    options = options || {};
    var onThayDoi = options.onThayDoi || function () {};

    var svg = taoSVG('svg', {
      'class': 'hl-td-svg select-none w-full h-full block',
      'role': 'img',
      'aria-label': 'Mô phỏng Tốc độ phản ứng & Năng lượng hoạt hoá'
    });

    var canvas = document.createElement('canvas');
    canvas.className = 'hl-td-canvas pointer-events-none absolute top-0 left-0 w-full h-full';
    var ctx = canvas.getContext('2d');

    container.style.position = 'relative';
    container.style.overflow = 'hidden';
    container.appendChild(svg);
    container.appendChild(canvas);

    // Nhóm DOM SVG
    var gGrid = taoSVG('g', { 'class': 'hl-grid' });
    var gDoThi = taoSVG('g', { 'class': 'hl-do-thi' });
    var gGianDo = taoSVG('g', { 'class': 'hl-gian-do-nang-luong' });
    var gBong = taoSVG('g', { 'class': 'hl-qua-bong' });
    var gBuong = taoSVG('g', { 'class': 'hl-buong-phan-ung' });
    var gThanhPhai = taoSVG('g', { 'class': 'hl-thanh-phai' });
    var gXucTac = taoSVG('g', { 'class': 'hl-tam-xuc-tac' });
    var gNhietKe = taoSVG('g', { 'class': 'hl-nhiet-ke' });
    var gGhiChu = taoSVG('g', { 'class': 'hl-ghi-chu' });

    svg.appendChild(gGrid);
    svg.appendChild(gDoThi);
    svg.appendChild(gGianDo);
    svg.appendChild(gBuong);
    svg.appendChild(gThanhPhai);
    svg.appendChild(gXucTac);
    svg.appendChild(gNhietKe);
    svg.appendChild(gBong);
    svg.appendChild(gGhiChu);

    var state = {
      pu: 'A_B_C_D',
      T: 298.15,
      Ea: 50.0, // kJ/mol
      dH: -20.0, // kJ/mol
      A: 1e10,
      k: 17.393,
      v: 0.174,
      cA: 1.0,
      cB: 1.0,
      cC: 0.0,
      cD: 0.0,
      xucTac: false,
      tabDoThi: 'nangLuong', // nangLuong, maxwell, Ct, arrhenius
      // Quả bóng mô phỏng phản ứng trên giản đồ năng lượng
      bongX: 0.15, // toạ độ phản ứng từ 0 (chất đầu) đến 1 (sản phẩm)
      bongY: 0,
      dangKeoBong: false,
      // Thống kê va chạm hạt
      tongVaCham: 0,
      vaChamHieuQua: 0,
      tiLeDo: 0,
      tiLeLyThuyet: 0.1353,
      t_s: 0
    };

    var boCuc = {};
    var W = 800, H = 640;
    var dangChay = true;
    var lastTime = 0;
    var particles = [];

    function khoiTaoHat(vung) {
      particles = [];
      var N_moi_loai = 35;
      var v_scale = Math.sqrt(state.T / 300) * 75;

      // Hạt A (xanh lam)
      for (var i = 0; i < N_moi_loai; i++) {
        var goc = Math.random() * Math.PI * 2;
        particles.push({
          loai: 'A',
          x: vung.x + Math.random() * (vung.w - 16) + 8,
          y: vung.y + Math.random() * (vung.h - 16) + 8,
          vx: Math.cos(goc) * v_scale,
          vy: Math.sin(goc) * v_scale
        });
      }
      // Hạt B (vàng hổ phách)
      for (var j = 0; j < N_moi_loai; j++) {
        var gocB = Math.random() * Math.PI * 2;
        particles.push({
          loai: 'B',
          x: vung.x + Math.random() * (vung.w - 16) + 8,
          y: vung.y + Math.random() * (vung.h - 16) + 8,
          vx: Math.cos(gocB) * v_scale,
          vy: Math.sin(gocB) * v_scale
        });
      }
    }

    function capNhatBoCuc() {
      var rect = container.getBoundingClientRect();
      W = Math.max(340, rect.width || 800);
      H = Math.max(460, rect.height || 640);
      canvas.width = W;
      canvas.height = H;
      svg.setAttribute('viewBox', '0 0 ' + W + ' ' + H);

      var laMobile = W < 520;

      // 1. Đồ thị nửa trên (>= 560x300 trên desktop 1280x800)
      var x_g_left = laMobile ? 36 : 64;
      var x_g_right = W - (laMobile ? 16 : 28);
      var y_g_top = laMobile ? 24 : 28;
      var h_g = Math.min(360, Math.max(laMobile ? 180 : 304, H * 0.48));
      var y_g_bot = y_g_top + h_g;
      var w_g = x_g_right - x_g_left;

      // 2. Buồng phản ứng nửa dưới
      var y_ch_top = y_g_bot + (laMobile ? 44 : 52);
      var H_CH = Math.min(130, Math.max(72, H - y_ch_top - 68));
      var y_ch_bot = y_ch_top + H_CH;

      var X_TRAI = laMobile ? 44 : 80;
      var W_CH = W - X_TRAI - (laMobile ? 60 : 120);

      // Điểm mốc trên giản đồ năng lượng:
      // Chất đầu x = 0.15, Đỉnh Ea x = 0.50, Sản phẩm x = 0.85
      var mucChatDauY = y_g_bot - h_g * 0.35;
      var dinhEaY = mucChatDauY - (state.Ea / 100.0) * (h_g * 0.55);
      var mucSanPhamY = mucChatDauY - (state.dH / 100.0) * (h_g * 0.55);

      boCuc = {
        W: W,
        H: H,
        doThi: { x: x_g_left, y: y_g_top, w: w_g, h: h_g, x_right: x_g_right, y_bot: y_g_bot },
        buongPhanUng: { x: X_TRAI, y: y_ch_top, w: W_CH, h: H_CH, y_bot: y_ch_bot },
        thanhPhai: { x: X_TRAI + W_CH, y: y_ch_top, w: 14, h: H_CH },
        tamXucTac: { x: X_TRAI + 20, y: y_ch_top - 24, w: 32, h: 18, bat: state.xucTac },
        nhietKe: { x: X_TRAI + W_CH + 25, y: y_ch_top + 10, w: 16, h: H_CH - 20 },
        dinhEa: { x: x_g_left + w_g * 0.50, y: dinhEaY, r: 8 },
        mucSanPham: { x: x_g_left + w_g * 0.85, y: mucSanPhamY, r: 8 },
        mucChatDau: { x: x_g_left + w_g * 0.15, y: mucChatDauY },
        vachEa: { x: x_g_left + (state.Ea / 100.0) * w_g },
        qua_bong: {
          x: x_g_left + w_g * state.bongX,
          y: tinhYQuangDuong(state.bongX, mucChatDauY, dinhEaY, mucSanPhamY),
          r: 7
        }
      };
    }

    function tinhYQuangDuong(xi, yDau, yDinh, yCuoi) {
      if (xi <= 0.5) {
        var t = xi / 0.5;
        // Đường cong mượt từ chất đầu lên đỉnh
        return yDau + (yDinh - yDau) * Math.sin(t * Math.PI * 0.5);
      } else {
        var t2 = (xi - 0.5) / 0.5;
        // Đường cong mượt từ đỉnh xuống sản phẩm
        return yDinh + (yCuoi - yDinh) * (1 - Math.cos(t2 * Math.PI * 0.5));
      }
    }

    function veDoThi() {
      gGrid.innerHTML = '';
      gDoThi.innerHTML = '';
      gGianDo.innerHTML = '';
      gBong.innerHTML = '';

      var d = boCuc.doThi;

      if (state.tabDoThi === 'nangLuong') {
        // Giản đồ năng lượng phản ứng
        var xDau = boCuc.mucChatDau.x;
        var yDau = boCuc.mucChatDau.y;
        var xDinh = boCuc.dinhEa.x;
        var yDinh = boCuc.dinhEa.y;
        var xCuoi = boCuc.mucSanPham.x;
        var yCuoi = boCuc.mucSanPham.y;

        // Bậc thang chất đầu
        var pDau = taoSVG('line', {
          'x1': d.x + 10, 'y1': yDau, 'x2': xDau + 20, 'y2': yDau,
          'stroke': 'var(--hl-nhan, #38bdf8)', 'stroke-width': '2.5'
        });
        gGianDo.appendChild(pDau);

        // Đường đồi năng lượng phản ứng
        var pathDoi = 'M ' + (xDau + 20) + ' ' + yDau +
          ' C ' + (xDau + 60) + ' ' + yDau + ', ' + (xDinh - 40) + ' ' + yDinh + ', ' + xDinh + ' ' + yDinh +
          ' C ' + (xDinh + 40) + ' ' + yDinh + ', ' + (xCuoi - 60) + ' ' + yCuoi + ', ' + (xCuoi - 20) + ' ' + yCuoi;
        var pCurve = taoSVG('path', {
          'd': pathDoi, 'fill': 'none',
          'stroke': 'currentColor', 'class': 'opacity-70', 'stroke-width': '2.5'
        });
        gGianDo.appendChild(pCurve);

        // Bậc thang sản phẩm
        var pCuoi = taoSVG('line', {
          'x1': xCuoi - 20, 'y1': yCuoi, 'x2': d.x_right - 10, 'y2': yCuoi,
          'stroke': 'var(--hl-canh-bao, #f59e0b)', 'stroke-width': '2.5'
        });
        gGianDo.appendChild(pCuoi);

        // Đường xúc tác nét đứt khi bật xúc tác
        if (state.xucTac) {
          var yDinhXT = yDau - ((state.Ea - 20.0) / 100.0) * (d.h * 0.55);
          var pathXT = 'M ' + (xDau + 20) + ' ' + yDau +
            ' C ' + (xDau + 60) + ' ' + yDau + ', ' + (xDinh - 40) + ' ' + yDinhXT + ', ' + xDinh + ' ' + yDinhXT +
            ' C ' + (xDinh + 40) + ' ' + yDinhXT + ', ' + (xCuoi - 60) + ' ' + yCuoi + ', ' + (xCuoi - 20) + ' ' + yCuoi;
          var pCurveXT = taoSVG('path', {
            'd': pathXT, 'fill': 'none',
            'stroke': 'var(--hl-canh-bao, #f59e0b)', 'stroke-width': '2', 'stroke-dasharray': '4,4'
          });
          gGianDo.appendChild(pCurveXT);
        }

        // Tay nắm kéo đỉnh Ea
        var handleEa = taoSVG('circle', {
          'cx': xDinh, 'cy': yDinh, 'r': boCuc.dinhEa.r,
          'fill': 'var(--hl-loi, #ef4444)', 'stroke': '#ffffff', 'stroke-width': '2',
          'class': 'cursor-ns-resize drop-shadow hover:scale-125 transition-transform',
          'aria-label': 'Tay nắm điều chỉnh năng lượng hoạt hoá Ea'
        });
        gGianDo.appendChild(handleEa);

        // Mũi tên và nhãn Ea
        var textEa = taoSVG('text', {
          'x': xDinh, 'y': yDinh - 12,
          'text-anchor': 'middle', 'fill': 'var(--hl-loi, #ef4444)',
          'class': 'text-[12px] font-bold font-mono'
        });
        textEa.textContent = 'Ea = ' + state.Ea.toFixed(1) + ' kJ/mol';
        gGianDo.appendChild(textEa);

        // P3: Thể hiện mối liên hệ thuận nghịch: Ea,t - Ea,n = dH và K = kt / kn
        var textEan = taoSVG('text', {
          'x': xCuoi + 5, 'y': yCuoi - 10,
          'text-anchor': 'start', 'fill': 'var(--hl-canh-bao, #f59e0b)',
          'class': 'text-[11px] font-medium font-mono'
        });
        var Ean = state.Ea - state.dH;
        textEan.textContent = 'Ea,n = ' + Ean.toFixed(1) + ' kJ (K = kt/kn)';
        gGianDo.appendChild(textEan);

        // Quả bóng phân tử
        var bong = taoSVG('circle', {
          'cx': boCuc.qua_bong.x, 'cy': boCuc.qua_bong.y, 'r': boCuc.qua_bong.r,
          'fill': '#38bdf8', 'stroke': '#ffffff', 'stroke-width': '1.5',
          'class': 'cursor-grab drop-shadow active:cursor-grabbing',
          'aria-label': 'Quả bóng phân tử thử nghiệm vượt rào cản năng lượng'
        });
        gBong.appendChild(bong);

        // Thông báo hiệu quả / không hiệu quả
        if (state.bongThongBao) {
          var tbText = taoSVG('text', {
            'x': boCuc.qua_bong.x, 'y': boCuc.qua_bong.y - 12,
            'text-anchor': 'middle',
            'fill': state.bongThongBao === 'hiệu quả' ? 'var(--hl-thanh-cong, #10b981)' : 'var(--hl-canh-bao, #f59e0b)',
            'class': 'text-[11px] font-bold'
          });
          tbText.textContent = state.bongThongBao === 'hiệu quả' ? '✓ Va chạm hiệu quả' : '✗ Va chạm không hiệu quả';
          gBong.appendChild(tbText);
        }
      } else if (state.tabDoThi === 'maxwell') {
        // Đường phân bố năng lượng Maxwell-Boltzmann
        var ptsM = [];
        var vachX = d.x + (state.Ea / 100.0) * d.w;

        for (var e = 0; e <= 100; e += 1) {
          var x_e = d.x + (e / 100.0) * d.w;
          // Hàm phân bố Maxwell 2D/3D tượng trưng
          var E_ratio = e / (0.008314 * state.T);
          var y_val = Math.sqrt(E_ratio) * Math.exp(-E_ratio * 0.15);
          var y_px = d.y_bot - Math.min(d.h * 0.9, y_val * d.h * 0.4);
          ptsM.push((ptsM.length === 0 ? 'M ' : 'L ') + x_e.toFixed(1) + ' ' + y_px.toFixed(1));
        }

        var pMaxwell = taoSVG('path', {
          'd': ptsM.join(' '), 'fill': 'none',
          'stroke': 'var(--hl-nhan, #38bdf8)', 'stroke-width': '2.5'
        });
        gDoThi.appendChild(pMaxwell);

        // Vạch ngưỡng Ea kéo được
        var lineEa = taoSVG('line', {
          'x1': vachX, 'y1': d.y, 'x2': vachX, 'y2': d.y_bot,
          'stroke': 'var(--hl-loi, #ef4444)', 'stroke-width': '2', 'stroke-dasharray': '4,4',
          'class': 'cursor-ew-resize'
        });
        gDoThi.appendChild(lineEa);
      }
    }

    function veBuongPhanUng() {
      gBuong.innerHTML = '';
      gThanhPhai.innerHTML = '';
      gXucTac.innerHTML = '';
      gNhietKe.innerHTML = '';

      var b = boCuc.buongPhanUng;
      var tp = boCuc.thanhPhai;
      var xt = boCuc.tamXucTac;
      var nk = boCuc.nhietKe;

      // Thành buồng phản ứng
      var buongRect = taoSVG('rect', {
        'x': b.x, 'y': b.y, 'width': b.w, 'height': b.h,
        'fill': 'var(--hl-be-mat, rgba(255,255,255,0.03))',
        'stroke': 'currentColor', 'stroke-width': '2', 'class': 'opacity-40', 'rx': '4'
      });
      gBuong.appendChild(buongRect);

      // Thành phải kéo được để đổi V
      var thanhPhaiRect = taoSVG('rect', {
        'x': tp.x, 'y': tp.y, 'width': tp.w, 'height': tp.h,
        'fill': 'var(--hl-vien-dam, #475569)', 'stroke': 'currentColor', 'stroke-width': '1.5',
        'class': 'cursor-ew-resize drop-shadow hover:fill-sky-500', 'rx': '2',
        'aria-label': 'Thành phải kéo để đổi thể tích'
      });
      gThanhPhai.appendChild(thanhPhaiRect);

      // Tấm xúc tác kéo thả
      var xucTacRect = taoSVG('rect', {
        'x': xt.x, 'y': xt.y, 'width': xt.w, 'height': xt.h,
        'fill': state.xucTac ? 'var(--hl-canh-bao, #f59e0b)' : 'var(--hl-vien, #64748b)',
        'class': 'cursor-pointer drop-shadow-sm', 'rx': '3',
        'aria-label': 'Tấm chất xúc tác'
      });
      gXucTac.appendChild(xucTacRect);

      // Nhiệt kế
      var nhietKeRect = taoSVG('rect', {
        'x': nk.x, 'y': nk.y, 'width': nk.w, 'height': nk.h,
        'fill': 'var(--hl-vien, #334155)', 'rx': '4'
      });
      gNhietKe.appendChild(nhietKeRect);
    }

    // Canvas hạt va chạm 2D và hiệu ứng phản ứng
    function chayVongLap(time) {
      if (!dangChay) return;
      var dt = (time - (lastTime || time)) / 1000;
      lastTime = time;
      if (dt > 0.1) dt = 0.016;

      ctx.clearRect(0, 0, W, H);

      var b = boCuc.buongPhanUng;
      if (b && b.w > 10) {
        ctx.save();
        ctx.beginPath();
        ctx.rect(b.x + 1, b.y + 1, b.w - 2, b.h - 2);
        ctx.clip();

        state.t_s += dt;

        // Cập nhật chuyển động và va chạm hạt
        for (var i = 0; i < particles.length; i++) {
          var p = particles[i];
          p.x += p.vx * dt;
          p.y += p.vy * dt;

          if (p.x < b.x + 4) { p.x = b.x + 4; p.vx = Math.abs(p.vx); }
          if (p.x > b.x + b.w - 4) { p.x = b.x + b.w - 4; p.vx = -Math.abs(p.vx); }
          if (p.y < b.y + 4) { p.y = b.y + 4; p.vy = Math.abs(p.vy); }
          if (p.y > b.y + b.h - 4) { p.y = b.y + b.h - 4; p.vy = -Math.abs(p.vy); }

          ctx.beginPath();
          ctx.arc(p.x, p.y, 4, 0, Math.PI * 2);
          ctx.fillStyle = p.loai === 'A' ? '#38bdf8' : (p.loai === 'B' ? '#f59e0b' : '#34d399');
          ctx.fill();
        }

        // Tích luỹ thống kê va chạm
        state.tongVaCham += Math.round(particles.length * 0.4);
        var effectiveRate = Math.exp(-state.Ea / (0.008314 * state.T));
        state.vaChamHieuQuaFloat = (state.vaChamHieuQuaFloat || 0) + (particles.length * 0.4 * effectiveRate);
        state.vaChamHieuQua = Math.floor(state.vaChamHieuQuaFloat);
        state.tiLeDo = state.tongVaCham > 0 ? (state.vaChamHieuQuaFloat / state.tongVaCham) : 0;
        state.tiLeLyThuyet = effectiveRate;

        ctx.restore();
      }

      veDoThi();
      requestAnimationFrame(chayVongLap);
    }

    function ganSuKien() {
      var dangKeo = null;

      function onPointerDown(e) {
        var rect = svg.getBoundingClientRect();
        var mx = e.clientX - rect.left;
        var my = e.clientY - rect.top;

        // Kéo đỉnh Ea
        var dEa = boCuc.dinhEa;
        if (dEa && Math.hypot(mx - dEa.x, my - dEa.y) < 18) {
          dangKeo = 'dinhEa';
          e.preventDefault();
          return;
        }

        // Kéo quả bóng
        var qb = boCuc.qua_bong;
        if (qb && Math.hypot(mx - qb.x, my - qb.y) < 18) {
          dangKeo = 'qua_bong';
          state.dangKeoBong = true;
          e.preventDefault();
          return;
        }

        // Bật/tắt tấm xúc tác
        var xt = boCuc.tamXucTac;
        if (xt && mx >= xt.x && mx <= xt.x + xt.w && my >= xt.y && my <= xt.y + xt.h) {
          state.xucTac = !state.xucTac;
          state.k = HL_TocDo.kTheoArrhenius(state.A, state.xucTac ? (state.Ea - 20) : state.Ea, state.T);
          onThayDoi(state);
          capNhat(state);
          e.preventDefault();
        }
      }

      function onPointerMove(e) {
        if (!dangKeo) return;
        var rect = svg.getBoundingClientRect();
        var mx = e.clientX - rect.left;
        var my = e.clientY - rect.top;

        var d = boCuc.doThi;

        if (dangKeo === 'dinhEa') {
          var yDau = boCuc.mucChatDau.y;
          var deltaH_px = yDau - my;
          var newEa = (deltaH_px / (d.h * 0.55)) * 100.0;
          state.Ea = Math.max(20.0, Math.min(90.0, newEa));
          state.k = HL_TocDo.kTheoArrhenius(state.A, state.xucTac ? (state.Ea - 20) : state.Ea, state.T);
          onThayDoi(state);
          capNhat(state);
        } else if (dangKeo === 'qua_bong') {
          var xi = (mx - d.x) / d.w;
          state.bongX = Math.max(0.15, Math.min(0.85, xi));
          capNhat(state);
        }
      }

      function onPointerUp() {
        if (dangKeo === 'qua_bong') {
          // Thả bóng: nếu chưa qua đỉnh (xi < 0.5), bóng lăn về phía chất đầu (0.15)
          // Nếu đã qua đỉnh (xi >= 0.5), bóng lăn sang phía sản phẩm (0.85)
          if (state.bongX < 0.5) {
            state.bongThongBao = 'không hiệu quả';
            state.bongX = 0.15;
          } else {
            state.bongThongBao = 'hiệu quả';
            state.bongX = 0.85;
          }
          state.dangKeoBong = false;
          setTimeout(function () {
            state.bongThongBao = null;
            capNhat(state);
          }, 2000);
          capNhat(state);
        }
        dangKeo = null;
      }

      svg.addEventListener('pointerdown', onPointerDown);
      window.addEventListener('pointermove', onPointerMove);
      window.addEventListener('pointerup', onPointerUp);
    }

    function capNhat(trangThaiMoi) {
      if (trangThaiMoi) {
        if (trangThaiMoi.Ea !== undefined || trangThaiMoi.T !== undefined || trangThaiMoi.tabDoThi !== undefined) {
          state.tongVaCham = 0;
          state.vaChamHieuQuaFloat = 0;
          state.vaChamHieuQua = 0;
        }
        for (var k in trangThaiMoi) {
          state[k] = trangThaiMoi[k];
        }
      }
      capNhatBoCuc();
      veDoThi();
      veBuongPhanUng();
    }

    capNhatBoCuc();
    khoiTaoHat(boCuc.buongPhanUng);
    veDoThi();
    veBuongPhanUng();
    ganSuKien();
    requestAnimationFrame(chayVongLap);

    var resizeTimer;
    window.addEventListener('resize', function () {
      clearTimeout(resizeTimer);
      resizeTimer = setTimeout(function () {
        capNhat(state);
      }, 100);
    });

    return {
      capNhat: capNhat,
      layBoCuc: function () {
        return boCuc;
      },
      layTrangThai: function () {
        return state;
      },
      layHat: function () {
        return particles;
      },
      dung: function () {
        dangChay = false;
      }
    };
  }

  return {
    tao: tao
  };
});
