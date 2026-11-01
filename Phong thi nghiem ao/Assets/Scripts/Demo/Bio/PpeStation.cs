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
    /// 5.1.21 & 5.7. Giá treo đồ bảo hộ cá nhân (Áo blouse trắng, găng tay nitrile, kính bảo hộ) — mã: do_bao_ho
    /// </summary>
    [DisallowMultipleComponent]
    public class PpeStation : MonoBehaviour, IInteractable
    {
        public bool wearingCoat = false;
        public bool wearingGloves = false;
        public bool wearingGoggles = false;

        public string UseHint => BioTransfer.PpeEquipped
            ? "Trạm đồ bảo hộ cá nhân (PPE) [Đang mặc đủ: Áo blouse + Găng tay nitrile + Kính bảo hộ (daMacBaoHo = true)] · Bấm: cởi đồ bảo hộ trước khi rời khu"
            : "Trạm đồ bảo hộ cá nhân (Áo blouse, Găng tay nitrile, Kính bảo hộ) · Bấm để mặc đầy đủ PPE trước khi vào thí nghiệm";

        public void EquipAll(bool equip)
        {
            wearingCoat = equip;
            wearingGloves = equip;
            wearingGoggles = equip;
            BioTransfer.PpeEquipped = equip;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("do_bao_ho", equip ? "mac_do_bao_ho" : "coi_do_bao_ho",
                equip
                    ? "Đã mặc đầy đủ áo blouse, đeo găng tay nitrile và kính bảo hộ (daMacBaoHo = true)."
                    : "Đã tháo bỏ găng tay và treo áo blouse đúng quy định trước khi rời phòng.");
        }

        public void Use(GameObject user)
        {
            EquipAll(!BioTransfer.PpeEquipped);
        }
    }
}
