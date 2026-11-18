// ve_giay.js – vẽ sơ đồ cơ chế "trên giấy" theo quy ước trình bày của đáp án HSG, kèm bộ chấm độ dễ nhìn.
//
// Ba lớp, dùng chung cho mọi cơ chế:
//   1. KHUNG_MAU – thư viện khung khó vẽ, mỗi khung lưu MỘT LẦN theo đúng cách đáp án vẽ (toạ độ chuẩn hoá theo độ dài
//      liên kết = 1, trục y hướng lên). Gặp lại khung đó thì đặt mẫu, không để máy tự bố trí.
//   2. Quy ước vẽ (trong veSoDo): nét "sau" vẽ trước; nét trước có khe che (halo màu giấy) ở đoạn giữa để chỗ giao nhau
//      đọc được là "đi phía trước" như ChemDraw; chữ nhóm thế cắt nét; mũi tên nửa đầu một ngạnh phía ngoài đường cong.
//   3. kiemTraDeNhin – đếm lỗi: nét cắt nhau (trừ khi một nét là "sau"), chữ đè nét/chữ, mũi tên cắt ngang liên kết
//      (bỏ qua đoạn sát hai đầu mũi tên), góc giữa hai liên kết tại một nguyên tử quá hẹp.
//
// Dữ liệu sơ đồ: { khung:[x0,x1,y0,y1], nt:{id:[x,y,"nhãn"?]}, lk:[[a,b,kiểu?,phía?]], goc:[[x,y]], mt:[{tu,qua,den}],
//                  pu:[{x1,x2,y,tren?,duoi?,thuan_nghich?}], ten:[[x,y,"A1"]], dt:[[x,y,"⁺"]], chu:[[x,y,"+"]] }
//   kiểu liên kết: 1 | 2 | 3 | "sau" | "dam" (nét đậm hình nêm, đầu hẹp ở a) | "gach" (nét gạch, đầu hẹp ở a)
//   mũi tên cong: kieu "nua_dau" (1 e, một ngạnh) | "day_du"/"vong" (2 e, hai ngạnh)

export const MAU_GIAY = "#fffdf7";

// ------------------------------------------------------------------ 1. thư viện khung mẫu
export const KHUNG_MAU = {
  // [1.1.1]propellane / bicyclo[1.1.1]pentane – kiểu "lều" của đáp án: hai C đầu cầu B1 (trên-phải), B2 (dưới-trái)
  // chụm ở giữa; ba cầu CH2 toả ra ba hướng xa nhau: đỉnh T, xa bên trái L, góc dưới phải R.
  // Nét L–B1 là cầu phía sau (đi dưới nét T–B2). Propellane thêm liên kết trung tâm B1–B2.
  // Vẽ to hơn vòng thường 1,35 lần (như đáp án) để mũi tên cong quanh hai C đầu cầu có chỗ.
  leu: {
    dinh: { T: [.07, 1.55], L: [-1.55, -.13], R: [1.08, -.81], B1: [.47, .34], B2: [0, -.27] },
    canh: [["T", "B1"], ["T", "B2"], ["L", "B1", "sau"], ["L", "B2"], ["R", "B1"], ["R", "B2"]],
    ghi_chu: "B1 nhận nhóm thế hướng lên-phải; electron độc thân của B2 đặt phía dưới.",
  },
  // vòng 4 cạnh dạng thoi "◇": đỉnh trái Tr (gắn =CH2 nằm ngang), trên Tn, phải P (C spiro / C mang nhóm thế), dưới D
  thoi: {
    dinh: { Tr: [0, 0], Tn: [.65, .65], P: [1.3, 0], D: [.65, -.65] },
    canh: [["Tr", "Tn"], ["Tn", "P"], ["P", "D"], ["D", "Tr"]],
  },
  // cyclopropene spiro gắn bên phải đỉnh S: C=C dựng đứng, nét đôi thứ hai phía trong vòng
  cyclopropen_dung: {
    dinh: { S: [0, 0], Tr: [.75, .58], Du: [.75, -.58] },
    canh: [["S", "Tr"], ["S", "Du"], ["Tr", "Du", 2, [.35, 0]]],
  },
};

