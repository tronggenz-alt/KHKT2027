// Build bản Unity WebGL của Phòng thí nghiệm Hoá ảo – chạy được bằng dòng lệnh, KHÔNG cần chỉnh cảnh bằng tay:
//   "<Unity>/Contents/MacOS/Unity" -batchmode -quit -projectPath unity/PhongThiNghiemHoaHoc -executeMethod BuildWebGL.Build -logFile logs/unity-build.log
// hoặc trong Unity: menu "Phòng thí nghiệm → Build WebGL".
// Việc làm: (1) tạo URP Asset nếu chưa có, (2) tạo vật liệu mẫu Resources/PTN (để shader vào bản build), (3) tạo cảnh chỉ có GameObject
// LabBridge, (4) đặt Player Settings (Gzip + Decompression Fallback, Strip Engine Code, tối ưu dung lượng), (5) build ra
// ../../frontend/unity-lab/ (tức frontend/unity-lab/Build/ của web). Mục tiêu dung lượng ≤ 25 MB.
using System;
using System.IO;
using System.Linq;
using PhongThiNghiem;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BuildWebGL
{
    const string CANH = "Assets/Scenes/PhongThiNghiem.unity";
    const long MUC_TIEU_BYTE = 25L * 1024 * 1024;

    [MenuItem("Phòng thí nghiệm/Build WebGL")]
    public static void Build()
    {
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Settings");
        Directory.CreateDirectory("Assets/Resources/PTN");
        ChuanBiURP();
        ChuanBiVatLieu();
        ChuanBiCanh();
        CaiDatPlayer();

        string dich = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "frontend", "unity-lab"));
        var tuyChon = new BuildPlayerOptions
        {
            scenes = new[] { CANH },
            locationPathName = dich,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };
        BuildReport bc = BuildPipeline.BuildPlayer(tuyChon);
        string thuMucBuild = Path.Combine(dich, "Build");
        long byteBuild = Directory.Exists(thuMucBuild) ? new DirectoryInfo(thuMucBuild).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;
        Debug.Log($"[BuildWebGL] {bc.summary.result} – {byteBuild / 1048576.0:F1} MB → {thuMucBuild}");
        if (byteBuild > MUC_TIEU_BYTE) Debug.LogWarning("[BuildWebGL] Bản build vượt mục tiêu 25 MB – xem phần 'Giảm dung lượng' trong README.md.");
        if (Application.isBatchMode) EditorApplication.Exit(bc.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    static void ChuanBiURP()
    {
        if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset) return;
        try
        {
            var duLieu = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(duLieu, "Assets/Settings/URP_Renderer.asset");
            var urp = UniversalRenderPipelineAsset.Create(duLieu);
            urp.shadowDistance = 6f;
            AssetDatabase.CreateAsset(urp, "Assets/Settings/URP_Asset.asset");
            GraphicsSettings.defaultRenderPipeline = urp;
            int muc = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(muc, false);
            AssetDatabase.SaveAssets();
        }
        catch (Exception e)
        {
            Debug.LogError("[BuildWebGL] Không tự tạo được URP Asset (" + e.Message + "). Tạo tay: Assets → Create → Rendering → URP Asset (with Universal Renderer), "
                           + "rồi gán vào Project Settings → Graphics và Quality.");
            throw;
        }
    }

    static void TaoVatLieu(string ten, string tenShader, Action<Material> caiDat)
    {
        string duong = $"Assets/Resources/PTN/{ten}.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(duong) != null) return;
        var sh = Shader.Find(tenShader);
        if (sh == null) { Debug.LogError($"[BuildWebGL] Không thấy shader {tenShader} – kiểm tra gói URP."); return; }
        var m = new Material(sh);
        caiDat?.Invoke(m);
        AssetDatabase.CreateAsset(m, duong);
    }

    static void ChuanBiVatLieu()
    {
        TaoVatLieu("ThuyTinh", "Universal Render Pipeline/Lit", m => LabVatLieu.DatTrongSuot(m, new Color(0.88f, 0.94f, 1f, 0.2f), 0.96f));
        TaoVatLieu("Long", "Universal Render Pipeline/Lit", m => LabVatLieu.DatTrongSuot(m, new Color(0.84f, 0.92f, 0.97f, 0.42f), 0.85f));
        TaoVatLieu("Dac", "Universal Render Pipeline/Lit", m => m.SetFloat("_Smoothness", 0.35f));
        TaoVatLieu("KimLoai", "Universal Render Pipeline/Lit", m => { m.SetFloat("_Metallic", 0.9f); m.SetFloat("_Smoothness", 0.7f); });
        TaoVatLieu("PhatSang", "Universal Render Pipeline/Particles/Unlit", m =>
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);   // Additive
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        });
        AssetDatabase.SaveAssets();
    }

    static void ChuanBiCanh()
    {
        var canh = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("LabBridge").AddComponent<LabBridge>();   // cảnh chỉ có MỘT GameObject – mọi thứ khác dựng bằng code lúc chạy
        EditorSceneManager.SaveScene(canh, CANH);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(CANH, true) };
    }

    static void CaiDatPlayer()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        PlayerSettings.companyName = "CHEMISTRY";
        PlayerSettings.productName = "PhongThiNghiemHoaHoc";
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;   // máy chủ không cần header Content-Encoding đặc biệt
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.template = "APPLICATION:Minimal";
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
        PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });   // WebGL 2
        DatToiUuDungLuong();
        DatNhapLieuCu();
    }

    /// <summary>Active Input Handling = Both: LabCamera / LabBridge dùng UnityEngine.Input (bộ nhập liệu cũ). Không có API công khai → SerializedObject.</summary>
    static void DatNhapLieuCu()
    {
        try
        {
            var ps = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            var so = new SerializedObject(ps);
            var pr = so.FindProperty("activeInputHandler");
            if (pr != null && pr.intValue == 1) { pr.intValue = 2; so.ApplyModifiedProperties(); }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[BuildWebGL] Không đặt được Active Input Handling: " + e.Message);
        }
    }

    /// <summary>Code Optimization = Disk Size (tên API khác nhau giữa các bản Unity → đặt qua reflection, lỗi thì chỉ cảnh báo).</summary>
    static void DatToiUuDungLuong()
    {
        try
        {
            Type kieu = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.WebGL.UserBuildSettings", false))
                .FirstOrDefault(t => t != null);
            var pr = kieu?.GetProperty("codeOptimization");
            if (pr == null) { Debug.LogWarning("[BuildWebGL] Không tìm thấy Code Optimization – đặt tay ở Build Profiles (Disk Size)."); return; }
            string[] ten = Enum.GetNames(pr.PropertyType);
            string chon = ten.FirstOrDefault(n => n == "DiskSize") ?? ten.FirstOrDefault(n => n.Contains("Size"));
            if (chon != null) pr.SetValue(null, Enum.Parse(pr.PropertyType, chon));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[BuildWebGL] Không đặt được Code Optimization: " + e.Message);
        }
    }
}
