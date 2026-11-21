// Kiểm thử phòng học trực tuyến từ đầu đến cuối bằng WebSocket thật (Node 22+):
//
//   node cloudflare/kiem_thu_phong.mjs http://127.0.0.1:8788      trên `wrangler pages dev` + `wrangler dev` (cloudflare/phong)
//   node cloudflare/kiem_thu_phong.mjs https://verlab.pages.dev   trên mạng (tạo một phòng thử rồi đóng lại)
//
// Dùng tài khoản thử gv.thu1, hs.thu1, hs.thu2 (Lớp thử A), hs.thu3 (lớp khác); mật khẩu đọc từ data/tai_khoan_mau_mat_khau.json (git bỏ qua).

import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const GOC = (process.argv[2] || "http://127.0.0.1:8788").replace(/\/$/, "");
const WS_GOC = GOC.replace(/^http/, "ws");
const MK = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), "../data/tai_khoan_mau_mat_khau.json"), "utf-8")).mat_khau;
const dat = [], hong = [];
const kiem = (ten, dk, ct = "") => { (dk ? dat : hong).push(ten); console.log((dk ? "  đạt " : "  HỎNG ") + ten + (dk ? "" : `  ← ${String(typeof ct === "string" ? ct : JSON.stringify(ct)).slice(0, 300)}`)); };
const ngu = (ms) => new Promise((r) => setTimeout(r, ms));

async function dangNhap(ten) {
  const r = await fetch(GOC + "/auth/dang-nhap", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ ten_dang_nhap: ten, mat_khau: MK[ten] }) });
  if (!r.ok) throw new Error(`Không đăng nhập được ${ten}: ${r.status}`);
  return r.headers.getSetCookie().map((c) => c.split(";")[0]).join("; ");
}

async function goi(cookie, duong, pm = "GET", body) {
  const h = { origin: GOC };
  if (cookie) h.cookie = cookie;
  if (body !== undefined) h["content-type"] = "application/json";
  const r = await fetch(GOC + duong, { method: pm, headers: h, body: body === undefined ? undefined : JSON.stringify(body) });
  const d = await r.json().catch(() => ({}));
  return { ma: r.status, d };
}

// WebSocket có hộp thư: chờ(tin thỏa điều kiện) trả tin đầu tiên khớp, kể cả tin đã đến trước
function moWs(duong, cookie, origin = GOC) {
  const ws = new WebSocket(WS_GOC + duong, { headers: { ...(cookie ? { cookie } : {}), origin } });
  const thu = [], cho = [];
  ws.dong = null;
  ws.addEventListener("message", (e) => {
    if (e.data === "pong") return;
    const m = JSON.parse(e.data);
    m._luc = performance.now();
    thu.push(m);
    for (const c of [...cho]) if (c.dk(m)) { cho.splice(cho.indexOf(c), 1); c.ok(m); }
  });
  ws.addEventListener("close", (e) => { ws.dong = e.code; for (const c of cho.splice(0)) c.ok(null); });
  ws.mo = new Promise((ok) => { ws.addEventListener("open", () => ok(true)); ws.addEventListener("error", () => ok(false)); ws.addEventListener("close", () => ok(false)); });
  ws.cho = (dk, ms = 5000) => {
    const san = thu.find(dk);
    if (san) { thu.splice(thu.indexOf(san), 1); return Promise.resolve(san); }
    return new Promise((ok) => {
      const c = { dk, ok: (m) => { clearTimeout(c.h); if (m) thu.splice(thu.indexOf(m), 1); ok(m); } };
      c.h = setTimeout(() => { cho.splice(cho.indexOf(c), 1); ok(null); }, ms);
      cho.push(c);
    });
  };
  ws.guiJ = (d) => ws.send(JSON.stringify(d));
  ws.boQua = () => thu.splice(0);
  return ws;
}

console.log(`Kiểm thử phòng trực tuyến trên ${GOC}`);
const gv = await dangNhap("gv.thu1"), hs1 = await dangNhap("hs.thu1"), hs2 = await dangNhap("hs.thu2"), hs3 = await dangNhap("hs.thu3");

