import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

import cuChiModule from '../../frontend/dieu-khien-tay/cu-chi.js';
const { HANG_SO, DieuKhien, docTay, VUNG } = cuChiModule;

const duLieuVangPath = path.join(__dirname, 'du_lieu', 'dieu_khien_tay_vang.json');
const duLieuVang = JSON.parse(fs.readFileSync(duLieuVangPath, 'utf8'));

// Hàm tạo bàn tay giả đơn giản
function taoTayGia(cx, cy, cham, ben = 'Right') {
  // 21 điểm
  const diem = [];
  const theGioi = [];
  for (let i = 0; i < 21; i++) {
    diem.push({ x: cx, y: cy, z: 0 });
    theGioi.push({ x: 0, y: 0, z: 0 });
  }
  // Nếu chạm, ngón cái (4) và trỏ (8) gần nhau, cổ tay (0) và gốc giữa (9) xa nhau
  diem[0] = { x: cx, y: cy, z: 0 };
  diem[9] = { x: cx, y: cy - 0.2, z: 0 };
  theGioi[0] = { x: 0, y: 0, z: 0 };
  theGioi[9] = { x: 0, y: -0.2, z: 0 };

  if (cham) {
    diem[4] = { x: cx, y: cy, z: 0 };
    diem[8] = { x: cx, y: cy, z: 0 };
    theGioi[4] = { x: 0, y: 0, z: 0 };
    theGioi[8] = { x: 0, y: 0, z: 0 };
  } else {
    diem[4] = { x: cx - 0.1, y: cy, z: 0 };
    diem[8] = { x: cx + 0.1, y: cy, z: 0 };
    theGioi[4] = { x: -0.1, y: 0, z: 0 };
    theGioi[8] = { x: 0.1, y: 0, z: 0 };
  }

  return {
    hand_landmarks: [diem],
    hand_world_landmarks: [theGioi],
    handedness: [[{ categoryName: ben }]]
  };
}

test('Mở rộng 1: Chạm nhẹ 0,2 s không kéo → đúng một cham_chon, toạ độ đúng theo VUNG', () => {
  const suKien = [];
  const dk = new DieuKhien({ khiSuKien: (sk) => suKien.push(sk) });
  const cacTay = {};
  const tiLe = 16 / 9;
  const T = 1 / 30;

  // Điểm cx = 0.5, cy = 0.5
  // VUNG = [[0.15, 0.85], [0.12, 0.82]]
  // x_cam = 0.5, y_cam = 0.5
  // x_vung = (0.5 - 0.15) / (0.85 - 0.15) = 0.35 / 0.70 = 0.5
  // y_vung = (0.5 - 0.12) / (0.82 - 0.12) = 0.38 / 0.70 ≈ 0.542857

  let t = 0;
  // Khung 1, 2: chụm để bắt đầu chạm (cần 2 khung liên tiếp)
  for (let f = 0; f < 2; f++) {
    t += T;
    const mp = taoTayGia(0.5, 0.5, true);
    const ds = docTay(mp, tiLe, t, cacTay);
    dk.capNhat(t, T, ds);
  }

  // Giữ chụm trong 0.2 s (khoảng 6 khung)
  for (let f = 0; f < 6; f++) {
    t += T;
    const mp = taoTayGia(0.5, 0.5, true);
    const ds = docTay(mp, tiLe, t, cacTay);
    dk.capNhat(t, T, ds);
  }

  // Nhả chụm (2 khung liên tiếp nhả)
  for (let f = 0; f < 2; f++) {
    t += T;
    const mp = taoTayGia(0.5, 0.5, false);
    const ds = docTay(mp, tiLe, t, cacTay);
    dk.capNhat(t, T, ds);
  }

  const chamChonList = suKien.filter(s => s.kieu === 'cham_chon');
  assert.equal(chamChonList.length, 1, `Kỳ vọng đúng 1 cham_chon nhưng nhận ${chamChonList.length}`);
  const cc = chamChonList[0];
  assert.ok(Math.abs(cc.x - 0.5) < 0.05, `Toạ độ x ${cc.x} lệch quá nhiều so với 0.5`);
  assert.ok(Math.abs(cc.y - 0.542857) < 0.05, `Toạ độ y ${cc.y} lệch quá nhiều so với 0.54`);
});

