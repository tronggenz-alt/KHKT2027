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
    /// 5.1.7 & 5.4.4. Giá ống nghiệm / Giá ống Eppendorf / Giá ống PCR — mã: gia_ong_nghiem, gia_ong_eppendorf, gia_ong_pcr
    /// </summary>
    [DisallowMultipleComponent]
    public class TubeRackSockets : MonoBehaviour, IInteractable
    {
        public string rackToolId = "gia_ong_nghiem";
        public string acceptedTubeType = "ong_nghiem"; // ong_nghiem | ong_eppendorf | ong_pcr
        public int slotCount = 12;
        public int occupiedCount = 4;

        public string UseHint => $"Giá đỡ ống ({rackToolId}) [{occupiedCount}/{slotCount} lỗ đang cắm ống {acceptedTubeType}] · Đưa ống đúng cỡ tới gần (≤ 8 cm) để hít vào ổ";

        public bool TrySnapTube(string tubeType, out string rejectReason)
        {
            if (!string.IsNullOrEmpty(acceptedTubeType) && tubeType != acceptedTubeType)
            {
                rejectReason = $"Giá này chỉ nhận {acceptedTubeType} (không vừa {tubeType})!";
                BioFeedback.ShowToast(rejectReason, true);
                return false;
            }
            if (occupiedCount >= slotCount)
            {
                rejectReason = "Giá ống đã đầy!";
                return false;
            }
            occupiedCount++;
            rejectReason = "";
            BioFeedback.PlayClick();
            BioFeedback.LogAction(rackToolId, "cam_ong_vao_gia", $"Đã đặt {tubeType} vào lỗ số {occupiedCount}/{slotCount} trên {rackToolId}.");
            return true;
        }

        public void Use(GameObject user)
        {
            BioFeedback.PlayClick();
            BioFeedback.LogAction(rackToolId, "kiem_tra_gia_ong", $"Kiểm tra {rackToolId}: {occupiedCount}/{slotCount} ống đang đặt ngay ngắn.");
        }
    }
}