// ---- trang và quyền ----
let r = await fetch(GOC + "/phong-hoc");
kiem("trang /phong-hoc công khai", r.status === 200 && (await r.text()).includes("Vào phòng học"), r.status);
r = await fetch(GOC + "/phong-truc-tuyen", { redirect: "manual" });
kiem("trang giáo viên chưa đăng nhập chuyển hướng", r.status === 303, r.status);
r = await fetch(GOC + "/phong-truc-tuyen", { headers: { cookie: hs1 }, redirect: "manual" });
kiem("học sinh không vào được trang giáo viên", r.status === 303 && r.headers.get("location") === "/hoc-sinh", r.headers.get("location"));
r = await goi(hs1, "/api/phong", "POST", { ten: "x" });
kiem("học sinh không tạo được phòng", r.ma === 403, r);

// ---- tạo phòng ----
const lop = (await goi(gv, "/api/lop")).d.find((l) => l.ten === "Lớp thử A");
r = await goi(gv, "/api/phong", "POST", { ten: "Phòng kiểm thử", lop_id: lop.id, cho_khach: false });
kiem("giáo viên tạo phòng có mã 6 ký tự", r.ma === 200 && /^[A-Z2-9]{6}$/.test(r.d.ma) && r.d.mo, r);
const phong = r.d;
r = await goi(gv, "/api/phong");
kiem("danh sách phòng có phòng mới", r.d.some((p) => p.id === phong.id && p.mo), r.d);

// ---- vào phòng: quyền ----
r = await goi(null, "/api/vao-phong", "POST", { ma: "ZZZZZZ" });
kiem("mã sai trả 404", r.ma === 404, r);
r = await goi(null, "/api/vao-phong", "POST", { ma: phong.ma, ho_ten: "Khách A" });
kiem("phòng không cho khách: khách nhận 401", r.ma === 401, r);
r = await goi(hs3, "/api/vao-phong", "POST", { ma: phong.ma });
kiem("học sinh lớp khác bị chặn", r.ma === 403, r);
r = await goi(gv, "/api/vao-phong", "POST", { ma: phong.ma });
kiem("giáo viên không vào như học sinh", r.ma === 403, r);
r = await goi(hs1, "/api/vao-phong", "POST", { ma: phong.ma.toLowerCase() });
kiem("học sinh trong lớp vào được (mã chữ thường cũng được)", r.ma === 200 && r.d.ban.ho_ten && r.d.ten === "Phòng kiểm thử", r);

// ---- WebSocket ----
const lay = moWs(`/api/vao-phong/ket-noi?ma=${phong.ma}`, hs1, "https://trang-la.example");
kiem("WebSocket từ trang khác bị chặn", !(await lay.mo), lay.dong);
const wsHs3 = moWs(`/api/vao-phong/ket-noi?ma=${phong.ma}`, hs3);
kiem("WebSocket của học sinh lớp khác bị chặn", !(await wsHs3.mo));
const wsKhongDn = moWs(`/api/phong/${phong.id}/ket-noi`, hs1);
kiem("học sinh không mở được WebSocket giáo viên", !(await wsKhongDn.mo));

const g = moWs(`/api/phong/${phong.id}/ket-noi`, gv);
kiem("giáo viên nối WebSocket", await g.mo);
let m = await g.cho((x) => x.t === "toan_bo");
kiem("giáo viên nhận toàn bộ trạng thái khi nối", m && m.phong.ma === phong.ma && Array.isArray(m.hoc_sinh), m);

const a = moWs(`/api/vao-phong/ket-noi?ma=${phong.ma}`, hs1);
kiem("học sinh nối WebSocket", await a.mo);
m = await a.cho((x) => x.t === "chao");
kiem("học sinh nhận lời chào có tên phòng", m && m.phong.ten === "Phòng kiểm thử" && m.ban.ma.startsWith("u"), m);
const maA = m.ban.ma;
m = await g.cho((x) => x.t === "su_kien" && x.su_kien.noi_dung === "Vào phòng");
kiem("giáo viên thấy học sinh vào phòng", Boolean(m), m);
m = await g.cho((x) => x.t === "hs" && x.hs.ma === maA && x.hs.online);
kiem("giáo viên thấy học sinh đang trực tuyến", Boolean(m));

