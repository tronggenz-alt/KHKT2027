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
    /// 5.1.16 & 5.2.6. Nồi hấp tiệt trùng đứng (121 °C, 103 kPa dư, 15–20 phút, khóa liên động áp suất) — mã: noi_hap
    /// Dùng lại AutoclaveProgram / SteamTable tầng 1.
    /// </summary>
    [DisallowMultipleComponent]
    public class AutoclaveController : MonoBehaviour, IInteractable
    {
        public bool hasEnoughWater = true;
        public bool lidLocked = true;
        public bool exhaustValveClosed = true;
        public float tempC = 25.0f;
        public float gaugePressureKpa = 0.0f; // 103 kPa dư ở 121 °C
        public bool cycleCompletedSterile = false;

        public string UseHint => cycleCompletedSterile
            ? $"Nồi hấp tiệt trùng [Đã hoàn tất chu trình 121 °C · 103 kPa · Băng chỉ thị nhiệt đã chuyển sọc đen · Áp suất dư: {BioFeedback.FormatVi(gaugePressureKpa, "0")} kPa]"
            : $"Nồi hấp tiệt trùng đứng [T = {BioFeedback.FormatVi(tempC, "0.0")} °C · P dư = {BioFeedback.FormatVi(gaugePressureKpa, "0.0")} kPa] · Bấm: chạy chu trình tiệt trùng 121 °C – 20 phút";

        public bool TryOpenLidWhilePressurized()
        {
            if (gaugePressureKpa > 5.0f)
            {
                BioFeedback.RecordMistake("noi_hap", "mo_nap_noi_hap_khi_con_ap_suat",
                    $"Cố mở nắp nồi hấp khi áp suất dư còn {BioFeedback.FormatVi(gaugePressureKpa, "0.0")} kPa ({BioFeedback.FormatVi(tempC, "0.0")} °C)!",
                    "Khóa liên động an toàn đã chặn mở nắp! Nếu mở nắp khi còn áp suất hơi nước bão hòa > 100 °C, dịch môi trường sẽ sôi bùng trào nổ gây bỏng nặng. Phải chờ áp suất về 0 kPa rồi mới mở van xả và mở nắp.");
                return false;
            }
            lidLocked = false;
            return true;
        }

        public void RunSterilizationCycle()
        {
            if (!hasEnoughWater)
            {
                BioFeedback.RecordMistake("noi_hap", "chay_noi_hap_thieu_nuoc",
                    "Vận hành nồi hấp khi mực nước cất dưới vạch tối thiểu!",
                    "Thanh đốt điện trở sẽ bị cháy khô và không đủ hơi nước bão hòa đạt 103 kPa.");
                return;
            }
            lidLocked = true;
            exhaustValveClosed = true;
            tempC = 121.0f;
            gaugePressureKpa = 103.0f;
            cycleCompletedSterile = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("noi_hap", "chay_chu_trinh_121C",
                "Nồi hấp đạt 121,0 °C – 103 kPa dư trong 20 phút -> Toàn bộ môi trường và túi hấp hủy đã tiệt trùng (sterile = true), sau đó hạ áp an toàn về 0 kPa.");
            gaugePressureKpa = 0.0f;
            tempC = 65.0f;
        }

        public void Use(GameObject user)
        {
            RunSterilizationCycle();
        }
    }
}
