using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Các phản ứng THPT còn lại theo nhóm quy luật:
    ///   - axit đặc (H₂SO₄, HNO₃) với halogenua, phi kim (S, C, P); nước cường toan với vàng;
    ///   - phức, màu đặc trưng (iot - hồ tinh bột, CrO₅), iođofom;
    ///   - phản ứng chậm theo thời gian: muối cacbonat / sunfua trong axit yếu, đun nước cứng tạm thời, NH₄⁺ + NO₂⁻,
    ///     thủy phân urê, Si / S trong kiềm, SiO₂ + HF, CuO + ancol;
    ///   - hữu cơ: este hóa, thủy phân este trong axit, thủy phân đường, nitro hóa và brom hóa benzen, Na + ancol.
    /// Phương trình dựng từ đúng công thức chất đang có trong bình rồi đi qua bộ cân bằng.
    /// </summary>
    public static partial class ChemEngine
    {
        /// <summary>Tra loài hữu cơ trong danh mục theo công thức ("CH3COOC2H5") hoặc theo tên ("ten:glucoz"). ChemMake gán.</summary>
        public static Func<string, SpeciesDef> FindOrganic;

        // ------------------------------------------------------------------
        // Công thức hữu cơ
        // ------------------------------------------------------------------

        static readonly Dictionary<string, string> BuiltinAcidFormula = new Dictionary<string, string>
        {
            ["Ac"] = "CH3COOH", ["Fo"] = "HCOOH", ["Bz"] = "C6H5COOH", ["Lac"] = "CH3CH(OH)COOH",
        };

        static string Ascii(SpeciesDef d)
        {
            if (d == null) return null;
            if (BuiltinAcidFormula.TryGetValue(d.Id, out var f)) return f;
            return AsciiOf(d.Formula);
        }

        static string Hill(Dictionary<string, double> c)
        {
            var sb = new System.Text.StringBuilder();
            void One(string el) { if (c.TryGetValue(el, out var v) && v > 0) { sb.Append(el); if (v != 1) sb.Append((int)v); } }
            One("C"); One("H");
            var rest = new List<string>(c.Keys);
            rest.Sort(StringComparer.Ordinal);
            foreach (var el in rest) if (el != "C" && el != "H") One(el);
            return sb.ToString();
        }

        static string Shift(string formula, params (string el, int n)[] delta)
        {
            var c = Equation.Parse(formula, out _);
            if (c == null) return null;
            foreach (var (el, n) in delta) c[el] = (c.TryGetValue(el, out var v) ? v : 0) + n;
            return Hill(c);
        }

        /// <summary>Số liên kết π cộng được Br₂ (anken 1, ankin và đien 2).</summary>
        static int PiBonds(SpeciesDef d)
        {
            string n = (d.Name ?? "").ToLowerInvariant();
            if (d.HasTag("ank_1_in") || n.Contains("axetilen") || n.Contains("propin") || n.Contains("butadien") || n.Contains("isopren")) return 2;
            return 1;
        }

        static SpeciesDef MainTagged(MixBuilder b, string tag)
        {
            SpeciesDef best = null; double n = 0;
            foreach (int s in b.Indices)
            {
                var d = SpeciesRegistry.At(s);
                if (d.HasTag(tag) && d.Kind != SpeciesKind.Lump && b.Get(s) > n) { n = b.Get(s); best = d; }
            }
            return best;
        }

        /// <summary>Phương trình riêng cho chất hữu cơ cụ thể trong các luật oxi hóa - khử theo nhãn ("@khong_no", "@andehit", "@ancol").</summary>
        static string OrganicEquation(Rx r, SpeciesDef d)
        {
            string F = Ascii(d);
            if (F == null || Equation.Parse(F, out _) == null) return null;
            if (r.Ox == "Br2" && r.Red == "@khong_no")
            {
                int k = PiBonds(d);
                return Equation.Balance(new[] { F, "Br2" }, new[] { Shift(F, ("Br", 2 * k)) });
            }
            if (r.Ox == "MnO4-" && r.Red == "@khong_no" && r.PhMin >= 4 && PiBonds(d) == 1)
                return Equation.Balance(new[] { F, "KMnO4", "H2O" }, new[] { Shift(F, ("H", 2), ("O", 2)), "MnO2", "KOH" });
            if (r.Ox == "Br2" && r.Red == "@andehit")
                return Equation.Balance(new[] { F, "Br2", "H2O" }, new[] { AcidOf(F), "HBr" });
            if (r.Ox == "CrVI" && r.Red == "@ancol")
            {
                string p = AldehydeOf(F);
                return p == null ? null : Equation.Balance(new[] { "Cr2O7^2-", F, "H+" }, new[] { "Cr^3+", p, "H2O" });
            }
            return null;
        }

        static string AcidOf(string aldehyde)
        {
            if (aldehyde == "HCHO") return "HCOOH";
            int i = aldehyde.LastIndexOf("CHO", StringComparison.Ordinal);
            if (i >= 0 && !aldehyde.EndsWith("COCH3")) return aldehyde.Substring(0, i) + "COOH" + aldehyde.Substring(i + 3);
            return Shift(aldehyde, ("O", 1));
        }

        static string AldehydeOf(string alcohol)
        {
            switch (alcohol)
            {
                case "CH3OH": return "HCHO";
                case "C2H5OH": return "CH3CHO";
                case "C3H7OH": return "C2H5CHO";
                case "C4H9OH": return "C3H7CHO";
                case "(CH3)2CHOH": return "CH3COCH3";
                case "C6H5CH2OH": return "C6H5CHO";
            }
            return null;
        }

        static string TollensEquation(string F)
        {
            if (F == "HCHO") return "HCHO + 4AgNO₃ + 6NH₃ + 2H₂O → (NH₄)₂CO₃ + 4Ag↓ + 4NH₄NO₃";
            if (F == "HCOOH") return "HCOOH + 2AgNO₃ + 4NH₃ + H₂O → (NH₄)₂CO₃ + 2Ag↓ + 2NH₄NO₃";
            string salt;
            int i = F.LastIndexOf("CHO", StringComparison.Ordinal);
            if (i >= 0) salt = F.Substring(0, i) + "COONH4" + F.Substring(i + 3);
            else salt = Shift(F, ("O", 1), ("N", 1), ("H", 3));
            if (salt == null) return null;
            string eq = $"{F} + 2AgNO3 + 3NH3 + H2O → {salt} + 2Ag + 2NH4NO3";
            return Equation.Check(eq) == null ? Pretty(eq) : null;
        }

        static string FehlingEquation(string F)
        {
            string salt;
            int i = F.LastIndexOf("CHO", StringComparison.Ordinal);
            if (i >= 0) salt = F.Substring(0, i) + "COONa" + F.Substring(i + 3);
            else salt = Shift(F, ("O", 1), ("H", -1), ("Na", 1));
            if (salt == null) return null;
            string eq = $"{F} + 2Cu(OH)2 + NaOH → {salt} + Cu2O + 3H2O";
            return Equation.Check(eq) == null ? Pretty(eq).Replace("Cu₂O", "Cu₂O↓") : null;
        }

        static string Pretty(string eq)
        {
            var parts = eq.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (p == "+" || p == "→" || p == "⇌") continue;
                int k = 0;
                while (k < p.Length && char.IsDigit(p[k])) k++;
                parts[i] = p.Substring(0, k) + Equation.Pretty(p.Substring(k));
            }
            return string.Join(" ", parts);
        }

        /// <summary>Tách este RCOOR' thành axit RCOOH, ancol R'OH và số nhóm este (chất béo: 3).</summary>
        static bool SplitEster(string F, out string acid, out string alcohol, out int n, out string salt)
        {
            acid = alcohol = salt = null; n = 1;
            var m = Regex.Match(F, @"^\((.+)COO\)3C3H5$");
            if (m.Success) { acid = m.Groups[1].Value + "COOH"; salt = m.Groups[1].Value + "COONa"; alcohol = "C3H5(OH)3"; n = 3; return true; }
            int i = F.IndexOf("COO", StringComparison.Ordinal);
            if (i < 0) return false;
            string r1 = F.Substring(0, i), r2 = F.Substring(i + 3);
            if (r2.Length == 0 || r2.StartsWith("H")) return false;
            acid = (r1.Length == 0 ? "H" : r1) + "COOH";
            salt = (r1.Length == 0 ? "H" : r1) + "COONa";
            alcohol = r2 + "OH";
            return true;
        }

        public static string SaponificationEquation(SpeciesDef d)
        {
            string F = Ascii(d);
            if (F == null || !SplitEster(F, out _, out var alc, out int n, out var salt)) return null;
            return Equation.Balance(new[] { F, "NaOH" }, new[] { salt, alc });
        }

        // ------------------------------------------------------------------
        // Luật tức thời (chạy trong cân bằng)
        // ------------------------------------------------------------------

        /// <summary>Chạy trước bảng oxi hóa - khử (các dấu hiệu trung gian bị phản ứng chính làm mất).</summary>
        static bool ThptEarly(Work w)
        {
            bool changed = false;
            // K₂Cr₂O₇ + H₂O₂ (H⁺): màu xanh lam thẫm CrO₅ xuất hiện rồi chuyển xanh lục Cr³⁺
            if (w.M("CrVI") > 1e-9 && w.M("H2O2") > 1e-9 && w.Ph < 3)
                Emit(w, EventKind.Color, "phuc:CrO5", "Thoáng xuất hiện màu xanh lam thẫm (CrO₅), sau đó chuyển xanh lục", "CrO₅ không bền, phân hủy nhanh trong nước; chiết bằng ete thì giữ được màu xanh lâu hơn.",
                     "Cr₂O₇²⁻ + 4H₂O₂ + 2H⁺ → 2CrO₅ + 5H₂O", color: Rgb.Hex("#1E3C9A"));
            // Iođofom: hợp chất có nhóm CH₃CO- (hoặc CH₃CH(OH)-) + I₂ trong kiềm (trước khi I₂ tự oxi hóa - khử trong kiềm)
            if (w.M("I2") > 1e-9 && w.Ph > 10)
            {
                foreach (int s in w.b.Snapshot())
                {
                    var d = SpeciesRegistry.At(s);
                    string F = Ascii(d);
                    string eq = null;
                    switch (F)
                    {
                        case "(CH3)2CO": eq = "CH₃COCH₃ + 3I₂ + 4NaOH → CHI₃↓ + CH₃COONa + 3NaI + 3H₂O"; break;
                        case "C2H5OH": eq = "C₂H₅OH + 4I₂ + 6NaOH → CHI₃↓ + HCOONa + 5NaI + 5H₂O"; break;
                        case "CH3CHO": eq = "CH₃CHO + 3I₂ + 4NaOH → CHI₃↓ + HCOONa + 3NaI + 3H₂O"; break;
                        case "C2H5COCH3": case "C6H5COCH3": case "(CH3)2CHOH": case "C3H7COCH3": eq = null; break;
                        default: continue;
                    }
                    int iNeed = F == "C2H5OH" ? 4 : 3;
                    double x = Math.Min(w.b.Get(s), w.M("I2") / iNeed);
                    if (x <= 1e-10) continue;
                    w.b.Add(s, -x);
                    w.b.Add("I2", -iNeed * x);
                    w.b.Add("I-", (2 * iNeed - 3) * x);
                    w.b.Add("CHI3", x);
                    Emit(w, EventKind.Precipitate, "kt:CHI3", "Kết tủa vàng iođofom CHI₃, mùi đặc trưng", "Phản ứng iođofom nhận biết nhóm CH₃-CO- (axeton, anđehit axetic) và CH₃-CH(OH)- (etanol).",
                         eq ?? "R-CO-CH₃ + 3I₂ + 4NaOH → CHI₃↓ + RCOONa + 3NaI + 3H₂O", amount: x, color: Rgb.Hex("#F2D23C"));
                    changed = true;
                }
            }
            return changed;
        }

        /// <summary>Chạy sau bảng oxi hóa - khử.</summary>
        static bool ThptInstant(Work w)
        {
            bool changed = false;
            var b = w.b;
            // Iot + hồ tinh bột: xanh tím; đun nóng mất màu, để nguội hiện lại
            if (w.M("I2") > 1e-8 && (w.M("starch") > 0 || TagAmount(b, "tinh_bot") > 1e-9))
            {
                if (w.T < 65) Emit(w, EventKind.Color, "phuc:I2tb", "Dung dịch hóa xanh tím (iot + hồ tinh bột)", "Nhận biết iot bằng hồ tinh bột và ngược lại; phân tử I₂ nằm trong lòng xoắn amilozơ.", color: Rgb.Hex("#14246A"));
                else Emit(w, EventKind.Color, "mat_mau:I2tb", "Đun nóng: dung dịch mất màu xanh tím (để nguội màu hiện lại)", "Khi nóng, mạch amilozơ duỗi ra, không giữ được phân tử I₂.");
            }
            return changed;
        }

        // ------------------------------------------------------------------
        // Luật theo thời gian (chạy trong Step)
        // ------------------------------------------------------------------

        static readonly Dictionary<string, (string cation, int k, string gas, string anion)> AcidSoluble = new Dictionary<string, (string, int, string, string)>
        {
            ["CaCO3"] = ("Ca2+", 1, "CO2", "CO3"), ["MgCO3"] = ("Mg2+", 1, "CO2", "CO3"), ["BaCO3"] = ("Ba2+", 1, "CO2", "CO3"),
            ["SrCO3"] = ("Sr2+", 1, "CO2", "CO3"), ["FeCO3"] = ("Fe2+", 1, "CO2", "CO3"), ["ZnCO3"] = ("Zn2+", 1, "CO2", "CO3"),
            ["CuCO3"] = ("Cu2+", 1, "CO2", "CO3"), ["PbCO3"] = ("Pb2+", 1, "CO2", "CO3"), ["MnCO3"] = ("Mn2+", 1, "CO2", "CO3"),
            ["FeS"] = ("Fe2+", 1, "H2S", "S"), ["ZnS"] = ("Zn2+", 1, "H2S", "S"), ["MnS"] = ("Mn2+", 1, "H2S", "S"),
            ["CaSO3"] = ("Ca2+", 1, "SO2", "SO3"), ["BaSO3"] = ("Ba2+", 1, "SO2", "SO3"),
        };

        /// <summary>Lượng proton còn cho được (mol): H⁺ tự do + proton của các axit yếu có pKa nhỏ hơn 5.</summary>
        static double AcidCapacity(Work w)
        {
            double cap = w.h * w.V;
            foreach (int s in w.b.Indices)
            {
                var d = SpeciesRegistry.At(s);
                if (d.Kind != SpeciesKind.Weak || d.Ka == null || d.Ka.Length == 0 || d.Ka[0] < 1e-5 || d.Charge == 1) continue;
                if (d.Id == "CO3" || d.Id == "S" || d.Id == "SO3") continue;
                double bound = WeakCharge(d, w.h) - (d.Charge - d.Ka.Length);
                cap += w.b.Get(s) * Math.Min(bound, 1);
            }
            return cap;
        }

        static bool ThptStep(Work w, double dt, ChemEnv env)
        {
            var b = w.b;
            bool changed = false;
            double T = env.TempC;
            // 1. Muối cacbonat / sunfua / sunfit không tan trong axit (cả axit yếu như giấm): tan dần, sủi khí ngay
            if (w.Ph < 6)
            {
                foreach (int s in b.Snapshot())
                {
                    var d = SpeciesRegistry.At(s);
                    if (d.Kind != SpeciesKind.Precipitate || !AcidSoluble.TryGetValue(d.Id, out var a)) continue;
                    double n = b.Get(s);
                    double cap = AcidCapacity(w);
                    double dn = Math.Min(n, Math.Min(cap / 2, (1.5e-3 + n * 0.05) * dt));
                    if (dn <= 1e-10) continue;
                    b.Add(s, -dn);
                    b.Add(a.cation, dn);
                    var cat = SpeciesRegistry.Get(a.cation);
                    string eq = Equation.BalanceAuto(new[] { d.Formula, "H+" }, new[] { cat.Formula, a.gas }, true);
                    if (eq != null) eq = eq.Replace(a.gas == "CO2" ? "CO₂" : a.gas == "SO2" ? "SO₂" : "H₂S", (a.gas == "CO2" ? "CO₂" : a.gas == "SO2" ? "SO₂" : "H₂S") + "↑");
                    Emit(w, EventKind.Gas, "khi:" + a.gas, a.gas == "CO2" ? "Sủi bọt khí không màu (CO₂), chất rắn tan dần" : a.gas == "H2S" ? "Khí H₂S mùi trứng thối thoát ra, chất rắn tan dần" : "Khí SO₂ mùi hắc thoát ra",
                         a.gas == "CO2" ? "Muối cacbonat tác dụng với axit (kể cả axit yếu như giấm) giải phóng CO₂." : "Muối của axit yếu, dễ bay hơi bị axit mạnh hơn đẩy ra.",
                         eq, a.gas == "CO2" ? Severity.Info : Severity.Warning, a.gas == "CO2" ? null : "khi_doc", a.gas, dn);
                    changed = true;
                }
            }
            // 2. Đun sôi: CO₂ hòa tan bay ra (nước cứng tạm thời đóng cặn CaCO₃)
            if (T > 80)
            {
                int co3 = SpeciesRegistry.IndexOf("CO3");
                double tot = b.Get(co3);
                if (tot > 1e-9)
                {
                    double free = tot * AlphaFirst(SpeciesRegistry.At(co3), w.h) + tot * AlphaK(SpeciesRegistry.At(co3), w.h, 1) * 0.5;
                    double dn = Math.Min(tot, free * (1 - Math.Exp(-0.08 * dt)));
                    if (dn > 1e-9)
                    {
                        b.Add(co3, -dn);
                        Emit(w, EventKind.Gas, "khi:CO2:dun", "Đun sôi: khí CO₂ thoát ra", "Đun nóng làm CO₂ hòa tan bay ra; với nước cứng tạm thời thì CaCO₃ kết tủa (cặn ấm đun nước).",
                             "Ca(HCO₃)₂ → CaCO₃↓ + CO₂↑ + H₂O (t°)", gas: "CO2", amount: dn);
                        changed = true;
                    }
                }
            }
            // 3. NH₄⁺ + NO₂⁻ đun nóng: khí N₂
            if (T > 60 && b.Get("NH3") > 1e-9 && b.Get("NO2") > 1e-9)
            {
                double nh4 = b.Get("NH3") * AlphaFirst(SpeciesRegistry.Get("NH3"), w.h);
                double dn = Math.Min(Math.Min(nh4, b.Get("NO2")), Math.Min(nh4, b.Get("NO2")) * (1 - Math.Exp(-0.1 * dt)) + 1e-7 * dt);
                if (dn > 1e-10)
                {
                    b.Add("NH3", -dn); b.Add("NO2", -dn);
                    Emit(w, EventKind.Gas, "khi:N2:nitrit", "Sủi bọt khí không màu N₂", "Điều chế nitơ trong phòng thí nghiệm: đun dung dịch bão hòa NH₄Cl và NaNO₂.", "NH₄⁺ + NO₂⁻ → N₂↑ + 2H₂O",
                         gas: "N2", amount: dn);
                    changed = true;
                }
            }
            // 4. Urê thủy phân trong kiềm khi đun: NH₃
            if (T > 60 && w.Ph > 11 && b.Get("ure") > 1e-9)
            {
                double dn = b.Get("ure") * (1 - Math.Exp(-0.05 * dt));
                b.Add("ure", -dn); b.Add("NH3", 2 * dn); b.Add("CO3", dn);
                Emit(w, EventKind.Reaction, "thuyphan:ure", "Urê bị thủy phân, có mùi khai NH₃", "Nhận biết phân urê: đun với dung dịch kiềm có khí NH₃ làm quỳ ẩm hóa xanh.",
                     "(NH₂)₂CO + 2OH⁻ → 2NH₃ + CO₃²⁻", amount: dn);
                changed = true;
            }
            // 5. Chất rắn (phi kim, Si, SiO₂, Au, CuO) với axit đặc / kiềm / ancol
            void Solid(string id, double rate, Action<double> apply, string key, string title, string detail, string eq, string gas, double gasPer, Severity sev = Severity.Warning, string hazard = "khi_doc")
            {
                double n = b.Get(id);
                if (n <= 1e-10) return;
                double dn = Math.Min(n, rate * dt);
                b.Add(id, -dn);
                apply(dn);
                Emit(w, gas != null ? EventKind.Gas : EventKind.Reaction, key, title, detail, eq, sev, hazard, gas, dn * gasPer);
                changed = true;
            }
            if (w.C("F") > 1 && w.Ph < 4)
                Solid("SiO2(s)", 3e-4, x => { b.Add("F", -6 * x); b.Add("SiF62-", x); }, "tan:SiO2", "SiO₂ (thủy tinh) bị ăn mòn, tan dần trong HF", "HF ăn mòn thủy tinh: dùng để khắc chữ lên thủy tinh; không đựng HF trong bình thủy tinh.",
                      "SiO₂ + 6HF → H₂SiF₆ + 2H₂O", null, 0, Severity.Warning, "an_mon");
            if (T > 60 && b.Get("CuO(s)") > 1e-9)
            {
                var alc = MainTagged(b, "ancol");
                string F = Ascii(alc), P = F != null ? AldehydeOf(F) : null;
                if (alc != null && P != null)
                {
                    var prod = FindOrganic?.Invoke(P);
                    Solid("CuO(s)", 2e-4, x => { b.Take(alc.Index, x); b.Add("Cu(s)", x); if (prod != null) b.Add(prod.Index, x); }, "cuo_ancol", "CuO đen chuyển thành đồng màu đỏ, có mùi anđehit",
                          "Ancol bậc một bị CuO oxi hóa thành anđehit khi đun nóng.", Equation.Balance(new[] { F, "CuO" }, new[] { P, "Cu", "H2O" }), null, 0, Severity.Info, null);
                }
            }
            // 6. Hữu cơ theo thời gian
            if (T > 60) changed |= OrganicSlow(w, dt, T);
            return changed;
        }

        static bool OrganicSlow(Work w, double dt, double T)
        {
            var b = w.b;
            bool changed = false;
            // Este hóa: axit cacboxylic + ancol, H₂SO₄ đặc xúc tác, đun
            if (w.C("SO4") > 3)
            {
                SpeciesDef acid = null, alc = MainTagged(b, "ancol");
                foreach (int s in b.Indices)
                {
                    var d = SpeciesRegistry.At(s);
                    string f = Ascii(d);
                    if (d.Kind == SpeciesKind.Weak && f != null && f.EndsWith("COOH") && b.Get(s) > 1e-9 && (acid == null || b.Get(s) > b.Get(acid.Index))) acid = d;
                }
                if (acid != null && alc != null && !alc.HasTag("poliol"))
                {
                    string fa = Ascii(acid), fo = Ascii(alc);
                    string ester = fa.Substring(0, fa.Length - 4) + "COO" + fo.Substring(0, fo.Length - 2);
                    if (fa == "HCOOH") ester = "HCOO" + fo.Substring(0, fo.Length - 2);
                    var es = FindOrganic?.Invoke(ester);
                    double na = b.Get(acid.Index), no = b.Get(alc.Index), ne = es != null ? b.Get(es.Index) : 0;
                    double lim = Math.Min(na, no);
                    if (lim > 1e-9 && ne < 2 * lim)   // cân bằng: hiệu suất ≈ 2/3
                    {
                        double dn = Math.Min(lim, lim * (1 - Math.Exp(-0.03 * dt)));
                        b.Add(acid.Index, -dn); b.Add(alc.Index, -dn);
                        if (es != null) b.Add(es.Index, dn);
                        Emit(w, EventKind.Reaction, "este_hoa", "Este hóa: chất lỏng nhẹ, mùi thơm tách thành lớp nổi trên mặt", "Phản ứng thuận nghịch, cần H₂SO₄ đặc xúc tác và đun nóng; hiệu suất khoảng 2/3.",
                             Equation.Balance(new[] { fa, fo }, new[] { ester, "H2O" }, "⇌") is string eq ? eq + " (H₂SO₄ đặc, t°)" : null, amount: dn);
                        changed = true;
                    }
                }
            }
            // Thủy phân este trong axit (thuận nghịch), đường và tinh bột trong axit
            if (w.Ph < 2)
            {
                foreach (int s in b.Snapshot())
                {
                    var d = SpeciesRegistry.At(s);
                    double n = b.Get(s);
                    if (n <= 1e-10 || d.Kind == SpeciesKind.Lump) continue;
                    string F = Ascii(d);
                    if (d.HasTag("este") && !d.HasTag("chat_beo") && w.C("SO4") < 10 && F != null && SplitEster(F, out var acidF, out var alcF, out _, out _))
                    {
                        var pa = FindOrganic?.Invoke(acidF); var po = FindOrganic?.Invoke(alcF);
                        double dn = n * (1 - Math.Exp(-0.01 * dt));
                        b.Add(s, -dn);
                        if (pa != null) b.Add(pa.Index, dn); else if (acidF == "CH3COOH") b.Add("Ac", dn);
                        if (po != null) b.Add(po.Index, dn);
                        Emit(w, EventKind.Reaction, "thuyphan:este", "Este bị thủy phân trong axit: lớp este mỏng dần (phản ứng thuận nghịch)", "Thủy phân este trong môi trường axit là phản ứng thuận nghịch; trong kiềm là một chiều (xà phòng hóa).",
                             Equation.Balance(new[] { F, "H2O" }, new[] { acidF, alcF }, "⇌") is string e1 ? e1 + " (H⁺, t°)" : null, amount: dn);
                        changed = true;
                    }
                    else if (T > 80 && (d.HasTag("tinh_bot") || (d.Name ?? "").Contains("saccaroz") || (d.Name ?? "").Contains("mantoz")))
                    {
                        bool sac = (d.Name ?? "").Contains("saccaroz");
                        var glu = FindOrganic?.Invoke("ten:glucoz"); var fru = FindOrganic?.Invoke("ten:fructoz");
                        double dn = n * (1 - Math.Exp(-0.03 * dt));
                        b.Add(s, -dn);
                        int k = d.HasTag("tinh_bot") ? 1 : 2;
                        if (glu != null) b.Add(glu.Index, (sac ? 1 : k) * dn);
                        if (sac && fru != null) b.Add(fru.Index, dn);
                        Emit(w, EventKind.Reaction, "thuyphan:duong", d.HasTag("tinh_bot") ? "Tinh bột bị thủy phân thành glucozơ (dung dịch sau đó tráng bạc được)" : "Saccarozơ bị thủy phân thành glucozơ và fructozơ",
                             "Đun nóng với axit loãng (hoặc enzim) làm đứt liên kết glicozit.",
                             sac ? "C₁₂H₂₂O₁₁ + H₂O → C₆H₁₂O₆ (glucozơ) + C₆H₁₂O₆ (fructozơ)" : d.HasTag("tinh_bot") ? "(C₆H₁₀O₅)ₙ + nH₂O → nC₆H₁₂O₆" : "C₁₂H₂₂O₁₁ + H₂O → 2C₆H₁₂O₆", amount: dn);
                        changed = true;
                    }
                }
            }
            return changed;
        }

        /// <summary>Benzen: nitro hóa (HNO₃ đặc + H₂SO₄ đặc, đun) và brom hóa (Br₂ lỏng, bột Fe).</summary>
        static bool Aromatic(Work w, double dt, double T)
        {
            var b = w.b;
            bool changed = false;
            SpeciesDef bz = null;
            foreach (int s in b.Indices) { var d = SpeciesRegistry.At(s); if (Ascii(d) == "C6H6" && d.ChemStt > 0) bz = d; }
            if (bz == null || b.Get(bz.Index) <= 1e-9) return false;
            if (T > 45 && w.C("NO3-") > 3 && w.C("SO4") > 5)
            {
                var nb = FindOrganic?.Invoke("C6H5NO2");
                double dn = Math.Min(b.Get(bz.Index), Math.Min(b.Get("NO3-"), b.Get(bz.Index) * (1 - Math.Exp(-0.04 * dt)) + 1e-7));
                b.Add(bz.Index, -dn); b.Add("NO3-", -dn);
                if (nb != null) b.Add(nb.Index, dn);
                Emit(w, EventKind.Reaction, "nitro_hoa", "Chất lỏng màu vàng nhạt, nặng hơn nước, mùi hạnh nhân (nitrobenzen) tách ra", "Nitro hóa benzen bằng hỗn hợp HNO₃ đặc và H₂SO₄ đặc (xúc tác, hút nước).",
                     "C₆H₆ + HNO₃ → C₆H₅NO₂ + H₂O (H₂SO₄ đặc, t°)", amount: dn);
                changed = true;
            }
            if (w.M("Br2") > 1e-9 && (b.Get("Fe(s)") > 1e-9 || w.M("Fe3+") > 1e-9))
            {
                var pb = FindOrganic?.Invoke("C6H5Br");
                double dn = Math.Min(Math.Min(b.Get(bz.Index), w.M("Br2")), Math.Min(b.Get(bz.Index), w.M("Br2")) * (1 - Math.Exp(-0.05 * dt)) + 1e-7);
                b.Add(bz.Index, -dn); b.Add("Br2", -dn); b.Add("Br-", dn);
                if (pb != null) b.Add(pb.Index, dn);
                Emit(w, EventKind.Gas, "brom_benzen", "Màu nâu đỏ của brom nhạt dần, khí HBr thoát ra (khói trắng trong không khí ẩm)", "Thế brom vào vòng benzen cần xúc tác Fe (FeBr₃); nước brom thì không phản ứng.",
                     "C₆H₆ + Br₂ → C₆H₅Br + HBr (Fe, t°)", Severity.Warning, "khi_doc", "HBr", dn);
                changed = true;
            }
            return changed;
        }

        /// <summary>Phần thể tích chất lỏng hữu cơ (dung môi, ancol) trong bình: 0 = toàn nước.</summary>
        public static double OrganicFraction(MixBuilder b, double V)
        {
            double ml = 0;
            foreach (int s in b.Indices)
            {
                var d = SpeciesRegistry.At(s);
                if (d.ChemStt <= 0 || d.Kind == SpeciesKind.Lump || d.Kind == SpeciesKind.Solid || d.Kind == SpeciesKind.Precipitate) continue;
                ml += b.Get(s) * Math.Max(1, d.MolarMass) / Math.Max(0.5, d.Density);
            }
            return Math.Min(1, ml / Math.Max(1e-9, V * 1000));
        }
    }
}
