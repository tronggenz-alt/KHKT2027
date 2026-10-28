/**
 * ve.js - Bộ vẽ vector SVG và Canvas nồng độ cho mô phỏng Cân bằng & Le Chatelier.
 * Đạt chuẩn Awesomic: Dark/Light qua CSS token, đồ thị cực đại, ống tiêm pít-tông kéo ngang,
 * nền khí đổi màu tức thì theo nồng độ NO2, chậu nước đá/nóng, viên xúc tác, cột nồng độ kéo được.
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.HLVeCanBang = factory();
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
      'class': 'hl-cb-svg select-none w-full h-full block',
      'role': 'img',
      'aria-label': 'Mô phỏng Cân bằng hoá học & Nguyên lý Le Chatelier'
    });

    var canvas = document.createElement('canvas');
    canvas.className = 'hl-cb-canvas pointer-events-none absolute top-0 left-0 w-full h-full';
    var ctx = canvas.getContext('2d');

    container.style.position = 'relative';
    container.style.overflow = 'hidden';
    container.appendChild(svg);
    container.appendChild(canvas);

    // Các nhóm DOM SVG
    var gGrid = taoSVG('g', { 'class': 'hl-grid' });
    var gDoThi = taoSVG('g', { 'class': 'hl-do-thi' });
    var gTruc = taoSVG('g', { 'class': 'hl-truc' });
    var gChauNuoc = taoSVG('g', { 'class': 'hl-chau-nuoc' });
    var gOngTiem = taoSVG('g', { 'class': 'hl-ong-tiem' });
    var gPiston = taoSVG('g', { 'class': 'hl-piston' });
    var gCotNongDo = taoSVG('g', { 'class': 'hl-cot-nong-do' });
    var gXucTac = taoSVG('g', { 'class': 'hl-xuc-tac' });
    var gGhiChu = taoSVG('g', { 'class': 'hl-ghi-chu' });

    svg.appendChild(gGrid);
    svg.appendChild(gDoThi);
    svg.appendChild(gTruc);
    svg.appendChild(gChauNuoc);
    svg.appendChild(gOngTiem);
    svg.appendChild(gPiston);
    svg.appendChild(gCotNongDo);
    svg.appendChild(gXucTac);
    svg.appendChild(gGhiChu);

    var state = {
      he: 'N2O4_2NO2',
      T: 298.15,
      V: 1.0, // Lít
      cA: 0.033762, // [N2O4]
      cB: 0.012476, // [NO2]
      cA0: 0.0400,
      cB0: 0.0,
      Q: 0.00461,
      K: 0.00461,
      chieu: 'cân bằng',
      xucTac: false,
      tabDoThi: 'Ct', // Ct, vt, QK, vanTHoff, Gxi
      t_s: 0,
      lichSuC: [] // [{ t, cA, cB, vt, vn, suKien }]
    };

    var boCuc = {};
    var W = 800, H = 640;
    var dangChay = true;
    var lastTime = 0;
    var particles = [];

    function khoiTaoHat(cA, cB, vung) {
      particles = [];
      var nB = Math.min(180, Math.max(10, Math.round(cB * 3000)));
      var nA = Math.min(120, Math.max(5, Math.round(cA * 1500)));

      for (var i = 0; i < nB; i++) {
        particles.push({
          loai: 'B', // NO2 (hạt đơn nâu đỏ)
          x: vung.x + Math.random() * (vung.w - 12) + 6,
          y: vung.y + Math.random() * (vung.h - 12) + 6,
          vx: (Math.random() - 0.5) * 60,
          vy: (Math.random() - 0.5) * 60
        });
      }
      for (var j = 0; j < nA; j++) {
        particles.push({
          loai: 'A', // N2O4 (cặp hạt dính nhau)
          x: vung.x + Math.random() * (vung.w - 12) + 6,
          y: vung.y + Math.random() * (vung.h - 12) + 6,
          vx: (Math.random() - 0.5) * 45,
          vy: (Math.random() - 0.5) * 45
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

      // 1. Đồ thị nửa trên (>= 560x300 trên desktop)
      var x_g_left = laMobile ? 36 : 64;
      var x_g_right = W - (laMobile ? 16 : 28);
      var y_g_top = laMobile ? 24 : 28;
      var h_g = Math.min(360, Math.max(laMobile ? 180 : 304, H * 0.46));
      var y_g_bot = y_g_top + h_g;
      var w_g = x_g_right - x_g_left;

      // 2. Ống tiêm phản ứng nửa dưới
      var y_syr_top = y_g_bot + (laMobile ? 44 : 52);
      var H_SYR = Math.min(120, Math.max(68, H - y_syr_top - 68));
      var y_syr_bot = y_syr_top + H_SYR;

      var X_DAU_ONG = laMobile ? 44 : 80;
      var W_SYR_MAX = W - X_DAU_ONG - (laMobile ? 70 : 130);
      var scale_V_px = W_SYR_MAX / 2.0; // V tối đa 2 Lít

      var piston_x = X_DAU_ONG + Math.max(20, Math.min(W_SYR_MAX, state.V * scale_V_px));
      var w_khi = Math.max(12, piston_x - X_DAU_ONG);

      boCuc = {
        W: W,
        H: H,
        doThi: { x: x_g_left, y: y_g_top, w: w_g, h: h_g, x_right: x_g_right, y_bot: y_g_bot },
        vungKhi: { x: X_DAU_ONG, y: y_syr_top, w: w_khi, h: H_SYR, x_dau: X_DAU_ONG, y_bot: y_syr_bot },
        pitTong: { x: piston_x, y: y_syr_top, w: 16, h: H_SYR, tayNamX: piston_x + 18, tayNamY: y_syr_top + H_SYR / 2 },
        nutNhietDo: { x: X_DAU_ONG + w_khi / 2, y: y_syr_bot + 26, r: 14 },
        ongNhoGiot: { x: X_DAU_ONG + 18, y: y_syr_top - 28, r: 12 },
        vienXucTac: { x: X_DAU_ONG + w_khi - 24, y: y_syr_top - 20, r: 10, bat: state.xucTac },
        cotNongDo: { x: W - 45, y: y_syr_top, w: 20, h: H_SYR }
      };
    }

    function veDoThi() {
      gGrid.innerHTML = '';
      gDoThi.innerHTML = '';
      gTruc.innerHTML = '';

      var d = boCuc.doThi;

      // Trục toạ độ
      var trucX = taoSVG('line', {
        'x1': d.x, 'y1': d.y_bot, 'x2': d.x_right, 'y2': d.y_bot,
        'stroke': 'currentColor', 'class': 'opacity-40', 'stroke-width': '1.5'
      });
      var trucY = taoSVG('line', {
        'x1': d.x, 'y1': d.y_bot, 'x2': d.x, 'y2': d.y,
        'stroke': 'currentColor', 'class': 'opacity-40', 'stroke-width': '1.5'
      });
      gTruc.appendChild(trucX);
      gTruc.appendChild(trucY);

      var nhanX = taoSVG('text', {
        'x': d.x_right - 10, 'y': d.y_bot - 8,
        'fill': 'currentColor', 'class': 'text-[12px] font-medium opacity-80', 'text-anchor': 'end'
      });
      nhanX.textContent = state.tabDoThi === 'vanTHoff' ? '1/T (10⁻³ K⁻¹)' : 'Thời gian t (s)';
      gTruc.appendChild(nhanX);

      var nhanY = taoSVG('text', {
        'x': d.x + 10, 'y': d.y + 14,
        'fill': 'currentColor', 'class': 'text-[12px] font-medium opacity-80', 'text-anchor': 'start'
      });
      nhanY.textContent = state.tabDoThi === 'Ct' ? 'Nồng độ C (mol/L)' : (state.tabDoThi === 'vt' ? 'Tốc độ v (M/s)' : 'Q / K');
      gTruc.appendChild(nhanY);

      // Vẽ đường nồng độ C-t từ lịch sử
      if (state.tabDoThi === 'Ct' && state.lichSuC.length > 1) {
        var ptsA = [];
        var ptsB = [];
        var maxT = Math.max(10, state.lichSuC[state.lichSuC.length - 1].t);
        var maxC = 0.08;

        state.lichSuC.forEach(function (pt) {
          var px = d.x + (pt.t / maxT) * d.w;
          var pyA = d.y_bot - (pt.cA / maxC) * d.h;
          var pyB = d.y_bot - (pt.cB / maxC) * d.h;
          ptsA.push((ptsA.length === 0 ? 'M ' : 'L ') + px.toFixed(1) + ' ' + Math.max(d.y, pyA).toFixed(1));
          ptsB.push((ptsB.length === 0 ? 'M ' : 'L ') + px.toFixed(1) + ' ' + Math.max(d.y, pyB).toFixed(1));
        });

        var pathA = taoSVG('path', {
          'd': ptsA.join(' '), 'fill': 'none',
          'stroke': 'var(--hl-nhan, #38bdf8)', 'stroke-width': '2'
        });
        var pathB = taoSVG('path', {
          'd': ptsB.join(' '), 'fill': 'none',
          'stroke': 'var(--hl-canh-bao, #f59e0b)', 'stroke-width': '2'
        });
        gDoThi.appendChild(pathA);
        gDoThi.appendChild(pathB);
      } else if (state.tabDoThi === 'Gxi') {
        // P3: Tab G-xi (đường cong thế Gibbs G theo độ tiến phản ứng xi)
        var xi_eq = 0.5;
        var ptsG = [];
        for (var step = 5; step <= 95; step += 2) {
          var xi = step / 100.0;
          var dG = 1.8 * Math.pow(xi - xi_eq, 2);
          var pxG = d.x + xi * d.w;
          var pyG = d.y_bot - 20 - (0.8 - dG) * (d.h * 0.7);
          ptsG.push((ptsG.length === 0 ? 'M ' : 'L ') + pxG.toFixed(1) + ' ' + pyG.toFixed(1));
        }

        var pathG = taoSVG('path', {
          'd': ptsG.join(' '), 'fill': 'none',
          'stroke': 'var(--hl-nhan, #38bdf8)', 'stroke-width': '2.5'
        });
        gDoThi.appendChild(pathG);

        // Quả bóng lăn về đáy cân bằng
        if (state.ballXi === undefined) state.ballXi = 0.5;
        if (!state.dangKeoBall) {
          state.ballXi += (xi_eq - state.ballXi) * 0.08;
        }
        var bx = d.x + state.ballXi * d.w;
        var b_dG = 1.8 * Math.pow(state.ballXi - xi_eq, 2);
        var by = d.y_bot - 20 - (0.8 - b_dG) * (d.h * 0.7);

        var ballG = taoSVG('circle', {
          'cx': bx, 'cy': by, 'r': '8',
          'fill': 'var(--hl-canh-bao, #f59e0b)', 'stroke': '#ffffff', 'stroke-width': '2',
          'class': 'cursor-grab drop-shadow hover:scale-125 transition-transform'
        });
        gDoThi.appendChild(ballG);
      }
    }

    function veOngTiem() {
      gOngTiem.innerHTML = '';
      gPiston.innerHTML = '';
      gChauNuoc.innerHTML = '';
      gXucTac.innerHTML = '';
      gCotNongDo.innerHTML = '';

      var vk = boCuc.vungKhi;
      var pt = boCuc.pitTong;
      var nb = boCuc.nutNhietDo;

      // Chậu ngâm nhiệt độ đáy
      var chau = taoSVG('rect', {
        'x': vk.x_dau - 10, 'y': vk.y_bot + 6, 'width': boCuc.W - vk.x_dau - 30, 'height': '18',
        'fill': state.T < 290 ? 'rgba(56, 189, 248, 0.25)' : (state.T > 320 ? 'rgba(239, 68, 68, 0.25)' : 'var(--hl-vien, #334155)'),
        'rx': '4', 'class': 'border border-chi'
      });
      var nutT = taoSVG('circle', {
        'cx': nb.x, 'cy': nb.y, 'r': nb.r,
        'fill': state.T < 290 ? 'var(--hl-nhan, #38bdf8)' : (state.T > 320 ? 'var(--hl-loi, #ef4444)' : 'var(--hl-vien, #64748b)'),
        'class': 'cursor-pointer drop-shadow',
        'aria-label': 'Nút điều chỉnh nhiệt độ chậu nước'
      });
      gChauNuoc.appendChild(chau);
      gChauNuoc.appendChild(nutT);

      // Thân ống tiêm trong suốt
      var ongTiemVien = taoSVG('rect', {
        'x': vk.x_dau, 'y': vk.y, 'width': boCuc.W - vk.x_dau - 40, 'height': vk.h,
        'fill': 'none', 'stroke': 'currentColor', 'stroke-width': '2', 'class': 'opacity-40', 'rx': '4'
      });
      gOngTiem.appendChild(ongTiemVien);

      // Vạch chia trên ống tiêm (mL)
      for (var v = 0.2; v <= 2.0; v += 0.2) {
        var vx = vk.x_dau + (v / 2.0) * (boCuc.W - vk.x_dau - 130);
        var vach = taoSVG('line', {
          'x1': vx, 'y1': vk.y, 'x2': vx, 'y2': vk.y + 10,
          'stroke': 'currentColor', 'class': 'opacity-30', 'stroke-width': '1'
        });
        gOngTiem.appendChild(vach);
      }

      // Pít-tông ống tiêm kéo ngang
      var pistonHead = taoSVG('rect', {
        'x': pt.x, 'y': pt.y, 'width': pt.w, 'height': pt.h,
        'fill': 'var(--hl-vien-dam, #475569)', 'stroke': 'currentColor', 'stroke-width': '1.5', 'rx': '2'
      });
      var pistonRod = taoSVG('line', {
        'x1': pt.x + pt.w, 'y1': pt.y + pt.h / 2, 'x2': pt.x + pt.w + 40, 'y2': pt.y + pt.h / 2,
        'stroke': 'currentColor', 'stroke-width': '5', 'class': 'opacity-80'
      });
      var pistonHandle = taoSVG('circle', {
        'cx': pt.tayNamX + 26, 'cy': pt.tayNamY, 'r': '11',
        'fill': 'var(--hl-nhan, #38bdf8)',
        'class': 'cursor-ew-resize drop-shadow active:scale-110 transition-transform',
        'tabindex': '0',
        'role': 'slider',
        'aria-label': 'Tay kéo pít-tông nén/giãn thể tích ống tiêm'
      });
      gPiston.appendChild(pistonHead);
      gPiston.appendChild(pistonRod);
      gPiston.appendChild(pistonHandle);

      // Viên xúc tác
      var xt = boCuc.vienXucTac;
      var vienXT = taoSVG('circle', {
        'cx': xt.x, 'cy': xt.y, 'r': xt.r,
        'fill': state.xucTac ? 'var(--hl-canh-bao, #f59e0b)' : 'var(--hl-vien, #64748b)',
        'class': 'cursor-pointer drop-shadow',
        'aria-label': 'Viên xúc tác'
      });
      gXucTac.appendChild(vienXT);
    }

    // Canvas hạt khí và đổi màu nền theo nồng độ NO2
    function chayVongLap(time) {
      if (!dangChay) return;
      var dt = (time - (lastTime || time)) / 1000;
      lastTime = time;
      if (dt > 0.1) dt = 0.016;

      ctx.clearRect(0, 0, W, H);

      var buoc = { r_t: 0, r_n: 0, cA: state.cA, cB: state.cB, Q: state.Q };
      // Cập nhật phản ứng động học số trôi về cân bằng (hệ số co giãn sư phạm)
      if (state.he === 'H2_I2_2HI') {
        // Delta n = 0: áp suất / thể tích không làm thay đổi Q
        state.Q = state.K || 50.0;
      } else {
        var k_t = state.xucTac ? 0.25 : 0.06;
        buoc = HL_CanBang.buocTichPhan(state.cA, state.cB, state.K, dt, k_t);
        state.cA = buoc.cA;
        state.cB = buoc.cB;
        state.Q = buoc.Q;
      }
      state.t_s += dt;

      // Lưu lịch sử vẽ C-t
      if (state.lichSuC.length === 0 || (state.t_s - state.lichSuC[state.lichSuC.length - 1].t) >= 0.2) {
        state.lichSuC.push({
          t: state.t_s,
          cA: state.cA,
          cB: state.cB,
          vt: buoc.r_t,
          vn: buoc.r_n
        });
        if (state.lichSuC.length > 150) state.lichSuC.shift();
      }

      var vk = boCuc.vungKhi;
      if (vk && vk.w > 10) {
        ctx.save();
        ctx.beginPath();
        ctx.rect(vk.x + 1, vk.y + 1, vk.w - 2, vk.h - 2);
        ctx.clip();

        // Độ đậm màu nâu đỏ tỉ lệ với [NO2] (tối đa ~0.04 M là đỏ nâu sẫm)
        var alphaMau = Math.min(0.85, Math.max(0.04, state.cB * 24.0));
        ctx.fillStyle = 'rgba(180, 83, 9, ' + alphaMau + ')';
        ctx.fillRect(vk.x, vk.y, vk.w, vk.h);

        // Chuyển động hạt minh hoạ
        for (var i = 0; i < particles.length; i++) {
          var p = particles[i];
          p.x += p.vx * dt;
          p.y += p.vy * dt;

          if (p.x < vk.x + 4) { p.x = vk.x + 4; p.vx = Math.abs(p.vx); }
          if (p.x > vk.x + vk.w - 4) { p.x = vk.x + vk.w - 4; p.vx = -Math.abs(p.vx); }
          if (p.y < vk.y + 4) { p.y = vk.y + 4; p.vy = Math.abs(p.vy); }
          if (p.y > vk.y + vk.h - 4) { p.y = vk.y + vk.h - 4; p.vy = -Math.abs(p.vy); }

          if (p.loai === 'B') {
            // NO2: chấm nâu đỏ
            ctx.beginPath();
            ctx.arc(p.x, p.y, 3.5, 0, Math.PI * 2);
            ctx.fillStyle = '#b45309';
            ctx.fill();
          } else {
            // N2O4: cặp 2 chấm không màu
            ctx.beginPath();
            ctx.arc(p.x - 3, p.y, 3, 0, Math.PI * 2);
            ctx.arc(p.x + 3, p.y, 3, 0, Math.PI * 2);
            ctx.fillStyle = 'rgba(255, 255, 255, 0.7)';
            ctx.fill();
          }
        }

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

        var pt = boCuc.pitTong;
        if (pt && Math.hypot(mx - (pt.tayNamX + 26), my - pt.tayNamY) < 24) {
          dangKeo = 'pitTong';
          e.preventDefault();
          return;
        }

        var nb = boCuc.nutNhietDo;
        if (nb && Math.hypot(mx - nb.x, my - nb.y) < 20) {
          dangKeo = 'nutNhietDo';
          e.preventDefault();
          return;
        }

        var xt = boCuc.vienXucTac;
        if (xt && Math.hypot(mx - xt.x, my - xt.y) < 18) {
          state.xucTac = !state.xucTac;
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

        var vk = boCuc.vungKhi;

        if (dangKeo === 'pitTong') {
          var W_MAX = boCuc.W - vk.x_dau - 130;
          var x_clamped = Math.max(vk.x_dau + 20, Math.min(vk.x_dau + W_MAX, mx));
          var newV = ((x_clamped - vk.x_dau) / W_MAX) * 2.0;
          newV = Math.max(0.2, Math.min(2.0, newV));

          var tiLeV = state.V / newV;
          state.V = newV;
          // Nồng độ tức thời đổi tỉ lệ nghịch với thể tích
          state.cA *= tiLeV;
          state.cB *= tiLeV;
          state.Q = Math.pow(state.cB, 2) / state.cA;
          state.chieu = HL_CanBang.chieu(state.Q, state.K);

          onThayDoi(state);
          capNhat(state);
        } else if (dangKeo === 'nutNhietDo') {
          var deltaX = mx - boCuc.nutNhietDo.x;
          if (deltaX > 4) {
            state.T = Math.min(373.15, state.T + 2);
          } else if (deltaX < -4) {
            state.T = Math.max(273.15, state.T - 2);
          }
          state.K = HL_CanBang.kcTheoT(state.T, 57.2, 298.15, 0.00461);
          state.chieu = HL_CanBang.chieu(state.Q, state.K);
          onThayDoi(state);
          capNhat(state);
        }
      }

      function onPointerUp() {
        dangKeo = null;
      }

      svg.addEventListener('pointerdown', onPointerDown);
      window.addEventListener('pointermove', onPointerMove);
      window.addEventListener('pointerup', onPointerUp);
    }

    function capNhat(trangThaiMoi) {
      if (trangThaiMoi) {
        for (var k in trangThaiMoi) {
          state[k] = trangThaiMoi[k];
        }
      }
      capNhatBoCuc();
      veDoThi();
      veOngTiem();
    }

    capNhatBoCuc();
    khoiTaoHat(state.cA, state.cB, boCuc.vungKhi);
    veDoThi();
    veOngTiem();
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
      datLaiHat: function () {
        khoiTaoHat(state.cA, state.cB, boCuc.vungKhi);
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
