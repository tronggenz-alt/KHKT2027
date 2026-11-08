// Dùng chung cho mọi trang sau đăng nhập: hết phiên thì về trang đăng nhập, nút đăng xuất ([data-dang-xuat]), tên người dùng ([data-ten-nguoi]),
// thanh bên và thanh dưới điện thoại, hộp tìm kiếm. Khung giáo viên, học sinh còn dùng thêm tab theo địa chỉ # và thẻ tài khoản.
const Phien = (() => {
  // Trang mới: thêm đường dẫn vào TRANG_CO_KHUNG và một dòng vào MENU; trong trang chỉ cần <header class="thanh"> như truc_tiep.html.
  const TRANG_CO_KHUNG = { "/giao-vien": "giao_vien", "/hoc-sinh": "hoc_sinh", "/truc-tiep": "giao_vien", "/trinh-chieu": "giao_vien", "/buoi-hoc": "giao_vien" };
  const MENU = {
    giao_vien: [
      ["", [["nha", "Hôm nay", "/giao-vien#tong-quan"], ["lop", "Lớp học", "/giao-vien#lop"], ["cham", "Chấm bài", "/giao-vien#ket-qua"]]],
      ["Buổi học", [["truc", "Theo dõi lớp", "/truc-tiep"], ["dieu", "Điều khiển", "/buoi-hoc"], ["chieu", "Trình chiếu", "/trinh-chieu"]]],
      ["Phòng thí nghiệm", [["hoa", "Phòng Hóa", "/hoa"], ["sinh", "Phòng Sinh", "/sinh"], ["sach", "Hướng dẫn máy", "/hoa#may"], ["ai", "Trợ lý AI", "/hoa#khoa"]]],
      ["Sắp có", [["han", "Hạn mức AI"]]],
    ],
    hoc_sinh: [
      ["", [["nha", "Hôm nay", "/hoc-sinh#lop"], ["cham", "Kết quả của tôi", "/hoc-sinh#ket-qua"], ["sinh", "Bài thí nghiệm", "/hoc-sinh#bai"], ["lich", "Lịch phòng", "/hoc-sinh#lich"]]],
      ["Tra cứu", [["sach", "Hướng dẫn máy", "/hoc-sinh#may"], ["tu", "Hướng dẫn dụng cụ", "/hoc-sinh#dung-cu"]]],
    ],
  };
  // Bốn mục đầu của thanh dưới điện thoại; mục thứ năm là "Thêm", mở toàn bộ menu
  const MENU_DUOI = {
    giao_vien: [["nha", "Hôm nay", "/giao-vien#tong-quan"], ["lop", "Lớp học", "/giao-vien#lop"], ["truc", "Theo dõi", "/truc-tiep"], ["dieu", "Điều khiển", "/buoi-hoc"]],
    hoc_sinh: [["nha", "Hôm nay", "/hoc-sinh#lop"], ["sinh", "Bài", "/hoc-sinh#bai"], ["cham", "Kết quả", "/hoc-sinh#ket-qua"], ["lich", "Lịch", "/hoc-sinh#lich"]],
  };
  const DUONG = {
    nha: '<path d="M3 11.5 12 4l9 7.5"/><path d="M5.5 10v9.5H10v-6h4v6h4.5V10"/>',
    lop: '<circle cx="9" cy="8" r="3.4"/><path d="M2.8 20c.5-3.6 3-5.6 6.2-5.6s5.7 2 6.2 5.6"/><path d="M16 4.9a3.4 3.4 0 0 1 0 6.3"/><path d="M18 14.9c1.8.8 3 2.5 3.3 5.1"/>',
    cham: '<rect x="4" y="4" width="16" height="16" rx="1.5"/><path d="m8 12.3 2.8 2.8L16 9.6"/>',
    hoa: '<path d="M9.5 3.5h5"/><path d="M10.5 3.5v6.2L5 18.6a1.3 1.3 0 0 0 1.1 1.9h11.8a1.3 1.3 0 0 0 1.1-1.9l-5.5-8.9V3.5"/><path d="M7.8 15h8.4"/>',
    sinh: '<path d="M7 3c0 5 10 5 10 10s-10 3-10 8"/><path d="M17 3c0 5-10 5-10 10s10 3 10 8"/><path d="M8.8 6.3h6.4"/><path d="M8.8 17.7h6.4"/>',
    sach: '<path d="M12 6.5C10 5 7.5 4.5 4 4.5v14c3.5 0 6 .5 8 2 2-1.5 4.5-2 8-2v-14c-3.5 0-6 .5-8 2z"/><path d="M12 6.5v14"/>',
    ai: '<path d="M12 3.5l1.9 5.1 5.1 1.9-5.1 1.9-1.9 5.1-1.9-5.1-5.1-1.9 5.1-1.9z"/><path d="M19 16v4"/><path d="M17 18h4"/>',
    truc: '<circle cx="12" cy="12" r="2"/><path d="M7.8 16.2a6 6 0 0 1 0-8.4"/><path d="M16.2 7.8a6 6 0 0 1 0 8.4"/><path d="M4.9 19.1a10 10 0 0 1 0-14.2"/><path d="M19.1 4.9a10 10 0 0 1 0 14.2"/>',
    dieu: '<path d="M5 4v6"/><path d="M5 14v6"/><path d="M12 4v3"/><path d="M12 11v9"/><path d="M19 4v9"/><path d="M19 17v3"/><circle cx="5" cy="12" r="2"/><circle cx="12" cy="9" r="2"/><circle cx="19" cy="15" r="2"/>',
    chieu: '<rect x="3" y="4" width="18" height="12.5" rx="1.5"/><path d="M12 16.5v4"/><path d="M8 20.5h8"/><path d="M10 8l4.5 2.3L10 12.6z"/>',
    han: '<path d="M4 18a9 9 0 1 1 16 0"/><path d="M12 14l3.5-4.5"/><circle cx="12" cy="14.5" r="1.4"/>',
    nguoi: '<circle cx="12" cy="8" r="3.8"/><path d="M4.5 20.5c.7-4 3.8-6.2 7.5-6.2s6.8 2.2 7.5 6.2"/>',
    tim: '<circle cx="11" cy="11" r="6.5"/><path d="m20 20-4.2-4.2"/>',
    lich: '<rect x="4" y="5" width="16" height="15.5" rx="1.5"/><path d="M4 10h16"/><path d="M8.5 3v4"/><path d="M15.5 3v4"/>',
    them: '<path d="M4 7h16"/><path d="M4 12h16"/><path d="M4 17h16"/>',
    dong: '<path d="m6 6 12 12"/><path d="M18 6 6 18"/>',
    binh: '<path d="M9 3.5h6"/><path d="M10.2 3.5v5.7L6.8 15a4.6 4.6 0 0 0 4 6.9h2.4a4.6 4.6 0 0 0 4-6.9L13.8 9.2V3.5"/>',
    song: '<path d="M2.5 12h4L9 5.5l4.5 13 2.8-6.5h5.2"/>',
    tu: '<rect x="5.5" y="3" width="13" height="18" rx="1.5"/><path d="M5.5 10.5h13"/><path d="M8.5 6v2"/><path d="M8.5 13v3"/>',
    tam_giac: '<path d="M12 4 2.8 19.5h18.4z"/><path d="M12 10v4.5"/><path d="M12 17.2v.1"/>',
    tep: '<path d="M6 3.5h8.5L19 8v12.5H6z"/><path d="M14 3.5V8.5h5"/><path d="m9.2 14 2 2 3.8-3.8"/>',
    robot: '<rect x="5" y="8" width="14" height="11" rx="2.5"/><path d="M12 8V4.5"/><circle cx="9.2" cy="13" r="1.1"/><circle cx="14.8" cy="13" r="1.1"/>',
    but: '<path d="M4 20l1-4.5L16.2 4.3a1.8 1.8 0 0 1 2.5 0l1 1a1.8 1.8 0 0 1 0 2.5L8.5 19z"/>',
    dung: '<path d="m5 12.5 4.5 4.5L19 7.5"/>',
  };
  const icon = (t) => `<svg class="ic" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${DUONG[t] || ""}</svg>`;
  const LOGO = '<svg viewBox="0 0 40 40" aria-hidden="true"><path d="M20 2.5 35.2 11.2v17.6L20 37.5 4.8 28.8V11.2z" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linejoin="round"/><path d="M12.5 13 20 28l7.5-15" fill="none" stroke="currentColor" stroke-width="3.6" stroke-linecap="round" stroke-linejoin="round"/></svg>';

  const fetchGoc = window.fetch.bind(window);

  window.fetch = async (...thamSo) => {
    const r = await fetchGoc(...thamSo);
    if (r.status === 401 && r.url.startsWith(location.origin + "/")) {
      location.replace("/dang-nhap?tiep=" + encodeURIComponent(location.pathname + location.hash));
    }
    return r;
  };

  // Bấm Quay lại sau khi đăng xuất: không hiện lại bản trang trình duyệt còn giữ
  addEventListener("pageshow", (e) => { if (e.persisted) location.reload(); });

  const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

  async function json(duong, tuyChon) {
    const r = await fetch(duong, tuyChon);
    const data = await r.json().catch(() => ({}));
    if (!r.ok) throw new Error(typeof data.detail === "string" ? data.detail : "Không tải được dữ liệu");
    return data;
  }

  // Gửi dữ liệu JSON: POST, PATCH, DELETE
  const gui = (duong, phuongThuc, body) => json(duong, { method: phuongThuc, headers: { "Content-Type": "application/json" }, body: JSON.stringify(body ?? {}) });

  // Lỗi mất kết nối của trình duyệt thành câu tiếng Việt
  const loiChu = (loi) => (loi.message === "Failed to fetch" ? "Không kết nối được máy chủ" : loi.message);

  const gio = (t) => new Date(t * 1000).toLocaleString("vi-VN", { hour: "2-digit", minute: "2-digit", day: "2-digit", month: "2-digit", year: "numeric" });

  const san = json("/auth/toi");

  async function dangXuat() {
    try {
      await fetchGoc("/auth/dang-xuat", { method: "POST" });
      location.replace("/dang-nhap");
    } catch {
      alert("Không kết nối được máy chủ nên chưa đăng xuất được. Hãy thử lại.");
    }
  }

  // Ở trang cùng đường dẫn thì dùng # để tab đổi ngay; còn lại là liên kết thường
  function lienKet(ic, ten, den) {
    const dd = location.pathname;
    const noi = den.startsWith(dd + "#") ? den.slice(dd.length) : den;
    return `<a href="${noi}" ${den === dd ? 'aria-current="page"' : ""}>${icon(ic)}<span>${ten}</span></a>`;
  }

  let daDungKhung = false;
  function dungKhung() {
    const vai = TRANG_CO_KHUNG[location.pathname];
    if (daDungKhung || !vai || !document.body) return;
    daDungKhung = true;
    document.body.classList.add("co-khung");
    const trangChu = vai === "giao_vien" ? "/giao-vien" : "/hoc-sinh";
    const ben = document.createElement("aside");
    ben.className = "ben";
    ben.id = "ben";
    ben.setAttribute("aria-label", "Điều hướng");
    ben.innerHTML = `
      <div class="ben-dau"><a class="nhan-hieu" href="${trangChu}">${LOGO}VERA Lab</a><button type="button" class="nut phu nho ben-dong" aria-label="Đóng menu">${icon("dong")}</button></div>
      <nav class="menu" aria-label="Các mục">${MENU[vai].map(([nhom, ds]) => `${nhom ? `<h4>${nhom}</h4>` : ""}${ds.map(([ic, ten, den]) => (den ? lienKet(ic, ten, den) : `<div class="sap">${icon(ic)}<span>${ten}</span><small>sắp có</small></div>`)).join("")}`).join("")}</nav>
      <div class="ben-cuoi menu">${lienKet("nguoi", "Tài khoản", trangChu + "#tai-khoan")}<button type="button" class="nut phu nho" data-dang-xuat>Đăng xuất</button></div>`;
    const duoi = document.createElement("nav");
    duoi.className = "duoi menu";
    duoi.setAttribute("aria-label", "Điều hướng nhanh");
    duoi.innerHTML = MENU_DUOI[vai].map(([ic, ten, den]) => lienKet(ic, ten, den)).join("") + `<button type="button" aria-expanded="false" aria-controls="ben">${icon("them")}<span>Thêm</span></button>`;
    const nen = document.createElement("div");
    nen.className = "ben-nen";
    document.body.prepend(ben, nen);
    document.body.append(duoi);

    const nutThem = duoi.querySelector("button");
    const doiMenu = (mo) => { ben.classList.toggle("mo", mo); nen.classList.toggle("mo", mo); nutThem.setAttribute("aria-expanded", String(mo)); };
    nutThem.addEventListener("click", () => doiMenu(!ben.classList.contains("mo")));
    nen.addEventListener("click", () => doiMenu(false));
    ben.querySelector(".ben-dong").addEventListener("click", () => doiMenu(false));
    ben.addEventListener("click", (e) => { if (e.target.closest("a")) doiMenu(false); });
    addEventListener("keydown", (e) => { if (e.key === "Escape") doiMenu(false); });

    const thanh = document.querySelector(".thanh .khung");
    if (thanh) {
      const nutTim = document.createElement("button");
      nutTim.type = "button";
      nutTim.className = "nut-tim";
      nutTim.innerHTML = `${icon("tim")}<span>Tìm hoặc hỏi AI</span><kbd>Ctrl K</kbd>`;
      nutTim.addEventListener("click", moTim);
      thanh.insertBefore(nutTim, thanh.querySelector(".nguoi"));
    }
    addEventListener("keydown", (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") { e.preventDefault(); moTim(); }
    });
  }

  // Tải tim.js ở lần mở đầu; tim.js tự mở hộp khi tải xong
  function moTim() {
    if (window.TimKiem) return window.TimKiem.mo();
    if (document.getElementById("tep-tim")) return;
    const s = document.createElement("script");
    s.id = "tep-tim";
    s.src = "/tinh/tim.js";
    document.head.append(s);
  }

  addEventListener("DOMContentLoaded", () => {
    dungKhung();
    san.then((nd) => {
      for (const o of document.querySelectorAll("[data-ten-nguoi]")) o.textContent = nd.ho_ten;
    }).catch(() => {});
    for (const nut of document.querySelectorAll("[data-dang-xuat]")) nut.addEventListener("click", dangXuat);
  });

  // napTab: { "ma": async (khung) => ... }, mỗi tab là một <section class="tab" id="tab-ma"> mở bằng địa chỉ #ma, tải dữ liệu ở lần mở đầu tiên.
  // id khác địa chỉ # để trình duyệt không tự cuộn, làm tiêu đề bị thanh trên cùng che. Tab trong luonNap tải lại mỗi lần mở.
  function ganTab(napTab, luonNap = []) {
    dungKhung();
    const daNap = new Set();
    const mo = () => {
      const hash = location.hash.slice(1);
      const ma = Object.hasOwn(napTab, hash) ? hash : Object.keys(napTab)[0];
      for (const ten of Object.keys(napTab)) document.getElementById("tab-" + ten).classList.toggle("dang-mo", ten === ma);
      scrollTo(0, 0);
      for (const a of document.querySelectorAll(".menu a[href^='#']")) {
        if (a.getAttribute("href") === "#" + ma) {
          a.setAttribute("aria-current", "page");
        } else {
          a.removeAttribute("aria-current");
        }
      }
      if (daNap.has(ma) && !luonNap.includes(ma)) return;
      daNap.add(ma);
      const khung = document.getElementById("tab-" + ma);
      Promise.resolve().then(() => napTab[ma](khung)).catch((loi) => {
        daNap.delete(ma);
        const bao = document.createElement("p");
        bao.className = "bao";
        bao.textContent = loiChu(loi);
        khung.append(bao);
      });
    };
    addEventListener("hashchange", mo);
    mo();
  }

  const VAI_TRO = { giao_vien: "Giáo viên", hoc_sinh: "Học sinh" };

  async function theTaiKhoan(khung) {
    const nd = await san;
    khung.innerHTML = `
      <h1>Tài khoản</h1>
      <section class="the">
        <dl class="bo-truong">
          <dt>Họ tên</dt><dd>${esc(nd.ho_ten)}</dd>
          <dt>Tên đăng nhập</dt><dd>${esc(nd.ten_dang_nhap)}</dd>
          <dt>Vai trò</dt><dd>${esc(VAI_TRO[nd.vai_tro])}</dd>
        </dl>
      </section>
      <section class="the">
        <h2>Đổi mật khẩu</h2>
        <form novalidate style="max-width: 420px">
          <input type="text" autocomplete="username" value="${esc(nd.ten_dang_nhap)}" hidden>
          <label class="o" style="margin-top: 0"><span>Mật khẩu hiện tại</span><input name="mat_khau_cu" type="password" autocomplete="current-password"></label>
          <label class="o"><span>Mật khẩu mới</span><input name="mat_khau_moi" type="password" autocomplete="new-password"><small>Ít nhất 8 ký tự. Đổi xong, các máy khác đang đăng nhập tài khoản này sẽ phải đăng nhập lại.</small></label>
          <label class="o"><span>Nhập lại mật khẩu mới</span><input name="nhap_lai" type="password" autocomplete="new-password"></label>
          <p class="bao" role="status" hidden></p>
          <button class="nut chinh" style="margin-top: 18px">Lưu mật khẩu mới</button>
        </form>
      </section>`;
    const form = khung.querySelector("form"), bao = form.querySelector(".bao"), nut = form.querySelector("button");
    const baoTin = (chu, xong) => { bao.textContent = chu; bao.classList.toggle("xong", xong); bao.hidden = false; };
    form.addEventListener("submit", async (e) => {
      e.preventDefault();
      const g = Object.fromEntries(new FormData(form));
      if (!g.mat_khau_cu || !g.mat_khau_moi) return baoTin("Hãy điền đủ các ô", false);
      if (g.mat_khau_moi !== g.nhap_lai) return baoTin("Hai lần nhập mật khẩu mới không khớp", false);
      nut.disabled = true;
      try {
        await gui("/auth/doi-mat-khau", "POST", { mat_khau_cu: g.mat_khau_cu, mat_khau_moi: g.mat_khau_moi });
        form.reset();
        baoTin("Đã đổi mật khẩu", true);
      } catch (loi) {
        baoTin(loiChu(loi), false);
      }
      nut.disabled = false;
    });
  }

  return { esc, json, gui, loiChu, gio, san, ganTab, theTaiKhoan, icon, LOGO };
})();
