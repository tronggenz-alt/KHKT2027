using System;
using System.Collections.Generic;
using Khkt.Simulation.Chem;

namespace Khkt.Simulation
{
    [Serializable] public class ChemPart { public string s; public float n; }
    [Serializable] public class ChemLocation { public string ke; public int tang; public int o; }

    /// <summary>Một hóa chất trong danh mục (data/chem/hoa_chat.json, sinh bởi tools/chuyen_hoa_chat.py).</summary>
    [Serializable]
    public class ChemEntry
    {
        public int stt;
        public string ten, ten_khong_dau, cong_thuc, phan_loai, nhom, trang_thai, dang_chua, nong_do, ghi_chu, mau, ma_vi_tri;
        public float C, M, d, ka, kb;
        public float[] cac_C;
        public ChemPart[] tp;
        public bool tp_theo_lit;
        public string[] the, nguy_hiem;
        public ChemLocation vi_tri;

        public string Formula => SpeciesRegistry.Pretty(cong_thuc);
        public bool IsGas => trang_thai == "khi";
        public bool IsSolid => trang_thai == "ran" || trang_thai == "ket_tua";
        public bool IsPaper => trang_thai == "giay";
        public bool IsLiquidLike => trang_thai == "dd" || trang_thai == "long";
        public bool IsIndicatorDropper => dang_chua == "lo_nho_giot";
        public bool HasHazard(string h) => nguy_hiem != null && Array.IndexOf(nguy_hiem, h) >= 0;
    }

    [Serializable] public class ChemShelf { public string ma, nhom, ten, kieu_tu; public int so_tang, so_o, so_luong; }
    [Serializable] public class ChemGroup { public string ma, ten, kieu_tu, mau; public int so_luong; }
    [Serializable] public class ChemFile { public int phien_ban; public ChemGroup[] nhom; public ChemShelf[] ke; public ChemEntry[] hoa_chat; }

    [Serializable] public class EquipEntry { public int stt; public string ten, ten_tieng_anh, phan_loai, cong_dung, thong_so; }
    [Serializable] public class EquipFile { public int phien_ban; public EquipEntry[] thiet_bi; }

    /// <summary>
    /// Tạo lượng chất từ một mục danh mục, đúng thành phần (không dùng UnityEngine: Unity và kiểm thử dùng chung).
    ///   dung dịch, chất lỏng: amount = ml; chất rắn: amount = gam; khí: amount = mol (sục khí).
    /// </summary>
    public static class ChemMake
    {
        static readonly Dictionary<string, float> DissolveHeatKJ = new Dictionary<string, float>
        {
            ["NaOH"] = 44.5f, ["KOH"] = 57.6f, ["LiOH"] = 23.6f, ["NH4NO3"] = -25.7f, ["NH4Cl"] = -14.8f, ["KNO3"] = -34.9f,
            ["NaCl"] = -3.9f, ["KCl"] = -17.2f, ["CaCl2"] = 82.8f, ["MgSO4"] = 91.2f, ["CuSO4"] = 66.5f, ["CuSO4.5H2O"] = -11.5f,
            ["Na2CO3"] = 26.7f, ["CaO"] = 65f, ["Na2O"] = 237f, ["K2O"] = 300f, ["BaO"] = 150f, ["P2O5"] = 188f, ["AlCl3"] = 330f,
            ["FeCl3"] = 151f, ["ZnCl2"] = 73f, ["NaHCO3"] = -16.7f, ["Na2S2O3.5H2O"] = -47f, ["(NH2)2CO"] = -15.4f,
            ["MgSO4.7H2O"] = -16f, ["Na2SO4"] = 2.4f, ["KMnO4"] = -43.6f, ["Li2O"] = 130f, ["SrO"] = 120f, ["SO3"] = 132f,
        };

        static readonly Dictionary<string, float> DropsPerMl = new Dictionary<string, float> { ["pp"] = 20f };

        /// <summary>Tra mục danh mục theo số thứ tự (Unity: ChemicalCatalog; kiểm thử: bộ đọc JSON).</summary>
        public static Func<int, ChemEntry> Lookup;
        /// <summary>Toàn bộ danh mục (để tìm sản phẩm hữu cơ theo công thức / tên).</summary>
        public static Func<IEnumerable<ChemEntry>> All;

