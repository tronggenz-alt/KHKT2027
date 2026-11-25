using System;
using System.Collections.Generic;

namespace Khkt.Simulation.Chem
{
    public static partial class ChemEngine
    {
        sealed class Work
        {
            public MixBuilder b;
            public double V, h;
            public ChemEnv env;
            public List<ChemEvent> events;
            public Result res;
            public double Ph => -Math.Log10(h);
            public double T => env.TempC;
            public double C(string id) => b.Get(id) / V;
            public double M(string id) => b.Get(id);
        }

        static void Emit(Work w, EventKind kind, string key, string title, string detail, string eq = null,
                         Severity sev = Severity.Info, string hazard = null, string gas = null, double amount = 0, Rgb? color = null)
        {
            if (w.events == null) return;
            foreach (var e in w.events)
                if (e.Key == key) { e.Amount += amount; return; }
            w.events.Add(new ChemEvent
            {
                Kind = kind, Key = key, Title = title, Detail = detail, Equation = eq, Severity = sev, Hazard = hazard,
                GasId = gas, Amount = amount, Color = color ?? Rgb.White,
            });
        }

        // ------------------------------------------------------------------
        // Oxi hóa - khử (bảng phản ứng)
        // ------------------------------------------------------------------

        sealed class Rx
        {
            public string Ox, Red;
            public double EOx, ERed;                    // số electron nhận / nhường trên 1 mol
            public (string id, double n)[] OxP, RedP;   // sản phẩm trên 1 mol chất phản ứng; "khi:X" là khí bay ra
            public double PhMax = 99, PhMin = -99, TMin = -999;
            public Func<Work, bool> When;
            public string Key, Title, Detail, Eq, Hazard;
            public Severity Sev = Severity.Info;
        }

        static List<Rx> redox;

        static (string, double)[] Pr(params (string, double)[] p) => p;

        static void BuildRedox()
        {
            if (redox != null) return;
            redox = new List<Rx>();
            void R(string ox, double eo, (string, double)[] op, string red, double er, (string, double)[] rp, string title, string eq,
                   double phMax = 99, double phMin = -99, double tMin = -999, string detail = null, Func<Work, bool> when = null,
                   string hazard = null, Severity sev = Severity.Info)
            {
                redox.Add(new Rx
                {
                    Ox = ox, EOx = eo, OxP = op, Red = red, ERed = er, RedP = rp, Title = title, Eq = eq, PhMax = phMax, PhMin = phMin,
                    TMin = tMin, Detail = detail, When = when, Hazard = hazard, Sev = sev, Key = "pu:" + ox + "+" + red + (phMax < 99 ? "<" + phMax : ""),
                });
            }

            var mn2 = Pr(("Mn2+", 1));
            var mno2 = Pr(("MnO2(kt)", 1));
            var cr3 = Pr(("Cr3+", 1));
            // Chỉ còn các mẫu theo nhóm chức hữu cơ (chất khử là nhãn "@..."): phản ứng vô cơ do bảng cặp oxi hóa - khử (ChemRedoxTable) suy ra.
            R("MnO4-", 5, mn2, "@andehit", 2, null, "Mất màu tím (anđehit bị oxi hóa)", "2MnO₄⁻ + 5RCHO + 6H⁺ → 2Mn²⁺ + 5RCOOH + 3H₂O", 4);
            R("MnO4-", 5, mn2, "@khong_no", 2, null, "Mất màu tím KMnO₄ (liên kết bội bị oxi hóa)", "RCH=CHR' + [O] (KMnO₄/H⁺) → sản phẩm oxi hóa", 4);
            R("MnO4-", 3, mno2, "@khong_no", 2, null, "Mất màu tím, kết tủa nâu đen MnO₂", "3CH₂=CH₂ + 2KMnO₄ + 4H₂O → 3C₂H₄(OH)₂ + 2MnO₂↓ + 2KOH", 99, 4,
              detail: "Phản ứng nhận biết liên kết đôi, liên kết ba (anken, ankin).");
            R("MnO4-", 3, mno2, "@andehit", 2, null, "Mất màu tím, kết tủa nâu đen MnO₂", "RCHO + KMnO₄ → RCOOK + MnO₂↓", 99, 4);
            R("CrVI", 3, cr3, "@ancol", 2, null, "Da cam chuyển xanh lục (ancol bị oxi hóa)", "Cr₂O₇²⁻ + 3C₂H₅OH + 8H⁺ → 2Cr³⁺ + 3CH₃CHO + 7H₂O", 2,
              detail: "Nguyên lý máy đo nồng độ cồn trong hơi thở.");
            R("CrVI", 3, cr3, "@andehit", 2, null, "Da cam chuyển xanh lục", "Cr₂O₇²⁻ + 3RCHO + 8H⁺ → 2Cr³⁺ + 3RCOOH + 4H₂O", 2);
            R("Br2", 2, Pr(("Br-", 2)), "@khong_no", 2, null, "Nước brom mất màu (phản ứng cộng)", "CH₂=CH₂ + Br₂ → CH₂Br-CH₂Br",
              detail: "Nhận biết hợp chất có liên kết đôi, liên kết ba.");
            R("Br2", 2, Pr(("Br-", 2)), "@andehit", 2, null, "Nước brom mất màu (anđehit bị oxi hóa)", "RCHO + Br₂ + H₂O → RCOOH + 2HBr",
              detail: "Phân biệt glucozơ (làm mất màu) với fructozơ (không).");
            R("Br2", 6, Pr(("Br-", 2)), "@phenol", 1, Pr(("TBP", 1)), "Nước brom mất màu, kết tủa trắng", "C₆H₅OH + 3Br₂ → C₆H₂Br₃OH↓ + 3HBr",
              detail: "Kết tủa 2,4,6-tribromphenol: nhận biết phenol.");
            R("Br2", 6, Pr(("Br-", 2)), "@anilin", 1, Pr(("TBA", 1)), "Nước brom mất màu, kết tủa trắng", "C₆H₅NH₂ + 3Br₂ → C₆H₂Br₃NH₂↓ + 3HBr",
              detail: "Kết tủa 2,4,6-tribromanilin: nhận biết anilin.");
        }

