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
    /// 5.1.21. Thùng rác nguy hại sinh học (màu vàng/đỏ có biểu tượng biohazard, nắp đạp chân) — mã: thung_rac_nguy_hai
    /// </summary>
    [DisallowMultipleComponent]
    public class BiohazardBin : MonoBehaviour, IInteractable
    {
        public int disposedItemsCount = 0;
        public BioHinge lidHinge;

        public string UseHint => $"Thùng rác nguy hại sinh học [Đã thu gom {disposedItemsCount} vật tư nhiễm sinh học] · Bấm: đạp chân mở nắp & bỏ rác nguy hại";

        public void DisposeBiohazardItem(string itemName, bool isSharpObject = false)
        {
            if (isSharpObject)
            {
                BioFeedback.RecordMistake("thung_rac_nguy_hai", "bo_vat_sac_vao_tui_nhua_nguy_hai",
                    $"Bỏ vật sắc nhọn ({itemName}) vào túi nilon thùng rác nguy hại!",
                    "Kim tiêm, dao lam, lam kính vỡ sẽ đâm thủng túi nhựa gây thương tích. Phải bỏ vật sắc nhọn vào hộp cứng màu vàng (SharpsBin).");
                return;
            }
            disposedItemsCount++;
            if (lidHinge != null) lidHinge.SetOpen(true);
            BioFeedback.PlayClick();
            BioFeedback.LogAction("thung_rac_nguy_hai", "bo_rac_nguy_hai",
                $"Đã bỏ '{itemName}' vào thùng rác nguy hại sinh học (tổng: {disposedItemsCount} món).");
        }

        public void Use(GameObject user)
        {
            DisposeBiohazardItem("Đĩa Petri / Găng tay đã qua sử dụng", false);
        }
    }
}
