// Phòng thí nghiệm sinh học tầng 2 (/lab/sinh). Bản Cloudflare của server/lab/sinh_api.py và sinh_db.py.
// Trạng thái máy sinh học bản FastAPI giữ trong bộ nhớ; ở đây nằm trong bảng trang_thai_may của D1.

import { BAI_SINH, DUNG_CU_SINH, DUNG_CU_SINH_THEO_MA } from "./du_lieu";
import { Ctx, Env, HttpLoi, NguoiDung, bayGio, chay, chuoi, q, q1, so, soNguyen, soNguyenTruong, than, thamSo, thamSoSo, tuyen } from "./khung";
import { dongCsv, oCsv, phanHoiCsv } from "./lop";
import { ghiSuKien } from "./phong_hoc";
import * as tk from "./tai_khoan";

const laGiaoVien = (c: Ctx) => Boolean(c.nd) && c.nd!.vai_tro === "giao_vien";
const ketQua = (muc: any[]) => ({ muc, tong: muc.length });

// Thân PATCH: chỉ các trường có gửi lên (exclude_unset)
function coGui(b: Record<string, any>, kieu: Record<string, "s" | "n" | "i">): Record<string, any> {
  const out: Record<string, any> = {};
  for (const [k, t] of Object.entries(kieu)) {
    if (!(k in b)) continue;
    out[k] = t === "s" ? chuoi(b, k, { md: null }) : t === "n" ? so(b, k, { md: null }) : soNguyenTruong(b, k, { md: null });
  }
  return out;
}

async function capNhat(env: Env, bang: string, khoa: string, gia_tri: any, truong: Record<string, any>) {
  const k = Object.keys(truong);
  if (k.length) await chay(env, `UPDATE ${bang} SET ${k.map((x) => `${x} = ?`).join(", ")} WHERE ${khoa}=?`, ...Object.values(truong), gia_tri);
}

// ---------------------------------------------------------------------------
// Tổng quan
// ---------------------------------------------------------------------------

async function tongQuan(env: Env) {
  const now = bayGio();
  const dc = await q<any>(env, "SELECT * FROM sinh_dung_cu");
  const sp = await q<any>(env, "SELECT * FROM sinh_pham");
  const NGUONG: Record<string, [number, number]> = { tu_mat_4: [2, 8], tu_dong_am20: [-25, -15], tu_am_37: [35, 39] };
  const tu: any[] = [];
  for (const t of Object.keys(NGUONG)) {
    const r = await q1<any>(env, "SELECT * FROM nhiet_do_tu WHERE tu=? ORDER BY thoi_gian DESC LIMIT 1", t);
    if (r) {
      const [lo, hi] = NGUONG[t];
      tu.push({ tu: t, nhiet_do: r.nhiet_do, thoi_gian: r.thoi_gian, canh_bao: !(lo <= r.nhiet_do && r.nhiet_do <= hi) || r.cua_mo === 1, cua_mo: Boolean(r.cua_mo) });
    } else tu.push({ tu: t, nhiet_do: 0, thoi_gian: now, canh_bao: false, cua_mo: false });
  }
  const dau_ngay = now - (now % 86400), cuoi_ngay = dau_ngay + 86400;
  return {
    dung_cu: { tong: dc.reduce((a, r) => a + r.so_luong, 0), hong: dc.reduce((a, r) => a + r.hong, 0), dang_muon: dc.reduce((a, r) => a + r.dang_muon, 0),
      can_bao_tri: dc.reduce((a, r) => a + r.can_bao_tri, 0), qua_han_hieu_chuan: dc.filter((r) => r.han_hieu_chuan > 0 && r.han_hieu_chuan < now).length },
    sinh_pham: { sap_het: sp.filter((r) => r.so_luong <= 2), sap_het_han: sp.filter((r) => now <= r.han_dung && r.han_dung <= now + 30 * 86400), qua_han: sp.filter((r) => r.han_dung < now) },
    tu,
    su_co_moi: await q(env, "SELECT * FROM su_co_sinh WHERE trang_thai IN ('moi', 'dang_xu_ly') ORDER BY thoi_gian DESC LIMIT 10"),
    so_su_co_moi: (await q1<any>(env, "SELECT COUNT(*) AS n FROM su_co_sinh WHERE trang_thai IN ('moi', 'dang_xu_ly')"))!.n,
    lich_hom_nay: await q(env, "SELECT * FROM dat_lich WHERE (bat_dau >= ? AND bat_dau < ?) OR (bat_dau < ? AND ket_thuc > ?) ORDER BY bat_dau ASC", dau_ngay, cuoi_ngay, dau_ngay, dau_ngay),
  };
}

// Unity chỉ dùng để biết máy chủ còn sống; tồn kho, sự cố, người đặt lịch chỉ giáo viên xem
tuyen("GET", "/lab/sinh/tong-quan", async (c) => (laGiaoVien(c) ? tongQuan(c.env) : { ket_noi: true }));

