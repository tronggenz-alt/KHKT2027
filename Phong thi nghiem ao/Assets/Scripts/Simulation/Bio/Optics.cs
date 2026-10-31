// Mô phỏng quang học kính hiển vi (C# thuần, không phụ thuộc UnityEngine).
using System;

namespace Khkt.Simulation.Bio
{
    public enum ObjectiveType
    {
        Scan4x,
        Low10x,
        High40x,
        Oil100x
    }

    public static class Optics
    {
        public const double DefaultWavelengthUm = 0.55; // 550 nm (ánh sáng xanh lục)
        public const double RetinaEyeResolutionMm = 0.0014; // 1,4 µm giới hạn võng mạc người

        public static (double mag, double na, string ringColor, bool isOil) GetObjectiveProps(ObjectiveType type)
        {
            switch (type)
            {
                case ObjectiveType.Scan4x:
                    return (4.0, 0.10, "Đỏ", false);
                case ObjectiveType.Low10x:
                    return (10.0, 0.25, "Vàng", false);
                case ObjectiveType.High40x:
                    return (40.0, 0.65, "Xanh nhạt", false);
                case ObjectiveType.Oil100x:
                    return (100.0, 1.25, "Trắng", true);
                default:
                    return (10.0, 0.25, "Vàng", false);
            }
        }

        public static double TotalMagnification(double eyeMag, double objMag) => eyeMag * objMag;

        /// <summary>Đường kính thị trường theo mm (Field of View): FOV = FN / M_obj</summary>
        public static double FieldOfViewMm(double fieldNumberMm, double objMag)
        {
            if (objMag <= 0) return 0;
            return fieldNumberMm / objMag;
        }

        /// <summary>Độ phân giải theo Rayleigh (µm): R = 0.61 * lambda / NA</summary>
        public static double ResolutionUm(double na, double wavelengthUm = DefaultWavelengthUm)
        {
            if (na <= 0) return double.PositiveInfinity;
            return 0.61 * wavelengthUm / na;
        }

        /// <summary>Độ sâu trường ảnh (Depth of Field) theo µm: DOF = (lambda * n / NA^2) + (e * n / (M * NA))</summary>
        public static double DepthOfFieldUm(double totalMag, double na, bool isOil = false, double wavelengthUm = DefaultWavelengthUm)
        {
            if (na <= 0 || totalMag <= 0) return 0;
            double n = isOil ? 1.515 : 1.0;
            double waveWave = (wavelengthUm * n) / (na * na);
            double geoWave = (RetinaEyeResolutionMm * 1000.0 * n) / (totalMag * na);
            return waveWave + geoWave;
        }

        /// <summary>Hệ số mờ (0 = nét hoàn hảo, >1 = mờ vượt quá độ sâu trường)</summary>
        public static double BlurFactor(double focalOffsetUm, double dofUm)
        {
            if (dofUm <= 0) return 1.0;
            return Math.Abs(focalOffsetUm) / (dofUm * 0.5);
        }

        /// <summary>
        /// Độ sáng trường nhìn kính hiển vi (0..1) theo chiết áp đèn (lamp01), độ mở màn chắn sáng (iris01),
        /// vị trí tụ quang (condenser01) và trạng thái thiếu dầu soi ở vật kính 100x.
        /// </summary>
        public static double Brightness(double lamp01, double iris01, double condenser01, bool oilNeededButMissing)
        {
            double l = Clamp01(lamp01);
            if (l <= 0.0) return 0.0;
            double ir = Clamp01(iris01);
            double cd = Clamp01(condenser01);

            double b = l * (0.12 + 0.88 * ir) * (0.25 + 0.75 * cd);
            if (oilNeededButMissing)
            {
                // Khúc xạ toàn phần qua khe không khí giữa tiêu bản và vật kính 100x làm mất sáng nghiêm trọng
                b *= 0.32;
            }
            return Clamp01(b);
        }

        private static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
    }
}
