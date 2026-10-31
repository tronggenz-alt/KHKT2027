// Mô phỏng cấy ria 4 vùng tách khuẩn lạc đơn trên đĩa thạch Petri (C# thuần, không phụ thuộc UnityEngine).
using System;
using System.Collections.Generic;

namespace Khkt.Simulation.Bio
{
    [Serializable]
    public struct StreakStroke
    {
        public int zone;
        public float x0, y0, x1, y1;
        public bool loopSterilizedBefore;
    }

    [Serializable]
    public struct ColonySpot
    {
        public float x, y, diameterMm;
        public string organismId;
    }

    public static class StreakPlate
    {
        /// <summary>
        /// Tính danh sách khuẩn lạc mọc dọc các nét cấy ria theo chủng, thời gian ủ (giờ), nhiệt độ (°C) và hạt giống cố định.
        /// </summary>
        public static void Colonies(
            IReadOnlyList<StreakStroke> strokes,
            double inoculumCfu,
            string organismId,
            double hours,
            double tempC,
            int seed,
            List<ColonySpot> output)
        {
            if (output == null) return;
            output.Clear();
            if (strokes == null || strokes.Count == 0 || inoculumCfu <= 0.0 || hours <= 0.0 || string.IsNullOrEmpty(organismId))
                return;

            // Trên 50 °C hoặc dưới 4 °C vi sinh vật không mọc
            if (tempC > 50.0 || tempC < 4.0)
                return;

            double optTemp = GetOptimalTemp(organismId);
            double maxDia = GetMaxDiameter24h(organismId);

            // Hệ số tốc độ mọc theo nhiệt độ (chuông Gauss quanh nhiệt độ tối ưu)
            double dt = tempC - optTemp;
            double tempFactor = Math.Exp(-(dt * dt) / (2.0 * 8.5 * 8.5));
            double effHours = hours * tempFactor;

            double baseDia = ColonyGrowth.ColonyDiameterMm(effHours, maxDia);
            if (baseDia < 0.05)
                return;

            var rng = new Random(seed);

            // Mật độ ban đầu trên đầu que cấy quy đổi ra số điểm khuẩn lạc tiềm năng cho nét đầu tiên
            double logCfu = Math.Log10(Math.Max(10.0, inoculumCfu));
            double currentDensity = Math.Min(65.0, Math.Max(8.0, logCfu * 9.0));

            int currentZone = strokes[0].zone;

            for (int i = 0; i < strokes.Count; i++)
            {
                StreakStroke s = strokes[i];
                if (i > 0)
                {
                    if (s.zone != currentZone)
                    {
                        // Chuyển sang vùng mới: nếu đã đốt que cấy và để nguội thì mật độ giảm mạnh (tạo khuẩn lạc rời ở vùng 3-4)
                        if (s.loopSterilizedBefore)
                            currentDensity *= 0.22;
                        else
                            currentDensity *= 0.88; // Quên đốt que cấy: mang nguyên mật độ dày sang vùng sau
                        currentZone = s.zone;
                    }
                    else
                    {
                        if (s.loopSterilizedBefore)
                            currentDensity *= 0.25;
                        else
                            currentDensity *= 0.85;
                    }
                }

                float dx = s.x1 - s.x0;
                float dy = s.y1 - s.y0;
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len < 0.5f) continue;

                // Pháp tuyến đơn vị để tạo độ lệch ngang tự nhiên dọc vết que cấy
                float nx = -dy / len;
                float ny = dx / len;

                int count = (int)Math.Round(currentDensity * Math.Min(1.5, len / 25.0));
                if (currentDensity >= 0.6 && count < 1) count = 1;

                // Cạnh tranh dinh dưỡng: khi mật độ dày (vùng 1), khuẩn lạc nhỏ hơn chút; vùng thưa đạt kích thước tối đa
                float crowding = count > 15 ? (float)Math.Max(0.55, 1.0 - (count - 15) * 0.008) : 1.0f;

                for (int k = 0; k < count; k++)
                {
                    float t = count == 1 ? 0.5f : (float)((k + 0.15 + 0.7 * rng.NextDouble()) / count);
                    float jitter = (float)((rng.NextDouble() - 0.5) * 1.4);
                    float cx = s.x0 + dx * t + nx * jitter;
                    float cy = s.y0 + dy * t + ny * jitter;
                    float sizeVar = (float)(0.88 + 0.24 * rng.NextDouble());
                    float dia = (float)baseDia * crowding * sizeVar;

                    output.Add(new ColonySpot
                    {
                        x = cx,
                        y = cy,
                        diameterMm = dia,
                        organismId = organismId
                    });
                }
            }
        }

        private static double GetOptimalTemp(string organismId)
        {
            switch (organismId)
            {
                case "l_bulgaricus": return 42.0;
                case "s_cerevisiae":
                case "b_subtilis":
                case "l_lactis":
                case "tap_bacillus_khong_khi": return 30.0;
                case "tap_nam_moc": return 28.0;
                default: return 37.0; // e_coli_k12
            }
        }

        private static double GetMaxDiameter24h(string organismId)
        {
            switch (organismId)
            {
                case "b_subtilis": return 3.2;
                case "s_cerevisiae": return 2.4;
                case "l_bulgaricus": return 1.4;
                case "l_lactis": return 1.5;
                case "tap_nam_moc": return 4.5;
                case "tap_bacillus_khong_khi": return 2.8;
                default: return 2.0; // e_coli_k12
            }
        }
    }
}

