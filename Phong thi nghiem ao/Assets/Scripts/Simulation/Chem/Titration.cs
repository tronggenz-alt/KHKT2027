using System;
using System.Collections.Generic;
using System.Linq;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Chuẩn độ điện thế axit – bazơ:
    ///  • điện cực pH: E = E_lệch − s·59,16·(pH − 7) mV ở 25 °C (Nernst); hiệu chuẩn bằng đệm 4,01 / 7,00 / 10,01 để tìm s, E_lệch;
    ///  • pH của mẫu sau khi thêm dung dịch chuẩn tính bằng bộ máy hóa học (ChemEngine.SolveH);
    ///  • chuẩn hóa NaOH bằng KHP (kali hiđro phtalat, pKa₁ 2,95, pKa₂ 5,41);
    ///  • điểm tương đương: cực đại dE/dV (nội suy parabol); kiểm tra bằng đồ thị Gran.
    /// </summary>
    public static class PotentiometricTitration
    {
        public const double NernstMv = 59.16;
        public const double KhpMolarMass = 204.22;
        const double KhpKa1 = 1.12e-3, KhpKa2 = 3.9e-6;
        public static readonly double[] Buffers = { 4.01, 7.00, 10.01 };

        /// <summary>Thế điện cực (mV) ở pH, với độ dốc tương đối slope (1 = 100%) và thế lệch tại pH 7.</summary>
        public static double ElectrodeMv(double ph, double slope, double offsetMv) => offsetMv - slope * NernstMv * (ph - 7);

        /// <summary>pH máy hiển thị từ thế đo được theo thông số hiệu chuẩn.</summary>
        public static double DisplayPh(double mv, double slope, double offsetMv) => 7 - (mv - offsetMv) / (slope * NernstMv);

        /// <summary>Hiệu chuẩn: đường thẳng E theo pH qua các điểm đệm → độ dốc (tỉ lệ) và thế lệch tại pH 7.</summary>
        public static (double slope, double offsetMv) Calibrate(IList<(double ph, double mv)> pts)
        {
            if (pts.Count < 2) return (1, 0);
            var f = UvVisLibrary.Linear(pts.Select(p => (p.ph, p.mv)).ToList());
            return (-f.Slope / NernstMv, f.Intercept + f.Slope * 7);
        }

        /// <summary>pH thật của mẫu (mix trong V0 lít) sau khi thêm v lít dung dịch chuẩn nồng độ c: NaOH (base = true) hoặc HCl.</summary>
        public static double SamplePh(Mix mix, double v0L, bool base_, double c, double vL)
        {
            var b = mix.ToBuilder();
            if (vL > 0) b.Add(base_ ? "Na+" : "Cl-", c * vL);  // bộ máy hóa học cân bằng điện tích: thêm Na⁺ tương đương thêm OH⁻
            return -Math.Log10(ChemEngine.SolveH(b, v0L + vL));
        }

        /// <summary>pH khi chuẩn độ nKhp mol KHP (trong v0 lít nước) bằng v lít NaOH nồng độ c (giải cân bằng điện tích bằng chia đôi).</summary>
        public static double KhpPh(double nKhp, double v0L, double c, double vL)
        {
            double V = v0L + vL, P = nKhp / V, K = nKhp / V, Na = c * vL / V;
            double lo = 0, hi = 14;
            for (int i = 0; i < 80; i++)
            {
                double ph = (lo + hi) / 2, h = Math.Pow(10, -ph), oh = 1e-14 / h;
                double den = h * h + KhpKa1 * h + KhpKa1 * KhpKa2;
                double hp = P * KhpKa1 * h / den, p2 = P * KhpKa1 * KhpKa2 / den;
                double charge = h + K + Na - oh - hp - 2 * p2;  // > 0: còn dư điện tích dương → pH thật cao hơn
                if (charge > 0) lo = ph; else hi = ph;
            }
            return (lo + hi) / 2;
        }

        /// <summary>
        /// Điểm tương đương: các cực đại cục bộ của |dE/dV| lớn hơn ngưỡng (mV/mL), cách nhau ít nhất minGapMl; vị trí nội suy parabol.
        /// pts: (V mL, E mV) theo thứ tự V tăng.
        /// </summary>
        public static List<(double v, double dEdV)> Endpoints(IList<(double v, double mv)> pts, double thresholdMvPerMl = 150, double minGapMl = 0.5)
        {
            var d = Derivative(pts);
            var res = new List<(double v, double dEdV)>();
            for (int i = 1; i < d.Count - 1; i++)
            {
                double a = Math.Abs(d[i - 1].d), b = Math.Abs(d[i].d), c = Math.Abs(d[i + 1].d);
                if (b < thresholdMvPerMl || b < a || b < c) continue;
                // nội suy parabol qua 3 điểm (khoảng V có thể không đều)
                double x0 = d[i - 1].v, x1 = d[i].v, x2 = d[i + 1].v;
                double den = (x0 - x1) * (x0 - x2) * (x1 - x2);
                double v = x1;
                if (Math.Abs(den) > 1e-12)
                {
                    double A = (x2 * (b - a) + x1 * (a - c) + x0 * (c - b)) / den;
                    double B = (x2 * x2 * (a - b) + x1 * x1 * (c - a) + x0 * x0 * (b - c)) / den;
                    if (A < 0) v = Math.Max(x0, Math.Min(x2, -B / (2 * A)));
                }
                if (res.Count > 0 && v - res[res.Count - 1].v < minGapMl)
                {
                    if (b > Math.Abs(res[res.Count - 1].dEdV)) res[res.Count - 1] = (v, d[i].d);
                    continue;
                }
                res.Add((v, d[i].d));
            }
            return res;
        }

        /// <summary>Đạo hàm dE/dV tại trung điểm mỗi khoảng.</summary>
        public static List<(double v, double d)> Derivative(IList<(double v, double mv)> pts)
        {
            var d = new List<(double v, double d)>();
            for (int i = 1; i < pts.Count; i++)
            {
                double dv = pts[i].v - pts[i - 1].v;
                if (dv <= 1e-9) continue;
                d.Add(((pts[i].v + pts[i - 1].v) / 2, (pts[i].mv - pts[i - 1].mv) / dv));
            }
            return d;
        }

        /// <summary>
        /// Đồ thị Gran cho axit yếu chuẩn bằng bazơ mạnh: G = V·10^(−pH) theo V là đường thẳng trước điểm tương đương,
        /// cắt trục V tại V_tđ. Dùng các điểm có V trong khoảng [lo, hi] (mL).
        /// </summary>
        public static (double veq, double r2) Gran(IList<(double v, double ph)> pts, double lo, double hi)
        {
            var sel = pts.Where(p => p.v >= lo && p.v <= hi).Select(p => (p.v, p.v * Math.Pow(10, -p.ph))).ToList();
            if (sel.Count < 3) return (double.NaN, 0);
            var f = UvVisLibrary.Linear(sel);
            return (f.Slope != 0 ? -f.Intercept / f.Slope : double.NaN, f.R2);
        }
    }
}
