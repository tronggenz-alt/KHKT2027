// Mô phỏng ghép gen – tạo ADN tái tổ hợp (C# thuần, không phụ thuộc UnityEngine).
// Quy trình Sinh học 12 "Công nghệ gen": cắt thể truyền (plasmid) và gen cần chuyển bằng cùng enzyme cắt giới hạn
// (restrictaza) → nối bằng ADN ligaza → biến nạp vào E. coli → chọn dòng trên đĩa LB + ampicillin + X-gal
// (khuẩn lạc trắng: có gen ghép làm hỏng lacZα; xanh: plasmid tự đóng vòng) → cắt kiểm tra và điện di.
using System;
using System.Collections.Generic;

namespace Khkt.Simulation.Bio
{
    /// <summary>Enzyme cắt giới hạn: trình tự nhận biết và kiểu đầu dính tạo ra sau khi cắt.</summary>
    public sealed class RestrictionEnzyme
    {
        public string Name;
        public string Site;      // trình tự nhận biết 5'→3' (đối xứng đảo ngược)
        public string Overhang;  // đầu dính sinh ra; hai đầu nối được với nhau khi cùng Overhang

        public static readonly RestrictionEnzyme EcoRI = new RestrictionEnzyme { Name = "EcoRI", Site = "G^AATTC", Overhang = "AATT" };
        public static readonly RestrictionEnzyme BamHI = new RestrictionEnzyme { Name = "BamHI", Site = "G^GATCC", Overhang = "GATC" };
        public static readonly RestrictionEnzyme HindIII = new RestrictionEnzyme { Name = "HindIII", Site = "A^AGCTT", Overhang = "AGCT" };
        public static readonly RestrictionEnzyme[] All = { EcoRI, BamHI, HindIII };

        public static RestrictionEnzyme Find(string name)
        {
            foreach (var e in All) if (string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }
    }

    /// <summary>Vị trí cắt của một enzyme trên phân tử (bp tính từ 0).</summary>
    public struct CutSite
    {
        public string Enzyme;
        public int Position;
        public CutSite(string enzyme, int position) { Enzyme = enzyme; Position = position; }
    }

    /// <summary>Phân tử ADN: plasmid vòng hoặc đoạn thẳng có hai đầu (đầu dính của enzyme nào, hoặc "" = đầu bằng / đầu PCR).</summary>
    public sealed class DnaMolecule
    {
        public string Name = "";
        public int LengthBp;
        public bool Circular;
        public string LeftEnd = "", RightEnd = "";
        public readonly List<CutSite> Sites = new List<CutSite>();
        public bool HasAmpR;          // gen kháng ampicillin (chọn lọc tế bào nhận plasmid)
        public bool LacZIntact;       // lacZα nguyên vẹn → khuẩn lạc xanh trên X-gal
        public string InsertName = ""; // gen đã ghép vào (rỗng = không có)

        public DnaMolecule Clone()
        {
            var m = new DnaMolecule { Name = Name, LengthBp = LengthBp, Circular = Circular, LeftEnd = LeftEnd, RightEnd = RightEnd, HasAmpR = HasAmpR, LacZIntact = LacZIntact, InsertName = InsertName };
            m.Sites.AddRange(Sites);
            return m;
        }

        public bool HasSite(string enzyme)
        {
            foreach (var s in Sites) if (s.Enzyme == enzyme) return true;
            return false;
        }

        public override string ToString() => $"{Name} ({LengthBp} bp, {(Circular ? "vòng" : "thẳng")})";
    }

    public enum LigationProduct { Recombinant, SelfLigatedVector, UncutVector }

    public struct LigationResult
    {
        public double RecombinantShare, SelfLigatedShare, UncutShare; // tỉ lệ các loại plasmid vòng (tổng = 1 nếu có)
        public double CircularYield;   // lượng plasmid vòng tạo ra (0..1, tương đối so với lý tưởng)
        public DnaMolecule Recombinant; // null nếu không thể tạo
        public string Note;
    }

    public struct TransformationResult
    {
        public int WhiteColonies, BlueColonies;
        public bool Lawn;      // không có kháng sinh: vi khuẩn mọc kín đĩa, không chọn lọc được
        public string Note;
    }

    public static class DnaCloning
    {
        // ------------------------------------------------------------------
        // Phân tử mẫu dùng trong phòng thí nghiệm ảo
        // ------------------------------------------------------------------

