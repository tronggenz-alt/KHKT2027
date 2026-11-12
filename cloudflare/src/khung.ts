// Khung nhỏ cho Pages Functions: bảng đường dẫn, lỗi HTTP, đọc thân yêu cầu, gọi D1.
// Giữ cùng hợp đồng với bản FastAPI (server/): cùng đường dẫn, cùng mã lỗi, lỗi trả {"detail": ...}.

export interface Env {
  DB: D1Database;
  ASSETS: Fetcher;
  ANTHROPIC_API_KEY?: string;
  CLAUDE_MODEL?: string;
  CLAUDE_EFFORT?: string;
  AI_PROVIDER?: string;
}

export interface NguoiDung {
  id: number;
  ten_dang_nhap: string;
  ho_ten: string;
  vai_tro: "giao_vien" | "hoc_sinh";
  mat_khau_bam: string;
  phai_doi_mat_khau: number;
  tao_luc: number;
}

export interface Ctx {
  req: Request;
  env: Env;
  url: URL;
  params: Record<string, any>;
  nd: NguoiDung | null;
  ip: string;
  cookie: string[]; // Set-Cookie gửi kèm câu trả lời
  _than?: any;
}

export class HttpLoi extends Error {
  constructor(public ma: number, public chi_tiet: any, public dau: Record<string, string> = {}) {
    super(typeof chi_tiet === "string" ? chi_tiet : "loi");
  }
}

export const bayGio = () => Date.now() / 1000;

export function json(data: unknown, status = 200, dau: Record<string, string> = {}): Response {
  return new Response(JSON.stringify(data), { status, headers: { "content-type": "application/json", ...dau } });
}

// ---------------------------------------------------------------------------
// Bảng đường dẫn. Mẫu kiểu FastAPI: "/api/lop/{lop_id:int}"; khóa quyền dùng mẫu đã bỏ ":int".
// ---------------------------------------------------------------------------

export type XuLy = (c: Ctx) => Promise<unknown> | unknown;
interface Tuyen { pm: string; mau: string; re: RegExp; ten: string[]; so: Set<string>; xu: XuLy }
const TUYEN: Tuyen[] = [];

export function tuyen(pm: string, mau: string, xu: XuLy): void {
  const ten: string[] = [], so = new Set<string>();
  const re = mau.replace(/[.]/g, "\\.").replace(/\{(\w+)(:int)?\}/g, (_, t, i) => {
    ten.push(t);
    if (i) so.add(t);
    return "([^/]+)";
  });
  TUYEN.push({ pm, mau: mau.replace(/:int\}/g, "}"), re: new RegExp(`^${re}$`), ten, so, xu });
}

export function timTuyen(pm: string, path: string): { t: Tuyen | null; mau: string | null; params: Record<string, any>; sai_pm: boolean } {
  let sai_pm = false;
  for (const t of TUYEN) {
    const m = t.re.exec(path);
    if (!m) continue;
    if (t.pm !== pm) { sai_pm = true; continue; }
    const params: Record<string, any> = {};
    t.ten.forEach((ten, i) => {
      const v = decodeURIComponent(m[i + 1]);
      params[ten] = t.so.has(ten) ? (/^-?\d+$/.test(v) ? Number(v) : NaN) : v;
    });
    return { t, mau: t.mau, params, sai_pm: false };
  }
  // Mẫu đường dẫn dù sai phương thức, để bảng quyền vẫn tra được
  const cung = TUYEN.find((t) => t.re.test(path));
  return { t: null, mau: cung ? cung.mau : null, params: {}, sai_pm };
}

export function soNguyen(c: Ctx, ten: string): number {
  const v = c.params[ten];
  if (typeof v !== "number" || Number.isNaN(v)) throw new HttpLoi(422, `${ten} phải là số nguyên`);
  return v;
}

// ---------------------------------------------------------------------------
// Đọc thân yêu cầu (thay cho pydantic): sai kiểu trả 422
// ---------------------------------------------------------------------------

export async function than(c: Ctx): Promise<Record<string, any>> {
  if (c._than !== undefined) return c._than;
  let b: any;
  try {
    b = await c.req.json();
  } catch {
    throw new HttpLoi(422, "Thân yêu cầu phải là JSON");
  }
  if (b === null || typeof b !== "object" || Array.isArray(b)) throw new HttpLoi(422, "Thân yêu cầu phải là đối tượng JSON");
  c._than = b;
  return b;
}

interface TuyChon { md?: any; toi_da?: number; nho?: number; lon?: number; rong?: boolean }

const THIEU = Symbol("thieu");

function lay(b: Record<string, any>, ten: string, o: TuyChon): any {
  const v = b[ten];
  if (v === undefined) {
    if (o.md === undefined) throw new HttpLoi(422, `Thiếu trường ${ten}`);
    return THIEU;
  }
  if (v === null) {
    if (o.rong) return null;
    if (o.md === null) return null;
    throw new HttpLoi(422, `Trường ${ten} không được để null`);
  }
  return v;
}

export function chuoi(b: Record<string, any>, ten: string, o: TuyChon = {}): string {
  const v = lay(b, ten, o);
  if (v === THIEU || v === null) return v === null ? (null as any) : o.md;
  if (typeof v !== "string") throw new HttpLoi(422, `Trường ${ten} phải là chuỗi`);
  if (o.toi_da !== undefined && v.length > o.toi_da) throw new HttpLoi(422, `Trường ${ten} dài quá ${o.toi_da} ký tự`);
  return v;
}

