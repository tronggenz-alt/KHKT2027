// Phòng thí nghiệm Hoá ảo – ĐIỀU KHIỂN TRANG: danh sách thí nghiệm, bảng điều khiển, số liệu, sổ tay, câu hỏi 3 dạng, thông tin,
// bước hướng dẫn, tấm kéo lên (điện thoại), chuyển Web 3D ⇄ Unity 3D. Lõi mô phỏng (mo-phong.js) giữ trạng thái; renderer chỉ vẽ.
// Mọi chữ từ dữ liệu JSON đưa vào bằng textContent (không innerHTML) – chống chèn mã.
import { PhongLabWeb } from "ptn/ve-web";
import { chonCheDo } from "ptn/chon-che-do";
import { LabUnityApp } from "ptn/lab-unity-app";

const T = window.PTNTinhToan, M = window.PTNMoPhong;
const GOC = "/static/hoa-hoc/phong-thi-nghiem/";
const DS_TN = ["an-toan", "chuan-do", "nhan-biet-ion", "pin-galvani", "dien-phan"];
const KHOA_HUY_HIEU = "ptn-huy-hieu-nhap-mon", KHOA_CHAT_LUONG = "ptn-chat-luong";
const dd = T.dinhDang;
const $ = (s, g = document) => g.querySelector(s);
const $$ = (s, g = document) => [...g.querySelectorAll(s)];
const SVG = "http://www.w3.org/2000/svg";
const luuDoc = (k, md = null) => { try { return localStorage.getItem(k) ?? md; } catch { return md; } };
const luuGhi = (k, v) => { try { localStorage.setItem(k, v); } catch { /* bỏ qua */ } };

/** Tạo phần tử: el("button", { class, text, on: { click }, aria-*, data-*, ... }, ...con). Chữ luôn qua textContent. */
function el(tag, tt = {}, ...con) {
  const e = document.createElement(tag);
  for (const [k, v] of Object.entries(tt)) {
    if (v === null || v === undefined || v === false) continue;
    if (k === "class") e.className = v;
    else if (k === "text") e.textContent = v;
    else if (k === "on") for (const [ten, f] of Object.entries(v)) e.addEventListener(ten, f);
    else if (k === "style") Object.assign(e.style, v);
    else if (k in e && typeof v !== "string") e[k] = v;
    else e.setAttribute(k, v === true ? "" : String(v));
  }
  for (const c of con.flat()) if (c !== null && c !== undefined && c !== false) e.append(c instanceof Node ? c : String(c));
  return e;
}
function svg(tag, tt = {}, ...con) {
  const e = document.createElementNS(SVG, tag);
  for (const [k, v] of Object.entries(tt)) if (v !== null && v !== undefined) (k === "text" ? (e.textContent = v) : e.setAttribute(k, String(v)));
  for (const c of con.flat()) if (c) e.append(c);
  return e;
}
function toast(chu, loai = "ok", ms = 4200) {
  const t = el("div", { class: `toast ${loai}`, role: "status", text: chu });
  const hop = $("#toasts");
  hop.append(t);
  while (hop.childElementCount > 2) hop.firstElementChild.remove();   // tối đa 2 thông báo – không che bảng điều khiển
  setTimeout(() => t.remove(), ms);
}
const nhom = (tieuDe, ...con) => el("section", { class: "ptn-nhom" }, tieuDe ? el("h3", { class: "ptn-nhom-tieu-de", text: tieuDe }) : null, ...con);
const nut = (chu, onClick, tt = {}) => el("button", { type: "button", class: tt.class || "btn btn-sm", on: { click: onClick }, ...tt, text: chu });
const chip = (chu, onClick, tt = {}) => el("button", { type: "button", class: "ptn-chip", "aria-pressed": "false", on: { click: onClick }, ...tt, text: chu });
const oSo = (nhan, id, rong = false) => el("div", { class: `ptn-o-so${rong ? " rong" : ""}` }, el("span", { text: nhan }), el("b", { id, text: "–" }));
const datChu = (id, chu) => { const e = document.getElementById(id); if (e && e.textContent !== chu) e.textContent = chu; };
const phanHoi = (e, kq) => { if (!e || !kq) return; e.textContent = kq.thong_bao || ""; e.className = `ptn-phan-hoi ${kq.dung === false ? "sai" : kq.dung ? "dung" : ""}`; };

// ------------------------------------------------------------------ biểu tượng GHS (tự vẽ SVG, không chép ảnh)
const HINH_GHS = {
  no: '<circle cx="50" cy="56" r="13" fill="#000"/><path d="M50 27v9M50 76v9M28 56h-9M81 56h-9M34 40l-6-6M72 78l-6-6M66 40l6-6M28 78l6-6" stroke="#000" stroke-width="4" stroke-linecap="round"/>',
  de_chay: '<path d="M50 24c4 12 18 18 16 34-1 12-8 18-16 18s-16-6-16-17c0-9 6-13 8-20 2 6 6 8 8 8-2-8-3-15 0-23z" fill="#000"/><rect x="30" y="78" width="40" height="5" fill="#000"/>',
  oxi_hoa: '<circle cx="50" cy="66" r="12" fill="none" stroke="#000" stroke-width="5"/><path d="M50 22c3 9 13 13 12 25-1 8-6 11-12 11s-12-4-12-11c0-6 4-9 6-14 1 4 4 6 6 6-1-6-2-11 0-17z" fill="#000"/><rect x="30" y="80" width="40" height="4" fill="#000"/>',
  khi_nen: '<rect x="26" y="48" width="44" height="18" rx="9" fill="#000"/><rect x="70" y="52" width="8" height="10" fill="#000"/>',
  an_mon: '<path d="M30 30l10 6-8 14zM58 26l10 6-8 14z" fill="#000"/><circle cx="35" cy="56" r="2.5" fill="#000"/><circle cx="63" cy="52" r="2.5" fill="#000"/><path d="M24 70h22l4-6 4 6M58 70h22" stroke="#000" stroke-width="5" fill="none"/>',
  doc_cap: '<circle cx="50" cy="42" r="14" fill="#000"/><circle cx="45" cy="40" r="3.4" fill="#fff"/><circle cx="55" cy="40" r="3.4" fill="#fff"/><path d="M32 62l36 16M68 62L32 78" stroke="#000" stroke-width="6" stroke-linecap="round"/>',
  kich_ung: '<rect x="45" y="26" width="10" height="34" rx="3" fill="#000"/><circle cx="50" cy="71" r="6" fill="#000"/>',
  suc_khoe: '<circle cx="50" cy="30" r="9" fill="#000"/><path d="M30 80c0-18 8-34 20-34s20 16 20 34z" fill="#000"/><path d="M50 52l3 7 7 1-5 5 1 7-6-3-6 3 1-7-5-5 7-1z" fill="#fff"/>',
  moi_truong: '<path d="M34 72V40M34 48l-9-8M34 52l9-9" stroke="#000" stroke-width="4" stroke-linecap="round"/><path d="M48 66c6-8 18-8 24 0-6 8-18 8-24 0z" fill="#000"/><path d="M72 66l8-6v12z" fill="#000"/><path d="M22 78h56" stroke="#000" stroke-width="3"/>',
};
function veGHS(ma, nhanTruyCap) {
  const s = svg("svg", { viewBox: "0 0 100 100", role: "img", "aria-label": nhanTruyCap || ma });
  s.append(svg("path", { d: "M50 4L96 50 50 96 4 50z", fill: "#fff", stroke: "#dc2626", "stroke-width": 7, "stroke-linejoin": "round" }));
  const g = svg("g");
  g.innerHTML = HINH_GHS[ma] || ""; // chuỗi SVG cố định trong code (không phải dữ liệu người dùng)
  s.append(g);
  return s;
}

// ------------------------------------------------------------------ đồ thị pH – V và ô phóng to vạch burette
function veDoThi(s) {
  const W = 320, H = 196, L = 32, R = 10, Tp = 10, B = 26;
  const Vmax = Math.max(40, Math.ceil(((s.V_tuong_duong || 20) * 2) / 5) * 5);
  const x = (v) => L + ((W - L - R) * v) / Vmax, y = (p) => Tp + ((H - Tp - B) * (14 - p)) / 14;
  const g = svg("svg", { viewBox: `0 0 ${W} ${H}`, class: "ptn-do-thi", role: "img", "aria-label": `Đồ thị pH theo thể tích NaOH: ${s.diem.length} điểm đo` });
  for (let p = 0; p <= 14; p += 2) { g.append(svg("line", { class: "luoi", x1: L, x2: W - R, y1: y(p), y2: y(p) }), svg("text", { x: L - 6, y: y(p) + 3, "text-anchor": "end", text: String(p) })); }
  for (let v = 0; v <= Vmax; v += 10) g.append(svg("text", { x: x(v), y: H - 10, "text-anchor": "middle", text: String(v) }));
  g.append(svg("line", { class: "truc", x1: L, x2: L, y1: Tp, y2: H - B }), svg("line", { class: "truc", x1: L, x2: W - R, y1: H - B, y2: H - B }));
  g.append(svg("text", { x: W - R, y: H - 1, "text-anchor": "end", text: "V(NaOH), mL" }), svg("text", { x: 4, y: 8, text: "pH" }));
  if (s.V_tuong_duong && s.V_doi_mau !== null) {
    g.append(svg("line", { class: "tuong-duong", x1: x(s.V_tuong_duong), x2: x(s.V_tuong_duong), y1: Tp, y2: H - B }));
    g.append(svg("text", { x: x(s.V_tuong_duong) + 4, y: Tp + 10, text: `tđ ${dd(s.V_tuong_duong)} mL` }));
  }
  if (s.diem.length > 1) g.append(svg("polyline", { class: "duong", points: s.diem.map(([v, p]) => `${x(v).toFixed(1)},${y(p).toFixed(1)}`).join(" ") }));
  const c = s.diem[s.diem.length - 1];
  if (c) g.append(svg("circle", { class: "diem", cx: x(c[0]), cy: y(c[1]), r: 3.5 }));
  return g;
}
function veVachBuret(V) {
  const W = 200, H = 210, giua = Math.max(1, Math.min(49, V)), tu = giua - 1.5, den = giua + 1.5;
  const y = (v) => 12 + ((H - 24) * (v - tu)) / (den - tu);
  const g = svg("svg", { viewBox: `0 0 ${W} ${H}`, class: "ptn-buret-zoom", role: "img", "aria-label": "Phóng to vạch chia burette quanh mặt khum" });
  g.append(svg("rect", { x: 60, y: 0, width: 80, height: H, fill: "rgba(160,200,235,0.18)", stroke: "rgba(120,140,160,0.5)" }));
  const yM = y(V);
  g.append(svg("path", { d: `M60 ${yM - 9} Q100 ${yM + 9} 140 ${yM - 9} L140 ${H} L60 ${H} Z`, fill: "rgba(120,180,230,0.35)" }));
  g.append(svg("path", { d: `M60 ${yM - 9} Q100 ${yM + 9} 140 ${yM - 9}`, fill: "none", stroke: "#2563eb", "stroke-width": 1.6 }));
  for (let k = Math.ceil(tu * 10); k <= Math.floor(den * 10); k++) {
    const v = k / 10, yy = y(v), dai = k % 10 === 0 ? 34 : k % 5 === 0 ? 24 : 14;
    g.append(svg("line", { x1: 60, x2: 60 + dai, y1: yy, y2: yy, stroke: "#1b2b3d", "stroke-width": k % 10 === 0 ? 1.6 : 1 }));
    if (k % 10 === 0) g.append(svg("text", { x: 54, y: yy + 3, "text-anchor": "end", text: String(v) }));
  }
  g.append(svg("line", { x1: 30, x2: 170, y1: yM, y2: yM, stroke: "#f59e0b", "stroke-dasharray": "3 3" }));
  g.append(svg("text", { x: 172, y: yM + 3, text: "mắt" }));
  return g;
}

// ------------------------------------------------------------------ trạng thái trang
const U = {
  du: {}, bangIon: null, banDo: null, mp: {}, huy: {}, id: null, ve: null, web: null, unity: null,
  kieuVe: "web", chatLuong: "cao", cheDo: "huong_dan", diChuyen: "di_bo", cheDoPhong: "di_dao",
  tramDangLam: null, tramGan: null, khuHienTai: null, miniRong: false,
  panel: null, ongChon: 1, thuocThu: "NaOH", soTayMoi: 0, cauHinh: null, s: null
};

