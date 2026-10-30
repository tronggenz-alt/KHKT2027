using System.Collections.Generic;
using System.IO;
using Khkt.Environment.Tang2;
using UnityEditor;
using UnityEngine;

namespace Khkt.EditorTools
{
    public static partial class LabSceneBuilder
    {
        public static Material t2WallQuocTe, t2WallToiGian;
        public static Material t2FloorQuocTe, t2FloorToiGian;
        public static Material t2BenchQuocTe, t2BenchToiGian;
        public static Material t2OakWood;
        public static Material t2SageAccent;
        public static Material t2Graphite;
        public static Material t2Aluminum;

        public static void EnsureFloor2ThemeMaterials()
        {
            const string dir = "Assets/Lab/Tang2/VatLieu";
            if (!AssetDatabase.IsValidFolder("Assets/Lab/Tang2"))
                AssetDatabase.CreateFolder("Assets/Lab", "Tang2");
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/Lab/Tang2", "VatLieu");

            // --- BỘ QUỐC TẾ (Đồng bộ Tầng 1) ---
            t2WallQuocTe = LabKit.Mat("T2_QuocTe_Tuong", new Color(0.92f, 0.93f, 0.94f), smoothness: 0.2f);
            t2FloorQuocTe = LabKit.Mat("T2_QuocTe_San", new Color(0.85f, 0.86f, 0.88f), smoothness: 0.45f);
            t2BenchQuocTe = LabKit.Mat("T2_QuocTe_MatBan", new Color(0.18f, 0.20f, 0.22f), smoothness: 0.4f);

            // --- BỘ TỐI GIẢN (Japandi / White Oak / Sage) ---
            // Tường trắng ngà #F4F2EE
            Color colWallTG; ColorUtility.TryParseHtmlString("#F4F2EE", out colWallTG);
            t2WallToiGian = LabKit.Mat("T2_ToiGian_Tuong", colWallTG, smoothness: 0.15f);

            // Sàn đá liền mảnh không ron #D9D6D0
            Color colFloorTG; ColorUtility.TryParseHtmlString("#D9D6D0", out colFloorTG);
            t2FloorToiGian = LabKit.Mat("T2_ToiGian_San", colFloorTG, smoothness: 0.35f);

            // Mặt bàn trắng mờ #E9E8E4
            Color colBenchTG; ColorUtility.TryParseHtmlString("#E9E8E4", out colBenchTG);
            t2BenchToiGian = LabKit.Mat("T2_ToiGian_MatBan", colBenchTG, smoothness: 0.4f);

            // Gỗ sồi trắng #CDB79A
            Color colOak; ColorUtility.TryParseHtmlString("#CDB79A", out colOak);
            t2OakWood = LabKit.Mat("T2_ToiGian_GoSoi", colOak, smoothness: 0.3f);

            // Màu nhấn: Xanh lá xô thơm (sage) #7A9A82
            Color colSage; ColorUtility.TryParseHtmlString("#7A9A82", out colSage);
            t2SageAccent = LabKit.Mat("T2_ToiGian_Sage", colSage, smoothness: 0.25f);

            // Than chì #2B2D2F
            Color colGraphite; ColorUtility.TryParseHtmlString("#2B2D2F", out colGraphite);
            t2Graphite = LabKit.Mat("T2_ToiGian_ThanChi", colGraphite, metallic: 0.2f, smoothness: 0.5f);

            // Nhôm anod xám nhạt
            t2Aluminum = LabKit.Mat("T2_ToiGian_Nhom", new Color(0.82f, 0.84f, 0.85f), metallic: 0.85f, smoothness: 0.7f);

            // Tạo hoặc cập nhật ScriptableObject ChuDe.asset
            const string assetPath = "Assets/Lab/Tang2/ChuDe.asset";
            var themeMap = AssetDatabase.LoadAssetAtPath<BioThemeMap>(assetPath);
            if (themeMap == null)
            {
                themeMap = ScriptableObject.CreateInstance<BioThemeMap>();
                AssetDatabase.CreateAsset(themeMap, assetPath);
            }

            themeMap.pairs = new List<ThemeMaterialPair>
            {
                new ThemeMaterialPair { id = "Tuong", quocTe = t2WallQuocTe, toiGian = t2WallToiGian },
                new ThemeMaterialPair { id = "San", quocTe = t2FloorQuocTe, toiGian = t2FloorToiGian },
                new ThemeMaterialPair { id = "MatBan", quocTe = t2BenchQuocTe, toiGian = t2BenchToiGian },
                new ThemeMaterialPair { id = "Ghe", quocTe = LabKit.Mat("GHS0", new Color(0.2f, 0.6f, 0.85f)), toiGian = t2OakWood },
                new ThemeMaterialPair { id = "Vien", quocTe = LabKit.Mat("VangCanhBao", new Color(0.95f, 0.75f, 0.1f)), toiGian = t2SageAccent }
            };

            EditorUtility.SetDirty(themeMap);
            AssetDatabase.SaveAssets();
        }

        public static void SetupFloor2ThemeSwitcher(GameObject floor2Root)
        {
            var switcher = floor2Root.GetComponent<BioThemeSwitcher>();
            if (switcher == null) switcher = floor2Root.AddComponent<BioThemeSwitcher>();

            switcher.themeMap = AssetDatabase.LoadAssetAtPath<BioThemeMap>("Assets/Lab/Tang2/ChuDe.asset");
            switcher.currentTheme = LabThemeMode.QuocTe;
            switcher.transitionDuration = 0.6f;
        }
    }
}
