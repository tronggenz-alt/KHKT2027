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
    public class GelCastingTray : MonoBehaviour
    {
        public bool gelReady;
        public float agarosePercent = 1.0f;
        public const int Wells = 8;
        GameObject gelVisual;

        public void Cast(float pct)
        {
            gelReady = true;
            agarosePercent = pct;
            if (gelVisual == null)
            {
                gelVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gelVisual.name = "Bản gel đã đông";
                UnityEngine.Object.Destroy(gelVisual.GetComponent<Collider>());
                gelVisual.transform.SetParent(transform, false);
                var vis = transform.Find("Visual");
                float top = vis != null ? vis.localPosition.y + vis.localScale.y * 0.5f : 0.03f;
                gelVisual.transform.localPosition = new Vector3(0, top + 0.002f, 0);
                gelVisual.transform.localScale = new Vector3(0.11f, 0.006f, 0.09f);
                var r = gelVisual.GetComponent<Renderer>();
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                r.material = new Material(shader) { color = new Color(0.8f, 0.9f, 0.85f, 0.85f) };
            }
            BioFeedback.PlaySuccess();
            MolUi.Info(this, $"Khay đúc có bản gel agarose {BioFeedback.FormatVi(pct, "0.0")} % đã đông, lược 8 giếng đã rút. Cầm khay đặt vào bể điện di (giếng ở phía cực âm – dây đen).");
            MolUi.Say("Đã đổ gel, chờ đông 20 phút rồi rút lược thẳng đứng: 8 giếng. Đặt gel vào bể điện di.");
        }
    }
}
