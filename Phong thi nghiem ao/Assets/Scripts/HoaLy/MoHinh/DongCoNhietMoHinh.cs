// Mô hình động cơ nhiệt: chu trình Otto, Diesel lý tưởng, nhiệt trị nhiên liệu, hiệu suất Carnot –
// dịch từ CHEMISTRY frontend/hoa-hoc/hoa-ly/dong-co-nhiet/nhiet-dong.js. C# thuần, không dùng UnityEngine.
// Thêm: dựng đường p–v của chu trình và trạng thái theo góc quay trục khuỷu 0–720° (4 kỳ) cho phần vẽ.
using System;
using System.Collections.Generic;

namespace Khkt.HoaLy
{
    public enum KieuChuTrinh { Otto, Diesel }

    public sealed class KetQuaChuTrinh
    {
        public KieuChuTrinh Kieu;
        public double R, Rc = 1, Gamma, Cv, Cp, RKhi;
        public double T1, P1, T2, P2, T3, P3, T4, P4;   // K, kPa
        public double QIn, QOut, W;                      // kJ/kg
        public double Eta, EtaPhanTram, EtaCarnot, MEP;  // MEP kPa
        public double V1, V2, V3;                        // m³/kg
        public double DeltaSChay;
        public double MKg, WJ, PKW;
    }

    public sealed class NhienLieu
    {
        public string Id, Ten, CongThuc, MoTa;
        public double C, H, O, M, DeltaFH;
    }

    public sealed class KetQuaNhienLieu
    {
        public double LHV_MJ_kg, HHV_MJ_kg, AFR, QIn, DeltaCH_LHV, DeltaCH_HHV;
        public string PhuongTrinh;
    }

    public struct ThamSoChuTrinh
    {
        public KieuChuTrinh Kieu;
        public double R, Rc, Gamma, T1, P1, QIn, N, B, S;

        public static ThamSoChuTrinh MacDinh => new ThamSoChuTrinh
        { Kieu = KieuChuTrinh.Otto, R = 10, Rc = 2, Gamma = 1.4, T1 = 300, P1 = 100, QIn = 1500, N = 1200, B = 80, S = 80 };
    }

    public static class DongCoNhietMoHinh
    {
        /// <summary>Hằng số khí riêng của không khí, kJ/(kg·K).</summary>
        public const double R = 0.287;

        public static KetQuaChuTrinh Tinh(ThamSoChuTrinh ts) => ts.Kieu == KieuChuTrinh.Diesel ? Diesel(ts) : Otto(ts);

        public static KetQuaChuTrinh Otto(ThamSoChuTrinh ts)
        {
            double r = ts.R, g = ts.Gamma, T1 = ts.T1, p1 = ts.P1, qIn = ts.QIn;
            double cv = R / (g - 1), cp = g * cv;
            double T2 = T1 * Math.Pow(r, g - 1), p2 = p1 * Math.Pow(r, g);
            double T3 = T2 + qIn / cv, p3 = p2 * (T3 / T2);
            double T4 = T3 * Math.Pow(r, 1 - g), p4 = p1 * (T4 / T1);
            double qOut = cv * (T4 - T1), w = qIn - qOut;
            double eta = 1 - Math.Pow(r, 1 - g);
            double v1 = R * T1 / p1, v2 = v1 / r;
            var kq = new KetQuaChuTrinh
            {
                Kieu = KieuChuTrinh.Otto, R = r, Gamma = g, Cv = cv, Cp = cp, RKhi = R,
                T1 = T1, P1 = p1, T2 = T2, P2 = p2, T3 = T3, P3 = p3, T4 = T4, P4 = p4,
                QIn = qIn, QOut = qOut, W = w, Eta = eta, EtaPhanTram = eta * 100, EtaCarnot = 1 - T1 / T3,
                MEP = w / (v1 - v2), V1 = v1, V2 = v2, V3 = v2, DeltaSChay = cv * Math.Log(T3 / T2),
            };
            CongSuat(kq, ts);
            return kq;
        }

