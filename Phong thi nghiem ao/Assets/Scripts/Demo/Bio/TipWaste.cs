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
    public class TipWaste : MonoBehaviour, IInteractable, IBioTarget
    {
        public int count;
        public string TargetName => "Thùng rác đầu côn";
        public string UseHint => $"Thùng rác đầu côn ({count} đầu côn đã thải) · Cầm micropipet rồi bấm vào đây để đẩy đầu côn";
        public void Use(GameObject user) => MolUi.Say(UseHint);
        public void Receive() => count++;

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            var p = MolUi.Pipette(tool);
            if (p == null || !p.tipAttached) return false;
            prompt = "Bấm nút đẩy đầu côn";
            return true;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }
        public void OnToolApplyComplete(BioTool tool) => MolUi.Pipette(tool)?.EjectTip(this);
    }
}
