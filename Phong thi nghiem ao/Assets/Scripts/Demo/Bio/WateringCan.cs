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
    /// 5.5.5. Bình tưới cây vòi hoa sen — mã: binh_tuoi
    /// </summary>
    [DisallowMultipleComponent]
    public class WateringCan : MonoBehaviour, IInteractable
    {
        public float waterLiters = 3.5f;
        public float soilMoisturePct = 68.0f;

        public string UseHint => $"Bình tưới cây vòi sen ({BioFeedback.FormatVi(waterLiters, "0.1")} L) [Độ ẩm giá thể: {BioFeedback.FormatVi(soilMoisturePct, "0")}%] · Bấm: tưới ẩm đều mặt chậu";

        public void Use(GameObject user)
        {
            waterLiters = Mathf.Max(0f, waterLiters - 0.3f);
            soilMoisturePct = Mathf.Min(85f, soilMoisturePct + 12f);
            BioFeedback.PlayBubblePop();
            BioFeedback.LogAction("binh_tuoi", "tuoi_am_chau_cay",
                $"Đã dùng bình tưới vòi hoa sen tưới ẩm chậu cây: độ ẩm đất đạt {BioFeedback.FormatVi(soilMoisturePct, "0")}%.");
        }
    }
}