// trạng thái trình duyệt
a.guiJ({ t: "tt", thiet_bi: "Máy tính · Chrome", hoat_dong: "Đang chờ giáo viên", tre_ms: 42 });
m = await g.cho((x) => x.t === "hs" && x.hs.thiet_bi === "Máy tính · Chrome");
kiem("giáo viên nhận thiết bị, việc đang làm, độ trễ", m && m.hs.hoat_dong === "Đang chờ giáo viên" && m.hs.tre_ms === 42, m);
const t0 = performance.now();
a.guiJ({ t: "tt", hien: false });
m = await g.cho((x) => x.t === "su_kien" && x.su_kien.noi_dung.startsWith("Rời khỏi trang"));
const tre = m ? Math.round(m._luc - t0) : null;
kiem("rời tab: giáo viên nhận sự kiện ngay", Boolean(m));
console.log(`    (từ lúc học sinh rời tab tới lúc giáo viên nhận: ${tre} ms)`);
m = await g.cho((x) => x.t === "hs" && x.hs.ma === maA && x.hs.hien === false);
kiem("thẻ học sinh chuyển sang 'rời trang', đếm 1 lần", m && m.hs.so_lan_roi_tab === 1, m);
await ngu(1100);
a.guiJ({ t: "tt", hien: true });
m = await g.cho((x) => x.t === "su_kien" && x.su_kien.noi_dung.startsWith("Quay lại"));
kiem("quay lại: có thời gian đã rời", Boolean(m), m);
m = await g.cho((x) => x.t === "hs" && x.hs.ma === maA && x.hs.hien === true);
kiem("cộng dồn thời gian rời trang", m && m.hs.giay_roi_tab >= 1, m?.hs);
a.guiJ({ t: "tt", ranh: true, focus: false });
m = await g.cho((x) => x.t === "hs" && x.hs.ma === maA && x.hs.ranh && !x.hs.focus);
kiem("không thao tác, đang ở cửa sổ khác", Boolean(m));

// chiếu, nhắn, khóa
g.guiJ({ t: "chieu", loai: "tin", tieu_de: "Bước 1", noi_dung: "Rửa tay" });
m = await a.cho((x) => x.t === "chieu");
kiem("học sinh nhận nội dung chiếu", m && m.chieu.tieu_de === "Bước 1", m);
a.guiJ({ t: "da_xem", phien_ban: m.chieu.phien_ban });
m = await g.cho((x) => x.t === "hs" && x.hs.ma === maA && x.hs.da_xem > 0);
kiem("giáo viên biết máy đã hiện nội dung", Boolean(m));
g.guiJ({ t: "tin", den: null, noi_dung: "Còn 5 phút" });
m = await a.cho((x) => x.t === "tin");
kiem("nhắn cả phòng", m && m.noi_dung === "Còn 5 phút" && !m.rieng, m);
g.guiJ({ t: "tin", den: maA, noi_dung: "Em xem lại bước 2" });
m = await a.cho((x) => x.t === "tin");
kiem("nhắn riêng", m && m.rieng, m);
g.guiJ({ t: "khoa", bat: true, noi_dung: "Nhìn lên bảng" });
m = await a.cho((x) => x.t === "khoa");
kiem("khóa màn hình cả phòng", m && m.khoa.bat && m.khoa.noi_dung === "Nhìn lên bảng", m);
a.guiJ({ t: "da_khoa", bat: true });
m = await g.cho((x) => x.t === "hs" && x.hs.ma === maA && x.hs.dang_khoa);
kiem("giáo viên biết máy đã khóa", Boolean(m));
g.guiJ({ t: "khoa", bat: false });
m = await a.cho((x) => x.t === "khoa");
kiem("mở khóa", m && !m.khoa.bat, m);

// câu hỏi nhanh
g.guiJ({ t: "cau_hoi", noi_dung: "Quỳ tím gặp axit?", lua_chon: ["Xanh", "Đỏ", "Không đổi"], dap_an: 1 });
m = await a.cho((x) => x.t === "cau_hoi" && x.cau_hoi);
kiem("học sinh nhận câu hỏi, không thấy đáp án", m && m.cau_hoi.lua_chon.length === 3 && m.cau_hoi.dap_an === undefined, m);
a.guiJ({ t: "tra_loi", cau_id: m.cau_hoi.id, chon: 1 });
m = await g.cho((x) => x.t === "dem_cau");
kiem("giáo viên thấy số trả lời ngay", m && m.dem[1] === 1, m);
g.guiJ({ t: "cong_bo" });
m = await a.cho((x) => x.t === "cau_hoi" && x.cau_hoi?.cong_bo);
kiem("công bố: học sinh thấy đáp án và thống kê", m && m.cau_hoi.dap_an === 1 && m.cau_hoi.dem[1] === 1, m);

