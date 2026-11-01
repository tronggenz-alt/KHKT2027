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
    /// 5.1.19. Bộ xương người trên giá có bánh xe — mã: bo_xuong_gia
    /// </summary>
    [DisallowMultipleComponent]
    public class SkeletonStand : MonoBehaviour, IInteractable
    {
        public static readonly string[] BoneNames =
        {
            "Hộp sọ (Cranium)",
            "Cột sống (33–34 đốt sống)",
            "Lồng ngực (12 đôi xương sườn & xương ức)",
            "Xương đùi (Femur — xương dài nhất cơ thể)",
            "Khung chậu (Pelvis)"
        };
        public int index = 0;

        public string UseHint => $"Mô hình bộ xương người 170 cm [Đang trỏ: {BoneNames[index]}] · Bấm để xem giải phẫu từng phần xương";

        public void Use(GameObject user)
        {
            string bone = BoneNames[index];
            index = (index + 1) % BoneNames.Length;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("bo_xuong_gia", "xem_ten_xuong", $"Tra cứu bộ xương: {bone}.");
        }
    }
}
