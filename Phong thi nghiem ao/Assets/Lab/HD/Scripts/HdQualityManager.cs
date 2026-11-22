using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Khkt.Rendering.HD
{
    /// <summary>
    /// Quản lý 3 mức chất lượng đồ họa PBR cho VERA Lab HD:
    ///   • Ultra: RenderScale 1.0, MSAA 4x, 4 tầng cascade, bóng mềm, tầm bóng 45 m (mặc định trên máy tính).
    ///   • Cao: RenderScale 0.9, MSAA 2x, 3 tầng cascade, bóng mềm, tầm bóng 35 m (mặc định trên iPad).
    ///   • Cân bằng: RenderScale 0.8, không MSAA, 2 tầng cascade, bóng cứng, tầm bóng 25 m (không thấp hơn bản cũ).
    ///
    /// Kèm bộ giữ nhịp 60 FPS: khi máy không theo kịp (cảnh nặng, máy nóng tự hạ xung) thì hạ dần độ phân giải kết xuất,
    /// khi dư sức thì nâng lại. Người dùng chỉ thấy hình hơi mềm đi một chút thay vì khung hình giật.
    /// </summary>
    public enum HdQualityTier
    {
        Ultra = 0,
        Cao = 1,
        CanBang = 2
    }

    public class HdQualityManager : MonoBehaviour
    {
        public static HdQualityManager Instance { get; private set; }

        public HdQualityTier CurrentTier { get; private set; } = HdQualityTier.Ultra;

        /// <summary>Độ phân giải kết xuất hiện tại (đã tính phần bộ giữ nhịp hạ xuống).</summary>
        public float DynamicScale { get; private set; } = 1f;

        /// <summary>Bộ giữ nhịp 60 FPS (tắt: giữ nguyên độ phân giải của mức đồ họa).</summary>
        public bool governorEnabled = true;

        public static event Action<HdQualityTier> OnQualityChanged;

        const string PrefKey = "VERA_HD_QUALITY_TIER";

        // ---- Bộ giữ nhịp ----
        const float MaxDrop = 0.25f;      // hạ tối đa 0,25 so với mức đồ họa
        const float FloorScale = 0.65f;   // không bao giờ thấp hơn mức này (chữ nhãn lọ vẫn đọc được)
        const float DropStep = 0.07f;
        const float RaiseStep = 0.05f;
        const float SlowFps = 54f;        // trung bình một giây dưới mức này là không theo kịp
        const float FullFps = 58.5f;      // trung bình một giây từ mức này là đủ nhịp
        const int MinGoodWindows = 8;
        const int MaxGoodWindows = 64;

        float baseScale = 1f;
        float winTime;
        int winFrames, winSlowFrames;
        int badWindows, goodWindows;
        int goodNeeded = MinGoodWindows;
        float holdUntil;
        float lastRaiseAt = -100f;

#if UNITY_EDITOR
        // Trong Editor các số này ghi thẳng vào file thiết lập URP: nhớ lại để trả về khi thoát Play
        UniversalRenderPipelineAsset editedAsset;
        float savedScale, savedShadowDistance;
        int savedMsaa, savedCascades;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ConfigureTargetFrameRate() => FramePacing.Apply();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InitHdSystem()
        {
            if (Instance == null)
            {
                var go = new GameObject("HD_RuntimeController");
                go.AddComponent<HdQualityManager>();
                go.AddComponent<HdPerformanceHud>();
                DontDestroyOnLoad(go);
            }
        }

        /// <summary>Mức mặc định khi người dùng chưa tự chọn: máy tính bảng dùng "Cao" (đủ nét trên màn 264 ppi, GPU không chạy sát trần).</summary>
        public static HdQualityTier DefaultTier => Application.isMobilePlatform ? HdQualityTier.Cao : HdQualityTier.Ultra;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR
            editedAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (editedAsset != null)
            {
                savedScale = editedAsset.renderScale;
                savedMsaa = editedAsset.msaaSampleCount;
                savedShadowDistance = editedAsset.shadowDistance;
                savedCascades = editedAsset.shadowCascadeCount;
            }
#endif

            // Bài kiểm thử tự động so ảnh từng điểm ảnh: giữ nguyên độ phân giải
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("KHKT_TEST"))) governorEnabled = false;

            int saved = PlayerPrefs.GetInt(PrefKey, (int)DefaultTier);
            SetTier((HdQualityTier)Mathf.Clamp(saved, 0, 2), false);
        }

        void OnEnable() => ZoneStreamer.ZoneChanged += OnZoneChanged;

        void OnDisable() => ZoneStreamer.ZoneChanged -= OnZoneChanged;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
#if UNITY_EDITOR
            if (editedAsset != null)
            {
                editedAsset.renderScale = savedScale;
                editedAsset.msaaSampleCount = savedMsaa;
                editedAsset.shadowDistance = savedShadowDistance;
                editedAsset.shadowCascadeCount = savedCascades;
            }
