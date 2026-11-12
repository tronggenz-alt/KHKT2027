// Đăng nhập và phân quyền cho toàn bộ máy chủ (bản Cloudflare của server/lab/dang_nhap.py).
//
// Quyền kiểm tra tại một chỗ (kiemQuyen), chạy trước mọi đường dẫn. Đường dẫn không có trong QUYEN thì chỉ giáo viên vào được.
// Khác bản FastAPI: không có lối riêng 127.0.0.1 cho Unity (trên mạng không có "cùng máy"), nên các đường dẫn UNITY cần đăng nhập.

import { Ctx, HttpLoi, NguoiDung, chuoi, json, than, tuyen } from "./khung";
import * as tk from "./tai_khoan";

export const COOKIE = "vera_phien";
const CONG_KHAI = "cong_khai", DA_DANG_NHAP = "da_dang_nhap";

const QUYEN: Record<string, string> = {
  "GET /dang-nhap": CONG_KHAI,
  "GET /quan-ly": CONG_KHAI, // tự chuyển hướng theo vai trò
  "POST /auth/dang-nhap": CONG_KHAI,
  "POST /auth/dang-ky": CONG_KHAI,
  "POST /auth/dang-xuat": CONG_KHAI,
  "GET /suc-khoe": CONG_KHAI,
  // ứng dụng Unity đang gọi (bản FastAPI cho gọi không cần đăng nhập từ cùng máy)
  "POST /chu-thich": DA_DANG_NHAP,
  "POST /tro-ly": DA_DANG_NHAP,
  "POST /robot/ke-hoach": DA_DANG_NHAP,
  "POST /api/nhat-ky": DA_DANG_NHAP,
  "POST /lab/sinh/su-kien": DA_DANG_NHAP,
  "GET /lab/sinh/tong-quan": DA_DANG_NHAP,
  // giáo viên và học sinh đều xem được
  "GET /auth/toi": DA_DANG_NHAP,
  "POST /auth/doi-mat-khau": DA_DANG_NHAP,
  "GET /api/toi/ket-qua": DA_DANG_NHAP,
  "POST /app/nhip": DA_DANG_NHAP,
  "GET /api/bai-giang/{bai_id}/trang/{so}": DA_DANG_NHAP,
  "GET /hoc-sinh": DA_DANG_NHAP,
  "GET /may": DA_DANG_NHAP,
  "GET /may/{ma}": DA_DANG_NHAP,
  "GET /the-thong-tin/{phan_ung_id}": DA_DANG_NHAP,
  "POST /hoi": DA_DANG_NHAP,
  "GET /lab/sinh/huong-dan": DA_DANG_NHAP,
  "GET /lab/sinh/huong-dan/{ma}": DA_DANG_NHAP,
  "GET /lab/sinh/bai-thi-nghiem": DA_DANG_NHAP,
  "GET /lab/sinh/dat-lich": DA_DANG_NHAP,
};
const TRANG = new Set(["/giao-vien", "/trinh-chieu", "/truc-tiep", "/buoi-hoc", "/hoa", "/sinh", "/hoc-sinh"]);
const KHI_PHAI_DOI = new Set(["/dang-nhap", "/quan-ly", "/auth/toi", "/auth/doi-mat-khau", "/auth/dang-xuat"]);

function maBearer(req: Request): string | null {
  const h = req.headers.get("authorization") || "";
  return h.slice(0, 7).toLowerCase() === "bearer " ? h.slice(7).trim() : null;
}

export function docCookie(req: Request, ten: string): string {
  for (const p of (req.headers.get("cookie") || "").split(";")) {
    const i = p.indexOf("=");
    if (i > 0 && p.slice(0, i).trim() === ten) return decodeURIComponent(p.slice(i + 1).trim());
  }
  return "";
}

function saiNguon(req: Request): boolean {
  const origin = req.headers.get("origin");
  if (["GET", "HEAD", "OPTIONS"].includes(req.method) || origin === null || maBearer(req) !== null) return false;
  try {
    return new URL(origin).host.toLowerCase() !== (req.headers.get("host") || "").toLowerCase();
  } catch {
    return true;
  }
}

const chuyen = (toi: string) => new Response(null, { status: 303, headers: { location: toi } });

