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
    /// 5.7. Đồng hồ bấm giờ để bàn 3 kênh — mã: dong_ho_bam_gio
    /// </summary>
    [DisallowMultipleComponent]
    public class BenchTimer : MonoBehaviour, IInteractable
    {
        public bool isRunning = false;
        public float elapsedSeconds = 0f;

        public string UseHint => $"Đồng hồ bấm giờ để bàn [{(isRunning ? "ĐANG ĐẾM" : "DỪNG")}: {BioFeedback.FormatVi(elapsedSeconds, "0.0")} s] · Bấm: bắt đầu / dừng / đặt lại";

        public void Use(GameObject user)
        {
            string viec;
            if (isRunning) { isRunning = false; viec = $"Dừng ở {BioFeedback.FormatVi(elapsedSeconds, "0.0")} s"; }
            else if (elapsedSeconds > 0f) { elapsedSeconds = 0f; viec = "Đặt lại về 0,0 s"; }
            else { isRunning = true; viec = "Bắt đầu đếm"; }
            BioFeedback.PlayClick();
            BioFeedback.ShowToast("Đồng hồ bấm giờ: " + viec);
            BioFeedback.LogAction("dong_ho_bam_gio", "bam_gio_thi_nghiem", $"Đồng hồ bấm giờ: {viec}.");
        }

        // Đếm theo thời gian thật (trước đây bấm lần đầu nhảy thẳng lên 60,0 s mà không đếm gì)
        void Update()
        {
            if (isRunning) elapsedSeconds += Time.deltaTime;
        }
    }
}