test('Mở rộng 2: Chụm 0,6 s không kéo → không có cham_chon', () => {
  const suKien = [];
  const dk = new DieuKhien({ khiSuKien: (sk) => suKien.push(sk) });
  const cacTay = {};
  const tiLe = 16 / 9;
  const T = 1 / 30;

  let t = 0;
  // Chụm 0.6s (18 khung)
  for (let f = 0; f < 20; f++) {
    t += T;
    const mp = taoTayGia(0.5, 0.5, true);
    const ds = docTay(mp, tiLe, t, cacTay);
    dk.capNhat(t, T, ds);
  }

  // Nhả
  for (let f = 0; f < 3; f++) {
    t += T;
    const mp = taoTayGia(0.5, 0.5, false);
    const ds = docTay(mp, tiLe, t, cacTay);
    dk.capNhat(t, T, ds);
  }

  const chamChonList = suKien.filter(s => s.kieu === 'cham_chon');
  assert.equal(chamChonList.length, 0, `Chụm lâu > 0.3s không được phát cham_chon`);
});

test('Mở rộng 3: Chụm rồi kéo → không có cham_chon', () => {
  const suKien = [];
  const dk = new DieuKhien({ khiSuKien: (sk) => suKien.push(sk) });
  const cacTay = {};
  const tiLe = 16 / 9;
  const T = 1 / 30;

  let t = 0;
  // Kéo di chuyển xa > NGUONG_TRUOT
  for (let f = 0; f < 5; f++) {
    t += T;
    const mp = taoTayGia(0.4 + f * 0.03, 0.5, true);
    const ds = docTay(mp, tiLe, t, cacTay);
    dk.capNhat(t, T, ds);
  }

  // Nhả
  for (let f = 0; f < 3; f++) {
    t += T;
    const mp = taoTayGia(0.55, 0.5, false);
    const ds = docTay(mp, tiLe, t, cacTay);
    dk.capNhat(t, T, ds);
  }

  const chamChonList = suKien.filter(s => s.kieu === 'cham_chon');
  assert.equal(chamChonList.length, 0, `Đã kéo thì không được phát cham_chon`);
});

test('Mở rộng 4: bat_dau / ket_thuc đúng thứ tự, mỗi thứ một lần, cho một lần kéo có lướt', () => {
  const suKien = [];
  const dk = new DieuKhien({ khiSuKien: (sk) => suKien.push(sk) });
  const cacTay = {};
  const tiLe = 16 / 9;
  const T = 1 / 30;

  // Lấy kịch bản keo_luot từ dữ liệu vàng
  const kb = duLieuVang.kich_ban['keo_luot'];
  for (let i = 0; i < kb.vao.length; i++) {
    const vao = kb.vao[i];
    const mpData = {
      hand_landmarks: vao.tay.map(h => h.diem.map(p => ({ x: p[0], y: p[1], z: p[2] }))),
      hand_world_landmarks: vao.tay.map(h => h.theGioi.map(p => ({ x: p[0], y: p[1], z: p[2] }))),
      handedness: vao.tay.map(h => [{ categoryName: h.ben }])
    };
    const ds = docTay(mpData, vao.tiLe, vao.t, cacTay);
    dk.capNhat(vao.t, vao.dt, ds);
  }

  const dsBatDau = suKien.filter(s => s.kieu === 'bat_dau');
  const dsKetThuc = suKien.filter(s => s.kieu === 'ket_thuc');

  assert.equal(dsBatDau.length, 1, `Phải có đúng 1 lần bat_dau, thực tế: ${dsBatDau.length}`);
  assert.equal(dsKetThuc.length, 1, `Phải có đúng 1 lần ket_thuc, thực tế: ${dsKetThuc.length}`);

  const idxBatDau = suKien.findIndex(s => s.kieu === 'bat_dau');
  const idxKetThuc = suKien.findIndex(s => s.kieu === 'ket_thuc');
  assert.ok(idxBatDau < idxKetThuc, `bat_dau phải xảy ra trước ket_thuc`);
});

