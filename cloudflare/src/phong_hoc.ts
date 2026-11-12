// Lớp học trực tiếp (bản Cloudflare của server/lab/nhip.py, trinh_chieu.py, truc_tiep.py, buoi_hoc.py).
//
//   POST /app/nhip                     app gọi khoảng 2 giây một lần: báo vị trí, việc đang làm; nhận trình chiếu, thông báo, lệnh robot, chú ý, buổi học
//   /api/bai-giang, /api/lop/{id}/trinh-chieu     trình chiếu bài giảng lên màn hình trong app
//   /api/mat-bang, /api/lop/{id}/truc-tiep, /thong-bao, /chu-y      theo dõi lớp
//   /api/lop/{id}/buoi-hoc, /robot     chế độ Kiểm tra, khóa hóa chất, lệnh robot
//
// Khác bản FastAPI: nhịp không giữ được trong bộ nhớ (mỗi lượt gọi có thể chạy trên máy khác) nên nằm trong bảng nhip của D1,
// mỗi máy một dòng ghi đè. Ảnh trang bài giảng do trình duyệt của giáo viên chuyển sẵn thành JPEG 1600×900 (tinh/chuyen_trang.js)
// và nằm trong bảng trang_bai_giang.

import { CHEM, CHEM_BY_STT } from "./du_lieu";
import { Ctx, Env, HttpLoi, NguoiDung, bayGio, cau, chay, chuoi, dauHoi, ds, dung, laSoNguyen, laChuoi, q, q1, so, soNguyen, soNguyenTruong, than, thamSoSo, tuyen } from "./khung";
import { lopCuaToi } from "./lop";
import { TEMPLATES, validate } from "./robot_plan";
import * as tk from "./tai_khoan";

export const GIAY_CON_MO = 8; // không thấy nhịp chừng này giây thì coi như máy đã tắt
const GIAY_TINH_LOI = 3 * 3600;
const GIAY_GIU_THONG_BAO = 300;
const GIU_SU_KIEN_NGAY = 30;
export const GIAY_GIU_LENH_ROBOT = 30;

// ---------------------------------------------------------------------------
// Nhịp
// ---------------------------------------------------------------------------

export async function docNhip(env: Env, ids: number[]): Promise<Map<number, any>> {
  if (!ids.length) return new Map();
  const r = await q<{ nguoi_dung_id: number; du_lieu: string }>(env,
    "SELECT nguoi_dung_id, du_lieu FROM nhip WHERE nguoi_dung_id IN (SELECT value FROM json_each(?))", JSON.stringify(ids));
  return new Map(r.map((x) => [x.nguoi_dung_id, JSON.parse(x.du_lieu)]));
}

const conMo = (n: any, now: number) => Boolean(n) && now - n.luc < GIAY_CON_MO;

// ---------------------------------------------------------------------------
// Bài giảng
// ---------------------------------------------------------------------------

const TOI_DA_MB = 60, TOI_DA_TRANG = 200, TOI_DA_ANH = 2 * 1024 * 1024;
const MAN_HINH_MAC_DINH = ["tuong_video", "t2_tuong_video"];
const MAN_HINH_HOP_LE = /^(t2_)?(tuong_video|ban|hanh_lang)(_\d+)?$/;

async function baiCuaToi(c: Ctx, bai_id: number) {
  const r = await q1<any>(c.env, "SELECT * FROM bai_giang WHERE id=? AND giao_vien_id=?", bai_id, c.nd!.id);
  if (!r) throw new HttpLoi(404, "Không có bài giảng này");
  return r;
}

tuyen("GET", "/api/bai-giang", (c) => q(c.env, "SELECT id, ten, so_trang, tao_luc FROM bai_giang WHERE giao_vien_id=? ORDER BY id DESC", c.nd!.id));

function loaiAnh(b: Uint8Array): string | null {
  if (b[0] === 0xff && b[1] === 0xd8 && b[2] === 0xff) return "image/jpeg";
  if (b[0] === 0x89 && b[1] === 0x50 && b[2] === 0x4e && b[3] === 0x47) return "image/png";
  return null;
}