// ---------------------------------------------------------------------------
// Dụng cụ
// ---------------------------------------------------------------------------

async function dsDungCu(env: Env, phong = "", phan_loai = "", tim = "") {
  const db = new Map((await q<any>(env, "SELECT * FROM sinh_dung_cu")).map((r) => [r.ma, r]));
  const w = tim.trim().toLowerCase();
  const out: any[] = [];
  for (const d of DUNG_CU_SINH) {
    if (phong && (Array.isArray(d.phong) ? !d.phong.includes(phong) : d.phong !== phong)) continue;
    if (phan_loai && d.phan_loai !== phan_loai) continue;
    if (w && !`${d.ma} ${d.ten || ""} ${d.cong_dung || ""}`.toLowerCase().includes(w)) continue;
    const r: any = db.get(d.ma) || {};
    out.push({ ...d, so_luong: r.so_luong ?? d.so_luong ?? 1, hong: r.hong ?? 0, dang_muon: r.dang_muon ?? 0, can_bao_tri: r.can_bao_tri ?? 0,
      vi_tri: r.vi_tri ?? d.vi_tri ?? "", lan_hieu_chuan: r.lan_hieu_chuan ?? 0, han_hieu_chuan: r.han_hieu_chuan ?? 0, ghi_chu: r.ghi_chu ?? "", cap_nhat: r.cap_nhat ?? 0 });
  }
  return out.sort((a, b) => (a.ma < b.ma ? -1 : a.ma > b.ma ? 1 : 0));
}

tuyen("GET", "/lab/sinh/dung-cu", async (c) => ketQua(await dsDungCu(c.env, thamSo(c, "phong"), thamSo(c, "phan_loai"), thamSo(c, "tim"))));

async function suaDungCu(env: Env, ma: string, f: Record<string, any>) {
  const cho_phep = ["so_luong", "hong", "dang_muon", "can_bao_tri", "vi_tri", "lan_hieu_chuan", "han_hieu_chuan", "ghi_chu"];
  const u = Object.fromEntries(Object.entries(f).filter(([k]) => cho_phep.includes(k)));
  if (Object.keys(u).length) await capNhat(env, "sinh_dung_cu", "ma", ma, { ...u, cap_nhat: bayGio() });
  return (await q1(env, "SELECT * FROM sinh_dung_cu WHERE ma=?", ma)) || {};
}

tuyen("PATCH", "/lab/sinh/dung-cu/{ma}", async (c) => {
  const ma = c.params.ma, st = DUNG_CU_SINH_THEO_MA.get(ma);
  if (!st) throw new HttpLoi(404, `Không tìm thấy dụng cụ '${ma}'`);
  const b = await than(c);
  const f = coGui(b, { so_luong: "i", hong: "i", dang_muon: "i", can_bao_tri: "i", vi_tri: "s", lan_hieu_chuan: "n", han_hieu_chuan: "n", ghi_chu: "s" });
  return { ...st, ...(await suaDungCu(c.env, ma, f)) };
});

// ---------------------------------------------------------------------------
// Sinh phẩm
// ---------------------------------------------------------------------------

tuyen("GET", "/lab/sinh/sinh-pham", (c) => {
  const dk: string[] = [], a: any[] = [];
  const tu = thamSo(c, "tu"), loai = thamSo(c, "loai"), tim = thamSo(c, "tim");
  if (tu) { dk.push("tu = ?"); a.push(tu); }
  if (loai) { dk.push("loai = ?"); a.push(loai); }
  if (tim) { const l = `%${tim.trim()}%`; dk.push("(id LIKE ? OR ten LIKE ? OR so_lo LIKE ?)"); a.push(l, l, l); }
  return q(c.env, `SELECT * FROM sinh_pham ${dk.length ? "WHERE " + dk.join(" AND ") : ""} ORDER BY id ASC`, ...a).then(ketQua);
});

tuyen("POST", "/lab/sinh/sinh-pham", async (c) => {
  const b = await than(c);
  const d = { id: chuoi(b, "id"), ten: chuoi(b, "ten"), loai: chuoi(b, "loai"), tu: chuoi(b, "tu"), ngan: chuoi(b, "ngan", { md: "" }) ?? "",
    so_luong: so(b, "so_luong", { md: 0 }), don_vi: chuoi(b, "don_vi", { md: "" }), so_lo: chuoi(b, "so_lo", { md: "" }) ?? "",
    han_dung: so(b, "han_dung", { md: null }), han_dung_ngay: so(b, "han_dung_ngay", { md: null }), cap_an_toan: chuoi(b, "cap_an_toan", { md: "BSL-1" }),
    ma_chat: chuoi(b, "ma_chat", { md: "" }) ?? "" };
  if (d.cap_an_toan !== "BSL-1") throw new HttpLoi(422, `Chỉ cho phép sinh phẩm cấp an toàn sinh học BSL-1 (nhận '${d.cap_an_toan}').`);
  const now = bayGio();
  const han = d.han_dung ?? now + (d.han_dung_ngay || 180) * 86400;
  await chay(c.env, "INSERT OR REPLACE INTO sinh_pham (id, ten, loai, tu, ngan, so_luong, don_vi, so_lo, han_dung, cap_an_toan, ma_chat, cap_nhat) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
    d.id, d.ten, d.loai, d.tu, d.ngan, d.so_luong, d.don_vi, d.so_lo, han, "BSL-1", d.ma_chat, now);
  return q1(c.env, "SELECT * FROM sinh_pham WHERE id=?", d.id);
});

