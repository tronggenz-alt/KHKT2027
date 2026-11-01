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
    /// 5.1.12. Lọ dầu soi kính (dầu cedar / tổng hợp, n = 1,515) — mã: lo_dau_soi
    /// </summary>
    [DisallowMultipleComponent]
    public class ImmersionOil : MonoBehaviour, IInteractable
    {
        public const float RefractiveIndex = 1.515f;
        public BioTool bioTool;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "lo_dau_soi";
            bioTool.toolDisplayName = "Lọ dầu soi kính (n = 1,515)";
            bioTool.kind = BioToolKind.ImmersionOilBottle;
        }

        public string UseHint => "Lọ dầu soi kính (n = 1,515) · Bấm để nhỏ 1 giọt dầu lên tiêu bản trước khi xoay vật kính 100×";

        public void Use(GameObject user)
        {
            var scopes = FindObjectsByType<MicroscopeController>(FindObjectsSortMode.None);
            for (int i = 0; i < scopes.Length; i++)
            {
                if (Vector3.Distance(transform.position, scopes[i].transform.position) < 1.5f)
                {
                    scopes[i].ApplyImmersionOil();
                    return;
                }
            }
            var slide = CoverSlip.FindNearestSlide(transform.position, 0.6f);
            if (slide != null && slide.sample != null)
            {
                slide.sample.hasImmersionOil = true;
                slide.RebuildHint();
            }
            BioFeedback.PlayBubblePop();
            BioFeedback.LogAction("lo_dau_soi", "nho_dau_soi", "Đã nhỏ dầu soi chiết suất n = 1,515 lên lam kính.");
        }
    }
}