        public const int VectorLengthBp = 2686;   // pUC19
        public const int GeneLengthBp = 720;      // gen GFP (huỳnh quang xanh lục)
        public const int PcrProductBp = 738;      // gen + 2 đuôi mồi mang vị trí EcoRI (đầu 5') và BamHI (đầu 3')

        /// <summary>Plasmid pUC19: vùng đa điểm cắt (MCS) nằm giữa gen lacZα; có gen kháng ampicillin.</summary>
        public static DnaMolecule Pu19()
        {
            var m = new DnaMolecule { Name = "Plasmid pUC19", LengthBp = VectorLengthBp, Circular = true, HasAmpR = true, LacZIntact = true };
            m.Sites.Add(new CutSite("EcoRI", 396));
            m.Sites.Add(new CutSite("BamHI", 417));
            m.Sites.Add(new CutSite("HindIII", 447));
            return m;
        }

        /// <summary>Sản phẩm PCR gen GFP: mồi xuôi mang EcoRI, mồi ngược mang BamHI (để ghép có định hướng).</summary>
        public static DnaMolecule GfpPcrProduct()
        {
            var m = new DnaMolecule { Name = "Sản phẩm PCR gen GFP", LengthBp = PcrProductBp, Circular = false, InsertName = "GFP" };
            m.Sites.Add(new CutSite("EcoRI", 6));
            m.Sites.Add(new CutSite("BamHI", PcrProductBp - 6));
            return m;
        }

        // ------------------------------------------------------------------
        // Cắt bằng enzyme giới hạn
        // ------------------------------------------------------------------

        /// <summary>
        /// Cắt phân tử bằng các enzyme đã cho (cắt hoàn toàn). Vòng bị cắt n chỗ → n đoạn thẳng; thẳng cắt n chỗ → n + 1 đoạn.
        /// Đoạn nằm giữa hai vị trí cắt mang đầu dính của enzyme ở hai phía. Không có vị trí cắt nào → trả lại bản sao nguyên vẹn.
        /// </summary>
        public static List<DnaMolecule> Digest(DnaMolecule m, IList<string> enzymes)
        {
            var cuts = new List<CutSite>();
            foreach (var s in m.Sites)
                foreach (var e in enzymes)
                    if (string.Equals(s.Enzyme, e, StringComparison.OrdinalIgnoreCase)) { cuts.Add(s); break; }
            cuts.Sort((a, b) => a.Position.CompareTo(b.Position));

            var result = new List<DnaMolecule>();
            if (cuts.Count == 0) { result.Add(m.Clone()); return result; }

            if (m.Circular)
            {
                // Đoạn i đi từ vị trí cắt i tới vị trí cắt i + 1; đoạn cuối đi vòng qua bp 0 nên là phần khung (mang ampR,
                // vì các vị trí cắt của MCS nằm sát nhau). Đầu trái mang đầu dính của vết cắt i, đầu phải của vết cắt i + 1.
                for (int i = 0; i < cuts.Count; i++)
                {
                    var a = cuts[i];
                    var b = cuts[(i + 1) % cuts.Count];
                    int len = cuts.Count == 1 ? m.LengthBp : (b.Position - a.Position + m.LengthBp) % m.LengthBp;
                    bool backbone = i == cuts.Count - 1;
                    result.Add(new DnaMolecule
                    {
                        Name = cuts.Count == 1 ? m.Name + " (mở vòng)" : (backbone ? m.Name + " (khung)" : m.Name + " (đoạn cắt ra)"),
                        LengthBp = len,
                        Circular = false,
                        LeftEnd = a.Enzyme,
                        RightEnd = b.Enzyme,
                        HasAmpR = m.HasAmpR && backbone,
                        LacZIntact = false,
                        InsertName = backbone ? "" : m.InsertName,
                    });
                }
                return result;
            }

            int prev = 0;
            string prevEnd = m.LeftEnd;
            for (int i = 0; i <= cuts.Count; i++)
            {
                int pos = i < cuts.Count ? cuts[i].Position : m.LengthBp;
                string end = i < cuts.Count ? cuts[i].Enzyme : m.RightEnd;
                int len = pos - prev;
                if (len > 0)
                {
                    bool middle = i > 0 && i < cuts.Count;
                    result.Add(new DnaMolecule
                    {
                        Name = middle ? m.Name + " (đã cắt hai đầu)" : m.Name + " (mẩu đầu)",
                        LengthBp = len,
                        Circular = false,
                        LeftEnd = prevEnd,
                        RightEnd = end,
                        InsertName = middle || cuts.Count == 1 ? m.InsertName : "",
                    });
                }
                prev = pos;
                prevEnd = end;
            }
            return result;
        }

