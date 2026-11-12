// Trợ lý AI cho app (bản Cloudflare của server/ai/main.py, machines.py, providers/).
// AI chỉ DIỄN GIẢI, trò chuyện và lập kế hoạch cho robot; bộ máy hóa học trong Unity quyết định phản ứng. Khóa API chỉ nằm ở máy chủ:
// mạng lưới khóa trong D1 (nhập trên web), khóa Claude trong secret ANTHROPIC_API_KEY của Cloudflare.

import { CHEM, CHEM_BY_STT, DUNG_CU_SINH, MACHINES, REACTIONS } from "./du_lieu";
import { Env, HttpLoi, chuoi, ds, laChuoi, so, than, tuyen } from "./khung";
import * as keypool from "./keypool";
import { ghiNhatKy } from "./kho_hoa";
import { LOI_AI_TAT, aiBiTat, hoaChatBiKhoa } from "./phong_hoc";
import { PLAN_SCHEMA, SYSTEM_PLAN, formatScene, validate } from "./robot_plan";

const SYSTEM_QA = "Bạn là trợ giảng hóa học cho học sinh THPT Việt Nam trong phòng thí nghiệm ảo. " +
  "Chỉ giải thích dựa trên NGỮ CẢNH được cung cấp (đó là kết quả mô phỏng đã kiểm chứng); " +
  "không tự đặt ra phản ứng hay số liệu mới. Nếu ngữ cảnh không đủ, nói rõ là chưa đủ thông tin. " +
  "Trả lời ngắn gọn (tối đa 4 câu), dễ đọc thành tiếng, dùng số liệu của chính học sinh khi có.";

const SYSTEM_ANNOTATE = "Bạn là trợ giảng hóa học THPT Việt Nam. Phòng thí nghiệm ảo vừa ghi nhận một hiện tượng; dữ kiện (tên hiện tượng, " +
  "phương trình, thành phần dung dịch, pH, nhiệt độ) do bộ máy mô phỏng tính, coi là đúng. " +
  "Viết 1-2 câu tiếng Việt (tối đa 45 từ) giải thích BẢN CHẤT vì sao có hiện tượng này trong đúng bình đó, " +
  "theo kiến thức sách giáo khoa; thêm một lưu ý an toàn ngắn nếu chất độc, ăn mòn hoặc dễ cháy. " +
  "Không lặp lại nguyên văn tiêu đề, không bịa thêm phản ứng, không dùng định dạng markdown.";

const ACTIONS = ["tim_hoa_chat", "mang_hoa_chat", "don_ban", "bat_tu_hut", "tat_lua", "rua_mat", "mo_tra_cuu"];

const SYSTEM_ASSIST = "Bạn là robot trợ lý hình người trong phòng thí nghiệm hóa học ảo của một trường THPT Việt Nam (300 m², phòng hóa chất 720 chất " +
  "xếp theo nhóm trên 27 kệ). Bạn trò chuyện bằng tiếng Việt, thân thiện, ngắn gọn (tối đa 4 câu), " +
  "dựa trên NGỮ CẢNH thí nghiệm đi kèm (do mô phỏng tính, coi là đúng). Bạn hướng dẫn thao tác đúng, giải thích hiện tượng, " +
  "nhắc an toàn, và có thể tự làm các hành động sau cho học sinh:\n" +
  "- tim_hoa_chat: đi tới kệ chỉ vị trí một hóa chất (ghi tên hoặc công thức vào hoa_chat)\n" +
  "- mang_hoa_chat: lấy lọ hóa chất trong kho mang ra bàn học sinh đang đứng (ghi hoa_chat)\n" +
  "- don_ban: đổ dung dịch thừa, trả dụng cụ về chỗ, lau chỗ tràn\n" +
  "- bat_tu_hut: bật quạt tủ hút và quạt hút (khi có khí độc)\n" +
  "- tat_lua: tắt mọi đèn cồn, đèn Bunsen, bếp\n" +
  "- rua_mat: dẫn tới vòi rửa mắt khẩn cấp\n" +
  "- mo_tra_cuu: mở bảng tra cứu hóa chất\n" +
  "Chỉ thêm hành động khi học sinh yêu cầu hoặc khi tình huống nguy hiểm cần xử lý ngay. " +
  "Giữ đúng phạm vi thí nghiệm trường phổ thông: không hướng dẫn điều chế chất nổ, chất độc hay vũ khí; " +
  "nếu được hỏi những điều đó, từ chối nhẹ nhàng và gợi ý thí nghiệm an toàn tương ứng. " +
  "Khi có QUY TRÌNH MÁY đi kèm (tài liệu vận hành của phòng lab), hướng dẫn đúng thứ tự các bước và nhắc lỗi hay gặp, an toàn của máy đó; " +
  "không tự đặt thêm bước hay thông số. Không dùng markdown.";

