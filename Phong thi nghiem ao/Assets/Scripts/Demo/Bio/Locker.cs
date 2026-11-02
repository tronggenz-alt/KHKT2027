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
    /// 5.6. Tủ đồ cá nhân học sinh (cất balô, lấy áo blouse & găng tay trước khi vào phòng phân tử) — mã: tu_do_ca_nhan
    /// </summary>
    [DisallowMultipleComponent]
    public class Locker : MonoBehaviour, IInteractable
    {
        public bool isOpen = false;
        public BioHinge doorHinge;

        public string UseHint => $"Tủ đồ cá nhân (Locker hành lang) [Cửa: {(isOpen ? "MỞ" : "ĐÓNG")} · Đồ bảo hộ PPE: {(BioTransfer.PpeEquipped ? "Đã mặc" : "Chưa mặc")}] · Bấm: cất đồ cá nhân & mặc áo blouse, găng tay";

        public void Use(GameObject user)
        {
            isOpen = !isOpen;
            BioTransfer.PpeEquipped = true;
            if (doorHinge != null) doorHinge.SetOpen(isOpen);
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("tu_do_ca_nhan", "cat_do_mac_ppe_tu_locker",
                "Đã mở tủ đồ cá nhân cất tư trang và mặc áo blouse trắng + găng tay nitrile (daMacBaoHo = true).");
        }
    }
}
