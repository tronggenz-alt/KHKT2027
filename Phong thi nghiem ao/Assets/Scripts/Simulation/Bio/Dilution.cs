// Mô phỏng pha loãng bậc 10 và đếm khuẩn lạc (CFU/mL) theo tiêu chuẩn ISO 4833-1 (C# thuần, không phụ thuộc UnityEngine).
using System;

namespace Khkt.Simulation.Bio
{
    public static class Dilution
    {
        /// <summary>
        /// Tính mật độ tế bào ban đầu (CFU/mL) từ số khuẩn lạc đếm được trên đĩa, thể tích trải (mL) và hệ số pha loãng (vd 1e-4).
        /// Công thức chuẩn ISO 4833-1: CFU/mL = N / (V * d).
        /// </summary>
        public static double CfuPerMl(int colonies, double platedMl, double dilution)
        {
            if (colonies <= 0 || platedMl <= 0.0 || dilution <= 0.0) return 0.0;
            return colonies / (platedMl * dilution);
        }

        /// <summary>
        /// Kiểm tra đĩa có nằm trong khoảng đếm chuẩn 30 .. 300 khuẩn lạc hay không (ISO 4833 / FDA BAM).
        /// </summary>
        public static bool IsCountable(int colonies)
        {
            return colonies >= 30 && colonies <= 300;
        }

        /// <summary>
        /// Thể tích dịch dư bám thành trong đầu côn (mL) khi không thay đầu côn giữa các bậc pha loãng (mo_phong = true).
        /// Khoảng 0,5% thể tích danh định của đầu côn (đổi từ µL sang mL).
        /// </summary>
        public static double CarryOverMl(double tipNominalUl)
        {
            if (tipNominalUl <= 0.0) return 0.0;
            return (tipNominalUl * 0.005) / 1000.0;
        }

        /// <summary>
        /// Lấy mẫu số khuẩn lạc theo phân phối Poisson với kỳ vọng expected và hạt giống cố định (tất định).
        /// </summary>
        public static int SampleColonies(double expected, int seed)
        {
            if (expected <= 0.0) return 0;
            var rng = new Random(seed);
            if (expected < 30.0)
            {
                double L = Math.Exp(-expected);
                int k = 0;
                double p = 1.0;
                do
                {
                    k++;
                    p *= rng.NextDouble();
                } while (p > L);
                return k - 1;
            }
            else
            {
                // Xấp xỉ chuẩn có hiệu chỉnh liên tục cho lambda lớn
                double u1 = 1.0 - rng.NextDouble();
                double u2 = 1.0 - rng.NextDouble();
                double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                int val = (int)Math.Round(expected + Math.Sqrt(expected) * z);
                return val < 0 ? 0 : val;
            }
        }
    }
}

