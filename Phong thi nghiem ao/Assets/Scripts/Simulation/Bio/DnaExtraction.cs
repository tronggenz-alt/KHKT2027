// Mô phỏng tách chiết ADN thô từ mô thực vật / động vật bằng muối, chất tẩy rửa và kết tủa cồn lạnh (C# thuần, không phụ thuộc UnityEngine).
using System;

namespace Khkt.Simulation.Bio
{
    public static class DnaExtraction
    {
        /// <summary>
        /// Khối lượng ADN kết tủa thu được (µg) từ sampleGrams mẫu vật tùy theo loại mẫu, muối NaCl, chất tẩy rửa SDS/nước rửa chén,
        /// nhiệt độ ủ ly giải (°C), nhiệt độ cồn ethanol (°C) và tỉ lệ thể tích cồn : dịch lọc.
        /// </summary>
        public static double YieldUg(
            double sampleGrams,
            string sampleKind,
            bool salt,
            bool detergent,
            double incubateC,
            double ethanolC,
            double ethanolToLysateRatio)
        {
            if (sampleGrams <= 0.0 || !detergent || ethanolToLysateRatio <= 0.1)
                return 0.0;

            double baseUgPerGram = GetBaseDnaContentUgPerGram(sampleKind);

            // Muối Na+ trung hòa điện tích âm của gốc photphat trên khung ADN giúp sợi ADN kết tụ khi gặp cồn
            double saltFactor = salt ? 1.0 : 0.08;

            // Nhiệt độ ủ ly giải: tối ưu 50 - 60 °C giúp phá màng và bất hoạt DNase; > 85 °C làm biến tính / đứt gãy ADN
            double tempFactor;
            if (incubateC > 85.0)
                tempFactor = 0.20;
            else if (incubateC >= 45.0 && incubateC <= 65.0)
                tempFactor = 1.00;
            else if (incubateC >= 20.0 && incubateC < 45.0)
                tempFactor = 0.72 + 0.28 * ((incubateC - 20.0) / 25.0);
            else
                tempFactor = 0.45;

            // Nhiệt độ cồn: cồn lạnh (-20 °C đến 4 °C) giảm độ tan của ADN tối đa; cồn ấm (25 °C) thu hồi kém hơn
            double ethanolTempFactor;
            if (ethanolC <= 4.0)
                ethanolTempFactor = 1.00;
            else if (ethanolC <= 30.0)
                ethanolTempFactor = Math.Max(0.35, 1.0 - (ethanolC - 4.0) * 0.022);
            else
                ethanolTempFactor = 0.25;

            // Tỉ lệ thể tích cồn : dịch lọc tối ưu từ 1,5 - 2,0 lần
            double ratioFactor = Math.Min(1.0, Math.Max(0.0, (ethanolToLysateRatio - 0.1) / 1.7));

            return sampleGrams * baseUgPerGram * saltFactor * tempFactor * ethanolTempFactor * ratioFactor;
        }

        private static double GetBaseDnaContentUgPerGram(string sampleKind)
        {
            if (string.IsNullOrEmpty(sampleKind)) return 160.0;
            string k = sampleKind.ToLowerInvariant();
            if (k.Contains("gan")) return 420.0;
            if (k.Contains("chuoi") || k.Contains("dau")) return 210.0;
            if (k.Contains("hanh")) return 150.0;
            return 160.0;
        }
    }
}

