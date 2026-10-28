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
    /// Máy HPLC (thông số theo Shimadzu Prominence LC-20: bơm tới 40 MPa, detector UV SPD-20A, RID-20A). Ba phương pháp theo tài liệu:
    /// axit hữu cơ (C18, pH 2,5, UV 210 nm), đường (NH₂, axetonitril 75%, RID), axit benzoic (C18, metanol 40%, pH 4, UV 228 nm).
    /// Thời gian lưu theo log k = log k_w − S·φ và pH; áp suất theo định luật Darcy. Lỗi mô phỏng: không đuổi khí (áp suất dao động,
    /// nền nhiễu), chưa cân bằng cột (thời gian lưu trôi), không lọc mẫu có cặn (cột tắc dần), lưu lượng / độ nhớt quá cao (vượt 400 bar).
    /// </summary>
    public class HplcProgram : InstrumentProgram
    {
        public override string Code => "hplc";
        static readonly string[] optional = { "day_chuan", "ket_thuc" };
        public override ICollection<string> Optional => optional;

        const float EquilNeedS = 5f, RunS = 6f;
        static readonly double[] flows = { 0.5, 1.0, 1.5, 2.0 };

        public int MethodIdx { get; private set; }
        public bool Pump { get; private set; }
        public bool Purged { get; private set; }
        public float Equil { get; private set; }
        public double Flow { get; private set; } = 1.0;
        public double Phi { get; private set; }
        public double Ph { get; private set; }
        public bool Filtered { get; private set; }
        public int Dilution { get; private set; } = 1;
        public bool UseStd { get; private set; }
        public int StdIndex { get; private set; }
        int analyteIdx;
        public double Clog { get; private set; } = 1;
        bool tripped;

        float runT = -1;
        string runName = "";
        bool runIsStd;
        public List<HplcLibrary.Peak> Peaks { get; private set; } = new List<HplcLibrary.Peak>();
        readonly List<(double t, double y)> trace = new List<(double, double)>();
        readonly List<string> notSeen = new List<string>();
        public readonly List<(double c, double a)> Cal = new List<(double, double)>();
        string calFor = "";
        readonly HashSet<string> stdSeen = new HashSet<string>();
        public double Found { get; private set; } = double.NaN;
        bool plotCal;
        readonly System.Random rng = new System.Random(5);

        public bool Running => runT >= 0;
        public void SetPlot(int m) => plotCal = m == 1;
        HplcLibrary Lib => InstrumentData.Instance != null ? InstrumentData.Instance.Hplc : new HplcLibrary();
        public HplcLibrary.MethodDef Method => Lib.Methods.Count > 0 ? Lib.Methods[MethodIdx % Lib.Methods.Count] : null;
        HplcLibrary.Compound Analyte => Method != null && Method.Compounds.Count > 0 ? Method.Compounds[DefaultAnalyte() % Method.Compounds.Count] : null;
        bool Sugar => Method?.Detector == "ri";
        bool Ready => Pump && !tripped;
        public double PressureBar => Pump && !tripped ? Lib.Pressure(Method?.Solvent ?? "", Phi, Flow, 30, Clog) : 0;

        /// <summary>Nồng độ dãy chuẩn: axit hữu cơ mg/L, đường g/L, axit benzoic mg/L.</summary>
        public double[] StdLevels => Sugar ? new[] { 1.0, 2, 4, 6, 8 } : Method?.Id == "bao_quan" ? new[] { 10.0, 25, 50, 100, 200 } : new[] { 50.0, 100, 200, 400, 800 };
        string Unit => Sugar ? "g/L" : "mg/L";
        double UnitToMolL(double v, HplcLibrary.Compound c) => Sugar ? v / c.M : v / 1000.0 / c.M;

        Button bMethod, bPump, bPurge, bFlow, bPhi, bPh, bFilter, bDil, bSrc, bStd, bAnalyte, bRun, bPlot, bOff;
        Text status;

        int DefaultAnalyte()
        {
            if (Method == null) return 0;
            if (analyteIdx >= 0) return analyteIdx;
            string want = Method.Id == "axit_huu_co" ? "citric" : Method.Id == "duong" ? "saccarozo" : "benzoic";
            return Math.Max(0, Method.Compounds.FindIndex(c => c.Id == want));
        }

        // =====================================================================

        public override void Build()
        {
            analyteIdx = -1;
            ApplyMethod();
            bMethod = S.AddButton("", 0, 0, NextMethod, false, 2);
            bPump = S.AddButton("Bật bơm", 2, 0, StartPump, true);
            bPurge = S.AddButton("Đuổi khí (purge)", 0, 1, Purge, false, 1, 13);
            bFlow = S.AddButton("", 1, 1, NextFlow);
            bPhi = S.AddButton("", 2, 1, NextPhi);
            bPh = S.AddButton("", 0, 2, NextPh);
            bFilter = S.AddButton("Lọc mẫu 0,45 µm", 1, 2, FilterSample, false, 1, 13);
            bDil = S.AddButton("", 2, 2, NextDilution);
            bSrc = S.AddButton("", 0, 3, ToggleSource);
            bStd = S.AddButton("", 1, 3, NextStd, false, 2, 13);
            bAnalyte = S.AddButton("", 0, 4, NextAnalyte, false, 2, 13);
            bPlot = S.AddButton("", 2, 4, () => plotCal = !plotCal);
            bRun = S.AddButton("Bơm mẫu 10 µL, chạy", 0, 5, StartRun, true, 2);
            bOff = S.AddButton("Rửa cột, tắt bơm", 2, 5, TurnOff, false, 1, 13);
            status = S.AddText("", 6, 0, 3, 13);
            S.SetResults("Chọn phương pháp theo chất cần đo, bật bơm, đuổi khí và cân bằng cột. Đặt ống mẫu vào ổ trên nóc máy ở phòng lab trước khi mở màn hình này.");
        }

        public override void Refresh()
        {
            var m = Method;
            InstrumentScreen.SetButton(bMethod, m != null ? $"Phương pháp: {m.Name}" : "-", !Running && !Pump);
            InstrumentScreen.SetButton(bPump, tripped ? "Bơm dừng: quá áp!" : Pump ? $"Bơm chạy · {PressureBar:0} bar" : "Bật bơm", !Running, Pump && !tripped);
            InstrumentScreen.SetButton(bPurge, Purged ? "Đã đuổi khí" : "Đuổi khí (purge)", !Running, Purged);
            InstrumentScreen.SetButton(bFlow, $"Lưu lượng {Fmt(Flow, "0.0")} mL/phút", !Running);
            InstrumentScreen.SetButton(bPhi, $"{(m?.Solvent == "metanol" ? "Metanol" : "Axetonitril")} {Phi * 100:0}%", !Running);
            InstrumentScreen.SetButton(bPh, Sugar ? "pH: không dùng" : $"pH pha động {Fmt(Ph, "0.0")}", !Running && !Sugar);
            InstrumentScreen.SetButton(bFilter, Filtered ? "Mẫu đã lọc 0,45 µm" : "Lọc mẫu 0,45 µm", !Running && Sample != null, Filtered);
            InstrumentScreen.SetButton(bDil, $"Pha loãng mẫu ×{Dilution}", !Running, Dilution > 1);
            InstrumentScreen.SetButton(bSrc, UseStd ? "Bơm: chuẩn" : "Bơm: mẫu ở ổ", !Running);
            InstrumentScreen.SetButton(bStd, UseStd ? $"Chuẩn {StdIndex + 1}/5: {Fmt(StdLevels[StdIndex], "0.##")} {Unit}" : Sample != null ? "Mẫu: " + SampleName : "Ổ mẫu trống", !Running && UseStd, UseStd);
            InstrumentScreen.SetButton(bAnalyte, "Chất chuẩn: " + (Analyte?.Name ?? "-"), !Running);
            InstrumentScreen.SetButton(bPlot, plotCal ? "Đồ thị: đường chuẩn" : "Đồ thị: sắc ký đồ", true);
            InstrumentScreen.SetButton(bRun, Running ? "Đang chạy…" : "Bơm mẫu 10 µL, chạy", Ready && !Running && (UseStd || Sample != null));
            InstrumentScreen.SetButton(bOff, null, Pump && !Running);
            string eq = !Pump ? "bơm tắt" : Equil < EquilNeedS ? $"đang cân bằng cột {Equil / EquilNeedS:P0}" : "cột đã cân bằng";
            status.text = $"{m?.Column} · lò cột 30 °C · {eq} · detector {(Sugar ? "RID" : $"UV {m?.Lambda:0} nm")}";
            S.SetResults(BuildResults());
        }

        // =====================================================================
        // Thao tác
        // =====================================================================

        void ApplyMethod()
        {
            var m = Method;
            if (m == null) return;
            Phi = m.Phi;
            Ph = m.Ph;
            analyteIdx = -1;
            Cal.Clear();
            stdSeen.Clear();
            Found = double.NaN;
        }

        public void NextMethod()
        {
            if (Running || Pump) { S.Say("Tắt bơm trước khi đổi cột, pha động", 2); return; }
            MethodIdx = (MethodIdx + 1) % Math.Max(1, Lib.Methods.Count);
            ApplyMethod();
            S.Done("chon_phuong_phap");
            S.Say($"Lắp cột {Method.Column}; pha động {Method.Mobile}; detector {(Sugar ? "chỉ số khúc xạ (RID)" : $"UV {Method.Lambda:0} nm")}.", 0);
        }

        public void SelectMethod(string id)
        {
            int i = Lib.Methods.FindIndex(x => x.Id == id);
            if (i < 0 || Pump) return;
            MethodIdx = i;
            ApplyMethod();
            S.Done("chon_phuong_phap");
        }

        public void StartPump()
        {
            if (Pump && !tripped) return;
            S.Done("chon_phuong_phap");
            Pump = true;
            tripped = false;
            Equil = 0;
            S.Say($"Bơm chạy {Fmt(Flow, "0.0")} mL/phút: áp suất {PressureBar:0} bar. Chờ cột cân bằng…", 0);
            CheckPressure();
        }

        public void Purge()
        {
            if (Running) return;
            if (!Pump) { S.Say("Bật bơm trước rồi mới mở van xả để đuổi khí", 2); return; }
            if (!S.Require("duoi_khi")) return;
            Purged = true;
            S.Done("duoi_khi");
            S.Say("Mở van xả, bơm 5 mL/phút trong 3 phút: hết bọt khí ở đường ống và piston.", 1);
        }

        public void NextFlow()
        {
            int i = Array.IndexOf(flows, Flow);
            Flow = flows[(i + 1) % flows.Length];
            if (Pump) { Equil = Math.Min(Equil, EquilNeedS * 0.6f); CheckPressure(); }
            if (Cal.Count > 0) S.Say("Đổi lưu lượng: thời gian lưu đổi theo, nên chạy lại chuẩn", 2);
        }

        public void NextPhi()
        {
            var m = Method;
            if (m == null) return;
            double[] opts = Sugar ? new[] { 0.70, 0.75, 0.80 } : m.Id == "bao_quan" ? new[] { 0.30, 0.40, 0.50, 0.60 } : new[] { 0.0, 0.05, 0.10 };
            int i = Array.FindIndex(opts, o => Math.Abs(o - Phi) < 1e-6);
            Phi = opts[(i + 1) % opts.Length];
            if (Pump) { Equil = 0; CheckPressure(); }
            Cal.Clear();
            S.Say($"Pha động mới ({Phi * 100:0}% {m.Solvent}): phải cân bằng cột lại; đường chuẩn cũ đã xóa.", 0);
        }

        public void NextPh()
        {
            if (Sugar) return;
            Ph = Ph < 3 ? 4.0 : Ph < 5 ? 7.0 : 2.5;
            if (Pump) Equil = 0;
            Cal.Clear();
            S.Say($"Pha động pH {Fmt(Ph, "0.0")}" + (Method?.Id == "axit_huu_co" && Ph > 4 ? ": axit hữu cơ bị ion hóa, gần như không được cột C18 giữ!" : ""), Method?.Id == "axit_huu_co" && Ph > 4 ? 2 : 0);
        }

        public void SetPhi(double phi) { Phi = phi; if (Pump) CheckPressure(); }
        public void SetPh(double ph) => Ph = ph;
        public void SetFlow(double f) { Flow = f; if (Pump) CheckPressure(); }
        public void SetDilution(int d) => Dilution = Math.Max(1, d);
        public void SelectAnalyte(string id) { int i = Method?.Compounds.FindIndex(c => c.Id == id) ?? -1; if (i >= 0) { analyteIdx = i; Cal.Clear(); } }
        public void SelectStd(int i) { UseStd = true; StdIndex = Mathf.Clamp(i, 0, 4); }
        public void UseSample() => UseStd = false;
        public void SkipEquilibration() => Equil = EquilNeedS;

        public void FilterSample()
        {
            if (Sample == null) return;
            Filtered = true;
            S.Done("loc_mau");
            S.Say("Lọc mẫu qua màng 0,45 µm vào lọ bơm mẫu.", 1);
        }

        public void NextDilution() { Dilution = Dilution == 1 ? 10 : Dilution == 10 ? 20 : Dilution == 20 ? 100 : 1; }
        public void ToggleSource() => UseStd = !UseStd;
        public void NextStd() { UseStd = true; StdIndex = (StdIndex + 1) % 5; }

        public void NextAnalyte()
        {
            var m = Method;
            if (m == null) return;
            analyteIdx = (DefaultAnalyte() + 1) % m.Compounds.Count;
            Cal.Clear();
            Found = double.NaN;
            S.Say($"Dãy chuẩn mới: {Analyte.Name}. Đường chuẩn cũ đã xóa.", 0);
        }

        void CheckPressure()
        {
            double p = Lib.Pressure(Method?.Solvent ?? "", Phi, Flow, 30, Clog);
            if (p > HplcLibrary.MaxBar)
            {
                tripped = true;
                S.Say($"ÁP SUẤT {p:0} bar VƯỢT GIỚI HẠN {HplcLibrary.MaxBar:0} bar: bơm tự dừng. Giảm lưu lượng / dùng pha động ít nhớt hơn" + (Clog > 1.2 ? ", thay lọc đầu cột (bị tắc do mẫu không lọc)" : "") + ".", 3);
            }
        }

        public void StartRun()
        {
            if (!Ready || Running) return;
            if (!UseStd && Sample == null) return;
            if (!S.Require(UseStd ? "day_chuan" : "chay_mau")) return;
            if (S.Guided && Equil < EquilNeedS) { S.Say("Cột chưa cân bằng: chờ áp suất, đường nền ổn định", 2); return; }
            var m = Method;
            Mix mix;
            double vol, dil;
            runIsStd = UseStd;
            if (UseStd)
            {
                var a = Analyte;
                mix = Mix.Of((a.Ids[0], UnitToMolL(StdLevels[StdIndex], a)));
                vol = 1000; dil = 1;
                runName = $"chuẩn {a.Name} {Fmt(StdLevels[StdIndex], "0.##")} {Unit}";
            }
            else
            {
                mix = Sample.Total.Mix;
                vol = Sample.VolumeMl;
                dil = Dilution;
                runName = $"{SampleName} (×{Dilution})";
                if (HasSolid(mix) && !Filtered) { Clog += 0.35; S.Say("Mẫu có cặn mà chưa lọc: hạt rắn bám đầu cột, áp suất tăng!", 2); CheckPressure(); if (tripped) return; }
            }
            notSeen.Clear();
            double ph = Sugar ? 7 : Ph;
            Peaks = Lib.Peaks(m, mix, vol, dil, Phi, ph, Flow, 10, notSeen);
            if (Equil < EquilNeedS)  // cột chưa cân bằng: thời gian lưu trôi
                for (int i = 0; i < Peaks.Count; i++) { var p = Peaks[i]; p.Tr *= 1 + 0.06 * (1 - Equil / EquilNeedS); Peaks[i] = p; }
            trace.Clear();
            runT = 0;
            plotCal = false;
            S.Say($"Bơm 10 µL {runName}…", 0);
        }

        public void TurnOff()
        {
            if (!Pump || Running) return;
            Pump = false;
            tripped = false;
            Equil = 0;
            if (S.DoneSteps.Contains("dinh_luong")) S.Done("ket_thuc");
            S.Say("Rửa cột bằng nước rồi metanol 50%, giảm dần lưu lượng, tắt bơm.", 0);
        }

        static bool HasSolid(Mix m)
        {
            for (int i = 0; i < m.Count; i++) if (m.SpeciesAt(i).IsSolidPhase && m.MolAt(i) > 1e-7) return true;
            return false;
        }

        // =====================================================================
        // Mô phỏng
        // =====================================================================

        public override void Tick(float dt)
        {
            if (Pump && !tripped && Equil < EquilNeedS)
            {
                Equil += dt * (float)Flow;
                if (Equil >= EquilNeedS) { S.Done("can_bang"); S.Say($"Cột đã cân bằng: áp suất ổn định {PressureBar:0} bar, đường nền phẳng.", 1); }
            }
            if (!Running) return;
            runT += dt;
            double run = Method.RunMin, tNow = runT / RunS * run;
            double from = trace.Count > 0 ? trace[trace.Count - 1].t : 0;
            for (double t = from + 0.01; t <= tNow; t += 0.01) trace.Add((t, Signal(t)));
            if (runT >= RunS) Finish();
        }

        double Signal(double t)
        {
            double y = HplcLibrary.Signal(Peaks, t);
            double noise = Purged ? 0.4 : 4;
            if (!Purged && Mathf.Repeat((float)t * 1.7f, 1) < 0.04f) noise += 25;  // bọt khí đi qua ô đo: gai nhọn
            if (Equil < EquilNeedS) y += 15 * (1 - Equil / EquilNeedS) * t / Method.RunMin;  // đường nền dốc
            return y + Gauss() * noise;
        }

        void Finish()
        {
            runT = -1;
            if (runIsStd)
            {
                var a = Analyte;
                var pk = Peaks.FirstOrDefault(p => p.C == a);
                if (pk.C == null || pk.Tr > Method.RunMin) { S.Say($"Không thấy pic {a.Name} trong {Method.RunMin:0} phút: chất bị giữ quá lâu hoặc detector không thấy.", 2); return; }
                if (calFor != a.Id + Method.Id) { Cal.Clear(); calFor = a.Id + Method.Id; }
                Cal.RemoveAll(c => c.c == StdLevels[StdIndex]);
                Cal.Add((StdLevels[StdIndex], pk.Area));
                Cal.Sort();
                stdSeen.Add(a.Id);
                if (Cal.Count >= 3) S.Done("day_chuan");
                S.Say($"Chuẩn {StdIndex + 1}: t_R = {Fmt(pk.Tr, "0.00")} phút, diện tích {Fmt(pk.Area, "0.0")}" + (Cal.Count >= 2 ? $" · R² {Fmt(Fit().R2, "0.0000")}" : ""), 1);
                if (StdIndex < 4) StdIndex++;
                plotCal = Cal.Count >= 2;
                return;
            }
            S.Done("chay_mau");
            var seen = Peaks.Where(p => p.Tr <= Method.RunMin).ToList();
            var late = Peaks.Where(p => p.Tr > Method.RunMin).ToList();
            Found = double.NaN;
            var ap = seen.FirstOrDefault(p => p.C == Analyte);
            if (Cal.Count >= 2 && calFor == Analyte.Id + Method.Id)
            {
                Found = ap.C != null ? Fit().X(ap.Area) * Dilution : 0;
                S.Done("dinh_luong");
                double top = Cal.Max(c => c.a);
                if (ap.C != null && ap.Area > top * 1.05) S.Say($"Pic {Analyte.Name} lớn hơn chuẩn cao nhất: pha loãng thêm rồi chạy lại.", 2);
            }
            S.Say($"Chạy xong {runName}: {seen.Count} pic" + (late.Count > 0 ? $"; {late.Count} chất chưa ra khỏi cột sau {Method.RunMin:0} phút ({string.Join(", ", late.Select(p => p.C.Name))})" : ""), late.Count > 0 ? 2 : 1);
            for (int i = 1; i < seen.Count; i++)
                if (HplcLibrary.Resolution(seen[i - 1], seen[i]) < 1.5) { S.Say($"Hai pic {seen[i - 1].C.Name} và {seen[i].C.Name} chưa tách hoàn toàn (Rs < 1,5).", 2); break; }
        }

        UvVisLibrary.Fit Fit() => UvVisLibrary.Linear(Cal.Select(c => (c.c, c.a)).ToList());

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
            var m = Method;
            if (m == null) return "";
            double t0 = Lib.DeadTime(Flow);
            sb.Append($"<color=#9FB3C8>Pha động:</color> {m.Mobile}" + (Math.Abs(Phi - m.Phi) > 1e-6 || Math.Abs(Ph - m.Ph) > 1e-6 && !Sugar ? $" <color=#FFC966>(đang đặt {Phi * 100:0}% {m.Solvent}{(Sugar ? "" : $", pH {Fmt(Ph, "0.0")}")})</color>" : "") + "\n");
            sb.Append($"<color=#9FB3C8>t₀ = V_cột / F = {Fmt(Lib.ColumnVolume, "0.00")} / {Fmt(Flow, "0.0")} = {Fmt(t0, "0.00")} phút · áp suất {PressureBar:0} bar (giới hạn {HplcLibrary.MaxBar:0})</color>\n");
            if (Running) sb.Append($"\n<color=#61DBFF>Đang chạy {runName}…</color>\n");
            if (!Running && trace.Count > 0)
            {
                var seen = Peaks.Where(p => p.Tr <= m.RunMin).ToList();
                sb.Append($"\n<b>Bảng pic ({runName})</b>\n");
                if (seen.Count == 0) sb.Append("Không có pic.\n");
                for (int i = 0; i < seen.Count; i++)
                {
                    var p = seen[i];
                    double k = (p.Tr - t0) / t0;
                    string rs = i > 0 ? $" · Rs {Fmt(HplcLibrary.Resolution(seen[i - 1], p), "0.0")}" : "";
                    sb.Append($"{Fmt(p.Tr, "0.00")} phút · k = {Fmt(k, "0.00")} · diện tích {Fmt(p.Area, "0.0")} · <b>{p.C.Name}</b>{(stdSeen.Contains(p.C.Id) ? " (khớp t_R chuẩn)" : "")}{rs}\n");
                }
            }
            if (Cal.Count > 0)
            {
                sb.Append($"\n<b>Đường chuẩn {Analyte?.Name}</b> ({Cal.Count} điểm)");
                if (Cal.Count >= 2) { var f = Fit(); sb.Append($": diện tích = {Fmt(f.Slope, "0.000")}·c {(f.Intercept >= 0 ? "+" : "−")} {Fmt(Math.Abs(f.Intercept), "0.00")}, R² {Fmt(f.R2, "0.0000")}"); }
                sb.Append('\n');
            }
            if (!double.IsNaN(Found))
            {
                sb.Append($"\n<b>Phân tích chuyên sâu</b>\n{Analyte.Name} trong mẫu gốc: <b>{Fmt(Found, Sugar ? "0.0" : "0")} {Unit}</b> (đo {Fmt(Found / Dilution, "0.###")} {Unit} × pha loãng {Dilution})\n");
                if (Analyte.Id == "benzoic") sb.Append($"<color=#9FB3C8>= {Fmt(Found * 144.1 / 122.12, "0")} mg/L tính theo natri benzoat</color>\n");
            }
            if (notSeen.Count > 0) sb.Append($"\n<color=#FFC966>Phương pháp / detector này không thấy:</color> {string.Join(", ", notSeen.Distinct())}" + (Sugar ? "" : " (đường không hấp thụ UV: dùng phương pháp Đường với RID)") + "\n");
            return sb.ToString();
        }

        // =====================================================================
        // Sơ đồ
        // =====================================================================

        public override void DrawSchematic(Painter p, float time)
        {
            SchemKit.Begin(p);
            float ph = time * 60 * (float)Flow;
            bool flow = Ready;
            // bình pha động, khử khí
            SchemKit.Vessel(p, 30, 200, 60, 110, 0.75f, new Color(0.7f, 0.85f, 1f, 0.35f));
            SchemKit.Box(p, 130, 230, 60, 50);
            SchemKit.Pipe(p, new[] { new Vector2(60, 300), new Vector2(60, 320), new Vector2(110, 320), new Vector2(110, 255), new Vector2(130, 255) }, flow, ph);
            // bơm hai piston
            var pump = new Vector2(270, 255);
            SchemKit.Box(p, pump.x - 50, pump.y - 40, 100, 80);
            float s1 = flow ? Mathf.Sin(time * 8 * (float)Flow) : 0;
            p.FillRect(pump.x - 35, pump.y - 10 + s1 * 12, 26, 8, new Color(0.75f, 0.75f, 0.8f));
            p.FillRect(pump.x + 9, pump.y - 10 - s1 * 12, 26, 8, new Color(0.75f, 0.75f, 0.8f));
            SchemKit.Pipe(p, new[] { new Vector2(190, 255), new Vector2(pump.x - 50, 255) }, flow, ph);
            // đồng hồ áp suất
            float pk = (float)(PressureBar / HplcLibrary.MaxBar);
            p.Ring(pump.x, pump.y + 75, 22, SchemKit.Edge, 2);
            float a = Mathf.Lerp(200, -20, Mathf.Clamp01(pk)) * Mathf.Deg2Rad;
            p.Line(pump.x, pump.y + 75, pump.x + Mathf.Cos(a) * 18, pump.y + 75 + Mathf.Sin(a) * 18, pk > 0.9f ? new Color(1f, 0.4f, 0.4f) : SchemKit.Good, 3);
            // van bơm mẫu
            var valve = new Vector2(400, 255);
            p.Disc(valve.x, valve.y, 26, SchemKit.Body);
            p.Ring(valve.x, valve.y, 26, SchemKit.Edge, 2);
            float va = Running && runT < 0.6f ? 60 : 0;
            p.Line(valve.x - 18 * Mathf.Cos(va * Mathf.Deg2Rad), valve.y - 18 * Mathf.Sin(va * Mathf.Deg2Rad), valve.x + 18 * Mathf.Cos(va * Mathf.Deg2Rad), valve.y + 18 * Mathf.Sin(va * Mathf.Deg2Rad), SchemKit.Flow, 4);
            p.Ring(valve.x, valve.y + 50, 14, new Color(0.9f, 0.8f, 0.5f, 0.8f), 2);  // vòng chứa mẫu
            SchemKit.Pipe(p, new[] { new Vector2(pump.x + 50, 255), new Vector2(valve.x - 26, 255) }, flow, ph);
            // cột trong lò
            var colA = new Vector2(470, 160);
            SchemKit.Box(p, colA.x - 10, colA.y - 50, 360, 100, new Color(0.15f, 0.18f, 0.22f));
            p.FillRect(colA.x, colA.y - 14, 340, 28, new Color(0.55f, 0.58f, 0.62f));
            SchemKit.Pipe(p, new[] { new Vector2(valve.x + 26, 255), new Vector2(440, 255), new Vector2(440, colA.y), new Vector2(colA.x, colA.y) }, flow, ph);
            if (Running)
            {
                Color[] cols = { new Color(1f, 0.5f, 0.8f), new Color(1f, 0.8f, 0.3f), new Color(0.5f, 1f, 0.6f), new Color(0.6f, 0.7f, 1f) };
                double tNow = runT / RunS * Method.RunMin;
                int k = 0;
                foreach (var pk2 in Peaks)
                {
                    double z = tNow / pk2.Tr;
                    if (z <= 1) p.FillRect(colA.x + (float)z * 330, colA.y - 12, 10, 24, cols[k % cols.Length]);
                    k++;
                }
            }
            // detector
            var det = new Vector2(880, 160);
            SchemKit.Box(p, det.x - 40, det.y - 50, 80, 100);
            SchemKit.Pipe(p, new[] { new Vector2(colA.x + 340, colA.y), new Vector2(det.x - 40, det.y) }, flow, ph);
            if (!Sugar && flow) p.Line(det.x, det.y + 45, det.x, det.y - 45, Painter.Wavelength((float)Method.Lambda) * new Color(1, 1, 1, 0.8f), 3);
            if (Sugar) p.FillPoly(new[] { new Vector2(det.x - 20, det.y - 20), new Vector2(det.x + 20, det.y - 20), new Vector2(det.x, det.y + 20) }, new Color(0.7f, 0.85f, 1f, 0.5f));
            SchemKit.Pipe(p, new[] { new Vector2(det.x + 40, det.y), new Vector2(950, det.y), new Vector2(950, 40) }, flow, ph);
            var d = Doc;
            S.SchemLabel("binh", d.PartName("binh_dung_moi"), 60, 330, 11);
            S.SchemLabel("khi", d.PartName("khu_khi"), 160, 300, 11);
            S.SchemLabel("bom", d.PartName("bom") + $"\n<color={(pk > 0.9f ? "#FF7A7A" : "#73F29E")}>{PressureBar:0} bar</color>", pump.x, pump.y - 64, 11);
            S.SchemLabel("van", d.PartName("van_bom_mau"), valve.x, valve.y - 40, 11);
            S.SchemLabel("cot", (Method?.Column ?? "") + " · lò cột 30 °C", colA.x + 170, colA.y + 68, 12);
            S.SchemLabel("det", Sugar ? d.PartName("det_ri") : d.PartName("det_uv") + $"\n<color=#61DBFF>{Method?.Lambda:0} nm</color>", det.x, det.y - 75, 11);
            S.SchemLabel("thai", d.PartName("thai"), 950, 24, 11);
            S.SchemLabel("trang_thai", tripped ? "<color=#FF7A7A>BƠM DỪNG: QUÁ ÁP</color>" : !Pump ? "<color=#8899AA>BƠM TẮT</color>" : Equil < EquilNeedS ? $"<color=#FFC966>ĐANG CÂN BẰNG CỘT {Equil / EquilNeedS:P0}</color>" : Running ? "<color=#61DBFF>ĐANG CHẠY</color>" : "<color=#73F29E>SẴN SÀNG</color>", 600, 330, 14);
        }

        // =====================================================================
        // Đồ thị
        // =====================================================================

        public override void DrawPlot(Painter p)
        {
            InstrumentScreen.PlotFrame(p);
            if (plotCal && Cal.Count > 0) { DrawCalibration(p); return; }
            var m = Method;
            double x1 = m?.RunMin ?? 10;
            double yMax = Math.Max(20, trace.Count > 0 ? trace.Max(t => t.y) * 1.1 : 20);
            S.SetPlotAxes($"Sắc ký đồ {runName}", "Thời gian (phút)", Sugar ? "Tín hiệu RID (µRIU)" : $"Độ hấp thụ ở {m?.Lambda:0} nm (mAU)", "0", Fmt(x1, "0"), "0", Fmt(yMax, "0"));
            var t0 = InstrumentScreen.Map(p, Lib.DeadTime(Flow), 0, 0, x1, 0, yMax);
            p.FillRect(t0.x, 0, 1, p.H, new Color(1, 1, 1, 0.25f));
            if (trace.Count > 1) p.Poly(trace.Select(t => InstrumentScreen.Map(p, t.t, Math.Max(0, t.y), 0, x1, 0, yMax)).ToList(), new Color(0.7f, 0.6f, 1f), 2);
        }

        void DrawCalibration(Painter p)
        {
            double cMax = StdLevels[4] * 1.2, aMax = Math.Max(1, Cal.Max(c => c.a) * 1.2);
            S.SetPlotAxes($"Đường chuẩn {Analyte?.Name}", $"Nồng độ ({Unit})", "Diện tích pic", "0", Fmt(cMax, "0.#"), "0", Fmt(aMax, "0"));
            if (Cal.Count >= 2)
            {
                var f = Fit();
                var a0 = InstrumentScreen.Map(p, 0, f.Intercept, 0, cMax, 0, aMax);
                var a1 = InstrumentScreen.Map(p, cMax, f.Intercept + f.Slope * cMax, 0, cMax, 0, aMax);
                p.Line(a0.x, a0.y, a1.x, a1.y, new Color(0.43f, 0.86f, 1f, 0.8f), 2);
            }
            foreach (var c in Cal) { var q = InstrumentScreen.Map(p, c.c, c.a, 0, cMax, 0, aMax); p.Disc(q.x, q.y, 6, SchemKit.Good); }
            if (!double.IsNaN(Found) && Found > 0)
            {
                var ap = Peaks.FirstOrDefault(x => x.C == Analyte);
                var q = InstrumentScreen.Map(p, Found / Dilution, Math.Min(aMax, ap.Area), 0, cMax, 0, aMax);
                p.Line(q.x - 8, q.y - 8, q.x + 8, q.y + 8, new Color(1f, 0.45f, 0.45f), 3);
                p.Line(q.x - 8, q.y + 8, q.x + 8, q.y - 8, new Color(1f, 0.45f, 0.45f), 3);
            }
        }
    }
}
