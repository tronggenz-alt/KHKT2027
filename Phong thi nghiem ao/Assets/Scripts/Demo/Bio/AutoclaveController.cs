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
    public class AutoclaveController : MonoBehaviour, IInteractable, IMachinePanel
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

        public float sterilizeTempC = 121f;
        public float holdMinutes = 20f;

        /// <summary>Áp suất dư của hơi nước bão hòa (kPa) theo phương trình Antoine cho nước 100–374 °C.</summary>
        public static float SaturatedGaugeKpa(float tempC)
        {
            double mmHg = Math.Pow(10.0, 8.14019 - 1810.94 / (244.485 + tempC));
            return Mathf.Max(0f, (float)(mmHg * 0.133322 - 101.325));
        }

        public MachineSpec PanelSpec()
        {
            var s = new MachineSpec { code = "MÁY · NỒI HẤP", title = "Nồi hấp tiệt trùng đứng", subtitle = "Tiệt trùng bằng hơi nước bão hòa dưới áp suất, có khóa liên động nắp" };
            s.parameters.Add(MachineParam.Slider("Nhiệt độ tiệt trùng", "°C", 105f, 134f, 1f, () => sterilizeTempC, v => sterilizeTempC = v, "0",
                v => $"Áp suất dư tương ứng: {BioFeedback.FormatVi(SaturatedGaugeKpa(v), "0")} kPa" + (v < 121f ? " · dưới 121 °C: không diệt hết bào tử" : v > 126f ? " · chu trình nhanh cho dụng cụ kim loại" : " · chuẩn cho môi trường nuôi cấy")));
            s.parameters.Add(MachineParam.Slider("Thời gian giữ nhiệt", "phút", 5f, 40f, 1f, () => holdMinutes, v => holdMinutes = v, "0",
                v => v < 15f ? "Dưới 15 phút ở 121 °C: chưa đủ để tiệt trùng" : v <= 20f ? "15–20 phút: đủ cho môi trường lỏng thể tích nhỏ" : "Trên 20 phút: dùng cho túi rác sinh học, bình lớn; đường trong môi trường dễ bị caramen hóa"));
            s.parameters.Add(MachineParam.Choice("Mực nước cất", new[] { "Dưới vạch", "Đủ nước" }, () => hasEnoughWater ? 1 : 0, i => hasEnoughWater = i == 1));
            s.runLabel = () => "Chạy chu trình hấp";
            s.status = () => $"Nhiệt độ buồng: {BioFeedback.FormatVi(tempC, "0.0")} °C\nÁp suất dư: {BioFeedback.FormatVi(gaugePressureKpa, "0")} kPa\nNắp: {(lidLocked ? "ĐÃ KHÓA" : "mở được")} · {(cycleCompletedSterile ? "Mẻ gần nhất: đạt tiệt trùng" : "Chưa có mẻ đạt")}";
            s.run = () =>
            {
                if (!hasEnoughWater) { RunSterilizationCycle(); return "Máy không chạy: mực nước cất dưới vạch tối thiểu, thanh đốt sẽ cháy khô. Châm thêm nước cất."; }
                bool sterile = sterilizeTempC >= 121f && holdMinutes >= 15f || sterilizeTempC >= 132f && holdMinutes >= 4f;
                float peak = SaturatedGaugeKpa(sterilizeTempC);
                if (sterile)
                {
                    RunSterilizationCycle();
                    return $"Đạt {BioFeedback.FormatVi(sterilizeTempC, "0")} °C, {BioFeedback.FormatVi(peak, "0")} kPa dư, giữ {BioFeedback.FormatVi(holdMinutes, "0")} phút.\nBăng chỉ thị nhiệt chuyển sọc đen: mẻ hấp ĐẠT tiệt trùng. Đã xả áp về 0 kPa, nồi còn 65 °C.";
                }
                lidLocked = true;
                cycleCompletedSterile = false;
                gaugePressureKpa = 0f;
                tempC = 65f;
                BioFeedback.RecordMistake("noi_hap", "chu_trinh_hap_khong_du",
                    $"Chu trình hấp {BioFeedback.FormatVi(sterilizeTempC, "0")} °C – {BioFeedback.FormatVi(holdMinutes, "0")} phút chưa đủ điều kiện tiệt trùng!",
                    "Hấp ướt cần tối thiểu 121 °C (103 kPa dư) trong 15 phút để diệt bào tử chịu nhiệt như Geobacillus stearothermophilus.");
                return $"Đạt {BioFeedback.FormatVi(sterilizeTempC, "0")} °C, {BioFeedback.FormatVi(peak, "0")} kPa dư, giữ {BioFeedback.FormatVi(holdMinutes, "0")} phút.\nBăng chỉ thị KHÔNG đổi màu hoàn toàn: mẻ hấp CHƯA đạt tiệt trùng. Cần ít nhất 121 °C trong 15 phút.";
            };
            return s;
        }

        public void Use(GameObject user)
        {
            RunSterilizationCycle();
        }
    }
}
