import test from 'node:test';
import assert from 'node:assert/strict';

import HLDinhDang from '../../frontend/hoa-hoc/hoa-ly/chung/dinh-dang.js';
import HLNgauNhien from '../../frontend/hoa-hoc/hoa-ly/chung/ngau-nhien.js';
import HLHat from '../../frontend/hoa-hoc/hoa-ly/chung/hat.js';

test('HLDinhDang: vachDep(0, 109, 5) chia vạch đẹp 1-2-5', () => {
  const ticks = HLDinhDang.vachDep(0, 109, 5);
  // Kiểm tra vạch xuất phát từ 0 và các vạch là bội của 20
  assert.equal(ticks[0], 0);
  assert.equal(ticks[1], 20);
  assert.equal(ticks[2], 40);
  assert.equal(ticks[3], 60);
  assert.equal(ticks[4], 80);
  assert.equal(ticks[5], 100);

  // Mọi vạch phải là bội của 1, 2, hoặc 5 x 10^n
  ticks.forEach(v => {
    assert.equal(v % 20, 0);
  });
});

test('HLNgauNhien: cùng seed sinh cùng dãy số', () => {
  const b1 = HLNgauNhien.taoBoNgauNhien(42);
  const b2 = HLNgauNhien.taoBoNgauNhien(42);

  for (let i = 0; i < 20; i++) {
    assert.equal(b1.deu(), b2.deu());
    assert.equal(b1.chuan(), b2.chuan());
  }
});

test('HLNgauNhien: maxwell3D có <v^2> = 3kT/m (±2%, N = 20 000)', () => {
  const m = 4.65e-26; // kg (N2)
  const kT = 1.38e-23 * 300; // J (300 K)
  const lyThuyet = 3 * kT / m;

  const bo = HLNgauNhien.taoBoNgauNhien(999);
  const N = 20000;
  let tongV2 = 0;
  for (let i = 0; i < N; i++) {
    const v = bo.maxwell3D(m, kT);
    tongV2 += v * v;
  }
  const thucNghiem = tongV2 / N;
  const saiSo = Math.abs(thucNghiem - lyThuyet) / lyThuyet;
  assert.ok(saiSo < 0.02, `Sai số <v^2> ${saiSo * 100}% >= 2%`);
});

test('HLHat: Va chạm hạt - hạt bảo toàn động lượng và năng lượng (1e-9)', () => {
  const he = new HLHat.HeHat({
    xMin: 0, xMax: 1000,
    yMin: 0, yMax: 1000,
    choPhepVaChamHat: true
  });

  // Hai hạt lao vào nhau với khối lượng và vận tốc khác nhau
  he.themHat({ id: 1, m: 2.0, r: 10, x: 100, y: 100, vx: 5.0, vy: 2.0 });
  he.themHat({ id: 2, m: 5.0, r: 10, x: 115, y: 100, vx: -3.0, vy: 1.0 });

  const pTruoc = he.dongLuong();
  const ETruoc = he.dongNang();

  // Cho bước chuyển động nhỏ để kích hoạt va chạm
  he.buoc(0.1);

  const pSau = he.dongLuong();
  const ESau = he.dongNang();

  assert.ok(Math.abs(pSau.px - pTruoc.px) < 1e-9, `Lệch px: ${Math.abs(pSau.px - pTruoc.px)}`);
  assert.ok(Math.abs(pSau.py - pTruoc.py) < 1e-9, `Lệch py: ${Math.abs(pSau.py - pTruoc.py)}`);
  assert.ok(Math.abs(ESau - ETruoc) < 1e-9, `Lệch E: ${Math.abs(ESau - ETruoc)}`);
});

test('HLHat: Hạt không bao giờ ra khỏi thành kể cả khi thành di chuyển nhanh', () => {
  const he = new HLHat.HeHat({
    xMin: 50, xMax: 300,
    yMin: 50, yMax: 200,
    vxMax: -200 // pít-tông nén cực nhanh về phía trái
  });

  // Tạo 50 hạt ngẫu nhiên
  const bo = HLNgauNhien.taoBoNgauNhien(123);
  for (let i = 0; i < 50; i++) {
    he.themHat({
      id: i,
      m: 1.0,
      r: 4,
      x: bo.deu(60, 290),
      y: bo.deu(60, 190),
      vx: bo.deu(-50, 50),
      vy: bo.deu(-50, 50)
    });
  }

  // Chạy 100 bước nén thành
  for (let b = 0; b < 100; b++) {
    const xMaxMoi = Math.max(100, 300 - b * 1.5);
    he.datThanh({ xMax: xMaxMoi, vxMax: -150 });
    he.buoc(0.01);

    // Kiểm tra tất cả các hạt
    for (const h of he.hat) {
      assert.ok(h.x - h.r >= he.thanh.xMin - 1e-6, `Hạt lọt ra ngoài bên trái: ${h.x}`);
      assert.ok(h.x + h.r <= he.thanh.xMax + 1e-6, `Hạt lọt ra ngoài bên phải: ${h.x} > ${he.thanh.xMax}`);
      assert.ok(h.y - h.r >= he.thanh.yMin - 1e-6, `Hạt lọt ra ngoài bên trên: ${h.y}`);
      assert.ok(h.y + h.r <= he.thanh.yMax + 1e-6, `Hạt lọt ra ngoài bên dưới: ${h.y}`);
    }
  }
});