        static ChemMake() { ChemEngine.FindOrganic = FindOrganic; }

        /// <summary>Loài hữu cơ trong danh mục theo công thức ("CH3COOC2H5") hoặc theo tên không dấu ("ten:glucoz").</summary>
        public static SpeciesDef FindOrganic(string query)
        {
            if (All == null || string.IsNullOrEmpty(query)) return null;
            bool byName = query.StartsWith("ten:");
            string q = byName ? query.Substring(4) : query;
            foreach (var e in All())
            {
                if (byName ? (e.ten_khong_dau ?? "").StartsWith(q) : e.cong_thuc == q)
                {
                    var sp = OrganicSpecies(e);
                    if (sp != null) return sp;
                }
            }
            return null;
        }

        /// <summary>Loài hữu cơ (phân tử, axit/bazơ yếu) của hóa chất không có thành phần ion.</summary>
        public static SpeciesDef OrganicSpecies(ChemEntry e)
        {
            bool organic = (e.the != null && e.the.Length > 0) || e.ka > 0 || e.kb > 0;
            if (!organic) return null;
            return ChemEngine.RegisterOrganic(e.stt, e.ten.ToLowerInvariant(), e.cong_thuc, e.the, e.ka, e.kb, e.M > 0 ? e.M : 150, e.d);
        }

        /// <summary>Chất rắn khó tan đã có sẵn trong bộ máy dưới dạng kết tủa (CaCO₃, BaSO₄, Cu(OH)₂...): cho vào là kết tủa, không phải "cục tan dần".</summary>
        static string InsolubleId(ChemEntry e)
        {
            if (e.cong_thuc == null || !(e.trang_thai == "ran" || e.trang_thai == "ket_tua")) return null;
            string f = e.cong_thuc;
            foreach (var p in ChemEngine.Pairs) if (p.Ppt == f && p.Solubility < 2e-3) return p.Ppt;
            var hy = ChemEngine.HydroxideFor(f);
            return hy;
        }

