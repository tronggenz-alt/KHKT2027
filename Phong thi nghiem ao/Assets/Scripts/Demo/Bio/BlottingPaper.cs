// Tách từ SlidePrepTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.1.6. Giấy thấm — mã: giay_tham
    /// </summary>
    [DisallowMultipleComponent]
    public class BlottingPaper : MonoBehaviour, IInteractable
    {
        public BioTool bioTool;
        public bool isUsedWet = false;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "giay_tham";
            bioTool.toolDisplayName = "Giấy thấm";
            bioTool.kind = BioToolKind.BlottingPaper;
        }

        public string UseHint => isUsedWet
            ? "Giấy thấm (đã thấm ướt dịch thừa) · Bỏ vào thùng rác"
            : "Giấy thấm · Đặt ở mép lamen để rút dịch thừa / kéo dung dịch muối hoặc nước cất qua tiêu bản";

        public void Use(GameObject user)
        {
            isUsedWet = true;
            var slide = CoverSlip.FindNearestSlide(transform.position, 0.5f);
            if (slide != null)
            {
                slide.OnToolInteractComplete(bioTool);
            }
            else
            {
                BioFeedback.PlayClick();
                BioFeedback.LogAction("giay_tham", "tham_dich_lamen", "Đã dùng giấy thấm kéo dung dịch qua mép lamen.");
            }
        }
    }
}
