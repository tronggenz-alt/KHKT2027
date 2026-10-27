using System.Globalization;
using Khkt.Simulation.Chem;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Máy quang phổ / máy so màu: đặt cuvet (ống nghiệm) vào ổ đo, màn hình hiện độ hấp thụ A ở bước sóng
    /// hấp thụ mạnh nhất (tính từ màu dung dịch theo định luật Beer: A = −lg T).
    /// </summary>
    public class Spectrophotometer : MonoBehaviour
    {
        public PlaceSocket slot;
        public TextMesh display;

        static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
        public string Reading { get; private set; } = "";

        void Update()
        {
            var c = slot != null && slot.Occupant != null ? slot.Occupant.GetComponent<LabContainer>() : null;
            if (c == null || c.VolumeMl < 0.5f) Reading = "Đặt cuvet";
            else
            {
                var look = c.Look;
                // Màu truyền qua → độ hấp thụ từng vùng (đỏ ~650 nm, lục ~530 nm, lam ~450 nm)
                float r = Mathf.Max(1e-3f, look.Liquid.r / 0.86f), g = Mathf.Max(1e-3f, look.Liquid.g / 0.93f), b = Mathf.Max(1e-3f, look.Liquid.b);
                float ar = -Mathf.Log10(Mathf.Min(1, r)), ag = -Mathf.Log10(Mathf.Min(1, g)), ab = -Mathf.Log10(Mathf.Min(1, b));
                ar += look.Turbidity; ag += look.Turbidity; ab += look.Turbidity;
                int nm = ar >= ag && ar >= ab ? 650 : ag >= ab ? 530 : 450;
                float a = Mathf.Max(ar, Mathf.Max(ag, ab));
                Reading = $"λ {nm} nm  A = {a.ToString("0.000", Vi)}";
            }
            if (display != null && display.text != Reading) display.text = Reading;
        }
    }
}
