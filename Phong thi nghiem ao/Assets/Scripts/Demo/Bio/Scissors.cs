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
    /// 5.1.4. Kéo mổ nhỏ — mã: keo_nho
    /// </summary>
    [DisallowMultipleComponent]
    public class Scissors : MonoBehaviour, IInteractable
    {
        public BioTool bioTool;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "keo_nho";
            bioTool.toolDisplayName = "Kéo nhỏ";
            bioTool.kind = BioToolKind.Scalpel;
        }

        public string UseHint => "Kéo mổ nhỏ · Cầm rồi trỏ vào khay mẫu để cắt lá, rong";

        public void Use(GameObject user)
        {
            // Trước đây bấm kéo ghi "đã cắt lá" dù không có mẫu nào.
            BioFeedback.ShowToast("Cầm kéo rồi trỏ vào khay mẫu (rong đuôi chó, lá) để cắt.");
        }
    }
}
