// -----------------------------------------------------------------------------
// MolecularTools.cs — Dụng cụ và máy Phòng sinh học phân tử, nối thành một quy trình chạy thật:
//   gắn đầu côn → hút thuốc thử trong khay đá → nhả vào ống PCR (ống ghi lại đúng thành phần, thể tích)
//   → đặt ống vào máy PCR → màn hình máy chạy chu trình nhiệt (Pcr.Run tính băng sản phẩm từ chính thành phần ống)
//   → đổ gel (lò vi sóng + khay đúc) → đặt gel vào bể → nạp thang chuẩn và mẫu vào giếng → bật nguồn
//   → buồng chụp gel vẽ ảnh gel từ băng thật và ước lượng kích thước theo thang chuẩn.
// Thao tác: cầm dụng cụ (bấm), trỏ vào đích rồi bấm (IBioTarget); con lăn chỉnh µL khi cầm micropipet.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    // =========================================================================
    // Thuốc thử và hỗn hợp trong ống
    // =========================================================================

    /// <summary>Mã các thuốc thử dùng trong phòng sinh học phân tử.</summary>
    public static class MolCodes
    {
        public const string MasterMix = "master_mix_2x";     // đệm, MgCl₂, dNTP, Taq, chất nạp mẫu xanh (kiểu "Green Master Mix")
        public const string Primers = "cap_moi_gfp";          // mồi xuôi (đuôi EcoRI) + mồi ngược (đuôi BamHI), 10 µM mỗi loại
        public const string Template = "adn_khuon_pgfp";      // ADN khuôn: plasmid mang gen GFP, 1 ng/µL
        public const string BananaDna = "adn_chuoi";          // ADN thô tách từ chuối (không có gen GFP)
        public const string Water = "nuoc_khong_nuclease";
        public const string Ladder = "thang_adn_100bp";       // thang chuẩn 100 bp đã pha sẵn chất nạp mẫu
        public const string LoadingDye = "dem_nap_mau_6x";
        public const string PlasmidCheck = "plasmid_cat_kiem_tra";  // plasmid tách từ khuẩn lạc, đã cắt EcoRI + BamHI

        public static string Name(string code)
        {
            switch (code)
            {
                case MasterMix: return "Master Mix 2×";
                case Primers: return "Cặp mồi GFP";
                case Template: return "ADN khuôn pGFP";
                case BananaDna: return "ADN chuối";
                case Water: return "Nước không nuclease";
                case Ladder: return "Thang ADN 100 bp";
                case LoadingDye: return "Đệm nạp mẫu 6×";
                case PlasmidCheck: return "Plasmid đã cắt kiểm tra";
                default: return code;
            }
        }

        /// <summary>Thang chuẩn 100 bp: 100 – 1000 bp cách 100, thêm 1500; băng 500 bp đậm gấp đôi để định hướng.</summary>
        public static readonly int[] LadderBp = { 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1500 };
        public const double LadderNgPerBandPerUl = 1.0;

        /// <summary>Chất có sẵn thuốc nạp mẫu (đặc, chìm xuống đáy giếng gel).</summary>
        public static bool HasLoadingDye(string code) => code == MasterMix || code == Ladder || code == LoadingDye;
    }

    /// <summary>Hỗn hợp trong ống / đầu côn: thể tích từng thành phần (µL) và các băng ADN (ng).</summary>
    [Serializable]
    public class MolMix
    {
        public List<string> codes = new List<string>();
        public List<float> ul = new List<float>();
        public List<BioDnaBandInfo> dna = new List<BioDnaBandInfo>();

        public float TotalUl { get { float s = 0; foreach (var v in ul) s += v; return s; } }
        public bool IsEmpty => TotalUl < 0.01f;

        public float Get(string code)
        {
            int i = codes.IndexOf(code);
            return i >= 0 ? ul[i] : 0f;
        }

        public void Add(string code, float v)
        {
            if (v <= 0) return;
            int i = codes.IndexOf(code);
            if (i >= 0) ul[i] += v; else { codes.Add(code); ul.Add(v); }
        }

        public bool HasLoadingDye
        {
            get
            {
                for (int i = 0; i < codes.Count; i++) if (MolCodes.HasLoadingDye(codes[i]) && ul[i] > 0.05f) return true;
                return false;
            }
        }

        /// <summary>Lấy takeUl từ src (chia đều theo tỉ lệ mọi thành phần và lượng ADN); removeFromSrc = false với chai gốc.</summary>
        public void TakeFrom(MolMix src, float takeUl, bool removeFromSrc)
        {
            float total = src.TotalUl;
            if (total <= 0.0001f || takeUl <= 0) return;
            float f = Mathf.Clamp01(takeUl / total);
            for (int i = 0; i < src.codes.Count; i++)
            {
                Add(src.codes[i], src.ul[i] * f);
                if (removeFromSrc) src.ul[i] -= src.ul[i] * f;
            }
            for (int i = 0; i < src.dna.Count; i++)
            {
                var b = src.dna[i];
                AddBand(new BioDnaBandInfo { bp = b.bp, ng = b.ng * f, smear = b.smear });
                if (removeFromSrc) { b.ng -= b.ng * f; src.dna[i] = b; }
            }
            if (removeFromSrc) src.Compact();
        }

        public void AddBand(BioDnaBandInfo b)
        {
            for (int i = 0; i < dna.Count; i++)
                if (dna[i].bp == b.bp && dna[i].smear == b.smear) { var d = dna[i]; d.ng += b.ng; dna[i] = d; return; }
            dna.Add(b);
        }

        public void Compact()
        {
            for (int i = codes.Count - 1; i >= 0; i--) if (ul[i] < 0.005f) { codes.RemoveAt(i); ul.RemoveAt(i); }
            for (int i = dna.Count - 1; i >= 0; i--) if (dna[i].ng < 0.001) dna.RemoveAt(i);
        }

        public void Clear() { codes.Clear(); ul.Clear(); dna.Clear(); }

        public string Describe()
        {
            if (IsEmpty) return "trống";
            var sb = new StringBuilder();
            sb.Append(BioFeedback.FormatVi(TotalUl, "0.0")).Append(" µL: ");
            for (int i = 0; i < codes.Count; i++)
            {
                if (ul[i] < 0.05f) continue;  // vết nhiễm không ghi lên nhãn (nhưng vẫn tính trong PCR)
                if (sb[sb.Length - 2] != ':') sb.Append(" + ");
                sb.Append(BioFeedback.FormatVi(ul[i], "0.#")).Append(' ').Append(MolCodes.Name(codes[i]));
            }
            if (dna.Count > 0)
            {
                sb.Append(" · ADN: ");
                for (int i = 0; i < dna.Count; i++) sb.Append(i > 0 ? ", " : "").Append(dna[i].bp).Append(" bp");
            }
            return sb.ToString();
        }
    }

    /// <summary>Tiện ích chung: cập nhật thẻ thông tin khi trạng thái đổi, ghi trạng thái máy lên web.</summary>
    static class MolUi
    {
        public static void Info(Component c, string description)
        {
            var info = c.GetComponent<EquipmentInfo>();
            if (info != null) info.description = description;
        }

        public static void Say(string text, bool warn = false) =>
            LabMessages.Say(text, warn ? LabMessages.Tone.Warn : LabMessages.Tone.Good);

        public static void MachineStatus(string ma, string state, string text, float progress) =>
            BioLabSync.Instance?.RecordMachineStatus(ma, state, text, progress);

        /// <summary>Micropipet trên dụng cụ đang cầm, hoặc null thật (không trả null giả của GetComponent để ?. dùng được).</summary>
        public static Micropipette Pipette(BioTool tool)
        {
            if (tool == null) return null;
            var p = tool.GetComponent<Micropipette>();
            return p != null ? p : null;
        }
    }

    // =========================================================================
    // Ống thuốc thử gốc (cắm cố định trong khay đá)
    // =========================================================================

    // =========================================================================
    // Micropipet (P10 / P200 / P1000)
    // =========================================================================

    // =========================================================================
    // Ống phản ứng (ống PCR 0,2 mL / eppendorf 1,5 mL)
    // =========================================================================

    // =========================================================================
    // Tủ thao tác PCR (tiền PCR)
    // =========================================================================

    // =========================================================================
    // Máy PCR (luân nhiệt)
    // =========================================================================

    /// <summary>Hồ sơ nhiệt PCR: biến tính đầu 95 °C 2 phút → n × (95 °C 30 s → Tₐ 30 s → 72 °C 45 s) → 72 °C 5 phút → giữ 4 °C.</summary>
    public static class PcrProfile
    {
        public const double Init = 120, Den = 30, Ann = 30, Ext = 45, Final = 300;

        public static double TotalSeconds(int cycles) => Init + cycles * (Den + Ann + Ext) + Final;

        public static (string phase, int cycle, double tempC) At(double t, double annealC, int cycles)
        {
            if (t < Init) return ("biến tính đầu 95 °C", 0, 95);
            double c = t - Init, per = Den + Ann + Ext;
            if (c < cycles * per)
            {
                int k = (int)(c / per);
                double r = c - k * per;
                if (r < Den) return ("biến tính 95 °C", k + 1, 95);
                if (r < Den + Ann) return ($"gắn mồi {annealC:0} °C", k + 1, annealC);
                return ("kéo dài 72 °C", k + 1, 72);
            }
            if (c < cycles * per + Final) return ("kéo dài cuối 72 °C", 0, 72);
            return ("giữ 4 °C", 0, 4);
        }
    }

    /// <summary>Màn hình điều khiển máy PCR (bước theo data/may/may_pcr.json).</summary>
    public class PcrProgram : InstrumentProgram
    {
        public override string Code => "may_pcr";
        public ThermalCyclerController cycler;
        UnityEngine.UI.Button bLid, bRun;
        string lastResult = "";

        public override void Build()
        {
            S.AddButton("Tₐ −1 °C", 0, 0, () => SetAnneal(-1));
            S.AddButton("Tₐ gợi ý (Tm − 5)", 1, 0, () => { cycler.annealC = 55; S.Done("cai_dat"); });
            S.AddButton("Tₐ +1 °C", 2, 0, () => SetAnneal(+1));
            S.AddButton("Chu kỳ −5", 0, 1, () => SetCycles(-5));
            S.AddButton("Chu kỳ +5", 2, 1, () => SetCycles(+5));
            bLid = S.AddButton("", 1, 1, () => { cycler.lidHeated = !cycler.lidHeated; S.Done("cai_dat"); });
            bRun = S.AddButton("Chạy chu trình", 0, 2, Run, true, 2);
            S.AddButton("Tua tới cuối", 2, 2, () => cycler.SkipToEnd());
            S.SetPlotAxes("NHIỆT ĐỘ KHỐI NHIỆT THEO THỜI GIAN", "Thời gian (phút)", "Nhiệt độ (°C)", "0", $"{PcrProfile.TotalSeconds(cycler.cycles) / 60:0}", "0", "100");
            if (cycler.loaded.Count > 0) S.Done("nap_ong");
            if (AnyCompleteMix()) S.Done("chuan_bi_mau");
            Refresh();
        }

        bool AnyCompleteMix()
        {
            foreach (var t in cycler.loaded) { var s = t.BuildPcrSetup(55, 30, true); if (s.hasBuffer && s.hasPrimers) return true; }
            return false;
        }

        void SetAnneal(int d)
        {
            if (cycler.running) return;
            cycler.annealC = Math.Max(40, Math.Min(72, cycler.annealC + d));
            S.Done("cai_dat");
        }

        void SetCycles(int d)
        {
            if (cycler.running) return;
            cycler.cycles = Mathf.Clamp(cycler.cycles + d, 10, 40);
            S.SetPlotAxes("NHIỆT ĐỘ KHỐI NHIỆT THEO THỜI GIAN", "Thời gian (phút)", "Nhiệt độ (°C)", "0", $"{PcrProfile.TotalSeconds(cycler.cycles) / 60:0}", "0", "100");
            S.Done("cai_dat");
        }

        void Run()
        {
            if (!S.Require("chay_chu_trinh")) return;
            if (cycler.loaded.Count == 0) { S.Say("Chưa có ống nào trong khối nhiệt: cầm ống PCR, trỏ vào máy và bấm để đặt ống.", 2); return; }
            if (!cycler.lidHeated) S.Say("Nắp nhiệt đang tắt: hơi nước sẽ ngưng trên nắp ống, hiệu suất giảm.", 2);
            cycler.StartRun();
            S.Done("chay_chu_trinh");
        }

        public void OnFinished(string summary)
        {
            lastResult = summary;
            if (S != null && InstrumentScreen.IsOpen && S.Program == this)
            {
                S.Done("thu_san_pham");
                S.Say("Xong chu trình. " + summary, 1);
            }
        }

        public override void Refresh()
        {
            InstrumentScreen.SetButton(bLid, cycler.lidHeated ? "Nắp nhiệt 105 °C: BẬT" : "Nắp nhiệt: TẮT", !cycler.running, !cycler.lidHeated);
            InstrumentScreen.SetButton(bRun, cycler.running ? "Đang chạy…" : "Chạy chu trình", !cycler.running, true);
            var sb = new StringBuilder();
            sb.Append($"<b>{cycler.machineName.ToUpperInvariant()}</b>\n");
            sb.Append($"• Chu trình: 95 °C 2′ → [95 °C 30″ → <b>{cycler.annealC:0} °C</b> 30″ → 72 °C 45″] × <b>{cycler.cycles}</b> → 72 °C 5′ → 4 °C\n");
            sb.Append($"• Mồi Tm ≈ 60 °C → Tₐ gợi ý 55 °C. Nắp nhiệt: {(cycler.lidHeated ? "105 °C" : "<color=#FF8866>TẮT</color>")}\n");
            sb.Append(cycler.running ? $"• Đang chạy: <b>{cycler.Phase}</b> ({cycler.simSeconds / 60:0}/{PcrProfile.TotalSeconds(cycler.cycles) / 60:0} phút)\n" : cycler.finished ? "• Đã chạy xong, đang giữ 4 °C\n" : "");
            sb.Append($"• Ống trong khối nhiệt ({cycler.loaded.Count}):\n");
            foreach (var t in cycler.loaded) sb.Append($"   – Ống {t.labelText}: {t.mix.Describe()}\n");
            if (cycler.loaded.Count == 0) sb.Append("   (chưa có — cầm ống PCR, trỏ vào máy rồi bấm)\n");
            if (!string.IsNullOrEmpty(lastResult)) sb.Append($"• Kết quả: {lastResult}");
            S.SetResults(sb.ToString());
        }

        public override void DrawSchematic(Painter p, float time)
        {
            p.Clear(new Color(0.06f, 0.07f, 0.1f));
            double temp = cycler.running ? PcrProfile.At(cycler.simSeconds, cycler.annealC, cycler.cycles).tempC : cycler.finished ? 4 : 25;
            var hot = Color.Lerp(new Color(0.25f, 0.45f, 0.9f), new Color(0.95f, 0.3f, 0.2f), Mathf.InverseLerp(4, 95, (float)temp));
            float x0 = 80, y0 = 60, w = 520, h = 150;
            p.FillRect(x0 - 10, y0 - 10, w + 20, h + 20, new Color(0.3f, 0.32f, 0.36f));
            p.FillRect(x0, y0, w, h, Color.Lerp(new Color(0.18f, 0.19f, 0.22f), hot, 0.6f));
            for (int i = 0; i < ThermalCyclerController.MaxTubes; i++)
            {
                float cx = x0 + 40 + i * 62, cy = y0 + h / 2;
                Color c = new Color(0.12f, 0.12f, 0.14f);
                if (i < cycler.loaded.Count)
                {
                    var s = cycler.loaded[i].BuildPcrSetup(cycler.annealC, cycler.cycles, true);
                    bool ok = s.hasBuffer && s.hasPrimers;
                    c = ok ? (s.hasTemplate ? new Color(0.3f, 0.85f, 0.4f) : new Color(0.75f, 0.75f, 0.8f)) : new Color(0.95f, 0.6f, 0.2f);
                }
                p.Disc(cx, cy, 22, new Color(0.08f, 0.08f, 0.1f));
                p.Disc(cx, cy, 16, c);
            }
            // nắp nhiệt
            p.FillRect(x0 - 10, y0 + h + 20, w + 20, 26, cycler.lidHeated ? new Color(0.85f, 0.35f, 0.25f) : new Color(0.35f, 0.35f, 0.4f));
            // nhiệt kế
            float bar = Mathf.InverseLerp(0, 100, (float)temp);
            p.FillRect(700, 50, 40, 250, new Color(0.15f, 0.16f, 0.2f));
            p.FillRect(705, 55, 30, 240 * bar, hot);
            S.SchemLabel("pcr_t", $"{temp:0} °C", 720, 320, 18, Color.white);
            S.SchemLabel("pcr_lid", cycler.lidHeated ? "Nắp nhiệt 105 °C" : "Nắp nhiệt tắt", x0 + w / 2, y0 + h + 33, 13, Color.white);
            S.SchemLabel("pcr_leg", "Xanh lá: đủ thành phần · Xám: đối chứng âm · Cam: thiếu thành phần", 420, 25, 12, new Color(0.75f, 0.8f, 0.85f));
        }

        public override void DrawPlot(Painter p)
        {
            InstrumentScreen.PlotFrame(p);
            double total = PcrProfile.TotalSeconds(cycler.cycles);
            double shown = cycler.running || cycler.finished ? (cycler.finished ? total : cycler.simSeconds) : total;
            Vector2 prev = Vector2.zero;
            var line = cycler.running || cycler.finished ? new Color(1f, 0.55f, 0.3f) : new Color(0.5f, 0.6f, 0.75f);
            for (int i = 0; i <= 600; i++)
            {
                double t = shown * i / 600.0;
                var pt = InstrumentScreen.Map(p, t, PcrProfile.At(t, cycler.annealC, cycler.cycles).tempC, 0, total, 0, 100);
                if (i > 0) p.Line(prev.x, prev.y, pt.x, pt.y, line, 2);
                prev = pt;
            }
        }
    }

    // =========================================================================
    // Đổ gel: lò vi sóng + khay đúc
    // =========================================================================

    // =========================================================================
    // Bể điện di + bộ nguồn
    // =========================================================================

    [Serializable]
    public class GelLane
    {
        public string label = "";
        public List<BioDnaBandInfo> bands = new List<BioDnaBandInfo>();
        public bool floated;   // không có chất nạp mẫu: mẫu nổi lên khỏi giếng
    }

    // =========================================================================
    // Buồng chụp gel
    // =========================================================================

    public class GelDocProgram : InstrumentProgram
    {
        public override string Code => "gel_doc";
        public GelDocController gelDoc;
        bool lightOn, imaged;
        string analysis = "";

        GelTankController Tank => gelDoc != null ? gelDoc.sourceTank : null;

        public override void Build()
        {
            S.AddButton("Bật nguồn buồng chụp", 0, 0, () => S.Done("chuan_bi"));
            S.AddButton("Đặt gel vào buồng", 1, 0, PlaceGel);
            S.AddButton("Ánh sáng xanh 470 nm", 2, 0, LightOn);
            S.AddButton("Chụp & phân tích", 0, 1, Capture, true, 3);
            S.SetPlotAxes("ẢNH GEL (giếng ở trên, ADN chạy xuống)", "Giếng 1 → 8", "Quãng đường từ giếng", "1", "8", "xa", "giếng");
            Refresh();
        }

        public void LightOn()
        {
            lightOn = true;
            S.Done("chon_che_do");
        }

        public void PlaceGel()
        {
            if (!S.Require("dat_gel")) return;
            if (Tank == null || !Tank.gelPlaced) { S.Say("Chưa có gel trong bể điện di.", 2); return; }
            if (Tank.running) { S.Say("Gel đang chạy điện di: tắt nguồn trước.", 2); return; }
            if (!Tank.hasCompletedRun) S.Say("Gel chưa chạy điện di xong: các băng còn nằm sát giếng.", 2);
            S.Done("dat_gel");
        }

        public void Capture()
        {
            if (!S.Require("chup_anh")) return;
            if (Tank == null || !Tank.gelPlaced) { S.Say("Chưa có gel.", 2); return; }
            if (!lightOn) { S.Say("Chưa bật nguồn sáng kích thích: ảnh tối đen.", 2); return; }
            imaged = true;
            gelDoc.captured = true;
            analysis = Analyze(Tank);
            S.Done("chup_anh");
            S.Say("Đã chụp ảnh gel.", 1);
            BioFeedback.PlaySuccess();
            BioLabSync.Instance?.RecordExperimentResult("pcr_dien_di", 12, "Phòng SHPT", analysis);
            MolUi.MachineStatus("gel_doc", "san_sang", "Đã chụp và phân tích gel", 1f);
        }

        /// <summary>Ước lượng kích thước băng theo thang chuẩn: nội suy tuyến tính log10(bp) theo quãng đường.</summary>
        public static string Analyze(GelTankController tank)
        {
            if (!tank.hasCompletedRun) return "Gel chưa chạy điện di: không đọc được kích thước băng.";
            GelLane ladder = tank.lanes.Find(l => l.label.StartsWith("Thang") && !l.floated);
            var sb = new StringBuilder();
            for (int i = 0; i < tank.lanes.Count; i++)
            {
                var lane = tank.lanes[i];
                sb.Append($"Giếng {i + 1} ({lane.label}): ");
                if (lane.floated) { sb.Append("trống – mẫu nổi khỏi giếng (thiếu đệm nạp mẫu). "); continue; }
                if (lane == ladder) { sb.Append("thang chuẩn 100 – 1500 bp. "); continue; }
                var visible = lane.bands.FindAll(b => b.ng >= 2);
                if (visible.Count == 0) { sb.Append("không có băng. "); continue; }
                visible.Sort((a, b) => b.bp.CompareTo(a.bp));
                for (int k = 0; k < visible.Count; k++)
                {
                    double d = tank.DistanceCm(visible[k].bp);
                    string est = ladder != null ? $"~{EstimateBp(tank, ladder, d):0} bp" : $"{d:0.00} cm (cần thang chuẩn để đọc kích thước)";
                    sb.Append(k > 0 ? ", " : "").Append(est).Append(visible[k].smear ? " (nhòe)" : "");
                }
                sb.Append(". ");
            }
            if (ladder == null) sb.Append("Không có giếng thang chuẩn nên không ước lượng được kích thước. ");
            return sb.ToString();
        }

        static double EstimateBp(GelTankController tank, GelLane ladder, double d)
        {
            // các băng thang theo quãng đường tăng dần (bp giảm dần)
            var pts = new List<(double d, double logBp)>();
            foreach (var b in ladder.bands) pts.Add((tank.DistanceCm(b.bp), Math.Log10(b.bp)));
            pts.Sort((a, b) => a.d.CompareTo(b.d));
            if (pts.Count < 2) return 0;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                if (d <= pts[i + 1].d || i == pts.Count - 2)
                {
                    double t = (d - pts[i].d) / Math.Max(1e-6, pts[i + 1].d - pts[i].d);
                    double est = Math.Pow(10, pts[i].logBp + t * (pts[i + 1].logBp - pts[i].logBp));
                    return Math.Round(est / 10.0) * 10.0;
                }
            }
            return 0;
        }

        public override void Refresh()
        {
            var tank = Tank;
            var sb = new StringBuilder("<b>BUỒNG CHỤP GEL</b>\n");
            if (tank == null || !tank.gelPlaced) sb.Append("• Chưa có gel: đổ gel, đặt vào bể, nạp mẫu và chạy điện di trước.\n");
            else
            {
                sb.Append($"• Gel agarose {BioFeedback.FormatVi(tank.agarosePercent, "0.0")} %, {tank.lanes.Count} giếng đã nạp, ");
                sb.Append(tank.hasCompletedRun ? $"đã chạy {tank.volts:0} V {tank.runMinutes:0} phút\n" : tank.running ? "đang chạy\n" : "chưa chạy\n");
            }
            sb.Append($"• Nguồn sáng: {(lightOn ? "xanh lam 470 nm + kính lọc cam (an toàn hơn UV)" : "tắt")}\n");
            if (imaged) sb.Append("• Phân tích: ").Append(analysis);
            S.SetResults(sb.ToString());
        }

        public override void DrawSchematic(Painter p, float time)
        {
            p.Clear(new Color(0.05f, 0.06f, 0.07f));
            p.FillRect(300, 40, 380, 270, new Color(0.13f, 0.14f, 0.16f));
            p.FillRect(340, 70, 300, 30, lightOn ? new Color(0.2f, 0.45f, 1f) : new Color(0.15f, 0.17f, 0.22f));
            p.FillRect(380, 110, 220, 120, new Color(0.75f, 0.85f, 0.8f, 0.35f));
            p.FillRect(440, 250, 100, 40, new Color(0.95f, 0.55f, 0.15f, 0.7f));
            S.SchemLabel("gd_cam", "Camera + kính lọc cam", 490, 300, 13, Color.white);
            S.SchemLabel("gd_gel", "Bản gel", 490, 170, 13, Color.black);
            S.SchemLabel("gd_led", lightOn ? "Đèn LED xanh 470 nm: BẬT" : "Đèn LED xanh: tắt", 490, 85, 13, Color.white);
        }

        public override void DrawPlot(Painter p)
        {
            p.Clear(new Color(0.02f, 0.02f, 0.03f));
            var tank = Tank;
            if (tank == null || !tank.gelPlaced || !lightOn) return;
            int n = GelCastingTray.Wells;
            float left = 40, right = p.W - 40, topY = p.H - 40, usable = p.H - 90;
            float laneW = (right - left) / n;
            double dMax = Math.Max(0.5, tank.DistanceCm(80));
            for (int i = 0; i < n; i++)
            {
                float cx = left + laneW * (i + 0.5f);
                p.FillRect(cx - laneW * 0.32f, topY, laneW * 0.64f, 8, new Color(0.22f, 0.22f, 0.25f));  // giếng
                if (i >= tank.lanes.Count) continue;
                var lane = tank.lanes[i];
                if (lane.floated) continue;
                foreach (var b in lane.bands)
                {
                    double d = tank.hasCompletedRun || tank.running ? tank.DistanceCm(b.bp) : 0;
                    float y = topY - 6 - (float)(d / dMax) * usable;
                    float glow = Mathf.Clamp01((float)(Math.Log10(Math.Max(1, b.ng)) / 2.0));
                    if (glow <= 0.05f) continue;
                    var c = Color.Lerp(new Color(0.05f, 0.08f, 0.05f), new Color(0.75f, 1f, 0.75f), glow);
                    float h = b.smear ? 26 : 5;
                    p.FillRect(cx - laneW * 0.3f, y - h / 2, laneW * 0.6f, h, c);
                }
            }
        }
    }

    // =========================================================================
    // Bộ tách chiết ADN chuối (đảo tách chiết)
    // =========================================================================

    // =========================================================================
    // Máy quang phổ OD600 / ADN và cuvet
    // =========================================================================

    public class Od600Program : InstrumentProgram
    {
        public override string Code => "may_do_od600";
        public Od600Controller controller;

        public override void Build()
        {
            S.AddButton("Bật máy, chọn 600 nm", 0, 0, () => S.Done("bat_may"), false, 2);
            S.AddButton("Đo mẫu trắng (LB)", 2, 0, () => { if (!S.Require("do_blank")) return; controller.blankDone = true; S.Done("do_blank"); S.Say("Đã chỉnh 0,000 với môi trường LB sạch.", 1); });
            S.AddButton("Nạp dịch khuẩn vào cuvet", 0, 1, () => { if (!S.Require("chuan_bi_mau")) return; S.Done("chuan_bi_mau"); }, false, 2);
            S.AddButton("Đo điểm tiếp (+1 giờ)", 0, 2, MeasureNext, true, 2);
            S.AddButton("Làm lại", 2, 2, () => { controller.curve.Clear(); controller.hours = 0; });
            S.SetPlotAxes("ĐƯỜNG CONG SINH TRƯỞNG E. coli (37 °C)", "Thời gian (giờ)", "OD600", "0", "12", "0", "2");
            Refresh();
        }

        void MeasureNext()
        {
            if (!S.Require("do_mau")) return;
            double h = controller.curve.Count == 0 ? 0 : controller.hours + 1;
            if (h > 12) { S.Say("Đã đo đủ 12 giờ. Bấm Làm lại để đo đường cong mới.", 0); return; }
            double od = controller.MeasureAt(h);
            S.Done("do_mau");
            S.Say($"t = {h:0} giờ: OD600 = {BioFeedback.FormatVi(od, "0.000")}", 1);
        }

        public override void Refresh()
        {
            S.SetResults("<b>MÁY QUANG PHỔ OD600</b>\n" +
                $"• Mẫu trắng: {(controller.blankDone ? "đã đo (0,000)" : "<color=#FF8866>chưa đo</color>")}\n" +
                $"• Điểm cuối: t = {controller.hours:0} giờ, OD600 = {BioFeedback.FormatVi(controller.measuredOd600, "0.000")} (≈ {controller.CellsPerMl:0.0e+0} tế bào/mL)\n" +
                "• Pha tiềm phát → pha lũy thừa (OD tăng nhanh) → pha cân bằng (OD ~1,2 – 1,5).");
        }

        public override void DrawSchematic(Painter p, float time)
        {
            p.Clear(new Color(0.05f, 0.08f, 0.12f));
            p.Disc(120, 175, 40, new Color(1f, 0.9f, 0.5f));
            p.Line(160, 175, 880, 175, new Color(1f, 0.6f, 0.2f, 0.9f), 4);
            p.FillRect(470, 120, 50, 110, new Color(0.85f, 0.85f, 0.6f, 0.6f));
            p.FillRect(840, 140, 60, 70, new Color(0.3f, 0.75f, 0.5f));
            S.SchemLabel("od_l", "Nguồn sáng", 120, 240, 13, Color.white);
            S.SchemLabel("od_c", "Cuvet 1 cm", 495, 250, 13, Color.white);
            S.SchemLabel("od_d", "Đầu thu", 870, 230, 13, Color.white);
        }

        public override void DrawPlot(Painter p)
        {
            InstrumentScreen.PlotFrame(p);
            foreach (var pt in controller.curve)
            {
                var m = InstrumentScreen.Map(p, pt.x, pt.y, 0, 12, 0, 2);
                p.Disc(m.x, m.y, 5, new Color(0.3f, 0.85f, 0.6f));
            }
        }
    }

}