tuyen("PATCH", "/lab/sinh/sinh-pham/{id}", async (c) => {
  const id = c.params.id, b = await than(c);
  const f = coGui(b, { ten: "s", loai: "s", tu: "s", ngan: "s", so_luong: "n", don_vi: "s", so_lo: "s", han_dung: "n", cap_an_toan: "s", ma_chat: "s" });
  if ("cap_an_toan" in f && f.cap_an_toan !== "BSL-1") throw new HttpLoi(422, `Chỉ cho phép cấp an toàn BSL-1 (nhận '${f.cap_an_toan}').`);
  if (Object.keys(f).length) await capNhat(c.env, "sinh_pham", "id", id, { ...f, cap_nhat: bayGio() });
  const r = await q1(c.env, "SELECT * FROM sinh_pham WHERE id=?", id);
  if (!r) throw new HttpLoi(404, `Không tìm thấy sinh phẩm '${id}'`);
  return r;
});

tuyen("DELETE", "/lab/sinh/sinh-pham/{id}", async (c) => {
  const id = c.params.id;
  if (!(await chay(c.env, "DELETE FROM sinh_pham WHERE id=?", id)).doi) throw new HttpLoi(404, `Không tìm thấy sinh phẩm '${id}'`);
  return { thanh_cong: true, id };
});

async function doiKhoSinhPham(env: Env, id: string, delta: number) {
  const r = await q1<any>(env, "SELECT * FROM sinh_pham WHERE id=?", id);
  if (!r) return null;
  await chay(env, "UPDATE sinh_pham SET so_luong=?, cap_nhat=? WHERE id=?", Math.max(0, r.so_luong + delta), bayGio(), id);
  return q1(env, "SELECT * FROM sinh_pham WHERE id=?", id);
}

for (const [duoi, dau] of [["xuat", -1], ["nhap", 1]] as const) {
  tuyen("POST", `/lab/sinh/sinh-pham/{id}/${duoi}`, async (c) => {
    const b = await than(c);
    so(b, "so_luong");
    chuoi(b, "nguoi", { md: "" }); chuoi(b, "ghi_chu", { md: "" });
    const r = await doiKhoSinhPham(c.env, c.params.id, dau * Math.abs(so(b, "so_luong")));
    if (!r) throw new HttpLoi(404, `Không tìm thấy sinh phẩm '${c.params.id}'`);
    return r;
  });
}

// ---------------------------------------------------------------------------
// Nhiệt độ tủ, đặt lịch, mượn trả, sự cố, bảo trì
// ---------------------------------------------------------------------------

async function themNhietDo(env: Env, tu: string, nhiet_do: number, cua_mo = 0, thoi_gian = 0) {
  const r = await chay(env, "INSERT INTO nhiet_do_tu (tu, thoi_gian, nhiet_do, cua_mo) VALUES (?,?,?,?)", tu, thoi_gian > 0 ? thoi_gian : bayGio(), nhiet_do, cua_mo);
  return q1(env, "SELECT * FROM nhiet_do_tu WHERE id=?", r.id);
}

tuyen("GET", "/lab/sinh/nhiet-do", (c) => {
  const dk: string[] = [], a: any[] = [];
  const tu = thamSo(c, "tu"), tu_luc = thamSoSo(c, "tu_luc", 0);
  if (tu) { dk.push("tu = ?"); a.push(tu); }
  if (tu_luc > 0) { dk.push("thoi_gian >= ?"); a.push(tu_luc); }
  return q(c.env, `SELECT * FROM nhiet_do_tu ${dk.length ? "WHERE " + dk.join(" AND ") : ""} ORDER BY thoi_gian ASC`, ...a).then(ketQua);
});

tuyen("POST", "/lab/sinh/nhiet-do", async (c) => {
  const b = await than(c);
  return themNhietDo(c.env, chuoi(b, "tu"), so(b, "nhiet_do"), soNguyenTruong(b, "cua_mo", { md: 0 }), so(b, "thoi_gian", { md: null }) || 0);
});

