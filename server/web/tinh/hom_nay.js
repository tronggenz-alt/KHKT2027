// Trang chủ giáo viên: biến số liệu thật từ API thành việc cần làm, lỗi hay gặp, dòng sự kiện và bản đồ có ghim.
// Không có số nào viết sẵn: thiếu dữ liệu thì phần đó nói rõ là chưa có.
const HomNay = (() => {
  const { esc, icon } = Phien;
  const lay = (u) => Phien.json(u).catch(() => null);
  const truoc = (t) => {
    const s = Date.now() / 1000 - t;
    if (s < 60) return "vừa xong";
    if (s < 3600) return `${Math.floor(s / 60)} phút trước`;
    if (s < 86400) return `${Math.floor(s / 3600)} giờ trước`;
    if (s < 86400 * 30) return `${Math.floor(s / 86400)} ngày trước`;
    return new Date(t * 1000).toLocaleDateString("vi-VN");
  };
  const buoi = () => { const h = new Date().getHours(); return h < 11 ? "Sáng nay" : h < 14 ? "Trưa nay" : h < 18 ? "Chiều nay" : "Tối nay"; };
  const ngay = () => new Date().toLocaleDateString("vi-VN", { weekday: "long", day: "numeric", month: "long" }).replace(/^./, (c) => c.toUpperCase());
  const ten = (s) => String(s || "").split(" (")[0];
  const fold = (s) => String(s || "").normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/đ/g, "d").replace(/Đ/g, "D").toLowerCase();

  // Sự cố phòng Sinh ghi dạng "[dụng cụ] Lỗi (giải thích)"
  function tachLoi(s) {
    const m = /^\[([^\]]+)\]\s*(.+?)\s*\((.*)\)\s*$/s.exec(s.mo_ta || "");
    return m ? { ten: m[2], vi_sao: m[3] } : { ten: s.mo_ta || s.loai, vi_sao: s.xu_ly || "" };
  }

  function loiHayGap(suCo) {
    const nhom = new Map();
    for (const s of suCo) {
      if (s.trang_thai === "da_xu_ly") continue;
      const g = nhom.get(s.loai) || { ...tachLoi(s), phong: s.phong, so: 0, cuoi: 0 };
      g.so += 1;
      g.cuoi = Math.max(g.cuoi, s.thoi_gian);
      nhom.set(s.loai, g);
    }
    return [...nhom.values()].sort((a, b) => b.so - a.so || b.cuoi - a.cuoi);
  }

  function tinhViec(d) {
    const v = [], sp = d.sinh.sinh_pham, quaHan = new Set(sp.qua_han.map((x) => x.id));
    const sapHet = sp.sap_het.filter((x) => !quaHan.has(x.id));
    if (sp.qua_han.length) v.push({ muc: "khan", icon: "tam_giac", nhom: "Kho sinh phẩm", tieu: `Hủy ${sp.qua_han.length} lô sinh phẩm đã quá hạn`, chi: sp.qua_han.map((x) => x.ten).slice(0, 2).join("; "), nut: "Mở kho sinh phẩm", link: "/sinh" });
    if (d.loi.length) v.push({ muc: "khan", icon: "song", nhom: "Lỗi thao tác", tieu: `Xem ${d.loi.reduce((t, l) => t + l.so, 0)} lỗi thao tác chưa xử lý`, chi: `Nhiều nhất: ${d.loi[0].ten} (${d.loi[0].so} lần)`, nut: "Xem các lỗi", link: "/sinh" });
    for (const l of d.lop) {
      const n = l.ket_qua.filter((k) => k.diem === null).length;
      if (n) v.push({ muc: "nen", icon: "cham", nhom: l.ten, tieu: `Chấm ${n} bài của ${l.ten}`, chi: "Học sinh đã làm xong trong ứng dụng", nut: "Chấm bài", link: "#ket-qua" });
    }
    const roi = d.ketQua.filter((k) => k.diem === null && !k.hoc_sinh_id);
    if (roi.length) {
      const dem = {};
      for (const k of roi) dem[k.ma_bai] = (dem[k.ma_bai] || 0) + 1;
      const [ma, so] = Object.entries(dem).sort((a, b) => b[1] - a[1])[0];
      v.push({ muc: "nen", icon: "tep", nhom: "Kết quả phòng ảo", tieu: `${roi.length} kết quả chưa gắn tài khoản học sinh`, chi: `Nhiều nhất: ${d.tenBai[ma] || ma} (${so} bài). Chấm ở Phòng Sinh.`, nut: "Xem kết quả", link: "/sinh" });
    }
    if (sapHet.length) v.push({ muc: "nen", icon: "binh", nhom: "Kho sinh phẩm", tieu: `Bổ sung ${sapHet.length} sinh phẩm sắp hết`, chi: sapHet.map((x) => `${ten(x.ten)}: còn ${x.so_luong} ${x.don_vi}`).slice(0, 2).join("; "), nut: "Nhập kho", link: "/sinh" });
    if (sp.sap_het_han.length) v.push({ muc: "nen", icon: "tu", nhom: "Kho sinh phẩm", tieu: `${sp.sap_het_han.length} sinh phẩm hết hạn trong 30 ngày`, chi: sp.sap_het_han.map((x) => ten(x.ten)).slice(0, 2).join("; "), nut: "Xem hạn dùng", link: "/sinh" });
    if (d.hoa.sap_het) v.push({ muc: "nen", icon: "binh", nhom: "Kho hóa chất", tieu: `Nhập thêm ${d.hoa.sap_het} hóa chất sắp hết`, chi: d.hoa.sap_het_ds.map((c) => c.ten).slice(0, 2).join("; "), nut: "Mở kho", link: "/hoa#kho" });
    const hong = d.hoa.dung_cu_hong + d.sinh.dung_cu.hong;
    if (hong) v.push({ muc: "nen", icon: "tam_giac", nhom: "Dụng cụ", tieu: `Xử lý ${hong} dụng cụ hỏng`, chi: "", nut: "Xem dụng cụ", link: d.hoa.dung_cu_hong ? "/hoa#thiet-bi" : "/sinh" });
    if (d.hoa.khoa_api && !d.hoa.khoa_dung_duoc) v.push({ muc: "khan", icon: "ai", nhom: "Trợ lý AI", tieu: "Không còn khóa AI nào dùng được", chi: "Học sinh hỏi trợ lý sẽ không có câu trả lời", nut: "Mở mạng lưới khóa", link: "/hoa#khoa" });
    for (const l of d.lop) {
      const chua = l.hoc_sinh.filter((h) => h.phai_doi_mat_khau).length;
      if (chua) v.push({ muc: "ghi", icon: "nguoi", nhom: l.ten, tieu: `${chua} học sinh chưa đổi mật khẩu tạm`, chi: "", nut: "", link: "#lop" });
      if (l.ma_lop_mo) v.push({ muc: "ghi", icon: "lop", nhom: l.ten, tieu: "Mã lớp vẫn đang mở", chi: `${l.hoc_sinh.length} học sinh đã vào. Đóng mã khi đủ lớp.`, nut: "", link: "#lop" });
    }
    return v;
  }

  function tinhOn(d) {
    const o = [];
    if (!d.hoa.sap_het) o.push(`${d.hoa.so_hoa_chat} hóa chất đều trên mức tối thiểu`);
    if (!d.hoa.dung_cu_hong && !d.sinh.dung_cu.hong) o.push(`${(d.hoa.tong_dung_cu + d.sinh.dung_cu.tong).toLocaleString("vi-VN")} dụng cụ, không có cái nào hỏng`);
    if (d.hoa.khoa_api && d.hoa.khoa_dung_duoc === d.hoa.khoa_api) o.push(`${d.hoa.khoa_api}/${d.hoa.khoa_api} khóa AI sẵn sàng`);
    if (d.sinh.tu.every((t) => !t.canh_bao)) o.push("Các tủ bảo quản trong ngưỡng");
    return o;
  }

  const LOAI_DONG = { robot: ["robot", "Robot"], phan_tich: ["song", "Máy phân tích"], hien_tuong: ["binh", "Phản ứng"], kho: ["tu", "Kho"], ghi_chu: ["but", "Ghi chú"], su_co: ["tam_giac", "Lỗi thao tác"], ket_qua: ["tep", "Kết quả"], hoc_sinh: ["nguoi", ""] };
  function dongSuKien(d) {
    const a = [];
    for (const l of d.nhatKy) {
      const tieu = l.tieu_de.replace(/^Lệnh robot:\s*/, "");
      if (l.loai === "robot" && tieu.length < 12) continue;  // lệnh cụt như "đi đi" không giúp giáo viên hiểu gì
      a.push({ t: l.thoi_gian, loai: l.loai, tieu, canh: l.muc_do !== "Info" });
    }
    for (const s of d.suCo) a.push({ t: s.thoi_gian, loai: "su_co", tieu: tachLoi(s).ten, canh: true });
    for (const k of d.ketQua) {
      let tt = "";
      try { tt = JSON.parse(k.du_lieu).tom_tat || ""; } catch { /* không có tóm tắt */ }
      a.push({ t: k.thoi_gian, loai: "ket_qua", tieu: d.tenBai[k.ma_bai] || k.ma_bai, chi: tt, canh: false });
    }
    // Sự kiện của học sinh trong lớp: có tên người làm
    for (const s of d.lopSuKien) a.push({ t: s.thoi_gian, loai: "hoc_sinh", tieu: s.tieu_de, chi: s.chi_tiet, ai: s.ho_ten, canh: s.muc_do !== "thong_tin" });
    a.sort((x, y) => y.t - x.t);
    const gop = [];
    for (const e of a) {
      const cu = gop.find((g) => g.loai === e.loai && g.tieu === e.tieu && g.ai === e.ai);
      if (cu) cu.so += 1;
      else gop.push({ ...e, so: 1 });
    }
    return gop.slice(0, 8);
  }

  async function tai() {
    const [nd, hoa, sinh, lop, nhatKy, suCo, ketQua, khoa, sk, nhiet, bai] = await Promise.all([
      Phien.san, Phien.json("/api/tong-quan"), Phien.json("/lab/sinh/tong-quan"), lay("/api/lop"), lay("/api/nhat-ky?gioi_han=200"), lay("/lab/sinh/su-co"),
      lay("/lab/sinh/ket-qua"), lay("/api/khoa"), lay("/suc-khoe"), lay("/lab/sinh/nhiet-do"), lay("/lab/sinh/bai-thi-nghiem"),
    ]);
    const dsLop = await Promise.all((lop || []).map(async (l) => ({
      ...l, hoc_sinh: (await lay(`/api/lop/${l.id}/hoc-sinh`)) || [], ket_qua: (await lay(`/api/lop/${l.id}/ket-qua`)) || [], tt: await lay(`/api/lop/${l.id}/truc-tiep`),
    })));
    const d = {
      nd, hoa, sinh, lop: dsLop, nhatKy: nhatKy || [], suCo: suCo?.muc || [], ketQua: ketQua?.muc || [], khoa: khoa?.ds || [], sk, nhiet: nhiet?.muc || [],
      tenBai: Object.fromEntries((bai?.muc || []).map((b) => [b.ma, b.ten])),
      lopSuKien: dsLop.flatMap((l) => l.tt?.su_kien || []),
      hocSinhMo: dsLop.flatMap((l) => (l.tt?.hoc_sinh || []).filter((h) => h.dang_mo)),
      soMay: dsLop.reduce((t, l) => t + (l.tt?.so_hoc_sinh || 0), 0),
    };
    d.loi = loiHayGap(d.suCo);
    d.viec = tinhViec(d);
    d.on = tinhOn(d);
    d.dong = dongSuKien(d);
    d.tinCuoi = Math.max(0, ...d.suCo.map((s) => s.thoi_gian), ...d.ketQua.map((k) => k.thoi_gian), ...d.nhatKy.map((n) => n.thoi_gian), ...d.lopSuKien.map((s) => s.thoi_gian));
    d.demNk = {};
    for (const n of d.nhatKy) d.demNk[n.loai] = (d.demNk[n.loai] || 0) + 1;
    return d;
  }

  // ------------------------------- Vẽ -------------------------------
  const MUC_CHU = { khan: "Cần xử lý", nen: "Nên làm", ghi: "Ghi nhớ" };
  const veViec = (ds) => ds.map((v) => `
    <a class="viec ${v.muc}" href="${v.link}">
      <span class="viec-ic">${icon(v.icon)}</span>
      <span class="viec-than"><small>${esc(v.nhom)} · ${MUC_CHU[v.muc]}</small><b>${esc(v.tieu)}</b>${v.chi ? `<em>${esc(v.chi)}</em>` : ""}</span>
      ${v.nut ? `<span class="viec-nut">${esc(v.nut)} →</span>` : ""}
    </a>`).join("");

  const veLoi = (ds) => ds.length ? ds.map((l) => `
    <article class="loi"><div class="loi-so"><b>${l.so}</b><span>lần</span></div>
      <div><h3>${esc(l.ten)}</h3><p>${esc(l.vi_sao)}</p><small>${esc(BanDo.TEN_PHONG[l.phong] || l.phong)} · lần cuối ${truoc(l.cuoi)}</small></div></article>`).join("")
    : `<p class="phu-chu">Chưa có lỗi thao tác nào chưa xử lý.</p>`;

  const veDong = (ds) => ds.length ? ds.map((e) => {
    const [ic, nhan] = LOAI_DONG[e.loai] || ["but", e.loai];
    return `<li class="dong ${e.canh ? "canh" : ""}"><span class="dong-ic">${icon(ic)}</span>
      <div><b>${e.ai ? `${esc(e.ai)}: ` : ""}${esc(e.tieu)}</b>${e.so > 1 ? `<i>×${e.so}</i>` : ""}${e.chi ? `<em>${esc(e.chi)}</em>` : ""}<small>${esc(e.ai ? "Học sinh" : nhan)} · ${truoc(e.t)}</small></div></li>`;
  }).join("") : `<li class="phu-chu">Chưa có hoạt động nào.</li>`;

  const TEN_TU = { tu_mat_4: ["Tủ mát", 2, 8], tu_dong_am20: ["Tủ đông", -25, -15], tu_am_37: ["Tủ ấm", 35, 39] };
  const veTu = (d) => d.sinh.tu.map((t) => {
    const [tenTu, thap, cao] = TEN_TU[t.tu] || [t.tu, 0, 1];
    const ls = d.nhiet.filter((n) => n.tu === t.tu).sort((a, b) => a.thoi_gian - b.thoi_gian).map((n) => n.nhiet_do);
    const lo = Math.min(thap, ...ls), hi = Math.max(cao, ...ls), y = (v) => (26 - (v - lo) / (hi - lo) * 26).toFixed(1);
    const duong = ls.map((v, i) => `${(i / Math.max(1, ls.length - 1) * 100).toFixed(1)},${y(v)}`).join(" ");
    return `<div class="tu ${t.canh_bao ? "canh" : ""}"><div class="tu-chu"><span>${tenTu}</span><b>${String(t.nhiet_do).replace(".", ",")} °C</b></div>
      <svg viewBox="0 -2 100 30" preserveAspectRatio="none" aria-hidden="true"><rect x="0" y="${y(cao)}" width="100" height="${(y(thap) - y(cao)).toFixed(1)}" class="tu-nguong"/><polyline points="${duong}" vector-effect="non-scaling-stroke" class="tu-duong"/></svg>
      <small>Ngưỡng ${thap} đến ${cao} °C · ${ls.length} lần đo</small></div>`;
  }).join("");

  const TEN_NCC = { groq: "Groq", gemini: "Gemini", deepseek: "DeepSeek" };
  function veKhoaAi(d) {
    if (!d.khoa.length) return `<p class="phu-chu">Chưa có khóa nào. Thêm khóa ở <a href="/hoa#khoa">Mạng lưới khóa AI</a>.</p>`;
    const tong = d.khoa.reduce((t, k) => t + k.so_lan_goi, 0);
    return `<ul class="khoa-ds">${d.khoa.map((k) => {
      const ok = k.bat && !k.dang_nghi_giay && k.trang_thai !== "khoa_sai", ncc = TEN_NCC[k.nha_cung_cap] || k.nha_cung_cap;
      return `<li class="${ok ? "ok" : ""}"><i></i><span>${esc(k.nhan || ncc)}</span><small>${esc(ncc)} · ${k.so_lan_goi} lần gọi${k.tre_ms ? ` · ${(k.tre_ms / 1000).toFixed(1).replace(".", ",")} s` : ""}</small><u style="--t:${tong ? Math.round(k.so_lan_goi / tong * 100) : 0}%"></u></li>`;
    }).join("")}</ul>`;
  }

  function trangThaiPhong(d) {
    const tt = {}, dem = {};
    for (const s of d.suCo) if (s.trang_thai !== "da_xu_ly") dem[s.phong] = (dem[s.phong] || 0) + 1;
    for (const [ma, so] of Object.entries(dem)) tt[ma] = { muc: "do", chu: `${so} lỗi` };
    const sp = d.sinh.sinh_pham, qh = new Set(sp.qua_han.map((x) => x.id));
    const canXem = qh.size + sp.sap_het.filter((x) => !qh.has(x.id)).length;
    if (canXem && !tt.kho_sinh_pham) tt.kho_sinh_pham = { muc: "vang", chu: `${canXem} lô cần xem` };
    if (d.hoa.sap_het) tt.kho_hoa_chat = { muc: "vang", chu: `${d.hoa.sap_het} sắp hết` };
    if (d.demNk.robot && !tt.phong_robot) tt.phong_robot = { muc: "lam", chu: `${d.demNk.robot} lệnh robot` };
    if (d.demNk.phan_tich && !tt.phong_may) tt.phong_may = { muc: "lam", chu: `${d.demNk.phan_tich} lần phân tích` };
    return tt;
  }

  function chiTietPhong(d, ma) {
    const sp = d.sinh.sinh_pham, qh = new Set(sp.qua_han.map((x) => x.id)), dong = [];
    const trong = d.hocSinhMo.filter((h) => h.khu === ma);
    if (trong.length) dong.push(["Học sinh đang ở đây", trong.map((h) => h.ho_ten).join(", ")]);
    if (ma === "kho_hoa_chat") dong.push(["Hóa chất trong kho", `${d.hoa.so_hoa_chat} chất, ${d.hoa.so_ke} kệ`], ["Sắp hết", `${d.hoa.sap_het}`]);
    if (ma === "phong_lab") dong.push(["Lần phản ứng đã ghi", `${d.demNk.hien_tuong || 0}`]);
    if (ma === "phong_may") dong.push(["Lần phân tích đã ghi", `${d.demNk.phan_tich || 0}`]);
    if (ma === "phong_robot") dong.push(["Lệnh robot đã nhận", `${d.demNk.robot || 0}`]);
    for (const l of d.loi.filter((x) => x.phong === ma)) dong.push([l.ten, `${l.so} lần`]);
    if (ma === "kho_sinh_pham") {
      for (const x of sp.qua_han) dong.push([ten(x.ten), "Quá hạn"]);
      for (const x of sp.sap_het.filter((x) => !qh.has(x.id)).slice(0, 4)) dong.push([ten(x.ten), `Còn ${x.so_luong} ${x.don_vi}`]);
    }
    return `<h3>${esc(BanDo.TEN_PHONG[ma] || ma)}</h3>${dong.length ? `<ul>${dong.map(([a, b]) => `<li><span>${esc(a)}</span><b>${esc(b)}</b></li>`).join("")}</ul>` : `<p>Phòng ảo chưa gửi số liệu nào về phòng này.</p>`}`;
  }

  async function veKho(el) {
    const [p, tq] = await Promise.all([lay("/api/hoa-chat?moi_trang=800"), lay("/api/tong-quan")]);
    if (!p || !tq) { el.innerHTML = `<p class="phu-chu">Không tải được kho.</p>`; return; }
    const ke = tq.ke.map((k) => ({ ...k, o: [] })), theoMa = new Map(ke.map((k) => [k.ma, k]));
    for (const c of p.ds) {
      const m = /^(K\d+)-(\d+)-(\d+)/.exec(c.vi_tri || "");
      if (m && theoMa.has(m[1])) theoMa.get(m[1]).o.push({ ...c, tang: +m[2], so_o: +m[3] });
    }
    el.innerHTML = `
      <label class="kho-tim">${icon("tim")}<input type="search" placeholder="Tìm trong ${p.ds.length} chất, không cần dấu: dong sunfat, NaOH, K07" aria-label="Tìm hóa chất"></label>
      <div class="kho-luoi">${ke.map((k) => `<div class="ke" title="${esc(k.ma)}: ${esc(k.ten)}"><div class="ke-o" style="--cot:${k.so_o};--hang:${k.so_tang}">${k.o.map((c) => `<i data-stt="${c.stt}" class="${c.sap_het ? "thieu" : ""}" style="grid-row:${c.tang};grid-column:${c.so_o};background:${esc(tq.nhom[c.nhom]?.mau || "#888")}"></i>`).join("")}</div><span>${esc(k.ma)}</span></div>`).join("")}</div>
      <p class="kho-chi" aria-live="polite">Di chuột hoặc chạm vào một ô để xem chất đó. Ô viền đỏ là chất sắp hết.</p>`;
    const theoStt = new Map(p.ds.map((c) => [String(c.stt), c])), chi = el.querySelector(".kho-chi"), luoi = el.querySelector(".kho-luoi"), o = el.querySelector("input");
    const xem = (e) => {
      const n = e.target.closest("i[data-stt]");
      if (!n) return;
      const c = theoStt.get(n.dataset.stt), nhom = tq.nhom[c.nhom];
      chi.innerHTML = `<b>${esc(c.ten)}</b>${c.cong_thuc ? ` · ${esc(c.cong_thuc)}` : ""} · ${esc(nhom?.ten || "")} · vị trí ${esc(c.vi_tri)} · còn ${c.so_luong.toLocaleString("vi-VN")} ${esc(c.don_vi)}`;
    };
    luoi.addEventListener("pointerover", xem);
    luoi.addEventListener("click", xem);
    o.addEventListener("input", () => {
      const tu = fold(o.value).split(/\s+/).filter(Boolean);
      luoi.classList.toggle("dang-tim", tu.length > 0);
      let thay = 0, dau = null;
      for (const n of luoi.querySelectorAll("i")) {
        const c = theoStt.get(n.dataset.stt);
        const hop = tu.length > 0 && tu.every((w) => `${fold(c.ten)} ${(c.cong_thuc || "").toLowerCase()} ${c.vi_tri.toLowerCase()}`.includes(w));
        n.classList.toggle("trung", hop);
        if (hop) { thay += 1; dau = dau || c; }
      }
      if (tu.length) chi.innerHTML = thay ? `Thấy <b>${thay}</b> chất. Đầu tiên: <b>${esc(dau.ten)}</b>, vị trí ${esc(dau.vi_tri)}, còn ${dau.so_luong.toLocaleString("vi-VN")} ${esc(dau.don_vi)}` : "Không thấy chất nào.";
    });
  }

  // ------------------------------- Ghép trang -------------------------------
  let phongChon = null, daKho = false, henGio = null;

  function veBanDo(khung, d) {
    const cham = d.hocSinhMo.map((h) => ({ tang: h.tang, x: h.x, z: h.z, ten: `${h.ho_ten}: ${h.ten_khu || ""}`, chu: (h.ho_ten.trim().split(/\s+/).pop() || "?")[0].toUpperCase(), muc: h.so_nguy_hiem ? "do" : h.so_canh_bao ? "vang" : "" }));
    BanDo.ve(khung.querySelector("#ban-do"), { trangThai: trangThaiPhong(d), cham, khiChon: (ma) => { phongChon = ma; khung.querySelector("#chi-tiet").innerHTML = chiTietPhong(d, ma); } });
    phongChon = phongChon || d.suCo.find((s) => s.trang_thai !== "da_xu_ly")?.phong || null;
    if (phongChon) {
      khung.querySelector(`#ban-do .phong[data-phong="${phongChon}"]`)?.classList.add("chon");
      khung.querySelector("#chi-tiet").innerHTML = chiTietPhong(d, phongChon);
    }
  }

  function veDau(khung, d) {
    const soCan = d.viec.filter((v) => v.muc !== "ghi").length, tongLoi = d.loi.reduce((t, l) => t + l.so, 0), tom = [];
    if (tongLoi) tom.push(`<b>${tongLoi} lỗi thao tác</b> chưa xử lý, nhiều nhất là “${esc(d.loi[0].ten)}”.`);
    if (d.sinh.sinh_pham.qua_han.length) tom.push(`<b>${d.sinh.sinh_pham.qua_han.length} lô sinh phẩm</b> đã quá hạn.`);
    if (d.hocSinhMo.length) tom.push(`<b>${d.hocSinhMo.length} học sinh</b> đang mở app.`);
    khung.querySelector("#hn-tieu").innerHTML = soCan ? `${buoi()} có <b>${soCan} việc</b> cần bạn` : `${buoi()} không có gì cần xử lý`;
    khung.querySelector("#hn-tom").innerHTML = tom.join(" ");
    khung.querySelector("#den").innerHTML = `
      <span class="den"><i class="${d.hoa.khoa_dung_duoc ? "ok" : ""}"></i>AI ${d.hoa.khoa_dung_duoc}/${d.hoa.khoa_api} khóa</span>
      <span class="den"><i class="${d.hocSinhMo.length ? "ok" : ""}"></i>${d.hocSinhMo.length}/${d.soMay} học sinh đang mở app</span>
      <span class="den"><i class="${Date.now() / 1000 - d.tinCuoi < 300 ? "ok" : ""}"></i>Phòng ảo: tin cuối ${d.tinCuoi ? truoc(d.tinCuoi) : "chưa có"}</span>`;
  }

  function veDong2(khung, d) {
    khung.querySelector("#dong").innerHTML = veDong(d.dong);
    khung.querySelector("#tin-cuoi").textContent = d.tinCuoi ? `tin cuối ${truoc(d.tinCuoi)}` : "";
  }

  async function ve(khung) {
    const d = await tai();
    khung.innerHTML = `
      <header class="hn-dau">
        <div><p class="hn-chao">Chào ${esc(d.nd.ho_ten)} · ${ngay()}</p><h1 id="hn-tieu"></h1><p class="hn-tom" id="hn-tom"></p></div>
        <div class="den-hang" id="den"></div>
      </header>
      <div class="hn-luoi">
        <section class="the o-ban-do">
          <div class="the-dau"><h2>Phòng thí nghiệm lúc này</h2>
            <div class="chu-giai"><span><i style="background:var(--do)"></i>Có lỗi</span><span><i style="background:var(--vang)"></i>Cần xem</span><span><i style="background:var(--lam)"></i>Có hoạt động</span><span><i style="background:var(--nhan)"></i>Học sinh đang mở app</span></div></div>
          <div id="ban-do"></div>
          <div class="the-phong" id="chi-tiet"><p>Chạm vào một phòng để xem trong đó có gì.</p></div>
          <p class="ghi-nho">Phòng đúng kích thước trong Unity, đồ đạc vẽ giản lược. Tầng 2 tách lên trên để nhìn được cả hai tầng.</p>
        </section>
        <section class="the hn-viec">
          <div class="the-dau"><h2>Cần bạn làm</h2><small>${d.viec.length} mục, tính từ số liệu thật</small></div>
          <div class="ds-viec" id="viec"></div>
          <ul class="ds-on">${d.on.map((s) => `<li>${icon("dung")}${esc(s)}</li>`).join("")}</ul>
        </section>
        <section class="the hn-loi">
          <h2>Học sinh hay sai ở đâu</h2>
          <p class="dan-loi">Ở phòng thật, giáo viên khó thấy hết từng thao tác của cả lớp. Phòng ảo ghi lại từng lần và giải thích vì sao đó là lỗi, nên biết phải dạy lại điều gì.</p>
          <div class="ds-loi">${veLoi(d.loi)}</div>
        </section>
        <section class="the hn-dong">
          <div class="the-dau"><h2>Dòng sự kiện từ phòng ảo</h2><small id="tin-cuoi"></small></div>
          <ul id="dong"></ul>
        </section>
        <section class="the">
          <div class="the-dau"><h2>Kho hóa chất, nhìn một lần</h2><small>${d.hoa.so_hoa_chat} chất trên ${d.hoa.so_ke} kệ, mỗi ô một chất, màu theo nhóm</small></div>
          <div id="kho"><p class="phu-chu">Đang tải…</p></div>
        </section>
        <section class="the hn-phu">
          <div><h2>Tủ bảo quản trong phòng ảo</h2><div class="ds-tu">${veTu(d)}</div></div>
          <div><h2>Mạng lưới khóa AI</h2>${veKhoaAi(d)}</div>
        </section>
      </div>`;
    veDau(khung, d);
    const hien = d.viec.slice(0, 4), con = d.viec.length - hien.length;
    khung.querySelector("#viec").innerHTML = veViec(hien) + (con > 0 ? `<button type="button" class="them-viec">Xem thêm ${con} mục</button>` : "");
    khung.querySelector(".them-viec")?.addEventListener("click", () => { khung.querySelector("#viec").innerHTML = veViec(d.viec); });
    veDong2(khung, d);
    veBanDo(khung, d);
    daKho = false;
    if (!daKho) { daKho = true; veKho(khung.querySelector("#kho")); }

    // Cập nhật số học sinh đang mở app, ghim và dòng sự kiện mỗi 10 giây khi tab đang mở
    clearInterval(henGio);
    henGio = setInterval(async () => {
      if (document.hidden || !khung.classList.contains("dang-mo") || document.querySelector(".tim-nen")) return;
      try {
        const moi = await tai();
        veDau(khung, moi);
        veDong2(khung, moi);
        veBanDo(khung, moi);
      } catch { /* lần sau thử lại */ }
    }, 10000);
  }

  return { ve };
})();
