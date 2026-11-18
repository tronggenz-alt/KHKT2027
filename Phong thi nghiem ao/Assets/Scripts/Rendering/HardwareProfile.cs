using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Khkt.Rendering
{
    public enum MucDoHoa { Thap = 0, Vua = 1, Cao = 2, SieuNet = 3 }

    /// <summary>
    /// Quản lý hồ sơ phần cứng và mức đồ họa toàn cục.
    /// Tự động đề xuất mức hợp lý theo GPU/RAM/CPU, áp dụng tức thì không cần khởi động lại.
    /// </summary>
    public static class HardwareProfile
    {
        const string PrefTuDong = "KHKT_DO_HOA_TU_DONG";
        const string PrefMuc = "KHKT_DO_HOA_MUC";

        static bool initialized;
        static bool tuDong = true;
        static MucDoHoa muc = MucDoHoa.Cao;
        static bool skipApplyForBaseline;
        static string cliAaOverride;
        static bool sceneHooked;
        static bool shadersWarmed;
        static GameObject hookGo;

        sealed class HardwareProfileHook : MonoBehaviour
        {
            void Start()
            {
                if (!skipApplyForBaseline)
                {
                    DisableLegacyHdQualityManager();
                    ApplyCurrent();
                    WarmUpShadersOnce();
                }
            }
        }

        static readonly Dictionary<ParticleSystem, int> particleBaseMax = new Dictionary<ParticleSystem, int>();

        static readonly FieldInfo FiSoftShadows = typeof(UniversalRenderPipelineAsset)
            .GetField("m_SoftShadowsSupported", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo FiSoftShadowQuality = typeof(UniversalRenderPipelineAsset)
            .GetField("m_SoftShadowQuality", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo FiAdditionalShadows = typeof(UniversalRenderPipelineAsset)
            .GetField("m_AdditionalLightShadowsSupported", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo FiAnyShadows = typeof(UniversalRenderPipelineAsset)
            .GetField("m_AnyShadowsSupported", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo FiRendererDataList = typeof(UniversalRenderPipelineAsset)
            .GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);

#if UNITY_EDITOR
        struct AssetSnapshot
        {
            public UniversalRenderPipelineAsset asset;
            public float renderScale;
            public int msaa;
            public float shadowDist;
            public int cascades;
            public int shadowRes;
            public bool fsrOverride;
            public float fsrSharpness;
            public bool softShadows;
            public object softShadowQuality;
            public bool additionalShadows;
            public HDRColorBufferPrecision hdrPrecision;
            public bool depthTex;
            public bool opaqueTex;
#if ENABLE_UPSCALER_FRAMEWORK
            public string upscalerName;
#else
            public UpscalingFilterSelection upscalingFilter;
#endif
        }

        static AssetSnapshot? savedSnapshot;
        static readonly List<(ScriptableRendererFeature feature, bool active)> savedFeatures = new List<(ScriptableRendererFeature, bool)>();
        static bool editorHooked;
#endif

        public static MucDoHoa Muc
        {
            get { EnsureInitialized(); return muc; }
        }

        public static bool TuDong
        {
            get { EnsureInitialized(); return tuDong; }
        }

        public static event Action Changed;

        public static int SoManhCau => Muc switch
        {
            MucDoHoa.Thap => 12,
            MucDoHoa.Vua => 16,
            MucDoHoa.Cao => 24,
            _ => 32
        };

        public static int SoHatToiDa => Muc switch
        {
            MucDoHoa.Thap => 200,
            MucDoHoa.Vua => 500,
            MucDoHoa.Cao => 1500,
            _ => 3000
        };

        public static float TiLeRender => Muc switch
        {
            MucDoHoa.Thap => 0.7f,
            MucDoHoa.Vua => 0.85f,
            _ => 1f
        };

        public static bool BongMem => Muc >= MucDoHoa.Cao;

        public static string TenMuc(MucDoHoa m) => m switch
        {
            MucDoHoa.Thap => "Thấp",
            MucDoHoa.Vua => "Vừa",
            MucDoHoa.Cao => "Cao",
            MucDoHoa.SieuNet => "Siêu nét",
            _ => "Cao"
        };

        public static MucDoHoa DeXuat()
        {
            string gpu = (SystemInfo.graphicsDeviceName ?? string.Empty).ToLowerInvariant();
            int vramMb = SystemInfo.graphicsMemorySize;
            int ramMb = SystemInfo.systemMemorySize;
            int cores = SystemInfo.processorCount;

            if (gpu.Contains("apple"))
            {
                if ((gpu.Contains("max") || gpu.Contains("ultra")) && ramMb >= 24000)
                    return MucDoHoa.SieuNet;
                if (ramMb >= 7000)
                    return MucDoHoa.Cao;
                return MucDoHoa.Vua;
            }

            bool isIrisXe = gpu.Contains("iris") && gpu.Contains("xe");
            if (isIrisXe)
                return MucDoHoa.Vua;

            bool isStrongDiscrete = gpu.Contains("rtx") || gpu.Contains("radeon rx") ||
                                    gpu.Contains("radeon pro") || gpu.Contains("gtx 1070") ||
                                    gpu.Contains("gtx 1080") || gpu.Contains("gtx 1660") ||
                                    gpu.Contains("arc a");
            if (isStrongDiscrete)
            {
                if (vramMb >= 10000 && ramMb >= 24000 &&
                    (gpu.Contains("rtx 40") || gpu.Contains("rtx 50") || gpu.Contains("rtx 308") || gpu.Contains("rtx 309") || gpu.Contains("rx 79") || gpu.Contains("rx 78")))
                    return MucDoHoa.SieuNet;
                if (vramMb >= 5500 && ramMb >= 12000)
                    return MucDoHoa.Cao;
                return MucDoHoa.Vua;
            }

            if (gpu.Contains("uhd") || gpu.Contains("hd graphics") || gpu.Contains("llvmpipe") || gpu.Contains("swiftshader"))
                return MucDoHoa.Thap;

            if (vramMb >= 3500 && ramMb >= 8000 && cores >= 6)
                return MucDoHoa.Vua;

            return MucDoHoa.Thap;
        }

        public static string MoTaMay()
        {
            string gpu = string.IsNullOrWhiteSpace(SystemInfo.graphicsDeviceName)
                ? "GPU chưa rõ"
                : SystemInfo.graphicsDeviceName.Trim();
            int ramGb = Mathf.Max(1, Mathf.RoundToInt(SystemInfo.systemMemorySize / 1024f));
            return $"Máy này: {gpu}, {ramGb} GB → đề xuất {TenMuc(DeXuat())}";
        }

        public static void Dat(MucDoHoa m)
        {
            EnsureInitialized();
            tuDong = false;
            muc = m;
            PlayerPrefs.SetInt(PrefTuDong, 0);
            PlayerPrefs.SetInt(PrefMuc, (int)muc);
            PlayerPrefs.Save();
            ApplyCurrent();
            Changed?.Invoke();
        }

        public static void DatTuDong()
        {
            EnsureInitialized();
            tuDong = true;
            muc = DeXuat();
            PlayerPrefs.SetInt(PrefTuDong, 1);
            PlayerPrefs.SetInt(PrefMuc, (int)muc);
            PlayerPrefs.Save();
            ApplyCurrent();
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitBeforeSceneLoad()
        {
            EnsureInitialized();
            if (!sceneHooked)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                sceneHooked = true;
            }
#if UNITY_EDITOR
            if (!editorHooked)
            {
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                editorHooked = true;
            }
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InitAfterSceneLoad()
        {
            EnsureInitialized();
            if (!skipApplyForBaseline)
            {
                DisableLegacyHdQualityManager();
                ApplyCurrent();
                WarmUpShadersOnce();
            }
            if (hookGo == null)
            {
                hookGo = new GameObject("HardwareProfileHook");
                UnityEngine.Object.DontDestroyOnLoad(hookGo);
                hookGo.AddComponent<HardwareProfileHook>();
            }
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!skipApplyForBaseline)
                DisableLegacyHdQualityManager();
            ApplyCurrent();
        }

        static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            ParseCommandLineArgs(out bool hasCliMuc, out bool cliAuto, out MucDoHoa cliMuc);
            if (hasCliMuc)
            {
                tuDong = cliAuto;
                muc = cliAuto ? DeXuat() : cliMuc;
                return;
            }

            if (!PlayerPrefs.HasKey(PrefTuDong))
            {
                tuDong = true;
                muc = DeXuat();
                PlayerPrefs.SetInt(PrefTuDong, 1);
                PlayerPrefs.SetInt(PrefMuc, (int)muc);
                PlayerPrefs.Save();
            }
            else
            {
                tuDong = PlayerPrefs.GetInt(PrefTuDong, 1) != 0;
                if (tuDong)
                {
                    muc = DeXuat();
                }
                else
                {
                    int raw = PlayerPrefs.GetInt(PrefMuc, (int)DeXuat());
                    muc = (MucDoHoa)Mathf.Clamp(raw, (int)MucDoHoa.Thap, (int)MucDoHoa.SieuNet);
                }
            }
        }

        static void ParseCommandLineArgs(out bool hasCliMuc, out bool cliAuto, out MucDoHoa cliMuc)
        {
            hasCliMuc = false;
            cliAuto = true;
            cliMuc = MucDoHoa.Cao;
            skipApplyForBaseline = false;
            cliAaOverride = null;

            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, "-vera-truoc", StringComparison.OrdinalIgnoreCase))
                {
                    skipApplyForBaseline = true;
                }
                else if (a.StartsWith("-vera-muc=", StringComparison.OrdinalIgnoreCase))
                {
                    string val = a.Substring("-vera-muc=".Length).Trim().ToLowerInvariant();
                    hasCliMuc = true;
                    switch (val)
                    {
                        case "thap": cliAuto = false; cliMuc = MucDoHoa.Thap; break;
                        case "vua": cliAuto = false; cliMuc = MucDoHoa.Vua; break;
                        case "cao": cliAuto = false; cliMuc = MucDoHoa.Cao; break;
                        case "sieunet": cliAuto = false; cliMuc = MucDoHoa.SieuNet; break;
                        default: cliAuto = true; cliMuc = DeXuat(); break;
                    }
                }
                else if (a.StartsWith("-vera-aa=", StringComparison.OrdinalIgnoreCase))
                {
                    cliAaOverride = a.Substring("-vera-aa=".Length).Trim().ToLowerInvariant();
                }
            }
        }

        public static void ApplyCurrent()
        {
            if (skipApplyForBaseline) return;

            DisableLegacyHdQualityManager();
            ApplyRetinaResolution(muc);
            ApplyQualitySettings(muc);
            ApplyUrpAsset(muc);
            ApplyCameras(muc);
            ApplyVolumes(muc);
            ApplyFloorAndBenchTextures(muc);
            ApplyParticleCap();
        }

        static void DisableLegacyHdQualityManager()
        {
            var hdType = Type.GetType("Khkt.Rendering.HD.HdQualityManager, Assembly-CSharp");
            if (hdType == null) return;
            var found = UnityEngine.Object.FindObjectsByType(hdType, FindObjectsInactive.Include);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] is MonoBehaviour mb && mb != null)
                {
                    var govField = hdType.GetField("governorEnabled", BindingFlags.Public | BindingFlags.Instance);
                    govField?.SetValue(mb, false);
                    if (mb.enabled) mb.enabled = false;
                }
            }
        }

        static void WarmUpShadersOnce()
        {
            if (shadersWarmed || !Application.isPlaying) return;
            shadersWarmed = true;

            string[] shaderNames =
            {
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Simple Lit",
                "Universal Render Pipeline/Unlit",
                "Khkt/HD/ThuyTinhHD",
                "Khkt/LaCay",
                "Khkt/KinhMo",
                "Khkt/CuaSoAo",
                "Khkt/ChuTrongCanh",
                "Khkt/VienSang",
                "Khkt/TieuBan"
            };

            var svc = new ShaderVariantCollection();
            for (int i = 0; i < shaderNames.Length; i++)
            {
                var sh = Shader.Find(shaderNames[i]);
                if (sh == null) continue;
                try
                {
                    svc.Add(new ShaderVariantCollection.ShaderVariant(sh, PassType.ScriptableRenderPipeline));
                }
                catch { }
            }
            svc.WarmUp();
            UnityEngine.Object.Destroy(svc);
        }

        static void ApplyQualitySettings(MucDoHoa m)
        {
            QualitySettings.lodBias = m switch
            {
                MucDoHoa.Thap => 0.85f,
                MucDoHoa.Vua => 1.25f,
                MucDoHoa.Cao => 2.0f,
                _ => 2.5f
            };
            QualitySettings.globalTextureMipmapLimit = m == MucDoHoa.Thap ? 1 : 0;
            QualitySettings.anisotropicFiltering = m == MucDoHoa.Thap
                ? AnisotropicFiltering.Disable
                : (m == MucDoHoa.Vua ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable);
            QualitySettings.shadowDistance = ShadowDistanceFor(m);
            QualitySettings.shadowCascades = ShadowCascadesFor(m);
            QualitySettings.softParticles = m >= MucDoHoa.Cao;
            QualitySettings.antiAliasing = MsaaCountFor(m);
        }

        static void ApplyUrpAsset(MucDoHoa m)
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset
                      ?? QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return;

