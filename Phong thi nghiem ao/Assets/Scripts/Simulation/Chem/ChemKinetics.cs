using System;
using System.Collections.Generic;

namespace Khkt.Simulation.Chem
{
    public static partial class ChemEngine
    {
        // ------------------------------------------------------------------
        // Loài tạo từ danh mục hóa chất
        // ------------------------------------------------------------------

        /// <summary>Phân tử hữu cơ (hoặc axit/bazơ hữu cơ yếu) theo số thứ tự trong danh mục.</summary>
        public static SpeciesDef RegisterOrganic(int stt, string name, string formula, IEnumerable<string> tags, double ka, double kb, double molarMass, double density)
        {
            string id = "hc:" + stt;
            var s = SpeciesRegistry.Get(id);
            if (s != null) return s;
            var kind = ka > 0 || kb > 0 ? SpeciesKind.Weak : SpeciesKind.Molecule;
            s = SpeciesRegistry.Add(id, formula, kind, kb > 0 ? 1 : 0, molarMass, name);
            if (ka > 0) s.Ka = new[] { ka };
            if (kb > 0) s.Ka = new[] { Kw / kb };
            if (tags != null) foreach (var t in tags) s.Tags.Add(t);
            s.Insoluble = s.HasTag("khong_tan");
            s.Density = density > 0 ? density : 1;
            s.ChemStt = stt;
            if (s.HasTag("phenol")) { s.Kind = SpeciesKind.Weak; s.Ka = new[] { 1e-10 }; }
            return s;
        }

        /// <summary>Vật liệu trơ (polime, bông thủy tinh...): chất rắn không tan, không phản ứng.</summary>
        public static SpeciesDef RegisterInert(int stt, string name, string formula, Rgb color, double molarMass)
        {
            string id = "tro:" + stt;
            var s = SpeciesRegistry.Get(id);
            if (s != null) return s;
            s = SpeciesRegistry.Add(id, string.IsNullOrEmpty(formula) ? name : formula, SpeciesKind.Solid, 0, molarMass, name);
            s.Color = color;
            s.ColorStrength = 1;
            s.ChemStt = stt;
            s.Tags.Add("tro");
            return s;
        }

        /// <summary>Hóa chất rắn vừa cho vào bình: tan dần thành các loài trong composition (theo 1 mol).</summary>
        public static SpeciesDef RegisterLump(int stt, string name, string formula, (string id, double n)[] composition, double molarMass, Rgb color, double heatKJ, IEnumerable<string> tags = null)
        {
            string id = "lump:" + stt;
            var s = SpeciesRegistry.Get(id);
            if (s != null) return s;
            s = SpeciesRegistry.Add(id, formula, SpeciesKind.Lump, 0, molarMass, name);
            s.Composition = composition;
            if (!string.IsNullOrEmpty(formula)) s.Tags.Add("f:" + formula);
            s.Color = color;
            s.ColorStrength = 1;
            s.ChemStt = stt;
            s.Density = 2.2;
            if (tags != null) foreach (var t in tags) s.Tags.Add(t);
            BuildPairs();
            bool slow = pairs.Exists(p => p.Ppt == formula) || Array.Exists(composition, c => SpeciesRegistry.Get(c.id)?.Kind == SpeciesKind.Solid || SpeciesRegistry.Get(c.id)?.Kind == SpeciesKind.Precipitate);
            s.DissolveRate = slow ? 4e-4 : 6e-3;
            // muối cacbonat / sunfit / sunfua không tan (đá vôi, FeS...): chỉ tan khi có axit, theo độ axit
            if (pairs.Exists(p => p.Ppt == formula && p.Solubility < 1e-3 && (p.Anion == "CO3" || p.Anion == "SO3" || p.Anion == "S"))) acidLumps.Add(s.Index);
            s.HeatKJ = heatKJ;
            return s;
        }

        // ------------------------------------------------------------------
        // Bước theo thời gian
        // ------------------------------------------------------------------

