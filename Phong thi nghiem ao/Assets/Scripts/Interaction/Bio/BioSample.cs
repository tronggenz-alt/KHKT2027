using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Simulation.Bio;

namespace Khkt.Interaction.Bio
{
    [Serializable]
    public struct BioDnaBandInfo
    {
        public int bp;
        public double ng;
        public bool smear;
    }

    [Serializable]
    public struct BioContaminantInfo
    {
        public string organismId;
        public int colonies;
    }

    /// <summary>
    /// Dữ liệu và trạng thái vật lý – sinh hóa của một mẫu vật sinh học
    /// (trên lam kính, trong ống nghiệm, ống Eppendorf, ống PCR, đĩa Petri hoặc cốc thí nghiệm).
    /// Bọc BioSampleData của Khkt.Simulation.Bio (mục 4 hợp đồng).
    /// </summary>
    public class BioSample : MonoBehaviour
    {
        public readonly BioSampleData data = new BioSampleData();

        public string sampleId = "";
        public string sampleName = "Mẫu trống";
        public string organismId = "";
        public double cfuPerMl = 0.0;
        public string specimenId = "";
        public bool sterile = true;
        public List<BioDnaBandInfo> dna = new List<BioDnaBandInfo>();
        public List<BioContaminantInfo> contaminants = new List<BioContaminantInfo>();

        public BioSampleData SyncToData()
        {
            data.organismId = organismId ?? "";
            data.cfuPerMl = cfuPerMl;
            data.specimenId = !string.IsNullOrEmpty(specimenId) ? specimenId : (sampleId ?? "");
            data.sterile = sterile && !isContaminated;
            data.dna.Clear();
            for (int i = 0; i < dna.Count; i++)
                data.dna.Add(new DnaBand { bp = dna[i].bp, ng = dna[i].ng, smear = dna[i].smear });
            data.contaminants.Clear();
            for (int i = 0; i < contaminants.Count; i++)
                data.contaminants.Add(new Contaminant { organismId = contaminants[i].organismId, colonies = contaminants[i].colonies });
            return data;
        }

        public void SyncFromData()
        {
            organismId = data.organismId ?? "";
            cfuPerMl = data.cfuPerMl;
            if (!string.IsNullOrEmpty(data.specimenId)) specimenId = data.specimenId;
            sterile = data.sterile;
            dna.Clear();
            for (int i = 0; i < data.dna.Count; i++)
                dna.Add(new BioDnaBandInfo { bp = data.dna[i].bp, ng = data.dna[i].ng, smear = data.dna[i].smear });
            contaminants.Clear();
            for (int i = 0; i < data.contaminants.Count; i++)
                contaminants.Add(new BioContaminantInfo { organismId = data.contaminants[i].organismId, colonies = data.contaminants[i].colonies });
            if (contaminants.Count > 0) isContaminated = true;
        }

        public float volumeMl = 0f;
        public float volumeUl = 0f;
        public float concentrationPct = 1.0f;
        public float temperatureC = 25.0f;
        public float pH = 7.0f;

        [Header("Trạng thái tiêu bản hiển vi")]
        public string stainId = "";
        public string stainName = "";
        public bool isSpreadFlat = false;
        public bool hasCoverslip = false;
        public float coverslipDropAngleDeg = 45f;
        public bool hasAirBubbles = false;
        public bool hasExcessStain = false;
        public bool isBlotted = false;
        public bool hasImmersionOil = false;
        public bool isCoverslipCracked = false;

        [Header("Trạng thái Hóa sinh & Enzyme")]
        public bool hasSubstrate = false;
        public string substrateId = "";
        public bool hasEnzyme = false;
        public string enzymeId = "";
        public string indicatorId = "";
        public float reactionProgress01 = 0f;
        public bool isDenatured = false;
        public float oxygenEvolutionRate = 0f;

        [Header("Trạng thái Vi sinh & Sinh học phân tử")]
        public bool isContaminated = false;
        public string contaminationReason = "";
        public int streakZonesCompleted = 0;
        public float incubationHours = 0f;
        public float colonyCount = 0f;
        public bool isAmplifiedByPcr = false;
        public int pcrCyclesCompleted = 0;
        public int dnaFragmentBp = 500;
        public int[] dnaLadderBp = { 100, 250, 500, 750, 1000 };

        [Header("Hiển thị chất lỏng / màu sắc")]
        public Renderer liquidRenderer;
        public Color liquidColor = new Color(0.82f, 0.90f, 0.96f, 0.45f);

        MaterialPropertyBlock mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public bool IsEmpty => string.IsNullOrEmpty(sampleId) && volumeMl <= 0.001f && volumeUl <= 0.01f && !hasSubstrate && !hasEnzyme;

        public void ClearSample()
        {
            sampleId = "";
            sampleName = "Mẫu trống";
            organismId = "";
            cfuPerMl = 0.0;
            specimenId = "";
            sterile = true;
            dna.Clear();
            contaminants.Clear();
            volumeMl = 0f;
            volumeUl = 0f;
            stainId = "";
            stainName = "";
            isSpreadFlat = false;
            hasCoverslip = false;
            hasAirBubbles = false;
            hasExcessStain = false;
            isBlotted = false;
            hasImmersionOil = false;
            isCoverslipCracked = false;
            hasSubstrate = false;
            substrateId = "";
            hasEnzyme = false;
            enzymeId = "";
            indicatorId = "";
            reactionProgress01 = 0f;
            isDenatured = false;
            oxygenEvolutionRate = 0f;
            isContaminated = false;
            contaminationReason = "";
            streakZonesCompleted = 0;
            incubationHours = 0f;
            colonyCount = 0f;
            isAmplifiedByPcr = false;
            pcrCyclesCompleted = 0;
            UpdateVisualColor();
        }

        public void SetLiquidColor(Color c)
        {
            liquidColor = c;
            UpdateVisualColor();
        }

        public void UpdateVisualColor()
        {
            if (liquidRenderer == null) return;
            bool visible = !IsEmpty || volumeMl > 0.01f || volumeUl > 0.1f;
            liquidRenderer.enabled = visible;
            if (!visible) return;

            mpb ??= new MaterialPropertyBlock();
            liquidRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, liquidColor);
            liquidRenderer.SetPropertyBlock(mpb);
        }

        public string DescribeSummary()
        {
            if (IsEmpty) return "Trống (chưa nạp mẫu)";
            string s = sampleName;
            if (!string.IsNullOrEmpty(stainName)) s += $" · Nhuộm: {stainName}";
            if (hasCoverslip) s += hasAirBubbles ? " · Lamen (có bọt khí!)" : " · Lamen chuẩn 45°";
            if (isCoverslipCracked) s += " · [VỠ LAMEN]";
            if (isDenatured) s += " · [Enzyme đã biến tính nhiệt]";
            if (isContaminated) s += $" · [Tạp nhiễm: {contaminationReason}]";
            if (streakZonesCompleted > 0) s += $" · Đã ria {streakZonesCompleted}/4 vùng";
            if (isAmplifiedByPcr) s += $" · Sau PCR ({pcrCyclesCompleted} chu kỳ, {dnaFragmentBp} bp)";
            return s;
        }
    }
}
