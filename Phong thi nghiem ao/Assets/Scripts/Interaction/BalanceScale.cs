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
        string format;
        int shownCenti = int.MinValue;
        bool shownErr;

        void Update()
        {
            float g = 0;
            Transform z = pan != null ? pan : transform;
            if (Time.time - lastScan > 0.5f)
            {
                // Lấy từ sổ đăng ký (không quét cả cảnh mỗi nửa giây): chỉ giữ dụng cụ đang bật trong vòng 2,5 m quanh đĩa cân
                lastScan = Time.time;
                Khkt.Rendering.LiveSet<Grabbable>.Collect(nearby, null, activeOnly: true);
                Vector3 zp = z.position;
                for (int i = nearby.Count - 1; i >= 0; i--)
                    if ((nearby[i].transform.position - zp).sqrMagnitude >= 2.5f * 2.5f) nearby.RemoveAt(i);
            }
            for (int i = 0; i < nearby.Count; i++)
            {
                var gr = nearby[i];
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
                // Chỉ dựng lại chữ khi số hiển thị thật sự đổi (trước đây mỗi khung hình tạo chuỗi mới và dựng lại lưới chữ)
                bool err = g > maxGrams;
                float shown = g - tare;
                int centi = Mathf.RoundToInt(shown * Mathf.Pow(10f, decimals));
                if (err != shownErr || centi != shownCenti || format == null)
                {
                    format ??= "0." + new string('0', decimals);
                    shownErr = err;
                    shownCenti = centi;
                    display.text = err ? "Err" : shown.ToString(format, Vi) + " g";
                }
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