tuyen("POST", "/api/bai-giang", async (c) => {
  // Trang nào cũng đã được trình duyệt chuyển thành ảnh 16:9; ở đây chỉ kiểm tra là ảnh thật, xếp theo tên tệp
  let form: FormData;
  try { form = await c.req.formData(); } catch { throw new HttpLoi(422, "Cần gửi biểu mẫu có ten và tep"); }
  const ten = String(form.get("ten") ?? "").trim();
  if (ten.length > 120) throw new HttpLoi(422, "Tên bài giảng dài quá 120 ký tự");
  if (!ten) throw new HttpLoi(422, "Hãy đặt tên bài giảng");
  const tep = form.getAll("tep").filter((t): t is File => typeof t !== "string").sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0));
  if (!tep.length) throw new HttpLoi(422, "Tệp không có trang nào");
  if (tep.length > TOI_DA_TRANG) throw new HttpLoi(422, `Mỗi bài giảng tối đa ${TOI_DA_TRANG} trang`);
  let tong = 0;
  const trang: { anh: Uint8Array; loai: string }[] = [];
  for (const t of tep) {
    const anh = new Uint8Array(await t.arrayBuffer());
    tong += anh.length;
    if (tong > TOI_DA_MB * 1024 * 1024) throw new HttpLoi(413, `Bài giảng lớn quá ${TOI_DA_MB} MB`);
    if (anh.length > TOI_DA_ANH) throw new HttpLoi(413, `Trang ${t.name} lớn quá 2 MB`);
    const loai = loaiAnh(anh);
    if (!loai) {
      throw new HttpLoi(422, String.fromCharCode(...anh.slice(0, 5)) === "%PDF-"
        ? "Trình duyệt chưa chuyển PDF thành ảnh. Hãy tải lại trang Trình chiếu rồi thử lại"
        : `${t.name} không phải PDF hoặc ảnh dùng được`);
    }
    trang.push({ anh, loai });
  }
  const r = await chay(c.env, "INSERT INTO bai_giang (giao_vien_id, ten, so_trang, tao_luc) VALUES (?,?,?,?)", c.nd!.id, ten, trang.length, bayGio());
  for (let i = 0; i < trang.length; i += 20) {
    await c.env.DB.batch(trang.slice(i, i + 20).map((t, j) =>
      cau(c.env, "INSERT INTO trang_bai_giang (bai_id, so, loai, anh) VALUES (?,?,?,?)", r.id, i + j + 1, t.loai, t.anh)));
  }
  return { id: r.id, ten, so_trang: trang.length };
});

tuyen("DELETE", "/api/bai-giang/{bai_id:int}", async (c) => {
  const id = soNguyen(c, "bai_id");
  await baiCuaToi(c, id);
  for (const r of await q<{ lop_id: number }>(c.env, "SELECT lop_id FROM trinh_chieu WHERE bai_id=? AND loai='slide'", id)) await ghiChieu(c.env, r.lop_id, "tat");
  await c.env.DB.batch([cau(c.env, "DELETE FROM trang_bai_giang WHERE bai_id=?", id), cau(c.env, "DELETE FROM bai_giang WHERE id=?", id)]);
  return { ok: true };
});

tuyen("GET", "/api/bai-giang/{bai_id:int}/trang/{so:int}", async (c) => {
  const bai_id = soNguyen(c, "bai_id"), s = soNguyen(c, "so"), nd = c.nd!;
  const duoc = (await q1(c.env, "SELECT 1 FROM bai_giang WHERE id=? AND giao_vien_id=?", bai_id, nd.id)) ||
    (await q1(c.env, `SELECT 1 FROM trinh_chieu t JOIN thanh_vien_lop m ON m.lop_id=t.lop_id
                      WHERE t.bai_id=? AND t.loai='slide' AND m.hoc_sinh_id=?`, bai_id, nd.id));
  const anh = duoc ? await q1<{ loai: string; anh: ArrayBuffer }>(c.env, "SELECT loai, anh FROM trang_bai_giang WHERE bai_id=? AND so=?", bai_id, s) : null;
  if (!anh) throw new HttpLoi(404, "Không có trang này");
  return new Response(new Uint8Array(anh.anh as any), { headers: { "content-type": anh.loai } });
});

// ---------------------------------------------------------------------------
// Đang chiếu
// ---------------------------------------------------------------------------

async function ghiChieu(env: Env, lop_id: number, loai: string, bai_id: number | null = null, trang = 1, tieu_de = "", noi_dung = "",
  man_hinh: string[] | null = null, dem_nguoc_den = 0): Promise<void> {
  // phien_ban luôn tăng, để app biết có việc mới mà không phải so từng trường
  const cu = await q1<{ phien_ban: number }>(env, "SELECT phien_ban FROM trinh_chieu WHERE lop_id=?", lop_id);
  const now = bayGio();
  const phien_ban = Math.max(Math.trunc(now * 1000), cu ? cu.phien_ban + 1 : 0);
  await chay(env, "INSERT OR REPLACE INTO trinh_chieu (lop_id, phien_ban, loai, bai_id, trang, tieu_de, noi_dung, man_hinh, dem_nguoc_den, cap_nhat) VALUES (?,?,?,?,?,?,?,?,?,?)",
    lop_id, phien_ban, loai, bai_id, trang, tieu_de, noi_dung, (man_hinh || MAN_HINH_MAC_DINH).join(","), dem_nguoc_den, now);
}

