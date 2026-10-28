import test from "node:test";
import assert from "node:assert/strict";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "../..");
const DIEU_KHIEN_JS_PATH = path.join(ROOT, "frontend/hoa-hoc/phong-thi-nghiem/dieu-khien.js");

const require = createRequire(import.meta.url);
const PTNDieuKhien = require(DIEU_KHIEN_JS_PATH);

test("Với mọi yaw, huongDiChuyen({W}) cùng hướng với hướng camera chiếu ngang (tích vô hướng > 0,99); A/D vuông góc", () => {
  const cacGoc = [0, Math.PI / 4, Math.PI / 2, Math.PI, -Math.PI / 2, -Math.PI * 0.75, 2.34];

  for (const yaw of cacGoc) {
    const huongCam = PTNDieuKhien.huongNhinNgang(yaw);
    const w = PTNDieuKhien.huongDiChuyen({ W: true }, yaw);
    const a = PTNDieuKhien.huongDiChuyen({ A: true }, yaw);
    const d = PTNDieuKhien.huongDiChuyen({ D: true }, yaw);
    const s = PTNDieuKhien.huongDiChuyen({ S: true }, yaw);

    // Tích vô hướng W và huongCam
    const dotW = w[0] * huongCam[0] + w[1] * huongCam[1];
    assert.ok(dotW > 0.99, `yaw=${yaw}: W phải cùng hướng camera chiếu ngang, dot=${dotW}`);

    // S ngược hướng W
    const dotS = s[0] * huongCam[0] + s[1] * huongCam[1];
    assert.ok(dotS < -0.99, `yaw=${yaw}: S phải ngược hướng camera, dot=${dotS}`);

    // A vuông góc với W
    const dotA = a[0] * w[0] + a[1] * w[1];
    assert.ok(Math.abs(dotA) < 1e-6, `yaw=${yaw}: A phải vuông góc với W, dot=${dotA}`);

    // D vuông góc với W
    const dotD = d[0] * w[0] + d[1] * w[1];
    assert.ok(Math.abs(dotD) < 1e-6, `yaw=${yaw}: D phải vuông góc với W, dot=${dotD}`);

    // A và D ngược hướng nhau
    const dotAD = a[0] * d[0] + a[1] * d[1];
    assert.ok(dotAD < -0.99, `yaw=${yaw}: A và D phải ngược nhau`);
  }
});

test("viTriCamera nằm sau điểm nhìn theo hướng yaw", () => {
  const diemNhin = [10, 1.45, 10];
  const khoangCach = 4.0;
  const pitch = 0.3; // radian

  const cacGoc = [0, Math.PI / 3, Math.PI / 2, Math.PI, -Math.PI / 2];
  for (const yaw of cacGoc) {
    const cam = PTNDieuKhien.viTriCamera(yaw, pitch, khoangCach, diemNhin);
    // Vector từ camera tới điểm nhìn
    const toiNhin = [diemNhin[0] - cam[0], diemNhin[2] - cam[2]];
    const lenToiNhin = Math.hypot(toiNhin[0], toiNhin[1]);
    const dirToiNhin = [toiNhin[0] / lenToiNhin, toiNhin[1] / lenToiNhin];

    // Hướng nhìn ngang mong muốn
    const huongCam = PTNDieuKhien.huongNhinNgang(yaw);

    // Vector từ camera tới điểm nhìn phải cùng hướng với huongCam
    const dot = dirToiNhin[0] * huongCam[0] + dirToiNhin[1] * huongCam[1];
    assert.ok(dot > 0.99, `yaw=${yaw}: camera phải nằm sau điểm nhìn để nhìn vào điểm nhìn, dot=${dot}`);

    // Khoảng cách 3D xấp xỉ khoangCach
    const dist3D = Math.hypot(cam[0] - diemNhin[0], cam[1] - diemNhin[1], cam[2] - diemNhin[2]);
    assert.ok(Math.abs(dist3D - khoangCach) < 1e-5, `Khoảng cách 3D phải là ${khoangCach}, thực tế ${dist3D}`);
  }
});

test("tayDon co lại khi có hộp chắn giữa, giữ nguyên khi không có", () => {
  const diemNhin = [10, 1.5, 10];
  const viTriMuon = [10, 1.5, 14]; // cách 4m về phía Nam (+Z)

  // Trường hợp không có hộp chắn
  const resKoChan = PTNDieuKhien.tayDon(diemNhin, viTriMuon, []);
  assert.equal(resKoChan.biChan, false);
  assert.equal(resKoChan.khoangCach, 4.0);
  assert.deepEqual(resKoChan.viTri, viTriMuon);

  // Hộp chắn ở z = 12 (giữa 10 và 14)
  const hopChanGiua = [{
    minX: 8, maxX: 12,
    minY: 0, maxY: 3,
    minZ: 12, maxZ: 12.5
  }];

  const resChan = PTNDieuKhien.tayDon(diemNhin, viTriMuon, hopChanGiua, 0.2);
  assert.equal(resChan.biChan, true);
  // Điểm chạm đầu tiên tại minZ = 12.0, khoảng cách chạm = 12 - 10 = 2.0m.
  // Co lại = t - dem = 2.0 - 0.2 = 1.8m.
  assert.ok(Math.abs(resChan.khoangCach - 1.8) < 1e-4, `Khoảng cách co phải là 1.8, thực tế: ${resChan.khoangCach}`);
  assert.ok(resChan.viTri[2] < 12.0, "Vị trí camera mới phải ở trước hộp chắn");

  // Hộp chắn ở ngoài vùng tia
  const hopNgoai = [{
    minX: 15, maxX: 18,
    minY: 0, maxY: 3,
    minZ: 10, maxZ: 15
  }];
  const resNgoai = PTNDieuKhien.tayDon(diemNhin, viTriMuon, hopNgoai);
  assert.equal(resNgoai.biChan, false);
  assert.equal(resNgoai.khoangCach, 4.0);
});

test("quayMuot không vượt quá góc đích và đi đường ngắn nhất qua ±π", () => {
  // Đi từ 0 tới 1 với tốc độ 2 rad/s, dt = 0.1s -> bước 0.2 -> tới 0.2
  const g1 = PTNDieuKhien.quayMuot(0, 1.0, 2.0, 0.1);
  assert.ok(Math.abs(g1 - 0.2) < 1e-5);

  // Bước lớn hơn khoảng cách -> dừng đúng góc đích, không vượt quá
  const gDich = PTNDieuKhien.quayMuot(0, 0.1, 2.0, 0.5); // bước 1.0 > 0.1
  assert.ok(Math.abs(gDich - 0.1) < 1e-5);

  // Đi đường ngắn nhất qua biên ±π:
  // Từ 3.0 rad (gần +π) tới -3.0 rad (gần -π)
  // Đường ngắn qua ±π có khoảng cách là 2π - 6.0 ≈ 0.283 rad (quay theo chiều dương)
  // Nếu đi vòng ngược lại thì mất 6.0 rad
  const quaBien = PTNDieuKhien.quayMuot(3.0, -3.0, 1.0, 0.1);
  // Phải tăng góc (tiến về phía +π) chứ không giảm góc
  assert.ok(quaBien > 3.0 || quaBien < -3.1, `Phải đi đường ngắn qua biên, kết quả = ${quaBien}`);
});
