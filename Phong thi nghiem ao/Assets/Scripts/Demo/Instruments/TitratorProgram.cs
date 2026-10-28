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
    /// Máy chuẩn độ điện thế tự động (thông số theo Metrohm 916 Ti-Touch + 800 Dosino 20 mL). pH của cốc mẫu tính bằng bộ máy hóa học
    /// sau mỗi lần thêm dung dịch chuẩn; điện cực thật lệch lý tưởng (độ dốc 96,5%, thế lệch +12 mV) nên phải hiệu chuẩn;
    /// NaOH ghi nhãn 0,1 M nhưng thật 0,0982 M nên phải xác định hệ số chuẩn bằng KHP. Chế độ DET tự chỉnh lượng thêm.
    /// Lỗi mô phỏng: không khuấy (pH đo trễ, điểm tương đương lệch), còn bọt khí trong bộ định lượng (thể tích thêm thiếu 1,5%).
    /// </summary>
    public class TitratorProgram : InstrumentProgram
    {
        public override string Code => "chuan_do_dien_the";
        static readonly string[] optional = { "ket_thuc" };
        public override ICollection<string> Optional => optional;

        // điện cực, dung dịch chuẩn thật (mô phỏng)
        public const double TrueSlope = 0.965, TrueOffset = 12, NaOHTrue = 0.0982, HClTrue = 0.1005, Nominal = 0.1;
        const double KhpMass = 0.2042, StopMl = 25, WaterMl = 50;

        // ---- trạng thái ----
        public double Slope { get; private set; } = 1;
        public double Offset { get; private set; }
        public bool Calibrated { get; private set; }
        readonly List<(double ph, double mv)> calPts = new List<(double, double)>();
        public bool Primed { get; private set; }
        public bool Stirring { get; private set; } = true;
        public bool Base { get; private set; } = true;  // NaOH; false: HCl
        public double Titer { get; private set; } = 1;
        public bool TiterDone { get; private set; }
        public double AliquotMl { get; private set; } = 2;
        static readonly double[] aliquots = { 1, 2, 5, 10 };

        enum Run { None, Titer, Sample }
        Run run = Run.None;
        double vDosed, dvNext, measuredMv, waitT;
        public readonly List<(double v, double mv, double ph)> Curve = new List<(double, double, double)>();
        public List<(double v, double dEdV)> Endpoints { get; private set; } = new List<(double, double)>();
        public (double veq, double r2) GranFit { get; private set; } = (double.NaN, 0);
        Mix runMix;
        double runV0L;
        string runName = "";
        int plotMode;  // 0 pH, 1 dE/dV, 2 Gran
        readonly System.Random rng = new System.Random(3);

        public bool Running => run != Run.None;
        public void SetPlot(int m) => plotMode = m;
        public double LastResultEqPerL { get; private set; } = double.NaN;

        Button bCal, bPrep, bStir, bTitrant, bTiter, bAliquot, bSample, bStart, bStop, bPlot, bClean;
        Text status;

        // =====================================================================

        public override void Build()
        {
            bCal = S.AddButton("", 0, 0, CalibrateNext, true, 2);
            bPrep = S.AddButton("Đuổi bọt khí (PREP)", 2, 0, Prime, false, 1, 13);
            bTitrant = S.AddButton("", 0, 1, ToggleTitrant);
            bTiter = S.AddButton("Chuẩn độ KHP (hệ số)", 1, 1, StartTiter, false, 1, 13);
            bStir = S.AddButton("", 2, 1, ToggleStir);
            bAliquot = S.AddButton("", 0, 2, NextAliquot);
            bSample = S.AddButton("", 1, 2, null, false, 2, 13);
            bStart = S.AddButton("Bắt đầu chuẩn độ mẫu", 0, 3, StartSample, true, 2);
            bStop = S.AddButton("Dừng", 2, 3, Stop);
            bPlot = S.AddButton("", 0, 4, () => plotMode = (plotMode + 1) % 3, false, 2);
            bClean = S.AddButton("Rửa, cất điện cực", 2, 4, Finish, false, 1, 13);
            status = S.AddText("", 5, 0, 3, 13);
            S.SetResults("Hiệu chuẩn điện cực trước (3 dung dịch đệm). Đặt ống mẫu vào ổ trên nóc máy ở phòng lab trước khi mở màn hình này.");
        }

        public override void Refresh()
        {
            string calText = Calibrated ? $"Đã hiệu chuẩn: độ dốc {Fmt(Slope * 100, "0.0")}%, lệch {Fmt(Offset, "+0;−0")} mV" : calPts.Count < 3 ? $"Hiệu chuẩn: đo đệm pH {Fmt(PotentiometricTitration.Buffers[calPts.Count], "0.00")}" : "Hiệu chuẩn";
            InstrumentScreen.SetButton(bCal, calText, !Running, Calibrated);
            InstrumentScreen.SetButton(bPrep, Primed ? "Đã đuổi bọt khí" : "Đuổi bọt khí (PREP)", !Running, Primed);
            InstrumentScreen.SetButton(bTitrant, Base ? "Chuẩn: NaOH 0,1 M" : "Chuẩn: HCl 0,1 M", !Running);
            InstrumentScreen.SetButton(bTiter, TiterDone && Base ? $"Hệ số NaOH t = {Fmt(Titer, "0.0000")}" : "Chuẩn độ KHP (hệ số)", !Running && Base, TiterDone && Base);
            InstrumentScreen.SetButton(bStir, Stirring ? "Khuấy: đang bật" : "Khuấy: tắt", true, Stirring);
            InstrumentScreen.SetButton(bAliquot, $"Hút mẫu {Fmt(AliquotMl, "0")} mL", !Running);
            InstrumentScreen.SetButton(bSample, Sample != null ? "Mẫu: " + SampleName : "Ổ mẫu trống", false, Sample != null);
            InstrumentScreen.SetButton(bStart, null, !Running && Sample != null);
            InstrumentScreen.SetButton(bStop, null, Running);
            InstrumentScreen.SetButton(bPlot, plotMode == 0 ? "Đồ thị: pH theo V" : plotMode == 1 ? "Đồ thị: đạo hàm dE/dV" : "Đồ thị: Gran", true);
            double mvNow = Curve.Count > 0 ? Curve[Curve.Count - 1].mv : double.NaN;
            status.text = $"Bộ định lượng: {Fmt(vDosed, "0.000")} / 20 mL · E = {(double.IsNaN(mvNow) ? "—" : Fmt(mvNow, "0.0") + " mV")} · pH = {(double.IsNaN(mvNow) ? "—" : Fmt(PotentiometricTitration.DisplayPh(mvNow, Slope, Offset), "0.00"))}";
            S.SetResults(BuildResults());
        }

        // =====================================================================
        // Thao tác
        // =====================================================================

        public void CalibrateNext()
        {
            if (Running) return;
            if (Calibrated) { calPts.Clear(); Calibrated = false; }
            double ph = PotentiometricTitration.Buffers[calPts.Count];
            double mv = PotentiometricTitration.ElectrodeMv(ph, TrueSlope, TrueOffset) + Gauss() * 0.2;
            calPts.Add((ph, mv));
            S.Say($"Đệm pH {Fmt(ph, "0.00")}: E = {Fmt(mv, "0.0")} mV. Tráng điện cực bằng nước cất trước khi nhúng đệm kế tiếp.", 0);
            if (calPts.Count < 3) return;
            (Slope, Offset) = PotentiometricTitration.Calibrate(calPts);
            Calibrated = true;
            S.Done("hieu_chuan");
            bool ok = Slope >= 0.95 && Slope <= 1.03 && Math.Abs(Offset) <= 15;
            S.Say($"Hiệu chuẩn xong: độ dốc {Fmt(Slope * 100, "0.0")}% (lý thuyết 59,16 mV/pH), thế lệch {Fmt(Offset, "+0.0;−0.0")} mV · " + (ok ? "đạt (95 – 103%, ±15 mV)" : "KHÔNG ĐẠT: làm sạch / thay điện cực"), ok ? 1 : 3);
        }

        public void Prime()
        {
            if (Running) return;
            if (!S.Require("duoi_khi")) return;
            Primed = true;
            vDosed = 0;
            S.Done("duoi_khi");
            S.Say("Đã nạp – đẩy dung dịch chuẩn 2 lần: hết bọt khí trong xilanh và ống dẫn.", 1);
        }

        public void ToggleStir() => Stirring = !Stirring;

        public void ToggleTitrant()
        {
            if (Running) return;
            Base = !Base;
            S.Say(Base ? "Dung dịch chuẩn: NaOH 0,1 M (chuẩn độ mẫu axit)" : "Dung dịch chuẩn: HCl 0,1 M (chuẩn độ mẫu bazơ, độ kiềm của nước)", 0);
        }

        public void NextAliquot()
        {
            if (Running) return;
            int i = Array.IndexOf(aliquots, AliquotMl);
            AliquotMl = aliquots[(i + 1) % aliquots.Length];
            S.Say($"Hút {Fmt(AliquotMl, "0")} mL mẫu bằng pipet vào cốc, thêm {WaterMl:0} mL nước cất cho ngập điện cực.", 0);
        }

        public void SetAliquot(double ml) => AliquotMl = ml;
        public void SetStirring(bool on) => Stirring = on;
        public void SetBase(bool b) => Base = b;

        public void StartTiter()
        {
            if (Running || !Base) return;
            if (!S.Require("titer")) return;
            runMix = null;
            runV0L = WaterMl / 1000.0;
            runName = $"KHP {Fmt(KhpMass, "0.0000")} g";
            Begin(Run.Titer);
            S.Say($"Cân {Fmt(KhpMass, "0.0000")} g KHP ({Fmt(KhpMass / PotentiometricTitration.KhpMolarMass * 1000, "0.000")} mmol), hòa tan trong {WaterMl:0} mL nước, chuẩn độ bằng NaOH…", 0);
        }

        public void StartSample()
        {
            if (Running || Sample == null) return;
            if (!Base && !TiterDone) { TiterDone = true; S.Done("titer"); }  // HCl chuẩn dùng hệ số ghi trên chai (đã chuẩn hóa sẵn)
            if (!S.Require("lay_mau")) return;
            S.Done("lay_mau");
            if (!S.Require("chuan_do")) return;
            double frac = AliquotMl / Math.Max(0.1, Sample.VolumeMl);
            runMix = Sample.Total.Mix.Scale(frac);
            runV0L = (AliquotMl + WaterMl) / 1000.0;
            runName = $"{SampleName}, {Fmt(AliquotMl, "0")} mL";
            Begin(Run.Sample);
            S.Say($"Chuẩn độ {runName} bằng {(Base ? "NaOH" : "HCl")} (chế độ DET)…", 0);
        }

        void Begin(Run r)
        {
            run = r;
            Curve.Clear();
            Endpoints = new List<(double, double)>();
            GranFit = (double.NaN, 0);
            vDosed = 0;
            dvNext = 0.1;
            waitT = 0;
            measuredMv = TrueMv(0);
            Record();
            plotMode = 0;
        }

        public void Stop()
        {
            if (!Running) return;
            Complete();
        }

        public void Finish()
        {
            if (Running) return;
            if (S.DoneSteps.Contains("ket_qua")) S.Done("ket_thuc");
            S.Say("Đã tráng điện cực bằng nước cất và ngâm trong KCl 3 mol/L.", 0);
        }

        // =====================================================================
        // Mô phỏng
        // =====================================================================

        double TrueConc => Base ? NaOHTrue : HClTrue;

        /// <summary>Thể tích thật đã vào cốc (còn bọt khí thì thiếu).</summary>
        double DeliveredL(double nominalMl) => nominalMl / 1000.0 * (Primed ? 1 : 0.985);

        double TruePh(double vMl)
        {
            double vL = DeliveredL(vMl);
            if (run == Run.Titer) return PotentiometricTitration.KhpPh(KhpMass / PotentiometricTitration.KhpMolarMass, runV0L, NaOHTrue, vL);
            return PotentiometricTitration.SamplePh(runMix, runV0L, Base, TrueConc, vL);
        }

        double TrueMv(double vMl) => PotentiometricTitration.ElectrodeMv(TruePh(vMl), TrueSlope, TrueOffset);

        public override void Tick(float dt)
        {
            if (!Running) return;
            waitT += dt;
            // mỗi điểm đo: thêm một lượng, chờ thế ổn định (mô phỏng 0,25 s mỗi điểm cho nhanh; máy thật vài giây)
            const float pointS = 0.06f;
            int guard = 0;
            while (waitT >= pointS && Running && guard++ < 6)
            {
                waitT -= pointS;
                Step();
            }
        }

        void Step()
        {
            double prevMv = measuredMv;
            vDosed += dvNext;
            double target = TrueMv(vDosed);
            // khuấy: điện cực thấy ngay pH mới; không khuấy: pH đo trễ, nhiễu
            double tau = Stirring ? 0.05 : 2.5;
            measuredMv += (target - measuredMv) * (1 - Math.Exp(-1.0 / tau)) + Gauss() * (Stirring ? 0.15 : 2.0);
            Record();
            // DET: muốn mỗi bước thế đổi khoảng 8 mV
            double dE = Math.Abs(measuredMv - prevMv);
            dvNext = Math.Max(0.01, Math.Min(0.4, dvNext * 8.0 / Math.Max(0.5, dE)));
            if (vDosed >= StopMl) { S.Say($"Đạt thể tích dừng {StopMl:0} mL", 2); Complete(); return; }
            var eps = PotentiometricTitration.Endpoints(Curve.Select(c => (c.v, c.mv)).ToList());
            if (eps.Count > 0 && vDosed > eps[eps.Count - 1].v + 2.5 && Curve.Count > 10)
            {
                double phNow = Curve[Curve.Count - 1].ph, phBack = Curve.Last(c => c.v <= vDosed - 1).ph;
                if (Math.Abs(phNow - phBack) < 0.25) Complete();
            }
        }

        void Record() => Curve.Add((vDosed, measuredMv, PotentiometricTitration.DisplayPh(measuredMv, Slope, Offset)));

        void Complete()
        {
            var r = run;
            run = Run.None;
            var pts = Curve.Select(c => (c.v, c.mv)).ToList();
            Endpoints = PotentiometricTitration.Endpoints(pts);
            if (Endpoints.Count == 0) { S.Say("Không tìm thấy điểm tương đương (không có bước nhảy thế rõ). Kiểm tra lượng mẫu, dung dịch chuẩn.", 2); return; }
            double v1 = Endpoints[0].v;
            var phPts = Curve.Select(c => (c.v, c.ph)).ToList();
            GranFit = Base ? PotentiometricTitration.Gran(phPts, v1 * 0.2, v1 * 0.8) : (double.NaN, 0);
            if (r == Run.Titer)
            {
                double n = KhpMass / PotentiometricTitration.KhpMolarMass;
                Titer = n / (Nominal * v1 / 1000.0);
                TiterDone = true;
                S.Done("titer");
                S.Say($"Điểm tương đương {Fmt(v1, "0.000")} mL → c(NaOH) thật = {Fmt(Nominal * Titer, "0.0000")} M, hệ số t = {Fmt(Titer, "0.0000")}", 1);
                return;
            }
            S.Done("chuan_do");
            S.Done("ket_qua");
            double c = (Base ? Nominal * Titer : HClTrue);
            LastResultEqPerL = c * v1 / AliquotMl;
            S.Say($"Điểm tương đương {Fmt(v1, "0.000")} mL · nồng độ đương lượng {Fmt(LastResultEqPerL, "0.0000")} mol/L" + (Endpoints.Count > 1 ? $" ({Endpoints.Count} điểm tương đương)" : ""), 1);
            if (!Stirring) S.Say("Chuẩn độ khi không khuấy: pH đo trễ, đường cong răng cưa, điểm tương đương bị lệch về sau.", 2);
            else if (Base && !TiterDone) S.Say("Chưa xác định hệ số chuẩn NaOH: kết quả tính theo 0,1 M ghi nhãn (cao hơn thật khoảng 1,8%).", 2);
        }

        double Gauss()
        {
            double u1 = 1 - rng.NextDouble(), u2 = rng.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }

        // =====================================================================
        // Kết quả
        // =====================================================================

        /// <summary>Axit / bazơ chính trong mẫu (để đổi nồng độ đương lượng ra hàm lượng chất).</summary>
        (string name, double M, int eq, double pKa) MainSpecies()
        {
            if (Sample == null) return ("", 0, 1, double.NaN);
            var m = Sample.Total.Mix;
            var known = new (string id, string name, double M, int eq, double pKa)[]
            {
                ("Ac", "axit axetic", 60.05, 1, 4.76), ("Cit", "axit xitric", 192.12, 3, double.NaN), ("Lac", "axit lactic", 90.08, 1, 3.86),
                ("Fo", "axit fomic", 46.03, 1, 3.75), ("Ox", "axit oxalic", 90.03, 2, double.NaN), ("Tart", "axit tartaric", 150.09, 2, double.NaN),
                ("CO3", "hiđrocacbonat (độ kiềm)", 61.02, 1, double.NaN), ("PO4", "axit photphoric", 98.0, 1, double.NaN),
            };
            var best = known.OrderByDescending(k => m.Get(k.id)).First();
            return m.Get(best.id) > 1e-6 ? (best.name, best.M, best.eq, best.pKa) : ("", 0, 1, double.NaN);
        }

        string BuildResults()
        {
            var sb = new StringBuilder();
            sb.Append($"<color=#9FB3C8>Điện cực:</color> {(Calibrated ? $"độ dốc {Fmt(Slope * 100, "0.0")}%, lệch {Fmt(Offset, "+0.0;−0.0")} mV" : "chưa hiệu chuẩn (máy coi như lý tưởng 100%, 0 mV)")}\n");
            sb.Append($"<color=#9FB3C8>Dung dịch chuẩn:</color> {(Base ? "NaOH" : "HCl")} 0,1 M" + (Base && TiterDone ? $" × t {Fmt(Titer, "0.0000")} = {Fmt(Nominal * Titer, "0.0000")} M" : Base ? " (chưa xác định hệ số)" : "") + "\n");
            if (Running) sb.Append($"\n<color=#61DBFF>Đang chuẩn độ {runName}: {Curve.Count} điểm đo</color>\n");
            if (!Running && Curve.Count > 3 && Endpoints.Count > 0)
            {
                sb.Append($"\n<b>Điểm tương đương ({runName})</b>\n");
                for (int i = 0; i < Endpoints.Count; i++)
                {
                    var e = Endpoints[i];
                    double ph = Curve.OrderBy(c => Math.Abs(c.v - e.v)).First().ph;
                    sb.Append($"EP{i + 1}: V = <b>{Fmt(e.v, "0.000")} mL</b>, pH {Fmt(ph, "0.00")}, dE/dV = {Fmt(Math.Abs(e.dEdV), "0")} mV/mL\n");
                }
                if (!double.IsNaN(GranFit.veq)) sb.Append($"<color=#9FB3C8>Kiểm tra bằng đồ thị Gran: V_tđ = {Fmt(GranFit.veq, "0.000")} mL (R² {Fmt(GranFit.r2, "0.0000")})</color>\n");
                if (!double.IsNaN(LastResultEqPerL) && runName.StartsWith(SampleName))
                {
                    double c = (Base ? Nominal * Titer : HClTrue);
                    double n = c * Endpoints[0].v;
                    sb.Append($"\n<b>Phân tích chuyên sâu</b>\nn = c·V = {Fmt(c, "0.0000")} × {Fmt(Endpoints[0].v, "0.000")} = {Fmt(n, "0.000")} mmol (đương lượng)\n");
                    sb.Append($"C = n / V_mẫu = {Fmt(n, "0.000")} / {Fmt(AliquotMl, "0")} = <b>{Fmt(LastResultEqPerL, "0.0000")} mol/L</b>\n");
                    var (name, M, eq, pKa) = MainSpecies();
                    if (M > 0)
                    {
                        double cm = LastResultEqPerL / eq;
                        sb.Append($"Nếu là {name} ({eq} H⁺ trao đổi): {Fmt(cm, "0.0000")} mol/L = <b>{Fmt(cm * M, "0.00")} g/L</b>");
                        if (name == "axit axetic") sb.Append($" (độ chua giấm {Fmt(cm * M / 10, "0.00")} g/100 mL)");
                        sb.Append('\n');
                        if (!double.IsNaN(pKa) && Base)
                        {
                            double phHalf = Curve.OrderBy(cv => Math.Abs(cv.v - Endpoints[0].v / 2)).First().ph;
                            sb.Append($"pH tại ½V_tđ = {Fmt(phHalf, "0.00")} ≈ pKa (tài liệu {Fmt(pKa, "0.00")})" + (Calibrated ? "" : " <color=#FFC966>– điện cực chưa hiệu chuẩn nên pH lệch</color>") + "\n");
                        }
                    }
                    if (Endpoints.Count >= 2) sb.Append("<color=#9FB3C8>Nhiều điểm tương đương: axit nhiều nấc có pKa cách xa nhau (ví dụ H₃PO₄).</color>\n");
                }
            }
            return sb.ToString();
        }

        // =====================================================================
        // Sơ đồ
        // =====================================================================

        public override void DrawSchematic(Painter p, float time)
        {
            SchemKit.Begin(p);
            float ph = time * 60;
            // bộ định lượng
            var dos = new Vector2(120, 200);
            SchemKit.Box(p, dos.x - 45, dos.y - 110, 90, 230);
            float fill = 1 - (float)(vDosed % 20 / 20.0);
            p.FillRect(dos.x - 22, dos.y - 90, 44, 180 * fill, Base ? new Color(0.75f, 0.85f, 1f, 0.55f) : new Color(1f, 0.85f, 0.75f, 0.55f));
            p.Frame(dos.x - 24, dos.y - 92, 48, 184, SchemKit.Edge);
            p.FillRect(dos.x - 22, dos.y - 90 + 180 * fill, 44, 8, new Color(0.7f, 0.7f, 0.75f));  // piston
            p.FillRect(dos.x - 3, dos.y - 90 + 180 * fill + 8, 6, 120 - 180 * fill + 90, new Color(0.55f, 0.55f, 0.6f));
            // chai chuẩn
            SchemKit.Vessel(p, 30, 40, 50, 90, 0.8f, Base ? new Color(0.75f, 0.85f, 1f, 0.5f) : new Color(1f, 0.85f, 0.75f, 0.5f));
            SchemKit.Pipe(p, new[] { new Vector2(55, 130), new Vector2(55, 160), new Vector2(dos.x - 24, 160) }, false, ph);
            // ống dẫn tới cốc
            var beaker = new Vector2(560, 120);
            SchemKit.Pipe(p, new[] { new Vector2(dos.x, dos.y + 120), new Vector2(dos.x, 330), new Vector2(beaker.x - 40, 330), new Vector2(beaker.x - 40, beaker.y + 40) }, Running, ph);
            // cốc + khuấy
            var lq = Sample != null ? Sample.Look.Liquid : default;
            Color liq = Sample != null && run == Run.Sample ? new Color(lq.r, lq.g, lq.b) : new Color(0.8f, 0.9f, 1f);
            liq.a = 0.45f;
            SchemKit.Vessel(p, beaker.x - 110, beaker.y - 90, 220, 170, 0.6f, liq);
            SchemKit.Box(p, beaker.x - 130, beaker.y - 115, 260, 22, new Color(0.18f, 0.2f, 0.25f));
            if (Stirring)
            {
                float a = time * 14;
                p.Line(beaker.x - Mathf.Cos(a) * 22, beaker.y - 78, beaker.x + Mathf.Cos(a) * 22, beaker.y - 78, new Color(0.95f, 0.95f, 0.95f), 6);
            }
            else p.Line(beaker.x - 22, beaker.y - 78, beaker.x + 22, beaker.y - 78, new Color(0.7f, 0.7f, 0.7f), 6);
            if (Running && Mathf.Repeat(time * 3, 1) < 0.3f) p.Disc(beaker.x - 40, beaker.y + 30, 4, new Color(0.8f, 0.9f, 1f));
            // điện cực
            var el = new Vector2(beaker.x + 30, beaker.y);
            p.FillRect(el.x - 10, el.y - 50, 20, 230, new Color(0.75f, 0.85f, 0.95f, 0.35f));
            p.Frame(el.x - 10, el.y - 50, 20, 230, SchemKit.Edge);
            p.Disc(el.x, el.y - 52, 12, new Color(0.7f, 0.85f, 1f, 0.6f));  // bầu thủy tinh
            p.FillRect(el.x - 3, el.y - 30, 6, 200, new Color(0.75f, 0.75f, 0.8f));  // dây Ag/AgCl
            // cáp tới bộ điều khiển
            var ctl = new Vector2(850, 200);
            SchemKit.Pipe(p, new[] { new Vector2(el.x, el.y + 180), new Vector2(ctl.x - 130, el.y + 180), new Vector2(ctl.x - 130, ctl.y), new Vector2(ctl.x - 90, ctl.y) }, false, ph, null, 3);
            SchemKit.Box(p, ctl.x - 90, ctl.y - 80, 180, 160, new Color(0.08f, 0.1f, 0.14f));
            // đường cong thu nhỏ trên màn hình bộ điều khiển
            if (Curve.Count > 1)
            {
                double vmax = Math.Max(1, Curve[Curve.Count - 1].v);
                var pts = Curve.Select(c => new Vector2(ctl.x - 80 + (float)(c.v / vmax) * 160, ctl.y - 70 + (float)Math.Max(0, Math.Min(14, c.ph)) / 14f * 140)).ToList();
                p.Poly(pts, SchemKit.Good, 2);
            }
            var d = Doc;
            S.SchemLabel("dosino", d.PartName("dosino") + $"\n<color=#61DBFF>{Fmt(vDosed, "0.000")} mL</color>", dos.x + 20, 62, 12);
            S.SchemLabel("chai", d.PartName("chai_chuan"), 55, 22, 11);
            S.SchemLabel("dau", d.PartName("dau_nho"), beaker.x - 120, beaker.y + 120, 11);
            S.SchemLabel("dien_cuc", d.PartName("dien_cuc"), el.x + 110, el.y + 120, 12);
            S.SchemLabel("khuay", d.PartName("khuay") + (Stirring ? "" : " <color=#FFC966>(tắt)</color>"), beaker.x + 205, beaker.y - 104, 12);
            double mv = Curve.Count > 0 ? Curve[Curve.Count - 1].mv : PotentiometricTitration.ElectrodeMv(7, TrueSlope, TrueOffset);
            S.SchemLabel("ctl", d.PartName("bo_dieu_khien") + $"\n<color=#73F29E>{Fmt(mv, "0.0")} mV · pH {Fmt(PotentiometricTitration.DisplayPh(mv, Slope, Offset), "0.00")}</color>", ctl.x, ctl.y - 105, 12);
            S.SchemLabel("trang_thai", Running ? $"<color=#61DBFF>ĐANG CHUẨN ĐỘ (DET): thêm {Fmt(dvNext, "0.000")} mL/lần</color>" : "<color=#73F29E>SẴN SÀNG</color>", 760, 335, 14);
        }

        // =====================================================================
        // Đồ thị
        // =====================================================================

        public override void DrawPlot(Painter p)
        {
            InstrumentScreen.PlotFrame(p);
            double vmax = Math.Max(5, Curve.Count > 0 ? Curve[Curve.Count - 1].v * 1.05 : 5);
            if (plotMode == 0 || Curve.Count < 3)
            {
                S.SetPlotAxes($"Đường cong chuẩn độ {runName}", $"Thể tích {(Base ? "NaOH" : "HCl")} thêm vào (mL)", "pH (máy hiển thị)", "0", Fmt(vmax, "0.0"), "0", "14");
                var pts = Curve.Select(c => InstrumentScreen.Map(p, c.v, Math.Max(0, Math.Min(14, c.ph)), 0, vmax, 0, 14)).ToList();
                p.Poly(pts, new Color(0.43f, 0.86f, 1f), 3);
                foreach (var c in Curve) { var q = InstrumentScreen.Map(p, c.v, Math.Max(0, Math.Min(14, c.ph)), 0, vmax, 0, 14); p.Disc(q.x, q.y, 2.5f, new Color(0.8f, 0.95f, 1f)); }
            }
            else if (plotMode == 1)
            {
                var der = PotentiometricTitration.Derivative(Curve.Select(c => (c.v, c.mv)).ToList());
                double dmax = Math.Max(50, der.Count > 0 ? der.Max(x => Math.Abs(x.d)) * 1.1 : 50);
                S.SetPlotAxes("Đạo hàm |dE/dV|: cực đại = điểm tương đương", "Thể tích (mL)", "|dE/dV| (mV/mL)", "0", Fmt(vmax, "0.0"), "0", Fmt(dmax, "0"));
                p.Poly(der.Select(x => InstrumentScreen.Map(p, x.v, Math.Abs(x.d), 0, vmax, 0, dmax)).ToList(), new Color(1f, 0.75f, 0.35f), 3);
            }
            else
            {
                var g = Curve.Where(c => c.v > 0).Select(c => (c.v, y: c.v * Math.Pow(10, -c.ph) * 1e5)).ToList();
                double gmax = Math.Max(1e-3, g.Count > 0 ? g.Where(x => Endpoints.Count == 0 || x.v < Endpoints[0].v).Select(x => x.y).DefaultIfEmpty(1).Max() * 1.2 : 1);
                S.SetPlotAxes("Đồ thị Gran: V·10⁻ᵖᴴ theo V, cắt trục V ở V_tđ", "Thể tích (mL)", "V·10⁻ᵖᴴ (×10⁻⁵)", "0", Fmt(vmax, "0.0"), "0", Fmt(gmax, "0.00"));
                foreach (var x in g) { var q = InstrumentScreen.Map(p, x.v, Math.Min(gmax, x.y), 0, vmax, 0, gmax); p.Disc(q.x, q.y, 3, new Color(0.7f, 0.6f, 1f)); }
                if (!double.IsNaN(GranFit.veq) && Endpoints.Count > 0)
                {
                    var sel = g.Where(x => x.v >= Endpoints[0].v * 0.2 && x.v <= Endpoints[0].v * 0.8).ToList();
                    if (sel.Count >= 2)
                    {
                        var f = UvVisLibrary.Linear(sel.Select(x => (x.v, x.y)).ToList());
                        var a = InstrumentScreen.Map(p, 0, Math.Min(gmax, f.Intercept), 0, vmax, 0, gmax);
                        var b = InstrumentScreen.Map(p, GranFit.veq, 0, 0, vmax, 0, gmax);
                        p.Line(a.x, a.y, b.x, b.y, new Color(1f, 0.6f, 0.9f, 0.8f), 2);
                    }
                }
            }
            foreach (var e in Endpoints)
            {
                var m = InstrumentScreen.Map(p, e.v, 0, 0, vmax, 0, 1);
                p.FillRect(m.x - 1, 0, 2, p.H, new Color(1f, 0.45f, 0.45f, 0.7f));
            }
        }
    }
}
