// Trang Đăng nhập: email + mật khẩu (đăng nhập / tạo tài khoản), "Tiếp tục với Google" khi server đã bật Google.
// Tạo tài khoản (hoặc tài khoản cũ chưa xác thực) → nhập mã 6 số gửi qua email; "Quên mật khẩu?" → mã qua email → mật khẩu mới.
// Form đăng ký có Cloudflare Turnstile khi server đã cấu hình TURNSTILE_*.
"use strict";

const $ = (s) => document.querySelector(s);
const thamSo = new URLSearchParams(location.search);
const tiep = (() => { // chỉ quay về đường dẫn nội bộ
  const t = thamSo.get("tiep") || "/";
  return t.startsWith("/") && !t.startsWith("//") && !t.includes("\\") ? t : "/";
})();

function esc(s) {
  return String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

function hienLoi(el, thongDiep) {
  el.classList.toggle("hidden", !thongDiep);
  el.innerHTML = thongDiep ? `<b aria-hidden="true">✕</b><span>${esc(thongDiep)}</span>` : "";
}
const baoLoi = (thongDiep) => hienLoi($("#loi-dang-nhap"), thongDiep);

const cheDo = () => document.querySelector('input[name="che-do"]:checked').value;

function datCheDo(cd) {
  const dangKy = cd === "dang-ky";
  document.querySelector(`input[name="che-do"][value="${cd}"]`).checked = true;
  $("#khung-ten").classList.toggle("hidden", !dangKy);
  $("#khung-mk2").classList.toggle("hidden", !dangKy);
  $("#goi-y-mk").classList.toggle("hidden", !dangKy);
  $("#nut-quen-mk").classList.toggle("hidden", dangKy);
  $("#o-mat-khau").autocomplete = dangKy ? "new-password" : "current-password";
  $("#nut-gui .nhan").textContent = dangKy ? "Tạo tài khoản" : "Đăng nhập";
  $("#ghi-chu-the").innerHTML = dangKy
    ? "Đã có tài khoản? Chọn <b>Đăng nhập</b> ở trên."
    : "Chưa có tài khoản? Chọn <b>Tạo tài khoản</b> ở trên.";
  baoLoi("");
  if (dangKy) veTurnstile();
}

async function postJson(url, duLieu) {
  const r = await TaiKhoan.goi(url, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(duLieu || {}),
  });
  const d = await r.json().catch(() => ({}));
  return { r, d };
}

function dangChay(nut, bat) {
  nut.disabled = bat;
  nut.querySelector(".quay")?.classList.toggle("hidden", !bat);
}

// ------------------------------------------------------------ Cloudflare Turnstile (chỉ ở form Tạo tài khoản)
let turnstileKhoa = "";
let turnstileToken = "";
let turnstileWidget = null;
let turnstileDangTai = null;

function veTurnstile() {
  if (!turnstileKhoa) return;
  const khung = $("#khung-turnstile");
  khung.classList.remove("hidden");
  if (turnstileWidget !== null || turnstileDangTai) return;
  turnstileDangTai = new Promise((xong, loi) => {
    const s = document.createElement("script");
    s.src = "https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit";
    s.async = true;
    s.onload = xong;
    s.onerror = loi;
    document.head.appendChild(s);
  }).then(() => {
    turnstileWidget = window.turnstile.render(khung, {
      sitekey: turnstileKhoa,
      theme: document.documentElement.dataset.theme === "toi" ? "dark" : "light",
      language: "vi",
      callback: (t) => { turnstileToken = t; },
      "expired-callback": () => { turnstileToken = ""; },
      "error-callback": () => { turnstileToken = ""; },
    });
  }).catch(() => baoLoi("Không tải được ô xác minh “Tôi không phải robot” – kiểm tra mạng rồi tải lại trang."));
}

function lamMoiTurnstile() {
  turnstileToken = "";
  if (turnstileWidget !== null && window.turnstile) window.turnstile.reset(turnstileWidget);
}

