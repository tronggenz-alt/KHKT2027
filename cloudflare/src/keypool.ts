// Mạng lưới khóa API: nhiều khóa Groq, DeepSeek, Gemini dùng chung như một (bản Cloudflare của server/lab/keypool.py).
//
// Khóa lưu trong bảng khoa_api của D1, giáo viên nhập trên web; không bao giờ gửi cho ứng dụng hay trình duyệt (web chỉ thấy dạng che).
// Danh sách mô hình và mô hình đang nghỉ chỉ nhớ trong bộ nhớ của máy đang chạy, như bản FastAPI; mất thì hỏi lại nhà cung cấp.

import { Env, bayGio, chay, q, q1 } from "./khung";

export const PROVIDERS: Record<string, any> = {
  groq: { ten: "Groq", base_url: "https://api.groq.com/openai/v1", mo_hinh: "tự chọn mạnh nhất", dau_khoa: "gsk_",
    du_phong: ["openai/gpt-oss-120b", "llama-3.3-70b-versatile", "llama-3.1-8b-instant"] },
  deepseek: { ten: "DeepSeek", base_url: "https://api.deepseek.com/v1", mo_hinh: "tự chọn mạnh nhất", dau_khoa: "sk-", du_phong: ["deepseek-chat"] },
  gemini: { ten: "Gemini", base_url: "https://generativelanguage.googleapis.com/v1beta/openai", mo_hinh: "tự chọn mạnh nhất",
    dau_khoa: "AIza", du_phong: ["gemini-flash-latest", "gemini-flash-lite-latest"] },
};

const TIMEOUT_MS = 30_000;
const DEADLINE_S = 80;
const MODELS_TTL = 6 * 3600;
const MAX_MODELS_PER_KEY = 5;
const THINK_ROOM = 2048;

const models = new Map<string, [number, string[]]>();
const cool = new Map<string, [number, string]>();
const last = new Map<number, string>();
const kc = (khoa: string, m: string) => `${khoa}\u0000${m}`;

export class AllKeysFailed extends Error {}
class BadKey extends Error {}

// ---------------------------------------------------------------------------
// Xếp hạng mô hình
// ---------------------------------------------------------------------------

const NOT_CHAT = /embed|tts|whisper|audio|speech|transcri|guard|moderat|image|imagen|veo|lyria|banana|aqa|(^|-)live(-|$)|native|computer-use|robotics|compound|playai|orpheus|distil|ocr|rerank|research|learnlm|clip|dall/;
const TIER: Record<string, number> = { ultra: 30, pro: 25, opus: 25, max: 15, maverick: 10, large: 10, reasoner: 10, thinking: 5,
  lite: -15, mini: -12, nano: -20, small: -12, tiny: -20, instant: -12, scout: -8, air: -8, preview: -2, exp: -2, experimental: -2 };
const FAMILY: Record<string, number> = { "gpt-oss": 35, kimi: 25, deepseek: 40, gemma: -30, allam: -20, saba: -10 };

function parse(mid: string): [number | null, number | null, number | null, Set<string>] {
  let version: number | null = null, size: number | null = null, experts: number | null = null;
  const words = new Set<string>();
  for (const tok of mid.split(/[-_/:]/)) {
    if (/^\d+(?:\.\d+)?b$/.test(tok)) size = parseFloat(tok);
    else if (/^\d+e$/.test(tok)) experts = parseFloat(tok);
    else if (/^\d+(?:\.\d+)?$/.test(tok)) { if (version === null && parseFloat(tok) < 20) version = parseFloat(tok); }
    else {
      const m = /^[a-z]+(\d+(?:\.\d+)?)$/.exec(tok);
      if (m && version === null && parseFloat(m[1]) < 20) version = parseFloat(m[1]);
      words.add(tok);
    }
  }
  return [version, size, experts, words];
}

export function strength(mid: string, latest = 0): number {
  const m = mid.toLowerCase();
  let [version, size, experts, words] = parse(m);
  if (version === null && words.has("latest")) version = latest;
  let s = 10 * (version || 0);
  for (const [w, v] of Object.entries(TIER)) if (words.has(w)) s += v;
  for (const [f, v] of Object.entries(FAMILY)) if (m.includes(f)) s += v;
  if (size) s += 3 * Math.log2(Math.max(size, 1));
  if (experts) s += 1.5 * Math.log2(Math.max(experts, 1));
  return s;
}

