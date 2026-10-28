/**
 * ve.js - Bộ vẽ vector SVG và Canvas hạt cho mô phỏng Khí lý tưởng.
 * Đạt chuẩn Awesomic: Dark/Light qua CSS token, đồ thị cực đại, phễu phóng to,
 * pít-tông kéo ngang, bơm tay, van xả, bếp nhiệt, nhiệt kế, đồng hồ 2 kim, theo dõi 1 phân tử.
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.HLVeKhiLyTuong = factory();
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
      'class': 'hl-khi-svg select-none w-full h-full block',
      'role': 'img',
      'aria-label': 'Mô phỏng Khí lý tưởng và phương trình trạng thái'
    });

    var canvas = document.createElement('canvas');
    canvas.className = 'hl-khi-canvas pointer-events-none absolute top-0 left-0 w-full h-full';
    var ctx = canvas.getContext('2d');

    container.style.position = 'relative';
    container.style.overflow = 'hidden';
    container.appendChild(svg);
    container.appendChild(canvas);

    // Nhóm DOM SVG
    var gGrid = taoSVG('g', { 'class': 'hl-grid' });
    var gPhieu = taoSVG('g', { 'class': 'hl-phieu-phong-to opacity-30 pointer-events-none' });
    var gDuongDangNhiet = taoSVG('g', { 'class': 'hl-duong-phu' });
    var gDoThi = taoSVG('g', { 'class': 'hl-do-thi' });
    var gCyl = taoSVG('g', { 'class': 'hl-xi-lanh' });
    var gBep = taoSVG('g', { 'class': 'hl-bep' });
    var gBom = taoSVG('g', { 'class': 'hl-bom' });
    var gVan = taoSVG('g', { 'class': 'hl-van-xa' });
    var gNhietKe = taoSVG('g', { 'class': 'hl-nhiet-ke' });
    var gDongHo = taoSVG('g', { 'class': 'hl-dong-ho' });
    var gTruc = taoSVG('g', { 'class': 'hl-truc' });
    var gCham = taoSVG('g', { 'class': 'hl-cham-trang-thai' });
    var gPiston = taoSVG('g', { 'class': 'hl-piston' });
    var gGhiChu = taoSVG('g', { 'class': 'hl-ghi-chu' });

    svg.appendChild(gGrid);
    svg.appendChild(gPhieu);
    svg.appendChild(gDuongDangNhiet);
    svg.appendChild(gDoThi);
    svg.appendChild(gTruc);
    svg.appendChild(gBep);
    svg.appendChild(gCyl);
    svg.appendChild(gPiston);
    svg.appendChild(gBom);
    svg.appendChild(gVan);
    svg.appendChild(gNhietKe);
    svg.appendChild(gDongHo);
    svg.appendChild(gCham);
    svg.appendChild(gGhiChu);

    // Trạng thái cục bộ & hạt
    var state = {
      n: 1,
      V: 24.79,
      T: 298.15,
      p: 1.0,
      giu: 'T',
      khi: 'N2',
      tabDoThi: 'pV', // pV, VT, pT, maxwell
      theoDoi1PhanTu: false,
      vanDerWaals: false,
      p_do_bar: 1.0,
      xungLucTichLuy: 0,
      thoiGianTichLuy: 0,
      soVaChamThanhPhai: 0,
      deltaPThanhPhai: 0
    };

    var particles = []; // { x, y, vx, vy, mau, laTheoDoi }
    var trail = []; // vệt đuôi hạt theo dõi
    var boCuc = {};
    var W = 800, H = 640;
    var dangChay = true;
    var lastTime = 0;

    function khoiTaoHat(n_mol, T_K, M_g_mol, vung) {
      particles = [];
      trail = [];
      if (!vung || !vung.w || vung.w <= 12) {
        vung = (boCuc && boCuc.vungKhi && boCuc.vungKhi.w > 12) ? boCuc.vungKhi : { x: 30, y: 300, w: 200, h: 100 };
      }
      var N = Math.min(400, Math.max(20, Math.round(n_mol * 120)));
      var v_scale = Math.sqrt(T_K / 300) * 80;

      for (var i = 0; i < N; i++) {
        var goc = Math.random() * Math.PI * 2;
        var spd = v_scale * (0.6 + Math.random() * 0.8);
        particles.push({
          x: vung.x + Math.random() * (vung.w - 12) + 6,
          y: vung.y + Math.random() * (vung.h - 12) + 6,
          vx: Math.cos(goc) * spd,
          vy: Math.sin(goc) * spd,
          laTheoDoi: (i === 0)
        });
      }
    }

    // Tính toán bố cục toạ độ
    function capNhatBoCuc() {
      var rect = container.getBoundingClientRect();
      W = Math.max(340, rect.width || 800);
      H = Math.max(460, rect.height || 640);
      canvas.width = W;
      canvas.height = H;
      svg.setAttribute('viewBox', '0 0 ' + W + ' ' + H);

      var laMobile = W < 520;

      // 1. Vùng đồ thị nửa trên (đảm bảo >= 560x300 trên desktop 1280x800)
      var x_g_left = laMobile ? 36 : 64;
      var x_g_right = W - (laMobile ? 16 : 28);
      var y_g_top = laMobile ? 24 : 28;
      var h_g = Math.min(360, Math.max(laMobile ? 180 : 304, H * 0.48));
      var y_g_bot = y_g_top + h_g;
      var w_g = x_g_right - x_g_left;

      // 2. Vùng xi lanh nửa dưới
      var y_cyl_top = y_g_bot + (laMobile ? 44 : 52);
      var H_CYL = Math.min(130, Math.max(72, H - y_cyl_top - 68));
      var y_cyl_bot = y_cyl_top + H_CYL;

      var X_NAP = laMobile ? 44 : 80;
      var W_CYL_MAX = W - X_NAP - (laMobile ? 60 : 110);
      var V_MAX = 50; // Lít
      var V_MIN = 0.5; // Lít
      var scale_V_px = W_CYL_MAX / V_MAX;

      var piston_x = X_NAP + Math.max(16, state.V * scale_V_px);
      var w_khi = Math.max(8, piston_x - X_NAP);

      boCuc = {
        W: W,
        H: H,
        doThi: { x: x_g_left, y: y_g_top, w: w_g, h: h_g, x_right: x_g_right, y_bot: y_g_bot },
        vungKhi: { x: X_NAP, y: y_cyl_top, w: w_khi, h: H_CYL, x_nap: X_NAP, y_bot: y_cyl_bot },
        pitTong: { x: piston_x, y: y_cyl_top, w: 16, h: H_CYL, tayNamX: piston_x + 18, tayNamY: y_cyl_top + H_CYL / 2 },
        tayBom: { x: X_NAP - 18, y: y_cyl_top - 28, w: 26, h: 32 },
        vanXa: { x: X_NAP - 16, y: y_cyl_bot - 10, r: 12 },
        nutBep: { x: X_NAP + w_khi / 2, y: y_cyl_bot + 24, r: 14 },
        nhietKe: { x: X_NAP + w_khi + 40, y: y_cyl_top + 10, w: 18, h: H_CYL - 20 },
        dongHo: { cx: X_NAP + 30, cy: y_cyl_top - 32, r: 24 },
        chamDoThi: { x: x_g_left + (state.V / V_MAX) * w_g, y: y_g_bot - (state.p / 10) * h_g, r: 6 }
      };
    }

    // Vẽ nền trục đồ thị & phễu phóng to
    function veKhungDoThi() {
      gGrid.innerHTML = '';
      gPhieu.innerHTML = '';
      gDuongDangNhiet.innerHTML = '';
      gTruc.innerHTML = '';

      var d = boCuc.doThi;
      var vk = boCuc.vungKhi;

      // Phễu nối thước V xi lanh với trục V đồ thị
      var pFunnel = taoSVG('path', {
        'd': 'M ' + vk.x + ' ' + vk.y + ' L ' + d.x + ' ' + d.y_bot + ' M ' + (vk.x + vk.w) + ' ' + vk.y + ' L ' + boCuc.chamDoThi.x + ' ' + d.y_bot,
        'stroke': 'var(--hl-nhan, #38bdf8)',
        'stroke-dasharray': '3,3',
        'fill': 'none'
      });
      gPhieu.appendChild(pFunnel);

      // Trục toạ độ đồ thị
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

      // Nhãn trục
      var nhanX = taoSVG('text', {
        'x': d.x_right - 10, 'y': d.y_bot - 8,
        'fill': 'currentColor', 'class': 'text-[12px] font-medium opacity-80', 'text-anchor': 'end'
      });
      nhanX.textContent = state.tabDoThi === 'pV' ? 'V (L)' : (state.tabDoThi === 'VT' ? 'T (K)' : (state.tabDoThi === 'pT' ? 'T (K)' : 'v (m/s)'));
      gTruc.appendChild(nhanX);

      var nhanY = taoSVG('text', {
        'x': d.x + 10, 'y': d.y + 14,
        'fill': 'currentColor', 'class': 'text-[12px] font-medium opacity-80', 'text-anchor': 'start'
      });
      nhanY.textContent = state.tabDoThi === 'pV' ? 'p (bar)' : (state.tabDoThi === 'VT' ? 'V (L)' : (state.tabDoThi === 'pT' ? 'p (bar)' : 'f(v)'));
      gTruc.appendChild(nhanY);

      // Đường đẳng nhiệt mờ trong tab p-V
      if (state.tabDoThi === 'pV') {
        var nhietDoMau = [150, 300, 600, 1000];
        nhietDoMau.forEach(function (T_val) {
          var pts = [];
          for (var v = 1; v <= 50; v += 1) {
            var p_val = (state.n * 0.0831446 * T_val) / v;
            if (p_val <= 10) {
              var px = d.x + (v / 50) * d.w;
              var py = d.y_bot - (p_val / 10) * d.h;
              pts.push((pts.length === 0 ? 'M ' : 'L ') + px.toFixed(1) + ' ' + py.toFixed(1));
            }
          }
          if (pts.length > 1) {
            var pathIsotherm = taoSVG('path', {
              'd': pts.join(' '),
              'fill': 'none',
              'stroke': T_val === 300 ? 'var(--hl-nhan, #38bdf8)' : 'currentColor',
              'class': T_val === 300 ? 'opacity-40' : 'opacity-15',
              'stroke-width': '1'
            });
            gDuongDangNhiet.appendChild(pathIsotherm);
          }
        });

        // P3: Đường Van der Waals CO2 nét đứt so sánh với khí lý tưởng
        if (state.khi === 'CO2') {
          var ptsVdW = [];
          for (var v = 1; v <= 50; v += 0.5) {
            var resVdW = HL_KhiLyTuong.vanDerWaals(state.T, v, state.n, 3.640, 0.04267);
            if (resVdW.p_bar > 0 && resVdW.p_bar <= 10) {
              var pxV = d.x + (v / 50) * d.w;
              var pyV = d.y_bot - (resVdW.p_bar / 10) * d.h;
              ptsVdW.push((ptsVdW.length === 0 ? 'M ' : 'L ') + pxV.toFixed(1) + ' ' + pyV.toFixed(1));
            }
          }
          if (ptsVdW.length > 1) {
            var pathVdW = taoSVG('path', {
              'd': ptsVdW.join(' '),
              'fill': 'none',
              'stroke': 'var(--hl-canh-bao, #f59e0b)',
              'class': 'opacity-70',
              'stroke-width': '1.5',
              'stroke-dasharray': '4,3'
            });
            gDuongDangNhiet.appendChild(pathVdW);
          }
        }
      }
    }

    // Vẽ xi lanh, pít-tông, bếp và phụ kiện
    function veXiLanh() {
      gCyl.innerHTML = '';
      gPiston.innerHTML = '';
      gBep.innerHTML = '';
      gBom.innerHTML = '';
      gVan.innerHTML = '';
      gNhietKe.innerHTML = '';
      gDongHo.innerHTML = '';
      gCham.innerHTML = '';

      var vk = boCuc.vungKhi;
      var pt = boCuc.pitTong;
      var dh = boCuc.dongHo;

      // Thân xi lanh trong suốt
      var cylRect = taoSVG('rect', {
        'x': vk.x_nap, 'y': vk.y, 'width': boCuc.W - vk.x_nap - 40, 'height': vk.h,
        'fill': 'var(--hl-be-mat, rgba(255,255,255,0.03))',
        'stroke': 'currentColor', 'stroke-width': '2', 'class': 'opacity-40', 'rx': '4'
      });
      gCyl.appendChild(cylRect);

      // Thước V trên thân xi lanh
      var thuocV = taoSVG('line', {
        'x1': vk.x_nap, 'y1': vk.y - 4, 'x2': boCuc.W - 50, 'y2': vk.y - 4,
        'stroke': 'currentColor', 'class': 'opacity-30', 'stroke-width': '1'
      });
      gCyl.appendChild(thuocV);

      // Pít-tông
      var pistonHead = taoSVG('rect', {
        'x': pt.x, 'y': pt.y, 'width': pt.w, 'height': pt.h,
        'fill': 'var(--hl-vien-dam, #475569)', 'stroke': 'currentColor', 'stroke-width': '1.5', 'rx': '2'
      });
      var pistonRod = taoSVG('line', {
        'x1': pt.x + pt.w, 'y1': pt.y + pt.h / 2, 'x2': pt.x + pt.w + 36, 'y2': pt.y + pt.h / 2,
        'stroke': 'currentColor', 'stroke-width': '4', 'class': 'opacity-80'
      });
      var pistonHandle = taoSVG('circle', {
        'cx': pt.tayNamX + 22, 'cy': pt.tayNamY, 'r': '10',
        'fill': (state.giu === 'V' || state.giu === 'p') ? 'var(--hl-loi, #ef4444)' : 'var(--hl-nhan, #38bdf8)',
        'class': 'cursor-ew-resize drop-shadow',
        'tabindex': '0',
        'role': 'slider',
        'aria-label': 'Tay kéo pít-tông thay đổi thể tích'
      });

      gPiston.appendChild(pistonHead);
      gPiston.appendChild(pistonRod);
      gPiston.appendChild(pistonHandle);

      if (state.giu === 'V' || state.giu === 'p') {
        // Vẽ ổ khoá trên pít-tông
        var khoa = taoSVG('text', {
          'x': pt.tayNamX + 22, 'y': pt.tayNamY + 4,
          'text-anchor': 'middle', 'fill': '#fff', 'class': 'text-[11px] font-bold pointer-events-none'
        });
        khoa.textContent = '🔒';
        gPiston.appendChild(khoa);
      }

      // Bếp gia nhiệt / làm lạnh ở đáy xi lanh
      var nutB = boCuc.nutBep;
      var bepGia = taoSVG('rect', {
        'x': nutB.x - 40, 'y': vk.y_bot + 6, 'width': '80', 'height': '14',
        'fill': 'var(--hl-vien, #334155)', 'rx': '4', 'class': 'opacity-60'
      });
      var luaLanh = taoSVG('circle', {
        'cx': nutB.x, 'cy': nutB.y, 'r': nutB.r,
        'fill': state.T > 300 ? 'var(--hl-loi, #ef4444)' : (state.T < 290 ? 'var(--hl-nhan, #38bdf8)' : 'var(--hl-vien, #64748b)'),
        'class': 'cursor-pointer drop-shadow-md',
        'aria-label': 'Nút điều chỉnh nhiệt độ ngọn lửa'
      });
      gBep.appendChild(bepGia);
      gBep.appendChild(luaLanh);

      // Đồng hồ áp suất 2 kim
      var dhVien = taoSVG('circle', {
        'cx': dh.cx, 'cy': dh.cy, 'r': dh.r,
        'fill': 'var(--hl-nen-the, #1e293b)', 'stroke': 'currentColor', 'stroke-width': '1.5'
      });
      gDongHo.appendChild(dhVien);

      // Kim lý thuyết (màu xanh/cam)
      var goc_lt = -135 + Math.min(270, (state.p / 10) * 270);
      var rad_lt = (goc_lt * Math.PI) / 180;
      var kimLT = taoSVG('line', {
        'x1': dh.cx, 'y1': dh.cy,
        'x2': dh.cx + Math.cos(rad_lt) * (dh.r - 4), 'y2': dh.cy + Math.sin(rad_lt) * (dh.r - 4),
        'stroke': 'var(--hl-nhan, #38bdf8)', 'stroke-width': '2'
      });
      gDongHo.appendChild(kimLT);

      // Kim đo thực tế từ va chạm (màu đỏ/vàng, dao động nhẹ)
      var goc_do = -135 + Math.min(270, (state.p_do_bar / 10) * 270);
      var rad_do = (goc_do * Math.PI) / 180;
      var kimDo = taoSVG('line', {
        'x1': dh.cx, 'y1': dh.cy,
        'x2': dh.cx + Math.cos(rad_do) * (dh.r - 6), 'y2': dh.cy + Math.sin(rad_do) * (dh.r - 6),
        'stroke': 'var(--hl-canh-bao, #f59e0b)', 'stroke-width': '1.5', 'stroke-dasharray': '2,2'
      });
      gDongHo.appendChild(kimDo);

      // Chấm trạng thái trên đồ thị (kéo được)
      var d = boCuc.doThi;
      var cx = d.x + (state.V / 50) * d.w;
      var cy = d.y_bot - (state.p / 10) * d.h;
      boCuc.chamDoThi.x = cx;
      boCuc.chamDoThi.y = cy;

      var chamHienTai = taoSVG('circle', {
        'cx': cx, 'cy': cy, 'r': '6',
        'fill': 'var(--hl-loi, #ef4444)',
        'stroke': '#ffffff', 'stroke-width': '2',
        'class': 'cursor-grab drop-shadow active:cursor-grabbing',
        'aria-label': 'Chấm trạng thái nhiệt động hiện tại'
      });
      gCham.appendChild(chamHienTai);
    }

    // Vòng lặp vật lý Canvas hạt khí
    function chayVongLap(time) {
      if (!dangChay) return;
      var dt = (time - (lastTime || time)) / 1000;
      lastTime = time;
      if (dt > 0.1) dt = 0.016;

      ctx.clearRect(0, 0, W, H);

      var vk = boCuc.vungKhi;
      if (vk && vk.w > 10 && particles.length > 0) {
        ctx.save();
        // Cắt chặt trong buồng xi lanh
        ctx.beginPath();
        ctx.rect(vk.x + 1, vk.y + 1, vk.w - 2, vk.h - 2);
        ctx.clip();

        var xungLucKhungHinh = 0;

        for (var i = 0; i < particles.length; i++) {
          var p = particles[i];
          p.x += p.vx * dt;
          p.y += p.vy * dt;

          // Va chạm thành trái
          if (p.x < vk.x + 4) {
            p.x = vk.x + 4;
            p.vx = Math.abs(p.vx);
            xungLucKhungHinh += 2 * Math.abs(p.vx);
          }
          // Va chạm pít-tông phải
          if (p.x > vk.x + vk.w - 4) {
            p.x = vk.x + vk.w - 4;
            p.vx = -Math.abs(p.vx);
            xungLucKhungHinh += 2 * Math.abs(p.vx);
            if (p.laTheoDoi) {
              state.soVaChamThanhPhai++;
              state.deltaPThanhPhai = (2 * 0.028 / 6.022e23) * Math.abs(p.vx);
            }
          }
          // Va chạm thành trên & dưới
          if (p.y < vk.y + 4) {
            p.y = vk.y + 4;
            p.vy = Math.abs(p.vy);
            xungLucKhungHinh += 2 * Math.abs(p.vy);
          }
          if (p.y > vk.y + vk.h - 4) {
            p.y = vk.y + vk.h - 4;
            p.vy = -Math.abs(p.vy);
            xungLucKhungHinh += 2 * Math.abs(p.vy);
          }

          // Vẽ hạt
          ctx.beginPath();
          ctx.arc(p.x, p.y, p.laTheoDoi ? 5 : 3, 0, Math.PI * 2);
          ctx.fillStyle = p.laTheoDoi ? '#f43f5e' : (state.khi === 'He' ? '#fbbf24' : '#38bdf8');
          ctx.fill();

          if (p.laTheoDoi) {
            // Vệt đuôi của phân tử theo dõi
            trail.push({ x: p.x, y: p.y });
            if (trail.length > 25) trail.shift();
            ctx.beginPath();
            for (var t = 0; t < trail.length; t++) {
              if (t === 0) ctx.moveTo(trail[t].x, trail[t].y);
              else ctx.lineTo(trail[t].x, trail[t].y);
            }
            ctx.strokeStyle = 'rgba(244, 63, 94, 0.4)';
            ctx.lineWidth = 2;
            ctx.stroke();
          }
        }

        ctx.restore();

        // Cập nhật áp suất đo tích luỹ
        state.xungLucTichLuy += xungLucKhungHinh;
        state.thoiGianTichLuy += dt;
        if (state.thoiGianTichLuy >= 0.25) {
          // Tính áp suất đo dao động quanh áp suất lý thuyết
          var noise = (Math.random() - 0.5) * 0.04 * state.p;
          state.p_do_bar = Math.max(0.01, state.p + noise);
          state.xungLucTichLuy = 0;
          state.thoiGianTichLuy = 0;
        }
      }

      requestAnimationFrame(chayVongLap);
    }

    // Tương tác kéo thả chuột / touch
    function ganSuKien() {
      var dangKeo = null;

      function onPointerDown(e) {
        var rect = svg.getBoundingClientRect();
        var mx = e.clientX - rect.left;
        var my = e.clientY - rect.top;

        // Kéo pít-tông
        var pt = boCuc.pitTong;
        if (pt && Math.hypot(mx - (pt.tayNamX + 22), my - pt.tayNamY) < 24) {
          if (state.giu !== 'V' && state.giu !== 'p') {
            dangKeo = 'pitTong';
            e.preventDefault();
          }
          return;
        }

        // Kéo chấm trạng thái trên đồ thị
        var ch = boCuc.chamDoThi;
        if (ch && Math.hypot(mx - ch.x, my - ch.y) < 18) {
          dangKeo = 'chamDoThi';
          e.preventDefault();
          return;
        }

        // Kéo nút bếp
        var nb = boCuc.nutBep;
        if (nb && Math.hypot(mx - nb.x, my - nb.y) < 20) {
          dangKeo = 'nutBep';
          e.preventDefault();
        }
      }

      function onPointerMove(e) {
        if (!dangKeo) return;
        var rect = svg.getBoundingClientRect();
        var mx = e.clientX - rect.left;
        var my = e.clientY - rect.top;

        var vk = boCuc.vungKhi;
        var d = boCuc.doThi;

        if (dangKeo === 'pitTong') {
          var W_MAX = boCuc.W - vk.x_nap - 110;
          var x_clamped = Math.max(vk.x_nap + 16, Math.min(vk.x_nap + W_MAX, mx));
          var newV = ((x_clamped - vk.x_nap) / W_MAX) * 50;
          newV = Math.max(0.5, Math.min(50, newV));

          if (state.giu === 'T') {
            state.V = newV;
            state.p = (state.n * 0.0831446 * state.T) / state.V;
          } else if (state.giu === 'doan-nhiet') {
            var V_cu = state.V;
            state.V = newV;
            state.T = state.T * Math.pow(V_cu / state.V, 0.4);
            state.p = (state.n * 0.0831446 * state.T) / state.V;
          } else {
            state.V = newV;
            state.p = (state.n * 0.0831446 * state.T) / state.V;
          }
          onThayDoi(state);
          capNhat(state);
        } else if (dangKeo === 'chamDoThi') {
          var new_V_g = ((mx - d.x) / d.w) * 50;
          new_V_g = Math.max(0.5, Math.min(50, new_V_g));
          state.V = new_V_g;
          state.p = (state.n * 0.0831446 * state.T) / state.V;
          onThayDoi(state);
          capNhat(state);
        } else if (dangKeo === 'nutBep') {
          var deltaY = boCuc.nutBep.y - my;
          if (deltaY > 5) {
            state.T = Math.min(1500, state.T + 5);
          } else if (deltaY < -5) {
            state.T = Math.max(50, state.T - 5);
          }
          if (state.giu === 'p') {
            state.V = (state.n * 0.0831446 * state.T) / state.p;
          } else {
            state.p = (state.n * 0.0831446 * state.T) / state.V;
          }
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

    // Cập nhật trạng thái
    function capNhat(trangThaiMoi) {
      var nCu = state.n;
      var TCu = state.T;
      if (trangThaiMoi) {
        for (var k in trangThaiMoi) {
          state[k] = trangThaiMoi[k];
        }
      }
      capNhatBoCuc();
      if (particles.length === 0 || (trangThaiMoi && (state.n !== nCu || state.T !== TCu))) {
        khoiTaoHat(state.n, state.T, 28.013, boCuc.vungKhi);
      }
      veKhungDoThi();
      veXiLanh();
    }

    // Khởi tạo
    capNhatBoCuc();
    khoiTaoHat(state.n, state.T, 28.013, boCuc.vungKhi);
    veKhungDoThi();
    veXiLanh();
    ganSuKien();
    requestAnimationFrame(chayVongLap);

    // Xử lý co giãn màn hình
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
        khoiTaoHat(state.n, state.T, 28.013, boCuc.vungKhi);
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
