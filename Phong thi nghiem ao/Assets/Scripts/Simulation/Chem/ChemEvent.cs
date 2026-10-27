namespace Khkt.Simulation.Chem
{
    public enum EventKind { Precipitate, Dissolve, Gas, Color, Heat, Layer, Reaction, Hazard }

    public enum Severity { Info, Notice, Warning, Danger }

    /// <summary>
    /// Một hiện tượng định tính bộ máy hóa học phát hiện được (kết tủa, khí, đổi màu, tỏa nhiệt, nguy hiểm).
    /// Unity hiện chú thích ngay từ nội dung này; AI (nếu có) chỉ diễn giải thêm, không quyết định phản ứng.
    /// </summary>
    public sealed class ChemEvent
    {
        public EventKind Kind;
        public Severity Severity = Severity.Info;
        /// <summary>Khóa lọc trùng, ví dụ "kt:AgCl", "khi:CO2", "pu:MnO4-+Fe2+".</summary>
        public string Key;
        public string Title;
        public string Detail;
        public string Equation;
        /// <summary>null | khi_doc | chay_no | bong | an_mon | ban_be</summary>
        public string Hazard;
        public string GasId;
        public double Amount;
        public Rgb Color = Rgb.White;

        public override string ToString() => $"[{Kind}/{Severity}] {Title} | {Equation} | {Detail}";
    }

    public struct ChemEnv
    {
        public double TempC;
        public bool InFumeHood;
        /// <summary>0..1 mức khuấy/lắc (tan nhanh hơn)</summary>
        public double Stir;

        public static ChemEnv Room => new ChemEnv { TempC = 25 };
    }
}