const ASSIST_SCHEMA = {
  type: "object",
  properties: {
    tra_loi: { type: "string" },
    hanh_dong: { type: "array", items: { type: "object", properties: { loai: { type: "string", enum: ACTIONS }, hoa_chat: { type: "string" } },
      required: ["loai", "hoa_chat"], additionalProperties: false } },
  },
  required: ["tra_loi", "hanh_dong"],
  additionalProperties: false,
};

// ---------------------------------------------------------------------------
// Nhà cung cấp: mạng lưới khóa trước, rồi Claude
// ---------------------------------------------------------------------------

interface NhaCungCap { NAME: string; ten_mo_hinh: () => Promise<string>; text: (s: string, u: string, m: number) => Promise<string>; structured: (s: string, u: string, sc: any, m: number) => Promise<any> }

function claude(env: Env): NhaCungCap {
  const mo_hinh = env.CLAUDE_MODEL || "claude-opus-5-5";
  const tao = async (system: string, user: string, max_tokens: number, format: any = null) => {
    const output_config: any = { effort: env.CLAUDE_EFFORT || "low" };
    if (format) output_config.format = format;
    const r = await fetch("https://api.anthropic.com/v1/messages", {
      method: "POST",
      headers: { "x-api-key": env.ANTHROPIC_API_KEY!, "anthropic-version": "2023-06-01", "anthropic-beta": "server-side-fallback-2026-07-01", "content-type": "application/json" },
      body: JSON.stringify({ model: mo_hinh, max_tokens, system: [{ type: "text", text: system, cache_control: { type: "ephemeral" } }],
        messages: [{ role: "user", content: user }], output_config, fallbacks: "default" }),
      signal: AbortSignal.timeout(30_000),
    });
    if (!r.ok) throw new Error(`Claude ${r.status}`);
    const d: any = await r.json();
    if (d.stop_reason === "refusal") throw new Error("Mô hình từ chối trả lời yêu cầu này");
    return d;
  };
  const chu = (d: any) => (d.content || []).filter((b: any) => b.type === "text").map((b: any) => b.text).join("").trim();
  return {
    NAME: "claude",
    ten_mo_hinh: async () => mo_hinh,
    text: async (s, u, m) => chu(await tao(s, u, m)),
    structured: async (s, u, sc, m) => {
      const d = await tao(s, u, m, { type: "json_schema", schema: sc });
      if (d.stop_reason === "max_tokens") throw new Error("Câu trả lời bị cắt (max_tokens)");
      return JSON.parse(chu(d));
    },
  };
}

function mangLuoi(env: Env): NhaCungCap {
  return { NAME: keypool.NAME, ten_mo_hinh: () => keypool.modelName(env),
    text: (s, u, m) => keypool.text(env, s, u, m), structured: (s, u, sc, m) => keypool.structured(env, s, u, sc, m) };
}

// Gọi lần lượt các nhà cung cấp đang dùng được; NAME là nhà vừa trả lời
class Chuoi {
  NAME: string;
  constructor(private ds: NhaCungCap[]) { this.NAME = ds[0].NAME; }
  ten_mo_hinh() { return this.ds[0].ten_mo_hinh(); }
  text(s: string, u: string, m = 400): Promise<string> { return this.thu((p) => p.text(s, u, m)); }
  structured(s: string, u: string, sc: any, m = 800): Promise<any> { return this.thu((p) => p.structured(s, u, sc, m)); }
  private async thu<T>(f: (p: NhaCungCap) => Promise<T>): Promise<T> {
    let loi: unknown = null;
    for (const p of this.ds) {
      try {
        const out = await f(p);
        this.NAME = p.NAME;
        return out;
      } catch (e) {
        loi = e;
      }
    }
    throw loi ?? new Error("không có nhà cung cấp");
  }
}

async function chon(env: Env): Promise<Chuoi | null> {
  const muon = (env.AI_PROVIDER || "").toLowerCase().replace("anthropic", "claude");
  const co_luoi = await keypool.available(env), co_claude = Boolean(env.ANTHROPIC_API_KEY);
  if (muon === "mang_luoi_khoa") return co_luoi ? new Chuoi([mangLuoi(env)]) : null;
  if (muon === "claude") return co_claude ? new Chuoi([claude(env)]) : null;
  const ds_ncc = [...(co_luoi ? [mangLuoi(env)] : []), ...(co_claude ? [claude(env)] : [])];
  return ds_ncc.length ? new Chuoi(ds_ncc) : null;
}

const loiAi = (e: unknown) => (e instanceof keypool.AllKeysFailed
  ? `Mình chưa kết nối được AI: ${e.message.slice(0, 240)}. Xem trang Mạng lưới khóa AI trên web quản lý.`
  : `Mình chưa kết nối được AI (${(e as Error)?.name || "Error"}).`);

// ---------------------------------------------------------------------------
// Tra hóa chất theo tên / công thức
// ---------------------------------------------------------------------------

