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
    /// 5.5.2. Trạm đo thoát hơi nước bằng áp kế bọt khí (Potometer) — mã: tram_potometer
    /// Gọi trực tiếp Transpiration.BubbleShiftMmPerMin của Backend.
    /// </summary>
    [DisallowMultipleComponent]
    public class PotometerRig : MonoBehaviour, IInteractable
    {
        public double wind01 = 0.0;
        public double light01 = 0.7;
        public double humidity01 = 0.45;
        public bool stomataBlockedWithVaseline = false;
        public bool stemCutInAirEmbolismMistake = false;
        public double bubblePositionMm = 10.0;

        public double CurrentSpeedMmPerMin => stemCutInAirEmbolismMistake
            ? 0.0
            : Transpiration.BubbleShiftMmPerMin(wind01, light01, humidity01, stomataBlockedWithVaseline);

        public string UseHint => $"Áp kế bọt khí Potometer [Gió: {BioFeedback.FormatVi((float)(wind01 * 100), "0")}% · Sáng: {BioFeedback.FormatVi((float)(light01 * 100), "0")}% · Ẩm: {BioFeedback.FormatVi((float)(humidity01 * 100), "0")}% · Khí khổng: {(stomataBlockedWithVaseline ? "Bịt Vaseline" : "Thông thoáng")} → Tốc độ bọt = {BioFeedback.FormatVi((float)CurrentSpeedMmPerMin, "0.2")} mm/phút] · Bấm: đổi điều kiện gió/sáng/ẩm";

        public void SetCutInAirEmbolismMistake()
        {
            stemCutInAirEmbolismMistake = true;
            BioFeedback.RecordMistake("tram_potometer", "cat_canh_cay_ngoai_khong_khi_tac_mach_go",
                "Cắt gốc cành cây ngoài không khí rồi mới cắm vào ống Potometer!",
                "Bọt khí lọt vào mạch gỗ (xylem embolism) làm đứt đoạn cột nước liên tục nên bọt khí chỉ thị đứng yên không dịch chuyển. Phải cắt vát gốc cành ngập hoàn toàn dưới mặt nước.");
        }

        public double MeasureCondition(string modeName, double w01, double l01, double h01, bool vaseline)
        {
            stemCutInAirEmbolismMistake = false;
            wind01 = w01;
            light01 = l01;
            humidity01 = h01;
            stomataBlockedWithVaseline = vaseline;
            double speed = CurrentSpeedMmPerMin;
            bubblePositionMm += speed * 5.0;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("tram_potometer", "do_thoat_hoi_nuoc",
                $"Potometer [{modeName}]: tốc độ dịch chuyển bọt khí = {BioFeedback.FormatVi((float)speed, "0.2")} mm/phút (vạch hiện tại: {BioFeedback.FormatVi((float)bubblePositionMm, "0.1")} mm).");
            return speed;
        }

        public void Use(GameObject user)
        {
            if (wind01 < 0.5)
                MeasureCondition("Bật quạt gió + Đèn sáng", 0.85, 0.9, 0.35, false);
            else if (!stomataBlockedWithVaseline)
                MeasureCondition("Bôi Vaseline mặt dưới lá (bịt khí khổng)", 0.2, 0.7, 0.45, true);
            else
                MeasureCondition("Điều kiện đối chứng chuẩn", 0.0, 0.7, 0.45, false);
        }
    }
}
