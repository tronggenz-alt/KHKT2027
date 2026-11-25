// Chuyển động của phong cách lưới: hiện dần khi cuộn tới, băng ảnh thật, thẻ ảnh nghiêng theo con trỏ.
// Chỉ là phần trang trí: không có JS thì mọi nội dung vẫn hiện đủ. Máy đặt "giảm chuyển động" thì đứng yên.
(function () {
  var yen = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;

  // Hiện dần: các ô cùng hàng lệch nhau một chút cho có nhịp
  var can = [].slice.call(document.querySelectorAll('[data-hien]'));
  if (!('IntersectionObserver' in window) || yen) {
    can.forEach(function (n) { n.classList.add('da-hien'); });
  } else {
    var io = new IntersectionObserver(function (ds) {
      ds.forEach(function (d) {
        if (!d.isIntersecting) return;
        var n = d.target, anh = [].slice.call(n.parentNode.children).filter(function (x) { return x.hasAttribute('data-hien'); });
        n.style.setProperty('--tre', Math.min(anh.indexOf(n), 6) * 0.07 + 's');
        n.classList.add('da-hien');
        io.unobserve(n);
      });
    }, { threshold: 0, rootMargin: "0px 0px -6% 0px" });
    can.forEach(function (n) { io.observe(n); });
  }

  // Băng ảnh thật: tự chuyển, dừng khi rê chuột hoặc khi tab bị ẩn
  var bang = document.getElementById('truot-that');
  if (bang) {
    var o = [].slice.call(bang.querySelectorAll('.truot')), dem = document.getElementById('dem-truot'), i = 0, hen = null;
    var di = function (buoc) {
      o[i].classList.remove('hien-truot');
      i = (i + buoc + o.length) % o.length;
      o[i].classList.add('hien-truot');
      if (dem) dem.textContent = String(i + 1).padStart(2, '0') + ' / ' + String(o.length).padStart(2, '0');
    };
    var chay = function () { if (!yen && !hen) hen = setInterval(function () { if (!document.hidden) di(1); }, 4500); };
    var dung = function () { clearInterval(hen); hen = null; };
    [].forEach.call(document.querySelectorAll('.nut-truot'), function (b) {
      b.addEventListener('click', function () { dung(); di(+b.dataset.buoc); chay(); });
    });
    bang.parentNode.addEventListener('mouseenter', dung);
    bang.parentNode.addEventListener('mouseleave', chay);
    chay();
  }

  // Thẻ ảnh nghiêng nhẹ theo con trỏ (chỉ với chuột, không áp cho màn hình cảm ứng)
  if (!yen && window.matchMedia && matchMedia('(hover: hover) and (pointer: fine)').matches) {
    [].forEach.call(document.querySelectorAll('[data-nghieng]'), function (t) {
      t.addEventListener('pointermove', function (e) {
        var r = t.getBoundingClientRect(), x = (e.clientX - r.left) / r.width - 0.5, y = (e.clientY - r.top) / r.height - 0.5;
        t.style.setProperty('--ry', (x * 7).toFixed(2) + 'deg');
        t.style.setProperty('--rx', (-y * 7).toFixed(2) + 'deg');
      });
      t.addEventListener('pointerleave', function () { t.style.setProperty('--ry', '0deg'); t.style.setProperty('--rx', '0deg'); });
    });
  }
})();

// Video nền phần đầu: chỉ chạy khi đang thấy và máy không đặt "giảm chuyển động" (khi đó chỉ hiện ảnh tĩnh)
(function () {
  var v = document.getElementById('vid-nen');
  if (!v) return;
  if (window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches) { v.removeAttribute('autoplay'); v.pause(); return; }
  var chay = function () { var p = v.play(); if (p && p.catch) p.catch(function () {}); };
  if ('IntersectionObserver' in window) {
    new IntersectionObserver(function (ds) { ds[0].isIntersecting ? chay() : v.pause(); }).observe(v);
  } else chay();
})();
