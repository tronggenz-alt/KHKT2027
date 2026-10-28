/* Phòng thí nghiệm Hoá học ảo – HÀM TÍNH THUẦN (không DOM). Nguồn sự thật DUY NHẤT cho mọi con số hoá học:
   bản Web 3D và bản Unity chỉ hiển thị kết quả từ đây (qua mo-phong.js), không tự tính lại.
   UMD: trình duyệt → window.PTNTinhToan · Node → require("…/tinh-toan.js").
   Quy ước: thể tích dung dịch tính bằng mL, nồng độ mol/L, khí đo ở đkc 25 °C – 1 bar (24,79 L/mol), F = 96 485 C/mol,
   nguyên tử khối theo SGK (Cu = 64, Ag = 108, Zn = 65…). */
(function (goc, nhaMay) {
  if (typeof module === "object" && module.exports) module.exports = nhaMay();
  else goc.PTNTinhToan = nhaMay();
})(typeof self !== "undefined" ? self : this, function () {
  "use strict";

  const KW = 1e-14;
  const F = 96485;
  const V_MOL = 24.79;          // L/mol ở đkc (25 °C, 1 bar)
  const HE_SO_NERNST = 0.0592;  // 2,303RT/F ở 25 °C
  const NTK = { H: 1, O: 16, Na: 23, Mg: 24, Al: 27, S: 32, Cl: 35.5, K: 39, Ca: 40, Fe: 56, Ni: 59, Cu: 64, Zn: 65, Ag: 108, Sn: 119, Ba: 137, Pb: 207 };

  const kep = (x, a, b) => Math.min(b, Math.max(a, x));
  const lg = Math.log10;

  /** Định dạng số kiểu Việt Nam: dấu phẩy thập phân, "0,64". */
  function dinhDang(x, soLe = 2) {
    if (!Number.isFinite(x)) return "–";
    return x.toFixed(soLe).replace("-", "−").replace(".", ",");
  }

  // ------------------------------------------------------------------ pH bằng cân bằng điện tích (giải số chia đôi)
  /**
   * pH của dung dịch acid (mạnh hoặc yếu đơn chức) đã thêm base mạnh NaOH.
   * Cân bằng điện tích: [H⁺] + [Na⁺] = [OH⁻] + [A⁻];  [A⁻] = Cₐ (acid mạnh) hoặc Cₐ·Ka/(Ka + [H⁺]) (acid yếu).
   * f([H⁺]) tăng đơn điệu → chia đôi trên log[H⁺] ∈ [−15; 1]. KHÔNG dùng công thức gần đúng từng đoạn.
   * @param {{Ca:number, Va:number, Cb:number, Vb:number, Ka?:number}} p  Ka bỏ trống = acid mạnh
   */
  function pHChuanDo({ Ca, Va, Cb, Vb, Ka = null }) {
    const Vt = Va + Vb;
    if (Vt <= 0) return 7;
    const ca = (Ca * Va) / Vt;     // tổng acid (mol/L)
    const na = (Cb * Vb) / Vt;     // Na⁺ (mol/L)
    const anion = (h) => (Ka ? (ca * Ka) / (Ka + h) : ca);
    const f = (h) => h + na - KW / h - anion(h);
    let thap = -15, cao = 1;       // log10[H⁺]
    for (let i = 0; i < 200; i++) {
      const giua = (thap + cao) / 2;
      if (f(10 ** giua) > 0) cao = giua; else thap = giua;
    }
    return -((thap + cao) / 2);
  }

  /** Thể tích NaOH tại điểm tương đương (mL). */
  const theTichTuongDuong = ({ Ca, Va, Cb }) => (Ca * Va) / Cb;

  /** Các điểm (V, pH) để vẽ đường cong chuẩn độ lý thuyết. */
  function duongCongChuanDo(p, Vmax = null, buoc = 0.25) {
    const Vtd = theTichTuongDuong(p);
    const het = Vmax ?? Math.max(2 * Vtd, Vtd + 10);
    const diem = [];
    for (let V = 0; V <= het + 1e-9; V += buoc) diem.push([V, pHChuanDo({ ...p, Vb: V })]);
    return diem;
  }

  // ------------------------------------------------------------------ chất chỉ thị
  const CHI_THI = {
    phenolphthalein: { ten: "Phenolphthalein", tu: 8.2, den: 10.0, mauAcid: [255, 255, 255, 0], mauBase: [232, 62, 140, 0.85],
      chuAcid: "không màu", chuGiua: "hồng nhạt", chuBase: "hồng" },
    methyl_orange: { ten: "Methyl orange", tu: 3.1, den: 4.4, mauAcid: [214, 48, 49, 0.8], mauBase: [241, 196, 15, 0.8],
      chuAcid: "đỏ", chuGiua: "da cam", chuBase: "vàng" },
  };

  /** Màu dung dịch theo pH thật và khoảng đổi màu của chỉ thị → {rgba: [r,g,b,a], mo_ta}. Không chỉ thị → không màu. */
  function mauChiThi(tenChiThi, pH) {
    const ct = CHI_THI[tenChiThi];
    if (!ct) return { rgba: [255, 255, 255, 0], mo_ta: "không màu", ti_le: 0 };
    const t = kep((pH - ct.tu) / (ct.den - ct.tu), 0, 1);
    const rgba = ct.mauAcid.map((a, i) => a + (ct.mauBase[i] - a) * t);
    const moTa = t <= 0.02 ? ct.chuAcid : t >= 0.98 ? ct.chuBase : ct.chuGiua;
    return { rgba, mo_ta: moTa, ti_le: t };
  }

  // ------------------------------------------------------------------ pin Galvani
  const THE_DIEN_CUC = { // E° (V) của cặp Mⁿ⁺/M so với điện cực hydrogen chuẩn, n = số electron
    Mg: { E0: -2.37, n: 2, ion: "Mg²⁺", mau: [176, 180, 186] },
    Zn: { E0: -0.763, n: 2, ion: "Zn²⁺", mau: [170, 178, 190] },
    Fe: { E0: -0.44, n: 2, ion: "Fe²⁺", mau: [120, 124, 130] },
    Ni: { E0: -0.257, n: 2, ion: "Ni²⁺", mau: [168, 166, 150] },
    Sn: { E0: -0.137, n: 2, ion: "Sn²⁺", mau: [196, 198, 200] },
    Pb: { E0: -0.126, n: 2, ion: "Pb²⁺", mau: [100, 104, 116] },
    Cu: { E0: 0.34, n: 2, ion: "Cu²⁺", mau: [196, 112, 64] },
    Ag: { E0: 0.799, n: 1, ion: "Ag⁺", mau: [222, 224, 228] },
  };

  /** Thế điện cực theo Nernst (phần mở rộng): E = E° + (0,0592/n)·lg[Mⁿ⁺]. */
  function theDienCuc(kimLoai, C = 1) {
    const d = THE_DIEN_CUC[kimLoai];
    return d.E0 + (HE_SO_NERNST / d.n) * lg(Math.max(C, 1e-9));
  }

  /**
   * Pin ghép hai điện cực. Cực có thế lớn hơn là cathode (+), nhỏ hơn là anode (−).
   * Epin = E(cathode) − E(anode) (≡ E° − (0,0592/n)·lg Q). Rút cầu muối → mạch hở → 0 V.
   */
  function pinGalvani({ trai, phai, C_trai = 1, C_phai = 1, cauMuoi = true }) {
    const Et = theDienCuc(trai, C_trai);
    const Ep = theDienCuc(phai, C_phai);
    const tCathode = Et >= Ep;
    const cathode = tCathode ? trai : phai, anode = tCathode ? phai : trai;
    const E0pin = THE_DIEN_CUC[cathode].E0 - THE_DIEN_CUC[anode].E0;
    const Epin = Math.abs(Et - Ep);
    const nA = THE_DIEN_CUC[anode].n, nC = THE_DIEN_CUC[cathode].n;
    const ionA = THE_DIEN_CUC[anode].ion, ionC = THE_DIEN_CUC[cathode].ion;
    // phương trình tổng: hệ số để số electron hai nửa bằng nhau
    const bcnn = nA === nC ? nA : nA * nC;
    const kA = bcnn / nA, kC = bcnn / nC;
    const hs = (k) => (k === 1 ? "" : String(k));
    return {
      cathode, anode, ben_cathode: tCathode ? "trai" : "phai",
      E0_pin: E0pin, E_pin: cauMuoi ? Epin : 0, mach_kin: !!cauMuoi,
      nua_anode: `${anode} → ${ionA} + ${nA}e`,
      nua_cathode: `${ionC} + ${nC}e → ${cathode}`,
      phuong_trinh: `${hs(kA)}${anode} + ${hs(kC)}${ionC} → ${hs(kA)}${ionA} + ${hs(kC)}${cathode}`,
    };
  }

  // ------------------------------------------------------------------ điện phân dung dịch
  const soMolElectron = (I, t) => (I * t) / F;

  /**
   * Điện phân trong thời gian t (s) với dòng I (A).
   * bai: "cuso4_tro" (CuSO₄, điện cực trơ graphite) · "nacl_mang_ngan" (NaCl có màng ngăn) · "cuso4_anode_cu" (anode Cu tan – tinh luyện đồng).
   * V_dd (L), C0 (mol/L) của muối ban đầu. Trả khối lượng, thể tích khí (đkc), pH, tỉ lệ Cu²⁺ còn lại, thứ tự điện phân.
   */
  function dienPhan({ bai, I, t, V_dd = 0.2, C0 = 0.5 }) {
    const ne = soMolElectron(I, t);
    const n0 = C0 * V_dd;
    const kq = { bai, I, t, n_e: ne, q: I * t, m_cathode: 0, m_anode_giam: 0, V_khi_cathode: 0, V_khi_anode: 0,
      khi_cathode: null, khi_anode: null, pH: 7, pH_cathode: null, pH_anode: null, ti_le_Cu2: 1, giai_doan: [] };
    if (bai === "cuso4_tro") {
      // Cathode: Cu²⁺ + 2e → Cu (trước); hết Cu²⁺ thì 2H₂O + 2e → H₂ + 2OH⁻. Anode: 2H₂O → O₂ + 4H⁺ + 4e (SO₄²⁻ không bị điện phân).
      const neCu = Math.min(ne, 2 * n0);
      const nCu = neCu / 2;
      const nH2 = Math.max(0, ne - neCu) / 2;
      const nO2 = ne / 4;
      const nH = neCu;                         // H⁺ sinh ra ở anode chỉ còn lại tương ứng phần Cu²⁺ đã khử (phần sau H⁺ + OH⁻ trung hoà)
      Object.assign(kq, { m_cathode: nCu * NTK.Cu, V_khi_cathode: nH2 * V_MOL, V_khi_anode: nO2 * V_MOL,
        khi_cathode: nH2 > 0 ? "H₂" : null, khi_anode: "O₂", ti_le_Cu2: n0 > 0 ? 1 - nCu / n0 : 0,
        pH: nH > 0 ? -lg(nH / V_dd) : 7, giai_doan: nH2 > 0 ? ["Cu²⁺ đã bị khử hết", "đang điện phân nước ở cathode"] : ["Cu²⁺ đang bị khử ở cathode"] });
      kq.n_Cu = nCu; kq.n_O2 = nO2; kq.n_H2 = nH2;
    } else if (bai === "nacl_mang_ngan") {
      // Cathode: 2H₂O + 2e → H₂ + 2OH⁻ (Na⁺ không bị khử). Anode: 2Cl⁻ → Cl₂ + 2e (trước H₂O); hết Cl⁻ thì 2H₂O → O₂ + 4H⁺ + 4e.
      const neCl = Math.min(ne, n0);
      const nCl2 = neCl / 2;
      const nO2 = Math.max(0, ne - neCl) / 4;
      const nH2 = ne / 2;
      const nOH = ne;                                   // ngăn cathode (màng ngăn giữ OH⁻)
      const Vngan = V_dd / 2;
      Object.assign(kq, { V_khi_cathode: nH2 * V_MOL, V_khi_anode: (nCl2 + nO2) * V_MOL, khi_cathode: "H₂", khi_anode: nO2 > 0 ? "Cl₂, O₂" : "Cl₂",
        pH_cathode: nOH > 0 ? 14 + lg(nOH / Vngan) : 7, pH_anode: nO2 > 0 ? -lg((Math.max(0, ne - neCl)) / Vngan) : 7,
        giai_doan: nO2 > 0 ? ["Cl⁻ đã bị oxi hoá hết", "đang điện phân nước ở anode"] : ["Cl⁻ đang bị oxi hoá ở anode"] });
      kq.pH = kq.pH_cathode;
      kq.n_H2 = nH2; kq.n_Cl2 = nCl2; kq.n_O2 = nO2; kq.n_OH = nOH;
    } else if (bai === "cuso4_anode_cu") {
      // Anode Cu tan: Cu → Cu²⁺ + 2e; cathode: Cu²⁺ + 2e → Cu. Nồng độ Cu²⁺ không đổi, không có khí.
      const nCu = ne / 2;
      Object.assign(kq, { m_cathode: nCu * NTK.Cu, m_anode_giam: nCu * NTK.Cu, ti_le_Cu2: 1, pH: 4.5,
        giai_doan: ["anode Cu tan dần, cathode dày lên – nồng độ Cu²⁺ gần như không đổi"] });
      kq.n_Cu = nCu;
    }
    return kq;
  }

  const THU_TU_DIEN_PHAN = {
    cuso4_tro: { cathode: ["Cu²⁺ + 2e → Cu (trước)", "2H₂O + 2e → H₂ + 2OH⁻ (khi hết Cu²⁺)"],
      anode: ["2H₂O → O₂ + 4H⁺ + 4e", "SO₄²⁻ không bị điện phân"] },
    nacl_mang_ngan: { cathode: ["Na⁺ không bị khử", "2H₂O + 2e → H₂ + 2OH⁻"], anode: ["2Cl⁻ → Cl₂ + 2e (trước H₂O)", "2H₂O → O₂ + 4H⁺ + 4e (khi hết Cl⁻)"] },
    cuso4_anode_cu: { cathode: ["Cu²⁺ + 2e → Cu"], anode: ["Cu → Cu²⁺ + 2e (anode tan)", "SO₄²⁻, H₂O không bị điện phân ở anode"] },
  };

  // ------------------------------------------------------------------ thử màu ngọn lửa
  const MAU_NGON_LUA = {
    Na: { ten: "Sodium (Na)", mau: "vàng", rgb: [255, 196, 30] },
    K: { ten: "Potassium (K)", mau: "tím nhạt", rgb: [190, 140, 255] },
    Li: { ten: "Lithium (Li)", mau: "đỏ tía", rgb: [220, 30, 90] },
    Ca: { ten: "Calcium (Ca)", mau: "đỏ cam", rgb: [255, 100, 40] },
    Ba: { ten: "Barium (Ba)", mau: "lục vàng", rgb: [170, 230, 60] },
    Cu: { ten: "Copper (Cu)", mau: "lục lam", rgb: [40, 220, 190] },
  };

  return {
    KW, F, V_MOL, HE_SO_NERNST, NTK, CHI_THI, THE_DIEN_CUC, THU_TU_DIEN_PHAN, MAU_NGON_LUA,
    kep, dinhDang, pHChuanDo, theTichTuongDuong, duongCongChuanDo, mauChiThi,
    theDienCuc, pinGalvani, soMolElectron, dienPhan,
  };
});
