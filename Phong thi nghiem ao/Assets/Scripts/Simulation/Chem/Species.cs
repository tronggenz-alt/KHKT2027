using System;
using System.Collections.Generic;

namespace Khkt.Simulation.Chem
{
    /// <summary>Màu RGB 0..1 (không phụ thuộc UnityEngine để bộ máy hóa học chạy kiểm thử độc lập).</summary>
    [Serializable]
    public struct Rgb
    {
        public float r, g, b;
        public Rgb(float r, float g, float b) { this.r = r; this.g = g; this.b = b; }

        public static Rgb Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return new Rgb(1, 1, 1);
            hex = hex.TrimStart('#');
            int v = Convert.ToInt32(hex, 16);
            return new Rgb(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
        }

        public static Rgb Lerp(Rgb a, Rgb b, float t) => new Rgb(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t);
        public static readonly Rgb White = new Rgb(1, 1, 1);
    }

    public enum SpeciesKind
    {
        /// <summary>Ion bền (không nhận/cho proton): Na⁺, Cl⁻, NO₃⁻... Cation kim loại có thể tạo hiđroxit.</summary>
        Ion,
        /// <summary>Hệ axit - bazơ yếu, lưu theo tổng (CO₃ = H₂CO₃ + HCO₃⁻ + CO₃²⁻). Charge là điện tích dạng nhiều proton nhất.</summary>
        Weak,
        /// <summary>Phân tử trung hòa tan trong nước (H₂O₂, Br₂, glucozơ...).</summary>
        Molecule,
        /// <summary>Chất chỉ thị (lượng tính bằng "đơn vị": 1 lít dung dịch chỉ thị = 1).</summary>
        Indicator,
        /// <summary>Kết tủa sinh ra trong dung dịch (tan lại được khi điều kiện đổi).</summary>
        Precipitate,
        /// <summary>Chất rắn không tan, phản ứng theo thời gian (kim loại, oxit, MnO₂...).</summary>
        Solid,
        /// <summary>Phức chất tan (Cu(NH₃)₄²⁺, FeSCN²⁺...). Tạo lại mỗi lần cân bằng.</summary>
        Complex,
        /// <summary>Hóa chất rắn vừa cho vào, chưa tan hết (tan dần theo DissolveRate).</summary>
        Lump,
    }

    /// <summary>Một loài hóa học trong dung dịch hoặc chất rắn trong bình.</summary>
    public sealed class SpeciesDef
    {
        public int Index;
        public string Id;
        public string Name;
        public string Formula;           // công thức hiển thị (chỉ số dưới Unicode)
        public SpeciesKind Kind;
        public int Charge;               // Ion: điện tích; Weak: điện tích dạng nhiều proton nhất
        public double[] Ka;              // Weak: hằng số phân li từng nấc; cation: thủy phân (1 nấc)
        public double MolarMass;
        public Rgb Color = Rgb.White;
        public double ColorStrength;     // độ hấp thụ trên 1 M (dung dịch) hoặc độ đục (kết tủa)

        // Cation tạo hiđroxit: M(OH)n, Ksp; lưỡng tính: M(OH)n + m OH⁻ ⇌ phức tan, K
        public double KspOH;
        public int OHn;
        public double AmphK;
        public int AmphM;
        public string HydroxideId;       // mã loài kết tủa hiđroxit (để hiển thị màu, ghi sự kiện)

        // Cặp kim loại: ion ⇌ kim loại, thế điện cực chuẩn
        public string MetalId;           // ion → kim loại ("Cu2+" → "Cu(s)")
        public string IonId;             // kim loại → ion
        public int IonCharge;
        public double E0 = double.NaN;

        public HashSet<string> Tags = new HashSet<string>();
        public bool Insoluble;           // chất lỏng hữu cơ không tan (tách lớp)
        public double Density = 1;
        public int ChemStt;              // số thứ tự trong danh mục hóa chất (loài tạo từ danh mục)

        // Lump: thành phần khi tan hết (theo 1 mol), tốc độ tan (mol/s)
        public (string id, double n)[] Composition;
        public double DissolveRate;
        /// <summary>Lump: nhiệt hòa tan (kJ/mol, dương = tỏa nhiệt)</summary>
        public double HeatKJ;

        public bool IsSolidPhase => Kind == SpeciesKind.Precipitate || Kind == SpeciesKind.Solid || Kind == SpeciesKind.Lump;
        public bool HasTag(string t) => Tags.Contains(t);
        public override string ToString() => Id;
    }

