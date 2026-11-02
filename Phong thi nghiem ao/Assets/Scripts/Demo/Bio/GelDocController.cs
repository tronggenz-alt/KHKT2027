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
    public class GelDocController : MonoBehaviour, IInteractable
    {
        public GelTankController sourceTank;
        public bool captured;
        GelDocProgram program;

        public string UseHint => "Buồng chụp gel (ánh sáng xanh 470 nm + kính lọc cam) · Bấm: mở phần mềm chụp và phân tích gel";

        public void Use(GameObject user)
        {
            if (sourceTank == null)
            {
                float best = float.MaxValue;
                foreach (var t in FindObjectsByType<GelTankController>())
                {
                    float d = Vector3.Distance(t.transform.position, transform.position);
                    if (d < best) { best = d; sourceTank = t; }
                }
            }
            program ??= new GelDocProgram { gelDoc = this };
            InstrumentScreen.Open(null, program, null);
        }
    }
}
