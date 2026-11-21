using System;
using System.Collections.Generic;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Bảng phức chất theo hằng số bền tổng β (và luật tỉ lệ cho phức tạo từ chính kết tủa của nó: HgI₂ → [HgI₄]²⁻,
    /// AgCN → [Ag(CN)₂]⁻). Phức tạo lại mỗi lần cân bằng, tan trở lại khi điều kiện đổi (giống kết tủa).
    /// Kèm tách pha của chất hữu cơ ít tan dạng phân tử (phenol, anilin, axit benzoic): vượt độ tan thì vẩn đục / tách lớp,
    /// chuyển thành muối (phenolat, ion anilini) thì tan.
    /// </summary>
    public static partial class ChemEngine
    {
        sealed class ComplexRule
        {
            public string Metal, Ligand, Complex, Title, Eq, Detail;
            public int N;
            public double LogBeta;
            public int LigandProtons = -1;      // hệ axit yếu: số proton gắn của dạng phối tử (0 = mất hết proton); -1 = ion bền
            public string Ppt;                  // phức tạo từ kết tủa của chính nó (luật tỉ lệ)
            public int PptRatio;
        }

        static List<ComplexRule> complexRules;

        static void Cx(string metal, string ligand, int n, double logBeta, string complex, string title, string eq, string detail, int protons = -1, string ppt = null, int pptRatio = 0)
        {
            complexRules.Add(new ComplexRule { Metal = metal, Ligand = ligand, N = n, LogBeta = logBeta, Complex = complex, Title = title, Eq = eq, Detail = detail, LigandProtons = protons, Ppt = ppt, PptRatio = pptRatio });
        }

        static void BuildComplexes()
        {
            if (complexRules != null) return;
            complexRules = new List<ComplexRule>();
            // β thực nghiệm (25 °C, nồng độ); CuCl₄²⁻, CoCl₄²⁻ dùng β hiệu dụng vì chỉ hiện rõ trong HCl rất đặc
            Cx("Fe3+", "F", 6, 16.1, "FeF6", "Màu vàng nâu của Fe³⁺ mất (tạo phức [FeF₆]³⁻ không màu)", "Fe³⁺ + 6F⁻ → [FeF₆]³⁻",
               "Ion florua \"che\" Fe³⁺: dùng để loại ảnh hưởng của Fe³⁺ trong phân tích.", 0);
            Cx("Fe3+", "PO4", 1, 9.35, "FePO4c", "Màu vàng của Fe³⁺ nhạt đi (phức sắt(III) - photphat không màu)", "Fe³⁺ + HPO₄²⁻ → [Fe(HPO₄)]⁺",
               "Axit photphoric tạo phức với Fe³⁺: cách che Fe³⁺ trong chuẩn độ.", 1);
            Cx("Cu2+", "Cl-", 4, -2.0, "CuCl4", "Dung dịch chuyển xanh lục rồi vàng lục ([CuCl₄]²⁻)", "Cu²⁺ + 4Cl⁻ ⇌ [CuCl₄]²⁻",
               "Cân bằng tạo phức chuyển dịch khi nồng độ Cl⁻ rất lớn; pha loãng thì trở lại màu xanh lam.");
            Cx("Co2+", "Cl-", 4, -3.0, "CoCl4", "Dung dịch hồng chuyển xanh lam ([CoCl₄]²⁻)", "[Co(H₂O)₆]²⁺ + 4Cl⁻ ⇌ [CoCl₄]²⁻ + 6H₂O",
               "Cân bằng nhạy với nồng độ Cl⁻ và nhiệt độ (đun nóng: xanh hơn).");
            Cx("Co2+", "SCN-", 4, 3.0, "CoSCN", "Dung dịch chuyển xanh lam ([Co(SCN)₄]²⁻)", "Co²⁺ + 4SCN⁻ → [Co(SCN)₄]²⁻",
               "Nhận biết Co²⁺: màu xanh rõ hơn khi lắc với axeton hoặc ancol amylic.");
            Cx("Ag+", "S2O32-", 2, 13.46, "AgS2O3", "Tạo phức tan [Ag(S₂O₃)₂]³⁻ (không màu)", "Ag⁺ + 2S₂O₃²⁻ → [Ag(S₂O₃)₂]³⁻",
               "Nguyên tắc định hình ảnh trong nhiếp ảnh: thiosunfat hòa tan AgBr chưa bị chiếu sáng.");
            Cx("Ag+", "CN", 2, 20.5, "AgCN2", "Kết tủa AgCN tan trong xianua dư ([Ag(CN)₂]⁻)", "AgCN + CN⁻ → [Ag(CN)₂]⁻",
               "Phức xianua rất bền (dùng trong mạ bạc, tách vàng bạc).", 0, "AgCN", 1);
            Cx("Hg2+", "I-", 4, 29.8, "HgI42-", "Kết tủa đỏ HgI₂ tan trong KI dư, tạo [HgI₄]²⁻ không màu", "HgI₂ + 2I⁻ → [HgI₄]²⁻",
               "Đây cũng là thành phần của thuốc thử Nessler (K₂[HgI₄] trong kiềm).", -1, "HgI2", 2);
        }

        /// <summary>Phần dạng có đúng k proton gắn (k = 0: mất hết proton) của hệ axit yếu ở [H⁺] = h.</summary>
        static double AlphaK(SpeciesDef d, double h, int k)
        {
            if (d.Kind != SpeciesKind.Weak || d.Ka == null) return k == 0 ? 1 : 0;
            int n = d.Ka.Length;
            // hạng tử j: số proton gắn = n - j
            double D = 0, want = 0, prod = 1;
            for (int j = 0; j <= n; j++)
            {
                if (j > 0) prod *= d.Ka[j - 1];
                double t = prod * Math.Pow(h, n - j);
                D += t;
                if (n - j == k) want = t;
            }
            return want / D;
        }

        static void ComplexTable(MixBuilder b, double V, double h)
        {
            BuildComplexes();
            foreach (var r in complexRules)
            {
                int mi = SpeciesRegistry.IndexOf(r.Metal), li = SpeciesRegistry.IndexOf(r.Ligand), ci = SpeciesRegistry.IndexOf(r.Complex);
                if (mi < 0 || li < 0 || ci < 0) continue;
                double M = b.Get(mi) / V, Ltot = b.Get(li) / V;
                if (M <= 1e-12 || Ltot <= 1e-12) continue;
                double x;
                if (r.Ppt != null)
                {
                    // tạo kết tủa trước (tỉ lệ ≤ PptRatio), dư phối tử thì tan dần thành phức (tỉ lệ ≥ N)
                    double ratio = Ltot / M;
                    double f = Math.Max(0, Math.Min(1, (ratio - r.PptRatio) / (r.N - r.PptRatio)));
                    x = Math.Min(f * M, Ltot / r.N);
                }
                else
                {
                    var ld = SpeciesRegistry.At(li);
                    double frac = r.LigandProtons >= 0 ? AlphaK(ld, h, r.LigandProtons) : 1;
                    double beta = Math.Pow(10, r.LogBeta);
                    double lo = 0, hi = Math.Min(M, Ltot / r.N);
                    for (int it = 0; it < 60; it++)
                    {
                        double mid = 0.5 * (lo + hi);
                        double free = Math.Max(0, Ltot - r.N * mid) * frac;
                        // phức / kim loại tự do = β [L]ⁿ
                        double lhs = mid, rhs = beta * Math.Pow(free, r.N) * (M - mid);
                        if (lhs < rhs) lo = mid; else hi = mid;
                    }
                    x = lo;
                }
                if (x * V <= 1e-12) continue;
                b.Add(mi, -x * V);
                b.Add(li, -r.N * x * V);
                b.Add(ci, x * V);
            }
        }

        static bool RevertComplex(MixBuilder b, SpeciesDef d, double m)
        {
            BuildComplexes();
            foreach (var r in complexRules)
            {
                if (r.Complex != d.Id) continue;
                b.Set(d.Index, 0);
                b.Add(r.Metal, m);
                b.Add(r.Ligand, r.N * m);
                return true;
            }
            return false;
        }

        static ChemEvent TableComplexEvent(SpeciesDef sp, double mol)
        {
            BuildComplexes();
            foreach (var r in complexRules)
                if (r.Complex == sp.Id)
                    return new ChemEvent { Kind = EventKind.Color, Key = "phuc:" + sp.Id, Title = r.Title, Equation = r.Eq, Detail = r.Detail, Color = sp.Color, Amount = mol };
            return null;
        }

        // ------------------------------------------------------------------
        // Tách pha của chất hữu cơ ít tan (dạng phân tử trung hòa)
        // ------------------------------------------------------------------

        /// <summary>Độ tan (mol/l) của dạng phân tử trung hòa theo công thức trong danh mục.</summary>
        static readonly Dictionary<string, double> OrganicSolubility = new Dictionary<string, double>
        {
            ["C6H5OH"] = 0.88, ["C6H5NH2"] = 0.39, ["C6H5COOH"] = 0.028, ["C6H4(OH)COOH"] = 0.016, ["CH3C6H4OH"] = 0.24,
            ["C6H5NHCH3"] = 0.05, ["C17H35COOH"] = 1e-5, ["C15H31COOH"] = 1e-5, ["C17H33COOH"] = 1e-5, ["C17H31COOH"] = 1e-5,
            ["C11H23COOH"] = 3e-4,
        };

        /// <summary>Mã kết tủa "tách ra" ứng với một loài hữu cơ ít tan (null nếu loài tan tốt).</summary>
        public static string SeparatedId(SpeciesDef d)
        {
            if (d == null || d.ChemStt <= 0 || d.Kind != SpeciesKind.Weak) return null;
            string f = AsciiOf(d.Formula);
            if (f == null || !OrganicSolubility.ContainsKey(f)) return null;
            string id = (d.HasTag("anilin") || d.Charge == 1 ? "AnilinKT:" : "PhenolKT:") + d.Id;
            var s = SpeciesRegistry.Get(id);
            if (s == null)
            {
                bool liquid = d.HasTag("anilin") || d.Density < 1.2 && d.Charge == 1;
                s = SpeciesRegistry.Add(id, d.Formula, SpeciesKind.Precipitate, 0, d.MolarMass, d.Name + (liquid ? " (tách lớp)" : " (tách ra, vẩn đục)"));
                s.Color = Rgb.Hex(liquid ? "#F4EEDC" : "#F4F2EA");
                s.ColorStrength = 1;
                s.Tags.Add("tachra:" + d.Id);
                s.ChemStt = d.ChemStt;
            }
            return id;
        }

        static string AsciiOf(string pretty)
        {
            if (pretty == null) return null;
            var sb = new System.Text.StringBuilder();
            foreach (char ch in pretty)
            {
                int k = "₀₁₂₃₄₅₆₇₈₉".IndexOf(ch);
                if (k >= 0) sb.Append((char)('0' + k)); else sb.Append(ch);
            }
            return sb.ToString();
        }

        static bool SeparateOrganics(MixBuilder b, double V, double h)
        {
            bool any = false;
            foreach (int s in b.Snapshot())
            {
                var d = SpeciesRegistry.At(s);
                string id = SeparatedId(d);
                if (id == null) continue;
                double sol = OrganicSolubility[AsciiOf(d.Formula)];
                double tot = b.Get(s);
                // dạng trung hòa: axit (phenol) là dạng đủ proton, bazơ (anilin) là dạng mất proton
                double neutral = d.Charge == 1 ? 1 - AlphaFirst(d, h) : AlphaFirst(d, h);
                double excess = tot * neutral - sol * V;
                if (excess <= 1e-9) continue;
                b.Add(s, -excess);
                b.Add(id, excess);
                any = true;
            }
            return any;
        }

        static bool RevertSeparated(MixBuilder b, SpeciesDef d, double m)
        {
            foreach (var t in d.Tags)
                if (t.StartsWith("tachra:"))
                {
                    b.Set(d.Index, 0);
                    b.Add(t.Substring(7), m);
                    return true;
                }
            return false;
        }
    }
}
