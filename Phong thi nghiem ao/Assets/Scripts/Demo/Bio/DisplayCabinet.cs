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
    /// 5.6. Tủ trưng bày mẫu vật hành lang (3 ngăn kính) — mã: tu_trung_bay
    /// </summary>
    [DisallowMultipleComponent]
    public class DisplayCabinet : MonoBehaviour, IInteractable
    {
        public bool glassDoorOpen = false;
        public int shelfIndex = 0;
        static readonly string[] ShelfTopics =
        {
            "Ngăn 1: Bộ sưu tập Tiến hóa & Hóa thạch Sinh giới",
            "Ngăn 2: Mô hình cấu trúc Siêu hiển vi Tế bào & Bào quan (Lục lạp, Ti thể)",
            "Ngăn 3: Mẫu tiêu bản Đa dạng Sinh học Việt Nam"
        };
        public BioHinge doorHinge;

        public string UseHint => $"Tủ kính trưng bày hành lang 3,6 m [{ShelfTopics[shelfIndex]} · Cửa kính: {(glassDoorOpen ? "MỞ" : "ĐÓNG")}] · Bấm: mở cửa kính & xem từng ngăn";

        public void Use(GameObject user)
        {
            glassDoorOpen = !glassDoorOpen;
            if (glassDoorOpen) shelfIndex = (shelfIndex + 1) % ShelfTopics.Length;
            if (doorHinge != null) doorHinge.SetOpen(glassDoorOpen);
            BioFeedback.PlayClick();
            BioFeedback.LogAction("tu_trung_bay", "xem_tu_trung_bay", $"Tủ trưng bày hành lang: {ShelfTopics[shelfIndex]}.");
        }
    }
}
