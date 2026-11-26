using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Bản đi thi (26/11/2026): đổi toàn bộ bàn thí nghiệm sang màu đen tiêu chuẩn phòng lab (mặt epoxy đen bóng mờ,
    /// thân tủ đen than) ở mọi cảnh trong bản build. Chạy: Unity -batchmode -executeMethod Khkt.EditorTools.KhktBanDen.Run
    ///  • Mặt bàn: đối tượng tên "Mặt bàn…" hoặc "Mặt làm việc" (trừ mặt gỗ: bàn giáo viên, bàn gỗ trang trí).
    ///  • Thân bàn: tủ dưới, cánh tủ, ngăn kéo, chân bàn, tấm che chân... nằm cùng nhóm với một mặt bàn đã đổi.
    /// Chạy lại nhiều lần vẫn ra đúng (bỏ qua chỗ đã đen).
    /// </summary>
    public static class KhktBanDen
    {
        const string MatTopPath = "Assets/Lab/Materials/BanDen_MatBan.mat";
        const string MatBodyPath = "Assets/Lab/Materials/BanDen_Than.mat";

        static readonly string[] BodyPrefixes =
        {
            "Tủ dưới", "Cánh tủ", "Ngăn kéo", "Chân bàn", "Thân bàn", "Tấm che chân", "Hộc bồn", "Tủ sau", "Tủ bàn", "Mặt dựng trước", "Khung bàn",
        };

        static bool IsTop(string n) => n.StartsWith("Mặt bàn") || n == "Mặt làm việc";
        static bool IsBody(string n) => BodyPrefixes.Any(n.StartsWith);

        static bool IsWood(Material m)
        {
            if (m == null) return false;
            string n = m.name.ToLowerInvariant();
            return n.Contains("go") && (n.StartsWith("go") || n.Contains("soi") || n.Contains("gỗ"));
        }

        [MenuItem("KHKT/Đổi bàn thí nghiệm sang đen")]
        public static void Run()
        {
            var top = Ensure(MatTopPath, "BanDen_MatBan", new Color(0.035f, 0.037f, 0.041f), 0.62f);
            var body = Ensure(MatBodyPath, "BanDen_Than", new Color(0.075f, 0.078f, 0.085f), 0.32f);
            int totalTop = 0, totalBody = 0;
            foreach (var sc in EditorBuildSettings.scenes)
            {
                if (!sc.enabled || sc.path.EndsWith("ManHinhChinh.unity")) continue;
                var scene = EditorSceneManager.OpenScene(sc.path, OpenSceneMode.Single);
                int nTop = 0, nBody = 0;
                var benches = new HashSet<Transform>();
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (!IsTop(r.name)) continue;
                    var mats = r.sharedMaterials;
                    if (mats.Any(IsWood)) continue;
                    if (r.transform.parent != null) benches.Add(r.transform.parent);
                    if (mats.All(m => m == top)) continue;
                    r.sharedMaterials = Enumerable.Repeat(top, mats.Length).ToArray();
                    nTop++;
                }
                foreach (var b in benches)
                    foreach (var r in b.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (r.transform.parent != b || !IsBody(r.name)) continue;
                        var mats = r.sharedMaterials;
                        if (mats.All(m => m == body)) continue;
                        r.sharedMaterials = Enumerable.Repeat(body, mats.Length).ToArray();
                        nBody++;
                    }
                if (nTop + nBody > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                Debug.Log($"[BanDen] {sc.path}: {nTop} mặt bàn, {nBody} phần thân bàn đổi sang đen ({benches.Count} bàn)");
                totalTop += nTop;
                totalBody += nBody;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[BanDen] XONG: {totalTop} mặt bàn, {totalBody} phần thân bàn");
        }

        /// <summary>Tạo vật liệu đen từ vật liệu epoxy cũ (cùng shader URP Lit), bỏ ảnh vân màu, giữ bản đồ pháp tuyến.</summary>
        static Material Ensure(string path, string name, Color color, float smooth)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/Lab/Materials/MatBanEpoxy.mat");
                m = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", null);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
