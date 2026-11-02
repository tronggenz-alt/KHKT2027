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
    public class Cuvette : MonoBehaviour
    {
        void Start() => MolUi.Info(this, "Cuvet 1 cm: cầm ở hai mặt nhám, hai mặt trong cho tia sáng đi qua. Lau sạch vân tay bằng giấy lau kính trước khi đặt vào máy.");
    }
}
