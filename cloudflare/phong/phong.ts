// Phòng học trực tuyến: mỗi phòng là một Durable Object giữ WebSocket của giáo viên và học sinh.
//
// Trình duyệt học sinh báo trạng thái thật (đang mở tab hay đã rời tab, cửa sổ có đang được chọn, có thao tác trong 60 giây qua,
// độ trễ mạng, thiết bị) và nhận lệnh của giáo viên (chiếu nội dung, nhắn tin, khóa màn hình, câu hỏi nhanh, mời ra).
// Giáo viên nhận mọi thay đổi ngay khi xảy ra, không phải hỏi lại máy chủ.
//
// Chỉ Pages Functions gọi được vào đây (Worker không có đường dẫn công khai). Functions đã kiểm đăng nhập, quyền với phòng,
// rồi gửi người gọi trong header x-vera-nguoi (JSON, mã hóa URI). Khách không có tài khoản được nhận diện bằng mã khách do đây cấp.
//
// Dùng WebSocket Hibernation: phòng ngủ khi không có tin, "ping" được trả "pong" tự động mà không đánh thức phòng,
// nên gói miễn phí đủ cho một lớp. Trạng thái nằm trong bộ nhớ của Durable Object (SQLite), không ghi D1.

import { DurableObject } from "cloudflare:workers";

interface Env { PHONG: DurableObjectNamespace }

type MucDo = "thong_tin" | "canh_bao" | "nguy_hiem";
interface Nguoi { vai: "gv" | "hs"; ma: string; ho_ten: string; ten_dang_nhap?: string; loai?: "tai_khoan" | "khach" }
interface Gan { vai: "gv" | "hs"; ma: string; tu: number }

interface Chieu { phien_ban: number; loai: "tat" | "tin" | "slide"; tieu_de: string; noi_dung: string; bai_id: number | null; trang: number; so_trang: number; luc: number }
interface CauHoi { id: number; noi_dung: string; lua_chon: string[]; dap_an: number | null; mo: boolean; cong_bo: boolean; bat_dau: number }
interface TongCau { id: number; noi_dung: string; lua_chon: string[]; dap_an: number | null; dem: number[]; so_tra_loi: number; so_dung: number | null }

interface Phong {
  id: number; ma: string; ten: string; giao_vien: string; lop: string; cho_khach: boolean; khoa_vao: boolean;
  che_do: "hoc" | "kiem_tra"; dong: boolean; tao_luc: number; dong_luc: number;
  chieu: Chieu; khoa: { bat: boolean; noi_dung: string }; cau_hoi: CauHoi | null; cau_da_qua: TongCau[]; dem_khach: number;
}

interface HocSinh {
  ma: string; ho_ten: string; loai: "tai_khoan" | "khach"; ten_dang_nhap: string;
  vao_dau: number; lan_vao: number; tu_luc: number; roi_luc: number; giay_trong_phong: number;
  hien: boolean; focus: boolean; ranh: boolean; hoat_dong: string; thiet_bi: string; tre_ms: number | null;
  so_lan_roi_tab: number; giay_roi_tab: number; roi_tab_tu: number;
  tay: number; tra_loi: Record<string, { chon: number; luc: number }>; da_xem: number; dang_khoa: boolean;
  khoa_rieng: string | null; bi_cam: boolean; tin_cho: { noi_dung: string; luc: number }[];
}

interface SuKien { id: number; luc: number; ma: string; ho_ten: string; muc: MucDo; noi_dung: string }

const GIAY_MAT_KET_NOI = 60; // không thấy "ping" (20 giây một lần) chừng này giây thì coi như đã rời phòng
const GIU_SU_KIEN = 200;
const now = () => Date.now() / 1000;
const json = (d: unknown, status = 200) => new Response(JSON.stringify(d), { status, headers: { "content-type": "application/json" } });

const chuoi = (v: unknown, toi_da: number) => (typeof v === "string" ? v.trim().slice(0, toi_da) : "");
const giay = (s: number) => (s < 60 ? `${Math.round(s)} giây` : `${Math.floor(s / 60)} phút ${Math.round(s % 60)} giây`);

