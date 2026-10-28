// Phòng thí nghiệm Hoá ảo – hàm tính thuần (frontend/hoa-hoc/phong-thi-nghiem/tinh-toan.js) khớp bảng chuẩn (sai số ±0,02).
// Chạy: node --test tests/js/
import { test } from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const T = require("../../frontend/hoa-hoc/phong-thi-nghiem/tinh-toan.js");
const gan = (thuc, mong, sai = 0.02, ten = "") => assert.ok(Math.abs(thuc - mong) <= sai, `${ten}: ${thuc} ≠ ${mong} (±${sai})`);

test("HCl 0,1 M 20 mL + NaOH 0,1 M (mạnh – mạnh)", () => {
  const p = { Ca: 0.1, Va: 20, Cb: 0.1 };
  for (const [V, pH] of [[0, 1.0], [10, 1.48], [19.9, 3.6], [20, 7.0], [20.1, 10.4], [30, 12.3]]) gan(T.pHChuanDo({ ...p, Vb: V }), pH, 0.02, `V=${V}`);
  gan(T.theTichTuongDuong(p), 20, 1e-9, "V tương đương");
});

test("CH₃COOH 0,1 M 20 mL + NaOH 0,1 M (yếu – mạnh, Ka = 1,75·10⁻⁵)", () => {
  const p = { Ca: 0.1, Va: 20, Cb: 0.1, Ka: 1.75e-5 };
  for (const [V, pH] of [[0, 2.88], [10, 4.76], [20, 8.73]]) gan(T.pHChuanDo({ ...p, Vb: V }), pH, 0.02, `V=${V}`);
});

test("đường cong chuẩn độ tăng dần, có bước nhảy quanh điểm tương đương", () => {
  const d = T.duongCongChuanDo({ Ca: 0.1, Va: 20, Cb: 0.1 }, 40, 0.5);
  for (let i = 1; i < d.length; i++) assert.ok(d[i][1] >= d[i - 1][1] - 1e-9);
  const pH = (V) => d.find(([v]) => Math.abs(v - V) < 1e-9)[1];
  assert.ok(pH(20.5) - pH(19.5) > 4, "bước nhảy pH");
});

test("chỉ thị: phenolphthalein không màu ở pH 7, hồng ở pH 11; methyl orange đỏ ở pH 2, vàng ở pH 6", () => {
  assert.equal(T.mauChiThi("phenolphthalein", 7).mo_ta, "không màu");
  assert.equal(T.mauChiThi("phenolphthalein", 11).mo_ta, "hồng");
  assert.equal(T.mauChiThi("phenolphthalein", 9).mo_ta, "hồng nhạt");
  assert.equal(T.mauChiThi("methyl_orange", 2).mo_ta, "đỏ");
  assert.equal(T.mauChiThi("methyl_orange", 6).mo_ta, "vàng");
});

test("điện phân CuSO₄ (điện cực trơ), I = 2 A, t = 965 s → 0,64 g Cu và 0,124 L O₂ (đkc)", () => {
  const kq = T.dienPhan({ bai: "cuso4_tro", I: 2, t: 965, V_dd: 0.2, C0: 0.5 });
  gan(kq.n_e, 0.02, 1e-4, "n(e)");
  gan(kq.m_cathode, 0.64, 0.02, "m(Cu)");
  gan(kq.V_khi_anode, 0.124, 0.02, "V(O₂)");
  assert.equal(kq.khi_cathode, null);
  assert.ok(kq.ti_le_Cu2 < 1 && kq.pH < 7);
});

test("điện phân CuSO₄ hết Cu²⁺ thì cathode thoát H₂; NaCl màng ngăn: cathode H₂ + OH⁻, anode Cl₂; anode Cu tan", () => {
  const het = T.dienPhan({ bai: "cuso4_tro", I: 2, t: 9650, V_dd: 0.1, C0: 0.05 });
  assert.equal(het.khi_cathode, "H₂");
  gan(het.ti_le_Cu2, 0, 1e-9);
  const nacl = T.dienPhan({ bai: "nacl_mang_ngan", I: 1, t: 965, V_dd: 0.2, C0: 1 });
  assert.equal(nacl.khi_cathode, "H₂");
  assert.equal(nacl.khi_anode, "Cl₂");
  gan(nacl.V_khi_cathode, 0.124, 0.01, "V(H₂)");
  assert.ok(nacl.pH_cathode > 12);
  const tan = T.dienPhan({ bai: "cuso4_anode_cu", I: 2, t: 965 });
  gan(tan.m_cathode, 0.64, 0.02);
  gan(tan.m_anode_giam, tan.m_cathode, 1e-9);
});

test("pin Zn – Cu: E°pin = 1,103 V; Zn là anode; rút cầu muối → 0 V", () => {
  const p = T.pinGalvani({ trai: "Zn", phai: "Cu" });
  gan(p.E0_pin, 1.103, 0.001, "E°pin");
  gan(p.E_pin, 1.103, 0.001, "Epin chuẩn");
  assert.equal(p.anode, "Zn");
  assert.equal(p.cathode, "Cu");
  assert.equal(p.phuong_trinh, "Zn + Cu²⁺ → Zn²⁺ + Cu");
  assert.equal(T.pinGalvani({ trai: "Zn", phai: "Cu", cauMuoi: false }).E_pin, 0);
  assert.equal(T.pinGalvani({ trai: "Ag", phai: "Cu" }).phuong_trinh, "Cu + 2Ag⁺ → Cu²⁺ + 2Ag");
  assert.ok(T.pinGalvani({ trai: "Zn", phai: "Cu", C_trai: 0.01, C_phai: 1 }).E_pin > 1.103, "Nernst: giảm [Zn²⁺] → E tăng");
});

test("định dạng số dùng dấu phẩy thập phân", () => {
  assert.equal(T.dinhDang(0.64), "0,64");
  assert.equal(T.dinhDang(1.1034, 3), "1,103");
});
