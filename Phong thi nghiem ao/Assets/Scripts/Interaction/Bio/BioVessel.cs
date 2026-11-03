using System.Collections.Generic;
using Khkt.Demo;
using Khkt.Demo.Bio;
using Khkt.Simulation.Bio;
using UnityEngine;

namespace Khkt.Interaction.Bio
{
    /// <summary>
    /// Bình chứa thật của lab sinh: cốc, ống nghiệm, bình tam giác, chai sinh phẩm, khay mẫu. Trong bình có gì do người học tự đưa vào,
    /// kết quả do BioRules tính theo thành phần và nhiệt độ, không có thứ tự bước nào quy định sẵn.
    /// Cầm một dụng cụ rồi trỏ vào bình là thấy làm được gì: rót, hút, nhỏ, gắp, thái, đo.
    /// </summary>
    [DisallowMultipleComponent]
    public class BioVessel : MonoBehaviour, IBioTarget
    {
        public string label = "Bình";
        public float capacityMl = 50f;
        /// <summary>Chai, khay sinh phẩm gốc: lấy ra được, không đổ ngược vào (quy tắc giữ sạch sinh phẩm).</summary>
        public bool isStock;
        public string stockId = "";
        public double stockAmount = 100;
        public string unit = "mL";
        public float storedTempC = 25f;
        public float pourRateMlPerS = 5f;
        public Transform fillMesh;
        public float fillBottomY, fillMaxHeight = 0.05f;
        public Renderer fillRenderer;
        public Transform foamMesh;

        public readonly BioMix mix = new BioMix();
        public float TemperatureC { get; set; } = 25f;
        public bool scored;
        public int sampleNumber;

        readonly List<string> events = new List<string>();
        EquipmentInfo info;
        MaterialPropertyBlock mpb;
        float lastTick;
        double lastLook = -1;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static int nextSample = 1;

        public BioMatter StockMatter => BioMatterDb.Get(stockId);
        public bool StockIsTissue => StockMatter != null && StockMatter.phase == BioPhase.Tissue;

        bool HasTissue
        {
            get
            {
                foreach (var kv in mix.amounts)
                    if (BioMatterDb.Get(kv.Key)?.phase == BioPhase.Tissue) return true;
                return false;
            }
        }

        void Awake()
        {
            info = GetComponent<EquipmentInfo>();
            TemperatureC = storedTempC;
            if (isStock && !string.IsNullOrEmpty(stockId)) mix.Add(stockId, stockAmount);
        }

        void OnEnable()
        {
            lastTick = Time.time;
            BioVesselSystem.Register(this);
            RefreshVisual(true);
        }

        void OnDisable() => BioVesselSystem.Unregister(this);

        // ------------------------------------------------------------------
        // Chạy luật (BioVesselSystem gọi 5 lần/giây, rải đều)
        // ------------------------------------------------------------------

        public void Tick()
        {
            float now = Time.time, dt = now - lastTick;
            lastTick = now;
            if (dt <= 0) return;
            var zone = BioThermalZone.At(transform.position);
            float env = zone != null ? zone.temperature() : isStock ? storedTempC : 25f;
            float rate = zone != null ? zone.rate : 0.015f;
            TemperatureC = Mathf.MoveTowards(TemperatureC, env, Mathf.Abs(env - TemperatureC) * Mathf.Clamp01(rate * dt) + 0.002f);
            if (isStock || mix.IsEmpty) { RefreshVisual(false); return; }
            events.Clear();
            BioRules.Step(mix, TemperatureC, dt, zone != null ? zone.timeScale : 60, events);
            foreach (var e in events)
            {
                BioFeedback.ShowToast(e, false, 5f, label);
                BioFeedback.LogAction("binh_sinh", "hien_tuong", $"{label}: {e}");
            }
            RefreshVisual(false);
        }

