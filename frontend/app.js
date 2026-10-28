// CHEMISTRY – giao diện (JS thuần)
"use strict";

const $ = (s) => document.querySelector(s);

const CHUOI_MAU = [
  { nhan: "metan → etyl axetat", chuoi: "CH4 → C2H2 → CH3CHO → C2H5OH → CH3COOH → CH3COOC2H5" },
  { nhan: "etilen → axit axetic", chuoi: "C2H4 → C2H5OH → CH3CHO → CH3COOH" },
  { nhan: "benzen → anilin", chuoi: "C6H6 → C6H5NO2 → C6H5NH2" },
  { nhan: "tinh bột → etanol → axit axetic", chuoi: "(C6H10O5)n → C6H12O6 → C2H5OH → CH3COOH" },
  { nhan: "propen + tác nhân → A, B, C (C3H6O), D", chuoi: "CH2=CH–CH3 →[HBr] A →[NaOH, t°] B →[CuO, t°] C (C3H6O) →[HCN] D" },
];

const GIAI_DOAN = [
  { id: "phan_tich", ten: "Phân tích" },
  { id: "tra_cuu", ten: "Tra cứu" },
  { id: "giai", ten: "Giải" },
  { id: "kiem_tra", ten: "Kiểm tra" },
  { id: "ve", ten: "Vẽ" },
  { id: "xong", ten: "Xong" },
];

let anhDe = null;       // File ảnh đề
let jobHienTai = null;
let nguonSuKien = null;
let hengio = null;
let coChemDraw = true;

// ------------------------------------------------------------ tiện ích
function esc(s) {
  return String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

// Công thức hoá học → HTML: số sau chữ cái/ngoặc là chỉ số dưới (CH₃COOH), hệ số đầu chất giữ nguyên,
// (..)n → n dưới, điện tích Mn2+/H+/OH- → chỉ số trên. Giống image_export.tach_cong_thuc.
function ctHTML(text) {
  const t = String(text ?? "").replace(/[₀-₉]/g, (c) => String(c.charCodeAt(0) - 0x2080));
  const dienTich = /([A-Za-z)\]])(\d*[+-])(?=$|[\s,.;)])/g;
  let out = "";
  const parts = [];
  for (const m of t.matchAll(dienTich)) {
    // ion nhiều nguyên tố: chữ số ngay trước dấu là chỉ số dưới (NH4+ → NH₄⁺), trừ khi có ≥ 2 chữ số (SO42- → SO₄²⁻)
    let i = m.index + m[1].length;
    const so = m[2].slice(0, -1);
    const tu = (t.slice(0, i).match(/[A-Za-z0-9()[\]]*$/) || [""])[0];
    if (so && (tu.match(/[A-Z]/g) || []).length > 1) i += so.length - (so.length >= 2 ? 1 : 0);
    parts.push({ i, j: m.index + m[0].length });
  }
  const sup = new Map(parts.map((p) => [p.i, p.j]));
  for (let i = 0; i < t.length; ) {
    if (sup.has(i)) {
      out += "<sup>" + esc(t.slice(i, sup.get(i))) + "</sup>";
      i = sup.get(i);
      continue;
    }
    const ch = t[i];
    const prev = i ? t[i - 1] : "";
    if (/\d/.test(ch) && prev && (/[A-Za-z]/.test(prev) || ")]".includes(prev))) {
      let j = i;
      while (j < t.length && /\d/.test(t[j]) && (j === i || !sup.has(j))) j++;
      out += "<sub>" + t.slice(i, j) + "</sub>";
      i = j;
      continue;
    }
    if (ch === "n" && prev === ")" && !/[A-Za-z]/.test(t[i + 1] || "")) {
      out += "<sub>n</sub>";
      i++;
      continue;
    }
    out += esc(ch);
    i++;
  }
  return out;
}

function toast(noiDung, loai = "tin", thoiGian = 6000) {
  const el = document.createElement("div");
  el.className = `toast ${loai}`;
  el.innerHTML = noiDung;
  $("#toasts").appendChild(el);
  setTimeout(() => el.remove(), thoiGian);
}

function linkHoa(s) {
  return esc(s).replace(/(https?:\/\/[^\s<]+)/g, '<a href="$1" target="_blank" rel="noopener">$1</a>');
}

// ------------------------------------------------------------ trạng thái hệ thống
async function kiemTraHeThong() {
  try {
    const r = await fetch("/api/suc-khoe");
    const d = await r.json();
    const toi = TaiKhoan.toi || {};
    const chuApp = !!toi.la_chu_app;
    coChemDraw = d.chemdraw && chuApp; // nút ChemDraw mở ChemDraw trên máy chạy app → chỉ chủ app
    const pill = (ok, chu, tieuDe) => `<span class="pill ${ok ? "ok" : "tat"}" title="${esc(tieuDe)}"><span class="den"></span>${esc(chu)}</span>`;
    const cl = toi.claude || {};
    const kc = toi.claude_key || {};
    const cr0 = toi.claude_rieng || {};
    const cr = { ...cr0, co: !!(cr0.duoc_phep && cr0.co && cr0.dung) }; // key riêng chỉ tính khi admin đã bật + "Dùng key của tôi"
    const coClaude = cl.co_the_dung || kc.da_mo || cr.co;
    $("#trang-thai-he-thong").innerHTML = (toi.key?.co
      ? pill(true, `Gemini · key của bạn …${toi.key.cuoi}`, "Bài giải dùng key Gemini riêng của bạn (mô hình chọn theo độ khó)")
      : pill(d.co_gemini, "Gemini · theo độ khó", d.co_gemini ? "Dễ → Flash-Lite · Trung bình → Flash · HSG → Pro" : "Chủ app chưa có GEMINI_API_KEY"))
      + pill((d.co_claude || cr.co) && coClaude, cl.la_admin ? "Claude · admin" : cr.co ? `Claude · key của bạn …${cr.cuoi}`
        : kc.da_mo ? `Claude · hôm nay còn ${kc.con_hom_nay}/${kc.gioi_han}`
        : cl.co_the_dung ? `Claude · còn ${cl.con_luot} lượt` : "Claude · cần key / mã",
        coClaude ? "Bật “Dùng Claude” ở mục 02" : "Nhập Key Claude hoặc mã Claude trong Cài đặt để dùng Claude");
    if (d.co_openai && !toi.key?.co) $("#trang-thai-he-thong").innerHTML += pill(true, "ChatGPT · dự phòng", "AI dự phòng khi Claude / Gemini lỗi (thứ tự do admin đặt)");
    if (chuApp) $("#trang-thai-he-thong").innerHTML += pill(d.chemdraw, "ChemDraw", d.chemdraw ? "Đã tìm thấy ChemDraw" : "Không thấy ChemDraw");
    if (d.co_chrome === false) {
      const nhanDemo = $("#che-do-demo");
      if (nhanDemo) nhanDemo.closest("label")?.setAttribute("hidden", "");
      const hc = $("#hien-chrome");
      if (hc) {
        hc.disabled = true;
        hc.title = "Cần cài đặt Google Chrome trên máy để xem Chrome khi tra cứu";
      }
    }
    if (chuApp && !toi.key?.co && !d.co_claude && !d.co_gemini && !d.co_openai) {
      toast("Chưa có API key nào trong file <b>.env</b>. Hãy thêm ANTHROPIC_API_KEY (hoặc GEMINI_API_KEY / OPENAI_API_KEY) rồi khởi động lại, hoặc thêm key Gemini của bạn trong <a href=\"/cai-dat#muc-key\">Cài đặt</a>.", "loi", 12000);
    }
  } catch {
    toast("Không kết nối được server. Hãy chạy lại start.command.", "loi", 10000);
  }
}

// ------------------------------------------------------------ key dùng để giải (đăng nhập, key riêng, dùng thử)
function goiYLoi(ma, thongDiep = "") {
  const toi = TaiKhoan.toi || {};
  if (ma === "can_dang_nhap" || (!toi.dang_nhap && /đăng nhập/i.test(thongDiep))) {
    return ` <a href="${esc(TaiKhoan.urlDangNhap())}">Đăng nhập →</a>`;
  }
  if (["het_luot_dung_thu", "can_key", "key_hong"].includes(ma) || /Cài đặt|key Gemini của bạn/i.test(thongDiep)) {
    return ' <a href="/cai-dat#muc-key">Mở Cài đặt →</a>';
  }
  return "";
}

function veTrangThaiKey(t) {
  const el = $("#trang-thai-key");
  if (!el) return;
  const dt = t.dung_thu || {};
  let lop = "", html = "";
  switch (t.nguon_key_giai) {
    case "ca_nhan":
      html = `Key Gemini của bạn (…${esc(t.key.cuoi)}) · <a href="/cai-dat#muc-key">Đổi key</a>`;
      break;
    case "chu_app":
      html = "Chủ app · key .env · không giới hạn";
      break;
    case "ki_cuu":
      html = "Kì cựu · key chủ app · không giới hạn";
      break;
    case "dung_thu":
      html = `Dùng thử: còn <b>${dt.con_lai}/${dt.gioi_han}</b> bài · <a href="/cai-dat#muc-key">Thêm key</a>`;
      break;
    case "het_luot":
      lop = "loi";
      html = `Hết bài dùng thử · <a href="/cai-dat#muc-key">Thêm key</a>`;
      break;
    default: // can_dang_nhap
      lop = "canh-bao";
      html = `Cần đăng nhập · ${TaiKhoan.nutDangNhap("Đăng nhập", "btn btn-sm nut-trong-dong")}`;
  }
  el.className = `trang-thai-key ${lop}`.trim();
  el.innerHTML = `<span class="cham" aria-hidden="true"></span><span>${html}</span>`;
}

// Mở bài trong lịch sử (/?bai=<job_id>) và báo lỗi đăng nhập (/?dang_nhap=loi)
async function moBaiTuUrl() {
  const q = new URLSearchParams(location.search);
  if (q.get("dang_nhap") === "loi") {
    toast("Đăng nhập Google không thành công (hoặc email chưa được xác minh). Hãy thử lại.", "loi", 9000);
    q.delete("dang_nhap");
    history.replaceState(null, "", location.pathname + (q.toString() ? "?" + q : "") + location.hash);
  }
  const bai = q.get("bai");
  if (!bai || !/^[\w-]{10,60}$/.test(bai)) return;
  try {
    const r = await fetch(`/api/ket-qua/${encodeURIComponent(bai)}`);
    const d = await r.json();
    if (!r.ok) throw new Error(d.loi || "Không mở được bài này.");
    hienKetQua(d);
  } catch (e) {
    toast("⚠ " + esc(e.message) + goiYLoi("", e.message), "loi", 9000);
  }
}

// ------------------------------------------------------------ 01 nhập đề: văn bản | hình ảnh | chuỗi mẫu
let cachNhap = "van-ban";

function chonCachNhap(cach, focus = false) {
  cachNhap = cach;
  document.querySelectorAll(".the-chon").forEach((t) => {
    const dang = t.dataset.cach === cach;
    t.classList.toggle("dang", dang);
    t.setAttribute("aria-selected", dang ? "true" : "false");
  });
  $("#khung-van-ban").classList.toggle("hidden", cach !== "van-ban");
  $("#khung-hinh-anh").classList.toggle("hidden", cach !== "hinh-anh");
  $("#khung-mau").classList.toggle("hidden", cach !== "mau");
  if (focus && cach === "van-ban") $("#chuoi").focus();
  capNhatTomTat();
}

// Dòng tóm tắt cạnh nút Giải: đang giải đề nào
function capNhatTomTat() {
  const chuoi = $("#chuoi").value.trim();
  let tt;
  if (cachNhap === "hinh-anh") tt = anhDe ? `Đề: ảnh “${anhDe.name || "ảnh dán"}”` : "Chưa chọn ảnh đề";
  else tt = chuoi ? `Đề: ${chuoi.length > 70 ? chuoi.slice(0, 70) + "…" : chuoi}` : "Chưa nhập chuỗi";
  $("#tom-tat-nhap").textContent = tt;
  const coDe = cachNhap === "hinh-anh" ? !!anhDe : !!chuoi;
  document.querySelectorAll('.muc-ben[data-muc="muc-nhap"]').forEach((m) => m.classList.toggle("xong", coDe));
}

function khoiTaoNhapLieu() {
  document.querySelectorAll(".the-chon").forEach((t) => (t.onclick = () => chonCachNhap(t.dataset.cach, true)));

  const vung = $("#chuoi-mau");
  for (const m of CHUOI_MAU) {
    const b = document.createElement("button");
    b.type = "button";
    b.className = "the-mau";
    b.innerHTML = `<span class="the-mau-ten">${esc(m.nhan)}</span><span class="the-mau-chuoi">${ctHTML(m.chuoi)}</span>`;
    b.onclick = () => {
      $("#chuoi").value = m.chuoi;
      document.querySelectorAll(".the-mau").forEach((x) => x.classList.toggle("dang", x === b));
      chonCachNhap("van-ban");
      toast(`Đã chọn chuỗi mẫu “${esc(m.nhan)}”. Bấm <b>Giải chuỗi</b> ở mục 02.`, "ok", 3000);
    };
    vung.appendChild(b);
  }

  // chèn chữ tại con trỏ; lui = số kí tự lùi con trỏ lại (để gõ tác nhân ngay trong [ ])
  const chen = (chu, lui = 0) => {
    const o = $("#chuoi");
    const a = o.selectionStart ?? o.value.length;
    const b = o.selectionEnd ?? o.value.length;
    o.value = o.value.slice(0, a) + chu + o.value.slice(b);
    o.focus();
    o.selectionStart = o.selectionEnd = a + chu.length - lui;
    capNhatTomTat();
  };
  $("#nut-mui-ten").onclick = () => chen(" → ");
  $("#nut-tac-nhan").onclick = () => chen(" →[] ", 2);
  $("#chuoi").addEventListener("input", capNhatTomTat);

  $("#nut-tai-anh").onclick = () => $("#file-anh").click();
  $("#file-anh").onchange = (e) => e.target.files[0] && chonAnh(e.target.files[0]);
  $("#nut-bo-anh").onclick = () => chonAnh(null);

  // kéo-thả ảnh vào bất kì đâu trong mục 01, hoặc dán ảnh → tự chuyển sang cách nhập "Hình ảnh"
  const vungAnh = $("#muc-nhap");
  ["dragenter", "dragover"].forEach((ev) => vungAnh.addEventListener(ev, (e) => { e.preventDefault(); $("#vung-anh").classList.add("keo"); }));
  ["dragleave", "drop"].forEach((ev) => vungAnh.addEventListener(ev, (e) => { e.preventDefault(); $("#vung-anh").classList.remove("keo"); }));
  vungAnh.addEventListener("drop", (e) => {
    const f = [...(e.dataTransfer?.files || [])].find((x) => x.type.startsWith("image/"));
    if (f) chonAnh(f);
  });
  document.addEventListener("paste", (e) => {
    const f = [...(e.clipboardData?.files || [])].find((x) => x.type.startsWith("image/"));
    if (!f) return;
    if (mucHienTai === "muc-co-che" || e.target.closest?.("#muc-co-che")) { // mục Cơ chế: dán được nhiều ảnh (tối đa 4)
      themAnhCoChe([...e.clipboardData.files].filter((x) => x.type.startsWith("image/")));
      toast("Đã dán ảnh đề cho mục Cơ chế.", "ok", 2500);
      return;
    }
    if (mucHienTai === "muc-3d" || e.target.closest?.("#muc-3d")) { chonAnh3D(f); toast("Đã dán ảnh cho mô hình 3D.", "ok", 2500); }
    else { chonAnh(f); toast("Đã dán ảnh đề.", "ok", 2500); }
  });

  $("#form-giai").addEventListener("submit", (e) => { e.preventDefault(); batDauGiai(); });
  document.addEventListener("keydown", (e) => {
    if (e.key !== "Enter" || !(e.metaKey || e.ctrlKey)) return;
    e.preventDefault();
    if (e.target.closest?.("#muc-3d") || (mucHienTai === "muc-3d" && !e.target.closest?.("#form-giai"))) dung3D();
    else if (e.target.closest?.("#muc-co-che") || (mucHienTai === "muc-co-che" && !e.target.closest?.("#form-giai"))) veCoChe();
    else batDauGiai();
  });
  capNhatTomTat();
}

function chonAnh(file) {
  if (file && file.size > 10 * 1024 * 1024) { toast("Ảnh quá lớn (tối đa 10 MB).", "loi"); return; }
  anhDe = file;
  if (file) {
    $("#anh-thu-nho").src = URL.createObjectURL(file);
    $("#ten-anh").textContent = file.name || "ảnh dán";
    $("#anh-trong").classList.add("hidden");
    $("#anh-xem").classList.remove("hidden");
    $("#anh-xem").classList.add("flex");
    chonCachNhap("hinh-anh");
  } else {
    $("#file-anh").value = "";
    $("#anh-xem").classList.add("hidden");
    $("#anh-xem").classList.remove("flex");
    $("#anh-trong").classList.remove("hidden");
    capNhatTomTat();
  }
}

// ------------------------------------------------------------ cột bên: mục đang xem + tiến độ
const CAC_MUC = ["muc-nhap", "muc-tuy-chon", "muc-ket-qua", "muc-3d", "muc-co-che", "muc-tim-cau-tao"];
let mucHienTai = "muc-nhap";

