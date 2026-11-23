// Mô hình cân bằng hóa học: Kc theo T (van 't Hoff), Kp ↔ Kc, thương số Q, bảng ICE, chiều chuyển dịch Le Chatelier –
// dịch từ CHEMISTRY frontend/hoa-hoc/hoa-ly/can-bang-le-chatelier/mo-hinh.js. C# thuần, không dùng UnityEngine.
// Thêm HeCanBang: hệ tổng quát aA + bB ⇌ cC + dD (đọc từ he-can-bang.json) để chạy mô phỏng theo thời gian.
using System;
using System.Collections.Generic;

namespace Khkt.HoaLy
{
    public enum ChieuDich { CanBang, Thuan, Nghich }

    public struct KetQuaPhanLi
    {
        public double X, CA, CB, Alpha, Q;
    }

    public struct KetQuaTichPhan
    {
        public double CA, CB, RT, RN, Q;
    }

    public struct GiaiThichLeChatelier
    {
        public ChieuDich Chieu;
        public string VanBan;
    }

    public static class CanBangMoHinh
    {
        public const double R_SI = 8.314462618;
        public const double R_BAR = 0.08314462618;

        /// <summary>K(T) = K_ref · exp(−ΔH/R · (1/T − 1/T_ref)).</summary>
        public static double KcTheoT(double T_K = 298.15, double deltaHKJ = 57.2, double T_ref = 298.15, double Kc_ref = 0.00461) =>
            Kc_ref * Math.Exp(-(deltaHKJ * 1000 / R_SI) * (1 / T_K - 1 / T_ref));

        /// <summary>Kp = Kc·(R·T)^Δn (R tính theo bar·L).</summary>
        public static double KpTuKc(double Kc = 0.00461, double T_K = 298.15, double deltaN = 1) => Kc * Math.Pow(R_BAR * T_K, deltaN);

        public static double KcTuKp(double Kp, double T_K, double deltaN) => Kp / Math.Pow(R_BAR * T_K, deltaN);

        /// <summary>Kp của N₂ + 3H₂ ⇌ 2NH₃ từ ΔH, ΔS: ln K = −ΔH/RT + ΔS/R.</summary>
        public static double KpHaber(double T_K = 298.15, double dHKJ = -91.8, double dS_J_K = -198.1) =>
            Math.Exp(-(dHKJ * 1000 / (R_SI * T_K)) + dS_J_K / R_SI);

        public static double TinhQ(double cA = 1, double cB = 0, double a = 1, double b = 2) =>
            cA <= 0 ? double.PositiveInfinity : Math.Pow(cB, b) / Math.Pow(cA, a);

        /// <summary>Q ngay sau khi nén thể tích s lần (nồng độ nhân s): Q = K·s^Δn.</summary>
        public static double QSauNen(double K = 0.00461, double s = 2, double deltaN = 1) => K * Math.Pow(s, deltaN);

        /// <summary>Chiều chuyển dịch: |Q−K|/K ≤ 0,5 % là cân bằng; Q &lt; K thuận; Q &gt; K nghịch.</summary>
        public static ChieuDich Chieu(double Q, double K)
        {
            double sai = (Q - K) / K;
            if (Math.Abs(sai) <= 0.005) return ChieuDich.CanBang;
            return Q < K ? ChieuDich.Thuan : ChieuDich.Nghich;
        }

        public static string TenChieu(ChieuDich c) => c == ChieuDich.Thuan ? "thuận" : c == ChieuDich.Nghich ? "nghịch" : "cân bằng";

        /// <summary>Bảng ICE cho A ⇌ 2B (N₂O₄ ⇌ 2NO₂): Kc = (cB0 + 2x)² / (cA0 − x).</summary>
        public static KetQuaPhanLi CanBangPhanLi(double cA0 = 0.04, double cB0 = 0, double Kc = 0.00461)
        {
            if (cB0 == 0)
            {
                double delta = Kc * Kc + 16 * Kc * cA0;
                double x = (-Kc + Math.Sqrt(delta)) / 8;
                return new KetQuaPhanLi { X = x, CA = cA0 - x, CB = 2 * x, Alpha = x / cA0, Q = Math.Pow(2 * x, 2) / (cA0 - x) };
            }
            double A = 4, B = 4 * cB0 + Kc, C = cB0 * cB0 - Kc * cA0;
            double d = B * B - 4 * A * C;
            double xg = (-B + Math.Sqrt(Math.Max(0, d))) / (2 * A);
            double ca = cA0 - xg, cb = cB0 + 2 * xg;
            return new KetQuaPhanLi { X = xg, CA = ca, CB = cb, Alpha = xg / (cA0 + cB0 / 2), Q = cb * cb / ca };
        }

