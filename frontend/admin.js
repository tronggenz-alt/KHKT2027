// Trang quản trị /admin (chỉ admin – backend kiểm tra quyền ở mọi /admin/api/*): tổng quan, hoạt động trực tiếp (SSE, dự phòng
// tự làm mới 10 giây), người đang online, biểu đồ 14 ngày, cảnh báo, bảng người dùng + chi tiết/khoá; thông báo trình duyệt khi có đăng ký mới.
"use strict";

const $ = (s) => document.querySelector(s);
const { esc, huyHieu, nutIp } = HoatDong;

function toast(noiDung, loai = "tin", thoiGian = 6000) {
  const el = document.createElement("div");
  el.className = `toast ${loai}`;
  el.innerHTML = noiDung;
  $("#toasts").appendChild(el);
  setTimeout(() => el.remove(), thoiGian);
}

let cuoiId = 0;      // id nhật ký mới nhất đã nhận
let nhoNhat = null;  // id nhỏ nhất đang hiện (để "Xem thêm")
let boLoc = null;
let nguon = null;    // EventSource
let henThamDo = null;
let dsNguoiDung = [];
const moEmail = new Set(); // id những dòng đang hiện email đầy đủ (giữ qua các lần vẽ lại bảng)
const sapXep = { cot: "id", tang: false };

// ------------------------------------------------------------ tổng quan + online + cảnh báo + biểu đồ
// ------------------------------------------------------------ thẻ thu gọn: 1 dòng tóm tắt cho mỗi khối
function tomTat(khoi, chu, canhBao = false) {
  const el = document.querySelector(`[data-tom-tat="${khoi}"]`);
  if (!el) return;
  el.textContent = chu;
  el.title = chu;
  el.classList.toggle("canh-bao", canhBao);
}
let soHoatDongMoi = 0;    // hoạt động mới đến trong lúc khối "Hoạt động trực tiếp" đang thu gọn
let hoatDongGanNhat = null;
function tomTatTrucTiep() {
  const d = hoatDongGanNhat;
  const ganNhat = d ? `${(HoatDong.SU_KIEN[d.su_kien] || [d.ten_su_kien || d.su_kien])[0]} · ${d.ten || d.email || "?"} · ${HoatDong.gio(d.ts)}`
    : "Chưa có hoạt động";
  tomTat("truc-tiep", soHoatDongMoi ? `${soHoatDongMoi} hoạt động mới · ${ganNhat}` : `Gần nhất: ${ganNhat}`);
}

// ------------------------------------------------------------ mở / đóng một khối bằng hiệu ứng quét (ChuyenCanh – chuyen-canh.js)
// Các khối luôn nằm trong trang (chỉ ẩn / hiện) → dữ liệu vẫn cập nhật trực tiếp như cũ. URL #ten-khoi + history → nút Back đóng khối.
const KHOI_HOP_LE = () => [...document.querySelectorAll("[data-khoi]")].map((k) => k.dataset.khoi);
let theVuaMo = null;
let viTriCuon = 0;

function datCheDo(khoi) { // khoi = "" → lưới thẻ
  const main = $("#ad-main");
  main.classList.toggle("che-do-mo", !!khoi);
  main.classList.toggle("che-do-luoi", !khoi);
  document.querySelectorAll("[data-khoi]").forEach((k) => k.classList.toggle("dang-mo", k.dataset.khoi === khoi));
  if (!khoi) {
    window.scrollTo(0, viTriCuon);
    if (theVuaMo) theVuaMo.focus({ preventScroll: true });
    return;
  }
  const sec = document.querySelector(`[data-khoi="${khoi}"]`);
  $("#ten-khoi-mo").textContent = sec.querySelector("h2").textContent;
  sec.classList.remove("hien-dan");
  void sec.offsetWidth; // chạy lại animation mờ dần
  sec.classList.add("hien-dan");
  window.scrollTo(0, 0);
  sec.querySelector("h2").focus({ preventScroll: true });
  if (khoi === "truc-tiep") { soHoatDongMoi = 0; tomTatTrucTiep(); }
}

const khoiDangMo = () => document.querySelector("[data-khoi].dang-mo")?.dataset.khoi || "";

async function chuyenKhoi(khoi) {
  if (khoi === khoiDangMo()) return;
  const dangMo = khoiDangMo();
  if (khoi && !dangMo) viTriCuon = window.scrollY; // nhớ chỗ đang cuộn ở lưới thẻ để quay lại đúng chỗ
  const the = (k) => document.querySelector(`[data-mo="${k}"]`);
  if (khoi) theVuaMo = the(khoi);
  const tuyChon = khoi
    ? { tu: dangMo ? null : the(khoi), nhan: `Mở · ${the(khoi)?.querySelector(".the-nho-tieu-de")?.firstChild?.textContent.trim() || ""}` }
    : { ve: () => theVuaMo || the(dangMo), nhan: "Về · Bảng quản trị" };  // mở: nở ra từ thẻ · đóng: thu về đúng thẻ đó
  const daChay = await ChuyenCanh.chuyen(() => datCheDo(khoi), tuyChon);
  if (!daChay) return; // đang có một lượt chuyển cảnh → lượt đó xong sẽ tự đồng bộ theo URL
  dongBoTheoUrl();
}

function khoiTheoUrl() {
  const k = decodeURIComponent(location.hash.slice(1));
  return KHOI_HOP_LE().includes(k) ? k : "";
}
function dongBoTheoUrl() { // bấm Back / Forward liên tục trong lúc màn đang chạy → khớp lại không hiệu ứng
  if (!ChuyenCanh.dangChay && khoiTheoUrl() !== khoiDangMo()) datCheDo(khoiTheoUrl());
}

function moKhoi(khoi, nut) {
  theVuaMo = nut || document.querySelector(`[data-mo="${khoi}"]`);
  history.pushState({ khoi }, "", `#${khoi}`);
  chuyenKhoi(khoi);
}
function dongKhoi() {
  if (history.state && history.state.khoi) history.back(); // → popstate → chuyenKhoi("")
  else { history.replaceState(null, "", location.pathname + location.search); chuyenKhoi(""); }
}

function ganTheThuGon() {
  document.querySelectorAll("[data-mo]").forEach((nut) => nut.addEventListener("click", () => moKhoi(nut.dataset.mo, nut)));
  $("#nut-quay-lai").addEventListener("click", dongKhoi);
  window.addEventListener("popstate", () => chuyenKhoi(khoiTheoUrl()));
  document.addEventListener("keydown", (e) => {
    if (e.key !== "Escape" || !khoiDangMo() || document.querySelector("dialog[open]")) return; // Esc trong hộp thoại: đóng hộp thoại trước
    e.preventDefault();
    dongKhoi();
  });
  if (khoiTheoUrl()) { // mở thẳng /admin#ten-khoi (tải lại trang khi đang mở một khối) – không chạy hiệu ứng
    history.replaceState({ khoi: khoiTheoUrl() }, "", location.href);
    datCheDo(khoiTheoUrl());
  }
}

async function taiTongQuan() {
  const d = await HoatDong.lay("/admin/api/tong-quan");
  document.querySelectorAll("[data-so]").forEach((el) => { el.textContent = d[el.dataset.so] ?? "–"; });
  cuoiId = Math.max(cuoiId, d.id_moi_nhat || 0);
}

async function taiOnline() {
  const ds = (await HoatDong.lay("/admin/api/online")).online;
  HoatDong.veOnline($("#ad-online"), ds);
  tomTat("online", ds.length ? `${ds.length} người đang online: ${ds.slice(0, 3).map((x) => x.ten || x.email || "?").join(", ")}${ds.length > 3 ? "…" : ""}`
    : "Chưa ai online (5 phút gần nhất)");
}

async function taiCanhBao() {
  const cb = await HoatDong.lay("/admin/api/canh-bao");
  const the = [];
  const tk = (ds) => ds.map((t) => `<button type="button" class="ten-nd lien-ket" data-uid="${t.id}">${esc(t.email || t.ten || "#" + t.id)}</button>`).join(", ");
  for (const x of cb.nhieu_tai_khoan_cung_ip) {
    the.push(`<div class="the the-canh-bao tim"><p class="font-semibold">${x.so_tai_khoan} tài khoản cùng một IP</p>
      <p class="text-sm mt-1">${nutIp(x.ip_info, x.ip)}</p>
      <p class="ghi-chu-nho mt-1">7 ngày qua · lần cuối ${HoatDong.ngayChu(x.lan_cuoi)}</p><p class="text-sm mt-2">${tk(x.tai_khoan)}</p></div>`);
  }
  for (const x of cb.dang_nhap_sai_nhieu) {
    the.push(`<div class="the the-canh-bao do"><p class="font-semibold">${esc(x.email)}: sai ${x.so_lan} lần</p>
      <p class="ghi-chu-nho mt-1">24 giờ qua · lần cuối ${HoatDong.ngayChu(x.lan_cuoi)}</p>
      <p class="text-sm mt-1">${x.ips.map((ip) => nutIp(null, ip)).join(", ") || "IP ?"}</p></div>`);
  }
  for (const x of cb.dang_nhap_sai_theo_ip) {
    the.push(`<div class="the the-canh-bao do"><p class="font-semibold">Một IP đăng nhập sai ${x.so_lan} lần (${x.so_email} email)</p>
      <p class="text-sm mt-1">${nutIp(x.ip_info, x.ip)}</p>
      <p class="ghi-chu-nho mt-1">24 giờ qua · lần cuối ${HoatDong.ngayChu(x.lan_cuoi)}</p></div>`);
  }
  for (const x of cb.tao_hang_loat) {
    const khi = x.ip ? `${nutIp(null, x.ip)} trong ${esc(x.trong)}` : `${HoatDong.ngayChu(x.tu)} → ${esc((x.den || "").slice(11, 16))}`;
    the.push(`<div class="the the-canh-bao"><p class="font-semibold">Tạo ${x.so_dang_ky} tài khoản hàng loạt</p>
      <p class="ghi-chu-nho mt-1">${khi}</p><p class="text-sm mt-2" style="overflow-wrap:anywhere">${esc(x.emails.join(", "))}</p></div>`);
  }
  for (const x of cb.vi_tri_la || []) {
    the.push(`<div class="the the-canh-bao ${x.loai === "quoc_gia" ? "do" : ""}"><p class="font-semibold">Đăng nhập từ ${x.loai === "quoc_gia" ? "quốc gia" : "tỉnh/thành"} khác thường lệ</p>
      <p class="text-sm mt-1">${tk([{ id: x.user_id, ten: x.ten, email: x.email }])}: <b>${esc(x.noi)}</b> <span class="ghi-chu-nho">(thường: ${esc(x.thuong_le)})</span></p>
      <p class="text-sm mt-1">${nutIp(x.ip_info, x.ip)}</p>
      <p class="ghi-chu-nho mt-1">${HoatDong.ngayChu(x.lan)} · ${esc(x.thiet_bi || "")} · vị trí IP chỉ gần đúng</p></div>`);
  }
  $("#ad-canh-bao").innerHTML = the.join("") || '<p class="ghi-chu-nho" style="color: var(--nhan)">✓ Không có dấu hiệu đáng ngờ.</p>';
  tomTat("canh-bao", the.length ? `${the.length} cảnh báo cần xem` : "✓ Không có dấu hiệu đáng ngờ", the.length > 0);
}