function laSo(ten: string, v: any): number {
  if (typeof v === "boolean") return v ? 1 : 0;
  if (typeof v === "string" && v.trim() !== "" && !Number.isNaN(Number(v))) v = Number(v);
  if (typeof v !== "number" || !Number.isFinite(v)) throw new HttpLoi(422, `Trường ${ten} phải là số`);
  return v;
}

function trongKhoang(ten: string, v: number, o: TuyChon): number {
  if (o.nho !== undefined && v < o.nho) throw new HttpLoi(422, `Trường ${ten} phải từ ${o.nho} trở lên`);
  if (o.lon !== undefined && v > o.lon) throw new HttpLoi(422, `Trường ${ten} phải từ ${o.lon} trở xuống`);
  return v;
}

export function so(b: Record<string, any>, ten: string, o: TuyChon = {}): number {
  const v = lay(b, ten, o);
  if (v === THIEU || v === null) return v === null ? (null as any) : o.md;
  return trongKhoang(ten, laSo(ten, v), o);
}

export function soNguyenTruong(b: Record<string, any>, ten: string, o: TuyChon = {}): number {
  const v = lay(b, ten, o);
  if (v === THIEU || v === null) return v === null ? (null as any) : o.md;
  const n = laSo(ten, v);
  if (!Number.isInteger(n)) throw new HttpLoi(422, `Trường ${ten} phải là số nguyên`);
  return trongKhoang(ten, n, o);
}

export function dung(b: Record<string, any>, ten: string, o: TuyChon = {}): boolean {
  const v = lay(b, ten, o);
  if (v === THIEU || v === null) return v === null ? (null as any) : o.md;
  if (typeof v === "boolean") return v;
  if (v === 0 || v === 1) return v === 1;
  if (typeof v === "string" && ["true", "false", "1", "0", "yes", "no", "on", "off"].includes(v.toLowerCase())) return ["true", "1", "yes", "on"].includes(v.toLowerCase());
  throw new HttpLoi(422, `Trường ${ten} phải là true hoặc false`);
}

export function ds<T>(b: Record<string, any>, ten: string, phan: (v: any, i: number) => T, o: TuyChon = {}): T[] {
  const v = lay(b, ten, o);
  if (v === THIEU || v === null) return v === null ? (null as any) : o.md;
  if (!Array.isArray(v)) throw new HttpLoi(422, `Trường ${ten} phải là danh sách`);
  if (o.toi_da !== undefined && v.length > o.toi_da) throw new HttpLoi(422, `Trường ${ten} tối đa ${o.toi_da} mục`);
  return v.map(phan);
}

export const laSoNguyen = (ten: string) => (v: any) => {
  const n = laSo(ten, v);
  if (!Number.isInteger(n)) throw new HttpLoi(422, `${ten} phải là số nguyên`);
  return n;
};
export const laChuoi = (ten: string) => (v: any) => {
  if (typeof v !== "string") throw new HttpLoi(422, `${ten} phải là chuỗi`);
  return v;
};

// Tham số trên địa chỉ (?q=...)
export function thamSo(c: Ctx, ten: string, md = ""): string {
  return c.url.searchParams.get(ten) ?? md;
}
export function thamSoSo(c: Ctx, ten: string, md: number, o: { nguyen?: boolean; nho?: number } = {}): number {
  const v = c.url.searchParams.get(ten);
  if (v === null || v === "") return md;
  const n = Number(v);
  if (!Number.isFinite(n) || (o.nguyen && !Number.isInteger(n))) throw new HttpLoi(422, `${ten} phải là số`);
  if (o.nho !== undefined && n < o.nho) throw new HttpLoi(422, `${ten} phải từ ${o.nho} trở lên`);
  return n;
}
export function thamSoDung(c: Ctx, ten: string): boolean {
  const v = (c.url.searchParams.get(ten) || "").toLowerCase();
  return ["true", "1", "yes", "on"].includes(v);
}

// ---------------------------------------------------------------------------
// D1
// ---------------------------------------------------------------------------

const chuan = (a: any[]) => a.map((v) => (v === undefined ? null : typeof v === "boolean" ? (v ? 1 : 0) : v));

export async function q<T = Record<string, any>>(env: Env, sql: string, ...args: any[]): Promise<T[]> {
  const r = await env.DB.prepare(sql).bind(...chuan(args)).all<T>();
  return r.results as T[];
}

export async function q1<T = Record<string, any>>(env: Env, sql: string, ...args: any[]): Promise<T | null> {
  return (await q<T>(env, sql, ...args))[0] ?? null;
}

export async function chay(env: Env, sql: string, ...args: any[]): Promise<{ id: number; doi: number }> {
  const r = await env.DB.prepare(sql).bind(...chuan(args)).run();
  return { id: Number(r.meta.last_row_id), doi: Number(r.meta.changes) };
}

export function cau(env: Env, sql: string, ...args: any[]): D1PreparedStatement {
  return env.DB.prepare(sql).bind(...chuan(args));
}

export const dauHoi = (n: number) => Array(n).fill("?").join(",");

// Bỏ dấu tiếng Việt, chữ thường (giống fold() bên Python)
export function fold(s: string): string {
  return (s || "").replace(/đ/g, "d").replace(/Đ/g, "D").normalize("NFD").replace(/\p{Mn}/gu, "").toLowerCase();
}

// Chuỗi ngẫu nhiên an toàn
export function ngauNhien(bang: string, dai: number): string {
  const out: string[] = [];
  const gioi_han = 256 - (256 % bang.length);
  while (out.length < dai) {
    for (const b of crypto.getRandomValues(new Uint8Array(dai * 2))) {
      if (b < gioi_han && out.length < dai) out.push(bang[b % bang.length]);
    }
  }
  return out.join("");
}
