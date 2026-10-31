// Mô phỏng lên men rượu và lên men lactic (C# thuần, không phụ thuộc UnityEngine).
using System;

namespace Khkt.Simulation.Bio
{
    public static class Fermentation
    {
        public const double GlucoseMolarMass = 180.16; // g/mol
        public const double EthanolMolarMass = 46.07;  // g/mol
        public const double Co2MolarMass = 44.01;      // g/mol
        public const double IdealGasConstantR = 0.0821; // L*atm/(mol*K)

        /// <summary>Số mol CO2 sinh ra từ m gram glucose lên men rượu hoàn toàn: C6H12O6 -> 2 C2H5OH + 2 CO2</summary>
        public static double MolesCo2FromGlucose(double glucoseGrams, double efficiency = 0.90)
        {
            if (glucoseGrams <= 0) return 0;
            double molGlucose = glucoseGrams / GlucoseMolarMass;
            return 2.0 * molGlucose * efficiency;
        }

        /// <summary>Thể tích khí CO2 làm phồng bóng bay (Lít) ở nhiệt độ t (°C) và áp suất 1 atm</summary>
        public static double BalloonVolumeLiters(double molesCo2, double tempCelsius = 30.0, double pressureAtm = 1.0)
        {
            if (molesCo2 <= 0) return 0;
            double tempK = 273.15 + tempCelsius;
            // V = n * R * T / P
            return (molesCo2 * IdealGasConstantR * tempK) / pressureAtm;
        }

        /// <summary>Đường kính bóng bay phồng (cm) giả định hình cầu</summary>
        public static double BalloonDiameterCm(double volumeLiters)
        {
            if (volumeLiters <= 0) return 0;
            // V = 4/3 * pi * r^3 -> r = (3V / (4pi))^(1/3)
            double volumeCm3 = volumeLiters * 1000.0;
            double radiusCm = Math.Pow((3.0 * volumeCm3) / (4.0 * Math.PI), 1.0 / 3.0);
            return radiusCm * 2.0;
        }

        /// <summary>Khối lượng axit lactic (g) sinh ra từ m gram glucose lên men lactic: C6H12O6 -> 2 CH3CH(OH)COOH</summary>
        public static double LacticAcidGrams(double glucoseGrams, double efficiency = 0.92)
        {
            if (glucoseGrams <= 0) return 0;
            return glucoseGrams * efficiency;
        }

        /// <summary>
        /// Khối lượng axit lactic (g) sinh ra theo thời gian ủ (giờ), nhiệt độ (°C) và thể tích sữa (mL) trong lên men sữa chua.
        /// </summary>
        public static double LacticAcidGramsAt(double hours, double tempC, double milkMl)
        {
            if (hours <= 0.0 || milkMl <= 0.0 || tempC < 10.0 || tempC > 55.0)
                return 0.0;

            // Nhiệt độ tối ưu cho L. bulgaricus / S. thermophilus là ~42 °C
            double dt = tempC - 42.0;
            double tempFactor = Math.Exp(-(dt * dt) / (2.0 * 8.5 * 8.5));

            // Lượng axit lactic tối đa sinh ra đạt khoảng 0,95 g trên 100 mL sữa (tương ứng độ chua ~0,95%, pH ~4,4)
            double maxGrams = (milkMl / 100.0) * 0.95;
            double rate = 0.35 * tempFactor;
            double progress = 1.0 - Math.Exp(-rate * hours);
            return maxGrams * progress;
        }

        /// <summary>
        /// Số mol khí CO2 sinh ra theo thời gian (giờ), nhiệt độ (°C), khối lượng đường (g) và trạng thái sống của nấm men.
        /// </summary>
        public static double Co2MolesAt(double hours, double tempC, double sugarG, bool yeastAlive)
        {
            if (!yeastAlive || hours <= 0.0 || sugarG <= 0.0 || tempC < 5.0 || tempC > 50.0)
                return 0.0;

            // Nhiệt độ tối ưu lên men rượu của S. cerevisiae khoảng 30 - 32 °C
            double dt = tempC - 32.0;
            double tempFactor = Math.Exp(-(dt * dt) / (2.0 * 9.0 * 9.0));

            double maxMoles = MolesCo2FromGlucose(sugarG, 0.90);
            double rate = 0.45 * tempFactor;
            double progress = 1.0 - Math.Exp(-rate * hours);
            return maxMoles * progress;
        }
    }
}