        static bool ApplyRedox(Work w, Rx r)
        {
            if (w.Ph > r.PhMax || w.Ph < r.PhMin || w.T < r.TMin) return false;
            double nOx = w.M(r.Ox);
            if (nOx <= 1e-12) return false;
            List<int> organics = null;
            double nRed;
            if (r.Red[0] == '@')
            {
                string tag = r.Red.Substring(1);
                organics = new List<int>();
                nRed = 0;
                foreach (int s in w.b.Indices)
                {
                    var d = SpeciesRegistry.At(s);
                    if ((d.Kind == SpeciesKind.Molecule || d.Kind == SpeciesKind.Weak) && d.HasTag(tag) && !(tag == "andehit" && d.HasTag("andehit_kiem")))
                    {
                        organics.Add(s);
                        nRed += w.b.Get(s);
                    }
                }
            }
            else nRed = w.M(r.Red);
            if (nRed <= 1e-12) return false;
            if (r.When != null && !r.When(w)) return false;
            double e = Math.Min(nOx * r.EOx, nRed * r.ERed);
            double dOx = e / r.EOx, dRed = e / r.ERed;
            w.b.Add(r.Ox, -dOx);
            if (organics != null)
            {
                double left = dRed;
                foreach (int s in organics) left -= w.b.Take(s, left);
            }
            else w.b.Add(r.Red, -dRed);
            double gas = 0;
            string gasId = null;
            void Products((string id, double n)[] p, double amount)
            {
                if (p == null) return;
                foreach (var (id, n) in p)
                {
                    if (id.StartsWith("khi:")) { gasId = id.Substring(4); gas += n * amount; }
                    else w.b.Add(id, n * amount);
                }
            }
            Products(r.OxP, dOx);
            Products(r.RedP, dRed);
            var ox = SpeciesRegistry.Get(r.Ox);
            string eq = r.Eq;
            if (organics != null && organics.Count > 0)
            {
                // phương trình theo đúng chất hữu cơ đang có (không dùng ví dụ etilen cho axetilen, stiren...)
                int main = organics[0];
                foreach (int s in organics) if (w.b.Get(s) > w.b.Get(main)) main = s;
                eq = OrganicEquation(r, SpeciesRegistry.At(main)) ?? (Equation.Check(r.Eq) == null && !r.Eq.Contains("CH₂=CH₂") ? r.Eq : null);
            }
            Emit(w, gasId != null ? EventKind.Gas : EventKind.Reaction, r.Key, r.Title, r.Detail, eq, r.Sev, r.Hazard, gasId, gasId != null ? gas : dOx, ox?.Color);
            return true;
        }

        // ------------------------------------------------------------------
        // Phản ứng tức thời (không thuận nghịch)
        // ------------------------------------------------------------------

