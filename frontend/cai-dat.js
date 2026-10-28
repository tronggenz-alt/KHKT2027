// Trang Cài đặt: tài khoản, key Gemini riêng (kiểm tra / lưu / xoá – không bao giờ nhận lại key), hướng dẫn, lịch sử bài giải.
"use strict";

const $ = (s) => document.querySelector(s);

function esc(s) {
  return String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

function toast(noiDung, loai = "tin", thoiGian = 6000) {
  const el = document.createElement("div");
  el.className = `toast ${loai}`;
  el.innerHTML = noiDung;
  $("#toasts").appendChild(el);
  setTimeout(() => el.remove(), thoiGian);
}

// ------------------------------------------------------------ 01 tài khoản
function veTaiKhoan(t) {
  const khu = $("#the-tai-khoan");
  const dt = t.dung_thu;
  if (!t.dang_nhap) {
    khu.innerHTML = `<p class="text-muc">Bạn chưa đăng nhập.</p>
      <p class="ghi-chu-nho mt-1">Đăng nhập (email + mật khẩu${t.dang_nhap_google ? " hoặc Google" : ""}) để giải bài – dùng thử ${dt.gioi_han} bài,
        lưu key Gemini riêng và xem lịch sử.</p>
      <div class="mt-4">${TaiKhoan.nutDangNhap("Đăng nhập / Tạo tài khoản", "btn")}</div>`;
    return;
  }
  const nd = t.nguoi_dung;
  const huyHieu = nd.cuc_bo ? '<span class="huy-hieu vang">chế độ cục bộ</span>'
    : (t.la_chu_app ? '<span class="huy-hieu xanh">admin</span> ' : t.la_ki_cuu ? '<span class="huy-hieu lam">kì cựu</span> '
      : `<span class="huy-hieu">${esc(t.nhan_thanh_vien || "thành viên")}</span> `)
      + `<span class="huy-hieu">${nd.dang_nhap_bang === "google" ? "Google" : "email + mật khẩu"}</span>`;
  // hàng nút: Đổi tên · Đổi mật khẩu (tài khoản email) · Nhãn thành viên (admin)
  const nut = [];
  if (!nd.cuc_bo) nut.push(`
    <details class="doi-mat-khau">
      <summary class="btn btn-sm">Đổi tên</summary>
      <form id="form-doi-ten" class="mt-4 grid gap-3 max-w-md" autocomplete="off" novalidate>
        <label for="ten-moi" class="sr-only">Tên hiển thị mới</label>
        <input id="ten-moi" class="o-nhap" maxlength="30" value="${esc(nd.ten)}" autocomplete="nickname" placeholder="Tên hiển thị mới">
        <p class="ghi-chu-nho">2–30 ký tự, dùng được tiếng Việt có dấu. Đổi tối đa 3 lần mỗi ngày.</p>
        <div><button type="submit" class="btn btn-primary btn-sm">Lưu tên mới</button></div>
        <p id="kq-doi-ten" class="ghi-chu-nho" aria-live="polite"></p>
      </form>
    </details>`);
  if (nd.dang_nhap_bang === "email") nut.push(`
    <details class="doi-mat-khau">
      <summary class="btn btn-sm">Đổi mật khẩu</summary>
      <form id="form-doi-mk" class="mt-4 grid gap-3 max-w-md" autocomplete="off" novalidate>
        <input id="mk-cu" type="password" class="o-nhap" placeholder="Mật khẩu hiện tại" autocomplete="current-password" maxlength="200">
        <input id="mk-moi" type="password" class="o-nhap" placeholder="Mật khẩu mới (≥ 8 ký tự)" autocomplete="new-password" maxlength="200">
        <input id="mk-moi-2" type="password" class="o-nhap" placeholder="Nhập lại mật khẩu mới" autocomplete="new-password" maxlength="200">
        <div><button type="submit" class="btn btn-primary btn-sm">Lưu mật khẩu mới</button></div>
        <p id="kq-doi-mk" class="ghi-chu-nho" aria-live="polite"></p>
      </form>
    </details>`);
  if (t.la_chu_app) nut.push(`
    <details class="doi-mat-khau">
      <summary class="btn btn-sm">Nhãn thành viên</summary>
      <form id="form-nhan" class="mt-4 grid gap-3 max-w-md" autocomplete="off" novalidate>
        <label for="o-nhan" class="ghi-chu-nho">Chữ trên nhãn hiện cạnh tên mọi người dùng không phải admin
          (hiện tại: <span class="huy-hieu">${esc(t.nhan_thanh_vien || "thành viên")}</span>)</label>
        <input id="o-nhan" class="o-nhap" maxlength="24" value="${esc(t.nhan_thanh_vien || "thành viên")}">
        <div><button type="submit" class="btn btn-primary btn-sm">Lưu nhãn</button></div>
        <p id="kq-nhan" class="ghi-chu-nho" aria-live="polite"></p>
      </form>
    </details>`);
  const dungThu = t.key.co ? "Đang dùng key Gemini của bạn – không giới hạn bởi app."
    : t.la_chu_app ? "Admin: giải bằng key của chủ app (.env), không giới hạn."
    : t.la_ki_cuu ? "Kì cựu: giải bằng key của chủ app, không giới hạn."
    : `Dùng thử bằng key của chủ app: đã dùng ${dt.da_dung}/${dt.gioi_han} bài, còn <b>${dt.con_lai}</b>.`;
  khu.innerHTML = `
    <div class="flex flex-wrap items-center gap-4">
      ${TaiKhoan.anhDaiDien(nd, true)}
      <div class="min-w-0 flex-1">
        <p class="font-semibold text-muc" style="overflow-wrap:anywhere">${esc(nd.cuc_bo ? "Chủ app (máy này)" : nd.ten || nd.email)} ${huyHieu}</p>
        ${nd.email ? `<p class="ghi-chu-nho truncate">${esc(nd.email)}</p>` : ""}
        <p class="ghi-chu-nho mt-1">${dungThu}</p>
      </div>
      ${nd.cuc_bo ? "" : '<button type="button" id="nut-dang-xuat" class="btn btn-sm">Đăng xuất</button>'}
    </div>
    ${nd.cuc_bo ? `<p class="ghi-chu-nho mt-4">Bạn đang mở app trên chính máy chạy app nên được coi là chủ app, không cần đăng nhập.
      Người khác mở qua Internet sẽ gặp trang Đăng nhập.</p>` : ""}
    ${nut.length ? `<div class="hang-nut-tk mt-5">${nut.join("")}</div>` : ""}`;
  $("#form-doi-mk")?.addEventListener("submit", doiMatKhau);
  $("#form-doi-ten")?.addEventListener("submit", doiTen);
  $("#form-nhan")?.addEventListener("submit", doiNhan);
  $("#nut-dang-xuat")?.addEventListener("click", () => {
    // đăng xuất bằng form POST thường, kèm mã CSRF trong ô ẩn
    $("#csrf-dang-xuat").value = t.csrf;
    $("#form-dang-xuat").submit();
  });
}

async function doiMatKhau(e) {
  e.preventDefault();
  const kq = $("#kq-doi-mk");
  const moi = $("#mk-moi").value;
  if (moi.length < 8) { kq.textContent = "Mật khẩu mới cần ít nhất 8 ký tự."; return; }
  if (moi !== $("#mk-moi-2").value) { kq.textContent = "Hai mật khẩu mới chưa khớp."; return; }
  try {
    await guiJson("/auth/doi-mat-khau", "POST", { mat_khau_cu: $("#mk-cu").value, mat_khau_moi: moi });
    e.target.reset();
    kq.textContent = "Đã đổi mật khẩu.";
    toast("Đã đổi mật khẩu.", "ok", 4000);
  } catch (err) {
    kq.textContent = err.message;
  }
}

async function doiTen(e) {
  e.preventDefault();
  const kq = $("#kq-doi-ten");
  const ten = $("#ten-moi").value.replace(/\s+/g, " ").trim();
  if (ten.length < 2 || ten.length > 30) { kq.textContent = "Tên cần từ 2 đến 30 ký tự."; return; }
  const nut = e.target.querySelector("button[type=submit]");
  nut.disabled = true;
  try {
    const d = await guiJson("/api/doi-ten", "POST", { ten });
    toast(`Đã đổi tên thành <b>${esc(d.ten)}</b>${d.con_lai_hom_nay >= 0 ? ` (hôm nay còn đổi được ${d.con_lai_hom_nay} lần)` : ""}.`, "ok", 5000);
    await TaiKhoan.tai(); // cập nhật ngay tên ở thanh trên + thẻ tài khoản, không tải lại trang
  } catch (err) {
    kq.textContent = err.message;
    nut.disabled = false;
  }
}

async function doiNhan(e) {
  e.preventDefault();
  const kq = $("#kq-nhan");
  const nhan = $("#o-nhan").value.trim();
  if (!nhan) { kq.textContent = "Nhãn không được để trống."; return; }
  try {
    const d = await guiJson("/admin/api/nhan-thanh-vien", "POST", { nhan });
    toast(`Đã đổi nhãn thành viên thành <span class="huy-hieu">${esc(d.nhan)}</span>.`, "ok", 4000);
    await TaiKhoan.tai();
  } catch (err) {
    kq.textContent = err.message;
  }
}

// ------------------------------------------------------------ 02 key Gemini
function veKey(t) {
  const hh = $("#huy-hieu-key");
  const coTheLuu = t.dang_nhap && t.ma_hoa_san_sang;
  if (t.key.co) {
    hh.className = "huy-hieu xanh";
    hh.textContent = `Đã lưu · …${t.key.cuoi}`;
    $("#mo-ta-key").textContent = "Mọi bài giải đang dùng key này. Dán key khác vào ô rồi bấm “Lưu key” để thay.";
  } else {
    hh.className = "huy-hieu" + (t.nguon_key_giai === "het_luot" ? " do" : "");
    hh.textContent = "Chưa có key";
    $("#mo-ta-key").textContent = !t.dang_nhap ? "Đăng nhập để lưu key của bạn."
      : t.la_chu_app ? "Không bắt buộc với chủ app (đang dùng key trong .env)."
      : t.la_ki_cuu ? "Không bắt buộc với tài khoản kì cựu (đang dùng key của chủ app)."
      : t.nguon_key_giai === "het_luot" ? "Bạn đã hết lượt dùng thử – thêm key để giải tiếp."
      : `Đang dùng thử bằng key của chủ app (còn ${t.dung_thu.con_lai} bài).`;
  }
  $("#canh-bao-khoa-chinh").classList.toggle("hidden", !t.dang_nhap || t.ma_hoa_san_sang);
  $("#nut-xoa-key").classList.toggle("hidden", !t.key.co);
  $("#nut-luu-key").disabled = !coTheLuu;
  $("#nut-kiem-tra-key").disabled = !t.dang_nhap;
  $("#o-key").disabled = !t.dang_nhap;
  $("#o-key").placeholder = t.dang_nhap ? "AIza…" : "Đăng nhập để nhập key";
}

function hienKetQuaKiemTra(kq) {
  const box = $("#ket-qua-kiem-tra");
  const lop = kq.hop_le === true && !kq.het_han_muc ? "ok" : kq.hop_le === false ? "loi" : "chua-ro";
  const dau = lop === "ok" ? "✓" : lop === "loi" ? "✕" : "!";
  box.className = `ket-qua-kiem-tra mt-4 ${lop}`;
  box.innerHTML = `<b aria-hidden="true">${dau}</b><span>${esc(kq.thong_diep || "")}${kq.cuoi ? ` <span class="font-mono">(…${esc(kq.cuoi)})</span>` : ""}</span>`;
}

async function bamNut(nut, chu, viec) {
  const nhan = nut.querySelector(".nhan") || nut;
  const cu = nhan.textContent;
  nut.disabled = true;
  nhan.textContent = chu;
  try { await viec(); } finally { nut.disabled = false; nhan.textContent = cu; veKey(TaiKhoan.toi); }
}

async function guiJson(url, method, duLieu) {
  const r = await TaiKhoan.goi(url, {
    method, headers: { "Content-Type": "application/json" }, body: duLieu ? JSON.stringify(duLieu) : undefined,
  });
  const d = await r.json().catch(() => ({}));
  if (!r.ok) throw new Error(d.loi || (Array.isArray(d.detail) ? "Dữ liệu gửi lên không hợp lệ." : "Có lỗi, hãy thử lại."));
  return d;
}

function khoiTaoKey() {
  $("#nut-hien-key").addEventListener("click", (e) => {
    const o = $("#o-key");
    const hien = o.type === "password";
    o.type = hien ? "text" : "password";
    e.currentTarget.textContent = hien ? "Ẩn" : "Hiện";
    e.currentTarget.setAttribute("aria-pressed", String(hien));
  });

  $("#nut-kiem-tra-key").addEventListener("click", (e) => bamNut(e.currentTarget, "Đang kiểm tra…", async () => {
    try {
      hienKetQuaKiemTra(await guiJson("/api/key/kiem-tra", "POST", { key: $("#o-key").value }));
    } catch (err) {
      hienKetQuaKiemTra({ hop_le: false, thong_diep: err.message });
    }
  }));

  $("#form-key").addEventListener("submit", (e) => {
    e.preventDefault();
    const nut = $("#nut-luu-key");
    if (nut.disabled) return;
    bamNut(nut, "Đang kiểm tra & lưu…", async () => {
      try {
        const d = await guiJson("/api/key", "POST", { key: $("#o-key").value });
        $("#o-key").value = "";
        $("#o-key").type = "password";
        $("#nut-hien-key").textContent = "Hiện";
        hienKetQuaKiemTra({ ...d.kiem_tra, thong_diep: `Đã lưu key (…${d.cuoi}). ${d.kiem_tra?.thong_diep || ""}` });
        toast("Đã lưu key Gemini của bạn. Từ giờ bài giải dùng key này.", "ok", 5000);
        await TaiKhoan.tai();
      } catch (err) {
        hienKetQuaKiemTra({ hop_le: false, thong_diep: err.message });
      }
    });
  });

  $("#nut-xoa-key").addEventListener("click", (e) => {
    if (!confirm("Xoá key Gemini của bạn khỏi app?\n\nSau khi xoá, bài giải sẽ dùng lượt dùng thử còn lại (nếu có). Key trên Google AI Studio không bị ảnh hưởng.")) return;
    bamNut(e.currentTarget, "Đang xoá…", async () => {
      try {
        await guiJson("/api/key", "DELETE");
        $("#ket-qua-kiem-tra").classList.add("hidden");
        toast("Đã xoá key của bạn khỏi app.", "ok", 4000);
        await TaiKhoan.tai();
      } catch (err) {
        toast("⚠ " + esc(err.message), "loi");
      }
    });
  });
}

// ------------------------------------------------------------ 04 lịch sử
const NHAN_NGUON = { ca_nhan: ["key của bạn", "xanh"], dung_thu: ["dùng thử", "vang"], chu_app: ["key chủ app", ""], demo: ["demo", ""] };
const NHAN_TRANG_THAI = { xong: ["xong", "xanh"], loi: ["lỗi", "do"], dang_giai: ["đang giải", "vang"] };

function ngayGio(s) {
  const m = /^(\d{4})-(\d{2})-(\d{2}) (\d{2}):(\d{2})/.exec(s || "");
  return m ? `${m[3]}/${m[2]}/${m[1]} ${m[4]}:${m[5]}` : esc(s);
}

async function taiLichSu() {
  const khu = $("#lich-su");
  if (!TaiKhoan.toi?.dang_nhap) {
    khu.innerHTML = '<div class="the the-trong p-5"><p class="ghi-chu-nho">Đăng nhập để xem lịch sử bài giải của bạn.</p></div>';
    return;
  }
  try {
    const r = await fetch("/api/lich-su", { cache: "no-store" });
    const d = await r.json();
    if (!r.ok) throw new Error(d.loi || "Không tải được lịch sử.");
    if (!d.bai.length) {
      khu.innerHTML = '<div class="the the-trong p-5"><p class="ghi-chu-nho">Chưa có bài nào. <a class="lien-ket" href="/">Giải bài đầu tiên →</a></p></div>';
      return;
    }
    khu.innerHTML = `<div class="ds-lich-su">${d.bai.map((b) => {
      const [nn, ln] = NHAN_NGUON[b.nguon_key] || [b.nguon_key, ""];
      const [nt, lt] = NHAN_TRANG_THAI[b.trang_thai] || [b.trang_thai, ""];
      const xem = b.trang_thai === "xong"
        ? `<a class="btn btn-sm" href="/?bai=${encodeURIComponent(b.job_id)}#muc-ket-qua">Xem</a>`
        : '<span class="ghi-chu-nho">—</span>';
      return `<div class="dong-lich-su">
        <span class="thoi-gian">${ngayGio(b.tao_luc)}</span>
        <div class="min-w-0">
          <p class="de" title="${esc(b.de_bai)}">${esc(b.de_bai)}</p>
          <div class="phu">
            <span class="huy-hieu ${lt}" ${b.loi ? `title="${esc(b.loi)}"` : ""}>${nt}</span>
            <span class="huy-hieu ${ln}">${esc(nn)}</span>
            ${b.hsg ? '<span class="huy-hieu">HSG</span>' : ""}
            ${b.mo_hinh ? `<span class="huy-hieu">${esc(b.mo_hinh)}</span>` : ""}
          </div>
          ${b.loi ? `<p class="ghi-chu-nho mt-1" style="color: var(--gach)">${esc(b.loi.slice(0, 220))}</p>` : ""}
        </div>
        ${xem}
      </div>`;
    }).join("")}</div>`;
  } catch (e) {
    khu.innerHTML = `<div class="hop-canh-bao">${esc(e.message)}</div>`;
  }
}

// ------------------------------------------------------------ 08 hoạt động (chỉ admin)
let hd = null; // { boLoc, cuoiId, nhoNhat }

function veMucHoatDong(t) {
  const la = Boolean(t.dang_nhap && t.la_chu_app);
  $("#muc-hoat-dong").hidden = !la;
  $("#ben-hoat-dong").hidden = !la;
  $("#tong-muc-ben").textContent = la ? " / 08" : " / 07";
  const ben = $("#ben-hoat-dong");
  ben.querySelector(".cham-thong-bao")?.remove();
  if (la && t.hoat_dong_moi > 0) ben.insertAdjacentHTML("beforeend", `<span class="cham-thong-bao" title="${t.hoat_dong_moi} lượt đăng ký/đăng nhập mới"></span>`);
  if (la && !hd) khoiTaoHoatDong();
}

async function taiOnline() {
  try {
    HoatDong.veOnline($("#hd-online"), (await HoatDong.lay("/admin/api/online")).online);
  } catch (e) {
    $("#hd-online").innerHTML = `<div class="hop-canh-bao">${esc(e.message)}</div>`;
  }
}

async function taiHoatDong(them = false) {
  const q = new URLSearchParams(hd.boLoc.giaTri());
  q.set("gioi_han", "40");
  if (them && hd.nhoNhat) q.set("truoc_id", hd.nhoNhat);
  try {
    const d = await HoatDong.lay(`/admin/api/hoat-dong?${q}`);
    HoatDong.veDanhSach($("#hd-ds"), d.su_kien, them);
    if (d.su_kien.length) hd.nhoNhat = d.su_kien[d.su_kien.length - 1].id;
    $("#hd-them").classList.toggle("hidden", d.su_kien.length < 40);
    hd.cuoiId = Math.max(hd.cuoiId || 0, d.id_moi_nhat || 0);
  } catch (e) {
    $("#hd-ds").innerHTML = `<div class="hop-canh-bao">${esc(e.message)}</div>`;
  }
}

function khoiTaoHoatDong() {
  hd = { cuoiId: 0, nhoNhat: null };
  hd.boLoc = HoatDong.taoBoLoc($("#hd-loc"), () => { hd.nhoNhat = null; taiHoatDong(); });
  HoatDong.ganMoChiTiet($("#muc-hoat-dong"), () => { taiOnline(); taiHoatDong(); });
  $("#hd-tai-lai").addEventListener("click", () => { taiOnline(); taiHoatDong(); });
  $("#hd-them").addEventListener("click", () => taiHoatDong(true));
  Promise.all([taiOnline(), taiHoatDong()]).then(() => {
    if (!("IntersectionObserver" in window)) { HoatDong.danhDauDaXem(hd.cuoiId); return; }
    const io = new IntersectionObserver((ds) => { // admin đã xem mục Hoạt động → tắt chấm thông báo
      if (ds.some((d) => d.isIntersecting)) { HoatDong.danhDauDaXem(hd.cuoiId); io.disconnect(); }
    });
    io.observe($("#hd-ds"));
  });
}

// ------------------------------------------------------------ 05 Mã Claude
let henKhoaMa = null;
const ngayVN = (s) => String(s || "").split("-").reverse().join("/");

function khoaOMa(giay) { // nhập sai 5 lần/giờ → backend khoá 1 giờ; giao diện đếm ngược
  clearInterval(henKhoaMa);
  const het = Date.now() + giay * 1000;
  const ve = () => {
    const con = Math.ceil((het - Date.now()) / 1000);
    const khoa = con > 0;
    $("#o-ma-claude").disabled = khoa;
    $("#nut-ma-claude").disabled = khoa;
    if (khoa) $("#kq-ma-claude").innerHTML = `<span class="chu-loi">Nhập sai quá 5 lần – ô nhập bị khoá, thử lại sau ${Math.ceil(con / 60)} phút.</span>`;
    else { clearInterval(henKhoaMa); $("#kq-ma-claude").textContent = ""; }
  };
  ve();
  henKhoaMa = setInterval(ve, 15000);
}

function veMaClaude(t) {
  const c = t.claude || {};
  const el = $("#tt-ma-claude");
  if (!t.dang_nhap) { el.innerHTML = '<p class="ghi-chu-nho">Đăng nhập để nhập mã Claude.</p>'; $("#form-ma-claude").hidden = true; return; }
  $("#form-ma-claude").hidden = !!c.la_admin;
  if (c.la_admin) {
    el.innerHTML = '<p><span class="huy-hieu xanh">admin</span> Bạn luôn dùng được Claude, không cần mã – bật “Dùng Claude” ở trang giải.</p>';
  } else if (c.co_ma && c.co_the_dung) {
    el.innerHTML = `<p><b>Claude: còn ${c.con_luot} lượt, hết hạn ngày ${ngayVN(c.het_han)}</b> <span class="ghi-chu-nho font-mono">(${esc(c.goi_y)})</span></p>
      <p class="ghi-chu-nho mt-1">Trên trang giải, bật “Dùng Claude cho bài HSG”. Nhập mã khác sẽ thay mã này.
        <button type="button" id="nut-go-ma" class="lien-ket">Gỡ mã</button></p>`;
  } else if (c.co_ma) {
    el.innerHTML = `<p><span class="huy-hieu do">${esc(c.ten_trang_thai)}</span> Mã <span class="font-mono">${esc(c.goi_y)}</span>
      không dùng được nữa – hãy xin admin mã khác.</p>`;
  } else {
    el.innerHTML = '<p>Chưa có mã Claude – bài giải dùng Gemini (Flash-Lite / Flash / Pro theo độ khó).</p>';
  }
  $("#nut-go-ma")?.addEventListener("click", async () => {
    if (!confirm("Gỡ mã Claude khỏi tài khoản này?")) return;
    try { await guiJson("/api/ma-claude", "DELETE"); await TaiKhoan.tai(); toast("Đã gỡ mã Claude.", "ok"); }
    catch (e) { toast(esc(e.message), "loi"); }
  });
  if (c.khoa_nhap_giay > 0) khoaOMa(c.khoa_nhap_giay);
}

function khoiTaoMaClaude() {
  $("#form-ma-claude").addEventListener("submit", async (e) => {
    e.preventDefault();
    const ma = $("#o-ma-claude").value.trim();
    if (!ma) { $("#kq-ma-claude").textContent = "Hãy nhập mã (dạng CLD-XXXX-XXXX)."; return; }
    const nut = $("#nut-ma-claude");
    nut.disabled = true;
    try {
      const r = await TaiKhoan.goi("/api/ma-claude", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ ma }) });
      const d = await r.json().catch(() => ({}));
      if (!r.ok) {
        if (d.ma === "ma_claude_bi_khoa") { khoaOMa(d.cho || 3600); return; }
        $("#kq-ma-claude").innerHTML = `<span class="chu-loi">${esc(d.loi || "Không kích hoạt được mã.")}</span>`;
        return;
      }
      $("#o-ma-claude").value = "";
      $("#kq-ma-claude").innerHTML = `<span class="chu-ok">✓ Đã kích hoạt: Claude còn ${d.con_luot} lượt, hết hạn ngày ${ngayVN(d.het_han)}.</span>`;
      await TaiKhoan.tai();
    } catch {
      $("#kq-ma-claude").textContent = "Không kết nối được server, hãy thử lại.";
    } finally {
      if (!$("#o-ma-claude").disabled) nut.disabled = false;
    }
  });
}