        /// <summary>Đoạn lớn nhất (khung plasmid hoặc gen đã cắt hai đầu).</summary>
        public static DnaMolecule Largest(List<DnaMolecule> fragments)
        {
            DnaMolecule best = null;
            foreach (var f in fragments) if (best == null || f.LengthBp > best.LengthBp) best = f;
            return best;
        }

        /// <summary>Hai đầu nối được khi cùng loại đầu dính (cùng enzyme, hoặc enzyme khác nhưng cùng phần nhô).</summary>
        public static bool EndsCompatible(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false; // đầu bằng / đầu PCR chưa cắt: coi như không nối (hiệu suất rất thấp)
            if (a == b) return true;
            var ea = RestrictionEnzyme.Find(a);
            var eb = RestrictionEnzyme.Find(b);
            return ea != null && eb != null && ea.Overhang == eb.Overhang;
        }

        // ------------------------------------------------------------------
        // Nối bằng ADN ligaza
        // ------------------------------------------------------------------

        /// <param name="vector">Khung plasmid đã cắt (đoạn thẳng), hoặc plasmid vòng nếu quên cắt.</param>
        /// <param name="insert">Gen đã cắt (đoạn thẳng).</param>
        /// <param name="insertToVectorRatio">Tỉ lệ số phân tử gen : khung (tối ưu khoảng 3 : 1).</param>
        /// <param name="tempC">Nhiệt độ ủ nối (T4 ligaza: 16 °C qua đêm hoặc 22 °C 1 giờ).</param>
        public static LigationResult Ligate(DnaMolecule vector, DnaMolecule insert, bool hasLigase, bool hasBuffer, double insertToVectorRatio, double tempC, double minutes)
        {
            var r = new LigationResult();
            if (vector == null) { r.Note = "Không có thể truyền (plasmid)."; return r; }

            if (vector.Circular)
            {
                // quên cắt plasmid: chỉ có plasmid nguyên vẹn, gen không chèn vào được
                r.UncutShare = 1;
                r.CircularYield = 1;
                r.Note = "Plasmid chưa bị cắt nên gen không chèn vào được: chỉ có plasmid ban đầu.";
                return r;
            }

            const double uncutBackground = 0.03; // một phần nhỏ plasmid cắt không hết vẫn còn vòng
            double ligEff = hasLigase && hasBuffer ? 1.0 : 0.0;
            if (ligEff > 0)
            {
                if (tempC > 37) ligEff *= 0.15;         // ligaza mất hoạt tính, đầu dính 4 nu không bắt cặp bền
                else if (tempC > 25) ligEff *= 0.6;
                ligEff *= Math.Min(1.0, minutes / 60.0); // chưa đủ 1 giờ thì nối chưa hết
            }

            bool selfCompatible = EndsCompatible(vector.LeftEnd, vector.RightEnd);
            bool forward = insert != null && EndsCompatible(vector.RightEnd, insert.LeftEnd) && EndsCompatible(insert.RightEnd, vector.LeftEnd);
            bool reverse = insert != null && EndsCompatible(vector.RightEnd, insert.RightEnd) && EndsCompatible(insert.LeftEnd, vector.LeftEnd);
            double ratio = Math.Max(0.0, insertToVectorRatio);

            double pRec = (forward || reverse) ? ligEff * 0.85 * ratio / (ratio + 0.8) : 0.0;
            double pSelf = selfCompatible ? ligEff * 0.55 / (1.0 + 0.25 * ratio) : 0.0;
            double total = pRec + pSelf + uncutBackground;
            r.RecombinantShare = pRec / total;
            r.SelfLigatedShare = pSelf / total;
            r.UncutShare = uncutBackground / total;
            r.CircularYield = Math.Min(1.0, total);

            if (pRec > 0)
            {
                var rec = new DnaMolecule
                {
                    Name = $"Plasmid tái tổ hợp pUC19-{insert.InsertName}",
                    LengthBp = vector.LengthBp + insert.LengthBp,
                    Circular = true,
                    HasAmpR = vector.HasAmpR,
                    LacZIntact = false,   // gen chèn vào giữa lacZα làm hỏng gen này → khuẩn lạc trắng
                    InsertName = insert.InsertName,
                };
                // hai vị trí cắt tái tạo ở hai đầu gen (dùng để cắt kiểm tra)
                rec.Sites.Add(new CutSite(vector.RightEnd, 396));
                rec.Sites.Add(new CutSite(vector.LeftEnd, 396 + insert.LengthBp));
                r.Recombinant = rec;
            }

            if (ligEff <= 0) r.Note = hasLigase ? "Thiếu đệm ligaza (cần ATP, Mg²⁺): không nối được." : "Không có ADN ligaza: các đoạn không nối lại, gần như không có plasmid vòng.";
            else if (!(forward || reverse)) r.Note = "Đầu dính của gen và khung plasmid không khớp nhau: gen không ghép được.";
            else if (selfCompatible) r.Note = "Khung cắt bằng một enzyme nên hai đầu tự nối lại được: sẽ có nhiều khuẩn lạc xanh.";
            else r.Note = "Cắt bằng hai enzyme khác nhau: khung không tự đóng vòng, gen ghép đúng chiều.";
            return r;
        }