tuyen("GET", "/lab/sinh/dat-lich", async (c) => {
  const dt = thamSo(c, "doi_tuong");
  let muc = dt ? await q<any>(c.env, "SELECT * FROM dat_lich WHERE doi_tuong=? ORDER BY bat_dau ASC", dt) : await q<any>(c.env, "SELECT * FROM dat_lich ORDER BY bat_dau ASC");
  if (!laGiaoVien(c)) muc = muc.map(({ nguoi: _n, ...x }) => x); // học sinh xem được lịch nhưng không cần biết tên người đặt
  return ketQua(muc);
});

const trungLich = (env: Env, doi_tuong: string, bat_dau: number, ket_thuc: number, tru = 0) =>
  q(env, "SELECT * FROM dat_lich WHERE doi_tuong = ? AND id != ? AND bat_dau < ? AND ket_thuc > ?", doi_tuong, tru, ket_thuc, bat_dau);

const loiTrung = (trung: any[]) => new HttpLoi(409, { detail: "Trùng lịch đặt với lịch đã có", trung });

tuyen("POST", "/lab/sinh/dat-lich", async (c) => {
  const b = await than(c);
  const d = { doi_tuong: chuoi(b, "doi_tuong"), nguoi: chuoi(b, "nguoi"), lop: chuoi(b, "lop"), bat_dau: so(b, "bat_dau"), ket_thuc: so(b, "ket_thuc"), muc_dich: chuoi(b, "muc_dich", { md: "" }) ?? "" };
  if (d.ket_thuc <= d.bat_dau) throw new HttpLoi(422, "Thời gian kết thúc phải sau thời gian bắt đầu.");
  const trung = await trungLich(c.env, d.doi_tuong, d.bat_dau, d.ket_thuc);
  if (trung.length) throw loiTrung(trung);
  const r = await chay(c.env, "INSERT INTO dat_lich (doi_tuong, nguoi, lop, bat_dau, ket_thuc, muc_dich, tao_luc) VALUES (?,?,?,?,?,?,?)",
    d.doi_tuong, d.nguoi, d.lop, d.bat_dau, d.ket_thuc, d.muc_dich, bayGio());
  return q1(c.env, "SELECT * FROM dat_lich WHERE id=?", r.id);
});

tuyen("PATCH", "/lab/sinh/dat-lich/{id:int}", async (c) => {
  const id = soNguyen(c, "id"), b = await than(c);
  const f = coGui(b, { doi_tuong: "s", nguoi: "s", lop: "s", bat_dau: "n", ket_thuc: "n", muc_dich: "s" });
  const cu = await q1<any>(c.env, "SELECT * FROM dat_lich WHERE id=?", id);
  if (!cu) throw new HttpLoi(404, `Không tìm thấy lịch id=${id}`);
  const doi_tuong = f.doi_tuong ?? cu.doi_tuong, bat_dau = f.bat_dau ?? cu.bat_dau, ket_thuc = f.ket_thuc ?? cu.ket_thuc;
  if (ket_thuc <= bat_dau) throw new HttpLoi(422, "Thời gian kết thúc phải sau thời gian bắt đầu.");
  const trung = await trungLich(c.env, doi_tuong, bat_dau, ket_thuc, id);
  if (trung.length) throw loiTrung(trung);
  await capNhat(c.env, "dat_lich", "id", id, f);
  return q1(c.env, "SELECT * FROM dat_lich WHERE id=?", id);
});

tuyen("DELETE", "/lab/sinh/dat-lich/{id:int}", async (c) => {
  const id = soNguyen(c, "id");
  if (!(await chay(c.env, "DELETE FROM dat_lich WHERE id=?", id)).doi) throw new HttpLoi(404, `Không tìm thấy lịch id=${id}`);
  return { thanh_cong: true, id };
});

tuyen("GET", "/lab/sinh/muon-tra", (c) => {
  const dk: string[] = [], a: any[] = [];
  const ma = thamSo(c, "ma_dung_cu"), nguoi = thamSo(c, "nguoi");
  if (ma) { dk.push("ma_dung_cu = ?"); a.push(ma); }
  if (nguoi) { dk.push("nguoi = ?"); a.push(nguoi); }
  return q(c.env, `SELECT * FROM muon_tra ${dk.length ? "WHERE " + dk.join(" AND ") : ""} ORDER BY luc_muon DESC`, ...a).then(ketQua);
});

tuyen("POST", "/lab/sinh/muon-tra", async (c) => {
  const b = await than(c);
  const ma = chuoi(b, "ma_dung_cu"), sl = soNguyenTruong(b, "so_luong", { md: 1 }) || 1, nguoi = chuoi(b, "nguoi"), lop = chuoi(b, "lop");
  const ht = so(b, "han_tra", { md: null }), now = bayGio();
  const r = await chay(c.env, "INSERT INTO muon_tra (ma_dung_cu, so_luong, nguoi, lop, luc_muon, han_tra, tinh_trang_tra) VALUES (?,?,?,?,?,?,?)",
    ma, sl, nguoi, lop, now, ht ?? now + 7200, "chua_tra");
  await chay(c.env, "UPDATE sinh_dung_cu SET dang_muon = dang_muon + ? WHERE ma = ?", sl, ma);
  return q1(c.env, "SELECT * FROM muon_tra WHERE id=?", r.id);
});

