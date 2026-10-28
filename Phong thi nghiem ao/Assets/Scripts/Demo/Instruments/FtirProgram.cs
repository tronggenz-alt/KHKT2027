using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Khkt.Simulation.Chem;
using UnityEngine;
using UnityEngine.UI;
using static Khkt.Demo.Instruments.UvVisProgram;

namespace Khkt.Demo.Instruments
{
    /// <summary>
    /// Máy FTIR có ATR kim cương (thông số theo Shimadzu IRSpirit + QATR-S). Phổ mẫu tính từ thư viện dải hấp thụ theo thành phần thật
    /// của mẫu; giao thoa đồ tính bằng tổng cosin của phổ đơn chùm (máy biến đổi Fourier ngược lại thành phổ). Lỗi mô phỏng: không đo nền
    /// hoặc nền cũ (dải CO₂ 2350 cm⁻¹, răng cưa hơi nước), không lau tinh thể (phổ lẫn mẫu trước), quét ít lần (nhiễu theo 1/√N).
    /// Phân tích chuyên sâu: trừ phổ nước, gán nhóm chức, tách hỗn hợp, tra thư viện (HQI), định lượng etanol theo dải 1045 cm⁻¹.
    /// </summary>
    public class FtirProgram : InstrumentProgram
    {
        public override string Code => "ftir";
        static readonly string[] optional = { "dat_thong_so", "ket_thuc" };
        public override ICollection<string> Optional => optional;

        const float WarmNeedS = 4f;
        const double NuHi = 4000, NuLo = 650;
        public static readonly double[] StdEthanol = { 5, 10, 20, 30, 40 };  // % thể tích
        static readonly int[] scanOpts = { 1, 4, 16, 64 };
        static readonly int[] resOpts = { 2, 4, 8, 16 };

        public bool Power { get; private set; }
        public float Warm { get; private set; }
        public bool Clean { get; private set; } = true;
        List<FtirLibrary.Part> residue = new List<FtirLibrary.Part>(), bgResidue = new List<FtirLibrary.Part>();
        public int Scans { get; private set; } = 16;
        public int Res { get; private set; } = 4;
        public bool HasBackground { get; private set; }
        float bgTime;
        public enum Src { Empty, Sample, Std }
        public Src OnCrystal { get; private set; } = Src.Empty;
        public int StdIndex { get; private set; }

        float scanT = -1;
        string specName = "";
        public readonly List<(double nu, double a)> Spectrum = new List<(double, double)>();
        public List<(double nu, double a)> Subtracted { get; private set; }
        public double WaterScale { get; private set; } = double.NaN;
        public List<FtirLibrary.Part> Unmixed { get; private set; }
        public List<(FtirLibrary.Part p, double hqi)> Hits { get; private set; }
        public List<(double nu, double a, string group)> Bands { get; private set; }
        readonly List<double> interferogram = new List<double>();
        public readonly List<(double c, double a)> Cal = new List<(double, double)>();
        public double EthanolPct { get; private set; } = double.NaN;
        readonly List<string> unknown = new List<string>();
        int plotMode;  // 0 phổ, 1 giao thoa đồ, 2 phổ đã trừ nước, 3 đường chuẩn
        readonly System.Random rng = new System.Random(13);

        public bool Ready => Power && Warm >= WarmNeedS;
        public void SetPlot(int m) => plotMode = m;
        public bool Scanning => scanT >= 0;
        FtirLibrary Lib => InstrumentData.Instance != null ? InstrumentData.Instance.Ftir : new FtirLibrary();

        Button bPower, bClean, bScans, bRes, bBg, bSample, bStd, bMeasure, bSub, bAnalyze, bRecord, bPlot, bFinish;
        Text status;

        // =====================================================================