function phongMoi(): Phong {
  return {
    id: 0, ma: "", ten: "", giao_vien: "", lop: "", cho_khach: false, khoa_vao: false, che_do: "hoc", dong: false, tao_luc: now(), dong_luc: 0,
    chieu: { phien_ban: 0, loai: "tat", tieu_de: "", noi_dung: "", bai_id: null, trang: 1, so_trang: 0, luc: 0 },
    khoa: { bat: false, noi_dung: "" }, cau_hoi: null, cau_da_qua: [], dem_khach: 0,
  };
}

export class PhongTrucTuyen extends DurableObject<Env> {
  phong: Phong = phongMoi();
  hs = new Map<string, HocSinh>();
  khach = new Map<string, { ma: string; ho_ten: string }>(); // mã khách -> người
  su_kien: SuKien[] = [];
  dem_tin = new Map<WebSocket, { tu: number; so: number }>(); // chặn gửi dồn dập

  constructor(ctx: DurableObjectState, env: Env) {
    super(ctx, env);
    ctx.setWebSocketAutoResponse(new WebSocketRequestResponsePair("ping", "pong"));
    ctx.blockConcurrencyWhile(async () => {
      this.phong = { ...phongMoi(), ...((await ctx.storage.get<Phong>("phong")) ?? {}) };
      for (const [, h] of await ctx.storage.list<HocSinh>({ prefix: "hs:" })) this.hs.set(h.ma, h);
      for (const [k, v] of await ctx.storage.list<{ ma: string; ho_ten: string }>({ prefix: "khach:" })) this.khach.set(k.slice(6), v);
      this.su_kien = (await ctx.storage.get<SuKien[]>("su_kien")) ?? [];
    });
  }

  // -------------------------------------------------------------------------
  // Lưu và gửi
  // -------------------------------------------------------------------------

  luuPhong() { this.ctx.storage.put("phong", this.phong); }
  luuHs(h: HocSinh) { this.ctx.storage.put("hs:" + h.ma, h); }

  gui(ws: WebSocket, d: unknown) {
    try { ws.send(JSON.stringify(d)); } catch { /* socket vừa đóng */ }
  }
  guiGv(d: unknown) { for (const ws of this.ctx.getWebSockets("gv")) this.gui(ws, d); }
  guiHs(ma: string, d: unknown) { for (const ws of this.ctx.getWebSockets("hs:" + ma)) this.gui(ws, d); }
  guiMoiHs(tao: (h: HocSinh) => unknown) {
    for (const ws of this.ctx.getWebSockets()) {
      const g = ws.deserializeAttachment() as Gan | null;
      const h = g?.vai === "hs" ? this.hs.get(g.ma) : undefined;
      if (h) this.gui(ws, tao(h));
    }
  }

  dangMo(ma: string, tru?: WebSocket): boolean {
    return this.ctx.getWebSockets("hs:" + ma).some((w) => w !== tru && w.readyState === WebSocket.OPEN);
  }

  ghiSuKien(h: { ma: string; ho_ten: string } | null, muc: MucDo, noi_dung: string) {
    const sk: SuKien = { id: (this.su_kien.at(-1)?.id ?? 0) + 1, luc: now(), ma: h?.ma ?? "", ho_ten: h?.ho_ten ?? "Giáo viên", muc, noi_dung };
    this.su_kien.push(sk);
    if (this.su_kien.length > GIU_SU_KIEN) this.su_kien.splice(0, this.su_kien.length - GIU_SU_KIEN);
    this.ctx.storage.put("su_kien", this.su_kien);
    this.guiGv({ t: "su_kien", su_kien: sk });
  }

  // -------------------------------------------------------------------------
  // Dữ liệu gửi đi
  // -------------------------------------------------------------------------

  demCau(cau: CauHoi): number[] {
    const dem = cau.lua_chon.map(() => 0);
    for (const h of this.hs.values()) {
      const t = h.tra_loi[cau.id];
      if (t && t.chon >= 0 && t.chon < dem.length) dem[t.chon]++;
    }
    return dem;
  }

  tongCau(cau: CauHoi): TongCau {
    const dem = this.demCau(cau);
    return { id: cau.id, noi_dung: cau.noi_dung, lua_chon: cau.lua_chon, dap_an: cau.dap_an, dem,
      so_tra_loi: dem.reduce((a, b) => a + b, 0), so_dung: cau.dap_an === null ? null : dem[cau.dap_an] };
  }

  soCauDung(h: HocSinh): number {
    let n = 0;
    for (const c of [...this.phong.cau_da_qua, ...(this.phong.cau_hoi ? [this.phong.cau_hoi] : [])]) {
      if (c.dap_an !== null && h.tra_loi[c.id]?.chon === c.dap_an && ("cong_bo" in c ? c.cong_bo || !c.mo : true)) n++;
    }
    return n;
  }

