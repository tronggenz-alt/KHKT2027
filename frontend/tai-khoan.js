// Tài khoản – dùng chung cho index.html, cai-dat.html, dang-nhap.html: thông tin người dùng (/api/toi), mã CSRF,
// gọi API kèm header X-CSRF-Token, khu tài khoản ở thanh trên (nút "Đăng nhập" → /dang-nhap, hoặc ảnh đại diện → Cài đặt).
// Trình duyệt không bao giờ nhận key Gemini, chỉ 4 ký tự cuối.
"use strict";

const TaiKhoan = (() => {
  let toi = null;
  const dsNghe = [];

  const escTK = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

  const BIEU_TUONG_NGUOI = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0 1 16 0"/></svg>`;

  async function tai() {
    let r;
    try {
      r = await fetch("/api/toi", { cache: "no-store", credentials: "same-origin" });
    } catch (e) {
      veThanhTren(true);
      throw e;
    }
    if (!r.ok || !(r.headers.get("content-type") || "").includes("json")) {
      veThanhTren(true); // vd chỉ đưa thư mục frontend lên hosting tĩnh: không có server API
      throw new Error("Không lấy được thông tin tài khoản từ server.");
    }
    toi = await r.json();
    veThanhTren();
    dsNghe.forEach((f) => f(toi));
    return toi;
  }

  // fetch kèm mã CSRF; mã cũ (phiên vừa đổi) thì lấy mã mới một lần rồi gửi lại
  async function goi(url, tuyChon = {}) {
    if (!toi) await tai();
    const lam = () => {
      const headers = new Headers(tuyChon.headers || {});
      headers.set("X-CSRF-Token", toi.csrf);
      return fetch(url, { credentials: "same-origin", ...tuyChon, headers });
    };
    let r = await lam();
    if (r.status === 403) {
      const d = await r.clone().json().catch(() => ({}));
      if (d.ma === "csrf") { await tai(); r = await lam(); }
    }
    return r;
  }

  const urlDangNhap = () => "/dang-nhap?tiep=" + encodeURIComponent(location.pathname + location.search);

  async function dangXuat() {
    try { await goi("/auth/dang-xuat", { method: "POST", headers: { Accept: "application/json" } }); } finally { location.href = "/"; }
  }

  function anhDaiDien(nd, lon = false) {
    const lop = "anh-dai-dien" + (lon ? " lon" : "");
    if (nd.anh) return `<img class="${lop}" src="${escTK(nd.anh)}" alt="" referrerpolicy="no-referrer">`;
    const chu = nd.cuc_bo ? "★" : (nd.ten || nd.email || "?").trim().charAt(0).toUpperCase();
    return `<span class="${lop}" aria-hidden="true">${escTK(chu)}</span>`;
  }

  function nutDangNhap(chu = "Đăng nhập", lop = "btn btn-sm") {
    return `<a href="${escTK(urlDangNhap())}" class="${lop} nut-google">${BIEU_TUONG_NGUOI}<span class="chu-dai">${escTK(chu)}</span></a>`;
  }

  // Khu tài khoản ở thanh trên – luôn có một nút, không bao giờ để trống
  function veThanhTren(loiKetNoi = false) {
    const khu = document.getElementById("khu-tai-khoan");
    if (!khu) return;
    if (loiKetNoi || !toi || !toi.dang_nhap) {
      khu.innerHTML = nutDangNhap(); // luôn có nút – không bao giờ để trống
      return;
    }
    if (toi.dang_nhap) {
      const nd = toi.nguoi_dung;
      const ten = nd.cuc_bo ? "Máy này" : (nd.ten || nd.email);
      const moi = toi.la_chu_app && toi.hoat_dong_moi > 0; // admin: có người đăng ký/đăng nhập kể từ lần xem nhật ký gần nhất
      const cham = moi ? `<span class="cham-thong-bao" aria-label="${toi.hoat_dong_moi} hoạt động mới"></span>` : "";
      khu.innerHTML = `<a href="/cai-dat${moi ? "#muc-hoat-dong" : ""}" class="chip-nguoi-dung" title="Cài đặt: tài khoản, key Gemini, lịch sử${moi ? ` · ${toi.hoat_dong_moi} lượt đăng ký/đăng nhập mới` : ""}">${cham}${anhDaiDien(nd)}<span class="ten">${escTK(ten)}</span>
        <svg class="banh-rang" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/></svg></a>`;
    }
  }

  return {
    tai, goi, dangXuat, urlDangNhap, anhDaiDien, nutDangNhap,
    khiDoi: (f) => dsNghe.push(f),
    get toi() { return toi; },
  };
})();