        public void RefreshVisual(bool force)
        {
            UpdateInfo();
            double total = mix.Total;
            var look = BioRules.Look(mix);
            double key = total * 1000 + look.r * 7 + look.g * 11 + look.b * 13 + look.a * 17 + mix.foam * 19;
            if (!force && System.Math.Abs(key - lastLook) < 0.01) return;
            lastLook = key;
            if (fillMesh != null)
            {
                float k = Mathf.Clamp01((float)total / Mathf.Max(0.01f, capacityMl));
                bool show = total > 0.0005f;
                if (fillMesh.gameObject.activeSelf != show) fillMesh.gameObject.SetActive(show);
                if (show)
                {
                    float h = Mathf.Max(0.0015f, fillMaxHeight * k);
                    var s = fillMesh.localScale;
                    fillMesh.localScale = new Vector3(s.x, h / 2f, s.z);
                    var p = fillMesh.localPosition;
                    fillMesh.localPosition = new Vector3(p.x, fillBottomY + h / 2f, p.z);
                    if (foamMesh != null)
                    {
                        bool foam = mix.foam > 0.08;
                        if (foamMesh.gameObject.activeSelf != foam) foamMesh.gameObject.SetActive(foam);
                        if (foam)
                        {
                            float fh = Mathf.Max(0.002f, fillMaxHeight * 0.5f * (float)mix.foam);
                            var fs = foamMesh.localScale;
                            foamMesh.localScale = new Vector3(fs.x, fh / 2f, fs.z);
                            foamMesh.localPosition = new Vector3(0, fillBottomY + h + fh / 2f, 0);
                        }
                    }
                }
                else if (foamMesh != null && foamMesh.gameObject.activeSelf) foamMesh.gameObject.SetActive(false);
            }
            if (fillRenderer != null)
            {
                mpb ??= new MaterialPropertyBlock();
                fillRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, new Color(look.r, look.g, look.b, Mathf.Max(0.3f, look.a)));
                fillRenderer.SetPropertyBlock(mpb);
            }
        }

        void UpdateInfo()
        {
            if (info == null) return;
            info.title = label + (sampleNumber > 0 ? $" · Mẫu {sampleNumber}" : "");
            string ph = BioRules.Phenomena(mix);
            info.description = isStock
                ? $"Sinh phẩm gốc, còn {mix.Of(stockId):0.#} {unit}. Lấy bằng ống nhỏ giọt, kẹp hoặc rót sang bình khác."
                : $"{mix.Describe()} · {TemperatureC:0} °C{(ph.Length > 0 ? " · " + ph : "")}";
        }

        // ------------------------------------------------------------------
        // Nhận tác động từ dụng cụ đang cầm
        // ------------------------------------------------------------------

        public string TargetName => label;