async function taiBieuDo() {
  const ds = (await HoatDong.lay("/admin/api/bieu-do?ngay=14")).ngay;
  const W = 560, H = 190, T = 12, B = 26, L = 28, R = 6;
  const max = Math.max(4, ...ds.map((d) => Math.max(d.dang_nhap, d.dang_ky)));
  const buoc = Math.ceil(max / 4);
  const tran = buoc * 4;
  const y = (v) => T + (H - T - B) * (1 - v / tran);
  const o = (W - L - R) / ds.length;
  const c = Math.max(3, o * 0.32);
  let svg = "";
  for (let k = 0; k <= 4; k += 1) {
    const v = k * buoc;
    svg += `<line class="luoi" x1="${L}" x2="${W - R}" y1="${y(v)}" y2="${y(v)}"/><text x="${L - 6}" y="${y(v) + 3}" text-anchor="end">${v}</text>`;
  }
  ds.forEach((d, i) => {
    const x = L + i * o + o / 2;
    const nhan = `${d.ngay.slice(8, 10)}/${d.ngay.slice(5, 7)}`;
    svg += `<rect class="cot-dn" x="${x - c - 1}" y="${y(d.dang_nhap)}" width="${c}" height="${y(0) - y(d.dang_nhap)}" rx="1.5"><title>${nhan}: ${d.dang_nhap} lượt đăng nhập</title></rect>`;
    svg += `<rect class="cot-dk" x="${x + 1}" y="${y(d.dang_ky)}" width="${c}" height="${y(0) - y(d.dang_ky)}" rx="1.5"><title>${nhan}: ${d.dang_ky} đăng ký</title></rect>`;
    if (i % 2 === ds.length % 2 || i === ds.length - 1) svg += `<text x="${x}" y="${H - 8}" text-anchor="middle">${nhan}</text>`;
  });
  tomTat("bieu-do", `${so(ds.reduce((t, d) => t + d.dang_nhap, 0))} lượt đăng nhập · ${so(ds.reduce((t, d) => t + d.dang_ky, 0))} đăng ký trong 14 ngày`);
  $("#ad-bieu-do").innerHTML = `<svg class="bieu-do-hd" viewBox="0 0 ${W} ${H}" role="img" aria-label="Số lượt đăng nhập và đăng ký mỗi ngày trong 14 ngày">${svg}</svg>`;
}

// ------------------------------------------------------------ bảng người dùng (tìm + sắp xếp)
const XEP_TT = (d) => (d.bi_khoa ? 0 : d.online ? 2 : 1);

async function taiNguoiDung() {
  dsNguoiDung = (await HoatDong.lay("/admin/api/nguoi-dung")).nguoi_dung;
  dsNguoiDung.forEach((d) => { // sắp xếp cột "Key Claude riêng": đang yêu cầu → được bật → còn lại
    const k = d.key_claude_rieng || {};
    d.key_rieng = k.yeu_cau_luc ? 0 : k.la_admin ? 2 : k.bat ? 1 : 3;
  });
  veBang();
  const yc = dsNguoiDung.filter((d) => (d.key_claude_rieng || {}).yeu_cau_luc).length;
  const ngo = dsNguoiDung.filter((d) => d.nghi_ngo).length;
  tomTat("nguoi-dung", [`${so(dsNguoiDung.length)} người dùng`, `${dsNguoiDung.filter((d) => d.online).length} online`,
    yc ? `${yc} yêu cầu key Claude` : "", ngo ? `${ngo} email nghi ngờ` : ""].filter(Boolean).join(" · "), yc + ngo > 0);
}

function veBang() {
  const tim = $("#ad-tim").value.trim().toLowerCase();
  const chiBat = $("#ad-loc-key-bat").checked, chiYc = $("#ad-loc-key-yc").checked;
  let ds = dsNguoiDung.filter((d) => (!tim || `${d.ten} ${d.email}`.toLowerCase().includes(tim))
    && (!chiBat || (d.key_claude_rieng || {}).bat) && (!chiYc || (d.key_claude_rieng || {}).yeu_cau_luc));
  const giaTri = (d) => (sapXep.cot === "trang_thai" ? XEP_TT(d) : d[sapXep.cot] ?? "");
  ds = ds.sort((a, b) => {
    const x = giaTri(a), y = giaTri(b);
    const ss = typeof x === "number" && typeof y === "number" ? x - y : String(x).localeCompare(String(y), "vi");
    return sapXep.tang ? ss : -ss;
  });
  document.querySelectorAll(".bang-nd th").forEach((th) => {
    th.setAttribute("aria-sort", th.dataset.cot === sapXep.cot ? (sapXep.tang ? "ascending" : "descending") : "none");
  });
  const soYc = dsNguoiDung.filter((d) => (d.key_claude_rieng || {}).yeu_cau_luc).length;
  $("#ad-so-nd").textContent = `(${ds.length}${tim || chiBat || chiYc ? "/" + dsNguoiDung.length : ""})` + (soYc ? ` · ${soYc} đang yêu cầu bật key Claude riêng` : "");
  $("#ad-bang").innerHTML = ds.map((d) => {
    const [xt, lxt] = HoatDong.XAC_THUC[d.xac_thuc] || HoatDong.XAC_THUC["-"];
    const tt = d.bi_khoa ? huyHieu("đã khoá", "do") : d.online ? huyHieu("● online", "xanh") : '<span class="ghi-chu-nho">—</span>';
    return `<tr data-uid="${d.id}" tabindex="0">
      <td class="so-hd">${d.id}</td><td>${esc(d.ten || "—")}</td><td class="o-email${moEmail.has(d.id) ? " mo" : ""}">${oEmail(d)}</td>
      <td>${huyHieu(d.vai_tro, d.la_admin ? "xanh" : d.la_ki_cuu ? "lam" : "")}</td><td>${huyHieu(xt, lxt)}${d.nghi_ngo
        ? ` <span title="${esc(d.nghi_ngo)}">${huyHieu("nghi ngờ", "vang")}</span>` : ""}</td>
      <td class="whitespace-nowrap">${ngayHaiDong(d.tao_luc)}</td><td class="whitespace-nowrap">${ngayHaiDong(d.dang_nhap_cuoi)}</td>
      <td class="o-ip">${nutIp(d.ip_cuoi_info, d.ip_cuoi, true) || '<span class="ghi-chu-nho">—</span>'}</td>
      <td class="so-hd">${d.so_bai_giai}</td><td>${tt}</td><td class="o-key-rieng">${oKeyRieng(d)}</td></tr>`;
  }).join("") || '<tr><td colspan="11" class="ghi-chu-nho">Không có ai khớp.</td></tr>';
}

// ngày trên, giờ dưới (bảng người dùng gọn hơn, không phải cuộn ngang)
const ngayHaiDong = (x) => HoatDong.ngayChu(x).replace(/^(\S+) (\d\d:\d\d)$/, '$1<br><small class="ghi-chu-nho">$2</small>');
// email dài: rút gọn "toannguy…@gmail.com", bấm vào mới hiện đầy đủ (bấm lần nữa để thu gọn) – không mở chi tiết
function emailGon(e) {
  const i = e.lastIndexOf("@");
  if (e.length <= 22 || i < 0) return e;
  const ten = e.slice(0, i), mien = e.slice(i + 1);
  return `${ten.slice(0, 8)}…@${mien.length > 14 ? mien.slice(0, 12) + "…" : mien}`;
}
function oEmail(d) {
  if (!d.email) return '<span class="ghi-chu-nho">—</span>';
  const gon = emailGon(d.email);
  if (gon === d.email) return esc(d.email);
  return `<button type="button" class="nut-email" data-email-uid="${d.id}" title="Bấm để xem đầy đủ" aria-expanded="${moEmail.has(d.id)}">
    <span class="email-gon">${esc(gon)}</span><span class="email-day">${esc(d.email)}</span></button>`;
}

function oKeyRieng(d) { // công tắc bật/tắt quyền dùng API key Claude riêng + trạng thái key
  const k = d.key_claude_rieng;
  if (!k) return "";
  return `<span class="flex items-center gap-1"><label class="cong-tac cong-tac-nho" title="${k.la_admin ? "Admin luôn được dùng" : "Bật / tắt quyền dùng key Claude riêng"}">
      <input type="checkbox" class="tg-key-rieng" data-uid="${d.id}" ${k.bat ? "checked" : ""} ${k.la_admin ? "disabled" : ""}>
      <span class="ranh"><span class="nut-tron"></span></span></label>${HoatDong.moTaKeyRieng(k)}</span>`;
}

// ------------------------------------------------------------ hoạt động: danh sách + luồng trực tiếp
async function taiHoatDong(them = false) {
  const q = new URLSearchParams(boLoc.giaTri());
  q.set("gioi_han", "50");
  if (them && nhoNhat) q.set("truoc_id", nhoNhat);
  const d = await HoatDong.lay(`/admin/api/hoat-dong?${q}`);
  HoatDong.veDanhSach($("#ad-ds"), d.su_kien, them);
  if (!them && d.su_kien.length && (!hoatDongGanNhat || d.su_kien[0].id >= hoatDongGanNhat.id)) { hoatDongGanNhat = d.su_kien[0]; tomTatTrucTiep(); }
  else if (!them && !hoatDongGanNhat) tomTatTrucTiep();
  if (d.su_kien.length) nhoNhat = d.su_kien[d.su_kien.length - 1].id;
  $("#ad-them").classList.toggle("hidden", d.su_kien.length < 50);
  cuoiId = Math.max(cuoiId, d.id_moi_nhat || 0);
}

let henLamMoi = null;
let henDaXem = null;
function nhanSuKien(d) {
  if (d.id <= cuoiId) return;
  cuoiId = d.id;
  if (boLoc.khop(d)) HoatDong.chenMoi($("#ad-ds"), d);
  hoatDongGanNhat = d;
  if (khoiDangMo() !== "truc-tiep") soHoatDongMoi += 1;
  tomTatTrucTiep();
  if (d.su_kien === "dang_ky") thongBaoDangKy(d);
  if (d.su_kien === "yeu_cau_key_claude_rieng") thongBaoYeuCauKey(d);
  clearTimeout(henLamMoi); // gom nhiều sự kiện liền nhau thành một lần làm mới
  henLamMoi = setTimeout(() => { taiTongQuan(); taiOnline(); taiNguoiDung(); taiCanhBao(); taiBieuDo(); taiChiPhi().catch(() => {});
    taiEmailRac().catch(() => {}); }, 1500);
  if (document.visibilityState === "visible") {
    clearTimeout(henDaXem);
    henDaXem = setTimeout(() => HoatDong.danhDauDaXem(cuoiId), 2000);
  }
}

function trangThai(kieu) {
  const el = $("#ad-trang-thai");
  el.className = `trang-thai-luong ${kieu}`;
  el.querySelector("span").textContent = kieu === "song" ? "trực tiếp" : kieu === "cham" ? "tự làm mới mỗi 10 giây" : "đang kết nối…";
}

function batLuong() {
  if (!("EventSource" in window)) { batThamDo(); return; }
  try { nguon?.close(); } catch { /* bỏ qua */ }
  nguon = new EventSource(`/admin/api/luong?sau=${cuoiId}`);
  nguon.onopen = () => { trangThai("song"); clearInterval(henThamDo); henThamDo = null; };
  nguon.onmessage = (e) => { try { nhanSuKien(JSON.parse(e.data)); } catch { /* bỏ qua */ } };
  nguon.onerror = () => { // mất kết nối SSE (proxy chặn, mạng chập chờn…) → tự làm mới mỗi 10 giây, thỉnh thoảng thử SSE lại
    nguon.close();
    nguon = null;
    batThamDo();
  };
}

let lanThamDo = 0;
function batThamDo() {
  trangThai("cham");
  if (henThamDo) return;
  henThamDo = setInterval(async () => {
    lanThamDo += 1;
    try {
      const d = await HoatDong.lay("/admin/api/hoat-dong?gioi_han=100");
      d.su_kien.filter((x) => x.id > cuoiId).reverse().forEach(nhanSuKien);
    } catch (e) {
      if (e.status === 401 || e.status === 403) location.href = "/dang-nhap?tiep=%2Fadmin";
    }
    if (lanThamDo % 6 === 0) { clearInterval(henThamDo); henThamDo = null; batLuong(); }
  }, 10000);
}

// ------------------------------------------------------------ chi phí AI
const so = (n) => Number(n || 0).toLocaleString("vi-VN");
const ngayVN = (s) => String(s || "").split("-").reverse().join("/");