// ------------------------------------------------------------ 06 Key Claude (key dùng chung – backend so key, không bao giờ trả key về)
let henKhoaKey = null;

function khoaOKey(giay) { // sai 5 lần/giờ (theo tài khoản + IP) → backend khoá 1 giờ; giao diện đếm ngược
  clearInterval(henKhoaKey);
  const het = Date.now() + giay * 1000;
  const ve = () => {
    const con = Math.ceil((het - Date.now()) / 1000);
    const khoa = con > 0;
    $("#o-key-claude").disabled = khoa;
    $("#nut-key-claude").disabled = khoa;
    if (khoa) $("#kq-key-claude").innerHTML = `<span class="chu-loi">Nhập sai key quá 5 lần – ô nhập bị khoá, thử lại sau ${Math.ceil(con / 60)} phút.</span>`;
    else { clearInterval(henKhoaKey); $("#kq-key-claude").textContent = ""; }
  };
  ve();
  henKhoaKey = setInterval(ve, 15000);
}

function veKeyClaude(t) {
  const k = t.claude_key || {};
  const el = $("#tt-key-claude");
  const form = $("#form-key-claude");
  const an = (x) => { form.hidden = x; form.classList.toggle("hidden", x); }; // class "flex" của Tailwind đè thuộc tính hidden
  if (!t.dang_nhap) { el.innerHTML = '<p class="ghi-chu-nho">Đăng nhập để nhập Key Claude.</p>'; an(true); return; }
  an(!!k.la_admin || k.trang_thai === "da_mo" || k.trang_thai === "bi_thu_hoi");
  if (k.la_admin) {
    el.innerHTML = '<p><span class="huy-hieu xanh">admin</span> Admin luôn dùng được Claude, không cần nhập key – bật “Dùng Claude” ở trang giải.</p>';
  } else if (k.trang_thai === "da_mo") {
    el.innerHTML = `<p class="chu-ok"><b>Đã mở khóa Claude ✓</b></p>
      <p class="ghi-chu-nho mt-1">Kích hoạt lúc ${esc(k.kich_hoat_luc)} · hôm nay còn <b>${k.con_hom_nay}/${k.gioi_han}</b> bài Claude.
        Trên trang giải, bật “Dùng Claude”. <button type="button" id="nut-go-key" class="lien-ket">Gỡ key</button></p>`;
  } else if (k.trang_thai === "key_cu") {
    el.innerHTML = '<p><span class="huy-hieu vang">key đã đổi</span> Chủ app đã đổi Key Claude – hãy nhập key mới để dùng tiếp Claude.</p>';
  } else if (k.trang_thai === "bi_thu_hoi") {
    el.innerHTML = '<p><span class="huy-hieu do">đã thu hồi</span> Admin đã thu hồi quyền Claude của tài khoản này – hãy liên hệ admin.</p>';
  } else if (!k.bat) {
    el.innerHTML = '<p class="ghi-chu-nho">Chủ app chưa bật Key Claude – bài giải dùng Gemini.</p>';
    an(true);
  } else {
    el.innerHTML = '<p>Chưa mở Claude – bài giải dùng Gemini (Flash-Lite / Flash / Pro theo độ khó).</p>';
  }
  $("#nut-go-key")?.addEventListener("click", async () => {
    if (!confirm("Gỡ Key Claude khỏi tài khoản này? Muốn dùng lại Claude phải nhập lại key.")) return;
    try { await guiJson("/api/key-claude", "DELETE"); await TaiKhoan.tai(); toast("Đã gỡ Key Claude.", "ok"); }
    catch (e) { toast(esc(e.message), "loi"); }
  });
  if (k.khoa_nhap_giay > 0 && !form.hidden) khoaOKey(k.khoa_nhap_giay);
}