        public override void Build()
        {
            bPower = S.AddButton("Bật máy", 0, 0, TogglePower, true);
            bClean = S.AddButton("Lau tinh thể ATR", 1, 0, CleanCrystal, false, 1, 13);
            bBg = S.AddButton("Đo phổ nền", 2, 0, MeasureBackground);
            bScans = S.AddButton("", 0, 1, NextScans);
            bRes = S.AddButton("", 1, 1, NextRes);
            bPlot = S.AddButton("", 2, 1, () => plotMode = (plotMode + 1) % 4);
            bSample = S.AddButton("", 0, 2, PutSample, false, 2, 13);
            bStd = S.AddButton("", 2, 2, PutStd, false, 1, 13);
            bMeasure = S.AddButton("Đo phổ mẫu", 0, 3, Measure, true);
            bSub = S.AddButton("Trừ phổ nước", 1, 3, SubtractWater);
            bAnalyze = S.AddButton("Nhận diện, tra thư viện", 2, 3, Analyze, false, 1, 13);
            bRecord = S.AddButton("Ghi điểm chuẩn etanol", 0, 4, RecordStd, false, 1, 13);
            bFinish = S.AddButton("Lau tinh thể, kết thúc", 1, 4, Finish, false, 2, 13);
            status = S.AddText("", 5, 0, 3, 13);
            S.SetPlotAxes("Phổ hồng ngoại", "Số sóng (cm⁻¹)", "Độ hấp thụ A", "4000", "650", "", "");
            S.SetResults("Bật máy, chờ ổn định, lau tinh thể rồi đo phổ nền. Đặt ống mẫu vào ổ trên nóc máy ở phòng lab trước khi mở màn hình này.");
        }

        public override void Refresh()
        {
            InstrumentScreen.SetButton(bPower, Power ? "Máy đang bật" : "Bật máy", true, Power);
            InstrumentScreen.SetButton(bClean, Clean ? "Tinh thể sạch" : "Lau tinh thể ATR", Ready && !Scanning, Clean);
            InstrumentScreen.SetButton(bBg, HasBackground ? $"Nền: đo {Time.time - bgTime:0} s trước" : "Đo phổ nền", Ready && !Scanning, HasBackground);
            InstrumentScreen.SetButton(bScans, $"Số lần quét: {Scans}", !Scanning);
            InstrumentScreen.SetButton(bRes, $"Độ phân giải {Res} cm⁻¹", !Scanning);
            InstrumentScreen.SetButton(bPlot, plotMode == 0 ? "Đồ thị: phổ" : plotMode == 1 ? "Đồ thị: giao thoa đồ" : plotMode == 2 ? "Đồ thị: đã trừ nước" : "Đồ thị: đường chuẩn", true);
            InstrumentScreen.SetButton(bSample, Sample != null ? "Nhỏ mẫu: " + SampleName : "Ổ mẫu trống", Ready && !Scanning && Sample != null, OnCrystal == Src.Sample);
            InstrumentScreen.SetButton(bStd, $"Chuẩn etanol {StdEthanol[StdIndex]:0}%", Ready && !Scanning, OnCrystal == Src.Std);
            InstrumentScreen.SetButton(bMeasure, Scanning ? $"Đang quét {Mathf.Min(Scans, 1 + (int)(scanT / ScanS * Scans))}/{Scans}" : "Đo phổ mẫu", Ready && !Scanning && OnCrystal != Src.Empty);
            InstrumentScreen.SetButton(bSub, null, Spectrum.Count > 10 && !Scanning);
            InstrumentScreen.SetButton(bAnalyze, null, Spectrum.Count > 10 && !Scanning);
            InstrumentScreen.SetButton(bRecord, null, Spectrum.Count > 10 && specName.StartsWith("chuẩn") && !Scanning);
            InstrumentScreen.SetButton(bFinish, null, Ready && !Scanning);
            string st = !Power ? "tắt" : Warm < WarmNeedS ? $"đang ổn định {Warm / WarmNeedS:P0}" : "sẵn sàng";
            status.text = $"Máy: {st} · laser He-Ne · detector DLATGS · tinh thể: {(OnCrystal == Src.Empty ? Clean ? "sạch" : "<color=#FFC966>còn cặn mẫu cũ</color>" : "có mẫu")}";
            S.SetResults(BuildResults());
        }

        float ScanS => 0.6f + 0.05f * Scans;

        // =====================================================================
        // Thao tác
        // =====================================================================

        public void TogglePower()
        {
            if (Power) return;
            Power = true;
            Warm = 0;
            S.Say("Bật nguồn hồng ngoại, laser He-Ne; detector đang ổn định nhiệt…", 0);
        }

        public void CleanCrystal()
        {
            if (!Ready || Scanning) return;
            if (!S.Require("lau_tinh_the")) return;
            Clean = true;
            residue.Clear();
            OnCrystal = Src.Empty;
            S.Done("lau_tinh_the");
            S.Say("Lau tinh thể bằng khăn thấm isopropanol, để khô.", 1);
        }

