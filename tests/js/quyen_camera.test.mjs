import test from 'node:test';
import assert from 'node:assert/strict';
import quyenCameraModule from '../../desktop/quyen_camera.js';
const { choPhepCamera } = quyenCameraModule;

const gocMayChu = 'http://127.0.0.1:54321';

test('Quyền camera 1: Hợp lệ trên /sinh-hoc với video', () => {
  const kq = choPhepCamera({
    urlYeuCau: 'http://127.0.0.1:54321/sinh-hoc',
    gocMayChu,
    loaiQuyen: 'media',
    kieuMedia: ['video']
  });
  assert.equal(kq, true);
});

test('Quyền camera 2: Hợp lệ trên /sinh-hoc kèm tham số với video', () => {
  const kq = choPhepCamera({
    urlYeuCau: 'http://127.0.0.1:54321/sinh-hoc?tab=giai-phau',
    gocMayChu,
    loaiQuyen: 'media',
    kieuMedia: ['video']
  });
  assert.equal(kq, true);
});

test('Quyền camera 3: Hợp lệ trên /static/dieu-khien-tay/demo.html', () => {
  const kq = choPhepCamera({
    urlYeuCau: 'http://127.0.0.1:54321/static/dieu-khien-tay/demo.html',
    gocMayChu,
    loaiQuyen: 'media',
    kieuMedia: ['video']
  });
  assert.equal(kq, true);
});

test('Quyền camera 4: Từ chối nếu kèm audio (micro)', () => {
  const kq = choPhepCamera({
    urlYeuCau: 'http://127.0.0.1:54321/sinh-hoc',
    gocMayChu,
    loaiQuyen: 'media',
    kieuMedia: ['video', 'audio']
  });
  assert.equal(kq, false);
});

test('Quyền camera 5: Từ chối nếu khác origin máy chủ', () => {
  const kq = choPhepCamera({
    urlYeuCau: 'http://malicious-site.com/sinh-hoc',
    gocMayChu,
    loaiQuyen: 'media',
    kieuMedia: ['video']
  });
  assert.equal(kq, false);
});

test('Quyền camera 6: Từ chối trên các trang không phải Sinh học (ví dụ trang chủ hoặc phòng lab Hoá)', () => {
  const kqTrangChu = choPhepCamera({
    urlYeuCau: 'http://127.0.0.1:54321/',
    gocMayChu,
    loaiQuyen: 'media',
    kieuMedia: ['video']
  });
  assert.equal(kqTrangChu, false);

  const kqLabHoa = choPhepCamera({
    urlYeuCau: 'http://127.0.0.1:54321/hoa-hoc/phong-thi-nghiem',
    gocMayChu,
    loaiQuyen: 'media',
    kieuMedia: ['video']
  });
  assert.equal(kqLabHoa, false);
});

test('Quyền camera 7: Từ chối nếu loại quyền không phải media', () => {
  const kq = choPhepCamera({
    urlYeuCau: 'http://127.0.0.1:54321/sinh-hoc',
    gocMayChu,
    loaiQuyen: 'geolocation',
    kieuMedia: ['video']
  });
  assert.equal(kq, false);
});