test('Mở rộng 5: veGocDauNoiBo: false → xoè hai tay 1 giây chỉ phát dat_lai; yaw, pitch, zoom không tự đổi', () => {
  const suKien = [];
  const dk = new DieuKhien({
    veGocDauNoiBo: false,
    khiSuKien: (sk) => suKien.push(sk)
  });
  const cacTay = {};

  // Lấy kịch bản xoe_dat_lai
  const kb = duLieuVang.kich_ban['xoe_dat_lai'];
  let yawTruocDatLai = null;

  for (let i = 0; i < kb.vao.length; i++) {
    const vao = kb.vao[i];
    const mpData = {
      hand_landmarks: vao.tay.map(h => h.diem.map(p => ({ x: p[0], y: p[1], z: p[2] }))),
      hand_world_landmarks: vao.tay.map(h => h.theGioi.map(p => ({ x: p[0], y: p[1], z: p[2] }))),
      handedness: vao.tay.map(h => [{ categoryName: h.ben }])
    };
    const ds = docTay(mpData, vao.tiLe, vao.t, cacTay);
    dk.capNhat(vao.t, vao.dt, ds);

    if (suKien.some(s => s.kieu === 'dat_lai') && yawTruocDatLai === null) {
      yawTruocDatLai = dk.yaw;
    }
  }

  const datLaiEvents = suKien.filter(s => s.kieu === 'dat_lai');
  assert.equal(datLaiEvents.length, 1, `Phải phát sự kiện dat_lai đúng 1 lần`);
  assert.equal(dk.veDau, false, `veDau phải là false khi veGocDauNoiBo = false`);
  // Sau khi phát dat_lai, góc yaw không bị hoạt cảnh kéo về GOC_DAU mà giữ nguyên vị trí trước đó
  assert.ok(yawTruocDatLai !== null, `Phải bắt được sự kiện dat_lai`);
  assert.equal(dk.yaw, yawTruocDatLai, `Góc yaw không được tự thay đổi khi veGocDauNoiBo = false`);
});

test('Mở rộng 6: Bật các sự kiện mà chạy lại 9 kịch bản vàng → số liệu vẫn khớp hoàn toàn', () => {
  for (const tenKichBan of Object.keys(duLieuVang.kich_ban)) {
    const kb = duLieuVang.kich_ban[tenKichBan];
    const suKien = [];
    const dk = new DieuKhien({ khiSuKien: (sk) => suKien.push(sk) });
    const cacTay = {};

    for (let i = 0; i < kb.vao.length; i++) {
      const vao = kb.vao[i];
      const raKyVong = kb.ra[i];

      const mpData = {
        hand_landmarks: vao.tay.map(h => h.diem.map(p => ({ x: p[0], y: p[1], z: p[2] }))),
        hand_world_landmarks: vao.tay.map(h => h.theGioi.map(p => ({ x: p[0], y: p[1], z: p[2] }))),
        handedness: vao.tay.map(h => [{ categoryName: h.ben }])
      };
      const ds = docTay(mpData, vao.tiLe, vao.t, cacTay);
      dk.capNhat(vao.t, vao.dt, ds);

      assert.ok(Math.abs(dk.yaw - raKyVong.yaw) <= 1e-6,
        `${tenKichBan} khung #${i} yaw không khớp khi bật sự kiện`);
      assert.ok(Math.abs(dk.pitch - raKyVong.pitch) <= 1e-6,
        `${tenKichBan} khung #${i} pitch không khớp khi bật sự kiện`);
      assert.ok(Math.abs(dk.zoom - raKyVong.zoom) <= 1e-6,
        `${tenKichBan} khung #${i} zoom không khớp khi bật sự kiện`);
      assert.equal(dk.cheDo, raKyVong.cheDo);
    }
  }
});

test('Mở rộng 7: Đầu vào hỏng (mảng rỗng, thiếu điểm, NaN) → không ném lỗi', () => {
  const dk = new DieuKhien();
  const cacTay = {};

  assert.doesNotThrow(() => {
    // 1. Rỗng
    docTay({ hand_landmarks: [], hand_world_landmarks: [], handedness: [] }, 1.77, 0.1, cacTay);
    dk.capNhat(0.1, 0.033, []);

    // 2. null / undefined thuộc tính
    docTay({ hand_landmarks: [[]], hand_world_landmarks: null, handedness: null }, 1.77, 0.2, cacTay);
    dk.capNhat(0.2, 0.033, []);

    // 3. Thiếu điểm (không đủ 21 điểm)
    const diemThieu = [{ x: 0.5, y: 0.5, z: 0 }];
    docTay({ hand_landmarks: [diemThieu], hand_world_landmarks: null, handedness: [[{ categoryName: 'Right' }]] }, 1.77, 0.3, cacTay);

    // 4. Toạ độ NaN
    const diemNaN = Array.from({ length: 21 }, () => ({ x: NaN, y: NaN, z: NaN }));
    docTay({ hand_landmarks: [diemNaN], hand_world_landmarks: null, handedness: [[{ categoryName: 'Right' }]] }, 1.77, 0.4, cacTay);
  });
});