function khoiTaoKeyClaude() {
  $("#form-key-claude").addEventListener("submit", async (e) => {
    e.preventDefault();
    const key = $("#o-key-claude").value.trim();
    if (!key) { $("#kq-key-claude").textContent = "Hãy nhập key."; return; }
    const nut = $("#nut-key-claude");
    nut.disabled = true;
    try {
      const r = await TaiKhoan.goi("/api/key-claude", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ key }) });
      const d = await r.json().catch(() => ({}));
      $("#o-key-claude").value = ""; // không giữ key trên trang
      if (!r.ok) {
        if (d.ma === "key_claude_bi_khoa") { khoaOKey(d.cho || 3600); return; }
        $("#kq-key-claude").innerHTML = `<span class="chu-loi">${esc(d.loi || "Không kích hoạt được key.")}</span>`;
        if (d.ma === "key_claude_bi_thu_hoi") await TaiKhoan.tai();
        return;
      }
      $("#kq-key-claude").innerHTML = '<span class="chu-ok">✓ Đã mở khóa Claude.</span>';
      await TaiKhoan.tai();
    } catch {
      $("#kq-key-claude").textContent = "Không kết nối được server, hãy thử lại.";
    } finally {
      if (!$("#o-key-claude").disabled) nut.disabled = false;
    }
  });
}