        /// <summary>Hóa chất rắn không tan trong nước nhưng tan trong axit (giải phóng CO₂, SO₂, H₂S).</summary>
        static readonly HashSet<int> acidLumps = new HashSet<int>();

        static readonly HashSet<string> stepIds = new HashSet<string>
        {
            "H2O2", "catalase", "Fe(OH)2", "Mn(OH)2", "S2O32-", "AgNH3", "Ag+", "CuPol", "Cu(OH)2", "ninhydrin", "ClO", "Ox",
        };

        /// <summary>Hệ số hoạt tính tương đối (0..1) của enzyme catalase theo nhiệt độ (°C) và trạng thái đã luộc.</summary>
        public static double CatalaseActivity(double tempC, bool boiled = false)
        {
            if (boiled || tempC < 0 || tempC >= 65) return 0;
            double diff = tempC - 37.0;
            double act = Math.Exp(-(diff * diff) / (2.0 * 14.0 * 14.0));
            if (tempC > 55.0) act *= (65.0 - tempC) / 10.0;
            return Math.Max(0.0, Math.Min(1.0, act));
        }

        /// <summary>Có cần chạy Step không (để vật chứa đứng yên không tốn công tính).</summary>
        public static bool NeedsStep(Mix mix, double tempC)
        {
            if (mix == null) return false;
            for (int i = 0; i < mix.Count; i++)
            {
                var d = mix.SpeciesAt(i);
                if (d.Kind == SpeciesKind.Lump || (d.Kind == SpeciesKind.Solid && d.Id != "AgMirror" && !d.HasTag("tro"))) return true;
                if (stepIds.Contains(d.Id)) return true;
                if (d.Kind == SpeciesKind.Precipitate && AcidSoluble.ContainsKey(d.Id)) return true;
                if (tempC > 45 && mix.Count > 1) return true;
                if (d.ChemStt > 0 && d.Kind != SpeciesKind.Lump && (d.HasTag("este") || Ascii(d) == "C6H6")) return true;
                if (tempC > 45 && (d.HasTag("protein") || d.HasTag("este") || d.HasTag("aren_ankyl") || d.HasTag("amino_axit"))) return true;
            }
            return false;
        }

        static readonly Dictionary<string, ((string id, double n)[] ions, double phAcid, double phBase)> oxides = new Dictionary<string, ((string, double)[], double, double)>
        {
            ["MgO(s)"] = (new[] { ("Mg2+", 1.0) }, 9, 99), ["FeO(s)"] = (new[] { ("Fe2+", 1.0) }, 3.5, 99), ["Fe2O3(s)"] = (new[] { ("Fe3+", 2.0) }, 2.5, 99),
            ["Fe3O4(s)"] = (new[] { ("Fe2+", 1.0), ("Fe3+", 2.0) }, 2.5, 99), ["CuO(s)"] = (new[] { ("Cu2+", 1.0) }, 4, 99), ["Cu2O(s)"] = (new[] { ("Cu2+", 1.0), ("Cu(s)", 1.0) }, 3, 99),
            ["ZnO(s)"] = (new[] { ("Zn2+", 1.0) }, 5, 13), ["PbO(s)"] = (new[] { ("Pb2+", 1.0) }, 4, 13), ["SnO(s)"] = (new[] { ("Sn2+", 1.0) }, 3, 13),
            ["MnO(s)"] = (new[] { ("Mn2+", 1.0) }, 5, 99), ["NiO(s)"] = (new[] { ("Ni2+", 1.0) }, 3, 99), ["CoO(s)"] = (new[] { ("Co2+", 1.0) }, 3, 99),
            ["Ag2O(s)"] = (new[] { ("Ag+", 2.0) }, 6, 99), ["HgO(s)"] = (new[] { ("Hg2+", 1.0) }, 4, 99), ["CdO(s)"] = (new[] { ("Cd2+", 1.0) }, 5, 99),
            ["BeO(s)"] = (new[] { ("Be2+", 1.0) }, 3, 13.5), ["Al2O3(s)"] = (new[] { ("Al3+", 2.0) }, 2, 13.5), ["Bi2O3(s)"] = (new[] { ("Bi3+", 2.0) }, 2, 99),
            ["CrO(s)"] = (new[] { ("Cr3+", 1.0) }, 3, 99), ["SnO2(s)"] = (new[] { ("Sn4+", 1.0) }, -1, 13.5),
        };

