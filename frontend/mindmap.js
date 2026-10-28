// Sơ đồ tư duy toàn hệ thống (/admin/mindmap): đường nối bằng SVG (bezier + quầng sáng), các nút bằng HTML – không thư viện.
// Dữ liệu: GET /admin/api/so-do (config/so_do_he_thong.json + tên mô hình thật + số lần gọi hôm nay), trạng thái: GET /admin/api/so-do/suc-khoe.
// Kéo để di chuyển · cuộn / chụm 2 ngón để zoom · bấm nút để mở / thu nhánh (nhánh mọc ra vẽ dần) · rê chuột xem chi tiết.
(() => {
  const $ = (s) => document.querySelector(s);
  const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
  const NS = "http://www.w3.org/2000/svg";
  const GIAM = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const KHE = 14, KHE_HE_THONG = 46, THOI_GIAN = 450;
  const VAI = { chinh: "Chính", du_phong_1: "Dự phòng 1", du_phong_2: "Dự phòng 2", khong_dung: "Bỏ qua", co_dieu_kien: "Chính · khi có quyền Claude", du_phong: "Dự phòng" };
  const TT = { ok: "Hoạt động", loi: "Lỗi", tat: "Chưa cấu hình", dang: "Đang kiểm tra…" };
  const SVG = (d, w = 1.8) => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="${w}" stroke-linecap="round" stroke-linejoin="round">${d}</svg>`;
  const ICON = {
    goc: SVG('<path d="M12 2.5l8.2 4.75v9.5L12 21.5l-8.2-4.75v-9.5z"/><path d="M12 6.6l4.7 2.7M7.3 14.7l4.7 2.7M7.3 9.3v5.4" stroke-width="1.2"/>', 1.6),
    chuoi: SVG('<circle cx="4.5" cy="12" r="2.3"/><circle cx="19.5" cy="12" r="2.3"/><path d="M7 12h9.5M13.5 9l3 3-3 3"/>'),
    khoi: SVG('<path d="M12 2.8 20 7.4v9.2l-8 4.6-8-4.6V7.4z"/><path d="M4 7.4l8 4.6 8-4.6M12 12v9.2"/>'),
    co_che: SVG('<path d="M4 18c2.5-7.5 9-11.5 16-9.5"/><path d="M16.5 5l3.5 3.5-4.2 2.7"/><circle cx="4.6" cy="19.4" r="1.5"/>'),
    ha_tang: SVG('<path d="M12 3 2.5 8 12 13l9.5-5z"/><path d="M2.5 12.5 12 17.5l9.5-5M2.5 16.5 12 21.5l9.5-5"/>'),
    notebooklm: SVG('<path d="M4 19.5v-15A2.5 2.5 0 0 1 6.5 2H20v20H6.5a2.5 2.5 0 0 1-2.5-2.5Z"/><path d="M6 6h10M6 10h10M6 14h6"/>'),
    notebook: SVG('<path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"/><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"/>'),
    pdf: SVG('<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/>'),
    web: SVG('<circle cx="12" cy="12" r="10"/><line x1="2" y1="12" x2="22" y2="12"/><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"/>'),
    youtube: SVG('<path d="M22.54 6.42a2.78 2.78 0 0 0-1.94-2C18.88 4 12 4 12 4s-6.88 0-8.6.46a2.78 2.78 0 0 0-1.94 2A29 29 0 0 0 1 11.75a29 29 0 0 0 .46 5.33A2.78 2.78 0 0 0 3.4 19c1.72.46 8.6.46 8.6.46s6.88 0 8.6-.46a2.78 2.78 0 0 0 1.94-2 29 29 0 0 0 .46-5.25 29 29 0 0 0-.46-5.33z"/><polygon points="9.75 15.02 15.5 11.75 9.75 8.48 9.75 15.02"/>'),
    note: SVG('<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="12" y1="18" x2="12" y2="12"/><line x1="9" y1="15" x2="15" y2="15"/>'),
    text: SVG('<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/>'),
  };

  const khung = $("#khung"), san = $("#san"), lopNut = $("#lop-nut"), lopCanh = $("#lop-canh"), lopFb = $("#lop-fb"), goiY = $("#goi-y");
  let duLieu = null, sucKhoe = null, goc = null, cot = [0, 0, 0, 0];
  const nodes = new Map();
  const view = { x: 0, y: 0, k: 1 };

  // ------------------------------------------------------------ cây dữ liệu
  function tao(du, cha, cap, phia) {
    const n = { id: du.id || "goc", du, cha, cap, phia, con: [], mo: true, hien: false, el: null, canh: null, x: 0, y: 0, w: 0, h: 0 };
    nodes.set(n.id, n);
    (cap === 0 ? du.he_thong : du.con || []).forEach((c) => n.con.push(tao(c, n, cap + 1, cap === 0 ? c.phia || "phai" : phia)));
    return n;
  }
  const heThong = (n) => { while (n && n.cap > 1) n = n.cha; return n; };
  const mau = (n) => (n.du.mau || (n.cap === 0 ? "#63d4a1" : heThong(n).du.mau || "#4285F4"));
  const hienThi = (n) => !n.cha || (n.cha.mo && hienThi(n.cha));

  // ------------------------------------------------------------ vẽ một nút
  function noiDung(n) {
    const du = n.du;
    if (n.cap === 0) {
      return `${ICON.goc}<div><b>${esc(duLieu.ten)}</b><small>${duLieu.tong_goi_hom_nay} lượt gọi AI hôm nay</small></div>`;
    }
    if (n.cap === 1) {
      const bieuTuong = ICON[du.bieu_tuong] || ICON[du.id] || "";
      const soHien = du.id === "notebooklm" ? (du.con ? du.con.length : 0) : (du.goi_hom_nay || 0);
      const tieuDeSo = du.id === "notebooklm" ? "Số notebook" : "Lượt gọi AI hôm nay";
      return `${bieuTuong}<b>${esc(du.ten)}</b><span class="so" title="${tieuDeSo}">${soHien}</span>`;
    }
    if (du.is_account) {
      const avt = du.picture ? `<img src="${esc(du.picture)}" style="width:20px;height:20px;border-radius:50%;object-fit:cover;">` : `<i class="cham ok" style="background:${du.mau || '#4285F4'}"></i>`;
      return `${avt}<div><div class="ten">${esc(du.ten)}</div><small class="dk">${esc(du.email || '')}</small></div><i class="cham ${du.status === 'dang_hoat_dong' ? 'ok' : 'loi'}"></i>`;
    }
    if (du.is_notebook) {
      return `${ICON.notebook}<div><b>${esc(du.ten)}</b><small>${du.so_nguon || 0} nguồn · ${du.so_note || 0} note</small></div><span class="the-ko" style="background:rgba(66,133,244,0.15);color:#93C5FD;border-color:rgba(66,133,244,0.3)">Notebook</span>`;
    }
    if (du.is_source || du.is_note) {
      const ic = ICON[du.bieu_tuong] || ICON.text;
      return `${ic}<div><span style="font-size:12px;font-weight:500;">${esc(du.ten)}</span><small class="dk">${esc(du.loai_nguon || 'tài liệu')}</small></div>`;
    }
    if (du.ai) {
      const ncc = duLieu.nha_cung_cap[du.ai];
      const logo = `<span class="logo" style="--mau-hang:${ncc.mau};--logo:url('/static/logos/${ncc.logo}')" title="${esc(ncc.ten)}"><i></i></span>`;
      const goi = du.goi_hom_nay ? `<span class="goi">· ${du.goi_hom_nay} lượt</span>` : "";
      if (du.nha_cung_cap_node) { // nhà cung cấp theo thứ tự router: Chính / Dự phòng 1 / Dự phòng 2
        const mh = du.models.length ? `${du.models.length} mô hình` : ncc.co_key ? "không dùng ở mục này" : "chưa cấu hình";
        return `${logo}<div><div class="ten-ncc">${esc(du.ten)}${goi}</div>
          <span class="vai ${du.vai_tro}">${VAI[du.vai_tro] || ""}</span><small class="dk">${esc(mh)}</small></div><i class="cham dang"></i>`;
      }
      const ten = du.models.length > 1 ? `${du.models.length} mô hình dự phòng` : du.models[0] || "(chưa đặt trong .env)";
      return `${logo}<div><div class="ten">${esc(ten)}${goi}</div>${du.nhan ? `<small class="dk">${esc(du.nhan)}</small>` : ""}
        ${du.vai_tro ? `<span class="vai ${du.vai_tro}">${VAI[du.vai_tro] || ""}</span>` : ""}</div><i class="cham dang"></i>`;
    }
    if (du.khong_ai) return `<div><b>${esc(du.ten)}</b></div><span class="the-ko">Không dùng AI</span>`;
    return `<i class="cham" style="background:${mau(n)};margin:0"></i><div><b>${esc(du.ten)}</b><small>${esc(du.mo_ta || "")}</small></div>`;
  }

  function veNut(n) {
    const el = document.createElement("div");
    el.className = "nut " + (n.cap === 0 ? "goc" : n.cap === 1 ? "cap1" : n.du.nha_cung_cap_node ? "ai ncc" : n.du.ai ? "ai mo-hinh"
      : n.du.khong_ai ? "khong-ai" : "nhanh");
    el.dataset.id = n.id;
    el.style.setProperty("--mau", mau(n));
    el.innerHTML = noiDung(n) + (n.con.length && n.cap > 0 ? `<span class="dem-con"></span>` : "");
    el.style.visibility = "hidden";
    lopNut.append(el);
    n.el = el;
    n.w = el.offsetWidth;
    n.h = el.offsetHeight;
  }
  function capNhatDauMo(n) {
    const d = n.el.querySelector(".dem-con");
    if (d) d.textContent = n.mo ? "−" : `+${n.con.length}`;
    n.el.classList.toggle("thu-gon", !n.mo);
  }

  // ------------------------------------------------------------ bố cục: trái / phải, cây con xếp dọc, cột theo cấp
  function caoCay(n) {
    const con = n.mo ? n.con : [];
    if (!con.length) return n.h;
    return Math.max(n.h, con.reduce((t, c) => t + caoCay(c), 0) + KHE * (con.length - 1));
  }
  function datCho(n, y) {
    n.y = y;
    n.x = n.cap === 0 ? 0 : (n.phia === "trai" ? -1 : 1) * (cot[n.cap] + n.w / 2);
    if (!n.mo || !n.con.length) return;
    let yy = y - (n.con.reduce((t, c) => t + caoCay(c), 0) + KHE * (n.con.length - 1)) / 2;
    n.con.forEach((c) => { const h = caoCay(c); datCho(c, yy + h / 2); yy += h + KHE; });
  }
  function bocCuc() {
    const rong = [0, 0, 0, 0];
    nodes.forEach((n) => { rong[n.cap] = Math.max(rong[n.cap], n.w); });
    cot = [0, goc.w / 2 + 96, 0, 0];
    cot[2] = cot[1] + rong[1] + 110;
    cot[3] = cot[2] + rong[2] + 100;
    datCho(goc, 0);
    if (!goc.mo) return;
    for (const phia of ["trai", "phai"]) {
      const ds = goc.con.filter((c) => c.phia === phia);
      let y = -(ds.reduce((t, c) => t + caoCay(c), 0) + KHE_HE_THONG * (ds.length - 1)) / 2;
      ds.forEach((c) => { const h = caoCay(c); datCho(c, y + h / 2); y += h + KHE_HE_THONG; });
    }
  }

  // ------------------------------------------------------------ đường nối
  const benNgoai = (n, phia) => n.x + (phia === "trai" ? -1 : 1) * n.w / 2;   // mép phía xa tâm
  const benTrong = (n, phia) => n.x - (phia === "trai" ? -1 : 1) * n.w / 2;
  function duongCong(cha, con, p) {
    const x1 = benNgoai(cha, con.phia), y1 = cha.y, x2 = benTrong(con, con.phia), y2 = con.y;
    const dx = (x2 - x1) * 0.55;
    return `M${x1} ${y1}C${x1 + dx} ${y1} ${x2 - dx} ${y2} ${x2} ${y2}`;
  }
  function taoCanh(n) {
    const g = document.createElementNS(NS, "g");
    const sang = document.createElementNS(NS, "path"), net = document.createElementNS(NS, "path");
    sang.setAttribute("class", "duong-sang");
    net.setAttribute("class", "duong");
    [sang, net].forEach((p) => { p.setAttribute("stroke", mau(n)); p.setAttribute("pathLength", "1"); });
    g.append(sang, net);
    lopCanh.append(g);
    n.canh = g;
  }
  function veCanh(n) { if (n.canh) n.canh.querySelectorAll("path").forEach((p) => p.setAttribute("d", duongCong(n.cha, n))); }

  function veFallback() { // nét đứt có mũi tên: AI chính → AI dự phòng (cung phồng ra phía ngoài); mỗi đích chỉ một nhãn "fallback"
    lopFb.textContent = "";
    const ganNhat = new Map(); // đích → nguồn gần nhất (đặt nhãn ở cung ngắn nhất cho đỡ chồng chữ)
    nodes.forEach((n) => (n.du.fallback || []).forEach((dich) => {
      const t = nodes.get(dich);
      if (t && n.hien && t.hien && (!ganNhat.has(dich) || Math.abs(t.y - n.y) < Math.abs(t.y - ganNhat.get(dich).y))) ganNhat.set(dich, n);
    }));
    nodes.forEach((n) => {
      (n.du.fallback || []).forEach((dich) => {
        const t = nodes.get(dich);
        if (!t || !n.hien || !t.hien) return;
        const s = n.phia === "trai" ? -1 : 1;
        const x1 = benNgoai(n, n.phia), x2 = benNgoai(t, t.phia) + s * 4;
        const phong = s * (46 + Math.abs(t.y - n.y) * 0.12);
        const p = document.createElementNS(NS, "path");
        p.setAttribute("d", `M${x1} ${n.y}C${x1 + phong} ${n.y} ${x2 + phong} ${t.y} ${x2} ${t.y}`);
        p.setAttribute("class", "duong-fb");
        p.setAttribute("stroke", "#9A9AAE");
        p.setAttribute("marker-end", "url(#mui-ten)");
        const chu = document.createElementNS(NS, "text");
        chu.setAttribute("class", "nhan-fb");
        chu.setAttribute("x", Math.max(Math.abs(x1), Math.abs(x2)) * s + phong * 0.78);
        chu.setAttribute("y", (n.y + t.y) / 2 + 3);
        chu.setAttribute("text-anchor", "middle");
        chu.textContent = "fallback";
        lopFb.append(p);
        if (ganNhat.get(dich) === n) lopFb.append(chu);
      });
    });
  }

  // ------------------------------------------------------------ cập nhật (mở / thu nhánh) có animation
  function datViTri(n, x, y) { n.el.style.transform = `translate(${x - n.w / 2}px, ${y - n.h / 2}px)`; }
  function capNhat({ ban_dau = false, tu = null } = {}) { // tu: nút vừa bấm mở / thu
    const cu = new Map();
    nodes.forEach((n) => { if (n.hien) cu.set(n.id, { x: n.x, y: n.y }); });
    bocCuc();
    const den = [], an = [];
    nodes.forEach((n) => {
      const hien = hienThi(n);
      if (hien && !n.hien) den.push(n);
      if (!hien && n.hien) an.push(n);
      n.hien = hien;
      if (n.el) capNhatDauMo(n);
    });
    // nút mới hiện: đặt thẳng ở vị trí cuối, mờ dần vào + đường nối vẽ dần (cấp sâu hơn trễ hơn một chút)
    den.forEach((n) => {
      n.el.style.visibility = "visible";
      datViTri(n, n.x, n.y);
      if (n.cha) { if (!n.canh) taoCanh(n); n.canh.style.display = ""; veCanh(n); }
      const tre = GIAM ? 0 : ban_dau ? n.cap * 260 : Math.max(0, n.cap - (tu ? tu.cap : n.cap - 1) - 1) * 160;
      if (!GIAM) {
        n.el.animate([{ opacity: 0, scale: "0.85" }, { opacity: 1, scale: "1" }], { duration: 420, delay: tre + 220, easing: "cubic-bezier(.2,.8,.2,1)", fill: "backwards" });
        n.canh?.querySelectorAll("path").forEach((p) => p.animate([{ strokeDasharray: "1 1", strokeDashoffset: 1 }, { strokeDasharray: "1 1", strokeDashoffset: 0 }],
          { duration: 520, delay: tre, easing: "cubic-bezier(.4,0,.2,1)", fill: "backwards" }));
      }
    });
    // nút bị thu: mờ đi rồi ẩn
    an.forEach((n) => {
      const xong = () => { if (!n.hien) { n.el.style.visibility = "hidden"; if (n.canh) n.canh.style.display = "none"; } };
      if (GIAM) { xong(); return; }
      n.el.animate([{ opacity: 1 }, { opacity: 0 }], { duration: 200 }).finished.then(xong);
      n.canh?.animate([{ opacity: 1 }, { opacity: 0 }], { duration: 200 });
    });
    // nút đang hiện: trượt mượt tới chỗ mới, đường nối đi theo từng khung hình
    const dichuyen = [...nodes.values()].filter((n) => n.hien && cu.has(n.id));
    veFallback();
    if (GIAM || !dichuyen.some((n) => cu.get(n.id).x !== n.x || cu.get(n.id).y !== n.y)) {
      dichuyen.forEach((n) => { datViTri(n, n.x, n.y); if (n.cha) veCanh(n); });
      return;
    }
    lopFb.style.opacity = "0";
    const t0 = performance.now();
    const muc = new Map(dichuyen.map((n) => [n.id, { x: n.x, y: n.y }]));
    const buoc = (t) => {
      const p = Math.min(1, (t - t0) / THOI_GIAN), e = 1 - Math.pow(1 - p, 3);
      dichuyen.forEach((n) => {
        const a = cu.get(n.id), b = muc.get(n.id);
        n.x = a.x + (b.x - a.x) * e; n.y = a.y + (b.y - a.y) * e;
        datViTri(n, n.x, n.y);
      });
      nodes.forEach((n) => { if (n.hien && n.cha) veCanh(n); });
      if (p < 1) requestAnimationFrame(buoc);
      else { veFallback(); lopFb.style.opacity = "1"; }
    };
    requestAnimationFrame(buoc);
  }

  // ------------------------------------------------------------ trạng thái (chấm xanh / đỏ)
  function trangThaiNut(n) {
    if (!n.du.models.length) { // chưa cấu hình (vd ChatGPT chưa có key / model) hoặc không dùng ở mục này
      const ncc = duLieu.nha_cung_cap[n.du.ai];
      return { tt: "tat", ds: [{ m: "", trang_thai: "tat", chi_tiet: ncc.co_key ? "Không dùng ở mục này" : ncc.thieu }] };
    }
    if (!sucKhoe) return { tt: "dang", ds: [] };
    const ds = n.du.models.map((m) => ({ m, ...(sucKhoe.mo_hinh[`${n.du.ai}:${m}`] || { trang_thai: "tat", chi_tiet: "Chưa kiểm tra" }) }));
    const tt = ds.some((x) => x.trang_thai === "ok") ? "ok" : ds.some((x) => x.trang_thai === "loi") ? "loi" : "tat";
    return { tt, ds };
  }
  function veTrangThai() {
    let ok = 0, loi = 0;
    nodes.forEach((n) => {
      if (!n.du.ai || !n.el) return;
      const { tt } = trangThaiNut(n);
      n.el.querySelector(".cham").className = "cham " + tt;
      if (tt === "ok") ok += 1;
      if (tt === "loi") loi += 1;
    });
    $("#trang-thai").textContent = `Dữ liệu ${duLieu.cap_nhat_luc} · ${duLieu.tong_goi_hom_nay} lượt gọi AI hôm nay`
      + (sucKhoe ? ` · trạng thái lúc ${sucKhoe.luc}: ${ok} nút AI hoạt động, ${loi} lỗi` : " · đang kiểm tra trạng thái…");
  }
  async function taiSucKhoe(lamMoi = false) {
    sucKhoe = null;
    veTrangThai();
    try { sucKhoe = await lay(`/admin/api/so-do/suc-khoe${lamMoi ? "?lam_moi=1" : ""}`); } catch (e) { sucKhoe = { luc: "lỗi", mo_hinh: {} }; }
    veTrangThai();
  }

  // ------------------------------------------------------------ gợi ý khi rê chuột
  function chiTiet(n) {
    const du = n.du, dong = (t, v) => (v || v === 0 ? `<dt>${t}</dt><dd>${v}</dd>` : "");
    if (n.cap === 0) {
      return `<h3>${esc(duLieu.ten)}</h3><dl>${dong("Mô tả", esc(duLieu.mo_ta))}${dong("Hôm nay", `${duLieu.tong_goi_hom_nay} lượt gọi AI`)}</dl>`;
    }
    if (du.ai) {
      const ncc = duLieu.nha_cung_cap[du.ai], { tt, ds } = trangThaiNut(n);
      if (du.nha_cung_cap_node) {
        const ke = du.fallback[0] && nodes.get(du.fallback[0]);
        return `<h3>${esc(ncc.ten)} · ${esc(VAI[du.vai_tro])}</h3><dl>
          ${dong("Thứ tự", "theo backend/ai/routing.json + /admin → Thứ tự AI")}${dong("Khi nào", esc(du.dieu_kien))}
          ${dong("Mô hình", du.models.map((m) => `<code>${esc(m)}</code>`).join("<br>") || esc(ncc.thieu || "không dùng ở mục này"))}
          ${dong("Endpoint", esc(ncc.endpoint))}
          ${dong("Hôm nay", `${du.goi_hom_nay || 0} lượt gọi${du.loi_hom_nay ? ` · <span style="color:var(--loi)">${du.loi_hom_nay} lỗi</span>` : ""}`)}
          ${dong("Trạng thái", `${TT[tt]}${ds.map((x) => `<br><small>${x.m ? esc(x.m) + ": " : ""}${esc(x.chi_tiet)}${x.lan_cuoi ? ` (gọi lúc ${x.lan_cuoi})` : ""}</small>`).join("")}`)}
          ${du.vai_tro === "khong_dung" ? "" : dong("Lỗi / hết hạn mức / quá thời gian →", ke ? esc(ke.du.ten) : "hết AI dự phòng – báo lỗi cho người dùng")}</dl>`;
      }
      return `<h3>${esc(ncc.ten)}${du.nhan ? ` · ${esc(du.nhan)}` : ""}</h3><dl>
        ${dong("Mô hình", du.models.map((m) => `<code>${esc(m)}</code>`).join("<br>") || "(chưa đặt)")}
        ${dong("Cấu hình", `<code>${du.model_tu === "gemini_du_phong" ? "GEMINI_MODEL_DU_PHONG" : esc(du.model_tu)}</code> (.env / config.py)`)}
        ${dong("Dùng cho", esc(du.dung_cho))}${dong("Endpoint", esc(ncc.endpoint))}
        ${dong("Hôm nay", `${du.goi_hom_nay || 0} lượt gọi${du.loi_hom_nay ? ` · <span style="color:var(--loi)">${du.loi_hom_nay} lỗi</span>` : ""}`)}
        ${dong("Trạng thái", `${TT[tt]}${ds.map((x) => `<br><small>${x.m ? esc(x.m) + ": " : ""}${esc(x.chi_tiet)}${x.lan_cuoi ? ` (gọi lúc ${x.lan_cuoi})` : ""}</small>`).join("")}`)}</dl>`;
    }
    if (du.khong_ai) return `<h3>${esc(du.ten)}</h3><dl>${dong("AI", "Không dùng AI")}${dong("Làm gì", esc(du.dung_cho))}</dl>`;
    return `<h3>${esc(du.ten)}</h3><dl>${dong("Mô tả", esc(du.mo_ta))}${dong("Endpoint", esc(du.endpoint))}${dong("Code", `<code>${esc(du.code)}</code>`)}
      ${dong("Hôm nay", du.goi_hom_nay !== undefined ? `${du.goi_hom_nay} lượt gọi AI${du.loi_hom_nay ? ` · ${du.loi_hom_nay} lỗi` : ""}` : "")}
      ${dong("Nhánh con", n.con.length ? `${n.con.length} – bấm để ${n.mo ? "thu gọn" : "mở"}` : "")}</dl>`;
  }
  function hienGoiY(n, e) {
    goiY.innerHTML = chiTiet(n);
    goiY.classList.add("hien");
    const r = goiY.getBoundingClientRect();
    goiY.style.left = `${Math.min(e.clientX + 16, innerWidth - r.width - 10)}px`;
    goiY.style.top = `${Math.min(e.clientY + 16, innerHeight - r.height - 10)}px`;
  }

  // ------------------------------------------------------------ kéo / zoom / bấm
  function apDung() {
    san.style.transform = `translate(${view.x}px, ${view.y}px) scale(${view.k})`;
    khung.style.backgroundPosition = `${view.x}px ${view.y}px`;
    khung.style.backgroundSize = `${24 * view.k}px ${24 * view.k}px`;
    $("#ti-le").textContent = `${Math.round(view.k * 100)}%`;
  }
  function zoom(heSo, cx = innerWidth / 2, cy = innerHeight / 2) {
    const k = Math.min(2.5, Math.max(0.25, view.k * heSo));
    view.x = cx - (cx - view.x) * (k / view.k);
    view.y = cy - (cy - view.y) * (k / view.k);
    view.k = k;
    apDung();
  }
  function vuaKhung() {
    let ds = [...nodes.values()].filter((n) => n.hien);
    if (!ds.length) ds = [...nodes.values()];
    const x0 = Math.min(...ds.map((n) => n.x - n.w / 2)) - 70, x1 = Math.max(...ds.map((n) => n.x + n.w / 2)) + 70;
    const y0 = Math.min(...ds.map((n) => n.y - n.h / 2)), y1 = Math.max(...ds.map((n) => n.y + n.h / 2));
    const tren = innerWidth > 640 ? 150 : 70;
    view.k = Math.min(1.1, Math.max(0.25, Math.min(innerWidth / (x1 - x0), (innerHeight - tren - 60) / (y1 - y0))));
    view.x = innerWidth / 2 - ((x0 + x1) / 2) * view.k;
    view.y = tren + (innerHeight - tren - 50) / 2 - ((y0 + y1) / 2) * view.k;
    apDung();
  }
  function ganTuongTac() {
    const diem = new Map();
    let keo = null, chum = null;
    khung.addEventListener("pointerdown", (e) => {
      khung.setPointerCapture(e.pointerId);
      diem.set(e.pointerId, { x: e.clientX, y: e.clientY });
      if (diem.size === 1) keo = { x: e.clientX, y: e.clientY, vx: view.x, vy: view.y, di: 0, nut: e.target.closest(".nut") };
      if (diem.size === 2) { const [a, b] = [...diem.values()]; chum = { d: Math.hypot(a.x - b.x, a.y - b.y), k: view.k }; keo = null; }
    });
    khung.addEventListener("pointermove", (e) => {
      if (!diem.has(e.pointerId)) {
        const nut = e.target.closest(".nut");
        if (nut) hienGoiY(nodes.get(nut.dataset.id), e); else goiY.classList.remove("hien");
        return;
      }
      diem.set(e.pointerId, { x: e.clientX, y: e.clientY });
      if (chum && diem.size === 2) {
        const [a, b] = [...diem.values()];
        zoom((chum.k * Math.hypot(a.x - b.x, a.y - b.y) / chum.d) / view.k, (a.x + b.x) / 2, (a.y + b.y) / 2);
      } else if (keo) {
        keo.di = Math.max(keo.di, Math.hypot(e.clientX - keo.x, e.clientY - keo.y));
        if (keo.di > 4) {
          khung.classList.add("dang-keo");
          goiY.classList.remove("hien");
          view.x = keo.vx + e.clientX - keo.x; view.y = keo.vy + e.clientY - keo.y;
          apDung();
        }
      }
    });
    const tha = (e) => {
      diem.delete(e.pointerId);
      khung.classList.remove("dang-keo");
      if (keo && keo.di <= 4 && keo.nut) bam(nodes.get(keo.nut.dataset.id));
      if (diem.size < 2) chum = null;
      keo = null;
    };
    khung.addEventListener("pointerup", tha);
    khung.addEventListener("pointercancel", tha);
    khung.addEventListener("pointerleave", () => goiY.classList.remove("hien"));
    khung.addEventListener("wheel", (e) => { e.preventDefault(); zoom(Math.exp(-e.deltaY * 0.0015), e.clientX, e.clientY); }, { passive: false });
    $("#nut-to").addEventListener("click", () => zoom(1.2));
    $("#nut-nho").addEventListener("click", () => zoom(1 / 1.2));
    $("#nut-vua").addEventListener("click", vuaKhung);
    $("#nut-mo-het").addEventListener("click", () => { nodes.forEach((n) => { n.mo = true; }); capNhat(); setTimeout(vuaKhung, THOI_GIAN + 50); });
    $("#nut-thu-het").addEventListener("click", () => { nodes.forEach((n) => { n.mo = n.cap === 0; }); capNhat(); setTimeout(vuaKhung, THOI_GIAN + 50); });
    $("#nut-lam-moi").addEventListener("click", () => taiSucKhoe(true));
  }
  function bam(n) {
    if (!n) return;
    if (n.du && n.du.is_notebook) {
      moDrawerNotebook(n.du);
    }
    if (!n.con.length) return;
    n.mo = !n.mo;
    capNhat({ tu: n });
    goiY.classList.remove("hien");
  }

  // ------------------------------------------------------------ NotebookLM Drawer & Điều khiển
  let notebookHienTai = null;
  const drawerEl = $("#nlm-drawer");

  function dongDrawer() {
    if (drawerEl) {
      drawerEl.classList.remove("mo");
      drawerEl.setAttribute("aria-hidden", "true");
    }
    notebookHienTai = null;
  }

  async function moDrawerNotebook(nb) {
    if (!drawerEl) return;
    notebookHienTai = nb;
    $("#nlm-drawer-title").textContent = nb.ten || "Notebook";
    $("#nlm-drawer-badge").textContent = `${nb.so_nguon || 0} nguồn · ${nb.so_note || 0} note`;
    $("#nlm-drawer-mota").textContent = nb.description || "Không có mô tả bổ sung cho notebook này.";
    $("#nlm-drawer-link-web").href = `https://notebooklm.google.com/notebook/${nb.notebook_id}`;
    
    // Nguồn tài liệu
    const soNguonEl = $("#nlm-drawer-so-nguon");
    const dsNguonEl = $("#nlm-drawer-ds-nguon");
    dsNguonEl.innerHTML = "<p style='color:var(--chu-3);font-size:12px;'>Đang tải danh sách nguồn…</p>";
    
    // Reset khung chat
    const chatDs = $("#nlm-chat-ds");
    chatDs.innerHTML = `<div class="chat-tin dap">Xin chào! Bạn có thể đặt câu hỏi để tôi trích xuất câu trả lời từ các tài liệu trong notebook <b>${esc(nb.ten)}</b>.</div>`;

    drawerEl.classList.add("mo");
    drawerEl.setAttribute("aria-hidden", "false");

    // Lấy chi tiết notebook từ server
    try {
      const res = await lay(`/admin/api/notebooklm/notebooks/${nb.account_id}/${nb.notebook_id}`);
      const sources = res.sources || [];
      const notes = res.notes || [];
      soNguonEl.textContent = `${sources.length} nguồn · ${notes.length} note`;
      
      let html = "";
      sources.forEach(s => {
        const loai = s.type || "text";
        const ic = ICON[loai] || ICON.text;
        const link = s.url ? `<a href="${esc(s.url)}" target="_blank" class="badge-tag" style="color:#60A5FA;background:rgba(255,255,255,0.06);text-decoration:none;">Xem ↗</a>` : "";
        html += `
          <div class="nguon-item">
            <div style="display:flex;align-items:center;gap:8px;min-width:0;">
              ${ic}
              <span style="overflow:hidden;text-overflow:ellipsis;white-space:nowrap;" title="${esc(s.title)}">${esc(s.title || "Tài liệu")}</span>
            </div>
            ${link}
          </div>
        `;
      });
      notes.forEach(n => {
        html += `
          <div class="nguon-item">
            <div style="display:flex;align-items:center;gap:8px;min-width:0;">
              ${ICON.note}
              <span style="overflow:hidden;text-overflow:ellipsis;white-space:nowrap;" title="${esc(n.title)}">${esc(n.title || "Ghi chú")}</span>
            </div>
            <span class="badge-tag" style="color:var(--chu-3);background:rgba(255,255,255,0.06);">Ghi chú</span>
          </div>
        `;
      });
      dsNguonEl.innerHTML = html || "<p style='color:var(--chu-3);font-size:12px;'>Chưa có nguồn tài liệu nào trong notebook này.</p>";
    } catch (e) {
      dsNguonEl.innerHTML = `<p style='color:var(--loi);font-size:12px;'>Không thể tải danh sách nguồn: ${esc(e.message)}</p>`;
    }
  }

  // Sự kiện gửi câu hỏi trong Drawer
  $("#nlm-chat-form")?.addEventListener("submit", async (e) => {
    e.preventDefault();
    if (!notebookHienTai) return;
    const input = $("#nlm-chat-nhap");
    const cauHoi = input.value.trim();
    if (!cauHoi) return;
    input.value = "";

    const chatDs = $("#nlm-chat-ds");
    chatDs.innerHTML += `<div class="chat-tin hoi">${esc(cauHoi)}</div>`;
    const tinDangCho = document.createElement("div");
    tinDangCho.className = "chat-tin dap";
    tinDangCho.textContent = "NotebookLM đang đọc tài liệu và trả lời…";
    chatDs.appendChild(tinDangCho);
    chatDs.scrollTop = chatDs.scrollHeight;

    const btnGui = $("#nlm-chat-gui");
    btnGui.disabled = true;

    try {
      const res = await fetch("/admin/api/notebooklm/query", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "same-origin",
        body: JSON.stringify({
          account_id: notebookHienTai.account_id,
          notebook_id: notebookHienTai.notebook_id,
          cau_hoi: cauHoi,
        }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.detail || `Lỗi ${res.status}`);
      tinDangCho.innerHTML = `<pre>${esc(data.tra_loi || "(Không có phản hồi)")}</pre>`;
    } catch (err) {
      tinDangCho.style.color = "var(--loi)";
      tinDangCho.textContent = `Lỗi: ${err.message}`;
    } finally {
      btnGui.disabled = false;
      chatDs.scrollTop = chatDs.scrollHeight;
    }
  });

  // Sự kiện nút "Dùng làm tài liệu cho AI Riêng"
  $("#nlm-drawer-btn-ai-rieng")?.addEventListener("click", async () => {
    if (!notebookHienTai) return;
    const btn = $("#nlm-drawer-btn-ai-rieng");
    btn.disabled = true;
    btn.textContent = "Đang nhập vào AI Riêng…";
    try {
      const res = await fetch("/admin/api/notebooklm/import-to-ai-rieng", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "same-origin",
        body: JSON.stringify({
          account_id: notebookHienTai.account_id,
          notebook_id: notebookHienTai.notebook_id,
          dong_bo_tu_dong: true,
        }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.detail || `Lỗi ${res.status}`);
      alert(`Đã nhập thành công ${data.so_doan || 0} đoạn từ notebook vào kho tri thức AI Riêng!\nTài liệu ID: #${data.tai_lieu_id}`);
    } catch (err) {
      alert(`Lỗi khi nhập vào AI Riêng: ${err.message}`);
    } finally {
      btn.disabled = false;
      btn.textContent = "Dùng làm tài liệu cho AI Riêng";
    }
  });

  $("#nlm-drawer-dong")?.addEventListener("click", dongDrawer);
  window.addEventListener("keydown", (e) => { if (e.key === "Escape") dongDrawer(); });

  // ------------------------------------------------------------ Đổi tài khoản NotebookLM trên Mindmap
  async function doiTaiKhoanNotebookLM(accId, tatCa = false) {
    try {
      const url = `/admin/api/notebooklm/mindmap?tat_ca=${tatCa ? '1' : '0'}${accId ? `&tai_khoan_id=${accId}` : ''}`;
      const data = await lay(url);
      const nodeNLM = nodes.get("notebooklm");
      if (!nodeNLM) return;

      // Xóa đệ quy các node con cũ
      function xoaCon(parent) {
        parent.con.forEach(c => {
          xoaCon(c);
          if (c.el) c.el.remove();
          if (c.canh) c.canh.remove();
          nodes.delete(c.id);
        });
        parent.con = [];
      }
      xoaCon(nodeNLM);

      // Cập nhật thông tin node NotebookLM
      nodeNLM.du.mo_ta = data.mo_ta;
      nodeNLM.du.status = data.status;
      nodeNLM.du.tai_khoan_hien_tai = data.tai_khoan_hien_tai;
      nodeNLM.du.che_do = data.che_do;

      // Dựng cây con mới
      (data.con || []).forEach(c => {
        nodeNLM.con.push(tao(c, nodeNLM, nodeNLM.cap + 1, nodeNLM.phia));
      });

      // Tạo phần tử DOM cho các node mới
      function taoDOMCon(n) {
        veNut(n);
        n.con.forEach(taoDOMCon);
      }
      nodeNLM.con.forEach(taoDOMCon);

      // Mở nhánh và định vị lại
      nodeNLM.mo = true;
      capNhat({ tu: nodeNLM });
      vuaKhung();
    } catch (e) {
      console.error("Lỗi khi chuyển tài khoản NotebookLM:", e);
    }
  }

  function khoiTaoThanhTaiKhoan(htNLM) {
    const thanhEl = $("#nlm-thanh-tai-khoan");
    const chonEl = $("#nlm-chon-tk");
    if (!thanhEl || !chonEl || !htNLM) return;
    const ds = htNLM.danh_sach_tai_khoan || [];
    if (!ds.length) {
      thanhEl.style.display = "none";
      return;
    }
    thanhEl.style.display = "inline-flex";
    let opts = ds.map(a => {
      const sel = (htNLM.che_do !== "tat_ca" && (htNLM.tai_khoan_hien_tai === a.id || (htNLM.cur_account && htNLM.cur_account.id === a.id))) ? "selected" : "";
      return `<option value="${a.id}" ${sel}>${esc(a.ten_goi_nho)} (${esc(a.email || 'Google')})</option>`;
    }).join("");
    const selTatCa = htNLM.che_do === "tat_ca" ? "selected" : "";
    opts += `<option value="tat_ca" ${selTatCa}>-- Hiện tất cả tài khoản --</option>`;
    chonEl.innerHTML = opts;

    chonEl.onchange = () => {
      const val = chonEl.value;
      if (val === "tat_ca") {
        doiTaiKhoanNotebookLM(null, true);
      } else {
        doiTaiKhoanNotebookLM(Number(val), false);
      }
    };
  }

  // ------------------------------------------------------------ khởi động
  async function lay(url) {
    const r = await fetch(url, { cache: "no-store", credentials: "same-origin" });
    if (r.status === 401 || r.status === 403) { location.href = "/dang-nhap?tiep=%2Fadmin%2Fmindmap"; throw new Error("Cần đăng nhập admin"); }
    if (!r.ok) throw new Error(`Lỗi ${r.status}`);
    return r.json();
  }
  async function batDau() {
    ganTuongTac();
    try { duLieu = await lay("/admin/api/so-do"); } catch (e) { $("#trang-thai").textContent = `Không tải được sơ đồ: ${e.message}`; return; }
    goc = tao(duLieu, null, 0, "");
    nodes.forEach((n) => { if (n.du.nha_cung_cap_node) n.mo = false; }); // AI thu gọn: thấy ngay chuỗi Chính ⇢ Dự phòng; bấm để xem mô hình
    if (innerWidth < 700) nodes.forEach((n) => { if (n.cap === 1) n.mo = false; }); // điện thoại: chỉ 4 nhánh chính, chạm để mở
    nodes.forEach(veNut);
    capNhat({ ban_dau: true });
    vuaKhung();
    veTrangThai();
    taiSucKhoe();

    const htNLM = (duLieu.he_thong || []).find(h => h.id === "notebooklm");
    if (htNLM) {
      khoiTaoThanhTaiKhoan(htNLM);
    }
  }
  batDau();
})();
