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

        public string UseHint => $"Ống nuôi cấy ({tubeKindId}: {organismId}) [Ủ {BioFeedback.FormatVi((float)incubatedHours, "0.0")} h · Mật độ: {CurrentCellsPerMl:E2} CFU/mL · OD600 = {BioFeedback.FormatVi((float)CurrentOd600, "0.00")}] · Đặt vào tủ ấm 37 °C để nuôi";

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

        /// <summary>Hệ số sinh trưởng theo nhiệt độ của vi khuẩn ưa ấm (E. coli): tối ưu ≈ 37 °C, dưới 8 °C hoặc trên 46 °C không mọc.</summary>
        public static double HeSoNhiet(float t)
        {
            if (t < 8f || t > 46f) return 0.0;
            double x = (t - 37.0) / 13.0;
            return Math.Max(0.0, 1.0 - x * x);
        }

        float demGhi;
        public bool daChetVinhVien;

        void Update()
        {
            float t = BioGanNhat.NhietDoTai(transform.position);
            if (t >= 60f && !daChetVinhVien) { daChetVinhVien = true; initialCellsPerMl = 0.0; incubatedHours = 0.0; }
            if (daChetVinhVien) return;
            incubatedHours += BioGanNhat.GiayMoPhong(transform.position, Time.deltaTime) / 3600.0 * HeSoNhiet(t);
            demGhi -= Time.deltaTime;
            if (demGhi <= 0f) { demGhi = 1f; UpdateGrowthState(); }
        }

        /// <summary>Bấm: xem độ đục. Không tua thời gian (trước đây mỗi lần bấm cộng 2 giờ ở mọi nhiệt độ).</summary>
        public void Use(GameObject user)
        {
            UpdateGrowthState();
            float t = BioGanNhat.NhietDoTai(transform.position);
            BioFeedback.ShowToast(daChetVinhVien ? "Ống canh khuẩn đã bị đun nóng ≥ 60 °C: vi khuẩn chết, canh trong, không mọc lại."
                : $"Ống canh khuẩn ở {BioFeedback.FormatVi(t, "0")} °C: {CurrentCellsPerMl:E2} tế bào/mL, OD600 = {BioFeedback.FormatVi((float)CurrentOd600, "0.00")}{(HeSoNhiet(t) <= 0.0 ? " · nhiệt độ này vi khuẩn không mọc" : "")}");
        }
    }
}
