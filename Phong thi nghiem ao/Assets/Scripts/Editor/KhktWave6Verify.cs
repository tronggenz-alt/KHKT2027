using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class KhktWave6Verify
{
    public static void Run()
    {
        EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/PhongThiNghiem.unity");
        if (!scene.IsValid())
        {
            Debug.LogError("[W6_FATAL] Cannot open Assets/Scenes/PhongThiNghiem.unity");
            EditorApplication.Exit(1);
            return;
        }

        foreach (var s in Object.FindObjectsByType<Khkt.Input.SerialControllerSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Object.DestroyImmediate(s);

        new GameObject("KhktWave6Runner").AddComponent<Khkt.Demo.KhktWave6Runner>();
        EditorApplication.EnterPlaymode();
    }
}

