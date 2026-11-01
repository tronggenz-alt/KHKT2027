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
    /// 5.1.8. Vòi nước bàn thí nghiệm (tự ngắt sau 20 s) — mã: voi_nuoc_ban
    /// </summary>
    [DisallowMultipleComponent]
    public class BenchTap : MonoBehaviour, IInteractable
    {
        public bool isRunning = false;
        public float autoShutoffTimer = 0f;
        public Transform waterStreamVisual;

        public string UseHint => isRunning
            ? $"Vòi nước bàn [ĐANG CHẢY — tự khóa sau {BioFeedback.FormatVi(20f - autoShutoffTimer, "0")} s] · Bấm: khóa vòi / hứng nước vào cốc"
            : "Vòi nước bàn thí nghiệm · Bấm để mở vòi nước (tự ngắt sau 20 s)";

        public void Use(GameObject user)
        {
            isRunning = !isRunning;
            autoShutoffTimer = 0f;
            if (waterStreamVisual != null) waterStreamVisual.gameObject.SetActive(isRunning);
            enabled = isRunning;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("voi_nuoc_ban", isRunning ? "mo_voi_nuoc" : "khoa_voi_nuoc",
                $"Vòi nước bàn: {(isRunning ? "Mở dòng chảy" : "Đã khóa")}.");
        }

        void Update()
        {
            if (!isRunning) { enabled = false; return; }
            autoShutoffTimer += Time.deltaTime;
            if (autoShutoffTimer >= 20f)
            {
                isRunning = false;
                if (waterStreamVisual != null) waterStreamVisual.gameObject.SetActive(false);
                enabled = false;
            }
        }
    }
}