        public void NextScans() { Scans = scanOpts[(Array.IndexOf(scanOpts, Scans) + 1) % scanOpts.Length]; S.Done("dat_thong_so"); }
        public void NextRes() { Res = resOpts[(Array.IndexOf(resOpts, Res) + 1) % resOpts.Length]; S.Done("dat_thong_so"); }
        public void SetScans(int n) { Scans = n; S.Done("dat_thong_so"); }

        public void MeasureBackground()
        {
            if (!Ready || Scanning) return;
            if (!S.Require("do_nen")) return;
            if (OnCrystal != Src.Empty) { S.Say("Phổ nền phải đo khi tinh thể trống, sạch. Lau tinh thể trước.", 2); return; }
            HasBackground = true;
            bgTime = Time.time;
            S.Done("do_nen");
            S.Say(Clean ? "Đã đo phổ nền (không khí, tinh thể sạch)." : "Đo nền khi tinh thể còn cặn: các dải của cặn bị trừ đi, phổ mẫu sau sẽ có dải âm.", Clean ? 1 : 2);
            bgResidue = Clean ? new List<FtirLibrary.Part>() : residue.ToList();  // nền chứa cặn: lau sau đó thì phổ mẫu có dải âm
        }

        public void PutSample()
        {
            if (!Ready || Sample == null) return;
            if (!S.Require("nho_mau")) return;
            OnCrystal = Src.Sample;
            S.Done("nho_mau");
            S.Say($"Nhỏ 2 giọt {SampleName} phủ kín tinh thể.", 0);
        }

        public void PutStd()
        {
            if (!Ready) return;
            if (OnCrystal == Src.Std) StdIndex = (StdIndex + 1) % StdEthanol.Length;
            OnCrystal = Src.Std;
            S.Say($"Nhỏ chuẩn etanol {StdEthanol[StdIndex]:0}% thể tích lên tinh thể.", 0);
        }

        public void SelectStd(int i) { StdIndex = Mathf.Clamp(i, 0, StdEthanol.Length - 1); OnCrystal = Src.Std; }

        public void Measure()
        {
            if (!Ready || Scanning || OnCrystal == Src.Empty) return;
            if (OnCrystal == Src.Sample && !S.Require("do_mau")) return;
            Spectrum.Clear();
            Subtracted = null;
            Unmixed = null;
            Hits = null;
            Bands = null;
            interferogram.Clear();
            specName = OnCrystal == Src.Sample ? SampleName : $"chuẩn etanol {StdEthanol[StdIndex]:0}%";
            scanT = 0;
            plotMode = 1;
            S.Say($"Quét {Scans} lần, độ phân giải {Res} cm⁻¹: gương di động chạy, máy thu giao thoa đồ…", 0);
        }

        public void SubtractWater()
        {
            if (Spectrum.Count < 10) return;
            var w = Lib.Water;
            if (w == null) return;
            // hệ số nước khớp theo dải tổ hợp 1900 – 2290 cm⁻¹ (chỉ nước hấp thụ, tránh vùng CO₂)
            double ab = 0, bb = 0;
            foreach (var (nu, a) in Spectrum)
                if (nu >= 1900 && nu <= 2290) { double r = w.A(nu); ab += a * r; bb += r * r; }
            WaterScale = bb > 0 ? ab / bb : 0;
            Subtracted = Spectrum.Select(s => (s.nu, s.a - WaterScale * w.A(s.nu))).ToList();
            plotMode = 2;
            S.Done("xu_ly");
            S.Say($"Đã trừ phổ nước (hệ số {Fmt(WaterScale, "0.000")}): còn lại dải của chất tan.", 1);
        }