export async function batDau() {
  khoiTaoGiaoDien();
  U.chatLuong = luuDoc(KHOA_CHAT_LUONG) || (matchMedia("(max-width: 900px)").matches || (navigator.hardwareConcurrency || 8) <= 4 ? "thap" : "cao");
  const giamChuyenDong = matchMedia("(prefers-reduced-motion: reduce)").matches;
  U.diChuyen = luuDoc("ptn-di-chuyen") || (giamChuyenDong ? "den_ngay" : "di_bo");

  const doc = (p) => fetch(GOC + p, { cache: "no-cache" }).then((r) => { if (!r.ok) throw new Error(`Không tải được ${p}`); return r.json(); });
  const [bang, banDo, ...ds] = await Promise.all([
    doc("bang-ion.json"),
    doc("ban-do.json"),
    ...DS_TN.map((id) => doc(`thi-nghiem/${id}.json`))
  ]);
  U.bangIon = bang;
  U.banDo = banDo;
  ds.forEach((d) => { U.du[d.id] = d; });

  // Khởi tạo máy trạng thái cho cả 5 thí nghiệm ngay từ đầu
  const trangThaiBanDau = {};
  DS_TN.forEach((id) => {
    const tn = U.du[id];
    const mp = M.tao(tn, { bangIon: U.bangIon, cheDo: U.cheDo });
    mp.on("trang_thai", (s) => {
      U.web?.capNhat(s);
      if (U.tramDangLam === id) nhanTrangThai(s);
    });
    mp.on("so_tay", (m) => {
      if (U.tramDangLam === id) themSoTay(m);
    });
    mp.on("hieu_ung", (e) => {
      if (U.tramDangLam === id) nhanHieuUng(e);
    });
    U.mp[id] = mp;
    trangThaiBanDau[id] = mp.trangThai();
  });

  veDanhSach();
  ganTuyChon();
  ganTab();
  ganTamKeo();

  // Quyết định chế độ hiển thị: Unity WebGL vs Three.js (bản nhẹ)
  const laApp = !!(window.chemistryApp && typeof window.chemistryApp.labWebGL === "function");
  let buildLab = { coSan: false };
  let coWebGL2 = false;
  let tenGpu = "";

  if (laApp) {
    try {
      buildLab = await window.chemistryApp.labWebGL();
    } catch (e) {
      console.warn("[PTN] Lỗi kiểm tra build WebGL:", e);
    }
    try {
      const c = document.createElement("canvas");
      const gl = c.getContext("webgl2");
      coWebGL2 = !!gl;
      if (gl) {
        const ext = gl.getExtension("WEBGL_debug_renderer_info");
        if (ext) tenGpu = gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) || "";
      }
    } catch {}
  }

  const cheDoChon = chonCheDo({
    laApp,
    build: buildLab,
    coWebGL2,
    gpu: tenGpu,
    soNhan: navigator.hardwareConcurrency || 8,
    ramGB: navigator.deviceMemory || 8,
    uuTien: luuDoc("ptn-che-do-uu-tien") || "tu_dong"
  });

  const cheDo = (typeof cheDoChon === "object" && cheDoChon) ? cheDoChon.cheDo : cheDoChon;
  if (cheDo === "unity") {
    const nutBanDayDu = $("#ptn-nut-ban-day-du");
    if (nutBanDayDu) nutBanDayDu.hidden = true;

    await LabUnityApp.mo({
      duLieu: U.du,
      bangIon: U.bangIon,
      id: new URLSearchParams(location.search).get("tn"),
      cheDo: U.cheDo,
      onChuyenThreeJs: () => {
        khoiDongThreeJs(trangThaiBanDau, { laApp, buildLab });
      }
    });
    return;
  }

  await khoiDongThreeJs(trangThaiBanDau, { laApp, buildLab });
}

async function khoiDongThreeJs(trangThaiBanDau, { laApp = false, buildLab = { coSan: false } } = {}) {
  window.PhongLab = window.PhongLab || {};
  window.PhongLab.app = {
    cheDo: () => "nhe",
    unitySanSang: () => false,
    tram: () => U.tramDangLam,
    fpsTrungBinh: () => 60,
    thaoTac: (id, lenh, json) => U.mp[id]?.thaoTac(lenh, typeof json === "string" ? JSON.parse(json) : (json || {})),
    trangThai: (id) => U.mp[id]?.trangThai() || {}
  };

  const nutBanDayDu = $("#ptn-nut-ban-day-du");
  if (nutBanDayDu) {
    if (laApp && buildLab.coSan) {
      nutBanDayDu.hidden = false;
      nutBanDayDu.onclick = async () => {
        luuGhi("ptn-che-do-uu-tien", "unity");
        nutBanDayDu.hidden = true;
        if (U.web) {
          try { U.web.huy(); } catch {}
          U.web = null;
        }
        await LabUnityApp.mo({
          duLieu: U.du,
          bangIon: U.bangIon,
          id: U.tramDangLam || new URLSearchParams(location.search).get("tn"),
          cheDo: U.cheDo,
          onChuyenThreeJs: () => khoiDongThreeJs(trangThaiBanDau, { laApp, buildLab })
        });
      };
    } else {
      nutBanDayDu.hidden = true;
    }
  }

  // Khởi tạo bộ vẽ Web 3D
  U.web = new PhongLabWeb($("#ptn-khung"), { chatLuong: U.chatLuong, onThaoTac: thaoTacTuVe, onChon: chonTuVe });
  U.ve = U.web;

  const dChu = $("#ptn-dang-tai-chu");
  const tVong = $(".ptn-thanh-tai", $("#ptn-dang-tai"));
  if (tVong) tVong.hidden = false;

  await U.web.dungPhong(U.banDo, U.du, trangThaiBanDau, (td) => {
    const chuTienDo = typeof td === "string" ? td : (td?.khu ? `Đang dựng phòng thí nghiệm… ${td.khu}` : "");
    if (dChu && chuTienDo) dChu.textContent = chuTienDo;
    if (tVong?.firstElementChild && td?.tiLe) tVong.firstElementChild.style.width = `${Math.round(td.tiLe * 100)}%`;
  });

  if (U.web.nhanVat) {
    U.web.nhanVat.onDoiViTri = (x, z) => capNhatViTri(x, z);
    U.web.nhanVat.onVaoTram = (id) => vaoTram(id);
    U.web.nhanVat.onRoiTram = () => roiTram();
    U.web.nhanVat.onTramGan = (id) => capNhatTramGan(id);
    U.web.nhanVat.onDenGan = (gan) => capNhatTramGan(gan ? (gan.tram?.tn || gan.tram?.id || gan.tn || gan) : null);
    U.web.nhanVat.onKhuVuc = (khu) => capNhatKhuVuc(khu);
  }
  U.web.onDenGan = (id) => capNhatTramGan(id);
  U.web.onKhu = (khu) => capNhatKhuVuc(khu);

  khoiTaoBanDoMini();
  khoiTaoJoystick();
  khoiTaoHuongDan();

  // Ngăn kéo trái: Mở / đóng bản đồ và danh sách trạm
  const moNganKeo = (mo) => {
    const nk = $("#ptn-ngan-keo-trai");
    const lp = $("#ptn-lop-phu-trai");
    if (!nk) return;
    nk.hidden = !mo;
    if (lp) lp.hidden = !mo;
    nk.classList.toggle("mo", mo);
  };
  $("#ptn-nut-mo-ngan-keo")?.addEventListener("click", () => moNganKeo(true));
  $("#ptn-ngan-keo-dong")?.addEventListener("click", () => moNganKeo(false));
  $("#ptn-lop-phu-trai")?.addEventListener("click", () => moNganKeo(false));

  // Tương tác prompt E khi đến gần bàn
  $("#ptn-hud-tuong-tac")?.addEventListener("click", () => {
    if (U.tramGan) vaoTram(U.tramGan);
  });

  // Hộp thoại cài đặt điều khiển (⚙)
  const hopCaiDat = $("#ptn-hop-cai-dat");
  $("#ptn-nut-cai-dat")?.addEventListener("click", () => {
    if (hopCaiDat?.showModal) {
      const cd = U.web?.nhanVat?.caiDat || {};
      const slNhay = $("#ptn-cd-do-nhay");
      const txtNhay = $("#ptn-cd-do-nhay-giatri");
      if (slNhay) {
        slNhay.value = String(cd.doNhay ?? 1.0);
        if (txtNhay) txtNhay.textContent = `${Number(slNhay.value).toFixed(1)}x`;
      }
      const chkDaoY = $("#ptn-cd-dao-y");
      if (chkDaoY) chkDaoY.checked = !!cd.daoY;
      const chkTuXoay = $("#ptn-cd-tu-xoay");
      if (chkTuXoay) chkTuXoay.checked = cd.tuXoayCam !== false;
      const chkTuVao = $("#ptn-cd-tu-vao-ban");
      if (chkTuVao) chkTuVao.checked = cd.tuVaoBan !== false;
      $$("[data-goc-mac-dinh]").forEach((b) => {
        b.setAttribute("aria-checked", String(b.dataset.gocMacDinh === (cd.gocNhinMacDinh || "goc_3")));
      });
      hopCaiDat.showModal();
    }
  });

  $("#ptn-cd-do-nhay")?.addEventListener("input", (e) => {
    const val = parseFloat(e.target.value) || 1.0;
    const txt = $("#ptn-cd-do-nhay-giatri");
    if (txt) txt.textContent = `${val.toFixed(1)}x`;
    U.web?.nhanVat?.capNhatCaiDat({ doNhay: val });
  });
  $("#ptn-cd-dao-y")?.addEventListener("change", (e) => {
    U.web?.nhanVat?.capNhatCaiDat({ daoY: e.target.checked });
  });
  $("#ptn-cd-tu-xoay")?.addEventListener("change", (e) => {
    U.web?.nhanVat?.capNhatCaiDat({ tuXoayCam: e.target.checked });
  });
  $("#ptn-cd-tu-vao-ban")?.addEventListener("change", (e) => {
    U.web?.nhanVat?.capNhatCaiDat({ tuVaoBan: e.target.checked });
  });
  $$("[data-goc-mac-dinh]").forEach((b) => b.addEventListener("click", () => {
    const goc = b.dataset.gocMacDinh;
    $$("[data-goc-mac-dinh]").forEach((x) => x.setAttribute("aria-checked", String(x === b)));
    U.web?.nhanVat?.capNhatCaiDat({ gocNhinMacDinh: goc });
  }));

  // Đóng dialog khi bấm nút [data-dong]
  $$("[data-dong]").forEach((b) => b.addEventListener("click", () => {
    b.closest("dialog")?.close();
  }));

  // Hộp thoại hướng dẫn phím tắt (?)
  const hopPhimTat = $("#ptn-hop-phim-tat");
  $("#ptn-nut-tro-giup")?.addEventListener("click", () => {
    if (hopPhimTat?.showModal) hopPhimTat.showModal();
  });

  // Điều khiển camera từ thanh HUD
  $$("[data-camera]").forEach((b) => b.addEventListener("click", () => {
    const kieu = b.dataset.camera;
    if (kieu === "theo_sau" || kieu === "goc_3") {
      U.web?.nhanVat?.datGocNhin("goc_3");
    } else if (kieu === "goc_1") {
      U.web?.nhanVat?.datGocNhin("goc_1");
    } else if (kieu === "toan_canh") {
      U.web?.nhanVat?.datGocNhin("toan_canh");
    } else if (kieu === "ve_cua") {
      if (U.banDo) {
        const xp = U.banDo.xuat_phat;
        const x = xp.x ?? (Array.isArray(xp) ? xp[0] : 11);
        const z = xp.z ?? (Array.isArray(xp) ? xp[1] : 13.6);
        U.web?.nhanVat?.dichChuyen(x, z, xp.huong ?? 0);
      }
    } else {
      U.ve?.dieuKhienCamera?.(kieu);
    }
  }));

  // Khởi tạo nút Mở bằng ứng dụng máy tính (GĐ4 PROMPT_XUAT_APP_CHEMISTRY.md)
  const nutUngDung = $("#ptn-nut-ung-dung");
  if (window.chemistryApp && typeof window.chemistryApp.moAppLab === "function") {
    if (nutUngDung) {
      nutUngDung.hidden = false;
      nutUngDung.addEventListener("click", () => {
        const tnId = U.tramDangLam || (U.id || "chuan-do");
        window.chemistryApp.moAppLab(tnId);
      });
    }
  } else {
    try {
      const resUngDung = await fetch("/api/phong-thi-nghiem/ung-dung").then((r) => r.ok ? r.json() : null);
      if (resUngDung && resUngDung.bat) {
        const hopUngDung = $("#ptn-hop-ung-dung");
        const linkMac = $("#ptn-link-tai-mac");
        const linkWin = $("#ptn-link-tai-win");

        if (linkMac && resUngDung.url_mac) {
          linkMac.href = resUngDung.url_mac;
          linkMac.hidden = false;
        }
        if (linkWin && resUngDung.url_win) {
          linkWin.href = resUngDung.url_win;
          linkWin.hidden = false;
        }

        if (nutUngDung) {
          nutUngDung.hidden = false;
          nutUngDung.addEventListener("click", () => {
            const tnId = U.tramDangLam || (U.id || "chuan-do");
            const deepLink = `phonglabhoa://mo?tn=${encodeURIComponent(tnId)}`;

            let daChuyen = false;
            const onBlur = () => { daChuyen = true; };
            window.addEventListener("blur", onBlur, { once: true });

            window.location.href = deepLink;

            setTimeout(() => {
              window.removeEventListener("blur", onBlur);
              if (!daChuyen && document.visibilityState === "visible") {
                hopUngDung?.showModal?.();
              }
            }, 1500);
          });
        }
      }
    } catch (e) {
      console.debug("[PTN] Ứng dụng lab:", e);
    }
  }


  $("#ptn-nut-roi-ban-top")?.addEventListener("click", () => roiTram());
  $("#ptn-nut-roi-ban")?.addEventListener("click", () => roiTram());
  $("#ptn-nut-lam-tn")?.addEventListener("click", () => {
    if (U.tramGan) vaoTram(U.tramGan);
  });

  new MutationObserver(() => U.ve?.doiGiaoDien()).observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });

  // Xử lý liên kết thẳng ?tn=id hoặc bắt đầu ở cửa vào
  const tham = new URLSearchParams(location.search).get("tn");
  if (DS_TN.includes(tham)) {
    vaoTram(tham, { tucThi: true });
  } else {
    U.cheDoPhong = "di_dao";
    datChu("ptn-ten-tn", "Phòng thí nghiệm ảo 3D");
    datChu("ptn-phu-de", "Không gian mở 5 khu vực thực hành");
    U.chonTab("tham-quan", false);
    veThamQuan();
  }

  // Ticker: chỉ cập nhật thí nghiệm cho trạm đang làm việc
  let truoc = performance.now();
  const nhip = (bay) => {
    const dt = Math.min(0.25, (bay - truoc) / 1000);
    truoc = bay;
    if (U.tramDangLam && U.mp[U.tramDangLam]) {
      U.mp[U.tramDangLam].capNhat(dt);
    }
    requestAnimationFrame(nhip);
  };
  requestAnimationFrame(nhip);

  $("#ptn-dang-tai").classList.add("an");
  kiemTraUnity().catch(() => {});

  window.PhongLab = {
    app: {
      cheDo: () => "nhe",
      unitySanSang: () => false,
      tram: () => U.tramDangLam,
      fpsTrungBinh: () => 60,
      thaoTac: (id, lenh, json) => U.mp[id]?.thaoTac(lenh, typeof json === "string" ? JSON.parse(json) : (json || {})),
      trangThai: (id) => U.mp[id]?.trangThai() || {}
    },
    trangThai: () => (U.tramDangLam ? U.mp[U.tramDangLam]?.trangThai() : U.mp[U.id]?.trangThai()),
    thaoTac: (l, t) => (U.tramDangLam ? U.mp[U.tramDangLam]?.thaoTac(l, t) : U.mp[U.id]?.thaoTac(l, t)),
    mo: (id) => vaoTram(id, { tucThi: true }),
    get ve() { return U.ve; },
    phong: {
      cheDo: () => U.web?.cheDo,
      viTri: () => {
        const v = U.web?.nhanVat?.viTri;
        if (!v) return null;
        return { x: v.x ?? v[0], z: v.z ?? v[1] };
      },
      khu: () => {
        const BD = window.BanDoPhongLab || window.PTNBanDo;
        const v = U.web?.nhanVat?.viTri;
        return (U.banDo && v && BD) ? BD.khuTai(U.banDo, v) : null;
      },
      tramGan: () => {
        const BD = window.BanDoPhongLab || window.PTNBanDo;
        const v = U.web?.nhanVat?.viTri;
        return (U.banDo && v && BD) ? BD.tramGanNhat(U.banDo, v, 2.0) : null;
      },
      diToi: (id, tuy = {}) => diToiTram(id, tuy),
      vaoTram: (id) => vaoTram(id, { tucThi: true }),
      roiTram: () => roiTram(),
      thongKe: () => U.web?.thongKe() || {},
      camera: () => U.web?.layThongTinCamera() || {},
      tramTrongKhung: (id) => U.web?.tramTrongKhung(id) || { ti_le: 0, tam_la_canvas: false },
      denGan: (id, { khoangCach = 1.0 } = {}) => {
        const tr = U.banDo?.tram?.find((t) => t.id === id || t.tn === id);
        if (!tr) return;
        const tam = tr.tam;
        const dx = (tr.diem_dung[0] - tam[0]);
        const dz = (tr.diem_dung[1] - tam[1]);
        const len = Math.hypot(dx, dz) || 1;
        const x = tam[0] + (dx / len) * khoangCach;
        const z = tam[1] + (dz / len) * khoangCach;
        U.web?.nhanVat?.dichChuyen(x, z, tr.huong ?? 0);
      },
      batNhaBupBe: (bat = true) => {
        U.web?.nhanVat?.chuyenCheDoTongQuan(bat);
      },
      doiGocNhin: (goc = "ngoi_ba") => {
        U.web?.nhanVat?.datGocNhin(goc === "ngoi_nhat" ? "goc_1" : "goc_3");
      }
    }
  };
}

