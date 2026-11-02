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
    /// 5.1.20. Bồn rửa mắt khẩn cấp — mã: bon_rua_mat
    /// </summary>
    [DisallowMultipleComponent]
    public class EyeWashStation : MonoBehaviour, IInteractable
    {
        public bool isSpraying = false;

        public string UseHint => isSpraying
            ? "Bồn rửa mắt khẩn cấp [ĐANG PHUN 2 VÒI RỬA MẮT — đã ghi nhật ký sự cố] · Bấm để khóa"
            : "Bồn rửa mắt khẩn cấp · Kéo cần khi hóa chất/sinh phẩm bắn vào mắt (xối liên tục ≥ 15 phút)";

        public void Use(GameObject user)
        {
            isSpraying = !isSpraying;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("bon_rua_mat", isSpraying ? "kich_hoat_rua_mat_khan_cap" : "dong_bon_rua_mat",
                isSpraying ? "Đã kéo cần kích hoạt vòi rửa mắt khẩn cấp hai tia và gửi thông báo sự cố lên web!" : "Đã đóng van bồn rửa mắt khẩn cấp.");
        }
    }
}
