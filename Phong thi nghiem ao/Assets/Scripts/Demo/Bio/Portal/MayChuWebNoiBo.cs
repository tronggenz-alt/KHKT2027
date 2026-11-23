using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Khkt.Demo.Bio.Portal
{
    /// <summary>
    /// Máy chủ HTTP tối giản chỉ nghe 127.0.0.1, phục vụ tệp tĩnh trong StreamingAssets/biology_lab.
    /// Trang BIO-LAB 3D dùng ES module + fetch nên Chrome/Edge không chạy được khi mở bằng file://;
    /// Windows (và Unity Editor không ở batchmode) mở trang qua địa chỉ http://127.0.0.1:cổng/ này.
    /// </summary>
    public static class MayChuWebNoiBo
    {
        static TcpListener listener;
        static Thread thread;
        static string root;
        public static int Port { get; private set; }

        public static string Url(string tenTep) => Port > 0 ? $"http://127.0.0.1:{Port}/{tenTep}" : null;

        /// <summary>Bật máy chủ (nếu chưa bật) phục vụ thư mục thuMucGoc. Trả về false nếu không mở được cổng.</summary>
        public static bool Bat(string thuMucGoc)
        {
            if (listener != null && root == thuMucGoc) return true;
            Tat();
            try
            {
                root = Path.GetFullPath(thuMucGoc);
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                thread = new Thread(Vong) { IsBackground = true, Name = "MayChuWebNoiBo" };
                thread.Start();
                Application.quitting -= Tat;
                Application.quitting += Tat;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MayChuWebNoiBo] Không mở được cổng: " + e.Message);
                Tat();
                return false;
            }
        }

        public static void Tat()
        {
            try { listener?.Stop(); } catch { }
            listener = null;
            Port = 0;
        }

        static void Vong()
        {
            var l = listener;
            while (l != null)
            {
                TcpClient c;
                try { c = l.AcceptTcpClient(); }
                catch { return; }
                ThreadPool.QueueUserWorkItem(_ => PhucVu(c));
            }
        }

        static void PhucVu(TcpClient c)
        {
            using (c)
            {
                try
                {
                    var s = c.GetStream();
                    s.ReadTimeout = 5000;
                    var reader = new StreamReader(s, Encoding.ASCII);
                    string dong = reader.ReadLine();
                    if (string.IsNullOrEmpty(dong)) return;
                    while (!string.IsNullOrEmpty(reader.ReadLine())) { }
                    var phan = dong.Split(' ');
                    if (phan.Length < 2 || (phan[0] != "GET" && phan[0] != "HEAD")) { Gui(s, 405, "text/plain", null); return; }
                    string duong = Uri.UnescapeDataString(phan[1].Split('?', '#')[0]).TrimStart('/');
                    if (duong.Length == 0) duong = "biology-lab-3d.html";
                    string full = Path.GetFullPath(Path.Combine(root, duong));
                    if (!full.StartsWith(root, StringComparison.Ordinal) || !File.Exists(full)) { Gui(s, 404, "text/plain", null); return; }
                    Gui(s, 200, Kieu(full), phan[0] == "HEAD" ? null : File.ReadAllBytes(full), new FileInfo(full).Length);
                }
                catch { }
            }
        }

        static void Gui(Stream s, int ma, string kieu, byte[] noiDung, long doDai = -1)
        {
            if (doDai < 0) doDai = noiDung?.Length ?? 0;
            string tt = ma == 200 ? "OK" : ma == 404 ? "Not Found" : "Method Not Allowed";
            var dau = Encoding.ASCII.GetBytes($"HTTP/1.1 {ma} {tt}\r\nContent-Type: {kieu}\r\nContent-Length: {doDai}\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n");
            s.Write(dau, 0, dau.Length);
            if (noiDung != null) s.Write(noiDung, 0, noiDung.Length);
            s.Flush();
        }

        static string Kieu(string p)
        {
            switch (Path.GetExtension(p).ToLowerInvariant())
            {
                case ".html": return "text/html; charset=utf-8";
                case ".js": case ".mjs": return "text/javascript; charset=utf-8";
                case ".css": return "text/css; charset=utf-8";
                case ".json": return "application/json; charset=utf-8";
                case ".svg": return "image/svg+xml";
                case ".png": return "image/png";
                case ".jpg": case ".jpeg": return "image/jpeg";
                case ".webp": return "image/webp";
                case ".woff": return "font/woff";
                case ".woff2": return "font/woff2";
                case ".wasm": return "application/wasm";
                case ".glb": return "model/gltf-binary";
                default: return "application/octet-stream";
            }
        }
    }
}