#if UNITY_EDITOR
            SaveSnapshotOnce(urp);
#endif

            float scale = TiLeRender;
            urp.renderScale = scale;
            bool needUpscale = scale < 0.999f;
#if ENABLE_UPSCALER_FRAMEWORK
            urp.upscalerName = needUpscale ? "FidelityFX Super Resolution 1.0" : "Automatic";
#else
            urp.upscalingFilter = needUpscale ? UpscalingFilterSelection.FSR : UpscalingFilterSelection.Auto;
#endif
            urp.fsrOverrideSharpness = needUpscale;
            urp.fsrSharpness = 0.92f;

            int msaa = MsaaCountFor(m);
            if (cliAaOverride == "msaa4x") msaa = 4;
            else if (cliAaOverride == "msaa2x") msaa = 2;
            else if (cliAaOverride == "taa" || cliAaOverride == "smaa" || cliAaOverride == "none") msaa = 1;
            urp.msaaSampleCount = msaa;

            urp.hdrColorBufferPrecision = m >= MucDoHoa.SieuNet
                ? HDRColorBufferPrecision._64Bits
                : HDRColorBufferPrecision._32Bits;

            urp.shadowDistance = ShadowDistanceFor(m);
            urp.shadowCascadeCount = ShadowCascadesFor(m);
            urp.mainLightShadowmapResolution = m switch
            {
                MucDoHoa.Thap => 1024,
                MucDoHoa.Vua => 2048,
                MucDoHoa.Cao => 2048,
                _ => 4096
            };

            bool soft = BongMem;
            FiSoftShadows?.SetValue(urp, soft);
            FiAnyShadows?.SetValue(urp, true);
            FiAdditionalShadows?.SetValue(urp, m >= MucDoHoa.Cao);

            if (FiSoftShadowQuality != null)
            {
                Type qType = FiSoftShadowQuality.FieldType;
                int qVal = m >= MucDoHoa.SieuNet ? 2 : (m >= MucDoHoa.Cao ? 1 : 0);
                FiSoftShadowQuality.SetValue(urp, Enum.ToObject(qType, qVal));
            }

            bool enableSsao = m >= MucDoHoa.SieuNet;
            urp.supportsCameraDepthTexture = enableSsao;
            urp.supportsCameraOpaqueTexture = m >= MucDoHoa.Vua;

            if (FiRendererDataList?.GetValue(urp) is ScriptableRendererData[] renderers)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var rd = renderers[i];
                    if (rd == null || rd.rendererFeatures == null) continue;
                    for (int f = 0; f < rd.rendererFeatures.Count; f++)
                    {
                        var feat = rd.rendererFeatures[f];
                        if (feat != null && feat.name.Contains("Occlusion", StringComparison.OrdinalIgnoreCase))
                        {
#if UNITY_EDITOR
                            SaveFeatureSnapshotOnce(feat);
#endif
                            feat.SetActive(enableSsao);
                        }
                    }
                }
            }
        }

        static void ApplyCameras(MucDoHoa m)
        {
            var cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            for (int i = 0; i < cams.Length; i++)
            {
                var cam = cams[i];
                if (cam == null) continue;
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data == null || data.renderType != CameraRenderType.Base) continue;
                if (cam.name.Contains("Preview", StringComparison.OrdinalIgnoreCase) ||
                    cam.name.Contains("Impostor", StringComparison.OrdinalIgnoreCase))
                    continue;

                // camera chỉ vẽ giao diện (hoặc đã gắn CameraKhongHauKy): không hậu kỳ, tonemapping làm trắng thành xám
                if (CameraKhongHauKy.BoQua(cam))
                {
                    data.renderPostProcessing = false;
                    continue;
                }

                data.renderPostProcessing = m != MucDoHoa.Thap;

                if (!string.IsNullOrEmpty(cliAaOverride))
                {
                    switch (cliAaOverride)
                    {
                        case "msaa4x":
                        case "msaa2x":
                            data.antialiasing = AntialiasingMode.None;
                            break;
                        case "smaa":
                            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                            data.antialiasingQuality = AntialiasingQuality.High;
                            break;
                        case "taa":
                            data.antialiasing = AntialiasingMode.TemporalAntiAliasing;
                            break;
                        case "none":
                            data.antialiasing = AntialiasingMode.None;
                            break;
                    }
                    continue;
                }

                // Trên Apple Silicon Metal TBDR, MSAA phần cứng nằm gọn trong tile memory:
                // sắc nét chữ trên kính/buret hơn SMAA/TAA và tiết kiệm ~3 ms GPU.
                data.antialiasing = AntialiasingMode.None;
            }
        }

        static void ApplyVolumes(MucDoHoa m)
        {
            if (!Application.isPlaying) return;
            var vols = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include);
            bool hqBloom = m >= MucDoHoa.SieuNet;
            for (int i = 0; i < vols.Length; i++)
            {
                var vol = vols[i];
                if (vol == null || vol.profile == null) continue;
                if (vol.profile.TryGet(out Bloom bloom))
                {
                    bloom.highQualityFiltering.overrideState = true;
                    bloom.highQualityFiltering.value = hqBloom;
                }
            }
        }

        static void ApplyFloorAndBenchTextures(MucDoHoa m)
        {
            if (!Application.isPlaying) return;
            int targetAniso = m switch
            {
                MucDoHoa.Thap => 1,
                MucDoHoa.Vua => 4,
                MucDoHoa.Cao => 8,
                _ => 16
            };
            float targetBias = m switch
            {
                MucDoHoa.Cao => -0.35f,
                MucDoHoa.SieuNet => -0.5f,
                _ => 0f
            };
            targetBias = Mathf.Clamp(targetBias, -0.5f, 0f);

            var mats = Resources.FindObjectsOfTypeAll<Material>();
            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat == null) continue;
                string mn = mat.name ?? string.Empty;
                bool isFloorOrBench = mn.StartsWith("San", StringComparison.OrdinalIgnoreCase) ||
                                      mn.StartsWith("T2_San", StringComparison.OrdinalIgnoreCase) ||
                                      mn.StartsWith("MatBan", StringComparison.OrdinalIgnoreCase) ||
                                      mn.StartsWith("T2_Ban", StringComparison.OrdinalIgnoreCase);
                if (!isFloorOrBench) continue;

                ApplyTexParams(mat, "_BaseMap", targetAniso, targetBias);
                ApplyTexParams(mat, "_BumpMap", targetAniso, targetBias);
                ApplyTexParams(mat, "_MetallicGlossMap", targetAniso, targetBias);
            }
        }

        static void ApplyTexParams(Material mat, string prop, int aniso, float bias)
        {
            if (!mat.HasProperty(prop)) return;
            var tex = mat.GetTexture(prop);
            if (tex == null) return;
            if (tex.anisoLevel < aniso || aniso == 1) tex.anisoLevel = aniso;
            // Không bao giờ đặt mipMapBias âm hơn -0.5
            tex.mipMapBias = Mathf.Clamp(bias, -0.5f, 0f);
        }

        static void ApplyParticleCap()
        {
            int cap = SoHatToiDa;
            var systems = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include);
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null) continue;
                var main = ps.main;
                if (!particleBaseMax.TryGetValue(ps, out int orig))
                {
                    orig = main.maxParticles;
                    particleBaseMax[ps] = orig;
                }
                main.maxParticles = Mathf.Min(orig, cap);
            }
        }

        static void ApplyRetinaResolution(MucDoHoa m)
        {
            if (Application.isBatchMode || Application.isEditor) return;
            if (m < MucDoHoa.Cao) return;
            if (Display.main == null) return;
            int physW = Display.main.systemWidth;
            int physH = Display.main.systemHeight;
            if (physW <= 0 || physH <= 0) return;

            if (Screen.width != physW || Screen.height != physH || Screen.fullScreenMode != FullScreenMode.FullScreenWindow)
                Screen.SetResolution(physW, physH, FullScreenMode.FullScreenWindow);
        }

        static float ShadowDistanceFor(MucDoHoa m) => m switch
        {
            MucDoHoa.Thap => 18f,
            MucDoHoa.Vua => 22f,
            MucDoHoa.Cao => 28f,
            _ => 45f
        };

        static int ShadowCascadesFor(MucDoHoa m) => m switch
        {
            MucDoHoa.Thap => 1,
            MucDoHoa.Vua => 2,
            MucDoHoa.Cao => 2,
            _ => 4
        };

        static int MsaaCountFor(MucDoHoa m)
        {
            bool ultraHighRes = Screen.width >= 2560 || (Display.main != null && Display.main.systemWidth >= 2560 && m == MucDoHoa.Cao);
            return m switch
            {
                MucDoHoa.Thap => 1,
                MucDoHoa.Vua => 2,
                MucDoHoa.Cao => ultraHighRes ? 2 : 4,
                _ => 4
            };
        }

