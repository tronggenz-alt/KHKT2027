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
    /// 5.7. Bút lông dầu kháng cồn & nhãn dán — mã: but_ghi_nhan
    /// </summary>
    [DisallowMultipleComponent]
    public class LabelPen : MonoBehaviour, IInteractable
    {
        public string currentLabel = "E. coli K-12 · 10⁻⁵ · 31/10";
        public bool labeledOnLidMistake = false;

        public string UseHint => $"Bút lông dầu kháng cồn & Nhãn dán [Nhãn: '{currentLabel}'] · Ghi lên ĐÁY đĩa Petri / THÂN ống nghiệm (không ghi lên nắp rời!)";

        public void WriteLabel(bool writeOnRemovableLidMistake = false)
        {
            labeledOnLidMistake = writeOnRemovableLidMistake;
            if (writeOnRemovableLidMistake)
            {
                BioFeedback.RecordMistake("but_ghi_nhan", "ghi_nhan_len_nap_dia_petri",
                    "Ghi tên mẫu lên NẮP đĩa Petri thay vì ghi ở ĐÁY đĩa thạch!",
                    "Khi mở nắp hoặc nếu nắp bị rơi/tráo đổi giữa các đĩa, sẽ không thể biết đáy thạch chứa mẫu nào. Quy tắc bắt buộc: luôn ghi nhãn ở mép ĐÁY đĩa Petri và THÂN ống nghiệm.");
                return;
            }
            BioFeedback.PlayClick();
            BioFeedback.LogAction("but_ghi_nhan", "ghi_nhan_dung_quy_cach",
                $"Đã dùng bút lông kháng cồn ghi nhãn '{currentLabel}' vào đáy đĩa Petri / thân ống nghiệm.");
        }

        public void Use(GameObject user)
        {
            WriteLabel(false);
        }
    }
}
