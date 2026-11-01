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
            : "Đĩa đồng hồ thủy tinh Ø 75 mm · Bấm: cho biểu bì thài lài tía vào ngâm dung dịch muối";

        public void Use(GameObject user)
        {
            if (sample == null) return;
            sample.sampleId = "bieu_bi_thai_lai_tia";
            sample.sampleName = "Biểu bì lá thài lài tía";
            sample.volumeMl = 2.0f;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("dia_dong_ho", "ngam_mau_dia_dong_ho",
                $"Đã ngâm mẫu '{sample.sampleName}' trong đĩa đồng hồ chứa dung dịch NaCl {BioFeedback.FormatVi(soakMolarity, "0.0")} M.");
        }
    }
}
