// Mô hình khí lý tưởng và khí thực Van der Waals – dịch từ CHEMISTRY frontend/hoa-hoc/hoa-ly/khi-ly-tuong/mo-hinh.js.
// C# thuần, không dùng UnityEngine. Đơn vị giữ như bản JS: p (bar), V (L), T (K), M (g/mol), năng lượng (J).
using System;

namespace Khkt.HoaLy
{
    public struct KetQuaVanDerWaals
    {
        public double PLyTuong, PVdw, Z;
    }

    public enum KieuQuaTrinh { DangNhiet, DangAp, DangTich, DoanNhiet, TuDo }

    public struct KetQuaQuaTrinh
    {
        public double N, P1, V1, T1, P2, V2, T2, Q, A, ASinh, DU;
    }

    public sealed class LoaiKhi
    {
        public string Id, Ten, CongThuc;
        public double M, Gamma, A, B;
        public float R = 0.6f, G = 0.8f, Bl = 1f;   // màu hiển thị (0..1)
    }

    public static class KhiLyTuongMoHinh
    {
        public const double R_SI = 8.314462618;     // J/(mol·K)
        public const double R_BAR = 0.08314462618;  // bar·L/(mol·K)
        public const double K_B = 1.380649e-23;     // J/K
        public const double N_A = 6.02214076e23;    // 1/mol

        public static double ApSuat(double n, double V_L = 24.79, double T_K = 298.15) => n * R_BAR * T_K / V_L;
        public static double TheTich(double n, double p_bar = 1, double T_K = 298.15) => n * R_BAR * T_K / p_bar;

        public static double VRms(double M_g_mol = 28.013, double T_K = 298.15) => Math.Sqrt(3 * R_SI * T_K / (M_g_mol * 1e-3));
        public static double VP(double M_g_mol = 28.013, double T_K = 298.15) => Math.Sqrt(2 * R_SI * T_K / (M_g_mol * 1e-3));
        public static double VTb(double M_g_mol = 28.013, double T_K = 298.15) => Math.Sqrt(8 * R_SI * T_K / (Math.PI * M_g_mol * 1e-3));
        public static double DongNangTb(double T_K = 298.15) => 1.5 * K_B * T_K;

        public static double NoiNang(double n = 1, double T_K = 298.15, double gamma = 1.4) => n * (R_SI / (gamma - 1)) * T_K;

        public static KetQuaVanDerWaals VanDerWaals(double n = 1, double V_L = 1, double T_K = 300, double a = 3.640, double b = 0.04267)
        {
            double pLt = n * R_BAR * T_K / V_L;
            double vEff = V_L - n * b;
            double pVdw = vEff > 0 ? n * R_BAR * T_K / vEff - a * Math.Pow(n / V_L, 2) : double.PositiveInfinity;
            return new KetQuaVanDerWaals { PLyTuong = pLt, PVdw = pVdw, Z = pVdw * V_L / (n * R_BAR * T_K) };
        }

