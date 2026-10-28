// Phòng thí nghiệm Hoá ảo – hình học dụng cụ (frontend/hoa-hoc/phong-thi-nghiem/hinh-hoc.js): liền mạch, không đường may,
// pháp tuyến hướng ra ngoài, mặt đúng chiều, lòng bình khớp thể tích. Chạy: node --test tests/js/
import { test } from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const H = require("../../frontend/hoa-hoc/phong-thi-nghiem/hinh-hoc.js");

const DUNG_CU = {
  coc: H.DUNG_CU.coc(0.07, 0.14),
  binhTamGiac: H.DUNG_CU.binhTamGiac(0.05, 0.15, 0.016, 0.1),
  ongNghiem: H.DUNG_CU.ongNghiem(0.0115, 0.15),
  loThuoc: H.DUNG_CU.loThuoc(0.03, 0.09, 0.012),
  denCon: H.DUNG_CU.denCon(),
};
const goc = (a, b, c) => { // góc quay (độ) tại b của đường a → b → c
  const u = [b.x - a.x, b.y - a.y], v = [c.x - b.x, c.y - b.y];
  const cs = (u[0] * v[0] + u[1] * v[1]) / (Math.hypot(...u) * Math.hypot(...v));
  return (Math.acos(Math.min(1, Math.max(-1, cs))) * 180) / Math.PI;
};

for (const [ten, ngoai] of Object.entries(DUNG_CU)) {
  test(`${ten}: không có nếp gấp – mọi chỗ đổi hướng của thành kính ≤ 24° (trừ góc nhọn khai báo)`, () => {
    const pts = H.muotProfile(H.thanhThuyTinh(ngoai));
    let max = 0;
    for (let i = 1; i < pts.length - 1; i++) if (!pts[i].sac) max = Math.max(max, goc(pts[i - 1], pts[i], pts[i + 1]));
    assert.ok(max <= 24, `${ten}: đổi hướng ${max.toFixed(1)}°`);
  });

  test(`${ten}: lưới kín – đường may khớp vị trí + pháp tuyến, pháp tuyến đơn vị, mặt quay đúng chiều`, () => {
    const phan = 48, m = H.latheMem(H.thanhThuyTinh(ngoai), { phan });
    const hang = phan + 1, K = m.v.length / 3 / hang;
    for (let k = 0; k < K; k++) {
      const a = k * hang, b = k * hang + phan;
      for (let c = 0; c < 3; c++) {
        assert.ok(Math.abs(m.v[a * 3 + c] - m.v[b * 3 + c]) < 1e-5, "đường may lệch vị trí");
        assert.ok(Math.abs(m.n[a * 3 + c] - m.n[b * 3 + c]) < 1e-5, "đường may lệch pháp tuyến");
      }
    }
    for (let o = 0; o < m.n.length; o += 3) assert.ok(Math.abs(Math.hypot(m.n[o], m.n[o + 1], m.n[o + 2]) - 1) < 1e-4, "pháp tuyến không đơn vị");
    let sai = 0, xet = 0;
    for (let t = 0; t < m.i.length; t += 3) {
      const [a, b, c] = [m.i[t], m.i[t + 1], m.i[t + 2]];
      const P = (i) => [m.v[i * 3], m.v[i * 3 + 1], m.v[i * 3 + 2]];
      const [pa, pb, pc] = [P(a), P(b), P(c)];
      const u = pb.map((x, i) => x - pa[i]), w = pc.map((x, i) => x - pa[i]);
      const f = [u[1] * w[2] - u[2] * w[1], u[2] * w[0] - u[0] * w[2], u[0] * w[1] - u[1] * w[0]];
      const lf = Math.hypot(...f);
      if (lf < 1e-14) continue; // tam giác suy biến ở trục
      const nv = [0, 1, 2].map((i) => m.n[a * 3 + i] + m.n[b * 3 + i] + m.n[c * 3 + i]);
      xet++;
      if (f[0] * nv[0] + f[1] * nv[1] + f[2] * nv[2] < 0) sai++;
    }
    assert.ok(xet > 100 && sai === 0, `${sai}/${xet} mặt quay ngược`);
  });

  test(`${ten}: vách ngoài hướng ra ngoài, vách trong hướng vào trục`, () => {
    const dinh = H.dinhLatCat(H.muotProfile(H.thanhThuyTinh(ngoai)));
    assert.ok(dinh.some((d) => d.x > 0.003 && d.nx > 0.9), "không có vách ngoài hướng ra ngoài");
    assert.ok(dinh.some((d) => d.x > 0.003 && d.nx < -0.9), "không có vách trong hướng vào trục");
    assert.ok(dinh.some((d) => d.ny < -0.9) && dinh.some((d) => d.ny > 0.9), "thiếu mặt đáy ngoài (xuống) hoặc đáy trong / mặt miệng (lên)");
  });
}

test("cốc: lòng bình = bán kính ngoài − bề dày, thể tích khớp hình trụ", () => {
  const r = 0.07, h = 0.14, pts = H.muotProfile(H.DUNG_CU.coc(r, h));
  assert.ok(Math.abs(H.banKinhTai(pts, 0.06) - (r - H.DAY_KINH)) < 1e-6);
  const ri = r - H.DAY_KINH, mong = Math.PI * ri * ri * 0.1 * 1e6;
  const thuc = H.theTichDenH(pts, 0.103);
  assert.ok(Math.abs(thuc - mong) / mong < 0.04, `thể tích ${thuc.toFixed(1)} mL ≠ ${mong.toFixed(1)} mL`);
});

test("bình tam giác: tra mực chất lỏng từ thể tích rồi đổi ngược lại ra đúng thể tích", () => {
  const pts = H.muotProfile(H.DUNG_CU.binhTamGiac(0.05, 0.15, 0.016, 0.1));
  for (const mL of [20, 50, 100]) {
    const h = H.hDeCoTheTich(pts, mL, H.DAY_KINH, 0.12);
    assert.ok(Math.abs(H.theTichDenH(pts, h) - mL) < 0.5, `${mL} mL`);
  }
});

test("khối chất lỏng: kín, bám sát lòng bình (bán kính ≤ lòng bình)", () => {
  const pts = H.muotProfile(H.DUNG_CU.binhTamGiac(0.05, 0.15, 0.016, 0.1));
  const p = H.chatLong(pts, 0.04);
  for (const [x, y] of p) assert.ok(x <= H.banKinhTai(pts, Math.min(y, 0.04)) + 1e-6 || y > 0.04, `lòi ra ngoài lòng bình tại y=${y}`);
  const m = H.latheMem(p, { phan: 32 });
  assert.ok(m.i.length > 0);
});
