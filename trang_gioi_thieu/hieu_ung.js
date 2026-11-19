// Hiệu ứng giao diện của trang giới thiệu. Mọi hiệu ứng đều là phần thêm:
// không có JS, không có IntersectionObserver hoặc máy đặt "giảm chuyển động" thì nội dung vẫn hiện đầy đủ.
(function () {
  var goc = document.documentElement;
  var giam = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  if (document.startViewTransition) goc.classList.add('co-vt');

  // Thanh trên: đổ bóng khi đã cuộn, thanh tiến độ đọc, nút lên đầu trang
  var thanh = document.querySelector('.thanh');
  var tienDo = document.createElement('div');
  tienDo.className = 'tien-do';
  tienDo.setAttribute('aria-hidden', 'true');
  thanh.appendChild(tienDo);
  var lenDau = document.createElement('button');
  lenDau.type = 'button';
  lenDau.className = 'len-dau';
  lenDau.setAttribute('aria-label', 'Lên đầu trang');
  lenDau.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 19V5M5.5 11.5 12 5l6.5 6.5"/></svg>';
  lenDau.addEventListener('click', function () {
    window.scrollTo({ top: 0, behavior: giam ? 'auto' : 'smooth' });
    var h = document.querySelector('.tab.dang-mo h1');
    if (h) h.focus({ preventScroll: true });
  });
  document.body.appendChild(lenDau);
  var choCuon = false;
  function khiCuon() {
    choCuon = false;
    var y = window.scrollY, cao = goc.scrollHeight - window.innerHeight;
    thanh.classList.toggle('da-cuon', y > 8);
    tienDo.style.transform = 'scaleX(' + (cao > 0 ? Math.min(1, y / cao) : 0) + ')';
    lenDau.classList.toggle('hien', y > 900);
  }
  window.addEventListener('scroll', function () {
    if (!choCuon) { choCuon = true; requestAnimationFrame(khiCuon); }
  }, { passive: true });
  window.addEventListener('vera:tab', khiCuon);
  khiCuon();

  // Vạch trượt theo mục đang chọn: menu chính, tab ngang, danh sách chương
  var capNhatVach = [];
  function taoVach(ds, layNut, doc, le) {
    var v = document.createElement('span');
    v.className = 'vach';
    v.setAttribute('aria-hidden', 'true');
    ds.appendChild(v);
    ds.classList.add('co-vach');
    function dat() {
      var n = layNut();
      if (!n || !n.offsetWidth) { v.style.opacity = '0'; return; }
      if (doc) {
        v.style.transform = 'translateY(' + (n.offsetTop + le) + 'px)';
        v.style.height = (n.offsetHeight - 2 * le) + 'px';
      } else {
        v.style.transform = 'translateX(' + (n.offsetLeft + le) + 'px)';
        v.style.width = (n.offsetWidth - 2 * le) + 'px';
      }
      v.style.opacity = '1';
      // Lần đặt đầu không trượt từ góc trái
      if (!v.classList.contains('chay')) requestAnimationFrame(function () { requestAnimationFrame(function () { v.classList.add('chay'); }); });
    }
    capNhatVach.push(dat);
    if (window.ResizeObserver) new ResizeObserver(dat).observe(ds);
    return dat;
  }
  var menu = document.getElementById('menu');
  var datVachMenu = taoVach(menu, function () { return menu.querySelector('a[aria-current="page"]'); }, false, 12);
  window.addEventListener('vera:tab', datVachMenu);
  [].forEach.call(document.querySelectorAll('[data-nhom-tab]'), function (nhom) {
    var ds = nhom.querySelector('[role="tablist"]');
    if (!ds) return;
    var doc = ds.classList.contains('ds-chuong');
    var dat = taoVach(ds, function () { return ds.querySelector('[aria-selected="true"]'); }, doc, doc ? 8 : 14);
    nhom.addEventListener('nhom-tab:chon', dat);
  });
  window.addEventListener('resize', function () { capNhatVach.forEach(function (f) { f(); }); });
  if (document.fonts && document.fonts.ready) document.fonts.ready.then(function () { capNhatVach.forEach(function (f) { f(); }); });

  // Thẻ phòng ở Tham quan: đổi phòng thì ảnh và chữ hiện lại nhẹ nhàng
  var thePhong = document.querySelector('.the-phong');
  window.addEventListener('vera:da-chon', function () {
    if (giam || !thePhong) return;
    thePhong.classList.remove('vua-doi');
    void thePhong.offsetWidth;
    thePhong.classList.add('vua-doi');
  });

  if (giam || !('IntersectionObserver' in window)) return;

  // Nội dung hiện dần khi cuộn tới, các phần tử cùng hàng nối đuôi nhau
  var HIEN = ['.dau-doan', '.o', '.dai-so > div', '.doan > h2', '.trich', '.o-so', '.cau-hoi', '.cac-buoc > li', '.the-tho', '.thiet-bi', '.anh-the', '.cuon-bang', '.moc', '.o-hdh',
    '.ds-tinh-nang > li', '.so-toi > div', '.trich-nho', '.phieu-du-an', '.the-do', '.hinh-do > figure', '.dong-ke > li'].join(', ');
  var quanSat = new IntersectionObserver(function (muc) {
    muc.forEach(function (m) {
      if (!m.isIntersecting) return;
      quanSat.unobserve(m.target);
      m.target.classList.add('da-hien');
    });
  }, { rootMargin: '0px 0px -6% 0px', threshold: 0.06 });
  [].forEach.call(document.querySelectorAll(HIEN), function (el) {
    if (el.closest('.mo-dau, .ds-chuong')) return;
    var cungHang = [].filter.call(el.parentNode.children, function (c) { return c.matches(HIEN); });
    el.style.setProperty('--tre', Math.min(cungHang.indexOf(el), 5) * 50 + 'ms');
    el.classList.add('cho-hien');
    el.addEventListener('transitionend', function xong(e) {
      if (e.target !== el || e.propertyName !== 'opacity' || !el.classList.contains('da-hien')) return;
      el.classList.remove('cho-hien', 'da-hien');
      el.style.removeProperty('--tre');
      el.removeEventListener('transitionend', xong);
    });
    quanSat.observe(el);
  });

  // Thanh hiệu năng chạy từ 0 tới giá trị đo
  var quanSatDo = new IntersectionObserver(function (muc) {
    muc.forEach(function (m) {
      if (!m.isIntersecting) return;
      quanSatDo.unobserve(m.target);
      m.target.classList.add('da-chay');
    });
  }, { threshold: 0.4 });
  [].forEach.call(document.querySelectorAll('.do, .bd-log'), function (el) { el.classList.add('cho-chay'); quanSatDo.observe(el); });
})();
