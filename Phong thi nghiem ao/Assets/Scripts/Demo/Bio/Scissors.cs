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

        public string UseHint => "Kéo mổ nhỏ · Cắt lá rong đuôi chó / lá thài lài tía thành mẩu nhỏ";

        public void Use(GameObject user)
        {
            BioFeedback.PlayClick();
            BioFeedback.LogAction("keo_nho", "cat_mau_la", "Đã dùng kéo nhỏ cắt cành/lá mẫu thực vật.");
        }
    }
}
