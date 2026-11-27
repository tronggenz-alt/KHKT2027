using System.Reflection;
using UnityEditor;
using UnityEngine;

// Chỉ dùng tạm ở bản gộp thử (không commit): chuẩn bị người 3D rồi dựng app Mac bằng MacBuild, ghi ra KHKT_MAC_OUT
public static class GopBuildTam
{
    public static void Run()
    {
        const string fbx = "Assets/Lab/NguoiChoi/Resources/NguoiChoi/NhanVat.fbx";
        Khkt.EditorTools.NguoiChoiImporter.AfterCharacterImport(fbx);   // tách ảnh da, áo rồi nhập lại
        Khkt.EditorTools.NguoiChoiImporter.AfterCharacterImport(fbx);   // tạo áo blouse trắng
        Debug.Log("[GOP] áo blouse: " + (AssetDatabase.LoadAssetAtPath<Material>("Assets/Lab/NguoiChoi/Resources/NguoiChoi/AoBlouse.mat") != null));
        string outPath = System.Environment.GetEnvironmentVariable("KHKT_MAC_OUT");
        var run = typeof(Khkt.EditorTools.MacBuild).GetMethod("RunBuild", BindingFlags.NonPublic | BindingFlags.Static);
        run.Invoke(null, new object[] { BuildTarget.StandaloneOSX, outPath, BuildOptions.None });
    }
}
