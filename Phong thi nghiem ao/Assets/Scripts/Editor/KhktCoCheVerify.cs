using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Kiểm thử mô-đun cơ chế 3D ⇄ Giấy (C3, 18/11):
///   ~/AppBuild/VERA_claude/dong_bo_va_chay.sh &lt;worktree&gt; KhktCoCheVerify.Run co_che
/// Mở cảnh phòng thí nghiệm, vào PlayMode, KhktCoCheRunner chạy các kiểm tra rồi thoát (0 = đạt). Ảnh: $TMPDIR/vera_coche_shots.
/// </summary>
public static class KhktCoCheVerify
{
    public static void Run()
    {
        EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/PhongThiNghiem.unity");
        if (!scene.IsValid())
        {
            Debug.LogError("[CC_FATAL] Không mở được Assets/Scenes/PhongThiNghiem.unity");
            EditorApplication.Exit(1);
            return;
        }
        foreach (var s in Object.FindObjectsByType<Khkt.Input.SerialControllerSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Object.DestroyImmediate(s);
        new GameObject("KhktCoCheRunner").AddComponent<Khkt.CoChe.KhktCoCheRunner>();
        EditorApplication.EnterPlaymode();
    }
}