#if UNITY_EDITOR
        static void SaveFeatureSnapshotOnce(ScriptableRendererFeature feat)
        {
            for (int i = 0; i < savedFeatures.Count; i++)
                if (savedFeatures[i].feature == feat) return;
            savedFeatures.Add((feat, feat.isActive));
        }

        static void SaveSnapshotOnce(UniversalRenderPipelineAsset urp)
        {
            if (savedSnapshot.HasValue) return;
            savedSnapshot = new AssetSnapshot
            {
                asset = urp,
                renderScale = urp.renderScale,
                msaa = urp.msaaSampleCount,
                shadowDist = urp.shadowDistance,
                cascades = urp.shadowCascadeCount,
                shadowRes = urp.mainLightShadowmapResolution,
                fsrOverride = urp.fsrOverrideSharpness,
                fsrSharpness = urp.fsrSharpness,
                softShadows = FiSoftShadows != null && (bool)FiSoftShadows.GetValue(urp),
                softShadowQuality = FiSoftShadowQuality != null ? FiSoftShadowQuality.GetValue(urp) : null,
                additionalShadows = FiAdditionalShadows != null && (bool)FiAdditionalShadows.GetValue(urp),
                hdrPrecision = urp.hdrColorBufferPrecision,
                depthTex = urp.supportsCameraDepthTexture,
                opaqueTex = urp.supportsCameraOpaqueTexture,
#if ENABLE_UPSCALER_FRAMEWORK
                upscalerName = urp.upscalerName
#else
                upscalingFilter = urp.upscalingFilter
#endif
            };
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !savedSnapshot.HasValue) return;
            var s = savedSnapshot.Value;
            savedSnapshot = null;
            if (s.asset == null) return;
            s.asset.renderScale = s.renderScale;
            s.asset.msaaSampleCount = s.msaa;
            s.asset.shadowDistance = s.shadowDist;
            s.asset.shadowCascadeCount = s.cascades;
            s.asset.mainLightShadowmapResolution = s.shadowRes;
            s.asset.fsrOverrideSharpness = s.fsrOverride;
            s.asset.fsrSharpness = s.fsrSharpness;
#if ENABLE_UPSCALER_FRAMEWORK
            s.asset.upscalerName = s.upscalerName;
#else
            s.asset.upscalingFilter = s.upscalingFilter;
#endif
            FiSoftShadows?.SetValue(s.asset, s.softShadows);
            if (FiSoftShadowQuality != null && s.softShadowQuality != null)
                FiSoftShadowQuality.SetValue(s.asset, s.softShadowQuality);
            FiAdditionalShadows?.SetValue(s.asset, s.additionalShadows);
            s.asset.hdrColorBufferPrecision = s.hdrPrecision;
            s.asset.supportsCameraDepthTexture = s.depthTex;
            s.asset.supportsCameraOpaqueTexture = s.opaqueTex;
            for (int i = 0; i < savedFeatures.Count; i++)
                if (savedFeatures[i].feature != null) savedFeatures[i].feature.SetActive(savedFeatures[i].active);
            savedFeatures.Clear();
        }
#endif
    }
}