async function taiChiPhi() {
  const ngay = document.querySelector('input[name="cp-ngay"]:checked')?.value || "1";
  const d = await HoatDong.lay(`/admin/api/chi-phi?ngay=${ngay}`);
  const t = d.tong;
  tomTat("chi-phi", `${{ 1: "Hôm nay", 7: "7 ngày", 30: "30 ngày" }[ngay] || "Hôm nay"}: ${so(t.so_lan)} lượt gọi AI · ước tính $${t.usd.toFixed(3)}`);
  const dong = d.theo_mo_hinh.map((r) => `<tr>
      <td style="overflow-wrap:anywhere"><span class="font-mono">${esc(r.model || "—")}</span> <span class="ghi-chu-nho">${esc(r.provider)}</span></td>
      <td class="so-hd">${so(r.so_lan)}</td><td class="so-hd">${r.loi ? `<span class="chu-loi">${so(r.loi)}</span>` : "—"}</td>
      <td class="so-hd">${so(r.tokens_in)}</td><td class="so-hd">${so(r.tokens_out)}${r.tokens_think ? ` <small class="ghi-chu-nho">(nghĩ ${so(r.tokens_think)})</small>` : ""}</td>
      <td class="so-hd">${r.nang_cap ? so(r.nang_cap) : "—"}</td><td class="so-hd">${r.key_rieng ? so(r.key_rieng) : "—"}</td>
      <td class="so-hd">$${r.usd.toFixed(4)}</td></tr>`).join("");
  const muc = d.ti_le_muc_do.map((m) => `<div class="dong-ti-le"><span>${esc(m.ten)}</span>
      <span class="thanh-ti-le"><i class="${m.muc_do}" style="width:${m.phan_tram}%"></i></span><b>${m.phan_tram}%</b>
      <small class="ghi-chu-nho">${so(m.so_bai)} bài</small></div>`).join("");
  const claude = d.claude_nhieu_nhat.length
    ? `<ol class="ds-claude">${d.claude_nhieu_nhat.map((r) => `<li><button type="button" class="ten-nd lien-ket" data-uid="${r.user_id}">${esc(r.ten || r.email || "#" + r.user_id)}</button>
        <span class="ghi-chu-nho">${so(r.so_lan)} lượt gọi · ${so(r.so_bai)} bài · ${so(r.tokens_in)} / ${so(r.tokens_out)} token</span></li>`).join("")}</ol>`
    : '<p class="ghi-chu-nho">Chưa ai dùng Claude trong khoảng này.</p>';
  $("#ad-chi-phi").innerHTML = `
    <div class="luoi-so luoi-so-nho">
      <div class="the the-so"><span class="so">${so(t.so_lan)}</span><span class="nhan">Lượt gọi AI</span></div>
      <div class="the the-so"><span class="so">${so(t.tokens_in)}</span><span class="nhan">Token vào</span></div>
      <div class="the the-so"><span class="so">${so(t.tokens_out)}</span><span class="nhan">Token ra (kể cả suy nghĩ)</span></div>
      <div class="the the-so vang"><span class="so">$${t.usd.toFixed(3)}</span><span class="nhan">Ước tính nếu trả phí</span></div>
    </div>
    ${dong ? `<div class="cuon-ngang mt-4"><table class="bang-nd"><thead><tr><th>Mô hình</th><th class="so-hd">Lượt</th><th class="so-hd">Lỗi</th><th class="so-hd">Token vào</th><th class="so-hd">Token ra</th>
      <th class="so-hd">Nâng cấp</th><th class="so-hd">Key người dùng</th><th class="so-hd">Ước tính</th></tr></thead><tbody>${dong}</tbody></table></div>`
      : '<p class="ghi-chu-nho mt-4">Chưa có lượt gọi AI nào trong khoảng này.</p>'}
    <div class="grid gap-6 mt-5 md:grid-cols-2">
      <div><h3 class="nhan-mono">Tỉ lệ đề Dễ / Trung bình / HSG</h3><div class="mt-2">${muc}</div>
        <p class="ghi-chu-nho mt-1">${d.bai_claude ? `${so(d.bai_claude)} bài giải bằng Claude.` : ""}${d.bai_chua_phan_loai ? ` ${so(d.bai_chua_phan_loai)} bài chưa phân loại (bài cũ hoặc tự chọn mức thông minh).` : ""}</p></div>
      <div><h3 class="nhan-mono">Dùng Claude nhiều nhất</h3><div class="mt-2">${claude}</div></div>
    </div>
    ${(d.theo_muc_thong_minh || []).length ? `<h3 class="nhan-mono mt-5">Thời gian &amp; token mỗi bài theo mức độ thông minh (so sánh trước / sau)</h3>
      <div class="cuon-ngang mt-2"><table class="bang-nd"><thead><tr><th>Mức</th><th class="so-hd">Số bài</th><th class="so-hd">Thời gian TB</th><th class="so-hd">Token TB / bài</th>
        <th class="so-hd">Lần gọi AI TB</th><th class="so-hd">Từ cache</th></tr></thead><tbody>${d.theo_muc_thong_minh.map((r) => `<tr><td>${esc(r.ten)}</td>
        <td class="so-hd">${so(r.so_bai)}</td><td class="so-hd">${r.tb_giay} s</td><td class="so-hd">${so(r.tb_token)}</td>
        <td class="so-hd">${r.tb_lan_goi}</td><td class="so-hd">${r.tu_cache || "—"}</td></tr>`).join("")}</tbody></table></div>` : ""}
    ${d.tra_cuu_tai_lieu && d.tra_cuu_tai_lieu.so_bai_giai ? `<p class="ghi-chu-nho mt-3">Tra cứu tài liệu khoa học: ${so(d.tra_cuu_tai_lieu.so_bai_giai)} bài giải
      có tra cứu · trung bình ${d.tra_cuu_tai_lieu.tb_so_bai_bao} bài báo/lần · ~${so(d.tra_cuu_tai_lieu.tong_token)} token phần tài liệu.</p>` : ""}
    <p class="ghi-chu-nho mt-4">${esc(d.ghi_chu)}</p>`;
}

// ------------------------------------------------------------ mã kích hoạt Claude
const MAU_TT_MA = { con_hieu_luc: "xanh", het_han: "vang", het_luot: "vang", thu_hoi: "do" };

async function taiMaClaude() {
  const ds = (await HoatDong.lay("/admin/api/ma-claude")).ma;
  const conHieuLuc = ds.filter((x) => x.trang_thai === "con_hieu_luc");
  tomTat("ma-claude", ds.length ? `${conHieuLuc.length} mã còn hiệu lực / ${ds.length} · còn ${so(conHieuLuc.reduce((t, x) => t + x.con_lai, 0))} lượt`
    : "Chưa tạo mã nào");
  $("#ad-ds-ma").innerHTML = ds.map((x) => `<tr>
      <td class="so-hd">${x.id}</td><td class="font-mono whitespace-nowrap">${esc(x.goi_y)}</td>
      <td class="whitespace-nowrap">${x.da_dung}/${x.tong_luot} <span class="ghi-chu-nho">(còn ${x.con_lai}${x.dang_giu ? `, đang giữ ${x.dang_giu}` : ""})</span></td>
      <td class="whitespace-nowrap">${ngayVN(x.het_han)}</td>
      <td>${x.nguoi_dung.length ? x.nguoi_dung.map((n) => `<button type="button" class="ten-nd lien-ket" data-uid="${n.id}">${esc(n.ten || n.email)}</button>
        <small class="ghi-chu-nho">${n.so_bai} bài</small>`).join("<br>") : '<span class="ghi-chu-nho">chưa ai</span>'}
        <small class="ghi-chu-nho"> · ${x.nguoi_dung.length}/${x.so_tai_khoan}</small></td>
      <td>${huyHieu(x.ten_trang_thai, MAU_TT_MA[x.trang_thai] || "")}</td><td style="overflow-wrap:anywhere">${esc(x.ghi_chu || "—")}</td>
      <td class="whitespace-nowrap">${x.trang_thai === "thu_hoi" ? "" : `<button type="button" class="btn btn-sm" data-them-luot="${x.id}">+ lượt</button>
        <button type="button" class="btn btn-sm btn-nguy-hiem" data-thu-hoi="${x.id}">Thu hồi</button>`}</td></tr>`).join("")
    || '<tr><td colspan="8" class="ghi-chu-nho">Chưa có mã nào.</td></tr>';
}

async function taoMaClaude(e) {
  e.preventDefault();
  const nut = e.target.querySelector("button[type=submit]");
  nut.disabled = true;
  try {
    const d = await HoatDong.gui("/admin/api/ma-claude", {
      so_luot: Number($("#ma-luot").value), het_han: $("#ma-het-han").value, so_tai_khoan: Number($("#ma-tai-khoan").value),
      ghi_chu: $("#ma-ghi-chu").value.trim(),
    });
    const hop = $("#ad-ma-moi");
    hop.hidden = false;
    hop.innerHTML = `<p class="nhan-mono">Mã mới – chỉ hiện một lần, hãy chép gửi người dùng ngay</p>
      <div class="flex flex-wrap items-center gap-3 mt-2"><code id="ma-goc" class="ma-goc">${esc(d.ma_goc)}</code>
        <button type="button" class="btn btn-sm" id="chep-ma">Chép</button></div>
      <p class="ghi-chu-nho mt-2">${d.tong_luot} lượt · hết hạn ${ngayVN(d.het_han)} · ${d.so_tai_khoan} tài khoản${d.ghi_chu ? " · " + esc(d.ghi_chu) : ""}</p>`;
    $("#chep-ma").onclick = async () => {
      try { await navigator.clipboard.writeText(d.ma_goc); toast("Đã chép mã.", "ok", 2500); } catch { toast("Không chép được – hãy bôi đen rồi chép.", "loi"); }
    };
    $("#ma-ghi-chu").value = "";
    await taiMaClaude();
  } catch (err) {
    toast("⚠ " + esc(err.message), "loi", 8000);
  } finally {
    nut.disabled = false;
  }
}

async function thaoTacMa(e) {
  const them = e.target.closest("[data-them-luot]");
  const thuHoi = e.target.closest("[data-thu-hoi]");
  try {
    if (them) {
      const so = Number(prompt("Cộng thêm bao nhiêu lượt?", "10"));
      if (!so || so < 1) return;
      await HoatDong.gui(`/admin/api/ma-claude/${them.dataset.themLuot}/them-luot`, { so });
      toast(`Đã cộng ${so} lượt.`, "ok", 3000);
    } else if (thuHoi) {
      if (!confirm(`THU HỒI mã #${thuHoi.dataset.thuHoi}? Không ai dùng được Claude bằng mã này nữa.`)) return;
      await HoatDong.gui(`/admin/api/ma-claude/${thuHoi.dataset.thuHoi}/thu-hoi`, {});
      toast("Đã thu hồi mã.", "ok", 3000);
    } else return;
    await taiMaClaude();
  } catch (err) {
    toast("⚠ " + esc(err.message), "loi", 8000);
  }
}

// ------------------------------------------------------------ Key Claude dùng chung (không bao giờ nhận / hiện key)
const MAU_TT_KEY = { da_mo: "xanh", key_cu: "vang", bi_thu_hoi: "do", tat: "vang", chua_mo: "" };

async function taiKeyClaude() {
  const d = await HoatDong.lay("/admin/api/key-claude");
  const ch = d.cau_hinh;
  tomTat("key-claude", ch.bat ? `${d.nguoi_dung.filter((x) => x.trang_thai === "da_mo").length} người đang mở Claude · tối đa ${ch.gioi_han_ngay} bài/ngày`
    : "Đang tắt (chưa đặt CLAUDE_ACCESS_KEY)");
  $("#ad-key-cau-hinh").innerHTML = ch.bat
    ? `Đang <b>bật</b> (CLAUDE_ACCESS_KEY trong .env) · tối đa <b>${ch.gioi_han_ngay}</b> bài Claude/người/ngày. Đổi key trong .env rồi khởi động lại app →
       mọi tài khoản kích hoạt bằng key cũ tự mất quyền. Admin luôn dùng được Claude.`
    : "Đang <b>tắt</b>: chưa đặt CLAUDE_ACCESS_KEY trong .env (nháy đúp cau-hinh-key-claude.command) – không ai mở được Claude bằng key.";
  $("#ad-ds-key").innerHTML = d.nguoi_dung.map((x) => `<tr>
      <td class="so-hd">${x.id}</td>
      <td><button type="button" class="ten-nd lien-ket" data-uid="${x.id}">${esc(x.ten || x.email)}</button>
        <br><small class="ghi-chu-nho" style="overflow-wrap:anywhere">${esc(x.email)}</small></td>
      <td>${huyHieu(x.ten_trang_thai, MAU_TT_KEY[x.trang_thai] || "")}${x.thu_hoi_luc ? `<br><small class="ghi-chu-nho">${esc(x.thu_hoi_luc)}</small>` : ""}</td>
      <td class="whitespace-nowrap">${esc(x.kich_hoat_luc || "—")}</td>
      <td class="so-hd">${x.hom_nay}/${x.gioi_han}</td><td class="so-hd">${x.tong}</td>
      <td class="whitespace-nowrap">${x.sai_30_ngay ? `${x.sai_24h} / ${x.sai_30_ngay}` : "—"}${x.khoa_nhap_giay ? ` ${huyHieu(`khoá nhập ${Math.ceil(x.khoa_nhap_giay / 60)} phút`, "do")}` : ""}</td>
      <td class="whitespace-nowrap">${x.trang_thai === "bi_thu_hoi"
        ? `<button type="button" class="btn btn-sm" data-mo-lai-key="${x.id}">Mở lại</button>`
        : `<button type="button" class="btn btn-sm btn-nguy-hiem" data-thu-hoi-key="${x.id}">Thu hồi</button>`}</td></tr>`).join("")
    || '<tr><td colspan="8" class="ghi-chu-nho">Chưa ai mở Claude bằng key.</td></tr>';
}