// đặt khung mẫu vào sơ đồ: goc = [x, y] vị trí gốc mẫu; ten = {đỉnh mẫu: id trong sơ đồ}
export function datKhung(sd, mau, [ox, oy], ten) {
  const k = KHUNG_MAU[mau];
  if (!k) throw new Error(`Chưa có khung mẫu "${mau}"`);
  for (const [d, [x, y]] of Object.entries(k.dinh)) sd.nt[ten[d]] = [ox + x, oy + y, ...(sd.nt[ten[d]]?.slice(2) || [])];
  for (const [a, b, kieu, phia] of k.canh) {
    const c = [ten[a], ten[b]];
    if (kieu) c.push(kieu);
    if (phia) c.push([ox + phia[0], oy + phia[1]]);
    sd.lk.push(c);
  }
}

// ------------------------------------------------------------------ 2. vẽ theo quy ước
const nuaRong = t => t ? t.length * .17 + .1 : 0;      // nửa bề rộng nhãn (đơn vị độ dài liên kết)
const KY_TU = .17;                                       // nửa bề rộng một ký tự

// Nhãn kiểu ChemDraw: ký tự gắn với liên kết nằm đúng tại nguyên tử, phần còn lại kéo ra phía ngoài.
// Nhóm gắn bên phải chỗ nối ("CO₂Me" sau C) → căn trái từ ký tự đầu; gắn bên trái ("MeO₂C") → căn phải từ ký tự cuối.
function chuanHoa(d) {
  const nt = Object.fromEntries(Object.entries(d.nt).map(([k, v]) => [k, { id: k, x: v[0], y: v[1], t: v[2] || "" }]));
  for (const [a, b] of d.lk) { (nt[a].ke ||= []).push(nt[b]); (nt[b].ke ||= []).push(nt[a]); }
  for (const a of Object.values(nt)) {
    a.can = "giua";
    if (a.t && [...a.t].length > 1 && a.ke?.length === 1) a.can = a.ke[0].x > a.x + .3 ? "phai" : "trai";
    const w = 2 * nuaRong(a.t);
    a.hop = a.can === "trai" ? { x0: a.x - KY_TU, x1: a.x - KY_TU + w } : a.can === "phai" ? { x0: a.x + KY_TU - w, x1: a.x + KY_TU } : { x0: a.x - w / 2, x1: a.x + w / 2 };
    a.hop.y0 = a.y - .24; a.hop.y1 = a.y + .24;
  }
  return nt;
}

