// Kiểm thử đơn vị C# thuần cho 4 mô hình Hóa lý (không phụ thuộc UnityEngine) – theo kiểu ThucVatSimTests.
// Gọi: HoaLySimTests.RunAll(out p, out f, out report, jsonCauHoi) – jsonCauHoi (tùy chọn) là nội dung 4 tệp cau-hoi.json
// để tính lại đáp số mọi câu dạng III bằng mô hình C#.
using System;
using System.Collections.Generic;
using System.Text;

namespace Khkt.HoaLy
{
    public static class HoaLySimTests
    {
        public static bool RunAll(out int passed, out int failed, out string report, IList<string> jsonCauHoi = null,
                                  string jsonKhi = null, string jsonPhanUng = null, string jsonHeCanBang = null, string jsonNhienLieu = null)
        {
            int p = 0, f = 0;
            var sb = new StringBuilder();

            void Check(bool ok, string label, string detail = "")
            {
                if (ok) { p++; sb.AppendLine("  ok   " + label + (detail == "" ? "" : "  (" + detail + ")")); }
                else { f++; sb.AppendLine("  SAI  " + label + " (" + detail + ")"); }
            }
            bool Gan(double a, double b, double tuongDoi = 1e-9) => Math.Abs(a - b) <= tuongDoi * Math.Max(1e-300, Math.Max(Math.Abs(a), Math.Abs(b)));

            // ===== 1. Khí lý tưởng =====
            double V0 = KhiLyTuongMoHinh.TheTich(1, 1, 298.15);
            Check(Math.Abs(V0 - 24.79) < 0.005, "Thể tích mol ở đkc (298,15 K; 1 bar) ≈ 24,79 L", V0.ToString("0.0000"));
            double pp = KhiLyTuongMoHinh.ApSuat(2, 10, 400);
            Check(Gan(pp * 10, 2 * KhiLyTuongMoHinh.R_BAR * 400), "pV = nRT", pp.ToString("0.0000") + " bar");
            Check(Gan(KhiLyTuongMoHinh.ApSuat(1, 5, 300) * 5, KhiLyTuongMoHinh.ApSuat(1, 15, 300) * 15), "Boyle: p₁V₁ = p₂V₂ (đẳng nhiệt)");
            Check(Gan(KhiLyTuongMoHinh.TheTich(1, 1, 600) / KhiLyTuongMoHinh.TheTich(1, 1, 300), 2), "Charles: V tỉ lệ T (đẳng áp)");
            double vr = KhiLyTuongMoHinh.VRms(28.013, 300);
            Check(Math.Abs(vr - 516.8) < 0.05, "v_rms(N₂, 300 K) = 516,8 m/s", vr.ToString("0.00"));
            Check(KhiLyTuongMoHinh.VP() < KhiLyTuongMoHinh.VTb() && KhiLyTuongMoHinh.VTb() < KhiLyTuongMoHinh.VRms(), "v_p < v_tb < v_rms");
            Check(Gan(KhiLyTuongMoHinh.VRms(4.0026, 300) / KhiLyTuongMoHinh.VRms(31.999, 300), Math.Sqrt(31.999 / 4.0026)), "Graham: v ∝ 1/√M");
            var dn = KhiLyTuongMoHinh.QuaTrinh(1, 300, 10, KieuQuaTrinh.DangNhiet, V2Dich: 20);
            Check(Math.Abs(dn.ASinh - 1729) < 0.5 && dn.DU == 0 && Gan(dn.Q, dn.ASinh), "Giãn đẳng nhiệt 10→20 L: A_sinh = nRT ln2 ≈ 1729 J, ΔU = 0", dn.ASinh.ToString("0.0"));
            var da = KhiLyTuongMoHinh.QuaTrinh(1, 300, 10, KieuQuaTrinh.DangAp, T2Dich: 600);
            Check(Gan(da.V2, 20, 1e-9) && Gan(da.Q, 1 * (KhiLyTuongMoHinh.R_SI / 0.4 + KhiLyTuongMoHinh.R_SI) * 300, 1e-3), "Đẳng áp: V₂ = 2V₁, Q = nCpΔT", da.Q.ToString("0.0"));
            var dt = KhiLyTuongMoHinh.QuaTrinh(1, 300, 10, KieuQuaTrinh.DoanNhiet, V2Dich: 5);
            Check(dt.Q == 0 && dt.T2 > 300 && Gan(dt.P2 * Math.Pow(5, 1.4), dt.P1 * Math.Pow(10, 1.4)), "Nén đoạn nhiệt: Q = 0, T tăng, pV^γ = const", dt.T2.ToString("0.0") + " K");
            var vdw = KhiLyTuongMoHinh.VanDerWaals(1, 0.5, 300, 3.640, 0.04267);
            Check(vdw.Z < 1 && vdw.PVdw < vdw.PLyTuong, "CO₂ nén (0,5 L): Van der Waals Z < 1 (lực hút thắng)", "Z = " + vdw.Z.ToString("0.000"));
            var vdwHe = KhiLyTuongMoHinh.VanDerWaals(1, 0.1, 300, 0.0346, 0.0237);
            Check(vdwHe.Z > 1, "He rất nén (0,1 L): Z > 1 (thể tích riêng thắng)", "Z = " + vdwHe.Z.ToString("0.000"));
            // tích phân phân bố Maxwell ≈ 1 và trung bình v² = v_rms²
            double tp = 0, tv2 = 0, dv = 2;
            for (double v = dv / 2; v < 5000; v += dv) { double fx = KhiLyTuongMoHinh.PhanBoMaxwell(v, 0.028013, 300) * dv; tp += fx; tv2 += fx * v * v; }
            Check(Math.Abs(tp - 1) < 1e-4 && Math.Abs(Math.Sqrt(tv2) - vr) < 0.5, "Phân bố Maxwell chuẩn hóa, ⟨v²⟩ = v_rms²", tp.ToString("0.00000"));
            var pi = new double[2];
            double pt = KhiLyTuongMoHinh.HopKhi(new[] { 0.79, 0.21 }, 24.79, 298.15, null, pi);
            Check(Gan(pi[0] + pi[1], pt) && Math.Abs(pi[1] / pt - 0.21) < 1e-12, "Dalton: Σp_i = p, p_i = x_i·p");
            double t0 = KhiLyTuongMoHinh.DoKhongTuyetDoi(new double[] { 0, 25, 50, 100 },
                new[] { 22.414 * 273.15 / 273.15, 22.414 * 298.15 / 273.15, 22.414 * 323.15 / 273.15, 22.414 * 373.15 / 273.15 }, out _, out _);
            Check(Math.Abs(t0 + 273.15) < 1e-6, "Ngoại suy Charles ra độ không tuyệt đối −273,15 °C", t0.ToString("0.000"));

            // hộp hạt: v_rms đo trên hạt khớp công thức, đổi T thì nhân √(T2/T1), đổi số hạt không vượt sức chứa
            var hop = new HopKhi2D(3000, 7);
            hop.KhoiTao(3000, 28.013, 300);
            double vrDo = hop.VRmsDo();
            Check(Math.Abs(vrDo / vr - 1) < 0.03, "Hạt lấy mẫu Maxwell: v_rms đo ≈ công thức (±3 %)", vrDo.ToString("0.0") + " / " + vr.ToString("0.0"));
            hop.DatNhietDo(1200);
            Check(Math.Abs(hop.VRmsDo() / vrDo - 2) < 1e-4, "T ×4 → v_rms ×2");
            hop.DatKhoiLuongMol(4.0026);
            Check(Math.Abs(hop.VRmsDo() / KhiLyTuongMoHinh.VRms(4.0026, 1200) - 1) < 0.03, "Đổi sang He: v_rms theo M mới");
            hop.DatSoHat(5000);
            Check(hop.SoHat == 3000, "Số hạt không vượt sức chứa");
            hop.DatSoHat(500);
            hop.DatRong(0.4f);
            bool trongHop = true;
            for (int k = 0; k < 400; k++) hop.Buoc(1f / 60, 1f / 2000);
            for (int i = 0; i < hop.SoHat; i++) if (hop.X[i] < 0 || hop.X[i] > hop.Rong || hop.Y[i] < 0 || hop.Y[i] > hop.Cao) trongHop = false;
            Check(trongHop && hop.VaChamPitTong > 0, "Hạt luôn trong hộp, có va chạm pít-tông", hop.VaChamPitTong + " va chạm");
            // áp suất động học: số va chạm/giây tỉ lệ √T và 1/rộng (cùng số hạt)
            double DemVaCham(double T, float rong)
            {
                var h = new HopKhi2D(2000, 99);
                h.KhoiTao(2000, 28.013, T);
                h.DatRong(rong);
                for (int k = 0; k < 120; k++) h.Buoc(1f / 60, 1f / 1500);
                h.DatLaiDem();
                for (int k = 0; k < 600; k++) h.Buoc(1f / 60, 1f / 1500);
                return h.XungPitTong / h.ThoiGianDem;
            }
            double x1 = DemVaCham(300, 1f), x2 = DemVaCham(1200, 1f), x3 = DemVaCham(300, 0.5f);
            Check(Math.Abs(x2 / x1 - 4) < 0.4, "Xung lượng lên pít-tông ∝ T (p ∝ T khi V không đổi)", (x2 / x1).ToString("0.00"));
            Check(Math.Abs(x3 / x1 - 2) < 0.25, "Thu nửa thể tích → xung lượng ×2 (Boyle)", (x3 / x1).ToString("0.00"));

            // ===== 2. Tốc độ phản ứng =====
            double k298 = TocDoMoHinh.KTheoArrhenius(1e10, 50, 298.15), k308 = TocDoMoHinh.KTheoArrhenius(1e10, 50, 308.15);
            Check(Gan(k308 / k298, TocDoMoHinh.TiLeTocDo(50, 298.15, 308.15), 1e-12), "Arrhenius: k(T2)/k(T1) = exp(−Ea/R·(1/T2 − 1/T1))");
            Check(Math.Abs(TocDoMoHinh.TiLeTocDo(50, 298.15, 308.15) - 1.9243) < 5e-5, "Ea = 50 kJ/mol: tăng 10 K tốc độ ×1,9243", TocDoMoHinh.TiLeTocDo().ToString("0.0000"));
            Check(Gan(Math.Log(k298), Math.Log(1e10) - 50000 / (TocDoMoHinh.R_SI * 298.15), 1e-12), "ln k = ln A − Ea/RT");
            var Tds = new List<double> { 280, 300, 320, 340, 360 };
            var kds = new List<double>();
            foreach (var T in Tds) kds.Add(TocDoMoHinh.KTheoArrhenius(3e9, 62, T));
            var khop = TocDoMoHinh.KhopArrhenius(Tds, kds);
            Check(khop.Co && Math.Abs(khop.EaKJ - 62) < 1e-6 && Math.Abs(khop.A / 3e9 - 1) < 1e-6, "Khớp Arrhenius tìm lại Ea, A", khop.EaKJ.ToString("0.000000"));
            Check(Math.Abs(TocDoMoHinh.HeSoTangXucTac(20, 298.15) - 3190.4) < 0.1, "Xúc tác giảm Ea 20 kJ/mol: tốc độ ×3190", TocDoMoHinh.HeSoTangXucTac().ToString("0.0"));
            double g = TocDoMoHinh.HeSoVantHoff(50, 298.15);
            Check(Math.Abs(TocDoMoHinh.EaTuGamma(g, 298.15) - 50) < 1e-9 && g > 1.9 && g < 2.0, "Van 't Hoff γ ↔ Ea", g.ToString("0.000"));
            Check(Math.Abs(TocDoMoHinh.NongDoBac1(1, 0.1, TocDoMoHinh.ThoiGianBanHuyBac1(0.1)) - 0.5) < 1e-12, "Bậc 1: C(t½) = C0/2");
            Check(Math.Abs(TocDoMoHinh.NongDoBac2(0.8, 0.3, TocDoMoHinh.ThoiGianBanHuyBac2(0.8, 0.3)) - 0.4) < 1e-12, "Bậc 2: C(t½) = C0/2");
            Check(Math.Abs(TocDoMoHinh.NongDoBac0(1, 0.01, TocDoMoHinh.ThoiGianBanHuyBac0(1, 0.01)) - 0.5) < 1e-12 && TocDoMoHinh.NongDoBac0(1, 0.01, 500) == 0, "Bậc 0: C(t½) = C0/2, không âm");
            Check(Math.Abs(TocDoMoHinh.Erfc(0) - 1) < 1e-6 && Math.Abs(TocDoMoHinh.Erfc(1) - 0.157299) < 1e-6 && Math.Abs(TocDoMoHinh.Erfc(5) - 1.5375e-12) < 2e-15, "erfc đúng (0, 1, 5)");
            var nl1 = TocDoMoHinh.PhanBoNangLuong3D(50, 298.15);
            var nl2 = TocDoMoHinh.PhanBoNangLuong3D(50, 308.15);
            Check(nl1.TiLe3D > nl1.ExpAmX && nl2.TiLe3D > nl1.TiLe3D, "Tỉ lệ phân tử E ≥ Ea tăng theo T", nl1.TiLe3D.ToString("0.000e0"));
            double tichPhanE = 0, tren = 0, dE = 0.01;
            for (double E = dE / 2; E < 200; E += dE) { double fx = TocDoMoHinh.MatDoNangLuong(E, 400) * dE; tichPhanE += fx; if (E >= 20) tren += fx; }
            Check(Math.Abs(tichPhanE - 1) < 1e-4 && Math.Abs(tren / TocDoMoHinh.PhanBoNangLuong3D(20, 400).TiLe3D - 1) < 2e-3, "Mật độ năng lượng chuẩn hóa, phần E ≥ Ea khớp công thức erfc", (tren).ToString("0.00000"));

            // ===== 3. Cân bằng – Le Chatelier =====
            double Kp = CanBangMoHinh.KpTuKc(0.00461, 298.15, 1);
            Check(Math.Abs(Kp - 0.11428) < 5e-6, "Kp = Kc(RT)^Δn = 0,11428", Kp.ToString("0.00000"));
            double Kc373 = CanBangMoHinh.KcTheoT(373.15, 57.2, 298.15, 0.00461);
            Check(Math.Abs(Kc373 - 0.4763) < 5e-5, "van 't Hoff: Kc(373 K) = 0,4763 (thu nhiệt: K tăng theo T)", Kc373.ToString("0.0000"));
            var ice = CanBangMoHinh.CanBangPhanLi(0.04, 0, 0.00461);
            Check(Math.Abs(ice.Q / 0.00461 - 1) < 1e-9 && Math.Abs(ice.CB - 0.01248) < 5e-6, "Bảng ICE N₂O₄ ⇌ 2NO₂: Q = Kc, [NO₂] = 0,01248 M", ice.CB.ToString("0.00000"));
            var ice2 = CanBangMoHinh.CanBangPhanLi(0.03, 0.02, 0.00461);
            Check(Math.Abs(ice2.Q / 0.00461 - 1) < 1e-9, "ICE có NO₂ ban đầu vẫn về Q = Kc", ice2.Q.ToString("0.000000"));
            double ca = 0.04, cb = 0;
            for (int k = 0; k < 40000; k++) { var b = CanBangMoHinh.BuocTichPhan(ca, cb, 0.00461, 0.01, 1.0); ca = b.CA; cb = b.CB; }
            Check(Math.Abs(cb - ice.CB) < 1e-5, "Tích phân Euler hội tụ về nghiệm ICE", cb.ToString("0.000000"));
            Check(CanBangMoHinh.Chieu(0.5, 1) == ChieuDich.Thuan && CanBangMoHinh.Chieu(2, 1) == ChieuDich.Nghich && CanBangMoHinh.Chieu(1.004, 1) == ChieuDich.CanBang, "Chiều: Q < K thuận, Q > K nghịch, |Q−K|/K ≤ 0,5 % cân bằng");
            Check(Gan(CanBangMoHinh.QSauNen(0.00461, 2, 1), 0.00922), "Nén 2 lần, Δn = 1: Q = 2K");
            Check(Math.Abs(CanBangMoHinh.KpHaber(298.15) / 5.435e5 - 1) < 0.02, "Kp Haber (298 K) từ ΔH, ΔS ≈ 5,4·10⁵", CanBangMoHinh.KpHaber().ToString("0.###e0"));

            // hệ tổng quát từ JSON (dùng dữ liệu nhúng nếu không truyền vào)
            var dsHe = CanBangMoHinh.DocDanhSach(jsonHeCanBang ?? HeMau);
            HeCanBang Tim(string id) { foreach (var h in dsHe) if (h.Id == id) return h; return null; }
            var no2 = Tim("N2O4_2NO2");
            if (no2 != null)
            {
                Check(no2.DeltaN == -1 && Math.Abs(no2.K - 216.92) < 1e-6, "Hệ 2NO₂ ⇌ N₂O₄: Δn = −1, K(298) = 216,92");
                int iNO2 = no2.Chat[0].SanPham ? 1 : 0, iN2O4 = 1 - iNO2;
                no2.T = 298.15; no2.C[iNO2] = 0; no2.C[iN2O4] = 0.04;
                no2.DenCanBang();
                Check(Math.Abs(no2.C[iNO2] - ice.CB) < 1e-6 && Math.Abs(no2.Q() / no2.K - 1) < 1e-9, "Hệ tổng quát khớp bảng ICE của bản JS", no2.C[iNO2].ToString("0.000000"));
                // tăng T: tỏa nhiệt (ΔH < 0) → K giảm → nghịch → NO₂ tăng
                double no2Truoc = no2.C[iNO2], Ktruoc = no2.K;
                no2.T = 348.15;
                var ch = CanBangMoHinh.Chieu(no2.Q(), no2.K);
                for (int k = 0; k < 600; k++) no2.Buoc(1.0 / 60);
                Check(no2.K < Ktruoc && ch == ChieuDich.Nghich && no2.C[iNO2] > no2Truoc && Math.Abs(no2.Q() / no2.K - 1) < 5e-3,
                    "Đun nóng hệ tỏa nhiệt: K giảm, dịch nghịch, NO₂ nâu tăng", CanBangMoHinh.TenChieu(ch));
                // nén ×2: Δn = −1 → Q = K/2 → thuận (về phía ít mol khí)
                no2.DenCanBang();
                double tyLeTruoc = no2.C[iN2O4] / (no2.C[iNO2] + no2.C[iN2O4]);
                no2.DoiTheTich(1.0, 0.5);
                double qSau = no2.Q();
                Check(Math.Abs(qSau / no2.K - 0.5) < 1e-6, "Nén 2 lần: Q = K·2^Δn = K/2 (khớp qSauNen)", (qSau / no2.K).ToString("0.000"));
                var ch2 = CanBangMoHinh.Chieu(qSau, no2.K);
                double xi = no2.XiCanBang();
                no2.DenCanBang();
                double tyLeSau = no2.C[iN2O4] / (no2.C[iNO2] + no2.C[iN2O4]);
                Check(ch2 == ChieuDich.Thuan && xi > 0 && tyLeSau > tyLeTruoc, "Nén: dịch thuận về phía ít phân tử khí (N₂O₄)", tyLeTruoc.ToString("0.000") + " → " + tyLeSau.ToString("0.000"));
                // thêm NO₂ → thuận
                no2.C[iNO2] += 0.02;
                Check(CanBangMoHinh.Chieu(no2.Q(), no2.K) == ChieuDich.Thuan && no2.XiCanBang() > 0, "Thêm chất đầu NO₂: dịch thuận");
                no2.DenCanBang();
                // xúc tác: K không đổi, chỉ nhanh hơn
                double KtXt = no2.K;
                no2.C[iN2O4] += 0.01;
                double conLai1, conLai2;
                { var c1 = (double[])no2.C.Clone(); no2.CoXucTac = false; no2.Buoc(0.3); conLai1 = Math.Abs(no2.XiCanBang()); no2.C = c1; }
                { no2.CoXucTac = true; no2.Buoc(0.3); conLai2 = Math.Abs(no2.XiCanBang()); no2.CoXucTac = false; }
                Check(no2.K == KtXt && conLai2 < conLai1, "Xúc tác: K không đổi, về cân bằng nhanh hơn");
            }
            else Check(false, "Có hệ N2O4_2NO2 trong he-can-bang.json");
            var hi = Tim("H2_I2_2HI");
            if (hi != null)
            {
                hi.T = 700; for (int i = 0; i < hi.C.Length; i++) hi.C[i] = hi.Chat[i].SanPham ? 0 : 0.1;
                hi.DenCanBang();
                double q0 = hi.Q();
                hi.DoiTheTich(1, 0.5);
                Check(hi.DeltaN == 0 && Math.Abs(hi.Q() / q0 - 1) < 1e-9 && CanBangMoHinh.Chieu(hi.Q(), hi.K) == ChieuDich.CanBang, "H₂ + I₂ ⇌ 2HI (Δn = 0): nén không làm chuyển dịch");
            }
            var nh3 = Tim("N2_3H2_2NH3");
            if (nh3 != null)
            {
                Check(nh3.LaKp && nh3.DeltaN == -2 && nh3.Kc(723.15) < nh3.Kc(298.15), "Haber: Kp → Kc theo Δn = −2; K giảm khi đun (tỏa nhiệt)");
                nh3.T = 723.15; for (int i = 0; i < nh3.C.Length; i++) nh3.C[i] = nh3.Chat[i].SanPham ? 0.01 : 1;
                nh3.DenCanBang();
                int iNH3 = Array.FindIndex(nh3.Chat, c => c.SanPham);
                double truoc = nh3.C[iNH3];
                nh3.DoiTheTich(1, 0.5); nh3.DenCanBang();
                Check(nh3.C[iNH3] > 2 * truoc && Math.Abs(nh3.Q() / nh3.K - 1) < 1e-6, "Haber: tăng áp suất (nén) tăng hiệu suất NH₃", (truoc * 2).ToString("0.0000") + " → " + nh3.C[iNH3].ToString("0.0000"));
            }
            var fe = Tim("Fe3_SCN_FeSCN");
            if (fe != null)
            {
                fe.T = 298.15; for (int i = 0; i < fe.C.Length; i++) fe.C[i] = fe.Chat[i].SanPham ? 0 : 0.01;
                fe.DenCanBang();
                fe.DoiTheTich(1, 2);   // pha loãng 2 lần
                Check(!fe.CoKhi && CanBangMoHinh.Chieu(fe.Q(), fe.K) == ChieuDich.Nghich, "Fe³⁺ + SCN⁻ pha loãng: dịch nghịch (về phía nhiều tiểu phân)");
                var mau = new float[4];
                fe.DenCanBang(); fe.MauHonHop(mau);
                Check(mau[0] > mau[1] && mau[0] > mau[2] && mau[3] > 0, "Màu dung dịch phức đỏ");
            }

            // ===== 4. Động cơ nhiệt =====
            var ts = ThamSoChuTrinh.MacDinh;
            var otto = DongCoNhietMoHinh.Otto(ts);
            Check(Math.Abs(otto.EtaPhanTram - 60.2) < 0.05, "Otto r = 10, γ = 1,4: η = 60,2 %", otto.EtaPhanTram.ToString("0.00"));
            Check(Math.Abs(otto.W / otto.QIn - otto.Eta) < 1e-12, "Otto: η = w/q_in = 1 − r^(1−γ)");
            Check(otto.Eta <= otto.EtaCarnot && otto.EtaCarnot < 1, "Otto: η ≤ η_Carnot (giữa T1 và T3)", (otto.EtaCarnot * 100).ToString("0.0") + " %");
            var ts8 = ts; ts8.R = 8; ts8.QIn = 1000;
            var o8 = DongCoNhietMoHinh.Otto(ts8);
            Check(Math.Abs(o8.T2 - 689) < 0.5 && Math.Abs(o8.W - 565) < 0.5, "Otto r = 8: T2 = 689 K, w = 565 kJ/kg", o8.T2.ToString("0.0") + " / " + o8.W.ToString("0.0"));
            double wSo = DongCoNhietMoHinh.CongTichPhanSo(otto);
            Check(Math.Abs(wSo / otto.W - 1) < 2e-3, "∮p dv trên chu trình 4 kỳ = w (Otto)", wSo.ToString("0.0") + " / " + otto.W.ToString("0.0"));
            var tsD = ts; tsD.Kieu = KieuChuTrinh.Diesel; tsD.R = 18; tsD.Rc = 2;
            var diesel = DongCoNhietMoHinh.Diesel(tsD);
            Check(Math.Abs(diesel.W / diesel.QIn - diesel.Eta) < 1e-9, "Diesel: công thức η khớp w/q_in", diesel.EtaPhanTram.ToString("0.00") + " %");
            Check(diesel.Eta <= diesel.EtaCarnot, "Diesel: η ≤ η_Carnot");
            double wD = DongCoNhietMoHinh.CongTichPhanSo(diesel);
            Check(Math.Abs(wD / diesel.W - 1) < 3e-3, "∮p dv trên chu trình 4 kỳ = w (Diesel)", wD.ToString("0.0") + " / " + diesel.W.ToString("0.0"));
            var tsD2 = tsD; tsD2.R = 10;
            Check(DongCoNhietMoHinh.Diesel(tsD2).Eta < DongCoNhietMoHinh.Otto(new ThamSoChuTrinh { R = 10, Gamma = 1.4, T1 = 300, P1 = 100, QIn = 1500 }).Eta,
                "Cùng r: η Diesel < η Otto");
            bool tangTheoR = true, duoiCarnot = true;
            double truocEta = 0;
            for (double r = 4; r <= 22; r += 0.5)
            {
                var t2 = ts; t2.R = r;
                var o = DongCoNhietMoHinh.Otto(t2);
                if (o.Eta <= truocEta) tangTheoR = false;
                if (o.Eta > o.EtaCarnot + 1e-12) duoiCarnot = false;
                truocEta = o.Eta;
                var td = ts; td.Kieu = KieuChuTrinh.Diesel; td.R = Math.Max(r, 6);
                for (double rc = 1.1; rc <= 3.5; rc += 0.2) { td.Rc = rc; var d = DongCoNhietMoHinh.Diesel(td); if (d.Eta > d.EtaCarnot + 1e-12) duoiCarnot = false; }
            }
            Check(tangTheoR, "η Otto tăng theo tỉ số nén r");
            Check(duoiCarnot, "Quét r = 4..22, rc = 1,1..3,5: η luôn ≤ η_Carnot");
            var dsNl = DongCoNhietMoHinh.DocNhienLieu(jsonNhienLieu ?? NhienLieuMau);
            var iso = dsNl.Find(n => n.Id == "isooctane");
            if (iso != null)
            {
                var kqNl = DongCoNhietMoHinh.TinhNhienLieu(iso);
                Check(Math.Abs(kqNl.LHV_MJ_kg - 44.4) < 0.3 && Math.Abs(kqNl.AFR - 15.1) < 0.2, "Isooctane: LHV ≈ 44,4 MJ/kg, AFR ≈ 15,1", kqNl.LHV_MJ_kg.ToString("0.00") + " MJ/kg; AFR " + kqNl.AFR.ToString("0.00"));
                Check(kqNl.PhuongTrinh.StartsWith("C8H18 + 12,5 (O₂ + 3,76 N₂) → 8 CO₂ + 9 H₂O + 47 N₂"), "Phương trình cháy isooctane cân bằng", kqNl.PhuongTrinh);
            }
            var h2 = dsNl.Find(n => n.Id == "hydrogen");
            if (h2 != null) Check(Math.Abs(DongCoNhietMoHinh.TinhNhienLieu(h2).HHV_MJ_kg - 142.9) < 0.1, "H₂: HHV ≈ 142,9 MJ/kg");

            // ===== 5. Dữ liệu JSON =====
            if (jsonKhi != null)
            {
                int soKhi = JsonHoaLy.L(JsonHoaLy.Lay(JsonHoaLy.D(JsonHoaLy.Doc(jsonKhi)), "danh_sach")).Count;
                Check(soKhi == 8, "khi.json có 8 chất khí", soKhi.ToString());
            }
            if (jsonPhanUng != null)
            {
                var pu = TocDoMoHinh.DocDanhSach(jsonPhanUng);
                Check(pu.Count == 4 && pu[0].Bac == 2 && pu[1].Bac == 1 && pu[2].Bac == 2 && pu[3].CoChatRan, "phan-ung.json: 4 phản ứng, bậc đọc đúng");
            }
            if (jsonCauHoi != null)
            {
                int tong = 0, tinhLai = 0, soKhop = 0;
                var lech = new List<string>();
                foreach (var js in jsonCauHoi)
                {
                    foreach (var c in CauHoiHoaLy.Doc(js))
                    {
                        tong++;
                        bool hopLe = c.Dang == DangCauHoi.TracNghiem ? c.LuaChon.Length >= 2 && c.ChiSoDapAn >= 0 && c.ChiSoDapAn < c.LuaChon.Length
                                   : c.Dang == DangCauHoi.DungSai ? c.Y.Length >= 2 : !string.IsNullOrEmpty(c.DapSo);
                        if (!hopLe) lech.Add(c.Id + " thiếu dữ liệu");
                        if (c.Dang != DangCauHoi.TraLoiNgan || c.TinhBang == null) continue;
                        double x = CauHoiHoaLy.TinhBang(c.TinhBang);
                        if (double.IsNaN(x)) { lech.Add(c.Id + ": không tính được " + JsonHoaLy.S(c.TinhBang, "ham")); continue; }
                        tinhLai++;
                        string s = CauHoiHoaLy.LamTron(x, (int)JsonHoaLy.F(c.TinhBang, "lam_tron", CauHoiHoaLy.SoChuSoThapPhan(c.DapSo)));
                        if (CauHoiHoaLy.KiemTraTraLoiNgan(s, c.DapSo)) soKhop++;
                        else lech.Add(c.Id + ": C# " + s + " ≠ " + c.DapSo);
                    }
                }
                Check(tong == 36 && lech.Count == 0 && tinhLai == 12 && soKhop == 12, "Câu hỏi: 36 câu hợp lệ, mô hình C# tính lại đúng 12/12 đáp số dạng III",
                    $"{tong} câu, {soKhop}/{tinhLai} khớp" + (lech.Count > 0 ? "; " + string.Join("; ", lech) : ""));
            }
            Check(CauHoiHoaLy.KiemTraTraLoiNgan("12,47", "12,47") && CauHoiHoaLy.KiemTraTraLoiNgan("12.47", "12,47") && !CauHoiHoaLy.KiemTraTraLoiNgan("12,5", "12,47")
                  && CauHoiHoaLy.KiemTraTraLoiNgan("1729", "1729") && CauHoiHoaLy.KiemTraTraLoiNgan("3190,4", "3190,4") && !CauHoiHoaLy.KiemTraTraLoiNgan("abc", "1"),
                "Chấm trả lời ngắn: nhận dấu phẩy và dấu chấm, sai số nửa đơn vị chữ số cuối");

            passed = p; failed = f;
            report = sb.ToString();
            return f == 0;
        }

