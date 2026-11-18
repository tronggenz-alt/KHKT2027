using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Khkt.Environment;
using Khkt.Rendering;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Khkt.HoaLy
{
    /// <summary>
    /// Kiểm thử 4 mô phỏng Hóa lý trong PlayMode (batchmode có đồ họa) – gọi từ Editor/KhktHoaLyVerify.Run:
    /// mở bằng reflection như MainMenu, chạy test C# thuần, đổi tham số qua thanh trượt rồi so với công thức
    /// (pV = nRT, Arrhenius, Le Chatelier dịch đúng chiều, η ≤ Carnot), số hạt ≤ SoHatToiDa, cấp phát mỗi khung, câu hỏi nhanh,
    /// chụp ảnh 1920×1080 vào $KHKT_HOALY_SHOTS (mặc định $TMPDIR/vera_hoaly_shots). Log [HL_PASS]/[HL_FAIL]/[HL_RESULT].
    /// </summary>
    public class KhktHoaLyRunner : MonoBehaviour
    {
        const int ShotW = 1920, ShotH = 1080;
        int passes, failures, runtimeErrors, shots;
        readonly List<string> failed = new List<string>(), errLines = new List<string>();
        string outDir;
        RenderTexture rt;
        Texture2D readTex;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            outDir = System.Environment.GetEnvironmentVariable("KHKT_HOALY_SHOTS");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.GetTempPath(), "vera_hoaly_shots");
            Directory.CreateDirectory(outDir);
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;

        void OnLog(string c, string st, LogType t)
        {
            if (t != LogType.Error && t != LogType.Exception && t != LogType.Assert) return;
            if (c != null && c.StartsWith("[HL_")) return;
            if (!string.IsNullOrEmpty(st) && st.Contains("SearchInit.IndexationOnStartup")) return;
            runtimeErrors++;
            errLines.Add($"{t}: {c} | {(st ?? "").Split('\n').FirstOrDefault()}");
        }

        void Check(bool ok, string ten, string ct = "")
        {
            if (ok) { passes++; Debug.Log($"[HL_PASS] {ten}" + (ct == "" ? "" : " | " + ct)); }
            else { failures++; failed.Add(ten); Debug.LogError($"[HL_FAIL] {ten}" + (ct == "" ? "" : " | " + ct)); }
        }

        static bool Gan(double a, double b, double tuongDoi) => Math.Abs(a - b) <= tuongDoi * Math.Max(Math.Abs(a), Math.Abs(b));
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator Giay(float s) { float t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < s) yield return null; }

        IEnumerator Start()
        {
            StartCoroutine(Watchdog(300f));
            rt = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            readTex = new Texture2D(ShotW, ShotH, TextureFormat.RGBA32, false);
            var mucCu = HardwareProfile.Muc;
            bool tuDongCu = HardwareProfile.TuDong;
            HardwareProfile.Dat(MucDoHoa.Cao);
            yield return Frames(10);

            // 0. test C# thuần (dùng đúng các tệp JSON trong Resources/HoaLy)
            string Doc(string ten) { var ta = Resources.Load<TextAsset>("HoaLy/" + ten); return ta != null ? ta.text : null; }
            var cauHoi = new[] { "cau_hoi_khi_ly_tuong", "cau_hoi_toc_do", "cau_hoi_can_bang", "cau_hoi_dong_co_nhiet" }.Select(Doc).ToList();
            Check(cauHoi.All(s => s != null), "Có đủ 4 tệp câu hỏi trong Resources/HoaLy");
            bool okThuan = HoaLySimTests.RunAll(out int p0, out int f0, out string bc, cauHoi, Doc("khi"), Doc("phan_ung"), Doc("he_can_bang"), Doc("nhien_lieu"));
            Debug.Log("[HL_UNIT]\n" + bc);
            Check(okThuan, "HoaLySimTests (C# thuần)", $"{p0} đạt, {f0} sai");

            // 1. mở bằng reflection đúng như MainMenu.MoHoaLy
            var camGia = new GameObject("Camera cảnh (giả)").AddComponent<Camera>();
            int uiCapCu = DesktopViewer.UiCapture;
            System.Reflection.MethodInfo mo = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                mo = asm.GetType("Khkt.HoaLy.HoaLyMoDun", false)?.GetMethod("Mo", new[] { typeof(string) });
                if (mo != null) break;
            }
            Check(mo != null, "MainMenu tìm được Khkt.HoaLy.HoaLyMoDun.Mo(string) qua reflection");
            mo?.Invoke(null, new object[] { "khi_ly_tuong" });
            yield return Frames(3);
            var mh = HoaLyMoDun.ManHinh;
            Check(HoaLyMoDun.DangMo && mh != null && mh.TrangHienTai is TrangKhiLyTuong, "Mo(\"khi_ly_tuong\") mở lớp phủ đúng trang");
            Check(DesktopViewer.UiCapture == uiCapCu + 1, "Mở lớp phủ khóa điều khiển lab (UiCapture+1)");
            Check(!camGia.enabled, "Camera cảnh bị tắt khi lớp phủ mở");
            mh.Cam.targetTexture = rt;
            yield return Giay(0.6f);

            yield return KiemKhi(mh);
            yield return KiemTocDo(mh);
            yield return KiemCanBang(mh);
            yield return KiemDongCo(mh);
            yield return KiemCauHoi(mh);

            // mở từng mã như 4 ô của màn Giải bài tập
            foreach (var ma in HoaLyMoDun.DanhSachMa)
            {
                HoaLyMoDun.Mo(ma);
                yield return null;
                Check(mh.TrangHienTai.Ma == ma, "Mo(\"" + ma + "\") chọn đúng tab");
            }
            HoaLyMoDun.Mo("khong_co");
            yield return null;
            Check(mh.TrangHienTai.Ma == "khi_ly_tuong", "Mã lạ thì về tab đầu, không lỗi");

            // đóng
            mh.Cam.targetTexture = null;
            HoaLyMoDun.Dong();
            yield return Frames(2);
            Check(!HoaLyMoDun.DangMo && DesktopViewer.UiCapture == uiCapCu && camGia.enabled, "Dong() đóng lớp phủ, trả điều khiển và camera cảnh");
            HoaLyMoDun.Mo("can_bang");
            yield return null;
            Check(HoaLyMoDun.DangMo && mh.TrangHienTai.Ma == "can_bang" && DesktopViewer.UiCapture == uiCapCu + 1, "Mở lại sau khi đóng");
            HoaLyMoDun.Dong();

            if (tuDongCu) HardwareProfile.DatTuDong(); else HardwareProfile.Dat(mucCu);
            Debug.Log($"[HL_SUMMARY] passes={passes} failures={failures} runtimeErrors={runtimeErrors} shots={shots}");
            if (failed.Count > 0) Debug.LogError("[HL_FAILED_LIST] " + string.Join(" || ", failed));
            if (errLines.Count > 0) Debug.LogError("[HL_RUNTIME_ERR_LIST] " + string.Join(" || ", errLines.Take(20)));
            bool ok = failures == 0 && runtimeErrors == 0;
            Debug.Log(ok ? "[HL_RESULT] ALL PASSED" : "[HL_RESULT] FAILED");