const SO_NHO = "₀₁₂₃₄₅₆₇₈₉";
const doiSoNho = (s: string) => s.replace(/[₀-₉]/g, (ch) => String(SO_NHO.indexOf(ch)));
const foldHoa = (s: string) => doiSoNho((s || "").replace(/đ/g, "d").replace(/Đ/g, "D").normalize("NFD").replace(/\p{Mn}/gu, "").toLowerCase()).trim();

const EN_VI: Record<string, string> = {
  acid: "axit", sodium: "natri", potassium: "kali", copper: "dong", iron: "sat", silver: "bac", barium: "bari",
  calcium: "canxi", magnesium: "magie", zinc: "kem", aluminium: "nhom", aluminum: "nhom", ammonium: "amoni",
  lead: "chi", manganese: "mangan", chromium: "crom", nickel: "niken", cobalt: "coban", lithium: "liti",
  hydroxide: "hidroxit", chloride: "clorua", sulfate: "sunfat", sulphate: "sunfat", sulfite: "sunfit",
  sulfide: "sunfua", nitrate: "nitrat", nitrite: "nitrit", carbonate: "cacbonat", bicarbonate: "hidrocacbonat",
  permanganate: "pemanganat", dichromate: "dicromat", chromate: "cromat", oxide: "oxit", peroxide: "peoxit",
  iodide: "iodua", bromide: "bromua", fluoride: "florua", phosphate: "photphat", acetate: "axetat",
  oxalate: "oxalat", thiocyanate: "thioxianat", thiosulfate: "thiosunfat", hydrogen: "hidro", water: "nuoc",
  sulfuric: "sunfuric", sulphuric: "sunfuric", hydrochloric: "clohidric", phosphoric: "photphoric",
  acetic: "axetic", formic: "fomic", carbonic: "cacbonic", glucose: "glucozo", fructose: "fructozo",
  sucrose: "saccarozo", starch: "tinh bot", ethanol: "etanol", methanol: "metanol", glycerol: "glixerol",
  phenolphthalein: "phenolphtalein", ammonia: "amoniac", iodine: "iot", chlorine: "clo", bromine: "brom",
  litmus: "quy", indicator: "chi thi", hidroxyt: "hidroxit", hydroxit: "hidroxit", sulfat: "sunfat",
  clohydric: "clohidric", "hiđroxit": "hidroxit",
};
const STOP = new Set(["dung", "dich", "dd", "solution", "loang", "dilute", "aqueous", "lo", "chai", "ong", "cua", "va", "the"]);

function tuCua(text: string): string[] {
  const out: string[] = [];
  for (const w of foldHoa(text).match(/[a-z]+/g) || []) out.push(...(EN_VI[w] ?? w).split(" "));
  return out.filter((w) => !STOP.has(w));
}

export function formulaKey(f: string): string {
  f = doiSoNho(f || "").split(/[.·*]/)[0].trim().replace(/\^?\d*[+\-]+$/, "");
  if (!/^(?:[A-Z][a-z]?\d*|[()\[\]]\d*)+$/.test(f || "-")) return "";
  const stack: Record<string, number>[] = [{}];
  for (const m of f.matchAll(/([A-Z][a-z]?)(\d*)|([()\[\]])(\d*)/g)) {
    const [, el, n, br, bn] = m;
    if (el) stack[stack.length - 1][el] = (stack[stack.length - 1][el] || 0) + Number(n || 1);
    else if (br === "(" || br === "[") stack.push({});
    else if (stack.length > 1) {
      const inner = stack.pop()!;
      for (const [k, v] of Object.entries(inner)) stack[stack.length - 1][k] = (stack[stack.length - 1][k] || 0) + v * Number(bn || 1);
    }
  }
  const counts = stack[0];
  const thu_tu = Object.keys(counts).sort((a, b) => {
    const ka = [a !== "C", a !== "H", a], kb = [b !== "C", b !== "H", b];
    for (let i = 0; i < 3; i++) if (ka[i] !== kb[i]) return ka[i] < kb[i] ? -1 : 1;
    return 0;
  });
  return thu_tu.map((e) => e + (counts[e] > 1 ? counts[e] : "")).join("");
}

let khoaChat: { c: any; name: string; formula: string; keys: Set<string>; words: Set<string> }[] | null = null;

function danhMucTra() {
  if (!khoaChat) {
    khoaChat = CHEM.hoa_chat.map((c: any) => {
      const name = c.ten_khong_dau || foldHoa(c.ten);
      return { c, name, formula: (c.cong_thuc || "").toLowerCase(),
        keys: new Set([formulaKey(c.cong_thuc || ""), ...(c.ten.match(/[A-Z][A-Za-z0-9()]*/g) || []).map(formulaKey)]), words: new Set(tuCua(name)) };
    });
  }
  return khoaChat!;
}