        public static KetQuaChuTrinh Diesel(ThamSoChuTrinh ts)
        {
            double r = ts.R, rc = ts.Rc, g = ts.Gamma, T1 = ts.T1, p1 = ts.P1;
            double cv = R / (g - 1), cp = g * cv;
            double T2 = T1 * Math.Pow(r, g - 1), p2 = p1 * Math.Pow(r, g);
            double p3 = p2, T3 = T2 * rc;
            double qIn = cp * (T3 - T2);
            double T4 = T3 * Math.Pow(rc / r, g - 1), p4 = p3 * Math.Pow(rc / r, g);
            double qOut = cv * (T4 - T1), w = qIn - qOut;
            double eta = 1 - Math.Pow(r, 1 - g) * ((Math.Pow(rc, g) - 1) / (g * (rc - 1)));
            double v1 = R * T1 / p1, v2 = v1 / r;
            var kq = new KetQuaChuTrinh
            {
                Kieu = KieuChuTrinh.Diesel, R = r, Rc = rc, Gamma = g, Cv = cv, Cp = cp, RKhi = R,
                T1 = T1, P1 = p1, T2 = T2, P2 = p2, T3 = T3, P3 = p3, T4 = T4, P4 = p4,
                QIn = qIn, QOut = qOut, W = w, Eta = eta, EtaPhanTram = eta * 100, EtaCarnot = 1 - T1 / T3,
                MEP = w / (v1 - v2), V1 = v1, V2 = v2, V3 = rc * v2, DeltaSChay = cp * Math.Log(T3 / T2),
            };
            CongSuat(kq, ts);
            return kq;
        }

        static void CongSuat(KetQuaChuTrinh kq, ThamSoChuTrinh ts)
        {
            double B = ts.B > 0 ? ts.B : 80, S = ts.S > 0 ? ts.S : 80, n = ts.N > 0 ? ts.N : 1200;
            double Vd = Math.PI * B * B / 4 * S * 1e-9;           // m³
            double V1m3 = Vd * kq.R / (kq.R - 1);
            kq.MKg = kq.P1 * V1m3 / (R * kq.T1);
            kq.WJ = kq.MKg * kq.W * 1000;
            kq.PKW = kq.WJ * (n / 2) / 60 / 1000;                 // máy 4 kỳ: 1 chu trình / 2 vòng
        }

        public static double HieuSuatCarnot(double TLanh, double TNong) => 1 - TLanh / TNong;
        public static double HieuSuatOtto(double r, double gamma) => 1 - 1 / Math.Pow(r, gamma - 1);

        /// <summary>Phản ứng cháy CxHyOz trong không khí (O₂ + 3,76 N₂), nhiệt trị LHV/HHV, AFR và nhiệt cấp q_in cho 1 kg môi chất.</summary>
        public static KetQuaNhienLieu TinhNhienLieu(NhienLieu nl, double lambda = 1.0, double k = 0.55)
        {
            double nCO2 = nl.C, nH2O = nl.H / 2, nO2 = nl.C + nl.H / 4 - nl.O / 2, nN2 = 3.76 * nO2;
            double dLHV = nCO2 * -393.5 + nH2O * -241.8 - nl.DeltaFH;
            double dHHV = nCO2 * -393.5 + nH2O * -285.8 - nl.DeltaFH;
            double M = nl.M > 0 ? nl.M : 1;
            var kq = new KetQuaNhienLieu
            {
                DeltaCH_LHV = dLHV, DeltaCH_HHV = dHHV,
                LHV_MJ_kg = Math.Abs(dLHV) / M, HHV_MJ_kg = Math.Abs(dHHV) / M,
                AFR = (nO2 * 32.0 + nN2 * 28.0) / M,
            };
            kq.QIn = k * (kq.LHV_MJ_kg * 1000) * Math.Min(1, lambda) / (lambda * kq.AFR + 1);
            string So(double x) => Math.Abs(x % 1) < 1e-9 ? ((int)Math.Round(x)).ToString() : x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
            kq.PhuongTrinh = nl.CongThuc + " + " + So(nO2) + " (O₂ + 3,76 N₂) → " + (nCO2 > 0 ? So(nCO2) + " CO₂ + " : "") +
                             (nH2O > 0 ? So(nH2O) + " H₂O + " : "") + So(nN2) + " N₂";
            return kq;
        }

        public static List<NhienLieu> DocNhienLieu(string json)
        {
            var ds = new List<NhienLieu>();
            foreach (var o in JsonHoaLy.L(JsonHoaLy.Lay(JsonHoaLy.D(JsonHoaLy.Doc(json)), "danh_sach")))
            {
                var d = JsonHoaLy.D(o);
                ds.Add(new NhienLieu
                {
                    Id = JsonHoaLy.S(d, "id"), Ten = JsonHoaLy.S(d, "ten"), CongThuc = JsonHoaLy.S(d, "cong_thuc"), MoTa = JsonHoaLy.S(d, "mo_ta"),
                    C = JsonHoaLy.F(d, "C"), H = JsonHoaLy.F(d, "H"), O = JsonHoaLy.F(d, "O"), M = JsonHoaLy.F(d, "M", 1), DeltaFH = JsonHoaLy.F(d, "delta_f_H"),
                });
            }
            return ds;
        }