export function rankModels(ids: string[]): string[] {
  const names: string[] = [];
  for (let i of ids) {
    i = i.startsWith("models/") ? i.slice(7) : i;
    if (i && !NOT_CHAT.test(i.toLowerCase()) && !names.includes(i)) names.push(i);
  }
  const latest = Math.max(0, ...names.map((n) => parse(n.toLowerCase())[0] || 0));
  return names.sort((a, b) => strength(b, latest) - strength(a, latest) || (a < b ? -1 : a > b ? 1 : 0));
}

async function goi(url: string, khoa: string, body?: unknown): Promise<Response> {
  return fetch(url, {
    method: body === undefined ? "GET" : "POST",
    headers: { Authorization: `Bearer ${khoa}`, ...(body === undefined ? {} : { "content-type": "application/json" }) },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(TIMEOUT_MS),
  });
}

const badKey = (st: number, t: string) => st === 401 || st === 403 || (st === 400 && t.toLowerCase().includes("api key"));

function modelGone(st: number, t: string): boolean {
  t = t.toLowerCase();
  if (st === 404) return true;
  if (st !== 400 || !t.includes("model")) return false;
  if (["thinking", "reasoning", "budget", "response_format", "json", "schema", "tool"].some((w) => t.includes(w))) return false;
  return ["not found", "does not exist", "not supported", "no longer available", "decommissioned", "deprecated", "not available", "only supports"].some((w) => t.includes(w));
}

async function listModels(row: any): Promise<string[]> {
  const cached = models.get(row.khoa);
  if (cached && bayGio() - cached[0] < MODELS_TTL) return cached[1];
  const p = PROVIDERS[row.nha_cung_cap];
  let ids: string[] = [];
  try {
    const r = await goi(p.base_url + "/models", row.khoa);
    const t = await r.text();
    if (badKey(r.status, t)) throw new BadKey(`${r.status} khóa không hợp lệ hoặc hết quyền`);
    if (r.status === 200) ids = (JSON.parse(t).data || []).filter((m: any) => m && typeof m === "object").map((m: any) => String(m.id ?? ""));
  } catch (e) {
    if (e instanceof BadKey) throw e;
    ids = [];
  }
  const ranked = rankModels(ids);
  const out = ranked.length ? ranked : [...p.du_phong];
  models.set(row.khoa, [ids.length ? bayGio() : bayGio() - MODELS_TTL + 300, out]);
  return out;
}

export const mask = (k: string) => (k.length > 10 ? k.slice(0, 4) + "…" + k.slice(-4) : "…");

function order(row: any, ranked: string[]): string[] {
  const pinned = (row.mo_hinh || "").trim();
  return (pinned ? [pinned] : []).concat(ranked.filter((m) => m !== pinned));
}

function publicRow(r: any) {
  const now = bayGio(), key = r.khoa;
  const thu_tu = order(r, models.get(key)?.[1] ?? []).slice(0, 8).map((m) => {
    const [until, why] = cool.get(kc(key, m)) ?? [0, ""];
    return { ten: m, nghi_giay: Math.max(0, Math.round(until - now)), ly_do: until > now ? why : "" };
  });
  return { ...r, khoa: mask(key), ten_nha_cung_cap: PROVIDERS[r.nha_cung_cap]?.ten ?? r.nha_cung_cap,
    dang_nghi_giay: Math.max(0, Math.round(r.nghi_den - now)), mo_hinh_dang_dung: last.get(r.id) ?? "", thu_tu_mo_hinh: thu_tu };
}

export async function listKeys(env: Env) {
  return (await q(env, "SELECT * FROM khoa_api ORDER BY uu_tien, id")).map(publicRow);
}

export async function addKey(env: Env, nha_cung_cap: string, khoa: string, nhan = "", mo_hinh = "", uu_tien = 10): Promise<number> {
  if (!(nha_cung_cap in PROVIDERS)) throw new Error("Nhà cung cấp phải là: " + Object.keys(PROVIDERS).join(", "));
  khoa = khoa.trim();
  const p = PROVIDERS[nha_cung_cap];
  if (khoa.includes("://") || /\s/.test(khoa)) {
    throw new Error(`Đây là đường link hoặc đoạn chữ, không phải khóa API. Bấm nút sao chép khóa trên trang của ${p.ten} rồi dán lại (khóa ${p.ten} bắt đầu bằng ${p.dau_khoa}…)`);
  }
  if (khoa.length < 20) throw new Error("Khóa API quá ngắn, có thể chưa sao chép hết");
  if (!khoa.startsWith(p.dau_khoa)) throw new Error(`Khóa ${p.ten} phải bắt đầu bằng ${p.dau_khoa}… Kiểm tra lại đã chọn đúng nhà cung cấp chưa`);
  const r = await chay(env, "INSERT INTO khoa_api (nha_cung_cap, nhan, khoa, mo_hinh, uu_tien, tao_luc) VALUES (?,?,?,?,?,?)",
    nha_cung_cap, nhan.trim(), khoa, mo_hinh.trim(), Math.trunc(uu_tien), bayGio());
  return r.id;
}

