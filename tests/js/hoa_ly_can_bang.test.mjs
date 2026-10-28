import test from 'node:test';
import assert from 'node:assert/strict';
import HL_CanBang from '../../frontend/hoa-hoc/hoa-ly/can-bang-le-chatelier/mo-hinh.js';

test('HL_CanBang: Kc theo phương trình van \'t Hoff', () => {
  // N2O4 ⇌ 2NO2: Kc_ref = 4.61e-3 ở 298.15 K, dH = 57.2 kJ/mol
  const dH = 57.2;
  const T_ref = 298.15;
  const Kc_ref = 4.61e-3;

  // 273.15 K -> Kc = 5.5787e-4
  const kc_273 = HL_CanBang.kcTheoT(273.15, dH, T_ref, Kc_ref);
  assert.ok(Math.abs(kc_273 - 5.5787e-4) / 5.5787e-4 < 0.001, `kc_273 = ${kc_273}`);

  // 323.15 K -> Kc = 2.7476e-2
  const kc_323 = HL_CanBang.kcTheoT(323.15, dH, T_ref, Kc_ref);
  assert.ok(Math.abs(kc_323 - 2.7476e-2) / 2.7476e-2 < 0.001, `kc_323 = ${kc_323}`);

  // 373.15 K -> Kc = 0.47625
  const kc_373 = HL_CanBang.kcTheoT(373.15, dH, T_ref, Kc_ref);
  assert.ok(Math.abs(kc_373 - 0.47625) / 0.47625 < 0.001, `kc_373 = ${kc_373}`);
});

test('HL_CanBang: Cân bằng ban đầu và nén 1/2 thể tích (hệ N2O4 ⇌ 2NO2)', () => {
  const Kc = 0.00461;
  const cA0 = 0.0400; // [A]0 = 0.0400 M
  const cb1 = HL_CanBang.canBangPhanLi(cA0, 0, Kc);

  // x = 0.006238, [N2O4] = 0.033762, [NO2] = 0.012476, alpha = 0.1559, Q = K
  assert.ok(Math.abs(cb1.x - 0.006238) < 1e-5, `x = ${cb1.x}`);
  assert.ok(Math.abs(cb1.cA - 0.033762) < 1e-5, `[N2O4] = ${cb1.cA}`);
  assert.ok(Math.abs(cb1.cB - 0.012476) < 1e-5, `[NO2] = ${cb1.cB}`);
  assert.ok(Math.abs(cb1.alpha - 0.1559) < 1e-4, `alpha = ${cb1.alpha}`);
  assert.ok(Math.abs(cb1.Q - Kc) / Kc < 1e-6, `Q = ${cb1.Q}`);

  // Nén còn 1/2 thể tích (s = 2):
  // Nồng độ tức thì nhân 2
  const cA_nen = cb1.cA * 2;
  const cB_nen = cb1.cB * 2;
  const Q_nen = Math.pow(cB_nen, 2) / cA_nen;

  // Q = 9.22e-3 = 2*K > K -> chiều nghịch của phân ly
  assert.ok(Math.abs(Q_nen - 9.22e-3) / 9.22e-3 < 0.005, `Q_nen = ${Q_nen}`);
  assert.equal(HL_CanBang.chieu(Q_nen, Kc), 'nghịch');

  // Cân bằng mới: [N2O4] = 0.070957, [NO2] = 0.018086
  // (cB_nen - 2y)^2 / (cA_nen + y) = Kc
  // Tương đương phản ứng phân ly bắt đầu từ cA_nen và cB_nen
  // 4y^2 - (4*cB_nen + Kc)*y + (cB_nen^2 - Kc*cA_nen) = 0
  const A = 4;
  const B = 4 * cB_nen + Kc;
  const C = Math.pow(cB_nen, 2) - Kc * cA_nen;
  const delta = B * B - 4 * A * C;
  const y = (-B + Math.sqrt(delta)) / (2 * A); // y âm vì chuyển dịch nghịch
  const cA_moi = cA_nen - y;
  const cB_moi = cB_nen + 2 * y;

  assert.ok(Math.abs(cA_moi - 0.070957) < 1e-4, `[N2O4] moi = ${cA_moi}`);
  assert.ok(Math.abs(cB_moi - 0.018086) < 1e-4, `[NO2] moi = ${cB_moi}`);

  // Kiểm tra chuỗi: 0.0125 < 0.0181 < 0.0250
  assert.ok(cb1.cB < cB_moi, '0.0125 < 0.0181');
  assert.ok(cB_moi < cB_nen, '0.0181 < 0.0250');
});

test('HL_CanBang: Kp, Haber và H2 + I2', () => {
  // Kp = Kc * (RT)^delta_n ở 298.15 K = 0.11428 bar (delta_n = 1)
  const Kp = HL_CanBang.kpTuKc(0.00461, 298.15, 1);
  assert.ok(Math.abs(Kp - 0.11428) < 0.0005, `Kp = ${Kp}`);

  // Haber: dH = -91.8 kJ, dS = -198.1 J/K
  // K°(298.15) = 5.435e5; K°(723.15) = 1.92e-4
  const K_haber_298 = HL_CanBang.kpHaber(298.15, -91.8, -198.1);
  assert.ok(Math.abs(K_haber_298 - 5.435e5) / 5.435e5 < 0.01, `K_haber_298 = ${K_haber_298}`);

  const K_haber_723 = HL_CanBang.kpHaber(723.15, -91.8, -198.1);
  assert.ok(Math.abs(K_haber_723 - 1.92e-4) / 1.92e-4 < 0.01, `K_haber_723 = ${K_haber_723}`);

  // H2 + I2 ⇌ 2HI (delta_n = 0): nén hay giãn đều có Q' = Q
  const Q_hi_nen = HL_CanBang.qSauNen(54.0, 2.0, 0);
  assert.equal(Q_hi_nen, 54.0);
  const Q_hi_gian = HL_CanBang.qSauNen(54.0, 0.5, 0);
  assert.equal(Q_hi_gian, 54.0);
});

test('HL_CanBang: Xúc tác không đổi Kc và tích phân ODE hội tụ về cân bằng', () => {
  // Xúc tác: Kc không đổi
  assert.equal(HL_CanBang.tiLeKcXucTac(), 1.0);

  // Tích phân số ODE: xuất phát từ trạng thái cA = 0.04, cB = 0, sau nhiều bước hội tụ về canBangPhanLi
  const Kc = 0.00461;
  const cbLyThuyet = HL_CanBang.canBangPhanLi(0.04, 0, Kc);

  let cA = 0.04;
  let cB = 0;
  const dt = 0.05;
  for (let step = 0; step < 500; step++) {
    const res = HL_CanBang.buocTichPhan(cA, cB, Kc, dt, 1.0);
    cA = res.cA;
    cB = res.cB;
  }

  assert.ok(Math.abs(cA - cbLyThuyet.cA) < 1e-4, `cA = ${cA}, kỳ vọng ${cbLyThuyet.cA}`);
  assert.ok(Math.abs(cB - cbLyThuyet.cB) < 1e-4, `cB = ${cB}, kỳ vọng ${cbLyThuyet.cB}`);
});