        /// <summary>Một bước Euler cho A ⇌ 2B: r_t = k_t[A], r_n = (k_t/Kc)[B]².</summary>
        public static KetQuaTichPhan BuocTichPhan(double cA, double cB, double Kc, double dt, double k_t = 1.0)
        {
            double k_n = k_t / Kc;
            double rt = k_t * cA, rn = k_n * cB * cB, r = rt - rn;
            double ca = Math.Max(1e-6, cA - r * dt);
            double cb = Math.Max(1e-6, cB + 2 * r * dt);
            return new KetQuaTichPhan { CA = ca, CB = cb, RT = rt, RN = rn, Q = cb * cb / ca };
        }

        /// <summary>Lời giải thích Le Chatelier từ số liệu thật (giaiThichLeChatelier của bản JS).</summary>
        public static GiaiThichLeChatelier GiaiThich(double Q, double K, string tacDong, bool laN2O4)
        {
            double ty = Q / K;
            if (Math.Abs(ty - 1.0) < 0.02)
                return new GiaiThichLeChatelier
                {
                    Chieu = ChieuDich.CanBang,
                    VanBan = "Hệ đang ở trạng thái cân bằng động (Q ≈ K). Tốc độ phản ứng thuận bằng tốc độ phản ứng nghịch (v_t = v_n). " +
                             "Nồng độ các chất và màu của hệ không đổi theo thời gian."
                };
            if (Q > K)
                return new GiaiThichLeChatelier
                {
                    Chieu = ChieuDich.Nghich,
                    VanBan = $"Ngay sau tác động {tacDong}, Q = {SoNgan(Q)} > K = {SoNgan(K)}. Theo nguyên lý Le Chatelier, hệ chuyển dịch theo chiều nghịch để Q giảm về K. " +
                             (laN2O4 ? "Chiều nghịch tạo lại NO₂ nâu đỏ từ N₂O₄ không màu." : "Tốc độ phản ứng nghịch lớn hơn tốc độ phản ứng thuận (v_n > v_t).")
                };
            return new GiaiThichLeChatelier
            {
                Chieu = ChieuDich.Thuan,
                VanBan = $"Ngay sau tác động {tacDong}, Q = {SoNgan(Q)} < K = {SoNgan(K)}. Theo nguyên lý Le Chatelier, hệ chuyển dịch theo chiều thuận để tăng sản phẩm. " +
                         (laN2O4 ? "Chiều thuận dime hóa NO₂ nâu đỏ thành N₂O₄ không màu." : "Tốc độ phản ứng thuận lớn hơn tốc độ phản ứng nghịch (v_t > v_n).")
            };
        }

        /// <summary>Số gọn kiểu khoa học cho văn bản (1,234 · 5,67e+08).</summary>
        public static string SoNgan(double x)
        {
            if (double.IsInfinity(x) || double.IsNaN(x)) return "∞";
            double a = Math.Abs(x);
            string s = a != 0 && (a >= 1e5 || a < 1e-3) ? x.ToString("0.###e+0", System.Globalization.CultureInfo.InvariantCulture)
                                                      : x.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
            return s.Replace('.', ',');
        }

        public static List<HeCanBang> DocDanhSach(string json)
        {
            var ds = new List<HeCanBang>();
            foreach (var o in JsonHoaLy.L(JsonHoaLy.Lay(JsonHoaLy.D(JsonHoaLy.Doc(json)), "danh_sach")))
                ds.Add(HeCanBang.TuJson(JsonHoaLy.D(o)));
            return ds;
        }
    }

