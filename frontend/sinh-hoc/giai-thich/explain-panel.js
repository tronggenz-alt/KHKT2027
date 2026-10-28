// explain-panel.js – ExplainPanel: khung giải thích (tên quy trình, bước i/n, "Điều gì xảy ra", "Vì sao?", "Ghi nhớ", chấm tiến độ,
// nút điều khiển, tốc độ, Tự động chạy, thanh đếm thời gian đọc, thu nhỏ, kéo thả / vuốt trên điện thoại).
// Chỉ hiển thị + phát lệnh người dùng qua onAction(tên, giá_trị); KHÔNG tự điều khiển bước hay 3D.
// Mọi chữ đưa vào bằng textContent (dữ liệu có thể đến từ AI) – không chèn HTML.
import { CAU_HINH } from "./config.js";

const $ = (s) => document.querySelector(s);

export class ExplainPanel {
  constructor({ luuDoc, luuGhi }) {
    this.luuDoc = luuDoc;
    this.luuGhi = luuGhi;
    this.khung = $("#khung-giai-thich");
    this._nghe = [];
    this.laThuNho = false;
    this._ganSuKien();
    this._ganKeoTha();
  }

  get coGiaoDien() { return !!this.khung; }
  onAction(fn) { this._nghe.push(fn); }
  _lenh(ten, gt) { for (const f of this._nghe) f(ten, gt); }

  // ---------- mở / đóng / thu nhỏ ----------
  mo() {
    if (!this.khung) return;
    this.khung.classList.remove("an");
    this.thuNho(!!this.luuDoc("bio-lab-gt-min", false), false);
    const nut = $("#nut-mo-giai-thich");
    nut?.setAttribute("aria-pressed", "true");
    nut?.classList.add("bat");
  }
  dong() {
    if (!this.khung) return;
    this.khung.classList.add("an");
    const nut = $("#nut-mo-giai-thich");
    nut?.setAttribute("aria-pressed", "false");
    nut?.classList.remove("bat");
  }
  get dangMo() { return !!this.khung && !this.khung.classList.contains("an"); }
  thuNho(bat, luu = true) {
    this.laThuNho = !!bat;
    this.khung?.classList.toggle("thu-nho", this.laThuNho);
    if (luu) this.luuGhi("bio-lab-gt-min", this.laThuNho);
  }
  /** Số điểm ảnh khung 3D bị bảng che (bên phải trên máy tính, phía dưới khi là tấm đáy trên điện thoại) → camera bù. */
  vungChe(khung3D) {
    if (!this.dangMo || this.laThuNho) return { phai: 0, duoi: 0 };
    const k = this.khung.getBoundingClientRect(), v = khung3D.getBoundingClientRect();
    if (k.width >= v.width * 0.8) return { phai: 0, duoi: Math.max(0, Math.min(v.height * CAU_HINH.camera.tyLeBangDienThoai, v.bottom - k.top)) };
    if (k.left + k.width / 2 > v.left + v.width / 2) return { phai: Math.max(0, v.right - k.left), duoi: 0 };
    return { phai: 0, duoi: 0 };
  }

  // ---------- nội dung ----------
  datDanhMuc(danhMuc, quyTrinh) {
    const sel = $("#gt-chon-quy-trinh");
    if (!sel) return;
    const lua = Object.values(danhMuc).map((q) => [q.id, `${q.bieu_tuong || "🧭"} ${q.process} (${q.steps.length} bước)`]);
    if (quyTrinh && !danhMuc[quyTrinh.id]) lua.push([quyTrinh.id, `🤖 ${quyTrinh.process} (${quyTrinh.steps.length} bước)`]);
    sel.replaceChildren(...lua.map(([gt, chu]) => Object.assign(document.createElement("option"), { value: gt, textContent: chu })));
    if (quyTrinh) sel.value = quyTrinh.id;
  }

