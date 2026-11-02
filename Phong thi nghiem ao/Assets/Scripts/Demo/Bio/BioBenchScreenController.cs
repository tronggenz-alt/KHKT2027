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
    /// 5.1.9. Màn hình bàn học sinh (BioBenchScreenController) — mã: man_hinh_ban
    /// Đọc dữ liệu từ data/sinh/bai_thi_nghiem.json, lật bước và đánh dấu hoàn thành.
    /// </summary>
    [DisallowMultipleComponent]
    public class BioBenchScreenController : MonoBehaviour, IInteractable
    {
        public string currentExperimentId = "te_bao_hanh";
        public int currentStepIndex = 0;
        public int completedStepsMask = 0;
        public string lastMeasurementSummary = "Chưa có số đo";
        public TextMesh screenTextMesh;

        void Start()
        {
            SetExperiment(currentExperimentId);
        }

        public BioExperimentEntry CurrentExperiment => BioCatalogLoader.GetExperiment(currentExperimentId);

        public string UseHint
        {
            get
            {
                var exp = CurrentExperiment;
                if (exp == null) return $"Màn hình bàn ({currentExperimentId}) · Bấm để chuyển bước";
                int total = exp.cac_buoc != null ? exp.cac_buoc.Length : 1;
                string stepDesc = (exp.cac_buoc != null && exp.cac_buoc.Length > 0)
                    ? exp.cac_buoc[currentStepIndex % exp.cac_buoc.Length]
                    : exp.muc_tieu;
                return $"Màn hình bàn [{exp.ten} · Bước {currentStepIndex + 1}/{total}: {stepDesc}] · Bấm: sang bước tiếp";
            }
        }

        public void SetExperiment(string expId)
        {
            BioCatalogLoader.EnsureLoaded();
            currentExperimentId = expId;
            currentStepIndex = 0;
            completedStepsMask = 0;
            UpdateVisualText();
        }

        public void MarkCurrentStepComplete(string measurementNote = null)
        {
            completedStepsMask |= (1 << currentStepIndex);
            if (!string.IsNullOrEmpty(measurementNote)) lastMeasurementSummary = measurementNote;
            var exp = CurrentExperiment;
            int total = (exp != null && exp.cac_buoc != null && exp.cac_buoc.Length > 0) ? exp.cac_buoc.Length : 1;
            currentStepIndex = (currentStepIndex + 1) % total;
            UpdateVisualText();
        }

        public void Use(GameObject user)
        {
            MarkCurrentStepComplete();
            BioFeedback.PlayClick();
            var exp = CurrentExperiment;
            BioFeedback.LogAction("man_hinh_ban", "lat_buoc_thi_nghiem",
                $"Màn hình bàn ({currentExperimentId}): chuyển sang bước {currentStepIndex + 1}/{(exp?.cac_buoc?.Length ?? 1)}.");
        }

        void UpdateVisualText()
        {
            if (screenTextMesh == null) return;
            var exp = CurrentExperiment;
            if (exp == null)
            {
                screenTextMesh.text = $"BÀI: {currentExperimentId}\nBước {currentStepIndex + 1}";
                return;
            }
            int total = exp.cac_buoc != null ? exp.cac_buoc.Length : 1;
            string stepStr = (exp.cac_buoc != null && exp.cac_buoc.Length > 0)
                ? exp.cac_buoc[currentStepIndex % exp.cac_buoc.Length]
                : exp.muc_tieu;
            screenTextMesh.text = $"LỚP {exp.lop}: {exp.ten}\nBước {currentStepIndex + 1}/{total}: {stepStr}\nKết quả: {lastMeasurementSummary}";
        }
    }
}