export function veSoDo(d, S = 34) {
  const [x0, x1, y0, y1] = d.khung;
  const X = x => (x - x0) * S, Y = y => (y1 - y) * S, f = v => v.toFixed(1);
  const nt = chuanHoa(d);
  const sau = [], khe = [], truoc = [];
  for (const [ia, ib, kieu = 1, phia] of d.lk) {
    const a = nt[ia], b = nt[ib];
    let dx = b.x - a.x, dy = b.y - a.y; const L = Math.hypot(dx, dy); dx /= L; dy /= L;
    const cat = n => !n.t ? 0 : n.can !== "giua" ? (Math.abs(dx) > .7 ? KY_TU + .1 : .32) : Math.abs(dx) > .7 ? nuaRong(n.t) + .02 : .32;
    const p = { x: a.x + dx * cat(a), y: a.y + dy * cat(a) }, q = { x: b.x - dx * cat(b), y: b.y - dy * cat(b) };
    if (kieu === "dam" || kieu === "gach") {   // lập thể: nêm đặc (hướng về người xem) / nêm gạch (phía sau)
      const w = .14, nx0 = -dy, ny0 = dx;
      if (kieu === "dam") truoc.push(`<path d="M${f(X(p.x))},${f(Y(p.y))} L${f(X(q.x + nx0 * w))},${f(Y(q.y + ny0 * w))} L${f(X(q.x - nx0 * w))},${f(Y(q.y - ny0 * w))} z" class="nem"/>`);
      else for (let k = 1; k <= 6; k++) {
        const t = k / 6, cx = p.x + (q.x - p.x) * t, cy = p.y + (q.y - p.y) * t, r = w * t;
        truoc.push(`<line x1="${f(X(cx + nx0 * r))}" y1="${f(Y(cy + ny0 * r))}" x2="${f(X(cx - nx0 * r))}" y2="${f(Y(cy - ny0 * r))}" class="lk"/>`);
      }
      continue;
    }
    const la_sau = kieu === "sau";
    const net = (u, v) => `<line x1="${f(X(u.x))}" y1="${f(Y(u.y))}" x2="${f(X(v.x))}" y2="${f(Y(v.y))}" class="${la_sau ? "lk sau" : "lk"}"/>`;
    const nx = -dy, ny = dx;
    let s = "";
    if (kieu === 2 && phia) {          // nét thứ hai ngắn hơn, nằm phía trong (về phía tâm vòng / phía chỉ định)
      const sgn = Math.sign((phia[0] - a.x) * nx + (phia[1] - a.y) * ny) || 1, o = .16 * sgn, c = .16;
      s = net(p, q) + net({ x: p.x + nx * o + dx * c, y: p.y + ny * o + dy * c }, { x: q.x + nx * o - dx * c, y: q.y + ny * o - dy * c });
    } else if (kieu === 2) {
      s = net({ x: p.x + nx * .08, y: p.y + ny * .08 }, { x: q.x + nx * .08, y: q.y + ny * .08 }) + net({ x: p.x - nx * .08, y: p.y - ny * .08 }, { x: q.x - nx * .08, y: q.y - ny * .08 });
    } else if (kieu === 3) {
      for (const o of [-.13, 0, .13]) s += net({ x: p.x + nx * o, y: p.y + ny * o }, { x: q.x + nx * o, y: q.y + ny * o });
    } else s = net(p, q);
    if (la_sau) { sau.push(s); continue; }
    // khe che: dải màu giấy ở 70% giữa nét, vẽ trước mọi nét "trước" → nét sau bị ngắt ở chỗ giao
    const k = .15, rong = kieu === 3 ? 13 : kieu === 2 ? 10 : 7;
    khe.push(`<line x1="${f(X(p.x + (q.x - p.x) * k))}" y1="${f(Y(p.y + (q.y - p.y) * k))}" x2="${f(X(q.x - (q.x - p.x) * k))}" y2="${f(Y(q.y - (q.y - p.y) * k))}" stroke="${MAU_GIAY}" stroke-width="${rong}"/>`);
    truoc.push(s);
  }
  let s = sau.join("") + khe.join("") + truoc.join("");
  for (const a of Object.values(nt)) if (a.t) {
    const t = a.t.replace(/([₀-₉])/g, c => `<tspan dy="4" font-size="11">${"₀₁₂₃₄₅₆₇₈₉".indexOf(c)}</tspan><tspan dy="-4">`);
    const xn = a.can === "trai" ? X(a.x - KY_TU) : a.can === "phai" ? X(a.x + KY_TU) : X(a.x);
    const neo = a.can === "trai" ? ' style="text-anchor:start"' : a.can === "phai" ? ' style="text-anchor:end"' : "";
    s += `<text x="${f(xn)}" y="${f(Y(a.y))}" class="nt"${neo}>${t}${"</tspan>".repeat((a.t.match(/[₀-₉]/g) || []).length)}</text>`;
  }
  for (const [x, y] of d.goc || []) s += `<circle cx="${f(X(x))}" cy="${f(Y(y))}" r="2.7" class="goc"/>`;
  for (const [x, y, t] of d.dt || []) s += `<text x="${f(X(x))}" y="${f(Y(y))}" class="dt">${t.replace("⁺", "+").replace("⁻", "−")}</text>`;
  for (const [x, y, t] of d.chu || []) s += `<text x="${f(X(x))}" y="${f(Y(y))}" class="nt">${t}</text>`;
  for (const m of d.mt || []) {        // mũi tên cong nửa đầu: một ngạnh ở phía ngoài đường cong
    const [ax, ay] = m.tu, [cx, cy] = m.qua, [bx, by] = m.den;
    s += `<path d="M${f(X(ax))},${f(Y(ay))} Q${f(X(cx))},${f(Y(cy))} ${f(X(bx))},${f(Y(by))}" class="mt"/>`;
    let tx = X(bx) - X(cx), ty = Y(by) - Y(cy); const tl = Math.hypot(tx, ty); tx /= tl; ty /= tl;
    let px = -ty, py = tx;
    const gx = (X(ax) + X(bx)) / 2, gy = (Y(ay) + Y(by)) / 2;
    if ((X(cx) - gx) * px + (Y(cy) - gy) * py < 0) { px = -px; py = -py; }
    s += `<path d="M${f(X(bx))},${f(Y(by))} L${f(X(bx) - tx * 8 + px * 4.5)},${f(Y(by) - ty * 8 + py * 4.5)}" class="mt"/>`;
    if (m.kieu && m.kieu !== "nua_dau") s += `<path d="M${f(X(bx))},${f(Y(by))} L${f(X(bx) - tx * 8 - px * 4.5)},${f(Y(by) - ty * 8 - py * 4.5)}" class="mt"/>`;
  }
  for (const p of d.pu || []) {
    const xa = X(p.x1), xb = X(p.x2), y = Y(p.y);
    if (p.thuan_nghich) s += `<line x1="${f(xa)}" y1="${f(y - 3)}" x2="${f(xb)}" y2="${f(y - 3)}" class="pu"/><path d="M${f(xb)},${f(y - 3)} l-9,-5" class="pu"/>` +
      `<line x1="${f(xa)}" y1="${f(y + 3)}" x2="${f(xb)}" y2="${f(y + 3)}" class="pu"/><path d="M${f(xa)},${f(y + 3)} l9,5" class="pu"/>`;
    else s += `<line x1="${f(xa)}" y1="${f(y)}" x2="${f(xb - 2)}" y2="${f(y)}" class="pu"/><path d="M${f(xb)},${f(y)} l-10,-4 l2,4 l-2,4 z" fill="#1b2621"/>`;
    if (p.tren) s += `<text x="${f((xa + xb) / 2)}" y="${f(y - 9)}" class="dk">${p.tren}</text>`;
    if (p.duoi) s += `<text x="${f((xa + xb) / 2)}" y="${f(y + 18)}" class="dk">${p.duoi}</text>`;
  }
  for (const [x, y, t] of d.ten || []) s += `<text x="${f(X(x))}" y="${f(Y(y))}" class="ten">${t}</text>`;
  return `<svg class="so-do" viewBox="0 0 ${f((x1 - x0) * S)} ${f((y1 - y0) * S)}">${s}</svg>`;
}

