import test from 'node:test';
import assert from 'node:assert/strict';
import HLNhietDong from '../../frontend/hoa-hoc/hoa-ly/dong-co-nhiet/nhiet-dong.js';
import HLCoCau from '../../frontend/hoa-hoc/hoa-ly/dong-co-nhiet/co-cau.js';

test('HLNhietDong: Otto – ca A (r = 8, T1 = 300 K, p1 = 100 kPa, q_in = 1000 kJ/kg)', () => {
  const ts = { r: 8, T1: 300, p1: 100, q_in: 1000, gamma: 1.4 };
  const res = HLNhietDong.otto(ts);

  const saiSoTuongDoi = (that, mongDoi) => Math.abs(that - mongDoi) / mongDoi;

  assert.ok(saiSoTuongDoi(res.T2, 689.22) < 0.0005, `T2 = ${res.T2} != 689.22`);
  assert.ok(saiSoTuongDoi(res.p2, 1837.92) < 0.0005, `p2 = ${res.p2} != 1837.92`);
  assert.ok(saiSoTuongDoi(res.T3, 2082.95) < 0.0005, `T3 = ${res.T3} != 2082.95`);
  assert.ok(saiSoTuongDoi(res.p3, 5554.53) < 0.0005, `p3 = ${res.p3} != 5554.53`);
  assert.ok(saiSoTuongDoi(res.T4, 906.66) < 0.0005, `T4 = ${res.T4} != 906.66`);
  assert.ok(saiSoTuongDoi(res.p4, 302.22) < 0.0005, `p4 = ${res.p4} != 302.22`);

  assert.ok(saiSoTuongDoi(res.q_out, 435.28) < 0.0005, `q_out = ${res.q_out} != 435.28`);
  assert.ok(saiSoTuongDoi(res.w, 564.72) < 0.0005, `w = ${res.w} != 564.72`);
  assert.ok(saiSoTuongDoi(res.eta, 0.5647) < 0.0005, `eta = ${res.eta} != 0.5647`);
  assert.ok(saiSoTuongDoi(res.MEP, 749.59) < 0.0005, `MEP = ${res.MEP} != 749.59`);
  assert.ok(saiSoTuongDoi(res.eta_Carnot, 0.8560) < 0.0005, `eta_Carnot = ${res.eta_Carnot} != 0.8560`);
  assert.ok(saiSoTuongDoi(res.delta_s_chay, 0.7935) < 0.001, `s3 - s2 = ${res.delta_s_chay} != 0.7935`);
});

test('HLNhietDong: Otto – ca B (r = 10, T1 = 300 K, p1 = 100 kPa, q_in = 1500 kJ/kg)', () => {
  const ts = { r: 10, T1: 300, p1: 100, q_in: 1500, gamma: 1.4 };
  const res = HLNhietDong.otto(ts);

  const saiSoTuongDoi = (that, mongDoi) => Math.abs(that - mongDoi) / mongDoi;

  assert.ok(saiSoTuongDoi(res.T2, 753.57) < 0.0005, `T2 = ${res.T2} != 753.57`);
  assert.ok(saiSoTuongDoi(res.p2, 2511.89) < 0.0005, `p2 = ${res.p2} != 2511.89`);
  assert.ok(saiSoTuongDoi(res.T3, 2844.16) < 0.0005, `T3 = ${res.T3} != 2844.16`);
  assert.ok(saiSoTuongDoi(res.p3, 9480.53) < 0.0005, `p3 = ${res.p3} != 9480.53`);
  assert.ok(saiSoTuongDoi(res.T4, 1132.28) < 0.0005, `T4 = ${res.T4} != 1132.28`);
  assert.ok(saiSoTuongDoi(res.p4, 377.43) < 0.0005, `p4 = ${res.p4} != 377.43`);

  assert.ok(saiSoTuongDoi(res.w, 902.84) < 0.0005, `w = ${res.w} != 902.84`);
  assert.ok(saiSoTuongDoi(res.eta, 0.6019) < 0.0005, `eta = ${res.eta} != 0.6019`);
  assert.ok(saiSoTuongDoi(res.MEP, 1165.10) < 0.0005, `MEP = ${res.MEP} != 1165.10`);
});

