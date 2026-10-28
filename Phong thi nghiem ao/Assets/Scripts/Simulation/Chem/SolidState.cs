using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Khkt.Simulation.Chem
{
    /// <summary>Đếm nguyên tử trong công thức (mọi nguyên tố thường gặp) và tính khối lượng mol.</summary>
    public static class Formula
    {
        public static readonly Dictionary<string, double> Mass = new Dictionary<string, double>
        {
            ["H"] = 1.008, ["C"] = 12.011, ["N"] = 14.007, ["O"] = 15.999, ["F"] = 18.998, ["Na"] = 22.990, ["Mg"] = 24.305, ["Al"] = 26.982,
            ["Si"] = 28.085, ["P"] = 30.974, ["S"] = 32.06, ["Cl"] = 35.45, ["K"] = 39.098, ["Ca"] = 40.078, ["Cr"] = 51.996, ["Mn"] = 54.938,
            ["Fe"] = 55.845, ["Co"] = 58.933, ["Ni"] = 58.693, ["Cu"] = 63.546, ["Zn"] = 65.38, ["Br"] = 79.904, ["Sr"] = 87.62, ["Ag"] = 107.87,
            ["Cd"] = 112.41, ["I"] = 126.90, ["Ba"] = 137.33, ["Au"] = 196.97, ["Hg"] = 200.59, ["Pb"] = 207.2,
        };

        static readonly Regex Token = new Regex(@"([A-Z][a-z]?)(\d*)|([\(\[])|([\)\]])(\d*)|[·•.](\d*)");

        /// <summary>Số nguyên tử từng nguyên tố; hỗ trợ ngoặc và nước kết tinh "CaSO4·2H2O".</summary>
        public static Dictionary<string, double> Count(string f)
        {
            var res = new Dictionary<string, double>();
            if (string.IsNullOrEmpty(f)) return res;
            f = Regex.Replace(f, @"\((s|r|kt|l|than)\)$", "");
            f = Regex.Replace(f, @"\d*[+-]$", "");
            foreach (var part in f.Split('·', '•'))
            {
                var m0 = Regex.Match(part, @"^(\d+)(.*)$");
                double mult = m0.Success ? double.Parse(m0.Groups[1].Value) : 1;
                string body = m0.Success ? m0.Groups[2].Value : part;
                var stack = new Stack<Dictionary<string, double>>();
                var cur = new Dictionary<string, double>();
                foreach (Match m in Token.Matches(body))
                {
                    if (m.Groups[1].Success)
                    {
                        double n = m.Groups[2].Value.Length > 0 ? double.Parse(m.Groups[2].Value) : 1;
                        cur[m.Groups[1].Value] = (cur.TryGetValue(m.Groups[1].Value, out var v) ? v : 0) + n;
                    }
                    else if (m.Groups[3].Success) { stack.Push(cur); cur = new Dictionary<string, double>(); }
                    else if (m.Groups[4].Success && stack.Count > 0)
                    {
                        double n = m.Groups[5].Value.Length > 0 ? double.Parse(m.Groups[5].Value) : 1;
                        var inner = cur;
                        cur = stack.Pop();
                        foreach (var kv in inner) cur[kv.Key] = (cur.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value * n;
                    }
                }
                foreach (var kv in cur) if (Mass.ContainsKey(kv.Key)) res[kv.Key] = (res.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value * mult;
            }
            return res;
        }

        public static double MolarMass(string f) => Count(f).Sum(kv => kv.Value * Mass[kv.Key]);
    }

    /// <summary>
    /// Chất rắn lấy từ mẫu để đo XRD / SEM / bom nhiệt lượng: lọc lấy kết tủa, hoặc cô cạn toàn bộ (muối tan kết tinh theo cặp ion,
    /// chất hữu cơ khó bay hơi còn lại dạng vô định hình, chất dễ bay hơi bay đi).
    /// </summary>
    public static class SolidResidue
    {
        public sealed class Phase
        {
            public string Id, Name, Formula;
            public double Mg;                 // khối lượng (mg) trong lượng mẫu đã lấy
            public bool Crystalline = true;   // false: vô định hình (keo hiđroxit, đường, axit hữu cơ)
            public bool Organic;
            public Dictionary<string, double> Atoms => Chem.Formula.Count(Formula);
        }

        // cặp ion kết tinh khi cô cạn (thứ tự ưu tiên: ít tan trước)
        static readonly (string cat, int zc, string an, int za, string id, string formula, string name)[] Pairs =
        {
            ("Ag+", 1, "Cl-", 1, "AgCl", "AgCl", "bạc clorua"),
            ("Ba2+", 2, "SO4", 2, "BaSO4", "BaSO4", "bari sunfat"),
            ("Ca2+", 2, "CO3", 2, "CaCO3", "CaCO3", "canxi cacbonat"),
            ("Ca2+", 2, "SO4", 2, "CaSO4", "CaSO4·2H2O", "canxi sunfat (thạch cao)"),
            ("Mg2+", 2, "CO3", 2, "MgCO3", "MgCO3", "magie cacbonat"),
            ("Na+", 1, "Cl-", 1, "NaCl(r)", "NaCl", "natri clorua"),
            ("K+", 1, "Cl-", 1, "KCl(r)", "KCl", "kali clorua"),
            ("Cu2+", 2, "SO4", 2, "CuSO4(r)", "CuSO4·5H2O", "đồng sunfat ngậm nước"),
            ("Zn2+", 2, "SO4", 2, "ZnSO4(r)", "ZnSO4·7H2O", "kẽm sunfat ngậm nước"),
            ("Ni2+", 2, "SO4", 2, "NiSO4(r)", "NiSO4·6H2O", "niken sunfat ngậm nước"),
            ("Na+", 1, "SO4", 2, "Na2SO4(r)", "Na2SO4", "natri sunfat"),
            ("Na+", 1, "CrVI", 2, "Na2Cr2O7(r)", "Na2Cr2O7·2H2O", "natri đicromat"),
            ("Al3+", 3, "SO4", 2, "Al2(SO4)3(r)", "Al2(SO4)3·18H2O", "nhôm sunfat ngậm nước"),
            ("Fe3+", 3, "SO4", 2, "Fe2(SO4)3(r)", "Fe2(SO4)3", "sắt(III) sunfat"),
            ("Na+", 1, "Bz", 1, "NaBz(r)", "C7H5O2Na", "natri benzoat"),
            ("Mg2+", 2, "Cl-", 1, "MgCl2(r)", "MgCl2·6H2O", "magie clorua ngậm nước"),
            ("Fe2+", 2, "Cl-", 1, "FeCl2(r)", "FeCl2·4H2O", "sắt(II) clorua"),
        };

        /// <summary>Axit hữu cơ khó bay hơi: ghép với cation thành muối (natri benzoat) trước, phần dư còn lại dạng axit.</summary>
        static readonly string[] AcidAnions = { "Bz", "Cit", "Tart", "Ox" };

        /// <summary>Chất hữu cơ khó bay hơi có trong thư viện (mã loài → công thức).</summary>
        static readonly Dictionary<string, (string formula, string name)> Organic = new Dictionary<string, (string, string)>
        {
            ["hc:578"] = ("C6H12O6", "glucozơ"), ["hc:579"] = ("C6H12O6", "fructozơ"), ["hc:580"] = ("C12H22O11", "saccarozơ"),
            ["hc:581"] = ("C12H22O11", "mantozơ"), ["hc:582"] = ("C12H22O11", "lactozơ"), ["Cit"] = ("C6H8O7", "axit xitric"),
            ["Tart"] = ("C4H6O6", "axit tartaric"), ["Ox"] = ("C2H2O4", "axit oxalic"), ["Bz"] = ("C7H6O2", "axit benzoic"),
        };

        /// <summary>
        /// Chất rắn thu được từ volumeTakenMl mẫu (mix trong volumeMl). evaporate = false: chỉ lọc lấy kết tủa / chất rắn sẵn có.
        /// notes: ghi chú (chất bay hơi, axit dư...).
        /// </summary>
        public static List<Phase> From(Mix mix, double volumeMl, double volumeTakenMl, bool evaporate, List<string> notes = null)
        {
            var res = new List<Phase>();
            if (mix == null) return res;
            double f = Math.Min(1, volumeTakenMl / Math.Max(1e-6, volumeMl));
            void Add(string id, string formula, string name, double mol, bool crystalline, bool organic = false)
            {
                if (mol <= 1e-12) return;
                double mg = mol * Chem.Formula.MolarMass(formula) * 1000;
                var p = res.FirstOrDefault(x => x.Id == id);
                if (p != null) { p.Mg += mg; return; }
                res.Add(new Phase { Id = id, Formula = formula, Name = name, Mg = mg, Crystalline = crystalline, Organic = organic });
            }
            var ions = new Dictionary<string, double>();
            for (int i = 0; i < mix.Count; i++)
            {
                var d = mix.SpeciesAt(i);
                double n = mix.MolAt(i) * f;
                if (n <= 1e-12) continue;
                if (d.Kind == SpeciesKind.Lump && d.Composition != null)
                {
                    foreach (var (cid, k) in d.Composition)
                    {
                        var s = SpeciesRegistry.Get(cid);
                        if (s != null && s.IsSolidPhase) Add(s.Id, CleanFormula(s.Id), s.Name ?? s.Id, n * k, !s.Id.Contains("OH"));
                    }
                    continue;
                }
                if (d.IsSolidPhase)
                {
                    bool amorphous = d.Id.Contains("(OH)") || d.Id.Contains("OH)") || d.Id == "H2SiO3" || d.Id.Contains("kt") && d.Id.Contains("OH") || d.Id == "ProteinDT";
                    Add(d.Id, CleanFormula(d.Id), string.IsNullOrEmpty(d.Name) ? d.Id : d.Name, n, !amorphous);
                    continue;
                }
                if (!evaporate) continue;
                if (Organic.TryGetValue(d.Id, out var org) && !AcidAnions.Contains(d.Id)) { Add("org:" + d.Id, org.formula, org.name, n, false, true); continue; }
                if (d.ChemStt > 0)
                {
                    if (d.HasTag("cacbohidrat") || d.MolarMass > 150) Add("org:" + d.Id, d.Formula ?? "C6H12O6", d.Name, n, false, true);
                    else notes?.Add($"{d.Name} bay hơi khi cô cạn");
                    continue;
                }
                if (d.Id == "C2H5OH" || d.Id == "Ac" || d.Id == "Fo") { notes?.Add($"{d.Name} bay hơi khi cô cạn"); continue; }
                ions[d.Id] = (ions.TryGetValue(d.Id, out var v) ? v : 0) + n;
            }
            if (!evaporate) return res;
            // kết tinh muối: ghép cation với anion theo thứ tự ưu tiên, theo đương lượng
            double Eq(string id, int z) => (ions.TryGetValue(id, out var v) ? v : 0) * z;
            foreach (var (cat, zc, an, za, id, formula, name) in Pairs)
            {
                double eq = Math.Min(Eq(cat, zc), Eq(an, za));
                if (eq <= 1e-12) continue;
                ions[cat] -= eq / zc;
                ions[an] -= eq / za;
                // số mol muối = đương lượng / (z_cation × số cation trong công thức)
                var atoms = Chem.Formula.Count(formula);
                string catEl = Regex.Match(cat, @"^[A-Z][a-z]?").Value;
                double nCat = atoms.TryGetValue(catEl, out var c) ? c : 1;
                Add(id, formula, name, eq / zc / nCat, true);
            }
            // axit hữu cơ khó bay hơi không ghép được với cation: còn lại dạng axit (vô định hình)
            foreach (var a in AcidAnions)
                if (ions.TryGetValue(a, out var left) && left > 1e-12 && Organic.TryGetValue(a, out var oa)) { Add("org:" + a, oa.formula, oa.name, left, false, true); ions[a] = 0; }
            foreach (var kv in ions)
                if (kv.Value > 1e-6 && (kv.Key == "Cl-" || kv.Key == "NO3-" || kv.Key == "CO3")) notes?.Add($"{kv.Key} dư (dạng axit) bay hơi khi cô cạn");
            return res;
        }

        static string CleanFormula(string id) => Regex.Replace(id, @"\((s|r|kt|l|than)\)$|kt$", "");

        public static double TotalMg(IEnumerable<Phase> ps) => ps.Sum(p => p.Mg);

        /// <summary>Tỉ lệ nguyên tử (không tính H, EDS không thấy H) của hỗn hợp pha theo khối lượng.</summary>
        public static Dictionary<string, double> AtomFractions(IEnumerable<Phase> ps)
        {
            var mol = new Dictionary<string, double>();
            foreach (var p in ps)
            {
                double M = Chem.Formula.MolarMass(p.Formula);
                if (M <= 0) continue;
                double n = p.Mg / M;
                foreach (var kv in p.Atoms) if (kv.Key != "H") mol[kv.Key] = (mol.TryGetValue(kv.Key, out var v) ? v : 0) + n * kv.Value;
            }
            double sum = mol.Values.Sum();
            return sum > 0 ? mol.ToDictionary(kv => kv.Key, kv => kv.Value / sum) : mol;
        }
    }

    /// <summary>
    /// Nhiễu xạ tia X dạng bột (data/may/tinh_chat/xrd.json): pic ở 2θ = 2·arcsin(λ/2d), cường độ theo thẻ chuẩn × phần khối lượng;
    /// độ rộng pic = độ rộng của máy ⊕ mở rộng do kích thước tinh thể (Scherrer); lệch độ cao mẫu làm pic dời góc;
    /// chất vô định hình cho bướu rộng. Tìm pha bằng so vị trí các vạch mạnh.
    /// </summary>
    public sealed class XrdLibrary
    {
        public sealed class PhaseCard
        {
            public string Id, Name, Formula, Cell, Shape, Source;
            public double GrainUm, CrystalNm;
            public readonly List<string> Ids = new List<string>();
            public readonly List<(double d, double i)> Lines = new List<(double, double)>();
        }

        public struct Comp { public PhaseCard Card; public double Weight, CrystalNm; }

        public readonly List<PhaseCard> Cards = new List<PhaseCard>();
        public double Lambda = 1.5406;
        public const double InstrumentFwhm = 0.08, GonioRadiusMm = 150;

        public PhaseCard Get(string speciesId) => Cards.FirstOrDefault(c => c.Ids.Contains(speciesId));
        public PhaseCard ById(string id) => Cards.FirstOrDefault(c => c.Id == id);

        public static XrdLibrary Load(string json)
        {
            var lib = new XrdLibrary();
            var r = MiniJson.Obj(MiniJson.Parse(json));
            lib.Lambda = MiniJson.N(r, "lambda", 1.5406);
            foreach (var o in MiniJson.A(r, "pha"))
            {
                var d = MiniJson.Obj(o);
                var c = new PhaseCard
                {
                    Id = MiniJson.S(d, "id"), Name = MiniJson.S(d, "ten"), Formula = MiniJson.S(d, "cong_thuc"), Cell = MiniJson.S(d, "o_mang"),
                    Shape = MiniJson.S(d, "hinh_dang"), Source = MiniJson.S(d, "nguon"), GrainUm = MiniJson.N(d, "co_hat_um", 10), CrystalNm = MiniJson.N(d, "tinh_the_nm", 100),
                };
                foreach (var s in MiniJson.A(d, "ids")) c.Ids.Add(s as string);
                foreach (var v in MiniJson.A(d, "vach")) { var a = MiniJson.Arr(v); c.Lines.Add(((double)a[0], (double)a[1])); }
                lib.Cards.Add(c);
            }
            return lib;
        }

        public double TwoTheta(double d) => d > Lambda / 2 ? 2 * Math.Asin(Lambda / (2 * d)) * 180 / Math.PI : double.NaN;
        public double D(double twoTheta) => Lambda / (2 * Math.Sin(twoTheta / 2 * Math.PI / 180));

        /// <summary>Độ rộng nửa chiều cao (độ 2θ): máy ⊕ Scherrer.</summary>
        public double Fwhm(double twoTheta, double crystalNm)
        {
            double th = twoTheta / 2 * Math.PI / 180;
            double beta = 0.9 * Lambda / 10 / (Math.Max(1, crystalNm) * Math.Cos(th)) * 180 / Math.PI;
            return Math.Sqrt(InstrumentFwhm * InstrumentFwhm + beta * beta);
        }

        /// <summary>Kích thước tinh thể (nm) từ độ rộng pic đo được (Scherrer, trừ độ rộng của máy).</summary>
        public double Scherrer(double twoTheta, double fwhmDeg)
        {
            double b2 = fwhmDeg * fwhmDeg - InstrumentFwhm * InstrumentFwhm;
            if (b2 <= 1e-8) return double.PositiveInfinity;
            double beta = Math.Sqrt(b2) * Math.PI / 180, th = twoTheta / 2 * Math.PI / 180;
            return 0.9 * Lambda / 10 / (beta * Math.Cos(th));
        }

        /// <summary>Độ dời góc (độ) khi mặt mẫu cao hơn mặt giá s (mm): Δ2θ = −2s·cosθ / R.</summary>
        public static double Displacement(double twoTheta, double sMm) => -2 * sMm * Math.Cos(twoTheta / 2 * Math.PI / 180) / GonioRadiusMm * 180 / Math.PI;

        /// <summary>Thành phần pha từ chất rắn: pha có thẻ chuẩn (theo khối lượng), phần vô định hình, pha tinh thể ngoài thư viện.</summary>
        public List<Comp> Compose(IList<SolidResidue.Phase> solids, out double amorphousFrac, List<string> unknown = null)
        {
            var res = new List<Comp>();
            double total = SolidResidue.TotalMg(solids);
            amorphousFrac = 0;
            if (total <= 0) return res;
            foreach (var s in solids)
            {
                double w = s.Mg / total;
                if (!s.Crystalline) { amorphousFrac += w; continue; }
                var card = Get(s.Id);
                if (card == null) { unknown?.Add(s.Name); continue; }
                int k = res.FindIndex(c => c.Card == card);
                if (k >= 0) { var c = res[k]; c.Weight += w; res[k] = c; }
                else res.Add(new Comp { Card = card, Weight = w, CrystalNm = card.CrystalNm });
            }
            return res;
        }

        static double PseudoVoigt(double x, double c, double fwhm)
        {
            double u = (x - c) / (fwhm / 2);
            return 0.5 * Math.Exp(-0.6931 * u * u) + 0.5 / (1 + u * u);
        }

        /// <summary>
        /// Cường độ (số đếm) ở 2θ. scale: số đếm của pic 100% khi mẫu chỉ có pha đó; displacementMm: lệch độ cao mặt mẫu;
        /// orient: hệ số nhân cường độ từng vạch (định hướng ưu tiên khi không nghiền), null = ngẫu nhiên đúng.
        /// </summary>
        public double Intensity(IList<Comp> comps, double amorphous, double twoTheta, double scale, double displacementMm, Func<PhaseCard, int, double> orient = null)
        {
            double y = 60 + 900 * Math.Exp(-twoTheta / 7.0);                        // nền: tán xạ không khí ở góc thấp
            y += amorphous * scale * 0.08 * Math.Exp(-0.5 * Math.Pow((twoTheta - 22) / 6.0, 2));  // bướu vô định hình
            foreach (var c in comps)
                for (int i = 0; i < c.Card.Lines.Count; i++)
                {
                    var (d, I) = c.Card.Lines[i];
                    double tt = TwoTheta(d);
                    if (double.IsNaN(tt)) continue;
                    tt += Displacement(tt, displacementMm);
                    double w = Fwhm(tt, c.CrystalNm);
                    if (Math.Abs(twoTheta - tt) > 12 * w) continue;
                    double area = I / 100.0 * c.Weight * (orient?.Invoke(c.Card, i) ?? 1);
                    y += scale * area * InstrumentFwhm / w * PseudoVoigt(twoTheta, tt, w);   // diện tích giữ nguyên khi pic rộng ra
                }
            return y;
        }

        /// <summary>Pic trong giản đồ (2θ, cường độ trên nền, độ rộng nửa chiều cao đo được).</summary>
        public static List<(double tt, double h, double fwhm)> Peaks(IList<(double tt, double y)> pat, double minHeight)
        {
            var res = new List<(double, double, double)>();
            int n = pat.Count;
            // làm trơn (trung bình trượt 5 điểm) để nhiễu thống kê không thành pic giả
            var s = new double[n];
            for (int i = 0; i < n; i++)
            {
                double sum = 0; int c = 0;
                for (int k = -2; k <= 2; k++) if (i + k >= 0 && i + k < n) { sum += pat[i + k].y; c++; }
                s[i] = sum / c;
            }
            for (int i = 3; i < n - 3; i++)
            {
                double y = s[i];
                bool max = true;
                for (int k = -3; k <= 3 && max; k++) if (k != 0 && s[i + k] > y) max = false;
                if (!max) continue;
                // nền cục bộ: trung bình hai bên ở ±(0,6 – 1,0)° (không lấy cực tiểu xa, vì bướu vô định hình làm nền dốc)
                double bl = 0, br = 0; int nl = 0, nr = 0;
                for (int k = i - 50; k <= i - 30; k++) if (k >= 0) { bl += s[k]; nl++; }
                for (int k = i + 30; k <= i + 50; k++) if (k < n) { br += s[k]; nr++; }
                double bg = nl > 0 && nr > 0 ? Math.Min(bl / nl, br / nr) : nl > 0 ? bl / nl : br / Math.Max(1, nr);
                double h = y - bg;
                if (h < minHeight || h < 6 * Math.Sqrt(Math.Max(1, bg)) / Math.Sqrt(5)) continue;
                double half = bg + h / 2;
                int l = i, r = i;
                while (l > 0 && s[l] > half) l--;
                while (r < n - 1 && s[r] > half) r++;
                double fw = pat[r].tt - pat[l].tt;
                double box = 5 * Math.Abs(pat[1].tt - pat[0].tt);
                fw = Math.Sqrt(Math.Max(fw * fw * 0.25, fw * fw - 0.462 * box * box));  // bỏ phần doãng do làm trơn
                if (fw > 1.5) continue;  // quá rộng: bướu vô định hình, không phải pic nhiễu xạ
                if (res.Count > 0 && Math.Abs(res[res.Count - 1].Item1 - pat[i].tt) < 0.1) continue;
                res.Add((pat[i].tt, h, fw));
            }
            return res;
        }

        /// <summary>
        /// Tìm pha: với mỗi thẻ, phần cường độ các vạch mạnh (≥ 15%) có pic đo trùng trong ±tol độ. Trả (thẻ, độ khớp 0..1, hệ số cường độ ước lượng).
        /// </summary>
        public List<(PhaseCard card, double fom, double scale)> Match(IList<(double tt, double h, double fwhm)> peaks, double tol = 0.2)
        {
            var res = new List<(PhaseCard, double, double)>();
            foreach (var c in Cards)
            {
                double tot = 0, hit = 0, sc = 0, scw = 0;
                bool strongest = false;
                foreach (var (d, I) in c.Lines)
                {
                    double tt = TwoTheta(d);
                    if (double.IsNaN(tt) || tt > 80 || tt < 10 || I < 8) continue;
                    tot += I;
                    var p = peaks.Where(q => Math.Abs(q.tt - tt) <= tol).OrderBy(q => Math.Abs(q.tt - tt)).FirstOrDefault();
                    if (p.h > 0) { hit += I; sc += p.h / I * 100 * I; scw += I; if (I >= 100) strongest = true; }
                }
                if (tot <= 0) continue;
                // vạch mạnh nhất (100%) của thẻ phải có pic tương ứng; không có thì pha đó gần như chắc chắn vắng mặt
                res.Add((c, strongest ? hit / tot : hit / tot * 0.5, scw > 0 ? sc / scw : 0));
            }
            return res.OrderByDescending(x => x.Item2).ThenByDescending(x => x.Item3).ToList();
        }
    }

    /// <summary>
    /// Phổ EDS (data/may/tinh_chat/eds.json): pic Gauss tại năng lượng vạch đặc trưng, độ rộng theo năng lượng (≈ 130 eV ở 5,9 keV),
    /// cường độ ∝ phần nguyên tử × độ nhạy × hệ số quá áp (U − 1)^1,67 (U = E₀ / E_kích thích); nền bức xạ hãm (Kramers).
    /// </summary>
    public sealed class EdsLibrary
    {
        public sealed class Line { public string El, Name; public int Z; public double KeV, Sens; }
        public readonly List<Line> Lines = new List<Line>();
        public string Source = "";

        public Line Get(string el) => Lines.FirstOrDefault(l => l.El == el);

        public static EdsLibrary Load(string json)
        {
            var lib = new EdsLibrary();
            var r = MiniJson.Obj(MiniJson.Parse(json));
            lib.Source = MiniJson.S(r, "nguon");
            foreach (var o in MiniJson.A(r, "nguyen_to"))
            {
                var d = MiniJson.Obj(o);
                lib.Lines.Add(new Line { El = MiniJson.S(d, "ky_hieu"), Name = MiniJson.S(d, "vach"), Z = (int)MiniJson.N(d, "Z"), KeV = MiniJson.N(d, "keV"), Sens = MiniJson.N(d, "nhay", 1) });
            }
            return lib;
        }

        /// <summary>Hệ số quá áp: không kích thích được khi E₀ nhỏ hơn năng lượng ngưỡng (lấy ≈ 1,1 × năng lượng vạch).</summary>
        public static double Overvoltage(double e0, double lineKeV)
        {
            double u = e0 / (lineKeV * 1.1);
            return u <= 1 ? 0 : Math.Pow(u - 1, 1.67) / Math.Pow(u, 1.0);
        }

        /// <summary>Phổ (số đếm theo kênh 10 eV, 0 – 10 keV). fractions: phần nguyên tử; coated: phủ vàng; tape: thấy băng dính cacbon.</summary>
        public double[] Spectrum(IDictionary<string, double> fractions, double e0, bool coated, bool tape, double liveCounts, Random rng)
        {
            const int n = 1000;
            var y = new double[n];
            var fr = new Dictionary<string, double>(fractions);
            if (coated) fr["Au"] = (fr.TryGetValue("Au", out var a) ? a : 0) + 0.12;
            if (tape) fr["C"] = (fr.TryGetValue("C", out var c) ? c : 0) + 0.06;
            double zbar = fr.Sum(kv => (Get(kv.Key)?.Z ?? 10) * kv.Value) / Math.Max(1e-9, fr.Values.Sum());
            for (int i = 1; i < n; i++)
            {
                double e = (i + 0.5) * 0.01;
                if (e >= e0) break;
                double brem = 4e-5 * zbar * (e0 - e) / e;  // tỉ lệ pic / nền cỡ 30 – 100 như đầu dò thật
                brem *= 1 - Math.Exp(-Math.Pow(e / 0.9, 3));  // hấp thụ năng lượng thấp ở cửa sổ đầu dò
                y[i] += brem;
            }
            foreach (var kv in fr)
            {
                var l = Get(kv.Key);
                if (l == null) continue;
                double amp = kv.Value * l.Sens * Overvoltage(e0, l.KeV);
                if (amp <= 0) continue;
                double s = LineWidth(l.KeV) / 2.355;
                for (int i = 0; i < n; i++)
                {
                    double e = (i + 0.5) * 0.01;
                    y[i] += amp * Math.Exp(-0.5 * Math.Pow((e - l.KeV) / s, 2)) / (s * 100);
                }
            }
            double sum = y.Sum();
            for (int i = 0; i < n; i++)
            {
                double mean = y[i] / Math.Max(1e-12, sum) * liveCounts;
                y[i] = Math.Max(0, mean + Math.Sqrt(Math.Max(mean, 1e-6)) * Gauss(rng));
            }
            return y;
        }

        /// <summary>Độ rộng nửa chiều cao (keV) của pic EDS: ≈ 130 eV ở Mn Kα 5,9 keV (đầu dò silic trôi).</summary>
        public static double LineWidth(double keV) => Math.Sqrt(0.06 * 0.06 + 0.0019 * keV);

        /// <summary>
        /// Định lượng không chuẩn: khớp phổ = nền bức xạ hãm + Σ pic Gauss của mọi vạch kích thích được (bình phương tối thiểu không âm,
        /// tách được các pic chồng nhau như Au Mα 2,12 / S Kα 2,31 keV); diện tích / (độ nhạy × quá áp), chuẩn hóa thành % nguyên tử.
        /// Pic nhỏ hơn 4 lần sai số thống kê thì coi như không có. skip: nguyên tố bỏ qua khi tính % (Au lớp phủ, C băng dính).
        /// </summary>
        public Dictionary<string, double> Quantify(double[] spec, double e0, ICollection<string> skip)
        {
            int lo = 20, hi = Math.Min(spec.Length, (int)(e0 / 0.01) - 5);
            var idx = Enumerable.Range(lo, Math.Max(0, hi - lo)).ToList();
            var lines = Lines.Where(l => Overvoltage(e0, l.KeV) > 0 && l.KeV / 0.01 < hi).ToList();
            double[] Col(int k)
            {
                if (k == 0) return idx.Select(i => { double e = (i + 0.5) * 0.01; return (e0 - e) / e * (1 - Math.Exp(-Math.Pow(e / 0.9, 3))); }).ToArray();
                if (k == 1) return idx.Select(i => 1.0).ToArray();
                var l = lines[k - 2];
                double s = LineWidth(l.KeV) / 2.355;
                return idx.Select(i => Math.Exp(-0.5 * Math.Pow(((i + 0.5) * 0.01 - l.KeV) / s, 2)) / (s * 100)).ToArray();
            }
            var x = LeastSquares.NonNegative(lines.Count + 2, Col, idx.Select(i => spec[i]).ToArray(), out _);
            var raw = new Dictionary<string, double>();
            var bgCol = Col(0);
            for (int k = 0; k < lines.Count; k++)
            {
                var l = lines[k];
                if (skip != null && skip.Contains(l.El)) continue;
                double area = x[k + 2];
                int c = (int)(l.KeV / 0.01) - lo;
                double bgHere = c >= 0 && c < bgCol.Length ? x[0] * bgCol[c] + x[1] : 0;
                double width = LineWidth(l.KeV) / 0.01;
                if (area > 4 * Math.Sqrt(Math.Max(1, (bgHere + area / width) * width))) raw[l.El] = area / (l.Sens * Overvoltage(e0, l.KeV));
            }
            double sum = raw.Values.Sum();
            return sum > 0 ? raw.ToDictionary(kv => kv.Key, kv => kv.Value / sum) : raw;
        }

        static double Gauss(Random r)
        {
            double u1 = 1 - r.NextDouble(), u2 = r.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }
    }
}
