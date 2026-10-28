// Phần "keo dán" cập nhật trong desktop/main.js: nạp NGUYÊN VĂN main.js với Electron giả, chạy kiemTraCapNhat() qua các kịch bản người dùng
// (đã mới nhất / có bản mới → tải → xác minh → mở bộ cài / từ chối / bỏ qua / bắt buộc / lỗi mạng / SHA sai). Chạy: node --test tests/js/
import { test, before, after, beforeEach } from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import http from "node:http";
import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import Module from "node:module";

const require = createRequire(import.meta.url);
const GOC = path.resolve(import.meta.dirname, "..", "..");
const capNhatThat = require(path.join(GOC, "desktop", "cap_nhat.js"));

const noiDung = crypto.randomBytes(200_000);
const sha = crypto.createHash("sha256").update(noiDung).digest("hex");
let may, cong, thuMuc, manifest, giaLap, M;

const LOOPBACK = { choPhepLoopback: true, timeoutMs: 3000 };
// cap_nhat.js cho phép loopback chỉ khi được bật → bọc để main.js (không biết gì về test) vẫn nói chuyện được với máy chủ cục bộ
const capNhatBoc = {
  ...capNhatThat,
  layManifest: (u) => capNhatThat.layManifest(u, LOOPBACK),
  banMoi: (m, v, n) => capNhatThat.banMoi(m, v, n, { choPhepLoopback: true }),
  taiBoCai: (t, d, o) => capNhatThat.taiBoCai(t, d, { ...o, ...LOOPBACK }),
};

function nap() {
  giaLap = { hop: [], moVan: [], hienThuMuc: [], thoat: 0, tienDo: [], cauHinh: {}, traLoi: [] };
  const cuaSoGia = { isDestroyed: () => false, setProgressBar: (t) => giaLap.tienDo.push(t) };
  const electron = {
    app: { requestSingleInstanceLock: () => true, quit: () => { giaLap.thoat++; }, on() {}, getPath: (k) => (k === "downloads" ? path.join(thuMuc, "Downloads") : thuMuc),
      getVersion: () => "1.0.0", isPackaged: false, whenReady: () => new Promise(() => {}), setAsDefaultProtocolClient() {}, setName() {}, dock: { setIcon() {} } },
    BrowserWindow: class { static getAllWindows() { return []; } },
    Menu: { buildFromTemplate: (t) => t, setApplicationMenu() {} },
    shell: { openPath: (p) => giaLap.moVan.push(p), showItemInFolder: (p) => giaLap.hienThuMuc.push(p), openExternal() {} },
    dialog: { showMessageBox: async (a, b) => { const o = b || a; giaLap.hop.push(o); return { response: giaLap.traLoi.shift() ?? 0 }; }, showErrorBox() {} },
    ipcMain: { handle() {}, on() {} }, safeStorage: { isEncryptionAvailable: () => false }, Notification: class { show() {} },
    protocol: { registerSchemesAsPrivileged() {}, handle() {} },
  };
  const goc = Module._load;
  Module._load = function (req, ...r) {
    if (req === "electron") return electron;
    if (req === "./cap_nhat") return capNhatBoc;
    return goc.call(this, req, ...r);
  };
  try {
    let src = fs.readFileSync(path.join(GOC, "desktop", "main.js"), "utf8");
    src = src.replace(/const URL_PHIEN_BAN = `[^`]*`;/, `const URL_PHIEN_BAN = "http://127.0.0.1:${cong}/phien-ban";`);
    src += "\nmodule.exports = { kiemTraCapNhat, henGioKiemTraCapNhat, taoMenuHeThong, dat: (w) => { mainWindow = w; } };\n";
    const m = new Module(path.join(GOC, "desktop", "main.js"), null);
    m.filename = path.join(GOC, "desktop", "main.js");
    m.paths = Module._nodeModulePaths(path.dirname(m.filename));
    m._compile(src, m.filename);
    m.exports.dat(cuaSoGia);
    return m.exports;
  } finally { Module._load = goc; }
}

before(async () => {
  thuMuc = fs.mkdtempSync(path.join(os.tmpdir(), "capnhat-main-"));
  fs.mkdirSync(path.join(thuMuc, "Downloads"), { recursive: true });
  process.env.HOME = thuMuc;
  may = http.createServer((req, res) => {
    if (req.url === "/phien-ban") { res.setHeader("content-type", "application/json"); return res.end(JSON.stringify(manifest)); }
    if (req.url === "/CHEMISTRY-mac.dmg") { res.setHeader("content-length", noiDung.length); return res.end(noiDung); }
    res.statusCode = 404; res.end();
  });
  await new Promise((r) => may.listen(0, "127.0.0.1", r));
  cong = may.address().port;
});
after(() => { may.close(); fs.rmSync(thuMuc, { recursive: true, force: true }); });
const nenTang = capNhatThat.khoaNenTang();
const taoManifest = (tuy = {}, shaDung = sha) => ({
  phien_ban: "1.1.0", ngay: "2026-10-25", ghi_chu: ["Sửa lỗi phòng thí nghiệm", "Thêm chi tiết phòng thật"], bat_buoc_tu: null,
  tai_ve: nenTang ? { [nenTang]: { url: `http://127.0.0.1:${cong}/CHEMISTRY-mac.dmg`, sha256: shaDung, kich_thuoc: noiDung.length } } : {}, ...tuy,
});
beforeEach(() => { M = nap(); });

