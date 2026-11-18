// Mô hình động học: Arrhenius, bậc phản ứng 0/1/2, Van 't Hoff, xúc tác, phân bố năng lượng Maxwell–Boltzmann –
// dịch từ CHEMISTRY frontend/hoa-hoc/hoa-ly/toc-do-phan-ung/mo-hinh.js. C# thuần, không dùng UnityEngine.
using System;
using System.Collections.Generic;

namespace Khkt.HoaLy
{
    public struct KetQuaKhopArrhenius
    {
        public bool Co;
        public double EaKJ, A, HeSoGoc, HeSoTuDo;
    }

    public struct KetQuaPhanBoNangLuong
    {
        public double X, ExpAmX, TiLe3D;
    }

    public sealed class PhanUngTocDo
    {
        public string Id, Ten, PhuongTrinh, Nguon, XucTac = "";
        public string ChatDau = "A", SanPham = "C";
        public float[] MauChatDau = { 0.22f, 0.74f, 0.97f }, MauSanPham = { 0.20f, 0.83f, 0.60f };
        public double EaKJ = 50, A = 1e10, DHKJ = -20, EaXtKJ = double.NaN, DeltaEaXtKJ = 20;
        /// <summary>Tổng bậc phản ứng (từ trường "bac").</summary>
        public int Bac = 1;
        /// <summary>Phản ứng dị thể (có chất rắn) – cho chọn diện tích bề mặt.</summary>
        public bool CoChatRan;

        /// <summary>Ea giảm bao nhiêu khi có xúc tác (kJ/mol): Ea − Ea_xt_kJ nếu có, không thì deltaEa_xt_kJ (mặc định 20).</summary>
        public double DoGiamEaXucTac => !double.IsNaN(EaXtKJ) ? EaKJ - EaXtKJ : DeltaEaXtKJ;

        /// <summary>Ea khi có xúc tác ứng với Ea đang đặt trên thanh trượt (không thấp hơn 5 kJ/mol).</summary>
        public double EaXucTac(double ea) => Math.Max(5, ea - DoGiamEaXucTac);
    }

    public static class TocDoMoHinh
    {
        public const double R_SI = 8.314462618;

        /// <summary>erfc như bản JS: chuỗi tiệm cận khi z > 4, xấp xỉ Chebyshev (Numerical Recipes) khi z ≤ 4.</summary>
        public static double Erfc(double z)
        {
            if (z > 4)
            {
                double z2 = z * z;
                return Math.Exp(-z2) / (z * Math.Sqrt(Math.PI)) * (1 - 1 / (2 * z2) + 3 / (4 * z2 * z2));
            }
            double t = 1.0 / (1.0 + 0.5 * z);
            return t * Math.Exp(-z * z - 1.26551223 +
                t * (1.00002368 + t * (0.37409196 + t * (0.09678418 + t * (-0.18628806 + t * (0.27886807 +
                t * (-1.13520398 + t * (1.48851587 + t * (-0.82215223 + t * 0.17087277)))))))));
        }

        public static double KTheoArrhenius(double A = 1e10, double EaKJ = 50, double T_K = 298.15) => A * Math.Exp(-EaKJ * 1000 / (R_SI * T_K));

        /// <summary>k(T2)/k(T1).</summary>
        public static double TiLeTocDo(double EaKJ = 50, double T1 = 298.15, double T2 = 308.15) => Math.Exp(-(EaKJ * 1000 / R_SI) * (1 / T2 - 1 / T1));

        /// <summary>Hệ số nhiệt độ Van 't Hoff γ ≈ exp(10·Ea/(R·T²)).</summary>
        public static double HeSoVantHoff(double EaKJ = 50, double T_K = 298.15) => Math.Exp(10 * EaKJ * 1000 / (R_SI * T_K * T_K));

        public static double EaTuGamma(double gamma = 2, double T_K = 298.15) => R_SI * T_K * T_K * Math.Log(gamma) / 10 / 1000;

        /// <summary>Tốc độ tăng bao nhiêu lần khi xúc tác làm giảm Ea đi deltaEa.</summary>
        public static double HeSoTangXucTac(double deltaEaKJ = 20, double T_K = 298.15) => Math.Exp(deltaEaKJ * 1000 / (R_SI * T_K));

        /// <summary>Khớp tuyến tính ln k = ln A − (Ea/R)(1/T).</summary>
        public static KetQuaKhopArrhenius KhopArrhenius(IList<double> T, IList<double> k)
        {
            var kq = new KetQuaKhopArrhenius();
            if (T == null || T.Count < 2) return kq;
            int N = T.Count;
            double sx = 0, sy = 0, sxx = 0, sxy = 0;
            for (int i = 0; i < N; i++)
            {
                double x = 1.0 / T[i], y = Math.Log(k[i]);
                sx += x; sy += y; sxx += x * x; sxy += x * y;
            }
            double den = N * sxx - sx * sx;
            if (Math.Abs(den) < 1e-15) return kq;
            double slope = (N * sxy - sx * sy) / den;
            double inter = (sy - slope * sx) / N;
            kq.Co = true;
            kq.HeSoGoc = slope; kq.HeSoTuDo = inter;
            kq.EaKJ = -slope * R_SI / 1000;
            kq.A = Math.Exp(inter);
            return kq;
        }

