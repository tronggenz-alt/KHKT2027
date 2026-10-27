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

        /// <summary>Hóa chất rắn vừa cho vào bình: tan dần thành các loài trong composition (theo 1 mol).</summary>
        public static SpeciesDef RegisterLump(int stt, string name, string formula, (string id, double n)[] composition, double molarMass, Rgb color, double heatKJ, IEnumerable<string> tags = null)
        {
            string id = "lump:" + stt;
            var s = SpeciesRegistry.Get(id);
            if (s != null) return s;
            s = SpeciesRegistry.Add(id, formula, SpeciesKind.Lump, 0, molarMass, name);
            s.Composition = composition;
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
            "H2O2", "Fe(OH)2", "Mn(OH)2", "S2O32-", "AgNH3", "Ag+", "CuPol", "Cu(OH)2", "ninhydrin", "ClO", "Ox",
        };

        /// <summary>Có cần chạy Step không (để vật chứa đứng yên không tốn công tính).</summary>
        public static bool NeedsStep(Mix mix, double tempC)
        {
            if (mix == null) return false;
            for (int i = 0; i < mix.Count; i++)
            {
                var d = mix.SpeciesAt(i);
                if (d.Kind == SpeciesKind.Lump || (d.Kind == SpeciesKind.Solid && d.Id != "AgMirror" && d.Id != "Au(s)" && d.Id != "SiO2(s)")) return true;
                if (stepIds.Contains(d.Id)) return true;
                if (tempC > 45 && (d.HasTag("protein") || d.HasTag("este") || d.HasTag("aren_ankyl") || d.HasTag("amino_axit"))) return true;
            }
            return false;
        }

        static readonly (string id, double rate, double heatKJ)[] waterMetals =
        {
            ("K(s)", 8e-3, 196), ("Na(s)", 4e-3, 184), ("Li(s)", 2e-3, 222), ("Ba(s)", 3e-3, 390), ("Ca(s)", 1.5e-3, 414),
        };

        static readonly Dictionary<string, (double rate, double phAcid, double phBase, double heatKJ)> acidMetals = new Dictionary<string, (double, double, double, double)>
        {
            ["Mg(s)"] = (2.5e-3, 7, 99, 467), ["Al(s)"] = (5e-4, 3.5, 12.5, 531), ["Zn(s)"] = (7e-4, 4.5, 13, 153), ["Mn(s)"] = (6e-4, 4, 99, 221),
            ["Cr(s)"] = (1e-4, 2.5, 99, 143), ["Fe(s)"] = (2.5e-4, 3, 99, 87), ["Co(s)"] = (8e-5, 2.5, 99, 56), ["Ni(s)"] = (6e-5, 2.5, 99, 54),
            ["Sn(s)"] = (8e-5, 2, 99, 9), ["Pb(s)"] = (3e-5, 1.5, 99, 0),
        };

        static readonly (string ion, string product, double e, double E)[] oxidantIons =
        {
            ("Hg2+", "Hg(l)", 2, 0.85), ("Ag+", "Ag(s)", 1, 0.80), ("Fe3+", "Fe2+", 1, 0.77), ("Cu2+", "Cu(s)", 2, 0.34),
            ("Pb2+", "Pb(s)", 2, -0.13), ("Sn2+", "Sn(s)", 2, -0.14), ("Ni2+", "Ni(s)", 2, -0.25), ("Co2+", "Co(s)", 2, -0.28),
            ("Fe2+", "Fe(s)", 2, -0.44), ("Zn2+", "Zn(s)", 2, -0.76),
        };

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
                    continue;
                }
                if (d.Kind != SpeciesKind.Solid) continue;

                // Kim loại kiềm, kiềm thổ + nước
                foreach (var (id, rate, heat) in waterMetals)
                {
                    if (d.Id != id) continue;
                    double dn = Math.Min(n, rate * dt);
                    b.Add(s, -dn);
                    b.Add(d.IonId, dn);
                    res.HeatJ += heat * 1000 * dn;
                    double total = dn;
                    bool violent = (id == "K(s)" || id == "Na(s)") && n > 0.004;
                    Emit(w, EventKind.Gas, "khi:H2:" + id, id == "K(s)" ? "Kali chạy trên mặt nước, bốc cháy ngọn lửa tím" : id == "Na(s)" ? "Natri chạy trên mặt nước, sủi khí H₂, tỏa nhiều nhiệt" : $"{d.Name} tan, sủi khí H₂",
                         "Kim loại kiềm (kiềm thổ) khử nước, dung dịch thu được làm phenolphtalein hóa hồng.", $"2{d.Formula} + 2H₂O → 2{d.Formula}OH + H₂↑".Replace("2Ca", "Ca").Replace("2Ba", "Ba"),
                         violent ? Severity.Danger : Severity.Warning, violent ? "chay_no" : "bong", "H2", total * d.IonCharge / 2.0);
                    changed = true;
                }

                if (acidMetals.TryGetValue(d.Id, out var am))
                {
                    double ph = w.Ph;
                    double nitrate = w.C("NO3-");
                    bool oxidizingAcid = nitrate > 0.3 && ph < 1.5;
                    bool passive = (d.Id == "Al(s)" || d.Id == "Fe(s)" || d.Id == "Cr(s)") && ((nitrate > 8 && ph < 0) || (w.C("SO4") > 12 && env.TempC < 60));
                    if (passive)
                    {
                        Emit(w, EventKind.Reaction, "thudong:" + d.Id, $"{d.Name} không tan (bị thụ động hóa)", "Al, Fe, Cr bị thụ động trong HNO₃ đặc nguội, H₂SO₄ đặc nguội.");
                    }
                    else if (oxidizingAcid)
                    {
                        changed |= MetalNitric(w, s, d, dt);
                    }
                    else if (ph < am.phAcid || (d.Id == "Mg(s)" && env.TempC > 70))
                    {
                        double factor = Math.Min(3, Math.Max(0.02, w.h / 0.1));
                        if (d.Id == "Mg(s)" && ph >= am.phAcid) factor = 0.05;  // Mg + nước nóng
                        double dn = Math.Min(n, am.rate * factor * dt * stir);
                        b.Add(s, -dn);
                        b.Add(d.IonId, dn);
                        res.HeatJ += am.heatKJ * 1000 * dn;
                        Emit(w, EventKind.Gas, "khi:H2:" + d.Id, $"{Cap(d.Name)} tan dần, sủi bọt khí H₂", "Kim loại đứng trước H trong dãy hoạt động hóa học đẩy được H₂ khỏi axit loãng.",
                             $"{d.Formula} + {(d.IonCharge == 1 ? "" : d.IonCharge.ToString())}H⁺ → {SpeciesRegistry.Get(d.IonId).Formula} + {(d.IonCharge == 2 ? "" : (d.IonCharge / 2.0).ToString("0.#"))}H₂↑",
                             Severity.Info, null, "H2", dn * d.IonCharge / 2.0);
                        changed = true;
                    }
                    else if (ph > am.phBase)
                    {
                        double oh = Kw / w.h;
                        double dn = Math.Min(n, am.rate * Math.Min(3, oh / 0.5) * dt * stir);
                        b.Add(s, -dn);
                        b.Add(d.IonId, dn);
                        res.HeatJ += am.heatKJ * 1000 * dn * 0.6;
                        Emit(w, EventKind.Gas, "khi:H2:kiem:" + d.Id, $"{Cap(d.Name)} tan trong kiềm, sủi bọt khí H₂", "Al, Zn (hiđroxit lưỡng tính) tan được trong dung dịch kiềm mạnh.",
                             d.Id == "Al(s)" ? "2Al + 2NaOH + 6H₂O → 2Na[Al(OH)₄] + 3H₂↑" : "Zn + 2NaOH + 2H₂O → Na₂[Zn(OH)₄] + H₂↑", Severity.Info, null, "H2", dn * d.IonCharge / 2.0);
                        changed = true;
                    }
                }
                else if ((d.Id == "Cu(s)" || d.Id == "Ag(s)" || d.Id == "Hg(l)") && w.C("NO3-") > 0.3 && w.Ph < 1.5)
                {
                    changed |= MetalNitric(w, s, d, dt);
                }
                else if ((d.Id == "Cu(s)" || d.Id == "Ag(s)") && w.C("SO4") > 10 && env.TempC > 80)
                {
                    double dn = Math.Min(n, 1.5e-4 * dt);
                    b.Add(s, -dn);
                    b.Add(d.IonId, dn);
                    b.Add("SO4", -dn * d.IonCharge / 2.0);
                    Emit(w, EventKind.Gas, "khi:SO2:" + d.Id, $"{Cap(d.Name)} tan trong H₂SO₄ đặc nóng, khí SO₂ mùi hắc", "H₂SO₄ đặc nóng có tính oxi hóa mạnh, tác dụng cả với kim loại đứng sau H.",
                         "Cu + 2H₂SO₄(đặc) → CuSO₄ + SO₂↑ + 2H₂O", Severity.Warning, "khi_doc", "SO2", dn * d.IonCharge / 2.0);
                    changed = true;
                }

                // Kim loại mạnh đẩy kim loại yếu ra khỏi dung dịch muối
                if (!double.IsNaN(d.E0) && d.IonId != null && d.E0 > -2.5)
                {
                    foreach (var (ion, product, e, E) in oxidantIons)
                    {
                        if (E <= d.E0 + 0.05) continue;
                        double cIon = w.C(ion);
                        if (cIon <= 1e-6 || b.Get(s) <= 0) continue;
                        double rate = 6e-4 * Math.Min(1, cIon / 0.05) * stir;
                        double dnM = Math.Min(b.Get(s), rate * dt);
                        double dnIon = dnM * d.IonCharge / e;
                        if (dnIon > b.Get(ion)) { dnIon = b.Get(ion); dnM = dnIon * e / d.IonCharge; }
                        b.Add(s, -dnM);
                        b.Add(d.IonId, dnM);
                        b.Add(ion, -dnIon);
                        b.Add(product, dnIon);
                        var pd = SpeciesRegistry.Get(product);
                        string title = product.EndsWith("(s)") || product.EndsWith("(l)")
                            ? $"{Cap(pd.Name)} bám lên bề mặt {d.Name}" + (ion == "Cu2+" ? ", màu xanh nhạt dần" : "")
                            : $"{Cap(d.Name)} tan trong dung dịch {SpeciesRegistry.Get(ion).Formula}";
                        Emit(w, EventKind.Reaction, $"daykl:{d.Id}:{ion}", title, "Kim loại mạnh hơn đẩy kim loại yếu hơn ra khỏi dung dịch muối (dãy điện hóa).",
                             $"{(d.IonCharge * 1.0 / e == 1 ? "" : "")}{d.Formula} + {SpeciesRegistry.Get(ion).Formula} → {SpeciesRegistry.Get(d.IonId).Formula} + {pd.Formula}",
                             amount: dnIon, color: pd.Color);
                        changed = true;
                    }
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
                // MnO2, PbO2 + HCl đặc → Cl2
                if ((d.Id == "MnO2(s)" || d.Id == "PbO2(s)") && w.C("Cl-") > 4 && w.Ph < 0.5 && env.TempC > (d.Id == "MnO2(s)" ? 40 : 0))
                {
                    double dn = Math.Min(n, 2e-4 * dt);
                    b.Add(s, -dn);
                    b.Add(d.Id == "MnO2(s)" ? "Mn2+" : "Pb2+", dn);
                    b.Add("Cl-", -2 * dn);
                    Emit(w, EventKind.Gas, "khi:Cl2:" + d.Id, "Khí Cl₂ vàng lục thoát ra (độc!)", "Điều chế clo: MnO₂ oxi hóa HCl đặc khi đun nóng.",
                         d.Id == "MnO2(s)" ? "MnO₂ + 4HCl → MnCl₂ + Cl₂↑ + 2H₂O" : "PbO₂ + 4HCl → PbCl₂ + Cl₂↑ + 2H₂O", Severity.Danger, "khi_doc", "Cl2", dn);
                    changed = true;
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

            // H2O2 phân hủy (xúc tác MnO2, Fe3+, đun nóng)
            double h2o2 = b.Get("H2O2");
            if (h2o2 > 1e-10)
            {
                double k = 0;
                string cat = null;
                if (b.Get("MnO2(s)") > 1e-7 || b.Get("MnO2(kt)") > 1e-7) { k += 0.35; cat = "MnO₂"; }
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
                    Emit(w, EventKind.Reaction, "trangbac", "Lớp bạc sáng như gương bám lên thành ống (tráng bạc)", "Nhóm -CHO khử ion bạc trong thuốc thử Tollens khi đun nhẹ.",
                         "RCHO + 2[Ag(NH₃)₂]OH → RCOONH₄ + 2Ag↓ + 3NH₃ + H₂O", amount: dAg, color: SpeciesRegistry.Get("AgMirror").Color);
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
                    Emit(w, EventKind.Precipitate, "kt:Cu2O", "Kết tủa đỏ gạch Cu₂O", "Anđehit (glucozơ) khử Cu(II) trong kiềm khi đun nóng (Fehling, Benedict).",
                         "RCHO + 2Cu(OH)₂ + NaOH → RCOONa + Cu₂O↓ + 3H₂O", amount: dCu / 2, color: SpeciesRegistry.Get("Cu2O").Color);
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
                             "Phản ứng một chiều trong môi trường kiềm.", d.HasTag("chat_beo") ? "(RCOO)₃C₃H₅ + 3NaOH → 3RCOONa + C₃H₅(OH)₃" : "RCOOR' + NaOH → RCOONa + R'OH", amount: dn);
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

        static bool MetalNitric(Work w, int s, SpeciesDef d, double dt)
        {
            double n = w.b.Get(s);
            bool conc = w.C("NO3-") > 6;
            double rate = (d.Id == "Cu(s)" || d.Id == "Ag(s)" ? 3e-4 : 5e-4) * (conc ? 2 : 1);
            double dn = Math.Min(n, rate * dt);
            double electrons = dn * d.IonCharge;
            double no3 = conc ? electrons : electrons / 3;  // NO2: 1 e, NO: 3 e
            if (no3 > w.b.Get("NO3-")) return false;
            w.b.Add(s, -dn);
            w.b.Add(d.IonId, dn);
            w.b.Add("NO3-", -no3);
            string gas = conc ? "NO2" : "NO";
            Emit(w, EventKind.Gas, $"khi:{gas}:{d.Id}", conc ? $"{Cap(d.Name)} tan, khí NO₂ màu nâu đỏ (độc!)" : $"{Cap(d.Name)} tan, khí NO hóa nâu ở miệng ống",
                 "HNO₃ oxi hóa được hầu hết kim loại (kể cả Cu, Ag) nhưng không giải phóng H₂." + (d.Id == "Cu(s)" ? " Dung dịch chuyển xanh (Cu²⁺)." : ""),
                 conc ? "Cu + 4HNO₃(đặc) → Cu(NO₃)₂ + 2NO₂↑ + 2H₂O" : "3Cu + 8HNO₃(loãng) → 3Cu(NO₃)₂ + 2NO↑ + 4H₂O",
                 Severity.Warning, "khi_doc", gas, no3);
            return true;
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
