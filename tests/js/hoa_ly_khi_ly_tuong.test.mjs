import test from 'node:test';
import assert from 'node:assert/strict';
import HL_KhiLyTuong from '../../frontend/hoa-hoc/hoa-ly/khi-ly-tuong/mo-hinh.js';

test('HL_KhiLyTuong: Thể tích mol chuẩn', () => {
  // n = 1, T = 298.15 K, p = 1 bar -> V = 24.790 L
  const V1 = HL_KhiLyTuong.theTich({ n: 1, T: 298.15, p_bar: 1 });
  assert.ok(Math.abs(V1 - 24.790) < 0.01, `V = ${V1}, mong đợi 24.790`);

  // n = 1, T = 273.15 K, p = 1 atm = 1.01325 bar -> V = 22.414 L
  const V2 = HL_KhiLyTuong.theTich({ n: 1, T: 273.15, p_bar: 1.01325 });
  assert.ok(Math.abs(V2 - 22.414) < 0.01, `V = ${V2}, mong đợi 22.414`);
});

test('HL_KhiLyTuong: Vận tốc phân tử ở 300 K', () => {
  const khiData = [
    { M: 28.013, v_rms: 516.8, v_p: 422.0, v_tb: 476.2 },
    { M: 31.999, v_rms: 483.6, v_p: 394.8, v_tb: 445.5 },
    { M: 4.0026, v_rms: 1367.2, v_p: 1116.3, v_tb: 1259.7 },
    { M: 2.016, v_rms: 1926.6, v_p: 1573.1, v_tb: 1775.0 },
    { M: 44.01, v_rms: 412.4 },
    { M: 39.948, v_rms: 432.8 }
  ];

  for (const k of khiData) {
    const rms = HL_KhiLyTuong.vRms(k.M, 300);
    assert.ok(Math.abs(rms - k.v_rms) / k.v_rms < 0.002, `v_rms (${k.M}) = ${rms}, kỳ vọng ${k.v_rms}`);
    if (k.v_p) {
      const p = HL_KhiLyTuong.vP(k.M, 300);
      assert.ok(Math.abs(p - k.v_p) / k.v_p < 0.002, `v_p = ${p}, kỳ vọng ${k.v_p}`);
    }
    if (k.v_tb) {
      const tb = HL_KhiLyTuong.vTb(k.M, 300);
      assert.ok(Math.abs(tb - k.v_tb) / k.v_tb < 0.002, `v_tb = ${tb}, kỳ vọng ${k.v_tb}`);
    }
  }
});

test('HL_KhiLyTuong: Động năng trung bình và kiểm chứng K2', () => {
  // Wđ(300 K) = 6.2129·10⁻²¹ J
  const Wd = HL_KhiLyTuong.dongNangTb(300);
  assert.ok(Math.abs(Wd - 6.2129e-21) / 6.2129e-21 < 0.001, `Wd = ${Wd}`);

  // Kiểm K2: 1 mol N2 ở 300 K trong V = nRT/p (p = 1 bar = 100 000 Pa)
  // (1/3) * (N/V) * m * v_rms^2 = 100 000 Pa
  const n = 1;
  const T = 300;
  const V_m3 = (n * HL_KhiLyTuong.R_SI * T) / 100000;
  const N = n * HL_KhiLyTuong.N_A;
  const m = 0.028013 / HL_KhiLyTuong.N_A;
  const v_rms = HL_KhiLyTuong.vRms(28.013, 300);
  const p_calc = (1 / 3) * (N / V_m3) * m * (v_rms * v_rms);
  assert.ok(Math.abs(p_calc - 100000) < 1e-6, `p_calc = ${p_calc}, mong đợi 100000`);
});