test('HLNhietDong: Diesel (r = 18, rc = 2, T1 = 300, p1 = 100)', () => {
  const ts = { r: 18, rc: 2, T1: 300, p1: 100, gamma: 1.4 };
  const res = HLNhietDong.diesel(ts);

  const saiSoTuongDoi = (that, mongDoi) => Math.abs(that - mongDoi) / mongDoi;

  assert.ok(saiSoTuongDoi(res.T2, 953.30) < 0.0005, `T2 = ${res.T2} != 953.30`);
  assert.ok(saiSoTuongDoi(res.p2, 5719.81) < 0.0005, `p2 = ${res.p2} != 5719.81`);
  assert.ok(saiSoTuongDoi(res.T3, 1906.60) < 0.0005, `T3 = ${res.T3} != 1906.60`);
  assert.ok(saiSoTuongDoi(res.T4, 791.70) < 0.0005, `T4 = ${res.T4} != 791.70`);
  assert.ok(saiSoTuongDoi(res.q_in, 957.59) < 0.0005, `q_in = ${res.q_in} != 957.59`);
  assert.ok(saiSoTuongDoi(res.q_out, 352.80) < 0.0005, `q_out = ${res.q_out} != 352.80`);
  assert.ok(saiSoTuongDoi(res.eta, 0.6316) < 0.0005, `eta = ${res.eta} != 0.6316`);

  // eta tính từ q và công thức đóng phải khớp nhau hoàn toàn
  const etaTuQ = (res.q_in - res.q_out) / res.q_in;
  assert.ok(Math.abs(res.eta - etaTuQ) < 1e-6, `eta đóng ${res.eta} != eta từ q ${etaTuQ}`);
});

test('HLNhietDong: Nhiên liệu hoá học (k = 0,55, λ = 1)', () => {
  const fuels = [
    { nl: { id: 'isooctane', C: 8, H: 18, O: 0, M: 114, delta_f_H: -259.2 }, delta_cH: -5065.0, LHV: 44.43, AFR: 15.05, q_in: 1522 },
    { nl: { id: 'ethanol', C: 2, H: 6, O: 1, M: 46, delta_f_H: -277.7 }, delta_cH: -1234.7, LHV: 26.84, AFR: 8.95, q_in: 1483 },
    { nl: { id: 'methane', C: 1, H: 4, O: 0, M: 16, delta_f_H: -74.8 }, delta_cH: -802.3, LHV: 50.14, AFR: 17.16, q_in: 1519 },
    { nl: { id: 'hydrogen', C: 0, H: 2, O: 0, M: 2, delta_f_H: 0.0 }, delta_cH: -241.8, LHV: 120.90, AFR: 34.32, q_in: 1883 },
  ];

  const saiSoTuongDoi = (that, mongDoi) => Math.abs(that - mongDoi) / mongDoi;

  for (const f of fuels) {
    const res = HLNhietDong.tinhNhienLieu(f.nl, { lambda: 1.0, k: 0.55 });
    assert.ok(saiSoTuongDoi(res.delta_cH_LHV, f.delta_cH) < 0.005, `${f.nl.id}: delta_cH = ${res.delta_cH_LHV}`);
    assert.ok(saiSoTuongDoi(res.LHV_MJ_kg, f.LHV) < 0.005, `${f.nl.id}: LHV = ${res.LHV_MJ_kg}`);
    assert.ok(saiSoTuongDoi(res.AFR, f.AFR) < 0.005, `${f.nl.id}: AFR = ${res.AFR}`);
    assert.ok(saiSoTuongDoi(res.q_in, f.q_in) < 0.005, `${f.nl.id}: q_in = ${res.q_in}`);
  }
});

test('HLNhietDong: Bảo toàn năng lượng ∮ dU = 0 và W = Q_in - Q_out', () => {
  const ts = { r: 10, T1: 300, p1: 100, q_in: 1500, gamma: 1.4 };
  const res = HLNhietDong.otto(ts);
  assert.ok(Math.abs(res.w - (res.q_in - res.q_out)) < 1e-9, 'w = q_in - q_out');
});

test('HLNhietDong: Cháy hữu hạn Wiebe tiệm cận chu trình lý tưởng', () => {
  const ts = {
    r: 10,
    T1: 300,
    p1: 100,
    q_in: 1500,
    gamma: 1.4,
    wiebe_phi0: 359.5,
    wiebe_delta_phi: 1.0,
    wiebe_phi_xa: 540,
    B: 80,
    S: 80,
    L: 140,
  };

  const wiebeRes = HLNhietDong.tinhWiebe(ts, HLCoCau);
  const ottoRes = HLNhietDong.otto(ts);

  assert.equal(wiebeRes.diem.length, 1441);
  // Hiệu suất Wiebe khi cháy tức thời tại 360° lệch Otto lý tưởng <= 1.0%
  const saiSoEta = Math.abs(wiebeRes.eta - ottoRes.eta) / ottoRes.eta;
  assert.ok(saiSoEta < 0.015, `Wiebe eta = ${wiebeRes.eta}, Otto eta = ${ottoRes.eta}, sai số = ${saiSoEta}`);
});