function biTuChoi(c: Ctx, mau: string | null, ma: number, loi: string, them: Record<string, unknown> = {}): Response {
  if (mau && TRANG.has(mau)) {
    if (ma === 401) return chuyen("/dang-nhap?tiep=" + encodeURIComponent(c.url.pathname));
    return chuyen(mau !== "/hoc-sinh" ? "/hoc-sinh" : "/dang-nhap");
  }
  return json({ detail: loi, ...them }, ma);
}

// Trả Response nếu bị chặn; null là cho qua (c.nd đã gán)
export async function kiemQuyen(c: Ctx, mau: string | null): Promise<Response | null> {
  if (saiNguon(c.req)) return json({ detail: "Yêu cầu đến từ trang khác, đã chặn" }, 403);
  const pm = c.req.method === "HEAD" ? "GET" : c.req.method;
  const muc = mau ? QUYEN[`${pm} ${mau}`] : undefined;
  const bearer = maBearer(c.req);
  c.nd = await tk.docPhien(c.env, bearer !== null ? bearer : docCookie(c.req, COOKIE));
  if (bearer !== null && c.nd === null && muc !== CONG_KHAI) return biTuChoi(c, mau, 401, "Phiên đăng nhập đã hết hạn");
  if (muc === CONG_KHAI) return null;
  if (c.nd === null) return biTuChoi(c, mau, 401, "Cần đăng nhập");
  if (c.nd.phai_doi_mat_khau && !(mau && KHI_PHAI_DOI.has(mau))) {
    if (mau && TRANG.has(mau)) return chuyen("/dang-nhap");
    return json({ detail: "Cần đổi mật khẩu trước khi tiếp tục", phai_doi_mat_khau: true }, 403);
  }
  if (muc === undefined && c.nd.vai_tro !== "giao_vien") return biTuChoi(c, mau, 403, "Chỉ giáo viên được dùng chức năng này");
  return null;
}

// ---------------------------------------------------------------------------
// Điểm cuối
// ---------------------------------------------------------------------------

const baoKhoa = (giay: number) =>
  new HttpLoi(429, `Sai quá nhiều lần, thử lại sau ${Math.max(1, Math.floor((giay + 59) / 60))} phút`, { "Retry-After": String(giay) });

async function moPhien(c: Ctx, nd: NguoiDung, kieu: string) {
  const k = kieu === "ung_dung" ? "ung_dung" : "web";
  const phien = await tk.taoPhien(c.env, nd.id, k);
  const out: Record<string, unknown> = { het_han: phien.het_han, nguoi_dung: await tk.congKhai(c.env, nd) };
  if (k === "ung_dung") out.ma_phien = phien.ma_phien;
  else c.cookie.push(`${COOKIE}=${phien.ma_phien}; Max-Age=${tk.HAN_PHIEN.web}; Path=/; HttpOnly; SameSite=Lax${c.url.protocol === "https:" ? "; Secure" : ""}`);
  return out;
}

tuyen("POST", "/auth/dang-nhap", async (c) => {
  const b = await than(c);
  const ten = tk.chuanTen(chuoi(b, "ten_dang_nhap", { toi_da: 64 }));
  const mat_khau = chuoi(b, "mat_khau", { toi_da: 256 });
  const kieu = chuoi(b, "kieu", { md: "web" });
  const giay = await tk.conBiKhoa(c.env, ten, c.ip);
  if (giay) throw baoKhoa(giay);
  const nd = await tk.xacThuc(c.env, ten, mat_khau);
  if (!nd) {
    await tk.ghiSai(c.env, ten, c.ip);
    throw new HttpLoi(401, "Sai tên đăng nhập hoặc mật khẩu");
  }
  await tk.xoaSai(c.env, ten, c.ip);
  return moPhien(c, nd, kieu);
});

