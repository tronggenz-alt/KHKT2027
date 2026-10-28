import test from 'node:test';
import assert from 'node:assert/strict';
import HLCoCau from '../../frontend/hoa-hoc/hoa-ly/dong-co-nhiet/co-cau.js';

test('HLCoCau: Thể tích công tác Vd và buồng cháy Vc chuẩn xác', () => {
  const ts = { B: 80, S: 80, L: 140, r: 10 };
  const hh = HLCoCau.hinhHoc(ts);

  // Vd = 402,124 cm³; Vc = 44,680 cm³
  assert.ok(Math.abs(hh.Vd_cm3 - 402.124) < 0.05, `Vd = ${hh.Vd_cm3} cm³`);
  assert.ok(Math.abs(hh.Vc_cm3 - 44.680) < 0.05, `Vc = ${hh.Vc_cm3} cm³`);
  assert.ok(Math.abs(hh.V1_cm3 - 446.804) < 0.05, `V1 = ${hh.V1_cm3} cm³`);
});

test('HLCoCau: Bảng thể tích V(φ) theo từng nấc góc quay', () => {
  const ts = { B: 80, S: 80, L: 140, r: 10 };
  const bangChuan = [
    { phi: 0, V: 44.680 },
    { phi: 30, V: 78.835 },
    { phi: 60, V: 167.094 },
    { phi: 90, V: 275.077 },
    { phi: 120, V: 368.156 },
    { phi: 180, V: 446.804 },
  ];

  for (const b of bangChuan) {
    const v = HLCoCau.V(b.phi, ts);
    const saiSo = Math.abs(v - b.V);
    assert.ok(saiSo < 0.05, `Tại phi = ${b.phi}°, V = ${v.toFixed(3)}, mong đợi ${b.V}`);
  }

  // V(90°) > Vc + Vd/2 = 245,74 do thanh truyền nghiêng (connecting rod obliquity)
  const v90 = HLCoCau.V(90, ts);
  const vGiua = 44.680 + 402.124 / 2; // 245.742
  assert.ok(v90 > vGiua, `V(90°) = ${v90} > ${vGiua} do góc nghiêng thanh truyền đẩy pít-tông trễ hơn`);
});

test('HLCoCau: Tính tuần hoàn và đối xứng', () => {
  const ts = { B: 80, S: 80, L: 140, r: 10 };

  for (let phi = 0; phi <= 360; phi += 15) {
    const v1 = HLCoCau.V(phi, ts);
    const v2 = HLCoCau.V(phi + 360, ts);
    assert.ok(Math.abs(v1 - v2) < 1e-6, `Tuần hoàn: V(${phi}) != V(${phi + 360})`);

    const vDoiXung = HLCoCau.V(360 - phi, ts);
    assert.ok(Math.abs(v1 - vDoiXung) < 1e-6, `Đối xứng: V(${phi}) != V(${360 - phi})`);
  }
});

test('HLCoCau: Nghịch đảo thetaTuV ∘ thuận V = đồng nhất trên [0°, 180°]', () => {
  const ts = { B: 80, S: 80, L: 140, r: 10 };

  for (let theta = 0; theta <= 180; theta += 1) {
    const v = HLCoCau.V(theta, ts);
    const thetaNghich = HLCoCau.thetaTuV(v, ts);
    assert.ok(Math.abs(theta - thetaNghich) < 1e-4, `Nghịch đảo sai: theta = ${theta}, nghịch = ${thetaNghich}`);
  }
});

test('HLCoCau: Đơn điệu của V trên [0, 180] và [180, 360]', () => {
  const ts = { B: 80, S: 80, L: 140, r: 10 };

  let vTruoc = HLCoCau.V(0, ts);
  for (let theta = 1; theta <= 180; theta++) {
    const v = HLCoCau.V(theta, ts);
    assert.ok(v > vTruoc, `V phải tăng đơn điệu trên (0, 180): v(${theta}) = ${v} <= ${vTruoc}`);
    vTruoc = v;
  }

  for (let theta = 181; theta <= 360; theta++) {
    const v = HLCoCau.V(theta, ts);
    assert.ok(v < vTruoc, `V phải giảm đơn điệu trên (180, 360): v(${theta}) = ${v} >= ${vTruoc}`);
    vTruoc = v;
  }
});

test('HLCoCau: Khối lượng khí m = 0,5189 g (r=10, T1=300, p1=100 kPa)', () => {
  const ts = { B: 80, S: 80, L: 140, r: 10 };
  const hh = HLCoCau.hinhHoc(ts);
  const V1_m3 = hh.V1_cm3 * 1e-6;
  const R = 0.287; // kJ/(kg·K)
  const T1 = 300;
  const p1 = 100; // kPa

  const m_kg = (p1 * V1_m3) / (R * T1);
  const m_g = m_kg * 1000;
  assert.ok(Math.abs(m_g - 0.5189) < 0.001, `m = ${m_g.toFixed(4)} g (kỳ vọng 0.5189 g)`);
});
