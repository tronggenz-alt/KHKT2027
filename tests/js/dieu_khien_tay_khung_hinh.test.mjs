import test from 'node:test';
import assert from 'node:assert/strict';
import {
  tinhVungCat, doiVeKhungDay, kichThuocTay, ThongKeTay, goiYNhanDien, NGUONG_TAY_XA
} from '../../frontend/dieu-khien-tay/khung-hinh.js';

const gan = (a, b, sai = 1e-9) => assert.ok(Math.abs(a - b) <= sai, `${a} ≠ ${b}`);

test('Cắt 4:3 ở giữa khung 16:9; khung đã 4:3 (hoặc hẹp hơn) giữ nguyên', () => {
  assert.deepEqual(tinhVungCat(1280, 720), { sx: 160, sy: 0, sw: 960, sh: 720 });
  assert.deepEqual(tinhVungCat(1920, 1080), { sx: 240, sy: 0, sw: 1440, sh: 1080 });
  assert.deepEqual(tinhVungCat(640, 480), { sx: 0, sy: 0, sw: 640, sh: 480 });
  assert.deepEqual(tinhVungCat(480, 640), { sx: 0, sy: 0, sw: 480, sh: 640 });
  assert.deepEqual(tinhVungCat(0, 0), { sx: 0, sy: 0, sw: 0, sh: 0 });
});

test('Vùng cắt không làm mất vùng tay hay hoạt động (VUNG x 0,15–0,85)', () => {
  const c = tinhVungCat(1280, 720);
  assert.ok(c.sx / 1280 <= 0.15 && (c.sx + c.sw) / 1280 >= 0.85);
});

test('Đổi toạ độ khung cắt → khung đầy đủ: góc, giữa, z, tay trái/phải, toạ độ 3D giữ nguyên', () => {
  const cat = tinhVungCat(1280, 720);
  const the_gioi = Array.from({ length: 21 }, (_, i) => ({ x: i * 0.001, y: 0, z: 0 }));
  const diem = Array.from({ length: 21 }, () => ({ x: 0.5, y: 0.5, z: 0.1 }));
  diem[0] = { x: 0, y: 0, z: 0 };
  diem[1] = { x: 1, y: 1, z: 0 };
  const ra = doiVeKhungDay({ landmarks: [diem], worldLandmarks: [the_gioi],
                             handedness: [[{ categoryName: 'Right', score: 0.9 }]] }, cat, 1280, 720);
  gan(ra.hand_landmarks[0][0].x, 0.125); gan(ra.hand_landmarks[0][0].y, 0);
  gan(ra.hand_landmarks[0][1].x, 0.875); gan(ra.hand_landmarks[0][1].y, 1);
  gan(ra.hand_landmarks[0][2].x, 0.5);   gan(ra.hand_landmarks[0][2].z, 0.075);
  assert.equal(ra.handedness[0][0].categoryName, 'Right');
  assert.equal(ra.handedness[0][0].category_name, 'Right');
  assert.equal(ra.hand_world_landmarks[0], the_gioi);
});

test('Một điểm bất kỳ: khung đầy đủ → khung cắt → đổi lại cho đúng vị trí cũ', () => {
  const cat = tinhVungCat(1280, 720);
  for (const [x, y] of [[0.2, 0.3], [0.5, 0.5], [0.8, 0.9]]) {
    const xc = (x * 1280 - cat.sx) / cat.sw, yc = (y * 720 - cat.sy) / cat.sh;
    const ra = doiVeKhungDay({ landmarks: [[{ x: xc, y: yc, z: 0 }]], worldLandmarks: [], handedness: [[{ categoryName: 'Left' }]] }, cat, 1280, 720);
    gan(ra.hand_landmarks[0][0].x, x, 1e-12); gan(ra.hand_landmarks[0][0].y, y, 1e-12);
    assert.deepEqual(ra.hand_world_landmarks, []);  // thiếu toạ độ 3D → để rỗng, docTay tự dùng 2D
  }
});

test('Kết quả rỗng / hỏng không ném lỗi', () => {
  const cat = tinhVungCat(1280, 720);
  assert.deepEqual(doiVeKhungDay(null, cat, 1280, 720), { hand_landmarks: [], hand_world_landmarks: [], handedness: [] });
  assert.deepEqual(doiVeKhungDay({}, cat, 1280, 720).hand_landmarks, []);
  assert.equal(kichThuocTay([], 16 / 9), 0);
});

test('Cỡ tay: cổ tay → gốc ngón giữa, x nhân tỉ lệ khung', () => {
  const diem = Array.from({ length: 21 }, () => ({ x: 0, y: 0 }));
  diem[0] = { x: 0.5, y: 0.6 }; diem[9] = { x: 0.5 + 0.06 / (16 / 9), y: 0.6 - 0.08 };
  gan(kichThuocTay(diem, 16 / 9), 0.1, 1e-12);
});

test('Thống kê 3 giây: tần số camera, tần số nhận diện, tỉ lệ thấy tay, số giây không thấy tay', () => {
  const tk = new ThongKeTay();
  for (let i = 0; i <= 90; i++) tk.ghiKhungCamera(i / 30);          // 30 hình/giây trong 3 s
  for (let i = 0; i <= 40; i++) tk.ghiNhanDien(i / 20, i % 2);       // 20 lần/giây, thấy tay một nửa số lần
  gan(tk.tanSoCamera, 30, 1e-9);
  gan(tk.tanSoNhanDien, 20, 1e-9);
  assert.ok(Math.abs(tk.tiLeThayTay - 0.5) < 0.03);
  for (let i = 41; i <= 100; i++) tk.ghiNhanDien(i / 20, 0);         // 3 s không thấy tay
  gan(tk.giayKhongThayTay(100 / 20), 100 / 20 - 39 / 20, 1e-9);
  assert.equal(tk.tiLeThayTay, 0);
  assert.ok(tk.nhanDien.length <= 61);                                // chỉ giữ cửa sổ 3 s
});

test('Gợi ý: tối → camera chậm → tay xa → không thấy tay → ổn (theo thứ tự ưu tiên)', () => {
  assert.match(goiYNhanDien({ doSang: 40, tanSoCamera: 12, kichThuocNhoNhat: 0.05, giayKhongThayTay: 5 }), /tối/);
  assert.match(goiYNhanDien({ doSang: 120, tanSoCamera: 12 }), /12 hình\/giây/);
  assert.match(goiYNhanDien({ doSang: 120, tanSoCamera: 30, kichThuocNhoNhat: NGUONG_TAY_XA - 0.01 }), /xa/);
  assert.match(goiYNhanDien({ doSang: 120, tanSoCamera: 30, kichThuocNhoNhat: null, giayKhongThayTay: 2.5 }), /Chưa thấy tay/);
  assert.equal(goiYNhanDien({ doSang: 120, tanSoCamera: 30, kichThuocNhoNhat: 0.2, giayKhongThayTay: 0 }), null);
  assert.equal(goiYNhanDien({ doSang: -1, tanSoCamera: 0 }), null);   // chưa đo được thì không báo bừa
});