        static bool InstantRules(Work w)
        {
            BuildRedox();
            bool changed = ThptEarly(w);
            foreach (var r in redox) changed |= ApplyRedox(w, r);
            changed |= RedoxTable(w);
            changed |= ThptInstant(w);
            var b = w.b;

            // Ank-1-in + [Ag(NH3)2]+ → kết tủa vàng (Ag⁺ và NH₃ ở dạng tổng sau khi hoàn nguyên phức)
            double agAll = w.M("Ag+") + w.M("AgNH3");
            if (w.Ph > 8 && agAll > 1e-10 && w.M("AgNH3") > 1e-10)
            {
                foreach (int s in b.Snapshot())
                {
                    var d = SpeciesRegistry.At(s);
                    if (!d.HasTag("ank_1_in")) continue;
                    double x = Math.Min(b.Get(s), agAll / 2);
                    if (x <= 1e-12) continue;
                    double fromCx = Math.Min(w.M("AgNH3"), 2 * x);
                    b.Add(s, -x); b.Add("AgNH3", -fromCx); b.Add("NH3", 2 * fromCx); b.Add("Ag+", -(2 * x - fromCx)); b.Add("Ag2C2", x);
                    Emit(w, EventKind.Precipitate, "kt:Ag2C2", "Kết tủa vàng nhạt (bạc axetilua)", "Nhận biết ank-1-in (liên kết ba đầu mạch).",
                         "HC≡CH + 2[Ag(NH₃)₂]⁺ → AgC≡CAg↓ + 2NH₄⁺ + 2NH₃", color: SpeciesRegistry.Get("Ag2C2").Color, amount: x);
                    changed = true;
                }
            }

            // Thuốc thử Nessler + NH3/NH4+ → kết tủa nâu đỏ
            double nes = w.M("nessler");
            if (nes > 1e-12 && w.M("NH3") > 1e-9)
            {
                double x = Math.Min(nes, w.M("NH3"));
                b.Add("nessler", -x); b.Add("NH3", -x); b.Add("NesslerKT", x);
                Emit(w, EventKind.Precipitate, "kt:Nessler", "Kết tủa nâu đỏ (thuốc thử Nessler)", "Nhận biết ion amoni / amoniac rất nhạy.",
                     "NH₄⁺ + 2[HgI₄]²⁻ + 4OH⁻ → HgO·Hg(NH₂)I↓ + 7I⁻ + 3H₂O", color: SpeciesRegistry.Get("NesslerKT").Color, amount: x);
                changed = true;
            }

            // Phenol + FeCl3 → tím; protein + Cu2+ trong kiềm → tím (biure); điphenylamin + NO3- → xanh; protein + HNO3 đặc → vàng
            changed |= ColorProduct(w, "Fe3+", "@phenol", "FePhenol", 1, 0, "phuc:FePhenol", "Màu tím đặc trưng", "Nhận biết phenol bằng FeCl₃.", "6C₆H₅OH + Fe³⁺ → [Fe(OC₆H₅)₆]³⁻ + 6H⁺", w.Ph < 7);
            changed |= ColorProduct(w, "Cu2+", "@protein", "BiuretTim", 1, 1, "mau:biure", "Màu tím (phản ứng màu biure)", "Protein (liên kết peptit) + Cu(OH)₂ trong kiềm cho màu tím.", null, w.Ph > 11);
            changed |= ColorProduct(w, "diphenylamin", "NO3-", "DPAXanh", 1, 0, "mau:dpa", "Màu xanh lam đậm (điphenylamin)", "Nhận biết ion nitrat trong H₂SO₄ đặc.", null, true);
            changed |= ColorProduct(w, "NO3-", "@protein", "XanthoVang", 0, 1, "mau:xantoprotein", "Protein chuyển màu vàng", "HNO₃ đặc nitro hóa vòng benzen trong protein (phản ứng xantoprotein).", null, w.C("NO3-") > 5 && w.Ph < 0.5);
            changed |= ColorProduct(w, "naphtol", "@cacbohidrat", "MolischTim", 1, 0, "mau:molisch", "Vòng màu tím (phản ứng Molisch)", "Nhận biết cacbohiđrat với α-naphtol trong H₂SO₄ đặc.", null, w.C("SO4") > 8);
            changed |= ColorProduct(w, "schiff", "@andehit", "SchiffDo", 1, 0, "mau:schiff", "Màu hồng tím (thuốc thử Schiff)", "Nhận biết nhóm anđehit.", null, true);
            changed |= ColorProduct(w, "Hg2+", "@protein", "MillonDo", 0, 1, "kt:millon", "Kết tủa đỏ gạch (thuốc thử Millon)", "Protein chứa tyrosin cho kết tủa đỏ với thuốc thử Millon khi đun.", null, w.T > 60 && w.C("NO3-") > 1);

            // H2SO4 đặc làm than hóa cacbohiđrat (đường hóa đen, sủi bọt SO2, CO2)
            if (w.C("SO4") > 10)
            {
                foreach (int s in b.Snapshot())
                {
                    var d = SpeciesRegistry.At(s);
                    if (!(d.HasTag("cacbohidrat") || (d.Kind == SpeciesKind.Lump && d.HasTag("cacbohidrat")))) continue;
                    double x = b.Get(s);
                    if (x <= 1e-9) continue;
                    b.Set(s, 0);
                    b.Add("C(than)", 6 * x);
                    w.res.HeatJ += x * 600e3;
                    Emit(w, EventKind.Hazard, "pu:thanhoa", "Đường hóa đen, phồng lên, sủi khí SO₂ (H₂SO₄ đặc háo nước)", "H₂SO₄ đặc lấy nước của cacbohiđrat để lại cacbon; phản ứng tỏa nhiều nhiệt.",
                         "C₁₂H₂₂O₁₁ → 12C + 11H₂O (H₂SO₄ đặc); C + 2H₂SO₄ → CO₂ + 2SO₂ + 2H₂O", Severity.Warning, "khi_doc", "SO2", x * 2, SpeciesRegistry.Get("C(than)").Color);
                    changed = true;
                }
            }

            // Chất lỏng hữu cơ không tan: tách lớp; I2/Br2 chuyển vào lớp hữu cơ (màu tím / da cam)
            foreach (int s in b.Indices)
            {
                var d = SpeciesRegistry.At(s);
                if (d.Kind == SpeciesKind.Molecule && d.Insoluble && b.Get(s) * d.MolarMass / Math.Max(0.5, d.Density) > 0.2)  // > 0,2 ml
                {
                    bool heavy = d.HasTag("nang");
                    Emit(w, EventKind.Layer, "lop:" + d.Id, $"Tách lớp: {d.Name} {(heavy ? "nằm dưới" : "nổi trên")} nước",
                         heavy ? "Chất hữu cơ không tan, nặng hơn nước." : "Chất hữu cơ không tan, nhẹ hơn nước.");
                    if (w.M("I2") > 1e-7) Emit(w, EventKind.Color, "lop:I2", "Lớp hữu cơ có màu tím (iot tan vào)", "I₂ tan trong dung môi hữu cơ tốt hơn trong nước: phương pháp chiết.");
                    break;
                }
            }
            return changed;
        }

