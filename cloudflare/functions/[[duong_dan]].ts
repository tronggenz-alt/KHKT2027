// Pages Functions: mọi đường dẫn có trong _routes.json (do xay.mjs tạo) đi qua đây; tệp tĩnh Cloudflare tự phục vụ.
import type { Env } from "../src/khung";
import { xuLy } from "../src/app";

export const onRequest: PagesFunction<Env> = (ctx) => xuLy(ctx.request, ctx.env);
