/* Phòng thí nghiệm Hoá học ảo – LÕI MÔ PHỎNG (máy trạng thái từng thí nghiệm). Không DOM, không vẽ.
   Renderer (Web 3D: ve-web.js · Unity: cau-unity.js) chỉ: (a) vẽ theo "trang_thai" nhận được, (b) gửi thao tác về đây qua thaoTac().
   Mọi con số hoá học lấy từ tinh-toan.js – không renderer nào tự tính lại.
   UMD: trình duyệt → window.PTNMoPhong (nạp tinh-toan.js trước) · Node → require("…/mo-phong.js").

   const mp = PTNMoPhong.tao(duLieuThiNghiem, { bangIon, ngauNhien, cheDo });
   mp.on("trang_thai", fn) · mp.on("so_tay", fn) · mp.on("hieu_ung", fn)   (trả hàm huỷ đăng kí)
   mp.thaoTac("ten_lenh", {...}) → { ok, thong_bao? }     mp.capNhat(dt_giay)     mp.trangThai()     mp.soTay() */
(function (goc, nhaMay) {
  if (typeof module === "object" && module.exports) module.exports = nhaMay(require("./tinh-toan.js"));
  else goc.PTNMoPhong = nhaMay(goc.PTNTinhToan);
})(typeof self !== "undefined" ? self : this, function (T) {
  "use strict";

  const dd = T.dinhDang;
  const lamTron = (x, n = 3) => Math.round(x * 10 ** n) / 10 ** n;
  const gio = (t) => `${String(Math.floor(t / 60)).padStart(2, "0")}:${String(Math.floor(t % 60)).padStart(2, "0")}`;
  const docSo = (x) => (typeof x === "number" ? x : parseFloat(String(x ?? "").trim().replace(/\s/g, "").replace(",", ".")));
  const tron = (mang, ngauNhien) => { const a = mang.slice(); for (let i = a.length - 1; i > 0; i--) { const j = Math.floor(ngauNhien() * (i + 1)); [a[i], a[j]] = [a[j], a[i]]; } return a; };

  // ================================================================== 1. CHUẨN ĐỘ ACID – BASE
  const CHUAN_DO = {
    khoiTao({ tn }) {
      const ch = tn.cau_hinh, bai = ch.bai[0];
      return { bai: bai.id, Ca: bai.Ca, Ka: bai.Ka, Va: ch.Va, Cb: ch.Cb, V: 0, khoa: "dong", chi_thi: null, da_lac: true, vet_mau: false,
        diem: [[0, lamTron(T.pHChuanDo({ Ca: bai.Ca, Va: ch.Va, Cb: ch.Cb, Vb: 0, Ka: bai.Ka }), 3)]], V_doi_mau: null, doc_buret: null,
        tinh: null, on_thi: false, ket_qua_on_thi: null, _hen_giot: 0 };
    },
    pH: (s) => T.pHChuanDo({ Ca: s.Ca, Va: s.Va, Cb: s.Cb, Vb: s.V, Ka: s.Ka }),
    them(s, dV, ctx) {
      const ch = ctx.tn.cau_hinh;
      if (s.V >= ch.V_buret - 1e-9) return false;
      s.V = Math.min(ch.V_buret, lamTron(s.V + dV, 4));
      s.da_lac = false;
      const pH = CHUAN_DO.pH(s);
      const cuoi = s.diem[s.diem.length - 1];
      if (s.V - cuoi[0] >= 0.1 - 1e-9 || dV <= ch.the_tich_giot + 1e-9) {
        s.diem.push([lamTron(s.V, 2), lamTron(pH, 3)]);
        if (s.diem.length > 800) s.diem.splice(1, 1);
      }
      const Vtd = T.theTichTuongDuong(s);
      if (s.chi_thi && T.mauChiThi(s.chi_thi, pH).ti_le < 0.15 && s.V >= 0.85 * Vtd && !s.vet_mau) {
        s.vet_mau = true;   // giọt NaOH đặc tạo vệt màu cục bộ, mất đi khi lắc
        ctx.hieuUng("vet_mau");
      }
      if (s.V >= ch.V_buret - 1e-9) { s.khoa = "dong"; ctx.ghi("canh_bao", "Burette đã hết dung dịch NaOH (50,00 mL)."); }
      return true;
    },
    kiemTraDoiMau(s, ctx) {
      if (s.V_doi_mau !== null || !s.chi_thi) return;
      const m = T.mauChiThi(s.chi_thi, CHUAN_DO.pH(s));
      if (m.ti_le >= 0.15) {
        s.V_doi_mau = s.V;
        s.vet_mau = false;
        ctx.ghi("hien_tuong", `Tại V(NaOH) = ${dd(s.V)} mL: dung dịch chuyển ${m.mo_ta} và bền sau khi lắc → điểm cuối chuẩn độ.`);
        ctx.hieuUng("doi_mau");
      }
    },
    lenh: {
      chon_bai(s, { id }, ctx) {
        const bai = ctx.tn.cau_hinh.bai.find((b) => b.id === id);
        if (!bai) return { ok: false, thong_bao: "Không có dung dịch này." };
        const chiThi = s.chi_thi, onThi = s.on_thi;
        Object.assign(s, CHUAN_DO.khoiTao(ctx), { bai: bai.id, Ca: bai.Ca, Ka: bai.Ka, chi_thi: chiThi });
        s.diem = [[0, lamTron(CHUAN_DO.pH(s), 3)]];
        if (onThi) CHUAN_DO.lenh.bat_on_thi(s, {}, ctx, true);
        ctx.ghi("thao_tac", `Dùng pipette lấy ${dd(s.Va)} mL dung dịch ${bai.cong_thuc}${s.on_thi ? " (nồng độ ẩn)" : ` ${dd(s.Ca, 3)} M`} cho vào bình tam giác.`);
        return { ok: true };
      },
      chon_chi_thi(s, { id }, ctx) {
        if (id && !T.CHI_THI[id]) return { ok: false, thong_bao: "Không có chất chỉ thị này." };
        s.chi_thi = id || null;
        if (s.chi_thi) {
          const m = T.mauChiThi(s.chi_thi, CHUAN_DO.pH(s));
          ctx.ghi("thao_tac", `Nhỏ 2–3 giọt ${T.CHI_THI[s.chi_thi].ten} vào bình tam giác.`);
          ctx.ghi("hien_tuong", `Dung dịch trong bình: ${m.mo_ta}.`);
        }
        return { ok: true };
      },
      mo_khoa(s, { kieu = "nhanh" }, ctx) {
        if (kieu !== "nhanh" && kieu !== "giot") return { ok: false };
        if (s.V >= ctx.tn.cau_hinh.V_buret) return { ok: false, thong_bao: "Burette đã hết dung dịch." };
        s.khoa = kieu;
        s._hen_giot = 0;
        ctx.ghi("thao_tac", kieu === "nhanh" ? "Mở khoá burette: NaOH chảy nhanh." : "Vặn khoá burette: NaOH nhỏ từng giọt.");
        return { ok: true };
      },
      dong_khoa(s, _, ctx) {
        if (s.khoa === "dong") return { ok: true };
        s.khoa = "dong";
        ctx.ghi("thao_tac", `Đóng khoá burette (V = ${dd(s.V)} mL).`);
        CHUAN_DO.kiemTraDoiMau(s, ctx);
        return { ok: true };
      },
      nho_giot(s, _, ctx) {
        if (!CHUAN_DO.them(s, ctx.tn.cau_hinh.the_tich_giot, ctx)) return { ok: false, thong_bao: "Burette đã hết dung dịch." };
        s.da_lac = true;
        ctx.hieuUng("giot");
        CHUAN_DO.kiemTraDoiMau(s, ctx);
        return { ok: true };
      },
      them(s, { ml = 1 }, ctx) {
        const v = Math.max(0, Math.min(10, docSo(ml)));
        if (!v || !CHUAN_DO.them(s, v, ctx)) return { ok: false, thong_bao: "Không thêm được." };
        ctx.ghi("thao_tac", `Cho thêm ${dd(v)} mL NaOH (V = ${dd(s.V)} mL).`);
        return { ok: true };
      },
      lac(s, _, ctx) {
        s.da_lac = true;
        if (s.vet_mau) { s.vet_mau = false; ctx.ghi("hien_tuong", "Lắc bình: vệt màu biến mất – chưa tới điểm cuối."); }
        CHUAN_DO.kiemTraDoiMau(s, ctx);
        ctx.hieuUng("lac");
        return { ok: true };
      },
      doc_buret(s, { gia_tri }, ctx) {
        const v = docSo(gia_tri);
        if (!Number.isFinite(v)) return { ok: false, thong_bao: "Nhập số đọc, ví dụ 19,95." };
        const dung = Math.abs(v - s.V) <= 0.05 + 1e-9;
        s.doc_buret = { gia_tri: v, dung };
        ctx.ghi("ket_qua", `Đọc burette: ${dd(v)} mL${dung ? " ✓" : ` – chưa chính xác (vạch thật ${dd(s.V)} mL)`}.`);
        return { ok: true, dung, thong_bao: dung ? "Đọc đúng (sai số cho phép ±0,05 mL)." : `Chưa đúng – hãy đọc ở đáy mặt khum (vạch thật ≈ ${dd(s.V)} mL).` };
      },
      tinh_nong_do(s, { C }, ctx) {
        const c = docSo(C);
        if (!Number.isFinite(c)) return { ok: false, thong_bao: "Nhập nồng độ, ví dụ 0,100." };
        const V = s.doc_buret?.gia_tri ?? s.V_doi_mau ?? s.V;
        const mong = (s.Cb * V) / s.Va;
        const dung = Math.abs(c - mong) <= Math.max(0.002, mong * 0.01);
        s.tinh = { C: c, dung, C_mong: lamTron(mong, 4) };
        ctx.ghi("ket_qua", `Tính C(acid) = ${dd(c, 3)} M${dung ? " ✓" : ` – kiểm tra lại: ${dd(s.Cb, 3)} × ${dd(V)} / ${dd(s.Va)} = ${dd(mong, 3)} M`}.`);
        return { ok: true, dung, thong_bao: dung ? "Chính xác!" : `Chưa đúng: C = C(NaOH)·V(NaOH)/V(acid) = ${dd(mong, 3)} M.` };
      },
      bat_on_thi(s, _, ctx, khongGhi = false) {
        const o = ctx.tn.cau_hinh.on_thi;
        const Ca = lamTron(o.Ca_tu + ctx.ngauNhien() * (o.Ca_den - o.Ca_tu), 3);
        const chiThi = s.chi_thi, bai = s.bai, Ka = s.Ka;
        Object.assign(s, CHUAN_DO.khoiTao(ctx), { bai, Ka, Ca, chi_thi: chiThi, on_thi: true });
        s.diem = [[0, lamTron(CHUAN_DO.pH(s), 3)]];
        if (!khongGhi) ctx.ghi("thao_tac", "Bật chế độ Ôn thi: nồng độ acid bị ẩn, không có máy đo pH – chỉ quan sát màu chất chỉ thị.");
        return { ok: true };
      },
      tat_on_thi(s, _, ctx) {
        const bai = ctx.tn.cau_hinh.bai.find((b) => b.id === s.bai);
        const chiThi = s.chi_thi;
        Object.assign(s, CHUAN_DO.khoiTao(ctx), { bai: bai.id, Ca: bai.Ca, Ka: bai.Ka, chi_thi: chiThi });
        s.diem = [[0, lamTron(CHUAN_DO.pH(s), 3)]];
        ctx.ghi("thao_tac", "Tắt chế độ Ôn thi.");
        return { ok: true };
      },
      nop_ket_qua(s, { C }, ctx) {
        const c = docSo(C);
        if (!s.on_thi) return { ok: false, thong_bao: "Chưa bật chế độ Ôn thi." };
        if (!Number.isFinite(c) || c <= 0) return { ok: false, thong_bao: "Nhập nồng độ, ví dụ 0,085." };
        const saiSo = (Math.abs(c - s.Ca) / s.Ca) * 100;
        const diem = saiSo <= 1 ? 10 : saiSo <= 2 ? 8 : saiSo <= 5 ? 6 : saiSo <= 10 ? 4 : 2;
        s.ket_qua_on_thi = { C_nop: c, Ca: s.Ca, sai_so: lamTron(saiSo, 2), diem };
        ctx.ghi("ket_qua", `Ôn thi: nộp C = ${dd(c, 3)} M; nồng độ thật ${dd(s.Ca, 3)} M; sai số ${dd(saiSo, 1)}% → ${diem}/10 điểm.`);
        return { ok: true, thong_bao: `Sai số ${dd(saiSo, 1)}% – ${diem}/10 điểm.` };
      },
    },
    tick(s, dt, ctx) {
      const ch = ctx.tn.cau_hinh;
      if (s.khoa === "nhanh") return CHUAN_DO.them(s, ch.toc_do_nhanh * dt, ctx);
      if (s.khoa === "giot") {
        s._hen_giot += dt;
        let doi = false;
        while (s._hen_giot >= ch.khoang_giot && s.khoa === "giot") {
          s._hen_giot -= ch.khoang_giot;
          if (!CHUAN_DO.them(s, ch.the_tich_giot, ctx)) break;
          s.da_lac = true;   // nhỏ từng giọt: vừa nhỏ vừa lắc đều tay
          ctx.hieuUng("giot");
          CHUAN_DO.kiemTraDoiMau(s, ctx);
          doi = true;
        }
        return doi ? "ngay" : false;
      }
      return false;
    },
    anh(s, ctx) {
      const ch = ctx.tn.cau_hinh, bai = ch.bai.find((b) => b.id === s.bai);
      const pH = CHUAN_DO.pH(s);
      const an = s.on_thi && !s.ket_qua_on_thi;
      const m = T.mauChiThi(s.chi_thi, pH);
      return {
        bai: s.bai, acid: { cong_thuc: bai.cong_thuc, ten: bai.ten, mo_ta: bai.mo_ta }, Ca: an ? null : s.Ca, Va: s.Va, Cb: s.Cb,
        V: lamTron(s.V, 3), V_buret: ch.V_buret, ti_le_buret: s.V / ch.V_buret, doc_buret_that: dd(s.V),
        khoa: s.khoa, chi_thi: s.chi_thi, ten_chi_thi: s.chi_thi ? T.CHI_THI[s.chi_thi].ten : null,
        pH: an ? null : lamTron(pH, 2), mau: m, vet_mau: s.vet_mau, da_lac: s.da_lac,
        diem: an ? [] : s.diem, V_tuong_duong: an ? null : lamTron(T.theTichTuongDuong(s), 3), V_doi_mau: s.V_doi_mau,
        doc_buret: s.doc_buret, tinh: s.tinh, on_thi: s.on_thi, ket_qua_on_thi: s.ket_qua_on_thi,
        het_buret: s.V >= ch.V_buret - 1e-9, the_tich_dd: s.Va + s.V,
      };
    },
    dieuKien: {
      co_chi_thi: (s) => !!s.chi_thi,
      V_tu_15: (s) => s.V >= 15,
      doi_mau_ben: (s) => s.V_doi_mau !== null,
      da_doc_buret: (s) => !!s.doc_buret,
      da_tinh_dung: (s) => !!s.tinh?.dung,
    },
  };

  // ================================================================== 2. AN TOÀN PHÒNG THÍ NGHIỆM
  const AN_TOAN = {
    khoiTao: ({ tn, ngauNhien }) => ({ tim: {}, bay_da_bam: {}, so_bam_dung_viec: 0, ghs_ket_qua: null, su_co: {}, huy_hieu: false,
      // số thứ tự vị trí xáo ngẫu nhiên: lỗi và việc làm đúng lẫn vào nhau
      thu_tu: tron([...tn.cau_hinh.loi, ...tn.cau_hinh.bay].map((x) => x.id), ngauNhien) }),
    kiemTraHuyHieu(s, ctx) {
      const ch = ctx.tn.cau_hinh;
      if (s.huy_hieu) return;
      if (Object.keys(s.tim).length === ch.loi.length && s.ghs_ket_qua?.dung === ch.ghs.length && Object.keys(s.su_co).length === ch.su_co.length) {
        s.huy_hieu = true;
        ctx.ghi("ket_qua", `🏅 Hoàn thành bài nhập môn – đạt huy hiệu "${ctx.tn.huy_hieu}".`);
        ctx.hieuUng("huy_hieu");
      }
    },
    lenh: {
      bam_diem(s, { id }, ctx) {
        const ch = ctx.tn.cau_hinh;
        const loi = ch.loi.find((l) => l.id === id);
        if (loi) {
          if (s.tim[id]) return { ok: true, loai: "loi", thong_bao: `Đã tìm: ${loi.ten}.` };
          s.tim[id] = true;
          ctx.ghi("hien_tuong", `Phát hiện lỗi ${Object.keys(s.tim).length}/${ch.loi.length}: ${loi.ten}. ${loi.giai_thich}`);
          ctx.hieuUng("tim_loi", { id });
          if (Object.keys(s.tim).length === ch.loi.length) ctx.ghi("ket_qua", "Đã tìm đủ 8 lỗi mất an toàn.");
          AN_TOAN.kiemTraHuyHieu(s, ctx);
          return { ok: true, loai: "loi", thong_bao: `${loi.ten} – ${loi.giai_thich}` };
        }
        const bay = ch.bay.find((b) => b.id === id);
        if (bay) {
          s.so_bam_dung_viec += 1;
          s.bay_da_bam[id] = true;
          ctx.ghi("thao_tac", `"${bay.ten}" – đây là việc làm đúng, không phải lỗi.`);
          return { ok: true, loai: "dung", thong_bao: bay.giai_thich };
        }
        return { ok: false, thong_bao: "Không có vị trí này." };
      },
      ghep_ghs(s, { tra_loi = {} }, ctx) {
        const ghs = ctx.tn.cau_hinh.ghs;
        const chi_tiet = Object.fromEntries(ghs.map((g) => [g.id, tra_loi[g.id] === g.id]));
        const dung = Object.values(chi_tiet).filter(Boolean).length;
        s.ghs_ket_qua = { dung, tong: ghs.length, chi_tiet };
        ctx.ghi("ket_qua", `Ghép biểu tượng GHS: đúng ${dung}/${ghs.length}.`);
        AN_TOAN.kiemTraHuyHieu(s, ctx);
        return { ok: true, thong_bao: dung === ghs.length ? "Chính xác cả 9 biểu tượng!" : `Đúng ${dung}/${ghs.length} – xem lại các ô đỏ.` };
      },
      xem_su_co(s, { id }, ctx) {
        const sc = ctx.tn.cau_hinh.su_co.find((x) => x.id === id);
        if (!sc) return { ok: false };
        if (!s.su_co[id]) { s.su_co[id] = true; ctx.ghi("thao_tac", `Đọc cách xử lí sự cố: ${sc.ten}.`); }
        AN_TOAN.kiemTraHuyHieu(s, ctx);
        return { ok: true };
      },
    },
    anh(s, ctx) {
      const ch = ctx.tn.cau_hinh;
      return {
        loi: ch.loi.map((l) => ({ id: l.id, ten: s.tim[l.id] ? l.ten : null, da_tim: !!s.tim[l.id], giai_thich: s.tim[l.id] ? l.giai_thich : null })),
        diem_nong: s.thu_tu.map((id, i) => {
          const x = ch.loi.find((l) => l.id === id) || ch.bay.find((b) => b.id === id);
          return { id, so: i + 1, vi_tri: x.vi_tri, da_tim: !!s.tim[id], la_viec_dung: !!s.bay_da_bam[id] };
        }),
        so_tim: Object.keys(s.tim).length, tong_loi: ch.loi.length, so_bam_dung_viec: s.so_bam_dung_viec,
        ghs_ket_qua: s.ghs_ket_qua, su_co_da_xem: Object.keys(s.su_co), huy_hieu: s.huy_hieu,
      };
    },
    dieuKien: {
      tim_du_loi: (s, ctx) => Object.keys(s.tim).length === ctx.tn.cau_hinh.loi.length,
      ghep_dung_ghs: (s, ctx) => s.ghs_ket_qua?.dung === ctx.tn.cau_hinh.ghs.length,
      doc_du_su_co: (s, ctx) => Object.keys(s.su_co).length === ctx.tn.cau_hinh.su_co.length,
    },
  };

  // ================================================================== 3. NHẬN BIẾT ION
  const NHAN_BIET = {
    taoOng(ctx, chiDinh = null) {
      const ch = ctx.tn.cau_hinh, bi = ctx.bangIon;
      let ion = chiDinh;
      if (!ion) {
        const n = ch.so_ong[0] + Math.floor(ctx.ngauNhien() * (ch.so_ong[1] - ch.so_ong[0] + 1));
        const cation = tron(ch.ion.filter((i) => bi.ion[i].loai === "cation"), ctx.ngauNhien);
        const anion = tron(ch.ion.filter((i) => bi.ion[i].loai === "anion"), ctx.ngauNhien);
        const soCation = Math.max(2, Math.min(n - 2, Math.round(n / 2)));
        ion = tron([...cation.slice(0, soCation), ...anion.slice(0, n - soCation)], ctx.ngauNhien);
      }
      return ion.map((id, i) => ({ so: i + 1, ion: id, mau_dd: bi.ion[id].mau_dd ? { ...bi.ion[id].mau_dd } : null, ket_tua: null, khi: null,
        da_dun: false, moi_truong: null, quy: null, lich_su: [], thuoc_thu_da_cho: [], _pu: null, _tg_kk: 0, _cho_dun: null }));
    },
    khoiTao(ctx, chiDinh = null) {
      return { ong: NHAN_BIET.taoOng(ctx, chiDinh), so_luot_thu: 0, du_doan: {}, ket_qua: null, ngon_lua: null, tab: "ong",
        da_thu_cation: false, da_thu_anion: false, da_thu_lua: false };
    },
    quanSat(s, ong, chu, ctx) {
      ong.lich_su.push(chu);
      if (ong.lich_su.length > 6) ong.lich_su.shift();
      ctx.ghi("hien_tuong", `Ống ${ong.so}: ${chu}`);
    },
    lenh: {
      nho(s, { so, thuoc_thu, kieu = "giot" }, ctx) {
        const bi = ctx.bangIon, ong = s.ong.find((o) => o.so === Number(so)), tt = bi.thuoc_thu[thuoc_thu];
        if (!ong || !tt) return { ok: false, thong_bao: "Chọn ống nghiệm và thuốc thử." };
        if (s.ket_qua) return { ok: false, thong_bao: "Đã kiểm tra – bấm \"Bộ ống mới\" để chơi lại." };
        s.so_luot_thu += 1;
        ong.thuoc_thu_da_cho.push(thuoc_thu);
        if (tt.moi_truong) ong.moi_truong = tt.moi_truong;
        if (["NaOH", "NH3", "KSCN"].includes(thuoc_thu)) s.da_thu_cation = true;
        if (["AgNO3", "BaCl2", "HCl", "H2SO4"].includes(thuoc_thu)) s.da_thu_anion = true;
        ctx.ghi("thao_tac", `Ống ${ong.so}: nhỏ ${kieu === "du" ? "dư" : "vài giọt"} dung dịch ${tt.ten}.`);
        ctx.hieuUng("nho", { so: ong.so, thuoc_thu });
        // acid gặp kết tủa đã có
        if ((thuoc_thu === "HCl" || thuoc_thu === "H2SO4") && ong.ket_tua && ong._pu?.voi_acid) {
          const va = ong._pu.voi_acid;
          if (va.tan) {
            ong.ket_tua = null;
            ong.mau_dd = bi.ion[ong.ion].mau_dd ? { ...bi.ion[ong.ion].mau_dd } : null;
            if (va.khi) ong.khi = { cong_thuc: va.khi, con_lai: 6 };
            NHAN_BIET.quanSat(s, ong, `kết tủa tan${va.khi ? ", sủi bọt khí không màu" : ""}.`, ctx);
          } else NHAN_BIET.quanSat(s, ong, "kết tủa không tan.", ctx);
          return { ok: true };
        }
        const pu = bi.phan_ung.find((p) => p.ion === ong.ion && p.thuoc_thu === thuoc_thu);
        if (!pu) { NHAN_BIET.quanSat(s, ong, "không có hiện tượng rõ rệt.", ctx); return { ok: true }; }
        if (pu.can_dun && !ong.da_dun) {
          ong._cho_dun = pu;
          NHAN_BIET.quanSat(s, ong, "chưa thấy hiện tượng rõ (thử đun nóng ống nghiệm).", ctx);
          return { ok: true };
        }
        NHAN_BIET.apDung(s, ong, pu, kieu, ctx);
        return { ok: true };
      },
      dun(s, { so }, ctx) {
        const ong = s.ong.find((o) => o.so === Number(so));
        if (!ong || s.ket_qua) return { ok: false };
        s.so_luot_thu += 1;
        ong.da_dun = true;
        ctx.ghi("thao_tac", `Ống ${ong.so}: đun nóng nhẹ trên đèn cồn.`);
        if (ong._cho_dun) { NHAN_BIET.apDung(s, ong, ong._cho_dun, "giot", ctx); ong._cho_dun = null; }
        else NHAN_BIET.quanSat(s, ong, "không có hiện tượng đặc biệt khi đun.", ctx);
        return { ok: true };
      },
      thu_quy(s, { so }, ctx) {
        const ong = s.ong.find((o) => o.so === Number(so));
        if (!ong || s.ket_qua) return { ok: false };
        s.so_luot_thu += 1;
        if (ong.khi?.cong_thuc === "NH₃") {
          ong.quy = "xanh";
          NHAN_BIET.quanSat(s, ong, "quỳ tím ẩm đặt trên miệng ống hoá xanh (khí có tính base).", ctx);
        } else {
          ong.quy = ong.moi_truong === "base" ? "xanh" : ong.moi_truong === "acid" ? "đỏ" : ctx.bangIon.ion[ong.ion].quy;
          NHAN_BIET.quanSat(s, ong, `nhúng giấy quỳ tím: quỳ ${ong.quy === "tím" ? "không đổi màu" : `hoá ${ong.quy}`}.`, ctx);
        }
        return { ok: true };
      },
      dat_du_doan(s, { so, ion }) {
        if (s.ket_qua) return { ok: false };
        if (ion) s.du_doan[Number(so)] = ion; else delete s.du_doan[Number(so)];
        return { ok: true };
      },
      kiem_tra(s, _, ctx) {
        if (s.ket_qua) return { ok: true };
        const ch = ctx.tn.cau_hinh.diem, bi = ctx.bangIon;
        const chi_tiet = s.ong.map((o) => ({ so: o.so, ion: o.ion, ten: bi.ion[o.ion].ten, muoi: bi.ion[o.ion].muoi,
          du_doan: s.du_doan[o.so] || null, dung: s.du_doan[o.so] === o.ion,
          phan_ung: bi.phan_ung.filter((p) => p.ion === o.ion).map((p) => ({ thuoc_thu: bi.thuoc_thu[p.thuoc_thu].ten, hien_tuong: p.hien_tuong, pt_ion: p.pt_ion })) }));
        const sai = chi_tiet.filter((c) => !c.dung).length;
        const luotThem = Math.max(0, s.so_luot_thu - ch.luot_mien_phi_moi_ong * s.ong.length);
        const diem = Math.max(0, ch.toi_da - luotThem * ch.tru_moi_luot_them - sai * ch.tru_doan_sai);
        s.ket_qua = { chi_tiet, dung: chi_tiet.length - sai, tong: chi_tiet.length, so_luot_thu: s.so_luot_thu, diem };
        ctx.ghi("ket_qua", `Kiểm tra: đúng ${chi_tiet.length - sai}/${chi_tiet.length} ống sau ${s.so_luot_thu} lượt thử → ${diem}/${ch.toi_da} điểm.`);
        ctx.hieuUng("ket_qua");
        return { ok: true, thong_bao: `Đúng ${chi_tiet.length - sai}/${chi_tiet.length} ống – ${diem} điểm.` };
      },
      bo_moi(s, { ion = null } = {}, ctx) {
        const giu = { da_thu_cation: s.da_thu_cation, da_thu_anion: s.da_thu_anion, da_thu_lua: s.da_thu_lua, ngon_lua: s.ngon_lua, tab: s.tab };
        Object.assign(s, NHAN_BIET.khoiTao(ctx, ion), giu);
        ctx.ghi("thao_tac", `Lấy bộ ${s.ong.length} ống nghiệm mới (không nhãn).`);
        return { ok: true };
      },
      chon_tab(s, { tab }) { s.tab = tab === "ngon_lua" ? "ngon_lua" : "ong"; return { ok: true }; },
      thu_lua(s, { kim_loai }, ctx) {
        const nl = T.MAU_NGON_LUA[kim_loai], muoi = ctx.bangIon.ngon_lua[kim_loai];
        if (!nl || !muoi) return { ok: false };
        s.ngon_lua = { kim_loai, muoi: muoi.muoi, mau: nl.mau, rgb: nl.rgb, ten: nl.ten };
        s.da_thu_lua = true;
        ctx.ghi("thao_tac", `Nhúng dây platinum vào ${muoi.muoi} rồi đưa vào ngọn lửa đèn cồn.`);
        ctx.ghi("hien_tuong", `Ngọn lửa có màu ${nl.mau} (${nl.ten}).`);
        ctx.hieuUng("ngon_lua", { kim_loai });
        return { ok: true };
      },
      tat_lua(s) { s.ngon_lua = null; return { ok: true }; },
    },
    apDung(s, ong, pu, kieu, ctx) {
      const phan = [];
      if (pu.ket_tua) {
        const luong = kieu === "du" ? 1 : Math.min(1, (ong.ket_tua?.luong || 0) + 0.45);
        ong.ket_tua = { mau: pu.ket_tua.mau, rgb: pu.ket_tua.rgb, luong, _ct: pu.ket_tua.cong_thuc };
        ong._pu = pu;
        ong._tg_kk = 0;
        phan.push(`xuất hiện kết tủa ${pu.ket_tua.mau}`);
        ctx.hieuUng("ket_tua", { so: ong.so, rgb: pu.ket_tua.rgb });
        if (kieu === "du" && pu.du) {
          if (pu.du.tan) {
            ong.ket_tua = null;
            ong.mau_dd = pu.du.mau_dd ? { ...pu.du.mau_dd } : null;
            phan.push(`nhỏ dư thì kết tủa tan${pu.du.mau_dd ? ` tạo dung dịch ${pu.du.mau_dd.mo_ta}` : ", dung dịch trong suốt"}`);
          } else phan.push("nhỏ dư kết tủa không tan");
        }
      }
      if (pu.khi) {
        ong.khi = { cong_thuc: pu.khi, con_lai: pu.khi === "NH₃" ? 12 : 6 };
        phan.push(pu.khi === "NH₃" ? "có khí mùi khai bay ra" : "sủi bọt khí không màu, không mùi");
        ctx.hieuUng("khi", { so: ong.so });
      }
      if (pu.mau_dd) {
        ong.mau_dd = { ...pu.mau_dd };
        phan.push(`dung dịch chuyển màu ${pu.mau_dd.mo_ta}`);
      }
      NHAN_BIET.quanSat(s, ong, `${phan.join("; ")}.`, ctx);
    },
    tick(s, dt, ctx) {
      let doi = false;
      for (const ong of s.ong) {
        const nk = ong._pu?.ngoai_khong_khi;
        if (ong.ket_tua && nk && ong.ket_tua._ct === ong._pu.ket_tua.cong_thuc) {
          ong._tg_kk += dt;
          if (ong._tg_kk >= nk.sau_giay) {
            ong.ket_tua = { mau: nk.ket_tua.mau, rgb: nk.ket_tua.rgb, luong: ong.ket_tua.luong, _ct: nk.ket_tua.cong_thuc };
            NHAN_BIET.quanSat(s, ong, `để ngoài không khí một lúc, kết tủa chuyển dần sang màu ${nk.ket_tua.mau}.`, ctx);
            doi = "ngay";
          }
        }
        if (ong.khi) {
          ong.khi.con_lai -= dt;
          if (ong.khi.con_lai <= 0) { ong.khi = null; doi = "ngay"; }
        }
      }
      return doi;
    },
    anh(s, ctx) {
      const bi = ctx.bangIon, lo = !!s.ket_qua;
      return {
        tab: s.tab, so_luot_thu: s.so_luot_thu, du_doan: { ...s.du_doan }, ket_qua: s.ket_qua, ngon_lua: s.ngon_lua,
        ong: s.ong.map((o) => ({ so: o.so, ion: lo ? o.ion : null, ten_ion: lo ? bi.ion[o.ion].ten : null, muoi: lo ? bi.ion[o.ion].muoi : null,
          mau_dd: o.mau_dd, ket_tua: o.ket_tua ? { mau: o.ket_tua.mau, rgb: o.ket_tua.rgb, luong: o.ket_tua.luong } : null,
          khi: o.khi ? { cong_thuc: lo ? o.khi.cong_thuc : null } : null, co_khi: !!o.khi, co_ket_tua: !!o.ket_tua, da_dun: o.da_dun, quy: o.quy, lich_su: o.lich_su.slice(), so_thuoc_thu: o.thuoc_thu_da_cho.length })),
        lua_chon_ion: ctx.tn.cau_hinh.ion.map((id) => ({ id, ten: bi.ion[id].ten })),
        thuoc_thu: ctx.tn.cau_hinh.thuoc_thu.map((id) => ({ id, ten: bi.thuoc_thu[id].ten, ten_day_du: bi.thuoc_thu[id].ten_day_du })),
        kim_loai_ngon_lua: ctx.tn.cau_hinh.kim_loai_ngon_lua.map((k) => ({ id: k, muoi: bi.ngon_lua[k].muoi })),
      };
    },
    dieuKien: {
      da_thu_cation: (s) => s.da_thu_cation,
      da_thu_anion: (s) => s.da_thu_anion,
      da_kiem_tra: (s) => !!s.ket_qua,
      da_thu_lua: (s) => s.da_thu_lua,
    },
  };

  // ================================================================== 4. PIN GALVANI
  const MAU_ION = { Cu: [70, 150, 230], Ni: [120, 200, 140], Fe: [190, 214, 170] };
  const PIN = {
    khoiTao({ tn }) {
      const ch = tn.cau_hinh;
      return { trai: ch.mac_dinh.trai, phai: ch.mac_dinh.phai, C_trai: ch.nong_do.mac_dinh, C_phai: ch.nong_do.mac_dinh, cau_muoi: true,
        t_chay: 0, doc: null, da_rut: false, da_doi_C: false };
    },
    lenh: {
      chon_kim_loai(s, { ben, kim_loai }, ctx) {
        if (!T.THE_DIEN_CUC[kim_loai] || (ben !== "trai" && ben !== "phai")) return { ok: false };
        s[ben] = kim_loai;
        s.t_chay = 0;
        ctx.ghi("thao_tac", `Cốc ${ben === "trai" ? "trái" : "phải"}: điện cực ${kim_loai} nhúng trong dung dịch ${ctx.tn.cau_hinh.muoi[kim_loai]}.`);
        return { ok: true };
      },
      doi_nong_do(s, { ben, C }, ctx) {
        const nd = ctx.tn.cau_hinh.nong_do, c = Math.min(nd.den, Math.max(nd.tu, docSo(C)));
        if (!Number.isFinite(c) || (ben !== "trai" && ben !== "phai")) return { ok: false };
        s[`C_${ben}`] = c;
        if (!s.da_doi_C && Math.abs(c - nd.mac_dinh) > 1e-9) { s.da_doi_C = true; }
        return { ok: true };
      },
      ghi_nong_do(s, _, ctx) {
        const p = PIN.tinh(s);
        ctx.ghi("ket_qua", `Nồng độ trái ${dd(s.C_trai, 3)} M, phải ${dd(s.C_phai, 3)} M → E = ${dd(p.E_pin, 3)} V (Nernst).`);
        return { ok: true };
      },
      rut_cau_muoi(s, _, ctx) {
        if (!s.cau_muoi) return { ok: true };
        s.cau_muoi = false;
        s.da_rut = true;
        ctx.ghi("thao_tac", "Rút cầu muối ra khỏi hai cốc.");
        ctx.ghi("hien_tuong", "Vôn kế chỉ 0 V: mạch hở – ion không còn đường di chuyển giữa hai dung dịch để trung hoà điện tích.");
        return { ok: true };
      },
      lap_cau_muoi(s, _, ctx) {
        if (s.cau_muoi) return { ok: true };
        s.cau_muoi = true;
        ctx.ghi("thao_tac", `Lắp lại cầu muối KNO₃ – vôn kế chỉ ${dd(PIN.tinh(s).E_pin, 3)} V.`);
        return { ok: true };
      },
      ghi_so_do(s, _, ctx) {
        const p = PIN.tinh(s);
        s.doc = { E: p.E_pin };
        ctx.ghi("ket_qua", `Vôn kế chỉ ${dd(p.E_pin, 3)} V. Anode (−): ${p.anode} (${p.nua_anode}); cathode (+): ${p.cathode} (${p.nua_cathode}). Phương trình: ${p.phuong_trinh}.`);
        return { ok: true, thong_bao: `E = ${dd(p.E_pin, 3)} V` };
      },
    },
    tinh: (s) => T.pinGalvani({ trai: s.trai, phai: s.phai, C_trai: s.C_trai, C_phai: s.C_phai, cauMuoi: s.cau_muoi }),
    tick(s, dt) {
      if (!s.cau_muoi || s.trai === s.phai && Math.abs(s.C_trai - s.C_phai) < 1e-9) return false;
      s.t_chay += dt;
      return true;
    },
    anh(s, ctx) {
      const p = PIN.tinh(s), ch = ctx.tn.cau_hinh;
      const tien = Math.min(1, s.t_chay / 600);
      const mau = (k) => T.THE_DIEN_CUC[k].mau;
      return {
        trai: s.trai, phai: s.phai, C_trai: s.C_trai, C_phai: s.C_phai, cau_muoi: s.cau_muoi, muoi_trai: ch.muoi[s.trai], muoi_phai: ch.muoi[s.phai],
        E0_trai: T.THE_DIEN_CUC[s.trai].E0, E0_phai: T.THE_DIEN_CUC[s.phai].E0, ...p, E_pin: lamTron(p.E_pin, 4), E0_pin: lamTron(p.E0_pin, 4),
        chieu_electron: s.cau_muoi && p.E_pin > 1e-6 ? (p.ben_cathode === "phai" ? "trai_sang_phai" : "phai_sang_trai") : null,
        mon_anode: s.cau_muoi ? tien * 0.5 : 0, day_cathode: s.cau_muoi ? tien * 0.5 : 0, t_chay: lamTron(s.t_chay, 1),
        mau_kim_loai: { trai: mau(s.trai), phai: mau(s.phai) },
        mau_dd: { trai: MAU_ION[s.trai] || null, phai: MAU_ION[s.phai] || null },
        doc: s.doc, kim_loai: ch.kim_loai.map((k) => ({ id: k, E0: T.THE_DIEN_CUC[k].E0, ion: T.THE_DIEN_CUC[k].ion })),
      };
    },
    dieuKien: {
      da_chon_cap: () => true,
      da_doc_von: (s) => !!s.doc,
      da_rut_cau_muoi: (s) => s.da_rut,
      da_doi_nong_do: (s) => s.da_doi_C,
    },
  };

  // ================================================================== 5. ĐIỆN PHÂN DUNG DỊCH
  const DIEN_PHAN = {
    khoiTao({ tn }) {
      const ch = tn.cau_hinh;
      return { bai: ch.bai[0].id, I: ch.I.mac_dinh, t_muc_tieu: ch.t.mac_dinh, t: 0, dang_chay: false, toc_do: ch.toc_do[1], da_dat: false, xong: false, doi_chieu: null };
    },
    bai: (s, ctx) => ctx.tn.cau_hinh.bai.find((b) => b.id === s.bai),
    ketQua: (s, ctx) => { const b = DIEN_PHAN.bai(s, ctx); return T.dienPhan({ bai: s.bai, I: s.I, t: s.t, V_dd: b.V_dd, C0: b.C0 }); },
    lenh: {
      chon_bai(s, { id }, ctx) {
        const b = ctx.tn.cau_hinh.bai.find((x) => x.id === id);
        if (!b) return { ok: false };
        Object.assign(s, { bai: id, t: 0, dang_chay: false, xong: false, doi_chieu: null });
        ctx.ghi("thao_tac", `Chọn bài: ${b.ten} (${dd(b.C0, 1)} M, ${dd(b.V_dd * 1000, 0)} mL).`);
        return { ok: true };
      },
      dat_I(s, { I }, ctx) {
        const r = ctx.tn.cau_hinh.I, v = Math.min(r.den, Math.max(r.tu, docSo(I)));
        if (!Number.isFinite(v)) return { ok: false };
        s.I = lamTron(v, 2); s.da_dat = true;
        return { ok: true };
      },
      dat_t(s, { t }, ctx) {
        const r = ctx.tn.cau_hinh.t, v = Math.min(r.den, Math.max(r.tu, Math.round(docSo(t))));
        if (!Number.isFinite(v)) return { ok: false };
        s.t_muc_tieu = v; s.da_dat = true;
        if (s.t > v) { s.t = v; }
        return { ok: true };
      },
      bat_nguon(s, _, ctx) {
        if (s.t >= s.t_muc_tieu - 1e-9) { s.t = 0; s.xong = false; s.doi_chieu = null; }
        s.dang_chay = true; s.da_dat = true;
        ctx.ghi("thao_tac", `Bật nguồn: I = ${dd(s.I, 1)} A, điện phân trong ${s.t_muc_tieu} s (tua ×${s.toc_do}).`);
        return { ok: true };
      },
      tat_nguon(s, _, ctx) {
        if (!s.dang_chay) return { ok: true };
        s.dang_chay = false;
        ctx.ghi("thao_tac", `Tắt nguồn tại t = ${Math.round(s.t)} s.`);
        return { ok: true };
      },
      tua(s, { toc_do }, ctx) {
        const v = Number(toc_do);
        if (!ctx.tn.cau_hinh.toc_do.includes(v)) return { ok: false };
        s.toc_do = v;
        return { ok: true };
      },
      doi_chieu(s, { gia_tri }, ctx) {
        const v = docSo(gia_tri);
        if (!Number.isFinite(v)) return { ok: false, thong_bao: "Nhập số, ví dụ 0,64." };
        const kq = DIEN_PHAN.ketQua(s, ctx);
        const laKhi = s.bai === "nacl_mang_ngan";
        const mong = laKhi ? kq.V_khi_cathode : kq.m_cathode;
        const dung = Math.abs(v - mong) <= Math.max(0.01, mong * 0.02);
        s.doi_chieu = { gia_tri: v, mong: lamTron(mong, 4), dung, dai_luong: laKhi ? "V(H₂) (L)" : "m(Cu) (g)" };
        ctx.ghi("ket_qua", `Đối chiếu ${laKhi ? "V(H₂)" : "m(Cu)"}: tính ${dd(v, 3)} – mô phỏng ${dd(mong, 3)} ${dung ? "✓" : "✗"}.`);
        return { ok: true, dung, thong_bao: dung ? "Khớp định luật Faraday!" : `Chưa khớp: n(e) = I·t/F = ${dd(kq.n_e, 4)} mol → ${dd(mong, 3)}.` };
      },
    },
    tick(s, dt, ctx) {
      if (!s.dang_chay) return false;
      s.t = Math.min(s.t_muc_tieu, s.t + dt * s.toc_do);
      if (s.t >= s.t_muc_tieu - 1e-9) {
        s.dang_chay = false;
        s.xong = true;
        const kq = DIEN_PHAN.ketQua(s, ctx);
        const tom = s.bai === "nacl_mang_ngan"
          ? `V(H₂) = ${dd(kq.V_khi_cathode, 3)} L, V(khí anode: ${kq.khi_anode}) = ${dd(kq.V_khi_anode, 3)} L, pH ngăn cathode ≈ ${dd(kq.pH_cathode, 1)}`
          : s.bai === "cuso4_tro"
            ? `m(Cu) = ${dd(kq.m_cathode, 3)} g, V(O₂) = ${dd(kq.V_khi_anode, 3)} L${kq.V_khi_cathode ? `, V(H₂) = ${dd(kq.V_khi_cathode, 3)} L` : ""}, pH ≈ ${dd(kq.pH, 2)}`
            : `cathode tăng ${dd(kq.m_cathode, 3)} g, anode giảm ${dd(kq.m_anode_giam, 3)} g`;
        ctx.ghi("ket_qua", `Điện phân xong ${s.t_muc_tieu} s với I = ${dd(s.I, 1)} A: n(e) = ${dd(kq.n_e, 4)} mol; ${tom}.`);
        ctx.hieuUng("xong");
        return "ngay";
      }
      return true;
    },
    anh(s, ctx) {
      const b = DIEN_PHAN.bai(s, ctx), kq = DIEN_PHAN.ketQua(s, ctx);
      const tron01 = (x) => Math.max(0, Math.min(1, x));
      const mau_dd = b.mau_dd ? { rgb: b.mau_dd, dam: tron01(kq.ti_le_Cu2) } : null;
      const chiThiCathode = s.bai === "nacl_mang_ngan" ? T.mauChiThi("phenolphthalein", kq.pH_cathode ?? 7) : null;
      return {
        bai: s.bai, ten_bai: b.ten, dien_cuc: b.dien_cuc, V_dd: b.V_dd, C0: b.C0, I: s.I, t: lamTron(s.t, 1), t_muc_tieu: s.t_muc_tieu,
        dang_chay: s.dang_chay, toc_do: s.toc_do, xong: s.xong, ket_qua: kq, thu_tu: T.THU_TU_DIEN_PHAN[s.bai],
        khi_dang_thoat: { cathode: s.dang_chay && !!kq.khi_cathode, anode: s.dang_chay && !!kq.khi_anode },
        mau_dd, chi_thi_cathode: chiThiCathode, quy_anode: s.bai === "nacl_mang_ngan" ? (kq.V_khi_anode > 0.0005 ? "mất màu" : "tím") : null,
        doi_chieu: s.doi_chieu, lua_chon_bai: ctx.tn.cau_hinh.bai.map((x) => ({ id: x.id, ten: x.ten })), toc_do_co_the: ctx.tn.cau_hinh.toc_do,
      };
    },
    dieuKien: {
      da_chon_bai: () => true,
      da_dat_I_t: (s) => s.da_dat,
      da_chay_xong: (s) => s.xong,
      da_doi_chieu: (s) => !!s.doi_chieu?.dung,
    },
  };

  const LOAI = { "chuan-do": CHUAN_DO, "an-toan": AN_TOAN, "nhan-biet-ion": NHAN_BIET, "pin-galvani": PIN, "dien-phan": DIEN_PHAN };

  // ================================================================== KHUNG CHUNG
  function tao(tn, { bangIon = null, ngauNhien = Math.random, cheDo = "huong_dan" } = {}) {
    const loai = LOAI[tn?.id];
    if (!loai) throw new Error(`Không có thí nghiệm "${tn?.id}"`);
    if (tn.id === "nhan-biet-ion" && !bangIon) throw new Error("Thiếu bảng ion (bang-ion.json)");
    const nghe = {};
    const phat = (ten, dl) => (nghe[ten] || []).slice().forEach((f) => { try { f(dl); } catch (e) { (typeof console !== "undefined") && console.error(e); } });
    let dongHo = 0, cho = 0;
    const soTay = [];
    const ctx = {
      tn, bangIon, ngauNhien, T,
      ghi(loaiGhi, chu) { const m = { t: lamTron(dongHo, 1), gio: gio(dongHo), loai: loaiGhi, chu }; soTay.push(m); if (soTay.length > 400) soTay.shift(); phat("so_tay", m); },
      hieuUng(ten, dl = {}) { phat("hieu_ung", { loai: ten, ...dl }); },
    };
    let s = loai.khoiTao(ctx);
    let buocXong = (tn.buoc || []).map(() => false);
    let cauHoi = null;
    let cheDoHienTai = cheDo === "tu_do" ? "tu_do" : "huong_dan";

    const dk = (key) => (key === "lam_cau_hoi" ? !!cauHoi : (loai.dieuKien?.[key]?.(s, ctx) ?? false));
    function capNhatBuoc() {
      (tn.buoc || []).forEach((b, i) => {
        if (!buocXong[i] && dk(b.dieu_kien)) {
          buocXong[i] = true;
          if (cheDoHienTai === "huong_dan") ctx.ghi("buoc", `✓ Bước ${i + 1}: ${b.tieu_de}`);
          ctx.hieuUng("buoc_xong", { buoc: i });
        }
      });
    }
    function chupAnh() {
      const i = buocXong.findIndex((x) => !x);
      return JSON.parse(JSON.stringify({
        id: tn.id, che_do: cheDoHienTai, dong_ho: lamTron(dongHo, 2),
        buoc_xong: buocXong, buoc_hien_tai: i === -1 ? null : i, cau_hoi: cauHoi,
        ...loai.anh(s, ctx),
      }));
    }
    const phatTrangThai = () => phat("trang_thai", chupAnh());

    const mp = {
      id: tn.id,
      on(ten, fn) { (nghe[ten] ||= []).push(fn); return () => { nghe[ten] = (nghe[ten] || []).filter((f) => f !== fn); }; },
      trangThai: chupAnh,
      soTay: () => soTay.slice(),
      thaoTac(lenh, ts = {}) {
        let kq;
        if (lenh === "dat_lai") {
          s = loai.khoiTao(ctx);
          buocXong = buocXong.map(() => false);
          cauHoi = null;
          ctx.ghi("thao_tac", "Làm lại thí nghiệm từ đầu.");
          kq = { ok: true };
        } else if (lenh === "doi_che_do") {
          cheDoHienTai = ts.che_do === "tu_do" ? "tu_do" : "huong_dan";
          kq = { ok: true };
        } else if (lenh === "nop_cau_hoi") {
          cauHoi = { dung: Number(ts.dung) || 0, tong: Number(ts.tong) || 0 };
          ctx.ghi("ket_qua", `Câu hỏi cuối bài: đúng ${cauHoi.dung}/${cauHoi.tong}.`);
          kq = { ok: true };
        } else {
          const f = loai.lenh[lenh];
          if (!f) return { ok: false, thong_bao: `Thao tác không hợp lệ: ${lenh}` };
          kq = f(s, ts || {}, ctx) || { ok: true };
        }
        capNhatBuoc();
        phatTrangThai();
        return kq;
      },
      capNhat(dt) {
        if (!(dt > 0)) return;
        dt = Math.min(dt, 0.25);
        dongHo += dt;
        const doi = loai.tick ? loai.tick(s, dt, ctx) : false;
        if (!doi) return;
        capNhatBuoc();
        cho += dt;
        if (doi === "ngay" || cho >= 0.05) { cho = 0; phatTrangThai(); }
      },
    };
    capNhatBuoc();
    return mp;
  }

  return { tao, THI_NGHIEM: Object.keys(LOAI), PHIEN_BAN: 1 };
});