test('HL_KhiLyTuong: Quá trình đẳng nhiệt và đoạn nhiệt', () => {
  // Đẳng nhiệt: n = 1, T = 300 K, V 10 -> 20 L: A' = 1728.94 J, Q = A', dU = 0
  const dn = HL_KhiLyTuong.quaTrinh({ n: 1, T: 300, V: 10 }, { V: 20 }, 'T');
  assert.ok(Math.abs(dn.A_sinh - 1728.94) < 0.1, `A_sinh = ${dn.A_sinh}`);
  assert.ok(Math.abs(dn.Q - 1728.94) < 0.1, `Q = ${dn.Q}`);
  assert.equal(dn.dU, 0);
  assert.ok(Math.abs(dn.dU - (dn.A + dn.Q)) < 1e-9);

  // Đoạn nhiệt: từ 300 K, V giảm còn 1/2
  // gamma = 1.4 -> T = 395.85 K, p x 2.6390
  const da1 = HL_KhiLyTuong.quaTrinh({ n: 1, T: 300, V: 20, p: 1, gamma: 1.4 }, { V: 10 }, 'doan-nhiet');
  assert.ok(Math.abs(da1.T2 - 395.85) < 0.1, `T2 = ${da1.T2}, mong đợi 395.85`);
  assert.ok(Math.abs(da1.p2 / da1.p1 - 2.6390) < 0.001, `p_ratio = ${da1.p2 / da1.p1}`);
  assert.equal(da1.Q, 0);
  assert.ok(Math.abs(da1.dU - (da1.A + da1.Q)) < 1e-9);

  // gamma = 5/3 -> T = 476.22 K
  const da2 = HL_KhiLyTuong.quaTrinh({ n: 1, T: 300, V: 20, p: 1, gamma: 5 / 3 }, { V: 10 }, 'doan-nhiet');
  assert.ok(Math.abs(da2.T2 - 476.22) < 0.1, `T2 = ${da2.T2}, mong đợi 476.22`);
  assert.ok(Math.abs(da2.dU - (da2.A + da2.Q)) < 1e-9);
});

test('HL_KhiLyTuong: Bảng Van der Waals CO2 ở 300 K', () => {
  // CO2: a = 3.640, b = 0.04267, n = 1, T = 300 K
  const testCases = [
    { V: 0.5, p_lt: 49.887, p_vdw: 39.981, Z: 0.8014 },
    { V: 0.2, p_lt: 124.717, p_vdw: 67.542, Z: 0.5416 },
    { V: 1.0, p_lt: 24.943, p_vdw: 22.415, Z: 0.8986 }
  ];

  for (const tc of testCases) {
    const res = HL_KhiLyTuong.vanDerWaals(1, tc.V, 300, 3.640, 0.04267);
    assert.ok(Math.abs(res.p_ly_tuong - tc.p_lt) < 0.01, `p_lt (${tc.V}) = ${res.p_ly_tuong}, mong đợi ${tc.p_lt}`);
    assert.ok(Math.abs(res.p_vdw - tc.p_vdw) < 0.01, `p_vdw (${tc.V}) = ${res.p_vdw}, mong đợi ${tc.p_vdw}`);
    assert.ok(Math.abs(res.Z - tc.Z) < 0.001, `Z (${tc.V}) = ${res.Z}, mong đợi ${tc.Z}`);
  }
});

test('HL_KhiLyTuong: Charles và ngoại suy độ không tuyệt đối -273.15 °C', () => {
  // Charles (p = 1 bar, n = 1) tại 0 °C (273.15 K) và 100 °C (373.15 K)
  const V_0 = HL_KhiLyTuong.theTich({ n: 1, T: 273.15, p_bar: 1 });
  const V_100 = HL_KhiLyTuong.theTich({ n: 1, T: 373.15, p_bar: 1 });

  const diem = [
    { t_C: 0, V_L: V_0 },
    { t_C: 100, V_L: V_100 }
  ];

  const hoiQuy = HL_KhiLyTuong.doKhongTuyetDoi(diem);
  assert.ok(hoiQuy !== null);
  assert.ok(Math.abs(hoiQuy.do_khong_tuyet_doi - (-273.15)) < 0.01, `t0 = ${hoiQuy.do_khong_tuyet_doi}`);
});
