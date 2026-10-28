/**
 * tuong-tac.js - Xử lý tương tác kéo thả Pointer Events cho Động cơ nhiệt 4 kỳ
 * Hỗ trợ 4 kiểu kéo: Vung bánh đà, Kéo mặt pít-tông, Kéo chấm p-V, Kéo thanh thời gian.
 * UMD: Chạy được trong Node.js (test) và trình duyệt (window.HLTuongTac)
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.HLTuongTac = factory();
  }
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';

  function khoiTaoTuongTac(svgEl, context) {
    var keoKieu = null; // 'banhda', 'piston', 'dot', 'timeline'
    var pointerId = null;
    var psiTruoc = 0;
    var lichSuGoc = [];

    var chuTuaNguoc = document.getElementById('hl-chu-tua-nguoc');

    function toSVGPoint(clientX, clientY) {
      var pt = svgEl.createSVGPoint();
      pt.x = clientX;
      pt.y = clientY;
      var ctm = svgEl.getScreenCTM();
      if (ctm) {
        return pt.matrixTransform(ctm.inverse());
      }
      var rect = svgEl.getBoundingClientRect();
      var vb = svgEl.viewBox ? svgEl.viewBox.baseVal : null;
      var vw = (vb && vb.width > 0) ? vb.width : rect.width;
      var vh = (vb && vb.height > 0) ? vb.height : rect.height;
      return {
        x: ((clientX - rect.left) / rect.width) * vw,
        y: ((clientY - rect.top) / rect.height) * vh
      };
    }

    svgEl.addEventListener('pointerdown', function (e) {
      if (e.button !== 0) return;
      var pt = toSVGPoint(e.clientX, e.clientY);
      var tl = context.layTiLe();
      var st = context.layTrangThai();

      var dxTam = pt.x - tl.x_tam;
      var dyTam = pt.y - tl.y_tam;
      var distTam = Math.sqrt(dxTam * dxTam + dyTam * dyTam);
      var rBanhDa = tl.r_banh_da || (tl.a_px + 28);

      var xDot = tl.xGraph ? tl.xGraph(st.V) : (tl.x_g_left + st.V * tl.scale_g_x);
      var yDot = tl.yGraph ? tl.yGraph(st.p) : (tl.y_g_bot - (st.p / tl.p_max) * tl.h_g);
      var distDot = Math.hypot(pt.x - xDot, pt.y - yDot);

      var xPiston = tl.xPiston ? tl.xPiston(st.V) : (tl.X_NAP + st.V * tl.scale_piston);
      var laPiston = (pt.x >= xPiston - 35 && pt.x <= xPiston + tl.chieuDaiCanPiston + 15) &&
                     (pt.y >= tl.y_tam - 50 && pt.y <= tl.y_tam + 50);

      if (distDot <= 24) {
        keoKieu = 'dot';
      } else if (distTam <= rBanhDa + 35) {
        keoKieu = 'banhda';
        psiTruoc = Math.atan2(dyTam, dxTam);
        lichSuGoc = [{ t: performance.now(), phi: st.phi }];
      } else if (laPiston) {
        keoKieu = 'piston';
      } else if (pt.y <= tl.y_g_bot + 10 && pt.x >= tl.x_g_left - 10 && pt.x <= tl.x_g_right + 10) {
        keoKieu = 'dot';
      }

      if (keoKieu) {
        pointerId = e.pointerId;
        try { svgEl.setPointerCapture(e.pointerId); } catch (err) {}
        context.dung();
        e.preventDefault();
      }
    });

    svgEl.addEventListener('pointermove', function (e) {
      if (!keoKieu) return;
      var pt = toSVGPoint(e.clientX, e.clientY);
      var tl = context.layTiLe();

      if (keoKieu === 'banhda') {
        var dx = pt.x - tl.x_tam;
        var dy = pt.y - tl.y_tam;
        var psiMoi = Math.atan2(dy, dx);
        var deltaPsi = psiMoi - psiTruoc;

        if (deltaPsi > Math.PI) deltaPsi -= 2 * Math.PI;
        else if (deltaPsi < -Math.PI) deltaPsi += 2 * Math.PI;

        psiTruoc = psiMoi;
        var deltaDeg = deltaPsi * (180 / Math.PI);

        // Bánh đà quay chiều kim đồng hồ tương ứng deltaDeg > 0
        if (Math.abs(deltaDeg) > 0.05) {
          if (deltaDeg < -0.5 && chuTuaNguoc) {
            chuTuaNguoc.style.opacity = '1';
          } else if (chuTuaNguoc) {
            chuTuaNguoc.style.opacity = '0';
          }

          context.buocGoc(deltaDeg);

          var now = performance.now();
          lichSuGoc.push({ t: now, phi: context.layTrangThai().phi });
          while (lichSuGoc.length > 2 && now - lichSuGoc[0].t > 100) {
            lichSuGoc.shift();
          }
        }
      } else if (keoKieu === 'piston') {
        var V_keo = tl.V_tu_piston ? tl.V_tu_piston(pt.x) : ((pt.x - tl.X_NAP) / tl.scale_piston);
        context.datTheoPiston(V_keo);
      } else if (keoKieu === 'dot') {
        var V_chuot = tl.V_tu_graph ? tl.V_tu_graph(pt.x) : ((pt.x - tl.x_g_left) / tl.scale_g_x);
        var p_chuot = tl.p_tu_graph ? tl.p_tu_graph(pt.y) : (((tl.y_g_bot - pt.y) / tl.h_g) * tl.p_max);
        context.datTheoDiemPV(V_chuot, p_chuot);
      }
    });

    function ketThucKeo(e) {
      if (keoKieu) {
        if (chuTuaNguoc) chuTuaNguoc.style.opacity = '0';

        if (keoKieu === 'banhda' && lichSuGoc.length >= 2) {
          var pDau = lichSuGoc[0];
          var pCuoi = lichSuGoc[lichSuGoc.length - 1];
          var dt = (pCuoi.t - pDau.t) / 1000;
          if (dt > 0.02) {
            var omega = (pCuoi.phi - pDau.phi) / dt;
            context.thaBanhDa(omega);
          }
        }

        try {
          if (pointerId !== null) svgEl.releasePointerCapture(pointerId);
        } catch (err) {}
        keoKieu = null;
        pointerId = null;
      }
    }

    svgEl.addEventListener('pointerup', ketThucKeo);
    svgEl.addEventListener('pointercancel', ketThucKeo);

    // Kéo thanh thời gian (Timeline 0-720°)
    var timelineEl = document.getElementById('hl-thanh-thoi-gian');
    if (timelineEl) {
      var dangKeoTimeline = false;

      function capNhatTuTimeline(e) {
        var rect = timelineEl.getBoundingClientRect();
        var f = (e.clientX - rect.left) / rect.width;
        f = Math.max(0, Math.min(1, f));
        var phiMoi = f * 720;
        context.datGoc(phiMoi);
      }

      timelineEl.addEventListener('pointerdown', function (e) {
        dangKeoTimeline = true;
        try { timelineEl.setPointerCapture(e.pointerId); } catch (err) {}
        context.dung();
        capNhatTuTimeline(e);
      });
      timelineEl.addEventListener('pointermove', function (e) {
        if (dangKeoTimeline) capNhatTuTimeline(e);
      });
      timelineEl.addEventListener('pointerup', function (e) {
        dangKeoTimeline = false;
        try { timelineEl.releasePointerCapture(e.pointerId); } catch (err) {}
      });
      timelineEl.addEventListener('pointercancel', function (e) {
        dangKeoTimeline = false;
        try { timelineEl.releasePointerCapture(e.pointerId); } catch (err) {}
      });
    }

    // Bàn phím điều khiển
    window.addEventListener('keydown', function (e) {
      if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
      if (e.code === 'Space') {
        context.chuyenChayDung();
        e.preventDefault();
      } else if (e.key === 'ArrowRight') {
        context.buocGoc(e.shiftKey ? 30 : 5);
        e.preventDefault();
      } else if (e.key === 'ArrowLeft') {
        context.buocGoc(e.shiftKey ? -30 : -5);
        e.preventDefault();
      } else if (e.key === 'KeyR' || e.key === 'r') {
        context.datLai();
      } else if (e.key === 'KeyG' || e.key === 'g') {
        context.ghimBongMa();
      }
    });
  }

  return {
    khoiTaoTuongTac: khoiTaoTuongTac
  };
});
