using System.IO;
using System.Linq;
using Khkt.Network;
using UnityEditor;
using UnityEngine;

namespace Khkt.EditorTools
{
    /// <summary>
    /// File nhân vật / chuyển động tải từ Mixamo thả vào Assets/Lab/NguoiChoi/:
    ///  • lần nhập đầu tiên tự đặt kiểu xương Humanoid (để mọi chuyển động Mixamo gắn được vào mọi nhân vật) và cho
    ///    chuyển động lặp, đứng tại chỗ (code tự dời người theo vị trí thật). Sửa tay trong Inspector sau đó thì được giữ.
    ///  • NhanVat.fbx dạng "FBX for Unity" nhúng ảnh da, áo bên trong: tự tách ra Assets/Lab/NguoiChoi/Textures để vật
    ///    liệu nhận ảnh, rồi tạo áo blouse trắng (Resources/NguoiChoi/AoBlouse.mat) từ ảnh áo khoác của nhân vật:
    ///    tô trắng nhưng giữ nếp vải. Thân người 3D tự mặc áo này cho lưới áo khoác (xem RemoteAvatar.IsOuterwear).
    /// </summary>
    public class NguoiChoiImporter : AssetPostprocessor
    {
        const string Folder = "Assets/Lab/NguoiChoi/";
        const string TextureFolder = "Assets/Lab/NguoiChoi/Textures";
        const string BlouseTexture = "Assets/Lab/NguoiChoi/Textures/AoBlouse.png";
        const string BlouseMaterial = "Assets/Lab/NguoiChoi/Resources/" + RemoteAvatar.BlousePath + ".mat";

        bool Mine => IsMine(assetPath);

        static bool IsMine(string path) => path.Replace('\\', '/').StartsWith(Folder)
                                           && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (!Mine || !assetImporter.importSettingsMissing) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.importCameras = false;
            mi.importLights = false;
        }

        void OnPreprocessAnimation()
        {
            if (!Mine) return;
            var mi = (ModelImporter)assetImporter;
            if (mi.clipAnimations.Length > 0) return;  // đã chỉnh clip rồi
            var clips = mi.defaultClipAnimations;
            if (clips.Length == 0) return;
            foreach (var c in clips)
            {
                c.loopTime = true;
                // nướng chuyển động gốc vào tư thế: người không tự trôi, không tự xoay, không lún theo clip
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;
            }
            mi.clipAnimations = clips;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (!IsMine(path) || Path.GetFileNameWithoutExtension(path) != "NhanVat") continue;
                string p = path;
                // không được tách ảnh / tạo tài nguyên ngay trong lúc nhập: làm ở khung sau
                EditorApplication.delayCall += () => AfterCharacterImport(p);
            }
        }

        /// <summary>Tách ảnh nhúng (nếu vật liệu chưa có ảnh) rồi tạo áo blouse trắng nếu chưa có. Gọi lại an toàn.</summary>
        public static void AfterCharacterImport(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) return;
            var mats = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToArray();
            if (mats.Length > 0 && mats.All(m => m.mainTexture == null))
            {
                if (!AssetDatabase.IsValidFolder(TextureFolder)) AssetDatabase.CreateFolder("Assets/Lab/NguoiChoi", "Textures");
                if (mi.ExtractTextures(TextureFolder))
                {
                    AssetDatabase.Refresh();
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);  // vật liệu tìm thấy ảnh vừa tách
                    Debug.Log("[NguoiChoi] Đã tách ảnh da, áo của nhân vật vào " + TextureFolder);
                }
                return;  // lần nhập lại sẽ gọi tiếp để tạo áo blouse
            }
            if (AssetDatabase.LoadAssetAtPath<Material>(BlouseMaterial) == null) MakeBlouse(path);
        }

        /// <summary>Áo blouse trắng: ảnh màu của lưới áo khoác đổi sang trắng hơi xanh lạnh, giữ độ sáng tối của nếp vải.</summary>
        static void MakeBlouse(string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var coat = model != null ? model.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => RemoteAvatar.IsOuterwear(r.name)) : null;
            var src = coat != null ? coat.sharedMaterial : null;
            var tex = src != null ? src.mainTexture : null;
            if (tex == null)
            {
                Debug.Log("[NguoiChoi] Nhân vật không có lưới áo khoác (Suit / Jacket / Coat…) có ảnh: giữ nguyên áo");
                return;
            }
            int size = Mathf.Min(2048, Mathf.Max(tex.width, tex.height));
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var img = new Texture2D(size, size, TextureFormat.RGB24, false);
            img.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            var px = img.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                float l = (0.299f * px[i].r + 0.587f * px[i].g + 0.114f * px[i].b) / 255f;
                float v = 0.80f + 0.19f * Mathf.Clamp01((l - 0.04f) / 0.40f);
                px[i] = new Color32((byte)(v * 0.975f * 255f), (byte)(v * 0.985f * 255f), (byte)(v * 255f), 255);
            }
            img.SetPixels32(px);
            img.Apply();
            if (!AssetDatabase.IsValidFolder(TextureFolder)) AssetDatabase.CreateFolder("Assets/Lab/NguoiChoi", "Textures");
            File.WriteAllBytes(BlouseTexture, img.EncodeToPNG());
            Object.DestroyImmediate(img);
            AssetDatabase.ImportAsset(BlouseTexture);
            if (AssetImporter.GetAtPath(BlouseTexture) is TextureImporter ti)
            {
                ti.sRGBTexture = true;
                ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
            }

            var mat = new Material(src) { name = "AoBlouse" };
            var white = AssetDatabase.LoadAssetAtPath<Texture2D>(BlouseTexture);
            foreach (var prop in new[] { "_MainTex", "_BaseMap" })
                if (mat.HasProperty(prop)) mat.SetTexture(prop, white);
            foreach (var prop in new[] { "_Color", "_BaseColor" })
                if (mat.HasProperty(prop)) mat.SetColor(prop, Color.white);
            string dir = Path.GetDirectoryName(BlouseMaterial).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Path.GetDirectoryName(dir).Replace('\\', '/'), Path.GetFileName(dir));
            AssetDatabase.CreateAsset(mat, BlouseMaterial);
            AssetDatabase.SaveAssets();
            Debug.Log($"[NguoiChoi] Đã tạo áo blouse trắng cho lưới \"{coat.name}\": {BlouseMaterial}");
        }
    }
}
