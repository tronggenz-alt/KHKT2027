// Hoạt động người dùng (chỉ admin) – dùng chung cho Cài đặt → 05 Hoạt động và trang /admin.
// Gọi /admin/api/* (backend kiểm tra quyền admin ở mọi API); hiển thị nhật ký, người đang online, hộp thoại chi tiết + khoá/mở khoá,
// IP kèm vị trí / nhà mạng (bấm vào IP → mọi tài khoản từng dùng IP đó).
"use strict";

const HoatDong = (() => {
  const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

  const SU_KIEN = {
    dang_ky: ["Đăng ký", "vang"], dang_nhap: ["Đăng nhập", "xanh"], dang_nhap_sai: ["Đăng nhập sai", "do"], dang_xuat: ["Đăng xuất", ""],
    xac_thuc_email: ["Xác thực email", "xanh"], xac_thuc_sai: ["Nhập sai mã", "do"], dat_lai_mat_khau: ["Đặt lại mật khẩu", "lam"],
    doi_ten: ["Đổi tên", "lam"], khoa: ["Khoá tài khoản", "do"], mo_khoa: ["Mở khoá tài khoản", "xanh"], doi_nhan: ["Đổi nhãn thành viên", "lam"],
    gan_ma_claude: ["Nhập mã Claude", "xanh"], nhap_sai_ma_claude: ["Nhập sai mã Claude", "do"], tao_ma_claude: ["Tạo mã Claude", "lam"],
    thu_hoi_ma_claude: ["Thu hồi mã Claude", "do"], them_luot_ma_claude: ["Cộng lượt mã Claude", "lam"],
    kich_hoat_key_claude: ["Kích hoạt Key Claude", "xanh"], nhap_sai_key_claude: ["Nhập sai Key Claude", "do"],
    khoa_key_claude: ["Bị khoá nhập Key Claude", "do"], go_key_claude: ["Gỡ Key Claude", ""],
    thu_hoi_key_claude: ["Thu hồi quyền Claude", "do"], mo_lai_key_claude: ["Mở lại quyền Claude", "lam"],
    bat_key_claude_rieng: ["Bật key Claude riêng", "xanh"], tat_key_claude_rieng: ["Tắt key Claude riêng", "do"],
    yeu_cau_key_claude_rieng: ["Yêu cầu bật key Claude riêng", "vang"], tu_choi_key_claude_rieng: ["Từ chối key Claude riêng", "do"],
    email_nghi_ngo: ["Email nghi ngờ", "vang"], duyet_nghi_ngo: ["Duyệt email nghi ngờ", "lam"],
    xoa_chua_xac_minh: ["Xoá tài khoản chưa xác minh", "do"], sua_ds_email: ["Sửa danh sách tên miền email", "lam"],
    doi_giao_dien: ["Đổi giao diện bằng mã", "xanh"], nhap_sai_ma_giao_dien: ["Nhập sai mã giao diện", "do"],
    tao_ma_giao_dien: ["Tạo mã giao diện", "lam"], thu_hoi_ma_giao_dien: ["Thu hồi mã giao diện", "do"],
    doi_thu_tu_ai: ["Đổi thứ tự AI", "lam"],
    ai_rieng: ["Quản trị AI Riêng", "lam"],
    bao_sai_ai: ["Báo lời giải sai", "vang"],
  };
  const LOAI = [["", "Mọi sự kiện"], ["login", "Đăng nhập"], ["register", "Đăng ký"], ["fail", "Đăng nhập sai / sai mã"], ["logout", "Đăng xuất"],
    ["verify", "Xác thực email"], ["rename", "Đổi tên"], ["password", "Đặt lại mật khẩu"], ["admin", "Thao tác của admin"], ["claude", "Mã / Key Claude"],
    ["spam", "Email rác / nghi ngờ"], ["theme", "Mã giao diện"]];
  const NHOM = {
    login: ["dang_nhap"], register: ["dang_ky"], fail: ["dang_nhap_sai", "xac_thuc_sai"], logout: ["dang_xuat"],
    verify: ["xac_thuc_email", "xac_thuc_sai"], rename: ["doi_ten"], password: ["dat_lai_mat_khau"],
    admin: ["khoa", "mo_khoa", "doi_nhan", "tao_ma_claude", "thu_hoi_ma_claude", "them_luot_ma_claude", "thu_hoi_key_claude", "mo_lai_key_claude",
      "bat_key_claude_rieng", "tat_key_claude_rieng", "tu_choi_key_claude_rieng", "duyet_nghi_ngo", "sua_ds_email", "tao_ma_giao_dien",
      "thu_hoi_ma_giao_dien", "doi_thu_tu_ai", "ai_rieng"],
    spam: ["email_nghi_ngo", "duyet_nghi_ngo", "xoa_chua_xac_minh", "sua_ds_email"],
    theme: ["doi_giao_dien", "nhap_sai_ma_giao_dien", "tao_ma_giao_dien", "thu_hoi_ma_giao_dien"],
    claude: ["gan_ma_claude", "nhap_sai_ma_claude", "tao_ma_claude", "thu_hoi_ma_claude", "them_luot_ma_claude",
      "kich_hoat_key_claude", "nhap_sai_key_claude", "khoa_key_claude", "go_key_claude", "thu_hoi_key_claude", "mo_lai_key_claude",
      "bat_key_claude_rieng", "tat_key_claude_rieng", "yeu_cau_key_claude_rieng", "tu_choi_key_claude_rieng"],
  };
  const CO = { tai_khoan_moi: ["tài khoản mới", "vang"], sai_nhieu: ["sai nhiều lần", "do"], nhieu_tai_khoan_ip: ["nhiều TK cùng IP", "lam"],
    vi_tri_la: ["vị trí lạ", "do"] };
  const XAC_THUC = { roi: ["đã xác thực", "xanh"], mien: ["miễn (admin / kì cựu)", ""], chua: ["chưa xác thực", "vang"], "-": ["—", ""] };
  const CACH = { email: "email + mật khẩu", google: "Google", cuc_bo: "máy chủ" };

  // ---------------------------------------------------------------- gọi API
  async function lay(url) {
    const r = await fetch(url, { cache: "no-store", credentials: "same-origin" });
    const d = await r.json().catch(() => ({}));
    if (!r.ok) throw Object.assign(new Error(d.loi || `Lỗi ${r.status}`), { status: r.status });
    if (d.nguon_vi_tri_ip) { // ghi công nguồn dữ liệu vị trí IP (bắt buộc theo giấy phép)
      document.querySelectorAll("[data-nguon-ip]").forEach((el) => { el.textContent = d.nguon_vi_tri_ip; el.hidden = false; });
    }
    return d;
  }

  async function gui(url, duLieu, phuongThuc = "POST") { // phuongThuc: POST (mặc định) | PUT | DELETE
    const r = await TaiKhoan.goi(url, { method: phuongThuc, headers: { "Content-Type": "application/json" }, body: JSON.stringify(duLieu || {}) });
    const d = await r.json().catch(() => ({}));
    if (!r.ok) throw Object.assign(new Error(d.loi || d.message || `Lỗi ${r.status}`), { status: r.status, ma: d.code || d.ma, thieu: d.missing || [] });
    return d;
  }

  // ---------------------------------------------------------------- thời gian
  const hai = (n) => String(n).padStart(2, "0");
  function gio(ts) {
    if (!ts) return "—";
    const d = new Date(ts * 1000);
    const hn = new Date();
    if (d.toDateString() === hn.toDateString()) return `${hai(d.getHours())}:${hai(d.getMinutes())}:${hai(d.getSeconds())}`;
    return `${hai(d.getDate())}/${hai(d.getMonth() + 1)} ${hai(d.getHours())}:${hai(d.getMinutes())}`;
  }
  function luc(ts) {
    if (!ts) return "";
    const d = new Date(ts * 1000);
    return `${hai(d.getDate())}/${hai(d.getMonth() + 1)}/${d.getFullYear()} ${hai(d.getHours())}:${hai(d.getMinutes())}:${hai(d.getSeconds())}`;
  }
  function ngayChu(s) { // "2026-10-11 09:49:25" → "11/10/2026 09:49"
    const m = /^(\d{4})-(\d{2})-(\d{2})[ T](\d{2}):(\d{2})/.exec(s || "");
    return m ? `${m[3]}/${m[2]}/${m[1]} ${m[4]}:${m[5]}` : esc(s || "—");
  }
  const ngayCuaTs = (ts) => { const d = new Date(ts * 1000); return `${d.getFullYear()}-${hai(d.getMonth() + 1)}-${hai(d.getDate())}`; };

  // ---------------------------------------------------------------- hiển thị
  const huyHieu = (chu, lop = "") => `<span class="huy-hieu ${lop}">${esc(chu)}</span>`;
  const huyHieuSuKien = (d) => { const [t, l] = SU_KIEN[d.su_kien] || [d.ten_su_kien || d.su_kien, ""]; return huyHieu(t, l); };
  const huyHieuCo = (co) => (co || []).map((c) => (CO[c] ? huyHieu("⚑ " + CO[c][0], CO[c][1]) : "")).join("");

  // IP kèm vị trí + nhà mạng ("113.161.1.2 · Hà Nội · VNPT"); bấm → mọi tài khoản từng dùng IP đó
  function nutIp(info, ip, gon = false) { // gon: IPv6 chỉ hiện 4 nhóm đầu (bảng người dùng), IP đầy đủ ở tooltip
    if (!ip) return "";
    let chu = (info && info.mo_ta) || ip;
    if (ip === "không xác định") return `<span>IP ${esc(chu)}</span>`;
    const nhom = ip.split(":");
    if (gon && nhom.length > 4) chu = chu.replace(ip, nhom.slice(0, 4).join(":") + ":…");
    return `<button type="button" class="nut-ip" data-ip="${esc(ip)}" title="${gon ? esc(ip) + " – " : ""}Xem mọi tài khoản từng dùng IP này">IP ${esc(chu)}</button>`;
  }
  const chuIp = (info, ip) => (ip ? `IP ${(info && info.mo_ta) || ip}` : ""); // dạng chữ (tooltip)
  function viTriLa(x) {
    if (!x) return "";
    return `${x.loai === "quoc_gia" ? "quốc gia" : "tỉnh/thành"} khác thường lệ: ${x.noi} (thường: ${x.thuong_le})`;
  }

  function dongSuKien(d, moi = false) {
    const ten = d.ten || (d.email ? "" : "(không rõ)");
    const nut = d.user_id
      ? `<button type="button" class="ten-nd" data-uid="${d.user_id}" title="Xem chi tiết người dùng">${esc(ten || d.email)}</button>`
      : `<span class="ten-nd" style="cursor:default">${esc(ten)}</span>`;
    const phu = [esc(d.ten_phuong_thuc), nutIp(d.ip_info, d.ip), esc(d.thiet_bi), esc(d.chi_tiet),
      d.vi_tri_la ? `<b class="chu-do">⚑ ${esc(viTriLa(d.vi_tri_la))}</b>` : ""].filter(Boolean).join(" · ");
    const doiTuong = d.doi_tuong ? ` <button type="button" class="ten-nd doi-tuong" data-uid="${d.doi_tuong}">→ người dùng #${d.doi_tuong}</button>` : "";
    return `<div class="dong-hd${moi ? " moi" : ""}" data-id="${d.id}">
      <span class="gio" title="${esc(luc(d.ts))}">${gio(d.ts)}</span>
      <div class="min-w-0">
        <div class="dong-1">${huyHieuSuKien(d)} ${nut}${ten && d.email ? ` <span class="email">${esc(d.email)}</span>` : ""}${doiTuong} ${huyHieuCo(d.co)}</div>
        <div class="dong-2">${phu}</div>
      </div>
    </div>`;
  }

  function veDanhSach(el, ds, them = false) {
    const html = ds.map((d) => dongSuKien(d)).join("");
    const khung = el.querySelector(".ds-hd");
    if (them && khung) { khung.insertAdjacentHTML("beforeend", html); return; }
    el.innerHTML = ds.length ? `<div class="ds-hd">${html}</div>`
      : '<div class="the the-trong p-5"><p class="ghi-chu-nho">Không có hoạt động nào khớp bộ lọc.</p></div>';
  }

  function chenMoi(el, d) {
    let khung = el.querySelector(".ds-hd");
    if (!khung) { el.innerHTML = '<div class="ds-hd"></div>'; khung = el.querySelector(".ds-hd"); }
    if (khung.querySelector(`[data-id="${d.id}"]`)) return;
    khung.insertAdjacentHTML("afterbegin", dongSuKien(d, true));
  }

  function veOnline(el, ds) {
    el.innerHTML = ds.length
      ? `<div class="ds-online">${ds.map((d) => `<button type="button" class="chip-online" data-uid="${d.id}" title="${esc(`${d.email || ""} · ${chuIp(d.ip_info, d.ip) || "IP ?"} · ${d.thiet_bi}`)}">
          <span class="cham-xanh" aria-hidden="true"></span><span class="truncate">${esc(d.ten || d.email || `#${d.id}`)}</span>
          ${d.vai_tro === "admin" ? huyHieu("admin", "xanh") : d.vai_tro === "kì cựu" ? huyHieu("kì cựu", "lam") : ""}<small>${gio(d.hoat_dong_cuoi)}</small></button>`).join("")}</div>`
      : '<p class="ghi-chu-nho">Không có ai online trong 5 phút gần nhất.</p>';
  }

  // ---------------------------------------------------------------- bộ lọc: người dùng / loại sự kiện / ngày
  function taoBoLoc(el, khiDoi) {
    el.innerHTML = `<div class="bo-loc-hd">
      <input type="search" class="o-nhap" data-loc="nguoi" placeholder="Lọc theo tên hoặc email" aria-label="Lọc theo người dùng" maxlength="120">
      <select class="o-nhap" data-loc="loai" aria-label="Loại sự kiện">${LOAI.map(([v, t]) => `<option value="${v}">${esc(t)}</option>`).join("")}</select>
      <input type="date" class="o-nhap" data-loc="ngay" aria-label="Ngày">
    </div>`;
    const o = (k) => el.querySelector(`[data-loc="${k}"]`);
    let hen = null;
    const doi = () => { clearTimeout(hen); hen = setTimeout(() => khiDoi(giaTri()), 250); };
    o("nguoi").addEventListener("input", doi);
    o("loai").addEventListener("change", doi);
    o("ngay").addEventListener("change", doi);
    function giaTri() { return { nguoi: o("nguoi").value.trim(), loai: o("loai").value, ngay: o("ngay").value }; }
    function khop(d) { // sự kiện mới (luồng trực tiếp) có khớp bộ lọc đang chọn không
      const g = giaTri();
      if (g.nguoi && !`${d.email} ${d.ten}`.toLowerCase().includes(g.nguoi.toLowerCase())) return false;
      if (g.loai && !(NHOM[g.loai] || []).includes(d.su_kien)) return false;
      if (g.ngay && ngayCuaTs(d.ts) !== g.ngay) return false;
      return true;
    }
    return { giaTri, khop, chuoiTruyVan: () => new URLSearchParams(giaTri()).toString() };
  }

  // ---------------------------------------------------------------- hộp thoại chi tiết người dùng
  let hop = null;
  let khiDoiChiTiet = null;

  function damBaoHop() {
    if (hop) return hop;
    hop = document.createElement("dialog");
    hop.className = "hop-thoai";
    hop.setAttribute("aria-label", "Chi tiết người dùng");
    document.body.appendChild(hop);
    hop.addEventListener("click", (e) => {
      if (e.target === hop) { hop.close(); return; } // bấm ra ngoài để đóng
      const ip = e.target.closest("[data-ip]");
      if (ip) { moIp(ip.dataset.ip); return; }
      const n = e.target.closest("[data-uid]");
      if (n) moChiTiet(n.dataset.uid); // vd "→ người dùng #12" trong lịch sử
    });
    return hop;
  }

  function moTaKeyRieng(k) { // "Chưa nhập key" / "Đã nhập key (…abcd)" / "Key lỗi" + yêu cầu / hạn xoá
    if (!k) return "";
    const tt = huyHieu(k.ten_trang_thai, k.trang_thai === "loi" ? "do" : k.trang_thai === "da_nhap" ? "xanh" : "");
    const yc = k.yeu_cau_luc ? ` ${huyHieu("đang yêu cầu", "vang")}` : "";
    const xoa = k.xoa_sau_ngay != null ? ` <small class="ghi-chu-nho">(đã tắt – xoá key sau ${k.xoa_sau_ngay} ngày)</small>` : "";
    return tt + yc + xoa + (k.loi ? ` <small class="ghi-chu-nho" title="${esc(k.loi)}">${esc(k.loi.slice(0, 60))}</small>` : "");
  }

  async function moChiTiet(uid) {
    const h = damBaoHop();
    h.innerHTML = '<div class="than"><p class="ghi-chu-nho">Đang tải…</p></div>';
    if (!h.open) h.showModal();
    let d;
    try {
      d = await lay(`/admin/api/nguoi-dung/${encodeURIComponent(uid)}`);
    } catch (e) {
      h.innerHTML = `<div class="dau"><b>Lỗi</b><button type="button" class="btn btn-sm" data-dong>Đóng</button></div>
        <div class="than"><div class="hop-canh-bao">${esc(e.message)}</div></div>`;
      h.querySelector("[data-dong]").onclick = () => h.close();
      return;
    }
    const [xt, lxt] = XAC_THUC[d.xac_thuc] || XAC_THUC["-"];
    const coTheKhoa = !d.la_admin && d.dang_nhap_bang !== "cuc_bo";
    const dangNhap = (d.lich_su || []).filter((x) => ["dang_nhap", "dang_nhap_sai", "dang_xuat", "dang_ky", "xac_thuc_email", "xac_thuc_sai",
      "dat_lai_mat_khau", "khoa", "mo_khoa"].includes(x.su_kien));
    const bg = d.bai_giai || {};
    h.innerHTML = `
      <div class="dau">
        <div class="min-w-0">
          <p class="font-semibold text-muc" style="font-size:1.1rem">${esc(d.ten || "(chưa đặt tên)")}
            ${huyHieu(d.vai_tro, d.la_admin ? "xanh" : d.la_ki_cuu ? "lam" : "")} ${d.bi_khoa ? huyHieu("ĐÃ KHOÁ", "do") : ""} ${d.online ? huyHieu("● online", "xanh") : ""}</p>
          <p class="ghi-chu-nho" style="overflow-wrap:anywhere">#${d.id} · ${esc(d.email || "—")}</p>
        </div>
        <button type="button" class="btn btn-sm" data-dong aria-label="Đóng">✕</button>
      </div>
      <div class="than">
        <div class="luoi-tt">
          <div><span>Đăng nhập bằng</span>${esc(CACH[d.dang_nhap_bang] || d.dang_nhap_bang)}</div>
          <div><span>Email</span>${huyHieu(xt, lxt)}</div>
          <div><span>Ngày tạo</span>${ngayChu(d.tao_luc)}</div>
          <div><span>Đăng nhập cuối</span>${ngayChu(d.dang_nhap_cuoi)}</div>
          <div><span>Hoạt động cuối</span>${d.hoat_dong_cuoi ? ngayChu(d.hoat_dong_cuoi) : "—"}</div>
          <div><span>IP lúc đăng ký</span>${nutIp(d.ip_dang_ky_info, d.ip_dang_ky) || "—"}</div>
          <div><span>IP gần nhất</span>${nutIp(d.ip_cuoi_info, d.ip_cuoi) || "—"}</div>
          <div><span>Bài đã giải</span><b>${bg.xong ?? 0}</b> <small class="ghi-chu-nho">(lỗi ${bg.loi ?? 0}, tổng ${bg.tong ?? 0})</small></div>
          <div><span>Dùng thử / key riêng</span>${d.luot_da_dung} lượt · ${d.co_key ? "có key" : "chưa có key"}</div>
        </div>
        ${d.key_claude_rieng ? `<div class="mt-4 flex flex-wrap gap-3 items-center">
          <label class="cong-tac"><input type="checkbox" data-key-rieng ${d.key_claude_rieng.bat ? "checked" : ""} ${d.key_claude_rieng.la_admin ? "disabled" : ""}>
            <span class="ranh"><span class="nut-tron"></span></span><span>Key Claude riêng</span></label>
          ${moTaKeyRieng(d.key_claude_rieng)}
          ${d.key_claude_rieng.la_admin ? '<small class="ghi-chu-nho">admin luôn được dùng</small>'
            : d.key_claude_rieng.bat_luc && d.key_claude_rieng.bat ? `<small class="ghi-chu-nho">bật lúc ${esc(d.key_claude_rieng.bat_luc)}${d.key_claude_rieng.bat_boi ? " bởi " + esc(d.key_claude_rieng.bat_boi) : ""}</small>` : ""}
          ${d.key_claude_rieng.yeu_cau_luc ? `<button type="button" class="btn btn-sm btn-nguy-hiem" data-tu-choi-key>Từ chối yêu cầu</button>` : ""}
        </div>` : ""}
        ${coTheKhoa ? `<div class="mt-4 flex flex-wrap gap-2 items-center">
          <button type="button" class="btn btn-sm ${d.bi_khoa ? "" : "btn-nguy-hiem"}" data-khoa="${d.bi_khoa ? "0" : "1"}">${d.bi_khoa ? "Mở khoá tài khoản" : "Khoá tài khoản"}</button>
          <span class="ghi-chu-nho">${d.bi_khoa ? "Người này đang không đăng nhập được." : "Khoá: đăng xuất ngay và không đăng nhập lại được."}</span>
        </div>` : d.la_admin ? '<p class="ghi-chu-nho mt-4">Không khoá được tài khoản admin.</p>' : ""}
        <h4>Lịch sử đăng nhập</h4>
        <div data-ls></div>
        <h4>Lịch sử đổi tên</h4>
        ${(d.doi_ten || []).length ? `<div class="ds-hd">${d.doi_ten.map((x) => `<div class="dong-hd"><span class="gio" title="${esc(luc(x.ts))}">${gio(x.ts)}</span>
          <div class="min-w-0"><div class="dong-1"><span>${esc(x.ten_cu)}</span> → <b>${esc(x.ten_moi)}</b></div>
          <div class="dong-2">${nutIp(x.ip_info, x.ip)}</div></div></div>`).join("")}</div>`
          : '<p class="ghi-chu-nho">Chưa đổi tên lần nào.</p>'}
      </div>`;
    veDanhSach(h.querySelector("[data-ls]"), dangNhap.slice(0, 60));
    h.querySelector("[data-dong]").onclick = () => h.close();
    h.querySelectorAll(".ten-nd[data-uid]").forEach((n) => { if (Number(n.dataset.uid) === d.id) n.removeAttribute("data-uid"); });
    const doiKeyRieng = async (body) => {
      try {
        await gui(`/admin/api/nguoi-dung/${d.id}/key-claude-rieng`, body);
        await moChiTiet(d.id);
        if (khiDoiChiTiet) khiDoiChiTiet();
      } catch (e) { alert(e.message); }
    };
    const tgKey = h.querySelector("[data-key-rieng]");
    if (tgKey && !tgKey.disabled) tgKey.onchange = () => doiKeyRieng({ bat: tgKey.checked });
    const nutTuChoi = h.querySelector("[data-tu-choi-key]");
    if (nutTuChoi) nutTuChoi.onclick = () => confirm("Từ chối yêu cầu dùng key Claude riêng của người này?") && doiKeyRieng({ bat: false, tu_choi: true });
    const nutKhoa = h.querySelector("[data-khoa]");
    if (nutKhoa) {
      nutKhoa.onclick = async () => {
        const khoa = nutKhoa.dataset.khoa === "1";
        if (!confirm(`${khoa ? "KHOÁ" : "MỞ KHOÁ"} tài khoản ${d.ten || ""} <${d.email || "#" + d.id}>?`)) return;
        nutKhoa.disabled = true;
        try {
          await gui(`/admin/api/nguoi-dung/${d.id}/khoa`, { khoa });
          await moChiTiet(d.id);
          if (khiDoiChiTiet) khiDoiChiTiet();
        } catch (e) {
          alert(e.message);
          nutKhoa.disabled = false;
        }
      };
    }
  }

  // ---------------------------------------------------------------- hộp thoại theo IP: mọi tài khoản từng dùng IP đó
  async function moIp(ip) {
    const h = damBaoHop();
    h.innerHTML = '<div class="than"><p class="ghi-chu-nho">Đang tải…</p></div>';
    if (!h.open) h.showModal();
    let d;
    try {
      d = await lay(`/admin/api/ip?ip=${encodeURIComponent(ip)}`);
    } catch (e) {
      h.innerHTML = `<div class="dau"><b>Lỗi</b><button type="button" class="btn btn-sm" data-dong>Đóng</button></div>
        <div class="than"><div class="hop-canh-bao">${esc(e.message)}</div></div>`;
      h.querySelector("[data-dong]").onclick = () => h.close();
      return;
    }
    const tt = d.ip_info || {};
    const tk = d.tai_khoan || [];
    const sai = d.dang_nhap_sai || [];
    h.innerHTML = `
      <div class="dau">
        <div class="min-w-0">
          <p class="font-semibold text-muc font-mono" style="font-size:1.05rem;overflow-wrap:anywhere">IP ${esc(d.ip)}</p>
          <p class="ghi-chu-nho" style="overflow-wrap:anywhere">${esc([tt.vi_tri, tt.nha_mang].filter(Boolean).join(" · ") ||
            (tt.cu ? "ghi trước khi sửa cách lấy IP (thực ra là máy chạy app)" : tt.noi_bo ? "mạng nội bộ" : "chưa rõ vị trí"))}</p>
        </div>
        <button type="button" class="btn btn-sm" data-dong aria-label="Đóng">✕</button>
      </div>
      <div class="than">
        <h4>Tài khoản từng dùng IP này (${tk.length})</h4>
        ${tk.length ? `<div class="ds-hd">${tk.map((x) => `<div class="dong-hd"><span class="gio">#${x.id}</span><div class="min-w-0">
          <div class="dong-1"><button type="button" class="ten-nd" data-uid="${x.id}">${esc(x.ten || x.email || "#" + x.id)}</button>
            ${x.ten && x.email ? `<span class="email">${esc(x.email)}</span>` : ""}
            ${huyHieu(x.vai_tro, x.la_admin ? "xanh" : x.la_ki_cuu ? "lam" : "")} ${x.bi_khoa ? huyHieu("đã khoá", "do") : ""}</div>
          <div class="dong-2">${esc([x.so_lan_dang_nhap ? `đăng nhập ${x.so_lan_dang_nhap} lần` : "", x.ghi_chu,
            x.lan_dau && x.lan_dau !== x.lan_cuoi ? `lần đầu ${ngayChu(x.lan_dau)}` : "",
            x.lan_cuoi ? `lần cuối ${ngayChu(x.lan_cuoi)}` : ""].filter(Boolean).join(" · "))}</div></div></div>`).join("")}</div>`
          : '<p class="ghi-chu-nho">Chưa có tài khoản nào đăng nhập từ IP này.</p>'}
        ${sai.length ? `<h4>Đăng nhập sai từ IP này</h4><div class="ds-hd">${sai.map((x) => `<div class="dong-hd"><span class="gio">${x.so_lan}×</span>
          <div class="min-w-0"><div class="dong-1"><span class="email">${esc(x.email || "(trống)")}</span></div>
          <div class="dong-2">lần cuối ${ngayChu(x.lan_cuoi)}</div></div></div>`).join("")}</div>` : ""}
        <h4>Hoạt động gần đây của IP</h4>
        <div data-ls></div>
        <p class="ghi-chu-nho mt-3" data-nguon-ip ${d.nguon_vi_tri_ip ? "" : "hidden"}>${esc(d.nguon_vi_tri_ip || "")}</p>
      </div>`;
    veDanhSach(h.querySelector("[data-ls]"), d.gan_day || []);
    h.querySelectorAll(`.nut-ip[data-ip="${CSS.escape(d.ip)}"]`).forEach((n) => n.removeAttribute("data-ip")); // đang xem IP này rồi
    h.querySelector("[data-dong]").onclick = () => h.close();
  }

  // bấm vào tên / chip online / dòng bảng có data-uid → mở chi tiết; bấm IP → các tài khoản dùng IP đó
  function ganMoChiTiet(goc, khiDoi) {
    khiDoiChiTiet = khiDoi || khiDoiChiTiet;
    goc.addEventListener("click", (e) => {
      const ip = e.target.closest("[data-ip]");
      if (ip && goc.contains(ip) && !(hop && hop.contains(ip))) { e.stopPropagation(); moIp(ip.dataset.ip); return; }
      const n = e.target.closest("[data-uid]");
      if (n && goc.contains(n) && !(hop && hop.contains(n))) moChiTiet(n.dataset.uid); // trong hộp thoại: hộp thoại tự xử lý
    });
  }

  async function danhDauDaXem(id) {
    try {
      await gui("/admin/api/da-xem", { id: id || 0 });
      if (TaiKhoan.toi) TaiKhoan.toi.hoat_dong_moi = 0;
      document.querySelectorAll(".cham-thong-bao").forEach((e) => e.remove());
    } catch { /* bỏ qua */ }
  }

  return { esc, SU_KIEN, LOAI, NHOM, CO, XAC_THUC, CACH, lay, gui, gio, luc, ngayChu, huyHieu, nutIp, chuIp, viTriLa, dongSuKien, veDanhSach,
    chenMoi, veOnline, taoBoLoc, moChiTiet, moIp, ganMoChiTiet, danhDauDaXem, moTaKeyRieng };
})();
