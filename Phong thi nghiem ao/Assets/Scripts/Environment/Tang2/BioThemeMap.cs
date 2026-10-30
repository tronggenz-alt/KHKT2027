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

    [CreateAssetMenu(fileName = "ChuDe", menuName = "Khkt/BioThemeMap", order = 100)]
    public class BioThemeMap : ScriptableObject
    {
        public List<ThemeMaterialPair> pairs = new List<ThemeMaterialPair>();

        public Material GetMaterial(Material current, LabThemeMode targetTheme)
        {
            if (current == null) return null;
            foreach (var p in pairs)
            {
                if (targetTheme == LabThemeMode.ToiGian && (p.quocTe == current || current.name.StartsWith(p.quocTe.name)))
                    return p.toiGian;
                if (targetTheme == LabThemeMode.QuocTe && (p.toiGian == current || current.name.StartsWith(p.toiGian.name)))
                    return p.quocTe;
            }
            return current;
        }
    }
}
