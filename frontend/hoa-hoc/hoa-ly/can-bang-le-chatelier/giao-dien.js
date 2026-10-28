/**
 * giao-dien.js - Điều khiển giao diện tương tác Cân bằng hoá học & Le Chatelier
 * Tích hợp HL_CanBang, HLVeCanBang, HLCongThuc, HLCauHoi, HLDinhDang
 */
(function (global) {
  'use strict';

  var app = {
    state: {
      he: 'N2O4_2NO2',
      heData: null,
      T: 298.15,
      V: 1.0,
      cA0: 0.0400,
      cA: 0.033762,
      cB: 0.012476,
      Q: 0.00461,
      Kc: 0.00461,
      chieu: 'cân bằng',
      alpha: 0.1559,
      xucTac: false,
      tabDoThi: 'Ct',
      tabPhai: 'thong-so',
      giaiThich: []
    },
    ve: null,
    danhSachHe: []
  };

  function tinhToan() {
    app.state.Kc = HL_CanBang.kcTheoT(app.state.T, 57.2, 298.15, 0.00461);
    app.state.chieu = HL_CanBang.chieu(app.state.Q, app.state.Kc);
    app.state.alpha = (app.state.cA0 - app.state.cA) / app.state.cA0;
  }

  function capNhatGiaoDien() {
    tinhToan();

    // Cập nhật thẻ trạng thái
    var elT = document.getElementById('hl-tt-t');
    var elV = document.getElementById('hl-tt-v');
    var elKc = document.getElementById('hl-tt-kc');
    var elQ = document.getElementById('hl-tt-q');
    var elChieu = document.getElementById('hl-tt-chieu');
    var elAlpha = document.getElementById('hl-tt-alpha');
    var elCA = document.getElementById('hl-tt-ca');
    var elCB = document.getElementById('hl-tt-cb');

    if (elT) elT.textContent = HLDinhDang.so(app.state.T, 1) + ' K (' + HLDinhDang.so(app.state.T - 273.15, 1) + ' °C)';
    if (elV) elV.textContent = HLDinhDang.so(app.state.V, 2) + ' L';
    if (elKc) elKc.textContent = HLDinhDang.khoaHoc(app.state.Kc, 3);
    if (elQ) elQ.textContent = HLDinhDang.khoaHoc(app.state.Q, 3);
    if (elChieu) {
      elChieu.textContent = app.state.chieu === 'cân bằng' ? '⇌ Cân bằng' : (app.state.chieu === 'thuận' ? '→ Chiều thuận' : '← Chiều nghịch');
      elChieu.className = 'font-mono text-xs font-bold ' + (app.state.chieu === 'cân bằng' ? 'text-sky-500' : (app.state.chieu === 'thuận' ? 'text-amber-500' : 'text-rose-500'));
    }
    if (elAlpha) elAlpha.textContent = HLDinhDang.so(app.state.alpha * 100, 1) + ' %';
    if (elCA) elCA.textContent = HLDinhDang.so(app.state.cA, 4) + ' M';
    if (elCB) elCB.textContent = HLDinhDang.so(app.state.cB, 4) + ' M';

    // Cập nhật thẻ giải thích Le Chatelier
    var elGt = document.getElementById('hl-giai-thich-le-chatelier');
    if (elGt) {
      var gt = HL_CanBang.giaiThichLeChatelier({
        Q: app.state.Q,
        K: app.state.Kc,
        he: app.state.he,
        tacDong: app.state.tacDong || 'thay đổi điều kiện'
      });
      elGt.innerHTML = gt.cau.map(function (c) { return '<p class="text-[11px] leading-relaxed mb-1">• ' + c + '</p>'; }).join('');
    }

    // Cập nhật sliders
    var slV = document.getElementById('hl-sl-v');
    var slT = document.getElementById('hl-sl-t');
    if (slV && document.activeElement !== slV) slV.value = app.state.V;
    if (slT && document.activeElement !== slT) slT.value = app.state.T;

    if (app.ve) {
      app.ve.capNhat(app.state);
    }
  }

  function khoiTao() {
    var khungSim = document.getElementById('hl-khung-mo-phong');
    if (!khungSim) return;

    app.ve = HLVeCanBang.tao(khungSim, {
      onThayDoi: function (st) {
        app.state.V = st.V;
        app.state.T = st.T;
        app.state.cA = st.cA;
        app.state.cB = st.cB;
        app.state.Q = st.Q;
        app.state.xucTac = st.xucTac;
        capNhatGiaoDien();
      }
    });

    // Tải danh sách hệ cân bằng
    fetch('/static/hoa-hoc/hoa-ly/can-bang-le-chatelier/he-can-bang.json')
      .then(function (r) { return r.json(); })
      .then(function (data) {
        app.danhSachHe = data.danh_sach || [];
        var selHe = document.getElementById('hl-sel-he');
        if (selHe) {
          selHe.innerHTML = '';
          app.danhSachHe.forEach(function (h) {
            var opt = document.createElement('option');
            opt.value = h.id;
            opt.textContent = h.ten;
            if (h.id === app.state.he) opt.selected = true;
            selHe.appendChild(opt);
          });
          selHe.addEventListener('change', function () {
            app.state.he = selHe.value;
            if (app.state.he === 'H2_I2_2HI') {
              app.state.Kc = 50.0;
              app.state.Q = 50.0;
            }
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

    // Nút nén 1/2 thể tích
    var btnNen = document.getElementById('hl-btn-nen-nua');
    if (btnNen) {
      btnNen.addEventListener('click', function () {
        app.state.tacDong = 'nén thể tích còn 1/2';
        app.state.V = Math.max(0.2, app.state.V * 0.5);
        app.state.cA *= 2.0;
        app.state.cB *= 2.0;
        app.state.Q = Math.pow(app.state.cB, 2) / app.state.cA;
        capNhatGiaoDien();
      });
    }

    // Nút giãn gấp đôi thể tích
    var btnGian = document.getElementById('hl-btn-gian-doi');
    if (btnGian) {
      btnGian.addEventListener('click', function () {
        app.state.tacDong = 'giãn thể tích gấp 2 lần';
        app.state.V = Math.min(2.0, app.state.V * 2.0);
        app.state.cA *= 0.5;
        app.state.cB *= 0.5;
        app.state.Q = Math.pow(app.state.cB, 2) / app.state.cA;
        capNhatGiaoDien();
      });
    }

    // Nút ngâm nước đá (0 °C)
    var btnDa = document.getElementById('hl-btn-nuoc-da');
    if (btnDa) {
      btnDa.addEventListener('click', function () {
        app.state.tacDong = 'làm lạnh bằng nước đá (0 °C)';
        app.state.T = 273.15;
        app.state.Kc = HL_CanBang.kcTheoT(app.state.T, 57.2, 298.15, 0.00461);
        capNhatGiaoDien();
      });
    }

    // Nút ngâm nước sôi (100 °C)
    var btnSoi = document.getElementById('hl-btn-nuoc-soi');
    if (btnSoi) {
      btnSoi.addEventListener('click', function () {
        app.state.tacDong = 'nung nóng bằng nước sôi (100 °C)';
        app.state.T = 373.15;
        app.state.Kc = HL_CanBang.kcTheoT(app.state.T, 57.2, 298.15, 0.00461);
        capNhatGiaoDien();
      });
    }

    // Nút xúc tác
    var btnXT = document.getElementById('hl-btn-xuc-tac');
    if (btnXT) {
      btnXT.addEventListener('click', function () {
        app.state.xucTac = !app.state.xucTac;
        btnXT.textContent = app.state.xucTac ? 'Tắt xúc tác' : 'Bật xúc tác';
        capNhatGiaoDien();
      });
    }

    // Nút Đặt lại
    var btnDatLai = document.getElementById('hl-btn-dat-lai');
    if (btnDatLai) {
      btnDatLai.addEventListener('click', function () {
        app.state.T = 298.15;
        app.state.V = 1.0;
        app.state.cA0 = 0.0400;
        var cb = HL_CanBang.canBangPhanLi(0.0400, 0, 0.00461);
        app.state.cA = cb.cA;
        app.state.cB = cb.cB;
        app.state.Q = cb.Q;
        app.state.Kc = 0.00461;
        app.state.xucTac = false;
        capNhatGiaoDien();
      });
    }

    // Sliders
    var slV = document.getElementById('hl-sl-v');
    if (slV) {
      slV.addEventListener('input', function () {
        var oldV = app.state.V;
        var newV = parseFloat(slV.value);
        var r = oldV / newV;
        app.state.V = newV;
        app.state.cA *= r;
        app.state.cB *= r;
        app.state.Q = Math.pow(app.state.cB, 2) / app.state.cA;
        capNhatGiaoDien();
      });
    }

    var slT = document.getElementById('hl-sl-t');
    if (slT) {
      slT.addEventListener('input', function () {
        app.state.T = parseFloat(slT.value);
        app.state.Kc = HL_CanBang.kcTheoT(app.state.T, 57.2, 298.15, 0.00461);
        capNhatGiaoDien();
      });
    }

    // Tải tab Công thức (C1–C7)
    var containerCongThuc = document.getElementById('hl-ds-cong-thuc-can-bang');
    if (containerCongThuc && typeof HLCongThuc !== 'undefined') {
      fetch('/static/hoa-hoc/hoa-ly/cong-thuc.json')
        .then(function (r) { return r.json(); })
        .then(function (data) {
          var ds = (data.cong_thuc || []).filter(function (c) {
            return (c.mo_phong || []).some(function (m) { return m.id === 'can-bang-le-chatelier'; }) || c.chu_de === 'can-bang';
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
      fetch('/static/hoa-hoc/hoa-ly/can-bang-le-chatelier/cau-hoi.json')
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
        he: app.state.he,
        T_K: app.state.T,
        V_L: app.state.V,
        C: {
          N2O4: (stVe && stVe.cA !== undefined) ? stVe.cA : app.state.cA,
          NO2: (stVe && stVe.cB !== undefined) ? stVe.cB : app.state.cB
        },
        Q: (app.state.he === 'H2_I2_2HI') ? (app.state.Kc || 50.0) : ((stVe && stVe.Q !== undefined) ? stVe.Q : app.state.Q),
        Kc: app.state.Kc,
        chieu: app.state.chieu,
        alpha: app.state.alpha,
        xuc_tac: app.state.xucTac,
        t_s: stVe ? stVe.t_s : 0
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
      if (params.he !== undefined) {
        app.state.he = params.he;
        if (params.he === 'H2_I2_2HI') {
          app.state.Kc = 50.0;
          app.state.Q = 50.0;
        }
      }
      if (params.V !== undefined) app.state.V = params.V;
      if (params.T !== undefined) app.state.T = params.T;
      if (params.cA !== undefined) app.state.cA = params.cA;
      if (params.cB !== undefined) app.state.cB = params.cB;
      if (params.xuc_tac !== undefined) app.state.xucTac = params.xuc_tac;
      if (app.state.he === 'H2_I2_2HI') {
        app.state.Q = app.state.Kc || 50.0;
      }
      capNhatGiaoDien();
    },
    datLai: function () {
      var btn = document.getElementById('hl-btn-dat-lai');
      if (btn) btn.click();
    }
  };

  document.addEventListener('DOMContentLoaded', khoiTao);
})(typeof window !== 'undefined' ? window : global);
