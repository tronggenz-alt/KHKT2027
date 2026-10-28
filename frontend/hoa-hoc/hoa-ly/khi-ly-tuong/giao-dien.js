/**
 * giao-dien.js - Điều khiển giao diện tương tác Khí lý tưởng
 * Tích hợp HL_KhiLyTuong, HLVeKhiLyTuong, HLCongThuc, HLCauHoi, HLDinhDang
 */
(function (global) {
  'use strict';

  var app = {
    state: {
      n: 1,
      V: 24.79,
      T: 298.15,
      p: 1.0,
      p_do_bar: 1.0,
      giu: 'T',
      khi: 'N2',
      khiData: null,
      tabDoThi: 'pV',
      tabPhai: 'thong-so',
      theoDoi1PhanTu: false,
      vanDerWaals: false,
      honHop: false,
      khi2: 'He',
      n2: 0.5,
      diemDoKhong: []
    },
    ve: null,
    danhSachKhi: []
  };

  function tinhToan() {
    var M = 28.013;
    if (app.state.khiData) {
      for (var i = 0; i < app.state.khiData.length; i++) {
        if (app.state.khiData[i].id === app.state.khi) {
          M = app.state.khiData[i].M;
          break;
        }
      }
    }

    if (app.state.giu === 'T') {
      app.state.p = HL_KhiLyTuong.apSuat({ n: app.state.n, T: app.state.T, V_L: app.state.V });
    } else if (app.state.giu === 'p') {
      app.state.V = HL_KhiLyTuong.theTich({ n: app.state.n, T: app.state.T, p_bar: app.state.p });
    } else if (app.state.giu === 'V') {
      app.state.p = HL_KhiLyTuong.apSuat({ n: app.state.n, T: app.state.T, V_L: app.state.V });
    } else {
      app.state.p = HL_KhiLyTuong.apSuat({ n: app.state.n, T: app.state.T, V_L: app.state.V });
    }

    app.state.U = HL_KhiLyTuong.noiNang(app.state.n, app.state.T, 1.4);
    app.state.v_rms = HL_KhiLyTuong.vRms(M, app.state.T);
    app.state.W_d = HL_KhiLyTuong.dongNangTb(app.state.T);
  }

  function capNhatGiaoDien() {
    tinhToan();

    // Cập nhật thẻ trạng thái trên cùng
    var elN = document.getElementById('hl-tt-n');
    var elP = document.getElementById('hl-tt-p');
    var elV = document.getElementById('hl-tt-v');
    var elT = document.getElementById('hl-tt-t');
    var elU = document.getElementById('hl-tt-u');
    var elVrms = document.getElementById('hl-tt-vrms');
    var elWd = document.getElementById('hl-tt-wd');

    if (elN) elN.textContent = HLDinhDang.so(app.state.n, 2) + ' mol';
    if (elP) elP.textContent = HLDinhDang.so(app.state.p, 3) + ' bar (' + HLDinhDang.so(app.state.p * 100, 1) + ' kPa)';
    if (elV) elV.textContent = HLDinhDang.so(app.state.V, 2) + ' L';
    if (elT) elT.textContent = HLDinhDang.so(app.state.T, 1) + ' K (' + HLDinhDang.so(app.state.T - 273.15, 1) + ' °C)';
    if (elU) elU.textContent = HLDinhDang.so(app.state.U, 0) + ' J';
    if (elVrms) elVrms.textContent = HLDinhDang.so(app.state.v_rms, 1) + ' m/s';
    if (elWd) elWd.textContent = HLDinhDang.khoaHoc(app.state.W_d, 2) + ' J';

    // Cập nhật sliders
    var slV = document.getElementById('hl-sl-v');
    var slT = document.getElementById('hl-sl-t');
    var slN = document.getElementById('hl-sl-n');
    if (slV && document.activeElement !== slV) slV.value = app.state.V;
    if (slT && document.activeElement !== slT) slT.value = app.state.T;
    if (slN && document.activeElement !== slN) slN.value = app.state.n;

    // Cập nhật bộ vẽ
    if (app.ve) {
      app.ve.capNhat(app.state);
    }
  }

  function khoiTao() {
    var khungSim = document.getElementById('hl-khung-mo-phong');
    if (!khungSim) return;

    app.ve = HLVeKhiLyTuong.tao(khungSim, {
      onThayDoi: function (st) {
        app.state.V = st.V;
        app.state.p = st.p;
        app.state.T = st.T;
        capNhatGiaoDien();
      }
    });

    // Tải danh sách khí
    fetch('/static/hoa-hoc/hoa-ly/khi-ly-tuong/khi.json')
      .then(function (res) { return res.json(); })
      .then(function (data) {
        app.state.khiData = data.danh_sach || [];
        var selKhi = document.getElementById('hl-sel-khi');
        if (selKhi) {
          selKhi.innerHTML = '';
          app.state.khiData.forEach(function (k) {
            var opt = document.createElement('option');
            opt.value = k.id;
            opt.textContent = k.ten + ' (' + k.ct + ')';
            if (k.id === app.state.khi) opt.selected = true;
            selKhi.appendChild(opt);
          });
        }
      })
      .catch(function () {});

    // Gắn sự kiện tab đồ thị
    var btnTabs = document.querySelectorAll('.hl-tab-do-thi');
    btnTabs.forEach(function (btn) {
      btn.addEventListener('click', function () {
        btnTabs.forEach(function (b) { b.classList.remove('active', 'border-sky-500', 'text-sky-500'); });
        btn.classList.add('active', 'border-sky-500', 'text-sky-500');
        app.state.tabDoThi = btn.getAttribute('data-tab');
        capNhatGiaoDien();
      });
    });

    // Gắn sự kiện tab phải
    var btnTabsPhai = document.querySelectorAll('.hl-tab-phai');
    btnTabsPhai.forEach(function (btn) {
      btn.addEventListener('click', function () {
        btnTabsPhai.forEach(function (b) { b.classList.remove('active', 'border-sky-500', 'text-sky-500'); });
        btn.classList.add('active', 'border-sky-500', 'text-sky-500');
        var tabId = btn.getAttribute('data-tab');
        app.state.tabPhai = tabId;
        document.querySelectorAll('.hl-noi-dung-tab-phai').forEach(function (nd) {
          nd.classList.add('hidden');
        });
        var targetNd = document.getElementById('hl-nd-' + tabId);
        if (targetNd) targetNd.classList.remove('hidden');
      });
    });

    // Gắn sự kiện bộ chọn "Giữ cố định"
    var radGiu = document.querySelectorAll('input[name="hl-giu"]');
    radGiu.forEach(function (rad) {
      rad.addEventListener('change', function () {
        app.state.giu = rad.value;
        capNhatGiaoDien();
      });
    });

    // Gắn sự kiện sliders
    var slV = document.getElementById('hl-sl-v');
    if (slV) {
      slV.addEventListener('input', function () {
        app.state.V = parseFloat(slV.value);
        capNhatGiaoDien();
      });
    }

    var slT = document.getElementById('hl-sl-t');
    if (slT) {
      slT.addEventListener('input', function () {
        app.state.T = parseFloat(slT.value);
        capNhatGiaoDien();
      });
    }

    var slN = document.getElementById('hl-sl-n');
    if (slN) {
      slN.addEventListener('input', function () {
        app.state.n = parseFloat(slN.value);
        if (app.ve) app.ve.datLaiHat();
        capNhatGiaoDien();
      });
    }

    // Nút Bơm +0,05 mol
    var btnBom = document.getElementById('hl-btn-bom');
    if (btnBom) {
      btnBom.addEventListener('click', function () {
        app.state.n = Math.min(5, app.state.n + 0.05);
        if (app.ve) app.ve.datLaiHat();
        capNhatGiaoDien();
      });
    }

    // Nút Xả khí
    var btnXa = document.getElementById('hl-btn-xa');
    if (btnXa) {
      btnXa.addEventListener('click', function () {
        app.state.n = Math.max(0.05, app.state.n - 0.05);
        if (app.ve) app.ve.datLaiHat();
        capNhatGiaoDien();
      });
    }

    // Nút Đặt lại
    var btnDatLai = document.getElementById('hl-btn-dat-lai');
    if (btnDatLai) {
      btnDatLai.addEventListener('click', function () {
        app.state.n = 1;
        app.state.V = 24.79;
        app.state.T = 298.15;
        app.state.p = 1.0;
        app.state.giu = 'T';
        app.state.khi = 'N2';
        app.state.vanDerWaals = false;
        app.state.theoDoi1PhanTu = false;
        if (app.ve) app.ve.datLaiHat();
        capNhatGiaoDien();
      });
    }

    // Nút "Ghi điểm" tìm độ không tuyệt đối
    var btnGhiDiem = document.getElementById('hl-btn-ghi-diem');
    if (btnGhiDiem) {
      btnGhiDiem.addEventListener('click', function () {
        var t_C = app.state.T - 273.15;
        app.state.diemDoKhong.push({ t_C: t_C, V_L: app.state.V });
        var bangDiem = document.getElementById('hl-bang-diem-do-khong');
        if (bangDiem) {
          var tr = document.createElement('tr');
          tr.innerHTML = '<td class="px-3 py-1">' + HLDinhDang.so(t_C, 1) + ' °C</td><td class="px-3 py-1">' + HLDinhDang.so(app.state.V, 2) + ' L</td>';
          bangDiem.appendChild(tr);
        }
        if (app.state.diemDoKhong.length >= 2) {
          var kq = HL_KhiLyTuong.doKhongTuyetDoi(app.state.diemDoKhong);
          var elKq = document.getElementById('hl-kq-do-khong');
          if (elKq && kq) {
            elKq.textContent = 'Ngoại suy T₀: ' + HLDinhDang.so(kq.do_khong_tuyet_doi, 2) + ' °C';
          }
        }
      });
    }

    // Tải và hiển thị Tab Công thức
    var containerCongThuc = document.getElementById('hl-ds-cong-thuc-khi');
    if (containerCongThuc && typeof HLCongThuc !== 'undefined') {
      fetch('/static/hoa-hoc/hoa-ly/cong-thuc.json')
        .then(function (r) { return r.json(); })
        .then(function (data) {
          var ds = (data.cong_thuc || []).filter(function (c) {
            return (c.mo_phong || []).some(function (m) { return m.id === 'khi-ly-tuong'; }) || c.chu_de === 'khi';
          });
          containerCongThuc.innerHTML = '';
          ds.forEach(function (ct) {
            containerCongThuc.appendChild(HLCongThuc.taoThe(ct));
          });
          if (typeof MathJax !== 'undefined' && MathJax.typesetPromise) {
            MathJax.typesetPromise([containerCongThuc]).catch(function () {});
          }
        })
        .catch(function () {});
    }

    // Tải và hiển thị Tab Học & Thi
    var containerCauHoi = document.getElementById('hl-nd-hoc-thi');
    if (containerCauHoi && typeof HLCauHoi !== 'undefined') {
      fetch('/static/hoa-hoc/hoa-ly/khi-ly-tuong/cau-hoi.json')
        .then(function (r) { return r.json(); })
        .then(function (data) {
          containerCauHoi.innerHTML = '';
          var elCH = HLCauHoi.tao(data.danh_sach || []);
          containerCauHoi.appendChild(elCH);
        })
        .catch(function () {});
    }

    capNhatGiaoDien();
  }

  // Xuất API kiểm thử tự động
  global.HoaLy = {
    trangThai: function () {
      var stVe = app.ve ? app.ve.layTrangThai() : null;
      return {
        n: app.state.n,
        V_L: app.state.V,
        T_K: app.state.T,
        p_bar: app.state.p,
        giu: app.state.giu,
        khi: app.state.khi,
        N_hat: (app.ve && app.ve.layHat().length > 0) ? app.ve.layHat().length : Math.round(app.state.n * 120),
        p_do_bar: (stVe && stVe.p_do_bar !== undefined) ? stVe.p_do_bar : app.state.p,
        U_J: app.state.U
      };
    },
    boCuc: function () {
      return app.ve ? app.ve.layBoCuc() : {};
    },
    hat: function () {
      return app.ve ? app.ve.layHat() : [];
    },
    datGiaTri: function (params) {
      if (!params) return;
      if (params.V !== undefined) app.state.V = params.V;
      if (params.T !== undefined) app.state.T = params.T;
      if (params.p !== undefined) app.state.p = params.p;
      if (params.n !== undefined) app.state.n = params.n;
      if (params.giu !== undefined) app.state.giu = params.giu;
      if (params.khi !== undefined) app.state.khi = params.khi;
      capNhatGiaoDien();
    },
    datLai: function () {
      var btn = document.getElementById('hl-btn-dat-lai');
      if (btn) btn.click();
    }
  };

  document.addEventListener('DOMContentLoaded', khoiTao);
})(typeof window !== 'undefined' ? window : global);