function datMucBen(id) {
  const nhomCu = NHOM_CUA_MUC[mucHienTai];
  mucHienTai = id;
  document.querySelectorAll(".muc-ben").forEach((m) => {
    const dang = m.dataset.muc === id;
    m.classList.toggle("dang", dang);
    if (dang) m.setAttribute("aria-current", "step"); else m.removeAttribute("aria-current");
  });
  const i = CAC_MUC.indexOf(id);
  if (i >= 0) $("#so-muc-ben").textContent = String(i + 1).padStart(2, "0");
  // sang bước thuộc mục cha khác (vd 03 → 04) thì mở mục đó, mục kia tự thu lại
  if (NHOM_CUA_MUC[id] && NHOM_CUA_MUC[id] !== nhomCu) moNhomBen(NHOM_CUA_MUC[id]);
  danhDauNhomBen(id);
}

// Mục lục dạng thư mục: "Giải chuỗi" (01–03), "Mô hình 3D" (04), "Cơ chế" (05), "Tìm cấu tạo chất" (06)
const NHOM_CUA_MUC = { "muc-nhap": "giai", "muc-tuy-chon": "giai", "muc-ket-qua": "giai", "muc-3d": "3d", "muc-co-che": "co-che", "muc-tim-cau-tao": "tim-cau-tao" };

function moNhomBen(nhom) { // nhom = null → thu lại hết
  document.querySelectorAll(".nhom-ben").forEach((g) => {
    const mo = g.dataset.nhom === nhom;
    g.classList.toggle("mo", mo);
    g.querySelector(".nut-nhom").setAttribute("aria-expanded", String(mo));
  });
}

function danhDauNhomBen(id) { // chấm xanh cạnh mục cha đang thu lại mà chứa bước hiện tại
  document.querySelectorAll(".nhom-ben").forEach((g) => g.classList.toggle("co-buoc-dang", g.dataset.nhom === NHOM_CUA_MUC[id]));
}

function khoiTaoNhomBen() {
  const nav = $(".ds-muc-ben.co-nhom");
  if (!nav) return;
  nav.querySelectorAll(".ds-con").forEach((ds) => [...ds.children].forEach((a, i, tatCa) => {
    a.style.setProperty("--i", i); // thứ tự hiện lần lượt
    a.style.setProperty("--n", tatCa.length);
  }));
  nav.querySelectorAll(".nut-nhom").forEach((nut) => nut.addEventListener("click", () => {
    const g = nut.closest(".nhom-ben");
    moNhomBen(g.classList.contains("mo") ? null : g.dataset.nhom);
  }));
  // mới vào trang: mở sẵn mục chứa bước hiện tại (theo #hash nếu có), không chạy hiệu ứng
  const dau = NHOM_CUA_MUC[location.hash.slice(1)] ? location.hash.slice(1) : mucHienTai;
  nav.classList.add("tat-hieu-ung");
  moNhomBen(NHOM_CUA_MUC[dau]);
  danhDauNhomBen(dau);
  requestAnimationFrame(() => requestAnimationFrame(() => nav.classList.remove("tat-hieu-ung")));
}

function khoiTaoCotBen() {
  khoiTaoNhomBen();
  const muc = CAC_MUC.map((id) => document.getElementById(id));
  if (!("IntersectionObserver" in window)) return;
  const dangThay = new Map();
  const io = new IntersectionObserver((ds) => {
    ds.forEach((d) => dangThay.set(d.target.id, d.isIntersecting));
    // mục đầu tiên (từ trên xuống) đang nằm trong vùng giữa màn hình
    const id = muc.map((m) => m.id).find((x) => dangThay.get(x));
    if (id) datMucBen(id);
  }, { rootMargin: "-30% 0px -55% 0px" });
  muc.forEach((m) => io.observe(m));
}

// ------------------------------------------------------------ tiến trình
function veCacBuocTienTrinh() {
  $("#cac-buoc-tien-trinh").innerHTML = GIAI_DOAN.map((g, i) =>
    `<li class="buoc-tt" data-id="${g.id}"><span class="cham">${String(i + 1).padStart(2, "0")}</span><span class="ten">${g.ten}</span></li>`).join("");
  $("#so-giai-doan").textContent = "01";
}

function datTrangThai(id, lop) {
  const el = document.querySelector(`.buoc-tt[data-id="${id}"]`);
  if (!el) return;
  el.classList.remove("dang", "xong", "loi", "canh-bao");
  if (lop) el.classList.add(lop);
  if (lop === "xong") el.querySelector(".cham").textContent = "✓";
}

function ghiNhatKy(ev) {
  const nk = $("#nhat-ky");
  const dong = document.createElement("div");
  const doc = ev.thong_diep.startsWith("Đang đọc") || ev.thong_diep.startsWith("Đang mở");
  dong.className = `dong ${doc ? "doc" : ev.trang_thai}`;
  const gio = new Date(ev.t * 1000).toLocaleTimeString("vi-VN", { hour12: false });
  dong.innerHTML = `<span class="gio">${gio}</span><span>${linkHoa(ev.thong_diep)}</span>`;
  nk.appendChild(dong);
  nk.scrollTop = nk.scrollHeight;
}

// ------------------------------------------------------------ mức độ đề (Dễ / Trung bình / HSG) + công tắc Claude
const TEN_MUC = { de: "Dễ", trung_binh: "Trung bình", hsg: "HSG" };
const THU_TU_MUC = ["de", "trung_binh", "hsg"];

function nhanMucDo(el, muc) {
  el.hidden = !TEN_MUC[muc];
  el.className = `huy-hieu nhan-muc-do ${muc === "hsg" ? "do" : muc === "trung_binh" ? "vang" : "xanh"}`;
  el.textContent = TEN_MUC[muc] || "";
  el.title = "Mức độ đề – quyết định mô hình AI dùng để giải";
}

function veCongTacClaude(t) {
  const c = t.claude || {};      // mã Claude (chỉ bài HSG)
  const k = t.claude_key || {};  // Key Claude dùng chung (mọi bài, giới hạn bài/ngày)
  const r0 = t.claude_rieng || {}; // API key Claude riêng (admin đã bật + người dùng bật "Dùng key của tôi")
  const r = { ...r0, co: !!(r0.duoc_phep && r0.co && r0.dung) };
  const coKey = !!k.da_mo && !k.la_admin;
  const hien = !!(t.dang_nhap && (c.co_the_dung || k.da_mo || r.co));
  $("#khung-dung-claude").hidden = !hien; // chỉ hiện khi backend cho biết tài khoản dùng được Claude
  if (!hien) { $("#dung-claude").checked = false; return; }
  $("#nhan-dung-claude").textContent = c.la_admin || coKey || r.co ? "Dùng Claude" : "Dùng Claude cho bài HSG";
  const phan = [];
  if (r.co && !c.la_admin) phan.push(`key của bạn …${r.cuoi}`);
  if (coKey && !r.co) phan.push(k.con_hom_nay > 0 ? `hôm nay còn ${k.con_hom_nay}/${k.gioi_han} bài` : "hôm nay đã hết lượt – sẽ dùng Gemini");
  if (c.co_the_dung && !c.la_admin) phan.push(`${coKey ? "mã: bài HSG, " : ""}còn ${c.con_luot} lượt · hết hạn ${String(c.het_han || "").split("-").reverse().join("/")}`);
  $("#luot-claude").textContent = c.la_admin ? "admin" : (phan.length ? `(${phan.join(" · ")})` : "");
  $("#luot-claude").className = c.la_admin ? "huy-hieu-admin" : "ghi-chu-nho";
}

// ------------------------------------------------------------ thanh mức độ thông minh: Nhanh · Cân bằng · Kỹ · Tối đa
const MUC_TM = ["nhanh", "can_bang", "ky", "toi_da"];
const MO_TA_TM = {
  nhanh: "~20–40 giây · Gemini Flash-Lite, không suy nghĩ thêm, bỏ bước kiểm tra",
  can_bang: "~30–60 giây · Gemini Flash, suy nghĩ ít, kiểm tra nhanh (RDKit + mô hình rẻ khi cần)",
  ky: "~1–2 phút · Gemini Pro (hoặc Claude nếu bạn được phép và bật “Dùng Claude”), suy nghĩ vừa, có kiểm tra",
  toi_da: "~2–4 phút · mô hình mạnh nhất bạn được phép, suy nghĩ nhiều, kiểm tra kỹ",
};
let coQuyenToiDa = false;

function veUocTinh(muc) {
  const tuDong = $("#tm-tu-dong").checked;
  $("#khung-thong-minh").classList.toggle("tu-dong", tuDong);
  document.querySelectorAll(".nhan-tm span").forEach((x) => x.classList.toggle("dang", !tuDong && MUC_TM[Number(x.dataset.i)] === muc));
  $("#tm-uoc-tinh").textContent = tuDong && !muc
    ? "Tự động: đề Dễ → Nhanh, Trung bình → Cân bằng, HSG → Kỹ (kéo thanh để tự chọn)."
    : (tuDong ? "Tự động → " : "") + `${$(`.nhan-tm span[data-i="${MUC_TM.indexOf(muc)}"]`).textContent}: ${MO_TA_TM[muc]}`;
}

function veQuyenToiDa(t) { // Tối đa chỉ mở cho admin / người có Key Claude, key riêng hoặc mã Claude
  const c = t.claude || {}, k = t.claude_key || {}, r = t.claude_rieng || {};
  coQuyenToiDa = !!(t.dang_nhap && (c.la_admin || c.co_the_dung || k.da_mo || (r.duoc_phep && r.co && r.dung)));
  $("#tm-toi-da").classList.toggle("mo", !coQuyenToiDa);
  $("#tm-toi-da").title = coQuyenToiDa ? "" : "Cần quyền Claude (Key Claude, API key Claude riêng hoặc mã Claude)";
  if (!coQuyenToiDa && Number($("#thanh-thong-minh").value) === 3) { $("#thanh-thong-minh").value = 2; veUocTinh("ky"); }
}

function khoiTaoThongMinh() {
  const thanh = $("#thanh-thong-minh");
  thanh.addEventListener("input", () => {
    if (Number(thanh.value) === 3 && !coQuyenToiDa) {
      thanh.value = 2;
      toast("Mức Tối đa cần quyền Claude (Key Claude, API key Claude riêng hoặc mã Claude) – đang dùng mức Kỹ.", "tin", 6000);
    }
    $("#tm-tu-dong").checked = false;
    veUocTinh(MUC_TM[Number(thanh.value)]);
  });
  $("#tm-tu-dong").addEventListener("change", () => veUocTinh($("#tm-tu-dong").checked ? "" : MUC_TM[Number(thanh.value)]));
  document.querySelectorAll(".nhan-tm span").forEach((x) => x.addEventListener("click", () => {
    thanh.value = x.dataset.i;
    thanh.dispatchEvent(new Event("input"));
  }));
  veUocTinh("");
}

// ------------------------------------------------------------ tra cứu tài liệu khoa học (công tắc tự động theo mức / DOI, nguồn tham khảo, PDF của bạn)
const MAU_DOI = /\b10\.\d{4,9}\/\S+/;
let tlTuDong = true;       // chưa bấm công tắc → backend tự quyết (bật ở Kỹ / Tối đa hoặc khi đề có DOI)
let tlCoCheTuDong = true;
let pdfBaiBao = null;      // { file, doi } – "Tải lên PDF của bạn" cho bài chỉ có tóm tắt (chỉ dùng cho lượt giải kế tiếp)

function veCongTacTL() {
  if (!tlTuDong) return;
  const muc = $("#tm-tu-dong").checked ? "" : MUC_TM[Number($("#thanh-thong-minh").value)];
  $("#tra-cuu-tl").checked = MAU_DOI.test($("#chuoi").value) || muc === "ky" || muc === "toi_da";
  $("#tl-ghi-chu").textContent = MAU_DOI.test($("#chuoi").value) ? "(tự bật: đề có DOI)"
    : muc ? `(tự động theo mức ${$(`.nhan-tm span[data-i="${MUC_TM.indexOf(muc)}"]`).textContent})` : "(tự động: bật ở mức Kỹ / Tối đa hoặc khi đề có DOI)";
}

function htmlNguon(ds, coNutPdf) {
  return ds.map((b) => `<li class="nguon-bai"><b>[${b.so}] ${esc(b.tieu_de)}</b>
      <span class="ghi-chu-nho">${esc([b.tac_gia, b.tap_chi, b.nam].filter(Boolean).join(" · "))}${b.trich_dan ? ` · ${b.trich_dan} trích dẫn` : ""}</span>
      <span class="flex flex-wrap items-center gap-2 mt-1">
        ${b.url ? `<a href="${esc(b.url)}" target="_blank" rel="noopener" class="lien-ket font-mono text-xs">${esc(b.doi ? "doi:" + b.doi : b.url)}</a>` : ""}
        ${b.toan_van ? `<span class="huy-hieu xanh">Đọc toàn văn (${esc(b.nguon_toan_van === "PDF bạn tải lên" ? "PDF bạn tải lên" : "open access")})</span>`
          : '<span class="huy-hieu vang">Chỉ đọc tóm tắt</span>'}
        ${coNutPdf && b.can_tai_pdf ? `<button type="button" class="btn btn-sm nut-pdf-bai" data-doi="${esc(b.doi)}">Tải lên PDF của bạn</button>` : ""}
      </span></li>`).join("");
}

function khoiTaoTraCuuTL() {
  $("#tra-cuu-tl").addEventListener("change", () => { tlTuDong = false; $("#tl-ghi-chu").textContent = "(bạn chọn)"; });
  $("#tra-cuu-tl-co-che").addEventListener("change", () => { tlCoCheTuDong = false; });
  $("#chuoi").addEventListener("input", veCongTacTL);
  $("#thanh-thong-minh").addEventListener("input", veCongTacTL);
  $("#tm-tu-dong").addEventListener("change", veCongTacTL);
  $("#nguon").addEventListener("click", (e) => {
    const nut = e.target.closest(".nut-pdf-bai");
    if (!nut) return;
    $("#file-pdf-bai").dataset.doi = nut.dataset.doi;
    $("#file-pdf-bai").click();
  });
  $("#file-pdf-bai").addEventListener("change", (e) => {
    const f = e.target.files[0];
    e.target.value = "";
    if (!f) return;
    if (!/pdf$/i.test(f.type || f.name)) { toast("Hãy chọn file PDF.", "loi"); return; }
    if (f.size > 25 * 1024 * 1024) { toast("File PDF quá lớn (tối đa 25 MB).", "loi"); return; }
    pdfBaiBao = { file: f, doi: $("#file-pdf-bai").dataset.doi || "" };
    toast(`Đang giải lại dựa trên toàn văn “${esc(f.name)}” (chỉ dùng cho lượt này)…`, "tin", 5000);
    batDauGiai();
  });
  veCongTacTL();
}

function veNangMuc(muc) { // kết quả: nâng mức thủ công nếu thấy phân loại sai → giải lại
  const el = $("#nang-muc");
  const cao = THU_TU_MUC.slice(THU_TU_MUC.indexOf(muc) + 1).filter((m) => m !== "de");
  el.hidden = !TEN_MUC[muc] || !cao.length;
  el.innerHTML = el.hidden ? "" : `Phân loại sai? Nâng mức rồi giải lại: ${cao.map((m) =>
    `<button type="button" class="btn btn-sm" data-nang-muc="${m}">${TEN_MUC[m]}</button>`).join(" ")}`;
}

function xuLySuKien(ev) {
  ghiNhatKy(ev);
  if (ev.muc_do) nhanMucDo($("#nhan-muc-do-tt"), ev.muc_do);
  if (ev.muc_tm && $("#tm-tu-dong").checked) { $("#thanh-thong-minh").value = MUC_TM.indexOf(ev.muc_tm); veUocTinh(ev.muc_tm); }
  if (ev.chuoi_tam || ev.buoc_tam) veChuoiTam(ev.chuoi_tam || [], ev.buoc_tam || []);
  const idx = GIAI_DOAN.findIndex((g) => g.id === ev.giai_doan);
  if (ev.giai_doan === "loi") {
    const dang = document.querySelector(".buoc-tt.dang");
    if (dang) datTrangThai(dang.dataset.id, "loi");
    return;
  }
  if (idx < 0) return;
  $("#so-giai-doan").textContent = String(idx + 1).padStart(2, "0");
  $("#giai-doan-ben").textContent = GIAI_DOAN[idx].ten;
  $("#thanh-tien-do-ben").style.width = `${Math.round(((ev.trang_thai === "xong" ? idx + 1 : idx + 0.5) / GIAI_DOAN.length) * 100)}%`;
  GIAI_DOAN.slice(0, idx).forEach((g) => {
    const el = document.querySelector(`.buoc-tt[data-id="${g.id}"]`);
    if (el && !el.classList.contains("xong")) datTrangThai(g.id, "xong");
  });
  if (ev.trang_thai === "xong") datTrangThai(ev.giai_doan, "xong");
  else if (ev.trang_thai === "canh_bao") {
    const el = document.querySelector(`.buoc-tt[data-id="${ev.giai_doan}"]`);
    if (el && !el.classList.contains("xong")) { datTrangThai(ev.giai_doan, "dang"); }
  } else datTrangThai(ev.giai_doan, "dang");
}

function dangChay(bat) {
  $("#nut-giai").disabled = bat;
  $("#nut-giai .nhan").textContent = bat ? "Đang giải..." : "Giải chuỗi";
  $("#nut-giai .quay").classList.toggle("hidden", !bat);
  clearInterval(hengio);
  $("#tien-do-ben").classList.toggle("hidden", !bat);
  if (bat) {
    const t0 = performance.now();
    $("#giai-doan-ben").textContent = GIAI_DOAN[0].ten;
    $("#thanh-tien-do-ben").style.width = "4%";
    hengio = setInterval(() => {
      const giay = ((performance.now() - t0) / 1000).toFixed(1) + " s";
      $("#dong-ho").textContent = giay;
      $("#dong-ho-ben").textContent = giay;
    }, 100);
  }
}

