using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Xuất bản iOS ra project Xcode. Gọi từ dòng lệnh:
    ///   -buildTarget iOS -executeMethod Khkt.EditorTools.IosBuild.Device     (Builds/iOS, chạy trên iPhone / iPad thật)
    ///   -buildTarget iOS -executeMethod Khkt.EditorTools.IosBuild.Simulator  (Builds/iOS-Simulator, chạy trên máy ảo)
    /// </summary>
    public static class IosBuild
    {
        const string DisplayName = "VERA Lab HD";
        const string BundleId = "com.trantrong.veralab.hd";
        const string TeamId = "29263U3K84";  // nhóm cá nhân trong Xcode (Settings > Accounts)
        const string MeshPath = "Assets/Lab/Tang2/GiaiPhau/BP3D2_xuong.asset";

        [MenuItem("KHKT/Xuất iPad (Xcode)", priority = 100)]
        public static void Device() => Run(iOSSdkVersion.DeviceSDK, "Builds/iOS_HD");
        public static void Simulator() => Run(iOSSdkVersion.SimulatorSDK, "Builds/iOS_HD-Simulator");

        static void Run(iOSSdkVersion sdk, string outDir)
        {
            // Lưới giải phẫu không nằm trong git: chưa có thì dựng lại cảnh để sinh lưới và nối vào cảnh
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) == null)
                LabSceneBuilder.Build();

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Debug.Log($"[IOS-BUILD] {sdk}, {scenes.Length} cảnh → {outDir}");

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.iOS.appleDeveloperTeamID = TeamId;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.sdkVersion = sdk;
            PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;  // mặc định x86_64, không chạy trên máy chip Apple
            // Góc nhìn thứ nhất: chỉ xoay ngang
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // Tên hiển thị chỉ đổi lúc xuất: tên cũ quyết định chỗ lưu dữ liệu của bản máy tính
            string oldName = PlayerSettings.productName;
            PlayerSettings.productName = DisplayName;
            BuildReport report;
            try
            {
                Directory.CreateDirectory(outDir);
                report = BuildPipeline.BuildPlayer(scenes, outDir, BuildTarget.iOS, BuildOptions.None);
            }
            finally
            {
                PlayerSettings.productName = oldName;
                AssetDatabase.SaveAssets();
            }

            var s = report.summary;
            Debug.Log($"[IOS-BUILD] {s.result}, lỗi {s.totalErrors}, {s.totalTime}, {s.outputPath}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