    /// <summary>
    /// Danh sách loài: ion, hệ axit yếu, phân tử, chỉ thị, kết tủa, chất rắn cài sẵn; loài hữu cơ và
    /// "cục rắn chưa tan" thêm lúc chạy từ danh mục hóa chất. Mã loài trùng với tools/chuyen_hoa_chat.py.
    /// </summary>
    public static class SpeciesRegistry
    {
        static readonly List<SpeciesDef> all = new List<SpeciesDef>();
        static readonly Dictionary<string, SpeciesDef> byId = new Dictionary<string, SpeciesDef>();
        static bool built;

        public static IReadOnlyList<SpeciesDef> All { get { Build(); return all; } }
        public static int Count { get { Build(); return all.Count; } }

        public static SpeciesDef Get(string id)
        {
            Build();
            return byId.TryGetValue(id, out var s) ? s : null;
        }

        public static int IndexOf(string id)
        {
            var s = Get(id);
            return s != null ? s.Index : -1;
        }

        public static SpeciesDef At(int index) { Build(); return all[index]; }

        public static SpeciesDef Add(string id, string formula, SpeciesKind kind, int charge = 0, double mm = 0, string name = null)
        {
            Build();
            return AddRaw(id, formula, kind, charge, mm, name);
        }

        static SpeciesDef AddRaw(string id, string formula, SpeciesKind kind, int charge, double mm, string name)
        {
            if (byId.TryGetValue(id, out var existing)) return existing;
            int z = kind == SpeciesKind.Ion || kind == SpeciesKind.Complex ? charge : 0;
            var s = new SpeciesDef { Index = all.Count, Id = id, Formula = Pretty(formula, z), Kind = kind, Charge = charge, MolarMass = mm, Name = name ?? Pretty(formula, z) };
            all.Add(s);
            byId[id] = s;
            return s;
        }

        // ------------------------------------------------------------------
        // Công thức hiển thị: chỉ số dưới, điện tích trên (Unicode)
        // ------------------------------------------------------------------

        const string Sub = "₀₁₂₃₄₅₆₇₈₉", Sup = "⁰¹²³⁴⁵⁶⁷⁸⁹";

        /// <summary>"Al2(SO4)3" → "Al₂(SO₄)₃"; mã ion "SO42-" với điện tích -2 → "SO₄²⁻". Số đứng sau dấu chấm (nước kết tinh) giữ nguyên.</summary>
        public static string Pretty(string f, int charge = 0)
        {
            if (string.IsNullOrEmpty(f)) return f;
            string body = f, sup = "";
            if (charge != 0 && (f.EndsWith("+") || f.EndsWith("-")))
            {
                body = f.Substring(0, f.Length - 1);
                int az = Math.Abs(charge);
                string zs = az.ToString();
                if (az > 1 && body.EndsWith(zs)) body = body.Substring(0, body.Length - zs.Length);
                if (az > 1) foreach (char c in zs) sup += Sup[c - '0'];
                sup += charge > 0 ? "⁺" : "⁻";
            }
            var sb = new System.Text.StringBuilder();
            bool afterDot = false;
            for (int i = 0; i < body.Length; i++)
            {
                char c = body[i];
                if (c == '.') { afterDot = true; sb.Append('·'); continue; }
                if (char.IsDigit(c))
                {
                    char p = i > 0 ? body[i - 1] : ' ';
                    bool sub = !afterDot && (char.IsLetter(p) || p == ')' || p == ']' || (char.IsDigit(p) && sb.Length > 0 && Sub.IndexOf(sb[sb.Length - 1]) >= 0));
                    sb.Append(sub ? Sub[c - '0'] : c);
                }
                else
                {
                    afterDot = false;
                    sb.Append(c);
                }
            }
            return sb + sup;
        }

        // ------------------------------------------------------------------
        // Bảng loài cài sẵn
        // ------------------------------------------------------------------

        static SpeciesDef Ion(string id, string formula, int z, double mm, string color = null, double strength = 0)
        {
            var s = AddRaw(id, formula, SpeciesKind.Ion, z, mm, null);
            if (color != null) { s.Color = Rgb.Hex(color); s.ColorStrength = strength; }
            return s;
        }