// giơ tay
a.guiJ({ t: "tay", gio: true });
m = await g.cho((x) => x.t === "su_kien" && x.su_kien.noi_dung === "Giơ tay");
kiem("giơ tay thành cảnh báo cho giáo viên", m && m.su_kien.muc === "canh_bao", m);
g.guiJ({ t: "ha_tay", ma: null });
m = await a.cho((x) => x.t === "tay");
kiem("giáo viên hạ tay", m && m.gio === false, m);

// chế độ kiểm tra
g.guiJ({ t: "che_do", gia_tri: "kiem_tra" });
m = await a.cho((x) => x.t === "phong");
kiem("học sinh được báo chế độ kiểm tra", m && m.phong.che_do === "kiem_tra", m);
a.guiJ({ t: "tt", hien: false });
m = await g.cho((x) => x.t === "su_kien" && x.su_kien.noi_dung.startsWith("Rời khỏi trang"));
kiem("chế độ kiểm tra: rời trang là cảnh báo", m && m.su_kien.muc === "canh_bao", m);
a.guiJ({ t: "tt", hien: true });

// lệnh sai
g.guiJ({ t: "cau_hoi", noi_dung: "Thiếu lựa chọn", lua_chon: ["Một"] });
m = await g.cho((x) => x.t === "loi");
kiem("lệnh sai trả lỗi rõ ràng", m && m.loi.includes("2 đến 6"), m);
a.guiJ({ t: "chieu", loai: "tin", tieu_de: "học sinh tự chiếu" });
m = await a.cho((x) => x.t === "chieu", 800);
kiem("học sinh không gửi được lệnh của giáo viên", m === null, m);

// ảnh bài giảng
const fd = new FormData();
fd.append("ten", "Bài kiểm thử phòng");
fd.append("tep", new Blob([new Uint8Array([0xff, 0xd8, 0xff, 0xe0, 1, 2, 3])], { type: "image/jpeg" }), "01.jpg");
fd.append("tep", new Blob([new Uint8Array([0xff, 0xd8, 0xff, 0xe0, 4, 5, 6])], { type: "image/jpeg" }), "02.jpg");
r = await fetch(GOC + "/api/bai-giang", { method: "POST", headers: { cookie: gv, origin: GOC }, body: fd });
const bai = await r.json();
g.guiJ({ t: "chieu", loai: "slide", bai_id: bai.id, trang: 2, so_trang: 2, tieu_de: "Bài · Trang 2/2" });
await a.cho((x) => x.t === "chieu" && x.chieu.loai === "slide");
r = await fetch(`${GOC}/api/vao-phong/anh?ma=${phong.ma}&bai=${bai.id}&trang=2`, { headers: { cookie: hs1 } });
kiem("học sinh trong phòng xem được trang đang chiếu", r.status === 200 && r.headers.get("content-type") === "image/jpeg", r.status);
r = await fetch(`${GOC}/api/vao-phong/anh?ma=${phong.ma}&bai=${bai.id}&trang=1`, { headers: { cookie: hs1 } });
kiem("trang không chiếu thì không xem được", r.status === 404, r.status);
r = await fetch(`${GOC}/api/vao-phong/anh?ma=${phong.ma}&bai=${bai.id}&trang=2`, { headers: { cookie: hs3 } });
kiem("người ngoài phòng không xem được", r.status === 404 || r.status === 403, r.status);
await goi(gv, `/api/bai-giang/${bai.id}`, "DELETE");

// khách
r = await goi(gv, `/api/phong/${phong.id}`, "PATCH", { cho_khach: true });
kiem("bật cho khách", r.ma === 200 && r.d.cho_khach, r);
r = await goi(null, "/api/vao-phong", "POST", { ma: phong.ma, ho_ten: "Giám khảo Một" });
kiem("khách vào bằng họ tên, nhận mã khách", r.ma === 200 && r.d.khach && r.d.ban.loai === "khach", r);
const maKhach = r.d.khach;
const k = moWs(`/api/vao-phong/ket-noi?ma=${phong.ma}&khach=${maKhach}`, null);
kiem("khách nối WebSocket", await k.mo);
m = await g.cho((x) => x.t === "hs" && x.hs.loai === "khach" && x.hs.online);
kiem("giáo viên thấy khách", m && m.hs.ho_ten === "Giám khảo Một", m);
r = await goi(null, "/api/vao-phong", "POST", { ma: phong.ma, khach: maKhach });
kiem("khách nối lại bằng mã cũ, không tạo người mới", r.ma === 200 && r.d.khach === maKhach, r);
r = await goi(null, "/api/vao-phong", "POST", { ma: phong.ma, khach: "sai" + maKhach.slice(3) });
kiem("mã khách giả bị từ chối", r.ma === 403, r);
const kGia = moWs(`/api/vao-phong/ket-noi?ma=${phong.ma}&khach=khonghople`, null);
kiem("WebSocket với mã khách giả bị từ chối", !(await kGia.mo));

