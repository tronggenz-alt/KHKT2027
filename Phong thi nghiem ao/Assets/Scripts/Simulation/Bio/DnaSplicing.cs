// Mô phỏng cắt giới hạn và ghép nối ADN tái tổ hợp (C# thuần, không phụ thuộc UnityEngine).
using System;
using System.Collections.Generic;

namespace Khkt.Simulation.Bio
{
    public sealed class StickyEnd
    {
        public string EnzymeName = "";
        public string Overhang5To3 = ""; // "" nếu là đầu bằng (blunt end)
        public bool IsBlunt => string.IsNullOrEmpty(Overhang5To3);
    }

    public sealed class DnaFragment
    {
        public string Id = "";
        public string Name = "";
        public int LengthBp;
        public bool IsCircular;
        public bool HasAmpR;
        public bool HasTetR;
        public bool HasLacZ;
        public string LeftEnzyme = "";
        public string RightEnzyme = "";
        public StickyEnd LeftStickyEnd;
        public StickyEnd RightStickyEnd;
        public readonly List<string> AvailableSites = new List<string>();
    }

    public sealed class RecombinantPlasmid
    {
        public string Name = "";
        public string VectorName = "";
        public string InsertName = "";
        public int VectorLengthBp;
        public int InsertLengthBp;
        public int TotalLengthBp;
        public bool IsCircular;
        public bool HasAntibioticResistance;
        public bool IsLacZDisrupted; // true -> khuẩn lạc trắng trên X-gal; false -> khuẩn lạc xanh
        public double[] FragmentLengthsBp = Array.Empty<double>();
        public string Summary = "";
    }

    public static class DnaSplicing
    {
        public static readonly Dictionary<string, (string recognition, string overhang)> Enzymes =
            new Dictionary<string, (string recognition, string overhang)>(StringComparer.OrdinalIgnoreCase)
            {
                ["EcoRI"] = ("5'-G^AATTC-3'", "AATT"),
                ["BamHI"] = ("5'-G^GATCC-3'", "GATC"),
                ["HindIII"] = ("5'-A^AGCTT-3'", "AGCT"),
                ["SmaI"] = ("5'-CCC^GGG-3'", "") // đầu bằng (blunt end)
            };

        public static StickyEnd GetStickyEnd(string enzymeName)
        {
            if (string.IsNullOrEmpty(enzymeName) || !Enzymes.TryGetValue(enzymeName, out var spec))
                return null;
            return new StickyEnd { EnzymeName = enzymeName, Overhang5To3 = spec.overhang };
        }

        public static DnaFragment CreateVector(string vectorId)
        {
            if (string.Equals(vectorId, "pBR322", StringComparison.OrdinalIgnoreCase))
            {
                var v = new DnaFragment
                {
                    Id = "pBR322",
                    Name = "Plasmid pBR322",
                    LengthBp = 4361,
                    IsCircular = true,
                    HasAmpR = true,
                    HasTetR = true,
                    HasLacZ = false
                };
                v.AvailableSites.AddRange(new[] { "EcoRI", "BamHI", "HindIII" });
                return v;
            }
            var puc = new DnaFragment
            {
                Id = "pUC19",
                Name = "Plasmid pUC19",
                LengthBp = 2686,
                IsCircular = true,
                HasAmpR = true,
                HasTetR = false,
                HasLacZ = true
            };
            puc.AvailableSites.AddRange(new[] { "EcoRI", "SmaI", "BamHI", "HindIII" });
            return puc;
        }

