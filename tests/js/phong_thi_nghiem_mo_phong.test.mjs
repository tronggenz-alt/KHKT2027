// Phòng thí nghiệm Hoá ảo – lõi mô phỏng (frontend/hoa-hoc/phong-thi-nghiem/mo-phong.js): chạy trọn một lượt từng thí nghiệm.
import { test } from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { readFileSync } from "node:fs";

const require = createRequire(import.meta.url);
const M = require("../../frontend/hoa-hoc/phong-thi-nghiem/mo-phong.js");
const goc = new URL("../../frontend/hoa-hoc/phong-thi-nghiem/", import.meta.url);
const doc = (p) => JSON.parse(readFileSync(new URL(p, goc), "utf-8"));
const bangIon = doc("bang-ion.json");
const tn = (id) => doc(`thi-nghiem/${id}.json`);
const chay = (mp, giay, buoc = 0.05) => { for (let t = 0; t < giay; t += buoc) mp.capNhat(buoc); };

test("chuẩn độ HCl + phenolphthalein: chảy nhanh rồi từng giọt → chuyển hồng ở V ≈ 20,0 mL; đọc burette, tính nồng độ", () => {
  const mp = M.tao(tn("chuan-do"));
  const suKien = [];
  mp.on("trang_thai", (s) => suKien.push(s));
  assert.equal(mp.trangThai().buoc_hien_tai, 0);
  mp.thaoTac("chon_chi_thi", { id: "phenolphthalein" });
  assert.equal(mp.trangThai().mau.mo_ta, "không màu");
  mp.thaoTac("mo_khoa", { kieu: "nhanh" });
  chay(mp, 36);                                           // 0,5 mL/s × 36 s = 18 mL
  mp.thaoTac("dong_khoa");
  let s = mp.trangThai();
  assert.ok(s.V > 17.5 && s.V < 18.5, `V = ${s.V}`);
  assert.equal(s.V_doi_mau, null);
  assert.ok(s.buoc_xong[1], "bước chảy nhanh tới 15 mL xong");
  mp.thaoTac("lac");
  mp.thaoTac("mo_khoa", { kieu: "giot" });
  for (let i = 0; i < 2000 && mp.trangThai().V_doi_mau === null; i++) mp.capNhat(0.1);
  mp.thaoTac("dong_khoa");
  s = mp.trangThai();
  assert.ok(Math.abs(s.V_doi_mau - 20.0) <= 0.1, `điểm cuối ${s.V_doi_mau}`);
  assert.ok(s.mau.mo_ta.startsWith("hồng"));
  assert.ok(s.diem.length > 20 && s.diem.at(-1)[1] > 8, "đồ thị pH – V có dữ liệu");
  assert.ok(Math.abs(s.V_tuong_duong - 20) < 1e-9);
  assert.equal(mp.thaoTac("doc_buret", { gia_tri: s.doc_buret_that }).dung, true);
  assert.equal(mp.thaoTac("tinh_nong_do", { C: "0,100" }).dung, true);
  s = mp.trangThai();
  assert.equal(s.buoc_hien_tai, null, "xong mọi bước hướng dẫn");
  assert.ok(mp.soTay().some((m) => m.loai === "hien_tuong" && m.chu.includes("điểm cuối")));
  assert.ok(suKien.length > 10, "phát sự kiện trang_thai");
});

test("chuẩn độ CH₃COOH: pH ban đầu 2,88; chế độ Ôn thi ẩn nồng độ và chấm sai số", () => {
  const mp = M.tao(tn("chuan-do"), { ngauNhien: () => 0.5 });
  mp.thaoTac("chon_bai", { id: "ch3cooh" });
  assert.ok(Math.abs(mp.trangThai().pH - 2.88) <= 0.02);
  mp.thaoTac("bat_on_thi");
  let s = mp.trangThai();
  assert.equal(s.Ca, null);
  assert.equal(s.pH, null);
  assert.deepEqual(s.diem, []);
  const kq = mp.thaoTac("nop_ket_qua", { C: "0,100" });   // Ca thật = 0,06 + 0,5 × 0,08 = 0,100
  assert.equal(kq.ok, true);
  s = mp.trangThai();
  assert.equal(s.ket_qua_on_thi.diem, 10);
  assert.equal(s.Ca, 0.1);
});

test("nhận biết ion: 4 ống đã biết → thử đúng thuốc thử, đoán đúng cả 4 ống; Fe²⁺ để ngoài không khí hoá nâu đỏ", () => {
  const mp = M.tao(tn("nhan-biet-ion"), { bangIon });
  mp.thaoTac("bo_moi", { ion: ["Fe2+", "Cl-", "SO4_2-", "NH4+"] });
  mp.thaoTac("nho", { so: 1, thuoc_thu: "NaOH" });
  let o1 = mp.trangThai().ong[0];
  assert.equal(o1.ket_tua.mau, "trắng xanh");
  assert.equal(o1.ion, null, "chưa kiểm tra thì không lộ ion");
  chay(mp, 9, 0.25);
  o1 = mp.trangThai().ong[0];
  assert.equal(o1.ket_tua.mau, "nâu đỏ");
  mp.thaoTac("nho", { so: 2, thuoc_thu: "AgNO3" });
  assert.equal(mp.trangThai().ong[1].ket_tua.mau, "trắng");
  mp.thaoTac("nho", { so: 3, thuoc_thu: "BaCl2" });
  mp.thaoTac("nho", { so: 3, thuoc_thu: "HCl" });
  assert.match(mp.trangThai().ong[2].lich_su.at(-1), /không tan/);
  mp.thaoTac("nho", { so: 4, thuoc_thu: "NaOH" });
  assert.match(mp.trangThai().ong[3].lich_su.at(-1), /đun nóng/);
  mp.thaoTac("dun", { so: 4 });
  mp.thaoTac("thu_quy", { so: 4 });
  assert.equal(mp.trangThai().ong[3].quy, "xanh");
  for (const [so, ion] of [[1, "Fe2+"], [2, "Cl-"], [3, "SO4_2-"], [4, "NH4+"]]) mp.thaoTac("dat_du_doan", { so, ion });
  mp.thaoTac("kiem_tra");
  const kq = mp.trangThai().ket_qua;
  assert.equal(kq.dung, 4);
  assert.equal(kq.diem, 100);
  assert.equal(mp.trangThai().ong[0].ion, "Fe2+", "kiểm tra xong thì lộ ion");
  assert.ok(kq.chi_tiet[0].phan_ung.some((p) => p.pt_ion.includes("Fe(OH)₂")));
});

