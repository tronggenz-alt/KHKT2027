import test from 'node:test';
import assert from 'node:assert/strict';
import HLDongLuc from '../../frontend/hoa-hoc/hoa-ly/dong-co-nhiet/dong-luc.js';
import HLNhietDong from '../../frontend/hoa-hoc/hoa-ly/dong-co-nhiet/nhiet-dong.js';
import HLCoCau from '../../frontend/hoa-hoc/hoa-ly/dong-co-nhiet/co-cau.js';

test('HLDongLuc: Hệ bảo toàn khi không nhiên liệu, ma sát = 0, tải = 0', () => {
  const ts = { r: 10, T1: 300, p1: 100, q_in: 0, gamma: 1.4, B: 80, S: 80, L: 140 };
  const hh = HLCoCau.hinhHoc(ts);
  const I = 0.05;
  const mdl = HLDongLuc.taoMoHinhDongLuc({ I });

  const DEG2RAD = Math.PI / 180;
  const layTrangThaiKhi = (phiDeg) => {
    // Không nhiên liệu: nén đoạn nhiệt và giãn nở đoạn nhiệt theo cùng một đường p*V^gamma = const
    const V = HLCoCau.V(phiDeg, ts);
    const p_kPa = 100 * Math.pow(hh.V1_cm3 / V, 1.4);
    const ds_dtheta_m = HLCoCau.ds_dtheta(phiDeg, ts) * 1e-3; // m/rad
    return {
      p_Pa: p_kPa * 1000,
      A_m2: hh.A_mm2 * 1e-6,
      ds_dtheta_m: ds_dtheta_m,
    };
  };

  let phi = 180; // Bắt đầu từ ĐCD
  let omega = 1200 * (2 * Math.PI / 60); // 1200 rpm
  const omegaDau = omega;

  // Chạy 1 chu trình 720°
  const dtKhung = 0.0005; // 0.5 ms
  while (phi < 180 + 720) {
    const res = mdl.buocDeMay(phi, omega, dtKhung, layTrangThaiKhi, { tai: 0, c0: 0, c1: 0, p_atm: 100000 });
    phi = res.phi;
    omega = res.omega_rad_s;
  }

  // Tốc độ góc sau 1 chu trình bảo toàn năng lượng
  const saiSo = Math.abs(omega - omegaDau) / omegaDau;
  assert.ok(saiSo < 0.005, `omega kết thúc = ${omega}, omega đầu = ${omegaDau}, sai số = ${saiSo}`);
});

test('HLDongLuc: Có nhiên liệu sinh công tăng động năng đúng m·w', () => {
  const ts = { r: 10, T1: 300, p1: 100, q_in: 1500, gamma: 1.4, B: 80, S: 80, L: 140 };
  const hh = HLCoCau.hinhHoc(ts);
  const tinhND = HLNhietDong.otto(ts);
  const I = 0.05;
  const mdl = HLDongLuc.taoMoHinhDongLuc({ I });

  const layTrangThaiKhi = (phiDeg) => {
    const phi = (phiDeg % 720 + 720) % 720;
    const V = HLCoCau.V(phi, ts);
    let p_bar = 1.0;
    if (phi < 180) {
      p_bar = 1.0;
    } else if (phi < 360) {
      // nén
      p_bar = 1.0 * Math.pow(hh.V1_cm3 / V, 1.4);
    } else if (phi < 540) {
      // giãn nở
      p_bar = (tinhND.p3 / 100) * Math.pow(hh.Vc_cm3 / V, 1.4);
    } else {
      p_bar = 1.0;
    }
    const ds_dtheta_m = HLCoCau.ds_dtheta(phi, ts) * 1e-3;
    return {
      p_Pa: p_bar * 1e5,
      A_m2: hh.A_mm2 * 1e-6,
      ds_dtheta_m: ds_dtheta_m,
    };
  };

  let phi = 0;
  let omega = 1000 * (2 * Math.PI / 60);
  const E_dau = 0.5 * I * omega * omega;

  const dtKhung = 0.0005;
  while (phi < 720) {
    const res = mdl.buocDeMay(phi, omega, dtKhung, layTrangThaiKhi, { tai: 0, c0: 0, c1: 0, p_atm: 100000 });
    phi = res.phi;
    omega = res.omega_rad_s;
  }

  const E_cuoi = 0.5 * I * omega * omega;
  const deltaE = E_cuoi - E_dau;
  const W_mongDoi = tinhND.W_J; // ≈ 468 J

  const saiSo = Math.abs(deltaE - W_mongDoi) / W_mongDoi;
  assert.ok(saiSo < 0.03, `deltaE = ${deltaE} J != W_mongDoi ${W_mongDoi} J, sai số = ${saiSo}`);
});
