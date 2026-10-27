using System;
using System.Collections.Generic;
using System.Globalization;

namespace Khkt.Simulation.Chem
{
    /// <summary>Hình ảnh của chất trong bình: màu dung dịch, độ đục, lớp cặn dưới đáy, lớp hữu cơ, gương bạc.</summary>
    public struct ChemLook
    {
        public Rgb Liquid;
        public float Alpha;
        public float Turbidity;
        public Rgb Deposit;
        public float DepositMl;
        public Rgb Layer;
        public float LayerMl;
        public bool LayerOnTop;
        public bool Mirror;
    }

    /// <summary>
    /// Tính màu từ thành phần: mỗi loài có màu hấp thụ theo nồng độ (định luật Beer, ba kênh R G B),
    /// chất chỉ thị đổi màu theo pH, kết tủa làm đục. Cùng một hàm cho mọi bình nên màu luôn khớp hóa học.
    /// </summary>
    public static class ChemColor
    {
        static readonly Rgb Water = new Rgb(0.86f, 0.93f, 1f);

        public static ChemLook Compute(Mix mix, double volumeMl, double ph, double tempC, float settled = 0)
        {
            var look = new ChemLook { Liquid = Water, Alpha = 0.28f, Layer = new Rgb(0.95f, 0.92f, 0.8f), LayerOnTop = true };
            if (mix == null) return look;
            double V = Math.Max(volumeMl, 1e-3) / 1000.0;
            double ar = 0, ag = 0, ab = 0;
            double pptMol = 0, pr = 0, pg = 0, pb = 0;
            double depMl = 0, dr = 0, dg = 0, db = 0;
            double layerMl = 0;
            bool heavyLayer = false;
            bool hasStarch = false;
            double i2 = 0;
            for (int i = 0; i < mix.Count; i++)
            {
                var d = mix.SpeciesAt(i);
                double n = mix.MolAt(i);
                double c = n / V;
                switch (d.Kind)
                {
                    case SpeciesKind.Indicator:
                        if (d.Id == "starch") { hasStarch = true; break; }
                        Absorb(IndicatorColor(d.Id, ph), c * d.ColorStrength, ref ar, ref ag, ref ab);
                        break;
                    case SpeciesKind.Precipitate:
                    {
                        double mass = n * Math.Max(50, d.MolarMass > 0 ? d.MolarMass : 150);
                        double settledPart = settled;
                        double susp = n * (1 - settledPart);
                        pptMol += susp;
                        pr += d.Color.r * susp; pg += d.Color.g * susp; pb += d.Color.b * susp;
                        double ml = mass / 1.2 * settledPart * 0.4;
                        depMl += ml; dr += d.Color.r * ml; dg += d.Color.g * ml; db += d.Color.b * ml;
                        break;
                    }
                    case SpeciesKind.Solid:
                    case SpeciesKind.Lump:
                    {
                        if (d.Id == "AgMirror") { look.Mirror = n > 2e-6; break; }
                        double ml = n * Math.Max(20, d.MolarMass) / Math.Max(1, d.Density > 1.01 ? d.Density : 2.5);
                        depMl += ml; dr += d.Color.r * ml; dg += d.Color.g * ml; db += d.Color.b * ml;
                        break;
                    }
                    default:
                        if (d.Insoluble && d.Kind == SpeciesKind.Molecule)
                        {
                            layerMl += n * d.MolarMass / Math.Max(0.5, d.Density);
                            heavyLayer |= d.HasTag("nang");
                            break;
                        }
                        if (d.Id == "I2") { i2 += c; }
                        if (d.ColorStrength <= 0) break;
                        Rgb col = d.Color;
                        if (d.Id == "CrVI")
                        {
                            double h = Math.Pow(10, -ph);
                            float yellow = (float)ChemEngine.AlphaLast(d, h);
                            col = Rgb.Lerp(Rgb.Hex("#E8731A"), Rgb.Hex("#F2C400"), yellow);
                        }
                        if (d.Id == "I2" && layerMl > 0) break;  // iot chuyển vào lớp hữu cơ
                        Absorb(col, c * d.ColorStrength, ref ar, ref ag, ref ab);
                        break;
                }
            }
            if (hasStarch && i2 > 2e-6 && tempC < 65)
                Absorb(Rgb.Hex("#14246A"), i2 * 4000, ref ar, ref ag, ref ab);  // xanh tím hồ tinh bột - iot

            const double path = 0.45;
            float tr = (float)Math.Exp(-ar * path), tg = (float)Math.Exp(-ag * path), tb = (float)Math.Exp(-ab * path);
            look.Liquid = new Rgb(Water.r * tr, Water.g * tg, Water.b * tb);
            look.Alpha = Clamp(0.26f + 0.62f * (1 - (tr + tg + tb) / 3f), 0.2f, 0.92f);

            if (pptMol > 0)
            {
                double cp = pptMol / V;
                float t = (float)(1 - Math.Exp(-cp * 450));
                var pc = new Rgb((float)(pr / pptMol), (float)(pg / pptMol), (float)(pb / pptMol));
                look.Turbidity = t;
                look.Liquid = Rgb.Lerp(look.Liquid, pc, t * 0.9f);
                look.Alpha = Math.Max(look.Alpha, look.Alpha + (0.96f - look.Alpha) * t);
            }
            if (depMl > 0)
            {
                look.DepositMl = (float)depMl;
                look.Deposit = new Rgb((float)(dr / depMl), (float)(dg / depMl), (float)(db / depMl));
            }
            if (layerMl > 0)
            {
                look.LayerMl = (float)layerMl;
                look.LayerOnTop = !heavyLayer;
                if (i2 > 1e-6) look.Layer = Rgb.Hex("#8A2A9A");
            }
            return look;
        }