#endif
        }

        // Tải / gỡ khu làm vài khung hình chậm vì đọc đĩa, không phải vì GPU quá tải: không tính vào bộ giữ nhịp
        void OnZoneChanged(int zone, bool loaded) => Hold(2f);

        void OnApplicationFocus(bool focus) { if (focus) Hold(2f); }

        void Hold(float seconds)
        {
            holdUntil = Mathf.Max(holdUntil, Time.unscaledTime + seconds);
            winTime = 0f; winFrames = 0; winSlowFrames = 0; badWindows = 0;
        }

        void Update()
        {
            // Phím tắt đổi mức chất lượng nhanh trên máy tính/bàn phím rời
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f6Key.wasPressedThisFrame) CycleNextTier();

            Govern(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Mỗi giây xét một lần:
        ///   • hai giây liền dưới 54 FPS: hạ độ phân giải một nấc;
        ///   • đủ nhịp liên tục 8 giây: thử nâng một nấc. Nâng xong mà tụt ngay thì lần sau chờ lâu gấp đôi (tối đa 64 giây),
        ///     để không nhấp nhổm lên xuống ở cảnh nặng.
        /// </summary>
        void Govern(float dt)
        {
            if (!governorEnabled || !FramePacing.Capped) return;   // bài đo hiệu năng bỏ giới hạn FPS: không can thiệp
            if (dt <= 0f || dt > 0.25f) return;                    // khung treo (tải cảnh, app bị che): bỏ qua
            if (Time.unscaledTime < holdUntil) return;

            winTime += dt;
            winFrames++;
            if (dt > 1f / 50f) winSlowFrames++;
            if (winTime < 1f) return;

            float fps = winFrames / winTime;
            float slowShare = (float)winSlowFrames / winFrames;
            winTime = 0f; winFrames = 0; winSlowFrames = 0;

            float floor = Mathf.Max(FloorScale, baseScale - MaxDrop);
            if (fps < SlowFps)
            {
                goodWindows = 0;
                if (++badWindows < 2 || DynamicScale <= floor + 0.001f) return;
                badWindows = 0;
                // vừa nâng xong đã tụt: mức vừa thử là quá sức, giãn lần thử sau
                if (Time.unscaledTime - lastRaiseAt < 6f) goodNeeded = Mathf.Min(MaxGoodWindows, goodNeeded * 2);
                ApplyScale(Mathf.Max(floor, DynamicScale - DropStep));
            }
            else
            {
                badWindows = 0;
                if (fps < FullFps || slowShare > 0.03f) { goodWindows = 0; return; }
                if (DynamicScale >= baseScale - 0.001f) { goodWindows = 0; goodNeeded = MinGoodWindows; return; }
                if (++goodWindows < goodNeeded) return;
                goodWindows = 0;
                lastRaiseAt = Time.unscaledTime;
                ApplyScale(Mathf.Min(baseScale, DynamicScale + RaiseStep));
            }
        }

        void ApplyScale(float scale)
        {
            if (Mathf.Abs(scale - DynamicScale) < 0.001f) return;
            DynamicScale = scale;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null) urp.renderScale = scale;
            Hold(1.5f);  // đổi kích thước bộ đệm làm một khung chậm: đợi ổn định rồi mới đo tiếp
        }

        public void CycleNextTier()
        {
            int next = ((int)CurrentTier + 1) % 3;
            SetTier((HdQualityTier)next, true);
        }

        public void SetTier(HdQualityTier tier, bool save)
        {
            CurrentTier = tier;
            if (save) PlayerPrefs.SetInt(PrefKey, (int)tier);

            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                switch (tier)
                {
                    case HdQualityTier.Ultra:
                        baseScale = 1.0f;
                        urp.msaaSampleCount = 4;
                        urp.shadowDistance = 45f;
                        urp.shadowCascadeCount = 4;
                        SetSoftShadows(urp, true);
                        QualitySettings.antiAliasing = 4;
                        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                        break;

                    case HdQualityTier.Cao:
                        baseScale = 0.9f;
                        urp.msaaSampleCount = 2;
                        urp.shadowDistance = 35f;
                        urp.shadowCascadeCount = 3;
                        SetSoftShadows(urp, true);
                        QualitySettings.antiAliasing = 2;
                        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                        break;

                    case HdQualityTier.CanBang:
                        baseScale = 0.8f;
                        urp.msaaSampleCount = 1;
                        urp.shadowDistance = 25f;
                        urp.shadowCascadeCount = 2;
                        SetSoftShadows(urp, false);
                        QualitySettings.antiAliasing = 0;
                        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                        break;
                }
                // đổi mức: bắt đầu lại từ độ phân giải gốc của mức đó, bộ giữ nhịp tự hạ nếu cần
                DynamicScale = baseScale;
                urp.renderScale = baseScale;
            }
            goodWindows = 0;
            goodNeeded = MinGoodWindows;
            Hold(2f);

            OnQualityChanged?.Invoke(tier);
            Debug.Log($"[VERA Lab HD] Đã chuyển mức đồ họa sang: {tier}");
        }

        static void SetSoftShadows(UniversalRenderPipelineAsset urp, bool enable)
        {
            try
            {
                var prop = typeof(UniversalRenderPipelineAsset).GetProperty("supportsSoftShadows",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(urp, enable);
                }
            }
            catch { }
        }

        public string GetTierName(HdQualityTier tier)
        {
            switch (tier)
            {
                case HdQualityTier.Ultra: return "Ultra (Siêu thực)";
                case HdQualityTier.Cao: return "Cao (Mượt mà)";
                case HdQualityTier.CanBang: return "Cân bằng (Tiết kiệm pin)";
                default: return tier.ToString();
            }
        }
    }
}
