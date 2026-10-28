/**
 * hinh-chung-minh.js - Bộ 34 mô-đun hình chuyển động 2D (SVG + rAF) đồng bộ từng bước
 * cho toàn bộ thư viện công thức Hoá lý (K1-K8, N1-N8, H1-H4, D1-D7, C1-C7).
 *
 * Tuân thủ Awesomic tokens, không dùng mã màu hex cứng.
 * Tương thích 100% cả chế độ Sáng và Tối (rgb(9, 9, 11)).
 * Phông chữ tiếng Việt kế thừa "Be Vietnam Pro", dùng <tspan> cho chỉ số dưới/trên.
 * Tự động giải phóng rAF khi đóng ngăn kéo hoặc chuyển bước.
 */

(function (global) {
  'use strict';

  var HOAT_ANH_DANG_CHAY = []; // Lưu danh sách các instance đang chạy để dọn dẹp

  var HLHinhChungMinh = {
    /**
     * Dọn dẹp tất cả các vòng lặp rAF đang chạy
     */
    donDepTatCa: function () {
      while (HOAT_ANH_DANG_CHAY.length > 0) {
        var inst = HOAT_ANH_DANG_CHAY.pop();
        if (inst && typeof inst.donDep === 'function') {
          try { inst.donDep(); } catch (e) {}
        }
      }
    },

    /**
     * Tạo phần tử chuyển động trực quan cho một công thức
     * @param {string} idHinh - Tên hình minh hoạ (hoặc mã công thức K1, K2...)
     * @param {object} [tuyChon] - Tuỳ chọn { ct, buocIndex, tongBuoc, w: 480, h: 220 }
     * @returns {HTMLElement} wrapper chứa SVG/Canvas + thanh điều khiển
     */
    taoHinh: function (idHinh, tuyChon) {
      tuyChon = tuyChon || {};
      var w = tuyChon.w || 480;
      var h = tuyChon.h || 220;

      var wrapper = document.createElement('div');
      wrapper.className = 'hl-hinh-cm-wrap flex flex-col items-center my-3 p-3 rounded-2xl bg-zinc-100/80 dark:bg-zinc-900/90 border border-zinc-200 dark:border-zinc-800 text-zinc-800 dark:text-zinc-200 shadow-inner w-full max-w-[520px] mx-auto select-none';
      wrapper.setAttribute('role', 'region');
      wrapper.setAttribute('aria-label', tuyChon.nhan || ('Mô phỏng trực quan: ' + idHinh));

      // 1. Chú thích bước
      var chuThichEl = document.createElement('div');
      chuThichEl.className = 'hl-hcm-chuthich w-full text-xs font-medium px-2 py-1 mb-2 rounded-lg bg-sky-500/10 text-sky-700 dark:text-sky-300 border border-sky-500/20 flex items-center justify-between gap-2';
      chuThichEl.innerHTML = '<span class="font-bold flex items-center gap-1"><span class="w-2 h-2 rounded-full bg-sky-500 animate-pulse"></span><span class="hl-hcm-buoc-ten">Giai đoạn minh hoạ</span></span><span class="hl-hcm-buoc-chi-tiet text-[11px] font-mono text-zinc-500 dark:text-zinc-400"></span>';
      wrapper.appendChild(chuThichEl);

      // 2. Khung SVG hiển thị
      var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      svg.setAttribute('viewBox', '0 0 ' + w + ' ' + h);
      svg.setAttribute('width', '100%');
      svg.setAttribute('height', '100%');
      svg.style.maxHeight = h + 'px';
      svg.style.maxWidth = w + 'px';
      svg.style.fontFamily = '"Be Vietnam Pro", -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
      svg.setAttribute('class', 'hl-hinh-cm-svg select-none overflow-visible w-full');
      wrapper.appendChild(svg);

      // 3. Thanh điều khiển: Tạm dừng / Phát lại / Đặt lại & Slider tương tác
      var thanhDieuKhien = document.createElement('div');
      thanhDieuKhien = document.createElement('div');
      thanhDieuKhien.className = 'hl-hcm-toolbar w-full mt-2.5 pt-2 border-t border-zinc-200/80 dark:border-zinc-800 flex items-center justify-between gap-2 flex-wrap text-xs';

      var cumNut = document.createElement('div');
      cumNut.className = 'flex items-center gap-1.5';
      var btnPlay = document.createElement('button');
      btnPlay.type = 'button';
      btnPlay.className = 'px-2.5 py-1 rounded-lg bg-white dark:bg-zinc-800 border border-zinc-200 dark:border-zinc-700 hover:bg-zinc-50 dark:hover:bg-zinc-750 font-semibold text-zinc-700 dark:text-zinc-200 shadow-sm transition-colors flex items-center gap-1';
      btnPlay.innerHTML = '<span class="hl-icon-play">⏸</span><span>Tạm dừng</span>';

      var btnReset = document.createElement('button');
      btnReset.type = 'button';
      btnReset.className = 'px-2 py-1 rounded-lg bg-white dark:bg-zinc-800 border border-zinc-200 dark:border-zinc-700 hover:bg-zinc-50 dark:hover:bg-zinc-750 font-semibold text-zinc-700 dark:text-zinc-200 shadow-sm transition-colors';
      btnReset.innerHTML = '↻';
      btnReset.title = 'Đặt lại thông số ban đầu';

      cumNut.appendChild(btnPlay);
      cumNut.appendChild(btnReset);
      thanhDieuKhien.appendChild(cumNut);

      var cumSlider = document.createElement('div');
      cumSlider.className = 'flex-1 min-w-[170px] flex items-center justify-end gap-2';
      cumSlider.innerHTML =
        '<label class="text-[11px] font-semibold text-zinc-500 dark:text-zinc-400 whitespace-nowrap hl-slider-nhan"></label>' +
        '<input type="range" class="hl-hcm-slider h-1.5 bg-zinc-200 dark:bg-zinc-700 rounded-lg appearance-none cursor-pointer accent-sky-500 w-24 sm:w-28">' +
        '<span class="hl-slider-giatri text-xs font-mono font-bold text-sky-600 dark:text-sky-400 w-12 text-right"></span>';
      thanhDieuKhien.appendChild(cumSlider);
      wrapper.appendChild(thanhDieuKhien);

      // Tra cứu hàm vẽ
      var fn = this.cacHinh[idHinh];
      if (!fn && tuyChon.ct && tuyChon.ct.ma) {
        fn = this.cacHinh[tuyChon.ct.ma];
      }
      if (!fn) {
        // Fallback tra cứu qua alias mã công thức
        var ma = (idHinh || '').toUpperCase();
        if (this.cacHinh[ma]) fn = this.cacHinh[ma];
      }

      var ctrl = {
        dangChay: true,
        t: 0,
        buocIndex: tuyChon.buocIndex || 1,
        tongBuoc: tuyChon.tongBuoc || 3,
        sliderVal: 1,
        rafId: null,
        setChuThich: function (ten, chiTiet) {
          chuThichEl.querySelector('.hl-hcm-buoc-ten').textContent = ten || 'Minh hoạ bước';
          chuThichEl.querySelector('.hl-hcm-buoc-chi-tiet').textContent = chiTiet || '';
        },
        caiDatSlider: function (cauHinh) {
          var lbl = cumSlider.querySelector('.hl-slider-nhan');
          var inp = cumSlider.querySelector('.hl-hcm-slider');
          var val = cumSlider.querySelector('.hl-slider-giatri');
          lbl.textContent = cauHinh.nhan || 'Tham số:';
          inp.min = cauHinh.min !== undefined ? cauHinh.min : 0;
          inp.max = cauHinh.max !== undefined ? cauHinh.max : 100;
          inp.step = cauHinh.step !== undefined ? cauHinh.step : 1;
          inp.value = cauHinh.macDinh !== undefined ? cauHinh.macDinh : inp.min;
          ctrl.sliderVal = parseFloat(inp.value);
          val.textContent = inp.value + (cauHinh.donVi ? ' ' + cauHinh.donVi : '');

          inp.oninput = function () {
            ctrl.sliderVal = parseFloat(inp.value);
            val.textContent = inp.value + (cauHinh.donVi ? ' ' + cauHinh.donVi : '');
            if (typeof cauHinh.onThayDoi === 'function') {
              cauHinh.onThayDoi(ctrl.sliderVal);
            }
          };
          ctrl.datLaiSlider = function () {
            inp.value = cauHinh.macDinh !== undefined ? cauHinh.macDinh : inp.min;
            ctrl.sliderVal = parseFloat(inp.value);
            val.textContent = inp.value + (cauHinh.donVi ? ' ' + cauHinh.donVi : '');
            if (typeof cauHinh.onThayDoi === 'function') {
              cauHinh.onThayDoi(ctrl.sliderVal);
            }
          };
        },
        donDep: function () {
          ctrl.dangChay = false;
          if (ctrl.rafId) {
            cancelAnimationFrame(ctrl.rafId);
            ctrl.rafId = null;
          }
        }
      };

      btnPlay.onclick = function () {
        ctrl.dangChay = !ctrl.dangChay;
        if (ctrl.dangChay) {
          btnPlay.innerHTML = '<span class="hl-icon-play">⏸</span><span>Tạm dừng</span>';
          vongLap();
        } else {
          btnPlay.innerHTML = '<span class="hl-icon-play">▶</span><span>Tiếp tục</span>';
          if (ctrl.rafId) cancelAnimationFrame(ctrl.rafId);
        }
      };

      btnReset.onclick = function () {
        ctrl.t = 0;
        if (ctrl.datLaiSlider) ctrl.datLaiSlider();
        if (typeof ctrl.onReset === 'function') ctrl.onReset();
      };

      // Đăng ký dọn dẹp
      HOAT_ANH_DANG_CHAY.push(ctrl);

      // Khởi tạo mô-đun
      var veHam = null;
      if (typeof fn === 'function') {
        veHam = fn.call(this, svg, w, h, ctrl, tuyChon);
      } else {
        svg.innerHTML = '<text x="' + (w/2) + '" y="' + (h/2) + '" text-anchor="middle" fill="currentColor" opacity="0.5" font-size="13">[' + idHinh + ']</text>';
      }

      var lastTime = performance.now();
      function vongLap(currTime) {
        if (!ctrl.dangChay) return;
        var dt = currTime ? Math.min((currTime - lastTime) / 1000, 0.1) : 0.016;
        lastTime = currTime || performance.now();
        ctrl.t += dt;

        if (typeof veHam === 'function') {
          try {
            veHam(ctrl.t, dt);
          } catch (e) {
            console.warn('Lỗi vẽ hoạt ảnh ' + idHinh + ':', e);
          }
        }
        ctrl.rafId = requestAnimationFrame(vongLap);
      }

      // Tạm dừng khi phần tử cuộn ra ngoài màn hình
      if (typeof IntersectionObserver !== 'undefined') {
        var obs = new IntersectionObserver(function (entries) {
          entries.forEach(function (ent) {
            if (!ent.isIntersecting && ctrl.dangChay) {
              if (ctrl.rafId) cancelAnimationFrame(ctrl.rafId);
            } else if (ent.isIntersecting && ctrl.dangChay) {
              lastTime = performance.now();
              vongLap();
            }
          });
        }, { threshold: 0.1 });
        obs.observe(wrapper);
      }

      // Khởi động vòng lặp
      vongLap();

      return wrapper;
    },

    cacHinh: {}
  };

  // Helper tạo defs marker mũi tên dùng chung
  function taoDefsMuiTen(svg) {
    var defs = document.createElementNS('http://www.w3.org/2000/svg', 'defs');
    defs.innerHTML =
      '<marker id="arr-nhan" viewBox="0 0 10 10" refX="6" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">' +
        '<path d="M 0 1 L 10 5 L 0 9 z" fill="var(--hl-nhan, #38bdf8)" />' +
      '</marker>' +
      '<marker id="arr-thanh-cong" viewBox="0 0 10 10" refX="6" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">' +
        '<path d="M 0 1 L 10 5 L 0 9 z" fill="var(--hl-thanh-cong, #10b981)" />' +
      '</marker>' +
      '<marker id="arr-canh-bao" viewBox="0 0 10 10" refX="6" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">' +
        '<path d="M 0 1 L 10 5 L 0 9 z" fill="var(--hl-canh-bao, #f59e0b)" />' +
      '</marker>' +
      '<marker id="arr-nguy-hiem" viewBox="0 0 10 10" refX="6" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">' +
        '<path d="M 0 1 L 10 5 L 0 9 z" fill="var(--hl-nguy-hiem, #ef4444)" />' +
      '</marker>' +
      '<pattern id="gach-thanh-chung" width="8" height="8" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">' +
        '<line x1="0" y1="0" x2="0" y2="8" stroke="currentColor" stroke-opacity="0.25" stroke-width="2"/>' +
      '</pattern>';
    svg.appendChild(defs);
  }

  // =========================================================================
  // KHỐI 1: KHÍ LÝ TƯỞNG (K1 - K8)
  // =========================================================================

  /**
   * K1: trang-thai-trung-gian (Phương trình Clapéron 1 -> 1' -> 2)
   */
  HLHinhChungMinh.cacHinh['trang-thai-trung-gian'] = function (svg, w, h, ctrl) {
    taoDefsMuiTen(svg);
    ctrl.caiDatSlider({ nhan: 'V₂/V₁:', min: 1.2, max: 3.5, step: 0.1, macDinh: 2.0, donVi: 'lần' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function (t) {
      var rV = ctrl.sliderVal;
      var buoc = ctrl.buocIndex;
      var chuThich = buoc === 1
        ? 'Bước 1: Trạng thái ban đầu (p₁, V₁, T₁)'
        : (buoc === 2
          ? 'Bước 2: Quá trình đẳng nhiệt Boyle (T₁) sang 1\' (p₂, V\', T₁)'
          : (buoc === 3
            ? 'Bước 3: Quá trình đẳng áp Charles (p₂) từ 1\' sang 2 (p₂, V₂, T₂)'
            : 'Bước 4-5: Khử biến trung gian V\' thu được p₁V₁/T₁ = p₂V₂/T₂'));
      ctrl.setChuThich('Khí lý tưởng K1', chuThich);

      var oX = 55, oY = h - 45, gW = w - 100, gH = h - 75;
      var x1 = oX + 40, y1 = 45;
      var x1p = oX + 130, y1p = 100;
      var x2 = oX + 130 + (rV - 1) * 70, y2 = 100;

      var pha = (t % 4) / 4;
      var curX = pha < 0.5 ? (x1 + (x1p - x1) * (pha * 2)) : (x1p + (x2 - x1p) * ((pha - 0.5) * 2));
      var curY = pha < 0.5 ? (y1 + (y1p - y1) * Math.sin(pha * Math.PI)) : 100;

      g.innerHTML =
        // Trục tọa độ
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + (oX + gW) + '" y2="' + oY + '" stroke="currentColor" stroke-width="1.5" />' +
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + oX + '" y2="' + (oY - gH) + '" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + (oX + gW + 6) + '" y="' + (oY + 4) + '" font-size="11" font-weight="bold" fill="currentColor">V</text>' +
        '<text x="' + oX + '" y="' + (oY - gH - 6) + '" font-size="11" font-weight="bold" fill="currentColor">p</text>' +
        // Điểm 1
        '<circle cx="' + x1 + '" cy="' + y1 + '" r="5" fill="var(--hl-nhan, #38bdf8)" />' +
        '<text x="' + x1 + '" y="' + (y1 - 8) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">1 (p₁, V₁, T₁)</text>' +
        // Đường đẳng nhiệt 1 -> 1'
        '<path d="M ' + x1 + ' ' + y1 + ' Q ' + ((x1 + x1p) / 2) + ' ' + (y1p - 5) + ' ' + x1p + ' ' + y1p + '" fill="none" stroke="var(--hl-nhan, #38bdf8)" stroke-width="2" stroke-dasharray="' + (buoc >= 2 ? 'none' : '3,3') + '" />' +
        '<circle cx="' + x1p + '" cy="' + y1p + '" r="5" fill="var(--hl-canh-bao, #f59e0b)" />' +
        '<text x="' + x1p + '" y="' + (y1p + 18) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-canh-bao, #f59e0b)">1\' (p₂, V\', T₁)</text>' +
        // Đường đẳng áp 1' -> 2
        '<line x1="' + x1p + '" y1="' + y1p + '" x2="' + x2 + '" y2="' + y2 + '" stroke="var(--hl-thanh-cong, #10b981)" stroke-width="2.5" />' +
        '<circle cx="' + x2 + '" cy="' + y2 + '" r="5" fill="var(--hl-thanh-cong, #10b981)" />' +
        '<text x="' + x2 + '" y="' + (y2 - 8) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">2 (p₂, V₂, T₂)</text>' +
        // Hạt trạng thái di chuyển
        '<circle cx="' + curX + '" cy="' + curY + '" r="4" fill="var(--hl-nguy-hiem, #ef4444)" stroke="#fff" stroke-width="1.5" />' +
        // Chú thích phương trình
        '<text x="' + (w/2) + '" y="' + (h - 12) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Khử V\' = (p₁V₁)/p₂ = (V₂T₁)/T₂ → p₁V₁/T₁ = p₂V₂/T₂ = nR</text>';
    };
  };

  /**
   * K2: phan-tu-dap-thanh (Áp suất động học vi mô p = 1/3 μ m <v²>)
   */
  HLHinhChungMinh.cacHinh['phan-tu-dap-thanh'] = function (svg, w, h, ctrl) {
    taoDefsMuiTen(svg);
    ctrl.caiDatSlider({ nhan: 'Vận tốc vₓ:', min: 100, max: 800, step: 50, macDinh: 400, donVi: 'm/s' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function (t) {
      var vx = ctrl.sliderVal;
      var speedRatio = vx / 400;
      var buoc = ctrl.buocIndex;
      ctrl.setChuThich('Áp suất động học K2',
        buoc <= 2 ? 'Hạt bay tới thành với p₁ = +mvₓ' : (buoc <= 4 ? 'Va chạm đàn hồi bật lại p₂ = −mvₓ → xung lượng Δp = 2mvₓ' : 'Tổng hợp 3 trục (x,y,z): p = ⅓ μ m ⟨v²⟩')
      );

      var xWall = w - 60;
      var yCenter = h / 2 - 10;
      var rH = 8;
      var chuKy = 2.5 / speedRatio;
      var pha = (t % chuKy) / chuKy; // 0..1
      var dX = pha < 0.5 ? (40 + (xWall - rH - 40) * (pha * 2)) : ((xWall - rH) - (xWall - rH - 40) * ((pha - 0.5) * 2));
      var dangBayToi = pha < 0.5;

      g.innerHTML =
        // Thành bình bên phải
        '<rect x="' + xWall + '" y="20" width="30" height="' + (h - 60) + '" fill="url(#gach-thanh-chung)" stroke="currentColor" stroke-width="1.5" rx="3" />' +
        '<line x1="' + xWall + '" y1="20" x2="' + xWall + '" y2="' + (h - 40) + '" stroke="currentColor" stroke-width="2.5" />' +
        '<text x="' + (xWall + 15) + '" y="' + (h/2) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor" transform="rotate(90 ' + (xWall + 15) + ' ' + (h/2) + ')">Thành bình (Diện tích S)</text>' +
        // Quỹ đạo chuyển động
        '<line x1="40" y1="' + yCenter + '" x2="' + (xWall - rH) + '" y2="' + yCenter + '" stroke="currentColor" stroke-dasharray="3,3" stroke-opacity="0.3" />' +
        // Hạt phân tử
        '<circle cx="' + dX + '" cy="' + yCenter + '" r="' + rH + '" fill="' + (dangBayToi ? 'var(--hl-nhan, #38bdf8)' : 'var(--hl-canh-bao, #f59e0b)') + '" stroke="currentColor" stroke-width="1.5" />' +
        // Vector vận tốc
        (dangBayToi
          ? '<line x1="' + dX + '" y1="' + (yCenter - 18) + '" x2="' + (dX + 45) + '" y2="' + (yCenter - 18) + '" stroke="var(--hl-nhan, #38bdf8)" stroke-width="2.5" marker-end="url(#arr-nhan)" />' +
            '<text x="' + (dX + 20) + '" y="' + (yCenter - 26) + '" font-size="11" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">p₁ = +m·v<tspan baseline-shift="sub" font-size="75%">x</tspan></text>'
          : '<line x1="' + dX + '" y1="' + (yCenter + 18) + '" x2="' + (dX - 45) + '" y2="' + (yCenter + 18) + '" stroke="var(--hl-canh-bao, #f59e0b)" stroke-width="2.5" marker-end="url(#arr-canh-bao)" />' +
            '<text x="' + (dX - 25) + '" y="' + (yCenter + 32) + '" font-size="11" font-weight="bold" fill="var(--hl-canh-bao, #f59e0b)">p₂ = −m·v<tspan baseline-shift="sub" font-size="75%">x</tspan></text>'
        ) +
        // Chú thích xung lực
        '<g transform="translate(' + (w/2 - 90) + ', ' + (h - 22) + ')">' +
          '<rect x="0" y="-14" width="180" height="22" rx="5" fill="currentColor" fill-opacity="0.08" />' +
          '<text x="90" y="2" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Δp = 2m·v<tspan baseline-shift="sub" font-size="75%">x</tspan> | F = N·Δp / Δt</text>' +
        '</g>';
    };
  };

  /**
   * K3: dong-nang-nhiet-do (Động năng trung bình Wđ = 3/2 kT)
   */
  HLHinhChungMinh.cacHinh['dong-nang-nhiet-do'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Nhiệt độ T:', min: 100, max: 800, step: 25, macDinh: 300, donVi: 'K' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    // Hạt chuyển động Brown trong buồng
    var nHat = 14;
    var hats = [];
    for (var i = 0; i < nHat; i++) {
      hats.push({
        x: 40 + Math.random() * 180,
        y: 35 + Math.random() * 110,
        vx: (Math.random() - 0.5) * 2,
        vy: (Math.random() - 0.5) * 2
      });
    }

    return function (t, dt) {
      var T = ctrl.sliderVal;
      var speedScale = Math.sqrt(T / 300);
      var wD = (1.5 * 1.380649e-23 * T * 1e21).toFixed(2); // zJ (10^-21 J)
      ctrl.setChuThich('Động năng tịnh tiến K3', 'Nhiệt độ T = ' + T + ' K → Động năng W̄_đ = ' + wD + ' · 10⁻²¹ J');

      // Cập nhật toạ độ hạt
      hats.forEach(function (pt) {
        pt.x += pt.vx * speedScale * 40 * dt;
        pt.y += pt.vy * speedScale * 40 * dt;
        if (pt.x < 35 || pt.x > 225) pt.vx *= -1;
        if (pt.y < 35 || pt.y > 145) pt.vy *= -1;
      });

      var barH = Math.min(110, (T / 800) * 110);

      g.innerHTML =
        // Buồng khí bên trái
        '<rect x="30" y="30" width="200" height="120" rx="8" fill="currentColor" fill-opacity="0.05" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="130" y="22" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Chuyển động nhiệt phân tử</text>' +
        hats.map(function (pt) {
          return '<circle cx="' + pt.x + '" cy="' + pt.y + '" r="3.5" fill="var(--hl-nhan, #38bdf8)" />';
        }).join('') +
        // Cột đo nhiệt kế / động năng bên phải
        '<g transform="translate(280, 30)">' +
          '<rect x="0" y="0" width="40" height="120" rx="6" fill="currentColor" fill-opacity="0.05" stroke="currentColor" stroke-width="1.5" />' +
          '<rect x="4" y="' + (116 - barH) + '" width="32" height="' + barH + '" rx="4" fill="var(--hl-nguy-hiem, #ef4444)" />' +
          '<text x="20" y="140" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-nguy-hiem, #ef4444)">T = ' + T + ' K</text>' +
        '</g>' +
        // Thẻ kết quả Wđ
        '<g transform="translate(350, 45)">' +
          '<rect x="0" y="0" width="115" height="90" rx="8" fill="currentColor" fill-opacity="0.06" stroke="currentColor" stroke-width="1" />' +
          '<text x="10" y="24" font-size="11" font-weight="bold" fill="currentColor">W̄<tspan baseline-shift="sub" font-size="75%">đ</tspan> = 3/2·k·T</text>' +
          '<text x="10" y="48" font-size="13" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">' + wD + ' zJ</text>' +
          '<text x="10" y="72" font-size="10" fill="currentColor" opacity="0.6">Tỉ lệ thuận với T</text>' +
        '</g>' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Nhiệt độ chính là thước đo động năng tịnh tiến trung bình</text>';
    };
  };

  /**
   * K4: van-toc-can-quan-phuong (v_rms = √(3RT/M))
   */
  HLHinhChungMinh.cacHinh['van-toc-can-quan-phuong'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Nhiệt độ T:', min: 200, max: 1000, step: 50, macDinh: 300, donVi: 'K' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function (t) {
      var T = ctrl.sliderVal;
      // Vận tốc H2 (M=2) và O2 (M=32)
      var vRmsH2 = Math.round(Math.sqrt((3 * 8.314 * T) / 0.002));
      var vRmsO2 = Math.round(Math.sqrt((3 * 8.314 * T) / 0.032));
      ctrl.setChuThich('Vận tốc căn quân phương K4', 'T = ' + T + ' K → H₂: ' + vRmsH2 + ' m/s | O₂: ' + vRmsO2 + ' m/s');

      // Vị trí đường đua
      var xH2 = 60 + ((t * vRmsH2 * 0.1) % 360);
      var xO2 = 60 + ((t * vRmsO2 * 0.1) % 360);

      g.innerHTML =
        // Đường đua H2 (nhẹ)
        '<rect x="50" y="30" width="380" height="36" rx="6" fill="currentColor" fill-opacity="0.05" stroke="currentColor" stroke-width="1" />' +
        '<text x="60" y="24" font-size="11" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">Khí H₂ (M = 2 g/mol) - Nhẹ, bay nhanh:</text>' +
        '<circle cx="' + xH2 + '" cy="48" r="7" fill="var(--hl-nhan, #38bdf8)" />' +
        '<text x="390" y="52" text-anchor="end" font-size="11" font-mono font-weight="bold" fill="var(--hl-nhan, #38bdf8)">' + vRmsH2 + ' m/s</text>' +
        // Đường đua O2 (nặng)
        '<rect x="50" y="95" width="380" height="36" rx="6" fill="currentColor" fill-opacity="0.05" stroke="currentColor" stroke-width="1" />' +
        '<text x="60" y="89" font-size="11" font-weight="bold" fill="var(--hl-canh-bao, #f59e0b)">Khí O₂ (M = 32 g/mol) - Nặng hơn 16 lần:</text>' +
        '<circle cx="' + xO2 + '" cy="113" r="11" fill="var(--hl-canh-bao, #f59e0b)" />' +
        '<text x="390" y="117" text-anchor="end" font-size="11" font-mono font-weight="bold" fill="var(--hl-canh-bao, #f59e0b)">' + vRmsO2 + ' m/s</text>' +
        // Công thức chốt
        '<text x="' + (w/2) + '" y="' + (h - 16) + '" text-anchor="middle" font-size="12" font-weight="bold" fill="currentColor">v<tspan baseline-shift="sub" font-size="75%">rms</tspan> = √(3RT/M) → Cùng T, khí nhẹ hơn bay nhanh hơn gấp √(M₂/M₁)</text>';
    };
  };

  /**
   * K5: ap-suat-rieng-phan-dalton (p = Σ pi = xi · p)
   */
  HLHinhChungMinh.cacHinh['ap-suat-rieng-phan-dalton'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Phần mol x₁:', min: 0.1, max: 0.9, step: 0.05, macDinh: 0.6 });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var x1 = ctrl.sliderVal;
      var x2 = parseFloat((1 - x1).toFixed(2));
      var pTong = 2.0; // bar
      var p1 = (x1 * pTong).toFixed(2);
      var p2 = (x2 * pTong).toFixed(2);
      ctrl.setChuThich('Định luật Dalton K5', 'p = p₁ + p₂ = ' + p1 + ' + ' + p2 + ' = 2,00 bar');

      g.innerHTML =
        // Bình hỗn hợp
        '<rect x="40" y="30" width="220" height="120" rx="8" fill="currentColor" fill-opacity="0.05" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="150" y="22" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Bình chung thể tích V</text>' +
        // Các hạt khí A (xanh lam) và B (hổ phách)
        '<circle cx="80" cy="60" r="5" fill="var(--hl-nhan, #38bdf8)" />' +
        '<circle cx="120" cy="80" r="5" fill="var(--hl-nhan, #38bdf8)" />' +
        '<circle cx="170" cy="55" r="5" fill="var(--hl-nhan, #38bdf8)" />' +
        '<circle cx="70" cy="110" r="5" fill="var(--hl-nhan, #38bdf8)" />' +
        '<circle cx="190" cy="115" r="5" fill="var(--hl-canh-bao, #f59e0b)" />' +
        '<circle cx="140" cy="125" r="5" fill="var(--hl-canh-bao, #f59e0b)" />' +
        '<circle cx="100" cy="130" r="5" fill="var(--hl-canh-bao, #f59e0b)" />' +
        // Bảng áp suất riêng phần bên phải
        '<g transform="translate(285, 30)">' +
          '<rect x="0" y="0" width="160" height="120" rx="8" fill="currentColor" fill-opacity="0.06" stroke="currentColor" stroke-width="1" />' +
          '<text x="12" y="22" font-size="11" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">Khí A: p₁ = ' + p1 + ' bar</text>' +
          '<text x="12" y="38" font-size="10" fill="currentColor" opacity="0.7">x₁ = ' + x1 + '</text>' +
          '<line x1="12" y1="50" x2="148" y2="50" stroke="currentColor" stroke-opacity="0.2" />' +
          '<text x="12" y="70" font-size="11" font-weight="bold" fill="var(--hl-canh-bao, #f59e0b)">Khí B: p₂ = ' + p2 + ' bar</text>' +
          '<text x="12" y="86" font-size="10" fill="currentColor" opacity="0.7">x₂ = ' + x2 + '</text>' +
          '<line x1="12" y1="96" x2="148" y2="96" stroke="currentColor" stroke-opacity="0.2" />' +
          '<text x="12" y="112" font-size="11" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">p = Σ p<tspan baseline-shift="sub" font-size="75%">i</tspan> = 2,00 bar</text>' +
        '</g>' +
        '<text x="' + (w/2) + '" y="' + (h - 12) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Mỗi chất khí tác dụng áp suất độc lập như thể chiếm trọn bình một mình</text>';
    };
  };

  /**
   * K6: dang-qua-trinh-do-khong (Ngoại suy Charles về Độ không tuyệt đối -273,15 °C)
   */
  HLHinhChungMinh.cacHinh['dang-qua-trinh-do-khong'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Nhiệt độ t:', min: -100, max: 200, step: 10, macDinh: 50, donVi: '°C' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var tC = ctrl.sliderVal;
      ctrl.setChuThich('Độ không tuyệt đối K6', 'Ngoại suy V → 0 tại đúng t = −273,15 °C (0 K)');

      var oX = 80, oY = h - 45, gW = w - 120, gH = h - 75;
      // Trục t từ -300 °C đến +200 °C
      var x0 = oX + (273.15 / 500) * gW; // Điểm 0 °C
      var xAbs0 = oX; // Điểm -273.15 °C
      var xPoint = oX + ((tC + 273.15) / 500) * gW;
      var yPoint = oY - ((tC + 273.15) / 500) * gH;

      g.innerHTML =
        // Trục tọa độ
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + (oX + gW) + '" y2="' + oY + '" stroke="currentColor" stroke-width="1.5" />' +
        '<line x1="' + x0 + '" y1="' + oY + '" x2="' + x0 + '" y2="' + (oY - gH) + '" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + (oX + gW + 6) + '" y="' + (oY + 4) + '" font-size="10" font-weight="bold" fill="currentColor">t (°C)</text>' +
        '<text x="' + (x0 - 15) + '" y="' + (oY - gH - 6) + '" font-size="10" font-weight="bold" fill="currentColor">V (Lít)</text>' +
        // Vạch 0 °C
        '<text x="' + x0 + '" y="' + (oY + 14) + '" text-anchor="middle" font-size="10" fill="currentColor">0</text>' +
        // Đường ngoại suy Charles (đứt nét ở miền lạnh < 0 °C, liền ở miền > 0 °C)
        '<line x1="' + xAbs0 + '" y1="' + oY + '" x2="' + x0 + '" y2="' + (oY - (273.15/500)*gH) + '" stroke="var(--hl-canh-bao, #f59e0b)" stroke-width="2" stroke-dasharray="3,3" />' +
        '<line x1="' + x0 + '" y1="' + (oY - (273.15/500)*gH) + '" x2="' + (oX + gW) + '" y2="' + (oY - gH) + '" stroke="var(--hl-nhan, #38bdf8)" stroke-width="2.5" />' +
        // Điểm -273.15 °C
        '<circle cx="' + xAbs0 + '" cy="' + oY + '" r="5" fill="var(--hl-nguy-hiem, #ef4444)" />' +
        '<text x="' + xAbs0 + '" y="' + (oY + 16) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-nguy-hiem, #ef4444)">−273,15 °C (0 K)</text>' +
        // Điểm thực nghiệm hiện tại
        '<circle cx="' + xPoint + '" cy="' + yPoint + '" r="5" fill="var(--hl-thanh-cong, #10b981)" />' +
        '<text x="' + xPoint + '" y="' + (yPoint - 8) + '" text-anchor="middle" font-size="10" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">t = ' + tC + ' °C</text>' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Độ không tuyệt đối là điểm gốc của thang đo nhiệt độ Kelvin T = t + 273,15</text>';
    };
  };

  /**
   * K7: cong-dang-nhiet-tich-phan (Công giãn đẳng nhiệt A' = nRT ln(V₂/V₁))
   */
  HLHinhChungMinh.cacHinh['cong-dang-nhiet-tich-phan'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'V₂/V₁:', min: 1.5, max: 4.0, step: 0.1, macDinh: 2.5, donVi: 'lần' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function (t) {
      var rV = ctrl.sliderVal;
      var cong = (8.314 * 300 * Math.log(rV)).toFixed(0);
      ctrl.setChuThich('Công đẳng nhiệt K7', 'A\' = nRT·ln(V₂/V₁) = ' + cong + ' J (diện tích tích phân ∫ p dV)');

      var oX = 55, oY = h - 45, gW = w - 100, gH = h - 75;
      var x1 = oX + 30;
      var x2 = oX + 30 + (rV / 4.0) * (gW - 60);

      // Tạo các dải chữ nhật Riemann vi phân
      var dStripes = [];
      var nStripes = 16;
      for (var i = 0; i < nStripes; i++) {
        var sx = x1 + (i / nStripes) * (x2 - x1);
        var sw = (x2 - x1) / nStripes;
        var relV = 1.0 + (sx - x1) / (gW - 60) * 3.0;
        var sh = (1.0 / relV) * gH * 0.85;
        dStripes.push('<rect x="' + sx + '" y="' + (oY - sh) + '" width="' + (sw - 0.5) + '" height="' + sh + '" fill="var(--hl-nhan, #38bdf8)" fill-opacity="0.3" stroke="var(--hl-nhan, #38bdf8)" stroke-width="0.5" />');
      }

      g.innerHTML =
        // Trục tọa độ
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + (oX + gW) + '" y2="' + oY + '" stroke="currentColor" stroke-width="1.5" />' +
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + oX + '" y2="' + (oY - gH) + '" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + (oX + gW + 6) + '" y="' + (oY + 4) + '" font-size="11" font-weight="bold" fill="currentColor">V</text>' +
        '<text x="' + oX + '" y="' + (oY - gH - 6) + '" font-size="11" font-weight="bold" fill="currentColor">p</text>' +
        // Dải Riemann tích phân
        dStripes.join('') +
        // Đường cong hyperbol p = nRT/V
        '<path d="M ' + x1 + ' ' + (oY - gH*0.85) + ' Q ' + ((x1+x2)/2) + ' ' + (oY - gH*0.45) + ' ' + x2 + ' ' + (oY - (gH*0.85)/rV) + '" fill="none" stroke="var(--hl-nhan, #38bdf8)" stroke-width="2.5" />' +
        // Vạch V1, V2
        '<line x1="' + x1 + '" y1="' + oY + '" x2="' + x1 + '" y2="' + (oY - gH*0.85) + '" stroke="currentColor" stroke-dasharray="2,2" />' +
        '<line x1="' + x2 + '" y1="' + oY + '" x2="' + x2 + '" y2="' + (oY - (gH*0.85)/rV) + '" stroke="currentColor" stroke-dasharray="2,2" />' +
        '<text x="' + x1 + '" y="' + (oY + 14) + '" text-anchor="middle" font-size="10" font-weight="bold" fill="currentColor">V₁</text>' +
        '<text x="' + x2 + '" y="' + (oY + 14) + '" text-anchor="middle" font-size="10" font-weight="bold" fill="currentColor">V₂</text>' +
        // Nhãn diện tích
        '<text x="' + ((x1+x2)/2) + '" y="' + (oY - 20) + '" text-anchor="middle" font-size="12" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">Diện tích = ∫ p·dV = ' + cong + ' J</text>' +
        '<text x="' + (w/2) + '" y="' + (h - 12) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Công sinh ra bằng chính diện tích dưới đường hyperbol đẳng nhiệt</text>';
    };
  };

  /**
   * K8: khi-thuc-van-der-waals ((p + an²/V²)(V - nb) = nRT)
   */
  HLHinhChungMinh.cacHinh['khi-thuc-van-der-waals'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Mật độ hạt:', min: 1, max: 4, step: 1, macDinh: 2 });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function (t) {
      ctrl.setChuThich('Khí thực Van der Waals K8', 'nb: Thể tích riêng phân tử | an²/V²: Lực hút nội phân tử kéo lùi hạt ở thành');

      g.innerHTML =
        // Buồng 1: Khí lý tưởng
        '<g transform="translate(40, 30)">' +
          '<rect x="0" y="0" width="180" height="110" rx="8" fill="currentColor" fill-opacity="0.05" stroke="currentColor" stroke-width="1.5" />' +
          '<text x="90" y="20" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">Khí lý tưởng</text>' +
          '<circle cx="45" cy="55" r="3" fill="var(--hl-thanh-cong, #10b981)" />' +
          '<circle cx="95" cy="70" r="3" fill="var(--hl-thanh-cong, #10b981)" />' +
          '<circle cx="140" cy="50" r="3" fill="var(--hl-thanh-cong, #10b981)" />' +
          '<text x="90" y="100" text-anchor="middle" font-size="10" fill="currentColor" opacity="0.7">Chất điểm (b = 0), không hút (a = 0)</text>' +
        '</g>' +
        // Buồng 2: Khí thực Van der Waals
        '<g transform="translate(260, 30)">' +
          '<rect x="0" y="0" width="180" height="110" rx="8" fill="currentColor" fill-opacity="0.05" stroke="currentColor" stroke-width="1.5" />' +
          '<text x="90" y="20" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-canh-bao, #f59e0b)">Khí thực</text>' +
          // Vùng cấm b quanh hạt
          '<circle cx="45" cy="55" r="10" fill="var(--hl-canh-bao, #f59e0b)" fill-opacity="0.2" stroke="var(--hl-canh-bao, #f59e0b)" stroke-dasharray="2,2" />' +
          '<circle cx="45" cy="55" r="4" fill="var(--hl-canh-bao, #f59e0b)" />' +
          '<circle cx="130" cy="65" r="10" fill="var(--hl-canh-bao, #f59e0b)" fill-opacity="0.2" stroke="var(--hl-canh-bao, #f59e0b)" stroke-dasharray="2,2" />' +
          '<circle cx="130" cy="65" r="4" fill="var(--hl-canh-bao, #f59e0b)" />' +
          // Mũi tên lực hút kéo lùi hạt ở thành
          '<line x1="160" y1="50" x2="140" y2="58" stroke="var(--hl-nguy-hiem, #ef4444)" stroke-width="2" marker-end="url(#arr-nguy-hiem)" />' +
          '<circle cx="165" cy="48" r="4" fill="var(--hl-nguy-hiem, #ef4444)" />' +
          '<text x="90" y="100" text-anchor="middle" font-size="10" fill="currentColor" opacity="0.7">Có thể tích riêng nb &amp; lực hút a</text>' +
        '</g>' +
        '<text x="' + (w/2) + '" y="' + (h - 12) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">(p + a·n²/V²)(V − n·b) = nRT</text>';
    };
  };

  // =========================================================================
  // KHỐI 2: NHIỆT ĐỘNG HỌC (N1 - N8)
  // =========================================================================

  /**
   * N1: can-bang-nang-luong-dl1 (ΔU = Q + A)
   */
  HLHinhChungMinh.cacHinh['can-bang-nang-luong-dl1'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Nhiệt lượng Q:', min: -50, max: 100, step: 5, macDinh: 40, donVi: 'J' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function (t) {
      var Q = ctrl.sliderVal;
      var A = -25; // Giãn nở sinh công
      var dU = Q + A;
      ctrl.setChuThich('Định luật I Nhiệt động học N1', 'ΔU = Q + A = ' + Q + ' + (' + A + ') = ' + dU + ' J');

      g.innerHTML =
        // Xi-lanh ở giữa
        '<g transform="translate(140, 35)">' +
          '<rect x="0" y="0" width="160" height="90" rx="4" fill="currentColor" fill-opacity="0.08" stroke="currentColor" stroke-width="2" />' +
          '<rect x="110" y="3" width="16" height="84" rx="2" fill="currentColor" fill-opacity="0.6" />' +
          '<text x="60" y="50" text-anchor="middle" font-size="14" font-weight="bold" fill="currentColor">Hệ khí (U)</text>' +
        '</g>' +
        // Mũi tên nhiệt Q
        '<g transform="translate(60, 65)">' +
          '<line x1="0" y1="15" x2="70" y2="15" stroke="' + (Q >= 0 ? 'var(--hl-nguy-hiem, #ef4444)' : 'var(--hl-nhan, #38bdf8)') + '" stroke-width="3" marker-end="url(#' + (Q >= 0 ? 'arr-nguy-hiem' : 'arr-nhan') + ')" />' +
          '<text x="35" y="5" text-anchor="middle" font-size="11" font-weight="bold" fill="' + (Q >= 0 ? 'var(--hl-nguy-hiem, #ef4444)' : 'var(--hl-nhan, #38bdf8)') + '">Q = ' + Q + ' J</text>' +
        '</g>' +
        // Mũi tên công A' sinh ra
        '<g transform="translate(315, 65)">' +
          '<line x1="0" y1="15" x2="75" y2="15" stroke="var(--hl-thanh-cong, #10b981)" stroke-width="3" marker-end="url(#arr-thanh-cong)" />' +
          '<text x="40" y="5" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">A\' = 25 J (Công sinh)</text>' +
        '</g>' +
        // Thanh hiển thị ΔU
        '<text x="' + (w/2) + '" y="' + (h - 14) + '" text-anchor="middle" font-size="12" font-weight="bold" fill="currentColor">ΔU = Q + A = Q − A\' → Nội năng tăng ' + dU + ' J</text>';
    };
  };

  /**
   * N2: pit-tong-dx (Công cơ học khí δA' = p S dx = p dV)
   */
  HLHinhChungMinh.cacHinh['pit-tong-dx'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Dịch chuyển dx:', min: 10, max: 60, step: 5, macDinh: 35, donVi: 'mm' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var dx = ctrl.sliderVal;
      ctrl.setChuThich('Công cơ học N2', 'δA\' = F·dx = (p·S)·dx = p·dV');

      var x0 = 50, yTop = 30, cylH = 85, pX1 = 130;
      var pX2 = pX1 + dx;

      g.innerHTML =
        // Vỏ xi lanh
        '<line x1="' + x0 + '" y1="' + yTop + '" x2="' + (w - 40) + '" y2="' + yTop + '" stroke="currentColor" stroke-width="2" />' +
        '<line x1="' + x0 + '" y1="' + (yTop + cylH) + '" x2="' + (w - 40) + '" y2="' + (yTop + cylH) + '" stroke="currentColor" stroke-width="2" />' +
        '<line x1="' + x0 + '" y1="' + yTop + '" x2="' + x0 + '" y2="' + (yTop + cylH) + '" stroke="currentColor" stroke-width="3" />' +
        // Khối khí ban đầu
        '<rect x="' + x0 + '" y="' + yTop + '" width="' + (pX1 - x0) + '" height="' + cylH + '" fill="var(--hl-nhan, #38bdf8)" fill-opacity="0.15" />' +
        '<text x="' + ((x0 + pX1)/2) + '" y="' + (yTop + cylH/2 + 5) + '" text-anchor="middle" font-size="13" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">Áp suất p</text>' +
        // Vùng dV = S·dx
        '<rect x="' + pX1 + '" y="' + yTop + '" width="' + dx + '" height="' + cylH + '" fill="var(--hl-thanh-cong, #10b981)" fill-opacity="0.25" stroke="var(--hl-thanh-cong, #10b981)" stroke-dasharray="2,2" />' +
        '<text x="' + (pX1 + dx/2) + '" y="' + (yTop + cylH/2 + 5) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">dV</text>' +
        // Pít-tông
        '<rect x="' + pX2 + '" y="' + (yTop + 2) + '" width="14" height="' + (cylH - 4) + '" fill="currentColor" fill-opacity="0.75" rx="2" stroke="currentColor" />' +
        '<rect x="' + (pX2 + 14) + '" y="' + (yTop + cylH/2 - 6) + '" width="80" height="12" fill="currentColor" fill-opacity="0.6" rx="2" />' +
        // Thước dx
        '<line x1="' + pX1 + '" y1="' + (yTop + cylH + 18) + '" x2="' + pX2 + '" y2="' + (yTop + cylH + 18) + '" stroke="var(--hl-thanh-cong, #10b981)" stroke-width="2" marker-end="url(#arr-thanh-cong)" />' +
        '<text x="' + (pX1 + dx/2) + '" y="' + (yTop + cylH + 32) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">dx = ' + dx + ' mm</text>' +
        '<text x="' + (w/2 + 30) + '" y="' + (h - 8) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">A\' = ∫ p·dV (Công bằng diện tích dưới đường biến đổi trạng thái)</text>';
    };
  };

  /**
   * N3: bac-tu-do-noi-nang (U = f/2 nRT, Cv = f/2 R)
   */
  HLHinhChungMinh.cacHinh['bac-tu-do-noi-nang'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Loại phân tử:', min: 3, max: 6, step: 2, macDinh: 3 });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function (t) {
      var f = ctrl.sliderVal === 3 ? 3 : 5; // 3 hoặc 5
      ctrl.setChuThich('Bậc tự do N3', (f === 3 ? 'Đơn nguyên tử (He, Ne, Ar): f = 3 (chỉ tịnh tiến) → Cv = 1,5 R' : 'Lưỡng nguyên tử (N₂, O₂, H₂): f = 5 (3 tịnh tiến + 2 quay) → Cv = 2,5 R'));

      var cx = 130, cy = 90;
      var rot = t * 1.5;

      g.innerHTML =
        // Hộp minh hoạ phân tử
        '<rect x="40" y="30" width="180" height="120" rx="8" fill="currentColor" fill-opacity="0.05" stroke="currentColor" stroke-width="1.5" />' +
        (f === 3
          // Đơn nguyên tử
          ? '<circle cx="' + cx + '" cy="' + cy + '" r="14" fill="var(--hl-nhan, #38bdf8)" stroke="currentColor" stroke-width="1.5" />' +
            '<line x1="' + cx + '" y1="' + cy + '" x2="' + (cx + 35) + '" y2="' + cy + '" stroke="var(--hl-nguy-hiem, #ef4444)" stroke-width="2" marker-end="url(#arr-nguy-hiem)" />' +
            '<line x1="' + cx + '" y1="' + cy + '" x2="' + cx + '" y2="' + (cy - 35) + '" stroke="var(--hl-thanh-cong, #10b981)" stroke-width="2" marker-end="url(#arr-thanh-cong)" />' +
            '<text x="' + cx + '" y="' + (cy + 36) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">3 bậc tịnh tiến (x, y, z)</text>'
          // Lưỡng nguyên tử
          : '<g transform="rotate(' + (rot * 50) + ' ' + cx + ' ' + cy + ')">' +
              '<circle cx="' + (cx - 20) + '" cy="' + cy + '" r="12" fill="var(--hl-canh-bao, #f59e0b)" stroke="currentColor" stroke-width="1.5" />' +
              '<circle cx="' + (cx + 20) + '" cy="' + cy + '" r="12" fill="var(--hl-canh-bao, #f59e0b)" stroke="currentColor" stroke-width="1.5" />' +
              '<line x1="' + (cx - 20) + '" y1="' + cy + '" x2="' + (cx + 20) + '" y2="' + cy + '" stroke="currentColor" stroke-width="3" />' +
            '</g>' +
            '<text x="' + cx + '" y="' + (cy + 38) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">3 tịnh tiến + 2 quay (f = 5)</text>'
        ) +
        // Bảng năng lượng bên phải
        '<g transform="translate(250, 35)">' +
          '<rect x="0" y="0" width="180" height="110" rx="8" fill="currentColor" fill-opacity="0.06" stroke="currentColor" stroke-width="1" />' +
          '<text x="12" y="24" font-size="11" font-weight="bold" fill="currentColor">Bậc tự do: f = ' + f + '</text>' +
          '<text x="12" y="48" font-size="12" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">U = ' + (f/2).toFixed(1) + ' · n·R·T</text>' +
          '<text x="12" y="72" font-size="12" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">C<tspan baseline-shift="sub" font-size="75%">v</tspan> = ' + (f/2).toFixed(1) + ' · R</text>' +
          '<text x="12" y="94" font-size="10" fill="currentColor" opacity="0.6">Mỗi bậc tích lũy ½ nRT</text>' +
        '</g>' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Khí lí tưởng: Nội năng chỉ phụ thuộc vào nhiệt độ và bậc tự do phân tử</text>';
    };
  };

  /**
   * N4: he-thuc-mayer (Cp - Cv = R)
   */
  HLHinhChungMinh.cacHinh['he-thuc-mayer'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Độ tăng ΔT:', min: 10, max: 100, step: 10, macDinh: 50, donVi: 'K' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var dT = ctrl.sliderVal;
      ctrl.setChuThich('Hệ thức Mayer N4', 'Q_p = Q_v + A\' = n·Cv·ΔT + n·R·ΔT → Cp − Cv = R');

      g.innerHTML =
        // Bình 1: Đẳng tích Cv (Chốt cứng pít-tông)
        '<g transform="translate(45, 30)">' +
          '<rect x="0" y="0" width="160" height="110" rx="4" fill="currentColor" fill-opacity="0.06" stroke="currentColor" stroke-width="1.5" />' +
          '<rect x="0" y="30" width="160" height="12" fill="currentColor" fill-opacity="0.6" />' +
          '<circle cx="30" cy="36" r="4" fill="var(--hl-nguy-hiem, #ef4444)" />' +
          '<circle cx="130" cy="36" r="4" fill="var(--hl-nguy-hiem, #ef4444)" />' +
          '<text x="80" y="20" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Đẳng tích (Cv): Chốt khoá</text>' +
          '<text x="80" y="80" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">A\' = 0 → Q = ΔU</text>' +
        '</g>' +
        // Bình 2: Đẳng áp Cp (Pít-tông tự do dâng cao)
        '<g transform="translate(255, 30)">' +
          '<rect x="0" y="0" width="160" height="110" rx="4" fill="currentColor" fill-opacity="0.06" stroke="currentColor" stroke-width="1.5" />' +
          '<rect x="0" y="' + (30 - dT*0.2) + '" width="160" height="12" fill="currentColor" fill-opacity="0.6" />' +
          '<text x="80" y="20" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Đẳng áp (Cp): Dâng cao</text>' +
          '<text x="80" y="80" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">Q = ΔU + n·R·ΔT</text>' +
        '</g>' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Đẳng áp cần thêm nhiệt lượng n·R·ΔT để sinh công đẩy khí quyển</text>';
    };
  };

  /**
   * N5: nen-doan-nhiet-poisson (TV^(γ-1) = const, pV^γ = const)
   */
  HLHinhChungMinh.cacHinh['nen-doan-nhiet-poisson'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Thể tích V:', min: 1.0, max: 3.0, step: 0.1, macDinh: 1.5, donVi: 'L' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var V = ctrl.sliderVal;
      ctrl.setChuThich('Đoạn nhiệt Poisson N5', 'Đường đoạn nhiệt (pV^γ = const, γ = 1,4) dốc hơn đường đẳng nhiệt (pV = const)');

      var oX = 60, oY = h - 45, gW = w - 120, gH = h - 75;
      g.innerHTML =
        // Trục toạ độ
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + (oX + gW) + '" y2="' + oY + '" stroke="currentColor" stroke-width="1.5" />' +
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + oX + '" y2="' + (oY - gH) + '" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + (oX + gW + 6) + '" y="' + (oY + 4) + '" font-size="11" font-weight="bold" fill="currentColor">V</text>' +
        '<text x="' + oX + '" y="' + (oY - gH - 6) + '" font-size="11" font-weight="bold" fill="currentColor">p</text>' +
        // Đường đẳng nhiệt (màu xanh)
        '<path d="M ' + (oX + 30) + ' ' + (oY - gH*0.8) + ' Q ' + (oX + 100) + ' ' + (oY - gH*0.35) + ' ' + (oX + gW - 20) + ' ' + (oY - 15) + '" fill="none" stroke="var(--hl-nhan, #38bdf8)" stroke-width="2" />' +
        '<text x="' + (oX + gW - 30) + '" y="' + (oY - 22) + '" font-size="10" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">Đẳng nhiệt (γ = 1)</text>' +
        // Đường đoạn nhiệt (màu đỏ, dốc hơn hẳn)
        '<path d="M ' + (oX + 30) + ' ' + (oY - gH*0.95) + ' Q ' + (oX + 70) + ' ' + (oY - gH*0.25) + ' ' + (oX + gW - 50) + ' ' + (oY - 10) + '" fill="none" stroke="var(--hl-nguy-hiem, #ef4444)" stroke-width="2.5" />' +
        '<text x="' + (oX + 75) + '" y="' + (oY - gH*0.6) + '" font-size="10" font-weight="bold" fill="var(--hl-nguy-hiem, #ef4444)">Đoạn nhiệt (γ = 1,4)</text>' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Nén cách nhiệt (δQ = 0): Công nhận vào làm nóng khối khí → p tăng vọt</text>';
    };
  };

  /**
   * N6: so-do-dong-co-nhiet (η = 1 - Q2/Q1 = A'/Q1)
   */
  HLHinhChungMinh.cacHinh['so-do-dong-co-nhiet'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Nhiệt nguồn nóng T₁:', min: 400, max: 1200, step: 50, macDinh: 800, donVi: 'K' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function (t) {
      var T1 = ctrl.sliderVal;
      var T2 = 300;
      var eta = ((1 - T2 / T1) * 100).toFixed(1);
      ctrl.setChuThich('Động cơ nhiệt N6', 'Hiệu suất Carnot cực đại η_max = 1 − T₂/T₁ = ' + eta + '%');

      var rot = (t * 120) % 360;

      g.innerHTML =
        // Nguồn nóng trên
        '<rect x="' + (w/2 - 70) + '" y="20" width="140" height="30" rx="6" fill="var(--hl-nguy-hiem, #ef4444)" fill-opacity="0.2" stroke="var(--hl-nguy-hiem, #ef4444)" stroke-width="1.5" />' +
        '<text x="' + (w/2) + '" y="40" text-anchor="middle" font-size="12" font-weight="bold" fill="var(--hl-nguy-hiem, #ef4444)">Nguồn nóng (T₁ = ' + T1 + ' K)</text>' +
        // Luồng nhiệt Q1 đi xuống
        '<line x1="' + (w/2) + '" y1="50" x2="' + (w/2) + '" y2="78" stroke="var(--hl-nguy-hiem, #ef4444)" stroke-width="4" marker-end="url(#arr-nguy-hiem)" />' +
        // Động cơ nhiệt ở giữa
        '<circle cx="' + (w/2) + '" cy="98" r="20" fill="currentColor" fill-opacity="0.1" stroke="currentColor" stroke-width="2" />' +
        '<text x="' + (w/2) + '" y="103" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">ĐC</text>' +
        // Công A' quay bánh đà sang phải
        '<line x1="' + (w/2 + 20) + '" y1="98" x2="' + (w/2 + 80) + '" y2="98" stroke="var(--hl-thanh-cong, #10b981)" stroke-width="3" marker-end="url(#arr-thanh-cong)" />' +
        '<g transform="translate(' + (w/2 + 105) + ', 98) rotate(' + rot + ')">' +
          '<circle cx="0" cy="0" r="14" fill="none" stroke="var(--hl-thanh-cong, #10b981)" stroke-width="2" />' +
          '<line x1="-14" y1="0" x2="14" y2="0" stroke="var(--hl-thanh-cong, #10b981)" />' +
        '</g>' +
        '<text x="' + (w/2 + 105) + '" y="125" text-anchor="middle" font-size="10" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">A\' = ' + eta + '%</text>' +
        // Luồng nhiệt thải Q2 đi xuống
        '<line x1="' + (w/2) + '" y1="118" x2="' + (w/2) + '" y2="145" stroke="var(--hl-nhan, #38bdf8)" stroke-width="3" marker-end="url(#arr-nhan)" />' +
        // Nguồn lạnh dưới
        '<rect x="' + (w/2 - 70) + '" y="145" width="140" height="30" rx="6" fill="var(--hl-nhan, #38bdf8)" fill-opacity="0.2" stroke="var(--hl-nhan, #38bdf8)" stroke-width="1.5" />' +
        '<text x="' + (w/2) + '" y="165" text-anchor="middle" font-size="12" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">Nguồn lạnh (T₂ = 300 K)</text>' +
        '<text x="' + (w/2) + '" y="' + (h - 6) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Bắt buộc phải nhả nhiệt Q₂ cho nguồn lạnh (Định luật II)</text>';
    };
  };

  /**
   * N7: chu-trinh-otto-pv (Chu trình Otto 4 kỳ η = 1 - 1/r^(γ-1))
   */
  HLHinhChungMinh.cacHinh['chu-trinh-otto-pv'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Tỉ số nén r:', min: 4, max: 12, step: 0.5, macDinh: 8 });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function (t) {
      var r = ctrl.sliderVal;
      var eta = ((1 - 1 / Math.pow(r, 0.4)) * 100).toFixed(1);
      ctrl.setChuThich('Chu trình Otto N7', 'r = ' + r + ' → Hiệu suất nhiệt lý thuyết η_Otto = ' + eta + '%');

      var oX = 60, oY = h - 45, gW = w - 120, gH = h - 75;
      var x1 = oX + gW - 30; // Điểm 1 (V1 lớn nhất)
      var x2 = oX + 40 + (12 - r) * 6; // Điểm 2 (V2 nhỏ)
      var y1 = oY - 20;
      var y2 = oY - 65;
      var y3 = oY - gH + 10;
      var y4 = oY - 45;

      g.innerHTML =
        // Trục toạ độ
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + (oX + gW) + '" y2="' + oY + '" stroke="currentColor" stroke-width="1.5" />' +
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + oX + '" y2="' + (oY - gH) + '" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + (oX + gW + 6) + '" y="' + (oY + 4) + '" font-size="11" font-weight="bold" fill="currentColor">V</text>' +
        '<text x="' + oX + '" y="' + (oY - gH - 6) + '" font-size="11" font-weight="bold" fill="currentColor">p</text>' +
        // Chu trình Otto (1-2 nén đoạn nhiệt, 2-3 cháy đẳng tích, 3-4 sinh công đoạn nhiệt, 4-1 xả đẳng tích)
        '<path d="M ' + x1 + ' ' + y1 + ' Q ' + ((x1+x2)/2) + ' ' + (y1-15) + ' ' + x2 + ' ' + y2 + ' L ' + x2 + ' ' + y3 + ' Q ' + ((x1+x2)/2) + ' ' + (y3+25) + ' ' + x1 + ' ' + y4 + ' Z" fill="var(--hl-nhan, #38bdf8)" fill-opacity="0.15" stroke="var(--hl-nhan, #38bdf8)" stroke-width="2.5" />' +
        // Các điểm trạng thái
        '<circle cx="' + x1 + '" cy="' + y1 + '" r="4" fill="currentColor" />' +
        '<text x="' + (x1 + 10) + '" y="' + y1 + '" font-size="10" font-weight="bold" fill="currentColor">1</text>' +
        '<circle cx="' + x2 + '" cy="' + y2 + '" r="4" fill="currentColor" />' +
        '<text x="' + (x2 - 12) + '" y="' + y2 + '" font-size="10" font-weight="bold" fill="currentColor">2</text>' +
        '<circle cx="' + x2 + '" cy="' + y3 + '" r="4" fill="var(--hl-nguy-hiem, #ef4444)" />' +
        '<text x="' + (x2 - 12) + '" y="' + y3 + '" font-size="10" font-weight="bold" fill="var(--hl-nguy-hiem, #ef4444)">3</text>' +
        '<circle cx="' + x1 + '" cy="' + y4 + '" r="4" fill="currentColor" />' +
        '<text x="' + (x1 + 10) + '" y="' + y4 + '" font-size="10" font-weight="bold" fill="currentColor">4</text>' +
        // Vạch nạp nhiệt Q1 và nhả nhiệt Q2
        '<line x1="' + x2 + '" y1="' + (y2 - 5) + '" x2="' + x2 + '" y2="' + (y3 + 5) + '" stroke="var(--hl-nguy-hiem, #ef4444)" stroke-width="3" />' +
        '<text x="' + (x2 - 25) + '" y="' + ((y2+y3)/2) + '" font-size="10" font-weight="bold" fill="var(--hl-nguy-hiem, #ef4444)">+Q₁</text>' +
        '<text x="' + (x1 + 15) + '" y="' + ((y1+y4)/2) + '" font-size="10" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">−Q₂</text>' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Tỉ số nén r càng cao, chu trình càng phình rộng → hiệu suất càng lớn</text>';
    };
  };

  /**
   * N8: chu-trinh-carnot-so-sanh (Hiệu suất Carnot cực đại η_max = 1 - TL/TH)
   */
  HLHinhChungMinh.cacHinh['chu-trinh-carnot-so-sanh'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Nguồn nóng TH:', min: 500, max: 1500, step: 50, macDinh: 900, donVi: 'K' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var TH = ctrl.sliderVal;
      var TL = 300;
      var eta = ((1 - TL / TH) * 100).toFixed(1);
      ctrl.setChuThich('Định lý Carnot N8', 'Không động cơ nhiệt nào hoạt động giữa 2 nguồn nhiệt đạt hiệu suất cao hơn Carnot: ' + eta + '%');

      var oX = 60, oY = h - 45, gW = w - 120, gH = h - 75;
      g.innerHTML =
        // Giản đồ T-S của chu trình Carnot (Hình chữ nhật hoàn hảo)
        '<rect x="' + (oX + 40) + '" y="' + (oY - (TH/1500)*gH) + '" width="140" height="' + (((TH - TL)/1500)*gH) + '" fill="var(--hl-thanh-cong, #10b981)" fill-opacity="0.2" stroke="var(--hl-thanh-cong, #10b981)" stroke-width="2" />' +
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + (oX + gW) + '" y2="' + oY + '" stroke="currentColor" stroke-width="1.5" />' +
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + oX + '" y2="' + (oY - gH) + '" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + (oX + gW + 6) + '" y="' + (oY + 4) + '" font-size="11" font-weight="bold" fill="currentColor">S (Entropy)</text>' +
        '<text x="' + oX + '" y="' + (oY - gH - 6) + '" font-size="11" font-weight="bold" fill="currentColor">T (Nhiệt độ)</text>' +
        // Vạch TH, TL
        '<text x="' + (oX - 10) + '" y="' + (oY - (TH/1500)*gH + 4) + '" text-anchor="end" font-size="10" font-weight="bold" fill="var(--hl-nguy-hiem, #ef4444)">T_H = ' + TH + ' K</text>' +
        '<text x="' + (oX - 10) + '" y="' + (oY - (TL/1500)*gH + 4) + '" text-anchor="end" font-size="10" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">T_L = 300 K</text>' +
        '<text x="' + (oX + 110) + '" y="' + (oY - ((TH+TL)/3000)*gH + 4) + '" text-anchor="middle" font-size="12" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">Công A\' = ΔS·(T_H − T_L)</text>' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">η_max = 1 − T_L / T_H (Chỉ phụ thuộc 2 nhiệt độ nguồn, không phụ thuộc tác chất)</text>';
    };
  };

  // =========================================================================
  // KHỐI 3: NHIỆT HOÁ HỌC (H1 - H4)
  // =========================================================================

  /**
   * H1: chu-trinh-hess (ΔrH° = Σ ΔfH°(sp) - Σ ΔfH°(đầu))
   */
  HLHinhChungMinh.cacHinh['chu-trinh-hess'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Tiến trình phản ứng:', min: 0, max: 100, step: 5, macDinh: 50, donVi: '%' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function (t) {
      var xA = 70, yA = 45;
      var xB = w - 70, yB = 45;
      var xC = w / 2, yC = h - 50;
      ctrl.setChuThich('Định luật Hess H1', 'Enthalpy là hàm trạng thái: Đường trực tiếp = Đường gián tiếp qua đơn chất');

      var pct = (t % 3) / 3;
      var dotDirX = xA + (xB - xA) * pct;

      g.innerHTML =
        // Khối Chất đầu
        '<rect x="' + (xA - 45) + '" y="' + (yA - 20) + '" width="90" height="38" rx="6" fill="currentColor" fill-opacity="0.1" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + xA + '" y="' + (yA + 4) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Chất đầu</text>' +
        // Khối Sản phẩm
        '<rect x="' + (xB - 45) + '" y="' + (yB - 20) + '" width="90" height="38" rx="6" fill="currentColor" fill-opacity="0.1" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + xB + '" y="' + (yB + 4) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Sản phẩm</text>' +
        // Khối Đơn chất bền
        '<rect x="' + (xC - 70) + '" y="' + (yC - 20) + '" width="140" height="38" rx="6" fill="currentColor" fill-opacity="0.1" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + xC + '" y="' + (yC + 4) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Đơn chất bền chuẩn</text>' +
        // Đường trực tiếp ΔrH°
        '<line x1="' + (xA + 45) + '" y1="' + yA + '" x2="' + (xB - 45) + '" y2="' + yB + '" stroke="var(--hl-nhan, #38bdf8)" stroke-width="3" marker-end="url(#arr-nhan)" />' +
        '<text x="' + xC + '" y="' + (yA - 8) + '" text-anchor="middle" font-size="12" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">Trực tiếp: Δ<tspan baseline-shift="sub" font-size="75%">r</tspan>H°</text>' +
        '<circle cx="' + dotDirX + '" cy="' + yA + '" r="4" fill="var(--hl-nhan, #38bdf8)" />' +
        // Nhánh gián tiếp 1
        '<line x1="' + xA + '" y1="' + (yA + 18) + '" x2="' + (xC - 50) + '" y2="' + (yC - 10) + '" stroke="var(--hl-canh-bao, #f59e0b)" stroke-width="2" stroke-dasharray="3,3" marker-end="url(#arr-canh-bao)" />' +
        '<text x="' + (xA + 15) + '" y="' + (yC - 12) + '" font-size="10" font-weight="bold" fill="var(--hl-canh-bao, #f59e0b)">−Σ Δ<tspan baseline-shift="sub" font-size="75%">f</tspan>H°(đầu)</text>' +
        // Nhánh gián tiếp 2
        '<line x1="' + (xC + 50) + '" y1="' + (yC - 10) + '" x2="' + xB + '" y2="' + (yB + 18) + '" stroke="var(--hl-thanh-cong, #10b981)" stroke-width="2" stroke-dasharray="3,3" marker-end="url(#arr-thanh-cong)" />' +
        '<text x="' + (xB - 15) + '" y="' + (yC - 12) + '" font-size="10" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">+Σ Δ<tspan baseline-shift="sub" font-size="75%">f</tspan>H°(sp)</text>' +
        '<text x="' + xC + '" y="' + (h - 8) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Δ<tspan baseline-shift="sub" font-size="75%">r</tspan>H° = Σ Δ<tspan baseline-shift="sub" font-size="75%">f</tspan>H°(sp) − Σ Δ<tspan baseline-shift="sub" font-size="75%">f</tspan>H°(chất đầu)</text>';
    };
  };

  /**
   * H2: nang-luong-lien-ket (ΔrH° = Σ Eb(đầu) - Σ Eb(sp))
   */
  HLHinhChungMinh.cacHinh['nang-luong-lien-ket'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Năng lượng bẻ Eb:', min: 200, max: 800, step: 20, macDinh: 436, donVi: 'kJ' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function (t) {
      ctrl.setChuThich('Năng lượng liên kết H2', 'Bẻ gãy liên kết chất đầu (+Eb thu nhiệt) → Tái hợp tạo sản phẩm (−Eb toả nhiệt)');

      var yBase = 120;
      var yAtoms = 40;
      var yProd = 145;

      g.innerHTML =
        // Mức chất đầu
        '<line x1="40" y1="' + yBase + '" x2="130" y2="' + yBase + '" stroke="currentColor" stroke-width="3" />' +
        '<text x="85" y="' + (yBase - 8) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Chất đầu (H₂ + Cl₂)</text>' +
        // Mũi tên bẻ gãy liên kết (thu nhiệt)
        '<line x1="85" y1="' + yBase + '" x2="85" y2="' + yAtoms + '" stroke="var(--hl-canh-bao, #f59e0b)" stroke-width="2.5" marker-end="url(#arr-canh-bao)" />' +
        '<text x="95" y="' + ((yBase+yAtoms)/2) + '" font-size="10" font-weight="bold" fill="var(--hl-canh-bao, #f59e0b)">+Σ E<tspan baseline-shift="sub" font-size="75%">b</tspan>(đầu) (Bẻ gãy)</text>' +
        // Mức nguyên tử tự do trên đỉnh
        '<line x1="160" y1="' + yAtoms + '" x2="280" y2="' + yAtoms + '" stroke="var(--hl-nguy-hiem, #ef4444)" stroke-dasharray="3,3" stroke-width="2" />' +
        '<text x="220" y="' + (yAtoms - 8) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-nguy-hiem, #ef4444)">Nguyên tử tự do (2H + 2Cl)</text>' +
        // Mũi tên ghép liên kết tạo sản phẩm (toả nhiệt)
        '<line x1="280" y1="' + yAtoms + '" x2="280" y2="' + yProd + '" stroke="var(--hl-thanh-cong, #10b981)" stroke-width="2.5" marker-end="url(#arr-thanh-cong)" />' +
        '<text x="290" y="' + ((yAtoms+yProd)/2) + '" font-size="10" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">−Σ E<tspan baseline-shift="sub" font-size="75%">b</tspan>(sp) (Tạo liên kết)</text>' +
        // Mức sản phẩm
        '<line x1="280" y1="' + yProd + '" x2="380" y2="' + yProd + '" stroke="currentColor" stroke-width="3" />' +
        '<text x="330" y="' + (yProd + 16) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Sản phẩm (2HCl)</text>' +
        // Độ chênh lệch ΔrH°
        '<line x1="410" y1="' + yBase + '" x2="410" y2="' + yProd + '" stroke="var(--hl-nhan, #38bdf8)" stroke-width="2" marker-end="url(#arr-nhan)" />' +
        '<text x="420" y="' + ((yBase+yProd)/2 + 4) + '" font-size="11" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">Δ<tspan baseline-shift="sub" font-size="75%">r</tspan>H° &lt; 0</text>' +
        '<text x="' + (w/2) + '" y="' + (h - 8) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Δ<tspan baseline-shift="sub" font-size="75%">r</tspan>H°₂₉₈ = Σ E<tspan baseline-shift="sub" font-size="75%">b</tspan>(chất đầu) − Σ E<tspan baseline-shift="sub" font-size="75%">b</tspan>(sản phẩm)</text>';
    };
  };

  /**
   * H3: nhiet-chay-nhien-lieu (LHV & AFR)
   */
  HLHinhChungMinh.cacHinh['nhiet-chay-nhien-lieu'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Hệ số dư gió λ:', min: 0.8, max: 1.4, step: 0.05, macDinh: 1.0 });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function (t) {
      var lam = ctrl.sliderVal;
      var afr = (15.1 * lam).toFixed(1);
      ctrl.setChuThich('Nhiệt trị & Tỉ lệ gió H3', 'Tỉ lệ không khí/nhiên liệu AFR = ' + afr + ' kg gió / kg xăng');

      g.innerHTML =
        // Buồng đốt trung tâm
        '<g transform="translate(160, 40)">' +
          '<rect x="0" y="0" width="160" height="90" rx="10" fill="var(--hl-nguy-hiem, #ef4444)" fill-opacity="0.15" stroke="var(--hl-nguy-hiem, #ef4444)" stroke-width="2" />' +
          '<text x="80" y="35" text-anchor="middle" font-size="13" font-weight="bold" fill="var(--hl-nguy-hiem, #ef4444)">Buồng đốt xăng</text>' +
          '<text x="80" y="60" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">LHV = 44 MJ/kg</text>' +
        '</g>' +
        // Nhiên liệu vào bên trái
        '<line x1="40" y1="70" x2="160" y2="70" stroke="var(--hl-canh-bao, #f59e0b)" stroke-width="3" marker-end="url(#arr-canh-bao)" />' +
        '<text x="90" y="60" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-canh-bao, #f59e0b)">1 kg Xăng</text>' +
        // Gió vào từ dưới
        '<line x1="240" y1="160" x2="240" y2="130" stroke="var(--hl-nhan, #38bdf8)" stroke-width="3" marker-end="url(#arr-nhan)" />' +
        '<text x="240" y="175" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">' + afr + ' kg Không khí</text>' +
        // Khí thải ra bên phải
        '<line x1="320" y1="85" x2="420" y2="85" stroke="currentColor" stroke-width="3" marker-end="url(#arr-thanh-cong)" />' +
        '<text x="370" y="75" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">CO₂ + H₂O + N₂</text>' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">LHV tính khi nước ở thể hơi; AFR chuẩn lý thuyết xăng ≈ 14,7 - 15,1</text>';
    };
  };

  /**
   * H4: dinh-luat-kirchhoff (ΔrH°(T2) = ΔrH°(T1) + ΔCp·(T2 - T1))
   */
  HLHinhChungMinh.cacHinh['dinh-luat-kirchhoff'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Nhiệt độ T₂:', min: 300, max: 800, step: 25, macDinh: 500, donVi: 'K' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var T2 = ctrl.sliderVal;
      ctrl.setChuThich('Định luật Kirchhoff H4', 'Đạo hàm d(ΔrH)/dT = ΔCp → Enthalpy đổi theo chênh lệch nhiệt dung');

      var oX = 60, oY = h - 40, gW = w - 120, gH = h - 70;
      var xT1 = oX + 60;
      var xT2 = oX + 60 + ((T2 - 300) / 500) * (gW - 100);

      g.innerHTML =
        // Trục tọa độ
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + (oX + gW) + '" y2="' + oY + '" stroke="currentColor" stroke-width="1.5" />' +
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + oX + '" y2="' + (oY - gH) + '" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + (oX + gW + 6) + '" y="' + (oY + 4) + '" font-size="11" font-weight="bold" fill="currentColor">T</text>' +
        '<text x="' + oX + '" y="' + (oY - gH - 6) + '" font-size="11" font-weight="bold" fill="currentColor">H</text>' +
        // Đường H(sản phẩm) - dốc hơn
        '<line x1="' + (oX + 30) + '" y1="' + (oY - 30) + '" x2="' + (oX + gW - 20) + '" y2="' + (oY - gH + 10) + '" stroke="var(--hl-thanh-cong, #10b981)" stroke-width="2.5" />' +
        '<text x="' + (oX + gW - 10) + '" y="' + (oY - gH + 15) + '" font-size="10" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">H(sp)</text>' +
        // Đường H(chất đầu) - thoai thoải
        '<line x1="' + (oX + 30) + '" y1="' + (oY - 60) + '" x2="' + (oX + gW - 20) + '" y2="' + (oY - gH*0.7) + '" stroke="var(--hl-nhan, #38bdf8)" stroke-width="2.5" />' +
        '<text x="' + (oX + gW - 10) + '" y="' + (oY - gH*0.7 + 5) + '" font-size="10" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">H(đầu)</text>' +
        // Vạch T1 và T2
        '<line x1="' + xT1 + '" y1="' + oY + '" x2="' + xT1 + '" y2="' + (oY - gH*0.55) + '" stroke="currentColor" stroke-dasharray="2,2" />' +
        '<text x="' + xT1 + '" y="' + (oY + 14) + '" text-anchor="middle" font-size="10" font-weight="bold" fill="currentColor">T₁</text>' +
        '<line x1="' + xT2 + '" y1="' + oY + '" x2="' + xT2 + '" y2="' + (oY - gH*0.8) + '" stroke="var(--hl-canh-bao, #f59e0b)" stroke-width="2" />' +
        '<text x="' + xT2 + '" y="' + (oY + 14) + '" text-anchor="middle" font-size="10" font-weight="bold" fill="var(--hl-canh-bao, #f59e0b)">T₂ = ' + T2 + ' K</text>' +
        '<text x="' + (w/2) + '" y="' + (h - 8) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Δ<tspan baseline-shift="sub" font-size="75%">r</tspan>H°(T₂) = Δ<tspan baseline-shift="sub" font-size="75%">r</tspan>H°(T₁) + ΔC<tspan baseline-shift="sub" font-size="75%">p</tspan>·(T₂ − T₁)</text>';
    };
  };

  // =========================================================================
  // KHỐI 4: TỐC ĐỘ PHẢN ỨNG (D1 - D7)
  // =========================================================================

  /**
   * D1: toc-do-trung-binh-ti-luong (v̄ = -1/a ΔCA/Δt = 1/c ΔCC/Δt)
   */
  HLHinhChungMinh.cacHinh['toc-do-trung-binh-ti-luong'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Khoảng Δt:', min: 10, max: 60, step: 5, macDinh: 30, donVi: 's' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var dt = ctrl.sliderVal;
      ctrl.setChuThich('Tốc độ trung bình D1', 'Quy ước chia cho hệ số tỉ lượng a để tốc độ phản ứng là duy nhất');

      var oX = 55, oY = h - 40, gW = w - 110, gH = h - 70;
      var t1 = 30, t2 = t1 + dt;
      var xT1 = oX + (t1 / 100) * gW;
      var xT2 = oX + (t2 / 100) * gW;

      g.innerHTML =
        // Trục tọa độ
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + (oX + gW) + '" y2="' + oY + '" stroke="currentColor" stroke-width="1.5" />' +
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + oX + '" y2="' + (oY - gH) + '" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + (oX + gW + 6) + '" y="' + (oY + 4) + '" font-size="11" font-weight="bold" fill="currentColor">t</text>' +
        '<text x="' + oX + '" y="' + (oY - gH - 6) + '" font-size="11" font-weight="bold" fill="currentColor">C</text>' +
        // Đường chất đầu giảm (đỏ)
        '<path d="M ' + oX + ' ' + (oY - gH*0.9) + ' Q ' + (oX + 60) + ' ' + (oY - gH*0.2) + ' ' + (oX + gW) + ' ' + (oY - 8) + '" fill="none" stroke="var(--hl-nguy-hiem, #ef4444)" stroke-width="2.5" />' +
        '<text x="' + (oX + 10) + '" y="' + (oY - gH*0.9 - 6) + '" font-size="10" font-weight="bold" fill="var(--hl-nguy-hiem, #ef4444)">Chất phản ứng: −(1/a)·ΔC<tspan baseline-shift="sub" font-size="75%">A</tspan></text>' +
        // Đường sản phẩm tăng (xanh)
        '<path d="M ' + oX + ' ' + oY + ' Q ' + (oX + 60) + ' ' + (oY - gH*0.7) + ' ' + (oX + gW) + ' ' + (oY - gH*0.85) + '" fill="none" stroke="var(--hl-thanh-cong, #10b981)" stroke-width="2.5" />' +
        '<text x="' + (oX + gW - 20) + '" y="' + (oY - gH*0.85 - 6) + '" text-anchor="end" font-size="10" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">Sản phẩm: +(1/c)·ΔC<tspan baseline-shift="sub" font-size="75%">C</tspan></text>' +
        // Vạch cát tuyến Δt
        '<line x1="' + xT1 + '" y1="' + oY + '" x2="' + xT1 + '" y2="' + (oY - gH*0.7) + '" stroke="currentColor" stroke-dasharray="2,2" />' +
        '<line x1="' + xT2 + '" y1="' + oY + '" x2="' + xT2 + '" y2="' + (oY - gH*0.7) + '" stroke="currentColor" stroke-dasharray="2,2" />' +
        '<line x1="' + xT1 + '" y1="' + (oY + 12) + '" x2="' + xT2 + '" y2="' + (oY + 12) + '" stroke="var(--hl-nhan, #38bdf8)" stroke-width="2" marker-end="url(#arr-nhan)" />' +
        '<text x="' + ((xT1+xT2)/2) + '" y="' + (oY + 24) + '" text-anchor="middle" font-size="10" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">Δt = ' + dt + ' s</text>' +
        '<text x="' + (w/2) + '" y="' + (h - 6) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">v̄ = −(1/a)(ΔC<tspan baseline-shift="sub" font-size="75%">A</tspan>/Δt) = +(1/c)(ΔC<tspan baseline-shift="sub" font-size="75%">C</tspan>/Δt) &gt; 0</text>';
    };
  };

  /**
   * D2: dinh-luat-tac-dung-khoi-luong (v = k · CA^a · CB^b)
   */
  HLHinhChungMinh.cacHinh['dinh-luat-tac-dung-khoi-luong'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Nồng độ CA:', min: 1, max: 4, step: 1, macDinh: 2 });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function (t) {
      var nA = ctrl.sliderVal;
      var nB = 3;
      var soCapVaCham = nA * nB;
      ctrl.setChuThich('Định luật tác dụng khối lượng D2', 'Số cặp va chạm khả dĩ N = N_A × N_B = ' + nA + ' × ' + nB + ' = ' + soCapVaCham + ' cặp');

      g.innerHTML =
        // Buồng phản ứng
        '<rect x="40" y="30" width="240" height="120" rx="8" fill="currentColor" fill-opacity="0.05" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="160" y="22" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Buồng va chạm lưỡng phân tử A + B</text>' +
        // Hạt A (xanh lam)
        Array.from({ length: nA }).map(function (_, i) {
          var y = 50 + i * 25;
          return '<circle cx="80" cy="' + y + '" r="8" fill="var(--hl-nhan, #38bdf8)" /><text x="80" y="' + (y+3) + '" text-anchor="middle" font-size="9" font-weight="bold" fill="#fff">A</text>';
        }).join('') +
        // Hạt B (hổ phách)
        Array.from({ length: nB }).map(function (_, j) {
          var y = 50 + j * 30;
          return '<circle cx="230" cy="' + y + '" r="8" fill="var(--hl-canh-bao, #f59e0b)" /><text x="230" y="' + (y+3) + '" text-anchor="middle" font-size="9" font-weight="bold" fill="#fff">B</text>';
        }).join('') +
        // Dây nối các cặp va chạm
        Array.from({ length: nA }).map(function (_, i) {
          var yA = 50 + i * 25;
          return Array.from({ length: nB }).map(function (_, j) {
            var yB = 50 + j * 30;
            return '<line x1="88" y1="' + yA + '" x2="222" y2="' + yB + '" stroke="currentColor" stroke-opacity="0.15" stroke-dasharray="2,2" />';
          }).join('');
        }).join('') +
        // Bảng tốc độ bên phải
        '<g transform="translate(305, 35)">' +
          '<rect x="0" y="0" width="145" height="110" rx="8" fill="currentColor" fill-opacity="0.06" stroke="currentColor" stroke-width="1" />' +
          '<text x="12" y="24" font-size="11" font-weight="bold" fill="currentColor">Tần số va chạm:</text>' +
          '<text x="12" y="50" font-size="13" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">Z ∝ C<tspan baseline-shift="sub" font-size="75%">A</tspan> · C<tspan baseline-shift="sub" font-size="75%">B</tspan></text>' +
          '<text x="12" y="74" font-size="12" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">v = k · C<tspan baseline-shift="sub" font-size="75%">A</tspan> · C<tspan baseline-shift="sub" font-size="75%">B</tspan></text>' +
          '<text x="12" y="96" font-size="10" fill="currentColor" opacity="0.6">' + soCapVaCham + ' cặp tương tác</text>' +
        '</g>' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Xác suất gặp nhau tỉ lệ thuận trực tiếp với tích số nồng độ các chất phản ứng</text>';
    };
  };

  /**
   * D3: he-so-nhiet-do-vant-hoff (v2/v1 = γ^((T2-T1)/10))
   */
  HLHinhChungMinh.cacHinh['he-so-nhiet-do-vant-hoff'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Nhiệt độ T₂:', min: 20, max: 60, step: 10, macDinh: 40, donVi: '°C' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var t2 = ctrl.sliderVal;
      var t1 = 20;
      var gamma = 2.0;
      var heSo = Math.pow(gamma, (t2 - t1) / 10).toFixed(1);
      ctrl.setChuThich('Hệ số Van \'t Hoff D3', 'Cứ tăng 10 °C tốc độ nhân γ = 2 lần: Tăng từ 20 °C lên ' + t2 + ' °C → Nhanh gấp ' + heSo + ' lần');

      var bars = [
        { t: 20, val: 1.0 },
        { t: 30, val: 2.0 },
        { t: 40, val: 4.0 },
        { t: 50, val: 8.0 },
        { t: 60, val: 16.0 }
      ];

      g.innerHTML =
        // Biểu đồ cột nhảy vọt cấp số nhân
        bars.map(function (b, idx) {
          var bH = (b.val / 16.0) * 90;
          var bX = 60 + idx * 75;
          var laChon = b.t <= t2;
          return (
            '<rect x="' + bX + '" y="' + (140 - bH) + '" width="40" height="' + bH + '" rx="4" fill="' + (laChon ? 'var(--hl-nguy-hiem, #ef4444)' : 'currentColor') + '" fill-opacity="' + (laChon ? 0.85 : 0.15) + '" />' +
            '<text x="' + (bX + 20) + '" y="' + (135 - bH) + '" text-anchor="middle" font-size="10" font-weight="bold" fill="currentColor">' + b.val + 'x</text>' +
            '<text x="' + (bX + 20) + '" y="156" text-anchor="middle" font-size="10" font-weight="bold" fill="currentColor">' + b.t + '°C</text>'
          );
        }).join('') +
        '<line x1="40" y1="140" x2="440" y2="140" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">v₂ / v₁ = γ^((T₂ − T₁) / 10) (Tăng theo cấp số nhân dù nhiệt độ chỉ tăng cấp số cộng)</text>';
    };
  };

  /**
   * D4: arrhenius-va-cham-hoat-dong (k = A · e^(-Ea/RT))
   */
  HLHinhChungMinh.cacHinh['arrhenius-va-cham-hoat-dong'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Nhiệt độ T:', min: 250, max: 700, step: 25, macDinh: 300, donVi: 'K' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var T = ctrl.sliderVal;
      ctrl.setChuThich('Phương trình Arrhenius D4', 'T = ' + T + ' K: Phần diện tích gạch chéo E ≥ Ea (va chạm hiệu quả) phình to');

      var oX = 55, oY = h - 45, gW = w - 110, gH = h - 75;
      var peakX = oX + 60 + (T - 250) * 0.1;
      var eaX = oX + 170;

      g.innerHTML =
        // Trục phân bố năng lượng
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + (oX + gW) + '" y2="' + oY + '" stroke="currentColor" stroke-width="1.5" />' +
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + oX + '" y2="' + (oY - gH) + '" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + (oX + gW + 6) + '" y="' + (oY + 4) + '" font-size="11" font-weight="bold" fill="currentColor">E</text>' +
        '<text x="' + oX + '" y="' + (oY - gH - 6) + '" font-size="11" font-weight="bold" fill="currentColor">f(E)</text>' +
        // Vùng phân tử hoạt động (E >= Ea)
        '<path d="M ' + eaX + ' ' + (oY - 35) + ' Q ' + (eaX + 40) + ' ' + (oY - 12) + ' ' + (oX + gW) + ' ' + oY + ' L ' + eaX + ' ' + oY + ' Z" fill="var(--hl-nguy-hiem, #ef4444)" fill-opacity="0.35" />' +
        // Đường cong Maxwell-Boltzmann
        '<path d="M ' + oX + ' ' + oY + ' Q ' + peakX + ' ' + (oY - gH*0.95) + ' ' + (oX + gW) + ' ' + oY + '" fill="none" stroke="var(--hl-nhan, #38bdf8)" stroke-width="2.5" />' +
        // Vạch ngưỡng Ea
        '<line x1="' + eaX + '" y1="' + oY + '" x2="' + eaX + '" y2="' + (oY - gH*0.8) + '" stroke="var(--hl-nguy-hiem, #ef4444)" stroke-width="2" stroke-dasharray="3,3" />' +
        '<text x="' + eaX + '" y="' + (oY - gH*0.8 - 6) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-nguy-hiem, #ef4444)">E<tspan baseline-shift="sub" font-size="75%">a</tspan></text>' +
        '<text x="' + (eaX + 40) + '" y="' + (oY - 20) + '" font-size="10" font-weight="bold" fill="var(--hl-nguy-hiem, #ef4444)">f = e^(−E<tspan baseline-shift="sub" font-size="75%">a</tspan>/RT)</text>' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Chỉ những phân tử có động năng E ≥ E<tspan baseline-shift="sub" font-size="75%">a</tspan> mới vượt qua được rào cản để phản ứng</text>';
    };
  };

  /**
   * D5: phan-ra-bac-1 (t1/2 = ln 2 / k)
   */
  HLHinhChungMinh.cacHinh['phan-ra-bac-1'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Thời gian t/t½:', min: 0, max: 4, step: 0.25, macDinh: 1.0 });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var nHalves = ctrl.sliderVal;
      var phanTramConLai = (100 * Math.pow(0.5, nHalves)).toFixed(1);
      ctrl.setChuThich('Động học bậc 1 D5', 't = ' + nHalves + '·t½ → Còn lại ' + phanTramConLai + '% nồng độ ban đầu');

      var oX = 55, oY = h - 45, gW = w - 120, gH = h - 75;

      g.innerHTML =
        // Trục C - t
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + (oX + gW) + '" y2="' + oY + '" stroke="currentColor" stroke-width="1.5" />' +
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + oX + '" y2="' + (oY - gH) + '" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + (oX + gW + 6) + '" y="' + (oY + 4) + '" font-size="11" font-weight="bold" fill="currentColor">t</text>' +
        '<text x="' + oX + '" y="' + (oY - gH - 6) + '" font-size="11" font-weight="bold" fill="currentColor">C</text>' +
        // Đường cong phân rã
        '<path d="M ' + oX + ' ' + (oY - gH) + ' Q ' + (oX + 70) + ' ' + (oY - gH*0.25) + ' ' + (oX + gW) + ' ' + (oY - 6) + '" fill="none" stroke="var(--hl-nhan, #38bdf8)" stroke-width="2.5" />' +
        // Các nấc chu kỳ bán rã 1, 2, 3
        '<line x1="' + (oX + 70) + '" y1="' + oY + '" x2="' + (oX + 70) + '" y2="' + (oY - gH*0.5) + '" stroke="currentColor" stroke-dasharray="2,2" />' +
        '<line x1="' + oX + '" y1="' + (oY - gH*0.5) + '" x2="' + (oX + 70) + '" y2="' + (oY - gH*0.5) + '" stroke="currentColor" stroke-dasharray="2,2" />' +
        '<text x="' + (oX + 70) + '" y="' + (oY + 14) + '" text-anchor="middle" font-size="10" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">1 t½</text>' +
        '<text x="' + (oX - 6) + '" y="' + (oY - gH*0.5 + 4) + '" text-anchor="end" font-size="10" fill="currentColor">50%</text>' +
        '<line x1="' + (oX + 140) + '" y1="' + oY + '" x2="' + (oX + 140) + '" y2="' + (oY - gH*0.25) + '" stroke="currentColor" stroke-dasharray="2,2" />' +
        '<text x="' + (oX + 140) + '" y="' + (oY + 14) + '" text-anchor="middle" font-size="10" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">2 t½</text>' +
        '<text x="' + (oX - 6) + '" y="' + (oY - gH*0.25 + 4) + '" text-anchor="end" font-size="10" fill="currentColor">25%</text>' +
        // Điểm trượt hiện tại
        '<circle cx="' + (oX + nHalves * 70) + '" cy="' + (oY - gH * (phanTramConLai / 100)) + '" r="5" fill="var(--hl-nguy-hiem, #ef4444)" />' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">t½ = ln 2 / k ≈ 0,693 / k (Chu kỳ bán rã hoàn toàn độc lập với nồng độ ban đầu C₀)</text>';
    };
  };

  /**
   * D6: dong-hoc-bac-0-va-2 (Bậc 0, bậc 1, bậc 2)
   */
  HLHinhChungMinh.cacHinh['dong-hoc-bac-0-va-2'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Bậc phản ứng n:', min: 0, max: 2, step: 1, macDinh: 2 });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var n = ctrl.sliderVal;
      ctrl.setChuThich('Động học đa bậc D6', (n === 0 ? 'Bậc 0: C giảm đường thẳng dốc đều C = C₀ − kt | t½ = C₀/(2k)' : (n === 1 ? 'Bậc 1: C giảm hàm mũ C = C₀·e^(−kt) | t½ = ln2/k độc lập C₀' : 'Bậc 2: 1/C = 1/C₀ + kt | t½ = 1/(k·C₀) tỉ lệ nghịch nồng độ')));

      var oX = 55, oY = h - 45, gW = w - 120, gH = h - 75;

      g.innerHTML =
        // Trục
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + (oX + gW) + '" y2="' + oY + '" stroke="currentColor" stroke-width="1.5" />' +
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + oX + '" y2="' + (oY - gH) + '" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + (oX + gW + 6) + '" y="' + (oY + 4) + '" font-size="11" font-weight="bold" fill="currentColor">t</text>' +
        '<text x="' + oX + '" y="' + (oY - gH - 6) + '" font-size="11" font-weight="bold" fill="currentColor">C</text>' +
        // Bậc 0 (đường thẳng dốc đều)
        '<line x1="' + oX + '" y1="' + (oY - gH) + '" x2="' + (oX + gW*0.8) + '" y2="' + oY + '" stroke="' + (n === 0 ? 'var(--hl-nguy-hiem, #ef4444)' : 'currentColor') + '" stroke-width="' + (n === 0 ? 3 : 1) + '" stroke-opacity="' + (n === 0 ? 1 : 0.25) + '" />' +
        '<text x="' + (oX + 50) + '" y="' + (oY - gH*0.7) + '" font-size="10" font-weight="bold" fill="' + (n === 0 ? 'var(--hl-nguy-hiem, #ef4444)' : 'currentColor') + '" opacity="' + (n === 0 ? 1 : 0.4) + '">Bậc 0 (Đường thẳng)</text>' +
        // Bậc 2 (đường cong hyperbol dốc gắt ban đầu rồi thoải dần)
        '<path d="M ' + oX + ' ' + (oY - gH) + ' Q ' + (oX + 35) + ' ' + (oY - 20) + ' ' + (oX + gW) + ' ' + (oY - 8) + '" fill="none" stroke="' + (n === 2 ? 'var(--hl-nhan, #38bdf8)' : 'currentColor') + '" stroke-width="' + (n === 2 ? 3 : 1) + '" stroke-opacity="' + (n === 2 ? 1 : 0.25) + '" />' +
        '<text x="' + (oX + 110) + '" y="' + (oY - 25) + '" font-size="10" font-weight="bold" fill="' + (n === 2 ? 'var(--hl-nhan, #38bdf8)' : 'currentColor') + '" opacity="' + (n === 2 ? 1 : 0.4) + '">Bậc 2: 1/C = 1/C₀ + kt</text>' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Bậc 0 phụ thuộc thuận C₀, bậc 1 độc lập C₀, bậc 2 tỉ lệ nghịch 1/C₀</text>';
    };
  };

  /**
   * D7: do-thi-nang-luong (Chất xúc tác hạ thấp Ea)
   */
  HLHinhChungMinh.cacHinh['do-thi-nang-luong'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Độ hạ ΔEa:', min: 10, max: 40, step: 5, macDinh: 20, donVi: 'kJ' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var dEa = ctrl.sliderVal;
      var ratio = Math.exp(dEa * 1000 / (8.314 * 298.15));
      var ratioStr = ratio > 1e4 ? ratio.toExponential(1) : Math.round(ratio).toLocaleString('vi-VN');
      ctrl.setChuThich('Chất xúc tác D7', 'Hạ Ea đi ' + dEa + ' kJ/mol → Tốc độ phản ứng tăng vọt gấp ' + ratioStr + ' lần');

      var oX = 40, oY = h - 35;
      var yDau = 110, ySp = 135;
      var yDinhGoc = 35;
      var yDinhXt = yDinhGoc + dEa * 1.5;

      g.innerHTML =
        // Trục thế năng
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + (w - 30) + '" y2="' + oY + '" stroke="currentColor" stroke-width="1.5" />' +
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + oX + '" y2="25" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + oX + '" y="16" font-size="10" font-weight="bold" fill="currentColor">E</text>' +
        // Chất đầu & sản phẩm
        '<line x1="50" y1="' + yDau + '" x2="110" y2="' + yDau + '" stroke="currentColor" stroke-width="3" />' +
        '<text x="80" y="' + (yDau - 6) + '" text-anchor="middle" font-size="10" font-weight="bold" fill="currentColor">Chất đầu</text>' +
        '<line x1="330" y1="' + ySp + '" x2="390" y2="' + ySp + '" stroke="currentColor" stroke-width="3" />' +
        '<text x="360" y="' + (ySp + 16) + '" text-anchor="middle" font-size="10" font-weight="bold" fill="currentColor">Sản phẩm</text>' +
        // Đường không xúc tác (Đỏ)
        '<path d="M 110 ' + yDau + ' C 160 ' + yDau + ', 180 ' + yDinhGoc + ', 220 ' + yDinhGoc + ' C 260 ' + yDinhGoc + ', 280 ' + ySp + ', 330 ' + ySp + '" fill="none" stroke="var(--hl-nguy-hiem, #ef4444)" stroke-width="2.5" />' +
        '<text x="220" y="' + (yDinhGoc - 8) + '" text-anchor="middle" font-size="10" font-weight="bold" fill="var(--hl-nguy-hiem, #ef4444)">E<tspan baseline-shift="sub" font-size="75%">a</tspan> gốc</text>' +
        // Đường có xúc tác (Xanh đứt nét)
        '<path d="M 110 ' + yDau + ' C 160 ' + yDau + ', 180 ' + yDinhXt + ', 220 ' + yDinhXt + ' C 260 ' + yDinhXt + ', 280 ' + ySp + ', 330 ' + ySp + '" fill="none" stroke="var(--hl-thanh-cong, #10b981)" stroke-width="2.5" stroke-dasharray="4,3" />' +
        '<text x="220" y="' + (yDinhXt + 16) + '" text-anchor="middle" font-size="10" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">E<tspan baseline-shift="sub" font-size="75%">a</tspan>\' = E<tspan baseline-shift="sub" font-size="75%">a</tspan> − ' + dEa + ' kJ</text>' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">k_xt / k = e^(ΔE<tspan baseline-shift="sub" font-size="75%">a</tspan> / RT) (Chất xúc tác chỉ hạ rào năng lượng, không làm đổi ΔH)</text>';
    };
  };

  // =========================================================================
  // KHỐI 5: CÂN BẰNG HOÁ HỌC (C1 - C7)
  // =========================================================================

  /**
   * C1: can-bang-dong-vt-vn (Cân bằng động vt = vn)
   */
  HLHinhChungMinh.cacHinh['can-bang-dong-vt-vn'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Tiến trình thời gian:', min: 0, max: 100, step: 5, macDinh: 70, donVi: '%' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var pct = ctrl.sliderVal;
      ctrl.setChuThich('Cân bằng động C1', 'Tại cân bằng: Tốc độ phản ứng thuận bằng tốc độ phản ứng nghịch (v_t = v_n ≠ 0)');

      var oX = 55, oY = h - 45, gW = w - 120, gH = h - 75;
      var curX = oX + (pct / 100) * gW;

      g.innerHTML =
        // Trục v - t
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + (oX + gW) + '" y2="' + oY + '" stroke="currentColor" stroke-width="1.5" />' +
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + oX + '" y2="' + (oY - gH) + '" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + (oX + gW + 6) + '" y="' + (oY + 4) + '" font-size="11" font-weight="bold" fill="currentColor">t</text>' +
        '<text x="' + oX + '" y="' + (oY - gH - 6) + '" font-size="11" font-weight="bold" fill="currentColor">v</text>' +
        // Tốc độ thuận giảm dần (xanh)
        '<path d="M ' + oX + ' ' + (oY - gH*0.9) + ' Q ' + (oX + 80) + ' ' + (oY - gH*0.48) + ' ' + (oX + gW) + ' ' + (oY - gH*0.48) + '" fill="none" stroke="var(--hl-nhan, #38bdf8)" stroke-width="2.5" />' +
        '<text x="' + (oX + 35) + '" y="' + (oY - gH*0.8) + '" font-size="10" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">v<tspan baseline-shift="sub" font-size="75%">thuận</tspan></text>' +
        // Tốc độ nghịch tăng dần (hổ phách)
        '<path d="M ' + oX + ' ' + oY + ' Q ' + (oX + 80) + ' ' + (oY - gH*0.48) + ' ' + (oX + gW) + ' ' + (oY - gH*0.48) + '" fill="none" stroke="var(--hl-canh-bao, #f59e0b)" stroke-width="2.5" />' +
        '<text x="' + (oX + 35) + '" y="' + (oY - 15) + '" font-size="10" font-weight="bold" fill="var(--hl-canh-bao, #f59e0b)">v<tspan baseline-shift="sub" font-size="75%">nghịch</tspan></text>' +
        // Vạch cân bằng v_t = v_n
        '<line x1="' + (oX + 110) + '" y1="' + (oY - gH*0.48) + '" x2="' + (oX + gW) + '" y2="' + (oY - gH*0.48) + '" stroke="var(--hl-thanh-cong, #10b981)" stroke-width="2" stroke-dasharray="3,3" />' +
        '<circle cx="' + curX + '" cy="' + (oY - gH*0.48) + '" r="5" fill="var(--hl-thanh-cong, #10b981)" />' +
        '<text x="' + (oX + gW - 20) + '" y="' + (oY - gH*0.48 - 8) + '" text-anchor="end" font-size="11" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">v<tspan baseline-shift="sub" font-size="75%">t</tspan> = v<tspan baseline-shift="sub" font-size="75%">n</tspan> (Cân bằng động)</text>' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">K<tspan baseline-shift="sub" font-size="75%">c</tspan> = k<tspan baseline-shift="sub" font-size="75%">t</tspan> / k<tspan baseline-shift="sub" font-size="75%">n</tspan> = [C]^c [D]^d / ([A]^a [B]^b)</text>';
    };
  };

  /**
   * C2: thuong-so-q-so-voi-k (Q so với K và chiều chuyển dịch)
   */
  HLHinhChungMinh.cacHinh['thuong-so-q-so-voi-k'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Thương số Q/K:', min: 0.2, max: 3.0, step: 0.1, macDinh: 0.5 });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var qk = ctrl.sliderVal;
      var chieu = qk < 0.95 ? 'Chiều thuận (→)' : (qk > 1.05 ? 'Chiều nghịch (←)' : 'Đang ở cân bằng (Q = K)');
      ctrl.setChuThich('Thương số phản ứng C2', 'Q/K = ' + qk + ' → ' + chieu);

      var oX = w / 2;
      // Đường cong thế Gibbs đáy thung lũng tại oX
      var ballX = oX + (Math.log(qk) / Math.log(3)) * 130;
      ballX = Math.max(80, Math.min(w - 80, ballX));
      var ballY = 60 + Math.pow((ballX - oX) / 80, 2) * 45;

      g.innerHTML =
        // Mặt thung lũng thế năng Gibbs G(ξ)
        '<path d="M ' + (oX - 140) + ' 135 Q ' + oX + ' 45 ' + (oX + 140) + ' 135" fill="none" stroke="currentColor" stroke-width="2.5" />' +
        // Đáy cân bằng Q = K
        '<line x1="' + oX + '" y1="35" x2="' + oX + '" y2="150" stroke="var(--hl-thanh-cong, #10b981)" stroke-dasharray="2,2" />' +
        '<text x="' + oX + '" y="165" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">Đáy cân bằng: Q = K</text>' +
        // Quả bóng trạng thái Q lăn về đáy
        '<circle cx="' + ballX + '" cy="' + (ballY - 8) + '" r="9" fill="' + (qk < 0.95 ? 'var(--hl-nhan, #38bdf8)' : (qk > 1.05 ? 'var(--hl-canh-bao, #f59e0b)' : 'var(--hl-thanh-cong, #10b981)')) + '" stroke="#fff" stroke-width="2" />' +
        '<text x="' + ballX + '" y="' + (ballY - 22) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Q</text>' +
        '<text x="' + (w/2) + '" y="' + (h - 8) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Q &lt; K: v<tspan baseline-shift="sub" font-size="75%">t</tspan> &gt; v<tspan baseline-shift="sub" font-size="75%">n</tspan> (chiều thuận) | Q &gt; K: v<tspan baseline-shift="sub" font-size="75%">n</tspan> &gt; v<tspan baseline-shift="sub" font-size="75%">t</tspan> (chiều nghịch)</text>';
    };
  };

  /**
   * C3: nen-thay-doi-Q (Nén thay đổi thể tích Q' = s^Δn · K)
   */
  HLHinhChungMinh.cacHinh['nen-thay-doi-Q'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Tỉ số nén s:', min: 1.0, max: 3.0, step: 0.2, macDinh: 2.0, donVi: 'lần' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var s = ctrl.sliderVal;
      // 2NO2 (nâu) <=> N2O4 (không màu), Δn = 1 - 2 = -1
      var qPhay = (Math.pow(s, -1)).toFixed(2);
      ctrl.setChuThich('Nén thể tích C3', '2NO₂ ⇌ N₂O₄ (Δn = −1): Nén s = ' + s + ' → Q\' = s⁻¹·K = ' + qPhay + ' K < K (Chiều thuận ghép đôi)');

      var boxW = 120 / s;

      g.innerHTML =
        // Bình ban đầu (thể tích V)
        '<g transform="translate(50, 40)">' +
          '<rect x="0" y="0" width="120" height="85" rx="6" fill="var(--hl-canh-bao, #f59e0b)" fill-opacity="0.2" stroke="currentColor" stroke-width="1.5" />' +
          '<circle cx="30" cy="30" r="5" fill="var(--hl-canh-bao, #f59e0b)" />' +
          '<circle cx="70" cy="50" r="5" fill="var(--hl-canh-bao, #f59e0b)" />' +
          '<circle cx="90" cy="25" r="5" fill="var(--hl-canh-bao, #f59e0b)" />' +
          '<text x="60" y="105" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Ban đầu (Q = K)</text>' +
        '</g>' +
        // Mũi tên nén
        '<line x1="190" y1="82" x2="250" y2="82" stroke="var(--hl-nguy-hiem, #ef4444)" stroke-width="3" marker-end="url(#arr-nguy-hiem)" />' +
        '<text x="220" y="70" text-anchor="middle" font-size="10" font-weight="bold" fill="var(--hl-nguy-hiem, #ef4444)">Nén ' + s + ' lần</text>' +
        // Bình sau khi nén (thể tích V/s)
        '<g transform="translate(275, 40)">' +
          '<rect x="0" y="0" width="' + boxW + '" height="85" rx="6" fill="var(--hl-canh-bao, #f59e0b)" fill-opacity="0.4" stroke="currentColor" stroke-width="1.5" />' +
          '<circle cx="' + (boxW*0.3) + '" cy="35" r="6" fill="var(--hl-nhan, #38bdf8)" />' +
          '<circle cx="' + (boxW*0.7) + '" cy="55" r="6" fill="var(--hl-nhan, #38bdf8)" />' +
          '<text x="' + (boxW/2) + '" y="105" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">V/' + s + ' (Q\' = ' + qPhay + ' K)</text>' +
        '</g>' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Tăng áp suất làm cân bằng chuyển dịch về phía có số mol khí ít hơn</text>';
    };
  };

  /**
   * C4: nhiet-do-vant-hoff-can-bang (Đồ thị Van 't Hoff ln K theo 1/T)
   */
  HLHinhChungMinh.cacHinh['nhiet-do-vant-hoff-can-bang'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Nhiệt phản ứng ΔH°:', min: -50, max: 50, step: 10, macDinh: -20, donVi: 'kJ' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var dH = ctrl.sliderVal;
      var toanhiet = dH < 0;
      ctrl.setChuThich('Phương trình Van \'t Hoff C4', 'ΔH° = ' + dH + ' kJ/mol: ' + (toanhiet ? 'Toả nhiệt → Tăng T làm K giảm (độ dốc dương)' : 'Thu nhiệt → Tăng T làm K tăng (độ dốc âm)'));

      var oX = 55, oY = h - 45, gW = w - 120, gH = h - 75;

      g.innerHTML =
        // Trục ln K theo 1/T
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + (oX + gW) + '" y2="' + oY + '" stroke="currentColor" stroke-width="1.5" />' +
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + oX + '" y2="' + (oY - gH) + '" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + (oX + gW + 6) + '" y="' + (oY + 4) + '" font-size="11" font-weight="bold" fill="currentColor">1/T</text>' +
        '<text x="' + oX + '" y="' + (oY - gH - 6) + '" font-size="11" font-weight="bold" fill="currentColor">ln K</text>' +
        // Đường biểu diễn Van 't Hoff
        (toanhiet
          ? '<line x1="' + (oX + 30) + '" y1="' + (oY - 20) + '" x2="' + (oX + gW - 30) + '" y2="' + (oY - gH + 10) + '" stroke="var(--hl-nhan, #38bdf8)" stroke-width="3" />' +
            '<text x="' + (oX + gW/2) + '" y="' + (oY - gH/2 - 10) + '" font-size="11" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">Toả nhiệt: Độ dốc = −ΔH°/R &gt; 0</text>'
          : '<line x1="' + (oX + 30) + '" y1="' + (oY - gH + 10) + '" x2="' + (oX + gW - 30) + '" y2="' + (oY - 20) + '" stroke="var(--hl-nguy-hiem, #ef4444)" stroke-width="3" />' +
            '<text x="' + (oX + gW/2) + '" y="' + (oY - gH/2 - 10) + '" font-size="11" font-weight="bold" fill="var(--hl-nguy-hiem, #ef4444)">Thu nhiệt: Độ dốc = −ΔH°/R &lt; 0</text>'
        ) +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">ln(K₂/K₁) = −(ΔH°/R)·(1/T₂ − 1/T₁)</text>';
    };
  };

  /**
   * C5: quan-he-kp-va-kc (Kp = Kc · (RT)^Δn)
   */
  HLHinhChungMinh.cacHinh['quan-he-kp-va-kc'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Số mol khí Δn:', min: -2, max: 2, step: 1, macDinh: -1 });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var dn = ctrl.sliderVal;
      ctrl.setChuThich('Mối liên hệ Kp và Kc C5', 'Δn = ' + dn + ' → Kp = Kc · (RT)^(' + dn + ')');

      g.innerHTML =
        // Sơ đồ chuyển đổi pi = Ci · RT
        '<g transform="translate(60, 45)">' +
          '<rect x="0" y="0" width="140" height="70" rx="8" fill="var(--hl-nhan, #38bdf8)" fill-opacity="0.1" stroke="var(--hl-nhan, #38bdf8)" stroke-width="1.5" />' +
          '<text x="70" y="30" text-anchor="middle" font-size="12" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">Nồng độ C<tspan baseline-shift="sub" font-size="75%">i</tspan> (mol/L)</text>' +
          '<text x="70" y="52" text-anchor="middle" font-size="14" font-weight="bold" fill="currentColor">K<tspan baseline-shift="sub" font-size="75%">c</tspan></text>' +
        '</g>' +
        // Mũi tên biến đổi x RT
        '<line x1="205" y1="80" x2="275" y2="80" stroke="var(--hl-thanh-cong, #10b981)" stroke-width="3" marker-end="url(#arr-thanh-cong)" />' +
        '<text x="240" y="70" text-anchor="middle" font-size="11" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">× (RT)^Δn</text>' +
        // Khối Kp bên phải
        '<g transform="translate(280, 45)">' +
          '<rect x="0" y="0" width="140" height="70" rx="8" fill="var(--hl-thanh-cong, #10b981)" fill-opacity="0.1" stroke="var(--hl-thanh-cong, #10b981)" stroke-width="1.5" />' +
          '<text x="70" y="30" text-anchor="middle" font-size="12" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">Áp suất p<tspan baseline-shift="sub" font-size="75%">i</tspan> (bar)</text>' +
          '<text x="70" y="52" text-anchor="middle" font-size="14" font-weight="bold" fill="currentColor">K<tspan baseline-shift="sub" font-size="75%">p</tspan></text>' +
        '</g>' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Khi Δn = 0 (số mol khí hai vế bằng nhau) thì K<tspan baseline-shift="sub" font-size="75%">p</tspan> = K<tspan baseline-shift="sub" font-size="75%">c</tspan></text>';
    };
  };

  /**
   * C6: bang-ice-phan-li (Bảng ICE phân li A ⇌ 2B)
   */
  HLHinhChungMinh.cacHinh['bang-ice-phan-li'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Độ phản ứng x:', min: 0.005, max: 0.035, step: 0.005, macDinh: 0.015, donVi: 'M' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var x = ctrl.sliderVal;
      var c0 = 0.040;
      var cA = (c0 - x).toFixed(3);
      var cB = (2 * x).toFixed(3);
      var kc = ((4 * x * x) / (c0 - x)).toFixed(4);
      ctrl.setChuThich('Bảng ICE phân li C6', 'A ⇌ 2B: x = ' + x + ' M → [A] = ' + cA + ' M, [B] = ' + cB + ' M (Kc = ' + kc + ')');

      g.innerHTML =
        // Bảng ICE thu nhỏ
        '<g transform="translate(60, 25)">' +
          '<rect x="0" y="0" width="360" height="110" rx="6" fill="currentColor" fill-opacity="0.05" stroke="currentColor" stroke-width="1.5" />' +
          // Dòng tiêu đề
          '<text x="50" y="22" font-size="11" font-weight="bold" fill="currentColor">Giai đoạn</text>' +
          '<text x="180" y="22" font-size="11" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">[A] (M)</text>' +
          '<text x="290" y="22" font-size="11" font-weight="bold" fill="var(--hl-canh-bao, #f59e0b)">[B] (M)</text>' +
          '<line x1="10" y1="30" x2="350" y2="30" stroke="currentColor" stroke-opacity="0.2" />' +
          // Initial I
          '<text x="50" y="50" font-size="10" fill="currentColor">I (Ban đầu)</text>' +
          '<text x="180" y="50" font-size="10" font-mono fill="currentColor">0,040</text>' +
          '<text x="290" y="50" font-size="10" font-mono fill="currentColor">0,000</text>' +
          // Change C
          '<text x="50" y="74" font-size="10" fill="currentColor">C (Phản ứng)</text>' +
          '<text x="180" y="74" font-size="10" font-mono fill="var(--hl-nguy-hiem, #ef4444)">− ' + x.toFixed(3) + '</text>' +
          '<text x="290" y="74" font-size="10" font-mono fill="var(--hl-thanh-cong, #10b981)">+ ' + (2*x).toFixed(3) + '</text>' +
          '<line x1="10" y1="84" x2="350" y2="84" stroke="currentColor" stroke-opacity="0.2" />' +
          // Equilibrium E
          '<text x="50" y="102" font-size="11" font-weight="bold" fill="currentColor">E (Cân bằng)</text>' +
          '<text x="180" y="102" font-size="11" font-mono font-weight="bold" fill="var(--hl-nhan, #38bdf8)">' + cA + '</text>' +
          '<text x="290" y="102" font-size="11" font-mono font-weight="bold" fill="var(--hl-canh-bao, #f59e0b)">' + cB + '</text>' +
        '</g>' +
        '<text x="' + (w/2) + '" y="' + (h - 8) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">K<tspan baseline-shift="sub" font-size="75%">c</tspan> = [B]² / [A] = 4x² / (C₀ − x)</text>';
    };
  };

  /**
   * C7: xuc-tac-khong-doi-can-bang (Chất xúc tác không làm chuyển dịch cân bằng)
   */
  HLHinhChungMinh.cacHinh['xuc-tac-khong-doi-can-bang'] = function (svg, w, h, ctrl) {
    ctrl.caiDatSlider({ nhan: 'Xúc tác hạ Ea:', min: 10, max: 40, step: 5, macDinh: 20, donVi: 'kJ' });

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    svg.appendChild(g);

    return function () {
      var dEa = ctrl.sliderVal;
      ctrl.setChuThich('Chất xúc tác C7', 'Xúc tác hạ cùng một lượng ΔEa cho cả 2 chiều → kt và kn cùng tăng tỉ lệ → Kc không đổi');

      var oX = 55, oY = h - 45, gW = w - 120, gH = h - 75;

      g.innerHTML =
        // Trục nồng độ theo thời gian
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + (oX + gW) + '" y2="' + oY + '" stroke="currentColor" stroke-width="1.5" />' +
        '<line x1="' + oX + '" y1="' + oY + '" x2="' + oX + '" y2="' + (oY - gH) + '" stroke="currentColor" stroke-width="1.5" />' +
        '<text x="' + (oX + gW + 6) + '" y="' + (oY + 4) + '" font-size="11" font-weight="bold" fill="currentColor">t</text>' +
        '<text x="' + oX + '" y="' + (oY - gH - 6) + '" font-size="11" font-weight="bold" fill="currentColor">[C]</text>' +
        // Vạch nồng độ cân bằng chung
        '<line x1="' + oX + '" y1="' + (oY - gH*0.5) + '" x2="' + (oX + gW) + '" y2="' + (oY - gH*0.5) + '" stroke="currentColor" stroke-dasharray="3,3" />' +
        '<text x="' + (oX + gW - 10) + '" y="' + (oY - gH*0.5 - 6) + '" text-anchor="end" font-size="10" font-weight="bold" fill="currentColor">[C] cân bằng (Cố định)</text>' +
        // Đường không xúc tác (chậm hơn)
        '<path d="M ' + oX + ' ' + (oY - 5) + ' Q ' + (oX + 130) + ' ' + (oY - gH*0.48) + ' ' + (oX + gW) + ' ' + (oY - gH*0.5) + '" fill="none" stroke="var(--hl-nhan, #38bdf8)" stroke-width="2" />' +
        '<text x="' + (oX + 160) + '" y="' + (oY - 18) + '" font-size="10" font-weight="bold" fill="var(--hl-nhan, #38bdf8)">Không xúc tác (Chậm)</text>' +
        // Đường có xúc tác (nhanh hơn hẳn, về cùng đích)
        '<path d="M ' + oX + ' ' + (oY - 5) + ' Q ' + (oX + 40) + ' ' + (oY - gH*0.48) + ' ' + (oX + gW) + ' ' + (oY - gH*0.5) + '" fill="none" stroke="var(--hl-thanh-cong, #10b981)" stroke-width="2.5" stroke-dasharray="4,3" />' +
        '<text x="' + (oX + 60) + '" y="' + (oY - gH*0.55) + '" font-size="10" font-weight="bold" fill="var(--hl-thanh-cong, #10b981)">Có xúc tác (Đạt sớm hơn)</text>' +
        '<text x="' + (w/2) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" font-weight="bold" fill="currentColor">Chất xúc tác chỉ rút ngắn thời gian đạt cân bằng, KHÔNG làm chuyển dịch vị trí cân bằng</text>';
    };
  };

  // Đăng ký alias cho mã K1..K8, N1..N8, H1..H4, D1..D7, C1..C7
  var maAlias = {
    'K1': 'trang-thai-trung-gian',
    'K2': 'phan-tu-dap-thanh',
    'K3': 'dong-nang-nhiet-do',
    'K4': 'van-toc-can-quan-phuong',
    'K5': 'ap-suat-rieng-phan-dalton',
    'K6': 'dang-qua-trinh-do-khong',
    'K7': 'cong-dang-nhiet-tich-phan',
    'K8': 'khi-thuc-van-der-waals',
    'N1': 'can-bang-nang-luong-dl1',
    'N2': 'pit-tong-dx',
    'N3': 'bac-tu-do-noi-nang',
    'N4': 'he-thuc-mayer',
    'N5': 'nen-doan-nhiet-poisson',
    'N6': 'so-do-dong-co-nhiet',
    'N7': 'chu-trinh-otto-pv',
    'N8': 'chu-trinh-carnot-so-sanh',
    'H1': 'chu-trinh-hess',
    'H2': 'nang-luong-lien-ket',
    'H3': 'nhiet-chay-nhien-lieu',
    'H4': 'dinh-luat-kirchhoff',
    'D1': 'toc-do-trung-binh-ti-luong',
    'D2': 'dinh-luat-tac-dung-khoi-luong',
    'D3': 'he-so-nhiet-do-vant-hoff',
    'D4': 'arrhenius-va-cham-hoat-dong',
    'D5': 'phan-ra-bac-1',
    'D6': 'dong-hoc-bac-0-va-2',
    'D7': 'do-thi-nang-luong',
    'C1': 'can-bang-dong-vt-vn',
    'C2': 'thuong-so-q-so-voi-k',
    'C3': 'nen-thay-doi-Q',
    'C4': 'nhiet-do-vant-hoff-can-bang',
    'C5': 'quan-he-kp-va-kc',
    'C6': 'bang-ice-phan-li',
    'C7': 'xuc-tac-khong-doi-can-bang'
  };

  Object.keys(maAlias).forEach(function (ma) {
    var idGoc = maAlias[ma];
    if (HLHinhChungMinh.cacHinh[idGoc]) {
      HLHinhChungMinh.cacHinh[ma] = HLHinhChungMinh.cacHinh[idGoc];
    }
  });

  if (typeof module !== 'undefined' && module.exports) {
    module.exports = HLHinhChungMinh;
  }
  global.HLHinhChungMinh = HLHinhChungMinh;
})(typeof window !== 'undefined' ? window : global);
