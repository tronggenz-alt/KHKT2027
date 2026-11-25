// Phòng học trực tuyến trên trình duyệt: giáo viên tạo phòng có mã 6 ký tự, học sinh vào bằng mã (tài khoản học sinh, hoặc khách
// nếu giáo viên cho phép), giáo viên quản lý trực tiếp và nhận trạng thái realtime qua WebSocket.
//
//   GET  /phong-truc-tuyen                    trang của giáo viên
//   GET  /phong-hoc                           trang của học sinh (công khai: khách cũng vào được nếu phòng cho phép)
//   GET  /api/phong                           phòng của tôi (đang mở và đã đóng gần đây)
//   POST /api/phong                           tạo phòng {ten, lop_id?, cho_khach?}
//   PATCH /api/phong/{id}                     đổi {ten?, cho_khach?, khoa_vao?}
//   POST /api/phong/{id}/dong                 đóng phòng, lưu bản tổng kết vào D1
//   GET  /api/phong/{id}/ket-noi              WebSocket của giáo viên
//   POST /api/vao-phong                       học sinh hoặc khách xin vào {ma, ho_ten?}
//   GET  /api/vao-phong/ket-noi?ma=&khach=    WebSocket của học sinh
//   GET  /api/vao-phong/anh?ma=&khach=&bai=&trang=   ảnh trang bài giảng đang chiếu trong phòng
//
// Phần realtime nằm trong Durable Object PhongTrucTuyen (cloudflare/phong/phong.ts, Worker verlab-phong). D1 chỉ giữ thông tin phòng
// và bản tổng kết khi đóng, để mỗi thay đổi trạng thái không tốn một lần ghi D1.

import { trang } from "./dang_nhap";
import { Ctx, Env, HttpLoi, bayGio, chay, chuoi, dung, ngauNhien, q, q1, soNguyen, soNguyenTruong, than, tuyen } from "./khung";
import * as tk from "./tai_khoan";

const BANG_MA = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // bỏ I, L, O, 0, 1 cho dễ đọc
const TOI_DA_PHONG_MO = 5;

function cua(env: Env, phong_id: number): DurableObjectStub {
  if (!env.PHONG) throw new HttpLoi(503, "Máy chủ chưa bật phòng trực tuyến (thiếu Worker verlab-phong)");
  return env.PHONG.get(env.PHONG.idFromName("phong:" + phong_id));
}

const dauNguoi = (n: Record<string, unknown>) => encodeURIComponent(JSON.stringify(n));

async function goi(env: Env, phong_id: number, duong: string, o: { body?: unknown; nguoi?: Record<string, unknown>; khach?: string } = {}): Promise<any> {
  const h: Record<string, string> = { "content-type": "application/json" };
  if (o.nguoi) h["x-vera-nguoi"] = dauNguoi(o.nguoi);
  if (o.khach) h["x-vera-khach"] = o.khach;
  const r = await cua(env, phong_id).fetch(new Request("https://phong" + duong, { method: o.body === undefined ? "GET" : "POST", headers: h, body: o.body === undefined ? undefined : JSON.stringify(o.body) }));
  const d: any = await r.json().catch(() => ({}));
  if (!r.ok) throw new HttpLoi(r.status, d.detail ?? "Phòng không trả lời");
  return d;
}

// WebSocket: trình duyệt gửi cookie kèm theo cả khi trang khác mở kết nối, nên phải kiểm Origin
function kiemNguonWs(c: Ctx): void {
  if (c.req.headers.get("upgrade")?.toLowerCase() !== "websocket") throw new HttpLoi(426, "Cần kết nối WebSocket");
  const origin = c.req.headers.get("origin");
  if (origin === null) return;
  let host = "";
  try { host = new URL(origin).host.toLowerCase(); } catch { /* sai */ }
  if (host !== (c.req.headers.get("host") || "").toLowerCase()) throw new HttpLoi(403, "Yêu cầu đến từ trang khác, đã chặn");
}

function chuyenWs(c: Ctx, phong_id: number, dau: Record<string, string>): Promise<Response> {
  const h = new Headers({ upgrade: "websocket", ...dau });
  for (const k of ["sec-websocket-key", "sec-websocket-version", "sec-websocket-extensions", "sec-websocket-protocol"]) {
    const v = c.req.headers.get(k);
    if (v) h.set(k, v);
  }
  return cua(c.env, phong_id).fetch(new Request("https://phong/ket-noi", { headers: h }));
}

