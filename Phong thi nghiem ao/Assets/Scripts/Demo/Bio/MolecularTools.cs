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

        public static Micropipette Pipette(BioTool tool) => tool != null ? tool.GetComponent<Micropipette>() : null;
    }

    // =========================================================================
    // Ống thuốc thử gốc (cắm cố định trong khay đá)
    // =========================================================================

    [DisallowMultipleComponent]
    public class ReagentTube : MonoBehaviour, IInteractable, IBioTarget
    {
        public string reagentCode = MolCodes.Water;
        public float stockUl = 500f;
        [Tooltip("Vết chất lạ lọt vào ống gốc do dùng lại đầu côn bẩn")]
        public List<string> contaminants = new List<string>();

        public string TargetName => MolCodes.Name(reagentCode);
        public string UseHint => $"{TargetName} (còn {BioFeedback.FormatVi(stockUl, "0")} µL{(contaminants.Count > 0 ? " · ĐÃ NHIỄM" : "")}) · Cầm micropipet đã gắn đầu côn rồi bấm vào ống để hút";

        public void Use(GameObject user) => MolUi.Say(UseHint);

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            var p = MolUi.Pipette(tool);
            if (p == null) return false;
            prompt = !p.tipAttached ? "Chưa gắn đầu côn" : p.load.IsEmpty ? $"Hút {p.VolumeText} {TargetName}" : "Đầu côn đang có dịch: nhả vào ống phản ứng trước";
            return true;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }
        public void OnToolApplyComplete(BioTool tool) => MolUi.Pipette(tool)?.AspirateStock(this);

        /// <summary>Hỗn hợp của một lần hút v µL từ ống gốc (kèm vết nhiễm nếu có).</summary>
        public void Draw(MolMix into, float v)
        {
            float take = Mathf.Min(v, stockUl);
            into.Add(reagentCode, take);
            foreach (var c in contaminants) into.Add(c, take * 0.002f);  // vết rất nhỏ nhưng PCR vẫn nhân lên được
            if (reagentCode == MolCodes.Ladder)
                foreach (int bp in MolCodes.LadderBp)
                    into.AddBand(new BioDnaBandInfo { bp = bp, ng = take * MolCodes.LadderNgPerBandPerUl * (bp == 500 ? 2 : 1) });
            stockUl -= take;
        }
    }

    // =========================================================================
    // Micropipet (P10 / P200 / P1000)
    // =========================================================================

    [DisallowMultipleComponent]
    public class Micropipette : MonoBehaviour, IScrollAdjustable
    {
        public string pipetteToolId = "micropipet_p10"; // micropipet_p10 | micropipet_p200 | micropipet_p1000
        public float minUl = 0.5f, maxUl = 10f, stepUl = 0.1f;
        public float volumeSetUl = 10f;
        public bool tipAttached;
        public MolMix load = new MolMix();
        /// <summary>Mọi chất đầu côn hiện tại đã chạm vào (để phát hiện dùng lại đầu côn gây nhiễm chéo).</summary>
        public readonly HashSet<string> tipHistory = new HashSet<string>();

        public string Model => pipetteToolId == "micropipet_p1000" ? "P1000" : pipetteToolId == "micropipet_p200" ? "P200" : "P10";
        public string TipKind => pipetteToolId == "micropipet_p1000" ? "xanh" : "vang";
        public string VolumeText => BioFeedback.FormatVi(volumeSetUl, maxUl <= 10f ? "0.0" : "0") + " µL";

        void Awake() => ConfigureRangeById(pipetteToolId);

        public void ConfigureRangeById(string id)
        {
            pipetteToolId = id;
            if (id == "micropipet_p1000") { minUl = 100f; maxUl = 1000f; stepUl = 10f; }
            else if (id == "micropipet_p200") { minUl = 20f; maxUl = 200f; stepUl = 1f; }
            else { pipetteToolId = "micropipet_p10"; minUl = 0.5f; maxUl = 10f; stepUl = 0.5f; }
            volumeSetUl = Mathf.Clamp(volumeSetUl, minUl, maxUl);
            Refresh();
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            if (!load.IsEmpty) { MolUi.Say("Không vặn núm thể tích khi đầu côn đang chứa dịch", true); return; }
            volumeSetUl = Mathf.Clamp(Mathf.Round((volumeSetUl + Mathf.Sign(delta) * stepUl) / stepUl) * stepUl, minUl, maxUl);
            BioFeedback.PlayClick();
            Refresh();
            MolUi.Say($"{Model}: đặt {VolumeText} (dải {BioFeedback.FormatVi(minUl, "0.#")} – {BioFeedback.FormatVi(maxUl, "0")} µL)");
        }

        public bool AttachTip(TipBox box)
        {
            if (tipAttached) { MolUi.Say("Đã có đầu côn. Thải đầu côn cũ vào thùng rác đầu côn trước khi lấy cái mới.", true); return false; }
            if (box.tipKind != TipKind)
            {
                BioFeedback.RecordMistake(pipetteToolId, "gan_dau_con_sai_co", $"Gắn đầu côn {box.tipKind} vào {Model}",
                    "Đầu côn sai cỡ không kín khí: hút thiếu hoặc rơi đầu côn. P10 / P200 dùng côn vàng, P1000 dùng côn xanh.");
                return false;
            }
            if (!box.TakeOne()) return false;
            tipAttached = true;
            load.Clear();
            tipHistory.Clear();
            BioFeedback.PlayClick();
            Refresh();
            MolUi.Say($"Đã gắn đầu côn vô trùng vào {Model}");
            return true;
        }

        public void EjectTip(TipWaste waste)
        {
            if (!tipAttached) return;
            tipAttached = false;
            load.Clear();
            tipHistory.Clear();
            waste.Receive();
            BioFeedback.PlayClick();
            Refresh();
            MolUi.Say($"Đã đẩy đầu côn của {Model} vào thùng rác đầu côn");
        }

        public void AspirateStock(ReagentTube stock)
        {
            if (!tipAttached)
            {
                BioFeedback.RecordMistake(pipetteToolId, "hut_khong_gan_dau_con", "Hút dịch khi chưa gắn đầu côn",
                    "Dịch tràn vào thân piston: nhiễm bẩn và hỏng micropipet. Luôn gắn đầu côn mới trước khi hút.");
                return;
            }
            if (!load.IsEmpty) { MolUi.Say("Đầu côn đang có dịch: nhả vào ống phản ứng trước", true); return; }
            // đầu côn đã chạm chất khác mà lại nhúng vào ống gốc: ống gốc bị nhiễm (lỗi kinh điển làm đối chứng âm có băng)
            bool dirty = false;
            foreach (var c in tipHistory)
                if (c != stock.reagentCode && !stock.contaminants.Contains(c)) { stock.contaminants.Add(c); dirty = true; }
            if (dirty)
                BioFeedback.RecordMistake(pipetteToolId, "dung_lai_dau_con", $"Dùng lại đầu côn đã chạm chất khác để hút {stock.TargetName}",
                    "Ống thuốc thử gốc đã bị nhiễm vết ADN / thuốc thử lạ: mọi ống pha sau đó đều nhiễm, đối chứng âm sẽ có băng. Mỗi thuốc thử một đầu côn mới.");
            if (stock.stockUl < volumeSetUl) { MolUi.Say($"{stock.TargetName} sắp hết", true); }
            stock.Draw(load, volumeSetUl);
            tipHistory.Add(stock.reagentCode);
            BioFeedback.PlayClick();
            Refresh();
            MolUi.Say($"Đã hút {VolumeText} {stock.TargetName}");
        }

        public void AspirateTube(MicroTube tube)
        {
            if (!tipAttached) { MolUi.Say("Gắn đầu côn trước khi hút", true); return; }
            if (!load.IsEmpty) { DispenseTube(tube); return; }
            float avail = tube.mix.TotalUl;
            if (avail < 0.2f) { MolUi.Say($"Ống {tube.labelText} trống", true); return; }
            float take = Mathf.Min(volumeSetUl, avail);
            load.TakeFrom(tube.mix, take, true);
            foreach (var c in tube.mix.codes) tipHistory.Add(c);
            tipHistory.Add("ong_" + tube.labelText);
            tube.Changed();
            BioFeedback.PlayClick();
            Refresh();
            MolUi.Say($"Đã hút {BioFeedback.FormatVi(take, "0.0")} µL từ ống {tube.labelText}{(take < volumeSetUl - 0.05f ? " (ống không đủ thể tích đã đặt)" : "")}");
        }

        public void DispenseTube(MicroTube tube)
        {
            if (load.IsEmpty) return;
            float v = load.TotalUl;
            tube.mix.TakeFrom(load, v, true);
            load.Clear();
            foreach (var c in tube.mix.codes) tipHistory.Add(c);
            tipHistory.Add("ong_" + tube.labelText);
            tube.Changed();
            BioFeedback.PlayClick();
            Refresh();
            MolUi.Say($"Đã nhả {BioFeedback.FormatVi(v, "0.0")} µL vào ống {tube.labelText} (ấn tới nấc 2 để đẩy hết giọt cuối)");
        }

        /// <summary>Nhả toàn bộ đầu côn ra (vào giếng gel...); trả về hỗn hợp vừa nhả.</summary>
        public MolMix DispenseAll()
        {
            var m = new MolMix();
            m.TakeFrom(load, load.TotalUl, true);
            load.Clear();
            tipHistory.Add("gel");
            BioFeedback.PlayClick();
            Refresh();
            return m;
        }

        public void Refresh()
        {
            string tip = tipAttached ? $"đầu côn {TipKind}" : "chưa gắn đầu côn";
            string content = load.IsEmpty ? "" : " · đang chứa " + load.Describe();
            MolUi.Info(this, $"{Model} đặt {VolumeText} · {tip}{content}. Cầm lên rồi lăn chuột để chỉnh thể tích; trỏ vào hộp đầu côn để gắn, vào ống để hút / nhả, vào thùng rác đầu côn để thải.");
        }
    }

    [DisallowMultipleComponent]
    public class TipBox : MonoBehaviour, IInteractable, IBioTarget
    {
        public string tipKind = "vang";   // vang: P10 / P200 · xanh: P1000
        public int remaining = 96;

        public string TargetName => $"Hộp đầu côn {tipKind}";
        public string UseHint => $"Hộp 96 đầu côn {tipKind} vô trùng (còn {remaining}) · Cầm micropipet rồi bấm vào hộp để gắn đầu côn";
        public void Use(GameObject user) => MolUi.Say(UseHint);

        public bool TakeOne()
        {
            if (remaining <= 0) { MolUi.Say("Hộp đầu côn đã hết", true); return false; }
            remaining--;
            return true;
        }

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            var p = MolUi.Pipette(tool);
            if (p == null) return false;
            prompt = p.tipAttached ? "Đã có đầu côn (thải đầu côn cũ trước)" : "Ấn micropipet vào hộp để gắn đầu côn";
            return true;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }
        public void OnToolApplyComplete(BioTool tool) => MolUi.Pipette(tool)?.AttachTip(this);
    }

    [DisallowMultipleComponent]
    public class TipWaste : MonoBehaviour, IInteractable, IBioTarget
    {
        public int count;
        public string TargetName => "Thùng rác đầu côn";
        public string UseHint => $"Thùng rác đầu côn ({count} đầu côn đã thải) · Cầm micropipet rồi bấm vào đây để đẩy đầu côn";
        public void Use(GameObject user) => MolUi.Say(UseHint);
        public void Receive() => count++;

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            var p = MolUi.Pipette(tool);
            if (p == null || !p.tipAttached) return false;
            prompt = "Bấm nút đẩy đầu côn";
            return true;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }
        public void OnToolApplyComplete(BioTool tool) => MolUi.Pipette(tool)?.EjectTip(this);
    }

    // =========================================================================
    // Ống phản ứng (ống PCR 0,2 mL / eppendorf 1,5 mL)
    // =========================================================================

    [DisallowMultipleComponent]
    public class MicroTube : MonoBehaviour, IBioTarget
    {
        public string tubeTypeId = "ong_pcr";  // ong_pcr | ong_eppendorf
        public string labelText = "1";
        public MolMix mix = new MolMix();
        public bool amplified;
        public int pcrCycles;

        public float MaxUl => tubeTypeId == "ong_pcr" ? 200f : 1500f;
        public string TargetName => $"Ống {labelText}";

        void Start() => Changed();

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            var p = MolUi.Pipette(tool);
            if (p == null) return false;
            if (!p.tipAttached) prompt = "Chưa gắn đầu côn";
            else if (!p.load.IsEmpty) prompt = $"Nhả {BioFeedback.FormatVi(p.load.TotalUl, "0.0")} µL vào ống {labelText}";
            else if (mix.IsEmpty) prompt = $"Ống {labelText} đang trống";
            else prompt = $"Hút {p.VolumeText} từ ống {labelText}";
            return true;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }

        public void OnToolApplyComplete(BioTool tool)
        {
            var p = MolUi.Pipette(tool);
            if (p == null || !p.tipAttached) return;
            if (!p.load.IsEmpty)
            {
                if (mix.TotalUl + p.load.TotalUl > MaxUl) { MolUi.Say($"Ống {labelText} sẽ tràn", true); return; }
                p.DispenseTube(this);
            }
            else p.AspirateTube(this);
        }

        public void Changed()
        {
            string kind = tubeTypeId == "ong_pcr" ? "Ống PCR 0,2 mL" : "Ống eppendorf 1,5 mL";
            MolUi.Info(this, $"{kind} – nhãn \"{labelText}\": {mix.Describe()}{(amplified ? $" · đã chạy PCR {pcrCycles} chu kỳ" : "")}.");
        }

        /// <summary>Thiết lập PCR từ đúng thành phần trong ống (công thức chuẩn 20 µL: 10 Master Mix + 1 mồi + 1 khuôn + 8 nước).</summary>
        public PcrSetup BuildPcrSetup(double annealC, int cycles, bool lidHeated)
        {
            float total = Mathf.Max(0.01f, mix.TotalUl);
            float mmFrac = mix.Get(MolCodes.MasterMix) / total;     // 0,5 là đúng 1×
            float primerUl = mix.Get(MolCodes.Primers);
            float gfpUl = mix.Get(MolCodes.Template);
            var s = new PcrSetup
            {
                hasBuffer = mmFrac >= 0.3f,
                hasDntp = mmFrac >= 0.3f,
                hasTaq = mmFrac >= 0.3f,
                hasPrimers = primerUl / total >= 0.015f,
                hasTemplate = gfpUl > 0.0001f,          // ADN chuối không mang gen GFP: mồi không có chỗ bám
                templateCopies = gfpUl * 2.0e5,          // 1 ng/µL plasmid ~ 2·10⁵ bản sao trong vùng mồi bám hiệu quả
                annealC = annealC,
                primerTm = 60.0,
                cycles = cycles,
                targetBp = DnaCloning.PcrProductBp,
                lidHeated = lidHeated,
                enzymeWarmMinutes = 0,
                productContamination = false,
            };
            return s;
        }
    }

    [DisallowMultipleComponent]
    public class IceBox : MonoBehaviour, IInteractable
    {
        public string UseHint => "Khay đá 0 – 4 °C giữ Master Mix (có enzyme Taq), mồi, ADN khuôn. Enzyme để ở nhiệt độ phòng lâu sẽ giảm hoạt tính.";
        public void Use(GameObject user) => MolUi.Say(UseHint);
    }

    // =========================================================================
    // Tủ thao tác PCR (tiền PCR)
    // =========================================================================

    [DisallowMultipleComponent]
    public class PcrHoodController : MonoBehaviour, IInteractable
    {
        public bool uvRunning;
        public bool sterilized;
        float uvT;
        const float UvRealSeconds = 6f;   // 15 phút chiếu UV tua nhanh còn 6 giây

        public string UseHint => uvRunning ? $"Đang chiếu UV khử ADN lạ ({Mathf.RoundToInt(uvT / UvRealSeconds * 15)}/15 phút)…"
            : sterilized ? "Tủ thao tác PCR đã chiếu UV 15 phút, sạch ADN lạ. Pha hỗn hợp PCR trong tủ." : "Bấm: chiếu UV 15 phút trước khi pha hỗn hợp PCR (phá ADN nhiễm trên bề mặt)";

        public void Use(GameObject user)
        {
            if (uvRunning) return;
            uvRunning = true;
            uvT = 0;
            BioFeedback.PlayButton();
            MolUi.Say("Đóng cửa tủ, bật đèn UV-C 15 phút. Không nhìn trực tiếp vào đèn UV.");
        }

        void Update()
        {
            if (!uvRunning) return;
            uvT += Time.deltaTime;
            if (uvT < UvRealSeconds) return;
            uvRunning = false;
            sterilized = true;
            BioFeedback.PlaySuccess();
            MolUi.Say("Đã chiếu UV xong: tắt UV, mở cửa tủ để pha hỗn hợp PCR");
        }
    }

    // =========================================================================
    // Máy PCR (luân nhiệt)
    // =========================================================================

    [DisallowMultipleComponent]
    public class ThermalCyclerController : MonoBehaviour, IInteractable, IBioTarget
    {
        public const int MaxTubes = 8;
        public readonly List<MicroTube> loaded = new List<MicroTube>();
        public double annealC = 55.0;
        public int cycles = 30;
        public bool lidHeated = true;
        public bool running;
        public bool finished;
        public double simSeconds;
        public float speed = 120f;          // 1 giờ chạy máy ≈ 30 giây thật
        public string machineName = "Máy PCR";
        PcrProgram program;

        public string TargetName => machineName;
        public string UseHint => running ? $"{machineName}: đang chạy ({Phase})" : $"{machineName} ({loaded.Count} ống) · Bấm: mở màn hình điều khiển";

        public string Phase
        {
            get
            {
                var (phase, cyc, _) = PcrProfile.At(simSeconds, annealC, cycles);
                return cyc > 0 ? $"chu kỳ {cyc}/{cycles}, {phase}" : phase;
            }
        }

        public void Use(GameObject user)
        {
            program ??= new PcrProgram { cycler = this };
            InstrumentScreen.Open(null, program, null);
        }

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            var tube = tool != null ? tool.GetComponent<MicroTube>() : null;
            if (tube == null) return false;
            if (running) prompt = "Máy đang chạy, không mở nắp";
            else if (tube.tubeTypeId != "ong_pcr") prompt = "Ống 1,5 mL không vừa giếng 0,2 mL của khối nhiệt";
            else if (loaded.Count >= MaxTubes) prompt = "Khối nhiệt đã đầy";
            else prompt = $"Đặt ống {tube.labelText} vào khối nhiệt";
            return true;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }

        public void OnToolApplyComplete(BioTool tool)
        {
            var tube = tool != null ? tool.GetComponent<MicroTube>() : null;
            if (tube == null || running || tube.tubeTypeId != "ong_pcr" || loaded.Count >= MaxTubes) return;
            if (tube.mix.IsEmpty) MolUi.Say($"Ống {tube.labelText} còn trống: vẫn đặt vào nhưng sẽ không có sản phẩm", true);
            var hand = HandController.Active;
            if (hand == null || hand.Held == null || hand.Held.gameObject != tube.gameObject) return;
            hand.ReleaseHeldInto(transform, SlotPosition(loaded.Count), transform.rotation);
            AddTube(tube);
        }

        void AddTube(MicroTube tube)
        {
            loaded.Add(tube);
            finished = false;
            var g = tube.GetComponent<Grabbable>();
            if (g != null) g.HeldChanged += held => { if (held) RemoveTube(tube); };
            BioFeedback.PlayClick();
            MolUi.Say($"Đã đặt ống {tube.labelText} vào {machineName} ({loaded.Count} ống). Bấm vào máy để cài chu trình và chạy.");
            program?.S?.Done("nap_ong");
        }

        void RemoveTube(MicroTube tube)
        {
            if (!loaded.Remove(tube)) return;
            for (int i = 0; i < loaded.Count; i++) loaded[i].transform.position = SlotPosition(i);
        }

        /// <summary>Giếng trên khối nhiệt (nhìn thấy qua nắp mở): hàng ngang ở mặt trên máy.</summary>
        Vector3 SlotPosition(int i) => transform.TransformPoint(new Vector3(-0.09f + i * 0.026f, 0.233f, -0.04f));

        public void StartRun()
        {
            if (running) return;
            if (loaded.Count == 0) { program?.S?.Say("Chưa có ống nào trong khối nhiệt", 2); return; }
            running = true;
            finished = false;
            simSeconds = 0;
            BioFeedback.PlayButton();
            MolUi.MachineStatus("may_pcr", "dang_chay", $"{machineName}: bắt đầu {cycles} chu kỳ", 0f);
        }

        void Update()
        {
            if (!running) return;
            double total = PcrProfile.TotalSeconds(cycles);
            int before = PcrProfile.At(simSeconds, annealC, cycles).cycle;
            simSeconds += Time.deltaTime * speed;
            int after = PcrProfile.At(simSeconds, annealC, cycles).cycle;
            if (after != before && after % 5 == 0)
                MolUi.MachineStatus("may_pcr", "dang_chay", $"{machineName}: chu kỳ {after}/{cycles}", (float)(simSeconds / total));
            if (simSeconds >= total) Finish();
        }

        public void SkipToEnd() { if (running) simSeconds = PcrProfile.TotalSeconds(cycles); }

        void Finish()
        {
            running = false;
            finished = true;
            var sb = new StringBuilder();
            var bands = new List<DnaBand>();
            foreach (var tube in loaded)
            {
                // vết khuôn lọt vào do dùng lại đầu côn (vd ống đối chứng âm) cũng có mặt trong mix và được nhân lên
                var setup = tube.BuildPcrSetup(annealC, cycles, lidHeated);
                Pcr.Run(setup, tube.labelText.GetHashCode(), bands);
                tube.amplified = true;
                tube.pcrCycles = cycles;
                foreach (var b in bands) tube.mix.AddBand(new BioDnaBandInfo { bp = b.bp, ng = b.ng, smear = b.smear });
                tube.Changed();
                sb.Append($"Ống {tube.labelText}: ").Append(bands.Count == 0 ? "không có sản phẩm" : string.Join(", ", bands.ConvertAll(b => $"{b.bp} bp ({b.ng:0} ng)"))).Append(". ");
            }
            BioFeedback.PlaySuccess();
            MolUi.Say($"{machineName} chạy xong, giữ 4 °C. Lấy ống ra để điện di kiểm tra.");
            MolUi.MachineStatus("may_pcr", "san_sang", $"{machineName}: xong {cycles} chu kỳ. {sb}", 1f);
            BioFeedback.LogAction("may_pcr", "hoan_tat_pcr", sb.ToString());
            program?.OnFinished(sb.ToString());
        }
    }

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

    [DisallowMultipleComponent]
    public class MicrowaveOven : MonoBehaviour, IInteractable, IBioTarget
    {
        public bool agaroseMelted;
        public float agarosePercent = 1.0f;

        public string TargetName => "Lò vi sóng";
        public string UseHint => agaroseMelted ? $"Bình agarose {BioFeedback.FormatVi(agarosePercent, "0.0")} % đã tan trong suốt (~60 °C). Cầm khay đúc gel rồi bấm vào lò để đổ gel."
            : "Bấm: đun bình agarose 1 % trong đệm TAE (nắp đậy hờ) đến khi tan trong suốt";

        public void Use(GameObject user)
        {
            if (agaroseMelted) { MolUi.Say(UseHint); return; }
            agaroseMelted = true;
            BioFeedback.PlaySuccess();
            MolUi.Say("Đã đun agarose 1 % tan trong suốt (đun ngắt quãng, lắc đều, nắp đậy hờ để không nổ bình). Để nguội ~60 °C rồi đổ khuôn.");
        }

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            var tray = tool != null ? tool.GetComponent<GelCastingTray>() : null;
            if (tray == null) return false;
            prompt = !agaroseMelted ? "Chưa đun agarose (bấm vào lò bằng tay không)" : tray.gelReady ? "Khay đã có gel" : "Đổ agarose vào khay đúc, cắm lược";
            return true;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0.8f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }

        public void OnToolApplyComplete(BioTool tool)
        {
            var tray = tool != null ? tool.GetComponent<GelCastingTray>() : null;
            if (tray == null || !agaroseMelted || tray.gelReady) return;
            tray.Cast(agarosePercent);
        }
    }

    [DisallowMultipleComponent]
    public class GelCastingTray : MonoBehaviour
    {
        public bool gelReady;
        public float agarosePercent = 1.0f;
        public const int Wells = 8;
        GameObject gelVisual;

        public void Cast(float pct)
        {
            gelReady = true;
            agarosePercent = pct;
            if (gelVisual == null)
            {
                gelVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gelVisual.name = "Bản gel đã đông";
                UnityEngine.Object.Destroy(gelVisual.GetComponent<Collider>());
                gelVisual.transform.SetParent(transform, false);
                var vis = transform.Find("Visual");
                float top = vis != null ? vis.localPosition.y + vis.localScale.y * 0.5f : 0.03f;
                gelVisual.transform.localPosition = new Vector3(0, top + 0.002f, 0);
                gelVisual.transform.localScale = new Vector3(0.11f, 0.006f, 0.09f);
                var r = gelVisual.GetComponent<Renderer>();
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                r.material = new Material(shader) { color = new Color(0.8f, 0.9f, 0.85f, 0.85f) };
            }
            BioFeedback.PlaySuccess();
            MolUi.Info(this, $"Khay đúc có bản gel agarose {BioFeedback.FormatVi(pct, "0.0")} % đã đông, lược 8 giếng đã rút. Cầm khay đặt vào bể điện di (giếng ở phía cực âm – dây đen).");
            MolUi.Say("Đã đổ gel, chờ đông 20 phút rồi rút lược thẳng đứng: 8 giếng. Đặt gel vào bể điện di.");
        }
    }

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

    [DisallowMultipleComponent]
    public class GelTankController : MonoBehaviour, IInteractable, IBioTarget
    {
        public bool gelPlaced;
        public float agarosePercent = 1.0f;
        public readonly List<GelLane> lanes = new List<GelLane>();
        public bool running;
        public bool hasCompletedRun;
        public double runMinutes;
        public double volts = 100;
        public PowerSupplyController psu;
        const float RealSecondsPer30Min = 12f;

        readonly List<Transform> dyeMarks = new List<Transform>();
        readonly List<Vector3> dyeHome = new List<Vector3>();
        Transform gelSlab;

        public string TargetName => "Bể điện di";
        public string UseHint => !gelPlaced ? "Bể điện di đã có đệm TAE 1×. Cầm khay gel đã đông rồi bấm vào bể để đặt gel."
            : running ? $"Đang điện di {volts:0} V: {runMinutes:0}/{psu?.timerMinutes ?? 30} phút"
            : $"Gel {BioFeedback.FormatVi(agarosePercent, "0.0")} % · {lanes.Count}/{GelCastingTray.Wells} giếng đã nạp{(hasCompletedRun ? " · đã chạy xong" : "")}. Cầm micropipet có mẫu rồi bấm vào bể để nạp giếng kế tiếp.";

        void Awake()
        {
            gelSlab = transform.Find("Bản gel");
            foreach (Transform c in transform)
                if (c.name == "Vạch thuốc nhuộm") { dyeMarks.Add(c); dyeHome.Add(c.localPosition); }
            if (psu == null) psu = GetComponentInChildren<PowerSupplyController>();
            ShowGel(false);
        }

        void ShowGel(bool on)
        {
            if (gelSlab != null) gelSlab.gameObject.SetActive(on);
            foreach (var d in dyeMarks) d.gameObject.SetActive(on && lanes.Count > 0);
        }

        public void Use(GameObject user) => MolUi.Say(UseHint);

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            if (tool == null) return false;
            var tray = tool.GetComponent<GelCastingTray>();
            if (tray != null)
            {
                prompt = gelPlaced ? "Bể đã có gel" : !tray.gelReady ? "Khay chưa có gel (đổ agarose ở lò vi sóng trước)" : "Đặt gel vào bể, giếng ở phía cực âm";
                return true;
            }
            var p = MolUi.Pipette(tool);
            if (p == null) return false;
            prompt = !gelPlaced ? "Chưa có gel trong bể" : running ? "Đang chạy điện di" : p.load.IsEmpty ? "Đầu côn trống: hút mẫu trước"
                : lanes.Count >= GelCastingTray.Wells ? "Đã nạp đủ 8 giếng" : $"Nạp {BioFeedback.FormatVi(p.load.TotalUl, "0.0")} µL vào giếng {lanes.Count + 1}";
            return true;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }

        public void OnToolApplyComplete(BioTool tool)
        {
            if (tool == null) return;
            var tray = tool.GetComponent<GelCastingTray>();
            if (tray != null)
            {
                if (gelPlaced || !tray.gelReady) return;
                var hand = HandController.Active;
                if (hand == null || hand.Held == null || hand.Held.gameObject != tray.gameObject) return;
                hand.ReleaseHeldInto(transform, transform.TransformPoint(new Vector3(0, 0.03f, 0.2f)), transform.rotation);
                tray.gameObject.SetActive(false);  // gel đã nằm trong bể (dùng bản gel của mô hình bể)
                gelPlaced = true;
                agarosePercent = tray.agarosePercent;
                lanes.Clear();
                hasCompletedRun = false;
                runMinutes = 0;
                ShowGel(true);
                BioFeedback.PlayClick();
                MolUi.Say("Đã đặt gel vào bể, đệm TAE ngập gel 3 – 5 mm. Nạp thang chuẩn vào giếng 1, mẫu vào các giếng sau.");
                return;
            }
            var p = MolUi.Pipette(tool);
            if (p == null || !gelPlaced || running || p.load.IsEmpty || lanes.Count >= GelCastingTray.Wells) return;
            var m = p.DispenseAll();
            var lane = new GelLane { label = Label(m) };
            if (!m.HasLoadingDye)
            {
                lane.floated = true;
                BioFeedback.RecordMistake("be_dien_di", "nap_mau_khong_co_chat_nap", "Nạp mẫu không trộn đệm nạp mẫu (loading dye)",
                    "Mẫu ADN nhẹ hơn đệm nên nổi lên khỏi giếng và trôi mất. Trộn mẫu với đệm nạp mẫu 6× (có glixerol nặng) trước khi nạp.");
            }
            else lane.bands.AddRange(m.dna);
            lanes.Add(lane);
            hasCompletedRun = false;
            runMinutes = 0;
            ShowGel(true);
            MolUi.Say($"Đã nạp giếng {lanes.Count}: {lane.label}. Nhúng đầu côn vừa chạm miệng giếng, không chọc thủng đáy giếng.");
        }

        static string Label(MolMix m)
        {
            if (m.Get(MolCodes.Ladder) > 0.05f) return "Thang 100 bp";
            if (m.dna.Count == 0) return $"{BioFeedback.FormatVi(m.TotalUl, "0")} µL mẫu (không có ADN thấy được)";
            return $"{BioFeedback.FormatVi(m.TotalUl, "0")} µL mẫu ADN";
        }

        public bool StartRun(double v)
        {
            if (!gelPlaced) { MolUi.Say("Chưa có gel trong bể", true); return false; }
            if (lanes.Count == 0) { MolUi.Say("Chưa nạp giếng nào", true); return false; }
            volts = v;
            running = true;
            if (hasCompletedRun) { runMinutes = 0; hasCompletedRun = false; }
            MolUi.MachineStatus("be_dien_di", "dang_chay", $"Điện di {v:0} V", 0f);
            return true;
        }

        public void StopRun()
        {
            if (!running) return;
            running = false;
            MolUi.Say($"Đã tắt nguồn sau {runMinutes:0} phút điện di.");
        }

        void Update()
        {
            if (!running) return;
            double timer = psu != null ? psu.timerMinutes : 30;
            runMinutes += Time.deltaTime * 30.0 / RealSecondsPer30Min;
            float f = (float)Math.Min(1.0, Electrophoresis.TrackingDyeDistanceCm(volts, runMinutes) / 6.0);
            for (int i = 0; i < dyeMarks.Count; i++)
                dyeMarks[i].localPosition = dyeHome[i] + new Vector3(0.09f * f, 0, 0);  // thuốc nhuộm chạy về cực dương (+x)
            if (runMinutes >= timer)
            {
                runMinutes = timer;
                running = false;
                hasCompletedRun = true;
                if (psu != null) psu.isRunningOutput = false;
                BioFeedback.PlaySuccess();
                MolUi.Say($"Điện di xong ({volts:0} V, {timer:0} phút). Tắt nguồn, chuyển gel sang buồng chụp gel.");
                MolUi.MachineStatus("be_dien_di", "san_sang", $"Điện di xong {volts:0} V {timer:0} phút", 1f);
            }
        }

        /// <summary>Quãng đường (cm) của một đoạn ADN trong lần chạy này (âm nghĩa là đã chạy ra khỏi gel).</summary>
        public double DistanceCm(int bp) => Electrophoresis.MigrationDistanceCm(bp, volts, runMinutes, agarosePercent);
    }

    [DisallowMultipleComponent]
    public class PowerSupplyController : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public float voltageV = 100f;
        public int timerMinutes = 30;
        public bool isRunningOutput;
        public GelTankController connectedTank;

        public string UseHint => $"Bộ nguồn điện di {voltageV:0} V · hẹn {timerMinutes} phút · {(isRunningOutput ? "ĐANG CẤP ĐIỆN (bấm để tắt)" : "Bấm: bật nguồn")} · Lăn chuột: chỉnh điện áp";

        void Start()
        {
            if (connectedTank == null) connectedTank = GetComponentInParent<GelTankController>();
        }

        public void Use(GameObject user)
        {
            if (connectedTank == null) { MolUi.Say("Bộ nguồn chưa nối với bể điện di", true); return; }
            if (isRunningOutput) { isRunningOutput = false; connectedTank.StopRun(); BioFeedback.PlayButton(); return; }
            if (!connectedTank.StartRun(voltageV)) return;
            isRunningOutput = true;
            BioFeedback.PlayButton();
            MolUi.Say($"Đậy nắp, nối dây đỏ (+) đỏ, đen (−) đen, bật {voltageV:0} V: bọt khí nhỏ nổi ở điện cực là đúng. ADN chạy từ cực âm về cực dương.");
        }

        public void OnScroll(float delta)
        {
            if (isRunningOutput || Mathf.Abs(delta) < 0.001f) return;
            voltageV = Mathf.Clamp(voltageV + Mathf.Sign(delta) * 10f, 50f, 150f);
            BioFeedback.PlayClick();
            MolUi.Say($"Điện áp đặt: {voltageV:0} V" + (voltageV > 120 ? " (cao: gel nóng, băng nhòe)" : ""));
        }
    }

    // =========================================================================
    // Buồng chụp gel
    // =========================================================================

    [DisallowMultipleComponent]
    public class GelDocController : MonoBehaviour, IInteractable
    {
        public GelTankController sourceTank;
        public bool captured;
        GelDocProgram program;

        public string UseHint => "Buồng chụp gel (ánh sáng xanh 470 nm + kính lọc cam) · Bấm: mở phần mềm chụp và phân tích gel";

        public void Use(GameObject user)
        {
            if (sourceTank == null)
            {
                float best = float.MaxValue;
                foreach (var t in FindObjectsByType<GelTankController>())
                {
                    float d = Vector3.Distance(t.transform.position, transform.position);
                    if (d < best) { best = d; sourceTank = t; }
                }
            }
            program ??= new GelDocProgram { gelDoc = this };
            InstrumentScreen.Open(null, program, null);
        }
    }

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
            S.AddButton("Ánh sáng xanh 470 nm", 2, 0, () => { lightOn = true; S.Done("chon_che_do"); });
            S.AddButton("Chụp & phân tích", 0, 1, Capture, true, 3);
            S.SetPlotAxes("ẢNH GEL (giếng ở trên, ADN chạy xuống)", "Giếng 1 → 8", "Quãng đường từ giếng", "1", "8", "xa", "giếng");
            Refresh();
        }

        void PlaceGel()
        {
            if (!S.Require("dat_gel")) return;
            if (Tank == null || !Tank.gelPlaced) { S.Say("Chưa có gel trong bể điện di.", 2); return; }
            if (Tank.running) { S.Say("Gel đang chạy điện di: tắt nguồn trước.", 2); return; }
            if (!Tank.hasCompletedRun) S.Say("Gel chưa chạy điện di xong: các băng còn nằm sát giếng.", 2);
            S.Done("dat_gel");
        }

        void Capture()
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

    [DisallowMultipleComponent]
    public class DnaExtractionKit : MonoBehaviour, IInteractable
    {
        public Transform outputRack;
        public int step;
        int made;

        static readonly string[] Steps =
        {
            "Nghiền 5 g chuối với 10 mL đệm ly giải (nước rửa bát phá màng tế bào, màng nhân + 1 thìa muối NaCl)",
            "Ủ dịch nghiền ở bể ổn nhiệt 60 °C trong 10 phút (enzyme phân hủy ADN bị bất hoạt)",
            "Lọc qua phễu giấy lọc lấy dịch trong vào ống nghiệm",
            "Rót nhẹ 2 thể tích cồn 90° lạnh theo thành ống: ADN kết tủa thành sợi trắng ở mặt phân cách, quấn bằng đũa thủy tinh",
        };

        public string UseHint => step < Steps.Length ? $"Tách chiết ADN chuối – bước {step + 1}/4: {Steps[step]} · Bấm để làm" : "Đã tách xong ADN chuối · Bấm để làm mẻ mới";

        public void Use(GameObject user)
        {
            if (step >= Steps.Length) step = 0;
            MolUi.Say($"Bước {step + 1}: {Steps[step]}");
            BioFeedback.PlayClick();
            step++;
            if (step < Steps.Length) return;

            double ug = DnaExtraction.YieldUg(5.0, "chuoi", true, true, 60.0, -20.0, 2.0);
            made++;
            var tube = BioFloor2InteractiveTools.MakeTube(outputRack != null ? outputRack : transform, $"Ống ADN chuối {made}",
                new Vector3(0, 0, -0.03f + 0.02f * ((made - 1) % 4)), "ong_eppendorf", $"ADN chuối {made}", new Color(0.95f, 0.85f, 0.2f));
            tube.mix.Add(MolCodes.BananaDna, 100f);
            tube.Changed();
            BioFeedback.PlaySuccess();
            MolUi.Say($"Thu được ~{ug:0} µg ADN thô (sợi trắng). Hòa vào 100 µL nước: ống \"ADN chuối {made}\" ở giá cạnh bộ tách chiết. ADN chuối không có gen GFP: dùng làm khuôn PCR với mồi GFP sẽ không có băng.");
            BioLabSync.Instance?.RecordExperimentResult("tach_chiet_adn", 10, "Phòng SHPT", $"Tách được ~{ug:0} µg ADN thô từ 5 g chuối");
        }
    }

    // =========================================================================
    // Máy quang phổ OD600 / ADN và cuvet
    // =========================================================================

    [DisallowMultipleComponent]
    public class Od600Controller : MonoBehaviour, IInteractable
    {
        public bool blankDone;
        public double hours = 0.0;
        public double measuredOd600;
        public readonly List<Vector2> curve = new List<Vector2>();
        Od600Program program;

        public double CellsPerMl => measuredOd600 * 8.0e8;
        public string UseHint => "Máy quang phổ OD600 / ADN · Bấm: mở màn hình đo";

        public void Use(GameObject user)
        {
            program ??= new Od600Program { controller = this };
            InstrumentScreen.Open(null, program, null);
        }

        /// <summary>Đo dịch E. coli nuôi lắc ở 37 °C sau h giờ (đường cong sinh trưởng ColonyGrowth).</summary>
        public double MeasureAt(double h)
        {
            hours = h;
            double cells = ColonyGrowth.PopulationAtTime(h, 1.0e6, 1.0e9, 1.0);
            measuredOd600 = ColonyGrowth.OpticalDensity600(cells) + (blankDone ? 0 : 0.38);
            curve.Add(new Vector2((float)h, (float)measuredOd600));
            if (!blankDone)
                BioFeedback.RecordMistake("may_quang_pho_od600", "quen_do_mau_trang", "Đo OD600 mà chưa đo mẫu trắng (môi trường LB sạch)",
                    "Môi trường LB màu vàng tự hấp thụ ánh sáng 600 nm: mọi số đo bị cộng thêm khoảng 0,38.");
            return measuredOd600;
        }
    }

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

    [DisallowMultipleComponent]
    public class Cuvette : MonoBehaviour
    {
        void Start() => MolUi.Info(this, "Cuvet 1 cm: cầm ở hai mặt nhám, hai mặt trong cho tia sáng đi qua. Lau sạch vân tay bằng giấy lau kính trước khi đặt vào máy.");
    }
}
