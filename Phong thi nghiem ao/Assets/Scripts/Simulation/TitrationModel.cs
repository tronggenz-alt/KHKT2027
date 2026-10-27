using UnityEngine;

namespace Khkt.Simulation
{
    /// <summary>
    /// Tính pH bằng bảo toàn điện tích: [H+] + [Na+] = [OH-] + [A-].
    /// Dùng được trước/tại/sau điểm tương đương, axit mạnh (ka = 0) và axit yếu.
    /// Bản Python tham chiếu có kiểm thử: khkt/titration.py. Sửa một bên thì sửa cả hai.
    /// </summary>
    public static class TitrationModel
    {
        const double Kw = 1e-14;

        /// <param name="molAcid">Tổng số mol axit HA (hoặc HCl)</param>
        /// <param name="molNa">Tổng số mol NaOH đã thêm</param>
        /// <param name="volumeL">Thể tích dung dịch (lít)</param>
        /// <param name="ka">Hằng số axit; 0 = axit mạnh</param>
        public static double Ph(double molAcid, double molNa, double volumeL, double ka = 0)
        {
            double cA = molAcid / volumeL, cNa = molNa / volumeL;
            double lo = -15, hi = 1;
            for (int i = 0; i < 80; i++)
            {
                double mid = 0.5 * (lo + hi);
                double h = System.Math.Pow(10, mid);
                double aMinus = ka <= 0 ? cA : cA * ka / (ka + h);
                if (h + cNa - Kw / h - aMinus > 0) hi = mid; else lo = mid;
            }
            return -0.5 * (lo + hi);
        }

        /// <summary>Phenolphtalein: 0 = không màu (pH ≤ 8,2), 1 = hồng đậm (pH ≥ 10).</summary>
        public static float PhenolphthaleinFraction(double ph) => Mathf.Clamp01((float)((ph - 8.2) / (10.0 - 8.2)));

        /// <summary>Nhiệt tỏa ra khi trung hòa (J), ΔH ≈ -57,3 kJ/mol cho axit mạnh - bazơ mạnh.</summary>
        public static double NeutralizationHeatJ(double molReacted) => molReacted * 57300.0;
    }
}