tuyen("PATCH", "/lab/sinh/muon-tra/{id:int}", async (c) => {
  const id = soNguyen(c, "id"), b = await than(c);
  const lt = so(b, "luc_tra", { md: null }), tinh_trang = chuoi(b, "tinh_trang_tra", { md: "tot" }) || "tot";
  const cu = await q1<any>(c.env, "SELECT * FROM muon_tra WHERE id=?", id);
  if (!cu) throw new HttpLoi(404, `Không tìm thấy bản ghi mượn trả id=${id}`);
  await chay(c.env, "UPDATE muon_tra SET luc_tra = ?, tinh_trang_tra = ? WHERE id = ?", lt ?? bayGio(), tinh_trang, id);
  if (cu.tinh_trang_tra === "chua_tra") {
    // Trước đó chưa trả mà nay trả: giảm dang_muon, nếu hỏng thì tăng hong
    await chay(c.env, "UPDATE sinh_dung_cu SET dang_muon = max(0, dang_muon - ?) WHERE ma = ?", cu.so_luong, cu.ma_dung_cu);
    if (tinh_trang === "hong") await chay(c.env, "UPDATE sinh_dung_cu SET hong = hong + ? WHERE ma = ?", cu.so_luong, cu.ma_dung_cu);
  }
  return q1(c.env, "SELECT * FROM muon_tra WHERE id=?", id);
});

async function themSuCo(env: Env, d: any) {
  const r = await chay(env, "INSERT INTO su_co_sinh (phong, loai, muc_do, mo_ta, xu_ly, trang_thai, thoi_gian) VALUES (?,?,?,?,?,?,?)",
    d.phong, d.loai, d.muc_do || "Info", d.mo_ta, d.xu_ly || "", d.trang_thai || "moi", d.thoi_gian ?? bayGio());
  return q1(env, "SELECT * FROM su_co_sinh WHERE id=?", r.id);
}

tuyen("GET", "/lab/sinh/su-co", (c) => {
  const dk: string[] = [], a: any[] = [];
  const phong = thamSo(c, "phong"), tt = thamSo(c, "trang_thai");
  if (phong) { dk.push("phong = ?"); a.push(phong); }
  if (tt) { dk.push("trang_thai = ?"); a.push(tt); }
  return q(c.env, `SELECT * FROM su_co_sinh ${dk.length ? "WHERE " + dk.join(" AND ") : ""} ORDER BY thoi_gian DESC`, ...a).then(ketQua);
});

tuyen("POST", "/lab/sinh/su-co", async (c) => {
  const b = await than(c);
  return themSuCo(c.env, { phong: chuoi(b, "phong"), loai: chuoi(b, "loai"), muc_do: chuoi(b, "muc_do", { md: "Info" }), mo_ta: chuoi(b, "mo_ta"),
    xu_ly: chuoi(b, "xu_ly", { md: "" }), trang_thai: chuoi(b, "trang_thai", { md: "moi" }), thoi_gian: so(b, "thoi_gian", { md: null }) });
});

tuyen("PATCH", "/lab/sinh/su-co/{id:int}", async (c) => {
  const id = soNguyen(c, "id"), b = await than(c);
  await capNhat(c.env, "su_co_sinh", "id", id, coGui(b, { muc_do: "s", mo_ta: "s", xu_ly: "s", trang_thai: "s" }));
  const r = await q1(c.env, "SELECT * FROM su_co_sinh WHERE id=?", id);
  if (!r) throw new HttpLoi(404, `Không tìm thấy sự cố id=${id}`);
  return r;
});

tuyen("GET", "/lab/sinh/bao-tri", (c) => {
  const ma = thamSo(c, "ma_dung_cu");
  return (ma ? q(c.env, "SELECT * FROM bao_tri WHERE ma_dung_cu=? ORDER BY ngay DESC", ma) : q(c.env, "SELECT * FROM bao_tri ORDER BY ngay DESC")).then(ketQua);
});

tuyen("POST", "/lab/sinh/bao-tri", async (c) => {
  const b = await than(c);
  const ma = chuoi(b, "ma_dung_cu"), loai = chuoi(b, "loai"), ngay = so(b, "ngay", { md: null }) ?? bayGio();
  const r = await chay(c.env, "INSERT INTO bao_tri (ma_dung_cu, loai, ngay, nguoi, ket_qua, ghi_chu) VALUES (?,?,?,?,?,?)",
    ma, loai, ngay, chuoi(b, "nguoi"), chuoi(b, "ket_qua"), chuoi(b, "ghi_chu", { md: "" }) || "");
  if (loai.toLowerCase().includes("hieu_chuan")) {
    await chay(c.env, "UPDATE sinh_dung_cu SET lan_hieu_chuan = ?, han_hieu_chuan = ?, can_bao_tri = 0 WHERE ma = ?", ngay, ngay + 180 * 86400, ma);
  }
  return q1(c.env, "SELECT * FROM bao_tri WHERE id=?", r.id);
});