// ------------------------------------------------------------ các bước: thẻ chính ↔ quên mật khẩu ↔ nhập mã
let buocMa = null; // { kieu: "xac_thuc" | "dat_lai", email }

function hienBuoc(buoc) {
  const chinh = buoc === "chinh";
  $("#form-dang-nhap").classList.toggle("hidden", !chinh);
  document.querySelector(".phan-doan").classList.toggle("hidden", !chinh);
  $("#ghi-chu-the").classList.toggle("hidden", !chinh);
  if (TaiKhoan.toi?.dang_nhap_google) $("#khu-google").classList.toggle("hidden", !chinh);
  $("#form-quen").classList.toggle("hidden", buoc !== "quen");
  $("#form-ma").classList.toggle("hidden", buoc !== "ma");
  hienLoi($("#loi-quen"), "");
  hienLoi($("#loi-ma"), "");
  if (buoc === "quen") $("#o-email-quen").focus();
  if (buoc === "ma") { $("#o-ma").value = ""; $("#o-ma").focus(); }
  if (chinh) { buocMa = null; dungDemNguoc(); }
}

let demNguocId = null;
function dungDemNguoc() {
  clearInterval(demNguocId);
  demNguocId = null;
  const nut = $("#nut-gui-lai-ma");
  nut.disabled = false;
  nut.textContent = "Gửi lại mã";
}
function demNguoc(giay) {
  dungDemNguoc();
  let con = Math.max(0, Math.ceil(giay || 0));
  const nut = $("#nut-gui-lai-ma");
  const ve = () => {
    if (con <= 0) { dungDemNguoc(); return; }
    nut.disabled = true;
    nut.textContent = con >= 120 ? `Gửi lại mã (${Math.ceil(con / 60)} phút)` : `Gửi lại mã (${con}s)`;
    con -= 1;
  };
  ve();
  if (con > 0) demNguocId = setInterval(ve, 1000);
}

function moBuocMa(kieu, email, thongBao, cho) {
  buocMa = { kieu, email };
  const datLai = kieu === "dat_lai";
  $("#tieu-de-ma").textContent = datLai ? "Đặt lại mật khẩu" : "Xác thực email";
  $("#khung-mk-moi").classList.toggle("hidden", !datLai);
  $("#nut-xac-nhan-ma .nhan").textContent = datLai ? "Đặt mật khẩu mới" : "Xác nhận";
  $("#mo-ta-ma").textContent = thongBao || `Đã gửi mã 6 số tới ${email}.`;
  hienBuoc("ma");
  demNguoc(cho ?? 60);
}

// ------------------------------------------------------------ đăng nhập / tạo tài khoản
async function gui(e) {
  e.preventDefault();
  const nut = $("#nut-gui");
  if (nut.disabled) return;
  const dangKy = cheDo() === "dang-ky";
  const email = $("#o-email").value.trim();
  const matKhau = $("#o-mat-khau").value;
  if (!/^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$/.test(email)) { baoLoi("Hãy nhập email hợp lệ."); $("#o-email").focus(); return; }
  if (dangKy && matKhau.length < 8) { baoLoi("Mật khẩu cần ít nhất 8 ký tự."); $("#o-mat-khau").focus(); return; }
  if (!matKhau) { baoLoi("Hãy nhập mật khẩu."); $("#o-mat-khau").focus(); return; }
  if (dangKy && matKhau !== $("#o-mat-khau-2").value) { baoLoi("Hai mật khẩu chưa khớp."); $("#o-mat-khau-2").focus(); return; }
  if (dangKy && turnstileKhoa && !turnstileToken) { baoLoi("Hãy hoàn thành ô xác minh “Tôi không phải robot” ở trên."); return; }

  dangChay(nut, true);
  baoLoi("");
  try {
    const { r, d } = await postJson(dangKy ? "/auth/dang-ky" : "/auth/dang-nhap-mat-khau",
      dangKy ? { email, mat_khau: matKhau, ten: $("#o-ten").value, tiep, captcha: turnstileToken, website: $("#o-website").value } : { email, mat_khau: matKhau, tiep });
    if (r.ok && d.can_xac_thuc) { // tài khoản cần xác thực email → bước nhập mã
      dangChay(nut, false);
      moBuocMa("xac_thuc", d.email || email, d.thong_bao, d.cho_gui_lai);
      return;
    }
    if (!r.ok) {
      if (d.ma === "email_da_co") datCheDo("dang-nhap");
      if (dangKy) lamMoiTurnstile();
      throw new Error(d.loi || "Không đăng nhập được, hãy thử lại.");
    }
    location.href = d.tiep || tiep;
  } catch (err) {
    baoLoi(err.message || "Không kết nối được server.");
    dangChay(nut, false);
  }
}

