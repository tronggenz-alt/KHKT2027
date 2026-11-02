// Tách từ MolecularTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    [DisallowMultipleComponent]
    public class Od600Controller : MonoBehaviour, IInteractable
    {
        public bool blankDone;
        public double hours = 0.0;
        public double measuredOd600;
        public readonly List<Vector2> curve = new List<Vector2>();
        Od600Program program;

        public double CellsPerMl => measuredOd600 * 8.0e8;
        public string UseHint => "Máy quang phổ OD600 / ADN · Bấm: mở màn hình đo";

        public void Use(GameObject user)
        {
            program ??= new Od600Program { controller = this };
            InstrumentScreen.Open(null, program, null);
        }

        /// <summary>Đo dịch E. coli nuôi lắc ở 37 °C sau h giờ (đường cong sinh trưởng ColonyGrowth).</summary>
        public double MeasureAt(double h)
        {
            hours = h;
            double cells = ColonyGrowth.PopulationAtTime(h, 1.0e6, 1.0e9, 1.0);
            measuredOd600 = ColonyGrowth.OpticalDensity600(cells) + (blankDone ? 0 : 0.38);
            curve.Add(new Vector2((float)h, (float)measuredOd600));
            if (!blankDone)
                BioFeedback.RecordMistake("may_quang_pho_od600", "quen_do_mau_trang", "Đo OD600 mà chưa đo mẫu trắng (môi trường LB sạch)",
                    "Môi trường LB màu vàng tự hấp thụ ánh sáng 600 nm: mọi số đo bị cộng thêm khoảng 0,38.");
            return measuredOd600;
        }
    }
}