        static SpeciesDef Weak(string id, string name, int topCharge, double mm, params double[] ka)
        {
            var s = AddRaw(id, id, SpeciesKind.Weak, topCharge, mm, name);
            s.Ka = ka;
            return s;
        }

        static SpeciesDef Mol(string id, string formula, double mm, string color = null, double strength = 0, string name = null)
        {
            var s = AddRaw(id, formula, SpeciesKind.Molecule, 0, mm, name);
            if (color != null) { s.Color = Rgb.Hex(color); s.ColorStrength = strength; }
            return s;
        }

        static SpeciesDef Ppt(string id, string color, string name = null, double mm = 0)
        {
            var s = AddRaw(id, id, SpeciesKind.Precipitate, 0, mm, name);
            s.Color = Rgb.Hex(color);
            s.ColorStrength = 1;
            return s;
        }

        static SpeciesDef Solid(string id, string formula, string color, double mm, string name = null)
        {
            var s = AddRaw(id, formula, SpeciesKind.Solid, 0, mm, name);
            s.Color = Rgb.Hex(color);
            s.ColorStrength = 1;
            return s;
        }

        static void Hydroxide(string ion, string ppt, string pptColor, int n, double ksp, double amphK = 0, int amphM = 0, double hydrolysisKa = 0)
        {
            var s = byId[ion];
            s.KspOH = ksp;
            s.OHn = n;
            s.AmphK = amphK;
            s.AmphM = amphM;
            s.HydroxideId = ppt;
            if (hydrolysisKa > 0) s.Ka = new[] { hydrolysisKa };
            Ppt(ppt, pptColor);
        }

        static void Metal(string metal, string ion, int z, double e0, string color, double mm, string name)
        {
            var m = Solid(metal, metal.Replace("(s)", "").Replace("(l)", ""), color, mm, name);
            m.IonId = ion;
            m.IonCharge = z;
            m.E0 = e0;
            if (ion != null && byId.TryGetValue(ion, out var i)) { i.MetalId = metal; i.E0 = e0; }
        }

