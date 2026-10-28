using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Đọc JSON nhỏ gọn (đối tượng → Dictionary&lt;string, object&gt;, mảng → List&lt;object&gt;, số → double, chuỗi, bool, null).
    /// JsonUtility của Unity không đọc được mảng lồng nhau ([[235, 1803], ...]) và đối tượng tự do, nên dữ liệu máy dùng bộ đọc này.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string s)
        {
            int i = 0;
            var v = Value(s, ref i);
            Skip(s, ref i);
            if (i < s.Length) throw new FormatException($"JSON thừa ký tự ở vị trí {i}");
            return v;
        }

        static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON kết thúc sớm");
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>();
                i++;
                Skip(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Skip(s, ref i);
                    string k = Str(s, ref i);
                    Skip(s, ref i);
                    if (s[i] != ':') throw new FormatException($"Thiếu ':' ở vị trí {i}");
                    i++;
                    d[k] = Value(s, ref i);
                    Skip(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException($"Thiếu ',' hoặc '}}' ở vị trí {i}");
                }
            }
            if (c == '[')
            {
                var l = new List<object>();
                i++;
                Skip(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i));
                    Skip(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new FormatException($"Thiếu ',' hoặc ']' ở vị trí {i}");
                }
            }
            if (c == '"') return Str(s, ref i);
            if (s.Length - i >= 4 && s.Substring(i, 4) == "true") { i += 4; return true; }
            if (s.Length - i >= 5 && s.Substring(i, 5) == "false") { i += 5; return false; }
            if (s.Length - i >= 4 && s.Substring(i, 4) == "null") { i += 4; return null; }
            int st = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (st == i) throw new FormatException($"Ký tự lạ '{c}' ở vị trí {i}");
            return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
        }

        static string Str(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException($"Cần chuỗi ở vị trí {i}");
            i++;
            var sb = new StringBuilder();
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    switch (s[i])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; break;
                        default: sb.Append(s[i]); break;
                    }
                }
                else sb.Append(s[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }

        // ---- tiện ích đọc ----
        public static Dictionary<string, object> Obj(object o) => o as Dictionary<string, object>;
        public static List<object> Arr(object o) => o as List<object> ?? new List<object>();
        public static string S(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : "";
        public static double N(Dictionary<string, object> d, string k, double def = 0) => d != null && d.TryGetValue(k, out var v) && v is double x ? x : def;
        public static bool B(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v is bool b && b;
        public static List<object> A(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? Arr(v) : new List<object>();
    }
}
