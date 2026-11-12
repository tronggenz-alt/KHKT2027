// Lớp học: giáo viên quản lý lớp của mình, học sinh xem kết quả của mình (bản Cloudflare của server/lab/lop_api.py).
// Lớp của giáo viên khác trả 404 như không tồn tại. Mật khẩu tạm chỉ trả về đúng một lần lúc tạo hoặc đặt lại.

import { BAI_SINH } from "./du_lieu";
import { Ctx, HttpLoi, bayGio, cau, chuoi, dauHoi, dung, fold, q, soNguyen, than, thamSo, thamSoSo, tuyen } from "./khung";
import * as tk from "./tai_khoan";

const TOI_DA_DONG = 200;

export async function lopCuaToi(c: Ctx, lop_id: number) {
  const lop = await tk.lopCuaGiaoVien(c.env, lop_id, c.nd!.id);
  if (!lop) throw new HttpLoi(404, "Không có lớp này");
  return lop;
}

async function hocSinhTrongLop(c: Ctx, lop_id: number, hs_id: number) {
  const nd = (await tk.coTrongLop(c.env, lop_id, hs_id)) ? await tk.timTheoId(c.env, hs_id) : null;
  if (!nd) throw new HttpLoi(404, "Học sinh này không có trong lớp");
  return nd;
}

tuyen("GET", "/api/lop", async (c) => (await tk.dsLopCuaGiaoVien(c.env, c.nd!.id)).map((l) => ({ ...l, ma_lop_mo: Boolean(l.ma_lop_mo) })));

tuyen("POST", "/api/lop", async (c) => {
  const b = await than(c);
  const lop = await tk.taoLop(c.env, chuoi(b, "ten", { toi_da: 120 }), chuoi(b, "nam_hoc", { md: "", toi_da: 20 }), c.nd!.id);
  return { ...lop, ma_lop_mo: Boolean(lop.ma_lop_mo), so_hoc_sinh: 0 };
});

tuyen("PATCH", "/api/lop/{lop_id:int}", async (c) => {
  const id = soNguyen(c, "lop_id");
  await lopCuaToi(c, id);
  const b = await than(c);
  await tk.suaLop(c.env, id, chuoi(b, "ten", { md: null, toi_da: 120 }), chuoi(b, "nam_hoc", { md: null, toi_da: 20 }), dung(b, "ma_lop_mo", { md: null }));
  const lop = await lopCuaToi(c, id);
  return { ...lop, ma_lop_mo: Boolean(lop.ma_lop_mo) };
});

tuyen("GET", "/api/lop/{lop_id:int}/hoc-sinh", async (c) => {
  const id = soNguyen(c, "lop_id");
  await lopCuaToi(c, id);
  return (await tk.hocSinhCuaLop(c.env, id)).map((h) => ({ ...h, phai_doi_mat_khau: Boolean(h.phai_doi_mat_khau) }));
});

function tenDangNhapTuHoTen(ho_ten: string, da_co: Set<string>): string {
  // "Nguyễn Văn An" thành nguyen.van.an; trùng thì thêm số: nguyen.van.an2
  let goc = (fold(ho_ten).match(/[a-z0-9]+/g) || []).join(".").slice(0, 28).replace(/^\.+|\.+$/g, "");
  if (goc.length < 3) goc = `hs.${goc}`.replace(/^\.+|\.+$/g, "");
  let ten = goc, so = 1;
  while (da_co.has(ten)) ten = `${goc}${++so}`;
  return ten;
}

function tachDong(dong: string): [string, string] {
  // Dán từ Excel thì cột cách nhau bằng tab; gõ tay thì dấu phẩy hoặc chấm phẩy
  const m = /[\t,;]/.exec(dong);
  return m ? [dong.slice(0, m.index).trim(), dong.slice(m.index + 1).trim()] : [dong.trim(), ""];
}

