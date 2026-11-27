using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Khkt.Menu
{
    /// <summary>
    /// Khóa API AI đám mây riêng của từng tài khoản, chỉ nằm trên máy này.
    /// Lưu ở persistentDataPath/khoa_ai/&lt;băm(máy chủ + mã tài khoản)&gt;.bin, đã mã hóa:
    /// Windows dùng DPAPI (chỉ đúng người dùng Windows đó giải được); máy khác dùng AES với khóa sinh từ mã máy
    /// (chỉ che, không chắc bằng DPAPI). Khóa không bao giờ được ghi log hay ghi vào tệp của dự án. Nơi duy nhất khóa rời app:
    /// yêu cầu Giải bài tập tới máy chủ cơ chế (MayChuCoChe, chỉ khóa Claude/Gemini, chỉ khi máy chủ ở máy này, mạng LAN
    /// hoặc https); máy chủ dùng khóa cho riêng yêu cầu đó rồi bỏ, không lưu.
    /// </summary>
    public static class ApiKeyStore
    {
        public const string Claude = "claude", OpenAi = "openai", Gemini = "gemini", Groq = "groq";
        public static readonly string[] Providers = { Gemini, Groq, Claude, OpenAi };

        public static string ProviderName(string p) => p == Claude ? "Anthropic Claude" : p == OpenAi ? "OpenAI" : p == Gemini ? "Google Gemini" : p == Groq ? "Groq" : p;

        public static string DefaultModel(string p) => p == Claude ? "claude-opus-5-5" : p == Gemini ? "gemini-flash-latest" : p == Groq ? "openai/gpt-oss-120b" : "";

        /// <summary>Đầu khóa thường gặp, chỉ để nhắc khi người dùng dán nhầm loại khóa.</summary>
        public static string KeyPrefix(string p) => p == Claude ? "sk-ant-" : p == OpenAi ? "sk-" : p == Gemini ? "AIza hoặc AQ." : p == Groq ? "gsk_" : "";

        /// <summary>Khóa có đầu như thường gặp của nhà cung cấp không (Gemini có dạng cũ AIza… và dạng mới AQ.…).</summary>
        public static bool LooksLike(string p, string key)
        {
            key = (key ?? "").Trim();
            if (p == Gemini) return key.StartsWith("AIza") || key.StartsWith("AQ.");
            string pre = KeyPrefix(p);
            return pre.Length == 0 || key.StartsWith(pre);
        }

        public sealed class Entry
        {
            public string provider, model, key;
            public bool Valid => !string.IsNullOrEmpty(provider) && !string.IsNullOrEmpty(key);
            public string ModelOrDefault => string.IsNullOrWhiteSpace(model) ? DefaultModel(provider) : model.Trim();
        }

        [Serializable] class Plain { public string nha_cung_cap, mo_hinh, khoa; }

        /// <summary>Thư mục lưu; bài thử đổi sang thư mục tạm.</summary>
        public static string Root = null;
        static string Dir => Root ?? Path.Combine(Application.persistentDataPath, "khoa_ai");

        static Entry cache;
        static string cacheOwner;

        /// <summary>Danh tính chủ khóa: tài khoản đang đăng nhập trên máy chủ đang dùng. Khách không có.</summary>
        public static string Owner => GameSession.User != null ? OwnerFor(GameSession.ServerUrl, GameSession.User.id) : null;

        public static string OwnerFor(string server, int userId) => (server ?? "").Trim().TrimEnd('/').ToLowerInvariant() + "#" + userId;

        static string PathFor(string owner)
        {
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes("vera-khoa-ai|" + owner));
                var sb = new StringBuilder();
                for (int i = 0; i < 16; i++) sb.Append(h[i].ToString("x2"));
                return Path.Combine(Dir, sb + ".bin");
            }
        }

        /// <summary>
        /// Khóa dùng chung của máy này (nhập ở Cài đặt → Trợ lý AI, không cần đăng nhập): dùng cho khách và cho tài khoản
        /// chưa lưu khóa riêng.
        /// </summary>
        public const string DeviceOwner = "may_nay";

        /// <summary>Chủ khóa khi lưu từ Cài đặt: tài khoản đang đăng nhập, khách thì là máy này.</summary>
        public static string OwnerOrDevice => Owner ?? DeviceOwner;

        /// <summary>Khóa đang dùng: của tài khoản đang đăng nhập; chưa có (hoặc là khách) thì khóa chung của máy này.</summary>
        public static Entry Current
        {
            get
            {
                string o = Owner;
                if (o != null)
                {
                    var e = Load(o);
                    if (e != null) return e;
                }
                if (!deviceLoaded)
                {
                    // bộ đệm riêng: không giành bộ đệm của tài khoản (mỗi lần đọc là một lần giải mã DPAPI)
                    string keepOwner = cacheOwner;
                    var keep = cache;
                    deviceCache = Load(DeviceOwner);
                    cacheOwner = keepOwner;
                    cache = keep;
                    deviceLoaded = true;
                }
                return deviceCache;
            }
        }

        static Entry deviceCache;
        static bool deviceLoaded;

        public static bool HasKey => Current != null;

        public static Entry Load(string owner)
        {
            if (string.IsNullOrEmpty(owner)) return null;
            if (owner == cacheOwner) return cache;
            Entry e = null;
            try
            {
                string path = PathFor(owner);
                if (File.Exists(path))
                {
                    var plain = Unprotect(File.ReadAllBytes(path), owner);
                    var p = plain != null ? JsonUtility.FromJson<Plain>(Encoding.UTF8.GetString(plain)) : null;
                    if (p != null && !string.IsNullOrEmpty(p.khoa)) e = new Entry { provider = p.nha_cung_cap, model = p.mo_hinh, key = p.khoa };
                }
            }
            catch (Exception ex) { Debug.LogWarning("[khoa_ai] không đọc được khóa đã lưu: " + ex.GetType().Name); }
            cacheOwner = owner;
            cache = e;
            return e;
        }

        public static void Save(string owner, Entry e)
        {
            if (string.IsNullOrEmpty(owner) || e == null || !e.Valid) throw new ArgumentException("thiếu tài khoản hoặc khóa");
            Directory.CreateDirectory(Dir);
            var json = JsonUtility.ToJson(new Plain { nha_cung_cap = e.provider, mo_hinh = (e.model ?? "").Trim(), khoa = e.key.Trim() });
            var data = Protect(Encoding.UTF8.GetBytes(json), owner);
            string path = PathFor(owner), tmp = path + ".tmp";
            File.WriteAllBytes(tmp, data);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            cacheOwner = owner;
            cache = new Entry { provider = e.provider, model = (e.model ?? "").Trim(), key = e.key.Trim() };
            if (owner == DeviceOwner) { deviceCache = cache; deviceLoaded = true; }
        }

        public static void Delete(string owner)
        {
            if (string.IsNullOrEmpty(owner)) return;
            try { string p = PathFor(owner); if (File.Exists(p)) File.Delete(p); }
            catch (Exception ex) { Debug.LogWarning("[khoa_ai] không xóa được tệp khóa: " + ex.GetType().Name); }
            if (owner == cacheOwner) cache = null;
            if (owner == DeviceOwner) { deviceCache = null; deviceLoaded = true; }
        }

        /// <summary>Đăng xuất: quên bản giải mã trong bộ nhớ (tệp mã hóa vẫn còn cho lần đăng nhập sau).</summary>
        public static void Forget()
        {
            cache = null;
            cacheOwner = null;
            deviceLoaded = false;
        }

        /// <summary>Che khóa để hiện: sk-ant-…a1b2.</summary>
        public static string Mask(string key)
        {
            key = (key ?? "").Trim();
            if (key.Length <= 8) return new string('•', key.Length);
            int head = Mathf.Min(7, key.Length / 4);
            return key.Substring(0, head) + "…" + key.Substring(key.Length - 4);
        }

        public static bool UsesDpapi => Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;

        // ------------------------------------------------------------------
        // Mã hóa
        // ------------------------------------------------------------------

        const byte TagDpapi = 1, TagAes = 2;

        static byte[] Protect(byte[] plain, string owner)
        {
            byte[] entropy = Encoding.UTF8.GetBytes("vera-lab|" + owner);
            if (UsesDpapi)
            {
                var c = Dpapi.Protect(plain, entropy);
                return Concat(TagDpapi, c);
            }
            using (var aes = Aes.Create())
            {
                aes.Key = DeviceKey(owner);
                aes.GenerateIV();
                using (var enc = aes.CreateEncryptor())
                {
                    var c = enc.TransformFinalBlock(plain, 0, plain.Length);
                    var body = new byte[16 + c.Length];
                    Buffer.BlockCopy(aes.IV, 0, body, 0, 16);
                    Buffer.BlockCopy(c, 0, body, 16, c.Length);
                    using (var mac = new HMACSHA256(aes.Key))
                    {
                        var tag = mac.ComputeHash(body);
                        var all = new byte[body.Length + 32];
                        Buffer.BlockCopy(body, 0, all, 0, body.Length);
                        Buffer.BlockCopy(tag, 0, all, body.Length, 32);
                        return Concat(TagAes, all);
                    }
                }
            }
        }

        static byte[] Unprotect(byte[] data, string owner)
        {
            if (data == null || data.Length < 2) return null;
            byte[] entropy = Encoding.UTF8.GetBytes("vera-lab|" + owner);
            var body = new byte[data.Length - 1];
            Buffer.BlockCopy(data, 1, body, 0, body.Length);
            if (data[0] == TagDpapi) return UsesDpapi ? Dpapi.Unprotect(body, entropy) : null;
            if (data[0] != TagAes || body.Length < 16 + 16 + 32) return null;
            var key = DeviceKey(owner);
            int n = body.Length - 32;
            using (var mac = new HMACSHA256(key))
            {
                var tag = mac.ComputeHash(body, 0, n);
                int diff = 0;
                for (int i = 0; i < 32; i++) diff |= tag[i] ^ body[n + i];
                if (diff != 0) return null;
            }
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                var iv = new byte[16];
                Buffer.BlockCopy(body, 0, iv, 0, 16);
                aes.IV = iv;
                using (var dec = aes.CreateDecryptor()) return dec.TransformFinalBlock(body, 16, n - 16);
            }
        }

        static byte[] DeviceKey(string owner)
        {
            using (var sha = SHA256.Create())
                return sha.ComputeHash(Encoding.UTF8.GetBytes("vera-lab-aes|" + SystemInfo.deviceUniqueIdentifier + "|" + System.Environment.UserName + "|" + owner));
        }

        static byte[] Concat(byte tag, byte[] b)
        {
            var o = new byte[b.Length + 1];
            o[0] = tag;
            Buffer.BlockCopy(b, 0, o, 1, b.Length);
            return o;
        }

        /// <summary>DPAPI của Windows qua crypt32 (System.Security.Cryptography.ProtectedData không có trong hồ sơ API của Unity).</summary>
        static class Dpapi
        {
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            struct Blob { public int cbData; public IntPtr pbData; }

            const int UiForbidden = 0x1;

            [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            static extern bool CryptProtectData(ref Blob pDataIn, string szDataDescr, ref Blob pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref Blob pDataOut);

            [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            static extern bool CryptUnprotectData(ref Blob pDataIn, IntPtr ppszDataDescr, ref Blob pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref Blob pDataOut);

            [DllImport("kernel32.dll")]
            static extern IntPtr LocalFree(IntPtr hMem);

            public static byte[] Protect(byte[] data, byte[] entropy) => Run(data, entropy, true);
            public static byte[] Unprotect(byte[] data, byte[] entropy) => Run(data, entropy, false);

            static byte[] Run(byte[] data, byte[] entropy, bool protect)
            {
                var hIn = GCHandle.Alloc(data, GCHandleType.Pinned);
                var hEnt = GCHandle.Alloc(entropy, GCHandleType.Pinned);
                var outBlob = new Blob();
                try
                {
                    var inBlob = new Blob { cbData = data.Length, pbData = hIn.AddrOfPinnedObject() };
                    var entBlob = new Blob { cbData = entropy.Length, pbData = hEnt.AddrOfPinnedObject() };
                    bool ok = protect
                        ? CryptProtectData(ref inBlob, "VERA Lab", ref entBlob, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref outBlob)
                        : CryptUnprotectData(ref inBlob, IntPtr.Zero, ref entBlob, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref outBlob);
                    if (!ok) throw new CryptographicException("DPAPI lỗi " + Marshal.GetLastWin32Error());
                    var result = new byte[outBlob.cbData];
                    Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
                    return result;
                }
                finally
                {
                    hIn.Free();
                    hEnt.Free();
                    if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
                }
            }
        }
    }
}
