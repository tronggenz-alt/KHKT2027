import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const congThucPath = path.resolve(__dirname, '../../frontend/hoa-hoc/hoa-ly/cong-thuc.json');
const hinhPath = path.resolve(__dirname, '../../frontend/hoa-hoc/hoa-ly/chung/hinh-chung-minh.js');

// Nạp module HLHinhChungMinh trong môi trường Node.js
const HLHinhChungMinh = (function () {
  const code = fs.readFileSync(hinhPath, 'utf8');
  const context = { module: {}, exports: {} };
  const fn = new Function('module', 'exports', 'global', code);
  fn(context.module, context.exports, context);
  return context.module.exports || context.HLHinhChungMinh;
})();

test('cong-thuc.json: tồn tại và có đầy đủ 34 công thức bắt buộc', () => {
  assert.ok(fs.existsSync(congThucPath), 'cong-thuc.json phải tồn tại');
  const data = JSON.parse(fs.readFileSync(congThucPath, 'utf8'));
  assert.ok(Array.isArray(data.cong_thuc), 'cong_thuc phải là mảng');
  assert.ok(data.cong_thuc.length >= 34, `Cần ít nhất 34 công thức, hiện có: ${data.cong_thuc.length}`);

  const requiredPrefixes = {
    'K': 8, // K1 - K8 (8)
    'N': 8, // N1 - N8 (8)
    'H': 4, // H1 - H4 (4)
    'D': 7, // D1 - D7 (7)
    'C': 7  // C1 - C7 (7)
  };

  const maSet = new Set(data.cong_thuc.map(c => c.ma || c.id));
  for (const [prefix, count] of Object.entries(requiredPrefixes)) {
    for (let i = 1; i <= count; i++) {
      const ma = `${prefix}${i}`;
      assert.ok(maSet.has(ma), `Thiếu công thức bắt buộc: ${ma}`);
    }
  }
});

test('cong-thuc.json: 100% công thức (34/34) có hình chuyển động hợp lệ trong HLHinhChungMinh', () => {
  const data = JSON.parse(fs.readFileSync(congThucPath, 'utf8'));

  assert.ok(HLHinhChungMinh, 'HLHinhChungMinh phải được nạp thành công');
  assert.ok(HLHinhChungMinh.cacHinh, 'HLHinhChungMinh.cacHinh phải tồn tại');

  for (const ct of data.cong_thuc) {
    const ma = ct.ma || ct.id;
    const cm = ct.chung_minh || {};
    const hinhId = cm.hinh || (cm.buoc && cm.buoc[0] && cm.buoc[0].hinh) || ma;
    assert.ok(hinhId, `Công thức ${ma} thiếu định danh hình chuyển động`);

    const fnHinh = HLHinhChungMinh.cacHinh[hinhId] || HLHinhChungMinh.cacHinh[ma];
    assert.equal(
      typeof fnHinh,
      'function',
      `Công thức ${ma} có mã hình '${hinhId}' nhưng không tìm thấy hàm vẽ trong HLHinhChungMinh.cacHinh`
    );

    // Kiểm tra mọi bước của công thức đều có hinh
    if (cm.buoc) {
      for (let i = 0; i < cm.buoc.length; i++) {
        const b = cm.buoc[i];
        const bHinh = b.hinh || hinhId;
        assert.ok(bHinh, `Bước ${i + 1} của ${ma} thiếu hinh`);
        assert.equal(
          typeof (HLHinhChungMinh.cacHinh[bHinh] || HLHinhChungMinh.cacHinh[ma]),
          'function',
          `Bước ${i + 1} của ${ma} có mã hình '${bHinh}' không hợp lệ`
        );
      }
    }
  }
});

test('cong-thuc.json: các công thức trọng tâm có đầy đủ cach_2', () => {
  const data = JSON.parse(fs.readFileSync(congThucPath, 'utf8'));
  const canCach2 = ['K1', 'K7', 'N2', 'N5', 'D5', 'D6', 'C1', 'C4'];

  for (const ma of canCach2) {
    const ct = data.cong_thuc.find(c => (c.ma || c.id) === ma);
    assert.ok(ct, `Không tìm thấy công thức ${ma}`);
    const c2 = ct.chung_minh && ct.chung_minh.cach_2;
    assert.ok(c2, `Công thức ${ma} bắt buộc phải có cach_2`);
    assert.ok(c2.ten && typeof c2.ten === 'string', `${ma}.cach_2 thiếu tên`);
    assert.ok(Array.isArray(c2.buoc) && c2.buoc.length >= 3, `${ma}.cach_2 phải có ít nhất 3 bước`);
  }
});

test('cong-thuc.json: không có chữ tiếng Việt có dấu nằm ngoài \\text{...} trong chuỗi latex', () => {
  const data = JSON.parse(fs.readFileSync(congThucPath, 'utf8'));
  const vnChars = new Set('àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđÀÁẠẢÃÂẦẤẬẨẪĂẰẮẶẲẴÈÉẸẺẼÊỀẾỆỂỄÌÍỊỈĨÒÓỌỎÕÔỒỐỘỔỖƠỜỚỢỞỠÙÚỤỦŨƯỪỨỰỬỮỲÝỴỶỸĐ');

  function kiemTraLatex(s, viTri) {
    if (!s) return;
    let i = 0;
    let inText = 0;
    while (i < s.length) {
      if (s.slice(i, i + 6) === '\\text{') {
        inText++;
        i += 6;
        continue;
      }
      if (s[i] === '}' && inText > 0) {
        inText--;
        i++;
        continue;
      }
      if (inText === 0 && vnChars.has(s[i])) {
        assert.fail(`Chữ có dấu '${s[i]}' nằm ngoài \\text{...} tại ${viTri}: "${s}"`);
      }
      i++;
    }
  }

  for (const ct of data.cong_thuc) {
    const ma = ct.ma || ct.id;
    kiemTraLatex(ct.latex, `${ma}.latex`);
    const cm = ct.chung_minh || {};
    (cm.buoc || []).forEach((b, idx) => {
      kiemTraLatex(b.latex, `${ma}.buoc[${idx + 1}]`);
    });
    if (cm.cach_2 && cm.cach_2.buoc) {
      cm.cach_2.buoc.forEach((b, idx) => {
        kiemTraLatex(b.latex, `${ma}.cach_2[${idx + 1}]`);
      });
    }
  }
});

test('cong-thuc.json: cấu trúc dữ liệu và kiểm chứng vi_du_so', () => {
  const data = JSON.parse(fs.readFileSync(congThucPath, 'utf8'));

  for (const ct of data.cong_thuc) {
    assert.ok(ct.id, `Công thức thiếu id: ${JSON.stringify(ct)}`);
    assert.ok(ct.ten, `Công thức ${ct.id} thiếu tên`);
    assert.ok(ct.latex, `Công thức ${ct.id} thiếu latex`);
    assert.ok(Array.isArray(ct.ky_hieu), `Công thức ${ct.id} thiếu ky_hieu`);
    assert.ok(ct.vi_du_so, `Công thức ${ct.id} thiếu vi_du_so`);
    assert.ok(ct.vi_du_so.vao && typeof ct.vi_du_so.vao === 'object', `Công thức ${ct.id} thiếu vi_du_so.vao`);
    assert.ok(typeof ct.vi_du_so.ra === 'number', `Công thức ${ct.id} thiếu vi_du_so.ra`);
    assert.ok(ct.chung_minh && Array.isArray(ct.chung_minh.buoc), `Công thức ${ct.id} thiếu buoc`);
  }
});