        /// <summary>
        /// quaTrinh(tuTrangThai, denGiaTri, giu) của bản JS. Trạng thái đầu (n, T1, V1, p1 tùy chọn), đích cho bằng V2 hoặc T2 hoặc p2.
        /// A là công khí NHẬN (quy ước SGK: ΔU = A + Q), ASinh = −A.
        /// </summary>
        public static KetQuaQuaTrinh QuaTrinh(double n, double T1, double V1, KieuQuaTrinh giu, double? V2Dich = null, double? T2Dich = null,
                                             double? p2Dich = null, double? p1 = null, double gamma = 1.4)
        {
            double P1 = p1 ?? n * R_BAR * T1 / V1;
            double Cv = R_SI / (gamma - 1);
            double T2 = T1, V2 = V1, P2 = P1, Q = 0, A = 0, dU = 0;
            switch (giu)
            {
                case KieuQuaTrinh.DangNhiet:
                    if (V2Dich.HasValue) { V2 = V2Dich.Value; P2 = n * R_BAR * T2 / V2; }
                    else if (p2Dich.HasValue) { P2 = p2Dich.Value; V2 = n * R_BAR * T2 / P2; }
                    {
                        double aSinh = n * R_SI * T1 * Math.Log(V2 / V1);
                        A = -aSinh; dU = 0; Q = aSinh;
                    }
                    break;
                case KieuQuaTrinh.DangAp:
                    P2 = P1;
                    if (T2Dich.HasValue) { T2 = T2Dich.Value; V2 = n * R_BAR * T2 / P2; }
                    else if (V2Dich.HasValue) { V2 = V2Dich.Value; T2 = P2 * V2 / (n * R_BAR); }
                    A = -(P1 * 100 * (V2 - V1));          // 1 bar·L = 100 J
                    dU = n * Cv * (T2 - T1);
                    Q = dU - A;
                    break;
                case KieuQuaTrinh.DangTich:
                    if (T2Dich.HasValue) { T2 = T2Dich.Value; P2 = n * R_BAR * T2 / V2; }
                    else if (p2Dich.HasValue) { P2 = p2Dich.Value; T2 = P2 * V2 / (n * R_BAR); }
                    A = 0;
                    dU = n * Cv * (T2 - T1);
                    Q = dU;
                    break;
                case KieuQuaTrinh.DoanNhiet:
                    Q = 0;
                    if (V2Dich.HasValue)
                    {
                        V2 = V2Dich.Value;
                        T2 = T1 * Math.Pow(V1 / V2, gamma - 1);
                        P2 = P1 * Math.Pow(V1 / V2, gamma);
                    }
                    else if (T2Dich.HasValue)
                    {
                        T2 = T2Dich.Value;
                        V2 = V1 * Math.Pow(T1 / T2, 1 / (gamma - 1));
                        P2 = n * R_BAR * T2 / V2;
                    }
                    dU = n * Cv * (T2 - T1);
                    A = dU;
                    break;
                default:
                    if (V2Dich.HasValue) V2 = V2Dich.Value;
                    if (T2Dich.HasValue) T2 = T2Dich.Value;
                    P2 = p2Dich ?? n * R_BAR * T2 / V2;
                    dU = n * Cv * (T2 - T1);
                    A = -((P1 + P2) / 2 * 100 * (V2 - V1));
                    Q = dU - A;
                    break;
            }
            return new KetQuaQuaTrinh { N = n, P1 = P1, V1 = V1, T1 = T1, P2 = P2, V2 = V2, T2 = T2, Q = Q, A = A, ASinh = -A, DU = dU };
        }

        /// <summary>Mật độ xác suất Maxwell 3D f(v) (s/m).</summary>
        public static double PhanBoMaxwell(double v, double M_kg = 0.028013, double T_K = 298.15)
        {
            double a = M_kg / (2 * R_SI * T_K);
            return 4 * Math.PI * Math.Pow(a / Math.PI, 1.5) * v * v * Math.Exp(-a * v * v);
        }

        /// <summary>hopKhi: áp suất tổng và riêng phần (định luật Dalton). pRieng phải cùng độ dài với nMol.</summary>
        public static double HopKhi(double[] nMol, double V_L, double T_K, double[] phanMol, double[] pRieng)
        {
            double nTong = 0;
            for (int i = 0; i < nMol.Length; i++) nTong += nMol[i];
            double pTong = nTong * R_BAR * T_K / V_L;
            for (int i = 0; i < nMol.Length; i++)
            {
                double x = nTong > 0 ? nMol[i] / nTong : 0;
                if (phanMol != null) phanMol[i] = x;
                if (pRieng != null) pRieng[i] = x * pTong;
            }
            return pTong;
        }

