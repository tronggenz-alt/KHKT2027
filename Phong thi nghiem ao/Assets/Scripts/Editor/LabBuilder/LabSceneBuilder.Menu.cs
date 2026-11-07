using Khkt.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Khkt.EditorTools
{
    public static partial class LabSceneBuilder
    {
        public const string MenuScenePath = "Assets/Scenes/ManHinhChinh.unity";
        const string MenuArtDir = "Assets/Lab/Logo/";

        [MenuItem("KHKT/Dựng màn hình chính")]
        public static void BuildMenuOnly()
        {
            BuildMenuScene();
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Cảnh màn hình chính: chỉ có camera và MainMenu, giao diện dựng lúc chạy như các bảng khác.</summary>
        static void BuildMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Camera màn hình chính");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.white;
            cam.orthographic = true;
            var extra = cam.GetUniversalAdditionalCameraData();
            extra.renderPostProcessing = false;
            extra.renderShadows = false;
            camGo.AddComponent<AudioListener>();

            var menu = new GameObject("Màn hình chính").AddComponent<MainMenu>();
            menu.menuCamera = cam;
            menu.background = MenuTexture("NenManHinhChinh.jpg");
            menu.logo = MenuTexture("LogoVera.png");
            menu.schoolLogo = MenuTexture("LogoTruongLeQuyDon.png");

            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        static Texture2D MenuTexture(string file)
        {
            string path = MenuArtDir + file;
            if (AssetImporter.GetAtPath(path) is TextureImporter imp)
            {
                // ảnh giao diện: giữ đúng tỉ lệ để tính phần cắt của ảnh nền
                if (imp.npotScale != TextureImporterNPOTScale.None || imp.mipmapEnabled)
                {
                    imp.npotScale = TextureImporterNPOTScale.None;
                    imp.mipmapEnabled = false;
                    imp.SaveAndReimport();
                }
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) Debug.LogWarning("[KHKT] Thiếu ảnh màn hình chính: " + path);
            return tex;
        }
    }
}
