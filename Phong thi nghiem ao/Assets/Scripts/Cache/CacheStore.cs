using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Khkt.Menu;
using UnityEngine;

namespace Khkt.Cache
{
    /// <summary>
    /// Kho tệp cache trên máy. Mỗi tài khoản một thư mục, mỗi mục một tệp có phần đầu ghi phiên bản và mã băm.
    /// Mọi lỗi đĩa (đầy, chỉ đọc, tệp đang bị giữ) chỉ làm mục đó không được lưu, không bao giờ ném ra ngoài.
    /// Đặt tên theo mã băm của khóa nên đường dẫn API không lộ ra tên tệp.
    /// </summary>
    public static class CacheStore
    {
        /// <summary>Đổi khi định dạng tệp đổi: tệp cũ tự bị bỏ qua.</summary>
        public const int FormatVersion = 1;

        /// <summary>Tổng dung lượng toàn bộ cache, gồm mọi tài khoản trên máy. Bài thử hạ xuống để thử LRU.</summary>
        public static long MaxTotalBytes { get; set; } = 200L * 1024 * 1024;

        /// <summary>Một mục lớn hơn mức này thì không lưu, tránh một tệp đẩy hết các mục khác ra.</summary>
        public const long MaxEntryBytes = 20L * 1024 * 1024;

        /// <summary>Ổ còn ít hơn mức này thì ngừng ghi cache, nhường chỗ cho việc khác.</summary>
        public const long MinFreeDiskBytes = 1024L * 1024 * 1024;

        const string Magic = "VERA";

        static string lastUserFolder;
        static bool warnedThisRun;
        static long? knownTotal;
        static int writesSinceScan;

        /// <summary>Cho bài thử đổi nơi lưu; để trống là dùng persistentDataPath.</summary>
        public static string RootOverride { get; set; }

        /// <summary>Cho bài thử giả lập ổ gần đầy. Nhỏ hơn 0 là đọc dung lượng thật của ổ.</summary>
        public static long FakeFreeBytes { get; set; } = -1;

        /// <summary>Lần ghi gần nhất có vào được đĩa không.</summary>
        public static bool LastWriteOk { get; private set; } = true;

        public static string Root => Path.Combine(RootOverride ?? Application.persistentDataPath, "cache_v" + FormatVersion);

        /// <summary>Thư mục của người đang dùng; tách theo máy chủ vì id tài khoản chỉ có nghĩa trong một máy chủ.</summary>
        public static string CurrentUserFolder
        {
            get
            {
                string who = GameSession.User != null ? "u" + GameSession.User.id : "khach";
                string folder = Path.Combine(Root, Hash(GameSession.ServerUrl).Substring(0, 12) + "_" + who);
                if (GameSession.User != null) lastUserFolder = folder;
                return folder;
            }
        }

        /// <summary>Đọc một mục. Tệp hỏng, sai phiên bản, thiếu đều trả false và xóa tệp hỏng đi.</summary>
        public static bool TryRead(string key, out byte[] body, out long ageSec, string folder = null)
        {
            body = null;
            ageSec = -1;
            string file = PathFor(key, folder);
            byte[] raw;
            try
            {
                if (!File.Exists(file)) return false;
                // Cho phép cửa sổ khác thay hoặc xóa tệp trong lúc đang đọc
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (fs.Length > MaxEntryBytes + 1024) { Discard(file); return false; }
                    raw = new byte[(int)fs.Length];
                    int got = 0;
                    while (got < raw.Length)
                    {
                        int n = fs.Read(raw, got, raw.Length - got);
                        if (n <= 0) break;
                        got += n;
                    }
                    if (got != raw.Length) { Discard(file); return false; }
                }
            }
            catch (Exception e) when (IsDiskError(e))
            {
                return false;
            }

