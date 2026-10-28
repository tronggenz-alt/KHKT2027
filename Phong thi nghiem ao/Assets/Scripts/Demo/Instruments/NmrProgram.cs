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
    /// ¹H-NMR để bàn 60 MHz (thông số theo Magritek Spinsolve 60). Mẫu: 0,45 mL mẫu ở ổ + 0,05 mL D₂O chứa DSS (chuẩn nội 0,00 ppm).
    /// FID tính từ các vạch (độ dịch chuyển Fulmer 2010, quy tắc n + 1), máy biến đổi Fourier thật (DFT) ra phổ.
    /// Lỗi mô phỏng: chưa shim (vạch rộng 6 Hz, không thấy tách vạch), không triệt nước (nước che chất tan), chưa chỉnh pha (vạch méo),
    /// chưa chuẩn ppm (lệch 0,12 ppm). Phân tích chuyên sâu: tích phân → số H, gán tín hiệu, tỉ lệ mol, qNMR với DSS.
    /// </summary>
    public class NmrProgram : InstrumentProgram
    {
        public override string Code => "nmr";
        static readonly string[] optional = { "ket_thuc" };
        public override ICollection<string> Optional => optional;

        const int N = 2048;
        const double Sw = 800, Center = 5, DssMolL = 0.01, SampleFrac = 0.9;
        const float ShimS = 3f, AcqS = 2f;

        public bool TubeReady { get; private set; }
        public bool Inserted { get; private set; }
        public bool Shimmed { get; private set; }
        float shimT = -1;
        public int Scans { get; private set; } = 8;
        public bool Presat { get; private set; } = true;
        float acqT = -1;
        double[] fidRe, fidIm;
        double phaseErr = 63, refErr = 0.12;
        public bool Phased { get; private set; }
        public bool Referenced { get; private set; }
        public List<(double ppm, double y)> Spectrum { get; private set; }
        List<NmrLibrary.Line> lines = new List<NmrLibrary.Line>();
        readonly List<string> unknown = new List<string>();
        public List<Signal> Signals { get; private set; }
        int plotMode = 1;  // 0 FID, 1 phổ
        readonly System.Random rng = new System.Random(29);

        public struct Signal
        {
            public double Ppm, Area, H, J, PerMol;  // H: so với DSS (9 H); PerMol: số H trong một phân tử (đo được)
            public int Lines, Index;
            public string Owner, Assign;
        }

        NmrLibrary Lib => InstrumentData.Instance != null ? InstrumentData.Instance.Nmr : new NmrLibrary();
        public double WidthHz => Shimmed ? 1.0 : 6.0;
        public bool Acquiring => acqT >= 0;

        Button bTube, bInsert, bShim, bScans, bPresat, bAcq, bPhase, bRef, bInt, bPlot, bOut;
        Text status;

        public override void Build()
        {
            bTube = S.AddButton("Chuẩn bị ống mẫu", 0, 0, PrepareTube, false, 2, 13);
            bInsert = S.AddButton("Đặt ống vào máy", 2, 0, Insert, false, 1, 13);
            bShim = S.AddButton("Shim tự động", 0, 1, Shim);
            bScans = S.AddButton("", 1, 1, () => Scans = Scans == 1 ? 8 : Scans == 8 ? 32 : 1);
            bPresat = S.AddButton("", 2, 1, () => Presat = !Presat, false, 1, 12);
            bAcq = S.AddButton("Đo (xung, thu FID)", 0, 2, Acquire, true, 2);
            bPlot = S.AddButton("", 2, 2, () => plotMode = 1 - plotMode);
            bPhase = S.AddButton("Chỉnh pha tự động", 0, 3, AutoPhase, false, 1, 13);
            bRef = S.AddButton("Chuẩn ppm (DSS = 0)", 1, 3, Reference, false, 1, 13);
            bInt = S.AddButton("Tích phân, gán tín hiệu", 2, 3, Integrate, false, 1, 12);
            bOut = S.AddButton("Lấy ống ra", 0, 4, TakeOut);
            status = S.AddText("", 5, 0, 3, 13);
            S.SetResults("Chuẩn bị ống mẫu từ ống nghiệm ở ổ máy (0,45 mL mẫu + 0,05 mL D₂O có chuẩn nội DSS), đặt vào máy, shim rồi đo.");
        }

        public override void Refresh()
        {
            InstrumentScreen.SetButton(bTube, TubeReady ? $"Ống: {SampleName} + DSS" : "Chuẩn bị ống mẫu", !Inserted && Sample != null, TubeReady);
            InstrumentScreen.SetButton(bInsert, Inserted ? "Ống đã ở trong máy" : "Đặt ống vào máy", TubeReady && !Inserted, Inserted);
            InstrumentScreen.SetButton(bShim, shimT >= 0 ? "Đang shim…" : Shimmed ? "Đã shim (1 Hz)" : "Shim tự động", Inserted && shimT < 0, Shimmed);
            InstrumentScreen.SetButton(bScans, $"Số lần quét: {Scans}", !Acquiring);
            InstrumentScreen.SetButton(bPresat, Presat ? "Triệt tín hiệu nước: bật" : "Triệt tín hiệu nước: tắt", !Acquiring, Presat);
            InstrumentScreen.SetButton(bAcq, Acquiring ? $"Đang đo {Mathf.Min(Scans, 1 + (int)(acqT / AcqS * Scans))}/{Scans}" : "Đo (xung, thu FID)", Inserted && !Acquiring && shimT < 0);
            InstrumentScreen.SetButton(bPlot, plotMode == 0 ? "Hiện: FID" : "Hiện: phổ", true);
            InstrumentScreen.SetButton(bPhase, Phased ? "Pha đã chỉnh" : "Chỉnh pha tự động", Spectrum != null, Phased);
            InstrumentScreen.SetButton(bRef, Referenced ? "Đã chuẩn ppm" : "Chuẩn ppm (DSS = 0)", Spectrum != null, Referenced);
            InstrumentScreen.SetButton(bInt, null, Spectrum != null);
            InstrumentScreen.SetButton(bOut, null, Inserted && !Acquiring);
            status.text = $"Nam châm 1,4 T · ¹H 60 MHz · độ rộng vạch {Fmt(WidthHz, "0.0")} Hz · 1 ppm = 60 Hz";
            S.SetResults(BuildResults());
        }

        // =====================================================================
        // Thao tác
        // =====================================================================

        public void PrepareTube()
        {
            if (Sample == null || Inserted) return;
            TubeReady = true;
            S.Done("chuan_bi_ong");
            S.Say($"Ống 5 mm: 0,45 mL {SampleName} + 0,05 mL D₂O chứa DSS (nồng độ trong ống {Fmt(DssMolL * 1000, "0")} mM).", 0);
        }

        public void Insert()
        {
            if (!TubeReady || Inserted) return;
            if (!S.Require("dat_ong")) return;
            Inserted = true;
            Shimmed = false;
            S.Done("dat_ong");
        }

        public void Shim()
        {
            if (!Inserted || shimT >= 0) return;
            if (!S.Require("shim")) return;
            shimT = 0;
            S.Say("Shim tự động: chỉnh dòng các cuộn shim để từ trường đều nhất (vạch hẹp nhất)…", 0);
        }

        public void SetScans(int n) => Scans = n;
        public void SetPresat(bool on) => Presat = on;

        public void Acquire()
        {
            if (!Inserted || Acquiring) return;
            if (!S.Require("do")) return;
            unknown.Clear();
            lines = Lib.Lines(Sample.Total.Mix, Sample.VolumeMl, DssMolL, SampleFrac, Presat ? 0.002 : 1, unknown);
            // tín hiệu lệch tham chiếu (chưa chuẩn ppm)
            var shifted = lines.Select(l => new NmrLibrary.Line { Ppm = l.Ppm + refErr, Amp = l.Amp, Owner = l.Owner, Signal = l.Signal }).ToList();
            double noise = 0.02 * Math.Sqrt(8.0 / Scans);
            (fidRe, fidIm) = NmrLibrary.Fid(shifted, N, Sw, Center, WidthHz, noise, rng);
            acqT = 0;
            Spectrum = null;
            Signals = null;
            Phased = Referenced = false;
            phaseErr = 40 + rng.NextDouble() * 60;
            plotMode = 0;
            S.Say($"Xung 90°, thu FID {N} điểm, {Scans} lần quét…", 0);
        }

        public void AutoPhase()
        {
            if (Spectrum == null) return;
            Phased = true;
            Recompute();
            S.Say("Chỉnh pha bậc 0: mọi vạch dựng thẳng, nền phẳng.", 1);
        }

        public void Reference()
        {
            if (Spectrum == null) return;
            Referenced = true;
            Recompute();
            S.Done("xu_ly");
            S.Say("Đặt tín hiệu DSS = 0,00 ppm.", 1);
        }

        void Recompute()
        {
            var sp = NmrLibrary.Spectrum(fidRe, fidIm, Sw, Center, Phased ? 0 : phaseErr);
            Spectrum = Referenced ? sp.Select(p => (p.ppm - refErr, p.y)).ToList() : sp;
        }

        public void Integrate()
        {
            if (Spectrum == null) return;
            if (!S.Require("tich_phan")) return;
            if (!Phased) S.Say("Chưa chỉnh pha: vạch méo, tích phân sai.", 2);
            var visible = lines.Select(l => new NmrLibrary.Line { Ppm = l.Ppm + (Referenced ? 0 : refErr), Amp = l.Amp, Owner = l.Owner, Signal = l.Signal }).ToList();
            var fit = NmrLibrary.FitAreas(Spectrum, visible, WidthHz);
            // chuẩn hóa: DSS = 9 H
            double dss = fit.TryGetValue("dss#0", out var v) ? v : 0;
            double perH = dss > 0 ? dss / 9 : 1;
            Signals = new List<Signal>();
            foreach (var g in visible.GroupBy(l => l.Owner + "#" + l.Signal))
            {
                var ls = g.ToList();
                double area = fit.TryGetValue(g.Key, out var a) ? a : 0;
                if (!Phased) area *= 0.6 + 0.8 * rng.NextDouble();
                var cp = Lib.ById(ls[0].Owner);
                var sig = cp?.Signals[ls[0].Signal];
                Signals.Add(new Signal
                {
                    Ppm = ls.Average(l => l.Ppm), Area = area, H = area / perH, Lines = ls.Count, Index = ls[0].Signal, J = sig?.j ?? 0, Owner = ls[0].Owner,
                    Assign = ls[0].Owner == "nuoc" ? "nước (HDO) + H của OH, COOH trao đổi" : ls[0].Owner == "dss" ? "DSS (chuẩn nội, 9 H)" : $"{cp?.Name}: {GroupName(cp, ls[0].Signal)}",
                });
            }
            // số H trong một phân tử: lấy tín hiệu nhiều H nhất của mỗi chất làm mốc
            for (int i = 0; i < Signals.Count; i++)
            {
                var sg = Signals[i];
                var cp = Lib.ById(sg.Owner);
                if (cp == null) { sg.PerMol = sg.Owner == "dss" ? 9 : double.NaN; Signals[i] = sg; continue; }
                var best = Signals.Where(x => x.Owner == sg.Owner).OrderByDescending(x => cp.Signals[x.Index].nH).First();
                double k = best.H / cp.Signals[best.Index].nH;
                sg.PerMol = k > 0 ? sg.H / k : double.NaN;
                Signals[i] = sg;
            }
            Signals = Signals.OrderByDescending(s => s.Ppm).ToList();
            S.Done("tich_phan");
            S.Say($"Tích phân {Signals.Count} tín hiệu (khớp đường vạch, chuẩn nội DSS = 9 H).", 1);
        }

        static string GroupName(NmrLibrary.Compound cp, int i)
        {
            if (cp == null) return "";
            var (ppm, nH, nb, _) = cp.Signals[i];
            string kind = ppm < 1.6 ? "CH₃ / CH₂ ankyl" : ppm < 2.6 ? "CH₃ cạnh C=O" : ppm < 3.0 ? "CH₂ cạnh COOH" : ppm < 4.4 ? "CH₂ / CH₃ cạnh O" : "CH anome (cạnh 2 O)";
            return $"{kind} ({nH:0.##} H)";
        }

        public void TakeOut()
        {
            if (!Inserted || Acquiring) return;
            Inserted = false;
            TubeReady = false;
            if (S.DoneSteps.Contains("tich_phan")) S.Done("ket_thuc");
            S.Say("Lấy ống mẫu ra, rửa, sấy.", 0);
        }

        public override void Tick(float dt)
        {
            if (shimT >= 0)
            {
                shimT += dt;
                if (shimT >= ShimS) { shimT = -1; Shimmed = true; S.Done("shim"); S.Say("Shim xong: độ rộng vạch 1 Hz.", 1); }
            }
            if (acqT >= 0)
            {
                acqT += dt;
                if (acqT >= AcqS)
                {
                    acqT = -1;
                    Recompute();
                    plotMode = 1;
                    S.Done("do");
                    S.Say("Đo xong. Biến đổi Fourier FID → phổ. Chỉnh pha, chuẩn ppm rồi tích phân.", 1);
                    if (!Presat) S.Say("Không triệt nước: tín hiệu nước ở 4,79 ppm lớn gấp hàng chục lần, che các tín hiệu chất tan.", 2);
                    if (!Shimmed) S.Say("Chưa shim: vạch rộng 6 Hz, các vạch của vạch ba / vạch bốn (cách 7 Hz) nhập vào nhau.", 2);
                }
            }
        }

        // =====================================================================
        // Kết quả
        // =====================================================================

        static string Mult(int n) => n switch { 1 => "đơn (s)", 2 => "đôi (d)", 3 => "ba (t)", 4 => "bốn (q)", 5 => "năm", _ => "đa vạch (m)" };

        string BuildResults()
        {
            var sb = new StringBuilder();
            sb.Append($"<color=#9FB3C8>Ống mẫu:</color> {(TubeReady ? $"0,45 mL {SampleName} + D₂O/DSS" : "chưa chuẩn bị")} · {(Shimmed ? "đã shim" : "chưa shim")} · {(Presat ? "triệt nước" : "không triệt nước")}\n");
            if (Signals != null)
            {
                sb.Append("\n<b>Tín hiệu</b> (δ ppm · kiểu tách · J · số H đo được từ tích phân)\n");
                foreach (var s in Signals.Where(s => s.Owner != "nuoc" || !Presat).Take(9))
                    sb.Append($"{Fmt(s.Ppm, "0.00")} · {Mult(s.Lines)}{(s.J > 0 && s.Lines > 1 ? $" J {Fmt(s.J, "0")} Hz" : "")} · {(double.IsNaN(s.PerMol) ? "—" : Fmt(s.PerMol, "0.00") + " H")} · <color=#61DBFF>{s.Assign}</color>\n");
                // định lượng
                var byOwner = Signals.Where(s => s.Owner != "nuoc" && s.Owner != "dss").GroupBy(s => s.Owner).ToList();
                if (byOwner.Count > 0)
                {
                    sb.Append("\n<b>Phân tích chuyên sâu: qNMR (chuẩn nội DSS)</b>\n<color=#9FB3C8>c = (I / số H) / (I_DSS / 9) × c_DSS / 0,9</color>\n");
                    var conc = new Dictionary<string, double>();
                    foreach (var g in byOwner)
                    {
                        var cp = Lib.ById(g.Key);
                        // dùng tín hiệu có nhiều H nhất của chất (tỉ lệ tín hiệu / nhiễu tốt nhất)
                        var best = g.OrderByDescending(s => cp?.Signals[s.Index].nH ?? 1).First();
                        double nH = cp?.Signals[best.Index].nH ?? 1;
                        double c = best.H / nH * DssMolL / SampleFrac;
                        conc[g.Key] = c;
                        sb.Append($"• <color=#61DBFF>{cp?.Name}</color>: {Fmt(c, "0.000")} mol/L ({Fmt(c * MolarMass(g.Key) , c * MolarMass(g.Key) > 100 ? "0" : "0.0")} g/L)\n");
                    }
                    if (conc.TryGetValue("metanol", out var me) && conc.TryGetValue("etanol", out var et) && et > 0)
                    {
                        double deg = et * 46.07 / 789 * 100;
                        double per = me * 32040 / (deg / 100);
                        sb.Append($"Metanol / etanol = {Fmt(me / et * 100, "0.00")}% mol · độ cồn ≈ {Fmt(deg, "0.0")}% · metanol {Fmt(me * 32040, "0")} mg/L = <b>{Fmt(per, "0")} mg/L cồn 100°</b>");
                        sb.Append(per > 100 ? " <color=#FF7A7A>(vượt giới hạn 100 mg/L)</color>\n" : "\n");
                    }
                }
            }
            if (unknown.Count > 0) sb.Append($"\n<color=#FFC966>Chất ngoài thư viện NMR (không bịa tín hiệu):</color> {string.Join(", ", unknown.Distinct())}\n");
            if (Lib.Compounds.Any(c => c.Estimated) && Signals != null && Signals.Any(s => Lib.ById(s.Owner)?.Estimated == true))
                sb.Append("<color=#9FB3C8>Một số độ dịch chuyển là giá trị điển hình làm tròn (isoamyl axetat, đường, axit xitric).</color>\n");
            return sb.ToString();
        }

        static double MolarMass(string id) => id switch
        {
            "etanol" => 46.07, "metanol" => 32.04, "axeton" => 58.08, "axit_axetic" => 60.05, "etyl_axetat" => 88.11,
            "isoamyl_axetat" => 130.19, "glucozo" => 180.16, "saccarozo" => 342.3, "axit_xitric" => 192.12, _ => 100,
        };

        // =====================================================================
        // Sơ đồ
        // =====================================================================

        public override void DrawSchematic(Painter p, float time)
        {
            SchemKit.Begin(p);
            var c = new Vector2(330, 175);
            SchemKit.Box(p, c.x - 230, c.y - 60, 130, 120, new Color(0.7f, 0.25f, 0.25f));  // cực N
            SchemKit.Box(p, c.x + 100, c.y - 60, 130, 120, new Color(0.25f, 0.35f, 0.7f));  // cực S
            for (int i = 0; i < 6; i++) p.Dashed(c.x - 100, c.y - 50 + i * 20, c.x + 100, c.y - 50 + i * 20, new Color(1, 1, 1, 0.15f), 1, 6, 0);
            // ống mẫu + cuộn RF
            p.FillRect(c.x - 12, c.y - 150, 24, 300, new Color(0.85f, 0.92f, 1f, 0.25f));
            if (Inserted)
            {
                Color lq = Sample != null ? new Color(Sample.Look.Liquid.r, Sample.Look.Liquid.g, Sample.Look.Liquid.b, 0.6f) : new Color(0.8f, 0.9f, 1f, 0.5f);
                p.FillRect(c.x - 10, c.y - 70, 20, 90, lq);
            }
            for (int i = 0; i < 6; i++) p.Ring(c.x, c.y - 40 + i * 12, 20, new Color(0.9f, 0.65f, 0.3f), 2);
            // spin tiến động
            if (Inserted)
                for (int i = 0; i < 5; i++)
                {
                    float a = time * (Acquiring ? 9 : 2) + i * 1.3f;
                    var o = new Vector2(c.x - 50 + i * 25, c.y + 95);
                    float tilt = Acquiring ? 0.9f : 0.25f;
                    p.Line(o.x, o.y, o.x + Mathf.Cos(a) * 12 * tilt, o.y + 24, new Color(0.6f, 0.9f, 1f), 2);
                    p.Disc(o.x, o.y, 3, new Color(0.9f, 0.95f, 1f));
                }
            // FID nhỏ ở bên phải
            var f0 = new Vector2(620, 60);
            SchemKit.Box(p, f0.x, f0.y, 330, 220, new Color(0.06f, 0.08f, 0.1f));
            if (fidRe != null)
            {
                int show = Acquiring ? (int)(N * Mathf.Clamp01(acqT / AcqS)) : N;
                double mx = fidRe.Take(50).Max(Math.Abs);
                var pts = new List<Vector2>();
                for (int i = 0; i < show && i < 800; i += 2) pts.Add(new Vector2(f0.x + 5 + i * 320f / 800, f0.y + 110 + (float)(fidRe[i] / mx) * 100));
                if (pts.Count > 1) p.Poly(pts, new Color(0.45f, 0.95f, 0.62f), 1.5f);
            }
            var d = Doc;
            S.SchemLabel("nam_cham", d.PartName("nam_cham"), c.x, 22, 12);
            S.SchemLabel("ong", d.PartName("ong_mau"), c.x + 90, c.y + 150, 11);
            S.SchemLabel("cuon", d.PartName("cuon_rf") + (Acquiring ? "\n<color=#FFD27A>xung 60 MHz</color>" : ""), c.x - 150, c.y + 100, 11);
            S.SchemLabel("shim", d.PartName("cuon_shim") + (Shimmed ? " · <color=#73F29E>đã shim</color>" : ""), c.x, c.y - 110, 11);
            S.SchemLabel("fid", "FID (tín hiệu tắt dần)", f0.x + 165, f0.y + 240, 12);
            S.SchemLabel("trang_thai", !Inserted ? "<color=#8899AA>CHƯA CÓ ỐNG MẪU</color>" : shimT >= 0 ? "<color=#FFC966>ĐANG SHIM</color>" : Acquiring ? "<color=#61DBFF>ĐANG ĐO</color>" : "<color=#73F29E>SẴN SÀNG</color>", 785, 330, 14);
        }

        // =====================================================================
        // Đồ thị
        // =====================================================================

        public override void DrawPlot(Painter p)
        {
            InstrumentScreen.PlotFrame(p);
            if (plotMode == 0 || Spectrum == null)
            {
                S.SetPlotAxes("FID (phần thực)", "Thời gian (s)", "Tín hiệu", "0", Fmt(N / Sw, "0.0"), "", "");
                if (fidRe == null) return;
                double mx = fidRe.Max(Math.Abs);
                var pts = new List<Vector2>();
                int show = Acquiring ? (int)(N * Mathf.Clamp01(acqT / AcqS)) : N;
                for (int i = 0; i < show; i++) pts.Add(new Vector2(3 + i * (p.W - 6f) / N, p.H / 2f + (float)(fidRe[i] / mx) * (p.H / 2f - 8)));
                if (pts.Count > 1) p.Poly(pts, new Color(0.45f, 0.95f, 0.62f), 1);
                return;
            }
            double x0 = 6, x1 = -0.5;  // ppm, giảm dần sang phải
            var vis = Spectrum.Where(s => s.ppm <= x0 && s.ppm >= x1).ToList();
            double yMax = vis.Where(s => Presat || Math.Abs(s.ppm - 4.79) > 0.3).Select(s => s.y).DefaultIfEmpty(1).Max() * 1.15;
            double yMin = Math.Min(0, vis.Min(s => s.y)) * 1.1;
            if (yMin < -yMax) yMin = -yMax;
            S.SetPlotAxes($"Phổ ¹H-NMR 60 MHz · {SampleName}" + (Phased ? "" : " (chưa chỉnh pha)"), "δ (ppm)", "Cường độ", "6", "−0,5", "", "");
            var pts2 = vis.Select(s => InstrumentScreen.Map(p, x0 - s.ppm, Math.Max(yMin, Math.Min(yMax, s.y)), 0, x0 - x1, yMin, yMax)).ToList();
            p.Poly(pts2, new Color(0.45f, 0.86f, 1f), 1.5f);
            if (Signals != null)
                foreach (var s in Signals.Where(s => s.Owner != "nuoc"))
                {
                    var q = InstrumentScreen.Map(p, x0 - s.Ppm, yMax * 0.95, 0, x0 - x1, yMin, yMax);
                    p.FillRect(q.x - 1, q.y - 14, 2, 14, new Color(1f, 0.6f, 0.9f));
                }
        }
    }
}
