// Mô phỏng thẩm thấu và thế nước tế bào thực vật (C# thuần, không phụ thuộc UnityEngine).
using System;

namespace Khkt.Simulation.Bio
{
    public static class Osmosis
    {
        public const double GasConstantLBarPerMolK = 0.08314; // L*bar/(mol*K)
        public const double GasConstantLMpaPerMolK = 0.008314; // L*MPa/(mol*K)

        /// <summary>Thế thẩm thấu (Solute Potential) theo công thức van 't Hoff: Psi_s = -i * C * R * T (MPa)</summary>
        public static double SolutePotentialMpa(double vanTHoffFactor_i, double molarity_C, double tempCelsius)
        {
            double tempK = 273.15 + tempCelsius;
            return -vanTHoffFactor_i * molarity_C * GasConstantLMpaPerMolK * tempK;
        }

        /// <summary>Thế nước (Water Potential): Psi_w = Psi_s + Psi_p (MPa)</summary>
        public static double WaterPotentialMpa(double solutePotential_Psi_s, double pressurePotential_Psi_p)
        {
            return solutePotential_Psi_s + pressurePotential_Psi_p;
        }

        /// <summary>
        /// Tỷ lệ thể tích chất nguyên sinh so với thể tích ban đầu (V / V0):
        /// Khi ngâm trong dung dịch ưu trương (Psi_ext < Psi_int), nước rút ra, áp suất trương Psi_p giảm về 0,
        /// sau đó chất nguyên sinh co lại (co nguyên sinh).
        /// </summary>
        public static double ProtoplastVolumeFraction(double internalPsi_s_Mpa, double externalPsi_w_Mpa, double cellWallElasticity = 5.0)
        {
            if (externalPsi_w_Mpa >= internalPsi_s_Mpa)
            {
                // Dung dịch nhược trương hoặc đẳng trương: tế bào no nước, có áp suất trương nước
                double delta = externalPsi_w_Mpa - internalPsi_s_Mpa;
                return Math.Min(1.05, 1.0 + (delta / cellWallElasticity) * 0.05);
            }
            else
            {
                // Dung dịch ưu trương: co nguyên sinh (Psi_p = 0, nồng độ bên trong tăng cho tới khi cân bằng)
                // Theo định luật Boyle-van 't Hoff: Psi_s * V = hằng số
                double ratio = externalPsi_w_Mpa / internalPsi_s_Mpa;
                if (ratio <= 0) return 0.5;
                double v = 1.0 / ratio;
                return Math.Max(0.45, Math.Min(1.0, v));
            }
        }

        /// <summary>Tỷ lệ co nguyên sinh (% tế bào co nguyên sinh trong vi trường quan sát)</summary>
        public static double PlasmolysisPercentage(double sucroseMolarity)
        {
            // Điểm co nguyên sinh bắt đầu (incipient plasmolysis) của biểu bì vảy hành tím ở khoảng 0,3 - 0,35 M sucrose
            if (sucroseMolarity <= 0.2) return 0.0;
            if (sucroseMolarity >= 0.8) return 100.0;

            // Đường cong phân bố sigmoid
            double x = (sucroseMolarity - 0.35) * 12.0;
            return 100.0 / (1.0 + Math.Exp(-x));
        }
    }
}
