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
    /// 5.1.4. Dao lam — mã: dao_lam
    /// </summary>
    [DisallowMultipleComponent]
    public class RazorBlade : MonoBehaviour, IInteractable
    {
        public BioTool bioTool;
        public bool scoredOnionGrid = false;
        public bool placedSafelyInTray = true;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "dao_lam";
            bioTool.toolDisplayName = "Dao lam rạch mẫu";
            bioTool.kind = BioToolKind.Scalpel;
            bioTool.holdDurationSeconds = 0.5f;
        }

        public string UseHint => scoredOnionGrid
            ? "Dao lam (đã rạch ô vuông 1 cm trên vảy hành) · Đặt lại vào hộp trên khay an toàn"
            : "Dao lam · Bấm để rạch ô vuông 1 × 1 cm trên mặt vảy hành";

        public void Use(GameObject user)
        {
            scoredOnionGrid = true;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("dao_lam", "rach_o_vuong_hanh", "Đã dùng dao lam rạch các ô vuông 1 × 1 cm trên mặt trong vảy hành tím.");
        }

        public void CheckSafePlacement(bool inTray)
        {
            placedSafelyInTray = inTray;
            if (!inTray)
            {
                BioFeedback.RecordMistake("dao_lam", "dat_dao_lam_ngoai_khay",
                    "Đặt dao lam trần ngoài khay dụng cụ!",
                    "Lưỡi dao lam rất sắc, phải đặt ngay ngắn trong hộp/khay quy định sau khi rạch mẫu hoặc bỏ vào thùng vật sắc nhọn màu vàng.");
            }
        }
    }
}
