using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Khkt.Demo;
using Khkt.Rendering;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Công cụ thiết lập & kiểm thử tự động các pha Kính Lỏng Apple (Renderer Feature, AlwaysIncludedShaders, P1 two-layer).
    /// </summary>
    public static class KhktKinhLongSetup
    {
        static readonly string[] RendererPaths =
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset"
        };

        [MenuItem("VERA/Kính Lỏng/1. Gắn KinhLongBlurFeature vào URP Renderers")]
        public static void CaiRendererFeature()
        {
            Shader blurShader = Shader.Find("Hidden/Khkt/KinhLongThapMo");
            int updated = 0;

            foreach (string path in RendererPaths)
            {
                var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (rd == null)
                {
                    Debug.LogWarning($"[KhktKinhLongSetup] Không tìm thấy {path}");
                    continue;
                }

                KinhLongBlurFeature existing = null;
                foreach (var f in rd.rendererFeatures)
                {
                    if (f is KinhLongBlurFeature bf)
                    {
                        existing = bf;
                        break;
                    }
                }

                if (existing == null)
                {
                    var feat = ScriptableObject.CreateInstance<KinhLongBlurFeature>();
                    feat.name = "KinhLongBlurFeature";
                    var so = new SerializedObject(feat);
                    var sp = so.FindProperty("blurShader");
                    if (sp != null && blurShader != null)
                    {
                        sp.objectReferenceValue = blurShader;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    AssetDatabase.AddObjectToAsset(feat, rd);
                    rd.rendererFeatures.Add(feat);
                    rd.SetDirty();
                    EditorUtility.SetDirty(rd);
                    updated++;
                    Debug.Log($"[KhktKinhLongSetup] Đã gắn KinhLongBlurFeature vào {path}");
                }
                else
                {
                    existing.SetActive(true);
                    EditorUtility.SetDirty(existing);
                    EditorUtility.SetDirty(rd);
                }

                int navBit = 1 << KinhLongLop.LayerDieuHuong;
                if ((rd.opaqueLayerMask.value & navBit) != 0 || (rd.transparentLayerMask.value & navBit) != 0)
                {
                    rd.opaqueLayerMask &= ~navBit;
                    rd.transparentLayerMask &= ~navBit;
                    rd.SetDirty();
                    EditorUtility.SetDirty(rd);
                    Debug.Log($"[KhktKinhLongSetup] Đã loại Layer {KinhLongLop.LayerDieuHuong} khỏi default opaque/transparent masks của {path}");
                }
            }

            ThemShaderVaoAlwaysIncluded("Hidden/Khkt/KinhLongThapMo", "Khkt/KinhLongApple", "Khkt/BoGocAnh");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[KhktKinhLongSetup] Hoàn tất cài đặt Renderer Features (mới thêm: {updated}).");
        }

        public static void KiemTraP2Batch()
        {
            CaiRendererFeature();

            string[] shaders = { "Hidden/Khkt/KinhLongThapMo", "Khkt/KinhLongApple", "Khkt/BoGocAnh" };
            foreach (string sName in shaders)
            {
                Shader sh = Shader.Find(sName);
                if (sh == null)
                    throw new System.Exception($"[P2_FAIL] Không tìm thấy shader: {sName}");
                if (!sh.isSupported)
                    throw new System.Exception($"[P2_FAIL] Shader không được hỗ trợ trên GPU hiện tại: {sName}");

                int errCount = 0;
                int msgCount = ShaderUtil.GetShaderMessageCount(sh);
                if (msgCount > 0)
                {
                    var msgs = ShaderUtil.GetShaderMessages(sh);
                    foreach (var m in msgs)
                    {
                        if (m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                        {
                            Debug.LogError($"[P2_SHADER_ERR] {sName}: {m.message}");
                            errCount++;
                        }
                        else
                        {
                            Debug.LogWarning($"[P2_SHADER_WARN] {sName}: {m.message}");
                        }
                    }
                }
                if (errCount > 0)
                    throw new System.Exception($"[P2_FAIL] Shader {sName} có {errCount} lỗi biên dịch!");
                Debug.Log($"[P2 Check] Shader '{sName}' OK (isSupported=True, compilerMessages={msgCount})");
            }

            KinhLongQuangHoc.DamBaoKhoiTao();
            Texture2D lut = KinhLongQuangHoc.BangKhucXa;
            if (lut == null || lut.width != 128 || lut.height != 1 || lut.format != TextureFormat.RGBAHalf)
                throw new System.Exception($"[P2_FAIL] LUT Snell không hợp lệ: {(lut != null ? lut.width + "x" + lut.height + " " + lut.format : "null")}");

            Color mid = lut.GetPixel(16, 0);
            Color plateau = lut.GetPixel(126, 0);
            Debug.Log($"[P2 Check] LUT Snell 128x1 RGBAHalf OK: u=16/127={mid}, u=126/127={plateau}");
            if (Mathf.Abs(mid.r) < 0.01f || Mathf.Abs(plateau.r) > 0.5f)
                throw new System.Exception($"[P2_FAIL] Đường cong LUT khúc xạ bất thường: mid={mid}, plateau={plateau}");

            Debug.Log("[P2_PASS] Tất cả 3 shader biên dịch sạch & LUT khúc xạ Snell 128x1 hợp lệ!");
            EditorApplication.Exit(0);
        }

        public static void CaiRendererFeatureBatch()
        {
            CaiRendererFeature();
            EditorApplication.Exit(0);
        }

        public static void ThemShaderVaoAlwaysIncluded(params string[] shaderNames)
        {
            var gs = GraphicsSettings.GetGraphicsSettings();
            if (gs == null) return;
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null || !arr.isArray) return;

            bool changed = false;
            foreach (string sName in shaderNames)
            {
                Shader sh = Shader.Find(sName);
                if (sh == null)
                {
                    Debug.LogWarning($"[KhktKinhLongSetup] Chưa tìm thấy shader '{sName}' để thêm vào AlwaysIncludedShaders.");
                    continue;
                }
                bool found = false;
                for (int i = 0; i < arr.arraySize; i++)
                {
                    if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    int idx = arr.arraySize;
                    arr.InsertArrayElementAtIndex(idx);
                    arr.GetArrayElementAtIndex(idx).objectReferenceValue = sh;
                    changed = true;
                    Debug.Log($"[KhktKinhLongSetup] Đã thêm '{sName}' vào AlwaysIncludedShaders.");
                }
            }

            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(gs);
            }
        }

        /// <summary>
        /// Kiểm tra P1 trong PlayMode để uGUI & RenderGraph CameraStack chạy đầy đủ như app thật.
        /// </summary>
        public static void KiemTraP1Batch()
        {
            CaiRendererFeature();

            int layer = LayerMask.NameToLayer(KinhLongLop.TenLayerDieuHuong);
            Debug.Log($"[P1 Check] Layer '{KinhLongLop.TenLayerDieuHuong}' = {layer}");
            if (layer != 14)
            {
                Debug.LogError($"[P1_FAIL] Layer KinhDieuHuong sai chỉ số: {layer} (cần 14)");
                EditorApplication.Exit(1);
                return;
            }

            EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var runner = new GameObject("KhktPhongThuRunner").AddComponent<KhktPhongThuRunner>();
            runner.CheDo = "P1";
            EditorApplication.EnterPlaymode();
        }
    }
}