export function chieuChoApp(r: any) {
  if (!r || r.loai === "tat") return { phien_ban: r ? r.phien_ban : 0, loai: "tat", tieu_de: "", noi_dung: "", anh: "", man_hinh: [], dem_nguoc_den: 0 };
  const anh = r.loai === "slide" ? `/api/bai-giang/${r.bai_id}/trang/${r.trang}` : "";
  return { phien_ban: r.phien_ban, loai: r.loai, tieu_de: r.tieu_de, noi_dung: r.noi_dung, anh, man_hinh: r.man_hinh.split(","), dem_nguoc_den: r.dem_nguoc_den };
}

async function trangThaiChieu(env: Env, lop_id: number) {
  const r = await q1<any>(env, "SELECT * FROM trinh_chieu WHERE lop_id=?", lop_id);
  const dang: any = chieuChoApp(r);
  if (r && r.loai === "slide") {
    const bai = await q1<any>(env, "SELECT ten, so_trang FROM bai_giang WHERE id=?", r.bai_id);
    Object.assign(dang, { bai_id: r.bai_id, trang: r.trang, ten_bai: bai ? bai.ten : "", so_trang: bai ? bai.so_trang : 0 });
  }
  const hoc_sinh = await tk.hocSinhCuaLop(env, lop_id);
  const now = bayGio();
  const nhip = await docNhip(env, hoc_sinh.map((h) => h.id));
  const dang_mo = [...nhip.values()].filter((n) => conMo(n, now));
  const da_hien = dang_mo.filter((n) => dang.phien_ban && n.da_hien === dang.phien_ban);
  const tre = da_hien.map((n) => n.do_tre_ms).filter((x) => x !== null && x !== undefined);
  return { dang_chieu: dang, so_hoc_sinh: hoc_sinh.length, may_dang_mo: dang_mo.length, may_da_hien: da_hien.length,
    do_tre_ms: tre.length ? Math.round(tre.reduce((a, b) => a + b, 0) / tre.length) : null };
}

tuyen("GET", "/api/lop/{lop_id:int}/trinh-chieu", async (c) => {
  const id = soNguyen(c, "lop_id");
  await lopCuaToi(c, id);
  return trangThaiChieu(c.env, id);
});

tuyen("POST", "/api/lop/{lop_id:int}/trinh-chieu", async (c) => {
  const id = soNguyen(c, "lop_id");
  await lopCuaToi(c, id);
  const b = await than(c);
  const loai = chuoi(b, "loai"), bai_id = soNguyenTruong(b, "bai_id", { md: null }), trang = soNguyenTruong(b, "trang", { md: 1 });
  const tieu_de = chuoi(b, "tieu_de", { md: "", toi_da: 200 }), noi_dung = chuoi(b, "noi_dung", { md: "", toi_da: 2000 });
  const man_hinh = ds(b, "man_hinh", laChuoi("man_hinh"), { md: [...MAN_HINH_MAC_DINH], toi_da: 20 });
  const dem_nguoc_giay = soNguyenTruong(b, "dem_nguoc_giay", { md: 0, nho: 0, lon: 7200 });
  for (const m of man_hinh) if (!MAN_HINH_HOP_LE.test(m)) throw new HttpLoi(422, `Không có màn hình '${m}'`);
  if (!man_hinh.length && loai !== "tat") throw new HttpLoi(422, "Hãy chọn ít nhất một màn hình");
  const dem_nguoc_den = dem_nguoc_giay ? bayGio() + dem_nguoc_giay : 0;
  if (loai === "slide") {
    const bai = await baiCuaToi(c, bai_id || 0);
    if (trang < 1 || trang > bai.so_trang) throw new HttpLoi(422, `Bài giảng này chỉ có ${bai.so_trang} trang`);
    await ghiChieu(c.env, id, "slide", bai.id, trang, `${bai.ten} · Trang ${trang}/${bai.so_trang}`, "", man_hinh, dem_nguoc_den);
  } else if (loai === "chu") {
    if (!tieu_de.trim()) throw new HttpLoi(422, "Hãy nhập tiêu đề");
    await ghiChieu(c.env, id, "chu", null, 1, tieu_de.trim(), noi_dung.trim(), man_hinh, dem_nguoc_den);
  } else if (loai === "tat") {
    await ghiChieu(c.env, id, "tat");
  } else {
    throw new HttpLoi(422, "Loại phải là slide, chu hoặc tat");
  }
  return trangThaiChieu(c.env, id);
});

// ---------------------------------------------------------------------------
// Sự kiện của học sinh
// ---------------------------------------------------------------------------

const MUC_DO: Record<string, string> = { danger: "nguy_hiem", nguy_hiem: "nguy_hiem", warning: "canh_bao", canh_bao: "canh_bao" };

