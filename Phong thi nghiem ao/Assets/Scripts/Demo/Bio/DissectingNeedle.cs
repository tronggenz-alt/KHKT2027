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
    /// 5.1.4. Kim mũi mác — mã: kim_mui_mac
    /// </summary>
    [DisallowMultipleComponent]
    public class DissectingNeedle : MonoBehaviour, IInteractable
    {
        public BioTool bioTool;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "kim_mui_mac";
            bioTool.toolDisplayName = "Kim mũi mác";
            bioTool.kind = BioToolKind.DissectingNeedle;
            bioTool.holdDurationSeconds = 0.4f;
        }

        public string UseHint => "Kim mũi mác · Dàn phẳng biểu bì trên lam kính và đỡ lamen hạ góc 45°";

        public void Use(GameObject user)
        {
            var slide = CoverSlip.FindNearestSlide(transform.position, 0.4f);
            if (slide != null && slide.sample != null)
            {
                BioTransfer.SpreadSpecimenWithNeedle(slide.sample);
                slide.RebuildHint();
            }
            else
            {
                BioFeedback.ShowToast("Đưa kim mũi mác tới lam kính để dàn phẳng mẫu biểu bì.");
            }
        }
    }
}