#if UNITY_EDITOR
            EditorApplication.Exit(ok ? 0 : 1);
#endif
        }

        IEnumerator KiemKhi(HoaLyManHinh mh)
        {
            mh.ChonTrang("khi_ly_tuong");
            var k = (TrangKhiLyTuong)mh.TrangHienTai;
            k.ChonKhiTheoId("N2");
            k.Dat(10, 300, 2);
            yield return Frames(2);
            double pDung = 2 * KhiLyTuongMoHinh.R_BAR * 300 / 10;
            Check(Gan(k.P, pDung, 1e-5), "Khí: p = nRT/V sau khi kéo thanh trượt", $"{k.P:0.0000} / {pDung:0.0000} bar");
            double p1 = k.P;
            k.Dat(20, 300, 2);
            yield return null;
            Check(Gan(k.P * 20, p1 * 10, 1e-5), "Khí: Boyle – V ×2 thì p ÷2", $"{k.P:0.0000} bar");
            k.Dat(20, 600, 2);
            yield return null;
            Check(Gan(k.P, p1, 1e-5), "Khí: V ×2 và T ×2 thì p không đổi");
            double vr1 = k.Hop.VRmsDo();
            k.Dat(20, 1200, 2);
            yield return null;
            double vr2 = k.Hop.VRmsDo();
            Check(Gan(vr2 / vr1, Math.Sqrt(2), 1e-3), "Khí: T ×2 → v_rms của hạt ×√2", $"{vr2 / vr1:0.0000}");
            Check(Math.Abs(vr2 / k.VRms - 1) < 0.06, "Khí: v_rms đo trên hạt khớp √(3RT/M)", $"{vr2:0} / {k.VRms:0} m/s");
            // số hạt theo mức đồ họa
            foreach (var muc in new[] { MucDoHoa.Thap, MucDoHoa.Vua, MucDoHoa.Cao, MucDoHoa.SieuNet })
            {
                HardwareProfile.Dat(muc);
                k.Dat(20, 300, 5);
                yield return Frames(2);
                Check(k.Hop.SoHat <= HardwareProfile.SoHatToiDa && k.Hop.SoHat >= HardwareProfile.SoHatToiDa * 0.95, $"Khí: n = 5 mol vẽ {k.Hop.SoHat} hạt ≤ SoHatToiDa ({HardwareProfile.TenMuc(muc)} {HardwareProfile.SoHatToiDa})");
            }
            HardwareProfile.Dat(MucDoHoa.Cao);
            k.ChonKhiTheoId("CO2");
            k.DatKhiThuc(true);
            k.Dat(0.5f, 300, 1);
            yield return Frames(2);
            Check(k.KhiThuc && k.Vdw.Z < 1, "Khí: CO₂ nén 0,5 L – Van der Waals Z < 1", $"Z = {k.Vdw.Z:0.000}");
            k.DatKhiThuc(false);
            k.ChonKhiTheoId("N2");
            k.Dat(24.79, 298.15, 1);
            yield return Giay(1.6f);
            // cấp phát mỗi khung (số liệu chữ ~5 lần/giây được tính vào)
            using (var ghi = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame"))
            {
                long tong = 0, lonNhat = 0;
                int soKhung = 0, khongCap = 0;
                float t0 = Time.realtimeSinceStartup;
                yield return null;
                while (Time.realtimeSinceStartup - t0 < 2f)
                {
                    yield return null;
                    long b = ghi.LastValue;
                    tong += b; lonNhat = Math.Max(lonNhat, b); soKhung++;
                    if (b == 0) khongCap++;
                }
                if (ghi.Valid)
                    Check(tong / (double)Math.Max(1, soKhung) < 3000, "Khí: cấp phát mỗi khung nhỏ (vẽ hạt không cấp phát; chữ số liệu 5 lần/giây)",
                        $"TB {tong / (double)Math.Max(1, soKhung):0} B/khung, lớn nhất {lonNhat} B, {khongCap}/{soKhung} khung không cấp phát");
                else Debug.Log("[HL_NOTE] Không đọc được bộ đếm GC Allocated In Frame – bỏ qua đo cấp phát");
            }
            Shot("khi_ly_tuong");
            k.Dat(8, 900, 3);
            yield return Giay(1.2f);
            Shot("khi_ly_tuong_nong");
        }

        IEnumerator KiemTocDo(HoaLyManHinh mh)
        {
            mh.ChonTrang("toc_do");
            var t = (TrangTocDo)mh.TrangHienTai;
            t.ChonPhanUng(0);
            t.DatXucTac(false);
            t.Dat(298.15, 50, 1);
            yield return null;
            Check(Gan(t.K, 1e10 * Math.Exp(-50000 / (8.314462618 * 298.15)), 1e-6), "Tốc độ: k = A·e^(−Ea/RT)", $"k = {t.K:0.000}");
            double k1 = t.K;
            t.Dat(308.15, 50, 1);
            yield return null;
            Check(Math.Abs(t.K / k1 - 1.9243) < 1e-4, "Tốc độ: tăng 10 K, Ea = 50 → k ×1,9243 (Arrhenius)", $"{t.K / k1:0.0000}");
            t.Dat(308.15, 80, 1);
            yield return null;
            double kCao = t.K;
            Check(kCao < k1, "Tốc độ: Ea lớn hơn → k nhỏ hơn");
            t.DatXucTac(true);
            yield return null;
            double tang = t.K / kCao;
            Check(Gan(tang, TocDoMoHinh.HeSoTangXucTac(t.PhanUng.DoGiamEaXucTac, 308.15), 1e-6), "Tốc độ: xúc tác tăng k đúng exp(ΔEa/RT)", $"×{tang:0.0}");
            double c = TocDoMoHinh.NongDo(t.PhanUng.Bac, t.C0, t.K, t.ThoiGianBanHuy);
            Check(Math.Abs(c - t.C0 / 2) < 1e-9, "Tốc độ: tại t½ nồng độ còn C₀/2");
            t.DatXucTac(false);
            t.Dat(298.15, 50, 1);
            yield return Giay(4f);
            Shot("toc_do");
            t.ChonPhanUng(3);
            t.DatXucTac(true);
            yield return Giay(4f);
            Shot("toc_do_zn_xuc_tac");
            t.DatXucTac(false);
            t.ChonPhanUng(0);
        }

        IEnumerator ChayToi(TrangCanBang cb, float giay)
        {
            yield return Giay(giay);
        }

        IEnumerator KiemCanBang(HoaLyManHinh mh)
        {
            mh.ChonTrang("can_bang");
            var cb = (TrangCanBang)mh.TrangHienTai;
            cb.ChonHe(0);
            yield return Giay(6f);
            var he = cb.He;
            int iNO2 = Array.FindIndex(he.Chat, x => x.CongThuc == "NO2");
            var ice = CanBangMoHinh.CanBangPhanLi(0.04, 0, 1 / he.K);
            Check(Math.Abs(he.Q() / he.K - 1) < 0.01 && Math.Abs(he.C[iNO2] / ice.CB - 1) < 0.01, "Cân bằng: hệ N₂O₄ tự về Q = K, [NO₂] khớp bảng ICE", $"[NO₂] = {he.C[iNO2]:0.00000} / {ice.CB:0.00000}");
            double no2 = he.C[iNO2], K0 = he.K;
            cb.Dat(348.15, 1);
            Check(he.K < K0 && cb.ChieuDuDoan == ChieuDich.Nghich, "Le Chatelier: đun nóng hệ tỏa nhiệt → K giảm, dự đoán dịch nghịch");
            yield return Giay(5f);
            Check(cb.XiSauTacDong < 0 && he.C[iNO2] > no2 && Math.Abs(he.Q() / he.K - 1) < 0.02, "Le Chatelier: sau khi đun hệ thật sự dịch nghịch, NO₂ nâu tăng", $"ξ = {cb.XiSauTacDong:0.00000}");
            Shot("can_bang_dun_nong");
            cb.Dat(348.15, 0.5);
            Check(cb.ChieuDuDoan == ChieuDich.Thuan && Gan(he.Q() / he.K, 0.5, 0.02), "Le Chatelier: nén ×2 (Δn = −1) → Q = K/2, dịch thuận");
            yield return Giay(5f);
            Check(cb.XiSauTacDong > 0, "Le Chatelier: sau khi nén hệ dịch thuận (về phía ít mol khí)", $"ξ = {cb.XiSauTacDong:0.00000}");
            cb.ThemChat(iNO2);
            Check(cb.ChieuDuDoan == ChieuDich.Thuan, "Le Chatelier: thêm NO₂ → dịch thuận");
            yield return Giay(4f);
            Check(cb.XiSauTacDong > 0, "Le Chatelier: thêm NO₂ – hệ đã dịch thuận");
            double Kx = he.K;
            cb.DatXucTac(true);
            yield return Frames(3);
            Check(he.K == Kx && he.CoXucTac, "Xúc tác không làm đổi K");
            cb.DatXucTac(false);

            cb.ChonHe(1);   // H₂ + I₂ ⇌ 2HI
            yield return Giay(5f);
            cb.Dat(cb.He.T, 0.5);
            Check(cb.ChieuDuDoan == ChieuDich.CanBang, "Le Chatelier: H₂ + I₂ ⇌ 2HI (Δn = 0) nén không chuyển dịch");
            cb.ChonHe(2);   // Haber
            yield return Giay(5f);
            int iNH3 = Array.FindIndex(cb.He.Chat, x => x.SanPham);
            double nh3 = cb.He.C[iNH3];
            cb.Dat(cb.He.T, 0.5);
            Check(cb.ChieuDuDoan == ChieuDich.Thuan, "Le Chatelier: Haber nén → dịch thuận");
            yield return Giay(5f);
            Check(cb.He.C[iNH3] > 2 * nh3, "Haber: nén làm tăng NH₃ hơn mức chỉ do nồng độ ×2", $"{nh3 * 2:0.0000} → {cb.He.C[iNH3]:0.0000}");
            double KCao = cb.He.K;
            cb.Dat(cb.He.T + 100, 0.5);
            Check(cb.He.K < KCao && cb.ChieuDuDoan == ChieuDich.Nghich, "Haber: tăng T (tỏa nhiệt) → K giảm, dịch nghịch");
            yield return Giay(2f);
            Shot("can_bang_haber");
            cb.ChonHe(3);   // Fe³⁺ + SCN⁻
            yield return Giay(5f);
            Shot("can_bang_fe_scn");
            cb.Dat(cb.He.T, 2);
            Check(cb.ChieuDuDoan == ChieuDich.Nghich, "Le Chatelier: pha loãng Fe³⁺ + SCN⁻ → dịch nghịch");
            yield return Giay(4f);
            Check(cb.XiSauTacDong < 0, "Pha loãng: hệ đã dịch nghịch", $"ξ = {cb.XiSauTacDong:0.000000}");
            cb.ChonHe(0);
        }

        IEnumerator KiemDongCo(HoaLyManHinh mh)
        {
            mh.ChonTrang("dong_co_nhiet");
            var d = (TrangDongCo)mh.TrangHienTai;
            d.DatChuTrinh(KieuChuTrinh.Otto);
            d.Dat(10, 1.4, 300, 1500, 2);
            yield return null;
            var kq = d.KetQua;
            Check(Math.Abs(kq.Eta - (1 - Math.Pow(10, -0.4))) < 1e-5 && Math.Abs(kq.EtaPhanTram - 60.2) < 0.05, "Động cơ: Otto r = 10 → η = 1 − r^(1−γ) = 60,2 %", $"{kq.EtaPhanTram:0.00} %");
            Check(kq.Eta <= kq.EtaCarnot, "Động cơ: η Otto ≤ η Carnot", $"{kq.Eta:0.000} ≤ {kq.EtaCarnot:0.000}");
            bool duoi = true;
            for (float r = 4; r <= 22; r += 2)
            {
                d.ThanhR.value = r;
                foreach (float q in new[] { 200f, 1500f, 2500f }) { d.ThanhQ.value = q; if (d.KetQua.Eta > d.KetQua.EtaCarnot + 1e-12) duoi = false; }
            }
            Check(duoi, "Động cơ: kéo r 4–22, q_in 200–2500 – Otto luôn ≤ Carnot");
            double wSo = DongCoNhietMoHinh.CongTichPhanSo(d.KetQua);
            Check(Math.Abs(wSo / d.KetQua.W - 1) < 3e-3, "Động cơ: diện tích chu trình p–v = công w", $"{wSo:0.0} / {d.KetQua.W:0.0} kJ/kg");
            int iIso = d.DanhSachNhienLieu.FindIndex(n => n.Id == "isooctane");
            d.ChonNhienLieu(iIso);
            var nl = DongCoNhietMoHinh.TinhNhienLieu(d.NhienLieuChon);
            Check(Math.Abs(d.ThamSo.QIn - nl.QIn) < 1 && Math.Abs(nl.LHV_MJ_kg - 44.43) < 0.05, "Động cơ: chọn isooctane đặt q_in từ nhiệt trị LHV", $"q_in = {d.ThamSo.QIn:0}, LHV = {nl.LHV_MJ_kg:0.00}");
            d.ThanhR.value = 10;
            yield return Giay(2.2f);
            Shot("dong_co_otto");
            double etaOtto10 = DongCoNhietMoHinh.Otto(new ThamSoChuTrinh { R = 10, Gamma = 1.4, T1 = 300, P1 = 100, QIn = 1500 }).Eta;
            d.DatChuTrinh(KieuChuTrinh.Diesel);
            d.ThanhR.value = 18; d.ThanhRc.value = 2;
            yield return null;
            var dz = d.KetQua;
            Check(dz.Kieu == KieuChuTrinh.Diesel && dz.Eta <= dz.EtaCarnot && Math.Abs(dz.W / dz.QIn - dz.Eta) < 1e-9, "Động cơ: Diesel η = w/q_in ≤ Carnot", $"{dz.EtaPhanTram:0.0} % ≤ {dz.EtaCarnot * 100:0.0} %");
            d.ThanhR.value = 10;
            yield return null;
            Check(d.KetQua.Eta < etaOtto10, "Động cơ: cùng r = 10, Diesel kém Otto");
            bool duoiD = true;
            for (float rc = 1.1f; rc <= 3.5f; rc += 0.4f) { d.ThanhRc.value = rc; if (d.KetQua.Eta > d.KetQua.EtaCarnot + 1e-12) duoiD = false; }
            Check(duoiD, "Động cơ: kéo rc 1,1–3,5 – Diesel luôn ≤ Carnot");
            d.ThanhR.value = 18; d.ThanhRc.value = 2;
            yield return Giay(1.6f);
            Shot("dong_co_diesel");
            d.DatChuTrinh(KieuChuTrinh.Otto);
        }

        IEnumerator KiemCauHoi(HoaLyManHinh mh)
        {
            mh.DatCheDoCauHoi(true);
            foreach (var ma in HoaLyMoDun.DanhSachMa)
            {
                mh.ChonTrang(ma);
                yield return null;
                var ch = mh.CauHoi;
                Check(ch.SoCau == 9, "Câu hỏi " + ma + ": 9 câu", ch.LoiNap);
                int dung = 0, sai = 0, tong = 0;
                for (int i = 0; i < ch.SoCau; i++)
                {
                    ch.Hien(i);
                    var c = ch.Cau;
                    if (c.Dang == DangCauHoi.TracNghiem)
                    {
                        ch.ChonLuaChon((c.ChiSoDapAn + 1) % c.LuaChon.Length);
                        if (ch.KetQua == false) sai++;
                        ch.Hien(i);
                        ch.ChonLuaChon(c.ChiSoDapAn);
                    }
                    else if (c.Dang == DangCauHoi.DungSai)
                    {
                        for (int y = 0; y < c.Y.Length; y++) ch.ChonY(y, !c.Y[y].Dung);
                        if (ch.KetQua == false) sai++;
                        ch.Hien(i);
                        for (int y = 0; y < c.Y.Length; y++) ch.ChonY(y, c.Y[y].Dung);
                    }
                    else
                    {
                        ch.NhapSo("-1234567");
                        if (ch.KetQua == false) sai++;
                        ch.Hien(i);
                        ch.NhapSo(c.DapSo);
                    }
                    tong++;
                    if (ch.KetQua == true) dung++;
                }
                Check(dung == 9 && sai == 9 && ch.SoDung == 9, "Câu hỏi " + ma + ": chấm đúng 9/9 câu trả lời đúng và 9/9 câu trả lời sai", $"đúng {dung}, bắt sai {sai}");
                ch.Hien(ma == "khi_ly_tuong" ? 3 : ma == "toc_do" ? 0 : ma == "can_bang" ? 7 : 1);
                if (ma == "khi_ly_tuong") { var c = ch.Cau; for (int y = 0; y < c.Y.Length; y++) ch.ChonY(y, y == 1 ? !c.Y[y].Dung : c.Y[y].Dung); }
                else if (ma == "can_bang") ch.NhapSo(ch.Cau.DapSo);
                else ch.ChonLuaChon(ch.Cau.ChiSoDapAn);
                yield return Giay(0.5f);
                Shot("cau_hoi_" + ma);
            }
            mh.DatCheDoCauHoi(false);
        }

        IEnumerator Watchdog(float s)
        {
            yield return new WaitForSecondsRealtime(s);
            Debug.LogError($"[HL_TIMEOUT] passes={passes} failures={failures}");
#if UNITY_EDITOR
            EditorApplication.Exit(2);
#endif
        }

        void Shot(string ten)
        {
            var mh = HoaLyMoDun.ManHinh;
            if (mh == null) return;
            var cam = mh.Cam;
            var cu = cam.targetTexture;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            readTex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0);
            readTex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = cu;
            string p = Path.Combine(outDir, ten + ".png");
            File.WriteAllBytes(p, readTex.EncodeToPNG());
            shots++;
            Debug.Log("[HL_SHOT] " + p);
        }
    }
}
