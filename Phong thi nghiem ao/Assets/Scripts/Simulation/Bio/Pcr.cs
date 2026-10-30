// Mô phỏng phản ứng chuỗi polymerase (PCR) (C# thuần, không phụ thuộc UnityEngine).
using System;

namespace Khkt.Simulation.Bio
{
    public static class Pcr
    {
        public const double DefaultEfficiency = 0.95; // Hiệu suất 95%
        public const double PlateauLimitCopies = 1e12; // Giới hạn bão hòa nồng độ sản phẩm

        /// <summary>Số bản sao DNA khuếch đại sau n chu kỳ: N(n) = N0 * (1 + E)^n</summary>
        public static double AmplifiedCopies(double initialCopies, int cycles, double efficiency = DefaultEfficiency)
        {
            if (cycles <= 0) return initialCopies;
            if (efficiency < 0) efficiency = 0;
            if (efficiency > 1.0) efficiency = 1.0;

            double ideal = initialCopies * Math.Pow(1.0 + efficiency, cycles);
            // Hiệu ứng bão hòa (plateau effect) do cạn kiệt mồi / dNTPs và ức chế enzyme
            return (ideal * PlateauLimitCopies) / (ideal + PlateauLimitCopies);
        }

        /// <summary>Nhiệt độ nóng chảy của mồi Tm (°C) theo công thức cơ bản (quy tắc Wallace-Itakura cho oligonucleotit): Tm = 2*(A+T) + 4*(G+C)</summary>
        public static double PrimerMeltingTemp(int countA, int countT, int countG, int countC)
        {
            int length = countA + countT + countG + countC;
            if (length <= 0) return 0;
            if (length < 14)
            {
                return 2 * (countA + countT) + 4 * (countG + countC);
            }
            else
            {
                // Công thức cho oligo dài: Tm = 64.9 + 41 * (G+C - 16.4) / L
                return 64.9 + 41.0 * (countG + countC - 16.4) / length;
            }
        }

        /// <summary>Nhiệt độ gắn mồi tối ưu Ta = Tm - 5 (°C)</summary>
        public static double AnnealingTemperature(double primerTm)
        {
            return primerTm - 5.0;
        }

        /// <summary>Thời gian kéo dài ở 72°C (giây) theo độ dài đoạn khuếch đại (khoảng 1 phút cho 1000 bp với Taq polymerase chuẩn)</summary>
        public static double ExtensionDurationSeconds(int ampliconLengthBp, double rateBpPerSecond = 20.0)
        {
            if (ampliconLengthBp <= 0) return 30.0;
            return Math.Max(30.0, ampliconLengthBp / rateBpPerSecond);
        }
    }
}
