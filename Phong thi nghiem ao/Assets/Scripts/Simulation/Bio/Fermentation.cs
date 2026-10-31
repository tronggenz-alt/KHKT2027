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
    }
}
