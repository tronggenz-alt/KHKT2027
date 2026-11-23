using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Giao diện ngoài lab (C4, 18/11):
///   KhktUiVerify.Run        – chụp ảnh "sau" và kiểm tra (Path.GetTempPath()/vera_ui_shots, log [UI_PASS]/[UI_FAIL])
///   KhktUiVerify.ChupTruoc  – chỉ chụp ảnh "trước" (đặt KHKT_UI_CHE_DO=truoc trong môi trường)
///   KhktUiVerify.ChupLab    – chụp ảnh dựng thật phòng thí nghiệm bằng camera trong cảnh (2880×1620) để làm nền màn chính
///                             và ảnh thu nhỏ; ghi vào KHKT_RA (mặc định Path.GetTempPath()/vera_ui_lab)
/// Chạy: ~/AppBuild/VERA_claude/dong_bo_va_chay.sh &lt;worktree&gt; KhktUiVerify.Run ui_sau
/// </summary>
public static class KhktUiVerify
{
    public static void Run()
    {
        DatTenVaBieuTuong();
        Bat();
    }

    /// <summary>Đặt tên và biểu tượng, lưu ProjectSettings rồi thoát (để chép ProjectSettings.asset về kho).</summary>
    public static void LuuTenVaBieuTuong()
    {
        DatTenVaBieuTuong();
        EditorApplication.Exit(0);
    }

    /// <summary>Tên app/cửa sổ "VERA Lab" và biểu tượng app (Mac, Windows: nhóm Standalone) từ ảnh 1024 vẽ lại.</summary>
    public static void DatTenVaBieuTuong()
    {
        PlayerSettings.productName = "VERA Lab";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Lab/Logo/IconVeraLab1024.png");
        if (tex == null) { Debug.LogError("[UI_FAIL] Thiếu Assets/Lab/Logo/IconVeraLab1024.png"); return; }
        var co = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Application);
        var ds = new Texture2D[co.Length];
        for (int i = 0; i < ds.Length; i++) ds[i] = tex;
        PlayerSettings.SetIcons(NamedBuildTarget.Standalone, ds, IconKind.Application);
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { tex }, IconKind.Any);
        AssetDatabase.SaveAssets();
        Debug.Log($"[UI_ICON] productName={PlayerSettings.productName}, {ds.Length} cỡ biểu tượng Standalone");
    }

    public static void ChupTruoc()
    {
        System.Environment.SetEnvironmentVariable("KHKT_UI_CHE_DO", "truoc");
        Bat();
    }

    static void Bat()
    {
        EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/ManHinhChinh.unity");
        if (!scene.IsValid())
        {
            Debug.LogError("[UI_FATAL] Không mở được Assets/Scenes/ManHinhChinh.unity");
            EditorApplication.Exit(1);
            return;
        }
        new GameObject("KhktUiRunner").AddComponent<Khkt.Menu.KhktUiRunner>();
        EditorApplication.EnterPlaymode();
    }

    public static void ChupLab()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/PhongThiNghiem.unity");
        if (!scene.IsValid()) { Debug.LogError("[UI_FATAL] Không mở được cảnh lab"); EditorApplication.Exit(1); return; }
        foreach (var s in Object.FindObjectsByType<Khkt.Input.SerialControllerSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Object.DestroyImmediate(s);
        new GameObject("KhktChupLabRunner").AddComponent<Khkt.Menu.KhktChupLabRunner>();
        EditorApplication.EnterPlaymode();
    }
}