        public static DnaFragment CreateTargetGene(string geneId)
        {
            if (string.Equals(geneId, "Insulin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(geneId, "gen_insulin", StringComparison.OrdinalIgnoreCase))
            {
                var ins = new DnaFragment
                {
                    Id = "Insulin",
                    Name = "Gen Insulin người (cDNA)",
                    LengthBp = 330,
                    IsCircular = false
                };
                ins.AvailableSites.AddRange(new[] { "EcoRI", "BamHI" });
                return ins;
            }
            if (string.Equals(geneId, "Cry1Ac", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(geneId, "Bt", StringComparison.OrdinalIgnoreCase))
            {
                var bt = new DnaFragment
                {
                    Id = "Cry1Ac",
                    Name = "Gen kháng sâu Bt (Cry1Ac)",
                    LengthBp = 1800,
                    IsCircular = false
                };
                bt.AvailableSites.AddRange(new[] { "BamHI", "HindIII" });
                return bt;
            }
            var gfp = new DnaFragment
            {
                Id = "GFP",
                Name = "Gen GFP (Protein huỳnh quang xanh lục)",
                LengthBp = 720,
                IsCircular = false
            };
            gfp.AvailableSites.AddRange(new[] { "EcoRI", "BamHI", "HindIII" });
            return gfp;
        }

        public static DnaFragment Digest(DnaFragment source, string leftEnzyme, string rightEnzyme = null)
        {
            if (source == null) return null;
            if (string.IsNullOrEmpty(rightEnzyme)) rightEnzyme = leftEnzyme;

            var frag = new DnaFragment
            {
                Id = source.Id,
                Name = source.Name,
                LengthBp = source.LengthBp,
                IsCircular = false,
                HasAmpR = source.HasAmpR,
                HasTetR = source.HasTetR,
                HasLacZ = source.HasLacZ,
                LeftEnzyme = leftEnzyme ?? "",
                RightEnzyme = rightEnzyme ?? "",
                LeftStickyEnd = GetStickyEnd(leftEnzyme),
                RightStickyEnd = GetStickyEnd(rightEnzyme)
            };
            frag.AvailableSites.AddRange(source.AvailableSites);
            return frag;
        }

        public static bool AreEndsCompatible(StickyEnd a, StickyEnd b)
        {
            if (a == null || b == null) return false;
            if (a.IsBlunt && b.IsBlunt) return true;
            if (a.IsBlunt || b.IsBlunt) return false;
            return string.Equals(a.Overhang5To3, b.Overhang5To3, StringComparison.OrdinalIgnoreCase);
        }

        public static RecombinantPlasmid Ligate(DnaFragment digestedVector, DnaFragment digestedInsert, bool hasT4Ligase = true)
        {
            if (digestedVector == null || digestedInsert == null || !hasT4Ligase)
                return null;
            if (digestedVector.IsCircular)
                return null;

            bool forwardMatch = AreEndsCompatible(digestedVector.LeftStickyEnd, digestedInsert.LeftStickyEnd) &&
                                AreEndsCompatible(digestedVector.RightStickyEnd, digestedInsert.RightStickyEnd);
            bool reverseMatch = AreEndsCompatible(digestedVector.LeftStickyEnd, digestedInsert.RightStickyEnd) &&
                                AreEndsCompatible(digestedVector.RightStickyEnd, digestedInsert.LeftStickyEnd);

            if (!forwardMatch && !reverseMatch)
                return null;

            int totalBp = digestedVector.LengthBp + digestedInsert.LengthBp;
            return new RecombinantPlasmid
            {
                Name = $"{digestedVector.Id}-{digestedInsert.Id}",
                VectorName = digestedVector.Name,
                InsertName = digestedInsert.Name,
                VectorLengthBp = digestedVector.LengthBp,
                InsertLengthBp = digestedInsert.LengthBp,
                TotalLengthBp = totalBp,
                IsCircular = true,
                HasAntibioticResistance = digestedVector.HasAmpR || digestedVector.HasTetR,
                IsLacZDisrupted = digestedVector.HasLacZ,
                FragmentLengthsBp = new double[] { digestedInsert.LengthBp, digestedVector.LengthBp, totalBp },
                Summary = $"ADN tái tổ hợp {digestedVector.Id}-{digestedInsert.Id} ({totalBp} bp = vector {digestedVector.LengthBp} bp + gen {digestedInsert.LengthBp} bp)"
            };
        }
    }
}

