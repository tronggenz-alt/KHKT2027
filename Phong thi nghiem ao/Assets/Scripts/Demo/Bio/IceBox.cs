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
    public class IceBox : MonoBehaviour, IInteractable
    {
        public string UseHint => "Khay đá 0 – 4 °C giữ Master Mix (có enzyme Taq), mồi, ADN khuôn. Enzyme để ở nhiệt độ phòng lâu sẽ giảm hoạt tính.";
        public void Use(GameObject user) => MolUi.Say(UseHint);
    }
}
