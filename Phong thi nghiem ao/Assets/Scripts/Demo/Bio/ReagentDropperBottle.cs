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
    /// 5.1.5. Lọ thuốc thử nhỏ giọt trên khay bàn (nước cất, xanh methylen 1%, Lugol, NaCl, H2O2 3%) — mã: lo_thuoc_thu_nho_giot
    /// </summary>
    [DisallowMultipleComponent]
    public class ReagentDropperBottle : MonoBehaviour, IInteractable
    {
        public string reagentId = "xanh_methylen";
        public string reagentName = "Xanh methylen 1 %";
        public float molarity = 0f;
        public float dropVolumeMl = 0.05f;
        public int dropsDispensed = 0;
        public BioTool bioTool;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "lo_thuoc_thu_nho_giot";
            bioTool.toolDisplayName = reagentName;
            bioTool.kind = BioToolKind.StainBottle;
            bioTool.reagentOrSampleId = reagentId;
            bioTool.reagentDisplayName = reagentName;
        }

        public string UseHint => $"Lọ nhỏ giọt {reagentName} (0,05 mL/giọt · đã nhỏ {dropsDispensed} giọt) · Bấm: nhỏ 1 giọt lên lam/ống gần nhất";

        public void Use(GameObject user)
        {
            dropsDispensed++;
            var slide = CoverSlip.FindNearestSlide(transform.position, 0.5f);
            if (slide != null)
            {
                slide.AddDrop(reagentId, reagentName, molarity);
            }
            BioFeedback.PlayBubblePop();
            BioFeedback.LogAction("lo_thuoc_thu_nho_giot", $"nho_giot_{reagentId}",
                $"Đã nhỏ 1 giọt ({BioFeedback.FormatVi(dropVolumeMl, "0.00")} mL) {reagentName} (tổng {dropsDispensed} giọt).");
        }
    }
}