// ------------------------------------------------------------ 07 API key Claude của bạn (backend kiểm tra + mã hoá, không trả key về)
function veClaudeRieng(t) {
  const r = t.claude_rieng || {};
  const el = $("#tt-claude-rieng");
  const phep = !!(t.dang_nhap && r.duoc_phep);
  $("#form-claude-rieng").classList.toggle("hidden", !phep);
  $("#khung-dung-key-rieng").classList.toggle("hidden", !(phep && r.co));
  $("#khung-yeu-cau-key").classList.toggle("hidden", !t.dang_nhap || phep);
  if (!t.dang_nhap) { el.innerHTML = '<p class="ghi-chu-nho">Đăng nhập để dùng API key Claude riêng.</p>'; return; }
  if (!phep) { // chưa được admin bật: không có ô nhập key, chỉ có nút gửi yêu cầu
    el.innerHTML = r.da_yeu_cau
      ? `<p><span class="huy-hieu vang">đang chờ duyệt</span> Đã gửi yêu cầu lúc ${esc(r.yeu_cau_luc)} – admin bật xong mục này sẽ hiện ô nhập key.</p>`
      : "<p>Tài khoản của bạn chưa được dùng API key Claude riêng. Bấm nút dưới để nhờ admin kích hoạt.</p>";
    $("#nut-yeu-cau-key").disabled = !!r.da_yeu_cau;
    $("#nut-yeu-cau-key").textContent = r.da_yeu_cau ? "Đã gửi yêu cầu" : "Gửi yêu cầu cho admin";
    return;
  }
  $("#dung-key-rieng").checked = !!r.dung;
  $("#nut-xoa-claude-rieng").classList.toggle("hidden", !r.co);
  el.innerHTML = r.co
    ? `<p class="chu-ok"><b>Đã lưu key …${esc(r.cuoi)} ✓</b></p><p class="ghi-chu-nho mt-1">Kiểm tra lúc ${esc(r.kiem_tra_luc)} · bật “Dùng Claude” ở trang giải
        để giải bằng key này (không tính lượt Claude của app). Dán key khác rồi bấm Lưu để thay.</p>`
      + (r.loi ? `<p class="chu-loi mt-1">⚠ Key lỗi lần gần nhất: ${esc(r.loi)}</p>` : "")
    : "<p>Chưa có key riêng – Claude chỉ dùng được khi có Key Claude chung / mã Claude.</p>";
}