export function findChemical(query: string): any {
  const qf = foldHoa(query);
  if (!qf) return null;
  const qkey = formulaKey((query || "").trim());
  const qwords = new Set(tuCua(query));
  const want_solid = (qf.match(/[a-z]+/g) || []).some((w) => ["ran", "tinh", "bot", "solid", "crystal", "powder"].includes(w));
  let best: any = null, best_score = 0;
  for (const { c, name, formula, keys, words } of danhMucTra()) {
    let score = 0;
    const cung_tu = qwords.size > 0 && qwords.size === words.size && [...qwords].every((w) => words.has(w));
    if (formula && formula === qf) score = 900;
    else if (qkey && qkey.length > 2 && keys.has(qkey)) score = 880;
    else if (name === qf) score = 850;
    else if (cung_tu) score = 820;
    else if (name.startsWith(qf)) score = 700;
    else if (qwords.size && [...qwords].every((w) => words.has(w))) score = 600;
    else if (name.includes(qf) || (formula && formula.includes(qf) && qf.length >= 3)) score = 400;
    const prefer = want_solid ? c.trang_thai === "ran" : c.trang_thai === "dd";
    score -= (prefer ? 0 : 3) + name.length / 100 + c.stt / 10000;
    if (score > best_score) { best = c; best_score = score; }
  }
  return best_score > 0 ? best : null;
}

// ---------------------------------------------------------------------------
// Quy trình máy (SOP) đưa vào ngữ cảnh
// ---------------------------------------------------------------------------

const ALIASES: Record<string, string[]> = {
  uv_vis: ["uv-vis", "uv vis", "uvvis", "quang pho hap thu phan tu", "quang pho kha kien", "tu ngoai kha kien", "lambert", "beer"],
  aas: ["aas", "hap thu nguyen tu", "ngon lua axetilen"],
  ftir: ["ftir", "hong ngoai", "atr", "giao thoa"],
  gc_ms: ["gc-ms", "gc ms", "gcms", "sac ky khi", "khoi pho"],
  hplc: ["hplc", "sac ky long"],
  chuan_do_dien_the: ["chuan do dien the", "chuan do tu dong", "dien cuc ph", "titrator"],
  xrd: ["xrd", "nhieu xa tia x", "nhieu xa"],
  sem: ["sem", "hien vi dien tu", "eds"],
  nmr: ["nmr", "cong huong tu"],
  nhiet_luong: ["bom nhiet luong", "nhiet luong", "nhiet dot chay"],
  tu_say: ["tu say", "do am", "chat kho"],
  lo_nung: ["lo nung", "tro tong so", "nung voi"],
  may_cat_nuoc: ["may cat nuoc", "nuoc cat", "do dan dien"],
  noi_hap: ["noi hap", "tiet trung", "autoclave", "noi hap tiet trung", "hap tiet trung"],
  kinh_hien_vi: ["kinh hien vi", "microscope", "vat kinh", "thi kinh", "tieu ban", "soi tieu ban", "do phong dai", "soi kinh", "oc vi cap", "oc so cap"],
  tu_an_toan_sinh_hoc: ["tu an toan sinh hoc", "tu thao tac vo trung", "biosafety cabinet", "bsc", "tu bsc", "dong khi laminar", "tu vo trung", "loc hepa"],
  may_pcr: ["may pcr", "luan nhiet", "thermal cycler", "khuech dai gen", "chu ky nhiet", "pcr", "bien tinh", "gan moi"],
  be_dien_di: ["dien di", "be dien di", "dien di gel", "electrophoresis", "agarose", "ban gel", "tae"],
  tu_am_vi_sinh: ["tu am", "tu am vi sinh", "incubator", "u vi khuan", "u vi sinh", "dia petri", "37 do"],
  gel_doc: ["gel doc", "chup gel", "buong chup gel", "buong toi uv", "gel documentation", "anh gel", "soi gel", "doc gel", "transilluminator"],
  may_do_od600: ["od600", "od 600", "do quang", "mat do quang", "may do od600", "may quang pho od600", "so mau vi sinh", "spectrophotometer 600", "duong cong sinh truong"],
  micropipet_p200: ["micropipet", "micropipette", "pipet tu dong", "hut mau", "nac 1", "nac 2", "p200"],
  micropipet_p10: ["micropipet p10", "pipet p10", "p10"],
  micropipet_p1000: ["micropipet p1000", "pipet p1000", "p1000"],
  hop_dau_con: ["dau con", "hop dau con", "tip pipet", "thay dau con"],
  que_cay_vong: ["que cay", "que cay vong", "cay ria", "dot que cay", "vong cay", "inoculating loop"],
  que_cay_thang: ["que cay thang", "cay dam", "stab"],
  que_trang: ["que trang", "trang dia", "cell spreader"],
  dao_nhuom_gram: ["nhuom gram", "dao nhuom gram", "gram stain", "tim ket tinh", "crystal violet", "safranin", "tay con", "gram duong", "gram am"],
  may_ly_tam_de_ban: ["ly tam", "may ly tam", "centrifuge", "rcf", "can bang rotor", "ong doi dien", "ly tam de ban"],
  may_ly_tam_mini: ["ly tam mini", "microcentrifuge", "spin down"],
  may_vortex: ["vortex", "may vortex", "may lac vortex", "lac vortex", "tron vortex"],
  may_dem_khuan_lac: ["dem khuan lac", "may dem khuan lac", "colony counter", "cfu"],
  coi_chay_su: ["coi chay", "coi chay su", "tach chiet adn", "nghien mau", "ket tua adn"],
  tram_potometer: ["potometer", "tram potometer", "thoat hoi nuoc", "do thoat hoi nuoc", "bot khi mao quan"],
  tram_quang_hop: ["tram quang hop", "rong duoi cho", "elodea", "dem bot khi quang hop", "que dom"],
  hop_huong_sang: ["huong sang", "hop huong sang", "phototropism", "moc vong"],
  gian_thuy_canh_nft: ["thuy canh", "gian thuy canh", "nft"],
  binh_len_men: ["binh len men", "len men ruou", "bong bay co2", "nam men"],
  bo_lam_sua_chua: ["lam sua chua", "len men lactic", "hu lam sua chua"],
  ban_giai_phau_so: ["ban giai phau", "ban giai phau so", "giai phau 3d"],
  tu_thao_tac_pcr: ["tu thao tac pcr", "tu pcr", "pcr hood"],
  bo_xu_ly_tran: ["bo xu ly tran", "tran do sinh hoc", "spill kit", "vo dia petri"],
  lo_dau_soi: ["dau soi", "dau soi kinh", "vat kinh 100x", "vat kinh dau"],
};