        public void Analyze()
        {
            if (Spectrum.Count < 10) return;
            if (Subtracted == null && Spectrum.Max(s => s.a) > 0.3) SubtractWater();
            var src = Subtracted ?? Spectrum;
            var region = src.Where(s => (s.nu >= 900 && s.nu <= 1800) || (s.nu >= 2800 && s.nu <= 3100)).ToList();
            Unmixed = Lib.Unmix(region, Subtracted != null, out double rms);
            Hits = Lib.Search(region.Where(s => s.nu <= 1800).ToList()).Take(3).ToList();
            double thr = Math.Max(0.01, 4 * NoiseSigma);
            Bands = FtirLibrary.Peaks(src.Where(s => s.nu < 2290 || s.nu > 2400 || !HasBackground).ToList(), thr).Take(8).Select(b => (b.nu, b.a, Lib.Assign(b.nu))).OrderByDescending(b => b.nu).ToList();
            S.Done("xu_ly");
            S.Say(Unmixed.Count == 0 ? "Không tách được chất nào (dải quá yếu so với nhiễu)" : $"Tách được {Unmixed.Count} thành phần; thư viện khớp nhất: {Hits[0].p.Label} (HQI {Hits[0].hqi:0})", Unmixed.Count > 0 ? 1 : 2);
            if (Cal.Count >= 2 && OnCrystal == Src.Sample)
            {
                var f = UvVisLibrary.Linear(Cal.Select(c => (c.c, c.a)).ToList());
                EthanolPct = f.X(Height1045(src));
            }
        }

        public void RecordStd()
        {
            if (Spectrum.Count < 10 || !specName.StartsWith("chuẩn")) return;
            if (Subtracted == null) SubtractWater();
            double h = Height1045(Subtracted);
            Cal.RemoveAll(c => c.c == StdEthanol[StdIndex]);
            Cal.Add((StdEthanol[StdIndex], h));
            Cal.Sort();
            plotMode = 3;
            S.Say($"Chuẩn etanol {StdEthanol[StdIndex]:0}%: chiều cao dải C-O 1045 cm⁻¹ = {Fmt(h, "0.0000")}" + (Cal.Count >= 2 ? $" · R² {Fmt(UvVisLibrary.Linear(Cal.Select(c => (c.c, c.a)).ToList()).R2, "0.0000")}" : ""), 1);
            if (StdIndex < StdEthanol.Length - 1) StdIndex++;
        }

        /// <summary>Chiều cao dải 1045 cm⁻¹ so với đường nền nối 1000 và 1110 cm⁻¹.</summary>
        static double Height1045(IList<(double nu, double a)> s)
        {
            double At(double nu) => s.OrderBy(p => Math.Abs(p.nu - nu)).First().a;
            return At(1045) - (At(1000) + At(1110)) / 2;
        }

        public void Finish()
        {
            if (!Ready) return;
            Clean = true;
            residue.Clear();
            OnCrystal = Src.Empty;
            if (S.DoneSteps.Contains("do_mau")) S.Done("ket_thuc");
            S.Say("Đã lau sạch mẫu khỏi tinh thể.", 0);
        }

        // =====================================================================
        // Mô phỏng
        // =====================================================================

        double NoiseSigma => 0.0025 / Math.Sqrt(Scans);

        List<FtirLibrary.Part> CrystalParts()
        {
            var parts = new List<FtirLibrary.Part>();
            unknown.Clear();
            if (OnCrystal == Src.Sample && Sample != null)
                parts.AddRange(Lib.Composition(Sample.Total.Mix, Sample.VolumeMl, Sample.BulkPh, unknown));
            else if (OnCrystal == Src.Std)
            {
                double c = StdEthanol[StdIndex] * 10 * 0.789 / 46.07;
                parts.AddRange(Lib.Composition(Mix.Of(("C2H5OH", c)), 1000, 7, null));
            }
            if (!Clean) parts.AddRange(residue.Select(r => { r.Amount *= 0.15; return r; }));  // màng cặn mỏng
            parts.AddRange(bgResidue.Select(r => { r.Amount *= -0.15; return r; }));          // cặn có trong phổ nền bị trừ đi
            return parts;
        }

        /// <summary>Độ hấp thụ ở nu có tính độ phân giải (dải bị doãng khi độ phân giải thô) và khí quyển chưa trừ.</summary>
        double TrueA(IList<FtirLibrary.Part> parts, double nu, double co2, double h2o)
        {
            double a = 0, res = 1.2 * Res;
            foreach (var p in parts)
                foreach (var (c, amp, w) in p.Ion ? p.E.IonBands : p.E.Bands)
                {
                    double we = Math.Sqrt(w * w + res * res);
                    a += p.Amount * amp * w / we * FtirLibrary.Shape(nu, c, we);
                }
            return a + FtirLibrary.Atmosphere(nu, co2, h2o);
        }

