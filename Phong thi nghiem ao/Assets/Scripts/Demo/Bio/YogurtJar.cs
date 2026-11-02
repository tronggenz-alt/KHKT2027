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
    /// 5.1.18. Bộ làm sữa chua (lên men lactic) — mã: bo_lam_sua_chua
    /// Gọi Fermentation.LacticAcidGramsAt của Backend.
    /// </summary>
    [DisallowMultipleComponent]
    public class YogurtJar : MonoBehaviour, IInteractable
    {
        public double lactoseGrams = 12.0; // trong 250 mL sữa
        public double tempC = 42.0;
        public double incubatedHours = 0.0;
        public bool hasStarterCulture = true;
        public bool lidClosed = true;

        public double LacticAcidGrams => hasStarterCulture ? Fermentation.LacticAcidGramsAt(incubatedHours, lactoseGrams, tempC) : 0.0;
        public float CurrentPh
        {
            get
            {
                double g = LacticAcidGrams;
                // Từ pH 6,60 của sữa tươi hạ dần về ~4,45 khi tích lũy axit lactic
                float drop = (float)Math.Min(2.15, (g / Math.Max(1.0, lactoseGrams)) * 2.5);
                return 6.60f - drop;
            }
        }
        public bool IsCoagulated => CurrentPh <= 4.75f;

        public string UseHint => $"Hũ làm sữa chua (L. bulgaricus) [Ủ {BioFeedback.FormatVi((float)tempC, "0")} °C · {BioFeedback.FormatVi((float)incubatedHours, "0.0")} giờ · Axit lactic: {BioFeedback.FormatVi((float)LacticAcidGrams, "0.00")} g · pH = {BioFeedback.FormatVi(CurrentPh, "0.00")} · {(IsCoagulated ? "Sữa đã đông đặc mịn" : "Đang lỏng")}] · Bấm: ủ thêm 3 giờ";

        public void IncubateHours(double hours, double temperatureC = 42.0)
        {
            tempC = temperatureC;
            if (tempC > 55.0)
            {
                BioFeedback.RecordMistake("bo_lam_sua_chua", "u_sua_chua_nuoc_soi",
                    $"Ủ sữa chua ở nhiệt độ quá cao ({BioFeedback.FormatVi((float)tempC, "0")} °C) làm chết vi khuẩn L. bulgaricus!",
                    "Nhiệt độ tối ưu cho vi khuẩn lactic sữa chua là 40 – 45 °C.");
            }
            incubatedHours += hours;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("bo_lam_sua_chua", "u_len_men_lactic",
                $"Sau {BioFeedback.FormatVi((float)incubatedHours, "0.0")} giờ ủ ở {BioFeedback.FormatVi((float)tempC, "0")} °C: sinh ra {BioFeedback.FormatVi((float)LacticAcidGrams, "0.00")} g axit lactic, pH giảm còn {BioFeedback.FormatVi(CurrentPh, "0.00")} ({(IsCoagulated ? "casein kết tủa làm sữa đông đặc" : "chưa đông")}).");
        }

        public void Use(GameObject user)
        {
            IncubateHours(3.0, tempC);
        }
    }
}
