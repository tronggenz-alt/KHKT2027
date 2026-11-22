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
    public class IncubatorController : MonoBehaviour, IInteractable, IScrollAdjustable, IMachinePanel
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

        [NonSerialized] public float panelHours = 24f;
        string panelResult = "";

        /// <summary>Bảng điều khiển: đặt nhiệt độ và thời gian ủ rồi chạy.</summary>
        public MachineSpec PanelSpec()
        {
            var s = new MachineSpec { code = "MÁY · TỦ ẤM VI SINH", title = "Tủ ấm vi sinh", subtitle = "Ủ đĩa Petri ở nhiệt độ ổn định, tua nhanh thời gian ủ" };
            s.parameters.Add(MachineParam.Slider("Nhiệt độ ủ", "°C", 20f, 60f, 0.5f, () => targetTempC, v => { targetTempC = v; currentTempC = v; }, "0.0",
                v => v < 30f ? "Dưới 30 °C: vi khuẩn ưa ấm mọc chậm, hợp với nấm mốc" : v <= 39f ? "35–37 °C: tối ưu cho E. coli và đa số vi khuẩn gây bệnh ở người" : v <= 45f ? "Trên 40 °C: phần lớn vi khuẩn ưa ấm bị ức chế" : "Trên 45 °C: chỉ vi khuẩn ưa nhiệt mọc được, thạch dễ khô nứt"));
            s.parameters.Add(MachineParam.Slider("Thời gian ủ", "giờ", 2f, 72f, 2f, () => panelHours, v => panelHours = v, "0",
                v => v < 16f ? "Dưới 16 giờ: khuẩn lạc còn rất nhỏ, khó đếm" : v <= 48f ? "18–48 giờ: khuẩn lạc đủ lớn để đếm và nhận dạng" : "Trên 48 giờ: khuẩn lạc mọc lan, dính vào nhau"));
            s.runLabel = () => "Bắt đầu ủ";
            s.status = () => $"Nhiệt độ buồng: {BioFeedback.FormatVi(currentTempC, "0.0")} °C\nSố đĩa trong tủ: {dishesInside.Count}\nTổng thời gian đã ủ: {BioFeedback.FormatVi(incubatedHours, "0")} giờ";
            s.result = () => panelResult;
            s.run = () =>
            {
                FastForwardHours(panelHours);
                panelResult = dishesInside.Count == 0
                    ? $"Đã chạy tủ {BioFeedback.FormatVi(panelHours, "0")} giờ ở {BioFeedback.FormatVi(currentTempC, "0.0")} °C nhưng trong tủ chưa có đĩa nào.\nĐặt đĩa Petri (úp ngược) vào tủ rồi chạy lại."
                    : $"Đã ủ {dishesInside.Count} đĩa trong {BioFeedback.FormatVi(panelHours, "0")} giờ ở {BioFeedback.FormatVi(currentTempC, "0.0")} °C.\nLấy đĩa ra, đặt lên máy đếm khuẩn lạc để đọc kết quả.";
                return panelResult;
            };
            return s;
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
