// Tách từ MicrobioTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Rendering;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.2.5. Ống canh thang & Ống thạch nghiêng giữ giống — mã: ong_canh_thang, ong_thach_nghieng
    /// </summary>
    [DisallowMultipleComponent]
    public class BrothTube : MonoBehaviour, IInteractable
    {
        public string tubeKindId = "ong_canh_thang";
        public string organismId = "e_coli_k12";
        public double initialCellsPerMl = 1.0e6;
        public double incubatedHours = 0.0;
        public bool capPlacedOnBenchMistake = false;
        public bool mouthFlamed = true;
        public BioSample sample;

        void Awake()
        {
            sample = GetComponent<BioSample>();
            if (sample == null) sample = gameObject.AddComponent<BioSample>();
            UpdateGrowthState();
        }

        public double CurrentCellsPerMl
        {
            get
            {
                var strain = BioCatalogLoader.GetStrain(organismId);
                double lagH = strain != null ? strain.lag_gio : 1.0;
                double kMax = strain != null && strain.k_toi_da_cfu_ml > 0 ? strain.k_toi_da_cfu_ml : 1.0e9;
                return ColonyGrowth.PopulationAtTime(incubatedHours, initialCellsPerMl, kMax, lagH);
            }
        }

        public double CurrentOd600 => ColonyGrowth.OpticalDensity600(CurrentCellsPerMl);

        public string UseHint => $"Ống nuôi cấy ({tubeKindId}: {organismId}) [Ủ {BioFeedback.FormatVi((float)incubatedHours, "0.0")} h · Mật độ: {CurrentCellsPerMl:E2} CFU/mL · OD600 = {BioFeedback.FormatVi((float)CurrentOd600, "0.00")}] · Bấm: hơ miệng ống & ủ thêm 2 giờ";

        public void SetCapOnBenchMistake()
        {
            capPlacedOnBenchMistake = true;
            if (sample != null)
            {
                sample.isContaminated = true;
                sample.contaminationReason = "Đặt nắp ống giống xuống mặt bàn";
            }
            BioFeedback.RecordMistake(tubeKindId, "dat_nap_ong_nghiem_xuong_ban",
                "Tháo nắp ống canh khuẩn rồi đặt ngửa/úp xuống mặt bàn!",
                "Theo kỹ thuật vô trùng chuẩn, phải kẹp giữ nắp ống bằng ngón út của bàn tay cầm que cấy và hơ miệng ống qua ngọn lửa đèn cồn trước & sau khi lấy mẫu.");
        }

        public void IncubateHours(double hours)
        {
            incubatedHours += hours;
            UpdateGrowthState();
            BioFeedback.PlayClick();
            BioFeedback.LogAction(tubeKindId, "u_ong_canh_khuan",
                $"Ống {tubeKindId} ({organismId}) sau {BioFeedback.FormatVi((float)incubatedHours, "0.0")} giờ: mật độ = {CurrentCellsPerMl:E2} tế bào/mL, độ đục OD600 = {BioFeedback.FormatVi((float)CurrentOd600, "0.00")}.");
        }

        void UpdateGrowthState()
        {
            if (sample == null) return;
            sample.organismId = organismId;
            sample.sampleId = organismId;
            sample.sampleName = $"Canh khuẩn {organismId}";
            sample.cfuPerMl = CurrentCellsPerMl;
            sample.volumeMl = 5.0f;
            sample.SyncToData();
        }

        public void Use(GameObject user)
        {
            IncubateHours(2.0);
        }
    }
}