async function thaoTacKey(e) {
  const thuHoi = e.target.closest("[data-thu-hoi-key]");
  const moLai = e.target.closest("[data-mo-lai-key]");
  try {
    if (thuHoi) {
      if (!confirm(`THU HỒI quyền Claude (Key Claude) của tài khoản #${thuHoi.dataset.thuHoiKey}? Người này không tự kích hoạt lại được.`)) return;
      await HoatDong.gui(`/admin/api/key-claude/${thuHoi.dataset.thuHoiKey}/thu-hoi`, {});
      toast("Đã thu hồi quyền Claude.", "ok", 3000);
    } else if (moLai) {
      await HoatDong.gui(`/admin/api/key-claude/${moLai.dataset.moLaiKey}/mo-lai`, {});
      toast("Đã mở lại – người này nhập lại Key Claude được.", "ok", 3000);
    } else return;
    await taiKeyClaude();
  } catch (err) {
    toast("⚠ " + esc(err.message), "loi", 8000);
  }
}

// ------------------------------------------------------------ Thứ tự AI (kéo thả / ↑ ↓) cho từng hệ thống
const LOGO_AI = { claude: ["claude.svg", "#D97757"], gemini: ["googlegemini.svg", "#8E75B2"], openai: ["openai.svg", "#10A37F"] };
const VAI_AI = ["Chính", "Dự phòng 1", "Dự phòng 2"];
function veThuTuAI(d) {
  $("#tt-timeout").textContent = d.timeout_giay;
  const ncc = d.nha_cung_cap, thieu = Object.values(ncc).filter((x) => !x.co_key).map((x) => x.ten);
  tomTat("thu-tu-ai", `Giải chuỗi: ${d.he_thong.giai_chuoi.thu_tu.map((x) => ncc[x].ten).join(" → ")}${thieu.length ? ` · ${thieu.join(", ")} chưa có key` : ""}`,
    thieu.length > 0);
  const vai = (k, h, x) => { // vai trò tính trong các AI hệ thống này thật sự dùng (vd 3D không dùng Claude → "bỏ qua")
    const bo = (d.khong_dung || {})[k] || [], i = h.thu_tu.filter((t) => !bo.includes(t)).indexOf(x);
    return i < 0 ? '<span class="tt-vai bo-qua" title="Hệ thống này không dùng AI này – luôn bỏ qua">Bỏ qua</span>'
      : `<span class="tt-vai ${i ? "" : "chinh"}">${VAI_AI[i]}</span>`;
  };
  $("#tt-luoi").innerHTML = Object.entries(d.he_thong).map(([k, h]) => `<div class="cot-thu-tu" data-he-thong="${k}">
      <div class="flex items-center justify-between gap-2"><b>${esc(h.ten)}</b>${h.da_doi
        ? `<button type="button" class="btn btn-sm" data-mac-dinh-ai="${k}">Về mặc định</button>` : '<span class="ghi-chu-nho">mặc định</span>'}</div>
      <ol class="ds-thu-tu">${h.thu_tu.map((x, i) => `<li draggable="true" data-ai="${x}">
        ${vai(k, h, x)}
        <span class="logo-ai" style="--mau-hang:${LOGO_AI[x][1]};--logo:url('/static/logos/${LOGO_AI[x][0]}')" aria-hidden="true"><i></i></span>
        <span class="tt-ten"><b>${esc(ncc[x].ten)}</b><small>${esc(ncc[x].mo_hinh)}</small>${ncc[x].co_key ? "" : `<small class="chu-loi">${esc(ncc[x].thieu)}</small>`}</span>
        <span class="tt-nut"><button type="button" class="btn btn-sm" data-dich="-1" ${i === 0 ? "disabled" : ""} aria-label="Đưa ${esc(ncc[x].ten)} lên">↑</button>
          <button type="button" class="btn btn-sm" data-dich="1" ${i === h.thu_tu.length - 1 ? "disabled" : ""} aria-label="Đưa ${esc(ncc[x].ten)} xuống">↓</button></span>
      </li>`).join("")}</ol></div>`).join("");
  $("#tt-du-phong").innerHTML = d.du_phong.length ? `<div class="cuon-ngang"><table class="bang-nd"><thead><tr><th>Lúc</th><th>Hệ thống</th><th>AI lỗi</th>
      <th>Lý do</th><th>Chuyển sang</th></tr></thead><tbody>${d.du_phong.map((x) => `<tr><td class="whitespace-nowrap">${HoatDong.ngayChu(x.luc)}</td>
      <td>${esc(x.he_thong)}</td><td>${huyHieu(x.tu, "do")}</td><td class="ghi-chu-nho">${esc(x.ly_do)}</td><td>${huyHieu(x.sang, "xanh")}</td></tr>`).join("")}
      </tbody></table></div>` : '<p class="ghi-chu-nho">Chưa có lần nào phải chuyển AI dự phòng.</p>';
}
async function taiThuTuAI() { veThuTuAI(await HoatDong.lay("/admin/api/thu-tu-ai")); }
async function luuThuTuAI(heThong, thuTu) {
  try {
    veThuTuAI(await HoatDong.gui("/admin/api/thu-tu-ai", { he_thong: heThong, thu_tu: thuTu }));
    toast(thuTu ? "Đã lưu thứ tự AI." : "Đã về thứ tự mặc định.", "ok", 2200);
  } catch (err) { toast("⚠ " + esc(err.message), "loi", 8000); taiThuTuAI().catch(() => {}); }
}
const thuTuCua = (ol) => [...ol.querySelectorAll("li[data-ai]")].map((li) => li.dataset.ai);
function ganThuTuAI() {
  const luoi = $("#tt-luoi");
  let dangKeo = null;
  luoi.addEventListener("click", (e) => {
    const md = e.target.closest("[data-mac-dinh-ai]"), nut = e.target.closest("[data-dich]");
    if (md) luuThuTuAI(md.dataset.macDinhAi, null);
    if (!nut) return;
    const li = nut.closest("li"), ol = li.parentElement, ds = thuTuCua(ol), i = ds.indexOf(li.dataset.ai), j = i + Number(nut.dataset.dich);
    if (j < 0 || j >= ds.length) return;
    [ds[i], ds[j]] = [ds[j], ds[i]];
    luuThuTuAI(li.closest("[data-he-thong]").dataset.heThong, ds);
  });
  luoi.addEventListener("dragstart", (e) => {
    dangKeo = e.target.closest("li[data-ai]");
    if (!dangKeo) return;
    dangKeo.classList.add("dang-keo-tha");
    e.dataTransfer.effectAllowed = "move";
    e.dataTransfer.setData("text/plain", dangKeo.dataset.ai);
  });
  luoi.addEventListener("dragover", (e) => {
    const li = e.target.closest("li[data-ai]");
    if (!dangKeo || !li || li === dangKeo || li.parentElement !== dangKeo.parentElement) return;
    e.preventDefault();
    const r = li.getBoundingClientRect();
    li.parentElement.insertBefore(dangKeo, e.clientY < r.top + r.height / 2 ? li : li.nextSibling); // xem trước vị trí mới
  });
  luoi.addEventListener("dragend", () => {
    if (!dangKeo) return;
    const ol = dangKeo.parentElement, heThong = dangKeo.closest("[data-he-thong]").dataset.heThong;
    dangKeo.classList.remove("dang-keo-tha");
    dangKeo = null;
    luuThuTuAI(heThong, thuTuCua(ol));
  });
}

// ------------------------------------------------------------ Mã giao diện (+K): tạo (mã gốc hiện 1 lần), danh sách, thu hồi
const MAU_TT_GD = { con_hieu_luc: "xanh", het_han: "vang", het_luot: "vang", thu_hoi: "do" };
function veMaGiaoDien(d) {
  const sel = $("#gd-theme");
  if (!sel.options.length) sel.innerHTML = Object.entries(d.themes).map(([k, v]) => `<option value="${esc(k)}">${esc(v)}</option>`).join("");
  const con = d.ma.filter((x) => x.trang_thai === "con_hieu_luc").length;
  tomTat("ma-giao-dien", d.ma.length ? `${con} mã còn hiệu lực / ${d.ma.length} · ${so(d.ma.reduce((t, x) => t + x.da_dung, 0))} lượt đã dùng` : "Chưa có mã nào");
  $("#gd-ds").innerHTML = d.ma.map((x) => `<tr>
      <td class="so-hd">${x.id}</td><td class="font-mono whitespace-nowrap">${x.ma
        ? `<b>${esc(x.ma)}</b> <button type="button" class="btn btn-sm ml-1" data-chep-gd="${esc(x.ma)}">Chép</button>`
        : `${esc(x.goi_y)} <small class="ghi-chu-nho">(không xem lại được)</small>`}</td><td>${esc(x.ten_theme)}</td>
      <td class="so-hd">${x.da_dung}${x.toi_da ? `/${x.toi_da}` : ""}</td><td class="whitespace-nowrap">${x.het_han ? ngayVN(x.het_han) : "—"}</td>
      <td>${huyHieu(x.ten_trang_thai, MAU_TT_GD[x.trang_thai] || "")}</td><td>${esc(x.ghi_chu || "—")}</td>
      <td class="ghi-chu-nho">${esc(x.admin)}<br>${esc(x.tao_luc)}</td>
      <td>${x.trang_thai === "thu_hoi" ? "" : `<button type="button" class="btn btn-sm btn-nguy-hiem" data-thu-hoi-gd="${x.id}">Thu hồi</button>`}</td></tr>`).join("")
    || '<tr><td colspan="9" class="ghi-chu-nho">Chưa có mã nào.</td></tr>';
}
async function taiMaGiaoDien() { veMaGiaoDien(await HoatDong.lay("/admin/api/ma-giao-dien")); }

// ------------------------------------------------------------ Lộ trình học Sinh: chương → bài (checkbox), thanh tiến độ % (toàn bộ + từng chương)
function veLoTrinh(d) {
  $("#lt-ten").textContent = d.ten;
  $("#lt-dem").textContent = `Đã hoàn thành ${d.xong}/${d.tong} bài`;
  $("#lt-phan-tram").textContent = `${d.phan_tram}%`;
  const thanh = $("#lt-thanh-tong");
  thanh.setAttribute("aria-valuenow", String(d.phan_tram));
  thanh.firstElementChild.style.width = `${d.phan_tram}%`;
  $("#lt-ds").innerHTML = d.chuong.map((ch) => {
    const pt = ch.tong ? Math.round((100 * ch.xong) / ch.tong) : 0;
    const bai = ch.bai.map((b) => `<li class="${b.xong ? "xong" : ""}">
        <label><input type="checkbox" data-lt-bai="${esc(b.id)}" ${b.xong ? "checked" : ""}><span>${esc(b.ten)}</span></label>
        ${b.co_quan.length ? `<span class="lt-co-quan">${b.co_quan.map((c) => `<a href="/sinh-hoc?co_quan=${encodeURIComponent(c.id)}" target="_blank" rel="noopener" title="Mở Phòng Lab 3D">${esc(c.icon)} ${esc(c.ten)}</a>`).join("")}</span>` : ""}
        ${b.xong && b.xong_luc ? `<small class="lt-ngay" title="Ngày đánh dấu hoàn thành">✓ ${new Date(b.xong_luc * 1000).toLocaleDateString("vi-VN")}</small>` : ""}
      </li>`).join("");
    return `<section class="lt-chuong${ch.tong && ch.xong === ch.tong ? " xong" : ""}">
      <div class="lt-chuong-dau"><h3>${esc(ch.ten)}</h3><span class="lt-chuong-dem">${ch.xong}/${ch.tong}${ch.tong && ch.xong === ch.tong ? " ✓" : ""}</span></div>
      <div class="lt-thanh lt-thanh-nho" aria-hidden="true"><span style="width:${pt}%"></span></div>
      <ul class="lt-bai">${bai}</ul>
    </section>`;
  }).join("");
  tomTat("lo-trinh", `${d.xong}/${d.tong} bài · ${d.phan_tram}%`);
}

