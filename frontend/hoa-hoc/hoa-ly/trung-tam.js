/**
 * trung-tam.js - Hiển thị lưới thẻ mô phỏng Hoá lý từ danh-muc.json
 */
(function () {
  'use strict';

  async function khoiTao() {
    var container = document.getElementById('luoi-mo-phong');
    if (!container) return;

    try {
      var res = await fetch('/static/hoa-hoc/hoa-ly/danh-muc.json', { cache: 'no-cache' });
      if (!res.ok) throw new Error('Không thể tải danh mục');
      var data = await res.json();

      var tieuDeEl = document.getElementById('hl-muc-tieu-de');
      var moTaEl = document.getElementById('hl-muc-mo-ta');
      if (tieuDeEl && data.ten_muc) tieuDeEl.textContent = data.ten_muc;
      if (moTaEl && data.mo_ta_muc) moTaEl.textContent = data.mo_ta_muc;

      container.innerHTML = '';
      (data.mo_phong || []).forEach(function (mp) {
        var card = document.createElement('div');
        var laCo = mp.trang_thai === 'co';
        var laThuVien = mp.loai === 'thu-vien';
        card.className = 'hl-card-hub' + 
          (laThuVien ? ' hl-card-hub-library ring-2 ring-sky-500/30 dark:ring-sky-400/20 bg-gradient-to-br from-sky-500/5 via-transparent to-transparent' : '') +
          (laCo ? ' hl-card-hub-active' : ' hl-card-hub-coming');

        var nhanHtml = (mp.nhan || []).map(function (nh) {
          return '<span class="hl-tag' + (laThuVien ? ' bg-sky-500/15 text-sky-600 dark:text-sky-400 border-sky-500/30' : '') + '">' + nh + '</span>';
        }).join('');

        var ctaText = laThuVien ? 'Vào thư viện' : 'Vào mô phỏng';
        var ctaHtml = laCo
          ? '<a href="' + mp.duong_dan + '" class="hl-btn-hub' + (laThuVien ? ' bg-sky-500 hover:bg-sky-400 text-white shadow-sky-500/20' : '') + '"><span>' + ctaText + '</span><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="w-4 h-4"><path d="M5 12h14M12 5l7 7-7 7"/></svg></a>'
          : '<span class="hl-tag-coming">Sắp ra mắt</span>';

        card.innerHTML =
          '<div class="hl-card-top">' +
            '<span class="hl-card-icon text-2xl font-bold font-serif' + (laThuVien ? ' text-sky-500 dark:text-sky-400' : '') + '" aria-hidden="true">' + (mp.bieu_tuong || '⚙️') + '</span>' +
            '<div class="hl-tags">' + nhanHtml + '</div>' +
          '</div>' +
          '<h2 class="hl-card-title">' + mp.ten + '</h2>' +
          (mp.phu_de ? '<p class="hl-card-sub">' + mp.phu_de + '</p>' : '') +
          '<p class="hl-card-desc">' + (mp.mo_ta || '') + '</p>' +
          '<div class="hl-card-bottom">' + ctaHtml + '</div>';

        if (laCo) {
          card.style.cursor = 'pointer';
          card.addEventListener('click', function (e) {
            if (e.target.tagName.toLowerCase() !== 'a') {
              window.location.href = mp.duong_dan;
            }
          });
        }

        container.appendChild(card);
      });
    } catch (err) {
      console.error('Lỗi khởi tạo trung tâm Hoá lý:', err);
      container.innerHTML = '<p class="text-muc-2">Không thể tải danh mục mô phỏng. Vui lòng thử lại sau.</p>';
    }
  }

  // Khởi tạo nút sáng / tối dùng chung với app
  function khoiTaoNutCheDo() {
    var nut = document.getElementById('nut-che-do');
    if (!nut) return;
    nut.addEventListener('click', function () {
      var hienTai = document.documentElement.dataset.theme;
      var moi = hienTai === 'toi' ? 'sang' : 'toi';
      document.documentElement.dataset.theme = moi;
      try {
        localStorage.setItem('giai-chuoi-giao-dien', moi);
      } catch (e) {}
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
      khoiTao();
      khoiTaoNutCheDo();
    });
  } else {
    khoiTao();
    khoiTaoNutCheDo();
  }
})();
