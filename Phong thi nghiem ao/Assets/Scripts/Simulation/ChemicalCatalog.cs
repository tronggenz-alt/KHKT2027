using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Khkt.Simulation.Chem;
using UnityEngine;

namespace Khkt.Simulation
{
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
            ChemMake.Lookup = Get;
            ChemMake.All = () => Chemicals;
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

        /// <summary>Loài hữu cơ (phân tử, axit/bazơ yếu) của hóa chất không có thành phần ion.</summary>
        public SpeciesDef OrganicSpecies(ChemEntry e) => ChemMake.OrganicSpecies(e);

        /// <summary>
        /// Lượng chất để đổ vào vật chứa:
        ///   dung dịch, chất lỏng: amount = ml; chất rắn: amount = gam; khí: amount = mol (sục khí).
        /// concentration &lt;= 0: dùng nồng độ gợi ý trong danh mục.
        /// </summary>
        public Solution Make(ChemEntry e, float amount, float concentration = 0)
        {
            if (e == null || amount <= 0) return default;
            var (ml, mix, pp) = ChemMake.Make(e, amount, concentration);
            return Solution.Of(ml, mix, pp);
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