// ------------------------------------------------------------------ 3. bộ chấm độ dễ nhìn
function giaoDoan(p1, p2, p3, p4) {     // giao điểm hai đoạn thẳng (null nếu không cắt)
  const d = (p2.x - p1.x) * (p4.y - p3.y) - (p2.y - p1.y) * (p4.x - p3.x);
  if (Math.abs(d) < 1e-9) return null;
  const t = ((p3.x - p1.x) * (p4.y - p3.y) - (p3.y - p1.y) * (p4.x - p3.x)) / d;
  const u = ((p3.x - p1.x) * (p2.y - p1.y) - (p3.y - p1.y) * (p2.x - p1.x)) / d;
  return t > 1e-6 && t < 1 - 1e-6 && u > 1e-6 && u < 1 - 1e-6 ? { x: p1.x + t * (p2.x - p1.x), y: p1.y + t * (p2.y - p1.y) } : null;
}
const kc = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
const hopChu = a => a.hop;
const trongHop = (p, h) => p.x > h.x0 && p.x < h.x1 && p.y > h.y0 && p.y < h.y1;

export function kiemTraDeNhin(d) {
  const nt = chuanHoa(d);
  const lk = d.lk.map(([a, b, kieu]) => ({ a: nt[a], b: nt[b], sau: kieu === "sau" }));
  const loi = { cat_nhau: [], chu_de: [], mui_ten_cat: [], mui_ten_sat: [], goc_hep: [] };
  // hai mũi tên đi sát nhau trên một quãng dài (khó biết mũi tên nào của bước nào)
  const mt = d.mt || [];
  const diemMT = (m, w) => ({ x: (1 - w) ** 2 * m.tu[0] + 2 * w * (1 - w) * m.qua[0] + w * w * m.den[0], y: (1 - w) ** 2 * m.tu[1] + 2 * w * (1 - w) * m.qua[1] + w * w * m.den[1] });
  for (let i = 0; i < mt.length; i++) for (let j = i + 1; j < mt.length; j++) {
    let sat = 0;
    for (let k = 3; k <= 17; k++) {
      const p = diemMT(mt[i], k / 20);
      if (Array.from({ length: 21 }, (_, q) => diemMT(mt[j], q / 20)).some(r => kc(p, r) < .18)) sat++;
    }
    if (sat >= 4) loi.mui_ten_sat.push(`mũi tên ${i + 1} sát mũi tên ${j + 1}`);
  }
  // nét cắt nhau
  for (let i = 0; i < lk.length; i++) for (let j = i + 1; j < lk.length; j++) {
    const u = lk[i], v = lk[j];
    if ([u.a, u.b].some(x => x === v.a || x === v.b) || u.sau || v.sau) continue;
    if (giaoDoan(u.a, u.b, v.a, v.b)) loi.cat_nhau.push(`${u.a.id}–${u.b.id} × ${v.a.id}–${v.b.id}`);
  }
  // chữ đè nét / chữ đè chữ
  const coChu = Object.values(nt).filter(a => a.t);
  for (const a of coChu) {
    const h = hopChu(a);
    for (const l of lk) {
      if (l.a === a || l.b === a) continue;
      for (let k = 1; k < 20; k++) {
        const p = { x: l.a.x + (l.b.x - l.a.x) * k / 20, y: l.a.y + (l.b.y - l.a.y) * k / 20 };
        if (trongHop(p, h)) { loi.chu_de.push(`"${a.t}" đè nét ${l.a.id}–${l.b.id}`); break; }
      }
    }
    for (const b of coChu) if (b.id > a.id) {
      const g = hopChu(b);
      if (h.x0 < g.x1 && g.x0 < h.x1 && h.y0 < g.y1 && g.y0 < h.y1) loi.chu_de.push(`"${a.t}" đè "${b.t}"`);
    }
  }
  // mũi tên cắt ngang liên kết / đè chữ (bỏ qua đoạn trong 0,4 đơn vị quanh hai đầu mũi tên)
  (d.mt || []).forEach((m, im) => {
    const tu = { x: m.tu[0], y: m.tu[1] }, den = { x: m.den[0], y: m.den[1] };
    const diem = w => ({ x: (1 - w) ** 2 * m.tu[0] + 2 * w * (1 - w) * m.qua[0] + w * w * m.den[0], y: (1 - w) ** 2 * m.tu[1] + 2 * w * (1 - w) * m.qua[1] + w * w * m.den[1] });
    let truoc = diem(0);
    for (let k = 1; k <= 30; k++) {
      const p = diem(k / 30);
      for (const l of lk) {
        const g = giaoDoan(truoc, p, l.a, l.b);
        if (g && kc(g, tu) > .4 && kc(g, den) > .4) loi.mui_ten_cat.push(`mũi tên ${im + 1} cắt ${l.a.id}–${l.b.id}`);
      }
      for (const a of coChu) if (trongHop(p, hopChu(a)) && kc(p, tu) > .4 && kc(p, den) > .4) loi.mui_ten_cat.push(`mũi tên ${im + 1} đè "${a.t}"`);
      truoc = p;
    }
  });
  // góc hẹp giữa hai liên kết tại một nguyên tử (< 14°)
  for (const a of Object.values(nt)) {
    const huong = lk.filter(l => l.a === a || l.b === a).map(l => { const o = l.a === a ? l.b : l.a; return Math.atan2(o.y - a.y, o.x - a.x); });
    for (let i = 0; i < huong.length; i++) for (let j = i + 1; j < huong.length; j++) {
      let g = Math.abs(huong[i] - huong[j]) * 180 / Math.PI; if (g > 180) g = 360 - g;
      if (g < 14) loi.goc_hep.push(`góc ${g.toFixed(0)}° tại ${a.id}`);
    }
  }
  for (const k in loi) loi[k] = [...new Set(loi[k])];
  return loi;
}
