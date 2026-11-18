using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Xuất bản dựng cho macOS (và Windows nếu có module).
    /// Tự động loại các script kiểm thử khỏi bản dựng mà không xóa tệp khỏi dự án.
    /// </summary>
    public static class MacBuild
    {
        const string MeshPath = "Assets/Lab/Tang2/GiaiPhau/BP3D2_xuong.asset";
        static readonly Regex TestScriptPattern = new Regex(@"^Khkt.*(Test|Runner|Verify|Bench).*\.cs$", RegexOptions.IgnoreCase);

        static string MacOutputPath => Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "AppBuild", "VERA_mac", "VERA Lab.app");

        static string WindowsOutputPath => Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "AppBuild", "VERA_win", "VERA Lab.exe");

        [MenuItem("KHKT/Xuất macOS (Release)", priority = 101)]
        public static void Build() => RunMac(BuildOptions.None);

        [MenuItem("KHKT/Xuất macOS (Development)", priority = 102)]
        public static void BuildDev() => RunMac(BuildOptions.Development);

        [MenuItem("KHKT/Xuất Windows", priority = 103)]
        public static void BuildWindows()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                Debug.Log("[MAC-BUILD] Module StandaloneWindows64 chưa được cài trên Unity này; bỏ qua xuất Windows.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }
            RunBuild(BuildTarget.StandaloneWindows64, WindowsOutputPath, BuildOptions.None);
        }

        static void RunMac(BuildOptions options)
        {
            RunBuild(BuildTarget.StandaloneOSX, MacOutputPath, options);
        }

        static void RunBuild(BuildTarget target, string outPath, BuildOptions options)
        {
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) == null)
                LabSceneBuilder.Build();

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => s.path)
                .ToArray();

            string outDir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(outDir))
                Directory.CreateDirectory(outDir);

            Debug.Log($"[MAC-BUILD] Bắt đầu xuất {target}, {scenes.Length} cảnh → {outPath} ({options})");

            var backups = new List<(string fullPath, string originalContent)>();
            BuildReport report = null;
            try
            {
                GuardTestScripts(backups);
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outPath,
                    target = target,
                    options = options
                });
            }
            finally
            {
                RestoreTestScripts(backups);
            }

            if (report == null)
            {
                Debug.LogError("[MAC-BUILD] Không nhận được BuildReport.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var summary = report.summary;
            Debug.Log($"[MAC-BUILD] Kết quả: {summary.result}, lỗi: {summary.totalErrors}, cảnh báo: {summary.totalWarnings}, thời gian: {summary.totalTime.TotalSeconds:0.0}s, dung lượng: {summary.totalSize / (1024f * 1024f):0.0} MB → {summary.outputPath}");

            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static void GuardTestScripts(List<(string fullPath, string originalContent)> backups)
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "Scripts");
            if (!Directory.Exists(scriptsRoot)) return;

            bool changed = false;
            foreach (string file in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (normalized.Contains("/Editor/")) continue;

                string name = Path.GetFileName(file);
                if (!TestScriptPattern.IsMatch(name)) continue;

                string content = File.ReadAllText(file);
                if (content.TrimStart().StartsWith("#if UNITY_EDITOR || KHKT_TEST", StringComparison.Ordinal))
                    continue;

                backups.Add((file, content));
                File.WriteAllText(file, "#if UNITY_EDITOR || KHKT_TEST\n" + content + "\n#endif\n");
                changed = true;
                Debug.Log($"[MAC-BUILD] Tạm bọc script kiểm thử khỏi bản dựng: {name}");
            }

            if (changed)
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static void RestoreTestScripts(List<(string fullPath, string originalContent)> backups)
        {
            if (backups.Count == 0) return;
            foreach (var (fullPath, originalContent) in backups)
            {
                if (File.Exists(fullPath))
                    File.WriteAllText(fullPath, originalContent);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