const UNSAFE_BIO = ["bsl 2", "bsl 3", "bsl 4", "bsl2", "bsl3", "bsl4", "cap an toan 2", "cap an toan 3", "cap an toan 4",
  "tang doc luc", "tang cuong doc luc", "bien doi doc luc", "doc to sinh hoc", "vu khi sinh hoc", "tao mam benh",
  "tao virus", "nuoi virus", "cay virus", "khang khang sinh", "tao chung khang thuoc",
  "tac nhan gay benh", "vi khuan gay benh", "virus gay benh", "nam gay benh",
  "nuoi cay mam benh", "phan lap tu benh pham", "phan lap tu mau", "phan lap tu dom", "phan lap tu mu",
  "bacillus anthracis", "vi khuan than", "benh than", "anthrax", "vibrio cholerae", "vi khuan ta", "phay khuan ta",
  "mycobacterium tuberculosis", "vi khuan lao", "truc khuan lao", "yersinia pestis", "dich hach", "salmonella", "thuong han",
  "staphylococcus aureus", "tu cau vang", "mrsa", "clostridium botulinum", "doc to botulinum", "clostridium tetani", "uon van",
  "shigella", "truc khuan ly", "klebsiella pneumoniae", "pseudomonas aeruginosa",
  "ebola", "marburg", "sars cov", "mers cov", "h5n1", "cum gia cam", "virus dai", "dau mua"];

const BSL1_REFUSAL = "Phòng thí nghiệm trường học chỉ thực hiện các thí nghiệm an toàn sinh học cấp 1 (BSL-1); " +
  "mình không thể hướng dẫn nuôi cấy, phân lập từ bệnh phẩm, tạo mới hay làm tăng độc lực các tác nhân gây bệnh nguy hiểm (BSL-2/3/4). " +
  "Bạn có thể thực hành kỹ thuật vi sinh và sinh học phân tử tương đương trên các chủng dạy học an toàn BSL-1 như: " +
  "cấy ria hoặc đo OD600 với E. coli K-12, nhuộm Gram vi khuẩn sữa chua (Lactobacillus bulgaricus, Streptococcus thermophilus), " +
  "quan sát Bacillus subtilis hoặc lên men nấm men bánh mì (Saccharomyces cerevisiae).";
const BSL1_CONTEXT = "AN TOÀN SINH HỌC (BẮT BUỘC - CHỈ BSL-1):\n" + BSL1_REFUSAL + "\n" +
  "Hãy từ chối nhẹ nhàng yêu cầu nguy hiểm này và gợi ý học sinh thực hiện các bài thí nghiệm BSL-1 an toàn ở trên.";

const strip = (s: string) => (s || "").toLowerCase().normalize("NFD").replace(/đ/g, "d").replace(/\p{Mn}/gu, "").replace(/[^a-z0-9]+/g, " ").trim();

function timMay(text: string, limit = 2): any[] {
  const t = " " + strip(text) + " ";
  const hits: [number, string][] = [];
  for (const [ma, words] of Object.entries(ALIASES)) {
    const d = MACHINES.get(ma);
    if (!d) continue;
    const keys = [...words.map(strip), strip(d.ten), ma.replace(/_/g, " ")];
    const best = Math.max(0, ...keys.filter((k) => k && t.includes(` ${k} `)).map((k) => k.length));
    if (best) hits.push([best, ma]);
  }
  return hits.sort((a, b) => b[0] - a[0] || (b[1] > a[1] ? 1 : -1)).slice(0, limit).map(([, ma]) => MACHINES.get(ma));
}

