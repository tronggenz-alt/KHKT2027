/**
 * khung-trang.js - Bộ khung giao diện thống nhất cho các trang mô phỏng Hoá lý
 * Quản lý Dark mode, Thu gọn bảng, Toàn màn hình, Xuất ảnh và Chia sẻ link
 * UMD: Chạy được trong Node.js (test) và trình duyệt (window.HLKhung)
 */
(function (root, factory) {
  if (typeof define === 'function' && define.amd) {
    define([], factory);
  } else if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.HLKhung = factory();
  }
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  function thietLapKhung(options) {
    if (typeof window === 'undefined') return;
    options = options || {};
    var idMoPhong = options.id || '';
    var tenMoPhong = options.ten || '';

    // 1. Quản lý Theme Sáng / Tối
    var nutTheme = document.getElementById('hl-nut-theme');
    var THEME_KEY = 'giai-chuoi-giao-dien';

    function apDungTheme(theme) {
      document.documentElement.setAttribute('data-theme', theme);
      if (nutTheme) {
        nutTheme.setAttribute('title', theme === 'toi' ? 'Chuyển sang chế độ sáng' : 'Chuyển sang chế độ tối');
        nutTheme.innerHTML = theme === 'toi' ?
          '<svg class="w-5 h-5 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 3v1m0 16v1m9-9h-1M4 9h-1m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z"/></svg>' :
          '<svg class="w-5 h-5 text-zinc-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z"/></svg>';
      }
      try { localStorage.setItem(THEME_KEY, theme); } catch (e) {}
    }

    var themeHienTai = 'toi';
    try {
      var saved = localStorage.getItem(THEME_KEY);
      if (saved) themeHienTai = saved;
    } catch (e) {}
    apDungTheme(themeHienTai);

    if (nutTheme) {
      nutTheme.onclick = function () {
        themeHienTai = themeHienTai === 'toi' ? 'sang' : 'toi';
        apDungTheme(themeHienTai);
        if (options.onDoiTheme) options.onDoiTheme(themeHienTai);
      };
    }

    // 2. Thu gọn bảng điều khiển (để hình chiếm toàn bộ chiều ngang)
    var nutThuGon = document.getElementById('hl-nut-thu-gon');
    var cotHinh = document.getElementById('hl-cot-hinh');
    var cotBang = document.getElementById('hl-cot-bang');
    var dangThuGon = false;

    if (nutThuGon && cotHinh && cotBang) {
      nutThuGon.onclick = function () {
        dangThuGon = !dangThuGon;
        if (dangThuGon) {
          cotBang.classList.add('hidden');
          cotHinh.classList.remove('lg:col-span-8', 'xl:col-span-9');
          cotHinh.classList.add('col-span-12');
          nutThuGon.innerHTML = '<span class="text-xs font-semibold">Hiện bảng ❯</span>';
        } else {
          cotBang.classList.remove('hidden');
          cotHinh.classList.remove('col-span-12');
          cotHinh.classList.add('lg:col-span-8', 'xl:col-span-9');
          nutThuGon.innerHTML = '<span class="text-xs font-semibold">Thu gọn bảng ❮</span>';
        }
        window.dispatchEvent(new Event('resize'));
      };
    }

    // 3. Toàn màn hình (Fullscreen API)
    var nutToanManHinh = document.getElementById('hl-nut-toan-man-hinh');
    if (nutToanManHinh) {
      nutToanManHinh.onclick = function () {
        if (!document.fullscreenElement) {
          document.documentElement.requestFullscreen().catch(function () {});
        } else {
          if (document.exitFullscreen) document.exitFullscreen();
        }
      };
    }

    // 4. Sao chép link chia sẻ
    var nutChiaSe = document.getElementById('hl-nut-chia-se');
    if (nutChiaSe) {
      nutChiaSe.onclick = function () {
        var url = window.location.href;
        navigator.clipboard.writeText(url).then(function () {
          nutChiaSe.title = 'Đã sao chép link!';
          setTimeout(function () { nutChiaSe.title = 'Sao chép link chia sẻ'; }, 2000);
        }).catch(function () {});
      };
    }

    // 5. Xuất ảnh PNG của khung mô phỏng
    var nutXuatAnh = document.getElementById('hl-nut-xuat-anh');
    if (nutXuatAnh && options.laySVG) {
      nutXuatAnh.onclick = function () {
        var svgEl = options.laySVG();
        if (!svgEl) return;
        var xml = new XMLSerializer().serializeToString(svgEl);
        var svgBlob = new Blob([xml], { type: 'image/svg+xml;charset=utf-8' });
        var URL = window.URL || window.webkitURL || window;
        var blobURL = URL.createObjectURL(svgBlob);
        var image = new Image();
        image.onload = function () {
          var canvas = document.createElement('canvas');
          canvas.width = svgEl.clientWidth * 2;
          canvas.height = svgEl.clientHeight * 2;
          var ctx = canvas.getContext('2d');
          ctx.drawImage(image, 0, 0, canvas.width, canvas.height);
          var png = canvas.toDataURL('image/png');
          var a = document.createElement('a');
          a.download = (idMoPhong || 'mo-phong') + '.png';
          a.href = png;
          a.click();
        };
        image.src = blobURL;
      };
    }
  }

  return {
    thietLapKhung: thietLapKhung
  };
});
