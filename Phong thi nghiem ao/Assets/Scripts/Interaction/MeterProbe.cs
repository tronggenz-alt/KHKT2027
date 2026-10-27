using System.Globalization;
using Khkt.Simulation.Chem;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Dụng cụ đo cầm tay: nhúng đầu đo vào vật chứa là đọc được số. Kiểu đo: pH, nhiệt độ, độ dẫn điện,
    /// oxi hòa tan. Màn hình số (TextMesh) hiện kết quả như máy thật.
    /// </summary>
    public class MeterProbe : MonoBehaviour
    {
        public enum Kind { PH, Temperature, Conductivity, DissolvedOxygen }

        public Kind kind = Kind.PH;
        public Transform tip;
        public TextMesh display;

        public string LastReading { get; private set; } = "";
        public LabContainer Measuring { get; private set; }

        static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        public string Read(LabContainer c)
        {
            Measuring = c;
            if (c == null || c.VolumeMl < 0.3f) { LastReading = "---"; return LastReading; }
            switch (kind)
            {
                case Kind.PH:
                    double ph = c.BulkPh + Noise(0.01);
                    LastReading = "pH " + ph.ToString("0.00", Vi);
                    break;
                case Kind.Temperature:
                    LastReading = (c.TemperatureC + (float)Noise(0.05)).ToString("0.0", Vi) + " °C";
                    break;
                case Kind.Conductivity:
                    double k = Conductivity(c);
                    LastReading = k >= 1000 ? (k / 1000).ToString("0.00", Vi) + " mS/cm" : k.ToString("0", Vi) + " µS/cm";
                    break;
                default:
                    double o2 = 8.3 * (1 - 0.02 * (c.TemperatureC - 25)) + (c.GasId == "O2" ? 4 : 0);
                    LastReading = o2.ToString("0.0", Vi) + " mg/L";
                    break;
            }
            return LastReading;
        }

        /// <summary>Độ dẫn điện ước tính (µS/cm): Σ λ·|z|·c, H⁺ và OH⁻ linh động hơn nhiều.</summary>
        public static double Conductivity(LabContainer c)
        {
            var mix = c.Total.Mix;
            double V = c.VolumeMl / 1000.0;
            if (V <= 0) return 0;
            double ph = c.BulkPh, h = System.Math.Pow(10, -ph), oh = 1e-14 / h;
            double k = 349.8 * h + 198 * oh;  // mS/cm khi c tính theo mol/l
            for (int i = 0; i < mix.Count; i++)
            {
                var d = mix.SpeciesAt(i);
                double conc = mix.MolAt(i) / V;
                double z = 0;
                if (d.Kind == SpeciesKind.Ion || d.Kind == SpeciesKind.Complex) z = System.Math.Abs(d.Charge);
                else if (d.Kind == SpeciesKind.Weak) z = System.Math.Abs(ChemEngine.WeakCharge(d, h));
                k += 60 * z * conc;
            }
            return k * 1000 + 1.5;
        }

        double Noise(double amp) => (Random.value - 0.5) * 2 * amp;

        void LateUpdate()
        {
            if (display != null && display.text != LastReading) display.text = string.IsNullOrEmpty(LastReading) ? "---" : LastReading;
        }

        public void Clear()
        {
            Measuring = null;
            LastReading = "";
        }
    }
}
