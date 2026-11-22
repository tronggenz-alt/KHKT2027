using UnityEngine;

namespace Khkt.Interaction.Bio
{
    public enum BioToolKind
    {
        Generic = 0,
        Forceps,          // Nhíp sinh học (nhip_sinh_hoc)
        Scalpel,          // Dao mổ sinh học (dao_mo_sinh_hoc)
        MountingNeedle,   // Kim mũi mác (kim_mui_mac)
        DissectingNeedle = MountingNeedle,
        BlottingPaper,    // Giấy thấm (giay_tham_sinh_hoc)
        StainDropper,     // Lọ thuốc nhuộm I2 / Xanh methylen / Orcein / Sudan III
        Dropper = StainDropper,
        StainBottle = StainDropper,
        ImmersionOil,     // Lọ dầu soi 100x (lo_dau_soi_immersion)
        ImmersionOilBottle = ImmersionOil,
        InoculationLoop,  // Que cấy vi sinh (que_cay_vi_sinh)
        InoculatingLoop = InoculationLoop,
        Micropipette,     // Micropipet P20 / P200 / P1000 (micropipet_bo_3_cay)
        WateringCan,      // Bình tưới cây (binh_tuoi_cay_nha_kinh)
        WashBottle = WateringCan,
        PhEcMeter,        // Bút đo pH/EC thủy canh (but_do_ph_ec_thuy_canh)
        ReflexHammer,     // Búa phản xạ thần kinh (bua_phan_xa_than_kinh)
        Stethoscope,      // Ống nghe huyết áp (may_do_huyet_ap_ong_nghe)
        TestTubeBrush,    // Chổi rửa ống nghiệm (choi_rua_ong_nghiem)
        GlowingSplint,    // Tàn đóm đỏ thử khí O2 (bo_thuoc_thu_sinh_hoa)
        CobaltPaper,      // Giấy tẩm CoCl2 thử thoát hơi nước (chau_cay_boc_tui_nilon)
        ReagentBottle,    // Lọ NaHCO3 2%, Benedict, Biuret, H2O2, Amylase...
        SlideSample,      // Lam kính / tiêu bản mang mẫu
        EppendorfTube,    // Ống Eppendorf 1,5 mL / ống PCR 0,2 mL
        PetriDish,        // Đĩa Petri thạch dinh dưỡng
        Coverslip,        // Lá kính đậy (lamen)
        LensPaper         // Giấy lau thấu kính / thị kính / vật kính
    }

    /// <summary>
    /// Thành phần gắn trên các dụng cụ sinh học cầm tay (kết hợp cùng Grabbable).
    /// Lưu trạng thái chuyên biệt của dụng cụ: nhiệt độ đầu que cấy, độ vô trùng, mã hóa chất/mẫu đang mang.
    /// </summary>
    public class BioTool : MonoBehaviour
    {
        public string toolId = "";
        public string displayName = "Dụng cụ sinh học";
        public string toolDisplayName { get => displayName; set => displayName = value; }
        public float holdDurationSeconds = 1.0f;
        public BioToolKind kind = BioToolKind.Generic;

        [Tooltip("Mã thuốc nhuộm / hóa chất / mẫu sinh học đang mang trên đầu dụng cụ")]
        public string reagentOrSampleId = "";
        public string reagentDisplayName = "";
        public float carriedVolumeUl = 0f;

        [Header("Trạng thái vô trùng & nhiệt độ (dành cho que cấy / đầu côn)")]
        public Transform ToolTip;
        public float temperatureC = 25f;
        public bool isSterile = false;
        public string contaminatedBy = "";
        public bool hasTip = true;
        public Renderer tipGlowRenderer;

        public bool sterile { get => isSterile; set => isSterile = value; }
        public Transform EffectiveToolTip => ToolTip != null ? ToolTip : transform;

        MaterialPropertyBlock mpb;
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public bool IsRedHot => temperatureC >= 450f;
        public bool IsTooHotForSample => temperatureC > 45f;

        void Update()
        {
            if (temperatureC > 25.05f)
            {
                // Nguội dần theo định luật làm lạnh Newton về nhiệt độ phòng 25 °C
                float coolRate = temperatureC > 200f ? 140f : 28f;
                temperatureC = Mathf.MoveTowards(temperatureC, 25f, coolRate * Time.deltaTime);
                UpdateTipGlow();
            }
        }

        public void HeatTo(float targetTempC)
        {
            temperatureC = Mathf.Max(temperatureC, targetTempC);
            if (temperatureC >= 650f)
            {
                isSterile = true;
                reagentOrSampleId = "";
                reagentDisplayName = "";
            }
            UpdateTipGlow();
        }

        void UpdateTipGlow()
        {
            if (tipGlowRenderer == null) return;
            mpb ??= new MaterialPropertyBlock();
            tipGlowRenderer.GetPropertyBlock(mpb);
            float t = Mathf.InverseLerp(250f, 800f, temperatureC);
            Color glow = Color.Lerp(Color.black, new Color(2.4f, 0.55f, 0.08f), t);
            mpb.SetColor(EmissionColorId, glow);
            tipGlowRenderer.SetPropertyBlock(mpb);
        }

        public string GetStatusSummary()
        {
            if (kind == BioToolKind.InoculationLoop)
            {
                string st = isSterile ? "Vô trùng" : "Chưa khử trùng";
                string sample = string.IsNullOrEmpty(reagentDisplayName) ? "Trống" : reagentDisplayName;
                return $"{displayName} [{BioFeedback.FormatVi(temperatureC, "0")} °C · {st} · Mẫu: {sample}]";
            }
            if (!string.IsNullOrEmpty(reagentDisplayName))
                return $"{displayName} ({reagentDisplayName})";
            return displayName;
        }
    }
}