test("nhận biết ion: Al³⁺ tan trong NaOH dư; Cu²⁺ tan trong NH₃ dư thành xanh lam thẫm; CO₃²⁻ + HCl sủi bọt; thử ngọn lửa", () => {
  const mp = M.tao(tn("nhan-biet-ion"), { bangIon });
  mp.thaoTac("bo_moi", { ion: ["Al3+", "Cu2+", "CO3_2-", "Fe3+"] });
  mp.thaoTac("nho", { so: 1, thuoc_thu: "NaOH", kieu: "du" });
  assert.equal(mp.trangThai().ong[0].ket_tua, null);
  mp.thaoTac("nho", { so: 2, thuoc_thu: "NH3", kieu: "du" });
  assert.equal(mp.trangThai().ong[1].mau_dd.mo_ta, "xanh lam thẫm");
  mp.thaoTac("nho", { so: 3, thuoc_thu: "HCl" });
  assert.ok(mp.trangThai().ong[2].khi);
  mp.thaoTac("nho", { so: 4, thuoc_thu: "KSCN" });
  assert.equal(mp.trangThai().ong[3].mau_dd.mo_ta, "đỏ máu");
  mp.thaoTac("thu_lua", { kim_loai: "Na" });
  assert.equal(mp.trangThai().ngon_lua.mau, "vàng");
});

test("bộ ống ngẫu nhiên có 4–6 ống, cả cation lẫn anion", () => {
  for (let k = 0; k < 20; k++) {
    const mp = M.tao(tn("nhan-biet-ion"), { bangIon });
    const n = mp.trangThai().ong.length;
    assert.ok(n >= 4 && n <= 6);
  }
});

test("pin Zn – Cu: vôn kế 1,10 V, electron từ Zn sang Cu; rút cầu muối → 0 V", () => {
  const mp = M.tao(tn("pin-galvani"));
  let s = mp.trangThai();
  assert.ok(Math.abs(s.E_pin - 1.103) < 0.001);
  assert.equal(s.anode, "Zn");
  assert.equal(s.chieu_electron, "trai_sang_phai");
  mp.thaoTac("ghi_so_do");
  mp.thaoTac("rut_cau_muoi");
  s = mp.trangThai();
  assert.equal(s.E_pin, 0);
  assert.equal(s.chieu_electron, null);
  mp.thaoTac("chon_kim_loai", { ben: "trai", kim_loai: "Mg" });
  mp.thaoTac("chon_kim_loai", { ben: "phai", kim_loai: "Ag" });
  mp.thaoTac("lap_cau_muoi");
  assert.ok(Math.abs(mp.trangThai().E_pin - 3.169) < 0.001);
  mp.thaoTac("doi_nong_do", { ben: "phai", C: 0.01 });
  assert.equal(mp.trangThai().buoc_xong[3], true);
});

test("điện phân CuSO₄ 2 A trong 965 s (tua ×60) → 0,64 g Cu; đối chiếu Faraday", () => {
  const mp = M.tao(tn("dien-phan"));
  mp.thaoTac("dat_I", { I: 2 });
  mp.thaoTac("dat_t", { t: 965 });
  mp.thaoTac("tua", { toc_do: 60 });
  mp.thaoTac("bat_nguon");
  chay(mp, 20, 0.1);
  const s = mp.trangThai();
  assert.equal(s.xong, true);
  assert.ok(Math.abs(s.ket_qua.m_cathode - 0.64) <= 0.02, `m = ${s.ket_qua.m_cathode}`);
  assert.ok(Math.abs(s.ket_qua.V_khi_anode - 0.124) <= 0.02);
  assert.equal(mp.thaoTac("doi_chieu", { gia_tri: "0,64" }).dung, true);
  assert.equal(mp.trangThai().buoc_hien_tai, null);
});

test("an toàn: tìm đủ 8 lỗi, ghép đúng 9 GHS, đọc 3 sự cố → huy hiệu Nhập môn", () => {
  const d = tn("an-toan");
  const mp = M.tao(d);
  assert.equal(mp.thaoTac("bam_diem", { id: "tu_hut" }).loai, "dung");
  for (const l of d.cau_hinh.loi) mp.thaoTac("bam_diem", { id: l.id });
  assert.equal(mp.trangThai().so_tim, 8);
  mp.thaoTac("ghep_ghs", { tra_loi: Object.fromEntries(d.cau_hinh.ghs.map((g) => [g.id, g.id])) });
  for (const sc of d.cau_hinh.su_co) mp.thaoTac("xem_su_co", { id: sc.id });
  assert.equal(mp.trangThai().huy_hieu, true);
  mp.thaoTac("nop_cau_hoi", { dung: 5, tong: 6 });
  assert.equal(mp.trangThai().buoc_hien_tai, null);
});
