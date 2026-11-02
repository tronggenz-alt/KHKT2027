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
    /// 5.2.14. Túi hấp hủy sinh học chịu nhiệt (Autoclave Bag) — mã: tui_hap_huy
    /// </summary>
    [DisallowMultipleComponent]
    public class AutoclaveBag : MonoBehaviour, IInteractable
    {
        public int contaminatedPlatesCount = 2;
        public bool indicatorTapeBlackStripes = false; // Đổi sang sọc đen sau khi hấp 121 °C

        public string UseHint => indicatorTapeBlackStripes
            ? $"Túi hấp hủy PP chịu nhiệt 135 °C [Đã hấp tiệt trùng 121 °C — băng chỉ thị đã hiện sọc đen ({contaminatedPlatesCount} đĩa đã bất hoạt)]"
            : $"Túi hấp hủy sinh học màu cam [Đang chứa {contaminatedPlatesCount} đĩa thạch đã nuôi cấy] · Phải đưa vào nồi hấp 121 °C trước khi thải bỏ";

        public void MarkAutoclaved()
        {
            indicatorTapeBlackStripes = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("tui_hap_huy", "hap_huy_rac_vi_sinh",
                "Túi hấp hủy đã qua chu trình nồi hấp 121 °C – 20 phút: băng chỉ thị nhiệt chuyển sọc đen, toàn bộ vi sinh vật đã bị tiêu diệt.");
        }

        public void Use(GameObject user)
        {
            MarkAutoclaved();
        }
    }
}
