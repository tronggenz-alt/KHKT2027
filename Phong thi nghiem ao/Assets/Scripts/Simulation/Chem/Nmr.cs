using System;
using System.Collections.Generic;
using System.Linq;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// ¹H-NMR (data/may/tinh_chat/nmr.json), máy 60 MHz:
    ///  • mỗi tín hiệu tách theo quy tắc n + 1 (cường độ theo tam giác Pascal), khoảng cách J (Hz) = J/60 ppm;
    ///  • nước (H₂O / HDO) và H trao đổi (OH, COOH) gộp thành một tín hiệu ở 4,79 ppm; có thể triệt tín hiệu nước;
    ///  • FID = tổng dao động tắt dần, máy biến đổi Fourier (DFT) ra phổ; sai pha thì vạch méo;
    ///  • chuẩn nội DSS (9 H, 0,00 ppm) để chuẩn ppm và định lượng (qNMR).
    /// </summary>
    public sealed class NmrLibrary
    {
        public sealed class Compound
        {
            public string Id, Name;
            public bool Estimated;
            public readonly List<string> Ids = new List<string>();
            public readonly List<(double ppm, double nH, int neighbors, double j)> Signals = new List<(double, double, int, double)>();
            public double TotalH => Signals.Sum(s => s.nH);
        }

        public struct Line { public double Ppm, Amp; public string Owner; public int Signal; }

        public readonly List<Compound> Compounds = new List<Compound>();
        public double WaterPpm = 4.79;
        public const double Mhz = 60, DssPpm = 0;

        public Compound Get(string speciesId) => Compounds.FirstOrDefault(c => c.Ids.Contains(speciesId));
        public Compound ById(string id) => Compounds.FirstOrDefault(c => c.Id == id);

        public static NmrLibrary Load(string json)
        {
            var lib = new NmrLibrary();
            var r = MiniJson.Obj(MiniJson.Parse(json));
            lib.WaterPpm = MiniJson.N(r, "nuoc_ppm", 4.79);
            foreach (var o in MiniJson.A(r, "chat"))
            {
                var d = MiniJson.Obj(o);
                var c = new Compound { Id = MiniJson.S(d, "id"), Name = MiniJson.S(d, "ten"), Estimated = MiniJson.B(d, "uoc_luong") };
                foreach (var s in MiniJson.A(d, "ids")) c.Ids.Add(s as string);
                foreach (var s in MiniJson.A(d, "tin_hieu")) { var a = MiniJson.Arr(s); c.Signals.Add(((double)a[0], (double)a[1], (int)(double)a[2], (double)a[3])); }
                lib.Compounds.Add(c);
            }
            return lib;
        }

        static double Binom(int n, int k)
        {
            double r = 1;
            for (int i = 1; i <= k; i++) r = r * (n - k + i) / i;
            return r;
        }

        /// <summary>Các vạch của mẫu: (ppm, biên độ ∝ nồng độ H mol/L). waterSuppression: hệ số giữ lại của tín hiệu nước (1 = không triệt).</summary>
        public List<Line> Lines(Mix mix, double volumeMl, double dssMolL, double sampleFraction, double waterSuppression, List<string> unknown = null)
        {
            var res = new List<Line>();
            double V = Math.Max(1e-6, volumeMl / 1000.0);
            double exchH = 0, organicVolFrac = 0;
            if (mix != null)
                for (int i = 0; i < mix.Count; i++)
                {
                    var d = mix.SpeciesAt(i);
                    double c = mix.MolAt(i) / V * sampleFraction;
                    if (c < 1e-6 || d.IsSolidPhase) continue;
                    var cp = Get(d.Id);
                    if (cp == null)
                    {
                        if (d.ChemStt > 0 && c > 1e-3) unknown?.Add(d.Name);
                        continue;
                    }
                    for (int si = 0; si < cp.Signals.Count; si++)
                    {
                        var (ppm, nH, nb, j) = cp.Signals[si];
                        for (int k = 0; k <= nb; k++)
                        {
                            double off = (k - nb / 2.0) * j / Mhz;
                            res.Add(new Line { Ppm = ppm + off, Amp = c * nH * Binom(nb, k) / Math.Pow(2, nb), Owner = cp.Id, Signal = si });
                        }
                    }
                    // H trao đổi (OH, COOH) nhập vào nước
                    if (cp.Id == "etanol" || cp.Id == "metanol") exchH += c;
                    else if (cp.Id == "axit_axetic") exchH += c;
                    else if (cp.Id == "glucozo") exchH += 5 * c;
                    else if (cp.Id == "saccarozo") exchH += 8 * c;
                    else if (cp.Id == "axit_xitric") exchH += 4 * c;
                    if (cp.Id == "etanol") organicVolFrac += c * 46.07 / 789;
                    if (cp.Id == "metanol") organicVolFrac += c * 32.04 / 792;
                }
            double waterH = 2 * 55.5 * sampleFraction * Math.Max(0, 1 - organicVolFrac) + exchH;
            res.Add(new Line { Ppm = WaterPpm, Amp = waterH * waterSuppression, Owner = "nuoc" });
            if (dssMolL > 0) res.Add(new Line { Ppm = DssPpm, Amp = 9 * dssMolL, Owner = "dss" });
            return res;
        }

        /// <summary>
        /// FID (phần thực, ảo) N điểm. Trục tần số quanh tâm centerPpm, bề rộng phổ swHz; widthHz: độ rộng vạch (shim); noise: biên độ nhiễu / √(số lần quét).
        /// </summary>
        public static (double[] re, double[] im) Fid(IList<Line> lines, int n, double swHz, double centerPpm, double widthHz, double noise, Random rng)
        {
            var re = new double[n];
            var im = new double[n];
            double dt = 1.0 / swHz;
            foreach (var l in lines)
            {
                double f = (l.Ppm - centerPpm) * Mhz;
                for (int i = 0; i < n; i++)
                {
                    double t = i * dt, dec = l.Amp * Math.Exp(-Math.PI * widthHz * t);
                    re[i] += dec * Math.Cos(2 * Math.PI * f * t);
                    im[i] += dec * Math.Sin(2 * Math.PI * f * t);
                }
            }
            for (int i = 0; i < n; i++) { re[i] += noise * Gauss(rng); im[i] += noise * Gauss(rng); }
            return (re, im);
        }

        /// <summary>
        /// Biến đổi Fourier rời rạc của FID → phổ (ppm giảm dần từ trái sang phải như phổ NMR). phaseDeg: pha bậc 0 cần sửa.
        /// Trả mảng (ppm, phần hấp thụ sau khi xoay pha).
        /// </summary>
        public static List<(double ppm, double y)> Spectrum(double[] re, double[] im, double swHz, double centerPpm, double phaseDeg)
        {
            int n = re.Length;
            var res = new List<(double, double)>(n);
            double c = Math.Cos(phaseDeg * Math.PI / 180), s = Math.Sin(phaseDeg * Math.PI / 180);
            // bảng cos/sin để DFT nhanh hơn
            var cs = new double[n];
            var sn = new double[n];
            for (int k = 0; k < n; k++) { cs[k] = Math.Cos(2 * Math.PI * k / n); sn[k] = Math.Sin(2 * Math.PI * k / n); }
            for (int m = 0; m < n; m++)
            {
                int kf = m - n / 2;  // tần số từ −SW/2 đến +SW/2
                double sr = re[0] * 0.5, si = im[0] * 0.5;  // điểm đầu FID nhân 0,5 (khử lệch nền)
                int idx = 0;
                for (int t = 1; t < n; t++)
                {
                    idx = (idx + kf + n) % n;  // (kf·t) mod n tăng dần
                    double cr = cs[idx], ci = -sn[idx];
                    sr += re[t] * cr - im[t] * ci;
                    si += re[t] * ci + im[t] * cr;
                }
                double ppm = centerPpm + kf * swHz / n / Mhz;
                res.Add((ppm, sr * c - si * s));
            }
            res.Reverse();  // ppm lớn bên trái
            return res;
        }

        /// <summary>
        /// Tích phân từng tín hiệu (mỗi nhóm vạch cùng một tín hiệu tách); vùng tích phân rộng ra hai bên nhưng không vượt quá
        /// điểm giữa với tín hiệu kề bên. Tín hiệu trùng vị trí (cách &lt; 0,02 ppm) được gộp.
        /// </summary>
        public static List<(double from, double to, double center, double area)> Integrate(IList<(double ppm, double y)> spec, IList<Line> lines, double widthPpm)
        {
            var groups = lines.GroupBy(l => l.Owner + "#" + l.Signal).Select(g => (lo: g.Min(l => l.Ppm), hi: g.Max(l => l.Ppm))).OrderBy(g => g.lo).ToList();
            for (int i = groups.Count - 1; i > 0; i--)
                if (groups[i].lo - groups[i - 1].hi < 0.02) { groups[i - 1] = (groups[i - 1].lo, Math.Max(groups[i - 1].hi, groups[i].hi)); groups.RemoveAt(i); }
            var res = new List<(double, double, double, double)>();
            double step = spec.Count > 1 ? Math.Abs(spec[1].ppm - spec[0].ppm) : 0.01;
            double pad = 0.06 + 8 * widthPpm;
            for (int i = 0; i < groups.Count; i++)
            {
                var (lo, hi) = groups[i];
                double a = lo - pad, b = hi + pad;
                if (i > 0) a = Math.Max(a, (groups[i - 1].hi + lo) / 2);
                if (i < groups.Count - 1) b = Math.Min(b, (hi + groups[i + 1].lo) / 2);
                double area = spec.Where(p => p.ppm >= a && p.ppm <= b).Sum(p => p.y) * step;
                res.Add((a, b, (lo + hi) / 2, area));
            }
            return res.OrderByDescending(g => g.Item3).ToList();
        }

        /// <summary>
        /// Khớp đường vạch (deconvolution): phổ = Σ hệ số × dạng vạch của từng tín hiệu (các vạch tách theo tỉ lệ Pascal, Lorentz độ rộng widthHz),
        /// bình phương tối thiểu không âm. Hệ số tỉ lệ với số H × nồng độ, dùng được cả khi tín hiệu sát nhau / chồng đuôi.
        /// Khóa: "chất#chỉ số tín hiệu".
        /// </summary>
        public static Dictionary<string, double> FitAreas(IList<(double ppm, double y)> spec, IList<Line> lines, double widthHz)
        {
            var groups = lines.GroupBy(l => l.Owner + "#" + l.Signal).ToList();
            var pts = spec.ToList();
            double hw = widthHz / Mhz / 2;
            double[] Basis(int gi)
            {
                var g = groups[gi].ToList();
                double tot = g.Sum(l => l.Amp);
                return pts.Select(p => g.Sum(l => l.Amp / tot * hw * hw / ((p.ppm - l.Ppm) * (p.ppm - l.Ppm) + hw * hw))).ToArray();
            }
            var x = LeastSquares.NonNegative(groups.Count, Basis, pts.Select(p => p.y).ToArray(), out _);
            var res = new Dictionary<string, double>();
            for (int i = 0; i < groups.Count; i++) res[groups[i].Key] = x[i];
            return res;
        }

        static double Gauss(Random r)
        {
            double u1 = 1 - r.NextDouble(), u2 = r.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }
    }
}
