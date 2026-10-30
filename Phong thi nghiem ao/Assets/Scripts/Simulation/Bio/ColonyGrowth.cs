// Mô phỏng sinh trưởng quần thể vi khuẩn & khuẩn lạc (C# thuần, không phụ thuộc UnityEngine).
using System;

namespace Khkt.Simulation.Bio
{
    public static class ColonyGrowth
    {
        public const double EColiK12DoublingTimeMinutes = 20.0;
        public const double DefaultCarryingCapacityK = 1e9; // 1 tỷ tế bào / mL
        public const double DefaultInitialN0 = 1e3; // 1000 tế bào / mL
        public const double DefaultLagPhaseHours = 0.5; // Pha tiềm phát 30 phút

        /// <summary>Tốc độ sinh trưởng riêng r (1/giờ) từ thời gian thế hệ (phút)</summary>
        public static double GrowthRatePerHour(double doublingTimeMinutes = EColiK12DoublingTimeMinutes)
        {
            if (doublingTimeMinutes <= 0) return 0;
            return (Math.Log(2.0) / doublingTimeMinutes) * 60.0; // ~2,079 / giờ
        }

        /// <summary>Số lượng tế bào N(t) theo mô hình logistic Verhulst có xét pha tiềm phát (lag phase)</summary>
        public static double PopulationAtTime(double hours, double n0 = DefaultInitialN0, double k = DefaultCarryingCapacityK, double lagHours = DefaultLagPhaseHours)
        {
            if (hours <= 0) return n0;
            if (hours < lagHours) return n0; // Pha lag

            double tEffective = hours - lagHours;
            double r = GrowthRatePerHour();
            double expPart = Math.Exp(-r * tEffective);
            return k / (1.0 + ((k - n0) / n0) * expPart);
        }

        /// <summary>Mật độ quang OD600 từ nồng độ tế bào N (1 OD600 ≈ 8 * 10^8 tế bào/mL đối với E. coli)</summary>
        public static double OpticalDensity600(double cellCountPerMl)
        {
            const double factor = 8.0e8;
            return cellCountPerMl / factor;
        }

        /// <summary>Tỷ lệ kích thước khuẩn lạc trên đĩa thạch (0.0 .. 1.0)</summary>
        public static double ColonyGrowthFraction(double hours, double lagHours = 1.0)
        {
            if (hours < lagHours) return 0.0;
            double t = hours - lagHours;
            // Khuẩn lạc đạt kích thước cực đại sau khoảng 18 - 24 giờ
            return 1.0 / (1.0 + Math.Exp(-0.4 * (t - 10.0)));
        }

        /// <summary>Đường kính khuẩn lạc trên đĩa thạch theo mm</summary>
        public static double ColonyDiameterMm(double hours, double maxDiameterMm = 2.5)
        {
            return ColonyGrowthFraction(hours) * maxDiameterMm;
        }
    }
}
