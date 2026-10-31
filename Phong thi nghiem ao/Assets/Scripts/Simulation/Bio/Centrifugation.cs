// Mô phỏng máy ly tâm vi lượng: gia tốc RCF (×g), tốc độ vòng quay RPM và kiểm tra cân bằng đối xứng rotor (C# thuần, không phụ thuộc UnityEngine).
using System;
using System.Collections.Generic;

namespace Khkt.Simulation.Bio
{
    public static class Centrifugation
    {
        // Hằng số chuyển đổi RCF chuẩn: RCF = 1,118 * 10^-5 * r(cm) * RPM^2
        // Nguồn: https://www.eppendorf.com/product-media/doc/en/59779_Technical-Report/Eppendorf_ Centrifugation_Technical-Report_022_Centrifugation-Basics.pdf
        public const double RcfConstant = 1.118e-5;
        public const double MaxMassImbalanceGrams = 0.10;

        /// <summary>
        /// Tính lực ly tâm tương đối RCF (×g) từ bán kính quay r (cm) và tốc độ quay (rpm).
        /// </summary>
        public static double Rcf(double radiusCm, double rpm)
        {
            if (radiusCm <= 0.0 || rpm <= 0.0) return 0.0;
            return RcfConstant * radiusCm * rpm * rpm;
        }

        /// <summary>
        /// Tính tốc độ quay (rpm) từ bán kính quay r (cm) và lực ly tâm tương đối RCF (×g).
        /// </summary>
        public static double Rpm(double radiusCm, double rcf)
        {
            if (radiusCm <= 0.0 || rcf <= 0.0) return 0.0;
            return Math.Sqrt(rcf / (RcfConstant * radiusCm));
        }

        /// <summary>
        /// Kiểm tra các ống đặt trong rotor có cân bằng đối xứng hay không và trả về lý do bằng tiếng Việt.
        /// Hỗ trợ đánh số lỗ 1..slotCount (mặc định) hoặc 0..slotCount-1.
        /// </summary>
        public static bool IsBalanced(
            IReadOnlyList<(int slot, double grams)> load,
            int slotCount,
            out string reasonVi)
        {
            if (slotCount <= 0)
            {
                reasonVi = "Số lỗ rotor không hợp lệ.";
                return false;
            }

            if (load == null || load.Count == 0)
            {
                reasonVi = "Rotor cân bằng (không có ống).";
                return true;
            }

            bool zeroBased = false;
            for (int i = 0; i < load.Count; i++)
            {
                if (load[i].slot == 0)
                {
                    zeroBased = true;
                    break;
                }
            }

            // Gom khối lượng theo từng lỗ
            var slotMass = new Dictionary<int, double>();
            for (int i = 0; i < load.Count; i++)
            {
                int s = load[i].slot;
                double g = load[i].grams;
                int minSlot = zeroBased ? 0 : 1;
                int maxSlot = zeroBased ? slotCount - 1 : slotCount;
                if (s < minSlot || s > maxSlot)
                {
                    reasonVi = $"Vị trí lỗ {s} nằm ngoài phạm vi rotor ({minSlot}..{maxSlot}).";
                    return false;
                }
                if (g <= 0.0) continue;
                if (slotMass.TryGetValue(s, out double cur))
                    slotMass[s] = cur + g;
                else
                    slotMass[s] = g;
            }

            if (slotMass.Count == 0)
            {
                reasonVi = "Rotor cân bằng (không có ống).";
                return true;
            }

            // Kiểm tra tổng mô-men вектор tâm khối lượng (hỗ trợ cả xếp đối xứng chẵn và tam giác đều 3 ống)
            double sumX = 0.0;
            double sumY = 0.0;
            foreach (var kv in slotMass)
            {
                int idx0 = zeroBased ? kv.Key : (kv.Key - 1);
                double angle = 2.0 * Math.PI * idx0 / slotCount;
                sumX += kv.Value * Math.Cos(angle);
                sumY += kv.Value * Math.Sin(angle);
            }
            double vectorImbalance = Math.Sqrt(sumX * sumX + sumY * sumY);

            // Kiểm tra từng cặp lỗ đối diện trên rotor chẵn để xuất thông báo cụ thể cho học sinh
            if (slotCount % 2 == 0)
            {
                int half = slotCount / 2;
                foreach (var kv in slotMass)
                {
                    int s = kv.Key;
                    double g = kv.Value;
                    int opp = zeroBased
                        ? ((s + half) % slotCount)
                        : (((s - 1 + half) % slotCount) + 1);

                    if (!slotMass.TryGetValue(opp, out double gOpp))
                    {
                        if (vectorImbalance > MaxMassImbalanceGrams)
                        {
                            reasonVi = $"Ống ở lỗ {s} chưa có ống đối diện ở lỗ {opp}.";
                            return false;
                        }
                    }
                    else if (Math.Abs(g - gOpp) > MaxMassImbalanceGrams && vectorImbalance > MaxMassImbalanceGrams)
                    {
                        reasonVi = $"Ống ở lỗ {s} ({g:0.##} g) và ống đối diện ở lỗ {opp} ({gOpp:0.##} g) chênh lệch khối lượng ({Math.Abs(g - gOpp):0.##} g > {MaxMassImbalanceGrams:0.##} g).";
                        return false;
                    }
                }
            }

            if (vectorImbalance > MaxMassImbalanceGrams)
            {
                reasonVi = $"Rotor mất cân bằng tâm khối lượng (độ lệch {vectorImbalance:0.##} g).";
                return false;
            }

            reasonVi = "Rotor đã cân bằng đối xứng.";
            return true;
        }
    }
}

