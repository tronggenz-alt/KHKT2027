// Bản đồ hai tầng phòng thí nghiệm ảo, vẽ bằng SVG, không cần thư viện nên chạy được khi không có Internet.
// Kích thước phòng cùng bảng KHU của server/lab/truc_tiep.py (lấy từ ZoneMap.cs trong Unity). Đồ đạc vẽ giản lược.
// Dùng: BanDo.ve(phanTu, { trangThai: { phong_sinh: { muc: "do", chu: "12 lỗi" } }, khiChon: (ma) => {}, to: false })
//   muc: "do" có lỗi, "vang" cần xem, "lam" có hoạt động. to: nhãn ghim to hơn, cho bản đồ đặt ở ô nhỏ.
const BanDo = (() => {
  const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

  const TEN_PHONG = {
    phong_lab: "Phòng thí nghiệm Hóa", kho_hoa_chat: "Phòng hóa chất", hanh_lang: "Hành lang tầng 1", phong_may: "Phòng máy phân tích", phong_robot: "Phòng tự động hóa và robot",
    phong_sinh: "Phòng thí nghiệm Sinh", phong_vi_sinh: "Phòng vi sinh", kho_sinh_pham: "Kho sinh phẩm", hanh_lang_t2: "Hành lang tầng 2", phong_sinh_phan_tu: "Phòng sinh học phân tử", nha_kinh: "Nhà kính thực vật",
    hanh_lang_dong: "Hành lang đông tầng 2", loi_thang: "Cầu thang và thang máy",
  };

  const TANG = [
    { so: 1, lop: "tang-hoa", ten: "Tầng 1 · Hóa học", canh: [[27, -2.2, 30, 6]], phong: [
      { ma: "phong_lab", x0: 0, z0: 0, x1: 20, z1: 15, nhan: "Phòng thí nghiệm", nz: 13.2 },
      { ma: "kho_hoa_chat", x0: 20, z0: 0, x1: 27, z1: 15, nhan: "Phòng hóa chất" },
      { ma: "hanh_lang", x0: 0, z0: -2.2, x1: 27, z1: 0, phu: true },
      { ma: "phong_may", x0: 0, z0: -9, x1: 13.5, z1: -2.2, nhan: "Máy phân tích" },
      { ma: "phong_robot", x0: 13.5, z0: -9, x1: 27, z1: -2.2, nhan: "Tự động hóa, robot" },
      { ma: "loi_thang", x0: 27, z0: -2.2, x1: 30, z1: 6, phu: true },
    ], do_dac() {
      const v = [];
      for (const x of [4.6, 10.4]) for (const z of [2.2, 7.1, 12]) v.push([x, z, 3.2, 1.1, 0.9, "ban"]);
      v.push([16, 8.6, 2.6, 1, 0.9, "ban"], [15.6, 13.6, 2.6, 1, 2, "may"], [15.6, 0.6, 3.2, 1, 1.1, "may"], [0.6, 6.4, 1, 2.4, 0.9, "ban"]);
      for (let i = 0; i < 9; i++) for (const x of [21, 23.1, 25.2]) v.push([x, 0.8 + i * 1.5, 0.9, 1.1, 2, "ke"]);
      for (let i = 0; i < 5; i++) for (const z of [-8.4, -3.8]) v.push([1 + i * 2.5, z, 1.5, 0.9, 1.2, "may"]);
      v.push([15.5, -6.3, 2.2, 2.2, 0.4, "ban"], [20.5, -6.3, 2.2, 2.2, 0.4, "ban"]);
      return v;
    } },
    { so: 2, lop: "tang-sinh", ten: "Tầng 2 · Sinh học", canh: [[27, -2.2, 30, 15]], phong: [
      { ma: "phong_sinh", x0: 0, z0: 0, x1: 20, z1: 15, nhan: "Phòng thí nghiệm Sinh" },
      { ma: "phong_vi_sinh", x0: 20, z0: 7.5, x1: 27, z1: 15, nhan: "Vi sinh", nz: 12.8 },
      { ma: "kho_sinh_pham", x0: 20, z0: 0, x1: 27, z1: 7.5, nhan: "Kho sinh phẩm" },
      { ma: "hanh_lang_t2", x0: 0, z0: -3.6, x1: 27, z1: 0, phu: true },
      { ma: "phong_sinh_phan_tu", x0: 0, z0: -9, x1: 13.5, z1: -3.6, nhan: "Sinh học phân tử" },
      { ma: "nha_kinh", x0: 13.5, z0: -9, x1: 27, z1: -3.6, nhan: "Nhà kính" },
      { ma: "loi_thang", x0: 27, z0: -2.2, x1: 30, z1: 6, phu: true },
      { ma: "hanh_lang_dong", x0: 27, z0: 6, x1: 30, z1: 15, phu: true },
    ], do_dac() {
      const v = [];
      for (const x of [4.6, 9.8]) for (const z of [2.2, 7.1]) v.push([x, z, 3.2, 1.1, 0.9, "ban"]);
      // Máy đặt theo tọa độ ở bản đồ cũ của sinh.html
      for (const [x, z, h] of [[2.2, 7.5, 1], [6.6, 14, 2], [18.5, 4.2, 1.5], [18.5, 5.8, 1.2], [18.5, 11.2, 1], [23, 11.5, 1], [24.2, 13.6, 1], [21.5, 6.2, 2], [23.5, 6.2, 2], [1.2, -6.2, 1.5], [10.2, -8.1, 1], [11.4, -8.1, 1], [12.6, -7, 1.2], [16.2, -8.2, 1], [19.6, -8.2, 1], [25, -6.2, 1.4]]) v.push([x - 0.5, z - 0.5, 1, 1, h, "may"]);
      return v;
    } },
  ];

  function ve(el, tuyChon = {}) {
    const C = 0.866, S = 0.5, K = 12.4, tt = tuyChon.trangThai || {};
    const chieu = (x, z, y, t) => [(x + z) * C * K - t * 232, (x - z) * S * K - y * K * 0.85 - t * 226];
    const dd = (pts) => pts.map((p) => p.map((n) => n.toFixed(1)).join(",")).join(" ");
    const hop = (x, z, w, s, h, cls, t) => {
      const a = (q, r, y) => chieu(q, r, y, t), x1 = x + w, z1 = z + s;
      return `<g class="vat ${cls}"><polygon class="mp" points="${dd([a(x1, z, 0), a(x1, z1, 0), a(x1, z1, h), a(x1, z, h)])}"/><polygon class="mt" points="${dd([a(x, z, 0), a(x1, z, 0), a(x1, z, h), a(x, z, h)])}"/><polygon class="mn" points="${dd([a(x, z, h), a(x1, z, h), a(x1, z1, h), a(x, z1, h)])}"/></g>`;
    };
    const san = (x0, z0, x1, z1, a) => `<polygon class="san-day" points="${dd([a(x0, z0, -0.7), a(x1, z0, -0.7), a(x1, z1, -0.7), a(x0, z1, -0.7)])}"/><polygon class="san-ben" points="${dd([a(x1, z0, 0), a(x1, z1, 0), a(x1, z1, -0.7), a(x1, z0, -0.7)])}"/><polygon class="san-truoc" points="${dd([a(x0, z0, 0), a(x1, z0, 0), a(x1, z0, -0.7), a(x0, z0, -0.7)])}"/>`;
    const nho = tuyChon.to || matchMedia("(max-width: 620px)").matches, co = nho ? 23 : 12.5, cao = nho ? 40 : 24;
    let hinh = "", ghim = "", noi = "";

    for (const tang of TANG) {
      const t = tang.so - 1, a = (x, z, y) => chieu(x, z, y, t);
      hinh += `<g class="tang ${tang.lop}">${san(0, -9, 27, 15, a)}${tang.canh.map(([x0, z0, x1, z1]) => san(x0, z0, x1, z1, a)).join("")}`;
      for (const p of [...tang.phong].sort((m, n) => (m.x0 - m.z1) - (n.x0 - n.z1))) {
        const bao = tt[p.ma];
        hinh += `<g class="phong ${p.phu ? "phu" : ""} ${bao?.muc || ""}" data-phong="${p.ma}" ${p.phu || !tuyChon.khiChon ? "" : `tabindex="0" role="button" aria-label="${esc(TEN_PHONG[p.ma] || p.ma)}"`}>
          <polygon class="san" points="${dd([a(p.x0, p.z0, 0), a(p.x1, p.z0, 0), a(p.x1, p.z1, 0), a(p.x0, p.z1, 0)])}"/>
          ${p.phu ? "" : `<polygon class="tuong" points="${dd([a(p.x0, p.z0, 0), a(p.x0, p.z1, 0), a(p.x0, p.z1, 2.3), a(p.x0, p.z0, 2.3)])}"/><polygon class="tuong t2" points="${dd([a(p.x0, p.z1, 0), a(p.x1, p.z1, 0), a(p.x1, p.z1, 2.3), a(p.x0, p.z1, 2.3)])}"/>`}
        </g>`;
      }
      for (const [x, z, w, s, h, c] of tang.do_dac().sort((m, n) => (m[0] - m[1]) - (n[0] - n[1]))) hinh += hop(x, z, w, s, h, c, t);
      for (const p of tang.phong) {
        if (p.phu) continue;
        const [tx, ty] = a(p.nx ?? p.x0 + 0.7, p.nz ?? p.z0 + 0.7, 0);
        hinh += `<text class="nhan-phong" transform="matrix(.866 .5 -.866 .5 ${tx.toFixed(1)} ${ty.toFixed(1)})">${esc(p.nhan)}</text>`;
        const bao = tt[p.ma];
        if (bao) {
          const [gx, gy] = a((p.x0 + p.x1) / 2, (p.z0 + p.z1) / 2, 0), r = bao.chu.length * co * 0.56 + (nho ? 28 : 20);
          ghim += `<g class="ghim ${bao.muc}" data-phong="${p.ma}" transform="translate(${gx.toFixed(1)} ${gy.toFixed(1)})">
            <ellipse class="song-a" rx="30" ry="15"/><ellipse class="song-a s2" rx="30" ry="15"/><ellipse class="tam" rx="4" ry="2"/>
            <line y2="-44"/><rect x="${(-r / 2).toFixed(1)}" y="${-44 - cao}" width="${r.toFixed(1)}" height="${cao}" rx="${cao / 2}"/><text y="${-44 - cao / 2 + co * 0.35}" text-anchor="middle" style="font-size:${co}px">${esc(bao.chu)}</text></g>`;
        }
      }
      hinh += `<text class="ten-tang" transform="matrix(.866 .5 -.866 .5 ${a(0.3, -12.6, 0).map((n) => n.toFixed(1)).join(" ")})">${esc(tang.ten)}</text></g>`;
    }
    // Học sinh đang mở app: chấm theo tọa độ thật app gửi lên (tang, x, z)
    for (const h of tuyChon.cham || []) {
      if (!h.tang || h.x == null || h.z == null) continue;
      const [cx, cy] = chieu(Math.min(30, Math.max(0, h.x)), Math.min(15, Math.max(-9, h.z)), 0, h.tang - 1), r = nho ? 15 : 9;
      ghim += `<g class="hs ${h.muc || ""}" transform="translate(${cx.toFixed(1)} ${cy.toFixed(1)})"><title>${esc(h.ten)}</title><circle r="${r}"/><text style="font-size:${r * 1.1}px">${esc(h.chu)}</text></g>`;
    }
    for (const [x, z] of [[0, 15], [0, -9], [27, -9], [27, 15]]) {
      const [a1, b1] = chieu(x, z, 2.3, 0), [a2, b2] = chieu(x, z, -0.7, 1);
      noi += `<line class="noi" x1="${a1.toFixed(1)}" y1="${b1.toFixed(1)}" x2="${a2.toFixed(1)}" y2="${b2.toFixed(1)}"/>`;
    }
    el.innerHTML = `<svg class="ban-do ${tuyChon.khiChon ? "chon-duoc" : ""}" viewBox="-372 -352 833 592" role="img" aria-label="Sơ đồ hai tầng phòng thí nghiệm ảo">${noi}${hinh}${ghim}</svg>`;
    if (!tuyChon.khiChon) return;
    const chon = (g) => {
      for (const n of el.querySelectorAll(".phong.chon")) n.classList.remove("chon");
      el.querySelector(`.phong[data-phong="${g.dataset.phong}"]`)?.classList.add("chon");
      tuyChon.khiChon(g.dataset.phong);
    };
    el.onclick = (e) => {
      const g = e.target.closest("[data-phong]");
      if (g && !g.classList.contains("phu")) chon(g);
    };
    el.onkeydown = (e) => {
      const g = e.target.closest("[data-phong]");
      if (g && (e.key === "Enter" || e.key === " ")) { e.preventDefault(); chon(g); }
    };
  }

  return { ve, TEN_PHONG };
})();
