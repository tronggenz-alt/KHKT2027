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
    /// SEM để bàn + EDS (thông số theo Hitachi TM4000Plus II: 5 – 20 kV, 10× – 100 000×). Ảnh vẽ theo hình dạng, cỡ hạt từng pha của chất rắn
    /// lấy từ mẫu (lập phương NaCl, mặt thoi canxit, lăng trụ thạch cao, hạt mịn BaSO₄ / AgCl, mảnh vỡ thạch anh, khối vô định hình).
    /// Phổ EDS theo năng lượng vạch tia X của X-Ray Data Booklet. Lỗi mô phỏng: không phủ vàng (ảnh tích điện), chưa lấy nét (mờ),
    /// quét nhanh (nhiễu), 5 kV (không kích thích vạch K của nguyên tố nặng), gán nhầm pic Au / C.
    /// </summary>
    public class SemProgram : InstrumentProgram
    {
        public override string Code => "sem";
        static readonly string[] optional = { "phu_vang", "ket_thuc" };
        public override ICollection<string> Optional => optional;

        const float PumpNeedS = 4f;
        static readonly int[] kvs = { 5, 10, 15, 20 };
        static readonly int[] mags = { 100, 300, 1000, 3000, 10000, 30000 };

        public bool Mounted { get; private set; }
        public bool Coated { get; private set; }
        public float Vacuum { get; private set; } = -1;
        public bool Beam { get; private set; }
        public int KV { get; private set; } = 15;
        public int Mag { get; private set; } = 1000;
        public bool Bse { get; private set; } = true;
        public bool SlowScan { get; private set; } = true;
        public double Focus { get; private set; } = 4.0;   // độ lệch tiêu cự (đơn vị tùy ý); 0 = nét
        public readonly SolidSource Src;
        List<SolidResidue.Phase> solids = new List<SolidResidue.Phase>();
        public double[] EdsSpectrum { get; private set; }
        public Dictionary<string, double> EdsQuant { get; private set; }
        bool skipCoat = true;
        int plotMode;  // 0 ảnh, 1 phổ EDS
        Color32[] imageCache;
        string imageKey = "";
        readonly System.Random rng = new System.Random(23);

        public bool VacuumOk => Vacuum >= PumpNeedS;
        public void SetPlot(int m) => plotMode = m;
        public bool Imaging => Beam && VacuumOk && Mounted;
        XrdLibrary Xrd => InstrumentData.Instance != null ? InstrumentData.Instance.Xrd : new XrdLibrary();
        EdsLibrary Eds => InstrumentData.Instance != null ? InstrumentData.Instance.Eds : new EdsLibrary();

        Button bSrc, bMount, bCoat, bPump, bBeam, bKv, bMagDown, bMag, bMagUp, bFocus, bFocusM, bFocusP, bDet, bScan, bEds, bPlot, bSkip, bOff;
        Text status;

        public SemProgram()
        {
            Src = new SolidSource(true,
                ("Chuẩn: tinh thể muối ăn (NaCl)", new[] { SolidSource.Pure("NaCl(r)", "NaCl", "natri clorua", 50) }),
                ("Chuẩn: bột đá vôi (canxit)", new[] { SolidSource.Pure("CaCO3", "CaCO3", "canxi cacbonat", 50) }),
                ("Chuẩn: cát (thạch anh)", new[] { SolidSource.Pure("SiO2(s)", "SiO2", "silic đioxit", 50) }),
                ("Chuẩn: thạch cao", new[] { SolidSource.Pure("CaSO4", "CaSO4·2H2O", "thạch cao", 50) }));
        }

        public override void Build()
        {
            bSrc = S.AddButton("", 0, 0, () => { Src.Next(); Mounted = false; Coated = false; imageKey = ""; }, false, 2, 13);
            bMount = S.AddButton("Gắn mẫu lên đế", 2, 0, Mount, false, 1, 13);
            bCoat = S.AddButton("Phủ vàng", 0, 1, Coat);
            bPump = S.AddButton("Hút chân không", 1, 1, Pump, false, 1, 13);
            bBeam = S.AddButton("Bật chùm electron", 2, 1, ToggleBeam, true, 1, 13);
            bKv = S.AddButton("", 0, 2, NextKv);
            bDet = S.AddButton("", 1, 2, () => { Bse = !Bse; S.Done("lay_net"); });
            bScan = S.AddButton("", 2, 2, () => SlowScan = !SlowScan, false, 1, 13);
            bMagDown = S.AddButton("Phóng đại −", 0, 3, () => SetMag(Prev(Mag)));
            bMag = S.AddButton("", 1, 3, null);
            bMagUp = S.AddButton("Phóng đại +", 2, 3, () => SetMag(Next(Mag)));
            bFocusM = S.AddButton("Tiêu cự −", 0, 4, () => { Focus -= 1; });
            bFocus = S.AddButton("Lấy nét tự động", 1, 4, AutoFocus, false, 1, 13);
            bFocusP = S.AddButton("Tiêu cự +", 2, 4, () => { Focus += 1; });
            bEds = S.AddButton("Thu phổ EDS", 0, 5, RunEds);
            bSkip = S.AddButton("", 1, 5, () => { skipCoat = !skipCoat; if (EdsSpectrum != null) EdsQuant = Eds.Quantify(EdsSpectrum, KV, skipCoat ? new[] { "Au", "C" } : null); }, false, 1, 12);
            bPlot = S.AddButton("", 2, 5, () => plotMode = 1 - plotMode);
            bOff = S.AddButton("Tắt chùm, xả buồng mẫu", 0, 6, Finish, false, 2, 13);
            status = S.AddText("", 7, 0, 3, 13);
            S.SetResults("Chuẩn bị mẫu rắn (lọc / cô cạn ống mẫu ở ổ máy, hoặc mẫu chuẩn), gắn lên đế, hút chân không rồi bật chùm electron.");
        }

        static int Prev(int m) { int i = Array.IndexOf(mags, m); return mags[Math.Max(0, i - 1)]; }
        static int Next(int m) { int i = Array.IndexOf(mags, m); return mags[Math.Min(mags.Length - 1, i + 1)]; }

        public override void Refresh()
        {
            InstrumentScreen.SetButton(bSrc, "Mẫu: " + Src.Current?.Name, !Beam);
            InstrumentScreen.SetButton(bMount, Mounted ? "Mẫu đã gắn" : "Gắn mẫu lên đế", !Beam && !(Vacuum >= 0), Mounted);
            InstrumentScreen.SetButton(bCoat, Coated ? "Đã phủ vàng" : "Phủ vàng", Mounted && Vacuum < 0, Coated);
            InstrumentScreen.SetButton(bPump, Vacuum < 0 ? "Hút chân không" : VacuumOk ? "Chân không: đạt" : $"Đang hút {Vacuum / PumpNeedS:P0}", Mounted && Vacuum < 0, VacuumOk);
            InstrumentScreen.SetButton(bBeam, Beam ? "Chùm electron: BẬT" : "Bật chùm electron", VacuumOk, Beam);
            InstrumentScreen.SetButton(bKv, $"Điện áp {KV} kV", true);
            InstrumentScreen.SetButton(bDet, Bse ? "Detector: BSE" : "Detector: SE", true);
            InstrumentScreen.SetButton(bScan, SlowScan ? "Quét: chậm (ít nhiễu)" : "Quét: nhanh", true);
            InstrumentScreen.SetButton(bMag, $"×{Mag:N0}".Replace(",", "."), false, true);
            InstrumentScreen.SetButton(bFocus, Math.Abs(Focus) < 0.5 ? "Ảnh đã nét" : "Lấy nét tự động", Imaging, Math.Abs(Focus) < 0.5);
            InstrumentScreen.SetButton(bEds, "Thu phổ EDS", Imaging);
            InstrumentScreen.SetButton(bSkip, skipCoat ? "Bỏ Au, C khi tính %" : "Tính cả Au, C", EdsSpectrum != null, skipCoat);
            InstrumentScreen.SetButton(bPlot, plotMode == 0 ? "Hiện: ảnh SEM" : "Hiện: phổ EDS", true);
            InstrumentScreen.SetButton(bOff, null, Beam || Vacuum >= 0);
            status.text = $"Chân không: {(Vacuum < 0 ? "buồng mẫu mở" : VacuumOk ? "đạt" : "đang hút")} · {KV} kV · ×{Mag} · vùng quét {Fmt(FieldUm, "0.#")} µm";
            S.SetResults(BuildResults());
        }

        double FieldUm => 127000.0 / Mag;

        // =====================================================================
        // Thao tác
        // =====================================================================

        public void SelectSource(string prefix) { Src.Select(prefix); Mounted = false; Coated = false; imageKey = ""; }

        public void Mount()
        {
            if (Beam || Vacuum >= 0) return;
            if (!S.Require("chuan_bi_mau")) return;
            solids = Src.Solids(Sample);
            if (SolidResidue.TotalMg(solids) < 0.5) { S.Say("Không thu được chất rắn từ mẫu (không có kết tủa / chất tan không bay hơi).", 2); return; }
            Mounted = true;
            Coated = false;
            imageKey = "";
            S.Done("chuan_bi_mau");
            S.Say($"Rắc chất rắn đã sấy khô lên băng dính cacbon trên đế nhôm ({string.Join(", ", solids.Select(s => s.Name))}).", 0);
        }

        public void Coat()
        {
            if (!Mounted || Vacuum >= 0) return;
            Coated = true;
            imageKey = "";
            S.Done("phu_vang");
            S.Say("Phủ lớp vàng dày khoảng 5 nm bằng máy phún xạ: bề mặt dẫn điện.", 1);
        }

        public void Pump()
        {
            if (!Mounted || Vacuum >= 0) return;
            if (!S.Require("hut_chan_khong")) return;
            Vacuum = 0;
            S.Say("Đóng buồng mẫu, bơm đang hút chân không…", 0);
        }

        public void ToggleBeam()
        {
            if (!VacuumOk) { S.Say("Chưa đạt chân không: không bật được sợi đốt", 2); return; }
            if (Beam) { Beam = false; return; }
            if (!S.Require("bat_chum")) return;
            Beam = true;
            S.Done("bat_chum");
            S.Say($"Bật sợi đốt W, tăng tốc {KV} kV.", 0);
        }

        public void NextKv() { KV = kvs[(Array.IndexOf(kvs, KV) + 1) % kvs.Length]; imageKey = ""; }
        public void SetKv(int kv) { KV = kv; imageKey = ""; }
        public void SetMag(int m) { Mag = m; Focus += 0.6 * (rng.NextDouble() - 0.5); imageKey = ""; }

        public void AutoFocus()
        {
            if (!Imaging) return;
            if (!S.Require("lay_net")) return;
            Focus = 0;
            S.Done("lay_net");
            S.Done("chup_anh");
            S.Say("Lấy nét tự động: chỉnh dòng vật kính đến khi cạnh hạt sắc nhất.", 1);
        }

        public void Capture()
        {
            if (!Imaging) return;
            S.Done("chup_anh");
        }

        public void RunEds()
        {
            if (!Imaging) return;
            if (!S.Require("eds")) return;
            S.Done("chup_anh");
            var fr = SolidResidue.AtomFractions(solids);
            EdsSpectrum = Eds.Spectrum(fr, KV, Coated, true, 2.5e5, rng);
            EdsQuant = Eds.Quantify(EdsSpectrum, KV, skipCoat ? new[] { "Au", "C" } : null);
            plotMode = 1;
            S.Done("eds");
            var heavy = fr.Keys.Where(el => Eds.Get(el) != null && EdsLibrary.Overvoltage(KV, Eds.Get(el).KeV) <= 0).ToList();
            S.Say(heavy.Count > 0 ? $"Ở {KV} kV không kích thích được vạch của {string.Join(", ", heavy)}: tăng điện áp (≥ 2 lần năng lượng vạch)." : $"Thu phổ EDS ở {KV} kV: {string.Join(", ", EdsQuant.OrderByDescending(kv => kv.Value).Select(kv => kv.Key))}", heavy.Count > 0 ? 2 : 1);
        }

        public void Finish()
        {
            Beam = false;
            Vacuum = -1;
            Mounted = false;
            if (S.DoneSteps.Contains("eds")) S.Done("ket_thuc");
            S.Say("Tắt sợi đốt, chờ nguội, xả khí buồng mẫu, lấy đế mẫu ra.", 0);
        }

        public override void Tick(float dt)
        {
            if (Vacuum >= 0 && Vacuum < PumpNeedS)
            {
                Vacuum += dt;
                if (Vacuum >= PumpNeedS) { S.Done("hut_chan_khong"); S.Say("Đã đạt chân không.", 1); }
            }
        }

        // =====================================================================
        // Kết quả
        // =====================================================================

        bool Charging => Imaging && !Coated && KV >= 10;

        string BuildResults()
        {
            var sb = new StringBuilder();
            sb.Append($"<color=#9FB3C8>Mẫu:</color> {Src.Describe(Sample)}\n");
            if (Mounted)
            {
                sb.Append($"<color=#9FB3C8>Chất rắn trên đế:</color> {string.Join(", ", solids.Select(s => $"{s.Name} {(s.Crystalline ? "" : "(vô định hình)")}"))}\n");
                if (Imaging)
                {
                    sb.Append($"\n<b>Ảnh ×{Mag}</b> (vùng {Fmt(FieldUm, "0.#")} µm): ");
                    var shapes = solids.Where(s => s.Crystalline).Select(s => Xrd.Get(s.Id)).Where(c => c != null).Select(c => $"{c.Name}: {ShapeName(c.Shape)} cỡ ~{Fmt(c.GrainUm, "0.#")} µm").ToList();
                    if (solids.Any(s => !s.Crystalline)) shapes.Add("khối vô định hình, bề mặt nhẵn, nứt nẻ khi khô");
                    sb.Append(string.Join("; ", shapes)).Append('\n');
                    if (Charging) sb.Append("<color=#FFC966>Ảnh có vệt sáng, trôi: mẫu cách điện tích điện. Phủ vàng, hoặc hạ xuống 5 kV.</color>\n");
                    if (Math.Abs(Focus) >= 0.5) sb.Append("<color=#FFC966>Ảnh mờ: chưa lấy nét.</color>\n");
                }
            }
            if (EdsQuant != null)
            {
                sb.Append($"\n<b>Phân tích chuyên sâu: EDS ({KV} kV)</b>\n");
                foreach (var kv in EdsQuant.OrderByDescending(x => x.Value))
                {
                    var l = Eds.Get(kv.Key);
                    sb.Append($"• <color=#61DBFF>{kv.Key}</color> {l?.Name} {Fmt(l?.KeV ?? 0, "0.00")} keV: <b>{Fmt(kv.Value * 100, "0.0")}% nguyên tử</b>\n");
                }
                sb.Append(skipCoat ? "<color=#9FB3C8>Đã bỏ Au (lớp phủ) và C (băng dính cacbon) khi tính %</color>\n" : "<color=#FFC966>Đang tính cả Au, C: đây là lớp phủ / băng dính, không phải thành phần mẫu</color>\n");
                var truth = SolidResidue.AtomFractions(solids);
                sb.Append("<color=#9FB3C8>Thành phần thật (để so): ").Append(string.Join(", ", truth.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value * 100:0}%"))).Append("</color>\n");
            }
            return sb.ToString();
        }

        static string ShapeName(string s) => s switch
        {
            "lap_phuong" => "tinh thể lập phương", "mat_thoi" => "khối mặt thoi", "lang_tru" => "tấm / lăng trụ dài",
            "hat_min" => "hạt rất mịn kết đám", "manh_vo" => "mảnh vỡ góc cạnh", _ => "hạt",
        };

        // =====================================================================
        // Sơ đồ cột electron
        // =====================================================================

        public override void DrawSchematic(Painter p, float time)
        {
            SchemKit.Begin(p);
            float cx = 250;
            SchemKit.Box(p, cx - 50, 260, 100, 70, new Color(0.25f, 0.27f, 0.32f));  // súng
            p.Disc(cx, 295, 7, Beam ? new Color(1f, 0.85f, 0.4f) : SchemKit.Off);
            for (int i = 0; i < 3; i++)
            {
                float y = 220 - i * 55;
                SchemKit.Box(p, cx - 70, y - 14, 40, 28, new Color(0.5f, 0.35f, 0.2f));
                SchemKit.Box(p, cx + 30, y - 14, 40, 28, new Color(0.5f, 0.35f, 0.2f));
            }
            SchemKit.Box(p, cx - 110, 20, 220, 70, Vacuum >= 0 ? new Color(0.08f, 0.1f, 0.16f) : new Color(0.2f, 0.22f, 0.25f));  // buồng mẫu
            p.FillRect(cx - 40, 30, 80, 10, new Color(0.7f, 0.72f, 0.75f));  // đế
            if (Mounted) p.FillRect(cx - 28, 40, 56, 5, Coated ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.9f, 0.9f, 0.88f));
            if (Imaging)
            {
                float sx = cx + Mathf.Sin(time * 6) * 18;
                p.Line(cx, 260, cx, 120, new Color(0.5f, 0.85f, 1f, 0.8f), 3);
                p.Line(cx, 120, sx, 45, new Color(0.5f, 0.85f, 1f, 0.8f), 3);
            }
            SchemKit.Box(p, cx + 120, 40, 60, 40, new Color(0.2f, 0.3f, 0.25f));    // EDS
            SchemKit.Box(p, cx - 180, 45, 50, 30, new Color(0.25f, 0.25f, 0.3f));  // SE
            p.Ring(cx, 100, 22, new Color(0.6f, 0.7f, 0.9f), 3);                     // BSE vành khuyên
            // ảnh thu nhỏ trên màn hình bên phải
            var mon = new Vector2(760, 175);
            SchemKit.Box(p, mon.x - 170, mon.y - 120, 340, 240, new Color(0.03f, 0.03f, 0.03f));
            if (Imaging && imageCache != null)
            {
                for (int y = 0; y < 220; y += 2)
                    for (int x = 0; x < 320; x += 2)
                    {
                        var c = imageCache[(y * 470 / 220) * 920 + x * 920 / 320];
                        p.FillRect(mon.x - 160 + x, mon.y - 110 + y, 2, 2, new Color32(c.r, c.g, c.b, 255));
                    }
            }
            var d = Doc;
            S.SchemLabel("sung", d.PartName("sung") + (Beam ? $"\n<color=#FFD27A>{KV} kV</color>" : ""), cx + 150, 300, 11);
            S.SchemLabel("tk", d.PartName("thau_kinh"), cx + 160, 200, 11);
            S.SchemLabel("quet", d.PartName("cuon_quet"), cx + 160, 140, 11);
            S.SchemLabel("buong", d.PartName("buong_mau") + (Vacuum < 0 ? " (mở)" : VacuumOk ? " (chân không)" : " (đang hút)"), cx, 12, 11);
            S.SchemLabel("eds", "EDS", cx + 150, 92, 11);
            S.SchemLabel("se", "SE", cx - 155, 88, 11);
            S.SchemLabel("bse", "BSE", cx + 40, 120, 11);
            S.SchemLabel("man", "Màn hình ảnh", mon.x, mon.y + 132, 11);
            S.SchemLabel("trang_thai", !Mounted ? "<color=#8899AA>CHƯA CÓ MẪU</color>" : Vacuum < 0 ? "<color=#FFC966>BUỒNG MẪU MỞ</color>" : !VacuumOk ? $"<color=#FFC966>ĐANG HÚT CHÂN KHÔNG {Vacuum / PumpNeedS:P0}</color>" : !Beam ? "<color=#73F29E>SẴN SÀNG</color>" : "<color=#61DBFF>ĐANG QUÉT ẢNH</color>", 560, 330, 14);
        }

        // =====================================================================
        // Ảnh SEM, phổ EDS
        // =====================================================================

        public override void DrawPlot(Painter p)
        {
            if (plotMode == 1 && EdsSpectrum != null) { DrawEds(p); return; }
            S.SetPlotAxes(Imaging ? $"Ảnh SEM ({(Bse ? "BSE" : "SE")}, {KV} kV, ×{Mag})" : "Ảnh SEM", "", "", "", "", "", "");
            if (!Imaging) { p.Clear(new Color(0.02f, 0.02f, 0.02f)); imageKey = ""; return; }
            string key = $"{Src.Index}|{Mag}|{KV}|{Bse}|{SlowScan}|{Math.Round(Focus, 1)}|{Coated}|{SolidResidue.TotalMg(solids):0.0}";
            if (key != imageKey || imageCache == null) { RenderImage(p); imageCache = p.Snapshot(); imageKey = key; }
            else p.Restore(imageCache);
            // thanh tỉ lệ
            double target = FieldUm / 5;
            double bar = Math.Pow(10, Math.Floor(Math.Log10(target)));
            if (target / bar >= 5) bar *= 5; else if (target / bar >= 2) bar *= 2;
            float px = (float)(bar / FieldUm * p.W);
            p.FillRect(p.W - px - 30, 20, px, 6, Color.white);
            S.SetPlotAxes($"Ảnh SEM ({(Bse ? "BSE" : "SE")}, {KV} kV, ×{Mag}) · thanh tỉ lệ (dưới phải) = {Fmt(bar, bar < 1 ? "0.#" : "0")} µm", "", "", "", "", "", "");
        }

        void RenderImage(Painter p)
        {
            var r = new System.Random(1000 + Src.Index);
            p.Clear(new Color(0.16f, 0.16f, 0.17f));  // băng dính cacbon
            for (int i = 0; i < 4000; i++) p.AddGray(r.Next(p.W), r.Next(p.H), (float)(r.NextDouble() * 0.08 - 0.04));
            double total = SolidResidue.TotalMg(solids);
            double scale = p.W / FieldUm;  // điểm ảnh / µm
            foreach (var s in solids.OrderByDescending(x => x.Mg))
            {
                var card = Xrd.Get(s.Id);
                string shape = !s.Crystalline ? "vo_dinh_hinh" : card?.Shape ?? "manh_vo";
                double size = !s.Crystalline ? 60 : card?.GrainUm ?? 10;
                float gray = Bse ? Mathf.Clamp(0.25f + MeanZ(s) / 60f, 0.3f, 0.95f) : 0.62f;
                double cover = 0.55 * s.Mg / Math.Max(1e-9, total);
                double pxSize = size * scale;
                if (pxSize < 3)
                {
                    // hạt nhỏ hơn điểm ảnh: thấy các đám kết tụ
                    int clusters = (int)Math.Min(400, cover * p.W * p.H / 900);
                    for (int i = 0; i < clusters; i++)
                    {
                        float cx = r.Next(p.W), cy = r.Next(p.H), rad = 6 + (float)r.NextDouble() * 18;
                        for (int k = 0; k < 40; k++) p.Disc(cx + (float)(r.NextDouble() - 0.5) * rad * 2, cy + (float)(r.NextDouble() - 0.5) * rad * 2, Math.Max(1, (float)pxSize), new Color(gray, gray, gray, 0.8f));
                    }
                    continue;
                }
                int count = (int)Math.Min(350, Math.Max(1, cover * p.W * p.H / (pxSize * pxSize)));
                for (int i = 0; i < count; i++)
                {
                    float x = (float)(r.NextDouble() * (p.W + pxSize) - pxSize / 2), y = (float)(r.NextDouble() * (p.H + pxSize) - pxSize / 2);
                    float sz = (float)(pxSize * (0.5 + r.NextDouble()));
                    float ang = (float)(r.NextDouble() * Math.PI);
                    DrawParticle(p, shape, x, y, sz, ang, gray, r);
                }
            }
            if (Charging)
                for (int k = 0; k < 18; k++)
                {
                    int y = r.Next(p.H), len = 100 + r.Next(500), x0 = r.Next(p.W);
                    for (int x = x0; x < Math.Min(p.W, x0 + len); x++) { p.AddGray(x, y, 0.5f); p.AddGray(x, y + 1, 0.35f); }
                }
            int blur = (int)Math.Min(6, Math.Abs(Focus) * 1.5);
            if (blur > 0) p.Blur(blur);
            float noise = SlowScan ? 0.04f : 0.18f;
            for (int i = 0; i < p.W * p.H / 3; i++) p.AddGray(r.Next(p.W), r.Next(p.H), (float)((r.NextDouble() - 0.5) * noise * 2));
        }

        static float MeanZ(SolidResidue.Phase s)
        {
            var z = new Dictionary<string, int> { ["H"] = 1, ["C"] = 6, ["N"] = 7, ["O"] = 8, ["Na"] = 11, ["Mg"] = 12, ["Al"] = 13, ["Si"] = 14, ["S"] = 16, ["Cl"] = 17, ["K"] = 19, ["Ca"] = 20, ["Cr"] = 24, ["Fe"] = 26, ["Ni"] = 28, ["Cu"] = 29, ["Zn"] = 30, ["Ag"] = 47, ["Ba"] = 56 };
            var a = s.Atoms;
            double n = a.Values.Sum();
            return n > 0 ? (float)(a.Sum(kv => (z.TryGetValue(kv.Key, out var v) ? v : 10) * kv.Value) / n) : 8;
        }

        static void DrawParticle(Painter p, string shape, float x, float y, float s, float ang, float g, System.Random r)
        {
            Color face = new Color(g, g, g), lit = new Color(Mathf.Min(1, g + 0.25f), Mathf.Min(1, g + 0.25f), Mathf.Min(1, g + 0.25f)), dark = new Color(g * 0.6f, g * 0.6f, g * 0.6f);
            Vector2 Rot(float dx, float dy) => new Vector2(x + dx * Mathf.Cos(ang) - dy * Mathf.Sin(ang), y + dx * Mathf.Sin(ang) + dy * Mathf.Cos(ang));
            switch (shape)
            {
                case "lap_phuong":
                {
                    float h = s / 2, dz = s * 0.3f;
                    p.FillPoly(new[] { Rot(-h, -h), Rot(h, -h), Rot(h, h), Rot(-h, h) }, face);
                    p.FillPoly(new[] { Rot(h, -h), Rot(h + dz, -h + dz), Rot(h + dz, h + dz), Rot(h, h) }, dark);
                    p.FillPoly(new[] { Rot(-h, h), Rot(h, h), Rot(h + dz, h + dz), Rot(-h + dz, h + dz) }, lit);
                    p.Poly(new[] { Rot(-h, -h), Rot(h, -h), Rot(h, h), Rot(-h, h), Rot(-h, -h) }, lit, 1.5f);
                    break;
                }
                case "mat_thoi":
                {
                    float h = s / 2, sk = s * 0.35f;
                    p.FillPoly(new[] { Rot(-h + sk, -h), Rot(h + sk, -h), Rot(h - sk, h), Rot(-h - sk, h) }, face);
                    p.Poly(new[] { Rot(-h + sk, -h), Rot(h + sk, -h), Rot(h - sk, h), Rot(-h - sk, h), Rot(-h + sk, -h) }, lit, 1.5f);
                    p.Line(Rot(-h + sk, -h).x, Rot(-h + sk, -h).y, Rot(h - sk, h).x, Rot(h - sk, h).y, dark, 1);
                    break;
                }
                case "lang_tru":
                {
                    float l = s * 1.6f, w = s * 0.35f;
                    p.FillPoly(new[] { Rot(-l, -w), Rot(l, -w), Rot(l, w), Rot(-l, w) }, face);
                    p.Line(Rot(-l, w).x, Rot(-l, w).y, Rot(l, w).x, Rot(l, w).y, lit, 2);
                    p.Line(Rot(-l, -w).x, Rot(-l, -w).y, Rot(l, -w).x, Rot(l, -w).y, dark, 2);
                    break;
                }
                case "hat_min":
                {
                    for (int k = 0; k < 6; k++)
                    {
                        float ox = (float)(r.NextDouble() - 0.5) * s * 1.6f, oy = (float)(r.NextDouble() - 0.5) * s * 1.6f;
                        p.Disc(x + ox, y + oy, s * 0.45f, face);
                        p.Disc(x + ox - s * 0.12f, y + oy + s * 0.12f, s * 0.18f, lit);
                    }
                    break;
                }
                case "vo_dinh_hinh":
                {
                    p.Disc(x, y, s * 0.6f, face);
                    p.Disc(x + s * 0.3f, y - s * 0.2f, s * 0.45f, face);
                    p.Disc(x - s * 0.15f, y + s * 0.2f, s * 0.25f, lit);
                    p.Line(x - s * 0.4f, y, x + s * 0.3f, y - s * 0.25f, dark, 1.5f);
                    break;
                }
                default:
                {
                    int n = 5 + r.Next(3);
                    var pts = new Vector2[n];
                    for (int k = 0; k < n; k++) { float a = k * Mathf.PI * 2 / n + (float)r.NextDouble() * 0.5f; float rr = s * (0.35f + (float)r.NextDouble() * 0.3f); pts[k] = Rot(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr); }
                    p.FillPoly(pts, face);
                    p.Poly(pts.Concat(new[] { pts[0] }).ToList(), lit, 1.5f);
                    break;
                }
            }
        }

        void DrawEds(Painter p)
        {
            InstrumentScreen.PlotFrame(p);
            double max = EdsSpectrum.Skip(20).Max() * 1.1;
            S.SetPlotAxes($"Phổ EDS ({KV} kV)", "Năng lượng (keV)", "Số đếm", "0", "10", "0", Fmt(max, "0"));
            var pts = new List<Vector2>();
            for (int i = 0; i < EdsSpectrum.Length; i += 2) pts.Add(InstrumentScreen.Map(p, i * 0.01, Math.Min(max, EdsSpectrum[i]), 0, 10, 0, max));
            p.Poly(pts, new Color(0.45f, 0.95f, 0.62f), 1.5f);
            foreach (var l in Eds.Lines)
            {
                if (EdsQuant == null || (!EdsQuant.ContainsKey(l.El) && !(skipCoat && (l.El == "Au" || l.El == "C")))) continue;
                int c = (int)(l.KeV / 0.01);
                if (c >= EdsSpectrum.Length || EdsSpectrum[Math.Max(0, c)] < max * 0.05) continue;
                var q = InstrumentScreen.Map(p, l.KeV, Math.Min(max, EdsSpectrum[c]), 0, 10, 0, max);
                p.FillRect(q.x - 1, q.y + 4, 2, 14, l.El == "Au" || l.El == "C" ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.45f, 0.45f));
            }
        }
    }
}
