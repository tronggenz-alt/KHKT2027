// Tài khoản giáo viên, học sinh: băm mật khẩu, phiên đăng nhập, lớp, giới hạn số lần sai (bản Cloudflare của server/lab/tai_khoan.py).
//
// Mật khẩu băm bằng PBKDF2-SHA256 100.000 vòng, muối 16 byte (Web Crypto). Bản FastAPI dùng Argon2id; Workers gói miễn phí
// chỉ có 10 ms CPU mỗi lượt gọi và giới hạn PBKDF2 ở 100.000 vòng, nên đây là mức cao nhất chạy được.
// Mã phiên là chuỗi ngẫu nhiên; D1 chỉ giữ bản băm SHA-256 của mã.

import { Env, HttpLoi, NguoiDung, bayGio, chay, ngauNhien, q, q1 } from "./khung";

export const HAN_PHIEN = { web: 12 * 3600, ung_dung: 14 * 86400 } as const;
const SAI_TOI_DA = 5;
const KHOA_GIAY = 300;
const TEN_HOP_LE = /^[a-z0-9._-]{3,32}$/;
const KY_TU_MA_LOP = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
const KY_TU_MAT_KHAU_TAM = "abcdefghjkmnpqrstuvwxyz23456789";
// Số vòng PBKDF2. Workers giới hạn tối đa 100.000; gói miễn phí chỉ có 10 ms CPU mỗi lượt gọi, nên số thật dùng
// là số đo được trên Cloudflare (xem docs/25). Mật khẩu tạm do máy sinh (10 ký tự ngẫu nhiên, phải đổi ở lần đăng nhập đầu)
// băm nhẹ hơn để giáo viên tạo cả lớp 200 học sinh trong một lượt gọi; đổi mật khẩu thì băm lại với số vòng đầy đủ.
export const VONG = 100_000;
export const VONG_TAM = 200;

export class LoiTaiKhoan extends HttpLoi {
  constructor(loi: string, ma = 422) { super(ma, loi); }
}
export class TenDaCo extends LoiTaiKhoan {
  constructor(loi: string) { super(loi, 409); }
}

export const chuanTen = (ten: string) => (ten || "").trim().toLowerCase();

// ---------------------------------------------------------------------------
// Băm
// ---------------------------------------------------------------------------

const b64 = (u: Uint8Array) => btoa(String.fromCharCode(...u));
const tuB64 = (s: string) => Uint8Array.from(atob(s), (ch) => ch.charCodeAt(0));

async function pbkdf2(mat_khau: string, muoi: Uint8Array, vong: number): Promise<Uint8Array> {
  const k = await crypto.subtle.importKey("raw", new TextEncoder().encode(mat_khau), "PBKDF2", false, ["deriveBits"]);
  return new Uint8Array(await crypto.subtle.deriveBits({ name: "PBKDF2", hash: "SHA-256", salt: muoi, iterations: vong }, k, 256));
}

export async function bamMatKhau(mat_khau: string, vong = VONG): Promise<string> {
  const muoi = crypto.getRandomValues(new Uint8Array(16));
  return `pbkdf2_sha256$${vong}$${b64(muoi)}$${b64(await pbkdf2(mat_khau, muoi, vong))}`;
}

async function dungMatKhau(bam: string, mat_khau: string): Promise<boolean> {
  const p = (bam || "").split("$");
  if (p.length !== 4 || p[0] !== "pbkdf2_sha256") return false;
  const vong = Number(p[1]);
  if (!Number.isInteger(vong) || vong < 1 || vong > 100_000) return false;
  let muoi: Uint8Array, mong: Uint8Array;
  try { muoi = tuB64(p[2]); mong = tuB64(p[3]); } catch { return false; }
  const ra = await pbkdf2(mat_khau, muoi, vong);
  if (ra.length !== mong.length) return false;
  let khac = 0;
  for (let i = 0; i < ra.length; i++) khac |= ra[i] ^ mong[i];
  return khac === 0;
}

export async function sha256Hex(s: string): Promise<string> {
  const d = new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(s)));
  return [...d].map((b) => b.toString(16).padStart(2, "0")).join("");
}

