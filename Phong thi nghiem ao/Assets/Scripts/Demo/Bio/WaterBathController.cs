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
    public class WaterBathController : MonoBehaviour, IInteractable, IScrollAdjustable, IMachinePanel
    {
        public float targetTempC = 60.0f;
        public float currentTempC = 60.0f;
        public int tubesInFloatingRack = 0;

        public string UseHint => $"Bể ổn nhiệt (Water Bath) [Đặt: {BioFeedback.FormatVi(targetTempC, "0.0")} °C · Thực: {BioFeedback.FormatVi(currentTempC, "0.0")} °C{(currentTempC >= 60f ? " · Có bọt nhiệt nhỏ" : "")}] · Bấm: ủ mẫu ở {BioFeedback.FormatVi(targetTempC, "0")} °C · Con lăn: chỉnh nhiệt độ";

        [NonSerialized] public float panelMinutes = 10f;

        public MachineSpec PanelSpec()
        {
            var s = new MachineSpec { code = "MÁY · BỂ ỔN NHIỆT", title = "Bể ổn nhiệt", subtitle = "Giữ ống mẫu trong giá nổi ở nhiệt độ đặt" };
            s.parameters.Add(MachineParam.Slider("Nhiệt độ nước", "°C", 25f, 95f, 1f, () => targetTempC, v => { targetTempC = v; currentTempC = v; }, "0",
                v => v <= 37f ? "37 °C: ủ enzyme, nuôi tế bào" : v <= 45f ? "42 °C: sốc nhiệt biến nạp plasmid vào E. coli (45–90 giây)" : v <= 70f ? "55–65 °C: tách chiết ADN, bất hoạt enzyme cắt" : "Trên 90 °C: biến tính protein, có bọt nhiệt, cẩn thận bỏng"));
            s.parameters.Add(MachineParam.Slider("Thời gian ủ", "phút", 1f, 60f, 1f, () => panelMinutes, v => panelMinutes = v, "0"));
            s.runLabel = () => "Ủ mẫu";
            s.status = () => $"Nhiệt độ nước: {BioFeedback.FormatVi(currentTempC, "0.0")} °C\nỐng trong giá nổi: {tubesInFloatingRack}";
            s.run = () =>
            {
                Use(null);
                return tubesInFloatingRack == 0
                    ? $"Bể đã ổn định ở {BioFeedback.FormatVi(currentTempC, "0")} °C. Chưa có ống nào trong giá nổi: đặt ống mẫu cạnh bể rồi chạy lại."
                    : $"Đã ủ {tubesInFloatingRack} ống ở {BioFeedback.FormatVi(currentTempC, "0")} °C trong {BioFeedback.FormatVi(panelMinutes, "0")} phút.";
            };
            return s;
        }

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