export async function ghiSuKien(env: Env, nd: NguoiDung | null, muc_do: string, tieu_de: string, chi_tiet = "", luc: number | null = null): Promise<void> {
  if (!nd || nd.vai_tro !== "hoc_sinh") return;
  const now = bayGio();
  await env.DB.batch([
    cau(env, "DELETE FROM su_kien_hoc_sinh WHERE thoi_gian<?", now - GIU_SU_KIEN_NGAY * 86400),
    cau(env, "INSERT INTO su_kien_hoc_sinh (hoc_sinh_id, thoi_gian, muc_do, tieu_de, chi_tiet) VALUES (?,?,?,?,?)",
      nd.id, Math.min(luc || now, now), MUC_DO[(muc_do || "").toLowerCase()] ?? "thong_tin", (tieu_de || "").slice(0, 300), (chi_tiet || "").slice(0, 1000)),
  ]);
}

// ---------------------------------------------------------------------------
// Buổi học
// ---------------------------------------------------------------------------

const CHE_DO = ["luyen_tap", "kiem_tra"];
export const LOI_AI_TAT = "Lớp đang làm bài kiểm tra nên giáo viên đã tắt trợ lý AI.";
const LOAI_NGUY_HIEM: Record<string, string> = { doc: "Độc", an_mon: "Ăn mòn", de_chay: "Dễ cháy", oxi_hoa: "Oxi hóa mạnh", phan_ung_nuoc: "Phản ứng mạnh với nước", no: "Dễ nổ" };

function tapKhoa(r: any): Set<number> {
  if (!r) return new Set();
  const loai = new Set<string>(JSON.parse(r.khoa_loai));
  const ngoai_le = new Set<number>(JSON.parse(r.ngoai_le));
  const out = new Set<number>();
  for (const c of CHEM.hoa_chat) if (c.nguy_hiem.some((x: string) => loai.has(x)) && !ngoai_le.has(c.stt)) out.add(c.stt);
  for (const s of JSON.parse(r.khoa_them)) out.add(s);
  return out;
}

async function buoiCuaNguoi(env: Env, nd: NguoiDung | null, lop?: number[]): Promise<any[]> {
  if (!nd || nd.vai_tro !== "hoc_sinh") return [];
  lop = lop ?? (await tk.lopCua(env, nd)).map((l) => l.id);
  if (!lop.length) return [];
  return q(env, `SELECT * FROM buoi_hoc WHERE lop_id IN (${dauHoi(lop.length)})`, ...lop);
}

export async function aiBiTat(env: Env, nd: NguoiDung | null): Promise<boolean> {
  return (await buoiCuaNguoi(env, nd)).some((r) => r.che_do === "kiem_tra");
}

export async function hoaChatBiKhoa(env: Env, nd: NguoiDung | null, hang?: any[]): Promise<Set<number>> {
  const out = new Set<number>();
  for (const r of hang ?? (await buoiCuaNguoi(env, nd))) for (const s of tapKhoa(r)) out.add(s);
  return out;
}

async function buoiChoApp(env: Env, nd: NguoiDung, lop: number[], da_buoi: number) {
  const rows = await buoiCuaNguoi(env, nd, lop);
  const phien_ban = Math.max(0, ...rows.map((r) => r.phien_ban));
  const kiem_tra = rows.some((r) => r.che_do === "kiem_tra");
  const het_gio = Math.max(0, ...rows.filter((r) => r.che_do === "kiem_tra").map((r) => r.het_gio));
  return { phien_ban, che_do: kiem_tra ? "kiem_tra" : "luyen_tap", ai_bat: !kiem_tra, robot_bat: !kiem_tra, het_gio,
    hoa_chat_khoa: da_buoi !== phien_ban ? [...(await hoaChatBiKhoa(env, nd, rows))].sort((a, b) => a - b) : null };
}

const chat = (stt: number) => {
  const c = CHEM_BY_STT.get(stt);
  return c ? { stt, ten: c.ten, cong_thuc: c.cong_thuc } : { stt, ten: `Chất ${stt}`, cong_thuc: "" };
};

