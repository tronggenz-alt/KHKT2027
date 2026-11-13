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
    public class MicrowaveOven : MonoBehaviour, IInteractable, IBioTarget, IMachinePanel
    {
        public bool agaroseMelted;
        public float agarosePercent = 1.0f;

        public string TargetName => "Lò vi sóng";
        public string UseHint => agaroseMelted ? $"Bình agarose {BioFeedback.FormatVi(agarosePercent, "0.0")} % đã tan trong suốt (~60 °C). Cầm khay đúc gel rồi bấm vào lò để đổ gel."
            : "Bấm: đun bình agarose 1 % trong đệm TAE (nắp đậy hờ) đến khi tan trong suốt";

        public MachineSpec PanelSpec()
        {
            var s = new MachineSpec { code = "MÁY · LÒ VI SÓNG", title = "Lò vi sóng đun agarose", subtitle = "Đun tan agarose trong đệm TAE để đổ gel điện di" };
            s.parameters.Add(MachineParam.Slider("Nồng độ agarose", "%", 0.5f, 2.5f, 0.1f, () => agarosePercent, v => { if (!agaroseMelted) agarosePercent = v; }, "0.0",
                v => v < 0.8f ? "Gel loãng: tách tốt đoạn ADN lớn (trên 5.000 bp), gel mềm dễ vỡ" : v <= 1.2f ? "Gel 1 %: tách tốt đoạn 500–10.000 bp" : "Gel đặc: tách tốt đoạn nhỏ (dưới 1.000 bp)"));
            s.runLabel = () => agaroseMelted ? "Đun mẻ mới" : "Đun agarose";
            s.status = () => agaroseMelted ? $"Bình agarose {BioFeedback.FormatVi(agarosePercent, "0.0")} % đã tan trong suốt (~60 °C).\nCầm khay đúc gel rồi bấm vào lò để đổ gel." : "Bình agarose chưa đun.";
            s.run = () =>
            {
                agaroseMelted = false;
                Use(null);
                return $"Đã đun agarose {BioFeedback.FormatVi(agarosePercent, "0.0")} % tan trong suốt (đun ngắt quãng, lắc đều, nắp đậy hờ).\nĐể nguội ~60 °C rồi đổ vào khay đúc có cắm lược.";
            };
            return s;
        }

        public void Use(GameObject user)
        {
            if (agaroseMelted) { MolUi.Say(UseHint); return; }
            agaroseMelted = true;
            BioFeedback.PlaySuccess();
            MolUi.Say("Đã đun agarose 1 % tan trong suốt (đun ngắt quãng, lắc đều, nắp đậy hờ để không nổ bình). Để nguội ~60 °C rồi đổ khuôn.");
        }

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            var tray = tool != null ? tool.GetComponent<GelCastingTray>() : null;
            if (tray == null) return false;
            prompt = !agaroseMelted ? "Chưa đun agarose (bấm vào lò bằng tay không)" : tray.gelReady ? "Khay đã có gel" : "Đổ agarose vào khay đúc, cắm lược";
            return true;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0.8f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }

        public void OnToolApplyComplete(BioTool tool)
        {
            var tray = tool != null ? tool.GetComponent<GelCastingTray>() : null;
            if (tray == null || !agaroseMelted || tray.gelReady) return;
            tray.Cast(agarosePercent);
        }
    }
}
