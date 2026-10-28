import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "../..");
const BAN_DO_JSON_PATH = path.join(ROOT, "frontend/hoa-hoc/phong-thi-nghiem/ban-do.json");
const BAN_DO_JS_PATH = path.join(ROOT, "frontend/hoa-hoc/phong-thi-nghiem/ban-do.js");

const banDo = JSON.parse(fs.readFileSync(BAN_DO_JSON_PATH, "utf-8"));

// Nạp UMD module
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const PTNBanDo = require(BAN_DO_JS_PATH);

test("kiemTra(banDo) trả về rỗng, có đủ 5 trạm nằm đúng khu", () => {
  const loi = PTNBanDo.kiemTra(banDo);
  assert.deepEqual(loi, [], `Bản đồ có lỗi: ${loi.join("; ")}`);

  assert.equal(banDo.tram.length, 5);
  const dsTN = ["an-toan", "chuan-do", "nhan-biet-ion", "pin-galvani", "dien-phan"];
  const coTN = banDo.tram.map((t) => t.tn);
  assert.deepEqual(coTN.sort(), dsTN.sort());

  for (const t of banDo.tram) {
    const k = banDo.khu.find((kh) => kh.id === t.khu);
    assert.ok(k, `Trạm ${t.tn} phải có khu`);
    assert.ok(t.tam[0] >= k.hcn[0] && t.tam[0] <= k.hcn[2], `${t.tn} tam.x trong khu`);
    assert.ok(t.tam[1] >= k.hcn[1] && t.tam[1] <= k.hcn[3], `${t.tn} tam.z trong khu`);
    assert.ok(t.diem_dung[0] >= k.hcn[0] && t.diem_dung[0] <= k.hcn[2], `${t.tn} diem_dung.x trong khu`);
    assert.ok(t.diem_dung[1] >= k.hcn[1] && t.diem_dung[1] <= k.hcn[3], `${t.tn} diem_dung.z trong khu`);
  }
});

test("timDuong từ xuat_phat tới diem_dung của từng trạm khác null và không cắt qua vật cản", () => {
  const luoi = PTNBanDo.taoLuoi(banDo, { o: 0.25, banKinhNguoi: 0.3 });
  const xp = [banDo.xuat_phat.x, banDo.xuat_phat.z];

  for (const t of banDo.tram) {
    const duong = PTNBanDo.timDuong(luoi, xp, t.diem_dung);
    assert.ok(duong, `Phải tìm được đường tới trạm ${t.tn}`);
    assert.ok(duong.length >= 2, `Đường đi tới ${t.tn} phải có ít nhất 2 điểm`);

    // Điểm đầu và điểm cuối
    assert.ok(Math.hypot(duong[0][0] - xp[0], duong[0][1] - xp[1]) < 0.5, "Điểm đầu gần xuất phát");
    assert.ok(Math.hypot(duong[duong.length - 1][0] - t.diem_dung[0], duong[duong.length - 1][1] - t.diem_dung[1]) < 0.5, "Điểm cuối gần trạm");

    // Mọi điểm trên đường và đoạn thẳng nối không cắt qua vật cản
    for (let i = 0; i < duong.length - 1; i++) {
      const p1 = duong[i];
      const p2 = duong[i + 1];
      const d = Math.hypot(p2[0] - p1[0], p2[1] - p1[1]);
      const buoc = 0.1;
      const n = Math.max(1, Math.ceil(d / buoc));
      for (let s = 0; s <= n; s++) {
        const px = p1[0] + (p2[0] - p1[0]) * (s / n);
        const pz = p1[1] + (p2[1] - p1[1]) * (s / n);
        assert.ok(!luoi.laChanViTri(px, pz), `Đoạn nối tại [${px.toFixed(2)}, ${pz.toFixed(2)}] không được chạm vật cản`);
      }
    }
  }
});

