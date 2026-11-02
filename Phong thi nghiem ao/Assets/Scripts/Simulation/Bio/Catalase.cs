// Mô phỏng hệ số hoạt tính của enzyme catalase theo nhiệt độ và trạng thái biến tính nhiệt (C# thuần, không phụ thuộc UnityEngine).
using System;

namespace Khkt.Simulation.Bio
{
    public static class Catalase
    {
        public const double OptimalTempC = 37.0;
        public const double DenatureTempC = 65.0;

        /// <summary>
        /// Hệ số hoạt tính xúc tác tương đối (0..1) của catalase; phản ứng 2 H2O2 -> 2 H2O + O2 đi qua ChemEngine.
        /// </summary>
        public static double ActivityFactor(double tempC, bool boiled)
        {
            if (boiled || tempC < 0.0 || tempC >= DenatureTempC)
                return 0.0;

            double diff = tempC - OptimalTempC;
            double act = Math.Exp(-(diff * diff) / (2.0 * 14.0 * 14.0));
            if (tempC > 55.0)
            {
                act *= (DenatureTempC - tempC) / 10.0;
            }
            return Math.Max(0.0, Math.Min(1.0, act));
        }
    }
}

