(function () {
  // Bảng phòng dùng chung với cảnh 3D: r là [x0, z0, x1, z1] theo mét, lấy từ bảng khu của ứng dụng
  window.VERA_TANG = {
    1: [
      { ten: 'Phòng thí nghiệm Hóa', k: 'lab', r: [0, 0, 20, 15], anh: 'hoa_lab', ta: 'Không gian chính với các cụm bàn học sinh, bàn giáo viên có màn hình lớn, tủ hút, khu đun nóng và ngăn kính chế tạo dụng cụ.' },
      { ten: 'Phòng hóa chất', k: 'kho', r: [20, 0, 27, 15], anh: 'hoa_kho', ta: 'Kệ chứa 720 hóa chất. Tra cứu tên chất là được dẫn tới đúng vị trí trên kệ.' },
      { ten: 'Hành lang', k: '', r: [0, -2.2, 27, 0], anh: 'hoa_hanhlang', ta: 'Trục nối các phòng, trưng bày lịch sử hóa học, có cửa tự động ở từng phòng.' },
      { ten: 'Phòng máy phân tích', k: 'may', r: [0, -9, 13.5, -2.2], anh: 'hoa_may', ta: '14 thiết bị hiện đại, mỗi máy có màn hình vận hành và hướng dẫn riêng.' },
      { ten: 'Phòng robot', k: 'robot', r: [13.5, -9, 27, -2.2], anh: 'hoa_robot', ta: 'Khu dành cho robot và các thiết bị tự động hóa.' },
      { ten: 'Lõi thang', k: 'thang', r: [27, -2.2, 30, 6], anh: 'thang', ta: 'Cầu thang và thang máy nối hai tầng.' }
    ],
    2: [
      { ten: 'Phòng thí nghiệm Sinh', k: 'sinh', r: [0, 0, 20, 15], anh: 'sinh_lab', ta: 'Phòng chính của tầng 2, nơi làm các bài quan sát tế bào, enzyme, quang hợp và lên men.' },
      { ten: 'Phòng vi sinh', k: 'visinh', r: [20, 7.5, 27, 15], anh: 'sinh_visinh', ta: 'Tủ cấy, nồi hấp và các bài nuôi cấy, nhuộm, đếm vi khuẩn, chỉ dùng chủng an toàn cho dạy học.' },
      { ten: 'Kho sinh phẩm', k: 'khosp', r: [20, 0, 27, 7.5], anh: 'sinh_kho', ta: 'Nơi lưu mẫu vật, sinh phẩm và các tủ bảo quản.' },
      { ten: 'Hành lang tầng 2', k: '', r: [0, -3.6, 27, 0], anh: 'sinh_hanhlang', ta: 'Trục nối các phòng trên tầng 2.' },
      { ten: 'Phòng sinh học phân tử', k: 'phantu', r: [0, -9, 13.5, -3.6], anh: 'sinh_phantu', ta: 'Tách chiết ADN, PCR, điện di và trạm ghép gen.' },
      { ten: 'Nhà kính thực vật', k: 'kinh', r: [13.5, -9, 27, -3.6], anh: 'sinh_nhakinh', ta: 'Khu trồng cây, nơi làm các bài về quang hợp và thực vật.' },
      { ten: 'Hành lang đông', k: '', r: [27, 6, 30, 15], anh: 'sinh_hanhlang', ta: 'Lối nối lõi thang với phòng vi sinh và kho sinh phẩm.' },
      { ten: 'Lõi thang', k: 'thang', r: [27, -2.2, 30, 6], anh: 'thang', ta: 'Cầu thang và thang máy nối hai tầng.' }
    ]
  };

  // Chuyển tab theo địa chỉ, nút Quay lại của trình duyệt vẫn dùng được
  var cacTab = [].slice.call(document.querySelectorAll('.tab'));
  var duongMenu = [].slice.call(document.querySelectorAll('#menu a'));
  // Liên kết cũ của trang một cuộn vẫn dẫn đúng tab
  var DIA_CHI_CU = { dau: 'gioi-thieu', 'tinh-nang': 'san-pham', 'diem-moi': 'san-pham', 'quy-hoach': 'tham-quan', 'lab-hoa': 'tham-quan', 'lab-sinh': 'tham-quan', 'tay-cam': 'cong-nghe', 'hieu-suat': 'tai-ve', 'lo-trinh': 'kiem-chung' };

  function tabTuDiaChi() {
    var ma = location.hash.replace(/^#\/?/, '');
    if (document.getElementById(ma) && document.getElementById(ma).classList.contains('tab')) return ma;
    return DIA_CHI_CU[ma] || 'gioi-thieu';
  }

  function moTab(lanDau) {
    var ma = tabTuDiaChi();
    cacTab.forEach(function (t) { t.classList.toggle('dang-mo', t.id === ma); });
    duongMenu.forEach(function (a) {
      if (a.getAttribute('href') === '#/' + ma) {
        a.setAttribute('aria-current', 'page');
        if (a.scrollIntoView && a.parentNode.scrollWidth > a.parentNode.clientWidth) a.scrollIntoView({ inline: 'center', block: 'nearest' });
      } else a.removeAttribute('aria-current');
    });
    var tab = document.getElementById(ma);
    document.title = ma === 'gioi-thieu' ? 'VERA Lab · Dự án Khoa học Kỹ thuật' : tab.dataset.ten + ' · VERA Lab';
    if (!lanDau) {
      window.scrollTo(0, 0);
      var tieuDe = tab.querySelector('h1');
      if (tieuDe) tieuDe.focus({ preventScroll: true });
    }
  }
  window.addEventListener('hashchange', function () { moTab(false); });
  moTab(true);

  // Liên kết bỏ qua không đổi địa chỉ, tránh làm bộ chuyển tab hiểu nhầm
  document.getElementById('bo-qua').addEventListener('click', function (e) {
    e.preventDefault();
    document.getElementById('noi-dung').focus();
  });

  // Nhóm tab con: mũi tên để chuyển, Home và End tới đầu cuối
  function taoNhomTab(goc) {
    var nut = [].slice.call(goc.querySelectorAll('[role="tab"]'));
    function chon(i, datTieuDiem) {
      nut.forEach(function (n, j) {
        var chonDung = j === i;
        n.setAttribute('aria-selected', chonDung);
        n.tabIndex = chonDung ? 0 : -1;
        document.getElementById(n.getAttribute('aria-controls')).hidden = !chonDung;
      });
      if (datTieuDiem) nut[i].focus();
      if (nut[i].parentNode.scrollWidth > nut[i].parentNode.clientWidth) nut[i].scrollIntoView({ inline: 'center', block: 'nearest' });
      goc.dispatchEvent(new CustomEvent('nhom-tab:chon', { detail: i }));
    }
    nut.forEach(function (n, i) {
      n.addEventListener('click', function () { chon(i); });
      n.addEventListener('keydown', function (e) {
        var dich = { ArrowDown: i + 1, ArrowRight: i + 1, ArrowUp: i - 1, ArrowLeft: i - 1, Home: 0, End: nut.length - 1 }[e.key];
        if (dich === undefined) return;
        e.preventDefault();
        chon((dich + nut.length) % nut.length, true);
      });
    });
    goc.addEventListener('click', function (e) {
      var sang = e.target.closest('[data-sang]');
      if (!sang) return;
      chon(+sang.dataset.sang, true);
      var dau = nut[0].parentNode.getBoundingClientRect();
      // Trên điện thoại danh sách nằm trên nội dung, cuộn lại để thấy đầu bước mới
      if (dau.top < 60 || dau.top > innerHeight) window.scrollBy(0, dau.top - 90);
    });
    return { chon: chon, nut: nut };
  }
  [].forEach.call(document.querySelectorAll('[data-nhom-tab]'), taoNhomTab);

  // Nút chọn trên sân khấu tối: bấm là đổi trạng thái, báo cho cảnh 3D
  function nhomChon(id, ten, sauKhiChon) {
    var goc = document.getElementById(id), nut = [].slice.call(goc.querySelectorAll('button'));
    function dat(giaTri) {
      nut.forEach(function (n) { n.setAttribute('aria-pressed', n.dataset[ten] === String(giaTri)); });
      sauKhiChon(giaTri);
    }
    goc.addEventListener('click', function (e) {
      var n = e.target.closest('button');
      if (n) dat(n.dataset[ten]);
    });
    return dat;
  }

  // Chú thích nguồn: mở danh mục, tô sáng mục được dẫn và cho quay về chỗ cũ
  var danhMuc = document.getElementById('danh-muc-nguon');
  document.addEventListener('click', function (e) {
    var cc = e.target.closest ? e.target.closest('.cc') : null;
    if (!cc) return;
    e.preventDefault();
    var muc = document.getElementById(cc.getAttribute('href').slice(1));
    danhMuc.open = true;
    document.querySelectorAll('.ds-nguon .sang').forEach(function (n) { n.classList.remove('sang'); });
    document.querySelectorAll('.quay-lai').forEach(function (n) { n.remove(); });
    muc.classList.add('sang');
    var ve = document.createElement('button');
    ve.type = 'button'; ve.className = 'quay-lai'; ve.textContent = '↑ Quay lại';
    ve.addEventListener('click', function () {
      muc.classList.remove('sang'); ve.remove();
      cc.scrollIntoView({ block: 'center' });
      cc.focus({ preventScroll: true });
    });
    muc.firstElementChild.appendChild(ve);
    muc.scrollIntoView({ block: 'center' });
    ve.focus({ preventScroll: true });
  });

  // Sân khấu ở trang đầu: tòa nhà 3D hoặc ảnh chụp trong ứng dụng
  var sanKhau = document.getElementById('san-khau'), chuCanh = document.getElementById('chu-canh');
  var CHU_CANH = {
    toa: 'Mô hình 3D dựng từ mặt bằng của ứng dụng. Kéo để xoay.',
    tach: 'Tầng 1 là khu Hóa học, tầng 2 là khu Sinh học. Kéo để xoay.',
    hoa: 'Phòng thí nghiệm Hóa, ảnh chụp trực tiếp trong ứng dụng.',
    sinh: 'Phòng thí nghiệm Sinh, ảnh chụp trực tiếp trong ứng dụng.'
  };
  var datCanh = nhomChon('chon-canh', 'che', function (che) {
    sanKhau.dataset.che = che;
    chuCanh.textContent = CHU_CANH[che];
    window.VERA_CANH = { tach: che === 'tach', anh: che === 'hoa' || che === 'sinh' };
    window.dispatchEvent(new CustomEvent('vera:canh', { detail: window.VERA_CANH }));
  });
  // Không dùng được 3D thì mở sẵn ảnh chụp
  window.addEventListener('vera:khong-3d', function () { datCanh('hoa'); });
  if (document.documentElement.classList.contains('khong-3d')) datCanh('hoa');

  // Tay cầm: mỗi nút đưa mô hình tới một trạng thái và hiện phần chữ tương ứng
  var buocTayCam = [].slice.call(document.querySelectorAll('.buoc-tc'));
  nhomChon('chon-tay-cam', 'buoc', function (i) {
    buocTayCam.forEach(function (n, j) { n.hidden = j !== +i; });
    window.VERA_BUOC_TC = +i;
    window.dispatchEvent(new CustomEvent('vera:tay-cam', { detail: +i }));
  });

  // Nút tải: chưa có tệp phát hành
  var tb = document.getElementById('thong-bao'), henTb = null;
  [].forEach.call(document.querySelectorAll('.nut-tai'), function (b) {
    b.addEventListener('click', function () {
      tb.textContent = 'Bản tải sắp ra mắt';
      tb.classList.add('hien');
      clearTimeout(henTb);
      henTb = setTimeout(function () { tb.classList.remove('hien'); }, 2200);
    });
  });

  // Thư viện ảnh của từng tầng: hiện 8 ảnh đầu, bấm để xem hết
  var A = 'anh/', SO_ANH_DAU = 8;
  function thuVien(id, ds) {
    var goc = document.getElementById(id);
    ds.forEach(function (a, i) {
      var n = document.createElement('button');
      n.type = 'button';
      n.className = 'anh-the';
      n.dataset.anh = a[0];
      n.dataset.chu = a[1];
      n.setAttribute('aria-label', 'Xem lớn: ' + a[1]);
      n.hidden = i >= SO_ANH_DAU;
      var im = document.createElement('img');
      im.src = A + a[0] + '.jpg'; im.alt = a[1]; im.loading = 'lazy';
      var s = document.createElement('span'); s.textContent = a[1];
      n.appendChild(im); n.appendChild(s);
      goc.appendChild(n);
    });
    if (ds.length <= SO_ANH_DAU) return;
    var them = document.createElement('button');
    them.type = 'button'; them.className = 'nut phu nho xem-them';
    them.textContent = 'Xem tất cả ' + ds.length + ' ảnh';
    them.addEventListener('click', function () {
      [].forEach.call(goc.querySelectorAll('.anh-the'), function (n) { n.hidden = false; });
      them.remove();
    });
    goc.parentNode.insertBefore(them, goc.nextSibling);
  }
  thuVien('thu-vien-hoa', [
    ['hoa_lab', 'Phòng thí nghiệm Hóa'], ['hoa_giang', 'Bàn giáo viên và màn hình'], ['hoa_kho', 'Phòng hóa chất'], ['hoa_may', 'Phòng máy phân tích'],
    ['hoa_robot', 'Phòng tự động hóa và robot'], ['hoa_tonghop', 'Khu đun nóng và tổng hợp'], ['hoa_chetao', 'Khu chế tạo dụng cụ'], ['ngoai', 'Mặt ngoài tòa nhà'],
    ['hoa_nhakhoahoc', 'Tường các nhà hóa học']
  ]);
  thuVien('thu-vien-sinh', [
    ['sinh_lab', 'Phòng thí nghiệm Sinh'], ['sinh_hienvi', 'Bàn kính hiển vi'], ['sinh_visinh', 'Phòng vi sinh'], ['sinh_phantu', 'Phòng sinh học phân tử'],
    ['sinh_tachchiet', 'Đảo tách chiết ADN'], ['sinh_dienhi', 'Khu PCR và điện di'], ['sinh_ghepgen', 'Trạm ghép gen'], ['sinh_nhakinh', 'Nhà kính thực vật'],
    ['sinh_ban', 'Khu bàn học sinh'], ['sinh_pcr', 'Tủ thao tác PCR'], ['sinh_quanghop', 'Trạm quang hợp'], ['sinh_kho', 'Kho sinh phẩm'],
    ['sinh_nhuom', 'Đảo nhuộm Gram'], ['sinh_thinghiem', 'Quầy rửa dụng cụ'], ['sinh_hanhlang', 'Hành lang tầng 2'], ['thang', 'Sảnh thang máy']
  ]);
  var xem = document.getElementById('xem');
  document.addEventListener('click', function (e) {
    var n = e.target.closest ? e.target.closest('.anh-the') : null;
    if (n) {
      document.getElementById('xem-anh').src = A + n.dataset.anh + '.jpg';
      document.getElementById('xem-anh').alt = n.dataset.chu;
      document.getElementById('xem-chu').textContent = n.dataset.chu;
      xem.showModal();
    } else if (e.target === xem) xem.close();
  });
  document.getElementById('dong-xem').addEventListener('click', function () { xem.close(); });

  // Chọn tầng và phòng, dùng chung với mặt bằng 3D
  var dsPhong = document.getElementById('ds-phong');
  function chonPhong(t, i) {
    var p = window.VERA_TANG[t][i];
    window.VERA_PHONG = { t: t, i: i };
    document.getElementById('phong-tang').textContent = t === 1 ? 'Tầng 1 · Hóa học' : 'Tầng 2 · Sinh học';
    document.getElementById('phong-ten').textContent = p.ten;
    document.getElementById('phong-mo-ta').textContent = p.ta;
    var anh = document.getElementById('phong-anh');
    anh.src = A + p.anh + '.jpg'; anh.alt = p.ten;
    dsPhong.innerHTML = '';
    window.VERA_TANG[t].forEach(function (q, j) {
      var b = document.createElement('button');
      b.type = 'button'; b.textContent = q.ten; b.setAttribute('aria-pressed', j === i);
      b.addEventListener('click', function () { chonPhong(t, j); });
      dsPhong.appendChild(b);
    });
    window.dispatchEvent(new CustomEvent('vera:da-chon', { detail: { t: t, i: i } }));
  }
  var nhomTang = document.getElementById('nhom-tang');
  nhomTang.addEventListener('nhom-tab:chon', function (e) { chonPhong(e.detail + 1, 0); });
  window.addEventListener('vera:chon', function (e) {
    if (e.detail.t !== window.VERA_PHONG.t) return;
    chonPhong(e.detail.t, e.detail.i);
  });
  chonPhong(1, 0);
})();
