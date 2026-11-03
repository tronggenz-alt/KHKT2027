using Khkt.Simulation.Bio;
using UnityEngine;

namespace Khkt.Interaction.Bio
{
    /// <summary>Bồn rửa: đổ bỏ dung dịch trong bình, xả ống nhỏ giọt đang cầm. Canh khuẩn còn sống thì không được đổ.</summary>
    [DisallowMultipleComponent]
    public class BioDrain : MonoBehaviour, IBioTarget
    {
        public string TargetName => "Bồn rửa";

        static bool Hazard(BioVessel v) => BioRules.HasBacteria(v.mix) && !v.mix.bacteriaDead;

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            if (tool == null) return false;
            var v = tool.GetComponent<BioVessel>();
            if (v != null && !v.isStock)
            {
                prompt = v.mix.IsEmpty ? $"{v.label} đang trống" : Hazard(v) ? "Vi sinh sống phải hấp hủy, không đổ vào bồn" : $"Đổ bỏ {v.label}";
                return true;
            }
            var d = tool.GetComponent<BioDropper>();
            if (d != null) { prompt = d.IsEmpty ? "Ống nhỏ giọt đang trống" : "Xả sạch ống nhỏ giọt"; return true; }
            return false;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }

        public void OnToolApplyComplete(BioTool tool)
        {
            if (tool == null) return;
            var v = tool.GetComponent<BioVessel>();
            if (v != null && !v.isStock)
            {
                if (Hazard(v))
                {
                    BioFeedback.RecordMistake("quay_rua_tay", "do_vi_sinh_vao_bon", "Đổ canh khuẩn sống vào bồn rửa",
                        "Vi sinh vật phải cho vào túi hấp hủy, hấp 121 °C trước khi bỏ.");
                    return;
                }
                v.Discard();
                BioFeedback.PlayClick();
            }
            tool.GetComponent<BioDropper>()?.Clear();
        }
    }
}
