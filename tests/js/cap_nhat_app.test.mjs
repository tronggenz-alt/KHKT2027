// App máy tính CHEMISTRY: cơ chế cập nhật (desktop/cap_nhat.js) – so sánh phiên bản, kiểm tra bản phát hành, tải + xác minh SHA-256.
// Chạy: node --test tests/js/
import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import http from "node:http";
import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

const require = createRequire(import.meta.url);
const C = require("../../desktop/cap_nhat.js");

const SHA = "a".repeat(64);
const manifestMau = (tuy = {}) => ({
  phien_ban: "1.2.0", ngay: "2026-10-25", ghi_chu: ["Sửa lỗi phòng thí nghiệm"], bat_buoc_tu: null,
  tai_ve: { "mac-arm64": { url: "https://github.com/x/y/releases/download/v1.2.0/CHEMISTRY-mac.dmg", sha256: SHA, kich_thuoc: 1000 } }, ...tuy,
});

test("so sánh phiên bản: số, 1.10 > 1.9, bản -beta nhỏ hơn bản chính thức", () => {
  const s = C.soSanhPhienBan;
  assert.equal(s("1.0.0", "1.0.1"), -1);
  assert.equal(s("1.10.0", "1.9.9"), 1);
  assert.equal(s("2.0.0", "1.99.99"), 1);
  assert.equal(s("1.0.0", "1.0.0"), 0);
  assert.equal(s("1.0.0-beta.1", "1.0.0"), -1);
  assert.equal(s("1.0.0", "1.0.0-rc.1"), 1);
  assert.equal(s("1.0.0-beta.2", "1.0.0-beta.10"), -1);
});

test("khoá nền tảng", () => {
  assert.equal(C.khoaNenTang("darwin", "arm64"), "mac-arm64");
  assert.equal(C.khoaNenTang("darwin", "x64"), "mac-x64");
  assert.equal(C.khoaNenTang("win32", "x64"), "win-x64");
  assert.equal(C.khoaNenTang("linux", "x64"), null);
});

test("kiểm tra bản phát hành: chấp nhận bản hợp lệ, từ chối http / tên miền lạ / sha256 sai / nền tảng lạ", () => {
  assert.ok(C.kiemTraManifest(manifestMau()).ok);
  const sai = (m) => assert.equal(C.kiemTraManifest(m).ok, false, JSON.stringify(m).slice(0, 120));
  sai(manifestMau({ phien_ban: "abc" }));
  sai(manifestMau({ tai_ve: { "mac-arm64": { url: "http://github.com/a", sha256: SHA, kich_thuoc: 10 } } }));
  sai(manifestMau({ tai_ve: { "mac-arm64": { url: "https://evil.example.com/a.dmg", sha256: SHA, kich_thuoc: 10 } } }));
  sai(manifestMau({ tai_ve: { "mac-arm64": { url: "https://github.com.evil.com/a.dmg", sha256: SHA, kich_thuoc: 10 } } }));
  sai(manifestMau({ tai_ve: { "mac-arm64": { url: "https://github.com/a", sha256: "xyz", kich_thuoc: 10 } } }));
  sai(manifestMau({ tai_ve: { "mac-arm64": { url: "https://github.com/a", sha256: SHA, kich_thuoc: -1 } } }));
  sai(manifestMau({ tai_ve: { "linux-x64": { url: "https://github.com/a", sha256: SHA, kich_thuoc: 10 } } }));
  sai(null);
});

test("bản mới: chỉ khi lớn hơn bản hiện tại VÀ có bộ cài cho đúng nền tảng", () => {
  assert.equal(C.banMoi(manifestMau(), "1.2.0", "mac-arm64"), null);
  assert.equal(C.banMoi(manifestMau(), "1.3.0", "mac-arm64"), null);
  assert.equal(C.banMoi(manifestMau(), "1.0.0", "win-x64"), null);       // có bản mới nhưng chưa có bộ cài Windows
  assert.equal(C.banMoi(manifestMau({ phien_ban: "bad" }), "1.0.0", "mac-arm64"), null);
  const b = C.banMoi(manifestMau(), "1.0.0", "mac-arm64");
  assert.equal(b.phien_ban, "1.2.0");
  assert.equal(b.bat_buoc, false);
  assert.equal(b.tai.kich_thuoc, 1000);
});

test("bản bắt buộc: bat_buoc_tu lớn hơn bản đang chạy", () => {
  assert.equal(C.banMoi(manifestMau({ bat_buoc_tu: "1.1.0" }), "1.0.5", "mac-arm64").bat_buoc, true);
  assert.equal(C.banMoi(manifestMau({ bat_buoc_tu: "1.1.0" }), "1.1.0", "mac-arm64").bat_buoc, false);
});

