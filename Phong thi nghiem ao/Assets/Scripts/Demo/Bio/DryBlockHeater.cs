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
    public class DryBlockHeater : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public float targetTempC = 65.0f;
        public float currentTempC = 65.0f;

        public string UseHint => $"Khối gia nhiệt khô (Dry Block) [Màn LED đỏ: {BioFeedback.FormatVi(currentTempC, "0.0")} °C] · Bấm: ủ ống Eppendorf 1,5 mL · Con lăn: chỉnh nhiệt độ";

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
