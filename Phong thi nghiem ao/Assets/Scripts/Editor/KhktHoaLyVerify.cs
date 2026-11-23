using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Kiểm thử 4 mô phỏng Hóa lý (Scripts/HoaLy, 18/11):
///   ~/AppBuild/VERA_claude/dong_bo_va_chay.sh ~/Developer/Antigravity KhktHoaLyVerify.Run hoa_ly
/// Mở cảnh trống (camera + đèn mặc định), vào PlayMode, Khkt.HoaLy.KhktHoaLyRunner chạy các kiểm tra rồi thoát (0 = đạt).
/// Ảnh: $KHKT_HOALY_SHOTS hoặc $TMPDIR/vera_hoaly_shots.
/// </summary>
public static class KhktHoaLyVerify
{
    public static void Run()
    {
        EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        new GameObject("KhktHoaLyRunner").AddComponent<Khkt.HoaLy.KhktHoaLyRunner>();
        EditorApplication.EnterPlaymode();
    }
}