async function batDauGiai() {
  if ($("#nut-giai").disabled) return;
  const dungAnh = cachNhap === "hinh-anh";
  const chuoi = (dungAnh ? $("#ghi-chu-anh").value : $("#chuoi").value).trim();
  const toi = TaiKhoan.toi;
  if (toi && !toi.dang_nhap) {
    toast("Hãy đăng nhập để giải bài." + goiYLoi("can_dang_nhap"), "loi", 9000);
    $("#trang-thai-key")?.scrollIntoView({ behavior: "smooth", block: "center" });
    return;
  }
  if (dungAnh && !anhDe) { toast("Hãy chọn ảnh đề (mục 01 → Hình ảnh).", "loi"); chonCachNhap("hinh-anh"); $("#muc-nhap").scrollIntoView({ behavior: "smooth" }); return; }
  if (!dungAnh && !chuoi) { toast("Hãy nhập chuỗi phản ứng (mục 01), hoặc chọn một chuỗi mẫu.", "loi"); chonCachNhap("van-ban", true); $("#muc-nhap").scrollIntoView({ behavior: "smooth" }); return; }
  if (nguonSuKien) nguonSuKien.close();

  const fd = new FormData();
  fd.append("chuoi", chuoi);
  fd.append("hien_chrome", $("#hien-chrome").checked);
  const nguonGiai = document.querySelector('input[name="nguon-giai"]:checked')?.value || "ai";
  fd.append("demo", nguonGiai === "demo");
  if (["ai_rieng", "gemini", "openai"].includes(nguonGiai)) fd.append("ai", nguonGiai); // backend kiểm tra lại quyền AI Riêng
  try { localStorage.setItem("giai-chuoi-ai", nguonGiai); } catch { /* bỏ qua */ }
  fd.append("muc_do", document.querySelector('input[name="muc-do"]:checked')?.value || "");
  fd.append("dung_claude", !$("#khung-dung-claude").hidden && $("#dung-claude").checked); // backend kiểm tra lại quyền Claude
  fd.append("muc_thong_minh", $("#tm-tu-dong").checked ? "" : MUC_TM[Number($("#thanh-thong-minh").value)]);
  fd.append("tra_cuu_tai_lieu", pdfBaiBao ? "1" : tlTuDong ? "" : $("#tra-cuu-tl").checked ? "1" : "0"); // "" = tự động (backend quyết)
  if (pdfBaiBao) { fd.append("pdf", pdfBaiBao.file, pdfBaiBao.file.name || "bai-bao.pdf"); fd.append("pdf_doi", pdfBaiBao.doi); pdfBaiBao = null; }
  if (dungAnh) fd.append("anh", anhDe, anhDe.name || "de.png");

  document.querySelectorAll('.muc-ben[data-muc="muc-tuy-chon"]').forEach((m) => m.classList.add("xong"));
  document.querySelectorAll('.muc-ben[data-muc="muc-ket-qua"]').forEach((m) => m.classList.remove("xong"));
  $("#muc-ket-qua").scrollIntoView({ behavior: "smooth" });
  $("#cho-ket-qua").classList.add("hidden");
  $("#the-tien-trinh").classList.remove("hidden");
  $("#nhat-ky").innerHTML = "";
  $("#chuoi-tam").innerHTML = "";
  $("#chuoi-tam").classList.add("hidden");
  $("#nhan-muc-do-tt").hidden = true;
  veCacBuocTienTrinh();
  dangChay(true);

  let jobId;
  try {
    const r = await TaiKhoan.goi("/api/giai", { method: "POST", body: fd });
    const d = await r.json();
    if (!r.ok) throw Object.assign(new Error(d.loi || "Không gửi được yêu cầu."), { ma: d.ma });
    jobId = d.job_id;
    if (d.thong_bao_claude) toast("⚠ " + esc(d.thong_bao_claude), "tin", 9000);
  } catch (e) {
    dangChay(false);
    toast(esc(e.message || "Không kết nối được server.") + goiYLoi(e.ma, e.message), "loi", 10000);
    TaiKhoan.tai().catch(() => {});
    return;
  }
  jobHienTai = jobId;
  TaiKhoan.tai().catch(() => {}); // cập nhật số lượt dùng thử còn lại

  let coLoi = null, loaiLoi = "";
  nguonSuKien = new EventSource(`/api/tien-trinh/${jobId}`);
  nguonSuKien.onmessage = (m) => {
    const ev = JSON.parse(m.data);
    if (ev.giai_doan === "loi") { coLoi = ev.thong_diep; loaiLoi = ev.loai_loi || ""; }
    xuLySuKien(ev);
  };
  nguonSuKien.addEventListener("ket_thuc", async () => {
    nguonSuKien.close();
    dangChay(false);
    TaiKhoan.tai().catch(() => {}); // bài dùng thử lỗi vì phía app thì được trả lượt
    if (coLoi) {
      toast("⚠ " + esc(coLoi) + (loaiLoi === "chua_day"  // AI Riêng chưa được dạy phần này → một nút giải lại bằng AI thường
        ? '<div class="mt-2"><button type="button" class="btn btn-sm" data-giai-ai-thuong>Giải bằng AI thường</button></div>'
        : goiYLoi("", coLoi)), "loi", loaiLoi === "chua_day" ? 20000 : 12000);
      if ($("#ket-qua").classList.contains("hidden")) $("#cho-ket-qua").classList.remove("hidden");
      return;
    }
    await taiKetQua(jobId);
  });
  nguonSuKien.onerror = () => {
    if (nguonSuKien.readyState === EventSource.CLOSED) return;
    nguonSuKien.close();
    dangChay(false);
    toast("Mất kết nối tới server trong khi giải. Hãy thử lại.", "loi");
  };
}

// ------------------------------------------------------------ kết quả
async function taiKetQua(jobId) {
  const r = await fetch(`/api/ket-qua/${jobId}`);
  const d = await r.json();
  if (!r.ok) { toast(esc(d.loi || "Không lấy được kết quả."), "loi"); return; }
  hienKetQua(d);
  toast("Đã giải xong! Kết quả ở mục 03.", "ok", 3500);
}

let loiGiaiHienTai = null;

// ------------------------------------------------------------ kết quả dạng CHUỖI: A1 —[phản ứng, điều kiện]→ A2 …
function gonCua(d) { // bài mới: d.gon; bài cũ / demo: dựng từ loi_giai
  if (d.gon) return d.gon;
  const s = d.loi_giai || { so_do: [], cac_buoc: [] };
  return { chuoi: s.so_do.map((n) => ({ ki_hieu: n.ki_hieu, ctct: n.cong_thuc, ten: n.ten, smiles: n.smiles })),
    buoc: s.cac_buoc.map((b) => ({ tu: b.chat_dau, den: b.chat_sau, phan_ung: b.loai_phan_ung, dieu_kien: b.dieu_kien })),
    luu_y: s.luu_y && s.luu_y.length ? s.luu_y : (s.ghi_chu ? [s.ghi_chu] : []), dap_an: s.dap_an || "" };
}

function htmlChuoi(g, anh = {}, loi = {}, coNut = false) {
  const chat = Object.fromEntries((g.chuoi || []).map((c) => [c.ki_hieu, c]));
  const nut = (kh) => {
    const c = chat[kh] || { ki_hieu: kh };
    const hinh = anh[c.smiles] ? `<img class="anh-phan-tu" src="${anh[c.smiles]}" alt="${esc(c.ten || kh)}" loading="lazy">` : "";
    return `<div class="nut-chuoi" title="${esc(c.ten ? `${kh}: ${c.ten}` : kh)}">${hinh}<b class="ki-hieu-chuoi">${ctHTML(kh)}</b>
      ${c.ctct && c.ctct !== kh ? `<span class="ctct">${ctHTML(c.ctct)}</span>` : ""}${c.ten ? `<span class="ten-chuoi">${esc(c.ten)}</span>` : ""}
      ${coNut && c.smiles ? `<button type="button" class="btn btn-sm nut-xem-3d" data-smiles="${esc(c.smiles)}" data-ten="${esc(c.ten || kh)}" title="Xem mô hình 3D">3D</button>` : ""}</div>`;
  };
  const buoc = g.buoc || [];
  if (!buoc.length) return (g.chuoi || []).map((c) => nut(c.ki_hieu)).join("");
  let html = "";
  buoc.forEach((b, i) => {
    if (i === 0 || b.tu !== buoc[i - 1].den) html += (i ? '<span class="ngat-chuoi"></span>' : "") + nut(b.tu);
    html += `<div class="mui-chuoi${loi[i] ? " co-loi" : ""}" title="${esc((loi[i] || []).join("; "))}">
        <span class="so-buoc-chuoi">(${i + 1})</span>${b.phan_ung ? `<b>${ctHTML(b.phan_ung)}</b>` : ""}
        ${b.dieu_kien ? `<span>${ctHTML(b.dieu_kien)}</span>` : ""}<i class="duong-mui"></i>
        ${b.can_cu ? `<span class="can-cu${/suy/i.test(b.can_cu) ? " suy-luan" : ""}" title="${/suy/i.test(b.can_cu) ? "AI tự suy luận (tài liệu không nói tới)" : "Lấy từ bài báo trong mục Nguồn tham khảo"}">${esc(b.can_cu)}</span>` : ""}
        ${coNut ? `<button type="button" class="btn btn-sm nut-co-che-buoc" data-buoc="${i}" title="Vẽ cơ chế phản ứng này ở mục 05">Cơ chế</button>` : ""}</div>`;
    html += nut(b.den);
  });
  return html;
}

function veChuoiTam(chuoi, buoc) { // streaming: hiện từng chất / bước ngay khi AI viết xong
  const el = $("#chuoi-tam");
  el.classList.remove("hidden");
  el.innerHTML = `<p class="nhan-mono">Đang hiện dần lời giải</p><div class="so-do-chuoi mt-2">${htmlChuoi({ chuoi, buoc })}</div>`;
}

function vePhuongTrinh(ds) {
  const el = $("#pt-chi-tiet");
  el.classList.toggle("hidden", !ds.length);
  el.innerHTML = ds.map((x) => `<li><span class="so">(${x.buoc})</span><span>${ctHTML(x.phuong_trinh)}${x.ghi_chu
    ? `<br><small class="ghi-chu-nho">${ctHTML(x.ghi_chu)}</small>` : ""}</span></li>`).join("");
}

async function xemPhuongTrinh() {
  const nut = $("#nut-pt");
  if (!jobHienTai || nut.disabled) return;
  nut.disabled = true;
  nut.querySelector(".quay").classList.remove("hidden");
  try {
    const r = await TaiKhoan.goi(`/api/phuong-trinh/${jobHienTai}`, { method: "POST" });
    const d = await r.json().catch(() => ({}));
    if (!r.ok) throw new Error(d.loi || "Không lấy được phương trình.");
    vePhuongTrinh(d.phuong_trinh || []);
    nut.classList.add("hidden");
  } catch (e) {
    toast("⚠ " + esc(e.message), "loi", 8000);
  } finally {
    nut.disabled = false;
    nut.querySelector(".quay").classList.add("hidden");
  }
}

function hienKetQua(d) {
  const sol = d.loi_giai;
  loiGiaiHienTai = sol;
  jobHienTai = d.job_id;
  const g = gonCua(d);
  const loi = Object.fromEntries((sol.cac_buoc || []).map((b, i) => [i, b.loi && b.loi.length ? b.loi : null]).filter((x) => x[1]));
  $("#so-do-chuoi").innerHTML = htmlChuoi(g, d.anh || {}, loi, true);
  $("#chuoi-tam").classList.add("hidden");
  $("#khoi-luu-y").classList.toggle("hidden", !(g.luu_y || []).length);
  $("#luu-y").innerHTML = (g.luu_y || []).map((x) => `<li>${ctHTML(x)}</li>`).join("");
  $("#khoi-dap-an").classList.toggle("hidden", !g.dap_an);
  $("#dap-an").innerHTML = ctHTML(g.dap_an || "");
  const coSan = d.phuong_trinh || (sol.cac_buoc.some((b) => b.phuong_trinh)
    ? sol.cac_buoc.map((b) => ({ buoc: b.so, phuong_trinh: b.phuong_trinh, ghi_chu: b.giai_thich })) : null);
  vePhuongTrinh(coSan || []);
  $("#nut-pt").classList.toggle("hidden", !!coSan);
  const tk = d.tokens || {};
  $("#thong-tin-giai").textContent = d.muc_thong_minh || d.ai_rieng
    ? `Mức ${d.ten_muc_thong_minh} · ${d.thoi_gian_giai} giây` + (d.tu_cache ? " · kết quả đã lưu (không gọi AI)" : tk.so_lan ? ` · ${tk.so_lan} lần gọi AI, ${Number((tk.vao || 0) + (tk.ra || 0)).toLocaleString("vi-VN")} token` : "")
    : "";
  $("#cho-ket-qua").classList.add("hidden");
  $("#ket-qua").classList.remove("hidden");

  if (window.chemistryApp && typeof window.chemistryApp.thongBao === "function") {
    if (document.hidden) {
      window.chemistryApp.thongBao("CHEMISTRY · Giải chuỗi hoàn tất", "Đã tìm thấy lời giải chuỗi phản ứng hoá học.");
    }
  }

  const t = Date.now();
  $("#anh-dap-an").src = d.tep.png + "?t=" + t;
  $("#lien-ket-anh").href = d.tep.png;
  $("#nut-tai-png").href = d.tep.tai_png;
  $("#nut-tai-anh-goc").href = d.tep.tai_png;
  $("#nut-tai-zip").href = d.tep.tai_zip;
  $("#nut-chemdraw").dataset.job = d.job_id;
  const nutBaoSai = $("#nut-bao-sai");
  if (nutBaoSai) {
    nutBaoSai.dataset.job = d.job_id;
    nutBaoSai.hidden = !!d.demo;
  }
  $("#mo-hinh").textContent = d.demo ? "Chế độ demo – lời giải mẫu soạn sẵn, không gọi AI"
    : `Giải bởi ${d.mo_hinh}` + (d.nang_cap ? " (đã tự nâng mô hình vì lời giải đầu còn lỗi)" : "");
  nhanMucDo($("#nhan-muc-do-kq"), d.demo ? "" : d.muc_do);
  veNangMuc(d.demo ? "" : d.muc_do);
  $("#chan-trang").textContent = d.demo
    ? "Lời giải mẫu soạn sẵn (chế độ demo, không gọi AI) – vui lòng kiểm tra lại"
    : "Lời giải do AI tạo – vui lòng kiểm tra lại";

  veAIRieng(d);

  const cb = $("#canh-bao");
  if (d.canh_bao && d.canh_bao.length) {
    cb.classList.remove("hidden");
    cb.innerHTML = `<b>⚠ Cần kiểm tra lại:</b><ul class="list-disc ml-5 mt-1">${d.canh_bao.map((x) => `<li>${ctHTML(x)}</li>`).join("")}</ul>`;
  } else cb.classList.add("hidden");

  $("#cac-buoc").innerHTML = ""; // định dạng cũ (từng bước + phương trình đầy đủ) đã thay bằng sơ đồ chuỗi ở trên

  $("#khoi-chat-an").classList.add("hidden"); // chất ẩn đã nằm trong sơ đồ chuỗi

  const gc = $("#ghi-chu");
  gc.classList.toggle("hidden", !sol.ghi_chu);
  gc.querySelector(".noi-dung-ghi-chu").innerHTML = ctHTML(sol.ghi_chu || "");

  $("#nguon").innerHTML = (d.tai_lieu || []).length ? htmlNguon(d.tai_lieu, true) : (sol.nguon_tham_khao || []).map((n) =>
    /^https?:\/\//.test(n) ? `<li><a href="${esc(n)}" target="_blank" rel="noopener">${esc(n)}</a></li>` : `<li>${esc(n)}</li>`
  ).join("") || '<li class="text-lang">Không có</li>';
  $("#nguon-ghi-chu").classList.toggle("hidden", !(d.tai_lieu || []).some((b) => b.can_tai_pdf));

  $("#nut-chemdraw").setAttribute("aria-disabled", coChemDraw ? "false" : "true");
  document.querySelectorAll('.muc-ben[data-muc="muc-ket-qua"]').forEach((m) => m.classList.add("xong"));
  $("#ket-qua").scrollIntoView({ behavior: "smooth" });
}

// ------------------------------------------------------------ AI Riêng: chọn AI giải, khung "kiến thức đã dùng" + đối chiếu Gemini
const MO_TA_NGUON_GIAI = {
  ai_rieng: "Dùng kiến thức giáo viên đã dạy, có Gemini đối chiếu.",
  ai: "Lần lượt các AI theo thứ tự admin đặt, lỗi thì chuyển AI kế.",
  gemini: "Gemini giải trước.",
  openai: "ChatGPT giải trước.",
  demo: "Lời giải mẫu, không gọi AI.",
};
function capNhatGhiChuChonAI() {
  const el = $("#ghi-chu-chon-ai");
  if (!el) return;
  const val = document.querySelector('input[name="nguon-giai"]:checked')?.value || "ai";
  el.textContent = MO_TA_NGUON_GIAI[val] || "";
}

let daChonAI = false; // chỉ đặt lựa chọn mặc định một lần (không ghi đè khi người dùng đã bấm chọn)
function veChonAI(t) {
  const coAR = !!(t.ai_rieng && t.ai_rieng.co);
  $("#lua-ai-rieng").hidden = !coAR;
  $("#lua-chatgpt").hidden = !t.co_openai;
  const dangChon = document.querySelector('input[name="nguon-giai"]:checked');
  if (dangChon && dangChon.closest("label").hidden) document.querySelector('input[name="nguon-giai"][value="ai"]').checked = true;
  if (!daChonAI) {
    daChonAI = true;
    let luu = null;
    try { luu = localStorage.getItem("giai-chuoi-ai"); } catch { /* bỏ qua */ }
    const o = luu && document.querySelector(`input[name="nguon-giai"][value="${luu}"]`);
    if (o && !o.closest("label").hidden) o.checked = true;
    else if (coAR) document.querySelector('input[name="nguon-giai"][value="ai_rieng"]').checked = true; // mặc định: AI Riêng
  }
  capNhatGhiChuChonAI();
}