        public static double NongDoBac0(double C0, double k, double t) => Math.Max(0, C0 - k * t);
        public static double ThoiGianBanHuyBac0(double C0, double k) => C0 / (2 * k);
        public static double NongDoBac1(double C0, double k, double t) => C0 * Math.Exp(-k * t);
        public static double ThoiGianBanHuyBac1(double k) => Math.Log(2) / k;
        public static double NongDoBac2(double C0, double k, double t) => 1.0 / (1.0 / C0 + k * t);
        public static double ThoiGianBanHuyBac2(double C0, double k) => 1.0 / (k * C0);

        public static double NongDo(int bac, double C0, double k, double t) =>
            bac <= 0 ? NongDoBac0(C0, k, t) : bac == 1 ? NongDoBac1(C0, k, t) : NongDoBac2(C0, k, t);

        public static double ThoiGianBanHuy(int bac, double C0, double k) =>
            bac <= 0 ? ThoiGianBanHuyBac0(C0, k) : bac == 1 ? ThoiGianBanHuyBac1(k) : ThoiGianBanHuyBac2(C0, k);

        /// <summary>Tốc độ tức thời v = k·C^bậc (M/s).</summary>
        public static double TocDoTucThoi(int bac, double C, double k) => k * Math.Pow(C, bac);

        /// <summary>Tỉ lệ phân tử có năng lượng E ≥ Ea (Maxwell–Boltzmann 3D): erfc(√x) + 2√(x/π)·e^(−x), x = Ea/RT.</summary>
        public static KetQuaPhanBoNangLuong PhanBoNangLuong3D(double EaKJ = 50, double T_K = 298.15)
        {
            double x = EaKJ * 1000 / (R_SI * T_K);
            double e = Math.Exp(-x), sx = Math.Sqrt(x);
            return new KetQuaPhanBoNangLuong { X = x, ExpAmX = e, TiLe3D = Erfc(sx) + 2 * Math.Sqrt(x / Math.PI) * e };
        }

        /// <summary>Mật độ phân bố năng lượng Maxwell–Boltzmann 3D f(E) (1/(kJ/mol)), E tính bằng kJ/mol.</summary>
        public static double MatDoNangLuong(double EKJ, double T_K)
        {
            double RT = R_SI * T_K / 1000;   // kJ/mol
            if (EKJ <= 0) return 0;
            return 2 / Math.Sqrt(Math.PI) * Math.Sqrt(EKJ) / Math.Pow(RT, 1.5) * Math.Exp(-EKJ / RT);
        }

        public static double TiLeVantHoff(double gamma, double T1, double T2) => Math.Pow(gamma, (T2 - T1) / 10);

        /// <summary>Đọc phan-ung.json.</summary>
        public static List<PhanUngTocDo> DocDanhSach(string json)
        {
            var ds = new List<PhanUngTocDo>();
            foreach (var o in JsonHoaLy.L(JsonHoaLy.Lay(JsonHoaLy.D(JsonHoaLy.Doc(json)), "danh_sach")))
            {
                var d = JsonHoaLy.D(o);
                var p = new PhanUngTocDo
                {
                    Id = JsonHoaLy.S(d, "id"), Ten = JsonHoaLy.S(d, "ten"), PhuongTrinh = JsonHoaLy.S(d, "phuong_trinh"),
                    Nguon = JsonHoaLy.S(d, "nguon"), XucTac = JsonHoaLy.S(d, "xuc_tac"),
                    EaKJ = JsonHoaLy.F(d, "Ea_kJ", 50), A = JsonHoaLy.F(d, "A", 1e10), DHKJ = JsonHoaLy.F(d, "dH_kJ"),
                    EaXtKJ = JsonHoaLy.F(d, "Ea_xt_kJ", double.NaN), DeltaEaXtKJ = JsonHoaLy.F(d, "deltaEa_xt_kJ", 20),
                };
                int bac = 0;
                foreach (var kv in JsonHoaLy.D(JsonHoaLy.Lay(d, "bac"))) bac += (int)Math.Round(kv.Value is double x ? x : 0);
                p.Bac = Math.Max(0, Math.Min(2, bac));
                bool coDau = false, coSp = false;
                foreach (var co in JsonHoaLy.L(JsonHoaLy.Lay(d, "chat")))
                {
                    var c = JsonHoaLy.D(co);
                    string vt = JsonHoaLy.S(c, "vai_tro");
                    if (JsonHoaLy.S(c, "pha") == "r") p.CoChatRan = true;
                    var mau = MauHex.Doc(JsonHoaLy.S(c, "mau"));
                    if (vt == "chat_dau" && !coDau && JsonHoaLy.S(c, "pha") != "r") { p.ChatDau = JsonHoaLy.S(c, "ct"); if (mau != null) p.MauChatDau = mau; coDau = true; }
                    if (vt == "san_pham" && !coSp) { p.SanPham = JsonHoaLy.S(c, "ct"); if (mau != null) p.MauSanPham = mau; coSp = true; }
                }
                ds.Add(p);
            }
            return ds;
        }
    }

    /// <summary>Lấy màu #rrggbb trong chuỗi CSS dạng "var(--x, #38bdf8)".</summary>
    public static class MauHex
    {
        public static float[] Doc(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            int i = s.IndexOf('#');
            if (i < 0 || i + 7 > s.Length) return null;
            try
            {
                int v = Convert.ToInt32(s.Substring(i + 1, 6), 16);
                return new[] { ((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f };
            }
            catch { return null; }
        }
    }
}
