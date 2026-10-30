// Mô phỏng quang hợp cành rong đuôi chó Elodea và cường độ sáng (C# thuần, không phụ thuộc UnityEngine).
using System;

namespace Khkt.Simulation.Bio
{
    public static class Photosynthesis
    {
        public const double DefaultCompensationPointWPerM2 = 2.0; // Điểm bù ánh sáng
        public const double DefaultVmaxBpm = 45.0; // Tốc độ cực đại 45 bọt/phút
        public const double DefaultKmWPerM2 = 25.0; // Hằng số nửa bão hòa

        /// <summary>Cường độ sáng (W/m2) theo khoảng cách d (m) từ nguồn công suất P (Watts) theo luật bình phương nghịch</summary>
        public static double LightIntensityWattsPerM2(double lampPowerWatts, double distanceMeters)
        {
            if (distanceMeters <= 0.02) distanceMeters = 0.02; // Tránh chia 0
            // I = P / (4 * pi * d^2)
            return lampPowerWatts / (4.0 * Math.PI * distanceMeters * distanceMeters);
        }

        /// <summary>Tốc độ bọt khí O2 thải ra từ vết cắt cành rong (bọt/phút) theo mô hình Michaelis-Menten trừ hô hấp</summary>
        public static double BubbleRatePerMinute(double lightIntensityWPerM2, double vmax = DefaultVmaxBpm, double km = DefaultKmWPerM2, double compensationPoint = DefaultCompensationPointWPerM2)
        {
            if (lightIntensityWPerM2 <= compensationPoint)
                return 0.0; // Dưới điểm bù ánh sáng: không có bọt O2 tích tụ

            double effectiveIntensity = lightIntensityWPerM2 - compensationPoint;
            // Michaelis-Menten
            return (vmax * effectiveIntensity) / (km + effectiveIntensity);
        }

        /// <summary>Thể tích O2 tích tụ trong ống nghiệm (mL) sau t phút</summary>
        public static double AccumulatedOxygenVolumeMl(double bubbleRatePerMinute, double minutes, double singleBubbleVolumeMl = 0.015)
        {
            if (minutes <= 0) return 0;
            return bubbleRatePerMinute * minutes * singleBubbleVolumeMl;
        }
    }
}
