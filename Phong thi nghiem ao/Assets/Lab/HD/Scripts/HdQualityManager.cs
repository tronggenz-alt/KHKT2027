using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Khkt.Rendering.HD
{
    /// <summary>
    /// Quản lý 3 mức chất lượng đồ họa PBR cho VERA Lab HD:
    ///   • Ultra (Mặc định): RenderScale 1.0, 4 tầng cascade, bóng mềm cao cấp, tầm bóng 45 m.
    ///   • Cao: RenderScale 0.9, 3 tầng cascade, bóng mềm, tầm bóng 35 m.
    ///   • Cân bằng: RenderScale 0.8, 2 tầng cascade, bóng cứng, tầm bóng 25 m (không thấp hơn bản cũ).
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

        public static event Action<HdQualityTier> OnQualityChanged;

        const string PrefKey = "VERA_HD_QUALITY_TIER";

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

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            int saved = PlayerPrefs.GetInt(PrefKey, (int)HdQualityTier.Ultra);
            SetTier((HdQualityTier)saved, false);
        }

        void Update()
        {
            // Phím tắt đổi mức chất lượng nhanh trên máy tính/bàn phím rời
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.f6Key.wasPressedThisFrame)
            {
                CycleNextTier();
            }
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
                        urp.renderScale = 1.0f;
                        urp.shadowDistance = 45f;
                        urp.shadowCascadeCount = 4;
                        SetSoftShadows(urp, true);
                        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                        break;

                    case HdQualityTier.Cao:
                        urp.renderScale = 0.9f;
                        urp.shadowDistance = 35f;
                        urp.shadowCascadeCount = 3;
                        SetSoftShadows(urp, true);
                        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                        break;

                    case HdQualityTier.CanBang:
                        urp.renderScale = 0.8f;
                        urp.shadowDistance = 25f;
                        urp.shadowCascadeCount = 2;
                        SetSoftShadows(urp, false);
                        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                        break;
                }
            }

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
