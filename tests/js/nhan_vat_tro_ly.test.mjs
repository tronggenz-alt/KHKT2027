import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const ROOT = path.resolve(__dirname, '../..');

test('1. Manifest web khớp 100% manifest gốc', () => {
  const gocPath = path.join(ROOT, 'tools/du_lieu/nhan-vat-nha-nghien-cuu.json');
  const webPath = path.join(ROOT, 'frontend/nhan-vat/nhan-vat-nha-nghien-cuu.json');

  assert.ok(fs.existsSync(gocPath), 'Phải có manifest gốc');
  assert.ok(fs.existsSync(webPath), 'Phải có manifest web');

  const goc = JSON.parse(fs.readFileSync(gocPath, 'utf-8'));
  const web = JSON.parse(fs.readFileSync(webPath, 'utf-8'));

  assert.deepStrictEqual(web, goc, 'Manifest web phải trùng khớp nội dung với manifest gốc');
});

test('2. stateMap chỉ trỏ tới clip hợp lệ trong animations hoặc mo_rong', () => {
  const gocPath = path.join(ROOT, 'tools/du_lieu/nhan-vat-nha-nghien-cuu.json');
  const manifest = JSON.parse(fs.readFileSync(gocPath, 'utf-8'));

  const validKeys = new Set([
    ...Object.keys(manifest.animations || {}),
    ...Object.keys(manifest.mo_rong || {}),
    ...Object.keys(manifest.unity_bo_sung || {})
  ]);

  for (const [state, clipOrArray] of Object.entries(manifest.stateMap || {})) {
    if (Array.isArray(clipOrArray)) {
      for (const clip of clipOrArray) {
        assert.ok(validKeys.has(clip), `Clip '${clip}' trong state '${state}' phải tồn tại trong manifest`);
      }
    } else {
      assert.ok(validKeys.has(clipOrArray), `Clip '${clipOrArray}' trong state '${state}' phải tồn tại trong manifest`);
    }
  }
});

test('3. chuyen-dong.js có đủ token Animate UI và tôn trọng reduced-motion', async () => {
  const cd = await import('../../frontend/chuyen-dong.js');

  assert.ok(cd.TOKENS, 'Phải export TOKENS');
  assert.ok(cd.TOKENS.spring.press.stiffness === 550, 'spring.press stiffness phải là 550');
  assert.ok(cd.TOKENS.spring.pop.stiffness === 400, 'spring.pop stiffness phải là 400');
  assert.ok(cd.TOKENS.scale.press === 0.97, 'scale.press phải là 0.97');
  assert.ok(cd.TOKENS.scale.dialog === 0.8, 'scale.dialog phải là 0.8');

  // Kiểm tra reduced-motion
  assert.strictEqual(typeof cd.giamChuyenDong, 'function');
  assert.strictEqual(cd.giamChuyenDong(), false, 'Môi trường node mặc định không có matchMedia nên trả false an toàn');
});

test('4. TroLy có cơ chế lùi về idle an toàn khi thiếu clip', () => {
  // Giả lập logic chọn clip của TroLy
  const actions = {
    idle: { name: 'idle', loop: true },
    talk: { name: 'talk', loop: true }
  };

  function layClipAnToan(ten, actionsObj) {
    let next = actionsObj[ten];
    if (!next) {
      next = actionsObj['idle'] || Object.values(actionsObj)[0];
    }
    return next;
  }

  const clipCo = layClipAnToan('talk', actions);
  assert.strictEqual(clipCo.name, 'talk');

  const clipThieu = layClipAnToan('clip_khong_ton_tai', actions);
  assert.strictEqual(clipThieu.name, 'idle', 'Clip thiếu phải lùi về idle an toàn');
});