        static void Absorb(Rgb col, double strength, ref double ar, ref double ag, ref double ab)
        {
            ar += strength * (1 - col.r);
            ag += strength * (1 - col.g);
            ab += strength * (1 - col.b);
        }

        static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;

        static Rgb Ramp(double ph, params (double ph, string hex)[] stops)
        {
            if (ph <= stops[0].ph) return Rgb.Hex(stops[0].hex);
            for (int i = 1; i < stops.Length; i++)
                if (ph <= stops[i].ph)
                {
                    float t = (float)((ph - stops[i - 1].ph) / (stops[i].ph - stops[i - 1].ph));
                    return Rgb.Lerp(Rgb.Hex(stops[i - 1].hex), Rgb.Hex(stops[i].hex), t);
                }
            return Rgb.Hex(stops[stops.Length - 1].hex);
        }

        public static Rgb IndicatorColor(string id, double ph)
        {
            switch (id)
            {
                case "mo": return Ramp(ph, (3.1, "#D8342A"), (3.8, "#E8702A"), (4.4, "#F2C030"));
                case "mr": return Ramp(ph, (4.4, "#D23A3A"), (5.3, "#E8803A"), (6.2, "#F2D040"));
                case "btb": return Ramp(ph, (6.0, "#E8D040"), (6.9, "#4CA04A"), (7.6, "#2A5AC8"));
                case "bcg": return Ramp(ph, (3.8, "#E8D040"), (4.6, "#5AA06A"), (5.4, "#2A6AD0"));
                case "tb": return Ramp(ph, (1.2, "#D23A3A"), (2.8, "#F2D040"), (8.0, "#F2D040"), (9.6, "#2A5AC8"));
                case "ay": return Ramp(ph, (10.1, "#F2D040"), (12.0, "#C83A2A"));
                case "univ": return Universal(ph);
                default: return Rgb.White;
            }
        }

        /// <summary>Giấy chỉ thị vạn năng (thang màu 1 - 14).</summary>
        public static Rgb Universal(double ph) => Ramp(ph, (1, "#D8202A"), (3, "#F2702A"), (5, "#F2D030"), (7, "#4CB04A"), (9, "#2A80C8"), (11, "#3A3AA8"), (13, "#5A2A8A"));

        /// <summary>Giấy quỳ: đỏ khi pH &lt; 5, tím 5 - 8, xanh khi pH &gt; 8.</summary>
        public static Rgb Litmus(double ph) => Ramp(ph, (4.5, "#D23A4A"), (6.5, "#8A4A8A"), (8.3, "#3A5AC8"));

        // ------------------------------------------------------------------
        // Mô tả bằng chữ (card, ngữ cảnh gửi AI)
        // ------------------------------------------------------------------

        static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        /// <summary>Các thành phần đáng kể, xếp theo lượng: "Cu²⁺ 0,10 M · SO₄²⁻ 0,10 M · Cu(OH)₂↓".</summary>
        public static List<string> Components(Mix mix, double volumeMl, int max = 5)
        {
            var list = new List<(double score, string text)>();
            if (mix == null) return new List<string>();
            double V = Math.Max(volumeMl, 1e-3) / 1000.0;
            for (int i = 0; i < mix.Count; i++)
            {
                var d = mix.SpeciesAt(i);
                double n = mix.MolAt(i);
                if (n < 1e-9) continue;
                string text;
                switch (d.Kind)
                {
                    case SpeciesKind.Precipitate: text = d.Formula + "↓"; break;
                    case SpeciesKind.Solid: text = d.Id == "AgMirror" ? "gương bạc" : d.Formula + " (rắn)"; break;
                    case SpeciesKind.Lump: text = d.Formula + " (chưa tan)"; break;
                    case SpeciesKind.Indicator: text = d.Name; break;
                    case SpeciesKind.Weak: text = (d.ChemStt > 0 ? d.Name : ChemEngine.IonFormula(d.Id)) + " " + Conc(n / V); break;
                    default: text = (d.ChemStt > 0 ? d.Name : d.Formula) + " " + Conc(n / V); break;
                }
                list.Add((d.Kind == SpeciesKind.Indicator ? 1e-9 : n, text));
            }
            list.Sort((a, b) => b.score.CompareTo(a.score));
            var outList = new List<string>();
            for (int i = 0; i < list.Count && i < max; i++) outList.Add(list[i].text);
            return outList;
        }

        static string Conc(double c) => c >= 0.095 ? c.ToString("0.0#", Vi) + " M" : c >= 0.001 ? (c * 1000).ToString("0.#", Vi) + " mM" : "vết";
    }
}
