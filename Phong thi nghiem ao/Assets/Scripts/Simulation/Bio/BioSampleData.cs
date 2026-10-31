// Dữ liệu mẫu sinh học (C# thuần, không phụ thuộc UnityEngine).
using System;
using System.Collections.Generic;

namespace Khkt.Simulation.Bio
{
    [Serializable]
    public struct DnaBand
    {
        public int bp;
        public double ng;
        public bool smear;
    }

    [Serializable]
    public struct Contaminant
    {
        public string organismId;
        public int colonies;
    }

    public sealed class BioSampleData
    {
        public string organismId = "";     // mã chung_vi_sinh, "" nếu không có vi sinh
        public double cfuPerMl;
        public string specimenId = "";     // mã tieu_ban, "" nếu không phải tiêu bản
        public readonly List<DnaBand> dna = new List<DnaBand>();
        public bool sterile = true;
        public readonly List<Contaminant> contaminants = new List<Contaminant>();

        public void CopyFrom(BioSampleData other)
        {
            if (other == null)
            {
                organismId = "";
                cfuPerMl = 0.0;
                specimenId = "";
                dna.Clear();
                sterile = true;
                contaminants.Clear();
                return;
            }
            organismId = other.organismId ?? "";
            cfuPerMl = other.cfuPerMl;
            specimenId = other.specimenId ?? "";
            sterile = other.sterile;
            dna.Clear();
            dna.AddRange(other.dna);
            contaminants.Clear();
            contaminants.AddRange(other.contaminants);
        }

        /// <summary>
        /// Trộn src (srcMl) vào dst (dstMl, thể tích TRƯỚC khi trộn); mật độ, ADN, tạp nhiễm chia theo thể tích.
        /// </summary>
        public static void MixInto(BioSampleData dst, double dstMl, BioSampleData src, double srcMl)
        {
            if (dst == null || src == null) return;
            if (dstMl < 0.0) dstMl = 0.0;
            if (srcMl < 0.0) srcMl = 0.0;
            double totalMl = dstMl + srcMl;
            if (totalMl <= 0.0) return;

            string dstOrg = dst.organismId ?? "";
            string srcOrg = src.organismId ?? "";
            double dstCells = Math.Max(0.0, dst.cfuPerMl) * dstMl;
            double srcCells = Math.Max(0.0, src.cfuPerMl) * srcMl;

            if (string.IsNullOrEmpty(dstOrg) && string.IsNullOrEmpty(srcOrg))
            {
                dst.organismId = "";
                dst.cfuPerMl = 0.0;
            }
            else if (string.IsNullOrEmpty(dstOrg))
            {
                dst.organismId = srcOrg;
                dst.cfuPerMl = srcCells / totalMl;
            }
            else if (string.IsNullOrEmpty(srcOrg))
            {
                dst.organismId = dstOrg;
                dst.cfuPerMl = dstCells / totalMl;
            }
            else if (dstOrg == srcOrg)
            {
                dst.cfuPerMl = (dstCells + srcCells) / totalMl;
            }
            else
            {
                // Khác chủng: chủng có tổng số tế bào lớn hơn giữ làm chủng chính, chủng nhỏ hơn đưa vào contaminants
                if (srcCells > dstCells)
                {
                    if (dstCells > 0.0)
                    {
                        AddOrMergeContaminant(dst.contaminants, dstOrg, (int)Math.Max(1.0, Math.Round(dstCells)));
                    }
                    dst.organismId = srcOrg;
                    dst.cfuPerMl = srcCells / totalMl;
                }
                else
                {
                    if (srcCells > 0.0)
                    {
                        AddOrMergeContaminant(dst.contaminants, srcOrg, (int)Math.Max(1.0, Math.Round(srcCells)));
                    }
                    dst.cfuPerMl = dstCells / totalMl;
                }
            }

            if (string.IsNullOrEmpty(dst.specimenId) && !string.IsNullOrEmpty(src.specimenId))
            {
                dst.specimenId = src.specimenId;
            }

            // Gộp các băng ADN cùng kích thước bp
            for (int i = 0; i < src.dna.Count; i++)
            {
                DnaBand b = src.dna[i];
                int found = -1;
                for (int j = 0; j < dst.dna.Count; j++)
                {
                    if (dst.dna[j].bp == b.bp)
                    {
                        found = j;
                        break;
                    }
                }
                if (found >= 0)
                {
                    DnaBand cur = dst.dna[found];
                    dst.dna[found] = new DnaBand
                    {
                        bp = cur.bp,
                        ng = cur.ng + b.ng,
                        smear = cur.smear || b.smear
                    };
                }
                else
                {
                    dst.dna.Add(b);
                }
            }

            // Gộp danh sách tạp nhiễm từ src
            for (int i = 0; i < src.contaminants.Count; i++)
            {
                Contaminant c = src.contaminants[i];
                if (!string.IsNullOrEmpty(c.organismId) && c.colonies > 0)
                {
                    AddOrMergeContaminant(dst.contaminants, c.organismId, c.colonies);
                }
            }

            dst.sterile = dst.sterile && src.sterile && dst.contaminants.Count == 0 && string.IsNullOrEmpty(dst.organismId);
        }

        private static void AddOrMergeContaminant(List<Contaminant> list, string orgId, int cols)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].organismId == orgId)
                {
                    list[i] = new Contaminant { organismId = orgId, colonies = list[i].colonies + cols };
                    return;
                }
            }
            list.Add(new Contaminant { organismId = orgId, colonies = cols });
        }
    }
}