        /// <summary>
        /// Dựng đường p–v kín của chu trình (1→2 nén đoạn nhiệt, 2→3 cấp nhiệt, 3→4 giãn đoạn nhiệt, 4→1 thải nhiệt đẳng tích)
        /// vào mảng cấp sẵn v[], p[] (soMoiDoan điểm cho mỗi đoạn cong). Trả về số điểm đã ghi.
        /// </summary>
        public static int DuongPV(KetQuaChuTrinh kq, float[] v, float[] p, int soMoiDoan)
        {
            int n = 0;
            double g = kq.Gamma;
            void Ghi(double vv, double pp) { if (n < v.Length) { v[n] = (float)vv; p[n] = (float)pp; n++; } }
            for (int i = 0; i <= soMoiDoan; i++)       // 1 → 2
            {
                double vv = kq.V1 + (kq.V2 - kq.V1) * i / soMoiDoan;
                Ghi(vv, kq.P1 * Math.Pow(kq.V1 / vv, g));
            }
            if (kq.Kieu == KieuChuTrinh.Diesel) Ghi(kq.V3, kq.P3);   // 2 → 3 đẳng áp
            else Ghi(kq.V2, kq.P3);                                  // 2 → 3 đẳng tích
            for (int i = 1; i <= soMoiDoan; i++)       // 3 → 4
            {
                double vv = kq.V3 + (kq.V1 - kq.V3) * i / soMoiDoan;
                Ghi(vv, kq.P3 * Math.Pow(kq.V3 / vv, g));
            }
            Ghi(kq.V1, kq.P1);                         // 4 → 1
            return n;
        }

        /// <summary>Thể tích riêng theo góc trục khuỷu φ (độ, 0 = điểm chết trên): v = v2 + (v1 − v2)·(1 − cos φ)/2.</summary>
        public static double TheTichTheoGoc(KetQuaChuTrinh kq, double phi) =>
            kq.V2 + (kq.V1 - kq.V2) * (1 - Math.Cos(phi * Math.PI / 180)) / 2;

        /// <summary>
        /// Trạng thái động cơ 4 kỳ theo góc φ ∈ [0, 720): kỳ (0 nạp, 1 nén, 2 nổ – sinh công, 3 xả), v (m³/kg), p (kPa), T (K).
        /// Nạp và xả ở p1; xả mở van ở 540° (p rơi đẳng tích về p1).
        /// </summary>
        public static int TrangThaiTheoGoc(KetQuaChuTrinh kq, double phi, out double v, out double p, out double T)
        {
            phi %= 720; if (phi < 0) phi += 720;
            v = TheTichTheoGoc(kq, phi);
            int ky = (int)(phi / 180);
            double g = kq.Gamma;
            switch (ky)
            {
                case 0: p = kq.P1; break;
                case 1: p = kq.P1 * Math.Pow(kq.V1 / v, g); break;
                case 2:
                    if (kq.Kieu == KieuChuTrinh.Diesel && v <= kq.V3) p = kq.P3;
                    else p = kq.P3 * Math.Pow(kq.V3 / v, g);
                    break;
                default: p = kq.P1; break;
            }
            T = p * v / R;
            return ky;
        }

        /// <summary>Công chu trình tính số ∮p dv (kJ/kg) theo góc 180°→540° (nạp và xả triệt tiêu nhau).</summary>
        public static double CongTichPhanSo(KetQuaChuTrinh kq, int buoc = 20000)
        {
            double tong = 0, vTruoc, pTruoc;
            TrangThaiTheoGoc(kq, 180, out vTruoc, out pTruoc, out _);
            for (int i = 1; i <= buoc; i++)
            {
                double phi = 180 + 360.0 * i / buoc;
                if (phi >= 540) phi = 539.999999;
                TrangThaiTheoGoc(kq, phi, out double v, out double p, out _);
                tong += 0.5 * (p + pTruoc) * (v - vTruoc);
                vTruoc = v; pTruoc = p;
            }
            return tong;
        }

        public static readonly string[] TenKy = { "Kỳ 1 · Nạp", "Kỳ 2 · Nén", "Kỳ 3 · Nổ – sinh công", "Kỳ 4 · Xả" };
    }
}
