using Khkt.Simulation.Bio;
using UnityEngine;

namespace Khkt.Interaction.Bio
{
    /// <summary>Cân, máy đo pH để bàn: cầm bình trỏ vào máy là đọc được số của đúng bình đó.</summary>
    [DisallowMultipleComponent]
    public class BioBenchMeter : MonoBehaviour, IBioTarget
    {
        public enum Kind { Balance, PhMeter }
        public Kind kind = Kind.Balance;

        public string TargetName => kind == Kind.Balance ? "Cân phân tích" : "Máy đo pH";

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            if (tool == null || tool.GetComponent<BioVessel>() == null) return false;
            prompt = kind == Kind.Balance ? "Đặt lên cân, đọc khối lượng" : "Nhúng điện cực, đọc pH";
            return true;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }

        public void OnToolApplyComplete(BioTool tool)
        {
            var v = tool != null ? tool.GetComponent<BioVessel>() : null;
            if (v == null) return;
            BioFeedback.PlayClick();
            if (kind == Kind.Balance)
                BioFeedback.ShowToast($"{v.label}: chất bên trong {v.mix.Total:0.00} g (đã trừ bì, coi dung dịch 1 g/mL)");
            else
                BioFeedback.ShowToast(v.mix.LiquidMl < 0.01 ? "Không có dung dịch để đo pH" : $"{v.label}: pH {BioRules.Ph(v.mix):0.0}");
        }
    }
}