        public static (float ml, Mix mix, float pp) Make(ChemEntry e, float amount, float concentration = 0)
        {
            if (e == null || amount <= 0) return (0, Mix.Empty, 0);
            var b = new MixBuilder();
            float pp = 0;
            float volume = 0;
            float c = concentration > 0 ? concentration : e.C;
            var org = (e.tp == null || e.tp.Length == 0) ? OrganicSpecies(e) : null;

            void AddParts(double mol, bool perLiter, double liters)
            {
                if (e.tp == null) return;
                foreach (var p in e.tp)
                {
                    double n = perLiter ? p.n * liters : p.n * mol;
                    if (p.s == "pp") { pp += (float)(liters * 1000 * DropsPerMl["pp"]); continue; }
                    if (SpeciesRegistry.Get(p.s) == null) continue;
                    b.Add(p.s, n);
                }
            }

            switch (e.trang_thai)
            {
                case "dd":
                {
                    volume = amount;
                    double L = amount / 1000.0;
                    if (e.ghi_chu == "ethanol")
                    {
                        var eth = Lookup?.Invoke(468);
                        var sp = eth != null ? OrganicSpecies(eth) : null;
                        if (sp != null) b.Add(sp.Index, c * L);
                    }
                    else if (e.tp_theo_lit) AddParts(0, true, L);
                    else AddParts(c * L, false, L);
                    if (org != null) b.Add(org.Index, Math.Max(c, 0.05) * L);
                    break;
                }
                case "long":
                {
                    // thủy ngân: chất lỏng nhưng không phải dung dịch (giọt kim loại)
                    if (e.tp != null && e.tp.Length == 1 && SpeciesRegistry.Get(e.tp[0].s)?.Kind == SpeciesKind.Solid)
                    {
                        b.Add(e.tp[0].s, amount * Math.Max(1, e.d) / Math.Max(1, e.M));
                        break;
                    }
                    volume = amount;
                    // danh mục không ghi M (dầu ăn, dầu hỏa...): dùng M ước lượng của loài hữu cơ, để vẫn có lượng chất (tách lớp, bay hơi)
                    double Ml = e.M > 0 ? e.M : org != null ? org.MolarMass : 0;
                    double mol = Ml > 0 ? amount * Math.Max(0.3, e.d) / Ml : 0;
                    if (e.tp_theo_lit) AddParts(0, true, amount / 1000.0);
                    else AddParts(mol, false, 0);
                    if (org != null) b.Add(org.Index, mol);
                    break;
                }
                case "khi":
                {
                    double mol = amount;
                    AddParts(mol, false, 0);
                    // khí không tan, không phản ứng với nước (H₂, O₂, N₂, CO...): vẫn giữ lượng khí để phản ứng khí - rắn khi đun, và để bộ quan sát báo
                    if ((e.tp == null || e.tp.Length == 0) && org == null && SpeciesRegistry.Get("khi:" + e.cong_thuc) != null) b.Add("khi:" + e.cong_thuc, mol);
                    if (org != null) b.Add(org.Index, mol * 0.05);  // khí hữu cơ tan rất ít
                    break;
                }
                case "giay":
                    break;
                default:  // rắn, kết tủa: một cục chất rắn tan dần
                {
                    double M = e.M > 0 ? e.M : 150;
                    double mol = amount / M;
                    string ppt = InsolubleId(e);
                    if (ppt != null)
                    {
                        double k = ppt == "Ag2O" ? 0.5 : 1;
                        b.Add(ppt, k * mol);
                        break;
                    }
                    var comp = new List<(string, double)>();
                    if (e.tp != null) foreach (var p in e.tp) if (SpeciesRegistry.Get(p.s) != null) comp.Add((p.s, p.n));
                    // kim loại, oxit không tan, chất rắn đặc biệt: cho thẳng chất rắn đó (không phải "cục tan dần")
                    if (comp.Count == 1 && org == null)
                    {
                        var only = SpeciesRegistry.Get(comp[0].Item1);
                        if (only.Kind == SpeciesKind.Solid || only.Kind == SpeciesKind.Precipitate) { b.Add(only.Index, comp[0].Item2 * mol); break; }
                    }
                    if (org != null && comp.Count == 0)
                    {
                        // chất hữu cơ ít tan dạng phân tử (phenol, axit benzoic...): là chất rắn tách pha, tan theo độ tan và pH
                        string sep = ChemEngine.SeparatedId(org);
                        if (sep != null) { b.Add(sep, mol); break; }
                        // axit, bazơ hữu cơ tan tốt (axit xitric, oxalic, amino axit): tan ngay, phản ứng axit - bazơ thấy được ngay khi trộn
                        if (org.Kind == SpeciesKind.Weak && !org.HasTag("khong_tan")) { b.Add(org.Index, mol); break; }
                        // polime, xenlulozơ, naphtalen...: chất rắn không tan, giữ nhãn tính chất
                        if (org.HasTag("khong_tan"))
                        {
                            var inertOrg = ChemEngine.RegisterInert(e.stt, e.ten, e.cong_thuc, Rgb.Hex(e.mau), M);
                            foreach (var t in org.Tags) if (t != "khong_tan") inertOrg.Tags.Add(t);
                            b.Add(inertOrg.Index, mol);
                            break;
                        }
                    }
                    if (org != null) comp.Add((org.Id, 1));
                    if (comp.Count == 0)
                    {
                        // vật liệu trơ (polime, bông thủy tinh, đá bọt...): vẫn là một chất rắn trong bình, không phản ứng
                        var inert = ChemEngine.RegisterInert(e.stt, e.ten, e.cong_thuc, Rgb.Hex(e.mau), M);
                        b.Add(inert.Index, mol);
                        break;
                    }
                    DissolveHeatKJ.TryGetValue(e.cong_thuc ?? "", out float heat);
                    var lump = ChemEngine.RegisterLump(e.stt, e.ten, e.cong_thuc, comp.ToArray(), M, Rgb.Hex(e.mau), heat, e.the);
                    b.Add(lump.Index, mol);
                    break;
                }
            }
            return (volume, b.ToMix(), pp);
        }
    }
}
