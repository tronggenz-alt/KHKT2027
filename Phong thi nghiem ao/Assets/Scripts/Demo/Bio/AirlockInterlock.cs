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
    /// 5.2.12. Buồng đệm (Airlock) 2 cửa khóa liên động + kiểm tra đồ bảo hộ PPE — mã: buong_dem
    /// </summary>
    [DisallowMultipleComponent]
    public class AirlockInterlock : MonoBehaviour, IInteractable
    {
        public AutoDoor outerDoor;
        public AutoDoor innerDoor;
        public bool outerOpen = false;
        public bool innerOpen = false;

        void Start()
        {
            if (outerDoor != null && innerDoor != null)
            {
                outerDoor.interlockWith = innerDoor;
                innerDoor.interlockWith = outerDoor;
            }
        }

        public string UseHint => $"Buồng đệm vô trùng (Airlock) [Cửa ngoài: {(outerOpen ? "MỞ" : "ĐÓNG")} · Cửa trong: {(innerOpen ? "MỞ" : "ĐÓNG")} · PPE: {(BioTransfer.PpeEquipped ? "Đã mặc đủ" : "Chưa mặc!")}] · Không bao giờ mở 2 cửa cùng lúc";

        public bool TryOpenInnerDoor()
        {
            if (outerOpen || (outerDoor != null && outerDoor.IsCurrentlyOpen))
            {
                BioFeedback.RecordMistake("buong_dem", "mo_2_cua_buong_dem_cung_luc",
                    "Cố mở cửa trong buồng đệm khi cửa ngoài hành lang chưa đóng kín!",
                    "Khóa liên động chéo (Interlock) đã chặn cửa! Nếu hai cửa mở cùng lúc, luồng khí hành lang sẽ tràn thẳng vào phòng sạch vi sinh.");
                return false;
            }
            if (!BioTransfer.PpeEquipped)
            {
                BioFeedback.ShowToast("Nhắc nhở buồng đệm: Hãy mặc áo blouse, đeo găng tay và kính bảo hộ trước khi vào phòng vi sinh!", true);
            }
            innerOpen = true;
            outerOpen = false;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("buong_dem", "qua_buong_dem_dung_chuan", "Đã đóng kín cửa ngoài trước khi mở cửa trong vào phòng vi sinh (đúng khóa liên động Airlock).");
            return true;
        }

        public void Use(GameObject user)
        {
            outerOpen = false;
            TryOpenInnerDoor();
        }
    }
}
