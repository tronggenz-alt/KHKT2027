import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from '../../frontend/vendor/three/three.module.js';
import { taoBoApOrbit } from '../../frontend/dieu-khien-tay/ap-orbit.js';
import cuChiModule from '../../frontend/dieu-khien-tay/cu-chi.js';
const { DieuKhien } = cuChiModule;

function taoControlsGia(camera) {
  const listeners = {};
  return {
    target: new THREE.Vector3(0, 0, 0),
    minDistance: 0.45,
    maxDistance: 10.0,
    minPolarAngle: 0.1,
    maxPolarAngle: Math.PI * 0.94,
    addEventListener(kieu, fn) {
      if (!listeners[kieu]) listeners[kieu] = [];
      listeners[kieu].push(fn);
    },
    removeEventListener(kieu, fn) {
      if (listeners[kieu]) {
        listeners[kieu] = listeners[kieu].filter(f => f !== fn);
      }
    },
    dispatchEvent(e) {
      if (listeners[e.type]) {
        for (const fn of listeners[e.type]) {
          fn(e);
        }
      }
    },
    update() {}
  };
}

function laySpherical(camera, target) {
  const offset = camera.position.clone().sub(target);
  const s = new THREE.Spherical();
  s.setFromVector3(offset);
  return s;
}

test('Áp Orbit 1: yaw +0,5 rad → góc phương vị camera giảm 0,5 (±1 %) sau khi hội tụ', () => {
  const camera = new THREE.PerspectiveCamera(40, 1, 0.1, 100);
  camera.position.set(0, 0, 4); // nhìn thẳng từ trục Z, theta = 0, phi = pi/2
  const controls = taoControlsGia(camera);
  const dk = new DieuKhien();
  const boAp = taoBoApOrbit({ THREE, camera, controls, dieuKhien: dk, doMuot: 0.03 });

  const s0 = laySpherical(camera, controls.target);
  assert.equal(s0.theta, 0);

  // Giả lập cử chỉ yaw tăng 0.5 rad
  dk.yaw += 0.5;

  // Cho chạy nhiều khung (tổng 0.5 s) để hội tụ
  for (let i = 0; i < 30; i++) {
    boAp.capNhat(1 / 60);
  }

  const s1 = laySpherical(camera, controls.target);
  const deltaTheta = s1.theta - s0.theta;
  // yaw tăng thì camera đi vòng sang trái -> theta giảm 0.5
  assert.ok(Math.abs(deltaTheta - (-0.5)) < 0.005,
    `Góc phương vị phải giảm 0.5 rad (±1%), thực tế delta = ${deltaTheta}`);
});

test('Áp Orbit 2: pitch +0,3 rad → góc cực giảm 0,3 (±1 %)', () => {
  const camera = new THREE.PerspectiveCamera(40, 1, 0.1, 100);
  camera.position.set(0, 0, 4);
  const controls = taoControlsGia(camera);
  const dk = new DieuKhien();
  const boAp = taoBoApOrbit({ THREE, camera, controls, dieuKhien: dk, doMuot: 0.03 });

  const s0 = laySpherical(camera, controls.target);
  // pitch tăng 0.3 rad
  dk.pitch += 0.3;

  for (let i = 0; i < 30; i++) {
    boAp.capNhat(1 / 60);
  }

  const s1 = laySpherical(camera, controls.target);
  const deltaPhi = s1.phi - s0.phi;
  // pitch tăng thì nhìn từ trên xuống nhiều hơn -> phi giảm 0.3
  assert.ok(Math.abs(deltaPhi - (-0.3)) < 0.005,
    `Góc cực phải giảm 0.3 rad (±1%), thực tế delta = ${deltaPhi}`);
});

test('Áp Orbit 3: zoom 1 → 2 → khoảng cách giảm một nửa', () => {
  const camera = new THREE.PerspectiveCamera(40, 1, 0.1, 100);
  camera.position.set(0, 0, 4);
  const controls = taoControlsGia(camera);
  const dk = new DieuKhien();
  const boAp = taoBoApOrbit({ THREE, camera, controls, dieuKhien: dk, doMuot: 0.03 });

  const s0 = laySpherical(camera, controls.target);
  assert.equal(s0.radius, 4);

  // zoom từ 1 lên 2
  dk.zoom = 2.0;

  for (let i = 0; i < 30; i++) {
    boAp.capNhat(1 / 60);
  }

  const s1 = laySpherical(camera, controls.target);
  assert.ok(Math.abs(s1.radius - 2.0) < 0.05,
    `Khoảng cách camera phải giảm một nửa còn 2.0 (±1%), thực tế: ${s1.radius}`);
});