interface PhongD1 { id: number; ma: string; ten: string; giao_vien_id: number; lop_id: number | null; cho_khach: number; khoa_vao: number; mo: number; tao_luc: number; dong_luc: number | null; tong_ket: string }

async function thongTinChoDo(env: Env, p: PhongD1) {
  const gv = await tk.timTheoId(env, p.giao_vien_id);
  const lop = p.lop_id ? await q1<{ ten: string }>(env, "SELECT ten FROM lop WHERE id=?", p.lop_id) : null;
  return { id: p.id, ma: p.ma, ten: p.ten, giao_vien: gv?.ho_ten ?? "", lop: lop?.ten ?? "", cho_khach: Boolean(p.cho_khach), khoa_vao: Boolean(p.khoa_vao), tao_luc: p.tao_luc };
}

function phongChoGv(p: PhongD1, ten_lop = "") {
  let tong_ket = null;
  try { tong_ket = p.tong_ket ? JSON.parse(p.tong_ket) : null; } catch { /* hỏng */ }
  return { id: p.id, ma: p.ma, ten: p.ten, lop_id: p.lop_id, ten_lop, cho_khach: Boolean(p.cho_khach), khoa_vao: Boolean(p.khoa_vao),
    mo: Boolean(p.mo), tao_luc: p.tao_luc, dong_luc: p.dong_luc, tong_ket };
}

async function phongCuaToi(c: Ctx, id: number): Promise<PhongD1> {
  const p = await q1<PhongD1>(c.env, "SELECT * FROM phong_truc_tuyen WHERE id=? AND giao_vien_id=?", id, c.nd!.id);
  if (!p) throw new HttpLoi(404, "Không có phòng này");
  return p;
}

const timPhongMo = (env: Env, ma: string) => q1<PhongD1>(env, "SELECT * FROM phong_truc_tuyen WHERE ma=? AND mo=1", (ma || "").trim().toUpperCase());

// ---------------------------------------------------------------------------
// Trang
// ---------------------------------------------------------------------------

tuyen("GET", "/phong-truc-tuyen", (c) => trang(c, "phong_truc_tuyen"));
tuyen("GET", "/phong-hoc", (c) => trang(c, "phong_hoc"));

// ---------------------------------------------------------------------------
// Giáo viên
// ---------------------------------------------------------------------------

tuyen("GET", "/api/phong", async (c) => {
  const ds = await q<PhongD1 & { ten_lop: string | null }>(c.env, `SELECT p.*, l.ten AS ten_lop FROM phong_truc_tuyen p LEFT JOIN lop l ON l.id=p.lop_id
    WHERE p.giao_vien_id=? AND (p.mo=1 OR p.dong_luc>?) ORDER BY p.mo DESC, p.id DESC LIMIT 30`, c.nd!.id, bayGio() - 30 * 86400);
  return ds.map((p) => phongChoGv(p, p.ten_lop ?? ""));
});

tuyen("POST", "/api/phong", async (c) => {
  const b = await than(c);
  const ten = chuoi(b, "ten", { md: "", toi_da: 120 }).trim() || "Phòng học " + new Date(Date.now() + 7 * 3600_000).toISOString().slice(11, 16).replace(":", "h");
  const lop_id = soNguyenTruong(b, "lop_id", { md: null });
  const cho_khach = dung(b, "cho_khach", { md: false });
  let ten_lop = "";
  if (lop_id !== null) {
    const lop = await tk.lopCuaGiaoVien(c.env, lop_id, c.nd!.id);
    if (!lop) throw new HttpLoi(404, "Không có lớp này");
    ten_lop = lop.ten;
  }
  const mo = await q1<{ n: number }>(c.env, "SELECT COUNT(*) AS n FROM phong_truc_tuyen WHERE giao_vien_id=? AND mo=1", c.nd!.id);
  if ((mo?.n ?? 0) >= TOI_DA_PHONG_MO) throw new HttpLoi(409, `Bạn đang mở ${TOI_DA_PHONG_MO} phòng. Hãy đóng bớt phòng cũ`);
  let id = 0, ma = "";
  for (let lan = 0; lan < 8 && !id; lan++) {
    ma = ngauNhien(BANG_MA, 6);
    try {
      id = (await chay(c.env, "INSERT INTO phong_truc_tuyen (ma, ten, giao_vien_id, lop_id, cho_khach, khoa_vao, mo, tao_luc) VALUES (?,?,?,?,?,0,1,?)",
        ma, ten, c.nd!.id, lop_id, cho_khach, bayGio())).id;
    } catch (e) {
      if (!String(e).includes("UNIQUE")) throw e;
    }
  }
  if (!id) throw new HttpLoi(500, "Không tạo được mã phòng, hãy thử lại");
  const p = (await phongCuaToi(c, id))!;
  try {
    await goi(c.env, id, "/khoi-tao", { body: await thongTinChoDo(c.env, p) });
  } catch (e) {
    await chay(c.env, "DELETE FROM phong_truc_tuyen WHERE id=?", id);
    throw e;
  }
  return phongChoGv(p, ten_lop);
});