        /// <summary>Phản ứng tạo màu: a + b → sản phẩm (tiêu thụ a nếu consumeA = 1, b nếu consumeB = 1).</summary>
        static bool ColorProduct(Work w, string a, string bId, string product, int consumeA, int consumeB, string key, string title, string detail, string eq, bool cond)
        {
            if (!cond) return false;
            double na = a[0] == '@' ? TagAmount(w.b, a.Substring(1)) : w.M(a);
            double nb = bId[0] == '@' ? TagAmount(w.b, bId.Substring(1)) : w.M(bId);
            if (na <= 1e-12 || nb <= 1e-12) return false;
            double x = Math.Min(na, nb);
            if (SpeciesRegistry.Get(product) == null) RegisterColorProduct(product);
            if (consumeA == 1) TakeTag(w.b, a, x);
            if (consumeB == 1) TakeTag(w.b, bId, x);
            w.b.Add(product, x);
            var pd = SpeciesRegistry.Get(product);
            Emit(w, pd.Kind == SpeciesKind.Precipitate ? EventKind.Precipitate : EventKind.Color, key, title, detail, eq, amount: x, color: pd.Color);
            return true;
        }

        static void RegisterColorProduct(string id)
        {
            switch (id)
            {
                case "BiuretTim": { var s = SpeciesRegistry.Add(id, "phức biure", SpeciesKind.Molecule); s.Color = Rgb.Hex("#7A3AA8"); s.ColorStrength = 60; break; }
                case "DPAXanh": { var s = SpeciesRegistry.Add(id, "sản phẩm điphenylamin", SpeciesKind.Molecule); s.Color = Rgb.Hex("#1A3A9A"); s.ColorStrength = 400; break; }
                case "XanthoVang": { var s = SpeciesRegistry.Add(id, "protein nitro hóa", SpeciesKind.Molecule); s.Color = Rgb.Hex("#E8B21A"); s.ColorStrength = 40; break; }
                case "MolischTim": { var s = SpeciesRegistry.Add(id, "sản phẩm Molisch", SpeciesKind.Molecule); s.Color = Rgb.Hex("#6A1A6A"); s.ColorStrength = 300; break; }
                case "SchiffDo": { var s = SpeciesRegistry.Add(id, "sản phẩm Schiff", SpeciesKind.Molecule); s.Color = Rgb.Hex("#C21A7A"); s.ColorStrength = 500; break; }
                case "RuhemannTim": { var s = SpeciesRegistry.Add(id, "tím Ruhemann", SpeciesKind.Molecule); s.Color = Rgb.Hex("#5A2A8A"); s.ColorStrength = 500; break; }
                case "MillonDo": { var s = SpeciesRegistry.Add(id, "kết tủa Millon", SpeciesKind.Precipitate); s.Color = Rgb.Hex("#A3201A"); s.ColorStrength = 1; break; }
            }
        }

