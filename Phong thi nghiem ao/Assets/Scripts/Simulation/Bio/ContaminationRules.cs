// Quy tắc tính xác suất tạp nhiễm vi sinh theo điều kiện thao tác vô trùng (C# thuần, không phụ thuộc UnityEngine).
using System;

namespace Khkt.Simulation.Bio
{
    [Serializable]
    public struct AsepticContext
    {
        public bool insideBsc, bscRunning;
        public double bscPurgeMinutes, sashHeightCm;
        public bool grilleBlocked, surfaceClean, handsWashed, ppeOn, nearFlame, toolSterile;
        public double openSeconds;
    }

    public static class ContaminationRules
    {
        // Hệ số khớp với mục he_so_nhiem trong data/sinh/chung_vi_sinh.json (mo_phong = true)
        public const double OpenSecondRate = 0.002;
        public const double FactorGoodBsc = 0.02;
        public const double FactorHighSash = 4.5;
        public const double FactorGrilleBlocked = 3.0;
        public const double FactorUnwashedHands = 2.5;
        public const double FactorNoPpe = 1.8;
        public const double FactorDirtySurface = 3.0;
        public const double FactorUnsterileTool = 15.0;
        public const double FactorNearFlame = 0.15;

        /// <summary>
        /// Xác suất nhiễm mẫu (0.0 .. 1.0) dựa trên ngữ cảnh thao tác vô trùng.
        /// </summary>
        public static double Probability(in AsepticContext c)
        {
            double openSec = Math.Max(0.0, c.openSeconds);
            if (openSec <= 0.0 && c.toolSterile) return 0.0;

            double mult = 1.0;

            if (c.insideBsc)
            {
                if (c.bscRunning && c.bscPurgeMinutes >= 5.0 && c.sashHeightCm > 0.0 && c.sashHeightCm <= 20.0 && !c.grilleBlocked)
                {
                    mult *= FactorGoodBsc;
                }
                else if (c.bscRunning)
                {
                    mult *= 0.12;
                    if (c.sashHeightCm > 20.0) mult *= FactorHighSash;
                    if (c.grilleBlocked) mult *= FactorGrilleBlocked;
                    if (c.bscPurgeMinutes < 5.0) mult *= 1.8;
                }
                else
                {
                    // Trong tủ nhưng chưa bật quạt
                    mult *= 1.1;
                }
            }
            else if (c.nearFlame)
            {
                mult *= FactorNearFlame;
            }

            if (!c.handsWashed) mult *= FactorUnwashedHands;
            if (!c.ppeOn) mult *= FactorNoPpe;
            if (!c.surfaceClean) mult *= FactorDirtySurface;
            if (!c.toolSterile) mult *= FactorUnsterileTool;

            double hazard = openSec * OpenSecondRate * mult;
            if (!c.toolSterile)
            {
                // Dụng cụ chưa tiệt trùng tiếp xúc trực tiếp với mẫu gây nguy cơ nhiễm tức thời
                hazard += 0.45;
            }

            double prob = 1.0 - Math.Exp(-hazard);
            if (prob < 0.0) return 0.0;
            if (prob > 1.0) return 1.0;
            return prob;
        }

        /// <summary>
        /// Tung xúc xắc tất định theo seed để quyết định mẫu có bị tạp nhiễm hay không.
        /// </summary>
        public static bool Roll(in AsepticContext c, int seed, out Contaminant result)
        {
            double p = Probability(in c);
            var rng = new Random(seed);
            if (rng.NextDouble() < p)
            {
                string org = rng.NextDouble() < 0.65 ? "tap_bacillus_khong_khi" : "tap_nam_moc";
                int cols = Math.Max(1, (int)Math.Round(1.0 + p * 15.0 * (0.4 + 0.6 * rng.NextDouble())));
                result = new Contaminant
                {
                    organismId = org,
                    colonies = cols
                };
                return true;
            }

            result = default;
            return false;
        }
    }
}

