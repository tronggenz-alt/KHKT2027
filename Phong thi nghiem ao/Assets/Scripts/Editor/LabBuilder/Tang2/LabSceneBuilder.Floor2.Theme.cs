using System.Collections.Generic;
using Khkt.Environment.Tang2;
using UnityEditor;
using UnityEngine;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Hai chủ đề tầng 2. "Quốc tế" dùng đúng vật liệu tầng 1 (màu nhận diện khu, sọc màu, LED xanh ngọc).
    /// "Tối giản" (Japandi: trắng ngà, gỗ sồi trắng, một màu nhấn xanh xô thơm, đèn hắt ấm 3000 K) là bộ vật liệu thay thế:
    /// bảng cặp (Quốc tế → Tối giản) lưu ở Assets/Lab/Tang2/ChuDe.asset, BioThemeSwitcher đổi vật liệu của các vật ở tầng 2
    /// lúc chạy (phím F7), không sửa asset. Vật chỉ có ở một chủ đề (dải sọc màu) gắn ThemeOnly.
    /// </summary>
    public static partial class LabSceneBuilder
    {
        static void EnsureFloor2ThemeMaterials()
        {
            Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
            var wall = Mat("T2_ToiGian_Tuong", Hex("#F4F2EE"), 0.15f);
            var floor = Mat("T2_ToiGian_San", Hex("#D9D6D0"), 0.38f);
            var floorHallTG = Mat("T2_ToiGian_SanHanhLang", Hex("#CFCBC4"), 0.35f);
            var bench = Mat("T2_ToiGian_MatBan", Hex("#E9E8E4"), 0.4f);
            var oakTex = (Texture2D)cabinetDoor.GetTexture("_BaseMap");
            var oakTG = Mat("T2_ToiGian_GoSoi", Color.Lerp(Hex("#CDB79A"), Color.white, 0.35f), 0.3f, 0, oakTex, Vector2.one);
            var seat = Mat("T2_ToiGian_MatGhe", Hex("#CDB79A"), 0.35f, 0, oakTex, Vector2.one);
            var sage = Mat("T2_ToiGian_XoThom", Hex("#7A9A82"), 0.3f);
            var graphite = Mat("T2_ToiGian_ThanChi", Hex("#2B2D2F"), 0.45f, 0.2f);
            var warmLed = Mat("T2_ToiGian_LedAm", new Color(1f, 0.9f, 0.78f), 0.5f, 0, emission: new Color(1.1f, 0.85f, 0.6f));
            var walkway = Mat("T2_ToiGian_LoiDi", Hex("#E2DFD9"), 0.35f);
            var leaf = Mat("T2_ToiGian_CanhCua", Color.Lerp(Hex("#CDB79A"), Color.white, 0.55f), 0.4f);

            var pairs = new List<(Material from, Material to)>
            {
                (paint, wall), (floorLab, floor), (t2FloorPhanTu, floor), (t2FloorKho, floor), (floorHall, floorHallTG), (t2FloorHallEast, floorHallTG),
                (epoxy, bench), (cabinetDoor, oakTG), (ledBlue, warmLed), (accent, sage), (t2Accent, sage), (accentDark, graphite), (t2AccentDark, graphite),
                (floorGuide, walkway), (Mat("T2_VienLoiDi", zoneBioLab, 0.4f), sage),
                (leafBioLab, leaf), (leafMicroBio, leaf), (leafBioStore, leaf), (leafMolecular, leaf), (leafGreenhouse, leaf),
            };
            foreach (var m in stoolMats) pairs.Add((m, seat));

            const string assetPath = RootT2 + "/ChuDe.asset";
            var map = AssetDatabase.LoadAssetAtPath<BioThemeMap>(assetPath);
            if (map == null)
            {
                map = ScriptableObject.CreateInstance<BioThemeMap>();
                AssetDatabase.CreateAsset(map, assetPath);
            }
            map.pairs = new List<ThemeMaterialPair>();
            foreach (var (from, to) in pairs)
                if (from != null && to != null) map.pairs.Add(new ThemeMaterialPair { id = from.name, quocTe = from, toiGian = to });
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();
        }

        static void SetupFloor2ThemeSwitcher(GameObject floor2Root)
        {
            var switcher = floor2Root.GetComponent<BioThemeSwitcher>();
            if (switcher == null) switcher = floor2Root.AddComponent<BioThemeSwitcher>();
            switcher.themeMap = AssetDatabase.LoadAssetAtPath<BioThemeMap>(RootT2 + "/ChuDe.asset");
            switcher.currentTheme = LabThemeMode.QuocTe;
            switcher.transitionDuration = 0.6f;
        }
    }
}
