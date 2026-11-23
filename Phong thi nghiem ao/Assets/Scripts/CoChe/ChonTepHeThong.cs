using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Khkt.CoChe
{
    /// <summary>
    /// Hộp chọn tệp ẢNH của hệ điều hành, không cần plugin, không chặn khung hình (chạy ở luồng nền, coroutine chờ kết quả).
    ///   macOS (chính): osascript "choose file of type {public.image}" – hộp chọn tệp chuẩn của Finder.
    ///   Windows: PowerShell + System.Windows.Forms.OpenFileDialog (-STA, lệnh mã hóa base64 nên không lo dấu nháy/Unicode).
    ///   Unity Editor: EditorUtility.OpenFilePanelWithFilters.
    ///   iPad/iOS: CHƯA hỗ trợ (cần plugin UIImagePickerController / PHPicker, làm sau cùng theo AGENTS.md) – trả lỗi rõ ràng,
    ///   người dùng gõ đề bằng chữ.
    /// Kết quả: (đường dẫn hoặc null nếu bấm Hủy, lỗi hoặc null).
    /// </summary>
    public static class ChonTepHeThong
    {
        public static readonly string[] DuoiAnh = { "png", "jpg", "jpeg", "webp", "heic", "gif" };

        /// <summary>Kiểm thử thay hộp chọn bằng hàm giả (không mở cửa sổ).</summary>
        public static Func<(string duong, string loi)> GiaLap;

        public static bool HoTro
        {
            get
            {
#if UNITY_EDITOR || UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN
                return true;
#else
                return false;
#endif
            }
        }

        public static IEnumerator ChonAnh(string tieuDe, Action<string, string> xong)
        {
            if (GiaLap != null)
            {
                yield return null;
                var (d, l) = GiaLap();
                xong(d, l);
                yield break;
            }
#if UNITY_EDITOR
            yield return null;
            string p = UnityEditor.EditorUtility.OpenFilePanelWithFilters(tieuDe, "", new[] { "Ảnh đề", string.Join(",", DuoiAnh) });
            xong(string.IsNullOrEmpty(p) ? null : p, null);
#elif UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN
            var viec = Task.Run(() => ChayHop(tieuDe));
            while (!viec.IsCompleted) yield return null;
            if (viec.IsFaulted) xong(null, "Không mở được hộp chọn tệp: " + viec.Exception?.GetBaseException().Message);
            else xong(viec.Result.duong, viec.Result.loi);
#else
            yield return null;
            xong(null, "Chọn ảnh đề chưa có trên thiết bị này (iPad làm sau) – hãy gõ đề bằng chữ.");
#endif
        }

        static (string duong, string loi) ChayHop(string tieuDe)
        {
#if UNITY_STANDALONE_OSX
            string loiNhac = tieuDe.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string script = $"POSIX path of (choose file of type {{\"public.image\"}} with prompt \"{loiNhac}\")";
            var (ma, ra, loi) = Chay("/usr/bin/osascript", new[] { "-e", script });
            if (ma != 0) return (null, loi.Contains("-128") ? null : "osascript: " + loi.Trim());   // -128 = người dùng bấm Hủy
            return (ra.Trim().Length > 0 ? ra.Trim() : null, null);
#elif UNITY_STANDALONE_WIN
            string loc = "Ảnh đề|" + string.Join(";", Array.ConvertAll(DuoiAnh, d => "*." + d));
            string ps = "[Console]::OutputEncoding=[Text.Encoding]::UTF8;Add-Type -AssemblyName System.Windows.Forms;" +
                        "$d=New-Object System.Windows.Forms.OpenFileDialog;" +
                        $"$d.Title='{tieuDe.Replace("'", "''")}';$d.Filter='{loc.Replace("'", "''")}';" +
                        "if($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){[Console]::Out.Write($d.FileName)}";
            string b64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(ps));
            var (ma, ra, loi) = Chay("powershell.exe", new[] { "-NoProfile", "-STA", "-NonInteractive", "-EncodedCommand", b64 });
            if (ma != 0) return (null, "PowerShell: " + loi.Trim());
            return (ra.Trim().Length > 0 ? ra.Trim() : null, null);
#else
            return (null, "Không hỗ trợ trên thiết bị này.");
#endif
        }

        static (int ma, string ra, string loi) Chay(string tep, string[] thamSo)
        {
            var psi = new ProcessStartInfo(tep)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            };
            psi.Arguments = string.Join(" ", Array.ConvertAll(thamSo, Nhay));
            using var p = Process.Start(psi);
            if (p == null) return (-1, "", "không chạy được " + tep);
            var raTask = p.StandardOutput.ReadToEndAsync();
            var loiTask = p.StandardError.ReadToEndAsync();
            p.WaitForExit();
            return (p.ExitCode, raTask.Result, loiTask.Result);
        }

        /// <summary>Bọc một tham số dòng lệnh trong dấu nháy kép (quy ước chung của .NET/Mono trên Mac và Windows).</summary>
        static string Nhay(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        /// <summary>Đọc ảnh đã chọn; lỗi đọc tệp → (null, lý do).</summary>
        public static (byte[] du, string loi) DocTep(string duong, long toiDa)
        {
            try
            {
                var fi = new FileInfo(duong);
                if (!fi.Exists) return (null, "Không thấy tệp " + duong);
                if (fi.Length > toiDa * 3) return (null, $"Ảnh quá lớn ({fi.Length / 1048576f:0.0} MB).");
                return (File.ReadAllBytes(duong), null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CoChe] Đọc ảnh đề lỗi: " + e.Message);
                return (null, "Không đọc được tệp: " + e.Message);
            }
        }
    }
}