        static double TagAmount(MixBuilder b, string tag)
        {
            double n = 0;
            foreach (int s in b.Indices) if (SpeciesRegistry.At(s).HasTag(tag) && SpeciesRegistry.At(s).Kind != SpeciesKind.Lump) n += b.Get(s);
            return n;
        }

        static void TakeTag(MixBuilder b, string idOrTag, double x)
        {
            if (idOrTag[0] != '@') { b.Add(idOrTag, -x); return; }
            string tag = idOrTag.Substring(1);
            foreach (int s in b.Snapshot())
            {
                if (x <= 0) break;
                var d = SpeciesRegistry.At(s);
                if (d.HasTag(tag) && d.Kind != SpeciesKind.Lump) x -= b.Take(s, x);
            }
        }

        // ------------------------------------------------------------------
        // Khí thoát ra
        // ------------------------------------------------------------------

        static bool GasEscape(Work w)
        {
            var b = w.b;
            double V = w.V, h = w.h;
            bool hot = w.T > 55;
            bool any = false;
            // khí không tan, không phản ứng (H₂, O₂, N₂, CO...): sục vào dung dịch thì thoát hết ra
            foreach (int s in b.Snapshot()) if (SpeciesRegistry.At(s).HasTag("khi_tro")) b.Set(s, 0);
            bool Weak(string id, double solubility, string gas, string title, string detail, string eq, string hazard, Severity sev, double smell = double.MaxValue, string smellTitle = null)
            {
                var d = SpeciesRegistry.Get(id);
                double tot = b.Get(d.Index) / V;
                if (tot <= 0) return false;
                double a0 = id == "NH3" ? 1 - AlphaFirst(d, h) : AlphaFirst(d, h);
                double c = tot * a0;
                if (c > smell && smellTitle != null) Emit(w, EventKind.Hazard, "mui:" + gas, smellTitle, detail, null, Severity.Notice, hazard);
                if (c <= solubility * 1.02) return false;
                // định luật Henry: lượng còn lại sao cho dạng khí tự do bằng độ tan ở pH SAU khi khí thoát (pH đổi khi khí bay ra)
                double total = b.Get(d.Index), lo = 0, hi = total;
                for (int it = 0; it < 40; it++)
                {
                    double x = 0.5 * (lo + hi);
                    b.Add(d.Index, -x);
                    double hx = SolveH(b, V);
                    b.Add(d.Index, x);
                    double freeX = (total - x) / V * (id == "NH3" ? 1 - AlphaFirst(d, hx) : AlphaFirst(d, hx));
                    if (freeX > solubility) lo = x; else hi = x;
                }
                double mol = Math.Min(total, hi);
                b.Add(d.Index, -mol);
                Emit(w, EventKind.Gas, "khi:" + gas, title, detail, eq, sev, hazard, gas, mol);
                return true;
            }
            any |= Weak("CO3", hot ? 0.012 : 0.034, "CO2", "Sủi bọt khí không màu (CO₂)", "Muối cacbonat gặp axit giải phóng CO₂; khí làm đục nước vôi trong.",
                        "CO₃²⁻ + 2H⁺ → CO₂↑ + H₂O", null, Severity.Info);
            any |= Weak("SO3", hot ? 0.3 : 1.2, "SO2", "Khí SO₂ mùi hắc thoát ra", "SO₂ độc, làm mất màu dung dịch brom, KMnO₄.",
                        "SO₃²⁻ + 2H⁺ → SO₂↑ + H₂O", "khi_doc", Severity.Warning, 0.05, "Có mùi hắc của SO₂");
            any |= Weak("S", hot ? 0.03 : 0.1, "H2S", "Khí H₂S mùi trứng thối thoát ra", "H₂S rất độc, làm đen giấy tẩm Pb(NO₃)₂.",
                        "S²⁻ + 2H⁺ → H₂S↑", "khi_doc", Severity.Warning, 0.005, "Có mùi trứng thối (H₂S)");
            any |= Weak("NH3", hot ? 0.3 : 1.5, "NH3", "Khí NH₃ mùi khai thoát ra", "Amoniac làm quỳ tím ẩm hóa xanh, tạo khói trắng với HCl đặc.",
                        "NH₄⁺ + OH⁻ → NH₃↑ + H₂O (đun nóng)", "khi_doc", Severity.Notice, 0.05, "Có mùi khai (NH₃)");
            any |= Weak("CN", 0.005, "HCN", "Khí HCN cực độc thoát ra!", "Xianua gặp axit giải phóng HCN: tuyệt đối không làm ngoài tủ hút.",
                        "CN⁻ + H⁺ → HCN↑", "khi_doc", Severity.Danger);
            any |= Weak("NO2", 0.15, "NO", "Khí NO hóa nâu đỏ NO₂ trong không khí", "Axit nitrơ không bền, phân hủy.",
                        "3HNO₂ → HNO₃ + 2NO↑ + H₂O", "khi_doc", Severity.Warning);
            double cl2 = b.Get("Cl2") / V;
            if (cl2 > 0.09)
            {
                double mol = (cl2 - 0.09) * V;
                b.Add("Cl2", -mol);
                Emit(w, EventKind.Gas, "khi:Cl2", "Khí Cl₂ vàng lục thoát ra (độc!)", "Clo gây ngạt, phải làm trong tủ hút.", null, Severity.Danger, "khi_doc", "Cl2", mol);
                any = true;
            }
            if (b.Get("Br2") / V > 0.2)
                Emit(w, EventKind.Hazard, "mui:Br2", "Hơi brom nâu đỏ bốc lên (độc, ăn da)", "Brom lỏng dễ bay hơi; thao tác trong tủ hút, đeo găng.", null, Severity.Warning, "khi_doc");
            if (b.Get("NO3-") / V > 6 && h > 3)
                Emit(w, EventKind.Hazard, "mui:HNO3", "Axit nitric đặc bốc khói", "HNO₃ đặc dễ bay hơi, phân hủy ra NO₂ độc.", null, Severity.Notice, "khi_doc");
            // HCl, HNO₃ là axit dễ bay hơi: khi đun, tích [H⁺][X⁻] lớn thì khí thoát ra (điều chế HCl từ NaCl rắn + H₂SO₄ đặc)
            if (w.T > 50)
                foreach (var (ion, gas, limit) in new[] { ("Cl-", "HCl", 30.0), ("NO3-", "HNO3", 60.0) })
                {
                    double c = b.Get(ion) / V, prod = c * h;
                    if (prod <= limit || c <= 0) continue;
                    double mol = Math.Min(b.Get(ion), (prod - limit) / Math.Max(h, 1e-6) * V * 0.5);
                    if (mol <= 1e-9) continue;
                    b.Add(ion, -mol);
                    Emit(w, EventKind.Gas, "khi:" + gas, gas == "HCl" ? "Khí HCl thoát ra, gặp hơi ẩm tạo khói trắng" : "Hơi HNO₃ thoát ra",
                         "Axit dễ bay hơi thoát ra khi đun dung dịch có nồng độ H⁺ và gốc axit lớn (axit khó bay hơi đẩy axit dễ bay hơi).",
                         gas == "HCl" ? "NaCl + H₂SO₄ → NaHSO₄ + HCl↑ (t°)" : null, Severity.Warning, "khi_doc", gas, mol);
                    any = true;
                }
            if (b.Get("Cl-") / V > 8 && h > 6)
                Emit(w, EventKind.Hazard, "mui:HCl", "HCl đặc bốc khói", "Hơi HCl gây kích ứng mắt và đường hô hấp.", null, Severity.Notice, "khi_doc");
            return any;
        }
    }
}
