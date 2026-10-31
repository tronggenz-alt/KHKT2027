// Mô phỏng tính hướng sáng dương của chồi cây và hiện tượng mọc vống (etiolation) trong tối (C# thuần, không phụ thuộc UnityEngine).
using System;

namespace Khkt.Simulation.Bio
{
    public static class Phototropism
    {
        public const double MaxBendAngleDeg = 70.0;
        public const double TimeConstantHours = 18.0;

        /// <summary>
        /// Góc uốn cong của thân mầm (độ) về phía nguồn sáng sau hours giờ chiếu sáng lệch góc lightAzimuthDeg.
        /// Nếu đặt trong tối hoàn toàn (fullyDark = true), auxin phân bố đều nên cây mọc thẳng (0 độ).
        /// </summary>
        public static double BendAngleDeg(double hours, double lightAzimuthDeg, bool fullyDark)
        {
            if (hours <= 0.0 || fullyDark || Math.Abs(lightAzimuthDeg) < 1e-4)
                return 0.0;

            double targetAngle = Math.Max(-MaxBendAngleDeg, Math.Min(MaxBendAngleDeg, lightAzimuthDeg));
            double progress = 1.0 - Math.Exp(-hours / TimeConstantHours);
            return targetAngle * progress;
        }

        /// <summary>
        /// Hệ số vươn dài trong tối (etiolation): 1,0 = cây chiếu sáng bình thường; > 1,0 = thân mầm vươn dài, mảnh và vàng lá khi ở trong tối.
        /// </summary>
        public static double EtiolationFactor(double hours, bool fullyDark)
        {
            if (!fullyDark || hours <= 0.0)
                return 1.0;

            return 1.0 + 1.25 * (1.0 - Math.Exp(-hours / 36.0));
        }
    }
}