test("diChuyen lao thẳng vào bàn thì bị chặn và trượt dọc mép; bước lớn không xuyên tường", () => {
  // Chuẩn độ tại tam [3.5, 11.5], bán kính bàn x trong [2.2, 4.8], z trong [10.875, 12.125]
  // Thử đi từ diem_dung [3.5, 12.7] lao thẳng vào bàn theo hướng Bắc (dz = -2.0)
  const pos = [3.5, 12.7];
  const moi = PTNBanDo.diChuyen(banDo, pos, [0, -2.0], 0.3);
  // Phải bị chặn lại ở mép Nam của bàn: 12.125 + 0.3 = 12.425
  assert.ok(moi[1] >= 12.4, `Không được xuyên vào bàn, z mới = ${moi[1]}`);
  assert.equal(moi[0], 3.5, "Không dịch chuyển x khi đi thẳng");

  // Đi chéo vào bàn: [dx=1.0, dz=-2.0], phải trượt dọc theo trục X
  const truot = PTNBanDo.diChuyen(banDo, pos, [1.0, -2.0], 0.3);
  assert.ok(truot[1] >= 12.4, `Trượt z chặn ở mép bàn, z = ${truot[1]}`);
  assert.ok(truot[0] > 3.5, `X phải trượt sang phải, x = ${truot[0]}`);

  // Bước cực lớn (nhảy vọt 50 m ra ngoài tường)
  const ngoai = PTNBanDo.diChuyen(banDo, [11, 8], [50, 50], 0.3);
  assert.ok(ngoai[0] <= banDo.phong.rong - 0.3, "Không xuyên tường Đông");
  assert.ok(ngoai[1] <= banDo.phong.sau - 0.3, "Không xuyên tường Nam");
  assert.ok(ngoai[0] >= 0.3, "Trong giới hạn x");
  assert.ok(ngoai[1] >= 0.3, "Trong giới hạn z");
});

test("tramGanNhat đúng ngay tại diem_dung và trả null ở giữa lối đi chính; khuTai đúng tại tâm mỗi khu", () => {
  for (const t of banDo.tram) {
    const gan = PTNBanDo.tramGanNhat(banDo, t.diem_dung);
    assert.ok(gan, `Tại diem_dung của ${t.tn} phải tìm thấy trạm`);
    assert.equal(gan.tn, t.tn);
    assert.ok(gan.khoangCach <= t.ban_kinh_tuong_tac);
  }

  // Tại giữa lối đi chính (x=11, z=8): cách xa mọi bàn
  const giua = PTNBanDo.tramGanNhat(banDo, [11, 8]);
  assert.equal(giua, null, "Giữa lối đi chính không gần bàn nào");

  // Kiểm tra khuTai tại tâm các khu
  for (const k of banDo.khu) {
    const tamX = (k.hcn[0] + k.hcn[2]) / 2;
    const tamZ = (k.hcn[1] + k.hcn[3]) / 2;
    assert.equal(PTNBanDo.khuTai(banDo, [tamX, tamZ]), k.id, `Tâm khu ${k.id} phải trả về đúng id`);
  }
});

test("lối vào phòng con đủ rộng >= 1.6 m, điểm xuất phát mới hợp lệ, layVatChanCamera đầy đủ", () => {
  // Điểm xuất phát mới
  assert.equal(banDo.xuat_phat.x, 11);
  assert.equal(banDo.xuat_phat.z, 13.6);
  const luoi = PTNBanDo.taoLuoi(banDo, { o: 0.25, banKinhNguoi: 0.3 });
  assert.equal(luoi.laChanViTri(banDo.xuat_phat.x, banDo.xuat_phat.z), false, "Điểm xuất phát phải thông thoáng");

  // Phòng con
  assert.ok(Array.isArray(banDo.phong_con), "banDo phải có mảng phong_con");
  assert.ok(banDo.phong_con.length >= 4, "Có ít nhất 4 phòng con");
  for (const pc of banDo.phong_con) {
    assert.ok(pc.loi_vao, `Phòng con ${pc.khu} phải có loi_vao`);
    assert.ok(pc.loi_vao.rong >= 1.6, `Lối vào phòng con ${pc.khu} phải rộng >= 1.6 m (thực tế: ${pc.loi_vao.rong})`);
    assert.ok(Array.isArray(pc.vach) && pc.vach.length > 0, `Phòng con ${pc.khu} phải có các đoạn vách`);
  }

  // Hộp chắn camera 3D
  const hopCam = PTNBanDo.layVatChanCamera(banDo);
  assert.ok(Array.isArray(hopCam) && hopCam.length >= 4, "Phải có ít nhất 4 tường bao chắn camera");
  for (const h of hopCam) {
    assert.ok(h.minX < h.maxX, "minX < maxX");
    assert.ok(h.minZ < h.maxZ, "minZ < maxZ");
    assert.ok(h.minY < h.maxY, "minY < maxY");
  }
});
