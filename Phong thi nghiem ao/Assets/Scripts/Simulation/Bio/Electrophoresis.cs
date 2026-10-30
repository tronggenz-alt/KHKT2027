// Mô phỏng điện di gel agarose phân tích DNA (C# thuần, không phụ thuộc UnityEngine).
using System;

namespace Khkt.Simulation.Bio
{
    public static class Electrophoresis
    {
        public const double GelLengthCm = 10.0; // Chiều dài khay gel 10 cm
        public const double DefaultGelConcentrationPercent = 1.0; // Gel agarose 1%

        /// <summary>
        /// Quãng đường di chuyển của đoạn DNA (cm) trên gel agarose:
        /// Tốc độ di chuyển tỉ lệ nghịch với log10(kích thước đoạn tính bằng bp).
        /// d = (-k * log10(bp) + c) * (V / 100) * (t / 60)
        /// </summary>
        public static double MigrationDistanceCm(int basePairs, double voltage = 100.0, double timeMinutes = 45.0, double gelPercent = DefaultGelConcentrationPercent)
        {
            if (basePairs <= 0) return 0;
            // Với gel 1%: đoạn 100 bp chạy ~7.5 cm, đoạn 1000 bp chạy ~3.5 cm, đoạn 10000 bp chạy ~1.0 cm
            double logBp = Math.Log10(basePairs);
            double baseDist = 9.5 - 2.0 * logBp;
            if (baseDist < 0.2) baseDist = 0.2;

            // Ảnh hưởng của nồng độ gel (gel đặc hơn -> di chuyển chậm hơn)
            double gelFactor = 1.0 / Math.Sqrt(Math.Max(0.5, gelPercent));
            // Ảnh hưởng điện thế và thời gian
            double timeFactor = timeMinutes / 60.0;
            double voltageFactor = voltage / 100.0;

            double dist = baseDist * gelFactor * timeFactor * voltageFactor;
            return Math.Min(GelLengthCm, Math.Max(0.0, dist));
        }

        /// <summary>Quãng đường vạch thuốc nhuộm chỉ thị Bromophenol Blue (cm) (chạy tương đương đoạn ~300 bp trong gel 1%)</summary>
        public static double TrackingDyeDistanceCm(double voltage = 100.0, double timeMinutes = 45.0)
        {
            return MigrationDistanceCm(300, voltage, timeMinutes, 1.0);
        }

        /// <summary>Độ sáng huỳnh quang của vạch (GelGreen / Ethidium Bromide) tỉ lệ thuận với khối lượng DNA (ng = số bản sao * bp * 660 / 6.022e11)</summary>
        public static double BandFluorescenceIntensity(double copies, int basePairs)
        {
            double massNg = (copies * basePairs * 660.0) / 6.022e11;
            // Độ nhạy phát hiện: từ 1 ng trở lên bắt đầu nhìn rõ
            return Math.Min(1.0, massNg / 50.0);
        }
    }
}