tuyen("POST", "/api/lop/{lop_id:int}/hoc-sinh", async (c) => {
  const lop_id = soNguyen(c, "lop_id");
  await lopCuaToi(c, lop_id);
  const b = await than(c);
  const dong_ds = chuoi(b, "danh_sach", { toi_da: 40000 }).split(/\r\n|\r|\n/).map((d) => d.trim()).filter(Boolean);
  if (!dong_ds.length) throw new HttpLoi(422, "Danh sách đang trống");
  if (dong_ds.length > TOI_DA_DONG) throw new HttpLoi(422, `Mỗi lần thêm tối đa ${TOI_DA_DONG} học sinh`);
  // Gói miễn phí giới hạn số lượt gọi CSDL mỗi yêu cầu: đọc mọi tài khoản một lần, kiểm tra từng dòng trong bộ nhớ,
  // rồi ghi cả lớp trong một lượt (batch). Dòng lỗi không làm hỏng các dòng khác.
  const moi_nd = new Map((await q<any>(c.env, "SELECT id, ten_dang_nhap, ho_ten, vai_tro FROM nguoi_dung")).map((n) => [n.ten_dang_nhap, n]));
  const da_co = new Set(moi_nd.keys());
  const them: any[] = [], loi: any[] = [], cho_tao: { ho_ten: string; ten: string; mat_khau: string }[] = [];
  const now = bayGio(), lenh: D1PreparedStatement[] = [];
  for (const dong of dong_ds) {
    const [ho_ten, ten_go] = tachDong(dong);
    try {
      const co_san = ten_go ? moi_nd.get(tk.chuanTen(ten_go)) : undefined;
      if (co_san) {
        // Học sinh đã có tài khoản: đưa vào lớp, giữ nguyên mật khẩu. Họ tên phải khớp để không thêm nhầm người.
        if (co_san.vai_tro !== "hoc_sinh" || fold(co_san.ho_ten) !== fold(ho_ten)) throw new tk.LoiTaiKhoan("Tên đăng nhập này đã có người khác dùng");
        lenh.push(cau(c.env, "INSERT OR IGNORE INTO thanh_vien_lop (lop_id, hoc_sinh_id, them_luc) VALUES (?,?,?)", lop_id, co_san.id, now));
        them.push({ id: co_san.id, ho_ten: co_san.ho_ten, ten_dang_nhap: co_san.ten_dang_nhap });
        continue;
      }
      if (ho_ten.length < 2 || ho_ten.length > 80) throw new tk.LoiTaiKhoan("Họ tên cần từ 2 đến 80 ký tự");
      const ten = ten_go ? tk.chuanTen(ten_go) : tenDangNhapTuHoTen(ho_ten, da_co);
      const mat_khau = tk.sinhMatKhauTam();
      tk.kiemTraTen(ten);
      if (da_co.has(ten)) throw new tk.TenDaCo("Tên đăng nhập này đã có người dùng");
      da_co.add(ten);
      cho_tao.push({ ho_ten, ten, mat_khau });
    } catch (e) {
      if (!(e instanceof tk.LoiTaiKhoan)) throw e;
      loi.push({ dong: dong.slice(0, 120), ly_do: e.message });
    }
  }
  for (const m of cho_tao) {
    lenh.push(cau(c.env, "INSERT INTO nguoi_dung (ten_dang_nhap, ho_ten, vai_tro, mat_khau_bam, phai_doi_mat_khau, tao_luc) VALUES (?,?,'hoc_sinh',?,1,?) RETURNING id",
      m.ten, m.ho_ten, await tk.bamMatKhau(m.mat_khau, tk.VONG_TAM), now));
    lenh.push(cau(c.env, "INSERT OR IGNORE INTO thanh_vien_lop (lop_id, hoc_sinh_id, them_luc) SELECT ?, id, ? FROM nguoi_dung WHERE ten_dang_nhap=?", lop_id, now, m.ten));
  }
  const ket_qua = lenh.length ? await c.env.DB.batch(lenh) : [];
  const ids = ket_qua.map((r: any) => r.results?.[0]?.id).filter((x: any) => x !== undefined);
  const tao = cho_tao.map((m, i) => ({ id: ids[i], ho_ten: m.ho_ten, ten_dang_nhap: m.ten, mat_khau_tam: m.mat_khau }));
  return { tao, them, loi };
});

tuyen("DELETE", "/api/lop/{lop_id:int}/hoc-sinh/{hs_id:int}", async (c) => {
  const lop_id = soNguyen(c, "lop_id"), hs_id = soNguyen(c, "hs_id");
  await lopCuaToi(c, lop_id);
  await hocSinhTrongLop(c, lop_id, hs_id);
  await tk.goKhoiLop(c.env, lop_id, hs_id);
  return { ok: true };
});

tuyen("POST", "/api/lop/{lop_id:int}/hoc-sinh/{hs_id:int}/dat-lai-mat-khau", async (c) => {
  const lop_id = soNguyen(c, "lop_id"), hs_id = soNguyen(c, "hs_id");
  await lopCuaToi(c, lop_id);
  const hs = await hocSinhTrongLop(c, lop_id, hs_id);
  const mat_khau = tk.sinhMatKhauTam();
  await tk.datMatKhau(c.env, hs_id, mat_khau, true);
  await tk.huyPhienKhac(c.env, hs_id);
  return { id: hs_id, ho_ten: hs.ho_ten, ten_dang_nhap: hs.ten_dang_nhap, mat_khau_tam: mat_khau };
});

