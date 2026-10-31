using UnityEngine;

namespace Khkt.Interaction.Bio
{
    /// <summary>
    /// Dữ liệu và trạng thái vật lý – sinh hóa của một mẫu vật sinh học
    /// (trên lam kính, trong ống nghiệm, ống Eppendorf, ống PCR, đĩa Petri hoặc cốc thí nghiệm).
    /// </summary>
    public class BioSample : MonoBehaviour
    {
        public string sampleId = "";
        public string sampleName = "Mẫu trống";
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
