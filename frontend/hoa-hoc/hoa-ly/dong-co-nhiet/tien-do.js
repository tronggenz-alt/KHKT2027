/**
 * tien-do.js - Quản lý tham số chu trình t ∈ [0, 6), ánh xạ góc trục khuỷu φ,
 * điểm p–V, phát hiện sự kiện và quy tắc liên tục.
 * Chuẩn UMD: chạy trên trình duyệt (window.HLTienDo) và Node.js (module.exports).
 * Không chạm DOM.
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) {
    var coCau = null;
    var nhietDong = null;
    try {
      coCau = require('./co-cau.js');
      nhietDong = require('./nhiet-dong.js');
    } catch (e) {}
    module.exports = factory(coCau, nhietDong);
  } else {
    root.HLTienDo = factory(root.HLCoCau, root.HLNhietDong);
  }
})(typeof globalThis !== 'undefined' ? globalThis : this, function (HLCoCau, HLNhietDong) {
  'use strict';

  var KY_TEN = [
    'Kỳ 1 · Nạp',
    'Kỳ 2 · Nén',
    'Đánh lửa',
    'Kỳ 3 · Nổ – sinh công',
    'Mở van xả',
    'Kỳ 4 · Xả',
  ];

  var QUA_TRINH_TEN = [
    'Nạp hỗn hợp nhiên liệu – không khí (p = const)',
    'Nén đoạn nhiệt (Q = 0)',
    'Cháy đẳng tích (V = const)',
    'Giãn nở đoạn nhiệt (Q = 0)',
    'Thải nhiệt đẳng tích (V = const)',
    'Đẩy khí thải ra ngoài (p = const)',
  ];

  /**
   * Tính trạng thái tức thời { phi, V, p, T, doan, f, ky, tenKy, tenQuaTrinh, deltaU, A, Q }
   * @param {number} t - tiến độ trong chu trình [0, 6)
   * @param {Object} ts - thông số chu trình
   */
  function tuTienDo(t, ts, ndLib, ccLib) {
    var ND = ndLib || HLNhietDong;
    var CC = ccLib || HLCoCau;

    // Chuẩn hoá t về [0, 6)
    var t_norm = (t % 6 + 6) % 6;
    var doan = Math.floor(t_norm);
    var f = t_norm - doan;

    var tinh = ND ? ND.otto(ts) : null;
    var hh = CC ? CC.hinhHoc(ts) : null;

    var Vc = hh ? hh.Vc_cm3 : 44.68;
    var V1 = hh ? hh.V1_cm3 : 446.80;
    var Vd = hh ? hh.Vd_cm3 : 402.12;

    var p1 = (tinh ? tinh.p1 : 100) / 100; // bar
    var T1 = tinh ? tinh.T1 : 300; // K
    var p2 = (tinh ? tinh.p2 : 2511.89) / 100; // bar
    var T2 = tinh ? tinh.T2 : 753.57; // K
    var p3 = (tinh ? tinh.p3 : 9480.53) / 100; // bar
    var T3 = tinh ? tinh.T3 : 2844.16; // K
    var p4 = (tinh ? tinh.p4 : 377.43) / 100; // bar
    var T4 = tinh ? tinh.T4 : 1132.28; // K

    var gamma = ts.gamma || 1.4;
    var cv = tinh ? tinh.cv : (0.287 / (gamma - 1));
    var m_kg = tinh ? tinh.m_kg : 0.0005189; // kg

    var phi = 0;
    var V = Vc;
    var p = p1;
    var T = T1;

    var deltaU = 0; // J
    var A = 0; // J (công khí nhận)
    var Q = 0; // J (nhiệt khí nhận)

    switch (doan) {
      case 0:
        // Đoạn 0: Nạp 0->1, phi: 0° -> 180°
        phi = f * 180;
        V = CC ? CC.V(phi, ts) : (Vc + f * Vd);
        p = p1;
        T = T1;
        deltaU = 0;
        A = 0;
        Q = 0;
        break;

      case 1:
        // Đoạn 1: Nén đoạn nhiệt 1->2, phi: 180° -> 360°
        phi = 180 + f * 180;
        V = CC ? CC.V(phi, ts) : (V1 - f * Vd);
        p = p1 * Math.pow(V1 / V, gamma);
        T = T1 * Math.pow(V1 / V, gamma - 1);
        deltaU = m_kg * cv * (T - T1) * 1000; // J
        Q = 0;
        A = deltaU; // Khí nhận công: A > 0
        break;

      case 2:
        // Đoạn 2: Cháy đẳng tích 2->3, phi: giữ 360° (ĐCT)
        phi = 360;
        V = Vc;
        T = T2 + f * (T3 - T2);
        p = p2 * (T / T2);
        deltaU = m_kg * cv * (T - T2) * 1000;
        A = 0;
        Q = deltaU;
        break;

      case 3:
        // Đoạn 3: Giãn nở đoạn nhiệt 3->4, phi: 360° -> 540°
        phi = 360 + f * 180;
        V = CC ? CC.V(phi, ts) : (Vc + f * Vd);
        p = p3 * Math.pow(Vc / V, gamma);
        T = T3 * Math.pow(Vc / V, gamma - 1);
        deltaU = m_kg * cv * (T - T3) * 1000;
        Q = 0;
        A = deltaU; // T < T3 => deltaU < 0, A < 0 (khí sinh công)
        break;

      case 4:
        // Đoạn 4: Mở van xả, thải nhiệt đẳng tích 4->1, phi: giữ 540° (ĐCD)
        phi = 540;
        V = V1;
        T = T4 - f * (T4 - T1);
        p = p4 - f * (p4 - p1);
        deltaU = m_kg * cv * (T - T4) * 1000;
        A = 0;
        Q = deltaU; // Thải nhiệt: Q < 0
        break;

      case 5:
        // Đoạn 5: Xả 1->0, phi: 540° -> 720°
        phi = 540 + f * 180;
        V = CC ? CC.V(phi, ts) : (V1 - f * Vd);
        p = p1;
        T = T1;
        deltaU = 0;
        A = 0;
        Q = 0;
        break;
    }

    var kyIndex = doan;
    if (doan === 0 || doan === 1) kyIndex = doan;
    else if (doan === 2) kyIndex = 2; // Đánh lửa
    else if (doan === 3) kyIndex = 3;
    else if (doan === 4) kyIndex = 4; // Mở van xả
    else if (doan === 5) kyIndex = 5;

    return {
      t: t_norm,
      phi: phi,
      V: V,
      p: p, // bar
      p_kPa: p * 100,
      T: T, // K
      doan: doan,
      f: f,
      ky: kyIndex,
      tenKy: KY_TEN[kyIndex],
      tenQuaTrinh: QUA_TRINH_TEN[kyIndex],
      deltaU: deltaU,
      A: A,
      Q: Q,
    };
  }

  /**
   * Bước quay góc trục khuỷu dPhi (độ).
   * Tự động bước qua các đoạn đẳng tích (đoạn 2 tại 360°, đoạn 4 tại 540°)
   * và phát sinh sự kiện.
   */
  function buocGoc(tHienTai, dPhi, ts) {
    var suKien = [];
    var t = tHienTai;
    var vongDelta = 0;

    if (Math.abs(dPhi) < 1e-9) {
      return { t: t, vongDelta: 0, suKien: suKien };
    }

    var chieuTien = dPhi > 0;
    var gocConLai = Math.abs(dPhi);

    while (gocConLai > 1e-9) {
      var doan = Math.floor(t % 6);
      var f = t - Math.floor(t);

      if (chieuTien) {
        // Chiều tiến
        if (doan === 2) {
          // Đang ở đoạn 2 (đánh lửa) -> chuyển sang đoạn 3
          t = 3;
          suKien.push('danh_lua');
          continue;
        }
        if (doan === 4) {
          // Đang ở đoạn 4 (thải nhiệt) -> chuyển sang đoạn 5
          t = 5;
          suKien.push('mo_van_xa');
          continue;
        }

        // Đoạn liên tục theo góc (0, 1, 3, 5): mỗi đoạn 180°
        var fConLai = 1 - f;
        var gocCoTheChay = fConLai * 180;

        if (gocConLai >= gocCoTheChay - 1e-9) {
          // Chạy hết đoạn này
          gocConLai -= gocCoTheChay;
          var doanSau = doan + 1;
          if (doanSau === 2) {
            // Chạm 360° -> phát sự kiện đánh lửa và nhảy thẳng qua đoạn 2 sang đoạn 3
            suKien.push('danh_lua');
            t = 3;
          } else if (doanSau === 4) {
            // Chạm 540° -> phát sự kiện mở van xả và nhảy thẳng qua đoạn 4 sang đoạn 5
            suKien.push('mo_van_xa');
            t = 5;
          } else if (doanSau >= 6) {
            // Hết chu trình (720° -> 0°)
            t = 0;
            vongDelta += 1;
            suKien.push('het_chu_trinh');
          } else {
            t = doanSau;
          }
        } else {
          // Chưa chạy hết đoạn
          t += gocConLai / 180;
          gocConLai = 0;
        }
      } else {
        // Chiều lùi (dPhi < 0)
        if (doan === 2) {
          t = 2; // về đầu đoạn 2
          t = 1.999999;
          suKien.push('danh_lua_nguoc');
          continue;
        }
        if (doan === 4) {
          t = 3.999999;
          suKien.push('mo_van_xa_nguoc');
          continue;
        }

        var fCoTheLui = f;
        var gocLuiToiDa = fCoTheLui * 180;

        if (gocConLai >= gocLuiToiDa - 1e-9) {
          gocConLai -= gocLuiToiDa;
          var doanTruoc = doan - 1;
          if (doanTruoc === 2) {
            // Lùi qua đoạn 2 -> nhảy về cuối đoạn 1
            suKien.push('danh_lua_nguoc');
            t = 1.999999;
          } else if (doanTruoc === 4) {
            suKien.push('mo_van_xa_nguoc');
            t = 3.999999;
          } else if (doanTruoc < 0) {
            t = 5.999999;
            vongDelta -= 1;
          } else {
            t = doanTruoc + 0.999999;
          }
        } else {
          t -= gocConLai / 180;
          gocConLai = 0;
        }
      }
    }

    t = (t % 6 + 6) % 6;
    return {
      t: t,
      vongDelta: vongDelta,
      suKien: suKien,
    };
  }

  /**
   * Tìm tiến độ t từ điểm chuột chiếu lên đường chu trình p–V.
   * Để chống nhảy nhầm (đặc biệt giữa kỳ nạp 0 và xả 5 trùng nhau),
   * chỉ tìm kiếm trong cửa sổ [tHienTai - 0.75, tHienTai + 0.75].
   */
  function tienDoTuDiemPV(V_target, p_target, tHienTai, ts, toaDoBienDoi, ndLib, ccLib) {
    var minT = tHienTai - 0.75;
    var maxT = tHienTai + 0.75;

    var bestT = tHienTai;
    var bestDistSq = Infinity;

    // Quét mẫu 60 điểm trong cửa sổ [minT, maxT]
    var buoc = (maxT - minT) / 60;
    for (var i = 0; i <= 60; i++) {
      var candT = minT + i * buoc;
      var st = tuTienDo(candT, ts, ndLib, ccLib);

      // Tính khoảng cách (chuẩn hoá theo toạ độ đồ thị nếu có)
      var dV = (st.V - V_target);
      var dp = (st.p - p_target);

      var distSq = dV * dV + dp * dp * 10;
      if (toaDoBienDoi && typeof toaDoBienDoi === 'function') {
        var ptCand = toaDoBienDoi(st.V, st.p);
        var ptTarget = toaDoBienDoi(V_target, p_target);
        var dx = ptCand.x - ptTarget.x;
        var dy = ptCand.y - ptTarget.y;
        distSq = dx * dx + dy * dy;
      }

      if (distSq < bestDistSq) {
        bestDistSq = distSq;
        bestT = candT;
      }
    }

    return (bestT % 6 + 6) % 6;
  }

  /**
   * Chuyển đổi vị trí thể tích V thành góc trục khuỷu phi trong nửa vòng hiện tại
   */
  function gocTuTheTich(V_target, phiHienTai, ts, ccLib) {
    var CC = ccLib || HLCoCau;
    var theta = CC ? CC.thetaTuV(V_target, ts) : 0; // [0, 180]
    var phiMod720 = (phiHienTai % 720 + 720) % 720;
    var nuaVong = Math.floor(phiMod720 / 180); // 0, 1, 2, 3

    var phiMoi = 0;
    if (nuaVong === 0) {
      // 0° -> 180°: pít-tông đi xuống (theta tăng)
      phiMoi = theta;
    } else if (nuaVong === 1) {
      // 180° -> 360°: pít-tông đi lên (theta giảm về 0)
      phiMoi = 360 - theta;
    } else if (nuaVong === 2) {
      // 360° -> 540°: pít-tông đi xuống
      phiMoi = 360 + theta;
    } else {
      // 540° -> 720°: pít-tông đi lên
      phiMoi = 720 - theta;
    }

    return phiMoi;
  }

  return {
    tuTienDo: tuTienDo,
    buocGoc: buocGoc,
    tienDoTuDiemPV: tienDoTuDiemPV,
    gocTuTheTich: gocTuTheTich,
    KY_TEN: KY_TEN,
    QUA_TRINH_TEN: QUA_TRINH_TEN,
  };
});
