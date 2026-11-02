// Tách từ MolecularTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    [DisallowMultipleComponent]
    public class ReagentTube : MonoBehaviour, IInteractable, IBioTarget
    {
        public string reagentCode = MolCodes.Water;
        public float stockUl = 500f;
        [Tooltip("Vết chất lạ lọt vào ống gốc do dùng lại đầu côn bẩn")]
        public List<string> contaminants = new List<string>();

        public string TargetName => MolCodes.Name(reagentCode);
        public string UseHint => $"{TargetName} (còn {BioFeedback.FormatVi(stockUl, "0")} µL{(contaminants.Count > 0 ? " · ĐÃ NHIỄM" : "")}) · Cầm micropipet đã gắn đầu côn rồi bấm vào ống để hút";

        public void Use(GameObject user) => MolUi.Say(UseHint);

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            var p = MolUi.Pipette(tool);
            if (p == null) return false;
            prompt = !p.tipAttached ? "Chưa gắn đầu côn" : p.load.IsEmpty ? $"Hút {p.VolumeText} {TargetName}" : "Đầu côn đang có dịch: nhả vào ống phản ứng trước";
            return true;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }
        public void OnToolApplyComplete(BioTool tool) => MolUi.Pipette(tool)?.AspirateStock(this);

        /// <summary>Hỗn hợp của một lần hút v µL từ ống gốc (kèm vết nhiễm nếu có).</summary>
        public void Draw(MolMix into, float v)
        {
            float take = Mathf.Min(v, stockUl);
            into.Add(reagentCode, take);
            foreach (var c in contaminants) into.Add(c, take * 0.002f);  // vết rất nhỏ nhưng PCR vẫn nhân lên được
            if (reagentCode == MolCodes.Ladder)
                foreach (int bp in MolCodes.LadderBp)
                    into.AddBand(new BioDnaBandInfo { bp = bp, ng = take * MolCodes.LadderNgPerBandPerUl * (bp == 500 ? 2 : 1) });
            stockUl -= take;
        }
    }
}