function timDungCu(text: string, limit = 2): any[] {
  const t = " " + strip(text) + " ";
  const hits: [number, any][] = [];
  for (const d of DUNG_CU_SINH) {
    if (!d.ma) continue;
    const may = d.may_man_hinh || "";
    const words = [...(ALIASES[d.ma] || []), ...(may && may !== d.ma ? ALIASES[may] || [] : [])];
    const keys = [...words.map(strip), strip(d.ten || ""), strip(d.ten_tieng_anh || ""), d.ma.replace(/_/g, " ")];
    const best = Math.max(0, ...keys.filter((k) => k && k.length >= 3 && t.includes(` ${k} `)).map((k) => k.length));
    if (best) hits.push([best, d]);
  }
  return hits.sort((a, b) => b[0] - a[0] || (b[1].ma > a[1].ma ? 1 : -1)).slice(0, limit).map(([, d]) => d);
}

function tomTatMay(d: any): string {
  const lines = [`MÁY: ${d.ten} (thông số theo ${d.may_mau.split("(")[0].trim()})`, "Nguyên lý: " + d.nguyen_ly.slice(0, 3).join(" ")];
  lines.push("Quy trình:\n" + d.quy_trinh.map((s: any, i: number) => `${i + 1}. ${s.ten}: ${s.viec}${s.loi ? ` (lỗi hay gặp: ${s.loi})` : ""}`).join("\n"));
  lines.push("An toàn: " + d.an_toan.join(" "));
  return lines.join("\n");
}

function tomTatDungCu(d: any): string {
  const lines = [`DỤNG CỤ: ${d.ten} (${d.phan_loai || ""})`];
  if (d.cong_dung) lines.push(`Công dụng: ${d.cong_dung}`);
  if (d.cach_dung?.length) lines.push("Cách dùng:\n" + d.cach_dung.slice(0, 6).map((s: string) => `- ${s}`).join("\n"));
  if (d.loi_thuong_gap?.length) lines.push("Lỗi thường gặp: " + d.loi_thuong_gap.slice(0, 4).join("; "));
  if (d.an_toan?.length) lines.push("An toàn: " + d.an_toan.slice(0, 4).join("; "));
  return lines.join("\n").slice(0, 1500);
}

function ngCanhMay(...texts: string[]): string {
  const all = texts.filter(Boolean).join(" ");
  const t = " " + strip(all) + " ";
  const parts: string[] = [];
  if (UNSAFE_BIO.some((kw) => t.includes(` ${strip(kw)} `))) parts.push(BSL1_CONTEXT);
  const docs = timMay(all), tools = timDungCu(all);
  if (docs.length) parts.push(docs.map(tomTatMay).join("\n\n"));
  if (tools.length) parts.push(tools.map(tomTatDungCu).join("\n\n"));
  return parts.join("\n\n");
}

// ---------------------------------------------------------------------------
// Điểm cuối
// ---------------------------------------------------------------------------

const boNhoDem = new Map<string, string>();

async function bam(s: string): Promise<string> {
  const d = new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(s)));
  return [...d].map((b) => b.toString(16).padStart(2, "0")).join("");
}

tuyen("GET", "/suc-khoe", async (c) => {
  const p = await chon(c.env);
  if (!c.nd) return { ok: p !== null, nha_cung_cap: "", mo_hinh: "" }; // người ngoài không thấy tên nhà cung cấp AI
  return { ok: p !== null, nha_cung_cap: p ? p.NAME : "khong", mo_hinh: p ? await p.ten_mo_hinh() : "" };
});

tuyen("GET", "/may", () => [...MACHINES.values()].map((d) => ({ ma: d.ma, ten: d.ten, may_mau: d.may_mau, so_buoc: d.quy_trinh.length })));

tuyen("GET", "/may/{ma}", (c) => {
  const d = MACHINES.get(c.params.ma);
  if (!d) throw new HttpLoi(404, "Không có máy này");
  return d;
});

tuyen("GET", "/the-thong-tin/{phan_ung_id}", (c) => {
  const r = REACTIONS.get(c.params.phan_ung_id);
  return { the_thong_tin: r ? r.the_thong_tin : null, an_toan: r ? r.an_toan : [] };
});