        /// <summary>doKhongTuyetDoi: khớp V = m·t + c (định luật Charles), trả về t0 = −c/m (°C), NaN nếu không khớp được.</summary>
        public static double DoKhongTuyetDoi(double[] tC, double[] V, out double m, out double c)
        {
            m = c = double.NaN;
            if (tC == null || tC.Length < 2) return double.NaN;
            int N = tC.Length;
            double sx = 0, sy = 0, sxy = 0, sx2 = 0;
            for (int i = 0; i < N; i++) { sx += tC[i]; sy += V[i]; sxy += tC[i] * V[i]; sx2 += tC[i] * tC[i]; }
            double den = N * sx2 - sx * sx;
            if (Math.Abs(den) < 1e-12) return double.NaN;
            m = (N * sxy - sx * sy) / den;
            c = (sy * sx2 - sx * sxy) / den;
            return m != 0 ? -c / m : -273.15;
        }

        public static double DoanNhietT2(double T1, double V1, double V2, double gamma) => T1 * Math.Pow(V1 / V2, gamma - 1);
        public static double DoanNhietP2(double p1, double V1, double V2, double gamma) => p1 * Math.Pow(V1 / V2, gamma);
    }

    /// <summary>Bộ sinh số ngẫu nhiên xorshift32 có hạt giống (kết quả lặp lại được, không cấp phát).</summary>
    public struct NgauNhien
    {
        uint s;
        double duTru;
        bool coDuTru;

        public NgauNhien(uint hat) { s = hat == 0 ? 2463534242u : hat; duTru = 0; coDuTru = false; }

        public uint Uint()
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return s;
        }

        /// <summary>(0, 1]</summary>
        public double Don() => (Uint() + 1.0) / 4294967296.0;

