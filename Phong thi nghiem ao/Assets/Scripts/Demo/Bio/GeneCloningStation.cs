// -----------------------------------------------------------------------------
// GeneCloningStation.cs — Trạm ghép gen (ADN tái tổ hợp) ở Phòng sinh học phân tử.
// Quy trình Sinh học 12 "Công nghệ gen": cắt plasmid pUC19 và gen GFP bằng enzyme cắt giới hạn → nối bằng ligaza
// → biến nạp vào E. coli khả biến (sốc nhiệt 42 °C) → cấy đĩa LB + ampicillin + X-gal → khuẩn lạc trắng mang gen ghép
// → tách plasmid, cắt kiểm tra và điện di (ống kết quả xuất ra giá, nạp vào bể điện di như mẫu PCR).
// Toàn bộ kết quả do DnaCloning (C# thuần) tính, nên làm sai bước nào thì kết quả sai đúng như lỗi đó gây ra.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    [DisallowMultipleComponent]
    public class GeneCloningStation : MonoBehaviour, IInteractable, IBioTarget
    {
        [Header("Chỗ đặt trên mô hình trạm (trình dựng cảnh gán)")]
        public Transform resultDish;      // đĩa petri kết quả: khuẩn lạc xanh / trắng mọc lên đây
        public Transform outputRack;      // giá ống: ống plasmid đã cắt kiểm tra xuất ra đây
        public Transform sourceSlot;      // chỗ đặt ống sản phẩm PCR mang đến làm nguồn gen

        // lựa chọn của học sinh
        public readonly HashSet<string> vectorEnzymes = new HashSet<string> { "EcoRI", "BamHI" };
        public readonly HashSet<string> insertEnzymes = new HashSet<string> { "EcoRI", "BamHI" };
        public bool ligase = true;
        public float ratio = 3f;
        public float ligTempC = 16f;
        public bool cellsOnIce = true;
        public float heatShockS = 45f;
        public bool ampicillin = true;
        public bool xgal = true;

        // kết quả từng bước
        public MicroTube sourceTube;
        public DnaMolecule vectorFragment, insertFragment;
        public List<DnaMolecule> vectorDigest, insertDigest;
        public bool digested, ligated, transformed, plated, verified;
        public LigationResult ligation;
        public TransformationResult colonies;
        public string lastNote = "";
        int outputCount;
        GeneCloningProgram program;
        readonly List<GameObject> colonyDots = new List<GameObject>();

        public string TargetName => "Trạm ghép gen";
        public string UseHint => "Trạm ghép gen (ADN tái tổ hợp pUC19-GFP) · Bấm: mở màn hình quy trình cắt – nối – biến nạp – chọn dòng";

        public void Use(GameObject user)
        {
            program ??= new GeneCloningProgram { station = this };
            InstrumentScreen.Open(null, program, null);
        }

        // ---- nhận ống sản phẩm PCR làm nguồn gen ----
        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            var tube = tool != null ? tool.GetComponent<MicroTube>() : null;
            if (tube == null) return false;
            prompt = sourceTube != null ? "Trạm đã có ống nguồn gen" : $"Dùng ống {tube.labelText} làm nguồn gen GFP";
            return true;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }

        public void OnToolApplyComplete(BioTool tool)
        {
            var tube = tool != null ? tool.GetComponent<MicroTube>() : null;
            if (tube == null || sourceTube != null) return;
            var hand = HandController.Active;
            if (hand == null || hand.Held == null || hand.Held.gameObject != tube.gameObject) return;
            var slot = sourceSlot != null ? sourceSlot : transform;
            hand.ReleaseHeldInto(slot, slot.position, slot.rotation);
            sourceTube = tube;
            var g = tube.GetComponent<Grabbable>();
            if (g != null) g.HeldChanged += held => { if (held && sourceTube == tube) { sourceTube = null; ResetFrom(0); } };
            ResetFrom(0);
            BioFeedback.PlayClick();
            LabMessages.Say(GeneAmountText(), LabMessages.Tone.Good);
        }

        /// <summary>Lượng gen GFP (bp 738) trong ống nguồn: cần ≥ 20 ng để cắt – nối được.</summary>
        public double GeneNg
        {
            get
            {
                if (sourceTube == null) return 0;
                double ng = 0;
                foreach (var b in sourceTube.mix.dna) if (Mathf.Abs(b.bp - DnaCloning.PcrProductBp) <= 20) ng += b.ng;
                return ng;
            }
        }

        public string GeneAmountText() => sourceTube == null
            ? "Chưa có ống sản phẩm PCR: dùng ống gen GFP mẫu của trạm (hoặc mang ống PCR của em tới đặt vào trạm)."
            : GeneNg >= 20 ? $"Ống {sourceTube.labelText}: có ~{GeneNg:0} ng sản phẩm PCR gen GFP 738 bp – đủ để ghép."
            : $"Ống {sourceTube.labelText}: không có (hoặc quá ít) sản phẩm 738 bp – không ghép được gen. Kiểm tra lại PCR bằng điện di.";

        /// <summary>Xóa kết quả từ bước step trở đi (đổi lựa chọn ở bước trước thì phải làm lại các bước sau).</summary>
        public void ResetFrom(int step)
        {
            if (step <= 0) { digested = false; vectorDigest = insertDigest = null; vectorFragment = insertFragment = null; }
            if (step <= 1) ligated = false;
            if (step <= 2) transformed = false;
            if (step <= 3) { plated = false; ClearColonies(); }
            if (step <= 4) verified = false;
        }

        // ---- 1. cắt ----
        public string Digest()
        {
            var plasmid = DnaCloning.Pu19();
            vectorDigest = DnaCloning.Digest(plasmid, new List<string>(vectorEnzymes));
            vectorFragment = DnaCloning.Largest(vectorDigest);

            bool haveGene = sourceTube == null || GeneNg >= 20;   // không mang ống tới: dùng ống gen mẫu của trạm
            insertDigest = haveGene ? DnaCloning.Digest(DnaCloning.GfpPcrProduct(), new List<string>(insertEnzymes)) : new List<DnaMolecule>();
            insertFragment = haveGene ? DnaCloning.Largest(insertDigest) : null;
            digested = true;
            ResetFrom(1);
            var sb = new StringBuilder();
            sb.Append(vectorEnzymes.Count == 0 ? "Plasmid không cắt (chưa chọn enzyme). " : $"Plasmid cắt {string.Join(" + ", vectorEnzymes)}: {Sizes(vectorDigest)}. ");
            sb.Append(!haveGene ? "Không có gen GFP để cắt. " : insertEnzymes.Count == 0 ? "Gen không cắt (đầu PCR không dính). " : $"Gen cắt {string.Join(" + ", insertEnzymes)}: {Sizes(insertDigest)}.");
            lastNote = sb.ToString();
            return lastNote;
        }

        static string Sizes(List<DnaMolecule> frags)
        {
            var parts = new List<string>();
            foreach (var f in frags) parts.Add(f.Circular ? $"{f.LengthBp} bp vòng (không bị cắt)" : $"{f.LengthBp} bp [{End(f.LeftEnd)}…{End(f.RightEnd)}]");
            return string.Join(", ", parts);
        }

        static string End(string e) => string.IsNullOrEmpty(e) ? "đầu bằng" : e;

        // ---- 2. nối ----
        public string Ligate()
        {
            ligation = DnaCloning.Ligate(vectorFragment, insertFragment, ligase, true, ratio, ligTempC, 60);
            ligated = true;
            ResetFrom(2);
            lastNote = ligation.Note + $" Plasmid vòng: {ligation.RecombinantShare:P0} tái tổ hợp, {ligation.SelfLigatedShare:P0} tự đóng vòng, {ligation.UncutShare:P0} không bị cắt.";
            return lastNote;
        }

        // ---- 3. biến nạp, 4. cấy chọn lọc ----
        public string TransformCells()
        {
            transformed = true;
            ResetFrom(3);
            lastNote = !cellsOnIce ? "Tế bào khả biến để ấm: màng mất khả năng nhận ADN, hiệu suất biến nạp rất thấp."
                : heatShockS <= 0 ? "Không sốc nhiệt: ADN khó đi qua màng tế bào."
                : heatShockS > 120 ? "Sốc nhiệt quá lâu: nhiều tế bào chết."
                : $"Sốc nhiệt 42 °C {heatShockS:0} giây rồi đặt lại vào đá, thêm môi trường LB, lắc 37 °C 1 giờ để tế bào biểu hiện gen kháng ampicillin.";
            return lastNote;
        }

        public string Plate()
        {
            colonies = DnaCloning.Transform(ligation, cellsOnIce, heatShockS, ampicillin, xgal, 1234 + (int)(ratio * 10) + (int)heatShockS);
            plated = true;
            ResetFrom(4);
            ShowColonies();
            lastNote = colonies.Note;
            BioLabSync.Instance?.RecordExperimentResult("ghep_gen_adn_tai_to_hop", 12, "Phòng SHPT",
                colonies.Lawn ? "Vi khuẩn mọc kín đĩa (thiếu ampicillin)" : $"{colonies.WhiteColonies} khuẩn lạc trắng, {colonies.BlueColonies} khuẩn lạc xanh");
            return lastNote;
        }

        // ---- 5. kiểm tra dòng: tách plasmid, cắt EcoRI + BamHI, xuất ống để điện di ----
        public string Verify()
        {
            var sb = new StringBuilder();
            DnaMolecule plasmid;
            if (colonies.Lawn) { lastNote = "Đĩa mọc kín (không có kháng sinh): không chọn được khuẩn lạc riêng."; return lastNote; }
            if (colonies.WhiteColonies > 0 && xgal && ligation.Recombinant != null) { plasmid = ligation.Recombinant; sb.Append("Chọn 1 khuẩn lạc trắng, nuôi lắc qua đêm, tách plasmid. "); }
            else if (colonies.WhiteColonies + colonies.BlueColonies > 0) { plasmid = DnaCloning.Pu19(); sb.Append("Không có khuẩn lạc trắng mang gen: chọn khuẩn lạc xanh (plasmid không có gen). "); }
            else { lastNote = "Không có khuẩn lạc nào để kiểm tra."; return lastNote; }

            var frags = DnaCloning.Digest(plasmid, new List<string> { "EcoRI", "BamHI" });
            var bands = new List<DnaBand>();
            DnaCloning.ToBands(frags, 300, bands);
            verified = true;
            outputCount++;
            var tube = BioFloor2InteractiveTools.MakeTube(outputRack != null ? outputRack : transform, $"Ống plasmid cắt kiểm tra {outputCount}",
                new Vector3(0, 0, -0.03f + 0.025f * ((outputCount - 1) % 4)), "ong_eppendorf", $"K{outputCount}", new Color(0.2f, 0.5f, 0.95f));
            tube.mix.Add("plasmid_cat_kiem_tra", 18f);
            tube.mix.Add(MolCodes.LoadingDye, 4f);
            foreach (var b in bands) tube.mix.AddBand(new BioDnaBandInfo { bp = b.bp, ng = b.ng, smear = false });
            tube.Changed();
            sb.Append($"Cắt bằng EcoRI + BamHI: {Sizes(frags)}. Ống K{outputCount} (đã trộn đệm nạp mẫu) ở giá bên phải trạm: nạp vào gel cạnh thang chuẩn để kiểm tra.");
            lastNote = sb.ToString();
            BioFeedback.PlaySuccess();
            return lastNote;
        }

        // ---- khuẩn lạc trên đĩa kết quả ----
        void ClearColonies()
        {
            foreach (var d in colonyDots) if (d != null) Destroy(d);
            colonyDots.Clear();
        }

        void ShowColonies()
        {
            ClearColonies();
            if (resultDish == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var white = new Material(shader) { color = new Color(0.96f, 0.95f, 0.88f) };
            var blue = new Material(shader) { color = new Color(0.15f, 0.35f, 0.85f) };
            if (colonies.Lawn)
            {
                var lawn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(lawn.GetComponent<Collider>());
                lawn.name = "Vi khuẩn mọc kín đĩa";
                lawn.transform.SetParent(resultDish, false);
                lawn.transform.localPosition = new Vector3(0, 0.0125f, 0);
                lawn.transform.localScale = new Vector3(0.082f, 0.0008f, 0.082f);
                lawn.GetComponent<Renderer>().sharedMaterial = white;
                colonyDots.Add(lawn);
                return;
            }
            // tối đa 150 chấm (đĩa thật có thể vài trăm khuẩn lạc): giữ đúng tỉ lệ trắng / xanh
            int total = colonies.WhiteColonies + colonies.BlueColonies;
            int shown = Mathf.Min(150, total);
            int whiteShown = total > 0 ? Mathf.RoundToInt(shown * (float)colonies.WhiteColonies / total) : 0;
            var rng = new System.Random(total * 31 + colonies.WhiteColonies);
            for (int i = 0; i < shown; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, r = 0.038f * Mathf.Sqrt((float)rng.NextDouble());
                var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(dot.GetComponent<Collider>());
                dot.name = "Khuẩn lạc";
                dot.transform.SetParent(resultDish, false);
                float s = 0.0025f + 0.002f * (float)rng.NextDouble();
                dot.transform.localPosition = new Vector3(Mathf.Cos(a) * r, 0.0125f, Mathf.Sin(a) * r);
                dot.transform.localScale = new Vector3(s, 0.0012f, s);
                dot.GetComponent<Renderer>().sharedMaterial = i < whiteShown ? white : blue;
                colonyDots.Add(dot);
            }
        }
    }

    /// <summary>Màn hình trạm ghép gen (bước theo data/may/tram_ghep_gen.json).</summary>
    public class GeneCloningProgram : InstrumentProgram
    {
        public override string Code => "tram_ghep_gen";
        public GeneCloningStation station;
        readonly Dictionary<string, UnityEngine.UI.Button> enzymeButtons = new Dictionary<string, UnityEngine.UI.Button>();
        UnityEngine.UI.Button bLig, bRatio, bTemp, bIce, bShock, bAmp, bXgal, bSource;
        static readonly float[] Ratios = { 1f, 3f, 5f };
        static readonly float[] Temps = { 16f, 22f, 37f };
        static readonly float[] Shocks = { 0f, 45f, 150f };

        public override void Build()
        {
            enzymeButtons.Clear();
            string[] names = { "EcoRI", "BamHI", "HindIII" };
            for (int i = 0; i < 3; i++)
            {
                string e = names[i];
                enzymeButtons["v" + e] = S.AddButton("", i, 0, () => Toggle(station.vectorEnzymes, e));
                enzymeButtons["i" + e] = S.AddButton("", i, 1, () => Toggle(station.insertEnzymes, e));
            }
            bSource = S.AddButton("", 0, 2, () => S.Say(station.GeneAmountText(), 0), false, 1, 12);
            S.AddButton("Ủ cắt 37 °C – 1 giờ", 1, 2, DoDigest, true, 2);
            bLig = S.AddButton("", 0, 3, () => { station.ligase = !station.ligase; station.ResetFrom(1); });
            bRatio = S.AddButton("", 1, 3, () => { station.ratio = Next(Ratios, station.ratio); station.ResetFrom(1); });
            bTemp = S.AddButton("", 2, 3, () => { station.ligTempC = Next(Temps, station.ligTempC); station.ResetFrom(1); });
            S.AddButton("Nối bằng ADN ligaza", 0, 4, DoLigate, true, 3);
            bIce = S.AddButton("", 0, 5, () => { station.cellsOnIce = !station.cellsOnIce; station.ResetFrom(2); });
            bShock = S.AddButton("", 1, 5, () => { station.heatShockS = Next(Shocks, station.heatShockS); station.ResetFrom(2); });
            S.AddButton("Biến nạp", 2, 5, DoTransform, true);
            bAmp = S.AddButton("", 0, 6, () => { station.ampicillin = !station.ampicillin; station.ResetFrom(3); });
            bXgal = S.AddButton("", 1, 6, () => { station.xgal = !station.xgal; station.ResetFrom(3); });
            S.AddButton("Cấy & ủ 37 °C", 2, 6, DoPlate, true);
            S.AddButton("Chọn khuẩn lạc trắng → tách plasmid → cắt kiểm tra", 0, 7, DoVerify, true, 3, 13);
            S.SetPlotAxes("SƠ ĐỒ ĐIỆN DI DỰ KIẾN (bp)", "Giếng: thang · plasmid · gen · kiểm tra", "Kích thước", "", "", "100", "5000");
            if (station.digested) S.Done("chuan_bi");
            Refresh();
        }

        static float Next(float[] options, float cur)
        {
            for (int i = 0; i < options.Length; i++) if (Mathf.Approximately(options[i], cur)) return options[(i + 1) % options.Length];
            return options[0];
        }

        void Toggle(HashSet<string> set, string e)
        {
            if (!set.Remove(e)) set.Add(e);
            station.ResetFrom(0);
            S.Done("chuan_bi");
        }

        void DoDigest()
        {
            if (!S.Require("cat_gioi_han")) return;
            S.Done("chuan_bi");
            string note = station.Digest();
            S.Done("cat_gioi_han");
            bool sameEnds = station.vectorFragment != null && station.insertFragment != null && !station.vectorFragment.Circular
                            && DnaCloning.EndsCompatible(station.vectorFragment.RightEnd, station.insertFragment.LeftEnd);
            S.Say(note, sameEnds ? 1 : 2);
        }

        void DoLigate()
        {
            if (!S.Require("noi_ligaza")) return;
            if (!station.digested) { S.Say("Chưa cắt: bấm \"Ủ cắt 37 °C\" trước.", 2); return; }
            string note = station.Ligate();
            S.Done("noi_ligaza");
            S.Say(note, station.ligation.RecombinantShare > 0.5 ? 1 : 2);
        }

        void DoTransform()
        {
            if (!S.Require("bien_nap")) return;
            if (!station.ligated) { S.Say("Chưa nối: bấm \"Nối bằng ADN ligaza\" trước.", 2); return; }
            S.Say(station.TransformCells(), station.cellsOnIce && station.heatShockS > 0 && station.heatShockS <= 120 ? 1 : 2);
            S.Done("bien_nap");
        }

        void DoPlate()
        {
            if (!S.Require("cay_chon_loc")) return;
            if (!station.transformed) { S.Say("Chưa biến nạp.", 2); return; }
            string note = station.Plate();
            S.Done("cay_chon_loc");
            S.Say(note + " Xem đĩa kết quả trên trạm.", station.colonies.WhiteColonies > station.colonies.BlueColonies ? 1 : 2);
        }

        void DoVerify()
        {
            if (!S.Require("kiem_tra_dong")) return;
            if (!station.plated) { S.Say("Chưa cấy đĩa.", 2); return; }
            string note = station.Verify();
            if (station.verified) S.Done("kiem_tra_dong");
            S.Say(note, station.verified ? 1 : 2);
        }

        public override void Refresh()
        {
            foreach (var kv in enzymeButtons)
            {
                bool vector = kv.Key[0] == 'v';
                string e = kv.Key.Substring(1);
                bool on = (vector ? station.vectorEnzymes : station.insertEnzymes).Contains(e);
                InstrumentScreen.SetButton(kv.Value, $"{(vector ? "Plasmid" : "Gen")}: {e} {(on ? "✓" : "")}", true, on);
            }
            InstrumentScreen.SetButton(bSource, station.sourceTube != null ? $"Nguồn gen: ống {station.sourceTube.labelText}" : "Nguồn gen: ống mẫu", true);
            InstrumentScreen.SetButton(bLig, station.ligase ? "Ligaza T4: CÓ" : "Ligaza: KHÔNG", true, !station.ligase);
            InstrumentScreen.SetButton(bRatio, $"Gen : plasmid = {station.ratio:0}:1", true);
            InstrumentScreen.SetButton(bTemp, $"Ủ nối {station.ligTempC:0} °C", true, station.ligTempC > 30);
            InstrumentScreen.SetButton(bIce, station.cellsOnIce ? "Tế bào khả biến: trên đá" : "Tế bào: để ấm", true, !station.cellsOnIce);
            InstrumentScreen.SetButton(bShock, station.heatShockS <= 0 ? "Sốc nhiệt: không" : $"Sốc nhiệt 42 °C: {station.heatShockS:0} s", true, station.heatShockS <= 0 || station.heatShockS > 120);
            InstrumentScreen.SetButton(bAmp, station.ampicillin ? "Đĩa có ampicillin" : "Đĩa KHÔNG ampicillin", true, !station.ampicillin);
            InstrumentScreen.SetButton(bXgal, station.xgal ? "Có X-gal + IPTG" : "KHÔNG X-gal", true, !station.xgal);

            var sb = new StringBuilder("<b>TRẠM GHÉP GEN · pUC19 + GEN GFP</b>\n");
            sb.Append("• Thể truyền pUC19 2686 bp: gen kháng ampicillin, vùng cắt (EcoRI 396 · BamHI 417 · HindIII 447) nằm giữa gen lacZα.\n");
            sb.Append($"• Gen GFP: sản phẩm PCR 738 bp, đầu 5′ mang EcoRI, đầu 3′ mang BamHI. {station.GeneAmountText()}\n");
            if (station.digested) sb.Append($"• Cắt: {(station.vectorFragment != null ? station.vectorFragment.ToString() : "-")} | gen: {(station.insertFragment != null ? station.insertFragment.ToString() : "không có")}\n");
            if (station.ligated) sb.Append($"• Nối: {station.ligation.RecombinantShare:P0} plasmid tái tổ hợp{(station.ligation.Recombinant != null ? $" ({station.ligation.Recombinant.LengthBp} bp)" : "")}\n");
            if (station.plated) sb.Append(station.colonies.Lawn ? "• Đĩa: mọc kín đĩa\n" : $"• Đĩa: <b>{station.colonies.WhiteColonies}</b> khuẩn lạc trắng · <b>{station.colonies.BlueColonies}</b> khuẩn lạc xanh\n");
            if (!string.IsNullOrEmpty(station.lastNote)) sb.Append($"• {station.lastNote}");
            S.SetResults(sb.ToString());
        }

        public override void DrawSchematic(Painter p, float time)
        {
            p.Clear(new Color(0.05f, 0.07f, 0.1f));
            // plasmid vòng (trái) và gen (giữa) → plasmid tái tổ hợp (phải)
            DrawRing(p, 160, 180, 95, new Color(0.35f, 0.6f, 0.95f), station.digested && station.vectorFragment != null && !station.vectorFragment.Circular);
            p.FillRect(400, 172, 170, 16, new Color(0.3f, 0.85f, 0.4f));
            if (station.ligated && station.ligation.Recombinant != null)
            {
                DrawRing(p, 790, 180, 110, new Color(0.35f, 0.6f, 0.95f), false);
                p.FillRect(745, 280, 90, 16, new Color(0.3f, 0.85f, 0.4f));
            }
            S.SchemLabel("gc_v", "pUC19 (ampR, lacZα)", 160, 40, 13, Color.white);
            S.SchemLabel("gc_i", "Gen GFP", 485, 205, 13, Color.white);
            S.SchemLabel("gc_r", station.ligated && station.ligation.Recombinant != null ? "pUC19-GFP (ADN tái tổ hợp)" : "Plasmid tái tổ hợp", 790, 40, 13, Color.white);
            S.SchemLabel("gc_a", "cắt → nối", 640, 180, 15, new Color(1f, 0.85f, 0.4f));
        }

        static void DrawRing(Painter p, float cx, float cy, float r, Color c, bool opened)
        {
            for (int i = 0; i < 90; i++)
            {
                if (opened && (i < 3 || i > 86)) continue;  // vết cắt ở đỉnh vòng
                float a0 = Mathf.PI * 2 * i / 90f + Mathf.PI / 2, a1 = Mathf.PI * 2 * (i + 1) / 90f + Mathf.PI / 2;
                p.Line(cx + Mathf.Cos(a0) * r, cy + Mathf.Sin(a0) * r, cx + Mathf.Cos(a1) * r, cy + Mathf.Sin(a1) * r, c, 6);
            }
        }

        public override void DrawPlot(Painter p)
        {
            p.Clear(new Color(0.02f, 0.02f, 0.03f));
            float topY = p.H - 30, usable = p.H - 70;
            float Y(int bp) => topY - (float)((System.Math.Log10(5000) - System.Math.Log10(Mathf.Clamp(bp, 100, 5000))) / (System.Math.Log10(5000) - 2.0)) * usable;
            void Lane(int idx, IEnumerable<int> bps)
            {
                float cx = 80 + idx * 200;
                p.FillRect(cx - 60, topY, 120, 8, new Color(0.22f, 0.22f, 0.25f));
                foreach (int bp in bps) p.FillRect(cx - 55, Y(bp) - 3, 110, 6, new Color(0.7f, 1f, 0.7f));
            }
            Lane(0, MolCodes.LadderBp);
            if (station.vectorDigest != null) Lane(1, Apparent(station.vectorDigest));
            if (station.insertDigest != null) Lane(2, Apparent(station.insertDigest));
            if (station.verified && station.ligation.Recombinant != null)
                Lane(3, Apparent(DnaCloning.Digest(station.ligation.Recombinant, new List<string> { "EcoRI", "BamHI" })));
        }

        static IEnumerable<int> Apparent(List<DnaMolecule> frags)
        {
            foreach (var f in frags) if (f.LengthBp >= 100) yield return DnaCloning.ApparentBp(f);
        }
    }
}