  hsChoGv(h: HocSinh) {
    const { tra_loi, tin_cho, ...con } = h;
    const cau = this.phong.cau_hoi;
    return { ...con, online: this.dangMo(h.ma), tra_loi_cau: cau ? tra_loi[cau.id]?.chon ?? null : null,
      so_cau_tra_loi: Object.keys(tra_loi).length, so_cau_dung: this.soCauDung(h) };
  }

  cauChoHs(h: HocSinh) {
    const c = this.phong.cau_hoi;
    if (!c) return null;
    const out: Record<string, unknown> = { id: c.id, noi_dung: c.noi_dung, lua_chon: c.lua_chon, mo: c.mo, cong_bo: c.cong_bo, da_chon: h.tra_loi[c.id]?.chon ?? null };
    if (c.cong_bo) Object.assign(out, { dap_an: c.dap_an, dem: this.demCau(c) });
    return out;
  }

  khoaCua(h: HocSinh) {
    if (h.khoa_rieng !== null) return { bat: true, noi_dung: h.khoa_rieng, rieng: true };
    return { bat: this.phong.khoa.bat, noi_dung: this.phong.khoa.noi_dung, rieng: false };
  }

  phongChoHs() {
    const p = this.phong;
    return { ten: p.ten, giao_vien: p.giao_vien, lop: p.lop, che_do: p.che_do };
  }

  toanBo() {
    const p = this.phong;
    const { cau_hoi, ...con } = p;
    return { t: "toan_bo", may_chu_luc: now(), phong: { ...con, cau_hoi: cau_hoi ? { ...cau_hoi, dem: this.demCau(cau_hoi) } : null },
      hoc_sinh: [...this.hs.values()].map((h) => this.hsChoGv(h)), su_kien: this.su_kien.slice(-100) };
  }

  guiHsChoGv(h: HocSinh) { this.guiGv({ t: "hs", may_chu_luc: now(), hs: this.hsChoGv(h) }); }

  tongKet() {
    const p = this.phong;
    const cau = [...p.cau_da_qua, ...(p.cau_hoi ? [this.tongCau(p.cau_hoi)] : [])];
    const t = now();
    const hoc_sinh = [...this.hs.values()].map((h) => ({
      ho_ten: h.ho_ten, loai: h.loai, ten_dang_nhap: h.ten_dang_nhap, lan_vao: h.lan_vao,
      giay_trong_phong: Math.round(h.giay_trong_phong + (this.dangMo(h.ma) ? t - h.tu_luc : 0)),
      so_lan_roi_tab: h.so_lan_roi_tab, giay_roi_tab: Math.round(h.giay_roi_tab + (h.roi_tab_tu ? t - h.roi_tab_tu : 0)),
      so_cau_tra_loi: Object.keys(h.tra_loi).length, so_cau_dung: this.soCauDung(h), bi_cam: h.bi_cam,
    })).sort((a, b) => a.ho_ten.localeCompare(b.ho_ten, "vi"));
    return { bat_dau: p.tao_luc, ket_thuc: p.dong_luc || t, so_nguoi: hoc_sinh.length, so_khach: hoc_sinh.filter((h) => h.loai === "khach").length, cau_hoi: cau, hoc_sinh };
  }

  // -------------------------------------------------------------------------
  // Yêu cầu từ Pages Functions
  // -------------------------------------------------------------------------

  kiemVao(n: Nguoi): string | null {
    if (this.phong.dong) return "Phòng đã đóng";
    const h = this.hs.get(n.ma);
    if (h?.bi_cam) return "Giáo viên đã mời bạn ra khỏi phòng này";
    if (!h && this.phong.khoa_vao) return "Phòng đã khóa, không nhận thêm người vào";
    return null;
  }

  docNguoi(req: Request): Nguoi | null {
    const khach = req.headers.get("x-vera-khach");
    if (khach) {
      const k = this.khach.get(khach);
      return k ? { vai: "hs", ma: k.ma, ho_ten: k.ho_ten, loai: "khach" } : null;
    }
    const h = req.headers.get("x-vera-nguoi");
    if (!h) return null;
    try { return JSON.parse(decodeURIComponent(h)); } catch { return null; }
  }