async function trangThaiBuoi(env: Env, lop_id: number) {
  const r = await q1<any>(env, "SELECT * FROM buoi_hoc WHERE lop_id=?", lop_id);
  const khoa = tapKhoa(r);
  const now = bayGio();
  const hoc_sinh = await tk.hocSinhCuaLop(env, lop_id);
  const nhip = await docNhip(env, hoc_sinh.map((h) => h.id));
  const cuoi = await q1<any>(env, "SELECT id, hoc_sinh_id, noi_dung, tao_luc FROM lenh_app WHERE lop_id=? AND loai='robot' ORDER BY id DESC LIMIT 1", lop_id);
  let lenh_robot_cuoi = null;
  if (cuoi) {
    const da_nhan = hoc_sinh.filter((h) => {
      const n = nhip.get(h.id);
      return conMo(n, now) && (cuoi.hoc_sinh_id === null || cuoi.hoc_sinh_id === h.id) && (n.da_nhan_lenh || 0) >= cuoi.id;
    }).length;
    lenh_robot_cuoi = { ...cuoi, da_nhan };
  }
  return { che_do: r ? r.che_do : "luyen_tap", het_gio: r ? r.het_gio : 0, may_chu_luc: now,
    khoa_loai: r ? JSON.parse(r.khoa_loai) : [],
    ngoai_le: r ? JSON.parse(r.ngoai_le).map(chat) : [], khoa_them: r ? JSON.parse(r.khoa_them).map(chat) : [],
    so_chat_khoa: khoa.size, tong_so_chat: CHEM.hoa_chat.length,
    so_hoc_sinh: hoc_sinh.length, may_dang_mo: hoc_sinh.filter((h) => conMo(nhip.get(h.id), now)).length,
    hoc_sinh: hoc_sinh.map((h) => ({ id: h.id, ho_ten: h.ho_ten })),
    lenh_robot_cuoi,
    loai_nguy_hiem: Object.entries(LOAI_NGUY_HIEM).map(([ma, ten]) => ({ ma, ten, so_chat: CHEM.hoa_chat.filter((c: any) => c.nguy_hiem.includes(ma)).length })),
    thi_nghiem_mau: Object.entries(TEMPLATES).map(([khoa, ten]) => ({ khoa, ten })) };
}

tuyen("GET", "/api/lop/{lop_id:int}/buoi-hoc", async (c) => {
  const id = soNguyen(c, "lop_id");
  await lopCuaToi(c, id);
  return trangThaiBuoi(c.env, id);
});

tuyen("POST", "/api/lop/{lop_id:int}/buoi-hoc", async (c) => {
  const id = soNguyen(c, "lop_id");
  await lopCuaToi(c, id);
  const b = await than(c);
  const che_do = chuoi(b, "che_do", { md: "luyen_tap" }), phut = soNguyenTruong(b, "phut", { md: 0, nho: 0, lon: 300 });
  const khoa_loai = ds(b, "khoa_loai", laChuoi("khoa_loai"), { md: [], toi_da: 10 });
  const ngoai_le = ds(b, "ngoai_le", laSoNguyen("ngoai_le"), { md: [], toi_da: 720 });
  const khoa_them = ds(b, "khoa_them", laSoNguyen("khoa_them"), { md: [], toi_da: 720 });
  if (!CHE_DO.includes(che_do)) throw new HttpLoi(422, "Chế độ phải là luyen_tap hoặc kiem_tra");
  for (const ma of khoa_loai) if (!(ma in LOAI_NGUY_HIEM)) throw new HttpLoi(422, `Không có loại nguy hiểm '${ma}'`);
  for (const s of [...ngoai_le, ...khoa_them]) if (!CHEM_BY_STT.has(s)) throw new HttpLoi(422, `Không có hóa chất số ${s}`);
  const cu = await q1<any>(c.env, "SELECT phien_ban FROM buoi_hoc WHERE lop_id=?", id);
  const now = bayGio();
  const sx = <T,>(a: T[]) => JSON.stringify([...new Set(a)].sort((x: any, y: any) => (x < y ? -1 : x > y ? 1 : 0)));
  await chay(c.env, "INSERT OR REPLACE INTO buoi_hoc (lop_id, che_do, het_gio, khoa_loai, ngoai_le, khoa_them, phien_ban, cap_nhat) VALUES (?,?,?,?,?,?,?,?)",
    id, che_do, phut && che_do === "kiem_tra" ? now + phut * 60 : 0, sx(khoa_loai), sx(ngoai_le), sx(khoa_them),
    Math.max(Math.trunc(now * 1000), cu ? cu.phien_ban + 1 : 0), now);
  return trangThaiBuoi(c.env, id);
});

function keHoachRobot(b: Record<string, any>): [any[], string] {
  const hanh_dong = chuoi(b, "hanh_dong"), khoa = chuoi(b, "khoa", { md: "", toi_da: 60 });
  const stt = soNguyenTruong(b, "stt", { md: 0 }), loi = chuoi(b, "loi", { md: "", toi_da: 300 });
  let buoc: any, nhan: string;
  if (hanh_dong === "lam_mau") {
    const ten = TEMPLATES[khoa];
    if (!ten) throw new HttpLoi(422, "Không có thí nghiệm mẫu này");
    [buoc, nhan] = [{ ky_nang: "thi_nghiem_mau", hoa_chat: khoa }, `Làm mẫu: ${ten}`];
  } else if (hanh_dong === "lay_hoa_chat") {
    if (!CHEM_BY_STT.has(stt)) throw new HttpLoi(422, "Không có hóa chất này");
    [buoc, nhan] = [{ ky_nang: "lay_hoa_chat", hoa_chat: String(stt) }, `Lấy ${CHEM_BY_STT.get(stt).ten} ra bàn`];
  } else if (["tat_lua", "bat_tu_hut", "don_ban"].includes(hanh_dong)) {
    [buoc, nhan] = [{ ky_nang: hanh_dong }, ({ tat_lua: "Tắt mọi nguồn lửa", bat_tu_hut: "Bật tủ hút", don_ban: "Dọn bàn" } as any)[hanh_dong]];
  } else if (hanh_dong === "noi") {
    if (!loi.trim()) throw new HttpLoi(422, "Hãy nhập câu robot sẽ nói");
    [buoc, nhan] = [{ ky_nang: "noi", loi: loi.trim() }, `Robot nói: ${loi.trim()}`];
  } else {
    throw new HttpLoi(422, "Không có lệnh này");
  }
  const theo_so = (x: string) => (/^\d+$/.test(String(x)) ? CHEM_BY_STT.get(Number(x)) ?? null : null);
  const [ke_hoach, bo] = validate([buoc], new Set(), theo_so);
  if (bo.length || !ke_hoach.length) throw new HttpLoi(422, bo.join("; ") || "Không tạo được lệnh");
  return [ke_hoach, nhan];
}