  datQuyTrinh(qt) {
    this._qt = qt;
    $("#gt-ten-quy-trinh").textContent = qt.process;
    const hang = $("#gt-hang-cham");
    hang.replaceChildren(...qt.steps.map((b, i) => {
      const n = document.createElement("button");
      n.type = "button";
      n.className = "gt-cham";
      n.dataset.buoc = String(i);
      n.title = `Bước ${i + 1}: ${b.title}`;
      n.setAttribute("aria-label", `Bước ${i + 1}: ${b.title}`);
      return n;
    }));
    this.anTomTat();
  }

  hienBuoc(i, b) {
    const tong = this._qt?.steps.length || 0;
    const dem = `${i + 1}/${tong}`;
    $("#gt-dem-buoc").textContent = dem;
    $("#gt-mini-chu").textContent = `Bước ${dem}`;
    const luong = $("#gt-nhan-luu");
    luong.textContent = b.luong || `Bước ${i + 1}`;
    luong.style.background = b.color;
    $("#gt-tieu-de-buoc").textContent = `${i + 1}. ${b.title}`;
    let nhanAI = $("#gt-nhan-ai");
    if (!nhanAI) {
      nhanAI = document.createElement("div");
      nhanAI.id = "gt-nhan-ai";
      nhanAI.className = "gt-nhan-ai";
      nhanAI.textContent = "🤖 Do AI tạo từ ảnh, nên đối chiếu SGK";
      const tieuDe = $("#gt-tieu-de-buoc");
      tieuDe?.parentNode?.insertBefore(nhanAI, tieuDe);
    }
    if (nhanAI) nhanAI.hidden = !(b.aiGenerated || this._qt?.aiGenerated);
    $("#gt-loi-giai").textContent = b.what;
    $("#gt-vi-sao").hidden = !b.why;
    $("#gt-vi-sao-chu").textContent = b.why;
    const ghiNhoText = [b.ghi_nho, b.valves ? `Van: ${b.valves}` : "", b.bloodState ? `Máu: ${b.bloodState}` : ""].filter(Boolean).join(" · ");
    $("#gt-ghi-nho").hidden = !ghiNhoText;
    $("#gt-ghi-nho-chu").textContent = ghiNhoText;

    $("#gt-noi-dung").scrollTop = 0;
    document.querySelectorAll("#gt-hang-cham .gt-cham").forEach((c, k) => {
      c.classList.toggle("da-qua", k < i);
      c.classList.toggle("hien-tai", k === i);
      c.setAttribute("aria-current", k === i ? "step" : "false");
    });
    if (i < tong - 1) this.anTomTat();
  }

  hienTomTat(qt) {
    const el = $("#gt-tom-tat");
    el.replaceChildren();
    const b = document.createElement("b");
    b.textContent = "🏁 Tóm tắt: ";
    el.append(b, document.createTextNode(qt.tom_tat || qt.steps.map((s) => s.title).join(" → ")));
    el.classList.remove("an");
  }
  anTomTat() { $("#gt-tom-tat")?.classList.add("an"); }

  capNhatDieuKhien({ state, autoPlay, speed }) {
    const dangChay = state === "playing";
    const ketThuc = state === "finished";
    for (const [id, chu] of [["#gt-nut-phat", ketThuc ? "↺" : dangChay ? "⏸" : "▶"], ["#gt-mini-phat", ketThuc ? "↺" : dangChay ? "⏸" : "▶"]]) {
      const n = $(id);
      if (!n) continue;
      n.textContent = chu;
      n.title = ketThuc ? "Xem lại từ đầu" : dangChay ? "Tạm dừng (Space)" : "Phát (Space)";
      n.classList.toggle("dang-phat", dangChay);
    }
    const tc = $("#gt-tu-chay");
    if (tc) tc.checked = autoPlay;
    document.querySelectorAll("#gt-toc-do [data-toc-do]").forEach((n) => n.setAttribute("aria-pressed", String(Number(n.dataset.tocDo) === speed)));
  }

  /** Thanh đếm thời gian đọc (Tự động chạy). null = ẩn. */
  capNhatDemNguoc(phanDa) {
    const el = $("#gt-dem-nguoc");
    if (!el) return;
    const an = phanDa === null;
    if (el.classList.contains("an") !== an) el.classList.toggle("an", an);
    if (!an) el.firstElementChild.style.transform = `scaleX(${Math.max(0, Math.min(1, phanDa))})`;
  }

