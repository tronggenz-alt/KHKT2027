using System;
using System.Collections.Generic;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Bộ máy hóa học tổng quát cho phòng thí nghiệm ảo.
    ///
    /// Cân bằng (Equilibrate, chạy mỗi khi trộn):
    ///   pH từ bảo toàn điện tích: [H⁺] − [OH⁻] + Σ zᵢcᵢ = 0. Không lưu H⁺/OH⁻: axit chỉ để lại gốc axit,
    ///   bazơ chỉ để lại cation, nên trộn theo thứ tự nào cũng ra đúng pH (giống mô hình chuẩn độ cũ).
    ///   Axit/bazơ yếu lưu theo tổng (CO₃ = H₂CO₃ + HCO₃⁻ + CO₃²⁻); hiđroxit kim loại kết tủa theo Ksp
    ///   ngay trong phương trình điện tích (lưỡng tính thì tan lại trong kiềm dư); kết tủa cặp ion theo Ksp;
    ///   phức NH₃, SCN⁻, poliol; khí thoát khi vượt độ tan. Kết tủa và phức tan lại khi điều kiện đổi.
    ///
    /// Phản ứng không thuận nghịch (oxi hóa - khử, nhận biết hữu cơ) chạy ngay trong cân bằng;
    /// phản ứng có tốc độ (kim loại tan, chất rắn tan, cần đun nóng) chạy trong Step theo thời gian.
    ///
    /// Không dùng UnityEngine: kiểm thử chạy độc lập (tests/ChemEngineTests.cs).
    /// </summary>
    public static partial class ChemEngine
    {
        public const double Kw = 1e-14;
        /// <summary>Thể tích tối thiểu (lít) để coi là có dung dịch (dưới mức này là chất rắn khô).</summary>
        public const double MinLiquidL = 2e-6;

        // ------------------------------------------------------------------
        // pH
        // ------------------------------------------------------------------

        /// <summary>pH của một lượng chất (không đổi thành phần). Nhớ kết quả trong Mix.</summary>
        public static double Ph(Mix mix, double volumeL)
        {
            if (mix == null || volumeL < MinLiquidL) return 7.0;
            if (!double.IsNaN(mix.cachedPh) && Math.Abs(mix.cachedPhVolume - volumeL) < 1e-12) return mix.cachedPh;
            var b = mix.ToBuilder();
            double ph = -Math.Log10(SolveH(b, volumeL));
            mix.cachedPh = ph;
            mix.cachedPhVolume = volumeL;
            return ph;
        }

        /// <summary>Phần điện tích của hệ axit yếu ở [H⁺] = h (theo 1 mol tổng).</summary>
        public static double WeakCharge(SpeciesDef d, double h)
        {
            var ka = d.Ka;
            int n = ka.Length;
            double term = Math.Pow(h, n), D = term, q = d.Charge * term;
            double prod = 1;
            for (int i = 1; i <= n; i++)
            {
                prod *= ka[i - 1];
                double t = prod * Math.Pow(h, n - i);
                D += t;
                q += (d.Charge - i) * t;
            }
            return q / D;
        }

        /// <summary>Tỉ lệ dạng mất hết proton (CO₃²⁻, S²⁻, PO₄³⁻...) ở [H⁺] = h.</summary>
        public static double AlphaLast(SpeciesDef d, double h)
        {
            if (d.Kind != SpeciesKind.Weak || d.Ka == null) return 1;
            var ka = d.Ka;
            int n = ka.Length;
            double D = Math.Pow(h, n), prod = 1, last = 0;
            for (int i = 1; i <= n; i++)
            {
                prod *= ka[i - 1];
                double t = prod * Math.Pow(h, n - i);
                D += t;
                if (i == n) last = t;
            }
            return last / D;
        }

        /// <summary>Tỉ lệ dạng nhiều proton nhất (H₂CO₃, H₂S, NH₄⁺, CH₃COOH...).</summary>
        public static double AlphaFirst(SpeciesDef d, double h)
        {
            if (d.Kind != SpeciesKind.Weak || d.Ka == null) return 1;
            var ka = d.Ka;
            int n = ka.Length;
            double first = Math.Pow(h, n), D = first, prod = 1;
            for (int i = 1; i <= n; i++)
            {
                prod *= ka[i - 1];
                D += prod * Math.Pow(h, n - i);
            }
            return first / D;
        }

        /// <summary>Cation tạo hiđroxit: (tan dạng ion, tan dạng phức hiđroxo, kết tủa) theo nồng độ (mol/l).</summary>
        public static void MetalSplit(SpeciesDef d, double cT, double h, out double free, out double hydroxo, out double ppt)
        {
            double oh = Kw / h;
            double s1 = d.KspOH / Math.Pow(oh, d.OHn);
            double s2 = d.AmphK > 0 ? d.AmphK * Math.Pow(oh, d.AmphM) : 0;
            if (double.IsInfinity(s1) || s1 + s2 >= cT)
            {
                double sum = s1 + s2;
                free = double.IsInfinity(s1) || sum <= 0 ? cT : cT * s1 / sum;
                hydroxo = cT - free;
                ppt = 0;
            }
            else
            {
                free = s1;
                hydroxo = s2;
                ppt = cT - s1 - s2;
            }
        }

        static double MetalCharge(SpeciesDef d, double cT, double h)
        {
            MetalSplit(d, cT, h, out double free, out double hydroxo, out _);
            double z = d.Charge;
            if (d.Ka != null && d.Ka.Length > 0) z -= d.Ka[0] / (d.Ka[0] + h);  // thủy phân: Fe³⁺ ⇌ FeOH²⁺ + H⁺
            return free * z - hydroxo * d.AmphM;
        }

        struct Term { public SpeciesDef d; public double c; }

        static readonly List<Term> terms = new List<Term>();

        /// <summary>[H⁺] thỏa bảo toàn điện tích (chia đôi trên log h, hàm điện tích tăng theo h).</summary>
        public static double SolveH(MixBuilder b, double V)
        {
            terms.Clear();
            foreach (int s in b.Indices)
            {
                var d = SpeciesRegistry.At(s);
                if (d.Kind == SpeciesKind.Ion || d.Kind == SpeciesKind.Weak || d.Kind == SpeciesKind.Complex)
                    terms.Add(new Term { d = d, c = b.Get(s) / V });
            }
            double lo = -16, hi = 1.6;
            for (int it = 0; it < 64; it++)
            {
                double mid = 0.5 * (lo + hi);
                double h = Math.Pow(10, mid);
                double f = h - Kw / h;
                foreach (var t in terms)
                {
                    var d = t.d;
                    if (d.Kind == SpeciesKind.Weak) f += t.c * WeakCharge(d, h);
                    else if (d.Kind == SpeciesKind.Ion && d.KspOH > 0) f += MetalCharge(d, t.c, h);
                    else f += d.Charge * t.c;
                }
                if (f > 0) hi = mid; else lo = mid;
            }
            return Math.Pow(10, 0.5 * (lo + hi));
        }

        // ------------------------------------------------------------------
        // Kết tủa cặp ion
        // ------------------------------------------------------------------

        public sealed class PairRule
        {
            public string Ppt, Cation, Anion, Color;
            public int A, B;
            public double Ksp;
            public bool BeatsAgComplex;
            public double Solubility;
            public string ColorName;
        }

        static List<PairRule> pairs;
        static HashSet<string> reversiblePpt;

        static void P(string ppt, string cat, int a, string an, int b, double ksp, string colorName, bool beats = false)
        {
            pairs.Add(new PairRule
            {
                Ppt = ppt, Cation = cat, A = a, Anion = an, B = b, Ksp = ksp, ColorName = colorName, BeatsAgComplex = beats,
                Solubility = Math.Pow(ksp / (Math.Pow(a, a) * Math.Pow(b, b)), 1.0 / (a + b)),
            });
        }

        public static IReadOnlyList<PairRule> Pairs { get { BuildPairs(); return pairs; } }

        static void BuildPairs()
        {
            if (pairs != null) return;
            pairs = new List<PairRule>();
            P("AgCl", "Ag+", 1, "Cl-", 1, 1.8e-10, "trắng");
            P("AgBr", "Ag+", 1, "Br-", 1, 5e-13, "vàng nhạt", true);
            P("AgI", "Ag+", 1, "I-", 1, 8.5e-17, "vàng", true);
            P("AgSCN", "Ag+", 1, "SCN-", 1, 1e-12, "trắng");
            P("Ag2CrO4", "Ag+", 2, "CrVI", 1, 1.1e-12, "đỏ gạch");
            P("Ag3PO4", "Ag+", 3, "PO4", 1, 8.9e-17, "vàng");
            P("Ag2S", "Ag+", 2, "S", 1, 6e-50, "đen", true);
            P("Ag2CO3", "Ag+", 2, "CO3", 1, 8.5e-12, "vàng nhạt");
            P("Ag2SO4", "Ag+", 2, "SO4", 1, 1.2e-5, "trắng");
            P("Ag2C2O4", "Ag+", 2, "Ox", 1, 5.4e-12, "trắng");
            P("BaSO4", "Ba2+", 1, "SO4", 1, 1.1e-10, "trắng");
            P("BaCO3", "Ba2+", 1, "CO3", 1, 2.6e-9, "trắng");
            P("BaCrO4", "Ba2+", 1, "CrVI", 1, 1.2e-10, "vàng");
            P("Ba3(PO4)2", "Ba2+", 3, "PO4", 2, 6e-39, "trắng");
            P("BaSO3", "Ba2+", 1, "SO3", 1, 5e-10, "trắng");
            P("BaC2O4", "Ba2+", 1, "Ox", 1, 1.6e-7, "trắng");
            P("BaF2", "Ba2+", 1, "F", 2, 1.8e-7, "trắng");
            P("SrSO4", "Sr2+", 1, "SO4", 1, 3.4e-7, "trắng");
            P("SrCO3", "Sr2+", 1, "CO3", 1, 5.6e-10, "trắng");
            P("CaCO3", "Ca2+", 1, "CO3", 1, 3.4e-9, "trắng");
            P("CaSO4", "Ca2+", 1, "SO4", 1, 4.9e-5, "trắng");
            P("Ca3(PO4)2", "Ca2+", 3, "PO4", 2, 2e-33, "trắng");
            P("CaC2O4", "Ca2+", 1, "Ox", 1, 2.3e-9, "trắng");
            P("CaF2", "Ca2+", 1, "F", 2, 3.5e-11, "trắng");
            P("MgCO3", "Mg2+", 1, "CO3", 1, 6.8e-6, "trắng");
            P("Mg3(PO4)2", "Mg2+", 3, "PO4", 2, 1e-24, "trắng");
            P("MgF2", "Mg2+", 1, "F", 2, 5.2e-11, "trắng");
            P("PbCl2", "Pb2+", 1, "Cl-", 2, 1.7e-5, "trắng");
            P("PbBr2", "Pb2+", 1, "Br-", 2, 6.6e-6, "trắng");
            P("PbI2", "Pb2+", 1, "I-", 2, 9.8e-9, "vàng tươi");
            P("PbSO4", "Pb2+", 1, "SO4", 1, 2.5e-8, "trắng");
            P("PbS", "Pb2+", 1, "S", 1, 3e-28, "đen");
            P("PbCrO4", "Pb2+", 1, "CrVI", 1, 2.8e-13, "vàng");
            P("PbCO3", "Pb2+", 1, "CO3", 1, 7.4e-14, "trắng");
            P("CuS", "Cu2+", 1, "S", 1, 6e-37, "đen");
            P("CuCO3", "Cu2+", 1, "CO3", 1, 1.4e-10, "xanh lục");
            P("ZnS", "Zn2+", 1, "S", 1, 2e-25, "trắng");
            P("ZnCO3", "Zn2+", 1, "CO3", 1, 1.5e-10, "trắng");
            P("Zn3(PO4)2", "Zn2+", 3, "PO4", 2, 9e-33, "trắng");
            P("FeS", "Fe2+", 1, "S", 1, 6e-19, "đen");
            P("FeCO3", "Fe2+", 1, "CO3", 1, 3.1e-11, "trắng xám");
            P("FePO4", "Fe3+", 1, "PO4", 1, 1.3e-22, "vàng nhạt");
            P("AlPO4", "Al3+", 1, "PO4", 1, 9.8e-21, "trắng");
            P("MnS", "Mn2+", 1, "S", 1, 3e-14, "hồng nhạt");
            P("MnCO3", "Mn2+", 1, "CO3", 1, 2.2e-11, "trắng hồng");
            P("NiS", "Ni2+", 1, "S", 1, 3e-21, "đen");
            P("CoS", "Co2+", 1, "S", 1, 4e-21, "đen");
            P("CdS", "Cd2+", 1, "S", 1, 8e-28, "vàng");
            P("HgS", "Hg2+", 1, "S", 1, 2e-53, "đen");
            P("Hg2Cl2", "Hg22+", 1, "Cl-", 2, 1.4e-18, "trắng");
            P("Fe4[Fe(CN)6]3", "Fe3+", 4, "FeCN64-", 3, 3e-41, "xanh đậm (xanh Phổ)");
            P("Fe3[Fe(CN)6]2", "Fe2+", 3, "FeCN63-", 2, 1e-40, "xanh đậm (xanh Turnbull)");
            P("Cu2[Fe(CN)6]", "Cu2+", 2, "FeCN64-", 1, 1.3e-16, "nâu đỏ");
            pairs.Sort((x, y) => x.Solubility.CompareTo(y.Solubility));
            reversiblePpt = new HashSet<string> { "H2SiO3", "CaXP" };
            foreach (var p in pairs) reversiblePpt.Add(p.Ppt);
        }

        static bool PairPrecipitate(MixBuilder b, double V, double h)
        {
            bool any = false;
            int agN = SpeciesRegistry.IndexOf("AgNH3"), nh3 = SpeciesRegistry.IndexOf("NH3");
            foreach (var r in pairs)
            {
                int ci = SpeciesRegistry.IndexOf(r.Cation), ai = SpeciesRegistry.IndexOf(r.Anion);
                double cat = b.Get(ci) / V;
                double fromComplex = 0;
                if (r.BeatsAgComplex && r.Cation == "Ag+") { fromComplex = b.Get(agN) / V; cat += fromComplex; }
                double an = b.Get(ai) / V;
                if (cat <= 0 || an <= 0) continue;
                var ad = SpeciesRegistry.At(ai);
                double frac = AlphaLast(ad, h);
                double q = Math.Pow(cat, r.A) * Math.Pow(frac * an, r.B);
                if (q <= r.Ksp * 1.0001) continue;
                double lo = 0, hi = Math.Min(cat / r.A, an / r.B);
                for (int it = 0; it < 60; it++)
                {
                    double x = 0.5 * (lo + hi);
                    double g = Math.Pow(Math.Max(0, cat - r.A * x), r.A) * Math.Pow(frac * Math.Max(0, an - r.B * x), r.B);
                    if (g > r.Ksp) lo = x; else hi = x;
                }
                double mol = lo * V;
                if (mol <= 1e-13) continue;
                double needCat = r.A * mol;
                double fromIon = Math.Min(b.Get(ci), needCat);
                b.Add(ci, -fromIon);
                double rest = needCat - fromIon;
                if (rest > 0)
                {
                    double took = b.Take(agN, rest);
                    b.Add(nh3, 2 * took);  // AgI, Ag₂S tạo được cả khi Ag⁺ đang ở dạng phức amoniac
                }
                b.Add(ai, -r.B * mol);
                b.Add(r.Ppt, mol);
                any = true;
            }
            // Axit silixic: gốc silicat gặp axit (pH < 9) tạo keo trắng
            int si = SpeciesRegistry.IndexOf("SiO3");
            double cs = b.Get(si) / V;
            if (cs > 2e-3 && h > 1e-9)
            {
                double mol = (cs - 2e-3) * V;
                b.Add(si, -mol);
                b.Add("H2SiO3", mol);
                any = true;
            }
            // Xà phòng gặp nước cứng (Ca²⁺, Mg²⁺): váng trắng
            double soap = b.Get("xaphong");
            if (soap > 1e-9)
            {
                foreach (var m in new[] { "Ca2+", "Mg2+" })
                {
                    double x = Math.Min(b.Get(m), soap / 2);
                    if (x <= 1e-9) continue;
                    b.Add(m, -x);
                    b.Add("xaphong", -2 * x);
                    b.Add("CaXP", x);
                    soap -= 2 * x;
                    any = true;
                }
            }
            return any;
        }

        // ------------------------------------------------------------------
        // Phức chất
        // ------------------------------------------------------------------

        static readonly (string metal, string complex, int n)[] AmmineComplexes =
        {
            ("Cu2+", "CuNH3", 4), ("Zn2+", "ZnNH3", 4), ("Ni2+", "NiNH3", 6), ("Ag+", "AgNH3", 2),
        };

        static void Complexate(MixBuilder b, double V, double h)
        {
            int nh3 = SpeciesRegistry.IndexOf("NH3");
            var nd = SpeciesRegistry.At(nh3);
            double free = b.Get(nh3) / V * (1 - AlphaFirst(nd, h));  // NH₃ tự do (không phải NH₄⁺)
            foreach (var (metal, complex, n) in AmmineComplexes)
            {
                if (free <= 1e-7) break;
                double cm = b.Get(metal) / V;
                if (cm <= 0) continue;
                double ratio = free / cm;
                // Cu²⁺: thiếu NH₃ thì chỉ ra Cu(OH)₂, dư nhiều (≥ 6 lần) thì tan hết thành phức xanh thẫm
                double f = metal == "Ag+" ? Clamp01((ratio - 1) / 2) : Clamp01((ratio - n * 0.5) / n);
                double x = Math.Min(f * cm, free / n);
                if (x <= 0) continue;
                b.Add(metal, -x * V);
                b.Add(nh3, -n * x * V);
                b.Add(complex, x * V);
                free -= n * x;
            }
            // Fe³⁺ + SCN⁻ → [Fe(SCN)]²⁺ đỏ máu (môi trường axit)
            double fe = b.Get("Fe3+"), scn = b.Get("SCN-");
            if (fe > 0 && scn > 0 && h > 1e-6)
            {
                double x = 0.95 * Math.Min(fe, scn);
                b.Add("Fe3+", -x);
                b.Add("SCN-", -x);
                b.Add("FeSCN", x);
            }
            // Cu(OH)₂ tan trong poliol (glixerol, glucozơ, saccarozơ) hoặc tactrat/xitrat (Fehling, Benedict): phức xanh lam
            if (h < 1e-8)
            {
                double cu = b.Get("Cu2+");
                if (cu > 0)
                {
                    double pol = b.Get("Tart") + b.Get("Cit");
                    foreach (int s in b.Indices)
                    {
                        var d = SpeciesRegistry.At(s);
                        if (d.Kind == SpeciesKind.Molecule && d.HasTag("poliol")) pol += b.Get(s);
                    }
                    double x = Math.Min(cu, pol / 2);
                    if (x > 0)
                    {
                        b.Add("Cu2+", -x);
                        b.Add("CuPol", x);
                    }
                }
            }
        }

        static void RevertReversible(MixBuilder b)
        {
            BuildPairs();
            foreach (int s in b.Snapshot())
            {
                var d = SpeciesRegistry.At(s);
                double m = b.Get(s);
                if (m <= 0) continue;
                if (d.Kind == SpeciesKind.Complex)
                {
                    switch (d.Id)
                    {
                        case "CuNH3": b.Set(s, 0); b.Add("Cu2+", m); b.Add("NH3", 4 * m); break;
                        case "ZnNH3": b.Set(s, 0); b.Add("Zn2+", m); b.Add("NH3", 4 * m); break;
                        case "NiNH3": b.Set(s, 0); b.Add("Ni2+", m); b.Add("NH3", 6 * m); break;
                        case "AgNH3": b.Set(s, 0); b.Add("Ag+", m); b.Add("NH3", 2 * m); break;
                        case "FeSCN": b.Set(s, 0); b.Add("Fe3+", m); b.Add("SCN-", m); break;
                        case "CuPol": b.Set(s, 0); b.Add("Cu2+", m); break;
                    }
                }
                else if (d.Kind == SpeciesKind.Precipitate && reversiblePpt.Contains(d.Id))
                {
                    b.Set(s, 0);
                    if (d.Id == "H2SiO3") b.Add("SiO3", m);
                    else if (d.Id == "CaXP") { b.Add("Ca2+", m); b.Add("xaphong", 2 * m); }
                    else
                    {
                        var r = pairs.Find(p => p.Ppt == d.Id);
                        b.Add(r.Cation, r.A * m);
                        b.Add(r.Anion, r.B * m);
                    }
                }
                else if (d.Kind == SpeciesKind.Precipitate && hydroxideOf.TryGetValue(d.Id, out var ion))
                {
                    b.Set(s, 0);
                    b.Add(ion, m);
                }
            }
        }

        static Dictionary<string, string> hydroxideOf;

        static void BuildHydroxideMap()
        {
            if (hydroxideOf != null) return;
            hydroxideOf = new Dictionary<string, string>();
            foreach (var d in SpeciesRegistry.All)
                if (d.Kind == SpeciesKind.Ion && d.KspOH > 0 && d.HydroxideId != null) hydroxideOf[d.HydroxideId] = d.Id;
        }

        /// <summary>Tách phần cation đã kết tủa hiđroxit thành loài kết tủa riêng (để hiện màu, ghi sự kiện).</summary>
        static void SplitHydroxides(MixBuilder b, double V, double h)
        {
            foreach (int s in b.Snapshot())
            {
                var d = SpeciesRegistry.At(s);
                if (d.Kind != SpeciesKind.Ion || d.KspOH <= 0) continue;
                MetalSplit(d, b.Get(s) / V, h, out _, out _, out double ppt);
                if (ppt * V <= 1e-12) continue;
                b.Add(s, -ppt * V);
                b.Add(d.HydroxideId, ppt * V);
            }
        }

        // ------------------------------------------------------------------
        // Cân bằng tổng
        // ------------------------------------------------------------------

        public sealed class Result
        {
            public double H = 1e-7;
            public double HeatJ;
            public bool Changed;
            public double Ph => -Math.Log10(H);
        }

        /// <summary>
        /// Đưa hỗn hợp về cân bằng. Ghi hiện tượng (kết tủa mới, tan, khí, phản ứng) vào events.
        /// V: thể tích dung dịch (lít). Trả về kết quả (H, nhiệt tỏa ra).
        /// </summary>
        public static Result Equilibrate(MixBuilder b, double V, ChemEnv env, List<ChemEvent> events)
        {
            BuildPairs();
            BuildHydroxideMap();
            var res = new Result();
            if (V < MinLiquidL) return res;
            var before = SolidSnapshot(b);

            var w = new Work { b = b, V = V, env = env, events = events, res = res };
            RevertReversible(b);
            w.h = SolveH(b, V);
            for (int outer = 0; outer < 6; outer++)
            {
                RevertReversible(b);
                bool changed = InstantRules(w);
                for (int it = 0; it < 5; it++)
                {
                    w.h = SolveH(b, V);
                    Complexate(b, V, w.h);
                    PairPrecipitate(b, V, w.h);
                }
                w.h = SolveH(b, V);
                changed |= GasEscape(w);
                if (changed) { res.Changed = true; continue; }
                break;
            }
            w.h = SolveH(b, V);
            SplitHydroxides(b, V, w.h);
            res.H = w.h;
            DiffSolids(before, b, V, events);
            return res;
        }

        static Dictionary<int, double> SolidSnapshot(MixBuilder b)
        {
            var d = new Dictionary<int, double>();
            foreach (int s in b.Indices)
            {
                var sp = SpeciesRegistry.At(s);
                if (sp.Kind == SpeciesKind.Precipitate || sp.Kind == SpeciesKind.Complex) d[s] = b.Get(s);
            }
            return d;
        }

        static void DiffSolids(Dictionary<int, double> before, MixBuilder b, double V, List<ChemEvent> events)
        {
            if (events == null) return;
            foreach (int s in b.Indices)
            {
                var sp = SpeciesRegistry.At(s);
                if (sp.Kind != SpeciesKind.Precipitate && sp.Kind != SpeciesKind.Complex) continue;
                double now = b.Get(s), was = before.TryGetValue(s, out var w) ? w : 0;
                double threshold = Math.Max(2e-7, 2e-5 * V);
                if (now - was > threshold && now > 3 * was)
                {
                    if (sp.Kind == SpeciesKind.Precipitate) events.Add(PrecipitateEvent(sp, now));
                    else events.Add(ComplexEvent(sp, now));
                }
            }
            foreach (var kv in before)
            {
                var sp = SpeciesRegistry.At(kv.Key);
                if (sp.Kind != SpeciesKind.Precipitate) continue;
                double now = b.Get(kv.Key);
                if (kv.Value > 2e-6 && now < 0.2 * kv.Value)
                    events.Add(new ChemEvent
                    {
                        Kind = EventKind.Dissolve, Key = "tan:" + sp.Id, Title = $"Kết tủa {sp.Formula} tan",
                        Detail = DissolveReason(sp.Id), Color = sp.Color, Amount = kv.Value - now,
                    });
            }
        }

        static string DissolveReason(string id)
        {
            switch (id)
            {
                case "Al(OH)3": case "Zn(OH)2": case "Pb(OH)2": case "Cr(OH)3": case "Sn(OH)2": case "Be(OH)2":
                    return "Hiđroxit lưỡng tính tan trong kiềm dư (hoặc trong axit).";
                case "Cu(OH)2": return "Cu(OH)₂ tan: tạo phức (với NH₃ dư hoặc poliol) hoặc bị axit hòa tan.";
                case "AgCl": case "Ag2O": return "Tạo phức [Ag(NH₃)₂]⁺ tan khi có amoniac dư, hoặc bị axit hòa tan.";
                case "CaCO3": case "BaCO3": case "MgCO3": return "Muối cacbonat tan trong axit, giải phóng CO₂.";
                default: return "Điều kiện dung dịch thay đổi (pH, chất tạo phức) làm kết tủa tan trở lại.";
            }
        }

        static ChemEvent PrecipitateEvent(SpeciesDef sp, double mol)
        {
            BuildPairs();
            var r = pairs.Find(p => p.Ppt == sp.Id);
            string eq = null, color = r?.ColorName;
            if (r != null)
            {
                var c = SpeciesRegistry.Get(r.Cation);
                string an = IonFormula(r.Anion);
                eq = $"{Coef(r.A)}{c.Formula} + {Coef(r.B)}{an} → {sp.Formula}↓";
            }
            else if (hydroxideOf.TryGetValue(sp.Id, out var ion))
            {
                var c = SpeciesRegistry.Get(ion);
                eq = ion == "Ag+" ? "2Ag⁺ + 2OH⁻ → Ag₂O↓ + H₂O" : $"{c.Formula} + {Coef(c.OHn)}OH⁻ → {sp.Formula}↓";
                color = HydroxideColorName(sp.Id);
            }
            string name = sp.Name != sp.Formula ? $"{sp.Formula} ({sp.Name})" : sp.Formula;
            return new ChemEvent
            {
                Kind = EventKind.Precipitate, Key = "kt:" + sp.Id, Color = sp.Color, Amount = mol, Equation = eq,
                Title = color != null ? $"Kết tủa {color}: {sp.Formula}" : $"Xuất hiện kết tủa {name}",
                Detail = PrecipitateNote(sp.Id),
            };
        }

        static string HydroxideColorName(string id)
        {
            switch (id)
            {
                case "Cu(OH)2": return "xanh lam";
                case "Fe(OH)3": return "nâu đỏ";
                case "Fe(OH)2": return "trắng xanh";
                case "Ni(OH)2": return "xanh lục";
                case "Co(OH)2": return "hồng";
                case "Cr(OH)3": return "xanh xám";
                case "Ag2O": return "đen nâu";
                case "HgO": return "vàng";
                case "Mn(OH)2": return "trắng (hóa nâu trong không khí)";
                default: return "trắng";
            }
        }

        static string PrecipitateNote(string id)
        {
            switch (id)
            {
                case "AgCl": return "Phản ứng nhận biết ion clorua. AgCl hóa đen dần ngoài ánh sáng, tan trong NH₃ dư.";
                case "AgBr": return "Nhận biết ion bromua; AgBr ít tan trong NH₃.";
                case "AgI": return "Nhận biết ion iotua; AgI không tan trong NH₃.";
                case "BaSO4": return "Nhận biết ion sunfat: BaSO₄ không tan cả trong axit mạnh.";
                case "CaCO3": return "Ví dụ: CO₂ làm vẩn đục nước vôi trong.";
                case "Fe(OH)2": return "Để ngoài không khí sẽ chuyển dần sang Fe(OH)₃ màu nâu đỏ.";
                case "Al(OH)3": return "Kết tủa keo trắng, lưỡng tính: tan trong kiềm dư và trong axit.";
                case "Zn(OH)2": return "Lưỡng tính: tan trong NaOH dư, tan cả trong NH₃ dư (tạo phức).";
                case "Cu(OH)2": return "Tan trong NH₃ dư tạo dung dịch xanh thẫm; tan trong glixerol, glucozơ.";
                case "PbI2": return "Kết tủa vàng tươi (thí nghiệm \"mưa vàng\").";
                case "Ag2CrO4": return "Dùng làm chỉ thị điểm cuối trong phép chuẩn độ Mohr.";
                case "Fe4[Fe(CN)6]3": return "Thuốc thử nhận biết Fe³⁺ bằng K₄[Fe(CN)₆].";
                case "Fe3[Fe(CN)6]2": return "Thuốc thử nhận biết Fe²⁺ bằng K₃[Fe(CN)₆].";
                case "CuS": case "PbS": case "Ag2S": return "Sunfua kim loại nặng màu đen, không tan trong axit loãng.";
                case "CdS": return "CdS màu vàng, dùng nhận biết ion Cd²⁺.";
                case "H2SiO3": return "Axit silixic yếu, kết tủa dạng keo khi axit hóa dung dịch silicat.";
                case "CaXP": return "Xà phòng mất tác dụng trong nước cứng vì tạo muối canxi không tan.";
                default: return "Ion sản phẩm vượt tích số tan nên tách ra khỏi dung dịch.";
            }
        }

        static ChemEvent ComplexEvent(SpeciesDef sp, double mol)
        {
            string title, eq, detail;
            switch (sp.Id)
            {
                case "CuNH3": title = "Dung dịch xanh thẫm [Cu(NH₃)₄]²⁺"; eq = "Cu(OH)₂ + 4NH₃ → [Cu(NH₃)₄](OH)₂"; detail = "NH₃ dư hòa tan Cu(OH)₂ tạo phức amin đồng màu xanh thẫm."; break;
                case "AgNH3": title = "Tạo phức tan [Ag(NH₃)₂]⁺"; eq = "Ag⁺ + 2NH₃ → [Ag(NH₃)₂]⁺"; detail = "Đây cũng là thuốc thử Tollens (tráng bạc)."; break;
                case "ZnNH3": title = "Zn(OH)₂ tan trong NH₃ dư"; eq = "Zn(OH)₂ + 4NH₃ → [Zn(NH₃)₄](OH)₂"; detail = "Khác Al(OH)₃: Zn(OH)₂ tạo phức với amoniac."; break;
                case "NiNH3": title = "Dung dịch xanh tím [Ni(NH₃)₆]²⁺"; eq = "Ni²⁺ + 6NH₃ → [Ni(NH₃)₆]²⁺"; detail = "Phức amin niken màu xanh tím."; break;
                case "FeSCN": title = "Màu đỏ máu [Fe(SCN)]²⁺"; eq = "Fe³⁺ + SCN⁻ → [Fe(SCN)]²⁺"; detail = "Phản ứng nhận biết ion Fe³⁺ rất nhạy."; break;
                case "CuPol": title = "Dung dịch xanh lam (phức đồng)"; eq = "2C₃H₅(OH)₃ + Cu(OH)₂ → [C₃H₅(OH)₂O]₂Cu + 2H₂O"; detail = "Ancol đa chức có các nhóm -OH kề nhau (glixerol, glucozơ, saccarozơ) hòa tan Cu(OH)₂."; break;
                case "FePhenol": title = "Màu tím đặc trưng (phenol + FeCl₃)"; eq = "6C₆H₅OH + Fe³⁺ → [Fe(OC₆H₅)₆]³⁻ + 6H⁺"; detail = "Nhận biết phenol bằng FeCl₃."; break;
                default: title = "Tạo phức " + sp.Formula; eq = null; detail = ""; break;
            }
            return new ChemEvent { Kind = EventKind.Color, Key = "phuc:" + sp.Id, Title = title, Equation = eq, Detail = detail, Color = sp.Color, Amount = mol };
        }

        static string Coef(int n) => n > 1 ? n.ToString() : "";

        /// <summary>Công thức ion gốc dạng hay viết trong phương trình (SO₄²⁻, CO₃²⁻, CrO₄²⁻...).</summary>
        public static string IonFormula(string id)
        {
            switch (id)
            {
                case "SO4": return "SO₄²⁻";
                case "SO3": return "SO₃²⁻";
                case "CO3": return "CO₃²⁻";
                case "PO4": return "PO₄³⁻";
                case "S": return "S²⁻";
                case "CrVI": return "CrO₄²⁻";
                case "Ox": return "C₂O₄²⁻";
                case "F": return "F⁻";
                case "CN": return "CN⁻";
                case "NO2": return "NO₂⁻";
                case "SiO3": return "SiO₃²⁻";
                case "Ac": return "CH₃COO⁻";
                case "NH3": return "NH₄⁺";
                default: return SpeciesRegistry.Get(id)?.Formula ?? id;
            }
        }

        static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
