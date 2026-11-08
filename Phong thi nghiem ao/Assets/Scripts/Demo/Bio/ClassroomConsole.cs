// Tách từ SlidePrepTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.1.14. Bảng điều khiển lớp học trên bàn giáo viên — mã: bang_dieu_khien_lop
    /// </summary>
    [DisallowMultipleComponent]
    public class ClassroomConsole : MonoBehaviour, IInteractable
    {
        public int currentExperimentIndex = 0;
        public bool roomLightsOn = true;
        public ClassroomProgram program = new ClassroomProgram();

        void Awake()
        {
            if (program == null) program = new ClassroomProgram();
            program.console = this;
        }

        public string UseHint
        {
            get
            {
                var exp = GetCurrentExperiment();
                string title = exp != null ? $"{exp.ma} – {exp.ten}" : "te_bao_hanh";
                return $"Bảng điều khiển lớp học [Bài đang phát cho 9 bàn: {title}] · Bấm: chuyển bài thí nghiệm cho cả lớp";
            }
        }

        public BioExperimentEntry GetCurrentExperiment()
        {
            BioCatalogLoader.EnsureLoaded();
            if (BioCatalogLoader.Experiments.Count == 0) return null;
            return BioCatalogLoader.Experiments[currentExperimentIndex % BioCatalogLoader.Experiments.Count];
        }

        public void Use(GameObject user)
        {
            BioCatalogLoader.EnsureLoaded();
            if (BioCatalogLoader.Experiments.Count > 0)
            {
                currentExperimentIndex = (currentExperimentIndex + 1) % BioCatalogLoader.Experiments.Count;
            }
            var exp = GetCurrentExperiment();
            string code = exp != null ? exp.ma : "te_bao_hanh";
            string name = exp != null ? exp.ten : "Quan sát tế bào biểu bì vảy hành";

            // Đồng bộ mọi màn hình bàn học sinh (BioBenchScreenController)
            var screens = FindObjectsByType<BioBenchScreenController>(FindObjectsSortMode.None);
            for (int i = 0; i < screens.Length; i++)
            {
                screens[i].SetExperiment(code);
            }

            if (BioLabProcedure.Instance != null)
            {
                bool isMol = code.Contains("pcr") || code.Contains("adn") || code.Contains("di_truyen");
                BioLabProcedure.Instance.SetScenario(
                    isMol ? BioLabProcedure.Scenario.DnaSplicingGfp : BioLabProcedure.Scenario.OnionPlasmolysis,
                    false);
            }

            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("bang_dieu_khien_lop", "chon_bai_thi_nghiem",
                $"Giáo viên phát bài thí nghiệm '{name}' ({code}) xuống toàn bộ {screens.Length} màn hình bàn học sinh.");
        }
    }
}
