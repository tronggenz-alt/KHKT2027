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
    /// 5.5.1. Trạm quang hợp rong đuôi chó (Elodea canadensis) — mã: tram_quang_hop
    /// Gọi trực tiếp Photosynthesis.LightIntensityAt, Photosynthesis.RelativeRate và Photosynthesis.BubblesPerMinute.
    /// </summary>
    [DisallowMultipleComponent]
    public class PhotosynthesisRig : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public bool lampOn = true;
        public double lampDistanceCm = 15.0; // Thanh trượt 10..50 cm
        public string lightColorFilter = "trang"; // trang | do | xanh_lam | xanh_luc (xanh lục ít bọt nhất!)
        public bool addedNaHco3 = true;           // Cung cấp CO2 hòa tan
        public double waterTempC = 26.0;
        public double collectedO2Ml = 0.0;

        public double ColorFilterMultiplier =>
            lightColorFilter == "xanh_luc" ? 0.15 :
            (lightColorFilter == "do" ? 0.92 :
            (lightColorFilter == "xanh_lam" ? 0.85 : 1.0));

        public double EffectiveIntensity => lampOn
            ? Photosynthesis.LightIntensityWattsPerM2(40.0 * ColorFilterMultiplier, lampDistanceCm / 100.0) * (addedNaHco3 ? 1.0 : 0.12)
            : 0.0;
        public double EffectiveLux => EffectiveIntensity;

        public double CurrentBubblesPerMin => Photosynthesis.BubbleRatePerMinute(EffectiveIntensity);

        public string UseHint => $"Trạm quang hợp Rong đuôi chó [Đèn: {(lampOn ? $"{BioFeedback.FormatVi((float)lampDistanceCm, "0")} cm · Lọc: {lightColorFilter}" : "TẮT")} · NaHCO₃: {(addedNaHco3 ? "Có (đủ CO₂)" : "Thiếu CO₂!")} → {BioFeedback.FormatVi((float)CurrentBubblesPerMin, "0.1")} bọt O₂/phút · Đã thu {BioFeedback.FormatVi((float)collectedO2Ml, "0.1")} mL O₂] · Bấm: đếm bọt & thu khí · Con lăn: kéo đèn 10–50 cm";

        public void SetConfiguration(double distCm, string colorFilter, bool hasBicarbonate, double tempC = 26.0)
        {
            lampOn = true;
            lampDistanceCm = Math.Clamp(distCm, 10.0, 50.0);
            lightColorFilter = colorFilter;
            addedNaHco3 = hasBicarbonate;
            waterTempC = tempC;

            if (!hasBicarbonate)
            {
                BioFeedback.RecordMistake("tram_quang_hop", "quang_hop_nuoc_cat_thieu_nahco3",
                    "Dùng nước cất đun sôi để nguội mà quên bổ sung NaHCO₃ 0,2 % (nguồn cung cấp CO₂)!",
                    "Khi thiếu CO₂ cho chu trình Calvin, pha sáng cũng bị ức chế nên cành rong đuôi chó hầu như ngừng nhả bọt khí O₂.");
            }
            else if (colorFilter == "xanh_luc")
            {
                BioFeedback.LogAction("tram_quang_hop", "kinh_loc_xanh_luc",
                    $"Kính lọc ánh sáng XANH LỤC (520–560 nm): diệp lục phản xạ ánh sáng xanh lục nên tốc độ nhả bọt giảm mạnh chỉ còn {BioFeedback.FormatVi((float)CurrentBubblesPerMin, "0.1")} bọt/phút.");
            }
        }

        public double RecordOneMinuteCount()
        {
            double bpm = CurrentBubblesPerMin;
            collectedO2Ml += bpm * 0.04;
            BioFeedback.PlayBubblePop();
            BioFeedback.LogAction("tram_quang_hop", "dem_bot_quang_hop",
                $"Ở khoảng cách đèn d = {BioFeedback.FormatVi((float)lampDistanceCm, "0")} cm (I = {BioFeedback.FormatVi((float)EffectiveLux, "0")} lux, lọc {lightColorFilter}): đếm được {BioFeedback.FormatVi((float)bpm, "0.1")} bọt O₂/phút (đã thu {BioFeedback.FormatVi((float)collectedO2Ml, "0.1")} mL O₂).");
            return bpm;
        }

        public void Use(GameObject user)
        {
            RecordOneMinuteCount();
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            lampDistanceCm = Math.Clamp(lampDistanceCm + Math.Sign(delta) * 5.0, 10.0, 50.0);
            RecordOneMinuteCount();
        }
    }
}
