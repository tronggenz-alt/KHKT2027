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
    /// 5.1.16 & 5.2.4. Tủ ấm vi sinh (20..60 °C, có thanh tua thời gian 24h/48h) — mã: tu_am
    /// </summary>
    [DisallowMultipleComponent]
    public class IncubatorController : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public float targetTempC = 37.0f;
        public float currentTempC = 37.0f;
        public float incubatedHours = 0f;
        public bool doorOpen = false;
        public List<PetriDish> dishesInside = new List<PetriDish>();
        public IncubatorProgram program = new IncubatorProgram();

        void Awake()
        {
            if (program == null) program = new IncubatorProgram();
            program.incubator = this;
        }

        public string UseHint => $"Tủ ấm vi sinh [Đặt: {BioFeedback.FormatVi(targetTempC, "0.0")} °C · Thực: {BioFeedback.FormatVi(currentTempC, "0.0")} °C · Đã ủ: {BioFeedback.FormatVi(incubatedHours, "0.0")} giờ · {dishesInside.Count} đĩa] · Bấm: ủ tua 24 giờ · Con lăn: đổi nhiệt độ";

        public void PlaceDish(PetriDish dish)
        {
            if (dish == null) return;
            if (!dish.isInverted)
            {
                dish.hasCondensationSpread = true;
                BioFeedback.RecordMistake("tu_am", "dat_dia_petri_ngua_trong_tu_am",
                    "Đặt đĩa Petri NGỬA (nắp ở trên) vào tủ ấm!",
                    "Khi ủ ở 37 °C, hơi nước từ thạch bốc lên đọng trên nắp nhựa rồi nhỏ ngược xuống mặt thạch làm các khuẩn lạc bị loang lẫn vào nhau. Phải ÚP NGƯỢC đĩa Petri (đáy thạch ở trên, nắp ở dưới) khi ủ.");
            }
            if (!dishesInside.Contains(dish)) dishesInside.Add(dish);
            BioFeedback.PlayClick();
            BioFeedback.LogAction("tu_am", "dat_dia_vao_tu_am",
                $"Đã đặt đĩa Petri '{dish.labelText}' ({(dish.isInverted ? "úp ngược đúng chuẩn" : "đặt ngửa — sai quy trình!")}) vào tủ ấm {BioFeedback.FormatVi(targetTempC, "0.0")} °C.");
        }

        public void FastForwardHours(float hours)
        {
            currentTempC = targetTempC;
            incubatedHours += hours;
            for (int i = 0; i < dishesInside.Count; i++)
            {
                if (dishesInside[i] != null)
                {
                    dishesInside[i].Incubate(hours, currentTempC);
                }
            }
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("tu_am", "tua_thoi_gian_u",
                $"Đã ủ trong tủ ấm ở {BioFeedback.FormatVi(currentTempC, "0.0")} °C thêm {BioFeedback.FormatVi(hours, "0")} giờ (tổng {BioFeedback.FormatVi(incubatedHours, "0")} giờ).");
        }

        public void Use(GameObject user)
        {
            FastForwardHours(24f);
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            targetTempC = Mathf.Clamp(targetTempC + Mathf.Sign(delta) * 1.0f, 20f, 60f);
            currentTempC = targetTempC;
        }
    }
}