tuyen("POST", "/api/lop/{lop_id:int}/robot", async (c) => {
  const id = soNguyen(c, "lop_id");
  await lopCuaToi(c, id);
  const b = await than(c);
  const hs = soNguyenTruong(b, "hoc_sinh_id", { md: null });
  if (hs !== null && !(await tk.coTrongLop(c.env, id, hs))) throw new HttpLoi(404, "Học sinh này không có trong lớp");
  const [ke_hoach, nhan] = keHoachRobot(b);
  const r = await chay(c.env, "INSERT INTO lenh_app (lop_id, hoc_sinh_id, loai, noi_dung, ke_hoach, tao_luc) VALUES (?,?,?,?,?,?)",
    id, hs, "robot", nhan, JSON.stringify(ke_hoach), bayGio());
  return { id: r.id, noi_dung: nhan, ke_hoach };
});

// ---------------------------------------------------------------------------
// Nhịp của app và theo dõi trực tiếp
// ---------------------------------------------------------------------------

export const KHU = [
  { ma: "phong_lab", ten: "Phòng thí nghiệm", x0: 0, z0: 0, x1: 20, z1: 15, tang: 1 },
  { ma: "kho_hoa_chat", ten: "Phòng hóa chất", x0: 20, z0: 0, x1: 27, z1: 15, tang: 1 },
  { ma: "hanh_lang", ten: "Hành lang", x0: 0, z0: -2.2, x1: 27, z1: 0, tang: 1 },
  { ma: "phong_may", ten: "Phòng máy phân tích", x0: 0, z0: -9, x1: 13.5, z1: -2.2, tang: 1 },
  { ma: "phong_robot", ten: "Phòng tự động hóa & robot", x0: 13.5, z0: -9, x1: 27, z1: -2.2, tang: 1 },
  { ma: "phong_sinh", ten: "Phòng thí nghiệm sinh học", x0: 0, z0: 0, x1: 20, z1: 15, tang: 2 },
  { ma: "phong_vi_sinh", ten: "Phòng vi sinh", x0: 20, z0: 7.5, x1: 27, z1: 15, tang: 2 },
  { ma: "kho_sinh_pham", ten: "Kho sinh phẩm", x0: 20, z0: 0, x1: 27, z1: 7.5, tang: 2 },
  { ma: "hanh_lang_t2", ten: "Hành lang tầng 2", x0: 0, z0: -3.6, x1: 27, z1: 0, tang: 2 },
  { ma: "phong_sinh_phan_tu", ten: "Phòng sinh học phân tử", x0: 0, z0: -9, x1: 13.5, z1: -3.6, tang: 2 },
  { ma: "nha_kinh", ten: "Nhà kính thực vật", x0: 13.5, z0: -9, x1: 27, z1: -3.6, tang: 2 },
  { ma: "hanh_lang_dong", ten: "Hành lang đông tầng 2", x0: 27, z0: 6, x1: 30, z1: 15, tang: 2 },
  { ma: "loi_thang", ten: "Cầu thang và thang máy", x0: 27, z0: -2.2, x1: 30, z1: 6, tang: 0 }, // 0: cả hai tầng
];

function khuTai(tang: number | null, x: number | null, z: number | null) {
  if (x === null || x === undefined || z === null || z === undefined) return null;
  return KHU.find((k) => (k.tang === 0 || k.tang === tang) && k.x0 <= x && x < k.x1 && k.z0 <= z && z < k.z1) ?? null;
}

