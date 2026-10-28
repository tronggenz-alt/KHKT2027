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
    /// Bom nhiệt lượng isoperibol (thông số theo IKA C 200: oxy 30 bar, tối đa 40 kJ, nhiệt kế 0,0001 K). Năng lượng mẫu tính từ nhiệt đốt cháy
    /// NIST; nhiệt dung thật của hệ C = 10 050 J/K phải tìm bằng axit benzoic chuẩn (26 434 J/g, SRM 39j). Đường nhiệt độ có trao đổi nhiệt với vỏ,
    /// hiệu chỉnh Regnault – Pfaundler. Lỗi mô phỏng: không hiệu chuẩn (dùng C ghi trên máy 10 000 J/K), oxy thiếu (cháy không hết, muội than),
    /// mẫu ướt (không cháy), mẫu quá nặng (vượt 40 kJ, máy không cho đốt), lấy ΔT thô không hiệu chỉnh.
    /// </summary>
    public class CalorimeterProgram : InstrumentProgram
    {
        public override string Code => "nhiet_luong";
        static readonly string[] optional = { "ket_thuc" };
        public override ICollection<string> Optional => optional;

        public const double TrueC = 10050, NominalC = 10000, WireJ = 50, MaxJ = 40000;
        const double Tj = 25.0, K = 0.003, Stir = 0.0004, FireAt = 5, MainEnd = 15, Minutes = 22, Dt = 0.05;
        const float RunS = 8f;

        public readonly SolidSource Src;
        public double MassG { get; private set; } = 1.0;
        static readonly double[] masses = { 0.5, 0.8, 1.0, 1.5, 2.0 };
        public bool Dried { get; private set; }
        public double O2Bar { get; private set; } = 30;
        public bool Filled { get; private set; }
        public bool InWater { get; private set; }
        public double CalibratedC { get; private set; } = double.NaN;
        public bool Calibrated => !double.IsNaN(CalibratedC);

        List<SolidResidue.Phase> charge = new List<SolidResidue.Phase>();
        double chargeEnergy, burnFrac = 1;
        CalorimetryLibrary.Run run;
        float runT = -1;
        public double RawDT { get; private set; } = double.NaN;
        public double CorrDT { get; private set; } = double.NaN;
        public double Q { get; private set; } = double.NaN;
        public double SpecificJg { get; private set; } = double.NaN;
        string runName = "";
        bool lastWasStandard;
        readonly List<string> unknown = new List<string>();
        readonly System.Random rng = new System.Random(31);

        CalorimetryLibrary Lib => InstrumentData.Instance != null ? InstrumentData.Instance.Calorimetry : new CalorimetryLibrary();
        public bool Running => runT >= 0;
        bool Standard => Src.Current?.Name.StartsWith("Chuẩn: axit benzoic") == true;

        Button bSrc, bDry, bMass, bO2, bFill, bWater, bFire, bCalc, bCal, bVent;
        Text status;

        public CalorimeterProgram()
        {
            Src = new SolidSource(true,
                ("Chuẩn: axit benzoic (NIST SRM 39j), viên nén", new[] { SolidSource.Pure("org:Bz", "C7H6O2", "axit benzoic", 1000, true, true) }),
                ("Viên glucozơ", new[] { SolidSource.Pure("org:hc:578", "C6H12O6", "glucozơ", 1000, false, true) }),
                ("Viên saccarozơ (đường kính)", new[] { SolidSource.Pure("org:hc:580", "C12H22O11", "saccarozơ", 1000, true, true) }),
                ("Etanol tuyệt đối trong nang", new[] { SolidSource.Pure("org:C2H5OH", "C2H6O", "etanol", 1000, false, true) }));
            Src.Index = 2;  // mặc định: mẫu chuẩn axit benzoic (sau 2 lựa chọn ống mẫu)
        }

        public override void Build()
        {
            bSrc = S.AddButton("", 0, 0, () => { Src.Next(); Reset(); }, false, 2, 12);
            bDry = S.AddButton("Sấy khô mẫu", 2, 0, Dry, false, 1, 13);
            bMass = S.AddButton("", 0, 1, NextMass);
            bO2 = S.AddButton("", 1, 1, NextO2);
            bFill = S.AddButton("Lắp sợi mồi, nạp O₂", 2, 1, Fill, false, 1, 12);
            bWater = S.AddButton("Đặt bom vào bình nước", 0, 2, PlaceInWater, false, 1, 12);
            bFire = S.AddButton("ĐỐT", 1, 2, Fire, true);
            bCalc = S.AddButton("Tính kết quả", 2, 2, Calculate);
            bCal = S.AddButton("Dùng làm hiệu chuẩn C", 0, 3, UseAsCalibration, false, 2, 13);
            bVent = S.AddButton("Xả khí, rửa bom", 2, 3, Vent);
            status = S.AddText("", 4, 0, 3, 13);
            S.SetResults("Hiệu chuẩn trước: đốt khoảng 1 g axit benzoic chuẩn để tìm nhiệt dung C của hệ. Mẫu lỏng / dung dịch phải sấy khô trước khi đốt.");
        }

        void Reset() { Dried = false; Filled = false; InWater = false; }

        /// <summary>Lần đốt chất chuẩn để hiệu chuẩn không bị chặn bởi bước hiệu chuẩn (chính nó là bước đó).</summary>
        bool Need(string code) => Standard || S.Require(code);

        public override void Refresh()
        {
            InstrumentScreen.SetButton(bSrc, "Mẫu: " + Src.Current?.Name, !Running && !Filled);
            InstrumentScreen.SetButton(bDry, Src.FromTube ? (Dried ? "Đã sấy khô" : "Sấy khô mẫu") : "Mẫu khô sẵn", !Running && Src.FromTube && !Filled, Dried || !Src.FromTube);
            InstrumentScreen.SetButton(bMass, Src.FromTube ? $"Lấy {Src.TakeMl:0} mL mẫu" : $"Cân {Fmt(MassG, "0.0000")} g", !Running && !Filled);
            InstrumentScreen.SetButton(bO2, $"Oxy {O2Bar:0} bar", !Running && !Filled);
            InstrumentScreen.SetButton(bFill, Filled ? $"Bom đã nạp {O2Bar:0} bar" : "Lắp sợi mồi, nạp O₂", !Running && !Filled, Filled);
            InstrumentScreen.SetButton(bWater, InWater ? "Bom trong bình, đang khuấy" : "Đặt bom vào bình nước", Filled && !InWater, InWater);
            InstrumentScreen.SetButton(bFire, Running ? $"Đang đo {RunMinutes:0.0} phút" : "ĐỐT", InWater && !Running && run == null);
            InstrumentScreen.SetButton(bCalc, null, run != null && !Running);
            InstrumentScreen.SetButton(bCal, Calibrated ? $"C = {Fmt(CalibratedC, "0")} J/K (đã hiệu chuẩn)" : "Dùng làm hiệu chuẩn C", !double.IsNaN(CorrDT) && lastWasStandard, Calibrated);
            InstrumentScreen.SetButton(bVent, null, (Filled || run != null) && !Running);
            double T = run != null && run.Curve.Count > 0 ? run.Curve[Math.Min(run.Curve.Count - 1, (int)(RunMinutes / Dt))].T : 24.6;
            status.text = $"Nhiệt độ nước {Fmt(T, "0.0000")} °C · vỏ {Fmt(Tj, "0.0")} °C · nhiệt dung đang dùng {(Calibrated ? Fmt(CalibratedC, "0") + " J/K (hiệu chuẩn)" : Fmt(NominalC, "0") + " J/K (ghi trên máy)")}";
            S.SetResults(BuildResults());
        }

        double RunMinutes => run == null ? 0 : Running ? Minutes * Mathf.Clamp01(runT / RunS) : Minutes;

        // =====================================================================
        // Thao tác
        // =====================================================================

        public void SelectSource(string prefix) { Src.Select(prefix); Reset(); run = null; }

        public void NextMass()
        {
            if (Src.FromTube) Src.TakeMl = Src.TakeMl == 10 ? 20 : Src.TakeMl == 20 ? 5 : 10;
            else MassG = masses[(Array.IndexOf(masses, MassG) + 1) % masses.Length];
            Dried = false;
        }

        public void SetMass(double g) => MassG = g;
        public void SetO2(double bar) => O2Bar = bar;
        public void NextO2() => O2Bar = O2Bar == 30 ? 40 : O2Bar == 40 ? 10 : O2Bar == 10 ? 20 : 30;

        public void Dry()
        {
            if (!Src.FromTube) return;
            Dried = true;
            S.Say($"Sấy {Src.TakeMl:0} mL mẫu ở 105 °C đến khối lượng không đổi: nước và chất dễ bay hơi bay đi.", 0);
        }

        /// <summary>Viên mẫu: khối lượng thật cân được (lệch nhẹ so với số chọn), năng lượng.</summary>
        void Prepare()
        {
            unknown.Clear();
            var solids = Src.Solids(Sample);
            if (!Src.FromTube)
            {
                double m = MassG * (0.995 + rng.NextDouble() * 0.01);
                solids = solids.Select(s => SolidSource.Pure(s.Id, s.Formula, s.Name, m * 1000 * s.Mg / Math.Max(1e-9, SolidResidue.TotalMg(solids)), s.Crystalline, s.Organic)).ToList();
            }
            charge = solids;
            chargeEnergy = Lib.Energy(charge, out _, unknown);
        }

        public void Fill()
        {
            if (Filled || Running) return;
            if (!Need("nap_bom")) return;
            if (Src.FromTube && Sample == null) { S.Say("Ổ mẫu trống", 2); return; }
            Prepare();
            double mg = SolidResidue.TotalMg(charge);
            S.Done("chuan_bi_mau");
            if (mg < 20) { S.Say($"Chỉ có {Fmt(mg, "0.0")} mg chất khô: quá ít để đo. Lấy nhiều mẫu hơn.", 2); return; }
            if (chargeEnergy + WireJ > MaxJ) { S.Say($"Mẫu tỏa khoảng {Fmt((chargeEnergy + WireJ) / 1000, "0.0")} kJ, vượt giới hạn an toàn 40 kJ của bom: giảm khối lượng mẫu.", 3); return; }
            Filled = true;
            burnFrac = O2Bar >= 25 ? 1 : O2Bar >= 18 ? 0.985 : 0.93;
            S.Done("nap_bom");
            S.Say($"Đã cân {Fmt(mg / 1000, "0.0000")} g mẫu vào chén, buộc sợi mồi, đóng bom, nạp oxy {O2Bar:0} bar.", 0);
        }

        public void PlaceInWater()
        {
            if (!Filled || InWater) return;
            if (!Need("dat_bom")) return;
            InWater = true;
            S.Done("dat_bom");
            S.Say("Đặt bom vào bình chứa 2000 g nước, đóng nắp, bật khuấy.", 0);
        }

        public void Fire()
        {
            if (!InWater || Running) return;
            if (!Need("dot")) return;
            bool wet = Src.FromTube && !Dried;
            double q = wet ? WireJ : chargeEnergy * burnFrac + WireJ;
            runName = Src.FromTube ? $"{Src.TakeMl:0} mL {SampleName} (chất khô)" : $"{Fmt(SolidResidue.TotalMg(charge) / 1000, "0.0000")} g {charge.FirstOrDefault()?.Name}";
            run = CalorimetryLibrary.Simulate(24.6, Tj, K, Stir, TrueC, q, FireAt, 0.7, Minutes, Dt, 0.0001, rng);
            runT = 0;
            RawDT = CorrDT = Q = SpecificJg = double.NaN;
            lastWasStandard = Standard;
            S.Say("Ghi nhiệt độ tiền kỳ 5 phút, đốt, rồi chính kỳ và hậu kỳ…", 0);
            if (wet) S.Say("Mẫu còn ướt (nhiều nước): sợi mồi cháy nhưng mẫu không bắt lửa! Phải sấy khô mẫu trước.", 3);
            else if (burnFrac < 1) S.Say($"Oxy chỉ {O2Bar:0} bar: mẫu cháy không hết, trong chén còn muội than.", 2);
        }

        public override void Tick(float dt)
        {
            if (!Running) return;
            runT += dt;
            if (runT >= RunS) { runT = -1; S.Done("dot"); S.Say("Kết thúc hậu kỳ. Bấm Tính kết quả.", 1); }
        }

        public void Calculate()
        {
            if (run == null || Running) return;
            if (!Need("tinh")) return;
            (RawDT, CorrDT) = CalorimetryLibrary.CorrectedRise(run.Curve, FireAt, MainEnd);
            double C = Calibrated ? CalibratedC : NominalC;
            Q = C * CorrDT - WireJ;
            double mg = SolidResidue.TotalMg(charge);
            SpecificJg = mg > 0 ? Q / (mg / 1000) : double.NaN;
            S.Done("tinh");
            S.Say($"ΔT hiệu chỉnh = {Fmt(CorrDT, "0.0000")} K (thô {Fmt(RawDT, "0.0000")} K) → Q = {Fmt(Q, "0")} J, {Fmt(SpecificJg, "0")} J/g", 1);
            if (!Calibrated && !lastWasStandard) S.Say("Chưa hiệu chuẩn: đang dùng C ghi trên máy (10 000 J/K), kết quả lệch. Đốt axit benzoic chuẩn trước.", 2);
        }

        public void UseAsCalibration()
        {
            if (double.IsNaN(CorrDT) || !lastWasStandard) return;
            if (!S.Require("hieu_chuan") && S.Guided) return;
            double mg = SolidResidue.TotalMg(charge);
            CalibratedC = (mg / 1000 * 26434 + WireJ) / CorrDT;
            S.Done("hieu_chuan");
            S.Say($"Hiệu chuẩn: C = (m·26 434 J/g + {WireJ:0} J sợi mồi) / ΔT = {Fmt(CalibratedC, "0")} J/K", 1);
        }

        public void Vent()
        {
            if (Running) return;
            Filled = InWater = false;
            run = null;
            if (S.DoneSteps.Contains("tinh")) S.Done("ket_thuc");
            S.Say("Xả khí trong bom từ từ, mở, rửa và lau khô bom.", 0);
        }

        // =====================================================================
        // Kết quả
        // =====================================================================

        string BuildResults()
        {
            var sb = new StringBuilder();
            sb.Append($"<color=#9FB3C8>Mẫu:</color> {Src.Describe(Sample)}\n");
            if (Filled) sb.Append($"<color=#9FB3C8>Trong chén:</color> {string.Join(", ", charge.Select(s => $"{s.Name} {Fmt(s.Mg, "0.0")} mg"))}\n");
            if (!double.IsNaN(Q))
            {
                double C = Calibrated ? CalibratedC : NominalC;
                double mg = SolidResidue.TotalMg(charge);
                sb.Append($"\n<b>Kết quả ({runName})</b>\nΔT thô {Fmt(RawDT, "0.0000")} K · hiệu chỉnh trao đổi nhiệt (Regnault – Pfaundler) {Fmt(CorrDT, "0.0000")} K\n");
                sb.Append($"Q = C·ΔT − Q_sợi mồi = {Fmt(C, "0")} × {Fmt(CorrDT, "0.0000")} − {WireJ:0} = <b>{Fmt(Q, "0")} J</b>\n");
                sb.Append($"Nhiệt đốt cháy: <b>{Fmt(SpecificJg, "0")} J/g</b> = {Fmt(SpecificJg / 4184, "0.00")} kcal/g\n");
                sb.Append("\n<b>Phân tích chuyên sâu</b>\n");
                var pure = charge.Count == 1 ? Lib.Get(charge[0].Id.StartsWith("org:") ? charge[0].Id.Substring(4) : charge[0].Id) : null;
                if (pure != null)
                {
                    double kjmol = SpecificJg * pure.M / 1000;
                    double dH = -kjmol + pure.DeltaN * 8.314 * 298.15 / 1000;
                    sb.Append($"ΔcU = −{Fmt(kjmol, "0")} kJ/mol; ΔcH = ΔcU + Δn·RT = {Fmt(dH, "0")} kJ/mol (Δn khí = {Fmt(pure.DeltaN, "0.#")})\n");
                    sb.Append($"<color=#9FB3C8>NIST: ΔcH° = {Fmt(pure.DeltaH, "0.#")} kJ/mol → sai {Fmt((dH / pure.DeltaH - 1) * 100, "+0.0;−0.0")}%</color>\n");
                }
                if (Src.FromTube && mg > 0)
                {
                    double perMl = Q / Src.TakeMl;
                    sb.Append($"Chất khô {Fmt(mg / Src.TakeMl * 100 / 1000, "0.0")} g/100 mL · năng lượng <b>{Fmt(perMl * 100 / 1000, "0")} kJ/100 mL = {Fmt(perMl * 100 / 4184, "0")} kcal/100 mL</b>\n");
                    sb.Append("<color=#9FB3C8>Bom đo năng lượng toàn phần; năng lượng dinh dưỡng (hệ số Atwater) thấp hơn một chút vì cơ thể không đốt hết.</color>\n");
                }
                if (unknown.Count > 0) sb.Append($"<color=#FFC966>Chất hữu cơ chưa có nhiệt đốt cháy trong thư viện (không tính):</color> {string.Join(", ", unknown.Distinct())}\n");
            }
            return sb.ToString();
        }

        // =====================================================================
        // Sơ đồ
        // =====================================================================

        public override void DrawSchematic(Painter p, float time)
        {
            SchemKit.Begin(p);
            var c = new Vector2(330, 165);
            SchemKit.Box(p, c.x - 220, c.y - 150, 440, 300, new Color(0.15f, 0.2f, 0.3f));   // vỏ
            SchemKit.Vessel(p, c.x - 170, c.y - 120, 340, 230, 0.85f, new Color(0.35f, 0.6f, 0.95f, 0.45f));  // bình nước
            SchemKit.Box(p, c.x - 60, c.y - 90, 120, 160, new Color(0.55f, 0.58f, 0.62f));  // bom
            p.FillRect(c.x - 25, c.y - 50, 50, 14, new Color(0.85f, 0.85f, 0.8f));  // chén
            if (Filled) p.FillRect(c.x - 15, c.y - 36, 30, 10, new Color(0.95f, 0.95f, 0.9f));
            bool burning = Running && RunMinutes > FireAt && RunMinutes < FireAt + 1.5;
            if (burning)
            {
                float fl = 18 + Mathf.Sin(time * 30) * 4;
                p.FillPoly(new[] { new Vector2(c.x - 14, c.y - 26), new Vector2(c.x + 14, c.y - 26), new Vector2(c.x, c.y - 26 + fl * 2) }, new Color(1f, 0.6f, 0.2f, 0.9f));
            }
            // cánh khuấy
            if (InWater)
            {
                float a = time * 12;
                p.Line(c.x + 120, c.y - 60, c.x + 120, c.y + 140, SchemKit.Edge, 3);
                p.Line(c.x + 120 - Mathf.Cos(a) * 20, c.y - 60, c.x + 120 + Mathf.Cos(a) * 20, c.y - 60, SchemKit.Edge, 4);
            }
            // nhiệt kế
            p.FillRect(c.x - 130, c.y - 80, 8, 220, new Color(0.9f, 0.9f, 0.95f));
            // trạm oxy
            var o2 = new Vector2(800, 170);
            SchemKit.Box(p, o2.x - 40, o2.y - 120, 80, 220, new Color(0.25f, 0.45f, 0.3f));
            p.Ring(o2.x, o2.y + 130, 26, SchemKit.Edge, 3);
            float ang = Mathf.Lerp(200, -20, (float)(O2Bar / 40)) * Mathf.Deg2Rad;
            p.Line(o2.x, o2.y + 130, o2.x + Mathf.Cos(ang) * 20, o2.y + 130 + Mathf.Sin(ang) * 20, O2Bar >= 25 ? SchemKit.Good : new Color(1f, 0.6f, 0.3f), 3);
            var d = Doc;
            double T = run != null && run.Curve.Count > 0 ? run.Curve[Math.Min(run.Curve.Count - 1, (int)(RunMinutes / Dt))].T : 24.6;
            S.SchemLabel("vo", d.PartName("vo"), c.x, c.y + 162, 11);
            S.SchemLabel("bom", d.PartName("bom"), c.x, c.y + 90, 11);
            S.SchemLabel("nhiet_ke", d.PartName("nhiet_ke") + $"\n<color=#61DBFF>{Fmt(T, "0.0000")} °C</color>", c.x - 230, c.y + 120, 11, null, TextAnchor.MiddleLeft);
            S.SchemLabel("oxy", d.PartName("oxy") + $"\n<color=#73F29E>{O2Bar:0} bar</color>", o2.x, o2.y - 140, 11);
            S.SchemLabel("trang_thai", Running ? (RunMinutes < FireAt ? "<color=#61DBFF>TIỀN KỲ</color>" : RunMinutes < MainEnd ? "<color=#FFC966>CHÍNH KỲ (ĐÃ ĐỐT)</color>" : "<color=#61DBFF>HẬU KỲ</color>") : "<color=#73F29E>SẴN SÀNG</color>", 620, 334, 14);
        }

        // =====================================================================
        // Đồ thị
        // =====================================================================

        public override void DrawPlot(Painter p)
        {
            InstrumentScreen.PlotFrame(p);
            if (run == null) { S.SetPlotAxes("Nhiệt độ nước theo thời gian", "Thời gian (phút)", "T (°C)", "0", Fmt(Minutes, "0"), "", ""); return; }
            int show = Math.Min(run.Curve.Count, (int)(RunMinutes / Dt) + 1);
            var vis = run.Curve.Take(show).ToList();
            double lo = run.Curve.Min(q => q.T) - 0.05, hi = run.Curve.Max(q => q.T) + 0.1;
            S.SetPlotAxes($"Nhiệt độ nước · {runName}", "Thời gian (phút)", "T (°C)", "0", Fmt(Minutes, "0"), Fmt(lo, "0.00"), Fmt(hi, "0.00"));
            var f = InstrumentScreen.Map(p, FireAt, lo, 0, Minutes, lo, hi);
            p.FillRect(f.x - 1, 0, 2, p.H, new Color(1f, 0.6f, 0.3f, 0.6f));
            var m = InstrumentScreen.Map(p, MainEnd, lo, 0, Minutes, lo, hi);
            p.FillRect(m.x - 1, 0, 2, p.H, new Color(1, 1, 1, 0.2f));
            p.Poly(vis.Select(q => InstrumentScreen.Map(p, q.t, q.T, 0, Minutes, lo, hi)).ToList(), new Color(1f, 0.55f, 0.4f), 2);
        }
    }
}