  async fetch(req: Request): Promise<Response> {
    const url = new URL(req.url);
    const nguoi = this.docNguoi(req);
    switch (url.pathname) {
      case "/khoi-tao": {
        // Tạo phòng hoặc đổi cài đặt (tên, cho khách, khóa vào): Functions đã ghi D1, ở đây chỉ chép lại
        const b = await req.json<Partial<Phong>>();
        const moi = !this.phong.id;
        Object.assign(this.phong, { id: b.id, ma: b.ma, ten: b.ten, giao_vien: b.giao_vien, lop: b.lop ?? "", cho_khach: Boolean(b.cho_khach), khoa_vao: Boolean(b.khoa_vao) });
        if (moi) this.phong.tao_luc = b.tao_luc ?? now();
        this.luuPhong();
        this.guiGv(this.toanBo());
        this.guiMoiHs(() => ({ t: "phong", phong: this.phongChoHs() }));
        return json({ ok: true });
      }
      case "/kiem-vao": {
        if (!nguoi) return json({ detail: "Mã khách không còn dùng được, hãy nhập lại mã phòng" }, 403);
        const loi = this.kiemVao(nguoi);
        return loi ? json({ detail: loi }, 403) : json({ ok: true });
      }
      case "/khach": {
        // Cấp mã khách: chỉ khi giáo viên cho khách vào (Functions đã kiểm cho_khach trong D1)
        if (this.phong.dong) return json({ detail: "Phòng đã đóng" }, 403);
        if (this.phong.khoa_vao) return json({ detail: "Phòng đã khóa, không nhận thêm người vào" }, 403);
        const { ho_ten } = await req.json<{ ho_ten: string }>();
        if (this.khach.size >= 200) return json({ detail: "Phòng đã đủ 200 khách" }, 403);
        const ma_khach = [...crypto.getRandomValues(new Uint8Array(18))].map((b) => b.toString(16).padStart(2, "0")).join("");
        this.phong.dem_khach++;
        const k = { ma: "k" + this.phong.dem_khach, ho_ten };
        this.khach.set(ma_khach, k);
        this.ctx.storage.put("khach:" + ma_khach, k);
        this.luuPhong();
        return json({ khach: ma_khach, ma: k.ma, ho_ten });
      }
      case "/ket-noi":
        return this.ketNoi(req, nguoi);
      case "/duoc-xem": {
        // Ảnh trang bài giảng: chỉ người trong phòng, chỉ trang đang chiếu
        const c = this.phong.chieu, h = nguoi ? this.hs.get(nguoi.ma) : undefined;
        const dung = Boolean(h && !h.bi_cam && !this.phong.dong && c.loai === "slide" && c.bai_id === Number(url.searchParams.get("bai")) && c.trang === Number(url.searchParams.get("trang")));
        return json({ ok: dung }, dung ? 200 : 404);
      }
      case "/tong-ket":
        return json(this.tongKet());
      case "/dong": {
        if (!this.phong.dong) {
          const t = now();
          this.phong.dong = true;
          this.phong.dong_luc = t;
          if (this.phong.cau_hoi) { this.phong.cau_da_qua.push(this.tongCau(this.phong.cau_hoi)); this.phong.cau_hoi = null; }
          for (const h of this.hs.values()) {
            if (this.dangMo(h.ma)) { h.giay_trong_phong += t - h.tu_luc; h.roi_luc = t; }
            if (h.roi_tab_tu) { h.giay_roi_tab += t - h.roi_tab_tu; h.roi_tab_tu = 0; }
            this.luuHs(h);
          }
          this.luuPhong();
          for (const ws of this.ctx.getWebSockets()) {
            this.gui(ws, { t: "dong_phong" });
            try { ws.close(4004, "Phòng đã đóng"); } catch { /* đã đóng */ }
          }
          await this.ctx.storage.deleteAlarm();
        }
        return json(this.tongKet());
      }
    }
    return json({ detail: "Not Found" }, 404);
  }