        static void Build()
        {
            if (built) return;
            built = true;

            // ---------- Cation ----------
            Ion("Li+", "Li+", 1, 6.94);
            Ion("Na+", "Na+", 1, 22.99);
            Ion("K+", "K+", 1, 39.10);
            Ion("Ba2+", "Ba2+", 2, 137.33);
            Ion("Sr2+", "Sr2+", 2, 87.62);
            Ion("Mg2+", "Mg2+", 2, 24.31);
            Ion("Ca2+", "Ca2+", 2, 40.08);
            Ion("Al3+", "Al3+", 3, 26.98);
            Ion("Fe2+", "Fe2+", 2, 55.85, "#B9DAB0", 1.5);
            Ion("Fe3+", "Fe3+", 3, 55.85, "#D9A23A", 10);
            Ion("Cu2+", "Cu2+", 2, 63.55, "#3C8FD8", 12);
            Ion("Zn2+", "Zn2+", 2, 65.38);
            Ion("Pb2+", "Pb2+", 2, 207.2);
            Ion("Ag+", "Ag+", 1, 107.87);
            Ion("Hg2+", "Hg2+", 2, 200.59);
            Ion("Hg22+", "Hg22+", 2, 401.18);
            Ion("Ni2+", "Ni2+", 2, 58.69, "#4CAF6A", 6);
            Ion("Co2+", "Co2+", 2, 58.93, "#D86A8A", 5);
            Ion("Mn2+", "Mn2+", 2, 54.94, "#F0D8DA", 0.6);
            Ion("Cr3+", "Cr3+", 3, 52.0, "#4E8D6A", 8);
            Ion("Sn2+", "Sn2+", 2, 118.71);
            Ion("Sn4+", "Sn4+", 4, 118.71);
            Ion("Cd2+", "Cd2+", 2, 112.41);
            Ion("Be2+", "Be2+", 2, 9.01);
            Ion("Bi3+", "Bi3+", 3, 208.98);
            Ion("Ti4+", "Ti4+", 4, 47.87);

            Hydroxide("Mg2+", "Mg(OH)2", "#F4F4F2", 2, 5.6e-12);
            Hydroxide("Ca2+", "Ca(OH)2", "#F4F4F2", 2, 5.5e-6);
            Hydroxide("Sr2+", "Sr(OH)2", "#F4F4F2", 2, 3.2e-4);
            Hydroxide("Al3+", "Al(OH)3", "#F2F4F6", 3, 1.3e-33, 40, 1, 1e-5);
            Hydroxide("Fe2+", "Fe(OH)2", "#D3DCC0", 2, 4.9e-17, 0, 0, 3e-10);
            Hydroxide("Fe3+", "Fe(OH)3", "#9C4A1A", 3, 1e-36, 0, 0, 6.3e-3);  // Fe(OH)₃ vô định hình mới sinh (tan hơn dạng già 2,8·10⁻³⁹)
            Hydroxide("Cu2+", "Cu(OH)2", "#4A9BE0", 2, 2.2e-20, 0, 0, 1e-8);
            Hydroxide("Zn2+", "Zn(OH)2", "#F4F4F2", 2, 3e-17, 0.13, 2, 1e-9);
            Hydroxide("Pb2+", "Pb(OH)2", "#F4F4F2", 2, 1.4e-20, 0.05, 1, 1e-8);
            Hydroxide("Ag+", "Ag2O", "#4A3A2E", 1, 2e-8);
            Hydroxide("Hg2+", "HgO", "#E7B02A", 2, 3.6e-26, 0, 0, 2.6e-4);
            Hydroxide("Hg22+", "Hg2O", "#2A2A2A", 2, 1.6e-23);
            Hydroxide("Ni2+", "Ni(OH)2", "#5BA35B", 2, 5.5e-16);
            Hydroxide("Co2+", "Co(OH)2", "#C0607A", 2, 1.6e-15);
            Hydroxide("Mn2+", "Mn(OH)2", "#EFE8DC", 2, 1.9e-13);
            Hydroxide("Cr3+", "Cr(OH)3", "#7A9A78", 3, 6.3e-31, 0.04, 1, 1e-4);
            Hydroxide("Sn2+", "Sn(OH)2", "#F4F4F2", 2, 5.4e-27, 0.01, 1, 1e-4);
            Hydroxide("Sn4+", "Sn(OH)4", "#F4F4F2", 4, 1e-56, 1e-3, 2, 1e-2);
            Hydroxide("Cd2+", "Cd(OH)2", "#F4F4F2", 2, 7.2e-15);
            Hydroxide("Be2+", "Be(OH)2", "#F4F4F2", 2, 6.9e-22, 0.05, 2, 1e-6);
            Hydroxide("Bi3+", "Bi(OH)3", "#F4F4F2", 3, 4e-31, 0, 0, 1e-2);
            Hydroxide("Ti4+", "TiO2·nH2O", "#F7F7F7", 4, 1e-50, 0, 0, 1e-1);

            // ---------- Anion bền ----------
            Ion("Cl-", "Cl-", -1, 35.45);
            Ion("Br-", "Br-", -1, 79.9);
            Ion("I-", "I-", -1, 126.9);
            Ion("NO3-", "NO3-", -1, 62.0);
            Ion("ClO4-", "ClO4-", -1, 99.45);
            Ion("ClO3-", "ClO3-", -1, 83.45);
            Ion("BrO3-", "BrO3-", -1, 127.9);
            Ion("IO3-", "IO3-", -1, 174.9);
            Ion("SCN-", "SCN-", -1, 58.08);
            Ion("MnO4-", "MnO4-", -1, 118.94, "#7A1A8A", 700);
            Ion("MnO42-", "MnO42-", -2, 118.94, "#2E8B57", 120);
            Ion("S2O32-", "S2O32-", -2, 112.13);
            Ion("S2O82-", "S2O82-", -2, 192.12);
            Ion("S4O62-", "S4O62-", -2, 224.26);
            Ion("FeCN64-", "[Fe(CN)6]4-", -4, 211.95, "#E9D96A", 4);
            Ion("FeCN63-", "[Fe(CN)6]3-", -3, 211.95, "#E39A2A", 9);
            Ion("CoCN63-", "[Co(CN)6]3-", -3, 214.9, "#F2E7A8", 1);
            Ion("SiF62-", "SiF62-", -2, 142.08);
            Ion("P2O74-", "P2O74-", -4, 173.94);
            Ion("P3O105-", "P3O105-", -5, 252.9);
            Ion("HgI42-", "[HgI4]2-", -2, 708.2);

            // ---------- Hệ axit - bazơ yếu (lưu theo tổng) ----------
            Weak("SO4", "sunfat", -1, 96.06, 1.0e-2);
            Weak("SO3", "sunfit", 0, 80.06, 1.4e-2, 6.3e-8);
            Weak("CO3", "cacbonat", 0, 60.01, 4.3e-7, 4.8e-11);
            Weak("PO4", "photphat", 0, 94.97, 7.1e-3, 6.3e-8, 4.2e-13);
            Weak("PO3", "photphit", 0, 78.97, 5e-2, 2e-7);
            Weak("PO2", "hipophotphit", 0, 62.97, 5.9e-2);
            Weak("S", "sunfua", 0, 32.06, 9.1e-8, 1e-13);
            Weak("NO2", "nitrit", 0, 46.0, 5.6e-4);
            Weak("F", "florua", 0, 19.0, 6.8e-4);
            Weak("CN", "xianua", 0, 26.02, 6.2e-10);
            Weak("ClO", "hipoclorit", 0, 51.45, 3e-8);
            Weak("ClO2", "clorit", 0, 67.45, 1.1e-2);
            Weak("SiO3", "silicat", 0, 76.08, 1.6e-10, 1e-12);
            Weak("B", "borat", 0, 61.83, 5.8e-10);
            Weak("CrVI", "cromat/đicromat", -1, 115.99, 3.2e-7);
            Weak("NH3", "amoniac/amoni", 1, 17.03, 5.6e-10);
            Weak("Ac", "axetat", 0, 59.04, 1.8e-5);
            Weak("Fo", "fomat", 0, 45.02, 1.8e-4);
            Weak("Ox", "oxalat", 0, 88.02, 5.9e-2, 6.4e-5);
            Weak("Cit", "xitrat", 0, 189.1, 7.4e-4, 1.7e-5, 4e-7);
            Weak("Tart", "tactrat", 0, 148.07, 9.2e-4, 4.3e-5);
            Weak("Bz", "benzoat", 0, 121.12, 6.3e-5);
            Weak("Lac", "lactat", 0, 89.07, 1.4e-4);
            // màu Cr(VI): cam (Cr₂O₇²⁻, môi trường axit) / vàng (CrO₄²⁻) do ChemColor tính theo pH
            byId["CrVI"].Color = Rgb.Hex("#E8A01A");
            byId["CrVI"].ColorStrength = 25;

            // ---------- Phân tử tan ----------
            Mol("H2O2", "H2O2", 34.01);
            Mol("Cl2", "Cl2", 70.9, "#D2E07A", 4);
            Mol("Br2", "Br2", 159.8, "#C2571A", 60);
            Mol("I2", "I2", 253.8, "#8A4A1A", 260);
            Mol("ure", "(NH2)2CO", 60.06, null, 0, "urê");
            Mol("xaphong", "C17H35COONa", 306.5, null, 0, "xà phòng");
            Mol("ninhydrin", "C9H6O4", 178.1, "#F4F2EA", 1, "ninhiđrin");
            Mol("naphtol", "C10H7OH", 144.2, null, 0, "α-naphtol");
            Mol("schiff", "fuchsin", 337.8, null, 0, "thuốc thử Schiff");
            Mol("nessler", "K2HgI4", 786.4, "#F7F2C8", 0.5, "thuốc thử Nessler");
            Mol("diphenylamin", "(C6H5)2NH", 169.2, null, 0, "điphenylamin");
            Mol("picric", "C6H2(NO2)3OH", 229.1, "#F0E04A", 40, "axit picric");
            Mol("C2H5OH", "C2H5OH", 46.07, null, 0, "etanol");

            // ---------- Phức (tạo trong cân bằng) ----------
            var c1 = AddRaw("CuNH3", "[Cu(NH3)4]2+", SpeciesKind.Complex, 2, 131.7, null); c1.Color = Rgb.Hex("#1F3FBF"); c1.ColorStrength = 55;
            AddRaw("AgNH3", "[Ag(NH3)2]+", SpeciesKind.Complex, 1, 141.9, null);
            AddRaw("ZnNH3", "[Zn(NH3)4]2+", SpeciesKind.Complex, 2, 133.5, null);
            var c4 = AddRaw("NiNH3", "[Ni(NH3)6]2+", SpeciesKind.Complex, 2, 160.9, null); c4.Color = Rgb.Hex("#6A6AD8"); c4.ColorStrength = 12;
            var c5 = AddRaw("FeSCN", "[Fe(SCN)]2+", SpeciesKind.Complex, 2, 113.9, null); c5.Color = Rgb.Hex("#A0101A"); c5.ColorStrength = 900;
            var c6 = AddRaw("CuPol", "phức đồng(II) - poliol", SpeciesKind.Complex, 0, 300, null); c6.Color = Rgb.Hex("#1E50C8"); c6.ColorStrength = 45;
            var c7 = AddRaw("FePhenol", "phức sắt(III) - phenolat", SpeciesKind.Complex, 3, 300, null); c7.Color = Rgb.Hex("#6A1A8A"); c7.ColorStrength = 300;

            // ---------- Chất chỉ thị ----------
            foreach (var (id, name) in new[] { ("mo", "metyl da cam"), ("mr", "metyl đỏ"), ("btb", "bromothymol xanh"), ("bcg", "bromocresol xanh"), ("tb", "thymol xanh"), ("ay", "alizarin vàng"), ("univ", "chỉ thị vạn năng"), ("starch", "hồ tinh bột") })
                AddRaw(id, id, SpeciesKind.Indicator, 0, 0, name).ColorStrength = id == "starch" ? 0 : 600;

            // ---------- Kết tủa từ cặp ion ----------
            Ppt("AgCl", "#F7F7F5"); Ppt("AgBr", "#F0E9B8"); Ppt("AgI", "#EAD66A"); Ppt("Ag2CrO4", "#A3392A"); Ppt("Ag3PO4", "#E9D45A");
            Ppt("Ag2S", "#1A1A1A"); Ppt("Ag2CO3", "#F0EBCF"); Ppt("Ag2SO4", "#F7F7F5"); Ppt("AgSCN", "#F7F7F5"); Ppt("Ag2C2O4", "#F7F7F5");
            Ppt("BaSO4", "#FAFAFA"); Ppt("BaCO3", "#FAFAFA"); Ppt("BaCrO4", "#F2D23C"); Ppt("Ba3(PO4)2", "#FAFAFA"); Ppt("BaSO3", "#FAFAFA");
            Ppt("BaC2O4", "#FAFAFA"); Ppt("BaF2", "#FAFAFA"); Ppt("SrSO4", "#FAFAFA"); Ppt("SrCO3", "#FAFAFA");
            Ppt("CaCO3", "#FAFAFA"); Ppt("CaSO4", "#FAFAFA"); Ppt("Ca3(PO4)2", "#FAFAFA"); Ppt("CaC2O4", "#FAFAFA"); Ppt("CaF2", "#FAFAFA");
            Ppt("MgCO3", "#FAFAFA"); Ppt("Mg3(PO4)2", "#FAFAFA"); Ppt("MgF2", "#FAFAFA");
            Ppt("PbCl2", "#F7F7F5"); Ppt("PbBr2", "#F7F7F5"); Ppt("PbI2", "#F2C400"); Ppt("PbSO4", "#FAFAFA"); Ppt("PbS", "#1A1A1A");
            Ppt("PbCrO4", "#F2C400"); Ppt("PbCO3", "#FAFAFA"); Ppt("CuS", "#151515"); Ppt("CuCO3", "#3E9F86"); Ppt("ZnS", "#FAFAFA");
            Ppt("ZnCO3", "#FAFAFA"); Ppt("FeS", "#151515"); Ppt("FeCO3", "#E8E2D0"); Ppt("MnS", "#E9B8A8"); Ppt("MnCO3", "#F2E2DA");
            Ppt("NiS", "#151515"); Ppt("CoS", "#151515"); Ppt("CdS", "#F2B705"); Ppt("HgS", "#151515"); Ppt("Hg2Cl2", "#FAFAFA");
            Ppt("FePO4", "#F2E9C8"); Ppt("AlPO4", "#FAFAFA"); Ppt("CaSiO3", "#FAFAFA"); Ppt("Zn3(PO4)2", "#FAFAFA");
            Ppt("Fe4[Fe(CN)6]3", "#1A3A8A", "xanh Phổ (xanh Berlin)"); Ppt("Fe3[Fe(CN)6]2", "#1A3A8A", "xanh Turnbull");
            Ppt("Cu2[Fe(CN)6]", "#7A2E1A"); Ppt("CaXP", "#F4F4F2", "xà phòng canxi (váng)");
            // kết tủa tạo bởi phản ứng (không phải cặp ion)
            Ppt("S(kt)", "#F2E46A", "lưu huỳnh"); Ppt("H2SiO3", "#F2F4F6", "axit silixic (keo)"); Ppt("Cu2O", "#B23A22");
            Ppt("Ag2C2", "#E9D96A", "bạc axetilua"); Ppt("TBP", "#F7F7F5", "2,4,6-tribromphenol"); Ppt("TBA", "#F7F7F5", "2,4,6-tribromanilin");
            Ppt("MnO2(kt)", "#4A3626", "mangan đioxit"); Ppt("ProteinDT", "#F2EFE6", "protein đông tụ"); Ppt("NesslerKT", "#B5651D", "kết tủa nâu (Nessler)");
            Ppt("C(than)", "#121212", "than (cacbon)"); Ppt("I2(r)", "#2A1A30", "iot rắn"); Ppt("Fe(OH)3kt", "#9C4A1A");

            // ---------- Chất rắn (kim loại, phi kim, oxit, hợp chất đặc biệt) ----------
            Metal("Li(s)", "Li+", 1, -3.04, "#CFCFCF", 6.94, "liti");
            Metal("K(s)", "K+", 1, -2.93, "#D9D9D9", 39.1, "kali");
            Metal("Ba(s)", "Ba2+", 2, -2.91, "#C9C9C9", 137.33, "bari");
            Metal("Ca(s)", "Ca2+", 2, -2.87, "#D0D0D0", 40.08, "canxi");
            Metal("Na(s)", "Na+", 1, -2.71, "#D9D9D9", 22.99, "natri");
            Metal("Mg(s)", "Mg2+", 2, -2.37, "#C9CDD0", 24.31, "magie");
            Metal("Al(s)", "Al3+", 3, -1.66, "#C8CCD0", 26.98, "nhôm");
            Metal("Ti(s)", null, 0, double.NaN, "#9A9EA2", 47.87, "titan");
            Metal("Mn(s)", "Mn2+", 2, -1.18, "#9E9E9E", 54.94, "mangan");
            Metal("Zn(s)", "Zn2+", 2, -0.76, "#A8B0B6", 65.38, "kẽm");
            Metal("Cr(s)", "Cr3+", 3, -0.74, "#B8BCC2", 52.0, "crom");
            Metal("Fe(s)", "Fe2+", 2, -0.44, "#7A7A7A", 55.85, "sắt");
            Metal("Co(s)", "Co2+", 2, -0.28, "#8A8E92", 58.93, "coban");
            Metal("Ni(s)", "Ni2+", 2, -0.25, "#A9A49A", 58.69, "niken");
            Metal("Sn(s)", "Sn2+", 2, -0.14, "#B9BDC2", 118.71, "thiếc");
            Metal("Pb(s)", "Pb2+", 2, -0.13, "#6B6E73", 207.2, "chì");
            Metal("Cu(s)", "Cu2+", 2, 0.34, "#B87333", 63.55, "đồng");
            Metal("Ag(s)", "Ag+", 1, 0.80, "#C0C0C0", 107.87, "bạc");
            Metal("Hg(l)", "Hg2+", 2, 0.85, "#BFC3C7", 200.59, "thủy ngân");
            Metal("Au(s)", null, 0, double.NaN, "#D4AF37", 196.97, "vàng");
            Metal("W(s)", null, 0, double.NaN, "#8A8A8A", 183.84, "vonfram");
            Solid("AgMirror", "Ag", "#D8DDE2", 107.87, "gương bạc");

            foreach (var (id, f, color, mm, name) in new (string, string, string, double, string)[]
            {
                ("S(s)", "S", "#F7E03C", 32.06, "lưu huỳnh"), ("C(s)", "C", "#222222", 12.01, "cacbon"), ("Cact(s)", "C", "#141414", 12.01, "than hoạt tính"), ("P(s)", "P", "#9B2D2D", 30.97, "photpho đỏ"),
                ("Pw(s)", "P", "#F2EFD8", 30.97, "photpho trắng"), ("Si(s)", "Si", "#5A6068", 28.09, "silic"), ("B(s)", "B", "#3A3530", 10.81, "bo"),
                ("As(s)", "As", "#6A6E72", 74.92, "asen"),
                ("MgO(s)", "MgO", "#FAFAFA", 40.3, null), ("FeO(s)", "FeO", "#2B2B2B", 71.84, null), ("Fe2O3(s)", "Fe2O3", "#8E2E1C", 159.69, null),
                ("Fe3O4(s)", "Fe3O4", "#222222", 231.53, null), ("CuO(s)", "CuO", "#1D1D1D", 79.55, null), ("Cu2O(s)", "Cu2O", "#B5372A", 143.09, null),
                ("ZnO(s)", "ZnO", "#FAFAFA", 81.38, null), ("PbO(s)", "PbO", "#E3B23C", 223.2, null), ("PbO2(s)", "PbO2", "#3B2A20", 239.2, null),
                ("Pb3O4(s)", "Pb3O4", "#D0461F", 685.6, null), ("SnO(s)", "SnO", "#3A3A3A", 134.7, null), ("SnO2(s)", "SnO2", "#FAFAFA", 150.7, null),
                ("MnO(s)", "MnO", "#5A6A4A", 70.94, null), ("MnO2(s)", "MnO2", "#2A2420", 86.94, null), ("CrO(s)", "CrO", "#1A1A1A", 68.0, null),
                ("Cr2O3(s)", "Cr2O3", "#3E7B3A", 152.0, null), ("NiO(s)", "NiO", "#5E7D3B", 74.69, null), ("CoO(s)", "CoO", "#3B4A3B", 74.93, null),
                ("TiO2(s)", "TiO2", "#F7F7F7", 79.87, null), ("SiO2(s)", "SiO2", "#F2F2F0", 60.08, null), ("Ag2O(s)", "Ag2O", "#3A2D26", 231.7, null),
                ("HgO(s)", "HgO", "#E2731F", 216.6, null), ("CdO(s)", "CdO", "#7A4A2A", 128.4, null), ("BeO(s)", "BeO", "#FAFAFA", 25.0, null),
                ("ZrO2(s)", "ZrO2", "#FAFAFA", 123.2, null), ("V2O5(s)", "V2O5", "#E08A1E", 181.9, null), ("MoO3(s)", "MoO3", "#F4F4F0", 143.9, null),
                ("WO3(s)", "WO3", "#D9C93A", 231.8, null), ("Sb2O3(s)", "Sb2O3", "#FAFAFA", 291.5, null), ("Bi2O3(s)", "Bi2O3", "#F2E07A", 466.0, null),
                ("SeO2(s)", "SeO2", "#FAFAFA", 111.0, null), ("TeO2(s)", "TeO2", "#FAFAFA", 159.6, null), ("As2O3(s)", "As2O3", "#FAFAFA", 197.8, null),
                ("As2O5(s)", "As2O5", "#FAFAFA", 229.8, null), ("Al2O3(s)", "Al2O3", "#FAFAFA", 101.96, null),
                ("CuCl(s)", "CuCl", "#F2F2E8", 99.0, null), ("CuI(s)", "CuI", "#F2EEE0", 190.45, null), ("FeS2(s)", "FeS2", "#C9A23A", 120.0, "pirit"),
                ("Na3AlF6(s)", "Na3AlF6", "#FAFAFA", 209.9, "criolit"), ("CaC2(s)", "CaC2", "#5A5A5A", 64.1, "canxi cacbua"),
                ("Al4C3(s)", "Al4C3", "#E9D96A", 143.96, "nhôm cacbua"), ("Mg3N2(s)", "Mg3N2", "#E9E2A8", 100.9, "magie nitrua"),
                ("Ca3N2(s)", "Ca3N2", "#8A7A6A", 148.25, "canxi nitrua"), ("NaNH2(s)", "NaNH2", "#F4F4F0", 39.0, "natri amiđua"),
                ("NaH(s)", "NaH", "#B0B0B0", 24.0, "natri hiđrua"), ("LiAlH4(s)", "LiAlH4", "#F4F4F0", 37.95, null), ("NaBH4(s)", "NaBH4", "#F4F4F0", 37.83, null),
                ("Na2O2(s)", "Na2O2", "#F5ECC8", 77.98, "natri peoxit"), ("K2O2(s)", "K2O2", "#F5ECC8", 110.2, "kali peoxit"), ("BaO2(s)", "BaO2", "#F4F4F0", 169.3, "bari peoxit"),
                ("SiO2gel(s)", "SiO2·xH2O", "#4E8DF5", 60.08, "silica gel"),
            })
                Solid(id, f, color, mm, name);
        }
    }
}