function khoiTaoGiaoDien() {
  const nutCheDo = $("#nut-che-do");
  const dat = (t, luu) => {
    document.documentElement.dataset.theme = t;
    const sangToi = t === "toi" ? "sáng" : "tối";
    nutCheDo.setAttribute("aria-label", `Chuyển sang chế độ ${sangToi}`);
    nutCheDo.title = `Chế độ ${sangToi}`;
    if (luu) luuGhi("giai-chuoi-giao-dien", t);
  };
  dat(document.documentElement.dataset.theme === "toi" ? "toi" : "sang", false);
  nutCheDo.addEventListener("click", () => dat(document.documentElement.dataset.theme === "toi" ? "sang" : "toi", true));
}

function veDanhSach() {
  const ds = $("#ptn-ds");
  if (!ds || !U.banDo) return;

  const dsKhu = U.banDo.khu || [];
  const tramTheoKhu = {};
  dsKhu.forEach((k) => { tramTheoKhu[k.id] = []; });
  (U.banDo.tram || []).forEach((tr) => {
    if (tramTheoKhu[tr.khu]) tramTheoKhu[tr.khu].push(tr);
  });

  ds.replaceChildren(
    ...dsKhu.map((khu) => {
      const dsTram = tramTheoKhu[khu.id] || [];
      const theKhu = el("div", { class: "ptn-khu-hop" },
        el("div", { class: "ptn-khu-tieu-de" },
          el("span", { class: `ptn-khu-cham ${khu.id.replace(/-/g, "_")}` }),
          el("span", { text: khu.ten })
        )
      );

      if (dsTram.length === 0) {
        theKhu.append(el("p", { class: "text-xs text-muc-2 px-2 py-1", text: "Khu vực tự do & tủ bảo hộ" }));
      } else {
        dsTram.forEach((tr) => {
          const d = U.du[tr.tn];
          if (!d) return;
          const laDangLam = U.tramDangLam === tr.id;
          const theTram = el("div", { class: `ptn-tram-the${laDangLam ? " dang-lam" : ""}` },
            el("div", { class: "ptn-tram-dau" },
              el("span", { class: "ptn-tram-ten", text: `${d.bieu_tuong} ${d.ten}` }),
              laDangLam
                ? el("span", { class: "ptn-tram-tag", text: "Đang làm" })
                : el("span", { class: "text-xs text-muc-2 font-mono", text: `Lớp ${d.lop}` })
            ),
            el("div", { class: "ptn-tram-nut-hang" },
              nut("Đi tới", () => {
                diToiTram(tr.id, { tucThi: false });
                const nk = $("#ptn-ngan-keo-trai"), lp = $("#ptn-lop-phu-trai");
                if (nk) nk.hidden = true; if (lp) lp.hidden = true;
              }, { class: "btn btn-sm btn-outline", title: `Nhân vật đi tới bàn ${d.ten}` }),
              nut("Đến ngay", () => {
                diToiTram(tr.id, { tucThi: true });
                const nk = $("#ptn-ngan-keo-trai"), lp = $("#ptn-lop-phu-trai");
                if (nk) nk.hidden = true; if (lp) lp.hidden = true;
              }, { class: "btn btn-sm btn-primary", title: `Dịch chuyển tức thì tới bàn ${d.ten}` })
            )
          );
          theKhu.append(theTram);
        });
      }
      return theKhu;
    })
  );
}

function ganTuyChon() {
  const datNhom = (attr, gt) => $$(`[${attr}]`).forEach((b) => b.setAttribute("aria-checked", String(b.getAttribute(attr) === gt)));
  datNhom("data-che-do", U.cheDo);
  datNhom("data-di-chuyen", U.diChuyen);
  datNhom("data-chat-luong", U.chatLuong);

  $$("[data-che-do]").forEach((b) => b.addEventListener("click", () => {
    U.cheDo = b.dataset.cheDo;
    datNhom("data-che-do", U.cheDo);
    Object.values(U.mp).forEach((mp) => mp.thaoTac("doi_che_do", { che_do: U.cheDo }));
  }));

  $$("[data-di-chuyen]").forEach((b) => b.addEventListener("click", () => {
    U.diChuyen = b.dataset.diChuyen;
    luuGhi("ptn-di-chuyen", U.diChuyen);
    datNhom("data-di-chuyen", U.diChuyen);
  }));

  $$("[data-chat-luong]").forEach((b) => b.addEventListener("click", () => {
    U.chatLuong = b.dataset.chatLuong;
    luuGhi(KHOA_CHAT_LUONG, U.chatLuong);
    datNhom("data-chat-luong", U.chatLuong);
    U.web?.datChatLuong(U.chatLuong);
  }));
}

function ganTab() {
  const tabs = $$('[role="tab"]');
  const chon = (ten, moTam = true) => {
    tabs.forEach((t) => {
      const c = t.dataset.tab === ten;
      t.setAttribute("aria-selected", String(c));
      t.tabIndex = c ? 0 : -1;
      const pn = $(`#pn-${t.dataset.tab}`);
      if (pn) pn.hidden = !c;
    });
    if (ten === "so-tay") { U.soTayMoi = 0; datChu("ptn-dem-so-tay", ""); }
    if (moTam && matchMedia("(max-width: 900px)").matches) moTamKeo(true);
  };
  tabs.forEach((t) => t.addEventListener("click", () => chon(t.dataset.tab)));
  $(".ptn-the-tab").addEventListener("keydown", (e) => {
    const i = tabs.indexOf(document.activeElement);
    if (i < 0 || !["ArrowLeft", "ArrowRight"].includes(e.key)) return;
    const k = tabs[(i + (e.key === "ArrowRight" ? 1 : -1) + tabs.length) % tabs.length];
    k.focus();
    chon(k.dataset.tab);
  });
  U.chonTab = chon;
}

function moTamKeo(mo) {
  const p = $("#ptn-cot-phai");
  p.classList.toggle("mo", mo);
  $("#ptn-tay-keo").setAttribute("aria-expanded", String(mo));
}

function ganTamKeo() {
  const tay = $("#ptn-tay-keo");
  let y0 = null;
  tay.addEventListener("click", () => moTamKeo(!$("#ptn-cot-phai").classList.contains("mo")));
  tay.addEventListener("pointerdown", (e) => { y0 = e.clientY; });
  window.addEventListener("pointerup", (e) => {
    if (y0 === null) return;
    const dy = e.clientY - y0;
    y0 = null;
    if (dy < -40) moTamKeo(true); else if (dy > 40) moTamKeo(false);
  });
}

// ------------------------------------------------------------------ điều hướng trạm & phòng mở
function diToiTram(id, { tucThi = false } = {}) {
  if (U.diChuyen === "den_ngay") tucThi = true;
  if (!U.banDo) return;
  const tr = U.banDo.tram.find((x) => x.id === id || x.tn === id);
  if (!tr) return;

  if (U.cheDoPhong === "tai_ban") {
    if (U.tramDangLam === tr.id) return;
    roiTram();
  }

  if (tucThi) {
    U.web?.nhanVat?.dichChuyen(tr.diem_dung[0], tr.diem_dung[1], tr.huong);
    vaoTram(tr.id, { tucThi: true });
  } else {
    U.web?.nhanVat?.diToi(tr.diem_dung[0], tr.diem_dung[1], () => {
      vaoTram(tr.id, { tucThi: false });
    });
  }
}

function vaoTram(id, { tucThi = false } = {}) {
  const tnId = id;
  const tn = U.du[tnId];
  if (!tn) return;

  U.id = tnId;
  U.tramDangLam = tnId;
  U.cheDoPhong = "tai_ban";

  datChu("ptn-ten-tn", `${tn.bieu_tuong} ${tn.ten}`);
  datChu("ptn-phu-de", tn.mo_ta);
  document.title = `${tn.ten} · Phòng thí nghiệm ảo · CHEMISTRY`;

  const url = new URL(location.href);
  url.searchParams.set("tn", tnId);
  history.replaceState(null, "", url);

  const cotPhai = $("#ptn-cot-phai");
  if (cotPhai) cotPhai.hidden = false;

  const badgeE = $("#ptn-hud-tuong-tac");
  if (badgeE) badgeE.hidden = true;

  const gDiDao = $("#ptn-nhom-cam-di-dao"), gTaiBan = $("#ptn-nhom-cam-tai-ban");
  if (gDiDao) gDiDao.hidden = true;
  if (gTaiBan) gTaiBan.hidden = false;

  const nutLam = $("#ptn-nut-lam-tn"), nutRoi = $("#ptn-nut-roi-ban");
  if (nutLam) nutLam.hidden = true;
  if (nutRoi) nutRoi.hidden = false;

  const mp = U.mp[tnId];
  const s = mp.trangThai();
  U.s = s;

  U.web?.vaoTram(tnId, { tucThi });
  U.panel = (BANG[tnId] || (() => ({})))($("#pn-dieu-khien"), tn, mp);

  veSoTay(mp);
  veCauHoi(tn, mp);
  veThongTin(tn);
  nhanTrangThai(s);

  U.chonTab("dieu-khien");
  veDanhSach();

  if (U.cauHinh) {
    const hien = U.cauHinh.unity_san_sang && (U.cauHinh.unity_bat || U.cauHinh.la_admin);
    const b = $("#ptn-nut-unity");
    if (b) b.hidden = !hien;
  }
}

