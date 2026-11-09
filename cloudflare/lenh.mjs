// Lệnh cho bản Cloudflare của web quản lý (chạy trong thư mục cloudflare/):
//
//   node lenh.mjs xay                          dựng dist/: trang giới thiệu + giao diện server/web, rồi `npx wrangler pages deploy`
//   node lenh.mjs csdl [--remote]              tạo bảng và dữ liệu ban đầu (kho hóa chất, thiết bị, dụng cụ sinh học) trong D1
//   node lenh.mjs tai-khoan-mau [--remote]     nạp tài khoản thử từ data/tai_khoan_mau.json, mật khẩu lấy từ data/tai_khoan_mau_mat_khau.json (git bỏ qua)
//   node lenh.mjs tao-giao-vien <ten> "<Họ tên>" [--remote]   tạo tài khoản giáo viên với mật khẩu tạm in ra một lần, phải đổi ở lần đăng nhập đầu
//
// Không có --remote thì ghi vào D1 cục bộ của `wrangler pages dev`. Mật khẩu chỉ băm trên máy này, D1 chỉ nhận bản băm.

import { execFileSync } from "node:child_process";
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { pbkdf2Sync, randomBytes, randomInt } from "node:crypto";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const DAY = dirname(fileURLToPath(import.meta.url));
const GOC = join(DAY, "..");
const TAM = join(DAY, ".tam");
const docJson = (p) => JSON.parse(readFileSync(join(GOC, p), "utf-8"));
const [lenh, ...thamSo] = process.argv.slice(2);
const tuXa = thamSo.includes("--remote");
const viTri = thamSo.filter((x) => !x.startsWith("--"));

const sql = (v) => (v === null || v === undefined ? "NULL" : typeof v === "number" ? String(v) : `'${String(v).replace(/'/g, "''")}'`);

function chayD1(ten_tep, noi_dung) {
  mkdirSync(TAM, { recursive: true });
  const p = join(TAM, ten_tep);
  writeFileSync(p, noi_dung, "utf-8");
  const npx = process.platform === "win32" ? "npx.cmd" : "npx";
  // Đường dẫn tương đối: thư mục dự án có dấu cách, Windows cần shell để chạy npx
  execFileSync(npx, ["wrangler", "d1", "execute", "verlab-db", tuXa ? "--remote" : "--local", "--file", `.tam/${ten_tep}`, "--yes"], { cwd: DAY, stdio: "inherit", shell: process.platform === "win32" });
  rmSync(p); // tệp có bản băm mật khẩu: không để lại
}

// Cùng định dạng với src/tai_khoan.ts
const VONG = Number(/export const VONG = ([\d_]+)/.exec(readFileSync(join(DAY, "src/tai_khoan.ts"), "utf-8"))[1].replace(/_/g, ""));
function bam(mat_khau) {
  const muoi = randomBytes(16);
  return `pbkdf2_sha256$${VONG}$${muoi.toString("base64")}$${pbkdf2Sync(mat_khau, muoi, VONG, 32, "sha256").toString("base64")}`;
}
const now = () => Date.now() / 1000;

