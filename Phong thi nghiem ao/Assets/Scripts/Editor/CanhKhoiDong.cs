using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Bấm Play trong Unity luôn chạy từ màn hình chính (ManHinhChinh), như app thật.
    /// Mở dự án mà đang ở cảnh trống (Untitled) thì tự mở ManHinhChinh.
    /// </summary>
    [InitializeOnLoad]
    static class CanhKhoiDong
    {
        const string CanhChinh = "Assets/Scenes/ManHinhChinh.unity";

        static CanhKhoiDong()
        {
            EditorApplication.delayCall += CaiDat;
        }

        static void CaiDat()
        {
            var canh = AssetDatabase.LoadAssetAtPath<SceneAsset>(CanhChinh);
            if (canh == null) return;
            EditorSceneManager.playModeStartScene = canh;

            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var dangMo = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(dangMo.path) && !dangMo.isDirty)
                EditorSceneManager.OpenScene(CanhChinh);
        }
    }
}