  async ketNoi(req: Request, nguoi: Nguoi | null): Promise<Response> {
    if (req.headers.get("upgrade")?.toLowerCase() !== "websocket") return json({ detail: "Cần kết nối WebSocket" }, 426);
    if (!nguoi) return json({ detail: "Mã khách không đúng, hãy vào lại phòng" }, 403);
    const [khach, may] = Object.values(new WebSocketPair());
    const t = now();
    if (nguoi.vai === "gv") {
      this.ctx.acceptWebSocket(may, ["gv"]);
      may.serializeAttachment({ vai: "gv", ma: nguoi.ma, tu: t } satisfies Gan);
      this.gui(may, this.toanBo());
    } else {
      // Mỗi học sinh một kết nối: mở ở tab hoặc máy khác thì kết nối cũ đóng lại
      const cu = this.ctx.getWebSockets("hs:" + nguoi.ma);
      this.ctx.acceptWebSocket(may, ["hs:" + nguoi.ma]);
      may.serializeAttachment({ vai: "hs", ma: nguoi.ma, tu: t } satisfies Gan);
      const loi = this.kiemVao(nguoi);
      if (loi) {
        this.gui(may, { t: "tu_choi", loi });
        may.close(4003, "Tu choi");
      } else {
        for (const w of cu) {
          this.gui(w, { t: "mo_noi_khac" });
          try { w.close(4001, "Mo o noi khac"); } catch { /* đã đóng */ }
        }
        this.vaoPhong(may, nguoi, cu.some((w) => w.readyState === WebSocket.OPEN));
      }
    }
    if ((await this.ctx.storage.getAlarm()) === null) await this.ctx.storage.setAlarm(Date.now() + 30_000);
    return new Response(null, { status: 101, webSocket: khach });
  }

  vaoPhong(ws: WebSocket, n: Nguoi, dang_mo_san: boolean) {
    const t = now();
    let h = this.hs.get(n.ma);
    if (!h) {
      h = { ma: n.ma, ho_ten: n.ho_ten, loai: n.loai ?? "tai_khoan", ten_dang_nhap: n.ten_dang_nhap ?? "", vao_dau: t, lan_vao: 0, tu_luc: t, roi_luc: 0,
        giay_trong_phong: 0, hien: true, focus: true, ranh: false, hoat_dong: "", thiet_bi: "", tre_ms: null, so_lan_roi_tab: 0, giay_roi_tab: 0, roi_tab_tu: 0,
        tay: 0, tra_loi: {}, da_xem: 0, dang_khoa: false, khoa_rieng: null, bi_cam: false, tin_cho: [] };
      this.hs.set(n.ma, h);
    }
    h.ho_ten = n.ho_ten;
    if (dang_mo_san) {
      h.giay_trong_phong += t - h.tu_luc; // chuyển sang tab mới: không tính là rời phòng
    } else {
      h.lan_vao++;
    }
    if (h.roi_tab_tu) { h.giay_roi_tab += t - h.roi_tab_tu; h.roi_tab_tu = 0; }
    Object.assign(h, { tu_luc: t, hien: true, focus: true, ranh: false, dang_khoa: false, da_xem: 0 });
    const tin = h.tin_cho;
    h.tin_cho = [];
    this.luuHs(h);
    this.gui(ws, { t: "chao", may_chu_luc: t, ban: { ma: h.ma, ho_ten: h.ho_ten, loai: h.loai }, phong: this.phongChoHs(),
      chieu: this.phong.chieu, khoa: this.khoaCua(h), cau_hoi: this.cauChoHs(h), tay: h.tay > 0 });
    for (const m of tin) this.gui(ws, { t: "tin", ...m, rieng: true });
    if (!dang_mo_san) this.ghiSuKien(h, "thong_tin", h.lan_vao > 1 ? "Vào lại phòng" : "Vào phòng");
    this.guiHsChoGv(h);
  }

  roiPhong(ma: string, ws: WebSocket, ly_do: string) {
    const h = this.hs.get(ma);
    if (!h || this.dangMo(ma, ws) || this.phong.dong) return;
    const t = now();
    h.giay_trong_phong += t - h.tu_luc;
    h.roi_luc = t;
    if (h.roi_tab_tu) { h.giay_roi_tab += t - h.roi_tab_tu; h.roi_tab_tu = 0; }
    h.dang_khoa = false;
    this.luuHs(h);
    this.ghiSuKien(h, this.phong.che_do === "kiem_tra" ? "canh_bao" : "thong_tin", ly_do);
    this.guiHsChoGv(h);
  }

  // -------------------------------------------------------------------------
  // WebSocket
  // -------------------------------------------------------------------------

