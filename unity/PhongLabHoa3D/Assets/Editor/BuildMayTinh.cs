using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PhongThiNghiem.Editor
{
    public static class BuildMayTinh
    {
        private const string DUONG_DAN_SCENE = "Assets/Scenes/PhongLab.unity";
        private const string TEN_UNG_DUNG = "PhongLabHoa3D";
        private const string SCHEME_DEEP_LINK = "phonglabhoa";
        private const string APP_ID = "vn.chemistry.phonglabhoa";

        [MenuItem("Phòng Lab/Build App macOS (Apple Silicon)", false, 100)]
        public static void Mac()
        {
            BuildMacThucThi(false);
        }

        public static void BuildMacBatch()
        {
            BuildMacThucThi(true);
        }

        [MenuItem("Phòng Lab/Build WebGL cho app CHEMISTRY", false, 102)]
        public static void WebGLApp()
        {
            BuildWebGLThucThi(false);
        }

        public static void WebGLAppBatch()
        {
            BuildWebGLThucThi(true);
        }

        private static void BuildMacThucThi(bool isBatchmode)
        {
            Debug.Log("[BuildMayTinh] Bắt đầu build ứng dụng macOS standalone...");

            // 1. Đồng bộ dữ liệu mới nhất
            DongBoDuLieu.DongBo();

            // 2. Đảm bảo scene chính tồn tại
            DamBaoSceneTonTai();

            // 3. Thiết lập cấu hình Player
            PlayerSettings.productName = "PhongLabHoa3D";
            PlayerSettings.companyName = "Antigravity";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, APP_ID);

            // Cấu hình URP Render Pipeline Asset để vật liệu không bị tím (magenta)
            var urpAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/URP_Asset.asset");
            if (urpAsset != null)
            {
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = urpAsset;
                QualitySettings.renderPipeline = urpAsset;
                Debug.Log("[BuildMayTinh] Đã cấu hình URP Render Pipeline Asset thành công.");
            }

            // 4. Đường dẫn xuất
            string thuMucBuild = Path.Combine(Application.dataPath, "..", "Builds", "macOS");
            Directory.CreateDirectory(thuMucBuild);
            string duongDanApp = Path.Combine(thuMucBuild, $"{TEN_UNG_DUNG}.app");

            BuildPlayerOptions opt = new BuildPlayerOptions
            {
                scenes = new string[] { DUONG_DAN_SCENE },
                locationPathName = duongDanApp,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(opt);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildMayTinh] Build macOS thành công! Kích thước: {summary.totalSize / (1024 * 1024)} MB");

                // 5. Đăng ký Deep Link URL Scheme phonglabhoa và tên hiển thị vào Info.plist
                DangKyDeepLinkMac(duongDanApp);

                // 6. Ký ad-hoc codesign
                KyAdHocCodesign(duongDanApp);

                // 7. Nén zip
                TaoGoiZipMac(thuMucBuild, duongDanApp);

                // 8. Chép sang outputs/app-lab/
                SaoChepMacSangOutputs(duongDanApp);

                Debug.Log("[BuildMayTinh] Hoàn thành toàn bộ quy trình đóng gói macOS.");
                if (isBatchmode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[BuildMayTinh] Build macOS thất bại! Lỗi: {summary.result}");
                if (isBatchmode) EditorApplication.Exit(1);
            }
        }

        private static void BuildWebGLThucThi(bool isBatchmode)
        {
            Debug.Log("[BuildMayTinh] Bắt đầu build WebGL cho app CHEMISTRY...");

            // 1. Đồng bộ dữ liệu
            DongBoDuLieu.DongBo();

            // 2. Đảm bảo scene chính tồn tại
            DamBaoSceneTonTai();

            // 3. Vô hiệu hoá Jint / Esprima trên WebGL
            VoHieuHoaJintChoWebGL();

            // 4. Thiết lập cấu hình Player
            PlayerSettings.productName = "lab";
            PlayerSettings.companyName = "Antigravity";
            PlayerSettings.bundleVersion = "1.0.0";

            var urpAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/URP_Asset.asset");
            if (urpAsset != null)
            {
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = urpAsset;
                QualitySettings.renderPipeline = urpAsset;
            }

            // WebGL specific settings
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.memorySize = 256;

            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSpeed);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);

            // 5. Đường dẫn xuất
            string thuMucBuild = Path.Combine(Application.dataPath, "..", "Builds", "WebGL", "lab");
            if (Directory.Exists(thuMucBuild))
            {
                try { Directory.Delete(thuMucBuild, true); } catch { }
            }
            Directory.CreateDirectory(thuMucBuild);

            BuildPlayerOptions opt = new BuildPlayerOptions
            {
                scenes = new string[] { DUONG_DAN_SCENE },
                locationPathName = thuMucBuild,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(opt);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                int mb = (int)(summary.totalSize / (1024 * 1024));
                Debug.Log($"[BuildMayTinh] Build WebGL thành công! Kích thước: {mb} MB");

                // Chuẩn hoá vị trí các tệp cố định
                ChuanHoaTepWebGL(thuMucBuild);

                // Sao chép sang outputs/app-lab/webgl/
                SaoChepWebGLSangOutputs(thuMucBuild);

                Debug.Log("[BuildMayTinh] Hoàn thành toàn bộ quy trình đóng gói WebGL.");
                if (isBatchmode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[BuildMayTinh] Build WebGL thất bại! Lỗi: {summary.result}");
                if (isBatchmode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("Phòng Lab/Build App Windows 64-bit", false, 101)]
        public static void Windows()
        {
            Debug.Log("[BuildMayTinh] Kiểm tra module Windows Standalone...");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                Debug.LogWarning("[BuildMayTinh] Unity hiện tại chưa cài đặt module Windows Standalone Build Support. Bỏ qua build Windows.");
                return;
            }

            DamBaoSceneTonTai();
            string thuMucBuild = Path.Combine(Application.dataPath, "..", "Builds", "Windows");
            Directory.CreateDirectory(thuMucBuild);
            string duongDanExe = Path.Combine(thuMucBuild, $"{TEN_UNG_DUNG}.exe");

            BuildPlayerOptions opt = new BuildPlayerOptions
            {
                scenes = new string[] { DUONG_DAN_SCENE },
                locationPathName = duongDanExe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(opt);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildMayTinh] Build Windows thành công: {duongDanExe}");
            }
            else
            {
                Debug.LogError($"[BuildMayTinh] Build Windows thất bại: {report.summary.result}");
            }
        }

        private static void DamBaoSceneTonTai()
        {
            string thuMucScene = Path.Combine(Application.dataPath, "Scenes");
            if (!Directory.Exists(thuMucScene))
            {
                Directory.CreateDirectory(thuMucScene);
            }

            string duongDanVatLy = Path.Combine(Application.dataPath, "Scenes", "PhongLab.unity");
            if (!File.Exists(duongDanVatLy))
            {
                var sceneMoi = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(sceneMoi, DUONG_DAN_SCENE);
                AssetDatabase.Refresh();
                Debug.Log($"[BuildMayTinh] Đã tạo Scene mặc định tại: {DUONG_DAN_SCENE}");
            }

            EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(DUONG_DAN_SCENE, true)
            };
        }

        private static void DangKyDeepLinkMac(string appPath)
        {
            string plistPath = Path.Combine(appPath, "Contents", "Info.plist");
            if (!File.Exists(plistPath))
            {
                Debug.LogWarning($"[BuildMayTinh] Không tìm thấy Info.plist tại: {plistPath}");
                return;
            }

            try
            {
                // Dùng plutil chèn CFBundleURLTypes và CFBundleDisplayName
                string xmlUrlTypes = "<array><dict><key>CFBundleURLName</key><string>" + APP_ID + "</string><key>CFBundleURLSchemes</key><array><string>" + SCHEME_DEEP_LINK + "</string></array></dict></array>";
                ChayLenh("plutil", $"-insert CFBundleURLTypes -xml \"{xmlUrlTypes}\" \"{plistPath}\"");
                ChayLenh("plutil", $"-insert CFBundleDisplayName -string \"Phòng Lab Hoá 3D\" \"{plistPath}\"");
                Debug.Log($"[BuildMayTinh] Đã đăng ký scheme '{SCHEME_DEEP_LINK}' và CFBundleDisplayName vào Info.plist.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildMayTinh] Lỗi đăng ký Deep Link trong Info.plist: {ex.Message}");
            }
        }

        private static void ChayLenh(string cmd, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = cmd,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    proc?.WaitForExit(10000);
                }
            }
            catch { }
        }


        private static void KyAdHocCodesign(string appPath)
        {
            try
            {
                Debug.Log("[BuildMayTinh] Đang ký ad-hoc codesign...");
                var psi = new ProcessStartInfo
                {
                    FileName = "codesign",
                    Arguments = $"--force --deep -s - \"{appPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit(10000);
                    Debug.Log($"[BuildMayTinh] Ký codesign xong với mã thoát: {proc.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BuildMayTinh] Không thể ký codesign: {ex.Message}");
            }
        }

        private static void TaoGoiZipMac(string thuMucBuild, string appPath)
        {
            try
            {
                string zipPath = Path.Combine(thuMucBuild, $"{TEN_UNG_DUNG}-mac.zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);

                Debug.Log($"[BuildMayTinh] Đang nén zip ra: {zipPath}");
                var psi = new ProcessStartInfo
                {
                    FileName = "zip",
                    Arguments = $"-r -q \"{zipPath}\" \"{Path.GetFileName(appPath)}\"",
                    WorkingDirectory = thuMucBuild,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit(30000);
                Debug.Log($"[BuildMayTinh] Nén zip hoàn thành! Mã: {proc.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BuildMayTinh] Lỗi tạo zip: {ex.Message}");
        }
    }

    private static void VoHieuHoaJintChoWebGL()
    {
        string[] plugins = { "Assets/Plugins/Jint/Jint.dll", "Assets/Plugins/Jint/Esprima.dll" };
        foreach (var p in plugins)
        {
            var importer = AssetImporter.GetAtPath(p) as PluginImporter;
            if (importer != null)
            {
                importer.SetCompatibleWithPlatform(BuildTarget.WebGL, false);
                importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, true);
                importer.SetCompatibleWithEditor(true);
                importer.SaveAndReimport();
                Debug.Log($"[BuildMayTinh] Đã cấu hình plugin {p}: WebGL=false, StandaloneOSX=true");
            }
        }
    }

    private static void ChuanHoaTepWebGL(string thuMucBuild)
    {
        // Unity WebGL thường đặt các tệp trong thư mục con Build/
        string thuMucCon = Path.Combine(thuMucBuild, "Build");
        if (Directory.Exists(thuMucCon))
        {
            foreach (var file in Directory.GetFiles(thuMucCon))
            {
                string ten = Path.GetFileName(file);
                string dich = Path.Combine(thuMucBuild, ten);
                File.Copy(file, dich, true);
            }
        }

        // Đổi tên nếu Unity sinh ra tên khác (ví dụ lab.loader.js)
        string[] cacKieu = { "loader.js", "data", "framework.js", "wasm" };
        foreach (var kieu in cacKieu)
        {
            string dichChuan = Path.Combine(thuMucBuild, $"lab.{kieu}");
            if (!File.Exists(dichChuan))
            {
                var tim = Directory.GetFiles(thuMucBuild, $"*.{kieu}");
                if (tim.Length > 0 && File.Exists(tim[0]))
                {
                    File.Copy(tim[0], dichChuan, true);
                }
            }
        }
    }

    private static string TimThuMucRepoRoot()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Documents/Antigravity"));
        if (Directory.Exists(root)) return root;

        root = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents/Antigravity"));
        if (Directory.Exists(root)) return root;

        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }

    private static void SaoChepWebGLSangOutputs(string thuMucBuild)
    {
        try
        {
            string repoRoot = TimThuMucRepoRoot();
            string outputDir = Path.Combine(repoRoot, "outputs", "app-lab", "webgl");
            Directory.CreateDirectory(outputDir);

            SaoChepThuMucDeQuy(thuMucBuild, outputDir);
            Debug.Log($"[BuildMayTinh] Đã sao chép WebGL build sang: {outputDir}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BuildMayTinh] Lỗi sao chép WebGL sang outputs: {ex.Message}");
        }
    }

    private static void SaoChepMacSangOutputs(string appPath)
    {
        try
        {
            string repoRoot = TimThuMucRepoRoot();
            string outputDir = Path.Combine(repoRoot, "outputs", "app-lab");
            Directory.CreateDirectory(outputDir);
            string dichApp = Path.Combine(outputDir, Path.GetFileName(appPath));

            ChayLenh("rm", $"-rf \"{dichApp}\"");
            ChayLenh("cp", $"-R \"{appPath}\" \"{dichApp}\"");
            Debug.Log($"[BuildMayTinh] Đã sao chép {Path.GetFileName(appPath)} sang {outputDir}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BuildMayTinh] Lỗi sao chép macOS sang outputs: {ex.Message}");
        }
    }

    private static void SaoChepThuMucDeQuy(string nguon, string dich)
    {
        Directory.CreateDirectory(dich);
        foreach (var f in Directory.GetFiles(nguon))
        {
            File.Copy(f, Path.Combine(dich, Path.GetFileName(f)), true);
        }
        foreach (var d in Directory.GetDirectories(nguon))
        {
            SaoChepThuMucDeQuy(d, Path.Combine(dich, Path.GetFileName(d)));
        }
    }
}
}

