using System;
using System.Collections.Generic;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Bảng màu các chất (một nguồn duy nhất): mã màu hiển thị và tên màu tiếng Việt theo SGK cho ion trong dung dịch,
    /// phức, kết tủa, chất rắn, khí. SpeciesRegistry lấy mã màu từ đây; tiêu đề hiện tượng ("Kết tủa xanh lam: Cu(OH)₂")
    /// lấy tên màu từ đây; bộ quan sát gọi tên màu dung dịch bất kỳ bằng ColorName.
    /// </summary>
    public static class ChemPalette
    {
        public sealed class Item
        {
            public string Id, Hex, Name, Note;
            public string NameBase; public double PhSwitch;   // màu trong môi trường bazơ (pH > PhSwitch), nếu khác
        }

        static Dictionary<string, Item> items;

        static void I(string id, string hex, string name, string note = null)
        {
            items[id] = new Item { Id = id, Hex = hex, Name = name, Note = note };
        }

        static void Build()
        {
            if (items != null) return;
            items = new Dictionary<string, Item>();
            // ---------- Ion, phân tử có màu trong dung dịch ----------
            I("Cu2+", "#3C8FD8", "xanh lam");
            I("Fe2+", "#B9DAB0", "lục nhạt", "để lâu trong không khí chuyển vàng (Fe³⁺)");
            I("Fe3+", "#D9A23A", "vàng nâu");
            I("Ni2+", "#4CAF6A", "xanh lục");
            I("Co2+", "#D86A8A", "hồng");
            I("Mn2+", "#F0D8DA", "hồng rất nhạt (gần như không màu)");
            I("Cr3+", "#4E8D6A", "xanh lục");
            I("MnO4-", "#7A1A8A", "tím");
            I("MnO42-", "#2E8B57", "xanh lục");
            I("CrVI", "#E8A01A", "da cam", "môi trường axit: Cr₂O₇²⁻ da cam; kiềm: CrO₄²⁻ vàng");
            items["CrVI"].NameBase = "vàng"; items["CrVI"].PhSwitch = 6.5;
            I("Cl2", "#D2E07A", "vàng lục nhạt");
            I("Br2", "#C2571A", "vàng nâu (da cam)");
            I("I2", "#8A4A1A", "nâu vàng", "trong CCl₄, benzen: tím; với hồ tinh bột: xanh tím");
            I("FeCN64-", "#E9D96A", "vàng nhạt");
            I("FeCN63-", "#E39A2A", "vàng cam");
            I("CoCN63-", "#F2E7A8", "vàng rất nhạt");
            I("picric", "#F0E04A", "vàng");
            I("xanh_methylen", "#1A53AF", "xanh lam");
            I("tim_ket_tinh", "#5A189A", "tím");
            I("safranin", "#D82A4A", "đỏ");
            // ---------- Phức ----------
            I("CuNH3", "#1F3FBF", "xanh lam thẫm");
            I("NiNH3", "#6A6AD8", "xanh tím");
            I("FeSCN", "#A0101A", "đỏ máu");
            I("CuPol", "#1E50C8", "xanh lam thẫm");
            I("FePhenol", "#6A1A8A", "tím");
            I("BiuretTim", "#7A3AA8", "tím");
            I("DPAXanh", "#1A3A9A", "xanh lam thẫm");
            I("XanthoVang", "#E8B21A", "vàng");
            I("MolischTim", "#6A1A6A", "tím");
            I("SchiffDo", "#C21A7A", "hồng tím");
            I("RuhemannTim", "#5A2A8A", "tím xanh");
            I("AgNH3", "#FFFFFF", "không màu");
            I("ZnNH3", "#FFFFFF", "không màu");
            I("CoNH3", "#C8862A", "vàng nâu", "để ngoài không khí chuyển đỏ nâu");
            I("CuCl4", "#4E9A3A", "xanh lục (vàng lục khi đặc)");
            I("CoCl4", "#2D5DD8", "xanh lam");
            I("CoSCN", "#2D4DC8", "xanh lam");
            I("FeF6", "#FFFFFF", "không màu");
            I("FePO4c", "#FFFFFF", "không màu");
            I("HgI4", "#FFFFFF", "không màu");
            I("AgS2O3", "#FFFFFF", "không màu");
            I("AgCN2", "#FFFFFF", "không màu");
            I("CuCN", "#FFFFFF", "không màu");
            I("AlOH4", "#FFFFFF", "không màu");
            I("ZnOH4", "#FFFFFF", "không màu");
            I("CrO5", "#1E3C9A", "xanh lam thẫm", "không bền, phân hủy nhanh");
            I("I2tb", "#14246A", "xanh tím");
            // ---------- Kết tủa từ cặp ion ----------
            I("AgCl", "#F7F7F5", "trắng", "hóa đen dần ngoài ánh sáng");
            I("AgBr", "#F0E9B8", "vàng nhạt");
            I("AgI", "#EAD66A", "vàng");
            I("AgSCN", "#F7F7F5", "trắng");
            I("Ag2CrO4", "#A3392A", "đỏ gạch");
            I("Ag3PO4", "#E9D45A", "vàng");
            I("Ag2S", "#1A1A1A", "đen");
            I("Ag2CO3", "#F0EBCF", "vàng nhạt (trắng ngà)");
            I("Ag2SO4", "#F7F7F5", "trắng");
            I("Ag2C2O4", "#F7F7F5", "trắng");
            I("AgCN", "#F7F7F5", "trắng");
            I("BaSO4", "#FAFAFA", "trắng", "không tan cả trong axit mạnh");
            I("BaCO3", "#FAFAFA", "trắng");
            I("BaCrO4", "#F2D23C", "vàng");
            I("Ba3(PO4)2", "#FAFAFA", "trắng");
            I("BaSO3", "#FAFAFA", "trắng");
            I("BaC2O4", "#FAFAFA", "trắng");
            I("BaF2", "#FAFAFA", "trắng");
            I("SrSO4", "#FAFAFA", "trắng");
            I("SrCO3", "#FAFAFA", "trắng");
            I("CaCO3", "#FAFAFA", "trắng");
            I("CaSO4", "#FAFAFA", "trắng");
            I("CaSO3", "#FAFAFA", "trắng");
            I("Ca3(PO4)2", "#FAFAFA", "trắng");
            I("CaC2O4", "#FAFAFA", "trắng");
            I("CaF2", "#FAFAFA", "trắng");
            I("MgCO3", "#FAFAFA", "trắng");
            I("Mg3(PO4)2", "#FAFAFA", "trắng");
            I("MgF2", "#FAFAFA", "trắng");
            I("PbCl2", "#F7F7F5", "trắng", "tan khi đun nóng");
            I("PbBr2", "#F7F7F5", "trắng");
            I("PbI2", "#F2C400", "vàng tươi");
            I("PbSO4", "#FAFAFA", "trắng");
            I("PbS", "#1A1A1A", "đen");
            I("PbCrO4", "#F2C400", "vàng");
            I("PbCO3", "#FAFAFA", "trắng");
            I("CuS", "#151515", "đen");
            I("CuCO3", "#3E9F86", "xanh lục");
            I("ZnS", "#FAFAFA", "trắng");
            I("ZnCO3", "#FAFAFA", "trắng");
            I("Zn3(PO4)2", "#FAFAFA", "trắng");
            I("FeS", "#151515", "đen");
            I("FeCO3", "#E8E2D0", "trắng xám");
            I("FePO4", "#F2E9C8", "vàng nhạt");
            I("AlPO4", "#FAFAFA", "trắng");
            I("MnS", "#E9B8A8", "hồng nhạt");
            I("MnCO3", "#F2E2DA", "trắng hồng");
            I("NiS", "#151515", "đen");
            I("CoS", "#151515", "đen");
            I("CdS", "#F2B705", "vàng");
            I("HgS", "#151515", "đen");
            I("HgI2", "#D8301A", "đỏ cam", "tan trong KI dư tạo [HgI₄]²⁻ không màu");
            I("Hg2Cl2", "#FAFAFA", "trắng");
            I("Fe4[Fe(CN)6]3", "#1A3A8A", "xanh đậm (xanh Phổ)");
            I("Fe3[Fe(CN)6]2", "#1A3A8A", "xanh đậm (xanh Turnbull)");
            I("Cu2[Fe(CN)6]", "#7A2E1A", "nâu đỏ");
            I("CaXP", "#F4F4F2", "trắng (váng)");
            // ---------- Hiđroxit ----------
            I("Mg(OH)2", "#F4F4F2", "trắng");
            I("Ca(OH)2", "#F4F4F2", "trắng");
            I("Sr(OH)2", "#F4F4F2", "trắng");
            I("Al(OH)3", "#F2F4F6", "keo trắng");
            I("Fe(OH)2", "#D3DCC0", "trắng xanh", "hóa nâu đỏ dần trong không khí");
            I("Fe(OH)3", "#9C4A1A", "nâu đỏ");
            I("Cu(OH)2", "#4A9BE0", "xanh lam");
            I("Zn(OH)2", "#F4F4F2", "trắng");
            I("Pb(OH)2", "#F4F4F2", "trắng");
            I("Ag2O", "#4A3A2E", "đen nâu");
            I("HgO", "#E7B02A", "vàng");
            I("Hg2O", "#2A2A2A", "đen");
            I("Ni(OH)2", "#5BA35B", "xanh lục");
            I("Co(OH)2", "#C0607A", "hồng");
            I("Mn(OH)2", "#EFE8DC", "trắng", "hóa nâu đen dần trong không khí");
            I("Cr(OH)3", "#7A9A78", "xanh xám");
            I("Sn(OH)2", "#F4F4F2", "trắng");
            I("Sn(OH)4", "#F4F4F2", "trắng");
            I("Cd(OH)2", "#F4F4F2", "trắng");
            I("Be(OH)2", "#F4F4F2", "trắng");
            I("Bi(OH)3", "#F4F4F2", "trắng");
            I("TiO2·nH2O", "#F7F7F7", "trắng");
            // ---------- Kết tủa do phản ứng ----------
            I("S(kt)", "#F2E46A", "vàng");
            I("H2SiO3", "#F2F4F6", "keo trắng");
            I("Cu2O", "#B23A22", "đỏ gạch");
            I("Ag2C2", "#E9D96A", "vàng nhạt");
            I("TBP", "#F7F7F5", "trắng");
            I("TBA", "#F7F7F5", "trắng");
            I("MnO2(kt)", "#4A3626", "nâu đen");
            I("ProteinDT", "#F2EFE6", "trắng");
            I("NesslerKT", "#B5651D", "nâu đỏ");
            I("C(than)", "#121212", "đen");
            I("I2(r)", "#2A1A30", "tím đen");
            I("CHI3", "#F2D23C", "vàng", "iođofom, mùi đặc trưng");
            I("PhenolKT", "#F4F2EA", "trắng đục (phenol tách ra)");
            I("AnilinKT", "#F4EEDC", "vàng nhạt (anilin tách lớp)");
            I("MillonDo", "#A3201A", "đỏ gạch");
            // ---------- Chất rắn ----------
            I("Cu(s)", "#B87333", "đỏ");
            I("Ag(s)", "#C0C0C0", "trắng bạc");
            I("AgMirror", "#D8DDE2", "sáng như gương");
            I("Au(s)", "#D4AF37", "vàng");
            I("Fe(s)", "#7A7A7A", "trắng xám");
            I("Zn(s)", "#A8B0B6", "trắng xám");
            I("Al(s)", "#C8CCD0", "trắng bạc");
            I("Mg(s)", "#C9CDD0", "trắng bạc");
            I("Pb(s)", "#6B6E73", "xám");
            I("Sn(s)", "#B9BDC2", "trắng bạc");
            I("Hg(l)", "#BFC3C7", "trắng bạc (lỏng)");
            I("S(s)", "#F7E03C", "vàng");
            I("P(s)", "#9B2D2D", "đỏ");
            I("C(s)", "#222222", "đen");
            I("CuO(s)", "#1D1D1D", "đen");
            I("Cu2O(s)", "#B5372A", "đỏ gạch");
            I("FeO(s)", "#2B2B2B", "đen");
            I("Fe2O3(s)", "#8E2E1C", "nâu đỏ");
            I("Fe3O4(s)", "#222222", "đen");
            I("ZnO(s)", "#FAFAFA", "trắng");
            I("MgO(s)", "#FAFAFA", "trắng");
            I("Al2O3(s)", "#FAFAFA", "trắng");
            I("PbO(s)", "#E3B23C", "vàng");
            I("PbO2(s)", "#3B2A20", "nâu đen");
            I("MnO2(s)", "#2A2420", "đen");
            I("Cr2O3(s)", "#3E7B3A", "lục thẫm");
            I("CuCl(s)", "#F2F2E8", "trắng");
            I("CuI(s)", "#F2EEE0", "trắng");
            I("Ag2O(s)", "#3A2D26", "đen nâu");
            I("HgO(s)", "#E2731F", "đỏ cam / vàng");
            I("SiO2(s)", "#F2F2F0", "trắng");
            I("P2O5(s)", "#FAFAFA", "trắng (khói trắng khi cháy)");
            I("CuSO4khan", "#EEEEEE", "trắng", "gặp nước chuyển xanh lam (CuSO₄·5H₂O)");
            // ---------- Khí ----------
            I("khi:NO2", "#9E401A", "nâu đỏ");
            I("khi:Cl2", "#C0D050", "vàng lục");
            I("khi:Br2", "#B04A1A", "nâu đỏ (hơi brom)");
            I("khi:I2", "#7A3A9A", "tím (hơi iot)");
            I("khi:NH4Cl", "#F4F4F4", "khói trắng");
            foreach (var g in new[] { "H2", "O2", "N2", "CO", "CO2", "SO2", "H2S", "NH3", "NO", "N2O", "CH4", "C2H4", "C2H2", "HCl", "HCN" })
                if (!items.ContainsKey("khi:" + g)) I("khi:" + g, "#FFFFFF", "không màu");
        }

        public static IEnumerable<Item> All { get { Build(); return items.Values; } }

        public static Item Get(string id)
        {
            Build();
            return id != null && items.TryGetValue(id, out var it) ? it : null;
        }

        /// <summary>Tên màu SGK của loài (null nếu chưa có trong bảng).</summary>
        public static string NameOf(string id) => Get(id)?.Name;

        /// <summary>Tên màu của loài ở pH cho trước (CrVI: da cam trong axit, vàng trong kiềm).</summary>
        public static string NameAt(string id, double ph)
        {
            var it = Get(id);
            if (it == null) return null;
            return it.NameBase != null && ph > it.PhSwitch ? it.NameBase : it.Name;
        }

        /// <summary>Gán mã màu trong bảng cho loài đã khai báo (gọi cuối SpeciesRegistry.Build).</summary>
        internal static void ApplyTo(SpeciesDef s)
        {
            var it = Get(s.Id);
            if (it == null || it.Hex == "#FFFFFF") return;
            s.Color = Rgb.Hex(it.Hex);
            if (s.ColorStrength <= 0 && (s.Kind == SpeciesKind.Precipitate || s.Kind == SpeciesKind.Solid)) s.ColorStrength = 1;
        }

        // ------------------------------------------------------------------
        // Gọi tên màu dung dịch bất kỳ (bộ quan sát)
        // ------------------------------------------------------------------

        static readonly Rgb Water = new Rgb(0.86f, 0.93f, 1f);

        /// <summary>Tên màu của dung dịch nhìn thấy (từ ChemLook): "không màu", "xanh lam", "vàng nâu", "hồng"...</summary>
        public static string ColorName(ChemLook look, float pink = 0)
        {
            if (pink > 0.25f) return pink > 0.6f ? "hồng đậm" : "hồng";
            string body = TintName(look.Liquid);
            if (look.Turbidity > 0.3f) return body == "không màu" ? "vẩn đục" : body + ", vẩn đục";
            return body;
        }

        /// <summary>Tên màu theo độ truyền qua so với nước (bỏ qua màu xanh nhạt sẵn có của nước trong hình).</summary>
        public static string TintName(Rgb liquid)
        {
            double tr = Math.Min(1, liquid.r / Water.r), tg = Math.Min(1, liquid.g / Water.g), tb = Math.Min(1, liquid.b / Water.b);
            double ar = -Math.Log(Math.Max(1e-4, tr)), ag = -Math.Log(Math.Max(1e-4, tg)), ab = -Math.Log(Math.Max(1e-4, tb));
            double amax = Math.Max(ar, Math.Max(ag, ab)), amin = Math.Min(ar, Math.Min(ag, ab));
            if (amax < 0.12) return "không màu";
            if (amax - amin < 0.15 * amax) return amax > 2 ? "đen" : "xám";
            // màu nhìn thấy = ánh sáng truyền qua; bỏ phần hấp thụ đều (xám) để màu đậm vẫn gọi đúng sắc
            double ex = Math.Exp(-Math.Min(3, amax - amin));
            RgbToHsv(Math.Exp(-(ar - amin) * 1.5 / Math.Max(1e-6, amax - amin)), Math.Exp(-(ag - amin) * 1.5 / Math.Max(1e-6, amax - amin)), Math.Exp(-(ab - amin) * 1.5 / Math.Max(1e-6, amax - amin)), out double h, out double sat, out _);
            double v = Math.Exp(-amin) * (0.4 + 0.6 * ex);
            string name;
            if (h < 12 || h >= 340) name = v > 0.75 && sat < 0.55 ? "hồng" : "đỏ";
            else if (h < 38) name = v < 0.45 ? "nâu đỏ" : "da cam";
            else if (h < 52) name = v < 0.55 ? "nâu" : "vàng nâu";
            else if (h < 70) name = "vàng";
            else if (h < 95) name = "vàng lục";
            else if (h < 165) name = "xanh lục";
            else if (h < 195) name = "xanh ngọc";
            else if (h < 250) name = "xanh lam";
            else if (h < 290) name = "tím";
            else name = v > 0.75 ? "hồng" : "tím hồng";
            if (amax < 0.35) name += " nhạt";
            else if (v < 0.3 && !name.StartsWith("nâu")) name += " thẫm";
            return name;
        }

        static void RgbToHsv(double r, double g, double b, out double h, out double s, out double v)
        {
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            v = max;
            double d = max - min;
            s = max <= 0 ? 0 : d / max;
            if (d < 1e-9) { h = 0; return; }
            if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
            if (h < 0) h += 360;
        }
    }
}