        // dữ liệu dự phòng khi chạy test không có Resources (giống he-can-bang.json / nhien-lieu.json, rút gọn)
        const string HeMau = "{\"danh_sach\":[" +
            "{\"id\":\"N2O4_2NO2\",\"chat\":[{\"ct\":\"NO2\",\"he_so\":2,\"vai_tro\":\"chat_dau\",\"pha\":\"k\",\"mau_rgb\":[180,83,9],\"do_dam\":1.0},{\"ct\":\"N2O4\",\"he_so\":1,\"vai_tro\":\"san_pham\",\"pha\":\"k\",\"do_dam\":0.0}],\"dH_kJ\":-57.2,\"K_ref\":216.92,\"T_ref\":298.15,\"loai_K\":\"Kc\"}," +
            "{\"id\":\"H2_I2_2HI\",\"chat\":[{\"ct\":\"H2\",\"he_so\":1,\"vai_tro\":\"chat_dau\",\"pha\":\"k\"},{\"ct\":\"I2\",\"he_so\":1,\"vai_tro\":\"chat_dau\",\"pha\":\"k\",\"do_dam\":0.9},{\"ct\":\"HI\",\"he_so\":2,\"vai_tro\":\"san_pham\",\"pha\":\"k\"}],\"dH_kJ\":-9.48,\"K_ref\":54.0,\"T_ref\":700.0,\"loai_K\":\"Kc\"}," +
            "{\"id\":\"N2_3H2_2NH3\",\"chat\":[{\"ct\":\"N2\",\"he_so\":1,\"vai_tro\":\"chat_dau\",\"pha\":\"k\"},{\"ct\":\"H2\",\"he_so\":3,\"vai_tro\":\"chat_dau\",\"pha\":\"k\"},{\"ct\":\"NH3\",\"he_so\":2,\"vai_tro\":\"san_pham\",\"pha\":\"k\"}],\"dH_kJ\":-91.8,\"K_ref\":5.435e5,\"T_ref\":298.15,\"loai_K\":\"Kp\"}," +
            "{\"id\":\"Fe3_SCN_FeSCN\",\"chat\":[{\"ct\":\"Fe3+\",\"he_so\":1,\"vai_tro\":\"chat_dau\",\"pha\":\"aq\",\"mau\":\"#fde047\"},{\"ct\":\"SCN-\",\"he_so\":1,\"vai_tro\":\"chat_dau\",\"pha\":\"aq\"},{\"ct\":\"[Fe(SCN)]2+\",\"he_so\":1,\"vai_tro\":\"san_pham\",\"pha\":\"aq\",\"mau\":\"#dc2626\",\"do_dam\":1.0}],\"dH_kJ\":-24.0,\"K_ref\":138.0,\"T_ref\":298.15,\"loai_K\":\"Kc\"}]}";

        const string NhienLieuMau = "{\"danh_sach\":[" +
            "{\"id\":\"isooctane\",\"cong_thuc\":\"C8H18\",\"C\":8,\"H\":18,\"O\":0,\"M\":114.0,\"delta_f_H\":-259.2}," +
            "{\"id\":\"hydrogen\",\"cong_thuc\":\"H2\",\"C\":0,\"H\":2,\"O\":0,\"M\":2.0,\"delta_f_H\":0.0}]}";
    }
}
