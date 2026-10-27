using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Độ bay hơi của dung dịch (chưng cất, đun hồi lưu, cô cạn) theo định luật Raoult giản lược:
    /// áp suất hơi bão hòa từng chất theo Clausius–Clapeyron từ nhiệt độ sôi và nhiệt hóa hơi
    /// (data/chem/nhiet_do_soi.json), nhiệt độ sôi của hỗn hợp là nhiệt độ mà Σ xᵢ·p*ᵢ(T) = 1 atm, hơi thoát ra
    /// có thành phần yᵢ = xᵢ·p*ᵢ(T): chất sôi thấp (etanol) giàu trong hơi hơn trong lỏng nên ra trước.
    /// Nước không lưu trong Mix: là phần thể tích còn lại sau khi trừ thể tích các chất hữu cơ (1 ml nước ≈ 1 g).
    /// Chất hữu cơ tan trong nước có hệ số hoạt độ Margules hai tham số theo nhóm (ancol lệch dương mạnh: rượu 20° sôi
    /// ~89 °C, giọt cất đầu ~65% etanol như thực tế); chất không tan là pha riêng, góp đủ áp suất hơi (chưng cất lôi cuốn
    /// hơi nước). Mức giáo dục: bỏ qua đẳng phí chính xác, tương tác giữa các chất hữu cơ, độ tăng điểm sôi do chất tan.
    /// </summary>
    public static class Volatility
    {
        public const double WaterTb = 100, WaterDhJ = 40700, WaterM = 18.015, R = 8.314;

        static readonly Dictionary<int, (double tb, double dh)> byStt = new Dictionary<int, (double, double)>();

        public static bool Loaded => byStt.Count > 0;

        /// <summary>Nạp bảng nhiệt độ sôi (nội dung nhiet_do_soi.json).</summary>
        public static void Load(string json)
        {
            byStt.Clear();
            if (string.IsNullOrEmpty(json)) return;
            var ci = CultureInfo.InvariantCulture;
            foreach (Match m in Regex.Matches(json, @"\{[^{}]*""stt""\s*:\s*(\d+)[^{}]*""ts""\s*:\s*(-?[\d.]+)([^{}]*)\}"))
            {
                int stt = int.Parse(m.Groups[1].Value, ci);
                double tb = double.Parse(m.Groups[2].Value, ci);
                double dh = 0;
                var md = Regex.Match(m.Groups[3].Value, @"""dh""\s*:\s*([\d.]+)");
                if (md.Success) dh = double.Parse(md.Groups[1].Value, ci) * 1000;
                if (dh <= 0) dh = 88 * (tb + 273.15);  // quy tắc Trouton
                byStt[stt] = (tb, dh);
            }
        }

        /// <summary>Nhiệt độ sôi (°C) và nhiệt hóa hơi (J/mol) của một loài hữu cơ (theo số thứ tự danh mục).</summary>
        public static bool TryGet(SpeciesDef d, out double tbC, out double dhJ)
        {
            tbC = dhJ = 0;
            if (d == null) return false;
            if (d.ChemStt > 0 && byStt.TryGetValue(d.ChemStt, out var v)) { tbC = v.tb; dhJ = v.dh; return true; }
            if (d.Id == "C2H5OH") { tbC = 78.4; dhJ = 38600; return true; }
            return false;
        }

        /// <summary>Áp suất hơi bão hòa (atm) ở t °C.</summary>
        public static double Psat(double tbC, double dhJ, double tC) => Math.Exp(dhJ / R * (1 / (tbC + 273.15) - 1 / (tC + 273.15)));

        public struct Component
        {
            public int index;          // chỉ số loài trong SpeciesRegistry
            public double mol, tb, dh, mlPerMol;
            public bool insoluble;     // không tan trong nước: pha riêng
            public double a, b;        // Margules với nước: ln γ∞ của chất hữu cơ (a) và của nước (b)
        }

        /// <summary>Phần lỏng bay hơi được: số mol nước và các chất hữu cơ có nhiệt độ sôi.</summary>
        public struct Liquid
        {
            public double waterMol;
            public List<Component> org;
            public double Total
            {
                get { double s = waterMol; if (org != null) foreach (var o in org) s += o.mol; return s; }
            }
        }

        static double MlPerMol(SpeciesDef d) => d.Id == "C2H5OH" ? 46.07 / 0.789 : d.MolarMass / Math.Max(0.3, d.Density);

        /// <summary>Tách thành phần bay hơi của dung dịch (Mix + thể tích ml).</summary>
        public static Liquid Analyze(Mix mix, double volumeMl)
        {
            var L = new Liquid { org = new List<Component>() };
            double orgMl = 0;
            if (mix != null)
                for (int i = 0; i < mix.Count; i++)
                {
                    var d = mix.SpeciesAt(i);
                    if (d.Kind != SpeciesKind.Molecule && d.Kind != SpeciesKind.Weak) continue;
                    if (d.ChemStt <= 0 && d.Id != "C2H5OH") continue;  // hệ axit vô cơ, ion: không tính thể tích, không bay hơi
                    double n = mix.MolAt(i), mpm = MlPerMol(d);
                    orgMl += n * mpm;
                    if (TryGet(d, out var tb, out var dh))
                    {
                        var (a, bb) = Margules(d);
                        L.org.Add(new Component { index = mix.IndexAt(i), mol = n, tb = tb, dh = dh, mlPerMol = mpm, insoluble = d.Insoluble, a = a, b = bb });
                    }
                }
            L.waterMol = Math.Max(0, volumeMl - orgMl) / WaterM;
            return L;
        }

        /// <summary>Tham số Margules (ln γ∞ chất hữu cơ, ln γ∞ nước) theo nhóm chất.</summary>
        static (double a, double b) Margules(SpeciesDef d)
        {
            if (d.HasTag("ancol") || d.Id == "C2H5OH") return (1.6, 0.8);
            if (d.Kind == SpeciesKind.Weak) return (0.2, 0.2);  // axit cacboxylic: gần lý tưởng
            return (1.0, 0.5);
        }

        /// <summary>Áp suất riêng phần (atm) của nước và từng chất hữu cơ ở t °C.</summary>
        static double Partials(Liquid L, double t, double[] org, out double water)
        {
            // pha nước: nước + chất hữu cơ tan; chất không tan là pha riêng (hoạt độ 1 khi có mặt)
            double nMis = L.waterMol;
            foreach (var o in L.org) if (!o.insoluble) nMis += o.mol;
            double xw = nMis > 0 ? L.waterMol / nMis : 0, lnGw = 0, sum = 0;
            for (int i = 0; i < L.org.Count; i++)
            {
                var o = L.org[i];
                double p = Psat(o.tb, o.dh, t);
                if (o.insoluble) { org[i] = o.mol > 1e-9 ? p : 0; sum += org[i]; continue; }
                double x = nMis > 0 ? o.mol / nMis : 0;
                double pair = x + xw, xi = pair > 0 ? x / pair : 0, xwp = pair > 0 ? xw / pair : 0;
                double lnG = xwp * xwp * (o.a + 2 * (o.b - o.a) * xi);
                lnGw += xi * xi * (o.b + 2 * (o.a - o.b) * xwp) * (pair > 0 ? x / Math.Max(1e-12, 1 - xw) : 0);
                org[i] = x * Math.Exp(lnG) * p;
                sum += org[i];
            }
            water = xw * Math.Exp(lnGw) * Psat(WaterTb, WaterDhJ, t);
            return sum + water;
        }

        /// <summary>Nhiệt độ sôi của hỗn hợp (°C). Không có chất bay hơi: +∞.</summary>
        public static double BubblePoint(Liquid L)
        {
            double total = L.Total;
            if (total <= 1e-9) return double.PositiveInfinity;
            var buf = new double[L.org.Count];
            double F(double t) => Partials(L, t, buf, out _) - 1;
            double lo = -60, hi = 420;
            if (F(hi) < 0) return hi;
            if (F(lo) > 0) return lo;
            for (int i = 0; i < 60; i++)
            {
                double mid = (lo + hi) / 2;
                if (F(mid) > 0) hi = mid; else lo = mid;
            }
            return (lo + hi) / 2;
        }

        /// <summary>Hơi thoát ra khi cấp thêm heatJ ở điểm sôi: số mol nước, từng chất hữu cơ, thể tích lỏng tương ứng.</summary>
        public struct Vapor
        {
            public double tC, waterMol, liquidMl;
            public List<(int index, double mol)> org;
            public double TotalMol { get { double s = waterMol; if (org != null) foreach (var o in org) s += o.mol; return s; } }
        }

        public static Vapor Vaporize(Liquid L, double heatJ)
        {
            var v = new Vapor { org = new List<(int, double)>() };
            double total = L.Total;
            if (total <= 1e-9 || heatJ <= 0) return v;
            double t = BubblePoint(L);
            v.tC = t;
            var ys = new double[L.org.Count];
            double sum = Partials(L, t, ys, out double yw);
            if (sum <= 0) return v;
            yw /= sum;
            double dhMix = yw * WaterDhJ;
            for (int i = 0; i < ys.Length; i++) { ys[i] /= sum; dhMix += ys[i] * L.org[i].dh; }
            double n = heatJ / Math.Max(1000, dhMix);
            v.waterMol = Math.Min(L.waterMol, yw * n);
            v.liquidMl = v.waterMol * WaterM;
            for (int i = 0; i < ys.Length; i++)
            {
                double ni = Math.Min(L.org[i].mol, ys[i] * n);
                if (ni <= 0) continue;
                v.org.Add((L.org[i].index, ni));
                v.liquidMl += ni * L.org[i].mlPerMol;
            }
            return v;
        }

        /// <summary>Hơi (hoặc chất ngưng tụ) dưới dạng Mix các chất hữu cơ (nước nằm trong thể tích).</summary>
        public static Mix OrganicMix(Vapor v)
        {
            var b = new MixBuilder();
            foreach (var (idx, mol) in v.org) b.Add(idx, mol);
            return b.ToMix();
        }
    }
}