            if (!Decode(raw, out body, out long savedUnix))
            {
                Discard(file);
                body = null;
                return false;
            }
            ageSec = Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - savedUnix);
            Touch(file);
            return true;
        }

        /// <summary>Ghi nguyên tử: ghi ra tệp tạm cùng thư mục, ép xuống đĩa, rồi mới thay tệp cũ. Lỗi thì tệp cũ còn nguyên.</summary>
        public static bool Write(string key, byte[] body, string folder = null)
        {
            LastWriteOk = false;
            if (body == null || body.Length > MaxEntryBytes) return false;
            if (FreeDiskBytes() < MinFreeDiskBytes) { Warn("ổ đĩa sắp đầy, tạm ngừng lưu cache"); return false; }

            string file = PathFor(key, folder);
            string tmp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";  // tên riêng: hai cửa sổ ghi cùng lúc không đè tệp tạm của nhau
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                byte[] head = Header(body);
                using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    fs.Write(head, 0, head.Length);
                    fs.Write(body, 0, body.Length);
                    fs.Flush(true);
                }
                long old = 0;
                try { if (File.Exists(file)) old = new FileInfo(file).Length; } catch (Exception e) when (IsDiskError(e)) { }
                if (File.Exists(file)) File.Replace(tmp, file, null);
                else File.Move(tmp, file);
                LastWriteOk = true;
                if (knownTotal.HasValue) knownTotal += head.Length + body.Length - old;
            }
            catch (Exception e) when (IsDiskError(e))
            {
                Warn(e.Message);
                TryDelete(tmp);
                return false;
            }
            // Đếm lại định kỳ vì cửa sổ khác cũng có thể ghi vào cùng thư mục
            if (++writesSinceScan >= 50) { knownTotal = null; writesSinceScan = 0; }
            if (!knownTotal.HasValue || knownTotal.Value > MaxTotalBytes) Trim(MaxTotalBytes);
            return true;
        }

        public static void Remove(string key, string folder = null) => TryDelete(PathFor(key, folder));

        /// <summary>Xóa toàn bộ dữ liệu của người đang dùng (hoặc người vừa đăng nhập gần nhất).</summary>
        public static void ClearUser()
        {
            string folder = GameSession.User != null ? CurrentUserFolder : lastUserFolder;
            lastUserFolder = null;
            if (folder == null) return;
            try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
            catch (Exception e) when (IsDiskError(e)) { Warn("không xóa hết được dữ liệu cũ: " + e.Message); }
            knownTotal = null;
        }

        /// <summary>Xóa mọi cache trên máy, gồm các phiên bản định dạng cũ.</summary>
        public static void ClearAll()
        {
            string baseDir = RootOverride ?? Application.persistentDataPath;
            try
            {
                foreach (var dir in Directory.GetDirectories(baseDir, "cache_v*")) Directory.Delete(dir, true);
            }
            catch (Exception e) when (IsDiskError(e)) { Warn(e.Message); }
            knownTotal = null;
        }

        /// <summary>Tổng số byte đang dùng.</summary>
        public static long TotalBytes()
        {
            long sum = 0;
            foreach (var f in AllFiles()) sum += f.Length;
            knownTotal = sum;
            return sum;
        }

        /// <summary>Xóa mục dùng lâu nhất cho tới khi tổng dung lượng về dưới 90 % giới hạn.</summary>
        public static int Trim(long limit)
        {
            var files = AllFiles();
            long sum = 0;
            foreach (var f in files) sum += f.Length;
            int removed = 0;
            if (sum > limit)
            {
                files.Sort((a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
                long target = limit / 10 * 9;
                foreach (var f in files)
                {
                    if (sum <= target) break;
                    long size = f.Length;
                    if (TryDelete(f.FullName)) { sum -= size; removed++; }
                }
            }
            knownTotal = sum;
            return removed;
        }

        /// <summary>Dọn khi mở app: thư mục định dạng cũ, tệp tạm do lần trước bị tắt ngang.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Housekeep()
        {
            string baseDir = RootOverride ?? Application.persistentDataPath;
            try
            {
                string current = Path.GetFileName(Root);
                foreach (var dir in Directory.GetDirectories(baseDir, "cache_v*"))
                    if (Path.GetFileName(dir) != current) Directory.Delete(dir, true);
                if (Directory.Exists(Root))
                    foreach (var tmp in Directory.GetFiles(Root, "*.tmp", SearchOption.AllDirectories))
                        // cửa sổ khác có thể đang ghi: chỉ xóa tệp đã nằm đó một lúc
                        if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(tmp)).TotalMinutes > 10) TryDelete(tmp);
            }
            catch (Exception e) when (IsDiskError(e)) { Warn(e.Message); }
            Trim(MaxTotalBytes);
        }

        static List<FileInfo> AllFiles()
        {
            var list = new List<FileInfo>();
            try
            {
                var root = new DirectoryInfo(Root);
                if (root.Exists) list.AddRange(root.GetFiles("*", SearchOption.AllDirectories));
            }
            catch (Exception e) when (IsDiskError(e)) { Warn(e.Message); }
            return list;
        }

        // Đầu tệp: VERA <định dạng> <phiên bản app> <lúc lưu> <độ dài> <SHA-256 phần thân>\n
        static byte[] Header(byte[] body)
        {
            string line = $"{Magic} {FormatVersion} {AppVersionToken()} {DateTimeOffset.UtcNow.ToUnixTimeSeconds()} {body.Length} {HashBytes(body)}\n";
            return Encoding.ASCII.GetBytes(line);
        }

        static bool Decode(byte[] raw, out byte[] body, out long savedUnix)
        {
            body = null;
            savedUnix = 0;
            int nl = Array.IndexOf(raw, (byte)'\n', 0, Math.Min(raw.Length, 256));
            if (nl <= 0) return false;
            string[] p = Encoding.ASCII.GetString(raw, 0, nl).Split(' ');
            if (p.Length != 6 || p[0] != Magic || p[1] != FormatVersion.ToString() || p[2] != AppVersionToken()) return false;
            if (!long.TryParse(p[3], out savedUnix) || !int.TryParse(p[4], out int len)) return false;
            if (len != raw.Length - nl - 1) return false;
            var b = new byte[len];
            Buffer.BlockCopy(raw, nl + 1, b, 0, len);
            if (HashBytes(b) != p[5]) return false;
            body = b;
            return true;
        }

        static string AppVersionToken() => (Application.version ?? "0").Replace(' ', '_');

        static string PathFor(string key, string folder) => Path.Combine(folder ?? CurrentUserFolder, Hash(key));

        /// <summary>Đánh dấu vừa dùng cho LRU. Thời điểm truy cập của Windows hay bị tắt nên dùng thời điểm ghi; thời gian lưu đã nằm trong phần đầu tệp.</summary>
        static void Touch(string file)
        {
            try
            {
                if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(file)).TotalMinutes > 10)
                    File.SetLastWriteTimeUtc(file, DateTime.UtcNow);
            }
            catch (Exception e) when (IsDiskError(e)) { }
        }

        static long FreeDiskBytes()
        {
            if (FakeFreeBytes >= 0) return FakeFreeBytes;
            try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(Root))).AvailableFreeSpace; }
            catch (Exception e) when (IsDiskError(e) || e is ArgumentException) { return long.MaxValue; }
        }

        static void Discard(string file)
        {
            Debug.LogWarning("[VERA] Bỏ một tệp cache hỏng hoặc khác phiên bản");
            TryDelete(file);
        }

        static bool TryDelete(string file)
        {
            try { if (File.Exists(file)) File.Delete(file); return true; }
            catch (Exception e) when (IsDiskError(e)) { return false; }
        }

        static void Warn(string message)
        {
            if (warnedThisRun) return;
            warnedThisRun = true;
            Debug.LogWarning("[VERA] Cache trên máy: " + message);
        }

        static bool IsDiskError(Exception e) =>
            e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException || e is NotSupportedException;

        internal static string Hash(string s) => HashBytes(Encoding.UTF8.GetBytes(s ?? ""));

        static string HashBytes(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(data);
                var sb = new StringBuilder(h.Length * 2);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
