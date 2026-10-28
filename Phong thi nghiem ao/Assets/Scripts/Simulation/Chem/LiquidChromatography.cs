using System;
using System.Collections.Generic;
using System.Linq;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Mô hình HPLC (data/may/tinh_chat/hplc.json):
    ///  • t₀ = thể tích pha động trong cột / lưu lượng; t_R = t₀(1 + k);
    ///  • log k = log k_w − S·φ (pha đảo: S dương; cột NH₂ chế độ HILIC: S âm); axit yếu: chỉ dạng phân tử được giữ;
    ///  • áp suất theo định luật Darcy: ΔP ∝ η·L·F/(d_p²·r²), độ nhớt hỗn hợp nước – metanol / axetonitril (mô phỏng);
    ///  • detector UV: diện tích ∝ ε(λ)·c; RID: diện tích ∝ nồng độ khối lượng (g/L). Chất không hấp thụ thì không có pic UV.
    /// </summary>
    public sealed class HplcLibrary
    {
        public sealed class Compound
        {
            public string Id, Name;
            public readonly List<string> Ids = new List<string>();
            public double LogKw, S, PKa = double.NaN, KIon, Eps, Dn, M;
        }

        public sealed class MethodDef
        {
            public string Id, Name, Column, Mobile, Solvent, Detector;
            public double Phi, Ph, Lambda, RunMin;
            public readonly List<Compound> Compounds = new List<Compound>();
            public Compound Get(string speciesId) => Compounds.FirstOrDefault(c => c.Ids.Contains(speciesId));
        }

        public struct Peak
        {
            public Compound C;
            public double Tr, Sigma, Area, Height, MolL;
        }

        public readonly List<MethodDef> Methods = new List<MethodDef>();
        public double LengthCm = 25, RadiusCm = 0.23, ParticleUm = 5, Porosity = 0.65, Plates = 12000;
        /// <summary>Hằng số áp suất (bar) cho cột 25 cm × 4,6 mm hạt 5 µm, mô phỏng: nước 30 °C, 1 mL/phút ≈ 115 bar.</summary>
        public const double PressureK = 7.6;
        public const double MaxBar = 400;

        public MethodDef Method(string id) => Methods.FirstOrDefault(m => m.Id == id);

        public static HplcLibrary Load(string json)
        {
            var lib = new HplcLibrary();
            var root = MiniJson.Obj(MiniJson.Parse(json));
            var col = MiniJson.Obj(root.TryGetValue("cot", out var c) ? c : null);
            lib.LengthCm = MiniJson.N(col, "dai_cm", 25);
            lib.RadiusCm = MiniJson.N(col, "ban_kinh_cm", 0.23);
            lib.ParticleUm = MiniJson.N(col, "hat_um", 5);
            lib.Porosity = MiniJson.N(col, "do_rong", 0.65);
            lib.Plates = MiniJson.N(col, "so_dia", 12000);
            foreach (var o in MiniJson.A(root, "phuong_phap"))
            {
                var d = MiniJson.Obj(o);
                var m = new MethodDef
                {
                    Id = MiniJson.S(d, "id"), Name = MiniJson.S(d, "ten"), Column = MiniJson.S(d, "cot"), Mobile = MiniJson.S(d, "pha_dong"),
                    Solvent = MiniJson.S(d, "dung_moi"), Detector = MiniJson.S(d, "detector"), Phi = MiniJson.N(d, "phi"), Ph = MiniJson.N(d, "ph", 7),
                    Lambda = MiniJson.N(d, "lambda"), RunMin = MiniJson.N(d, "chay_phut", 15),
                };
                foreach (var co in MiniJson.A(d, "chat"))
                {
                    var x = MiniJson.Obj(co);
                    var cp = new Compound
                    {
                        Id = MiniJson.S(x, "id"), Name = MiniJson.S(x, "ten"), LogKw = MiniJson.N(x, "log_kw"), S = MiniJson.N(x, "S"),
                        PKa = MiniJson.N(x, "pka", double.NaN), KIon = MiniJson.N(x, "k_ion"), Eps = MiniJson.N(x, "eps"), Dn = MiniJson.N(x, "dn"), M = MiniJson.N(x, "M"),
                    };
                    foreach (var s in MiniJson.A(x, "ids")) cp.Ids.Add(s as string);
                    m.Compounds.Add(cp);
                }
                lib.Methods.Add(m);
            }
            return lib;
        }

        // =====================================================================

        /// <summary>Thể tích pha động trong cột (mL).</summary>
        public double ColumnVolume => Porosity * Math.PI * RadiusCm * RadiusCm * LengthCm;

        public double DeadTime(double flow) => ColumnVolume / Math.Max(0.01, flow);

        /// <summary>Hệ số giữ k ở phần dung môi φ và pH pha động.</summary>
        public static double K(Compound c, double phi, double ph)
        {
            double kn = Math.Pow(10, c.LogKw - c.S * phi);
            if (double.IsNaN(c.PKa)) return kn;
            double fNeutral = 1.0 / (1.0 + Math.Pow(10, ph - c.PKa));
            return kn * fNeutral + c.KIon * (1 - fNeutral);
        }

        /// <summary>Độ nhớt pha động (cP) ở nhiệt độ tC: hỗn hợp nước – metanol có cực đại ở khoảng 40 – 50% (mô phỏng).</summary>
        public static double Viscosity(string solvent, double phi, double tC)
        {
            double eta = solvent == "metanol" ? 0.89 * (1 - phi) + 0.55 * phi + 3.0 * phi * (1 - phi)
                                              : 0.89 * (1 - phi) + 0.34 * phi + 0.9 * phi * (1 - phi);
            return eta * Math.Exp(-0.022 * (tC - 25));
        }

        /// <summary>Áp suất qua cột (bar); clog: hệ số tắc do cặn (1 = cột sạch).</summary>
        public double Pressure(string solvent, double phi, double flow, double tC, double clog = 1) =>
            PressureK * Viscosity(solvent, phi, tC) * flow * LengthCm / (ParticleUm * ParticleUm * RadiusCm * RadiusCm) * clog;

        /// <summary>Các pic của mẫu (đã pha loãng) theo phương pháp, φ, pH, lưu lượng. unknown: chất có trong mẫu mà phương pháp không đo được.</summary>
        public List<Peak> Peaks(MethodDef m, Mix mix, double volumeMl, double dilution, double phi, double ph, double flow, double injectUl = 10, List<string> notSeen = null)
        {
            var res = new List<Peak>();
            if (m == null || mix == null) return res;
            double V = Math.Max(1e-6, volumeMl / 1000.0), t0 = DeadTime(flow);
            for (int i = 0; i < mix.Count; i++)
            {
                var d = mix.SpeciesAt(i);
                double c = mix.MolAt(i) / V / Math.Max(1, dilution);
                if (c < 1e-8 || d.IsSolidPhase) continue;
                var cp = m.Get(d.Id);
                if (cp == null)
                {
                    if (d.ChemStt > 0 && c > 1e-4) notSeen?.Add(d.Name);
                    continue;
                }
                double signal = m.Detector == "ri" ? cp.Dn * c * cp.M : cp.Eps * c;  // RID: g/L; UV: A trong ô đo 1 cm
                if (signal <= 0) { notSeen?.Add(cp.Name); continue; }
                double k = K(cp, phi, ph), tr = t0 * (1 + k);
                int at = res.FindIndex(p => p.C == cp);
                if (at >= 0) { var p = res[at]; p.MolL += c; p.Area += signal * injectUl * 60; res[at] = p; continue; }
                res.Add(new Peak { C = cp, Tr = tr, MolL = c, Area = signal * injectUl * 60 });
            }
            for (int i = 0; i < res.Count; i++)
            {
                var p = res[i];
                p.Sigma = p.Tr / Math.Sqrt(Plates) + 0.01;  // + phần doãng ngoài cột
                p.Height = p.Area / (p.Sigma * Math.Sqrt(2 * Math.PI));
                res[i] = p;
            }
            return res.OrderBy(p => p.Tr).ToList();
        }

        public static double Signal(IList<Peak> peaks, double t)
        {
            double y = 0;
            foreach (var p in peaks)
            {
                double x = (t - p.Tr) / p.Sigma;
                if (Math.Abs(x) < 8) y += p.Height * Math.Exp(-0.5 * x * x);
            }
            return y;
        }

        /// <summary>Độ phân giải của hai pic kề nhau.</summary>
        public static double Resolution(Peak a, Peak b) => 2 * Math.Abs(b.Tr - a.Tr) / (4 * a.Sigma + 4 * b.Sigma);
    }
}
