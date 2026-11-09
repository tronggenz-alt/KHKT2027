// Web quản lý phòng hóa (/api: kho hóa chất, thiết bị, nhật ký, mạng lưới khóa AI). Bản Cloudflare của server/lab/api.py.

import { CHEM, CHEM_BY_STT, EQUIP_BY_STT } from "./du_lieu";
import { Ctx, HttpLoi, bayGio, chay, chuoi, dung, fold, q, q1, so, soNguyen, soNguyenTruong, than, thamSo, thamSoDung, thamSoSo, tuyen } from "./khung";
import * as keypool from "./keypool";
import { ghiSuKien } from "./phong_hoc";

export async function ghiNhatKy(c: Ctx, loai: string, tieu_de: string, noi_dung = "", muc_do = "Info", nguon = ""): Promise<number> {
  return (await chay(c.env, "INSERT INTO nhat_ky (thoi_gian, loai, muc_do, tieu_de, noi_dung, nguon) VALUES (?,?,?,?,?,?)",
    bayGio(), loai, muc_do, tieu_de, noi_dung, nguon)).id;
}

const gon = (x: number) => String(Number(x.toPrecision(6))); // giống định dạng :g của Python

function hangChat(s: any) {
  const c = CHEM_BY_STT.get(s.stt);
  return { stt: c.stt, ten: c.ten, cong_thuc: c.cong_thuc, nhom: c.nhom, phan_loai: c.phan_loai, trang_thai: c.trang_thai, nong_do: c.nong_do,
    vi_tri: c.ma_vi_tri, nguy_hiem: c.nguy_hiem, so_luong: s.so_luong, don_vi: s.don_vi, toi_thieu: s.toi_thieu,
    han_dung: s.han_dung, ghi_chu: s.ghi_chu, sap_het: s.so_luong <= s.toi_thieu };
}

tuyen("GET", "/api/tong-quan", async (c) => {
  const stock = await q<any>(c.env, "SELECT * FROM ton_kho");
  const low = stock.filter((s) => s.so_luong <= s.toi_thieu);
  const eq = await q<any>(c.env, "SELECT * FROM thiet_bi");
  const groups = Object.fromEntries(CHEM.nhom.map((g: any) => [g.ma, { ten: g.ten, mau: g.mau, so_luong: g.so_luong }]));
  const su_co = (await q1<any>(c.env, "SELECT COUNT(*) AS n FROM nhat_ky WHERE thoi_gian>? AND muc_do IN ('Warning','Danger')", bayGio() - 7 * 86400))!.n;
  const keys = await keypool.listKeys(c.env);
  return { so_hoa_chat: stock.length, sap_het: low.length, so_ke: CHEM.ke.length,
    so_thiet_bi: eq.length, tong_dung_cu: eq.reduce((a, e) => a + e.so_luong, 0), dung_cu_hong: eq.reduce((a, e) => a + e.hong, 0),
    dang_muon: eq.reduce((a, e) => a + e.dang_muon, 0), su_co_7_ngay: su_co,
    khoa_api: keys.length, khoa_dung_duoc: keys.filter((k: any) => k.bat && k.dang_nghi_giay === 0 && k.trang_thai !== "khoa_sai").length,
    nhom: groups, ke: CHEM.ke, sap_het_ds: low.slice(0, 12).map(hangChat) };
});