  async webSocketMessage(ws: WebSocket, tin: string | ArrayBuffer) {
    if (typeof tin !== "string" || tin.length > 6000) return;
    const g = ws.deserializeAttachment() as Gan | null;
    if (!g || this.phong.dong) return;
    // Quá 40 tin trong 10 giây: trình duyệt lỗi hoặc cố tình gửi dồn, đóng kết nối
    const d = this.dem_tin.get(ws), t = now();
    if (!d || t - d.tu > 10) this.dem_tin.set(ws, { tu: t, so: 1 });
    else if (++d.so > 40) { ws.close(4008, "Gui qua nhieu"); return; }
    let m: any;
    try { m = JSON.parse(tin); } catch { return; }
    if (!m || typeof m !== "object") return;
    if (g.vai === "gv") this.lenhGiaoVien(ws, m);
    else this.tinHocSinh(g.ma, m);
  }

  async webSocketClose(ws: WebSocket, code: number) {
    this.dem_tin.delete(ws);
    const g = ws.deserializeAttachment() as Gan | null;
    if (g?.vai === "hs" && code !== 4001 && code !== 4003) this.roiPhong(g.ma, ws, "Rời phòng (đóng trang hoặc mất mạng)");
    try { ws.close(1000, "Da dong"); } catch { /* đã đóng */ }
  }

  async webSocketError(ws: WebSocket) {
    const g = ws.deserializeAttachment() as Gan | null;
    if (g?.vai === "hs") this.roiPhong(g.ma, ws, "Mất kết nối");
  }

  // Máy mất mạng đột ngột không kịp đóng WebSocket: không thấy "ping" quá 60 giây thì coi như đã rời phòng
  async alarm() {
    const t = Date.now();
    const con = this.ctx.getWebSockets();
    for (const ws of con) {
      const g = ws.deserializeAttachment() as Gan | null;
      const cuoi = this.ctx.getWebSocketAutoResponseTimestamp(ws)?.getTime() ?? (g ? g.tu * 1000 : 0);
      if (t - cuoi > GIAY_MAT_KET_NOI * 1000) {
        try { ws.close(4000, "Mat ket noi"); } catch { /* đã đóng */ }
        if (g?.vai === "hs") this.roiPhong(g.ma, ws, "Mất kết nối");
      }
    }
    if (this.ctx.getWebSockets().some((w) => w.readyState === WebSocket.OPEN)) await this.ctx.storage.setAlarm(Date.now() + 30_000);
  }

  tinHocSinh(ma: string, m: any) {
    const h = this.hs.get(ma);
    if (!h || h.bi_cam) return;
    const t = now();
    let doi = false;
    if (m.t === "tt") {
      if (typeof m.hien === "boolean" && m.hien !== h.hien) {
        h.hien = m.hien;
        if (!m.hien) {
          h.so_lan_roi_tab++;
          h.roi_tab_tu = t;
          this.ghiSuKien(h, this.phong.che_do === "kiem_tra" ? "canh_bao" : "thong_tin", "Rời khỏi trang phòng học (chuyển tab, thu nhỏ hoặc khóa máy)");
        } else if (h.roi_tab_tu) {
          const lau = t - h.roi_tab_tu;
          h.giay_roi_tab += lau;
          h.roi_tab_tu = 0;
          this.ghiSuKien(h, "thong_tin", `Quay lại trang phòng học sau ${giay(lau)}`);
        }
        doi = true;
      }
      for (const k of ["focus", "ranh"] as const) if (typeof m[k] === "boolean" && m[k] !== h[k]) { h[k] = m[k]; doi = true; }
      for (const k of ["hoat_dong", "thiet_bi"] as const) if (typeof m[k] === "string" && chuoi(m[k], 80) !== h[k]) { h[k] = chuoi(m[k], 80); doi = true; }
      if (typeof m.tre_ms === "number" && Number.isFinite(m.tre_ms)) { h.tre_ms = Math.max(0, Math.min(60000, Math.round(m.tre_ms))); doi = true; }
    } else if (m.t === "tay") {
      const gio = Boolean(m.gio);
      if (gio !== h.tay > 0) {
        h.tay = gio ? t : 0;
        if (gio) this.ghiSuKien(h, "canh_bao", "Giơ tay");
        doi = true;
      }
    } else if (m.t === "tra_loi") {
      const c = this.phong.cau_hoi;
      if (!c || !c.mo || m.cau_id !== c.id || !Number.isInteger(m.chon) || m.chon < 0 || m.chon >= c.lua_chon.length) return;
      h.tra_loi[c.id] = { chon: m.chon, luc: t };
      doi = true;
      this.guiGv({ t: "dem_cau", cau_id: c.id, dem: this.demCau(c) });
    } else if (m.t === "da_xem") {
      if (Number.isInteger(m.phien_ban) && m.phien_ban !== h.da_xem) { h.da_xem = m.phien_ban; doi = true; }
    } else if (m.t === "da_khoa") {
      if (Boolean(m.bat) !== h.dang_khoa) { h.dang_khoa = Boolean(m.bat); doi = true; }
    }
    if (doi) {
      this.luuHs(h);
      this.guiHsChoGv(h);
    }
  }

