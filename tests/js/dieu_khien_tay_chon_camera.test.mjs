import test from 'node:test';
import assert from 'node:assert/strict';
import chonCameraModule from '../../frontend/dieu-khien-tay/chon-camera.js';
const { chonCamera } = chonCameraModule;

test('Chọn camera 1: ["Camera idc🎶", "Camera FaceTime HD"] → FaceTime', () => {
  const ds = [
    { deviceId: 'dev_iphone', label: 'Camera idc🎶' },
    { deviceId: 'dev_facetime', label: 'Camera FaceTime HD' }
  ];
  assert.equal(chonCamera(ds), 'dev_facetime');
});

test('Chọn camera 2: ["iPhone Camera", "FaceTime HD Camera"] → FaceTime', () => {
  const ds = [
    { deviceId: 'dev_iphone', label: 'iPhone Camera' },
    { deviceId: 'dev_facetime', label: 'FaceTime HD Camera' }
  ];
  assert.equal(chonCamera(ds), 'dev_facetime');
});

test('Chọn camera 3: Chỉ có iPhone → null', () => {
  const ds = [
    { deviceId: 'dev_iphone', label: 'iPhone của Trọng' },
    { deviceId: 'dev_ipad', label: 'iPad Camera' }
  ];
  assert.equal(chonCamera(ds), null);
});

test('Chọn camera 4: Nhãn rỗng (chưa cấp quyền) → trả về device đầu tiên', () => {
  const ds = [
    { deviceId: 'dev_default', label: '' },
    { deviceId: 'dev_alt', label: '' }
  ];
  assert.equal(chonCamera(ds), 'dev_default');
});

test('Chọn camera 5: Webcam USB + iPhone → webcam', () => {
  const ds = [
    { deviceId: 'dev_usb', label: 'USB 1080P Pro Webcam' },
    { deviceId: 'dev_iphone', label: 'iPhone Camera' }
  ];
  assert.equal(chonCamera(ds), 'dev_usb');
});

test('Chọn camera 6: Nhãn tiếng Việt "Camera Liên tục" → bỏ', () => {
  const ds = [
    { deviceId: 'dev_lien_tuc', label: 'Camera Liên tục từ iPhone' }
  ];
  assert.equal(chonCamera(ds), null);
});

test('Chọn camera 7: Mảng rỗng hoặc null → null', () => {
  assert.equal(chonCamera([]), null);
  assert.equal(chonCamera(null), null);
});
