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
    /// 5.1.4. Kẹp (panh) mũi nhọn 12 cm — mã: kep_panh
    /// </summary>
    [DisallowMultipleComponent]
    public class Forceps : MonoBehaviour, IInteractable
    {
        public BioTool bioTool;
        public string heldSpecimenId = "";
        public string heldSpecimenName = "";
        public bool peeledTooFast = false;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "kep_panh";
            bioTool.toolDisplayName = "Kẹp (panh) mũi nhọn";
            bioTool.kind = BioToolKind.Forceps;
            bioTool.holdDurationSeconds = 0.8f; // Giữ 0,8 s để bóc lớp biểu bì mỏng 1 lớp tế bào
        }

        public string UseHint => string.IsNullOrEmpty(heldSpecimenId)
            ? "Kẹp (panh) mũi nhọn · Giữ 0,8 s trên vảy hành/lá thài lài để bóc biểu bì mỏng"
            : $"Kẹp đang giữ: {heldSpecimenName}{(peeledTooFast ? " [Dày/rách do bóc vội]" : " [Mỏng 1 lớp chuẩn]")} · Bấm lam kính để đặt";

        public void PeelSpecimen(string specId, string specName, float holdSeconds)
        {
            heldSpecimenId = specId;
            heldSpecimenName = specName;
            peeledTooFast = holdSeconds < 0.75f;
            if (bioTool != null)
            {
                bioTool.reagentOrSampleId = specId;
                bioTool.reagentDisplayName = specName;
            }
            if (peeledTooFast)
            {
                BioFeedback.RecordMistake("kep_panh", "boc_bieu_bi_qua_nhanh",
                    $"Bóc biểu bì quá nhanh ({BioFeedback.FormatVi(holdSeconds, "0.0")} s < 0,8 s) làm biểu bì bị rách và lẫn nhiều lớp mô thịt!",
                    "Khi soi hiển vi, các lớp tế bào chồng lên nhau làm ảnh tối và không thấy rõ từng tế bào. Phải giữ nhẹ nhàng ≥ 0,8 s để tách đúng 1 lớp tế bào biểu bì.");
            }
            else
            {
                BioFeedback.PlaySuccess();
                BioFeedback.LogAction("kep_panh", "boc_bieu_bi_mong", $"Đã bóc lớp biểu bì mỏng '{specName}' (1 lớp tế bào).");
            }
        }

        public void Use(GameObject user)
        {
            var slide = CoverSlip.FindNearestSlide(transform.position, 0.45f);
            if (slide != null && !string.IsNullOrEmpty(heldSpecimenId))
            {
                slide.OnToolInteractComplete(bioTool);
                if (slide.sample != null) slide.sample.isSpreadFlat = !peeledTooFast;
                heldSpecimenId = "";
                heldSpecimenName = "";
            }
            else if (string.IsNullOrEmpty(heldSpecimenId))
            {
                PeelSpecimen("bieu_bi_vay_hanh", "Biểu bì vảy hành tím", 0.9f);
            }
        }
    }
}
