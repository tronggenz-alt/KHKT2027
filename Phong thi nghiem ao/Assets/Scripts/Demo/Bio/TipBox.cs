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
    public class TipBox : MonoBehaviour, IInteractable, IBioTarget
    {
        public string tipKind = "vang";   // vang: P10 / P200 · xanh: P1000
        public int remaining = 96;

        public string TargetName => $"Hộp đầu côn {tipKind}";
        public string UseHint => $"Hộp 96 đầu côn {tipKind} vô trùng (còn {remaining}) · Cầm micropipet rồi bấm vào hộp để gắn đầu côn";
        public void Use(GameObject user) => MolUi.Say(UseHint);

        public bool TakeOne()
        {
            if (remaining <= 0) { MolUi.Say("Hộp đầu côn đã hết", true); return false; }
            remaining--;
            return true;
        }

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            var p = MolUi.Pipette(tool);
            if (p == null) return false;
            prompt = p.tipAttached ? "Đã có đầu côn (thải đầu côn cũ trước)" : "Ấn micropipet vào hộp để gắn đầu côn";
            return true;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }
        public void OnToolApplyComplete(BioTool tool) => MolUi.Pipette(tool)?.AttachTip(this);
    }
}