test('Áp Orbit 4: Kẹp min/max cả góc cực lẫn khoảng cách', () => {
  const camera = new THREE.PerspectiveCamera(40, 1, 0.1, 100);
  camera.position.set(0, 0, 4);
  const controls = taoControlsGia(camera);
  controls.minPolarAngle = 0.5;
  controls.maxPolarAngle = 2.0;
  controls.minDistance = 1.0;
  controls.maxDistance = 6.0;

  const dk = new DieuKhien();
  const boAp = taoBoApOrbit({ THREE, camera, controls, dieuKhien: dk, doMuot: 0.03 });

  // 1. Thử kéo pitch cực lớn để vượt minPolarAngle
  dk.pitch += 10.0;
  for (let i = 0; i < 30; i++) boAp.capNhat(1 / 60);
  let s = laySpherical(camera, controls.target);
  assert.ok(Math.abs(s.phi - controls.minPolarAngle) < 0.01,
    `Góc cực phi phải bị kẹp ở minPolarAngle 0.5, thực tế: ${s.phi}`);

  // 2. Thử kéo pitch cực nhỏ để vượt maxPolarAngle
  dk.pitch -= 20.0;
  for (let i = 0; i < 30; i++) boAp.capNhat(1 / 60);
  s = laySpherical(camera, controls.target);
  assert.ok(Math.abs(s.phi - controls.maxPolarAngle) < 0.01,
    `Góc cực phi phải bị kẹp ở maxPolarAngle 2.0, thực tế: ${s.phi}`);

  // 3. Thử zoom cực lớn vượt minDistance
  dk.zoom = 100.0;
  for (let i = 0; i < 30; i++) boAp.capNhat(1 / 60);
  s = laySpherical(camera, controls.target);
  assert.ok(Math.abs(s.radius - controls.minDistance) < 0.01,
    `Khoảng cách phải bị kẹp ở minDistance 1.0, thực tế: ${s.radius}`);

  // 4. Thử zoom cực nhỏ vượt maxDistance
  dk.zoom = 0.01;
  for (let i = 0; i < 30; i++) boAp.capNhat(1 / 60);
  s = laySpherical(camera, controls.target);
  assert.ok(Math.abs(s.radius - controls.maxDistance) < 0.01,
    `Khoảng cách phải bị kẹp ở maxDistance 6.0, thực tế: ${s.radius}`);
});

test('Áp Orbit 5: Mượt: một bước Δ lớn được áp dần qua nhiều khung vẽ, không vượt quá', () => {
  const camera = new THREE.PerspectiveCamera(40, 1, 0.1, 100);
  camera.position.set(0, 0, 4);
  const controls = taoControlsGia(camera);
  const dk = new DieuKhien();
  const boAp = taoBoApOrbit({ THREE, camera, controls, dieuKhien: dk, doMuot: 0.03 });

  // Đổi yaw đột ngột 1.0 rad
  dk.yaw += 1.0;

  // Khung 1 (dt = 0.016s)
  boAp.capNhat(0.016);
  const sKhung1 = laySpherical(camera, controls.target);
  const delta1 = Math.abs(sKhung1.theta);
  // k = 1 - exp(-0.016/0.03) ≈ 0.413
  assert.ok(delta1 > 0.3 && delta1 < 0.5,
    `Khung 1 phải áp một phần mượt (~41%), thực tế delta = ${delta1}`);

  // Khung 2
  boAp.capNhat(0.016);
  const sKhung2 = laySpherical(camera, controls.target);
  const delta2 = Math.abs(sKhung2.theta);
  assert.ok(delta2 > delta1 && delta2 < 1.0,
    `Khung 2 phải tiếp tục tăng mượt, delta2=${delta2}`);

  // Không bao giờ được vượt quá 1.0
  assert.ok(delta2 <= 1.0, `Không được vượt quá mục tiêu 1.0`);
});

test('Áp Orbit 6: "start" từ chuột → quán tính của dieuKhien dừng', () => {
  const camera = new THREE.PerspectiveCamera(40, 1, 0.1, 100);
  camera.position.set(0, 0, 4);
  const controls = taoControlsGia(camera);
  const dk = new DieuKhien();
  taoBoApOrbit({ THREE, camera, controls, dieuKhien: dk });

  dk.wYaw = 2.5;
  dk.wPitch = -1.2;
  dk.cheDo = 'quan_tinh';

  // Sự kiện start do chuột (không có cờ doTay)
  controls.dispatchEvent({ type: 'start' });

  assert.equal(dk.wYaw, 0);
  assert.equal(dk.wPitch, 0);
  assert.equal(dk.cheDo, 'cho');
});

test('Áp Orbit 7: dongBo() sau khi trang tự bay camera → không có cú giật', () => {
  const camera = new THREE.PerspectiveCamera(40, 1, 0.1, 100);
  camera.position.set(0, 0, 4);
  const controls = taoControlsGia(camera);
  const dk = new DieuKhien();
  const boAp = taoBoApOrbit({ THREE, camera, controls, dieuKhien: dk });

  // Cho tay kéo nhẹ
  dk.yaw += 0.2;
  boAp.capNhat(0.016);

  // Trang tự bay camera sang vị trí mới (5, 5, 5)
  camera.position.set(5, 5, 5);
  controls.target.set(0, 1, 0);

  // Gọi dongBo()
  boAp.dongBo();

  // Khung tiếp theo (chưa có cử chỉ tay mới)
  const posTruoc = camera.position.clone();
  boAp.capNhat(0.016);
  const posSau = camera.position.clone();

  assert.ok(posTruoc.distanceTo(posSau) < 1e-4,
    `Sau dongBo() không được có cú giật camera: kc = ${posTruoc.distanceTo(posSau)}`);
});