test("đang dùng bản mới nhất: bấm tay thì báo, tự động kiểm tra thì im lặng", async () => {
  manifest = taoManifest({ phien_ban: "1.0.0" });
  await M.kiemTraCapNhat({ imLang: true });
  assert.equal(giaLap.hop.length, 0);
  await M.kiemTraCapNhat();
  assert.match(giaLap.hop[0].message, /mới nhất/);
});

test("có bản mới → Tải về → xác minh → Mở bộ cài: file đúng chỗ, đúng nội dung, thanh tiến độ chạy rồi tắt", async (t) => {
  if (!nenTang) return t.skip("nền tảng không hỗ trợ");
  manifest = taoManifest();
  giaLap.traLoi = [0, 0];   // Tải về và cài → Mở bộ cài
  await M.kiemTraCapNhat();
  assert.match(giaLap.hop[0].message, /1\.1\.0/);
  assert.match(giaLap.hop[0].detail, /Sửa lỗi phòng thí nghiệm/);
  assert.deepEqual(giaLap.hop[0].buttons, ["Tải về và cài", "Để sau", "Bỏ qua bản này"]);
  assert.match(giaLap.hop[1].message, /SHA-256/);
  assert.equal(giaLap.moVan.length, 1);
  assert.ok(giaLap.moVan[0].startsWith(path.join(thuMuc, "Downloads")));
  assert.ok(fs.readFileSync(giaLap.moVan[0]).equals(noiDung));
  assert.equal(giaLap.tienDo[0], 0);
  assert.equal(giaLap.tienDo.at(-1), -1);
});

test("Để sau: không tải gì; Bỏ qua bản này: ghi nhớ và lần tự kiểm tra sau im lặng", async (t) => {
  if (!nenTang) return t.skip("nền tảng không hỗ trợ");
  manifest = taoManifest({ phien_ban: "1.2.5" });
  giaLap.traLoi = [1];
  await M.kiemTraCapNhat();
  assert.equal(giaLap.moVan.length, 0);
  giaLap.traLoi = [2];
  await M.kiemTraCapNhat();                       // Bỏ qua bản này
  const truoc = giaLap.hop.length;
  await M.kiemTraCapNhat({ imLang: true });       // tự động: không hỏi lại
  assert.equal(giaLap.hop.length, truoc);
  manifest = taoManifest({ phien_ban: "1.2.6" });
  giaLap.traLoi = [1];
  await M.kiemTraCapNhat({ imLang: true });       // bản KHÁC thì hỏi lại
  assert.equal(giaLap.hop.length, truoc + 1);
});

test("bản bắt buộc: không có nút bỏ qua; chọn Thoát thì thoát app", async (t) => {
  if (!nenTang) return t.skip("nền tảng không hỗ trợ");
  manifest = taoManifest({ phien_ban: "2.0.0", bat_buoc_tu: "1.5.0" });
  giaLap.traLoi = [1];
  await M.kiemTraCapNhat({ imLang: true });
  assert.deepEqual(giaLap.hop[0].buttons, ["Tải về và cài", "Thoát ứng dụng"]);
  assert.match(giaLap.hop[0].detail, /BẮT BUỘC/);
  assert.equal(giaLap.thoat, 1);
});

test("SHA-256 sai: báo lỗi, KHÔNG mở file, không để file trong Downloads", async (t) => {
  if (!nenTang) return t.skip("nền tảng không hỗ trợ");
  manifest = taoManifest({ phien_ban: "3.0.0" }, "c".repeat(64));
  const truoc = fs.readdirSync(path.join(thuMuc, "Downloads")).length;
  giaLap.traLoi = [0];
  await M.kiemTraCapNhat();
  assert.equal(giaLap.moVan.length, 0);
  assert.equal(giaLap.hop.at(-1).type, "warning");
  assert.match(giaLap.hop.at(-1).detail, /SHA-256/);
  assert.equal(fs.readdirSync(path.join(thuMuc, "Downloads")).length, truoc);
  assert.equal(giaLap.tienDo.at(-1), -1);        // thanh tiến độ luôn được tắt dù lỗi
});

test("không có mạng / máy chủ lỗi: bấm tay thì báo lỗi, tự động kiểm tra thì im lặng", async () => {
  may.close();
  const cu = cong;
  try {
    await M.kiemTraCapNhat({ imLang: true });
    assert.equal(giaLap.hop.length, 0);
    await M.kiemTraCapNhat();
    assert.equal(giaLap.hop.at(-1).type, "warning");
  } finally {
    await new Promise((r) => may.listen(cu, "127.0.0.1", r));
  }
});

test("menu có mục Kiểm tra cập nhật trong menu ứng dụng và Trợ giúp", () => {
  const mau = M.taoMenuHeThong.toString();
  assert.equal((mau.match(/Kiểm tra cập nhật\.\.\./g) || []).length, 2);
});