if (lenh === "xay") {
  // Danh sách hồ sơ máy (data/may/*.json) đóng gói vào Function
  const may = readdirSync(join(GOC, "data/may")).filter((f) => f.endsWith(".json")).sort();
  writeFileSync(join(DAY, "src/may_du_lieu.ts"),
    "// Tạo tự động bởi `node lenh.mjs xay`, đừng sửa tay.\n" + may.map((f, i) => `import m${i} from "../../data/may/${f}";\n`).join("") +
    `export const MAY: any[] = [${may.map((_, i) => `m${i}`).join(", ")}];\n`);
  const dist = join(DAY, "dist");
  rmSync(dist, { recursive: true, force: true });
  cpSync(join(GOC, "trang_gioi_thieu"), dist, { recursive: true, filter: (p) => !p.endsWith("_redirects") });
  if (existsSync(join(dist, "tinh")) || existsSync(join(dist, "_trang"))) throw new Error("trang_gioi_thieu/ có thư mục trùng tên tinh/ hoặc _trang/");
  cpSync(join(GOC, "server/web/tinh"), join(dist, "tinh"), { recursive: true });
  mkdirSync(join(dist, "_trang"));
  for (const f of readdirSync(join(GOC, "server/web")).filter((f) => f.endsWith(".html"))) cpSync(join(GOC, "server/web", f), join(dist, "_trang", f));
  // Chỉ các đường dẫn này chạy Function (tính vào hạn mức 100.000 lượt/ngày); ảnh, CSS, JS là tệp tĩnh miễn phí
  const include = ["/auth/*", "/api/*", "/app/*", "/lab/*", "/robot/*", "/may", "/may/*", "/the-thong-tin/*", "/suc-khoe", "/chu-thich", "/tro-ly", "/hoi",
    "/dang-nhap", "/quan-ly", "/giao-vien", "/hoc-sinh", "/trinh-chieu", "/truc-tiep", "/buoi-hoc", "/hoa", "/sinh"];
  writeFileSync(join(dist, "_routes.json"), JSON.stringify({ version: 1, include, exclude: [] }, null, 2));
  writeFileSync(join(dist, "_headers"), "/tinh/*\n  Cache-Control: no-cache\n/_trang/*\n  Cache-Control: no-store\n");
  console.log(`Đã dựng ${dist}`);
} else if (lenh === "csdl") {
  const chem = docJson("data/chem/hoa_chat.json").hoa_chat;
  const equip = docJson("data/chem/thiet_bi.json").thiet_bi;
  const dc = docJson("data/sinh/dung_cu.json").dung_cu || [];
  const sp = docJson("data/sinh/sinh_pham_mac_dinh.json").sinh_pham || [];
  const t = now();
  const ton = (c) => (c.trang_thai === "khi" ? [2, "bình", 1] : c.trang_thai === "giay" ? [5, "hộp", 2] : ["dd", "long"].includes(c.trang_thai) ? [500, "ml", 100] : [250, "g", 50]);
  const NHIEU = ["Dụng cụ thủy tinh", "Dụng cụ đo lường", "Dụng cụ hỗ trợ", "Vật tư tiêu hao"];
  const dong = [readFileSync(join(DAY, "csdl/luoc_do.sql"), "utf-8")];
  for (const c of chem) {
    const [so, dv, it] = ton(c);
    dong.push(`INSERT OR IGNORE INTO ton_kho (stt, so_luong, don_vi, toi_thieu, cap_nhat) VALUES (${c.stt}, ${so}, ${sql(dv)}, ${it}, ${t});`);
  }
  for (const e of equip) dong.push(`INSERT OR IGNORE INTO thiet_bi (stt, so_luong, cap_nhat) VALUES (${e.stt}, ${NHIEU.includes(e.phan_loai) ? 10 : 2}, ${t});`);
  for (const d of dc) if (d.ma) dong.push(`INSERT OR IGNORE INTO sinh_dung_cu (ma, so_luong, vi_tri, cap_nhat) VALUES (${sql(d.ma)}, ${Math.trunc(Number(d.so_luong ?? 1))}, ${sql(String(d.vi_tri ?? ""))}, ${t});`);
  for (const s of sp) {
    if (!s.id) continue;
    dong.push(`INSERT OR IGNORE INTO sinh_pham (id, ten, loai, tu, ngan, so_luong, don_vi, so_lo, han_dung, cap_an_toan, ma_chat, cap_nhat) VALUES (${[
      s.id, s.ten ?? "", s.loai ?? "hoa_chat_sinh", s.tu ?? "ke", s.ngan ?? "", Number(s.so_luong ?? 0), s.don_vi ?? "", s.so_lo ?? "",
      t + Number(s.han_dung_ngay ?? 180) * 86400, s.cap_an_toan ?? "BSL-1", s.ma_chat ?? "", t].map(sql).join(", ")});`);
  }
  for (const [tu, lui, nd] of [["tu_mat_4", 3600, 4.2], ["tu_mat_4", 600, 4.0], ["tu_dong_am20", 3600, -20.5], ["tu_dong_am20", 600, -20.1], ["tu_am_37", 3600, 37.0], ["tu_am_37", 600, 37.1]]) {
    dong.push(`INSERT INTO nhiet_do_tu (tu, thoi_gian, nhiet_do, cua_mo) SELECT ${sql(tu)}, ${t - lui}, ${nd}, 0 WHERE (SELECT COUNT(*) FROM nhiet_do_tu) < 6;`);
  }
  chayD1("khoi_tao.sql", dong.join("\n") + "\n");
} else if (lenh === "tai-khoan-mau") {
  const mau = docJson("data/tai_khoan_mau.json");
  const tep = join(GOC, "data/tai_khoan_mau_mat_khau.json");
  if (!existsSync(tep)) throw new Error("Chưa có data/tai_khoan_mau_mat_khau.json: chạy `python -m server.lab.quan_tri nap-mau` trước");
  const { mat_khau, ma_lop } = JSON.parse(readFileSync(tep, "utf-8"));
  const t = now(), dong = [];
  for (const [vai, ds] of [["giao_vien", mau.giao_vien], ["hoc_sinh", mau.hoc_sinh]]) {
    for (const u of ds) {
      if (!mat_khau[u.ten_dang_nhap]) throw new Error(`Thiếu mật khẩu của ${u.ten_dang_nhap}`);
      dong.push(`INSERT INTO nguoi_dung (ten_dang_nhap, ho_ten, vai_tro, mat_khau_bam, tao_luc) VALUES (${sql(u.ten_dang_nhap)}, ${sql(u.ho_ten)}, '${vai}', ${sql(bam(mat_khau[u.ten_dang_nhap]))}, ${t})
        ON CONFLICT(ten_dang_nhap) DO UPDATE SET ho_ten=excluded.ho_ten, mat_khau_bam=excluded.mat_khau_bam, phai_doi_mat_khau=0;`);
    }
  }
  for (const l of mau.lop) {
    // Mã lớp đóng sẵn: trang công khai trên mạng, không để người lạ tự đăng ký vào lớp thử
    dong.push(`INSERT OR IGNORE INTO lop (ten, nam_hoc, giao_vien_id, ma_lop, ma_lop_mo, tao_luc) SELECT ${sql(l.ten)}, ${sql(l.nam_hoc)}, id, ${sql(ma_lop[l.ten])}, 0, ${t}
      FROM nguoi_dung WHERE ten_dang_nhap=${sql(l.giao_vien)};`);
    for (const hs of l.hoc_sinh) {
      dong.push(`INSERT OR IGNORE INTO thanh_vien_lop (lop_id, hoc_sinh_id, them_luc) SELECT l.id, n.id, ${t} FROM lop l, nguoi_dung n WHERE l.ma_lop=${sql(ma_lop[l.ten])} AND n.ten_dang_nhap=${sql(hs)};`);
    }
  }
  chayD1("tai_khoan_mau.sql", dong.join("\n") + "\n");
  console.log("Đã nạp tài khoản thử; mật khẩu như trong data/tai_khoan_mau_mat_khau.json");
} else if (lenh === "tao-giao-vien") {
  const [ten, ho_ten] = viTri;
  if (!ten || !ho_ten) throw new Error('Cách dùng: node lenh.mjs tao-giao-vien <ten_dang_nhap> "<Họ tên>" [--remote]');
  if (!/^[a-z0-9._-]{3,32}$/.test(ten)) throw new Error("Tên đăng nhập gồm 3 đến 32 ký tự: chữ không dấu, số, dấu chấm, gạch nối, gạch dưới");
  const BANG = "abcdefghjkmnpqrstuvwxyz23456789";
  const mat_khau = Array.from({ length: 12 }, () => BANG[randomInt(BANG.length)]).join("");
  chayD1("giao_vien.sql", `INSERT INTO nguoi_dung (ten_dang_nhap, ho_ten, vai_tro, mat_khau_bam, phai_doi_mat_khau, tao_luc) VALUES (${sql(ten)}, ${sql(ho_ten)}, 'giao_vien', ${sql(bam(mat_khau))}, 1, ${now()});\n`);
  console.log(`Đã tạo giáo viên ${ten}. Mật khẩu tạm (chỉ hiện lần này, phải đổi ở lần đăng nhập đầu): ${mat_khau}`);
} else {
  console.log(readFileSync(fileURLToPath(import.meta.url), "utf-8").split("\n").slice(0, 8).join("\n"));
}