        /// <summary>Lệch khí quyển so với lúc đo nền (CO₂, hơi nước trong buồng máy thay đổi theo thời gian).</summary>
        (double co2, double h2o) AtmosphereResidual()
        {
            if (!HasBackground) return (0.35, 0.08);  // dùng nền cũ của nhà máy: lệch rất nhiều
            double age = Time.time - bgTime;
            double drift = Math.Min(1, age / 600.0);
            return (0.02 + 0.12 * drift, 0.004 + 0.02 * drift);
        }

        public override void Tick(float dt)
        {
            if (Power && Warm < WarmNeedS)
            {
                Warm += dt;
                if (Warm >= WarmNeedS) { S.Done("bat_may"); S.Say("Máy đã ổn định.", 1); }
            }
            if (!Scanning) return;
            scanT += dt;
            if (interferogram.Count == 0) BuildInterferogram();
            if (scanT >= ScanS) FinishScan();
        }

        List<(double nu, double a)> Compute()
        {
            var parts = CrystalParts();
            var (co2, h2o) = AtmosphereResidual();
            var res = new List<(double nu, double a)>();
            for (double nu = NuHi; nu >= NuLo; nu -= 4)
            {
                double a = TrueA(parts, nu, co2, h2o);
                double t = Math.Pow(10, -Math.Max(0, a));
                res.Add((nu, a + Gauss() * NoiseSigma / Math.Max(0.15, t)));  // nhiễu lớn hơn ở vùng hấp thụ mạnh (ít sáng tới detector)
            }
            return res;
        }

        void BuildInterferogram()
        {
            var spec = Compute();
            // phổ đơn chùm: nguồn (bao hình gần vật đen) × truyền qua; giao thoa đồ = Σ B(ν)·cos(2πνδ)
            const int n = 600;
            const double maxDelta = 0.02;  // cm
            for (int i = 0; i < n; i++)
            {
                double delta = (i - n / 2) * maxDelta * 2 / n, s = 0;
                for (int k = 0; k < spec.Count; k += 2)
                {
                    double nu = spec[k].nu, src = Math.Exp(-Math.Pow((nu - 1900) / 1500, 2));
                    s += src * Math.Pow(10, -Math.Max(0, spec[k].a)) * Math.Cos(2 * Math.PI * nu * delta);
                }
                interferogram.Add(s);
            }
            pending = spec;  // phổ hiện sau khi quét xong
        }

        List<(double nu, double a)> pending;

        void FinishScan()
        {
            scanT = -1;
            Spectrum.Clear();
            if (pending != null) Spectrum.AddRange(pending);
            pending = null;
            plotMode = 0;
            if (OnCrystal == Src.Sample)
            {
                S.Done("do_mau");
                residue = CrystalParts().Where(p => p.E.Id != "nuoc").ToList();
                Clean = false;
            }
            else if (OnCrystal == Src.Std) { residue = CrystalParts().Where(p => p.E.Id != "nuoc").ToList(); Clean = false; }
            var (co2, h2o) = AtmosphereResidual();
            S.Say($"Đã đo phổ {specName}." + (co2 > 0.1 ? " Có dải CO₂ ở 2350 cm⁻¹ và răng cưa hơi nước: phổ nền chưa đo hoặc đã cũ." : ""), co2 > 0.1 ? 2 : 1);
            if (Scans <= 4) S.Say($"Chỉ quét {Scans} lần: phổ nhiễu (nhiễu giảm theo √N; 64 lần nhiễu nhỏ hơn 1 lần 8 lần).", 2);


        }

        double Gauss()
        {
            double u1 = 1 - rng.NextDouble(), u2 = rng.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }

        // =====================================================================
        // Kết quả
        // =====================================================================

