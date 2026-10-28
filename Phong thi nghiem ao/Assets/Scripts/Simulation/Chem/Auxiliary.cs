using System;
using System.Collections.Generic;
using System.Linq;

namespace Khkt.Simulation.Chem
{
    /// <summary>Hơi nước bão hòa theo IAPWS-IF97 (miền 4): áp suất theo nhiệt độ và ngược lại.</summary>
    public static class SteamTable
    {
        static readonly double[] n = { 0, 1167.0521452767, -724213.16703206, -17.073846940092, 12020.82470247, -3232555.0322333,
                                       14.91510861353, -4823.2657361591, 405113.40542057, -0.23855557567849, 650.17534844798 };

        /// <summary>Áp suất hơi bão hòa (bar tuyệt đối) ở nhiệt độ tC (°C), 0 – 373 °C.</summary>
        public static double PsatBar(double tC)
        {
            double T = tC + 273.15;
            double th = T + n[9] / (T - n[10]);
            double A = th * th + n[1] * th + n[2], B = n[3] * th * th + n[4] * th + n[5], C = n[6] * th * th + n[7] * th + n[8];
            return Math.Pow(2 * C / (-B + Math.Sqrt(B * B - 4 * A * C)), 4) * 10;  // MPa → bar
        }

        /// <summary>Nhiệt độ sôi (°C) ở áp suất pBar (bar tuyệt đối), giải ngược bằng chia đôi.</summary>
        public static double TsatC(double pBar)
        {
            double lo = 1, hi = 370;
            for (int i = 0; i < 60; i++) { double m = (lo + hi) / 2; if (PsatBar(m) < pBar) lo = m; else hi = m; }
            return (lo + hi) / 2;
        }
    }

    /// <summary>Tiệt trùng nhiệt ẩm: số bào tử giảm theo giá trị D(T) = D₁₂₁·10^((121 − T)/z).</summary>
    public static class Sterilization
    {
        public static double D(double tC, double d121, double z) => d121 * Math.Pow(10, (121 - tC) / z);

        /// <summary>Số bậc giảm (lg N₀/N) sau đoạn thời gian dtMin ở nhiệt độ tC; dưới 100 °C coi như không diệt bào tử.</summary>
        public static double LogReduction(double tC, double dtMin, double d121, double z) => tC < 100 ? 0 : dtMin / D(tC, d121, z);
    }

    /// <summary>
    /// Sấy và nung mẫu (phương pháp khối lượng): nước, chất dễ bay hơi mất khi sấy 105 °C; chất hữu cơ cháy khi nung 550 °C (muối natri của axit
    /// hữu cơ để lại Na₂CO₃, hiđrat mất nước); CaCO₃ phân hủy thành CaO ở nhiệt độ cao (mô phỏng: bắt đầu khoảng 750 °C).
    /// </summary>
    public static class Gravimetry
    {
        /// <summary>Thành phần mẫu ướt: nước (g), chất dễ bay hơi khác (g), chất rắn (theo pha, mg).</summary>
        public sealed class WetSample
        {
            public double WaterG, VolatileG;
            public List<SolidResidue.Phase> Solids = new List<SolidResidue.Phase>();
            public double SolidsG => SolidResidue.TotalMg(Solids) / 1000;
            public double TotalG => WaterG + VolatileG + SolidsG;
        }

        /// <summary>Lấy volumeMl dung dịch: nước ≈ thể tích trừ phần thể tích chất hữu cơ lỏng; chất rắn theo cô cạn.</summary>
        public static WetSample FromSolution(Mix mix, double totalMl, double takeMl)
        {
            var s = new WetSample();
            double f = Math.Min(1, takeMl / Math.Max(1e-6, totalMl));
            double volFrac = 0, volatileG = 0;
            for (int i = 0; i < mix.Count; i++)
            {
                var d = mix.SpeciesAt(i);
                double n = mix.MolAt(i) * f;
                if (d.Id == "C2H5OH" || d.Id == "hc:467" || d.Id == "Ac" || (d.ChemStt > 0 && !d.HasTag("cacbohidrat") && d.MolarMass < 150 && !d.IsSolidPhase))
                {
                    double M = d.MolarMass > 0 ? d.MolarMass : 46;
                    volatileG += n * M;
                    volFrac += n * M / Math.Max(0.5, d.Density > 0 ? d.Density : 0.8) / Math.Max(1e-6, takeMl);
                }
            }
            s.Solids = SolidResidue.From(mix, totalMl, takeMl, true);
            s.VolatileG = volatileG;
            double solidsVolFrac = s.SolidsG / 1.5 / Math.Max(1e-6, takeMl);
            s.WaterG = takeMl * Math.Max(0, 1 - volFrac - solidsVolFrac) * 0.997;
            return s;
        }

