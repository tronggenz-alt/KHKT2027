// Theme "Hoa anh đào" (mở bằng mã giao diện do admin cấp – mã chỉ kiểm ở server, không có trong code này). GIỮ NGUYÊN bố cục: chỉ đổi biến màu + bật một lớp hiệu ứng.
//   Màu: nền #FFF0F5 → #FFE4EC · thẻ #FFF7FA viền #FFB7C5 · nhấn #F48FB1 · đậm #C2185B (tiêu đề, nút) · chữ #5A3A44.
//   Hình (SVG tự vẽ): cành anh đào góc trên trái + góc dưới phải (nằm SAU nội dung – không bao giờ che chữ), 1–2 bông hoa ở góc mỗi thẻ lớn,
//   chấm trang trí → cánh hoa, nút "+K" hồng có bông hoa nhỏ.
//   Hoạt ảnh (canvas, 25–40 cánh, dừng khi tab ẩn): cánh hoa rơi chậm lắc lư theo gió · hover thẻ: vài cánh bung ra · thẻ thu nhỏ về góc trái:
//   vệt cánh hoa bay theo · vừa áp dụng mã: "mưa hoa" ~1.5 s. prefers-reduced-motion: không canvas, chỉ hình tĩnh.
(() => {
  const MAU = { nen1: "#FFF0F5", nen2: "#FFE4EC", the: "#FFF7FA", vien: "#FFB7C5", nhan: "#F48FB1", dam: "#C2185B", chu: "#5A3A44" };

  // ---------------------------------------------------------------- hình SVG tự vẽ
  // một bông anh đào 5 cánh (cánh có khía ở đầu), tâm (x, y), bán kính r, xoay g độ
  const hoa = (x, y, r, g = 0, nhat = "#FFD1DC", dam = MAU.nhan) => {
    const canh = Array.from({ length: 5 }, (_, i) => `<path transform="rotate(${i * 72})" d="M0 0C${-r * 0.55} ${-r * 0.35} ${-r * 0.5} ${-r * 0.95} ${-r * 0.16} ${-r}L0 ${-r * 0.84}L${r * 0.16} ${-r}C${r * 0.5} ${-r * 0.95} ${r * 0.55} ${-r * 0.35} 0 0z"/>`).join("");
    const nhuy = Array.from({ length: 5 }, (_, i) => {
      const a = (i * 72 + 36) * Math.PI / 180;
      return `<line x1="0" y1="0" x2="${(Math.sin(a) * r * 0.42).toFixed(1)}" y2="${(-Math.cos(a) * r * 0.42).toFixed(1)}"/><circle cx="${(Math.sin(a) * r * 0.45).toFixed(1)}" cy="${(-Math.cos(a) * r * 0.45).toFixed(1)}" r="${(r * 0.06).toFixed(1)}"/>`;
    }).join("");
    return `<g transform="translate(${x} ${y}) rotate(${g})"><g fill="${nhat}" stroke="${dam}" stroke-width="0.7">${canh}</g>
      <g stroke="${MAU.dam}" stroke-width="0.6" fill="${MAU.dam}">${nhuy}</g><circle r="${(r * 0.16).toFixed(1)}" fill="${MAU.dam}"/></g>`;
  };
  const nu = (x, y, g) => `<g transform="translate(${x} ${y}) rotate(${g})"><path d="M0 0C-3 -5 -3 -10 0 -13C3 -10 3 -5 0 0z" fill="${MAU.nhan}"/><path d="M0 0L0 4" stroke="#8B5E5A" stroke-width="1.2"/></g>`;
  const CANH = `<svg viewBox="0 0 320 230" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
    <g fill="none" stroke="#8B5E5A" stroke-linecap="round">
      <path d="M-6 26C40 30 80 40 118 62C150 80 182 92 222 96C252 99 276 110 302 132" stroke-width="7"/>
      <path d="M-6 26C40 30 80 40 118 62C150 80 182 92 222 96" stroke="#A9746C" stroke-width="2.4" opacity=".55"/>
      <path d="M118 62C126 92 120 124 104 152C96 166 86 178 70 190" stroke-width="4"/>
      <path d="M182 92C198 70 220 56 246 48" stroke-width="3.4"/>
      <path d="M222 96C230 120 248 138 266 150" stroke-width="2.6"/>
      <path d="M60 36C70 18 88 8 108 4" stroke-width="2.8"/>
      <path d="M104 152C118 162 126 178 128 196" stroke-width="2.2"/>
    </g>
    ${hoa(108, 6, 15, 12)}${hoa(248, 46, 17, -18)}${hoa(166, 82, 13, 40, "#FFE4EC")}${hoa(268, 152, 14, 8)}${hoa(70, 192, 16, -30)}
    ${hoa(128, 198, 11, 22, "#FFE4EC")}${hoa(300, 132, 12, -8)}${hoa(40, 30, 10, 50, "#FFE4EC")}${hoa(212, 104, 9, 0)}
    ${nu(88, 46, -40)}${nu(232, 60, 30)}${nu(112, 128, -70)}${nu(286, 118, 60)}${nu(150, 70, 10)}
  </svg>`;
  const THE_HOA = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 90 70">${hoa(58, 24, 17, 10)}${hoa(28, 46, 11, -25, "#FFE4EC")}${nu(80, 52, 40)}</svg>`;
  const HOA_NHO = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="-20 -20 40 40">${hoa(0, 0, 17, 0, "#FFFFFF", "#FFFFFF")}</svg>`;
  const uri = (svg) => `url("data:image/svg+xml,${encodeURIComponent(svg.replace(/\s+/g, " "))}")`;

  const CSS = `
    html[data-giao-dien="sakura"] .sakura-canh { position: fixed; z-index: -1; width: clamp(180px, 24vw, 330px); pointer-events: none; opacity: 0.95; }
    html[data-giao-dien="sakura"] .sakura-canh svg { display: block; width: 100%; height: auto; }
    html[data-giao-dien="sakura"] .sakura-canh.tren-trai { top: 60px; left: 0; }
    html[data-giao-dien="sakura"] .sakura-canh.duoi-phai { bottom: 0; right: 0; transform: rotate(180deg); }
    html[data-giao-dien="sakura"] body:not([data-muc-lon=""]) .sakura-canh { opacity: 0.2; } /* khu làm việc: cành mờ hẳn để chữ dễ đọc */
    html[data-giao-dien="sakura"] .sakura-lop { position: fixed; inset: 0; width: 100%; height: 100%; pointer-events: none; }
    html[data-giao-dien="sakura"] .sakura-lop.nen { z-index: -1; }
    html[data-giao-dien="sakura"] .sakura-lop.tren { z-index: 75; }
    html[data-giao-dien="sakura"] .the-lon-trang-tri { background: ${uri(THE_HOA)} no-repeat right 10px top 8px / 92px auto; opacity: 0.95; }
    html[data-giao-dien="sakura"] .the-lon:hover .the-lon-trang-tri { animation: sakura-lac 1.6s ease-in-out infinite alternate; transform-origin: 85% 15%; }
    @keyframes sakura-lac { from { transform: rotate(-3deg); } to { transform: rotate(3deg); } }
    html[data-giao-dien="sakura"] .cham-trang-tri i { width: calc(var(--s) * 2.4); height: calc(var(--s) * 1.7); border-radius: 100% 0 100% 0;
      background: linear-gradient(135deg, #FFD1DC, ${MAU.nhan}); opacity: 0.6; }
    html[data-giao-dien="sakura"] .nut-k { background: ${MAU.nhan}; border-color: ${MAU.nhan}; color: #fff; box-shadow: 0 6px 16px -8px ${MAU.dam}; }
    html[data-giao-dien="sakura"] .nut-k:hover { background: ${MAU.dam}; border-color: ${MAU.dam}; color: #fff; }
    html[data-giao-dien="sakura"] .nut-k-hoa { display: inline-block; width: 14px; height: 14px; background: ${uri(HOA_NHO)} center / contain no-repeat; }
    html[data-giao-dien="sakura"] .the-lon-icon, html[data-giao-dien="sakura"] .nut-mini-icon { background: #FFE4EC; color: ${MAU.dam}; }
  `;

  // ---------------------------------------------------------------- cánh hoa trên canvas
  const giam = () => window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  let style = null, canh = [], lopNen = null, lopTren = null, raf = 0, dangBat = false, nghe = [];
  let nenHoa = [], trenHoa = [], mau = [], tTruoc = 0;

  function mauCanh(c1, c2) { // vẽ sẵn một cánh hoa (sprite) → drawImage nhanh hơn vẽ path mỗi khung
    const c = document.createElement("canvas");
    c.width = c.height = 48;
    const x = c.getContext("2d"), s = 20;
    x.translate(24, 24);
    const g = x.createLinearGradient(0, -s, 0, s);
    g.addColorStop(0, c1);
    g.addColorStop(1, c2);
    x.fillStyle = g;
    x.beginPath();
    x.moveTo(0, s);
    x.bezierCurveTo(-s * 0.95, s * 0.35, -s * 0.8, -s * 0.85, -s * 0.2, -s);
    x.lineTo(0, -s * 0.72);
    x.lineTo(s * 0.2, -s);
    x.bezierCurveTo(s * 0.8, -s * 0.85, s * 0.95, s * 0.35, 0, s);
    x.fill();
    x.strokeStyle = "rgba(194, 24, 91, 0.25)";
    x.lineWidth = 1;
    x.stroke();
    return c;
  }
  function taoLop(ten) {
    const c = document.createElement("canvas");
    c.className = `sakura-lop ${ten}`;
    c.setAttribute("aria-hidden", "true");
    document.body.append(c);
    return c;
  }
  function coLai() {
    const dpr = Math.min(1.5, window.devicePixelRatio || 1);
    [lopNen, lopTren].forEach((c) => {
      if (!c) return;
      c.width = Math.round(innerWidth * dpr);
      c.height = Math.round(innerHeight * dpr);
      c.getContext("2d").setTransform(dpr, 0, 0, dpr, 0, 0);
    });
  }
  const ngauNhien = (a, b) => a + Math.random() * (b - a);
  function canhMoi(o = {}) {
    return {
      x: ngauNhien(-20, innerWidth + 20), y: ngauNhien(-innerHeight, -10), s: ngauNhien(7, 14), vx: ngauNhien(-0.15, 0.25), vy: ngauNhien(0.35, 0.9),
      g: ngauNhien(0, Math.PI * 2), vg: ngauNhien(-0.02, 0.02), lat: ngauNhien(0, Math.PI * 2), vlat: ngauNhien(0.02, 0.05),
      pha: ngauNhien(0, Math.PI * 2), bien: ngauNhien(0.3, 0.9), song: Infinity, tuoi: 0, mo: ngauNhien(0.55, 0.9), anh: mau[Math.floor(Math.random() * mau.length)],
      ...o,
    };
  }
  function soCanhNen() { return innerWidth < 640 ? 25 : innerWidth < 1100 ? 32 : 40; }

  function capNhat(ds, dt, t, gio, laNen) {
    for (let i = ds.length - 1; i >= 0; i -= 1) {
      const p = ds[i];
      p.tuoi += dt * 16.7;
      p.x += (p.vx + gio + Math.sin(t / 900 + p.pha) * p.bien * 0.35) * dt;
      p.y += p.vy * dt;
      p.g += p.vg * dt;
      p.lat += p.vlat * dt;
      if (p.keo) { p.vx *= 0.985; p.vy = p.vy * 0.985 + 0.012 * dt; } // cánh bung ra: chậm dần rồi rơi
      const het = p.y > innerHeight + 30 || p.x < -60 || p.x > innerWidth + 60 || p.tuoi > p.song;
      if (het) {
        if (laNen && ds.length <= soCanhNen()) Object.assign(p, canhMoi({ y: -20 }));
        else ds.splice(i, 1);
      }
    }
  }
  function ve(c, ds) {
    const x = c.getContext("2d");
    x.clearRect(0, 0, innerWidth, innerHeight);
    for (const p of ds) {
      const mo = p.song === Infinity ? p.mo : p.mo * Math.max(0, 1 - p.tuoi / p.song);
      if (mo <= 0) continue;
      x.save();
      x.globalAlpha = mo;
      x.translate(p.x, p.y);
      x.rotate(p.g);
      x.scale(Math.max(0.25, Math.abs(Math.cos(p.lat))), 1); // lật cánh như bay trong gió
      x.drawImage(p.anh, -p.s, -p.s, p.s * 2, p.s * 2);
      x.restore();
    }
  }
  function khung(t) {
    raf = 0;
    if (!dangBat || document.hidden) return;
    const dt = Math.min(3, tTruoc ? (t - tTruoc) / 16.7 : 1);
    tTruoc = t;
    const gio = Math.sin(t / 4200) * 0.35 + Math.sin(t / 1700) * 0.1; // gió thổi nhẹ, đổi chiều chậm
    capNhat(nenHoa, dt, t, gio, true);
    capNhat(trenHoa, dt, t, gio, false);
    ve(lopNen, nenHoa);
    ve(lopTren, trenHoa);
    raf = requestAnimationFrame(khung);
  }
  function chay() { if (!raf && dangBat && !document.hidden && !giam()) { tTruoc = 0; raf = requestAnimationFrame(khung); } }

  // cánh bung ra từ viền một hình chữ nhật (hover thẻ)
  function bung(r, so = 7) {
    for (let i = 0; i < so; i += 1) {
      const canhNao = Math.floor(Math.random() * 4);
      const x = canhNao === 1 ? r.right : canhNao === 3 ? r.left : ngauNhien(r.left, r.right);
      const y = canhNao === 0 ? r.top : canhNao === 2 ? r.bottom : ngauNhien(r.top, r.bottom);
      const a = Math.atan2(y - (r.top + r.height / 2), x - (r.left + r.width / 2));
      trenHoa.push(canhMoi({ x, y, s: ngauNhien(6, 10), vx: Math.cos(a) * ngauNhien(1.2, 2.4), vy: Math.sin(a) * ngauNhien(1.2, 2.4), keo: true,
        song: ngauNhien(800, 1200), mo: 0.9 }));
    }
    chay();
  }
  function muaHoa(giay = 1.5) { // cơn mưa hoa ngắn khi vừa áp dụng mã, rồi trở về mức rơi bình thường
    if (giam()) return;
    const het = performance.now() + giay * 1000;
    const tha = () => {
      if (!dangBat || performance.now() > het) return;
      for (let i = 0; i < 6; i += 1) {
        trenHoa.push(canhMoi({ y: ngauNhien(-40, -10), vy: ngauNhien(2.2, 4), vx: ngauNhien(-0.6, 0.9), s: ngauNhien(8, 15), song: 4000, mo: 0.95 }));
      }
      setTimeout(tha, 60);
    };
    tha();
    chay();
  }

  // ---------------------------------------------------------------- bật / tắt lớp hiệu ứng
  function bat({ mua = false } = {}) {
    if (!dangBat) {
      dangBat = true;
      style = document.createElement("style");
      style.id = "sakura-css";
      style.textContent = CSS;
      document.head.append(style);
      canh = ["tren-trai", "duoi-phai"].map((vt) => {
        const el = document.createElement("div");
        el.className = `sakura-canh ${vt}`;
        el.innerHTML = CANH;
        document.body.append(el);
        return el;
      });
      if (!giam()) {
        mau = [mauCanh("#FFE4EC", MAU.nhan), mauCanh("#FFD1DC", "#F8A5C2"), mauCanh("#FFFFFF", "#FFB7C5")];
        lopNen = taoLop("nen");
        lopTren = taoLop("tren");
        coLai();
        nenHoa = Array.from({ length: soCanhNen() }, () => canhMoi({ y: ngauNhien(-innerHeight, innerHeight) }));
        trenHoa = [];
        const them = (muc, ten, f, tuyChon) => { muc.addEventListener(ten, f, tuyChon); nghe.push([muc, ten, f, tuyChon]); };
        them(window, "resize", coLai);
        them(document, "visibilitychange", chay); // tab ẩn → khung() tự dừng; hiện lại → chạy tiếp
        const lanCuoi = new WeakMap();
        them(document, "mouseover", (e) => {
          const t = e.target.closest?.(".the-lon");
          if (!t || (e.relatedTarget && t.contains(e.relatedTarget)) || performance.now() - (lanCuoi.get(t) || 0) < 700) return;
          lanCuoi.set(t, performance.now());
          bung(t.getBoundingClientRect());
        });
        them(document, "trang-chu:bay", (e) => { // vệt cánh hoa bay theo thẻ đang thu nhỏ / phóng to
          const { bong, thoiGian } = e.detail, het = performance.now() + thoiGian;
          const theo = () => {
            if (!dangBat || !bong.isConnected || performance.now() > het) return;
            const r = bong.getBoundingClientRect();
            for (let i = 0; i < 2; i += 1) {
              trenHoa.push(canhMoi({ x: ngauNhien(r.left, r.right), y: ngauNhien(r.top, r.bottom), s: ngauNhien(5, 9), vx: ngauNhien(-0.8, 0.8),
                vy: ngauNhien(0.2, 1.2), song: ngauNhien(700, 1100), keo: true, mo: 0.9 }));
            }
            requestAnimationFrame(theo);
          };
          theo();
          chay();
        });
        chay();
      }
    }
    if (mua) muaHoa(1.5);
  }

  function tat() {
    dangBat = false;
    if (raf) cancelAnimationFrame(raf);
    raf = 0;
    nghe.forEach(([muc, ten, f, tuyChon]) => muc.removeEventListener(ten, f, tuyChon));
    nghe = [];
    [style, lopNen, lopTren, ...canh].forEach((el) => el?.remove());
    style = lopNen = lopTren = null;
    canh = [];
    nenHoa = [];
    trenHoa = [];
  }

  (window.GIAO_DIEN = window.GIAO_DIEN || {}).sakura = {
    id: "sakura",
    ten: "Hoa anh đào",
    sang: true, // luôn nền sáng hồng (kể cả khi đang bật chế độ tối)
    bien: {
      "--giay": MAU.the, "--giay-lom": "#FFEEF3", "--muc": MAU.chu, "--muc-2": "#7A5561", "--lang": "#A47E89",
      "--chi": MAU.vien, "--chi-dam": MAU.nhan, "--nhan": MAU.dam, "--nhan-lom": MAU.nen2, "--ho-phach": "#D81B60", "--tren-nhan": "#FFFFFF",
      "--dai": `linear-gradient(135deg, ${MAU.nhan} 0%, #E86A9A 52%, ${MAU.dam} 100%)`,
      "--bong": "0 1px 2px rgba(194, 24, 91, 0.05), 0 10px 26px -16px rgba(194, 24, 91, 0.32)",
      "--nen-trang": `linear-gradient(180deg, ${MAU.nen1} 0%, ${MAU.nen2} 100%)`,
      "--the-lon-nen": MAU.the, "--the-lon-vien": MAU.vien, "--trang-chu-cham": MAU.nhan, "--chu-tieu-de": MAU.dam,
      "--cn-mau": MAU.dam, "--cn-luoi": "rgba(194, 24, 91, 0.07)",
    },
    hieuUng: { bat, tat, mua: muaHoa },
  };
})();
