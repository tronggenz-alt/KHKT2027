// Nền chìm của trang giới thiệu: một mạng lục giác kiểu khung phân tử vẽ sau nội dung.
// Để yên thì rất nhạt và trôi chậm theo từng mảng; con trỏ tới đâu, liên kết quanh đó đậm lên rồi nhạt dần.
// Chỉ là phần trang trí: không có JS hoặc canvas thì trang vẫn đầy đủ. Máy đặt "giảm chuyển động" chỉ nhận một khung tĩnh.
(function () {
  var goc = document.documentElement;
  var cv = document.createElement('canvas');
  if (!cv.getContext) return;
  var cx = cv.getContext('2d');
  if (!cx) return;
  cv.className = 'nen-chim';
  cv.setAttribute('aria-hidden', 'true');
  document.body.insertBefore(cv, document.body.firstChild);

  var giam = window.matchMedia('(prefers-reduced-motion: reduce)');
  var MUC = 6;            // số mức đậm nhạt khi gộp nét vẽ
  var A_NGHI = 0.15;      // độ đậm tối đa khi để yên
  var A_NONG = 0.55;      // độ đậm tối đa quanh con trỏ
  var W = 0, H = 0, ti = 1, canhLG = 40, banKinh = 230;
  var vx, vy, vMo, vNong, vNut, ea, eb, eMuc, eMucNong, hx, hy, hCo;
  var soDinh = 0, soCanh = 0, soO = 0;
  var mauChu = '#1f2328', mauNhan = '#0b63d1';
  var tx = -9999, ty = -9999, coTro = false;
  var song = [];          // các vòng lan ra khi đổi tab
  var truoc = 0, khung = 0, dangChay = false;

  function bam(i, j) {
    var n = Math.sin(i * 127.1 + j * 311.7) * 43758.5453;
    return n - Math.floor(n);
  }
  function nhieu(x, y) {
    var xi = Math.floor(x), yi = Math.floor(y), fx = x - xi, fy = y - yi;
    fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
    var a = bam(xi, yi), b = bam(xi + 1, yi), c = bam(xi, yi + 1), d = bam(xi + 1, yi + 1);
    return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
  }
  function muot(a, b, x) {
    x = (x - a) / (b - a);
    x = x < 0 ? 0 : x > 1 ? 1 : x;
    return x * x * (3 - 2 * x);
  }

  function layMau() {
    var k = getComputedStyle(goc);
    mauChu = k.getPropertyValue('--chu').trim() || mauChu;
    mauNhan = k.getPropertyValue('--nhan').trim() || mauNhan;
  }

  // Dựng mạng lục giác phủ kín khung nhìn (dư một ô mỗi phía)
  function dung() {
    W = window.innerWidth; H = window.innerHeight;
    ti = Math.min(window.devicePixelRatio || 1, 1.5);
    cv.width = Math.round(W * ti); cv.height = Math.round(H * ti);
    canhLG = Math.max(28, Math.min(46, W / 34));
    banKinh = W < 700 ? 150 : 230;
    var s = canhLG, ngang = Math.sqrt(3) * s, doc = 1.5 * s;
    var cot = Math.ceil(W / ngang) + 2, hang = Math.ceil(H / doc) + 2;
    var khoa = {}, khoaCanh = {}, X = [], Y = [], A = [], B = [], OX = [], OY = [], OC = [];
    function dinh(x, y) {
      var k = Math.round(x * 2) + ',' + Math.round(y * 2);
      if (khoa[k] === undefined) { khoa[k] = X.length; X.push(x); Y.push(y); }
      return khoa[k];
    }
    for (var r = -1; r < hang; r++) {
      for (var c = -1; c < cot; c++) {
        var ox = c * ngang + (r & 1 ? ngang / 2 : 0), oy = r * doc, d = [];
        for (var g = 0; g < 6; g++) {
          var goc6 = Math.PI / 6 + g * Math.PI / 3;
          d.push(dinh(ox + s * Math.cos(goc6), oy + s * Math.sin(goc6)));
        }
        for (g = 0; g < 6; g++) {
          var p = d[g], q = d[(g + 1) % 6], kc = p < q ? p + '-' + q : q + '-' + p;
          if (!khoaCanh[kc]) { khoaCanh[kc] = 1; A.push(p); B.push(q); }
        }
        // Một phần nhỏ số ô có vòng thơm bên trong, như vòng benzen
        OX.push(ox); OY.push(oy); OC.push(bam(c * 3 + 11, r * 7 + 5) < 0.13 ? 1 : 0);
      }
    }
    soDinh = X.length; soCanh = A.length; soO = OX.length;
    vx = new Float32Array(X); vy = new Float32Array(Y);
    vMo = new Float32Array(soDinh); vNong = new Float32Array(soDinh); vNut = new Uint8Array(soDinh);
    for (var i = 0; i < soDinh; i++) vNut[i] = bam(Math.round(X[i]) + 3, Math.round(Y[i]) + 17) < 0.16 ? 1 : 0;
    ea = new Uint32Array(A); eb = new Uint32Array(B);
    eMuc = new Uint8Array(soCanh); eMucNong = new Uint8Array(soCanh);
    hx = new Float32Array(OX); hy = new Float32Array(OY); hCo = new Uint8Array(OC);
  }

  function ve(luc) {
    var t = luc / 1000, dt = Math.min(0.1, (luc - truoc) / 1000 || 0.016);
    truoc = luc;
    var f = 1 / (canhLG * 9), i, k;
    var tat = Math.exp(-dt / 0.85), R = banKinh, conNong = false;

    // Bỏ các vòng lan đã hết
    for (i = song.length - 1; i >= 0; i--) if (t - song[i].t > 1.6) song.splice(i, 1);

    for (i = 0; i < soDinh; i++) {
      var x = vx[i], y = vy[i];
      // Mảng nhạt trôi chậm: nơi hiện nơi ẩn, không phủ đều như giấy dán tường
      vMo[i] = muot(0.40, 0.78, nhieu(x * f + t * 0.045, y * f - t * 0.028));
      var gan = 0;
      if (coTro) {
        var dx = x - tx, dy = y - ty, kc = Math.sqrt(dx * dx + dy * dy);
        if (kc < R) { gan = 1 - kc / R; gan = gan * gan * (3 - 2 * gan); }
      }
      for (k = 0; k < song.length; k++) {
        var tuoi = t - song[k].t, sx = x - song[k].x, sy = y - song[k].y;
        var lech = Math.abs(Math.sqrt(sx * sx + sy * sy) - tuoi * 760);
        if (lech < 110) { var v = (1 - lech / 110) * (1 - tuoi / 1.6) * 0.55; if (v > gan) gan = v; }
      }
      var n = vNong[i];
      n = gan > n ? n + (gan - n) * Math.min(1, dt * 14) : Math.max(gan, n * tat);
      if (n < 0.004) n = 0; else conNong = true;
      vNong[i] = n;
    }

    cx.setTransform(ti, 0, 0, ti, 0, 0);
    cx.clearRect(0, 0, W, H);
    cx.lineCap = 'round';

    for (i = 0; i < soCanh; i++) {
      var a = ea[i], b = eb[i];
      eMuc[i] = Math.round((vMo[a] + vMo[b]) * 0.5 * MUC);
      eMucNong[i] = Math.round((vNong[a] + vNong[b]) * 0.5 * MUC);
    }
    // Lớp để yên: màu chữ, rất nhạt
    cx.strokeStyle = mauChu; cx.lineWidth = 1;
    for (k = 1; k <= MUC; k++) {
      cx.globalAlpha = A_NGHI * k / MUC;
      cx.beginPath();
      for (i = 0; i < soCanh; i++) if (eMuc[i] === k) { cx.moveTo(vx[ea[i]], vy[ea[i]]); cx.lineTo(vx[eb[i]], vy[eb[i]]); }
      cx.stroke();
    }
    // Lớp quanh con trỏ: màu nhấn, đậm hơn
    if (conNong) {
      cx.strokeStyle = mauNhan; cx.lineWidth = 1.3;
      for (k = 1; k <= MUC; k++) {
        cx.globalAlpha = A_NONG * k / MUC;
        cx.beginPath();
        for (i = 0; i < soCanh; i++) if (eMucNong[i] === k) { cx.moveTo(vx[ea[i]], vy[ea[i]]); cx.lineTo(vx[eb[i]], vy[eb[i]]); }
        cx.stroke();
      }
    }
    // Nguyên tử ở một số nút
    for (i = 0; i < soDinh; i++) {
      if (!vNut[i]) continue;
      var mo = vMo[i], nong = vNong[i];
      if (mo > 0.08) { cx.globalAlpha = A_NGHI * 1.5 * mo; cx.fillStyle = mauChu; cx.beginPath(); cx.arc(vx[i], vy[i], 2.4, 0, 6.2832); cx.fill(); }
      if (nong > 0.03) { cx.globalAlpha = Math.min(1, A_NONG * 1.5 * nong); cx.fillStyle = mauNhan; cx.beginPath(); cx.arc(vx[i], vy[i], 2.4 + nong * 1.4, 0, 6.2832); cx.fill(); }
    }
    // Vòng thơm trong một số ô
    var rv = canhLG * 0.56;
    for (i = 0; i < soO; i++) {
      if (!hCo[i]) continue;
      var ox = hx[i], oy = hy[i];
      var m2 = muot(0.40, 0.78, nhieu(ox * f + t * 0.045, oy * f - t * 0.028)), n2 = 0;
      if (conNong) {
        if (coTro) { var ex = ox - tx, ey = oy - ty, ek = Math.sqrt(ex * ex + ey * ey); if (ek < R) { n2 = 1 - ek / R; n2 = n2 * n2 * (3 - 2 * n2); } }
      }
      if (m2 > 0.08) { cx.globalAlpha = A_NGHI * m2; cx.strokeStyle = mauChu; cx.lineWidth = 1; cx.beginPath(); cx.arc(ox, oy, rv, 0, 6.2832); cx.stroke(); }
      if (n2 > 0.03) { cx.globalAlpha = A_NONG * n2; cx.strokeStyle = mauNhan; cx.lineWidth = 1.3; cx.beginPath(); cx.arc(ox, oy, rv, 0, 6.2832); cx.stroke(); }
    }
    cx.globalAlpha = 1;
    return conNong;
  }

  // Vòng lặp: đủ 60 khung/giây khi đang có vùng đậm, khoảng 30 khung/giây khi chỉ trôi nhẹ
  var dangNong = false;
  function lap(luc) {
    if (!dangChay) return;
    khung = requestAnimationFrame(lap);
    if (!dangNong && !coTro && !song.length && luc - truoc < 32) return;
    dangNong = ve(luc);
  }
  function chay() {
    if (giam.matches) { dangChay = false; cancelAnimationFrame(khung); ve(0); return; }
    if (dangChay || document.hidden) return;
    dangChay = true; truoc = performance.now();
    khung = requestAnimationFrame(lap);
  }
  function dungLai() { dangChay = false; cancelAnimationFrame(khung); }

  window.addEventListener('pointermove', function (e) {
    tx = e.clientX; ty = e.clientY; coTro = true;
  }, { passive: true });
  function hetTro() { coTro = false; }
  goc.addEventListener('mouseleave', hetTro);
  window.addEventListener('blur', hetTro);
  window.addEventListener('pointerup', function (e) { if (e.pointerType !== 'mouse') hetTro(); }, { passive: true });
  window.addEventListener('pointercancel', hetTro, { passive: true });

  // Đổi tab: một vòng lan nhẹ từ giữa phía trên màn hình
  window.addEventListener('vera:tab', function () {
    if (giam.matches || !dangChay) return;
    song.push({ x: W / 2, y: H * 0.3, t: performance.now() / 1000 });
    if (song.length > 3) song.shift();
  });

  document.addEventListener('visibilitychange', function () { if (document.hidden) dungLai(); else chay(); });
  var hen = 0;
  window.addEventListener('resize', function () {
    clearTimeout(hen);
    hen = setTimeout(function () {
      // Trên điện thoại thanh địa chỉ co giãn làm đổi chiều cao liên tục: chỉ dựng lại khi thật sự cần
      if (window.innerWidth === W && Math.abs(window.innerHeight - H) < 140 && window.innerHeight <= H) return;
      dung(); if (giam.matches) ve(0);
    }, 160);
  });
  function doiMau() { layMau(); if (giam.matches) ve(0); }
  if (window.MutationObserver) new MutationObserver(doiMau).observe(goc, { attributes: true, attributeFilter: ['data-theme'] });
  var toi = window.matchMedia('(prefers-color-scheme: dark)');
  if (toi.addEventListener) { toi.addEventListener('change', doiMau); giam.addEventListener('change', function () { dungLai(); chay(); }); }

  layMau(); dung(); chay();
})();
