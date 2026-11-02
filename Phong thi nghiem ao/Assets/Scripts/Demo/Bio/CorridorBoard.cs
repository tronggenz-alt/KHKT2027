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
    /// 5.6. Màn hình bảng tin hành lang tầng 2 (Lịch phòng hôm nay, Cảnh báo tủ lạnh & sự cố) — mã: man_hinh_bang_tin
    /// </summary>
    [DisallowMultipleComponent]
    public class CorridorBoard : MonoBehaviour, IInteractable
    {
        public int pageIndex = 0;
        static readonly string[] PageTitles =
        {
            "TRANG 1/3: NỘI QUY AN TOÀN SINH HỌC CẤP 1 (BSL-1)",
            "TRANG 2/3: LỊCH ĐẶT PHÒNG & THIẾT BỊ HÔM NAY (Đồng bộ Web /sinh)",
            "TRANG 3/3: TRẠNG THÁI NHIỆT ĐỘ TỦ (4,0 °C / −20,0 °C) & SỰ CỐ AN TOÀN"
        };
        public TextMesh boardTextMesh;

        public string UseHint => $"Màn hình bảng tin hành lang tầng 2 [{PageTitles[pageIndex]}] · Bấm để lật trang";

        public void Use(GameObject user)
        {
            pageIndex = (pageIndex + 1) % PageTitles.Length;
            if (boardTextMesh != null) boardTextMesh.text = PageTitles[pageIndex];
            BioFeedback.PlayClick();
            BioFeedback.LogAction("man_hinh_bang_tin", "lat_trang_bang_tin", $"Bảng tin hành lang tầng 2: {PageTitles[pageIndex]}.");
        }
    }
}
