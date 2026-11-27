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

            ThemShaderVaoAlwaysIncluded("Hidden/Khkt/KinhLongThapMo");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[KhktKinhLongSetup] Hoàn tất cài đặt Renderer Features (mới thêm: {updated}).");
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
