// Kiểm thử phản ứng theo chương trình (THCS, THPT, nâng cao) và bất biến trên mọi cặp hóa chất trong danh mục.
// Dùng đúng mã của game (ChemMake, ChemMixer, ChemEngine.DryStep), không cần Unity.
// Chạy:  powershell -ExecutionPolicy Bypass -File tools/kiem_thu_phan_ung.ps1            (bộ phản ứng chuẩn)
//        powershell -ExecutionPolicy Bypass -File tools/kiem_thu_phan_ung.ps1 cap 0 1   (mọi cặp, phân mảnh 0/1)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Khkt.Simulation;
using Khkt.Simulation.Chem;

static class PhanUngTests
{
    static readonly Dictionary<int, ChemEntry> cat = new Dictionary<int, ChemEntry>();

    // -----------------------------------------------------------------------
    // Đọc danh mục bằng MiniJson (giống dữ liệu Unity đọc)
    // -----------------------------------------------------------------------
    static void Load(string root)
    {
        var doc = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(Path.Combine(root, "data/chem/hoa_chat.json")));
        foreach (Dictionary<string, object> h in (List<object>)doc["hoa_chat"])
        {
            float F(string k) => h.TryGetValue(k, out var v) && v is double d ? (float)d : 0f;
            string S(string k) => h.TryGetValue(k, out var v) ? v as string : null;
            var e = new ChemEntry
            {
                stt = (int)(double)h["stt"], ten = S("ten"), ten_khong_dau = S("ten_khong_dau"), cong_thuc = S("cong_thuc"), trang_thai = S("trang_thai"), ghi_chu = S("ghi_chu"),
                mau = S("mau"), nhom = S("nhom"), phan_loai = S("phan_loai"), C = F("C"), M = F("M"), d = F("d"), ka = F("ka"), kb = F("kb"),
                tp_theo_lit = h.TryGetValue("tp_theo_lit", out var tl) && tl is bool bb && bb,
            };
            e.tp = ((List<object>)h["tp"]).Select(o => { var p = (Dictionary<string, object>)o; return new ChemPart { s = (string)p["s"], n = (float)(double)p["n"] }; }).ToArray();
            e.the = ((List<object>)h["the"]).Select(o => (string)o).ToArray();
            cat[e.stt] = e;
        }
        ChemMake.Lookup = s => cat.TryGetValue(s, out var x) ? x : null;
        ChemMake.All = () => cat.Values;
    }

    // -----------------------------------------------------------------------
    // Một bình: giống LabContainer (trộn, chạy theo thời gian, nhiệt) + FluidNetwork (đun khô)
    // -----------------------------------------------------------------------
    sealed class Vessel
    {
        public double ml, T = 25, eqT = 25, now;
        public float pp;
        public Mix mix = Mix.Empty;
        public readonly List<ChemEvent> ev = new List<ChemEvent>();
        readonly HeatWatch heat = new HeatWatch();
        public bool record = true;

        public void Add((float ml, Mix mix, float pp) s)
        {
            var tmp = new List<ChemEvent>();
            if (ml + s.ml < 0.002) { mix = mix + s.mix; ml += s.ml; pp += s.pp; return; }
            mix = ChemMixer.Merge(mix, ml, pp, s.mix, s.ml, s.pp, new ChemEnv { TempC = T }, tmp, out double J);
            ml += s.ml; pp += s.pp;
            eqT = T;
            Warm(J);
            if (record) ev.AddRange(tmp);
        }

        void Warm(double J)
        {
            if (J == 0 || ml < 0.05) return;
            double before = T;
            T = Math.Max(-10, Math.Min(100, T + J / (ml * 4.18)));
            var e = heat.Add(T - before, now, T);
            if (e != null && record) ev.Add(e);
        }

        public void Run(double seconds, double dt, double forceT = -1, bool air = true)
        {
            for (double t = 0; t < seconds; t += dt)
            {
                now += dt;
                if (forceT > 0) T = forceT;
                var tmp = new List<ChemEvent>();
                if (ml > 0.002)
                {
                    float p = pp;
                    mix = ChemMixer.Tick(mix, ml, dt, new ChemEnv { TempC = T }, ref p, ref eqT, tmp, out double J, out _);
                    pp = p;
                    if (forceT <= 0) Warm(J);
                }
                else if (!mix.IsEmpty)
                {
                    var b = mix.ToBuilder();
                    var gases = new Dictionary<string, double>();
                    ChemEngine.DryStep(b, T, dt, air, tmp, gases, out _);
                    mix = b.ToMix();
                }
                if (record) foreach (var e in tmp) if (!ev.Any(x => x.Key == e.Key)) ev.Add(e);
            }
        }

        public double Ph => ml > 0.002 ? ChemEngine.Ph(mix, ml / 1000) : 7;
    }

    // -----------------------------------------------------------------------
    // Bộ phản ứng chuẩn
    // -----------------------------------------------------------------------
    sealed class P { public int stt; public float amt, c; }
    sealed class G { public string lv, name, need; public P[] ps; public double T = 25; public double sec = 40; }
    static P D(int s, float c = 0, float ml = 5) => new P { stt = s, c = c, amt = ml };
    static P R(int s, float g = 0.3f) => new P { stt = s, amt = g };
    static P K(int s, float mol = 0.0004f) => new P { stt = s, amt = mol };
    static P W(float ml = 5) => D(95, 0, ml);
    static readonly List<G> Gold = new List<G>();
    static string lv;
    static void A(string name, string need, params P[] ps) => Gold.Add(new G { lv = lv, name = name, need = need, ps = ps });
    static void H(double T, string name, string need, params P[] ps) => Gold.Add(new G { lv = lv, name = name, need = need, ps = ps, T = T });

    /// <summary>
    /// need: các điều kiện cách nhau bởi dấu cách, tất cả phải đúng. Mỗi điều kiện là một hay nhiều khả năng nối bằng "|":
    ///   khóa sự kiện (so khớp đầu chuỗi: "kt:AgCl", "khi:H2"), "~chữ" có trong tiêu đề, "=chữ" có trong phương trình,
    ///   "!x" không được có x. Mọi phương trình phát ra phải cân bằng.
    /// </summary>
    static string Judge(G g, List<ChemEvent> ev)
    {
        foreach (var e in ev)
        {
            var bad = Equation.Check(e.Equation);
            if (bad != null) return $"PT lệch ({e.Equation}: {bad})";
        }
        bool Match(string tok)
        {
            foreach (var alt0 in tok.Split('|'))
            {
                string alt = alt0.StartsWith("~") || alt0.StartsWith("=") ? alt0.Replace('_', ' ') : alt0;
                if (alt.StartsWith("~")) { if (ev.Any(e => (e.Title ?? "").IndexOf(alt.Substring(1), StringComparison.OrdinalIgnoreCase) >= 0)) return true; }
                else if (alt.StartsWith("=")) { if (ev.Any(e => (e.Equation ?? "").Contains(alt.Substring(1)))) return true; }
                else if (ev.Any(e => (e.Key ?? "").StartsWith(alt))) return true;
            }
            return false;
        }
        foreach (var tok in g.need.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (tok.StartsWith("!")) { if (Match(tok.Substring(1))) return "có thừa " + tok.Substring(1); }
            else if (!Match(tok)) return "thiếu " + tok;
        }
        if (ev.Count == 0) return "không có thông báo nào";
        return null;
    }

    static bool debug;

    static (Vessel v, string verdict) RunGold(G g)
    {
        var v = new Vessel();
        bool liquid = false;
        foreach (var p in g.ps)
        {
            var e = cat[p.stt];
            v.Add(ChemMake.Make(e, p.amt, p.c));
            liquid |= v.ml > 0.002;
            v.Run(liquid ? 2 : 0.5, 0.25);
            if (debug) Console.WriteLine($"         [gỡ lỗi] sau {e.ten}: pH {v.Ph:0.00}, {v.T:0} °C, {v.ml:0.##} ml: {v.mix}");
        }
        v.Run(g.sec, 0.25, g.T > 25 ? g.T : -1);
        if (debug) Console.WriteLine($"         [gỡ lỗi] cuối: pH {v.Ph:0.00}, {v.T:0} °C: {v.mix}");
        return (v, Judge(g, v.ev));
    }

    /// <summary>Thử trộn các chất theo số thứ tự danh mục (liều mặc định như trong game: 5 ml dung dịch, 0,3 g rắn), in mọi thẻ hiện tượng.</summary>
    static int Try(int[] stts)
    {
        var v = new Vessel();
        foreach (int s in stts)
        {
            var e = cat[s];
            float dose = e.trang_thai == "khi" ? 0.0004f : e.trang_thai == "dd" || e.trang_thai == "long" ? 5f : 0.3f;
            int before = v.ev.Count;
            v.Add(ChemMake.Make(e, dose));
            v.Run(20, 0.25);
            Console.WriteLine($"+ {e.ten} ({e.cong_thuc}, {ChemicalConc(e)}) → pH {v.Ph:0.0}, {v.T:0} °C");
            for (int i = before; i < v.ev.Count; i++)
            {
                var x = v.ev[i];
                Console.WriteLine($"   [{x.Kind}] {x.Title}");
                if (!string.IsNullOrEmpty(x.Equation)) Console.WriteLine($"      PT: {x.Equation}");
                if (!string.IsNullOrEmpty(x.Detail)) Console.WriteLine($"      {x.Detail}");
            }
        }
        var look = ChemColor.Compute(v.mix, v.ml, v.Ph, v.T);
        Console.WriteLine($"Cuối: {v.ml:0.#} ml, dung dịch {ChemPalette.ColorName(look)}, độ đục {look.Turbidity:0.00}; thành phần: {v.mix}");
        return 0;
    }

    static string ChemicalConc(ChemEntry e) => e.trang_thai == "dd" ? $"{e.C:0.###} M, 5 ml" : e.trang_thai == "long" ? "5 ml" : e.trang_thai == "khi" ? "sục khí" : "0,3 g";

    static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("KHKT_CHEM_OUT") == null) Console.OutputEncoding = System.Text.Encoding.UTF8;  // Mono (Unity): đổi mã hóa làm mất Console.SetOut
        Load(Environment.GetEnvironmentVariable("KHKT_ROOT") ?? Directory.GetCurrentDirectory());
        if (args.Length > 0 && args[0] == "cap") return Pairs(int.Parse(args[1]), int.Parse(args[2]), args.Length > 3 ? args[3] : null);
        if (args.Length > 0 && args[0] == "thu") return Try(args.Skip(1).Select(int.Parse).ToArray());
        BuildGold();
        int ok = 0;
        var byLevel = new SortedDictionary<string, (int ok, int all)>();
        string filter = args.Length > 0 ? args[0] : null;
        bool verbose = args.Contains("-v") || args.Contains("-d");
        debug = args.Contains("-d");
        foreach (var g in Gold)
        {
            if (filter != null && filter != "-v" && filter != "-d" && !g.name.Contains(filter)) continue;
            var (v, verdict) = RunGold(g);
            byLevel.TryGetValue(g.lv, out var c);
            c.all++;
            if (verdict == null) { ok++; c.ok++; }
            byLevel[g.lv] = c;
            if (verdict != null || verbose)
            {
                Console.WriteLine($"  {(verdict == null ? "ok  " : "SAI ")} [{g.lv}] {g.name}: {verdict}");
                foreach (var e in v.ev) Console.WriteLine($"         · {e.Key} | {e.Title} | {e.Equation}");
            }
        }
        Console.WriteLine();
        foreach (var kv in byLevel) Console.WriteLine($"{kv.Key}: {kv.Value.ok}/{kv.Value.all}");
        int total = byLevel.Values.Sum(x => x.all);
        Console.WriteLine($"\n{ok}/{total} phản ứng đạt");
        return ok == total ? 0 : 1;
    }

    // -----------------------------------------------------------------------
    // Mọi cặp: bất biến
    //   1. lần trộn nào có cả hai chất cũng có ít nhất một thẻ (phản ứng / không phản ứng / đang tan)
    //   2. mọi phương trình phát ra đều cân bằng
    //   3. không có thẻ "không phản ứng" khi thành phần đã đổi
    // -----------------------------------------------------------------------
    static int Pairs(int k, int N, string outPath)
    {
        var list = cat.Values.Where(e => e.trang_thai != "giay").OrderBy(e => e.stt).ToList();
        int pairs = 0, silent = 0, unbalanced = 0, lies = 0, reacted = 0, noReact = 0;
        var badEq = new Dictionary<string, string>();
        var silentEx = new List<string>();
        using var w = outPath != null ? new StreamWriter(outPath) : null;
        bool Liquid(ChemEntry e) => e.trang_thai == "dd" || e.trang_thai == "long";
        float Dose(ChemEntry e) => e.trang_thai == "khi" ? 0.0004f : Liquid(e) ? 5f : 0.3f;
        for (int i = 0; i < list.Count; i++)
            for (int j = i + 1; j < list.Count; j++)
            {
                if ((i * 31 + j) % N != k) continue;
                var a = list[i]; var b = list[j];
                var first = Liquid(a) ? a : Liquid(b) ? b : a;
                var second = ReferenceEquals(first, a) ? b : a;
                pairs++;
                var v = new Vessel { record = false };
                v.Add(ChemMake.Make(first, Dose(first)));
                v.Run(1, 0.5);
                v.record = true;
                v.Add(ChemMake.Make(second, Dose(second)));
                v.Run(10, 1);
                v.Run(10, 1, 85);
                if (v.ev.Count == 0) { silent++; if (silentEx.Count < 40) silentEx.Add($"{a.stt} {a.ten} + {b.stt} {b.ten}"); }
                bool any = v.ev.Any(e => e.Kind != EventKind.NoReaction && e.Kind != EventKind.Heat);
                if (any) reacted++; else noReact++;
                foreach (var e in v.ev)
                {
                    var bad = Equation.Check(e.Equation);
                    if (bad != null) { unbalanced++; badEq[e.Equation] = $"{bad} · {a.ten} + {b.ten}"; }
                }
                w?.WriteLine(string.Join("\t", a.stt, b.stt, string.Join(",", v.ev.Select(e => e.Key))));
            }
        Console.WriteLine($"cặp {pairs}; có phản ứng {reacted}; thẻ không phản ứng / hòa tan {noReact}; KHÔNG CÓ THẺ {silent}; phương trình lệch {unbalanced}");
        foreach (var s in silentEx) Console.WriteLine("  im lặng: " + s);
        foreach (var kv in badEq.Take(40)) Console.WriteLine($"  lệch: {kv.Key}  ({kv.Value})");
        return silent == 0 && unbalanced == 0 ? 0 : 1;
    }

    // -----------------------------------------------------------------------
    static void BuildGold()
    {
        lv = "1 Tiểu học";
        A("Giấm + baking soda sủi bọt", "khi:CO2", D(655), R(660, 1));
        A("Giấm + đá vôi sủi bọt", "khi:CO2", D(655), R(279, 0.5f));
        A("Nước vôi trong + CO₂ vẩn đục", "kt:CaCO3", D(127), K(645));
        A("Dầu ăn + nước tách lớp", "lop:", W(), D(550, 0, 2));
        A("Muối ăn tan trong nước", "hoatan:|quan_sat:hoa_tan !kt: !khi:", W(), R(661, 0.5f));
        A("Đường tan trong nước", "hoatan:|quan_sat:hoa_tan !kt:", W(), R(580, 0.5f));
        A("Hồ tinh bột + iot hóa xanh tím", "phuc:I2tb", D(401), D(407, 0, 1));
        A("Xà phòng + nước cứng tạo váng", "kt:CaXP", D(153), R(675, 0.5f));
        A("Thuốc tím tan trong nước", "hoatan:|quan_sat:", W(), D(659, 0, 1));
        A("Vôi sống + nước tỏa nhiệt", "nhiet", W(), R(668, 1));
        A("Muối amoni nitrat tan, nước lạnh đi", "nhiet_lanh", W(), R(701, 2));
        H(250, "Đường đun khô hóa đen", "nhiet_phan:duong", R(580, 0.5f));
        A("Phenolphtalein + nước vôi hóa hồng", "quan_sat:chi_thi ~hồng", D(127), D(394, 0, 0.5f));

        lv = "2 THCS";
        A("Zn + HCl", "khi:H2:Zn !kt:", D(98, 2), R(19, 0.1f));
        A("Fe + HCl", "khi:H2:Fe !kt:", D(98, 2), R(18, 0.1f));
        A("Mg + HCl", "khi:H2:Mg !kt:", D(98, 2), R(16, 0.05f));
        A("Al + HCl", "khi:H2:Al !kt:", D(98, 2), R(17, 0.05f));
        A("Zn + H₂SO₄ loãng", "khi:H2:Zn", D(102, 1), R(19, 0.1f));
        A("Fe + H₂SO₄ loãng", "khi:H2:Fe", D(102, 1), R(18, 0.1f));
        A("Cu + HCl loãng: không phản ứng", "quan_sat:khong_pu !khi: !daykl", D(98, 1), R(20));
        A("Ag + HCl: không phản ứng", "quan_sat:khong_pu !khi:", D(98, 1), R(21));
        A("Fe + CuSO₄", "daykl:Fe(s):Cu2+ =Fe_+_Cu²⁺_→", D(253), R(18));
        A("Al + CuSO₄", "daykl:Al(s):Cu2+ =2Al_+_3Cu²⁺", D(253), R(17));
        A("Zn + CuSO₄", "daykl:Zn(s):Cu2+", D(253), R(19));
        A("Cu + AgNO₃", "daykl:Cu(s):Ag+ =Cu_+_2Ag⁺", D(230), R(20));
        A("Zn + FeSO₄", "daykl:Zn(s):Fe2+", D(251), R(19));
        A("Cu + FeSO₄: không phản ứng", "quan_sat:khong_pu !daykl", D(251), R(20));
        A("Na + nước", "khi:H2:Na =OH⁻", W(), R(13, 0.05f));
        A("K + nước", "khi:H2:K =OH⁻", W(), R(14, 0.05f));
        A("Ca + nước", "khi:H2:Ca =OH⁻", W(), R(29, 0.05f));
        A("Ba + nước", "khi:H2:Ba =OH⁻", W(), R(30, 0.1f));
        A("CaO + nước", "nhiet", W(), R(40, 1));
        A("Na₂O + nước", "nhiet", W(), R(37, 0.5f));
        A("P₂O₅ + nước", "nhiet", W(), R(66, 1));
        A("CO₂ tan vào nước", "quan_sat:khi_tan =CO₂", W(), K(645));
        A("NaOH + HCl: trung hòa", "quan_sat:axit_bazo =H⁺_+_OH⁻_→_H₂O", D(124), D(98));
        A("HCl + phenolphtalein: không màu", "quan_sat:chi_thi ~không_màu", D(98), D(394, 0, 0.5f));
        A("NaOH + phenolphtalein hóa hồng", "quan_sat:chi_thi ~hồng", D(124), D(394, 0, 0.5f));
        A("HCl + metyl da cam hóa đỏ", "quan_sat:chi_thi ~đỏ", D(98), D(395, 0, 0.5f));
        A("NaOH + metyl da cam: vàng", "quan_sat:chi_thi ~vàng", D(124), D(395, 0, 0.5f));
        A("BaCl₂ + H₂SO₄", "kt:BaSO4", D(155), D(102));
        A("BaCl₂ + Na₂SO₄", "kt:BaSO4", D(155), D(242));
        A("AgNO₃ + NaCl", "kt:AgCl", D(230), D(149));
        A("AgNO₃ + HCl", "kt:AgCl", D(230), D(98));
        A("CuSO₄ + NaOH", "kt:Cu(OH)2 ~xanh_lam", D(253), D(124));
        A("FeCl₃ + NaOH", "kt:Fe(OH)3 ~nâu_đỏ", D(159), D(124));
        A("FeCl₂ + NaOH", "kt:Fe(OH)2 ~trắng_xanh", D(158), D(124));
        A("MgCl₂ + NaOH", "kt:Mg(OH)2", D(154), D(124));
        A("AlCl₃ + NaOH vừa đủ", "kt:Al(OH)3", D(157), D(124, 0.1f, 5));
        A("Al(OH)₃ tan trong NaOH dư", "kt:Al(OH)3 tan:Al(OH)3", D(157), D(124, 0.1f, 3), D(124, 1, 5));
        A("ZnSO₄ + NaOH dư", "kt:Zn(OH)2 tan:Zn(OH)2", D(254), D(124, 0.1f, 3), D(124, 2, 5));
        A("Na₂CO₃ + HCl dư", "khi:CO2", D(275, 0.5f), D(98, 2));
        A("CaCO₃ + HCl", "khi:CO2", D(98, 2), R(279, 0.2f));
        A("Na₂SO₃ + HCl", "khi:SO2|mui:SO2", D(268, 1), D(98, 3));
        A("Na₂CO₃ + CaCl₂", "kt:CaCO3", D(275), D(153));
        A("Na₂CO₃ + BaCl₂", "kt:BaCO3", D(275), D(155));
        A("K₂CO₃ + nước vôi trong", "kt:CaCO3", D(127), D(276));
        A("CuO + HCl", "tan:CuO !kt:", D(98, 2), R(48, 0.1f));
        A("Fe₂O₃ + HCl", "tan:Fe2O3 !kt:", D(98, 2), R(46, 0.1f));
        A("MgO + HCl", "tan:MgO", D(98, 2), R(41, 0.1f));
        A("Cu(OH)₂ + H₂SO₄", "tan:Cu(OH)2", D(102, 1), R(134, 0.1f));
        A("Fe(OH)₃ + HCl", "tan:Fe(OH)3", D(98, 2), R(133, 0.1f));
        A("Mg(OH)₂ + HCl", "tan:Mg(OH)2", D(98, 2), R(128, 0.1f));
        A("Al + NaOH", "khi:H2:Al =[Al(OH)₄]⁻", D(124, 1), R(17, 0.05f));
        A("Zn + NaOH", "khi:H2:Zn", D(124, 2), R(19, 0.1f));
        A("Al₂O₃ + NaOH", "tan:Al2O3", D(124, 2), R(44, 0.1f));
        A("ZnO + NaOH", "tan:ZnO", D(124, 2), R(50, 0.1f));
        A("Cl₂ + NaOH", "pu:Cl2+Cl2 =ClO⁻ !khi:Cl2", D(124, 1), K(651));
        A("CO₂ + NaOH dư", "quan_sat:axit_bazo =CO₂", D(124, 1), K(645));
        A("CO₂ + nước vôi trong", "kt:CaCO3", D(127), K(645, 0.00005f));
        A("SO₂ + nước vôi trong", "kt:CaSO3", D(127), K(647, 0.00005f));
        A("C₂H₄ + nước brom", "pu:Br2+@khong_no =C₂H₄Br₂", D(413), K(426));
        A("C₂H₂ + nước brom", "pu:Br2+@khong_no =C₂H₂Br₄", D(413), K(430));
        A("CH₄ + nước brom: không phản ứng", "quan_sat:khong_pu !pu:", D(413), K(418));
        A("Etanol + Na", "na_ancol =C₂H₅ONa !khi:H2:Na", D(468), R(13, 0.05f));
        A("CH₃COOH + Na₂CO₃", "khi:CO2", D(505), D(275, 1));
        A("CH₃COOH + Zn", "khi:H2:Zn", D(505, 1, 5), R(19, 0.1f));
        A("CH₃COOH + NaOH", "quan_sat:axit_bazo =CH₃COOH", D(505), D(124, 1));
        A("CH₃COOH + CuO", "tan:CuO", D(505, 1, 5), R(48, 0.05f));
        H(85, "Este hóa CH₃COOH + C₂H₅OH (H₂SO₄ đặc)", "este_hoa =CH₃COOC₂H₅", D(505, 0, 2), D(468, 0, 2), D(102, 18, 1));
        H(85, "Chất béo + NaOH đun: xà phòng hóa", "xaphonghoa", D(124, 3), D(549, 0, 2));
        H(70, "Glucozơ + AgNO₃/NH₃ đun: tráng bạc", "trangbac", D(404), R(578));
        H(85, "Lòng trắng trứng đun: đông tụ", "kt:dongtu", D(594));
        A("Đường + H₂SO₄ đặc hóa đen", "pu:thanhoa", D(102, 18), R(580, 0.5f));
        A("FeS + HCl", "khi:H2S|mui:H2S", D(98, 2), R(356, 0.1f));
        A("NaCl + KNO₃: không phản ứng", "quan_sat:khong_pu", D(149), D(217));
        A("H₂O₂ + MnO₂", "khi:O2:H2O2", D(97, 0, 5), R(57));
        H(400, "Fe + S đun", "kho:Fe+S", R(18), R(8, 0.2f));
        H(550, "C + CuO đun", "kho:C+CuO", R(11, 0.05f), R(48, 0.5f));
        H(900, "Nung CaCO₃", "nhiet_phan:CaCO3", R(279));
        H(400, "H₂ + CuO đun", "kho:H2+CuO", R(48), K(653, 0.005f));
        H(700, "Đốt magie", "chay:Mg", R(16, 0.1f));
        H(400, "Đốt photpho đỏ", "chay:P", R(9, 0.1f));
        H(400, "Đốt lưu huỳnh", "chay:S", R(8, 0.1f));
        H(800, "Đốt than", "chay:C", R(11, 0.05f));
        H(600, "Sắt cháy trong oxi", "chay:Fe", R(18, 0.2f), K(652, 0.01f));
        H(300, "Nhiệt phân KMnO₄", "nhiet_phan:KMnO4", R(696));
        H(400, "Nhiệt phân KClO₃ (MnO₂)", "nhiet_phan:KClO3", R(178), R(57, 0.05f));
        H(200, "Nhiệt phân Cu(OH)₂", "nhiet_phan:Cu(OH)2", R(134));
        H(400, "Nhiệt phân Fe(OH)₃", "nhiet_phan:Fe(OH)3", R(133));
        H(200, "CuSO₄·5H₂O mất nước", "nhiet_phan:CuSO4.5H2O", R(383));
        H(200, "Nhiệt phân NaHCO₃", "nhiet_phan:NaHCO3", R(660));
        A("CuSO₄ khan + nước hóa xanh", "hoatan: ~xanh_lam !kt:", W(), R(681, 0.3f));

        lv = "3 THPT 10";
        A("Cl₂ + NaBr", "pu:Cl2+Br-|pu:ClO+Br-", D(183), K(651));
        A("Cl₂ + KI", "pu:Cl2+I-", D(197), K(651));
        A("Nước brom + KI", "pu:Br2+I-", D(197), D(413));
        A("Cl₂ + nước (nước clo)", "quan_sat:khi_tan =HClO", W(), K(651));
        A("AgNO₃ + NaBr", "kt:AgBr ~vàng_nhạt", D(230), D(183));
        A("AgNO₃ + NaI", "kt:AgI ~vàng", D(230), D(196));
        A("AgNO₃ + NaF: không kết tủa", "quan_sat:khong_pu !kt:", D(230), D(208));
        A("I₂ + hồ tinh bột", "phuc:I2tb", D(401), D(415, 0, 1));
        H(85, "Hồ tinh bột - iot mất màu khi đun", "phuc:I2tb tan:I2tb|~mất_màu", D(401), D(415, 0, 1));
        A("KMnO₄ + HCl đặc", "pu:MnO4-+Cl- =Cl₂", D(338), D(98, 12));
        H(85, "MnO₂ + HCl đặc đun", "pu:MnO2(s)+Cl- =Cl₂", D(98, 12), R(57, 0.1f));
        H(85, "K₂Cr₂O₇ + HCl đặc đun", "pu:CrVI+Cl- =Cl₂", D(342), D(98, 12));
        A("Nước Gia-ven + HCl", "pu:ClO+Cl-", D(679, 0, 2), D(98, 5));
        A("Nước Gia-ven tẩy màu chỉ thị", "taymau", D(395, 0, 1), D(679, 0, 3));
        H(85, "NaCl rắn + H₂SO₄ đặc đun: khí HCl", "khi:HCl", D(102, 18, 3), R(661, 0.5f));
        H(60, "KBr + H₂SO₄ đặc: Br₂, SO₂", "pu:SO4+Br- =Br₂", D(102, 18, 3), R(184, 0.3f));
        A("Fe₃O₄ + HCl", "tan:Fe3O4", D(98, 2), R(47, 0.1f));
        A("H₂O₂ + KI (H⁺)", "pu:H2O2+I-", D(197), D(102), D(97, 0, 1));
        A("KMnO₄ + H₂O₂ (H⁺)", "pu:MnO4-+H2O2", D(102), D(338, 0, 1), D(97, 0, 1));
        A("Hg + S (nhiệt độ thường)", "kho:Hg+S", R(31, 0.5f), R(8, 0.2f));
        H(450, "Zn + S đun", "kho:Zn+S", R(19), R(8, 0.2f));
        H(85, "S + H₂SO₄ đặc đun", "khi:SO2:S(s) =SO₂", D(102, 18), R(8, 0.05f));
        H(85, "S + HNO₃ đặc đun", "khi:NO2:S(s) =SO₄²⁻|=HSO₄⁻", D(104, 15), R(8, 0.05f));
        A("H₂S + Pb(NO₃)₂", "kt:PbS", D(229), K(649));
        A("H₂S + CuSO₄", "kt:CuS", D(253), K(649));
        A("H₂S + FeCl₃", "pu:Fe3++S", D(159), K(649));
        A("H₂S + SO₂ trong nước", "pu:SO3+S", W(), K(649), K(647, 0.0002f));
        A("Nước brom + H₂S", "pu:Br2+S", D(413), K(649));
        A("Na₂S + HCl", "khi:H2S|mui:H2S", D(353, 1), D(98, 3));
        A("SO₂ + nước brom", "pu:Br2+SO3", D(413), K(647));
        A("SO₂ + KMnO₄", "pu:MnO4-+SO3", D(709), K(647));
        A("SO₂ + NaOH dư", "quan_sat:axit_bazo =SO₂", D(124, 1), K(647));
        H(90, "Cu + H₂SO₄ đặc nóng", "khi:SO2:Cu =SO₂", D(102, 18), R(20, 0.1f));
        A("Fe + H₂SO₄ đặc nguội: thụ động", "thudong:Fe !khi:", D(102, 18), R(18));
        A("Al + H₂SO₄ đặc nguội: thụ động", "thudong:Al !khi:", D(102, 18), R(17));
        H(90, "C + H₂SO₄ đặc nóng", "khi:SO2:C(s) =CO₂", D(102, 18), R(11, 0.05f));
        A("Pha loãng: rót nước vào axit đặc (sai)", "nguyhiem:nuocvaoaxit", D(102, 18, 5), W(5));
        A("Pha loãng H₂SO₄ đặc vào nước tỏa nhiệt", "nhiet", W(10), D(102, 18, 2));
        A("Na₂S₂O₃ + HCl vẩn đục", "kt:S:thio", D(271), D(98));
        A("Na₂S₂O₃ + H₂SO₄ vẩn đục", "kt:S:thio", D(271), D(102));
        A("NaOH rắn tan tỏa nhiệt", "nhiet", W(), R(686, 1));
        A("NH₄Cl tan thu nhiệt", "nhiet_lanh", W(), R(693, 2));
        A("Al + I₂ (giọt nước)", "kho:I2+Al", R(17, 0.1f), R(7, 0.3f));

        lv = "4 THPT 11";
        A("FeCl₃ + KSCN đỏ máu", "phuc:FeSCN ~đỏ_máu", D(159), D(351));
        A("K₂Cr₂O₇ + NaOH: da cam → vàng", "quan_sat: ~vàng", D(342), D(124, 1));
        A("K₂CrO₄ + H₂SO₄: vàng → da cam", "quan_sat: ~da_cam", D(341), D(102, 1));
        A("Quỳ: NH₃ + phenolphtalein hồng", "quan_sat:chi_thi ~hồng", D(143), D(394, 0, 0.5f));
        A("NH₃ + HCl đặc: khói trắng", "khoi:NH4Cl", D(98, 12), K(648));
        A("NH₃ + CuSO₄ tạo kết tủa", "kt:Cu(OH)2", D(253), D(143, 0.1f, 3));
        A("NH₃ dư + CuSO₄ xanh thẫm", "phuc:CuNH3 ~xanh lam_thẫm|~xanh_thẫm", D(253), D(720, 0, 5));
        A("NH₃ dư + AlCl₃: kết tủa không tan", "kt:Al(OH)3 !tan:Al(OH)3", D(157), D(720, 0, 5));
        A("NH₃ + FeCl₃", "kt:Fe(OH)3", D(159), D(143));
        A("NH₃ dư + ZnSO₄", "kt:Zn(OH)2|phuc:ZnNH3 phuc:ZnNH3", D(254), D(143, 0.1f, 2), D(720, 0, 5));
        A("NH₃ dư + AgNO₃", "phuc:AgNH3", D(230), D(143, 0.1f, 3), D(720, 0, 5));
        H(85, "NH₄Cl + NaOH đun", "khi:NH3", D(152), D(124, 1));
        H(400, "Nhiệt phân NH₄Cl", "nhiet_phan:NH4Cl", R(693));
        H(300, "Nhiệt phân NH₄NO₃", "nhiet_phan:NH4NO3", R(701));
        H(85, "NH₄Cl + NaNO₂ đun: N₂", "khi:N2:nitrit", D(152, 1), D(240, 1));
        A("Cu + HNO₃ đặc", "khi:NO2:Cu =Cu_+_2NO₃⁻_+_4H⁺", D(104, 15), R(20, 0.1f));
        A("Cu + HNO₃ loãng", "khi:NO:Cu =3Cu_+_2NO₃⁻_+_8H⁺", D(104, 2), R(20, 0.1f));
        A("Ag + HNO₃ loãng", "khi:NO:Ag =3Ag", D(104, 2), R(21, 0.1f));
        A("Fe + HNO₃ loãng", "khi:NO:Fe =Fe³⁺ !khi:H2", D(104, 2), R(18, 0.05f));
        A("Zn + HNO₃ loãng", "khi:NO:Zn|khi:N", D(104, 2), R(19, 0.1f));
        A("Mg + HNO₃ loãng", "khi:NO:Mg|khi:N", D(104, 2), R(16, 0.05f));
        A("Fe + HNO₃ đặc nguội: thụ động", "thudong:Fe !khi:", D(104, 15), R(18));
        A("Al + HNO₃ đặc nguội: thụ động", "thudong:Al !khi:", D(104, 15), R(17));
        H(90, "C + HNO₃ đặc nóng", "khi:NO2:C(s) =CO₂", D(104, 15), R(11, 0.05f));
        H(90, "P + HNO₃ đặc nóng", "khi:NO2:P(s) =H₃PO₄|=H₂PO₄⁻", D(104, 15), R(9, 0.05f));
        A("Cu + NaNO₃ + HCl", "khi:NO:Cu", D(98, 2), D(216, 1), R(20, 0.1f));
        A("Fe(NO₃)₂ + HCl: khí NO", "pu:NO3-+Fe2+", D(225), D(98, 2));
        H(300, "Nhiệt phân AgNO₃", "nhiet_phan:AgNO3", R(698));
        H(500, "Nhiệt phân Pb(NO₃)₂", "nhiet_phan:Pb(NO3)2", R(699));
        A("H₃PO₄ + NaOH", "quan_sat:axit_bazo =H₃PO₄", D(106), D(124, 0.3f));
        A("Na₃PO₄ + AgNO₃", "kt:Ag3PO4 ~vàng", D(295), D(230));
        A("CO₂ dư hòa tan CaCO₃", "kt:CaCO3 tan:CaCO3", D(127), K(645, 0.00005f), K(645, 0.01f));
        A("Ca(HCO₃)₂ + NaOH", "kt:CaCO3", D(292), D(124));
        A("Ca(HCO₃)₂ + nước vôi", "kt:CaCO3", D(292), D(127));
        H(95, "Đun nước cứng tạm thời", "kt:CaCO3", D(292));
        A("NaHCO₃ + HCl", "khi:CO2", D(289, 1), D(98, 2));
        A("NaHCO₃ + NaOH", "quan_sat:axit_bazo =HCO₃⁻", D(289), D(124));
        A("Na₂CO₃ + HCl thiếu: chưa có khí", "quan_sat:axit_bazo !khi:CO2", D(275, 0.5f), D(98, 0.1f, 2));
        H(400, "CO + CuO đun", "kho:CO+CuO", R(48), K(646, 0.005f));
        H(600, "CO + Fe₂O₃ đun", "kho:CO+Fe2O3", R(46), K(646, 0.01f));
        A("Na₂SiO₃ + HCl", "kt:H2SiO3", D(366), D(98, 1));
        A("Na₂SiO₃ + CO₂", "kt:H2SiO3", D(366), K(645, 0.002f));
        A("SiO₂ + HF", "tan:SiO2 =SiF₄|=H₂SiF₆", D(101, 10), R(65, 0.05f));
        H(85, "Si + NaOH đặc", "khi:H2:Si", D(124, 2), R(33, 0.05f));
        A("Etan + nước brom: không phản ứng", "quan_sat:khong_pu", D(413), K(419));
        A("Hexan + KMnO₄: không phản ứng", "lop: !pu:", D(709), D(423, 0, 1));
        A("Etilen + KMnO₄", "pu:MnO4-+@khong_no =C₂H₄", D(709), K(426));
        A("Axetilen + AgNO₃/NH₃", "kt:Ag2C2", D(404), K(430));
        A("Propin + AgNO₃/NH₃", "kt:Ag2C2", D(404), K(431));
        A("Benzen + nước brom: chỉ chiết", "lop: !pu:Br2", D(413), D(434, 0, 2));
        H(60, "Benzen + Br₂ lỏng (Fe)", "brom_benzen =C₆H₅Br", D(434, 0, 2), D(6, 0, 1), R(18, 0.1f));
        H(55, "Nitro hóa benzen", "nitro_hoa =C₆H₅NO₂", D(104, 15, 2), D(102, 18, 2), D(434, 0, 1));
        H(85, "Toluen + KMnO₄ đun", "pu:toluen", D(709), D(435, 0, 1));
        A("Stiren + nước brom", "pu:Br2+@khong_no =C₆H₅", D(413), D(440, 0, 0.5f));
        A("Glixerol + Cu(OH)₂", "phuc:CuPol", D(253), D(124, 1), D(478, 0, 1));
        A("Etanol + Cu(OH)₂: không tan", "kt:Cu(OH)2 !phuc:CuPol", D(253), D(124, 1), D(468, 0, 1));
        H(80, "Etanol + CuO đun", "cuo_ancol =CH₃CHO", D(468, 0, 2), R(48, 0.2f));
        A("K₂Cr₂O₇ + etanol + H₂SO₄", "pu:CrVI+@ancol", D(342), D(102, 2), D(468, 0, 1));
        A("Phenol + nước brom", "kt:TBP|~kết_tủa_trắng", D(413), R(480, 0.05f));
        A("Phenol + NaOH: tan", "quan_sat:axit_bazo|tan:PhenolKT", D(124, 1), R(480, 0.3f));
        A("Phenol + Na", "khi:H2:Na|na_ancol", D(434, 0, 3), R(480, 0.5f), R(13, 0.05f));
        A("CO₂ + natri phenolat: vẩn đục", "kt:PhenolKT", D(124, 2), R(480, 0.6f), K(645, 0.01f));
        A("Phenol + FeCl₃", "phuc:FePhenol", D(159), R(480, 0.1f));
        H(70, "HCHO + AgNO₃/NH₃ đun", "trangbac", D(404), D(490, 0.5f, 1));
        H(70, "CH₃CHO + AgNO₃/NH₃ đun", "trangbac", D(404), D(491, 0, 0.2f));
        H(85, "CH₃CHO + Cu(OH)₂/NaOH đun", "kt:Cu2O", D(253), D(124, 2), D(491, 0, 0.2f));
        A("CH₃CHO + nước brom", "pu:Br2+@andehit =CH₃COOH", D(413), D(491, 0, 0.2f));
        H(70, "HCOOH + AgNO₃/NH₃ đun", "trangbac", D(404), D(504, 0, 0.01f));
        H(70, "Axeton + AgNO₃/NH₃: không", "!trangbac", D(404), D(498, 0, 1));
        A("Axit axetic + quỳ (metyl da cam)", "quan_sat:chi_thi", D(505), D(395, 0, 0.5f));

        lv = "5 THPT 12";
        H(85, "Etyl axetat + H₂SO₄ loãng đun: thủy phân", "thuyphan:este =CH₃COOH", D(102, 1), D(534, 0, 2));
        H(85, "Etyl axetat + NaOH đun", "xaphonghoa", D(124, 3), D(534, 0, 2));
        A("Glucozơ + Cu(OH)₂ (thường): xanh lam", "phuc:CuPol", D(253), D(124, 1), R(578));
        H(85, "Glucozơ + Cu(OH)₂ đun: đỏ gạch", "kt:Cu2O", D(253), D(124, 1), R(578));
        A("Glucozơ + nước brom", "pu:Br2+@andehit =C₆H₁₂O₇", D(413), R(578));
        H(70, "Fructozơ + AgNO₃/NH₃ đun", "trangbac", D(404), R(579));
        A("Fructozơ + nước brom: không", "!pu:Br2+@andehit", D(413), R(579));
        A("Saccarozơ + Cu(OH)₂", "phuc:CuPol", D(253), D(124, 1), R(580));
        H(70, "Saccarozơ + AgNO₃/NH₃: không tráng bạc", "!trangbac", D(404), R(580));
        H(90, "Saccarozơ + H₂SO₄ loãng đun: thủy phân", "thuyphan:duong", D(102, 1), R(580));
        H(90, "Tinh bột + H₂SO₄ loãng đun: thủy phân", "thuyphan:duong", D(102, 1), R(583));
        A("Tinh bột + iot", "phuc:I2tb", W(), R(583, 0.05f), D(415, 0, 1));
        A("Metylamin + HCl", "quan_sat:axit_bazo =CH₃NH₃⁺", D(552, 1), D(98, 1));
        A("Metylamin + FeCl₃", "kt:Fe(OH)3", D(159), D(552, 1));
        A("Anilin + nước brom", "kt:TBA|~kết_tủa_trắng", D(413), D(557, 0, 0.2f));
        A("Anilin + HCl: tan", "tan:AnilinKT|quan_sat:axit_bazo", D(557, 0, 1), D(98, 2));
        A("Glyxin + HCl", "quan_sat:axit_bazo", R(526), D(98, 1));
        H(85, "Glyxin + ninhiđrin đun", "mau:ninhydrin", R(526), D(406));
        A("Lòng trắng trứng + biure", "mau:biure", D(594), D(405));
        A("Lòng trắng trứng + HNO₃ đặc", "mau:xantoprotein", D(594), D(104, 15));
        A("Lòng trắng trứng + Cu(OH)₂", "mau:biure", D(594), D(253), D(124, 2));
        A("Polietilen + HCl: không phản ứng", "quan_sat:khong_pu", D(98), R(599));
        A("Fe + FeCl₃", "daykl:Fe(s):Fe3+ =Fe_+_2Fe³⁺", D(719), R(18, 0.05f));
        A("Cu + FeCl₃", "daykl:Cu(s):Fe3+ =Cu_+_2Fe³⁺", D(719), R(20, 0.05f));
        H(90, "Mg + nước nóng", "khi:H2:Mg", W(), R(16, 0.05f));
        A("Na₂CO₃ + MgCl₂ (làm mềm nước)", "kt:MgCO3", D(154), D(275));
        A("Na₃PO₄ + CaCl₂ (làm mềm nước)", "kt:Ca3(PO4)2", D(153), D(295));
        H(600, "Nhiệt nhôm Al + Fe₂O₃", "kho:nhietnhom", R(17, 0.1f), R(46, 0.3f));
        A("Al + CuCl₂", "daykl:Al(s):Cu2+", D(161), R(17, 0.05f));
        H(300, "Fe + Cl₂", "kho:Fe+Cl2", R(18, 0.1f), K(651, 0.005f));
        H(300, "Cu + Cl₂", "kho:Cu+Cl2", R(20, 0.1f), K(651, 0.005f));
        H(200, "Na + Cl₂", "kho:Na+Cl2", R(13, 0.05f), K(651, 0.005f));
        A("Cl₂ + FeCl₂", "pu:Cl2+Fe2+", D(158), K(651));
        A("H₂O₂ + FeSO₄ (H⁺)", "pu:H2O2+Fe2+", D(251), D(102), D(97, 0, 1));
        A("FeCl₂ để ngoài không khí (NaOH)", "oxkk:Fe", D(158), D(124));
        A("CrCl₃ + NaOH", "kt:Cr(OH)3", D(172), D(124, 1, 1));
        A("Cr(OH)₃ tan trong NaOH dư", "kt:Cr(OH)3 tan:Cr(OH)3", D(172), D(124, 1, 1), D(124, 2, 5));
        A("K₂Cr₂O₇ + FeSO₄ + H₂SO₄", "pu:CrVI+Fe2+", D(251), D(102), D(342, 0, 0.5f));
        A("KMnO₄ + FeSO₄ + H₂SO₄", "pu:MnO4-+Fe2+", D(251), D(102), D(338, 0, 0.5f));
        H(300, "Núi lửa amoni đicromat", "nhiet_phan:(NH4)2Cr2O7", R(345));
        A("NiCl₂ + NaOH", "kt:Ni(OH)2 ~xanh_lục", D(169), D(124));
        A("CoCl₂ + NaOH", "kt:Co(OH)2", D(170), D(124));
        A("MnSO₄ + NaOH (hóa nâu)", "kt:Mn(OH)2", D(261), D(124));
        A("Pb(NO₃)₂ + KI: vàng tươi", "kt:PbI2 ~vàng_tươi", D(229), D(197));
        A("Ba²⁺ + CrO₄²⁻", "kt:BaCrO4", D(155), D(341));
        A("FeSO₄ + K₃[Fe(CN)₆]", "kt:Fe3[Fe(CN)6]2", D(251), D(381));
        A("FeCl₃ + K₄[Fe(CN)₆]", "kt:Fe4[Fe(CN)6]3", D(159), D(380));
        A("Nhận biết NO₃⁻: Cu + H₂SO₄", "khi:NO:Cu", D(216, 1), D(102, 2), R(20, 0.1f));
        A("Nhận biết NH₄⁺ bằng Nessler", "kt:Nessler", D(412), D(152));
        A("Phèn chua tan, nước hơi chua", "hoatan:|quan_sat:", W(), R(386, 0.3f));

        lv = "6 Nâng cao";
        A("Hg(NO₃)₂ + KI: đỏ HgI₂", "kt:HgI2", D(231), D(197, 0.1f, 5));
        A("HgI₂ tan trong KI dư", "kt:HgI2 tan:HgI2", D(231), D(197, 0.1f, 9), D(197, 1, 5));
        A("HgCl₂ + SnCl₂: trắng Hg₂Cl₂", "kt:Hg2Cl2|pu:Hg2++Sn2+", D(165), D(167, 0.1f, 2));
        A("HgCl₂ + Cu: Hg bám", "daykl:Cu(s):Hg2+", D(165), R(20));
        A("AgCl + NH₃ tan", "tan:AgCl phuc:AgNH3", D(720), R(164, 0.05f));
        A("AgBr + Na₂S₂O₃ tan", "tan:AgBr phuc:AgS2O3", D(271, 1), R(194, 0.05f));
        A("AgNO₃ + KCN dư: phức tan", "kt:AgCN tan:AgCN !khi:HCN", D(230), D(349, 0.05f, 5), D(349, 1, 5));
        A("CuSO₄ + KI", "pu:Cu2++I- =2Cu²⁺", D(253), D(197));
        A("CuSO₄ + HCl đặc: vàng lục", "phuc:CuCl4", D(253), D(98, 12));
        A("CoCl₂ + HCl đặc: hồng → xanh", "phuc:CoCl4", D(170), D(98, 12));
        A("CoCl₂ + KSCN đặc: xanh", "phuc:CoSCN", D(170), D(351, 3));
        A("CoCl₂ + NH₃ dư", "phuc:CoNH3", D(170), D(720, 0, 5));
        A("NiSO₄ + NH₃ dư", "phuc:NiNH3", D(259), D(720, 0, 5));
        A("CrCl₃ + NaOH dư + H₂O₂: vàng", "pu:H2O2+Cr3+ ~vàng", D(172), D(124, 2), D(97, 0, 1));
        A("K₂Cr₂O₇ + H₂O₂ + H⁺: CrO₅ xanh", "phuc:CrO5", D(342), D(102), D(97, 0, 1));
        A("FeCl₃ + NaF: mất màu", "phuc:FeF6 !kt:", D(159), D(208, 1));
        A("FeCl₃ + H₃PO₄: mất màu", "phuc:FePO4c !kt:", D(159), D(106, 1));
        A("FeCl₃ + Na₂CO₃: thủy phân kép", "kt:Fe(OH)3 khi:CO2", D(159, 0.5f), D(275, 1));
        A("AlCl₃ + Na₂CO₃: thủy phân kép", "kt:Al(OH)3 khi:CO2", D(157, 0.5f), D(275, 1));
        A("AlCl₃ + Na₂S: thủy phân kép", "kt:Al(OH)3 khi:H2S|mui:H2S", D(157, 0.5f), D(353, 1));
        H(85, "Urê + NaOH đun: NH₃", "khi:NH3", D(124, 2), R(598));
        A("MnSO₄ + KMnO₄: MnO₂", "pu:MnO4-+Mn2+|kt:MnO2", D(261), D(338));
        H(85, "Mn²⁺ + S₂O₈²⁻ (Ag⁺) đun: tím", "pu:S2O82-+Mn2+", D(261, 0.01f), R(274), D(230, 0, 0.5f));
        A("Na₂S₂O₃ + I₂", "pu:I2+S2O32-", D(415), D(271));
        A("KI + H₂O₂ + hồ tinh bột", "phuc:I2tb", D(197), D(102), D(401), D(97, 0, 1));
        A("Iođofom: axeton + I₂ + NaOH", "kt:CHI3", D(498, 0, 0.5f), D(415), D(124, 1));
        H(60, "Iođofom: etanol + I₂ + NaOH", "kt:CHI3", D(468, 0, 0.5f), D(415), D(124, 1));
        A("Schiff + HCHO", "mau:schiff", D(411), D(490, 0.5f, 1));
        H(85, "Benedict + glucozơ đun", "kt:Cu2O", D(408), R(578));
        H(85, "Fehling + glucozơ đun", "kt:Cu2O", D(402), D(403), R(578));
        H(85, "Fehling + saccarozơ: không ra Cu₂O", "!kt:Cu2O", D(402), D(403), R(580));
        A("Molisch + glucozơ + H₂SO₄ đặc", "mau:molisch", D(410), R(578), D(102, 18));
        H(85, "Millon + protein đun", "kt:millon", D(409), D(594));
        A("Điphenylamin + nitrat", "mau:dpa", D(416), D(216));
        A("Na₂O₂ + nước", "nuoc:Na2O2", W(), R(369));
        A("CaC₂ + nước", "nuoc:CaC2", W(), R(372));
        A("Al₄C₃ + nước", "nuoc:Al4C3", W(), R(374));
        A("Mg₃N₂ + nước", "nuoc:Mg3N2", W(), R(373));
        A("NaH + nước", "nuoc:NaH", W(), R(377));
        H(60, "Au + nước cường toan", "pu:NO3-+Au(s)|khi:NO:Au(s)", D(98, 12), D(104, 15, 2), R(22, 0.02f));
        H(90, "S + NaOH đặc đun", "pu:S(s)+S(s)", D(124, 5), R(8, 0.05f));
        A("BaCrO₄ + HCl: tan", "tan:BaCrO4", D(98, 2), R(347, 0.05f));
        H(95, "PbCl₂ (dung dịch bão hòa có cặn) tan khi đun", "tan:PbCl2", D(163));
        A("Pb(OH)₂ + NaOH dư: tan", "tan:Pb(OH)2", D(124, 2), R(136, 0.05f));
        A("CoCl₂ khan + nước: xanh → hồng", "hoatan: ~hồng", W(), R(682, 0.1f));
        A("Pb + H₂SO₄ loãng: gần như không tan", "!khi:H2", D(102, 1), R(23));
        A("Cl₂ + NaNO₂: oxi hóa nitrit", "pu:Cl2+NO2", D(240), K(651));
        A("KMnO₄ + SnCl₂ (H⁺)", "pu:MnO4-+Sn2+", D(167), D(98, 1), D(338, 0, 0.5f));
        A("Nước brom + SnCl₂", "pu:Br2+Sn2+", D(167), D(413));
        A("K₂Cr₂O₇ + SnCl₂ (H⁺)", "pu:CrVI+Sn2+", D(167), D(98, 1), D(342, 0, 0.5f));
        A("H₂O₂ + Na₂SO₃", "pu:H2O2+SO3", D(268), D(97, 0, 1));
        A("HIO₃ + KI", "pu:IO3-+I-", D(197), D(116));
    }

}