const KET_LUAN_DC = {
  dong_thuan: ["✓ Gemini đối chiếu: đồng thuận", "xanh"], doi_chieu_nen: ["✓ Chắc chắn – mọi bước có căn cứ trong kiến thức đã dạy", "xanh"],
  trong_tai: ["⚖ Hai AI khác nhau – mô hình mạnh đã phân xử", "vang"], khac_nhau: ["⚠ AI Riêng và Gemini khác nhau – nên kiểm tra lại", "do"],
  chi_mot_ben: ["Gemini đối chiếu: chỉ một lời giải qua kiểm tra RDKit", "vang"],
};
function veAIRieng(d) {
  const k = $("#khoi-ai-rieng");
  const ar = d.ai_rieng;
  if (!ar) { k.classList.add("hidden"); k.innerHTML = ""; return; }
  const laAdmin = !!(TaiKhoan.toi && TaiKhoan.toi.claude && TaiKhoan.toi.claude.la_admin);
  const dc = ar.doi_chieu || {};
  const [kl, mau] = KET_LUAN_DC[dc.ket_luan] || ["", ""];
  const nguon = (ar.nguon || []).filter((n) => n.da_dung || ar.tang !== "thu_vien");
  const dongNguon = (n) => {
    const ten = `<b>[${esc(n.nhan)}]</b> ${esc(n.ten_loai)}: ${ctHTML(n.tieu_de)}${n.trang ? ` · tr. ${esc(n.trang)}` : ""}`;
    const link = laAdmin && n.id ? ` <a class="lien-ket" href="/admin/ai-rieng#muc=${encodeURIComponent(n.loai)}:${encodeURIComponent(n.id)}" target="_blank" rel="noopener">mở</a>` : "";
    return `<li class="${n.da_dung ? "da-dung" : ""}" title="${n.da_dung ? "Lời giải dựa vào mục này" : "Đã tra nhưng lời giải không dùng"}">${ten}${link}</li>`;
  };
  k.innerHTML = `<div class="flex flex-wrap items-center gap-2"><span class="nhan-mono">AI Riêng</span>
      <span class="huy-hieu ${ar.tang === "thu_vien" ? "xanh" : ""}">${esc(ar.ten_tang || "")}</span>${kl ? `<span class="huy-hieu ${mau}">${esc(kl)}</span>` : ""}</div>
    ${dc.loai_ben ? `<p class="ghi-chu-nho mt-2">${dc.loai_ben.ten === "gemini" ? "Lời giải Gemini" : "Lời giải AI Riêng"} bị loại: ${ctHTML(dc.loai_ben.ly_do)}</p>` : ""}
    ${(dc.khac || []).length ? `<p class="ghi-chu-nho mt-2">Khác nhau ở: ${dc.khac.map((x) => `${esc(x.ki_hieu)} (AI Riêng ${ctHTML(x.ai_rieng)} / Gemini ${ctHTML(x.gemini)})`).join("; ")}</p>` : ""}
    ${(ar.chua_duoc_day || []).length ? `<p class="ghi-chu-nho mt-2 chu-loi">Chưa được dạy: ${ar.chua_duoc_day.map((x) => ctHTML(x)).join("; ")}</p>` : ""}
    ${(ar.mau_thuan || []).length ? `<p class="ghi-chu-nho mt-2">Theo kiến thức giáo viên (khác hiểu biết chung): ${ar.mau_thuan.map((x) => ctHTML(x)).join("; ")}</p>` : ""}
    ${nguon.length ? `<p class="ghi-chu-nho mt-3">Kiến thức đã dùng${nguon.some((n) => !n.da_dung) ? " (mờ = đã tra, không dùng)" : ""}:</p>
      <ul class="ds-nguon-ar mt-1">${nguon.map(dongNguon).join("")}</ul>` : `<p class="ghi-chu-nho mt-2">Kho chưa có mục nào liên quan – lời giải dùng kiến thức chung.</p>`}`;
  k.classList.remove("hidden");
  const tieuDe = Object.fromEntries((ar.nguon || []).map((n) => [n.nhan, `${n.ten_loai}: ${n.tieu_de}`]));
  document.querySelectorAll("#so-do-chuoi .can-cu").forEach((el) => { // căn cứ từng bước: [Q1] → tên quy tắc
    const nhan = el.textContent.match(/[SQMT]\d+/g) || [];
    el.title = nhan.length ? nhan.map((x) => tieuDe[x] || x).join(" · ") : el.textContent;
    el.classList.toggle("suy-luan", /kiến thức chung|chưa được dạy|suy luận/i.test(el.textContent)); // «tên mục» (từ thư viện) vẫn là căn cứ
  });
}

document.addEventListener("click", (e) => {
  if (!e.target.closest("[data-giai-ai-thuong]")) return;
  document.querySelector('input[name="nguon-giai"][value="ai"]').checked = true;
  e.target.closest(".toast")?.remove();
  batDauGiai();
});

$("#nut-chemdraw").addEventListener("click", async (e) => {
  const job = e.currentTarget.dataset.job;
  if (!job) return;
  try {
    const r = await TaiKhoan.goi(`/api/chemdraw/${job}`, { method: "POST" });
    const d = await r.json();
    toast(esc(d.thong_bao || d.loi || "Đã gửi lệnh mở ChemDraw."), d.ok ? "ok" : "tin");
  } catch {
    toast("Không gửi được lệnh mở ChemDraw.", "loi");
  }
});

const nutBaoSaiEl = $("#nut-bao-sai");
if (nutBaoSaiEl) {
  nutBaoSaiEl.addEventListener("click", async (e) => {
    const job = e.currentTarget.dataset.job;
    if (!job) return;
    const ghiChu = prompt("Góp ý / báo sai lời giải cho giáo viên quản trị:\n(Ví dụ: Sai chất B, phản ứng này cần đun nóng...)");
    if (ghiChu === null) return;
    try {
      const r = await TaiKhoan.goi("/api/bao-sai", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ job_id: job, ghi_chu: ghiChu.trim() }),
      });
      const res = await r.json();
      if (r.ok) {
        toast("Đã gửi phản ánh tới giáo viên quản trị! Cảm ơn bạn.", "ok");
      } else {
        toast(esc(res.loi || "Không gửi được phản ánh."), "loi");
      }
    } catch {
      toast("Lỗi kết nối khi gửi phản ánh.", "loi");
    }
  });
}

$("#nut-mo-het").addEventListener("click", (e) => {
  const tatCa = [...document.querySelectorAll("details.buoc")];
  const mo = tatCa.some((x) => !x.open);
  tatCa.forEach((x) => (x.open = mo));
  e.currentTarget.textContent = mo ? "Đóng tất cả" : "Mở tất cả";
});

// ------------------------------------------------------------ 04 mô hình 3D (3Dmol.js, tải khi cần + tải trước thông minh)
const THU_VIEN_3D = "https://cdn.jsdelivr.net/npm/3dmol@2.5.3/build/3Dmol-min.js";
const GOI_Y_3D = ["ethanol", "axit axetic", "benzen", "glucozơ", "CH3COOC2H5", "aspirin", "caffeine", "CC(C)(O)C#N"];
const TEN_NGUYEN_TO = {
  H: "Hiđro", C: "Cacbon", N: "Nitơ", O: "Oxi", F: "Flo", P: "Photpho", S: "Lưu huỳnh",
  Cl: "Clo", Br: "Brom", I: "Iot", Na: "Natri", K: "Kali", Ca: "Canxi", Mg: "Magie", Fe: "Sắt"
};
let cach3D = "van-ban";
let anh3D = null;
let viewer3D = null;
let duLieu3D = null;
let hua3D = null;
const duLieu3DCache = new Map(); // Bộ nhớ đệm client giúp chuyển đổi giữa các chất ngay lập tức (0ms)
let cheDoDo = null; // null | "khoang-cach" | "goc"
let nguyenTuDaChon = []; // danh sách nguyên tử đang chọn để đo
let cacPhepDo = []; // lưu các phép đo đã thực hiện: { loai, diem: [], ketQua, nhan }

function taiThuVien3D() {
  if (window.$3Dmol) return Promise.resolve();
  if (!hua3D) {
    hua3D = new Promise((ok, loi) => {
      const s = document.createElement("script");
      s.src = THU_VIEN_3D;
      s.onload = ok;
      s.onerror = () => { hua3D = null; loi(new Error("Không tải được thư viện 3D (3Dmol.js) – cần kết nối Internet.")); };
      document.head.appendChild(s);
    });
  }
  return hua3D;
}

// Tải trước thư viện 3D ở nền khi trình duyệt rảnh rỗi để loại bỏ độ trễ khi người dùng mở mục 3D
if (typeof window !== "undefined") {
  if ("requestIdleCallback" in window) {
    requestIdleCallback(() => taiThuVien3D().catch(() => {}), { timeout: 4000 });
  } else {
    setTimeout(() => taiThuVien3D().catch(() => {}), 2500);
  }
}

function bienCss(ten) { return getComputedStyle(document.documentElement).getPropertyValue(ten).trim(); }

function capNhatNen3D() {
  if (!viewer3D) return;
  viewer3D.setBackgroundColor(bienCss("--giay-lom"));
  veKieu3D();
}

function chonCach3D(cach) {
  cach3D = cach;
  document.querySelectorAll("[data-cach3d]").forEach((t) => {
    const dang = t.dataset.cach3d === cach;
    t.classList.toggle("dang", dang);
    t.setAttribute("aria-selected", dang ? "true" : "false");
  });
  $("#khung-3d-van-ban").classList.toggle("hidden", cach !== "van-ban");
  $("#khung-3d-hinh-anh").classList.toggle("hidden", cach !== "hinh-anh");
}

function chonAnh3D(file) {
  if (file && file.size > 10 * 1024 * 1024) { toast("Ảnh quá lớn (tối đa 10 MB).", "loi"); return; }
  anh3D = file;
  if (file) {
    $("#anh-3d-thu-nho").src = URL.createObjectURL(file);
    $("#ten-anh-3d").textContent = file.name || "ảnh dán";
    $("#anh-3d-trong").classList.add("hidden");
    $("#anh-3d-xem").classList.remove("hidden");
    $("#anh-3d-xem").classList.add("flex");
    chonCach3D("hinh-anh");
  } else {
    $("#file-anh-3d").value = "";
    $("#anh-3d-xem").classList.add("hidden");
    $("#anh-3d-xem").classList.remove("flex");
    $("#anh-3d-trong").classList.remove("hidden");
  }
}

function layMau3D() {
  const luaChon = $("#mau-3d")?.value || "Jmol";
  if (luaChon === "carbon-toi") {
    return {
      colorscheme: {
        prop: "elem",
        map: {
          C: "#2c3038", H: "#ffffff", O: "#e63946", N: "#3a86ff", S: "#ffb703",
          Cl: "#06d6a0", Br: "#9d0208", F: "#70e000", P: "#fb8500", I: "#7209b7"
        }
      }
    };
  }
  return { colorscheme: luaChon };
}

function veKieu3D() {
  if (!viewer3D || !duLieu3D) return;
  const kieu = document.querySelector('input[name="kieu-3d"]:checked')?.value || "bong-que";
  const mau = layMau3D();
  const st = {
    "bong-que": { stick: { radius: 0.14, ...mau }, sphere: { scale: 0.27, ...mau } },
    que: { stick: { radius: 0.19, ...mau } },
    dac: { sphere: { scale: 1.0, ...mau } },
    day: { line: { linewidth: 2, ...mau } },
  }[kieu];
  viewer3D.setStyle({}, st);
  viewer3D.removeAllLabels();
  viewer3D.removeAllShapes();

  // Nhãn nguyên tố
  if ($("#nhan-3d").checked) {
    for (const a of viewer3D.selectedAtoms({})) {
      if (a.elem === "H") continue;
      viewer3D.addLabel(a.elem, {
        position: { x: a.x, y: a.y, z: a.z }, inFront: true, alignment: "center", fontSize: 13,
        fontColor: bienCss("--muc"), backgroundColor: bienCss("--giay"), backgroundOpacity: 0.8, borderThickness: 0,
      });
    }
  }

  // Vẽ các phép đo khoảng cách và góc hiện có
  for (const pd of cacPhepDo) {
    if (pd.loai === "khoang-cach" && pd.diem.length === 2) {
      const [p1, p2] = pd.diem;
      viewer3D.addLine({ start: p1, end: p2, color: "#10b981", dashed: true, linewidth: 3 });
      const mid = { x: (p1.x + p2.x) / 2, y: (p1.y + p2.y) / 2, z: (p1.z + p2.z) / 2 };
      viewer3D.addLabel(pd.ketQua, {
        position: mid, inFront: true, alignment: "center", fontSize: 13,
        fontColor: "#064e3b", backgroundColor: "#d1fae5", backgroundOpacity: 0.9, borderThickness: 1, borderColor: "#10b981"
      });
    } else if (pd.loai === "goc" && pd.diem.length === 3) {
      const [p1, p2, p3] = pd.diem;
      viewer3D.addLine({ start: p1, end: p2, color: "#3b82f6", dashed: true, linewidth: 2.5 });
      viewer3D.addLine({ start: p3, end: p2, color: "#3b82f6", dashed: true, linewidth: 2.5 });
      viewer3D.addLabel(pd.ketQua, {
        position: p2, inFront: true, alignment: "center", fontSize: 13,
        fontColor: "#1e3a8a", backgroundColor: "#dbeafe", backgroundOpacity: 0.9, borderThickness: 1, borderColor: "#3b82f6"
      });
    }
  }

  // Đánh dấu các nguyên tử đang chọn dở trong lượt đo
  for (let i = 0; i < nguyenTuDaChon.length; i++) {
    const a = nguyenTuDaChon[i];
    viewer3D.addLabel(`[${i + 1}]`, {
      position: { x: a.x, y: a.y, z: a.z }, inFront: true, alignment: "center", fontSize: 12,
      fontColor: "#ffffff", backgroundColor: "#f59e0b", backgroundOpacity: 0.95,
    });
  }

  // Bề mặt phân tử (Surface)
  capNhatBeMat3D();
  viewer3D.render();
}

function capNhatBeMat3D() {
  if (!viewer3D || !duLieu3D || !window.$3Dmol) return;
  try {
    viewer3D.removeAllSurfaces();
    const beMat = $("#be-mat-3d")?.value || "none";
    if (beMat === "none") return;

    const mau = layMau3D();
    const mapType = {
      vdw: $3Dmol.SurfaceType.VDW,
      sas: $3Dmol.SurfaceType.SAS,
      ses: $3Dmol.SurfaceType.SES,
    }[beMat];

    if (mapType != null) {
      viewer3D.addSurface(mapType, {
        opacity: 0.76,
        ...mau,
      });
    }
  } catch (err) {
    console.warn("Lỗi dựng bề mặt 3D:", err);
  }
}

// ------------------------------------------------------------ Tương tác nguyên tử & Đo đạc
function khoiTaoTuongTac3D() {
  if (!viewer3D) return;

  const badge = $("#badge-hover-3d");
  const wrapper = $("#khung-3d-wrapper");

  // Rê chuột xem thông số nguyên tử
  viewer3D.setHoverable({}, true, (atom, viewer, event, container) => {
    if (!atom || !badge) return;
    const tenTiengViet = TEN_NGUYEN_TO[atom.elem] || atom.elem;
    const soHieu = atom.serial != null ? atom.serial : (atom.index != null ? atom.index + 1 : "");
    const dienTich = atom.charge != null ? ` · Điện tích: ${atom.charge > 0 ? "+" : ""}${atom.charge.toFixed(2)}` : "";
    badge.innerHTML = `<span class="ten-elem">${atom.elem} (${tenTiengViet}) #${soHieu}</span>` +
                      `<span class="chi-tiet-elem">X: ${atom.x.toFixed(2)}  Y: ${atom.y.toFixed(2)}  Z: ${atom.z.toFixed(2)}${dienTich}</span>`;

    if (event && wrapper) {
      const rect = wrapper.getBoundingClientRect();
      const clientX = event.clientX != null ? event.clientX : (event.originalEvent?.clientX || rect.left + 50);
      const clientY = event.clientY != null ? event.clientY : (event.originalEvent?.clientY || rect.top + 50);
      badge.style.left = `${clientX - rect.left}px`;
      badge.style.top = `${clientY - rect.top}px`;
      badge.classList.remove("hidden");
    }
  }, () => {
    if (badge) badge.classList.add("hidden");
  });

  // Nhấp chuột vào nguyên tử để đo khoảng cách / góc
  viewer3D.setClickable({}, true, (atom) => {
    if (!atom || !cheDoDo) return;
    xuLyChonNguyenTuDo(atom);
  });
}