tuyen("PATCH", "/api/phong/{phong_id:int}", async (c) => {
  const id = soNguyen(c, "phong_id");
  const p = await phongCuaToi(c, id);
  if (!p.mo) throw new HttpLoi(409, "Phòng đã đóng");
  const b = await than(c);
  const ten = chuoi(b, "ten", { md: null, toi_da: 120 }), cho_khach = dung(b, "cho_khach", { md: null }), khoa_vao = dung(b, "khoa_vao", { md: null });
  if (ten !== null && !ten.trim()) throw new HttpLoi(422, "Hãy đặt tên phòng");
  await chay(c.env, "UPDATE phong_truc_tuyen SET ten=COALESCE(?, ten), cho_khach=COALESCE(?, cho_khach), khoa_vao=COALESCE(?, khoa_vao) WHERE id=?",
    ten === null ? null : ten.trim(), cho_khach, khoa_vao, id);
  const moi = await phongCuaToi(c, id);
  await goi(c.env, id, "/khoi-tao", { body: await thongTinChoDo(c.env, moi) });
  return phongChoGv(moi);
});

tuyen("POST", "/api/phong/{phong_id:int}/dong", async (c) => {
  const id = soNguyen(c, "phong_id");
  const p = await phongCuaToi(c, id);
  if (!p.mo) return phongChoGv(p);
  const tong_ket = await goi(c.env, id, "/dong", { body: {} });
  await chay(c.env, "UPDATE phong_truc_tuyen SET mo=0, dong_luc=?, tong_ket=? WHERE id=?", bayGio(), JSON.stringify(tong_ket), id);
  return phongChoGv(await phongCuaToi(c, id));
});

tuyen("GET", "/api/phong/{phong_id:int}/tong-ket", async (c) => {
  const id = soNguyen(c, "phong_id");
  const p = await phongCuaToi(c, id);
  if (!p.mo) return phongChoGv(p).tong_ket;
  return goi(c.env, id, "/tong-ket");
});

tuyen("GET", "/api/phong/{phong_id:int}/ket-noi", async (c) => {
  kiemNguonWs(c);
  const id = soNguyen(c, "phong_id");
  const p = await phongCuaToi(c, id);
  if (!p.mo) throw new HttpLoi(409, "Phòng đã đóng");
  return chuyenWs(c, id, { "x-vera-nguoi": dauNguoi({ vai: "gv", ma: "g" + c.nd!.id, ho_ten: c.nd!.ho_ten }) });
});

// ---------------------------------------------------------------------------
// Học sinh và khách
// ---------------------------------------------------------------------------

function nguoiHs(c: Ctx) {
  const nd = c.nd!;
  return { vai: "hs", ma: "u" + nd.id, ho_ten: nd.ho_ten, ten_dang_nhap: nd.ten_dang_nhap, loai: "tai_khoan" };
}

// Học sinh đã đăng nhập có được vào phòng này không (chưa hỏi Durable Object)
async function kiemTaiKhoan(c: Ctx, p: PhongD1): Promise<void> {
  const nd = c.nd!;
  if (nd.phai_doi_mat_khau) throw new HttpLoi(403, "Hãy đổi mật khẩu trước khi vào phòng");
  if (nd.vai_tro === "giao_vien") throw new HttpLoi(403, "Giáo viên quản lý phòng ở trang Phòng trực tuyến, không vào như học sinh");
  if (p.lop_id !== null && !(await tk.coTrongLop(c.env, p.lop_id, nd.id))) {
    if (!p.cho_khach) throw new HttpLoi(403, "Phòng này chỉ dành cho học sinh của lớp đã chọn");
  }
}

