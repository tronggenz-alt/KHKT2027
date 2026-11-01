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
    /// 5.5.5. Hệ thống phun sương làm mát nhà kính (≤ 150 hạt sương) — mã: he_phun_suong
    /// </summary>
    [DisallowMultipleComponent]
    public class MistSystem : MonoBehaviour, IInteractable
    {
        public bool isMisting = false;
        public int activeParticleBudget = 120; // <= 150 hạt theo mục 5.5.5

        public string UseHint => $"Hệ thống phun sương cao áp nhà kính [{(isMisting ? $"ĐANG PHUN ({activeParticleBudget} hạt sương)" : "CHỜ CHU KỲ 90 s")}] · Bấm: bật/tắt phun sương tức thì";

        public void Use(GameObject user)
        {
            isMisting = !isMisting;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("he_phun_suong", isMisting ? "bat_phun_suong" : "tat_phun_suong",
                $"Hệ thống phun sương nhà kính: {(isMisting ? "Đã kích hoạt phun sương hạ nhiệt & giữ ẩm lá" : "Đã ngắt van phun sương")}.");
        }
    }
}
