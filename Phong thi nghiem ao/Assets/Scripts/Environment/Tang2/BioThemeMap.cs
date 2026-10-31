using System;
using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Environment.Tang2
{
    [Serializable]
    public struct ThemeMaterialPair
    {
        public string id;
        public Material quocTe;
        public Material toiGian;
    }

    /// <summary>Bảng cặp vật liệu Quốc tế ↔ Tối giản của tầng 2 (so khớp đúng vật liệu, không so tên).</summary>
    [CreateAssetMenu(fileName = "ChuDe", menuName = "Khkt/BioThemeMap", order = 100)]
    public class BioThemeMap : ScriptableObject
    {
        public List<ThemeMaterialPair> pairs = new List<ThemeMaterialPair>();

        Dictionary<Material, Material> toMinimal, toIntl;

        void Build()
        {
            toMinimal = new Dictionary<Material, Material>();
            toIntl = new Dictionary<Material, Material>();
            foreach (var p in pairs)
            {
                if (p.quocTe == null || p.toiGian == null) continue;
                toMinimal[p.quocTe] = p.toiGian;
                if (!toIntl.ContainsKey(p.toiGian)) toIntl[p.toiGian] = p.quocTe;
            }
        }

        /// <summary>Vật liệu tương ứng ở chủ đề đích, hoặc null nếu vật liệu này không đổi theo chủ đề.</summary>
        public Material GetMaterial(Material current, LabThemeMode target)
        {
            if (current == null) return null;
            if (toMinimal == null) Build();
            var d = target == LabThemeMode.ToiGian ? toMinimal : toIntl;
            return d.TryGetValue(current, out var m) ? m : null;
        }
    }
}