function datCheDoDo(loai) {
  if (cheDoDo === loai) {
    // Tắt chế độ đo nếu đang bật
    cheDoDo = null;
    nguyenTuDaChon = [];
    $("#nut-do-kc-3d").classList.remove("dang-chon");
    $("#nut-do-goc-3d").classList.remove("dang-chon");
    $("#thong-bao-do-3d").innerHTML = '<span class="text-lang">Rê chuột xem nguyên tử · Bấm nút để đo liên kết</span>';
    veKieu3D();
    return;
  }

  cheDoDo = loai;
  nguyenTuDaChon = [];
  $("#nut-do-kc-3d").classList.toggle("dang-chon", loai === "khoang-cach");
  $("#nut-do-goc-3d").classList.toggle("dang-chon", loai === "goc");

  const tb = $("#thong-bao-do-3d");
  if (loai === "khoang-cach") {
    tb.innerHTML = '<span class="font-medium text-nhan">Đo khoảng cách:</span> hãy nhấp chọn nguyên tử thứ nhất...';
  } else if (loai === "goc") {
    tb.innerHTML = '<span class="font-medium text-lam">Đo góc:</span> hãy nhấp chọn 3 nguyên tử (nguyên tử 2 là đỉnh góc)...';
  }
  veKieu3D();
}

function xuLyChonNguyenTuDo(atom) {
  // Tránh chọn trùng nguyên tử liền trước
  if (nguyenTuDaChon.some((a) => a.x === atom.x && a.y === atom.y && a.z === atom.z)) return;
  nguyenTuDaChon.push({ elem: atom.elem, x: atom.x, y: atom.y, z: atom.z, serial: atom.serial || atom.index + 1 });

  const tb = $("#thong-bao-do-3d");

  if (cheDoDo === "khoang-cach") {
    if (nguyenTuDaChon.length === 1) {
      tb.innerHTML = `<span class="font-medium text-nhan">Đo khoảng cách:</span> Đã chọn <b>${atom.elem}</b> #${atom.serial || 1}. Hãy nhấp nguyên tử thứ hai...`;
      veKieu3D();
    } else if (nguyenTuDaChon.length === 2) {
      const [a1, a2] = nguyenTuDaChon;
      const dx = a2.x - a1.x, dy = a2.y - a1.y, dz = a2.z - a1.z;
      const d = Math.sqrt(dx * dx + dy * dy + dz * dz);
      const strD = `${d.toFixed(3)} Å`;
      cacPhepDo.push({ loai: "khoang-cach", diem: [a1, a2], ketQua: strD });
      tb.innerHTML = `<span class="ket-qua-do">Khoảng cách ${a1.elem}–${a2.elem}: ${strD}</span>`;
      $("#nut-xoa-do-3d").classList.remove("hidden");
      nguyenTuDaChon = [];
      veKieu3D();
    }
  } else if (cheDoDo === "goc") {
    if (nguyenTuDaChon.length === 1) {
      tb.innerHTML = `<span class="font-medium text-lam">Đo góc:</span> Đã chọn <b>${atom.elem}</b> (cạnh 1). Hãy nhấp đỉnh góc...`;
      veKieu3D();
    } else if (nguyenTuDaChon.length === 2) {
      tb.innerHTML = `<span class="font-medium text-lam">Đo góc:</span> Đã chọn đỉnh <b>${atom.elem}</b>. Hãy nhấp nguyên tử cạnh 2...`;
      veKieu3D();
    } else if (nguyenTuDaChon.length === 3) {
      const [a1, a2, a3] = nguyenTuDaChon; // a2 là đỉnh
      const v1x = a1.x - a2.x, v1y = a1.y - a2.y, v1z = a1.z - a2.z;
      const v2x = a3.x - a2.x, v2y = a3.y - a2.y, v2z = a3.z - a2.z;
      const dot = v1x * v2x + v1y * v2y + v1z * v2z;
      const m1 = Math.sqrt(v1x * v1x + v1y * v1y + v1z * v1z);
      const m2 = Math.sqrt(v2x * v2x + v2y * v2y + v2z * v2z);
      let cosVal = m1 && m2 ? dot / (m1 * m2) : 1;
      cosVal = Math.max(-1, Math.min(1, cosVal));
      const gocDeg = (Math.acos(cosVal) * 180) / Math.PI;
      const strGoc = `${gocDeg.toFixed(1)}°`;
      cacPhepDo.push({ loai: "goc", diem: [a1, a2, a3], ketQua: strGoc });
      tb.innerHTML = `<span class="ket-qua-do">Góc ${a1.elem}–${a2.elem}–${a3.elem}: ${strGoc}</span>`;
      $("#nut-xoa-do-3d").classList.remove("hidden");
      nguyenTuDaChon = [];
      veKieu3D();
    }
  }
}

function xoaTatCaDo() {
  cacPhepDo = [];
  nguyenTuDaChon = [];
  $("#nut-xoa-do-3d").classList.add("hidden");
  $("#thong-bao-do-3d").innerHTML = '<span class="text-lang">Đã xoá các phép đo. Rê chuột hoặc bấm nút để đo tiếp.</span>';
  veKieu3D();
}

function batTatToanManHinh3D() {
  const wrapper = $("#khung-3d-wrapper");
  if (!wrapper) return;
  const daBat = wrapper.classList.toggle("toan-man-hinh");
  if (daBat) {
    if (wrapper.requestFullscreen) wrapper.requestFullscreen().catch(() => {});
  } else {
    if (document.fullscreenElement && document.exitFullscreen) document.exitFullscreen().catch(() => {});
  }
  setTimeout(() => {
    viewer3D?.resize();
    viewer3D?.render();
  }, 100);
}

function taiAnh3DHD() {
  if (!viewer3D) return;
  const khung = $("#khung-3d");
  const cuW = khung.style.width;
  const cuH = khung.style.height;
  const cuPos = khung.style.position;
  const cuLeft = khung.style.left;

  try {
    // Tạm thời mở rộng canvas để xuất ảnh 4K độ nét cao (2560x1600)
    khung.style.position = "fixed";
    khung.style.left = "-9999px";
    khung.style.width = "2560px";
    khung.style.height = "1600px";
    viewer3D.resize();
    viewer3D.render();
    const uri = viewer3D.pngURI();
    taiXuong(`${tenTep3D()}_3d_4K.png`, uri, "image/png");
    toast("Đã xuất ảnh mô hình 3D chuẩn 4K siêu nét.", "ok", 3000);
  } catch (err) {
    console.warn("Lỗi xuất ảnh HD, quay về chế độ thường:", err);
    taiXuong(`${tenTep3D()}_3d.png`, viewer3D.pngURI(), "image/png");
  } finally {
    khung.style.position = cuPos;
    khung.style.left = cuLeft;
    khung.style.width = cuW;
    khung.style.height = cuH;
    viewer3D.resize();
    viewer3D.render();
  }
}

function hien3D(d) {
  duLieu3D = d;
  $("#ket-qua-3d").classList.remove("hidden");
  if (!viewer3D) {
    viewer3D = $3Dmol.createViewer($("#khung-3d"), { backgroundColor: bienCss("--giay-lom"), antialias: true });
  }
  viewer3D.clear();
  viewer3D.addModel(d.sdf, "sdf");
  veKieu3D();
  khoiTaoTuongTac3D();
  viewer3D.zoomTo();
  viewer3D.zoom(1.15);
  viewer3D.spin($("#xoay-3d").checked ? "y" : false, 0.6);
  viewer3D.render();
  viewer3D.resize();

  $("#ten-3d").textContent = d.ten;
  $("#ten-en-3d").textContent = d.ten_tieng_anh && d.ten_tieng_anh.toLowerCase() !== d.ten.toLowerCase() ? d.ten_tieng_anh : "";
  $("#anh-2d-3d").src = d.anh_2d || "";
  $("#anh-2d-3d").classList.toggle("hidden", !d.anh_2d);
  $("#ct-3d").innerHTML = ctHTML(d.cong_thuc);
  $("#m-3d").textContent = `${d.khoi_luong} g/mol`;
  $("#iupac-3d").textContent = d.iupac || "—";
  $("#smiles-3d").textContent = d.smiles;
  $("#pubchem-3d").innerHTML = d.cid ? `<a href="${esc(d.url)}" target="_blank" rel="noopener">CID ${esc(d.cid)} ↗</a>` : "Không có trong PubChem";
  $("#nguon-3d").textContent = `${d.nguon_3d} · ${d.so_nguyen_tu} nguyên tử`;
  $("#cach-3d").textContent = d.cach_nhan_dien;
  const cb = $("#canh-bao-3d");
  cb.classList.toggle("hidden", !(d.canh_bao || []).length);
  cb.innerHTML = (d.canh_bao || []).map((x) => ctHTML(x)).join("<br>");
  document.querySelectorAll('.muc-ben[data-muc="muc-3d"]').forEach((m) => m.classList.add("xong"));
}

async function dung3D(tuyChon = {}) {
  if ($("#nut-3d").disabled) return;
  const smiles = tuyChon.smiles || "";
  const dungAnh = !smiles && cach3D === "hinh-anh";
  const vanBan = (tuyChon.van_ban ?? (dungAnh ? "" : $("#o-3d").value)).trim();
  if (dungAnh && !anh3D) { toast("Hãy chọn ảnh chất (mục 04 → Hình ảnh).", "loi"); return; }
  if (!dungAnh && !smiles && !vanBan) { toast("Hãy nhập tên, công thức hoặc SMILES của chất.", "loi"); $("#o-3d").focus(); return; }

  // Kiểm tra bộ nhớ đệm client (0ms)
  const khoaCache = smiles ? `smiles:${smiles}` : `text:${vanBan.toLowerCase()}`;
  if (!dungAnh && duLieu3DCache.has(khoaCache)) {
    const cachedData = duLieu3DCache.get(khoaCache);
    await taiThuVien3D();
    hien3D(cachedData);
    $("#trang-thai-3d").textContent = `Đã dựng (từ bộ nhớ đệm): ${cachedData.ten}`;
    $("#ket-qua-3d").scrollIntoView({ behavior: "smooth", block: "nearest" });
    return;
  }

  const fd = new FormData();
  fd.append("van_ban", vanBan);
  fd.append("smiles", smiles);
  if (dungAnh) fd.append("anh", anh3D, anh3D.name || "chat.png");

  const nut = $("#nut-3d");
  nut.disabled = true;
  nut.querySelector(".nhan").textContent = "Đang dựng...";
  nut.querySelector(".quay").classList.remove("hidden");
  $("#trang-thai-3d").textContent = dungAnh ? "AI đang đọc ảnh, tra PubChem..." : "Đang tra PubChem và dựng cấu dạng 3D...";
  try {
    const [r] = await Promise.all([TaiKhoan.goi("/api/3d", { method: "POST", body: fd }), taiThuVien3D()]);
    const d = await r.json();
    if (!r.ok) throw Object.assign(new Error(d.loi || "Không dựng được mô hình 3D."), { ma: d.ma });

    // Lưu vào bộ nhớ đệm client
    if (!dungAnh) {
      duLieu3DCache.set(khoaCache, d);
      if (d.smiles) duLieu3DCache.set(`smiles:${d.smiles}`, d);
    }

    hien3D(d);
    $("#trang-thai-3d").textContent = `Đã dựng: ${d.ten}`;
    $("#ket-qua-3d").scrollIntoView({ behavior: "smooth", block: "nearest" });
  } catch (e) {
    $("#trang-thai-3d").textContent = "";
    toast("⚠ " + esc(e.message || "Không dựng được mô hình 3D.") + goiYLoi(e.ma, e.message), "loi", 9000);
  } finally {
    nut.disabled = false;
    nut.querySelector(".nhan").textContent = "Dựng mô hình 3D";
    nut.querySelector(".quay").classList.add("hidden");
  }
}

function taiXuong(ten, noiDung, kieu) {
  const a = document.createElement("a");
  a.href = noiDung.startsWith("data:") ? noiDung : URL.createObjectURL(new Blob([noiDung], { type: kieu }));
  a.download = ten;
  document.body.appendChild(a);
  a.click();
  a.remove();
}

function tenTep3D() {
  return (duLieu3D?.ten_tieng_anh || duLieu3D?.ten || "chat").normalize("NFD").replace(/[̀-ͯ]/g, "")
    .replace(/[^A-Za-z0-9]+/g, "_").replace(/^_|_$/g, "").slice(0, 40) || "chat";
}

function khoiTao3D() {
  document.querySelectorAll("[data-cach3d]").forEach((t) => (t.onclick = () => chonCach3D(t.dataset.cach3d)));
  const goiY = $("#goi-y-3d");
  for (const g of GOI_Y_3D) {
    const b = document.createElement("button");
    b.type = "button";
    b.className = "chip";
    b.innerHTML = ctHTML(g);
    b.onmouseenter = () => taiThuVien3D().catch(() => {});
    b.onclick = () => { $("#o-3d").value = g; chonCach3D("van-ban"); dung3D(); };
    goiY.appendChild(b);
  }
  $("#form-3d").addEventListener("submit", (e) => { e.preventDefault(); dung3D(); });
  $("#nut-tai-anh-3d").onclick = () => $("#file-anh-3d").click();
  $("#file-anh-3d").onchange = (e) => e.target.files[0] && chonAnh3D(e.target.files[0]);
  $("#nut-bo-anh-3d").onclick = () => chonAnh3D(null);
  const vung = $("#muc-3d");
  vung.addEventListener("mouseenter", () => taiThuVien3D().catch(() => {}), { once: true });
  ["dragenter", "dragover"].forEach((ev) => vung.addEventListener(ev, (e) => { e.preventDefault(); $("#vung-anh-3d").classList.add("keo"); }));
  ["dragleave", "drop"].forEach((ev) => vung.addEventListener(ev, (e) => { e.preventDefault(); $("#vung-anh-3d").classList.remove("keo"); }));
  vung.addEventListener("drop", (e) => {
    const f = [...(e.dataTransfer?.files || [])].find((x) => x.type.startsWith("image/"));
    if (f) chonAnh3D(f);
  });

  document.querySelectorAll('input[name="kieu-3d"]').forEach((r) => r.addEventListener("change", veKieu3D));
  $("#be-mat-3d")?.addEventListener("change", capNhatBeMat3D);
  $("#mau-3d")?.addEventListener("change", veKieu3D);
  $("#nhan-3d").addEventListener("change", veKieu3D);
  $("#xoay-3d").addEventListener("change", () => viewer3D?.spin($("#xoay-3d").checked ? "y" : false, 0.6));
  $("#nut-goc-3d").onclick = () => { if (viewer3D) { viewer3D.zoomTo(); viewer3D.zoom(1.15); viewer3D.render(); } };
  $("#nut-toan-man-hinh-3d")?.addEventListener("click", batTatToanManHinh3D);
  document.addEventListener("fullscreenchange", () => {
    if (!document.fullscreenElement) $("#khung-3d-wrapper")?.classList.remove("toan-man-hinh");
    viewer3D?.resize();
    viewer3D?.render();
  });

  // Công cụ đo khoảng cách / góc
  $("#nut-do-kc-3d")?.addEventListener("click", () => datCheDoDo("khoang-cach"));
  $("#nut-do-goc-3d")?.addEventListener("click", () => datCheDoDo("goc"));
  $("#nut-xoa-do-3d")?.addEventListener("click", xoaTatCaDo);

  $("#tai-png-3d").onclick = () => viewer3D && taiXuong(`${tenTep3D()}_3d.png`, viewer3D.pngURI(), "image/png");
  $("#tai-png-hd-3d")?.addEventListener("click", taiAnh3DHD);
  $("#tai-sdf-3d").onclick = () => duLieu3D && taiXuong(`${tenTep3D()}_3d.sdf`, duLieu3D.sdf, "chemical/x-mdl-sdfile");
  $("#tai-xyz-3d").onclick = () => duLieu3D && taiXuong(`${tenTep3D()}.xyz`, duLieu3D.xyz, "chemical/x-xyz");
  window.addEventListener("resize", () => viewer3D?.resize());

  // nút "3D" trên các chất trong lời giải → dựng ở mục 04
  document.addEventListener("click", (e) => {
    const nut = e.target.closest?.(".nut-xem-3d");
    if (!nut) return;
    chonCach3D("van-ban");
    $("#o-3d").value = nut.dataset.ten || nut.dataset.smiles;
    window.TrangChu?.mo("3d"); // trang chủ mới: mở nhóm Mô hình 3D (đang ở nhóm Giải chuỗi)
    $("#muc-3d").scrollIntoView({ behavior: "smooth" });
    dung3D({ smiles: nut.dataset.smiles, van_ban: nut.dataset.ten || "" });
  });
  document.addEventListener("mouseover", (e) => {
    if (e.target.closest?.(".nut-xem-3d") || e.target.closest?.('[data-muc="muc-3d"]')) {
      taiThuVien3D().catch(() => {});
    }
  });
}

// ------------------------------------------------------------ 05 cơ chế phản ứng (mũi tên cong)
const GOI_Y_CO_CHE = [
  "CH3CH2Br + OH- → CH3CH2OH + Br-",
  "propen + HBr",
  "2-brompropan + KOH/etanol, đun nóng",
  "CH3COOH + C2H5OH (xúc tác H2SO4 đặc)",
  "benzen + HNO3/H2SO4 đặc",
  "CH3CHO + HCN",
];
let duLieuCoChe = null;

