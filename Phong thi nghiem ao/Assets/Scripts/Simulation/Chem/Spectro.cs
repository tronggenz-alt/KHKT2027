using System;
using System.Collections.Generic;
using System.Linq;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Mô hình máy quang phổ UV-Vis (dữ liệu data/may/tinh_chat/uv_vis.json, mọi λmax và ε có nguồn):
    ///  • phổ của dung dịch A(λ) = Σ εᵢ(λ)·b·cᵢ theo các chất có trong thư viện; chất có màu ngoài thư viện được liệt kê, không bịa số;
    ///  • đường chuẩn tuyến tính (bình phương tối thiểu, R²);
    ///  • tách hỗn hợp nhiều chất hấp thụ: phổ đo được ≈ tổ hợp phổ chuẩn của từng chất, giải bình phương tối thiểu với cᵢ ≥ 0.
    /// </summary>
    public sealed class UvVisLibrary
    {
        public sealed class Entry
        {
            public string Id, Name, PerMol, Shape, Source, Note;
            public readonly List<(double l, double eps, double sigma)> Bands = new List<(double, double, double)>();
            public readonly List<(double l, double eps)> Points = new List<(double, double)>();
            public double TailLeft = 10, TailRight = 40, PhMax = double.NaN, PKa = double.NaN;

            /// <summary>Hệ số hấp thụ mol (L·mol⁻¹·cm⁻¹) ở bước sóng l (nm).</summary>
            public double Eps(double l)
            {
                double e = 0;
                foreach (var b in Bands) e += b.eps * Math.Exp(-(l - b.l) * (l - b.l) / (2 * b.sigma * b.sigma));
                if (Points.Count > 0)
                {
                    var p0 = Points[0];
                    var pn = Points[Points.Count - 1];
                    if (l <= p0.l) e += p0.eps * Math.Exp(-(l - p0.l) * (l - p0.l) / (2 * TailLeft * TailLeft));
                    else if (l >= pn.l) e += pn.eps * Math.Exp(-(l - pn.l) * (l - pn.l) / (2 * TailRight * TailRight));
                    else
                        for (int i = 1; i < Points.Count; i++)
                            if (l <= Points[i].l)
                            {
                                var a = Points[i - 1];
                                var b = Points[i];
                                e += a.eps + (b.eps - a.eps) * (l - a.l) / (b.l - a.l);
                                break;
                            }
                }
                return e;
            }

            /// <summary>Bước sóng hấp thụ cực đại trong khoảng [lo, hi] (nm).</summary>
            public double LambdaMax(double lo = 300, double hi = 900)
            {
                double best = lo, be = -1;
                for (double l = lo; l <= hi; l += 1) { double e = Eps(l); if (e > be) { be = e; best = l; } }
                return best;
            }
        }

        public readonly List<Entry> Entries = new List<Entry>();
        public readonly List<(string code, string name, double cut)> Cuvettes = new List<(string, string, double)>();
        public string CuvetteSource = "";

        public Entry Get(string id) => Entries.FirstOrDefault(e => e.Id == id);

        public static UvVisLibrary Load(string json)
        {
            var lib = new UvVisLibrary();
            var root = MiniJson.Obj(MiniJson.Parse(json));
            foreach (var o in MiniJson.A(root, "chat"))
            {
                var d = MiniJson.Obj(o);
                var e = new Entry
                {
                    Id = MiniJson.S(d, "id"), Name = MiniJson.S(d, "ten"), PerMol = MiniJson.S(d, "theo_mol"), Shape = MiniJson.S(d, "dang_dai"),
                    Source = MiniJson.S(d, "nguon"), Note = MiniJson.S(d, "ghi_chu"),
                    PhMax = MiniJson.N(d, "dieu_kien_ph_max", double.NaN), PKa = MiniJson.N(d, "pKa", double.NaN),
                };
                foreach (var b in MiniJson.A(d, "dai"))
                {
                    var bd = MiniJson.Obj(b);
                    e.Bands.Add((MiniJson.N(bd, "l"), MiniJson.N(bd, "eps"), MiniJson.N(bd, "sigma", 30)));
                }
                foreach (var p in MiniJson.A(d, "diem"))
                {
                    var pl = MiniJson.Arr(p);
                    e.Points.Add(((double)pl[0], (double)pl[1]));
                }
                var t = MiniJson.Obj(d.TryGetValue("duoi", out var tv) ? tv : null);
                if (t != null) { e.TailLeft = MiniJson.N(t, "trai", 10); e.TailRight = MiniJson.N(t, "phai", 40); }
                lib.Entries.Add(e);
            }
            foreach (var o in MiniJson.A(root, "cuvet"))
            {
                var d = MiniJson.Obj(o);
                lib.Cuvettes.Add((MiniJson.S(d, "ma"), MiniJson.S(d, "ten"), MiniJson.N(d, "cat_nm", 190)));
            }
            lib.CuvetteSource = MiniJson.S(root, "nguon_cuvet");
            return lib;
        }

        /// <summary>Phenolphtalein: 1 giọt = 0,05 mL dung dịch 1% (10 g/L), M = 318,33 g/mol.</summary>
        public const double PhenolphthaleinMolPerDrop = 0.05e-3 * 10.0 / 318.33;

        /// <summary>
        /// Các chất hấp thụ trong dung dịch: (mục thư viện, nồng độ mol/L). unknown: chất có màu không có trong thư viện
        /// (hoặc ngoài điều kiện của số liệu) để máy báo "ngoài thư viện".
        /// </summary>
        public List<(Entry e, double c)> Absorbers(Mix mix, double volumeMl, double ph, double ppDrops, List<string> unknown)
        {
            var res = new List<(Entry, double)>();
            double V = Math.Max(1e-6, volumeMl / 1000.0);
            if (mix != null)
                for (int i = 0; i < mix.Count; i++)
                {
                    var d = mix.SpeciesAt(i);
                    double c = mix.MolAt(i) / V;
                    if (c <= 1e-9) continue;
                    var e = Get(d.Id);
                    if (e != null)
                    {
                        if (!double.IsNaN(e.PhMax) && ph > e.PhMax)
                        {
                            unknown?.Add($"{e.Name}: ở pH {ph:0.0} chất chuyển dạng khác, chưa có số liệu");
                            continue;
                        }
                        res.Add((e, c));
                    }
                    else if (d.ColorStrength > 0 && !d.IsSolidPhase && (d.Color.r < 0.92f || d.Color.g < 0.92f || d.Color.b < 0.92f))
                        unknown?.Add(string.IsNullOrEmpty(d.Name) ? d.Id : d.Name);
                }
            var pp = Get("pp");
            if (pp != null && ppDrops > 0)
            {
                double frac = 1.0 / (1.0 + Math.Pow(10, (double.IsNaN(pp.PKa) ? 9.3 : pp.PKa) - ph));
                double c = ppDrops * PhenolphthaleinMolPerDrop * frac / V;
                if (c > 1e-9) res.Add((pp, c));
            }
            return res;
        }

        public static double Absorbance(IList<(Entry e, double c)> abs, double l, double pathCm = 1)
        {
            double a = 0;
            foreach (var (e, c) in abs) a += e.Eps(l) * c * pathCm;
            return a;
        }

        /// <summary>Độ hấp thụ của chính cuvet (so với thạch anh): tăng vọt dưới bước sóng cắt.</summary>
        public double CuvetteAbsorbance(string code, double l)
        {
            foreach (var cv in Cuvettes)
                if (cv.code == code) return 4.0 / (1.0 + Math.Exp((l - cv.cut + 10) / 6.0));
            return 0;
        }

        // =====================================================================
        // Đường chuẩn, tách hỗn hợp
        // =====================================================================

        public struct Fit
        {
            public double Slope, Intercept, R2;
            public int N;
            public double X(double y) => Slope != 0 ? (y - Intercept) / Slope : double.NaN;
        }

        public static Fit Linear(IList<(double x, double y)> pts)
        {
            var f = new Fit { N = pts.Count };
            if (pts.Count < 2) return f;
            double mx = pts.Average(p => p.x), my = pts.Average(p => p.y), sxx = 0, sxy = 0, syy = 0;
            foreach (var p in pts) { sxx += (p.x - mx) * (p.x - mx); sxy += (p.x - mx) * (p.y - my); syy += (p.y - my) * (p.y - my); }
            if (sxx <= 0) return f;
            f.Slope = sxy / sxx;
            f.Intercept = my - f.Slope * mx;
            f.R2 = syy > 0 ? sxy * sxy / (sxx * syy) : 1;
            return f;
        }

        /// <summary>
        /// Tách hỗn hợp: tìm cᵢ ≥ 0 sao cho Σ εᵢ(λ)·b·cᵢ khớp nhất phổ đo (bình phương tối thiểu, bỏ dần thành phần âm).
        /// residualRms: sai lệch còn lại (đơn vị độ hấp thụ); lớn nghĩa là còn chất ngoài thư viện.
        /// </summary>
        public static double[] Unmix(IList<Entry> comps, IList<(double l, double a)> spectrum, double pathCm, out double residualRms)
        {
            int k = comps.Count;
            var c = new double[k];
            var active = Enumerable.Range(0, k).ToList();
            for (int guard = 0; guard <= k && active.Count > 0; guard++)
            {
                int m = active.Count;
                var ata = new double[m, m];
                var atb = new double[m];
                foreach (var (l, a) in spectrum)
                    for (int i = 0; i < m; i++)
                    {
                        double ei = comps[active[i]].Eps(l) * pathCm;
                        atb[i] += ei * a;
                        for (int j = 0; j < m; j++) ata[i, j] += ei * comps[active[j]].Eps(l) * pathCm;
                    }
                var x = Solve(ata, atb);
                int worst = -1;
                double wv = 0;
                for (int i = 0; i < m; i++) if (x[i] < wv) { wv = x[i]; worst = i; }
                if (worst < 0)
                {
                    Array.Clear(c, 0, k);
                    for (int i = 0; i < m; i++) c[active[i]] = x[i];
                    break;
                }
                active.RemoveAt(worst);
                Array.Clear(c, 0, k);
            }
            double ss = 0;
            foreach (var (l, a) in spectrum)
            {
                double fit = 0;
                for (int i = 0; i < k; i++) fit += comps[i].Eps(l) * pathCm * c[i];
                ss += (a - fit) * (a - fit);
            }
            residualRms = spectrum.Count > 0 ? Math.Sqrt(ss / spectrum.Count) : 0;
            return c;
        }

        /// <summary>Giải hệ tuyến tính nhỏ (khử Gauss, chọn trụ). Ma trận suy biến: trả 0.</summary>
        internal static double[] Solve(double[,] a, double[] b)
        {
            int n = b.Length;
            var m = (double[,])a.Clone();
            var x = (double[])b.Clone();
            for (int col = 0; col < n; col++)
            {
                int piv = col;
                for (int r = col + 1; r < n; r++) if (Math.Abs(m[r, col]) > Math.Abs(m[piv, col])) piv = r;
                if (Math.Abs(m[piv, col]) < 1e-30) return new double[n];
                if (piv != col)
                {
                    for (int j = 0; j < n; j++) { var t = m[col, j]; m[col, j] = m[piv, j]; m[piv, j] = t; }
                    var tb = x[col]; x[col] = x[piv]; x[piv] = tb;
                }
                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    double f = m[r, col] / m[col, col];
                    if (f == 0) continue;
                    for (int j = col; j < n; j++) m[r, j] -= f * m[col, j];
                    x[r] -= f * x[col];
                }
            }
            for (int i = 0; i < n; i++) x[i] /= m[i, i];
            return x;
        }
    }
}