    public sealed class ChatCanBang
    {
        public string CongThuc, Ten, Pha = "k";
        public int HeSo = 1;
        public bool SanPham;
        public float[] Mau = { 0.8f, 0.8f, 0.8f };
        /// <summary>Độ đậm màu (0 = không màu). Dùng để pha màu ống nghiệm.</summary>
        public double DoDam;
        public bool LaKhi => Pha == "k";
    }

    /// <summary>
    /// Hệ cân bằng tổng quát Σν_R·R ⇌ Σν_P·P với nồng độ C[] (mol/L). K(T) theo van 't Hoff; hệ "Kp" đổi sang Kc bằng Δn.
    /// Mô phỏng theo thời gian: tìm độ tiến ξ_cb (giải Q(ξ) = K bằng chia đôi trên ln) rồi tiến tới đó theo hàm mũ với hằng số thời gian Tau.
    /// Mọi mảng cấp sẵn, Buoc() không cấp phát.
    /// </summary>
    public sealed class HeCanBang
    {
        public string Id, Ten, PhuongTrinh, Nguon, XucTac = "";
        public ChatCanBang[] Chat = new ChatCanBang[0];
        public double DHKJ, KRef = 1, TRef = 298.15;
        public bool LaKp;
        public double[] C = new double[0];
        public double T = 298.15;
        /// <summary>Hằng số thời gian tiến về cân bằng (s) – xúc tác làm nhỏ đi, K không đổi.</summary>
        public double Tau = 1.6;
        public bool CoXucTac;

        public bool LaN2O4 => Id == "N2O4_2NO2";
        public bool CoKhi { get { foreach (var c in Chat) if (c.LaKhi) return true; return false; } }

        /// <summary>Δn khí = Σν(sản phẩm khí) − Σν(chất đầu khí).</summary>
        public int DeltaN
        {
            get
            {
                int d = 0;
                foreach (var c in Chat) if (c.LaKhi) d += c.SanPham ? c.HeSo : -c.HeSo;
                return d;
            }
        }

        /// <summary>Hằng số cân bằng nồng độ Kc ở T.</summary>
        public double Kc(double T_K)
        {
            double K = CanBangMoHinh.KcTheoT(T_K, DHKJ, TRef, KRef);
            return LaKp ? CanBangMoHinh.KcTuKp(K, T_K, DeltaN) : K;
        }

        public double K => Kc(T);

        public double Q()
        {
            double lnQ = LnQ(0);
            return Math.Exp(lnQ);
        }

        double LnQ(double xi)
        {
            double s = 0;
            for (int i = 0; i < Chat.Length; i++)
            {
                var ch = Chat[i];
                double c = C[i] + (ch.SanPham ? ch.HeSo : -ch.HeSo) * xi;
                if (c <= 0) return ch.SanPham ? double.NegativeInfinity : double.PositiveInfinity;
                s += (ch.SanPham ? ch.HeSo : -ch.HeSo) * Math.Log(c);
            }
            return s;
        }

        /// <summary>Độ tiến ξ (mol/L) từ trạng thái hiện tại tới cân bằng (âm = chuyển dịch nghịch).</summary>
        public double XiCanBang()
        {
            double lo = double.NegativeInfinity, hi = double.PositiveInfinity;
            for (int i = 0; i < Chat.Length; i++)
            {
                var ch = Chat[i];
                double gioiHan = C[i] / ch.HeSo;
                if (ch.SanPham) lo = Math.Max(lo, -gioiHan); else hi = Math.Min(hi, gioiHan);
            }
            if (double.IsInfinity(lo) || double.IsInfinity(hi) || hi <= lo) return 0;
            double lnK = Math.Log(K);
            for (int k = 0; k < 200; k++)
            {
                double mid = 0.5 * (lo + hi);
                if (mid == lo || mid == hi) break;
                if (LnQ(mid) < lnK) lo = mid; else hi = mid;
            }
            return 0.5 * (lo + hi);
        }

        void Tien(double xi)
        {
            for (int i = 0; i < Chat.Length; i++)
                C[i] = Math.Max(0, C[i] + (Chat[i].SanPham ? Chat[i].HeSo : -Chat[i].HeSo) * xi);
        }