// ------------------------------------------------------------ nhập mã (xác thực email / đặt lại mật khẩu)
async function guiMa(e) {
  e.preventDefault();
  const nut = $("#nut-xac-nhan-ma");
  if (nut.disabled || !buocMa) return;
  const loi = (t) => hienLoi($("#loi-ma"), t);
  const ma = $("#o-ma").value.replace(/\D/g, "");
  if (ma.length !== 6) { loi("Mã gồm 6 chữ số."); $("#o-ma").focus(); return; }
  let duLieu = { ma };
  let url = "/auth/xac-thuc-email";
  if (buocMa.kieu === "dat_lai") {
    const mk = $("#o-mk-moi").value;
    if (mk.length < 8) { loi("Mật khẩu mới cần ít nhất 8 ký tự."); $("#o-mk-moi").focus(); return; }
    if (mk !== $("#o-mk-moi-2").value) { loi("Hai mật khẩu mới chưa khớp."); $("#o-mk-moi-2").focus(); return; }
    url = "/auth/dat-lai-mat-khau";
    duLieu = { email: buocMa.email, ma, mat_khau_moi: mk, tiep };
  }
  dangChay(nut, true);
  loi("");
  try {
    const { r, d } = await postJson(url, duLieu);
    if (!r.ok) {
      if (d.ma === "het_phien_xac_thuc") hienBuoc("chinh");
      throw new Error(d.loi || "Mã không đúng, hãy thử lại.");
    }
    location.href = d.tiep || tiep;
  } catch (err) {
    (buocMa ? loi : baoLoi)(err.message || "Không kết nối được server.");
    dangChay(nut, false);
    $("#o-ma").select();
  }
}

async function guiLaiMa() {
  if (!buocMa) return;
  const nut = $("#nut-gui-lai-ma");
  nut.disabled = true;
  hienLoi($("#loi-ma"), "");
  try {
    const { r, d } = buocMa.kieu === "dat_lai"
      ? await postJson("/auth/quen-mat-khau", { email: buocMa.email })
      : await postJson("/auth/gui-lai-ma");
    if (!r.ok) {
      if (d.cho) demNguoc(d.cho); else nut.disabled = false;
      throw new Error(d.loi || "Không gửi lại được mã.");
    }
    $("#mo-ta-ma").textContent = d.thong_bao || `Đã gửi mã mới tới ${buocMa.email}.`;
    $("#o-ma").value = "";
    $("#o-ma").focus();
    demNguoc(d.cho_gui_lai ?? 60);
  } catch (err) {
    hienLoi($("#loi-ma"), err.message || "Không kết nối được server.");
  }
}

async function guiQuen(e) {
  e.preventDefault();
  const nut = $("#nut-gui-ma-quen");
  if (nut.disabled) return;
  const email = $("#o-email-quen").value.trim();
  if (!/^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$/.test(email)) { hienLoi($("#loi-quen"), "Hãy nhập email hợp lệ."); return; }
  dangChay(nut, true);
  try {
    const { r, d } = await postJson("/auth/quen-mat-khau", { email });
    if (!r.ok) throw new Error(d.loi || "Không gửi được mã.");
    moBuocMa("dat_lai", email, d.thong_bao, d.cho_gui_lai);
  } catch (err) {
    hienLoi($("#loi-quen"), err.message || "Không kết nối được server.");
  } finally {
    dangChay(nut, false);
  }
}

