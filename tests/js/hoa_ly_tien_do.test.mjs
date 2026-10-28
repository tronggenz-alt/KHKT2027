import test from 'node:test';
import assert from 'node:assert/strict';
import HLTienDo from '../../frontend/hoa-hoc/hoa-ly/dong-co-nhiet/tien-do.js';
import HLNhietDong from '../../frontend/hoa-hoc/hoa-ly/dong-co-nhiet/nhiet-dong.js';
import HLCoCau from '../../frontend/hoa-hoc/hoa-ly/dong-co-nhiet/co-cau.js';

const ts = { r: 10, T1: 300, p1: 100, q_in: 1500, gamma: 1.4, B: 80, S: 80, L: 140 };

test('HLTienDo: Các mốc tuTienDo chính xác', () => {
  const calc = HLNhietDong.otto(ts);

  // t = 1 -> trạng thái 1 tại phi = 180°
  const st1 = HLTienDo.tuTienDo(1.0, ts, HLNhietDong, HLCoCau);
  assert.equal(st1.doan, 1);
  assert.equal(st1.phi, 180);
  assert.ok(Math.abs(st1.T - calc.T1) < 0.1, `t=1: T = ${st1.T} != T1 ${calc.T1}`);

  // t = 2 -> trạng thái 2 tại phi = 360°
  const st2 = HLTienDo.tuTienDo(2.0, ts, HLNhietDong, HLCoCau);
  assert.equal(st2.doan, 2);
  assert.equal(st2.phi, 360);
  assert.ok(Math.abs(st2.T - calc.T2) < 0.1, `t=2: T = ${st2.T} != T2 ${calc.T2}`);

  // t = 2.5 -> phi = 360°, T = T2 + 0.5 * q_in / cv
  const st25 = HLTienDo.tuTienDo(2.5, ts, HLNhietDong, HLCoCau);
  assert.equal(st25.doan, 2);
  assert.equal(st25.phi, 360);
  const T_mongDoi25 = calc.T2 + 0.5 * (ts.q_in / calc.cv);
  assert.ok(Math.abs(st25.T - T_mongDoi25) < 0.1, `t=2.5: T = ${st25.T} != ${T_mongDoi25}`);

  // t = 3 -> trạng thái 3
  const st3 = HLTienDo.tuTienDo(3.0, ts, HLNhietDong, HLCoCau);
  assert.equal(st3.doan, 3);
  assert.equal(st3.phi, 360);
  assert.ok(Math.abs(st3.T - calc.T3) < 0.1, `t=3: T = ${st3.T} != T3 ${calc.T3}`);

  // t = 5 -> trạng thái xả tại phi = 540°
  const st5 = HLTienDo.tuTienDo(5.0, ts, HLNhietDong, HLCoCau);
  assert.equal(st5.doan, 5);
  assert.equal(st5.phi, 540);
  assert.ok(Math.abs(st5.T - calc.T1) < 0.1, `t=5: T = ${st5.T} != T1 ${calc.T1}`);

  // Định luật I kiểm chứng: ΔU = A + Q sai số <= 1e-6 cho mọi mốc
  for (let t = 0; t < 6; t += 0.25) {
    const st = HLTienDo.tuTienDo(t, ts, HLNhietDong, HLCoCau);
    const tongAQ = st.A + st.Q;
    assert.ok(Math.abs(st.deltaU - tongAQ) < 1e-6, `Tại t = ${t}: ΔU (${st.deltaU}) != A+Q (${tongAQ})`);
  }
});

test('HLTienDo: buocGoc nhảy qua đoạn đẳng tích và phát sự kiện', () => {
  // Từ t = 1.9, cộng Δφ = +30° -> t ∈ (3; 3.2), có sự kiện danh_lua
  const buoc1 = HLTienDo.buocGoc(1.9, 30, ts);
  assert.ok(buoc1.t > 3.0 && buoc1.t < 3.2, `t sau khi bước 30° từ 1.9 = ${buoc1.t}`);
  assert.ok(buoc1.suKien.includes('danh_lua'), 'Phải có sự kiện danh_lua');

  // Từ t = 3.1, cộng Δφ = -30° -> t ∈ (1; 2), có sự kiện danh_lua_nguoc
  const buoc2 = HLTienDo.buocGoc(3.1, -30, ts);
  assert.ok(buoc2.t > 1.0 && buoc2.t < 2.0, `t sau khi lùi 30° từ 3.1 = ${buoc2.t}`);
  assert.ok(buoc2.suKien.includes('danh_lua_nguoc'), 'Phải có sự kiện danh_lua_nguoc');

  // Quay 720° -> vongDelta = +1, có sự kiện het_chu_trinh
  const buoc3 = HLTienDo.buocGoc(1.0, 720, ts);
  assert.equal(buoc3.vongDelta, 1, 'Quay đủ 720° tăng 1 vòng');
  assert.ok(buoc3.suKien.includes('het_chu_trinh'), 'Phải có sự kiện het_chu_trinh');
  assert.ok(Math.abs(buoc3.t - 1.0) < 1e-6, `t quay lại vị trí cũ: ${buoc3.t}`);
});

test('HLTienDo: Chống nhảy nhầm trên đồ thị p–V (cửa sổ ±0.75)', () => {
  const hh = HLCoCau.hinhHoc(ts);
  const calc = HLNhietDong.otto(ts);

  // Đang ở t = 5.5 (giữa kỳ xả), kéo chấm về phía V lớn (ví dụ V = V1)
  const tMoi = HLTienDo.tienDoTuDiemPV(hh.V1_cm3, calc.p1 / 100, 5.5, ts, null, HLNhietDong, HLCoCau);

  // t phải nằm trong [5.0; 5.5], tuyệt đối không rơi vào [0; 1)
  assert.ok(tMoi >= 5.0 && tMoi <= 5.5, `tMoi = ${tMoi} phải nằm trong kỳ xả [5.0, 5.5]`);
  assert.ok(!(tMoi >= 0 && tMoi < 1.0), 'Không được nhảy sang kỳ nạp [0, 1)');
});

test('HLTienDo: gocTuTheTich cho góc trong nửa vòng hiện tại', () => {
  const hh = HLCoCau.hinhHoc(ts);
  const V_giua = hh.Vc_cm3 + hh.Vd_cm3 / 2;

  // Nửa vòng 0 (0° - 180°): đang ở phi = 45°
  const phi0 = HLTienDo.gocTuTheTich(V_giua, 45, ts, HLCoCau);
  assert.ok(phi0 >= 0 && phi0 <= 180, `Nửa vòng 0: phi = ${phi0}`);

  // Nửa vòng 1 (180° - 360°): đang ở phi = 250°
  const phi1 = HLTienDo.gocTuTheTich(V_giua, 250, ts, HLCoCau);
  assert.ok(phi1 >= 180 && phi1 <= 360, `Nửa vòng 1: phi = ${phi1}`);

  // Nửa vòng 2 (360° - 540°): đang ở phi = 400°
  const phi2 = HLTienDo.gocTuTheTich(V_giua, 400, ts, HLCoCau);
  assert.ok(phi2 >= 360 && phi2 <= 540, `Nửa vòng 2: phi = ${phi2}`);

  // Nửa vòng 3 (540° - 720°): đang ở phi = 650°
  const phi3 = HLTienDo.gocTuTheTich(V_giua, 650, ts, HLCoCau);
  assert.ok(phi3 >= 540 && phi3 <= 720, `Nửa vòng 3: phi = ${phi3}`);
});
