// Tách từ GreenhouseAndCommonTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.5.5. Bộ dụng cụ sang chậu trồng cây (3 bước: cho đất -> đặt cây -> tưới định gốc) — mã: bo_sang_chau
    /// </summary>
    [DisallowMultipleComponent]
    public class PottingKit : MonoBehaviour, IInteractable
    {
        public int step = 0; // 0..3
        static readonly string[] StepNames =
        {
            "Bước 1/3: Xúc giá thể đất tơi xốp + xơ dừa vào 2/3 chậu",
            "Bước 2/3: Đặt bầu cây con thẳng đứng giữa chậu và lấp đất vừa ngang cổ rễ",
            "Bước 3/3: Tưới nước nhẹ nhàng định gốc"
        };

        public string UseHint => step >= 3
            ? "Bộ dụng cụ sang chậu trồng cây [Đã hoàn tất sang chậu 3 bước — cây con đứng vững]"
            : $"Bàn sang chậu thực vật [{StepNames[step]}] · Bấm để thực hiện";

        public void Use(GameObject user)
        {
            if (step >= 3) step = 0;
            string msg = StepNames[step];
            step++;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("bo_sang_chau", $"sang_chau_buoc_{step}", $"Thao tác sang chậu: {msg}.");
        }
    }
}
