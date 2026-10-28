/* CHEMISTRY.app – CẬP NHẬT: hỏi máy chủ bản mới nhất, tải bộ cài, KIỂM TRA SHA-256 rồi mới trao cho người dùng mở.
 * Thuần Node (không import electron) để kiểm thử bằng `node --test tests/js/`; main.js lo phần hộp thoại / menu.
 *
 * Vì sao không dùng electron-updater: macOS chỉ cho tự thay app tại chỗ khi app ký bằng chứng chỉ Apple Developer; bản này ký ad-hoc nên sẽ
 * bị từ chối. Cách làm ở đây chạy được với app chưa ký: tải về → xác minh toàn vẹn → mở file .dmg / .exe cho người dùng cài.
 *
 * An toàn: chỉ https (trừ 127.0.0.1 khi test), chỉ tên miền trong HOST_HOP_LE (kể cả mỗi lần chuyển hướng), giới hạn dung lượng + thời gian,
 * sai SHA-256 hoặc sai kích thước → xoá file và báo lỗi. Danh sách HOST_HOP_LE phải khớp backend/cap_nhat_app.py. */
"use strict";

const https = require("https");
const http = require("http");
const fs = require("fs");
const crypto = require("crypto");

const HOST_HOP_LE = ["github.com", "githubusercontent.com", "giai-chuoi-huu-co.onrender.com"];
const NEN_TANG = ["mac-arm64", "mac-x64", "win-x64"];
const TOI_DA_MANIFEST = 256 * 1024;
const TOI_DA_BO_CAI = 1.5 * 1024 * 1024 * 1024;
const MAU_PHIEN_BAN = /^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$/;
const MAU_SHA256 = /^[0-9a-fA-F]{64}$/;

function hostHopLe(host, choPhepLoopback = false) {
  host = String(host || "").toLowerCase();
  if (choPhepLoopback && (host === "127.0.0.1" || host === "localhost")) return true;
  return HOST_HOP_LE.some((h) => host === h || host.endsWith("." + h));
}

function urlHopLe(url, choPhepLoopback = false) {
  let u;
  try { u = new URL(url); } catch { return false; }
  if (u.protocol === "https:") return hostHopLe(u.hostname, choPhepLoopback);
  return choPhepLoopback && u.protocol === "http:" && hostHopLe(u.hostname, true) && (u.hostname === "127.0.0.1" || u.hostname === "localhost");
}

/** So sánh x.y.z[-tiền-phát-hành]: -1 nếu a < b, 0 nếu bằng, 1 nếu a > b. Bản không có hậu tố (chính thức) lớn hơn bản -beta cùng số. */
function soSanhPhienBan(a, b) {
  const tach = (v) => {
    const [loi, ...pre] = String(v).split("-");
    return { so: loi.split(".").map((n) => parseInt(n, 10) || 0), pre: pre.join("-") };
  };
  const x = tach(a), y = tach(b);
  for (let i = 0; i < 3; i++) {
    if ((x.so[i] || 0) !== (y.so[i] || 0)) return (x.so[i] || 0) < (y.so[i] || 0) ? -1 : 1;
  }
  if (x.pre === y.pre) return 0;
  if (!x.pre) return 1;
  if (!y.pre) return -1;
  const px = x.pre.split("."), py = y.pre.split(".");
  for (let i = 0; i < Math.max(px.length, py.length); i++) {
    if (px[i] === undefined) return -1;
    if (py[i] === undefined) return 1;
    const nx = /^\d+$/.test(px[i]), ny = /^\d+$/.test(py[i]);
    if (nx && ny) { if (+px[i] !== +py[i]) return +px[i] < +py[i] ? -1 : 1; }
    else if (px[i] !== py[i]) return px[i] < py[i] ? -1 : 1;
  }
  return 0;
}

function khoaNenTang(platform = process.platform, arch = process.arch) {
  if (platform === "darwin") return arch === "arm64" ? "mac-arm64" : "mac-x64";
  if (platform === "win32") return "win-x64";
  return null;
}

/** Kiểm tra bản phát hành do máy chủ trả về (phòng máy chủ bị sai / bị giả mạo). → { ok, loi } */
function kiemTraManifest(m, { choPhepLoopback = false } = {}) {
  if (!m || typeof m !== "object") return { ok: false, loi: "không phải đối tượng" };
  if (typeof m.phien_ban !== "string" || !MAU_PHIEN_BAN.test(m.phien_ban)) return { ok: false, loi: "phien_ban sai dạng" };
  if (m.bat_buoc_tu != null && (typeof m.bat_buoc_tu !== "string" || !MAU_PHIEN_BAN.test(m.bat_buoc_tu))) return { ok: false, loi: "bat_buoc_tu sai dạng" };
  if (m.ghi_chu != null && (!Array.isArray(m.ghi_chu) || !m.ghi_chu.every((x) => typeof x === "string"))) return { ok: false, loi: "ghi_chu sai dạng" };
  const tv = m.tai_ve == null ? {} : m.tai_ve;
  if (typeof tv !== "object" || Array.isArray(tv)) return { ok: false, loi: "tai_ve sai dạng" };
  for (const [nen, t] of Object.entries(tv)) {
    if (!NEN_TANG.includes(nen)) return { ok: false, loi: `nền tảng lạ: ${nen}` };
    if (!t || !urlHopLe(t.url, choPhepLoopback)) return { ok: false, loi: `tai_ve.${nen}.url không hợp lệ` };
    if (!MAU_SHA256.test(String(t.sha256 || ""))) return { ok: false, loi: `tai_ve.${nen}.sha256 sai` };
    if (!Number.isInteger(t.kich_thuoc) || t.kich_thuoc <= 0 || t.kich_thuoc > TOI_DA_BO_CAI) return { ok: false, loi: `tai_ve.${nen}.kich_thuoc sai` };
  }
  return { ok: true, loi: "" };
}