tuyen("POST", "/app/nhip", async (c) => {
  const b = await than(c);
  const moi = {
    da_hien: soNguyenTruong(b, "da_hien", { md: 0 }), da_nhan_lenh: soNguyenTruong(b, "da_nhan_lenh", { md: 0 }),
    da_buoi: soNguyenTruong(b, "da_buoi", { md: 0 }), dang_khoa: dung(b, "dang_khoa", { md: false }),
    tang: soNguyenTruong(b, "tang", { md: null, nho: 1, lon: 2 }), x: so(b, "x", { md: null, nho: -500, lon: 500 }), z: so(b, "z", { md: null, nho: -500, lon: 500 }),
    dang_cam: chuoi(b, "dang_cam", { md: "", toi_da: 80 }), dang_lam: chuoi(b, "dang_lam", { md: "", toi_da: 160 }),
  };
  const nd = c.nd!, now = bayGio(), env = c.env;
  const lop = (await tk.lopCua(env, nd)).map((l) => l.id);
  const la_hs = nd.vai_tro === "hoc_sinh" && lop.length > 0;
  const dk = lop.length ? dauHoi(lop.length) : "";
  // Gộp các lượt đọc thành một lần gọi CSDL
  const doc = await env.DB.batch([
    cau(env, "SELECT du_lieu FROM nhip WHERE nguoi_dung_id=?", nd.id),
    lop.length ? cau(env, `SELECT * FROM trinh_chieu WHERE lop_id IN (${dk}) ORDER BY (loai='tat'), cap_nhat DESC LIMIT 1`, ...lop) : cau(env, "SELECT NULL WHERE 0"),
    la_hs ? cau(env, `SELECT id, loai, noi_dung, ke_hoach FROM lenh_app WHERE lop_id IN (${dk}) AND (hoc_sinh_id IS NULL OR hoc_sinh_id=?) AND id>?
                     AND ((loai='robot' AND tao_luc>?) OR (loai<>'robot' AND tao_luc>?)) ORDER BY id LIMIT 20`,
      ...lop, nd.id, moi.da_nhan_lenh, now - GIAY_GIU_LENH_ROBOT, now - GIAY_GIU_THONG_BAO) : cau(env, "SELECT NULL WHERE 0"),
    la_hs ? cau(env, `SELECT noi_dung FROM chu_y_lop WHERE lop_id IN (${dk}) ORDER BY tu_luc DESC LIMIT 1`, ...lop) : cau(env, "SELECT NULL WHERE 0"),
  ]);
  const cu: any = doc[0].results[0] ? JSON.parse((doc[0].results[0] as any).du_lieu) : {};
  const viec: any = doc[1].results[0] ?? null;
  let do_tre = cu.do_tre_ms ?? null;
  if (viec && moi.da_hien === viec.phien_ban && cu.da_hien !== moi.da_hien) do_tre = Math.round((now - viec.cap_nhat) * 1000);
  // Không đổi gì thì 4 giây mới ghi một lần: đỡ tốn hạn mức ghi của D1 mà máy vẫn được tính là đang mở (8 giây)
  const giong = Object.entries(moi).every(([k, v]) => cu[k] === v) && cu.do_tre_ms === do_tre && now - (cu.luc || 0) < 4;
  if (!giong) await chay(env, "INSERT OR REPLACE INTO nhip (nguoi_dung_id, du_lieu, luc) VALUES (?,?,?)", nd.id, JSON.stringify({ ...moi, luc: now, do_tre_ms: do_tre }), now);
  const lenh = (doc[2].results as any[]).map((r) => (r.loai === "robot" ? { id: r.id, loai: r.loai, noi_dung: r.noi_dung, ke_hoach: JSON.parse(r.ke_hoach) } : { id: r.id, loai: r.loai, noi_dung: r.noi_dung }));
  const cy: any = doc[3].results[0];
  return { may_chu_luc: now, trinh_chieu: chieuChoApp(viec), lenh, chu_y: cy ? { bat: true, noi_dung: cy.noi_dung } : { bat: false, noi_dung: "" },
    buoi: await buoiChoApp(env, nd, lop, moi.da_buoi) };
});

tuyen("GET", "/api/mat-bang", () => KHU);

