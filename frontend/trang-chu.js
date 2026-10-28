// Trang chủ 3 thẻ lớn [Giải chuỗi] [Mô hình 3D] [Cơ chế] + hiệu ứng THU NHỎ VỀ GÓC TRÁI (kỹ thuật FLIP, Web Animations):
//   bấm thẻ → thẻ thu nhỏ bay về góc trên trái thành nút nhỏ (icon + tên), 2 thẻ còn lại mờ dần trượt ra ngoài, nội dung mục mở rộng ra;
//   bấm nút nhỏ / nút Back của trình duyệt → chạy ngược về trang chủ. prefers-reduced-motion → chỉ mờ dần.
// Các mục cũ (01–05) vẫn nằm nguyên trong trang (app.js không đổi) – chỉ ẩn / hiện theo body[data-muc-lon] + [data-thuoc-muc].
// Sự kiện cho theme (vd cánh hoa bay theo thẻ): document "trang-chu:bay" { bong, thoiGian, chieu: "mo" | "ve" }.
const TrangChu = (() => {
  const $ = (s) => document.querySelector(s);
  const MUC = {
    giai: { ten: "Giải chuỗi", hash: "giai-chuoi", dau: "muc-nhap" },
    "3d": { ten: "Mô hình 3D", hash: "mo-hinh-3d", dau: "muc-3d" },
    "co-che": { ten: "Cơ chế", hash: "co-che", dau: "muc-co-che" },
    "tim-cau-tao": { ten: "Tìm cấu tạo chất", hash: "tim-cau-tao", dau: "muc-tim-cau-tao" },
  };
  const THEO_HASH = { "giai-chuoi": "giai", "muc-nhap": "giai", "muc-tuy-chon": "giai", "muc-ket-qua": "giai", "ket-qua": "giai",
    "mo-hinh-3d": "3d", "muc-3d": "3d", "co-che": "co-che", "muc-co-che": "co-che",
    "tim-cau-tao": "tim-cau-tao", "muc-tim-cau-tao": "tim-cau-tao", "tim-chat": "tim-cau-tao", "muc-tim-chat": "tim-cau-tao" };
  const EASE = "cubic-bezier(0.22, 1, 0.36, 1)";
  const TG = 520;
  let dangChay = false;
  const giamHieuUng = () => window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const hienTai = () => document.body.dataset.mucLon || "";
  const theCua = (muc) => document.querySelector(`.the-lon[data-chon-muc="${muc}"]`);
  const doi = (a) => Promise.race([a.finished.catch(() => {}), new Promise((r) => setTimeout(r, (a.effect?.getTiming().duration || 0) + (a.effect?.getTiming().delay || 0) + 300))]);

  function capNhatMini(muc) {
    if (!muc) return;
    $("#nut-mini .nut-mini-icon").innerHTML = theCua(muc).querySelector(".the-lon-icon").innerHTML;
    $("#nut-mini .nut-mini-ten").textContent = MUC[muc].ten;
    $("#nut-mini").setAttribute("aria-label", `${MUC[muc].ten} – về trang chủ`);
  }
  function datCheDo(muc) {
    if (muc) document.body.dataset.mucHien = muc; // trang chủ: giữ nguyên nhóm đang dựng sẵn (về trang chủ không phải dựng lại gì)
    document.body.dataset.mucLon = muc;
    capNhatMini(muc);
    document.title = muc ? `${MUC[muc].ten} · CHEMISTRY` : "CHEMISTRY · Giải Chuỗi Hữu Cơ";
  }
  // khung 3D (3Dmol) đo lại kích thước – gọi SAU hiệu ứng (giữa hiệu ứng sẽ làm khựng)
  const doLai = (muc) => { if (muc === "3d") window.dispatchEvent(new Event("resize")); };
  // chờ khung hình hiện tại vẽ xong (bóng + animation đã lên compositor) rồi mới làm việc nặng
  const sauKhungHinh = () => new Promise((r) => requestAnimationFrame(() => setTimeout(r, 0)));

  // MƯỢT: mọi chuyển động chỉ dùng transform + opacity (chạy trên compositor – việc nặng ở luồng chính không làm khựng)
  // transform đưa hình chữ nhật `tu` trùng khít `den` (transform-origin: 0 0)
  const bienDoi = (tu, den) => `translate(${den.left - tu.left}px, ${den.top - tu.top}px) scale(${den.width / tu.width}, ${den.height / tu.height})`;

  // bản sao cố định đúng vị trí một phần tử (bay trên màn hình trong lúc bố cục thật đổi phía dưới)
  function bong(el, r = el.getBoundingClientRect()) {
    const g = el.cloneNode(true);
    g.removeAttribute("id");
    g.querySelectorAll("[id]").forEach((x) => x.removeAttribute("id"));
    g.classList.add("bong-flip");
    g.setAttribute("aria-hidden", "true");
    Object.assign(g.style, { left: `${r.left}px`, top: `${r.top}px`, width: `${r.width}px`, height: `${r.height}px`, transform: "none",
      transformOrigin: "0 0", willChange: "transform, opacity", visibility: "visible" }); // (thẻ gốc có thể đang ẩn – bản sao luôn hiện)
    document.body.append(g);
    return g;
  }
  const phat = (g, chieu) => document.dispatchEvent(new CustomEvent("trang-chu:bay", { detail: { bong: g, thoiGian: TG, chieu } }));

  // vị trí nút nhỏ khi khu làm việc hiện – TÍNH TRƯỚC (không phải dựng cả khu làm việc mới biết): cùng mép trái với khung trang,
  // ngay dưới thanh trên + padding 12px của .thanh-muc; kích thước đo trên một bản sao nhỏ
  function miniDuKien(muc) {
    const tam = $("#nut-mini").cloneNode(true);
    tam.removeAttribute("id");
    tam.querySelector(".nut-mini-icon").innerHTML = theCua(muc).querySelector(".the-lon-icon").innerHTML;
    tam.querySelector(".nut-mini-ten").textContent = MUC[muc].ten;
    Object.assign(tam.style, { position: "fixed", left: "0", top: "0", visibility: "hidden" });
    document.body.append(tam);
    const { width: w, height: h } = tam.getBoundingClientRect(); // (không dùng offsetWidth: làm tròn xuống → chữ bị xuống dòng)
    tam.remove();
    return new DOMRect($("#trang-chu .khung").getBoundingClientRect().left, $(".thanh-tren").getBoundingClientRect().bottom + 12, w, h);
  }
  function banSaoMini(muc, r) { // bản sao nút nhỏ (đúng hình viên thuốc) để hiện dần ở cuối đường bay
    const g = $("#nut-mini").cloneNode(true);
    g.removeAttribute("id");
    g.querySelector(".nut-mini-icon").innerHTML = theCua(muc).querySelector(".the-lon-icon").innerHTML;
    g.querySelector(".nut-mini-ten").textContent = MUC[muc].ten;
    g.classList.add("bong-flip");
    g.setAttribute("aria-hidden", "true");
    Object.assign(g.style, { left: `${r.left}px`, top: `${r.top}px`, width: `${r.width}px`, height: `${r.height}px`, transformOrigin: "0 0",
      willChange: "transform, opacity", visibility: "visible", opacity: "0", whiteSpace: "nowrap" });
    document.body.append(g);
    return g;
  }
  // hai thẻ còn lại: trượt ra ngoài (mở) / vào lại (về) – theo hàng ngang (máy tính) hoặc dọc (điện thoại)
  function huongRa(r, rChon) {
    const ngang = Math.abs(r.top - rChon.top) < 20;
    return `translate(${ngang ? Math.sign(r.left - rChon.left) * 90 : 0}px, ${ngang ? 0 : Math.sign(r.top - rChon.top) * 70}px) scale(0.96)`;
  }
  // keyframes: thẻ lớn → nút nhỏ (thân thẻ mờ nhanh, hộp mờ dần ở nửa sau) và bản sao nút nhỏ hiện dần (đi ngược đường)
  const kfThe = (rThe, rMini) => [{ transform: "none", opacity: 1 }, { opacity: 1, offset: 0.45 }, { transform: bienDoi(rThe, rMini), opacity: 0 }];
  const kfMini = (rThe, rMini) => [{ transform: bienDoi(rMini, rThe), opacity: 0 }, { opacity: 0, offset: 0.4 }, { transform: "none", opacity: 1 }];

  async function moTuTrangChu(muc) {
    const kg = $("#khong-gian"), mini = $("#nut-mini"), chon = theCua(muc);
    window.scrollTo(0, 0);
    const truoc = [...document.querySelectorAll(".the-lon")].map((t) => [t, t.getBoundingClientRect()]);
    const rChon = truoc.find(([t]) => t === chon)[1];
    let rMini = miniDuKien(muc);
    // 1. bóng bay NGAY (trang chủ thật ẩn đi – chỉ visibility, không dựng lại bố cục)
    const g = bong(chon, rChon), nho = banSaoMini(muc, rMini);
    const a = { the: g.animate(kfThe(rChon, rMini), { duration: TG, easing: EASE, fill: "forwards" }),
      than: g.querySelector(".the-lon-than").animate([{ opacity: 1 }, { opacity: 0 }], { duration: TG * 0.22, easing: "ease-out", fill: "forwards" }),
      mini: nho.animate(kfMini(rChon, rMini), { duration: TG, easing: EASE, fill: "forwards" }) };
    const ra = truoc.filter(([t]) => t !== chon).map(([t, r]) => {
      const b = bong(t, r);
      return [b, b.animate([{ opacity: 1, transform: "none" }, { opacity: 0, transform: huongRa(r, rChon) }], { duration: TG * 0.8, easing: EASE, fill: "forwards" })];
    });
    $("#trang-chu").style.visibility = "hidden";
    phat(g, "mo");
    // 2. khung hình kế: mới dựng khu làm việc (việc nặng) – bóng vẫn bay mượt trên compositor
    await sauKhungHinh();
    datCheDo(muc);
    $("#trang-chu").style.visibility = "";
    mini.style.visibility = "hidden";
    const that = mini.getBoundingClientRect();
    if (Math.abs(that.left - rMini.left) + Math.abs(that.top - rMini.top) + Math.abs(that.width - rMini.width) > 0.5) { // lệch → chỉnh đích giữa chừng
      rMini = that;
      Object.assign(nho.style, { left: `${that.left}px`, top: `${that.top}px`, width: `${that.width}px`, height: `${that.height}px` });
      a.the.effect.setKeyframes(kfThe(rChon, rMini));
      a.mini.effect.setKeyframes(kfMini(rChon, rMini));
    }
    const noiDung = kg.animate([{ opacity: 0, transform: "translateY(16px)" }, { opacity: 1, transform: "none" }],
      { duration: TG * 0.7, delay: TG * 0.2, easing: EASE, fill: "backwards" });
    await Promise.all([...Object.values(a), ...ra.map(([, x]) => x), noiDung].map(doi));
    mini.style.visibility = "";
    [g, nho, ...ra.map(([b]) => b)].forEach((x) => x.remove());
    doLai(muc);
  }

  async function veTrangChu() {
    const muc = hienTai(), kg = $("#khong-gian"), mini = $("#nut-mini");
    const rMini = mini.getBoundingClientRect();
    const nho = banSaoMini(muc, rMini);
    nho.style.opacity = "1";
    mini.style.visibility = "hidden";
    await doi(kg.animate([{ opacity: 1, transform: "none" }, { opacity: 0, transform: "translateY(12px)" }], { duration: 160, easing: "ease-in", fill: "forwards" }));
    window.scrollTo(0, 0);
    datCheDo(""); // trang chủ nhẹ: dựng lại nhanh
    kg.getAnimations().forEach((x) => x.cancel());
    mini.style.visibility = "";
    const cacThe = [...document.querySelectorAll(".the-lon")];
    cacThe.forEach((t) => { t.style.visibility = "hidden"; });
    const chon = theCua(muc), rChon = chon.getBoundingClientRect();
    const g = bong(chon, rChon);
    const nguoc = (kf) => kf.slice().reverse().map((k) => ({ ...k, offset: k.offset === undefined ? undefined : 1 - k.offset }));
    const a = [g.animate(nguoc(kfThe(rChon, rMini)), { duration: TG, easing: EASE, fill: "both" }),
      g.querySelector(".the-lon-than").animate([{ opacity: 0 }, { opacity: 1 }], { duration: TG * 0.3, delay: TG * 0.7, easing: "ease-out", fill: "both" }),
      nho.animate(nguoc(kfMini(rChon, rMini)), { duration: TG, easing: EASE, fill: "both" })];
    const vao = cacThe.filter((t) => t !== chon).map((t) => {
      const r = t.getBoundingClientRect(), b = bong(t, r);
      return [b, b.animate([{ opacity: 0, transform: huongRa(r, rChon) }, { opacity: 1, transform: "none" }], { duration: TG * 0.85, delay: TG * 0.15, easing: EASE, fill: "both" })];
    });
    phat(g, "ve");
    await Promise.all([...a, ...vao.map(([, x]) => x)].map(doi));
    cacThe.forEach((t) => { t.style.visibility = ""; });
    [g, nho, ...vao.map(([b]) => b)].forEach((x) => x.remove());
    chon.focus({ preventScroll: true });
  }

  async function moDan(muc) { // giảm chuyển động / đổi thẳng giữa các mục: chỉ mờ dần
    window.scrollTo(0, 0);
    datCheDo(muc);
    doLai(muc);
    const el = muc ? $("#khong-gian") : $("#trang-chu");
    await doi(el.animate([{ opacity: 0 }, { opacity: 1 }], { duration: 220, easing: "ease-out" }));
  }

  async function chuyen(muc, { lichSu = "day" } = {}) {
    if (dangChay || muc === hienTai()) return;
    dangChay = true;
    const tu = hienTai();
    if (lichSu === "day") history.pushState({ muc, tuTrangChu: true }, "", muc ? `#${MUC[muc].hash}` : location.pathname + location.search);
    if (lichSu === "thay") history.replaceState({ muc }, "", muc ? `#${MUC[muc].hash}` : location.pathname + location.search);
    try {
      if (giamHieuUng() || (tu && muc)) await moDan(muc);
      else if (muc) await moTuTrangChu(muc);
      else await veTrangChu();
    } finally {
      dangChay = false;
    }
    const dung = theoUrl(); // bấm Back / Forward liên tục trong lúc hiệu ứng chạy → khớp lại theo URL, không hiệu ứng
    if (dung !== hienTai()) datCheDo(dung);
  }

  function theoUrl() {
    if (/[?&]bai=/.test(location.search) && !location.hash) return "giai";
    return THEO_HASH[decodeURIComponent(location.hash.slice(1))] || "";
  }

  // Mở một mục từ code khác (vd nút "3D" trên chất trong lời giải): đang ở khu làm việc thì đổi ngay (đồng bộ, để app.js cuộn tới được)
  function mo(muc) {
    if (!MUC[muc] || muc === hienTai()) return;
    if (hienTai()) {
      history.replaceState({ muc, tuTrangChu: !!history.state?.tuTrangChu }, "", `#${MUC[muc].hash}`); // giữ cờ → nút nhỏ vẫn "Back" đúng
      datCheDo(muc);
      doLai(muc);
      $("#khong-gian").animate([{ opacity: 0.4 }, { opacity: 1 }], { duration: 200 });
    } else chuyen(muc);
  }

  // rê chuột / chạm / Tab vào thẻ → dựng sẵn nhóm đó trong nền (đang ẩn) → lúc bấm không còn việc nặng
  function dungSan(muc) { if (!hienTai() && !dangChay && document.body.dataset.mucHien !== muc) document.body.dataset.mucHien = muc; }

  function batDau() {
    document.querySelectorAll(".the-lon").forEach((t) => {
      t.addEventListener("click", () => chuyen(t.dataset.chonMuc));
      ["pointerenter", "touchstart", "focus"].forEach((ev) => t.addEventListener(ev, () => dungSan(t.dataset.chonMuc), { passive: true }));
    });
    $("#nut-mini")?.addEventListener("click", () => {
      if (history.state && history.state.tuTrangChu) history.back(); // mở từ trang chủ → Back (popstate chạy hiệu ứng ngược)
      else chuyen("", { lichSu: "thay" });
    });
    window.addEventListener("popstate", () => {
      const muc = theoUrl();
      if (muc !== hienTai()) chuyen(muc, { lichSu: "khong" });
    });
    // tải trang: inline script trong <body> đã đặt sẵn nhóm theo URL (không nháy) – ở đây chỉ gắn trạng thái lịch sử + nút nhỏ
    const dau = theoUrl();
    datCheDo(dau);
    if (dau) history.replaceState({ muc: dau }, "", location.href);

    // Hỗ trợ thanh điều hướng và ô nhập nhanh kiểu Awesomic
    document.querySelectorAll("[data-dieu-huong]").forEach((el) => {
      el.addEventListener("click", (e) => {
        e.preventDefault();
        const muc = el.dataset.dieuHuong || "giai";
        chuyen(muc);
      });
    });
    const btnHero = $("#hero-nut-giai");
    const inpHero = $("#hero-nhap-de");
    if (btnHero && inpHero) {
      const chayHero = () => {
        const val = inpHero.value.trim();
        if (!val) {
          chuyen("giai");
          return;
        }
        const laDeDai = val.length > 80 || /chất|hỗn hợp|phản ứng|đốt cháy|khối lượng|este|peptit/i.test(val);
        if (laDeDai) {
          chuyen("tim-cau-tao");
          const o = $("#o-de-tim-chat");
          if (o) {
            o.value = val;
            o.dispatchEvent(new Event("input", { bubbles: true }));
            $("#nut-tim-chat")?.click();
          }
        } else {
          chuyen("giai");
          const o = $("#o-chuoi");
          if (o) {
            o.value = val;
            o.dispatchEvent(new Event("input", { bubbles: true }));
            $("#nut-giai")?.click();
          }
        }
      };
      btnHero.addEventListener("click", chayHero);
      inpHero.addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); chayHero(); } });
    }
    document.querySelectorAll(".chip-vi-du").forEach((btn) => {
      btn.addEventListener("click", (e) => {
        e.preventDefault();
        if (btn.dataset.chuyen) {
          chuyen(btn.dataset.chuyen);
        } else if (btn.dataset.viDu && inpHero) {
          inpHero.value = btn.dataset.viDu;
          btnHero?.click();
        }
      });
    });
  }

  batDau();
  return { mo, chuyen, get hienTai() { return hienTai(); } };
})();
window.TrangChu = TrangChu;