function khoiTaoClaudeRieng() {
  $("#form-claude-rieng").addEventListener("submit", async (e) => {
    e.preventDefault();
    const key = $("#o-claude-rieng").value.trim();
    if (!key) { $("#kq-claude-rieng").textContent = "Hãy dán API key Claude (sk-ant-api03-…)."; return; }
    const nut = $("#nut-claude-rieng");
    nut.disabled = true;
    $("#kq-claude-rieng").textContent = "Đang gọi thử Claude để kiểm tra key…";
    try {
      const d = await guiJson("/api/claude-rieng", "POST", { key });
      $("#o-claude-rieng").value = ""; // không giữ key trên trang
      $("#kq-claude-rieng").innerHTML = `<span class="chu-ok">✓ Key hợp lệ, đã lưu (…${esc(d.cuoi)}).</span>`
        + (d.canh_bao ? `<br><span class="chu-loi">⚠ ${esc(d.canh_bao)}</span>` : "");
      await TaiKhoan.tai();
    } catch (err) {
      $("#kq-claude-rieng").innerHTML = `<span class="chu-loi">${esc(err.message)}</span>`;
    } finally {
      nut.disabled = false;
    }
  });
  $("#dung-key-rieng").addEventListener("change", async (e) => {
    try { await guiJson("/api/claude-rieng/dung", "POST", { dung: e.target.checked }); await TaiKhoan.tai();
      toast(e.target.checked ? "Sẽ giải bằng key Claude của bạn." : "Đã tắt – giải bằng Key Claude chung / Gemini như bình thường.", "ok", 3500); }
    catch (err) { e.target.checked = !e.target.checked; toast(esc(err.message), "loi"); }
  });
  $("#nut-yeu-cau-key").addEventListener("click", async (e) => {
    e.target.disabled = true;
    try { await guiJson("/api/claude-rieng/yeu-cau", "POST", {}); await TaiKhoan.tai(); toast("Đã gửi yêu cầu cho admin.", "ok", 3500); }
    catch (err) { e.target.disabled = false; toast(esc(err.message), "loi"); }
  });
  $("#nut-xoa-claude-rieng").addEventListener("click", async () => {
    if (!confirm("Xoá API key Claude của bạn khỏi app?")) return;
    try { await guiJson("/api/claude-rieng", "DELETE"); $("#kq-claude-rieng").textContent = ""; await TaiKhoan.tai(); toast("Đã xoá key Claude.", "ok"); }
    catch (err) { toast(esc(err.message), "loi"); }
  });
}