        /// <summary>Đặt ngay về cân bằng.</summary>
        public void DenCanBang() => Tien(XiCanBang());

        /// <summary>Bước thời gian dt (s): tiến một phần (1 − e^(−dt/τ)) quãng còn lại tới cân bằng. Trả về ξ vừa đi.</summary>
        public double Buoc(double dt)
        {
            double tau = CoXucTac ? Tau / 5 : Tau;
            double xi = XiCanBang() * (1 - Math.Exp(-dt / tau));
            Tien(xi);
            return xi;
        }

        /// <summary>Đổi thể tích từ V1 sang V2 (nồng độ nhân V1/V2): nén khí hoặc pha loãng dung dịch.</summary>
        public void DoiTheTich(double V1, double V2)
        {
            double s = V1 / V2;
            for (int i = 0; i < C.Length; i++) C[i] *= s;
        }

        /// <summary>Tỉ lệ v_t / v_n = K / Q (từ định luật tác dụng khối lượng với k_n = k_t / K).</summary>
        public double TiLeVtVn() => K / Q();

        /// <summary>Màu hỗn hợp (r,g,b,a) pha theo nồng độ × độ đậm của các chất có màu.</summary>
        public void MauHonHop(float[] rgba, double nongDoDam = 0.05)
        {
            double r = 0, g = 0, b = 0, w = 0;
            for (int i = 0; i < Chat.Length; i++)
            {
                double ww = C[i] * Chat[i].DoDam;
                r += Chat[i].Mau[0] * ww; g += Chat[i].Mau[1] * ww; b += Chat[i].Mau[2] * ww; w += ww;
            }
            if (w <= 0) { rgba[0] = rgba[1] = rgba[2] = 1; rgba[3] = 0; return; }
            rgba[0] = (float)(r / w); rgba[1] = (float)(g / w); rgba[2] = (float)(b / w);
            rgba[3] = (float)Math.Min(1, w / nongDoDam);
        }

        public static HeCanBang TuJson(Dictionary<string, object> d)
        {
            var he = new HeCanBang
            {
                Id = JsonHoaLy.S(d, "id"), Ten = JsonHoaLy.S(d, "ten"), PhuongTrinh = JsonHoaLy.S(d, "phuong_trinh"),
                Nguon = JsonHoaLy.S(d, "nguon"), XucTac = JsonHoaLy.S(d, "xuc_tac"),
                DHKJ = JsonHoaLy.F(d, "dH_kJ"), KRef = JsonHoaLy.F(d, "K_ref", 1), TRef = JsonHoaLy.F(d, "T_ref", 298.15),
                LaKp = JsonHoaLy.S(d, "loai_K") == "Kp",
            };
            var ds = new List<ChatCanBang>();
            foreach (var o in JsonHoaLy.L(JsonHoaLy.Lay(d, "chat")))
            {
                var c = JsonHoaLy.D(o);
                var ch = new ChatCanBang
                {
                    CongThuc = JsonHoaLy.S(c, "ct"), Ten = JsonHoaLy.S(c, "ten"), Pha = JsonHoaLy.S(c, "pha", "k"),
                    HeSo = (int)Math.Round(JsonHoaLy.F(c, "he_so", 1)), SanPham = JsonHoaLy.S(c, "vai_tro") == "san_pham",
                };
                var rgb = JsonHoaLy.L(JsonHoaLy.Lay(c, "mau_rgb"));
                if (rgb.Count == 3) ch.Mau = new[] { (float)((double)rgb[0] / 255), (float)((double)rgb[1] / 255), (float)((double)rgb[2] / 255) };
                else ch.Mau = MauHex.Doc(JsonHoaLy.S(c, "mau")) ?? ch.Mau;
                // độ đậm: theo JSON; chất không ghi thì coi là không màu, riêng phức [Fe(SCN)]²⁺ và Fe³⁺ có màu trong SGK
                ch.DoDam = JsonHoaLy.F(c, "do_dam", ch.CongThuc == "Fe3+" ? 0.25 : 0);
                ds.Add(ch);
            }
            he.Chat = ds.ToArray();
            he.C = new double[he.Chat.Length];
            return he;
        }
    }
}
