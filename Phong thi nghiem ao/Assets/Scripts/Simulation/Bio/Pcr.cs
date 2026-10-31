// Mô phỏng phản ứng chuỗi polymerase (PCR) (C# thuần, không phụ thuộc UnityEngine).
using System;
using System.Collections.Generic;

namespace Khkt.Simulation.Bio
{
    [Serializable]
    public struct PcrSetup
    {
        public bool hasTemplate, hasPrimers, hasTaq, hasDntp, hasBuffer, lidHeated, productContamination;
        public double annealC, primerTm, enzymeWarmMinutes, templateCopies;
        public int cycles, targetBp;
    }

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

        /// <summary>
        /// Chạy phản ứng PCR hoàn chỉnh và xuất các băng ADN thu được vào danh sách output.
        /// </summary>
        public static void Run(in PcrSetup s, int seed, List<DnaBand> output)
        {
            if (output == null) return;
            output.Clear();

            // Thiếu một trong các thành phần thiết yếu (mồi, Taq, dNTP, đệm) -> phản ứng không xảy ra
            if (!s.hasPrimers || !s.hasTaq || !s.hasDntp || !s.hasBuffer)
                return;
            if (s.cycles <= 0 || s.targetBp <= 0)
                return;

            double effTemplate = s.hasTemplate ? Math.Max(0.0, s.templateCopies) : 0.0;
            // Nhiễm chéo sản phẩm PCR (amplicon carryover) làm xuất hiện băng ở cả ống đối chứng âm
            if (s.productContamination)
            {
                effTemplate = Math.Max(effTemplate, 1000.0);
            }
            if (effTemplate <= 0.0)
                return;

            // Nếu nhiệt độ gắn mồi Ta quá cao so với Tm (> Tm + 3 °C), mồi không bắt cặp được -> mất băng hoàn toàn
            if (s.annealC > s.primerTm + 3.0)
                return;

            double eff = DefaultEfficiency;
            double attenuation = 1.0;

            // Ta cao hơn Tm (nhưng <= Tm + 3): mồi gắn kém -> băng mờ rõ rệt
            if (s.annealC > s.primerTm)
            {
                double over = s.annealC - s.primerTm;
                eff *= Math.Max(0.68, 0.86 - over * 0.05);
                attenuation *= Math.Max(0.08, 0.22 - over * 0.04);
            }
            else if (s.annealC < s.primerTm - 10.0)
            {
                // Ta quá thấp (< Tm - 10 °C): gắn mồi không đặc hiệu làm giảm nhẹ hiệu suất băng chính và sinh băng phụ
                eff *= 0.78;
                attenuation *= 0.65;
            }

            // Quên bật nắp nhiệt (heated lid): hơi nước ngưng tụ trên nắp ống làm sai lệch nồng độ, giảm hiệu suất
            if (!s.lidHeated)
            {
                eff *= 0.65;
                attenuation *= 0.45;
            }

            // Để enzyme Taq ngoài đá lạnh lâu trước khi chạy -> giảm hoạt tính theo thời gian
            if (s.enzymeWarmMinutes > 5.0)
            {
                double decay = Math.Exp(-(s.enzymeWarmMinutes - 5.0) * 0.035);
                eff *= Math.Max(0.1, decay);
                attenuation *= decay;
            }

            double copies = AmplifiedCopies(effTemplate, s.cycles, eff);
            // Quy đổi số bản sao sang khối lượng nanogram (ng)
            double massNg = ((copies * s.targetBp * 660.0) / 6.022e14) * attenuation;

            if (massNg >= 0.2)
            {
                output.Add(new DnaBand
                {
                    bp = s.targetBp,
                    ng = massNg,
                    smear = s.enzymeWarmMinutes > 35.0
                });
            }

            // Sinh thêm băng phụ (non-specific bands) khi Ta < Tm - 10 °C
            if (s.annealC < s.primerTm - 10.0 && massNg >= 0.5)
            {
                var rng = new Random(seed);
                int subBp1 = Math.Max(100, (int)Math.Round(s.targetBp * (0.42 + 0.18 * rng.NextDouble())));
                if (subBp1 == s.targetBp) subBp1 = Math.Max(100, s.targetBp / 2);
                output.Add(new DnaBand
                {
                    bp = subBp1,
                    ng = massNg * (0.30 + 0.15 * rng.NextDouble()),
                    smear = false
                });
            }
        }
    }
}
