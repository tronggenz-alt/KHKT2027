using System;
using System.Collections.Generic;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Bộ máy oxi hóa - khử tổng quát cho chất vô cơ: một bảng cặp oxi hóa - khử (bán phản ứng) với E°; thế thực tế tính
    /// theo phương trình Nernst từ nồng độ ion TỰ DO (sau khi đã tạo kết tủa, phức) và pH; phản ứng xảy ra theo quy tắc α khi
    /// E(chất oxi hóa) − E(chất khử) đủ lớn. Không có phản ứng nào viết riêng cho từng cặp chất:
    ///   - kim loại + axit (cặp 2H⁺/H₂, E = −0,059 pH), kim loại + nước, kim loại đẩy kim loại, Fe + Fe³⁺, Cu + Fe³⁺;
    ///   - HNO₃, H₂SO₄ đặc nóng với kim loại và phi kim; nước cường toan với vàng (Cl⁻ làm E(Au) giảm);
    ///   - Ag⁺ + I⁻ cho kết tủa chứ không khử (Ag⁺ tự do quá nhỏ), Cu²⁺ + I⁻ ra CuI (cặp Cu²⁺/CuI);
    ///   - Cl₂, Br₂, I₂ tự oxi hóa - khử trong kiềm; MnO₄⁻ bị khử về Mn²⁺ (axit) hay MnO₂ (trung tính) theo pH.
    /// Chọn sản phẩm: chất oxi hóa bị khử xuống mức sâu nhất còn khả thi, chất khử bị oxi hóa lên mức cao nhất còn khả thi.
    /// Điều kiện động học (cần đun, xúc tác, thụ động hóa, axit đặc) là cột dữ liệu của từng cặp, lấy theo SGK Hóa học 10 - 12.
    /// E° theo bảng thế điện cực chuẩn trong nước ở 25 °C (SGK Hóa học 12; Bratsch, J. Phys. Chem. Ref. Data 18, 1989).
    /// </summary>
    public static partial class ChemEngine
    {
        public sealed class Couple
        {
            public string Name;
            public (string id, double nu)[] Ox, Red;   // "khi:X": khí (hoạt độ 1, bay ra); không ghi H⁺, H₂O
            public int N;                               // số electron
            public double M;                            // số H⁺ ở vế oxi hóa (Ox + n e + m H⁺ → Red + ...)
            public double E0;
            public double PhMinOx = -99, PhMaxOx = 99, PhMinRed = -99, PhMaxRed = 99;
            public double TMinOx = -999, TMinRed = -999;
            public double MinConc, MaxConc = 1e9;       // nồng độ chất oxi hóa đầu tiên khi đóng vai chất oxi hóa (mol/l)
            public double MinConcRed;                   // nồng độ chất khử đầu tiên khi đóng vai chất khử (Cl⁻: chỉ HCl đặc)
            public bool OxOnly, RedOnly;
            public string Catalyst;                     // chất xúc tác cần có khi đóng vai chất oxi hóa
            public string[] FastWith;                   // chất khử phản ứng nhanh không cần xúc tác
            public bool IsMetal;                        // Mⁿ⁺/M
            public bool IsHydrogen;                     // 2H⁺/H₂ (axit và nước)
            public bool Dispro;                         // chất tự oxi hóa - khử nhanh được (halogen trong kiềm, S trong kiềm nóng)
            public bool RedIsSolidForm;
            public bool TotalActivity;                  // dùng nồng độ tổng (H₂SO₄ đặc: phân tử H₂SO₄ là chất oxi hóa)                 // E° ứng với dạng khử là hiđroxit rắn (CrO₄²⁻/Cr(OH)₃): hoạt độ dạng khử = 1
        }

        static List<Couple> couples;

        static (string, double)[] S(params (string, double)[] x) => x;

        static Couple C(string name, (string, double)[] ox, (string, double)[] red, int n, double m, double e0) =>
            new Couple { Name = name, Ox = ox, Red = red, N = n, M = m, E0 = e0 };

        /// <summary>
        /// Kim loại: tốc độ tan (mol/s ở [H⁺] = 0,1 M), nhiệt (kJ/mol), bị thụ động trong axit đặc nguội, nhiệt độ phá màng
        /// hiđroxit (Mg tác dụng với nước nóng), phản ứng với nước ở nhiệt độ thường (kiềm, kiềm thổ: phản ứng với nước trước).
        /// </summary>
        static readonly Dictionary<string, (double rate, double heat, bool passive, double hotWater, bool water)> MetalData = new Dictionary<string, (double, double, bool, double, bool)>
        {
            ["K(s)"] = (8e-3, 196, false, 0, true), ["Na(s)"] = (4e-3, 184, false, 0, true), ["Li(s)"] = (2e-3, 222, false, 0, true),
            ["Ba(s)"] = (3e-3, 390, false, 0, true), ["Ca(s)"] = (1.5e-3, 414, false, 0, true),
            ["Mg(s)"] = (2.5e-3, 467, false, 70, false), ["Al(s)"] = (5e-4, 531, true, 0, false), ["Zn(s)"] = (7e-4, 153, false, 0, false),
            ["Mn(s)"] = (6e-4, 221, false, 0, false), ["Cr(s)"] = (1e-4, 143, true, 0, false), ["Fe(s)"] = (2.5e-4, 87, true, 0, false),
            ["Co(s)"] = (8e-5, 56, false, 0, false), ["Ni(s)"] = (6e-5, 54, false, 0, false), ["Sn(s)"] = (8e-5, 9, false, 0, false),
            ["Pb(s)"] = (3e-5, 0, false, 0, false), ["Cu(s)"] = (3e-4, 0, false, 0, false), ["Ag(s)"] = (3e-4, 0, false, 0, false),
            ["Hg(l)"] = (2e-4, 0, false, 0, false), ["Au(s)"] = (2e-4, 0, false, 0, false),
        };

        static void BuildCouples()
        {
            if (couples != null) return;
            couples = new List<Couple>();
            // ---- kim loại: lấy từ bảng loài (Metal(...) trong Species.cs) ----
            foreach (var d in SpeciesRegistry.All)
                if (d.Kind == SpeciesKind.Solid && d.IonId != null && !double.IsNaN(d.E0))
                    couples.Add(new Couple { Name = SpeciesRegistry.Get(d.IonId).Formula + "/" + d.Formula, Ox = S((d.IonId, 1)), Red = S((d.Id, 1)), N = d.IonCharge, E0 = d.E0, IsMetal = true });
            // ---- hiđro: 2H⁺ + 2e → H₂ (axit loãng; với nước, kiềm thì E giảm theo pH) ----
            couples.Add(new Couple { Name = "2H⁺/H₂", Ox = S(), Red = S(("khi:H2", 1)), N = 2, M = 2, E0 = 0, OxOnly = true, IsHydrogen = true });
            // ---- ion kim loại nhiều số oxi hóa ----
            couples.Add(C("Fe³⁺/Fe²⁺", S(("Fe3+", 1)), S(("Fe2+", 1)), 1, 0, 0.771));
            couples.Add(C("Hg²⁺/Hg₂²⁺", S(("Hg2+", 2)), S(("Hg22+", 1)), 2, 0, 0.920));
            couples.Add(C("Hg₂²⁺/Hg", S(("Hg22+", 1)), S(("Hg(l)", 2)), 2, 0, 0.797));
            couples.Add(C("Sn⁴⁺/Sn²⁺", S(("Sn4+", 1)), S(("Sn2+", 1)), 2, 0, 0.151));
            couples.Add(C("Cu²⁺/CuI", S(("Cu2+", 1), ("I-", 1)), S(("CuI(s)", 1)), 1, 0, 0.86));
            couples.Add(new Couple { Name = "AuCl₄⁻/Au", Ox = S(("AuCl4-", 1)), Red = S(("Au(s)", 1), ("Cl-", 4)), N = 3, E0 = 1.002 });
            // ---- halogen (cả vai chất khử trong kiềm: tự oxi hóa - khử) ----
            couples.Add(new Couple { Name = "Cl₂/Cl⁻", Ox = S(("Cl2", 1)), Red = S(("Cl-", 2)), N = 2, E0 = 1.358, MinConcRed = 3, Dispro = true });
            couples.Add(new Couple { Name = "Br₂/Br⁻", Ox = S(("Br2", 1)), Red = S(("Br-", 2)), N = 2, E0 = 1.087, Dispro = true });
            couples.Add(new Couple { Name = "I₂/I⁻", Ox = S(("I2", 1)), Red = S(("I-", 2)), N = 2, E0 = 0.536, Dispro = true });
            couples.Add(new Couple { Name = "HClO/Cl⁻", Ox = S(("ClO", 1)), Red = S(("Cl-", 1)), N = 2, M = 1, E0 = 1.482, OxOnly = true });   // Cl⁻ chỉ bị oxi hóa đến Cl₂
            couples.Add(new Couple { Name = "HClO/Cl₂", Ox = S(("ClO", 2)), Red = S(("Cl2", 1)), N = 2, M = 2, E0 = 1.611, Dispro = true });
            couples.Add(new Couple { Name = "ClO₃⁻/Cl⁻", Ox = S(("ClO3-", 1)), Red = S(("Cl-", 1)), N = 6, M = 6, E0 = 1.451, OxOnly = true, PhMaxOx = 0 });
            couples.Add(new Couple { Name = "BrO₃⁻/Br₂", Ox = S(("BrO3-", 2)), Red = S(("Br2", 1)), N = 10, M = 12, E0 = 1.482, RedOnly = true, Dispro = true });
            couples.Add(new Couple { Name = "BrO₃⁻/Br⁻", Ox = S(("BrO3-", 1)), Red = S(("Br-", 1)), N = 6, M = 6, E0 = 1.423, OxOnly = true, PhMaxOx = 3 });
            couples.Add(new Couple { Name = "IO₃⁻/I₂", Ox = S(("IO3-", 2)), Red = S(("I2", 1)), N = 10, M = 12, E0 = 1.195, Dispro = true });
            // ---- mangan, crom, chì ----
            couples.Add(new Couple { Name = "MnO₄⁻/Mn²⁺", Ox = S(("MnO4-", 1)), Red = S(("Mn2+", 1)), N = 5, M = 8, E0 = 1.507, TMinRed = 60 });
            couples.Add(C("MnO₄⁻/MnO₂", S(("MnO4-", 1)), S(("MnO2(kt)", 1)), 3, 4, 1.679));
            couples.Add(new Couple { Name = "MnO₄⁻/MnO₄²⁻", Ox = S(("MnO4-", 1)), Red = S(("MnO42-", 1)), N = 1, E0 = 0.558, PhMinOx = 12, OxOnly = true });
            couples.Add(new Couple { Name = "MnO₂/Mn²⁺", Ox = S(("MnO2(kt)", 1)), Red = S(("Mn2+", 1)), N = 2, M = 4, E0 = 1.224, PhMinRed = 2 });
            couples.Add(new Couple { Name = "MnO₂(r)/Mn²⁺", Ox = S(("MnO2(s)", 1)), Red = S(("Mn2+", 1)), N = 2, M = 4, E0 = 1.224, OxOnly = true, TMinOx = 40 });
            couples.Add(new Couple { Name = "PbO₂/Pb²⁺", Ox = S(("PbO2(s)", 1)), Red = S(("Pb2+", 1)), N = 2, M = 4, E0 = 1.455, OxOnly = true });
            couples.Add(new Couple { Name = "Cr₂O₇²⁻/Cr³⁺", Ox = S(("CrVI", 1)), Red = S(("Cr3+", 1)), N = 3, M = 7, E0 = 1.33, PhMaxOx = 6, PhMaxRed = 6, TMinRed = 60 });
            couples.Add(new Couple { Name = "CrO₄²⁻/Cr(OH)₃", Ox = S(("CrVI", 1)), Red = S(("Cr3+", 1)), N = 3, M = 5, E0 = -0.13 + 0.05916 * 14 * 5 / 3, PhMinOx = 8, PhMinRed = 8, RedIsSolidForm = true });
            // ---- oxi, peoxit ----
            couples.Add(new Couple { Name = "H₂O₂/H₂O", Ox = S(("H2O2", 1)), Red = S(), N = 2, M = 2, E0 = 1.776, OxOnly = true });
            couples.Add(new Couple { Name = "O₂/H₂O₂", Ox = S(("khi:O2", 1)), Red = S(("H2O2", 1)), N = 2, M = 2, E0 = 0.695, RedOnly = true });
            couples.Add(new Couple { Name = "S₂O₈²⁻/SO₄²⁻", Ox = S(("S2O82-", 1)), Red = S(("SO4", 2)), N = 2, E0 = 2.01, OxOnly = true, Catalyst = "Ag+", TMinOx = 60, FastWith = new[] { "I-", "Fe2+" } });
            // ---- nitơ (HNO₃ đặc → NO₂, loãng → NO: SGK Hóa 11) ----
            couples.Add(new Couple { Name = "NO₃⁻/NO", Ox = S(("NO3-", 1)), Red = S(("khi:NO", 1)), N = 3, M = 4, E0 = 0.957, OxOnly = true, PhMaxOx = 1, MinConc = 0.05, MaxConc = 6 });
            couples.Add(new Couple { Name = "NO₃⁻/NO₂", Ox = S(("NO3-", 1)), Red = S(("khi:NO2", 1)), N = 1, M = 2, E0 = 0.803, OxOnly = true, PhMaxOx = 1, MinConc = 6 });
            couples.Add(new Couple { Name = "HNO₂/NO", Ox = S(("NO2", 1)), Red = S(("khi:NO", 1)), N = 1, M = 1, E0 = 0.983, OxOnly = true, PhMaxOx = 4 });
            couples.Add(new Couple { Name = "NO₃⁻/HNO₂", Ox = S(("NO3-", 1)), Red = S(("NO2", 1)), N = 2, M = 3, E0 = 0.934, RedOnly = true });
            // ---- lưu huỳnh (H₂SO₄ đặc nóng: phân tử H₂SO₄ là chất oxi hóa; E hiệu dụng, chỉ khi C ≥ 10 M và đun) ----
            couples.Add(new Couple { Name = "H₂SO₄ đặc/SO₂", Ox = S(("SO4", 1)), Red = S(("khi:SO2", 1)), N = 2, M = 4, E0 = 1.0, OxOnly = true, MinConc = 10, TMinOx = 50, TotalActivity = true });
            couples.Add(new Couple { Name = "SO₄²⁻/H₂SO₃", Ox = S(("SO4", 1)), Red = S(("SO3", 1)), N = 2, M = 4, E0 = 0.172, RedOnly = true });
            couples.Add(C("H₂SO₃/S", S(("SO3", 1)), S(("S(kt)", 1)), 4, 4, 0.45));
            couples.Add(new Couple { Name = "S/H₂S", Ox = S(("S(kt)", 1)), Red = S(("S", 1)), N = 2, M = 2, E0 = 0.144 });
            couples.Add(new Couple { Name = "S(r)/H₂S", Ox = S(("S(s)", 1)), Red = S(("S", 1)), N = 2, M = 2, E0 = 0.144, OxOnly = true, Dispro = true, TMinOx = 60, PhMinOx = 12 });
            couples.Add(new Couple { Name = "SO₄²⁻/S", Ox = S(("SO4", 1)), Red = S(("S(s)", 1)), N = 6, M = 8, E0 = 0.357, RedOnly = true, TMinRed = 60 });
            couples.Add(new Couple { Name = "H₂SO₃/S(r)", Ox = S(("SO3", 1)), Red = S(("S(s)", 1)), N = 4, M = 4, E0 = 0.45, RedOnly = true, TMinRed = 60, Dispro = true });
            couples.Add(new Couple { Name = "S₄O₆²⁻/S₂O₃²⁻", Ox = S(("S4O62-", 1)), Red = S(("S2O32-", 2)), N = 2, E0 = 0.08 });
            couples.Add(new Couple { Name = "SO₄²⁻/S₂O₃²⁻", Ox = S(("SO4", 2)), Red = S(("S2O32-", 1)), N = 8, M = 10, E0 = 0.29, RedOnly = true });
            // ---- cacbon, photpho, silic ----
            couples.Add(new Couple { Name = "CO₂/C", Ox = S(("khi:CO2", 1)), Red = S(("C(s)", 1)), N = 4, M = 4, E0 = 0.207, RedOnly = true, TMinRed = 60 });
            couples.Add(new Couple { Name = "CO₂/C₂O₄²⁻", Ox = S(("khi:CO2", 2)), Red = S(("Ox", 1)), N = 2, M = 2, E0 = -0.49, RedOnly = true, TMinRed = 55 });  // oxalat: đun 60 - 70 °C
            couples.Add(new Couple { Name = "H₃PO₄/P", Ox = S(("PO4", 1)), Red = S(("P(s)", 1)), N = 5, M = 5, E0 = -0.41, RedOnly = true, TMinRed = 60 });
            couples.Add(new Couple { Name = "SiO₃²⁻/Si", Ox = S(("SiO3", 1)), Red = S(("Si(s)", 1)), N = 4, M = 6, E0 = -1.70 + 0.05916 * 14 * 6 / 4, RedOnly = true, PhMinRed = 12 });  // màng SiO₂: chỉ tan trong kiềm
        }

        public static IReadOnlyList<Couple> Couples { get { BuildCouples(); return couples; } }

        // ------------------------------------------------------------------
        // Thế Nernst
        // ------------------------------------------------------------------

        /// <summary>
        /// Dạng tham chiếu của hệ axit yếu trong E° (số proton gắn): H₂SO₃, H₂S, HClO, HNO₂, H₃PO₄, CO₂ (H₂CO₃) là dạng đủ proton;
        /// SO₄²⁻, C₂O₄²⁻, SiO₃²⁻, CrO₄²⁻ là dạng mất hết proton. Nernst dùng nồng độ đúng dạng này ở pH hiện tại.
        /// </summary>
        static readonly Dictionary<string, int> RefProtons = new Dictionary<string, int>
        {
            ["SO3"] = 2, ["S"] = 2, ["ClO"] = 1, ["NO2"] = 1, ["PO4"] = 3, ["CO3"] = 2, ["F"] = 1,
            ["SO4"] = 0, ["Ox"] = 0, ["SiO3"] = 0, ["CrVI"] = 0,
        };

        /// <summary>Hoạt độ (≈ nồng độ mol/l) dùng cho Nernst: chất rắn, khí, nước = 1; cation dùng phần ion tự do; axit yếu dùng dạng tham chiếu.</summary>
        static double Activity(Work w, string id, double shift = 0, bool total = false)
        {
            if (id.StartsWith("khi:")) return 1;
            var d = SpeciesRegistry.Get(id);
            if (d == null) return 1e-9;
            if (d.Kind == SpeciesKind.Solid || d.Kind == SpeciesKind.Precipitate) return 1;
            double c = Math.Max(0, w.M(id) + shift) / w.V;
            if (d.Kind == SpeciesKind.Ion && d.KspOH > 0)
            {
                MetalSplit(d, c, w.h, out double free, out _, out _);
                c = free;
            }
            else if (!total && d.Kind == SpeciesKind.Weak && d.Ka != null && RefProtons.TryGetValue(id, out int k))
                c *= AlphaK(d, w.h, Math.Min(k, d.Ka.Length));
            return Math.Max(c, 1e-12);
        }

        static bool Present(Work w, (string id, double nu)[] side)
        {
            foreach (var (id, _) in side)
            {
                if (id.StartsWith("khi:")) continue;
                if (w.M(id) <= 1e-12) return false;
            }
            return true;
        }

        /// <summary>E của cặp theo Nernst. extentE: số mol electron đã trao đổi (tìm điểm cân bằng); roleSign +1: cặp đang bị khử, −1: đang bị oxi hóa.</summary>
        static double Nernst(Work w, Couple c, double extentE = 0, int roleSign = 0)
        {
            double lq = 0;
            if (!c.RedIsSolidForm) foreach (var (id, nu) in c.Red) lq += nu * Math.Log10(Activity(w, id, roleSign * nu * extentE / c.N, c.TotalActivity));
            foreach (var (id, nu) in c.Ox) lq -= nu * Math.Log10(Activity(w, id, -roleSign * nu * extentE / c.N, c.TotalActivity));
            lq += c.M * w.Ph;   // − m·log[H⁺]
            lq = Math.Max(-400, Math.Min(400, lq));   // hoạt độ đã chặn dưới 1e-9 nên không vô cùng; số hạng pH có thể lớn (m·pH)
            return c.E0 - 0.05916 / c.N * lq;
        }

        /// <summary>Có chất rắn (kim loại, MnO₂ rắn, phi kim) tham gia: phản ứng xảy ra trên bề mặt, theo thời gian.</summary>
        static bool HasSolid((string id, double nu)[] side)
        {
            foreach (var (id, _) in side)
            {
                var d = SpeciesRegistry.Get(id);
                if (d != null && d.Kind == SpeciesKind.Solid) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Màng bảo vệ của kim loại
        // ------------------------------------------------------------------

        /// <summary>Lý do kim loại không phản ứng dù thế cho phép (thụ động hóa, màng muối ít tan, màng hiđroxit), hoặc null.</summary>
        static string MetalFilm(Work w, Couple metal, Couple ox)
        {
            var md = SpeciesRegistry.Get(metal.Red[0].id);
            var ion = SpeciesRegistry.Get(metal.Ox[0].id);
            MetalData.TryGetValue(md.Id, out var data);
            // thụ động hóa trong HNO₃ đặc nguội, H₂SO₄ đặc nguội (SGK Hóa 12)
            if (data.passive && w.T < 60 && (w.C("NO3-") > 8 && w.Ph < 0 || w.C("SO4") > 12))
                return "thudong";
            // màng muối ít tan của chính kim loại với gốc axit có trong dung dịch (PbSO₄, PbCl₂, CaSO₄, BaSO₄...)
            if (ox.IsHydrogen)
                foreach (var p in Pairs)
                {
                    if (p.Cation != ion.Id || p.Solubility > 0.05 || p.Anion == "CO3" || p.Anion == "S" || p.Anion == "SO3") continue;
                    if (w.C(p.Anion) > 0.02) return "mang_muoi:" + p.Ppt;
                }
            // màng hiđroxit trong nước, dung dịch trung tính / kiềm: bề mặt có pH cao do H₂ thoát ra
            if (ox.IsHydrogen && ion.KspOH > 0 && w.Ph > 4)
            {
                if (data.hotWater > 0 && w.T >= data.hotWater) return null;
                double hLocal = Math.Min(w.h, 1e-10);
                double allowed = ion.KspOH / Math.Pow(Kw / hLocal, ion.OHn) + (ion.AmphK > 0 ? ion.AmphK * Math.Pow(Kw / w.h, ion.AmphM) : 0);
                if (allowed < 0.1) return "mang_hidroxit";
            }
            return null;
        }

        // ------------------------------------------------------------------
        // Áp dụng
        // ------------------------------------------------------------------

        sealed class Candidate { public Couple Ox, Red; public double Eox, Ered; }

        /// <summary>
        /// Một lượt oxi hóa - khử. heterogeneous = false: phản ứng trong dung dịch (trong cân bằng, tới cân bằng Nernst).
        /// true: phản ứng có chất rắn (kim loại, MnO₂ rắn, phi kim) chạy theo thời gian dt.
        /// </summary>
        static bool RedoxTable(Work w, bool heterogeneous = false, double dt = 0, double stir = 1)
        {
            BuildCouples();
            bool changed = false;
            for (int round = 0; round < 8; round++)
            {
                var best = new Dictionary<string, Candidate>();
                double ph = w.Ph;
                foreach (var ox in couples)
                {
                    if (ox.RedOnly || ph < ox.PhMinOx || ph > ox.PhMaxOx || w.T < ox.TMinOx && ox.Catalyst == null) continue;
                    if (!ox.IsHydrogen && !Present(w, ox.Ox)) continue;
                    if (ox.MinConc > 0 || ox.MaxConc < 1e9)
                    {
                        double c0 = w.C(ox.Ox[0].id);
                        if (c0 < ox.MinConc || c0 > ox.MaxConc) continue;
                    }
                    double eOx = Nernst(w, ox);
                    foreach (var red in couples)
                    {
                        if (red.OxOnly || ReferenceEquals(red, ox) || ph < red.PhMinRed || ph > red.PhMaxRed || w.T < red.TMinRed) continue;
                        if (red.Red.Length == 0 || !Present(w, red.Red)) continue;
                        if (red.MinConcRed > 0 && w.C(red.Red[0].id) < red.MinConcRed) continue;
                        // H⁺ chỉ bị khử thành H₂ trên bề mặt kim loại (hoặc Si trong kiềm), không bị khử bởi ion trong dung dịch
                        if (ox.IsHydrogen && !(red.IsMetal || red.Red[0].id == "Si(s)")) continue;
                        string oxSp = ox.IsHydrogen ? "H+" : ox.Ox[0].id, redSp = red.Red[0].id;
                        // tự oxi hóa - khử chỉ với chất có cột Dispro (các chất khác về nhiệt động thì được nhưng rất chậm: H₂O₂, H₂SO₃)
                        if (oxSp == redSp && !(ox.Dispro && red.Dispro)) continue;
                        // dạng oxi hóa của chất khử không được trùng chính chất oxi hóa
                        if (red.Ox.Length > 0 && !ox.IsHydrogen && red.Ox[0].id == oxSp) continue;
                        bool solid = HasSolid(ox.Ox) || HasSolid(red.Red);
                        if (solid != heterogeneous) continue;
                        if (ox.Catalyst != null && !(w.C(ox.Catalyst) > 1e-5 && w.T >= ox.TMinOx) && (ox.FastWith == null || Array.IndexOf(ox.FastWith, red.Red[0].id) < 0)) continue;
                        double eRed = Nernst(w, red);
                        if (eOx - eRed < (heterogeneous ? 0.05 : 0.1)) continue;
                        // kim loại có màng bảo vệ với chất oxi hóa này: bỏ cặp (chất oxi hóa khác vẫn có thể phản ứng)
                        if (heterogeneous && red.IsMetal)
                        {
                            string film = MetalFilm(w, red, ox);
                            if (film != null) { FilmEvent(w, red.Red[0].id, film); continue; }
                        }
                        string key = (ox.IsHydrogen ? "H+" : ox.Ox[0].id) + "|" + red.Red[0].id;
                        // chất oxi hóa bị khử xuống mức sâu nhất còn khả thi (E thấp nhất); chất khử lên mức oxi hóa kế tiếp
                        // (E thấp nhất: Fe → Fe²⁺ trước, Fe²⁺ → Fe³⁺ ở lượt sau nếu chất oxi hóa đủ mạnh)
                        if (best.TryGetValue(key, out var cur))
                        {
                            bool sameOx = ReferenceEquals(cur.Ox, ox);
                            if (sameOx ? eRed >= cur.Ered : eOx >= cur.Eox - 1e-9) continue;
                        }
                        best[key] = new Candidate { Ox = ox, Red = red, Eox = eOx, Ered = eRed };
                    }
                }
                if (best.Count == 0) break;
                // một chất khử phản ứng với chất oxi hóa mạnh nhất trước (Fe trong HNO₃ không giải phóng H₂)
                var strongest = new Dictionary<string, Candidate>();
                foreach (var cnd in best.Values)
                {
                    string r = cnd.Red.Red[0].id;
                    if (!strongest.TryGetValue(r, out var cur) || cnd.Eox - cnd.Ered > cur.Eox - cur.Ered) strongest[r] = cnd;
                }
                var list = new List<Candidate>(strongest.Values);
                list.Sort((a, b) => (b.Eox - b.Ered).CompareTo(a.Eox - a.Ered));
                bool any = false;
                foreach (var cand in list)
                {
                    if (!Present(w, cand.Red.Red) || (!cand.Ox.IsHydrogen && !Present(w, cand.Ox.Ox))) continue;
                    any |= heterogeneous ? ApplySolid(w, cand, dt, stir) : ApplyHomogeneous(w, cand);
                }
                changed |= any;
                if (!any || heterogeneous) break;
                w.h = SolveH(w.b, w.V);
            }
            return changed;
        }

        /// <summary>Có phản ứng oxi hóa - khử với chất rắn (kim loại, MnO₂, phi kim) sắp xảy ra theo thời gian không.</summary>
        public static bool SolidRedoxPending(Mix mix, double ml, ChemEnv env)
        {
            if (mix == null || ml < 0.002) return false;
            var b = mix.ToBuilder();
            double V = ml / 1000.0;
            var w = new Work { b = b, V = V, env = env, events = null, res = new Result() };
            w.h = SolveH(b, V);
            BuildCouples();
            foreach (var ox in couples)
            {
                if (ox.RedOnly || w.Ph < ox.PhMinOx || w.Ph > ox.PhMaxOx || w.T < ox.TMinOx) continue;
                if (!ox.IsHydrogen && !Present(w, ox.Ox)) continue;
                if (ox.MinConc > 0 || ox.MaxConc < 1e9) { double c0 = w.C(ox.Ox[0].id); if (c0 < ox.MinConc || c0 > ox.MaxConc) continue; }
                double eOx = Nernst(w, ox);
                foreach (var red in couples)
                {
                    if (red.OxOnly || ReferenceEquals(red, ox) || w.Ph < red.PhMinRed || w.Ph > red.PhMaxRed || w.T < red.TMinRed) continue;
                    if (red.Red.Length == 0 || !Present(w, red.Red) || !(HasSolid(ox.Ox) || HasSolid(red.Red))) continue;
                    if (eOx - Nernst(w, red) < 0.05) continue;
                    if (red.IsMetal && MetalFilm(w, red, ox) != null) continue;
                    return true;
                }
            }
            return false;
        }

        static double MaxElectrons(Work w, (string id, double nu)[] side, int n)
        {
            double e = double.MaxValue;
            foreach (var (id, nu) in side)
            {
                if (id.StartsWith("khi:")) continue;
                e = Math.Min(e, w.M(id) / nu * n);
            }
            return e;
        }

        static bool ApplyHomogeneous(Work w, Candidate c)
        {
            double eMax = Math.Min(MaxElectrons(w, c.Ox.Ox, c.Ox.N), MaxElectrons(w, c.Red.Red, c.Red.N));
            if (eMax <= 1e-12 || double.IsInfinity(eMax) || eMax == double.MaxValue) return false;
            // điểm cân bằng: E(oxi hóa) = E(khử), pH giải lại theo lượng đã phản ứng (phản ứng sinh hoặc tiêu thụ H⁺)
            double h0 = w.h;
            double Gap(double e)
            {
                Shift(w, c, e, +1);
                w.h = SolveH(w.b, w.V);
                double g = Nernst(w, c.Ox) - Nernst(w, c.Red);
                Shift(w, c, e, -1);
                w.h = h0;
                return g;
            }
            double lo = 0, hi = eMax;
            if (Gap(eMax) > 0) lo = eMax;
            else
                for (int it = 0; it < 30; it++)
                {
                    double mid = 0.5 * (lo + hi);
                    if (Gap(mid) > 0) lo = mid; else hi = mid;
                }
            // chỉ tính là có phản ứng khi chuyển dịch đáng kể (≥ 2% chất hạn chế); lượng vết theo Nernst thì không báo
            if (lo <= 1e-11 || lo < 0.02 * eMax) return false;
            Transfer(w, c, lo);
            return true;
        }

        /// <summary>Thêm (sign = +1) hoặc bỏ (−1) phần chuyển đổi ứng với e mol electron, không kể khí bay ra.</summary>
        static void Shift(Work w, Candidate c, double e, int sign)
        {
            void Side((string id, double nu)[] side, double s, int n)
            {
                foreach (var (id, nu) in side) if (!id.StartsWith("khi:")) w.b.Add(id, sign * s * nu * e / n);
            }
            Side(c.Ox.Ox, -1, c.Ox.N); Side(c.Ox.Red, +1, c.Ox.N);
            Side(c.Red.Red, -1, c.Red.N); Side(c.Red.Ox, +1, c.Red.N);
        }

        static bool ApplySolid(Work w, Candidate c, double dt, double stir)
        {
            string metalId = c.Red.IsMetal ? c.Red.Red[0].id : null;
            double org = OrganicFraction(w.b, w.V);
            if (metalId != null)
            {
                // kim loại kiềm, kiềm thổ trong dung dịch nước: phản ứng với nước trước (SGK Hóa 12)
                if (MetalData.TryGetValue(metalId, out var mw) && mw.water && !c.Ox.IsHydrogen && org < 0.5) return false;
                // trong dầu hỏa, benzen (gần như không có nước): không phản ứng với nước
                if (c.Ox.IsHydrogen && org > 0.5) return false;
            }
            double eMax = Math.Min(c.Ox.IsHydrogen ? double.MaxValue : MaxElectrons(w, c.Ox.Ox, c.Ox.N), MaxElectrons(w, c.Red.Red, c.Red.N));
            if (eMax <= 1e-12 || eMax == double.MaxValue) return false;
            double rate;
            if (metalId != null && MetalData.TryGetValue(metalId, out var m))
            {
                double factor = c.Ox.IsHydrogen ? (w.Ph < 4 ? Math.Min(3, Math.Max(0.02, w.h / 0.1)) : m.water ? 1 : 0.05) : 1;
                rate = m.rate * factor;
            }
            else rate = 2.5e-4;
            if (!c.Ox.IsHydrogen && c.Ox.Ox.Length > 0 && !HasSolid(c.Ox.Ox)) rate *= Math.Min(1, w.C(c.Ox.Ox[0].id) / 0.05);
            double e = Math.Min(eMax, rate * c.Red.N * dt * stir);
            if (e <= 1e-14) return false;
            if (metalId != null && MetalData.TryGetValue(metalId, out var mh) && c.Ox.IsHydrogen) w.res.HeatJ += mh.heat * 1000 * e / c.Red.N;
            Transfer(w, c, e);
            return true;
        }

        static void FilmEvent(Work w, string metalId, string film)
        {
            if (w.events == null) return;
            var md = SpeciesRegistry.Get(metalId);
            if (film == "thudong")
                Emit(w, EventKind.Reaction, "thudong:" + metalId, $"{Cap(md.Name)} không tan (bị thụ động hóa)", "Al, Fe, Cr bị thụ động trong HNO₃ đặc nguội, H₂SO₄ đặc nguội: lớp oxit bền phủ kín bề mặt.");
            else if (film.StartsWith("mang_muoi:"))
                Emit(w, EventKind.Reaction, "thudong:" + metalId, $"{Cap(md.Name)} gần như không tan: lớp {SpeciesRegistry.Get(film.Substring(10)).Formula} ít tan bọc ngoài",
                     "Muối tạo thành không tan phủ kín bề mặt kim loại, ngăn phản ứng tiếp tục.");
        }

        /// <summary>Chuyển e mol electron: chất oxi hóa → dạng khử, chất khử → dạng oxi hóa; ghi hiện tượng và phương trình.</summary>
        static void Transfer(Work w, Candidate c, double e)
        {
            string gas = null; double gasMol = 0;
            void Change((string id, double nu)[] side, double sign, int n)
            {
                foreach (var (id, nu) in side)
                {
                    double amt = nu * e / n;
                    if (id.StartsWith("khi:")) { if (sign > 0) { if (gas == null || id != "khi:CO2") gas = id.Substring(4); gasMol += amt; } continue; }
                    w.b.Add(id, sign * amt);
                }
            }
            var ev = w.events != null ? RedoxEvent(w, c) : default;
            Change(c.Ox.Ox, -1, c.Ox.N);
            Change(c.Ox.Red, +1, c.Ox.N);
            Change(c.Red.Red, -1, c.Red.N);
            Change(c.Red.Ox, +1, c.Red.N);
            if (w.events == null) return;
            string redId = c.Red.Red[0].id, oxId = c.Ox.IsHydrogen ? "H+" : c.Ox.Ox[0].id;
            string key;
            if (c.Ox.IsHydrogen && c.Red.IsMetal) key = "khi:H2:" + redId;
            else if (c.Red.IsMetal && SpeciesRegistry.Get(oxId)?.Kind == SpeciesKind.Ion && SpeciesRegistry.Get(oxId).Charge > 0) key = "daykl:" + redId + ":" + oxId;
            else if (gas != null && HasSolid(c.Red.Red)) key = "khi:" + gas + ":" + redId;
            else if (gas != null && HasSolid(c.Ox.Ox)) key = "khi:" + gas + ":" + oxId;
            else key = "pu:" + oxId + "+" + redId;
            bool toxic = gas == "Cl2" || gas == "NO" || gas == "NO2" || gas == "SO2" || gas == "H2S";
            Emit(w, gas != null ? EventKind.Gas : EventKind.Reaction, key, ev.title, ev.detail, ev.eq, toxic ? Severity.Warning : Severity.Info, toxic ? "khi_doc" : null,
                 gas, gas != null ? gasMol : e, SpeciesRegistry.Get(oxId)?.Color);
        }

        // ------------------------------------------------------------------
        // Chú thích: tiêu đề từ bảng màu, phương trình từ bộ cân bằng
        // ------------------------------------------------------------------

        /// <summary>Công thức của loài trong phương trình ion thu gọn, theo dạng tồn tại ở pH hiện tại.</summary>
        static string FormOf(string id, Work w)
        {
            if (id.StartsWith("khi:")) return id.Substring(4);
            var d = SpeciesRegistry.Get(id);
            if (d == null) return id;
            if (id == "CrVI") return w.Ph < 6 ? "Cr2O7^2-" : "CrO4^2-";
            if (d.Kind == SpeciesKind.Weak && d.Ka != null)
            {
                int k = (int)Math.Round(WeakCharge(d, w.h) - (d.Charge - d.Ka.Length));
                return ChemObserver.Form(d, Math.Max(0, Math.Min(d.Ka.Length, k))) ?? d.Formula;
            }
            if (d.Kind == SpeciesKind.Ion && d.KspOH > 0 && w.Ph > 8 && d.HydroxideId != null)
            {
                string metal = AsciiOf(SpeciesRegistry.Get(d.MetalId ?? "")?.Formula ?? "") ?? "";
                if (d.AmphK > 0 && w.Ph > 11 && metal.Length > 0) return $"[{metal}(OH){d.OHn + d.AmphM}]^{(d.AmphM == 1 ? "" : d.AmphM.ToString())}-";
                return d.HydroxideId;
            }
            return d.Formula;
        }

        static (string title, string detail, string eq) RedoxEvent(Work w, Candidate c)
        {
            var lhs = new List<string>(); var rhs = new List<string>();
            void Add(List<string> to, (string id, double nu)[] side) { foreach (var (id, _) in side) { string f = FormOf(id, w); if (!to.Contains(f)) to.Add(f); } }
            Add(lhs, c.Red.Red); Add(lhs, c.Ox.Ox);
            if (c.Ox.IsHydrogen && w.Ph < 7) lhs.Add("H+");
            Add(rhs, c.Red.Ox); Add(rhs, c.Ox.Red);
            string eq = Equation.BalanceAuto(lhs, rhs, w.Ph < 7);
            var gases = new List<string>();
            foreach (var (id, _) in c.Ox.Red) if (id.StartsWith("khi:")) gases.Add(id.Substring(4));
            foreach (var (id, _) in c.Red.Ox) if (id.StartsWith("khi:")) gases.Add(id.Substring(4));
            if (eq != null)
                foreach (var gg in gases)
                {
                    string g = Equation.Pretty(gg);
                    if (eq.EndsWith(" " + g)) eq += "↑";
                    eq = eq.Replace(" " + g + " +", " " + g + "↑ +");
                }
            // tiêu đề: những gì nhìn thấy được
            var parts = new List<string>();
            string redId = c.Red.Red[0].id;
            var rd = SpeciesRegistry.Get(redId);
            if (rd != null && rd.Kind == SpeciesKind.Solid) parts.Add($"{rd.Name} tan dần");
            if (!c.Ox.IsHydrogen)
            {
                string oxId = c.Ox.Ox[0].id;
                var od = SpeciesRegistry.Get(oxId);
                string oxName = ChemPalette.NameAt(oxId, w.Ph);
                if (od != null && od.Kind == SpeciesKind.Solid) parts.Add($"{od.Formula} {oxName ?? ""} tan dần".Replace("  ", " "));
                else if (od != null && oxName != null && oxName != "không màu" && !oxName.StartsWith("hồng rất nhạt")) parts.Add($"màu {oxName.Split('(')[0].Trim()} của {Equation.Pretty(FormOf(oxId, w))} nhạt dần");
            }
            void Show((string id, double nu)[] prods)
            {
                foreach (var (id, _) in prods)
                {
                    if (id.StartsWith("khi:")) continue;
                    var sp = SpeciesRegistry.Get(id);
                    string name = ChemPalette.NameAt(id, w.Ph);
                    if (sp == null || name == null || name == "không màu" || name.StartsWith("hồng rất nhạt")) continue;
                    string f = Equation.Pretty(FormOf(id, w));
                    if (sp.Kind == SpeciesKind.Precipitate) parts.Add($"xuất hiện kết tủa {name} {f}");
                    else if (sp.Kind == SpeciesKind.Solid) parts.Add($"{sp.Name} màu {name} bám ra");
                    else parts.Add($"dung dịch có màu {name.Split('(')[0].Trim()} ({f})");
                }
            }
            Show(c.Ox.Red); Show(c.Red.Ox);
            foreach (var gg in gases) parts.Add(GasPhrase(gg));
            string title = parts.Count == 0 ? $"Phản ứng oxi hóa - khử giữa cặp {c.Ox.Name} và {c.Red.Name} (không có dấu hiệu rõ)" : string.Join(", ", parts);
            title = char.ToUpper(title[0]) + title.Substring(1);
            string detail = $"E({c.Ox.Name}) ≈ {c.Eox:0.00} V > E({c.Red.Name}) ≈ {c.Ered:0.00} V (tính theo Nernst ở nồng độ, pH hiện tại): " +
                            "chất oxi hóa mạnh hơn tác dụng với chất khử mạnh hơn (quy tắc α).";
            if (c.Ox.IsHydrogen && rd != null)
                detail = MetalData.TryGetValue(redId, out var md) && md.water && w.Ph > 4
                    ? $"Kim loại rất hoạt động (E° = {rd.E0:0.00} V) khử được nước ở nhiệt độ thường; dung dịch thu được có tính bazơ."
                    : $"Kim loại đứng trước H (E° = {rd.E0:0.00} V < E(2H⁺/H₂) ≈ {c.Eox:0.00} V ở pH này) đẩy được H₂.";
            return (title, detail, eq);
        }

        static string GasPhrase(string gas)
        {
            switch (gas)
            {
                case "H2": return "sủi bọt khí không màu H₂";
                case "NO": return "khí NO không màu thoát ra, hóa nâu đỏ ở miệng ống";
                case "NO2": return "khí NO₂ màu nâu đỏ thoát ra";
                case "SO2": return "khí SO₂ mùi hắc thoát ra";
                case "Cl2": return "khí Cl₂ màu vàng lục thoát ra";
                case "CO2": return "sủi bọt khí CO₂";
                case "O2": return "sủi bọt khí O₂";
                case "H2S": return "khí H₂S mùi trứng thối thoát ra";
                default: return "khí " + SpeciesRegistry.Pretty(gas) + " thoát ra";
            }
        }
    }
}
