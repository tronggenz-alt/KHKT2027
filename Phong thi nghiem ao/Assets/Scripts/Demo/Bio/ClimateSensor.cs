// Tách từ GreenhouseAndCommonTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.5.5. Trạm cảm biến vi khí hậu nhà kính (Nhiệt độ, Độ ẩm, Cường độ sáng Lux) — mã: cam_bien_vi_khi_hau
    /// </summary>
    [DisallowMultipleComponent]
    public class ClimateSensor : MonoBehaviour, IInteractable
    {
        public float airTempC = 26.4f;
        public float relativeHumidityPct = 72.0f;
        public float lightLux = 18500.0f;

        public string UseHint => $"Trạm cảm biến vi khí hậu nhà kính [T = {BioFeedback.FormatVi(airTempC, "0.1")} °C · RH = {BioFeedback.FormatVi(relativeHumidityPct, "0")}% · Ánh sáng = {BioFeedback.FormatVi(lightLux, "0")} lux] · Bấm: đọc & đồng bộ dữ liệu lên web";

        public void Use(GameObject user)
        {
            BioFeedback.PlayClick();
            BioFeedback.LogAction("cam_bien_vi_khi_hau", "doc_vi_khi_hau_nha_kinh",
                $"Cảm biến vi khí hậu nhà kính: Nhiệt độ = {BioFeedback.FormatVi(airTempC, "0.1")} °C, Độ ẩm = {BioFeedback.FormatVi(relativeHumidityPct, "0")}%, Cường độ sáng = {BioFeedback.FormatVi(lightLux, "0")} lux.");
        }
    }
}
