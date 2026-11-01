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
    /// 5.1.21. Tủ sơ cứu y tế — mã: tu_so_cuu
    /// </summary>
    [DisallowMultipleComponent]
    public class FirstAidCabinet : MonoBehaviour, IInteractable
    {
        public bool isOpen = false;
        public BioHinge doorHinge;

        public string UseHint => isOpen
            ? "Tủ sơ cứu y tế [Gạc vô trùng, cồn povidone-iodine, băng cá nhân, nước muối sinh lý 0,9 %] · Bấm: đóng tủ"
            : "Tủ sơ cứu y tế · Bấm để mở tủ lấy băng gạc và dung dịch sát khuẩn vết thương";

        public void Use(GameObject user)
        {
            isOpen = !isOpen;
            if (doorHinge != null) doorHinge.SetOpen(isOpen);
            BioFeedback.PlayClick();
            BioFeedback.LogAction("tu_so_cuu", isOpen ? "mo_tu_so_cuu" : "dong_tu_so_cuu",
                $"Tủ sơ cứu y tế: {(isOpen ? "Đã mở kiểm tra vật tư sơ cứu" : "Đã đóng")}.");
        }
    }
}
