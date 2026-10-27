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
            // --- KMnO4 trong axit ---
            R("MnO4-", 5, mn2, "Fe2+", 1, Pr(("Fe3+", 1)), "Màu tím KMnO₄ mất ngay (Fe²⁺ bị oxi hóa)", "MnO₄⁻ + 5Fe²⁺ + 8H⁺ → Mn²⁺ + 5Fe³⁺ + 4H₂O", 4,
              detail: "Fe²⁺ khử MnO₄⁻ (tím) thành Mn²⁺ (gần như không màu). Cơ sở phép chuẩn độ pemanganat.");
            R("MnO4-", 5, mn2, "H2O2", 2, Pr(("khi:O2", 1)), "Mất màu tím, sủi bọt khí O₂", "2MnO₄⁻ + 5H₂O₂ + 6H⁺ → 2Mn²⁺ + 5O₂↑ + 8H₂O", 4,
              detail: "Ở đây H₂O₂ đóng vai trò chất khử.");
            R("MnO4-", 5, mn2, "I-", 1, Pr(("I2", 0.5)), "Mất màu tím, dung dịch chuyển nâu (I₂)", "2MnO₄⁻ + 10I⁻ + 16H⁺ → 2Mn²⁺ + 5I₂ + 8H₂O", 4);
            R("MnO4-", 5, mn2, "SO3", 2, Pr(("SO4", 1)), "Mất màu tím (SO₃²⁻ khử MnO₄⁻)", "2MnO₄⁻ + 5SO₃²⁻ + 6H⁺ → 2Mn²⁺ + 5SO₄²⁻ + 3H₂O", 4);
            R("MnO4-", 5, mn2, "S", 2, Pr(("S(kt)", 1)), "Mất màu tím, vẩn đục vàng lưu huỳnh", "2MnO₄⁻ + 5H₂S + 6H⁺ → 2Mn²⁺ + 5S↓ + 8H₂O", 4);
            R("MnO4-", 5, mn2, "Ox", 2, Pr(("khi:CO2", 2)), "Mất màu tím, sủi bọt CO₂ (cần đun nóng)", "2MnO₄⁻ + 5C₂O₄²⁻ + 16H⁺ → 2Mn²⁺ + 10CO₂↑ + 8H₂O", 4, tMin: 55,
              detail: "Phản ứng chậm ở nhiệt độ thường, nhanh dần khi đun và khi có Mn²⁺ xúc tác.");
            R("MnO4-", 5, mn2, "Cl-", 1, Pr(("khi:Cl2", 0.5)), "Khí Cl₂ vàng lục thoát ra (độc!)", "2KMnO₄ + 16HCl → 2KCl + 2MnCl₂ + 5Cl₂↑ + 8H₂O", 1,
              when: w => w.C("Cl-") > 3, hazard: "khi_doc", sev: Severity.Danger, detail: "Điều chế clo trong phòng thí nghiệm: phải làm trong tủ hút.");
            R("MnO4-", 5, mn2, "Br-", 1, Pr(("Br2", 0.5)), "Mất màu tím, dung dịch vàng nâu (Br₂)", "2MnO₄⁻ + 10Br⁻ + 16H⁺ → 2Mn²⁺ + 5Br₂ + 8H₂O", 2);
            R("MnO4-", 5, mn2, "NO2", 2, Pr(("NO3-", 1)), "Mất màu tím (nitrit bị oxi hóa)", "2MnO₄⁻ + 5NO₂⁻ + 6H⁺ → 2Mn²⁺ + 5NO₃⁻ + 3H₂O", 4);
            R("MnO4-", 5, mn2, "Sn2+", 2, Pr(("Sn4+", 1)), "Mất màu tím", "2MnO₄⁻ + 5Sn²⁺ + 16H⁺ → 2Mn²⁺ + 5Sn⁴⁺ + 8H₂O", 4);
            R("MnO4-", 5, mn2, "@andehit", 2, null, "Mất màu tím (anđehit bị oxi hóa)", "2MnO₄⁻ + 5RCHO + 6H⁺ → 2Mn²⁺ + 5RCOOH + 3H₂O", 4);
            R("MnO4-", 5, mn2, "@khong_no", 2, null, "Mất màu tím KMnO₄ (liên kết bội bị oxi hóa)", "RCH=CHR' + [O] (KMnO₄/H⁺) → sản phẩm oxi hóa", 4);
            // --- KMnO4 trong môi trường trung tính / kiềm: MnO2 nâu đen hoặc manganat xanh lục ---
            R("MnO4-", 3, mno2, "SO3", 2, Pr(("SO4", 1)), "Mất màu tím, kết tủa nâu đen MnO₂", "2MnO₄⁻ + 3SO₃²⁻ + H₂O → 2MnO₂↓ + 3SO₄²⁻ + 2OH⁻", 12, 4);
            R("MnO4-", 3, mno2, "@khong_no", 2, null, "Mất màu tím, kết tủa nâu đen MnO₂", "3CH₂=CH₂ + 2KMnO₄ + 4H₂O → 3C₂H₄(OH)₂ + 2MnO₂↓ + 2KOH", 99, 4,
              detail: "Phản ứng nhận biết liên kết đôi, liên kết ba (anken, ankin).");
            R("MnO4-", 3, mno2, "@andehit", 2, null, "Mất màu tím, kết tủa nâu đen MnO₂", "RCHO + KMnO₄ → RCOOK + MnO₂↓", 99, 4);
            R("MnO4-", 3, mno2, "Fe2+", 1, Pr(("Fe3+", 1)), "Mất màu tím, có kết tủa nâu", "MnO₄⁻ + 3Fe²⁺ + ... → MnO₂↓ + 3Fe³⁺", 12, 4);
            R("MnO4-", 3, mno2, "H2O2", 2, Pr(("khi:O2", 1)), "Mất màu tím, sủi bọt O₂, kết tủa MnO₂", "2MnO₄⁻ + 3H₂O₂ → 2MnO₂↓ + 3O₂↑ + 2OH⁻ + 2H₂O", 12, 4);
            R("MnO4-", 3, mno2, "I-", 1, Pr(("I2", 0.5)), "Mất màu tím, kết tủa nâu MnO₂ và I₂", "2MnO₄⁻ + 6I⁻ + 4H₂O → 2MnO₂↓ + 3I₂ + 8OH⁻", 12, 4);
            R("MnO4-", 1, Pr(("MnO42-", 1)), "SO3", 2, Pr(("SO4", 1)), "Dung dịch chuyển xanh lục (manganat)", "2MnO₄⁻ + SO₃²⁻ + 2OH⁻ → 2MnO₄²⁻ + SO₄²⁻ + H₂O", 99, 12);
            // --- Cr(VI) trong axit: da cam → xanh lục ---
            var cr3 = Pr(("Cr3+", 1));
            R("CrVI", 3, cr3, "Fe2+", 1, Pr(("Fe3+", 1)), "Da cam chuyển xanh lục (Cr³⁺)", "Cr₂O₇²⁻ + 6Fe²⁺ + 14H⁺ → 2Cr³⁺ + 6Fe³⁺ + 7H₂O", 3);
            R("CrVI", 3, cr3, "I-", 1, Pr(("I2", 0.5)), "Dung dịch xanh lục, giải phóng I₂", "Cr₂O₇²⁻ + 6I⁻ + 14H⁺ → 2Cr³⁺ + 3I₂ + 7H₂O", 3);
            R("CrVI", 3, cr3, "SO3", 2, Pr(("SO4", 1)), "Da cam chuyển xanh lục", "Cr₂O₇²⁻ + 3SO₃²⁻ + 8H⁺ → 2Cr³⁺ + 3SO₄²⁻ + 4H₂O", 4);
            R("CrVI", 3, cr3, "S", 2, Pr(("S(kt)", 1)), "Xanh lục, vẩn đục lưu huỳnh", "Cr₂O₇²⁻ + 3H₂S + 8H⁺ → 2Cr³⁺ + 3S↓ + 7H₂O", 3);
            R("CrVI", 3, cr3, "@ancol", 2, null, "Da cam chuyển xanh lục (ancol bị oxi hóa)", "Cr₂O₇²⁻ + 3C₂H₅OH + 8H⁺ → 2Cr³⁺ + 3CH₃CHO + 7H₂O", 2,
              detail: "Nguyên lý máy đo nồng độ cồn trong hơi thở.");
            R("CrVI", 3, cr3, "@andehit", 2, null, "Da cam chuyển xanh lục", "Cr₂O₇²⁻ + 3RCHO + 8H⁺ → 2Cr³⁺ + 3RCOOH + 4H₂O", 2);
            R("CrVI", 3, cr3, "H2O2", 2, Pr(("khi:O2", 1)), "Da cam chuyển xanh lục, sủi bọt", "Cr₂O₇²⁻ + 3H₂O₂ + 8H⁺ → 2Cr³⁺ + 3O₂↑ + 7H₂O", 2);
            // --- Halogen ---
            R("Cl2", 2, Pr(("Cl-", 2)), "Br-", 1, Pr(("Br2", 0.5)), "Dung dịch chuyển vàng nâu (Br₂)", "Cl₂ + 2Br⁻ → 2Cl⁻ + Br₂", 9, detail: "Clo hoạt động mạnh hơn brom.");
            R("Cl2", 2, Pr(("Cl-", 2)), "I-", 1, Pr(("I2", 0.5)), "Dung dịch chuyển nâu (I₂)", "Cl₂ + 2I⁻ → 2Cl⁻ + I₂", 9, detail: "Có hồ tinh bột thì chuyển xanh tím.");
            R("Cl2", 2, Pr(("Cl-", 2)), "Fe2+", 1, Pr(("Fe3+", 1)), "Lục nhạt chuyển vàng nâu (Fe³⁺)", "Cl₂ + 2Fe²⁺ → 2Cl⁻ + 2Fe³⁺");
            R("Cl2", 2, Pr(("Cl-", 2)), "SO3", 2, Pr(("SO4", 1)), "Clo mất màu", "Cl₂ + SO₃²⁻ + H₂O → 2Cl⁻ + SO₄²⁻ + 2H⁺");
            R("Cl2", 2, Pr(("Cl-", 2)), "S", 2, Pr(("S(kt)", 1)), "Vẩn đục vàng lưu huỳnh", "Cl₂ + H₂S → 2HCl + S↓");
            R("Cl2", 2, Pr(("Cl-", 2)), "S2O32-", 8, Pr(("SO4", 2)), "Clo bị khử bởi thiosunfat", "4Cl₂ + S₂O₃²⁻ + 5H₂O → 8Cl⁻ + 2SO₄²⁻ + 10H⁺");
            R("Br2", 2, Pr(("Br-", 2)), "I-", 1, Pr(("I2", 0.5)), "Brom oxi hóa iotua thành I₂", "Br₂ + 2I⁻ → 2Br⁻ + I₂", 9);
            R("Br2", 2, Pr(("Br-", 2)), "Fe2+", 1, Pr(("Fe3+", 1)), "Nước brom mất màu", "Br₂ + 2Fe²⁺ → 2Br⁻ + 2Fe³⁺");
            R("Br2", 2, Pr(("Br-", 2)), "SO3", 2, Pr(("SO4", 1)), "Nước brom mất màu", "Br₂ + SO₃²⁻ + H₂O → 2Br⁻ + SO₄²⁻ + 2H⁺");
            R("Br2", 2, Pr(("Br-", 2)), "S", 2, Pr(("S(kt)", 1)), "Nước brom mất màu, vẩn đục vàng", "Br₂ + H₂S → 2HBr + S↓");
            R("Br2", 2, Pr(("Br-", 2)), "@khong_no", 2, null, "Nước brom mất màu (phản ứng cộng)", "CH₂=CH₂ + Br₂ → CH₂Br-CH₂Br",
              detail: "Nhận biết hợp chất có liên kết đôi, liên kết ba.");
            R("Br2", 2, Pr(("Br-", 2)), "@andehit", 2, null, "Nước brom mất màu (anđehit bị oxi hóa)", "RCHO + Br₂ + H₂O → RCOOH + 2HBr",
              detail: "Phân biệt glucozơ (làm mất màu) với fructozơ (không).");
            R("Br2", 6, Pr(("Br-", 2)), "@phenol", 1, Pr(("TBP", 1)), "Nước brom mất màu, kết tủa trắng", "C₆H₅OH + 3Br₂ → C₆H₂Br₃OH↓ + 3HBr",
              detail: "Kết tủa 2,4,6-tribromphenol: nhận biết phenol.");
            R("Br2", 6, Pr(("Br-", 2)), "@anilin", 1, Pr(("TBA", 1)), "Nước brom mất màu, kết tủa trắng", "C₆H₅NH₂ + 3Br₂ → C₆H₂Br₃NH₂↓ + 3HBr",
              detail: "Kết tủa 2,4,6-tribromanilin: nhận biết anilin.");
            R("I2", 2, Pr(("I-", 2)), "S2O32-", 1, Pr(("S4O62-", 0.5)), "Màu nâu của I₂ mất dần", "I₂ + 2S₂O₃²⁻ → 2I⁻ + S₄O₆²⁻",
              detail: "Cơ sở phép chuẩn độ iot - thiosunfat (hồ tinh bột làm chỉ thị).");
            R("I2", 2, Pr(("I-", 2)), "SO3", 2, Pr(("SO4", 1)), "Màu iot mất", "I₂ + SO₃²⁻ + H₂O → 2I⁻ + SO₄²⁻ + 2H⁺");
            R("I2", 2, Pr(("I-", 2)), "S", 2, Pr(("S(kt)", 1)), "Màu iot mất, vẩn đục vàng", "I₂ + H₂S → 2HI + S↓");
            R("I2", 2, Pr(("I-", 2)), "Sn2+", 2, Pr(("Sn4+", 1)), "Màu iot mất", "I₂ + Sn²⁺ → 2I⁻ + Sn⁴⁺");
            // --- Fe3+ ---
            R("Fe3+", 1, Pr(("Fe2+", 1)), "I-", 1, Pr(("I2", 0.5)), "Dung dịch vàng chuyển nâu (I₂)", "2Fe³⁺ + 2I⁻ → 2Fe²⁺ + I₂", 4);
            R("Fe3+", 1, Pr(("Fe2+", 1)), "S", 2, Pr(("S(kt)", 1)), "Vàng nhạt dần, vẩn đục lưu huỳnh", "2Fe³⁺ + H₂S → 2Fe²⁺ + S↓ + 2H⁺", 4);
            R("Fe3+", 1, Pr(("Fe2+", 1)), "SO3", 2, Pr(("SO4", 1)), "Màu vàng nâu nhạt dần", "2Fe³⁺ + SO₃²⁻ + H₂O → 2Fe²⁺ + SO₄²⁻ + 2H⁺", 4);
            R("Fe3+", 1, Pr(("Fe2+", 1)), "Sn2+", 2, Pr(("Sn4+", 1)), "Màu vàng mất", "2Fe³⁺ + Sn²⁺ → 2Fe²⁺ + Sn⁴⁺");
            // --- H2O2 là chất oxi hóa ---
            R("H2O2", 2, null, "I-", 1, Pr(("I2", 0.5)), "Dung dịch chuyển nâu vàng (I₂)", "H₂O₂ + 2I⁻ + 2H⁺ → I₂ + 2H₂O", 6);
            R("H2O2", 2, null, "Fe2+", 1, Pr(("Fe3+", 1)), "Lục nhạt chuyển vàng nâu", "H₂O₂ + 2Fe²⁺ + 2H⁺ → 2Fe³⁺ + 2H₂O", 6);
            R("H2O2", 2, null, "S", 2, Pr(("S(kt)", 1)), "Vẩn đục vàng lưu huỳnh", "H₂O₂ + H₂S → S↓ + 2H₂O");
            R("H2O2", 2, null, "SO3", 2, Pr(("SO4", 1)), "SO₃²⁻ bị oxi hóa thành SO₄²⁻", "H₂O₂ + SO₃²⁻ → SO₄²⁻ + H₂O");
            // --- Nước Gia-ven (ClO⁻) ---
            R("ClO", 2, Pr(("Cl-", 1)), "Cl-", 1, Pr(("khi:Cl2", 0.5)), "Khí Cl₂ thoát ra (độc!)", "NaClO + 2HCl → NaCl + Cl₂↑ + H₂O", 5,
              hazard: "khi_doc", sev: Severity.Danger, detail: "Không trộn nước tẩy Javen với chất tẩy có axit.");
            R("ClO", 2, Pr(("Cl-", 1)), "I-", 1, Pr(("I2", 0.5)), "Giải phóng I₂ (nâu)", "ClO⁻ + 2I⁻ + H₂O → Cl⁻ + I₂ + 2OH⁻");
            R("ClO", 2, Pr(("Cl-", 1)), "Fe2+", 1, Pr(("Fe3+", 1)), "Fe²⁺ bị oxi hóa", "ClO⁻ + 2Fe²⁺ + 2H⁺ → Cl⁻ + 2Fe³⁺ + H₂O");
            R("ClO", 2, Pr(("Cl-", 1)), "SO3", 2, Pr(("SO4", 1)), "Sunfit bị oxi hóa", "ClO⁻ + SO₃²⁻ → Cl⁻ + SO₄²⁻");
            // --- Nitrat đặc, nitrit, iođat, pesunfat, Cu2+ + I- ---
            R("NO3-", 3, Pr(("khi:NO", 1)), "Fe2+", 1, Pr(("Fe3+", 1)), "Khí NO hóa nâu trong không khí", "3Fe²⁺ + NO₃⁻ + 4H⁺ → 3Fe³⁺ + NO↑ + 2H₂O", 0.5,
              when: w => w.C("NO3-") > 1, hazard: "khi_doc", sev: Severity.Warning);
            R("NO2", 1, Pr(("khi:NO", 1)), "I-", 1, Pr(("I2", 0.5)), "Giải phóng I₂ và khí NO", "2NO₂⁻ + 2I⁻ + 4H⁺ → 2NO↑ + I₂ + 2H₂O", 4);
            R("IO3-", 5, Pr(("I2", 0.5)), "I-", 1, Pr(("I2", 0.5)), "Dung dịch chuyển nâu (I₂)", "IO₃⁻ + 5I⁻ + 6H⁺ → 3I₂ + 3H₂O", 5);
            R("S2O82-", 2, Pr(("SO4", 2)), "I-", 1, Pr(("I2", 0.5)), "Giải phóng I₂ (chậm dần)", "S₂O₈²⁻ + 2I⁻ → 2SO₄²⁻ + I₂");
            R("S2O82-", 2, Pr(("SO4", 2)), "Fe2+", 1, Pr(("Fe3+", 1)), "Fe²⁺ bị oxi hóa", "S₂O₈²⁻ + 2Fe²⁺ → 2SO₄²⁻ + 2Fe³⁺");
            R("Cu2+", 1, Pr(("CuI(s)", 1)), "I-", 2, Pr(("I2", 0.5)), "Kết tủa trắng CuI, dung dịch nâu I₂", "2Cu²⁺ + 4I⁻ → 2CuI↓ + I₂");
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
            Emit(w, gasId != null ? EventKind.Gas : EventKind.Reaction, r.Key, r.Title, r.Detail, r.Eq, r.Sev, r.Hazard, gasId, gasId != null ? gas : dOx, ox?.Color);
            return true;
        }

        // ------------------------------------------------------------------
        // Phản ứng tức thời (không thuận nghịch)
        // ------------------------------------------------------------------

        static bool InstantRules(Work w)
        {
            BuildRedox();
            bool changed = false;
            foreach (var r in redox) changed |= ApplyRedox(w, r);
            var b = w.b;

            // Halogen trong kiềm: tự oxi hóa - khử (mất màu)
            if (w.Ph > 9)
            {
                double cl2 = w.M("Cl2");
                if (cl2 > 1e-10)
                {
                    b.Set("Cl2", 0); b.Add("Cl-", cl2); b.Add("ClO", cl2);
                    Emit(w, EventKind.Reaction, "pu:Cl2+OH", "Clo mất màu trong kiềm (tạo nước Gia-ven)", null, "Cl₂ + 2NaOH → NaCl + NaClO + H₂O");
                    changed = true;
                }
                double br2 = w.M("Br2");
                if (br2 > 1e-10)
                {
                    b.Set("Br2", 0); b.Add("Br-", br2 * 5 / 3); b.Add("BrO3-", br2 / 3);
                    Emit(w, EventKind.Reaction, "pu:Br2+OH", "Brom mất màu trong kiềm", null, "3Br₂ + 6OH⁻ → 5Br⁻ + BrO₃⁻ + 3H₂O");
                    changed = true;
                }
                double i2 = w.M("I2");
                if (i2 > 1e-10 && w.Ph > 10)
                {
                    b.Set("I2", 0); b.Add("I-", i2 * 5 / 3); b.Add("IO3-", i2 / 3);
                    Emit(w, EventKind.Reaction, "pu:I2+OH", "Màu iot mất trong kiềm", null, "3I₂ + 6OH⁻ → 5I⁻ + IO₃⁻ + 3H₂O");
                    changed = true;
                }
            }

            // Ank-1-in + [Ag(NH3)2]+ → kết tủa vàng (Ag⁺ và NH₃ ở dạng tổng sau khi hoàn nguyên phức)
            if (w.Ph > 8 && w.M("Ag+") > 1e-10 && w.M("NH3") > 2 * w.M("Ag+"))
            {
                foreach (int s in b.Snapshot())
                {
                    var d = SpeciesRegistry.At(s);
                    if (!d.HasTag("ank_1_in")) continue;
                    double x = Math.Min(b.Get(s), w.M("Ag+") / 2);
                    if (x <= 1e-12) continue;
                    b.Add(s, -x); b.Add("Ag+", -2 * x); b.Add("Ag2C2", x);
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
            bool Weak(string id, double solubility, string gas, string title, string detail, string eq, string hazard, Severity sev, double smell = double.MaxValue, string smellTitle = null)
            {
                var d = SpeciesRegistry.Get(id);
                double tot = b.Get(d.Index) / V;
                if (tot <= 0) return false;
                double a0 = id == "NH3" ? 1 - AlphaFirst(d, h) : AlphaFirst(d, h);
                double c = tot * a0;
                if (c > smell && smellTitle != null) Emit(w, EventKind.Hazard, "mui:" + gas, smellTitle, detail, null, Severity.Notice, hazard);
                if (c <= solubility * 1.02) return false;
                double removeTot = (c - solubility) / Math.Max(a0, 1e-9);
                double mol = Math.Min(b.Get(d.Index), removeTot * V);
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
            any |= Weak("NH3", hot ? 0.02 : 1.5, "NH3", "Khí NH₃ mùi khai thoát ra", "Amoniac làm quỳ tím ẩm hóa xanh, tạo khói trắng với HCl đặc.",
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
            if (b.Get("Cl-") / V > 8 && h > 6)
                Emit(w, EventKind.Hazard, "mui:HCl", "HCl đặc bốc khói", "Hơi HCl gây kích ứng mắt và đường hô hấp.", null, Severity.Notice, "khi_doc");
            return any;
        }
    }
}