// ---------------------------------------------------------------------------
// Kết quả thí nghiệm
// ---------------------------------------------------------------------------

export const tenBai = () => new Map<string, string>(BAI_SINH.map((b) => [b.ma, b.ten]));

function tomTat(du_lieu: string): string {
  try {
    const v = JSON.parse(du_lieu);
    return String((v && typeof v === "object" && v.tom_tat) || "");
  } catch {
    return "";
  }
}

const hangKetQua = (k: any, ten_bai: Map<string, string>) => ({
  id: k.id, hoc_sinh_id: k.hoc_sinh_id, lop: k.lop, ma_bai: k.ma_bai, ten_bai: ten_bai.get(k.ma_bai) ?? k.ma_bai,
  thoi_gian: k.thoi_gian, tom_tat: tomTat(k.du_lieu), diem: k.diem, nhan_xet: k.nhan_xet,
});

export async function ketQuaTheoHocSinh(c: Ctx, ids: number[], ma_bai = ""): Promise<any[]> {
  if (!ids.length) return [];
  let sql = `SELECT * FROM ket_qua_tn WHERE hoc_sinh_id IN (${dauHoi(ids.length)})`;
  const args: any[] = [...ids];
  if (ma_bai) { sql += " AND ma_bai = ?"; args.push(ma_bai); }
  return q(c.env, sql + " ORDER BY thoi_gian DESC", ...args);
}

async function ketQuaCuaLop(c: Ctx, lop_id: number, ma_bai = "", hoc_sinh_id = 0) {
  const ho_ten = new Map((await tk.hocSinhCuaLop(c.env, lop_id)).map((h) => [h.id, h.ho_ten]));
  const ids = ho_ten.has(hoc_sinh_id) ? [hoc_sinh_id] : hoc_sinh_id ? [] : [...ho_ten.keys()];
  const tb = tenBai();
  return (await ketQuaTheoHocSinh(c, ids, ma_bai)).map((k) => ({ ...hangKetQua(k, tb), ho_ten: ho_ten.get(k.hoc_sinh_id) }));
}

tuyen("GET", "/api/lop/{lop_id:int}/ket-qua", async (c) => {
  const id = soNguyen(c, "lop_id");
  await lopCuaToi(c, id);
  return ketQuaCuaLop(c, id, thamSo(c, "ma_bai"), thamSoSo(c, "hoc_sinh_id", 0, { nguyen: true }));
});

// Ô bắt đầu bằng = + - @ bị Excel coi là công thức; họ tên do học sinh tự gõ nên phải vô hiệu hóa
export function oCsv(v: unknown): string {
  const chu = v === null || v === undefined ? "" : String(v);
  return ["=", "+", "-", "@", "\t", "\r"].includes(chu.slice(0, 1)) ? "'" + chu : chu;
}

export function dongCsv(o: string[]): string {
  return o.map((x) => (/[",\r\n]/.test(x) ? `"${x.replace(/"/g, '""')}"` : x)).join(",") + "\r\n";
}

export function phanHoiCsv(noi_dung: string, ten_tep: string): Response {
  return new Response("﻿" + noi_dung, {
    headers: { "content-type": "text/csv; charset=utf-8", "content-disposition": `attachment; filename=${ten_tep}` },
  });
}

// Giờ Việt Nam: máy chủ Cloudflare chạy giờ UTC, còn máy trường chạy giờ +7
function gioVN(t: number): string {
  const d = new Date((t + 7 * 3600) * 1000);
  const p = (n: number) => String(n).padStart(2, "0");
  return `${p(d.getUTCDate())}/${p(d.getUTCMonth() + 1)}/${d.getUTCFullYear()} ${p(d.getUTCHours())}:${p(d.getUTCMinutes())}`;
}

tuyen("GET", "/api/lop/{lop_id:int}/ket-qua.csv", async (c) => {
  const id = soNguyen(c, "lop_id");
  const lop = await lopCuaToi(c, id);
  let out = dongCsv(["Học sinh", "Lớp", "Bài", "Thời gian", "Tóm tắt", "Điểm", "Nhận xét"]);
  for (const k of await ketQuaCuaLop(c, id)) {
    out += dongCsv([k.ho_ten, lop.ten, k.ten_bai, gioVN(k.thoi_gian), k.tom_tat, k.diem, k.nhan_xet].map(oCsv));
  }
  return phanHoiCsv(out, `ket_qua_lop_${id}.csv`);
});

tuyen("GET", "/api/toi/ket-qua", async (c) => {
  const tb = tenBai();
  return (await ketQuaTheoHocSinh(c, [c.nd!.id])).map((k) => hangKetQua(k, tb));
});