/** Có bản mới hơn bản đang chạy cho nền tảng này không? → null hoặc { phien_ban, ghi_chu, bat_buoc, ngay, tai } */
function banMoi(m, phienBanHienTai, nenTang = khoaNenTang(), tuyChon = {}) {
  const kt = kiemTraManifest(m, tuyChon);
  if (!kt.ok || !nenTang) return null;
  if (soSanhPhienBan(m.phien_ban, phienBanHienTai) <= 0) return null;
  const tai = (m.tai_ve || {})[nenTang];
  if (!tai) return null;   // có bản mới nhưng chưa có bộ cài cho máy này → không làm phiền
  return {
    phien_ban: m.phien_ban,
    ngay: m.ngay || "",
    ghi_chu: m.ghi_chu || [],
    bat_buoc: !!m.bat_buoc_tu && soSanhPhienBan(phienBanHienTai, m.bat_buoc_tu) < 0,
    tai: { url: tai.url, sha256: String(tai.sha256).toLowerCase(), kich_thuoc: tai.kich_thuoc },
  };
}

function lay(url, { timeoutMs, choPhepLoopback, soLanChuyenHuong }) {
  return new Promise((resolve, reject) => {
    if (!urlHopLe(url, choPhepLoopback)) return reject(new Error("địa chỉ không được phép: " + url));
    const mod = url.startsWith("https:") ? https : http;
    const req = mod.get(url, { headers: { "User-Agent": "CHEMISTRY-app-cap-nhat", Accept: "*/*" } }, (res) => {
      if ([301, 302, 303, 307, 308].includes(res.statusCode) && res.headers.location) {
        res.resume();
        if (soLanChuyenHuong <= 0) return reject(new Error("quá nhiều lần chuyển hướng"));
        const tiep = new URL(res.headers.location, url).toString();
        return lay(tiep, { timeoutMs, choPhepLoopback, soLanChuyenHuong: soLanChuyenHuong - 1 }).then(resolve, reject);
      }
      if (res.statusCode !== 200) { res.resume(); return reject(new Error("máy chủ trả mã " + res.statusCode)); }
      resolve(res);
    });
    req.setTimeout(timeoutMs, () => req.destroy(new Error("hết thời gian chờ")));
    req.on("error", reject);
  });
}

/** Tải JSON bản phát hành (ngắn, có giới hạn dung lượng). */
async function layManifest(url, { timeoutMs = 8000, choPhepLoopback = false } = {}) {
  const res = await lay(url, { timeoutMs, choPhepLoopback, soLanChuyenHuong: 3 });
  return new Promise((resolve, reject) => {
    const cuc = []; let tong = 0;
    res.on("data", (c) => { tong += c.length; if (tong > TOI_DA_MANIFEST) { res.destroy(new Error("phản hồi quá lớn")); } else cuc.push(c); });
    res.on("end", () => { try { resolve(JSON.parse(Buffer.concat(cuc).toString("utf8"))); } catch (e) { reject(new Error("JSON sai")); } });
    res.on("error", reject);
  });
}

/** Tải bộ cài về `dich`, kiểm tra kích thước + SHA-256. Sai → xoá file, ném lỗi. onTienDo(tỉ_lệ 0..1). */
async function taiBoCai(tai, dich, { onTienDo = () => {}, timeoutMs = 30000, choPhepLoopback = false } = {}) {
  const res = await lay(tai.url, { timeoutMs, choPhepLoopback, soLanChuyenHuong: 5 });
  const tam = dich + ".part";
  await new Promise((resolve, reject) => {
    const bam = crypto.createHash("sha256");
    const ra = fs.createWriteStream(tam);
    let nhan = 0;
    const loi = (e) => { ra.destroy(); fs.rm(tam, { force: true }, () => reject(e)); };
    res.on("data", (c) => {
      nhan += c.length;
      if (nhan > tai.kich_thuoc) return res.destroy(new Error("tải về lớn hơn kích thước khai báo"));
      bam.update(c);
      ra.write(c);
      onTienDo(nhan / tai.kich_thuoc);
    });
    res.on("error", loi);
    res.socket && res.socket.setTimeout(timeoutMs, () => res.destroy(new Error("mất kết nối khi tải")));
    res.on("end", () => {
      ra.end(() => {
        if (nhan !== tai.kich_thuoc) return loi(new Error(`tải thiếu (${nhan}/${tai.kich_thuoc} byte)`));
        if (bam.digest("hex") !== tai.sha256) return loi(new Error("SHA-256 không khớp – file có thể bị hỏng hoặc bị thay đổi, đã xoá"));
        fs.rename(tam, dich, (e) => (e ? loi(e) : resolve()));
      });
    });
  });
  return dich;
}

module.exports = { HOST_HOP_LE, NEN_TANG, hostHopLe, urlHopLe, soSanhPhienBan, khoaNenTang, kiemTraManifest, banMoi, layManifest, taiBoCai };
