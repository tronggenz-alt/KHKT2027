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
    /// 5.1.7. Đĩa đồng hồ thủy tinh lõm Ø 75 mm — mã: dia_dong_ho
    /// </summary>
    [DisallowMultipleComponent]
    public class WatchGlass : MonoBehaviour, IInteractable
    {
        public BioSample sample;
        public float soakMolarity = 0.5f; // Dung dịch muối ngâm biểu bì

        void Awake()
        {
            sample = GetComponent<BioSample>();
            if (sample == null) sample = gameObject.AddComponent<BioSample>();
        }

        public string UseHint => sample != null && !sample.IsEmpty
            ? $"Đĩa đồng hồ Ø 75 mm [Đang ngâm: {sample.sampleName} trong NaCl {BioFeedback.FormatVi(soakMolarity, "0.0")} M]"
            : "Đĩa đồng hồ thủy tinh Ø 75 mm (trống) · Đặt biểu bì đã bóc vào để ngâm dung dịch muối";

        public void Use(GameObject user)
        {
            // Trước đây bấm vào đĩa trống là tự có biểu bì thài lài tía: nay chỉ báo đúng thứ đang có trong đĩa.
            if (sample == null || sample.IsEmpty)
            {
                BioFeedback.ShowToast("Đĩa đồng hồ đang trống. Dùng kẹp bóc biểu bì từ khay mẫu đã rạch rồi đặt vào đĩa.");
                return;
            }
            BioFeedback.ShowToast($"Đĩa đồng hồ: {sample.sampleName} đang ngâm NaCl {BioFeedback.FormatVi(soakMolarity, "0.0")} M.");
        }
    }
}