        string BuildResults()
        {
            var sb = new StringBuilder();
            sb.Append($"<color=#9FB3C8>Thông số:</color> {Scans} lần quét, độ phân giải {Res} cm⁻¹, ATR kim cương · nhiễu ≈ {Fmt(NoiseSigma * 1000, "0.0")} mA\n");
            if (Spectrum.Count > 10)
            {
                var top = FtirLibrary.Peaks(Spectrum, 0.05).Take(4).OrderByDescending(p => p.nu);
                sb.Append($"\n<b>Phổ {specName}</b>: dải mạnh {string.Join(", ", top.Select(p => $"{p.nu:0}"))} cm⁻¹\n");
                if (Spectrum.Max(s => s.a) > 0.3 && Subtracted == null) sb.Append("<color=#FFC966>Dải nước (3330, 1638 cm⁻¹) che chất tan: bấm Trừ phổ nước.</color>\n");
            }
            if (Bands != null && Bands.Count > 0)
            {
                sb.Append("\n<b>Gán dải theo nhóm chức</b>\n");
                foreach (var b in Bands) sb.Append($"{b.nu:0} cm⁻¹ (A {Fmt(b.a, "0.000")}): {b.group}\n");
            }
            if (Unmixed != null)
            {
                sb.Append("\n<b>Phân tích chuyên sâu: tách hỗn hợp</b>\n<color=#9FB3C8>Phổ = Σ phổ chuẩn × lượng (bình phương tối thiểu không âm, vùng 900 – 1800 và 2800 – 3100 cm⁻¹)</color>\n");
                foreach (var u in Unmixed.Take(5))
                {
                    double molL = (u.Ion ? u.E.IonLiquid : u.E.Liquid) ? u.Amount * u.E.Density * 1000 / u.E.M : u.Amount;
                    string amt = (u.Ion ? u.E.IonLiquid : u.E.Liquid) ? $"{Fmt(u.Amount * 100, "0.0")}% thể tích ≈ {Fmt(molL, "0.00")} mol/L" : $"≈ {Fmt(molL, "0.00")} mol/L";
                    sb.Append($"• <color=#61DBFF>{u.Label}</color>: {amt}\n");
                }
                if (Hits != null) sb.Append($"<color=#9FB3C8>Tra thư viện (vùng vân tay): {string.Join("; ", Hits.Select(h => $"{h.p.Label} {h.hqi:0}"))}</color>\n");
            }
            if (!double.IsNaN(EthanolPct)) sb.Append($"\n<b>Định lượng etanol (dải 1045 cm⁻¹)</b>: <b>{Fmt(EthanolPct, "0.0")}% thể tích</b>\n");
            if (unknown.Count > 0) sb.Append($"\n<color=#FFC966>Chất hữu cơ ngoài thư viện phổ (không bịa dải):</color> {string.Join(", ", unknown.Distinct())}\n");
            return sb.ToString();
        }

        // =====================================================================
        // Sơ đồ
        // =====================================================================