async function veCoChe(tuyChon = {}) {
  if (anhCoChe.length && !tuyChon.chat) return docAnhCoChe(); // đã đưa ảnh đề → đọc ảnh, xác nhận rồi mới vẽ
  const nut = $("#nut-co-che");
  if (nut.disabled) return null;
  const vanBan = (tuyChon.van_ban ?? $("#o-co-che").value).trim();
  if (!vanBan && !(tuyChon.chat && tuyChon.chat.length)) {
    toast("Hãy nhập một phản ứng, vd: CH3CH2Br + OH- → CH3CH2OH + Br-", "loi");
    $("#o-co-che").focus();
    return null;
  }
  nut.disabled = true;
  nut.querySelector(".nhan").textContent = "Đang vẽ...";
  nut.querySelector(".quay").classList.remove("hidden");
  $("#trang-thai-co-che").textContent = "Đang nhận diện phản ứng và dựng cơ chế...";
  try {
    const r = await TaiKhoan.goi("/api/co-che", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ van_ban: vanBan, loai: tuyChon.loai ?? $("#loai-co-che").value, chat: tuyChon.chat || [],
                             san_pham: tuyChon.san_pham || [], dieu_kien: tuyChon.dieu_kien || "",
                             dung_claude: !$("#khung-dung-claude").hidden && $("#dung-claude").checked, // backend kiểm tra lại quyền
                             tra_cuu_tai_lieu: tlCoCheTuDong ? null : $("#tra-cuu-tl-co-che").checked }), // null = tự bật khi có DOI
    });
    const d = await r.json().catch(() => ({}));
    if (!r.ok) throw Object.assign(new Error(d.loi || "Không vẽ được cơ chế."), { ma: d.ma });
    hienCoChe(d);
    (d.thong_bao_claude || []).forEach((t) => toast("⚠ " + esc(t), "tin", 9000));
    $("#trang-thai-co-che").textContent = "";
    return d;
  } catch (e) {
    $("#trang-thai-co-che").textContent = "";
    toast("⚠ " + esc(e.message || "Không vẽ được cơ chế.") + goiYLoi(e.ma, e.message), "loi", 10000);
    return null;
  } finally {
    nut.disabled = false;
    nut.querySelector(".nhan").textContent = "Vẽ cơ chế";
    nut.querySelector(".quay").classList.add("hidden");
  }
}

let giaiThichCoCheDangTai = false;
let giaiThichCoCheCache = null;

function veGiaiThichCoChe(gt) {
  const hop = $("#hop-giai-thich-co-che");
  if (!hop) return;
  if (!gt || !gt.chi_tiet_buoc || !gt.chi_tiet_buoc.length) {
    if (duLieuCoChe?.ghi_chu?.length) {
      hop.innerHTML = `<ol class="ds-buoc-co-che">${(duLieuCoChe.ghi_chu || []).map((g, i) =>
        `<li><span class="so">Bước ${i + 1}</span><span>${ctHTML(g || "")}</span></li>`).join("")}</ol>`;
    } else {
      hop.innerHTML = `<p class="ghi-chu-nho py-3">Chưa có phân tích chi tiết cho cơ chế này.</p>`;
    }
    return;
  }
  let html = "";
  if (gt.tong_quan) {
    html += `<div class="tong-quan-co-che"><b>Tổng quan cơ chế:</b> ${esc(gt.tong_quan)}</div>`;
  }
  html += `<div class="luoi-buoc-chi-tiet">`;
  gt.chi_tiet_buoc.forEach((b) => {
    html += `
      <div class="the-buoc-chi-tiet">
        <div class="tieu-de-buoc">
          <span class="huy-hieu-buoc">BƯỚC ${b.buoc}</span>
          <span>${esc(b.tieu_de || `Bước ${b.buoc}`)}</span>
        </div>
        <div class="dong-dien-bien">${ctHTML(b.dien_bien || "")}</div>
        <div class="luoi-dac-diem">
          <div class="muc-dac-diem">
            <b>Dòng electron (Curly Arrows)</b>
            <span>${esc(b.ban_chat_e || "Chuyển dịch electron theo liên kết")}</span>
          </div>
          <div class="muc-dac-diem">
            <b>Tâm phản ứng</b>
            <span>${esc(b.tam_phan_ung || "Trung tâm ái nhân / ái điện tử")}</span>
          </div>
          <div class="muc-dac-diem">
            <b>Lập thể & Chọn lọc</b>
            <span>${esc(b.lap_the || "Theo trật tự bền vững")}</span>
          </div>
        </div>
      </div>
    `;
  });
  html += `</div>`;
  hop.innerHTML = html;
}

async function taiGiaiThichCoCheNen(d) {
  giaiThichCoCheDangTai = true;
  giaiThichCoCheCache = null;
  const buoc = (d.co_che?.cac_buoc || []).map((b, i) => ({
    so: i + 1,
    ghi_chu: b.ghi_chu || d.ghi_chu?.[i] || "",
    chat: b.chat || []
  }));
  try {
    const r = await TaiKhoan.goi("/api/co-che/giai-thich", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        van_ban: $("#o-co-che")?.value || d.ten,
        ten: d.ten,
        loai: d.loai || "",
        buoc: buoc
      })
    });
    if (r.ok) {
      const gt = await r.json();
      giaiThichCoCheCache = gt;
      const hop = $("#hop-giai-thich-co-che");
      if (hop && !hop.classList.contains("hidden")) {
        veGiaiThichCoChe(gt);
      }
    }
  } catch (err) {
    console.warn("Background fetch giai thich co che:", err);
  } finally {
    giaiThichCoCheDangTai = false;
  }
}

function hienCoChe(d) {
  duLieuCoChe = d;
  $("#nguon-co-che").classList.toggle("hidden", !(d.tai_lieu || []).length);
  $("#nguon-co-che ul").innerHTML = htmlNguon(d.tai_lieu || [], false);
  $("#ket-qua-co-che").classList.remove("hidden");
  $("#ten-co-che").textContent = d.ten;
  const nguon = $("#nguon-co-che");
  nguon.className = "huy-hieu " + (d.nguon === "mau" ? "xanh" : "vang");
  nguon.textContent = d.nguon === "mau" ? "Mẫu chuẩn (thư viện)" : "AI tạo, cần kiểm tra";
  // Bước 1 (Tức thì): Render ngay hình ảnh SVG ra màn hình
  $("#khung-co-che").innerHTML = d.svg;
  const cb = $("#canh-bao-co-che");
  cb.classList.toggle("hidden", !(d.canh_bao && d.canh_bao.length));
  cb.innerHTML = (d.canh_bao || []).map((x) => esc(x)).join("<br>");

  // Reset nút và khung giải thích
  const nutGt = $("#nut-xem-giai-thich-co-che");
  const hopGt = $("#hop-giai-thich-co-che");
  if (nutGt) {
    nutGt.classList.remove("mo");
    nutGt.setAttribute("aria-expanded", "false");
  }
  if (hopGt) {
    hopGt.classList.add("hidden");
    hopGt.innerHTML = "";
  }

  // Bước 2 (Chạy ngầm): Kích hoạt lấy giải thích chi tiết trong nền
  taiGiaiThichCoCheNen(d);

  document.querySelectorAll('.muc-ben[data-muc="muc-co-che"]').forEach((m) => m.classList.add("xong"));
  $("#ket-qua-co-che").scrollIntoView({ behavior: "smooth", block: "nearest" });
}

function tenTepCoChe(duoi) {
  const t = (duLieuCoChe?.co_che?.loai || "co_che").toLowerCase().replace(/[^a-z0-9_]+/g, "_");
  return `co_che_${t}.${duoi}`;
}

// PNG nền trắng, độ phân giải cao (×4) – vẽ SVG lên canvas ngay trong trình duyệt
function taiPngCoChe() {
  const svgEl = $("#khung-co-che svg");
  if (!svgEl) return;
  const w = Number(svgEl.getAttribute("width")) || svgEl.viewBox.baseVal.width;
  const h = Number(svgEl.getAttribute("height")) || svgEl.viewBox.baseVal.height;
  const k = 4;
  const img = new Image();
  img.onload = () => {
    const c = document.createElement("canvas");
    c.width = Math.round(w * k);
    c.height = Math.round(h * k);
    const g = c.getContext("2d");
    g.fillStyle = "#fff";
    g.fillRect(0, 0, c.width, c.height);
    g.drawImage(img, 0, 0, c.width, c.height);
    taiXuong(tenTepCoChe("png"), c.toDataURL("image/png"), "image/png");
  };
  img.onerror = () => toast("Không tạo được PNG – hãy dùng Tải SVG.", "loi");
  img.src = "data:image/svg+xml;charset=utf-8," + encodeURIComponent(duLieuCoChe.svg);
}

function khoiTaoCoChe() {
  $("#goi-y-co-che").innerHTML = GOI_Y_CO_CHE.map((g) => `<button type="button" class="chip">${esc(g)}</button>`).join("");
  $("#goi-y-co-che").addEventListener("click", (e) => {
    const c = e.target.closest(".chip");
    if (!c) return;
    $("#o-co-che").value = c.textContent;
    veCoChe();
  });
  $("#nut-co-che").addEventListener("click", () => veCoChe());
  $("#tai-png-co-che").addEventListener("click", taiPngCoChe);
  $("#tai-svg-co-che").addEventListener("click", () => duLieuCoChe && taiXuong(tenTepCoChe("svg"), duLieuCoChe.svg, "image/svg+xml"));
  $("#tai-cdxml-co-che").addEventListener("click", () => duLieuCoChe && taiXuong(tenTepCoChe("cdxml"), duLieuCoChe.cdxml, "chemical/x-cdxml"));
  $("#copy-json-co-che").addEventListener("click", async () => {
    if (!duLieuCoChe) return;
    const json = JSON.stringify(duLieuCoChe.co_che, null, 2);
    try {
      await navigator.clipboard.writeText(json);
      toast("Đã copy JSON cơ chế.", "ok", 2500);
    } catch {
      taiXuong(tenTepCoChe("json"), json, "application/json");
    }
  });

  const nutGt = $("#nut-xem-giai-thich-co-che");
  const hopGt = $("#hop-giai-thich-co-che");
  if (nutGt && hopGt) {
    nutGt.addEventListener("click", () => {
      const dangAn = hopGt.classList.contains("hidden");
      if (dangAn) {
        hopGt.classList.remove("hidden");
        nutGt.classList.add("mo");
        nutGt.setAttribute("aria-expanded", "true");
        if (giaiThichCoCheCache) {
          veGiaiThichCoChe(giaiThichCoCheCache);
        } else if (giaiThichCoCheDangTai) {
          hopGt.innerHTML = `<div class="dang-tai-giai-thich"><span class="quay"></span> Đang tạo phân tích chi tiết từng bước phản ứng...</div>`;
        } else if (duLieuCoChe?.ghi_chu?.length) {
          hopGt.innerHTML = `<ol class="ds-buoc-co-che">${(duLieuCoChe.ghi_chu || []).map((g, i) =>
            `<li><span class="so">Bước ${i + 1}</span><span>${ctHTML(g || "")}</span></li>`).join("")}</ol>`;
        } else {
          hopGt.innerHTML = `<div class="dang-tai-giai-thich"><span class="quay"></span> Đang kết nối tải giải thích...</div>`;
        }
      } else {
        hopGt.classList.add("hidden");
        nutGt.classList.remove("mo");
        nutGt.setAttribute("aria-expanded", "false");
      }
    });
  }
  // nút "Cơ chế" cạnh mỗi phương trình của lời giải: dùng luôn SMILES đã có
  document.addEventListener("click", (e) => {
    const nut = e.target.closest?.(".nut-co-che-buoc");
    if (!nut || !loiGiaiHienTai) return;
    const b = loiGiaiHienTai.cac_buoc[Number(nut.dataset.buoc)];
    if (!b) return;
    $("#o-co-che").value = b.phuong_trinh || "";
    $("#loai-co-che").value = "";
    window.TrangChu?.mo("co-che");
    $("#muc-co-che").scrollIntoView({ behavior: "smooth" });
    veCoChe({
      van_ban: `${b.phuong_trinh || ""}${b.loai_phan_ung ? ` (${b.loai_phan_ung})` : ""}`,
      chat: b.chat_tham_gia.map((c) => c.smiles).filter(Boolean),
      san_pham: b.san_pham.map((c) => c.smiles).filter(Boolean),
      dieu_kien: b.dieu_kien || "",
      loai: "",
    });
  });
  khoiTaoAnhCoChe();
}

// ------------------------------------------------------------ 05 Cơ chế: NHẬP ĐỀ BẰNG HÌNH ẢNH → xác nhận → vẽ (dùng lại veCoChe)
const ANH_CO_CHE_TOI_DA = 4;
const DUOI_ANH = /\.(jpe?g|png|webp|heic|heif)$/i;
const KIEU_ANH = /^image\/(jpe?g|png|webp|heic|heif)$/i;
let anhCoChe = [];       // [{ blob, ten, url }] – ảnh đã nén, chờ đọc
let docAnh = null;       // kết quả đọc ảnh đang chờ xác nhận (người dùng sửa trực tiếp trên đây)

const laHeic = (f) => /heic|heif/i.test(f.type || "") || /\.(heic|heif)$/i.test(f.name || "");

async function nenAnh(file) { // cạnh dài ≤ 1600 px, JPEG. HEIC: Safari tự giải mã; trình duyệt khác gửi nguyên để máy chủ đổi sang JPG
  try {
    const bmp = await createImageBitmap(file);
    const ti = Math.min(1, 1600 / Math.max(bmp.width, bmp.height));
    const c = document.createElement("canvas");
    c.width = Math.max(1, Math.round(bmp.width * ti));
    c.height = Math.max(1, Math.round(bmp.height * ti));
    const g = c.getContext("2d");
    g.fillStyle = "#fff";
    g.fillRect(0, 0, c.width, c.height);
    g.drawImage(bmp, 0, 0, c.width, c.height);
    bmp.close?.();
    const blob = await new Promise((xong) => c.toBlob(xong, "image/jpeg", 0.9));
    if (blob) return blob;
  } catch { /* không giải mã được trong trình duyệt (vd HEIC trên Chrome) */ }
  if (laHeic(file)) return file;
  throw new Error("không đọc được ảnh");
}

async function themAnhCoChe(files) {
  for (const f of [...files]) {
    const ten = f.name || "ảnh dán";
    if (anhCoChe.length >= ANH_CO_CHE_TOI_DA) { toast(`Tối đa ${ANH_CO_CHE_TOI_DA} ảnh mỗi lần – xoá bớt ảnh để thêm.`, "loi"); break; }
    if (f.type ? !KIEU_ANH.test(f.type) : !DUOI_ANH.test(ten)) {
      toast(`“${esc(ten)}”: định dạng ${esc(f.type || "này")} không hỗ trợ – chỉ nhận JPG, PNG, WEBP, HEIC.`, "loi", 8000);
      continue;
    }
    if (f.size > 25 * 1024 * 1024) { toast(`“${esc(ten)}” quá lớn (${(f.size / 1048576).toFixed(1)} MB) – tối đa 25 MB.`, "loi", 8000); continue; }
    try {
      const blob = await nenAnh(f);
      if (blob.size > 10 * 1024 * 1024) { toast(`“${esc(ten)}” vẫn quá lớn sau khi nén (tối đa 10 MB).`, "loi", 8000); continue; }
      anhCoChe.push({ blob, ten: blob === f ? ten : ten.replace(/\.[^.]+$/, "") + ".jpg", url: blob === f ? "" : URL.createObjectURL(blob) });
    } catch {
      toast(`Không đọc được ảnh “${esc(ten)}” – hãy dùng JPG, PNG, WEBP hoặc HEIC.`, "loi", 8000);
    }
  }
  veAnhCoChe();
}

function veAnhCoChe() {
  const ds = $("#ds-anh-co-che");
  ds.classList.toggle("hidden", !anhCoChe.length);
  $("#nut-doc-anh-co-che").classList.toggle("hidden", !anhCoChe.length);
  ds.innerHTML = anhCoChe.map((a, i) => `<figure class="anh-co-che">
      ${a.url ? `<img src="${a.url}" alt="Ảnh đề ${i + 1}">` : `<span class="anh-heic">HEIC<br><small>${esc(a.ten)}</small></span>`}
      <button type="button" class="xoa-anh" data-i="${i}" aria-label="Xoá ảnh ${i + 1}" title="Xoá ảnh này">×</button>
      <figcaption>${i + 1}</figcaption></figure>`).join("");
}

function xoaAnhCoChe(i) {
  const [a] = anhCoChe.splice(i, 1);
  if (a?.url) URL.revokeObjectURL(a.url);
  veAnhCoChe();
}

async function docAnhCoChe() {
  const nut = $("#nut-doc-anh-co-che");
  if (!anhCoChe.length || nut.disabled) return null;
  if (!TaiKhoan.toi?.dang_nhap) { toast("Hãy đăng nhập để đọc ảnh đề." + goiYLoi("can_dang_nhap"), "loi", 8000); return null; }
  const fd = new FormData();
  anhCoChe.forEach((a) => fd.append("anh", a.blob, a.ten));
  fd.append("van_ban", $("#o-co-che").value.trim());
  fd.append("dung_claude", !$("#khung-dung-claude").hidden && $("#dung-claude").checked); // backend kiểm tra lại quyền
  nut.disabled = true;
  $("#nut-co-che").disabled = true;
  nut.querySelector(".quay").classList.remove("hidden");
  $("#trang-thai-co-che").textContent = `Đang đọc ${anhCoChe.length} ảnh đề bằng AI (thường 15–60 giây)…`;
  try {
    const r = await TaiKhoan.goi("/api/co-che/doc-anh", { method: "POST", body: fd });
    const d = await r.json().catch(() => ({}));
    if (!r.ok) throw Object.assign(new Error(d.loi || (r.status === 413 ? "Ảnh quá lớn." : "Không đọc được ảnh đề.")), { ma: d.ma });
    (d.thong_bao_claude || []).forEach((t) => toast("⚠ " + esc(t), "tin", 9000));
    hienXacNhan(d);
    return d;
  } catch (e) {
    toast("⚠ " + esc(e.message || "Không đọc được ảnh đề.") + goiYLoi(e.ma, e.message), "loi", 12000);
    return null;
  } finally {
    nut.disabled = false;
    $("#nut-co-che").disabled = false;
    nut.querySelector(".quay").classList.add("hidden");
    $("#trang-thai-co-che").textContent = "";
  }
}