tuyen("POST", "/api/vao-phong", async (c) => {
  const b = await than(c);
  const ma = chuoi(b, "ma", { toi_da: 16 }).trim().toUpperCase();
  const ho_ten = chuoi(b, "ho_ten", { md: "", toi_da: 60 }).trim().replace(/\s+/g, " ");
  const khach = chuoi(b, "khach", { md: "", toi_da: 64 });
  const giay = await tk.conBiKhoa(c.env, "vao-phong", c.ip);
  if (giay) throw new HttpLoi(429, `Nhập sai mã quá nhiều lần, thử lại sau ${Math.max(1, Math.floor((giay + 59) / 60))} phút`, { "Retry-After": String(giay) });
  const p = /^[A-Z0-9]{6}$/.test(ma) ? await timPhongMo(c.env, ma) : null;
  if (!p) {
    await tk.ghiSai(c.env, "vao-phong", c.ip, 20); // mã 6 ký tự đọc được, phải chặn dò thử
    throw new HttpLoi(404, "Không có phòng nào đang mở với mã này");
  }
  const info = await thongTinChoDo(c.env, p);
  const chung = { ma: p.ma, ten: p.ten, giao_vien: info.giao_vien, lop: info.lop };
  // Đã đăng nhập thì luôn vào bằng tài khoản (giáo viên nhận lỗi rõ ràng); chưa đăng nhập mới là khách
  if (c.nd) {
    await kiemTaiKhoan(c, p);
    await goi(c.env, p.id, "/kiem-vao", { body: {}, nguoi: nguoiHs(c) });
    return { ...chung, ban: { ho_ten: c.nd.ho_ten, loai: "tai_khoan" } };
  }
  if (!p.cho_khach) throw new HttpLoi(401, "Phòng này cần đăng nhập tài khoản học sinh", { "x-can-dang-nhap": "1" });
  if (khach) {
    // Khách đã vào trước đó (nối lại sau khi mất mạng): kiểm mã khách cũ, không tạo người mới
    await goi(c.env, p.id, "/kiem-vao", { body: {}, khach });
    return { ...chung, ban: { ho_ten, loai: "khach" }, khach };
  }
  if (!ho_ten) throw new HttpLoi(422, "Hãy nhập họ tên để giáo viên biết bạn là ai");
  if (ho_ten.length < 2) throw new HttpLoi(422, "Họ tên quá ngắn");
  const k = await goi(c.env, p.id, "/khach", { body: { ho_ten } });
  return { ...chung, ban: { ho_ten, loai: "khach" }, khach: k.khach };
});

tuyen("GET", "/api/vao-phong/ket-noi", async (c) => {
  kiemNguonWs(c);
  const p = await timPhongMo(c.env, c.url.searchParams.get("ma") || "");
  if (!p) throw new HttpLoi(404, "Phòng đã đóng hoặc không có");
  const khach = c.url.searchParams.get("khach") || "";
  if (khach) {
    if (!p.cho_khach) throw new HttpLoi(403, "Phòng không còn nhận khách");
    return chuyenWs(c, p.id, { "x-vera-khach": khach });
  }
  if (!c.nd) throw new HttpLoi(401, "Cần đăng nhập");
  await kiemTaiKhoan(c, p);
  return chuyenWs(c, p.id, { "x-vera-nguoi": dauNguoi(nguoiHs(c)) });
});

tuyen("GET", "/api/vao-phong/anh", async (c) => {
  const s = c.url.searchParams;
  const p = await timPhongMo(c.env, s.get("ma") || "");
  const bai = Number(s.get("bai")), so = Number(s.get("trang"));
  if (!p || !Number.isInteger(bai) || !Number.isInteger(so)) throw new HttpLoi(404, "Không có trang này");
  const khach = s.get("khach") || "";
  if (!khach && !c.nd) throw new HttpLoi(401, "Cần đăng nhập");
  await goi(c.env, p.id, `/duoc-xem?bai=${bai}&trang=${so}`, khach ? { khach } : { nguoi: nguoiHs(c) });
  const anh = await q1<{ loai: string; anh: ArrayBuffer }>(c.env,
    "SELECT t.loai, t.anh FROM trang_bai_giang t JOIN bai_giang b ON b.id=t.bai_id WHERE t.bai_id=? AND t.so=? AND b.giao_vien_id=?", bai, so, p.giao_vien_id);
  if (!anh) throw new HttpLoi(404, "Không có trang này");
  return new Response(new Uint8Array(anh.anh as any), { headers: { "content-type": anh.loai } });
});
