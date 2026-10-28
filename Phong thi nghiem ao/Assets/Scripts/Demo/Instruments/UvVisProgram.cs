using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Khkt.Simulation.Chem;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Demo.Instruments
{
    /// <summary>
    /// Máy quang phổ UV-Vis hai chùm tia (thông số theo Shimadzu UV-1800 / UV-1900i, quy trình theo hướng dẫn của ĐH Trà Vinh
    /// và Viện Kiểm nghiệm ATVSTP Quốc gia). Số đo mô phỏng các lỗi thật: chưa đo nền thì cộng hấp thụ của dung môi, cuvet;
    /// đèn chưa nóng thì trôi; cuvet nhựa / thủy tinh dưới ~340 nm thì thiếu sáng, số đo nhiễu và bị cắt; A cao thì ánh sáng lạc
    /// làm số đo bão hòa (~3,3). Phổ của mẫu dựng từ thư viện dải hấp thụ có nguồn; hỗn hợp được tách theo phổ chuẩn từng chất.
    /// </summary>
    public class UvVisProgram : InstrumentProgram
    {
        public override string Code => "uv_vis";
        static readonly string[] optional = { "day_chuan", "ket_thuc" };
        public override ICollection<string> Optional => optional;

        public const float SelfTestS = 3f, WarmNeedS = 6f;
        const double StrayLight = 0.0005;  // ánh sáng lạc (tỉ lệ so với chùm sáng đầy đủ), mô phỏng
        static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        enum Mode { Lambda, Scan, Quant }
        public enum Src { Blank, Std, Sample }

        // ---- trạng thái máy ----
        public bool Power { get; private set; }
        public float SelfTest { get; private set; }
        public float Warm { get; private set; }
        Mode mode = Mode.Lambda;
        public int Lambda { get; private set; } = 525;
        public string Cuvette { get; private set; } = "nhua";
        public Src Source { get; private set; } = Src.Blank;
        public int StdIndex { get; private set; }
        public int Dilution { get; private set; } = 1;
        public bool Baseline { get; private set; }
        float baselineWarm, baselineTime;
        double baselineError;  // khi đo nền bằng dung dịch không phải mẫu trắng (Thực hành)
        int analyteIdx;
        bool plotCalibration;

        // ---- kết quả ----
        public double LastA { get; private set; } = double.NaN;
        public readonly List<(double c, double a)> CalPoints = new List<(double, double)>();
        public UvVisLibrary.Fit CalFit { get; private set; }
        public double SampleConc { get; private set; } = double.NaN;  // mol/L trong mẫu gốc (đã nhân pha loãng)
        double sampleA = double.NaN;
        public readonly List<(double l, double a)> Spectrum = new List<(double, double)>();
        float scanT = -1;
        int scanFrom = 300, scanTo = 800;
        public List<(UvVisLibrary.Entry e, double c)> Unmixed { get; private set; }
        public double UnmixRms { get; private set; } = double.NaN;
        readonly List<string> outside = new List<string>();
        readonly System.Random rng = new System.Random(7);

        Button bPower, bMode, bCuv, bLambda, bBlank, bStd, bSample, bDil, bAnalyte, bZero, bMeasure, bScan, bRecord, bQuant, bUnmix, bPlot;
        Text status;

        UvVisLibrary Lib => InstrumentData.Instance != null ? InstrumentData.Instance.Uv : new UvVisLibrary();
        List<UvVisLibrary.Entry> Analytes => Lib.Entries.Where(e => e.Id != "pp").ToList();
        UvVisLibrary.Entry Analyte => Analytes.Count > 0 ? Analytes[analyteIdx % Analytes.Count] : null;
        public bool Ready => Power && SelfTest >= SelfTestS;

        public double[] StdConc
        {
            get
            {
                var a = Analyte;
                if (a == null) return new double[0];
                double peak = a.Eps(a.LambdaMax(200, 900));
                double hi = 1.1 / Math.Max(1, peak);
                double p = Math.Pow(10, Math.Floor(Math.Log10(hi)));
                hi = Math.Round(hi / p * 10) / 10 * p;
                return new[] { 0.2, 0.4, 0.6, 0.8, 1.0 }.Select(f => f * hi).ToArray();
            }
        }

        // =====================================================================
        // Nút
        // =====================================================================

        public override void Build()
        {
            bPower = S.AddButton("Bật máy", 0, 0, TogglePower, true);
            bMode = S.AddButton("", 1, 0, NextMode);
            bCuv = S.AddButton("", 2, 0, NextCuvette);
            S.AddButton("λ −10", 0, 1, () => SetLambda(Lambda - 10));
            bLambda = S.AddButton("", 1, 1, null);
            S.AddButton("λ +10", 2, 1, () => SetLambda(Lambda + 10));
            S.AddButton("λ −1", 0, 2, () => SetLambda(Lambda - 1));
            S.AddButton("Đặt λmax", 1, 2, SetLambdaMax);
            S.AddButton("λ +1", 2, 2, () => SetLambda(Lambda + 1));
            bBlank = S.AddButton("Mẫu trắng", 0, 3, () => SetSource(Src.Blank));
            bStd = S.AddButton("", 1, 3, NextStd);
            bSample = S.AddButton("Mẫu ở ổ máy", 2, 3, () => SetSource(Src.Sample));
            bDil = S.AddButton("", 0, 4, NextDilution);
            bAnalyte = S.AddButton("", 1, 4, NextAnalyte, false, 2);
            bZero = S.AddButton("Đo nền (auto zero)", 0, 5, AutoZero, false, 1, 13);
            bMeasure = S.AddButton("Đo", 1, 5, MeasureNow);
            bScan = S.AddButton("Quét phổ", 2, 5, StartScan);
            bRecord = S.AddButton("Ghi điểm chuẩn", 0, 6, RecordStandard);
            bQuant = S.AddButton("Đo mẫu, tính c", 1, 6, QuantifySample);
            bUnmix = S.AddButton("Tách hỗn hợp", 2, 6, UnmixNow);
            bPlot = S.AddButton("", 0, 7, () => plotCalibration = !plotCalibration);
            S.AddButton("Tắt máy", 1, 7, TurnOff);
            status = S.AddText("", 8, 0, 3, 13);
            S.SetPlotAxes("Phổ hấp thụ", "Bước sóng λ (nm)", "Độ hấp thụ A", "", "", "", "");
            S.SetResults("Bật máy để bắt đầu. Đặt ống mẫu / cuvet vào ổ trên nóc máy ở phòng lab trước khi mở màn hình này.");
        }

        public override void Refresh()
        {
            InstrumentScreen.SetButton(bPower, Power ? "Máy đang bật" : "Bật máy", true, Power);
            InstrumentScreen.SetButton(bMode, mode == Mode.Lambda ? "Chế độ: đo tại λ" : mode == Mode.Scan ? "Chế độ: quét phổ" : "Chế độ: định lượng", Ready);
            InstrumentScreen.SetButton(bCuv, "Cuvet: " + CuvetteName(Cuvette), true);
            InstrumentScreen.SetButton(bLambda, $"λ = {Lambda} nm", false, true);
            InstrumentScreen.SetButton(bBlank, null, Ready, Source == Src.Blank);
            InstrumentScreen.SetButton(bStd, $"Chuẩn {StdIndex + 1}/5: {Fmt(StdConc.Length > 0 ? StdConc[StdIndex] * 1000 : 0, "0.###")} mM", Ready, Source == Src.Std);
            InstrumentScreen.SetButton(bSample, Sample != null ? "Mẫu: " + Short(SampleName) : "Ổ mẫu trống", Ready && Sample != null, Source == Src.Sample);
            InstrumentScreen.SetButton(bDil, $"Pha loãng mẫu ×{Dilution}", Ready, Dilution > 1);
            InstrumentScreen.SetButton(bAnalyte, "Dãy chuẩn: " + (Analyte != null ? Analyte.Name : "-"), Ready);
            InstrumentScreen.SetButton(bZero, null, Ready, false);
            InstrumentScreen.SetButton(bMeasure, null, Ready && scanT < 0);
            InstrumentScreen.SetButton(bScan, scanT >= 0 ? "Đang quét…" : "Quét phổ", Ready && scanT < 0);
            InstrumentScreen.SetButton(bRecord, null, Ready && Source == Src.Std);
            InstrumentScreen.SetButton(bQuant, null, Ready && Source == Src.Sample);
            InstrumentScreen.SetButton(bUnmix, null, Spectrum.Count > 10 && scanT < 0);
            InstrumentScreen.SetButton(bPlot, plotCalibration ? "Đồ thị: đường chuẩn" : "Đồ thị: phổ", true);
            string lamp = !Power ? "tắt" : SelfTest < SelfTestS ? "đang tự kiểm tra" : Warm < WarmNeedS ? $"đang làm nóng ({Warm / WarmNeedS:P0})" : "ổn định";
            status.text = $"Đèn: {lamp} · nguồn sáng {(Lambda < 340 ? "D₂" : "W-halogen")} · nền: {(Baseline ? "đã đo" : "chưa đo")}";
            S.SetResults(BuildResults());
        }


        // ---- chọn trực tiếp (bài kiểm thử, phím tắt) ----
        public void SelectMode(int m) { mode = (Mode)Mathf.Clamp(m, 0, 2); S.Done("chon_che_do"); plotCalibration = mode == Mode.Quant; }
        public void SelectCuvette(string code) { Cuvette = code; S.Done("chon_cuvet"); }
        public void SelectAnalyte(string id) { int i = Analytes.FindIndex(e => e.Id == id); if (i >= 0 && i != analyteIdx) { analyteIdx = i; CalPoints.Clear(); CalFit = default; SampleConc = double.NaN; } }
        public void SelectStd(int i) { StdIndex = Mathf.Clamp(i, 0, 4); Source = Src.Std; }
        public void SetDilution(int d) => Dilution = Math.Max(1, d);
        public bool Scanning => scanT >= 0;
        // =====================================================================
        // Thao tác
        // =====================================================================

        public void TogglePower()
        {
            if (Power) return;
            Power = true;
            SelfTest = 0;
            Warm = 0;
            S.Say("Máy tự kiểm tra: cách tử về vị trí gốc, bật đèn D₂ và W-halogen…", 0);
        }

        public void TurnOff()
        {
            if (!Power) return;
            Power = false;
            Baseline = false;
            if (S.DoneSteps.Contains("do_mau")) S.Done("ket_thuc");
            S.Say("Đã tắt đèn và máy. Nhớ rửa cuvet bằng nước cất.", 0);
        }

        public void NextMode()
        {
            if (!Ready) return;
            mode = (Mode)(((int)mode + 1) % 3);
            S.Done("chon_che_do");
            if (mode == Mode.Quant) plotCalibration = true;
            if (mode == Mode.Scan) plotCalibration = false;
        }

        public void NextCuvette()
        {
            var cs = Lib.Cuvettes;
            if (cs.Count == 0) return;
            int i = cs.FindIndex(c => c.code == Cuvette);
            Cuvette = cs[(i + 1) % cs.Count].code;
            S.Done("chon_cuvet");
            if (Baseline) S.Say("Đổi cuvet: nên đo nền lại với cuvet mới", 2);
        }

        public void SetLambda(int l)
        {
            Lambda = Mathf.Clamp(l, 190, 1100);
        }

        public void SetLambdaMax()
        {
            if (Spectrum.Count < 10) { S.Say("Quét phổ trước để máy tìm λmax", 2); return; }
            var top = Spectrum.Where(p => p.l >= 340).OrderByDescending(p => p.a).FirstOrDefault();
            if (top.a <= 0.02) { S.Say("Phổ không có đỉnh rõ (A quá nhỏ)", 2); return; }
            Lambda = (int)Math.Round(top.l);
            S.Say($"Đặt λ = λmax = {Lambda} nm: độ nhạy cao nhất, ít ảnh hưởng của sai số bước sóng", 1);
        }

        public void SetSource(Src s)
        {
            if (!Ready) return;
            if (s == Src.Sample && Sample == null) { S.Say("Ổ mẫu trống: đặt ống mẫu / cuvet vào ổ trên nóc máy ở phòng lab", 2); return; }
            Source = s;
            S.Say(s == Src.Blank ? "Đặt mẫu trắng (nước cất) vào cuvet" : s == Src.Std ? $"Đặt chuẩn {StdIndex + 1}" : $"Đặt mẫu {SampleName} (pha loãng ×{Dilution})", 0);
        }

        public void NextStd()
        {
            if (!Ready) return;
            if (Source == Src.Std) StdIndex = (StdIndex + 1) % 5;
            Source = Src.Std;
            S.Say($"Đặt chuẩn {StdIndex + 1}: {Analyte?.Name} {Fmt(StdConc[StdIndex] * 1000, "0.###")} mM", 0);
        }

        public void NextDilution()
        {
            Dilution = Dilution == 1 ? 2 : Dilution == 2 ? 5 : Dilution == 5 ? 10 : Dilution == 10 ? 100 : 1;
            S.Say($"Mẫu pha loãng ×{Dilution} bằng bình định mức (nước cất)", 0);
        }

        public void NextAnalyte()
        {
            analyteIdx = (analyteIdx + 1) % Math.Max(1, Analytes.Count);
            CalPoints.Clear();
            CalFit = default;
            SampleConc = double.NaN;
            S.Say($"Dãy chuẩn mới: {Analyte?.Name}. Đường chuẩn cũ đã xóa.", 0);
        }

        public void AutoZero()
        {
            if (!Ready) { S.Say("Máy chưa sẵn sàng", 2); return; }
            if (!S.Require("do_nen")) return;
            if (S.Guided && Source != Src.Blank) { S.Say("Đo nền phải dùng mẫu trắng: bấm \"Mẫu trắng\" trước", 2); return; }
            Baseline = true;
            baselineWarm = Warm;
            baselineTime = Time.time;
            baselineError = Source == Src.Blank ? 0 : TrueA(Lambda);  // đo nền bằng dung dịch có chất: mọi số đo sau bị trừ sai
            S.Done("do_nen");
            if (Source != Src.Blank) S.Say("Đã đo nền bằng dung dịch không phải mẫu trắng: các số đo sau sẽ bị trừ sai!", 3);
            else if (Warm < WarmNeedS) S.Say("Đã đo nền khi đèn chưa ổn định: đường nền sẽ trôi dần", 2);
            else S.Say("Đã đo nền (auto zero): A = 0 với mẫu trắng ở mọi bước sóng", 1);
        }

        public void MeasureNow()
        {
            if (!Ready) return;
            if (Source == Src.Sample && !S.Require("do_mau")) return;
            if (Source != Src.Sample && !Baseline && !S.Require("day_chuan")) return;
            if (mode != Mode.Lambda) S.Done("chon_che_do");
            LastA = Read(Lambda);
            if (Source == Src.Sample) { S.Done("do_mau"); sampleA = LastA; }
            S.Say($"A({Lambda} nm) = {Fmt(LastA, "0.000")} · T = {Fmt(Math.Pow(10, -LastA) * 100, "0.0")}%", 1);
            WarnHigh(LastA);
        }

        void WarnHigh(double a)
        {
            if (a > 2) S.Say($"A = {Fmt(a, "0.00")} > 2: ánh sáng qua mẫu quá yếu, số đo mất tuyến tính. Pha loãng mẫu rồi đo lại.", 2);
        }

        public void StartScan()
        {
            if (!Ready) return;
            if (Source == Src.Sample && !S.Require("do_mau")) return;
            S.Done("chon_che_do");
            Spectrum.Clear();
            Unmixed = null;
            scanFrom = Cuvette == "thach_anh" ? 200 : 300;
            scanTo = 800;
            scanT = 0;
            plotCalibration = false;
            S.Say($"Quét phổ {scanFrom} – {scanTo} nm…", 0);
        }

        public void RecordStandard()
        {
            if (!Ready || Source != Src.Std) return;
            if (!S.Require("day_chuan")) return;
            double a = Read(Lambda);
            double c = StdConc[StdIndex];
            CalPoints.RemoveAll(p => Math.Abs(p.c - c) < 1e-12);
            CalPoints.Add((c, a));
            CalPoints.Sort((x, y) => x.c.CompareTo(y.c));
            var pts = new List<(double x, double y)>(CalPoints.Select(p => (p.c, p.a)));
            CalFit = UvVisLibrary.Linear(pts);
            plotCalibration = true;
            if (CalPoints.Count >= 3) S.Done("day_chuan");
            S.Say($"Ghi chuẩn {StdIndex + 1}: c = {Fmt(c * 1000, "0.###")} mM, A = {Fmt(a, "0.000")}" + (CalPoints.Count >= 2 ? $" · R² = {Fmt(CalFit.R2, "0.0000")}" : ""), 1);
            if (StdIndex < 4) StdIndex++;
        }

        public void QuantifySample()
        {
            if (!Ready || Sample == null) return;
            if (Source != Src.Sample) SetSource(Src.Sample);
            if (!S.Require("do_mau")) return;
            if (CalPoints.Count < 2) { S.Say("Chưa có đường chuẩn: đo ít nhất 3 chuẩn (Ghi điểm chuẩn) trước", 2); return; }
            if (S.Guided && CalPoints.Count < 3) { S.Say("Cần ít nhất 3 điểm chuẩn để dựng đường chuẩn tin cậy", 2); return; }
            double a = Read(Lambda);
            sampleA = a;
            LastA = a;
            SampleConc = CalFit.X(a) * Dilution;
            plotCalibration = true;
            S.Done("do_mau");
            double topA = CalPoints.Max(p => p.a);
            if (a > 2) WarnHigh(a);
            else if (a > topA * 1.05) S.Say($"A mẫu ({Fmt(a, "0.000")}) cao hơn chuẩn cao nhất ({Fmt(topA, "0.000")}): ngoại suy, kết quả không tin cậy. Pha loãng mẫu.", 2);
            else if (CalFit.R2 < 0.995) S.Say($"Đường chuẩn R² = {Fmt(CalFit.R2, "0.0000")} < 0,995: kiểm tra lại dãy chuẩn, mẫu trắng", 2);
            else S.Say($"c({Analyte?.Name}) trong mẫu gốc = {Sci(SampleConc)} M (đo được {Sci(SampleConc / Dilution)} M, ×{Dilution})", 1);
        }

        public void UnmixNow()
        {
            if (Spectrum.Count < 10) { S.Say("Quét phổ trước", 2); return; }
            var pts = Spectrum.Where(p => p.l >= 340 && p.l <= 790 && Lib.CuvetteAbsorbance(Cuvette, p.l) < 0.5 && p.a < 2.5).ToList();
            if (Cuvette == "thach_anh") pts = Spectrum.Where(p => p.l >= 220 && p.l <= 790 && p.a < 2.5).ToList();
            var comps = Lib.Entries.ToList();
            var c = UvVisLibrary.Unmix(comps, pts, 1, out double rms);
            Unmixed = new List<(UvVisLibrary.Entry, double)>();
            for (int i = 0; i < comps.Count; i++) if (c[i] * comps[i].Eps(comps[i].LambdaMax(200, 900)) > 0.003) Unmixed.Add((comps[i], c[i]));  // bỏ thành phần chỉ góp dưới 0,003 A (nhiễu)
            UnmixRms = rms;
            S.Say(Unmixed.Count == 0 ? "Không tách được chất nào trong thư viện" : $"Tách được {Unmixed.Count} chất theo thư viện phổ chuẩn (sai lệch còn lại {Fmt(rms, "0.000")})", Unmixed.Count > 0 ? 1 : 2);
            if (!Baseline) S.Say("Chưa đo nền: phổ còn lẫn hấp thụ của dung môi, cuvet nên kết quả tách bị lệch", 2);
        }

        // =====================================================================
        // Mô phỏng phép đo
        // =====================================================================

        public override void Tick(float dt)
        {
            if (Power)
            {
                if (SelfTest < SelfTestS)
                {
                    SelfTest += dt;
                    if (SelfTest >= SelfTestS) { S.Done("bat_may"); S.Say("Tự kiểm tra xong. Đèn đang làm nóng (máy thật 15 – 30 phút; ở đây rút ngắn).", 0); }
                }
                else if (Warm < WarmNeedS)
                {
                    Warm += dt;
                    if (Warm >= WarmNeedS) { S.Done("lam_nong"); S.Say("Đèn đã ổn định", 1); }
                }
            }
            if (scanT >= 0)
            {
                scanT += dt;
                float dur = 2.5f;
                int n = (int)((scanTo - scanFrom) / 2);
                int want = Mathf.Min(n + 1, (int)(scanT / dur * n));
                while (Spectrum.Count < want)
                {
                    int l = scanFrom + Spectrum.Count * 2;
                    Lambda = l;
                    Spectrum.Add((l, Read(l)));
                }
                if (scanT >= dur)
                {
                    scanT = -1;
                    var peaks = Peaks();
                    if (peaks.Count > 0) Lambda = (int)peaks[0].l;
                    if (Source == Src.Sample) S.Done("do_mau");
                    if (Spectrum.Any(q => q.a > 2 && Lib.CuvetteAbsorbance(Cuvette, q.l) < 0.5)) S.Say($"Quét xong: A vượt 2 (lớn nhất {Fmt(Spectrum.Max(q => q.a), "0.00")}), phổ bị bão hòa, đỉnh không xác định được. Pha loãng mẫu rồi quét lại.", 2); else S.Say("Quét xong" + (peaks.Count > 0 ? ": đỉnh " + string.Join(", ", peaks.Select(p => $"{p.l:0} nm (A {Fmt(p.a, "0.00")})")) : ": không có đỉnh rõ"), 1);
                    foreach (var p in Spectrum) if (Lib.CuvetteAbsorbance(Cuvette, p.l) > 2 && p.l > scanFrom) { S.Say($"Dưới khoảng 340 nm cuvet {CuvetteName(Cuvette).ToLowerInvariant()} chặn tia tử ngoại: phổ bị cắt, nhiễu. Dùng cuvet thạch anh.", 2); break; }
                }
            }
        }

        /// <summary>Độ hấp thụ thật của chất đang đặt trong chùm mẫu (đã chia pha loãng).</summary>
        double TrueA(double l)
        {
            if (Source == Src.Blank) return 0;
            if (Source == Src.Std)
            {
                var a = Analyte;
                return a == null ? 0 : a.Eps(l) * StdConc[StdIndex];
            }
            var abs = SampleAbsorbers();
            return UvVisLibrary.Absorbance(abs, l) / Dilution;
        }

        List<(UvVisLibrary.Entry e, double c)> SampleAbsorbers()
        {
            outside.Clear();
            if (Sample == null) return new List<(UvVisLibrary.Entry, double)>();
            return Lib.Absorbers(Sample.Total.Mix, Sample.VolumeMl, Sample.BulkPh, Sample.Total.indicatorDrops, outside);
        }

        /// <summary>Số máy hiển thị ở bước sóng l: hai chùm tia, ánh sáng lạc, cuvet, nền, đèn chưa ổn định, nhiễu.</summary>
        public double Read(double l)
        {
            if (!Ready) return double.NaN;
            double cuv = Lib.CuvetteAbsorbance(Cuvette, l);
            double a = TrueA(l);
            // chùm mẫu và chùm so sánh cùng đi qua cuvet cùng loại: tỉ số khử được cuvet, trừ khi ánh sáng gần như bị chặn hết
            double iS = Math.Pow(10, -(cuv + a)) + StrayLight, iR = Math.Pow(10, -cuv) + StrayLight;
            double read = -Math.Log10(iS / iR);
            if (!Baseline) read += 0.045 + 0.02 * Math.Sin(l / 37.0) + 0.25 * Math.Min(1, cuv);  // dung môi, cuvet chưa được trừ (mô phỏng)
            else read -= baselineError;
            if (Warm < WarmNeedS) read += 0.03 * (1 - Warm / WarmNeedS) * Math.Sin(Time.time * 0.9 + l * 0.01);
            if (Baseline && baselineWarm < WarmNeedS) read += Math.Min(0.05, 0.02 * (1 - baselineWarm / WarmNeedS) * (Time.time - baselineTime) / 5.0);
            double noise = 0.0015 / Math.Sqrt(Math.Max(1e-6, iS * 10));
            read += Gauss() * Math.Min(0.6, noise);
            return read;
        }

        double Gauss()
        {
            double u1 = 1 - rng.NextDouble(), u2 = rng.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }

        List<(double l, double a)> Peaks()
        {
            var res = new List<(double, double)>();
            for (int i = 3; i < Spectrum.Count - 3; i++)
            {
                var p = Spectrum[i];
                if (p.a < 0.05 || p.a > 2 || Lib.CuvetteAbsorbance(Cuvette, p.l) > 0.5) continue;  // vùng bão hòa (A > 2) không tính là đỉnh
                bool max = true;
                for (int k = -3; k <= 3 && max; k++) if (k != 0 && Spectrum[i + k].a > p.a) max = false;
                if (max && (res.Count == 0 || Math.Abs(res[res.Count - 1].Item1 - p.l) > 20)) res.Add(p);
            }
            return res.OrderByDescending(p => p.Item2).Take(3).ToList();
        }

        // =====================================================================
        // Kết quả, phân tích chuyên sâu
        // =====================================================================

        string BuildResults()
        {
            var sb = new StringBuilder();
            string src = Source == Src.Blank ? "mẫu trắng (nước cất)" : Source == Src.Std ? $"chuẩn {StdIndex + 1} ({Analyte?.Name})" : $"{SampleName} (pha loãng ×{Dilution})";
            sb.Append("<color=#9FB3C8>Đang đặt:</color> ").Append(src).Append('\n');
            if (!double.IsNaN(LastA)) sb.Append($"<color=#9FB3C8>Số đo gần nhất:</color> A({Lambda} nm) = <b>{Fmt(LastA, "0.000")}</b>\n");
            if (CalPoints.Count > 0)
            {
                sb.Append($"\n<b>Đường chuẩn {Analyte?.Name} tại {Lambda} nm</b> ({CalPoints.Count} điểm)\n");
                if (CalPoints.Count >= 2)
                    sb.Append($"A = {Fmt(CalFit.Slope, "0")}·c {(CalFit.Intercept >= 0 ? "+" : "−")} {Fmt(Math.Abs(CalFit.Intercept), "0.0000")} · R² = {Fmt(CalFit.R2, "0.0000")}\n")
                      .Append($"<color=#9FB3C8>Độ dốc = ε·b → ε ≈ {Fmt(CalFit.Slope, "0")} L·mol⁻¹·cm⁻¹</color>\n");
                if (!double.IsNaN(SampleConc))
                    sb.Append($"Mẫu: A = {Fmt(sampleA, "0.000")} → c = {Sci(SampleConc / Dilution)} M × {Dilution} = <b>{Sci(SampleConc)} M</b>\n");
            }
            if (Spectrum.Count > 10 && scanT < 0)
            {
                var pk = Peaks();
                sb.Append("\n<b>Phổ</b>: ").Append(pk.Count > 0 ? string.Join(" · ", pk.Select(p => $"đỉnh {p.l:0} nm (A {Fmt(p.a, "0.00")})")) : "không có đỉnh rõ").Append('\n');
            }
            if (Unmixed != null)
            {
                sb.Append("\n<b>Phân tích chuyên sâu: tách hỗn hợp</b>\n<color=#9FB3C8>Phổ đo = Σ εᵢ(λ)·b·cᵢ theo thư viện phổ chuẩn, giải bình phương tối thiểu (cᵢ ≥ 0)</color>\n");
                double main = Unmixed.Count > 0 ? Unmixed.Max(u => u.e.Eps(Lambda) * u.c) : 0;
                double sum = Unmixed.Sum(u => u.e.Eps(Lambda) * u.c);
                foreach (var (e, c) in Unmixed.OrderByDescending(u => u.c * u.e.Eps(u.e.LambdaMax())))
                {
                    double share = sum > 0 ? e.Eps(Lambda) * c / sum : 0;
                    sb.Append($"• <color=#61DBFF>{e.Name}</color>: {Sci(c)} M");
                    if (Source == Src.Sample && Dilution > 1) sb.Append($" → mẫu gốc {Sci(c * Dilution)} M");
                    sb.Append($" · góp {share:P0} A ở {Lambda} nm\n");
                }
                sb.Append($"Sai lệch còn lại (rms) = {Fmt(UnmixRms, "0.0000")}: ").Append(UnmixRms < 0.01 ? "phổ khớp tốt với thư viện" : "<color=#FFC966>còn phần hấp thụ chưa giải thích (chất ngoài thư viện, chưa đo nền, đục)</color>").Append('\n');
            }
            if (Source == Src.Sample)
            {
                SampleAbsorbers();
                if (outside.Count > 0) sb.Append("\n<color=#FFC966>Ngoài thư viện phổ (không tính vào phổ, không bịa số):</color> ").Append(string.Join("; ", outside)).Append('\n');
                if (Sample != null && Sample.Total.Mix != null && HasSolid(Sample.Total.Mix)) sb.Append("<color=#FFC966>Mẫu có kết tủa / cặn: lọc hoặc ly tâm trước khi đo (hạt rắn tán xạ ánh sáng)</color>\n");
            }
            return sb.ToString();
        }

        static bool HasSolid(Mix m)
        {
            for (int i = 0; i < m.Count; i++) if (m.SpeciesAt(i).IsSolidPhase && m.MolAt(i) > 1e-7) return true;
            return false;
        }

        // =====================================================================
        // Sơ đồ nguyên lý
        // =====================================================================

        static readonly Color Body = new Color(0.12f, 0.17f, 0.25f), Edge = new Color(0.45f, 0.6f, 0.75f), Off = new Color(0.3f, 0.35f, 0.4f);

        public override void DrawSchematic(Painter p, float time)
        {
            p.Clear(new Color(0.05f, 0.08f, 0.13f));
            p.Frame(4, 4, p.W - 8, p.H - 8, new Color(1, 1, 1, 0.08f));
            bool lit = Ready || (Power && Mathf.Repeat(time * 3, 1) < 0.5f);
            float warmK = !Power ? 0 : SelfTest < SelfTestS ? 0.4f : Mathf.Clamp01(0.4f + 0.6f * Warm / WarmNeedS);
            bool d2 = Lambda < 340;
            var lampD2 = new Vector2(70, 270);
            var lampW = new Vector2(70, 90);
            var mirror = new Vector2(180, 180);
            var slitIn = new Vector2(270, 180);
            var grating = new Vector2(380, 180);
            var slitOut = new Vector2(490, 180);
            var split = new Vector2(590, 180);
            float refY = 285, smpY = 75;
            var cuvRef = new Vector2(770, refY);
            var cuvSmp = new Vector2(770, smpY);
            var detRef = new Vector2(905, refY);
            var detSmp = new Vector2(905, smpY);
            Color mono = Painter.Wavelength(Lambda);

            // đèn
            Lamp(p, lampD2, lit && d2 ? new Color(0.75f, 0.6f, 1f, warmK) : Off);
            Lamp(p, lampW, lit && !d2 ? new Color(1f, 0.92f, 0.7f, warmK) : Off);
            // gương chuyển đèn
            float ang = d2 ? 45 : -45;
            Seg(p, mirror, ang, 46, Edge, 4);
            // khe vào / ra
            Slit(p, slitIn);
            Slit(p, slitOut);
            // cách tử xoay theo λ
            float gAng = 70 + (Lambda - 190) / 910f * 40;
            Seg(p, grating, gAng, 80, new Color(0.6f, 0.75f, 0.9f), 6);
            for (int i = -3; i <= 3; i++) Seg(p, grating + Rot(gAng, i * 10), gAng + 90, 8, new Color(0.9f, 0.95f, 1f, 0.6f), 1);
            // bộ chia chùm
            p.Disc(split.x, split.y, 16, Body);
            p.Ring(split.x, split.y, 16, Edge);
            Seg(p, split, time * 360 % 360, 30, new Color(0.8f, 0.9f, 1f, 0.8f), 3);
            // cuvet, detector
            Color sampleCol = SourceColor();
            Cuv(p, cuvRef, new Color(0.7f, 0.85f, 1f, 0.25f));
            Cuv(p, cuvSmp, sampleCol);
            double a = Ready ? Math.Max(0, Math.Min(4, TrueA(Lambda))) : 0;
            float tr = (float)Math.Pow(10, -a);
            Det(p, detRef, Ready ? 1 : 0);
            Det(p, detSmp, Ready ? tr : 0);

            if (Ready)
            {
                Color white = d2 ? new Color(0.75f, 0.6f, 1f, 0.9f) : new Color(1f, 0.95f, 0.8f, 0.9f);
                float ph = time * 90;
                Beam(p, d2 ? lampD2 + new Vector2(30, 0) : lampW + new Vector2(30, 0), mirror, white, ph, 1);
                Beam(p, mirror, slitIn, white, ph, 1);
                Beam(p, slitIn, grating, white, ph, 1);
                // sau cách tử: cầu vồng tỏa ra, chỉ một màu qua khe ra
                for (int k = -2; k <= 2; k++)
                    if (k != 0) p.Line(grating.x, grating.y, slitOut.x - 10, slitOut.y + k * 22, Painter.Wavelength(Lambda + k * 60) * new Color(1, 1, 1, 0.35f), 2);
                Beam(p, grating, slitOut, mono, ph, 1);
                Beam(p, slitOut, split, mono, ph, 1);
                Beam(p, split, new Vector2(650, refY), mono, ph, 1);
                Beam(p, new Vector2(650, refY), cuvRef, mono, ph, 1);
                Beam(p, cuvRef, detRef, mono, ph, 1);
                Beam(p, split, new Vector2(650, smpY), mono, ph, 1);
                Beam(p, new Vector2(650, smpY), cuvSmp, mono, ph, 1);
                Beam(p, cuvSmp, detSmp, mono, ph, Mathf.Max(0.08f, tr));
            }

            // nhãn
            var d = Doc;
            S.SchemLabel("den_d2", d.PartName("den_d2"), lampD2.x, lampD2.y + 38, 12);
            S.SchemLabel("den_w", d.PartName("den_w"), lampW.x, lampW.y - 38, 12);
            S.SchemLabel("guong", d.PartName("guong_doi_den"), mirror.x + 20, mirror.y - 62, 12);
            S.SchemLabel("khe_vao", d.PartName("khe_vao"), slitIn.x, slitIn.y + 46, 12);
            S.SchemLabel("cach_tu", d.PartName("cach_tu") + $"\n<color=#61DBFF>λ = {Lambda} nm</color>", grating.x, grating.y - 66, 12);
            S.SchemLabel("khe_ra", d.PartName("khe_ra"), slitOut.x + 10, slitOut.y + 72, 12);
            S.SchemLabel("chia", d.PartName("chia_chum"), split.x, split.y - 34, 12);
            S.SchemLabel("cuv_ref", d.PartName("cuvet_so_sanh") + "\n(mẫu trắng)", cuvRef.x, cuvRef.y + 50, 12);
            S.SchemLabel("cuv_smp", d.PartName("cuvet_mau"), cuvSmp.x, cuvSmp.y + 52, 12);
            S.SchemLabel("det_ref", "Detector (I₀)", detRef.x, detRef.y + 40, 12);
            S.SchemLabel("det_smp", Ready ? $"Detector (I)\n<color=#73F29E>T = {tr * 100:0.#}%</color>" : "Detector (I)", detSmp.x, detSmp.y + 44, 12);
            S.SchemLabel("trang_thai", !Power ? "<color=#8899AA>MÁY TẮT</color>" : SelfTest < SelfTestS ? "<color=#FFC966>ĐANG TỰ KIỂM TRA…</color>" : Warm < WarmNeedS ? $"<color=#FFC966>ĐÈN ĐANG LÀM NÓNG {Warm / WarmNeedS:P0}</color>" : scanT >= 0 ? "<color=#61DBFF>ĐANG QUÉT PHỔ</color>" : "<color=#73F29E>SẴN SÀNG</color>", 490, 330, 14);
        }

        Color SourceColor()
        {
            if (Source == Src.Blank) return new Color(0.7f, 0.85f, 1f, 0.25f);
            if (Source == Src.Std)
            {
                var sp = Analyte != null ? SpeciesRegistry.Get(Analyte.Id) : null;
                var c = sp != null ? new Color(sp.Color.r, sp.Color.g, sp.Color.b, 0.75f) : new Color(1, 0.4f, 1, 0.7f);
                return c;
            }
            if (Sample == null) return new Color(0.7f, 0.85f, 1f, 0.25f);
            var l = Sample.Look.Liquid;
            return new Color(l.r, l.g, l.b, Mathf.Lerp(0.85f, 0.4f, Mathf.Log10(Dilution) / 2f));
        }

        static Vector2 Rot(float deg, float len) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad)) * len;

        static void Seg(Painter p, Vector2 c, float deg, float len, Color col, float t)
        {
            var d = Rot(deg, len / 2);
            p.Line(c.x - d.x, c.y - d.y, c.x + d.x, c.y + d.y, col, t);
        }

        static void Lamp(Painter p, Vector2 c, Color glow)
        {
            if (glow.a > 0.05f && glow != Off) p.Disc(c.x, c.y, 26, new Color(glow.r, glow.g, glow.b, glow.a * 0.35f));
            p.FillRect(c.x - 30, c.y - 18, 60, 36, Body);
            p.Frame(c.x - 30, c.y - 18, 60, 36, Edge);
            p.Disc(c.x, c.y, 9, glow == Off ? Off : new Color(glow.r, glow.g, glow.b, Mathf.Max(0.3f, glow.a)));
        }

        static void Slit(Painter p, Vector2 c)
        {
            p.FillRect(c.x - 3, c.y + 6, 6, 34, Edge);
            p.FillRect(c.x - 3, c.y - 40, 6, 34, Edge);
        }

        static void Cuv(Painter p, Vector2 c, Color liquid)
        {
            p.FillRect(c.x - 20, c.y - 32, 40, 52, liquid);
            p.Frame(c.x - 22, c.y - 34, 44, 66, new Color(0.85f, 0.92f, 1f, 0.7f), 2);
        }

        static void Det(Painter p, Vector2 c, float level)
        {
            p.FillRect(c.x - 18, c.y - 24, 36, 48, Body);
            p.Frame(c.x - 18, c.y - 24, 36, 48, Edge);
            p.FillRect(c.x + 22, c.y - 24, 10, 48, new Color(1, 1, 1, 0.08f));
            p.FillRect(c.x + 22, c.y - 24, 10, 48 * Mathf.Clamp01(level), new Color(0.45f, 0.95f, 0.62f, 0.9f));
        }

        static void Beam(Painter p, Vector2 a, Vector2 b, Color c, float phase, float intensity)
        {
            var k = c;
            k.a = 0.25f * intensity + 0.1f;
            p.Line(a.x, a.y, b.x, b.y, k, 5);
            k.a = Mathf.Clamp01(intensity);
            p.Dashed(a.x, a.y, b.x, b.y, k, 3, 10, phase);
        }

        // =====================================================================
        // Đồ thị
        // =====================================================================

        public override void DrawPlot(Painter p)
        {
            InstrumentScreen.PlotFrame(p);
            if (plotCalibration) DrawCalibration(p);
            else DrawSpectrum(p);
        }

        void DrawSpectrum(Painter p)
        {
            double x0 = Cuvette == "thach_anh" ? 200 : 300, x1 = 800;
            double yMax = 0.5;
            foreach (var s in Spectrum) if (!double.IsNaN(s.a)) yMax = Math.Max(yMax, Math.Min(4, s.a) * 1.15);
            if (!double.IsNaN(LastA)) yMax = Math.Max(yMax, Math.Min(4, LastA) * 1.15);
            S.SetPlotAxes("Phổ hấp thụ", "Bước sóng λ (nm)", "Độ hấp thụ A", $"{x0:0}", $"{x1:0}", "0", Fmt(yMax, "0.00"));
            // phổ từng chất sau khi tách hỗn hợp
            if (Unmixed != null)
            {
                Color[] cols = { new Color(1f, 0.6f, 0.9f, 0.8f), new Color(1f, 0.75f, 0.3f, 0.8f), new Color(0.5f, 1f, 0.6f, 0.8f), new Color(0.6f, 0.7f, 1f, 0.8f), new Color(1f, 1f, 0.5f, 0.8f) };
                int k = 0;
                foreach (var (e, c) in Unmixed)
                {
                    var pts = new List<Vector2>();
                    for (double l = x0; l <= x1; l += 4) pts.Add(InstrumentScreen.Map(p, l, Math.Min(yMax, e.Eps(l) * c), x0, x1, 0, yMax));
                    var col = cols[k++ % cols.Length];
                    for (int i = 1; i < pts.Count; i += 2) p.Line(pts[i - 1].x, pts[i - 1].y, pts[i].x, pts[i].y, col, 2);
                }
            }
            if (Spectrum.Count > 1)
            {
                var pts = Spectrum.Select(s => InstrumentScreen.Map(p, s.l, Math.Max(0, Math.Min(yMax, s.a)), x0, x1, 0, yMax)).ToList();
                p.Poly(pts, new Color(0.43f, 0.86f, 1f), 3);
            }
            // vạch bước sóng đang đặt
            var m = InstrumentScreen.Map(p, Lambda, 0, x0, x1, 0, yMax);
            p.FillRect(m.x - 1, 0, 2, p.H, Painter.Wavelength(Lambda) * new Color(1, 1, 1, 0.6f));
            if (!double.IsNaN(LastA))
            {
                var q = InstrumentScreen.Map(p, Lambda, Math.Max(0, Math.Min(yMax, LastA)), x0, x1, 0, yMax);
                p.Disc(q.x, q.y, 6, new Color(1f, 0.45f, 0.45f));
            }
        }

        void DrawCalibration(Painter p)
        {
            double cMax = StdConc.Length > 0 ? StdConc[StdConc.Length - 1] * 1.25 : 1e-3;
            if (!double.IsNaN(SampleConc)) cMax = Math.Max(cMax, SampleConc / Dilution * 1.1);
            double aMax = 0.5;
            foreach (var c in CalPoints) aMax = Math.Max(aMax, c.a * 1.2);
            if (!double.IsNaN(sampleA)) aMax = Math.Max(aMax, Math.Min(4, sampleA) * 1.15);
            S.SetPlotAxes($"Đường chuẩn {Analyte?.Name} tại {Lambda} nm", "Nồng độ c (mM)", "Độ hấp thụ A", "0", Fmt(cMax * 1000, "0.###"), "0", Fmt(aMax, "0.00"));
            if (CalPoints.Count >= 2)
            {
                var a0 = InstrumentScreen.Map(p, 0, CalFit.Intercept, 0, cMax, 0, aMax);
                var a1 = InstrumentScreen.Map(p, cMax, CalFit.Intercept + CalFit.Slope * cMax, 0, cMax, 0, aMax);
                p.Line(a0.x, a0.y, a1.x, a1.y, new Color(0.43f, 0.86f, 1f, 0.8f), 2);
            }
            foreach (var c in CalPoints)
            {
                var q = InstrumentScreen.Map(p, c.c, c.a, 0, cMax, 0, aMax);
                p.Disc(q.x, q.y, 6, new Color(0.45f, 0.95f, 0.62f));
            }
            if (!double.IsNaN(SampleConc) && !double.IsNaN(sampleA))
            {
                var q = InstrumentScreen.Map(p, SampleConc / Dilution, Math.Min(aMax, sampleA), 0, cMax, 0, aMax);
                p.Line(q.x - 8, q.y - 8, q.x + 8, q.y + 8, new Color(1f, 0.45f, 0.45f), 3);
                p.Line(q.x - 8, q.y + 8, q.x + 8, q.y - 8, new Color(1f, 0.45f, 0.45f), 3);
            }
        }

        // =====================================================================

        string CuvetteName(string code)
        {
            foreach (var c in Lib.Cuvettes) if (c.code == code) return c.name.Replace("Cuvet ", "");
            return code;
        }

        static string Short(string s) => s.Length > 14 ? s.Substring(0, 13) + "…" : s;
        public static string Fmt(double x, string f) => double.IsNaN(x) ? "—" : x.ToString(f, Vi);

        /// <summary>Số khoa học kiểu 2,1×10⁻⁴.</summary>
        public static string Sci(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return "—";
            if (x == 0) return "0";
            int e = (int)Math.Floor(Math.Log10(Math.Abs(x)));
            double m = x / Math.Pow(10, e);
            if (e >= -2 && e <= 2) return x.ToString(e < 0 ? "0.####" : "0.##", Vi);
            const string sup = "⁰¹²³⁴⁵⁶⁷⁸⁹";
            string es = (e < 0 ? "⁻" : "") + string.Concat(Math.Abs(e).ToString().Select(ch => sup[ch - '0']));
            return m.ToString("0.00", Vi) + "×10" + es;
        }
    }
}