function theChatXacNhan(c, i, vai, j) {
  const vang = c.smiles && (!c.hop_le || c.khong_chac);
  const tieuDe = !c.smiles ? "đề bắt tìm" : !c.hop_le ? "SMILES không hợp lệ – hãy sửa" : c.khong_chac ? "AI không chắc – hãy kiểm tra" : "";
  return `<figure class="the-chat${vang ? " chat-vang" : ""}${c.smiles ? "" : " chat-tim"}" data-i="${i}" data-vai="${vai}" data-j="${j}"
      ${tieuDe ? `title="${esc(tieuDe)}"` : ""}>
      ${c.anh ? `<img src="${c.anh}" alt="${esc(c.smiles || "")}">` : `<span class="chat-trong">${c.smiles ? "⚠" : "?"}</span>`}
      <figcaption class="font-mono">${esc(c.smiles || "? (đề bắt tìm)")}</figcaption>
      <button type="button" class="lien-ket nut-sua-chat">Sửa</button></figure>`;
}

function hienXacNhan(d) {
  docAnh = d;
  const vb = $("#o-co-che").value;
  const cauNoi = [...vb.matchAll(/câu\s*(\d+)/gi)].map((m) => m[1]); // "vẽ cơ chế câu 2" → chỉ tick sẵn câu 2
  const soCau = (p) => (String(p.so_cau).match(/\d+/) || [""])[0];
  const tick = (p, i) => (cauNoi.length ? cauNoi.includes(soCau(p)) : i === 0);
  $("#xn-tom-tat").textContent = `Mức ${d.ten_muc_do} · ${d.mo_hinh} · ${d.phan_ung.length} phản ứng / câu hỏi`
    + (d.khong_chac_chan.length ? ` · ${d.khong_chac_chan.length} chỗ không chắc (tô vàng)` : "");
  $("#xn-anh-goc").innerHTML = anhCoChe.map((a, i) => a.url ? `<img src="${a.url}" alt="Ảnh đề ${i + 1}">` : "").join("");
  $("#xn-de-bai").value = d.de_bai || "";
  const nghi = [...(d.canh_bao || []), ...(d.khong_chac_chan || [])];
  $("#xn-nghi").classList.toggle("hidden", !nghi.length);
  $("#xn-nghi").innerHTML = `<p class="nhan-mono">Cần kiểm tra</p><ul>${nghi.map((x) => `<li>${esc(x)}</li>`).join("")}</ul>`;
  $("#xn-phan-ung").innerHTML = d.phan_ung.length
    ? d.phan_ung.map((p, i) => `<div class="xn-pu" data-i="${i}">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <label class="xn-chon"><input type="checkbox" class="xn-tick" data-i="${i}" ${tick(p, i) ? "checked" : ""}>
            <b>${esc(p.so_cau)}</b> <span class="ghi-chu-nho">${esc(p.yeu_cau || "")}</span></label>
          <span class="xn-kq ghi-chu-nho" data-i="${i}"></span>
        </div>
        <div class="xn-chat-hang">
          ${p.chat_dau.map((c, j) => theChatXacNhan(c, i, "chat_dau", j)).join('<span class="xn-cong">+</span>') || '<span class="ghi-chu-nho">(chưa có chất đầu)</span>'}
          <span class="xn-mui-ten">→</span>
          <input type="text" class="o-nhap xn-dk" data-i="${i}" maxlength="200" value="${esc(p.tac_nhan_dieu_kien || "")}" placeholder="điều kiện / tác nhân">
          <span class="xn-mui-ten">→</span>
          ${p.san_pham.map((c, j) => theChatXacNhan(c, i, "san_pham", j)).join('<span class="xn-cong">+</span>') || '<span class="ghi-chu-nho">(sản phẩm: AI tự suy)</span>'}
        </div>
        <label class="xn-loai-nhan ghi-chu-nho">Loại cơ chế <select class="o-nhap chon-co-che xn-loai" data-i="${i}">${$("#loai-co-che").innerHTML}</select></label>
        </div>`).join("")
    : '<p class="ghi-chu-nho">Không thấy phản ứng nào trong ảnh – hãy gõ phản ứng vào ô “Phản ứng” rồi bấm Vẽ cơ chế.</p>';
  $("#xn-cau-truc").innerHTML = d.cau_truc_trong_anh.length
    ? `<p class="nhan-mono">Công thức cấu tạo đọc được trong ảnh</p><div class="xn-chat-hang">${d.cau_truc_trong_anh.map((c) =>
      `<figure class="the-chat${!c.hop_le || c.khong_chac ? " chat-vang" : ""}">${c.anh ? `<img src="${c.anh}" alt="${esc(c.smiles)}">` : ""}
        <figcaption><span class="ghi-chu-nho">${esc(c.vi_tri)}</span><br><span class="font-mono">${esc(c.smiles)}</span></figcaption></figure>`).join("")}</div>`
    : "";
  d.phan_ung.forEach((p, i) => { // gợi ý loại cơ chế của AI đọc ảnh (không có trong danh sách → Tự động nhận diện)
    const chon = document.querySelector(`.xn-loai[data-i="${i}"]`);
    chon.value = [...chon.options].some((o) => o.value === p.goi_y_loai) ? p.goi_y_loai : "";
  });
  $("#xn-ve-tat-ca").classList.toggle("hidden", d.phan_ung.length < 2);
  $("#xn-trang-thai").textContent = "";
  $("#xac-nhan-co-che").classList.remove("hidden");
  $("#xac-nhan-co-che").scrollIntoView({ behavior: "smooth", block: "start" });
}

function suaChat(the) { // nút "Sửa": ô nhập SMILES hoặc tên chất → backend nhận diện + vẽ lại bằng RDKit
  const i = Number(the.dataset.i), vai = the.dataset.vai, j = Number(the.dataset.j);
  const c = docAnh.phan_ung[i][vai][j];
  the.innerHTML = `<input type="text" class="o-nhap o-nhap-mono xn-o-sua" value="${esc(c.smiles || "")}" placeholder="SMILES hoặc tên chất" maxlength="300">
    <span class="flex gap-2"><button type="button" class="btn btn-sm btn-primary nut-luu-chat">OK</button>
    <button type="button" class="btn btn-sm nut-huy-chat">Huỷ</button></span>`;
  const o = the.querySelector(".xn-o-sua");
  o.focus();
  o.select();
  const veLai = () => { the.outerHTML = theChatXacNhan(c, i, vai, j); };
  const luu = async () => {
    const chuoi = o.value.trim();
    if (!chuoi) { o.focus(); return; }
    the.querySelector(".nut-luu-chat").disabled = true;
    try {
      const r = await TaiKhoan.goi("/api/co-che/chat", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ chuoi }) });
      const d = await r.json().catch(() => ({}));
      if (!r.ok) throw new Error(d.loi || "Không nhận ra chất.");
      Object.assign(c, { smiles: d.smiles, hop_le: true, khong_chac: false, anh: d.anh });
      veLai();
      (d.canh_bao || []).forEach((t) => toast(esc(t), "tin", 7000));
    } catch (e) {
      toast("⚠ " + esc(e.message), "loi", 8000);
      const nutLuu = the.querySelector(".nut-luu-chat");
      if (nutLuu) nutLuu.disabled = false;
    }
  };
  the.querySelector(".nut-luu-chat").onclick = luu;
  the.querySelector(".nut-huy-chat").onclick = veLai;
  o.addEventListener("keydown", (e) => {
    if (e.key === "Enter") { e.preventDefault(); e.stopPropagation(); luu(); }
    if (e.key === "Escape") veLai();
  });
}

async function veTuAnh(tatCa = false) { // "Đúng rồi, vẽ cơ chế": chuyển dữ liệu ĐÃ XÁC NHẬN sang luồng vẽ cơ chế đang có
  if (!docAnh) return;
  if (tatCa) document.querySelectorAll(".xn-tick").forEach((t) => { t.checked = true; });
  const chon = [...document.querySelectorAll(".xn-tick")].filter((t) => t.checked).map((t) => Number(t.dataset.i));
  if (!chon.length) { toast("Hãy tick ít nhất một phản ứng để vẽ (hoặc bấm Vẽ tất cả).", "loi"); return; }
  for (const i of chon) {
    const p = docAnh.phan_ung[i];
    if (!p.chat_dau.some((c) => c.smiles)) { toast(`${esc(p.so_cau)}: chưa có chất đầu nào.`, "loi"); return; }
    const sai = [...p.chat_dau, ...p.san_pham].find((c) => c.smiles && !c.hop_le);
    if (sai) { toast(`${esc(p.so_cau)}: “${esc(sai.smiles)}” chưa phải SMILES hợp lệ – bấm Sửa ở chất tô vàng.`, "loi", 8000); return; }
  }
  const deBai = $("#xn-de-bai").value.trim();
  const nutVe = [$("#xn-ve"), $("#xn-ve-tat-ca")];
  nutVe.forEach((n) => { n.disabled = true; });
  let xong = 0;
  for (const [k, i] of chon.entries()) {
    const p = docAnh.phan_ung[i];
    p.tac_nhan_dieu_kien = document.querySelector(`.xn-dk[data-i="${i}"]`)?.value.trim() || "";
    const kq = document.querySelector(`.xn-kq[data-i="${i}"]`);
    kq.textContent = "Đang vẽ…";
    $("#xn-trang-thai").textContent = chon.length > 1 ? `Đang vẽ ${k + 1}/${chon.length}…` : "Đang vẽ…";
    const d = await veCoChe({
      van_ban: [`${p.so_cau}: ${p.yeu_cau || "vẽ cơ chế"}`, deBai].filter(Boolean).join("\n").slice(0, 600),
      chat: p.chat_dau.map((c) => c.smiles).filter(Boolean),
      san_pham: p.san_pham.map((c) => c.smiles).filter(Boolean),
      dieu_kien: p.tac_nhan_dieu_kien.slice(0, 200),
      loai: document.querySelector(`.xn-loai[data-i="${i}"]`)?.value || "",
    });
    p.ket_qua = d;
    if (d) xong += 1;
    kq.innerHTML = d ? `✓ Đã vẽ · <button type="button" class="lien-ket xn-xem" data-i="${i}">Xem</button>` : '<span class="chu-loi">✗ Không vẽ được</span>';
  }
  nutVe.forEach((n) => { n.disabled = false; });
  $("#xn-trang-thai").textContent = chon.length > 1 ? `Đã vẽ ${xong}/${chon.length} – bấm “Xem” ở từng câu để mở lại.` : "";
}

function khoiTaoAnhCoChe() {
  $("#nut-tai-anh-co-che").onclick = () => $("#file-anh-co-che").click();
  $("#nut-chup-anh-co-che").onclick = () => $("#chup-anh-co-che").click();
  if (window.matchMedia?.("(pointer: coarse)").matches) $("#nut-chup-anh-co-che").classList.remove("hidden"); // điện thoại / máy tính bảng
  for (const id of ["#file-anh-co-che", "#chup-anh-co-che"]) {
    $(id).onchange = async (e) => { await themAnhCoChe(e.target.files); e.target.value = ""; };
  }
  $("#nut-doc-anh-co-che").onclick = docAnhCoChe;
  $("#ds-anh-co-che").addEventListener("click", (e) => { const x = e.target.closest(".xoa-anh"); if (x) xoaAnhCoChe(Number(x.dataset.i)); });
  const vung = $("#muc-co-che");
  ["dragenter", "dragover"].forEach((ev) => vung.addEventListener(ev, (e) => {
    if (![...(e.dataTransfer?.types || [])].includes("Files")) return;
    e.preventDefault();
    $("#vung-anh-co-che").classList.add("keo");
  }));
  ["dragleave", "drop"].forEach((ev) => vung.addEventListener(ev, () => $("#vung-anh-co-che").classList.remove("keo")));
  vung.addEventListener("drop", (e) => {
    const ds = [...(e.dataTransfer?.files || [])];
    if (!ds.length) return;
    e.preventDefault();
    themAnhCoChe(ds);
  });
  $("#xn-phan-ung").addEventListener("click", (e) => {
    const sua = e.target.closest(".nut-sua-chat");
    if (sua) { suaChat(sua.closest(".the-chat")); return; }
    const xem = e.target.closest(".xn-xem");
    if (xem && docAnh?.phan_ung[Number(xem.dataset.i)]?.ket_qua) hienCoChe(docAnh.phan_ung[Number(xem.dataset.i)].ket_qua);
  });
  $("#xn-ve").onclick = () => veTuAnh(false);
  $("#xn-ve-tat-ca").onclick = () => veTuAnh(true);
  $("#xn-bo").onclick = () => { docAnh = null; $("#xac-nhan-co-che").classList.add("hidden"); };
}

// ------------------------------------------------------------ giao diện sáng / tối
const KHOA_GIAO_DIEN = "giai-chuoi-giao-dien";

function datGiaoDien(t, luu) {
  document.documentElement.dataset.theme = t;
  const nut = $("#nut-che-do");
  const sangToi = t === "toi" ? "sáng" : "tối";
  nut.setAttribute("aria-label", `Chuyển sang chế độ ${sangToi}`);
  nut.title = `Chế độ ${sangToi}`;
  if (luu) { try { localStorage.setItem(KHOA_GIAO_DIEN, t); } catch { /* chế độ riêng tư: bỏ qua */ } }
  capNhatNen3D();
}

function khoiTaoGiaoDien() {
  datGiaoDien(document.documentElement.dataset.theme === "toi" ? "toi" : "sang", false);
  $("#nut-che-do").addEventListener("click", () =>
    datGiaoDien(document.documentElement.dataset.theme === "toi" ? "sang" : "toi", true));
  // chưa chọn tay thì đi theo hệ điều hành
  const mq = window.matchMedia ? matchMedia("(prefers-color-scheme: dark)") : null;
  mq?.addEventListener?.("change", (e) => {
    let daChon = null;
    try { daChon = localStorage.getItem(KHOA_GIAO_DIEN); } catch { /* bỏ qua */ }
    if (!daChon) datGiaoDien(e.matches ? "toi" : "sang", false);
  });
}

// ------------------------------------------------------------ MỤC 06: TÌM CẤU TẠO CHẤT (TỪ ĐỀ DÀI)
let ketQuaTimChatHienTai = null;

