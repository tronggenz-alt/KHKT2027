using System;
using System.Collections.Generic;
using System.Text;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Phương trình hóa học: đọc công thức (cả dạng "Fe2(SO4)3", "SO42-" lẫn "Fe₂(SO₄)₃", "SO₄²⁻"), tự tìm hệ số
    /// cân bằng nguyên tố và điện tích, kiểm tra một phương trình đã viết có cân bằng không.
    /// Mọi phương trình tự sinh (đẩy kim loại, oxi hóa - khử theo bảng thế, axit - bazơ) đi qua đây nên luôn cân bằng.
    /// </summary>
    public static class Equation
    {
        const string SubD = "₀₁₂₃₄₅₆₇₈₉", SupD = "⁰¹²³⁴⁵⁶⁷⁸⁹";

        /// <summary>Đếm nguyên tố và điện tích của một chất. null nếu không đọc được (gốc R, chữ thường...).</summary>
        public static Dictionary<string, double> Parse(string formula, out int charge)
        {
            charge = 0;
            if (string.IsNullOrWhiteSpace(formula)) return null;
            var sb = new StringBuilder();
            foreach (char ch in formula.Trim())
            {
                int k = SubD.IndexOf(ch);
                if (k >= 0) { sb.Append((char)('0' + k)); continue; }
                k = SupD.IndexOf(ch);
                if (k >= 0) { sb.Append('^').Append((char)('0' + k)); continue; }
                if (ch == '⁺') { sb.Append("^+"); continue; }
                if (ch == '⁻') { sb.Append("^-"); continue; }
                if (ch == '↓' || ch == '↑' || ch == ' ') continue;
                if (ch == '·') { sb.Append('.'); continue; }
                sb.Append(ch);
            }
            // điện tích: dạng Unicode "SO₄²⁻" (đã đổi thành "SO4^2-") hoặc ASCII có dấu mũ "SO4^2-"; "NH4+", "OH-" là điện tích 1
            string f = sb.ToString();
            if (f.EndsWith("+") || f.EndsWith("-"))
            {
                int sign = f.EndsWith("+") ? 1 : -1;
                f = f.Substring(0, f.Length - 1);
                int caret = f.LastIndexOf('^');
                int mag = 1;
                if (caret >= 0)
                {
                    string digits = f.Substring(caret + 1).Replace("^", "");
                    if (digits.Length > 0 && !int.TryParse(digits, out mag)) return null;
                    if (digits.Length == 0) mag = 1;
                    f = f.Substring(0, caret);
                    // các chữ số trên khác (²⁻ ghi thành ^2^-) đã gộp ở trên
                    while (f.EndsWith("^")) f = f.Substring(0, f.Length - 1);
                    int c2 = f.IndexOf('^');
                    if (c2 >= 0)
                    {
                        string rest = f.Substring(c2).Replace("^", "");
                        f = f.Substring(0, c2);
                        if (!int.TryParse(rest + (mag == 1 && digits.Length == 0 ? "" : digits), out mag)) return null;
                    }
                }
                charge = sign * mag;
            }
            if (f.Contains("^")) return null;
            // hệ số nước kết tinh: "CuSO4.5H2O"
            var total = new Dictionary<string, double>();
            foreach (var part in f.Split('.'))
            {
                int p = 0, mult = 0;
                while (p < part.Length && char.IsDigit(part[p])) { mult = mult * 10 + (part[p] - '0'); p++; }
                var c = Group(part.Substring(p));
                if (c == null) return null;
                foreach (var kv in c) total[kv.Key] = (total.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value * (mult == 0 ? 1 : mult);
            }
            return total.Count > 0 ? total : null;
        }

        static Dictionary<string, double> Group(string s)
        {
            var stack = new Stack<Dictionary<string, double>>();
            stack.Push(new Dictionary<string, double>());
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '(' || c == '[') { stack.Push(new Dictionary<string, double>()); i++; }
                else if (c == ')' || c == ']')
                {
                    i++;
                    int k = 0, st = i;
                    while (i < s.Length && char.IsDigit(s[i])) { k = k * 10 + (s[i] - '0'); i++; }
                    if (i == st) k = 1;
                    if (stack.Count < 2) return null;
                    var top = stack.Pop();
                    foreach (var kv in top) Add(stack.Peek(), kv.Key, kv.Value * k);
                }
                else if (char.IsUpper(c))
                {
                    string el = c.ToString();
                    i++;
                    if (i < s.Length && char.IsLower(s[i])) { el += s[i]; i++; }
                    int k = 0, st = i;
                    while (i < s.Length && char.IsDigit(s[i])) { k = k * 10 + (s[i] - '0'); i++; }
                    if (i == st) k = 1;
                    Add(stack.Peek(), el, k);
                }
                else if (c == '-' || c == '=' || c == '≡') i++;   // liên kết trong công thức cấu tạo thu gọn
                else return null;
            }
            return stack.Count == 1 ? stack.Pop() : null;
        }

        static void Add(Dictionary<string, double> d, string k, double v) => d[k] = (d.TryGetValue(k, out var o) ? o : 0) + v;

        static readonly string[] Arrows = { "→", "⇌", "⟶" };

        /// <summary>
        /// Phương trình đã viết có cân bằng không. Trả về null nếu cân bằng (hoặc không kiểm được: gốc R, "[O]", nhiều
        /// phương trình nối bằng ";"), ngược lại là mô tả chỗ lệch.
        /// </summary>
        public static string Check(string eq)
        {
            if (string.IsNullOrEmpty(eq)) return null;
            string e = StripConditions(eq);
            if (e.Contains(";") || e.Contains("...") || e.Contains("[O]") || e.Contains("R'") || HasGenericR(e)) return null;
            int pos = -1, len = 0;
            foreach (var a in Arrows) { int p = e.IndexOf(a, StringComparison.Ordinal); if (p >= 0) { pos = p; len = a.Length; break; } }
            if (pos < 0) return null;
            var sides = new[] { e.Substring(0, pos), e.Substring(pos + len) };
            var tot = new[] { new Dictionary<string, double>(), new Dictionary<string, double>() };
            var q = new double[2];
            for (int k = 0; k < 2; k++)
            {
                foreach (var termRaw in sides[k].Split(new[] { " + " }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string term = termRaw.Trim();
                    int i = 0;
                    while (i < term.Length && (char.IsDigit(term[i]) || term[i] == '.' || term[i] == ',')) i++;
                    double coef = i > 0 && double.TryParse(term.Substring(0, i).Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cf) ? cf : 1;
                    var cnt = Parse(term.Substring(i), out int z);
                    if (cnt == null) return null;
                    foreach (var kv in cnt) Add(tot[k], kv.Key, coef * kv.Value);
                    q[k] += coef * z;
                }
            }
            var bad = new List<string>();
            var all = new HashSet<string>(tot[0].Keys);
            all.UnionWith(tot[1].Keys);
            foreach (var el in all)
            {
                tot[0].TryGetValue(el, out var l);
                tot[1].TryGetValue(el, out var r);
                if (Math.Abs(l - r) > 1e-6) bad.Add($"{el} {l:0.##}/{r:0.##}");
            }
            if (Math.Abs(q[0] - q[1]) > 1e-6) bad.Add($"điện tích {q[0]:0.##}/{q[1]:0.##}");
            return bad.Count == 0 ? null : string.Join(", ", bad);
        }

        static bool HasGenericR(string e)
        {
            for (int i = 0; i < e.Length; i++)
                if (e[i] == 'R' && (i + 1 >= e.Length || !char.IsLower(e[i + 1])) && (i == 0 || !char.IsLetter(e[i - 1]))) return true;
            return false;
        }

        /// <summary>Bỏ ghi chú điều kiện trong ngoặc: "(đặc)", "(loãng)", "(t°)", "(xúc tác MnO₂, t°)".</summary>
        static string StripConditions(string e)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < e.Length)
            {
                if (e[i] == '(')
                {
                    int j = e.IndexOf(')', i);
                    if (j > i)
                    {
                        string inner = e.Substring(i + 1, j - i - 1);
                        bool note = false;
                        foreach (char ch in inner) if (ch > 127 && SubD.IndexOf(ch) < 0 && SupD.IndexOf(ch) < 0 && ch != '⁺' && ch != '⁻') { note = true; break; }
                        if (note || inner.Contains(" ")) { i = j + 1; continue; }
                    }
                }
                sb.Append(e[i]);
                i++;
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // Tìm hệ số
        // ------------------------------------------------------------------

        /// <summary>
        /// Cân bằng lhs → rhs (công thức đã có hoặc chưa có chỉ số dưới Unicode). Trả về chuỗi "2Al + 3Cu²⁺ → 2Al³⁺ + 3Cu"
        /// hoặc null nếu không cân bằng được với đúng các chất đã cho (thiếu H₂O, H⁺...).
        /// </summary>
        public static string Balance(IList<string> lhs, IList<string> rhs, string arrow = "→")
        {
            var coef = Coefficients(lhs, rhs);
            if (coef == null) return null;
            var sb = new StringBuilder();
            for (int i = 0; i < lhs.Count; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(coef[i] == 1 ? "" : coef[i].ToString()).Append(Pretty(lhs[i]));
            }
            sb.Append(' ').Append(arrow).Append(' ');
            for (int i = 0; i < rhs.Count; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(coef[lhs.Count + i] == 1 ? "" : coef[lhs.Count + i].ToString()).Append(Pretty(rhs[i]));
            }
            return sb.ToString();
        }

        /// <summary>Thử cân bằng; nếu không được thì thêm H₂O, H⁺, OH⁻ vào bên cần (axit / bazơ theo môi trường).</summary>
        public static string BalanceAuto(IList<string> lhs, IList<string> rhs, bool acidic, string arrow = "→")
        {
            var r = Balance(lhs, rhs, arrow);
            if (r != null) return r;
            string ion = acidic ? "H+" : "OH-";
            var tries = new List<(string[] l, string[] r)>();
            // vị trí của H₂O và của H⁺/OH⁻: 0 = không thêm, 1 = vế trái, 2 = vế phải
            for (int wpos = 0; wpos < 3; wpos++)
                for (int ipos = 0; ipos < 3; ipos++)
                {
                    if (wpos == 0 && ipos == 0) continue;
                    var l = new List<string>(); var rr = new List<string>();
                    if (wpos == 1) l.Add("H2O"); else if (wpos == 2) rr.Add("H2O");
                    if (ipos == 1) l.Add(ion); else if (ipos == 2) rr.Add(ion);
                    tries.Add((l.ToArray(), rr.ToArray()));
                }
            foreach (var (l, rr) in tries)
            {
                var L = new List<string>(lhs); L.AddRange(l);
                var R = new List<string>(rhs); R.AddRange(rr);
                r = Balance(L, R, arrow);
                if (r != null) return r;
            }
            return null;
        }

        static int[] Coefficients(IList<string> lhs, IList<string> rhs)
        {
            int n = lhs.Count + rhs.Count;
            var comps = new List<(Dictionary<string, double> c, int z)>();
            foreach (var f in lhs) { var c = Parse(Pretty(f), out int z); if (c == null) return null; comps.Add((c, z)); }
            foreach (var f in rhs) { var c = Parse(Pretty(f), out int z); if (c == null) return null; comps.Add((c, z)); }
            var elems = new List<string>();
            foreach (var (c, _) in comps) foreach (var k in c.Keys) if (!elems.Contains(k)) elems.Add(k);
            int rows = elems.Count + 1;
            var A = new double[rows, n];
            for (int j = 0; j < n; j++)
            {
                double s = j < lhs.Count ? 1 : -1;
                for (int i = 0; i < elems.Count; i++) A[i, j] = s * (comps[j].c.TryGetValue(elems[i], out var v) ? v : 0);
                A[elems.Count, j] = s * comps[j].z;
            }
            // nghiệm không âm nhỏ nhất: cố định hệ số cuối, giải hệ còn lại; thử nhân lên đến khi được số nguyên
            var x = NullVector(A, rows, n);
            if (x == null) return null;
            double min = double.MaxValue;
            foreach (var v in x) { if (v <= 1e-9) return null; min = Math.Min(min, v); }
            for (int mult = 1; mult <= 60; mult++)
            {
                var res = new int[n];
                bool ok = true;
                for (int j = 0; j < n; j++)
                {
                    double v = x[j] / min * mult;
                    int r = (int)Math.Round(v);
                    if (Math.Abs(v - r) > 1e-6 || r <= 0) { ok = false; break; }
                    res[j] = r;
                }
                if (ok) return res;
            }
            return null;
        }

        /// <summary>Một vectơ trong không gian nghiệm của A·x = 0 khi không gian đó có số chiều đúng bằng 1.</summary>
        static double[] NullVector(double[,] A, int rows, int n)
        {
            var M = (double[,])A.Clone();
            var pivotCol = new int[rows];
            int r = 0;
            var isPivot = new bool[n];
            for (int c = 0; c < n && r < rows; c++)
            {
                int best = -1; double bv = 1e-10;
                for (int i = r; i < rows; i++) if (Math.Abs(M[i, c]) > bv) { bv = Math.Abs(M[i, c]); best = i; }
                if (best < 0) continue;
                for (int k = 0; k < n; k++) { var t = M[r, k]; M[r, k] = M[best, k]; M[best, k] = t; }
                double p = M[r, c];
                for (int k = 0; k < n; k++) M[r, k] /= p;
                for (int i = 0; i < rows; i++)
                {
                    if (i == r || Math.Abs(M[i, c]) < 1e-14) continue;
                    double f = M[i, c];
                    for (int k = 0; k < n; k++) M[i, k] -= f * M[r, k];
                }
                pivotCol[r] = c;
                isPivot[c] = true;
                r++;
            }
            int free = n - r;
            if (free != 1) return null;
            int fc = Array.FindIndex(isPivot, v => !v);
            var x = new double[n];
            x[fc] = 1;
            for (int i = 0; i < r; i++) x[pivotCol[i]] = -M[i, fc];
            return x;
        }

        /// <summary>"SO42-" → "SO₄²⁻", "Fe2(SO4)3" → "Fe₂(SO₄)₃"; chuỗi đã đẹp thì giữ nguyên.</summary>
        public static string Pretty(string f)
        {
            if (string.IsNullOrEmpty(f)) return f;
            foreach (char ch in f) if (SubD.IndexOf(ch) >= 0 || SupD.IndexOf(ch) >= 0 || ch == '⁺' || ch == '⁻') return f;
            int charge = 0;
            if (f.EndsWith("+") || f.EndsWith("-")) Parse(f, out charge);
            return SpeciesRegistry.Pretty(f.Replace("^", ""), charge);
        }
    }
}
