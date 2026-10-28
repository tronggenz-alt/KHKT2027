import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Import module UMD
import cuChiModule from '../../frontend/dieu-khien-tay/cu-chi.js';
const { HANG_SO, DieuKhien, docTay } = cuChiModule;

const duLieuVangPath = path.join(__dirname, 'du_lieu', 'dieu_khien_tay_vang.json');
const duLieuVang = JSON.parse(fs.readFileSync(duLieuVangPath, 'utf8'));

function doiKhungVangSangMediaPipe(dsTay) {
  const hand_landmarks = [];
  const hand_world_landmarks = [];
  const handedness = [];

  for (const h of dsTay) {
    hand_landmarks.push(h.diem.map(p => ({ x: p[0], y: p[1], z: p[2] })));
    hand_world_landmarks.push(h.theGioi.map(p => ({ x: p[0], y: p[1], z: p[2] })));
    handedness.push([{ category_name: h.ben, categoryName: h.ben }]);
  }

  return { hand_landmarks, hand_world_landmarks, handedness };
}

function soKhopSo(thucTe, kyVong, saiSo = 1e-6) {
  return Math.abs(thucTe - kyVong) <= saiSo;
}

test('Kiểm tra HANG_SO khớp hoàn toàn với dữ liệu vàng', () => {
  const hsVang = duLieuVang.hang_so;
  assert.equal(HANG_SO.CHAM_BAT, hsVang.CHAM_BAT);
  assert.equal(HANG_SO.CHAM_NHA, hsVang.CHAM_NHA);
  assert.ok(soKhopSo(HANG_SO.GOC_DAU[0], hsVang.GOC_DAU[0]));
  assert.ok(soKhopSo(HANG_SO.GOC_DAU[1], hsVang.GOC_DAU[1]));
  assert.equal(HANG_SO.ZOOM_MIN, hsVang.ZOOM_MIN);
  assert.equal(HANG_SO.ZOOM_MAX, hsVang.ZOOM_MAX);
  assert.ok(soKhopSo(HANG_SO.PITCH_MAX, hsVang.PITCH_MAX));
  assert.equal(HANG_SO.DO_NHAY, hsVang.DO_NHAY);
  assert.equal(HANG_SO.NGUONG_TRUOT, hsVang.NGUONG_TRUOT);
  assert.equal(HANG_SO.QT_NGUONG, hsVang.QT_NGUONG);
  assert.equal(HANG_SO.QT_TAT, hsVang.QT_TAT);
  assert.equal(HANG_SO.QT_MAX, hsVang.QT_MAX);
  assert.equal(HANG_SO.MAT_TAY_S, hsVang.MAT_TAY_S);
  assert.equal(HANG_SO.XOE_GIU_S, hsVang.XOE_GIU_S);
  assert.equal(HANG_SO.ONE_EURO.minCutoff, hsVang.ONE_EURO.minCutoff);
  assert.equal(HANG_SO.ONE_EURO.beta, hsVang.ONE_EURO.beta);
  assert.equal(HANG_SO.ONE_EURO.dCutoff, hsVang.ONE_EURO.dCutoff);
});

const danhSachKichBan = Object.keys(duLieuVang.kich_ban);
test(`Kiểm tra dữ liệu vàng đủ 9 kịch bản (${danhSachKichBan.length}/9)`, () => {
  assert.equal(danhSachKichBan.length, 9);
});

for (const tenKichBan of danhSachKichBan) {
  test(`Kịch bản vàng: ${tenKichBan}`, () => {
    const kb = duLieuVang.kich_ban[tenKichBan];
    const dk = new DieuKhien();
    const cacTay = {};

    assert.equal(kb.vao.length, kb.ra.length, `Số khung vào/ra không khớp ở ${tenKichBan}`);

    for (let i = 0; i < kb.vao.length; i++) {
      const vao = kb.vao[i];
      const raKyVong = kb.ra[i];

      const mpData = doiKhungVangSangMediaPipe(vao.tay);
      const dsTay = docTay(mpData, vao.tiLe, vao.t, cacTay);
      dk.capNhat(vao.t, vao.dt, dsTay);

      // So khớp từng tay
      assert.equal(dsTay.length, raKyVong.tay.length,
        `${tenKichBan} khung #${i}: Số tay thực tế (${dsTay.length}) khác kỳ vọng (${raKyVong.tay.length})`);

      for (let j = 0; j < dsTay.length; j++) {
        const tayTT = dsTay[j];
        const tayKV = raKyVong.tay[j];

        assert.equal(tayTT.ben, tayKV.ben,
          `${tenKichBan} khung #${i} tay #${j}: 'ben' lệch: ${tayTT.ben} vs ${tayKV.ben}`);

        assert.ok(soKhopSo(tayTT.conTro[0], tayKV.conTro[0]),
          `${tenKichBan} khung #${i} tay #${j}: conTro[0] lệch: ${tayTT.conTro[0]} vs ${tayKV.conTro[0]}`);

        assert.ok(soKhopSo(tayTT.conTro[1], tayKV.conTro[1]),
          `${tenKichBan} khung #${i} tay #${j}: conTro[1] lệch: ${tayTT.conTro[1]} vs ${tayKV.conTro[1]}`);

        assert.equal(tayTT.cham, tayKV.cham,
          `${tenKichBan} khung #${i} tay #${j}: 'cham' lệch: ${tayTT.cham} vs ${tayKV.cham}`);

        assert.ok(soKhopSo(tayTT.tl, tayKV.tl),
          `${tenKichBan} khung #${i} tay #${j}: 'tl' lệch: ${tayTT.tl} vs ${tayKV.tl}`);

        assert.equal(tayTT.xoe, tayKV.xoe,
          `${tenKichBan} khung #${i} tay #${j}: 'xoe' lệch: ${tayTT.xoe} vs ${tayKV.xoe}`);
      }

      // So khớp DieuKhien
      assert.ok(soKhopSo(dk.yaw, raKyVong.yaw),
        `${tenKichBan} khung #${i}: 'yaw' lệch: ${dk.yaw} vs ${raKyVong.yaw}`);

      assert.ok(soKhopSo(dk.pitch, raKyVong.pitch),
        `${tenKichBan} khung #${i}: 'pitch' lệch: ${dk.pitch} vs ${raKyVong.pitch}`);

      assert.ok(soKhopSo(dk.zoom, raKyVong.zoom),
        `${tenKichBan} khung #${i}: 'zoom' lệch: ${dk.zoom} vs ${raKyVong.zoom}`);

      assert.equal(dk.cheDo, raKyVong.cheDo,
        `${tenKichBan} khung #${i}: 'cheDo' lệch: ${dk.cheDo} vs ${raKyVong.cheDo}`);

      assert.ok(soKhopSo(dk.wYaw, raKyVong.wYaw),
        `${tenKichBan} khung #${i}: 'wYaw' lệch: ${dk.wYaw} vs ${raKyVong.wYaw}`);

      assert.ok(soKhopSo(dk.wPitch, raKyVong.wPitch),
        `${tenKichBan} khung #${i}: 'wPitch' lệch: ${dk.wPitch} vs ${raKyVong.wPitch}`);

      assert.equal(dk.veDau, raKyVong.veDau,
        `${tenKichBan} khung #${i}: 'veDau' lệch: ${dk.veDau} vs ${raKyVong.veDau}`);
    }
  });
}
