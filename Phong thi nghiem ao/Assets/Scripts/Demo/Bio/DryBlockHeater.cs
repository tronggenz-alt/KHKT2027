// Tách từ GeneralBioTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.4.10. Khối gia nhiệt khô (Dry Block Heater, cho ống Eppendorf) — mã: khoi_gia_nhiet_kho
    /// </summary>
    [DisallowMultipleComponent]
    public class DryBlockHeater : MonoBehaviour, IInteractable, IScrollAdjustable, IMachinePanel
    {
        public float targetTempC = 65.0f;
        public float currentTempC = 65.0f;

        public string UseHint => $"Khối gia nhiệt khô (Dry Block) [Màn LED đỏ: {BioFeedback.FormatVi(currentTempC, "0.0")} °C] · Bấm: ủ ống Eppendorf 1,5 mL · Con lăn: chỉnh nhiệt độ";

        [NonSerialized] public float panelMinutes = 10f;

        public MachineSpec PanelSpec()
        {
            var s = new MachineSpec { code = "MÁY · KHỐI GIA NHIỆT", title = "Khối gia nhiệt khô", subtitle = "Ủ ống Eppendorf 1,5 mL trong khối nhôm, không cần nước" };
            s.parameters.Add(MachineParam.Slider("Nhiệt độ khối", "°C", 30f, 100f, 1f, () => targetTempC, v => { targetTempC = v; currentTempC = v; }, "0",
                v => v <= 37f ? "37 °C: phản ứng cắt enzyme giới hạn" : v <= 56f ? "56 °C: ủ proteinase K khi tách ADN" : v <= 70f ? "65 °C: bất hoạt enzyme, hòa tan ADN" : "95 °C: biến tính ADN, protein"));
            s.parameters.Add(MachineParam.Slider("Thời gian ủ", "phút", 1f, 60f, 1f, () => panelMinutes, v => panelMinutes = v, "0"));
            s.runLabel = () => "Gia nhiệt";
            s.status = () => $"Màn LED: {BioFeedback.FormatVi(currentTempC, "0.0")} °C";
            s.run = () => { Use(null); return $"Đã ủ ống trên khối nhiệt {BioFeedback.FormatVi(currentTempC, "0")} °C trong {BioFeedback.FormatVi(panelMinutes, "0")} phút."; };
            return s;
        }

        public void Use(GameObject user)
        {
            currentTempC = targetTempC;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("khoi_gia_nhiet_kho", "gia_nhiet_eppendorf",
                $"Đã gia nhiệt ống Eppendorf trên khối nhiệt khô ở {BioFeedback.FormatVi(currentTempC, "0.0")} °C.");
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            targetTempC = Mathf.Clamp(targetTempC + Mathf.Sign(delta) * 5.0f, 30f, 100f);
            currentTempC = targetTempC;
        }
    }
}
