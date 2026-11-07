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
    /// 5.1.16 & 5.4.10. Bể ổn nhiệt (WaterBath, 25..95 °C) — mã: be_on_nhiet
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterBathController : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public float targetTempC = 60.0f;
        public float currentTempC = 60.0f;
        public int tubesInFloatingRack = 0;

        public string UseHint => $"Bể ổn nhiệt (Water Bath) [Đặt: {BioFeedback.FormatVi(targetTempC, "0.0")} °C · Thực: {BioFeedback.FormatVi(currentTempC, "0.0")} °C{(currentTempC >= 60f ? " · Có bọt nhiệt nhỏ" : "")}] · Bấm: ủ mẫu ở {BioFeedback.FormatVi(targetTempC, "0")} °C · Con lăn: chỉnh nhiệt độ";

        public void Use(GameObject user)
        {
            currentTempC = targetTempC;
            tubesInFloatingRack = 0;
            foreach (var v in FindObjectsByType<BioVessel>())
                if ((v.transform.position - transform.position).sqrMagnitude < 0.2f) tubesInFloatingRack++;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("be_on_nhiet", "u_mau_be_on_nhiet",
                $"Đã ủ mẫu trong giá nổi bể ổn nhiệt ở {BioFeedback.FormatVi(currentTempC, "0.0")} °C.");
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            targetTempC = Mathf.Clamp(targetTempC + Mathf.Sign(delta) * 5.0f, 25f, 95f);
            currentTempC = targetTempC;
        }
    }
}
