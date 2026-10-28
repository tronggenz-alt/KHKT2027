/**
 * giao-dien.js - Điều phối toàn bộ giao diện mô phỏng 2D Động cơ nhiệt 4 kỳ:
 * vẽ SVG, tương tác kéo chuột/cảm ứng, bảng điều khiển, số liệu, nhiên liệu, học & thi,
 * và cung cấp hook window.HoaLy cho kiểm thử tự động.
 */
(function () {
  'use strict';

  var CC = window.HLCoCau;
  var ND = window.HLNhietDong;
  var TD = window.HLTienDo;
  var DL = window.HLDongLuc;
  var VE = window.HLVe;
  var HK = window.HLHatKhi;

  // Tham số mô hình mặc định
  var ts = {
    r: 10,
    gamma: 1.4,
    T1: 300,
    p1: 100, // kPa
    q_in: 1500, // kJ/kg
    B: 80,
    S: 80,
    L: 140,
    rc: 2.0,
    chu_trinh: 'otto',
    n: 1200,
    tai: 0,
  };

  var tuyChonHienThi = {
    loaiDoThi: 'pv',
    logP: false,
    toCong: true,
    canhThang: false,
    bongMa: [], // danh sách chu trình cũ đã ghim
  };

  var W_hien_tai = 811;
  var H_hien_tai = 640;

  function layKichThuocKhung() {
    var wrap = document.getElementById('hl-khung-svg-wrapper') || svgEl;
    if (wrap) {
      var rect = wrap.getBoundingClientRect();
      if (rect.width > 50 && rect.height > 50) {
        W_hien_tai = Math.round(rect.width);
        H_hien_tai = Math.round(rect.height);
      }
    }
    return { w: W_hien_tai, h: H_hien_tai };
  }

  var tHienTai = 1.0; // Mặc định ở điểm 1 (bắt đầu nén)
  var vongHienTai = 1;
  var dangChay = false;
  var quanTinhBat = true;
  var deMayBat = false;

  var omegaQuanTinh = 0; // deg/s
  var omegaDeMay = 0; // rad/s
  var soLanDanhLua = 0;
  var hoatAnhDanhLuaKetThuc = 0;

  var heSoTocDo = 1.0;
  var chuKyThoiGian = 8.0; // 8s một chu trình ở 1x

  var svgEl, canvasEl, hatKhiInstance, svgDungSan;
  var moHinhDongLuc;

  // -------------------------------------------------------------------------
  // 1. TẢI TÊN MÔ PHỎNG & KHỞI TẠO NGUỒN SỰ THẬT TỪ danh-muc.json
  // -------------------------------------------------------------------------
  async function capNhatTenTuDanhMuc() {
    try {
      var res = await fetch('/static/hoa-hoc/hoa-ly/danh-muc.json', { cache: 'no-cache' });
      if (!res.ok) return;
      var data = await res.json();
      var muc = (data.mo_phong || []).find(function (m) { return m.id === 'dong-co-nhiet'; });
      if (muc && muc.ten) {
        document.title = muc.ten + ' · Hoá lý · CHEMISTRY';
        var breadcrumbTen = document.getElementById('hl-breadcrumb-ten');
        if (breadcrumbTen) breadcrumbTen.textContent = muc.ten;
      }
    } catch (e) {
      console.warn('Không thể đọc danh-muc.json:', e);
    }
  }

  // -------------------------------------------------------------------------
  // 2. KHỞI TẠO VÀ ĐỌC URL QUERY PARAMS (LINK CHIA SẺ)
  // -------------------------------------------------------------------------
  function docQueryParams() {
    var params = new URLSearchParams(window.location.search);
    if (params.has('r')) ts.r = parseFloat(params.get('r')) || ts.r;
    if (params.has('g')) ts.gamma = parseFloat(params.get('g')) || ts.gamma;
    if (params.has('q')) ts.q_in = parseFloat(params.get('q')) || ts.q_in;
    if (params.has('t1')) ts.T1 = parseFloat(params.get('t1')) || ts.T1;
    if (params.has('p1')) ts.p1 = (parseFloat(params.get('p1')) || 1.0) * 100;
    if (params.has('rc')) ts.rc = parseFloat(params.get('rc')) || ts.rc;
    if (params.has('ct')) ts.chu_trinh = params.get('ct');
    if (params.has('t')) tHienTai = parseFloat(params.get('t')) || 1.0;
    if (params.get('canh') === 'thang') tuyChonHienThi.canhThang = true;
  }

  function taoLinkChiaSe() {
    var url = new URL(window.location.href);
    url.searchParams.set('r', ts.r);
    url.searchParams.set('g', ts.gamma);
    url.searchParams.set('q', ts.q_in);
    url.searchParams.set('t1', ts.T1);
    url.searchParams.set('p1', (ts.p1 / 100).toFixed(2));
    url.searchParams.set('t', tHienTai.toFixed(2));
    if (ts.chu_trinh !== 'otto') url.searchParams.set('ct', ts.chu_trinh);
    if (ts.chu_trinh === 'diesel') url.searchParams.set('rc', ts.rc);
    if (tuyChonHienThi.canhThang) url.searchParams.set('canh', 'thang');
    return url.toString();
  }

  // -------------------------------------------------------------------------
  // 3. TÍNH TOÁN & CẬP NHẬT GIAO DIỆN
  // -------------------------------------------------------------------------
  function layTinhToanHienTai() {
    var tinhND = ts.chu_trinh === 'diesel' ? ND.diesel(ts) : ND.otto(ts);
    var hhCC = CC.hinhHoc(ts);
    return { tinhND: tinhND, hhCC: hhCC };
  }

  function layTrangThaiTucThoi() {
    return TD.tuTienDo(tHienTai, ts, ND, CC);
  }

  function capNhatToanBo(doiThongSo) {
    var sz = layKichThuocKhung();
    var tt = layTinhToanHienTai();
    var st = layTrangThaiTucThoi();

    if (doiThongSo) {
      VE.veCoDinh(svgDungSan, ts, tt.tinhND, tt.hhCC, tuyChonHienThi, sz.w, sz.h);
      capNhatBangSoLieu(tt.tinhND, tt.hhCC);
    }

    VE.capNhatChuyenDong(svgDungSan, st, ts, tt.tinhND, tt.hhCC, tuyChonHienThi, sz.w, sz.h);

    var elTenQTTop = document.getElementById('hl-nhan-qua-trinh');
    if (elTenQTTop) elTenQTTop.textContent = st.tenKy + ' · ' + st.tenQuaTrinh;

    // Cập nhật thẻ trạng thái hiện tại
    var elP = document.getElementById('hl-stat-p');
    var elV = document.getElementById('hl-stat-v');
    var elT = document.getElementById('hl-stat-t');
    var elPhi = document.getElementById('hl-stat-phi');
    var elBadgeKy = document.getElementById('hl-badge-ky');
    var elTenQT = document.getElementById('hl-ten-qua-trinh');

    if (elP) elP.innerHTML = CC.dinhDang(st.p, 2) + ' <span class="hl-unit">bar</span>';
    if (elV) elV.innerHTML = CC.dinhDang(st.V, 1) + ' <span class="hl-unit">cm³</span>';
    if (elT) elT.innerHTML = Math.round(st.T) + ' <span class="hl-unit">K</span>';
    if (elPhi) elPhi.textContent = CC.dinhDang(st.phi, 1) + '°';
    if (elBadgeKy) elBadgeKy.textContent = st.tenKy;
    if (elTenQT) elTenQT.textContent = st.tenQuaTrinh;

    // Định luật I: ΔU = A + Q
    var elDu = document.getElementById('hl-val-du');
    var elA = document.getElementById('hl-val-a');
    var elQ = document.getElementById('hl-val-q');
    var elDl1Status = document.getElementById('hl-chk-dl1-status');

    var duVal = st.deltaU;
    var aVal = st.A;
    var qVal = st.Q;
    if (elDu) elDu.textContent = (duVal >= 0 ? '+' : '') + CC.dinhDang(duVal, 1) + ' J';
    if (elA) elA.textContent = (aVal >= 0 ? '+' : '') + CC.dinhDang(aVal, 1) + ' J';
    if (elQ) elQ.textContent = (qVal >= 0 ? '+' : '') + CC.dinhDang(qVal, 1) + ' J';

    var saiSoDl1 = Math.abs(duVal - (aVal + qVal));
    if (elDl1Status) {
      if (saiSoDl1 <= 1e-4) {
        elDl1Status.textContent = '✓ Khớp';
        elDl1Status.className = 'hl-tag-pass';
      } else {
        elDl1Status.textContent = 'Sai lệch';
        elDl1Status.className = 'hl-tag-fail';
      }
    }

    // Cập nhật Timeline
    var thumb = document.getElementById('hl-timeline-thumb');
    var labelPhi = document.getElementById('hl-label-phi');
    var labelVong = document.getElementById('hl-label-vong');
    if (thumb) {
      var phanTram = (st.phi / 720) * 100;
      thumb.style.left = phanTram + '%';
    }
    if (labelPhi) labelPhi.textContent = Math.round(st.phi);
    if (labelVong) labelVong.textContent = vongHienTai;

    // Cập nhật canvas hạt khí
    if (hatKhiInstance && canvasEl) {
      var tl = VE.tinhTiLe(ts, tt.tinhND, tt.hhCC, sz.w, sz.h, tuyChonHienThi.canhThang);
      var xPiston = tl.xPiston(st.V);
      var vungKhi = {
        x: tl.X_NAP,
        y: tl.y_cyl_top,
        w: Math.max(0, xPiston - tl.X_NAP),
        h: tl.H_CYL
      };
      hatKhiInstance.capNhatVaVe(vungKhi, st.T, st.doan);
    }
  }

  function capNhatBangSoLieu(tinhND, hhCC) {
    var tbody = document.getElementById('hl-tbody-trang-thai');
    if (!tbody) return;

    var cacDiem = [
      { id: '0 (Bắt đầu nạp)', p: tinhND.p1 / 100, V: hhCC.Vc_cm3, T: tinhND.T1 },
      { id: '1 (Cuối nạp / nén)', p: tinhND.p1 / 100, V: hhCC.V1_cm3, T: tinhND.T1 },
      { id: '2 (Cuối nén)', p: tinhND.p2 / 100, V: hhCC.Vc_cm3, T: tinhND.T2 },
      { id: '3 (Sau cháy)', p: tinhND.p3 / 100, V: hhCC.Vc_cm3, T: tinhND.T3 },
      { id: '4 (Cuối giãn nở)', p: tinhND.p4 / 100, V: hhCC.V1_cm3, T: tinhND.T4 },
    ];

    tbody.innerHTML = cacDiem.map(function (d) {
      return '<tr>' +
        '<td class="py-1 text-left font-sans font-medium text-muc">' + d.id + '</td>' +
        '<td class="py-1 text-right">' + CC.dinhDang(d.p, 2) + '</td>' +
        '<td class="py-1 text-right">' + CC.dinhDang(d.V, 1) + '</td>' +
        '<td class="py-1 text-right">' + Math.round(d.T) + '</td>' +
        '<td class="py-1 text-right">' + Math.round(d.T - 273.15) + '</td>' +
      '</tr>';
    }).join('');

    // Hiển thị tóm tắt hiệu suất & công
    var setEl = function (id, val) {
      var el = document.getElementById(id);
      if (el) el.textContent = val;
    };

    setEl('hl-val-qin-res', CC.dinhDang(tinhND.q_in, 1) + ' kJ/kg');
    setEl('hl-val-qout-res', CC.dinhDang(tinhND.q_out, 1) + ' kJ/kg');
    setEl('hl-val-w-res', CC.dinhDang(tinhND.w, 1) + ' kJ/kg');
    setEl('hl-val-W-res', CC.dinhDang(tinhND.W_J, 1) + ' J');
    setEl('hl-val-eta-res', CC.dinhDang(tinhND.eta_phan_tram, 1) + ' %');
    setEl('hl-val-carnot-res', CC.dinhDang(tinhND.eta_Carnot * 100, 1) + ' %');
    setEl('hl-val-mep-res', CC.dinhDang(tinhND.MEP / 100, 2) + ' bar');
    setEl('hl-val-cong-suat-res', CC.dinhDang(tinhND.P_kW, 2) + ' kW');
  }

  // -------------------------------------------------------------------------
  // 4. VÒNG LẶP HOẠT HỌA (ANIMATION LOOP)
  // -------------------------------------------------------------------------
  var thoiGianKhungTruoc = null;

  function vongLap(thoiGianHienTai) {
    if (!thoiGianKhungTruoc) thoiGianKhungTruoc = thoiGianHienTai;
    var dt_ms = thoiGianHienTai - thoiGianKhungTruoc;
    thoiGianKhungTruoc = thoiGianHienTai;

    if (dt_ms > 200) dt_ms = 200; // giới hạn tránh bước quá lớn khi chuyển tab
    var dt_s = dt_ms / 1000;

    var coChuyenDong = false;

    if (deMayBat) {
      // Chế độ Đề máy (tính phương trình chuyển động thật I * domega/dt)
      var tt = layTinhToanHienTai();
      var layKhi = function (phiDeg) {
        var p = ((phiDeg % 720) + 720) % 720;
        var t;
        if (p < 180) t = p / 180;
        else if (p < 360) t = 1 + (p - 180) / 180;
        else if (p < 540) t = 3 + (p - 360) / 180;
        else t = 5 + (p - 540) / 180;

        var stPhi = TD.tuTienDo(t, ts, ND, CC);
        return {
          p_Pa: stPhi.p * 1e5,
          A_m2: tt.hhCC.A_mm2 * 1e-6,
          ds_dtheta_m: CC.ds_dtheta(phiDeg, ts) * 1e-3,
        };
      };

      var dt_mo_phong = Math.min(0.04, dt_s);
      var resDeMay = moHinhDongLuc.buocDeMay(layTrangThaiTucThoi().phi, omegaDeMay, dt_mo_phong, layKhi, {
        tai: ts.tai || 0,
        p_atm: 100000,
        I: 0.08,
      });

      omegaDeMay = resDeMay.omega_rad_s;
      var dPhi = resDeMay.phi - layTrangThaiTucThoi().phi;
      if (Math.abs(dPhi) > 1e-4) {
        buocGoc(dPhi);
        coChuyenDong = true;
      }

      // Cập nhật đồng hồ đề máy
      var rpmEl = document.getElementById('hl-rpm-val');
      var pDongCoEl = document.getElementById('hl-p-dong-co');
      var ttDongCoEl = document.getElementById('hl-trang-thai-dong-co');
      if (rpmEl) rpmEl.textContent = Math.round(resDeMay.rpm);
      if (pDongCoEl) {
        var pKW = (tt.tinhND.W_J * (resDeMay.rpm / 2) / 60) / 1000;
        pDongCoEl.textContent = CC.dinhDang(pKW, 1);
      }
      if (ttDongCoEl) {
        if (resDeMay.rpm > 650) {
          ttDongCoEl.textContent = 'Máy đã nổ';
          ttDongCoEl.className = 'font-bold text-green-600';
        } else {
          ttDongCoEl.textContent = 'Chưa đủ đà';
          ttDongCoEl.className = 'font-bold text-gach';
        }
      }
    } else if (dangChay) {
      // Chế độ chạy tự động bình thường
      var dPhiChay = (720 / chuKyThoiGian) * heSoTocDo * dt_s;
      buocGoc(dPhiChay);
      coChuyenDong = true;
    } else if (quanTinhBat && Math.abs(omegaQuanTinh) > 0.5) {
      // Chế độ quán tính bánh đà tự do
      omegaQuanTinh = moHinhDongLuc.buocQuanTinh(omegaQuanTinh, dt_s);
      var dPhiQT = omegaQuanTinh * dt_s;
      buocGoc(dPhiQT);
      coChuyenDong = true;
    }

    // Kiểm tra hoạt ảnh đánh lửa (250 ms)
    if (thoiGianHienTai < hoatAnhDanhLuaKetThuc) {
      var badgeDanhLua = document.getElementById('hl-thong-bao-danh-lua');
      if (badgeDanhLua) badgeDanhLua.style.opacity = '1';
      coChuyenDong = true;
    } else {
      var badgeDL = document.getElementById('hl-thong-bao-danh-lua');
      if (badgeDL && badgeDL.style.opacity !== '0') badgeDL.style.opacity = '0';
    }

    if (coChuyenDong || document.visibilityState === 'visible') {
      capNhatToanBo(false);
    }

    requestAnimationFrame(vongLap);
  }

  function buocGoc(dPhi) {
    var buoc = TD.buocGoc(tHienTai, dPhi, ts);
    tHienTai = buoc.t;
    vongHienTai += buoc.vongDelta;

    if (buoc.suKien.indexOf('danh_lua') !== -1) {
      soLanDanhLua++;
      hoatAnhDanhLuaKetThuc = performance.now() + 250;
    }
  }

  // -------------------------------------------------------------------------
  // 5. KHỞI TẠO CÁC NÚT BẤM & TAB GIAO DIỆN
  // -------------------------------------------------------------------------
  function khoiTaoDieuKhien() {
    // Nút Chạy / Dừng
    var btnPlay = document.getElementById('hl-btn-play');
    var iconPlay = document.getElementById('hl-icon-play');
    var iconPause = document.getElementById('hl-icon-pause');
    var textPlay = document.getElementById('hl-text-play');

    function capNhatNutPlay() {
      if (dangChay) {
        if (iconPlay) iconPlay.classList.add('hidden');
        if (iconPause) iconPause.classList.remove('hidden');
        if (textPlay) textPlay.textContent = 'Dừng';
      } else {
        if (iconPlay) iconPlay.classList.remove('hidden');
        if (iconPause) iconPause.classList.add('hidden');
        if (textPlay) textPlay.textContent = 'Chạy';
      }
    }

    if (btnPlay) {
      btnPlay.addEventListener('click', function () {
        dangChay = !dangChay;
        omegaQuanTinh = 0;
        capNhatNutPlay();
      });
    }

    // Nút Kỳ trước / Kỳ sau / Đặt lại
    var btnPrev = document.getElementById('hl-btn-prev-ky');
    var btnNext = document.getElementById('hl-btn-next-ky');
    var btnReset = document.getElementById('hl-btn-reset');

    if (btnPrev) {
      btnPrev.addEventListener('click', function () {
        dangChay = false;
        capNhatNutPlay();
        buocGoc(-90);
        capNhatToanBo(false);
      });
    }
    if (btnNext) {
      btnNext.addEventListener('click', function () {
        dangChay = false;
        capNhatNutPlay();
        buocGoc(90);
        capNhatToanBo(false);
      });
    }
    if (btnReset) {
      btnReset.addEventListener('click', function () {
        dangChay = false;
        omegaQuanTinh = 0;
        omegaDeMay = 0;
        tHienTai = 1.0;
        vongHienTai = 1;
        capNhatNutPlay();
        capNhatToanBo(false);
      });
    }

    // Chọn tốc độ
    var selTocDo = document.getElementById('hl-select-toc-do');
    if (selTocDo) {
      selTocDo.addEventListener('change', function () {
        heSoTocDo = parseFloat(selTocDo.value) || 1.0;
      });
    }

    // Checkbox quán tính & đề máy
    var chkQT = document.getElementById('hl-chk-quan-tinh');
    var chkDM = document.getElementById('hl-chk-de-may');
    var khuDM = document.getElementById('hl-khu-de-may');
    if (chkQT) {
      chkQT.addEventListener('change', function () {
        quanTinhBat = chkQT.checked;
        if (!quanTinhBat) omegaQuanTinh = 0;
      });
    }
    if (chkDM) {
      chkDM.addEventListener('change', function () {
        deMayBat = chkDM.checked;
        dangChay = false;
        capNhatNutPlay();
        if (khuDM) {
          if (deMayBat) khuDM.classList.remove('hidden');
          else khuDM.classList.add('hidden');
        }
      });
    }

    var sliderTai = document.getElementById('hl-slider-tai');
    var labelTai = document.getElementById('hl-label-tai');
    if (sliderTai) {
      sliderTai.addEventListener('input', function () {
        ts.tai = parseFloat(sliderTai.value) || 0;
        if (labelTai) labelTai.textContent = ts.tai + ' N·m';
      });
    }

    // Slider r (tỉ số nén)
    var sliderR = document.getElementById('hl-slider-r');
    var valR = document.getElementById('hl-val-r');
    if (sliderR) {
      sliderR.value = ts.r;
      sliderR.addEventListener('input', function () {
        ts.r = parseFloat(sliderR.value);
        if (valR) valR.textContent = CC.dinhDang(ts.r, 1);
        capNhatToanBo(true);
      });
    }

    // Slider gamma
    var sliderGamma = document.getElementById('hl-slider-gamma');
    var valGamma = document.getElementById('hl-val-gamma');
    if (sliderGamma) {
      sliderGamma.value = ts.gamma;
      sliderGamma.addEventListener('input', function () {
        ts.gamma = parseFloat(sliderGamma.value);
        if (valGamma) valGamma.textContent = CC.dinhDang(ts.gamma, 2);
        capNhatToanBo(true);
      });
    }

    // Slider q_in
    var sliderQin = document.getElementById('hl-slider-qin');
    var valQin = document.getElementById('hl-val-qin');
    if (sliderQin) {
      sliderQin.value = ts.q_in;
      sliderQin.addEventListener('input', function () {
        ts.q_in = parseFloat(sliderQin.value);
        if (valQin) valQin.textContent = Math.round(ts.q_in);
        capNhatToanBo(true);
      });
    }

    // Slider T1 và p1
    var sliderT1 = document.getElementById('hl-slider-t1');
    var valT1 = document.getElementById('hl-val-t1');
    if (sliderT1) {
      sliderT1.value = ts.T1;
      sliderT1.addEventListener('input', function () {
        ts.T1 = parseFloat(sliderT1.value);
        if (valT1) valT1.textContent = Math.round(ts.T1);
        capNhatToanBo(true);
      });
    }

    var sliderP1 = document.getElementById('hl-slider-p1');
    var valP1 = document.getElementById('hl-val-p1');
    if (sliderP1) {
      sliderP1.value = (ts.p1 / 100).toFixed(2);
      sliderP1.addEventListener('input', function () {
        var pBar = parseFloat(sliderP1.value);
        ts.p1 = pBar * 100;
        if (valP1) valP1.textContent = CC.dinhDang(pBar, 2);
        capNhatToanBo(true);
      });
    }

    // Chọn chu trình (otto / diesel / wiebe)
    var selCT = document.getElementById('hl-select-chu-trinh');
    var grpRc = document.getElementById('hl-group-rc');
    if (selCT) {
      selCT.value = ts.chu_trinh;
      selCT.addEventListener('change', function () {
        ts.chu_trinh = selCT.value;
        if (grpRc) {
          if (ts.chu_trinh === 'diesel') grpRc.classList.remove('hidden');
          else grpRc.classList.add('hidden');
        }
        capNhatToanBo(true);
      });
    }

    // Nút tuỳ chọn đồ thị: Log p, Tô công, Ghim bóng ma
    var btnLog = document.getElementById('hl-btn-log');
    if (btnLog) {
      btnLog.addEventListener('click', function () {
        tuyChonHienThi.logP = !tuyChonHienThi.logP;
        btnLog.classList.toggle('active', tuyChonHienThi.logP);
        capNhatToanBo(true);
      });
    }

    var btnDienTich = document.getElementById('hl-btn-dien-tich');
    if (btnDienTich) {
      btnDienTich.addEventListener('click', function () {
        tuyChonHienThi.toCong = !tuyChonHienThi.toCong;
        btnDienTich.classList.toggle('active', tuyChonHienThi.toCong);
        if (svgDungSan && svgDungSan.gDienTichCong) {
          svgDungSan.gDienTichCong.style.display = tuyChonHienThi.toCong ? 'block' : 'none';
        }
      });
    }

    var btnBongMa = document.getElementById('hl-btn-bong-ma');
    if (btnBongMa) {
      btnBongMa.addEventListener('click', function () {
        ghimBongMa();
      });
    }

    var btnCanhThang = document.getElementById('hl-btn-canh-thang');
    if (btnCanhThang) {
      if (tuyChonHienThi.canhThang) btnCanhThang.classList.add('active');
      btnCanhThang.addEventListener('click', function () {
        tuyChonHienThi.canhThang = !tuyChonHienThi.canhThang;
        btnCanhThang.classList.toggle('active', tuyChonHienThi.canhThang);
        capNhatToanBo(true);
      });
    }

    // Nút chuyển tab (Thông số / Số liệu / Nhiên liệu / Học)
    var tabButtons = document.querySelectorAll('.hl-tab-nav');
    tabButtons.forEach(function (btn) {
      btn.addEventListener('click', function () {
        var tabId = btn.dataset.tab;
        tabButtons.forEach(function (b) { b.classList.remove('active'); });
        btn.classList.add('active');

        document.querySelectorAll('.hl-tab-pane').forEach(function (p) {
          p.classList.add('hidden');
        });
        var pane = document.getElementById('hl-tab-' + tabId);
        if (pane) pane.classList.remove('hidden');
      });
    });

    // Nút chia sẻ link
    var btnChiaSe = document.getElementById('hl-btn-chia-se');
    if (btnChiaSe) {
      btnChiaSe.addEventListener('click', function () {
        var link = taoLinkChiaSe();
        if (navigator.clipboard) {
          navigator.clipboard.writeText(link).then(function () {
            alert('Đã sao chép liên kết chia sẻ vào khay nhớ tạm!');
          });
        } else {
          prompt('Liên kết chia sẻ của bạn:', link);
        }
      });
    }

    // Nút sáng / tối
    var nutCheDo = document.getElementById('nut-che-do');
    if (nutCheDo) {
      nutCheDo.addEventListener('click', function () {
        var hienTai = document.documentElement.dataset.theme;
        var moi = hienTai === 'toi' ? 'sang' : 'toi';
        document.documentElement.dataset.theme = moi;
        try { localStorage.setItem('giai-chuoi-giao-dien', moi); } catch (e) {}
        capNhatToanBo(true);
      });
    }
  }

  function ghimBongMa() {
    var tt = layTinhToanHienTai();
    var tl = VE.tinhTiLe(ts, tt.tinhND, tt.hhCC);
    var gBM = svgDungSan.gBongMa;
    if (!gBM) return;

    // Giữ tối đa 2 chu trình cũ
    while (gBM.children.length >= 2) {
      gBM.removeChild(gBM.firstChild);
    }

    var clone = svgDungSan.gDuongChuTrinh.cloneNode(true);
    clone.removeAttribute('id');
    clone.setAttribute('opacity', '0.45');
    clone.setAttribute('stroke-dasharray', '4 3');
    clone.style.pointerEvents = 'none';

    // Thêm nhãn r, eta
    var txt = document.createElementNS('http://www.w3.org/2000/svg', 'text');
    txt.setAttribute('x', tl.X_NAP + tt.hhCC.V1_cm3 * tl.scale_x);
    txt.setAttribute('y', 120 + gBM.children.length * 16);
    txt.setAttribute('font-size', '9');
    txt.setAttribute('fill', 'var(--lang)');
    txt.textContent = 'r=' + ts.r + ' η=' + CC.dinhDang(tt.tinhND.eta_phan_tram, 1) + '%';
    clone.appendChild(txt);

    gBM.appendChild(clone);
  }

  // -------------------------------------------------------------------------
  // 6. KHỞI TẠO TAB NHIÊN LIỆU (HOÁ 10) & TAB HỌC (CÂU HỎI THPT)
  // -------------------------------------------------------------------------
  async function khoiTaoTabNhienLieu() {
    var selNL = document.getElementById('hl-select-nhien-lieu');
    if (!selNL) return;

    try {
      var res = await fetch('/static/hoa-hoc/hoa-ly/dong-co-nhiet/nhien-lieu.json');
      if (!res.ok) return;
      var data = await res.json();
      var ds = data.danh_sach || [];

      selNL.innerHTML = ds.map(function (nl) {
        return '<option value="' + nl.id + '">' + nl.ten + ' (' + nl.cong_thuc + ')</option>';
      }).join('');

      var qInTinhDuoc = 1500;

      function capNhatTinhNhienLieu() {
        var idChon = selNL.value;
        var nl = ds.find(function (x) { return x.id === idChon; }) || ds[0];
        var sLambda = document.getElementById('hl-slider-lambda');
        var sK = document.getElementById('hl-slider-k');
        var lambda = sLambda ? parseFloat(sLambda.value) : 1.0;
        var k = sK ? parseFloat(sK.value) : 0.55;

        var lblLambda = document.getElementById('hl-val-lambda');
        var lblK = document.getElementById('hl-val-k');
        if (lblLambda) lblLambda.textContent = CC.dinhDang(lambda, 2);
        if (lblK) lblK.textContent = CC.dinhDang(k, 2);

        var ketQua = ND.tinhNhienLieu(nl, { lambda: lambda, k: k });
        qInTinhDuoc = Math.round(ketQua.q_in);

        var ptEl = document.getElementById('hl-pt-chay');
        var dchEl = document.getElementById('hl-val-dch');
        var lhvEl = document.getElementById('hl-val-lhv');
        var afrEl = document.getElementById('hl-val-afr');
        var qinEl = document.getElementById('hl-val-qin-tinh');

        if (ptEl) ptEl.textContent = ketQua.phuong_trinh;
        if (dchEl) dchEl.textContent = CC.dinhDang(ketQua.delta_cH_LHV, 1) + ' kJ/mol';
        if (lhvEl) lhvEl.textContent = CC.dinhDang(ketQua.LHV_MJ_kg, 2) + ' MJ/kg';
        if (afrEl) afrEl.textContent = CC.dinhDang(ketQua.AFR, 2) + ' kg không khí / kg';
        if (qinEl) qinEl.textContent = qInTinhDuoc + ' kJ/kg';
      }

      selNL.addEventListener('change', capNhatTinhNhienLieu);
      var sLambda = document.getElementById('hl-slider-lambda');
      var sK = document.getElementById('hl-slider-k');
      if (sLambda) sLambda.addEventListener('input', capNhatTinhNhienLieu);
      if (sK) sK.addEventListener('input', capNhatTinhNhienLieu);

      var btnApDung = document.getElementById('hl-btn-ap-dung-qin');
      if (btnApDung) {
        btnApDung.addEventListener('click', function () {
          ts.q_in = qInTinhDuoc;
          var sliderQin = document.getElementById('hl-slider-qin');
          var valQin = document.getElementById('hl-val-qin');
          if (sliderQin) sliderQin.value = ts.q_in;
          if (valQin) valQin.textContent = ts.q_in;
          capNhatToanBo(true);
          alert('Đã cập nhật nhiệt cấp q_in = ' + ts.q_in + ' kJ/kg vào chu trình!');
        });
      }

      capNhatTinhNhienLieu();
    } catch (e) {
      console.warn('Lỗi khởi tạo tab nhiên liệu:', e);
    }
  }

  async function khoiTaoTabHoc() {
    var khuCauHoi = document.getElementById('hl-khu-cau-hoi');
    if (!khuCauHoi) return;

    try {
      var res = await fetch('/static/hoa-hoc/hoa-ly/dong-co-nhiet/cau-hoi.json');
      if (!res.ok) return;
      var data = await res.json();
      var ds = data.danh_sach || [];

      var dangHienTai = 'I';

      function renderCauHoi() {
        khuCauHoi.innerHTML = '';
        var dsDang = ds.filter(function (c) { return c.dang === dangHienTai; });

        dsDang.forEach(function (c) {
          var card = document.createElement('div');
          card.className = 'p-3 rounded-lg border border-chi/80 bg-giay-lom/30 flex flex-col gap-2';

          var noiDungHtml = '';
          if (c.dang === 'I') {
            noiDungHtml = (c.lua_chon || []).map(function (opt, idx) {
              var ma = String.fromCharCode(65 + idx);
              return '<label class="flex items-center gap-2 cursor-pointer p-1 rounded hover:bg-giay">' +
                '<input type="radio" name="' + c.id + '" value="' + ma + '" class="hl-radio">' +
                '<span>' + opt + '</span>' +
              '</label>';
            }).join('');
          } else if (c.dang === 'II') {
            noiDungHtml = (c.y || []).map(function (item, idx) {
              return '<div class="flex items-center justify-between p-1 rounded hover:bg-giay text-[11px]">' +
                '<span class="flex-1 pr-2">' + item.noi_dung + '</span>' +
                '<div class="flex gap-2 shrink-0 font-medium">' +
                  '<label class="cursor-pointer"><input type="radio" name="' + c.id + '_' + idx + '" value="dung"> Đ</label>' +
                  '<label class="cursor-pointer"><input type="radio" name="' + c.id + '_' + idx + '" value="sai"> S</label>' +
                '</div>' +
              '</div>';
            }).join('');
          } else if (c.dang === 'III') {
            noiDungHtml = '<div class="flex items-center gap-2 mt-1">' +
              '<input type="text" id="inp_' + c.id + '" placeholder="Nhập đáp số..." class="hl-input-num flex-1 p-1">' +
              '<button type="button" class="hl-btn-ctrl hl-btn-kiem-tra px-3 py-1 text-xs" data-id="' + c.id + '">Kiểm tra</button>' +
            '</div>';
          }

          card.innerHTML =
            '<div class="font-semibold text-muc text-xs">' + c.tieu_de + '</div>' +
            '<p class="text-muc leading-relaxed">' + c.cau_hoi + '</p>' +
            '<div class="mt-1 flex flex-col gap-1">' + noiDungHtml + '</div>' +
            '<div id="gt_' + c.id + '" class="mt-2 p-2 rounded bg-giay border border-chi/60 text-[10px] text-lang hidden">' +
              '<b class="text-muc block mb-0.5">Lời giải chi tiết:</b>' + c.giai_thich +
            '</div>';

          // Nút kiểm tra đáp án
          var btnKiemTra = card.querySelector('.hl-btn-kiem-tra');
          if (btnKiemTra) {
            btnKiemTra.addEventListener('click', function () {
              var inp = card.querySelector('#inp_' + c.id);
              var gt = card.querySelector('#gt_' + c.id);
              if (inp && gt) {
                gt.classList.remove('hidden');
                var val = inp.value.trim().replace('.', ',');
                if (val === c.dap_so) {
                  inp.style.borderColor = '#16a34a';
                } else {
                  inp.style.borderColor = '#dc2626';
                }
              }
            });
          }

          khuCauHoi.appendChild(card);
        });
      }

      // Nút lọc dạng I / II / III
      var btnsDang = document.querySelectorAll('.hl-btn-dang-cau');
      btnsDang.forEach(function (btn) {
        btn.addEventListener('click', function () {
          btnsDang.forEach(function (b) { b.classList.remove('active'); });
          btn.classList.add('active');
          dangHienTai = btn.dataset.dang;
          renderCauHoi();
        });
      });

      renderCauHoi();
    } catch (e) {
      console.warn('Lỗi khởi tạo tab học:', e);
    }
  }

  async function khoiTaoTabCongThuc() {
    var khuCT = document.getElementById('hl-danh-sach-ct-mo-phong');
    if (!khuCT || !window.HLCongThuc) return;
    try {
      var data = await window.HLCongThuc.taiDuLieu();
      var cacCT = (data.cong_thuc || []).filter(function (ct) {
        return (ct.mo_phong || []).some(function (mp) { return mp.id === 'dong-co-nhiet'; });
      });
      khuCT.innerHTML = '';
      cacCT.forEach(function (ct) {
        var card = window.HLCongThuc.taoTheCongThuc(ct);
        khuCT.appendChild(card);
      });
    } catch (e) {
      console.warn('Lỗi khởi tạo tab công thức:', e);
    }
  }

  // -------------------------------------------------------------------------
  // 7. HOOK KIỂM THỬ window.HoaLy (PLAYWRIGHT CDP)
  // -------------------------------------------------------------------------
  function caiDatHookKiemThu() {
    window.HoaLy = {
      id: 'dong-co-nhiet',
      trangThai: function () {
        var st = layTrangThaiTucThoi();
        var tt = layTinhToanHienTai();
        return {
          t: st.t,
          phi: st.phi,
          vong: vongHienTai,
          doan: st.doan,
          ky: st.ky,
          p_bar: parseFloat(st.p.toFixed(3)),
          V_cm3: parseFloat(st.V.toFixed(3)),
          T_K: parseFloat(st.T.toFixed(1)),
          W_J: parseFloat(tt.tinhND.W_J.toFixed(2)),
          eta: parseFloat(tt.tinhND.eta_phan_tram.toFixed(2)),
          deltaU: parseFloat(st.deltaU.toFixed(2)),
          A: parseFloat(st.A.toFixed(2)),
          Q: parseFloat(st.Q.toFixed(2)),
          f: st.f,
          soLanDanhLua: soLanDanhLua,
          omegaDeMay: omegaDeMay,
          rpm: Math.round(omegaDeMay * (60 / (2 * Math.PI))),
          tai: ts.tai
        };
      },
      datTienDo: function (newT) {
        tHienTai = newT;
        capNhatToanBo(false);
      },
      datGoc: function (newPhi) {
        var stHienTai = layTrangThaiTucThoi();
        var dPhi = newPhi - stHienTai.phi;
        buocGoc(dPhi);
        capNhatToanBo(false);
      },
      datThamSo: function (obj) {
        Object.assign(ts, obj);
        capNhatToanBo(true);
      },
      datLai: function () {
        tHienTai = 1.0;
        vongHienTai = 1;
        omegaQuanTinh = 0;
        dangChay = false;
        capNhatToanBo(true);
      },
      choi: function () {
        dangChay = true;
      },
      dung: function () {
        dangChay = false;
        omegaQuanTinh = 0;
      },
      buoc: function (ms) {
        var dt = (ms || 16.67) / 1000;
        buocTienDo(dt);
        capNhatToanBo(false);
      },
      deMay: function (rpm) {
        deMayBat = true;
        var chkDM = document.getElementById('hl-chk-de-may');
        if (chkDM) chkDM.checked = true;
        var khuDM = document.getElementById('hl-khu-de-may');
        if (khuDM) khuDM.classList.remove('hidden');
        omegaDeMay = (rpm || 1000) * (2 * Math.PI / 60);
      },
      boCuc: function () {
        var sz = layKichThuocKhung();
        var tt = layTinhToanHienTai();
        var st = layTrangThaiTucThoi();
        var tl = VE.tinhTiLe(ts, tt.tinhND, tt.hhCC, sz.w, sz.h, tuyChonHienThi.canhThang);
        var rect = svgEl.getBoundingClientRect();
        var vb = svgEl.viewBox ? svgEl.viewBox.baseVal : null;
        var sx = (vb && vb.width > 0) ? rect.width / vb.width : 1;
        var sy = (vb && vb.height > 0) ? rect.height / vb.height : 1;
        var xP = tl.xPiston(st.V);

        var cycleW = (tt.hhCC.V1_cm3 - tt.hhCC.Vc_cm3) * tl.scale_g_x * sx;
        var cycleH = ((tt.tinhND.p3 - tt.tinhND.p1) / 100) * tl.scale_g_y * sy;

        return {
          khung: { w: Math.round(rect.width), h: Math.round(rect.height) },
          doThi: {
            x: rect.left + tl.x_g_left * sx,
            y: rect.top + tl.y_g_top * sy,
            w: tl.w_g * sx,
            h: tl.h_g * sy
          },
          duong: { w: Math.round(cycleW), h: Math.round(cycleH) },
          vungKhi: {
            x: rect.left + tl.X_NAP * sx,
            y: rect.top + tl.y_cyl_top * sy,
            w: (xP - tl.X_NAP) * sx,
            h: tl.H_CYL * sy
          },
          coChu: { truc: 12, nhanDiem: 13 }
        };
      },
      hat: function () {
        if (!hatKhiInstance) return [];
        return hatKhiInstance.layDanhSachHat();
      },
      toaDo: {
        tamBanhDa: function () {
          var sz = layKichThuocKhung();
          var tt = layTinhToanHienTai();
          var tl = VE.tinhTiLe(ts, tt.tinhND, tt.hhCC, sz.w, sz.h, tuyChonHienThi.canhThang);
          return svgToClient(tl.x_tam, tl.y_tam);
        },
        chotKhuyu: function () {
          var chot = document.getElementById('hl-chot-khuyu');
          if (chot) {
            var r = chot.getBoundingClientRect();
            return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
          }
          return this.tamBanhDa();
        },
        matPiston: function () {
          var sz = layKichThuocKhung();
          var st = layTrangThaiTucThoi();
          var tt = layTinhToanHienTai();
          var tl = VE.tinhTiLe(ts, tt.tinhND, tt.hhCC, sz.w, sz.h, tuyChonHienThi.canhThang);
          var xPiston = tl.xPiston(st.V);
          return svgToClient(xPiston, tl.y_tam);
        },
        diemPV: function () {
          var dot = document.getElementById('hl-dot-trang-thai');
          if (dot) {
            var r = dot.getBoundingClientRect();
            return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
          }
          var sz = layKichThuocKhung();
          var st = layTrangThaiTucThoi();
          var tt = layTinhToanHienTai();
          var tl = VE.tinhTiLe(ts, tt.tinhND, tt.hhCC, sz.w, sz.h, tuyChonHienThi.canhThang);
          return svgToClient(tl.xGraph(st.V), tl.yGraph(st.p));
        },
        banhDa: function () { return this.tamBanhDa(); },
        pitTong: function () { return this.matPiston(); },
        matPitTong: function () { return this.matPiston(); },
        chamDoThi: function () { return this.diemPV(); },
        thanhThoiGian: function () {
          var el = document.getElementById('hl-thanh-thoi-gian');
          var rect = el ? el.getBoundingClientRect() : { left: 0, top: 0, width: 0, height: 0 };
          return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
        }
      }
    };
  }

  function svgToClient(svgX, svgY) {
    if (!svgEl) return { x: svgX, y: svgY };
    var pt = svgEl.createSVGPoint();
    pt.x = svgX;
    pt.y = svgY;
    var ctm = svgEl.getScreenCTM();
    if (ctm) {
      var clientPt = pt.matrixTransform(ctm);
      return { x: clientPt.x, y: clientPt.y };
    }
    var rect = svgEl.getBoundingClientRect();
    var vb = svgEl.viewBox ? svgEl.viewBox.baseVal : null;
    var vw = (vb && vb.width > 0) ? vb.width : rect.width;
    var vh = (vb && vb.height > 0) ? vb.height : rect.height;
    return {
      x: rect.left + (svgX / vw) * rect.width,
      y: rect.top + (svgY / vh) * rect.height
    };
  }

  // -------------------------------------------------------------------------
  // 8. ĐIỂM VÀO CHÍNH (BOOTSTRAP)
  // -------------------------------------------------------------------------
  function khoiTao() {
    svgEl = document.getElementById('hl-svg');
    canvasEl = document.getElementById('hl-canvas-hat-khi');
    if (!svgEl) return;

    docQueryParams();
    svgDungSan = VE.khoiTaoSVG(svgEl);
    if (canvasEl && HK) {
      hatKhiInstance = HK.khoiTaoHatKhi(canvasEl, svgEl);
    }
    moHinhDongLuc = DL.taoMoHinhDongLuc({ I: 0.05 });

    // Khởi tạo tương tác kéo
    var contextTuongTac = {
      layTrangThai: layTrangThaiTucThoi,
      datTienDo: function (newT) { tHienTai = newT; capNhatToanBo(false); },
      datGoc: function (newPhi) {
        var stH = layTrangThaiTucThoi();
        buocGoc(newPhi - stH.phi);
        capNhatToanBo(false);
      },
      buocGoc: function (dPhi) {
        buocGoc(dPhi);
        capNhatToanBo(false);
      },
      datTheoPiston: function (V_keo) {
        var stH = layTrangThaiTucThoi();
        var phiMoi = TD.gocTuTheTich(V_keo, stH.phi, ts, CC);
        buocGoc(phiMoi - stH.phi);
        capNhatToanBo(false);
      },
      datTheoDiemPV: function (V_chuot, p_chuot) {
        var tMoi = TD.tienDoTuDiemPV(V_chuot, p_chuot, tHienTai, ts, null, ND, CC);
        tHienTai = tMoi;
        capNhatToanBo(false);
      },
      layTiLe: function () {
        var sz = layKichThuocKhung();
        var tt = layTinhToanHienTai();
        return VE.tinhTiLe(ts, tt.tinhND, tt.hhCC, sz.w, sz.h, tuyChonHienThi.canhThang);
      },
      chuyenChayDung: function () {
        dangChay = !dangChay;
        omegaQuanTinh = 0;
        var btnPlay = document.getElementById('hl-btn-play');
        if (btnPlay) btnPlay.click();
      },
      dung: function () {
        dangChay = false;
        omegaQuanTinh = 0;
      },
      datLai: function () {
        tHienTai = 1.0;
        vongHienTai = 1;
        omegaQuanTinh = 0;
        dangChay = false;
        capNhatToanBo(false);
      },
      ghimBongMa: ghimBongMa,
      thaBanhDa: function (omegaDeg) {
        if (quanTinhBat) {
          omegaQuanTinh = omegaDeg;
        }
        if (deMayBat && Math.abs(omegaDeg) > 50) {
          omegaDeMay = omegaDeg * (Math.PI / 180);
        }
      },
    };

    window.HLTuongTac.khoiTaoTuongTac(svgEl, contextTuongTac);

    khoiTaoDieuKhien();
    khoiTaoTabNhienLieu();
    khoiTaoTabHoc();
    khoiTaoTabCongThuc();
    capNhatTenTuDanhMuc();
    caiDatHookKiemThu();

    // Theo dõi thay đổi kích thước khung hình
    if (typeof ResizeObserver !== 'undefined') {
      var ro = new ResizeObserver(function (entries) {
        for (var i = 0; i < entries.length; i++) {
          var cr = entries[i].contentRect;
          if (cr.width > 50 && cr.height > 50) {
            W_hien_tai = Math.round(cr.width);
            H_hien_tai = Math.round(cr.height);
            capNhatToanBo(true);
          }
        }
      });
      var wrapEl = document.getElementById('hl-khung-svg-wrapper') || svgEl;
      if (wrapEl) ro.observe(wrapEl);
    }
    window.addEventListener('resize', function () { capNhatToanBo(true); });

    if (window.HLKhung && window.HLKhung.thietLapKhung) {
      window.HLKhung.thietLapKhung({
        id: 'dong-co-nhiet',
        ten: 'Động cơ nhiệt 4 kỳ',
        laySVG: function () { return svgEl; },
        onDoiTheme: function () { capNhatToanBo(true); }
      });
    }

    capNhatToanBo(true);
    requestAnimationFrame(vongLap);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', khoiTao);
  } else {
    khoiTao();
  }
})();
