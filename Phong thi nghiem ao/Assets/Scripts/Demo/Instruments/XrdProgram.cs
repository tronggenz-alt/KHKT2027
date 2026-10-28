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
    /// Máy XRD bột để bàn (thông số theo Rigaku MiniFlex 600: Cu, 40 kV, 15 mA, goniomet 150 mm). Giản đồ tính từ thẻ chuẩn của Handbook of
    /// Mineralogy theo thành phần pha thật của chất rắn lấy từ mẫu (lọc kết tủa / cô cạn) hoặc mẫu chuẩn trong hộp mẫu.
    /// Lỗi mô phỏng: không nghiền (cường độ sai do định hướng ưu tiên), không ép phẳng (pic dời góc), cửa mở (khóa liên động không cho phát tia),
    /// quét nhanh (nhiễu). Phân tích chuyên sâu: tìm pha (search-match), tỉ lệ pha, kích thước tinh thể (Scherrer), d theo Bragg.
    /// </summary>
    public class XrdProgram : InstrumentProgram
    {
        public override string Code => "xrd";
        static readonly string[] optional = { "dat_thong_so", "ket_thuc" };
        public override ICollection<string> Optional => optional;

        const float AgingNeedS = 4f;
        const double From2T = 10, To2T = 80, Step = 0.02;
        static readonly string[] speedNames = { "nhanh (10°/phút)", "thường (4°/phút)", "chậm (1°/phút)" };

        public bool Power { get; private set; }
        public float Aging { get; private set; }
        public bool DoorClosed { get; private set; } = true;
        public bool Ground { get; private set; }
        public bool Pressed { get; private set; }
        public bool Loaded { get; private set; }
        public int Speed { get; private set; } = 1;
        public readonly SolidSource Src;
        List<SolidResidue.Phase> solids = new List<SolidResidue.Phase>();
        List<XrdLibrary.Comp> comps = new List<XrdLibrary.Comp>();
        double amorphous;
        readonly List<string> unknownPhases = new List<string>();
        double displacement;
        readonly Dictionary<(string, int), double> orient = new Dictionary<(string, int), double>();

        float scanT = -1;
        string scanName = "";
        public readonly List<(double tt, double y)> Pattern = new List<(double, double)>();
        public List<(double tt, double h, double fwhm)> PeakList { get; private set; }
        public List<(XrdLibrary.PhaseCard card, double fom, double scale)> Matches { get; private set; }
        public double CrystalNm { get; private set; } = double.NaN;
        string sizePhase = "";
        bool showSticks = true;
        readonly System.Random rng = new System.Random(17);

        public bool Ready => Power && Aging >= AgingNeedS;
        public bool Scanning => scanT >= 0;
        XrdLibrary Lib => InstrumentData.Instance != null ? InstrumentData.Instance.Xrd : new XrdLibrary();

        Button bPower, bDoor, bSrc, bTake, bGrind, bPress, bLoad, bSpeed, bScan, bStop, bMatch, bSize, bOff, bSticks;
        Text status;

        public XrdProgram()
        {
            Src = new SolidSource(true,
                ("Chuẩn: halit NaCl tinh khiết", new[] { SolidSource.Pure("NaCl(r)", "NaCl", "natri clorua", 500) }),
                ("Chuẩn: muối ăn giảm natri (NaCl + KCl)", new[] { SolidSource.Pure("NaCl(r)", "NaCl", "natri clorua", 300), SolidSource.Pure("KCl(r)", "KCl", "kali clorua", 200) }),
                ("Chuẩn: đá vôi nghiền (canxit)", new[] { SolidSource.Pure("CaCO3", "CaCO3", "canxi cacbonat", 500) }),
                ("Chuẩn: cát (thạch anh)", new[] { SolidSource.Pure("SiO2(s)", "SiO2", "silic đioxit", 500) }),
                ("Chuẩn: thạch cao", new[] { SolidSource.Pure("CaSO4", "CaSO4·2H2O", "thạch cao", 500) }));
        }

        public override void Build()
        {
            bPower = S.AddButton("Bật máy", 0, 0, TogglePower, true);
            bDoor = S.AddButton("", 1, 0, ToggleDoor);
            bSticks = S.AddButton("", 2, 0, () => showSticks = !showSticks, false, 1, 13);
            bSrc = S.AddButton("", 0, 1, NextSource, false, 2, 13);
            bTake = S.AddButton("", 2, 1, NextTake, false, 1, 13);
            bGrind = S.AddButton("Nghiền mịn (cối mã não)", 0, 2, Grind, false, 1, 12);
            bPress = S.AddButton("Ép phẳng vào giá", 1, 2, Press, false, 1, 13);
            bLoad = S.AddButton("Đặt giá mẫu vào máy", 2, 2, Load, false, 1, 12);
            bSpeed = S.AddButton("", 0, 3, NextSpeed, false, 1, 12);
            bScan = S.AddButton("Quét 10 – 80°", 1, 3, StartScan, true);
            bStop = S.AddButton("Dừng", 2, 3, () => { if (Scanning) FinishScan(); });
            bMatch = S.AddButton("Tìm pha (search-match)", 0, 4, MatchPhases, false, 1, 12);
            bSize = S.AddButton("Kích thước tinh thể", 1, 4, Size, false, 1, 13);
            bOff = S.AddButton("Hạ cao áp, lấy mẫu", 2, 4, Finish, false, 1, 12);
            status = S.AddText("", 5, 0, 3, 13);
            S.SetResults("Bật máy, làm ấm ống tia X. Chọn nguồn mẫu: ống mẫu ở ổ máy (lọc kết tủa / cô cạn) hoặc mẫu chuẩn trong hộp mẫu.");
        }

        public override void Refresh()
        {
            InstrumentScreen.SetButton(bPower, !Power ? "Bật máy" : Aging < AgingNeedS ? $"Làm ấm ống {Aging / AgingNeedS:P0}" : "40 kV · 15 mA", !Power, Ready);
            InstrumentScreen.SetButton(bDoor, DoorClosed ? "Cửa: đóng" : "Cửa: MỞ", !Scanning, DoorClosed);
            InstrumentScreen.SetButton(bSticks, showSticks ? "Vạch thẻ chuẩn: hiện" : "Vạch thẻ chuẩn: ẩn", true, showSticks);
            InstrumentScreen.SetButton(bSrc, "Mẫu: " + Src.Current?.Name, !Scanning);
            InstrumentScreen.SetButton(bTake, Src.FromTube ? $"Lấy {Src.TakeMl:0} mL" : "—", !Scanning && Src.FromTube);
            InstrumentScreen.SetButton(bGrind, Ground ? "Đã nghiền mịn" : "Nghiền mịn (cối mã não)", !Scanning, Ground);
            InstrumentScreen.SetButton(bPress, Pressed ? "Mặt mẫu đã phẳng" : "Ép phẳng vào giá", !Scanning, Pressed);
            InstrumentScreen.SetButton(bLoad, Loaded ? "Giá mẫu đã đặt" : "Đặt giá mẫu vào máy", !Scanning && !DoorClosed, Loaded);
            InstrumentScreen.SetButton(bSpeed, "Tốc độ: " + speedNames[Speed], !Scanning);
            InstrumentScreen.SetButton(bScan, Scanning ? $"Đang quét {CurrentAngle:0.0}°" : "Quét 10 – 80°", Ready && !Scanning && Loaded);
            InstrumentScreen.SetButton(bStop, null, Scanning);
            InstrumentScreen.SetButton(bMatch, null, Pattern.Count > 100 && !Scanning);
            InstrumentScreen.SetButton(bSize, null, Matches != null && !Scanning);
            InstrumentScreen.SetButton(bOff, null, Ready && !Scanning);
            status.text = $"Ống Cu Kα λ = 1,5406 Å · màn chắn tia X: {(Scanning ? "MỞ" : "đóng")} · khóa liên động: {(DoorClosed ? "cửa đóng" : "<color=#FFC966>cửa mở</color>")}";
            S.SetResults(BuildResults());
        }

        double ScanSeconds => Speed == 0 ? 3 : Speed == 1 ? 5 : 8;
        double CurrentAngle => From2T + (To2T - From2T) * Mathf.Clamp01(scanT / (float)ScanSeconds);

        // =====================================================================
        // Thao tác
        // =====================================================================

        public void TogglePower()
        {
            if (Power) return;
            Power = true;
            Aging = 0;
            S.Say("Bật nước làm mát, nâng dần cao áp và dòng ống tia X (làm ấm)…", 0);
        }

        public void ToggleDoor()
        {
            if (Scanning) return;
            DoorClosed = !DoorClosed;
        }

        public void NextSource() { Src.Next(); Invalidate(); }
        public void SelectSource(string prefix) { Src.Select(prefix); Invalidate(); }
        public void NextTake() { Src.TakeMl = Src.TakeMl == 10 ? 2 : Src.TakeMl == 2 ? 5 : 10; Invalidate(); }

        void Invalidate()
        {
            Ground = Pressed = Loaded = false;
        }

        public void Grind()
        {
            if (!S.Require("chuan_bi_mau")) return;
            Ground = true;
            if (Pressed) S.Done("chuan_bi_mau");
            S.Say("Nghiền mịn mẫu (cỡ hạt dưới 10 µm) trong cối mã não.", 0);
        }

        public void Press()
        {
            if (!S.Require("chuan_bi_mau")) return;
            Pressed = true;
            S.Say("Ép bột vào giá mẫu bằng lam kính: mặt mẫu phẳng, ngang mặt giá.", 0);
            if (Ground) S.Done("chuan_bi_mau");
        }

        public void Load()
        {
            if (DoorClosed) { S.Say("Mở cửa che chắn trước khi đặt giá mẫu", 2); return; }
            if (!S.Require("dat_mau")) return;
            solids = Src.Solids(Sample);
            unknownPhases.Clear();
            comps = Lib.Compose(solids, out amorphous, unknownPhases);
            double total = SolidResidue.TotalMg(solids);
            if (total < 5) { S.Say(total <= 0 ? "Không thu được chất rắn nào từ mẫu (không có kết tủa / chất tan không bay hơi)" : $"Chỉ thu được {Fmt(total, "0.0")} mg chất rắn: quá ít để phủ giá mẫu. Lấy nhiều mẫu hơn.", 2); return; }
            Loaded = true;
            displacement = Pressed ? 0 : 0.25;
            orient.Clear();
            scanName = Src.Describe(Sample);
            S.Done("dat_mau");
            S.Say($"Đã đặt {Fmt(total, "0")} mg chất rắn lên giá. Đóng cửa trước khi quét.", 0);
        }

        public void NextSpeed() { Speed = (Speed + 1) % 3; S.Done("dat_thong_so"); }
        public void SetSpeed(int s) { Speed = Mathf.Clamp(s, 0, 2); S.Done("dat_thong_so"); }
        public void CloseDoor() => DoorClosed = true;
        public void OpenDoor() => DoorClosed = false;

        public void StartScan()
        {
            if (!Ready || Scanning) return;
            if (!DoorClosed) { S.Say("Cửa đang mở: khóa liên động không cho mở màn chắn tia X. Đóng cửa.", 3); return; }
            if (!Loaded) { S.Say("Chưa có mẫu trên giá", 2); return; }
            if (!S.Require("quet")) return;
            Pattern.Clear();
            PeakList = null;
            Matches = null;
            CrystalNm = double.NaN;
            scanT = 0;
            S.Say($"Quét 2θ từ {From2T:0}° đến {To2T:0}°, bước {Step:0.00}°, tốc độ {speedNames[Speed]}…", 0);
        }

        double Orient(XrdLibrary.PhaseCard c, int i)
        {
            if (Ground) return 1;
            if (!orient.TryGetValue((c.Id, i), out var f)) { f = i == 0 ? 0.35 + rng.NextDouble() * 0.3 : 0.5 + rng.NextDouble() * 1.8; orient[(c.Id, i)] = f; }
            return f;
        }

        public override void Tick(float dt)
        {
            if (Power && Aging < AgingNeedS)
            {
                Aging += dt;
                if (Aging >= AgingNeedS) { S.Done("bat_may"); S.Say("Ống tia X đã ổn định ở 40 kV, 15 mA.", 1); }
            }
            if (!Scanning) return;
            scanT += dt;
            double upto = CurrentAngle;
            double next = Pattern.Count > 0 ? Pattern[Pattern.Count - 1].tt + Step : From2T;
            double scale = 40000 * (Speed == 0 ? 0.25 : Speed == 1 ? 0.6 : 2.4);  // quét chậm: đếm lâu hơn ở mỗi bước
            for (double tt = next; tt <= upto + 1e-9 && tt <= To2T + 1e-9; tt += Step)
            {
                double mean = Lib.Intensity(comps, amorphous, tt, scale, displacement, Orient);
                Pattern.Add((tt, Math.Max(0, mean + Math.Sqrt(Math.Max(1, mean)) * Gauss())));
            }
            if (scanT >= ScanSeconds) FinishScan();
        }

        void FinishScan()
        {
            scanT = -1;
            S.Done("quet");
            S.Say($"Quét xong: {Pattern.Count} điểm. Bấm Tìm pha để so thẻ chuẩn.", 1);
            if (Speed == 0) S.Say("Quét nhanh: số đếm ít, nhiễu lớn; pic yếu có thể lẫn vào nền.", 2);
        }

        public void MatchPhases()
        {
            if (Pattern.Count < 100) return;
            double bgLevel = Pattern.Select(p => p.y).OrderBy(v => v).Skip(Pattern.Count / 4).First();
            PeakList = XrdLibrary.Peaks(Pattern, Math.Max(80, 6 * Math.Sqrt(bgLevel)));
            Matches = Lib.Match(PeakList, displacement > 0 ? 0.35 : 0.2).Where(m => m.fom >= 0.75).ToList();
            S.Done("xu_ly");
            if (Matches.Count == 0) S.Say(PeakList.Count == 0 || amorphous > 0.5 ? "Không có pic nhọn của pha tinh thể, chỉ có bướu rộng: mẫu chủ yếu vô định hình (hoặc quá ít mẫu)." : "Có pic nhưng không khớp thẻ chuẩn nào trong thư viện (pha ngoài thư viện).", 2);
            else S.Say($"Tìm thấy {Matches.Count} pha: " + string.Join(", ", Matches.Select(m => $"{m.card.Name} (độ khớp {m.fom:P0})")), 1);
            if (!Pressed && Matches.Count > 0) S.Say("Pic lệch khỏi vị trí thẻ chuẩn khoảng 0,2°: mặt mẫu không ngang mặt giá. Ép phẳng mẫu rồi quét lại.", 2);
            if (!Ground && Matches.Count > 0) S.Say("Tỉ lệ cường độ các pic khác thẻ chuẩn: mẫu chưa nghiền mịn (định hướng ưu tiên).", 2);
        }

        public void Size()
        {
            if (Matches == null || Matches.Count == 0 || PeakList == null) return;
            var m = Matches[0];
            double tt0 = Lib.TwoTheta(m.card.Lines[0].d);
            var p = PeakList.OrderBy(q => Math.Abs(q.tt - tt0)).FirstOrDefault();
            if (p.h <= 0) return;
            CrystalNm = Lib.Scherrer(p.tt, p.fwhm);
            sizePhase = m.card.Name;
            S.Say(double.IsInfinity(CrystalNm) ? "Pic hẹp bằng độ rộng của máy: tinh thể lớn hơn khoảng 200 nm (Scherrer không đo được)." : $"Scherrer ({m.card.Name}, 2θ = {Fmt(p.tt, "0.00")}°, FWHM {Fmt(p.fwhm, "0.000")}°): L ≈ {Fmt(CrystalNm, "0")} nm", 1);
        }

        public void Finish()
        {
            if (!Ready || Scanning) return;
            if (S.DoneSteps.Contains("xu_ly")) S.Done("ket_thuc");
            S.Say("Hạ cao áp về mức chờ, mở cửa lấy giá mẫu.", 0);
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
            sb.Append($"<color=#9FB3C8>Mẫu:</color> {Src.Describe(Sample)}\n");
            if (Loaded)
            {
                sb.Append($"<color=#9FB3C8>Chất rắn trên giá ({Fmt(SolidResidue.TotalMg(solids), "0")} mg):</color> {string.Join(", ", solids.Select(s => s.Name))}\n");
                if (Src.Notes.Count > 0) sb.Append($"<color=#9FB3C8>{string.Join("; ", Src.Notes.Distinct())}</color>\n");
            }
            if (PeakList != null && PeakList.Count > 0)
            {
                sb.Append("\n<b>Pic mạnh (Bragg: d = λ / 2sinθ)</b>\n");
                foreach (var p in PeakList.OrderByDescending(q => q.h).Take(5).OrderBy(q => q.tt))
                    sb.Append($"2θ = {Fmt(p.tt, "0.00")}° → d = {Fmt(Lib.D(p.tt), "0.000")} Å · cao {Fmt(p.h, "0")}\n");
            }
            if (Matches != null && Matches.Count > 0)
            {
                sb.Append("\n<b>Phân tích chuyên sâu: pha tinh thể</b>\n");
                double sumScale = Matches.Sum(m => m.scale);
                foreach (var m in Matches)
                    sb.Append($"• <color=#61DBFF>{m.card.Name}</color> · {m.card.Cell} · độ khớp {m.fom:P0} · ≈ {m.scale / Math.Max(1e-9, sumScale):P0} khối lượng (bán định lượng)\n");
                sb.Append("<color=#9FB3C8>Tỉ lệ pha ước lượng theo cường độ pic mạnh nhất, coi hệ số RIR các pha bằng nhau (mô phỏng)</color>\n");
            }
            if (!double.IsNaN(CrystalNm)) sb.Append(double.IsInfinity(CrystalNm) ? $"\nKích thước tinh thể {sizePhase}: > 200 nm (pic hẹp)\n" : $"\nKích thước tinh thể {sizePhase} (Scherrer): <b>≈ {Fmt(CrystalNm, "0")} nm</b>\n");
            if (Loaded && amorphous > 0.05) sb.Append($"\n<color=#FFC966>Phần vô định hình ≈ {amorphous:P0}: chỉ cho bướu rộng quanh 20 – 25°, XRD không nhận diện được (dùng phương pháp khác: IR, HPLC…)</color>\n");
            if (unknownPhases.Count > 0) sb.Append($"\n<color=#FFC966>Pha tinh thể chưa có thẻ chuẩn trong thư viện (không bịa pic):</color> {string.Join(", ", unknownPhases.Distinct())}\n");
            return sb.ToString();
        }

        // =====================================================================
        // Sơ đồ: goniomet θ/2θ
        // =====================================================================

        public override void DrawSchematic(Painter p, float time)
        {
            SchemKit.Begin(p);
            var c = new Vector2(330, 120);
            float R = 190;
            double tt = Scanning ? CurrentAngle : 30;
            float th = (float)(tt / 2 * Mathf.Deg2Rad);
            p.Ring(c.x, c.y, R, new Color(1, 1, 1, 0.1f), 2);
            // mẫu nằm ngang ở tâm
            p.FillRect(c.x - 50, c.y - 6, 100, 12, new Color(0.85f, 0.85f, 0.8f));
            p.FillRect(c.x - 40, c.y + 6, 80, 4, Loaded ? new Color(0.95f, 0.95f, 0.9f) : SchemKit.Off);
            var tube = c + new Vector2(-Mathf.Cos(th), Mathf.Sin(th)) * R;
            var det = c + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * R;
            SchemKit.Box(p, tube.x - 26, tube.y - 18, 52, 36, new Color(0.3f, 0.32f, 0.4f));
            SchemKit.Box(p, det.x - 20, det.y - 16, 40, 32, new Color(0.25f, 0.3f, 0.25f));
            bool on = Ready && Scanning;
            Color xray = new Color(0.75f, 0.6f, 1f, 0.9f);
            if (on)
            {
                p.Dashed(tube.x, tube.y, c.x, c.y + 8, xray, 3, 9, time * 80);
                p.Dashed(c.x, c.y + 8, det.x, det.y, xray, 3, 9, time * 80);
            }
            // cửa che chắn
            p.Frame(30, 10, 600, 300, DoorClosed ? new Color(0.45f, 0.95f, 0.62f, 0.5f) : new Color(1f, 0.6f, 0.3f, 0.7f), 3);
            // khung minh họa định luật Bragg
            var b = new Vector2(780, 170);
            SchemKit.Box(p, b.x - 160, b.y - 140, 320, 290, new Color(0.08f, 0.1f, 0.14f));
            for (int i = 0; i < 4; i++)
                for (int k = 0; k < 9; k++) p.Disc(b.x - 120 + k * 30, b.y - 80 + i * 40, 6, new Color(0.6f, 0.75f, 0.95f));
            float a = 25 * Mathf.Deg2Rad;
            for (int i = 0; i < 2; i++)
            {
                var hit = new Vector2(b.x - 15 + i * 30, b.y + 40 - i * 40);
                p.Line(hit.x - Mathf.Cos(a) * 140, hit.y + Mathf.Sin(a) * 140, hit.x, hit.y, xray, 2);
                p.Line(hit.x, hit.y, hit.x + Mathf.Cos(a) * 140, hit.y + Mathf.Sin(a) * 140, xray, 2);
            }
            var d = Doc;
            S.SchemLabel("ong", d.PartName("ong_tia_x") + (on ? "\n<color=#C9A6FF>40 kV · 15 mA</color>" : ""), tube.x - 10, tube.y + 40, 11);
            S.SchemLabel("det", d.PartName("detector") + $"\n<color=#61DBFF>2θ = {tt:0.0}°</color>", det.x + 10, det.y + 40, 11);
            S.SchemLabel("mau", d.PartName("gia_mau"), c.x, c.y - 26, 11);
            S.SchemLabel("cua", d.PartName("cua_khoa") + (DoorClosed ? " · <color=#73F29E>đóng</color>" : " · <color=#FFC966>MỞ</color>"), 330, 24, 11);
            S.SchemLabel("bragg", "nλ = 2d·sinθ: tia phản xạ trên hai lớp\nnguyên tử lệch nhau 2d·sinθ, cùng pha thì có pic", b.x, b.y + 128, 11);
            S.SchemLabel("trang_thai", !Power ? "<color=#8899AA>MÁY TẮT</color>" : Aging < AgingNeedS ? $"<color=#FFC966>ĐANG LÀM ẤM ỐNG {Aging / AgingNeedS:P0}</color>" : Scanning ? "<color=#61DBFF>ĐANG QUÉT</color>" : "<color=#73F29E>SẴN SÀNG</color>", 330, 334, 14);
        }

        // =====================================================================
        // Đồ thị
        // =====================================================================

        public override void DrawPlot(Painter p)
        {
            InstrumentScreen.PlotFrame(p);
            double yMax = Math.Max(500, Pattern.Count > 0 ? Pattern.Max(q => q.y) * 1.1 : 500);
            S.SetPlotAxes($"Giản đồ nhiễu xạ tia X · {scanName}", "2θ (độ), Cu Kα", "Cường độ (số đếm)", "10", "80", "0", Fmt(yMax, "0"));
            if (showSticks && Matches != null)
            {
                Color[] cols = { new Color(1f, 0.6f, 0.9f, 0.85f), new Color(0.5f, 1f, 0.6f, 0.85f), new Color(1f, 0.8f, 0.3f, 0.85f) };
                int k = 0;
                foreach (var m in Matches)
                {
                    var col = cols[k++ % cols.Length];
                    foreach (var (d, I) in m.card.Lines)
                    {
                        double tt = Lib.TwoTheta(d);
                        if (double.IsNaN(tt) || tt < From2T || tt > To2T) continue;
                        var q = InstrumentScreen.Map(p, tt, 0, From2T, To2T, 0, yMax);
                        p.FillRect(q.x - 1, 0, 3, (float)(I / 100.0) * p.H * 0.25f, col);
                    }
                }
            }
            if (Pattern.Count > 1)
            {
                var pts = new List<Vector2>(Pattern.Count / 2 + 1);
                for (int i = 0; i < Pattern.Count; i += 2) pts.Add(InstrumentScreen.Map(p, Pattern[i].tt, Pattern[i].y, From2T, To2T, 0, yMax));
                p.Poly(pts, new Color(0.45f, 0.86f, 1f), 1.5f);
            }
            if (PeakList != null)
                foreach (var pk in PeakList.OrderByDescending(q => q.h).Take(8))
                {
                    var q = InstrumentScreen.Map(p, pk.tt, pk.h, From2T, To2T, 0, yMax);
                    p.Disc(q.x, Math.Min(p.H - 6, q.y + 8), 3, new Color(1f, 0.45f, 0.45f));
                }
        }
    }
}