async function taiLoTrinh() { veLoTrinh(await HoatDong.lay("/admin/api/lo-trinh")); }

// ------------------------------------------------------------ Phòng thí nghiệm Hoá ảo: công tắc bản Unity 3D + trạng thái bản build
function vePhongThiNghiem(d) {
  $("#ptn-unity-bat").checked = d.unity_bat;
  const mb = d.dung_luong_build / 1048576;
  $("#ptn-unity-build").textContent = d.unity_san_sang ? "Đã có – sẵn sàng" : "Chưa có bản build";
  $("#ptn-unity-dung-luong").textContent = d.unity_san_sang ? `${mb.toFixed(1).replace(".", ",")} MB` : "–";
  const cb = $("#ptn-unity-canh-bao");
  cb.hidden = d.unity_san_sang || !d.unity_bat;
  cb.textContent = "⚠ Người dùng sẽ chưa thấy nút vì chưa có bản build. Build theo unity/README.md, đặt vào frontend/unity-lab/Build/.";
  tomTat("phong-thi-nghiem", `Unity: ${d.unity_bat ? "Bật" : "Tắt"} · Build: ${d.unity_san_sang ? "có" : "chưa"}`, d.unity_bat && !d.unity_san_sang);
}

async function taiPhongThiNghiem() { vePhongThiNghiem(await HoatDong.lay("/admin/api/phong-thi-nghiem")); }

async function danhDauBai(e) {
  const o = e.target.closest("[data-lt-bai]");
  if (!o) return;
  const id = o.dataset.ltBai;
  o.disabled = true;
  try {
    veLoTrinh(await HoatDong.gui(`/admin/api/lo-trinh/${encodeURIComponent(id)}`, { xong: o.checked }));
    document.querySelector(`[data-lt-bai="${CSS.escape(id)}"]`)?.focus(); // vẽ lại danh sách → giữ con trỏ bàn phím đúng ô vừa bấm
  } catch (err) {
    o.checked = !o.checked;
    o.disabled = false;
    toast("⚠ " + esc(err.message), "loi", 8000);
  }
}

// ------------------------------------------------------------ Email rác: bị chặn, nghi ngờ, danh sách chặn / cho phép
const MAU_LY_DO = { email_tam: "do", ten_mien_bi_chan: "do", khong_mx: "vang", khong_ton_tai: "vang", honeypot: "do", captcha: "vang",
  qua_nhieu_lan_thu: "lam", qua_gioi_han_ngay: "lam", dinh_dang: "", ten_mien_google: "do" };

async function taiEmailRac() {
  const q = new URLSearchParams({ ly_do: $("#er-loc-ly-do").value, tim: $("#er-tim").value.trim() });
  veEmailRac(await HoatDong.lay(`/admin/api/email-rac?${q}`));
}

function veEmailRac(d) {
  const ds = d.danh_sach;
  tomTat("email-rac", `7 ngày: ${so(d.thong_ke.tong)} lần chặn${d.nghi_ngo.length ? ` · ${d.nghi_ngo.length} email nghi ngờ chờ duyệt` : ""}`,
    d.nghi_ngo.length > 0);
  $("#er-danh-sach").innerHTML = `Danh sách email tạm thời: <b>${so(ds.so_ten_mien)}</b> tên miền (mở, từ
    <a class="lien-ket" href="${esc(ds.nguon)}" target="_blank" rel="noopener">disposable-email-domains</a>) ·
    ${ds.cap_nhat_luc ? `tải lần cuối ${esc(ds.cap_nhat_luc)}` : "chưa tải bản mới (đang dùng bản đi kèm)"} · tự tải lại mỗi ${ds.cap_nhat_sau_ngay} ngày.
    Kiểm tra thêm: bản ghi MX, mẫu tempmail.*, ô bẫy bot, tối đa 3 lần thử đăng ký/IP/giờ.`;
  const sel = $("#er-loc-ly-do");
  if (sel.options.length === 1) sel.insertAdjacentHTML("beforeend", Object.entries(d.ly_do).map(([k, v]) => `<option value="${k}">${esc(v)}</option>`).join(""));
  const tk = d.thong_ke;
  $("#er-thong-ke").innerHTML = `<p class="text-sm">7 ngày qua: <b>${so(tk.tong)}</b> lần bị chặn${tk.theo_ly_do.length ? " · " : ""}
    ${tk.theo_ly_do.map((x) => `${huyHieu(x.ten, MAU_LY_DO[x.ly_do] || "")} ${so(x.so)}`).join(" &nbsp;")}</p>`;
  $("#er-nghi-ngo").innerHTML = d.nghi_ngo.length ? `<div class="cuon-ngang"><table class="bang-nd"><thead><tr><th class="so-hd">ID</th><th>Tài khoản</th>
      <th>Lý do</th><th>Ngày tạo</th><th>IP đăng ký</th><th></th></tr></thead><tbody>${d.nghi_ngo.map((x) => `<tr>
      <td class="so-hd">${x.id}</td><td><button type="button" class="ten-nd lien-ket" data-uid="${x.id}">${esc(x.ten || x.email)}</button>
        <br><small class="ghi-chu-nho font-mono">${esc(x.email)}</small>${x.bi_khoa ? ` ${huyHieu("đã khoá", "do")}` : ""}</td>
      <td>${esc(x.ly_do)}</td><td class="whitespace-nowrap">${HoatDong.ngayChu(x.tao_luc)}</td><td class="o-ip">${nutIp(null, x.ip_dang_ky, true) || "—"}</td>
      <td class="whitespace-nowrap"><button type="button" class="btn btn-sm" data-duyet="${x.id}">Cho qua</button>
        <button type="button" class="btn btn-sm btn-nguy-hiem" data-duyet-khoa="${x.id}">Khoá</button></td></tr>`).join("")}</tbody></table></div>`
    : '<p class="ghi-chu-nho">Không có tài khoản nào đang chờ duyệt.</p>';
  $("#er-quy-tac").innerHTML = d.quy_tac.length ? `<div class="cuon-ngang"><table class="bang-nd"><thead><tr><th>Tên miền</th><th>Loại</th><th>Ghi chú</th>
      <th>Thêm bởi</th><th>Lúc</th><th></th></tr></thead><tbody>${d.quy_tac.map((x) => `<tr><td class="font-mono">${esc(x.ten_mien)}</td>
      <td>${x.loai === "chan" ? huyHieu("chặn", "do") : huyHieu("cho phép", "xanh")}</td><td>${esc(x.ghi_chu || "—")}</td><td>${esc(x.admin)}</td>
      <td class="whitespace-nowrap">${esc(x.luc)}</td><td><button type="button" class="btn btn-sm" data-xoa-quy-tac="${esc(x.ten_mien)}">Bỏ</button></td></tr>`).join("")}
      </tbody></table></div>` : '<p class="ghi-chu-nho">Chưa thêm tên miền nào.</p>';
  $("#er-chan").innerHTML = d.chan.length ? `<div class="cuon-ngang"><table class="bang-nd"><thead><tr><th>Lúc</th><th>Email</th><th>IP</th><th>Lý do</th>
      <th>Chi tiết</th></tr></thead><tbody>${d.chan.map((x) => `<tr><td class="whitespace-nowrap">${HoatDong.ngayChu(x.luc)}</td>
      <td class="font-mono" style="overflow-wrap:anywhere">${esc(x.email || "—")}</td><td class="o-ip">${nutIp(null, x.ip, true) || "—"}</td>
      <td>${huyHieu(x.ten_ly_do, MAU_LY_DO[x.ly_do] || "")}${x.hanh_dong === "google" ? ' <small class="ghi-chu-nho">(Google)</small>' : ""}</td>
      <td class="ghi-chu-nho">${esc(x.chi_tiet || "")}</td></tr>`).join("")}</tbody></table></div>`
    : '<p class="ghi-chu-nho">Chưa có lần nào bị chặn.</p>';
}

async function guiEmailRac(url, body, thanhCong) {
  try {
    veEmailRac(await HoatDong.gui(url, body));
    toast(thanhCong, "ok", 3000);
    return true;
  } catch (err) {
    toast("⚠ " + esc(err.message), "loi", 8000);
    return false;
  }
}

// ------------------------------------------------------------ Claude API máy chủ (Admin API Anthropic) + nạp tiền
const usd = (v, so = 2) => (v == null ? "—" : `$${Number(v).toFixed(so)}`);
const soTok = (n) => Number(n || 0).toLocaleString("vi-VN");