function roiTram() {
  if (U.cheDoPhong !== "tai_ban") return;

  if (U.tramDangLam && U.mp[U.tramDangLam]) {
    const mp = U.mp[U.tramDangLam];
    const st = mp.trangThai();
    if (st.dang_chay || st.khoa === "mo") {
      mp.thaoTac("tam_dung");
      mp.themSoTay?.({
        thoi_gian: Math.round(performance.now() / 1000),
        kieu: "buoc",
        noi_dung: "⏸ Tạm dừng khi rời bàn thí nghiệm."
      });
    }
  }

  U.tramDangLam = null;
  U.cheDoPhong = "di_dao";

  const cotPhai = $("#ptn-cot-phai");
  if (cotPhai) cotPhai.hidden = true;

  U.web?.roiTram();

  datChu("ptn-ten-tn", "Phòng thí nghiệm ảo 3D");
  datChu("ptn-phu-de", "Không gian mở 5 khu vực thực hành");
  document.title = "Phòng thí nghiệm ảo · CHEMISTRY";

  const url = new URL(location.href);
  url.searchParams.delete("tn");
  history.replaceState(null, "", url);

  const gDiDao = $("#ptn-nhom-cam-di-dao"), gTaiBan = $("#ptn-nhom-cam-tai-ban");
  if (gDiDao) gDiDao.hidden = false;
  if (gTaiBan) gTaiBan.hidden = true;

  const nutRoi = $("#ptn-nut-roi-ban");
  if (nutRoi) nutRoi.hidden = true;

  const badgeE = $("#ptn-hud-tuong-tac");
  const nutLam = $("#ptn-nut-lam-tn");
  if (U.tramGan) {
    if (badgeE) badgeE.hidden = false;
    if (nutLam) {
      nutLam.hidden = false;
      nutLam.textContent = `🧪 Làm thí nghiệm: ${U.du[U.tramGan]?.ten || U.tramGan} (E)`;
    }
  } else {
    if (badgeE) badgeE.hidden = true;
    if (nutLam) nutLam.hidden = true;
  }

  const b = $("#ptn-nut-unity");
  if (b) b.hidden = true;

  U.chonTab("tham-quan", false);
  veThamQuan();
  veDanhSach();
}

function moThiNghiem(id) {
  vaoTram(id, { tucThi: true });
}

function capNhatViTri(x, z) {
  const nvG = $("#ptn-mini-nv");
  if (nvG && U.web?.nhanVat) {
    const rot = U.web.nhanVat.gocQuay ?? U.web.nhanVat.gocHuong ?? 0;
    const rotDeg = (-rot * 180) / Math.PI;
    nvG.setAttribute("transform", `translate(${x * 10}, ${z * 10}) rotate(${rotDeg})`);
  }
}

function capNhatKhuVuc(khu) {
  const kId = typeof khu === "string" ? khu : khu?.id;
  const khuObj = (typeof khu === "object" && khu !== null && khu.ten) ? khu : U.banDo?.khu?.find((k) => k.id === kId);
  U.khuHienTai = kId;
  const viTriEl = $("#ptn-vi-tri-hien-tai");
  if (viTriEl) viTriEl.textContent = khuObj ? khuObj.ten : (kId || "Khu vực tự do");
  const dotEl = $("#ptn-khu-dot");
  if (dotEl) {
    dotEl.className = `ptn-khu-cham ${kId ? kId.replace(/-/g, "_") : "trung_tam"}`;
  }
  if (U.cheDoPhong === "di_dao") veThamQuan();
}

function capNhatTramGan(id) {
  U.tramGan = id;
  const badgeE = $("#ptn-hud-tuong-tac");
  const chuE = $("#ptn-hud-tuong-tac-chu");
  const nutLam = $("#ptn-nut-lam-tn");
  if (id && U.cheDoPhong === "di_dao") {
    const tn = U.du[id];
    const ten = tn?.ten || id;
    if (badgeE) {
      badgeE.hidden = false;
      if (chuE) chuE.textContent = `Thực hành: ${ten}`;
    }
    if (nutLam) {
      nutLam.hidden = false;
      nutLam.textContent = `🧪 Làm thí nghiệm: ${ten} (E)`;
    }
  } else {
    if (badgeE) badgeE.hidden = true;
    if (nutLam) nutLam.hidden = true;
  }
  if (U.cheDoPhong === "di_dao") veThamQuan();
}

function veThamQuan() {
  const pn = $("#pn-tham-quan");
  if (!pn) return;

  const khu = U.khuHienTai;
  const tramGan = U.tramGan ? U.du[U.tramGan] : null;

  pn.replaceChildren(
    // Thẻ 1: Khu vực hiện tại
    el("div", { class: "ptn-tq-the" },
      el("div", { class: "ptn-tq-the-tieu-de" },
        el("span", { class: `ptn-khu-cham ${khu?.id || "trung_tam"}` }),
        el("span", { text: khu ? khu.ten : "Đang tham quan phòng lab" })
      ),
      el("p", { class: "text-xs text-muc-2 mb-2", text: khu?.mo_ta || "Phòng thí nghiệm Hoá học ảo 3D không gian mở." })
    ),

    // Thẻ 2: Bàn thí nghiệm gần nhất
    el("div", { class: "ptn-tq-the" },
      el("div", { class: "ptn-tq-the-tieu-de" },
        el("span", { text: "🧪 Bàn thí nghiệm gần bạn" })
      ),
      tramGan
        ? el("div", {},
            el("p", { class: "font-semibold text-sm mb-1", text: `${tramGan.bieu_tuong} ${tramGan.ten}` }),
            el("p", { class: "text-xs text-muc-2 mb-3", text: tramGan.mo_ta }),
            nut("Vào làm thí nghiệm (E)", () => vaoTram(tramGan.id), { class: "btn btn-primary btn-sm w-full" })
          )
        : el("p", { class: "text-xs text-muc-2", text: "Hãy di chuyển lại gần một bàn thí nghiệm (có viền sáng) để bắt đầu." })
    ),

    // Thẻ 3: Tiến độ 5 thí nghiệm
    el("div", { class: "ptn-tq-the" },
      el("div", { class: "ptn-tq-the-tieu-de" }, el("span", { text: "📋 Tiến độ 5 thí nghiệm" })),
      el("div", { class: "flex flex-col gap-2 mt-2" },
        ...DS_TN.map((id) => {
          const d = U.du[id];
          const mp = U.mp[id];
          const st = mp?.trangThai();
          const daXong = st?.so_tay?.length > 1;
          const laHuyHieu = id === "an-toan" && luuDoc(KHOA_HUY_HIEU);
          return el("div", { class: "flex items-center justify-between text-xs p-1.5 rounded bg-giay" },
            el("span", { class: "font-medium", text: `${d.bieu_tuong} ${d.ten}` }),
            el("span", {
              class: daXong || laHuyHieu ? "text-green-600 font-semibold" : "text-muc-2",
              text: laHuyHieu ? "🏅 Đã nhận huy hiệu" : daXong ? "✓ Đã làm" : "Chưa làm"
            })
          );
        })
      )
    ),

    // Thẻ 4: Phím tắt điều khiển
    el("div", { class: "ptn-tq-the" },
      el("div", { class: "ptn-tq-the-tieu-de" }, el("span", { text: "⌨️ Bàn phím điều khiển" })),
      el("div", { class: "ptn-phim-tat-grid" },
        el("kbd", { text: "W A S D" }), el("span", { text: "Di chuyển nhân vật (hoặc phím mũi tên)" }),
        el("kbd", { text: "Shift" }), el("span", { text: "Giữ để chạy nhanh gấp đôi" }),
        el("kbd", { text: "E" }), el("span", { text: "Làm thí nghiệm / Tương tác tủ bảo hộ" }),
        el("kbd", { text: "Esc" }), el("span", { text: "Rời bàn thí nghiệm trở về phòng" }),
        el("kbd", { text: "V" }), el("span", { text: "Toàn cảnh phòng ⇄ Theo sau nhân vật" }),
        el("kbd", { text: "M" }), el("span", { text: "Mở rộng / Thu nhỏ bản đồ mini" }),
        el("kbd", { text: "Chuột" }), el("span", { text: "Bấm sàn để đi tới, kéo để xoay camera" })
      )
    ),

    // Thẻ 5: Ngoại hình nhân vật & đồ bảo hộ
    el("div", { class: "ptn-tq-the" },
      el("div", { class: "ptn-tq-the-tieu-de" }, el("span", { text: "👤 Nhân vật & Đồ bảo hộ" })),
      el("div", { class: "flex flex-col gap-2 mt-2" },
        el("div", { class: "flex items-center justify-between text-xs" },
          el("span", { text: "Phong cách:" }),
          el("div", { class: "flex gap-1" },
            nut("Nam", () => U.web?.nhanVat?.datNgoaiHinh({ kieu: "binh_thuong" }), { class: "btn btn-xs" }),
            nut("Nữ", () => U.web?.nhanVat?.datNgoaiHinh({ kieu: "nu" }), { class: "btn btn-xs" }),
            nut("Giảng viên", () => U.web?.nhanVat?.datNgoaiHinh({ kieu: "giang_vien" }), { class: "btn btn-xs" })
          )
        ),
        el("div", { class: "flex items-center justify-between text-xs mt-1" },
          el("span", { text: "Trang bị bảo hộ:" }),
          el("div", { class: "flex gap-1" },
            nut("Áo blouse", () => {
              const nv = U.web?.nhanVat;
              if (nv) { nv.datNgoaiHinh({ aoBlouse: !nv.trangBi.aoBlouse }); toast(nv.trangBi.aoBlouse ? "Đã mặc áo blouse." : "Đã cởi áo blouse."); }
            }, { class: "btn btn-xs btn-outline" }),
            nut("Kính", () => {
              const nv = U.web?.nhanVat;
              if (nv) { nv.datNgoaiHinh({ kinh: !nv.trangBi.kinh }); toast(nv.trangBi.kinh ? "Đã đeo kính bảo hộ." : "Đã tháo kính."); }
            }, { class: "btn btn-xs btn-outline" }),
            nut("Găng tay", () => {
              const nv = U.web?.nhanVat;
              if (nv) { nv.datNgoaiHinh({ gangTay: !nv.trangBi.gangTay }); toast(nv.trangBi.gangTay ? "Đã đeo găng tay." : "Đã tháo găng tay."); }
            }, { class: "btn btn-xs btn-outline" })
          )
        )
      )
    )
  );
}

