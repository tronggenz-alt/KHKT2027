/**
 * giao-dien.js - Điều khiển giao diện tương tác Tốc độ phản ứng & Năng lượng hoạt hoá
 * Tích hợp HL_TocDo, HLVeTocDo, HLCongThuc, HLCauHoi, HLDinhDang
 */
(function (global) {
  'use strict';

  var app = {
    state: {
      pu: 'A_B_C_D',
      puData: null,
      T: 298.15,
      Ea: 50.0,
      dH: -20.0,
      A: 1.0e10,
      k: 17.393,
      v: 0.174,
      cA: 1.0,
      cB: 1.0,
      cC: 0.0,
      cD: 0.0,
      xucTac: false,
      tabDoThi: 'nangLuong',
      tabPhai: 'thong-so',
      bangThiNghiem: []
    },
    ve: null,
    danhSachPU: []
  };

  function tinhToan() {
    var Ea_hieu_dung = app.state.xucTac ? (app.state.Ea - 20.0) : app.state.Ea;
    app.state.k = HL_TocDo.kTheoArrhenius(app.state.A, Ea_hieu_dung, app.state.T);
    app.state.v = app.state.k * app.state.cA * app.state.cB;
  }

  function capNhatGiaoDien() {
    tinhToan();

    // Cập nhật thẻ trạng thái
    var elT = document.getElementById('hl-tt-t');
    var elEa = document.getElementById('hl-tt-ea');
    var elK = document.getElementById('hl-tt-k');
    var elV = document.getElementById('hl-tt-v');
    var elXT = document.getElementById('hl-tt-xt');
    var elTiLe = document.getElementById('hl-tt-ti-le');

    if (elT) elT.textContent = HLDinhDang.so(app.state.T, 1) + ' K (' + HLDinhDang.so(app.state.T - 273.15, 1) + ' °C)';
    if (elEa) elEa.textContent = HLDinhDang.so(app.state.Ea, 1) + ' kJ/mol';
    if (elK) elK.textContent = HLDinhDang.so(app.state.k, 3);
    if (elV) elV.textContent = HLDinhDang.so(app.state.v, 3) + ' M/s';
    if (elXT) {
      elXT.textContent = app.state.xucTac ? 'Đang bật (giảm 20 kJ)' : 'Không có';
      elXT.className = 'font-mono text-xs font-bold ' + (app.state.xucTac ? 'text-amber-500' : 'text-lang');
    }
    if (elTiLe) {
      var eff = Math.exp(-app.state.Ea / (0.008314 * app.state.T));
      elTiLe.textContent = HLDinhDang.khoaHoc(eff, 3);
    }

    // Cập nhật sliders
    var slEa = document.getElementById('hl-sl-ea');
    var slT = document.getElementById('hl-sl-t');
    if (slEa && document.activeElement !== slEa) slEa.value = app.state.Ea;
    if (slT && document.activeElement !== slT) slT.value = app.state.T;

    if (app.ve) {
      app.ve.capNhat(app.state);
    }
  }

  function khoiTao() {
    var khungSim = document.getElementById('hl-khung-mo-phong');
    if (!khungSim) return;

    app.ve = HLVeTocDo.tao(khungSim, {
      onThayDoi: function (st) {
        app.state.Ea = st.Ea;
        app.state.T = st.T;
        app.state.xucTac = st.xucTac;
        capNhatGiaoDien();
      }
    });

    // Tải danh sách phản ứng
    fetch('/static/hoa-hoc/hoa-ly/toc-do-phan-ung/phan-ung.json')
      .then(function (r) { return r.json(); })
      .then(function (data) {
        app.danhSachPU = data.danh_sach || [];
        var selPU = document.getElementById('hl-sel-pu');
        if (selPU) {
          selPU.innerHTML = '';
          app.danhSachPU.forEach(function (pu) {
            var opt = document.createElement('option');
            opt.value = pu.id;
            opt.textContent = pu.ten;
            if (pu.id === app.state.pu) opt.selected = true;
            selPU.appendChild(opt);
          });
          selPU.addEventListener('change', function () {
            app.state.pu = selPU.value;
            capNhatGiaoDien();
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

    // Nút bật/tắt xúc tác
    var btnXT = document.getElementById('hl-btn-xuc-tac');
    if (btnXT) {
      btnXT.addEventListener('click', function () {
        app.state.xucTac = !app.state.xucTac;
        btnXT.textContent = app.state.xucTac ? 'Tắt xúc tác' : 'Bật xúc tác (giảm 20 kJ)';
        capNhatGiaoDien();
      });
    }

    // Nút Đặt lại
    var btnDatLai = document.getElementById('hl-btn-dat-lai');
    if (btnDatLai) {
      btnDatLai.addEventListener('click', function () {
        app.state.Ea = 50.0;
        app.state.T = 298.15;
        app.state.A = 1.0e10;
        app.state.xucTac = false;
        app.state.cA = 1.0;
        app.state.cB = 1.0;
        capNhatGiaoDien();
      });
    }

    // Sliders
    var slEa = document.getElementById('hl-sl-ea');
    if (slEa) {
      slEa.addEventListener('input', function () {
        app.state.Ea = parseFloat(slEa.value);
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

    // Tab Thí nghiệm: Nút Chạy thí nghiệm đo k ở nhiệt độ hiện tại
    var btnDoK = document.getElementById('hl-btn-do-k');
    if (btnDoK) {
      btnDoK.addEventListener('click', function () {
        var k_do = HL_TocDo.kTheoArrhenius(app.state.A, app.state.Ea, app.state.T);
        app.state.bangThiNghiem.push({
          T: app.state.T,
          k: k_do
        });

        var tbody = document.getElementById('hl-bang-thi-nghiem-k');
        if (tbody) {
          var tr = document.createElement('tr');
          tr.innerHTML = '<td class="px-3 py-1">' + HLDinhDang.so(app.state.T, 1) + ' K</td><td class="px-3 py-1">' + HLDinhDang.so(k_do, 3) + '</td><td class="px-3 py-1 font-mono">' + Math.log(k_do).toFixed(3) + '</td>';
          tbody.appendChild(tr);
        }

        if (app.state.bangThiNghiem.length >= 3) {
          var resKhop = HL_TocDo.khopArrhenius(app.state.bangThiNghiem);
          var elKq = document.getElementById('hl-kq-khop-arrhenius');
          if (elKq && resKhop) {
            var gamma = HL_TocDo.heSoVantHoff(resKhop.Ea_kJ, 298.15);
            elKq.innerHTML = '<div>Ea khớp: <strong>' + HLDinhDang.so(resKhop.Ea_kJ, 2) + ' kJ/mol</strong> (Ea đặt: ' + HLDinhDang.so(app.state.Ea, 1) + ' kJ)</div>' +
              '<div>Hệ số Van \'t Hoff γ: <strong>' + HLDinhDang.so(gamma, 2) + '</strong></div>';
          }
        }
      });
    }

    // Tải tab Công thức (D1–D7)
    var containerCongThuc = document.getElementById('hl-ds-cong-thuc-toc-do');
    if (containerCongThuc && typeof HLCongThuc !== 'undefined') {
      fetch('/static/hoa-hoc/hoa-ly/cong-thuc.json')
        .then(function (r) { return r.json(); })
        .then(function (data) {
          var ds = (data.cong_thuc || []).filter(function (c) {
            return (c.mo_phong || []).some(function (m) { return m.id === 'toc-do-phan-ung'; }) || c.chu_de === 'dong-hoc';
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

    // Tải tab Học & Thi
    var containerCauHoi = document.getElementById('hl-nd-hoc-thi');
    if (containerCauHoi && typeof HLCauHoi !== 'undefined') {
      fetch('/static/hoa-hoc/hoa-ly/toc-do-phan-ung/cau-hoi.json')
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

  global.HoaLy = {
    trangThai: function () {
      var stVe = app.ve ? app.ve.layTrangThai() : null;
      return {
        pu: app.state.pu,
        T_K: app.state.T,
        Ea_kJ: app.state.Ea,
        dH_kJ: app.state.dH,
        xuc_tac: app.state.xucTac,
        C: {
          A: app.state.cA,
          B: app.state.cB,
          C: app.state.cC,
          D: app.state.cD
        },
        k: app.state.k,
        v: app.state.v,
        vaCham: stVe ? stVe.tongVaCham : 0,
        vaChamHieuQua: stVe ? stVe.vaChamHieuQua : 0,
        tiLeDo: stVe ? stVe.tiLeDo : 0,
        tiLeLyThuyet: stVe ? stVe.tiLeLyThuyet : 0
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
      if (params.Ea !== undefined) app.state.Ea = params.Ea;
      if (params.T !== undefined) app.state.T = params.T;
      if (params.xuc_tac !== undefined) app.state.xucTac = params.xuc_tac;
      if (params.cA !== undefined) app.state.cA = params.cA;
      if (params.cB !== undefined) app.state.cB = params.cB;
      capNhatGiaoDien();
    },
    chayThiNghiem: function (T_K) {
      if (T_K !== undefined) app.state.T = T_K;
      var btn = document.getElementById('hl-btn-do-k');
      if (btn) btn.click();
    },
    datLai: function () {
      var btn = document.getElementById('hl-btn-dat-lai');
      if (btn) btn.click();
    }
  };

  document.addEventListener('DOMContentLoaded', khoiTao);
})(typeof window !== 'undefined' ? window : global);
