import test from 'node:test';
import assert from 'node:assert/strict';
import HL_TocDo from '../../frontend/hoa-hoc/hoa-ly/toc-do-phan-ung/mo-hinh.js';

test('HL_TocDo: Tỉ số tốc độ và hệ số Van \'t Hoff (Ea = 50 kJ/mol, A = 1.0e10)', () => {
  const Ea = 50.0;
  const A = 1.0e10;

  const k_298 = HL_TocDo.kTheoArrhenius(A, Ea, 298.15);
  const k_308 = HL_TocDo.kTheoArrhenius(A, Ea, 308.15);
  const k_348 = HL_TocDo.kTheoArrhenius(A, Ea, 348.15);

  // k(308.15)/k(298.15) = 1.9243
  const tiLe_308_298 = k_308 / k_298;
  assert.ok(Math.abs(tiLe_308_298 - 1.9243) < 0.001, `tiLe_308_298 = ${tiLe_308_298}`);

  // k(348.15)/k(298.15) = 18.114
  const tiLe_348_298 = k_348 / k_298;
  assert.ok(Math.abs(tiLe_348_298 - 18.114) < 0.01, `tiLe_348_298 = ${tiLe_348_298}`);

  // gamma xấp xỉ theo D3: exp(10*Ea/(R*T^2)) ở 298.15 K = 1.9670
  const gamma = HL_TocDo.heSoVantHoff(Ea, 298.15);
  assert.ok(Math.abs(gamma - 1.9670) < 0.001, `gamma = ${gamma}`);

  // Ea để gamma = 2 ở 298.15 K = 51.23 kJ/mol
  const Ea_gamma2 = HL_TocDo.eaTuGamma(2.0, 298.15);
  assert.ok(Math.abs(Ea_gamma2 - 51.23) < 0.02, `Ea_gamma2 = ${Ea_gamma2}`);
});

test('HL_TocDo: Bảng hằng số tốc độ k và khớp Arrhenius', () => {
  const Ea = 50.0;
  const A = 1.0e10;

  // Bảng giá trị chuẩn
  const bangK = [
    { T: 298.15, k_exp: 17.393 },
    { T: 308.15, k_exp: 33.469 },
    { T: 318.15, k_exp: 61.807 },
    { T: 328.15, k_exp: 109.95 }
  ];

  const diemKhop = [];
  for (const item of bangK) {
    const k = HL_TocDo.kTheoArrhenius(A, Ea, item.T);
    assert.ok(Math.abs(k - item.k_exp) < 0.02, `k(${item.T}) = ${k}, kỳ vọng ${item.k_exp}`);
    diemKhop.push({ T: item.T, k: k });
  }

  // Khớp Arrhenius từ 4 điểm này phải ra lại Ea = 50.00 kJ/mol (sai số 1e-6) và A = 1.0e10
  const kqKhop = HL_TocDo.khopArrhenius(diemKhop);
  assert.ok(kqKhop !== null);
  assert.ok(Math.abs(kqKhop.Ea_kJ - 50.0) < 1e-5, `Ea khop = ${kqKhop.Ea_kJ}`);
  assert.ok(Math.abs(kqKhop.A - 1.0e10) / 1.0e10 < 1e-4, `A khop = ${kqKhop.A}`);
});

test('HL_TocDo: Xúc tác và Bậc phản ứng 0, 1, 2', () => {
  // Xúc tác: giảm 20 kJ/mol ở 298.15 K -> tăng 3190.4 lần
  const tangXT = HL_TocDo.heSoTangXucTac(20.0, 298.15);
  assert.ok(Math.abs(tangXT - 3190.4) < 0.5, `tangXT = ${tangXT}`);

  // Bậc 1: t1/2 = 30 s -> k = 0.023105 s⁻¹; C(60 s)/C0 = 0.25
  const t_half = 30;
  const k1 = Math.log(2) / t_half;
  assert.ok(Math.abs(k1 - 0.023105) < 1e-5, `k1 = ${k1}`);
  const c60 = HL_TocDo.nongDoBac1(1.0, k1, 60);
  assert.ok(Math.abs(c60 - 0.25) < 1e-5, `c60 = ${c60}`);

  // Bậc 2: C0 = 0.1 M, k = 0.5 L/(mol·s): C(20 s) = 0.05 M, t1/2 = 20 s
  const c2_20 = HL_TocDo.nongDoBac2(0.1, 0.5, 20);
  assert.ok(Math.abs(c2_20 - 0.05) < 1e-5, `c2_20 = ${c2_20}`);
  const t2_half = HL_TocDo.thoiGianBanHuyBac2(0.1, 0.5);
  assert.equal(t2_half, 20);

  // Bậc 0: C = C0 - k*t chặn ở 0
  assert.equal(HL_TocDo.nongDoBac0(1.0, 0.1, 5), 0.5);
  assert.equal(HL_TocDo.nongDoBac0(1.0, 0.1, 15), 0);
});

test('HL_TocDo: Phân bố năng lượng 3D và thừa số Arrhenius', () => {
  // x = Ea/(RT) (298.15 K) = 20.170
  // exp(-x) = 1.7393e-9; tỉ lệ 3D = 9.028e-9
  const res = HL_TocDo.phanBoNangLuong3D(50.0, 298.15);
  assert.ok(Math.abs(res.x - 20.170) < 0.01, `x = ${res.x}`);
  assert.ok(Math.abs(res.exp_am_x - 1.7393e-9) / 1.7393e-9 < 0.002, `exp(-x) = ${res.exp_am_x}`);
  assert.ok(Math.abs(res.tiLe3D - 9.028e-9) / 9.028e-9 < 0.01, `tiLe3D = ${res.tiLe3D}`);
});