function maPhienMoi(): string {
  return b64(crypto.getRandomValues(new Uint8Array(32))).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

// ---------------------------------------------------------------------------
// Người dùng
// ---------------------------------------------------------------------------

export function kiemTraTen(ten: string): void {
  if (!TEN_HOP_LE.test(ten)) throw new LoiTaiKhoan("Tên đăng nhập gồm 3 đến 32 ký tự: chữ không dấu, số, dấu chấm, gạch nối, gạch dưới");
}

export function kiemTraMatKhau(mat_khau: string, ten = ""): void {
  if (mat_khau.length < 8) throw new LoiTaiKhoan("Mật khẩu cần ít nhất 8 ký tự");
  if (mat_khau.length > 128) throw new LoiTaiKhoan("Mật khẩu dài quá 128 ký tự");
  if (ten && mat_khau.toLowerCase() === ten) throw new LoiTaiKhoan("Mật khẩu không được trùng tên đăng nhập");
}

export async function taoNguoiDung(env: Env, ten: string, ho_ten: string, vai_tro: string, mat_khau: string, phai_doi = false): Promise<NguoiDung> {
  ten = chuanTen(ten);
  ho_ten = (ho_ten || "").trim();
  if (vai_tro !== "giao_vien" && vai_tro !== "hoc_sinh") throw new LoiTaiKhoan("Vai trò không hợp lệ");
  kiemTraTen(ten);
  if (ho_ten.length < 2 || ho_ten.length > 80) throw new LoiTaiKhoan("Họ tên cần từ 2 đến 80 ký tự");
  kiemTraMatKhau(mat_khau, ten);
  if (await timTheoTen(env, ten)) throw new TenDaCo("Tên đăng nhập này đã có người dùng");
  let r;
  try {
    r = await chay(env, "INSERT INTO nguoi_dung (ten_dang_nhap, ho_ten, vai_tro, mat_khau_bam, phai_doi_mat_khau, tao_luc) VALUES (?,?,?,?,?,?)",
      ten, ho_ten, vai_tro, await bamMatKhau(mat_khau, phai_doi ? VONG_TAM : VONG), phai_doi ? 1 : 0, bayGio());
  } catch (e) {
    if (String(e).includes("UNIQUE")) throw new TenDaCo("Tên đăng nhập này đã có người dùng");
    throw e;
  }
  return (await timTheoId(env, r.id))!;
}

export const timTheoTen = (env: Env, ten: string) => q1<NguoiDung>(env, "SELECT * FROM nguoi_dung WHERE ten_dang_nhap=?", chuanTen(ten));
export const timTheoId = (env: Env, id: number) => q1<NguoiDung>(env, "SELECT * FROM nguoi_dung WHERE id=?", id);

let bamGia: string | null = null;

export async function xacThuc(env: Env, ten: string, mat_khau: string): Promise<NguoiDung | null> {
  const nd = await timTheoTen(env, ten);
  if (!nd) {
    bamGia = bamGia || (await bamMatKhau(maPhienMoi())); // tên không có vẫn băm một lượt: thời gian trả lời không lộ tên nào có thật
    await dungMatKhau(bamGia, mat_khau);
    return null;
  }
  return (await dungMatKhau(nd.mat_khau_bam, mat_khau)) ? nd : null;
}

export async function datMatKhau(env: Env, id: number, mat_khau: string, phai_doi = false): Promise<void> {
  await chay(env, "UPDATE nguoi_dung SET mat_khau_bam=?, phai_doi_mat_khau=? WHERE id=?", await bamMatKhau(mat_khau, phai_doi ? VONG_TAM : VONG), phai_doi ? 1 : 0, id);
}

export const dungMatKhauCua = (nd: NguoiDung, mat_khau: string) => dungMatKhau(nd.mat_khau_bam, mat_khau);

export async function lopCua(env: Env, nd: NguoiDung): Promise<{ id: number; ten: string; nam_hoc: string }[]> {
  if (nd.vai_tro === "giao_vien") return q(env, "SELECT id, ten, nam_hoc FROM lop WHERE giao_vien_id=? ORDER BY id", nd.id);
  return q(env, "SELECT l.id, l.ten, l.nam_hoc FROM lop l JOIN thanh_vien_lop t ON t.lop_id=l.id WHERE t.hoc_sinh_id=? ORDER BY l.id", nd.id);
}

export async function congKhai(env: Env, nd: NguoiDung) {
  return { id: nd.id, ten_dang_nhap: nd.ten_dang_nhap, ho_ten: nd.ho_ten, vai_tro: nd.vai_tro,
    phai_doi_mat_khau: Boolean(nd.phai_doi_mat_khau), lop: await lopCua(env, nd) };
}

// ---------------------------------------------------------------------------
// Phiên đăng nhập
// ---------------------------------------------------------------------------

export async function taoPhien(env: Env, nguoi_dung_id: number, kieu: "web" | "ung_dung" = "web") {
  const now = bayGio();
  const ma = maPhienMoi();
  const het_han = now + HAN_PHIEN[kieu];
  await env.DB.batch([
    env.DB.prepare("DELETE FROM phien WHERE het_han<?").bind(now),
    env.DB.prepare("INSERT INTO phien (ma_bam, nguoi_dung_id, kieu, tao_luc, het_han) VALUES (?,?,?,?,?)").bind(await sha256Hex(ma), nguoi_dung_id, kieu, now, het_han),
  ]);
  return { ma_phien: ma, het_han };
}

export async function docPhien(env: Env, ma: string): Promise<NguoiDung | null> {
  if (!ma) return null;
  const bam = await sha256Hex(ma), now = bayGio();
  const r = await q1<{ nguoi_dung_id: number; kieu: string; het_han: number } & NguoiDung>(env,
    "SELECT p.kieu, p.het_han AS het_han_phien, n.* FROM phien p JOIN nguoi_dung n ON n.id=p.nguoi_dung_id WHERE p.ma_bam=? AND p.het_han>?", bam, now);
  if (!r) return null;
  const het_han = (r as any).het_han_phien as number;
  if (r.kieu === "ung_dung" && now + HAN_PHIEN.ung_dung - het_han > 300) {
    await chay(env, "UPDATE phien SET het_han=? WHERE ma_bam=?", now + HAN_PHIEN.ung_dung, bam);
  }
  const { kieu: _k, het_han_phien: _h, ...nd } = r as any;
  return nd as NguoiDung;
}

export async function huyPhien(env: Env, ma: string): Promise<void> {
  await chay(env, "DELETE FROM phien WHERE ma_bam=?", await sha256Hex(ma));
}

export async function huyPhienKhac(env: Env, id: number, giu_ma = ""): Promise<void> {
  await chay(env, "DELETE FROM phien WHERE nguoi_dung_id=? AND ma_bam<>?", id, giu_ma ? await sha256Hex(giu_ma) : "");
}

// ---------------------------------------------------------------------------
// Giới hạn số lần sai (theo cặp tên đăng nhập + IP, hoặc mã lớp + IP)
// ---------------------------------------------------------------------------

const khoaSai = (muc: string, ip: string) => sha256Hex(`${muc}|${ip}`);

export async function conBiKhoa(env: Env, muc: string, ip: string): Promise<number> {
  const r = await q1<{ khoa_den: number }>(env, "SELECT khoa_den FROM gioi_han_sai WHERE khoa=?", await khoaSai(muc, ip));
  const now = bayGio();
  return r && r.khoa_den > now ? Math.max(0, Math.trunc(r.khoa_den - now) + 1) : 0;
}

export async function ghiSai(env: Env, muc: string, ip: string, toi_da = SAI_TOI_DA): Promise<void> {
  const now = bayGio(), khoa = await khoaSai(muc, ip);
  await chay(env, "DELETE FROM gioi_han_sai WHERE lan_cuoi<? AND khoa_den<?", now - 86400, now);
  const r = await q1<{ so_lan: number; lan_cuoi: number }>(env, "SELECT so_lan, lan_cuoi FROM gioi_han_sai WHERE khoa=?", khoa);
  const so_lan = r && now - r.lan_cuoi < KHOA_GIAY ? r.so_lan + 1 : 1;
  const khoa_den = so_lan >= toi_da ? now + KHOA_GIAY : 0;
  await chay(env, "INSERT OR REPLACE INTO gioi_han_sai (khoa, so_lan, lan_cuoi, khoa_den) VALUES (?,?,?,?)", khoa, khoa_den ? 0 : so_lan, now, khoa_den);
}

export async function xoaSai(env: Env, muc: string, ip: string): Promise<void> {
  await chay(env, "DELETE FROM gioi_han_sai WHERE khoa=?", await khoaSai(muc, ip));
}

// ---------------------------------------------------------------------------
// Lớp
// ---------------------------------------------------------------------------

export async function taoLop(env: Env, ten: string, nam_hoc: string, giao_vien_id: number) {
  ten = (ten || "").trim();
  if (ten.length < 1 || ten.length > 60) throw new LoiTaiKhoan("Tên lớp cần từ 1 đến 60 ký tự");
  let ma: string;
  do ma = ngauNhien(KY_TU_MA_LOP, 6);
  while (await q1(env, "SELECT 1 FROM lop WHERE ma_lop=?", ma));
  const r = await chay(env, "INSERT INTO lop (ten, nam_hoc, giao_vien_id, ma_lop, tao_luc) VALUES (?,?,?,?,?)", ten, (nam_hoc || "").trim(), giao_vien_id, ma, bayGio());
  return (await q1(env, "SELECT * FROM lop WHERE id=?", r.id))!;
}

export async function themVaoLop(env: Env, lop_id: number, hoc_sinh_id: number): Promise<void> {
  await chay(env, "INSERT OR IGNORE INTO thanh_vien_lop (lop_id, hoc_sinh_id, them_luc) VALUES (?,?,?)", lop_id, hoc_sinh_id, bayGio());
}

export const timLopTheoMa = (env: Env, ma_lop: string) =>
  q1<{ id: number }>(env, "SELECT * FROM lop WHERE ma_lop=? AND ma_lop_mo=1", (ma_lop || "").trim().toUpperCase());

export const sinhMatKhauTam = () => ngauNhien(KY_TU_MAT_KHAU_TAM, 10);

export const lopCuaGiaoVien = (env: Env, lop_id: number, gv: number) => q1<any>(env, "SELECT * FROM lop WHERE id=? AND giao_vien_id=?", lop_id, gv);

export const dsLopCuaGiaoVien = (env: Env, gv: number) => q<any>(env, `SELECT l.id, l.ten, l.nam_hoc, l.ma_lop, l.ma_lop_mo, COUNT(t.hoc_sinh_id) AS so_hoc_sinh
  FROM lop l LEFT JOIN thanh_vien_lop t ON t.lop_id=l.id WHERE l.giao_vien_id=? GROUP BY l.id ORDER BY l.id`, gv);

export async function suaLop(env: Env, lop_id: number, ten: string | null, nam_hoc: string | null, ma_lop_mo: boolean | null): Promise<void> {
  if (ten !== null && ten !== undefined) {
    ten = ten.trim();
    if (ten.length < 1 || ten.length > 60) throw new LoiTaiKhoan("Tên lớp cần từ 1 đến 60 ký tự");
    await chay(env, "UPDATE lop SET ten=? WHERE id=?", ten, lop_id);
  }
  if (nam_hoc !== null && nam_hoc !== undefined) await chay(env, "UPDATE lop SET nam_hoc=? WHERE id=?", nam_hoc.trim(), lop_id);
  if (ma_lop_mo !== null && ma_lop_mo !== undefined) await chay(env, "UPDATE lop SET ma_lop_mo=? WHERE id=?", ma_lop_mo ? 1 : 0, lop_id);
}

export const hocSinhCuaLop = (env: Env, lop_id: number) => q<{ id: number; ten_dang_nhap: string; ho_ten: string; phai_doi_mat_khau: number }>(env,
  `SELECT n.id, n.ten_dang_nhap, n.ho_ten, n.phai_doi_mat_khau FROM nguoi_dung n
   JOIN thanh_vien_lop t ON t.hoc_sinh_id=n.id WHERE t.lop_id=? ORDER BY n.ho_ten`, lop_id);

export const coTrongLop = async (env: Env, lop_id: number, hs: number) => Boolean(await q1(env, "SELECT 1 FROM thanh_vien_lop WHERE lop_id=? AND hoc_sinh_id=?", lop_id, hs));

export async function goKhoiLop(env: Env, lop_id: number, hs: number): Promise<void> {
  await chay(env, "DELETE FROM thanh_vien_lop WHERE lop_id=? AND hoc_sinh_id=?", lop_id, hs);
}

export async function hocSinhCuaGiaoVien(env: Env, gv: number): Promise<Set<number>> {
  const r = await q<{ hoc_sinh_id: number }>(env, "SELECT t.hoc_sinh_id FROM thanh_vien_lop t JOIN lop l ON l.id=t.lop_id WHERE l.giao_vien_id=?", gv);
  return new Set(r.map((x) => x.hoc_sinh_id));
}
