#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;

/// <summary>
/// Chạy kiểm thử hóa học (tests/ChemEngineTests.cs, tests/PhanUngTests.cs) bên trong Unity ở chế độ batch.
/// Dùng khi Windows Application Control chặn dll tự biên dịch chạy bằng dotnet. Tham số đọc từ biến môi trường
/// KHKT_CHEM_ARGS (cách nhau bởi "|"), kết quả ghi vào KHKT_CHEM_OUT.
/// </summary>
public static class KhktChemRun
{
    public static void Run()
    {
        string outPath = Environment.GetEnvironmentVariable("KHKT_CHEM_OUT") ?? "chem_tests.txt";
        string which = Environment.GetEnvironmentVariable("KHKT_CHEM_TEST") ?? "PhanUngTests";
        string raw = Environment.GetEnvironmentVariable("KHKT_CHEM_ARGS") ?? "";
        var args = raw.Length > 0 ? raw.Split('|') : new string[0];
        int code = 1;
        using (var w = new StreamWriter(outPath, false, new System.Text.UTF8Encoding(false)))
        {
            var old = Console.Out;
            Console.SetOut(w);
            try
            {
                Type t = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) { t = asm.GetType(which); if (t != null) break; }
                var main = t.GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                object r = main.GetParameters().Length == 0 ? main.Invoke(null, null) : main.Invoke(null, new object[] { args });
                code = r is int i ? i : 0;
            }
            catch (Exception e) { Console.WriteLine("LOI: " + e); }
            finally { Console.SetOut(old); }
        }
        EditorApplication.Exit(code);
    }
}
#endif