function veClaudeApi(d) {
  $("#cl-nguong").value = d.nguong;
  tomTat("claude-api", !d.co_admin_key ? `Chưa có Admin key · nội bộ 30 ngày ${usd((d.noi_bo || {}).tong_usd, 3)}`
    : d.loi ? "⚠ Admin API báo lỗi" : d.so_du_uoc_tinh != null ? `Số dư ước tính ${usd(d.so_du_uoc_tinh)} · hôm nay ${usd(d.hom_nay, 3)}`
      : `Hôm nay ${usd(d.hom_nay, 3)} · 30 ngày ${usd(d.ngay_30, 2)}`, !!(d.loi || d.canh_bao));
  const noiBo = d.noi_bo || { theo_mo_hinh: [] };
  const bangNoiBo = `<p class="nhan-mono mt-5">Nhật ký nội bộ của app (Claude bằng key máy chủ, 30 ngày – ước tính theo bảng giá)</p>
    <div class="cuon-ngang mt-2"><table class="bang-nd"><thead><tr><th>Mô hình</th><th class="so-hd">Lượt gọi</th><th class="so-hd">Lỗi</th><th class="so-hd">Token vào</th><th class="so-hd">Token ra</th><th class="so-hd">Ước tính</th></tr></thead><tbody>
    ${noiBo.theo_mo_hinh.map((r) => `<tr><td class="font-mono">${esc(r.model || "—")}</td><td class="so-hd">${r.so_lan}</td><td class="so-hd">${r.loi || 0}</td>
      <td class="so-hd">${soTok(r.tokens_in)}</td><td class="so-hd">${soTok(r.tokens_out)}</td><td class="so-hd">${usd(r.usd, 4)}</td></tr>`).join("")
      || '<tr><td colspan="6" class="ghi-chu-nho">Chưa có lượt gọi Claude bằng key máy chủ trong 30 ngày.</td></tr>'}</tbody></table></div>`;
  if (!d.co_admin_key) {
    $("#ad-claude-api").innerHTML = `<div class="hop-vang"><p><b>Chưa có ANTHROPIC_ADMIN_KEY</b> – mới chỉ hiện số liệu nhật ký nội bộ. Cách tạo:</p>
      <ol class="ds-claude mt-2">${d.huong_dan.map((x) => `<li>${esc(x)}</li>`).join("")}</ol></div>${bangNoiBo}`;
    return;
  }
  if (d.loi) {
    $("#ad-claude-api").innerHTML = `<div class="hop-canh-bao">⚠ ${esc(d.loi)}</div>${bangNoiBo}`;
    return;
  }
  const max = Math.max(0.01, ...d.bieu_do.map((x) => x.usd));
  const W = 560, H = 170, T = 10, B = 24, L = 40, R = 6, o = (W - L - R) / d.bieu_do.length;
  const y = (v) => T + (H - T - B) * (1 - v / max);
  let svg = [0, 0.5, 1].map((k) => `<line class="luoi" x1="${L}" x2="${W - R}" y1="${y(max * k)}" y2="${y(max * k)}"/>
    <text x="${L - 5}" y="${y(max * k) + 3}" text-anchor="end">$${(max * k).toFixed(max * k < 1 ? 2 : 1)}</text>`).join("");
  d.bieu_do.forEach((x, i) => {
    const cx = L + i * o + o / 2, nhan = `${x.ngay.slice(8, 10)}/${x.ngay.slice(5, 7)}`;
    svg += `<rect class="cot-dn" x="${cx - o * 0.32}" y="${y(x.usd)}" width="${o * 0.64}" height="${y(0) - y(x.usd)}" rx="1.5"><title>${nhan}: ${usd(x.usd, 4)}</title></rect>`;
    if (i % 5 === 4 || i === d.bieu_do.length - 1) svg += `<text x="${cx}" y="${H - 7}" text-anchor="middle">${nhan}</text>`;
  });
  const noiBoUsd = noiBo.tong_usd || 0;
  $("#ad-claude-api").innerHTML = `
    ${d.canh_bao ? `<div class="hop-canh-bao mb-4">⚠ Số dư ước tính <b>${usd(d.so_du_uoc_tinh)}</b> dưới ngưỡng ${usd(d.nguong)} – nạp thêm tín dụng tại platform.claude.com.</div>` : ""}
    <div class="luoi-o-so">
      <div class="o-so${d.canh_bao ? " do" : ""}"><span class="nhan-mono">Số dư (ước tính)</span><b>${d.so_du_uoc_tinh == null ? "—" : usd(d.so_du_uoc_tinh)}</b>
        <small>${d.tong_nap ? `đã nạp ${usd(d.tong_nap)} − đã dùng ${usd(d.da_dung_tu_lan_nap_dau)}` : "chưa ghi lần nạp nào"}</small></div>
      <div class="o-so"><span class="nhan-mono">Hôm nay (UTC)</span><b>${usd(d.hom_nay, 3)}</b></div>
      <div class="o-so"><span class="nhan-mono">7 ngày</span><b>${usd(d.ngay_7, 3)}</b></div>
      <div class="o-so"><span class="nhan-mono">30 ngày</span><b>${usd(d.ngay_30, 3)}</b><small>nhật ký app ước tính ${usd(noiBoUsd, 3)}</small></div>
    </div>
    <svg class="bieu-do-hd mt-4" viewBox="0 0 ${W} ${H}" role="img" aria-label="Chi phí Claude mỗi ngày trong 30 ngày">${svg}</svg>
    <p class="nhan-mono mt-4">Token theo mô hình (Anthropic, 30 ngày)</p>
    <div class="cuon-ngang mt-2"><table class="bang-nd"><thead><tr><th>Mô hình</th><th class="so-hd">Token vào</th><th class="so-hd">Đọc cache</th><th class="so-hd">Ghi cache</th><th class="so-hd">Token ra</th></tr></thead><tbody>
    ${d.mo_hinh.map((m) => `<tr><td class="font-mono">${esc(m.model)}</td><td class="so-hd">${soTok(m.tokens_in)}</td><td class="so-hd">${soTok(m.cache_doc)}</td>
      <td class="so-hd">${soTok(m.cache_ghi)}</td><td class="so-hd">${soTok(m.tokens_out)}</td></tr>`).join("") || '<tr><td colspan="5" class="ghi-chu-nho">Chưa dùng Claude trong 30 ngày.</td></tr>'}
    </tbody></table></div>${bangNoiBo}
    <p class="ghi-chu-nho mt-2">Cập nhật lúc ${esc(d.cap_nhat_luc)} · tự lấy lại sau ${Math.ceil(d.cache_con_giay / 60)} phút (hoặc bấm Làm mới).</p>`;
}

function veLichSuNap(d) {
  $("#cl-lich-su-nap").innerHTML = `<p class="nhan-mono">Lịch sử nạp · tổng ${usd(d.tong_nap)}</p>
    <div class="cuon-ngang mt-2"><table class="bang-nd"><thead><tr><th>Lúc</th><th class="so-hd">USD</th><th>Ghi chú</th><th>Admin</th><th></th></tr></thead><tbody>
    ${d.nap.map((n) => `<tr><td class="whitespace-nowrap">${esc(n.luc)}</td><td class="so-hd">${usd(n.usd)}</td><td>${esc(n.ghi_chu || "—")}</td>
      <td>${esc(n.admin)}</td><td><button type="button" class="btn btn-sm btn-nguy-hiem" data-xoa-nap="${n.id}">Xoá</button></td></tr>`).join("")
      || '<tr><td colspan="5" class="ghi-chu-nho">Chưa ghi lần nạp nào – nhập số tiền đã nạp ở platform.claude.com để tính số dư.</td></tr>'}</tbody></table></div>`;
}

async function taiClaudeApi(lamMoi = false) {
  const d = await HoatDong.lay(`/admin/api/claude${lamMoi ? "?lam_moi=1" : ""}`);
  veClaudeApi(d);
  veLichSuNap(d);
}

async function guiClaudeApi(url, body, thanhCong) {
  try {
    const d = await HoatDong.gui(url, body);
    veClaudeApi(d);
    veLichSuNap(d);
    toast(thanhCong, "ok", 2500);
  } catch (err) {
    toast("⚠ " + esc(err.message), "loi", 8000);
  }
}

// ------------------------------------------------------------ thông báo trình duyệt (tuỳ chọn)
const KHOA_TB = "chemistry-admin-thong-bao";
const tbBat = () => { try { return localStorage.getItem(KHOA_TB) === "1" && Notification.permission === "granted"; } catch { return false; } };

function veNutThongBao() {
  const nut = $("#ad-thong-bao");
  if (!("Notification" in window)) {
    nut.disabled = true;
    nut.textContent = "🔕 Trình duyệt này không hỗ trợ thông báo";
    return;
  }
  const bat = tbBat();
  nut.setAttribute("aria-pressed", String(bat));
  nut.textContent = `${bat ? "🔔" : "🔕"} Thông báo đăng ký mới: ${bat ? "bật" : "tắt"}`;
}

async function doiThongBao() {
  if (!("Notification" in window)) return;
  if (tbBat()) {
    try { localStorage.setItem(KHOA_TB, "0"); } catch { /* bỏ qua */ }
  } else {
    const quyen = Notification.permission === "granted" ? "granted" : await Notification.requestPermission();
    if (quyen !== "granted") { toast("Trình duyệt chưa cho phép thông báo – hãy cho phép trong cài đặt của trang.", "loi"); return; }
    try { localStorage.setItem(KHOA_TB, "1"); } catch { /* bỏ qua */ }
    toast("Đã bật: trình duyệt sẽ báo khi có người đăng ký mới (khi trang này đang mở).", "ok", 4000);
  }
  veNutThongBao();
}

function thongBaoDangKy(d) {
  toast(`<b>Đăng ký mới:</b> ${esc(d.ten || "")} ${esc(d.email || "")}`, "ok", 6000);
  if (!tbBat()) return;
  try {
    const tb = new Notification("CHEMISTRY · Có người đăng ký mới", { body: `${d.ten || ""} ${d.email || ""}\n${d.thiet_bi || ""}`.trim(), tag: `dk-${d.id}` });
    tb.onclick = () => { window.focus(); HoatDong.moChiTiet(d.user_id); tb.close(); };
  } catch { /* bỏ qua */ }
}

function thongBaoYeuCauKey(d) { // người dùng xin bật "API key Claude riêng"
  toast(`<b>Yêu cầu bật key Claude riêng:</b> ${esc(d.ten || "")} ${esc(d.email || "")} – duyệt ở bảng Người dùng.`, "tin", 9000);
  if (!tbBat()) return;
  try {
    const tb = new Notification("CHEMISTRY · Yêu cầu bật key Claude riêng", { body: `${d.ten || ""} ${d.email || ""}`.trim(), tag: `yc-${d.id}` });
    tb.onclick = () => { window.focus(); HoatDong.moChiTiet(d.user_id); tb.close(); };
  } catch { /* bỏ qua */ }
}

// ------------------------------------------------------------ khởi động
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

const tomTatCaiDat = () => tomTat("cai-dat-chung", `Hoạt ảnh Cài đặt: ${$("#cd-hoat-anh").checked ? "BẬT" : "TẮT"}`);

