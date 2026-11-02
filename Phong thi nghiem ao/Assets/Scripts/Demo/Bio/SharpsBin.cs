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
    /// 5.1.21. Thùng đựng vật sắc nhọn (hộp cứng màu vàng) — mã: thung_vat_sac
    /// </summary>
    [DisallowMultipleComponent]
    public class SharpsBin : MonoBehaviour, IInteractable
    {
        public int sharpsCount = 0;

        public string UseHint => $"Hộp đựng vật sắc nhọn màu vàng [Đang chứa {sharpsCount} vật sắc (lam kính vỡ, dao lam)] · Bấm: bỏ vật sắc nhọn an toàn";

        public void DisposeSharp(string itemName)
        {
            sharpsCount++;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("thung_vat_sac", "bo_vat_sac_nhon",
                $"Đã bỏ vật sắc nhọn '{itemName}' vào hộp kháng thủng màu vàng (tổng: {sharpsCount}).");
        }

        public void Use(GameObject user)
        {
            DisposeSharp("Lam kính / Lưỡi dao lam đã dùng");
        }
    }
}