async function khoiTaoTimCauTao() {
  const oDe = $("#de-bai-tim-chat");
  const oTracNghiem = $("#trac-nghiem-tim-chat");
  const oCauHoi = $("#cau-hoi-tim-chat");
  const chkDungClaude = $("#dung-claude-tim-chat");
  const nutGiai = $("#nut-giai-tim-chat");
  const vungTienDo = $("#tien-do-tim-chat");
  const msgTienDo = $("#thong-diep-tien-do-tim-chat");
  const subTienDo = $("#chi-tiet-tien-do-tim-chat");
  const vungKetQua = $("#khung-ket-qua-tim-chat");
  const vungCanhBao = $("#canh-bao-tim-chat");

  if (!oDe || !nutGiai) return;

  // Tải danh sách đề mẫu
  async function napDeMau() {
    try {
      const r = await fetch("/api/tim-cau-tao/mau");
      if (!r.ok) return;
      const data = await r.json();
      const ds = data.de_mau || [];
      const container = $("#ds-de-mau-tim-chat");
      if (!container) return;
      container.innerHTML = "";
      ds.forEach((dm, idx) => {
        const btn = document.createElement("button");
        btn.type = "button";
        btn.className = "chip-mau";
        btn.textContent = `${idx + 1}. ${dm.tieu_de}`;
        btn.title = (dm.de_bai || "").slice(0, 100) + "...";
        btn.onclick = () => {
          container.querySelectorAll(".chip-mau").forEach((c) => c.classList.remove("dang-chon"));
          btn.classList.add("dang-chon");
          oDe.value = dm.de_bai || "";
          oTracNghiem.value = dm.trac_nghiem || "";
          oCauHoi.value = dm.cau_hoi || "";
        };
        container.appendChild(btn);
      });
    } catch (e) {
      console.warn("Không tải được đề mẫu:", e);
    }
  }

  // Tải ảnh đề bài & OCR
  const nutTaiAnh = $("#nut-tai-anh-tim-chat");
  const fileAnh = $("#file-anh-tim-chat");
  const tenAnh = $("#ten-anh-tim-chat");
  const nutOcr = $("#nut-ocr-tim-chat");

  if (nutTaiAnh && fileAnh) {
    nutTaiAnh.onclick = () => fileAnh.click();
    fileAnh.onchange = () => {
      const f = fileAnh.files && fileAnh.files[0];
      if (f) {
        tenAnh.textContent = f.name;
        nutOcr.classList.remove("hidden");
      } else {
        tenAnh.textContent = "";
        nutOcr.classList.add("hidden");
      }
    };
  }

  if (nutOcr) {
    nutOcr.onclick = async () => {
      const f = fileAnh.files && fileAnh.files[0];
      if (!f) return;
      nutOcr.disabled = true;
      nutOcr.textContent = "Đang nhận diện...";
      try {
        const fd = new FormData();
        fd.append("anh", f);
        const r = await TaiKhoan.goi("/api/tim-cau-tao/ocr", { method: "POST", body: fd });
        if (!r.ok) {
          const err = await r.json().catch(() => ({}));
          throw new Error(err.loi || err.detail || "Không đọc được ảnh.");
        }
        const data = await r.json();
        if (data.de_bai) {
          oDe.value = data.de_bai;
          toast("Đã nhận diện đề từ ảnh! Bạn có thể kiểm tra và chỉnh sửa lại chữ nếu muốn.", "tin");
        }
      } catch (e) {
        toast(`Lỗi OCR: ${e.message}`, "loi");
      } finally {
        nutOcr.disabled = false;
        nutOcr.textContent = "🔍 Đọc ảnh đề (OCR)";
      }
    };
  }

  // Nút Giải
  if (nutGiai) {
    nutGiai.onclick = async () => {
      const deBai = oDe.value.trim();
      if (!deBai) {
        toast("Vui lòng dán đề bài hoặc tải ảnh đề.", "canh_bao");
        oDe.focus();
        return;
      }

      nutGiai.disabled = true;
      vungTienDo.classList.remove("hidden");
      vungKetQua.classList.add("hidden");
      msgTienDo.textContent = "Đang bắt đầu phân tích đề bài...";
      subTienDo.textContent = "Hệ thống chuẩn bị pipeline 5 bước.";

      try {
        const payload = {
          de_bai: deBai,
          trac_nghiem: oTracNghiem.value.trim(),
          cau_hoi: oCauHoi.value.trim(),
          dung_claude: chkDungClaude.checked,
          dung_cache: true,
          streaming: true,
        };

        const r = await TaiKhoan.goi("/api/tim-cau-tao/giai", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(payload),
        });

        if (!r.ok) {
          const err = await r.json().catch(() => ({}));
          throw new Error(err.loi || err.detail || "Lỗi khi xử lý.");
        }

        // Đọc SSE Stream
        const reader = r.body.getReader();
        const decoder = new TextDecoder("utf-8");
        let buffer = "";

        while (true) {
          const { done, value } = await reader.read();
          if (done) break;
          buffer += decoder.decode(value, { stream: true });
          const parts = buffer.split("\n\n");
          buffer = parts.pop() || "";

          for (const part of parts) {
            const line = part.trim();
            if (!line.startsWith("data:")) continue;
            const jsonStr = line.slice(5).trim();
            if (!jsonStr) continue;
            try {
              const ev = JSON.parse(jsonStr);
              if (ev.giai_doan === "loi") {
                throw new Error(ev.thong_diep || "Lỗi xử lý.");
              }
              if (ev.thong_diep) {
                msgTienDo.textContent = ev.thong_diep;
              }
              if (ev.giai_doan === "buoc_1") {
                subTienDo.textContent = "Trích xuất từng dữ kiện nguyên văn để phục vụ kiểm tra chéo.";
              } else if (ev.giai_doan === "buoc_2") {
                subTienDo.textContent = "Bảo toàn nguyên tố, mol, tỉ lệ %, độ bất bão hoà k và CTPT khả dĩ.";
              } else if (ev.giai_doan === "buoc_3") {
                subTienDo.textContent = "Biện luận chuyển hoá 4 bước và sinh tất cả ứng viên thỏa mãn.";
              } else if (ev.giai_doan === "buoc_4") {
                subTienDo.textContent = "Ma trận đối chiếu từng ứng viên với 100% dữ kiện đề bài.";
              } else if (ev.giai_doan === "xong" && ev.ket_qua) {
                ketQuaTimChatHienTai = ev.ket_qua;
                veKetQuaTimChat(ev.ket_qua);
              }
            } catch (eParse) {
              console.warn("Lỗi parse SSE chunk:", eParse);
            }
          }
        }
      } catch (err) {
        toast(`Lỗi: ${err.message}`, "loi");
      } finally {
        nutGiai.disabled = false;
        vungTienDo.classList.add("hidden");
      }
    };
  }

  // Vẽ kết quả
  function veKetQuaTimChat(kq) {
    vungKetQua.classList.remove("hidden");
    vungKetQua.scrollIntoView({ behavior: "smooth", block: "start" });

    // Độ tin cậy
    const badgeTC = $("#badge-tin-cay-tim-chat");
    const doTC = (kq.do_tin_cay || "cao").toLowerCase();
    badgeTC.className = `huy-hieu-tin-cay ${doTC}`;
    badgeTC.textContent = `Độ tin cậy: ${doTC.toUpperCase()}`;

    const modelEl = $("#ai-model-tim-chat");
    if (modelEl) modelEl.textContent = kq.mo_hinh_giai ? `(Mô hình: ${kq.mo_hinh_giai})` : "";

    // Cảnh báo
    if (kq.canh_bao && kq.canh_bao.length > 0) {
      vungCanhBao.classList.remove("hidden");
      vungCanhBao.innerHTML = `<b>Lưu ý:</b> ` + kq.canh_bao.map((c) => esc(c)).join("<br>");
    } else {
      vungCanhBao.classList.add("hidden");
      vungCanhBao.innerHTML = "";
    }

    // Đáp án trắc nghiệm nếu có
    const badgeTN = $("#dap-an-tn-badge");
    if (kq.dap_an_trac_nghiem) {
      badgeTN.classList.remove("hidden");
      badgeTN.textContent = `Đáp án: ${kq.dap_an_trac_nghiem}`;
    } else {
      badgeTN.classList.add("hidden");
    }

    // Lưới đáp án cấu tạo
    const luoiDA = $("#luoi-dap-an-chat");
    luoiDA.innerHTML = "";
    const dapAnMap = kq.dap_an || {};
    for (const [chat, ctct] of Object.entries(dapAnMap)) {
      const card = document.createElement("div");
      card.className = "chat-dap-an-card";
      card.innerHTML = `
        <div class="chat-dap-an-ten">Chất ${esc(chat)}</div>
        <div class="chat-dap-an-ctct">${esc(ctct)}</div>
      `;
      luoiDA.appendChild(card);
    }

    // Kết luận
    const klEl = $("#ket-luan-tim-chat");
    if (klEl) klEl.innerHTML = kq.ket_luan ? `<b>Tóm tắt lời giải:</b> ${esc(kq.ket_luan)}` : "";

    // Phần 1: Bảng dữ kiện
    $("#dem-so-du-kien").textContent = String((kq.bang_du_kien || []).length);
    const dsDK = $("#ds-du-kien-trich");
    dsDK.innerHTML = "";
    (kq.bang_du_kien || []).forEach((dk) => {
      const div = document.createElement("div");
      div.className = "dk-item";
      const tagLoai = dk.loai === "dinh_luong" ? "Định lượng" : "Định tính";
      div.innerHTML = `
        <span class="dk-id">${esc(dk.id)}</span>
        <span class="dk-tag ${esc(dk.loai)}">${tagLoai}</span>
        <span class="dk-text">${esc(dk.noi_dung_goc)}</span>
      `;
      dsDK.appendChild(div);
    });

    // Phần 2: Suy luận & Tính toán code
    const khoiTinh = $("#khoi-tinh-toan-code");
    const tc = kq.tinh_toan_code || {};
    let strTinh = "<b>Số liệu tính toán từ module code SGK:</b><br>";
    if (tc.dot_chay && Object.keys(tc.dot_chay).length > 0) {
      const dc = tc.dot_chay;
      strTinh += `• Đốt cháy: n(C) = ${dc.n_C || 0} mol; n(H) = ${dc.n_H || 0} mol; n(O) = ${dc.n_O || 0} mol → CT đơn giản nhất: <b>${dc.ct_don_gian || 'Chưa rõ'}</b><br>`;
    }
    if (tc.cong_thuc_don_gian && tc.cong_thuc_don_gian.ct_don_gian) {
      strTinh += `• Tỉ lệ %: CT đơn giản nhất = <b>${tc.cong_thuc_don_gian.ct_don_gian}</b> (M_đg = ${tc.cong_thuc_don_gian.M_don_gian || 0})<br>`;
    }
    if (tc.ctpt_kha_di && tc.ctpt_kha_di.length > 0) {
      strTinh += `• CTPT khả dĩ phù hợp hoá trị: ${tc.ctpt_kha_di.map((c) => `<b>${c.ctpt}</b> (k=${c.k}, M=${c.M})`).join(", ")}<br>`;
    }
    if (tc.khoi_luong_mol && Object.keys(tc.khoi_luong_mol).length > 0) {
      strTinh += `• Khối lượng mol & Delta: ` + Object.entries(tc.khoi_luong_mol).map(([k, v]) => `${k} (M=${v}, k=${tc.do_bat_bao_hoa[k] ?? '?'})`).join(" | ") + `<br>`;
    }
    khoiTinh.innerHTML = strTinh;

    const dsSL = $("#ds-cac-buoc-suy-luan");
    dsSL.innerHTML = "";
    (kq.cac_buoc_suy_luan || []).forEach((b) => {
      const div = document.createElement("div");
      div.className = "p-3 rounded-lg border border-chi bg-giay-lom";
      div.innerHTML = `
        <div class="font-bold text-sm text-nhan mb-1">Bước ${b.buoc}: ${esc(b.tieu_de)}</div>
        <div class="text-sm leading-relaxed">${esc(b.noi_dung).replace(/\n/g, '<br>')}</div>
        ${b.can_cu_du_kien && b.can_cu_du_kien.length > 0 ? `<div class="mt-2 text-xs text-lang font-mono">Căn cứ: ${b.can_cu_du_kien.map(esc).join(", ")}</div>` : ''}
      `;
      dsSL.appendChild(div);
    });

    // Phần 3: Ma trận kiểm tra chéo
    const tableHead = $("#matrix-head-row");
    const tableBody = $("#matrix-body");
    tableHead.innerHTML = `<th>Dữ kiện</th><th>Nội dung trích nguyên văn</th>`;

    const ungVienList = kq.ung_vien || [];
    ungVienList.forEach((uv) => {
      const th = document.createElement("th");
      th.innerHTML = `${esc(uv.ten_ung_vien)}<br><span class="text-xs font-normal font-mono ${uv.dat_chuan ? 'text-nhan' : 'text-do'}">${uv.dat_chuan ? '✓ ĐẠT' : '✗ LOẠI'}</span>`;
      tableHead.appendChild(th);
    });

    tableBody.innerHTML = "";
    (kq.bang_kiem_tra || []).forEach((row) => {
      const tr = document.createElement("tr");
      let html = `<td class="font-mono font-bold text-nhan">${esc(row.id_du_kien)}</td><td class="text-xs max-w-xs">${esc(row.noi_dung_goc)}</td>`;
      ungVienList.forEach((uv) => {
        const res = (row.ket_qua_ung_vien || {})[uv.ten_ung_vien] || {};
        const ok = res.thoa_man !== false;
        html += `<td><span class="${ok ? 'check-ok' : 'check-no'}">${ok ? '✓ Thỏa' : '✗ Sai'}</span><div class="text-xs text-lang mt-1">${esc(res.giai_thich || '')}</div></td>`;
      });
      tr.innerHTML = html;
      tableBody.appendChild(tr);
    });
  }

  // Nút Xuất ảnh
  const nutXuatAnh = $("#nut-xuat-anh-tim-chat");
  if (nutXuatAnh) {
    nutXuatAnh.onclick = async () => {
      if (!ketQuaTimChatHienTai) return;
      nutXuatAnh.disabled = true;
      nutXuatAnh.textContent = "Đang tạo ảnh...";
      try {
        const r = await TaiKhoan.goi("/api/tim-cau-tao/xuat-anh", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ ket_qua: ketQuaTimChatHienTai }),
        });
        if (!r.ok) throw new Error("Không thể tạo ảnh.");
        const blob = await r.blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = `loi_giai_tim_cau_tao_${Date.now()}.png`;
        a.click();
        URL.revokeObjectURL(url);
        toast("Đã xuất ảnh lời giải PNG thành công!", "tin");
      } catch (e) {
        toast(`Lỗi xuất ảnh: ${e.message}`, "loi");
      } finally {
        nutXuatAnh.disabled = false;
        nutXuatAnh.textContent = "🖼️ Xuất ảnh lời giải (PNG)";
      }
    };
  }

  // Nút Sao chép
  const nutSaoChep = $("#nut-sao-chep-tim-chat");
  if (nutSaoChep) {
    nutSaoChep.onclick = () => {
      if (!ketQuaTimChatHienTai) return;
      const kq = ketQuaTimChatHienTai;
      let text = `LỜI GIẢI TÌM CẤU TẠO CHẤT\nĐề bài: ${kq.de_bai}\n\n`;
      text += `1. BẢNG DỮ KIỆN:\n` + (kq.bang_du_kien || []).map((dk) => `[${dk.id}] ${dk.noi_dung_goc}`).join("\n") + "\n\n";
      text += `2. SUY LUẬN:\n` + (kq.cac_buoc_suy_luan || []).map((b) => `Bước ${b.buoc} (${b.tieu_de}):\n${b.noi_dung}`).join("\n\n") + "\n\n";
      text += `3. ĐÁP ÁN CẤU TẠO:\n` + Object.entries(kq.dap_an || {}).map(([k, v]) => `Chất ${k}: ${v}`).join("\n");
      if (kq.dap_an_trac_nghiem) text += `\nĐáp án trắc nghiệm: ${kq.dap_an_trac_nghiem}`;
      navigator.clipboard.writeText(text).then(() => {
        toast("Đã sao chép lời giải vào bộ nhớ tạm!", "tin");
      });
    };
  }

  // Tải đề mẫu khi khởi tạo
  napDeMau();
}

khoiTaoGiaoDien();
khoiTaoNhapLieu();
khoiTaoCotBen();
khoiTao3D();
khoiTaoCoChe();
khoiTaoTimCauTao();
veCacBuocTienTrinh();
TaiKhoan.khiDoi(veTrangThaiKey);
TaiKhoan.khiDoi(veCongTacClaude);
TaiKhoan.khiDoi(veChonAI);
TaiKhoan.khiDoi(veQuyenToiDa);
function canChinhTooltip(hop) {
  if (!hop) return;
  hop.style.left = "50%";
  hop.style.transform = "translateX(-50%)";
  const r = hop.getBoundingClientRect();
  const pad = 12;
  if (r.left < pad) {
    const shift = pad - r.left;
    hop.style.left = `calc(50% + ${shift}px)`;
  } else if (r.right > window.innerWidth - pad) {
    const shift = r.right - (window.innerWidth - pad);
    hop.style.left = `calc(50% - ${shift}px)`;
  }
  if (r.top < 10) {
    hop.classList.add("duoi");
  } else {
    hop.classList.remove("duoi");
  }
}

function khoiTaoTuyChonGiai() {
  document.querySelectorAll('input[name="nguon-giai"]').forEach((r) => {
    r.addEventListener("change", capNhatGhiChuChonAI);
  });
  capNhatGhiChuChonAI();

  // Tooltips dấu hỏi (?): bấm mở, bấm ra ngoài đóng
  document.addEventListener("click", (e) => {
    const nut = e.target.closest(".nut-meo");
    if (nut) {
      e.preventDefault();
      e.stopPropagation();
      const cha = nut.closest(".hop-meo-cha");
      const dangMo = cha?.classList.contains("mo");
      document.querySelectorAll(".hop-meo-cha.mo").forEach((el) => el.classList.remove("mo"));
      if (!dangMo && cha) {
        cha.classList.add("mo");
        canChinhTooltip(cha.querySelector(".hop-meo"));
      }
      return;
    }
    if (!e.target.closest(".hop-meo")) {
      document.querySelectorAll(".hop-meo-cha.mo").forEach((el) => el.classList.remove("mo"));
    }
  });

  // Desktop hover: tự căn lề không bị cắt mép
  document.querySelectorAll(".hop-meo-cha").forEach((cha) => {
    cha.addEventListener("mouseenter", () => {
      canChinhTooltip(cha.querySelector(".hop-meo"));
    });
  });
}

function khoiTaoTogglePhanHe() {
  const toggle = $("#toggle-phan-he");
  const btnHoa = $("#btn-toggle-hoa");
  const btnSinh = $("#btn-toggle-sinh");
  if (!toggle || !btnHoa || !btnSinh) return;

  btnSinh.addEventListener("click", (e) => {
    e.preventDefault();
    if (toggle.dataset.active === "sinh") return;
    toggle.dataset.active = "sinh";
    btnHoa.classList.remove("active");
    btnHoa.setAttribute("aria-selected", "false");
    btnSinh.classList.add("active");
    btnSinh.setAttribute("aria-selected", "true");

    // Hiệu ứng chuyển cảnh 60fps mượt mà kiểu Claude Chat ⇄ Code
    const vungChinh = document.querySelector("main") || document.querySelector("#trang-chu") || document.body;
    if (vungChinh) {
      vungChinh.style.willChange = "transform, opacity";
      vungChinh.style.transition = "opacity 0.22s cubic-bezier(0.22, 1, 0.36, 1), transform 0.22s cubic-bezier(0.22, 1, 0.36, 1)";
      vungChinh.style.opacity = "0";
      vungChinh.style.transform = "translate3d(-20px, 0, 0)";
    }
    setTimeout(() => {
      window.location.href = "/sinh-hoc";
    }, 200);
  });

  btnHoa.addEventListener("click", (e) => {
    e.preventDefault();
    if (toggle.dataset.active !== "sinh") return;
    toggle.dataset.active = "hoa";
    btnSinh.classList.remove("active");
    btnSinh.setAttribute("aria-selected", "false");
    btnHoa.classList.add("active");
    btnHoa.setAttribute("aria-selected", "true");
  });
}

khoiTaoThongMinh();
khoiTaoTraCuuTL();
khoiTaoTuyChonGiai();
khoiTaoTogglePhanHe();
$("#nut-pt").addEventListener("click", xemPhuongTrinh);
$("#nang-muc").addEventListener("click", (e) => {
  const n = e.target.closest("[data-nang-muc]");
  if (!n) return;
  const o = document.querySelector(`input[name="muc-do"][value="${n.dataset.nangMuc}"]`);
  if (o) o.checked = true;
  batDauGiai();
});
TaiKhoan.tai()
  .catch(() => toast("Không kết nối được server API (/api/toi). Nếu đưa app lên Cloudflare, hãy chạy app bằng start.command rồi mở qua Cloudflare Tunnel – không đưa riêng thư mục frontend lên Cloudflare Pages.", "loi", 12000))
  .finally(() => { kiemTraHeThong(); moBaiTuUrl(); });
