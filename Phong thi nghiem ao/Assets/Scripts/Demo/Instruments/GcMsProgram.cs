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
    /// Máy GC-MS tứ cực đơn (thông số theo Shimadzu GCMS-QP2020 NX, cột 5% phenyl 30 m). Thời gian lưu tính từ chỉ số lưu RI (NIST)
    /// theo chương trình nhiệt của lò; phổ khối EI 70 eV của NIST; tra thư viện theo độ khớp; định lượng metanol / etanol theo dãy chuẩn.
    /// Lỗi mô phỏng: lò bắt đầu nóng (pic đầu dồn lại), trễ dung môi dài (mất pic chất ra sớm), quét từ m/z thấp (nền nước, không khí),
    /// không pha loãng / chia dòng thấp (quá tải cột, detector bão hòa), chưa tune (m/z lệch, độ khớp thấp).
    /// </summary>
    public class GcMsProgram : InstrumentProgram
    {
        public override string Code => "gc_ms";
        static readonly string[] optional = { "day_chuan", "ket_thuc" };
        public override ICollection<string> Optional => optional;

        const float VacuumNeedS = 4f, TuneS = 2f, RunS = 7f;
        public static readonly double[] StdMeOH = { 20, 50, 100, 200, 500 };   // mg/L
        public static readonly double[] StdEtOH = { 1, 2, 3, 4, 5 };           // % thể tích

        public bool Gas { get; private set; }
        public bool MsOn { get; private set; }
        public float Vacuum { get; private set; }
        public bool Tuned { get; private set; }
        float tuneT = -1;
        public readonly GcLibrary.Method M = new GcLibrary.Method();
        public int Dilution { get; private set; } = 1;
        public bool UseStd { get; private set; }
        public int StdIndex { get; private set; }

        float runT = -1;
        string runName = "";
        bool runIsStd;
        int runStd;
        public List<GcLibrary.Peak> Peaks { get; private set; } = new List<GcLibrary.Peak>();
        readonly List<(double t, double y)> tic = new List<(double, double)>();
        public readonly List<(GcLibrary.Peak p, GcLibrary.Entry hit, double match, double ri)> Identified = new List<(GcLibrary.Peak, GcLibrary.Entry, double, double)>();
        readonly List<string> unknown = new List<string>(), nonVolatile = new List<string>();
        public readonly List<(double c, double a)> CalMe = new List<(double, double)>(), CalEt = new List<(double, double)>();
        public double MeOHmgL { get; private set; } = double.NaN;
        public double EthanolPct { get; private set; } = double.NaN;
        int sel;
        int plotMode;  // 0 TIC, 1 phổ khối, 2 đường chuẩn
        readonly System.Random rng = new System.Random(11);

        public bool Running => runT >= 0;
        public void SetPlot(int m) => plotMode = m;
        GcLibrary Lib => InstrumentData.Instance != null ? InstrumentData.Instance.Gc : new GcLibrary();
        bool Ready => Gas && MsOn && Vacuum >= VacuumNeedS;

        Button bGas, bMs, bTune, bT0, bRate, bSplit, bDelay, bScan, bDil, bSrc, bStd, bRun, bPrev, bNext, bPlot, bBake;
        Text status;

        // =====================================================================

        public override void Build()
        {
            bGas = S.AddButton("Mở khí mang He", 0, 0, OpenGas, true);
            bMs = S.AddButton("Bật MS, hút chân không", 1, 0, StartMs, false, 1, 13);
            bTune = S.AddButton("Auto tune (PFTBA)", 2, 0, Tune, false, 1, 13);
            bT0 = S.AddButton("", 0, 1, NextT0);
            bRate = S.AddButton("", 1, 1, NextRate);
            bSplit = S.AddButton("", 2, 1, NextSplit);
            bDelay = S.AddButton("", 0, 2, NextDelay);
            bScan = S.AddButton("", 1, 2, NextScan);
            bDil = S.AddButton("", 2, 2, NextDilution);
            bSrc = S.AddButton("", 0, 3, ToggleSource);
            bStd = S.AddButton("", 1, 3, NextStd, false, 2, 13);
            bRun = S.AddButton("Bơm 1 µL, chạy", 0, 4, StartRun, true, 2);
            bPlot = S.AddButton("", 2, 4, () => plotMode = (plotMode + 1) % 3);
            bPrev = S.AddButton("◀ Pic trước", 0, 5, () => Select(sel - 1));
            bNext = S.AddButton("Pic sau ▶", 1, 5, () => Select(sel + 1));
            bBake = S.AddButton("Nung cột, chờ", 2, 5, Bake);
            status = S.AddText("", 6, 0, 3, 13);
            S.SetResults("Mở khí mang, bật MS và chờ chân không. Đặt ống mẫu vào ổ trên nóc máy ở phòng lab trước khi mở màn hình này.");
        }

        public override void Refresh()
        {
            InstrumentScreen.SetButton(bGas, Gas ? "Khí He: 1,0 mL/phút" : "Mở khí mang He", true, Gas);
            InstrumentScreen.SetButton(bMs, !MsOn ? "Bật MS, hút chân không" : Vacuum < VacuumNeedS ? $"Chân không {Vacuum / VacuumNeedS:P0}" : "MS sẵn sàng", Gas && !MsOn, MsOn && Vacuum >= VacuumNeedS);
            InstrumentScreen.SetButton(bTune, Tuned ? "Đã tune" : tuneT >= 0 ? "Đang tune…" : "Auto tune (PFTBA)", Ready && tuneT < 0, Tuned);
            InstrumentScreen.SetButton(bT0, $"Lò đầu {M.T0:0} °C", !Running);
            InstrumentScreen.SetButton(bRate, $"Tăng {M.Rate:0} °C/phút", !Running);
            InstrumentScreen.SetButton(bSplit, $"Chia dòng {M.Split:0}:1", !Running);
            InstrumentScreen.SetButton(bDelay, $"Trễ dung môi {Fmt(M.SolventDelay, "0.0")} phút", !Running, false);
            InstrumentScreen.SetButton(bScan, $"Quét m/z {M.ScanLo:0}–{M.ScanHi:0}", !Running);
            InstrumentScreen.SetButton(bDil, $"Pha loãng mẫu ×{Dilution}", !Running, Dilution > 1);
            InstrumentScreen.SetButton(bSrc, UseStd ? "Bơm: chuẩn" : "Bơm: mẫu ở ổ", !Running);
            InstrumentScreen.SetButton(bStd, UseStd ? $"Chuẩn {StdIndex + 1}/5: metanol {StdMeOH[StdIndex]:0} mg/L + etanol {StdEtOH[StdIndex]:0}%" : Sample != null ? "Mẫu: " + SampleName : "Ổ mẫu trống", !Running && UseStd, UseStd);
            InstrumentScreen.SetButton(bRun, Running ? $"Đang chạy {RunMinutes:0.0} / {M.RunMin:0} phút" : "Bơm 1 µL, chạy", Ready && !Running && (UseStd || Sample != null));
            InstrumentScreen.SetButton(bPlot, plotMode == 0 ? "Đồ thị: TIC" : plotMode == 1 ? "Đồ thị: phổ khối" : "Đồ thị: đường chuẩn", true);
            InstrumentScreen.SetButton(bPrev, null, Identified.Count > 1);
            InstrumentScreen.SetButton(bNext, null, Identified.Count > 1);
            InstrumentScreen.SetButton(bBake, null, Ready && !Running);
            double T = Running ? GcLibrary.OvenTemp(M, RunMinutes) : M.T0;
            status.text = $"Lò: {T:0} °C · buồng bơm 250 °C · nguồn ion 200 °C · chân không {(MsOn ? Vacuum >= VacuumNeedS ? "cao" : "đang hút" : "tắt")} · {(Tuned ? "đã tune" : "chưa tune")}";
            S.SetResults(BuildResults());
        }

        double RunMinutes => Running ? runT / RunS * M.RunMin : 0;

        // =====================================================================
        // Thao tác
        // =====================================================================

        public void OpenGas()
        {
            if (Gas) return;
            Gas = true;
            S.Done("bat_khi");
            S.Say("Mở bình heli, đặt lưu lượng cột 1,0 mL/phút. Đầu nối kín: nền m/z 28 (N₂) thấp.", 1);
        }

        public void StartMs()
        {
            if (MsOn) return;
            if (!Gas) { S.Say("Phải có khí mang trước khi bật MS (không khí lọt vào cột nóng làm hỏng pha tĩnh, cháy dây tóc)", 3); return; }
            if (!S.Require("chan_khong")) return;
            MsOn = true;
            Vacuum = 0;
            S.Say("Bơm turbo đang hút chân không…", 0);
        }

        public void Tune()
        {
            if (!Ready || tuneT >= 0) return;
            if (!S.Require("tune")) return;
            tuneT = 0;
            S.Say("Auto tune: đưa PFTBA vào nguồn ion, hiệu chỉnh m/z 69, 219, 502 và độ nhạy…", 0);
        }

        public void NextT0() { M.T0 = M.T0 == 35 ? 40 : M.T0 == 40 ? 60 : M.T0 == 60 ? 80 : 35; MethodTouched(); }
        public void NextRate() { M.Rate = M.Rate == 5 ? 10 : M.Rate == 10 ? 20 : 5; MethodTouched(); }
        public void NextSplit() { M.Split = M.Split == 10 ? 50 : M.Split == 50 ? 100 : 10; MethodTouched(); }
        public void NextDelay() { M.SolventDelay = M.SolventDelay == 0 ? 1.0 : M.SolventDelay == 1.0 ? 1.5 : M.SolventDelay == 1.5 ? 3.0 : 0; MethodTouched(); }
        public void NextScan() { M.ScanLo = M.ScanLo == 29 ? 10 : 29; MethodTouched(); }
        public void NextDilution() => Dilution = Dilution == 1 ? 10 : Dilution == 10 ? 100 : 1;
        public void ToggleSource() => UseStd = !UseStd;
        public void NextStd() { UseStd = true; StdIndex = (StdIndex + 1) % 5; }
        public void SelectStd(int i) { UseStd = true; StdIndex = Mathf.Clamp(i, 0, 4); }
        public void SetMethod(double t0, double rate, double split, double delay, double scanLo) { M.T0 = t0; M.Rate = rate; M.Split = split; M.SolventDelay = delay; M.ScanLo = scanLo; MethodTouched(); }
        public void SetDilution(int d) => Dilution = Math.Max(1, d);
        public void UseSample() => UseStd = false;

        void MethodTouched()
        {
            S.Done("phuong_phap");
            if (CalMe.Count > 0) { CalMe.Clear(); CalEt.Clear(); S.Say("Đổi phương pháp: đường chuẩn cũ không còn dùng được, đã xóa.", 2); }
        }

        public void StartRun()
        {
            if (!Ready || Running) return;
            if (!UseStd && Sample == null) return;
            if (!S.Require(UseStd ? "day_chuan" : "chay_mau")) return;
            S.Done("phuong_phap");
            runIsStd = UseStd;
            runStd = StdIndex;
            Mix mix;
            double vol, dil;
            if (UseStd)
            {
                mix = Mix.Of(("hc:467", StdMeOH[StdIndex] / 32040.0), ("C2H5OH", StdEtOH[StdIndex] * 10 * 0.789 / 46.07));
                vol = 1000; dil = 1;
                runName = $"chuẩn {StdIndex + 1}";
            }
            else
            {
                mix = Sample.Total.Mix;
                vol = Sample.VolumeMl;
                dil = Dilution;
                runName = $"{SampleName} (×{Dilution})";
            }
            unknown.Clear();
            nonVolatile.Clear();
            Peaks = Lib.Peaks(mix, vol, M, dil, unknown, nonVolatile);
            Identified.Clear();
            tic.Clear();
            runT = 0;
            plotMode = 0;
            S.Say($"Bơm 1 µL {runName}, chia dòng {M.Split:0}:1. Lò {M.T0:0} °C giữ {M.Hold:0} phút, tăng {M.Rate:0} °C/phút…", 0);
        }

        public void Select(int i)
        {
            if (Identified.Count == 0) return;
            sel = (i % Identified.Count + Identified.Count) % Identified.Count;
            plotMode = 1;
        }

        public void Bake()
        {
            if (!Ready || Running) return;
            if (S.DoneSteps.Contains("nhan_dien")) S.Done("ket_thuc");
            S.Say("Nung cột ở 250 °C trong 5 phút để đẩy hết chất sôi cao; để máy ở chế độ chờ.", 0);
        }

        // =====================================================================
        // Mô phỏng
        // =====================================================================

        public override void Tick(float dt)
        {
            if (MsOn && Vacuum < VacuumNeedS)
            {
                Vacuum += dt;
                if (Vacuum >= VacuumNeedS) { S.Done("chan_khong"); S.Say("Đã đạt chân không cao. Bật dây tóc được.", 1); }
            }
            if (tuneT >= 0)
            {
                tuneT += dt;
                if (tuneT >= TuneS) { tuneT = -1; Tuned = true; S.Done("tune"); S.Say("Tune xong: m/z 69 / 219 / 502 đúng vị trí, độ phân giải đơn vị khối.", 1); }
            }
            if (!Running) return;
            runT += dt;
            double tMax = RunMinutes;
            double from = tic.Count > 0 ? tic[tic.Count - 1].t : 0;
            for (double t = from + 0.004; t <= tMax; t += 0.004) tic.Add((t, Signal(t)));
            if (runT >= RunS) Finish();
        }

        /// <summary>TIC thu được ở thời điểm t: trước trễ dung môi dây tóc tắt (= 0).</summary>
        double Signal(double t)
        {
            if (t < M.SolventDelay) return 0;
            double y = GcLibrary.Tic(Peaks, t) * (Tuned ? 1 : 0.6);
            if (M.ScanLo <= 18)
            {
                double tm = Lib.DeadTime(M);
                y += 4e4 + Math.Min(GcLibrary.SaturationCounts, 1.5e8 * Math.Exp(-0.5 * Math.Pow((t - tm * 1.04) / 0.06, 2)));  // nền khí, nước (dung môi) ra rất sớm
            }
            return y + 300 + Gauss() * 120;
        }

        void Finish()
        {
            runT = -1;
            var lib = Lib;
            foreach (var p in Peaks)
            {
                if (p.Tr < M.SolventDelay) continue;  // ra trong lúc dây tóc còn tắt
                var spec = lib.SpectrumAt(Peaks, p.Tr, M);
                if (!Tuned) spec = spec.ToDictionary(kv => kv.Key + (kv.Key % 3 == 0 ? 1 : 0), kv => kv.Value * 0.8).GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.Sum(x => x.Value));  // m/z lệch khi chưa tune
                var hit = lib.Search(spec, M.ScanLo)[0];
                Identified.Add((p, hit.e, hit.match, lib.RetentionIndex(p.Tr, M)));
            }
            sel = 0;
            int lost = Peaks.Count(p => p.Tr < M.SolventDelay);
            if (runIsStd) RecordStandard();
            else
            {
                S.Done("chay_mau");
                if (Identified.Count > 0) S.Done("nhan_dien");
                Quantify();
            }
            S.Say($"Chạy xong {runName}: {Identified.Count} pic" + (lost > 0 ? $" · <b>{lost} chất ra trước khi hết trễ dung môi ({Fmt(M.SolventDelay, "0.0")} phút) nên mất pic</b>" : ""), lost > 0 ? 2 : 1);
            if (Peaks.Any(p => p.Overloaded)) S.Say("Pic quá tải (doãng, đỉnh phẳng): pha loãng mẫu hoặc tăng tỉ lệ chia dòng.", 2);
            if (M.ScanLo <= 18) S.Say("Quét từ m/z 10: nền nước (18) và không khí (28, 32) rất lớn. Phân tích thường quét từ m/z 29 trở lên.", 2);
            if (M.T0 >= 60 && Identified.Count >= 2 && Identified[1].p.Tr - Identified[0].p.Tr < 0.05) S.Say("Lò bắt đầu nóng: các chất dễ bay hơi ra dồn sát nhau, không tách. Hạ nhiệt độ đầu của lò.", 2);
        }

        void RecordStandard()
        {
            foreach (var (p, hit, _, _) in Identified)
            {
                double a = GcLibrary.MeasuredArea(p);
                if (p.E.Id == "metanol") { CalMe.RemoveAll(c => c.c == StdMeOH[runStd]); CalMe.Add((StdMeOH[runStd], a)); CalMe.Sort(); }
                if (p.E.Id == "etanol") { CalEt.RemoveAll(c => c.c == StdEtOH[runStd]); CalEt.Add((StdEtOH[runStd], a)); CalEt.Sort(); }
            }
            if (CalMe.Count >= 3) S.Done("day_chuan");
            if (StdIndex < 4) StdIndex++;
            plotMode = 2;
        }

        void Quantify()
        {
            MeOHmgL = EthanolPct = double.NaN;
            var me = Identified.FirstOrDefault(x => x.p.E.Id == "metanol");
            var et = Identified.FirstOrDefault(x => x.p.E.Id == "etanol");
            if (CalEt.Count >= 2 && et.p.E != null) EthanolPct = Fit(CalEt).X(GcLibrary.MeasuredArea(et.p)) * Dilution;
            if (CalMe.Count >= 2) MeOHmgL = me.p.E != null ? Fit(CalMe).X(GcLibrary.MeasuredArea(me.p)) * Dilution : 0;
        }

        static UvVisLibrary.Fit Fit(List<(double c, double a)> pts) => UvVisLibrary.Linear(pts.Select(p => (p.c, p.a)).ToList());

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
            sb.Append($"<color=#9FB3C8>Phương pháp:</color> lò {M.T0:0} °C ({M.Hold:0} phút) → {M.Rate:0} °C/phút; chia dòng {M.Split:0}:1; trễ dung môi {Fmt(M.SolventDelay, "0.0")} phút; m/z {M.ScanLo:0}–{M.ScanHi:0}\n");
            if (Running) sb.Append($"\n<color=#61DBFF>Đang chạy {runName}…</color>\n");
            if (!Running && Identified.Count > 0)
            {
                sb.Append($"\n<b>Bảng pic ({runName})</b>\n");
                for (int i = 0; i < Identified.Count; i++)
                {
                    var (p, hit, match, ri) = Identified[i];
                    string mark = i == sel ? "<color=#61DBFF>▶</color> " : "   ";
                    bool riOk = Math.Abs(ri - hit.RI) < 40;
                    sb.Append($"{mark}{Fmt(p.Tr, "0.00")} phút · RI {Fmt(ri, "0")} · <b>{hit.Name}</b> (khớp {match:0}/999{(riOk ? ", RI khớp" : ", <color=#FFC966>RI lệch</color>")})" + (p.Saturated ? " <color=#FFC966>bão hòa</color>" : "") + "\n");
                }
                var s = Identified[Math.Min(sel, Identified.Count - 1)];
                var top = s.hit.Spectrum.OrderByDescending(x => x.i).Take(4).Select(x => $"{x.mz}").ToList();
                sb.Append($"<color=#9FB3C8>Pic chọn: pic cơ bản m/z {s.hit.BasePeak}; mảnh chính {string.Join(", ", top)}; M = {Fmt(s.hit.M, "0")} → ion phân tử m/z {Math.Round(s.hit.M)}{(s.hit.Spectrum.Any(x => x.mz == (int)Math.Round(s.hit.M) && x.i > 30) ? "" : " (rất yếu)")}</color>\n");
            }
            if (CalMe.Count > 0 || CalEt.Count > 0)
            {
                sb.Append($"\n<b>Đường chuẩn</b>: metanol {CalMe.Count} điểm" + (CalMe.Count >= 2 ? $" (R² {Fmt(Fit(CalMe).R2, "0.0000")})" : "") + $"; etanol {CalEt.Count} điểm" + (CalEt.Count >= 2 ? $" (R² {Fmt(Fit(CalEt).R2, "0.0000")})" : "") + "\n");
            }
            if (!double.IsNaN(MeOHmgL) || !double.IsNaN(EthanolPct))
            {
                sb.Append("\n<b>Phân tích chuyên sâu: rượu</b>\n");
                if (!double.IsNaN(EthanolPct)) sb.Append($"Độ cồn: <b>{Fmt(EthanolPct, "0.0")}% thể tích</b> (pha loãng ×{Dilution})\n");
                if (!double.IsNaN(MeOHmgL))
                {
                    if (MeOHmgL < StdMeOH[0] / Dilution * 0.5) sb.Append("Metanol: <b>không phát hiện</b> (dưới chuẩn thấp nhất)\n");
                    else
                    {
                        sb.Append($"Metanol: <b>{Fmt(MeOHmgL, "0")} mg/L</b>\n");
                        if (!double.IsNaN(EthanolPct) && EthanolPct > 1)
                        {
                            double per = MeOHmgL / (EthanolPct / 100);
                            sb.Append($"Quy về cồn 100°: {Fmt(MeOHmgL, "0")} / {Fmt(EthanolPct / 100, "0.000")} = <b>{Fmt(per, "0")} mg/L cồn 100°</b>\n");
                            sb.Append(per > 100 ? $"<color=#FF7A7A>VƯỢT giới hạn 100 mg/L cồn 100° (QCVN 6-3:2010/BYT) khoảng {Fmt(per / 100, "0")} lần: rượu không an toàn, nguy cơ ngộ độc metanol.</color>\n" : "<color=#73F29E>Trong giới hạn 100 mg/L cồn 100°.</color>\n");
                        }
                    }
                }
            }
            if (unknown.Count > 0) sb.Append("\n<color=#FFC966>Chất dễ bay hơi ngoài thư viện (không có phổ, không bịa pic):</color> " + string.Join(", ", unknown.Distinct()) + "\n");
            if (nonVolatile.Count > 0) sb.Append("<color=#FFC966>Chất không bay hơi (ở lại buồng bơm mẫu, làm bẩn liner): " + string.Join(", ", nonVolatile.Distinct()) + "</color>\n");
            return sb.ToString();
        }

        // =====================================================================
        // Sơ đồ
        // =====================================================================

        public override void DrawSchematic(Painter p, float time)
        {
            SchemKit.Begin(p);
            float ph = time * 60;
            double tNow = RunMinutes;
            double T = Running ? GcLibrary.OvenTemp(M, tNow) : M.T0;
            // bình khí
            SchemKit.Box(p, 30, 60, 50, 200, Gas ? new Color(0.2f, 0.3f, 0.45f) : SchemKit.Body);
            p.Disc(55, 270, 14, SchemKit.Edge);
            // buồng bơm mẫu
            var inj = new Vector2(190, 290);
            SchemKit.Pipe(p, new[] { new Vector2(55, 270), new Vector2(55, 310), new Vector2(inj.x - 30, 310) }, Gas, ph);
            SchemKit.Box(p, inj.x - 30, inj.y - 20, 60, 50, new Color(0.45f, 0.2f, 0.15f));
            if (Running && runT < 0.4f) p.FillRect(inj.x - 3, inj.y + 30, 6, 30, new Color(0.9f, 0.9f, 1f));  // kim tiêm
            // lò cột với cột cuộn
            var ovenC = new Vector2(360, 170);
            float heat = Mathf.InverseLerp(30, 250, (float)T);
            SchemKit.Box(p, ovenC.x - 120, ovenC.y - 120, 240, 240, Color.Lerp(new Color(0.12f, 0.17f, 0.25f), new Color(0.35f, 0.15f, 0.1f), heat));
            for (int r = 0; r < 4; r++) p.Ring(ovenC.x, ovenC.y, 40 + r * 18, new Color(0.75f, 0.7f, 0.6f, 0.6f), 2);
            SchemKit.Pipe(p, new[] { new Vector2(inj.x, inj.y - 20), new Vector2(inj.x, ovenC.y), new Vector2(ovenC.x - 94, ovenC.y) }, Gas, ph);
            // chất đang đi trong cột: điểm màu chạy trên các vòng theo tỉ lệ thời gian / thời gian lưu
            if (Running)
            {
                Color[] cols = { new Color(1f, 0.5f, 0.8f), new Color(1f, 0.8f, 0.3f), new Color(0.5f, 1f, 0.6f), new Color(0.6f, 0.7f, 1f) };
                int k = 0;
                foreach (var pk in Peaks)
                {
                    double z = tNow / pk.Tr;
                    if (z <= 1)
                    {
                        float ang = (float)(z * 4 * Mathf.PI * 2);
                        float rad = 94 - (float)z * 54;
                        p.Disc(ovenC.x + Mathf.Cos(ang) * rad, ovenC.y + Mathf.Sin(ang) * rad, 7, cols[k % cols.Length]);
                    }
                    k++;
                }
            }
            // đường dẫn sang MS
            var src = new Vector2(600, 170);
            SchemKit.Pipe(p, new[] { new Vector2(ovenC.x + 40, ovenC.y), new Vector2(ovenC.x + 120, ovenC.y), new Vector2(src.x - 30, src.y) }, Gas, ph, SchemKit.Hot);
            // buồng chân không
            SchemKit.Box(p, src.x - 40, src.y - 90, 380, 180, new Color(0.08f, 0.1f, 0.14f));
            // nguồn ion
            SchemKit.Box(p, src.x - 25, src.y - 40, 60, 80);
            bool fil = Ready && (!Running || tNow >= M.SolventDelay);
            p.Line(src.x - 18, src.y + 30, src.x + 28, src.y + 30, fil ? new Color(1f, 0.85f, 0.4f) : SchemKit.Off, 3);
            if (fil) for (int i = 0; i < 4; i++) p.Dashed(src.x - 10 + i * 12, src.y + 28, src.x - 10 + i * 12, src.y - 20, new Color(0.6f, 0.8f, 1f, 0.8f), 2, 5, ph * 0.5f);
            // tứ cực
            var q = new Vector2(760, 170);
            for (int i = 0; i < 4; i++) p.FillRect(q.x - 80, q.y - 30 + (i % 2) * 48 + (i / 2) * 6, 160, 8, new Color(0.7f, 0.72f, 0.78f));
            if (fil && Ready)
            {
                for (int i = 0; i < 6; i++)
                {
                    float xx = Mathf.Repeat(time * 260 + i * 60, 220);
                    float yy = q.y + Mathf.Sin(xx * 0.08f + i) * 10;
                    p.Disc(q.x - 110 + xx, yy, 3, new Color(0.6f, 0.9f, 1f));
                }
            }
            // detector
            SchemKit.Box(p, 880, src.y - 30, 40, 60, new Color(0.25f, 0.3f, 0.2f));
            double sig = tic.Count > 0 ? tic[tic.Count - 1].y : 0;
            p.FillRect(926, src.y - 30, 8, 60 * Mathf.Clamp01((float)(sig / GcLibrary.SaturationCounts)), SchemKit.Good);
            // bơm chân không
            p.Ring(760, 60, 18, MsOn ? SchemKit.Good : SchemKit.Off, 3);
            if (MsOn) SchemKitSpin(p, new Vector2(760, 60), time);
            var d = Doc;
            S.SchemLabel("binh", d.PartName("binh_khi"), 55, 40, 11);
            S.SchemLabel("inj", d.PartName("buong_bom") + "\n<color=#FF9966>250 °C</color>", inj.x + 110, inj.y + 10, 11);
            S.SchemLabel("lo", d.PartName("lo_cot") + $" · <color=#FF9966>{T:0} °C</color>", ovenC.x, ovenC.y + 135, 12);
            S.SchemLabel("cot", d.PartName("cot"), ovenC.x, ovenC.y - 135, 11);
            S.SchemLabel("src", d.PartName("nguon_ion") + (Ready && Running && tNow < M.SolventDelay ? "\n<color=#FFC966>dây tóc tắt (trễ dung môi)</color>" : ""), src.x, src.y - 110, 11);
            S.SchemLabel("q", d.PartName("tu_cuc"), q.x, q.y + 62, 11);
            S.SchemLabel("det", d.PartName("detector"), 900, src.y - 50, 11);
            S.SchemLabel("bom", d.PartName("bom_chan_khong"), 760, 26, 11);
            S.SchemLabel("trang_thai", !Gas ? "<color=#8899AA>CHƯA CÓ KHÍ MANG</color>" : !MsOn ? "<color=#FFC966>MS TẮT</color>" : Vacuum < VacuumNeedS ? $"<color=#FFC966>ĐANG HÚT CHÂN KHÔNG {Vacuum / VacuumNeedS:P0}</color>" : Running ? $"<color=#61DBFF>ĐANG CHẠY {Fmt(tNow, "0.00")} phút</color>" : "<color=#73F29E>SẴN SÀNG</color>", 360, 330, 14);
        }

        static void SchemKitSpin(Painter p, Vector2 c, float time)
        {
            for (int i = 0; i < 3; i++)
            {
                float a = time * 20 + i * 2.094f;
                p.Line(c.x, c.y, c.x + Mathf.Cos(a) * 15, c.y + Mathf.Sin(a) * 15, SchemKit.Good, 2);
            }
        }

        // =====================================================================
        // Đồ thị
        // =====================================================================

        public override void DrawPlot(Painter p)
        {
            InstrumentScreen.PlotFrame(p);
            if (plotMode == 1 && Identified.Count > 0) DrawSpectrum(p);
            else if (plotMode == 2) DrawCalibration(p);
            else DrawTic(p);
        }

        void DrawTic(Painter p)
        {
            double x1 = M.RunMin;
            if (!Running && Identified.Count > 0) x1 = Math.Min(M.RunMin, Math.Max(3, Identified.Max(x => x.p.Tr) * 1.6 + 0.5));  // phóng to vùng có pic
            double yMax = Math.Max(2e4, tic.Count > 0 ? tic.Where(t => t.t <= x1).Select(t => t.y).DefaultIfEmpty(0).Max() * 1.1 : 2e4);
            S.SetPlotAxes($"Sắc ký đồ ion tổng (TIC) {runName}" + (Peaks.Count > 1 && Peaks.Max(q => q.Height) > 20 * Peaks.Min(q => q.Height) ? " · đường xanh mảnh: phóng đại ×50" : ""), "Thời gian (phút)", "Cường độ (số đếm)", "0", Fmt(x1, "0.#"), "0", Sci(yMax));
            // vùng trễ dung môi
            var sd = InstrumentScreen.Map(p, M.SolventDelay, 0, 0, x1, 0, yMax);
            p.FillRect(0, 0, sd.x, p.H, new Color(1f, 0.8f, 0.4f, 0.07f));
            if (tic.Count > 1)
            {
                var pts = new List<Vector2>();
                for (int i = 0; i < tic.Count && tic[i].t <= x1; i += x1 < 5 ? 1 : 2) pts.Add(InstrumentScreen.Map(p, tic[i].t, tic[i].y, 0, x1, 0, yMax));
                p.Poly(pts, new Color(1f, 0.6f, 0.8f), 2);
                // pic nhỏ cạnh pic rất lớn (metanol cạnh etanol): vẽ thêm đường phóng đại ×50
                if (Peaks.Count > 1 && Peaks.Max(q => q.Height) > 20 * Peaks.Min(q => q.Height))
                {
                    var zoom = new List<Vector2>();
                    for (int i = 0; i < tic.Count && tic[i].t <= x1; i += x1 < 5 ? 1 : 2) zoom.Add(InstrumentScreen.Map(p, tic[i].t, Math.Min(yMax, tic[i].y * 50), 0, x1, 0, yMax));
                    p.Poly(zoom, new Color(0.6f, 0.9f, 1f, 0.55f), 1.5f);
                }
            }
            for (int i = 0; i < Identified.Count; i++)
            {
                var m = InstrumentScreen.Map(p, Identified[i].p.Tr, 0, 0, x1, 0, yMax);
                p.FillRect(m.x - 1, 0, 2, 14, i == sel ? new Color(0.4f, 0.85f, 1f) : new Color(1, 1, 1, 0.5f));
            }
        }

        void DrawSpectrum(Painter p)
        {
            var s = Identified[Math.Min(sel, Identified.Count - 1)];
            var spec = Lib.SpectrumAt(Peaks, s.p.Tr, M);
            double max = spec.Count > 0 ? spec.Values.Max() : 1;
            double x0 = M.ScanLo, x1 = Math.Max(50, Math.Ceiling((s.hit.M + 15) / 10) * 10);
            S.SetPlotAxes($"Phổ khối tại {Fmt(s.p.Tr, "0.00")} phút (trên) so với thư viện NIST: {s.hit.Name} (dưới)", "m/z", "Cường độ tương đối (%)", $"{x0:0}", $"{x1:0}", "−100", "100");
            float mid = p.H / 2f;
            p.FillRect(0, mid, p.W, 1, new Color(1, 1, 1, 0.3f));
            foreach (var kv in spec)
            {
                if (kv.Key > x1) continue;
                var a = InstrumentScreen.Map(p, kv.Key, 0, x0 - 2, x1, -1, 1);
                p.FillRect(a.x - 2, mid, 4, (float)(kv.Value / max) * (p.H / 2f - 6), new Color(1f, 0.6f, 0.8f));
            }
            foreach (var (mz, i) in s.hit.Spectrum)
            {
                if (mz < x0 || mz > x1) continue;
                var a = InstrumentScreen.Map(p, mz, 0, x0 - 2, x1, -1, 1);
                float h = (float)(i / 999.0) * (p.H / 2f - 6);
                p.FillRect(a.x - 2, mid - h, 4, h, new Color(0.45f, 0.86f, 1f));
            }
        }

        void DrawCalibration(Painter p)
        {
            var cal = CalMe.Count > 0 ? CalMe : CalEt;
            bool me = CalMe.Count > 0;
            double cMax = (me ? StdMeOH[4] : StdEtOH[4]) * 1.2, aMax = Math.Max(1, cal.Count > 0 ? cal.Max(c => c.a) * 1.2 : 1);
            S.SetPlotAxes(me ? "Đường chuẩn metanol (diện tích pic)" : "Đường chuẩn etanol", me ? "Metanol (mg/L)" : "Etanol (% thể tích)", "Diện tích pic", "0", Fmt(cMax, "0"), "0", Sci(aMax));
            if (cal.Count >= 2)
            {
                var f = Fit(cal);
                var a0 = InstrumentScreen.Map(p, 0, f.Intercept, 0, cMax, 0, aMax);
                var a1 = InstrumentScreen.Map(p, cMax, f.Intercept + f.Slope * cMax, 0, cMax, 0, aMax);
                p.Line(a0.x, a0.y, a1.x, a1.y, new Color(0.43f, 0.86f, 1f, 0.8f), 2);
            }
            foreach (var c in cal) { var q = InstrumentScreen.Map(p, c.c, c.a, 0, cMax, 0, aMax); p.Disc(q.x, q.y, 6, SchemKit.Good); }
            if (me && !double.IsNaN(MeOHmgL) && Identified.Any(x => x.p.E.Id == "metanol"))
            {
                double a = GcLibrary.MeasuredArea(Identified.First(x => x.p.E.Id == "metanol").p);
                var q = InstrumentScreen.Map(p, MeOHmgL / Dilution, Math.Min(aMax, a), 0, cMax, 0, aMax);
                p.Line(q.x - 8, q.y - 8, q.x + 8, q.y + 8, new Color(1f, 0.45f, 0.45f), 3);
                p.Line(q.x - 8, q.y + 8, q.x + 8, q.y - 8, new Color(1f, 0.45f, 0.45f), 3);
            }
        }
    }
}
