using System;
using System.Collections.Generic;
using System.Linq;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Thư viện phổ ATR-FTIR (data/may/tinh_chat/ftir.json):
    ///  • phổ của mẫu = Σ phổ từng chất theo phần thể tích (chất lỏng) hoặc nồng độ mol/L (chất tan); nước chiếm phần còn lại;
    ///  • axit yếu tách hai dạng (phân tử / ion) theo pH mẫu: C=O 1710 cm⁻¹ của axit đổi thành COO⁻ 1550 cm⁻¹;
    ///  • tách hỗn hợp từ phổ (bình phương tối thiểu không âm) và tra thư viện theo chỉ số khớp HQI.
    /// </summary>
    public sealed class FtirLibrary
    {
        public sealed class Entry
        {
            public string Id, Name;
            public readonly List<string> Ids = new List<string>();
            public bool Liquid, IonLiquid;
            public double M, Density = 1, PKa = double.NaN;
            public readonly List<(double nu, double a, double w)> Bands = new List<(double, double, double)>(), IonBands = new List<(double, double, double)>();
            public bool HasIon => IonBands.Count > 0;

            /// <summary>Độ hấp thụ của chất nguyên chất (lỏng) hoặc dung dịch 1 mol/L, ion = dạng ion.</summary>
            public double A(double nu, bool ion = false)
            {
                double s = 0;
                foreach (var (c, a, w) in ion ? IonBands : Bands) s += a * Shape(nu, c, w);
                return s;
            }

            /// <summary>Đổi nồng độ mol/L ra lượng dùng trong phổ: phần thể tích (lỏng) hoặc giữ mol/L (tan).</summary>
            public double Amount(double molL, bool ion) => (ion ? IonLiquid : Liquid) ? molL * M / (Density * 1000.0) : molL;
        }

        /// <summary>Thành phần phổ: chất, lượng (phần thể tích hoặc mol/L), dạng ion hay phân tử.</summary>
        public struct Part
        {
            public Entry E;
            public double Amount;
            public bool Ion;
            public string Label => E.Name + (E.HasIon ? (Ion ? " (dạng ion)" : " (dạng phân tử)") : "");
            public double A(double nu) => E.A(nu, Ion) * Amount;
        }

        public readonly List<Entry> Entries = new List<Entry>();
        public readonly List<(double lo, double hi, string name)> Groups = new List<(double, double, string)>();
        public string TableSource = "";
        public Entry Water => Get("H2O");

        public Entry Get(string speciesId) => Entries.FirstOrDefault(e => e.Ids.Contains(speciesId));
        public Entry ById(string id) => Entries.FirstOrDefault(e => e.Id == id);

        /// <summary>Dải giả Voigt (nửa Gauss, nửa Lorentz), cao 1 ở tâm, độ rộng nửa chiều cao w.</summary>
        public static double Shape(double nu, double c, double w)
        {
            double x = (nu - c) / (w / 2);
            return 0.5 * Math.Exp(-0.6931 * x * x) + 0.5 / (1 + x * x);
        }

        public static FtirLibrary Load(string json)
        {
            var lib = new FtirLibrary();
            var root = MiniJson.Obj(MiniJson.Parse(json));
            lib.TableSource = MiniJson.S(root, "nguon_bang");
            foreach (var o in MiniJson.A(root, "chat"))
            {
                var d = MiniJson.Obj(o);
                var e = new Entry
                {
                    Id = MiniJson.S(d, "id"), Name = MiniJson.S(d, "ten"), Liquid = MiniJson.S(d, "kieu") == "long",
                    IonLiquid = MiniJson.S(d, "dai_ion_kieu") == "long", M = MiniJson.N(d, "M"), Density = MiniJson.N(d, "d", 1), PKa = MiniJson.N(d, "pka", double.NaN),
                };
                foreach (var s in MiniJson.A(d, "ids")) e.Ids.Add(s as string);
                foreach (var b in MiniJson.A(d, "dai")) { var a = MiniJson.Arr(b); e.Bands.Add(((double)a[0], (double)a[1], (double)a[2])); }
                foreach (var b in MiniJson.A(d, "dai_ion")) { var a = MiniJson.Arr(b); e.IonBands.Add(((double)a[0], (double)a[1], (double)a[2])); }
                lib.Entries.Add(e);
            }
            foreach (var o in MiniJson.A(root, "nhom_chuc"))
            {
                var d = MiniJson.Obj(o);
                lib.Groups.Add((MiniJson.N(d, "tu"), MiniJson.N(d, "den"), MiniJson.S(d, "ten")));
            }
            return lib;
        }

        /// <summary>
        /// Các thành phần của mẫu nước (gồm nước). unknown: chất hữu cơ có lượng đáng kể mà thư viện chưa có (máy báo, không bịa phổ).
        /// </summary>
        public List<Part> Composition(Mix mix, double volumeMl, double ph, List<string> unknown = null)
        {
            var res = new List<Part>();
            double V = Math.Max(1e-6, volumeMl / 1000.0), liquidFrac = 0;
            if (mix != null)
                for (int i = 0; i < mix.Count; i++)
                {
                    var d = mix.SpeciesAt(i);
                    double c = mix.MolAt(i) / V;
                    if (c < 1e-6 || d.IsSolidPhase) continue;
                    var e = Get(d.Id);
                    if (e == null)
                    {
                        if (d.ChemStt > 0 && c > 1e-3) unknown?.Add(string.IsNullOrEmpty(d.Name) ? d.Id : d.Name);
                        continue;
                    }
                    if (e.HasIon && !double.IsNaN(e.PKa))
                    {
                        double fIon = 1.0 / (1.0 + Math.Pow(10, e.PKa - ph));
                        AddPart(res, e, e.Amount(c * (1 - fIon), false), false, ref liquidFrac);
                        AddPart(res, e, e.Amount(c * fIon, true), true, ref liquidFrac);
                    }
                    else AddPart(res, e, e.Amount(c, false), false, ref liquidFrac);
                }
            var w = Water;
            if (w != null) res.Insert(0, new Part { E = w, Amount = Math.Max(0, 1 - liquidFrac), Ion = false });
            return res;
        }

        static void AddPart(List<Part> res, Entry e, double amount, bool ion, ref double liquidFrac)
        {
            if (amount <= 1e-7) return;
            if (ion ? e.IonLiquid : e.Liquid) liquidFrac += amount;
            int k = res.FindIndex(p => p.E == e && p.Ion == ion);
            if (k >= 0) { var p = res[k]; p.Amount += amount; res[k] = p; }
            else res.Add(new Part { E = e, Amount = amount, Ion = ion });
        }

        public static double Absorbance(IList<Part> parts, double nu, bool withWater = true)
        {
            double a = 0;
            foreach (var p in parts) if (withWater || p.E.Id != "nuoc") a += p.A(nu);
            return a;
        }

        /// <summary>Hấp thụ của khí quyển trong buồng máy: CO₂ (2350, 667 cm⁻¹) và hơi nước (răng cưa 1400 – 1900, 3500 – 3900 cm⁻¹).</summary>
        public static double Atmosphere(double nu, double co2, double h2o)
        {
            double a = co2 * (0.9 * Shape(nu, 2360, 18) + 0.75 * Shape(nu, 2338, 16) + 0.25 * Shape(nu, 667, 10));
            if ((nu > 1350 && nu < 1950) || (nu > 3450 && nu < 3950))
            {
                double env = nu < 2000 ? Math.Exp(-Math.Pow((nu - 1600) / 160, 2)) : Math.Exp(-Math.Pow((nu - 3750) / 120, 2));
                a += h2o * env * Math.Pow(Math.Abs(Math.Sin(nu * 0.37) * Math.Sin(nu * 0.113)), 6);
            }
            return a;
        }

        /// <summary>Nhóm chức theo bảng tần số cho một dải ở số sóng nu.</summary>
        public string Assign(double nu)
        {
            var hit = Groups.Where(g => nu >= g.lo - 10 && nu <= g.hi + 10).OrderBy(g => g.hi - g.lo).FirstOrDefault();
            return hit.name ?? "vùng vân tay";
        }

        /// <summary>Đỉnh của phổ (nu giảm dần cũng được): cực đại cục bộ cao hơn ngưỡng, cách nhau ít nhất minGap cm⁻¹.</summary>
        public static List<(double nu, double a)> Peaks(IList<(double nu, double a)> spec, double threshold, double minGap = 25)
        {
            var res = new List<(double nu, double a)>();
            for (int i = 2; i < spec.Count - 2; i++)
            {
                var p = spec[i];
                if (p.a < threshold) continue;
                bool max = true;
                for (int k = -2; k <= 2 && max; k++) if (k != 0 && spec[i + k].a > p.a) max = false;
                if (!max) continue;
                int near = res.FindIndex(q => Math.Abs(q.nu - p.nu) < minGap);
                if (near < 0) res.Add(p);
                else if (res[near].a < p.a) res[near] = p;
            }
            return res.OrderByDescending(p => p.a).ToList();
        }

        /// <summary>
        /// Tách hỗn hợp: mọi dạng (phân tử / ion) của mọi chất trong thư viện, trừ nước nếu skipWater, khớp phổ bằng bình phương tối thiểu không âm.
        /// Trả (thành phần, lượng) có lượng đáng kể; rms: sai lệch còn lại.
        /// </summary>
        public List<Part> Unmix(IList<(double nu, double a)> spec, bool skipWater, out double rms)
        {
            var cands = new List<Part>();
            foreach (var e in Entries)
            {
                if (skipWater && e.Id == "nuoc") continue;
                cands.Add(new Part { E = e, Amount = 1, Ion = false });
                if (e.HasIon) cands.Add(new Part { E = e, Amount = 1, Ion = true });
            }
            var x = LeastSquares.NonNegative(cands.Count, i => spec.Select(s => cands[i].E.A(s.nu, cands[i].Ion)).ToArray(), spec.Select(s => s.a).ToArray(), out rms);
            var res = new List<Part>();
            for (int i = 0; i < cands.Count; i++)
            {
                var p = new Part { E = cands[i].E, Amount = x[i], Ion = cands[i].Ion };
                if (PeakA(p) > 0.004) res.Add(p);  // bỏ thành phần góp dưới 0,004 A (nhiễu)
            }
            return res.OrderByDescending(PeakA).ToList();
        }

        /// <summary>Độ hấp thụ ở dải mạnh nhất của một thành phần.</summary>
        public static double PeakA(Part p)
        {
            var bands = p.Ion ? p.E.IonBands : p.E.Bands;
            return bands.Count == 0 ? 0 : bands.Max(b => b.a) * p.Amount;
        }

        /// <summary>Chỉ số khớp phổ HQI (0 – 100) giữa phổ đo và phổ chuẩn của từng chất (tương quan bình phương trên vùng đã chọn).</summary>
        public List<(Part p, double hqi)> Search(IList<(double nu, double a)> spec)
        {
            var res = new List<(Part, double)>();
            foreach (var e in Entries)
                foreach (bool ion in e.HasIon ? new[] { false, true } : new[] { false })
                {
                    double ab = 0, aa = 0, bb = 0;
                    foreach (var (nu, a) in spec)
                    {
                        double r = e.A(nu, ion);
                        ab += a * r; aa += a * a; bb += r * r;
                    }
                    double hqi = aa > 0 && bb > 0 ? 100 * ab * ab / (aa * bb) : 0;
                    res.Add((new Part { E = e, Amount = 1, Ion = ion }, hqi));
                }
            return res.OrderByDescending(r => r.Item2).ToList();
        }
    }

    /// <summary>Bình phương tối thiểu không âm (bỏ dần thành phần âm), dùng chung cho các phép tách phổ.</summary>
    public static class LeastSquares
    {
        /// <param name="k">số thành phần</param>
        /// <param name="column">phổ chuẩn của thành phần i tại mọi điểm đo</param>
        /// <param name="y">phổ đo</param>
        public static double[] NonNegative(int k, Func<int, double[]> column, double[] y, out double rms)
        {
            var cols = new double[k][];
            for (int i = 0; i < k; i++) cols[i] = column(i);
            var c = new double[k];
            var active = Enumerable.Range(0, k).ToList();
            for (int guard = 0; guard <= k && active.Count > 0; guard++)
            {
                int m = active.Count;
                var ata = new double[m, m];
                var atb = new double[m];
                for (int i = 0; i < m; i++)
                {
                    var ci = cols[active[i]];
                    for (int p = 0; p < y.Length; p++) atb[i] += ci[p] * y[p];
                    for (int j = i; j < m; j++)
                    {
                        var cj = cols[active[j]];
                        double s = 0;
                        for (int p = 0; p < y.Length; p++) s += ci[p] * cj[p];
                        ata[i, j] = ata[j, i] = s;
                    }
                }
                var x = UvVisLibrary.Solve(ata, atb);
                int worst = -1;
                double wv = 0;
                for (int i = 0; i < m; i++) if (x[i] < wv) { wv = x[i]; worst = i; }
                Array.Clear(c, 0, k);
                if (worst < 0) { for (int i = 0; i < m; i++) c[active[i]] = x[i]; break; }
                active.RemoveAt(worst);
            }
            double ss = 0;
            for (int p = 0; p < y.Length; p++)
            {
                double fit = 0;
                for (int i = 0; i < k; i++) fit += cols[i][p] * c[i];
                ss += (y[p] - fit) * (y[p] - fit);
            }
            rms = y.Length > 0 ? Math.Sqrt(ss / y.Length) : 0;
            return c;
        }
    }
}
