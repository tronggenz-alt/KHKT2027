// Điểm vào của mọi đường dẫn động: kiểm quyền, gọi xử lý, đổi lỗi thành JSON {"detail": ...} như FastAPI.

import "./ai";
import { COOKIE, kiemQuyen } from "./dang_nhap";
import "./kho_hoa";
import { Ctx, Env, HttpLoi, json, timTuyen } from "./khung";
import "./lop";
import "./phong_hoc";
import "./sinh";

export async function xuLy(req: Request, env: Env): Promise<Response> {
  const url = new URL(req.url);
  const c: Ctx = { req, env, url, params: {}, nd: null, ip: req.headers.get("cf-connecting-ip") || "", cookie: [] };
  let res: Response;
  try {
    const pm = req.method === "HEAD" ? "GET" : req.method;
    const tim = timTuyen(pm, url.pathname);
    const chan = await kiemQuyen(c, tim.mau);
    if (chan) res = chan;
    else if (!tim.t) res = tim.sai_pm ? json({ detail: "Method Not Allowed" }, 405) : json({ detail: "Not Found" }, 404);
    else {
      c.params = tim.params;
      const out = await tim.t.xu(c);
      res = out instanceof Response ? out : json(out);
    }
  } catch (e) {
    if (e instanceof HttpLoi) res = json({ detail: e.chi_tiet }, e.ma, e.dau);
    else {
      console.error(e);
      res = json({ detail: "Lỗi máy chủ" }, 500);
    }
  }
  // Máy dùng chung: trình duyệt không được giữ lại trang, dữ liệu của người vừa đăng xuất
  const h = new Headers(res.headers);
  h.set("Cache-Control", "no-store");
  for (const ck of c.cookie) h.append("Set-Cookie", ck);
  return new Response(req.method === "HEAD" ? null : res.body, { status: res.status, headers: h });
}

export { COOKIE };