// ---------------------------------------------------------------------------
// Kết quả thực hành
// ---------------------------------------------------------------------------

async function trongPhamVi(c: Ctx): Promise<(k: any) => boolean> {
  // Kết quả gắn với học sinh chỉ giáo viên của học sinh đó xem được; kết quả không gắn tài khoản giáo viên nào cũng xem được
  const cua_toi = await tk.hocSinhCuaGiaoVien(c.env, c.nd!.id);
  return (k) => k.hoc_sinh_id === null || cua_toi.has(k.hoc_sinh_id);
}

async function ketQuaDuocXem(c: Ctx, id: number) {
  const r = await q1<any>(c.env, "SELECT * FROM ket_qua_tn WHERE id=?", id);
  if (!r || !(await trongPhamVi(c))(r)) throw new HttpLoi(404, `Không tìm thấy kết quả id=${id}`);
  return r;
}

async function dsKetQua(env: Env, lop = "", ma_bai = "", hoc_sinh = "") {
  const dk: string[] = [], a: any[] = [];
  if (lop) { dk.push("lop = ?"); a.push(lop); }
  if (ma_bai) { dk.push("ma_bai = ?"); a.push(ma_bai); }
  if (hoc_sinh) { dk.push("hoc_sinh LIKE ?"); a.push(`%${hoc_sinh.trim()}%`); }
  return q<any>(env, `SELECT * FROM ket_qua_tn ${dk.length ? "WHERE " + dk.join(" AND ") : ""} ORDER BY thoi_gian DESC`, ...a);
}

tuyen("GET", "/lab/sinh/ket-qua", async (c) => {
  const pv = await trongPhamVi(c);
  return ketQua((await dsKetQua(c.env, thamSo(c, "lop"), thamSo(c, "ma_bai"), thamSo(c, "hoc_sinh"))).filter(pv));
});

tuyen("GET", "/lab/sinh/ket-qua.csv", async (c) => {
  const pv = await trongPhamVi(c);
  let out = dongCsv(["ID", "Học sinh", "Lớp", "Bàn", "Mã bài", "Thời gian", "Điểm", "Nhận xét", "Dữ liệu JSON"]);
  for (const r of (await dsKetQua(c.env)).filter(pv)) {
    out += dongCsv([r.id, r.hoc_sinh, r.lop, r.ban, r.ma_bai, r.thoi_gian, r.diem, r.nhan_xet, r.du_lieu].map(oCsv));
  }
  return phanHoiCsv(out, "ket_qua_sinh_hoc.csv");
});

tuyen("GET", "/lab/sinh/ket-qua/{id:int}", (c) => ketQuaDuocXem(c, soNguyen(c, "id")));

async function themKetQua(env: Env, d: any) {
  let du_lieu = d.du_lieu ?? "{}";
  if (typeof du_lieu !== "string") du_lieu = JSON.stringify(du_lieu);
  const r = await chay(env, "INSERT INTO ket_qua_tn (hoc_sinh, lop, ban, ma_bai, thoi_gian, du_lieu, diem, nhan_xet, hoc_sinh_id) VALUES (?,?,?,?,?,?,?,?,?)",
    d.hoc_sinh, d.lop, d.ban ?? "", d.ma_bai, d.thoi_gian ?? bayGio(), du_lieu, d.diem === null || d.diem === undefined ? null : Number(d.diem), d.nhan_xet ?? "", d.hoc_sinh_id ?? null);
  return q1(env, "SELECT * FROM ket_qua_tn WHERE id=?", r.id);
}

// Chỉ giáo viên được ghi điểm. Kết quả từ tài khoản học sinh lấy tên, lớp từ tài khoản, không tin dữ liệu gửi lên.
async function ketQuaTheoNguoiGui(env: Env, dl: any, nd: NguoiDung | null) {
  dl = { ...dl, hoc_sinh_id: null };
  if (nd && nd.vai_tro === "giao_vien") return dl;
  Object.assign(dl, { diem: null, nhan_xet: "" });
  if (nd) {
    const lop = (await tk.lopCua(env, nd)).map((l) => l.ten);
    Object.assign(dl, { hoc_sinh: nd.ho_ten, hoc_sinh_id: nd.id, lop: lop.includes(dl.lop) ? dl.lop : lop[0] ?? "Chưa vào lớp" });
  }
  return dl;
}