tuyen("POST", "/hoi", async (c) => {
  const b = await than(c);
  const cau_hoi = chuoi(b, "cau_hoi");
  const ngu_canh = b.ngu_canh;
  if (!ngu_canh || typeof ngu_canh !== "object" || Array.isArray(ngu_canh)) throw new HttpLoi(422, "Thiếu trường ngu_canh");
  if (await aiBiTat(c.env, c.nd)) return { tra_loi: LOI_AI_TAT, nguon: "khoa" };
  const key = await bam(JSON.stringify({ cau_hoi, ngu_canh }));
  if (boNhoDem.has(key)) return { tra_loi: boNhoDem.get(key), nguon: "cache" };
  const reaction = REACTIONS.get(String(ngu_canh.phan_ung_id || ""));
  const card = reaction ? reaction.the_thong_tin : "Chưa có thẻ thông tin cho thí nghiệm này.";
  const p = await chon(c.env);
  if (!p) return { tra_loi: card, nguon: "the_thong_tin" };
  const context: any = Object.fromEntries(Object.entries(ngu_canh).filter(([, v]) => v !== null && v !== undefined));
  if (reaction) Object.assign(context, { phuong_trinh: reaction.phuong_trinh, the_thong_tin: reaction.the_thong_tin });
  let text: string;
  try {
    text = await p.text(SYSTEM_QA, `NGỮ CẢNH: ${JSON.stringify(context)}\n\nCÂU HỎI: ${cau_hoi}`, 300);
  } catch {
    return { tra_loi: card, nguon: "the_thong_tin" }; // AI lỗi/chậm: vẫn có thẻ soạn sẵn
  }
  boNhoDem.set(key, text);
  return { tra_loi: text, nguon: "llm" };
});

tuyen("POST", "/chu-thich", async (c) => {
  const b = await than(c);
  const fact: any = { tieu_de: chuoi(b, "tieu_de"), phuong_trinh: chuoi(b, "phuong_trinh", { md: "" }), chi_tiet: chuoi(b, "chi_tiet", { md: "" }),
    muc_do: chuoi(b, "muc_do", { md: "Info" }), loai: chuoi(b, "loai", { md: "" }), thanh_phan: ds(b, "thanh_phan", laChuoi("thanh_phan"), { md: [] }),
    ph: so(b, "ph", { md: null }), nhiet_do: so(b, "nhiet_do", { md: null }), ngu_canh: chuoi(b, "ngu_canh", { md: "" }) };
  for (const k of ["ph", "nhiet_do"]) if (fact[k] === null) delete fact[k];
  if (await aiBiTat(c.env, c.nd)) return { giai_thich: "", nguon: "khoa" }; // giải thích hiện tượng cũng là gợi ý
  const key = await bam(JSON.stringify([fact.tieu_de, fact.phuong_trinh, [...fact.thanh_phan].sort()]));
  if (boNhoDem.has(key)) return { giai_thich: boNhoDem.get(key), nguon: "cache" };
  const p = await chon(c.env);
  if (!p) return { giai_thich: fact.chi_tiet, nguon: "the_thong_tin" };
  let text: string;
  try {
    text = await p.text(SYSTEM_ANNOTATE, "DỮ KIỆN: " + JSON.stringify(fact), 300);
  } catch {
    return { giai_thich: "", nguon: "loi" };
  }
  text = text.replace(/\s+/g, " ").trim();
  boNhoDem.set(key, text);
  return { giai_thich: text, nguon: "llm" };
});

tuyen("POST", "/tro-ly", async (c) => {
  const b = await than(c);
  const cau_hoi = chuoi(b, "cau_hoi"), ngu_canh = chuoi(b, "ngu_canh", { md: "" }), lich_su = ds(b, "lich_su", laChuoi("lich_su"), { md: [] });
  if (await aiBiTat(c.env, c.nd)) return { tra_loi: LOI_AI_TAT, hanh_dong: [], nguon: "khoa" };
  const p = await chon(c.env);
  if (!p) return { tra_loi: "Máy chủ chưa cấu hình mô hình AI; robot dùng lệnh cơ bản.", hanh_dong: [], nguon: "khong" };
  const sop = ngCanhMay(cau_hoi, ngu_canh);
  const user = `NGỮ CẢNH THÍ NGHIỆM:\n${ngu_canh}\n\n` + (sop ? `QUY TRÌNH MÁY (tài liệu của phòng lab, coi là đúng):\n${sop}\n\n` : "") +
    `HỘI THOẠI GẦN ĐÂY:\n${lich_su.slice(-8).join("\n")}\n\nHỌC SINH: ${cau_hoi}`;
  let data: any;
  try {
    data = await p.structured(SYSTEM_ASSIST, user, ASSIST_SCHEMA, 1500);
  } catch (e) {
    return { tra_loi: loiAi(e), hanh_dong: [], nguon: "loi" };
  }
  const hanh_dong: any[] = [];
  for (const a of Array.isArray(data.hanh_dong) ? data.hanh_dong : []) {
    if (!a || !ACTIONS.includes(a.loai)) continue;
    let stt = 0;
    if (a.loai === "tim_hoa_chat" || a.loai === "mang_hoa_chat") {
      const ch = findChemical(String(a.hoa_chat ?? ""));
      if (!ch) continue;
      stt = ch.stt;
    }
    hanh_dong.push({ loai: a.loai, stt, ghi_chu: String(a.hoa_chat ?? "") });
  }
  let tra_loi = String(data.tra_loi ?? "");
  const khoa = await hoaChatBiKhoa(c.env, c.nd);
  const duoc = hanh_dong.filter((a) => !(a.loai === "mang_hoa_chat" && khoa.has(a.stt)));
  if (duoc.length < hanh_dong.length) tra_loi = (tra_loi + " Hóa chất này đang bị giáo viên khóa nên mình không mang ra được.").trim();
  return { tra_loi, hanh_dong: duoc, nguon: p.NAME };
});

