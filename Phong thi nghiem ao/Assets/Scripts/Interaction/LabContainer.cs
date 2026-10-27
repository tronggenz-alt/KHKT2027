using System;
using System.Collections.Generic;
using Khkt.Simulation;
using Khkt.Simulation.Chem;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Một vật chứa: bình nón, cốc, ống nghiệm, chai, buret, pipet. Mọi thao tác rót, hút, xả, nhỏ giọt
    /// đều là Take() ở vật này và Add() ở vật kia; mỗi lần trộn, bộ máy hóa học (ChemEngine) tính lại
    /// cân bằng: pH, kết tủa, khí, đổi màu, nhiệt. Hiện tượng phát ra qua sự kiện ChemEventRaised để
    /// giao diện chú thích (và AI giải thích thêm nếu có).
    ///
    /// slowMixing (bình nón): phần mới cho vào nằm ở "vùng chưa trộn" và chỉ hòa vào cả bình khi lắc
    /// (SwirlDetector) hoặc tự khuếch tán chậm. Vì vậy gần điểm cuối thấy vệt hồng chỗ giọt rơi rồi tan khi lắc.
    /// </summary>
    public class LabContainer : MonoBehaviour
    {
        public string label = "Bình";
        public float capacityMl = 250f;
        public Solution contents;
        [Tooltip("Cốc/chai chứa hóa chất gốc: pipet nhúng vào đây là hút")]
        public bool isStock;
        [Tooltip("Miệng bình: điểm rót vào, điểm đưa đầu pipet tới")]
        public Transform mouth;
        [Tooltip("Có tính phản ứng (tắt cho chai trong kho để đỡ tốn công tính)")]
        public bool reactive = true;

        [Header("Hiển thị")]
        public LiquidFill fill;
        public Renderer liquidRenderer;
        [Tooltip("Lớp cặn / chất rắn dưới đáy (trụ dẹt, gốc ở đáy)")]
        public Transform deposit;
        [Tooltip("Thủy tinh (tráng bạc thì đổi màu)")]
        public Renderer glassRenderer;
        public Color clearColor = new Color(0.85f, 0.92f, 1f, 0.3f);
        public Color pinkColor = new Color(0.95f, 0.3f, 0.65f, 0.7f);
        [Tooltip("Bán kính trong của đáy (để tính độ dày lớp cặn)")]
        public float bottomRadiusM = 0.03f;

        [Header("Trộn (bình nón)")]
        public bool slowMixing;
        public SwirlDetector swirl;
        public float diffusionRate = 0.15f;
        public float swirlMixRate = 3f;

        public Solution Unmixed { get; private set; }
        public Solution Total => contents + Unmixed;
        public float VolumeMl => contents.volumeMl + Unmixed.volumeMl;
        public float FreeMl => Mathf.Max(0, capacityMl - VolumeMl);
        public double BulkPh => contents.Ph;
        public float PinkVisible { get; private set; }
        /// <summary>Thời gian cả bình hồng liên tục (s). Điểm cuối: hồng nhạt bền khoảng 30 s.</summary>
        public float PinkPersistS { get; private set; }
        public float TemperatureC { get; private set; } = 25f;
        public float SwirlNow => Mathf.Max(swirl != null ? swirl.Intensity : 0f, ExternalStir);
        /// <summary>Khuấy từ máy khuấy (0..1), do MagneticStirrer đặt mỗi khung hình (tự về 0 khi thôi đặt).</summary>
        public float ExternalStir
        {
            get => Time.time - stirStamp < 0.2f ? stirValue : 0f;
            set { stirValue = value; stirStamp = Time.time; }
        }
        float stirValue, stirStamp = -10f;
        /// <summary>Công suất cấp nhiệt (W) từ bếp, đèn cồn; nguồn nhiệt cộng dồn mỗi khung hình.</summary>
        public float HeatInputW { get; set; }
        /// <summary>Đang nằm trong tủ hút (khí độc được hút đi).</summary>
        public bool InFumeHood { get; set; }
        public ChemLook Look { get; private set; }
        /// <summary>Tốc độ thoát khí gần đây (mol/s) và khí chính, để vẽ bọt và khói.</summary>
        public float GasRate { get; private set; }
        public string GasId { get; private set; }
        public float Settled { get; private set; }

        // ---- Mạng dòng chảy (giai đoạn C3): sôi, đun khô, khí sinh ra ----
        /// <summary>Nhiệt độ sôi hiện tại (°C): mạng dòng chảy đặt theo thành phần (rượu loãng ~89 °C). Mặc định nước 100 °C.</summary>
        public float BoilCapC { get; set; } = 100f;
        /// <summary>Nhiệt đã cấp khi dung dịch đang sôi (J), chờ mạng dòng chảy dùng để hóa hơi.</summary>
        public float ExcessHeatJ { get; set; }
        /// <summary>Nhiệt cấp vào khi bình gần như khô (J): chất rắn nóng lên, nhiệt phân.</summary>
        public float DryHeatJ { get; set; }
        /// <summary>Lần cuối được nguồn nhiệt cấp nhiệt (Time.time).</summary>
        public float LastHeatedTime { get; private set; } = -100f;
        readonly Dictionary<string, double> pendingGas = new Dictionary<string, double>();

        /// <summary>Lấy lượng khí sinh ra từ lần lấy trước (mol theo từng khí) để mạng dòng chảy dẫn đi. false: không có.</summary>
        public bool TakeGas(Dictionary<string, double> into)
        {
            if (pendingGas.Count == 0) return false;
            foreach (var kv in pendingGas) into[kv.Key] = (into.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value;
            pendingGas.Clear();
            return true;
        }

        /// <summary>Lấy ra đúng phần part (một lớp chất lỏng, nước lọc, phần hóa hơi), rồi cân bằng lại phần còn lại.</summary>
        public void TakeExact(Solution part)
        {
            if (part.IsEmpty) return;
            if (Unmixed.volumeMl > 0)
            {
                contents = Merge(contents, Unmixed);
                Unmixed = default;
            }
            contents -= part;
            if (reactive && contents.volumeMl > 0.002f && !contents.Mix.IsEmpty)
            {
                var b = contents.Mix.ToBuilder();
                events.Clear();
                ChemEngine.Equilibrate(b, contents.volumeMl / 1000.0, Env, events);
                contents.mix = b.ToMix();
                Raise(events, 0);
            }
            visualDirty = true;
        }

        /// <summary>Phát một hiện tượng (từ mạng dòng chảy) qua sự kiện chung, không lặp lại trong minGap giây.</summary>
        public void Announce(ChemEvent e, float minGap = 6f)
        {
            if (Notify(e.Key ?? e.Title, minGap)) ChemEventRaised?.Invoke(this, e);
        }

        /// <summary>Có dung dịch cho vào (lượng cho vào).</summary>
        public event Action<Solution> Added;
        /// <summary>Hiện tượng hóa học ở bất kỳ vật chứa nào (để chú thích, robot trợ lý xử lý sự cố).</summary>
        public static event Action<LabContainer, ChemEvent> ChemEventRaised;
        /// <summary>Tràn ra ngoài (ml).</summary>
        public static event Action<LabContainer, Solution> Spilled;

        static MaterialPropertyBlock mpb;  // tạo khi cần: Unity không cho tạo trong khởi tạo tĩnh
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Dictionary<(Material, int), Material> tints = new Dictionary<(Material, int), Material>();
        Material liquidBase;

        /// <summary>
        /// Vật liệu dùng chung cho một màu (làm tròn 5 bit mỗi kênh) dựa trên vật liệu gốc. Dùng thay MaterialPropertyBlock
        /// cho vật có màu cố định: vật dùng MaterialPropertyBlock không được SRP Batcher gộp, mỗi vật thành một lần vẽ riêng.
        /// </summary>
        public static Material SharedTint(Material baseMat, Color c)
        {
            Color32 q = c;
            int key = (q.r >> 3) << 15 | (q.g >> 3) << 10 | (q.b >> 3) << 5 | (q.a >> 3);
            if (tints.TryGetValue((baseMat, key), out var m) && m != null) return m;
            m = new Material(baseMat) { name = baseMat.name + " " + key, hideFlags = HideFlags.DontSave };
            m.SetColor(BaseColorId, c);
            tints[(baseMat, key)] = m;
            return m;
        }
        readonly List<ChemEvent> events = new List<ChemEvent>();
        readonly Dictionary<string, float> lastEvent = new Dictionary<string, float>();
        float stepTimer, gasDecay, lastPh = -100;
        bool visualDirty = true;
        Color glassBase;
        bool glassBaseKnown;

        /// <summary>Cho chất vào. Trả về thể tích bị tràn ra ngoài (ml).</summary>
        public float Add(Solution s)
        {
            if (s.IsEmpty) return 0;
            float spill = 0;
            if (s.volumeMl > FreeMl)
            {
                spill = s.volumeMl - FreeMl;
                var spilled = s.Portion(spill);
                s = s.Portion(FreeMl);
                Spilled?.Invoke(this, spilled);
            }
            if (slowMixing && contents.volumeMl > 1f) Unmixed += s;
            else contents = Merge(contents, s);
            Settled = Mathf.Max(0, Settled - 0.5f);
            visualDirty = true;
            Added?.Invoke(s);
            return spill;
        }

        /// <summary>Lấy ra ml (lấy đều từ cả bình, kể cả kết tủa đang lơ lửng).</summary>
        public Solution Take(float ml)
        {
            if (Unmixed.volumeMl > 0)
            {
                contents = Merge(contents, Unmixed);
                Unmixed = default;
            }
            if (contents.volumeMl <= 0.0001f)
            {
                // Lọ chất rắn: lấy một phần chất rắn theo tỉ lệ "ml" quy ước (1 ml ≈ 1 g)
                return default;
            }
            var t = contents.Portion(Mathf.Min(ml, contents.volumeMl));
            contents -= t;
            visualDirty = true;
            return t;
        }

        /// <summary>Đổ hết ra ngoài (bộ dụng cụ bị đổ, bình vỡ). Báo sự kiện Spilled để robot / cảnh báo xử lý. Trả về ml đã đổ.</summary>
        public float SpillAll()
        {
            var s = Take(VolumeMl);
            if (s.IsEmpty) return 0;
            Spilled?.Invoke(this, s);
            return s.volumeMl;
        }

        /// <summary>Xúc lấy một lượng chất rắn (mol tính theo tỉ lệ khối lượng) từ lọ không có nước.</summary>
        public Solution TakeSolidFraction(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            var t = contents.Scale(fraction);
            contents -= t;
            visualDirty = true;
            return t;
        }

        /// <summary>Máy ly tâm: kết tủa lắng hết ngay.</summary>
        public void ForceSettle()
        {
            if (Settled < 1f) { Settled = 1f; visualDirty = true; }
            settleHoldUntil = Time.time + 20f;
        }

        float settleHoldUntil;

        public void SetContents(Solution s)
        {
            contents = s;
            Unmixed = default;
            PinkPersistS = 0;
            TemperatureC = 25f;
            Settled = 0;
            if (reactive && s.volumeMl > 0.01f && !s.Mix.IsEmpty)
            {
                var b = s.Mix.ToBuilder();
                ChemEngine.Equilibrate(b, s.volumeMl / 1000.0, Env, null);
                contents.mix = b.ToMix();
            }
            visualDirty = true;
            UpdateVisual();
        }

        ChemEnv Env => new ChemEnv { TempC = TemperatureC, InFumeHood = InFumeHood, Stir = SwirlNow };

        Solution Merge(Solution a, Solution s)
        {
            var m = a + s;
            if (!reactive || m.volumeMl < 0.002f) return m;
            double V = m.volumeMl / 1000.0;
            double hA = 0, ohA = 0, hB = 0, ohB = 0;
            bool liquidBoth = a.volumeMl > 0.002f && s.volumeMl > 0.002f;
            if (liquidBoth)
            {
                ChemEngine.FreeIons(a.Mix, a.volumeMl / 1000.0, out hA, out ohA);
                ChemEngine.FreeIons(s.Mix, s.volumeMl / 1000.0, out hB, out ohB);
            }
            double sulfuricBefore = a.volumeMl > 0.01f ? a.Mix.Get("SO4") / (a.volumeMl / 1000.0) : 0;
            var b = m.Mix.ToBuilder();
            events.Clear();
            var res = ChemEngine.Equilibrate(b, V, Env, events);
            m.mix = b.ToMix();
            double heat = res.HeatJ;
            if (liquidBoth)
            {
                ChemEngine.FreeIons(m.Mix, V, out double hM, out double ohM);
                heat += ChemEngine.NeutralizationJ(hA, ohA, hB, ohB, hM, ohM);
            }
            // Pha loãng axit sunfuric đặc tỏa rất nhiều nhiệt; đổ nước vào axit đặc thì sôi bắn
            double sulfuricAfter = m.Mix.Get("SO4") / V;
            if (sulfuricBefore > 6 && sulfuricAfter < sulfuricBefore * 0.97)
            {
                double mol = a.Mix.Get("SO4");
                heat += 60000 * mol * (1 - sulfuricAfter / sulfuricBefore);
                if (s.volumeMl > 0.5f && s.Mix.Get("SO4") / (s.volumeMl / 1000.0) < 3)
                    events.Add(new ChemEvent
                    {
                        Kind = EventKind.Hazard, Severity = Severity.Danger, Hazard = "ban_be", Key = "nguyhiem:nuocvaoaxit",
                        Title = "Nước sôi bắn ra! Không được đổ nước vào axit đặc",
                        Detail = "Pha loãng H₂SO₄ đặc: rót từ từ axit vào nước và khuấy đều, không làm ngược lại.",
                    });
            }
            ApplyHeat(heat, m.volumeMl);
            Raise(events, res.HeatJ > 0 ? (float)res.HeatJ : 0f);
            return m;
        }

        void ApplyHeat(double joules, float volumeMl)
        {
            if (joules == 0 || volumeMl < 0.05f) return;
            float before = TemperatureC;
            TemperatureC = Mathf.Min(BoilCapC, TemperatureC + (float)(joules / (volumeMl * 4.18)));  // 1 ml ≈ 1 g nước
            if (TemperatureC - before > 6f && Notify("nhiet", 8f))
                ChemEventRaised?.Invoke(this, new ChemEvent
                {
                    Kind = EventKind.Heat, Severity = TemperatureC > 80 ? Severity.Warning : Severity.Info, Hazard = TemperatureC > 80 ? "bong" : null,
                    Key = "nhiet", Title = $"Dung dịch nóng lên (≈ {TemperatureC:0} °C)", Detail = "Phản ứng tỏa nhiệt (trung hòa, hòa tan, kim loại tác dụng với nước...).",
                });
        }

        bool Notify(string key, float minGap)
        {
            float now = Time.time;
            if (lastEvent.TryGetValue(key, out float t) && now - t < minGap) return false;
            lastEvent[key] = now;
            return true;
        }

        void Raise(List<ChemEvent> list, float _)
        {
            foreach (var e in list)
            {
                if (e.Kind == EventKind.Gas && e.Amount > 0 && !string.IsNullOrEmpty(e.GasId))
                    pendingGas[e.GasId] = Math.Min(1.0, (pendingGas.TryGetValue(e.GasId, out var pg) ? pg : 0) + e.Amount);
                if (e.Kind == EventKind.Gas && e.Amount > 0)
                {
                    GasRate = Mathf.Max(GasRate, (float)e.Amount * 8f + 0.001f);
                    GasId = e.GasId;
                    gasDecay = 1.5f;
                }
                if (InFumeHood && e.Hazard == "khi_doc" && e.Severity <= Severity.Danger)
                {
                    e.Detail = (e.Detail ?? "") + " Đang làm trong tủ hút: khí được hút ra ngoài.";
                    if (e.Severity > Severity.Notice) e.Severity = Severity.Notice;
                }
                if (Notify(e.Key ?? e.Title, e.Kind == EventKind.Gas ? 6f : 4f))
                    ChemEventRaised?.Invoke(this, e);
            }
            list.Clear();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (slowMixing && Unmixed.volumeMl > 0)
            {
                float k = diffusionRate + swirlMixRate * SwirlNow;
                float f = 1f - Mathf.Exp(-k * dt);
                var moved = Unmixed.Scale(f);
                if (Unmixed.volumeMl * (1 - f) < 1e-5f) moved = Unmixed;
                Unmixed -= moved;
                contents = Merge(contents, moved);
                visualDirty = true;
            }

            // Nhiệt: nguồn cấp nhiệt, nguội dần về nhiệt độ phòng
            if (HeatInputW > 0)
            {
                LastHeatedTime = Time.time;
                if (VolumeMl > 0.05f)
                {
                    float cap = Mathf.Max(5f, VolumeMl) * 4.18f;
                    float t = TemperatureC + HeatInputW * dt / cap;
                    if (t > BoilCapC)
                    {
                        // đã tới nhiệt độ sôi: nhiệt cấp thêm để hóa hơi (mạng dòng chảy dùng)
                        ExcessHeatJ = Mathf.Min(2e5f, ExcessHeatJ + (t - BoilCapC) * cap);
                        t = BoilCapC;
                    }
                    TemperatureC = t;
                    if (TemperatureC >= BoilCapC - 0.5f && Notify("soi", 10f))
                        ChemEventRaised?.Invoke(this, new ChemEvent { Kind = EventKind.Heat, Key = "soi", Title = $"Dung dịch sôi (≈ {BoilCapC:0} °C)", Detail = "Đun sôi: hướng miệng ống ra chỗ không có người, không đun khô.", Severity = Severity.Notice, Hazard = "bong" });
                }
                else DryHeatJ = Mathf.Min(2e5f, DryHeatJ + HeatInputW * dt);
            }
            HeatInputW = 0;
            // tỏa nhiệt ra không khí theo diện tích bề mặt (∝ V^2/3): bình 100 ml ở 86 °C mất ~20 W, nên đèn cồn 250 W đủ đun sôi, chưng cất
            float vol = Mathf.Max(1f, VolumeMl);
            float lossW = 0.35f * Mathf.Abs(TemperatureC - 25f) * Mathf.Pow(vol / 100f, 2f / 3f);
            TemperatureC = Mathf.MoveTowards(TemperatureC, 25f, lossW * dt / (vol * 4.18f));

            // Quá trình theo thời gian (kim loại tan, chất rắn tan, đun nóng...) chạy 5 lần/giây
            stepTimer += dt;
            if (reactive && stepTimer >= 0.2f)
            {
                float step = stepTimer;
                stepTimer = 0;
                if (contents.volumeMl > 0.002f && ChemEngine.NeedsStep(contents.Mix, TemperatureC))
                {
                    var b = contents.Mix.ToBuilder();
                    events.Clear();
                    float pp = contents.indicatorDrops;
                    var res = ChemEngine.Step(b, contents.volumeMl / 1000.0, step, Env, events, ref pp);
                    contents.indicatorDrops = pp;
                    if (res.Changed || events.Count > 0)
                    {
                        contents.mix = b.ToMix();
                        visualDirty = true;
                    }
                    ApplyHeat(res.HeatJ, contents.volumeMl);
                    Raise(events, 0);
                }
                // Kết tủa lắng dần khi để yên
                float targetSettle = SwirlNow > 0.2f && Time.time > settleHoldUntil ? 0f : 1f;
                float newSettle = Mathf.MoveTowards(Settled, targetSettle, step / (targetSettle > Settled ? 25f : 2f));
                if (Mathf.Abs(newSettle - Settled) > 0.01f) { Settled = newSettle; visualDirty = true; }
            }
            gasDecay -= dt;
            if (gasDecay <= 0) GasRate = Mathf.MoveTowards(GasRate, 0, dt * 0.05f);

            float bulk = contents.Pink;
            float local = 0;
            if (slowMixing && Unmixed.volumeMl > 0)
            {
                // vùng nhỏ (5% thể tích) quanh chỗ giọt rơi chứa toàn bộ phần chưa trộn
                var near = contents.Scale(0.05f) + Unmixed;
                near.indicatorDrops = contents.indicatorDrops + Unmixed.indicatorDrops;
                local = near.Pink * 0.6f;
            }
            float pink = Mathf.Max(bulk, local);
            if (Mathf.Abs(pink - PinkVisible) > 0.002f) visualDirty = true;
            PinkVisible = pink;
            PinkPersistS = bulk > 0.05f ? PinkPersistS + dt : 0f;
            if (visualDirty) UpdateVisual();
        }

        void UpdateVisual()
        {
            visualDirty = false;
            if (fill != null) fill.SetVolume(VolumeMl);
            double ph = contents.volumeMl > 0.002f ? contents.Ph : 7;
            var look = ChemColor.Compute(Total.Mix, Mathf.Max(0.001f, VolumeMl), ph, TemperatureC, Settled);
            Look = look;
            lastPh = (float)ph;
            if (liquidRenderer != null)
            {
                liquidRenderer.enabled = VolumeMl > 0.02f;
                var baseColor = new Color(look.Liquid.r, look.Liquid.g, look.Liquid.b, Mathf.Max(clearColor.a, look.Alpha));
                if (look.Alpha < 0.3f && look.Turbidity < 0.05f) baseColor = new Color(clearColor.r * look.Liquid.r / 0.86f, clearColor.g * look.Liquid.g / 0.93f, clearColor.b * look.Liquid.b, clearColor.a);
                var color = Color.Lerp(baseColor, pinkColor, PinkVisible * 0.8f);
                if (!reactive)
                {
                    // lọ trong kho (màu không đổi): dùng chung vật liệu theo màu để SRP Batcher gộp được hàng trăm lọ
                    if (liquidBase == null) liquidBase = liquidRenderer.sharedMaterial;
                    if (liquidBase != null) liquidRenderer.sharedMaterial = SharedTint(liquidBase, color);
                }
                else
                {
                    if (mpb == null) mpb = new MaterialPropertyBlock();
                    liquidRenderer.GetPropertyBlock(mpb);
                    mpb.SetColor(BaseColorId, color);
                    liquidRenderer.SetPropertyBlock(mpb);
                }
            }
            if (deposit != null)
            {
                float ml = look.DepositMl;
                bool show = ml > 0.002f;
                if (deposit.gameObject.activeSelf != show) deposit.gameObject.SetActive(show);
                if (show)
                {
                    float area = Mathf.PI * bottomRadiusM * bottomRadiusM;
                    float h = Mathf.Clamp(ml * 1e-6f / area, 0.0008f, 0.04f);
                    deposit.localScale = new Vector3(deposit.localScale.x, h / 2f, deposit.localScale.z);
                    deposit.localPosition = new Vector3(deposit.localPosition.x, (fill != null && fill.innerProfile.Length > 0 ? fill.innerProfile[0].y : 0.003f) + h / 2f, deposit.localPosition.z);
                    var r = deposit.GetComponent<Renderer>();
                    if (r != null)
                    {
                        if (mpb == null) mpb = new MaterialPropertyBlock();
                        r.GetPropertyBlock(mpb);
                        mpb.SetColor(BaseColorId, new Color(look.Deposit.r, look.Deposit.g, look.Deposit.b, 1));
                        r.SetPropertyBlock(mpb);
                    }
                }
            }
            if (glassRenderer != null)
            {
                if (mpb == null) mpb = new MaterialPropertyBlock();
                if (!glassBaseKnown && glassRenderer.sharedMaterial != null && glassRenderer.sharedMaterial.HasProperty(BaseColorId))
                {
                    glassBase = glassRenderer.sharedMaterial.GetColor(BaseColorId);
                    glassBaseKnown = true;
                }
                glassRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, look.Mirror ? new Color(0.82f, 0.85f, 0.88f, 0.85f) : glassBase);
                glassRenderer.SetPropertyBlock(mpb);
            }
        }
    }
}