        /// <summary>Phân phối chuẩn N(0,1) bằng Box–Muller.</summary>
        public double Chuan()
        {
            if (coDuTru) { coDuTru = false; return duTru; }
            double u = Don(), v = Don();
            double r = Math.Sqrt(-2 * Math.Log(u));
            duTru = r * Math.Sin(2 * Math.PI * v);
            coDuTru = true;
            return r * Math.Cos(2 * Math.PI * v);
        }
    }

    /// <summary>
    /// Hộp khí 2D cho phần vẽ hạt: mỗi hạt có vận tốc 3D thật (m/s) lấy mẫu theo Maxwell (mỗi thành phần ~ N(0, √(RT/M))),
    /// chạy trên mặt phẳng x–y, va chạm đàn hồi với thành (khí lý tưởng: bỏ qua va chạm giữa các hạt).
    /// Mảng cấp sẵn theo sức chứa; Buoc() không cấp phát.
    /// </summary>
    public sealed class HopKhi2D
    {
        public readonly int SucChua;
        public int SoHat { get; private set; }
        public readonly float[] X, Y;
        public readonly float[] VX, VY, VZ;
        /// <summary>Kích thước hộp (đơn vị vẽ). Cao cố định, Rong đổi theo thể tích (pít-tông bên phải).</summary>
        public float Rong = 1f, Cao = 1f;
        public double T { get; private set; } = 298.15;
        public double M { get; private set; } = 28.013;
        /// <summary>Tổng xung lượng (đơn vị vẽ·m/s) truyền cho pít-tông, và số va chạm, cộng dồn từ lần DatLaiDem() trước.</summary>
        public double XungPitTong, ThoiGianDem;
        public int VaChamPitTong;
        NgauNhien rng;

        public HopKhi2D(int sucChua, uint hat = 12345)
        {
            SucChua = Math.Max(1, sucChua);
            X = new float[SucChua]; Y = new float[SucChua];
            VX = new float[SucChua]; VY = new float[SucChua]; VZ = new float[SucChua];
            rng = new NgauNhien(hat);
        }

        double Sigma => Math.Sqrt(KhiLyTuongMoHinh.R_SI * T / (M * 1e-3));

        void LayMau(int i)
        {
            double s = Sigma;
            X[i] = (float)(rng.Don() * Rong);
            Y[i] = (float)(rng.Don() * Cao);
            VX[i] = (float)(rng.Chuan() * s);
            VY[i] = (float)(rng.Chuan() * s);
            VZ[i] = (float)(rng.Chuan() * s);
        }

        public void KhoiTao(int so, double M_g_mol, double T_K)
        {
            M = M_g_mol; T = T_K;
            SoHat = 0;
            DatSoHat(so);
        }

        /// <summary>Thêm hạt mới (lấy mẫu ở T hiện tại) hoặc bớt hạt cuối.</summary>
        public void DatSoHat(int so)
        {
            so = Math.Max(0, Math.Min(SucChua, so));
            for (int i = SoHat; i < so; i++) LayMau(i);
            SoHat = so;
        }

        /// <summary>Đổi nhiệt độ: nhân vận tốc với √(T2/T1) (giữ hình dạng phân bố).</summary>
        public void DatNhietDo(double T_K)
        {
            if (T_K <= 0 || Math.Abs(T_K - T) < 1e-9) return;
            float k = (float)Math.Sqrt(T_K / T);
            for (int i = 0; i < SucChua; i++) { VX[i] *= k; VY[i] *= k; VZ[i] *= k; }
            T = T_K;
        }

        /// <summary>Đổi loại khí cùng nhiệt độ: v ∝ 1/√M.</summary>
        public void DatKhoiLuongMol(double M_g_mol)
        {
            if (M_g_mol <= 0 || Math.Abs(M_g_mol - M) < 1e-9) return;
            float k = (float)Math.Sqrt(M / M_g_mol);
            for (int i = 0; i < SucChua; i++) { VX[i] *= k; VY[i] *= k; VZ[i] *= k; }
            M = M_g_mol;
        }

        /// <summary>Đổi bề rộng hộp (thể tích). Hạt nằm ngoài được đẩy vào trong.</summary>
        public void DatRong(float rong)
        {
            Rong = Math.Max(1e-3f, rong);
            for (int i = 0; i < SoHat; i++)
                if (X[i] > Rong) { X[i] = Rong - (X[i] - Rong) % Rong; if (VX[i] > 0) VX[i] = -VX[i]; }
        }

        /// <summary>Đi một bước dt (s thực của màn hình). tiLe: đơn vị vẽ trên mỗi (m/s·s).</summary>
        public void Buoc(float dt, float tiLe)
        {
            float k = dt * tiLe;
            for (int i = 0; i < SoHat; i++)
            {
                float x = X[i] + VX[i] * k, y = Y[i] + VY[i] * k;
                if (x < 0) { x = -x; VX[i] = -VX[i]; }
                else if (x > Rong)
                {
                    x = 2 * Rong - x;
                    XungPitTong += 2 * Math.Abs(VX[i]);
                    VaChamPitTong++;
                    VX[i] = -VX[i];
                }
                if (y < 0) { y = -y; VY[i] = -VY[i]; }
                else if (y > Cao) { y = 2 * Cao - y; VY[i] = -VY[i]; }
                // bước quá dài (hiếm) thì kẹp lại trong hộp
                X[i] = x < 0 ? 0 : (x > Rong ? Rong : x);
                Y[i] = y < 0 ? 0 : (y > Cao ? Cao : y);
            }
            ThoiGianDem += dt;
        }

        public void DatLaiDem() { XungPitTong = 0; VaChamPitTong = 0; ThoiGianDem = 0; }

        public double TocDo(int i) => Math.Sqrt((double)VX[i] * VX[i] + (double)VY[i] * VY[i] + (double)VZ[i] * VZ[i]);

        /// <summary>v_rms đo trực tiếp trên các hạt (3D).</summary>
        public double VRmsDo()
        {
            if (SoHat == 0) return 0;
            double s = 0;
            for (int i = 0; i < SoHat; i++) s += (double)VX[i] * VX[i] + (double)VY[i] * VY[i] + (double)VZ[i] * VZ[i];
            return Math.Sqrt(s / SoHat);
        }

        /// <summary>Đếm số hạt theo khoảng tốc độ [0, vMax) chia đều cho ngan.Length ngăn.</summary>
        public void BieuDoTocDo(int[] ngan, double vMax)
        {
            Array.Clear(ngan, 0, ngan.Length);
            double k = ngan.Length / vMax;
            for (int i = 0; i < SoHat; i++)
            {
                int j = (int)(TocDo(i) * k);
                if (j >= 0 && j < ngan.Length) ngan[j]++;
            }
        }
    }
}