// mở ở tab khác
const a2 = moWs(`/api/vao-phong/ket-noi?ma=${phong.ma}`, hs1);
kiem("mở tab thứ hai", await a2.mo);
m = await a.cho((x) => x.t === "mo_noi_khac");
kiem("tab cũ được báo đã mở ở nơi khác", Boolean(m));
await ngu(400);
const ds = (await g.cho((x) => x.t === "hs" && x.hs.ma === maA, 1500)) ?? null;
kiem("chuyển tab không tính là rời phòng", !ds || ds.hs.online, ds?.hs);

// mời ra
const b = moWs(`/api/vao-phong/ket-noi?ma=${phong.ma}`, hs2);
await b.mo;
m = await b.cho((x) => x.t === "chao");
const maB = m.ban.ma;
g.guiJ({ t: "moi_ra", ma: maB, cam: true });
m = await b.cho((x) => x.t === "bi_moi_ra");
kiem("mời ra, cấm vào lại", m && m.cam, m);
r = await goi(hs2, "/api/vao-phong", "POST", { ma: phong.ma });
kiem("học sinh bị cấm không xin vào lại được", r.ma === 403, r);
const b2 = moWs(`/api/vao-phong/ket-noi?ma=${phong.ma}`, hs2);
m = await b2.cho((x) => x.t === "tu_choi");
kiem("WebSocket của học sinh bị cấm bị từ chối", Boolean(m), m);
g.guiJ({ t: "bo_cam", ma: maB });
await g.cho((x) => x.t === "toan_bo" && x.hoc_sinh.find((h) => h.ma === maB && !h.bi_cam));
r = await goi(hs2, "/api/vao-phong", "POST", { ma: phong.ma });
kiem("cho vào lại", r.ma === 200, r);

// khóa phòng
r = await goi(gv, `/api/phong/${phong.id}`, "PATCH", { khoa_vao: true });
r = await goi(null, "/api/vao-phong", "POST", { ma: phong.ma, ho_ten: "Khách Hai" });
kiem("khóa phòng: không nhận khách mới", r.ma === 403, r);
r = await goi(null, "/api/vao-phong", "POST", { ma: phong.ma, khach: maKhach });
kiem("khóa phòng: người đã vào vẫn nối lại được", r.ma === 200, r);

// đóng phòng
r = await goi(gv, `/api/phong/${phong.id}/dong`, "POST", {});
const tk = r.d.tong_ket;
kiem("đóng phòng, có bản tổng kết", r.ma === 200 && !r.d.mo && tk && tk.so_nguoi === 3 && tk.so_khach === 1, r);
const hsTk = tk?.hoc_sinh.find((h) => h.ten_dang_nhap === "hs.thu1");
kiem("tổng kết: số lần rời trang, câu đúng", hsTk && hsTk.so_lan_roi_tab === 2 && hsTk.so_cau_dung === 1 && hsTk.giay_trong_phong >= 1, hsTk);
m = await a2.cho((x) => x.t === "dong_phong");
kiem("học sinh được báo phòng đã đóng", Boolean(m));
r = await goi(hs1, "/api/vao-phong", "POST", { ma: phong.ma });
kiem("phòng đóng thì mã không dùng được", r.ma === 404, r);
r = await goi(gv, "/api/phong");
kiem("phòng đã đóng nằm trong lịch sử kèm tổng kết", r.d.some((p) => p.id === phong.id && !p.mo && p.tong_ket), r.d);

for (const w of [g, a, a2, b, b2, k]) try { w.close(); } catch { /* đã đóng */ }
console.log(`\n${dat.length} đạt, ${hong.length} hỏng`);
process.exit(hong.length ? 1 : 0);