  lenhGiaoVien(ws: WebSocket, m: any) {
    const p = this.phong, t = now();
    const loi = (chu: string) => this.gui(ws, { t: "loi", loi: chu });
    const theo = (ma: unknown) => (typeof ma === "string" ? this.hs.get(ma) : undefined);
    switch (m.t) {
      case "chieu": {
        const loai = m.loai;
        if (!["tat", "tin", "slide"].includes(loai)) return loi("Loại chiếu không đúng");
        const c: Chieu = { phien_ban: Math.max(Math.trunc(t * 1000), p.chieu.phien_ban + 1), loai, tieu_de: chuoi(m.tieu_de, 200), noi_dung: chuoi(m.noi_dung, 2000),
          bai_id: null, trang: 1, so_trang: 0, luc: t };
        if (loai === "tin" && !c.tieu_de && !c.noi_dung) return loi("Hãy nhập tiêu đề hoặc nội dung");
        if (loai === "slide") {
          if (!Number.isInteger(m.bai_id) || !Number.isInteger(m.trang) || !Number.isInteger(m.so_trang) || m.trang < 1 || m.trang > m.so_trang) return loi("Trang bài giảng không đúng");
          Object.assign(c, { bai_id: m.bai_id, trang: m.trang, so_trang: m.so_trang });
        }
        p.chieu = c;
        this.guiMoiHs(() => ({ t: "chieu", chieu: c }));
        if (loai !== "slide" || m.trang === 1) this.ghiSuKien(null, "thong_tin", loai === "tat" ? "Tắt nội dung đang chiếu" : `Chiếu: ${c.tieu_de || c.noi_dung.slice(0, 60)}`);
        break;
      }
      case "tin": {
        const noi_dung = chuoi(m.noi_dung, 300);
        if (!noi_dung) return loi("Hãy nhập nội dung");
        const tin = { noi_dung, luc: t };
        const h = theo(m.den);
        if (m.den) {
          if (!h) return loi("Không có học sinh này trong phòng");
          if (this.dangMo(h.ma)) this.guiHs(h.ma, { t: "tin", ...tin, rieng: true });
          else { h.tin_cho = [...h.tin_cho, tin].slice(-5); this.luuHs(h); }
          this.ghiSuKien(null, "thong_tin", `Nhắn riêng ${h.ho_ten}: ${noi_dung}`);
        } else {
          this.guiMoiHs(() => ({ t: "tin", ...tin, rieng: false }));
          this.ghiSuKien(null, "thong_tin", `Nhắn cả phòng: ${noi_dung}`);
        }
        break;
      }
      case "khoa": {
        const bat = Boolean(m.bat), noi_dung = chuoi(m.noi_dung, 200) || "Tất cả chú ý lên giáo viên";
        if (m.den) {
          const h = theo(m.den);
          if (!h) return loi("Không có học sinh này trong phòng");
          h.khoa_rieng = bat ? noi_dung : null;
          this.luuHs(h);
          this.guiHs(h.ma, { t: "khoa", khoa: this.khoaCua(h) });
          this.ghiSuKien(h, "thong_tin", bat ? "Giáo viên khóa màn hình của em này" : "Giáo viên mở khóa màn hình của em này");
        } else {
          p.khoa = { bat, noi_dung: bat ? noi_dung : "" };
          if (!bat) for (const h of this.hs.values()) if (h.khoa_rieng !== null) { h.khoa_rieng = null; this.luuHs(h); }
          this.guiMoiHs((h) => ({ t: "khoa", khoa: this.khoaCua(h) }));
          this.ghiSuKien(null, "thong_tin", bat ? `Khóa màn hình cả phòng: ${noi_dung}` : "Mở khóa màn hình cả phòng");
        }
        break;
      }
      case "cau_hoi": {
        const noi_dung = chuoi(m.noi_dung, 300);
        const lua_chon = Array.isArray(m.lua_chon) ? m.lua_chon.map((x: unknown) => chuoi(x, 120)).filter(Boolean) : [];
        if (!noi_dung) return loi("Hãy nhập câu hỏi");
        if (lua_chon.length < 2 || lua_chon.length > 6) return loi("Câu hỏi cần từ 2 đến 6 lựa chọn");
        const dap_an = Number.isInteger(m.dap_an) && m.dap_an >= 0 && m.dap_an < lua_chon.length ? m.dap_an : null;
        if (p.cau_hoi) p.cau_da_qua = [...p.cau_da_qua, this.tongCau(p.cau_hoi)].slice(-50);
        const id = Math.max(0, ...p.cau_da_qua.map((c) => c.id)) + 1;
        p.cau_hoi = { id, noi_dung, lua_chon, dap_an, mo: true, cong_bo: false, bat_dau: t };
        this.guiMoiHs((h) => ({ t: "cau_hoi", cau_hoi: this.cauChoHs(h) }));
        this.ghiSuKien(null, "thong_tin", `Câu hỏi nhanh: ${noi_dung}`);
        break;
      }
      case "dong_cau":
      case "cong_bo": {
        const c = p.cau_hoi;
        if (!c) return loi("Chưa có câu hỏi nào");
        c.mo = false;
        if (m.t === "cong_bo") c.cong_bo = true;
        this.guiMoiHs((h) => ({ t: "cau_hoi", cau_hoi: this.cauChoHs(h) }));
        for (const h of this.hs.values()) if (this.dangMo(h.ma)) this.guiHsChoGv(h);
        break;
      }
      case "xoa_cau": {
        if (p.cau_hoi) p.cau_da_qua = [...p.cau_da_qua, this.tongCau(p.cau_hoi)].slice(-50);
        p.cau_hoi = null;
        this.guiMoiHs(() => ({ t: "cau_hoi", cau_hoi: null }));
        break;
      }
      case "moi_ra": {
        const h = theo(m.ma);
        if (!h) return loi("Không có học sinh này trong phòng");
        h.bi_cam = Boolean(m.cam);
        h.tay = 0;
        const dang = this.dangMo(h.ma);
        for (const w of this.ctx.getWebSockets("hs:" + h.ma)) {
          this.gui(w, { t: "bi_moi_ra", cam: h.bi_cam });
          try { w.close(4003, "Bi moi ra"); } catch { /* đã đóng */ }
        }
        if (dang) { h.giay_trong_phong += t - h.tu_luc; h.roi_luc = t; }
        if (h.roi_tab_tu) { h.giay_roi_tab += t - h.roi_tab_tu; h.roi_tab_tu = 0; }
        this.luuHs(h);
        this.ghiSuKien(h, "canh_bao", h.bi_cam ? "Bị mời ra và không được vào lại" : "Bị mời ra khỏi phòng");
        break;
      }
      case "bo_cam": {
        const h = theo(m.ma);
        if (!h) return loi("Không có học sinh này trong phòng");
        h.bi_cam = false;
        this.luuHs(h);
        this.ghiSuKien(h, "thong_tin", "Được vào lại phòng");
        break;
      }
      case "ha_tay": {
        const ds = m.ma ? [theo(m.ma)].filter((x): x is HocSinh => Boolean(x)) : [...this.hs.values()];
        for (const h of ds) if (h.tay) { h.tay = 0; this.luuHs(h); this.guiHs(h.ma, { t: "tay", gio: false }); }
        break;
      }
      case "che_do": {
        if (m.gia_tri !== "hoc" && m.gia_tri !== "kiem_tra") return loi("Chế độ phải là hoc hoặc kiem_tra");
        p.che_do = m.gia_tri;
        this.guiMoiHs(() => ({ t: "phong", phong: this.phongChoHs() }));
        this.ghiSuKien(null, "thong_tin", p.che_do === "kiem_tra" ? "Bật chế độ kiểm tra: rời trang sẽ thành cảnh báo" : "Về chế độ học");
        break;
      }
      default:
        return loi("Không có lệnh này");
    }
    this.luuPhong();
    this.guiGv(this.toanBo());
  }
}

export default {
  fetch: () => new Response("Not Found", { status: 404 }),
} satisfies ExportedHandler<Env>;
