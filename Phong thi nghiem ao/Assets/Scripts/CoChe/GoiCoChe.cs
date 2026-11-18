using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Khkt.CoChe
{
    /// <summary>
    /// Đọc gói cơ chế .verapack (hợp đồng: nguyen_mau/co_che_3d/HOP_DONG_APP.md mục 4, Python: server/ai/co_che/dong_goi.py).
    ///   "VERA1" | phiên bản 1 | salt 16 | IV 16 | AES-256-CBC(gzip(JSON)) | HMAC-SHA256 32
    /// Kiểm HMAC (so thời gian hằng) TRƯỚC khi giải mã; giải AES-CBC bằng Aes.Create() (chạy trên Mac, Windows, iOS/IL2CPP);
    /// giải nén gzip. Mọi bước trong bộ nhớ, không ghi bản rõ ra đĩa.
    /// Khóa nằm ở Resources/CoChe/khoa.bytes (git bỏ qua) đã được che bằng XOR với mặt nạ dựng từ ba hằng rải ở ba lớp.
    /// Việc che chỉ làm khó thêm: người dịch ngược app vẫn lấy được khóa (xem báo cáo C2).
    /// </summary>
    public static class GoiCoChe
    {
        internal const string CheA = "lều-propellane-1.1.1";
        static readonly byte[] MaDau = Encoding.ASCII.GetBytes("VERA1");
        const int DoDaiDau = 5 + 1 + 16 + 16;
        static byte[] khoa;

        /// <summary>Thư mục gói trong bản dựng.</summary>
        public static string ThuMuc => Path.Combine(Application.streamingAssetsPath, "CoChe");

        /// <summary>Khóa gốc (bỏ che). Thử nghiệm có thể đặt khóa khác qua DatKhoa.</summary>
        public static byte[] Khoa
        {
            get
            {
                if (khoa != null) return khoa;
                var ta = Resources.Load<TextAsset>("CoChe/khoa");
                if (ta == null || ta.bytes == null || ta.bytes.Length != 32)
                    throw new InvalidOperationException("Thiếu khóa gói cơ chế (Resources/CoChe/khoa.bytes). Chạy: python -m server.ai.co_che.dong_goi --unity");
                var mn = MatNa();
                var k = new byte[32];
                for (int i = 0; i < 32; i++) k[i] = (byte)(ta.bytes[i] ^ mn[i]);
                return khoa = k;
            }
        }

        public static void DatKhoa(byte[] k) => khoa = k;

        static byte[] MatNa()
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes("VERA·" + CheA + "·" + TrinhPhat3D.CheB + "·" + CoCheMoDun.CheC));
        }

        static byte[] HMAC(byte[] khoa, byte[] du, int dai)
        {
            using var h = new HMACSHA256(khoa);
            return h.ComputeHash(du, 0, dai);
        }

        static byte[] KhoaCon(byte[] goc, string nhan, byte[] salt)
        {
            var d = new byte[nhan.Length + salt.Length];
            Encoding.ASCII.GetBytes(nhan, 0, nhan.Length, d, 0);
            Buffer.BlockCopy(salt, 0, d, nhan.Length, salt.Length);
            return HMAC(goc, d, d.Length);
        }

        static bool BangNhau(byte[] a, int ia, byte[] b, int n)
        {
            int khac = 0;                                    // so thời gian hằng: không dừng sớm
            for (int i = 0; i < n; i++) khac |= a[ia + i] ^ b[i];
            return khac == 0;
        }

        /// <summary>Giải gói → chuỗi JSON. Sai định dạng / HMAC / khóa → ném InvalidDataException.</summary>
        public static string GiaiJson(byte[] goi, byte[] khoaGoc = null)
        {
            khoaGoc ??= Khoa;
            if (goi == null || goi.Length < DoDaiDau + 16 + 32) throw new InvalidDataException("Gói quá ngắn");
            for (int i = 0; i < 5; i++) if (goi[i] != MaDau[i]) throw new InvalidDataException("Không phải gói VERA");
            if (goi[5] != 1) throw new InvalidDataException($"Phiên bản gói {goi[5]} chưa hỗ trợ");
            var salt = new byte[16];
            var iv = new byte[16];
            Buffer.BlockCopy(goi, 6, salt, 0, 16);
            Buffer.BlockCopy(goi, 22, iv, 0, 16);
            int than = goi.Length - 32;
            var dung = HMAC(KhoaCon(khoaGoc, "VERA-ky", salt), goi, than);
            if (!BangNhau(goi, than, dung, 32)) throw new InvalidDataException("Gói bị sửa hoặc sai khóa (HMAC không khớp)");
            byte[] tho;
            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = KhoaCon(khoaGoc, "VERA-ma", salt);
                aes.IV = iv;
                using var giai = aes.CreateDecryptor();
                tho = giai.TransformFinalBlock(goi, DoDaiDau, than - DoDaiDau);
            }
            using var vao = new MemoryStream(tho);
            using var gz = new GZipStream(vao, CompressionMode.Decompress);
            using var ra = new MemoryStream();
            gz.CopyTo(ra);
            return Encoding.UTF8.GetString(ra.ToArray());
        }

        public static BaiCoChe Giai(byte[] goi, byte[] khoaGoc = null) => BaiCoChe.TuJson(GiaiJson(goi, khoaGoc));

        /// <summary>Mã các bài có trong StreamingAssets/CoChe (Mac, Windows, iOS đọc tệp trực tiếp).</summary>
        public static List<string> DanhSachTrongMay()
        {
            var ds = new List<string>();
            if (!Directory.Exists(ThuMuc)) return ds;
            foreach (var p in Directory.GetFiles(ThuMuc, "*.verapack")) ds.Add(Path.GetFileNameWithoutExtension(p));
            ds.Sort(StringComparer.Ordinal);
            return ds;
        }

        public static BaiCoChe NapTrongMay(string ma) => Giai(File.ReadAllBytes(Path.Combine(ThuMuc, ma + ".verapack")));
    }
}