tuyen("POST", "/lab/sinh/ket-qua", async (c) => {
  const b = await than(c);
  const d = { hoc_sinh: chuoi(b, "hoc_sinh"), lop: chuoi(b, "lop"), ban: chuoi(b, "ban", { md: "" }) ?? "", ma_bai: chuoi(b, "ma_bai"),
    thoi_gian: so(b, "thoi_gian", { md: null }), du_lieu: b.du_lieu ?? {}, diem: so(b, "diem", { md: null }), nhan_xet: chuoi(b, "nhan_xet", { md: "" }) ?? "" };
  return themKetQua(c.env, await ketQuaTheoNguoiGui(c.env, d, c.nd));
});

tuyen("PATCH", "/lab/sinh/ket-qua/{id:int}", async (c) => {
  const id = soNguyen(c, "id");
  await ketQuaDuocXem(c, id);
  const b = await than(c);
  const u: Record<string, any> = {};
  if ("diem" in b) u.diem = so(b, "diem", { md: null, nho: 0, lon: 10 });
  if ("nhan_xet" in b) u.nhan_xet = chuoi(b, "nhan_xet", { md: null });
  if ("du_lieu" in b) u.du_lieu = b.du_lieu !== null && typeof b.du_lieu === "object" ? JSON.stringify(b.du_lieu) : b.du_lieu;
  await capNhat(c.env, "ket_qua_tn", "id", id, u);
  return q1(c.env, "SELECT * FROM ket_qua_tn WHERE id=?", id);
});

// ---------------------------------------------------------------------------
// Hướng dẫn, bài thí nghiệm
// ---------------------------------------------------------------------------

tuyen("GET", "/lab/sinh/huong-dan", () => ketQua(DUNG_CU_SINH.map((d) => ({ ma: d.ma, ten: d.ten, phan_loai: d.phan_loai }))));

tuyen("GET", "/lab/sinh/huong-dan/{ma}", (c) => {
  const d = DUNG_CU_SINH_THEO_MA.get(c.params.ma);
  if (!d) throw new HttpLoi(404, `Không tìm thấy hướng dẫn cho dụng cụ '${c.params.ma}'`);
  return d;
});

const baiChoHocSinh = (b: any) => ({ ma: b.ma, ten: b.ten, lop: b.lop, sgk: b.sgk, muc_tieu: b.muc_tieu, dung_cu: b.dung_cu, nguon: b.nguon,
  buoc: b.buoc.map((x: any) => ({ so: x.so, mo_ta: x.mo_ta })), cau_hoi: b.cau_hoi.map((x: any) => ({ hoi: x.hoi })) });

tuyen("GET", "/lab/sinh/bai-thi-nghiem", (c) => ketQua(laGiaoVien(c) ? BAI_SINH : BAI_SINH.map(baiChoHocSinh)));

// ---------------------------------------------------------------------------
// Trạng thái máy, sự kiện từ Unity
// ---------------------------------------------------------------------------

const MAY_MAC_DINH: [string, string][] = [
  ["kinh_hien_vi", "Kính hiển vi quang học (29 máy)"], ["tu_an_toan_sinh_hoc", "Tủ an toàn sinh học cấp II A2"], ["tu_am_vi_sinh", "Tủ ấm vi sinh 37 °C"],
  ["noi_hap", "Nồi hấp tiệt trùng 121 °C"], ["may_pcr", "Máy luân nhiệt PCR 96 giếng"], ["be_dien_di", "Bể điện di ngang + bộ nguồn"],
  ["gel_doc", "Buồng chụp gel UV / Blue LED"], ["may_do_od600", "Máy đo quang phổ OD600"], ["tram_ghep_gen", "Trạm ghép gen (ADN tái tổ hợp)"],
];
const TRANG_THAI_HOP_LE = new Set(["san_sang", "dang_chay", "canh_bao", "hong"]);

async function ghiTrangThaiMay(env: Env, ma: string, trang_thai: string, mo_ta = "", tien_do = 0, luc: number | null = null) {
  if (!ma) throw new Error("thiếu mã máy 'ma'");
  if (!TRANG_THAI_HOP_LE.has(trang_thai)) throw new Error(`trạng thái '${trang_thai}' không hợp lệ (san_sang | dang_chay | canh_bao | hong)`);
  const muc = { ma, trang_thai, mo_ta, tien_do: Math.max(0, Math.min(1, Number(tien_do))), cap_nhat: luc || bayGio(), nguon: "unity" };
  await chay(env, "INSERT OR REPLACE INTO trang_thai_may (ma, du_lieu) VALUES (?,?)", ma, JSON.stringify(muc));
  return muc;
}

tuyen("GET", "/lab/sinh/trang-thai-may", async (c) => {
  const da = new Map((await q<any>(c.env, "SELECT ma, du_lieu FROM trang_thai_may")).map((r) => [r.ma, JSON.parse(r.du_lieu)]));
  const muc: any[] = MAY_MAC_DINH.map(([ma, ten]) => da.get(ma) ??
    { ma, trang_thai: "san_sang", mo_ta: `${ten}: chưa có số liệu từ phòng ảo`, tien_do: 0, cap_nhat: 0, nguon: "mac_dinh" });
  const co = new Set(MAY_MAC_DINH.map(([ma]) => ma));
  for (const [ma, v] of da) if (!co.has(ma)) muc.push(v);
  return ketQua(muc);
});