tuyen("GET", "/api/lop/{lop_id:int}/truc-tiep", async (c) => {
  const lop_id = soNguyen(c, "lop_id");
  await lopCuaToi(c, lop_id);
  const sau_id = thamSoSo(c, "sau_id", 0, { nguyen: true, nho: 0 });
  const env = c.env, now = bayGio();
  const dsh = await tk.hocSinhCuaLop(env, lop_id);
  const ten = new Map(dsh.map((h) => [h.id, h.ho_ten]));
  const ids = [...ten.keys()];
  const nhip = await docNhip(env, ids);
  let su_kien: any[] = [];
  const dem = new Map<number, Record<string, number>>();
  if (ids.length) {
    const j = JSON.stringify(ids);
    for (const r of await q<any>(env, `SELECT hoc_sinh_id, muc_do, COUNT(*) AS n FROM su_kien_hoc_sinh
        WHERE hoc_sinh_id IN (SELECT value FROM json_each(?)) AND thoi_gian>? AND muc_do<>'thong_tin' GROUP BY hoc_sinh_id, muc_do`, j, now - GIAY_TINH_LOI)) {
      dem.set(r.hoc_sinh_id, { ...(dem.get(r.hoc_sinh_id) || {}), [r.muc_do]: r.n });
    }
    su_kien = (await q<any>(env, `SELECT * FROM su_kien_hoc_sinh WHERE hoc_sinh_id IN (SELECT value FROM json_each(?)) AND id>? ORDER BY id DESC LIMIT 40`, j, sau_id))
      .reverse().map((k) => ({ ...k, ho_ten: ten.get(k.hoc_sinh_id) }));
  }
  const hoc_sinh = dsh.map((h) => {
    const n = nhip.get(h.id) || {};
    const dang_mo = conMo(nhip.get(h.id), now);
    const khu = dang_mo ? khuTai(n.tang ?? null, n.x ?? null, n.z ?? null) : null;
    const s = dem.get(h.id) || {};
    return { id: h.id, ho_ten: h.ho_ten, ten_dang_nhap: h.ten_dang_nhap, dang_mo, giay_truoc: nhip.has(h.id) ? Math.round(now - n.luc) : null,
      tang: dang_mo ? n.tang ?? null : null, x: dang_mo ? n.x ?? null : null, z: dang_mo ? n.z ?? null : null,
      khu: khu ? khu.ma : "", ten_khu: khu ? khu.ten : dang_mo && n.x !== null && n.x !== undefined ? "Ngoài trời" : "",
      dang_cam: dang_mo ? n.dang_cam || "" : "", dang_lam: dang_mo ? n.dang_lam || "" : "",
      so_canh_bao: s.canh_bao || 0, so_nguy_hiem: s.nguy_hiem || 0 };
  });
  const dang_mo = hoc_sinh.filter((h) => h.dang_mo).map((h) => h.id);
  const chu_y = await q1<any>(env, "SELECT noi_dung, tu_luc FROM chu_y_lop WHERE lop_id=?", lop_id);
  const cuoi = await q1<any>(env, "SELECT id, hoc_sinh_id, noi_dung, tao_luc FROM lenh_app WHERE lop_id=? AND loai='thong_bao' ORDER BY id DESC LIMIT 1", lop_id);
  let thong_bao = null;
  if (cuoi) {
    const nhan = dang_mo.filter((i) => (cuoi.hoc_sinh_id === null || cuoi.hoc_sinh_id === i) && (nhip.get(i)?.da_nhan_lenh || 0) >= cuoi.id);
    thong_bao = { ...cuoi, da_nhan: nhan.length, gui_toi: ten.get(cuoi.hoc_sinh_id) ?? "Cả lớp" };
  }
  return { may_chu_luc: now, so_hoc_sinh: dsh.length, may_dang_mo: dang_mo.length, hoc_sinh, su_kien,
    chu_y: { bat: Boolean(chu_y), noi_dung: chu_y ? chu_y.noi_dung : "", tu_luc: chu_y ? chu_y.tu_luc : 0,
      may_da_khoa: dang_mo.filter((i) => nhip.get(i)?.dang_khoa).length },
    thong_bao_cuoi: thong_bao };
});

tuyen("POST", "/api/lop/{lop_id:int}/thong-bao", async (c) => {
  const id = soNguyen(c, "lop_id");
  await lopCuaToi(c, id);
  const b = await than(c);
  const noi_dung = chuoi(b, "noi_dung", { toi_da: 300 }).trim(), hs = soNguyenTruong(b, "hoc_sinh_id", { md: null });
  if (!noi_dung) throw new HttpLoi(422, "Hãy nhập nội dung");
  if (hs !== null && !(await tk.coTrongLop(c.env, id, hs))) throw new HttpLoi(404, "Học sinh này không có trong lớp");
  const r = await chay(c.env, "INSERT INTO lenh_app (lop_id, hoc_sinh_id, loai, noi_dung, tao_luc) VALUES (?,?,?,?,?)", id, hs, "thong_bao", noi_dung, bayGio());
  return { id: r.id };
});

tuyen("POST", "/api/lop/{lop_id:int}/chu-y", async (c) => {
  const id = soNguyen(c, "lop_id");
  await lopCuaToi(c, id);
  const b = await than(c);
  const bat = dung(b, "bat"), noi_dung = chuoi(b, "noi_dung", { md: "", toi_da: 200 });
  if (bat) await chay(c.env, "INSERT OR REPLACE INTO chu_y_lop (lop_id, noi_dung, tu_luc) VALUES (?,?,?)", id, noi_dung.trim() || "Tất cả chú ý lên giáo viên", bayGio());
  else await chay(c.env, "DELETE FROM chu_y_lop WHERE lop_id=?", id);
  return { bat };
});