// ------------------------------------------------------------ hoạt ảnh mở Cài đặt (phong cách công nghệ, < 0,8 giây; admin bật/tắt)
let daHoatAnh = false;

function moCaiDat(t) { // chạy một lần khi /api/toi trả về; tắt → hiện ngay; giảm chuyển động → chỉ mờ dần (CSS)
  if (daHoatAnh) return;
  daHoatAnh = true;
  const html = document.documentElement;
  const bat = t.hoat_anh_cai_dat !== false;
  try { localStorage.setItem("hoat-anh-cai-dat", bat ? "1" : "0"); } catch { /* bỏ qua */ }
  if (!bat) { html.classList.remove("cho-hoat-anh"); return; }
  document.querySelectorAll("main .muc").forEach((m, i) => m.style.setProperty("--i", i));
  if (!window.matchMedia?.("(prefers-reduced-motion: reduce)").matches) {
    const lop = document.createElement("div");
    lop.className = "ha-lop";
    lop.setAttribute("aria-hidden", "true");
    lop.innerHTML = '<div class="ha-luoi"></div><div class="ha-quet"></div>';
    document.body.appendChild(lop);
    setTimeout(() => lop.remove(), 900);
    giaiMaTieuDe();
  }
  html.classList.add("hoat-anh-chay");
  html.classList.remove("cho-hoat-anh");
  setTimeout(() => html.classList.remove("hoat-anh-chay"), 1200);
}