async function apSuKien(c: Ctx, loai: string, dl: any, t: number) {
  const env = c.env, nd = c.nd;
  if (loai === "su_co") {
    await ghiSuKien(env, nd, dl.muc_do ?? "", dl.mo_ta ?? "", "", t);
    await themSuCo(env, { phong: dl.phong ?? "phong_sinh", loai: dl.loai ?? "su_kien_lab", muc_do: dl.muc_do ?? "Info", mo_ta: dl.mo_ta ?? JSON.stringify(dl),
      xu_ly: dl.xu_ly ?? "", trang_thai: dl.trang_thai ?? "moi", thoi_gian: t });
  } else if (loai === "dung_cu_hong") {
    const ma = dl.ma ?? "", st = DUNG_CU_SINH_THEO_MA.get(ma);
    if (!st) throw new Error(`không có dụng cụ '${ma}'`);
    const hien = await q1<any>(env, "SELECT hong FROM sinh_dung_cu WHERE ma=?", ma);
    await suaDungCu(env, ma, { hong: (hien ? hien.hong : 0) + Math.max(1, Math.trunc(Number(dl.so_luong ?? 1))) });
    let phong = dl.phong || st.phong || "phong_sinh";
    if (Array.isArray(phong)) phong = phong[0] || "phong_sinh";
    await themSuCo(env, { phong, loai: "vo_dung_cu", muc_do: "canh_bao", mo_ta: `[${ma}] ${dl.ly_do ?? "Dụng cụ hỏng trong phòng ảo"}`, thoi_gian: t });
  } else if (loai === "can_bao_tri") {
    const ma = dl.ma ?? "";
    if (!DUNG_CU_SINH_THEO_MA.has(ma)) throw new Error(`không có dụng cụ '${ma}'`);
    await suaDungCu(env, ma, { can_bao_tri: 1, ghi_chu: dl.ly_do ?? "" });
  } else if (loai === "sinh_pham_xuat" || loai === "sinh_pham_nhap") {
    const sl = Math.abs(Number(dl.so_luong ?? 1));
    if (!(await doiKhoSinhPham(env, dl.id ?? "", loai === "sinh_pham_xuat" ? -sl : sl))) throw new Error(`Không tìm thấy sinh phẩm id=${dl.id ?? ""}`);
  } else if (loai === "nhiet_do_tu" || loai === "nhiet_do") {
    await themNhietDo(env, dl.tu ?? "tu_mat_4", Number(dl.nhiet_do ?? 4), dl.cua_mo ? 1 : 0, t);
  } else if (loai === "trang_thai_may") {
    await ghiTrangThaiMay(env, dl.ma ?? "", dl.trang_thai ?? "san_sang", dl.mo_ta ?? "", Number(dl.tien_do ?? 0), t);
  } else if (loai === "ket_qua_tn" || loai === "ket_qua") {
    dl = await ketQuaTheoNguoiGui(env, dl, nd);
    const ten_bai = new Map(BAI_SINH.map((b) => [b.ma, b.ten]));
    await ghiSuKien(env, nd, "", "Nộp kết quả: " + (ten_bai.get(dl.ma_bai) ?? String(dl.ma_bai)), "", t);
    await themKetQua(env, { hoc_sinh_id: dl.hoc_sinh_id, hoc_sinh: dl.hoc_sinh ?? "Học sinh", lop: dl.lop ?? "Lớp", ban: dl.ban ?? "",
      ma_bai: dl.ma_bai ?? "bai_chua_dat_ten", thoi_gian: t, du_lieu: dl.du_lieu ?? {}, diem: dl.diem, nhan_xet: dl.nhan_xet ?? "" });
  } else {
    throw new Error(`loại sự kiện '${loai}' không có trong hợp đồng`);
  }
}

tuyen("POST", "/lab/sinh/su-kien", async (c) => {
  const b = await than(c);
  const ds_sk = b.su_kien ?? [];
  if (!Array.isArray(ds_sk)) throw new HttpLoi(422, "su_kien phải là danh sách");
  let nhan = 0;
  const loi: any[] = [];
  for (const [i, it] of ds_sk.entries()) {
    try {
      if (!it || typeof it !== "object" || typeof it.loai !== "string") throw new Error("thiếu loai");
      const dl = it.du_lieu && typeof it.du_lieu === "object" && !Array.isArray(it.du_lieu) ? it.du_lieu : {};
      await apSuKien(c, it.loai, dl, Number(it.thoi_gian) || bayGio());
      nhan++;
    } catch (e) {
      loi.push({ chi_so: i, ly_do: (e as Error).message });
    }
  }
  return { nhan, loi };
});
