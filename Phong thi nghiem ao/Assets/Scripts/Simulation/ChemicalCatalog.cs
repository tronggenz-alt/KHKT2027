using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Khkt.Simulation.Chem;
using UnityEngine;

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
    /// Danh mục 720 hóa chất và 80 thiết bị: tra cứu, tìm kiếm không dấu, và tạo lượng chất (Solution)
    /// đúng thành phần để cho vào vật chứa.
    /// </summary>
    public class ChemicalCatalog : MonoBehaviour
    {
        public TextAsset chemicalsJson;
        public TextAsset equipmentJson;
        [Tooltip("Nhiệt độ sôi, nhiệt hóa hơi (nhiet_do_soi.json) cho chưng cất / hồi lưu / cô cạn")]
        public TextAsset boilingJson;

        public static ChemicalCatalog Instance { get; private set; }
        public ChemEntry[] Chemicals { get; private set; } = Array.Empty<ChemEntry>();
        public ChemShelf[] Shelves { get; private set; } = Array.Empty<ChemShelf>();
        public ChemGroup[] Groups { get; private set; } = Array.Empty<ChemGroup>();
        public EquipEntry[] Equipment { get; private set; } = Array.Empty<EquipEntry>();

        readonly Dictionary<int, ChemEntry> byStt = new Dictionary<int, ChemEntry>();
        readonly Dictionary<string, ChemShelf> shelfByCode = new Dictionary<string, ChemShelf>();
        readonly Dictionary<string, ChemGroup> groupByCode = new Dictionary<string, ChemGroup>();

        static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        void Awake()
        {
            Instance = this;
            Load();
        }

        public void Load()
        {
            if (chemicalsJson != null)
            {
                var f = JsonUtility.FromJson<ChemFile>(chemicalsJson.text);
                Chemicals = f.hoa_chat ?? Array.Empty<ChemEntry>();
                Shelves = f.ke ?? Array.Empty<ChemShelf>();
                Groups = f.nhom ?? Array.Empty<ChemGroup>();
            }
            if (equipmentJson != null)
                Equipment = JsonUtility.FromJson<EquipFile>(equipmentJson.text).thiet_bi ?? Array.Empty<EquipEntry>();
            if (boilingJson != null) Khkt.Simulation.Chem.Volatility.Load(boilingJson.text);
            byStt.Clear();
            foreach (var c in Chemicals) byStt[c.stt] = c;
            foreach (var s in Shelves) shelfByCode[s.ma] = s;
            foreach (var g in Groups) groupByCode[g.ma] = g;
        }

        public ChemEntry Get(int stt) => byStt.TryGetValue(stt, out var c) ? c : null;
        public ChemShelf Shelf(string code) => code != null && shelfByCode.TryGetValue(code, out var s) ? s : null;
        public ChemGroup Group(string code) => code != null && groupByCode.TryGetValue(code, out var g) ? g : null;

        /// <summary>"Kệ K04 · Muối halogenua · tầng 3 · ô 5"</summary>
        public string LocationText(ChemEntry e)
        {
            if (e?.vi_tri == null) return "";
            var s = Shelf(e.vi_tri.ke);
            return $"Kệ {e.vi_tri.ke} · tầng {e.vi_tri.tang} · ô {e.vi_tri.o}" + (s != null ? $" ({Group(s.nhom)?.ten ?? s.ten})" : "");
        }

        // ------------------------------------------------------------------
        // Tìm kiếm (không dấu, theo tên, công thức, nhóm, số thứ tự)
        // ------------------------------------------------------------------

        public static string Fold(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace('đ', 'd').Replace('Đ', 'D');
            var norm = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(norm.Length);
            foreach (char c in norm)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat == UnicodeCategory.NonSpacingMark) continue;
                int sub = "₀₁₂₃₄₅₆₇₈₉".IndexOf(c);
                if (sub >= 0) { sb.Append((char)('0' + sub)); continue; }
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        /// <summary>Kết quả xếp theo độ khớp: trùng công thức &gt; tên bắt đầu bằng &gt; chứa từ khóa.</summary>
        public List<ChemEntry> Search(string query, int max = 30)
        {
            var result = new List<(int score, ChemEntry e)>();
            string q = Fold(query).Trim();
            if (q.Length == 0) return new List<ChemEntry>();
            string[] words = q.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int.TryParse(q, out int sttQuery);
            foreach (var c in Chemicals)
            {
                string name = c.ten_khong_dau ?? Fold(c.ten);
                string formula = (c.cong_thuc ?? "").ToLowerInvariant();
                string group = Fold(Group(c.nhom)?.ten) + " " + Fold(c.phan_loai);
                int score = 0;
                if (sttQuery > 0 && c.stt == sttQuery) score = 1000;
                if (formula.Length > 0 && formula == q) score = Math.Max(score, 900);
                else if (formula.Length > 0 && formula.StartsWith(q) && q.Length >= 2) score = Math.Max(score, 600);
                if (name == q) score = Math.Max(score, 850);
                else if (name.StartsWith(q)) score = Math.Max(score, 700);
                bool all = true;
                foreach (var w in words) if (!(name.Contains(w) || formula.Contains(w) || group.Contains(w))) { all = false; break; }
                if (all) score = Math.Max(score, 300 + (name.Contains(q) ? 100 : 0));
                if (score > 0) result.Add((score - (c.trang_thai == "dd" ? 0 : 3) - name.Length / 20, c));
            }
            result.Sort((a, b) => b.score.CompareTo(a.score));
            var list = new List<ChemEntry>();
            for (int i = 0; i < result.Count && i < max; i++) list.Add(result[i].e);
            return list;
        }

        // ------------------------------------------------------------------
        // Tạo lượng chất
        // ------------------------------------------------------------------

        static readonly Dictionary<string, float> DissolveHeatKJ = new Dictionary<string, float>
        {
            ["NaOH"] = 44.5f, ["KOH"] = 57.6f, ["LiOH"] = 23.6f, ["NH4NO3"] = -25.7f, ["NH4Cl"] = -14.8f, ["KNO3"] = -34.9f,
            ["NaCl"] = -3.9f, ["KCl"] = -17.2f, ["CaCl2"] = 82.8f, ["MgSO4"] = 91.2f, ["CuSO4"] = 66.5f, ["CuSO4.5H2O"] = -11.5f,
            ["Na2CO3"] = 26.7f, ["CaO"] = 65f, ["Na2O"] = 237f, ["K2O"] = 300f, ["BaO"] = 150f, ["P2O5"] = 188f, ["AlCl3"] = 330f,
            ["FeCl3"] = 151f, ["ZnCl2"] = 73f, ["NaHCO3"] = -16.7f, ["Na2S2O3.5H2O"] = -47f, ["(NH2)2CO"] = -15.4f,
            ["MgSO4.7H2O"] = -16f, ["Na2SO4"] = 2.4f, ["KMnO4"] = -43.6f,
        };

        /// <summary>Loài hữu cơ (phân tử, axit/bazơ yếu) của hóa chất không có thành phần ion.</summary>
        public SpeciesDef OrganicSpecies(ChemEntry e)
        {
            bool organic = (e.the != null && e.the.Length > 0) || e.ka > 0 || e.kb > 0;
            if (!organic) return null;
            return ChemEngine.RegisterOrganic(e.stt, e.ten.ToLowerInvariant(), e.cong_thuc, e.the, e.ka, e.kb, e.M > 0 ? e.M : 150, e.d);
        }

        static readonly Dictionary<string, float> DropsPerMl = new Dictionary<string, float> { ["pp"] = 20f };

        /// <summary>
        /// Lượng chất để đổ vào vật chứa:
        ///   dung dịch, chất lỏng: amount = ml; chất rắn: amount = gam; khí: amount = mol (sục khí).
        /// concentration &lt;= 0: dùng nồng độ gợi ý trong danh mục.
        /// </summary>
        public Solution Make(ChemEntry e, float amount, float concentration = 0)
        {
            if (e == null || amount <= 0) return default;
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
                    if (SpeciesRegistry.Get(p.s) == null) { Debug.LogWarning($"[Hóa chất] {e.ten}: chưa có loài {p.s}"); continue; }
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
                        var eth = Get(468);
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
                    if (org != null) b.Add(org.Index, mol * 0.05);  // khí hữu cơ tan rất ít
                    break;
                }
                case "giay":
                    break;
                default:  // rắn, kết tủa: một cục chất rắn tan dần
                {
                    double M = e.M > 0 ? e.M : 150;
                    double mol = amount / M;
                    var comp = new List<(string, double)>();
                    if (e.tp != null) foreach (var p in e.tp) if (SpeciesRegistry.Get(p.s) != null) comp.Add((p.s, p.n));
                    if (org != null) comp.Add((org.Id, 1));
                    if (comp.Count == 0) { volume = 0; break; }
                    DissolveHeatKJ.TryGetValue(e.cong_thuc ?? "", out float heat);
                    var lump = ChemEngine.RegisterLump(e.stt, e.ten.ToLowerInvariant(), e.cong_thuc, comp.ToArray(), M, Rgb.Hex(e.mau), heat, e.the);
                    b.Add(lump.Index, mol);
                    break;
                }
            }
            return Solution.Of(volume, b.ToMix(), pp);
        }

        /// <summary>Nồng độ hiển thị: "0,1 M" hoặc chuỗi gốc trong danh mục.</summary>
        public static string ConcText(ChemEntry e, float c = 0)
        {
            if (e.trang_thai == "dd" && !e.tp_theo_lit && (c > 0 || e.C > 0)) return (c > 0 ? c : e.C).ToString("0.###", Vi) + " M";
            return e.nong_do;
        }

        public static string StateText(ChemEntry e)
        {
            switch (e.trang_thai)
            {
                case "dd": return "Dung dịch";
                case "long": return "Chất lỏng";
                case "khi": return "Khí";
                case "giay": return "Giấy";
                case "ket_tua": return "Chất rắn (kết tủa)";
                default: return "Chất rắn";
            }
        }

        public static string HazardText(ChemEntry e)
        {
            if (e.nguy_hiem == null || e.nguy_hiem.Length == 0) return "";
            var parts = new List<string>();
            foreach (var h in e.nguy_hiem)
                switch (h)
                {
                    case "doc": parts.Add("Độc"); break;
                    case "an_mon": parts.Add("Ăn mòn"); break;
                    case "de_chay": parts.Add("Dễ cháy"); break;
                    case "oxi_hoa": parts.Add("Oxi hóa mạnh"); break;
                    case "phan_ung_nuoc": parts.Add("Phản ứng với nước"); break;
                    case "no": parts.Add("Có thể nổ"); break;
                }
            return string.Join(" · ", parts);
        }
    }
}