function giaiMaTieuDe() { // tiêu đề các mục "giải mã" từ ký tự ngẫu nhiên thành chữ thật (~0,4 giây, lệch nhau 45 ms)
  const KY_TU = "01<>/\\#%&*+=▖▘▝▗";
  document.querySelectorAll("main .muc:not([hidden]) .tieu-de-muc").forEach((el, k) => {
    const goc = el.textContent;
    const batDau = performance.now() + k * 45;
    el.setAttribute("aria-label", goc);
    const ve = (bay) => {
      const p = Math.min(1, Math.max(0, (bay - batDau) / 380));
      const lo = Math.floor(goc.length * p);
      el.textContent = goc.slice(0, lo) + [...goc.slice(lo)].map((c) => (c === " " ? " " : KY_TU[Math.floor(Math.random() * KY_TU.length)])).join("");
      if (p < 1) requestAnimationFrame(ve);
      else { el.textContent = goc; el.removeAttribute("aria-label"); }
    };
    requestAnimationFrame(ve);
  });
}

// ------------------------------------------------------------ cột bên + giao diện sáng/tối
function khoiTaoCotBen() {
  const ids = ["muc-tai-khoan", "muc-key", "muc-huong-dan", "muc-lich-su", "muc-ma-claude", "muc-key-claude", "muc-claude-rieng", "muc-hoat-dong"];
  const dat = (id) => {
    document.querySelectorAll(".muc-ben").forEach((m) => m.classList.toggle("dang", m.dataset.muc === id));
    $("#so-muc-ben").textContent = String(ids.indexOf(id) + 1).padStart(2, "0");
  };
  if (!("IntersectionObserver" in window)) return;
  const dangThay = new Map();
  const io = new IntersectionObserver((ds) => {
    ds.forEach((d) => dangThay.set(d.target.id, d.isIntersecting));
    const id = ids.find((x) => dangThay.get(x));
    if (id) dat(id);
  }, { rootMargin: "-30% 0px -55% 0px" });
  ids.forEach((id) => io.observe(document.getElementById(id)));
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
khoiTaoCotBen();
khoiTaoKey();
khoiTaoMaClaude();
khoiTaoKeyClaude();
khoiTaoClaudeRieng();
TaiKhoan.khiDoi((t) => { veTaiKhoan(t); veKey(t); veMaClaude(t); veKeyClaude(t); veClaudeRieng(t); veMucHoatDong(t); moCaiDat(t); });
$("#nut-tai-lai-lich-su").addEventListener("click", taiLichSu);
TaiKhoan.tai()
  .then(taiLichSu)
  .catch(() => toast("Không kết nối được server. Hãy chạy lại start.command.", "loi", 10000));
