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
    /// 5.1.3. Lamen (lá kính đậy 22 × 22 × 0,17 mm) — mã: lamen
    /// </summary>
    [DisallowMultipleComponent]
    public class CoverSlip : MonoBehaviour, IInteractable
    {
        public BioTool bioTool;
        public bool isBroken = false;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "lamen";
            bioTool.toolDisplayName = "Lamen (22 × 22 × 0,17 mm)";
            bioTool.kind = BioToolKind.Coverslip;
            bioTool.holdDurationSeconds = 0.6f; // Giữ 0,6 s để hạ nghiêng 45° -> 0° không bọt khí
        }

        public string UseHint => isBroken
            ? "Lamen bị vỡ — bỏ vào thùng vật sắc nhọn"
            : "Lamen (22 × 22 × 0,17 mm) · Đưa tới lam kính và GIỮ 0,6 s để hạ nghiêng 45° (bấm nhanh sẽ sinh bọt khí)";

        public void Use(GameObject user)
        {
            var slide = FindNearestSlide(transform.position, 0.35f);
            if (slide != null)
            {
                // Bấm nhanh (không giữ) -> thả rơi thẳng góc 85° tạo bọt khí (5.1.3 bước 4)
                BioTransfer.LowerCoverslip(slide.sample, 85f);
                slide.RebuildHint();
            }
            else
            {
                BioFeedback.ShowToast("Hãy đưa lamen tới gần lam kính có mẫu (giữ 0,6 s để hạ góc 45° không bọt khí).");
            }
        }

        public void ApplySmooth45DegreeDrop(GlassSlide slide)
        {
            if (slide == null) return;
            BioTransfer.LowerCoverslip(slide.sample, 45f);
            slide.RebuildHint();
        }

        public static GlassSlide FindNearestSlide(Vector3 pos, float maxDist)
        {
            var slides = FindObjectsByType<GlassSlide>(FindObjectsSortMode.None);
            GlassSlide best = null;
            float bestD = maxDist;
            for (int i = 0; i < slides.Length; i++)
            {
                float d = Vector3.Distance(pos, slides[i].transform.position);
                if (d < bestD) { bestD = d; best = slides[i]; }
            }
            return best;
        }
    }
}
