using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Khkt.Demo;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Phòng thử quang học Kính Lỏng Apple (P3, P5, P6, P9):
    /// mở cảnh sạch PlayMode ở 2880x1800, dựng các trường hợp kiểm chuẩn (a)-(g)
    /// xuất ảnh ra ~/AppBuild/kinh_long_so_sanh/ và kiểm tra định lượng tiêu chí §12.2.
    /// </summary>
    public static class KinhLongPhongThu
    {
        public static void ChupVaKiemTraP3()
        {
            KhktKinhLongSetup.CaiRendererFeature();
            EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var runner = new GameObject("KhktPhongThuRunner").AddComponent<KhktPhongThuRunner>();
            runner.CheDo = "P3";
            EditorApplication.EnterPlaymode();
        }

        public static void KiemTraP4Batch()
        {
            KhktKinhLongSetup.CaiRendererFeature();
            EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var runner = new GameObject("KhktPhongThuRunner").AddComponent<KhktPhongThuRunner>();
            runner.CheDo = "P4";
            EditorApplication.EnterPlaymode();
        }

        public static void KiemTraP5Batch()
        {
            KhktKinhLongSetup.CaiRendererFeature();
            EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var runner = new GameObject("KhktPhongThuRunner").AddComponent<KhktPhongThuRunner>();
            runner.CheDo = "P5";
            EditorApplication.EnterPlaymode();
        }
    }
}