function khoiTaoBanDoMini() {
  const mini = $("#ptn-ban-do-mini"), svgEl = $("#ptn-mini-svg"), nutThuPhong = $("#ptn-mini-thu-phong");
  if (!mini || !svgEl || !U.banDo) return;

  nutThuPhong?.addEventListener("click", () => {
    mini.classList.toggle("mo-rong");
    U.miniRong = mini.classList.contains("mo-rong");
  });

  window.addEventListener("keydown", (e) => {
    if (["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName)) return;
    if (e.key === "m" || e.key === "M") {
      mini.classList.toggle("mo-rong");
      U.miniRong = mini.classList.contains("mo-rong");
    }
  });

  const banDo = U.banDo;
  svgEl.setAttribute("viewBox", "0 0 220 160");
  svgEl.replaceChildren();

  // Nền phòng
  svgEl.append(svg("rect", { x: 0, y: 0, width: 220, height: 160, fill: "#0f172a" }));

  // Các khu vực
  const mauKhu = {
    an_toan: "#059669", dinh_luong: "#2563eb", dinh_tinh: "#7c3aed", dien_hoa: "#d97706", trung_tam: "#475569"
  };

  const dsKhu = banDo.khu || [];
  dsKhu.forEach((k) => {
    const x = k.hcn[0] * 10, y = k.hcn[1] * 10;
    const w = (k.hcn[2] - k.hcn[0]) * 10, h = (k.hcn[3] - k.hcn[1]) * 10;
    const kId = k.id.replace(/-/g, "_");
    const m = mauKhu[kId] || "#64748b";
    const r = svg("rect", {
      x, y, width: w, height: h,
      fill: m, "fill-opacity": "0.18",
      stroke: m, "stroke-width": "1", "stroke-opacity": "0.55", rx: 2
    });
    r.style.cursor = "pointer";
    r.addEventListener("click", (e) => {
      e.stopPropagation();
      const rect = svgEl.getBoundingClientRect();
      const clickX = ((e.clientX - rect.left) / rect.width) * 22;
      const clickZ = ((e.clientY - rect.top) / rect.height) * 16;
      if (U.diChuyen === "den_ngay") U.web.nhanVat?.dichChuyen(clickX, clickZ);
      else U.web.nhanVat?.diToi(clickX, clickZ);
    });
    svgEl.append(r);
    const t = svg("text", {
      x: x + w / 2, y: y + 14,
      fill: m, "font-size": "8", "font-weight": "bold", "text-anchor": "middle", "pointer-events": "none"
    });
    t.textContent = k.ten;
    svgEl.append(t);
  });

  // Vật cản tĩnh
  const BD = window.BanDoPhongLab || window.PTNBanDo;
  const vatCan = BD ? BD.layVatCan(banDo) : [];
  vatCan.forEach((v) => {
    const x = v.minX * 10, y = v.minZ * 10, w = (v.maxX - v.minX) * 10, h = (v.maxZ - v.minZ) * 10;
    svgEl.append(svg("rect", {
      x, y, width: w, height: h, fill: "#334155", "fill-opacity": "0.6", stroke: "#475569", "stroke-width": "0.6", rx: 1.5, "pointer-events": "none"
    }));
  });

  // Điểm trạm
  (banDo.tram || []).forEach((tr) => {
    const cx = tr.tam[0] * 10, cy = tr.tam[1] * 10;
    const g = svg("g", { style: "cursor: pointer;" });
    g.append(svg("circle", { cx, cy, r: "4.5", fill: "#38bdf8", stroke: "#ffffff", "stroke-width": "1.2" }));
    const t = svg("text", {
      x: cx, y: cy + 12, fill: "#e2e8f0", "font-size": "7.5", "font-weight": "600", "text-anchor": "middle", "pointer-events": "none"
    });
    t.textContent = U.du[tr.tn]?.bieu_tuong || "🧪";
    g.append(t);
    g.addEventListener("click", (e) => {
      e.stopPropagation();
      diToiTram(tr.id);
    });
    svgEl.append(g);
  });

  // Điểm đánh dấu nhân vật
  const xp = banDo.xuat_phat;
  const xpX = xp ? (xp.x ?? (Array.isArray(xp) ? xp[0] : 11)) : 11;
  const xpZ = xp ? (xp.z ?? (Array.isArray(xp) ? xp[1] : 15.2)) : 15.2;
  const nvG = svg("g", { id: "ptn-mini-nv", transform: `translate(${xpX * 10}, ${xpZ * 10})` });
  nvG.append(svg("circle", { cx: 0, cy: 0, r: "4", fill: "#38bdf8", stroke: "#ffffff", "stroke-width": "1.2" }));
  nvG.append(svg("polygon", { points: "0,-7 3,2 0,0 -3,2", fill: "#38bdf8" }));
  svgEl.append(nvG);
}

function khoiTaoJoystick() {
  const joy = $("#ptn-joystick"), nut = $("#ptn-joystick-nut");
  if (!joy || !nut) return;

  const laCamUng = "ontouchstart" in window || navigator.maxTouchPoints > 0;
  if (laCamUng) joy.hidden = false;

  let pointerId = null, cx = 0, cy = 0;
  const R = 32;

  joy.addEventListener("pointerdown", (e) => {
    pointerId = e.pointerId;
    joy.setPointerCapture(pointerId);
    const rect = joy.getBoundingClientRect();
    cx = rect.left + rect.width / 2;
    cy = rect.top + rect.height / 2;
    xuLyJoystick(e.clientX, e.clientY);
  });

  joy.addEventListener("pointermove", (e) => {
    if (e.pointerId !== pointerId) return;
    xuLyJoystick(e.clientX, e.clientY);
  });

  const dungJoy = (e) => {
    if (e.pointerId !== pointerId) return;
    pointerId = null;
    nut.style.transform = "translate(0px, 0px)";
    U.web?.nhanVat?.dieuKhienJoystick(0, 0);
  };

  joy.addEventListener("pointerup", dungJoy);
  joy.addEventListener("pointercancel", dungJoy);

  function xuLyJoystick(px, py) {
    let dx = px - cx, dy = py - cy;
    const d = Math.hypot(dx, dy);
    if (d > R) { dx = (dx / d) * R; dy = (dy / d) * R; }
    nut.style.transform = `translate(${dx}px, ${dy}px)`;
    U.web?.nhanVat?.dieuKhienJoystick(dx / R, dy / R);
  }
}

function khoiTaoHuongDan() {
  const hd = $("#ptn-huong-dan-dau"), nutDong = $("#ptn-hdd-dong");
  if (!hd) return;
  if (!luuDoc("ptn-da-vao-lan-dau")) hd.hidden = false;
  nutDong?.addEventListener("click", () => {
    hd.hidden = true;
    luuGhi("ptn-da-vao-lan-dau", "1");
  });
}

function nhanTrangThai(s) {
  U.s = s;
  U.ve?.capNhat(s);
  U.panel?.capNhat?.(s);
  veBuoc(s);
}

function nhanHieuUng(e) {
  U.ve?.hieuUng(e);
  if (e.loai === "huy_hieu") {
    luuGhi(KHOA_HUY_HIEU, "1");
    toast(`🏅 Chúc mừng! Bạn đạt huy hiệu "${U.du["an-toan"].huy_hieu}".`, "ok", 6000);
    veDanhSach();
  } else if (e.loai === "doi_mau") {
    toast("Dung dịch đổi màu bền – đã tới điểm cuối chuẩn độ. Đóng khoá và đọc burette.", "ok");
  }
}

/** Thao tác bấm trực tiếp trong khung 3D (khoá burette, lắc bình, cầu muối, điểm "tìm lỗi", tủ bảo hộ). */
function thaoTacTuVe(lenh, ts) {
  if (lenh === "tu_bao_ho") {
    const nv = U.web?.nhanVat;
    if (nv) {
      const bat = !nv.trangBi.aoBlouse;
      nv.datNgoaiHinh({ aoBlouse: bat, kinh: bat, gangTay: bat });
      toast(bat ? "Đã trang bị áo blouse, kính và găng tay bảo hộ!" : "Đã cất đồ bảo hộ vào tủ.");
      if (U.cheDoPhong === "di_dao") veThamQuan();
    }
    return;
  }
  const id = U.tramDangLam || U.id;
  const mp = U.mp[id], s = mp?.trangThai();
  if (!mp) return;
  if (lenh === "bat_tat_khoa") return mp.thaoTac(s?.khoa === "dong" ? "mo_khoa" : "dong_khoa", { kieu: "giot" });
  if (lenh === "doi_cau_muoi") return mp.thaoTac(s?.cau_muoi ? "rut_cau_muoi" : "lap_cau_muoi");
  const kq = mp.thaoTac(lenh, ts);
  if (lenh === "bam_diem" && kq?.thong_bao) toast(kq.loai === "dung" ? `✓ Việc làm đúng: ${kq.thong_bao}` : `⚠ Lỗi: ${kq.thong_bao}`, kq.loai === "dung" ? "ok" : "loi", 6500);
  return kq;
}

function chonTuVe(c) {
  if (c?.loai === "ong") U.panel?.chonOng?.(c.so);
}

// ------------------------------------------------------------------ bước hướng dẫn
function veBuoc(s) {
  const k = $("#ptn-buoc"), tn = U.du[s.id];
  if (s.che_do !== "huong_dan" || !tn.buoc?.length) { k.hidden = true; return; }
  k.hidden = false;
  const i = s.buoc_hien_tai;
  const khoa = `${s.id}:${i}`;
  if (k.dataset.khoa === khoa) return;
  k.dataset.khoa = khoa;
  if (i === null) {
    k.replaceChildren(el("div", { class: "ptn-buoc-so", text: "✓" }),
      el("div", {}, el("b", { text: "Hoàn thành các bước hướng dẫn!" }), el("p", { text: "Làm câu hỏi cuối bài để ôn tập theo dạng đề thi tốt nghiệp THPT." })),
      nut("Làm câu hỏi", () => U.chonTab("cau-hoi")));
    return;
  }
  const b = tn.buoc[i];
  const goiY = el("p", { class: "ptn-goi-y", hidden: true, text: `💡 ${b.goi_y}` });
  k.replaceChildren(el("div", { class: "ptn-buoc-so", text: `${i + 1}/${tn.buoc.length}` }),
    el("div", {}, el("b", { text: b.tieu_de }), el("p", { text: b.huong_dan })),
    b.goi_y ? nut("Gợi ý", (e) => { goiY.hidden = !goiY.hidden; e.currentTarget.setAttribute("aria-expanded", String(!goiY.hidden)); }, { "aria-expanded": "false" }) : el("span"),
    goiY);
}

// ------------------------------------------------------------------ sổ tay
function veSoTay(mp) {
  const pn = $("#pn-so-tay");
  const ds = el("ol", { class: "ptn-so-tay", id: "ptn-ds-so-tay", "aria-live": "polite" });
  pn.replaceChildren(nhom("Sổ tay thí nghiệm", el("p", { class: "ptn-ghi-chu", text: "Tự ghi lại thao tác và hiện tượng. Có thể tải về để nộp hoặc ôn tập." }),
    el("div", { class: "ptn-hang", style: { margin: "8px 0 12px" } }, nut("Tải sổ tay (.txt)", taiSoTay)), ds));
  U.soTayMoi = 0;
  datChu("ptn-dem-so-tay", "");
  mp.soTay().forEach((m) => themSoTay(m, true));
}
const ICON_SO_TAY = { thao_tac: "✋", hien_tuong: "👁", ket_qua: "📌", buoc: "✓", canh_bao: "⚠" };
function themSoTay(m, cu = false) {
  const ds = $("#ptn-ds-so-tay");
  if (!ds) return;
  ds.prepend(el("li", { class: m.loai }, el("time", { text: m.gio }), el("span", { text: `${ICON_SO_TAY[m.loai] || "•"} ${m.chu}` })));
  if (!cu && $("#pn-so-tay").hidden) { U.soTayMoi += 1; datChu("ptn-dem-so-tay", String(U.soTayMoi)); }
}
function taiSoTay() {
  const tn = U.du[U.id], ds = U.mp[U.id].soTay();
  const chu = [`SỔ TAY THÍ NGHIỆM – ${tn.ten}`, `Ngày: ${new Date().toLocaleString("vi-VN")}`, "", ...ds.map((m) => `[${m.gio}] ${m.chu}`)].join("\n");
  const a = el("a", { href: URL.createObjectURL(new Blob([chu], { type: "text/plain;charset=utf-8" })), download: `so-tay-${tn.id}.txt` });
  document.body.append(a);
  a.click();
  setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 500);
}

// ------------------------------------------------------------------ câu hỏi cuối bài (3 dạng đề thi tốt nghiệp THPT)
function veCauHoi(tn, mp) {
  const pn = $("#pn-cau-hoi"), ch = tn.cau_hoi, chu = "ABCD";
  const form = el("form", { novalidate: true });
  const p1 = ch.bon_lua_chon.map((c, i) => el("fieldset", { class: "ptn-cau", "data-loai": "mcq", "data-i": i },
    el("legend", { text: `Câu ${i + 1}. ${c.cau}` }),
    ...c.lua_chon.map((l, k) => el("label", {}, el("input", { type: "radio", name: `mcq-${i}`, value: chu[k] }), el("span", { text: `${chu[k]}. ${l}` }))),
    el("p", { class: "ptn-giai-thich", hidden: true })));
  const p2 = ch.dung_sai.map((c, i) => el("fieldset", { class: "ptn-cau", "data-loai": "ds", "data-i": i },
    el("legend", { text: `Câu ${i + 1}. ${c.cau}` }),
    el("div", { class: "ptn-ds-y" }, ...c.y.map((y, k) => el("div", { class: "ptn-y" }, el("span", { text: `${"abcd"[k]}) ${y.noi_dung}` }),
      el("div", { class: "ptn-hang" }, el("label", {}, el("input", { type: "radio", name: `ds-${i}-${k}`, value: "1" }), el("span", { text: "Đúng" })),
        el("label", {}, el("input", { type: "radio", name: `ds-${i}-${k}`, value: "0" }), el("span", { text: "Sai" })))))),
    el("p", { class: "ptn-giai-thich", hidden: true })));
  const p3 = ch.tra_loi_ngan.map((c, i) => el("fieldset", { class: "ptn-cau", "data-loai": "tln", "data-i": i },
    el("legend", { text: `Câu ${i + 1}. ${c.cau}` }),
    el("div", { class: "ptn-nhap", style: { marginTop: "8px" } }, el("input", { type: "text", inputmode: "decimal", name: `tln-${i}`, "aria-label": `Đáp số câu ${i + 1}`, autocomplete: "off" }),
      c.don_vi ? el("span", { text: c.don_vi }) : null),
    el("p", { class: "ptn-giai-thich", hidden: true })));
  const kq = el("div", { class: "ptn-the-ket-qua", hidden: true, "aria-live": "polite" });
  form.append(nhom("Phần I. Trắc nghiệm nhiều lựa chọn", ...p1), nhom("Phần II. Trắc nghiệm đúng – sai", ...p2),
    nhom("Phần III. Trả lời ngắn", ...p3), el("div", { class: "ptn-hang" }, el("button", { type: "submit", class: "btn btn-primary btn-sm", text: "Nộp bài" }),
      nut("Làm lại", () => { form.reset(); $$(".ptn-giai-thich", form).forEach((g) => { g.hidden = true; }); kq.hidden = true; })), kq);
  form.addEventListener("submit", (e) => {
    e.preventDefault();
    let dung = 0, tong = 0;
    ch.bon_lua_chon.forEach((c, i) => {
      tong += 1;
      const chon = form.elements[`mcq-${i}`].value, ok = chon === c.dap_an;
      if (ok) dung += 1;
      const g = $(`[data-loai="mcq"][data-i="${i}"] .ptn-giai-thich`, form);
      g.hidden = false;
      g.className = `ptn-giai-thich ${ok ? "dung" : "sai"}`;
      g.textContent = `${ok ? "✓ Đúng." : `✗ Đáp án ${c.dap_an}.`} ${c.giai_thich}`;
    });
    ch.dung_sai.forEach((c, i) => {
      const dong = c.y.map((y, k) => {
        tong += 1;
        const v = form.elements[`ds-${i}-${k}`].value, ok = v !== "" && (v === "1") === y.dung;
        if (ok) dung += 1;
        return `${"abcd"[k]}) ${y.dung ? "Đúng" : "Sai"}${ok ? " ✓" : " ✗"} – ${y.giai_thich}`;
      });
      const g = $(`[data-loai="ds"][data-i="${i}"] .ptn-giai-thich`, form);
      g.hidden = false;
      g.textContent = dong.join("\n");
      g.style.whiteSpace = "pre-line";
    });
    ch.tra_loi_ngan.forEach((c, i) => {
      tong += 1;
      const v = parseFloat(String(form.elements[`tln-${i}`].value).replace(",", "."));
      const ok = Number.isFinite(v) && Math.abs(v - parseFloat(c.dap_so.replace(",", "."))) <= (c.sai_so || 0) + 1e-9;
      if (ok) dung += 1;
      const g = $(`[data-loai="tln"][data-i="${i}"] .ptn-giai-thich`, form);
      g.hidden = false;
      g.className = `ptn-giai-thich ${ok ? "dung" : "sai"}`;
      g.textContent = `${ok ? "✓ Đúng." : `✗ Đáp số: ${c.dap_so}${c.don_vi ? ` ${c.don_vi}` : ""}.`} ${c.giai_thich}`;
    });
    kq.hidden = false;
    kq.textContent = `Kết quả: đúng ${dung}/${tong} ý (${Math.round((100 * dung) / tong)}%).`;
    mp.thaoTac("nop_cau_hoi", { dung, tong });
  });
  pn.replaceChildren(form);
}

// ------------------------------------------------------------------ thông tin
function veThongTin(tn) {
  const ghs = Object.fromEntries((U.du["an-toan"].cau_hinh.ghs || []).map((g) => [g.ma, g]));
  const ds = (mang) => el("ul", { class: "ptn-ds-gon" }, ...mang.map((x) => el("li", { text: x })));
  const hc = el("ul", { class: "ptn-ds-gon" }, ...tn.hoa_chat.map((h) => el("li", {}, el("span", { text: h.ten }),
    h.nguy_hiem.length ? el("span", { class: "ptn-ghi-chu", text: ` – ${h.nguy_hiem.map((m) => `${ghs[m]?.id || ""} ${ghs[m]?.ten || m}`).join(", ")}` }) : null)));
  const con = [nhom("Mục tiêu", ds(tn.muc_tieu)), nhom("Dụng cụ", ds(tn.dung_cu)), nhom("Hoá chất", hc), nhom("An toàn", ds(tn.an_toan))];
  if (tn.id === "nhan-biet-ion") {
    const bang = el("table", { class: "ptn-bang" }, el("thead", {}, el("tr", {}, el("th", { text: "Ion" }), el("th", { text: "Thuốc thử" }), el("th", { text: "Hiện tượng – phương trình" }))),
      el("tbody", {}, ...U.bangIon.phan_ung.map((p) => el("tr", {}, el("td", { text: U.bangIon.ion[p.ion].ten }), el("td", { text: U.bangIon.thuoc_thu[p.thuoc_thu].ten }),
        el("td", {}, el("div", { text: p.hien_tuong }), el("code", { text: p.pt_ion }))))));
    con.push(nhom("", el("details", {}, el("summary", { text: "Bảng tra cứu ion (xem sau khi tự làm)" }), bang)));
  }
  $("#pn-thong-tin").replaceChildren(nhom("", el("p", { text: tn.mo_ta })), ...con);
}

// ================================================================== BẢNG ĐIỀU KHIỂN TỪNG THÍ NGHIỆM
const BANG = {
  "chuan-do": (pn, tn, mp) => {
    const ch = tn.cau_hinh;
    const cBai = ch.bai.map((b) => chip(b.cong_thuc, () => mp.thaoTac("chon_bai", { id: b.id }), { "data-bai": b.id, title: b.mo_ta }));
    const cCt = [["", "Không"], ["phenolphthalein", "Phenolphthalein"], ["methyl_orange", "Methyl orange"]]
      .map(([id, ten]) => chip(ten, () => mp.thaoTac("chon_chi_thi", { id: id || null }), { "data-ct": id }));
    const bNhanh = nut("Chảy nhanh", () => mp.thaoTac("mo_khoa", { kieu: "nhanh" }));
    const bGiot = nut("Từng giọt", () => mp.thaoTac("mo_khoa", { kieu: "giot" }));
    const bDong = nut("Đóng khoá", () => mp.thaoTac("dong_khoa"), { class: "btn btn-primary btn-sm" });
    const b1g = nut("+1 giọt", () => mp.thaoTac("nho_giot"));
    const b1m = nut("+1 mL", () => mp.thaoTac("them", { ml: 1 }));
    const bLac = nut("Lắc bình", () => mp.thaoTac("lac"));
    const doThi = el("div", { id: "ptn-do-thi" });
    const zoom = el("div", { id: "ptn-zoom" });
    const oDoc = el("input", { type: "text", inputmode: "decimal", placeholder: "19,95", "aria-label": "Số đọc trên burette (mL)" });
    const phDoc = el("p", { class: "ptn-phan-hoi" });
    const oC = el("input", { type: "text", inputmode: "decimal", placeholder: "0,100", "aria-label": "Nồng độ acid tính được (M)" });
    const phC = el("p", { class: "ptn-phan-hoi" });
    const bOnThi = chip("Chế độ Ôn thi", () => mp.thaoTac(U.s.on_thi ? "tat_on_thi" : "bat_on_thi"));
    const oNop = el("input", { type: "text", inputmode: "decimal", placeholder: "0,085", "aria-label": "Nồng độ acid bạn xác định (M)" });
    const phNop = el("p", { class: "ptn-phan-hoi" });
    const khuOnThi = el("div", { hidden: true }, el("p", { class: "ptn-ghi-chu", text: "Nồng độ acid bị ẩn, không có máy đo pH. Chuẩn độ, đọc burette rồi nhập C(acid)." }),
      el("div", { class: "ptn-nhap" }, oNop, el("span", { text: "M" }), nut("Nộp", () => phanHoi(phNop, mp.thaoTac("nop_ket_qua", { C: oNop.value })), { class: "btn btn-primary btn-sm" })), phNop);
    const khuSo = el("div", { class: "ptn-so-lieu" }, oSo("V(NaOH) đã dùng", "cd-V"), oSo("pH (máy đo)", "cd-pH"), oSo("Màu dung dịch", "cd-mau", true), oSo("Điểm cuối", "cd-cuoi", true));
    pn.replaceChildren(
      nhom("Dung dịch trong bình tam giác (20,00 mL)", el("div", { class: "ptn-hang" }, ...cBai), el("p", { class: "ptn-ghi-chu", id: "cd-acid" })),
      nhom("Chất chỉ thị", el("div", { class: "ptn-hang" }, ...cCt)),
      nhom("Burette NaOH 0,100 M", el("div", { class: "ptn-hang" }, bNhanh, bGiot, bDong), el("div", { class: "ptn-hang", style: { marginTop: "6px" } }, b1g, b1m, bLac),
        el("p", { class: "ptn-ghi-chu", text: "Mẹo: chảy nhanh tới khoảng 15 mL, sau đó nhỏ từng giọt và lắc đều. Có thể bấm trực tiếp vào khoá burette hoặc bình tam giác." })),
      nhom("Số liệu", khuSo),
      nhom("Đồ thị pH – V(NaOH)", doThi),
      nhom("Đọc burette (phóng to mặt khum)", zoom, el("div", { class: "ptn-nhap", style: { marginTop: "8px" } }, oDoc, el("span", { text: "mL" }),
        nut("Ghi số đọc", () => phanHoi(phDoc, mp.thaoTac("doc_buret", { gia_tri: oDoc.value })))), phDoc),
      nhom("Tính nồng độ acid", el("p", { class: "ptn-ghi-chu", text: "C(acid) = C(NaOH) × V(NaOH) / V(acid)" }),
        el("div", { class: "ptn-nhap" }, oC, el("span", { text: "M" }), nut("Kiểm tra", () => phanHoi(phC, mp.thaoTac("tinh_nong_do", { C: oC.value })))), phC),
      nhom("Ôn thi", el("div", { class: "ptn-hang" }, bOnThi), khuOnThi));
    let khoaDoThi = "";
    return {
      capNhat(s) {
        cBai.forEach((b) => b.setAttribute("aria-pressed", String(b.dataset.bai === s.bai)));
        cCt.forEach((b) => b.setAttribute("aria-pressed", String((b.dataset.ct || null) === s.chi_thi)));
        datChu("cd-acid", `${s.acid.ten} – ${s.acid.mo_ta}${s.Ca !== null ? ` · ${dd(s.Ca, 3)} M` : " · nồng độ ẩn"}`);
        bNhanh.setAttribute("aria-pressed", String(s.khoa === "nhanh"));
        bGiot.setAttribute("aria-pressed", String(s.khoa === "giot"));
        [bNhanh, bGiot, b1g, b1m].forEach((b) => { b.disabled = s.het_buret; });
        datChu("cd-V", s.on_thi && !s.ket_qua_on_thi ? "đọc trên burette" : `${dd(s.V)} mL`);
        datChu("cd-pH", s.pH === null ? "(Ôn thi: ẩn)" : dd(s.pH));
        const m = $("#cd-mau");
        const a = s.mau.rgba;
        m.replaceChildren(el("span", { class: "ptn-cham-mau", style: { background: a[3] > 0.02 ? `rgba(${a.slice(0, 3).map(Math.round).join(",")},${Math.max(0.35, a[3])})` : "transparent" } }), s.mau.mo_ta);
        datChu("cd-cuoi", s.V_doi_mau !== null ? `${dd(s.V_doi_mau)} mL (đổi màu bền)` : s.vet_mau ? "vệt màu xuất hiện – lắc bình" : "chưa đổi màu");
        const kDt = `${s.diem.length}:${s.V_doi_mau}:${s.on_thi}`;
        if (kDt !== khoaDoThi) { khoaDoThi = kDt; doThi.replaceChildren(s.on_thi && !s.ket_qua_on_thi ? el("p", { class: "ptn-ghi-chu", text: "Chế độ Ôn thi: ẩn đồ thị pH." }) : veDoThi(s)); }
        zoom.replaceChildren(veVachBuret(s.V));
        bOnThi.setAttribute("aria-pressed", String(s.on_thi));
        khuOnThi.hidden = !s.on_thi;
        if (!s.ket_qua_on_thi) phNop.textContent = "";
      },
    };
  },

  "an-toan": (pn, tn, mp) => {
    const ch = tn.cau_hinh;
    const tienDo = el("div", { class: "ptn-tien-do", role: "progressbar", "aria-label": "Số lỗi đã tìm", "aria-valuemin": "0", "aria-valuemax": String(ch.loi.length) }, el("span"));
    const dsNut = el("div", { class: "ptn-hang" });
    const daTim = el("ol", { class: "ptn-ds-gon" });
    const nghia = [...ch.ghs].sort(() => Math.random() - 0.5);
    const oGhs = ch.ghs.map((g) => {
      const sel = el("select", { class: "ptn-chon-o", "aria-label": `Ý nghĩa biểu tượng ${g.id}` }, el("option", { value: "", text: "– chọn ý nghĩa –" }),
        ...nghia.map((n) => el("option", { value: n.id, text: n.y_nghia })));
      return { g, sel, o: el("div", { class: "ptn-ghs-o", "data-ghs": g.id }, veGHS(g.ma, `Biểu tượng ${g.id}`), el("span", { class: "ptn-ghs-ma", text: g.id }), sel) };
    });
    const phGhs = el("p", { class: "ptn-phan-hoi" });
    const suCo = ch.su_co.map((sc) => {
      const d = el("details", { class: "ptn-su-co" }, el("summary", { text: sc.ten }), el("ol", {}, ...sc.buoc.map((b) => el("li", { text: b }))));
      d.addEventListener("toggle", () => { if (d.open) mp.thaoTac("xem_su_co", { id: sc.id }); });
      return d;
    });
    const huyHieu = el("div", { class: "ptn-the-ket-qua", hidden: true, text: `🏅 Bạn đã hoàn thành bài nhập môn và đạt huy hiệu "${tn.huy_hieu}".` });
    pn.replaceChildren(
      huyHieu,
      nhom("1. Tìm lỗi mất an toàn", el("p", { id: "at-dem", class: "ptn-ghi-chu" }), tienDo,
        el("p", { class: "ptn-ghi-chu", text: "Bấm các vị trí đánh số trong khung 3D, hoặc dùng các nút dưới đây (cùng tác dụng). Có cả việc làm đúng để đánh lừa." }),
        dsNut, el("h4", { class: "ptn-nhom-tieu-de", style: { marginTop: "12px" }, text: "Lỗi đã tìm" }), daTim),
      nhom("2. Biểu tượng cảnh báo GHS", el("div", { class: "ptn-ghs" }, ...oGhs.map((x) => x.o)),
        el("div", { class: "ptn-hang", style: { marginTop: "8px" } }, nut("Kiểm tra", () => phanHoi(phGhs, mp.thaoTac("ghep_ghs", { tra_loi: Object.fromEntries(oGhs.map((x) => [x.g.id, x.sel.value])) })), { class: "btn btn-primary btn-sm" })), phGhs),
      nhom("3. Xử lí sự cố", ...suCo));
    return {
      capNhat(s) {
        datChu("at-dem", `Đã tìm ${s.so_tim}/${s.tong_loi} lỗi${s.so_bam_dung_viec ? ` · ${s.so_bam_dung_viec} lần chọn nhầm việc làm đúng` : ""}.`);
        tienDo.firstElementChild.style.width = `${(100 * s.so_tim) / s.tong_loi}%`;
        tienDo.setAttribute("aria-valuenow", String(s.so_tim));
        if (dsNut.childElementCount !== s.diem_nong.length) {
          dsNut.replaceChildren(...s.diem_nong.map((d) => chip(`${d.so}. ${d.vi_tri}`, () => thaoTacTuVe("bam_diem", { id: d.id }), { "data-dn": d.id })));
        }
        s.diem_nong.forEach((d) => {
          const b = $(`[data-dn="${d.id}"]`, dsNut);
          if (!b) return;
          b.setAttribute("aria-pressed", String(d.da_tim));
          b.textContent = `${d.da_tim ? "✓" : d.la_viec_dung ? "○" : d.so}. ${d.vi_tri}`;
        });
        daTim.replaceChildren(...s.loi.filter((l) => l.da_tim).map((l) => el("li", {}, el("b", { text: l.ten }), el("div", { class: "ptn-ghi-chu", text: l.giai_thich }))));
        if (s.ghs_ket_qua) oGhs.forEach((x) => { x.o.classList.toggle("dung", !!s.ghs_ket_qua.chi_tiet[x.g.id]); x.o.classList.toggle("sai", !s.ghs_ket_qua.chi_tiet[x.g.id]); });
        huyHieu.hidden = !s.huy_hieu;
      },
    };
  },

  "nhan-biet-ion": (pn, tn, mp) => {
    const bi = U.bangIon;
    const tabOng = chip("Ống nghiệm", () => mp.thaoTac("chon_tab", { tab: "ong" }));
    const tabLua = chip("Ngọn lửa", () => mp.thaoTac("chon_tab", { tab: "ngon_lua" }));
    const dsOng = el("div", { class: "ptn-hang", role: "group", "aria-label": "Chọn ống nghiệm" });
    const dsTt = el("div", { class: "ptn-hang", role: "group", "aria-label": "Chọn thuốc thử" },
      ...tn.cau_hinh.thuoc_thu.map((id) => chip(bi.thuoc_thu[id].ten, () => { U.thuocThu = id; capNhatTt(); }, { "data-tt": id, title: bi.thuoc_thu[id].ten_day_du })));
    const capNhatTt = () => $$("[data-tt]", dsTt).forEach((b) => b.setAttribute("aria-pressed", String(b.dataset.tt === U.thuocThu)));
    const lichSu = el("ul", { class: "ptn-lich-su", "aria-live": "polite" });
    const duDoan = el("div", { class: "ptn-so-lieu" });
    const kq = el("div", { "aria-live": "polite" });
    const khuOng = el("div", {},
      nhom("Chọn ống nghiệm", dsOng),
      nhom("Thuốc thử", dsTt, el("div", { class: "ptn-hang", style: { marginTop: "8px" } },
        nut("Nhỏ vài giọt", () => mp.thaoTac("nho", { so: U.ongChon, thuoc_thu: U.thuocThu, kieu: "giot" }), { class: "btn btn-primary btn-sm" }),
        nut("Nhỏ dư", () => mp.thaoTac("nho", { so: U.ongChon, thuoc_thu: U.thuocThu, kieu: "du" })),
        nut("Đun nóng", () => mp.thaoTac("dun", { so: U.ongChon })),
        nut("Thử quỳ tím", () => mp.thaoTac("thu_quy", { so: U.ongChon })))),
      nhom("", el("p", { class: "ptn-ghi-chu", id: "nb-tieu-de-ls" }), lichSu),
      nhom("Kết luận: ống nào chứa ion nào?", el("p", { class: "ptn-ghi-chu", id: "nb-luot" }), duDoan,
        el("div", { class: "ptn-hang", style: { marginTop: "8px" } }, nut("Kiểm tra", () => { const r = mp.thaoTac("kiem_tra"); if (r?.thong_bao) toast(r.thong_bao, "ok"); }, { class: "btn btn-primary btn-sm" }),
          nut("Bộ ống mới", () => mp.thaoTac("bo_moi"))), kq));
    const dsMuoi = el("div", { class: "ptn-hang" }, ...tn.cau_hinh.kim_loai_ngon_lua.map((k) => chip(bi.ngon_lua[k].muoi, () => mp.thaoTac("thu_lua", { kim_loai: k }), { "data-kl": k })));
    const khuLua = el("div", { hidden: true },
      nhom("Thử màu ngọn lửa", el("p", { class: "ptn-ghi-chu", text: "Nhúng dây platinum sạch vào muối rồi đưa vào ngọn lửa đèn cồn." }), dsMuoi,
        el("div", { class: "ptn-hang", style: { marginTop: "8px" } }, nut("Rút dây ra", () => mp.thaoTac("tat_lua"))),
        el("div", { class: "ptn-so-lieu", style: { marginTop: "10px" } }, oSo("Màu ngọn lửa", "nb-lua", true))));
    pn.replaceChildren(nhom("", el("div", { class: "ptn-hang" }, tabOng, tabLua)), khuOng, khuLua);
    capNhatTt();
    let soOngTruoc = 0, daVeKq = false;
    const panel = {
      chonOng(so) { U.ongChon = so; $$("[data-ong]", dsOng).forEach((b) => b.setAttribute("aria-pressed", String(Number(b.dataset.ong) === so))); U.ve?.ct?.chonOng?.(so); if (U.s) panel.capNhat(U.s); },
      capNhat(s) {
        tabOng.setAttribute("aria-pressed", String(s.tab === "ong"));
        tabLua.setAttribute("aria-pressed", String(s.tab === "ngon_lua"));
        khuOng.hidden = s.tab !== "ong";
        khuLua.hidden = s.tab !== "ngon_lua";
        if (s.ong.length !== soOngTruoc || (!s.ket_qua && daVeKq)) {
          soOngTruoc = s.ong.length;
          daVeKq = false;
          dsOng.replaceChildren(...s.ong.map((o) => chip(`Ống ${o.so}`, () => panel.chonOng(o.so), { "data-ong": o.so })));
          duDoan.replaceChildren(...s.ong.map((o) => el("label", { class: "ptn-o-so" }, el("span", { text: `Ống ${o.so}` }),
            el("select", { class: "ptn-chon-o", "data-du-doan": o.so, on: { change: (e) => mp.thaoTac("dat_du_doan", { so: o.so, ion: e.target.value || null }) } },
              el("option", { value: "", text: "– ion? –" }), ...s.lua_chon_ion.map((i) => el("option", { value: i.id, text: i.ten }))))));
          if (!s.ong.some((o) => o.so === U.ongChon)) U.ongChon = 1;
          kq.replaceChildren();
          setTimeout(() => panel.chonOng(U.ongChon), 0);
        }
        $$("[data-ong]", dsOng).forEach((b) => b.setAttribute("aria-pressed", String(Number(b.dataset.ong) === U.ongChon)));
        $$("[data-du-doan]", duDoan).forEach((sel) => { sel.value = s.du_doan[sel.dataset.duDoan] || ""; sel.disabled = !!s.ket_qua; });
        const o = s.ong.find((x) => x.so === U.ongChon);
        datChu("nb-tieu-de-ls", o ? `Ống ${o.so}${o.mau_dd ? ` – dung dịch ${o.mau_dd.mo_ta}` : " – dung dịch không màu"}${o.ket_tua ? `, có kết tủa ${o.ket_tua.mau}` : ""}${o.khi ? ", đang sủi bọt khí" : ""}` : "");
        lichSu.replaceChildren(...(o?.lich_su.length ? o.lich_su.map((t) => el("li", { text: t })) : [el("li", { text: "Chưa thử thuốc thử nào." })]));
        datChu("nb-luot", `Đã thử ${s.so_luot_thu} lượt · ${s.ong.length} ống (miễn phí ${tn.cau_hinh.diem.luot_mien_phi_moi_ong * s.ong.length} lượt, sau đó trừ điểm).`);
        if (s.ket_qua && !daVeKq) {
          daVeKq = true;
          kq.replaceChildren(el("div", { class: "ptn-the-ket-qua", style: { marginTop: "10px" } },
            el("b", { text: `Đúng ${s.ket_qua.dung}/${s.ket_qua.tong} ống – ${s.ket_qua.diem} điểm (${s.ket_qua.so_luot_thu} lượt thử).` }),
            el("table", { class: "ptn-bang", style: { marginTop: "8px" } }, el("thead", {}, el("tr", {}, el("th", { text: "Ống" }), el("th", { text: "Chứa" }), el("th", { text: "Bạn chọn" }), el("th", { text: "Phương trình nhận biết" }))),
              el("tbody", {}, ...s.ket_qua.chi_tiet.map((c) => el("tr", {}, el("td", { text: String(c.so) }), el("td", { text: `${c.ten} (${c.muoi})` }),
                el("td", { class: c.dung ? "dung" : "sai", text: `${c.du_doan ? (bi.ion[c.du_doan]?.ten || c.du_doan) : "–"} ${c.dung ? "✓" : "✗"}` }),
                el("td", {}, ...c.phan_ung.map((p) => el("div", {}, el("code", { text: p.pt_ion }), el("div", { class: "ptn-ghi-chu", text: `${p.thuoc_thu}: ${p.hien_tuong}` }))))))))));
        }
        $$("[data-kl]", dsMuoi).forEach((b) => b.setAttribute("aria-pressed", String(s.ngon_lua?.kim_loai === b.dataset.kl)));
        datChu("nb-lua", s.ngon_lua ? `${s.ngon_lua.mau} – ${s.ngon_lua.ten}` : "chưa thử");
      },
    };
    return panel;
  },

  "pin-galvani": (pn, tn, mp) => {
    const ch = tn.cau_hinh;
    const chonKl = (ben) => el("select", { class: "ptn-chon-o", "aria-label": `Điện cực ${ben === "trai" ? "trái" : "phải"}`, "data-ben": ben,
      on: { change: (e) => mp.thaoTac("chon_kim_loai", { ben, kim_loai: e.target.value }) } },
      ...ch.kim_loai.map((k) => el("option", { value: k, text: `${k} (E° = ${dd(T.THE_DIEN_CUC[k].E0, 3)} V)` })));
    const kT = chonKl("trai"), kP = chonKl("phai");
    const truot = (ben) => {
      const r = el("input", { type: "range", class: "ptn-truot", min: "-3", max: "0", step: "0.1", value: "0", "aria-label": `Nồng độ ion cốc ${ben === "trai" ? "trái" : "phải"}` });
      r.addEventListener("input", () => mp.thaoTac("doi_nong_do", { ben, C: 10 ** Number(r.value) }));
      r.addEventListener("change", () => mp.thaoTac("ghi_nong_do"));
      return r;
    };
    const tT = truot("trai"), tP = truot("phai");
    const bCau = nut("Rút cầu muối", () => mp.thaoTac(U.s.cau_muoi ? "rut_cau_muoi" : "lap_cau_muoi"));
    const viSao = el("div", { class: "ptn-the-ket-qua", hidden: true }, el("b", { text: "Vì sao vôn kế về 0 V?" }),
      el("p", { class: "ptn-ghi-chu", text: "Electron đi ở mạch ngoài, nhưng trong dung dịch điện tích phải được trung hoà nhờ ion di chuyển qua cầu muối. Rút cầu muối → mạch hở, hai cốc tích điện trái dấu, phản ứng dừng ngay." }));
    pn.replaceChildren(
      nhom("Điện cực", el("div", { class: "ptn-so-lieu" }, el("label", { class: "ptn-o-so" }, el("span", { text: "Cốc trái" }), kT), el("label", { class: "ptn-o-so" }, el("span", { text: "Cốc phải" }), kP))),
      nhom("Vôn kế", el("div", { class: "ptn-so-lieu" }, oSo("E đo được", "pg-E"), oSo("E°pin (chuẩn)", "pg-E0"), oSo("Anode (−)", "pg-an"), oSo("Cathode (+)", "pg-ca"), oSo("Dòng electron (mạch ngoài)", "pg-e", true)),
        el("div", { class: "ptn-hang", style: { marginTop: "8px" } }, nut("Ghi số đo", () => { const r = mp.thaoTac("ghi_so_do"); if (r?.thong_bao) toast(`Đã ghi vào sổ tay: ${r.thong_bao}`); }, { class: "btn btn-primary btn-sm" }), bCau), viSao),
      nhom("Phương trình", el("p", { class: "ptn-pt", id: "pg-an-pt" }), el("p", { class: "ptn-pt", id: "pg-ca-pt", style: { marginTop: "6px" } }), el("p", { class: "ptn-pt", id: "pg-pt", style: { marginTop: "6px" } }),
        el("p", { class: "ptn-ghi-chu", text: "E°pin = E°(cathode) − E°(anode)" })),
      nhom("(Mở rộng) Nồng độ ion – phương trình Nernst", el("p", { class: "ptn-ghi-chu", text: "E = E° − (0,0592/n)·lg Q. Phần mở rộng, ngoài chương trình bắt buộc." }),
        el("label", { class: "ptn-ghi-chu", text: "Cốc trái: " }, el("b", { id: "pg-Ct" })), tT, el("label", { class: "ptn-ghi-chu", text: "Cốc phải: " }, el("b", { id: "pg-Cp" })), tP));
    return {
      capNhat(s) {
        if (kT.value !== s.trai) kT.value = s.trai;
        if (kP.value !== s.phai) kP.value = s.phai;
        datChu("pg-E", `${dd(s.E_pin, 3)} V`);
        datChu("pg-E0", `${dd(s.E0_pin, 3)} V`);
        datChu("pg-an", s.anode);
        datChu("pg-ca", s.cathode);
        datChu("pg-e", s.chieu_electron ? `${s.anode} → ${s.cathode} (qua dây dẫn)` : "không có dòng điện");
        datChu("pg-an-pt", `Anode: ${s.nua_anode}`);
        datChu("pg-ca-pt", `Cathode: ${s.nua_cathode}`);
        datChu("pg-pt", s.phuong_trinh);
        datChu("pg-Ct", `${dd(s.C_trai, 3)} M ${s.muoi_trai}`);
        datChu("pg-Cp", `${dd(s.C_phai, 3)} M ${s.muoi_phai}`);
        bCau.textContent = s.cau_muoi ? "Rút cầu muối" : "Lắp cầu muối";
        viSao.hidden = s.cau_muoi;
      },
    };
  },

  "dien-phan": (pn, tn, mp) => {
    const ch = tn.cau_hinh;
    const cBai = ch.bai.map((b) => chip(b.ten, () => mp.thaoTac("chon_bai", { id: b.id }), { "data-bai": b.id }));
    const oI = el("input", { type: "range", class: "ptn-truot", min: String(ch.I.tu), max: String(ch.I.den), step: String(ch.I.buoc), value: String(ch.I.mac_dinh), "aria-label": "Cường độ dòng điện I (A)" });
    oI.addEventListener("input", () => mp.thaoTac("dat_I", { I: oI.value }));
    const oT = el("input", { type: "number", min: String(ch.t.tu), max: String(ch.t.den), step: "1", value: String(ch.t.mac_dinh), "aria-label": "Thời gian điện phân t (giây)" });
    oT.addEventListener("change", () => mp.thaoTac("dat_t", { t: oT.value }));
    const cT = [965, 1930, 3860].map((t) => chip(`${t} s`, () => { oT.value = String(t); mp.thaoTac("dat_t", { t }); }));
    const bNguon = nut("Bật nguồn", () => mp.thaoTac(U.s.dang_chay ? "tat_nguon" : "bat_nguon"), { class: "btn btn-primary btn-sm" });
    const cTua = ch.toc_do.map((v) => chip(`×${v}`, () => mp.thaoTac("tua", { toc_do: v }), { "data-tua": v, "aria-label": `Tua nhanh gấp ${v} lần` }));
    const tienDo = el("div", { class: "ptn-tien-do", role: "progressbar", "aria-label": "Thời gian điện phân", "aria-valuemin": "0", "aria-valuemax": "100" }, el("span"));
    const dsCa = el("ul", { class: "ptn-ds-gon" }), dsAn = el("ul", { class: "ptn-ds-gon" });
    const oDc = el("input", { type: "text", inputmode: "decimal", placeholder: "0,64", "aria-label": "Giá trị bạn tính theo Faraday" });
    const phDc = el("p", { class: "ptn-phan-hoi" });
    pn.replaceChildren(
      nhom("Bài điện phân", el("div", { class: "ptn-hang" }, ...cBai)),
      nhom("Nguồn điện", el("label", { class: "ptn-ghi-chu" }, "Cường độ I = ", el("b", { id: "dp-I" })), oI,
        el("div", { class: "ptn-nhap", style: { marginTop: "8px" } }, el("span", { text: "Thời gian t =" }), oT, el("span", { text: "s" })),
        el("div", { class: "ptn-hang", style: { marginTop: "6px" } }, ...cT),
        el("div", { class: "ptn-hang", style: { marginTop: "10px" } }, bNguon, ...cTua),
        el("p", { class: "ptn-ghi-chu", id: "dp-t" }), tienDo),
      nhom("Kết quả (định luật Faraday, F = 96 485 C/mol)", el("div", { class: "ptn-so-lieu" }, oSo("n(e) = I·t/F", "dp-ne"), oSo("Cathode", "dp-ca"), oSo("Anode", "dp-an"), oSo("pH", "dp-pH"), oSo("Dung dịch", "dp-dd", true))),
      nhom("Thứ tự điện phân", el("div", { class: "ptn-so-lieu" }, el("div", { class: "ptn-o-so" }, el("span", { text: "Cathode (−)" }), dsCa), el("div", { class: "ptn-o-so" }, el("span", { text: "Anode (+)" }), dsAn))),
      nhom("Đối chiếu", el("p", { class: "ptn-ghi-chu", id: "dp-dc-hd" }), el("div", { class: "ptn-nhap" }, oDc, el("span", { id: "dp-dc-dv" }), nut("Kiểm tra", () => phanHoi(phDc, mp.thaoTac("doi_chieu", { gia_tri: oDc.value })))), phDc));
    let baiTruoc = "";
    return {
      capNhat(s) {
        const kq = s.ket_qua, nacl = s.bai === "nacl_mang_ngan", tan = s.bai === "cuso4_anode_cu";
        cBai.forEach((b) => b.setAttribute("aria-pressed", String(b.dataset.bai === s.bai)));
        if (Number(oI.value) !== s.I) oI.value = String(s.I);
        if (document.activeElement !== oT && Number(oT.value) !== s.t_muc_tieu) oT.value = String(s.t_muc_tieu);
        datChu("dp-I", `${dd(s.I, 1)} A`);
        bNguon.textContent = s.dang_chay ? "Tắt nguồn" : s.xong ? "Chạy lại" : "Bật nguồn";
        cTua.forEach((b) => b.setAttribute("aria-pressed", String(Number(b.dataset.tua) === s.toc_do)));
        const pt = Math.round((100 * s.t) / s.t_muc_tieu);
        tienDo.firstElementChild.style.width = `${pt}%`;
        tienDo.setAttribute("aria-valuenow", String(pt));
        datChu("dp-t", `t = ${Math.round(s.t)} / ${s.t_muc_tieu} s${s.dang_chay ? ` (đang chạy ×${s.toc_do})` : s.xong ? " – xong" : ""}`);
        datChu("dp-ne", `${dd(kq.n_e, 4)} mol`);
        datChu("dp-ca", nacl ? `H₂: ${dd(kq.V_khi_cathode, 3)} L` : `+${dd(kq.m_cathode, 3)} g Cu${kq.V_khi_cathode ? `; H₂ ${dd(kq.V_khi_cathode, 3)} L` : ""}`);
        datChu("dp-an", tan ? `−${dd(kq.m_anode_giam, 3)} g Cu (tan)` : `${kq.khi_anode}: ${dd(kq.V_khi_anode, 3)} L`);
        datChu("dp-pH", nacl ? `ngăn cathode ≈ ${dd(kq.pH_cathode, 1)}` : tan ? "gần như không đổi" : `≈ ${dd(kq.pH, 2)}`);
        datChu("dp-dd", nacl ? `Phenolphthalein ở cathode: ${s.chi_thi_cathode?.mo_ta || "không màu"} · quỳ ẩm ở anode: ${s.quy_anode}`
          : tan ? "Xanh lam, nồng độ Cu²⁺ không đổi" : `Cu²⁺ còn ${Math.round(kq.ti_le_Cu2 * 100)}% – màu xanh nhạt dần`);
        if (s.bai !== baiTruoc) {
          baiTruoc = s.bai;
          dsCa.replaceChildren(...s.thu_tu.cathode.map((x) => el("li", { text: x })));
          dsAn.replaceChildren(...s.thu_tu.anode.map((x) => el("li", { text: x })));
          datChu("dp-dc-hd", nacl ? "Tính thể tích H₂ (lít, đkc 24,79 L/mol) thoát ra ở cathode rồi nhập:" : "Tính khối lượng Cu (gam) bám vào cathode rồi nhập:");
          datChu("dp-dc-dv", nacl ? "L" : "g");
          phDc.textContent = "";
        }
      },
    };
  },
};

// ================================================================== CHUYỂN WEB 3D ⇄ UNITY 3D
async function kiemTraUnity() {
  const r = await fetch("/api/phong-thi-nghiem/cau-hinh", { credentials: "same-origin", cache: "no-store" });
  if (!r.ok) return;
  const ch = (U.cauHinh = await r.json());
  const hien = ch.unity_san_sang && (ch.unity_bat || ch.la_admin) && U.cheDoPhong === "tai_ban";
  const b = $("#ptn-nut-unity");
  if (b) {
    b.hidden = !hien;
    datChu("ptn-nhan-unity", `Web 3D ⇄ Unity 3D${!ch.unity_bat ? " (chỉ admin – đang tắt)" : ""}`);
    b.onclick = () => (U.kieuVe === "web" ? hoiMoUnity() : veWeb());
  }
}
function hoiMoUnity() {
  if (!matchMedia("(max-width: 900px)").matches) return moUnity();
  const hop = $("#ptn-hop-unity");
  hop.showModal();
  $("[data-dong]", hop).onclick = () => hop.close();
  $("#ptn-hop-unity-mo").onclick = () => { hop.close(); moUnity(); };
}
async function moUnity() {
  const tai = $("#ptn-dang-tai"), thanh = $(".ptn-thanh-tai", tai);
  tai.classList.remove("an");
  thanh.hidden = false;
  datChu("ptn-dang-tai-chu", "Đang tải bản Unity 3D…");
  try {
    const { PhongLabUnity } = await import("ptn/cau-unity");
    if (!U.unity) {
      U.unity = new PhongLabUnity($("#ptn-khung"), { tep: U.cauHinh.unity_tep, onThaoTac: thaoTacTuVe, onChon: chonTuVe,
        onTienTrinh: (p) => { thanh.firstElementChild.style.width = `${Math.round(p * 100)}%`; } });
      await U.unity.tai();
    }
    U.web.hien(false);
    U.unity.hien(true);
    U.ve = U.unity;
    U.kieuVe = "unity";
    U.ve.mo(U.id, U.du[U.id], U.mp[U.id].trangThai());   // lõi giữ trạng thái → Unity nhận lại đúng tiến trình
    toast("Đã chuyển sang bản Unity 3D.", "ok");
  } catch (e) {
    console.error(e);
    U.unity?.hien(false);
    veWeb();
    toast("Không tải được bản Unity – quay lại bản Web 3D.", "loi", 6000);
  } finally {
    tai.classList.add("an");
    thanh.hidden = true;
  }
}
function veWeb() {
  U.unity?.hien(false);
  U.web.hien(true);
  U.ve = U.web;
  U.kieuVe = "web";
  U.cheDoPhong = "tai_ban";
  U.web.vaoTram(U.id, { tucThi: true });
}