        static readonly Dictionary<string, ((string id, double n)[] products, string gas, double gasN, double rate, string title, string eq, string hazard)> waterSolids =
            new Dictionary<string, ((string, double)[], string, double, double, string, string, string)>
            {
                ["CaC2(s)"] = (new[] { ("Ca2+", 1.0) }, "C2H2", 1, 2e-3, "Sủi mạnh khí axetilen (C₂H₂)", "CaC₂ + 2H₂O → Ca(OH)₂ + C₂H₂↑", "chay_no"),
                ["Al4C3(s)"] = (new[] { ("Al3+", 4.0) }, "CH4", 3, 8e-4, "Sủi khí metan, kết tủa keo trắng", "Al₄C₃ + 12H₂O → 4Al(OH)₃↓ + 3CH₄↑", "chay_no"),
                ["Mg3N2(s)"] = (new[] { ("Mg2+", 3.0), ("NH3", 2.0) }, null, 0, 6e-4, "Kết tủa trắng, có mùi khai NH₃", "Mg₃N₂ + 6H₂O → 3Mg(OH)₂↓ + 2NH₃↑", null),
                ["Ca3N2(s)"] = (new[] { ("Ca2+", 3.0), ("NH3", 2.0) }, null, 0, 6e-4, "Có mùi khai NH₃", "Ca₃N₂ + 6H₂O → 3Ca(OH)₂ + 2NH₃↑", null),
                ["NaNH2(s)"] = (new[] { ("Na+", 1.0), ("NH3", 1.0) }, null, 0, 2e-3, "Có mùi khai NH₃", "NaNH₂ + H₂O → NaOH + NH₃↑", "an_mon"),
                ["NaH(s)"] = (new[] { ("Na+", 1.0) }, "H2", 1, 3e-3, "Sủi mạnh khí H₂", "NaH + H₂O → NaOH + H₂↑", "chay_no"),
                ["LiAlH4(s)"] = (new[] { ("Li+", 1.0), ("Al3+", 1.0) }, "H2", 4, 2e-3, "Sủi rất mạnh khí H₂ (nguy hiểm)", "LiAlH₄ + 4H₂O → LiOH + Al(OH)₃ + 4H₂↑", "chay_no"),
                ["NaBH4(s)"] = (new[] { ("Na+", 1.0), ("B", 1.0) }, "H2", 4, 2e-4, "Sủi bọt khí H₂", "NaBH₄ + 2H₂O → NaBO₂ + 4H₂↑", "chay_no"),
                ["Na2O2(s)"] = (new[] { ("Na+", 2.0) }, "O2", 0.5, 2e-3, "Sủi bọt khí O₂, dung dịch hóa kiềm", "2Na₂O₂ + 2H₂O → 4NaOH + O₂↑", "an_mon"),
                ["K2O2(s)"] = (new[] { ("K+", 2.0) }, "O2", 0.5, 2e-3, "Sủi bọt khí O₂", "2K₂O₂ + 2H₂O → 4KOH + O₂↑", "an_mon"),
            };