tuyen("GET", "/api/hoa-chat", async (c) => {
  const qq = thamSo(c, "q"), nhom = thamSo(c, "nhom"), sap_het = thamSoDung(c, "sap_het");
  const trang = thamSoSo(c, "trang", 1, { nguyen: true }), moi_trang = thamSoSo(c, "moi_trang", 50, { nguyen: true });
  const words = fold(qq).split(/\s+/).filter(Boolean); // mọi từ đều phải có: "dong sunfat" khớp "Đồng(II) sunfat"
  const out: any[] = [];
  for (const s of await q<any>(c.env, "SELECT * FROM ton_kho ORDER BY stt")) {
    const ch = CHEM_BY_STT.get(s.stt);
    if (!ch) continue;
    if (nhom && ch.nhom !== nhom) continue;
    if (sap_het && s.so_luong > s.toi_thieu) continue;
    const hay = `${ch.ten_khong_dau} ${(ch.cong_thuc || "").toLowerCase()} ${ch.ma_vi_tri.toLowerCase()} #${ch.stt}`;
    if (words.length && !words.every((w) => hay.includes(w) || w === String(ch.stt))) continue;
    out.push(hangChat(s));
  }
  const start = Math.max(0, (trang - 1) * moi_trang);
  return { tong: out.length, trang, moi_trang, ds: out.slice(start, start + moi_trang) };
});

tuyen("PATCH", "/api/hoa-chat/{stt:int}", async (c) => {
  const stt = soNguyen(c, "stt");
  const s = await q1<any>(c.env, "SELECT * FROM ton_kho WHERE stt=?", stt);
  if (!s) throw new HttpLoi(404, "Không có hóa chất này");
  const b = await than(c);
  const so_luong = so(b, "so_luong", { md: null }), toi_thieu = so(b, "toi_thieu", { md: null }), them = so(b, "them", { md: null });
  const han_dung = chuoi(b, "han_dung", { md: null }), ghi_chu = chuoi(b, "ghi_chu", { md: null });
  let qty = s.so_luong;
  if (them) {
    qty = Math.max(0, qty + them);
    const ch = CHEM_BY_STT.get(stt);
    await ghiNhatKy(c, "kho", `${them > 0 ? "Nhập" : "Xuất"} ${gon(Math.abs(them))} ${s.don_vi} ${ch.ten}`, `Còn ${gon(qty)} ${s.don_vi}`, "Info", "web");
  }
  if (so_luong !== null) qty = Math.max(0, so_luong);
  await chay(c.env, "UPDATE ton_kho SET so_luong=?, toi_thieu=?, han_dung=?, ghi_chu=?, cap_nhat=? WHERE stt=?",
    qty, toi_thieu ?? s.toi_thieu, han_dung ?? s.han_dung, ghi_chu ?? s.ghi_chu, bayGio(), stt);
  return hangChat(await q1(c.env, "SELECT * FROM ton_kho WHERE stt=?", stt));
});

tuyen("GET", "/api/thiet-bi", async (c) =>
  (await q<any>(c.env, "SELECT * FROM thiet_bi ORDER BY stt")).map((r) => ({ ...EQUIP_BY_STT.get(r.stt), ...r, san_sang: r.so_luong - r.hong - r.dang_muon })));

tuyen("PATCH", "/api/thiet-bi/{stt:int}", async (c) => {
  const stt = soNguyen(c, "stt");
  const cu = await q1<any>(c.env, "SELECT * FROM thiet_bi WHERE stt=?", stt);
  if (!cu) throw new HttpLoi(404, "Không có thiết bị này");
  const b = await than(c);
  const moi: Record<string, any> = {
    so_luong: soNguyenTruong(b, "so_luong", { md: null }), hong: soNguyenTruong(b, "hong", { md: null }), dang_muon: soNguyenTruong(b, "dang_muon", { md: null }),
    vi_tri: chuoi(b, "vi_tri", { md: null }), ghi_chu: chuoi(b, "ghi_chu", { md: null }),
  };
  const r: any = { ...cu, ...Object.fromEntries(Object.entries(moi).filter(([, v]) => v !== null)) };
  r.hong = Math.max(0, Math.min(r.hong, r.so_luong));
  r.dang_muon = Math.max(0, Math.min(r.dang_muon, r.so_luong - r.hong));
  await chay(c.env, "UPDATE thiet_bi SET so_luong=?, hong=?, dang_muon=?, vi_tri=?, ghi_chu=?, cap_nhat=? WHERE stt=?",
    r.so_luong, r.hong, r.dang_muon, r.vi_tri, r.ghi_chu, bayGio(), stt);
  return { ...EQUIP_BY_STT.get(stt), ...r };
});

