using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Khkt.CoChe
{
    /// <summary>
    /// Bộ đọc JSON nhỏ (JsonUtility của Unity không đọc được đối tượng dạng từ điển {id: [x,y,z]} của hợp đồng cơ chế).
    /// Kết quả: Dictionary&lt;string, object&gt;, List&lt;object&gt;, string, double, bool, null.
    /// </summary>
    public static class JsonNho
    {
        public static object Doc(string s)
        {
            int i = 0;
            var v = GiaTri(s, ref i);
            BoTrang(s, ref i);
            if (i != s.Length) throw new FormatException($"JSON thừa ký tự ở vị trí {i}");
            return v;
        }

        static void BoTrang(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\n' || s[i] == '\r' || s[i] == '\t')) i++;
        }

        static object GiaTri(string s, ref int i)
        {
            BoTrang(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON hết bất ngờ");
            char c = s[i];
            if (c == '{') return DoiTuong(s, ref i);
            if (c == '[') return Mang(s, ref i);
            if (c == '"') return Chuoi(s, ref i);
            if (c == 't' && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (c == 'f' && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (c == 'n' && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            return So(s, ref i);
        }

        static Dictionary<string, object> DoiTuong(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            BoTrang(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true)
            {
                BoTrang(s, ref i);
                string k = Chuoi(s, ref i);
                BoTrang(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException($"Thiếu ':' ở vị trí {i}");
                i++;
                d[k] = GiaTri(s, ref i);
                BoTrang(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return d; }
                throw new FormatException($"Thiếu ',' hoặc '}}' ở vị trí {i}");
            }
        }

        static List<object> Mang(string s, ref int i)
        {
            var l = new List<object>();
            i++;
            BoTrang(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(GiaTri(s, ref i));
                BoTrang(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return l; }
                throw new FormatException($"Thiếu ',' hoặc ']' ở vị trí {i}");
            }
        }

        static string Chuoi(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException($"Cần chuỗi ở vị trí {i}");
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            throw new FormatException("Chuỗi JSON chưa đóng");
        }

        static double So(string s, ref int i)
        {
            int a = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (a == i) throw new FormatException($"Ký tự lạ '{s[a]}' ở vị trí {a}");
            return double.Parse(s.Substring(a, i - a), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        // ---- tiện ích đọc
        public static Dictionary<string, object> D(object o) => o as Dictionary<string, object> ?? new Dictionary<string, object>();
        public static List<object> L(object o) => o as List<object> ?? new List<object>();
        public static object Lay(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v : null;
        public static string S(Dictionary<string, object> d, string k, string macDinh = "") => Lay(d, k) is string s ? s : (Lay(d, k)?.ToString() ?? macDinh);
        public static float F(object o) => o is double x ? (float)x : o is bool b ? (b ? 1f : 0f) : 0f;
        public static int I(object o) => o is double x ? (int)Math.Round(x) : 0;
        public static bool B(Dictionary<string, object> d, string k) => Lay(d, k) is bool b && b;
    }
}
