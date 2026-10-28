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
    /// Máy AAS ngọn lửa không khí – axetilen. Điều kiện đo từng nguyên tố theo sổ tay phương pháp của Agilent; thứ tự mở / đóng khí
    /// theo SOP của các trường (Fresno State, RIT). Mô phỏng: tín hiệu dâng dần khi hút mẫu, nền ngọn lửa, về 0 bằng nước cất,
    /// đường chuẩn cong ở nồng độ cao, nhiễu ion hóa / hóa học, nguyên tố cần ngọn lửa N₂O (Al) không có tín hiệu.
    /// Phân tích chuyên sâu: bảng kết quả mọi kim loại đã đo trong mẫu, có hệ số pha loãng, khoảng làm việc, ghi chú nhiễu.
    /// </summary>
    public class AasProgram : InstrumentProgram
    {
        public override string Code => "aas";
        static readonly string[] optional = { "tat_lua" };
        public override ICollection<string> Optional => optional;

        public const float SelfTestS = 2f, LampWarmS = 4f;
        public enum Src { None, Blank, Std, Sample }

        public bool Power { get; private set; }
        float selfTest;
        public bool Fan { get; private set; }
        public string Lamp { get; private set; }
        float lampWarm, turretAngle, turretTarget;
        public bool Air { get; private set; }
        public bool Acetylene { get; private set; }
        public bool Flame { get; private set; }
        public Src Source { get; private set; } = Src.None;
        public int StdIndex { get; private set; }
        public int Dilution { get; private set; } = 1;
        public bool Additive { get; private set; }  // chất giải phóng (La/Sr) hoặc chống ion hóa (K/Cs) cho mọi dung dịch
        public double Signal { get; private set; }
        double zeroOffset;
        public bool Zeroed { get; private set; }
        float aspirateT;
        bool plotCal;
        readonly List<(float t, float a)> trace = new List<(float, float)>();
        public readonly List<(double c, double a)> CalPoints = new List<(double, double)>();
        public UvVisLibrary.Fit CalFit { get; private set; }
        public readonly Dictionary<string, (double meas, int dil, double orig, double a, string note)> Results = new Dictionary<string, (double, int, double, double, string)>();
        readonly System.Random rng = new System.Random(11);

        Button bPower, bFan, bLamp, bAir, bAcet, bIgnite, bAcetOff, bAirOff, bAdd, bBlank, bStd, bSample, bDil, bAsp, bZero, bRec, bMeas, bPlot;
        Text status;

        AasLibrary Lib => InstrumentData.Instance != null ? InstrumentData.Instance.Aas : new AasLibrary();
        AasLibrary.Element El => Lamp != null ? Lib.Get(Lamp) : null;
        public bool Ready => Power && selfTest >= SelfTestS;
        bool LampReady => El != null && lampWarm >= LampWarmS;

        public override void Build()
        {
            bPower = S.AddButton("Bật máy", 0, 0, TogglePower, true);
            bFan = S.AddButton("", 1, 0, () => { Fan = !Fan; S.Say(Fan ? "Đã bật quạt hút khí thải" : "Đã tắt quạt hút", Fan ? 1 : 2); CheckPowerStep(); });
            bLamp = S.AddButton("", 2, 0, NextLamp);
            bAir = S.AddButton("Mở không khí", 0, 1, OpenAir);
            bAcet = S.AddButton("Mở axetilen", 1, 1, OpenAcetylene);
            bIgnite = S.AddButton("Châm lửa", 2, 1, Ignite, true);
            bAcetOff = S.AddButton("Đóng axetilen", 0, 2, CloseAcetylene);
            bAirOff = S.AddButton("Đóng không khí", 1, 2, CloseAir);
            bAdd = S.AddButton("", 2, 2, () => { Additive = !Additive; S.Say(Additive ? "Đã thêm chất phụ gia (La / Sr giải phóng; K / Cs chống ion hóa) vào mọi dung dịch, kể cả mẫu trắng và chuẩn" : "Bỏ chất phụ gia", 0); }, false, 1, 13);
            bBlank = S.AddButton("Nước cất", 0, 3, () => SetSource(Src.Blank));
            bStd = S.AddButton("", 1, 3, NextStd);
            bSample = S.AddButton("Mẫu ở ổ máy", 2, 3, () => SetSource(Src.Sample));
            bDil = S.AddButton("", 0, 4, () => { Dilution = Dilution == 1 ? 10 : Dilution == 10 ? 100 : Dilution == 100 ? 1000 : 1; S.Say($"Mẫu pha loãng ×{Dilution} bằng bình định mức", 0); });
            bAsp = S.AddButton("", 1, 4, ToggleAspirate);
            bZero = S.AddButton("Về 0", 2, 4, AutoZero);
            bRec = S.AddButton("Ghi điểm chuẩn", 0, 5, RecordStandard);
            bMeas = S.AddButton("Đo mẫu", 1, 5, MeasureSample, true);
            bPlot = S.AddButton("", 2, 5, () => plotCal = !plotCal);
            S.AddButton("Tắt máy", 0, 6, TurnOff);
            status = S.AddText("", 7, 0, 3, 13);
            S.SetResults("Bật máy và quạt hút, chọn đèn của kim loại cần đo. Đặt ống mẫu vào ổ trên nóc máy ở phòng lab trước khi mở màn hình này.");
        }

        public override void Refresh()
        {
            InstrumentScreen.SetButton(bPower, Power ? "Máy đang bật" : "Bật máy", true, Power);
            InstrumentScreen.SetButton(bFan, Fan ? "Quạt hút: BẬT" : "Quạt hút: tắt", true, Fan);
            InstrumentScreen.SetButton(bLamp, El != null ? $"Đèn: {El.Symbol} ({El.Name}) ▸" : "Chọn đèn ▸", Ready, El != null);
            InstrumentScreen.SetButton(bAir, null, Ready && !Air, Air);
            InstrumentScreen.SetButton(bAcet, null, Ready && !Acetylene, Acetylene);
            InstrumentScreen.SetButton(bIgnite, Flame ? "Lửa đang cháy" : "Châm lửa", Ready && !Flame, Flame);
            InstrumentScreen.SetButton(bAcetOff, null, Acetylene);
            InstrumentScreen.SetButton(bAirOff, null, Air);
            InstrumentScreen.SetButton(bAdd, Additive ? "Phụ gia: CÓ" : "Phụ gia: không", Ready, Additive);
            InstrumentScreen.SetButton(bBlank, null, Ready, Source == Src.Blank);
            var std = El != null ? El.Standards() : new double[0];
            InstrumentScreen.SetButton(bStd, std.Length > 0 ? $"Chuẩn {StdIndex + 1}/{std.Length}: {Fmt(std[Math.Min(StdIndex, std.Length - 1)], "0.###")} mg/L" : "Chuẩn ▸", Ready && El != null, Source == Src.Std);
            InstrumentScreen.SetButton(bSample, Sample != null ? "Mẫu: " + (SampleName.Length > 14 ? SampleName.Substring(0, 13) + "…" : SampleName) : "Ổ mẫu trống", Ready && Sample != null, Source == Src.Sample);
            InstrumentScreen.SetButton(bDil, $"Pha loãng mẫu ×{Dilution}", Ready, Dilution > 1);
            InstrumentScreen.SetButton(bAsp, Aspirating ? "Nhấc ống hút" : "Nhúng ống hút", Ready && Source != Src.None, Aspirating);
            InstrumentScreen.SetButton(bZero, null, Ready && Flame);
            InstrumentScreen.SetButton(bRec, null, Ready && Source == Src.Std && Aspirating);
            InstrumentScreen.SetButton(bMeas, null, Ready && Source == Src.Sample && Aspirating);
            InstrumentScreen.SetButton(bPlot, plotCal ? "Đồ thị: đường chuẩn" : "Đồ thị: tín hiệu", true);
            string lampS = El == null ? "chưa chọn" : lampWarm < LampWarmS ? $"{El.Symbol} đang làm nóng {lampWarm / LampWarmS:P0}" : $"{El.Symbol} {Fmt(El.Line, "0.0")} nm, khe {Fmt(El.Slit, "0.0")} nm, {El.LampMa:0} mA";
            status.text = $"Đèn: {lampS} · lửa: {(Flame ? "cháy" : "tắt")} · về 0: {(Zeroed ? "rồi" : "chưa")}";
            S.SetResults(BuildResults());
        }

        bool Aspirating => aspirateT >= 0 && Source != Src.None && aspiring;
        bool aspiring;


        // ---- chọn trực tiếp (bài kiểm thử) ----
        public void SetFan(bool on) { Fan = on; CheckPowerStep(); }
        public void SelectLamp(string sym)
        {
            var els = Lib.Elements;
            int target = els.FindIndex(e => e.Symbol == sym);
            if (target < 0) return;
            Lamp = target == 0 ? els[els.Count - 1].Symbol : els[target - 1].Symbol;  // NextLamp chọn đèn kế tiếp
            NextLamp();
        }
        public void SelectStd(int i) { if (El == null) return; StdIndex = Mathf.Clamp(i, 0, El.Standards().Length - 1); if (aspiring) aspirateT = 0; Source = Src.Std; }
        public void SetDilution(int d) { Dilution = Math.Max(1, d); if (aspiring) aspirateT = 0; }
        public void SetAspirate(bool on) { if (aspiring != on) ToggleAspirate(); }
        public void SetAdditive(bool on) => Additive = on;
        public bool LampWarmed => LampReady;
        // =====================================================================
        // Thao tác
        // =====================================================================

        public void TogglePower()
        {
            if (Power) return;
            Power = true;
            selfTest = 0;
            S.Say("Máy tự kiểm tra… Nhớ bật quạt hút khí thải phía trên đầu đốt.", 0);
        }

        void CheckPowerStep()
        {
            if (Ready && Fan) S.Done("bat_may");
        }

        public void TurnOff()
        {
            if (Flame) { S.Say("Đang có lửa: tắt lửa (đóng axetilen rồi không khí) trước khi tắt máy", 2); return; }
            Power = false;
            aspiring = false;
            S.Say("Đã tắt máy", 0);
        }

        public void NextLamp()
        {
            if (!Ready) return;
            if (!S.Require("chon_den")) return;
            var els = Lib.Elements;
            if (els.Count == 0) return;
            int i = Lamp == null ? -1 : els.FindIndex(e => e.Symbol == Lamp);
            var e2 = els[(i + 1) % els.Count];
            Lamp = e2.Symbol;
            lampWarm = 0;
            turretTarget = -((i + 1) % els.Count) * 360f / els.Count;
            CalPoints.Clear();
            CalFit = default;
            Zeroed = false;
            StdIndex = 0;
            S.Done("chon_den");
            string n2o = e2.Flame == "n2o" ? (e2.OnlyN2O ? " · CẦN ngọn lửa N₂O – axetilen (máy này chỉ có không khí – axetilen: không đo được)" : " · hãng khuyên dùng ngọn lửa N₂O – axetilen; ở đây phải thêm chất giải phóng") : "";
            S.Say($"Đèn {e2.Symbol}: {Fmt(e2.Line, "0.0")} nm, khe {Fmt(e2.Slit, "0.0")} nm, dòng đèn {e2.LampMa:0} mA, ngọn lửa {(e2.Flame == "n2o" ? "N₂O" : "không khí")} – axetilen ({e2.Stoichiometry}){n2o}", e2.OnlyN2O ? 2 : 0);
        }

        public void OpenAir()
        {
            if (!Ready) return;
            Air = true;
            S.Say("Đã mở khí nén (không khí)", 0);
        }

        public void OpenAcetylene()
        {
            if (!Ready) return;
            if (!Air)
            {
                if (S.Guided) { S.Say("Phải mở không khí TRƯỚC rồi mới mở axetilen", 2); return; }
                S.Say("NGUY HIỂM: mở axetilen khi chưa có không khí: hỗn hợp giàu nhiên liệu, nguy cơ cháy ngược vào buồng phun!", 3);
            }
            Acetylene = true;
            if (Air) S.Say("Đã mở axetilen", 0);
        }

        public void Ignite()
        {
            if (!Ready) return;
            if (!S.Require("chau_lua")) return;
            if (!Air || !Acetylene) { S.Say("Cần mở cả không khí và axetilen trước khi châm lửa", 2); return; }
            if (!Fan)
            {
                if (S.Guided) { S.Say("Bật quạt hút khí thải trước khi châm lửa", 2); return; }
                S.Say("Cảnh báo: châm lửa khi chưa bật quạt hút: khí thải và hơi kim loại tràn ra phòng", 3);
            }
            Flame = true;
            S.Done("chau_lua");
            S.Say("Đã châm lửa: ngọn lửa không khí – axetilen ổn định (khoảng 2300 °C)", 1);
        }

        public void CloseAcetylene()
        {
            if (!Acetylene) return;
            Acetylene = false;
            if (Flame)
            {
                Flame = false;
                aspiring = false;
                S.Say("Đã đóng axetilen: ngọn lửa tắt êm. Giờ đóng không khí.", 1);
                if (S.DoneSteps.Contains("do_mau")) S.Done("tat_lua");
            }
        }

        public void CloseAir()
        {
            if (!Air) return;
            if (Flame && Acetylene)
            {
                if (S.Guided) { S.Say("Tắt lửa: đóng axetilen TRƯỚC, không khí sau", 2); return; }
                S.Say("NGUY HIỂM: đóng không khí khi axetilen còn chảy: ngọn lửa bùng, nguy cơ cháy ngược!", 3);
                Flame = false;
                aspiring = false;
            }
            Air = false;
        }

        public void SetSource(Src s)
        {
            if (!Ready) return;
            if (s == Src.Sample && Sample == null) { S.Say("Ổ mẫu trống: đặt ống mẫu vào ổ trên nóc máy ở phòng lab", 2); return; }
            if (aspiring && Source != s) aspirateT = 0;
            Source = s;
        }

        public void NextStd()
        {
            if (!Ready || El == null) return;
            var std = El.Standards();
            if (Source == Src.Std) StdIndex = (StdIndex + 1) % std.Length;
            if (aspiring && Source != Src.Std) aspirateT = 0;
            if (aspiring) aspirateT = 0;
            Source = Src.Std;
            S.Say($"Chuẩn {StdIndex + 1}: {El.Symbol} {Fmt(std[StdIndex], "0.###")} mg/L", 0);
        }

        public void ToggleAspirate()
        {
            if (!Ready || Source == Src.None) return;
            aspiring = !aspiring;
            aspirateT = 0;
            if (aspiring && Source == Src.Sample && Sample != null)
            {
                var solids = new Dictionary<string, double>();
                AasLibrary.ElementMgL(Sample.Total.Mix, Sample.VolumeMl, solids);
                if (solids.Values.Any(v => v > 0.5))
                {
                    if (S.Guided) { aspiring = false; S.Say("Mẫu có kết tủa / cặn: lọc hoặc phá mẫu trước (cặn làm tắc ống mao quản, kim loại trong cặn không được đo đủ)", 2); return; }
                    S.Say("Mẫu có cặn: ống mao quản dễ tắc, tín hiệu dao động và thấp hơn thật", 2);
                }
            }
            if (aspiring && !Flame) S.Say("Chưa có lửa: hút mẫu cũng không có tín hiệu nguyên tử", 2);
        }

        public void AutoZero()
        {
            if (!Ready || !Flame) return;
            if (!S.Require("ve_0")) return;
            if (S.Guided && !(Aspirating && Source == Src.Blank)) { S.Say("Về 0 khi đang hút nước cất (mẫu trắng)", 2); return; }
            if (aspirateT < 1.2f && Aspirating && S.Guided) { S.Say("Chờ tín hiệu ổn định (khoảng 2 giây sau khi nhúng ống hút)", 2); return; }
            zeroOffset += Signal;
            Signal = 0;
            Zeroed = true;
            S.Done("ve_0");
            S.Say(Source == Src.Blank ? "Đã về 0 với nước cất" : "Đã về 0 bằng dung dịch không phải mẫu trắng: các kết quả sau sẽ thấp hơn thật!", Source == Src.Blank ? 1 : 3);
        }

        public void RecordStandard()
        {
            if (!Ready || Source != Src.Std || !Aspirating || El == null) return;
            if (!S.Require("day_chuan")) return;
            if (aspirateT < 1.2f) { S.Say("Chờ tín hiệu ổn định rồi mới ghi", 2); return; }
            double c = El.Standards()[StdIndex];
            CalPoints.RemoveAll(p => Math.Abs(p.c - c) < 1e-12);
            CalPoints.Add((c, Signal));
            CalPoints.Sort((x, y) => x.c.CompareTo(y.c));
            var pts = new List<(double x, double y)> { (0, 0) };
            pts.AddRange(CalPoints.Select(p => (p.c, p.a)));
            CalFit = UvVisLibrary.Linear(pts);
            plotCal = true;
            if (CalPoints.Count >= 3) S.Done("day_chuan");
            S.Say($"Ghi chuẩn {StdIndex + 1}: {Fmt(c, "0.###")} mg/L, A = {Fmt(Signal, "0.0000")}" + (CalPoints.Count >= 2 ? $" · R² = {Fmt(CalFit.R2, "0.0000")}" : ""), 1);
            if (StdIndex < El.Standards().Length - 1) { StdIndex++; aspirateT = 0; }
        }

        public void MeasureSample()
        {
            if (!Ready || Source != Src.Sample || !Aspirating || El == null) return;
            if (!S.Require("do_mau")) return;
            if (CalPoints.Count < 2) { S.Say("Chưa có đường chuẩn: ghi ít nhất 3 chuẩn", 2); return; }
            if (aspirateT < 1.2f) { S.Say("Chờ tín hiệu ổn định rồi mới đo", 2); return; }
            double a = Signal;
            double meas = Math.Max(0, CalFit.X(a));
            double topA = CalPoints.Max(p => p.a);
            string note = "";
            if (a > topA * 1.05) note = "VƯỢT chuẩn cao nhất: pha loãng thêm";
            else if (meas < El.Lo) note = meas < El.Lo / 10 ? "không phát hiện" : "dưới khoảng làm việc";
            AasLibrary.Interference(El.Symbol, Sample.Total.Mix, Sample.VolumeMl, Additive, out var interf);
            if (interf != null) note = (note.Length > 0 ? note + "; " : "") + interf;
            if (El.OnlyN2O) note = "cần ngọn lửa N₂O – axetilen";
            Results[El.Symbol] = (meas, Dilution, meas * Dilution, a, note);
            plotCal = true;
            S.Done("do_mau");
            if (a > topA * 1.05) S.Say($"A = {Fmt(a, "0.000")} vượt chuẩn cao nhất (A {Fmt(topA, "0.000")}): đường chuẩn cong, kết quả thấp hơn thật. Pha loãng mẫu (×10, ×100) rồi đo lại.", 2);
            else S.Say($"{El.Symbol}: {Fmt(meas, "0.000")} mg/L × {Dilution} = {Fmt(meas * Dilution, "0.##")} mg/L trong mẫu gốc", 1);
        }

        // =====================================================================
        // Mô phỏng
        // =====================================================================

        public override void Tick(float dt)
        {
            if (Power && selfTest < SelfTestS)
            {
                selfTest += dt;
                if (selfTest >= SelfTestS) { S.Say("Tự kiểm tra xong", 0); CheckPowerStep(); }
            }
            if (El != null && lampWarm < LampWarmS && Power)
            {
                lampWarm += dt;
                if (lampWarm >= LampWarmS) { S.Done("lam_nong_den"); S.Say($"Đèn {El.Symbol} đã ổn định", 1); }
            }
            turretAngle = Mathf.MoveTowardsAngle(turretAngle, turretTarget, 180 * dt);
            if (aspiring) aspirateT += dt;
            double target = TargetReading();
            Signal += (target - Signal) * Math.Min(1, dt / 0.35);
            double noise = (0.0008 + 0.004 * Math.Abs(Signal)) * (LampReady ? 1 : 4) * Gauss();
            if (aspiring && Source == Src.Sample && SampleHasSolids()) noise += 0.02 * Gauss();
            float now = Time.time;
            trace.Add((now, (float)(Signal + noise)));
            while (trace.Count > 0 && trace[0].t < now - 30) trace.RemoveAt(0);
        }

        double Gauss()
        {
            double u1 = 1 - rng.NextDouble(), u2 = rng.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }

        bool SampleHasSolids()
        {
            var solids = new Dictionary<string, double>();
            AasLibrary.ElementMgL(Sample.Total.Mix, Sample.VolumeMl, solids);
            return solids.Values.Any(v => v > 0.5);
        }

        /// <summary>Nồng độ nguyên tố của đèn đang chọn trong dung dịch đang hút (mg/L, đã chia pha loãng).</summary>
        public double SourceConc()
        {
            if (El == null || Source == Src.None) return 0;
            if (Source == Src.Blank) return 0;
            if (Source == Src.Std) return El.Standards()[StdIndex];
            if (Sample == null) return 0;
            var solids = new Dictionary<string, double>();
            var all = AasLibrary.ElementMgL(Sample.Total.Mix, Sample.VolumeMl, solids);
            double c = all.TryGetValue(El.Symbol, out var v) ? v : 0;
            double s = solids.TryGetValue(El.Symbol, out var w) ? w : 0;
            return (c - 0.5 * s) / Dilution;  // phần nằm trong cặn chỉ được hút lên một phần
        }

        double TargetReading()
        {
            if (!Ready || El == null) return 0;
            double bg = Flame ? 0.012 : 0;  // nền ngọn lửa
            double a = 0;
            if (Flame && aspiring && aspirateT > 0.15f)
            {
                double interf = 1;
                if (Source == Src.Sample && Sample != null) interf = AasLibrary.Interference(El.Symbol, Sample.Total.Mix, Sample.VolumeMl, Additive, out _);
                a = El.Absorbance(SourceConc(), true, interf);
                bg += 0.004;  // dung môi
            }
            double drift = LampReady ? 0 : 0.03 * Math.Sin(Time.time * 1.3);
            return a + bg + drift - zeroOffset;
        }

        // =====================================================================
        // Kết quả
        // =====================================================================

        string BuildResults()
        {
            var sb = new StringBuilder();
            if (El != null)
            {
                sb.Append($"<b>{El.Symbol} – {El.Name}</b>: vạch {Fmt(El.Line, "0.0")} nm · khe {Fmt(El.Slit, "0.0")} nm · đèn {El.LampMa:0} mA\n")
                  .Append($"<color=#9FB3C8>Khoảng làm việc {Fmt(El.Lo, "0.###")} – {Fmt(El.Hi, "0.##")} mg/L · nồng độ đặc trưng ≈ {Fmt(El.CharacteristicConc, "0.###")} mg/L (A = 0,0044)</color>\n");
                if (!string.IsNullOrEmpty(El.Note)) sb.Append($"<color=#9FB3C8>{El.Note}</color>\n");
            }
            string src = Source == Src.None ? "—" : Source == Src.Blank ? "nước cất" : Source == Src.Std ? $"chuẩn {StdIndex + 1}" : $"{SampleName} ×{Dilution}";
            sb.Append($"Đang hút: {(aspiring ? src : "(ống hút ngoài không khí)")} · A = <b>{Fmt(Signal, "0.0000")}</b>\n");
            if (CalPoints.Count >= 2)
                sb.Append($"\n<b>Đường chuẩn</b> ({CalPoints.Count} chuẩn + 0): A = {Fmt(CalFit.Slope, "0.0000")}·c {(CalFit.Intercept >= 0 ? "+" : "−")} {Fmt(Math.Abs(CalFit.Intercept), "0.0000")} · R² = {Fmt(CalFit.R2, "0.0000")}\n");
            if (Results.Count > 0)
            {
                sb.Append("\n<b>Phân tích chuyên sâu: kim loại trong mẫu</b>\n<color=#9FB3C8>Kim loại · đo được · ×pha loãng · mẫu gốc</color>\n");
                foreach (var kv in Results)
                {
                    var r = kv.Value;
                    string col = r.note.Contains("VƯỢT") || r.note.Contains("N₂O") ? "#FFC966" : "#73F29E";
                    sb.Append($"• <color=#61DBFF>{kv.Key}</color>: {Fmt(r.meas, "0.000")} mg/L × {r.dil} = <b>{Fmt(r.orig, "0.##")} mg/L</b>");
                    if (r.note.Length > 0) sb.Append($" <color={col}>({r.note})</color>");
                    sb.Append('\n');
                }
            }
            return sb.ToString();
        }

        // =====================================================================
        // Sơ đồ nguyên lý
        // =====================================================================

        static readonly Color Body = new Color(0.12f, 0.17f, 0.25f), Edge = new Color(0.45f, 0.6f, 0.75f), Off = new Color(0.3f, 0.35f, 0.4f);

        public override void DrawSchematic(Painter p, float time)
        {
            p.Clear(new Color(0.05f, 0.08f, 0.13f));
            p.Frame(4, 4, p.W - 8, p.H - 8, new Color(1, 1, 1, 0.08f));
            var els = Lib.Elements;
            var turret = new Vector2(95, 205);
            var burner = new Vector2(420, 165);
            var chamber = new Vector2(420, 100);
            var nebul = new Vector2(305, 100);
            var cup = new Vector2(220, 40);
            var mono = new Vector2(660, 205);
            var pmt = new Vector2(860, 205);
            float beamY = 205;

            // tháp đèn
            p.Disc(turret.x, turret.y, 62, Body);
            p.Ring(turret.x, turret.y, 62, Edge);
            int n = Math.Max(1, els.Count);
            for (int i = 0; i < n && i < els.Count; i++)
            {
                float ang = (turretAngle + i * 360f / n) * Mathf.Deg2Rad;
                var lp = turret + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 44;
                bool sel = els[i].Symbol == Lamp;
                p.Disc(lp.x, lp.y, 8, sel && Ready ? new Color(1f, 0.85f, 0.5f, 0.4f + 0.6f * Mathf.Clamp01(lampWarm / LampWarmS)) : Off);
            }
            // đầu đốt, buồng phun, ống mao quản, cốc mẫu
            p.FillRect(burner.x - 100, burner.y - 6, 200, 10, new Color(0.55f, 0.6f, 0.65f));
            p.FillRect(chamber.x - 90, chamber.y - 30, 180, 60, Body);
            p.Frame(chamber.x - 90, chamber.y - 30, 180, 60, Edge);
            p.FillRect(burner.x - 8, chamber.y + 30, 16, burner.y - chamber.y - 36, Body);
            p.Disc(nebul.x, nebul.y, 12, Body);
            p.Ring(nebul.x, nebul.y, 12, Edge);
            p.Line(nebul.x - 10, nebul.y - 6, cup.x + 6, cup.y + 10, new Color(0.85f, 0.9f, 1f, 0.7f), 2);
            p.FillRect(cup.x - 22, cup.y - 18, 44, 34, new Color(0.7f, 0.85f, 1f, 0.25f));
            p.Frame(cup.x - 24, cup.y - 20, 48, 40, new Color(0.85f, 0.92f, 1f, 0.6f));
            // đường khí và van
            Valve(p, new Vector2(120, 112), Air, new Color(0.4f, 0.7f, 1f));
            Valve(p, new Vector2(120, 72), Acetylene, new Color(1f, 0.65f, 0.25f));
            p.Line(132, 112, nebul.x - 12, nebul.y + 4, new Color(0.4f, 0.7f, 1f, Air ? 0.9f : 0.3f), 3);
            p.Line(132, 72, chamber.x - 90, chamber.y - 18, new Color(1f, 0.65f, 0.25f, Acetylene ? 0.9f : 0.3f), 3);
            // chụp hút khí thải
            var hood = new[] { new Vector2(330, 318), new Vector2(510, 318), new Vector2(470, 290), new Vector2(370, 290) };
            p.FillPoly(hood, Body);
            for (int k = 0; k < 3; k++)
            {
                float a = (Fan ? time * 600 : 0) + k * 120;
                var c = new Vector2(420, 326);
                var d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad) * 0.35f) * 18;
                p.Line(c.x, c.y, c.x + d.x, c.y + d.y, Fan ? new Color(0.8f, 0.9f, 1f) : Off, 3);
            }
            // ngọn lửa (màu đặc trưng của kim loại khi hút mẫu)
            float T = 1;
            if (Flame)
            {
                var flick = Mathf.Sin(time * 23) * 4 + Mathf.Sin(time * 37) * 3;
                Color outer = new Color(1f, 0.55f, 0.2f, 0.55f);
                if (aspiring && Source != Src.None && Source != Src.Blank) outer = Color.Lerp(outer, FlameColor(), 0.75f);
                p.FillPoly(new[] { new Vector2(burner.x - 100, burner.y + 4), new Vector2(burner.x + 100, burner.y + 4), new Vector2(burner.x + 70, burner.y + 70 + flick), new Vector2(burner.x - 70, burner.y + 66 - flick) }, outer);
                p.FillPoly(new[] { new Vector2(burner.x - 90, burner.y + 4), new Vector2(burner.x + 90, burner.y + 4), new Vector2(burner.x + 50, burner.y + 22), new Vector2(burner.x - 50, burner.y + 22) }, new Color(0.35f, 0.55f, 1f, 0.7f));
                T = (float)Math.Pow(10, -Math.Max(0, Math.Min(3, Signal + zeroOffset)));
            }
            // giọt mẫu: hút lên, phun sương, bay lên ngọn lửa
            if (aspiring && Source != Src.None)
            {
                for (int k = 0; k < 6; k++)
                {
                    float f = Mathf.Repeat(time * 0.8f + k / 6f, 1);
                    var q = Vector2.Lerp(cup + new Vector2(6, 10), nebul + new Vector2(-10, -6), f);
                    p.Disc(q.x, q.y, 2.5f, new Color(0.7f, 0.9f, 1f, 0.9f));
                }
                for (int k = 0; k < 18; k++)
                {
                    float f = Mathf.Repeat(time * 0.6f + k * 0.13f, 1);
                    float x = Mathf.Lerp(chamber.x - 80, chamber.x + 80, Mathf.Repeat(k * 0.37f + time * 0.1f, 1));
                    float y = Mathf.Lerp(chamber.y - 20, Flame ? burner.y + 40 : chamber.y + 20, f);
                    if (y > chamber.y + 25 && Mathf.Abs(x - burner.x) > 8 && y < burner.y) continue;
                    p.Disc(x, y, 1.6f, new Color(0.85f, 0.95f, 1f, 0.6f));
                }
            }
            // bộ đơn sắc, ống nhân quang
            p.FillRect(mono.x - 60, mono.y - 45, 120, 90, Body);
            p.Frame(mono.x - 60, mono.y - 45, 120, 90, Edge);
            p.Line(mono.x - 20, mono.y - 30, mono.x + 20, mono.y + 30, new Color(0.6f, 0.75f, 0.9f), 5);
            p.FillRect(pmt.x - 30, pmt.y - 22, 60, 44, Body);
            p.Frame(pmt.x - 30, pmt.y - 22, 60, 44, Edge);
            p.FillRect(pmt.x + 36, pmt.y - 30, 10, 60, new Color(1, 1, 1, 0.08f));
            if (LampReady && Ready)
            {
                Color line = Painter.Wavelength((float)El.Line);
                float ph = time * 90;
                Beam(p, turret + new Vector2(62, 0), new Vector2(burner.x - 100, beamY), line, ph, 1);
                Beam(p, new Vector2(burner.x - 100, beamY), new Vector2(burner.x + 100, beamY), line, ph, (1 + T) / 2);
                Beam(p, new Vector2(burner.x + 100, beamY), new Vector2(mono.x - 60, beamY), line, ph, T);
                Beam(p, new Vector2(mono.x + 60, beamY), new Vector2(pmt.x - 30, beamY), line, ph, T);
                p.FillRect(pmt.x + 36, pmt.y - 30, 10, 60 * T, new Color(0.45f, 0.95f, 0.62f, 0.9f));
            }

            var d2 = Doc;
            S.SchemLabel("thap", d2.PartName("thap_den") + (El != null ? $"\n<color=#61DBFF>{El.Symbol} · {Fmt(El.Line, "0.0")} nm</color>" : ""), turret.x, turret.y + 88, 12);
            S.SchemLabel("mao", d2.PartName("mao_quan"), cup.x - 40, cup.y + 46, 12);
            S.SchemLabel("phun", d2.PartName("phun_suong"), chamber.x + 150, chamber.y - 4, 12);
            S.SchemLabel("dau_dot", d2.PartName("dau_dot"), burner.x - 175, burner.y + 4, 12);
            S.SchemLabel("lua", Flame ? d2.PartName("ngon_lua") : "(chưa có lửa)", burner.x, burner.y + 92, 12);
            S.SchemLabel("don_sac", d2.PartName("don_sac"), mono.x, mono.y - 62, 12);
            S.SchemLabel("pmt", d2.PartName("pmt") + (LampReady && Ready ? $"\n<color=#73F29E>T = {T * 100:0.#}%</color>" : ""), pmt.x, pmt.y - 52, 12);
            S.SchemLabel("khi", "Không khí\n\nAxetilen", 70, 92, 12);
            S.SchemLabel("hut", Fan ? "Quạt hút: chạy" : "Quạt hút: tắt", 560, 318, 12, Fan ? Good : Dim);
            S.SchemLabel("trang_thai", !Power ? "<color=#8899AA>MÁY TẮT</color>" : !Ready ? "<color=#FFC966>ĐANG TỰ KIỂM TRA…</color>" : Flame ? "<color=#73F29E>LỬA ĐANG CHÁY</color>" : "<color=#61DBFF>SẴN SÀNG (chưa châm lửa)</color>", 760, 330, 14);
        }

        static Color Good => GlassKit.Good;
        static Color Dim => GlassKit.Dim;

        /// <summary>Màu ngọn lửa khi có kim loại (như thí nghiệm thử màu ngọn lửa).</summary>
        Color FlameColor()
        {
            switch (Lamp)
            {
                case "Na": return new Color(1f, 0.85f, 0.1f, 0.75f);
                case "K": return new Color(0.75f, 0.5f, 1f, 0.6f);
                case "Cu": return new Color(0.2f, 0.9f, 0.5f, 0.6f);
                case "Ca": return new Color(1f, 0.35f, 0.15f, 0.65f);
                default: return new Color(1f, 0.6f, 0.25f, 0.55f);
            }
        }

        static void Valve(Painter p, Vector2 c, bool open, Color gas)
        {
            p.Disc(c.x, c.y, 10, open ? new Color(0.45f, 0.95f, 0.62f) : new Color(1f, 0.45f, 0.45f));
            p.Ring(c.x, c.y, 10, gas, 2);
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
            if (plotCal) DrawCal(p);
            else DrawTrace(p);
        }

        void DrawTrace(Painter p)
        {
            float now = Time.time;
            double yMax = 0.2;
            foreach (var t in trace) yMax = Math.Max(yMax, t.a * 1.2);
            S.SetPlotAxes($"Tín hiệu hấp thụ theo thời gian{(El != null ? $" · {El.Symbol} {Fmt(El.Line, "0.0")} nm" : "")}", "Thời gian (30 s gần nhất)", "Độ hấp thụ A", "−30 s", "bây giờ", "0", Fmt(yMax, "0.00"));
            if (trace.Count > 1)
            {
                var pts = trace.Select(t => InstrumentScreen.Map(p, t.t - now + 30, Math.Max(0, Math.Min(yMax, t.a)), 0, 30, 0, yMax)).ToList();
                p.Poly(pts, new Color(0.43f, 0.86f, 1f), 2);
            }
            foreach (var c in CalPoints)
            {
                var q = InstrumentScreen.Map(p, 30, Math.Min(yMax, c.a), 0, 30, 0, yMax);
                p.FillRect(q.x - 30, q.y - 1, 30, 2, new Color(0.45f, 0.95f, 0.62f, 0.5f));
            }
        }

        void DrawCal(Painter p)
        {
            var std = El != null ? El.Standards() : new double[] { 1 };
            double cMax = std[std.Length - 1] * 1.3;
            double aMax = 0.2;
            foreach (var c in CalPoints) aMax = Math.Max(aMax, c.a * 1.25);
            (double meas, int dil, double orig, double a, string note) r = default;
            bool has = El != null && Results.TryGetValue(El.Symbol, out r);
            if (has) { cMax = Math.Max(cMax, r.meas * 1.15); aMax = Math.Max(aMax, r.a * 1.15); }
            S.SetPlotAxes($"Đường chuẩn {(El != null ? El.Symbol : "")}", "Nồng độ (mg/L)", "Độ hấp thụ A", "0", Fmt(cMax, "0.##"), "0", Fmt(aMax, "0.00"));
            if (El != null)
            {
                // đường thật của máy (cong ở nồng độ cao) để thấy vì sao phải pha loãng
                var curve = new List<Vector2>();
                for (int i = 0; i <= 40; i++) { double c = cMax * i / 40; curve.Add(InstrumentScreen.Map(p, c, Math.Min(aMax, El.Absorbance(c)), 0, cMax, 0, aMax)); }
                for (int i = 1; i < curve.Count; i += 2) p.Line(curve[i - 1].x, curve[i - 1].y, curve[i].x, curve[i].y, new Color(1, 1, 1, 0.18f), 2);
            }
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
            if (has)
            {
                var q = InstrumentScreen.Map(p, r.meas, Math.Min(aMax, r.a), 0, cMax, 0, aMax);
                p.Line(q.x - 8, q.y - 8, q.x + 8, q.y + 8, new Color(1f, 0.45f, 0.45f), 3);
                p.Line(q.x - 8, q.y + 8, q.x + 8, q.y - 8, new Color(1f, 0.45f, 0.45f), 3);
            }
        }
    }
}