tuyen("POST", "/auth/dang-ky", async (c) => {
  const b = await than(c);
  const ma_lop = chuoi(b, "ma_lop", { toi_da: 16 }), ho_ten = chuoi(b, "ho_ten", { toi_da: 120 });
  const ten = chuoi(b, "ten_dang_nhap", { toi_da: 64 }), mat_khau = chuoi(b, "mat_khau", { toi_da: 256 });
  const kieu = chuoi(b, "kieu", { md: "web" });
  const giay = await tk.conBiKhoa(c.env, "dang-ky", c.ip);
  if (giay) throw baoKhoa(giay);
  const lop = await tk.timLopTheoMa(c.env, ma_lop);
  if (!lop) {
    await tk.ghiSai(c.env, "dang-ky", c.ip, 10); // mã lớp ngắn để đọc được, nên phải chặn dò thử
    throw new HttpLoi(403, "Mã lớp không đúng hoặc lớp đã đóng đăng ký");
  }
  const nd = await tk.taoNguoiDung(c.env, ten, ho_ten, "hoc_sinh", mat_khau);
  await tk.themVaoLop(c.env, lop.id, nd.id);
  return moPhien(c, nd, kieu);
});

tuyen("POST", "/auth/dang-xuat", async (c) => {
  const ma = maBearer(c.req) || docCookie(c.req, COOKIE);
  if (ma) await tk.huyPhien(c.env, ma);
  c.cookie.push(`${COOKIE}=""; Max-Age=0; Path=/; HttpOnly; SameSite=Lax${c.url.protocol === "https:" ? "; Secure" : ""}`);
  return { ok: true };
});

tuyen("GET", "/auth/toi", (c) => tk.congKhai(c.env, c.nd!));

tuyen("POST", "/auth/doi-mat-khau", async (c) => {
  const b = await than(c);
  const cu = chuoi(b, "mat_khau_cu", { toi_da: 256 }), moi = chuoi(b, "mat_khau_moi", { toi_da: 256 });
  const nd = c.nd!, muc = `doi-mat-khau:${nd.id}`;
  const giay = await tk.conBiKhoa(c.env, muc, c.ip);
  if (giay) throw baoKhoa(giay);
  if (!(await tk.dungMatKhauCua(nd, cu))) {
    await tk.ghiSai(c.env, muc, c.ip);
    throw new HttpLoi(400, "Mật khẩu hiện tại không đúng");
  }
  tk.kiemTraMatKhau(moi, nd.ten_dang_nhap);
  if (moi === cu) throw new HttpLoi(422, "Mật khẩu mới phải khác mật khẩu hiện tại");
  await tk.xoaSai(c.env, muc, c.ip);
  await tk.datMatKhau(c.env, nd.id, moi);
  await tk.huyPhienKhac(c.env, nd.id, maBearer(c.req) || docCookie(c.req, COOKIE));
  return tk.congKhai(c.env, (await tk.timTheoId(c.env, nd.id))!);
});

// ---------------------------------------------------------------------------
// Trang: tệp HTML nằm ở /_trang/ trong bản dựng (lấy từ server/web/)
// ---------------------------------------------------------------------------

export async function trang(c: Ctx, ten: string): Promise<Response> {
  const r = await c.env.ASSETS.fetch(new Request(new URL(`/_trang/${ten}`, c.url)));
  if (!r.ok) throw new HttpLoi(404, "Không có trang này");
  return new Response(r.body, { headers: { "content-type": "text/html; charset=utf-8" } });
}

const trangChu = (nd: NguoiDung) => (nd.vai_tro === "giao_vien" ? "/giao-vien" : "/hoc-sinh");

tuyen("GET", "/dang-nhap", (c) => trang(c, "dang_nhap"));
// Nút Đăng nhập của trang giới thiệu: đã đăng nhập thì vào thẳng trang của mình
tuyen("GET", "/quan-ly", (c) => chuyen(c.nd && !c.nd.phai_doi_mat_khau ? trangChu(c.nd) : "/dang-nhap"));
tuyen("GET", "/giao-vien", (c) => trang(c, "giao_vien"));
tuyen("GET", "/trinh-chieu", (c) => trang(c, "trinh_chieu"));
tuyen("GET", "/truc-tiep", (c) => trang(c, "truc_tiep"));
tuyen("GET", "/buoi-hoc", (c) => trang(c, "buoi_hoc"));
tuyen("GET", "/hoa", (c) => trang(c, "index"));
tuyen("GET", "/sinh", (c) => trang(c, "sinh"));
tuyen("GET", "/hoc-sinh", (c) => (c.nd!.vai_tro === "giao_vien" ? chuyen("/giao-vien") : trang(c, "hoc_sinh")));
