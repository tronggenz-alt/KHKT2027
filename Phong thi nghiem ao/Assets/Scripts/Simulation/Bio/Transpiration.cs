// Mô phỏng tốc độ thoát hơi nước qua lá bằng áp kế bọt khí (potometer) (C# thuần, không phụ thuộc UnityEngine).
using System;

namespace Khkt.Simulation.Bio
{
    public static class Transpiration
    {
        // Tốc độ dịch chuyển bọt khí cơ sở trong ống mao quản potometer (mm/phút, mo_phong = true)
        public const double BaseShiftMmPerMin = 1.8;

        /// <summary>
        /// Tốc độ dịch chuyển vạch bọt khí trong potometer (mm/phút) theo mức gió (0..1), ánh sáng (0..1), độ ẩm không khí (0..1) và trạng thái khí khổng.
        /// </summary>
        public static double BubbleShiftMmPerMin(
            double wind01,
            double light01,
            double humidity01,
            bool stomataBlocked)
        {
            double w = Clamp01(wind01);
            double l = Clamp01(light01);
            double h = Clamp01(humidity01);

            // Khi bôi vaseline bịt kín khí khổng, chỉ còn thoát hơi nước qua tầng cutin rất yếu (~8% bình thường)
            if (stomataBlocked)
            {
                return BaseShiftMmPerMin * 0.08 * (1.0 - 0.7 * h);
            }

            // Ánh sáng kích thích mở khí khổng; gió thổi lớp không khí bão hòa sát mặt lá; độ ẩm cao làm giảm chênh lệch thế nước
            double lightFactor = 0.30 + 1.50 * l;
            double windFactor = 1.00 + 1.80 * w;
            double humidityFactor = Math.Max(0.08, 1.0 - 0.85 * h);

            return BaseShiftMmPerMin * lightFactor * windFactor * humidityFactor;
        }

        private static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
    }
}

