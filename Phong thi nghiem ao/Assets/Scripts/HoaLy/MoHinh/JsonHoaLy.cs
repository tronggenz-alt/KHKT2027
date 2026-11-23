// Bộ đọc JSON nhỏ, C# thuần (không dùng UnityEngine) cho dữ liệu Hóa lý (cau-hoi.json, khi.json, he-can-bang.json…).
// Kết quả: Dictionary<string, object>, List<object>, string, double, bool hoặc null.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Khkt.HoaLy
{
    public static class JsonHoaLy
    {
        public static object Doc(string s)
        {
            int i = 0;
            var v = GiaTri(s, ref i);
            BoTrang(s, ref i);
            if (i < s.Length) throw new FormatException("JSON thừa ký tự ở vị trí " + i);
            return v;
        }

        public static Dictionary<string, object> D(object o) => o as Dictionary<string, object> ?? new Dictionary<string, object>();
        public static List<object> L(object o) => o as List<object> ?? new List<object>();
        public static object Lay(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v : null;
        public static string S(Dictionary<string, object> d, string k, string macDinh = "") => Lay(d, k) is string s ? s : macDinh;
        public static double F(Dictionary<string, object> d, string k, double macDinh = 0) => Lay(d, k) is double x ? x : macDinh;
        public static bool Co(Dictionary<string, object> d, string k) => d != null && d.ContainsKey(k) && d[k] != null;

        static void BoTrang(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r' || s[i] == '﻿')) i++;
        }

        static object GiaTri(string s, ref int i)
        {
            BoTrang(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON hết đột ngột");
            char c = s[i];
            if (c == '{')
            {
                i++;
                var d = new Dictionary<string, object>();
                BoTrang(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    BoTrang(s, ref i);
                    string k = Chuoi(s, ref i);
                    BoTrang(s, ref i);
                    if (s[i] != ':') throw new FormatException("Thiếu ':' ở " + i);
                    i++;
                    d[k] = GiaTri(s, ref i);
                    BoTrang(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("Thiếu ',' hoặc '}' ở " + i);
                }
            }
            if (c == '[')
            {
                i++;
                var l = new List<object>();
                BoTrang(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(GiaTri(s, ref i));
                    BoTrang(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new FormatException("Thiếu ',' hoặc ']' ở " + i);
                }
            }
            if (c == '"') return Chuoi(s, ref i);
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int b = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (b == i) throw new FormatException("Ký tự lạ '" + c + "' ở " + i);
            return double.Parse(s.Substring(b, i - b), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        static string Chuoi(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("Cần chuỗi ở " + i);
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
            throw new FormatException("Chuỗi không đóng");
        }
    }
}