export async function updateKey(env: Env, id: number, f: { nhan?: string | null; mo_hinh?: string | null; uu_tien?: number | null; bat?: boolean | null }) {
  const sets: Record<string, any> = {};
  for (const k of ["nhan", "mo_hinh", "uu_tien", "bat"] as const) if (f[k] !== null && f[k] !== undefined) sets[k] = f[k];
  if ("bat" in sets) {
    sets.bat = sets.bat ? 1 : 0;
    if (sets.bat) sets.nghi_den = 0;
  }
  if (!Object.keys(sets).length) return;
  await chay(env, `UPDATE khoa_api SET ${Object.keys(sets).map((k) => `${k}=?`).join(", ")} WHERE id=?`, ...Object.values(sets), id);
}

export const deleteKey = (env: Env, id: number) => chay(env, "DELETE FROM khoa_api WHERE id=?", id);

export async function available(env: Env): Promise<boolean> {
  return Boolean(await q1(env, "SELECT 1 FROM khoa_api WHERE bat=1 AND nghi_den<=? LIMIT 1", bayGio()));
}

const candidates = (env: Env) => q<any>(env, "SELECT * FROM khoa_api WHERE bat=1 AND nghi_den<=? ORDER BY uu_tien, dung_luc", bayGio());

async function mark(env: Env, id: number, trang_thai: string, loi = "", nghi = 0, ok = false, tre_ms = 0) {
  const now = bayGio();
  if (ok) await chay(env, "UPDATE khoa_api SET trang_thai=?, loi_gan_nhat='', so_lan_goi=so_lan_goi+1, dung_luc=?, tre_ms=?, nghi_den=0 WHERE id=?", trang_thai, now, tre_ms, id);
  else await chay(env, "UPDATE khoa_api SET trang_thai=?, loi_gan_nhat=?, so_lan_loi=so_lan_loi+1, dung_luc=?, nghi_den=? WHERE id=?", trang_thai, loi.slice(0, 300), now, now + nghi, id);
}

function formats(provider: string, json_mode: boolean, schema: any): (any | null)[] {
  if (!json_mode) return [null];
  if (provider === "gemini") return [...(schema ? [{ type: "json_schema", json_schema: { name: "ket_qua", strict: true, schema } }] : []), null];
  return [{ type: "json_object" }];
}

function reasoning(provider: string, model: string): (string | null)[] {
  const m = model.toLowerCase();
  if (provider === "gemini") return ["none", "minimal", "low", null];
  if (provider === "groq" && m.includes("gpt-oss")) return ["low", null];
  if (provider === "groq" && m.includes("qwen3")) return ["none", null];
  return [null];
}

function thinks(provider: string, model: string, effort: string | null): boolean {
  const m = model.toLowerCase();
  if (effort === "none") return false;
  return provider === "gemini" || m.includes("reasoner") || m.includes("gpt-oss") || m.includes("qwen3") || m.split("-").includes("r1");
}

function setCool(row: any, model: string, seconds: number, why: string) {
  cool.set(kc(row.khoa, model), [bayGio() + seconds, why.split(/\s+/).join(" ").slice(0, 160)]);
}

async function post(row: any, model: string, messages: any[], max_tokens: number, json_mode: boolean, schema: any): Promise<[number, string, Headers]> {
  const p = PROVIDERS[row.nha_cung_cap];
  const fmts = formats(row.nha_cung_cap, json_mode, schema), effs = reasoning(row.nha_cung_cap, model);
  let fi = 0, ri = 0;
  for (;;) {
    const body: any = { model, messages, max_tokens: max_tokens + (thinks(row.nha_cung_cap, model, effs[ri]) ? THINK_ROOM : 0), temperature: 0.3 };
    if (fmts[fi]) body.response_format = fmts[fi];
    if (effs[ri]) body.reasoning_effort = effs[ri];
    const r = await goi(p.base_url + "/chat/completions", row.khoa, body);
    const t = await r.text();
    if (r.status !== 400 || badKey(r.status, t) || modelGone(r.status, t)) return [r.status, t, r.headers];
    const tl = t.toLowerCase();
    if (["thinking", "budget", "reasoning"].some((w) => tl.includes(w)) && ri + 1 < effs.length) ri++;
    else if (fi + 1 < fmts.length) fi++;
    else if (ri + 1 < effs.length) ri++;
    else return [r.status, t, r.headers];
  }
}

