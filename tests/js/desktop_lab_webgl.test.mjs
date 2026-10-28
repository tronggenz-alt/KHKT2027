import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import os from "node:os";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const labWebGL = require("../../desktop/lab_webgl.js");

test("lab_webgl: kieuMime trả đúng MIME chuẩn", () => {
  assert.equal(labWebGL.kieuMime("lab.wasm"), "application/wasm");
  assert.equal(labWebGL.kieuMime("lab.framework.js"), "text/javascript");
  assert.equal(labWebGL.kieuMime("lab.data"), "application/octet-stream");
  assert.equal(labWebGL.kieuMime("data.json"), "application/json");
  assert.equal(labWebGL.kieuMime("index.html"), "text/html");
  assert.equal(labWebGL.kieuMime("style.css"), "text/css");
});

test("lab_webgl: giaiDuongDan chặn path traversal và đường dẫn ngoài gốc", () => {
  const tmpGoc = fs.mkdtempSync(path.join(os.tmpdir(), "chemlab-test-"));
  const subFile = path.join(tmpGoc, "lab.loader.js");
  fs.writeFileSync(subFile, "console.log('loader');");

  // Hợp lệ
  assert.equal(labWebGL.giaiDuongDan(tmpGoc, "/lab.loader.js"), subFile);
  assert.equal(labWebGL.giaiDuongDan(tmpGoc, "lab.loader.js"), subFile);

  // Path traversal bị chặn (trả về null)
  assert.equal(labWebGL.giaiDuongDan(tmpGoc, "../../../etc/passwd"), null);
  assert.equal(labWebGL.giaiDuongDan(tmpGoc, "/../outside.txt"), null);
  assert.equal(labWebGL.giaiDuongDan(tmpGoc, "..\\..\\windows\\system32"), null);

  // File không tồn tại
  assert.equal(labWebGL.giaiDuongDan(tmpGoc, "/khong-co.wasm"), null);

  fs.rmSync(tmpGoc, { recursive: true, force: true });
});

test("lab_webgl: giaiDuongDan tìm được file trong thư mục con Build/", () => {
  const tmpGoc = fs.mkdtempSync(path.join(os.tmpdir(), "chemlab-build-"));
  const buildDir = path.join(tmpGoc, "Build");
  fs.mkdirSync(buildDir);
  const wasmFile = path.join(buildDir, "lab.wasm");
  fs.writeFileSync(wasmFile, "mock wasm binary");

  // Yêu cầu /lab.wasm nhưng file nằm trong Build/lab.wasm
  const res = labWebGL.giaiDuongDan(tmpGoc, "/lab.wasm");
  assert.equal(res, wasmFile);

  fs.rmSync(tmpGoc, { recursive: true, force: true });
});

test("lab_webgl: thongTinBuild kiểm tra đủ 4 file", () => {
  const tmpGoc = fs.mkdtempSync(path.join(os.tmpdir(), "chemlab-info-"));

  // Chưa có file nào
  const info1 = labWebGL.thongTinBuild(tmpGoc);
  assert.equal(info1.coSan, false);

  // Tạo đủ 4 file
  fs.writeFileSync(path.join(tmpGoc, "lab.loader.js"), "loader");
  fs.writeFileSync(path.join(tmpGoc, "lab.data"), "data");
  fs.writeFileSync(path.join(tmpGoc, "lab.framework.js"), "framework");
  fs.writeFileSync(path.join(tmpGoc, "lab.wasm"), "wasm");

  const info2 = labWebGL.thongTinBuild(tmpGoc);
  assert.equal(info2.coSan, true);
  assert.ok(info2.tep.loader);
  assert.ok(info2.tep.data);
  assert.ok(info2.tep.framework);
  assert.ok(info2.tep.wasm);
  assert.ok(info2.kichThuocMB >= 0);

  fs.rmSync(tmpGoc, { recursive: true, force: true });
});
