/**
 * keo.js - Bộ xử lý tương tác kéo thả Pointer Events & Cảm ứng đa nền tảng
 * Hỗ trợ kéo góc (bánh đà, núm vặn), kéo trục (pít-tông), quán tính và bàn phím
 * UMD: Chạy được trong Node.js (test) và trình duyệt (window.HLKeo)
 */
(function (root, factory) {
  if (typeof define === 'function' && define.amd) {
    define([], factory);
  } else if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.HLKeo = factory();
  }
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  /**
   * Kéo theo trục (ngang hoặc dọc)
   * @param {Element} element - Phần tử nhận tương tác
   * @param {Object} options - { truc: 'x'|'y', min, max, giaTriBanDau, khiDoi, khiTha }
   */
  function taoKeoTruc(element, options) {
    if (!element || typeof window === 'undefined') return null;
    options = options || {};
    var truc = options.truc || 'x';
    var min = options.min !== undefined ? options.min : 0;
    var max = options.max !== undefined ? options.max : 100;
    var giaTri = options.giaTriBanDau !== undefined ? options.giaTriBanDau : min;
    var dangKeo = false;
    var pointerId = null;
    var startPos = 0;
    var startVal = 0;

    element.style.touchAction = 'none';
    element.style.cursor = truc === 'x' ? 'ew-resize' : 'ns-resize';
    element.setAttribute('role', 'slider');
    element.setAttribute('tabindex', '0');
    element.setAttribute('aria-valuemin', min);
    element.setAttribute('aria-valuemax', max);
    element.setAttribute('aria-valuenow', giaTri);

    function onPointerDown(e) {
      dangKeo = true;
      pointerId = e.pointerId;
      if (element.setPointerCapture) element.setPointerCapture(pointerId);
      startPos = truc === 'x' ? e.clientX : e.clientY;
      startVal = giaTri;
      e.preventDefault();
    }

    function onPointerMove(e) {
      if (!dangKeo) return;
      var curPos = truc === 'x' ? e.clientX : e.clientY;
      var dPx = curPos - startPos;
      var dVal = options.pxSangGiaTri ? options.pxSangGiaTri(dPx) : dPx;
      var newVal = Math.max(min, Math.min(max, startVal + dVal));
      giaTri = newVal;
      element.setAttribute('aria-valuenow', giaTri);
      if (options.khiDoi) options.khiDoi(giaTri);
    }

    function onPointerUp(e) {
      if (!dangKeo) return;
      dangKeo = false;
      if (element.releasePointerCapture && pointerId !== null) {
        try { element.releasePointerCapture(pointerId); } catch (err) {}
      }
      pointerId = null;
      if (options.khiTha) options.khiTha(giaTri);
    }

    function onKeyDown(e) {
      var step = options.step || (max - min) / 50;
      if (e.shiftKey) step *= 5;
      if (e.key === 'ArrowRight' || e.key === 'ArrowUp') {
        giaTri = Math.min(max, giaTri + step);
        element.setAttribute('aria-valuenow', giaTri);
        if (options.khiDoi) options.khiDoi(giaTri);
        e.preventDefault();
      } else if (e.key === 'ArrowLeft' || e.key === 'ArrowDown') {
        giaTri = Math.max(min, giaTri - step);
        element.setAttribute('aria-valuenow', giaTri);
        if (options.khiDoi) options.khiDoi(giaTri);
        e.preventDefault();
      }
    }

    element.addEventListener('pointerdown', onPointerDown);
    element.addEventListener('pointermove', onPointerMove);
    element.addEventListener('pointerup', onPointerUp);
    element.addEventListener('pointercancel', onPointerUp);
    element.addEventListener('keydown', onKeyDown);

    return {
      datGiaTri: function (v) {
        giaTri = Math.max(min, Math.min(max, v));
        element.setAttribute('aria-valuenow', giaTri);
      },
      layGiaTri: function () { return giaTri; },
      huy: function () {
        element.removeEventListener('pointerdown', onPointerDown);
        element.removeEventListener('pointermove', onPointerMove);
        element.removeEventListener('pointerup', onPointerUp);
        element.removeEventListener('pointercancel', onPointerUp);
        element.removeEventListener('keydown', onKeyDown);
      }
    };
  }

  /**
   * Kéo góc quanh tâm (bánh đà, núm xoay)
   */
  function taoKeoGoc(element, options) {
    if (!element || typeof window === 'undefined') return null;
    options = options || {};
    var dangKeo = false;
    var pointerId = null;
    var gocTruoc = 0;
    var lichSu = []; // lưu { t, goc } để tính vận tốc khi thả

    element.style.touchAction = 'none';
    element.style.cursor = 'grab';

    function layTam() {
      if (options.layTam) return options.layTam();
      var rect = element.getBoundingClientRect();
      return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
    }

    function onPointerDown(e) {
      dangKeo = true;
      pointerId = e.pointerId;
      if (element.setPointerCapture) element.setPointerCapture(pointerId);
      var tam = layTam();
      gocTruoc = Math.atan2(e.clientY - tam.y, e.clientX - tam.x);
      lichSu = [{ t: performance.now(), goc: gocTruoc }];
      if (options.khiBatDau) options.khiBatDau();
      e.preventDefault();
    }

    function onPointerMove(e) {
      if (!dangKeo) return;
      var tam = layTam();
      var gocMoi = Math.atan2(e.clientY - tam.y, e.clientX - tam.x);
      var dGoc = gocMoi - gocTruoc;

      // Xử lý góc chuyển qua ranh giới [-PI, PI]
      if (dGoc > Math.PI) dGoc -= 2 * Math.PI;
      else if (dGoc < -Math.PI) dGoc += 2 * Math.PI;

      gocTruoc = gocMoi;
      var now = performance.now();
      lichSu.push({ t: now, goc: gocMoi, dGoc: dGoc });
      if (lichSu.length > 8) lichSu.shift();

      if (options.khiQuay) options.khiQuay(dGoc, gocMoi);
    }

    function onPointerUp(e) {
      if (!dangKeo) return;
      dangKeo = false;
      if (element.releasePointerCapture && pointerId !== null) {
        try { element.releasePointerCapture(pointerId); } catch (err) {}
      }
      pointerId = null;

      // Tính vận tốc góc khi thả (rad/s)
      var vanTocGoc = 0;
      if (lichSu.length >= 2) {
        var dt = (lichSu[lichSu.length - 1].t - lichSu[0].t) / 1000;
        if (dt > 0.01 && dt < 0.25) {
          var tongDGoc = 0;
          for (var i = 1; i < lichSu.length; i++) {
            tongDGoc += lichSu[i].dGoc || 0;
          }
          vanTocGoc = tongDGoc / dt;
        }
      }
      if (options.khiTha) options.khiTha(vanTocGoc);
    }

    element.addEventListener('pointerdown', onPointerDown);
    element.addEventListener('pointermove', onPointerMove);
    element.addEventListener('pointerup', onPointerUp);
    element.addEventListener('pointercancel', onPointerUp);

    return {
      huy: function () {
        element.removeEventListener('pointerdown', onPointerDown);
        element.removeEventListener('pointermove', onPointerMove);
        element.removeEventListener('pointerup', onPointerUp);
        element.removeEventListener('pointercancel', onPointerUp);
      }
    };
  }

  return {
    taoKeoTruc: taoKeoTruc,
    taoKeoGoc: taoKeoGoc
  };
});