        /// <summary>Chất rắn còn lại sau khi nung ở tC trong minutes phút (đã than hóa). black: phần cacbon chưa cháy hết (mg).</summary>
        public static List<SolidResidue.Phase> Ash(IList<SolidResidue.Phase> dry, double tC, double minutes, out double carbonMg)
        {
            var res = new List<SolidResidue.Phase>();
            carbonMg = 0;
            double burn = tC < 400 ? 0 : 1 - Math.Exp(-minutes / (tC >= 540 ? 25.0 : 120.0));   // tỉ lệ chất hữu cơ đã cháy
            double calc = tC < 700 ? 0 : 1 - Math.Exp(-minutes / Math.Max(3, 400 * Math.Exp(-(tC - 750) / 40.0)));  // tỉ lệ CaCO₃ đã phân hủy
            void Add(string id, string formula, string name, double mg)
            {
                if (mg < 0.05) return;  // dưới 0,05 mg: cân không phân biệt được
                var p = res.FirstOrDefault(x => x.Id == id);
                if (p != null) p.Mg += mg;
                else res.Add(new SolidResidue.Phase { Id = id, Formula = formula, Name = name, Mg = mg });
            }
            foreach (var p in dry)
            {
                double M = Formula.MolarMass(p.Formula), mol = p.Mg / M;
                if (p.Organic || p.Id.StartsWith("org:"))
                {
                    double c = Formula.Count(p.Formula).TryGetValue("C", out var nc) ? nc : 0;
                    carbonMg += mol * c * 12.011 * (1 - burn) * 0.3;  // một phần cacbon thành than đen chưa cháy
                    continue;
                }
                if (p.Id == "NaBz(r)") { Add("Na2CO3(r)", "Na2CO3", "natri cacbonat", mol / 2 * 105.99 * burn); Add(p.Id, p.Formula, p.Name, p.Mg * (1 - burn)); continue; }
                if (p.Id == "CaCO3") { Add("CaCO3", "CaCO3", "canxi cacbonat", p.Mg * (1 - calc)); Add("CaO(r)", "CaO", "canxi oxit (vôi sống)", mol * calc * 56.08); continue; }
                if (p.Id == "MgCO3") { Add("MgO(r)", "MgO", "magie oxit", mol * 40.30); continue; }
                if (p.Formula.Contains("·") && tC >= 400)
                {
                    string anhyd = p.Formula.Split('·')[0];
                    Add(p.Id + "~khan", anhyd, p.Name.Replace(" ngậm nước", "") + " (khan)", mol * Formula.MolarMass(anhyd));
                    continue;
                }
                Add(p.Id, p.Formula, p.Name, p.Mg);
            }
            return res;
        }

        /// <summary>Nước còn lại sau khi sấy minutes phút ở tC (nước bay hơi theo hàm mũ; dưới 60 °C rất chậm).</summary>
        public static double WaterLeft(double water0, double tC, double minutes, double tauMin) =>
            water0 * Math.Exp(-minutes / (tauMin * (tC >= 100 ? 1 : tC >= 60 ? 4 : 40)));
    }

    /// <summary>Máy cất nước: độ dẫn của nước cất theo số lần cất, phần đầu bẩn, CO₂ hấp thụ khi để hở.</summary>
    public static class Distiller
    {
        public const double TapUs = 320, Carryover = 0.005, Co2SaturatedUs = 1.0, PureWaterUs = 0.055;

        /// <summary>Độ dẫn (µS/cm, 25 °C) nước ra sau stages lần cất; firstFractionMl: lượng đã bỏ đi đầu tiên; openMin: thời gian để hở.</summary>
        public static double Conductivity(int stages, double discardedMl, double openMin)
        {
            double ions = TapUs;
            for (int i = 0; i < stages; i++) ions *= Carryover;
            double rinse = 6.0 * Math.Exp(-discardedMl / 150.0);             // phần đầu rửa đường ống còn bẩn
            double co2 = Co2SaturatedUs * (0.15 + 0.85 * (1 - Math.Exp(-openMin / 40.0)));  // CO₂ hòa tan dần khi để hở
            return PureWaterUs + ions + rinse + co2;
        }

        public static int Grade(double us) => us <= 0.1 ? 1 : us <= 1.0 ? 2 : us <= 5.0 ? 3 : 0;
    }
}
