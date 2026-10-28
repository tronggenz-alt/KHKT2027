using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Mô hình AAS ngọn lửa (data/may/tinh_chat/aas_nguyen_to.json, theo sổ tay phương pháp của Agilent):
    ///  • tổng nồng độ từng nguyên tố trong mẫu (mg/L), đếm từ công thức mọi loài chứa nguyên tố đó (ion, phức, kết tủa, cục rắn);
    ///  • độ hấp thụ theo nồng độ: giới hạn trên của khoảng làm việc cho A ≈ 0,8 (sổ tay: 0,8 – 1,0), vùng cao đường chuẩn cong;
    ///  • nguyên tố chỉ nguyên tử hóa được trong ngọn lửa N₂O – axetilen (Al) thì ngọn lửa không khí – axetilen gần như không có tín hiệu.
    /// </summary>
    public sealed class AasLibrary
    {
        public sealed class Element
        {
            public string Symbol, Name, Flame, Stoichiometry, Note;
            public double Line, Slit, LampMa, Lo, Hi;
            public bool OnlyN2O;

            /// <summary>Độ hấp thụ bão hòa (máy AAS ngọn lửa thật khó đọc trên A ≈ 2), mô phỏng.</summary>
            public const double Saturation = 2.0;

            /// <summary>Độ hấp thụ ở vạch khuyến cáo, ngọn lửa đúng loại, nồng độ mgL (mg/L) trong dung dịch đưa vào máy.</summary>
            public double Absorbance(double mgL, bool airAcetylene = true, double interference = 1)
            {
                if (mgL <= 0) return 0;
                if (OnlyN2O && airAcetylene) return 0.0005 * mgL / Hi;  // gần như không nguyên tử hóa được
                double lin = mgL * interference * 0.9 / Hi;
                double bent = lin / (1 + 0.14 * lin);  // cong dần
                return bent <= 1 ? bent : 1 + (Saturation - 1) * Math.Tanh((bent - 1) / (Saturation - 1));  // ở giới hạn trên A ≈ 0,8; trên A = 1 bão hòa dần về 2 (nối trơn)
            }

            /// <summary>Nồng độ đặc trưng (cho A = 0,0044) suy ra từ độ dốc vùng thấp.</summary>
            public double CharacteristicConc => 0.0044 * Hi / 0.9;

            /// <summary>Dãy chuẩn khuyên dùng (mg/L): 5%, 10%, 20%, 40% giới hạn trên, làm tròn đẹp.</summary>
            public double[] Standards() => new[] { 0.05, 0.1, 0.2, 0.4 }.Select(f => Nice(f * Hi)).ToArray();

            static double Nice(double v)
            {
                double p = Math.Pow(10, Math.Floor(Math.Log10(v)));
                double m = v / p;
                double n = m < 1.5 ? 1 : m < 2.2 ? 2 : m < 3.5 ? 2.5 : m < 7 ? 5 : 10;
                return Math.Round(n * p, 6);
            }
        }

        public readonly List<Element> Elements = new List<Element>();
        public string Source = "";

        public Element Get(string sym) => Elements.FirstOrDefault(e => e.Symbol == sym);

        public static AasLibrary Load(string json)
        {
            var lib = new AasLibrary();
            var root = MiniJson.Obj(MiniJson.Parse(json));
            lib.Source = MiniJson.S(root, "nguon");
            foreach (var o in MiniJson.A(root, "nguyen_to"))
            {
                var d = MiniJson.Obj(o);
                var r = MiniJson.A(d, "khoang");
                lib.Elements.Add(new Element
                {
                    Symbol = MiniJson.S(d, "ky_hieu"), Name = MiniJson.S(d, "ten"), Flame = MiniJson.S(d, "ngon_lua"), Stoichiometry = MiniJson.S(d, "kieu_lua"),
                    Note = MiniJson.S(d, "ghi_chu"), Line = MiniJson.N(d, "l"), Slit = MiniJson.N(d, "khe"), LampMa = MiniJson.N(d, "den_ma"),
                    Lo = r.Count > 0 ? (double)r[0] : 0, Hi = r.Count > 1 ? (double)r[1] : 1, OnlyN2O = MiniJson.B(d, "chi_n2o"),
                });
            }
            return lib;
        }

        // =====================================================================
        // Đếm nguyên tố trong mẫu
        // =====================================================================

        public static readonly Dictionary<string, double> AtomicMass = new Dictionary<string, double>
        {
            ["Cu"] = 63.55, ["Zn"] = 65.38, ["Fe"] = 55.85, ["Ni"] = 58.69, ["Pb"] = 207.2, ["Cr"] = 52.00, ["Mn"] = 54.94, ["Cd"] = 112.41,
            ["Co"] = 58.93, ["Mg"] = 24.31, ["Na"] = 22.99, ["K"] = 39.10, ["Ca"] = 40.08, ["Al"] = 26.98, ["Ag"] = 107.87, ["Hg"] = 200.59,
        };

        /// <summary>Loài có mã không phải công thức (phức, họ axit) → nguyên tố chứa trong đó.</summary>
        static readonly Dictionary<string, (string el, int n)[]> Special = new Dictionary<string, (string, int)[]>
        {
            ["CrVI"] = new[] { ("Cr", 1) }, ["CuNH3"] = new[] { ("Cu", 1) }, ["NiNH3"] = new[] { ("Ni", 1) }, ["ZnNH3"] = new[] { ("Zn", 1) },
            ["AgNH3"] = new[] { ("Ag", 1) }, ["FeSCN"] = new[] { ("Fe", 1) }, ["FeCN63-"] = new[] { ("Fe", 1) }, ["FeCN64-"] = new[] { ("Fe", 1) },
            ["CoCN63-"] = new[] { ("Co", 1) }, ["CuPol"] = new[] { ("Cu", 1) }, ["FePhenol"] = new[] { ("Fe", 1) }, ["HgI42-"] = new[] { ("Hg", 1) },
            ["AgMirror"] = new[] { ("Ag", 1) }, ["CaXP"] = new[] { ("Ca", 1) },
        };

        static readonly Regex Suffix = new Regex(@"(\((s|kt|l|r|than)\)|kt)$");
        static readonly Regex Charge = new Regex(@"\d*[+-]$");
        static readonly Regex Token = new Regex(@"([A-Z][a-z]?)(\d*)|([\(\[])|([\)\]])(\d*)");

        /// <summary>Số nguyên tử từng nguyên tố trong một mã loài (ví dụ "Fe4[Fe(CN)6]3" → Fe 7). Chỉ trả nguyên tố có trong bảng khối lượng.</summary>
        public static Dictionary<string, int> CountAtoms(string id)
        {
            var res = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(id)) return res;
            if (Special.TryGetValue(id, out var sp)) { foreach (var (el, n) in sp) res[el] = n; return res; }
            if (!char.IsUpper(id[0]) || id.Contains(":")) return res;  // chất hữu cơ, mã đặc biệt
            string f = Charge.Replace(Suffix.Replace(id, ""), "");
            var stack = new Stack<Dictionary<string, int>>();
            var cur = new Dictionary<string, int>();
            foreach (Match m in Token.Matches(f))
            {
                if (m.Groups[1].Success)
                {
                    int n = m.Groups[2].Value.Length > 0 ? int.Parse(m.Groups[2].Value) : 1;
                    cur[m.Groups[1].Value] = (cur.TryGetValue(m.Groups[1].Value, out var v) ? v : 0) + n;
                }
                else if (m.Groups[3].Success) { stack.Push(cur); cur = new Dictionary<string, int>(); }
                else if (m.Groups[4].Success && stack.Count > 0)
                {
                    int n = m.Groups[5].Value.Length > 0 ? int.Parse(m.Groups[5].Value) : 1;
                    var inner = cur;
                    cur = stack.Pop();
                    foreach (var kv in inner) cur[kv.Key] = (cur.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value * n;
                }
            }
            foreach (var kv in cur) if (AtomicMass.ContainsKey(kv.Key)) res[kv.Key] = kv.Value;
            return res;
        }

        /// <summary>Tổng nồng độ (mg/L) từng nguyên tố trong dung dịch; solids: phần nằm trong chất rắn / kết tủa (mg/L tương đương).</summary>
        public static Dictionary<string, double> ElementMgL(Mix mix, double volumeMl, Dictionary<string, double> solids = null)
        {
            var mol = new Dictionary<string, double>();
            var molSolid = new Dictionary<string, double>();
            void AddSpecies(SpeciesDef d, double n, int depth)
            {
                if (d == null || n <= 0 || depth > 3) return;
                if (d.Kind == SpeciesKind.Lump && d.Composition != null)
                {
                    foreach (var (id, k) in d.Composition) AddSpecies(SpeciesRegistry.Get(id), n * k, depth + 1);
                    return;
                }
                foreach (var kv in CountAtoms(d.Id))
                {
                    mol[kv.Key] = (mol.TryGetValue(kv.Key, out var v) ? v : 0) + n * kv.Value;
                    if (d.IsSolidPhase) molSolid[kv.Key] = (molSolid.TryGetValue(kv.Key, out var w) ? w : 0) + n * kv.Value;
                }
            }
            if (mix != null)
                for (int i = 0; i < mix.Count; i++)
                {
                    var d = mix.SpeciesAt(i);
                    double n = mix.MolAt(i);
                    if (d.Kind == SpeciesKind.Lump) { AddSpecies(d, n, 0); foreach (var (id, k) in d.Composition ?? Array.Empty<(string, double)>()) { var s = SpeciesRegistry.Get(id); foreach (var kv in CountAtoms(s?.Id)) molSolid[kv.Key] = (molSolid.TryGetValue(kv.Key, out var w) ? w : 0) + n * k * kv.Value; } }
                    else AddSpecies(d, n, 0);
                }
            double V = Math.Max(1e-6, volumeMl / 1000.0);
            var res = new Dictionary<string, double>();
            foreach (var kv in mol) res[kv.Key] = kv.Value / V * AtomicMass[kv.Key] * 1000.0;
            if (solids != null) foreach (var kv in molSolid) solids[kv.Key] = kv.Value / V * AtomicMass[kv.Key] * 1000.0;
            return res;
        }

        /// <summary>
        /// Hệ số ảnh hưởng (≤ 1) của nền mẫu trong ngọn lửa không khí – axetilen theo ghi chú của sổ tay (mô hình định tính):
        /// Ca bị photphat, sunfat, silicat, Al làm giảm mạnh nếu không thêm chất giải phóng (La / Sr); Na, K bị ion hóa một phần.
        /// </summary>
        public static double Interference(string sym, Mix mix, double volumeMl, bool releasingAgent, out string note)
        {
            note = null;
            if (mix == null) return 1;
            double V = Math.Max(1e-6, volumeMl / 1000.0);
            if (sym == "Ca" && !releasingAgent)
            {
                double interferent = (mix.Get("PO4") + mix.Get("SO4") + mix.Get("SiO3") + mix.Get("Al3+")) / V;
                if (interferent > 1e-4) { note = "Ca bị photphat / sunfat / silicat / Al làm giảm tín hiệu: thêm La hoặc Sr (chất giải phóng)"; return 0.7; }
            }
            if ((sym == "Na" || sym == "K") && !releasingAgent)
            {
                note = sym == "Na" ? "Na bị ion hóa một phần: thêm K 2000 µg/mL vào mọi dung dịch" : "K bị ion hóa một phần: thêm Cs 1000 µg/mL vào mọi dung dịch";
                return 0.85;
            }
            return 1;
        }
    }
}
