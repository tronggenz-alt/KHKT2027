using System;
using System.Collections.Generic;
using System.Linq;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Bom nhiệt lượng kiểu isoperibol (data/may/tinh_chat/nhiet_dot_chay.json):
    ///  • năng lượng mẫu = Σ m·|ΔcU|, ΔcU = ΔcH − Δn(khí)·R·T (Δn = n CO₂ − n O₂ của CₓHᵧO_z);
    ///  • nhiệt độ nước theo thời gian: trao đổi nhiệt với vỏ (dT/dt = k·(T_vỏ − T) + nhiệt khuấy), nhiệt tỏa ra theo hàm mũ sau khi đốt;
    ///  • hiệu chỉnh ΔT theo Regnault – Pfaundler (độ trôi tiền kỳ, hậu kỳ), trừ nhiệt sợi mồi.
    /// </summary>
    public sealed class CalorimetryLibrary
    {
        public sealed class Fuel
        {
            public string Id, Name, State, Source;
            public int C, H, O;
            public double M, DeltaH, CertifiedJg;
            public readonly List<string> Ids = new List<string>();
            public double DeltaN => C - (C + H / 4.0 - O / 2.0);  // n CO₂ − n O₂ (H₂O lỏng)
            /// <summary>Nhiệt đốt cháy ở thể tích không đổi (J/g, dương).</summary>
            public double Ujg(double tempK = 298.15) => CertifiedJg > 0 ? CertifiedJg : -(DeltaH * 1000 - DeltaN * 8.314 * tempK) / M;
        }

        public readonly List<Fuel> Fuels = new List<Fuel>();
        public Fuel Get(string speciesId) => Fuels.FirstOrDefault(f => f.Ids.Contains(speciesId));
        public Fuel ById(string id) => Fuels.FirstOrDefault(f => f.Id == id);

        public static CalorimetryLibrary Load(string json)
        {
            var lib = new CalorimetryLibrary();
            var r = MiniJson.Obj(MiniJson.Parse(json));
            foreach (var o in MiniJson.A(r, "chat"))
            {
                var d = MiniJson.Obj(o);
                var f = new Fuel
                {
                    Id = MiniJson.S(d, "id"), Name = MiniJson.S(d, "ten"), State = MiniJson.S(d, "trang_thai"), Source = MiniJson.S(d, "nguon"),
                    C = (int)MiniJson.N(d, "C"), H = (int)MiniJson.N(d, "H"), O = (int)MiniJson.N(d, "O"), M = MiniJson.N(d, "M"),
                    DeltaH = MiniJson.N(d, "dH"), CertifiedJg = MiniJson.N(d, "chung_nhan_J_g"),
                };
                foreach (var s in MiniJson.A(d, "ids")) f.Ids.Add(s as string);
                lib.Fuels.Add(f);
            }
            return lib;
        }

        /// <summary>Năng lượng (J) của chất rắn khô: phần cháy được theo thư viện, phần khác (muối) là tro không cháy.</summary>
        public double Energy(IEnumerable<SolidResidue.Phase> solids, out double combustibleMg, List<string> unknown = null)
        {
            double e = 0;
            combustibleMg = 0;
            foreach (var p in solids)
            {
                string sid = p.Id.StartsWith("org:") ? p.Id.Substring(4) : p.Id;
                var f = Get(sid);
                if (f == null) { if (p.Organic) unknown?.Add(p.Name); continue; }
                e += p.Mg / 1000.0 * f.Ujg();
                combustibleMg += p.Mg;
            }
            return e;
        }

        // =====================================================================
        // Mô phỏng nhiệt độ
        // =====================================================================

        public sealed class Run
        {
            public readonly List<(double t, double T)> Curve = new List<(double, double)>();
            public double FireAt, Released;
        }

        /// <summary>
        /// Đường nhiệt độ (phút, °C): nước ban đầu T0, vỏ Tj, hằng số trao đổi k (1/phút), nhiệt khuấy (K/phút), nhiệt dung C (J/K),
        /// năng lượng tỏa Q (J) bắt đầu ở fireAt, hằng số thời gian tau (phút). noise: nhiễu nhiệt kế (K).
        /// </summary>
        public static Run Simulate(double T0, double Tj, double k, double stir, double C, double Q, double fireAt, double tau, double minutes, double dt, double noise, Random rng)
        {
            var run = new Run { FireAt = fireAt, Released = Q };
            double T = T0, released = 0;
            for (double t = 0; t <= minutes + 1e-9; t += dt)
            {
                run.Curve.Add((t, T + noise * Gauss(rng)));
                double target = t >= fireAt ? Q * (1 - Math.Exp(-(t + dt - fireAt) / tau)) : 0;
                double dq = target - released;
                released = target;
                T += dq / C + (k * (Tj - T) + stir) * dt;
            }
            return run;
        }

        /// <summary>
        /// ΔT đã hiệu chỉnh trao đổi nhiệt (Regnault – Pfaundler): độ trôi tiền kỳ g₁ ở T₁ (trước khi đốt), hậu kỳ g₂ ở T₂;
        /// độ trôi trong chính kỳ coi là tuyến tính theo nhiệt độ; hiệu chỉnh = Σ g(T)·dt trên chính kỳ.
        /// Trả (ΔT thô = T cuối chính kỳ − T đầu, ΔT hiệu chỉnh).
        /// </summary>
        public static (double raw, double corrected) CorrectedRise(IList<(double t, double T)> c, double fireAt, double mainEnd)
        {
            (double slope, double mean) Fit(double a, double b)
            {
                var pts = c.Where(p => p.t >= a && p.t <= b).ToList();
                if (pts.Count < 2) return (0, pts.Count > 0 ? pts[0].T : 0);
                var f = UvVisLibrary.Linear(pts.Select(p => (p.t, p.T)).ToList());
                return (f.Slope, pts.Average(p => p.T));
            }
            var pre = Fit(0, fireAt);
            double tEnd = c[c.Count - 1].t;
            var post = Fit(mainEnd, tEnd);
            double T1 = c.Where(p => p.t <= fireAt).Last().T, T2 = c.Where(p => p.t <= mainEnd).Last().T;
            double raw = T2 - T1;
            double g1 = pre.slope, g2 = post.slope, m1 = pre.mean, m2 = post.mean;
            double corr = 0;
            var main = c.Where(p => p.t > fireAt && p.t <= mainEnd).ToList();
            for (int i = 1; i < main.Count; i++)
            {
                double Tm = (main[i].T + main[i - 1].T) / 2, dt = main[i].t - main[i - 1].t;
                double g = Math.Abs(m2 - m1) > 1e-9 ? g1 + (g2 - g1) * (Tm - m1) / (m2 - m1) : g1;
                corr += g * dt;
            }
            return (raw, raw - corr);
        }

        static double Gauss(Random r)
        {
            double u1 = 1 - r.NextDouble(), u2 = r.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }
    }
}
