using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>Cánh tay robot cộng tác chạy chu trình mẫu (gắp - di chuyển - thả), và đèn trạng thái nhấp nháy.</summary>
    public class CobotDemo : MonoBehaviour
    {
        public Transform baseYaw, shoulder, elbow, wrist;
        public Renderer[] blinkLights;
        public float period = 6f;

        MaterialPropertyBlock mpb;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Update()
        {
            float t = Time.time * Mathf.PI * 2 / period;
            if (baseYaw != null) baseYaw.localRotation = Quaternion.Euler(0, 55f * Mathf.Sin(t), 0);
            if (shoulder != null) shoulder.localRotation = Quaternion.Euler(-35f + 20f * Mathf.Sin(t * 2), 0, 0);
            if (elbow != null) elbow.localRotation = Quaternion.Euler(70f + 25f * Mathf.Cos(t * 2), 0, 0);
            if (wrist != null) wrist.localRotation = Quaternion.Euler(-30f, 90f * Mathf.Sin(t), 0);
            if (blinkLights == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            for (int i = 0; i < blinkLights.Length; i++)
            {
                var r = blinkLights[i];
                if (r == null) continue;
                bool on = Mathf.PerlinNoise(i * 3.7f, Time.time * 2.5f) > 0.45f;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionId, on ? new Color(0.3f, 2.2f, 1.2f) : new Color(0.02f, 0.1f, 0.05f));
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
