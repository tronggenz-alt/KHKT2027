using System;
using System.Globalization;
using Khkt.Simulation.Chem;
using UnityEngine;

namespace Khkt.Simulation
{
    /// <summary>
    /// Một lượng chất: thể tích, thành phần (Mix: số mol từng loài) và số giọt phenolphtalein.
    /// pH tính từ bảo toàn điện tích (ChemEngine), nên trộn bất kỳ hai lượng nào theo thứ tự nào cũng đúng.
    /// HCl chỉ để lại Cl⁻, NaOH chỉ để lại Na⁺ trong thành phần: molHCl, molNaOH đọc từ đó cho bài chuẩn độ.
    /// </summary>
    [Serializable]
    public struct Solution
    {
        public float volumeMl;
        public float indicatorDrops;
        [NonSerialized] public Mix mix;

        public const float DropMl = 0.05f;

        public Mix Mix => mix ?? Mix.Empty;

        public static Solution Water(float ml) => new Solution { volumeMl = ml };
        public static Solution HCl(float ml, float c) => new Solution { volumeMl = ml, mix = Mix.Of(("Cl-", c * ml / 1000.0)) };
        public static Solution NaOH(float ml, float c) => new Solution { volumeMl = ml, mix = Mix.Of(("Na+", c * ml / 1000.0)) };
        public static Solution Phenolphthalein(float drops) => new Solution { volumeMl = drops * DropMl, indicatorDrops = drops };
        public static Solution Of(float ml, Mix mix, float ppDrops = 0) => new Solution { volumeMl = ml, mix = mix, indicatorDrops = ppDrops };

        /// <summary>Số mol HCl tương đương (= Cl⁻) và NaOH (= Na⁺): dùng cho bài chuẩn độ HCl - NaOH.</summary>
        public double molHCl => Mix.Get("Cl-");
        public double molNaOH => Mix.Get("Na+");

        public bool IsEmpty => volumeMl <= 1e-5f && Mix.IsEmpty;

        public Solution Scale(float f) => new Solution
        {
            volumeMl = volumeMl * f,
            indicatorDrops = indicatorDrops * f,
            mix = Mix.Scale(f),
        };

        /// <summary>Lấy ra ml (chia đều mọi thành phần, kể cả kết tủa lơ lửng).</summary>
        public Solution Portion(float ml) => volumeMl <= 0 ? default : Scale(Mathf.Clamp01(ml / volumeMl));

        public static Solution operator +(Solution a, Solution b) => new Solution
        {
            volumeMl = a.volumeMl + b.volumeMl,
            indicatorDrops = a.indicatorDrops + b.indicatorDrops,
            mix = a.Mix + b.Mix,
        };

        public static Solution operator -(Solution a, Solution b) => new Solution
        {
            volumeMl = Mathf.Max(0, a.volumeMl - b.volumeMl),
            indicatorDrops = Mathf.Max(0, a.indicatorDrops - b.indicatorDrops),
            mix = a.Mix - b.Mix,
        };

        public double Ph => ChemEngine.Ph(Mix, volumeMl / 1000.0);

        /// <summary>Độ hồng 0..1 nhìn thấy được (cần có phenolphtalein; ít chỉ thị thì nhạt hơn).</summary>
        public float Pink => indicatorDrops < 0.3f ? 0f
            : TitrationModel.PhenolphthaleinFraction(Ph) * Mathf.Clamp01(indicatorDrops / 2f);

        /// <summary>Mô tả ngắn: "25,0 ml · Cu²⁺ 0,10 M, SO₄²⁻ 0,10 M".</summary>
        public string Describe(int max = 3)
        {
            if (IsEmpty) return "Trống";
            var vi = CultureInfo.GetCultureInfo("vi-VN");
            string v = volumeMl.ToString(volumeMl < 10 ? "0.00" : "0.0", vi) + " ml";
            var parts = ChemColor.Components(Mix, volumeMl, max);
            if (indicatorDrops >= 0.3f) parts.Add("phenolphtalein");
            if (parts.Count == 0) return volumeMl > 0 ? $"{v} nước cất" : "Trống";
            return volumeMl > 0.01f ? $"{v} · {string.Join(", ", parts)}" : string.Join(", ", parts);
        }
    }
}
