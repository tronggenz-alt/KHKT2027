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
    /// Khung chung cho phương pháp khối lượng (tủ sấy: độ ẩm; lò nung: tro, nung vôi): cân chén, cân mẫu, gia nhiệt từng đợt, làm nguội, cân lại
    /// đến khối lượng không đổi. Lỗi mô phỏng: cân lúc nóng (thiếu khoảng 1,5 mg), làm nguội ngoài không khí (mẫu hút ẩm / CO₂), chỉ cân một lần,
    /// nhiệt độ sai (đường bị cháy khi sấy nóng quá; tro còn đen khi nung thiếu), cho mẫu chưa than hóa vào lò (bắn mẫu).
    /// </summary>
    public abstract class WeighingProgram : InstrumentProgram
    {
        protected abstract bool Ashing { get; }
        protected abstract double[] Temps { get; }
        protected abstract double DefaultTemp { get; }
        protected abstract double ConstantMg { get; }
        protected abstract string HeatVerb { get; }  // "Sấy" / "Nung"
        protected abstract List<(string name, Func<WeighingProgram, Specimen> make)> Sources { get; }

        public sealed class Specimen
        {
            public double WaterG, VolatileG;
            public List<SolidResidue.Phase> Dry = new List<SolidResidue.Phase>();
            public string Name;
        }

        public enum Cool { Desiccator, Air, Hot }

        public double SetTemp { get; private set; }
        public int SourceIdx { get; private set; }
        public double TakeMl { get; private set; } = 10;
        public Cool Cooling { get; private set; } = Cool.Desiccator;
        public double DishG { get; private set; } = double.NaN;
        public double FilledG { get; private set; } = double.NaN;
        public bool Charred { get; private set; }
        public double Minutes { get; private set; }
        public readonly List<(double min, double g)> Weighings = new List<(double, double)>();
        protected Specimen sample;
        double spatterFrac;
        float heatT = -1;
        protected readonly System.Random rng = new System.Random(41);

        public bool Heating => heatT >= 0;
        public bool Constant => Weighings.Count >= 2 && Math.Abs(Weighings[Weighings.Count - 1].g - Weighings[Weighings.Count - 2].g) * 1000 <= ConstantMg;

        Button bSrc, bTake, bTemp, bDish, bFill, bChar, bHeat, bCool, bWeigh, bCalc;
        Text status;

        public override void Build()
        {
            SetTemp = DefaultTemp;
            bSrc = S.AddButton("", 0, 0, () => { SourceIdx = (SourceIdx + 1) % Sources.Count; Reset(); }, false, 2, 12);
            bTake = S.AddButton("", 2, 0, () => { TakeMl = TakeMl == 10 ? 5 : TakeMl == 5 ? 20 : 10; Reset(); }, false, 1, 13);
            bTemp = S.AddButton("", 0, 1, NextTemp);
            bDish = S.AddButton("Cân chén rỗng (m₀)", 1, 1, WeighDish, false, 1, 13);
            bFill = S.AddButton("Cho mẫu, cân (m₁)", 2, 1, Fill, false, 1, 13);
            bChar = S.AddButton("Than hóa trên bếp", 0, 2, Char, false, 1, 13);
            bHeat = S.AddButton("", 1, 2, Heat, true, 2);
            bCool = S.AddButton("", 0, 3, NextCool, false, 2, 12);
            bWeigh = S.AddButton("Cân (m₂)", 2, 3, Weigh, true);
            bCalc = S.AddButton("Tính kết quả", 0, 4, Calc, false, 1, 13);
            status = S.AddText("", 5, 0, 3, 13);
            S.SetResults("Cân chén rỗng, cho mẫu vào cân, rồi " + HeatVerb.ToLowerInvariant() + " từng đợt, làm nguội trong bình hút ẩm và cân lại đến khối lượng không đổi.");
        }

        public override void Refresh()
        {
            var src = Sources[SourceIdx % Sources.Count];
            InstrumentScreen.SetButton(bSrc, "Mẫu: " + src.name, !Heating && double.IsNaN(FilledG));
            bool tube = src.name.StartsWith("Mẫu ở ổ");
            InstrumentScreen.SetButton(bTake, tube ? $"Lấy {TakeMl:0} mL" : "—", tube && double.IsNaN(FilledG));
            InstrumentScreen.SetButton(bTemp, $"Nhiệt độ {SetTemp:0} °C", !Heating);
            InstrumentScreen.SetButton(bDish, double.IsNaN(DishG) ? "Cân chén rỗng (m₀)" : $"m₀ = {Fmt(DishG, "0.0000")} g", double.IsNaN(DishG), !double.IsNaN(DishG));
            InstrumentScreen.SetButton(bFill, double.IsNaN(FilledG) ? "Cho mẫu, cân (m₁)" : $"m₁ = {Fmt(FilledG, "0.0000")} g", !double.IsNaN(DishG) && double.IsNaN(FilledG), !double.IsNaN(FilledG));
            InstrumentScreen.SetButton(bChar, Ashing ? (Charred ? "Đã than hóa" : "Than hóa trên bếp") : "—", Ashing && !double.IsNaN(FilledG) && !Charred && Minutes == 0, Charred);
            InstrumentScreen.SetButton(bHeat, Heating ? $"Đang {HeatVerb.ToLowerInvariant()}…" : $"{HeatVerb} 60 phút ở {SetTemp:0} °C", !double.IsNaN(FilledG) && !Heating);
            InstrumentScreen.SetButton(bCool, Cooling == Cool.Desiccator ? "Làm nguội: trong bình hút ẩm" : Cooling == Cool.Air ? "Làm nguội: ngoài không khí" : "Làm nguội: không (cân ngay khi nóng)", !Heating);
            InstrumentScreen.SetButton(bWeigh, null, !double.IsNaN(FilledG) && !Heating && Minutes > 0);
            InstrumentScreen.SetButton(bCalc, null, Weighings.Count > 0);
            status.text = $"{(Ashing ? "Lò" : "Tủ")}: {(Heating ? SetTemp.ToString("0") : "25")} °C · đã {HeatVerb.ToLowerInvariant()} {Minutes:0} phút · {Weighings.Count} lần cân" + (Constant ? " · <color=#73F29E>khối lượng không đổi</color>" : "");
            S.SetResults(BuildResults());
        }

        void Reset() { DishG = FilledG = double.NaN; Charred = false; Minutes = 0; Weighings.Clear(); sample = null; spatterFrac = 0; }

        public void NextTemp() { int i = Array.IndexOf(Temps, SetTemp); SetTemp = Temps[(i + 1) % Temps.Length]; }
        public void SetTemperature(double t) => SetTemp = t;
        public void SelectSource(string prefix) { int i = Sources.FindIndex(s => s.name.StartsWith(prefix)); if (i >= 0) { SourceIdx = i; Reset(); } }
        public void SetCooling(Cool c) => Cooling = c;
        void NextCool() => Cooling = (Cool)(((int)Cooling + 1) % 3);

        public void WeighDish()
        {
            if (!double.IsNaN(DishG)) return;
            if (!Ashing) S.Done("bat_may");  // tủ sấy đã bật sẵn, ổn định ở nhiệt độ đặt
            if (!S.Require(Ashing ? "nung_chen" : "can_chen")) return;
            DishG = Math.Round(25 + rng.NextDouble() * 2, 4);
            S.Done(Ashing ? "nung_chen" : "can_chen");
            S.Say($"Chén rỗng (đã {HeatVerb.ToLowerInvariant()}, nguội trong bình hút ẩm): m₀ = {Fmt(DishG, "0.0000")} g", 0);
        }

        public void Fill()
        {
            if (double.IsNaN(DishG) || !double.IsNaN(FilledG)) return;
            if (!S.Require("can_mau")) return;
            sample = Sources[SourceIdx % Sources.Count].make(this);
            if (sample == null) { S.Say("Ổ mẫu trống", 2); return; }
            double g = MassG(sample);
            if (g < 0.01) { S.Say("Mẫu quá ít", 2); return; }
            FilledG = Math.Round(DishG + g, 4);
            S.Done("can_mau");
            S.Say($"Cho {sample.Name} vào chén: m₁ = {Fmt(FilledG, "0.0000")} g (mẫu {Fmt(g, "0.0000")} g)", 0);
        }

        public void Char()
        {
            if (!Ashing || double.IsNaN(FilledG) || Minutes > 0) return;
            Charred = true;
            S.Done("than_hoa");
            S.Say("Đốt nhẹ chén mẫu trên bếp điện trong tủ hút đến khi hết khói (than hóa).", 0);
        }

        public void Heat()
        {
            if (double.IsNaN(FilledG) || Heating) return;
            if (!S.Require(Ashing ? "nung" : "say")) return;
            if (Ashing && !Charred && Minutes == 0 && OrganicFrac() > 0.3)
            {
                spatterFrac = 0.03 + rng.NextDouble() * 0.04;
                S.Say($"Cho mẫu chưa than hóa vào lò nóng: mẫu cháy bùng, bắn ra ngoài khoảng {spatterFrac:P0} khối lượng!", 3);
            }
            heatT = 0;
        }

        public override void Tick(float dt)
        {
            if (!Heating) return;
            heatT += dt;
            if (heatT >= 1.2f)
            {
                heatT = -1;
                Minutes += 60;
                S.Done(Ashing ? "nung" : "say");
                S.Say($"{HeatVerb} xong thêm 60 phút (tổng {Minutes:0} phút). Làm nguội rồi cân.", 1);
            }
        }

        double OrganicFrac()
        {
            double tot = SolidResidue.TotalMg(sample.Dry), org = sample.Dry.Where(p => p.Organic || p.Id.StartsWith("org:")).Sum(p => p.Mg);
            return tot > 0 ? org / tot : 0;
        }

        protected static double MassG(Specimen s) => s.WaterG + s.VolatileG + SolidResidue.TotalMg(s.Dry) / 1000;

        /// <summary>Khối lượng mẫu (g) sau khi gia nhiệt hiện tại; residue: chất rắn còn lại; carbonMg: than chưa cháy.</summary>
        protected double CurrentMass(out List<SolidResidue.Phase> residue, out double carbonMg)
        {
            carbonMg = 0;
            double water = Gravimetry.WaterLeft(sample.WaterG, SetTemp, Minutes, 25 * Math.Max(0.4, TakeMl / 10));
            double vol = Gravimetry.WaterLeft(sample.VolatileG, SetTemp, Minutes, 8);
            residue = sample.Dry;
            if (Ashing && Minutes > 0) { residue = Gravimetry.Ash(sample.Dry, SetTemp, Minutes, out carbonMg); water = vol = 0; }
            double dry = SolidResidue.TotalMg(residue) / 1000;
            // sấy quá nóng: đường bị caramen hóa / phân hủy, mất thêm khối lượng
            if (!Ashing && SetTemp > 120) dry -= OrganicFrac() * dry * 0.03 * (Minutes / 60) * (SetTemp - 105) / 45;
            double m = water + vol + dry + carbonMg / 1000;
            return m * (1 - spatterFrac);
        }

        public void Weigh()
        {
            if (double.IsNaN(FilledG) || Heating || Minutes <= 0) return;
            if (!S.Require(Ashing ? "lam_nguoi_can" : "lam_nguoi_can")) return;
            double m = CurrentMass(out var residue, out _);
            double err = 0;
            if (Cooling == Cool.Hot) err = -0.0015 - rng.NextDouble() * 0.0008;               // dòng khí nóng bốc lên đỡ đĩa cân
            else if (Cooling == Cool.Air)
            {
                double hygro = residue.Sum(p => p.Mg * (p.Id.StartsWith("org:") ? 0.006 : p.Id == "CaO(r)" ? 0.02 : p.Id.Contains("NaCl") ? 0.0008 : 0.001)) / 1000;
                err = hygro;                                                                    // hút ẩm / CO₂ khi nguội ngoài không khí
            }
            double reading = Math.Round(DishG + m + err + (rng.NextDouble() - 0.5) * 0.0002, 4);
            Weighings.Add((Minutes, reading));
            S.Done("lam_nguoi_can");
            string msg = $"Cân: m₂ = {Fmt(reading, "0.0000")} g";
            if (Weighings.Count >= 2)
            {
                double d = (Weighings[Weighings.Count - 1].g - Weighings[Weighings.Count - 2].g) * 1000;
                msg += $" (chênh lần trước {Fmt(d, "+0.0;−0.0")} mg)";
                if (Constant) { S.Done("khoi_luong_khong_doi"); msg += " · đã đạt khối lượng không đổi"; }
            }
            S.Say(msg, 1);
            if (Cooling == Cool.Hot) S.Say("Cân khi chén còn nóng: dòng khí nóng bốc lên làm số cân nhỏ hơn thật.", 2);
            if (Cooling == Cool.Air) S.Say("Làm nguội ngoài không khí: mẫu khô hút ẩm (CaO hút cả CO₂), số cân lớn hơn thật.", 2);
        }

        public void Calc()
        {
            if (Weighings.Count == 0) return;
            S.Done("ket_qua");
            if (!Constant) S.Say("Chưa đạt khối lượng không đổi (mới cân một lần hoặc còn giảm): kết quả có thể chưa đúng.", 2);
        }

        protected abstract string Interpret(double m0, double m1, double m2);

        string BuildResults()
        {
            var sb = new StringBuilder();
            sb.Append($"<color=#9FB3C8>Mẫu:</color> {Sources[SourceIdx % Sources.Count].name}{(sample != null ? $" · {sample.Name}" : "")}\n");
            if (!double.IsNaN(DishG)) sb.Append($"m₀ (chén) = {Fmt(DishG, "0.0000")} g\n");
            if (!double.IsNaN(FilledG)) sb.Append($"m₁ (chén + mẫu) = {Fmt(FilledG, "0.0000")} g · mẫu {Fmt(FilledG - DishG, "0.0000")} g\n");
            if (Weighings.Count > 0)
            {
                sb.Append("\n<b>Các lần cân</b>\n");
                foreach (var (min, g) in Weighings) sb.Append($"sau {min:0} phút: {Fmt(g, "0.0000")} g\n");
                var last = Weighings[Weighings.Count - 1].g;
                sb.Append("\n<b>Phân tích chuyên sâu</b>\n").Append(Interpret(DishG, FilledG, last));
            }
            return sb.ToString();
        }

        // =====================================================================
        // Sơ đồ, đồ thị
        // =====================================================================

        public override void DrawSchematic(Painter p, float time)
        {
            SchemKit.Begin(p);
            float glow = Heating ? Mathf.InverseLerp(25, 1000, (float)SetTemp) : 0;
            var c = new Vector2(250, 175);
            SchemKit.Box(p, c.x - 170, c.y - 140, 340, 280, new Color(0.18f, 0.2f, 0.24f));
            p.FillRect(c.x - 140, c.y - 110, 280, 220, Color.Lerp(new Color(0.08f, 0.1f, 0.12f), Ashing ? new Color(0.9f, 0.35f, 0.1f) : new Color(0.45f, 0.3f, 0.2f), glow * 1.2f));
            for (int i = 0; i < 6; i++) p.Line(c.x - 140 + i * 56, c.y - 110, c.x - 140 + i * 56, c.y - 100, Heating ? new Color(1f, 0.5f, 0.2f) : SchemKit.Off, 3);
            if (!double.IsNaN(FilledG))
            {
                p.FillPoly(new[] { new Vector2(c.x - 40, c.y - 40), new Vector2(c.x + 40, c.y - 40), new Vector2(c.x + 28, c.y - 70), new Vector2(c.x - 28, c.y - 70) }, new Color(0.9f, 0.9f, 0.88f));
                double carb = 0;
                if (sample != null) CurrentMass(out _, out carb);
                p.FillRect(c.x - 30, c.y - 46, 60, 6, carb > 0.5 ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.95f, 0.95f, 0.9f));
                if (Heating && !Ashing) for (int i = 0; i < 3; i++) p.Disc(c.x - 20 + i * 20, c.y - 20 + Mathf.Repeat(time * 40 + i * 30, 90), 4, new Color(0.8f, 0.9f, 1f, 0.5f));
            }
            // bình hút ẩm và cân
            var d = new Vector2(620, 120);
            p.FillPoly(new[] { new Vector2(d.x - 80, d.y - 60), new Vector2(d.x + 80, d.y - 60), new Vector2(d.x + 60, d.y + 60), new Vector2(d.x - 60, d.y + 60) }, new Color(0.6f, 0.75f, 0.9f, 0.25f));
            for (int i = 0; i < 12; i++) p.Disc(d.x - 50 + i * 9, d.y - 45, 3, new Color(0.3f, 0.5f, 0.95f));
            var b = new Vector2(850, 110);
            SchemKit.Box(p, b.x - 80, b.y - 50, 160, 100, new Color(0.85f, 0.87f, 0.9f));
            p.FillRect(b.x - 60, b.y + 10, 120, 30, new Color(0.1f, 0.25f, 0.15f));
            var doc = Doc;
            S.SchemLabel("lo", doc.PartName("buong") + $"\n<color=#FFB27A>{(Heating ? SetTemp.ToString("0") : "25")} °C</color>", c.x, c.y + 125, 12);
            S.SchemLabel("binh", doc.PartName("binh_hut_am"), d.x, d.y + 80, 11);
            S.SchemLabel("can", doc.PartName("can") + (Weighings.Count > 0 ? $"\n<color=#73F29E>{Fmt(Weighings[Weighings.Count - 1].g, "0.0000")} g</color>" : ""), b.x, b.y - 75, 11);
            S.SchemLabel("trang_thai", Heating ? $"<color=#FFC966>ĐANG {HeatVerb.ToUpperInvariant()} {SetTemp:0} °C</color>" : "<color=#73F29E>SẴN SÀNG</color>", 700, 320, 14);
        }

        public override void DrawPlot(Painter p)
        {
            InstrumentScreen.PlotFrame(p);
            if (sample == null || double.IsNaN(FilledG)) { S.SetPlotAxes("Khối lượng chén + mẫu theo thời gian", "Thời gian (phút)", "Khối lượng (g)", "0", "300", "", ""); return; }
            double tMax = Math.Max(240, Minutes + 60);
            double lo = DishG, hi = FilledG + 0.002;
            if (Weighings.Count > 0) lo = Math.Min(lo, Weighings.Min(w => w.g) - 0.002);
            double hold = SetTemp, minsSave = Minutes;
            S.SetPlotAxes($"Khối lượng chén + mẫu khi {HeatVerb.ToLowerInvariant()} ở {SetTemp:0} °C", "Thời gian (phút)", "Khối lượng (g)", "0", Fmt(tMax, "0"), Fmt(lo, "0.000"), Fmt(hi, "0.000"));
            var pts = new List<Vector2>();
            for (double t = 0; t <= minsSave; t += 5)
            {
                Minutes = t;
                pts.Add(InstrumentScreen.Map(p, t, DishG + CurrentMass(out _, out _), 0, tMax, lo, hi));
            }
            Minutes = minsSave;
            if (pts.Count > 1) p.Poly(pts, new Color(1f, 0.7f, 0.4f, 0.7f), 2);
            foreach (var (min, g) in Weighings) { var q = InstrumentScreen.Map(p, min, g, 0, tMax, lo, hi); p.Disc(q.x, q.y, 6, SchemKit.Good); }
            var z = InstrumentScreen.Map(p, 0, DishG, 0, tMax, lo, hi);
            p.FillRect(0, z.y, p.W, 1, new Color(1, 1, 1, 0.3f));
        }
    }

    /// <summary>Tủ sấy: độ ẩm, chất khô (sấy 105 °C đến khối lượng không đổi, theo các TCVN xác định độ ẩm).</summary>
    public class DryingOvenProgram : WeighingProgram
    {
        public override string Code => "tu_say";
        static readonly string[] optional = { "khoi_luong_khong_doi" };
        public override ICollection<string> Optional => optional;
        protected override bool Ashing => false;
        protected override double[] Temps => new[] { 105.0, 60, 150 };
        protected override double DefaultTemp => 105;
        protected override double ConstantMg => 1.0;
        protected override string HeatVerb => "Sấy";
        protected override List<(string name, Func<WeighingProgram, Specimen> make)> Sources => sources;

        static readonly List<(string, Func<WeighingProgram, Specimen>)> sources = new List<(string, Func<WeighingProgram, Specimen>)>
        {
            ("Mẫu ở ổ (dung dịch)", w =>
            {
                if (w.Sample == null) return null;
                var g = Gravimetry.FromSolution(w.Sample.Total.Mix, w.Sample.VolumeMl, w.TakeMl);
                return new Specimen { WaterG = g.WaterG, VolatileG = g.VolatileG, Dry = g.Solids, Name = $"{w.TakeMl:0} mL {w.SampleName}" };
            }),
            ("Đường kính ẩm (2 g, 1% nước)", w => new Specimen { WaterG = 0.02, Dry = new List<SolidResidue.Phase> { SolidSource.Pure("org:hc:580", "C12H22O11", "saccarozơ", 1980, true, true) }, Name = "2 g đường kính ẩm" }),
            ("Muối ăn ẩm (5 g, 3% nước)", w => new Specimen { WaterG = 0.15, Dry = new List<SolidResidue.Phase> { SolidSource.Pure("NaCl(r)", "NaCl", "natri clorua", 4850) }, Name = "5 g muối ăn ẩm" }),
        };

        protected override string Interpret(double m0, double m1, double m2)
        {
            double sampleG = m1 - m0, lost = m1 - m2;
            double moist = lost / sampleG * 100;
            double trueMoist = (sample.WaterG + sample.VolatileG) / MassG(sample) * 100;
            var sb = new StringBuilder();
            sb.Append($"Độ ẩm = (m₁ − m₂) / (m₁ − m₀) × 100 = {Fmt(lost, "0.0000")} / {Fmt(sampleG, "0.0000")} × 100 = <b>{Fmt(moist, "0.00")}%</b>\n");
            sb.Append($"Chất khô = <b>{Fmt(100 - moist, "0.00")}%</b> ({Fmt(m2 - m0, "0.0000")} g)\n");
            sb.Append($"<color=#9FB3C8>Giá trị thật của mẫu: độ ẩm {Fmt(trueMoist, "0.00")}% (gồm cả chất dễ bay hơi như etanol)</color>\n");
            if (SetTemp > 120 && sample.Dry.Any(p => p.Organic || p.Id.StartsWith("org:"))) sb.Append("<color=#FFC966>Sấy quá nóng: đường bị caramen hóa, mất thêm khối lượng, độ ẩm tính ra cao hơn thật.</color>\n");
            if (SetTemp < 100) sb.Append("<color=#FFC966>Sấy dưới 100 °C: nước bay hơi rất chậm, chưa khô hết.</color>\n");
            if (sample.VolatileG > 0.01) sb.Append("<color=#9FB3C8>Mẫu có etanol / chất dễ bay hơi: \"độ ẩm\" theo phương pháp sấy gồm cả phần này.</color>\n");
            return sb.ToString();
        }
    }

    /// <summary>Lò nung: tro tổng số ở 550 °C (TCVN 7038), nung vôi ở 900 °C.</summary>
    public class FurnaceProgram : WeighingProgram
    {
        public override string Code => "lo_nung";
        static readonly string[] optional = { "than_hoa", "khoi_luong_khong_doi" };
        public override ICollection<string> Optional => optional;
        protected override bool Ashing => true;
        protected override double[] Temps => new[] { 550.0, 900, 400 };
        protected override double DefaultTemp => 550;
        protected override double ConstantMg => 0.5;
        protected override string HeatVerb => "Nung";
        protected override List<(string name, Func<WeighingProgram, Specimen> make)> Sources => sources;

        static readonly List<(string, Func<WeighingProgram, Specimen>)> sources = new List<(string, Func<WeighingProgram, Specimen>)>
        {
            ("Mẫu ở ổ: chất khô sau khi sấy", w =>
            {
                if (w.Sample == null) return null;
                var dry = SolidResidue.From(w.Sample.Total.Mix, w.Sample.VolumeMl, w.TakeMl, true);
                return new Specimen { Dry = dry, Name = $"chất khô của {w.TakeMl:0} mL {w.SampleName}" };
            }),
            ("Đá vôi nghiền (1 g CaCO₃)", w => new Specimen { Dry = new List<SolidResidue.Phase> { SolidSource.Pure("CaCO3", "CaCO3", "canxi cacbonat", 1000) }, Name = "1 g đá vôi" }),
            ("Đường kính (2 g)", w => new Specimen { Dry = new List<SolidResidue.Phase> { SolidSource.Pure("org:hc:580", "C12H22O11", "saccarozơ", 2000, true, true) }, Name = "2 g đường kính" }),
            ("Muối ăn (2 g NaCl)", w => new Specimen { Dry = new List<SolidResidue.Phase> { SolidSource.Pure("NaCl(r)", "NaCl", "natri clorua", 2000) }, Name = "2 g muối ăn" }),
        };

        protected override string Interpret(double m0, double m1, double m2)
        {
            double sampleG = m1 - m0, ashG = m2 - m0;
            CurrentMass(out var residue, out double carbon);
            var sb = new StringBuilder();
            sb.Append($"Tro (%) = (m₂ − m₀) / (m₁ − m₀) × 100 = {Fmt(ashG, "0.0000")} / {Fmt(sampleG, "0.0000")} × 100 = <b>{Fmt(ashG / sampleG * 100, "0.00")}%</b>\n");
            sb.Append($"<color=#9FB3C8>Thành phần tro (mô phỏng): {(residue.Count == 0 ? "không còn tro" : string.Join(", ", residue.Select(r => $"{r.Name} {Fmt(r.Mg, "0.0")} mg")))}</color>\n");
            if (carbon > 0.5) sb.Append($"<color=#FFC966>Tro còn đen ({Fmt(carbon, "0.0")} mg cacbon chưa cháy): nung thêm, hoặc nhỏ vài giọt H₂O₂ / HNO₃ rồi nung lại.</color>\n");
            if (sample.Dry.Any(p => p.Id == "CaCO3"))
            {
                double caco3 = sample.Dry.Where(p => p.Id == "CaCO3").Sum(p => p.Mg) / 1000;
                double lostCo2 = caco3 * 1000 - residue.Where(r => r.Id == "CaCO3" || r.Id == "CaO(r)").Sum(r => r.Mg);
                sb.Append($"CaCO₃ → CaO + CO₂: đã mất {Fmt(lostCo2, "0.0")} mg CO₂ ({Fmt(lostCo2 / (caco3 * 1000) * 100, "0.0")}%; phân hủy hết là 43,97%)\n");
                if (SetTemp < 700) sb.Append("<color=#9FB3C8>Ở 550 °C CaCO₃ chưa phân hủy: tro của mẫu chứa đá vôi vẫn là CaCO₃.</color>\n");
            }
            return sb.ToString();
        }
    }
}
