using System.Linq;
using Khkt.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Chỉ trong bản sao: dựng bản .exe mở từ màn hình chính, kèm KhktCacheBench.
public static class KhktCacheBuild
{
    public static void BuildBench()
    {
        string outDir = System.Environment.GetEnvironmentVariable("KHKT_BUILD");
        PlayerSettings.runInBackground = true;  // không tự dừng khi cửa sổ chưa được Windows trao quyền nhập
        var scene = EditorSceneManager.OpenScene(LabSceneBuilder.MenuScenePath);
        new GameObject("Đo cache").AddComponent<KhktCacheBench>();
        EditorSceneManager.SaveScene(scene);
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var report = BuildPipeline.BuildPlayer(scenes, System.IO.Path.Combine(outDir, "PhongThiNghiemAo.exe"), BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log($"[KHKT-BUILD] {report.summary.result} {report.summary.totalSize / 1048576} MB {report.summary.totalTime}");
        EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
}
