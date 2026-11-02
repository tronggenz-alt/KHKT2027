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
    /// 5.7. Nhiệt kế thủy ngân / rượu / điện tử (−10..110 °C) — mã: nhiet_ke
    /// </summary>
    [DisallowMultipleComponent]
    public class Thermometer : MonoBehaviour, IInteractable
    {
        public float currentReadingC = 25.0f;

        public string UseHint => $"Nhiệt kế phòng thí nghiệm (−10 .. 110 °C) [Số đọc hiện tại: {BioFeedback.FormatVi(currentReadingC, "0.1")} °C] · Bấm: nhúng bầu nhiệt kế đọc nhiệt độ dung dịch";

        public float MeasureTemperature(float containerTempC, bool touchedHotBottomMistake = false)
        {
            currentReadingC = touchedHotBottomMistake ? containerTempC + 14.0f : containerTempC;
            if (touchedHotBottomMistake)
            {
                BioFeedback.RecordMistake("nhiet_ke", "cham_bau_nhiet_ke_vao_day_coc_dang_dun",
                    "Để bầu nhiệt kế chạm sát đáy cốc thủy tinh đang đun nóng!",
                    "Đáy cốc tiếp xúc trực tiếp với mặt bếp có nhiệt độ cao hơn lòng chất lỏng làm số đọc bị vọt sai lệch.");
            }
            else
            {
                BioFeedback.PlayClick();
                BioFeedback.LogAction("nhiet_ke", "do_nhiet_do_dung_dich",
                    $"Nhúng bầu nhiệt kế giữa lòng chất lỏng: đọc được T = {BioFeedback.FormatVi(currentReadingC, "0.1")} °C.");
            }
            return currentReadingC;
        }

        public void Use(GameObject user)
        {
            MeasureTemperature(37.0f, false);
        }
    }
}
