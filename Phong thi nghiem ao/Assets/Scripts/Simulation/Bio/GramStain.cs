// Mô phỏng quy trình nhuộm phân biệt Gram 4 bước theo chuẩn ASM (C# thuần, không phụ thuộc UnityEngine).
using System;

namespace Khkt.Simulation.Bio
{
    public enum GramResult
    {
        Tim,
        Hong,
        LoangLo,
        KhongCoTeBao
    }

    public static class GramStain
    {
        // Ngưỡng thời gian chuẩn (giây) dựa theo ASM Gram Stain Protocol (https://asm.org/Protocols/Gram-Stain-Protocols)
        public const double MinCrystalVioletSeconds = 20.0;
        public const double MinIodineSeconds = 20.0;
        public const double MinDecolorizeSeconds = 5.0;
        public const double MaxDecolorizeSeconds = 30.0;
        public const double MinSafraninSeconds = 15.0;
        public const double MaxSmearThickness01 = 0.75;

        /// <summary>
        /// Đánh giá kết quả màu sắc tế bào sau quy trình nhuộm Gram 4 bước.
        /// </summary>
        public static GramResult Result(
            bool gramPositive,
            double crystalVioletS,
            double iodineS,
            double decolorizeS,
            double safraninS,
            bool heatFixed,
            double smearThickness01)
        {
            // 1. Không cố định nhiệt trên ngọn lửa -> tế bào trôi sạch khi rửa nước
            if (!heatFixed || smearThickness01 <= 0.01)
                return GramResult.KhongCoTeBao;

            // 2. Vết bôi quá dày -> cồn tẩy không đều, chỗ tím chỗ hồng loang lổ
            if (smearThickness01 > MaxSmearThickness01)
                return GramResult.LoangLo;

            // 3. Chưa nhuộm tím kết tinh đủ thời gian
            if (crystalVioletS < MinCrystalVioletSeconds)
            {
                return safraninS >= MinSafraninSeconds ? GramResult.Hong : GramResult.LoangLo;
            }

            // 4. Tẩy cồn quá lâu (> 30s): phức hợp CV-I bị rửa trôi cả ở vi khuẩn Gram dương
            if (decolorizeS > MaxDecolorizeSeconds)
            {
                return safraninS >= MinSafraninSeconds ? GramResult.Hong : GramResult.LoangLo;
            }

            // 5. Tẩy cồn quá ngắn (< 5s): lớp màng ngoài Gram âm chưa bị hòa tan hết, giữ nguyên màu tím
            if (decolorizeS < MinDecolorizeSeconds)
            {
                return GramResult.Tim;
            }

            // 6. Thời gian tẩy cồn chuẩn (5 .. 30s)
            if (gramPositive)
            {
                // Nếu quên nhỏ Lugol (hoặc quá ngắn < 20s), phức CV-I không bền nên bị cồn rửa trôi
                if (iodineS < MinIodineSeconds)
                    return safraninS >= MinSafraninSeconds ? GramResult.Hong : GramResult.LoangLo;
                return GramResult.Tim;
            }
            else
            {
                // Vi khuẩn Gram âm đã mất màu tím sau bước tẩy cồn chuẩn, bắt màu hồng safranin
                return safraninS >= MinSafraninSeconds ? GramResult.Hong : GramResult.LoangLo;
            }
        }
    }
}

