#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Khkt.EditorTools
{
    /// Unity đôi khi bỏ sót Assets/Plugins/iOS/BioWebViewPlugin.mm khi xuất Xcode,
    /// làm lỗi liên kết _OpenBioWebView/_CloseBioWebView. Script này gắn tệp vào UnityFramework nếu thiếu.
    public static class BioWebViewPostBuild
    {
        const string Src = "Assets/Plugins/iOS/BioWebViewPlugin.mm";
        const string Rel = "Libraries/Plugins/iOS/BioWebViewPlugin.mm";

        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS || !File.Exists(Src)) return;
            Debug.Log($"[BIOWEB] importer = {AssetImporter.GetAtPath(Src)?.GetType().Name ?? "null"}");

            string projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            string fw = proj.GetUnityFrameworkTargetGuid();
            if (proj.FindFileGuidByProjectPath(Rel) != null)
            {
                Debug.Log("[BIOWEB] da co trong du an Xcode");
                return;
            }
            string dst = Path.Combine(path, Rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(Src, dst, true);
            string guid = proj.AddFile(Rel, Rel, PBXSourceTree.Source);
            proj.AddFileToBuild(fw, guid);
            proj.AddFrameworkToProject(fw, "WebKit.framework", false);
            proj.WriteToFile(projPath);
            Debug.Log("[BIOWEB] da gan BioWebViewPlugin.mm vao UnityFramework");
        }
    }
}
#endif
