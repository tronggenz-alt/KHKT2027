using Khkt.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Chỉ trong bản sao: dựng cảnh, thêm KhktAutoTest rồi vào Play. KhktAutoTest tự thoát Unity khi xong.
// BuildBench: dựng cảnh, thêm KhktBenchmark, xuất bản .exe (Windows 64-bit) để đo khung hình ngoài trình soạn thảo.
public static class KhktPlayVerify
{
    public static void Run()
    {
        EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);  // không nạp lại script giữa lúc Play
        LabSceneBuilder.Build();
        EditorSceneManager.OpenScene(LabSceneBuilder.ScenePath);
        // bài thử không được mở cổng COM của Mega / ESP32 thật đang cắm vào máy
        foreach (var s in Object.FindObjectsByType<Khkt.Input.SerialControllerSource>()) Object.DestroyImmediate(s);
        new GameObject("AutoTest").AddComponent<KhktAutoTest>();
        EditorApplication.EnterPlaymode();
    }

    public static void BuildBench()
    {
        string outDir = System.Environment.GetEnvironmentVariable("KHKT_BUILD");
        PlayerSettings.enableFrameTimingStats = true;
        PlayerSettings.runInBackground = true;  // không tự dừng khi cửa sổ chưa được Windows trao quyền nhập  // đo thời gian CPU / GPU mỗi khung hình trong bản .exe
        LabSceneBuilder.Build();
        var scene = EditorSceneManager.OpenScene(LabSceneBuilder.ScenePath);
        new GameObject("Benchmark").AddComponent<KhktBenchmark>();
        EditorSceneManager.SaveScene(scene);
        var report = BuildPipeline.BuildPlayer(new[] { LabSceneBuilder.ScenePath }, System.IO.Path.Combine(outDir, "PhongThiNghiemAo.exe"),
                                               BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log($"[KHKT-BUILD] {report.summary.result} {report.summary.totalSize / 1048576} MB {report.summary.totalTime}");
        EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }

    /// <summary>Bản .exe chơi được (không kèm bài kiểm thử, không kèm bộ đo): cảnh lõi + các cảnh khu theo Build Settings.</summary>
    public static void BuildPlay()
    {
        string outDir = System.Environment.GetEnvironmentVariable("KHKT_BUILD");
        PlayerSettings.runInBackground = true;
        LabSceneBuilder.Build();
        var scenes = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(EditorBuildSettings.scenes, s => s.enabled), s => s.path));
        Debug.Log($"[KHKT-BUILD] Cảnh đưa vào bản dựng ({scenes.Length}): {string.Join(", ", scenes)}");
        var report = BuildPipeline.BuildPlayer(scenes, System.IO.Path.Combine(outDir, "PhongThiNghiemAo.exe"), BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log($"[KHKT-BUILD] {report.summary.result} {report.summary.totalSize / 1048576} MB {report.summary.totalTime}");
        EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
}