        /// <summary>
        /// Chạy các quá trình theo thời gian trong dt giây (kim loại tan, chất rắn tan, phản ứng cần đun...),
        /// rồi cân bằng lại nếu có thay đổi. Trả về kết quả (nhiệt tỏa ra để vật chứa nóng lên).
        /// </summary>
        public static Result Step(MixBuilder b, double V, double dt, ChemEnv env, List<ChemEvent> events, ref float ppDrops)
        {
            var res = new Result();
            if (V < MinLiquidL) return res;
            var w = new Work { b = b, V = V, env = env, events = events, res = res };
            w.h = SolveH(b, V);
            bool changed = false;
            double stir = 1 + 2 * env.Stir;

            foreach (int s in b.Snapshot())
            {
                var d = SpeciesRegistry.At(s);
                double n = b.Get(s);
                if (n <= 0) continue;

                // Hóa chất rắn tan dần
                if (d.Kind == SpeciesKind.Lump)
                {
                    double acid = acidLumps.Contains(s) ? (w.Ph < 5.5 ? Math.Min(3, w.h / 0.1) : 0) : 1;
                    if (acid <= 0) continue;
                    double dn = Math.Min(n, (d.DissolveRate * dt * stir + n * 0.02 * dt) * acid);
                    if (acidLumps.Contains(s)) dn = Math.Min(dn, w.h * V * 0.5);  // mỗi bước chỉ tan theo lượng H⁺ tự do (CaCO₃ + 2H⁺), axit nhỏ từng đợt vẫn sủi CO₂
                    b.Add(s, -dn);
                    foreach (var (id, k) in d.Composition) b.Add(id, k * dn);
                    res.HeatJ += d.HeatKJ * 1000 * dn;
                    changed = true;
                    if (n - dn <= 1e-12) LumpDissolved(w, d);
                    continue;
                }
                if (d.Kind != SpeciesKind.Solid) continue;

                // Kim loại kiềm trong ancol, phenol (nhóm -OH có H linh động; mẫu theo nhóm chức)
                if (MetalData.TryGetValue(d.Id, out var mAlc) && mAlc.water && OrganicFraction(b, V) > 0.5)
                {
                    var alc = MainTagged(b, "ancol") ?? MainTagged(b, "phenol");
                    string F = Ascii(alc);
                    if (alc != null && F != null)
                    {
                        double dnA = Math.Min(n, mAlc.rate * 0.2 * dt);
                        b.Add(s, -dnA);
                        b.Add(d.IonId, dnA);
                        string prod = F.EndsWith("OH") ? F.Substring(0, F.Length - 2) + "O" + d.Formula : null;
                        Emit(w, EventKind.Gas, "na_ancol", $"{Cap(d.Name)} tan dần, sủi bọt khí H₂ (êm hơn trong nước)", "Ancol, phenol có nguyên tử H linh động trong nhóm -OH tác dụng với kim loại kiềm.",
                             prod != null ? Equation.Balance(new[] { F, d.Formula }, new[] { prod, "H2" }) : null, Severity.Warning, "chay_no", "H2", dnA / 2);
                        changed = true;
                    }
                    continue;
                }

                // Oxit tan trong axit (oxit lưỡng tính tan cả trong kiềm)
                if (oxides.TryGetValue(d.Id, out var ox))
                {
                    bool acid = w.Ph < ox.phAcid, basic = w.Ph > ox.phBase;
                    if (acid || basic)
                    {
                        double dn = Math.Min(n, 3e-4 * dt * stir * (acid ? Math.Min(3, Math.Max(0.05, w.h / 0.1)) : 1));
                        b.Add(s, -dn);
                        foreach (var (id, k) in ox.ions) b.Add(id, k * dn);
                        Emit(w, EventKind.Dissolve, "tan:" + d.Id, $"{d.Formula} tan dần" + (acid ? " trong axit" : " trong kiềm"),
                             acid ? "Oxit bazơ tác dụng với axit tạo muối và nước." : "Oxit lưỡng tính tan trong dung dịch kiềm mạnh.", amount: dn, color: d.Color);
                        changed = true;
                    }
                }
                // Chất rắn phản ứng với nước
                if (waterSolids.TryGetValue(d.Id, out var ws))
                {
                    double dn = Math.Min(n, ws.rate * dt * stir);
                    b.Add(s, -dn);
                    foreach (var (id, k) in ws.products) b.Add(id, k * dn);
                    Emit(w, ws.gas != null ? EventKind.Gas : EventKind.Reaction, "nuoc:" + d.Id, ws.title, "Chất này phản ứng ngay với nước.", ws.eq,
                         ws.hazard == "chay_no" ? Severity.Warning : Severity.Notice, ws.hazard, ws.gas, dn * ws.gasN);
                    res.HeatJ += 100e3 * dn;
                    changed = true;
                }
                // Than hoạt tính hấp phụ chất màu
                if (d.Id == "Cact(s)")
                {
                    foreach (int t in b.Snapshot())
                    {
                        var td = SpeciesRegistry.At(t);
                        if (td.ColorStrength <= 0 || !(td.Kind == SpeciesKind.Molecule || td.Kind == SpeciesKind.Indicator || td.Id == "I2" || td.Id == "Br2" || td.Id == "picric")) continue;
                        b.Add(t, -b.Get(t) * (1 - Math.Exp(-0.15 * dt)));
                        changed = true;
                        Emit(w, EventKind.Color, "happhu", "Dung dịch nhạt màu dần (than hoạt tính hấp phụ)", "Than hoạt tính có diện tích bề mặt rất lớn, giữ lại phân tử chất màu.");
                    }
                    ppDrops *= (float)Math.Exp(-0.15 * dt);
                }
            }

            changed |= RedoxTable(w, true, dt, stir);
            changed |= ThptStep(w, dt, env);
            if (env.TempC > 25) changed |= Aromatic(w, dt, env.TempC);

            // Biến tính enzyme catalase khi đun nóng >= 65 °C
            double catRaw = b.Get("catalase");
            if (catRaw > 1e-12 && env.TempC >= 65)
            {
                b.Set("catalase", 0);
                b.Add("catalase_bat_hoat", catRaw);
                changed = true;
            }

            // H2O2 phân hủy (xúc tác catalase sinh học, MnO2, Fe3+, đun nóng)
            double h2o2 = b.Get("H2O2");
            if (h2o2 > 1e-10)
            {
                double k = 0;
                string cat = null;
                double catMol = b.Get("catalase");
                if (catMol > 1e-12)
                {
                    double act = CatalaseActivity(env.TempC, false);
                    if (act > 0)
                    {
                        k += 0.85 * act;
                        cat = "catalase";
                    }
                }
                if (b.Get("MnO2(s)") > 1e-7 || b.Get("MnO2(kt)") > 1e-7) { k += 0.35; cat ??= "MnO₂"; }
                if (w.C("Fe3+") > 1e-4) { k += 0.03; cat ??= "Fe³⁺"; }
                if (w.C("Cu2+") > 1e-3) { k += 0.01; cat ??= "Cu²⁺"; }
                if (env.TempC > 60) k += 0.01;
                if (w.Ph > 11) k += 0.004;
                if (k > 0)
                {
                    double dn = h2o2 * (1 - Math.Exp(-k * dt));
                    b.Add("H2O2", -dn);
                    res.HeatJ += 98e3 * dn;
                    Emit(w, EventKind.Gas, "khi:O2:H2O2", cat != null ? $"Sủi bọt mạnh khí O₂ (xúc tác {cat})" : "H₂O₂ phân hủy, sủi bọt khí O₂",
                         "Khí O₂ làm que đóm còn tàn đỏ bùng cháy.", "2H₂O₂ → 2H₂O + O₂↑", Severity.Info, null, "O2", dn / 2);
                    changed = true;
                }
            }

            // Hiđroxit bị oxi hóa bởi không khí
            double fe2 = b.Get("Fe(OH)2");
            if (fe2 > 1e-10)
            {
                double dn = fe2 * (1 - Math.Exp(-0.012 * dt));
                b.Add("Fe(OH)2", -dn);
                b.Add("Fe3+", dn);
                Emit(w, EventKind.Color, "oxkk:Fe", "Kết tủa trắng xanh hóa nâu đỏ dần", "Fe(OH)₂ bị oxi trong không khí oxi hóa thành Fe(OH)₃.", "4Fe(OH)₂ + O₂ + 2H₂O → 4Fe(OH)₃", amount: dn, color: SpeciesRegistry.Get("Fe(OH)3").Color);
                changed = true;
            }
            double mn = b.Get("Mn(OH)2");
            if (mn > 1e-10)
            {
                double dn = mn * (1 - Math.Exp(-0.008 * dt));
                b.Add("Mn(OH)2", -dn);
                b.Add("MnO2(kt)", dn);
                Emit(w, EventKind.Color, "oxkk:Mn", "Kết tủa trắng hóa nâu đen dần", "Mn(OH)₂ bị oxi không khí oxi hóa.", "2Mn(OH)₂ + O₂ → 2MnO₂ + 2H₂O", amount: dn, color: SpeciesRegistry.Get("MnO2(kt)").Color);
                changed = true;
            }

            // Thiosunfat + axit → S vẩn đục (chậm)
            double thio = b.Get("S2O32-");
            if (thio > 1e-10 && w.Ph < 4)
            {
                double k = 0.06 * Math.Pow(Math.Min(10, w.h / 0.1), 0.7) * (env.TempC > 40 ? 2 : 1);
                double dn = thio * (1 - Math.Exp(-k * dt));
                b.Add("S2O32-", -dn);
                b.Add("S(kt)", dn);
                b.Add("SO3", dn);
                Emit(w, EventKind.Precipitate, "kt:S:thio", "Dung dịch vẩn đục vàng dần (lưu huỳnh)", "Tốc độ phản ứng phụ thuộc nồng độ và nhiệt độ: thí nghiệm khảo sát tốc độ phản ứng.",
                     "S₂O₃²⁻ + 2H⁺ → S↓ + SO₂↑ + H₂O", amount: dn, color: SpeciesRegistry.Get("S(kt)").Color);
                changed = true;
            }

            // ----- Phản ứng cần đun nóng -----
            double T = env.TempC;
            if (T > 45)
            {
                double ald = TagAmount(b, "andehit") + (w.Ph > 9 ? TagAmount(b, "andehit_kiem") : 0);
                double agc = b.Get("AgNH3") + (b.Get("NH3") > 0 ? b.Get("Ag+") : 0);
                if (ald > 1e-10 && agc > 1e-10 && w.Ph > 8)
                {
                    double dAld = ald * (1 - Math.Exp(-0.05 * dt));
                    double dAg = Math.Min(agc, 2 * dAld);
                    double fromCx = Math.Min(b.Get("AgNH3"), dAg);
                    b.Add("AgNH3", -fromCx);
                    b.Add("NH3", 2 * fromCx);
                    b.Add("Ag+", -(dAg - fromCx));
                    TakeTag(b, "@andehit", dAg / 2);
                    b.Add("AgMirror", dAg);
                    var ald1 = MainTagged(b, "andehit") ?? MainTagged(b, "andehit_kiem");
                    Emit(w, EventKind.Reaction, "trangbac", "Lớp bạc sáng như gương bám lên thành ống (tráng bạc)", "Nhóm -CHO khử ion bạc trong thuốc thử Tollens khi đun nhẹ.",
                         (ald1 != null ? TollensEquation(Ascii(ald1)) : null) ?? "RCHO + 2[Ag(NH₃)₂]OH → RCOONH₄ + 2Ag↓ + 3NH₃ + H₂O", amount: dAg, color: SpeciesRegistry.Get("AgMirror").Color);
                    changed = true;
                }
            }
            if (T > 70)
            {
                double ald = TagAmount(b, "andehit") + TagAmount(b, "andehit_kiem");
                double cu = b.Get("CuPol") + b.Get("Cu(OH)2");
                if (ald > 1e-10 && cu > 1e-10 && w.Ph > 10)
                {
                    double dAld = ald * (1 - Math.Exp(-0.04 * dt));
                    double dCu = Math.Min(cu, 2 * dAld);
                    double fromCx = Math.Min(b.Get("CuPol"), dCu);
                    b.Add("CuPol", -fromCx);
                    b.Add("Cu(OH)2", -(dCu - fromCx));
                    TakeTag(b, "@andehit", dCu / 2);
                    b.Add("Cu2O", dCu / 2);
                    var ald2 = MainTagged(b, "andehit") ?? MainTagged(b, "andehit_kiem");
                    Emit(w, EventKind.Precipitate, "kt:Cu2O", "Kết tủa đỏ gạch Cu₂O", "Anđehit (glucozơ) khử Cu(II) trong kiềm khi đun nóng (Fehling, Benedict).",
                         (ald2 != null ? FehlingEquation(Ascii(ald2)) : null) ?? "RCHO + 2Cu(OH)₂ + NaOH → RCOONa + Cu₂O↓ + 3H₂O", amount: dCu / 2, color: SpeciesRegistry.Get("Cu2O").Color);
                    changed = true;
                }
                double prot = TagAmount(b, "protein");
                if (prot > 1e-10)
                {
                    double dn = prot * (1 - Math.Exp(-0.1 * dt));
                    TakeTag(b, "@protein", dn);
                    b.Add("ProteinDT", dn);
                    Emit(w, EventKind.Precipitate, "kt:dongtu", "Protein đông tụ (vẩn trắng)", "Đun nóng làm protein biến tính, đông tụ (như luộc trứng).", amount: dn, color: SpeciesRegistry.Get("ProteinDT").Color);
                    changed = true;
                }
                if (w.Ph > 12)
                {
                    foreach (int s in b.Snapshot())
                    {
                        var d = SpeciesRegistry.At(s);
                        if (!d.HasTag("este") || d.Kind == SpeciesKind.Lump) continue;
                        double dn = b.Get(s) * (1 - Math.Exp(-0.03 * dt));
                        b.Add(s, -dn);
                        b.Add(d.HasTag("chat_beo") ? "xaphong" : "Ac", d.HasTag("chat_beo") ? 3 * dn : dn);
                        b.Add("Na+", 0);
                        Emit(w, EventKind.Reaction, "xaphonghoa", d.HasTag("chat_beo") ? "Xà phòng hóa: lớp chất béo tan dần" : "Este bị thủy phân trong kiềm, lớp este tan dần",
                             "Phản ứng một chiều trong môi trường kiềm.", SaponificationEquation(d) ?? (d.HasTag("chat_beo") ? "(RCOO)₃C₃H₅ + 3NaOH → 3RCOONa + C₃H₅(OH)₃" : "RCOOR' + NaOH → RCOONa + R'OH"), amount: dn);
                        changed = true;
                    }
                }
                if (b.Get("MnO4-") > 1e-10 && TagAmount(b, "aren_ankyl") > 1e-10)
                {
                    double dn = Math.Min(b.Get("MnO4-"), TagAmount(b, "aren_ankyl") * 2) * (1 - Math.Exp(-0.05 * dt));
                    b.Add("MnO4-", -dn);
                    b.Add("MnO2(kt)", dn);
                    TakeTag(b, "@aren_ankyl", dn / 2);
                    Emit(w, EventKind.Reaction, "pu:toluen", "Đun nóng: màu tím mất, kết tủa nâu đen", "Toluen (ankylbenzen) bị KMnO₄ oxi hóa khi đun nóng, benzen thì không.",
                         "C₆H₅CH₃ + 2KMnO₄ → C₆H₅COOK + 2MnO₂↓ + KOH + H₂O", amount: dn);
                    changed = true;
                }
            }
            if (T > 60 && b.Get("ninhydrin") > 1e-12 && (TagAmount(b, "amino_axit") > 1e-12 || TagAmount(b, "protein") > 1e-12))
            {
                double dn = b.Get("ninhydrin") * (1 - Math.Exp(-0.2 * dt));
                b.Add("ninhydrin", -dn);
                if (SpeciesRegistry.Get("RuhemannTim") == null) RegisterColorProduct("RuhemannTim");
                b.Add("RuhemannTim", dn);
                Emit(w, EventKind.Color, "mau:ninhydrin", "Màu tím xanh (ninhiđrin)", "Amino axit, protein cho màu tím khi đun với ninhiđrin.", amount: dn, color: SpeciesRegistry.Get("RuhemannTim").Color);
                changed = true;
            }

            // Nước Gia-ven tẩy màu chất chỉ thị
            if (w.C("ClO") > 0.005)
            {
                bool any = ppDrops > 0.05f;
                ppDrops *= (float)Math.Exp(-0.3 * dt);
                foreach (int s in b.Snapshot())
                {
                    var d = SpeciesRegistry.At(s);
                    if (d.Kind == SpeciesKind.Indicator || (d.Kind == SpeciesKind.Molecule && d.ColorStrength > 0 && d.Id != "Cl2" && d.Id != "Br2" && d.Id != "I2"))
                    {
                        b.Add(s, -b.Get(s) * (1 - Math.Exp(-0.3 * dt)));
                        any = true;
                    }
                }
                if (any)
                {
                    Emit(w, EventKind.Color, "taymau", "Màu chất chỉ thị bị tẩy mất", "ClO⁻ có tính oxi hóa mạnh, phá hủy phân tử chất màu (tính tẩy màu của nước Gia-ven).");
                    changed = true;
                }
            }

            if (changed)
            {
                var r2 = Equilibrate(b, V, env, events);
                res.H = r2.H;
                res.HeatJ += r2.HeatJ;
                res.Changed = true;
            }
            else res.H = w.h;
            return res;
        }

