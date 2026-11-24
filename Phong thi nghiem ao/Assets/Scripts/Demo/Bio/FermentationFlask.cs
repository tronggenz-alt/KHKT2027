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

        double co2Tich;
        bool menChet;
        float demVe;
        /// <summary>CO₂ tích lũy dần theo nhiệt độ từng lúc (đun ≥ 60 °C men chết hẳn, làm lạnh lại không sống lại).</summary>
        public double Co2Moles => co2Tich;
        public double Co2Liters => Fermentation.BalloonVolumeLiters(Co2Moles, waterTempC);
        public double BalloonDiameterCm => Fermentation.BalloonDiameterCm(Co2Liters);

        public Transform balloonVisual;

        public string UseHint => $"Bình lên men rượu 250 mL [Đường: {BioFeedback.FormatVi((float)glucoseGrams, "0.0")} g · Men: {BioFeedback.FormatVi((float)yeastGrams, "0.0")} g · T = {BioFeedback.FormatVi((float)waterTempC, "0")} °C · Đã ủ {BioFeedback.FormatVi((float)elapsedHours, "0.0")} h → Bóng bay Ø {BioFeedback.FormatVi((float)BalloonDiameterCm, "0.1")} cm] · Đặt vào tủ ấm hoặc bể ổn nhiệt để men hoạt động";

        public void SetupCondition(double sugarG, double yeastG, double tempC)
        {
            glucoseGrams = sugarG;
            yeastGrams = yeastG;
            waterTempC = tempC;
            elapsedHours = 0.0;
            co2Tich = 0.0;
            menChet = tempC >= 60.0;
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
            bool song = !menChet && yeastGrams > 0.0;
            co2Tich += Math.Max(0.0, Fermentation.Co2MolesAt(elapsedHours + hours, waterTempC, glucoseGrams, song) - Fermentation.Co2MolesAt(elapsedHours, waterTempC, glucoseGrams, song));
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

        void Update()
        {
            // Nước trong bình tiến dần về nhiệt độ chỗ đặt bình (bàn, bể ổn nhiệt, tủ ấm, tủ lạnh); thời gian trôi theo chỗ đặt
            float moiTruong = BioGanNhat.NhietDoTai(transform.position);
            waterTempC += (moiTruong - waterTempC) * Mathf.Clamp01(Time.deltaTime * 0.05f);
            if (waterTempC >= 60.0) menChet = true;
            double h = BioGanNhat.GiayMoPhong(transform.position, Time.deltaTime) / 3600.0;
            bool song = !menChet && yeastGrams > 0.0;
            co2Tich += Math.Max(0.0, Fermentation.Co2MolesAt(elapsedHours + h, waterTempC, glucoseGrams, song) - Fermentation.Co2MolesAt(elapsedHours, waterTempC, glucoseGrams, song));
            elapsedHours += h;
            demVe -= Time.deltaTime;
            if (demVe <= 0f) { demVe = 1f; UpdateBalloonScale(); }
        }

        /// <summary>Bấm: quan sát bình. Không tua thời gian (trước đây mỗi lần bấm cộng 2 giờ).</summary>
        public void Use(GameObject user)
        {
            UpdateBalloonScale();
            BioFeedback.ShowToast($"Bình lên men: {BioFeedback.FormatVi((float)waterTempC, "0")} °C, đã {BioFeedback.FormatVi((float)elapsedHours, "0.0")} giờ, bóng Ø {BioFeedback.FormatVi((float)BalloonDiameterCm, "0.1")} cm{(menChet ? " · men đã chết vì nóng" : "")}");
        }
    }
}
