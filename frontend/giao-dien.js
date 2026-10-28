// Quản lý GIAO DIỆN (theme) + hộp thoại nút "+K" (nhập mã do admin cấp).
// Theme = biến CSS (đặt inline trên <html>, thắng cả chế độ sáng / tối) + lớp hiệu ứng bật / tắt – KHÔNG đụng bố cục.
// Cấu hình từng theme: frontend/themes/<id>.js (window.GIAO_DIEN[id]). Lựa chọn lưu localStorage (bọc try/catch).
// Mã chỉ kiểm ở server: POST /api/theme/redeem { code } → { themeId } (CSDL lưu HMAC, không có mã nào trong code này).
const GiaoDien = (() => {
  const KHOA = "giai-chuoi-giao-dien-ma";
  const html = document.documentElement;
  let hienTai = "default";
  let bienDaDat = [];
  const giam = () => window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const cauHinh = (id) => (window.GIAO_DIEN || {})[id] || (window.GIAO_DIEN || {}).default;
  const doc = () => { try { return localStorage.getItem(KHOA) || "default"; } catch { return "default"; } };
  const luu = (id) => { try { if (id === "default") localStorage.removeItem(KHOA); else localStorage.setItem(KHOA, id); } catch { /* chế độ riêng tư */ } };
  const khiSanSang = (f) => (document.body ? f() : document.addEventListener("DOMContentLoaded", f, { once: true }));

  function datBien(cfg) {
    bienDaDat.forEach((k) => html.style.removeProperty(k));
    bienDaDat = [];
    Object.entries(cfg.bien || {}).forEach(([k, v]) => { html.style.setProperty(k, v); bienDaDat.push(k); });
    if (cfg.sang) { html.style.setProperty("color-scheme", "light"); bienDaDat.push("color-scheme"); }
    html.dataset.giaoDien = cfg.id;
  }

  // chuyen: hiệu ứng mờ chuyển ~400 ms (View Transitions nếu trình duyệt có, không thì chuyển màu CSS); mua: theme chạy hiệu ứng "mới áp dụng"
  function ap(id, { chuyen = false, mua = false, luuLai = true } = {}) {
    const cfg = cauHinh(id), cu = cauHinh(hienTai);
    if (luuLai) luu(cfg.id);
    const doi = () => {
      if (cu !== cfg) cu?.hieuUng?.tat?.();
      datBien(cfg);
      hienTai = cfg.id;
      khiSanSang(() => cfg.hieuUng?.bat?.({ mua }));
    };
    if (chuyen && !giam() && document.startViewTransition) document.startViewTransition(doi);
    else if (chuyen && !giam()) {
      html.classList.add("dang-doi-giao-dien");
      doi();
      setTimeout(() => html.classList.remove("dang-doi-giao-dien"), 450);
    } else doi();
    document.dispatchEvent(new CustomEvent("giao-dien:doi", { detail: { id: cfg.id } }));
    return cfg;
  }

  // ------------------------------------------------------------ hộp thoại "+K"
  // gọi API kèm mã CSRF: dùng TaiKhoan.goi (tai-khoan.js – khai báo const nên không nằm trên window) nếu có, không thì tự lấy CSRF
  async function goiApi(url, tuyChon) {
    if (typeof TaiKhoan !== "undefined" && TaiKhoan.goi) return TaiKhoan.goi(url, tuyChon);
    const csrf = (await (await fetch("/api/toi", { credentials: "same-origin" })).json()).csrf;
    return fetch(url, { credentials: "same-origin", ...tuyChon, headers: { ...(tuyChon.headers || {}), "X-CSRF-Token": csrf } });
  }
  let hop = null;
  function taoHop() {
    hop = document.createElement("dialog");
    hop.className = "hop-ma-gd";
    hop.setAttribute("aria-labelledby", "td-ma-gd");
    hop.innerHTML = `<form method="dialog" class="hop-ma-gd-trong" novalidate>
        <div class="flex items-start justify-between gap-3">
          <div><p class="nhan-mono">Giao diện</p><h2 id="td-ma-gd" class="hop-ma-gd-td">Nhập mã giao diện</h2></div>
          <button type="button" class="nut-dong-hop" data-dong aria-label="Đóng">×</button>
        </div>
        <p class="ghi-chu-nho mt-2">Mã do admin cấp. Đang dùng: <b data-ten-hien-tai></b>.</p>
        <input type="text" class="o-nhap mt-3" data-o-ma maxlength="60" autocomplete="off" autocapitalize="characters" spellcheck="false"
          placeholder="Nhập mã…" aria-label="Mã giao diện">
        <p class="hop-ma-gd-loi hidden" role="alert" data-loi></p>
        <div class="flex flex-wrap gap-2 mt-4">
          <button type="submit" class="btn btn-primary" data-ap-dung>Áp dụng</button>
          <button type="button" class="btn" data-mac-dinh>Về mặc định</button>
        </div>
      </form>`;
    document.body.append(hop);
    const o = hop.querySelector("[data-o-ma]"), loi = hop.querySelector("[data-loi]"), nut = hop.querySelector("[data-ap-dung]");
    const baoLoi = (t) => { loi.textContent = t; loi.classList.toggle("hidden", !t); };
    hop.querySelector("[data-dong]").addEventListener("click", () => hop.close());
    hop.addEventListener("click", (e) => { if (e.target === hop) hop.close(); }); // bấm ra ngoài → đóng
    hop.querySelector("[data-mac-dinh]").addEventListener("click", () => { ap("default", { chuyen: true }); hop.close(); });
    hop.querySelector("form").addEventListener("submit", async (e) => {
      e.preventDefault();
      if (!o.value.trim()) { baoLoi("Hãy nhập mã."); o.focus(); return; }
      nut.disabled = true;
      baoLoi("");
      try {
        const r = await goiApi("/api/theme/redeem", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ code: o.value }) });
        const d = await r.json().catch(() => ({}));
        if (!r.ok) { baoLoi(d.loi || `Lỗi ${r.status} – thử lại sau.`); return; }
        o.value = "";
        hop.close();
        ap(d.themeId, { chuyen: true, mua: true });
      } catch {
        baoLoi("Không kết nối được máy chủ – thử lại sau.");
      } finally {
        nut.disabled = false;
      }
    });
  }
  function moHop() {
    if (!hop) taoHop();
    hop.querySelector("[data-ten-hien-tai]").textContent = cauHinh(hienTai).ten;
    hop.querySelector("[data-loi]").classList.add("hidden");
    hop.showModal();
    hop.querySelector("[data-o-ma]").focus();
  }

  ap(doc(), { luuLai: false }); // áp ngay trong <head> → không nháy màu mặc định
  khiSanSang(() => document.getElementById("nut-ma-giao-dien")?.addEventListener("click", moHop));
  return { ap, moHop, get hienTai() { return hienTai; } };
})();