async function tryKey(env: Env, row: any, messages: any[], max_tokens: number, json_mode: boolean, schema: any, tried: string[], deadline: number) {
  const p = PROVIDERS[row.nha_cung_cap];
  const tag = `${p.ten}#${row.id}`;
  let ranked: string[];
  try {
    ranked = await listModels(row);
  } catch (e) {
    await mark(env, row.id, "khoa_sai", String((e as Error).message), 3600);
    tried.push(`${tag}: khóa không hợp lệ`);
    return null;
  }
  const now = bayGio();
  const thu = order(row, ranked).filter((m) => (cool.get(kc(row.khoa, m))?.[0] ?? 0) <= now).slice(0, MAX_MODELS_PER_KEY);
  let status = "loi", last_err = "mọi mô hình đang nghỉ";
  for (const model of thu) {
    if (bayGio() > deadline) break;
    const t0 = bayGio();
    let st: number, text: string, hd: Headers;
    try {
      [st, text, hd] = await post(row, model, messages, max_tokens, json_mode, schema);
    } catch (e) {
      if ((e as Error).name === "TimeoutError") {
        setCool(row, model, 600, "trả lời quá lâu");
        tried.push(`${tag} ${model}: quá lâu`);
        status = "loi"; last_err = `${model} trả lời quá lâu`;
        continue;
      }
      await mark(env, row.id, "loi_mang", (e as Error).name, 20);
      tried.push(`${tag}: lỗi mạng`);
      return null;
    }
    if (badKey(st, text)) {
      await mark(env, row.id, "khoa_sai", `${st} khóa không hợp lệ hoặc hết quyền`, 3600);
      tried.push(`${tag}: khóa không hợp lệ`);
      return null;
    }
    if (st === 429) {
      let wait = parseFloat(hd.get("retry-after") || "60") || 60;
      if (text.includes("limit: 0") || text.includes("limit:0")) wait = 6 * 3600;
      setCool(row, model, Math.min(Math.max(wait, 5), 6 * 3600), "429 hết hạn mức");
      tried.push(`${tag} ${model}: 429`);
      status = "gioi_han"; last_err = `429 quá giới hạn (${model})`;
      continue;
    }
    if (modelGone(st, text)) {
      setCool(row, model, 86400, `${st} mô hình không còn`);
      models.set(row.khoa, [0, ranked]);
      tried.push(`${tag} ${model}: không còn`);
      status = "loi"; last_err = `${model} không còn (${st})`;
      continue;
    }
    if (st >= 400) {
      setCool(row, model, st >= 500 ? 30 : 600, `${st} ${text.slice(0, 120)}`);
      tried.push(`${tag} ${model}: lỗi ${st}`);
      status = "loi"; last_err = `${st} ${text.slice(0, 200)}`;
      continue;
    }
    let choice: any, msg: any;
    try {
      choice = JSON.parse(text).choices[0];
      msg = choice.message;
      if (!msg) throw new Error();
    } catch {
      setCool(row, model, 60, "phản hồi không đúng định dạng");
      tried.push(`${tag} ${model}: phản hồi lạ`);
      continue;
    }
    const cut = choice.finish_reason === "length";
    if (cut && !(msg.content || "").trim() && !msg.tool_calls) {
      setCool(row, model, 300, "hết max_tokens khi đang suy nghĩ");
      tried.push(`${tag} ${model}: hết token`);
      continue;
    }
    await mark(env, row.id, "tot", "", 0, true, (bayGio() - t0) * 1000);
    last.set(row.id, model);
    return { message: msg, nha_cung_cap: row.nha_cung_cap, mo_hinh: model, khoa_id: row.id, cat_cut: cut };
  }
  const soonest = Math.min(Infinity, ...order(row, ranked).map((m) => cool.get(kc(row.khoa, m))?.[0] ?? 0));
  await mark(env, row.id, status, last_err, Math.max(5, (Number.isFinite(soonest) ? soonest : 0) - bayGio()));
  if (!thu.length) tried.push(`${tag}: mọi mô hình đang nghỉ`);
  return null;
}