  // ---------- sự kiện giao diện ----------
  _ganSuKien() {
    const bam = (id, ten, gt) => $(id)?.addEventListener("click", (e) => { e.stopPropagation(); this._lenh(ten, gt); });
    bam("#gt-nut-dong", "dong");
    bam("#gt-nut-thu-nho", "thuNho");
    bam("#gt-nut-can-goc", "canGoc");
    bam("#gt-nut-truoc", "truoc");
    bam("#gt-nut-sau", "sau");
    bam("#gt-nut-phat", "phat");
    bam("#gt-nut-lap", "lap");
    bam("#gt-mini-truoc", "truoc");
    bam("#gt-mini-phat", "phat");
    bam("#gt-mini-sau", "sau");
    $("#gt-mini-bar")?.addEventListener("click", (e) => { if (!e.target.closest("button")) this._lenh("moRong"); });
    $("#gt-chon-quy-trinh")?.addEventListener("change", (e) => this._lenh("chonQuyTrinh", e.target.value));
    $("#gt-hang-cham")?.addEventListener("click", (e) => {
      const c = e.target.closest(".gt-cham");
      if (c) this._lenh("denBuoc", Number(c.dataset.buoc));
    });
    $("#gt-toc-do")?.addEventListener("click", (e) => {
      const n = e.target.closest("[data-toc-do]");
      if (n) this._lenh("tocDo", Number(n.dataset.tocDo));
    });
    $("#gt-tu-chay")?.addEventListener("change", (e) => this._lenh("tuChay", e.target.checked));
  }

  // kéo thả bằng thanh tiêu đề (máy tính) · vuốt xuống / lên để thu nhỏ / mở (tấm đáy trên điện thoại)
  _ganKeoTha() {
    const thanh = $("#gt-thanh-keo"), k = this.khung;
    if (!thanh || !k) return;
    const manRong = () => !window.matchMedia(`(max-width: 960px)`).matches;
    const kep = (x, a, b) => Math.min(b, Math.max(a, x));
    const dat = (left, top) => {
      const maxL = Math.max(10, window.innerWidth - (k.offsetWidth || 320) - 10), maxT = Math.max(10, window.innerHeight - 80);
      Object.assign(k.style, { left: `${kep(left, 10, maxL)}px`, top: `${kep(top, 10, maxT)}px`, right: "auto", bottom: "auto" });
    };
    const luu = this.luuDoc("bio-lab-gt-pos", null);
    if (luu && Number.isFinite(luu.left) && Number.isFinite(luu.top) && manRong()) dat(luu.left, luu.top);
    let keo = null;
    thanh.addEventListener("pointerdown", (e) => {
      if (!manRong() || e.target.closest("button, select, input")) return;
      const r = k.getBoundingClientRect();
      keo = { x: e.clientX, y: e.clientY, l: r.left, t: r.top };
      try { thanh.setPointerCapture(e.pointerId); } catch { /* bỏ qua */ }
    });
    thanh.addEventListener("pointermove", (e) => { if (keo) dat(keo.l + e.clientX - keo.x, keo.t + e.clientY - keo.y); });
    const tha = (e) => {
      if (!keo) return;
      keo = null;
      try { thanh.releasePointerCapture(e.pointerId); } catch { /* bỏ qua */ }
      const r = k.getBoundingClientRect();
      this.luuGhi("bio-lab-gt-pos", { left: Math.round(r.left), top: Math.round(r.top) });
      this._lenh("diChuyen");
    };
    thanh.addEventListener("pointerup", tha);
    thanh.addEventListener("pointercancel", tha);
    let y0 = 0;
    thanh.addEventListener("touchstart", (e) => { if (!manRong()) y0 = e.touches[0].clientY; }, { passive: true });
    thanh.addEventListener("touchend", (e) => {
      if (manRong()) return;
      const dy = e.changedTouches[0].clientY - y0;
      if (dy > 45) this._lenh("thuNho", true);
      else if (dy < -45) this._lenh("moRong");
    }, { passive: true });
  }
}
