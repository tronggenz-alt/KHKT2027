// Mô phỏng độ chính xác và sai số thao tác micropipet theo ISO 8655-2 (C# thuần, không phụ thuộc UnityEngine).
using System;

namespace Khkt.Simulation.Bio
{
    public static class PipetteAccuracy
    {
        // Sai số hệ thống tương đối chuẩn ở 100% thể tích danh định theo ISO 8655-2 (~0,8%)
        // Nguồn: https://www.iso.org/standard/76626.html
        public const double IsoSystematicErrorFraction = 0.008;

        /// <summary>
        /// Thể tích thực tế hút được (µL) phụ thuộc thể tích đặt, cỡ pipet, nấc bấm khi hút (1 hoặc 2), góc nghiêng và độ ngập đầu côn.
        /// </summary>
        public static double ActualUl(
            double setUl,
            double nominalMaxUl,
            int stageWhenAspirating,
            double tiltDeg,
            bool tipSubmerged)
        {
            if (setUl <= 0.0 || nominalMaxUl <= 0.0 || stageWhenAspirating <= 0)
                return 0.0;

            // Đầu côn không ngập trong dịch: hút phải bọt khí, chỉ lấy được lượng rất nhỏ dính đầu côn
            if (!tipSubmerged)
                return setUl * 0.15;

            double vol = Math.Min(setUl, nominalMaxUl * 1.05);

            // Lỗi bấm xuống tận nấc 2 (blow-out) trước khi hút: hút dư 12% (trong dải 1,05 - 1,20x, mo_phong = true)
            if (stageWhenAspirating >= 2)
            {
                vol *= 1.12;
            }

            // Sai số hệ thống ISO 8655-2 tăng nhẹ khi hút ở đầu dưới dải đo (10% nominal)
            double ratio = Math.Max(0.1, vol / nominalMaxUl);
            double isoFactor = 1.0 + IsoSystematicErrorFraction * (0.5 / Math.Sqrt(ratio));
            vol *= isoFactor;

            // Nghiêng pipet > 20 độ làm giảm áp suất thủy tĩnh cột dịch, tăng sai số hút (mo_phong = true)
            double absTilt = Math.Abs(tiltDeg);
            if (absTilt > 20.0)
            {
                double tiltError = Math.Min(0.15, (absTilt - 20.0) * 0.0025);
                vol *= (1.0 + tiltError);
            }

            return vol;
        }

        /// <summary>
        /// Tỉ lệ thể tích dịch còn sót lại trong đầu côn khi nhả (nấc 1 còn sót ~3,5%, nấc 2 thổi hết ≈ 0).
        /// </summary>
        public static double ResidualFraction(int stageWhenDispensing)
        {
            if (stageWhenDispensing <= 0) return 1.0;
            if (stageWhenDispensing == 1) return 0.035; // 3,5% (trong khoảng 2 - 5%, mo_phong = true)
            return 0.0; // Nấc 2 (blow-out) đẩy sạch dịch
        }
    }
}

