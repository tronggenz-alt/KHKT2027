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
            : "Dao lam · Cầm rồi trỏ vào khay mẫu để rạch";

        public void Use(GameObject user)
        {
            // Trước đây bấm dao lam ở đâu cũng ghi "đã rạch vảy hành": nay chỉ hướng dẫn, việc rạch làm khi trỏ dao vào khay mẫu.
            BioFeedback.ShowToast("Cầm dao lam rồi trỏ vào khay mẫu (vảy hành, lá) để rạch. Xong đặt dao lại vào khay.");
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