function khoiTaoGiaoDien() {
  const nut = $("#nut-che-do");
  const dat = (t, luu) => {
    document.documentElement.dataset.theme = t;
    const sangToi = t === "toi" ? "sáng" : "tối";
    nut.setAttribute("aria-label", `Chuyển sang chế độ ${sangToi}`);
    nut.title = `Chế độ ${sangToi}`;
    if (luu) { try { localStorage.setItem("giai-chuoi-giao-dien", t); } catch { /* bỏ qua */ } }
  };
  dat(document.documentElement.dataset.theme === "toi" ? "toi" : "sang", false);
  nut.addEventListener("click", () => dat(document.documentElement.dataset.theme === "toi" ? "sang" : "toi", true));
}

khoiTaoGiaoDien();
document.querySelectorAll('input[name="che-do"]').forEach((r) => r.addEventListener("change", () => datCheDo(cheDo())));
$("#form-dang-nhap").addEventListener("submit", gui);
$("#form-ma").addEventListener("submit", guiMa);
$("#form-quen").addEventListener("submit", guiQuen);
$("#nut-gui-lai-ma").addEventListener("click", guiLaiMa);
$("#nut-quen-mk").addEventListener("click", () => {
  $("#o-email-quen").value = $("#o-email").value.trim();
  hienBuoc("quen");
});
document.querySelectorAll(".nut-quay-lai").forEach((n) => n.addEventListener("click", () => hienBuoc("chinh")));
$("#o-ma").addEventListener("input", (e) => { // chỉ giữ chữ số; đủ 6 số thì tự gửi (trừ khi còn phải nhập mật khẩu mới)
  e.target.value = e.target.value.replace(/\D/g, "").slice(0, 6);
  if (e.target.value.length === 6 && buocMa?.kieu === "xac_thuc") $("#form-ma").requestSubmit();
});
$("#nut-hien-mk").addEventListener("click", (e) => {
  const hien = $("#o-mat-khau").type === "password";
  for (const o of [$("#o-mat-khau"), $("#o-mat-khau-2")]) o.type = hien ? "text" : "password";
  e.currentTarget.textContent = hien ? "Ẩn" : "Hiện";
  e.currentTarget.setAttribute("aria-pressed", String(hien));
});
datCheDo(thamSo.get("che_do") === "dang-ky" ? "dang-ky" : "dang-nhap");
if (thamSo.get("loi") === "google") baoLoi("Đăng nhập Google không thành công (hoặc email chưa được Google xác minh). Hãy thử lại hoặc dùng email.");
if (thamSo.get("loi") === "email_tam") baoLoi("Vui lòng dùng email thật, không hỗ trợ email tạm thời – tài khoản Google này dùng tên miền email không được chấp nhận.");
if (thamSo.get("loi") === "khoa") baoLoi("Tài khoản này đã bị admin khoá. Hãy liên hệ admin để được mở khoá.");

TaiKhoan.tai()
  .then((t) => {
    $("#so-luot").textContent = t.dung_thu?.gioi_han ?? 3;
    if (t.dang_nhap_google) {
      $("#khu-google").classList.remove("hidden");
      $("#nut-google").href = "/auth/dang-nhap?tiep=" + encodeURIComponent(tiep);
    }
    turnstileKhoa = t.turnstile_site_key || "";
    if (cheDo() === "dang-ky") veTurnstile();
    $("#o-email").focus();
  })
  .catch(() => baoLoi("Không kết nối được server API. Hãy kiểm tra app đang chạy (tu-chay.command / start.command)."));