        /// <summary>Hóa chất rắn vừa tan hết: báo hòa tan, kèm màu dung dịch nếu thành phần có màu.</summary>
        static void LumpDissolved(Work w, SpeciesDef d)
        {
            string color = null;
            double best = 0;
            if (d.Composition != null)
                foreach (var (id, _) in d.Composition)
                {
                    var sp = SpeciesRegistry.Get(id);
                    string name = ChemPalette.NameOf(id);
                    if (sp == null || name == null || name == "không màu" || name.StartsWith("hồng rất nhạt") || sp.ColorStrength <= best) continue;
                    best = sp.ColorStrength;
                    color = name.Split('(')[0].Trim();
                }
            string what = Cap(d.Name);
            Emit(w, EventKind.Dissolve, "hoatan:" + d.ChemStt, color != null ? $"{what} tan hết, dung dịch có màu {color}" : $"{what} tan hết trong nước",
                 d.HeatKJ < -10 ? "Quá trình hòa tan thu nhiệt: dung dịch lạnh đi." : d.HeatKJ > 30 ? "Quá trình hòa tan tỏa nhiệt." : "Hòa tan là biến đổi vật lí (chất tan phân tán thành ion / phân tử).");
        }

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        // ------------------------------------------------------------------
        // Nhiệt
        // ------------------------------------------------------------------

        /// <summary>Số mol H⁺ và OH⁻ tự do của một lượng dung dịch.</summary>
        public static void FreeIons(Mix mix, double V, out double h, out double oh)
        {
            if (V < MinLiquidL || mix == null) { h = oh = 0; return; }
            double ph = Ph(mix, V);
            h = Math.Pow(10, -ph) * V;
            oh = Math.Pow(10, ph - 14) * V;
        }

        /// <summary>Nhiệt trung hòa (J) khi trộn hai lượng dung dịch a, b thành m (ΔH ≈ −57,3 kJ/mol H₂O).</summary>
        public static double NeutralizationJ(double hA, double ohA, double hB, double ohB, double hM, double ohM)
        {
            double n = Math.Max(Math.Max(0, hA + hB - hM), Math.Max(0, ohA + ohB - ohM));
            n = Math.Min(n, Math.Max(hA + hB, ohA + ohB));
            return n * 57300;
        }
    }
}
