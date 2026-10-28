/**
 * tools/tach_du_lieu_thu_tay.mjs
 * Tách mảng "vao" từ tests/js/du_lieu/dieu_khien_tay_vang.json sang frontend/dieu-khien-tay/du-lieu-thu/<kịch_bản>.json
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const rootDir = path.resolve(__dirname, '..');

const duLieuVangPath = path.join(rootDir, 'tests', 'js', 'du_lieu', 'dieu_khien_tay_vang.json');
const thuMucDich = path.join(rootDir, 'frontend', 'dieu-khien-tay', 'du-lieu-thu');

fs.mkdirSync(thuMucDich, { recursive: true });

const duLieuVang = JSON.parse(fs.readFileSync(duLieuVangPath, 'utf8'));
const cacKichBan = Object.keys(duLieuVang.kich_ban);

for (const ten of cacKichBan) {
  const vao = duLieuVang.kich_ban[ten].vao;
  const tepDich = path.join(thuMucDich, `${ten}.json`);
  fs.writeFileSync(tepDich, JSON.stringify(vao, null, 2), 'utf8');
  console.log(`Đã xuất ${ten}.json: ${vao.length} khung`);
}

console.log(`Hoàn thành tách ${cacKichBan.length} tệp dữ liệu thử nghiệm tại ${thuMucDich}`);