// ---------------------------------------------------------------- tải thật qua máy chủ cục bộ
const noiDung = crypto.randomBytes(300_000);
const shaDung = crypto.createHash("sha256").update(noiDung).digest("hex");
let may, cong, thuMuc;
before(async () => {
  thuMuc = fs.mkdtempSync(path.join(os.tmpdir(), "capnhat-"));
  may = http.createServer((req, res) => {
    if (req.url === "/ban-phat-hanh.json") { res.setHeader("content-type", "application/json"); return res.end(JSON.stringify(manifestMau())); }
    if (req.url === "/bo-cai.dmg") { res.setHeader("content-length", noiDung.length); return res.end(noiDung); }
    if (req.url === "/chuyen-huong") { res.statusCode = 302; res.setHeader("location", "/bo-cai.dmg"); return res.end(); }
    if (req.url === "/sang-ten-mien-la") { res.statusCode = 302; res.setHeader("location", "https://evil.example.com/x.dmg"); return res.end(); }
    if (req.url === "/to-qua-lon") { return res.end(Buffer.concat([noiDung, noiDung])); }
    if (req.url === "/khong-co") { res.statusCode = 404; return res.end("no"); }
    res.statusCode = 404; res.end();
  });
  await new Promise((r) => may.listen(0, "127.0.0.1", r));
  cong = may.address().port;
});
after(() => { may.close(); fs.rmSync(thuMuc, { recursive: true, force: true }); });
const tuyChon = { choPhepLoopback: true, timeoutMs: 3000 };
const goc = () => `http://127.0.0.1:${cong}`;

test("tải bộ cài đúng: file có mặt, đúng nội dung, báo tiến độ tới 100%, không để lại file .part", async () => {
  const dich = path.join(thuMuc, "dung.dmg");
  const tiens = [];
  await C.taiBoCai({ url: `${goc()}/bo-cai.dmg`, sha256: shaDung, kich_thuoc: noiDung.length }, dich, { ...tuyChon, onTienDo: (t) => tiens.push(t) });
  assert.ok(fs.readFileSync(dich).equals(noiDung));
  assert.ok(tiens.length > 0 && Math.abs(tiens[tiens.length - 1] - 1) < 1e-9);
  assert.ok(!fs.existsSync(dich + ".part"));
});

test("theo chuyển hướng 302 trong cùng nơi được phép", async () => {
  const dich = path.join(thuMuc, "chuyen-huong.dmg");
  await C.taiBoCai({ url: `${goc()}/chuyen-huong`, sha256: shaDung, kich_thuoc: noiDung.length }, dich, tuyChon);
  assert.ok(fs.existsSync(dich));
});

test("SHA-256 sai → báo lỗi và XOÁ file (không bao giờ trao file chưa xác minh)", async () => {
  const dich = path.join(thuMuc, "sai-sha.dmg");
  await assert.rejects(C.taiBoCai({ url: `${goc()}/bo-cai.dmg`, sha256: "b".repeat(64), kich_thuoc: noiDung.length }, dich, tuyChon), /SHA-256/);
  assert.ok(!fs.existsSync(dich) && !fs.existsSync(dich + ".part"));
});

test("sai kích thước (thiếu hoặc thừa) → từ chối và xoá", async () => {
  const d1 = path.join(thuMuc, "thieu.dmg"), d2 = path.join(thuMuc, "thua.dmg");
  await assert.rejects(C.taiBoCai({ url: `${goc()}/bo-cai.dmg`, sha256: shaDung, kich_thuoc: noiDung.length + 10 }, d1, tuyChon), /thiếu/);
  await assert.rejects(C.taiBoCai({ url: `${goc()}/to-qua-lon`, sha256: shaDung, kich_thuoc: noiDung.length }, d2, tuyChon));
  assert.ok(!fs.existsSync(d1) && !fs.existsSync(d2) && !fs.existsSync(d1 + ".part") && !fs.existsSync(d2 + ".part"));
});

test("chuyển hướng sang tên miền không được phép → từ chối", async () => {
  await assert.rejects(C.taiBoCai({ url: `${goc()}/sang-ten-mien-la`, sha256: shaDung, kich_thuoc: noiDung.length }, path.join(thuMuc, "la.dmg"), tuyChon), /không được phép/);
});

test("máy chủ trả 404 / địa chỉ lạ → lỗi rõ ràng", async () => {
  await assert.rejects(C.taiBoCai({ url: `${goc()}/khong-co`, sha256: shaDung, kich_thuoc: 1 }, path.join(thuMuc, "x.dmg"), tuyChon), /404/);
  await assert.rejects(C.layManifest("https://evil.example.com/a.json"), /không được phép/);
  await assert.rejects(C.layManifest(`${goc()}/ban-phat-hanh.json`), /không được phép/);   // loopback không được phép khi không bật tuỳ chọn test
});

test("lấy bản phát hành qua mạng cục bộ", async () => {
  const m = await C.layManifest(`${goc()}/ban-phat-hanh.json`, tuyChon);
  assert.equal(m.phien_ban, "1.2.0");
});
