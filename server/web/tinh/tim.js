// Hộp "Tìm hoặc hỏi AI" (Ctrl K). phien.js tải tệp này ở lần mở đầu tiên.
// Tìm trong dữ liệu thật mà tài khoản được xem, không phân biệt dấu. Học sinh không gọi được AI thử nên không có dòng hỏi AI.
const TimKiem = (() => {
  const { esc, icon, json, gui, loiChu } = Phien;
  const fold = (s) => String(s || "").normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/đ/g, "d").replace(/Đ/g, "D").toLowerCase();
  const lay = (u) => json(u).catch(() => null);
  const GOI_Y = `<p class="tim-trong">Thử gõ: dong sunfat, micropipet, PCR</p>`;
  let hop = null, kho = null;

  async function napKho() {
    const nd = await Phien.san, laGv = nd.vai_tro === "giao_vien";
    const [hc, dc, bai, may] = await Promise.all([laGv ? lay("/api/hoa-chat?moi_trang=800") : null, lay("/lab/sinh/huong-dan"), lay("/lab/sinh/bai-thi-nghiem"), lay("/may")]);
    return {
      laGv,
      ds: [
        ...(hc?.ds || []).map((c) => ({ loai: "Hóa chất", icon: "binh", ten: c.ten, phu: `${c.cong_thuc || ""} · ${c.vi_tri} · còn ${c.so_luong} ${c.don_vi}`, link: "/hoa#kho", tim: fold(`${c.ten} ${c.cong_thuc} ${c.vi_tri}`) })),
        ...(dc?.muc || []).map((c) => ({ loai: "Dụng cụ", icon: "tu", ten: c.ten, phu: c.phan_loai || "", link: laGv ? "/sinh" : "/hoc-sinh#dung-cu", tim: fold(`${c.ten} ${c.ma}`) })),
        ...(bai?.muc || []).map((b) => ({ loai: "Bài thí nghiệm", icon: "sinh", ten: b.ten, phu: `Lớp ${b.lop}`, link: laGv ? "/sinh" : "/hoc-sinh#bai", tim: fold(b.ten) })),
        ...(may || []).map((m) => ({ loai: "Máy", icon: "song", ten: m.ten, phu: `${m.so_buoc} bước vận hành`, link: laGv ? "/hoa#may" : "/hoc-sinh#may", tim: fold(m.ten) })),
      ],
    };
  }

  const dong = () => { hop?.remove(); hop = null; };

  async function mo() {
    if (hop) return;
    hop = document.createElement("div");
    hop.className = "tim-nen";
    hop.innerHTML = `<div class="tim-hop" role="dialog" aria-modal="true" aria-label="Tìm kiếm"><label>${icon("tim")}<input placeholder="Tìm hóa chất, dụng cụ, bài, máy…" aria-label="Tìm"></label><div class="tim-kq">${GOI_Y}</div></div>`;
    document.body.append(hop);
    hop.addEventListener("click", (e) => { if (e.target === hop) dong(); });
    const o = hop.querySelector("input"), kq = hop.querySelector(".tim-kq");
    o.focus();
    kho = kho || await napKho();
    if (!hop) return;
    if (kho.laGv) o.placeholder = "Tìm hóa chất, dụng cụ, bài, máy, hoặc đặt câu hỏi";

    const ve = () => {
      const tu = fold(o.value).split(/\s+/).filter(Boolean);
      if (!tu.length) { kq.innerHTML = GOI_Y; return; }
      const ds = kho.ds.filter((m) => tu.every((w) => m.tim.includes(w))).slice(0, 7);
      kq.innerHTML = ds.map((m) => `<a href="${m.link}">${icon(m.icon)}<span><b>${esc(m.ten)}</b><small>${esc(m.loai)} · ${esc(m.phu)}</small></span></a>`).join("")
        + (kho.laGv ? `<button type="button" class="tim-hoi">${icon("ai")}<span><b>Hỏi trợ lý AI: “${esc(o.value)}”</b><small>Trả lời qua mạng lưới khóa của máy chủ</small></span></button>` : "")
        + (!ds.length && !kho.laGv ? `<p class="tim-trong">Không thấy kết quả nào.</p>` : "");
      for (const a of kq.querySelectorAll("a")) a.addEventListener("click", dong);
      kq.querySelector(".tim-hoi")?.addEventListener("click", async () => {
        const cau = o.value;
        kq.innerHTML = `<p class="tim-trong">Đang hỏi trợ lý…</p>`;
        try {
          const r = await gui("/api/ai/thu", "POST", { cau_hoi: cau });
          kq.innerHTML = `<div class="tim-tl"><p>${esc(r.tra_loi)}</p><small>AI có thể sai, hãy kiểm tra lại trước khi dạy. ${esc(r.nha_cung_cap)} · ${esc(r.mo_hinh)}</small></div>`;
        } catch (loi) { kq.innerHTML = `<p class="tim-trong">${esc(loiChu(loi))}</p>`; }
      });
    };
    o.addEventListener("input", ve);
    ve();
  }

  addEventListener("keydown", (e) => { if (e.key === "Escape") dong(); });
  return { mo };
})();
TimKiem.mo();
