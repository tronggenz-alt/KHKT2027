/* Phòng thí nghiệm Hoá học ảo – HÌNH HỌC DỤNG CỤ THUỶ TINH dùng chung (không DOM, không three.js).
   Nguồn sự thật cho hình dạng: bản Web 3D (ve-web.js) dựng BufferGeometry từ đây; bản Unity (LabHinh.cs) là bản chuyển
   CÙNG thuật toán sang C# – sửa hình ở đây thì sửa cả hai nơi (số liệu dụng cụ nằm trong DUNG_CU).
   UMD: trình duyệt → window.PTNHinhHoc · Node → require("…/hinh-hoc.js").

   Vì sao có file này (lỗi cũ): profile chỉ vài điểm nối bằng đoạn thẳng → bờ vai bình gãy khúc; Unity còn tính pháp tuyến bằng
   RecalculateNormals trên đỉnh nhân đôi ở đường may → một vạch sáng/tối chạy dọc thân bình.
   Cách sửa: (1) profile điều khiển được BO GÓC bằng cung Bézier bậc 2 liền tiếp tuyến (G1); (2) pháp tuyến tính theo công thức từ
   tiếp tuyến profile, đỉnh đường may có cùng pháp tuyến; (3) thành thuỷ tinh có độ dày thật + gờ miệng bo tròn.

   Quy ước profile: danh sách điểm [x, y] đi từ TÂM ĐÁY (x = 0) ra ngoài rồi lên miệng, đơn vị mét; pháp tuyến hướng ra ngoài
   vật liệu là (dy, −dx).
     [x, y]         điểm trơn (pháp tuyến lấy trung bình hai đoạn kề)
     [x, y, rho>0]  góc được bo bằng cung bán kính ≈ rho (tự co lại nếu đoạn kề quá ngắn)
     [x, y, 0]      góc NHỌN thật (đỉnh được tách đôi để pháp tuyến không bị nhoè) */