tuyen("GET", "/api/nhat-ky", (c) => {
  const loai = thamSo(c, "loai"), gioi_han = thamSoSo(c, "gioi_han", 200, { nguyen: true });
  return loai ? q(c.env, "SELECT * FROM nhat_ky WHERE loai=? ORDER BY id DESC LIMIT ?", loai, gioi_han)
    : q(c.env, "SELECT * FROM nhat_ky ORDER BY id DESC LIMIT ?", gioi_han);
});

tuyen("POST", "/api/nhat-ky", async (c) => {
  const b = await than(c);
  const loai = chuoi(b, "loai", { md: "ghi_chu" }), tieu_de = chuoi(b, "tieu_de"), noi_dung = chuoi(b, "noi_dung", { md: "" });
  const muc_do = chuoi(b, "muc_do", { md: "Info" }), nguon_gui = chuoi(b, "nguon", { md: "web" });
  const nd = c.nd;
  const nguon = nd && nd.vai_tro === "hoc_sinh" ? `hoc_sinh:${nd.ten_dang_nhap}` : nguon_gui; // học sinh không ghi giả được nguồn
  await ghiSuKien(c.env, nd, muc_do, tieu_de, noi_dung);
  return { id: await ghiNhatKy(c, loai, tieu_de.slice(0, 300), noi_dung.slice(0, 2000), muc_do, nguon) };
});

// ---------------------------------------------------------------------------
// Mạng lưới khóa API
// ---------------------------------------------------------------------------

tuyen("GET", "/api/khoa", async (c) => ({ nha_cung_cap: keypool.PROVIDERS, ds: await keypool.listKeys(c.env) }));

tuyen("POST", "/api/khoa", async (c) => {
  const b = await than(c);
  try {
    return { id: await keypool.addKey(c.env, chuoi(b, "nha_cung_cap"), chuoi(b, "khoa"), chuoi(b, "nhan", { md: "" }), chuoi(b, "mo_hinh", { md: "" }), soNguyenTruong(b, "uu_tien", { md: 10 })) };
  } catch (e) {
    if (e instanceof HttpLoi) throw e;
    throw new HttpLoi(400, (e as Error).message);
  }
});

tuyen("PATCH", "/api/khoa/{key_id:int}", async (c) => {
  const b = await than(c);
  await keypool.updateKey(c.env, soNguyen(c, "key_id"), { nhan: chuoi(b, "nhan", { md: null }), mo_hinh: chuoi(b, "mo_hinh", { md: null }),
    uu_tien: soNguyenTruong(b, "uu_tien", { md: null }), bat: dung(b, "bat", { md: null }) });
  return { ok: true };
});

tuyen("DELETE", "/api/khoa/{key_id:int}", async (c) => {
  await keypool.deleteKey(c.env, soNguyen(c, "key_id"));
  return { ok: true };
});

tuyen("POST", "/api/khoa/{key_id:int}/thu", async (c) => {
  try {
    return await keypool.testKey(c.env, soNguyen(c, "key_id"));
  } catch (e) {
    if (e instanceof HttpLoi) throw e;
    throw new HttpLoi(404, (e as Error).message);
  }
});

tuyen("POST", "/api/ai/thu", async (c) => {
  const b = await than(c);
  try {
    const res = await keypool.chat(c.env, [{ role: "system", content: "Bạn là trợ giảng hóa học THPT. Trả lời ngắn bằng tiếng Việt." },
      { role: "user", content: chuoi(b, "cau_hoi") }], 400);
    return { tra_loi: res.message.content || "", nha_cung_cap: res.nha_cung_cap, mo_hinh: res.mo_hinh, khoa_id: res.khoa_id };
  } catch (e) {
    if (e instanceof keypool.AllKeysFailed) throw new HttpLoi(503, e.message);
    throw e;
  }
});
