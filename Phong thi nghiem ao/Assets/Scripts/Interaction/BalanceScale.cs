using System.Globalization;
using Khkt.Demo;
using Khkt.Simulation.Chem;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Cân điện tử: cộng khối lượng mọi vật đặt trên đĩa (vỏ dụng cụ + chất chứa bên trong), hiện trên
    /// màn hình số. Bấm vào cân để trừ bì (đưa về 0).
    /// </summary>
    public class BalanceScale : MonoBehaviour, IDemoUsable
    {
        public Transform pan;
        public Vector3 zoneSize = new Vector3(0.16f, 0.2f, 0.16f);
        public TextMesh display;
        [Tooltip("Số chữ số sau dấu phẩy (2: 0,01 g; 4: 0,0001 g)")]
        public int decimals = 2;
        public float maxGrams = 620f;

        public float Grams { get; private set; }
        float tare;
        static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        public string UseHint => "Bấm chuột trái: trừ bì (về 0)";

        public void Use() => tare = Grams;

        readonly System.Collections.Generic.List<Grabbable> nearby = new System.Collections.Generic.List<Grabbable>();
        float lastScan = -10f;

        void Update()
        {
            float g = 0;
            Transform z = pan != null ? pan : transform;
            if (Time.time - lastScan > 0.5f)
            {
                lastScan = Time.time;
                nearby.Clear();
                foreach (var gr in FindObjectsByType<Grabbable>())
                    if (Vector3.Distance(gr.transform.position, z.position) < 2.5f) nearby.Add(gr);
            }
            foreach (var gr in nearby)
            {
                if (gr == null || gr.IsHeld) continue;
                Vector3 l = z.InverseTransformPoint(gr.transform.position);
                if (Mathf.Abs(l.x) > zoneSize.x / 2 || l.y < -0.01f || l.y > zoneSize.y || Mathf.Abs(l.z) > zoneSize.z / 2) continue;
                g += gr.massG;
                var c = gr.GetComponent<LabContainer>();
                if (c != null) g += ContentsGrams(c);
            }
            Grams = g;
            if (display != null)
            {
                float shown = g - tare;
                display.text = g > maxGrams ? "Err" : shown.ToString("0." + new string('0', decimals), Vi) + " g";
            }
        }

        public static float ContentsGrams(LabContainer c)
        {
            var mix = c.Total.Mix;
            double g = c.VolumeMl;  // nước, dung dịch loãng ≈ 1 g/ml
            for (int i = 0; i < mix.Count; i++)
            {
                var d = mix.SpeciesAt(i);
                if (d.IsSolidPhase) g += mix.MolAt(i) * d.MolarMass;
            }
            return (float)g;
        }
    }
}