export async function chat(env: Env, messages: any[], max_tokens = 600, json_mode = false, schema: any = null) {
  const tried: string[] = [];
  const rows = await candidates(env);
  if (!rows.length) throw new AllKeysFailed("Không có khóa nào đang dùng được (chưa thêm khóa, hoặc tất cả đang nghỉ)");
  const deadline = bayGio() + DEADLINE_S;
  for (const row of rows) {
    if (bayGio() > deadline) { tried.push("hết thời gian chờ"); break; }
    const res = await tryKey(env, row, messages, max_tokens, json_mode, schema, tried, deadline);
    if (res) return res;
  }
  throw new AllKeysFailed("Mọi khóa đều lỗi: " + tried.join("; "));
}

export async function testKey(env: Env, id: number) {
  const row = await q1<any>(env, "SELECT * FROM khoa_api WHERE id=?", id);
  if (!row) throw new Error("Không có khóa này");
  models.delete(row.khoa);
  for (const k of [...cool.keys()]) if (k.startsWith(row.khoa + "\u0000")) cool.delete(k);
  const tried: string[] = [];
  const t0 = bayGio();
  const res = await tryKey(env, row, [{ role: "user", content: "Trả lời đúng một từ: OK" }], 5, false, null, tried, bayGio() + DEADLINE_S);
  const ranked = models.get(row.khoa)?.[1] ?? [];
  if (res) return { ok: true, tre_ms: Math.round((bayGio() - t0) * 1000), mo_hinh: res.mo_hinh, so_mo_hinh: ranked.length, bo_qua: tried };
  return { ok: false, loi: tried.join("; ") || "không rõ", so_mo_hinh: ranked.length };
}

// ---------------------------------------------------------------------------
// Giao diện nhà cung cấp cho phần AI
// ---------------------------------------------------------------------------

export const NAME = "mang_luoi_khoa";

export async function modelName(env: Env): Promise<string> {
  const rows = await candidates(env);
  if (!rows.length) return "";
  const r = rows[0];
  const m = last.get(r.id) || (r.mo_hinh || "").trim() || models.get(r.khoa)?.[1]?.[0] || "tự chọn mạnh nhất";
  return `${PROVIDERS[r.nha_cung_cap].ten} ${m}`;
}

export async function text(env: Env, system: string, user: string, max_tokens = 400): Promise<string> {
  const res = await chat(env, [{ role: "system", content: system }, { role: "user", content: user }], max_tokens);
  return String(res.message.content || "").trim();
}

export async function structured(env: Env, system: string, user: string, schema: any, max_tokens = 800): Promise<any> {
  const hint = "\n\nTrả lời DUY NHẤT bằng một đối tượng JSON gọn (không xuống dòng thụt lề, không bọc markdown) theo lược đồ: " + JSON.stringify(schema);
  const res = await chat(env, [{ role: "system", content: system + hint }, { role: "user", content: user }], max_tokens, true, schema);
  return loadsLenient(String(res.message.content || ""));
}

export function loadsLenient(text: string): any {
  const start = (text || "").indexOf("{");
  if (start < 0) throw new Error("không có JSON trong câu trả lời");
  const s = text.slice(start);
  const end = s.lastIndexOf("}");
  const whole = end >= 0 ? s.slice(0, end + 1) : s;
  for (const cand of [whole, whole.replace(/,\s*([}\]])/g, "$1")]) {
    try {
      const v = JSON.parse(cand);
      if (v && typeof v === "object" && !Array.isArray(v)) return v;
    } catch { /* thử cách khác */ }
  }
  // Cắt cụt: duyệt ngoài chuỗi, nhớ mỗi chỗ vừa đóng xong một khối và các ngoặc còn mở lúc đó
  const stack: string[] = [], cuts: [number, string][] = [];
  let in_str = false, esc = false;
  for (let i = 0; i < s.length; i++) {
    const ch = s[i];
    if (in_str) {
      if (esc) esc = false;
      else if (ch === "\\") esc = true;
      else if (ch === '"') in_str = false;
    } else if (ch === '"') in_str = true;
    else if (ch === "{" || ch === "[") stack.push(ch === "{" ? "}" : "]");
    else if ((ch === "}" || ch === "]") && stack.length) {
      stack.pop();
      cuts.push([i, [...stack].reverse().join("")]);
    }
  }
  for (const [i, closers] of cuts.reverse()) {
    try {
      const v = JSON.parse(s.slice(0, i + 1) + closers);
      if (v && typeof v === "object" && !Array.isArray(v)) return v;
    } catch { /* thử chỗ cắt trước */ }
  }
  throw new Error("JSON bị cắt cụt, không đọc được");
}