(function (goc, nhaMay) {
  if (typeof module === "object" && module.exports) module.exports = nhaMay();
  else goc.PTNHinhHoc = nhaMay();
})(typeof self !== "undefined" ? self : this, function () {
  "use strict";

  const EPS = 1e-9;
  /** Bề dày thành thuỷ tinh (m) – cũng là khoảng cách lòng bình ↔ mặt chất lỏng. */
  const DAY_KINH = 0.0022;

  /** Profile điều khiển → chuỗi điểm dày {x, y, sac}. `soDoanCung` = số đoạn mỗi cung bo. */
  function muotProfile(ctrl, soDoanCung = 8) {
    const kq = [];
    const them = (x, y, sac) => {
      const c = kq[kq.length - 1];
      if (c && Math.hypot(c.x - x, c.y - y) < EPS) { if (sac) c.sac = true; return; }
      kq.push({ x, y, sac: !!sac });
    };
    const n = ctrl.length;
    them(ctrl[0][0], ctrl[0][1], false);
    for (let i = 1; i < n - 1; i++) {
      const [px, py, rho] = ctrl[i];
      if (rho === undefined) { them(px, py, false); continue; }
      if (rho === 0) { them(px, py, true); continue; }
      const ax = ctrl[i - 1][0] - px, ay = ctrl[i - 1][1] - py, bx = ctrl[i + 1][0] - px, by = ctrl[i + 1][1] - py;
      const la = Math.hypot(ax, ay), lb = Math.hypot(bx, by);
      if (la < EPS || lb < EPS || Math.abs(ax * by - ay * bx) < 1e-12 * la * lb) { them(px, py, false); continue; }
      const t = Math.min(rho, la / 2, lb / 2);
      const x0 = px + (ax / la) * t, y0 = py + (ay / la) * t, x2 = px + (bx / lb) * t, y2 = py + (by / lb) * t;
      for (let k = 0; k <= soDoanCung; k++) {
        const s = k / soDoanCung, u = 1 - s;
        them(u * u * x0 + 2 * u * s * px + s * s * x2, u * u * y0 + 2 * u * s * py + s * s * y2, false);
      }
    }
    them(ctrl[n - 1][0], ctrl[n - 1][1], false);
    return kq;
  }

  const phapDoan = (a, b) => {
    const dx = b.x - a.x, dy = b.y - a.y, l = Math.hypot(dx, dy);
    return l < EPS ? [0, 0] : [dy / l, -dx / l];
  };

  /** Đỉnh của lát cắt: {x, y, nx, ny, noi} – `noi` = có nối mặt với đỉnh kế tiếp (đỉnh tách đôi ở góc nhọn thì KHÔNG nối). */
  function dinhLatCat(pts) {
    const m = pts.length, dinh = [];
    for (let i = 0; i < m; i++) {
      const tr = i > 0 ? phapDoan(pts[i - 1], pts[i]) : null;
      const sau = i < m - 1 ? phapDoan(pts[i], pts[i + 1]) : null;
      const p = pts[i];
      if (!tr || !sau) { const nn = tr || sau; dinh.push({ x: p.x, y: p.y, nx: nn[0], ny: nn[1], noi: true }); continue; }
      if (p.sac) {
        dinh.push({ x: p.x, y: p.y, nx: tr[0], ny: tr[1], noi: false });
        dinh.push({ x: p.x, y: p.y, nx: sau[0], ny: sau[1], noi: true });
        continue;
      }
      let nx = tr[0] + sau[0], ny = tr[1] + sau[1];
      const l = Math.hypot(nx, ny);
      if (l < EPS) { nx = sau[0]; ny = sau[1]; } else { nx /= l; ny /= l; }
      dinh.push({ x: p.x, y: p.y, nx, ny, noi: true });
    }
    return dinh;
  }

  /** Mặt tròn xoay quanh trục y. Trả {v, n, uv, i} (Float32Array / Uint32Array), đường may có CÙNG vị trí và pháp tuyến. */
  function latheMem(ctrl, { phan = 64, soDoanCung = 8 } = {}) {
    const dinh = dinhLatCat(muotProfile(ctrl, soDoanCung));
    const K = dinh.length, hang = phan + 1;
    const v = new Float32Array(K * hang * 3), nn = new Float32Array(K * hang * 3), uv = new Float32Array(K * hang * 2);
    let tong = 0;
    const dai = [0];
    for (let k = 1; k < K; k++) { tong += Math.hypot(dinh[k].x - dinh[k - 1].x, dinh[k].y - dinh[k - 1].y); dai.push(tong); }
    for (let k = 0; k < K; k++) {
      const d = dinh[k];
      for (let j = 0; j <= phan; j++) {
        const a = (j * 2 * Math.PI) / phan, s = Math.sin(a), c = Math.cos(a), o = k * hang + j;
        v[o * 3] = d.x * s; v[o * 3 + 1] = d.y; v[o * 3 + 2] = d.x * c;
        nn[o * 3] = d.nx * s; nn[o * 3 + 1] = d.ny; nn[o * 3 + 2] = d.nx * c;
        uv[o * 2] = j / phan; uv[o * 2 + 1] = tong > 0 ? dai[k] / tong : 0;
      }
    }
    const idx = [];
    for (let k = 0; k < K - 1; k++) {
      if (!dinh[k].noi) continue;
      for (let j = 0; j < phan; j++) {
        const a = k * hang + j, b = a + hang;
        idx.push(a, a + 1, b, a + 1, b + 1, b);
      }
    }
    return { v, n: nn, uv, i: Uint32Array.from(idx) };
  }

  /** Dịch điểm i của profile điều khiển vào phía trong một đoạn `day` (đường xiên góc – miter, giới hạn 3×). */
  function dichVao(ctrl, i, day) {
    const n = ctrl.length, p = ctrl[i];
    const A = { x: ctrl[Math.max(0, i - 1)][0], y: ctrl[Math.max(0, i - 1)][1] }, B = { x: ctrl[Math.min(n - 1, i + 1)][0], y: ctrl[Math.min(n - 1, i + 1)][1] };
    const P = { x: p[0], y: p[1] };
    const tr = i > 0 ? phapDoan(A, P) : phapDoan(P, B), sau = i < n - 1 ? phapDoan(P, B) : tr;
    let mx = tr[0] + sau[0], my = tr[1] + sau[1];
    const l = Math.hypot(mx, my);
    if (l < EPS) { mx = sau[0]; my = sau[1]; } else { mx /= l; my /= l; }
    const cos = Math.max(0.33, mx * sau[0] + my * sau[1]);
    return [p[0] - (mx * day) / cos, p[1] - (my * day) / cos];
  }

  /** Profile NGOÀI của dụng cụ → vòng kín của THÀNH thuỷ tinh: vách ngoài ↑ miệng (gờ bo tròn) ↓ vách trong → đáy trong.
      `dayDay` = bề dày đáy (m). Đầu vào đi từ tâm đáy (0, 0) tới miệng (điểm cuối). */
  function thanhThuyTinh(ngoai, day = DAY_KINH, dayDay) {
    const n = ngoai.length, rl = day * 0.46;
    const phang = n > 1 && Math.abs(ngoai[1][1] - ngoai[0][1]) < EPS; // đáy phẳng dày hơn thành; đáy tròn dày bằng thành
    if (dayDay === undefined) dayDay = phang ? day * 1.7 : day;
    const trong = ngoai.map((p, i) => {
      const [x, y] = dichVao(ngoai, i, day);
      const rho = p[2] === undefined ? undefined : p[2] === 0 ? 0 : Math.max(p[2] - day, 0.0006);
      return [Math.max(0, x), y, rho];
    });
    // đáy trong: nâng lên theo bề dày đáy
    trong[0] = [0, dayDay];
    if (phang) trong[1][1] = dayDay;
    // miệng: mặt trên phẳng, hai mép bo tròn
    const mieng = ngoai[n - 1], xt = trong[n - 1][0];
    const vong = ngoai.slice(0, n - 1).map((p) => p.slice());
    vong.push([mieng[0], mieng[1], rl], [xt, mieng[1], rl]);
    for (let i = n - 2; i >= 1; i--) vong.push(trong[i]);
    vong.push([0, dayDay]);
    return vong;
  }

  /** Bán kính LÒNG bình (trong) tại độ cao y theo profile ngoài dày `pts` (kết quả muotProfile), trừ bề dày thành theo độ xiên. */
  function banKinhTai(pts, y, day = DAY_KINH) {
    for (let i = 1; i < pts.length; i++) {
      const a = pts[i - 1], b = pts[i], dy = b.y - a.y;
      if (Math.abs(dy) < EPS || y < Math.min(a.y, b.y) || y > Math.max(a.y, b.y)) continue;
      const t = (y - a.y) / dy, dxdy = (b.x - a.x) / dy;
      return Math.max(0.0005, a.x + (b.x - a.x) * t - day * Math.sqrt(1 + dxdy * dxdy));
    }
    return Math.max(0.0005, pts[pts.length - 1].x - day);
  }

  /** Profile điều khiển của KHỐI chất lỏng cao h trong bình (profile ngoài dày `pts`), mặt khum lõm – dùng với latheMem. */
  function chatLong(pts, h, day = DAY_KINH) {
    h = Math.max(0.006, h);
    const y0 = 0.003, p = [[0, y0]];
    const N = 14;
    for (let i = 0; i <= N; i++) { const y = y0 + ((h - y0) * i) / N; p.push([banKinhTai(pts, y, day), y]); }
    const rw = banKinhTai(pts, h, day), m = Math.min(0.004, rw * 0.18);
    p[p.length - 1] = [rw, h + m];
    for (let i = 1; i <= 8; i++) { const x = rw * (1 - i / 8); p.push([Math.max(0, x), h + m * Math.pow(x / rw, 4)]); }
    p[1] = [p[1][0], p[1][1], 0.003]; // góc đáy lòng bình bo nhẹ (khớp đáy kính bo)
    return p;
  }

  function theTichDenH(pts, h, day = DAY_KINH, buoc = 0.0005) {
    let v = 0;
    for (let y = 0.003; y < h; y += buoc) { const dy = Math.min(buoc, h - y), r = banKinhTai(pts, y + dy / 2, day); v += Math.PI * r * r * dy; }
    return v * 1e6; // mL
  }

  function hDeCoTheTich(pts, mL, day, hMax) {
    let a = 0.003, b = hMax;
    for (let i = 0; i < 30; i++) { const g = (a + b) / 2; if (theTichDenH(pts, g, day) < mL) a = g; else b = g; }
    return (a + b) / 2;
  }

  /** Cung tròn tâm (cx, cy), bán kính r, từ góc a0 → a1 (độ) thành các điểm trơn – dùng cho đáy tròn ống nghiệm, bình cầu. */
  function cungTron(cx, cy, r, a0, a1, soDiem = 12) {
    const p = [];
    for (let i = 0; i <= soDiem; i++) { const a = ((a0 + ((a1 - a0) * i) / soDiem) * Math.PI) / 180; p.push([cx + r * Math.cos(a), cy + r * Math.sin(a)]); }
    return p;
  }

  /** Số liệu hình dạng dụng cụ (m) – profile NGOÀI, từ tâm đáy tới miệng. */
  const DUNG_CU = {
    /** Cốc có mỏ: thân trụ, đáy bo, miệng loe nhẹ. */
    coc: (r, h) => [[0, 0], [r, 0, 0.006], [r, h - 0.012, 0.01], [r * 1.05, h]],
    /** Bình tam giác (Erlenmeyer): đáy bo, thân CÔN thẳng, vai bo lớn, cổ trụ, miệng loe. */
    binhTamGiac: (r, h, rc, hc) => [[0, 0], [r, 0, 0.011], [rc, hc, 0.028], [rc, h - 0.012, 0.007], [rc * 1.18, h]],
    /** Ống nghiệm: đáy bán cầu, miệng loe có gờ. */
    ongNghiem: (r, h) => [[0, 0], ...cungTron(0, r, r, -90, 0, 14).slice(1), [r, h - 0.01, 0.005], [r * 1.14, h]],
    /** Lọ thuốc thử cổ hẹp (vai tròn). */
    loThuoc: (r, h, rc) => [[0, 0], [r, 0, 0.006], [r, h * 0.58, 0.022], [rc, h * 0.8, 0.012], [rc, h - 0.008, 0.003], [rc * 1.12, h]],
    /** Đèn cồn: thân phình tròn, cổ thon, miệng. */
    denCon: () => [[0, 0], [0.042, 0, 0.009], [0.046, 0.03, 0.02], [0.044, 0.05, 0.022], [0.016, 0.07, 0.016], [0.012, 0.086, 0.004], [0.0125, 0.098]],
    /** Phễu: nón trên + cuống. */
    pheu: () => [[0, 0], [0.004, 0], [0.004, 0.05, 0.003], [0.035, 0.09, 0.006], [0.04, 0.12]],
  };

  return { DAY_KINH, EPS, muotProfile, dinhLatCat, latheMem, thanhThuyTinh, banKinhTai, chatLong, theTichDenH, hDeCoTheTich, cungTron, DUNG_CU };
});