async function batDau() {
  khoiTaoGiaoDien();
  ganTheThuGon();
  veNutThongBao();
  $("#ad-thong-bao").addEventListener("click", doiThongBao);
  boLoc = HoatDong.taoBoLoc($("#ad-loc"), () => { nhoNhat = null; taiHoatDong().catch((e) => toast(esc(e.message), "loi")); });
  HoatDong.ganMoChiTiet(document.body, () => { taiNguoiDung(); taiOnline(); taiTongQuan(); });
  $("#ad-them").addEventListener("click", () => taiHoatDong(true));
  const ngay30 = new Date(Date.now() + 30 * 86400000);
  $("#ma-het-han").value = `${ngay30.getFullYear()}-${String(ngay30.getMonth() + 1).padStart(2, "0")}-${String(ngay30.getDate()).padStart(2, "0")}`;
  $("#ad-form-ma").addEventListener("submit", taoMaClaude);
  $("#ad-ds-ma").addEventListener("click", thaoTacMa);
  $("#ad-ds-key").addEventListener("click", thaoTacKey);
  $("#lt-ds").addEventListener("change", danhDauBai);
  $("#ptn-unity-bat").addEventListener("change", async (e) => {
    try {
      const d = await HoatDong.gui("/admin/api/phong-thi-nghiem", { unity_bat: e.target.checked });
      vePhongThiNghiem(d);
      toast(d.unity_bat ? "Đã BẬT bản Unity 3D cho mọi người." : "Đã TẮT bản Unity 3D cho mọi người.", "ok", 3000);
    } catch (err) { e.target.checked = !e.target.checked; toast("⚠ " + esc(err.message), "loi", 8000); }
  });
  $("#cd-hoat-anh").addEventListener("change", async (e) => {
    try {
      const d = await HoatDong.gui("/admin/api/cai-dat-chung", { hoat_anh_cai_dat: e.target.checked });
      e.target.checked = d.hoat_anh_cai_dat;
      tomTatCaiDat();
      toast(d.hoat_anh_cai_dat ? "Đã BẬT hoạt ảnh Cài đặt cho mọi người." : "Đã TẮT hoạt ảnh Cài đặt cho mọi người.", "ok", 3000);
    } catch (err) { e.target.checked = !e.target.checked; toast("⚠ " + esc(err.message), "loi", 8000); }
  });
  $("#cl-lam-moi").addEventListener("click", async (e) => {
    e.target.disabled = true;
    try { await taiClaudeApi(true); toast("Đã lấy lại số liệu từ Anthropic.", "ok", 2500); } catch (err) { toast("⚠ " + esc(err.message), "loi", 8000); }
    e.target.disabled = false;
  });
  $("#cl-form-nap").addEventListener("submit", (e) => {
    e.preventDefault();
    const so = Number($("#cl-nap-usd").value);
    if (!(so > 0)) { toast("Nhập số USD đã nạp (lớn hơn 0).", "loi"); return; }
    guiClaudeApi("/admin/api/claude/nap", { usd: so, ghi_chu: $("#cl-nap-ghi-chu").value.trim() }, `Đã ghi lần nạp $${so}.`);
    $("#cl-nap-usd").value = "";
    $("#cl-nap-ghi-chu").value = "";
  });
  $("#cl-form-nguong").addEventListener("submit", (e) => {
    e.preventDefault();
    guiClaudeApi("/admin/api/claude/canh-bao", { usd: Number($("#cl-nguong").value) || 0 }, "Đã lưu ngưỡng cảnh báo.");
  });
  $("#cl-lich-su-nap").addEventListener("click", (e) => {
    const x = e.target.closest("[data-xoa-nap]");
    if (x && confirm("Xoá lần nạp này khỏi lịch sử?")) guiClaudeApi(`/admin/api/claude/nap/${x.dataset.xoaNap}/xoa`, {}, "Đã xoá.");
  });
  document.querySelectorAll('input[name="cp-ngay"]').forEach((o) => o.addEventListener("change", () => taiChiPhi().catch((e) => toast(esc(e.message), "loi"))));
  $("#ad-tim").addEventListener("input", veBang);
  ganThuTuAI();
  // Mã giao diện
  $("#gd-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    const luot = $("#gd-luot").value.trim();
    try {
      const d = await HoatDong.gui("/admin/api/ma-giao-dien", { theme_id: $("#gd-theme").value, ma: $("#gd-ma").value.trim(),
        so_luot: luot ? Number(luot) : null, het_han: $("#gd-han").value, ghi_chu: $("#gd-ghi-chu").value.trim() });
      veMaGiaoDien(d);
      $("#gd-vua-tao").innerHTML = `Đã tạo mã <b class="font-mono" style="font-size:1.05rem">${esc(d.ma_goc)}</b> – đưa mã này cho người cần dùng
        (luôn xem lại / chép được trong bảng bên dưới)
        <button type="button" class="btn btn-sm ml-2" data-chep-gd="${esc(d.ma_goc)}">Chép</button>`;
      $("#gd-vua-tao").classList.remove("hidden");
      ["#gd-ma", "#gd-luot", "#gd-han", "#gd-ghi-chu"].forEach((id) => { $(id).value = ""; });
    } catch (err) { toast("⚠ " + esc(err.message), "loi", 8000); }
  });
  $("#khoi-ma-giao-dien").addEventListener("click", async (e) => {
    const chep = e.target.closest("[data-chep-gd]"), thu = e.target.closest("[data-thu-hoi-gd]");
    if (chep) { try { await navigator.clipboard.writeText(chep.dataset.chepGd); toast("Đã chép mã.", "ok", 2000); } catch { /* bỏ qua */ } }
    if (thu && confirm(`THU HỒI mã giao diện #${thu.dataset.thuHoiGd}? Người đã áp dụng vẫn giữ giao diện trên máy họ, nhưng không ai nhập được mã này nữa.`)) {
      try { veMaGiaoDien(await HoatDong.gui(`/admin/api/ma-giao-dien/${thu.dataset.thuHoiGd}/thu-hoi`, {})); toast("Đã thu hồi.", "ok", 2500); }
      catch (err) { toast("⚠ " + esc(err.message), "loi", 8000); }
    }
  });
  // Email rác
  $("#er-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    const loai = $("#er-loai").value;
    if (await guiEmailRac("/admin/api/email-rac/quy-tac", { ten_mien: $("#er-ten-mien").value, loai, ghi_chu: $("#er-ghi-chu").value },
      loai === "chan" ? "Đã thêm vào danh sách CHẶN." : "Đã thêm vào danh sách CHO PHÉP.")) {
      $("#er-ten-mien").value = ""; $("#er-ghi-chu").value = "";
    }
  });
  $("#td-email-rac").closest("section").addEventListener("click", (e) => {
    const xoa = e.target.closest("[data-xoa-quy-tac]"), ok = e.target.closest("[data-duyet]"), khoa = e.target.closest("[data-duyet-khoa]");
    if (xoa) guiEmailRac("/admin/api/email-rac/quy-tac/xoa", { ten_mien: xoa.dataset.xoaQuyTac }, "Đã bỏ khỏi danh sách.");
    if (ok) guiEmailRac(`/admin/api/email-rac/nghi-ngo/${ok.dataset.duyet}`, { khoa: false }, "Đã cho qua – bỏ đánh dấu nghi ngờ.");
    if (khoa && confirm(`KHOÁ tài khoản #${khoa.dataset.duyetKhoa}? (mở khoá lại được trong chi tiết người dùng)`)) {
      guiEmailRac(`/admin/api/email-rac/nghi-ngo/${khoa.dataset.duyetKhoa}`, { khoa: true }, "Đã khoá tài khoản.").then(() => taiNguoiDung());
    }
  });
  $("#er-cap-nhat").addEventListener("click", async (e) => {
    e.target.disabled = true;
    await guiEmailRac("/admin/api/email-rac/cap-nhat", {}, "Đã tải bản mới nhất của danh sách email tạm thời.");
    e.target.disabled = false;
  });
  let henTimEr = null;
  $("#er-loc-ly-do").addEventListener("change", () => taiEmailRac().catch((e) => toast(esc(e.message), "loi")));
  $("#er-tim").addEventListener("input", () => { clearTimeout(henTimEr); henTimEr = setTimeout(() => taiEmailRac().catch(() => {}), 300); });
  ["#ad-loc-key-bat", "#ad-loc-key-yc"].forEach((id) => $(id).addEventListener("change", veBang));
  $("#ad-bang").addEventListener("click", (e) => {
    if (e.target.closest(".o-key-rieng")) e.stopPropagation(); // không mở chi tiết khi bấm công tắc
    const em = e.target.closest(".nut-email");
    if (!em) return;
    e.stopPropagation(); // bấm email: chỉ mở / thu gọn email, không mở chi tiết
    const uid = Number(em.dataset.emailUid), mo = !moEmail.has(uid);
    if (mo) moEmail.add(uid); else moEmail.delete(uid);
    em.closest("td").classList.toggle("mo", mo);
    em.setAttribute("aria-expanded", mo);
  });
  $("#ad-bang").addEventListener("change", async (e) => {
    const tg = e.target.closest(".tg-key-rieng");
    if (!tg) return;
    tg.disabled = true;
    try {
      await HoatDong.gui(`/admin/api/nguoi-dung/${tg.dataset.uid}/key-claude-rieng`, { bat: tg.checked });
      toast(tg.checked ? "Đã BẬT key Claude riêng cho người này." : "Đã TẮT – key của họ bị vô hiệu ngay (giữ 30 ngày nếu bật lại).", "ok", 3500);
      await taiNguoiDung();
    } catch (err) { tg.checked = !tg.checked; tg.disabled = false; toast("⚠ " + esc(err.message), "loi", 8000); }
  });
  document.querySelectorAll(".bang-nd th").forEach((th) => th.addEventListener("click", () => {
    sapXep.tang = sapXep.cot === th.dataset.cot ? !sapXep.tang : th.dataset.cot === "ten" || th.dataset.cot === "email";
    sapXep.cot = th.dataset.cot;
    veBang();
  }));
  $("#ad-bang").addEventListener("keydown", (e) => {
    const tr = e.target.closest("tr[data-uid]");
    if (tr && (e.key === "Enter" || e.key === " ")) { e.preventDefault(); HoatDong.moChiTiet(tr.dataset.uid); }
  });
  // ------------------------------------------------------------ NotebookLM – Quản lý nhiều tài khoản Google
  let dsNotebookLMAccounts = [];

  async function taiNotebookLM() {
    try {
      const ds = (await HoatDong.lay("/admin/api/notebooklm/accounts")).tai_khoan || [];
      dsNotebookLMAccounts = ds;
      veBangNotebookLM(ds);
      const soTk = ds.length;
      const soEl = $("#nlm-so-tk");
      if (soEl) soEl.textContent = soTk ? `(${soTk} tài khoản)` : "";
      const loi = ds.filter(a => a.status === "can_dang_nhap_lai" || a.status === "loi");
      if (loi.length > 0) {
        tomTat("notebooklm", `${soTk} tài khoản · ${loi.length} cần đăng nhập lại`, true);
        const cb = $("#nlm-canh-bao-chung");
        if (cb) {
          cb.innerHTML = `<span>⚠ Có ${loi.length} tài khoản Google cần đăng nhập lại: <b>${loi.map(x => esc(x.ten_goi_nho)).join(", ")}</b>. Vui lòng bấm "Sửa" để cập nhật cookie.</span>`;
          cb.classList.remove("hidden");
        }
      } else {
        tomTat("notebooklm", soTk ? `${soTk} tài khoản · Sẵn sàng` : "Chưa kết nối tài khoản nào");
        const cb = $("#nlm-canh-bao-chung");
        if (cb) cb.classList.add("hidden");
      }
    } catch (err) {
      tomTat("notebooklm", "Lỗi tải tài khoản", true);
    }
  }

  function veBangNotebookLM(ds) {
    const tbody = $("#nlm-bang-ds");
    if (!tbody) return;
    if (!ds.length) {
      tbody.innerHTML = `<tr><td colspan="6" class="text-center py-6 text-muc-2">Chưa có tài khoản Google NotebookLM nào. Bấm <b>"+ Thêm tài khoản Google"</b> để kết nối.</td></tr>`;
      return;
    }
    tbody.innerHTML = ds.map(a => {
      const macDinhBadge = a.is_default ? `<span class="px-2 py-0.5 text-xs rounded-full bg-blue-500/20 text-blue-600 dark:text-blue-400 font-semibold ml-2">Mặc định</span>` : "";
      let ttBadge = "";
      if (a.status === "dang_hoat_dong") {
        ttBadge = `<span class="px-2 py-0.5 text-xs rounded-full bg-green-500/20 text-green-600 dark:text-green-400 font-medium">Đã kết nối</span>`;
      } else if (a.status === "can_dang_nhap_lai") {
        ttBadge = `<span class="px-2 py-0.5 text-xs rounded-full bg-amber-500/20 text-amber-600 dark:text-amber-400 font-medium" title="${esc(a.error_msg || '')}">Cần đăng nhập lại</span>`;
      } else {
        ttBadge = `<span class="px-2 py-0.5 text-xs rounded-full bg-red-500/20 text-red-600 dark:text-red-400 font-medium" title="${esc(a.error_msg || '')}">Lỗi kết nối</span>`;
      }
      const avatar = `<div class="w-7 h-7 rounded-full bg-blue-500 text-white font-semibold inline-flex items-center justify-center mr-2 text-xs">${esc((a.ten_goi_nho || 'G')[0].toUpperCase())}</div>`;
      const dongBoCuoi = a.last_sync_at ? new Date(a.last_sync_at * 1000).toLocaleString("vi-VN") : "Chưa đồng bộ";

      return `
        <tr data-acc-id="${a.id}">
          <td>
            <div class="flex items-center">
              ${avatar}
              <span class="font-medium text-muc">${esc(a.ten_goi_nho)}</span>
              ${macDinhBadge}
            </div>
          </td>
          <td class="font-mono text-xs text-muc-2">${esc(a.email || "—")}</td>
          <td>${ttBadge}</td>
          <td class="so-hd font-semibold">${a.so_notebook || 0}</td>
          <td class="text-xs text-muc-2">${dongBoCuoi}</td>
          <td class="text-right whitespace-nowrap">
            <div class="inline-flex items-center gap-1">
              ${!a.is_default ? `<button type="button" class="btn btn-sm" data-nlm-mac-dinh="${a.id}" title="Đặt làm mặc định">★ Mặc định</button>` : ""}
              <button type="button" class="btn btn-sm" data-nlm-kiem-tra="${a.id}" title="Kiểm tra kết nối với Google">✓ Kiểm tra</button>
              <button type="button" class="btn btn-sm" data-nlm-dong-bo="${a.id}" title="Đồng bộ danh sách notebook">↻ Đồng bộ</button>
              <button type="button" class="btn btn-sm" data-nlm-sua="${a.id}" title="Cập nhật cookie / Đổi tên">✏ Sửa</button>
              <button type="button" class="btn btn-sm text-red-600 dark:text-red-400" data-nlm-xoa="${a.id}" title="Xóa tài khoản này">✕</button>
            </div>
          </td>
        </tr>
      `;
    }).join("");
  }

  // Modal thêm/sửa NotebookLM
  const nlmModal = $("#nlm-modal");
  const nlmForm = $("#nlm-form");
  const nlmLoi = $("#nlm-form-loi");
  const nlmCookieLoi = $("#nlm-cookie-loi");

  // Đếm cookie NGAY TRÊN TRÌNH DUYỆT khi dán: chỉ đọc TÊN cookie để báo "Đã nhận diện: N cookie ✓" – không gửi giá trị đi đâu
  const NLM_BAT_BUOC = ["SID", "HSID", "SSID", "APISID", "SAPISID"];
  function nlmDemTenCookie(chu) {
    let s = String(chu || "").trim().replace(/^["'`]+|["'`]+$/g, "");
    const ten = new Set();
    const laGoogle = (d) => { d = String(d || "").replace(/^\./, "").toLowerCase(); return !d || d === "google.com" || d.endsWith(".google.com"); };
    if (/^[\[{]/.test(s)) {
      try {
        let j = JSON.parse(s);
        if (j && !Array.isArray(j) && Array.isArray(j.cookies)) j = j.cookies;
        if (Array.isArray(j)) j.forEach((c) => { if (c && c.name && laGoogle(c.domain ?? c.host)) ten.add(String(c.name)); });
        else if (j && typeof j === "object") Object.keys(j).forEach((k) => ten.add(k));
      } catch { return { so: 0, loi: "JSON chưa đúng định dạng." }; }
    } else {
      const curl = s.match(/(?:-H|--header)\s+(['"])\s*cookie\s*:\s*([\s\S]*?)\1/i) || s.match(/(?:-b|--cookie)\s+(['"])([\s\S]*?)\1/);
      if (curl) s = curl[2];
      s = s.replace(/^\s*cookie\s*:\s*/i, "");
      s.split(/[;\n]/).forEach((p) => {
        const i = p.indexOf("=");
        const t = i > 0 ? p.slice(0, i).trim() : "";
        if (t && /^[A-Za-z0-9!#$%&'*+\-.^_`|~]+$/.test(t) && !/^(path|domain|expires|max-age|secure|httponly|samesite|priority)$/i.test(t)) ten.add(t);
      });
      if (!ten.size && s && !s.includes("=")) return { so: 0, loi: "Bạn mới dán giá trị của một cookie. Cần dán cả chuỗi dạng tên=giá_trị; tên=giá_trị." };
    }
    const thieu = NLM_BAT_BUOC.filter((t) => !ten.has(t));
    if (!ten.has("__Secure-1PSID") && !ten.has("__Secure-3PSID")) thieu.push("__Secure-1PSID hoặc __Secure-3PSID");
    return { so: ten.size, thieu };
  }
  function nlmHienDem() {
    const el = $("#nlm-cookie-dem");
    if (!el) return;
    const chu = $("#nlm-cookie").value;
    if (!chu.trim()) { el.textContent = ""; el.className = "text-xs mt-1"; return; }
    const d = nlmDemTenCookie(chu);
    if (d.loi) { el.textContent = d.loi; el.className = "text-xs mt-1 text-amber-600"; return; }
    el.textContent = `Đã nhận diện: ${d.so} cookie ✓` + (d.thieu.length ? ` · còn thiếu: ${d.thieu.join(", ")}` : "");
    el.className = "text-xs mt-1 " + (d.thieu.length ? "text-amber-600" : "text-green-600");
  }
  $("#nlm-cookie")?.addEventListener("input", () => { nlmHienDem(); nlmCookieLoi?.classList.add("hidden"); });
  function nlmBaoLoi(el, chu) { if (!el) return; el.textContent = chu; el.classList.remove("hidden"); }

  function moModalNotebookLM(acc = null) {
    if (!nlmModal) return;
    nlmForm.reset();
    nlmLoi.classList.add("hidden");
    nlmCookieLoi?.classList.add("hidden");
    nlmHienDem();
    if (acc) {
      $("#nlm-modal-tieu-de").textContent = "Cập nhật tài khoản Google NotebookLM";
      $("#nlm-form-id").value = acc.id;
      $("#nlm-ten-goi-nho").value = acc.ten_goi_nho || "";
      $("#nlm-email").value = acc.email || "";
      $("#nlm-cookie").placeholder = "(Để trống nếu không muốn đổi Cookie)";
      $("#nlm-cookie").required = false;
      $("#nlm-is-default").checked = !!acc.is_default;
    } else {
      $("#nlm-modal-tieu-de").textContent = "Thêm tài khoản Google NotebookLM";
      $("#nlm-form-id").value = "";
      $("#nlm-ten-goi-nho").value = "";
      $("#nlm-cookie").placeholder = "Dán chuỗi cookie (SID, HSID, SSID, OSID...), JSON từ EditThisCookie/Cookie-Editor, hoặc lệnh curl...";
      $("#nlm-cookie").required = true;
      $("#nlm-is-default").checked = dsNotebookLMAccounts.length === 0;
    }
    if (!nlmModal.open) nlmModal.showModal(); // <dialog>: lớp trên cùng, không bị thanh cố định / hiệu ứng khối che
    (acc ? $("#nlm-cookie") : $("#nlm-ten-goi-nho")).focus();
  }

  function dongModalNotebookLM() {
    if (nlmModal?.open) nlmModal.close();
  }
  // đóng bằng bất kỳ cách nào (Hủy, ×, Esc) → xoá cookie đã dán khỏi ô nhập
  nlmModal?.addEventListener("close", () => { $("#nlm-cookie").value = ""; nlmHienDem(); });

  $("#nlm-btn-them")?.addEventListener("click", () => moModalNotebookLM(null));
  $("#nlm-modal-dong")?.addEventListener("click", dongModalNotebookLM);
  $("#nlm-btn-huy")?.addEventListener("click", dongModalNotebookLM);

  nlmForm?.addEventListener("submit", async (e) => {
    e.preventDefault();
    nlmLoi.classList.add("hidden");
    const nutLuu = $("#nlm-btn-luu");
    nutLuu.disabled = true;
    nutLuu.textContent = "Đang kiểm tra kết nối…";

    const id = $("#nlm-form-id").value;
    const ten_goi_nho = $("#nlm-ten-goi-nho").value.trim();
    const raw_cookie = $("#nlm-cookie").value;          // gửi NGUYÊN VĂN – máy chủ tự chuẩn hoá
    const email = $("#nlm-email").value.trim();
    const is_default = $("#nlm-is-default").checked;
    nlmLoi.classList.add("hidden");
    nlmCookieLoi?.classList.add("hidden");

    try {
      if (id) {
        await HoatDong.gui(`/admin/api/notebooklm/accounts/${id}`, { ten_goi_nho, raw_cookie: raw_cookie.trim() ? raw_cookie : undefined, email, is_default }, "PUT");
        toast("Đã cập nhật tài khoản NotebookLM.", "ok");
      } else {
        if (!raw_cookie.trim()) { nlmBaoLoi(nlmCookieLoi, "Hãy dán cookie của tài khoản Google."); return; }
        const acc = await HoatDong.gui("/admin/api/notebooklm/accounts", { ten_goi_nho, raw_cookie, email, is_default });
        toast(acc.status === "dang_hoat_dong" ? "Đã kết nối tài khoản NotebookLM thành công!" : `Đã lưu – ${acc.error_msg || "chưa kết nối được, hãy bấm Kiểm tra sau."}`,
          acc.status === "dang_hoat_dong" ? "ok" : "canh-bao");
      }
      $("#nlm-cookie").value = "";                        // xoá cookie khỏi giao diện ngay sau khi lưu
      nlmHienDem();
      dongModalNotebookLM();
      await taiNotebookLM();
    } catch (err) {
      const veCookie = /COOKIE|cookie/.test(String(err.ma || "")) || (err.thieu || []).length;
      nlmBaoLoi(veCookie ? nlmCookieLoi : nlmLoi, err.message || "Lỗi khi lưu tài khoản.");
    } finally {
      nutLuu.disabled = false;
      nutLuu.textContent = "Lưu & Kết nối";
    }
  });

  // Bảng thao tác NotebookLM
  $("#khoi-notebooklm")?.addEventListener("click", async (e) => {
    const macDinh = e.target.closest("[data-nlm-mac-dinh]");
    const dongBo = e.target.closest("[data-nlm-dong-bo]");
    const kiemTra = e.target.closest("[data-nlm-kiem-tra]");
    const sua = e.target.closest("[data-nlm-sua]");
    const xoa = e.target.closest("[data-nlm-xoa]");

    if (macDinh) {
      const id = macDinh.dataset.nlmMacDinh;
      macDinh.disabled = true;
      try {
        await HoatDong.gui(`/admin/api/notebooklm/accounts/${id}/mac-dinh`, {});
        toast("Đã đổi tài khoản mặc định thành công.", "ok");
        await taiNotebookLM();
      } catch (err) {
        toast("⚠ " + esc(err.message), "loi");
      } finally {
        macDinh.disabled = false;
      }
    } else if (kiemTra) {
      kiemTra.disabled = true;
      kiemTra.textContent = "Đang kiểm tra…";
      try {
        const kq = await HoatDong.gui(`/admin/api/notebooklm/accounts/${kiemTra.dataset.nlmKiemTra}/check`, {});
        toast(kq.ket_noi ? "✓ Đã kết nối" : "⚠ " + esc(kq.message), kq.ket_noi ? "ok" : "loi", kq.ket_noi ? 4000 : 9000);
        await taiNotebookLM();
      } catch (err) {
        toast("⚠ " + esc(err.message), "loi", 9000);
      } finally {
        kiemTra.disabled = false;
        kiemTra.textContent = "✓ Kiểm tra";
      }
    } else if (dongBo) {
      const id = dongBo.dataset.nlmDongBo;
      dongBo.disabled = true;
      dongBo.textContent = "↻ Đang đồng bộ…";
      try {
        const res = await HoatDong.gui(`/admin/api/notebooklm/accounts/${id}/sync`, {});
        toast(`Đã đồng bộ ${res.so_notebook || 0} notebook.`, "ok");
        await taiNotebookLM();
      } catch (err) {
        toast("⚠ Đồng bộ thất bại: " + esc(err.message), "loi", 8000);
      } finally {
        dongBo.disabled = false;
        dongBo.textContent = "↻ Đồng bộ";
      }
    } else if (sua) {
      const id = Number(sua.dataset.nlmSua);
      const acc = dsNotebookLMAccounts.find(a => a.id === id);
      if (acc) moModalNotebookLM(acc);
    } else if (xoa) {
      const id = xoa.dataset.nlmXoa;
      const acc = dsNotebookLMAccounts.find(a => a.id === Number(id));
      const ten = acc ? acc.ten_goi_nho : `#${id}`;
      if (confirm(`Bạn có chắc chắn muốn XÓA tài khoản "${ten}" khỏi hệ thống?`)) {
        xoa.disabled = true;
        try {
          await HoatDong.gui(`/admin/api/notebooklm/accounts/${id}`, {}, "DELETE");
          toast("Đã xóa tài khoản NotebookLM.", "ok");
          await taiNotebookLM();
        } catch (err) {
          toast("⚠ " + esc(err.message), "loi");
          xoa.disabled = false;
        }
      }
    }
  });

  $("#nlm-btn-kiem-tra-tat-ca")?.addEventListener("click", async (e) => {
    const btn = e.target;
    btn.disabled = true;
    btn.textContent = "Đang kiểm tra…";
    try {
      let loiCount = 0;
      for (const acc of dsNotebookLMAccounts) {
        try {
          const kq = await HoatDong.gui(`/admin/api/notebooklm/accounts/${acc.id}/check`, {});
          if (!kq.ket_noi) loiCount++;
        } catch {
          loiCount++;
        }
      }
      toast(loiCount > 0 ? `Kiểm tra xong: có ${loiCount} tài khoản lỗi.` : "Tất cả tài khoản đều hoạt động tốt!", loiCount > 0 ? "canh-bao" : "ok");
      await taiNotebookLM();
    } catch (err) {
      toast("⚠ " + esc(err.message), "loi");
    } finally {
      btn.disabled = false;
      btn.textContent = "Kiểm tra kết nối";
    }
  });

  try {
    await TaiKhoan.tai();
    await Promise.all([taiTongQuan(), taiHoatDong(), taiOnline(), taiNguoiDung(), taiCanhBao(), taiBieuDo(), taiChiPhi(), taiMaClaude(), taiKeyClaude(), taiClaudeApi(), taiEmailRac(), taiMaGiaoDien(), taiThuTuAI(), taiNotebookLM(), taiLoTrinh(), taiPhongThiNghiem(),
      HoatDong.lay("/admin/api/cai-dat-chung").then((d) => { $("#cd-hoat-anh").checked = d.hoat_anh_cai_dat; tomTatCaiDat(); })]);
  } catch (e) {
    if (e.status === 401 || e.status === 403) { location.href = "/dang-nhap?tiep=%2Fadmin"; return; }
    toast("⚠ " + esc(e.message || "Không tải được dữ liệu."), "loi", 10000);
  }
  HoatDong.danhDauDaXem(cuoiId);
  batLuong();
  setInterval(() => { taiOnline(); taiTongQuan(); taiChiPhi().catch(() => {}); taiKeyClaude().catch(() => {}); taiNotebookLM().catch(() => {}); }, 60000); // "online" tự hết sau 5 phút không hoạt động
}

batDau();
