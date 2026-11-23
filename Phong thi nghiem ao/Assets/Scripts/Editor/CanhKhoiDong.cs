using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Bấm Play trong Unity luôn chạy từ màn hình chính (ManHinhChinh), như app thật.
    /// Mở dự án mà đang ở cảnh trống (Untitled) thì tự mở ManHinhChinh.
    /// Khi chạy ngầm (-batchmode: kiểm thử, xuất app) thì không ép về ManHinhChinh, vì bộ kiểm thử tự mở cảnh mình cần;
    /// nếu bị ép, màn chính đứng chờ máy chủ và bài thử treo (18/11 treo 1,5 giờ).
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
            if (Application.isBatchMode)
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

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