        // ------------------------------------------------------------------
        // Biến nạp và chọn dòng
        // ------------------------------------------------------------------

        /// <param name="heatShockSeconds">Sốc nhiệt 42 °C (tối ưu 30 – 60 s).</param>
        public static TransformationResult Transform(in LigationResult lig, bool competentCellsOnIce, double heatShockSeconds, bool ampicillin, bool xgal, int seed)
        {
            var t = new TransformationResult();
            double eff = competentCellsOnIce ? 1.0 : 0.15;   // tế bào khả biến để ấm làm màng mất khả năng nhận ADN
            if (heatShockSeconds <= 0) eff *= 0.08;
            else if (heatShockSeconds < 20) eff *= 0.4;
            else if (heatShockSeconds > 120) eff *= 0.2;      // sốc quá lâu làm chết tế bào
            else if (heatShockSeconds > 90) eff *= 0.6;

            if (!ampicillin)
            {
                t.Lawn = true;
                t.Note = "Đĩa không có ampicillin: mọi vi khuẩn đều mọc kín đĩa, không phân biệt được tế bào nhận plasmid.";
                return t;
            }

            var rng = new Random(seed);
            double baseColonies = 320.0 * eff * lig.CircularYield;
            double jitter = 0.9 + 0.2 * rng.NextDouble();
            int total = (int)Math.Round(baseColonies * jitter);
            int white = (int)Math.Round(total * lig.RecombinantShare);
            int blue = Math.Max(0, total - white);
            if (!xgal) { white += blue; blue = 0; }
            t.WhiteColonies = white;
            t.BlueColonies = blue;
            if (total == 0) t.Note = "Không có khuẩn lạc: không có plasmid vòng mang gen kháng ampicillin vào được tế bào.";
            else if (!xgal) t.Note = "Thiếu X-gal / IPTG: mọi khuẩn lạc đều trắng, không phân biệt được dòng có gen ghép.";
            else t.Note = $"{white} khuẩn lạc trắng (mang gen ghép) và {blue} khuẩn lạc xanh (plasmid không có gen).";
            return t;
        }

        /// <summary>
        /// Kích thước "biểu kiến" khi chạy điện di: plasmid vòng siêu xoắn chưa cắt chạy nhanh hơn đoạn thẳng cùng kích thước
        /// (khoảng 0,7 lần). Đoạn thẳng giữ nguyên.
        /// </summary>
        public static int ApparentBp(DnaMolecule m) => m.Circular ? (int)Math.Round(m.LengthBp * 0.7) : m.LengthBp;

        /// <summary>Băng điện di của danh sách phân tử (ng chia theo khối lượng tỉ lệ kích thước).</summary>
        public static void ToBands(List<DnaMolecule> molecules, double totalNg, List<DnaBand> output)
        {
            output.Clear();
            int sum = 0;
            foreach (var m in molecules) sum += m.LengthBp;
            if (sum <= 0) return;
            foreach (var m in molecules)
                output.Add(new DnaBand { bp = ApparentBp(m), ng = totalNg * m.LengthBp / sum, smear = false });
        }
    }
}