function boBuocHoaChatKhoa(steps: any[], khoa: Set<number>): [any[], string[]] {
  // Bỏ từ bước đầu tiên đụng tới hóa chất đang khóa trở đi: các bước sau thường cần chính chất đó
  if (!khoa.size) return [steps, []];
  for (const [i, s] of steps.entries()) {
    const dung = new Set<number>([s.stt]);
    for (const v of [s.doi_tuong, s.dich]) {
      const m = /^hoa_chat:(\d+)$/.exec(v);
      if (m) dung.add(Number(m[1]));
    }
    const trung = [...dung].find((x) => khoa.has(x));
    if (trung !== undefined) return [steps.slice(0, i), [`bước ${i + 1}: ${CHEM_BY_STT.get(trung)?.ten ?? "hóa chất"} đang bị giáo viên khóa, dừng kế hoạch tại đây`]];
  }
  return [steps, []];
}

tuyen("POST", "/robot/ke-hoach", async (c) => {
  const b = await than(c);
  const cau_hoi = chuoi(b, "cau_hoi"), lich_su = ds(b, "lich_su", laChuoi("lich_su"), { md: [] });
  const tg = b.the_gioi && typeof b.the_gioi === "object" ? b.the_gioi : {};
  const scene = { khu_vuc: String(tg.khu_vuc ?? ""), nguoi_dang_cam: String(tg.nguoi_dang_cam ?? ""), robot_dang_cam: String(tg.robot_dang_cam ?? ""),
    che_do: String(tg.che_do ?? ""), su_kien: Array.isArray(tg.su_kien) ? tg.su_kien.map(String) : [],
    vat: (Array.isArray(tg.vat) ? tg.vat : []).filter((v: any) => v && typeof v.id === "string")
      .map((v: any) => ({ id: v.id, ten: String(v.ten ?? ""), loai: String(v.loai ?? ""), chua: String(v.chua ?? ""), khoang_cach: Number(v.khoang_cach ?? 0) || 0 })) };
  const nd = c.nd;
  if (await aiBiTat(c.env, nd)) return { tra_loi: LOI_AI_TAT, ke_hoach: [], bo_qua: [], nguon: "khoa" };
  const p = await chon(c.env);
  if (!p) return { tra_loi: "Máy chủ chưa cấu hình mô hình AI; robot dùng lệnh cơ bản.", ke_hoach: [], bo_qua: [], nguon: "khong" };
  const sop = ngCanhMay(cau_hoi);
  const user = `ẢNH CHỤP CẢNH:\n${formatScene(scene)}\n\n` + (sop ? `QUY TRÌNH MÁY (tài liệu của phòng lab):\n${sop}\n\n` : "") +
    `HỘI THOẠI GẦN ĐÂY:\n${lich_su.slice(-8).join("\n")}\n\nHỌC SINH: ${cau_hoi}`;
  let data: any;
  try {
    data = await p.structured(SYSTEM_PLAN, user, PLAN_SCHEMA, 4000);
  } catch (e) {
    return { tra_loi: loiAi(e), ke_hoach: [], bo_qua: [], nguon: "loi" };
  }
  let [steps, dropped] = validate(Array.isArray(data.ke_hoach) ? data.ke_hoach : [], new Set(scene.vat.map((v: any) => v.id)), findChemical);
  const [con, bi_khoa] = boBuocHoaChatKhoa(steps, await hoaChatBiKhoa(c.env, nd));
  steps = con;
  dropped = dropped.concat(bi_khoa);
  try {
    const summary = steps.map((s) => s.ky_nang + (s.doi_tuong ? `(${s.doi_tuong})` : "")).join(" → ");
    await ghiNhatKy(c, "robot", "Lệnh robot: " + cau_hoi.slice(0, 200), (summary || "chỉ trả lời") + (dropped.length ? "\nBỏ: " + dropped.join("; ") : ""),
      dropped.length ? "Warning" : "Info", p.NAME);
  } catch { /* nhật ký không được làm hỏng lệnh robot */ }
  let tra_loi = String(data.tra_loi || "");
  if (bi_khoa.length) tra_loi = (tra_loi + " " + bi_khoa[0]).trim();
  return { tra_loi, ke_hoach: steps.map((s) => ({ stt: s.stt, ky_nang: s.ky_nang, doi_tuong: s.doi_tuong, dich: s.dich, so_luong: s.so_luong, loi: s.loi })),
    bo_qua: dropped, nguon: p.NAME };
});