        public override void DrawSchematic(Painter p, float time)
        {
            SchemKit.Begin(p);
            var src = new Vector2(80, 230);
            var bs = new Vector2(300, 230);
            var fixedM = new Vector2(300, 320);
            float mirrorX = 520 + (Scanning ? Mathf.Sin(time * 9) * 30 : 0);
            var atr = new Vector2(600, 90);
            var det = new Vector2(880, 90);
            Color ir = new Color(1f, 0.45f, 0.3f, 0.85f);
            float ph = time * 80;
            // nguồn
            SchemKit.Box(p, src.x - 40, src.y - 30, 80, 60);
            p.Disc(src.x, src.y, 12, Ready ? new Color(1f, 0.5f, 0.2f) : SchemKit.Off);
            // bộ chia chùm (vạch chéo)
            p.Line(bs.x - 30, bs.y - 30, bs.x + 30, bs.y + 30, new Color(0.8f, 0.85f, 0.95f), 5);
            // gương cố định, gương di động
            p.FillRect(fixedM.x - 40, fixedM.y, 80, 8, new Color(0.85f, 0.9f, 0.95f));
            p.FillRect(mirrorX, bs.y - 40, 8, 80, new Color(0.85f, 0.9f, 0.95f));
            p.FillRect(mirrorX + 8, bs.y - 6, 60, 12, SchemKit.Body);  // trục động cơ
            if (Ready)
            {
                Beam(p, src + new Vector2(40, 0), bs, ir, ph);
                Beam(p, bs, new Vector2(bs.x, fixedM.y), ir, ph);
                Beam(p, bs, new Vector2(mirrorX, bs.y), ir, ph);
                Beam(p, bs, new Vector2(bs.x, atr.y + 20), ir, ph);
                // laser He-Ne song song
                p.Line(src.x + 40, src.y + 18, mirrorX, src.y + 18, new Color(1f, 0.2f, 0.2f, 0.7f), 2);
                Beam(p, new Vector2(bs.x, atr.y + 20), new Vector2(atr.x - 60, atr.y + 20), ir, ph);
                Beam(p, new Vector2(atr.x + 60, atr.y + 20), new Vector2(det.x - 30, atr.y + 20), ir, ph);
            }
            // tinh thể ATR (hình thang) + mẫu
            p.FillPoly(new[] { new Vector2(atr.x - 70, atr.y + 40), new Vector2(atr.x + 70, atr.y + 40), new Vector2(atr.x + 40, atr.y), new Vector2(atr.x - 40, atr.y) }, new Color(0.75f, 0.9f, 1f, 0.45f));
            if (Ready)
            {
                p.Line(atr.x - 60, atr.y + 20, atr.x, atr.y + 38, ir, 3);
                p.Line(atr.x, atr.y + 38, atr.x + 60, atr.y + 20, ir, 3);
                for (int i = 0; i < 5; i++) p.Line(atr.x - 12 + i * 6, atr.y + 40, atr.x - 12 + i * 6, atr.y + 40 + 10 * Mathf.Abs(Mathf.Sin(time * 6 + i)), new Color(1f, 0.6f, 0.4f, 0.5f), 2);
            }
            if (OnCrystal != Src.Empty || !Clean)
            {
                var lq = Sample != null ? Sample.Look.Liquid : default;
                Color c = OnCrystal == Src.Sample && Sample != null ? new Color(lq.r, lq.g, lq.b) : new Color(0.85f, 0.9f, 1f);
                c.a = OnCrystal == Src.Empty ? 0.25f : 0.7f;
                p.FillPoly(new[] { new Vector2(atr.x - 35, atr.y + 40), new Vector2(atr.x + 35, atr.y + 40), new Vector2(atr.x + 22, atr.y + 58), new Vector2(atr.x - 22, atr.y + 58) }, c);
            }
            // detector + tín hiệu
            SchemKit.Box(p, det.x - 30, det.y - 10, 60, 60);
            if (Scanning && interferogram.Count > 0)
            {
                int i = (int)(Mathf.Repeat(time * 2, 1) * (interferogram.Count - 1));
                double mx = interferogram.Max(Math.Abs);
                p.FillRect(det.x + 36, det.y - 10, 8, 60 * Mathf.Clamp01((float)(0.5 + 0.5 * interferogram[i] / mx)), SchemKit.Good);
            }
            var d = Doc;
            S.SchemLabel("nguon", d.PartName("nguon_ir"), src.x, src.y - 50, 11);
            S.SchemLabel("bs", d.PartName("chia_chum"), bs.x - 70, bs.y - 40, 11);
            S.SchemLabel("gcd", d.PartName("guong_co_dinh"), fixedM.x, fixedM.y + 22, 11);
            S.SchemLabel("gdd", d.PartName("guong_di_dong") + (Scanning ? "\n<color=#61DBFF>đang chạy</color>" : ""), mirrorX + 40, bs.y + 62, 11);
            S.SchemLabel("laser", d.PartName("laser"), 190, src.y + 34, 10, new Color(1f, 0.55f, 0.55f));
            S.SchemLabel("atr", d.PartName("atr"), atr.x, atr.y - 18, 11);
            S.SchemLabel("det", d.PartName("detector"), det.x, det.y + 68, 11);
            S.SchemLabel("trang_thai", !Power ? "<color=#8899AA>MÁY TẮT</color>" : Warm < WarmNeedS ? $"<color=#FFC966>ĐANG ỔN ĐỊNH {Warm / WarmNeedS:P0}</color>" : Scanning ? "<color=#61DBFF>ĐANG QUÉT: thu giao thoa đồ</color>" : "<color=#73F29E>SẴN SÀNG</color>", 620, 330, 14);
        }

        static void Beam(Painter p, Vector2 a, Vector2 b, Color c, float phase)
        {
            var k = c;
            k.a = 0.3f;
            p.Line(a.x, a.y, b.x, b.y, k, 6);
            p.Dashed(a.x, a.y, b.x, b.y, c, 3, 10, phase);
        }

        // =====================================================================
        // Đồ thị
        // =====================================================================

