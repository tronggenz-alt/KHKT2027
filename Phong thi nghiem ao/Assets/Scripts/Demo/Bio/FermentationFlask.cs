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
    /// 5.1.17. Bình lên men rượu (bình tam giác 250 mL + bóng bay chụp miệng) — mã: binh_len_men
    /// Gọi trực tiếp Fermentation.Co2MolesAt và Fermentation.BalloonDiameterCm của Backend.
    /// </summary>
    [DisallowMultipleComponent]
    public class FermentationFlask : MonoBehaviour, IInteractable
    {
        public double glucoseGrams = 10.0;
        public double yeastGrams = 2.0;
        public double waterTempC = 32.0; // Nếu đun sôi >= 60 °C thì men chết!
        public double elapsedHours = 0.0;
        public bool balloonAttached = true;

        public double Co2Moles => Fermentation.Co2MolesAt(elapsedHours, waterTempC, glucoseGrams, waterTempC < 60.0 && yeastGrams > 0.0);
        public double Co2Liters => Fermentation.BalloonVolumeLiters(Co2Moles, waterTempC);
        public double BalloonDiameterCm => Fermentation.BalloonDiameterCm(Co2Liters);

        public Transform balloonVisual;

        public string UseHint => $"Bình lên men rượu 250 mL [Đường: {BioFeedback.FormatVi((float)glucoseGrams, "0.0")} g · Men: {BioFeedback.FormatVi((float)yeastGrams, "0.0")} g · T = {BioFeedback.FormatVi((float)waterTempC, "0")} °C · Đã ủ {BioFeedback.FormatVi((float)elapsedHours, "0.0")} h → Bóng bay Ø {BioFeedback.FormatVi((float)BalloonDiameterCm, "0.1")} cm] · Bấm: ủ tua 2 giờ";

        public void SetupCondition(double sugarG, double yeastG, double tempC)
        {
            glucoseGrams = sugarG;
            yeastGrams = yeastG;
            waterTempC = tempC;
            elapsedHours = 0.0;
            if (tempC >= 55.0)
            {
                BioFeedback.RecordMistake("binh_len_men", "pha_men_bang_nuoc_soi",
                    $"Pha nấm men S. cerevisiae bằng nước quá nóng ({BioFeedback.FormatVi((float)tempC, "0")} °C ≥ 55 °C) làm chết tế bào nấm men!",
                    "Enzyme zymase và màng tế bào nấm men bị biến tính nhiệt nên không sinh ra khí CO₂, bóng bay không phồng.");
            }
            UpdateBalloonScale();
        }

        public void AdvanceTimeHours(double hours)
        {
            elapsedHours += hours;
            UpdateBalloonScale();
            BioFeedback.PlayBubblePop();
            BioFeedback.LogAction("binh_len_men", "len_men_ruou_co2",
                $"Lên men rượu sau {BioFeedback.FormatVi((float)elapsedHours, "0.0")} giờ ở {BioFeedback.FormatVi((float)waterTempC, "0")} °C: n(CO₂) = {BioFeedback.FormatVi((float)Co2Moles, "0.000")} mol ({BioFeedback.FormatVi((float)Co2Liters, "0.00")} L) → Đường kính bóng bay = {BioFeedback.FormatVi((float)BalloonDiameterCm, "0.1")} cm.");
        }

        void UpdateBalloonScale()
        {
            if (balloonVisual == null) return;
            float dMeters = Mathf.Max(0.03f, (float)BalloonDiameterCm * 0.01f);
            balloonVisual.localScale = new Vector3(dMeters, dMeters * 1.15f, dMeters);
        }

        public void Use(GameObject user)
        {
            AdvanceTimeHours(2.0);
        }
    }
}
