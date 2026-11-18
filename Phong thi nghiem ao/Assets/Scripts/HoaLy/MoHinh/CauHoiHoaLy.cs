// Câu hỏi nhanh của 4 mô phỏng Hóa lý (đọc cau-hoi.json của CHEMISTRY, ba dạng theo đề thi mới):
//   I  – trắc nghiệm 4 lựa chọn, II – đúng/sai từng ý, III – trả lời ngắn (số).
// Câu dạng III có "tinh_bang" → TinhBang() tính lại bằng chính mô hình C# để kiểm chứng đáp số. C# thuần, không dùng UnityEngine.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Khkt.HoaLy
{
    public enum DangCauHoi { TracNghiem, DungSai, TraLoiNgan }

    public sealed class YDungSai
    {
        public string NoiDung;
        public bool Dung;
    }

    public sealed class CauHoi
    {
        public string Id, TieuDe, NoiDung, GiaiThich, Nguon = "";
        public DangCauHoi Dang;
        public string[] LuaChon = new string[0];
        /// <summary>Dạng I: chữ cái đáp án ("A".."D").</summary>
        public string DapAn = "";
        public YDungSai[] Y = new YDungSai[0];
        /// <summary>Dạng III: đáp số dạng chữ, dấu phẩy thập phân ("12,47").</summary>
        public string DapSo = "";
        public Dictionary<string, object> TinhBang;

        public int ChiSoDapAn => string.IsNullOrEmpty(DapAn) ? -1 : char.ToUpperInvariant(DapAn[0]) - 'A';
    }

    public static class CauHoiHoaLy
    {
        public static List<CauHoi> Doc(string json)
        {
            var ds = new List<CauHoi>();
            foreach (var o in JsonHoaLy.L(JsonHoaLy.Lay(JsonHoaLy.D(JsonHoaLy.Doc(json)), "danh_sach")))
            {
                var d = JsonHoaLy.D(o);
                string dang = JsonHoaLy.S(d, "dang", "I");
                var c = new CauHoi
                {
                    Id = JsonHoaLy.S(d, "id"), TieuDe = JsonHoaLy.S(d, "tieu_de"), NoiDung = JsonHoaLy.S(d, "cau_hoi"),
                    GiaiThich = JsonHoaLy.S(d, "giai_thich"), Nguon = JsonHoaLy.S(d, "nguon"),
                    Dang = dang == "II" ? DangCauHoi.DungSai : dang == "III" ? DangCauHoi.TraLoiNgan : DangCauHoi.TracNghiem,
                    DapAn = JsonHoaLy.S(d, "dap_an"), DapSo = JsonHoaLy.S(d, "dap_so"),
                    TinhBang = JsonHoaLy.Lay(d, "tinh_bang") as Dictionary<string, object>,
                };
                var lc = new List<string>();
                foreach (var x in JsonHoaLy.L(JsonHoaLy.Lay(d, "lua_chon"))) lc.Add(x as string ?? "");
                c.LuaChon = lc.ToArray();
                var ys = new List<YDungSai>();
                foreach (var x in JsonHoaLy.L(JsonHoaLy.Lay(d, "y")))
                {
                    var yd = JsonHoaLy.D(x);
                    ys.Add(new YDungSai { NoiDung = JsonHoaLy.S(yd, "noi_dung"), Dung = JsonHoaLy.Lay(yd, "dung") is bool b && b });
                }
                c.Y = ys.ToArray();
                ds.Add(c);
            }
            return ds;
        }

        /// <summary>Đọc số kiểu Việt Nam ("12,47", "1.729", "3190,4") hoặc kiểu quốc tế.</summary>
        public static bool DocSo(string s, out double x)
        {
            x = double.NaN;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim().Replace(" ", "").Replace(" ", "");
            if (s.Contains(",")) s = s.Replace(".", "").Replace(',', '.');
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out x);
        }

        /// <summary>Số chữ số sau dấu phẩy của đáp số mẫu.</summary>
        public static int SoChuSoThapPhan(string dapSo)
        {
            int i = dapSo.IndexOf(',');
            return i < 0 ? 0 : dapSo.Length - i - 1;
        }

        /// <summary>Trả lời ngắn đúng khi lệch không quá nửa đơn vị ở chữ số cuối của đáp số (hoặc 0,1 % nếu lớn hơn).</summary>
        public static bool KiemTraTraLoiNgan(string nhap, string dapSo)
        {
            if (!DocSo(nhap, out double a) || !DocSo(dapSo, out double b)) return false;
            double dungSai = Math.Max(0.5 * Math.Pow(10, -SoChuSoThapPhan(dapSo)), Math.Abs(b) * 1e-3);
            return Math.Abs(a - b) <= dungSai + 1e-12;
        }

        static double P(Dictionary<string, object> d, string k, double macDinh) => JsonHoaLy.F(d, k, macDinh);

        /// <summary>
        /// Tính lại "tinh_bang" của câu dạng III bằng mô hình C#. Trả về NaN nếu không biết hàm.
        /// </summary>
        public static double TinhBang(Dictionary<string, object> tb)
        {
            if (tb == null) return double.NaN;
            string ham = JsonHoaLy.S(tb, "ham"), truong = JsonHoaLy.S(tb, "truong");
            var p = JsonHoaLy.D(JsonHoaLy.Lay(tb, "tham_so"));
            switch (ham)
            {
                case "theTich": return KhiLyTuongMoHinh.TheTich(P(p, "n", 1), P(p, "p_bar", P(p, "p", 1)), P(p, "T", 298.15));
                case "apSuat": return KhiLyTuongMoHinh.ApSuat(P(p, "n", 1), P(p, "V_L", P(p, "V", 24.79)), P(p, "T", 298.15));
                case "vRms": return KhiLyTuongMoHinh.VRms(P(p, "M_g_mol", 28.013), P(p, "T", 298.15));
                case "quaTrinh":
                {
                    var tu = JsonHoaLy.D(JsonHoaLy.Lay(p, "tu"));
                    var den = JsonHoaLy.D(JsonHoaLy.Lay(p, "den"));
                    string giu = JsonHoaLy.S(p, "giu");
                    var kieu = giu == "T" || giu == "dang-nhiet" ? KieuQuaTrinh.DangNhiet : giu == "p" || giu == "dang-ap" ? KieuQuaTrinh.DangAp
                             : giu == "V" || giu == "dang-tich" ? KieuQuaTrinh.DangTich : giu == "doan-nhiet" ? KieuQuaTrinh.DoanNhiet : KieuQuaTrinh.TuDo;
                    var kq = KhiLyTuongMoHinh.QuaTrinh(P(tu, "n", 1), P(tu, "T", 298.15), P(tu, "V", 24.79), kieu,
                        JsonHoaLy.Co(den, "V") ? P(den, "V", 0) : (double?)null, JsonHoaLy.Co(den, "T") ? P(den, "T", 0) : (double?)null,
                        JsonHoaLy.Co(den, "p") ? P(den, "p", 0) : (double?)null, null, P(tu, "gamma", 1.4));
                    switch (truong)
                    {
                        case "A_sinh": return kq.ASinh;
                        case "A": return kq.A;
                        case "Q": return kq.Q;
                        case "dU": return kq.DU;
                        case "T2": return kq.T2;
                        case "V2": return kq.V2;
                        case "p2": return kq.P2;
                    }
                    return double.NaN;
                }
                case "tiLeTocDo": return TocDoMoHinh.TiLeTocDo(P(p, "Ea_kJ", 50), P(p, "T1", 298.15), P(p, "T2", 308.15));
                case "thoiGianBanHuyBac1":
                    // đề cho t½ hỏi k (k = ln2 / t½), hoặc cho k hỏi t½
                    if (JsonHoaLy.Co(p, "t_half")) return Math.Log(2) / P(p, "t_half", 1);
                    return TocDoMoHinh.ThoiGianBanHuyBac1(P(p, "k", 0.01));
                case "heSoTangXucTac": return TocDoMoHinh.HeSoTangXucTac(P(p, "deltaEa_kJ", 20), P(p, "T", 298.15));
                case "kTheoArrhenius": return TocDoMoHinh.KTheoArrhenius(P(p, "A", 1e10), P(p, "Ea_kJ", 50), P(p, "T", 298.15));
                case "kpTuKc": return CanBangMoHinh.KpTuKc(P(p, "Kc", 0.00461), P(p, "T", 298.15), P(p, "delta_n", 1));
                case "kcTheoT": return CanBangMoHinh.KcTheoT(P(p, "T", 298.15), P(p, "deltaH_kJ", 57.2), P(p, "T_ref", 298.15), P(p, "Kc_ref", 0.00461));
                case "canBangPhanLi":
                {
                    var kq = CanBangMoHinh.CanBangPhanLi(P(p, "C0", 0.04), P(p, "cB0", 0), P(p, "Kc", 0.00461));
                    switch (truong)
                    {
                        case "cA": return kq.CA;
                        case "cB": return kq.CB;
                        case "x": return kq.X;
                        case "alpha": return kq.Alpha;
                    }
                    return double.NaN;
                }
                case "otto":
                case "diesel":
                {
                    var ts = ThamSoChuTrinh.MacDinh;
                    ts.Kieu = ham == "diesel" ? KieuChuTrinh.Diesel : KieuChuTrinh.Otto;
                    ts.R = P(p, "r", ham == "diesel" ? 18 : 10); ts.Rc = P(p, "rc", 2); ts.Gamma = P(p, "gamma", 1.4);
                    ts.T1 = P(p, "T1", 300); ts.P1 = P(p, "p1", 100); ts.QIn = P(p, "q_in", 1500);
                    var kq = DongCoNhietMoHinh.Tinh(ts);
                    switch (truong)
                    {
                        case "T2": return kq.T2;
                        case "T3": return kq.T3;
                        case "T4": return kq.T4;
                        case "p2": return kq.P2;
                        case "p3": return kq.P3;
                        case "w": return kq.W;
                        case "q_out": return kq.QOut;
                        case "eta": return kq.Eta;
                        case "eta_phan_tram": return kq.EtaPhanTram;
                        case "MEP": return kq.MEP;
                    }
                    return double.NaN;
                }
            }
            return double.NaN;
        }

        /// <summary>Làm tròn như JS rồi viết dấu phẩy thập phân: (12.4719, 2) → "12,47".</summary>
        public static string LamTron(double x, int soLe)
        {
            double r = Math.Round(x, soLe, MidpointRounding.AwayFromZero);
            return r.ToString("F" + Math.Max(0, soLe), CultureInfo.InvariantCulture).Replace('.', ',');
        }
    }
}