        public override void DrawPlot(Painter p)
        {
            InstrumentScreen.PlotFrame(p);
            if (plotMode == 1) { DrawInterferogram(p); return; }
            if (plotMode == 3 && Cal.Count > 0) { DrawCalibration(p); return; }
            var spec = plotMode == 2 && Subtracted != null ? Subtracted : Spectrum;
            double yMax = plotMode == 2 && Subtracted != null ? 0.05 : 0.2, yMin = 0;
            foreach (var s in spec) { yMax = Math.Max(yMax, s.a * 1.1); yMin = Math.Min(yMin, s.a * 1.1); }
            yMax = Math.Min(yMax, 2);
            S.SetPlotAxes(plotMode == 2 && Subtracted != null ? $"Phổ {specName} đã trừ nước" : $"Phổ hồng ngoại {specName}", "Số sóng (cm⁻¹)", "Độ hấp thụ A", "4000", "650", Fmt(yMin, "0.00"), Fmt(yMax, "0.00"));
            // vạch 0
            var z0 = InstrumentScreen.Map(p, NuHi, 0, NuHi, NuLo, yMin, yMax);
            p.FillRect(0, z0.y, p.W, 1, new Color(1, 1, 1, 0.25f));
            if (Unmixed != null && plotMode == 2)
            {
                Color[] cols = { new Color(1f, 0.6f, 0.9f, 0.8f), new Color(1f, 0.75f, 0.3f, 0.8f), new Color(0.5f, 1f, 0.6f, 0.8f), new Color(0.6f, 0.7f, 1f, 0.8f) };
                int k = 0;
                foreach (var u in Unmixed.Take(4))
                {
                    var pts = new List<Vector2>();
                    for (double nu = NuHi; nu >= NuLo; nu -= 8) pts.Add(InstrumentScreen.Map(p, nu, Math.Min(yMax, u.A(nu)), NuHi, NuLo, yMin, yMax));
                    var col = cols[k++ % cols.Length];
                    for (int i = 1; i < pts.Count; i += 2) p.Line(pts[i - 1].x, pts[i - 1].y, pts[i].x, pts[i].y, col, 2);
                }
            }
            if (spec.Count > 1) p.Poly(spec.Select(s => InstrumentScreen.Map(p, s.nu, Math.Max(yMin, Math.Min(yMax, s.a)), NuHi, NuLo, yMin, yMax)).ToList(), new Color(1f, 0.78f, 0.43f), 2);
            if (Bands != null)
                foreach (var b in Bands) { var q = InstrumentScreen.Map(p, b.nu, Math.Min(yMax, b.a), NuHi, NuLo, yMin, yMax); p.Disc(q.x, q.y, 4, new Color(0.45f, 0.86f, 1f)); }
        }

        void DrawInterferogram(Painter p)
        {
            S.SetPlotAxes("Giao thoa đồ (tín hiệu detector theo vị trí gương)", "Hiệu quang trình δ (cm)", "Tín hiệu", "−0,02", "+0,02", "", "");
            if (interferogram.Count < 2) return;
            double mx = interferogram.Max(Math.Abs);
            int show = Scanning ? (int)(interferogram.Count * Mathf.Clamp01(scanT / ScanS)) : interferogram.Count;
            var pts = new List<Vector2>();
            for (int i = 0; i < show; i++) pts.Add(new Vector2(3 + i * (p.W - 6f) / interferogram.Count, p.H / 2f + (float)(interferogram[i] / mx) * (p.H / 2f - 8)));
            p.Poly(pts, new Color(0.6f, 0.9f, 0.7f), 2);
        }

        void DrawCalibration(Painter p)
        {
            double cMax = StdEthanol[StdEthanol.Length - 1] * 1.2, aMax = Math.Max(0.05, Cal.Max(c => c.a) * 1.2);
            S.SetPlotAxes("Đường chuẩn etanol: chiều cao dải C-O 1045 cm⁻¹", "Etanol (% thể tích)", "Chiều cao dải (A)", "0", Fmt(cMax, "0"), "0", Fmt(aMax, "0.000"));
            if (Cal.Count >= 2)
            {
                var f = UvVisLibrary.Linear(Cal.Select(c => (c.c, c.a)).ToList());
                var a0 = InstrumentScreen.Map(p, 0, f.Intercept, 0, cMax, 0, aMax);
                var a1 = InstrumentScreen.Map(p, cMax, f.Intercept + f.Slope * cMax, 0, cMax, 0, aMax);
                p.Line(a0.x, a0.y, a1.x, a1.y, new Color(0.43f, 0.86f, 1f, 0.8f), 2);
            }
            foreach (var c in Cal) { var q = InstrumentScreen.Map(p, c.c, c.a, 0, cMax, 0, aMax); p.Disc(q.x, q.y, 6, SchemKit.Good); }
        }
    }
}