        static bool IsCutter(BioTool t) => t.GetComponent<RazorBlade>() != null || t.GetComponent<Scissors>() != null || t.GetComponent<MortarPestle>() != null;
        static bool IsPhProbe(BioTool t) => t.GetComponent<TestPaper>() != null || t.toolId == "may_do_ec_ph" || t.toolId == "giay_chi_thi_ph";

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            if (tool == null) return false;
            var dropper = tool.GetComponent<BioDropper>();
            if (dropper != null)
            {
                if (!dropper.IsEmpty) prompt = isStock ? "Không nhỏ ngược vào chai gốc" : $"Nhỏ 1 giọt vào {label}";
                else prompt = mix.LiquidMl < 0.01 ? "Trong bình không có chất lỏng để hút" : $"Hút dung dịch từ {label}";
                return true;
            }
            var other = tool.GetComponent<BioVessel>();
            if (other != null && other != this)
            {
                if (isStock && other.isStock) prompt = "Hai chai gốc: lấy một cốc sạch để chiết ra";
                else if (isStock) prompt = $"Lấy {BioMatterDb.Name(stockId)} sang {other.label} (giữ chuột)";
                else if (other.mix.IsEmpty && !mix.IsEmpty) prompt = $"Rót từ {label} sang {other.label} (giữ chuột)";
                else prompt = $"Rót {other.label} vào {label} (giữ chuột)";
                return true;
            }
            if (tool.GetComponent<Forceps>() != null)
            {
                if (string.IsNullOrEmpty(tool.reagentOrSampleId))
                {
                    if (StockIsTissue) prompt = BioSpecimenMap.NeedsCut(stockId) && !scored ? $"Phải rạch {label} bằng dao lam trước khi bóc biểu bì" : $"Gắp mẫu từ {label} (giữ chuột)";
                    else prompt = HasTissue ? "Gắp mô ra (giữ chuột)" : "Trong bình không có mẫu để gắp";
                }
                else prompt = isStock ? "Không trả mẫu đã gắp về khay gốc" : $"Thả {tool.reagentDisplayName} vào {label}";
                return true;
            }
            if (IsCutter(tool))
            {
                bool grind = tool.GetComponent<MortarPestle>() != null;
                if (isStock && StockIsTissue) prompt = BioSpecimenMap.NeedsCut(stockId) ? $"Rạch ô vuông trên {label} (giữ chuột)" : "Gắp mẫu ra bình rồi mới thái, nghiền";
                else prompt = HasTissue ? (grind ? $"Nghiền mô trong {label} (giữ chuột)" : $"Thái nhỏ mô trong {label} (giữ chuột)") : "Trong bình không có mô để thái, nghiền";
                return true;
            }
            if (tool.GetComponent<GlassRod>() != null) { prompt = isStock ? "Không khuấy trong chai gốc" : $"Khuấy {label} (giữ chuột)"; return true; }
            if (tool.GetComponent<Thermometer>() != null) { prompt = $"Đo nhiệt độ {label}"; return true; }
            if (tool.GetComponent<GlowingSplint>() != null) { prompt = $"Đưa tàn đóm vào miệng {label}"; return true; }
            if (tool.GetComponent<LabelPen>() != null) { prompt = $"Ghi số mẫu lên {label}"; return true; }
            if (IsPhProbe(tool)) { prompt = $"Đo pH {label}"; return true; }
            var pip = tool.GetComponent<Micropipette>();
            if (pip != null)
            {
                if (!pip.tipAttached) prompt = "Micropipet chưa gắn đầu côn";
                else prompt = pip.genericLoad.IsEmpty ? $"Hút {pip.VolumeText} từ {label}" : isStock ? "Không bơm ngược vào chai gốc" : $"Bơm vào {label}";
                return true;
            }
            var loop = tool.GetComponent<InoculatingLoop>();
            if (loop != null)
            {
                if (loop.wireTempC > 45f) prompt = "Que cấy còn nóng: để nguội kẻo chết vi khuẩn";
                else if (string.IsNullOrEmpty(loop.carriedOrganismId)) prompt = BioRules.HasBacteria(mix) ? $"Lấy một vòng vi khuẩn từ {label}" : $"Trong {label} không có vi khuẩn";
                else prompt = $"Cấy vi khuẩn vào {label}";
                return true;
            }
            return false;
        }

        public float GetRequiredHoldSeconds(BioTool tool)
        {
            if (tool == null) return 0;
            if (tool.GetComponent<BioVessel>() != null || IsCutter(tool)) return 1.5f;
            if (tool.GetComponent<GlassRod>() != null) return 1f;
            if (tool.GetComponent<Forceps>() != null && string.IsNullOrEmpty(tool.reagentOrSampleId)) return 0.8f;
            return 0;
        }

        public void OnToolApplyStep(BioTool tool, float dt, float progress01)
        {
            var other = tool != null ? tool.GetComponent<BioVessel>() : null;
            if (other == null || other == this || (isStock && other.isStock)) return;
            float amount = Mathf.Max(pourRateMlPerS, other.pourRateMlPerS) * dt;
            if (isStock || (other.mix.IsEmpty && !mix.IsEmpty)) Pour(this, other, amount);
            else Pour(other, this, amount);
        }

        public void OnToolApplyComplete(BioTool tool)
        {
            if (tool == null) return;
            var dropper = tool.GetComponent<BioDropper>();
            if (dropper != null) { ApplyDropper(dropper); return; }
            var forceps = tool.GetComponent<Forceps>();
            if (forceps != null) { ApplyForceps(tool, forceps); return; }
            if (IsCutter(tool)) { ApplyCut(tool); return; }
            if (tool.GetComponent<GlassRod>() != null)
            {
                if (!isStock) { BioFeedback.PlayClick(); BioFeedback.ShowToast($"Đã khuấy đều {label}"); }
                return;
            }
            var th = tool.GetComponent<Thermometer>();
            if (th != null) { th.MeasureTemperature(TemperatureC); BioFeedback.ShowToast($"{label}: {TemperatureC:0.#} °C"); return; }
            var splint = tool.GetComponent<GlowingSplint>();
            if (splint != null)
            {
                bool flame = mix.o2Recent > 0.5;
                splint.burstIntoFlame = flame;
                BioFeedback.ShowToast(flame ? "Tàn đóm bùng cháy: khí sinh ra là O₂" : "Tàn đóm không bùng cháy: chưa có đủ O₂", !flame);
                return;
            }
            if (tool.GetComponent<LabelPen>() != null)
            {
                if (sampleNumber == 0) sampleNumber = nextSample++;
                BioFeedback.PlayClick();
                UpdateInfo();
                BioFeedback.ShowToast($"Đã ghi nhãn: Mẫu {sampleNumber}");
                return;
            }
            if (IsPhProbe(tool))
            {
                BioFeedback.ShowToast(mix.LiquidMl < 0.01 ? "Không có chất lỏng để đo" : $"{label}: pH ≈ {BioRules.Ph(mix):0.0}");
                return;
            }
            var pip = tool.GetComponent<Micropipette>();
            if (pip != null) { ApplyPipette(pip); return; }
            var loop = tool.GetComponent<InoculatingLoop>();
            if (loop != null) ApplyLoop(loop);
        }

        void ApplyDropper(BioDropper d)
        {
            if (d.IsEmpty)
            {
                var got = mix.TakeLiquid(BioDropper.CapacityMl);
                if (got.IsEmpty) return;
                d.Load(got);
                BioFeedback.PlayClick();
            }
            else if (isStock) BioFeedback.ShowToast("Không nhỏ ngược vào chai gốc", true);
            else if (mix.Total + BioDropper.DropMl > capacityMl) BioFeedback.ShowToast("Bình đã đầy", true);
            else { mix.Merge(d.TakeDrop()); BioFeedback.PlayBubblePop(); }
            CheckStock();
            RefreshVisual(true);
        }

        void ApplyForceps(BioTool tool, Forceps f)
        {
            if (string.IsNullOrEmpty(tool.reagentOrSampleId))
            {
                string src = null;
                if (StockIsTissue)
                {
                    if (BioSpecimenMap.NeedsCut(stockId) && !scored)
                    {
                        BioFeedback.RecordMistake("kep_panh", "boc_bieu_bi_chua_rach", "Bóc biểu bì khi chưa rạch ô vuông bằng dao lam",
                            "Rạch một ô vuông nhỏ ở mặt trong vảy hành rồi mới bóc, lớp biểu bì mới mỏng một lớp tế bào.");
                        return;
                    }
                    src = stockId;
                    mix.amounts[stockId] = System.Math.Max(0, mix.Of(stockId) - 1);
                    CheckStock();
                }
                else
                {
                    foreach (var kv in mix.amounts)
                        if (BioMatterDb.Get(kv.Key)?.phase == BioPhase.Tissue) { src = kv.Key; break; }
                    if (src == null) { BioFeedback.ShowToast("Trong bình không có mẫu để gắp", true); return; }
                    double left = mix.Of(src) - 1;
                    if (left <= 0) mix.amounts.Remove(src); else mix.amounts[src] = left;
                }
                tool.reagentOrSampleId = BioSpecimenMap.SpecimenId(src);
                tool.reagentDisplayName = BioMatterDb.Name(src);
                f.heldSpecimenId = tool.reagentOrSampleId;
                f.heldSpecimenName = tool.reagentDisplayName;
                f.carriedMatter = src;
                BioFeedback.PlayClick();
                BioFeedback.ShowToast($"Kẹp đang giữ: {tool.reagentDisplayName}");
            }
            else if (!isStock && !string.IsNullOrEmpty(f.carriedMatter))
            {
                mix.Add(f.carriedMatter, 1);
                tool.reagentOrSampleId = tool.reagentDisplayName = f.heldSpecimenId = f.heldSpecimenName = f.carriedMatter = "";
                BioFeedback.PlayClick();
            }
            RefreshVisual(true);
        }

        void ApplyCut(BioTool tool)
        {
            if (isStock && StockIsTissue && BioSpecimenMap.NeedsCut(stockId))
            {
                scored = true;
                var blade = tool.GetComponent<RazorBlade>();
                if (blade != null) blade.scoredOnionGrid = true;
                BioFeedback.PlayClick();
                BioFeedback.ShowToast($"Đã rạch ô vuông trên {label}: dùng kẹp bóc lớp biểu bì");
            }
            else if (!isStock && HasTissue)
            {
                mix.ground = true;
                BioFeedback.PlayClick();
                BioFeedback.ShowToast(tool.GetComponent<MortarPestle>() != null ? $"Đã nghiền mô trong {label}" : $"Đã thái nhỏ mô trong {label}");
            }
        }

        void ApplyPipette(Micropipette p)
        {
            if (!p.tipAttached) { BioFeedback.ShowToast("Gắn đầu côn trước khi hút", true); return; }
            if (p.genericLoad.IsEmpty)
            {
                var got = mix.TakeLiquid(p.volumeSetUl / 1000.0);
                if (got.IsEmpty) { BioFeedback.ShowToast("Không có chất lỏng để hút", true); return; }
                p.genericLoad.Merge(got);
            }
            else if (isStock) { BioFeedback.ShowToast("Không bơm ngược vào chai gốc", true); return; }
            else { mix.Merge(p.genericLoad); p.genericLoad.Clear(); }
            BioFeedback.PlayClick();
            CheckStock();
            RefreshVisual(true);
        }

        void ApplyLoop(InoculatingLoop loop)
        {
            if (loop.wireTempC > 45f)
            {
                BioFeedback.RecordMistake("que_cay_vong", "que_nong_cham_mau", "Chạm que cấy còn nóng vào mẫu", "Để que nguội vài giây trong vùng vô trùng, que nóng làm chết vi khuẩn.");
                return;
            }
            if (string.IsNullOrEmpty(loop.carriedOrganismId))
            {
                string org = mix.Has(BioMatterDb.EColi) ? BioMatterDb.EColi : mix.Has(BioMatterDb.BSubtilis) ? BioMatterDb.BSubtilis : mix.Has(BioMatterDb.Dh5a) ? BioMatterDb.Dh5a : null;
                if (org == null) { BioFeedback.ShowToast("Trong bình không có vi khuẩn để lấy", true); return; }
                if (mix.bacteriaDead) { BioFeedback.ShowToast("Vi khuẩn trong bình đã chết", true); return; }
                loop.carriedOrganismId = org;
                loop.isSterile = false;
                BioFeedback.PlayClick();
                return;
            }
            if (isStock) return;
            if (!BioTransfer.NearSpiritFlame && mix.Has(BioMatterDb.Broth, 0.1)) mix.contaminated = true;
            mix.Add(loop.carriedOrganismId, 0.001);
            loop.carriedOrganismId = "";
            BioFeedback.PlayClick();
            BioFeedback.ShowToast($"Đã cấy vào {label}. Đặt vào tủ ấm 37 °C rồi theo dõi độ đục.");
        }

        static void Pour(BioVessel from, BioVessel to, float amount)
        {
            if (from.mix.IsEmpty) return;
            float room = to.capacityMl - (float)to.mix.Total;
            if (room <= 0.001f) { BioFeedback.ShowToast($"{to.label} đã đầy", true, 1.5f); return; }
            amount = Mathf.Min(amount, room);
            var part = from.mix.Take(amount);
            to.TemperatureC = (to.TemperatureC * (float)to.mix.Total + from.TemperatureC * amount) / Mathf.Max(0.001f, (float)to.mix.Total + amount);
            to.mix.Merge(part);
            from.CheckStock();
            from.RefreshVisual(true);
            to.RefreshVisual(true);
        }

        void CheckStock()
        {
            if (isStock && mix.Of(stockId) <= 0.001) BioFeedback.ShowToast($"Đã hết {label}", true);
        }

        /// <summary>Đổ bỏ (bồn rửa).</summary>
        public void Discard()
        {
            mix.Clear();
            RefreshVisual(true);
        }
    }

    /// <summary>Đổi mã sinh phẩm sang mã tiêu bản, chất nhuộm và nồng độ muối để lam kính, kính hiển vi đang có dùng được.</summary>
    public static class BioSpecimenMap
    {
        public static bool NeedsCut(string stock) => stock == BioMatterDb.Onion || stock == BioMatterDb.Tradescantia;

        public static string SpecimenId(string stock)
        {
            if (stock == BioMatterDb.Onion) return "bieu_bi_vay_hanh";
            if (stock == BioMatterDb.Tradescantia) return "khi_khong_mo";
            return stock;
        }

        public static string SlideReagent(string stock)
        {
            if (stock == BioMatterDb.Methylene) return "xanh_methylen";
            if (stock == BioMatterDb.Lugol) return "lugol";
            if (stock == BioMatterDb.Water) return "nuoc_cat";
            return stock;
        }

        public static float Molarity(BioMix m)
        {
            if (m.Has(BioMatterDb.Nacl10)) return 1.71f;
            if (m.Has(BioMatterDb.Nacl09)) return 0.154f;
            return 0f;
        }
    }
}
